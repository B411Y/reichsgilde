using System.Runtime.CompilerServices;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Partie;

// ═════════════════════════════════════════════════════════════════════════════
//  Startortlos (Entscheidung des Nutzers vom 15.11.2026: „nur noch Bauzonen“)
//
//  Es gibt keine Hofstellen und Startplätze mehr (die Kartenfelder dazu sind seit dem 16.11.2026 entfernt). Jedes Haus beginnt mit einem Wohnhaus und der Werkstatt seines Berufs auf zwei
//  Grundstücken in den Bauzonen – sofort fertig, kostenlos:
//
//    • KI-Häuser landen ganz zufällig im Stadtinneren (Bauzonen der Lage Kern und Mauerring, also innerhalb der Mauern) – ohne Bedingung
//      zum Markt oder zum Abstand voneinander. Wohnhaus und Werkstatt eines Hauses werden unabhängig voneinander gelost.
//    • Der Spieler ohne Startwahl (Messungen, Tests) wird genauso gelost; mit Startwahl (NeuePartie.Startwahl) setzt er beides selbst
//      (Bauwesen.Start.cs). Gelost wird für ihn trotzdem – so stehen die KI-Häuser mit und ohne Wahl gleich, und für ihn bleibt Platz.
//    • Wie viele Häuser beginnen können, ergibt sich daraus, wie viele Paare (Wohnhaus + Werkstatt) in die Zonen im Stadtinneren passen
//      (Haeuser), höchstens Partiebesetzung.HoechstensHoefe (8 Häuser zusammen: der Spieler und 7 KI-Häuser).
//      Passen weniger, spielen weniger mit – in die Vorstadt weicht kein Haus aus.
//
//  Das Los hat eine eigene Zufallsquelle aus dem Startwert der Partie (Zugwuerfel.Mischen mit LosSchluessel): Die übrigen Würfe der Partie
//  bleiben, wie sie ohne das Los wären, und jeder Rechner einer Mehrspielerpartie lost dasselbe.
//
//  Die Stellen sind die des Bauwesens (Bauwesen.FreieOrte: Raster, Zone, Lage, Straße vor der Tür, kein fester Bau) für ein Wohnhaus der
//  Stufe 0 und eine Werkstatt. Sie hängen nur an der Karte und den Bau-Daten und werden je Karte einmal gerechnet und gemerkt.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Wo ein Haus beginnt: die Fläche seines Wohnhauses und die seiner Werkstatt (Grundrisse in den Bauzonen).</summary>
public sealed record Startort(Grundriss Wohnhaus, Grundriss Werkstatt);

/// <summary>Lost die Startorte der Häuser in den Bauzonen im Stadtinneren (siehe Kopf).</summary>
public static class Startortlos
{
    /// <summary>Schlüssel der Zufallsquelle des Loses.</summary>
    public const int LosSchluessel = 0x4F7274; // „Ort“

    /// <summary>So oft wird neu gelost, wenn ein Haus keinen Platz mehr findet (die ersten haben die Zonen ungünstig zerschnitten); danach gilt die dichte Aufstellung.</summary>
    private const int Versuche = 20;

    internal static Zufallsquelle Losquelle(int zufallsstartwert, int versuch) => new(Zugwuerfel.Mischen(zufallsstartwert, LosSchluessel, versuch));

    /// <summary>Stellen für Wohnhäuser und für Werkstätten, in der festen Reihenfolge des Bauwesens.</summary>
    private sealed record Stellen(IReadOnlyList<Grundriss> Wohnhaeuser, IReadOnlyList<Grundriss> Werkstaetten);

    /// <summary>Was je Karte gemerkt wird: die Stellen im Stadtinneren und die dichte Aufstellung (eine Stelle nach der anderen, je die erste freie).</summary>
    private sealed record Vorrat(string Schluessel, Stellen Innen, IReadOnlyList<Startort> Dicht);

