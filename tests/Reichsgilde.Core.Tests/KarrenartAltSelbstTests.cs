using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Spielstände aus „Karren selbst fahren“ (PR #559, Datenversion 98, Format 3) mit den alten Karrennamen: Beim Laden werden sie wie jeder
/// Spielstand bis Format 3 umbenannt (Ochsenkarren → Pferdekarren, Pferdekarren → Großer Pferdekarren, Datenversion 99); die Felder des
/// Selbstfahrens und des Verweilens bleiben erhalten.
/// </summary>
static class KarrenartAltSelbstTests
{
    static readonly DateTime Zeitpunkt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    static long Um(int stunde) => stunde * 60 - Spielzeit.TagesbeginnMinute;

    static (Simulation Sim, Stadtwirtschaft W, Konto K, Betrieb Grube) Aufbau(bool selbst)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with
            {
                StartStatut = Fronstatut.FreieStadt, Siegbedingung = Siegbedingung.Endlos, KarrenSelbstFahren = selbst,
            },
            new Spielzeit(Um(10)), Wirtschaftsdaten.AusJson(Datenpfad.OhneJahreszeiten()));
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Spieler", startvermoegen: 5_000);
        var grube = w.BetriebEroeffnen("Grube", Betriebsart.Mine, k);
        grube.Einlagern("Eisen", 40);
        w.BetriebEroeffnen("G", Betriebsart.Giesserei, k);
        return (sim, w, k, grube);
    }

    static void Ticken(Simulation sim, long bisMinute)
    {
        while (sim.Zeit.VergangeneMinuten < bisMinute) sim.Tick();
    }

    /// <summary>Wie main mit „Karren selbst fahren“ ihn geschrieben hätte: Format 3, Ochsenkarren und (Zweispänner) Pferdekarren.</summary>
    static string AlsMainSpielstand(string json)
    {
        var alt = Regex.Replace(json, "Großer Pferdekarren|Pferdekarren", m => m.Value == "Pferdekarren" ? "Ochsenkarren" : "Pferdekarren");
        Pruefe.Wahr(alt.Contains("\"Ochsenkarren", StringComparison.Ordinal), "alte Namen");
        Pruefe.Wahr(!alt.Contains("Großer Pferdekarren", StringComparison.Ordinal), "kein Großer Pferdekarren");
        var knoten = JsonNode.Parse(alt)!.AsObject();
        Pruefe.Gleich(Speicherstand.AktuellesFormat, (int)knoten["Format"]!);
        knoten["Format"] = 3;
        return knoten.ToJsonString();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Karrenart alt: Selbst gefahrener Spielstand (Format 3, Ochsenkarren) lädt als Pferdekarren mit Ziel, Herkunft und Einstellung", () =>
        {
            var (sim, w, k, grube) = Aufbau(selbst: true);
            Pruefe.Wahr(w.Ausfuehren(k, new KarrenBeladen("Grube", 0, new Dictionary<string, int> { ["Eisen"] = 10 })).Erfolg, "beladen");
            Pruefe.Wahr(w.Ausfuehren(k, new KarrenLosschicken("Grube", new[] { 0 }, "G")).Erfolg, "losgeschickt");
            Ticken(sim, sim.Zeit.VergangeneMinuten + 30);
            var text = Speicherung.Speichern(sim, k, Zeitpunkt);

            var (geladen, spieler) = Speicherung.Laden(AlsMainSpielstand(text));
            var gk = geladen.Wirtschaft!.Betriebe.Single(b => b.Name == "Grube").Karren[0];
            Pruefe.Gleich(("Pferdekarren", "Pferdekarren 1", "G", "Grube"), (gk.Typ, gk.Name, gk.ZielBetrieb, gk.VonBetrieb));
            Pruefe.Wahr(!geladen.Wirtschaft.KarrenAutomatisch && geladen.Einstellungen.KarrenSelbstFahren, "fährt selbst");
            Pruefe.Gleich(text, Speicherung.Speichern(geladen, spieler, Zeitpunkt));

            Ticken(geladen, gk.Ankunft + 1);
            Ticken(sim, grube.Karren[0].Ankunft + 1);
            Pruefe.Gleich("G", gk.StandortBetrieb);
            Pruefe.Gleich(Speicherung.Speichern(sim, k, Zeitpunkt), Speicherung.Speichern(geladen, spieler, Zeitpunkt));
        }),

        ("Karrenart alt: Verweilender Ochsenkarren (Format 3) lädt als Pferdekarren und verweilt bis zur selben Minute", () =>
        {
            var (sim, _, k, grube) = Aufbau(selbst: false);
            sim.Tick();
            var karren = grube.Karren[0];
            Ticken(sim, karren.Ankunft + 5);
            var bis = karren.VerweiltBis!.Value;
            var text = Speicherung.Speichern(sim, k, Zeitpunkt);

            var (geladen, spieler) = Speicherung.Laden(AlsMainSpielstand(text));
            var gk = geladen.Wirtschaft!.Betriebe.Single(b => b.Name == "Grube").Karren[0];
            Pruefe.Gleich(("Pferdekarren", bis), (gk.Typ, gk.VerweiltBis!.Value));
            Pruefe.Gleich(text, Speicherung.Speichern(geladen, spieler, Zeitpunkt));
        }),
    };
}
