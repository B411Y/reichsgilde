using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Reichsgilde.Core.Speichern;

/// <summary>
/// Alte Spielstände rechnen wie auf main (niedriger Befund der Prüfung von phase5/gehtempo, 1.11.2026): Ein Spielstand von vor Datenversion 65
/// (Greybox, Herbst 1406, Datenversion 55) bringt seine Daten mit und muss Jahr für Jahr genau so weiterlaufen wie mit dem Kern auf main – die
/// Fahrzeit nach Weglänge, die gemeinsame Strecke und die Marktplätze gelten nur mit <c>logistik.weglaenge</c>. Verglichen wird der Spielstand
/// ohne die Abschnitte „Daten“ und „Karte“ (die schreibt der neue Kern mit seinen neuen, leeren Feldern; die Simulation selbst steht in
/// „Wirtschaft“). Die Sollwerte sind auf main 880e3387 gerechnet (Probe mit demselben Ablauf, 1.11.2026). Ebenso ohne „Meldungen“ (Benachrichtigungen
/// Stufe 2, 6.11.2026): der Verlauf der Meldungen ist neu, die Simulation liest ihn nie – „Wirtschaft“ bleibt bitgleich. Seit dem 11.11.2026
/// (Bart der Zwerginnen) auch ohne das Erbgut der Personen (<c>Spielstandfelder.Erbgut</c>, die Simulation liest es nie): Die Sollwerte dafür
/// sind auf main 770c374a mit demselben Ablauf gerechnet, wo die Sollwerte von main 880e3387 mit Erbgut noch galten.
/// </summary>
static class AlterSpielstandSollwertTests
{
    static readonly string[] Sollwerte =
    {
        // Ohne die Grundstücksverwaltung (OhneGrundstuecksverwaltung, 15.11.2026, „nur noch Bauzonen“): gerechnet auf main f7890512 mit demselben
        // Ablauf – dort mit der Grundstücksverwaltung noch gleich den Sollwerten ohne Erbgut von main 770c374a (12b73856…, b08c5d4a…, 9c9ad7e3…),
        // die mit Erbgut den Sollwerten von main 880e3387 glichen (32479e9e…, 258c8b4f…, b579efb5…).
        "39cf5c5aa4d11a2c6742582c1e4c8f7029e626ed73a100e4a1c63386df28025b",
        "63f84daee1b36a73eb7a6126d3b75c4a9aa7a9c5c32bada912355337442f9e49",
        "bc8300243e1dcbd0806890f67b2cc6b829b503077e4c212161b765cf9bc1e71b",
    };

    /// <summary>
    /// Seit „nur noch Bauzonen“ (15.11.2026) werden beim Laden die Werkstätten auf Werkstattplätzen der Karte zu Grundstücken (mit der Kennung des
    /// Platzes), und Bauplätze fallen weg. Damit der Vergleich mit dem Kern davor bleibt, zählen hier weder diese beiden Arten von Grundstücken
    /// noch die Nummern (die verschieben sich durch die neuen Grundstücke) – alles Übrige an den Grundstücken wird weiter verglichen: Wohnhäuser,
    /// Leerstand, Baustellen und neu gebaute Werkstätten in ihrer Reihenfolge.
    /// </summary>
    internal static void OhneGrundstuecksverwaltung(JsonObject n)
    {
        if (n["Wirtschaft"]?["Bauen"] is not JsonObject bauen || bauen["Grundstuecke"] is not JsonArray alt) return;
        bauen.Remove("NaechsteNummer");
        var neu = new JsonArray();
        foreach (var g in alt.ToList())
        {
            var o = g!.AsObject();
            var art = (string?)o["Art"];
            alt.Remove(g);
            if (art == "Bauplatz" || art == "Werkstatt" && o["Kennung"] is not null && o["Arbeit"] is null) continue;
            o.Remove("Nummer");
            neu.Add(o);
        }
        bauen["Grundstuecke"] = neu;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Alter Spielstand (Greybox, Herbst 1406, Datenversion 55): drei Jahre weiter bitgleich mit dem Kern auf main 880e3387 (SHA-256 ohne Daten, Karte, Meldungen, Erbgut und Grundstücksverwaltung; Sollwerte von main f7890512)", () =>
        {
            var datei = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
                "tests", "Reichsgilde.Core.Tests", "Spielstaende", "format2-greybox-herbst1406.json.gz");
            using var gz = new GZipStream(File.OpenRead(datei), CompressionMode.Decompress);
            var (sim, _) = Speicherung.Laden(new StreamReader(gz).ReadToEnd());
            Pruefe.Gleich(55, sim.Wirtschaft!.Daten.Version);
            for (var jahr = 0; jahr < Sollwerte.Length; jahr++)
            {
                var j = sim.Zeit.Jahr;
                while (sim.Zeit.Jahr == j) sim.Tick();
                var n = JsonNode.Parse(Speicherung.Speichern(sim, null, new DateTime(2026, 1, 1)))!.AsObject();
                foreach (var schluessel in n.Select(p => p.Key).ToList())
                    if (schluessel.Equals("daten", StringComparison.OrdinalIgnoreCase) || schluessel.Equals("karte", StringComparison.OrdinalIgnoreCase)
                        || schluessel.Equals("meldungen", StringComparison.OrdinalIgnoreCase))
                        n.Remove(schluessel);
                Spielstandfelder.OhneLeere(n, Spielstandfelder.Hafenziel); // neue Felder (Datenversion 73), solange sie nichts tragen
                Spielstandfelder.Ohne(n, Spielstandfelder.NurLaden); // nur fürs Laden (Datenversion 75), durchgehend ohne Wirkung
                Spielstandfelder.Ohne(n, Spielstandfelder.Erbgut); // Allele für den 3D-Kopf: die Simulation liest sie nie (Bart der Zwerginnen, 11.11.2026)
                Spielstandfelder.Ohne(n, Spielstandfelder.Hauschronik); // die Hauschronik (Datenversion 83): die Simulation liest sie nie
                OhneGrundstuecksverwaltung(n); // nur noch Bauzonen (15.11.2026): Werkstätten auf Werkstattplätzen sind jetzt auch Grundstücke
                Spielstandfelder.MitAltenKarrennamen(n); // Karrenarten ohne Ochsen (Datenversion 98): nur die Namen und das Format
                var ist = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(n.ToJsonString()))).ToLowerInvariant();
                if (Environment.GetEnvironmentVariable("SOLLWERTE_ZEIGEN") is not null) Console.WriteLine($"Jahr {jahr + 1}: {ist}");
                else Pruefe.Gleich(Sollwerte[jahr], ist, $"Jahr {jahr + 1} nach dem Laden");
            }
        }),
    };
}