    private static readonly ConditionalWeakTable<Stadtkarte, Vorrat> Gemerkt = new();
    private static readonly object Sperre = new();

    /// <summary>Woran die Stellen außer der Karte hängen: Raster, Abstand zur Straße und die Maße von Wohnhaus (Stufe 0) und Werkstatt.</summary>
    private static string Schluessel(BauDaten d)
    {
        string M(Gebaeudemass m) => $"{Zahlenformat.Knapp(m.Breite)}x{Zahlenformat.Knapp(m.Tiefe)}:{string.Join(",", m.Lagen)}";
        return $"{Zahlenformat.Knapp(d.Raster)}|{Zahlenformat.Knapp(d.StrassenAbstand)}|{M(d.Wohnhaus[0])}|{M(d.Werkstatt)}";
    }

    /// <summary>Null, wenn auf dieser Karte mit diesen Daten nicht gebaut wird (kein Abschnitt „bauen“, keine Wohnhäuser).</summary>
    private static Vorrat? VorratFuer(Stadtkarte karte, Wirtschaftsdaten daten)
    {
        if (!Baut(daten)) return null;
        var schluessel = Schluessel(daten.Bauen!);
        lock (Sperre)
        {
            if (Gemerkt.TryGetValue(karte, out var bekannt) && bekannt.Schluessel == schluessel) return bekannt;
            // Eine leere Stadt auf dieser Karte, nur zum Rechnen der Stellen (kein Haus, kein Grundstück).
            var w = new Simulation(Partieeinstellungen.Standard, null, daten).Wirtschaft;
            if (w is null) return null;
            w.KarteSetzen(karte);
            if (!w.Bauen.IstAktiv) return null;
            var art = daten.Berufe[Startaufstellung.Berufe(daten)[0]].Werkstatt; // alle Werkstätten sind gleich groß (bauen.werkstatt)
            var (wohnhaus, werkstatt) = ((BauBefehl)new WohnhausBauen(0, 0, 0, 0), (BauBefehl)new WerkstattBauen(art, 0, 0, 0));
            var (haeuser, werkstaetten) = (w.Bauen.Startstellen(wohnhaus), w.Bauen.Startstellen(werkstatt));
            var innen = new Stellen(haeuser, werkstaetten);
            var vorrat = new Vorrat(schluessel, innen, Aufstellen(innen, Partiebesetzung.HoechstensHoefe, null));
            Gemerkt.AddOrUpdate(karte, vorrat);
            return vorrat;
        }
    }

    /// <summary>
    /// Stellt bis zu <paramref name="anzahl"/> Häuser auf: je Haus erst das Wohnhaus, dann die Werkstatt, jede auf einer Stelle im Stadtinneren,
    /// die noch nichts überdeckt. Mit <paramref name="los"/> jede freie Stelle gleich wahrscheinlich, ohne die erste (dichte Aufstellung). Hört
    /// auf, wenn ein Haus nicht mehr passt.
    /// </summary>
    private static List<Startort> Aufstellen(Stellen innen, int anzahl, Zufallsquelle? los)
    {
        var (frei, freiW) = (innen.Wohnhaeuser.ToList(), innen.Werkstaetten.ToList());
        Grundriss? Nehmen(List<Grundriss> topf)
        {
            if (topf.Count == 0) return null;
            var g = topf[los?.Next(topf.Count) ?? 0];
            foreach (var liste in new[] { frei, freiW }) liste.RemoveAll(x => x.Ueberlappt(g)); // vergeben: was sie überdeckt, fällt weg
            return g;
        }
        var orte = new List<Startort>(anzahl);
        while (orte.Count < anzahl)
        {
            if (Nehmen(frei) is not { } haus || Nehmen(freiW) is not { } werkstatt) break;
            orte.Add(new Startort(haus, werkstatt));
        }
        return orte;
    }

