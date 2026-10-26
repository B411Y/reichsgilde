using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Tests;

/// <summary>
/// Freie Startplätze (Entscheidung des Nutzers vom 26.10.2026, PR #209): Werkstatt- und Wohnhausplätze stehen in jeder Drehung und in jedem
/// Abstand zum Weg. Es entfallen die Regeln „hof“, „eingang“, „werkstattnorm“, „gasse“ und „rechtwinklig“; der Kern stellt ein Haus auf
/// einem schrägen Wohnhausplatz in dessen Drehung (<see cref="Bauwesen.Flaeche(Grundstueck)"/>).
/// </summary>
static class FreieStartplaetzeTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenentwurf Oeffnen(string kennung) => Kartenentwurf.AusJson(File.ReadAllText(Datenpfad.Karte(kennung)), Katalog.Value);

    static string Liste(IEnumerable<Befund> befunde) => string.Join("\n  ", befunde.Select(b => b.ToString()));

    /// <summary>Die Regeln, die es seit PR #209 nicht mehr gibt.</summary>
    static readonly string[] Entfallen = { "hof", "eingang", "werkstattnorm", "gasse", "rechtwinklig" };

    static List<Befund> Fehler(IEnumerable<Befund> befunde) => befunde.Where(b => b.Schwere == Schwere.Fehler).ToList();

    /// <summary>Abstand eines Punktes zur nächsten Mittellinie eines Weges (Stützpunkte; double.MaxValue ohne Wege).</summary>
    static double NaechsterWeg(Kartenentwurf e, Kartenort p) =>
        e.Wege.Where(w => w.Punkte.Count >= 2).Select(w => Kartengeometrie.AbstandLinie(p, w.Punkte)).DefaultIfEmpty(double.MaxValue).Min();

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Freie Startplätze: ein schräger Wohnhausplatz ergibt keinen Fehler (Probestadt, Stelle und ganze Karte)", () =>
        {
            var e = Oeffnen("probestadt");
            var stelle = Kartenpruefung.PruefeStelle(e, "wohnhausplatz", 0, -35, 205, kennung: "Wohnhausplatz2");
            Pruefe.Wahr(Fehler(stelle).Count == 0, "schräg an seiner Stelle\n  " + Liste(stelle));
            e.Versetzen("Wohnhausplatz2", 0, -35, 205);
            Pruefe.Gleich(205.0, e.Gebaeude.Single(g => g.Kennung == "Wohnhausplatz2").Drehung);
            Pruefe.Wahr(!e.HatFehler, "Probestadt mit schrägem Wohnhausplatz des Spielers\n  " + Liste(Fehler(e.Befunde)));
        }),

        ("Freie Startplätze: eine Werkstatt 20 m vom nächsten Weg, schräg, als Startplatz des Spielers ergibt keinen Fehler", () =>
        {
            var e = Oeffnen("probestadt");
            var w = e.Setzen("werkstattplatz", 172, -95, 75); // zwischen Mühlenteich und Feldweg, kein Weg führt hin
            Pruefe.Wahr(NaechsterWeg(e, w.Hof!.Value) >= 20, $"Hof {w.Hof} liegt {NaechsterWeg(e, w.Hof!.Value):0.0} m vom Weg");
            e.SpielerStartplatzSetzen(w.Kennung);
            var f = Fehler(e.Befunde);
            Pruefe.Wahr(f.Count == 0, "Werkstatt fern vom Weg als Startplatz\n  " + Liste(f));
        }),

        ("Freie Startplätze: eine Greybox-Werkstatt mit Drehung 90 ergibt keinen Fehler", () =>
        {
            var e = Oeffnen("greybox");
            // Die Gießerei Eisenbart an ihrer Stelle gedreht: keine der entfallenen Regeln (der Abstand zur Bauzone K1 bleibt eine Regel)
            var stelle = Kartenpruefung.PruefeStelle(e, "werkstattplatz", 22, 13, 90, kennung: "Giesserei_Eisenbart");
            Pruefe.Wahr(!stelle.Any(b => Entfallen.Contains(b.Regel)), "keine der entfallenen Regeln\n  " + Liste(stelle));
            // Eine neue Werkstatt auf freiem Feld vor der Stadt, Tür nach Osten, ohne Gasse am Hof
            var w = e.Setzen("werkstattplatz", -150, 120, 90);
            var f = Fehler(e.Befunde);
            Pruefe.Wahr(f.Count == 0, "Greybox-Werkstatt mit Drehung 90\n  " + Liste(f));
            Pruefe.Gleich(90.0, w.Drehung);
        }),

        ("Freie Startplätze: keine Karte meldet eine der entfallenen Regeln", () =>
        {
            foreach (var kennung in new[] { "greybox", "koeln", "probestadt", "messstadt" })
            {
                if (!File.Exists(Datenpfad.Karte(kennung))) continue;
                var befunde = Oeffnen(kennung).Befunde.Where(b => Entfallen.Contains(b.Regel)).ToList();
                Pruefe.Wahr(befunde.Count == 0, $"{kennung}:\n  " + Liste(befunde));
            }
        }),

        ("Freie Startplätze: Auf einem schrägen Wohnhausplatz steht das Haus in der Drehung des Platzes (Bauwesen.Flaeche)", () =>
        {
            var json = JsonNode.Parse(File.ReadAllText(Datenpfad.Karte("greybox")))!;
            var platz = json["gebaeude"]!.AsArray().Single(g => (string)g!["kennung"]! == "Wohnhaus1")!;
            platz["drehung"] = 30;
            var karte = Stadtkarte.AusJson(json.ToJsonString(), Gebaeudekatalog.AusDatei(Stadtkarte.KatalogZu(Datenpfad.Karte("greybox"))));
            Pruefe.Wahr(karte.Pruefen().Count == 0, "Die Karte liest sich: " + string.Join(" | ", karte.Pruefen()));

            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie(), karte: karte);
            var bauen = sim.Wirtschaft!.Bauen;
            var haus = bauen.Wohnhaus(spieler)!;
            Pruefe.Gleich("Wohnhaus1", haus.Kennung);
            // Der Grundriss bleibt das umschließende Rechteck (für Überlappung und Bauen) …
            Pruefe.Gleich(0, haus.Grundriss.Drehung);
            Pruefe.Wahr(haus.Grundriss.Breite > 17 && haus.Grundriss.Tiefe > 8, $"umschließendes Rechteck: {haus.Grundriss}");
            // … das Haus steht wie der Platz: 17 × 8 m, 30°, der Eingang 4 m vor der Mitte in Richtung (sin 30°, cos 30°).
            var f = bauen.Flaeche(haus);
            Pruefe.Gleich((0.0, -44.3, 17.0, 8.0, 30.0), (f.X, f.Z, f.Breite, f.Tiefe, f.DrehungGrad));
            Pruefe.Wahr(Math.Abs(f.Eingang.X - 4 * Math.Sin(Math.PI / 6)) < 1e-9, $"Eingang x {f.Eingang.X}");
            Pruefe.Wahr(Math.Abs(f.Eingang.Z - (-44.3 + 4 * Math.Cos(Math.PI / 6))) < 1e-9, $"Eingang z {f.Eingang.Z}");

            // Ein rechtwinkliger Platz (Wohnhaus2, 180°): genau sein Grundriss
            var anderes = bauen.Wohnhaus(sim.Wirtschaft.Konten[1])!;
            Pruefe.Gleich("Wohnhaus2", anderes.Kennung);
            Pruefe.Gleich(Hausflaeche.Von(anderes.Grundriss), bauen.Flaeche(anderes));
            Pruefe.Gleich(180.0, bauen.Flaeche(anderes).DrehungGrad);
        }),
    };
}
