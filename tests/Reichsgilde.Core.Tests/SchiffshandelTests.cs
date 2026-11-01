using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Tests für den Schiffshandel (PR #199): Häfen und Routen aus der Karte, Kauf, Fahrt, Zoll, Flusspiraten, Märkte der fernen Städte mit
/// Erholung, Lager eines Betriebs, wiederkehrende Aufträge, Rechnung, Lage für die Darstellung, Speichern und Laden.
/// Gespielt wird in der Probestadt (Hafen am Sandfluss).
/// </summary>
static class SchiffshandelTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json.Value));

    const string Piratenzeile = "\"piraten\": {\"name\": \"Flusspiraten\", \"chance\": 0.05, \"staerke\": 1.0, \"beuteAnteil\": 0.5}";

    /// <summary>Die Daten mit anderen Piraten (Chance, Stärke).</summary>
    static Wirtschaftsdaten Piraten(double chance, double staerke = 1)
    {
        Pruefe.Wahr(Json.Value.Contains(Piratenzeile), "Die Piraten stehen nicht wie erwartet in wirtschaft.json");
        return Wirtschaftsdaten.AusJson(Json.Value.Replace(Piratenzeile,
            $"\"piraten\": {{\"name\": \"Flusspiraten\", \"chance\": {chance.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
            $"\"staerke\": {staerke.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"beuteAnteil\": 0.5}}"));
    }

    static Wirtschaftsdaten OhnePiraten => Piraten(0);

    static Stadtkarte Karte(string kennung = "probestadt") => Stadtkarte.AusDatei(Datenpfad.Karte(kennung));

    /// <summary>Eine Stadt mit Karte um 9 Uhr im ersten Jahr und ein Haus ohne Familie (Talente 0) mit Geld.</summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto K) Stadt(Wirtschaftsdaten? daten = null, double geld = 20_000, string karte = "probestadt")
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
            new Spielzeit(9 * 60 - Spielzeit.TagesbeginnMinute), daten ?? OhnePiraten);
        var w = sim.Wirtschaft!;
        w.KarteSetzen(Karte(karte));
        var k = w.KontoEroeffnen("Haus Reeder", startvermoegen: geld);
        return (sim, w, k);
    }

    static Dictionary<string, int> D(params (string Ware, int Stueck)[] x) => x.ToDictionary(y => y.Ware, y => y.Stueck);
    static readonly Dictionary<string, int> Nichts = new();

    static string Text(IReadOnlyDictionary<string, int> x) => string.Join(", ", x.OrderBy(y => y.Key, StringComparer.Ordinal).Select(y => $"{y.Key}={y.Value}"));

    /// <summary>Tickt, bis die Bedingung gilt (höchstens so viele Minuten).</summary>
    static void Bis(Simulation sim, Func<bool> fertig, int hoechstens = 3000)
    {
        for (var i = 0; i < hoechstens && !fertig(); i++) sim.Tick();
        Pruefe.Wahr(fertig(), $"nach {hoechstens} Minuten nicht erreicht");
    }

    static Schiff Kaufen(Stadtwirtschaft w, Konto k)
    {
        var e = w.Ausfuehren(k, new SchiffKaufen());
        Pruefe.Wahr(e.Erfolg, e.Meldung);
        return w.Schiffahrt.SchiffeVon(k).Last();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Daten: Datenversion 58, ein Flussschiff, zwei ferne Städte (flussauf und flussab); Fehler fallen auf", () =>
        {
            var d = Daten.Value.Schiffshandel!;
            Pruefe.Gleich(66, Daten.Value.Version);
            Pruefe.Gleich(1, d.Schiffe.Count);
            Pruefe.Gleich("flussauf, flussab", string.Join(", ", d.Staedte.Select(s => s.Richtung)));
            Pruefe.Gleich(120, d.Schiffe["Flussschiff"].Gesamtkapazitaet);
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"richtung\": \"flussab\"", "\"richtung\": \"seitwärts\"")), "richtung");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"Gold\": {\"faktor\"", "\"Goldstaub\": {\"faktor\"")), "Goldstaub");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"zollJeStelle\": 0.025", "\"zollJeStelle\": 1.5")), "zollJeStelle");
        }),

        ("Ohne Hafen kein Schiffshandel: ohne Karte, Greybox und Messstadt ohne Fluss – Kauf abgelehnt; ohne Abschnitt auch in der Probestadt", () =>
        {
            var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Landratte", startvermoegen: 20_000);
            Pruefe.Wahr(!w.Schiffahrt.IstAktiv, "ohne Karte");
            Pruefe.Wahr(!w.Ausfuehren(k, new SchiffKaufen()).Erfolg, "ohne Karte kein Kauf");
            foreach (var karte in new[] { "greybox", "messstadt" })
            {
                var (_, w2, k2) = Stadt(karte: karte);
                if (w2.Karte!.Wasser.Any(g => g.Art == "fluss")) continue; // die Greybox bekommt einen Fluss mit Hafen (PR #199)
                Pruefe.Wahr(!w2.Schiffahrt.IstAktiv, karte);
                Pruefe.Gleich("Die Stadt hat keinen Hafen an einem Fluss.", w2.Ausfuehren(k2, new SchiffKaufen()).Meldung);
            }
            var knoten = JsonNode.Parse(Json.Value)!.AsObject();
            knoten.Remove("schiffshandel");
            var (_, w3, k3) = Stadt(Wirtschaftsdaten.AusJson(knoten.ToJsonString()));
            Pruefe.Wahr(!w3.Schiffahrt.IstAktiv, "ohne Abschnitt");
            Pruefe.Gleich("In dieser Stadt gibt es keinen Schiffshandel.", w3.Ausfuehren(k3, new SchiffKaufen()).Meldung);
        }),

        ("Nur der Hafenplatz ist ein Hafen: Zollstelle und Hafenkran stehen auch am Ufer, zählen aber nicht", () =>
        {
            var pfad = Datenpfad.Karte("probestadt");
            var hafenzeile = "{\"kennung\": \"Hafen1\", \"katalog\": \"hafenplatz\", \"name\": \"Hafen am Sandfluss\", \"punkt\": [-90, 108], \"drehung\": 180}";
            var json = File.ReadAllText(pfad);
            Pruefe.Wahr(json.Contains(hafenzeile), "Hafen1 steht nicht wie erwartet in probestadt.json");
            json = json.Replace(hafenzeile, hafenzeile +
                ",\n  {\"kennung\": \"Zoll1\", \"katalog\": \"zollstelle\", \"name\": \"Zollstelle\", \"punkt\": [-60, 108], \"drehung\": 180}" +
                ",\n  {\"kennung\": \"Kran1\", \"katalog\": \"hafenkran\", \"name\": \"Tretradkran\", \"punkt\": [-120, 108], \"drehung\": 180}");
            var karte = Stadtkarte.AusJson(json, Gebaeudekatalog.AusDatei(Stadtkarte.KatalogZu(pfad)));
            Pruefe.Wahr(karte.Gebaeude.Count(g => g.Baustein?.AmUfer == true) >= 3, "Zollstelle und Kran stehen am Ufer");
            var w = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                new Spielzeit(9 * 60 - Spielzeit.TagesbeginnMinute), OhnePiraten).Wirtschaft!;
            w.KarteSetzen(karte);
            Pruefe.Gleich("Hafen1", string.Join(", ", w.Schiffahrt.Haefen.Select(h => h.Kennung)));
        }),

        ("Hafen und Routen der Probestadt: Liegeplatz im Wasser vor der Kaimauer, flussab bis zum Ende des Sandflusses im Westen, flussauf bis zum Osten", () =>
        {
            var (_, w, _) = Stadt();
            var s = w.Schiffahrt;
            Pruefe.Wahr(s.IstAktiv, "Probestadt hat einen Hafen");
            var hafen = s.Haefen.Single();
            Pruefe.Gleich(("Hafen1", "Sandfluss"), (hafen.Kennung, hafen.Fluss));
            Pruefe.Wahr(w.Karte!.Gelaendeform.Wasser.IstWasser(hafen.Liegeplatz.X, hafen.Liegeplatz.Z), $"Liegeplatz {hafen.Liegeplatz} liegt im Wasser");
            var ab = s.Route("Hafen1", flussauf: false);
            var auf = s.Route("Hafen1", flussauf: true);
            Pruefe.Gleich(hafen.Liegeplatz, ab[0]);
            Pruefe.Gleich(hafen.Liegeplatz, auf[0]);
            Pruefe.Wahr(ab[^1].Abstand(new(-380, 300)) < 1, $"flussab endet bei (-380 | 300), nicht {ab[^1]}");
            Pruefe.Wahr(auf[^1].Abstand(new(495, 195)) < 1, $"flussauf endet bei (495 | 195), nicht {auf[^1]}");
            for (var i = 2; i < ab.Count; i++) Pruefe.Wahr(ab[i].Abstand(ab[i - 1]) <= Schiffahrt.RoutenSchritt + 1.5, "Punkte dicht genug");
            Pruefe.Wahr(s.RoutenLaenge("Hafen1", true) > 500 && s.RoutenLaenge("Hafen1", false) > 300, "Länge in Metern");
            Pruefe.Gleich(0, s.Route("Hafen9", true).Count);
        }),

        ("Greybox (seit PR #199): Hafen am Grenzfluss, Routen flussauf nach Westen und flussab nach Osten bis hinter den Kartenrand", () =>
        {
            var (_, w, _) = Stadt(karte: "greybox");
            var s = w.Schiffahrt;
            Pruefe.Wahr(s.IstAktiv, "Greybox hat einen Hafen");
            var hafen = s.Haefen.Single();
            Pruefe.Gleich("Grenzfluss", hafen.Fluss);
            Pruefe.Wahr(hafen.Liegeplatz.Abstand(new(38, 140)) < 20, $"Liegeplatz {hafen.Liegeplatz} vor dem Hafen bei (38 | 140)");
            Pruefe.Wahr(w.Karte!.Gelaendeform.Wasser.IstWasser(hafen.Liegeplatz.X, hafen.Liegeplatz.Z), "Liegeplatz im Wasser");
            var feld = w.Karte.Kartengrenzen.Spielfeld;
            var ab = s.Route(hafen.Kennung, flussauf: false);
            var auf = s.Route(hafen.Kennung, flussauf: true);
            Pruefe.Wahr(ab[^1].X > feld.X1 + 100 && auf[^1].X < feld.X0 - 100, $"Routen enden hinter dem Rand: {auf[^1]} … {ab[^1]}");
        }),

        ("Kauf: Investition und Name; zu wenig Geld abgelehnt; Verkauf am Kai bringt die Hälfte zurück", () =>
        {
            var (_, w, k) = Stadt(geld: 2_000);
            var preis = Daten.Value.Schiffshandel!.Schiffe["Flussschiff"].Preis;
            Pruefe.Wahr(w.Schiffahrt.KaufVerboten(k)!.Contains("es fehlen"), "zu wenig Geld");
            Pruefe.Wahr(!w.Ausfuehren(k, new SchiffKaufen()).Erfolg, "abgelehnt");
            var (_, w2, k2) = Stadt();
            var s = Kaufen(w2, k2);
            Pruefe.Gleich("Sankt Nikolaus", s.Name);
            Pruefe.Gleich((SchiffZustand.AmKai, "Hafen1"), (s.Zustand, s.Hafen));
            Pruefe.Ungefaehr(20_000 - preis, k2.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(preis, k2.Laufend.Investitionen, 1e-9);
            Pruefe.Gleich("Gute Hoffnung", Kaufen(w2, k2).Name);
            Pruefe.Wahr(w2.Ausfuehren(k2, new SchiffVerkaufen("Sankt Nikolaus")).Erfolg, "Verkauf");
            Pruefe.Ungefaehr(preis * 0.5, k2.Laufend.Verkaeufe, 1e-9);
            Pruefe.Gleich(1, w2.Schiffahrt.SchiffeVon(k2).Count());
            var fremd = w2.KontoEroeffnen("Haus Fremd", startvermoegen: 1_000);
            Pruefe.Wahr(w2.Ausfuehren(fremd, new SchiffVerkaufen("Gute Hoffnung")).Meldung.Contains("gehört nicht"), "fremdes Schiff");
        }),

        ("Fahrt: Beladen 22 Min., flussauf 134 Min. hin, 34 Min. Liegezeit, 80 Min. zurück (Datenversion 57: × 1140/1020); verkauft dort, kauft Eisen, verkauft es daheim am Markt", () =>
        {
            var (sim, w, k) = Stadt();
            var s = Kaufen(w, k);
            var typ = s.Daten;
            var eisenfurt = w.Schiffahrt.Stadt("Eisenfurt")!;
            Pruefe.Gleich((134, 80), (Schiffahrt.Fahrzeit(typ, eisenfurt, true), Schiffahrt.Fahrzeit(typ, eisenfurt, false)));
            Pruefe.Gleich(22 + 134 + 34 + 80, Schiffahrt.Rundreise(typ, eisenfurt));
            Pruefe.Ungefaehr(242.0 * Spielzeit.MinutenProTag / 1020, Schiffahrt.Rundreise(typ, eisenfurt), 0.002); // je Jahr gleich viele Fahrten wie früher
            var e = w.Ausfuehren(k, new SchiffsauftragErteilen(s.Name, "Eisenfurt", D(("Körnermahlzeit", 20)), D(("Eisen", 30))));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(SchiffZustand.Beladen, s.Zustand);
            Pruefe.Gleich(20, s.Ladung["Körnermahlzeit"]);
            Pruefe.Wahr(k.Laufend.Zutaten > 0, "Hinfracht am Markt gekauft");
            var start = sim.Zeit.VergangeneMinuten;
            Pruefe.Gleich(start + 22 + 134 + 34 + 80, w.Schiffahrt.ZurueckUm(s));
            Bis(sim, () => s.Zustand == SchiffZustand.Hinfahrt);
            Pruefe.Gleich(start + 22, sim.Zeit.VergangeneMinuten - 1);
            Bis(sim, () => s.Zustand == SchiffZustand.InFernerStadt);
            Pruefe.Gleich(start + 156, sim.Zeit.VergangeneMinuten - 1);
            Pruefe.Gleich("Eisen=30", Text(s.Ladung));
            Pruefe.Wahr(k.Laufend.Umsatz > 0, "Körnermahlzeit dort verkauft");
            Pruefe.Wahr(eisenfurt.Druck("Körnermahlzeit") > 19 && eisenfurt.Druck("Eisen") > 29, "Druck auf den fernen Markt");
            Bis(sim, () => s.Zustand == SchiffZustand.Rueckfahrt);
            var vorher = k.Laufend.Umsatz;
            Bis(sim, () => s.Zustand == SchiffZustand.AmKai);
            Pruefe.Gleich(start + 270, sim.Zeit.VergangeneMinuten - 1);
            Pruefe.Wahr(k.Laufend.Umsatz > vorher, "Eisen daheim verkauft");
            Pruefe.Gleich(0, s.Geladen);
            Pruefe.Gleich((1, (Schiffsauftrag?)null), (s.Fahrten, s.Auftrag));
        }),

        ("Auftrag: geprüft wird Zustand, Ziel, Waren der Stadt, Laderäume (4 × 30), Lager, Geld; unterwegs kein neuer Auftrag", () =>
        {
            var (_, w, k) = Stadt();
            var s = Kaufen(w, k);
            string? Grund(Schiffsauftrag a) => w.Schiffahrt.AuftragVerboten(k, s.Name, a);
            Pruefe.Wahr(Grund(new("Atlantis", D(("Eisen", 1)), Nichts))!.Contains("Atlantis"), "Ziel");
            Pruefe.Wahr(Grund(new("Eisenfurt", D(("Dolch", 10)), Nichts))!.Contains("kauft kein Dolch"), "Hinfracht, die die Stadt nicht kauft");
            Pruefe.Wahr(Grund(new("Eisenfurt", Nichts, D(("Gold", 10))))!.Contains("kein Gold"), "Rückfracht, die es dort nicht gibt");
            Pruefe.Wahr(Grund(new("Eisenfurt", Nichts, D(("Eisen", 121))))!.Contains("Zu viel Rückfracht"), "Laderäume");
            Pruefe.Wahr(Grund(new("Eisenfurt", Nichts, D(("Eisen", 60), ("Holz", 31), ("Granit", 30))))!.Contains("Zu viel"), "je Laderaum eine Ware");
            Pruefe.Gleich(null, Grund(new("Eisenfurt", Nichts, D(("Eisen", 60), ("Holz", 30), ("Granit", 30)))));
            Pruefe.Gleich("Der Auftrag ist leer.", Grund(new("Eisenfurt", Nichts, Nichts)));
            Pruefe.Wahr(Grund(new("Eisenfurt", D(("Kerze", 10)), Nichts, Betrieb: "Nirgendwo"))!.Contains("Nirgendwo"), "Betrieb");
            var (_, wArm, kArm) = Stadt(geld: 3_100);
            var sArm = Kaufen(wArm, kArm);
            Pruefe.Wahr(wArm.Schiffahrt.AuftragVerboten(kArm, sArm.Name, new("Weidenmünd", D(("Goldkette", 30)), Nichts))!.Contains("so viel habt ihr nicht"), "Geld");
            Pruefe.Wahr(w.Ausfuehren(k, new SchiffsauftragErteilen(s.Name, "Eisenfurt", Nichts, D(("Eisen", 30)))).Erfolg, "erster Auftrag");
            Pruefe.Wahr(w.Ausfuehren(k, new SchiffsauftragErteilen(s.Name, "Eisenfurt", Nichts, D(("Eisen", 30)))).Meldung.Contains("unterwegs"), "zweiter");
            Pruefe.Wahr(w.Ausfuehren(k, new SchiffVerkaufen(s.Name)).Meldung.Contains("unterwegs"), "Verkauf unterwegs");
        }),

        ("Zoll: je beladener Fahrt Zollstellen × 2,5 % vom Marktwert der Ladung; leere Fahrt zollfrei", () =>
        {
            var (sim, w, k) = Stadt();
            var s = Kaufen(w, k);
            var weidenmuend = w.Schiffahrt.Stadt("Weidenmünd")!;
            w.Ausfuehren(k, new SchiffsauftragErteilen(s.Name, "Weidenmünd", Nichts, D(("Wachs", 30))));
            Bis(sim, () => s.Zustand == SchiffZustand.Hinfahrt);
            Pruefe.Gleich(0.0, k.Laufend.Transport, "leer hin: kein Zoll");
            Bis(sim, () => s.Zustand == SchiffZustand.InFernerStadt);
            var wert = 30 * w.Markt["Wachs"].Preis;
            Bis(sim, () => s.Zustand == SchiffZustand.Rueckfahrt);
            Pruefe.Ungefaehr(weidenmuend.Daten.Zollstellen * 0.025 * wert, k.Laufend.Transport, 1e-6);
        }),

        ("Flusspiraten: Chance 1, Stärke 1, ohne Geleit – die Hälfte jeder Ware ist fort, als Raub vermerkt; Stärke 0 – die Mannschaft schlägt sie zurück", () =>
        {
            var (sim, w, k) = Stadt(Piraten(1, 1));
            var meldungen = new List<string>();
            w.Meldung += meldungen.Add;
            var s = Kaufen(w, k);
            w.Ausfuehren(k, new SchiffsauftragErteilen(s.Name, "Eisenfurt", D(("Kerze", 21)), Nichts));
            Bis(sim, () => s.Zustand == SchiffZustand.Hinfahrt);
            Pruefe.Gleich(10, s.Ladung["Kerze"]); // 21 − aufgerundet 10,5
            Pruefe.Wahr(k.Laufend.Raubwert > 0, "Raub vermerkt");
            Pruefe.Wahr(meldungen.Any(m => m.StartsWith("Flusspiraten überfallen Sankt Nikolaus")), "Meldung");

            var (sim2, w2, k2) = Stadt(Piraten(1, 0));
            var s2 = Kaufen(w2, k2);
            w2.Ausfuehren(k2, new SchiffsauftragErteilen(s2.Name, "Eisenfurt", D(("Kerze", 21)), Nichts));
            Bis(sim2, () => s2.Zustand == SchiffZustand.Hinfahrt);
            Pruefe.Gleich(21, s2.Ladung["Kerze"]);
            Pruefe.Gleich(0.0, k2.Laufend.Raubwert);
        }),

        ("Geleit: Kosten der Eskorte × 3 je beladener Fahrt, senkt das Risiko; die Stadtwache nur, wenn ein Haus sie stellt", () =>
        {
            var (sim, w, k) = Stadt(Piraten(0.05, 0.01)); // seltene, schwache Piraten: Risiko > 0, aber kein Überfall, der die Rechnung stört
            var s = Kaufen(w, k);
            Pruefe.Wahr(w.Ausfuehren(k, new SchiffGeleitWaehlen(s.Name, "Schwer")).Erfolg, "Schwer");
            Pruefe.Gleich(90.0, w.Schiffahrt.GeleitKosten("Schwer"));
            Pruefe.Wahr(Kalkulation.SchiffsRisiko(w, k, "Schwer") < Kalkulation.SchiffsRisiko(w, k, "Keine") / 4, "Schutz 0,85");
            Pruefe.Wahr(w.Ausfuehren(k, new SchiffGeleitWaehlen(s.Name, "Stadtwache")).Meldung.Contains("Wachhaus"), "Stadtwache");
            Pruefe.Wahr(!w.Ausfuehren(k, new SchiffGeleitWaehlen(s.Name, "Ritter")).Erfolg, "unbekannt");
            w.Ausfuehren(k, new SchiffsauftragErteilen(s.Name, "Eisenfurt", D(("Kerze", 10)), D(("Eisen", 10))));
            Bis(sim, () => s.Zustand == SchiffZustand.AmKai && s.Fahrten == 1);
            var zoll = k.Laufend.Transport - 2 * 90;
            Pruefe.Wahr(zoll > 0 && zoll < 100, $"Transport = 2 × Geleit + Zoll ({k.Laufend.Transport})");
        }),

        ("Ferner Markt: Jeder Verkauf drückt den Preis dort, jeder Kauf hebt ihn; je Stunde holt er 7,2 % auf (ein Jahr: ein Viertel bleibt)", () =>
        {
            var (sim, w, k) = Stadt();
            var stadt = w.Schiffahrt.Stadt("Weidenmünd")!;
            var ring = stadt.Ankaufpreis("Silberring");
            var gold = stadt.Verkaufspreis("Gold");
            Pruefe.Ungefaehr(Daten.Value.Waren["Silberring"].Grundpreis * 1.4, ring, 1e-9);
            Pruefe.Wahr(stadt.Erloes("Silberring", 10) < 10 * ring && stadt.Kosten("Gold", 10) > 10 * gold, "Preis verschiebt sich je Stück");
            Pruefe.Ungefaehr(stadt.Erloes("Silberring", 10), stadt.Erloes("Silberring", 4) + stadt.Erloes("Silberring", 6, vorher: 4), 1e-9);
            stadt.Verkaufen("Silberring", 30);
            stadt.Kaufen("Gold", 30);
            Pruefe.Wahr(stadt.Ankaufpreis("Silberring") < ring * 0.9, "gesättigt");
            Pruefe.Wahr(stadt.Verkaufspreis("Gold") > gold * 1.1, "teurer");
            // eine Stunde: Druck × 0,928 (Datenversion 56; nach den 19 Stunden eines Jahres ist ein Viertel übrig wie vorher nach 17)
            for (var i = 0; i < 60; i++) sim.Tick();
            Pruefe.Ungefaehr(30 * 0.928, stadt.Druck("Silberring"), 1e-9);
            Pruefe.Ungefaehr(Math.Pow(0.92, 17), Math.Pow(0.928, Spielzeit.StundenProTag), 0.003);
            for (var i = 0; i < 60 * 50; i++) sim.Tick();
            Pruefe.Ungefaehr(ring, stadt.Ankaufpreis("Silberring"), 0.01);
            Pruefe.Ungefaehr(gold, stadt.Verkaufspreis("Gold"), 0.01);
        }),

        ("Mit Betrieb: Hinfracht aus dem Lager, Rückfracht ins Lager bis es voll ist, der Rest auf den Markt", () =>
        {
            var (sim, w, k) = Stadt();
            var b = w.BetriebEroeffnen("Gießerei am Hafen", Betriebsart.Giesserei, k, startkarren: "");
            b.Einlagern("Silberring", 30);
            b.Einlagern("Gold", 5);
            var s = Kaufen(w, k);
            var zutaten = k.Laufend.Zutaten;
            var e = w.Ausfuehren(k, new SchiffsauftragErteilen(s.Name, "Weidenmünd", D(("Silberring", 30)), D(("Gold", 50)), Betrieb: b.Name));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(0, b.ImLager("Silberring"));
            Pruefe.Gleich(zutaten, k.Laufend.Zutaten, "nichts am Markt gekauft");
            Pruefe.Wahr(!w.Ausfuehren(k, new SchiffsauftragErteilen(s.Name, "Weidenmünd", D(("Silberring", 1)), Nichts, b.Name)).Erfolg, "Lager leer");
            var umsatz = k.Laufend.Umsatz;
            Bis(sim, () => s.Zustand == SchiffZustand.AmKai && s.Fahrten == 1);
            Pruefe.Gleich(b.LagerKapazitaetJeWare, b.ImLager("Gold"));
            Pruefe.Wahr(k.Laufend.Umsatz > umsatz, "Ringe dort und 15 Gold am Markt verkauft");
            var lager = w.Ausfuehren(k, new SchiffsauftragErteilen(s.Name, "Weidenmünd", D(("Silberring", 5)), Nichts, b.Name));
            Pruefe.Wahr(lager.Meldung.Contains("liegen nur 0 Silberring"), lager.Meldung);
        }),

        ("Wiederholen: Das Schiff fährt nach der Rückkehr gleich wieder los; „beenden“ lässt es nach der Fahrt am Kai", () =>
        {
            var (sim, w, k) = Stadt();
            var s = Kaufen(w, k);
            w.Ausfuehren(k, new SchiffsauftragErteilen(s.Name, "Eisenfurt", Nichts, D(("Holz", 30)), Wiederholen: true));
            Bis(sim, () => s.Fahrten == 1);
            Pruefe.Gleich(SchiffZustand.Beladen, s.Zustand);
            Pruefe.Wahr(s.Auftrag!.Wiederholen, "wiederholt sich");
            Pruefe.Wahr(w.Ausfuehren(k, new SchiffsauftragBeenden(s.Name)).Erfolg, "beenden");
            Bis(sim, () => s.Fahrten == 2);
            Pruefe.Gleich((SchiffZustand.AmKai, (Schiffsauftrag?)null), (s.Zustand, s.Auftrag));
            Pruefe.Wahr(!w.Ausfuehren(k, new SchiffsauftragBeenden(s.Name)).Erfolg, "nichts zu beenden");
        }),

        ("Rechnung: Kalkulation.Schiffsfahrt sagt den Gewinn einer Fahrt ohne Piraten genau voraus; BesteSchiffsfahrt findet einen Auftrag mit Gewinn", () =>
        {
            var (sim, w, k) = Stadt();
            sim.Tick(); // erst mit der ersten Minute gilt die Nachfrage nach den Einwohnern (Einwohnerfaktor)
            var s = Kaufen(w, k);
            var stadt = w.Schiffahrt.Stadt("Eisenfurt")!;
            var rueck = D(("Eisen", 30), ("Granit", 30));
            var r = Kalkulation.Schiffsfahrt(w, k, s.Daten, stadt, Nichts, rueck);
            Pruefe.Gleich(270, r.Minuten);
            Pruefe.Ungefaehr(r.Gewinn * Spielzeit.MinutenProTag / 270, r.GewinnJeJahr, 1e-9);
            var vorher = k.Vermoegen;
            w.Ausfuehren(k, new SchiffsauftragErteilen(s.Name, stadt.Name, Nichts, rueck));
            Bis(sim, () => s.Fahrten == 1);
            Pruefe.Ungefaehr(r.Gewinn, k.Vermoegen - vorher, 0.01); // daheim gehen die Preise mit der Nachfrage der Einwohner etwas mit
            Pruefe.Ungefaehr(k.Vermoegen - vorher, s.ErtragLaufend, 1e-9);

            var v = Kalkulation.BesteSchiffsfahrt(w, k, s.Daten, 10_000);
            Pruefe.Wahr(v is not null && v.Rechnung.Gewinn > 0, "ein lohnender Auftrag");
            Pruefe.Gleich(null, w.Schiffahrt.AuftragVerboten(k, s.Name, new Schiffsauftrag(v!.Ziel, v.Hinfracht, v.Rueckfracht)));
            Pruefe.Wahr(Kalkulation.BesteSchiffsfahrt(w, k, s.Daten, 0)!.Hinfracht.Count == 0, "ohne Geld keine Hinfracht");
        }),

        ("Unterhalt: zum Jahresende 350 Gulden je Schiff als Fixkosten; der Ertrag des Jahres wird zum Vorjahr", () =>
        {
            var (sim, w, k) = Stadt();
            var s = Kaufen(w, k);
            w.Ausfuehren(k, new SchiffsauftragErteilen(s.Name, "Eisenfurt", Nichts, D(("Eisen", 30))));
            Bis(sim, () => s.Fahrten == 1);
            var ertrag = s.ErtragLaufend;
            Bis(sim, () => sim.Zeit.Jahr == Spielzeit.StartJahr + 1);
            Pruefe.Gleich(350.0, k.Vorjahr!.Fixkosten);
            Pruefe.Ungefaehr(ertrag - 350, s.ErtragVorjahr!.Value, 1e-9);
            Pruefe.Gleich(0.0, s.ErtragLaufend);
        }),

        ("Lage für die Darstellung: am Kai am Liegeplatz, nach dem Ablegen auf der Route, hinter dem Kartenrand unsichtbar, zurück wieder am Liegeplatz", () =>
        {
            var (sim, w, k) = Stadt();
            var s = Kaufen(w, k);
            var sa = w.Schiffahrt;
            var hafen = sa.Haefen[0];
            var lage = sa.Lage(s, sim.Zeit.VergangeneMinuten);
            Pruefe.Wahr(lage.Sichtbar && lage.Ort == hafen.Liegeplatz, "am Kai");
            w.Ausfuehren(k, new SchiffsauftragErteilen(s.Name, "Eisenfurt", Nichts, D(("Eisen", 30))));
            Bis(sim, () => s.Zustand == SchiffZustand.Hinfahrt);
            var route = sa.Route("Hafen1", flussauf: true);
            var nach10 = sa.Lage(s, s.Beginn + 10);
            Pruefe.Wahr(nach10.Sichtbar, "10 Minuten nach dem Ablegen zu sehen");
            Pruefe.Wahr(nach10.Ort.X > hafen.Liegeplatz.X + 30, $"flussauf nach Osten: {nach10.Ort}");
            Pruefe.Wahr(nach10.RichtungX > 0.5, "fährt nach Osten");
            Pruefe.Ungefaehr(10.0 / 134, nach10.Fortschritt, 1e-9);
            var schritt = sa.Lage(s, s.Beginn + 10.5);
            Pruefe.Wahr(schritt.Ort.Abstand(nach10.Ort) is > 1 and < 6, "gleitet zwischen den Minuten");
            var rand = w.Karte!.Kartengrenzen.Spielfeld.X1;
            Pruefe.Wahr(sa.Lage(s, s.Ende - 1) is var spaet && (!spaet.Sichtbar || spaet.Ort.X > rand), "kurz vor der fernen Stadt hinter dem Kartenrand");
            Pruefe.Wahr(!sa.Lage(s, s.Ende).Sichtbar, "am Ende der Route unsichtbar");
            Bis(sim, () => s.Zustand == SchiffZustand.Rueckfahrt);
            var zurueck = sa.Lage(s, s.Ende - 2);
            Pruefe.Wahr(zurueck.Sichtbar && zurueck.Ort.Abstand(hafen.Liegeplatz) < 40 && zurueck.RichtungX < 0, $"kurz vor dem Hafen, nach Westen: {zurueck}");
            Pruefe.Wahr(sa.Lage(s, s.Beginn + 1) is var frueh && (!frueh.Sichtbar || frueh.Ort.X > rand), "am Anfang der Rückfahrt noch hinter dem Rand");
            Pruefe.Wahr(route.Count > 10, "Route");
        }),

        ("Speichern und Laden mitten auf der Fahrt: Zustand, Ladung, Auftrag, Märkte der fernen Städte bleiben – die Partie läuft gleich weiter", () =>
        {
            var (sim, w, k) = Stadt(Daten.Value); // mit Piraten: Der Zufall muss gleich weiterlaufen
            var s = Kaufen(w, k);
            w.Ausfuehren(k, new SchiffGeleitWaehlen(s.Name, "Normal"));
            w.Ausfuehren(k, new SchiffsauftragErteilen(s.Name, "Weidenmünd", D(("Silberring", 20)), D(("Gold", 30)), Wiederholen: true));
            Bis(sim, () => s.Zustand == SchiffZustand.Hinfahrt);
            for (var i = 0; i < 30; i++) sim.Tick();
            var json = Speicherung.Speichern(sim, k);
            var (sim2, k2) = Speicherung.Laden(json);
            var w2 = sim2.Wirtschaft!;
            var s2 = w2.Schiffahrt.Schiff(s.Name)!;
            Pruefe.Gleich((s.Zustand, s.Beginn, s.Ende, s.Geleit, s.Hafen), (s2.Zustand, s2.Beginn, s2.Ende, s2.Geleit, s2.Hafen));
            Pruefe.Gleich(Text(s.Ladung), Text(s2.Ladung));
            Pruefe.Wahr(s2.Auftrag is { Ziel: "Weidenmünd", Wiederholen: true } && s2.Auftrag.Rueckfracht["Gold"] == 30, "Auftrag");
            Pruefe.Gleich(k2, s2.Besitzer);
            for (var i = 0; i < 2000; i++) { sim.Tick(); sim2.Tick(); }
            Pruefe.Gleich(s.Fahrten, s2.Fahrten);
            Pruefe.Ungefaehr(k.Vermoegen, k2!.Vermoegen, 1e-12);
            foreach (var stadt in w.Schiffahrt.Staedte)
                Pruefe.Ungefaehr(stadt.Druck("Gold") + 1, w2.Schiffahrt.Stadt(stadt.Name)!.Druck("Gold") + 1, 1e-12);
            Pruefe.Wahr(s.Fahrten >= 5, $"Fahrten: {s.Fahrten}");
        }),

        ("Alte Spielstände ohne Schiffshandel laden weiter: keine Schiffe, ferne Märkte ausgeglichen", () =>
        {
            var (sim, w, k) = Stadt();
            var s = Kaufen(w, k);
            w.Ausfuehren(k, new SchiffsauftragErteilen(s.Name, "Eisenfurt", Nichts, D(("Eisen", 30))));
            var knoten = JsonNode.Parse(Speicherung.Speichern(sim, k))!.AsObject();
            var wirtschaft = knoten.First(x => x.Key.Equals("wirtschaft", StringComparison.OrdinalIgnoreCase)).Value!.AsObject();
            var schluessel = wirtschaft.Select(x => x.Key).Single(x => x.Equals("schiffahrt", StringComparison.OrdinalIgnoreCase));
            wirtschaft.Remove(schluessel);
            var w2 = Speicherung.Laden(knoten.ToJsonString()).Simulation.Wirtschaft!;
            Pruefe.Gleich(0, w2.Schiffahrt.Schiffe.Count);
            Pruefe.Wahr(w2.Schiffahrt.IstAktiv, "Hafen der Karte gibt es weiter");
            Pruefe.Gleich(0.0, w2.Schiffahrt.Stadt("Eisenfurt")!.Druck("Eisen"));
        }),
    };
}
