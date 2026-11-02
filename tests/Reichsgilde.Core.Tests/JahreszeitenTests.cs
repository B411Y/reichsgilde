using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Mehrspieler;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Tests: Folgen der Jahreszeiten für die Wirtschaft (Abschnitt „jahreszeiten“, Datenversion 67; Nutzer 31.10.2026, Weg B) – Winter mit Nachfrage
/// und langsamer Arbeit im Freien, strenger Winter mit Eis auf dem Fluss (Hafen und Fähre zu, die Schiffe ruhen), Ernte im Herbst, Schlamm im
/// Frühling (auch im Zeitfaktor, nach dem das Bild fährt), Festmärkte, Meldungen; ohne Abschnitt bleibt alles wie bis Datenversion 66.
/// </summary>
static class JahreszeitenTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json.Value));
    static readonly Lazy<Wirtschaftsdaten> OhneJahreszeiten = new(() => Wirtschaftsdaten.AusJson(OhneAbschnitt(Json.Value, "jahreszeiten")));

    /// <summary>Die Minute seit Spielbeginn: Jahr ab 0 (= 1400), Uhrzeit.</summary>
    static long Minute(int jahr, int stunde, int minute = 0) => jahr * (long)Spielzeit.MinutenProTag + stunde * 60 + minute - Spielzeit.TagesbeginnMinute;

    /// <summary>
    /// Die echten Daten ohne Abschnitt „jahreszeiten“ – für Tests der Fahrzeit an sich (Weglänge, Steigung, Belag, alte Daten), die im Frühling 1400
    /// laufen und mit Zeitfaktor 1 auf flachem Fahrweg rechnen. Die Jahreszeit selbst prüft diese Datei.
    /// </summary>
    internal static Wirtschaftsdaten DatenOhneJahreszeiten() => Wirtschaftsdaten.AusJson(OhneAbschnitt(File.ReadAllText(Datenpfad.Wirtschaft()), "jahreszeiten"));

    static string OhneAbschnitt(string json, string abschnitt)
    {
        var knoten = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        knoten.Remove(abschnitt);
        return knoten.ToJsonString();
    }

    static Stadtwirtschaft Wirtschaft(int startwert, Wirtschaftsdaten? daten = null) =>
        new(daten ?? Daten.Value, Partieeinstellungen.Standard.StartStatut, startwert);

    /// <summary>Der erste Startwert, dessen Winter 1403 streng ist und die Bedingung erfüllt.</summary>
    static int StrengerStartwert(Func<Winterlage, bool>? bedingung = null)
    {
        var w = Wirtschaft(1);
        for (var s = 1; s < 5000; s++)
        {
            var probe = s == 1 ? w : Wirtschaft(s);
            if (probe.Jahreszeiten.WinterVon(1403) is { Streng: true } l && (bedingung?.Invoke(l) ?? true)) return s;
        }
        throw new Exception("Kein Startwert mit passendem strengen Winter gefunden.");
    }

    /// <summary>Eine Partie mit Karte (ohne Häuser) zu dieser Minute.</summary>
    static (Simulation Sim, Stadtwirtschaft W) Stadt(long minute, int startwert = 1400, string karte = "probestadt", Wirtschaftsdaten? daten = null)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert },
            new Spielzeit(minute), daten ?? Daten.Value);
        var w = sim.Wirtschaft!;
        w.KarteSetzen(Stadtkarte.AusDatei(Datenpfad.Karte(karte)));
        w.EinwohnerZaehlen();
        return (sim, w);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Jahreszeiten: Daten werden geprüft (Ware, Betriebsart, Belag, Stunden, Überschneidung der Festmärkte)", () =>
        {
            Pruefe.Wahr(Daten.Value.Jahreszeiten is not null && Daten.Value.Version >= 67, "Abschnitt ab Datenversion 67");
            string Kaputt(string alt, string neu)
            {
                Pruefe.Wahr(Json.Value.Contains(alt), $"„{alt}“ steht nicht in wirtschaft.json");
                try { Wirtschaftsdaten.AusJson(Json.Value.Replace(alt, neu)); return ""; }
                catch (WirtschaftsdatenFehler e) { return e.Message; }
            }
            Pruefe.Wahr(Kaputt("\"Holz\": 1.4", "\"Holzkohle\": 1.4").Contains("Holzkohle"), "unbekannte Ware");
            Pruefe.Wahr(Kaputt("\"Holzfaellerei\": 0.85", "\"Koehlerei\": 0.85").Contains("Koehlerei"), "unbekannte Betriebsart");
            Pruefe.Wahr(Kaputt("\"Trampelpfad\": 1.3", "\"Pflaster\": 1.3").Contains("befestigt"), "Schlamm auf Pflaster");
            Pruefe.Wahr(Kaputt("\"zufrierenVon\": 7", "\"zufrierenVon\": 2").Contains("zufrierenVon"), "Stunde vor Tagesbeginn");
            Pruefe.Wahr(Kaputt("\"jahreszeit\": \"Herbst\", \"von\": 10", "\"jahreszeit\": \"Fruehling\", \"von\": 12").Contains("überschneiden"), "zwei Märkte zugleich");
        }),

        ("Jahreszeiten: Der Winter eines Jahres ist fest aus Startwert und Jahr; etwa drei von zehn sind streng, Zufrieren 7–10 Uhr, 6–14 Stunden", () =>
        {
            var w = Wirtschaft(1400);
            Pruefe.Gleich(null, w.Jahreszeiten.WinterVon(1402));
            Pruefe.Gleich(w.Jahreszeiten.WinterVon(1403), Wirtschaft(1400).Jahreszeiten.WinterVon(1403));
            Pruefe.Gleich(null, Wirtschaft(1400, OhneJahreszeiten.Value).Jahreszeiten.WinterVon(1403));
            int winter = 0, streng = 0, bisEnde = 0;
            for (var s = 1; s <= 40; s++)
            {
                var probe = Wirtschaft(s * 37);
                for (var jahr = 1403; jahr < 1403 + 4 * 50; jahr += 4)
                {
                    var l = probe.Jahreszeiten.WinterVon(jahr)!;
                    winter++;
                    if (!l.Streng) continue;
                    streng++;
                    var stunde = (l.ZufrierenMinute + Spielzeit.TagesbeginnMinute) / 60;
                    Pruefe.Wahr(stunde is >= 7 and <= 10 && l.ZufrierenMinute % 60 == 0, $"Zufrieren um {stunde} Uhr");
                    var dauer = l.TauwetterMinute - l.ZufrierenMinute;
                    Pruefe.Wahr(l.BisJahresende ? l.TauwetterMinute == Spielzeit.MinutenProTag : dauer is >= 6 * 60 and <= 14 * 60, $"Dauer {dauer} min");
                    if (l.BisJahresende) bisEnde++;
                }
            }
            var anteil = (double)streng / winter;
            Pruefe.Wahr(anteil is > 0.26 and < 0.34, $"Anteil strenger Winter {anteil:0.000}");
            Pruefe.Wahr(bisEnde > 0 && bisEnde < streng, $"mal bis zum Jahresende zu, mal mit Eisgang ({bisEnde} von {streng})");
        }),

        ("Strenger Winter: Meldung zum Jahresbeginn, „zugefroren“ und „Eisgang“, Hafen geschlossen, die Schiffe ruhen und kommen um die Eiszeit später heim", () =>
        {
            var s = StrengerStartwert(l => l.ZufrierenMinute <= 7 * 60 - Spielzeit.TagesbeginnMinute && !l.BisJahresende);
            var (sim, w) = Stadt(Minute(3, 4), s);
            var lage = w.Jahreszeiten.DiesesJahr!;
            var meldungen = new List<string>();
            w.Meldung += meldungen.Add;
            var k = w.KontoEroeffnen("Haus Eisgang", startvermoegen: 20_000);
            Pruefe.Wahr(w.Ausfuehren(k, new SchiffKaufen()).Erfolg, "Schiff gekauft");
            var schiff = w.Schiffahrt.SchiffeVon(k).Single();
            sim.Tick(); // 04:00 – die Meldung zur Jahreszeit
            Pruefe.Wahr(meldungen.Any(m => m.StartsWith("Winter 1403") && m.Contains("strenger Winter") && m.Contains("Sandfluss")), string.Join(" | ", meldungen));
            Pruefe.Wahr(w.Jahreszeiten.EisDroht && !w.Jahreszeiten.FlussZugefroren, "Eis droht, noch offen");
            var auftrag = w.Ausfuehren(k, new SchiffsauftragErteilen(schiff.Name, "Eisenfurt", new Dictionary<string, int>(), new Dictionary<string, int> { ["Holz"] = 20 }));
            Pruefe.Wahr(auftrag.Erfolg, auftrag.Meldung);
            while (sim.Zeit.MinuteImTag < lage.ZufrierenMinute) sim.Tick();
            Pruefe.Wahr(schiff.Zustand != SchiffZustand.AmKai, $"Schiff unterwegs ({schiff.Zustand})");
            var (zustand, ende) = (schiff.Zustand, schiff.Ende);
            sim.Tick(); // die erste Minute im Eis
            Pruefe.Wahr(w.Jahreszeiten.FlussZugefroren, "zugefroren");
            Pruefe.Wahr(meldungen.Any(m => m.StartsWith("Der Sandfluss ist zugefroren! Hafen und Fähre sind geschlossen, 1 Schiff ruht bis zum Eisgang")), string.Join(" | ", meldungen));
            Pruefe.Wahr(w.Ausfuehren(k, new SchiffKaufen()).Erfolg, "zweites Schiff, am Kai");
            var amKai = w.Schiffahrt.SchiffeVon(k).Single(x => x.Zustand == SchiffZustand.AmKai);
            Pruefe.Wahr(w.Schiffahrt.AuftragVerboten(k, amKai.Name, new Schiffsauftrag("Eisenfurt", new Dictionary<string, int>(), new Dictionary<string, int> { ["Holz"] = 1 }))
                            is { } grund && grund.Contains("zugefroren"), "kein Auftrag im Eis");
            while (sim.Zeit.MinuteImTag < lage.TauwetterMinute) sim.Tick();
            Pruefe.Gleich(zustand, schiff.Zustand);
            Pruefe.Gleich(ende + (lage.TauwetterMinute - lage.ZufrierenMinute), schiff.Ende);
            sim.Tick();
            Pruefe.Wahr(!w.Jahreszeiten.FlussZugefroren && meldungen.Any(m => m.StartsWith("Eisgang auf dem Sandfluss")), "Eisgang");
            Pruefe.Gleich(ende + (lage.TauwetterMinute - lage.ZufrierenMinute), schiff.Ende);
        }),

        ("Strenger Winter: Die Fähre kostet im Eis die Wartezeit (Kern und Bild lesen denselben Wasserhalt); vorher und danach wie sonst", () =>
        {
            var s = StrengerStartwert(l => !l.BisJahresende);
            var (sim, w) = Stadt(Minute(3, 4), s);
            var lage = w.Jahreszeiten.DiesesJahr!;
            Pruefe.Wahr(w.Wasserhalt is { FaehreMinuten: 30 }, "Probestadt hat eine Fähre, 30 Minuten");
            while (sim.Zeit.MinuteImTag <= lage.ZufrierenMinute) sim.Tick();
            Pruefe.Gleich(120, w.Wasserhalt!.FaehreMinuten);
            Pruefe.Gleich(Daten.Value.Logistik.Wasser!.FurtMinuten, w.Wasserhalt!.FurtMinuten);
            while (sim.Zeit.MinuteImTag <= lage.TauwetterMinute) sim.Tick();
            Pruefe.Gleich(30, w.Wasserhalt!.FaehreMinuten);
        }),

        ("Winter: Brennholz, Kerzen, Schnaps gefragt (im strengen Winter mehr), Holzschlag und Steinbruch langsamer; Sommer wie in den Daten", () =>
        {
            var streng = StrengerStartwert(); // 1403 streng; einen milden Winter suchen wir unten
            var (_, sommer) = Stadt(Minute(1, 12));
            var (_, winter) = Stadt(Minute(3, 12), streng);
            Pruefe.Gleich(1.0, sommer.Markt["Holz"].Jahreszeitfaktor);
            Pruefe.Ungefaehr(1.6, winter.Markt["Holz"].Jahreszeitfaktor, 1e-12); // 1 + 0,4 × 1,5
            Pruefe.Ungefaehr(1.375, winter.Markt["Kerze"].Jahreszeitfaktor, 1e-12);
            Pruefe.Gleich(1.0, winter.Markt["Eisen"].Jahreszeitfaktor);
            Pruefe.Wahr(winter.Markt["Holz"].Preis > sommer.Markt["Holz"].Preis * 1.2, $"Holz im Winter teurer ({winter.Markt["Holz"].Preis:0.00} gegen {sommer.Markt["Holz"].Preis:0.00})");
            var w = Wirtschaft(1);
            for (var jahr = 1403; ; jahr += 4)
                if (w.Jahreszeiten.WinterVon(jahr) is { Streng: false })
                {
                    Pruefe.Ungefaehr(1.4, w.Jahreszeiten.NachfrageFaktor("Holz", Jahreszeit.Winter, false, null), 1e-12);
                    break;
                }
            Pruefe.Gleich(0.85, winter.Jahreszeiten.Arbeitsfaktor(Betriebsart.Holzfaellerei, Jahreszeit.Winter));
            Pruefe.Gleich(0.8, winter.Jahreszeiten.Arbeitsfaktor(Betriebsart.Steinbruch, Jahreszeit.Winter));
            Pruefe.Gleich(1.0, winter.Jahreszeiten.Arbeitsfaktor(Betriebsart.Mine, Jahreszeit.Winter));
            Pruefe.Gleich(1.0, winter.Jahreszeiten.Arbeitsfaktor(Betriebsart.Giesserei, Jahreszeit.Winter));
        }),

        ("Herbst: Die Ernte bringt Gerste, Roggen, Kräuter billiger auf den Markt, Höfe und Kräutergärten bringen mehr – auch in der Rechnung der Kalkulation", () =>
        {
            var (_, sommer) = Stadt(Minute(1, 18));
            var (_, herbst) = Stadt(Minute(2, 18)); // nach dem Herbstmarkt (10–16 Uhr)
            Pruefe.Ungefaehr(sommer.Markt["Gerste"].Grundversorgung * 1.25, herbst.Markt["Gerste"].Grundversorgung, 1e-9);
            Pruefe.Wahr(herbst.Markt["Gerste"].Preis < sommer.Markt["Gerste"].Preis * 0.92, "Gerste im Herbst billiger");
            Pruefe.Gleich(sommer.Markt["Eisen"].Preis, herbst.Markt["Eisen"].Preis);
            var k = herbst.KontoEroeffnen("Haus Ernte");
            var hof = herbst.BetriebEroeffnen("Hof am Bach", Betriebsart.Bauernhof, k);
            herbst.Einstellen(hof, "Altknecht", 2);
            var vorher = Kalkulation.Leistung(herbst, hof);
            herbst.Uhr = new Spielzeit(Minute(1, 18)); // derselbe Hof im Sommer
            Pruefe.Ungefaehr(vorher / 1.2, Kalkulation.Leistung(herbst, hof), 1e-12);
        }),

        ("Herbst und Winter: Ein Bauernhof bringt in der Simulation im Herbst mehr und im Winter weniger als im Sommer", () =>
        {
            double Ertrag(int jahr)
            {
                var (sim, w) = Stadt(Minute(jahr, 8), daten: Daten.Value);
                var k = w.KontoEroeffnen("Haus Hof");
                var hof = w.BetriebEroeffnen("Hof", Betriebsart.Bauernhof, k, startkarren: "");
                w.Einstellen(hof, "Altknecht", 1);
                for (var i = 0; i < 180; i++) sim.Tick(); // unter der Lagergrenze bleiben
                return hof.ImLager("Gerste") + hof.ImLager("Roggen");
            }
            var (sommer, herbst, winter) = (Ertrag(1), Ertrag(2), Ertrag(3));
            Pruefe.Wahr(herbst > sommer && winter < sommer, $"Sommer {sommer}, Herbst {herbst}, Winter {winter}");
        }),

        ("Frühling: Schlamm macht Trampelpfad und Fahrweg langsamer (Zeitfaktor, den auch das Bild nimmt), Kies und Pflaster nicht; Fahrzeit wird neu gerechnet", () =>
        {
            var daten = Daten.Value;
            var karte = Stadtkarte.AusDatei(Datenpfad.Karte("greybox"));
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, daten,
                new NeuePartie(Beruf: "Schmied"), karte: karte);
            var w = sim.Wirtschaft!;
            var betrieb = w.Betriebe.First(b => b.Konto == spieler);
            var (hin, _) = w.Fahrten(betrieb)!.Value;
            Pruefe.Gleich(Jahreszeit.Fruehling, w.Uhr.Jahreszeit);
            var fruehling = hin.Select(w.Zeitfaktor).ToList();
            var fahrzeitFruehling = w.Fahrzeit(betrieb, "Handkarren", true);
            w.Uhr = new Spielzeit(Minute(1, 4)); // ohne FahrzeitenVeraltet: die gemerkten Zeiten gelten nur für ihre Jahreszeit
            var sommer = hin.Select(w.Zeitfaktor).ToList();
            var fahrzeitSommer = w.Fahrzeit(betrieb, "Handkarren", true);
            var erde = 0;
            for (var i = 0; i < hin.Count; i++)
            {
                var s = hin[i];
                var stufe = s.Art == Fahrbahn.Strasse && s.Weg >= 0 ? w.Wegebau.BelagBei(s.Weg, s.Stelle) : (Belagstufe?)null;
                var erwartet = stufe switch { Belagstufe.Trampelpfad => 1.3, Belagstufe.Fahrweg => 1.2, _ => 1.0 };
                if (erwartet > 1) erde++;
                Pruefe.Ungefaehr(sommer[i] * erwartet, fruehling[i], 1e-12);
            }
            Pruefe.Wahr(erde > 0, "der Weg führt auch über Erde");
            Pruefe.Wahr(fahrzeitFruehling > fahrzeitSommer, $"Frühling {fahrzeitFruehling} min, Sommer {fahrzeitSommer} min");
            Pruefe.Gleich(1.0, w.Jahreszeiten.Schlamm(Belagstufe.Pflaster));
        }),

        ("Frühling: Die feste Fahrzeit aus dem Umland (ohne Strecke) dauert im Schlamm länger, aus der Werkstatt nicht", () =>
        {
            var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, new Spielzeit(Minute(0, 8)), Daten.Value);
            var w = sim.Wirtschaft!; // ohne Karte: feste Fahrzeiten
            var k = w.KontoEroeffnen("Haus Umland");
            var grube = w.BetriebEroeffnen("Grube", Betriebsart.Mine, k);
            var giesserei = w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, k);
            Pruefe.Gleich(138, w.Fahrzeit(grube.Karren[0], zumMarkt: true)); // 120 × 1,15
            Pruefe.Gleich(30, w.Fahrzeit(giesserei.Karren[0], zumMarkt: true));
            w.Uhr = new Spielzeit(Minute(1, 8));
            Pruefe.Gleich(120, w.Fahrzeit(grube.Karren[0], zumMarkt: true));
        }),

        ("Festmärkte: Im Frühling und Herbst von 10 bis 16 Uhr mehr Nachfrage und Gäste, Meldung zu Beginn und Ende; im Sommer keiner", () =>
        {
            var (sim, w) = Stadt(Minute(2, 9, 58));
            var meldungen = new List<string>();
            w.Meldung += meldungen.Add;
            var preis = w.Markt["Werkzeug"].Preis;
            Pruefe.Gleich(null, w.Jahreszeiten.FestmarktJetzt);
            while (sim.Zeit.UhrzeitInMinuten <= 10 * 60) sim.Tick();
            Pruefe.Gleich("Herbstmarkt", w.Jahreszeiten.FestmarktJetzt?.Name);
            Pruefe.Ungefaehr(1.2, w.Markt["Werkzeug"].Jahreszeitfaktor, 1e-12);
            Pruefe.Ungefaehr(1.6, w.Jahreszeiten.Besucherfaktor, 1e-12);
            Pruefe.Wahr(w.Markt["Werkzeug"].Preis > preis * 1.05, "Preis steigt");
            Pruefe.Wahr(meldungen.Any(m => m.StartsWith("Festmarkt: Herbstmarkt bis 16 Uhr")), string.Join(" | ", meldungen));
            Pruefe.Ungefaehr(1.3, w.Jahreszeiten.Gaestefaktor(new Spielzeit(Minute(2, 11))), 1e-12); // die Stunde 10–11 Uhr
            Pruefe.Gleich(1.0, w.Jahreszeiten.Gaestefaktor(new Spielzeit(Minute(2, 10)))); // die Stunde 9–10 Uhr
            while (sim.Zeit.UhrzeitInMinuten <= 16 * 60) sim.Tick();
            Pruefe.Gleich(null, w.Jahreszeiten.FestmarktJetzt);
            Pruefe.Gleich(1.0, w.Markt["Werkzeug"].Jahreszeitfaktor);
            Pruefe.Wahr(meldungen.Any(m => m == "Festmarkt: Herbstmarkt ist vorbei."), "Ende gemeldet");
            var (_, sommer) = Stadt(Minute(1, 12));
            Pruefe.Gleich(null, sommer.Jahreszeiten.FestmarktJetzt);
            Pruefe.Gleich(1.0, sommer.Jahreszeiten.Besucherfaktor);
        }),

        ("Jahreszeiten: Zum Jahresbeginn sagt eine Meldung, was die Jahreszeit bringt", () =>
        {
            var (sim, w) = Stadt(Minute(0, 4));
            var meldungen = new List<string>();
            w.Meldung += meldungen.Add;
            sim.Tick();
            var fruehling = meldungen.Single(m => m.StartsWith("Frühling 1400"));
            Pruefe.Wahr(fruehling.Contains("Erdwege sind aufgeweicht") && fruehling.Contains("Frühjahrsmarkt von 10 bis 16 Uhr"), fruehling);
            var (sim2, w2) = Stadt(Minute(2, 4));
            meldungen.Clear();
            w2.Meldung += meldungen.Add;
            sim2.Tick();
            var herbst = meldungen.Single(m => m.StartsWith("Herbst 1402"));
            Pruefe.Wahr(herbst.Contains("die Ernte bringt Gerste, Roggen und Kräuter auf den Markt") && herbst.Contains("Bauernhof und Kräutergarten bringen mehr"), herbst);
        }),

        ("Jahreszeiten: Ohne Abschnitt wirkt nichts – Faktoren 1, kein Eis, keine Meldung, feste Fahrzeit wie bisher", () =>
        {
            var (sim, w) = Stadt(Minute(3, 4), StrengerStartwert(), daten: OhneJahreszeiten.Value);
            var meldungen = new List<string>();
            w.Meldung += meldungen.Add;
            for (var i = 0; i < 12 * 60; i++) sim.Tick();
            Pruefe.Wahr(!w.Jahreszeiten.IstAktiv && !w.Jahreszeiten.FlussZugefroren && w.Jahreszeiten.DiesesJahr is null, "aus");
            Pruefe.Wahr(w.Markt.Staende.All(s => s.Jahreszeitfaktor == 1 && s.Angebotsfaktor == 1), "Markt wie in den Daten");
            Pruefe.Wahr(!meldungen.Any(m => m.Contains("Winter 1403") || m.Contains("zugefroren")), string.Join(" | ", meldungen));
            Pruefe.Gleich(30, w.Wasserhalt!.FaehreMinuten);
        }),

        ("Strenger Winter: Speichern und Laden mitten im Eis – die Partie läuft genau so weiter (nichts davon steht im Spielstand)", () =>
        {
            var s = StrengerStartwert(l => !l.BisJahresende);
            var karte = Stadtkarte.AusDatei(Datenpfad.Karte("probestadt"));
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = s },
                Daten.Value, new NeuePartie(Startvermoegen: 20_000, Rivalen: 2), new Spielzeit(Minute(3, 4)), karte);
            var w = sim.Wirtschaft!;
            var lage = w.Jahreszeiten.DiesesJahr!;
            w.Ausfuehren(spieler, new SchiffKaufen());
            var schiff = w.Schiffahrt.SchiffeVon(spieler).Single();
            sim.Tick();
            w.Ausfuehren(spieler, new SchiffsauftragErteilen(schiff.Name, "Eisenfurt", new Dictionary<string, int>(), new Dictionary<string, int> { ["Holz"] = 20 }, Wiederholen: true));
            while (sim.Zeit.MinuteImTag < lage.ZufrierenMinute + 30) sim.Tick();
            Pruefe.Wahr(w.Jahreszeiten.FlussZugefroren, "im Eis");
            var (geladen, _) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            Pruefe.Wahr(geladen.Wirtschaft!.Jahreszeiten.FlussZugefroren, "nach dem Laden im Eis");
            for (var i = 0; i < 16 * 60; i++) { sim.Tick(); geladen.Tick(); }
            Pruefe.Gleich(Zustandspruefsumme.Berechnen(sim), Zustandspruefsumme.Berechnen(geladen));
        }),

        ("Alter Spielstand (Daten ohne Abschnitt jahreszeiten) lädt und bleibt ohne Jahreszeiten – er bringt seine Daten mit", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                OhneJahreszeiten.Value, new NeuePartie(), new Spielzeit(Minute(3, 9)), Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));
            var (geladen, _) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            Pruefe.Wahr(!geladen.Wirtschaft!.Jahreszeiten.IstAktiv, "ohne Jahreszeiten");
            Pruefe.Gleich(1.0, geladen.Wirtschaft!.Markt["Holz"].Jahreszeitfaktor);
        }),
    };
}
