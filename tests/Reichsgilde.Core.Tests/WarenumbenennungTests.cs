using System.IO.Compression;
using System.Text.RegularExpressions;
using Reichsgilde.Core;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

// ─────────────────────────────────────────────────────────────────────────────
//  Dolch statt Rapier (PR #232): Alte Spielstände mit der Ware „Rapier“ laden weiter, die Ware heißt danach überall „Dolch“.
//  Der alte Spielstand ist derselbe wie in UhrumstellungTests (Greybox, Herbst 1406, Eisenbart mit Gießerei und Schiffen).
// ─────────────────────────────────────────────────────────────────────────────

static class WarenumbenennungTests
{
    static readonly Lazy<string> AlterStand = new(() =>
    {
        var datei = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
            "tests", "Reichsgilde.Core.Tests", "Spielstaende", "format2-greybox-herbst1406.json.gz");
        using var gz = new GZipStream(File.OpenRead(datei), CompressionMode.Decompress);
        return new StreamReader(gz).ReadToEnd();
    });

    static int Anzahl(string text, string wort) => Regex.Matches(text, $@"\b{wort}\b").Count;

    static void Ticken(Simulation sim, int minuten)
    {
        for (var i = 0; i < minuten; i++) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Dolch statt Rapier: Texte mit Endungen (Rapiere → Dolche, Rapieren → Dolchen, Rapiers → Dolchs), andere Wörter bleiben", () =>
        {
            Pruefe.Gleich("Dolch", Warenumbenennung.Text("Rapier"));
            Pruefe.Gleich("3 Dolch und 2 Silberring (Wert 412)", Warenumbenennung.Text("3 Rapier und 2 Silberring (Wert 412)"));
            Pruefe.Gleich("zu Beschlägen, Dolchen und Silberringen; Dolche; des Dolchs", Warenumbenennung.Text("zu Beschlägen, Rapieren und Silberringen; Rapiere; des Rapiers"));
            Pruefe.Gleich("Giftdolch, Rapierklinge, rapier", Warenumbenennung.Text("Giftdolch, Rapierklinge, rapier"));
            Pruefe.Gleich(("Dolch", "Silberring"), (Warenumbenennung.NeuerName("Rapier"), Warenumbenennung.NeuerName("Silberring")));
        }),

        ("Dolch statt Rapier: Ein alter Spielstand mit Rapieren lädt – Daten, Werkstätten, Lager, Markt, Karren und Chronik heißen danach Dolch", () =>
        {
            var json = AlterStand.Value;
            Pruefe.Wahr(Anzahl(json, "Rapier") >= 15, $"Rapier im alten Spielstand: {Anzahl(json, "Rapier")}");
            var (sim, spieler) = Speicherung.Laden(json);
            var w = sim.Wirtschaft!;
            Pruefe.Wahr(w.Daten.Waren.ContainsKey("Dolch") && !w.Daten.Waren.ContainsKey("Rapier"), "Ware in den mitgespeicherten Daten");
            Pruefe.Wahr(w.Markt.Staende.Any(m => m.Ware == "Dolch") && w.Markt.Staende.All(m => m.Ware != "Rapier"), "Marktstand");
            Pruefe.Wahr(w.Daten.Art(Betriebsart.Giesserei).Produkte.Contains("Dolch"), "Produkt der Gießerei");
            Pruefe.Wahr(w.Betriebe.Any(b => b.ImLager("Dolch") > 0), "Dolche im Lager");
            // Die alte Partie läuft mit ihren Zahlen weiter: Werte des Rapiers, Giftdolch noch aus Eisen.
            Pruefe.Gleich((141.0, 60.0), (w.Daten.Waren["Dolch"].Grundpreis, w.Daten.Waren["Dolch"].Zeit));
            Pruefe.Gleich(1, w.Daten.Waren["Giftdolch"].Zutaten["Eisen"]);

            var neu = Speicherung.Speichern(sim, spieler, DateTime.UnixEpoch);
            Pruefe.Gleich(0, Anzahl(neu, "Rapier|Rapiere|Rapieren|Rapiers"), "kein Rapier mehr im neuen Spielstand");
            Pruefe.Wahr(neu.Contains("in der Gießerei zu Beschlägen, Dolchen und Silberringen"), "Berufsbeschreibung");
            Pruefe.Wahr(neu.Contains("Gießerei Eisenbart stellt von Dolch auf Silberring um"), "Chronik der KI");
        }),

        ("Dolch statt Rapier: Umbenannt geladen = nie umbenannt – derselbe Spielstand, Wurf für Wurf gleich weiter", () =>
        {
            // Bezug: der alte Spielstand, einmal geladen und mit „Dolch“ gespeichert. Daraus wieder ein „alter“ mit Rapieren.
            var (sim, spieler) = Speicherung.Laden(AlterStand.Value);
            Ticken(sim, 120);
            var bezug = Speicherung.Speichern(sim, spieler, DateTime.UnixEpoch);
            Pruefe.Wahr(!bezug.Contains("\"Dolch\": \""), "kein Giftanschlag geplant (dessen Feld heißt „Dolch“)");
            // Das Feld „Dolche“ der Giftdaten (Giftdolch, Schattendolch) hieß schon immer so – es bleibt.
            var alt = Regex.Replace(bezug, @"\bDolch(en|e|s)?\b(?!""\s*:\s*\{\s*""Giftdolch)", "Rapier$1");
            Pruefe.Wahr(Anzahl(alt, "Rapier") >= 10, $"Rapier im nachgebauten Spielstand: {Anzahl(alt, "Rapier")}");

            var (kopie, kopieSpieler) = Speicherung.Laden(alt);
            Pruefe.Gleich(bezug, Speicherung.Speichern(kopie, kopieSpieler, DateTime.UnixEpoch));
            Ticken(sim, 600);
            Ticken(kopie, 600);
            Pruefe.Gleich(Speicherung.Speichern(sim, spieler, DateTime.UnixEpoch), Speicherung.Speichern(kopie, kopieSpieler, DateTime.UnixEpoch));
        }),

        ("Dolch statt Rapier: Ein Spielstand ohne Rapier bleibt zeichengleich (auch das Feld „Dolch“ eines Giftanschlags)", () =>
        {
            var knoten = System.Text.Json.Nodes.JsonNode.Parse("""{"Auftraege": [{"Dolch": "Giftdolch"}], "Lager": {"Dolch": 3}, "Text": "Ein Dolch hat ihn gestreift"}""");
            var vorher = knoten!.ToJsonString();
            Warenumbenennung.Umbenennen(knoten);
            Pruefe.Gleich(vorher, knoten.ToJsonString());
            var alt = System.Text.Json.Nodes.JsonNode.Parse("""{"Lager": {"Eisen": 1, "Rapier": 3, "Silber": 2}, "Waren": ["Beschlag", "Rapier"]}""");
            Warenumbenennung.Umbenennen(alt);
            Pruefe.Gleich("""{"Lager":{"Eisen":1,"Dolch":3,"Silber":2},"Waren":["Beschlag","Dolch"]}""", alt!.ToJsonString());
        }),
    };
}
