using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Aufschlag am Hafen (Datenversion 80, Nutzerentscheidung „B“ vom 09.11.2026, <c>schiffshandel.hafenAufschlag</c> = 0,03): Verkauft ein Karren
/// eine Ausfuhrware (eine Ware, die eine ferne Stadt kauft) direkt am offenen Hafen, bringt sie 3 % mehr; am Marktplatz, für andere Waren und am
/// geschlossenen Hafen (Winter) nichts. Preis und Vorrat des Markts bleiben dieselben. Die Proben stehen in Köln am Bauort mit der kürzesten Fahrt
/// zum Hafen (wie in den Hafenziel-Tests); verglichen wird jeweils dieselbe Partie mit und ohne Aufschlag.
/// </summary>
static class HafenAufschlagTests
{
    const string Hand = "Handkarren";

    /// <summary>Die Wirtschaftsdaten mit Bauzeit 0 für Werkstätten (die Werkstatt steht sofort) und diesem Aufschlag am Hafen.</summary>
    internal static string Daten(double? aufschlag, bool zumHafen = true)
    {
        const string werkstatt = "\"werkstatt\": { \"breite\": 16, \"tiefe\": 14, \"bauzeitJahre\": 1,";
        var json = File.ReadAllText(Datenpfad.Wirtschaft());
        if (!json.Contains(werkstatt)) throw new InvalidOperationException("Werkstatt-Maße in wirtschaft.json haben sich geändert – Test anpassen.");
        json = json.Replace(werkstatt, "\"werkstatt\": { \"breite\": 16, \"tiefe\": 14, \"bauzeitJahre\": 0,");
        if (!zumHafen) json = Datenpfad.OhneHafenziel(json);
        var knoten = JsonNode.Parse(json)!;
        if (aufschlag is { } a) knoten["schiffshandel"]!["hafenAufschlag"] = a;
        else knoten["schiffshandel"]!.AsObject().Remove("hafenAufschlag"); // alte Daten (bis Datenversion 78)
        return knoten.ToJsonString();
    }

    internal static long Minute(int jahr, int stunde) => jahr * (long)Spielzeit.MinutenProTag + stunde * 60 - Spielzeit.TagesbeginnMinute;

