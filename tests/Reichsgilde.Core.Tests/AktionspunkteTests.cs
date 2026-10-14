using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für Aktionspunkte: Kosten persönlicher Handlungen, Übertrag, Training, KI.</summary>
static class AktionspunkteTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Simulation NeueSimulation(int apJeRunde = 4, int startwert = 1400) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert, AktionspunkteProRunde = apJeRunde },
            wirtschaftsdaten: Daten.Value);

    static (Konto Konto, Familie Familie) Haus(Stadtwirtschaft w, string name = "Familie Fuchs")
    {
        var konto = w.KontoEroeffnen(name, startvermoegen: 50_000);
        return (konto, w.Familien.Gruenden(konto, Volk.Mensch, Geschlecht.Mann, alter: 24));
    }

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    static Kandidat Kandidat(Stadtwirtschaft w, Konto k) =>
        w.Familien.Kandidaten.First(x => w.Familien.WerbenVerboten(k, x) is null);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("AP: Jedes Haus beginnt mit den AP je Runde aus den Partie-Einstellungen", () =>
        {
            Pruefe.Gleich(4, Haus(NeueSimulation().Wirtschaft!).Konto.Aktionspunkte.Vorrat);
            Pruefe.Gleich(6, Haus(NeueSimulation(apJeRunde: 6).Wirtschaft!).Konto.Aktionspunkte.Vorrat);
        }),

        ("AP: Persönliche Handlungen kosten, Wirtschaft ist frei, Abgelehntes kostet nichts", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (k, _) = Haus(w);
            var g = w.BetriebEroeffnen("Gießerei Fuchs", Betriebsart.Giesserei, k);
            Pruefe.Wahr(w.Ausfuehren(k, new ProduktWaehlen(g.Name, "Rapier")).Erfolg, "Produkt");
            Pruefe.Wahr(w.Ausfuehren(k, new PersonalSetzen(g.Name, "Geselle", 2)).Erfolg, "Personal");
            Pruefe.Gleich(4, k.Aktionspunkte.Vorrat);

            Pruefe.Wahr(w.Ausfuehren(k, new Werben(Kandidat(w, k).Person.Nummer, 100)).Erfolg, "Werben");
            Pruefe.Gleich(3, k.Aktionspunkte.Vorrat);
            Pruefe.Wahr(!w.Ausfuehren(k, new Werben(999_999, 100)).Erfolg, "unbekannter Kandidat");
            Pruefe.Gleich(3, k.Aktionspunkte.Vorrat); // abgelehnt → kostet nichts
            Pruefe.Gleich(1, w.ApKosten(new Schenken("", 0)));
            Pruefe.Gleich(2, w.ApKosten(new Anklagen("")));
            Pruefe.Gleich(2, w.ApKosten(new Erpressen("", 0)));
            Pruefe.Gleich(0, w.ApKosten(new AusbildungWaehlen(0, null)));
        }),

        ("AP: Reicht der Vorrat nicht, wird abgelehnt – mit verständlicher Meldung", () =>
        {
            var w = NeueSimulation(apJeRunde: 1).Wirtschaft!;
            var (k, _) = Haus(w);
            var rivale = w.KontoEroeffnen("Haus Eisenbart");
            Pruefe.Wahr(w.Ausfuehren(k, new Werben(Kandidat(w, k).Person.Nummer, 100)).Erfolg, "Werben");
            var e = w.Ausfuehren(k, new SpitzelAnsetzen(rivale.Inhaber));
            Pruefe.Wahr(!e.Erfolg, "ohne AP abgelehnt");
            Pruefe.Wahr(e.Meldung.Contains("Nicht genug Aktionspunkte"), e.Meldung);
            Pruefe.Gleich(0, k.Aktionspunkte.Vorrat);
        }),

        ("AP: Jede Runde kommen die AP dazu, angespart wird höchstens bis 12 – der Rest verfällt", () =>
        {
            var sim = NeueSimulation();
            var (k, _) = Haus(sim.Wirtschaft!);
            JahrBeenden(sim);
            Pruefe.Gleich(8, k.Aktionspunkte.Vorrat);
            JahrBeenden(sim);
            Pruefe.Gleich(12, k.Aktionspunkte.Vorrat);
            JahrBeenden(sim);
            Pruefe.Gleich(12, k.Aktionspunkte.Vorrat);
        }),

        ("Training: ½ Stern je Übung, je Talent einmal im Jahr, höchstens 6 – 2 AP unter 3 Sternen, 4 unter 5, darüber 6", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (k, f) = Haus(w);
            var o = f.Oberhaupt;
            o.TalentSetzen(Talent.Rhetorik, 2);
            Pruefe.Wahr(w.Ausfuehren(k, new TalentTrainieren(Talent.Rhetorik)).Erfolg, "üben");
            Pruefe.Gleich(2.5, o.Talent(Talent.Rhetorik));
            Pruefe.Gleich(2, k.Aktionspunkte.Vorrat);
            var e = w.Ausfuehren(k, new TalentTrainieren(Talent.Rhetorik));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("schon geübt"), e.Meldung);
            Pruefe.Gleich(2, k.Aktionspunkte.Vorrat);

            o.TalentSetzen(Talent.Kampf, 6);
            Pruefe.Wahr(w.Ausfuehren(k, new TalentTrainieren(Talent.Kampf)).Meldung.Contains("meisterhaft"), "Höchstwert");

            // Gestaffelte Kosten
            o.TalentSetzen(Talent.Verhandeln, 3);
            o.TalentSetzen(Talent.Handwerk, 5.75);
            Pruefe.Gleich(4, w.ApKosten(k, new TalentTrainieren(Talent.Verhandeln)));
            Pruefe.Gleich(6, w.ApKosten(k, new TalentTrainieren(Talent.Handwerk)));
            Pruefe.Gleich(2, w.ApKosten(k, new TalentTrainieren(Talent.NachtUndNebel)));
            var e2 = w.Ausfuehren(k, new TalentTrainieren(Talent.Handwerk));
            Pruefe.Wahr(!e2.Erfolg && e2.Meldung.Contains("Nicht genug Aktionspunkte"), e2.Meldung);
            k.Aktionspunkte.Setzen(10);
            Pruefe.Wahr(w.Ausfuehren(k, new TalentTrainieren(Talent.Handwerk)).Erfolg, "Handwerk");
            Pruefe.Gleich(6, o.Talent(Talent.Handwerk)); // gedeckelt
            Pruefe.Gleich(4, k.Aktionspunkte.Vorrat);
            Pruefe.Wahr(w.Ausfuehren(k, new TalentTrainieren(Talent.Verhandeln)).Erfolg, "Verhandeln");
            Pruefe.Gleich(0, k.Aktionspunkte.Vorrat);

            JahrBeenden(sim);
            Pruefe.Gleich(4, k.Aktionspunkte.Vorrat);
            Pruefe.Wahr(w.Ausfuehren(k, new TalentTrainieren(Talent.Rhetorik)).Erfolg, "neues Jahr – wieder üben");
            Pruefe.Gleich(3, o.Talent(Talent.Rhetorik));
        }),

        ("Daten: Ein Tippfehler im Befehlsnamen der Kostentabelle fällt beim Laden auf", () =>
        {
            var json = File.ReadAllText(Datenpfad.Wirtschaft()).Replace("\"Werben\": 1", "\"Werbn\": 1");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(json), "„Werbn“ gibt es nicht");
        }),

        ("KI: hält sich an die AP – nie unter 0, nie über 12, und übt, wenn sonst AP verfielen", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 77 },
                Daten.Value, new NeuePartie(Rivale: KiCharakter.Vorsichtig));
            var w = sim.Wirtschaft!;
            var rivale = w.Konten.First(k => k != spieler);
            rivale.Aktionspunkte.Setzen(12);
            var f = w.Familien.Von(rivale)!;
            var vorher = f.Oberhaupt.TalentSumme;
            for (var jahr = 0; jahr < 15; jahr++)
            {
                JahrBeenden(sim);
                Pruefe.Wahr(rivale.Aktionspunkte.Vorrat is >= 0 and <= 12, $"Vorrat {rivale.Aktionspunkte.Vorrat}");
            }
            Pruefe.Wahr(w.Familien.Von(rivale)!.Oberhaupt.TalentSumme > vorher || w.Familien.Von(rivale)!.Oberhaupt != f.Oberhaupt,
                "die KI hat nie geübt");
        }),

        ("Speichern: AP-Vorrat und Übungsjahre überstehen Speichern und Laden", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 33 },
                Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            Pruefe.Wahr(w.Ausfuehren(spieler, new TalentTrainieren(Talent.Verhandeln)).Erfolg, "üben");
            var (kopie, kopieSpieler) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            Pruefe.Gleich(spieler.Aktionspunkte.Vorrat, kopieSpieler!.Aktionspunkte.Vorrat);
            Pruefe.Gleich(4, kopie.Wirtschaft!.AktionspunkteJeRunde);
            var e = kopie.Wirtschaft.Ausfuehren(kopieSpieler, new TalentTrainieren(Talent.Verhandeln));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("schon geübt"), e.Meldung);
        }),
    };
}
