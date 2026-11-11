using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Höchstens eine Kirche je Glaube auch beim Start (Nutzer, 11.11.2026). Seit der Startaufstellung (PR 1c) folgt das aus der allgemeinen Regel
/// „jeder Beruf höchstens einmal je Partie“ (<see cref="Startaufstellung"/>): Es gibt am Start höchstens einen Prediger. Die frühere eigene
/// Startregel aus PR 1a (KI-Haus nimmt statt einer zweiten Kirche einen festen anderen Beruf) ist darin aufgegangen.
/// </summary>
static class KirchenstartTests
{
    static readonly string Json = File.ReadAllText(Datenpfad.Wirtschaft());
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json));

    static Partieeinstellungen Einstellungen(int startwert) =>
        Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert };

    /// <summary>Die Greybox-Karte, deren erste KI-Häuser als Prediger beginnen (Werkstatt → Kirche); auf Wunsch mit anderem Volk.</summary>
    static Stadtkarte Karte(int prediger, Volk? volk = null)
    {
        var pfad = Datenpfad.Karte("greybox");
        var json = JsonNode.Parse(File.ReadAllText(pfad))!;
        var haeuser = json["start"]!["haeuser"]!.AsArray();
        for (var i = 0; i < prediger; i++)
        {
            var betriebe = haeuser[i]!["betriebe"]!.AsArray();
            betriebe[0] = new JsonObject { ["platz"] = (string)betriebe[0]!["platz"]!, ["art"] = "Kirche" };
            if (volk is { } v) haeuser[i]!["volk"] = v.ToString();
        }
        return Stadtkarte.AusJson(json.ToJsonString(), Gebaeudekatalog.AusDatei(Stadtkarte.KatalogZu(pfad)));
    }

    static Stadtwirtschaft Partie(Stadtkarte karte, NeuePartie neu, int startwert = 1400) =>
        Partieaufbau.Erstellen(Einstellungen(startwert), Daten.Value, neu, karte: karte).Simulation.Wirtschaft!;

    static int Kirchen(Stadtwirtschaft w) => w.Betriebe.Count(b => w.Kirchenwesen.IstKirche(b));

    static readonly NeuePartie ZwergPrediger = new(Volk: Volk.Zwerg, Beruf: "Prediger", Rivalen: 3);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Kirchenstart: Spieler Prediger – über 100 Startwerte nie ein KI-Prediger, eine Kirche; Gegenprobe Vorgabe (Karte wie geschrieben) vier Kirchen", () =>
        {
            var karte = Karte(3, Volk.Zwerg); // alle drei KI-Häuser wären laut Karte zwergische Prediger
            for (var s = 1; s <= 100; s++)
            {
                var w = Partie(karte, ZwergPrediger, s);
                Pruefe.Gleich(1, Kirchen(w), $"Startwert {s}");
                Pruefe.Wahr(w.Konten.Skip(1).All(k => !k.Beherrscht("Prediger")), $"Startwert {s}: kein KI-Haus Prediger");
                foreach (var g in new[] { Reichsgilde.Core.Glaube.Konfession.Katholisch, Reichsgilde.Core.Glaube.Konfession.Evangelisch })
                    Pruefe.Wahr(w.Kirchenwesen.KirchenDesGlaubens(g) <= 1, $"Startwert {s}: {g}");
            }
            // Gegenprobe: ohne die Aufstellung (Vorgabe) beginnen Spieler und KI-Häuser als zwergische Prediger – vier Kirchen
            var v = Partie(karte, ZwergPrediger with { Aufstellung = Aufstellungsart.Vorgabe });
            Pruefe.Gleich(4, Kirchen(v), "ohne Regel");
            Pruefe.Wahr(v.Kirchenwesen.KirchenDesGlaubens(v.Glaube.GlaubeVon(v.Konten[0])!.Value) >= 2, "ohne Regel zwei Kirchen eines Glaubens");
        }),

        ("Kirchenstart: Spieler kein Prediger – höchstens ein KI-Prediger, nie mehr als eine Kirche; in manchen Partien gibt es einen", () =>
        {
            var mitKirche = 0;
            for (var s = 1; s <= 100; s++)
            {
                var w = Partie(Karte(0), new NeuePartie(Beruf: "Schmied", Rivalen: 3), s);
                Pruefe.Wahr(Kirchen(w) <= 1, $"Startwert {s}: {Kirchen(w)} Kirchen");
                if (Kirchen(w) == 1) mitKirche++;
            }
            Pruefe.Wahr(mitKirche > 0 && mitKirche < 100, $"Partien mit Kirche: {mitKirche}");
        }),
    };
}
