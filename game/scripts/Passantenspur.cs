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
/// <see cref="Spurnah"/> bis <see cref="Spurfern"/> Meter rechts der Mitte, mit dem Schwanken von 0,20 bis 0,75 m: Die Mitte der Figur bleibt auf der
/// rechten Hälfte, der Körper (Schulterbreite etwa 0,5 m) reicht höchstens bis zum rechten Rand der 2-m-Gasse. Der Karrenausweichschritt
/// (Schritt 5d) kommt in der Darstellung noch dazu und führt bis an den Rand.
///
/// Weil die Figur seitlich versetzt geht (Übergang, Schwanken), geht sie auf dem Boden etwas weiter als die Mittellinie: <see cref="Bewegung"/> gibt die
/// wirkliche Richtung und das Verhältnis der beiden Wege (für den Takt der Schritte, damit die Füße nicht rutschen) an.
/// </summary>
public static class Passantenspur
{
    /// <summary>Festes Standardmaß einer Gasse (Meter): die Spuren sind darauf abgestimmt. Die Breiten der Karte (2 bis 5 m) werden nicht gelesen.</summary>
    public const float Gassenbreite = 2.0f;

    /// <summary>Die Grundspur liegt zwischen so viel und so viel Meter rechts der Mitte (bei zwei Metern Gassenbreite die rechte Hälfte).</summary>
    public const float Spurnah = 0.35f, Spurfern = 0.60f;

    /// <summary>Wie weit (Meter) die Figur beim Gehen um ihre Spur pendelt, höchstens.</summary>
    public const float Schwanken = 0.15f;

    /// <summary>Länge (Meter) von Anfang und Ende des Weges, über die sich die Spur angleicht.</summary>
    public const float Uebergang = 1.0f;

    /// <summary>Wellenlängen (Meter) der beiden Schwingungen: die erste 3 bis 4 m, die zweite 4 bis 5 m.</summary>
    public const float WelleKurzMin = 3f, WelleKurzMax = 4f, WelleLangMin = 4f, WelleLangMax = 5f;

    /// <summary>
    /// Ein fester „Zufall“ (0 bis unter 1) aus einer Nummer und einem Schlüssel (Mischfunktion, kein Zufallsgenerator): Wellenlängen und Phasen
    /// hängen so nicht voneinander und nicht von der Grundspur ab, zwei Personen mit ähnlicher Spur schwanken also nicht im Gleichschritt.
    /// </summary>
    public static float Bruchteil(int nummer, int schluessel)
    {
        unchecked
        {
            var h = (uint)nummer * 0x9E3779B1u ^ (uint)(schluessel + 1) * 0x85EBCA6Bu;
            h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
            return (h >> 8) / 16777216f;
        }
    }

    /// <summary>
    /// Die eigene Spur dieser Person (Meter rechts der Mitte), fest nach der Nummer, von <see cref="Spurnah"/> bis <see cref="Spurfern"/>. Der Goldene Schnitt
    /// verteilt aufeinanderfolgende Nummern gleichmäßig über die Breite (Nachbarn im Haus liegen nicht auf derselben Spur).
    /// </summary>
    public static float Grundspur(int nummer)
    {
        var x = (nummer * 2L + 1) * 0.6180339887498949;
        return Spurnah + (Spurfern - Spurnah) * (float)(x - Math.Floor(x));
    }

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
    public static float Rampe(float meter, float laenge = Uebergang)
    {
        var t = Math.Clamp(meter / laenge, 0f, 1f);
        return t * t * (3 - 2 * t);
    }

    /// <summary>
    /// Der seitliche Abstand zur Mitte des Weges (Meter, nach rechts positiv) nach <paramref name="strecke"/> Metern, mit noch <paramref name="rest"/>
    /// Metern bis zum Ende. Am Anfang kommt sie von <paramref name="startseite"/> (0, wenn die Figur an einem Ort losgeht; ihr bisheriges Maß, wenn sie
    /// von einem laufenden Weg auf den neuen übergeht) und gleicht sich über <see cref="Uebergang"/> Meter an die Spur an, am Ende geht sie ebenso zurück
    /// auf die Mitte des Weges.
    /// </summary>
    public static float Seite(int nummer, float strecke, float rest, float startseite = 0) =>
        Versatz(nummer, strecke, rest, (startseite, 0), (1, 0)).X;

