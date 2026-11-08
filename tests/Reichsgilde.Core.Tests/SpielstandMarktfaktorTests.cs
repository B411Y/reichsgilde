using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Mehrspieler;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

// ═════════════════════════════════════════════════════════════════════════════
//  Spielstand: der Faktor der Bürger am Markt nach dem Laden (Balancing nach dem Handgeld, Datenversion 75)
//
//  Das Jahresende lernt das Übliche der Einkäufe neu (Einwohnerschaft), den Faktor an den Marktständen setzt die Bürger-KI erst zur nächsten
//  vollen Stunde. Wer in dieser ersten Stunde des Jahres lud, rechnete bis zum Datenstand 74 den Faktor mit dem neuen Üblichen – durchgehend
//  galt noch das alte. Der Gleichheitstest fand es (Startwert 4711, Laden um 4 Uhr im Jahr 5), sobald ein Karren in dieser Minute verkaufte.
// ═════════════════════════════════════════════════════════════════════════════

static class SpielstandMarktfaktorTests
{
    static readonly DateTime Zeitpunkt = new(2026, 11, 9, 12, 0, 0);

    static string Faktoren(Simulation sim) =>
        string.Join("\n", sim.Wirtschaft!.Markt.Staende.Select(s => $"{s.Ware} {s.NachfrageFaktor:R}"));

    /// <summary>Lädt den Spielstand so, wie ihn ein Kern bis Datenversion 74 schrieb (ohne das Übliche am Markt).</summary>
    static Simulation OhneFeldLaden(string json)
    {
        var knoten = JsonNode.Parse(json)!;
        knoten["Wirtschaft"]!["Einwohner"]!.AsObject().Remove("EinkaufErwartetImMarkt");
        return Speicherung.Laden(knoten.ToJsonString()).Item1;
    }

    /// <summary>
    /// Greybox, Startwert 4711: die erste Minute eines Jahres (ab dem dritten, vorher lernt die Stadt noch ein), an der das neue Übliche den
    /// Faktor am Markt wirklich verschiebt – nicht nur innerhalb der Grenze 1 ± marktSpanne.
    /// </summary>
    static (Simulation Sim, Konto Spieler, string Json) ErsteStundeMitNeuemUeblichen()
    {
        var (sim, spieler) = Partieaufbau.Erstellen(
            Partieeinstellungen.Standard with { Zufallsstartwert = 4711, Siegbedingung = Siegbedingung.Endlos },
            Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()), new NeuePartie(Startvermoegen: 15_000, Beruf: "Tischler"),
            karte: Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));
        var start = sim.Zeit.Jahr;
        for (var jahr = start + 2; jahr <= start + 8; jahr++)
        {
            while (sim.Zeit.Jahr < jahr) sim.Tick();
            var json = Speicherung.Speichern(sim, spieler, Zeitpunkt);
            if (Faktoren(OhneFeldLaden(json)) != Faktoren(sim)) return (sim, spieler, json);
        }
        throw new Exception("In acht Jahren verschiebt kein Jahresende den Faktor am Markt – Startwert oder Zeitraum anpassen");
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Spielstand: Laden in der ersten Stunde nach dem Jahresende – der Faktor der Bürger am Markt und die nächste Stunde wie durchgehend; " +
         "ein älterer Spielstand ohne das Feld rechnet wie bisher mit dem neuen Üblichen (Gegenprobe)", () =>
        {
            var (sim, spieler, json) = ErsteStundeMitNeuemUeblichen();
            Pruefe.Gleich(0, sim.Zeit.MinuteImTag, "erste Minute des Jahres");
            var ew = JsonNode.Parse(json)!["Wirtschaft"]!["Einwohner"]!;
            Pruefe.Wahr(ew["EinkaufErwartetImMarkt"] is not null, "Das Übliche am Markt steht im Spielstand");

            var (geladen, geladenerSpieler) = Speicherung.Laden(json);
            Pruefe.Wahr(geladenerSpieler is not null, "Spieler nach dem Laden");
            Pruefe.Gleich(Faktoren(sim), Faktoren(geladen), "Faktor der Bürger je Marktstand nach dem Laden");

            // Gegenprobe: so lud ein Kern bis Datenversion 74 – mit dem jetzigen Üblichen, wie es der Marktfaktor zeigt
            var alt = OhneFeldLaden(json);
            var e = alt.Wirtschaft!.Einwohnerschaft;
            var k = e.Daten.Kopplung.Markt;
            Pruefe.Ungefaehr(e.Marktfaktor * (1 - k + k * e.Warenkorbfaktor("Truhe")), alt.Wirtschaft.Markt["Truhe"].NachfrageFaktor, 1e-12);

            for (var i = 0; i < 60; i++) { sim.Tick(); geladen.Tick(); }
            Pruefe.Gleich(Zustandspruefsumme.Berechnen(sim), Zustandspruefsumme.Berechnen(geladen), "Prüfsumme nach der ersten Stunde");
            Pruefe.Wahr(JsonNode.Parse(Speicherung.Speichern(sim, spieler, Zeitpunkt))!["Wirtschaft"]!["Einwohner"]!["EinkaufErwartetImMarkt"] is null,
                "Nach der nächsten vollen Stunde gilt das neue Übliche, das Feld fällt wieder weg");
        }),
    };
}
