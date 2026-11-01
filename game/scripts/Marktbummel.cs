namespace Reichsgilde;

/// <summary>
/// Wie die Leute ihre Zeit auf dem Markt verbringen (Nutzer, 1.11.2026: Markt v3) – nur Rechnung ohne Godot, damit ein Test sie mitübersetzen kann
/// (<c>MarktbummelTests</c>); die Darstellung (Stadtansicht.Marktbummel.cs) führt sie aus.
///
/// Wer auf dem Markt ist, bleibt nicht an einer Stelle stehen, bis der Kern ihn weiterschickt: Er geht von Ziel zu Ziel – an einen Stand, zu einer
/// Gesprächsgruppe, an den Brunnen (Wasser holen), an den Brunnenrand (anlehnen) oder auf eine Bank, oder er bleibt irgendwo stehen und schaut sich um. Jede Pause ist
/// anders lang, keiner geht im Gleichtakt los. Nur wenige gehen ein Stück um den Brunnen herum, und nicht alle in dieselbe Richtung.
/// Alles fest aus der Nummer der Person und dem wievielten Ziel (kein Würfel, jeder Lauf gleich).
/// </summary>
public static class Marktbummel
{
    /// <summary>Was man an einer Stelle des Marktes tut.</summary>
    public enum Art { Stand, Gruppe, Brunnen, Rand, Bank, Schauen }

    /// <summary>Anteile (zusammen 1) der Ziele in der Reihenfolge von <see cref="Art"/>: meist ein Stand oder ein Gespräch.</summary>
    public static readonly float[] Anteile = { 0.34f, 0.26f, 0.12f, 0.08f, 0.08f, 0.12f };

    /// <summary>Wie lange (Spielminuten) man an einer Stelle bleibt: von … bis (die meisten Pausen eher kurz, einige lang).</summary>
    public static (double Von, double Bis) Pause(Art art) => art switch
    {
        Art.Stand => (10, 45),
        Art.Gruppe => (20, 80),
        Art.Brunnen => (6, 16),
        Art.Rand => (15, 50),
        Art.Bank => (20, 70),
        _ => (4, 14),
    };

    /// <summary>Allein an einer Gesprächsstelle wartet man so viele Spielminuten (von … bis) auf jemanden, dann geht man weiter.</summary>
    public const double AlleinVon = 8, AlleinBis = 22;

    /// <summary>So viele Spielminuten nach der Ankunft am Brunnen schöpft man Wasser (bückt sich über den Rand), danach steht man da.</summary>
    public const double Schoepfen = 5;

    /// <summary>Höchstens so viele Leute gehen auf einem Platz zugleich um den Brunnen herum.</summary>
    public const int KreisenHoechstens = 2;

    /// <summary>So oft (Anteil der Wege zum Brunnen, an den Rand oder zum Schauen) geht man vorher ein Stück um den Brunnen.</summary>
    public const float KreisenAnteil = 0.3f;

    /// <summary>So viele Personen hat eine Gesprächsgruppe höchstens; geredet wird erst ab zweien.</summary>
    public const int GruppeHoechstens = 4;

    /// <summary>Die Plätze einer Gruppe in der Reihenfolge, in der sie sich füllen (Winkel in Vierteln): zwei stehen sich gegenüber, die nächsten dazwischen.</summary>
    public static readonly int[] Gruppenfolge = { 0, 2, 1, 3 };

    /// <summary>Abstand (Meter) jeder Person von der Mitte der Gruppe: zwei stehen sich in 1,2 m gegenüber.</summary>
    public const float GruppeRadius = 0.6f;

    /// <summary>Redet sie? Nur, wenn außer ihr noch jemand in der Gruppe steht.</summary>
    public static bool Redet(int anwesendeAusserIhr) => anwesendeAusserIhr >= 1;

    /// <summary>Welches Ziel sie als <paramref name="zaehler"/>-tes nach der Ankunft am Markt wählt (Ankunftszeit macht jeden Besuch anders).</summary>
    public static Art Waehlen(int nummer, int zaehler, long ankunft)
    {
        var x = Passantenspur.Bruchteil(nummer, Schluessel(zaehler, ankunft, 11));
        for (var i = 0; i < Anteile.Length; i++)
        {
            if (x < Anteile[i]) return (Art)i;
            x -= Anteile[i];
        }
        return Art.Schauen;
    }

    /// <summary>Die Arten, die sie nimmt, wenn die gewählte keinen freien Platz hat: zuerst die gewählte, dann die übrigen in fester Folge.</summary>
    public static IEnumerable<Art> Reihenfolge(Art erste)
    {
        yield return erste;
        foreach (var a in new[] { Art.Schauen, Art.Stand, Art.Gruppe, Art.Brunnen, Art.Bank, Art.Rand })
            if (a != erste) yield return a;
    }

    /// <summary>Wie lange (Spielminuten) die Pause an dieser Stelle dauert: zwischen den Grenzen von <see cref="Pause"/>, kurze häufiger.</summary>
    public static double Verweilen(Art art, int nummer, int zaehler, long ankunft)
    {
        var (von, bis) = Pause(art);
        var x = Passantenspur.Bruchteil(nummer, Schluessel(zaehler, ankunft, 23));
        return von + (bis - von) * x * x;
    }

