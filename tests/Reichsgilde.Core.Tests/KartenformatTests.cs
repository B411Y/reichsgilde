using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Kartenformat 2 und Gebäudekatalog (PR #163): Die Karte sagt, wo etwas steht, der Katalog, was es ist. Dazu die Belegung am Start
/// aus der Karte (mit dem Ersatz ohne Karte) und alte Spielstände (Karte in Format 1, Werkstätten ohne Platz).
/// </summary>
static class KartenformatTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));

    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static string GreyboxPfad => Datenpfad.Karte("greybox");
    static Gebaeudekatalog Katalog() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json"));
    static Stadtkarte Greybox() => Stadtkarte.AusDatei(GreyboxPfad);

    /// <summary>Die Greybox-Karte als JSON zum Abwandeln.</summary>
    static JsonNode GreyboxJson() => JsonNode.Parse(File.ReadAllText(GreyboxPfad))!;

    static Partieeinstellungen Endlos => Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos };

    /// <summary>Ein Katalog aus Bausteinen als JSON-Text (für Fehlerfälle).</summary>
    static string KatalogMit(params string[] bausteine) => $$"""{ "bausteine": [ {{string.Join(", ", bausteine)}} ] }""";

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Katalog: wird gelesen – Plätze, Umland mit Betriebsart, Werkstatt je Beruf, Wohnhaus je Stufe, Natur mit Varianten", () =>
        {
            var k = Katalog();
            Pruefe.Wahr(k.Bausteine.Count > 40, "Katalog fast leer");
            Pruefe.Gleich(Bausteinart.Werkstattplatz, k.Finde("werkstattplatz")!.Art);
            Pruefe.Wahr(k.Finde("werkstattplatz")!.Modell is null, "Ein Werkstattplatz hat kein festes Modell");
            Pruefe.Gleich("grube", k.Umland(Betriebsart.Mine)!.Kennung);
            Pruefe.Gleich("gebaeude/tischler", k.Werkstatt("Tischler")!.Modell);
            Pruefe.Gleich("gebaeude/stadthof", k.Wohnhaus(4)!.Modell);
            Pruefe.Gleich("gebaeude/einwohner_ratsherrenhaus", k.Einwohnerhaus("Ratsherrenhaus")!.Modell);
            Pruefe.Gleich(3, k.Finde("tanne")!.Varianten);
            Pruefe.Gleich(-2.85, k.Finde("dom")!.Versatz![1]);
            Pruefe.Wahr(k.Finde("schloss") is null, "unbekannte Kennung");
            // Jeder Beruf der Daten hat ein Werkstattmodell, jede Umland-Betriebsart einen Baustein.
            foreach (var (beruf, b) in Daten.Value.Berufe)
            {
                Pruefe.Wahr(k.Werkstatt(beruf) is not null, $"Werkstattmodell für {beruf}");
                if (b.Umland is { } umland) Pruefe.Wahr(k.Umland(umland) is not null, $"Umlandbaustein für {umland}");
            }
        }),

        ("Katalog: Modelle und Katalog passen zusammen – jedes Modell des Katalogs gibt es, jedes Modell der Stadt steht im Katalog", () =>
        {
            // Modelle liegen in Unterordnern (gebaeude/, umland/ …); der Katalog nennt sie mit Unterordner: „gebaeude/schmied“.
            var ordner = Path.Combine(Path.GetDirectoryName(Datenordner)!, "assets", "modelle");
            var dateien = Directory.GetFiles(ordner, "*.glb", SearchOption.AllDirectories)
                .Select(d => Path.GetRelativePath(ordner, d).Replace('\\', '/')[..^".glb".Length]).ToHashSet();
            var k = Katalog();
            foreach (var b in k.Bausteine.Where(b => b.Modell is not null))
                Pruefe.Wahr(dateien.Contains(b.Modell!), $"Baustein „{b.Kennung}“: Modell {b.Modell}.glb fehlt");
            // Nicht in der Stadt aufgestellt: Figuren, Karren, Vögel, Innenräume; die Marktstände gehören zum Markt.
            var imKatalog = k.Bausteine.Select(b => b.Modell).OfType<string>().ToHashSet();
            var fehlen = dateien.Where(d => !imKatalog.Contains(d) && !d.StartsWith("figuren/") && !d.StartsWith("fahrzeuge/") && !d.StartsWith("tiere/")
                                            && !d.StartsWith("innen/") && d != "gebaeude/marktstaende").ToList();
            Pruefe.Wahr(fehlen.Count == 0, "Modelle ohne Baustein im Katalog: " + string.Join(", ", fehlen));
        }),

        ("Katalog: Fehler werden gemeldet – unbekannte Art, Größe fehlt, Kennung doppelt, Betriebsart unbekannt, Natur ohne Knoten", () =>
        {
            Pruefe.WirftMitMeldung(() => Gebaeudekatalog.AusJson(KatalogMit("""{ "kennung": "x", "art": "palast", "groesse": [1, 1] }""")), "Die Art „palast“ gibt es nicht");
            Pruefe.WirftMitMeldung(() => Gebaeudekatalog.AusJson(KatalogMit("""{ "kennung": "x", "art": "werkstattplatz" }""")), "groesse fehlt");
            Pruefe.WirftMitMeldung(() => Gebaeudekatalog.AusJson(KatalogMit("""{ "kennung": "x", "art": "werkstattplatz", "groesse": [1, 1] }""",
                """{ "kennung": "x", "art": "werkstattplatz", "groesse": [1, 1] }""")), "„x“ ist doppelt");
            Pruefe.WirftMitMeldung(() => Gebaeudekatalog.AusJson(KatalogMit("""{ "kennung": "x", "art": "umlandbetrieb", "modell": "x", "betriebsart": "Goldmine", "groesse": [1, 1] }""")),
                "betriebsart „Goldmine“");
            Pruefe.WirftMitMeldung(() => Gebaeudekatalog.AusJson(KatalogMit("""{ "kennung": "x", "art": "natur", "modell": "natur_baeume" }""")), "knoten fehlt");
            Pruefe.WirftMitMeldung(() => Gebaeudekatalog.AusJson("""{ "format": 2 }"""), "neuer als dieser Leser");
        }),

        ("Greybox: Format 2 – jedes Gebäude im Katalog aufgelöst, vier Werkstattplätze, Umland mit Betriebsnamen, Mauer mit zwei Toren, Natur", () =>
        {
            var k = Greybox();
            Pruefe.Gleich(2, k.Format);
            Pruefe.Wahr(k.Gebaeude.All(g => g.Baustein is not null && g.Art != ""), "jedes Gebäude aufgelöst");
            Pruefe.Gleich(4, k.GebaeudeDerArt(Bausteinart.Werkstattplatz).Count());
            Pruefe.Gleich(12, k.GebaeudeDerArt(Bausteinart.Umlandbetrieb).Count());
            Pruefe.Wahr(k.GebaeudeDerArt(Bausteinart.Umlandbetrieb).All(g => g.Betrieb == g.Name), "Umlandbetriebe tragen ihren Betriebsnamen");
            Pruefe.Gleich(3, k.GebaeudeDerArt(Bausteinart.Umlandstelle).Count());
            Pruefe.Gleich("Feld, Wald, Weide", string.Join(", ", k.GebaeudeDerArt(Bausteinart.Umlandstelle).Select(g => g.Name).Order()), "Namen wie im Kern");
            Pruefe.Gleich(3, k.GebaeudeDerArt(Bausteinart.Berg).Count()); // je Grube ein Felsberg
            Pruefe.Gleich("Osttor, Westtor", string.Join(", ", k.Mauern.SelectMany(m => m.Tore).Select(t => t.Name).Order()));
            Pruefe.Wahr(k.Natur.Flaechen.Count > 0, "Natur fehlt");
            Pruefe.Gleich(string.Join(", ", Enumerable.Range(1, 8).Select(i => $"Wohnhaus{i}")), string.Join(", ", k.Wohnhausplaetze.Select(g => g.Kennung)));

            // Schräg gedreht belegt ein Gebäude das umschließende Rechteck: Grube Eisenbart (16 × 14, −60°).
            var grube = k.GebaeudeMit("Grube_Eisenbart")!;
            Pruefe.Gleich(-60.0, grube.Drehung);
            var g = grube.Grundriss!.Value;
            Pruefe.Wahr(Math.Abs(g.Breite - (16 * 0.5 + 14 * Math.Sqrt(3) / 2)) < 1e-9 && g.Drehung == 0, $"Rechteck um die Grube: {g}");
        }),

        ("Greybox: jeder Betrieb hat seinen Platz – Startwerkstatt je Beruf am Weinmarkt, Rivalen, jede Liegenschaft aus wirtschaft.json", () =>
        {
            var k = Greybox();
            Pruefe.Gleich("Weinmarkt", k.PlatzVon("Gießerei am Weinmarkt")!.Kennung);
            Pruefe.Gleich("Weinmarkt", k.PlatzVon("Wechselstube am Weinmarkt")!.Kennung);
            Pruefe.Gleich(Bausteinart.Werkstattplatz, k.PlatzVon("Gießerei Eisenbart")!.Art);
            Pruefe.Wahr(k.PlatzVon("Gießerei an der Ostgasse") is null && k.PlatzVon(" am Weinmarkt") is null, "kein Platz für fremde Namen");
            foreach (var l in Daten.Value.Liegenschaften)
                Pruefe.Wahr(k.PlatzVon(l.Name) is { Art: Bausteinart.Umlandbetrieb } p && p.Baustein!.BetriebsartWert == l.Art, $"Platz für {l.Name}");
            foreach (var b in k.Startbelegung.Haeuser.SelectMany(h => h.Betriebe))
                Pruefe.Wahr(k.GebaeudeMit(b.Platz)?.Betrieb is not null, $"Startbetrieb auf {b.Platz}");
        }),

        ("Start: Die Greybox führt dieselben drei Häuser wie der Ersatz ohne Karte – gleiche Konten, Betriebe, Völker und Wappen", () =>
        {
            var (mit, _) = Partieaufbau.Erstellen(Endlos, Daten.Value, new NeuePartie(), karte: Greybox());
            var (ohne, _) = Partieaufbau.Erstellen(Endlos, Daten.Value, new NeuePartie());
            var (w1, w2) = (mit.Wirtschaft!, ohne.Wirtschaft!);
            Pruefe.Gleich(string.Join(", ", w2.Konten.Select(k => $"{k.Inhaber} {k.Wappen}")), string.Join(", ", w1.Konten.Select(k => $"{k.Inhaber} {k.Wappen}")));
            Pruefe.Gleich(string.Join(", ", w2.Betriebe.Select(b => $"{b.Name}/{b.Art}/{b.Konto.Inhaber}/{b.Produkt}/{b.Arbeiter.Count}")),
                          string.Join(", ", w1.Betriebe.Select(b => $"{b.Name}/{b.Art}/{b.Konto.Inhaber}/{b.Produkt}/{b.Arbeiter.Count}")));
            Pruefe.Gleich(string.Join(", ", w2.Konten.Select(k => w2.Familien.Von(k)?.Oberhaupt.Volk)), string.Join(", ", w1.Konten.Select(k => w1.Familien.Von(k)?.Oberhaupt.Volk)));
            Pruefe.Gleich(string.Join(", ", Startbelegung.Ersatz.Rivalennamen), string.Join(", ", Greybox().Startbelegung.Rivalennamen));
            Pruefe.Gleich("#b33329", Greybox().Startbelegung.Farbe("Haus Eisenbart"));
            Pruefe.Gleich("#336bb8", Greybox().Startbelegung.Farbe("Haus Hammerschmid", istSpieler: true));
            // Jeder Betrieb hat seinen Platz in der Karte.
            Pruefe.Wahr(w1.Betriebe.All(b => w1.Bauen.PlatzVon(b.Name)?.Gebaeude is not null), "jeder Startbetrieb steht auf einem Platz der Karte");
        }),

        ("Start: Die Karte bestimmt die KI-Häuser – Name, Volk, Wappen, Betriebe, Wohnhaus, Startplatz des Spielers je Beruf, Zahl der Rivalen", () =>
        {
            var json = GreyboxJson();
            var haeuser = json["start"]!["haeuser"]!.AsArray();
            var erstes = haeuser[0]!;
            erstes["name"] = "Haus Steinhauer";
            erstes["volk"] = "Mensch";
            erstes["wappen"] = new JsonObject { ["teilung"] = "Geteilt", ["farbe1"] = "Rot", ["farbe2"] = "Silber", ["figur"] = "Turm" };
            erstes["wohnhaus"] = "Wohnhaus5";
            erstes["betriebe"]![0]!["produkt"] = null;
            haeuser.RemoveAt(2); // ohne Kieselbach
            json["gebaeude"]!.AsArray().Add(new JsonObject
            {
                ["kennung"] = "Probeplatz", ["katalog"] = "werkstattplatz", ["name"] = "Probeplatz", ["punkt"] = new JsonArray(-110, -5), ["drehung"] = 0,
            });
            json["start"]!["spieler"]!["jeBeruf"] = new JsonObject { ["Steinmetz"] = new JsonObject { ["platz"] = "Probeplatz", ["name"] = "{art} am Probeplatz" } };
            var karte = Stadtkarte.AusJson(json.ToJsonString(), Katalog());

            var (sim, spieler) = Partieaufbau.Erstellen(Endlos with { Schwierigkeit = Schwierigkeit.Schwer }, Daten.Value, new NeuePartie(Beruf: "Steinmetz", Rivalen: 2), karte: karte);
            var w = sim.Wirtschaft!;
            Pruefe.Gleich("Haus Hammerschmid, Haus Steinhauer, Haus Eichwald", string.Join(", ", w.Konten.Select(k => k.Inhaber)));
            var steinhauer = w.Konten[1];
            Pruefe.Gleich(new Wappen(Teilung.Geteilt, Tinktur.Rot, Tinktur.Silber, Figur.Turm), steinhauer.Wappen);
            Pruefe.Gleich(Volk.Mensch, w.Familien.Von(steinhauer)!.Oberhaupt.Volk);
            Pruefe.Gleich("Wohnhaus5", w.Bauen.Wohnhaus(steinhauer)!.Kennung);
            Pruefe.Gleich("Wohnhaus3", w.Bauen.Wohnhaus(w.Konten[2])!.Kennung);
            // Die Betriebe heißen wie ihr Platz in der Karte (die Gießerei bleibt „Gießerei Eisenbart“, so steht es am Platz).
            Pruefe.Gleich("Gießerei Eisenbart, Grube Eisenbart", string.Join(", ", w.Betriebe.Where(b => b.Konto == steinhauer).Select(b => b.Name)));
            Pruefe.Gleich("Maurerei am Probeplatz", w.Betriebe.Single(b => b.Konto == spieler).Name);
            Pruefe.Gleich("Probeplatz", w.Bauen.PlatzVon("Maurerei am Probeplatz")!.Gebaeude!.Kennung);

            Pruefe.WirftMitMeldung(() => Partieaufbau.Erstellen(Endlos, Daten.Value, new NeuePartie(Rivalen: 3), karte: karte), "ein bis zwei KI-Häuser");
            Pruefe.WirftMitMeldung(() => Partieaufbau.Erstellen(Endlos, Daten.Value, new NeuePartie("Steinhauer"), karte: karte), "Name eines Rivalen");
            Pruefe.WirftMitMeldung(() => Partieaufbau.Erstellen(Endlos, Daten.Value,
                new NeuePartie(Wappen: new Wappen(Teilung.Geteilt, Tinktur.Rot, Tinktur.Silber, Figur.Turm)), karte: karte), "KI-Haus");
            // „Haus Eisenbart“ gibt es in dieser Karte nicht mehr – der Name ist frei.
            Pruefe.Gleich("Haus Eisenbart", Partieaufbau.Erstellen(Endlos, Daten.Value, new NeuePartie("Eisenbart", Rivalen: 1), karte: karte).Spieler.Inhaber);
        }),

        ("Start: Fehler in der Karte werden gemeldet – Platz unbekannt, falsche Betriebsart, Wappen doppelt, kein Wohnhausplatz, Platz doppelt", () =>
        {
            void Fehler(Action<JsonNode> aendern, string meldung)
            {
                var json = GreyboxJson();
                aendern(json);
                Pruefe.WirftMitMeldung(() => Stadtkarte.AusJson(json.ToJsonString(), Katalog()), meldung);
            }
            Fehler(j => j["start"]!["haeuser"]![0]!["betriebe"]![0]!["platz"] = "Nirgendwo", "„Nirgendwo“ ist kein Werkstattplatz");
            Fehler(j => j["start"]!["haeuser"]![0]!["betriebe"]![1]!["art"] = "Steinbruch", "nicht Steinbruch");
            Fehler(j => j["start"]!["haeuser"]![1]!["wappen"] = j["start"]!["haeuser"]![0]!["wappen"]!.DeepClone(), "dasselbe Wappen");
            Fehler(j => j["start"]!["haeuser"]![1]!["wohnhaus"] = "Markt", "„Markt“ ist kein Wohnhausplatz");
            Fehler(j => j["start"]!["haeuser"]![1]!["betriebe"]![0]!["platz"] = "Giesserei_Eisenbart", "mehrfach vergeben");
            Fehler(j => j["start"]!["spieler"]!["werkstatt"]!["platz"] = "Grube_Eisenbart", "kein Werkstattplatz der Karte");
            Fehler(j => j["natur"]!["flaechen"]![0]!["gruppen"]![0]!["teile"]![0]!["arten"]!["palme"] = 1, "Natur-Baustein „palme“");
            Fehler(j => j["gebaeude"]!.AsArray().Add(new JsonObject { ["kennung"] = "X", ["katalog"] = "grube", ["punkt"] = new JsonArray(0, 0), ["betrieb"] = "Grube Eisenbart" }),
                "„Grube Eisenbart“ steht an mehreren Plätzen");
        }),

        ("Spielstand: Karte mit Katalog und Start liegt im Spielstand und wird beim Laden wieder aufgelöst", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Endlos, Daten.Value, new NeuePartie(), karte: Greybox());
            var json = Speicherung.Speichern(sim, spieler);
            var (geladen, _) = Speicherung.Laden(json);
            var k = geladen.Wirtschaft!.Karte!;
            Pruefe.Gleich(2, k.Format);
            Pruefe.Wahr(k.Katalog is not null && k.Gebaeude.All(g => g.Baustein is not null), "Katalog im Spielstand, Gebäude aufgelöst");
            Pruefe.Gleich("Haus Eisenbart", k.Startbelegung.Haeuser[0].Name);
            Pruefe.Gleich(Speicherung.Speichern(sim, spieler, DateTime.UnixEpoch), Speicherung.Speichern(geladen, geladen.Wirtschaft.Konten[0], DateTime.UnixEpoch));
        }),

        ("Alter Spielstand: Karte in Format 1 wird durch die Kartendatei derselben Stadt ersetzt; Grundstücke bleiben", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Endlos, Daten.Value, new NeuePartie(), karte: Greybox());
            var stand = JsonNode.Parse(Speicherung.Speichern(sim, spieler))!;
            // So sah die Karte im Spielstand bis Format 1 aus (gekürzt): feste Bauten statt Gebäude, kein Katalog.
            stand["Karte"] = JsonNode.Parse("""
                { "Format": 1, "Kennung": "greybox", "Stadt": "Greybox-Stadt", "Raster": 2, "Wege": [], "Bauzonen": [],
                  "Feste": [ { "Kennung": "Wohnhaus1", "Art": "wohnhaus", "Name": "Wohnhaus 1", "Punkt": [0, -44.3], "Groesse": [17, 8] } ] }
                """);
            var alt = stand.ToJsonString();
            var (geladen, _) = Speicherung.Laden(alt, Greybox());
            var w = geladen.Wirtschaft!;
            Pruefe.Gleich(2, w.Karte!.Format);
            Pruefe.Gleich(11, w.Karte.Bauzonen.Count);
            Pruefe.Gleich(string.Join(" | ", sim.Wirtschaft!.Bauen.Grundstuecke.Select(g => $"{g.Kennung} {g.Grundriss} {g.Besitzer?.Inhaber}")),
                          string.Join(" | ", w.Bauen.Grundstuecke.Select(g => $"{g.Kennung} {g.Grundriss} {g.Besitzer?.Inhaber}")));
            var koeln = Stadtkarte.AusDatei(Datenpfad.Karte("koeln"));
            Pruefe.WirftMitMeldung(() => Speicherung.Laden(alt, koeln), "gehört zur Karte „greybox“");
        }),

        ("Alter Spielstand: Eine Werkstatt ohne Platz in der Karte bekommt beim Laden ein Grundstück in einer Bauzone – bei jedem Laden dasselbe", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Endlos, Daten.Value, new NeuePartie(Startvermoegen: 50_000), karte: Greybox());
            var w = sim.Wirtschaft!;
            // Bis PR #162 gab es „Schänke an der Ostgasse“ als Werkstatt zum Kauf – die Karte hat keinen Platz mehr dafür.
            w.BetriebEroeffnen("Schänke an der Ostgasse", Betriebsart.Schaenke, spieler);
            Pruefe.Wahr(w.Bauen.PlatzVon("Schänke an der Ostgasse") is null, "vor dem Laden ohne Platz");

            var (geladen, _) = Speicherung.Laden(Speicherung.Speichern(sim, spieler), Greybox());
            var w2 = geladen.Wirtschaft!;
            var platz = w2.Bauen.PlatzVon("Schänke an der Ostgasse")!;
            var g = platz.Grundstueck!;
            Pruefe.Gleich(Grundstuecksart.Werkstatt, g.Art);
            Pruefe.Gleich(Bauzustand.Werkstatt, g.Zustand);
            Pruefe.Gleich(Betriebsart.Schaenke, g.Betriebsart);
            Pruefe.Wahr(w2.Karte!.Bauzonen.Any(z => z.Enthaelt(g.Grundriss.X, g.Grundriss.Z)), "in einer Bauzone");
            Pruefe.Wahr(w2.Bauen.PlatzVerboten(new WerkstattBauen(Betriebsart.Giesserei, g.Grundriss.X, g.Grundriss.Z, g.Grundriss.Drehung))?.Contains("Schänke an der Ostgasse") == true,
                "Der Platz ist belegt");
            Pruefe.Wahr(w2.Betriebe.Any(b => b.Name == "Schänke an der Ostgasse" && b.Konto.Inhaber == spieler.Inhaber), "Der Betrieb arbeitet weiter");

            // Noch einmal speichern und laden: dasselbe Grundstück, kein zweites.
            var (wieder, _) = Speicherung.Laden(Speicherung.Speichern(geladen, w2.Konten[0]), Greybox());
            var alle = wieder.Wirtschaft!.Bauen.Grundstuecke.Where(x => x.Betrieb == "Schänke an der Ostgasse").ToList();
            Pruefe.Gleich(1, alle.Count);
            Pruefe.Gleich(g.Grundriss, alle[0].Grundriss);
        }),

        ("Alter Spielstand: Freie Werkstätten zum Kauf ohne Platz in der Karte (alte Daten) sind nach dem Laden nicht mehr zu haben", () =>
        {
            var daten = JsonNode.Parse(Datenpfad.OhneAktionspunkte())!;
            daten["liegenschaften"]!.AsArray().Add(new JsonObject { ["name"] = "Gießerei an der Südgasse", ["art"] = "Giesserei", ["kaufpreis"] = 3000 });
            var alteDaten = Wirtschaftsdaten.AusJson(daten.ToJsonString());
            var (sim, spieler) = Partieaufbau.Erstellen(Endlos, alteDaten, new NeuePartie(), karte: Greybox());
            Pruefe.Wahr(sim.Wirtschaft!.FreieLiegenschaften.Any(l => l.Name == "Gießerei an der Südgasse"), "vor dem Laden zu haben");

            var (geladen, ich) = Speicherung.Laden(Speicherung.Speichern(sim, spieler), Greybox());
            var w = geladen.Wirtschaft!;
            Pruefe.Wahr(w.FreieLiegenschaften.All(l => l.Name != "Gießerei an der Südgasse"), "nach dem Laden nicht mehr zu haben");
            Pruefe.Gleich("Eine Liegenschaft „Gießerei an der Südgasse“ gibt es nicht.", w.LiegenschaftErwerbVerboten(ich!, "Gießerei an der Südgasse"));
            Pruefe.Wahr(w.FreieLiegenschaften.Any(l => l.Name == "Grube am Silberberg"), "Umland bleibt zu haben");
        }),
    };
}
