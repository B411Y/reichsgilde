using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Höchstzahl der Karren je Betrieb nach Arbeitsort (Datenversion 66, Entscheidung des Nutzers vom 2.11.2026): Werkstatt 3, Umland 5 – für Spieler
/// und KI gleich (<c>logistik.karrenHoechstens</c>, <see cref="Stadtwirtschaft.KarrenKaufVerboten"/>). Die KI kauft den vierten und fünften Karren
/// im Umland nur wie jeden: nach Stillstand und wenn er sich in der Frist bezahlt macht.
/// </summary>
static class KarrenHoechstzahlTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Wirtschaftsdaten DatenMit(Action<JsonObject> aendern)
    {
        var knoten = JsonNode.Parse(File.ReadAllText(Datenpfad.Wirtschaft()))!.AsObject();
        aendern(knoten);
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    }

    /// <summary>Köln, „Schwer“: die KI-Häuser mit Werkstatt und Umlandbetrieb.</summary>
    static (Simulation Sim, Stadtwirtschaft W) Koeln(Wirtschaftsdaten? daten = null)
    {
        var pfad = Datenpfad.Karte("koeln");
        var haeuser = JsonNode.Parse(File.ReadAllText(pfad))!["start"]!["haeuser"]!.AsArray().Count;
        var (sim, _) = Partieaufbau.Erstellen(
            Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = Schwierigkeit.Schwer },
            daten ?? Daten.Value, new NeuePartie(Rivalen: haeuser), karte: Stadtkarte.AusDatei(pfad));
        return (sim, sim.Wirtschaft!);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Karren-Höchstzahl: Daten ab 66 – Werkstatt 3, Umland 5; Prüfung meldet unbekannte Orte und Werte unter 1", () =>
        {
            var d = Daten.Value;
            Pruefe.Gleich(69, d.Version);
            Pruefe.Gleich(3, d.Logistik.HoechstKarren(Arbeitsort.Werkstatt));
            Pruefe.Gleich(5, d.Logistik.HoechstKarren(Arbeitsort.Umland));
            Pruefe.Wirft(() => DatenMit(j => j["logistik"]!["karrenHoechstens"]!["Umland"] = 0));
            Pruefe.Wirft(() => DatenMit(j => j["logistik"]!["karrenHoechstens"]!["Mond"] = 2));
            var ohne = DatenMit(j => j["logistik"]!.AsObject().Remove("karrenHoechstens"));
            Pruefe.Gleich(null, ohne.Logistik.HoechstKarren(Arbeitsort.Umland));
        }),

        ("Karren-Höchstzahl: Der Spieler kauft im Umland bis zum fünften Karren, in der Stadt bis zum dritten – dann lehnt der Kern ab", () =>
        {
            var (_, w) = Koeln();
            var k = w.KontoEroeffnen("Haus Lind", startvermoegen: 50_000);
            var umland = w.BetriebEroeffnen("Grube am Silberberg", Betriebsart.Mine, k, "");
            var stadt = w.BetriebEroeffnen("Gießerei Lind", Betriebsart.Giesserei, k, "");
            for (var i = 0; i < 5; i++)
                Pruefe.Wahr(w.Ausfuehren(k, new KarrenKaufen(umland.Name, "Ochsenkarren")).Erfolg, $"Umland Karren {i + 1}");
            var sechster = w.Ausfuehren(k, new KarrenKaufen(umland.Name, "Ochsenkarren"));
            Pruefe.Wahr(!sechster.Erfolg && sechster.Meldung.Contains("5 Karren"), sechster.Meldung);
            Pruefe.Gleich(5, umland.Karren.Count);
            for (var i = 0; i < 3; i++)
                Pruefe.Wahr(w.Ausfuehren(k, new KarrenKaufen(stadt.Name, "Handkarren")).Erfolg, $"Stadt Karren {i + 1}");
            Pruefe.Wahr(w.KarrenKaufVerboten(stadt) is not null, "der vierte in der Stadt");
            Pruefe.Wahr(!w.Ausfuehren(k, new KarrenKaufen(stadt.Name, "Handkarren")).Erfolg, "abgelehnt");
            Pruefe.Gleich(3, stadt.Karren.Count);
        }),

        ("Karren-Höchstzahl: Ohne Eintrag (Daten vor 66) keine Grenze im Kern, die KI bleibt bei drei (KiProfil)", () =>
        {
            var (_, w) = Koeln(DatenMit(j => j["logistik"]!.AsObject().Remove("karrenHoechstens")));
            var k = w.KontoEroeffnen("Haus Lind", startvermoegen: 50_000);
            var b = w.BetriebEroeffnen("Grube am Silberberg", Betriebsart.Mine, k, "");
            for (var i = 0; i < 7; i++) Pruefe.Wahr(w.Ausfuehren(k, new KarrenKaufen(b.Name, "Ochsenkarren")).Erfolg, $"Karren {i + 1}");
            Pruefe.Gleich(KiProfil.Standard.MaxKarrenJeBetrieb, w.Rivalen[0].MaxKarren(Arbeitsort.Umland));
        }),

        ("Karren-Höchstzahl: Die KI kauft am gebremsten Holzschlag mit drei Karren einen vierten, wenn er sich bezahlt macht – an der Grenze von fünf nur noch Tausch", () =>
        {
            var (_, w) = Koeln();
            var holz = w.Betriebe.Single(b => b.Name == "Holzschlag Hardevust");
            var ki = w.Rivalen.Single(r => r.Konto == holz.Konto);
            holz.Konto.Entwicklerhilfe(20_000);
            Pruefe.Gleich(5, ki.MaxKarren(Arbeitsort.Umland));
            Pruefe.Gleich(3, ki.MaxKarren(Arbeitsort.Werkstatt));
            while (holz.Karren.Count < 3) holz.KarrenKaufen("Kriegskarren");
            // bei 25 % Stillstand: unter der Grenze nur Käufe (je Art einer)
            var plaene = ki.Karrenplaene(holz, 0.25).ToList();
            Pruefe.Wahr(plaene.All(p => p.Tausch is null) && plaene.Count == w.Daten.Logistik.Karren.Count, "Käufe");
            var bester = ki.BesterKarrenplan(holz, 0.25);
            Pruefe.Wahr(bester is { Tausch: null }, "ein vierter Karren rechnet sich");
            Pruefe.Wahr(bester!.Value.Kosten <= bester.Value.Mehrgewinn * ki.Amortisationsjahre, "in der Frist bezahlt");
            // unter 20 % kauft die KI zum Jahresende nichts (KarrenNachkaufen), und ein Plan ohne Mehrgewinn wird nie gewählt
            Pruefe.Gleich(null, ki.BesterKarrenplan(holz, 0.0));
            while (holz.Karren.Count < 5) holz.KarrenKaufen("Kriegskarren");
            Pruefe.Wahr(ki.Karrenplaene(holz, 0.25).All(p => p.Tausch is not null), "an der Grenze nur Tausch");
        }),
    };
}