    /// <summary>Wie lange (Spielminuten) sie allein an einer Gesprächsstelle wartet.</summary>
    public static double Alleinwarten(int nummer, int zaehler) => AlleinVon + (AlleinBis - AlleinVon) * Passantenspur.Bruchteil(nummer, 31 + 7 * zaehler);

    /// <summary>Geht sie auf diesem Weg zuerst ein Stück um den Brunnen (nur zum Brunnen, an den Rand oder zum Schauen; die Obergrenze prüft die Darstellung)?</summary>
    public static bool Kreist(Art ziel, int nummer, int zaehler, long ankunft) =>
        ziel is Art.Brunnen or Art.Rand or Art.Schauen && Passantenspur.Bruchteil(nummer, Schluessel(zaehler, ankunft, 37)) < KreisenAnteil;

    /// <summary>In welche Richtung sie um den Brunnen geht (+1 gegen den Uhrzeigersinn in x-z, −1 mit ihm), wie weit (Bogenmaß, 100° bis 250°) und in welchem Abstand (Meter über den Halbmesser des Brunnens, 1,8 bis 3 m: außen an denen vorbei, die Wasser holen oder am Rand lehnen). Die Darstellung kürzt den Bogen, damit er vor dem Ziel endet.</summary>
    public static (int Richtung, float Spanne, float Abstand) Kreisbogen(int nummer, int zaehler, long ankunft)
    {
        var richtung = Passantenspur.Bruchteil(nummer, Schluessel(zaehler, ankunft, 41)) < 0.5f ? 1 : -1;
        var spanne = (100 + 150 * Passantenspur.Bruchteil(nummer, Schluessel(zaehler, ankunft, 43))) * MathF.PI / 180;
        var abstand = 1.8f + 1.2f * Passantenspur.Bruchteil(nummer, Schluessel(zaehler, ankunft, 47));
        return (richtung, spanne, abstand);
    }

    /// <summary>
    /// Punkte auf einem Bogen um (<paramref name="mx"/>, <paramref name="mz"/>) mit <paramref name="radius"/>, ab dem Winkel <paramref name="anfang"/>
    /// (Bogenmaß, gemessen wie atan2(z, x)) über <paramref name="spanne"/> in <paramref name="richtung"/>; etwa alle <paramref name="schritt"/> Meter ein Punkt,
    /// so weit außen, dass die Sehnen den Kreis nicht schneiden.
    /// </summary>
    public static List<(float X, float Z)> Bogen(float mx, float mz, float radius, float anfang, float spanne, int richtung, float schritt = 0.8f)
    {
        var teile = Math.Max(2, (int)MathF.Ceiling(radius * spanne / schritt));
        var aussen = radius / MathF.Cos(spanne / teile / 2);
        var punkte = new List<(float X, float Z)>();
        for (var k = 0; k <= teile; k++)
        {
            var w = anfang + richtung * spanne * k / teile;
            punkte.Add((mx + MathF.Cos(w) * aussen, mz + MathF.Sin(w) * aussen));
        }
        return punkte;
    }

    /// <summary>
    /// Wie weit (Bogenmaß) sie den Kopf samt Körper gerade von ihrer Blickrichtung wegdreht, wenn sie allein steht und sich umschaut: zwei langsame
    /// Wellen (Perioden 9–13 s und 5–7 s, Phasen aus der Nummer), zusammen höchstens <paramref name="weite"/>.
    /// </summary>
    public static float Umschauen(int nummer, double sekunden, float weite = 0.9f)
    {
        var (p1, p2) = (9 + 4 * Passantenspur.Bruchteil(nummer, 51), 5 + 2 * Passantenspur.Bruchteil(nummer, 52));
        var (f1, f2) = (Passantenspur.Bruchteil(nummer, 53) * MathF.Tau, Passantenspur.Bruchteil(nummer, 54) * MathF.Tau);
        return weite * (0.65f * MathF.Sin((float)(sekunden * MathF.Tau / p1) + f1) + 0.35f * MathF.Sin((float)(sekunden * MathF.Tau / p2) + f2));
    }

    /// <summary>Auf welcher Seite (−1 bis 1) sie auf dem Weg zu diesem Ziel von der Geraden abweicht (Auffächern auf dem Platz).</summary>
    public static float Seite(int nummer, int zaehler, long ankunft) => 2 * Passantenspur.Bruchteil(nummer, Schluessel(zaehler, ankunft, 61)) - 1;

    /// <summary>Gehtempo auf dem Markt als Anteil des eigenen: meist schlendernd (0,65–0,8), manchmal zielstrebig (0,9–1).</summary>
    public static float Tempoanteil(int nummer, int zaehler, long ankunft)
    {
        var x = Passantenspur.Bruchteil(nummer, Schluessel(zaehler, ankunft, 67));
        return x < 0.25f ? 0.9f + 0.4f * x : 0.65f + 0.2f * (x - 0.25f);
    }

    /// <summary>Ein Schlüssel für <see cref="Passantenspur.Bruchteil"/> aus Ziel, Ankunft und Zweck (jedes Ziel, jeder Besuch, jede Frage anders).</summary>
    private static int Schluessel(int zaehler, long ankunft, int zweck) => unchecked(zweck * 1000003 + zaehler * 7919 + (int)(ankunft % 100000) * 31);
}
