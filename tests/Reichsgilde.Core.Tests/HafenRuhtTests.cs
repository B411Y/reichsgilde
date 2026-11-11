using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Hafen ruht (Datenversion 70, Nutzerentscheidung vom 03.11.2026): Der Schalter <c>schiffshandel.aktiv</c> nimmt den Schiffshandel aus dem Spiel,
/// ohne den Code zu löschen. Aus: keine Schiffe (Spieler und KI), der Hafen ist ein zweiter Ort zum Handeln mit dem Markt der Stadt und im Eis zu.
/// An: alles rechnet wie vor dem Schalter – geprüft gegen Prüfsummen, die der Kern vor dieser Änderung (Branch phase5/jahreszeiten 0512c835)
/// für dieselbe Partie gerechnet hat.
/// </summary>
static class HafenRuhtTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));

    static Stadtkarte Karte(string kennung) => Stadtkarte.AusDatei(Datenpfad.Karte(kennung));

    /// <summary>
    /// Die Greybox, wie sie vor den fairen Hofstellen war (Umbau greybox-hofstellen-fair, 9.11.2026): Tischlerei Eichwald bei 22 | −13 und Maurerei
    /// Kieselbach bei 25 | −29 mit ihren alten Gassen – auf dieser Karte rechnete der Kern vor dem Schalter die Sollwerte.
    /// </summary>
    static Stadtkarte GreyboxVorDenFairenHofstellen()
    {
        var n = JsonNode.Parse(File.ReadAllText(Datenpfad.Karte("greybox")))!.AsObject();
        void Punkt(string kennung, double x, double z) => n["gebaeude"]!.AsArray().Single(g => (string)g!["kennung"]! == kennung)!["punkt"] = new JsonArray(x, z);
        void Linie(string name, params double[][] punkte) =>
            n["wege"]!.AsArray().Single(w => (string)w!["name"]! == name)!["linie"] = new JsonArray(punkte.Select(p => (JsonNode)new JsonArray(p[0], p[1])).ToArray());
        Punkt("Tischlerei_Eichwald", 22, -13);
        Punkt("Maurerei_Kieselbach", 25, -29);
        Linie("Gasse Tischlerei Eichwald", new[] { 22, -6.5 }, new[] { 12.5, -6.5 }, new[] { 0.0, 0 });
        Linie("Gasse Maurerei Kieselbach", new[] { 25, -22.5 }, new[] { 11, -22.5 }, new[] { 11.0, -13 }, new[] { 0.0, 0 });
        return Stadtkarte.AusJson(n.ToJsonString(), Gebaeudekatalog.AusDatei(Stadtkarte.KatalogZu(Datenpfad.Karte("greybox"))));
    }

    /// <summary>Eine Stadt mit Karte zu dieser Minute (ohne Häuser), Daten aus der Datei.</summary>
    static (Simulation Sim, Stadtwirtschaft W) Stadt(Wirtschaftsdaten daten, long minute = 9 * 60 - Spielzeit.TagesbeginnMinute, int startwert = 1400,
        string karte = "probestadt")
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert },
            new Spielzeit(minute), daten);
        var w = sim.Wirtschaft!;
        w.KarteSetzen(Karte(karte));
        w.EinwohnerZaehlen();
        return (sim, w);
    }

    /// <summary>
    /// Die Partie für den Vergleich „Schalter an“: Greybox, Startwert 1400, ein Mensch (Schmied) und die KI-Häuser. Der Mensch kauft gleich ein
    /// Schiff und schickt es mit dem Vorschlag der Tafel los, der Auftrag wiederholt sich. Ergebnis: SHA-256 des Spielstands ohne Daten und Karte
    /// nach jedem Jahr. (Dieselbe Rechnung lief für die Sollwerte mit dem Kern vor dem Schalter.)
    /// </summary>
    static List<string> Partie(string json, int jahre)
    {
        // Karren zum Hafen und Handgeld (Datenversion 73) wie vor dem Schalter abschalten
        json = Datenpfad.WieVorHafenziel(json);
        json = Datenpfad.WieVorBalancingHandgeld(json); // Fang der Taschendiebe, Moschus- und Fechterduft (Datenversion 75) wie vor dem Schalter
        // Blitzschaden (Datenversion 71) wie vor dem Schalter abschalten: alte Blitzwerte (Reparatur 0, keine Anteile, kein Sprung)
        var knoten = JsonNode.Parse(json)!;
        var blitz = knoten["blitz"]!.AsObject();
        foreach (var feld in new[] { "reparaturAnteil", "lagerAnteil", "waechterFaktor", "wohnhausAnteil", "sprung" }) blitz.Remove(feld);
        blitz["reparatur"] = 0;
        // Ein Lehrling zum Start (Datenversion 72) wie vor dem Schalter abschalten: ohne die Angabe zwei in der Stadt, drei im Umland
        knoten["personal"]!.AsObject().Remove("startJeBetrieb");
        foreach (var feld in Buergermessung.NeueFelder) knoten["buerger"]!.AsObject().Remove(feld); // Bürgerdichte (5.11.2026) wie vor dem Schalter
        knoten.AsObject().Remove("lebensweise"); // Lebensweise (Datenversion 74) gab es vor dem Schalter nicht
        knoten.AsObject().Remove("stress"); // Stress (Datenversion 77) ebenso wenig – die zwei neuen erworbenen Züge kommen ohne ihn nie vor
        knoten.AsObject().Remove("geheimnisse"); // Geheimnisse (Datenversion 78) ebenso wenig
        knoten["werkstatt"]!.AsObject().Remove("jedeWirkungRechnen"); // Gulden-Rechnung jeder Wirkung (Datenversion 79) ebenso wenig
        var daten = Wirtschaftsdaten.AusJson(knoten.ToJsonString());
        var (sim, spieler) = Partieaufbau.Erstellen(
            Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos },
            daten, new NeuePartie(Startvermoegen: 15_000, Beruf: "Schmied", Startplaetze: Startplatzvergabe.Vorgabe), karte: GreyboxVorDenFairenHofstellen()); // ohne Los wie vor den Hofstellen
        var w = sim.Wirtschaft!;
        var kauf = w.Ausfuehren(spieler, new SchiffKaufen());
        if (kauf.Erfolg && w.Schiffahrt.SchiffeVon(spieler).FirstOrDefault() is { } schiff
            && Kalkulation.BesteSchiffsfahrt(w, spieler, schiff.Daten, spieler.Vermoegen / 2) is { } v)
            w.Ausfuehren(spieler, new SchiffsauftragErteilen(schiff.Name, v.Ziel, v.Hinfracht, v.Rueckfracht, Wiederholen: true));
        var summen = new List<string> { $"Schiffe {w.Schiffahrt.Schiffe.Count}" };
        for (var i = 0; i < jahre; i++)
        {
            var jahr = sim.Zeit.Jahr;
            while (sim.Zeit.Jahr == jahr) sim.Tick();
            var n = JsonNode.Parse(Speicherung.Speichern(sim, null, new DateTime(2026, 1, 1)))!.AsObject();
            foreach (var schluessel in n.Select(p => p.Key).ToList())
                if (schluessel.Equals("daten", StringComparison.OrdinalIgnoreCase) || schluessel.Equals("karte", StringComparison.OrdinalIgnoreCase)
                    || schluessel.Equals("meldungen", StringComparison.OrdinalIgnoreCase)) // der Verlauf der Meldungen (Stufe 2, 6.11.2026) – die Simulation liest ihn nie
                    n.Remove(schluessel);
            // Das Feld „Blitz“ (Einschläge im Spielstand, seit dem Blitzschaden) gab es vor dem Schalter nicht
            foreach (var schluessel in n.Select(p => p.Key).Where(k => k.Equals("wirtschaft", StringComparison.OrdinalIgnoreCase)).ToList())
                n[schluessel]!.AsObject().Remove("Blitz");
            Spielstandfelder.OhneLeere(n, Spielstandfelder.Hafenziel); // neue Felder (Datenversion 73), solange sie nichts tragen
            Spielstandfelder.Ohne(n, Spielstandfelder.NurLaden); // nur fürs Laden (Datenversion 75), durchgehend ohne Wirkung
            Spielstandfelder.Ohne(n, Spielstandfelder.Erbgut); // Allele für den 3D-Kopf: die Simulation liest sie nie (Bart der Zwerginnen, 11.11.2026)
            Spielstandfelder.Ohne(n, Spielstandfelder.Hauschronik); // die Hauschronik (Datenversion 83): die Simulation liest sie nie
            summen.Add(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(n.ToJsonString()))).ToLowerInvariant());
        }
        summen[0] += $" → {w.Schiffahrt.Schiffe.Count}, Fahrten {w.Schiffahrt.Schiffe.Sum(s => s.Fahrten)}";
        return summen;
    }

    /// <summary>
    /// Sollwerte der Partie mit laufendem Schiffshandel, gerechnet mit dem Kern vor dem Schalter (phase5/jahreszeiten 0512c835, Datenversion 69):
    /// erste Zeile Schiffe am Anfang → am Ende und Fahrten, dann die Prüfsumme nach jedem Jahr.
    /// </summary>
    static readonly string[] Sollwerte =
    {
        "Schiffe 1 → 1, Fahrten 6",
        // Ohne das Erbgut der Personen (11.11.2026): gerechnet auf main 770c374a mit demselben Ablauf – dort mit Erbgut noch gleich den
        // Sollwerten vor dem Schalter (eff1eef3…, 46ceacc4…, 37185b3f…).
        "251fc776248248679ed221f56e28bef2301978c6580469534b790d3dc892a754",
        "b01bd17cc3d054c55c2c038e086ed6bfc55e9d744b08a7dc73486eac310c1bed",
        "317386f4506249b9a64121890ac1eda08760a0a4b0f8cee904cf28d9e9b16055",
    };

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Hafen ruht: Datenversion 70, der Schalter schiffshandel.aktiv steht in wirtschaft.json auf false; ohne Angabe läuft der Schiffshandel (alte Daten)", () =>
        {
            var d = Wirtschaftsdaten.AusJson(Json.Value);
            Pruefe.Gleich(83, d.Version);
            Pruefe.Wahr(d.Schiffshandel is { Aktiv: false }, "schiffshandel.aktiv: false");
            Pruefe.Wahr(Wirtschaftsdaten.AusJson(Datenpfad.MitSchiffshandel(Json.Value)).Schiffshandel is { Aktiv: true }, "MitSchiffshandel: an");
            var ohne = Json.Value.Replace("\"schiffshandel\": {\n    \"aktiv\": false,", "\"schiffshandel\": {");
            Pruefe.Wahr(ohne != Json.Value, "Zeile gefunden");
            Pruefe.Wahr(Wirtschaftsdaten.AusJson(ohne).Schiffshandel is { Aktiv: true }, "ohne Angabe: an");
        }),

        ("Hafen ruht: Der Hafen bleibt (HatHafen, Liste der Häfen), aber niemand kann ein Schiff kaufen oder losschicken – mit klarer Begründung", () =>
        {
            var (_, w) = Stadt(Wirtschaftsdaten.AusJson(Json.Value));
            var k = w.KontoEroeffnen("Haus Reeder", startvermoegen: 50_000);
            var s = w.Schiffahrt;
            Pruefe.Wahr(s.HatHafen && s.Haefen.Count > 0, "Hafen da");
            Pruefe.Wahr(s.Ruht && !s.IstAktiv, "ruht, kein Schiffshandel");
            Pruefe.Wahr(s.KaufVerboten(k) is { } grund && grund.Contains("ruht"), s.KaufVerboten(k) ?? "kein Grund");
            var kauf = w.Ausfuehren(k, new SchiffKaufen());
            Pruefe.Wahr(!kauf.Erfolg && kauf.Meldung.Contains("ruht"), kauf.Meldung);
            Pruefe.Gleich(0, s.Schiffe.Count);
            var auftrag = new Schiffsauftrag("Eisenfurt", new Dictionary<string, int>(), new Dictionary<string, int> { ["Holz"] = 10 });
            Pruefe.Wahr(s.AuftragVerboten(k, "Möwe", auftrag) is { } nein && nein.Contains("ruht"), "Auftrag verboten");
            Pruefe.Wahr(!w.Ausfuehren(k, new SchiffsauftragErteilen("Möwe", "Eisenfurt", new Dictionary<string, int>(), new Dictionary<string, int>())).Erfolg, "Befehl abgelehnt");
            // Mit laufendem Schiffshandel geht der Kauf in derselben Stadt
            var (_, an) = Stadt(Wirtschaftsdaten.AusJson(Datenpfad.MitSchiffshandel(Json.Value)));
            var k2 = an.KontoEroeffnen("Haus Reeder", startvermoegen: 50_000);
            Pruefe.Wahr(an.Schiffahrt.IstAktiv && !an.Schiffahrt.Ruht && an.Ausfuehren(k2, new SchiffKaufen()).Erfolg, "an: Kauf geht");
        }),

        ("Hafen ruht: Am Hafen handelt man mit dem Markt der Stadt – dieselben Waren, derselbe Vorrat, dieselben Preise, auch nach einem Handel", () =>
        {
            var (sim, w) = Stadt(Wirtschaftsdaten.AusJson(Json.Value));
            var markt = w.Schiffahrt.HafenMarkt;
            Pruefe.Wahr(ReferenceEquals(markt, w.Markt), "derselbe Markt");
            var holz = w.Markt["Holz"].Preis;
            var k = w.KontoEroeffnen("Haus Reeder", startvermoegen: 5_000);
            for (var i = 0; i < 120; i++) sim.Tick();
            foreach (var ware in w.Daten.Waren.Keys)
                Pruefe.Gleich(w.Markt[ware].Preis, markt[ware].Preis, ware);
            Pruefe.Wahr(holz > 0 && k.Vermoegen > 0, "Preise und Konto");
        }),

        ("Hafen ruht: Im Winter ist der Hafen zu (Eisschollen) – die ganze Jahreszeit, mild oder streng, auch im Eis; Frühling, Sommer und Herbst offen; ohne Jahreszeiten immer offen; ohne Hafen nie", () =>
        {
            var daten = Wirtschaftsdaten.AusJson(Json.Value);
            long Minute(int jahr, int stunde) => jahr * (long)Spielzeit.MinutenProTag + stunde * 60 - Spielzeit.TagesbeginnMinute;
            for (var jahr = 0; jahr < 8; jahr++)
                foreach (var stunde in new[] { 4, 12, 22 })
                {
                    var (_, w) = Stadt(daten, Minute(jahr, stunde));
                    var winter = w.Uhr.Jahreszeit == Jahreszeit.Winter;
                    Pruefe.Gleich(jahr % 4 == 3, winter, $"Jahr {jahr}: Winter");
                    Pruefe.Gleich(!winter, w.Schiffahrt.HafenOffen, $"Jahr {jahr}, {stunde} Uhr");
                }
            // Im Eis des strengen Winters ebenso zu
            int startwert = -1;
            for (var s = 1; s < 5000 && startwert < 0; s++)
                if (new Stadtwirtschaft(daten, Partieeinstellungen.Standard.StartStatut, s).Jahreszeiten.WinterVon(1403) is { Streng: true, BisJahresende: false })
                    startwert = s;
            Pruefe.Wahr(startwert > 0, "Startwert mit strengem Winter 1403");
            var (sim, eis) = Stadt(daten, 3 * (long)Spielzeit.MinutenProTag, startwert);
            var lage = eis.Jahreszeiten.DiesesJahr!;
            while (sim.Zeit.MinuteImTag <= lage.ZufrierenMinute) sim.Tick();
            Pruefe.Wahr(eis.Jahreszeiten.FlussZugefroren && !eis.Schiffahrt.HafenOffen, "im Eis zu");
            // Ohne Abschnitt „jahreszeiten“ kein Winter mit Folgen: offen
            var (_, ohne) = Stadt(Wirtschaftsdaten.AusJson(Datenpfad.OhneJahreszeiten(Json.Value)), Minute(3, 12));
            Pruefe.Wahr(ohne.Schiffahrt.HafenOffen, "ohne Jahreszeiten im Jahr 3 offen");
            var ohneKarte = new Stadtwirtschaft(daten, Partieeinstellungen.Standard.StartStatut, 1);
            Pruefe.Wahr(!ohneKarte.Schiffahrt.HatHafen && !ohneKarte.Schiffahrt.HafenOffen, "ohne Karte kein Hafen");
        }),

        ("Hafen ruht: Die KI kauft in 6 Jahren Greybox kein Schiff (mit laufendem Schiffshandel kauft sie welche); die fernen Märkte bleiben unberührt", () =>
        {
            int Schiffe(string json, out bool unberuehrt)
            {
                var (sim, _) = Partieaufbau.Erstellen(
                    Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = Schwierigkeit.Schwer },
                    Wirtschaftsdaten.AusJson(json), new NeuePartie(Startvermoegen: 15_000, Rivale: KiCharakter.Wagemutig), karte: Karte("greybox"));
                var w = sim.Wirtschaft!;
                var gekauft = 0;
                w.Meldung += m => { if (m.Contains(" kauft ") && m.Contains("Flussschiff")) gekauft++; };
                while (sim.Zeit.Jahr < 1406) sim.Tick();
                unberuehrt = w.Schiffahrt.Staedte.All(s => w.Daten.Waren.Keys.All(x => s.Druck(x) == 0));
                return Math.Max(gekauft, w.Schiffahrt.Schiffe.Count);
            }
            Pruefe.Gleich(0, Schiffe(Json.Value, out var unberuehrt));
            Pruefe.Wahr(unberuehrt, "kein Druck auf den fernen Märkten");
            Pruefe.Wahr(Schiffe(Datenpfad.MitSchiffshandel(Json.Value), out _) > 0, "mit Schiffshandel kauft die KI Schiffe");
        }),

        ("Hafen ruht: Ein Spielstand mit Schiffen lädt mit Daten, in denen der Schiffshandel ruht – die Schiffe sind verworfen, kein Absturz", () =>
        {
            var an = Wirtschaftsdaten.AusJson(Datenpfad.MitSchiffshandel(Json.Value));
            var (sim, w) = Stadt(an);
            var k = w.KontoEroeffnen("Haus Reeder", startvermoegen: 20_000);
            Pruefe.Wahr(w.Ausfuehren(k, new SchiffKaufen()).Erfolg, "Schiff gekauft");
            var stand = JsonNode.Parse(Speicherung.Speichern(sim, k, new DateTime(2026, 1, 1)))!.AsObject();
            // Die mitgespeicherten Daten auf „ruht“ stellen (so käme es nur von Hand)
            var datenKnoten = stand.First(p => p.Key.Equals("daten", StringComparison.OrdinalIgnoreCase)).Value!;
            var schiffshandel = datenKnoten.AsObject().First(p => p.Key.Equals("schiffshandel", StringComparison.OrdinalIgnoreCase)).Value!.AsObject();
            var aktiv = schiffshandel.FirstOrDefault(p => p.Key.Equals("aktiv", StringComparison.OrdinalIgnoreCase)).Key ?? "aktiv";
            schiffshandel[aktiv] = false;
            var (geladen, _) = Speicherung.Laden(stand.ToJsonString());
            var g = geladen.Wirtschaft!;
            Pruefe.Wahr(g.Schiffahrt.Ruht, "ruht nach dem Laden");
            Pruefe.Gleich(0, g.Schiffahrt.Schiffe.Count);
            Pruefe.Gleich(1, g.Schiffahrt.BeimLadenVerworfen);
            for (var i = 0; i < 60; i++) geladen.Tick();
        }),

        ("Hafen ruht: Schalter an – die Partie mit Schiff (Greybox, 3 Jahre) ist bitgleich mit dem Kern vor dem Schalter", () =>
        {
            var ist = Partie(Datenpfad.MitSchiffshandel(Json.Value), 3);
            Pruefe.Gleich(string.Join("\n", Sollwerte), string.Join("\n", ist));
        }),
    };
}
