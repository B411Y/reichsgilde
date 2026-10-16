using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für die Dynastie: Leben und Tod, Brautschau, Kinder, Erbfolge und Volksnähe im Rat.</summary>
static class DynastieTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Simulation NeueSimulation(int startwert = 1400, Siegbedingung sieg = Siegbedingung.Endlos) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = sieg, Zufallsstartwert = startwert }, wirtschaftsdaten: Daten.Value);

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    /// <summary>Ein Haus mit Familie. Ein Oberhaupt von 130 Jahren stirbt am Jahresende so gut wie sicher.</summary>
    static (Konto Konto, Familie Familie) Haus(Stadtwirtschaft w, string name, Volk volk = Volk.Mensch,
        Geschlecht geschlecht = Geschlecht.Mann, int? alter = null, double vermoegen = 50_000)
    {
        var konto = w.KontoEroeffnen(name, startvermoegen: vermoegen);
        return (konto, w.Familien.Gruenden(konto, volk, geschlecht, alter: alter));
    }

    static int _nummer = 90_000;

    /// <summary>Fügt ein Kind des Oberhaupts hinzu (für Erbfolge-Tests ohne jahrelanges Warten).</summary>
    static Person Kind(Stadtwirtschaft w, Familie f, int alter, Geschlecht g = Geschlecht.Mann, string vorname = "Kind")
    {
        var p = new Person(_nummer++, vorname, f.Nachname, f.Volk, g, w.Familien.Jahr - alter, vater: f.Oberhaupt.Nummer);
        f.MitgliederListe.Add(p);
        return p;
    }

    /// <summary>Verheiratet das Oberhaupt sofort mit einem passenden Kandidaten (Geschenke über mehrere Jahre gespart).</summary>
    static Person Verheiraten(Simulation sim, Konto konto, Volk? volk = null)
    {
        var w = sim.Wirtschaft!;
        var buch = w.Familien;
        var f = buch.Von(konto)!;
        var k = buch.Kandidaten.First(x => buch.WerbenVerboten(konto, x) is null && (volk is null || x.Person.Volk == volk));
        for (var jahr = 0; jahr < 5 && buch.Bereitschaft(konto, k) < buch.Heiratsschwelle(k); jahr++)
        {
            Pruefe.Wahr(w.Ausfuehren(konto, new Werben(k.Person.Nummer, 1000)).Erfolg, "Werben abgelehnt");
            if (buch.Bereitschaft(konto, k) < buch.Heiratsschwelle(k)) JahrBeenden(sim);
            if (buch.KandidatNummer(k.Person.Nummer) is null) // hat anderweitig geheiratet – neuer Versuch
                return Verheiraten(sim, konto, volk);
        }
        var e = w.Ausfuehren(konto, new Heiraten(k.Person.Nummer));
        Pruefe.Wahr(e.Erfolg, e.Meldung);
        return f.Ehepartner!;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Dynastie-Daten: Elben leben am längsten und bekommen am seltensten Kinder, Orks sind nicht spielbar", () =>
        {
            var d = Daten.Value.Dynastie;
            Pruefe.Wahr(d.IstAktiv, "keine Dynastie-Daten");
            Pruefe.Wahr(d.Volk(Volk.Elb).Sterbealter > d.Volk(Volk.Zwerg).Sterbealter
                        && d.Volk(Volk.Zwerg).Sterbealter > d.Volk(Volk.Mensch).Sterbealter
                        && d.Volk(Volk.Mensch).Sterbealter > d.Volk(Volk.Halbling).Sterbealter, "Lebenserwartung");
            Pruefe.Wahr(d.Volk(Volk.Elb).Geburtenrate < d.Volk(Volk.Zwerg).Geburtenrate
                        && d.Volk(Volk.Zwerg).Geburtenrate < d.Volk(Volk.Mensch).Geburtenrate
                        && d.Volk(Volk.Mensch).Geburtenrate <= d.Volk(Volk.Halbling).Geburtenrate, "Geburtenrate");
            Pruefe.Wahr(d.Volk(Volk.Zwerg).Hochzeit > d.Volk(Volk.Mensch).Hochzeit, "Zwerge verlangen eine hohe Mitgift");
            Pruefe.Wahr(d.Volk(Volk.Elb).Haushalt > d.Volk(Volk.Mensch).Haushalt * 2, "Elben leben teuer");
            Pruefe.WirftMitMeldung(() => Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie(Volk: Volk.Ork)), "nicht spielbar");
        }),

        ("Partieaufbau: eigenes Volk, Geschlecht und Vorname; Haus Eisenbart sind Zwerge; beide beginnen ledig", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value,
                new NeuePartie("Familie Apfelgrund", Volk: Volk.Halbling, Geschlecht: Geschlecht.Frau, Vorname: "Trudel"));
            var w = sim.Wirtschaft!;
            var f = w.Familien.Von(spieler)!;
            Pruefe.Gleich("Trudel Apfelgrund", f.Oberhaupt.Name);
            Pruefe.Gleich(Volk.Halbling, f.Volk);
            Pruefe.Gleich("Matriarchin", f.Oberhaupt.OberhauptTitel);
            Pruefe.Gleich(24, f.Oberhaupt.Alter(w.Familien.Jahr));
            Pruefe.Wahr(f.Ehepartner is null, "Spieler sollte ledig beginnen");
            var rivale = w.Familien.Von(w.Konten[1])!;
            Pruefe.Gleich(Volk.Zwerg, rivale.Volk);
            Pruefe.Gleich("Eisenbart", rivale.Nachname);
            Pruefe.Gleich(10, w.Familien.Kandidaten.Count);
            Pruefe.Wahr(w.Familien.Kandidaten.Count(k => k.Person.Geschlecht == Geschlecht.Frau) is >= 4 and <= 6, "Kandidaten beider Geschlechter");
        }),

        ("Sterberisiko wächst mit dem Alter (Gompertz); die Verfassung zeigt es grob an", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var buch = w.Familien;
            var jung = Haus(w, "Familie Jung", alter: 25).Familie.Oberhaupt;
            var alt = Haus(w, "Familie Alt", alter: 70).Familie.Oberhaupt;
            var greis = Haus(w, "Familie Greis", alter: 85).Familie.Oberhaupt;
            Pruefe.Wahr(buch.Sterberisiko(jung) < 0.01, $"25 Jahre: {buch.Sterberisiko(jung):P1}");
            Pruefe.Wahr(buch.Sterberisiko(alt) > 0.08, $"70 Jahre: {buch.Sterberisiko(alt):P1}");
            Pruefe.Wahr(buch.Sterberisiko(greis) > 0.6, $"85 Jahre: {buch.Sterberisiko(greis):P1}");
            Pruefe.Gleich("rüstig", buch.Verfassung(jung));
            Pruefe.Gleich("gebrechlich", buch.Verfassung(greis));
            var elb = Haus(w, "Haus Elb", Volk.Elb, alter: 85).Familie.Oberhaupt;
            Pruefe.Gleich("rüstig", buch.Verfassung(elb)); // Elben altern langsamer
        }),

        ("Brautschau: nur das andere Geschlecht, nur standesgemäß, Geschenke wirken höchstens 20 Punkte im Jahr", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var buch = w.Familien;
            var (konto, f) = Haus(w, "Familie Werber");
            var gleich = buch.Kandidaten.First(k => k.Person.Geschlecht == Geschlecht.Mann);
            var e = w.Ausfuehren(konto, new Werben(gleich.Person.Nummer, 100));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("sucht eine Ehefrau"), e.Meldung);
            if (buch.Kandidaten.FirstOrDefault(k => k.Stand > 0 && k.Person.Geschlecht == Geschlecht.Frau) is { } hoch)
            {
                e = w.Ausfuehren(konto, new Werben(hoch.Person.Nummer, 100));
                Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("standesgemäß"), e.Meldung);
            }

            var k = buch.Kandidaten.First(x => buch.WerbenVerboten(konto, x) is null);
            var vorher = konto.Vermoegen;
            Pruefe.Wahr(w.Ausfuehren(konto, new Werben(k.Person.Nummer, 5000)).Erfolg, "Werben");
            Pruefe.Ungefaehr(20, buch.Zuneigung(konto, k), 0.001);
            var preis = 20 * buch.GuldenJeZuneigung(konto); // 400 ohne Rhetorik, je Stern Rhetorik 5 % billiger
            Pruefe.Wahr(preis < 400, $"Rhetorik macht das Werben günstiger ({preis:0})");
            Pruefe.Ungefaehr(preis, vorher - konto.Vermoegen, 0.001); // bezahlt wird nur, was wirkt
            Pruefe.Ungefaehr(preis, konto.Laufend.Familie, 0.001);
            e = w.Ausfuehren(konto, new Werben(k.Person.Nummer, 100));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("braucht Zeit"), e.Meldung);

            // Ansehen (Gemeiner mit 50.000 Gulden: 20) + Zuneigung 20 ≥ Schwelle 30 → Heirat möglich
            Pruefe.Wahr(buch.Bereitschaft(konto, k) >= buch.Heiratsschwelle(k), "Bereitschaft");
            vorher = konto.Vermoegen;
            e = w.Ausfuehren(konto, new Heiraten(k.Person.Nummer));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(k.Person, f.Ehepartner);
            Pruefe.Gleich("Werber", f.Ehepartner!.Familienname);
            Pruefe.Ungefaehr(buch.Hochzeitskosten(k), vorher - konto.Vermoegen, 0.001);
            Pruefe.Wahr(buch.KandidatNummer(k.Person.Nummer) is null, "Kandidat sollte die Brautschau verlassen");
            Pruefe.Wahr(buch.DiesesJahr.Any(x => x.Text.Contains("Hochzeit")), "Hochzeit fehlt in der Chronik");
            e = w.Ausfuehren(konto, new Werben(buch.Kandidaten.First(x => x.Person.Geschlecht == Geschlecht.Frau).Person.Nummer, 100));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("verheiratet"), e.Meldung);
        }),

        ("Werben braucht Zeit: Ohne Ansehen reicht ein Jahr nicht, die Zuneigung verblasst, im zweiten Jahr klappt es", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var buch = w.Familien;
            var (konto, _) = Haus(w, "Familie Arm", vermoegen: 3000);
            var k = buch.Kandidaten.First(x => buch.WerbenVerboten(konto, x) is null && x.Stand == 0);
            w.Ausfuehren(konto, new Werben(k.Person.Nummer, 1000));
            var e = w.Ausfuehren(konto, new Heiraten(k.Person.Nummer));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("zögert"), e.Meldung);
            JahrBeenden(sim);
            if (buch.KandidatNummer(k.Person.Nummer) is null) return; // anderweitig verheiratet – auch das gehört dazu
            Pruefe.Ungefaehr(14, buch.Zuneigung(konto, k), 0.001); // 20 − 30 %
            w.Ausfuehren(konto, new Werben(k.Person.Nummer, 1000));
            Pruefe.Wahr(w.Ausfuehren(konto, new Heiraten(k.Person.Nummer)).Erfolg, "Heirat im zweiten Jahr");
        }),

        ("Kinder: Junge Paare bekommen über die Jahre Kinder ihres Hauses; Unterhalt steht in der Jahresrechnung", () =>
        {
            // Über mehrere Startwerte, damit kein einzelner Glücks- oder Pechwurf entscheidet:
            // Erwartet werden je Paar etwa 3 Kinder in 12 Jahren.
            var kinderGesamt = 0;
            foreach (var startwert in new[] { 1, 2, 3, 4, 5, 6 })
            {
                var sim = NeueSimulation(startwert: startwert);
                var w = sim.Wirtschaft!;
                var (konto, f) = Haus(w, "Familie Kinderreich", alter: 20);
                var partner = Verheiraten(sim, konto, Volk.Mensch);
                Pruefe.Wahr(w.Familien.Geburtschance(f) > 0.2, $"Geburtschance {w.Familien.Geburtschance(f):P0}");
                for (var i = 0; i < 12; i++) JahrBeenden(sim);
                var kinder = f.KinderVon(partner).ToList();
                kinderGesamt += kinder.Count;
                Pruefe.Wahr(kinder.All(k => k.Volk == Volk.Mensch && k.Familienname == "Kinderreich"), "Kinder gehören zum Haus");
                // Gezahlt wird vor dem Jahreswechsel – danach kann sich die Familie schon verändert haben.
                Pruefe.Wahr(konto.Vorjahr!.Familie >= 50, $"Familienkosten {konto.Vorjahr.Familie}");
            }
            Pruefe.Wahr(kinderGesamt >= 8, $"6 Paare haben in 12 Jahren nur {kinderGesamt} Kinder");
        }),

        ("Ehe zwischen Völkern: erlaubt, Kinder gehören dem Haus-Volk, aber deutlich weniger fruchtbar", () =>
        {
            var sim = NeueSimulation(startwert: 3);
            var w = sim.Wirtschaft!;
            var buch = w.Familien;
            var (konto, f) = Haus(w, "Haus Ambossfaust", Volk.Zwerg, alter: 40);
            var zwergin = buch.Kandidaten.FirstOrDefault(k => k.Person.Volk == Volk.Zwerg && k.Person.Geschlecht == Geschlecht.Frau);
            var menschin = buch.Kandidaten.First(k => k.Person.Volk == Volk.Mensch && k.Person.Geschlecht == Geschlecht.Frau);
            if (zwergin is not null)
                Pruefe.Wahr(buch.ErwarteteKinder(f, zwergin.Person) > buch.ErwarteteKinder(f, menschin.Person), "Zwergin verspricht mehr Kinder");
            var rate = Daten.Value.Dynastie.Volk(Volk.Zwerg).Geburtenrate * Daten.Value.Dynastie.Mischehe.Fruchtbarkeit;
            Verheiraten(sim, konto, Volk.Mensch);
            var chance = buch.Geburtschance(f);
            Pruefe.Wahr(chance == 0 || Math.Abs(chance - rate) < 1e-9, $"Geburtschance {chance} statt {rate}");
        }),

        ("Erbfolge: Testament vor dem ältesten volljährigen Kind; Ämter und Gunst beginnen von vorn", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (konto, f) = Haus(w, "Familie Erblich", alter: 130);
            var tochter = Kind(w, f, 30, Geschlecht.Frau, "Agnes");
            var sohn = Kind(w, f, 20, Geschlecht.Mann, "Konrad");
            Kind(w, f, 5, Geschlecht.Mann, "Klein");
            Pruefe.Gleich(tochter, w.Familien.Erbe(f)); // gleichberechtigt: das älteste Kind
            Pruefe.Wahr(w.Ausfuehren(konto, new ErbenBestimmen(sohn.Nummer)).Erfolg, "Testament");
            Pruefe.Gleich(sohn, w.Familien.Erbe(f));

            // Ein Amt und Gunst, die beim Erbfall verloren gehen
            Pruefe.Wahr(w.Ausfuehren(Wohnhaushilfe.MitWohnhaus(w, konto), new StandErwerben()).Erfolg, "Aufstieg");
            var ratsherr = w.Rat.Ratsherren[0];
            w.Ausfuehren(konto, new Schenken(ratsherr.Name, 400));
            Pruefe.Wahr(w.Rat.Gunst(ratsherr, konto) > 0, "Gunst");

            var altesOberhaupt = f.Oberhaupt;
            Person? verstorben = null, nachfolger = null;
            w.Familien.Erbfall += (_, tot, neu) => (verstorben, nachfolger) = (tot, neu);
            JahrBeenden(sim);
            Pruefe.Gleich(sohn, f.Oberhaupt);
            Pruefe.Gleich(altesOberhaupt, verstorben);
            Pruefe.Gleich(sohn, nachfolger);
            Pruefe.Wahr(f.Ahnen.Count == 1 && !f.Ahnen[0].Lebt, "Ahnenreihe");
            Pruefe.Wahr(konto.IstAktiv, "Haus sollte weiterleben");
            Pruefe.Gleich(0.0, w.Rat.Gunst(ratsherr, konto));
            Pruefe.Gleich(1, konto.Stand); // der Stand bleibt
            Pruefe.Wahr(w.Familien.LetztesJahr.Any(e => e.Text.Contains("Wie im Testament bestimmt")), "Chronik");
        }),

        ("Witwenrecht: Mit nur unmündigen Kindern führt der Ehepartner das Haus weiter", () =>
        {
            var sim = NeueSimulation(startwert: 11);
            var w = sim.Wirtschaft!;
            var (konto, f) = Haus(w, "Familie Witwe", alter: 60);
            var partner = Verheiraten(sim, konto, Volk.Mensch);
            Kind(w, f, 3);
            // Das Oberhaupt altert schlagartig: so stirbt es am Jahresende sicher.
            var alt = new Person(_nummer++, f.Oberhaupt.Vorname, f.Nachname, Volk.Mensch, Geschlecht.Mann, w.Familien.Jahr - 130);
            f.MitgliederListe.Remove(f.Oberhaupt);
            f.MitgliederListe.Add(alt);
            f.Oberhaupt = alt;
            Pruefe.Gleich(partner, w.Familien.Erbe(f));
            JahrBeenden(sim);
            if (!partner.Lebt) return; // sehr selten stirbt auch der Partner
            Pruefe.Gleich(partner, f.Oberhaupt);
            Pruefe.Wahr(f.Ehepartner is null, "Die Witwe ist nun ledig");
            Pruefe.Wahr(konto.IstAktiv, "Haus lebt weiter");
        }),

        ("Ohne Erben erlischt das Haus: Betriebe stehen still, keine Befehle mehr, „Letzte Dynastie“ zählt es nicht", () =>
        {
            var sim = NeueSimulation(sieg: Siegbedingung.LetzteDynastie);
            var w = sim.Wirtschaft!;
            var (konto, f) = Haus(w, "Familie Letzt", alter: 130);
            Kind(w, f, 4); // unmündig, kein Ehepartner
            var betrieb = w.BetriebEroeffnen("Gießerei Letzt", Betriebsart.Giesserei, konto);
            w.Einstellen(betrieb, "Geselle", 2);
            var (anderes, _) = Haus(w, "Familie Bleibt", alter: 25);
            Konto? erloschen = null;
            w.Familien.Erloschen += k => erloschen = k;
            JahrBeenden(sim);
            Pruefe.Gleich(konto, erloschen);
            Pruefe.Wahr(konto.IstErloschen && !konto.IstAktiv, "erloschen");
            Pruefe.Gleich(0, betrieb.Belegt);
            var e = w.Ausfuehren(Wohnhaushilfe.MitWohnhaus(w, konto), new StandErwerben());
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("erloschen"), e.Meldung);
            Pruefe.Wahr(sim.IstBeendet, "Nur noch eine Dynastie – die Partie sollte enden");
            Pruefe.Gleich(anderes, w.Rangliste[0]);
        }),

        ("Volksnähe im Rat: eigenes Volk +5; Ehe zwischen Völkern −5 beim eigenen, +3 beim Volk des Partners", () =>
        {
            var sim = NeueSimulation(startwert: 5);
            var w = sim.Wirtschaft!;
            var buch = w.Familien;
            var (konto, _) = Haus(w, "Haus Grauschmied", Volk.Zwerg, alter: 40);
            Pruefe.Gleich(5.0, buch.Volksnaehe(Volk.Zwerg, konto));
            Pruefe.Gleich(0.0, buch.Volksnaehe(Volk.Mensch, konto));
            Verheiraten(sim, konto, Volk.Mensch);
            Pruefe.Gleich(0.0, buch.Volksnaehe(Volk.Zwerg, konto));
            Pruefe.Gleich(3.0, buch.Volksnaehe(Volk.Mensch, konto));
            var zwergRatsherr = w.Rat.Ratsherren.First(r => r.Daten.Volk == Volk.Zwerg);
            var amt = w.Rat.Aemter.First(a => a.Stufe == 1);
            Pruefe.Ungefaehr(w.Rat.Ansehen(konto) + w.Rat.Gunst(zwergRatsherr, konto) + buch.RhetorikPunkte(konto) + w.Glaube.Naehe(zwergRatsherr, konto),
                w.Rat.Punkte(zwergRatsherr, amt, konto), 1e-9);
        }),

        ("KI: Haus Eisenbart wirbt und heiratet in den ersten Jahren; Zwerge bekommen langsam Kinder", () =>
        {
            // Erwartet: etwa 1,5 Nachkommen je Zwergenhaus nach 20 Jahren (Messung, siehe Wirtschaftsmodell).
            var kinder = 0;
            foreach (var startwert in new[] { 101, 202, 303, 404, 505, 606, 707, 808, 909, 1010 })
            {
                var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert },
                    Daten.Value, new NeuePartie());
                var w = sim.Wirtschaft!;
                var rivale = w.Konten[1];
                var f = w.Familien.Von(rivale)!;
                for (var i = 0; i < 6; i++) JahrBeenden(sim);
                Pruefe.Wahr(f.Ehepartner is not null || f.Ahnen.Count > 0, $"Startwert {startwert}: nach 6 Jahren nicht verheiratet");
                Pruefe.Wahr(rivale.Vorjahr!.Familie > 0, "Familienkosten fehlen");
                for (var i = 0; i < 14; i++) JahrBeenden(sim);
                kinder += f.Mitglieder.Count(p => p.Vater is not null);
            }
            Pruefe.Wahr(kinder >= 8, $"10 Zwergenhäuser haben in 20 Jahren nur {kinder} Nachkommen");
        }),

        ("Speichern: Familien, Brautschau, Zuneigung und Chronik überstehen Speichern und Laden", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 202 },
                Daten.Value, new NeuePartie(Volk: Volk.Elb, Geschlecht: Geschlecht.Frau));
            var w = sim.Wirtschaft!;
            for (var i = 0; i < 6; i++) JahrBeenden(sim);
            var k = w.Familien.Kandidaten.First(x => w.Familien.WerbenVerboten(spieler, x) is null);
            w.Ausfuehren(spieler, new Werben(k.Person.Nummer, 200));
            var (kopie, kopieSpieler) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            var a = w.Familien;
            var b = kopie.Wirtschaft!.Familien;
            Pruefe.Gleich(a.Von(spieler)!.Oberhaupt.NameMitHerkunft, b.Von(kopieSpieler!)!.Oberhaupt.NameMitHerkunft);
            Pruefe.Gleich(Volk.Elb, b.Von(kopieSpieler!)!.Volk);
            var rivA = a.Von(w.Konten[1])!;
            var rivB = b.Von(kopie.Wirtschaft.Konten[1])!;
            Pruefe.Gleich(rivA.Ehepartner?.NameMitHerkunft, rivB.Ehepartner?.NameMitHerkunft);
            Pruefe.Gleich(rivA.Mitglieder.Count, rivB.Mitglieder.Count);
            Pruefe.Gleich(a.Kandidaten.Count, b.Kandidaten.Count);
            Pruefe.Ungefaehr(a.Zuneigung(spieler, k), b.Zuneigung(kopieSpieler!, b.KandidatNummer(k.Person.Nummer)!), 1e-9);
            Pruefe.Ungefaehr(a.ZuneigungRest(spieler, k), b.ZuneigungRest(kopieSpieler!, b.KandidatNummer(k.Person.Nummer)!), 1e-9);
            Pruefe.Gleich(a.LetztesJahr.Count, b.LetztesJahr.Count);
            Pruefe.Gleich(a.Jahr, b.Jahr);
        }),
    };
}
