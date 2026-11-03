using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Landhandel (Datenversion 70, „Hafen ruht“, Nutzer: Option A): Solange der Schiffshandel ruht und die Karte einen Hafen hat, heben die
/// Faktoren <c>schiffshandel.landhandel.angebot</c> und <c>.nachfrage</c> Grundversorgung und Nachfrage einzelner Waren – sie gleichen aus,
/// was ohne Rückfracht und Ausfuhr der Schiffe fehlt. Mit laufendem Schiffshandel und ohne Hafen wirken sie nicht.
/// </summary>
static class LandhandelTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));

    /// <summary>Die Daten mit einem bekannten Landhandel (Eisen: Angebot × 1,5; Werkzeug: Nachfrage × 1,2) statt dem aus der Datei.</summary>
    static string MitLandhandel(string json)
    {
        var n = JsonNode.Parse(json)!.AsObject();
        n["schiffshandel"]!["landhandel"] = new JsonObject
        {
            ["angebot"] = new JsonObject { ["Eisen"] = 1.5 },
            ["nachfrage"] = new JsonObject { ["Werkzeug"] = 1.2 },
        };
        return n.ToJsonString();
    }

    /// <summary>Dieselben Daten mit laufendem Schiffshandel (<c>schiffshandel.aktiv: true</c>).</summary>
    static string An(string json)
    {
        var n = JsonNode.Parse(json)!.AsObject();
        n["schiffshandel"]!["aktiv"] = true;
        return n.ToJsonString();
    }

    /// <summary>Eine Stadt mit Karte um 9 Uhr im ersten Jahr (ohne Häuser), Einwohner gezählt.</summary>
    static Stadtwirtschaft Stadt(string json, string? karte = "greybox")
    {
        var w = new Stadtwirtschaft(Wirtschaftsdaten.AusJson(json), Partieeinstellungen.Standard.StartStatut, 1400);
        if (karte is not null) w.KarteSetzen(Stadtkarte.AusDatei(Datenpfad.Karte(karte)));
        w.EinwohnerZaehlen();
        return w;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Landhandel: Ruht der Schiffshandel in einer Stadt mit Hafen, heben die Faktoren Grundversorgung und Nachfrage – andere Waren bleiben gleich", () =>
        {
            var json = MitLandhandel(Json.Value);
            var ruht = Stadt(json);
            var an = Stadt(An(json));
            Pruefe.Wahr(ruht.Schiffahrt.LandhandelWirkt && !an.Schiffahrt.LandhandelWirkt, "wirkt nur, wenn er ruht");
            Pruefe.Gleich(1.5, ruht.Markt["Eisen"].LandhandelAngebot);
            Pruefe.Gleich(1.2, ruht.Markt["Werkzeug"].LandhandelNachfrage);
            Pruefe.Ungefaehr(an.Markt["Eisen"].Grundversorgung * 1.5, ruht.Markt["Eisen"].Grundversorgung, 1e-9);
            Pruefe.Ungefaehr(an.Markt["Werkzeug"].Nachfrage * 1.2, ruht.Markt["Werkzeug"].Nachfrage, 1e-9);
            Pruefe.Wahr(ruht.Markt["Eisen"].Preis < an.Markt["Eisen"].Preis && ruht.Markt["Werkzeug"].Preis > an.Markt["Werkzeug"].Preis, "Preise");
            foreach (var ware in new[] { "Holz", "Kerze", "Silberring" })
                Pruefe.Gleich(an.Markt[ware].Preis, ruht.Markt[ware].Preis, ware);
        }),

        ("Landhandel: Mit laufendem Schiffshandel und in einer Stadt ohne Hafen (ohne Karte) bleiben alle Faktoren 1", () =>
        {
            var json = MitLandhandel(Json.Value);
            foreach (var (name, w) in new[] { ("an", Stadt(An(json))), ("ohne Karte", Stadt(json, karte: null)) })
            {
                Pruefe.Wahr(!w.Schiffahrt.LandhandelWirkt, name);
                foreach (var s in w.Markt.Staende)
                    Pruefe.Wahr(s.LandhandelAngebot == 1 && s.LandhandelNachfrage == 1, $"{name}: {s.Ware}");
            }
            // Wird die Karte erst nach dem ersten Zählen gesetzt, findet die Stadt ihren Hafen trotzdem
            var spaet = Stadt(json, karte: null);
            spaet.KarteSetzen(Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));
            spaet.EinwohnerZaehlen();
            Pruefe.Wahr(spaet.Schiffahrt.HatHafen && spaet.Markt["Eisen"].LandhandelAngebot == 1.5, "Karte später");
        }),

        ("Landhandel: In wirtschaft.json stehen Faktoren über 1; unbekannte Waren und Faktoren außerhalb (0, 3] lehnt die Prüfung ab", () =>
        {
            var l = Wirtschaftsdaten.AusJson(Json.Value).Schiffshandel!.Landhandel;
            Pruefe.Wahr(l.Angebot.Count + l.Nachfrage.Count > 0, "Landhandel in den Daten");
            Pruefe.Wahr(l.Angebot.Values.Concat(l.Nachfrage.Values).All(x => x > 1), "nur Faktoren über 1");
            var n = JsonNode.Parse(Json.Value)!.AsObject();
            n["schiffshandel"]!["landhandel"] = new JsonObject
            {
                ["angebot"] = new JsonObject { ["Mondstein"] = 1.1 },
                ["nachfrage"] = new JsonObject { ["Werkzeug"] = 4 },
            };
            var fehler = "";
            try { Wirtschaftsdaten.AusJson(n.ToJsonString()); }
            catch (WirtschaftsdatenFehler ex) { fehler = string.Join("\n", ex.Fehler); }
            Pruefe.Wahr(fehler.Contains("schiffshandel.landhandel.angebot") && fehler.Contains("Mondstein"), fehler);
            Pruefe.Wahr(fehler.Contains("schiffshandel.landhandel.nachfrage.Werkzeug"), fehler);
        }),
    };
}