    /// <summary>Gibt es mit diesen Daten das Bauwesen (wie <see cref="Bauwesen.IstAktiv"/>: Abschnitt „bauen“ und Wohnhäuser) und Berufe mit Werkstatt?</summary>
    private static bool Baut(Wirtschaftsdaten daten) => daten.Bauen is { Wohnhaus.Count: > 0 } && daten.Wohnen is not null && daten.Berufe.Count > 0;

    /// <summary>Wird auf dieser Karte in den Bauzonen begonnen (eine Karte, dazu Abschnitt „bauen“ und Wohnhäuser in den Daten)? Rechnet nichts.</summary>
    public static bool IstAktiv(Stadtkarte? karte, Wirtschaftsdaten daten) => karte is not null && Baut(daten);

    /// <summary>
    /// Wie viele Häuser (Spieler und KI zusammen) auf dieser Karte beginnen können: so viele Paare aus Wohnhaus und Werkstatt, wie in die
    /// Bauzonen im Stadtinneren passen (Kern und Mauerring, dicht gestellt), höchstens <see cref="Partiebesetzung.HoechstensHoefe"/>. 0 ohne
    /// Bauwesen. Dieselbe Zahl nimmt die Kartenprüfung („Zonen reichen für N Häuser“).
    /// </summary>
    public static int Haeuser(Stadtkarte? karte, Wirtschaftsdaten daten) => karte is not null && VorratFuer(karte, daten) is { } v ? v.Dicht.Count : 0;

    /// <summary>
    /// Die geeigneten Startorte des Loses: alle Stellen im Stadtinneren, an denen ein Wohnhaus (Stufe 0) bzw. eine Werkstatt stehen kann, in der
    /// festen Reihenfolge des Bauwesens. Leer ohne Bauwesen.
    /// </summary>
    public static IReadOnlyList<Grundriss> Startstellen(Stadtkarte? karte, Wirtschaftsdaten daten, bool werkstatt) =>
        karte is not null && VorratFuer(karte, daten) is { } v ? (werkstatt ? v.Innen.Werkstaetten : v.Innen.Wohnhaeuser) : Array.Empty<Grundriss>();

    /// <summary>
    /// Lost die Startorte für <paramref name="anzahl"/> Häuser (das erste ist der Spieler, dann die KI-Häuser in ihrer Reihenfolge): je Haus
    /// zuerst das Wohnhaus, dann die Werkstatt, jede Stelle im Stadtinneren gleich wahrscheinlich unter denen, die noch frei sind. Gleicher
    /// Startwert, gleiche Karte = gleiche Orte. Null ohne Bauwesen. Wirft, wenn mehr Häuser beginnen sollen, als passen (<see cref="Haeuser"/>).
    /// </summary>
    public static IReadOnlyList<Startort>? Losen(Stadtkarte karte, Wirtschaftsdaten daten, int zufallsstartwert, int anzahl)
    {
        if (VorratFuer(karte, daten) is not { } vorrat) return null;
        if (anzahl > vorrat.Dicht.Count)
            throw new ArgumentException($"In den Bauzonen der Stadt ist Platz für {vorrat.Dicht.Count} {(vorrat.Dicht.Count == 1 ? "Haus" : "Häuser")} " +
                                        $"(je ein Wohnhaus und eine Werkstatt) – für {anzahl} zu wenig.");
        for (var versuch = 0; versuch < Versuche; versuch++)
        {
            var orte = Aufstellen(vorrat.Innen, anzahl, Losquelle(zufallsstartwert, versuch));
            if (orte.Count == anzahl) return orte;
        }
        // Kein Los ging auf (sehr enge Zonen): die dichte Aufstellung, gemischt – wer wo steht, bleibt Zufall.
        var mischer = Losquelle(zufallsstartwert, Versuche);
        var dicht = vorrat.Dicht.ToList();
        var gemischt = new List<Startort>(anzahl);
        while (gemischt.Count < anzahl)
        {
            var i = mischer.Next(dicht.Count);
            gemischt.Add(dicht[i]);
            dicht.RemoveAt(i);
        }
        return gemischt;
    }
}
