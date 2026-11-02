using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

/// <summary>
/// Kartenwerkstatt: Das Feld „nur“ einer Bauzone (Werkstatt-Bauplatz, „werkstatt“) lässt sich setzen und wieder löschen – mit Rückgängig,
/// und es landet so in der Datei, wie das Spiel es liest (<see cref="Bauzone.NurWerkstaetten"/>).
/// </summary>
static class KartenwerkstattBauzoneNurTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenentwurf Greybox() => Kartenentwurf.AusJson(File.ReadAllText(Datenpfad.Karte("greybox")), Katalog.Value);

    /// <summary>Das Feld „nur“ der Zone mit dieser Kennung in der Datei (null: fehlt).</summary>
    static string? NurInDatei(Kartenentwurf e, string kennung) =>
        JsonNode.Parse(e.ZuJson())!["bauzonen"]!.AsArray().First(z => (string?)z!["kennung"] == kennung)!["nur"]?.GetValue<string>();

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Kartenwerkstatt: „nur Werkstätten“ einer Bauzone setzen, löschen, rückgängig und wiederholen", () =>
        {
            var e = Greybox();
            var zone = e.Bauzonen[0];
            Pruefe.Gleich(null, zone.Nur, "die Greybox hat keine Werkstatt-Bauplätze");
            Pruefe.Gleich(null, NurInDatei(e, zone.Kennung), "ohne „nur“ steht das Feld nicht in der Datei");

            e.BauzoneNurSetzen(zone, Bauzone.NurWerkstatt);
            Pruefe.Gleich(Bauzone.NurWerkstatt, zone.Nur);
            Pruefe.Gleich(Bauzone.NurWerkstatt, NurInDatei(e, zone.Kennung));
            var karte = Stadtkarte.AusJson(e.ZuJson(), Katalog.Value);
            Pruefe.Wahr(karte.Bauzonen.Single(z => z.Kennung == zone.Kennung).NurWerkstaetten, "das Spiel liest die Zone als Werkstatt-Bauplatz");

            Pruefe.Wahr(e.Geschichte.Rueckgaengig(), "Rückgängig");
            Pruefe.Gleich(null, zone.Nur, "nach Rückgängig wieder frei");
            Pruefe.Wahr(e.Geschichte.Wiederholen(), "Wiederholen");
            Pruefe.Gleich(Bauzone.NurWerkstatt, zone.Nur);

            e.BauzoneNurSetzen(zone, null);
            Pruefe.Gleich(null, zone.Nur);
            Pruefe.Gleich(null, NurInDatei(e, zone.Kennung), "gelöscht: das Feld fällt aus der Datei");
            Pruefe.Wahr(e.Geschichte.Rueckgaengig(), "Löschen rückgängig");
            Pruefe.Gleich(Bauzone.NurWerkstatt, zone.Nur);
        }),

        ("Kartenwerkstatt: „nur“ einer Bauzone kennt nur „werkstatt“; gleicher Wert schreibt keinen Schritt", () =>
        {
            var e = Greybox();
            var zone = e.Bauzonen[0];
            Pruefe.WirftMitMeldung(() => e.BauzoneNurSetzen(zone, "wohnhaus"), "werkstatt");
            Pruefe.Gleich(null, zone.Nur);
            Pruefe.Wahr(!e.Geschichte.KannRueckgaengig, "die abgelehnte Änderung steht nicht in der Geschichte");
            e.BauzoneNurSetzen(zone, null);
            Pruefe.Wahr(!e.Geschichte.KannRueckgaengig, "ohne Änderung kein Schritt");
        }),
    };
}
