using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;

namespace Reichsgilde;

/// <summary>
/// Silhouetten der Bewohner hinter den leuchtenden Fenstern der Wohnhäuser (Nutzerwunsch): Nachts sieht man hin und wieder einen dunklen
/// Schattenriss – stehend, langsam vorbeigehend oder sitzend. <b>Wer</b> zu Hause ist, liest die Stadtansicht aus dem Kern
/// (<c>Einwohnerschaft.ImHaus</c>, nur lesend); Größe, Schulterbreite und Kopf kommen aus Volk und Geschlecht (<see cref="FigurMasse"/>, die
/// Maße der Figurenmodelle). <b>Gezeichnet</b> wird im Fenster-Shader (<c>fenster.gdshader</c>): je leuchtendem Fenster ein Schattenriss aus
/// einigen Ellipsen, ohne Modell, ohne Karte, ohne zusätzlichen Zeichenaufruf. Das Spiel setzt je Haus zwei Instanz-Uniforms
/// (<c>person_a</c>, <c>person_b</c>), das Fenstermaterial bleibt für alle Häuser einer Gruppe dasselbe. Nur Häuser nahe der Kamera
/// und nur bei nahem Zoom bekommen Personen; sonst bleibt alles null und der Shader tut nichts. Nur Darstellung, keine Zufallsquelle des Kerns.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Bis zu diesem Zoom (Kamerahöhe in Metern) zeigen die Fenster Silhouetten; weiter weg wären sie nur Flimmern.</summary>
    private const float SilhouettenMaxZoom = 75f;

    /// <summary>Alle zwei Zehntelsekunden Echtzeit schaut die Stadtansicht, wer wo zu Hause ist.</summary>
    private const float SilhouettenTakt = 0.4f;

    /// <summary>Die Fenster der Wohnhäuser nach Hausnummer (Wohnhäuser der Einwohnerschaft), zusammen mit den zuletzt gesetzten Personen.</summary>
    private readonly Dictionary<int, (ModellInstanz Modell, Vector4 A, Vector4 B)> _silhouettenHaeuser = new();
    /// <summary>Messschalter: Umgebungsvariable <c>RG_OHNESILHOUETTEN=1</c> schaltet die Silhouetten ab (Vergleich mit <c>--messung</c>).</summary>
    private static readonly bool SilhouettenAus = System.Environment.GetEnvironmentVariable("RG_OHNESILHOUETTEN") == "1";
    private double _silhouettenUhr;
    private bool _figurenAn;

    /// <summary>Merkt die Fenster eines Wohnhauses (aufgerufen von <see cref="WohnhausModell"/>).</summary>
    private void SilhouettenMelden(int nummer, ModellInstanz? modell)
    {
        if (modell is { Fenster.Length: > 0 }) _silhouettenHaeuser[nummer] = (modell, Vector4.Zero, Vector4.Zero);
    }

    /// <summary>
    /// Maße einer Person für den Shader: x = Körpergröße (m), y = Schulterbreite (m), z = Kopfradius (m), w = 1 bei Frauen. Größe aus der Hüfthöhe
    /// der Figur (Hüfte ist etwa 0,53 der Größe), Breite und Kopf nach Volk: Zwerge breit, Elben und Halblinge schmal, Orks und Oger massig.
    /// </summary>
    private static Vector4 SilhouettenMass(Volk volk, Geschlecht geschlecht)
    {
        var (_, huefte, _, groesse) = FigurMasse(volk);
        var hoehe = huefte * 1.9f * groesse.Y;
        var (breite, kopf) = volk switch
        {
            Volk.Zwerg => (0.58f, 0.115f),
            Volk.Elb => (0.38f, 0.10f),
            Volk.Halbling => (0.40f, 0.105f),
            Volk.Ork => (0.46f * groesse.X, 0.11f * groesse.Y),
            Volk.Oger => (0.46f * groesse.X, 0.11f * groesse.Y),
            _ => (0.46f, 0.11f),
        };
        var frau = geschlecht == Geschlecht.Frau;
        return new Vector4(hoehe, frau ? breite * 0.85f : breite, frau ? kopf * 0.96f : kopf, frau ? 1 : 0);
    }

    /// <summary>
    /// Schaltet die Silhouetten nach Zoom, Tageszeit und Kamera ein und setzt für die Häuser nahe der Bildmitte, wer gerade zu Hause ist.
    /// Je Bild nur eine Abfrage der Uhr; die Häuser fragt sie im <see cref="SilhouettenTakt"/>.
    /// </summary>
    private void SilhouettenAktualisieren(double delta)
    {
        if (_silhouettenHaeuser.Count == 0) return;
        var an = _raum is null && !SilhouettenAus && _kamera.Size <= SilhouettenMaxZoom && _fensterLicht > 0.5f;
        if (an != _figurenAn)
        {
            _figurenAn = an;
            foreach (var stoff in _fensterStoffe.Values) stoff.SetShaderParameter("figuren_an", an ? 1f : 0f);
            if (!an) SilhouettenLeeren();
        }
        if (!an) return;
        _silhouettenUhr += delta;
        if (_silhouettenUhr < SilhouettenTakt) return;
        _silhouettenUhr = 0;

        var ew = _simulation?.Wirtschaft?.Einwohnerschaft;
        if (ew is not { IstAktiv: true }) return;
        // Bildmitte auf dem Boden: der Blickstrahl der Kamera trifft die Ebene y = 0
        var vorn = -_kamera.GlobalBasis.Z;
        if (Mathf.Abs(vorn.Y) < 1e-3f) return;
        var mitte = _kamera.GlobalPosition + vorn * (-_kamera.GlobalPosition.Y / vorn.Y);
        var reichweite = _kamera.Size * 1.25f;
        foreach (var (nummer, eintrag) in _silhouettenHaeuser.ToArray())
        {
            var haus = eintrag.Modell.Wurzel.GlobalPosition;
            var nah = new Vector2(haus.X - mitte.X, haus.Z - mitte.Z).LengthSquared() <= reichweite * reichweite;
            Vector4 a = Vector4.Zero, b = Vector4.Zero;
            if (nah)
            {
                var da = ew.ImHaus(nummer).Take(2).ToArray();
                if (da.Length > 0) a = SilhouettenMass(da[0].Volk, da[0].Geschlecht);
                if (da.Length > 1) b = SilhouettenMass(da[1].Volk, da[1].Geschlecht);
            }
            if (a == eintrag.A && b == eintrag.B) continue;
            SilhouettenSetzen(eintrag.Modell, a, b);
            _silhouettenHaeuser[nummer] = (eintrag.Modell, a, b);
        }
    }

    private static void SilhouettenSetzen(ModellInstanz modell, Vector4 a, Vector4 b)
    {
        foreach (var teil in modell.Fenster.Select(f => f.Teil).Distinct())
        {
            teil.SetInstanceShaderParameter("person_a", a);
            teil.SetInstanceShaderParameter("person_b", b);
        }
    }

    /// <summary>Nimmt allen Häusern die Personen (Zoom zu weit, Tag, Innenansicht).</summary>
    private void SilhouettenLeeren()
    {
        foreach (var (nummer, eintrag) in _silhouettenHaeuser.ToArray())
        {
            if (eintrag.A == Vector4.Zero && eintrag.B == Vector4.Zero) continue;
            SilhouettenSetzen(eintrag.Modell, Vector4.Zero, Vector4.Zero);
            _silhouettenHaeuser[nummer] = (eintrag.Modell, Vector4.Zero, Vector4.Zero);
        }
    }
}
