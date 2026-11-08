namespace Reichsgilde;

/// <summary>
/// Die Marktrunde der Streife (reine Rechnung ohne Godot, vom Test mitübersetzt: <c>StreifeMarktrundeTests</c>; benutzt von Stadtansicht.Streife.cs).
/// Früher ging die Runde von der Tür nur bis zum ersten Knoten des Gassennetzes am Platz und auf demselben Weg zurück. Lag das Wachhaus
/// nahe am Markt (Köln, Wachhaus am Heumarkt: der Knoten vor der Tür lag schon im Umkreis des Platzes), war die Runde nur 5 m lang und der
/// Wachmann drehte sich auf der Stelle (Befund 08.11.2026).
/// <para><b>Regel</b>: Die Runde umläuft den Markt. Um den Platz herum liegen vier Seiten (Norden, Osten, Süden, Westen, je nachdem, ob ein
/// Punkt mehr quer oder mehr längs vom Platz liegt); auf jeder Seite wird der äußerste Wegpunkt innerhalb eines Ringes gewählt. Die Streife geht
/// von der Tür zur nächsten Seite, reihum über die übrigen und zurück zur Tür, jedes Stück auf dem kürzesten Weg des Netzes. Ist die Runde
/// kürzer als <see cref="MindestLaenge"/>, berührt sie weniger als <see cref="MindestSeiten"/> Seiten oder geht sie mehr als <see cref="HoechstDoppelt"/>
/// ihrer Länge zweimal (dieselbe Gasse hin und zurück), wird der Ring weiter (<see cref="Ringe"/>);
/// länger als <see cref="HoechstLaenge"/> wird sie nur, wenn es nicht anders geht (Wachhaus weit vom Markt). Wegpunkte außerhalb der Mauer gibt der
/// Aufrufer gar nicht erst mit (die Streife bleibt auf der Seite der Mauer, auf der der Markt liegt).</para>
/// <para>Keine Regel des Kerns, kein Zufall: dieselbe Karte ergibt dieselbe Runde.</para>
/// </summary>
public static class Marktrunde
{
    /// <summary>So lang (Meter, hin und zurück) soll eine Marktrunde mindestens sein.</summary>
    public const float MindestLaenge = 150f;

    /// <summary>Länger (Meter) wird die Runde nur, wenn schon der engste Ring darüber liegt.</summary>
    public const float HoechstLaenge = 400f;

    /// <summary>Die Ringe um den Platz (Meter über den Rand des Pflasters hinaus), von innen nach außen probiert.</summary>
    public static readonly float[] Ringe = { 15f, 35f, 60f, 90f, 130f };

    /// <summary>
    /// Plant die Runde: <paramref name="tuer"/> (Tür des Wachhauses), der Platz (<paramref name="mitte"/>, halbe Kantenlänge <paramref name="halb"/>),
    /// <paramref name="wegpunkte"/> (Punkte auf den Wegen der Karte, aus denen die Haltepunkte gewählt werden) und <paramref name="weg"/> (kürzester Weg
    /// des Netzes zwischen zwei Punkten, mit beiden Enden; leer ohne Weg). Ergebnis: die Punkte der Runde (beginnt und endet an der Tür) und ihre
    /// Länge; null, wenn kein Haltepunkt zu erreichen ist.
    /// </summary>
    public static (List<(float X, float Z)> Runde, float Laenge)? Planen((float X, float Z) tuer, (float X, float Z) mitte, float halb,
        IReadOnlyList<(float X, float Z)> wegpunkte, Func<(float X, float Z), (float X, float Z), IReadOnlyList<(float X, float Z)>> weg)
    {
        var versuche = new List<(List<(float X, float Z)> Runde, float Laenge, int Seiten, float Doppelt)>();
        List<(float X, float Z)>? vorher = null;
        foreach (var ring in Ringe)
        {
            var halte = Haltepunkte(tuer, mitte, halb, halb + ring, wegpunkte);
            if (halte.Count == 0 || (vorher is not null && halte.SequenceEqual(vorher))) continue; // dieselben Punkte wie im engeren Ring
            vorher = halte;
            // reihum in beiden Richtungen (beginnend an der Seite nahe der Tür)
            var richtungen = new List<List<(float X, float Z)>> { halte };
            if (halte.Count > 2) richtungen.Add(halte.Take(1).Concat(Enumerable.Reverse(halte).SkipLast(1)).ToList());
            foreach (var folge in richtungen)
            {
                if (Zusammensetzen(tuer, folge, weg, out var seiten) is not { } runde) continue;
                var doppelt = DoppeltAnteil(runde.Runde);
                if (runde.Laenge >= MindestLaenge && runde.Laenge <= HoechstLaenge && seiten >= MindestSeiten && doppelt <= HoechstDoppelt) return runde;
                versuche.Add((runde.Runde, runde.Laenge, seiten, doppelt));
            }
        }
        // kein Ring mit allem: in der richtigen Länge die mit drei Seiten, dann die mit dem wenigsten doppelten Weg (bei Gleichstand die kürzere), sonst die
        // kürzeste über der Höchstlänge, sonst die längste unter der Mindestlänge
        var wahl = versuche.Where(v => v.Laenge >= MindestLaenge && v.Laenge <= HoechstLaenge)
                .OrderByDescending(v => v.Seiten >= MindestSeiten).ThenBy(v => v.Doppelt).ThenBy(v => v.Laenge).Take(1)
            .Concat(versuche.Where(v => v.Laenge > HoechstLaenge).OrderBy(v => v.Laenge).Take(1))
            .Concat(versuche.OrderByDescending(v => v.Laenge).Take(1)).ToList();
        return wahl.Count == 0 ? null : (wahl[0].Runde, wahl[0].Laenge);
    }

