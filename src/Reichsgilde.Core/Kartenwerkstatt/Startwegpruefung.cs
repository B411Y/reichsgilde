using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Startwege – wie weit die Karren der Startbetriebe zum Markt fahren (Befund M4 der Prüfung von phase5/gehtempo, 1.11.2026)
//
//  Seit Datenversion 65 hängt die Fahrzeit am Weg der Karren (Stadtwirtschaft.Fahrzeit), nicht an der Luftlinie. Die Fairness der Kölner
//  Startgrundstücke (karte.py, Regel 5) misst deshalb den Weg, den der Kern rechnet: vom Halteplatz am Modell über Hof und Zubringer und das
//  Netz aller Wege bis zum Halt am nächsten Marktplatz (Karrenweg). Verglichen wird der Platz, nicht der Beruf: Jede Startwerkstatt mit dem
//  Modell des Schmieds (die Halteplätze der Werkstattmodelle liegen je Beruf bis etwa einen halben Meter auseinander), jeder Umlandbetrieb
//  der Startbelegung mit seinem eigenen Modell. Steigung, Belag und Wasser zählen nicht – nur die Länge.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Startbetrieb und die Länge seines Karrenwegs zum nächsten Marktplatz (null: kein Weg – im Spiel gilt dann die feste Fahrzeit).</summary>
/// <param name="Wer">„Spieler“ oder der Name des Hauses.</param>
/// <param name="Platz">Kennung des Platzes in der Karte.</param>
/// <param name="Ort">Werkstatt in der Stadt oder Umland.</param>
/// <param name="Meter">Weg vom Halteplatz bis zum Halt am Markt.</param>
/// <param name="Markt">Name des Marktplatzes, zu dem er fährt.</param>
public sealed record Startweg(string Wer, string Platz, Arbeitsort Ort, double? Meter, string? Markt, Kartenort Punkt);

public static class Startwegpruefung
{
    /// <summary>So weit darf ein Startweg vom Mittel seiner Art (Werkstatt bzw. Umland) abweichen, sonst ein Hinweis (wie Regel 5: ±10 %).</summary>
    public const double Spanne = 0.10;

    /// <summary>Das Werkstattmodell, mit dem jede Startwerkstatt gemessen wird (der Platz zählt, nicht der Beruf).</summary>
    public const string Bezugsberuf = "Schmied";

    /// <summary>
    /// Die Startwege des Entwurfs: die Werkstatt des Spielers (Grundeinstellung, nicht <c>jeBeruf</c>) und jeder Betrieb der KI-Häuser aus der
    /// Startbelegung, in dieser Reihenfolge. Leer ohne Startbelegung, ohne Marktplatz oder wenn die Karte sich (noch) nicht laden lässt – das
    /// melden andere Regeln.
    /// </summary>
    public static IReadOnlyList<Startweg> Rechnen(Kartenentwurf entwurf)
    {
        if (entwurf.Startnetz() is not var (karte, netz)) return Array.Empty<Startweg>();
        // die Plätze so, wie sie jetzt im Entwurf stehen (das gespeicherte Netz kennt nur Wege, Wasser und Marktplätze)
        Kartengebaeude? Platz(string kennung) => entwurf.GebaeudeMit(kennung) is { } g
            ? new Kartengebaeude { Kennung = g.Kennung, Katalog = g.Katalog, Name = g.Name, Punkt = new[] { g.X, g.Z }, Drehung = g.Drehung, Massstab = g.Massstab, Baustein = g.Baustein }
            : null;
        return Rechnen(karte, netz, entwurf.Start.AlsStartbelegung(), Platz);
    }

    /// <summary>Wie oben für eine geladene Karte.</summary>
    public static IReadOnlyList<Startweg> Rechnen(Stadtkarte karte) =>
        karte.Start is { } start && karte.Marktplaetze.Count > 0
            ? Rechnen(karte, Karrennetz.Von(karte, marktlinien: true, alleWege: true), start, karte.GebaeudeMit)
            : Array.Empty<Startweg>();

    private static IReadOnlyList<Startweg> Rechnen(Stadtkarte karte, Karrennetz netz, Startbelegung start, Func<string, Kartengebaeude?> platzMit)
    {
        if (karte.Marktplaetze.Count == 0) return Array.Empty<Startweg>();
        var liste = new List<Startweg>();
        void Messen(string wer, string kennung, Betriebsart? art)
        {
            if (platzMit(kennung) is not { Baustein: not null } g) return; // fehlt der Platz, meldet das die Startbelegung
            var umland = g.Art == Bausteinart.Umlandbetrieb;
            var ort = umland ? Arbeitsort.Umland : Arbeitsort.Werkstatt;
            var daten = new BetriebsartDaten { Beruf = umland ? "" : Bezugsberuf, Ort = ort };
            var platz = new Betriebsplatz(kennung, g, null);
            var (hin, _, markt, _) = Stadtwirtschaft.StreckeWieImBild(
                Karrenweg.Start(platz, umland ? art ?? Betriebsart.Mine : Betriebsart.Giesserei, daten, karte.Katalog), karte, netz);
            liste.Add(new Startweg(wer, kennung, ort, hin.Count > 0 ? hin.Sum(s => s.Laenge) : null, markt?.Name, new Kartenort(g.Punkt[0], g.Punkt[1])));
        }
        Messen("Spieler", start.Spieler.Werkstatt.Platz, null);
        foreach (var h in start.Haeuser)
            foreach (var b in h.Betriebe)
                Messen(h.Name, b.Platz, b.Art);
        return liste;
    }

    /// <summary>
    /// Hinweise (Regel „startweg“): ein Startbetrieb ohne Weg zum Markt, und je Art (Werkstatt, Umland) jeder Weg, der mehr als
    /// <see cref="Spanne"/> vom Mittel abweicht.
    /// </summary>
    public static List<Befund> Befunde(IReadOnlyList<Startweg> wege)
    {
        var befunde = new List<Befund>();
        static string M(double x) => Zahlenformat.Knapp(Math.Round(x, 1));
        foreach (var w in wege.Where(w => w.Meter is null))
            befunde.Add(new Befund(Schwere.Hinweis, "startweg",
                $"{w.Platz} ({w.Wer}): Kein Weg zum Markt – der Kern nimmt dann die feste Fahrzeit.", new[] { w.Platz }, w.Punkt));
        foreach (var gruppe in wege.Where(w => w.Meter is not null).GroupBy(w => w.Ort).Where(g => g.Count() > 1))
        {
            var mittel = gruppe.Average(w => w.Meter!.Value);
            foreach (var w in gruppe.Where(w => Math.Abs(w.Meter!.Value - mittel) > Spanne * mittel))
                befunde.Add(new Befund(Schwere.Hinweis, "startweg",
                    $"{w.Platz} ({w.Wer}): {M(w.Meter!.Value)} m Karrenweg zum {w.Markt}, {(gruppe.Key == Arbeitsort.Werkstatt ? "Startwerkstätten" : "Start-Umland")} im Mittel {M(mittel)} m (±{Spanne:P0}) – " +
                    $"die Karren brauchen {(w.Meter > mittel ? "länger" : "kürzer")} als die der anderen Häuser.", new[] { w.Platz }, w.Punkt));
        }
        return befunde;
    }
}
