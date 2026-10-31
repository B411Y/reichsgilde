using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;
using Reichsgilde.Kartenbau;
using Reichsgilde.Kartenbau.Staedte;

/// <summary>
/// Kartenwerkstatt, Schritt 5d (PR #182): Wegbelag – Format und Befehle mit Rückgängig, Tempo der Karren je Stufe, Austreten und Verwildern,
/// Verfall, Bauauftrag von Spieler und KI, Stadtrat (Pflaster der Stadt, Antrag „Weg pflastern“), Spielstand; alte Karten unverändert.
/// </summary>
static class WegebauTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    /// <summary>Die echten Daten mit geändertem Inhalt (z. B. ohne „wegebau“).</summary>
    static Wirtschaftsdaten DatenMit(Action<JsonObject> aendern)
    {
        var knoten = JsonNode.Parse(File.ReadAllText(Datenpfad.Wirtschaft()))!.AsObject();
        aendern(knoten);
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    }

    static Kartenentwurf Probestadt() => new Probestadt().Bauen(Katalog.Value, Daten.Value);
    static int Nr(Stadtkarte k, string name) => k.Wege.FindIndex(w => w.Name == name);

    /// <summary>Probepartie in der Probestadt (Spieler Schmied, Werkstatt bei (−30 | −10) an der Hauptstraße, Markt bei (0 | 0)).</summary>
    static (Simulation Sim, Konto Spieler, Betrieb Werkstatt) Partie(Kartenentwurf? e = null, Wirtschaftsdaten? daten = null)
    {
        var (sim, spieler) = (e ?? Probestadt()).Ausprobieren(daten ?? Daten.Value, "Schmied");
        return (sim, spieler, sim.Wirtschaft!.Betriebe.Single(b => b.Konto == spieler));
    }

    /// <summary>Die Karrenart der Werkstatt in der Probestadt (Startkarren der Gießerei).</summary>
    const string Hand = "Handkarren";

    /// <summary>Die Fahrzeit eines Handkarrens nach der Formel (Länge × Belagfaktor ÷ Tempo, Datenversion 65; FahrzeitWeglaengeTests.NachFormel).</summary>
    static int Erwartet(Stadtwirtschaft w, Betrieb b, bool hin) => FahrzeitWeglaengeTests.NachFormel(w, b, Hand, hin);

    /// <summary>Eine Fahrt über flache Stücke mit dem Belagfaktor <paramref name="faktor"/> mit dem Handkarren: Länge × Faktor ÷ Tempo, auf ganze Minuten.</summary>
    static int Flach(IReadOnlyList<Fahrtstueck> fahrt, double faktor) =>
        (int)Math.Round(fahrt.Sum(s => s.Laenge) * faktor / (Daten.Value.Logistik.Karren[Hand].MeterJeMinute), MidpointRounding.AwayFromZero);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Wegbelag: Format – Abschnitte je Weg (von, bis, stufe), Vorgabe der Klasse, geschrieben und gelesen; Befehle mit Rückgängig; alte Karten ohne Belag", () =>
        {
            var e = Probestadt();
            var haupt = e.Wege.Single(w => w.Name == "Hauptstraße");
            Pruefe.Gleich(0, haupt.Belag.Count);
            Pruefe.Gleich(Belagstufe.Fahrweg, Wegbelag.Vorgabe(Wegklasse.Haupt));
            Pruefe.Gleich(Belagstufe.Trampelpfad, Wegbelag.Vorgabe(Wegklasse.Gasse));
            e.BelagSetzen(haupt, 20, 60, Belagstufe.Pflaster);
            e.BelagSetzen(haupt, 80, 50, Belagstufe.Kies); // verkehrt herum angegeben, überdeckt das Ende des Pflasters
            string Text(EntwurfWeg w) => string.Join(", ", w.BelagAbschnitte.Select(a => $"{a.Von:0.#}–{a.Bis:0.#} {a.Stufe}"));
            Pruefe.Gleich("0–20 Fahrweg, 20–50 Pflaster, 50–80 Kies, 80–140 Fahrweg", Text(haupt));
            Pruefe.Gleich(2, haupt.Belag.Count, "nur was von der Vorgabe abweicht");
            e.BelagSetzen(haupt, 50, 80, Belagstufe.Fahrweg); // zurück auf die Vorgabe
            Pruefe.Gleich("0–20 Fahrweg, 20–50 Pflaster, 50–140 Fahrweg", Text(haupt));
            e.Rueckgaengig();
            Pruefe.Gleich("0–20 Fahrweg, 20–50 Pflaster, 50–80 Kies, 80–140 Fahrweg", Text(haupt));
            // geschrieben und gelesen
            var json = e.ZuJson();
            Pruefe.Wahr(json.Contains("\"belag\": [{\"von\": 20, \"bis\": 50, \"stufe\": \"pflaster\"}, {\"von\": 50, \"bis\": 80, \"stufe\": \"kies\"}]"), json);
            var karte = Stadtkarte.AusJson(json, Katalog.Value);
            var weg = karte.Wege[Nr(karte, "Hauptstraße")];
            Pruefe.Gleich((Belagstufe.Pflaster, 1.0), Wegbelag.StufeBei(weg.Klasse, weg.Laenge, weg.Belag, 30));
            Pruefe.Gleich(Belagstufe.Fahrweg, Wegbelag.StufeBei(weg.Klasse, weg.Laenge, weg.Belag, 100).Stufe);
            Pruefe.Gleich(json, Kartenentwurf.AusJson(json, Katalog.Value).ZuJson(), "wieder geöffnet: dieselbe Datei");
            // ganzer Weg: Kies bis zum Ende (ohne „bis“), dann zurück auf die Vorgabe (das Feld entfällt)
            var land = e.Wege.Single(w => w.Name == "Landstraße nach Osten");
            e.BelagSetzen(land, Belagstufe.Kies);
            Pruefe.Wahr(e.ZuJson().Contains("\"belag\": [{\"von\": 0, \"stufe\": \"kies\"}]"), "ganzer Weg");
            e.BelagSetzen(land, (Belagstufe?)null);
            Pruefe.Gleich(0, land.Belag.Count);
            // ein Abschnitt hinter dem Ende: Hinweis „belag“; die Prüfung der Karte lehnt falsche Abschnitte ab
            e.BelagSetzen(land, 100, 120, Belagstufe.Pflaster);
            e.StuetzpunktVerschieben(land, 1, 140, 0, einrasten: false);
            Pruefe.Wahr(e.Befunde.Any(b => b.Regel == "belag" && b.Schwere == Schwere.Hinweis), "Hinweis hinter dem Ende");
            var falsch = json.Replace("{\"von\": 20, \"bis\": 50", "{\"von\": 20, \"bis\": 10");
            Pruefe.Wahr(Stadtkarte.AusJson(json, Katalog.Value) is not null, "richtig");
            try
            {
                Stadtkarte.AusJson(falsch, Katalog.Value);
                Pruefe.Wahr(false, "bis vor von muss ein Fehler sein");
            }
            catch (KartenFehler) { }
            // Greybox, Köln, Messstadt: kein Belag in der Datei – alles nach der Vorgabe
            foreach (var kennung in new[] { "greybox", "koeln", "messstadt", "probestadt" })
                Pruefe.Wahr(!File.ReadAllText(Datenpfad.Karte(kennung)).Contains("\"belag\""), kennung);
        }),

        ("Wegbelag: Tempo – auf Fahrweg (Bezug) Länge ÷ Tempo, Pflaster höchstens 15 % schneller, Kies schneller, Trampelpfad langsamer; ohne „wegebau“ kein Belag", () =>
        {
            var (sim, _, werkstatt) = Partie();
            var w = sim.Wirtschaft!;
            Pruefe.Wahr(w.Wegebau.IstAktiv, "aktiv");
            Pruefe.Wahr(w.Fahrten(werkstatt)!.Value.Hin.All(s => s.Art != Fahrbahn.Strasse || s.Weg >= 0), "jedes Straßenstück kennt seinen Weg");
            var (h0, z0) = w.Fahrten(werkstatt)!.Value;
            Pruefe.Gleich((Flach(h0, 1), Flach(z0, 1)), (w.Fahrzeit(werkstatt, Hand, true), w.Fahrzeit(werkstatt, Hand, false)), "Fahrweg = Bezug");
            Pruefe.Ungefaehr(1, w.Wegebau.Faktor(w.Fahrten(werkstatt)!.Value.Hin[0]), 0);
            // Pflaster auf der ganzen Hauptstraße: 0,85 ÷ 1 = 0,85 der Zeit, aber höchstens 15 % schneller (Faktor 1 ÷ 1,15)
            var e = Probestadt();
            e.BelagSetzen(e.Wege.Single(x => x.Name == "Hauptstraße"), Belagstufe.Pflaster);
            var (simP, _, wp) = Partie(e);
            var p = simP.Wirtschaft!;
            var hinP = p.Fahrten(wp)!.Value.Hin;
            Pruefe.Wahr(hinP.Where(s => s.Art == Fahrbahn.Strasse).All(s => p.Karte!.Wege[s.Weg].Name == "Hauptstraße"), "nur über die Hauptstraße");
            Pruefe.Ungefaehr(1 / 1.15, p.Wegebau.Faktor(hinP[0]), 1e-12);
            Pruefe.Gleich(Flach(hinP, 1 / 1.15), p.Fahrzeit(wp, Hand, true));
            Pruefe.Gleich(Erwartet(p, wp, false), p.Fahrzeit(wp, Hand, false));
            // Trampelpfad: 0,85 ÷ 0,7
            e.BelagSetzen(e.Wege.Single(x => x.Name == "Hauptstraße"), Belagstufe.Trampelpfad);
            var (simT, _, wt) = Partie(e);
            Pruefe.Gleich(Flach(simT.Wirtschaft!.Fahrten(wt)!.Value.Hin, 0.85 / 0.70), simT.Wirtschaft.Fahrzeit(wt, Hand, true));
            // Kies: 0,85 ÷ 0,95
            e.BelagSetzen(e.Wege.Single(x => x.Name == "Hauptstraße"), Belagstufe.Kies);
            var (simK, _, wk) = Partie(e);
            Pruefe.Gleich(Flach(simK.Wirtschaft!.Fahrten(wk)!.Value.Hin, 0.85 / 0.95), simK.Wirtschaft.Fahrzeit(wk, Hand, true));
            // ohne Abschnitt „wegebau“: Pflaster wirkt nicht, keine Abfragen
            var (simO, _, wo) = Partie(e, DatenMit(d => d.Remove("wegebau")));
            Pruefe.Wahr(!simO.Wirtschaft!.Wegebau.IstAktiv, "ohne Daten nicht aktiv");
            Pruefe.Gleich(Flach(simO.Wirtschaft.Fahrten(wo)!.Value.Hin, 1), simO.Wirtschaft.Fahrzeit(wo, Hand, true));
            Pruefe.Gleich(Belagstufe.Kies, simO.Wirtschaft.Wegebau.BelagBei(0, 10), "Startbelag der Karte ohne Daten");
            // Greybox mit allen Häusern: jeder Karren nach der Formel (Länge, Steigung, Belag, Tempo seiner Art)
            var greybox = Stadtkarte.AusDatei(Datenpfad.Karte("greybox"));
            var simG = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie(Rivalen: 3), karte: greybox).Simulation;
            var g = simG.Wirtschaft!;
            foreach (var b in g.Betriebe.Where(b => g.Fahrten(b) is { Hin.Count: > 0 }))
                foreach (var k in b.Karren)
                    Pruefe.Gleich((FahrzeitWeglaengeTests.NachFormel(g, b, k.Typ, true), FahrzeitWeglaengeTests.NachFormel(g, b, k.Typ, false)),
                        (g.Fahrzeit(k, true), g.Fahrzeit(k, false)), $"{b.Name} {k.Typ}");
        }),

        ("Wegbelag: Abfragen – Stücke zu 5 m, Abschnitte, Belag und Verkehr an einer Stelle, nächster Weg zu einem Punkt", () =>
        {
            var e = Probestadt();
            e.BelagSetzen(e.Wege.Single(x => x.Name == "Hauptstraße"), 20, 50, Belagstufe.Pflaster);
            var (sim, _, _) = Partie(e);
            var wb = sim.Wirtschaft!.Wegebau;
            var haupt = Nr(sim.Wirtschaft.Karte!, "Hauptstraße");
            Pruefe.Gleich(28, wb.StueckZahl(haupt), "140 m");
            Pruefe.Gleich(Belagstufe.Pflaster, wb.BelagBei(haupt, 22));
            Pruefe.Gleich(Belagstufe.Fahrweg, wb.BelagBei(haupt, 52));
            var abschnitte = wb.Abschnitte(haupt);
            Pruefe.Gleich("0–20 Fahrweg, 20–50 Pflaster, 50–140 Fahrweg", string.Join(", ", abschnitte.Select(a => $"{a.Von:0.#}–{a.Bis:0.#} {a.Stufe}")));
            Pruefe.Ungefaehr(1, abschnitte[1].Tempo, 1e-12);
            Pruefe.Ungefaehr(0.85, abschnitte[0].Tempo, 1e-12);
            Pruefe.Wahr(wb.Verkehr(haupt, 70) >= 40, "Grundverkehr der Hauptstraße");
            Pruefe.Wahr(wb.Stueck(haupt, 14).Verkehr > wb.Stueck(haupt, 0).Verkehr, "am Markt mehr Fußverkehr als am Tor");
            var stelle = wb.WegstelleBei(-30, 1)!.Value;
            Pruefe.Gleich(haupt, stelle.Weg);
            Pruefe.Ungefaehr(40, stelle.Stelle, 1e-9);
            Pruefe.Ungefaehr(1, stelle.Abstand, 1e-9);
            Pruefe.Wahr(wb.WegstelleBei(0, 300) is null, "weit weg");
            var feldweg = Nr(sim.Wirtschaft.Karte!, "Feldweg Ost-Nord");
            Pruefe.Wahr(wb.Stuecke(feldweg).All(s => s.Verkehr < 40), "Feldweg ohne Grundverkehr und Karren");
        }),

        ("Wegbelag: Austreten und Verwildern – Verkehr tritt einen Trampelpfad zum Fahrweg aus, ein Fahrweg ohne Verkehr verwildert; ohne Zufall", () =>
        {
            var e = Probestadt();
            var feld = e.Wege.Single(x => x.Name == "Feldweg Ost-Nord");
            e.BelagSetzen(feld, Belagstufe.Trampelpfad);
            var (sim, _, werkstatt) = Partie(e);
            var w = sim.Wirtschaft!;
            var wb = w.Wegebau;
            var nr = Nr(w.Karte!, "Feldweg Ost-Nord");
            var haupt = Nr(w.Karte!, "Hauptstraße");
            var stelle = w.Fahrten(werkstatt)!.Value.Hin.First(s => s.Weg == haupt).Stelle;
            // der Feldweg hat keinen Verkehr: ein Trampelpfad bleibt, ein Fahrweg (Feldweg West-Nord) verwildert um 0,1 je Jahr
            var west = Nr(w.Karte!, "Feldweg West-Nord");
            for (var jahr = 1; jahr <= 9; jahr++) wb.Jahresende();
            Pruefe.Gleich(Belagstufe.Fahrweg, wb.Stueck(west, 2).Stufe);
            Pruefe.Ungefaehr(0.1, wb.Stueck(west, 2).Austritt, 1e-9);
            wb.Jahresende();
            Pruefe.Gleich(Belagstufe.Trampelpfad, wb.Stueck(west, 2).Stufe, "nach zehn Jahren ohne Verkehr");
            Pruefe.Gleich(Belagstufe.Trampelpfad, wb.Stueck(nr, 2).Stufe);
            Pruefe.Gleich(0.0, wb.Stueck(nr, 2).Austritt);
            // die Hauptstraße (Grundverkehr) bleibt Fahrweg
            Pruefe.Gleich(Belagstufe.Fahrweg, wb.BelagBei(haupt, stelle));
            // 30 Karrenfahrten im Jahr auf einem Trampelpfad: (30 + 40 − 5) ÷ 50 > 0,5 → +0,5 je Jahr, nach zwei Jahren Fahrweg
            var e2 = Probestadt();
            e2.BelagSetzen(e2.Wege.Single(x => x.Name == "Hauptstraße"), Belagstufe.Trampelpfad);
            var (sim2, _, w2) = Partie(e2);
            var wb2 = sim2.Wirtschaft!.Wegebau;
            void Jahr()
            {
                for (var i = 0; i < 30; i++) wb2.FahrtZaehlen(w2);
                wb2.Jahresende();
            }
            Jahr();
            Pruefe.Gleich(30, wb2.FahrtenVorjahr(w2.Name));
            Pruefe.Gleich(Belagstufe.Trampelpfad, wb2.BelagBei(haupt, stelle));
            Pruefe.Ungefaehr(0.5, wb2.Stueck(haupt, wb2.StueckBei(haupt, stelle)).Austritt, 1e-9);
            Pruefe.Wahr(wb2.Verkehr(haupt, stelle) >= 70, "30 Karren + Grundverkehr");
            Jahr();
            Pruefe.Gleich(Belagstufe.Fahrweg, wb2.BelagBei(haupt, stelle), "ausgetreten");
            Pruefe.Gleich(Erwartet(sim2.Wirtschaft, w2, true), sim2.Wirtschaft.Fahrzeit(w2, Hand, true), "als Fahrweg wieder Länge ÷ Tempo");
            // Karren zählen wirklich: ein Jahr Spiel, dann stehen Fahrten im Vorjahr
            var (sim3, _, w3) = Partie();
            for (var i = 0; i < Spielzeit.MinutenProTag; i++) sim3.Tick();
            Pruefe.Wahr(sim3.Wirtschaft!.Wegebau.FahrtenVorjahr(w3.Name) > 0 || sim3.Wirtschaft.Betriebe.Any(b => sim3.Wirtschaft.Wegebau.FahrtenVorjahr(b.Name) > 0),
                "Karrenfahrten gezählt");
        }),

        ("Wegbelag: Verfall – Kies und Pflaster verlieren Zustand (Tempo sinkt etwas) und fallen unter der Schwelle eine Stufe zurück", () =>
        {
            var e = Probestadt();
            var json = JsonNode.Parse(e.ZuJson())!;
            var haupt = json["wege"]!.AsArray().First(x => x!["name"]!.GetValue<string>() == "Hauptstraße")!.AsObject();
            haupt["belag"] = JsonNode.Parse("[{\"von\": 0, \"stufe\": \"pflaster\", \"zustand\": 0.3}]");
            var (sim, _, werkstatt) = Partie(Kartenentwurf.AusJson(json.ToJsonString(), Katalog.Value));
            var w = sim.Wirtschaft!;
            var wb = w.Wegebau;
            var nr = Nr(w.Karte!, "Hauptstraße");
            Pruefe.Ungefaehr(0.95 + 0.05 * 0.3, wb.Stueck(nr, 3).Tempo, 1e-12);
            var vorher = w.Fahrzeit(werkstatt, Hand, true);
            Pruefe.Gleich(Erwartet(w, werkstatt, true), vorher);
            wb.Jahresende(); // 0,3 − 0,03 = 0,27: noch Pflaster
            Pruefe.Gleich(Belagstufe.Pflaster, wb.Stueck(nr, 3).Stufe);
            Pruefe.Ungefaehr(0.27, wb.Stueck(nr, 3).Zustand, 1e-9);
            wb.Jahresende(); // 0,24 < 0,25: Kies, neu
            Pruefe.Gleich(Belagstufe.Kies, wb.Stueck(nr, 3).Stufe);
            Pruefe.Ungefaehr(1, wb.Stueck(nr, 3).Zustand, 1e-12);
            Pruefe.Gleich(Erwartet(w, werkstatt, true), w.Fahrzeit(werkstatt, Hand, true), "neu gerechnet nach dem Jahreswechsel");
            Pruefe.Wahr(w.Fahrzeit(werkstatt, Hand, true) >= vorher, "Kies ist nicht schneller");
        }),

        ("Wegbelag: Bauauftrag des Spielers – Preis je Meter, Bauzeit, Baustelle; danach Pflaster und kürzere Fahrt; Ausbessern kostet den fehlenden Zustand", () =>
        {
            var (sim, spieler, werkstatt) = Partie();
            var w = sim.Wirtschaft!;
            var wb = w.Wegebau;
            var nr = Nr(w.Karte!, "Hauptstraße");
            var v = wb.Vorschau(spieler, nr, 38, 72, Belagstufe.Pflaster); // auf ganze Stücke: 35–75 m
            Pruefe.Gleich((35.0, 75.0, 40.0), (v.Von, v.Bis, v.Meter));
            Pruefe.Ungefaehr(40 * 15, v.Kosten, 1e-9);
            Pruefe.Gleich(60 + 80, v.BauzeitMinuten);
            Pruefe.Gleich(null, v.Verboten);
            Pruefe.Wahr(wb.Vorschau(spieler, nr, 0, 140, Belagstufe.Fahrweg).Verboten!.Contains("Kies oder Pflaster"), "nur Kies und Pflaster");
            Pruefe.Wahr(wb.Vorschau(spieler, 99, 0, 10, Belagstufe.Kies).Verboten is not null, "kein Weg");
            var vermoegen = spieler.Vermoegen;
            var e = w.Ausfuehren(spieler, new WegAusbauen(nr, 38, 72, Belagstufe.Pflaster));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Ungefaehr(vermoegen - 600, spieler.Vermoegen, 1e-9);
            var bau = wb.Baustellen.Single();
            Pruefe.Wahr(bau.Bauherr == spieler && bau.Stufe == Belagstufe.Pflaster && bau.Von == 35 && bau.Bis == 75, "Baustelle");
            Pruefe.Wahr(wb.Stueck(nr, 8).Baustelle && !wb.Stueck(nr, 6).Baustelle, "Stücke im Bau");
            Pruefe.Wahr(wb.Vorschau(spieler, nr, 70, 80, Belagstufe.Kies).Verboten!.Contains("schon gebaut"), "überlappt die Baustelle");
            Pruefe.Gleich(Belagstufe.Fahrweg, wb.BelagBei(nr, 40), "noch im Bau");
            var vorher = w.Fahrzeit(werkstatt, Hand, true);
            wb.Minute(new Spielzeit(bau.FertigAb - 1));
            Pruefe.Gleich(1, wb.Baustellen.Count);
            wb.Minute(new Spielzeit(bau.FertigAb));
            Pruefe.Gleich(0, wb.Baustellen.Count);
            Pruefe.Gleich(Belagstufe.Pflaster, wb.BelagBei(nr, 40));
            Pruefe.Gleich(Belagstufe.Fahrweg, wb.BelagBei(nr, 76));
            Pruefe.Wahr(w.Fahrzeit(werkstatt, Hand, true) < vorher, $"schneller: {w.Fahrzeit(werkstatt, Hand, true)} < {vorher}");
            Pruefe.Gleich(Erwartet(w, werkstatt, true), w.Fahrzeit(werkstatt, Hand, true));
            // schon gepflastert: nichts zu tun; Kies auf Pflaster bleibt Pflaster
            Pruefe.Wahr(wb.Vorschau(spieler, nr, 35, 75, Belagstufe.Pflaster).Verboten!.Contains("schon"), "schon Pflaster");
            Pruefe.Ungefaehr(0, wb.Vorschau(spieler, nr, 35, 75, Belagstufe.Kies).Kosten, 1e-9);
            // nach drei Jahren Verfall kostet Ausbessern 9 % des Neupreises
            for (var i = 0; i < 3; i++) wb.Jahresende();
            Pruefe.Ungefaehr(600 * 0.09, wb.Vorschau(spieler, nr, 35, 75, Belagstufe.Pflaster).Kosten, 1e-6);
            // zu lang, zu teuer
            Pruefe.Wahr(wb.Vorschau(spieler, Nr(w.Karte!, "Landstraße nach Westen"), -10, 500, Belagstufe.Kies).Verboten is null, "auf die Länge beschnitten (120 m)");
            spieler.Investition(spieler.Vermoegen);
            Pruefe.Wahr(!w.Ausfuehren(spieler, new WegAusbauen(nr, 0, 30, Belagstufe.Kies)).Erfolg, "kein Geld");
        }),

        ("Wegbelag: KI baut, wenn sich die schnellere Fahrt ihrer Karren in absehbarer Zeit bezahlt macht – sonst nicht", () =>
        {
            Wirtschaftsdaten Mit(double wert) => DatenMit(d => d["ki"]!["wegebau"]!["wertJeKarrenminute"] = wert);
            foreach (var (wert, baut) in new[] { (0.0, false), (100.0, true) })
            {
                var (sim, _, _) = Partie(daten: Mit(wert));
                var w = sim.Wirtschaft!;
                var ki = w.Rivalen[0];
                ki.Konto.Amtseinkommen(50_000);
                foreach (var b in ki.Betriebe.Where(b => w.Fahrten(b) is { Hin.Count: > 0 }))
                    for (var i = 0; i < 30; i++) w.Wegebau.FahrtZaehlen(b);
                w.Wegebau.Jahresende();
                ki.Jahresende();
                var bau = w.Wegebau.Baustellen.Where(x => x.Bauherr == ki.Konto).ToList();
                Pruefe.Gleich(baut, bau.Count == 1, $"Wert {wert}: {string.Join(" | ", ki.Entscheidungen)}");
                if (!baut) continue;
                Pruefe.Wahr(ki.Entscheidungen.Any(x => x.Contains("ausbauen") && x.Contains(Zahlenformat.Ganz(bau[0].Kosten))), "gemeldet");
                Pruefe.Wahr(ki.Betriebe.Any(b => w.Fahrten(b)!.Value.Hin.Any(s => s.Weg == bau[0].Weg)), "auf der eigenen Strecke");
                Pruefe.Wahr(w.Wegebau.ErsparteMinutenJeJahr(ki.Konto, bau[0].Weg, bau[0].VonStueck, bau[0].BisStueck, bau[0].Stufe) > 0, "spart Minuten");
            }
        }),

        ("Wegbelag: Stadtrat – die Stadt pflastert aus der Wegekasse an Hauptstraße und Markt; Antrag „Weg pflastern“ mit Abstimmung, Ansehen und AP", () =>
        {
            var (sim, spieler, werkstatt) = Partie();
            var w = sim.Wirtschaft!;
            var wb = w.Wegebau;
            var nr = Nr(w.Karte!, "Hauptstraße");
            Pruefe.Gleich(0.0, wb.Wegekasse);
            wb.Jahresende();
            Pruefe.Gleich(600.0, wb.Wegekasse);
            // Antrag nur im Rat
            Pruefe.Wahr(!w.Ausfuehren(spieler, new WegPflasterBeantragen(nr, 0, 40)).Erfolg, "nicht im Rat");
            var amt = w.Rat.Aemter.OrderBy(a => a.Stufe).First();
            amt.Ratsherr = null;
            amt.Konto = spieler;
            var ap = spieler.Aktionspunkte.Vorrat;
            var e = w.Ausfuehren(spieler, new WegPflasterBeantragen(nr, 0, 40));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(ap - 2, spieler.Aktionspunkte.Vorrat, "2 AP");
            Pruefe.Wahr(!w.Ausfuehren(spieler, new WegPflasterBeantragen(nr, 50, 60)).Erfolg, "ein Antrag je Jahr");
            var (ja, nein, _, _) = wb.AntragPrognose(spieler, nr, 0, 40);
            Pruefe.Wahr(ja > nein, $"Hauptstraße mit viel Verkehr: {ja} dafür, {nein} dagegen");
            var ansehen = w.Rat.Ansehen(spieler);
            var protokoll = new List<string>();
            wb.RatSitzung(w.Jahr, protokoll.Add);
            Pruefe.Wahr(protokoll.Any(p => p.Contains("ANGENOMMEN")), string.Join("\n", protokoll));
            Pruefe.Ungefaehr(ansehen + 5, w.Rat.Ansehen(spieler), 1e-9);
            Pruefe.Wahr(wb.Antrag is null, "Antrag erledigt");
            var stadt = wb.Baustellen.Where(b => b.Bauherr is null).ToList();
            Pruefe.Wahr(stadt.Any(b => b.Weg == nr && b.Von == 0 && b.Bis == 40), "der Antrag wird gebaut");
            // was übrig blieb (600 − 600 = 0): die Stadt baut dieses Jahr nichts mehr
            Pruefe.Gleich(1, stadt.Count);
            Pruefe.Ungefaehr(0, wb.Wegekasse, 1e-9);
            // nächstes Jahr: 600 für die Stadt – sie pflastert am Markt, wo der meiste Verkehr ist (40 m)
            wb.Jahresende();
            wb.RatSitzung(w.Jahr, protokoll.Add);
            var neu = wb.Baustellen.Where(b => b.Bauherr is null && b.Von != 0).ToList();
            Pruefe.Gleich(1, neu.Count, string.Join("\n", protokoll));
            Pruefe.Wahr(neu[0].Weg == nr && neu[0].Bis - neu[0].Von == 40 && neu[0].Von <= 70 && neu[0].Bis >= 70, $"am Markt: {neu[0]}");
            // abgelehnt: ein Feldweg ohne Verkehr, ohne Gunst – Ansehen bleibt
            var feld = Nr(w.Karte!, "Feldweg Ost-Nord");
            w.Ausfuehren(spieler, new WegPflasterBeantragen(feld, 0, 20));
            var (ja2, nein2, _, _) = wb.AntragPrognose(spieler, feld, 0, 20);
            Pruefe.Wahr(nein2 > ja2, $"{ja2} dafür, {nein2} dagegen");
            protokoll.Clear();
            wb.RatSitzung(w.Jahr, protokoll.Add);
            Pruefe.Wahr(protokoll.Any(p => p.Contains("abgelehnt")), string.Join("\n", protokoll));
            // das Ansehen hält fünf Jahre (das laufende mitgezählt)
            Pruefe.Wahr(wb.AnsehenBonus(spieler) == 5, "Ansehen jetzt");
        }),

        ("Wegbelag: Spielstand – Belag, Austritt, Baustellen, Wegekasse, Antrag und Ansehen rund; alter Stand ohne Wegebau beginnt mit der Karte", () =>
        {
            var (sim, spieler, werkstatt) = Partie();
            var w = sim.Wirtschaft!;
            var wb = w.Wegebau;
            var nr = Nr(w.Karte!, "Hauptstraße");
            wb.Jahresende(); // Feldwege verwildern ein wenig
            for (var i = 0; i < 7; i++) wb.FahrtZaehlen(werkstatt);
            wb.Jahresende();
            w.Ausfuehren(spieler, new WegAusbauen(nr, 0, 20, Belagstufe.Kies));
            var amt = w.Rat.Aemter.OrderBy(a => a.Stufe).First();
            (amt.Ratsherr, amt.Konto) = (null, spieler);
            w.Ausfuehren(spieler, new WegPflasterBeantragen(nr, 100, 120));
            for (var i = 0; i < 3; i++) wb.FahrtZaehlen(werkstatt);
            var json = Speicherung.Speichern(sim, spieler);
            var (geladen, spieler2) = Speicherung.Laden(json);
            var w2 = geladen.Wirtschaft!;
            var wb2 = w2.Wegebau;
            for (var weg = 0; weg < w.Karte!.Wege.Count; weg++)
                Pruefe.Gleich(string.Join(";", wb.Stuecke(weg)), string.Join(";", wb2.Stuecke(weg)), $"Weg {weg}");
            Pruefe.Gleich(wb.Wegekasse, wb2.Wegekasse);
            Pruefe.Gleich(string.Join(";", wb.Baustellen.Select(b => (b.Weg, b.Von, b.Bis, b.Stufe, b.BauherrName, b.FertigAb, b.Kosten))),
                string.Join(";", wb2.Baustellen.Select(b => (b.Weg, b.Von, b.Bis, b.Stufe, b.BauherrName, b.FertigAb, b.Kosten))));
            Pruefe.Gleich((wb.Antrag!.Value.Antragsteller.Inhaber, wb.Antrag.Value.Weg, wb.Antrag.Value.Von, wb.Antrag.Value.Bis),
                (wb2.Antrag!.Value.Antragsteller.Inhaber, wb2.Antrag.Value.Weg, wb2.Antrag.Value.Von, wb2.Antrag.Value.Bis));
            Pruefe.Gleich(7, wb2.FahrtenVorjahr(werkstatt.Name));
            var werkstatt2 = w2.Betriebe.Single(b => b.Name == werkstatt.Name);
            Pruefe.Gleich(w.Fahrzeit(werkstatt, Hand, true), w2.Fahrzeit(werkstatt2, Hand, true));
            // dasselbe Jahr weiter: dieselben Fahrten zählen mit (3 + 0 → im Vorjahr 3)
            wb.Jahresende();
            wb2.Jahresende();
            Pruefe.Gleich(3, wb2.FahrtenVorjahr(werkstatt.Name));
            Pruefe.Gleich(string.Join(";", wb.Stuecke(nr)), string.Join(";", wb2.Stuecke(nr)));
            // Ansehen aus einem Antrag
            var protokoll = new List<string>();
            wb.RatSitzung(w.Jahr, protokoll.Add);
            var (g3, s3) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            Pruefe.Gleich(wb.AnsehenBonus(spieler), g3.Wirtschaft!.Wegebau.AnsehenBonus(s3!));
            // alter Spielstand ohne „Wegebau“: der Belag der Karte, Kasse leer
            var knoten = JsonNode.Parse(json)!;
            knoten["Wirtschaft"]!.AsObject().Remove("Wegebau");
            var (alt, _) = Speicherung.Laden(knoten.ToJsonString());
            Pruefe.Gleich(0.0, alt.Wirtschaft!.Wegebau.Wegekasse);
            Pruefe.Gleich(0, alt.Wirtschaft.Wegebau.Baustellen.Count);
            Pruefe.Wahr(alt.Wirtschaft.Wegebau.Stuecke(nr).All(s => s.Stufe == Belagstufe.Fahrweg && s.Austritt == 1), "Start der Karte");
        }),

        ("Wegbelag: Kartenskript „belag“ – ganzer Weg, Abschnitt, Vorgabe; unbekannter Weg oder Stufe ändert nichts", () =>
        {
            var ordner = Path.Combine(Path.GetTempPath(), "rg-belag-" + Guid.NewGuid().ToString("N"));
            try
            {
                var projekt = Projektordner.Finden()!;
                Directory.CreateDirectory(ordner);
                var datei = Path.Combine(ordner, "probestadt.json");
                File.WriteAllText(datei, Probestadt().ZuJson());
                var aus = new StringWriter();
                Pruefe.Gleich(0, Befehle.Belag(projekt, datei, "Hauptstraße", "pflaster", (50, 90), aus), aus.ToString());
                Pruefe.Gleich(0, Befehle.Belag(projekt, datei, "Landstraße_nach_Osten", "kies", null, aus), aus.ToString());
                var karte = Stadtkarte.AusDatei(datei, Path.Combine(Datenordner, "katalog.json"));
                Pruefe.Gleich("50-90 Pflaster", string.Join(", ", karte.Wege[Nr(karte, "Hauptstraße")].Belag!.Select(b => $"{b.Von}-{b.Bis} {b.Stufe}")));
                Pruefe.Gleich(Belagstufe.Kies, karte.Wege[Nr(karte, "Landstraße nach Osten")].Belag!.Single().Stufe);
                Pruefe.Gleich(0, Befehle.Belag(projekt, datei, "2", "vorgabe", null, aus), aus.ToString());
                karte = Stadtkarte.AusDatei(datei, Path.Combine(Datenordner, "katalog.json"));
                Pruefe.Gleich(null, karte.Wege[2].Belag);
                var stand = File.ReadAllText(datei);
                Pruefe.Gleich(2, Befehle.Belag(projekt, datei, "Gibtsnicht", "kies", null, aus));
                Pruefe.Gleich(2, Befehle.Belag(projekt, datei, "Hauptstraße", "gold", null, aus));
                Pruefe.Gleich(stand, File.ReadAllText(datei));
            }
            finally
            {
                if (Directory.Exists(ordner)) Directory.Delete(ordner, true);
            }
        }),
    };
}
