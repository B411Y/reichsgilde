using Reichsgilde.Core.Zeit;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests für Kartenleser, Bauprüfung, Leerstand kaufen und abreißen, Neubau von Wohnhaus und Werkstatt
/// und die Betriebe toter Häuser, die zum Verkauf stehen (Entscheidungen vom 17.10.2026).
///
/// Die Testkarte ist klein und rechtwinklig, damit man die Zahlen im Kopf nachrechnen kann:
///   • Hauptstraße (Karrenstraße) entlang z = 0, Gerbergasse entlang x = −60 (nach Süden).
///   • Nördlich der Hauptstraße vier Wohnhausplätze (16 × 10, Eingang nach Süden), Mitte bei x = −30, −10, 10, 30, z = −9
///     (W2 mit „lage“). Ohne Abschnitt „start“ gilt die Reihenfolge der Datei und die Startbelegung ohne Karte.
///   • Südlich: Kernzone K1 (x −40…40, z 4…40) mit dem Werkstattplatz „Weinmarkt“ (16 × 14 um 0/30),
///     Vorstadt V1 an der Gasse (x −56…−42, z 20…60), Umland U1 (x 60…100, z 4…40).
/// </summary>
static class BauenTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));

    /// <summary>Der kleine Katalog der Testkarte: Markt, Wohnhausplatz 16 × 10, Werkstattplatz 16 × 14 und der Mauer-Baukasten.</summary>
    public const string KatalogJson = """
    {
      "format": 1,
      "bausteine": [
        { "kennung": "markt", "art": "sonderbau", "groesse": [10, 10] },
        { "kennung": "wohnhausplatz", "art": "wohnhausplatz", "groesse": [16, 10] },
        { "kennung": "werkstattplatz", "art": "werkstattplatz", "groesse": [16, 14] },
        { "kennung": "stadtmauer", "art": "mauer", "modell": "stadtmauer" }
      ]
    }
    """;

    public const string KarteJson = """
    {
      "format": 2, "kennung": "test", "stadt": "Teststadt", "raster": 2.0,
      "mauern": [ { "name": "Stadtmauer", "linie": [[-120, -50], [120, -50]] } ],
      "wege": [
        { "name": "Hauptstraße", "klasse": "haupt", "linie": [[-100, 0], [100, 0]] },
        { "name": "Gerbergasse", "klasse": "gasse", "linie": [[-60, 0], [-60, 70]] }
      ],
      "bauzonen": [
        { "kennung": "K1", "art": "kern", "flaeche": [[-40, 4], [40, 4], [40, 40], [-40, 40]] },
        { "kennung": "V1", "art": "vorstadt", "flaeche": [[-56, 20], [-42, 20], [-42, 60], [-56, 60]] },
        { "kennung": "U1", "art": "umland", "flaeche": [[60, 4], [100, 4], [100, 40], [60, 40]] }
      ],
      "gebaeude": [
        { "kennung": "Markt", "katalog": "markt", "name": "Markt", "punkt": [0, -30] },
        { "kennung": "W1", "katalog": "wohnhausplatz", "name": "Wohnhausplatz 1", "punkt": [-30, -9] },
        { "kennung": "W2", "katalog": "wohnhausplatz", "name": "Wohnhausplatz 2", "punkt": [-10, -9], "lage": "mauerring" },
        { "kennung": "W3", "katalog": "wohnhausplatz", "name": "Wohnhausplatz 3", "punkt": [10, -9] },
        { "kennung": "W4", "katalog": "wohnhausplatz", "name": "Wohnhausplatz 4", "punkt": [30, -9] },
        { "kennung": "G1", "katalog": "werkstattplatz", "name": "Weinmarkt", "punkt": [0, 30] }
      ]
    }
    """;

    static Gebaeudekatalog Katalog() => Gebaeudekatalog.AusJson(KatalogJson);

    static Stadtkarte Karte() => Stadtkarte.AusJson(KarteJson, Katalog());

    static Simulation NeueSimulation() =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);

    static (Simulation Sim, Stadtwirtschaft W) MitKarte()
    {
        var sim = NeueSimulation();
        sim.Wirtschaft!.KarteSetzen(Karte());
        return (sim, sim.Wirtschaft);
    }

    /// <summary>Ein Haus mit Stand, Wohnhausstufe und Bargeld – ohne die Wege dorthin (die prüfen Stand- und Wohnhaustests).</summary>
    static Konto Haus(Stadtwirtschaft w, string name, int stand, int stufe, double vermoegen, string beruf = "Schmied")
    {
        var k = w.KontoEroeffnen(name, startvermoegen: vermoegen);
        k.Stand = stand;
        k.WohnhausStufe = stufe;
        k.BerufLernen(beruf);
        w.Wohnen.HauswertAktualisieren(k);
        return k;
    }

    static void Ticken(Simulation sim, int minuten)
    {
        for (var i = 0; i < minuten; i++) sim.Tick();
    }

    /// <summary>Sucht game/karten/&lt;kennung&gt;.json, egal von wo aus die Tests gestartet werden.</summary>
    static string Kartenpfad(string kennung) => Datenpfad.Karte(kennung);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Karte: Köln wird gelesen – Wege mit Klasse, Bauzonen, Gebäude aus dem Katalog; Startgrundstücke als Werkstatt- und Wohnhausplatz", () =>
        {
            var k = Stadtkarte.AusDatei(Kartenpfad("koeln"));
            Pruefe.Gleich(2, k.Format);
            Pruefe.Gleich("koeln", k.Kennung);
            Pruefe.Gleich(2.0, k.Raster);
            Pruefe.Gleich(33, k.Wege.Count); // Stadtskript Köln (PR #213): 15 Haupt- (seit 1.11.2026 mit dem Malzbüchel), 8 Land-, 3 Wallstraßen, 5 Gassen, 2 Leinpfade
            Pruefe.Wahr(k.Wege.Any(w => w.Klasse == Wegklasse.Gasse) && k.Wege.Any(w => w.Klasse == Wegklasse.Wall), "Gassen und Wallstraße fehlen");
            Pruefe.Gleich(k.Wege.Count(w => w.Klasse is Wegklasse.Land or Wegklasse.Haupt or Wegklasse.Ring or Wegklasse.Wall), k.Wege.Count(w => w.IstKarrenstrasse));
            Pruefe.Gleich(52, k.Bauzonen.Count); // seit der gemeinsamen Strecke von Kern und Bild (31.10.2026) 52, seit den Märkten an den Enden der Mauer und in Deutz 51, seit der Hausreihe vor dem Tor (1.11.2026) 52
            Pruefe.Gleich(11, k.Bauzonen.Count(z => z.Art == Lage.Kern)); // um den alten Heumarkt ist Bauland: 11 → 12; seit 1.11.2026 liegt das Startgrundstück an der Stadtseite des Alter Markts auf der Zone zwischen Dom und Trankgasse: 11
            Pruefe.Gleich(8, k.Bauzonen.Count(z => z.Art == Lage.Umland));
            Pruefe.Wahr(k.Gebaeude.All(g => g.Baustein is not null), "jedes Gebäude ist im Katalog aufgelöst");
            Pruefe.Gleich(8, k.Wohnhausplaetze.Count);
            Pruefe.Gleich(8, k.GebaeudeDerArt(Bausteinart.Werkstattplatz).Count());
            Pruefe.Gleich(2, k.Mauern.Count); // Stadtmauer und Deutzer Mauer
            Pruefe.Gleich(10, k.Mauern.Sum(m => m.Tore.Count)); // 7 in der Stadtmauer (fünf Tore, zwei Ufertore), 3 in Deutz
            Pruefe.Wahr(k.Wasser.Any(x => x.Name == "Rhein") && k.Wasser.Any(x => x.Name == "Duffesbach") && k.Uebergaenge.Count == 1, "Rhein, Duffesbach, Fähre");
            Pruefe.Wahr(k.GebaeudeMit("Rheinhafen")!.Katalog == "hafenplatz" && k.GebaeudeMit("Rheinkran") is not null, "Hafen und Kran am Ufer");
            Pruefe.Wahr(k.Wasserplan.Bruecken.Any(b => b.WegName == "Rheinbrücke" && b.Art == Uebergangsart.Steinbruecke), "Rheinbrücke aus Stein");
            Pruefe.Wahr(k.Start is not null, "Köln hat eine Startbelegung");
            Pruefe.Gleich(7, k.Startbelegung.Haeuser.Count); // sieben KI-Häuser und der Spieler auf den acht Startgrundstücken
            Pruefe.Gleich("Haus Overstolz", k.Startbelegung.Haeuser[0].Name);
            Pruefe.Gleich("S1_Wohnhaus", k.Startbelegung.Spieler.Wohnhaus);

            var s1 = k.GebaeudeMit("S2_Werkstatt")!.Grundriss!.Value; // 16 × 14 im Startgrundstück, an der Rheingasse vor dem Heumarkt (seit 31.10.2026)
            Pruefe.Wahr(s1 is { Breite: 16, Tiefe: 14 }, "Werkstattplatz 16 × 14 m");
            Pruefe.Wahr(k.GebaeudeMit("Gereon")!.Grundriss is { Breite: 16, Tiefe: 14 }, "Kirche 16 × 14 wie im Kapazitätswerkzeug");

            var k1 = k.Bauzonen.First(z => z.Art == Lage.Kern && z.Enthaelt(z.Flaeche.Average(p => p[0]), z.Flaeche.Average(p => p[1]))); // eine Kernzone, deren Mitte in ihr liegt
            var (mx, mz) = (k1.Flaeche.Average(p => p[0]), k1.Flaeche.Average(p => p[1]));
            Pruefe.Wahr(k1.Enthaelt(mx, mz), "Die Mitte von K1 liegt in K1");
            Pruefe.Gleich(k1, k.ZoneBei(mx, mz));
            Pruefe.Gleich(Lage.Umland, k.ZoneBei(-400, 300)!.Art); // weit draußen: die nächste Zone (U2)
        }),

        ("Karte: Fehler werden gemeldet – kaputtes JSON, altes und zu neues Format, unbekannter Baustein, fehlender Katalog", () =>
        {
            var katalog = Katalog();
            Pruefe.WirftMitMeldung(() => Stadtkarte.AusJson("{ \"format\": 2, ", katalog), "Kein gültiges JSON");
            Pruefe.WirftMitMeldung(() => Stadtkarte.AusJson("""{ "format": 1 }""", katalog), "Format 1 wird nicht mehr gelesen");
            Pruefe.WirftMitMeldung(() => Stadtkarte.AusJson("""{ "format": 3 }""", katalog), "neuer als dieser Leser");
            Pruefe.WirftMitMeldung(() => Stadtkarte.AusJson("""{ "wege": [ { "name": "X", "klasse": "autobahn", "linie": [[0,0],[1,1]] } ] }""", katalog), "Kein gültiges JSON");
            Pruefe.WirftMitMeldung(() => Stadtkarte.AusJson("""{ "gebaeude": [ { "kennung": "X", "katalog": "schloss", "punkt": [0, 0] } ] }""", katalog),
                "Den Baustein „schloss“ gibt es im Katalog nicht");
            // Ein schräger Wohnhausplatz ist seit PR #209 erlaubt
            Pruefe.Wahr(!Stadtkarte.AusJson("""{ "gebaeude": [ { "kennung": "W", "katalog": "wohnhausplatz", "punkt": [0, 0], "drehung": 45 } ] }""", katalog)
                .Pruefen().Any(f => f.Contains("rechtwinklig")), "schräger Wohnhausplatz liest sich");
            Pruefe.WirftMitMeldung(() => Stadtkarte.AusJson("""{ "mauern": [ { "name": "M", "linie": [[0, 0], [1, 0]], "katalog": "zaun" } ] }""", katalog),
                "Mauer-Baukasten „zaun“");
            var ohne = System.Text.Json.JsonSerializer.Deserialize<Stadtkarte>("{}")!;
            Pruefe.Wahr(ohne.Pruefen().Contains("Der Gebäudekatalog fehlt."), "ohne Katalog");
        }),

        ("Grundriss: Drehung, Ausdehnung, Raster, Felder und Vorplatz", () =>
        {
            var g = new Grundriss(-24, 11, 16, 14, 2); // Eingang nach Norden
            Pruefe.Gleich((8.0, 7.0), (g.HalbX, g.HalbZ));
            Pruefe.Gleich((-24.0, 4.0), g.Eingang);
            Pruefe.Wahr(g.AufDemRaster(2), "liegt auf dem Raster");
            Pruefe.Wahr(!(g with { X = -23 }).AufDemRaster(2), "−23 liegt neben dem Raster");
            Pruefe.Gleich(56, g.Felder(2).Count());
            Pruefe.Wahr(g.Felder(2, nurVorplatz: true).All(f => Math.Abs(f.Z - 5) < 1e-9), "Vorplatz ist die nördlichste Reihe (z = 5)");
            var quer = new Grundriss(-51, 28, 8, 10, 3); // Eingang nach Westen: Breite liegt entlang z
            Pruefe.Gleich((5.0, 4.0), (quer.HalbX, quer.HalbZ));
            Pruefe.Wahr(quer.Felder(2, nurVorplatz: true).All(f => Math.Abs(f.X + 55) < 1e-9), "Vorplatz ist die westlichste Reihe (x = −55)");
            Pruefe.Wahr(new Grundriss(0, 0, 4, 4, 0).Ueberlappt(new Grundriss(3, 0, 4, 4, 0)), "überlappt");
            Pruefe.Wahr(!new Grundriss(0, 0, 4, 4, 0).Ueberlappt(new Grundriss(4, 0, 4, 4, 0)), "aneinanderstoßen ist erlaubt");
            Pruefe.Gleich(1, Grundriss.DrehungZu(5, 1));
            Pruefe.Gleich(2, Grundriss.DrehungZu(0, -3));
        }),

        ("Bauen: Startplätze der Karte in Konto-Reihenfolge – auch, wenn die Karte erst nach den Häusern kommt", () =>
        {
            var (_, w) = MitKarte();
            var konten = Enumerable.Range(1, 5).Select(i => w.KontoEroeffnen($"Haus {i}")).ToList();
            for (var i = 0; i < 4; i++)
            {
                var g = w.Bauen.Wohnhaus(konten[i])!;
                Pruefe.Gleich($"W{i + 1}", g.Kennung);
                Pruefe.Gleich(-30.0 + 20 * i, g.Grundriss.X);
                Pruefe.Gleich(0, g.Grundriss.Drehung);
                Pruefe.Gleich(Bauzustand.Bewohnt, g.Zustand);
            }
            Pruefe.Wahr(w.Bauen.Wohnhaus(konten[4]) is null, "Für das fünfte Haus hat die Karte keinen Platz");
            Pruefe.Gleich(Lage.Mauerring, w.Bauen.Wohnhaus(konten[1])!.Lage); // aus der Karte („lage“)
            Pruefe.Gleich(Lage.Kern, w.Bauen.Wohnhaus(konten[0])!.Lage);      // ohne Angabe: die nächste Bauzone

            var sim2 = NeueSimulation();
            var a = sim2.Wirtschaft!.KontoEroeffnen("Haus A");
            var b = sim2.Wirtschaft.KontoEroeffnen("Haus B");
            sim2.Wirtschaft.KarteSetzen(Karte());
            Pruefe.Gleich("W1", sim2.Wirtschaft.Bauen.Wohnhaus(a)!.Kennung);
            Pruefe.Gleich("W2", sim2.Wirtschaft.Bauen.Wohnhaus(b)!.Kennung);
            Pruefe.Wirft(() => sim2.Wirtschaft.KarteSetzen(Karte()));

            // Partieaufbau: Spieler, Eisenbart, Eichwald, Kieselbach – in dieser Reihenfolge.
            var (sim3, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie(), karte: Karte());
            var w3 = sim3.Wirtschaft!;
            Pruefe.Gleich("W1", w3.Bauen.Wohnhaus(spieler)!.Kennung);
            Pruefe.Gleich("W4", w3.Bauen.Wohnhaus(w3.Konten.Single(k => k.Inhaber == Partieaufbau.DritterRivale))!.Kennung);

            // Ohne Karte gibt es kein Bauwesen.
            var ohne = NeueSimulation().Wirtschaft!;
            var c = ohne.KontoEroeffnen("Haus C", startvermoegen: 50_000);
            Pruefe.Wahr(!ohne.Bauen.IstAktiv && ohne.Bauen.Grundstuecke.Count == 0, "ohne Karte keine Grundstücke");
            Pruefe.Gleich("In dieser Stadt wird nicht gebaut.", ohne.Ausfuehren(c, new WohnhausBauen(0, 0, 11, 2)).Meldung);
        }),

        ("Bauen: Bauprüfung – Bauzone, Raster, Lage, Überdeckung und Vorderseite an der passenden Straße", () =>
        {
            var (_, w) = MitKarte();
            var b = w.Bauen;
            Pruefe.Gleich(null, b.PlatzVerboten(new WerkstattBauen(Betriebsart.Giesserei, -24, 11, 2)));
            Pruefe.Wahr(b.PlatzVerboten(new WerkstattBauen(Betriebsart.Giesserei, -24, 11, 0))!.Contains("Karrenstraße"), "Rücken zur Straße");
            Pruefe.Wahr(b.PlatzVerboten(new WerkstattBauen(Betriebsart.Giesserei, -23, 11, 2))!.Contains("Raster"), "neben dem Raster");
            Pruefe.Gleich(new WerkstattBauen(Betriebsart.Giesserei, -24, 11, 2), b.Einrasten(new WerkstattBauen(Betriebsart.Giesserei, -23.4, 10.2, 2)));
            Pruefe.Gleich("Hier steht Weinmarkt.", b.PlatzVerboten(new WerkstattBauen(Betriebsart.Giesserei, 0, 25, 2)));
            Pruefe.Wahr(b.PlatzVerboten(new WerkstattBauen(Betriebsart.Giesserei, -24, -31, 0))!.Contains("keine Bauzone"), "nördlich der Straße ist keine Zone");
            Pruefe.Gleich("Im Umland wird nicht gebaut.", b.PlatzVerboten(new WerkstattBauen(Betriebsart.Giesserei, 80, 11, 2)));
            Pruefe.Wahr(b.PlatzVerboten(new WerkstattBauen(Betriebsart.Giesserei, 34, 11, 2))!.Contains("keine Bauzone"), "ragt aus der Zone");
            Pruefe.Wahr(b.PlatzVerboten(new WohnhausBauen(0, -24, 11, 5))!.Contains("Drehung"), "Drehung 0–3");

            // An der Gerbergasse (Vorstadt): Wohnhaus ja, Werkstatt nein, Stadthof nur in Kernlage oder am Mauerring.
            Pruefe.Gleich(null, b.PlatzVerboten(new WohnhausBauen(1, -51, 28, 3)));
            Pruefe.Wahr(b.PlatzVerboten(new WerkstattBauen(Betriebsart.Giesserei, -49, 28, 3))!.Contains("Karrenstraße"), "Werkstatt an der Gasse");
            Pruefe.Gleich("Ein Stadthof steht nur in Kernlage oder am Mauerring.", b.PlatzVerboten(new WohnhausBauen(4, -49, 32, 3)));
            Pruefe.Gleich(null, b.PlatzVerboten(new WohnhausBauen(4, -24, 11, 2))); // in Kernlage an der Hauptstraße

            // Wohnhausplätze und Baustellen sind belegt.
            var k = Haus(w, "Haus Bau", stand: 1, stufe: 1, vermoegen: 20_000);
            Pruefe.Gleich("Hier steht schon das Wohnhaus von Haus Bau.", b.PlatzVerboten(new WohnhausBauen(1, -30, -9, 0)));
            Pruefe.Wahr(b.PlatzVerboten(new WohnhausBauen(1, 10, -9, 0))!.Contains("Wohnhausplatz 3"), "unvergebener Startplatz bleibt frei");
            Pruefe.Wahr(w.Ausfuehren(k, new WerkstattBauen(Betriebsart.Giesserei, -24, 11, 2)).Erfolg, "Werkstatt bauen");
            Pruefe.Gleich("Hier steht schon die Baustelle von Gießerei an der Hauptstraße.", b.PlatzVerboten(new WohnhausBauen(1, -20, 9, 2)));
            Pruefe.Gleich(null, b.PlatzVerboten(new WohnhausBauen(1, -12, 9, 2))); // direkt daneben
        }),

        ("Bauen: Werkstatt – Preis wie eine freie, ein Jahr Bauzeit ohne Löhne und Unterhalt, danach wie gekauft", () =>
        {
            var (sim, w) = MitKarte();
            var k = Haus(w, "Haus Esse", stand: 1, stufe: 1, vermoegen: 10_000);
            var befehl = new WerkstattBauen(Betriebsart.Giesserei, -24, 11, 2);
            var v = w.Bauen.Vorschau(k, befehl);
            Pruefe.Gleich(null, v.Verboten);
            Pruefe.Gleich("Gießerei", v.Gebaeude);
            Pruefe.Gleich("Gießerei an der Hauptstraße", v.Betrieb);
            Pruefe.Gleich(3_500.0, v.Kosten);
            Pruefe.Gleich(Spielzeit.MinutenProTag, v.BauzeitMinuten);
            Betrieb? erworben = null;
            w.BetriebErworben += b => erworben = b;
            Grundstueck? begonnen = null, fertig = null;
            w.Bauen.BauBegonnen += g => begonnen = g;
            w.Bauen.BauFertig += g => fertig = g;

            var e = w.Ausfuehren(k, befehl);
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(6_500.0, k.Vermoegen);
            Pruefe.Gleich(3_500.0, k.Laufend.Investitionen);
            var g = w.Bauen.Werkstatt("Gießerei an der Hauptstraße")!;
            Pruefe.Gleich(g, begonnen);
            Pruefe.Gleich(Bauzustand.Baustelle, g.Zustand);
            Pruefe.Gleich(Bauarbeit.Neubau, g.Arbeit);
            Pruefe.Gleich((long)Spielzeit.MinutenProTag, g.FertigAb);
            Pruefe.Wahr(w.Betriebe.All(x => x.Name != g.Betrieb), "noch kein Betrieb");

            // Die zweite Werkstatt an derselben Straße bekommt eine römische Zahl.
            Pruefe.Gleich("Gießerei an der Hauptstraße II", w.Bauen.Vorschau(k, befehl with { X = -8 }).Betrieb);

            Ticken(sim, Spielzeit.MinutenProTag); // fertig ist die Baustelle in der Minute 1.140 – das ist schon das nächste Jahr
            Pruefe.Wahr(w.Betriebe.All(x => x.Name != g.Betrieb), "vor Minute 1.140 noch Baustelle");
            Pruefe.Gleich(0.0, k.Vorjahr!.Personal + k.Vorjahr.Fixkosten); // das Baujahr kostete keine Löhne, keinen Unterhalt
            Ticken(sim, 1);
            var betrieb = w.Betriebe.Single(x => x.Name == "Gießerei an der Hauptstraße");
            Pruefe.Gleich(k, betrieb.Konto);
            Pruefe.Gleich(Betriebsart.Giesserei, betrieb.Art);
            Pruefe.Gleich(1, betrieb.Karren.Count); // Startkarren wie beim Kauf
            Pruefe.Gleich(betrieb, erworben);
            Pruefe.Gleich(g, fertig);
            Pruefe.Gleich(Bauzustand.Werkstatt, g.Zustand);
            Pruefe.Wahr(w.Ausfuehren(k, new PersonalSetzen(betrieb.Name, "Lehrling", 2)).Erfolg, "Personal einstellen");
        }),

        ("Bauen: Werkstatt – Voraussetzungen wie beim Kauf (Stand, Handwerk, Kaufpreis und ein Jahr Unterhalt), nur Stufe 1 in der Stadt", () =>
        {
            var (_, w) = MitKarte();
            var befehl = new WerkstattBauen(Betriebsart.Giesserei, -24, 11, 2);
            var gemein = Haus(w, "Haus Gemein", stand: 0, stufe: 0, vermoegen: 50_000);
            Pruefe.Wahr(w.Ausfuehren(gemein, befehl).Meldung.Contains("mindestens Bürger"), "Gemeine bauen keine Werkstatt");
            var tischler = Haus(w, "Haus Hobel", stand: 1, stufe: 1, vermoegen: 50_000, beruf: "Tischler");
            Pruefe.Wahr(w.Ausfuehren(tischler, befehl).Meldung.Contains("beherrscht dieses Handwerk nicht"), "fremdes Handwerk");
            var arm = Haus(w, "Haus Arm", stand: 1, stufe: 1, vermoegen: 3_550);
            Pruefe.Wahr(w.Ausfuehren(arm, befehl).Meldung.Contains("ein Jahr Unterhalt"), "Unterhalt muss bar da sein");
            var reich = Haus(w, "Haus Reich", stand: 1, stufe: 1, vermoegen: 50_000);
            Pruefe.Wahr(w.Ausfuehren(reich, befehl with { Art = Betriebsart.Mine }).Meldung.Contains("gepachtet"), "keine Grube in der Stadt");
            Pruefe.Wahr(w.Ausfuehren(reich, befehl with { Art = Betriebsart.Schmiede }).Meldung.Contains("nur Stufe 1"), "Schmiede entsteht durch Ausbau");
            Pruefe.Wahr(w.Bauen.BauherrVerboten(reich, befehl) is null, "Bürger mit Handwerk und Geld darf");
        }),

        ("Bauen: Wohnhaus neu – Hauswert der Zielstufe minus altes Haus, zwei Jahre ab Steinhaus, dann Umzug und das alte wird Bauplatz", () =>
        {
            var (sim, w) = MitKarte();
            var k = Haus(w, "Haus Stein", stand: 1, stufe: 1, vermoegen: 6_000); // Bürger im Bürgerhaus (Hauswert 1.500)
            var alt = w.Bauen.Wohnhaus(k)!;
            Pruefe.Wahr(w.Ausfuehren(k, new WohnhausBauen(0, -24, 11, 2)).Meldung.Contains("kleiner bauen"), "kleiner geht nicht");
            Pruefe.Wahr(w.Ausfuehren(k, new WohnhausBauen(3, -24, 11, 2)).Meldung.Contains("mindestens Patrizier"), "Stadtpalais erst als Patrizier");

            var befehl = new WohnhausBauen(2, -24, 9, 2); // Steinhaus 8 × 10 an der Hauptstraße
            var v = w.Bauen.Vorschau(k, befehl);
            Pruefe.Gleich((5_500.0, 1_500.0, 4_000.0, 2 * Spielzeit.MinutenProTag), (v.Preis, v.Anrechnung, v.Kosten, v.BauzeitMinuten));
            var rein = k.Reinvermoegen;
            Pruefe.Wahr(w.Ausfuehren(k, befehl).Erfolg, "Steinhaus bauen");
            Pruefe.Gleich(2_000.0, k.Vermoegen);
            Pruefe.Gleich(5_500.0, k.Hauswert);  // das Geld steckt schon im neuen Haus …
            Pruefe.Gleich(rein, k.Reinvermoegen); // … das Reinvermögen bleibt
            Pruefe.Gleich(1, k.WohnhausStufe);    // gewohnt wird bis zum Umzug im alten
            Pruefe.Gleich(alt, w.Bauen.Wohnhaus(k));
            Pruefe.Gleich(2, w.Bauen.UmzugZiel(k));
            Pruefe.Wahr(w.Wohnen.AusbauVerboten(k)!.Contains("neues Wohnhaus"), "kein Ausbau während des Neubaus");
            Pruefe.Wahr(w.Ausfuehren(k, new WohnhausBauen(2, 8, 9, 2)).Meldung.Contains("baut schon"), "nur ein Neubau zugleich");
            k.Stand = 1;
            Pruefe.Wahr(w.Wohnen.StandVerboten(k, 2)!.Contains("noch nicht bezogen"), "Patrizier erst nach dem Umzug");

            Grundstueck? abgerissen = null;
            w.Bauen.Abgerissen += g => abgerissen = g;
            Ticken(sim, 2 * Spielzeit.MinutenProTag + 1);
            Pruefe.Gleich(2, k.WohnhausStufe);
            Pruefe.Gleich(5_500.0, k.Hauswert);
            var neu = w.Bauen.Wohnhaus(k)!;
            Pruefe.Gleich((-24.0, 9.0), (neu.Grundriss.X, neu.Grundriss.Z));
            Pruefe.Gleich(Bauzustand.Bewohnt, neu.Zustand);
            Pruefe.Gleich(alt, abgerissen);
            Pruefe.Gleich(Bauzustand.Bauplatz, alt.Zustand);
            Pruefe.Gleich(null, w.Bauen.UmzugZiel(k));
            Pruefe.Gleich(null, w.Wohnen.StandVerboten(k, 2));
        }),

        ("Bauen: Leerstand kaufen – 50 % + 20 % Sanierung − eigenes Haus, 4 Stunden, dann Umzug; der Leerstand ist weg", () =>
        {
            var (sim, w) = MitKarte();
            var tot = Haus(w, "Haus Pleite", stand: 3, stufe: 3, vermoegen: 0); // Stadtpalais: Hauswert 15.500
            var k = Haus(w, "Haus Glück", stand: 2, stufe: 2, vermoegen: 6_000); // Patrizier im Steinhaus (5.500)
            var palais = w.Bauen.Wohnhaus(tot)!;
            var altesHaus = w.Bauen.Wohnhaus(k)!;
            Pruefe.Wahr(w.Ausfuehren(k, new LeerstandKaufen(tot.Inhaber)).Meldung.Contains("wohnt noch"), "nur Leerstand");
            tot.IstBankrott = true;
            Pruefe.Gleich(Bauzustand.Leerstand, palais.Zustand);
            Pruefe.Wahr(w.Wohnen.Leerstand.Contains(tot), "Leerstand");

            var v = w.Bauen.LeerstandVorschau(k, tot);
            Pruefe.Gleich(("Stadtpalais", 15_500.0, 7_750.0, 3_100.0, 5_500.0, 5_350.0), (v.Gebaeude, v.Hauswert, v.Kaufpreis, v.Sanierung, v.Anrechnung, v.KaufSumme));
            Pruefe.Gleich((1_550.0, 9_300.0, 240, 240), (v.Abriss, v.AbrissSumme, v.SanierungMinuten, v.AbrissMinuten));
            Pruefe.Gleich(null, v.KaufVerboten);
            Pruefe.Wahr(v.AbrissVerboten!.Contains("es fehlen"), "für den Abriss reicht das Geld nicht");

            var e = w.Ausfuehren(k, new LeerstandKaufen(tot.Inhaber));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(650.0, k.Vermoegen);
            Pruefe.Gleich(15_500.0, k.Hauswert); // ein Schnäppchen: 30 % des Hauswerts mehr Reinvermögen
            Pruefe.Gleich(0.0, tot.Hauswert);
            Pruefe.Wahr(!w.Wohnen.Leerstand.Contains(tot), "verkauft – kein Leerstand mehr");
            Pruefe.Gleich(Bauzustand.Baustelle, palais.Zustand);
            Pruefe.Gleich(Bauarbeit.Sanierung, palais.Arbeit);
            Pruefe.Gleich(3, palais.Stufe);
            Ticken(sim, 241);
            Pruefe.Gleich(3, k.WohnhausStufe);
            Pruefe.Gleich(palais, w.Bauen.Wohnhaus(k));
            Pruefe.Gleich(Bauzustand.Bewohnt, palais.Zustand);
            Pruefe.Gleich(Bauzustand.Bauplatz, altesHaus.Zustand);
            Pruefe.Gleich(15_500.0, k.Hauswert);
        }),

        ("Bauen: Leerstand kaufen – nur größer als das eigene Haus und höchstens eine Stufe über dem Stand", () =>
        {
            var (_, w) = MitKarte();
            var tot = Haus(w, "Haus Pleite", stand: 1, stufe: 1, vermoegen: 0);
            var k = Haus(w, "Haus Gleich", stand: 1, stufe: 1, vermoegen: 50_000);
            tot.IstErloschen = true;
            Pruefe.Wahr(w.Ausfuehren(k, new LeerstandKaufen(tot.Inhaber)).Meldung.Contains("nicht größer"), "gleich groß lohnt nicht");
            var palast = Haus(w, "Haus Palast", stand: 3, stufe: 3, vermoegen: 0);
            palast.IstBankrott = true;
            Pruefe.Wahr(w.Ausfuehren(k, new LeerstandKaufen(palast.Inhaber)).Meldung.Contains("mindestens Patrizier"), "Stadtpalais erst als Patrizier");
            Pruefe.Wahr(w.Ausfuehren(k, new LeerstandKaufen("Haus Niemand")).Meldung.Contains("gibt es nicht"), "unbekanntes Haus");
        }),

        ("Bauen: Leerstand abreißen – Kauf + Abriss (mindestens 100), danach ein Bauplatz, auf dem wieder gebaut werden darf", () =>
        {
            var (sim, w) = MitKarte();
            var tot = Haus(w, "Haus Hütte", stand: 0, stufe: 0, vermoegen: 0); // Fachwerkhaus: Hauswert 0
            var k = Haus(w, "Haus Neu", stand: 1, stufe: 1, vermoegen: 5_000);
            tot.IstBankrott = true;
            var huette = w.Bauen.Wohnhaus(tot)!;
            Pruefe.Gleich(100.0, w.Bauen.LeerstandVorschau(k, tot).AbrissSumme);
            Pruefe.Wahr(w.Ausfuehren(k, new LeerstandAbreissen(tot.Inhaber)).Erfolg, "abreißen");
            Pruefe.Gleich(4_900.0, k.Vermoegen);
            Pruefe.Gleich(Bauarbeit.Abriss, huette.Arbeit);
            Pruefe.Wahr(!w.Wohnen.Leerstand.Contains(tot), "kein Leerstand mehr");
            Pruefe.Wahr(w.Ausfuehren(k, new LeerstandAbreissen(tot.Inhaber)).Meldung.Contains("nicht mehr leer"), "nur einmal");
            Ticken(sim, 241);
            Pruefe.Gleich(Bauzustand.Bauplatz, huette.Zustand);
            Pruefe.Wahr(w.Bauen.Bauplaetze.Contains(huette), "Bauplatz");
            Pruefe.Gleich(Lage.Kern, huette.Lage); // außerhalb der Zonen – die nächste ist K1

            // Ein Wohnhausplatz (außerhalb jeder Bauzone) nimmt jedes Wohnhaus auf – eingepasst in seine Fläche und Drehung, keine Werkstatt.
            var befehl = w.Bauen.Einrasten(new WohnhausBauen(1, -27, -10, 2));
            Pruefe.Gleich(new WohnhausBauen(1, -30, -9, 0), befehl);
            Pruefe.Gleich(new Grundriss(-30, -9, 16, 10, 0), w.Bauen.Grundriss(befehl));
            Pruefe.Gleich(null, w.Bauen.BauVerboten(k, befehl));
            Pruefe.Gleich(null, w.Bauen.PlatzVerboten(new WohnhausBauen(4, -30, -9, 0))); // auch ein Stadthof, eingepasst (Lage Kern)
            Pruefe.Gleich(new Grundriss(-30, -9, 16, 10, 0), w.Bauen.Vorschau(k, new WohnhausBauen(4, -30, -9, 0)).Grundriss);
            Pruefe.Wahr(w.Bauen.PlatzVerboten(new WohnhausBauen(1, -32, -9, 0))!.Contains("genau auf den Wohnhausplatz"), "halb darauf");
            Pruefe.Gleich("Auf einem Wohnhausplatz stehen nur Wohnhäuser.", w.Bauen.PlatzVerboten(new WerkstattBauen(Betriebsart.Giesserei, -30, -9, 0)));
            Pruefe.Gleich(new WerkstattBauen(Betriebsart.Giesserei, -30, -9, 0), w.Bauen.Einrasten(new WerkstattBauen(Betriebsart.Giesserei, -29.5, -9.3, 0))); // Werkstatt: nur aufs Raster
            Grundstueck? bebaut = null;
            w.Bauen.BauplatzBebaut += g => bebaut = g;
            Pruefe.Wahr(w.Ausfuehren(k, befehl).Erfolg, "auf dem Bauplatz bauen");
            Pruefe.Gleich(huette, bebaut);
            Pruefe.Wahr(!w.Bauen.Grundstuecke.Contains(huette), "Bauplatz ist vergeben");
        }),

        ("Bauen: Grundstücke und Baustellen überstehen Speichern und Laden – samt Karte; alte Spielstände bekommen die Startplätze", () =>
        {
            var (sim, w) = MitKarte();
            var k = Haus(w, "Haus Fuchs", stand: 1, stufe: 1, vermoegen: 20_000);
            var tot = Haus(w, "Haus Pleite", stand: 2, stufe: 2, vermoegen: 0);
            tot.IstBankrott = true;
            Pruefe.Wahr(w.Ausfuehren(k, new WerkstattBauen(Betriebsart.Giesserei, -24, 11, 2)).Erfolg, "Werkstatt");
            Pruefe.Wahr(w.Ausfuehren(k, new LeerstandKaufen(tot.Inhaber)).Erfolg, "Leerstand");
            Ticken(sim, 100);

            var json = Speicherung.Speichern(sim);
            var (sim2, _) = Speicherung.Laden(json);
            var w2 = sim2.Wirtschaft!;
            Pruefe.Wahr(w2.Karte is { Kennung: "test" }, "Karte im Spielstand");
            Pruefe.Gleich(w.Bauen.Grundstuecke.Count, w2.Bauen.Grundstuecke.Count);
            var k2 = w2.Konten.Single(x => x.Inhaber == "Haus Fuchs");
            Pruefe.Gleich(w.Bauen.Wohnhaus(k)!.Nummer, w2.Bauen.Wohnhaus(k2)!.Nummer);
            Pruefe.Gleich(k.Hauswert, k2.Hauswert);
            Pruefe.Gleich(k.Reinvermoegen, k2.Reinvermoegen);
            Ticken(sim, Spielzeit.MinutenProTag - 20); // über das Ende der Bauzeit (ein Jahr) hinaus
            Ticken(sim2, Spielzeit.MinutenProTag - 20);
            Pruefe.Gleich(Speicherung.Speichern(sim, zeitpunkt: DateTime.UnixEpoch), Speicherung.Speichern(sim2, zeitpunkt: DateTime.UnixEpoch));
            Pruefe.Wahr(w2.Betriebe.Any(b => b.Name == "Gießerei an der Hauptstraße"), "die Werkstatt ist auch im geladenen Spiel fertig");
            Pruefe.Gleich(2, w2.Konten.Single(x => x.Inhaber == "Haus Fuchs").WohnhausStufe); // das Steinhaus ist bezogen

            // Alter Spielstand: ohne Karte und ohne Grundstücke – die aktuelle Karte wird übergeben, die Häuser bekommen ihre Startplätze.
            var alt = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
            alt.Remove("Karte");
            alt["Wirtschaft"]!.AsObject().Remove("Bauen");
            var (sim3, _) = Speicherung.Laden(alt.ToJsonString(), Karte());
            var w3 = sim3.Wirtschaft!;
            Pruefe.Gleich("W1", w3.Bauen.Wohnhaus(w3.Konten[0])!.Kennung);
            Pruefe.Gleich("W2", w3.Bauen.Wohnhaus(w3.Konten[1])!.Kennung);
        }),

        ("Tote Häuser: Ihre Betriebe stehen zum Verkauf wie verkaufte – ohne Erlös, mit Stufe; Pachten werden frei", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var tot = w.KontoEroeffnen("Haus Pleite");
            var schmiede = w.BetriebEroeffnen("Gießerei am Weinmarkt", Betriebsart.Giesserei, tot);
            schmiede.Ausbauen(Betriebsart.Schmiede, 0);
            var grube = w.BetriebEroeffnen("Grube Pleite", Betriebsart.Mine, tot);
            w.Einstellen(schmiede, "Geselle", 2);
            var abgegeben = new List<(Betrieb, LiegenschaftDaten)>();
            w.BetriebAbgegeben += (b, l) => abgegeben.Add((b, l));

            tot.IstBankrott = true;
            w.Stilllegen(tot);
            var vorher = tot.Vermoegen;
            sim.Tick();
            Pruefe.Wahr(!w.Betriebe.Contains(schmiede) && !w.Betriebe.Contains(grube), "Betriebe gehören niemandem mehr");
            Pruefe.Gleich(2, abgegeben.Count);
            Pruefe.Gleich(vorher, tot.Vermoegen); // kein Erlös
            var frei = w.FreieLiegenschaften.Single(l => l.Name == "Gießerei am Weinmarkt");
            Pruefe.Gleich((Betriebsart.Schmiede, 6_000.0, true, 3), (frei.Art, frei.Kaufpreis, frei.Bebaut, frei.MindestGrad));
            Pruefe.Wahr(w.FreieLiegenschaften.Single(l => l.Name == "Grube Pleite").IstPacht, "die Grube ist wieder zu pachten");

            var k = Haus(w, "Haus Erbe", stand: 1, stufe: 1, vermoegen: 20_000);
            Pruefe.Wahr(w.Ausfuehren(k, new LiegenschaftErwerben("Grube Pleite")).Erfolg, "pachten");
        }),

        ("Tote Häuser: Bankrott nach drei Jahren im Minus gibt die Betriebe frei; alte Spielstände ebenso", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var arm = w.KontoEroeffnen("Haus Arm", startvermoegen: -50_000);
            w.BetriebEroeffnen("Gießerei Arm", Betriebsart.Giesserei, arm);
            var json = Speicherung.Speichern(sim);
            Ticken(sim, 3 * Spielzeit.MinutenProTag);
            Pruefe.Wahr(arm.IstBankrott, "bankrott");
            Pruefe.Wahr(w.Betriebe.All(b => b.Konto != arm), "Betrieb frei");
            Pruefe.Wahr(w.FreieLiegenschaften.Any(l => l.Name == "Gießerei Arm"), "zu kaufen");

            // Alter Spielstand, in dem ein bankrottes Haus seinen Betrieb noch hat: Beim Laden wird er frei.
            var alt = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
            alt["Wirtschaft"]!["Konten"]![0]!["IstBankrott"] = true;
            var w2 = Speicherung.Laden(alt.ToJsonString()).Simulation.Wirtschaft!;
            Pruefe.Wahr(w2.Betriebe.Count == 0, "beim Laden frei");
            Pruefe.Wahr(w2.FreieLiegenschaften.Any(l => l.Name == "Gießerei Arm"), "zu kaufen nach dem Laden");
        }),

        ("Bauen: Daten werden geprüft – je Wohnhausstufe ein Eintrag, Maße auf dem Raster, Lagen nicht leer, nurNeubau nur ab dem Bürgerhaus", () =>
        {
            Pruefe.Gleich(65, Daten.Value.Version);
            Pruefe.Gleich(5, Daten.Value.Bauen!.Wohnhaus.Count);
            Pruefe.Gleich((16.0, 14.0), (Daten.Value.Bauen.Werkstatt.Breite, Daten.Value.Bauen.Werkstatt.Tiefe));
            Pruefe.Gleich("False False False True True", string.Join(" ", Daten.Value.Bauen.Wohnhaus.Select(m => m.NurNeubau))); // Stadtpalais, Stadthof
            var json = Datenpfad.OhneAktionspunkte();
            var kurz = System.Text.Json.Nodes.JsonNode.Parse(json)!;
            kurz["bauen"]!["wohnhaus"]!.AsArray().RemoveAt(4);
            kurz["bauen"]!["wohnhaus"]![0]!["nurNeubau"] = true;
            kurz["bauen"]!["werkstatt"]!["breite"] = 15;
            kurz["bauen"]!["werkstatt"]!["lagen"] = new System.Text.Json.Nodes.JsonArray();
            kurz["bauen"]!["werkstatt"]!["nurNeubau"] = true;
            try { Wirtschaftsdaten.AusJson(kurz.ToJsonString()); throw new Exception("Fehler erwartet"); }
            catch (WirtschaftsdatenFehler f)
            {
                Pruefe.Wahr(f.Fehler.Any(x => x.Contains("je Stufe des Wohnhauses")), "Stufenzahl");
                Pruefe.Wahr(f.Fehler.Any(x => x.Contains("Vielfache des Rasters")), "Raster");
                Pruefe.Wahr(f.Fehler.Any(x => x.Contains("lagen darf nicht leer")), "Lagen");
                Pruefe.Wahr(f.Fehler.Any(x => x.Contains("Startwohnhaus kann nicht nurNeubau")), "Startwohnhaus");
                Pruefe.Wahr(f.Fehler.Any(x => x.Contains("nurNeubau gibt es nur für Wohnhäuser")), "Werkstatt");
            }
        }),

        ("Bauen: Stadtpalais und Stadthof nur als Neubau, wo gebaut wird – ohne Karte weiter als Ausbau; ein laufender Ausbau wird fertig", () =>
        {
            var (sim, w) = MitKarte();
            var buerger = Haus(w, "Haus Bürger", stand: 1, stufe: 1, vermoegen: 50_000);
            Pruefe.Gleich(null, w.Wohnen.AusbauVerboten(buerger)); // Bürgerhaus → Steinhaus bleibt Ausbau am Platz
            var patrizier = Haus(w, "Haus Patrizier", stand: 2, stufe: 2, vermoegen: 50_000);
            Pruefe.Wahr(w.Bauen.NurNeubau(3) && w.Bauen.NurNeubau(4) && !w.Bauen.NurNeubau(2), "Stadtpalais und Stadthof nur als Neubau");
            Pruefe.Gleich("Ein Stadtpalais entsteht nur als Neubau – im Baumodus neu bauen oder einen Leerstand kaufen.", w.Wohnen.AusbauVerboten(patrizier));
            Pruefe.Wahr(!w.Ausfuehren(patrizier, new WohnhausAusbauen()).Erfolg, "kein Ausbau zum Stadtpalais");
            Pruefe.Wahr(w.Wohnen.StandVerboten(patrizier, 3)!.Contains("Stadtpalais (neu bauen oder einen Leerstand kaufen)"), w.Wohnen.StandVerboten(patrizier, 3)!);
            Pruefe.Gleich(null, w.Bauen.BauVerboten(patrizier, new WohnhausBauen(3, -22, 9, 2))); // als Neubau geht es
            var edler = Haus(w, "Haus Edel", stand: 3, stufe: 3, vermoegen: 50_000);
            Pruefe.Wahr(w.Wohnen.AusbauVerboten(edler)!.StartsWith("Ein Stadthof entsteht nur als Neubau"), "Stadthof");

            // Ein Ausbau zum Stadtpalais, der schon läuft (alter Spielstand), wird normal fertig.
            patrizier.WohnhausFertigAb = sim.Zeit.VergangeneMinuten + 10;
            Ticken(sim, 11);
            Pruefe.Gleich(3, patrizier.WohnhausStufe);

            // Ohne Karte (etwa Köln) bleibt der Ausbau zu allen Stufen erlaubt.
            var ohne = NeueSimulation().Wirtschaft!;
            var koelner = Haus(ohne, "Haus Köln", stand: 3, stufe: 3, vermoegen: 50_000);
            Pruefe.Wahr(!ohne.Bauen.NurNeubau(4), "ohne Karte kein Neubau-Zwang");
            Pruefe.Gleich(null, ohne.Wohnen.AusbauVerboten(koelner));
            Pruefe.Wahr(ohne.Ausfuehren(koelner, new WohnhausAusbauen()).Erfolg, "Ausbau zum Stadthof in Köln");
        }),

        ("Bauen: Seit wann ein Haus leer steht – Minute von Bankrott oder Erlöschen, im Spielstand; alte Spielstände zählen ab dem Laden", () =>
        {
            var (sim, w) = MitKarte();
            var arm = Haus(w, "Haus Arm", stand: 1, stufe: 1, vermoegen: -50_000);
            var k = Haus(w, "Haus Reich", stand: 2, stufe: 1, vermoegen: 50_000);
            Pruefe.Gleich(null, w.Bauen.LeerSeit(arm)); // lebt noch
            Ticken(sim, 3 * Spielzeit.MinutenProTag); // drei Jahresabschlüsse im Minus: Bankrott in der Jahresabrechnung, die Uhr steht schon auf Minute 3.420
            Pruefe.Wahr(arm.IstBankrott, "bankrott");
            Pruefe.Gleich(3L * Spielzeit.MinutenProTag, w.Bauen.LeerSeit(arm));
            Pruefe.Gleich(3L * Spielzeit.MinutenProTag, w.Bauen.Leerstand(arm)!.LeerSeit);
            Ticken(sim, 500);
            Pruefe.Gleich(3L * Spielzeit.MinutenProTag, w.Bauen.LeerSeit(arm)); // bleibt

            // Erlöschen (über Stilllegen wie im Familienbuch): ab dieser Minute.
            var erloschen = Haus(w, "Haus Erlöschen", stand: 1, stufe: 1, vermoegen: 0);
            erloschen.IstErloschen = true;
            w.Stilllegen(erloschen);
            Pruefe.Gleich(3L * Spielzeit.MinutenProTag + 500, w.Bauen.LeerSeit(erloschen));

            // Ohne Stilllegen (alter Spielstand, Tests): zählt ab der nächsten Minute, bis dahin „jetzt“.
            var still = Haus(w, "Haus Still", stand: 2, stufe: 2, vermoegen: 0);
            still.IstBankrott = true;
            Pruefe.Gleich(3L * Spielzeit.MinutenProTag + 500, w.Bauen.LeerSeit(still));
            Ticken(sim, 3);
            Pruefe.Gleich(3L * Spielzeit.MinutenProTag + 500, w.Bauen.LeerSeit(still));

            // Speichern und Laden; ein alter Spielstand ohne Zeitpunkt zählt ab dem Laden.
            var json = Speicherung.Speichern(sim);
            var w2 = Speicherung.Laden(json).Simulation.Wirtschaft!;
            Pruefe.Gleich(3L * Spielzeit.MinutenProTag, w2.Bauen.LeerSeit(w2.Konten.Single(x => x.Inhaber == "Haus Arm")));
            var alt = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
            foreach (var g in alt["Wirtschaft"]!["Bauen"]!["Grundstuecke"]!.AsArray()) g!.AsObject().Remove("LeerSeit");
            var (sim3, _) = Speicherung.Laden(alt.ToJsonString());
            var arm3 = sim3.Wirtschaft!.Konten.Single(x => x.Inhaber == "Haus Arm");
            Pruefe.Gleich(3L * Spielzeit.MinutenProTag + 503, sim3.Wirtschaft.Bauen.LeerSeit(arm3));
            Ticken(sim3, 5);
            Pruefe.Gleich(3L * Spielzeit.MinutenProTag + 503, sim3.Wirtschaft.Bauen.LeerSeit(arm3));

            // Gekauft: kein Leerstand mehr.
            Pruefe.Wahr(w.Ausfuehren(k, new LeerstandKaufen(still.Inhaber)).Erfolg, "kaufen");
            Pruefe.Gleich(null, w.Bauen.LeerSeit(still));
            Pruefe.Wahr(w.Bauen.Grundstuecke.All(g => g.LeerSeit is null || g.Besitzer is { IstAktiv: false } && g.Arbeit is null), "nur Leerstand hat einen Zeitpunkt");
        }),

        ("Bauen: Bauorte – zuerst die Bauplätze, dann die Rasterstellen; alle gültig, fest geordnet; wer nicht bauen darf, bekommt keine", () =>
        {
            var (sim, w) = MitKarte();
            var tot = Haus(w, "Haus Hütte", stand: 0, stufe: 0, vermoegen: 0);
            var k = Haus(w, "Haus Neu", stand: 3, stufe: 1, vermoegen: 50_000);
            var vorlage = new WohnhausBauen(3, 0, 0, 0);
            var vorher = w.Bauen.Bauorte(k, vorlage);
            Pruefe.Wahr(vorher.Count > 10, $"nur {vorher.Count} Bauorte");
            Pruefe.Wahr(vorher.All(o => w.Bauen.BauVerboten(k, o) is null), "alle gültig");
            Pruefe.Wahr(vorher.Contains(new WohnhausBauen(3, -22, 9, 2)), "an der Hauptstraße in Kernlage");
            Pruefe.Wahr(vorher.All(o => o.Z > 0), "nördlich der Hauptstraße ist keine Bauzone");
            Pruefe.Wahr(vorher.Zip(vorher.Skip(1)).All(p => p.First.Z - w.Bauen.Grundriss(p.First).HalbZ <= p.Second.Z - w.Bauen.Grundriss(p.Second).HalbZ),
                "von Norden nach Süden (nach der nordwestlichen Ecke)");

            // Ein Wohnhausplatz wird frei: Er steht vorn, eingepasst in seine Drehung.
            tot.IstBankrott = true;
            Pruefe.Wahr(w.Ausfuehren(k, new LeerstandAbreissen(tot.Inhaber)).Erfolg, "abreißen");
            Ticken(sim, 241);
            var nachher = w.Bauen.Bauorte(k, vorlage);
            Pruefe.Gleich<BauBefehl>(new WohnhausBauen(3, -30, -9, 0), nachher[0]);
            Pruefe.Gleich(vorher.Count + 1, nachher.Count);
            Pruefe.Wahr(w.Bauen.Bauorte(k, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0)).All(o => !w.Bauen.Grundriss(o).Ueberlappt(w.Bauen.Bauplaetze.Single().Grundriss)),
                "keine Werkstatt auf dem Wohnhausplatz");

            // Ein Gebäude belegt Bauorte; wer nicht bauen darf (Stand, Geld, schon ein Neubau), bekommt keine.
            Pruefe.Wahr(w.Ausfuehren(k, nachher[1]).Erfolg, "bauen");
            Pruefe.Gleich(0, w.Bauen.Bauorte(k, vorlage).Count); // baut schon
            var anderer = Haus(w, "Haus Zwei", stand: 3, stufe: 1, vermoegen: 50_000);
            Pruefe.Wahr(!w.Bauen.Bauorte(anderer, vorlage).Contains(nachher[1]), "die Baustelle ist belegt");
            Pruefe.Gleich(0, w.Bauen.Bauorte(Haus(w, "Haus Gemein", stand: 0, stufe: 0, vermoegen: 50_000), vorlage).Count);
            Pruefe.Gleich(0, w.Bauen.Bauorte(Haus(w, "Haus Arm", stand: 3, stufe: 1, vermoegen: 100), vorlage).Count);
            Pruefe.Gleich(0, NeueSimulation().Wirtschaft!.Bauen.Bauorte(k, vorlage).Count); // ohne Karte
        }),
    };
}