    /// <summary>Der Bauort in Köln mit der kürzesten Fahrt (hin + zurück) zum Hafen, an dem der Hafen näher ist als jeder Marktplatz.</summary>
    internal static readonly Lazy<WerkstattBauen> NahAmHafen = new(() =>
    {
        var (_, w, k) = Koeln(Daten(0.03), Minute(1, 9));
        int Rund(int? hin, int? zurueck) => (hin ?? 9999) + (zurueck ?? 9999);
        return w.Bauen.Bauorte(k, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0)).Cast<WerkstattBauen>()
            .Select(o => (Ort: o, Hafen: Rund(w.FahrzeitZumHafenAmBauort(o, Hand), w.FahrzeitZumHafenAmBauort(o, Hand, false)),
                Markt: Rund(w.FahrzeitAmBauort(o, Hand), w.FahrzeitAmBauort(o, Hand, false))))
            .Where(x => x.Hafen < x.Markt)
            .OrderBy(x => x.Hafen).ThenBy(x => x.Ort.X).ThenBy(x => x.Ort.Z).ThenBy(x => x.Ort.Drehung)
            .First().Ort;
    });

    internal static (Simulation Sim, Stadtwirtschaft W, Konto K) Koeln(string json, long minute)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 },
            new Spielzeit(minute), Wirtschaftsdaten.AusJson(json));
        var w = sim.Wirtschaft!;
        w.KarteSetzen(Stadtkarte.AusDatei(Datenpfad.Karte("koeln")));
        w.EinwohnerZaehlen();
        var k = w.KontoEroeffnen("Haus Probe", startvermoegen: 1_000_000);
        k.Stand = 1;
        k.WohnhausStufe = 1;
        k.BerufLernen("Schmied");
        w.Wohnen.HauswertAktualisieren(k);
        return (sim, w, k);
    }

    /// <summary>
    /// Köln zu dieser Minute, eine Gießerei am Hafen mit <paramref name="stueck"/> Stück dieser Ware im Lager (ohne Zutaten: Der Karren bricht in
    /// der ersten Minute auf und nimmt die Ware mit; der Handkarren fasst 15 je Ladefläche). Bis er verkauft hat und umkehrt: Ziel, Umsatz und
    /// Steuer der Werkstatt.
    /// </summary>
    static (bool ZielHafen, double Umsatz, double Steuer, bool HafenOffen) Verkauf(string json, long minute, string ware, int stueck = 15,
        bool handelsauftrag = false)
    {
        var (sim, w, k) = Koeln(json, minute);
        var e = w.Ausfuehren(k, NahAmHafen.Value);
        Pruefe.Wahr(e.Erfolg, e.Meldung);
        var name = w.Bauen.Grundstuecke.Last(g => g.Art == Grundstuecksart.Werkstatt).Betrieb!;
        var b = w.Betriebe.Single(x => x.Name == name);
        b.Einlagern(ware, stueck);
        var karren = b.Karren[0];
        if (handelsauftrag) // Handel von Hand: derselbe Karren mit Auftrag (verkaufen, nichts kaufen)
        {
            var a = w.Ausfuehren(k, new Handelsauftrag(b.Name, 0, new Dictionary<string, int> { [ware] = stueck }, new Dictionary<string, int>()));
            Pruefe.Wahr(a.Erfolg, a.Meldung);
        }
        for (var i = 0; i < Spielzeit.MinutenProTag && karren.Zustand != KarrenZustand.ZumMarkt; i++) sim.Tick();
        Pruefe.Gleich(KarrenZustand.ZumMarkt, karren.Zustand, "aufgebrochen");
        Pruefe.Gleich(stueck, karren.Ladung.GetValueOrDefault(ware), $"{ware} geladen");
        Pruefe.Gleich(0.0, b.Laufend.Umsatz, "vor der Ankunft kein Umsatz");
        for (var i = 0; i < Spielzeit.MinutenProTag && karren.Zustand != KarrenZustand.ZumBetrieb; i++) sim.Tick();
        Pruefe.Gleich(KarrenZustand.ZumBetrieb, karren.Zustand, "verkauft und auf dem Rückweg");
        Pruefe.Wahr(b.Laufend.Umsatz > 0, "verkauft");
        return (karren.ZielHafen, b.Laufend.Umsatz, b.Laufend.Steuer, w.Schiffahrt.HafenOffen);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Hafenaufschlag: Daten – 3 % in den Spieldaten, Ausfuhrwaren sind genau die Waren, die eine ferne Stadt kauft (dieselben wie die Nachfrage " +
         "des Landhandels); ohne Angabe 0, über 5 % oder negativ ein Fehler", () =>
        {
            var d = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
            Pruefe.Gleich(84, d.Version);
            var s = d.Schiffshandel!;
            Pruefe.Ungefaehr(0.03, s.HafenAufschlag, 1e-12);
            var ausfuhr = d.Waren.Keys.Where(s.IstAusfuhrware).OrderBy(x => x, StringComparer.Ordinal).ToList();
            Pruefe.Gleich(string.Join(",", s.Landhandel.Nachfrage.Keys.OrderBy(x => x, StringComparer.Ordinal)), string.Join(",", ausfuhr));
            Pruefe.Wahr(s.IstAusfuhrware("Silberring") && s.IstAusfuhrware("Dolch") && !s.IstAusfuhrware("Beschlag") && !s.IstAusfuhrware("Eisen"), "Gießerei");
            Pruefe.Gleich(0.0, Wirtschaftsdaten.AusJson(Daten(null)).Schiffshandel!.HafenAufschlag);
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Daten(0.06)), "hafenAufschlag");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Daten(-0.01)), "hafenAufschlag");
            Wirtschaftsdaten.AusJson(Daten(0.05)); // 5 % geht
        }),

        ("Hafenaufschlag: Ein Karren verkauft Silberringe (Ausfuhrware) im Sommer am Hafen – Umsatz und Steuer genau 3 % höher als ohne Aufschlag; " +
         "Beschläge (keine Ausfuhrware) gleich; Gegenprobe: am Marktplatz (ohne Karren zum Hafen) kein Aufschlag; Handelsauftrag am Hafen ebenso 3 %", () =>
        {
            var mit = Verkauf(Daten(0.03), Minute(1, 9), "Silberring");
            var ohne = Verkauf(Daten(0), Minute(1, 9), "Silberring");
            Pruefe.Wahr(mit.ZielHafen && ohne.ZielHafen && mit.HafenOffen, "am offenen Hafen verkauft");
            Pruefe.Ungefaehr(ohne.Umsatz * 1.03, mit.Umsatz, 1e-9 * mit.Umsatz);
            Pruefe.Ungefaehr(ohne.Steuer * 1.03, mit.Steuer, 1e-9 * mit.Steuer + 1e-12);
            var alt = Verkauf(Daten(null), Minute(1, 9), "Silberring");
            Pruefe.Gleich(ohne.Umsatz, alt.Umsatz); // alte Daten ohne Feld: wie 0

            var beschlagMit = Verkauf(Daten(0.03), Minute(1, 9), "Beschlag");
            var beschlagOhne = Verkauf(Daten(0), Minute(1, 9), "Beschlag");
            Pruefe.Wahr(beschlagMit.ZielHafen, "Beschläge am Hafen verkauft");
            Pruefe.Gleich(beschlagOhne.Umsatz, beschlagMit.Umsatz);

            var marktMit = Verkauf(Daten(0.03, zumHafen: false), Minute(1, 9), "Silberring");
            var marktOhne = Verkauf(Daten(0, zumHafen: false), Minute(1, 9), "Silberring");
            Pruefe.Wahr(!marktMit.ZielHafen, "zum Marktplatz");
            Pruefe.Gleich(marktOhne.Umsatz, marktMit.Umsatz);

            // Handel von Hand (Handelsauftrag) am Hafen: derselbe Aufschlag
            var handMit = Verkauf(Daten(0.03), Minute(1, 9), "Silberring", handelsauftrag: true);
            var handOhne = Verkauf(Daten(0), Minute(1, 9), "Silberring", handelsauftrag: true);
            Pruefe.Wahr(handMit.ZielHafen && handMit.HafenOffen, "Handelsauftrag am Hafen");
            Pruefe.Ungefaehr(handOhne.Umsatz * 1.03, handMit.Umsatz, 1e-9 * handMit.Umsatz);
        }),

        ("Hafenaufschlag: Bricht der Karren im späten Herbst zum Hafen auf und kommt im Winter an (Hafen zu), gibt es keinen Aufschlag; " +
         "Gegenprobe im Herbst um 9 Uhr: mit Aufschlag", () =>
        {
            var spaet = (2 + 1) * (long)Spielzeit.MinutenProTag - 10; // Herbst, zehn Minuten vor dem Jahreswechsel
            var mit = Verkauf(Daten(0.03), spaet, "Silberring");
            var ohne = Verkauf(Daten(0), spaet, "Silberring");
            Pruefe.Wahr(mit.ZielHafen && !mit.HafenOffen, "zum Hafen gefahren, angekommen im Winter");
            Pruefe.Gleich(ohne.Umsatz, mit.Umsatz);

            var herbstMit = Verkauf(Daten(0.03), Minute(2, 9), "Silberring");
            var herbstOhne = Verkauf(Daten(0), Minute(2, 9), "Silberring");
            Pruefe.Wahr(herbstMit.ZielHafen && herbstMit.HafenOffen, "Herbst: offen");
            Pruefe.Ungefaehr(herbstOhne.Umsatz * 1.03, herbstMit.Umsatz, 1e-9 * herbstMit.Umsatz);
        }),
    };
}
