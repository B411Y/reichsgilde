namespace Reichsgilde;

/// <summary>
/// Die Marktrunde der Streife (reine Rechnung ohne Godot, vom Test mitübersetzt: <c>StreifeMarktrundeTests</c>; benutzt von Stadtansicht.Streife.cs).
/// Früher ging die Runde von der Tür nur bis zum ersten Knoten des Gassennetzes am Platz und auf demselben Weg zurück. Lag das Wachhaus
/// nahe am Markt (Köln, Wachhaus am Heumarkt: der Knoten vor der Tür lag schon im Umkreis des Platzes), war die Runde nur 5 m lang und der
/// Wachmann drehte sich auf der Stelle (Befund 08.11.2026).
/// <para><b>Regel</b>: Die Runde umläuft den Markt. Um den Platz herum liegen vier Seiten (Norden, Osten, Süden, Westen, je nachdem, ob ein
/// Punkt mehr quer oder mehr längs vom Platz liegt); auf jeder Seite wird der äußerste Wegpunkt innerhalb eines Ringes gewählt. Die Streife geht
/// von der Tür zur nächsten Seite, reihum über die übrigen und zurück zur Tür, jedes Stück auf dem kürzesten Weg des Netzes. Ist die Runde
/// kürzer als <see cref="MindestLaenge"/> oder berührt sie weniger als <see cref="MindestSeiten"/> Seiten, wird der Ring weiter (<see cref="Ringe"/>);
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
        var versuche = new List<(List<(float X, float Z)> Runde, float Laenge, int Seiten)>();
        List<(float X, float Z)>? vorher = null;
        foreach (var ring in Ringe)
        {
            var halte = Haltepunkte(tuer, mitte, halb, halb + ring, wegpunkte);
            if (halte.Count == 0 || (vorher is not null && halte.SequenceEqual(vorher))) continue; // dieselben Punkte wie im engeren Ring
            vorher = halte;
            // reihum in beiden Richtungen (beginnend an der Seite nahe der Tür); die kürzere, die noch lang genug ist – so läuft die Streife einen Ring statt
            // eine Gasse hin und zurück
            var andersherum = halte.Take(1).Concat(Enumerable.Reverse(halte).SkipLast(1)).ToList();
            var wahlen = new List<((List<(float X, float Z)> Runde, float Laenge) Runde, int Seiten)>();
            if (Zusammensetzen(tuer, halte, weg, out var seitenA) is { } a) wahlen.Add((a, seitenA));
            if (halte.Count > 2 && Zusammensetzen(tuer, andersherum, weg, out var seitenB) is { } b) wahlen.Add((b, seitenB));
            if (wahlen.Count == 0) continue;
            var (runde, seiten) = wahlen.OrderByDescending(w => w.Seiten).ThenByDescending(w => w.Runde.Laenge >= MindestLaenge).ThenBy(w => w.Runde.Laenge).First();
            if (runde.Laenge >= MindestLaenge && runde.Laenge <= HoechstLaenge && seiten >= MindestSeiten) return runde;
            versuche.Add((runde.Runde, runde.Laenge, seiten));
        }
        // kein Ring mit allem: lieber die richtige Länge mit weniger Seiten (die meisten, der engste Ring), sonst die kürzeste über der Mindestlänge,
        // sonst die längste darunter
        var wahl = versuche.Where(v => v.Laenge >= MindestLaenge && v.Laenge <= HoechstLaenge).OrderByDescending(v => v.Seiten).Take(1)
            .Concat(versuche.Where(v => v.Laenge > HoechstLaenge).OrderBy(v => v.Laenge).Take(1))
            .Concat(versuche.OrderByDescending(v => v.Laenge).Take(1)).ToList();
        return wahl.Count == 0 ? null : (wahl[0].Runde, wahl[0].Laenge);
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
