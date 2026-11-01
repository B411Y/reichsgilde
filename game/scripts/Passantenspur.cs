namespace Reichsgilde;

/// <summary>
/// Wo auf der Gasse eine Figur geht (seitlich von der Mitte des Weges), nur Rechnung ohne Godot, damit ein Test die Datei mitübersetzen kann
/// (<c>PassantenspurTests</c>). Die Darstellung stellt die Figur dann senkrecht zur Gehrichtung um dieses Maß nach rechts (Stadtansicht.Passanten.cs).
///
/// Drei Schichten (Nutzer, 1.11.2026):
///   1. <see cref="Grundspur"/>: jede Person hat ihre eigene Spur, fest aus der Personennummer (kein Zufall, jeder Lauf gleich), und zwar auf der
///      rechten Hälfte der Gasse (Rechtsverkehr).
///   2. <see cref="Schwankung"/>: um diese Spur pendelt sie sanft, ±<see cref="Schwanken"/> Meter, mit Wellenlängen von 3 bis 5 Metern längs der
///      zurückgelegten Strecke und einer Phase aus der Nummer.
///   3. <see cref="Seite"/>: am Anfang und Ende des Weges und beim Anschluss an einen laufenden Weg gleicht sich die Spur über
///      <see cref="Uebergang"/> Meter an, statt zu springen.
///
/// Das Maß der Gasse ist <see cref="Gassenbreite"/>, fest 2 m (Annahme: die schmalste Gasse der Karten, der Fußweg im Wald, ist 2 m breit, die
/// Hauptgassen 5 m; die Breite der Karte wird hier nicht gelesen, damit die Spuren überall gleich aussehen). Die Spur reicht von
/// <see cref="Spurnah"/> bis <see cref="Spurfern"/> Meter rechts der Mitte, mit dem Schwanken von 0,25 bis 0,85 m, der Körper bleibt also
/// innerhalb der rechten Hälfte (Schulterbreite etwa 0,5 m). Der Karrenausweichschritt (Schritt 5d) kommt in der Darstellung noch dazu.
/// </summary>
public static class Passantenspur
{
    /// <summary>Festes Standardmaß einer Gasse (Meter): die Spuren sind darauf abgestimmt. Die Breiten der Karte (2 bis 5 m) werden nicht gelesen.</summary>
    public const float Gassenbreite = 2.0f;

    /// <summary>Die Grundspur liegt zwischen so viel und so viel Meter rechts der Mitte (bei zwei Metern Gassenbreite die rechte Hälfte).</summary>
    public const float Spurnah = 0.40f, Spurfern = 0.70f;

    /// <summary>Wie weit (Meter) die Figur beim Gehen um ihre Spur pendelt, höchstens.</summary>
    public const float Schwanken = 0.15f;

    /// <summary>Länge (Meter) von Anfang und Ende des Weges, über die sich die Spur angleicht.</summary>
    public const float Uebergang = 1.0f;

    /// <summary>Wellenlängen (Meter) der beiden Schwingungen: die erste 3 bis 4 m, die zweite 4 bis 5 m.</summary>
    public const float WelleKurzMin = 3f, WelleKurzMax = 4f, WelleLangMin = 4f, WelleLangMax = 5f;

    /// <summary>Gleichmäßig verteilter Bruchteil (0 bis 1) aus einer Nummer und einem Schlüssel: Goldener Schnitt, dadurch auch bei aufeinanderfolgenden Nummern weit gestreut.</summary>
    public static float Bruchteil(int nummer, int schluessel)
    {
        const double phi = 0.6180339887498949;
        var x = (nummer * 2 + 1 + schluessel * 7919L) * phi;
        return (float)(x - Math.Floor(x));
    }

    /// <summary>Die eigene Spur dieser Person (Meter rechts der Mitte), fest nach der Nummer, von <see cref="Spurnah"/> bis <see cref="Spurfern"/>.</summary>
    public static float Grundspur(int nummer) => Spurnah + (Spurfern - Spurnah) * Bruchteil(nummer, 0);

    /// <summary>Die erste und zweite Wellenlänge dieser Person (Meter).</summary>
    public static (float Kurz, float Lang) Wellenlaengen(int nummer) =>
        (WelleKurzMin + (WelleKurzMax - WelleKurzMin) * Bruchteil(nummer, 1), WelleLangMin + (WelleLangMax - WelleLangMin) * Bruchteil(nummer, 2));

    /// <summary>
    /// Wie weit sie nach <paramref name="strecke"/> Metern auf dem Weg von ihrer Spur abweicht (−<see cref="Schwanken"/> bis +<see cref="Schwanken"/>):
    /// zwei Sinuswellen (Gewichte 0,6 und 0,4, zusammen höchstens 1) mit Phasen aus der Nummer.
    /// </summary>
    public static float Schwankung(int nummer, float strecke)
    {
        var (kurz, lang) = Wellenlaengen(nummer);
        var (phase1, phase2) = (Bruchteil(nummer, 3) * MathF.Tau, Bruchteil(nummer, 4) * MathF.Tau);
        return Schwanken * (0.6f * MathF.Sin(MathF.Tau * strecke / kurz + phase1) + 0.4f * MathF.Sin(MathF.Tau * strecke / lang + phase2));
    }

    /// <summary>Weiche Rampe von 0 auf 1 über <see cref="Uebergang"/> Meter (S-Kurve: am Anfang und am Ende ohne Knick).</summary>
    public static float Rampe(float meter)
    {
        var t = Math.Clamp(meter / Uebergang, 0f, 1f);
        return t * t * (3 - 2 * t);
    }

    /// <summary>
    /// Der seitliche Abstand zur Mitte des Weges (Meter, nach rechts positiv) nach <paramref name="strecke"/> Metern, mit noch <paramref name="rest"/>
    /// Metern bis zum Ende. Am Anfang kommt sie von <paramref name="startseite"/> (0, wenn die Figur an einem Ort losgeht; ihr bisheriges Maß, wenn sie
    /// von einem laufenden Weg auf den neuen übergeht) und gleicht sich über <see cref="Uebergang"/> Meter an die Spur an, am Ende geht sie ebenso zurück
    /// auf die Mitte des Weges.
    /// </summary>
    public static float Seite(int nummer, float strecke, float rest, float startseite = 0)
    {
        var ziel = Grundspur(nummer) + Schwankung(nummer, strecke);
        var an = Rampe(strecke);
        return (startseite + (ziel - startseite) * an) * Rampe(rest);
    }

    /// <summary>
    /// Die Richtung, senkrecht zu der die Figur versetzt wird, aus den Punkten ein Stück vor und hinter ihr (also über Ecken geglättet) – ohne Godot als
    /// ebene Richtung (x, z). Die Rechtsrichtung ist (−z, x). Ist die Strecke zu kurz oder kehrt sie um (Summe null), gilt <paramref name="rueckfall"/>.
    /// </summary>
    public static (float X, float Z) Rechts((float X, float Z) zurueck, (float X, float Z) vor, (float X, float Z) rueckfall)
    {
        var (dx, dz) = (vor.X - zurueck.X, vor.Z - zurueck.Z);
        var laenge = MathF.Sqrt(dx * dx + dz * dz);
        if (laenge < 0.05f) (dx, dz, laenge) = (rueckfall.X, rueckfall.Z, MathF.Sqrt(rueckfall.X * rueckfall.X + rueckfall.Z * rueckfall.Z));
        if (laenge < 1e-6f) return (0, 0);
        return (-dz / laenge, dx / laenge);
    }
}
