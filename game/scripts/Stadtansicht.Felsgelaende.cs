using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Das Gelände unter den Felsen als Bild für den Fels-Shader (Fels R1) – ein Teil der <c>partial class Stadtansicht</c>. Der Fels soll am Fuß die Farbe
/// des Bodens annehmen (wie die Runtime Virtual Texture in Unreal, nur kleiner): Dafür muss jeder Bildpunkt eines Stücks wissen, wie hoch er über
/// dem Gelände liegt – bei großen, gekippten Stücken am Hang genau an seiner Stelle, nicht nur unter der Mitte des Stücks.
/// <list type="bullet">
/// <item><b>Bild</b>: die Höhen der Rasterpunkte des Bodens (2 m, <see cref="Gelaendeform.Ausschnitt"/>, mit dem Bett der Wege) im Rechteck um alle
/// Felsstücke samt <see cref="FelsGelaendeRand"/> und <see cref="FelsGelaendeVorrat"/>, ein Kanal Halbfloat (Rh). Köln mit 300 Stücken: etwa 730 × 580 Punkte, unter 1 MB.</item>
/// <item><b>Globale Uniforms</b> <c>fels_gelaende</c> (das Bild) und <c>fels_gelaende_feld</c> (Ecke und 1 / Ausdehnung; z = 0: kein Bild) –
/// <c>fels.gdshaderinc</c> liest sie mit einer Abtastung je Bildpunkt (bilinear, wie der Kern; das Netz teilt die Zellen in Dreiecke, der Unterschied
/// ist am Fuß nur Zentimeter).</item>
/// <item><b>Nachführen</b>: Kommen Stücke außerhalb des Rechtecks dazu, wird es neu und größer gebaut; ändert sich das Gelände (Werkstatt), nur der
/// Ausschnitt darin. Beim Verlassen der Stadt aus (<see cref="FelsGelaendeAufgeben"/>).</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>So weit (Meter) reicht das Bild über die Mitten der Stücke hinaus: halbe Breite des größten Stücks samt Kippen und Spielraum.</summary>
    private const float FelsGelaendeRand = 30f;

    /// <summary>Höchstens so viele Rasterpunkte je Seite (4 km): größere Karten gibt es nicht, aber ein verirrtes Stück soll kein Riesenbild bauen.</summary>
    private const int FelsGelaendeMax = 2048;

    /// <summary>Vorrat je Seite beim Neubau (Rasterpunkte, 128 m): Der Pinsel setzt Stück für Stück nach außen, nicht jedes soll das Bild neu bauen.</summary>
    private const int FelsGelaendeVorrat = 64;

    private Image? _felsGelaendeBild;
    private ImageTexture? _felsGelaendeTextur;

    /// <summary>Das Rechteck des Bildes in Rasterpunkten: erster Punkt und Zahl je Richtung.</summary>
    private (int I0, int J0, int Breite, int Tiefe) _felsGelaendeFeld;

    private bool _felsGelaendeNeu;
    private Rect2? _felsGelaendeGeaendert;

    /// <summary>Stücke kamen dazu oder gingen weg: beim nächsten Nachziehen prüfen, ob das Rechteck noch reicht.</summary>
    private void FelsGelaendeVormerken() => _felsGelaendeNeu = true;

    /// <summary>Das Gelände hat sich im Rechteck geändert (Werkstatt): den Ausschnitt beim nächsten Nachziehen neu lesen.</summary>
    private void FelsGelaendeBereich(Rect2 rect) => _felsGelaendeGeaendert = _felsGelaendeGeaendert is { } alt ? alt.Merge(rect) : rect;

    /// <summary>Baut bzw. ergänzt das Bild (aus <see cref="FelsenNachziehen"/>).</summary>
    private void FelsGelaendeNachziehen()
    {
        if (_felsGelaendeNeu)
        {
            _felsGelaendeNeu = false;
            if (FelsGelaendeGebraucht() is { } feld && !FelsGelaendeEnthaelt(feld))
                FelsGelaendeBauen((feld.I0 - FelsGelaendeVorrat, feld.J0 - FelsGelaendeVorrat,
                    Math.Min(FelsGelaendeMax, feld.Breite + 2 * FelsGelaendeVorrat), Math.Min(FelsGelaendeMax, feld.Tiefe + 2 * FelsGelaendeVorrat)));
        }
        if (_felsGelaendeGeaendert is { } rect && _felsGelaendeBild is not null)
        {
            _felsGelaendeGeaendert = null;
            FelsGelaendeAusschnitt(rect);
        }
        else _felsGelaendeGeaendert = null;
    }

    /// <summary>Das Rechteck (Rasterpunkte) um alle Stücke mit Rand; null ohne Stücke.</summary>
    private (int I0, int J0, int Breite, int Tiefe)? FelsGelaendeGebraucht()
    {
        if (_felsBilder.Count == 0) return null;
        var (x0, z0, x1, z1) = (double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
        foreach (var b in _felsBilder.Values)
            (x0, z0, x1, z1) = (Math.Min(x0, b.X), Math.Min(z0, b.Z), Math.Max(x1, b.X), Math.Max(z1, b.Z));
        var a = Hoehenraster.Abstand;
        var i0 = (int)Math.Floor((x0 - FelsGelaendeRand) / a);
        var j0 = (int)Math.Floor((z0 - FelsGelaendeRand) / a);
        var i1 = (int)Math.Ceiling((x1 + FelsGelaendeRand) / a);
        var j1 = (int)Math.Ceiling((z1 + FelsGelaendeRand) / a);
        return (i0, j0, Math.Min(FelsGelaendeMax, i1 - i0 + 1), Math.Min(FelsGelaendeMax, j1 - j0 + 1));
    }

    /// <summary>Liegt das gebrauchte Rechteck im vorhandenen Bild?</summary>
    private bool FelsGelaendeEnthaelt((int I0, int J0, int Breite, int Tiefe) f)
    {
        var g = _felsGelaendeFeld;
        return _felsGelaendeBild is not null && f.I0 >= g.I0 && f.J0 >= g.J0 && f.I0 + f.Breite <= g.I0 + g.Breite && f.J0 + f.Tiefe <= g.J0 + g.Tiefe;
    }

    /// <summary>
    /// Die Höhen eines Ausschnitts als Bild (Zeile für Zeile; auf flachem Boden überall 0) im Format Rh: Halbfloat lässt sich überall linear filtern
    /// (32-Bit-Float nicht auf jeder Grafikkarte) und ist bis 64 m Höhe auf 3 cm genau.
    /// </summary>
    private Image FelsGelaendeHoehen(int i0, int j0, int breite, int tiefe)
    {
        var werte = _gelaende is { } g ? g.Ausschnitt(i0, j0, breite, tiefe) : new float[breite * tiefe];
        var bytes = new byte[werte.Length * 4];
        Buffer.BlockCopy(werte, 0, bytes, 0, bytes.Length);
        var bild = Image.CreateFromData(breite, tiefe, false, Image.Format.Rf, bytes);
        bild.Convert(Image.Format.Rh);
        return bild;
    }

    private void FelsGelaendeBauen((int I0, int J0, int Breite, int Tiefe) f)
    {
        _felsGelaendeFeld = f;
        _felsGelaendeBild = FelsGelaendeHoehen(f.I0, f.J0, f.Breite, f.Tiefe);
        _felsGelaendeTextur = ImageTexture.CreateFromImage(_felsGelaendeBild);
        var a = (float)Hoehenraster.Abstand;
        RenderingServer.GlobalShaderParameterSet("fels_gelaende", _felsGelaendeTextur);
        // Punkt k liegt bei (I0 + k) · a, die Mitte seines Bildpunkts bei (k + ½) / Breite: Ecke eine halbe Zelle davor
        RenderingServer.GlobalShaderParameterSet("fels_gelaende_feld",
            new Vector4(f.I0 * a - a / 2, f.J0 * a - a / 2, 1f / (f.Breite * a), 1f / (f.Tiefe * a)));
    }

    /// <summary>Liest die Höhen im Rechteck (Meter) neu ins Bild und lädt es hoch.</summary>
    private void FelsGelaendeAusschnitt(Rect2 rect)
    {
        var f = _felsGelaendeFeld;
        var a = Hoehenraster.Abstand;
        var i0 = Math.Max(f.I0, (int)Math.Floor(rect.Position.X / a) - 1);
        var j0 = Math.Max(f.J0, (int)Math.Floor(rect.Position.Y / a) - 1);
        var i1 = Math.Min(f.I0 + f.Breite - 1, (int)Math.Ceiling(rect.End.X / a) + 1);
        var j1 = Math.Min(f.J0 + f.Tiefe - 1, (int)Math.Ceiling(rect.End.Y / a) + 1);
        if (i0 > i1 || j0 > j1) return;
        var (b, t) = (i1 - i0 + 1, j1 - j0 + 1);
        var stueck = FelsGelaendeHoehen(i0, j0, b, t);
        _felsGelaendeBild!.BlitRect(stueck, new Rect2I(0, 0, b, t), new Vector2I(i0 - f.I0, j0 - f.J0));
        _felsGelaendeTextur!.Update(_felsGelaendeBild);
    }

    /// <summary>Beim Verlassen der Stadt: kein Bild mehr (die Testszene und das Hauptmenü nehmen wieder den Boden unter der Mitte).</summary>
    private void FelsGelaendeAufgeben()
    {
        if (_felsGelaendeBild is null) return;
        RenderingServer.GlobalShaderParameterSet("fels_gelaende_feld", Vector4.Zero);
        // die Textur nicht festhalten: ein Punkt statt des ganzen Bildes
        RenderingServer.GlobalShaderParameterSet("fels_gelaende", ImageTexture.CreateFromImage(Image.CreateEmpty(1, 1, false, Image.Format.Rh)));
        _felsGelaendeBild = null;
        _felsGelaendeTextur = null;
    }
}
