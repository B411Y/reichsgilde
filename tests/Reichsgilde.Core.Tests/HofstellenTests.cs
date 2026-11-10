using System.Text.Json;
using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;

/// <summary>
/// Hofstellen (Nutzerentscheidung 09.11.2026): das Modell in der Karte, die Umrechnung alter Karten (Startpaare → Start-Hofstellen) und die
/// Kartenprüfung (Regel „hofstelle“, Startwege über alle Start-Hofstellen). Start und Bauen: <see cref="HofstellenStartTests"/>,
/// <see cref="HofstellenBauenTests"/>; die Befehle der Kartenwerkstatt: <see cref="KartenwerkstattHofstelleTests"/>.
/// </summary>
static class HofstellenTests
{
    internal static readonly Lazy<Gebaeudekatalog> Katalog =
        new(() => Gebaeudekatalog.AusDatei(Path.Combine(Path.GetDirectoryName(Datenpfad.Wirtschaft())!, "katalog.json")));

    /// <summary>Der Text einer Kartendatei ohne „start.hofstellen“ – so sah sie vor den Hofstellen aus.</summary>
    internal static string OhneHofstellen(string json)
    {
        var n = JsonNode.Parse(json)!.AsObject();
        (n["start"] as JsonObject)?.Remove("hofstellen");
        return n.ToJsonString();
    }

    /// <summary>
    /// Eine Karte von vor den Hofstellen, wie sie in einem alten Spielstand steht: ohne „start.hofstellen“ und ohne die Umrechnung von
    /// <see cref="Stadtkarte.AusJson"/> (der Spielstand liest seine Karte direkt).
    /// </summary>
    internal static Stadtkarte AlteKarte(string kennung)
    {
        var karte = JsonSerializer.Deserialize<Stadtkarte>(OhneHofstellen(File.ReadAllText(Datenpfad.Karte(kennung))), Stadtkarte.Optionen)!;
        karte.Katalog = Katalog.Value;
        Pruefe.Gleich(0, karte.Pruefen().Count, "alte Karte ohne Fehler");
        Pruefe.Wahr(!karte.HatHofstellen, "ohne Hofstellen");
        return karte;
    }

