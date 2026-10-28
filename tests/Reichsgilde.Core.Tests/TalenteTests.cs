using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für Talente und Ausbildung (Dynastie Teil 2).</summary>
static class TalenteTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Simulation NeueSimulation(int startwert = 1400) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, wirtschaftsdaten: Daten.Value);

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    static (Konto Konto, Familie Familie) Haus(Stadtwirtschaft w, string name, Volk volk = Volk.Mensch, int? alter = null, double vermoegen = 50_000)
    {
        var konto = w.KontoEroeffnen(name, startvermoegen: vermoegen);
        return (konto, w.Familien.Gruenden(konto, volk, Geschlecht.Mann, alter: alter));
    }

    static int _nummer = 95_000;

    static Person Kind(Stadtwirtschaft w, Familie f, int alter, string vorname = "Kind")
    {
        var p = new Person(_nummer++, vorname, f.Nachname, f.Volk, Geschlecht.Mann, w.Familien.Jahr - alter, vater: f.Oberhaupt.Nummer);
        f.MitgliederListe.Add(p);
        return p;
    }

    static void AlleTalente(Person p, double wert)
    {
        p.ZuegeLeeren(); // Charakterzüge (PR #227) verschöben die wirksamen Sterne – die Tests hier prüfen die Talente allein
        foreach (var t in TalentTexte.Alle) p.TalentSetzen(t, wert);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Talente-Daten: Zwerge sind die besten Handwerker, Halblinge die geschicktesten Gauner, Elben die besten Redner", () =>
        {
            var t = Daten.Value.Dynastie.Talente;
            Pruefe.Wahr(t.IstAktiv, "keine Talentdaten");
            double Bester(Talent talent) => new[] { Volk.Mensch, Volk.Zwerg, Volk.Elb, Volk.Halbling }.Max(v => t.Grundwert(v, talent));
            Pruefe.Gleich(Bester(Talent.Handwerk), t.Grundwert(Volk.Zwerg, Talent.Handwerk));
            Pruefe.Gleich(Bester(Talent.NachtUndNebel), t.Grundwert(Volk.Halbling, Talent.NachtUndNebel));
            Pruefe.Gleich(Bester(Talent.Rhetorik), t.Grundwert(Volk.Elb, Talent.Rhetorik));
            Pruefe.Gleich(6, t.Ausbildungen.Count);
            Pruefe.Gleich("Bürger", t.Ausbildungen["Universität"].MindestStand);
            Pruefe.Gleich("2½", TalentTexte.Sterne(2.5));
            Pruefe.Gleich("½", TalentTexte.Sterne(0.5));
            Pruefe.Gleich("3", TalentTexte.Sterne(3.1));
        }),

        ("Gewürfelt: Grundwert des Volkes ± 1 Stern in halben Schritten; im Mittel genau der Grundwert", () =>
        {
            var summe = 0.0;
            var anzahl = 0;
            for (var s = 0; s < 60; s++)
            {
                var sim = NeueSimulation(startwert: s);
                var (_, f) = Haus(sim.Wirtschaft!, "Haus Grauschmied", Volk.Zwerg);
                var h = f.Oberhaupt.Anlage(Talent.Handwerk); // ohne Charakterzüge
                Pruefe.Wahr(h is >= 2 and <= 4, $"Handwerk {h}");
                Pruefe.Gleich(h, Math.Round(h * 2) / 2);
                summe += h;
                anzahl++;
                foreach (var k in sim.Wirtschaft!.Familien.Kandidaten)
                    Pruefe.Wahr(k.Person.TalentSumme > 0, "Kandidaten haben Talente");
            }
            Pruefe.Ungefaehr(3, summe / anzahl, 0.1);
        }),

        ("Vererbung: Kinder liegen zwischen dem Mittel der Eltern und dem Grundwert ihres Volkes", () =>
        {
            var geboren = 0;
            for (var s = 0; s < 12 && geboren < 3; s++)
            {
                var sim = NeueSimulation(startwert: 500 + s);
                var w = sim.Wirtschaft!;
                var (konto, f) = Haus(w, "Familie Fuchs", alter: 20);
                var buch = w.Familien;
                var k = buch.Kandidaten.FirstOrDefault(x => buch.WerbenVerboten(konto, x) is null && x.Person.Volk == Volk.Mensch && x.Person.Alter(buch.Jahr) < 26);
                if (k is null) continue;
                for (var j = 0; j < 4 && buch.Bereitschaft(konto, k) < buch.Heiratsschwelle(k) && buch.KandidatNummer(k.Person.Nummer) is not null; j++)
                {
                    w.Ausfuehren(konto, new Werben(k.Person.Nummer, 1000));
                    if (buch.Bereitschaft(konto, k) < buch.Heiratsschwelle(k)) JahrBeenden(sim);
                }
                if (!w.Ausfuehren(konto, new Heiraten(k.Person.Nummer)).Erfolg) continue;
                f.Oberhaupt.TalentSetzen(Talent.NachtUndNebel, 6);
                f.Ehepartner!.TalentSetzen(Talent.NachtUndNebel, 6);
                for (var j = 0; j < 8 && !f.Kinder.Any(); j++) JahrBeenden(sim);
                foreach (var kind in f.Kinder)
                {
                    // 0,5 × 6 + 0,5 × 1 (Mensch) = 3,5 ± 0,5
                    var n = kind.Talent(Talent.NachtUndNebel);
                    Pruefe.Wahr(n is >= 3 and <= 4, $"Nacht und Nebel des Kindes: {n}");
                    geboren++;
                }
            }
            Pruefe.Wahr(geboren >= 1, "kein Kind geboren");
        }),

        ("Ausbildung: beginnt in der zweiten Hälfte der Kindheit, kostet nur dann; Universität erst ab Bürger", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var buch = w.Familien;
            var (konto, f) = Haus(w, "Familie Lehrer", alter: 40);
            var klein = Kind(w, f, 5, "Klein");
            var gross = Kind(w, f, 9, "Gross");
            Pruefe.Gleich(8, buch.AusbildungAb(Volk.Mensch));
            Pruefe.Wahr(!buch.IstInAusbildungsalter(klein) && buch.IstInAusbildungsalter(gross), "Ausbildungsalter");

            var e = w.Ausfuehren(konto, new AusbildungWaehlen(klein.Nummer, "Zunftlehre"));
            Pruefe.Wahr(e.Erfolg && e.Meldung.Contains("beginnt mit 8"), e.Meldung);
            e = w.Ausfuehren(konto, new AusbildungWaehlen(gross.Nummer, "Universität"));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("Bürger"), e.Meldung);
            e = w.Ausfuehren(konto, new AusbildungWaehlen(f.Oberhaupt.Nummer, "Zunftlehre"));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("volljährig"), e.Meldung);
            Pruefe.Wahr(w.Ausfuehren(konto, new AusbildungWaehlen(gross.Nummer, "Zunftlehre")).Erfolg, "Zunftlehre");
            Pruefe.Ungefaehr(60, buch.Ausbildungskosten(f), 1e-9); // nur das große Kind lernt schon

            var handwerk = gross.Talent(Talent.Handwerk);
            var kampf = gross.Talent(Talent.Kampf);
            var kleinHandwerk = klein.Talent(Talent.Handwerk);
            JahrBeenden(sim);
            if (gross.Lebt)
            {
                Pruefe.Ungefaehr(Math.Min(6, handwerk + 0.5), gross.Talent(Talent.Handwerk), 1e-9);
                Pruefe.Ungefaehr(Math.Min(6, kampf + 0.25), gross.Talent(Talent.Kampf), 1e-9);
            }
            if (klein.Lebt) Pruefe.Gleich(kleinHandwerk, klein.Talent(Talent.Handwerk));
            Pruefe.Wahr(konto.Vorjahr!.Familie >= 60, "Ausbildung steht in der Jahresrechnung");
        }),

        ("Ausbildung: Jedes Volk lernt insgesamt gleich viel – Elben länger, aber langsamer", () =>
        {
            var buch = NeueSimulation().Wirtschaft!.Familien;
            foreach (var volk in new[] { Volk.Mensch, Volk.Zwerg, Volk.Elb, Volk.Halbling })
            {
                var jahre = Daten.Value.Dynastie.Volk(volk).Volljaehrig - buch.AusbildungAb(volk);
                Pruefe.Ungefaehr(8, jahre * buch.Ausbildungsfaktor(volk), 1e-9);
            }
        }),

        ("Abschluss: Wer volljährig wird, schließt die Ausbildung ab – mit einer Meldung im Familienbuch", () =>
        {
            for (var s = 0; s < 5; s++)
            {
                var sim = NeueSimulation(startwert: 70 + s);
                var w = sim.Wirtschaft!;
                var (konto, f) = Haus(w, "Familie Kloster", alter: 45);
                var kind = Kind(w, f, 15, "Ansgar");
                Pruefe.Wahr(w.Ausfuehren(konto, new AusbildungWaehlen(kind.Nummer, "Klosterschule")).Erfolg, "Klosterschule");
                JahrBeenden(sim);
                if (!kind.Lebt) continue;
                Pruefe.Gleich(null, kind.Ausbildung);
                Pruefe.Gleich("Klosterschule", kind.Abschluss);
                Pruefe.Wahr(w.Familien.LetztesJahr.Any(x => x.Text.Contains("Ausbildung abgeschlossen")), "Meldung");
                return;
            }
            throw new Exception("Das Kind ist in allen Partien gestorben.");
        }),

        ("Wirkung: Verhandeln, Handwerk und Rhetorik des Oberhaupts", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var buch = w.Familien;
            var (konto, f) = Haus(w, "Familie Talent");
            AlleTalente(f.Oberhaupt, 4);
            Pruefe.Ungefaehr(1.06, buch.VerkaufsFaktor(konto), 1e-9);
            Pruefe.Ungefaehr(1.12, buch.LeistungsFaktor(konto), 1e-9);
            Pruefe.Ungefaehr(8, buch.RhetorikPunkte(konto), 1e-9);
            var (gegner, g) = Haus(w, "Familie Stumm");
            AlleTalente(g.Oberhaupt, 1);
            Pruefe.Ungefaehr(3, buch.Plaedoyer(konto, gegner), 1e-9);
            Pruefe.Ungefaehr(-3, buch.Plaedoyer(gegner, konto), 1e-9);
            var ohne = w.KontoEroeffnen("Ohne Familie");
            Pruefe.Gleich(1.0, buch.VerkaufsFaktor(ohne));
        }),

        ("Wirkung: Handwerkskunst – ein begabtes Haus fertigt in derselben Zeit mehr", () =>
        {
            double Gefertigt(double handwerk)
            {
                var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 9 },
                    Daten.Value, new NeuePartie());
                var w = sim.Wirtschaft!;
                AlleTalente(w.Familien.Von(spieler)!.Oberhaupt, 0);
                w.Familien.Von(spieler)!.Oberhaupt.TalentSetzen(Talent.Handwerk, handwerk);
                for (var i = 0; i < 600; i++) sim.Tick();
                // Gezählt werden die Stücke – Umsatz schwankt mit Preisen und Fuhren und verdeckte die Wirkung.
                return spieler.Laufend.Hergestellt.Values.Sum() + w.Betriebe.First(b => b.Konto == spieler).Fortschritt / 1000;
            }
            Pruefe.Wahr(Gefertigt(5) > Gefertigt(0), $"Handwerk 5 soll mehr schaffen als 0 ({Gefertigt(5)} / {Gefertigt(0)})");
        }),

        ("Wirkung: Bei Nacht und Nebel hebt die Erfolgschance und senkt die Zeugenchance; Kampf stärkt Wegelagerer", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (konto, f) = Haus(w, "Familie Schatten");
            AlleTalente(f.Oberhaupt, 0);
            f.Oberhaupt.TalentSetzen(Talent.NachtUndNebel, 5);
            f.Oberhaupt.TalentSetzen(Talent.Kampf, 5);
            var u = w.Gauner;
            var g = u.Daten;
            Pruefe.Ungefaehr(g.Diebstahl.Chance + 0.2, u.Erfolgschance(Gaunerstueck.Diebstahl, null, konto), 1e-9);
            Pruefe.Ungefaehr(g.Diebstahl.Chance, u.Erfolgschance(Gaunerstueck.Diebstahl, null), 1e-9);
            Pruefe.Ungefaehr(g.ZeugenChance * 0.6, u.Zeugenchance(null, true, konto), 1e-9);
            Pruefe.Ungefaehr(Math.Min(1, g.Ueberfall.Staerke + 0.2), u.Staerke(konto), 1e-9);
            Pruefe.Ungefaehr(0.15, w.Familien.KampfSchutz(konto), 1e-9);
        }),

        ("KI: bildet ihre Kinder aus (ältestes Klosterschule bzw. Universität, die anderen Zunft, Wagemutige schicken eins auf die Gasse)", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Wagemut", alter: 45, vermoegen: 3000);
            var a = Kind(w, f, 10, "Erstes");
            var b = Kind(w, f, 7, "Zweites");
            var c = Kind(w, f, 3, "Drittes");
            w.KiAnmelden(konto, Partieaufbau.ProfilFuer(KiCharakter.Wagemutig) with { Gaunerei = 2 });
            for (var i = 0; i < 1440 && c.Ausbildung is null; i++) sim.Tick();
            Pruefe.Gleich("Klosterschule", a.Ausbildung); // Gemeiner – die Universität ist verschlossen
            Pruefe.Gleich("Zunftlehre", b.Ausbildung);
            Pruefe.Gleich("Gasse", c.Ausbildung);
        }),

        ("KI: setzt das begabteste volljährige Kind als Erben ein", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Erbe", alter: 60);
            var aelter = Kind(w, f, 30, "Alt");
            var begabt = Kind(w, f, 25, "Begabt");
            AlleTalente(aelter, 1);
            AlleTalente(begabt, 3);
            w.KiAnmelden(konto, KiProfil.Standard with { Gaunerei = 0 });
            for (var i = 0; i < 1440 && f.Testament is null; i++) sim.Tick();
            Pruefe.Gleich(begabt, f.Testament);
        }),

        ("Speichern: Talente, Ausbildung und Abschluss überstehen Speichern und Laden", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 33 },
                Daten.Value, new NeuePartie(Volk: Volk.Halbling));
            var w = sim.Wirtschaft!;
            var f = w.Familien.Von(spieler)!;
            var kind = Kind(w, f, 9, "Pippo");
            w.Ausfuehren(spieler, new AusbildungWaehlen(kind.Nummer, "Gasse"));
            kind.Abschluss = "Kontorlehre";
            var (kopie, kopieSpieler) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            var g = kopie.Wirtschaft!.Familien.Von(kopieSpieler!)!;
            foreach (var t in TalentTexte.Alle)
                Pruefe.Gleich(f.Oberhaupt.Talent(t), g.Oberhaupt.Talent(t));
            var kopieKind = g.Mitglieder.First(p => p.Nummer == kind.Nummer);
            Pruefe.Gleich("Gasse", kopieKind.Ausbildung);
            Pruefe.Gleich("Kontorlehre", kopieKind.Abschluss);
            var kA = w.Familien.Kandidaten[0].Person;
            var kB = kopie.Wirtschaft.Familien.Kandidaten[0].Person;
            Pruefe.Ungefaehr(kA.TalentSumme, kB.TalentSumme, 1e-9);
        }),
    };
}
