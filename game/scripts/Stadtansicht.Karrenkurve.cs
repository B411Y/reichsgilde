using Godot;

namespace Reichsgilde;

/// <summary>
/// Prüfhilfe <c>--karrenkurve=1</c> (Teil der <c>partial class Stadtansicht</c>, Darstellung 22.11.2026): Die Kamera folgt nach drei Sekunden dem fahrenden Karren mit dem größten Knick in der
/// nahen Zoomstufe (<c>--karrenkurve=&lt;zoom&gt;</c> nimmt diesen Zoom), und alle zwei Sekunden steht eine Zeile <c>[karrenkurve]</c> im Protokoll: Abstand Zugpunkt–Deichsel (Soll, Ist, größte
/// Abweichung), Knickwinkel (jetzt, größter) und die Rechenkosten der Schleppkurve je Karren und Bild (Mikrosekunden, <see cref="Gespannlauf"/>).
/// </summary>
public partial class Stadtansicht
{
    private static readonly string? KarrenKurveOption = Wasserarten.Startoption("karrenkurve");
    /// <summary><c>--karrenstarr=1</c>: das Gespann dreht wie ein starres Teil (Zustand vor der Schleppkurve), nur zum Vergleichen für Bilder.</summary>
    private static readonly bool KarrenStarr = Wasserarten.Startoption("karrenstarr") is "1";
    private static readonly string? KarrenKurveArt = Wasserarten.Startoption("karrenkurveart");
    private KarrenAnsicht? _kurveKarren;
    private double _kurveZeit, _kurveMelde, _kurveUebersicht;
    private double _kurveAbweichung, _kurveKnickMax;
    private long _kurveTicks, _kurveAnzahl;

    /// <summary>Misst einen Aufruf von <see cref="Gespannlauf.Weiter"/> (nur mit <c>--karrenkurve</c>).</summary>
    private void KarrenKurveMessen(long start)
    {
        if (KarrenKurveOption is null or "0" or "aus") return;
        _kurveTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
        _kurveAnzahl++;
    }

    private static long KarrenKurveStart() => KarrenKurveOption is null or "0" or "aus" ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();

    /// <summary>Einmal je Bild (nur mit <c>--karrenkurve</c>).</summary>
    private void KarrenKurveFolgen(double delta)
    {
        if (KarrenKurveOption is null or "0" or "aus") return;
        _kurveZeit += delta;
        if (_kurveZeit < 3) return;
        _kurveUebersicht += delta;
        if (_kurveUebersicht > 10)
        {
            _kurveUebersicht = 0;
            GD.Print($"[karrenkurve] Karren {_karren.Count}, auf dem Weg {_karren.Values.Count(k => k.AufWeg)}, fahrend {_karren.Values.Count(k => k.AufWeg && k.Geschwindigkeit > 0.1f)}, mit Schleppkurve {_karren.Values.Count(k => k.Lauf is not null)}");
        }
        // Der erste fahrende Karren (der gewünschten Art, <c>--karrenkurveart=hand|pferd</c>) im Bild; er bleibt gewählt, solange er auf dem Weg ist (so sind Bilder mit und ohne Schleppkurve vergleichbar)
        if (_kurveKarren is null || !IsInstanceValid(_kurveKarren.Wurzel) || !_kurveKarren.AufWeg)
            _kurveKarren = _karren.Values.FirstOrDefault(k => k.Lauf is not null && k.AufWeg && k.Geschwindigkeit > 0.1f && (KarrenKurveArt is null || k.Art.Datei.EndsWith("_" + KarrenKurveArt)));
        if (_kurveKarren?.Lauf is not { } lauf) return;
        var lage = _kurveKarren.Wurzel.Position;
        var zoom = float.TryParse(KarrenKurveOption, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z) && z > 1 ? z : KameraMinZoom;
        KameraSetzen((float)lauf.ZugX * 0.5f + lage.X * 0.5f, (float)lauf.ZugZ * 0.5f + lage.Z * 0.5f, zoom);
        _kurveAbweichung = Math.Max(_kurveAbweichung, Math.Abs(lauf.DeichselIst - lauf.Deichsel));
        _kurveKnickMax = Math.Max(_kurveKnickMax, Math.Abs(lauf.Knick) * 180 / Math.PI);
        _kurveMelde += delta;
        if (_kurveMelde < 2) return;
        _kurveMelde = 0;
        var us = _kurveAnzahl > 0 ? _kurveTicks * 1e6 / System.Diagnostics.Stopwatch.Frequency / _kurveAnzahl : 0;
        GD.Print($"[karrenkurve] {_kurveKarren.Art.Datei}: Deichsel Soll {lauf.Deichsel:0.000} m, Ist {lauf.DeichselIst:0.000} m (größte Abweichung {_kurveAbweichung:0.000000} m), " +
                 $"Knick {lauf.Knick * 180 / Math.PI:0.0} Grad (größter {_kurveKnickMax:0.0}), Zugpunkt ({lauf.ZugX:0.0}, {lauf.ZugZ:0.0}), Achse ({lauf.VorneX:0.0}, {lauf.VorneZ:0.0}), Rechenkosten {us:0.00} µs je Karren und Bild ({_kurveAnzahl} Aufrufe)");
    }
}