    /// <summary>
    /// Höchstens so viel der Runde (Anteil der Länge) darf die Streife zweimal gehen (hin und zurück durch dieselbe Gasse), sonst wird der nächste Ring probiert
    /// – ohne diese Grenze ging die Runde in Köln 15 m die Rheingasse hinauf und wieder zurück, statt um den Block zu laufen.
    /// </summary>
    public const float HoechstDoppelt = 0.25f;

    /// <summary>
    /// Welcher Anteil der Länge einer Runde zweimal gegangen wird: Stücke (auf 0,5 m gerundet, gleich in welcher Richtung), die mehr als einmal vorkommen; ohne
    /// das erste und letzte Stück (Tür zum Weg, das ist immer hin und zurück).
    /// </summary>
    public static float DoppeltAnteil(IReadOnlyList<(float X, float Z)> runde)
    {
        if (runde.Count < 4) return 0;
        static (int, int) Raster((float X, float Z) p) => ((int)MathF.Round(p.X * 2), (int)MathF.Round(p.Z * 2));
        var zahl = new Dictionary<((int, int), (int, int)), int>();
        var stuecke = new List<(((int, int), (int, int)) Schluessel, float Laenge)>();
        var gesamt = 0f;
        for (var i = 2; i < runde.Count - 1; i++)
        {
            var (a, b) = (Raster(runde[i - 1]), Raster(runde[i]));
            var schluessel = a.CompareTo(b) <= 0 ? (a, b) : (b, a);
            var laenge = Abstand(runde[i - 1], runde[i]);
            zahl[schluessel] = zahl.GetValueOrDefault(schluessel) + 1;
            stuecke.Add((schluessel, laenge));
            gesamt += laenge;
        }
        var doppelt = stuecke.Where(s => zahl[s.Schluessel] > 1).Sum(s => s.Laenge);
        return gesamt <= 0 ? 0 : doppelt / gesamt;
    }

    /// <summary>An so vielen Seiten des Platzes soll die Runde vorbeikommen (wo die Karte so viele hat).</summary>
    public const int MindestSeiten = 3;

    /// <summary>
    /// Je Seite des Platzes der äußerste Wegpunkt höchstens <paramref name="ring"/> m von der Mitte (nicht auf dem Pflaster selbst, also mindestens
    /// <paramref name="halb"/> von der Mitte), reihum Norden, Osten, Süden, Westen; begonnen wird an der Seite, deren Punkt der Tür am nächsten liegt.
    /// </summary>
    public static List<(float X, float Z)> Haltepunkte((float X, float Z) tuer, (float X, float Z) mitte, float halb, float ring, IReadOnlyList<(float X, float Z)> wegpunkte)
    {
        var seiten = new (float X, float Z)?[4];
        var weite = new float[4];
        foreach (var p in wegpunkte)
        {
            var d = Abstand(p, mitte);
            if (d > ring || d < halb) continue;
            var (dx, dz) = (p.X - mitte.X, p.Z - mitte.Z);
            var seite = Math.Abs(dz) >= Math.Abs(dx) ? (dz < 0 ? 0 : 2) : (dx > 0 ? 1 : 3);
            if (seiten[seite] is { } alt && (d < weite[seite] || (d == weite[seite] && (p.X > alt.X || (p.X == alt.X && p.Z >= alt.Z))))) continue;
            (seiten[seite], weite[seite]) = (p, d);
        }
        var liste = seiten.Where(s => s is not null).Select(s => s!.Value).ToList();
        if (liste.Count == 0) return liste;
        var erster = 0;
        for (var i = 1; i < liste.Count; i++)
            if (Abstand(liste[i], tuer) < Abstand(liste[erster], tuer)) erster = i;
        return liste.Skip(erster).Concat(liste.Take(erster)).ToList();
    }

    /// <summary>
    /// Tür, die Haltepunkte der Reihe nach (unerreichbare fallen weg) und zurück zur Tür; null, wenn kein Haltepunkt zu erreichen ist.
    /// <paramref name="erreicht"/>: wie viele Haltepunkte (Seiten des Platzes) die Runde berührt.
    /// </summary>
    public static (List<(float X, float Z)> Runde, float Laenge)? Zusammensetzen((float X, float Z) tuer, IReadOnlyList<(float X, float Z)> halte,
        Func<(float X, float Z), (float X, float Z), IReadOnlyList<(float X, float Z)>> weg, out int erreicht)
    {
        var runde = new List<(float X, float Z)> { tuer };
        var hier = tuer;
        erreicht = 0;
        foreach (var ziel in halte)
        {
            var stueck = weg(hier, ziel);
            if (stueck.Count == 0) continue;
            Anhaengen(runde, stueck);
            hier = ziel;
            erreicht++;
        }
        if (erreicht == 0) return null;
        var heim = weg(hier, tuer);
        if (heim.Count == 0) return null; // ohne Rückweg keine Runde
        Anhaengen(runde, heim);
        Anhaengen(runde, new[] { tuer });
        var laenge = 0f;
        for (var i = 1; i < runde.Count; i++) laenge += Abstand(runde[i - 1], runde[i]);
        return (runde, laenge);
    }

    private static void Anhaengen(List<(float X, float Z)> runde, IReadOnlyList<(float X, float Z)> stueck)
    {
        foreach (var p in stueck)
            if (Abstand(runde[^1], p) > 0.05f) runde.Add(p);
    }

    public static float Abstand((float X, float Z) a, (float X, float Z) b)
    {
        var (dx, dz) = (a.X - b.X, a.Z - b.Z);
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
