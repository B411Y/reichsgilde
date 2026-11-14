using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Kirche;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Der Domprediger (Nutzer, 11.11.2026; Datenversion 86, Abschnitt kirche.dom): Der Stadt-Dom ist ein Amt auf Zeit. Der Rat wählt,
/// 3 Jahre Amtszeit mit Wiederwahl, kein Gehalt, 60 % der Gemeinde seiner Konfession; nur Prediger mit eigener Kirche (F1),
/// Absetzen nach Verurteilung (F2). Jeder Test mit Gegenprobe.
/// </summary>
static class DompredigerTests
{
    static readonly string Json = File.ReadAllText(Datenpfad.Wirtschaft());
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json));

    /// <summary>Dieselben Daten ohne Dom-Amt (wie vor Datenversion 86) – für die Gegenprobe.</summary>
    static readonly Lazy<Wirtschaftsdaten> OhneDom = new(() => Wirtschaftsdaten.AusJson(Json.Replace(DomZeile, "")));

    const string DomZeile = ",\n    \"dom\": { \"anteil\": 0.6, \"amtsjahre\": 3, \"predigtFaktor\": 1.5, \"gunst\": 3 }";

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: daten ?? Daten.Value);

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    /// <summary>Ein Haus mit Familie dieses Glaubens, Geld und Aktionspunkten.</summary>
    static Konto Haus(Stadtwirtschaft w, string name, Konfession glaube)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: 50_000);
        w.Familien.Gruenden(k, Volk.Mensch, Geschlecht.Mann, alter: 30, glaube: glaube);
        k.Aktionspunkte.Setzen(12);
        return k;
    }

    /// <summary>Ein Prediger mit eigener Kirche (ein Geselle).</summary>
    static (Konto K, Betrieb Kirche) Prediger(Stadtwirtschaft w, string name, Konfession glaube)
    {
        var k = Haus(w, name, glaube);
        k.BerufLernen("Prediger");
        var b = w.BetriebEroeffnen($"Kapelle {name}", Betriebsart.Kirche, k, startkarren: "");
        w.Einstellen(b, "Geselle", 1);
        return (k, b);
    }

    /// <summary>Setzt das Haus auf einen Sitz im Rat (statt des Ratsherrn).</summary>
    static void InDenRat(Stadtwirtschaft w, Konto k, string amt = "Zöllner")
    {
        var a = w.Rat.AmtNamens(amt)!;
        a.Ratsherr = null;
        a.Konto = k;
    }

    /// <summary>Das Haus gibt seine Kirche ab (verkauft sie; vorher eine Gießerei, weil ein Betrieb bleiben muss).</summary>
    static void KircheWeg(Stadtwirtschaft w, Betrieb kirche)
    {
        w.BetriebEroeffnen($"Gießerei {kirche.Konto.Inhaber}", Betriebsart.Giesserei, kirche.Konto, startkarren: "");
        var e = w.Ausfuehren(kirche.Konto, new BetriebVerkaufen(kirche.Name));
        Pruefe.Wahr(e.Erfolg, e.Meldung);
    }

    /// <summary>Der Kirchenstand im Spielstand (das Objekt mit „Beichtwissen“).</summary>
    static System.Text.Json.Nodes.JsonObject? KirchenStand(System.Text.Json.Nodes.JsonNode? knoten) => knoten switch
    {
        System.Text.Json.Nodes.JsonObject o when o.ContainsKey("Beichtwissen") => o,
        System.Text.Json.Nodes.JsonObject o => o.Select(x => KirchenStand(x.Value)).FirstOrDefault(x => x is not null),
        System.Text.Json.Nodes.JsonArray a => a.Select(KirchenStand).FirstOrDefault(x => x is not null),
        _ => null,
    };

    /// <summary>Entfernt das Feld „Dom“ aus dem Kirchenstand eines Spielstands (wie ein alter Stand).</summary>
    static bool EntferneDom(System.Text.Json.Nodes.JsonNode knoten)
    {
        if (knoten is System.Text.Json.Nodes.JsonObject o)
        {
            if (o.ContainsKey("Beichtwissen") && o.ContainsKey("Dom")) { o.Remove("Dom"); return true; }
            return o.Select(x => x.Value).ToList().Any(x => x is not null && EntferneDom(x));
        }
        return knoten is System.Text.Json.Nodes.JsonArray a && a.ToList().Any(x => x is not null && EntferneDom(x));
    }

    static string Grund(Befehlsergebnis e) => e.Erfolg ? "" : e.Meldung;

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Domprediger: Daten – Anteil 60 %, 3 Amtsjahre, Predigt × 1,5, Gunst 3, Bewerbung 1 AP; ohne kirche.dom kein Amt; falsche Werte abgewiesen", () =>
        {
            var d = Daten.Value.Kirche!.Dom!;
            Pruefe.Gleich(0.6, d.Anteil);
            Pruefe.Gleich(3, d.Amtsjahre);
            Pruefe.Gleich(1.5, d.PredigtFaktor);
            Pruefe.Gleich(3.0, d.Gunst);
            Pruefe.Gleich(87, Daten.Value.Version);
            Pruefe.Gleich(1, Daten.Value.Aktionspunkte!.KostenFuer(new DomBewerben()));
            Pruefe.Gleich(0, Daten.Value.Aktionspunkte!.KostenFuer(new DomStimme(null)));
            Pruefe.Wahr(Json.Contains(DomZeile), "Die Zeile für die Gegenprobe passt nicht mehr zu wirtschaft.json.");
            Pruefe.Gleich(null, OhneDom.Value.Kirche!.Dom);
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Replace("\"anteil\": 0.6, \"amtsjahre\": 3", "\"anteil\": 1.2, \"amtsjahre\": 3")), "kirche.dom.anteil");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Replace("\"amtsjahre\": 3, \"predigtFaktor\"", "\"amtsjahre\": 0, \"predigtFaktor\"")), "kirche.dom.amtsjahre");

            var mit = NeueSimulation().Wirtschaft!;
            Pruefe.Wahr(mit.Domamt.IstAktiv, "mit kirche.dom gibt es das Amt");
            var ohne = NeueSimulation(OhneDom.Value).Wirtschaft!;
            Pruefe.Wahr(!(ohne.Domamt.IstAktiv), "ohne kirche.dom kein Amt");
            var (p, _) = Prediger(ohne, "Haus Psalm", Konfession.Katholisch);
            Pruefe.Wahr((Grund(ohne.Ausfuehren(p, new DomBewerben()))).Contains("kein Amt des Dompredigers"), Grund(ohne.Ausfuehren(p, new DomBewerben())));
            Pruefe.Gleich(12, p.Aktionspunkte.Vorrat); // abgewiesen kostet nichts
        }),

        ("Domprediger: Bewerben nur als Prediger mit eigener Kirche, kirchlichem Glauben und – gilt einer – dem Stadtglauben", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var ohneKirche = Haus(w, "Haus Leer", Konfession.Katholisch);
            ohneKirche.BerufLernen("Prediger");
            Pruefe.Wahr((w.Domamt.BewerbenVerboten(ohneKirche) ?? "").Contains("eigener Kirche"), w.Domamt.BewerbenVerboten(ohneKirche) ?? "");
            var (kath, _) = Prediger(w, "Haus Psalm", Konfession.Katholisch);
            var (ev, _) = Prediger(w, "Haus Luther", Konfession.Evangelisch);
            Pruefe.Gleich(null, w.Domamt.BewerbenVerboten(kath));
            Pruefe.Gleich(null, w.Domamt.BewerbenVerboten(ev)); // Glaubensfreiheit: jeder kirchliche Prediger (F1)

            w.Glaube.Stadtglaube = Konfession.Katholisch;
            Pruefe.Wahr((w.Domamt.BewerbenVerboten(ev) ?? "").Contains("Stadtglaube"), w.Domamt.BewerbenVerboten(ev) ?? "");
            Pruefe.Gleich(null, w.Domamt.BewerbenVerboten(kath));

            var e = w.Ausfuehren(kath, new DomBewerben());
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(11, kath.Aktionspunkte.Vorrat);
            Pruefe.Wahr((Grund(w.Ausfuehren(kath, new DomBewerben()))).Contains("schon"), Grund(w.Ausfuehren(kath, new DomBewerben())));
            Pruefe.Gleich(1, w.Domamt.Bewerbungen.Count);
            Pruefe.Wahr((Grund(w.Ausfuehren(ev, new DomBewerben()))).Contains("Stadtglaube"), Grund(w.Ausfuehren(ev, new DomBewerben())));
            Pruefe.Gleich(12, ev.Aktionspunkte.Vorrat);
        }),

        ("Domprediger: Der Rat wählt am Jahresende – 3 Jahre im Amt; die Stimme eines Hauses im Rat zählt (DomStimme); keine Wahl mitten in der Amtszeit", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (a, _) = Prediger(w, "Haus Psalm", Konfession.Katholisch);
            var (b, _) = Prediger(w, "Haus Weihrauch", Konfession.Katholisch);
            var rat = Haus(w, "Haus Ratsherr", Konfession.Katholisch);
            InDenRat(w, rat);
            Pruefe.Wahr(w.Ausfuehren(a, new DomBewerben()).Erfolg, "a bewirbt sich");
            Pruefe.Wahr(w.Ausfuehren(b, new DomBewerben()).Erfolg, "b bewirbt sich");
            Pruefe.Wahr((Grund(w.Ausfuehren(a, new DomStimme(b.Inhaber)))).Contains("Mitglieder des Rates"), Grund(w.Ausfuehren(a, new DomStimme(b.Inhaber)))); // a sitzt nicht im Rat
            Pruefe.Wahr((Grund(w.Ausfuehren(rat, new DomStimme("Haus Niemand")))).Contains("nicht zur Wahl"), Grund(w.Ausfuehren(rat, new DomStimme("Haus Niemand"))));
            Pruefe.Wahr(w.Ausfuehren(rat, new DomStimme(b.Inhaber)).Erfolg, "Stimme für b");
            Pruefe.Gleich(b, w.Domamt.StimmeVon(rat)!.Haus);
            Pruefe.Gleich(a, w.Domamt.VorgabeStimme()!.Haus); // ohne Stimme: der erste Bewerber
            Pruefe.Gleich(2, w.Domamt.Kandidaten().Count);

            var punkt = w.Rat.Sitzungsvorschau().Single(p => p.Thema == Reichsgilde.Core.Politik.Sitzungsthema.Domprediger);
            Pruefe.Gleich(2, punkt.Kandidaten.Count);
            Pruefe.Gleich(w.Rat.Aemter.Count(x => x.IstBesetzt), punkt.Stimmen.Count); // alle Sitze stimmen ab (die Kandidaten sitzen nicht im Rat)
            Pruefe.Gleich(b.Inhaber, punkt.Stimmen.Single(s => s.Haus == rat).Kandidat);
            var gewaehlt = punkt.Vorschau.Gewaehlt;

            var jahr = sim.Zeit.Jahr;
            JahrBeenden(sim);
            Pruefe.Wahr(w.Domamt.Inhaber == a || w.Domamt.Inhaber == b, "einer der beiden ist Domprediger");
            if (gewaehlt is not null) Pruefe.Gleich(gewaehlt, w.Domamt.Inhaber!.Inhaber);
            Pruefe.Gleich(jahr + 3, w.Domamt.Bis);
            Pruefe.Wahr(w.Rat.LetzteSitzung.Any(t => t.StartsWith($"Wahl {jahr} – Domprediger", StringComparison.Ordinal) && t.Contains($"bis Ende {jahr + 3}")),
                string.Join(" | ", w.Rat.LetzteSitzung));
            Pruefe.Gleich(0, w.Domamt.Bewerbungen.Count); // Bewerbungen verfallen mit der Sitzung
            Pruefe.Gleich(null, w.Domamt.StimmeVon(rat));

            // Mitten in der Amtszeit: keine Bewerbung, kein Punkt in der Sitzung.
            var verlierer = w.Domamt.Inhaber == a ? b : a;
            Pruefe.Wahr((Grund(w.Ausfuehren(verlierer, new DomBewerben()))).Contains($"vergeben bis Ende {jahr + 3}"), Grund(w.Ausfuehren(verlierer, new DomBewerben())));
            Pruefe.Wahr(!(w.Rat.Sitzungsvorschau().Any(p => p.Thema == Reichsgilde.Core.Politik.Sitzungsthema.Domprediger)), "kein Punkt mitten in der Amtszeit");
        }),

        ("Domprediger: Nach 3 Jahren steht das Amt wieder zur Wahl – Wiederwahl mit Amtsbonus; erfüllt der Inhaber die Bedingungen nicht mehr und bewirbt sich niemand, wird es frei", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (a, kirche) = Prediger(w, "Haus Psalm", Konfession.Katholisch);
            var jahr = sim.Zeit.Jahr;
            w.Domamt.Einsetzen(a, jahr + 1); // gewählt im Vorjahr der Amtszeit … das letzte Amtsjahr ist das nächste
            Pruefe.Wahr(!(w.Domamt.IstWahljahr(jahr)), "dieses Jahr keine Wahl");
            Pruefe.Gleich(0, w.Domamt.Kandidaten().Count);
            JahrBeenden(sim);
            Pruefe.Gleich(a, w.Domamt.Inhaber);
            Pruefe.Wahr(w.Domamt.IstWahljahr(jahr + 1), "letztes Amtsjahr");
            Pruefe.Wahr((w.Domamt.BewerbenVerboten(a) ?? "").Contains("ohnehin wieder zur Wahl"), w.Domamt.BewerbenVerboten(a) ?? "");
            var kandidat = w.Domamt.Kandidaten().Single();
            Pruefe.Wahr(kandidat.IstAmtsinhaber && kandidat.Haus == a, "der Inhaber steht von selbst zur Wahl");
            var (b, _) = Prediger(w, "Haus Weihrauch", Konfession.Katholisch);
            Pruefe.Gleich(null, w.Domamt.BewerbenVerboten(b)); // im letzten Amtsjahr darf sich jeder bewerben
            var r = w.Rat.Ratsherren.First(x => w.Glaube.GlaubeVon(x) == Konfession.Katholisch);
            Pruefe.Ungefaehr(w.Domamt.Punkte(r, a) - w.Rat.Daten.Gunst.Amtsbonus,
                w.Rat.Ansehen(a) + w.Rat.Meinung(r, a) + w.Familien.RhetorikPunkte(a), 1e-9); // Amtsbonus nur für den Inhaber

            JahrBeenden(sim); // Sitzung des letzten Amtsjahres: nur der Inhaber steht zur Wahl
            Pruefe.Gleich(a, w.Domamt.Inhaber);
            Pruefe.Gleich(jahr + 1 + 3, w.Domamt.Bis);
            Pruefe.Wahr(w.Rat.LetzteSitzung.Any(t => t.Contains("Domprediger") && t.Contains("bleibt im Amt")), string.Join(" | ", w.Rat.LetzteSitzung));

            // Gegenprobe: ohne Kirche steht er nicht mehr zur Wahl – nach der Sitzung des letzten Amtsjahres ist das Amt frei.
            w.Domamt.Einsetzen(a, sim.Zeit.Jahr);
            KircheWeg(w, kirche);
            Pruefe.Gleich(0, w.Domamt.Kandidaten().Count);
            JahrBeenden(sim);
            Pruefe.Gleich(null, w.Domamt.Inhaber);
            Pruefe.Gleich(0, w.Domamt.Bis);
            Pruefe.Wahr(w.Rat.LetzteSitzung.Any(t => t.Contains("Amtszeit abgelaufen")), string.Join(" | ", w.Rat.LetzteSitzung));
        }),

        ("Domprediger: Kollekte – der Dom nimmt 60 % der Gemeinde seiner Konfession, die eigene Kirche den Rest; kein Gehalt aus der Stadtkasse", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (a, kirche) = Prediger(w, "Haus Psalm", Konfession.Katholisch);
            var kd = w.Kirchenwesen.Daten;
            var kath = w.Glaube.Anteil(Konfession.Katholisch);
            var ev = w.Glaube.Anteil(Konfession.Evangelisch);
            var kirchen = w.Kirchenwesen.Kirchen.ToList();
            Pruefe.Gleich(1, kirchen.Count);

            // Gegenprobe ohne Domprediger: alles in die Kirche (die Evangelischen als Fremde mit halber Spende), der Dom bekommt nichts.
            var vorher = w.Kirchenwesen.Verteilen(kirchen, out var leer);
            Pruefe.Gleich((0.0, 0.0), leer);
            Pruefe.Ungefaehr(kd.GlaeubigeJeJahr * (kath + ev), vorher[kirche].Glaeubige, 1e-9);

            w.Domamt.Einsetzen(a, 1500);
            var nachher = w.Kirchenwesen.Verteilen(kirchen, out var dom);
            Pruefe.Ungefaehr(kd.GlaeubigeJeJahr * (kath + ev) * 0.6, dom.Glaeubige, 1e-9); // die Evangelischen ohne Kirche gehen auch in den Dom
            Pruefe.Ungefaehr(kd.GlaeubigeJeJahr * kath * 0.6 * kd.SpendeJeGlaeubigem + kd.GlaeubigeJeJahr * ev * 0.6 * kd.SpendeJeGlaeubigem * 0.5, dom.Kollekte, 1e-9);
            Pruefe.Ungefaehr(kd.GlaeubigeJeJahr * (kath + ev) * 0.4, nachher[kirche].Glaeubige, 1e-9);
            Pruefe.Ungefaehr(vorher[kirche].Kollekte, nachher[kirche].Kollekte + dom.Kollekte, 1e-9); // nichts geht verloren, nichts kommt dazu

            // Gibt es keine Kirche seiner Konfession, nimmt der Dom die ganze Gemeinde (ein evangelischer Domprediger bei Glaubensfreiheit).
            var (luther, _) = Prediger(w, "Haus Luther", Konfession.Evangelisch);
            var nurKath = new List<Betrieb> { kirche };
            w.Domamt.Einsetzen(luther, 1500);
            w.Kirchenwesen.Verteilen(nurKath, out var domEv);
            Pruefe.Ungefaehr(kd.GlaeubigeJeJahr * ev + kd.GlaeubigeJeJahr * kath * 0, domEv.Glaeubige, 1e-9); // die Katholiken haben ihre Kirche

            // Zur Messe: Die Domkollekte geht an das Haus (steuerfrei), kein Amtseinkommen.
            w.Domamt.Einsetzen(a, 1500);
            var steuerVorher = a.Laufend.Steuer;
            w.Kirchenwesen.Stunde(new Reichsgilde.Core.Zeit.Spielzeit(kd.MesseUm * 60 - Reichsgilde.Core.Zeit.Spielzeit.TagesbeginnMinute));
            Pruefe.Wahr(a.Laufend.Domkollekte > 0, "Domkollekte gebucht");
            Pruefe.Ungefaehr(w.Domamt.DiesesJahr.Kollekte, a.Laufend.Domkollekte, 1e-9);
            Pruefe.Gleich(steuerVorher, a.Laufend.Steuer);
            Pruefe.Gleich(0.0, a.Laufend.Amtseinkommen);
            w.Rat.EinkommenZahlen();
            Pruefe.Gleich(0.0, a.Laufend.Amtseinkommen); // kein Gehalt aus der Stadtkasse
        }),

        ("Domprediger: Das Amt endet bei anderem Stadtglauben, beim Übertritt und bei Bankrott; ein Stadtglaube, dem er folgt, lässt es bestehen", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (a, _) = Prediger(w, "Haus Psalm", Konfession.Katholisch);
            w.Domamt.Einsetzen(a, 1500);
            var rat = Haus(w, "Haus Kreuz", Konfession.Katholisch);
            InDenRat(w, rat);
            Pruefe.Wahr(w.Ausfuehren(rat, new StadtglaubeBeantragen(Konfession.Katholisch)).Erfolg, "Antrag");
            JahrBeenden(sim);
            Pruefe.Gleich((Konfession?)Konfession.Katholisch, w.Glaube.Stadtglaube);
            Pruefe.Gleich(a, w.Domamt.Inhaber); // Gegenprobe: sein eigener Glaube

            w.Glaube.Stadtglaube = null;
            var e = w.Ausfuehren(a, new Konvertieren(Konfession.Evangelisch));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(null, w.Domamt.Inhaber);

            var (b, _) = Prediger(w, "Haus Weihrauch", Konfession.Evangelisch);
            w.Domamt.Einsetzen(b, 1500);
            w.Glaube.Stadtglaube = Konfession.Evangelisch;
            w.Domamt.StadtglaubeGeaendert(_ => { });
            Pruefe.Gleich(b, w.Domamt.Inhaber);
            w.Glaube.Stadtglaube = Konfession.Katholisch;
            w.Domamt.StadtglaubeGeaendert(_ => { });
            Pruefe.Gleich(null, w.Domamt.Inhaber);

            w.Domamt.Einsetzen(b, 1500);
            w.Rat.Ausscheiden(b);
            Pruefe.Gleich(null, w.Domamt.Inhaber);
        }),

        ("Domprediger: Verurteilt – der Domprediger wird abgesetzt; vor dem Urteil bleibt er im Amt", () =>
        {
            var daten = Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte(Json.Replace("\"spitzelChance\": 0.5", "\"spitzelChance\": 1.0")));
            var sim = NeueSimulation(daten);
            var w = sim.Wirtschaft!;
            var klaeger = Haus(w, "Familie Klage", Konfession.Katholisch);
            var (taeter, _) = Prediger(w, "Familie Tat", Konfession.Katholisch);
            var nachruecker = w.Rat.Ratsherren.Where(r => w.Rat.Aemter.All(a => a.Ratsherr != r)).ToList();
            for (var i = 0; i < 4; i++)
                Pruefe.Wahr(w.Ausfuehren(taeter, new Schenken(nachruecker[i % nachruecker.Count].Name, 800)).Erfolg, "Geschenk");
            Pruefe.Wahr(w.Ausfuehren(klaeger, new SpitzelAnsetzen(taeter.Inhaber)).Erfolg, "Spitzel");
            JahrBeenden(sim);
            w.Domamt.Einsetzen(taeter, sim.Zeit.Jahr + 2);
            w.Ausfuehren(klaeger, new Anklagen(taeter.Inhaber));
            var prognose = w.Justiz.Prognose(w.Justiz.VerfahrenGegen(klaeger, taeter)!);
            Pruefe.Wahr(Stadtgericht.IstSchuldig(prognose), "Prognose: schuldig – " + string.Join(", ", prognose.Select(p => $"{p.Richter.Rolle} {p.Ueberzeugung:0.0}")) + $" Beweise {w.Justiz.Beweise(klaeger, taeter).Count()}");
            Pruefe.Gleich(taeter, w.Domamt.Inhaber); // Gegenprobe: vor dem Urteil
            JahrBeenden(sim);
            var urteil = w.Justiz.LetzteUrteile.Single();
            Pruefe.Wahr(urteil.Schuldig, urteil.Text);
            Pruefe.Wahr((urteil.Text).Contains("Verlust des Amtes Domprediger"), urteil.Text);
            Pruefe.Gleich(null, w.Domamt.Inhaber);
        }),

        ("Domprediger: Wirkung – Predigt im Dom × 1,5 (auch ohne Kanzel), Gunst +3 bei den Ratsherren seiner Konfession am Jahresende", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (a, kirche) = Prediger(w, "Haus Psalm", Konfession.Katholisch);
            var normal = w.Glaube.Bekehrungskraft(a);
            w.Domamt.Einsetzen(a, 1500);
            Pruefe.Ungefaehr(normal * 1.5, w.Glaube.Bekehrungskraft(a), 1e-12);
            KircheWeg(w, kirche);
            Pruefe.Gleich(null, w.Glaube.KanzelVon(a));
            Pruefe.Gleich(null, w.Glaube.BekehrenVerboten(a)); // der Dom ist seine Kanzel
            var e = w.Ausfuehren(a, new Bekehrungspredigt());
            Pruefe.Wahr(e.Erfolg && e.Meldung.Contains("im Dom"), e.Meldung);

            var kath = w.Rat.Ratsherren.First(r => w.Glaube.GlaubeVon(r) == Konfession.Katholisch);
            var ev = w.Rat.Ratsherren.First(r => w.Glaube.GlaubeVon(r) == Konfession.Evangelisch);
            var (kVorher, eVorher) = (w.Rat.Gunst(kath, a), w.Rat.Gunst(ev, a));
            w.Domamt.Jahresende();
            Pruefe.Ungefaehr(kVorher + 3, w.Rat.Gunst(kath, a), 1e-9);
            Pruefe.Ungefaehr(eVorher, w.Rat.Gunst(ev, a), 1e-9); // Gegenprobe: andere Konfession

            // Gegenprobe ohne Amt: keine Predigt ohne Kanzel, keine Gunst.
            var (b, kirche2) = Prediger(w, "Haus Weihrauch", Konfession.Evangelisch);
            KircheWeg(w, kirche2);
            Pruefe.Wahr((w.Glaube.BekehrenVerboten(b) ?? "").Contains("eigenen Kirche"), w.Glaube.BekehrenVerboten(b) ?? "");
        }),

        ("Domprediger: Speichern und Laden – Inhaber, Amtsende, Bewerbungen, Stimmen und Kirchenbuch bleiben; alte Spielstände ohne Dom laden mit freiem Amt", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (a, _) = Prediger(w, "Haus Psalm", Konfession.Katholisch);
            var (b, _) = Prediger(w, "Haus Weihrauch", Konfession.Katholisch);
            var rat = Haus(w, "Haus Ratsherr", Konfession.Katholisch);
            InDenRat(w, rat);
            w.Domamt.Einsetzen(a, sim.Zeit.Jahr);
            Pruefe.Wahr(w.Ausfuehren(b, new DomBewerben()).Erfolg, "Bewerbung");
            Pruefe.Wahr(w.Ausfuehren(rat, new DomStimme(b.Inhaber)).Erfolg, "Stimme");
            w.Domamt.KollekteBuchen(10, 22);

            var json = Speicherung.Speichern(sim);
            Pruefe.Wahr(json.Contains("\"Dom\""), "Dom im Spielstand");
            var geladen = Speicherung.Laden(json).Simulation.Wirtschaft!;
            var a2 = geladen.Konten.Single(k => k.Inhaber == "Haus Psalm");
            var b2 = geladen.Konten.Single(k => k.Inhaber == "Haus Weihrauch");
            var rat2 = geladen.Konten.Single(k => k.Inhaber == "Haus Ratsherr");
            Pruefe.Gleich(a2, geladen.Domamt.Inhaber);
            Pruefe.Gleich(w.Domamt.Bis, geladen.Domamt.Bis);
            Pruefe.Gleich(b2, geladen.Domamt.Bewerbungen.Single());
            Pruefe.Gleich(b2, geladen.Domamt.StimmeVon(rat2)!.Haus);
            Pruefe.Gleich(22.0, geladen.Domamt.DiesesJahr.Kollekte);
            Pruefe.Gleich(22.0, a2.Laufend.Domkollekte);

            // Ein alter Stand ohne Dom (wie vor Datenversion 86): das Amt ist frei.
            var alt = System.Text.Json.Nodes.JsonNode.Parse(json)!;
            Pruefe.Wahr(EntferneDom(alt), "Feld Dom gefunden");
            var altGeladen = Speicherung.Laden(alt.ToJsonString()).Simulation.Wirtschaft!;
            Pruefe.Gleich(null, altGeladen.Domamt.Inhaber);
            Pruefe.Gleich(0, altGeladen.Domamt.Bewerbungen.Count);

            // Ohne Domprediger, Bewerbung und Kollekte schreibt der Spielstand nichts zum Dom.
            var leer = System.Text.Json.Nodes.JsonNode.Parse(Speicherung.Speichern(NeueSimulation()))!;
            Pruefe.Wahr(KirchenStand(leer) is { } ks && (!ks.ContainsKey("Dom") || ks["Dom"] is null), "ohne Domprediger kein Dom-Stand");
        }),

        ("Domprediger: KI – ein KI-Prediger mit Kirche bewirbt sich (1 AP), eine KI ohne Kirche nicht; eine KI im Rat stimmt nach ihrem Charakter", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (ki, _) = Prediger(w, "Familie Rom", Konfession.Katholisch);
            w.KiAnmelden(ki, Reichsgilde.Core.Ki.KiProfil.Standard);
            var ohneKirche = Haus(w, "Haus Leer", Konfession.Katholisch);
            w.KiAnmelden(ohneKirche, Reichsgilde.Core.Ki.KiProfil.Standard);
            var ratsKi = Haus(w, "Haus Kreuz", Konfession.Katholisch);
            w.KiAnmelden(ratsKi, Reichsgilde.Core.Ki.KiProfil.Standard);
            InDenRat(w, ratsKi);
            for (var i = 0; i < 12 * 60 - Reichsgilde.Core.Zeit.Spielzeit.TagesbeginnMinute; i++) sim.Tick(); // über die Politikstunde
            Pruefe.Wahr(w.Domamt.Bewerbungen.Contains(ki), "die KI mit Kirche bewirbt sich: " + string.Join(" | ", w.Rivalen.Single(r => r.Konto == ki).Entscheidungen));
            Pruefe.Wahr(!w.Domamt.Bewerbungen.Contains(ohneKirche), "ohne Kirche keine Bewerbung");
            var punkt = w.Rat.Sitzungsvorschau().Single(p => p.Thema == Reichsgilde.Core.Politik.Sitzungsthema.Domprediger);
            var stimme = punkt.Stimmen.Single(st => st.Haus == ratsKi);
            Pruefe.Gleich(ki.Inhaber, stimme.Kandidat); // der einzige Kandidat
            Pruefe.Wahr(!stimme.Frei, "eine KI setzt ihre Stimme selbst");
            JahrBeenden(sim);
            Pruefe.Gleich(ki, w.Domamt.Inhaber);
        }),

        ("Domprediger: Leeres Amt – die Partie rechnet wie ohne kirche.dom (zwei Jahre, gleiches Vermögen aller Häuser)", () =>
        {
            var mit = NeueSimulation();
            var ohne = NeueSimulation(OhneDom.Value);
            for (var i = 0; i < 2; i++) { JahrBeenden(mit); JahrBeenden(ohne); }
            var km = mit.Wirtschaft!.Konten.Select(k => (k.Inhaber, k.Vermoegen)).ToList();
            var ko = ohne.Wirtschaft!.Konten.Select(k => (k.Inhaber, k.Vermoegen)).ToList();
            Pruefe.Gleich(ko.Count, km.Count);
            Pruefe.Gleich(null, mit.Wirtschaft.Domamt.Inhaber); // auf dieser Karte beginnt kein Haus als Prediger
            for (var i = 0; i < km.Count; i++) Pruefe.Gleich(ko[i], km[i]);
        }),
    };
}
