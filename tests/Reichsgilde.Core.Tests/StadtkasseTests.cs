using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Tests für die Stadtkasse, die Anträge zu Steuer, Bau und Fest und die Befugnisse der Ämter (PR #200):
/// Einnahmen und Ausgaben, Startbestand, jeder Antrag (Deckung, Abstimmung, Wirkung), Tagesordnung, jede Befugnis, Speichern und Laden.
/// Die KI-Anträge prüft <see cref="KiStadtkasseTests"/>.
/// </summary>
static class StadtkasseTests
{
    static readonly Lazy<string> Json = new(() => Datenpfad.OhneAktionspunkte());
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json.Value));
    static readonly Lazy<Wirtschaftsdaten> MitAp = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    internal static Simulation NeueSimulation(Wirtschaftsdaten? daten = null, Spielzeit? zeit = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, zeit, daten ?? Daten.Value);

    /// <summary>Die Daten mit geändertem Abschnitt „politik“.</summary>
    static Wirtschaftsdaten Geaendert(Action<JsonObject> aendern)
    {
        var knoten = JsonNode.Parse(Json.Value)!.AsObject();
        aendern(knoten["politik"]!.AsObject());
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    }

    internal static Konto Haus(Stadtwirtschaft w, string name, double vermoegen = 20_000)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: vermoegen);
        return k;
    }

    /// <summary>Setzt das Haus auf diesen Sitz (statt des Ratsherrn).</summary>
    internal static void Sitz(Stadtwirtschaft w, string amt, Konto k)
    {
        var sitz = w.Rat.AmtNamens(amt)!;
        sitz.Ratsherr = null;
        sitz.Konto = k;
    }

    internal static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    internal static void Ok(Stadtwirtschaft w, Konto k, Befehl befehl)
    {
        var e = w.Ausfuehren(k, befehl);
        Pruefe.Wahr(e.Erfolg, e.Meldung);
    }

    internal static void Abgelehnt(Stadtwirtschaft w, Konto k, Befehl befehl, string grund)
    {
        var e = w.Ausfuehren(k, befehl);
        Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains(grund), $"erwartet abgelehnt mit „{grund}“, war: {e.Erfolg} – {e.Meldung}");
    }

    /// <summary>Bringt die Kasse auf diesen Stand (über den Posten Bußen bzw. Amtsgehälter).</summary>
    internal static void KasseAuf(Stadtwirtschaft w, double stand)
    {
        var diff = stand - w.Kasse.Stand;
        if (diff > 0) w.Kasse.Buchen(Kassenposten.Strafen, diff);
        else w.Kasse.Buchen(Kassenposten.Amtsgehaelter, -diff);
    }

    /// <summary>Das Haus hält nur noch dieses Amt (ein anderes, in das es gewählt wurde, geht an einen Ratsherrn zurück).</summary>
    internal static void NurAmt(Stadtwirtschaft w, string amt, Konto k)
    {
        foreach (var a in w.Rat.Aemter.Where(a => a.Konto == k && a.Name != amt))
        {
            a.Konto = null;
            a.Ratsherr = w.Rat.Ratsherren.First(r => w.Rat.Aemter.All(x => x.Ratsherr != r));
        }
        Sitz(w, amt, k);
    }

    /// <summary>Alle Ratsherren schätzen dieses Haus (Gunst 100) – sie stimmen jedem Antrag zu Steuer, Bau und Fest zu.</summary>
    static void Beliebt(Stadtwirtschaft w, Konto k)
    {
        foreach (var r in w.Rat.Ratsherren) w.Rat.GunstHinzufuegen(r, k, 100);
    }

    /// <summary>Ein Haus mit Beweisen für eine Tat eines anderen klagt an (zwei Beweise: sicherer Schuldspruch).</summary>
    static void Anklage(Stadtwirtschaft w, Konto anklaeger, Konto beklagter, Vergehen tat, double betrag)
    {
        for (var i = 0; i < 2; i++)
            w.Justiz.BeweisGeben(anklaeger, w.Justiz.TatMelden(beklagter, tat, anklaeger.Inhaber, betrag)!);
        Ok(w, anklaeger, new Anklagen(beklagter.Inhaber));
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Stadtkasse: Steuern und Glaubenszins kommen herein (die Standesgebühr nicht), die Amtsgehälter aller Ämter gehen hinaus; das Jahr wird zum Vorjahr", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            Pruefe.Wahr(w.Kasse.IstAktiv, "Kasse aktiv");
            Pruefe.Gleich(3000.0, w.Kasse.Stand);
            Pruefe.Gleich(5, w.Kasse.SteuersatzProzent);
            Pruefe.Gleich(0.05, w.Umsatzsteuer);
            var k = Haus(w, "Steuerzahler", 1_000_000);
            k.Steuer(100);
            k.Steuer(50, Kassenposten.Glaubenszins);
            Ok(w, Wohnhaushilfe.MitWohnhaus(w, k), new StandErwerben()); // die Standesgebühr geht an den Landesherrn
            Pruefe.Gleich(100.0, w.Kasse.DiesesJahr[Kassenposten.Umsatzsteuer]);
            Pruefe.Gleich(50.0, w.Kasse.DiesesJahr[Kassenposten.Glaubenszins]);
            Pruefe.Gleich(0.0, w.Kasse.DiesesJahr[Kassenposten.Gebuehren]);
            Pruefe.Gleich(3150.0, w.Kasse.Stand);
            Sitz(w, "Zöllner", k);
            JahrBeenden(sim);
            var vorjahr = w.Kasse.Vorjahr!;
            Pruefe.Gleich(Spielzeit.StartJahr, vorjahr.Jahr);
            // 800 + 2 × 500 + 2 × 300 + 2 × 150 – auch für die Ratsherren; eine kleine Stadt zahlt weniger (Datenversion 57: hier ohne
            // arbeitendes Haus 100 ÷ 250 Einwohner = 0,4)
            var f = w.Kasse.Gehaltsfaktor;
            Pruefe.Ungefaehr(0.4, f, 1e-12);
            Pruefe.Ungefaehr(2700.0 * f, vorjahr[Kassenposten.Amtsgehaelter], 1e-9);
            Pruefe.Ungefaehr(150.0 * f, k.Vorjahr!.Amtseinkommen, 1e-9);
            Pruefe.Gleich(150.0, vorjahr.Einnahmen);
            Pruefe.Ungefaehr(2700.0 * f, vorjahr.Ausgaben, 1e-9);
            Pruefe.Ungefaehr(3000 + 150 - 2700 * f, w.Kasse.Stand, 1e-9);
            Pruefe.Gleich(0.0, w.Kasse.DiesesJahr.Einnahmen);
            Pruefe.Gleich("Umsatzsteuer|Glaubenszins|Amtsgehaelter", string.Join("|", vorjahr.Posten.Select(p => p.Posten)));
            Pruefe.Ungefaehr(150.0 - 100 * 0.2 - 2700 * f, w.Kasse.HaushaltMit(4)!.Value, 1e-9); // Haushalt des Vorjahres mit 4 % statt 5 %
            // Die leere Kasse hat keine Folgen: Die Gehälter werden weiter gezahlt, die Kasse geht ins Minus.
            JahrBeenden(sim);
            JahrBeenden(sim);
            Pruefe.Wahr(w.Kasse.Stand < 0, $"Kasse {w.Kasse.Stand}");
            Pruefe.Ungefaehr(150.0 * f, k.Vorjahr!.Amtseinkommen, 1e-9);
        }),

        ("Stadtkasse: Die Umsatzsteuer aller Verkäufe am Markt landet in der Kasse; den Haushalt sieht nur der Kämmerer", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = Haus(w, "Gießer");
            w.Einstellen(w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, k), "Geselle", 2);
            JahrBeenden(sim);
            Pruefe.Wahr(k.Vorjahr!.Steuer > 0, "Steuern gezahlt");
            Pruefe.Ungefaehr(k.Vorjahr.Steuer, w.Kasse.Vorjahr![Kassenposten.Umsatzsteuer], 1e-6);
            Pruefe.Ungefaehr(0.05 * k.Vorjahr.Umsatz, k.Vorjahr.Steuer, 1e-6);
            Pruefe.Wahr(w.Kasse.HaushaltVerboten(k)!.Contains("Kämmerer"), "nur der Kämmerer");
            Sitz(w, "Kämmerer", k);
            Pruefe.Gleich<string?>(null, w.Kasse.HaushaltVerboten(k));
        }),

        ("Stadtkasse: Ohne Abschnitt politik.kasse gibt es keine Kasse – Steuersatz aus den Daten, Gehälter wie bisher, keine neuen Anträge", () =>
        {
            var sim = NeueSimulation(Geaendert(p => p.Remove("kasse")));
            var w = sim.Wirtschaft!;
            Pruefe.Wahr(!w.Kasse.IstAktiv && !w.Ratsantraege.IstAktiv && !w.Befugnisse.IstAktiv, "alles aus");
            Pruefe.Gleich(0.05, w.Umsatzsteuer);
            var k = Haus(w, "Haus");
            Sitz(w, "Marktmeister", k);
            Pruefe.Gleich(0.05, w.SteuersatzMarkt(k));
            Abgelehnt(w, k, new SteuersatzBeantragen(4), "keine Stadtkasse");
            Abgelehnt(w, k, new StreifeVerlegen(Stadtviertel.Nord), "keine Stadtkasse");
            k.Steuer(100);
            Pruefe.Gleich(0.0, w.Kasse.DiesesJahr.Einnahmen);
            JahrBeenden(sim);
            Pruefe.Gleich(300.0, k.Vorjahr!.Amtseinkommen);
            Pruefe.Gleich<StadtkasseStand?>(null, w.Sichern().Stadtkasse);
        }),

        ("Stadtkasse: Startbestand je Karte (koeln 6.000, sonst 3.000); eine Karte setzt ihn nur, solange nichts gebucht ist", () =>
        {
            var w0 = NeueSimulation().Wirtschaft!;
            Pruefe.Gleich(6000.0, w0.Kasse.StartbestandFuer("koeln"));
            Pruefe.Gleich(3000.0, w0.Kasse.StartbestandFuer("probestadt"));
            var daten = Geaendert(p => p["kasse"]!["startJeKarte"] = new JsonObject { ["probestadt"] = 4321 });
            var w = NeueSimulation(daten).Wirtschaft!;
            w.KarteSetzen(Stadtkarte.AusDatei(Datenpfad.Karte("probestadt")));
            Pruefe.Gleich(4321.0, w.Kasse.Stand);
            var w2 = NeueSimulation(daten).Wirtschaft!;
            w2.Kasse.Buchen(Kassenposten.Strafen, 10);
            w2.KarteSetzen(Stadtkarte.AusDatei(Datenpfad.Karte("probestadt")));
            Pruefe.Gleich(3010.0, w2.Kasse.Stand);
        }),

        ("Antrag Steuersatz: 3–8 %, nur im Rat, je Jahr einer; die Ratsherren senken bei voller Kasse und gesundem Haushalt, erhöhen bei leerer", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = Haus(w, "Reformer");
            var draussen = Haus(w, "Draußen");
            Sitz(w, "Zöllner", k);
            Abgelehnt(w, draussen, new SteuersatzBeantragen(4), "nur Mitglieder des Rates");
            Abgelehnt(w, k, new SteuersatzBeantragen(9), "zwischen 3 und 8 %");
            Abgelehnt(w, k, new SteuersatzBeantragen(2), "zwischen 3 und 8 %");
            Abgelehnt(w, k, new SteuersatzBeantragen(5), "schon 5 %");
            // Kasse 3.000: Senken will keiner (erst über der doppelten Reserve), nur der Antragsteller
            var senken = new Ratsantrag(Antragsart.Steuersatz, k, Prozent: 4);
            Pruefe.Gleich((1, 6, 0, (bool?)false), w.Ratsantraege.Prognose(senken));
            KasseAuf(w, 10_000); // über 2 × 3.000 (Zwerge) – aber im ersten Jahr kennt noch keiner den Haushalt
            Pruefe.Gleich((1, 6, 0, (bool?)false), w.Ratsantraege.Prognose(senken));
            k.Steuer(4000);
            JahrBeenden(sim); // Haushalt: 4.000 Steuern + 7.000 Bußen − 2.700 Gehälter
            Pruefe.Gleich((7, 0, 0, (bool?)true), w.Ratsantraege.Prognose(senken));
            Ok(w, k, new SteuersatzBeantragen(4));
            Pruefe.Gleich(senken, w.Ratsantraege.Antrag(Antragsart.Steuersatz));
            Abgelehnt(w, k, new SteuersatzBeantragen(3), "schon ein Antrag vor");
            JahrBeenden(sim);
            Pruefe.Gleich(4, w.Kasse.SteuersatzProzent);
            Pruefe.Gleich(0.04, w.Umsatzsteuer);
            Pruefe.Wahr(w.Rat.LetzteSitzung.Any(s => s.Contains("Steuersatz 4 %") && s.Contains("ANGENOMMEN")), string.Join(" | ", w.Rat.LetzteSitzung));
            Pruefe.Wahr(w.Ratsantraege.Antrag(Antragsart.Steuersatz) is null, "Antrag erledigt");
            // Leere Kasse: Erhöhen geht durch, Senken nicht
            KasseAuf(w, 1000);
            Pruefe.Gleich((7, 0, 0, (bool?)true), w.Ratsantraege.Prognose(new Ratsantrag(Antragsart.Steuersatz, k, Prozent: 6)));
            Pruefe.Gleich((1, 6, 0, (bool?)false), w.Ratsantraege.Prognose(new Ratsantrag(Antragsart.Steuersatz, k, Prozent: 3)));

            // Gunst: Wer die Ratsherren für sich hat, bekommt ihre Stimme auch gegen die Kasse
            Beliebt(w, k);
            Pruefe.Gleich((7, 0, 0, (bool?)true), w.Ratsantraege.Prognose(new Ratsantrag(Antragsart.Steuersatz, k, Prozent: 3)));
            // Ein Jahr ohne Einnahmen: Der Haushalt des Vorjahres ist im Minus (nur Gehälter) – trotz voller Kasse senkt keiner, erhöhen wollen alle
            var neuling = Haus(w, "Neuling");
            Sitz(w, "Gefängniswärter", neuling);
            KasseAuf(w, 50_000);
            JahrBeenden(sim);
            JahrBeenden(sim);
            Pruefe.Wahr(w.Kasse.Stand > 40_000 && w.Kasse.HaushaltMit(3) < 0, $"Kasse {w.Kasse.Stand}, Haushalt {w.Kasse.HaushaltMit(3)}");
            Pruefe.Gleich((1, 5, 1, (bool?)false), w.Ratsantraege.Prognose(new Ratsantrag(Antragsart.Steuersatz, neuling, Prozent: 3))); // der Reformer enthält sich
            Pruefe.Gleich((6, 0, 1, (bool?)true), w.Ratsantraege.Prognose(new Ratsantrag(Antragsart.Steuersatz, neuling, Prozent: 6)));
        }),

        ("Antrag Steuersatz: Die Ratsherren rechnen nach ihrem Volk (Zwerg 3.000, Elb 2.400, Mensch 2.000, Halbling 1.500)", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = Haus(w, "Reformer");
            Sitz(w, "Zöllner", k);
            KasseAuf(w, 2200); // unter der Reserve der Zwerge und der Elbe, über der der Menschen und Halblinge
            var hoeher = new Ratsantrag(Antragsart.Steuersatz, k, Prozent: 6);
            // dafür: Erzhammer (Zwerg), Kupferbart (Zwerg), Silberhain (Elb), Antragsteller; dagegen: Wendelstein, Eisenfaust, Kesselflick
            Pruefe.Gleich((4, 3, 0, (bool?)false), w.Ratsantraege.Prognose(hoeher));
            Pruefe.Wahr(Stadtrat.IstAngenommen(4, 3, false), "Mehrheit");
        }),

        ("Antrag zu Steuer, Bau und Fest: Der Spieler stimmt vorab ab, die Prognose zählt seine Stimme; Gleichstand entscheidet der Bürgermeister", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = Haus(w, "Reformer");
            var spieler = Haus(w, "Spieler");
            var draussen = Haus(w, "Draußen");
            Sitz(w, "Zöllner", k);
            Sitz(w, "Marktmeister", spieler);
            KasseAuf(w, 2200);
            Pruefe.Wahr(w.Ratsantraege.StimmeVerboten(spieler, Antragsart.Steuersatz)!.Contains("kein Antrag"), "ohne Antrag");
            Ok(w, k, new SteuersatzBeantragen(6));
            // ohne Stimme des Spielers: 4 dafür (3 Ratsherren + Antragsteller), 2 dagegen, 1 Enthaltung
            Pruefe.Gleich((4, 2, 1, (bool?)false), w.Ratsantraege.Prognose(w.Ratsantraege.Antrag(Antragsart.Steuersatz)!));
            Abgelehnt(w, k, new RatsantragStimme(Antragsart.Steuersatz, false), "hat den Antrag gestellt");
            Abgelehnt(w, draussen, new RatsantragStimme(Antragsart.Steuersatz, false), "nur Mitglieder des Rates");
            Ok(w, spieler, new RatsantragStimme(Antragsart.Steuersatz, false));
            Pruefe.Gleich((bool?)false, w.Ratsantraege.StimmeVon(spieler, Antragsart.Steuersatz));
            Pruefe.Gleich((4, 3, 0, (bool?)false), w.Ratsantraege.Prognose(w.Ratsantraege.Antrag(Antragsart.Steuersatz)!));
            // Gleichstand: Ein Zwerg (Kupferbart, Wärter) wird durch ein Haus ersetzt, das sich enthält – 3 zu 3, der Bürgermeister ist dagegen
            Sitz(w, "Gefängniswärter", draussen);
            var (ja, nein, _, bm) = w.Ratsantraege.Prognose(w.Ratsantraege.Antrag(Antragsart.Steuersatz)!);
            Pruefe.Gleich((3, 3, (bool?)false), (ja, nein, bm));
            Pruefe.Wahr(!Stadtrat.IstAngenommen(ja, nein, bm), "Gleichstand, der Bürgermeister ist dagegen");
            // In der Sitzung zählt die Kasse nach den Gehältern (2.200 − 2.700 < 0): Nun wollen alle Ratsherren mehr Steuern.
            JahrBeenden(sim);
            Pruefe.Gleich(6, w.Kasse.SteuersatzProzent);
            Pruefe.Wahr(w.Rat.LetzteSitzung.Any(s => s.Contains("Steuersatz 6 %") && s.Contains("ANGENOMMEN") && s.Contains("5 dafür, 1 dagegen")),
                string.Join(" | ", w.Rat.LetzteSitzung));
        }),

        ("Antrag Bauvorhaben: nur mit Deckung; angenommen bezahlt die Kasse, die Baustelle steht ihre Bauzeit, dann wirkt der Bau; Ansehen", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = Haus(w, "Bauherr");
            Sitz(w, "Zöllner", k);
            Abgelehnt(w, k, new BauvorhabenBeantragen("kirche"), "gibt es nicht");
            Abgelehnt(w, k, new BauvorhabenBeantragen("hafen"), "keinen Hafen");
            KasseAuf(w, 2000);
            Abgelehnt(w, k, new BauvorhabenBeantragen("brunnen"), "kostet 2.500");
            KasseAuf(w, 20_000);
            var brunnen = new Ratsantrag(Antragsart.Bauvorhaben, k, Bauvorhaben: "brunnen");
            Pruefe.Gleich(2500.0, w.Ratsantraege.Kosten(brunnen));
            Pruefe.Gleich((7, 0, 0, (bool?)true), w.Ratsantraege.Prognose(brunnen));
            Ok(w, k, new BauvorhabenBeantragen("brunnen"));
            Pruefe.Gleich(1.0, w.Ratsantraege.Krankheitsfaktor);
            JahrBeenden(sim);
            Pruefe.Gleich(2500.0, w.Kasse.Vorjahr![Kassenposten.Bauvorhaben]); // die Sitzung bucht noch ins alte Jahr
            Pruefe.Wahr(w.Ratsantraege.ImBau("brunnen") && !w.Ratsantraege.IstGebaut("brunnen"), "Baustelle");
            var baustelle = w.Ratsantraege.Baustellen.Single();
            Pruefe.Gleich(("brunnen", "Brunnen", k), (baustelle.Kennung, baustelle.Name, baustelle.Antragsteller));
            Pruefe.Ungefaehr(3, w.Ratsantraege.AnsehenBonus(k), 1e-9);
            Abgelehnt(w, k, new BauvorhabenBeantragen("brunnen"), "wird schon gebaut");
            while (sim.Zeit.VergangeneMinuten < baustelle.FertigAb) sim.Tick();
            sim.Tick();
            Pruefe.Wahr(w.Ratsantraege.IstGebaut("brunnen") && w.Ratsantraege.Baustellen.Count == 0, "fertig");
            Pruefe.Gleich(0.75, w.Ratsantraege.Krankheitsfaktor);
            Abgelehnt(w, k, new BauvorhabenBeantragen("brunnen"), "schon gebaut");
            // Ansehen 3 Jahre: im Jahr des Beschlusses und den zwei folgenden
            JahrBeenden(sim);
            JahrBeenden(sim);
            Pruefe.Ungefaehr(0, w.Ratsantraege.AnsehenBonus(k), 1e-9);
        }),

        ("Antrag Bauvorhaben: angenommen, aber die Kasse reicht bei der Sitzung nicht mehr – dann wird nichts gebaut", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = Haus(w, "Bauherr");
            Sitz(w, "Zöllner", k);
            Beliebt(w, k);
            KasseAuf(w, 10_000);
            Ok(w, k, new BauvorhabenBeantragen("marktplatz"));
            KasseAuf(w, 4000); // vor der Sitzung kommen die Gehälter dazu
            JahrBeenden(sim);
            Pruefe.Wahr(w.Ratsantraege.Baustellen.Count == 0 && !w.Ratsantraege.IstGebaut("marktplatz"), "nichts gebaut");
            Pruefe.Wahr(w.Rat.LetzteSitzung.Any(s => s.Contains("Marktplatz vergrößern") && s.Contains("reicht nicht mehr")), string.Join(" | ", w.Rat.LetzteSitzung));
            Pruefe.Gleich(0.0, w.Kasse.Vorjahr![Kassenposten.Bauvorhaben]);
        }),

        ("Antrag Bauvorhaben: Marktplatz – mehr Nachfrage; Mauer – nächtliche Taten gelingen seltener", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = Haus(w, "Bauherr");
            Sitz(w, "Zöllner", k);
            Beliebt(w, k);
            KasseAuf(w, 50_000);
            var stand = w.Markt["Silberring"];
            var vorher = stand.Nachfrage;
            var giesserei = w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, k);
            var schutzVorher = w.Wache.Patrouillefaktor(giesserei);
            Ok(w, k, new BauvorhabenBeantragen("marktplatz"));
            JahrBeenden(sim);
            var fertig = w.Ratsantraege.Baustellen.Max(b => b.FertigAb);
            while (sim.Zeit.VergangeneMinuten <= fertig + 1) sim.Tick(); // eine Minute später gilt der Faktor am Markt
            Pruefe.Wahr(w.Ratsantraege.IstGebaut("marktplatz"), "gebaut");
            Pruefe.Ungefaehr(1.05, w.Ratsantraege.Nachfragefaktor, 1e-12);
            Pruefe.Ungefaehr(1.05, stand.Nachfrage / (stand.Grundnachfrage * stand.NachfrageFaktor), 1e-12);
            Pruefe.Wahr(vorher > 0, "Nachfrage");
            Ok(w, k, new BauvorhabenBeantragen("mauer"));
            JahrBeenden(sim);
            fertig = w.Ratsantraege.Baustellen.Max(b => b.FertigAb);
            while (sim.Zeit.VergangeneMinuten <= fertig) sim.Tick();
            Pruefe.Ungefaehr(0.85, w.Ratsantraege.Nachtschutzfaktor, 1e-12);
            Pruefe.Ungefaehr(schutzVorher * 0.85, w.Wache.Patrouillefaktor(giesserei), 1e-12);
        }),

        ("Antrag Fest: nur mit Deckung; angenommen gilt es das ganze nächste Jahr – mehr Nachfrage und Gäste, Ansehen schon zur Wahl", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = Haus(w, "Festwirt");
            Sitz(w, "Zöllner", k);
            KasseAuf(w, 1000);
            Abgelehnt(w, k, new FestBeantragen(), "Ein Fest kostet 1.500");
            KasseAuf(w, 3000);
            // 3.000 − 1.500 = 1.500: Nur der Halbling (Kesselflick) ist so großzügig
            Pruefe.Gleich((2, 5, 0, (bool?)false), w.Ratsantraege.Prognose(new Ratsantrag(Antragsart.Fest, k)));
            KasseAuf(w, 20_000);
            Ok(w, k, new FestBeantragen());
            Pruefe.Wahr(!w.Ratsantraege.FestLaeuft, "noch nicht");
            JahrBeenden(sim);
            Pruefe.Wahr(w.Ratsantraege.FestLaeuft, "Festjahr");
            Pruefe.Gleich(w.Jahr, w.Ratsantraege.FestJahr);
            Pruefe.Gleich(1500.0, w.Kasse.Vorjahr![Kassenposten.Feste]);
            Pruefe.Ungefaehr(5, w.Ratsantraege.AnsehenBonus(k), 1e-9);
            sim.Tick();
            Pruefe.Ungefaehr(1.05, w.Markt.Ratsfaktor, 1e-12);
            Pruefe.Ungefaehr(1.2, w.Ratsantraege.Gaestefaktor, 1e-12);
            JahrBeenden(sim);
            sim.Tick();
            Pruefe.Wahr(!w.Ratsantraege.FestLaeuft, "vorbei");
            Pruefe.Gleich(1.0, w.Markt.Ratsfaktor);
            Pruefe.Gleich(1.0, w.Ratsantraege.Gaestefaktor);
            Pruefe.Ungefaehr(0, w.Ratsantraege.AnsehenBonus(k), 1e-9);
        }),

        ("Tagesordnung: alle Anträge in der Reihenfolge der Abstimmung – Statut, Stadtglaube, Steuersatz, Bauvorhaben, Fest", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var a = Haus(w, "Anna");
            var b = Haus(w, "Bert");
            Sitz(w, "Zöllner", a);
            Sitz(w, "Gefängniswärter", b);
            KasseAuf(w, 20_000);
            Ok(w, b, new FestBeantragen());
            Ok(w, a, new BauvorhabenBeantragen("brunnen"));
            Ok(w, b, new SteuersatzBeantragen(4));
            Ok(w, a, new StadtglaubeBeantragen(Reichsgilde.Core.Glaube.Konfession.Katholisch));
            Ok(w, b, new StatutBeantragen(Fronstatut.FreieStadt));
            var t = w.Ratsantraege.Tagesordnung;
            Pruefe.Gleich("Statut|Stadtglaube|Steuersatz|Bauvorhaben|Fest", string.Join("|", t.Select(x => x.Thema)));
            Pruefe.Gleich("Bert|Anna|Bert|Anna|Bert", string.Join("|", t.Select(x => x.Antragsteller.Inhaber)));
            Pruefe.Wahr(t[3].Text.Contains("Brunnen") && t[2].Text.Contains("4 %"), string.Join(" | ", t.Select(x => x.Text)));
            JahrBeenden(sim);
            var protokoll = w.Rat.LetzteSitzung.Where(s => s.StartsWith("Ratssitzung")).ToList();
            Pruefe.Wahr(protokoll.Count == 5 && protokoll[2].Contains("Steuersatz") && protokoll[3].Contains("Brunnen") && protokoll[4].Contains("Fest"),
                string.Join(" | ", protokoll));
        }),

        ("Bürgermeister: einmal im Jahr ein Antrag ohne Aktionspunkte; der Kämmerer beantragt den Steuersatz immer ohne", () =>
        {
            var sim = NeueSimulation(MitAp.Value);
            var w = sim.Wirtschaft!;
            var bm = Haus(w, "Bürgermeisterhaus");
            var kaemmerer = Haus(w, "Kämmererhaus");
            Sitz(w, "Bürgermeister", bm);
            Sitz(w, "Kämmerer", kaemmerer);
            KasseAuf(w, 20_000);
            Pruefe.Gleich(2, w.ApKosten(new FestBeantragen()));
            Pruefe.Wahr(w.Befugnisse.FreierAntragOffen(bm), "frei");
            Pruefe.Gleich(0, w.ApKosten(bm, new FestBeantragen()));
            Pruefe.Gleich(0, w.ApKosten(bm, new StatutBeantragen(Fronstatut.FreieStadt)));
            var ap = bm.Aktionspunkte.Vorrat;
            Ok(w, bm, new FestBeantragen());
            Pruefe.Gleich(ap, bm.Aktionspunkte.Vorrat);
            Pruefe.Wahr(!w.Befugnisse.FreierAntragOffen(bm), "verbraucht");
            Pruefe.Gleich(2, w.ApKosten(bm, new BauvorhabenBeantragen("brunnen")));
            Ok(w, bm, new BauvorhabenBeantragen("brunnen"));
            Pruefe.Gleich(ap - 2, bm.Aktionspunkte.Vorrat);
            // Ein abgelehnter Antrag verbraucht den freien nicht
            var bm2 = Haus(w, "Zweiter");
            Sitz(w, "Bürgermeister", bm2);
            Abgelehnt(w, bm2, new FestBeantragen(), "schon ein Antrag vor");
            Pruefe.Wahr(w.Befugnisse.FreierAntragOffen(bm2), "noch frei");
            // Kämmerer
            Pruefe.Gleich(0, w.ApKosten(kaemmerer, new SteuersatzBeantragen(4)));
            Pruefe.Gleich(2, w.ApKosten(kaemmerer, new FestBeantragen()));
            var apK = kaemmerer.Aktionspunkte.Vorrat;
            Ok(w, kaemmerer, new SteuersatzBeantragen(4));
            Pruefe.Gleich(apK, kaemmerer.Aktionspunkte.Vorrat);
            // Im neuen Jahr hat der Bürgermeister wieder einen freien Antrag
            JahrBeenden(sim);
            Sitz(w, "Bürgermeister", bm);
            Pruefe.Wahr(w.Befugnisse.FreierAntragOffen(bm), "neues Jahr");
        }),

        ("Stadtrichter: setzt im Vorsitz die Buße um bis zu ±25 % – höchstens so viel, nur im eigenen Vorsitz; die Buße geht in die Kasse", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var richter = Haus(w, "Richterhaus");
            var anklaeger = Haus(w, "Kläger");
            var beklagter = Haus(w, "Beklagter", 20_000);
            Sitz(w, "Stadtrichter", richter);
            Anklage(w, anklaeger, beklagter, Vergehen.Diebstahl, 1000);
            var basis = w.Justiz.DrohendeBusse(anklaeger, beklagter);
            Pruefe.Gleich(6000.0, basis); // zwei Taten zu 1.000 × 3
            Abgelehnt(w, anklaeger, new StrafmassSetzen("Kläger", "Beklagter", 25), "nur der Stadtrichter");
            Abgelehnt(w, richter, new StrafmassSetzen("Kläger", "Beklagter", 30), "±25 %");
            Abgelehnt(w, richter, new StrafmassSetzen("Beklagter", "Kläger", 10), "nicht anhängig");
            Ok(w, richter, new StrafmassSetzen("Kläger", "Beklagter", 25));
            Pruefe.Gleich(25, w.Befugnisse.Strafmass(anklaeger, beklagter));
            Pruefe.Gleich(7500.0, w.Justiz.DrohendeBusse(anklaeger, beklagter));
            // Im eigenen Verfahren führt der Bürgermeister den Vorsitz
            Anklage(w, anklaeger, richter, Vergehen.Diebstahl, 100);
            Abgelehnt(w, richter, new StrafmassSetzen("Kläger", "Richterhaus", -25), "Bürgermeister den Vorsitz");
            var kasse = w.Kasse.DiesesJahr[Kassenposten.Strafen];
            var gebuehren = w.Kasse.DiesesJahr[Kassenposten.Gebuehren];
            Pruefe.Gleich(2 * w.Justiz.Daten.Gerichtsgebuehr, gebuehren);
            JahrBeenden(sim);
            Pruefe.Wahr(w.Justiz.LetzteUrteile.Any(u => u.Beklagter == beklagter && u.Schuldig && u.Text.Contains("7.500")), string.Join(" | ", w.Justiz.LetzteUrteile.Select(u => u.Text)));
            Pruefe.Ungefaehr(kasse + 7500 + 600, w.Kasse.Vorjahr![Kassenposten.Strafen], 1e-6); // dazu die Buße des Richters (3 × 2 × 100)
            Pruefe.Gleich(0, w.Befugnisse.Strafmass(anklaeger, beklagter));
        }),

        ("Stadthauptmann: verlegt die Streife in ein Viertel – dort doppelter Schutz (mindestens 10 %); ein Ratsherr verlegt nichts", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var hauptmann = Haus(w, "Hauptmannshaus");
            var k = Haus(w, "Werkstatthaus");
            var giesserei = w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, k);
            Abgelehnt(w, hauptmann, new StreifeVerlegen(Stadtviertel.Nord), "nur der Stadthauptmann");
            Pruefe.Gleich<Stadtviertel?>(null, w.Befugnisse.Streife);
            Sitz(w, "Stadthauptmann", hauptmann);
            var viertel = w.Befugnisse.ViertelVon(giesserei);
            var anderes = (Stadtviertel)(((int)viertel + 1) % 4);
            Pruefe.Gleich(1.0, w.Wache.Patrouillefaktor(giesserei));
            Ok(w, hauptmann, new StreifeVerlegen(anderes));
            Pruefe.Gleich(1.0, w.Wache.Patrouillefaktor(giesserei));
            Ok(w, hauptmann, new StreifeVerlegen(viertel));
            Pruefe.Gleich<Stadtviertel?>(viertel, w.Befugnisse.Streife);
            Pruefe.Ungefaehr(0.9, w.Wache.Patrouillefaktor(giesserei), 1e-12); // ohne Wachhaus: die Stadtbüttel, 10 %
            Pruefe.Ungefaehr(0.4, w.Befugnisse.StreifeSchutz(giesserei, 0.2), 1e-12); // doppelt
            Pruefe.Ungefaehr(0.6, w.Befugnisse.StreifeSchutz(giesserei, 0.4), 1e-12); // höchstens 60 %
            Pruefe.Ungefaehr(0.2, w.Befugnisse.StreifeSchutz(null, 0.2), 1e-12); // Wohnhäuser: wie gewöhnlich
            // Verliert er das Amt, geht die Streife wieder gleichmäßig
            var sitz = w.Rat.AmtNamens("Stadthauptmann")!;
            sitz.Konto = null;
            sitz.Ratsherr = w.Rat.Ratsherren[0];
            Pruefe.Gleich<Stadtviertel?>(null, w.Befugnisse.Streife);
            Pruefe.Gleich(1.0, w.Wache.Patrouillefaktor(giesserei));
        }),

        ("Stadthauptmann: Viertel nach der Himmelsrichtung von der Mitte der Karte", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var karte = Stadtkarte.AusDatei(Datenpfad.Karte("probestadt"));
            w.KarteSetzen(karte);
            var k = Haus(w, "Haus");
            var mitte = (X: karte.Gebaeude.Average(g => g.Punkt[0]), Z: karte.Gebaeude.Average(g => g.Punkt[1]));
            var gesehen = new HashSet<Stadtviertel>();
            foreach (var g in karte.Gebaeude.Where(g => g.Betrieb is not null))
            {
                var b = w.BetriebEroeffnen(g.Betrieb!, Betriebsart.Giesserei, k);
                var (dx, dz) = (g.Punkt[0] - mitte.X, g.Punkt[1] - mitte.Z);
                var erwartet = Math.Abs(dx) > Math.Abs(dz) ? (dx > 0 ? Stadtviertel.Ost : Stadtviertel.West) : (dz < 0 ? Stadtviertel.Nord : Stadtviertel.Sued);
                Pruefe.Gleich(erwartet, w.Befugnisse.ViertelVon(b));
                gesehen.Add(erwartet);
            }
            Pruefe.Wahr(gesehen.Count >= 2, $"Viertel: {string.Join(", ", gesehen)}");
        }),

        ("Marktmeister: halbe Steuer auf eigene Marktverkäufe, Preise der nächsten Stunde nur für ihn", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var mm = Haus(w, "Marktmeisterhaus");
            var anderer = Haus(w, "Anderer");
            Sitz(w, "Marktmeister", mm);
            Pruefe.Gleich(0.025, w.SteuersatzMarkt(mm));
            Pruefe.Gleich(0.05, w.SteuersatzMarkt(anderer));
            Pruefe.Wahr(w.Befugnisse.Marktvorschau(anderer).Count == 0 && w.Befugnisse.MarktvorschauVerboten(anderer) is not null, "nicht für andere");
            w.Einstellen(w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, mm), "Geselle", 2);
            w.Einstellen(w.BetriebEroeffnen("Gießerei 2", Betriebsart.Giesserei, anderer), "Geselle", 2);
            JahrBeenden(sim);
            Pruefe.Wahr(mm.Vorjahr!.Umsatz > 0, "verkauft");
            Pruefe.Ungefaehr(0.025 * mm.Vorjahr.Umsatz, mm.Vorjahr.Steuer, 1e-6);
            Pruefe.Ungefaehr(0.05 * anderer.Vorjahr!.Umsatz, anderer.Vorjahr.Steuer, 1e-6);
            // Vorschau: der Preis zu Beginn der nächsten Stunde, wenn niemand verkauft
            for (var i = 0; i < 200; i++) sim.Tick();
            var vorschau = w.Befugnisse.Marktvorschau(mm);
            Pruefe.Gleich(w.Markt.Staende.Count(), vorschau.Count);
            var ring = vorschau.Single(v => v.Ware == "Silberring");
            Pruefe.Gleich(w.Markt["Silberring"].Preis, ring.PreisJetzt);
            w.Markt["Silberring"].NeueStunde();
            Pruefe.Ungefaehr(ring.PreisNaechsteStunde, w.Markt["Silberring"].Preis, 1e-9);
        }),

        ("Gefängniswärter: lässt Gefangene gegen Kaution frei (1.000 je Jahr, das bleibt) – das Geld bekommt er", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var waerter = Haus(w, "Wärterhaus");
            var anklaeger = Haus(w, "Kläger");
            var raeuber = Haus(w, "Räuber", 50_000);
            var armer = Haus(w, "Armer", 50_000);
            Anklage(w, anklaeger, raeuber, Vergehen.Raub, 500);
            Anklage(w, anklaeger, armer, Vergehen.Raub, 500);
            JahrBeenden(sim);
            Pruefe.Wahr(w.Justiz.ImKerker(raeuber) && w.Justiz.ImKerker(armer), "im Kerker");
            Abgelehnt(w, waerter, new GefangenenFreilassen("Räuber"), "nur der Gefängniswärter");
            Sitz(w, "Gefängniswärter", waerter);
            var kaution = w.Befugnisse.Kaution(raeuber);
            Pruefe.Gleich(1000.0 * (w.Justiz.KerkerBis(raeuber)!.Value - w.Jahr + 1), kaution);
            Abgelehnt(w, waerter, new GefangenenFreilassen("Kläger"), "sitzt nicht im Kerker");
            armer.Schaden(armer.Vermoegen - 10);
            Abgelehnt(w, waerter, new GefangenenFreilassen("Armer"), "so viel hat Armer nicht");
            var (geldR, geldW) = (raeuber.Vermoegen, waerter.Vermoegen);
            Ok(w, waerter, new GefangenenFreilassen("Räuber"));
            Pruefe.Wahr(!w.Justiz.ImKerker(raeuber), "frei");
            Pruefe.Ungefaehr(geldR - kaution, raeuber.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(geldW + kaution, waerter.Vermoegen, 1e-9);
            Pruefe.Wahr(w.Ausfuehren(raeuber, new Schenken("Ulrich Wendelstein", 10)).Erfolg, "wieder handlungsfähig");
        }),

        ("Zöllner: Die erste Zollstelle jeder Fahrt gehört der Stadt (Hafenzoll in die Kasse); der Zöllner zahlt dort nichts", () =>
        {
            foreach (var zoellner in new[] { false, true })
            {
                var sim = NeueSimulation(Daten.Value, new Spielzeit(9 * 60 - Spielzeit.TagesbeginnMinute));
                var w = sim.Wirtschaft!;
                w.KarteSetzen(Stadtkarte.AusDatei(Datenpfad.Karte("probestadt")));
                var k = Haus(w, "Reeder", 50_000);
                if (zoellner) Sitz(w, "Zöllner", k);
                Ok(w, k, new SchiffKaufen());
                var schiff = w.Schiffahrt.SchiffeVon(k).Single();
                var stadt = w.Schiffahrt.Staedte.OrderByDescending(s => s.Daten.Zollstellen).First();
                Pruefe.Gleich(stadt.Daten.Zollstellen - (zoellner ? 1 : 0), w.Schiffahrt.Zollstellen(k, stadt));
                var ware = stadt.Daten.Kauft.Keys.OrderBy(x => x, StringComparer.Ordinal).First();
                Ok(w, k, new SchiffsauftragErteilen(schiff.Name, stadt.Name, new Dictionary<string, int> { [ware] = 10 }, new Dictionary<string, int>()));
                var transport = k.Laufend.Transport;
                while (schiff.Zustand == SchiffZustand.Beladen) sim.Tick();
                var zoll = k.Laufend.Transport - transport;
                Pruefe.Wahr(zoll > 0, "Zoll gezahlt");
                var stellen = w.Schiffahrt.Zollstellen(k, stadt);
                Pruefe.Ungefaehr(zoellner ? 0 : zoll / stellen, w.Kasse.DiesesJahr[Kassenposten.Zoelle], 1e-9);
            }
        }),

        ("Bauvorhaben Hafen: Schiffe laden und liegen 40 % kürzer", () =>
        {
            var sim = NeueSimulation(Daten.Value, new Spielzeit(9 * 60 - Spielzeit.TagesbeginnMinute));
            var w = sim.Wirtschaft!;
            w.KarteSetzen(Stadtkarte.AusDatei(Datenpfad.Karte("probestadt")));
            var k = Haus(w, "Reeder");
            Sitz(w, "Zöllner", k);
            Beliebt(w, k);
            KasseAuf(w, 50_000);
            var typ = w.Schiffahrt.Daten!.Schiffe.Values.First();
            var stadt = w.Schiffahrt.Staedte[0];
            Pruefe.Gleich(typ.LadeMinuten, w.Schiffahrt.LadeMinuten(typ));
            var rundreise = Schiffahrt.Rundreise(typ, stadt);
            Ok(w, k, new BauvorhabenBeantragen("hafen"));
            JahrBeenden(sim);
            var fertig = w.Ratsantraege.Baustellen.Single().FertigAb;
            while (sim.Zeit.VergangeneMinuten <= fertig) sim.Tick();
            Pruefe.Ungefaehr(0.6, w.Ratsantraege.Hafenfaktor, 1e-12);
            Pruefe.Gleich((int)Math.Round(typ.LadeMinuten * 0.6, MidpointRounding.AwayFromZero), w.Schiffahrt.LadeMinuten(typ));
            Pruefe.Gleich((int)Math.Round(typ.LiegeMinuten * 0.6, MidpointRounding.AwayFromZero), w.Schiffahrt.LiegeMinuten(typ));
            Pruefe.Wahr(Schiffahrt.Rundreise(typ, stadt, w.Ratsantraege.Hafenfaktor) < rundreise, "kürzere Rundreise");
        }),

        ("Stadtkasse: Kasse, Anträge, Stimmen, Baustelle, Fest, Streife, Strafmaß und freier Antrag überstehen Speichern und Laden", () =>
        {
            var sim = NeueSimulation(MitAp.Value);
            var w = sim.Wirtschaft!;
            var bm = Haus(w, "Bürgermeisterhaus");
            var spieler = Haus(w, "Spieler");
            var richter = Haus(w, "Richterhaus");
            var hauptmann = Haus(w, "Hauptmannshaus");
            var klaeger = Haus(w, "Kläger");
            var beklagter = Haus(w, "Beklagter");
            Sitz(w, "Bürgermeister", bm);
            Sitz(w, "Marktmeister", spieler);
            Sitz(w, "Stadtrichter", richter);
            Sitz(w, "Stadthauptmann", hauptmann);
            Beliebt(w, bm);
            KasseAuf(w, 30_000);
            Ok(w, bm, new BauvorhabenBeantragen("brunnen"));
            Ok(w, bm, new FestBeantragen());
            JahrBeenden(sim); // Baustelle und Fest laufen
            Sitz(w, "Bürgermeister", bm);
            Ok(w, bm, new SteuersatzBeantragen(6));
            Ok(w, spieler, new RatsantragStimme(Antragsart.Steuersatz, false));
            Ok(w, hauptmann, new StreifeVerlegen(Stadtviertel.Ost));
            Anklage(w, klaeger, beklagter, Vergehen.Diebstahl, 500);
            Ok(w, richter, new StrafmassSetzen("Kläger", "Beklagter", -20));
            bm.Steuer(77);

            var json = Speicherung.Speichern(sim, spieler);
            var (geladen, sp) = Speicherung.Laden(json);
            var g = geladen.Wirtschaft!;
            Pruefe.Gleich(w.Kasse.Stand, g.Kasse.Stand);
            Pruefe.Gleich(77.0, g.Kasse.DiesesJahr[Kassenposten.Umsatzsteuer]);
            Pruefe.Gleich(w.Kasse.Vorjahr!.Ausgaben, g.Kasse.Vorjahr!.Ausgaben);
            Pruefe.Gleich(6, g.Ratsantraege.Antrag(Antragsart.Steuersatz)!.Prozent);
            Pruefe.Gleich((bool?)false, g.Ratsantraege.StimmeVon(sp!, Antragsart.Steuersatz));
            Pruefe.Gleich("brunnen", g.Ratsantraege.Baustellen.Single().Kennung);
            Pruefe.Wahr(g.Ratsantraege.FestLaeuft, "Fest");
            Pruefe.Gleich(w.Rat.Ansehen(bm), g.Rat.Ansehen(g.Konten.First(k => k.Inhaber == "Bürgermeisterhaus")));
            Pruefe.Gleich<Stadtviertel?>(Stadtviertel.Ost, g.Befugnisse.Streife);
            Pruefe.Gleich(-20, g.Befugnisse.Strafmass(g.Konten.First(k => k.Inhaber == "Kläger"), g.Konten.First(k => k.Inhaber == "Beklagter")));
            Pruefe.Wahr(!g.Befugnisse.FreierAntragOffen(g.Konten.First(k => k.Inhaber == "Bürgermeisterhaus")), "freier Antrag verbraucht");
            Pruefe.Gleich(Speicherung.Speichern(sim, spieler, DateTime.UnixEpoch), Speicherung.Speichern(geladen, sp, DateTime.UnixEpoch));
            // Steuern nach dem Laden fließen weiter in die Kasse
            g.Konten[0].Steuer(3);
            Pruefe.Gleich(80.0, g.Kasse.DiesesJahr[Kassenposten.Umsatzsteuer]);

            // Ein Spielstand von vor der Stadtkasse: Startbestand und Steuersatz aus den Daten, keine Anträge
            var knoten = JsonNode.Parse(json)!;
            var wirtschaft = knoten["Wirtschaft"]!.AsObject();
            wirtschaft.Remove("Stadtkasse");
            wirtschaft.Remove("Ratsantraege");
            wirtschaft.Remove("Befugnisse");
            var (alt, _) = Speicherung.Laden(knoten.ToJsonString());
            var a = alt.Wirtschaft!;
            Pruefe.Gleich(3000.0, a.Kasse.Stand);
            Pruefe.Gleich(5, a.Kasse.SteuersatzProzent);
            Pruefe.Wahr(a.Ratsantraege.Antraege.Count == 0 && a.Ratsantraege.Baustellen.Count == 0 && a.Befugnisse.Streife is null, "leer");
            a.Konten[0].Steuer(10);
            Pruefe.Gleich(3010.0, a.Kasse.Stand);
        }),

        ("Stadtkasse: Daten werden geprüft (Steuersätze, Bauvorhaben, Fest, Befugnisse)", () =>
        {
            Pruefe.Gleich(0, Daten.Value.Pruefen().Count);
            var f = "";
            try
            {
                Geaendert(p =>
                {
                    p["kasse"]!["steuerMin"] = 9;
                    p["fest"]!["kosten"] = 0;
                    p["befugnisse"]!["richterSpanne"] = 2;
                    p["bauvorhaben"]!.AsArray().Add(JsonNode.Parse("{\"kennung\": \"brunnen\", \"name\": \"Noch ein Brunnen\", \"kosten\": 10, \"wirkung\": \"Gesundheit\", \"wert\": 2}"));
                });
            }
            catch (Exception e) { f = e.Message; }
            foreach (var teil in new[] { "steuerMin", "politik.fest", "politik.befugnisse", "„brunnen“ ist doppelt", "wert muss zwischen 0 und 1" })
                Pruefe.Wahr(f.Contains(teil), $"„{teil}“ fehlt in: {f}");
        }),
    };
}