    static string Beschreibung(IEnumerable<Hofstelle> h) =>
        string.Join(" | ", h.Select(x => $"{x.Kennung} {x.Name ?? "-"} {x.Werkstatt} {x.Wohnhaus}{(x.Start ? " start" : "")}"));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Hofstellen: Eine alte Karte bekommt beim Lesen aus jedem Startpaar eine Start-Hofstelle (Greybox H1–H4, Köln S1–S8 aus den Kennungen); der Spieler zuerst, mit dem Namen seiner Werkstatt", () =>
        {
            var greybox = Stadtkarte.AusJson(OhneHofstellen(File.ReadAllText(Datenpfad.Karte("greybox"))), Katalog.Value);
            Pruefe.Gleich("H1 Weinmarkt Weinmarkt Wohnhaus1 start | H2 - Giesserei_Eisenbart Wohnhaus2 start | H3 - Tischlerei_Eichwald Wohnhaus3 start | H4 - Maurerei_Kieselbach Wohnhaus4 start",
                Beschreibung(greybox.Hofstellen));
            var koeln = Stadtkarte.AusJson(OhneHofstellen(File.ReadAllText(Datenpfad.Karte("koeln"))), Katalog.Value);
            Pruefe.Gleich("S1, S2, S3, S4, S5, S6, S7, S8", string.Join(", ", koeln.Hofstellen.Select(h => h.Kennung)));
            Pruefe.Gleich("Heumarkt", koeln.Hofstellen[0].Name);
            Pruefe.Wahr(koeln.Hofstellen.All(h => h.Start && h.Werkstatt == h.Kennung + "_Werkstatt" && h.Wohnhaus == h.Kennung + "_Wohnhaus"), "Köln: Plätze S<n>_…");
            // Gegenprobe: ohne Startbelegung keine Hofstellen; eine Karte mit Hofstellen behält genau ihre
            var leer = Kartenentwurf.Neu("leer", "Leer", 400, 300, Katalog.Value).ZuStadtkarte();
            Pruefe.Gleich(0, leer.Hofstellen.Count);
            var eigene = JsonNode.Parse(File.ReadAllText(Datenpfad.Karte("greybox")))!.AsObject();
            eigene["start"]!["hofstellen"] = JsonNode.Parse("""[{"kennung": "X", "werkstatt": "Weinmarkt", "wohnhaus": "Wohnhaus5"}]""");
            var mit = Stadtkarte.AusJson(eigene.ToJsonString(), Katalog.Value);
            Pruefe.Gleich("X - Weinmarkt Wohnhaus5", Beschreibung(mit.Hofstellen));
        }),

        ("Hofstellen: Die Kartenwerkstatt rechnet eine alte Karte ebenso um und schreibt die Hofstellen unter start.hofstellen; wieder geöffnet dieselben", () =>
        {
            var e = Kartenentwurf.AusJson(OhneHofstellen(File.ReadAllText(Datenpfad.Karte("greybox"))), Katalog.Value);
            Pruefe.Gleich("H1, H2, H3, H4", string.Join(", ", e.Start.Hofstellen.Select(h => h.Kennung)));
            var text = e.ZuJson();
            var liste = JsonNode.Parse(text)!["start"]!["hofstellen"]!.AsArray();
            Pruefe.Gleich(4, liste.Count);
            Pruefe.Gleich("""{"kennung":"H1","name":"Weinmarkt","werkstatt":"Weinmarkt","wohnhaus":"Wohnhaus1","start":true}""", liste[0]!.ToJsonString());
            var wieder = Kartenentwurf.AusJson(text, Katalog.Value);
            Pruefe.Gleich(text, wieder.ZuJson());
            Pruefe.Wahr(wieder.Start.Hofstellen.SequenceEqual(e.Start.Hofstellen), "dieselben Hofstellen");
        }),

        ("Hofstellen: Der Kern lehnt eine Karte ab, deren Hofstelle keinen Werkstatt- bzw. Wohnhausplatz trifft oder deren Platz zu zwei Hofstellen gehört", () =>
        {
            string? Fehler(string hofstellen)
            {
                var n = JsonNode.Parse(File.ReadAllText(Datenpfad.Karte("greybox")))!.AsObject();
                n["start"]!["hofstellen"] = JsonNode.Parse(hofstellen);
                try { Stadtkarte.AusJson(n.ToJsonString(), Katalog.Value); return null; }
                catch (KartenFehler f) { return string.Join(" ", f.Fehler); }
            }
            Pruefe.Wahr(Fehler("""[{"kennung":"A","werkstatt":"Wohnhaus5","wohnhaus":"Wohnhaus6"}]""") is { } a && a.Contains("„Wohnhaus5“ ist kein Werkstattplatz"), "Werkstatt falsch");
            Pruefe.Wahr(Fehler("""[{"kennung":"A","werkstatt":"Weinmarkt","wohnhaus":"Giesserei_Eisenbart"}]""") is { } b && b.Contains("kein Wohnhausplatz"), "Wohnhaus falsch");
            Pruefe.Wahr(Fehler("""[{"kennung":"A","werkstatt":"Weinmarkt","wohnhaus":"Wohnhaus5"},{"kennung":"B","werkstatt":"Weinmarkt","wohnhaus":"Wohnhaus6"}]""") is { } c
                && c.Contains("gehört zu mehreren Hofstellen"), "Platz doppelt");
            Pruefe.Wahr(Fehler("""[{"kennung":"A","werkstatt":"Weinmarkt","wohnhaus":"Wohnhaus5"},{"kennung":"A","werkstatt":"Giesserei_Eisenbart","wohnhaus":"Wohnhaus6"}]""") is { } d
                && d.Contains("Kennung „A“ ist doppelt"), "Kennung doppelt");
            Pruefe.Gleich(null, Fehler("""[{"kennung":"A","werkstatt":"Weinmarkt","wohnhaus":"Wohnhaus5"}]""")); // Gegenprobe
        }),

        ("Hofstellen: Kartenprüfung – zu wenig Start-Hofstellen für Spieler und KI-Häuser ist ein Fehler, eine Vorgabe außerhalb der Start-Hofstellen ein Hinweis", () =>
        {
            var e = Kartenentwurf.AusJson(OhneHofstellen(File.ReadAllText(Datenpfad.Karte("greybox"))), Katalog.Value);
            Pruefe.Wahr(e.Befunde.All(b => b.Regel != "hofstelle"), "umgerechnet: nichts zu melden");
            e.HofstelleStartSetzen("H4", false);
            var fehler = e.Befunde.Where(b => b.Regel == "hofstelle" && b.Schwere == Schwere.Fehler).Select(b => b.Text).ToList();
            Pruefe.Gleich("Hofstellen: 3 Startplätze für den Spieler und 3 KI-Häuser – es braucht mindestens 4.", string.Join(" / ", fehler));
            Pruefe.Wahr(e.Befunde.Any(b => b.Regel == "hofstelle" && b.Schwere == Schwere.Hinweis && b.Text.Contains("Haus Kieselbach") && b.Text.Contains("kein Startplatz")),
                "Vorgabe Kieselbach: Hinweis");
            e.Rueckgaengig();
            Pruefe.Wahr(e.Befunde.All(b => b.Regel != "hofstelle"), "zurückgenommen");
        }),

        ("Hofstellen: Die Startwege messen jede Start-Hofstelle (Werkstatt mit dem Modell des Schmieds) und die Umlandbetriebe – ohne Hofstellen wie bisher die Vorgabe", () =>
        {
            var e = Kartenentwurf.AusJson(OhneHofstellen(File.ReadAllText(Datenpfad.Karte("greybox"))), Katalog.Value);
            var wege = e.Startwegrechnung();
            Pruefe.Gleich("Hofstelle H1, Hofstelle H2, Hofstelle H3, Hofstelle H4, Haus Eisenbart, Haus Eichwald, Haus Kieselbach", string.Join(", ", wege.Select(w => w.Wer)));
            Pruefe.Wahr(wege.Take(4).All(w => w.Meter is > 0 && w.Ort == Reichsgilde.Core.Wirtschaft.Arbeitsort.Werkstatt), "Werkstätten mit Weg");
            // Gegenprobe: die alte Karte misst wie bisher Spieler und Betriebe der Vorgabe
            var alt = Startwegpruefung.Rechnen(AlteKarte("greybox"));
            Pruefe.Gleich("Spieler, Haus Eisenbart, Haus Eisenbart, Haus Eichwald, Haus Eichwald, Haus Kieselbach, Haus Kieselbach", string.Join(", ", alt.Select(w => w.Wer)));
            Pruefe.Gleich(wege[0].Meter, alt[0].Meter); // derselbe Platz, derselbe Weg
            // Das Angebot des Startmenüs: die Start-Hofstellen mit ihrem Weg
            var karte = e.ZuStadtkarte();
            var angebot = Hofstellenvergabe.Angebot(karte);
            Pruefe.Gleich("H1, H2, H3, H4", string.Join(", ", angebot.Select(a => a.Kennung)));
            Pruefe.Gleich(wege[0].Meter, angebot[0].Meter);
            Pruefe.Gleich(0, Hofstellenvergabe.Angebot(AlteKarte("greybox")).Count);
        }),

        ("Hofstellen: Der Name der Spielerwerkstatt folgt der Hofstelle – auf seiner Vorgabe wie in der Karte, sonst nach dem Namen (mit oder ohne Präposition), ohne Namen nach der nächsten Karrenstraße", () =>
        {
            var karte = Stadtkarte.AusJson(OhneHofstellen(File.ReadAllText(Datenpfad.Karte("greybox"))), Katalog.Value);
            Pruefe.Gleich("{art} am Weinmarkt", Hofstellenvergabe.WerkstattnameSpieler(karte, karte.Hofstellen[0]));
            Pruefe.Gleich("{art} am Heumarkt", Hofstellenvergabe.WerkstattnameSpieler(karte, new Hofstelle { Kennung = "Z", Name = "Heumarkt", Werkstatt = "Weinmarkt", Wohnhaus = "Wohnhaus1" }));
            Pruefe.Gleich("{art} Unter Fettenhennen", Hofstellenvergabe.WerkstattnameSpieler(karte, new Hofstelle { Kennung = "Z", Name = "Unter Fettenhennen", Werkstatt = "X", Wohnhaus = "Y" }));
            Pruefe.Gleich("{art} am Malzbüchel", Hofstellenvergabe.WerkstattnameSpieler(karte, new Hofstelle { Kennung = "Z", Name = "am Malzbüchel", Werkstatt = "X", Wohnhaus = "Y" }));
            var ohne = Hofstellenvergabe.WerkstattnameSpieler(karte, new Hofstelle { Kennung = "Z", Werkstatt = "Giesserei_Eisenbart", Wohnhaus = "Wohnhaus2" });
            Pruefe.Wahr(ohne.StartsWith("{art} a") && ohne != "{art} Z", $"nach der Straße: {ohne}");
            Pruefe.Gleich("{art} Z", Hofstellenvergabe.WerkstattnameSpieler(karte, new Hofstelle { Kennung = "Z", Werkstatt = "fehlt", Wohnhaus = "fehlt" }));
        }),
    };
}