    /// <summary>
    /// Der seitliche Versatz als Vektor (x, z in Metern) nach <paramref name="strecke"/> Metern, mit noch <paramref name="rest"/> Metern bis zum Ende:
    /// <paramref name="rechts"/> ist die Rechtsrichtung der Figur (Länge 1), <paramref name="start"/> der Versatz, mit dem sie auf den Weg kommt (null, wenn sie
    /// an einem Ort losgeht; sonst ihr bisheriger Versatz als Vektor, also auch seine Richtung – so springt sie beim Anschluss nicht). Der Versatz gleitet
    /// über <see cref="Uebergang"/> Meter von <paramref name="start"/> auf die Spur (<see cref="Grundspur"/> plus <see cref="Schwankung"/> nach rechts)
    /// und am Ende zurück auf null.
    /// </summary>
    public static (float X, float Z) Versatz(int nummer, float strecke, float rest, (float X, float Z) start, (float X, float Z) rechts)
    {
        var ziel = Grundspur(nummer) + Schwankung(nummer, strecke);
        // Je weiter der alte Versatz vom Ziel weg ist (anderer Weg, andere Richtung), desto länger dauert der Übergang: so bleibt er überall gleich flach
        var (dx, dz) = (rechts.X * ziel - start.X, rechts.Z * ziel - start.Z);
        var laenge = Uebergang * MathF.Max(1, MathF.Sqrt(dx * dx + dz * dz) / (Spurfern + Schwanken));
        var an = Rampe(strecke, laenge);
        var aus = 1 - (1 - Rampe(rest)) * an; // am Anfang bleibt der alte Versatz auch auf einem ganz kurzen Weg, wie er ist; das Ausblenden greift erst, wenn die Spur erreicht ist
        return ((start.X * (1 - an) + rechts.X * ziel * an) * aus, (start.Z * (1 - an) + rechts.Z * ziel * an) * aus);
    }

    /// <summary>Die Mittellinie eines Weges: der Punkt (x, z) nach so vielen Metern Weg (die Darstellung liefert ihn aus dem Gassennetz, ein Test aus einer Punktfolge).</summary>
    public interface IMittellinie
    {
        (float X, float Z) Bei(float strecke);
    }

    /// <summary>Wo die Figur ist und wohin sie geht: <see cref="Ort"/> (Mittellinie plus Versatz), der <see cref="Versatz"/> und die <see cref="Rechts"/>richtung dazu, und die <see cref="Bewegung"/> (Weg auf dem Boden je Meter Mittellinie als Vektor; null, wenn nicht verlangt).</summary>
    public readonly record struct Gang((float X, float Z) Ort, (float X, float Z) Versatz, (float X, float Z) Rechts, (float X, float Z) Bewegung);

    /// <summary>Wie weit (Meter) vor und zurück der Weg auf dem Boden betrachtet wird, um die Bewegung zu messen.</summary>
    public const float Schrittweite = 0.1f;

    /// <summary>Wie weit (Meter) vor und zurück die Mittellinie betrachtet wird, um die Rechtsrichtung zu finden (Ecken werden über etwa einen Meter gerundet).</summary>
    public const float Fenster = 0.5f;

    /// <summary>
    /// Die Figur nach <paramref name="strecke"/> Metern auf einem Weg der Länge <paramref name="laenge"/>. Mit <paramref name="mitBewegung"/> gehört
    /// dazu, wohin sie auf dem Boden wirklich geht (aus ihrer Lage kurz davor und kurz danach (je 0,1 m), also auch an Ecken und im Übergang): Die Darstellung dreht die
    /// Figur in diese Richtung (kein seitliches Rutschen) und lässt die Schritte um die Länge dieser Bewegung schneller laufen. Auf gerader Strecke im Gehen
    /// ist sie höchstens 1,04, in den Übergängen bis etwa 1,5; beim Anschluss mit seitlichem Versatz oder an einer Ecke kann sie auch weit darüber oder
    /// darunter liegen (die Darstellung begrenzt sie).
    /// </summary>
    public static Gang Gehen(IMittellinie mitte, int nummer, float laenge, float strecke, (float X, float Z) start, bool mitBewegung)
    {
        var jetzt = Punkt(mitte, nummer, laenge, strecke, start);
        if (!mitBewegung) return jetzt;
        var (s0, s1) = (MathF.Max(0, strecke - Schrittweite), MathF.Min(laenge, strecke + Schrittweite));
        if (s1 - s0 < 1e-3f) return jetzt;
        var (davor, danach) = (Punkt(mitte, nummer, laenge, s0, start).Ort, Punkt(mitte, nummer, laenge, s1, start).Ort);
        return jetzt with { Bewegung = ((danach.X - davor.X) / (s1 - s0), (danach.Z - davor.Z) / (s1 - s0)) };
    }

    private static Gang Punkt(IMittellinie mitte, int nummer, float laenge, float strecke, (float X, float Z) start)
    {
        var hier = mitte.Bei(strecke);
        var zurueck = mitte.Bei(MathF.Max(0, strecke - Fenster));
        var vor = mitte.Bei(MathF.Min(laenge, strecke + Fenster));
        var rechts = Rechts(zurueck, vor, (vor.X - hier.X, vor.Z - hier.Z));
        var versatz = Versatz(nummer, strecke, laenge - strecke, start, rechts);
        return new Gang((hier.X + versatz.X, hier.Z + versatz.Z), versatz, rechts, (0, 0));
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
