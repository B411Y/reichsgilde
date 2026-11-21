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
    static string GreyboxPfad => Altkarte.Pfad("greybox");
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
            Pruefe.Gleich(5, k.Finde("tanne")!.Varianten);
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
            // Nicht in der Stadt aufgestellt: Figuren, Karren, Vögel, Innenräume, die Flussschiffe, die Nachen und der Hafensteg (ihn setzt die Stadtansicht an jeden Hafen); die Marktstände gehören zum Markt.
            // Ohne Katalog: die Felsstücke und der Felswand-Satz (setzt der Fels-Pinsel, Felspinsel F3/F5, Felsen R4) und die Kulissenberge hinter dem Kartenrand.
            var imKatalog = k.Bausteine.Select(b => b.Modell).OfType<string>().ToHashSet();
            var fehlen = dateien.Where(d => !imKatalog.Contains(d) && !d.StartsWith("figuren/") && !d.StartsWith("karren/") && !d.StartsWith("tiere/")
                                            && !d.StartsWith("innen/") && d != "gebaeude/marktstaende" && d != "gebaeude/lichter" && d != "wasser/wasser_schiff" && d != "wasser/wasser_niederlaender" && d != "wasser/wasser_nachen" && d != "wasser/wasser_hafensteg"
                                            && d != "natur/natur_felsen" && d != "natur/natur_felswand" && !d.StartsWith("kulisse/")).ToList();
            // Ohne Katalog: Requisiten (requisiten/, z. B. der Eimer der Löschkette) setzt das Spiel bei Ereignissen, nicht die Karte.
            fehlen.RemoveAll(d => d.StartsWith("requisiten/"));
            // Ohne Katalog: der 3D-Handschuh des Mauszeigers (zeiger/, Oberfläche, nicht Teil der Karte).
            fehlen.RemoveAll(d => d.StartsWith("zeiger/"));
            // Ohne Katalog: Modelle der Oberfläche (oberflaeche/, z. B. die Schriftrolle der Meldungen); die Oberfläche setzt sie, nicht die Karte.
            fehlen.RemoveAll(d => d.StartsWith("oberflaeche/"));
            // Ohne Katalog: die Baukästen der Restflächen-Füllung (props/fuellung_*, props_fuellung_kit); sie stellt die Stadtansicht nach restflaechen.json, nicht die Karte.
            fehlen.RemoveAll(d => d.StartsWith("props/fuellung_") || d == "props/props_fuellung_kit");
            // Ohne Katalog: die Probe zum Neubau der Gebäude (gebaeude/neubau/); sie steht nur mit Startoption (--neubauprobe, Stadtansicht.Neubau.cs), bis der Nutzer den Tausch freigibt.
            fehlen.RemoveAll(d => d.StartsWith("gebaeude/neubau/"));
            // Ebenso die Module der Häuserzeilen (gebaeude/zeilen/, modulares Wohnhaussystem): keine Bausteine der Karte, das Spiel setzt sie
            // selbst zu Zeilen zusammen (Stadtansicht.Zeilen.cs), bisher nur mit Startoption --zeilenprobe.
            fehlen.RemoveAll(d => d.StartsWith("gebaeude/zeilen/"));
            // Ohne Katalog: der Entwurf der Klappbrücke (bruecke/); noch nicht im Spiel, Einbau nach Abnahme durch den Nutzer.
            fehlen.RemoveAll(d => d.StartsWith("bruecke/"));
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

        ("Greybox: Format 2 – jedes Gebäude im Katalog aufgelöst, keine Werkstatt- und Wohnhausplätze, Umland mit Betriebsnamen, Mauer mit drei Toren, Hafen, Natur", () =>
        {
            var k = Greybox();
            Pruefe.Gleich(2, k.Format);
            Pruefe.Wahr(k.Gebaeude.All(g => g.Baustein is not null && g.Art != ""), "jedes Gebäude aufgelöst");
            Pruefe.Gleich(0, k.GebaeudeDerArt(Bausteinart.Werkstattplatz).Count() + k.Wohnhausplaetze.Count); // bis 16.11.2026 vier und acht
            Pruefe.Gleich(12, k.GebaeudeDerArt(Bausteinart.Umlandbetrieb).Count());
            Pruefe.Wahr(k.GebaeudeDerArt(Bausteinart.Umlandbetrieb).All(g => g.Betrieb == g.Name), "Umlandbetriebe tragen ihren Betriebsnamen");
            Pruefe.Gleich(4, k.GebaeudeDerArt(Bausteinart.Umlandstelle).Count()); // seit PR #199 mit dem Hafen
            Pruefe.Gleich("Feld, Hafen am Grenzfluss, Wald, Weide", string.Join(", ", k.GebaeudeDerArt(Bausteinart.Umlandstelle).Select(g => g.Name).Order()), "Namen wie im Kern");
            Pruefe.Gleich(3, k.GebaeudeDerArt(Bausteinart.Berg).Count()); // je Grube ein Felsberg
            Pruefe.Gleich("Osttor, Südtor, Westtor", string.Join(", ", k.Mauern.SelectMany(m => m.Tore).Select(t => t.Name).Order()));
            Pruefe.Wahr(k.Natur.Flaechen.Count > 0, "Natur fehlt");

            // Schräg gedreht belegt ein Gebäude das umschließende Rechteck: Grube Eisenbart (16 × 14, −60°).
            var grube = k.GebaeudeMit("Grube_Eisenbart")!;
            Pruefe.Gleich(-60.0, grube.Drehung);
            var g = grube.Grundriss!.Value;
            Pruefe.Wahr(Math.Abs(g.Breite - (16 * 0.5 + 14 * Math.Sqrt(3) / 2)) < 1e-9 && g.Drehung == 0, $"Rechteck um die Grube: {g}");
        }),

        ("Greybox: Plätze der Karte haben nur noch die Umlandbetriebe – die der Rivalen und jede Liegenschaft aus wirtschaft.json; die Werkstätten in der Stadt nicht", () =>
        {
            var k = Greybox();
            Pruefe.Wahr(k.PlatzVon("Gießerei am Weinmarkt") is null && k.PlatzVon("Gießerei Eisenbart") is null && k.PlatzVon("Gießerei an der Ostgasse") is null,
                "kein Platz der Karte für Werkstätten in der Stadt (bis 16.11.2026 Werkstattplätze)");
            Pruefe.Gleich(Bausteinart.Umlandbetrieb, k.PlatzVon("Grube Eisenbart")!.Art);
            foreach (var l in Daten.Value.Liegenschaften)
                Pruefe.Wahr(k.PlatzVon(l.Name) is { Art: Bausteinart.Umlandbetrieb } p && p.Baustein!.BetriebsartWert == l.Art, $"Platz für {l.Name}");
            foreach (var b in k.Startbelegung.Haeuser.SelectMany(h => h.Betriebe))
                Pruefe.Wahr(b.Platz == "" ? Daten.Value.Art(b.Art).InDerStadt : k.GebaeudeMit(b.Platz) is { Art: Bausteinart.Umlandbetrieb, Betrieb: not null },
                    $"Startbetrieb {b.Art} auf „{b.Platz}“: Werkstatt ohne Platz oder Umlandbetrieb mit Namen");
        }),

        ("Start: Die Greybox führt dieselben drei Häuser wie der Ersatz ohne Karte – gleiche Konten, Betriebe, Völker und Wappen", () =>
        {
            // Mit Karte heißt die Werkstatt des Spielers nach ihrer Straße (gelost), ohne Karte „am Weinmarkt“ – sonst dieselben Betriebe
            var (mit, _) = Partieaufbau.Erstellen(Endlos, Daten.Value, new NeuePartie(Aufstellung: Aufstellungsart.Vorgabe), karte: Greybox());
            var (ohne, _) = Partieaufbau.Erstellen(Endlos, Daten.Value, new NeuePartie(Aufstellung: Aufstellungsart.Vorgabe));
            var (w1, w2) = (mit.Wirtschaft!, ohne.Wirtschaft!);
            Pruefe.Gleich(string.Join(", ", w2.Konten.Select(k => $"{k.Inhaber} {k.Wappen}")), string.Join(", ", w1.Konten.Select(k => $"{k.Inhaber} {k.Wappen}")));
            static string Betriebe(Stadtwirtschaft w) => string.Join(", ", w.Betriebe.Select(b =>
                $"{(b.Konto == w.Konten[0] ? "(Werkstatt des Spielers)" : b.Name)}/{b.Art}/{b.Konto.Inhaber}/{b.Produkt}/{b.Arbeiter.Count}"));
            Pruefe.Gleich(Betriebe(w2), Betriebe(w1));
            Pruefe.Gleich("Gießerei am Weinmarkt", w2.Betriebe[0].Name);
            Pruefe.Wahr(w1.Betriebe[0].Name.StartsWith("Gießerei a", StringComparison.Ordinal) && w1.Betriebe[0].Name != "Gießerei am Weinmarkt", w1.Betriebe[0].Name);
            Pruefe.Gleich(string.Join(", ", w2.Konten.Select(k => w2.Familien.Von(k)?.Oberhaupt.Volk)), string.Join(", ", w1.Konten.Select(k => w1.Familien.Von(k)?.Oberhaupt.Volk)));
            Pruefe.Gleich(string.Join(", ", Startbelegung.Ersatz.Rivalennamen), string.Join(", ", Greybox().Startbelegung.Rivalennamen));
            Pruefe.Wahr(Greybox().Startbelegung.Farbe("Haus Eisenbart") is null, "die Karte nennt keine Farbe für ein KI-Haus (sie kommt aus dem Wappen)");
            Pruefe.Wahr(Greybox().Startbelegung.Farbe("Haus Hammerschmid", istSpieler: true) is null, "die Karte nennt keine Farbe für den Spieler (sie kommt aus dem Wappen)");
            // Jeder Betrieb hat seinen Platz: die Werkstätten in der Stadt ein Grundstück, die Umlandbetriebe ihr Gebäude der Karte.
            Pruefe.Wahr(w1.Betriebe.All(b => w1.Bauen.PlatzVon(b.Name) is { } p && (b.InDerStadt ? p.Grundstueck is not null : p.Gebaeude is not null)),
                "jeder Startbetrieb steht auf seinem Platz");
        }),

        ("Start: Die Karte bestimmt die KI-Häuser – Name, Volk, Wappen, Betriebe, Zahl der Rivalen; wo sie wohnen und arbeiten, lost die Partie", () =>
        {
            var json = GreyboxJson();
            var haeuser = json["start"]!["haeuser"]!.AsArray();
            var erstes = haeuser[0]!;
            erstes["name"] = "Haus Steinhauer";
            erstes["volk"] = "Mensch";
            erstes["wappen"] = new JsonObject { ["teilung"] = "Geteilt", ["farbe1"] = "Rot", ["farbe2"] = "Silber", ["figur"] = "Turm" };
            erstes["betriebe"]![0]!["produkt"] = null;
            haeuser.RemoveAt(2); // ohne Kieselbach
            var karte = Stadtkarte.AusJson(json.ToJsonString(), Katalog());

            // Aufstellung nach der Karte: jedes Haus mit Name, Volk, Wappen und Betrieben der Startbelegung
            var (sim, spieler) = Partieaufbau.Erstellen(Endlos with { Schwierigkeit = Schwierigkeit.Schwer }, Daten.Value,
                new NeuePartie(Beruf: "Steinmetz", Rivalen: 2, Aufstellung: Aufstellungsart.Vorgabe), karte: karte);
            var w = sim.Wirtschaft!;
            Pruefe.Gleich("Haus Hammerschmid, Haus Steinhauer, Haus Eichwald", string.Join(", ", w.Konten.Select(k => k.Inhaber)));
            var steinhauer = w.Konten[1];
            Pruefe.Gleich(new Wappen(Teilung.Geteilt, Tinktur.Rot, Tinktur.Silber, Figur.Turm), steinhauer.Wappen);
            Pruefe.Gleich(Volk.Mensch, w.Familien.Von(steinhauer)!.Oberhaupt.Volk);
            Pruefe.Wahr(w.Konten.All(k => w.Bauen.Wohnhaus(k) is { Kennung: null }), "jedes Haus wohnt auf einem gelosten Grundstück");
            // Die Werkstatt in der Stadt heißt nach dem Haus, der Umlandbetrieb wie sein Platz in der Karte („Grube Eisenbart“ steht dort).
            Pruefe.Gleich("Gießerei Steinhauer, Grube Eisenbart", string.Join(", ", w.Betriebe.Where(b => b.Konto == steinhauer).Select(b => b.Name)));
            Pruefe.Wahr(w.Betriebe.Single(b => b.Konto == spieler).Name.StartsWith("Maurerei a", StringComparison.Ordinal), w.Betriebe.Single(b => b.Konto == spieler).Name);
            Pruefe.Wahr(w.Bauen.PlatzVon(w.Betriebe.Single(b => b.Konto == spieler).Name) is { Grundstueck.Kennung: null }, "die Werkstatt des Spielers steht auf einem Grundstück");

            Pruefe.WirftMitMeldung(() => Partieaufbau.Erstellen(Endlos, Daten.Value, new NeuePartie(Rivalen: 3, Aufstellung: Aufstellungsart.Vorgabe), karte: karte), "null bis zwei KI-Häuser");
            Pruefe.WirftMitMeldung(() => Partieaufbau.Erstellen(Endlos, Daten.Value, new NeuePartie("Steinhauer", Aufstellung: Aufstellungsart.Vorgabe), karte: karte), "Name eines Rivalen");
            Pruefe.WirftMitMeldung(() => Partieaufbau.Erstellen(Endlos, Daten.Value,
                new NeuePartie(Wappen: new Wappen(Teilung.Geteilt, Tinktur.Rot, Tinktur.Silber, Figur.Turm), Aufstellung: Aufstellungsart.Vorgabe), karte: karte), "KI-Haus");
            // „Haus Eisenbart“ gibt es in dieser Karte nicht mehr – der Name ist frei.
            Pruefe.Gleich("Haus Eisenbart", Partieaufbau.Erstellen(Endlos, Daten.Value, new NeuePartie("Eisenbart", Rivalen: 1, Aufstellung: Aufstellungsart.Vorgabe), karte: karte).Spieler.Inhaber);
        }),

        ("Start: Fehler in der Karte werden gemeldet – Platz unbekannt oder kein Umlandbetrieb, falsche Betriebsart, Wappen doppelt, Platz doppelt; ein alter Wohnhausplatz wird still vergessen", () =>
        {
            void Fehler(Action<JsonNode> aendern, string meldung)
            {
                var json = GreyboxJson();
                aendern(json);
                Pruefe.WirftMitMeldung(() => Stadtkarte.AusJson(json.ToJsonString(), Katalog()), meldung);
            }
            Fehler(j => j["start"]!["haeuser"]![0]!["betriebe"]![0]!["platz"] = "Nirgendwo", "„Nirgendwo“ ist kein Umlandbetrieb");
            Fehler(j => j["start"]!["haeuser"]![0]!["betriebe"]![0]!["platz"] = "Markt", "„Markt“ ist kein Umlandbetrieb");
            Fehler(j => j["start"]!["haeuser"]![0]!["betriebe"]![1]!["art"] = "Steinbruch", "nicht Steinbruch");
            Fehler(j => j["start"]!["haeuser"]![1]!["wappen"] = j["start"]!["haeuser"]![0]!["wappen"]!.DeepClone(), "dasselbe Wappen");
            Fehler(j => j["start"]!["haeuser"]![1]!["betriebe"]![1]!["platz"] = "Grube_Eisenbart", "mehrfach vergeben");
            // Felder alter Karten (Wohnhaus, Startplatz des Spielers) liest der Kern nicht mehr – kein Fehler, was auch darin steht
            var alt = GreyboxJson();
            alt["start"]!["haeuser"]![1]!["wohnhaus"] = "Markt";
            alt["start"]!["spieler"] = new JsonObject { ["farbe"] = "#336bb8" };
            alt["start"]!["spieler"]!["werkstatt"] = new JsonObject { ["platz"] = "Grube_Eisenbart", ["name"] = "{art} am Markt" };
            var gelesen = Stadtkarte.AusJson(alt.ToJsonString(), Katalog());
            Pruefe.Wahr(gelesen.Startbelegung.Haeuser[1].Wohnhaus is null && gelesen.Startbelegung.Spieler.Werkstatt.Platz == "", "alte Felder vergessen");
            Fehler(j => j["natur"]!["flaechen"]![0]!["gruppen"]![0]!["teile"]![0]!["arten"]!["palme"] = 1, "Natur-Baustein „palme“");
            Fehler(j => j["gebaeude"]!.AsArray().Add(new JsonObject { ["kennung"] = "X", ["katalog"] = "grube", ["punkt"] = new JsonArray(0, 0), ["betrieb"] = "Grube Eisenbart" }),
                "„Grube Eisenbart“ steht an mehreren Plätzen");
        }),

        ("Spielstand: Karte mit Katalog und Start liegt im Spielstand und wird beim Laden wieder aufgelöst", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Endlos, Daten.Value, new NeuePartie(Aufstellung: Aufstellungsart.Vorgabe), karte: Greybox());
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
            var (sim, spieler) = Partieaufbau.Erstellen(Endlos, Daten.Value, new NeuePartie(Aufstellung: Aufstellungsart.Vorgabe), karte: Greybox());
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
            Pruefe.Gleich(Greybox().Bauzonen.Count, w.Karte.Bauzonen.Count); // die Zonen der Kartendatei (seit 4.11.2026 aus dem Raster: 9)
            Pruefe.Gleich(string.Join(" | ", sim.Wirtschaft!.Bauen.Grundstuecke.Select(g => $"{g.Kennung} {g.Grundriss} {g.Besitzer?.Inhaber}")),
                          string.Join(" | ", w.Bauen.Grundstuecke.Select(g => $"{g.Kennung} {g.Grundriss} {g.Besitzer?.Inhaber}")));
            var koeln = Stadtkarte.AusDatei(Altkarte.Pfad("koeln"));
            Pruefe.WirftMitMeldung(() => Speicherung.Laden(alt, koeln), "gehört zur Karte „greybox“");
        }),

        ("Alter Spielstand: Eine Werkstatt ohne Platz in der Karte bekommt beim Laden ein Grundstück in einer Bauzone – bei jedem Laden dasselbe", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Endlos, Daten.Value, new NeuePartie(Startvermoegen: 50_000, Aufstellung: Aufstellungsart.Vorgabe), karte: Greybox());
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
            var (sim, spieler) = Partieaufbau.Erstellen(Endlos, alteDaten, new NeuePartie(Aufstellung: Aufstellungsart.Vorgabe), karte: Greybox());
            Pruefe.Wahr(sim.Wirtschaft!.FreieLiegenschaften.Any(l => l.Name == "Gießerei an der Südgasse"), "vor dem Laden zu haben");

            var (geladen, ich) = Speicherung.Laden(Speicherung.Speichern(sim, spieler), Greybox());
            var w = geladen.Wirtschaft!;
            Pruefe.Wahr(w.FreieLiegenschaften.All(l => l.Name != "Gießerei an der Südgasse"), "nach dem Laden nicht mehr zu haben");
            Pruefe.Gleich("Eine Liegenschaft „Gießerei an der Südgasse“ gibt es nicht.", w.LiegenschaftErwerbVerboten(ich!, "Gießerei an der Südgasse"));
            Pruefe.Wahr(w.FreieLiegenschaften.Any(l => l.Name == "Grube am Silberberg"), "Umland bleibt zu haben");
        }),
    };
}
