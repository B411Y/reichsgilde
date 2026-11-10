using System.Text.Json;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Bauzonen nur für Werkstätten (<see cref="Bauzone.Nur"/> = „werkstatt“, 1.11.2026): Die Werkstatt-Bauplätze nahe den Kölner Märkten waren so groß
/// wie ein Stadthof; ohne die Regel nähme die KI sie für ihre ersten Stadthöfe (Befund der Prüfung von phase5/gehtempo). Seit den Hofstellen
/// (9.11.2026) hat Köln keine mehr – das Feld gilt weiter für ältere Karten. Geprüft an der Greybox mit einer Stadtzone „nur werkstatt“.
/// </summary>
static class BauzoneNurTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));
    static readonly Lazy<Gebaeudekatalog> Katalog =
        new(() => Gebaeudekatalog.AusDatei(Path.Combine(Path.GetDirectoryName(Datenpfad.Wirtschaft())!, "katalog.json")));

    /// <summary>Die Greybox, deren größte Stadtzone ein Werkstatt-Bauplatz ist (wie eine Karte von vor den Hofstellen mit „nur“), mit allen Häusern ohne Los.</summary>
    static (Stadtwirtschaft W, string Zone) MitWerkstattBauplatz()
    {
        var knoten = JsonNode.Parse(File.ReadAllText(Datenpfad.Karte("greybox")))!.AsObject();
        static double Flaeche(JsonObject z)
        {
            var p = z["flaeche"]!.AsArray().Select(q => (X: (double)q![0]!, Z: (double)q[1]!)).ToList();
            return Math.Abs(p.Select((a, i) => a.X * p[(i + 1) % p.Count].Z - p[(i + 1) % p.Count].X * a.Z).Sum()) / 2;
        }
        var zone = knoten["bauzonen"]!.AsArray().Select(z => z!.AsObject()).Where(z => (string)z["art"]! != "umland")
            .OrderByDescending(Flaeche).ThenBy(z => (string)z["kennung"]!, StringComparer.Ordinal).First();
        zone["nur"] = Bauzone.NurWerkstatt;
        var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, Daten.Value,
            new NeuePartie(Startplaetze: Startplatzvergabe.Vorgabe), karte: Stadtkarte.AusJson(knoten.ToJsonString(), Katalog.Value));
        return (sim.Wirtschaft!, (string)zone["kennung"]!);
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
        ("Bauzone „nur werkstatt“: Köln hat seit den Hofstellen keine Werkstatt-Bauplätze mehr; auf einem (ältere Karte) steht eine Werkstatt, ein Stadthof nicht", () =>
        {
            Pruefe.Wahr(Stadtkarte.AusDatei(Datenpfad.Karte("koeln")).Bauzonen.All(z => z.Nur is null), "Köln ohne „nur“");
            var (w, zone) = MitWerkstattBauplatz();
            var k = Bauherr(w);
            var bauplatz = w.Karte!.Bauzonen.Single(z => z.Kennung == zone);
            var ort = w.Bauen.Bauorte(k, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0))
                .First(b => w.Bauen.Grundriss(b).Felder(2).All(f => bauplatz.Enthaelt(f.X, f.Z)));
            Pruefe.Gleich(null, w.Bauen.PlatzVerboten(ort));
            // ein Stadthof (16 × 14) genau dort nicht
            Pruefe.Gleich("Dieser Bauplatz ist für eine Werkstatt.", w.Bauen.PlatzVerboten(new WohnhausBauen(4, ort.X, ort.Z, ort.Drehung)));
        }),

        ("Bauzone „nur werkstatt“: Bauorte für Wohnhäuser meiden den Werkstatt-Bauplatz, Bauorte für Werkstätten finden ihn", () =>
        {
            var (w, zone) = MitWerkstattBauplatz();
            var k = Bauherr(w);
            var plaetze = w.Karte!.Bauzonen.Where(z => z.NurWerkstaetten).ToList();
            Pruefe.Gleich(zone, plaetze.Single().Kennung);
            bool AufPlatz(BauBefehl b) => plaetze.Any(z => w.Bauen.Grundriss(b).Felder(2).Any(f => z.Enthaelt(f.X, f.Z)));
            foreach (var stufe in new[] { 1, 3, 4 })
            {
                var orte = w.Bauen.Bauorte(k, new WohnhausBauen(stufe, 0, 0, 0));
                Pruefe.Wahr(orte.Count > 0, $"Wohnhaus Stufe {stufe}: der Bauherr darf bauen");
                Pruefe.Wahr(!orte.Any(AufPlatz), $"Wohnhaus Stufe {stufe} auf einem Werkstatt-Bauplatz");
            }
            var werkstatt = w.Bauen.Bauorte(k, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0)).Where(AufPlatz).ToList();
            Pruefe.Gleich(1, plaetze.Count(z => werkstatt.Any(b => w.Bauen.Grundriss(b).Felder(2).All(f => z.Enthaelt(f.X, f.Z)))));
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
