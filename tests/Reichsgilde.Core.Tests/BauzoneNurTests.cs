using System.Text.Json;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Bauzonen nur für Werkstätten (<see cref="Bauzone.Nur"/> = „werkstatt“, 1.11.2026): Die Werkstatt-Bauplätze nahe den Kölner Märkten sind so groß
/// wie ein Stadthof; ohne die Regel nähme die KI die beiden am Alter Markt für ihre ersten Stadthöfe (Befund der Prüfung von phase5/gehtempo).
/// </summary>
static class BauzoneNurTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));
    static readonly Lazy<Gebaeudekatalog> Katalog =
        new(() => Gebaeudekatalog.AusDatei(Path.Combine(Path.GetDirectoryName(Datenpfad.Wirtschaft())!, "katalog.json")));

    static Stadtwirtschaft Koeln()
    {
        var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, Daten.Value,
            new NeuePartie(Rivalen: 7), karte: Stadtkarte.AusDatei(Datenpfad.Karte("koeln")));
        return sim.Wirtschaft!;
    }

    static Konto Bauherr(Stadtwirtschaft w)
    {
        var k = w.KontoEroeffnen("Haus Probe", startvermoegen: 1_000_000);
        k.Stand = 5;
        k.WohnhausStufe = 1;
        k.BerufLernen("Schmied");
        w.Wohnen.HauswertAktualisieren(k);
        return k;
    }

    /// <summary>Die Meldung, mit der das Laden scheitert – oder null.</summary>
    static string? Fehlerfrei(Action laden)
    {
        try { laden(); return null; }
        catch (Exception e) { return e.Message + " " + string.Join(" ", (e as KartenFehler)?.Fehler ?? Array.Empty<string>()); }
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Bauzone „nur werkstatt“: Köln hat sieben Werkstatt-Bauplätze; dort steht eine Werkstatt, ein Stadthof nicht", () =>
        {
            var w = Koeln();
            var plaetze = w.Karte!.Bauzonen.Where(z => z.NurWerkstaetten).Select(z => z.Kennung).ToList();
            Pruefe.Gleich("WB_Neumarkt1, WB_Neumarkt2, WB_Neumarkt3, WB_Neumarkt4, WB_Neumarkt5, WB_AlterMarkt1, WB_AlterMarkt2", string.Join(", ", plaetze));
            Pruefe.Wahr(w.Karte.Bauzonen.Where(z => !z.Kennung.StartsWith("WB_")).All(z => z.Nur is null), "nur die Bauplätze tragen „nur“");
            // WB_AlterMarkt2 (−20 … −6 | −102 … −86): genau eine Werkstatt bzw. ein Stadthof (16 × 14), nach Osten gedreht
            Pruefe.Gleich(null, w.Bauen.PlatzVerboten(new WerkstattBauen(Betriebsart.Giesserei, -13, -94, 1)));
            Pruefe.Gleich("Dieser Bauplatz ist für eine Werkstatt.", w.Bauen.PlatzVerboten(new WohnhausBauen(4, -13, -94, 1)));
            Pruefe.Gleich("Dieser Bauplatz ist für eine Werkstatt.", w.Bauen.PlatzVerboten(new WohnhausBauen(1, -11, -90, 1)));
        }),

        ("Bauzone „nur werkstatt“: Bauorte für Wohnhäuser meiden die Werkstatt-Bauplätze, Bauorte für Werkstätten finden alle sieben", () =>
        {
            var w = Koeln();
            var k = Bauherr(w);
            var plaetze = w.Karte!.Bauzonen.Where(z => z.NurWerkstaetten).ToList();
            bool AufPlatz(BauBefehl b) => plaetze.Any(z => w.Bauen.Grundriss(b).Felder(2).Any(f => z.Enthaelt(f.X, f.Z)));
            foreach (var stufe in new[] { 1, 3, 4 })
            {
                var orte = w.Bauen.Bauorte(k, new WohnhausBauen(stufe, 0, 0, 0));
                Pruefe.Wahr(orte.Count > 0, $"Wohnhaus Stufe {stufe}: der Bauherr darf bauen");
                Pruefe.Wahr(!orte.Any(AufPlatz), $"Wohnhaus Stufe {stufe} auf einem Werkstatt-Bauplatz");
            }
            var werkstatt = w.Bauen.Bauorte(k, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0)).Where(AufPlatz).ToList();
            Pruefe.Gleich(7, plaetze.Count(z => werkstatt.Any(b => w.Bauen.Grundriss(b).Felder(2).All(f => z.Enthaelt(f.X, f.Z)))));
        }),

        ("Bauzone „nur“: ohne Feld wie bisher – Greybox ohne „nur“, Datei Byte für Byte, kein „nur“ in der Stadtkarte; unbekannter Wert ist ein Fehler", () =>
        {
            var text = File.ReadAllText(Datenpfad.Karte("greybox"));
            var karte = Stadtkarte.AusDatei(Datenpfad.Karte("greybox"));
            Pruefe.Wahr(karte.Bauzonen.Count > 0 && karte.Bauzonen.All(z => z.Nur is null && !z.NurWerkstaetten), "Greybox ohne „nur“");
            Pruefe.Wahr(!JsonSerializer.Serialize(karte.Bauzonen[0]).Contains("ur\""), "kein Feld „nur“ ohne Wert");
            var e = Kartenentwurf.AusJson(text, Katalog.Value);
            Pruefe.Gleich(text, e.ZuJson());
            Pruefe.Wahr(e.Befunde.All(b => b.Regel != "zone-nur"), "Greybox: kein Befund zone-nur");
            // Köln liest und schreibt „nur“ unverändert
            var koeln = File.ReadAllText(Datenpfad.Karte("koeln"));
            Pruefe.Gleich(koeln, Kartenentwurf.AusJson(koeln, Katalog.Value).ZuJson());
            // ein unbekannter Wert
            var knoten = JsonNode.Parse(text)!.AsObject();
            knoten["bauzonen"]![0]!.AsObject()["nur"] = "lager";
            var falsch = Kartenentwurf.AusJson(knoten.ToJsonString(), Katalog.Value);
            Pruefe.Wahr(falsch.Befunde.Any(b => b.Regel == "zone-nur" && b.Schwere == Schwere.Fehler), string.Join(" | ", falsch.Befunde));
            // auch das Spiel lehnt sie beim Laden ab (Groß- und Kleinschreibung zählt)
            knoten["bauzonen"]![0]!.AsObject()["nur"] = "Werkstatt";
            Pruefe.Wahr(Fehlerfrei(() => Stadtkarte.AusJson(knoten.ToJsonString(), Katalog.Value)) is { } meldung && meldung.Contains("nur ist"), "Stadtkarte meldet „nur“");
        }),
    };
}
