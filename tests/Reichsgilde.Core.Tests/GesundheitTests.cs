using Reichsgilde.Core.Zeit;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Gesundheit;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für Gesundheit: Krankheiten, Pest, Medicus, Kräuterschnaps, Krötenschleim.</summary>
static class GesundheitTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    /// <summary>Krötenschleim gelingt immer – so hängt der Test nicht am Zufall.</summary>
    static readonly Lazy<Wirtschaftsdaten> SichererSchleim = new(() => Wirtschaftsdaten.AusJson(Datenpfad.EinVersuchJeNacht(File.ReadAllText(Datenpfad.Wirtschaft()))
        .Replace("\"kroetenschleim\": { \"kosten\": 200, \"chance\": 0.6,", "\"kroetenschleim\": { \"kosten\": 200, \"chance\": 1.0,")
        .Replace("\"zeugenChance\": 0.25", "\"zeugenChance\": 0")));

    static Simulation NeueSimulation(int startwert = 1400, Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert },
            wirtschaftsdaten: daten ?? Daten.Value);

    static (Konto Konto, Familie Familie) Haus(Stadtwirtschaft w, string name = "Familie Fuchs", Volk volk = Volk.Mensch)
    {
        var konto = w.KontoEroeffnen(name, startvermoegen: 20_000);
        return (konto, w.Familien.Gruenden(konto, volk, Geschlecht.Mann, alter: 30));
    }

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Gesundheit-Daten: acht Krankheiten und zwei Vergiftungen, die Pest, Kräuterschnaps und Gegengift als Marktware", () =>
        {
            var g = Daten.Value.Gesundheit!;
            Pruefe.Gleich(10, g.Krankheiten.Count);
            Pruefe.Gleich(2, g.Krankheiten.Values.Count(k => k.Gegengift && k.Chance == 0)); // Vergiftungen kommen nur vom Dolch
            Pruefe.Wahr(Daten.Value.Waren.ContainsKey(g.Gegengift), "Gegengift fehlt auf dem Markt");
            Pruefe.Wahr(g.Pest is not null, "keine Pest");
            Pruefe.Wahr(Daten.Value.Waren.ContainsKey(g.Schnaps), "Schnaps fehlt auf dem Markt");
            Pruefe.Wahr(g.Pest!.Anfaelligkeit(Volk.Elb) < g.Pest.Anfaelligkeit(Volk.Mensch), "Elben sind weniger anfällig");
            var json = File.ReadAllText(Datenpfad.Wirtschaft()).Replace("\"Ruhr\", \"Lungenentzündung\", \"Haarausfall\"", "\"Ruhr\", \"Schnupfen\", \"Haarausfall\"");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(json), "„Schnupfen“ gibt es nicht");
        }),

        ("Krankheit: senkt die wirksamen Talente, nicht die Anlage – Üben und Speichern zählen die Anlage", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (k, f) = Haus(w);
            var o = f.Oberhaupt;
            o.ZuegeLeeren(); // ohne Charakterzüge (PR #227) – hier zählt nur die Krankheit
            o.TalentSetzen(Talent.Rhetorik, 3);
            Pruefe.Wahr(w.Gesundheit.Erkranken(k, o, "Husten"), "erkrankt");
            Pruefe.Gleich(2, o.Talent(Talent.Rhetorik));
            Pruefe.Gleich(3, o.Anlage(Talent.Rhetorik));
            Pruefe.Gleich(2, w.Familien.Talent(k, Talent.Rhetorik)); // die Wirkung im Spiel
            Pruefe.Wahr(w.Ausfuehren(k, new TalentTrainieren(Talent.Rhetorik)).Erfolg, "üben");
            Pruefe.Gleich(3.5, o.Anlage(Talent.Rhetorik));
            Pruefe.Wahr(!w.Gesundheit.Erkranken(k, o, "Ruhr"), "wer krank ist, bekommt keine zweite Krankheit");
        }),

        ("Krankheit: endet am Jahresende ihres letzten Jahres; Haarausfall des Oberhaupts kostet Ansehen", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (k, f) = Haus(w);
            var vorher = w.Rat.Ansehen(k);
            w.Gesundheit.Erkranken(k, f.Oberhaupt, "Haarausfall"); // dauert 2 Jahre
            Pruefe.Ungefaehr(vorher - 3, w.Rat.Ansehen(k), 1e-9);
            Pruefe.Gleich(w.Familien.Jahr + 1, f.Oberhaupt.Krankheit!.Bis);
            JahrBeenden(sim);
            Pruefe.Gleich("Haarausfall", f.Oberhaupt.Krankheit?.Name);
            JahrBeenden(sim);
            Pruefe.Wahr(f.Oberhaupt.Krankheit?.Name != "Haarausfall", "nach zwei Jahren überstanden");
        }),

        ("Krankheit: Etwa 15 % der Familienmitglieder erkranken je Jahr", () =>
        {
            int personen = 0, krank = 0;
            for (var s = 0; s < 60; s++)
            {
                var sim = NeueSimulation(startwert: 900 + s);
                var w = sim.Wirtschaft!;
                for (var i = 0; i < 20; i++) Haus(w, $"Haus {i}");
                w.Gesundheit.NaechstePest = 0; // keine Pest in dieser Zählung
                JahrBeenden(sim);
                foreach (var konto in w.Konten)
                    foreach (var p in w.Familien.Von(konto)!.Lebende)
                    {
                        personen++;
                        if (p.Krankheit is not null) krank++;
                    }
            }
            Pruefe.Ungefaehr(0.15, krank / (double)personen, 0.15);
        }),

        ("Medicus: heilt gewöhnliche Krankheiten sofort (Posten Familie); bei der Pest halbiert er das Sterberisiko", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (k, f) = Haus(w);
            var o = f.Oberhaupt;
            w.Gesundheit.Erkranken(k, o, "Lungenentzündung");
            var e = w.Ausfuehren(k, new MedicusRufen(o.Nummer));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Wahr(o.Krankheit is null, "geheilt");
            Pruefe.Gleich(120, k.Laufend.Familie);
            Pruefe.Wahr(!w.Ausfuehren(k, new MedicusRufen(o.Nummer)).Erfolg, "gesund – kein Medicus nötig");

            o.Krankheit = Erkrankung.Aus(w.Gesundheit.Daten, Erkrankung.PestName, w.Familien.Jahr);
            Pruefe.Ungefaehr(0.4, w.Gesundheit.Sterberisiko(o.Krankheit), 1e-9);
            Pruefe.Wahr(w.Ausfuehren(k, new MedicusRufen(o.Nummer)).Erfolg, "Medicus bei Pest");
            Pruefe.Ungefaehr(0.2, w.Gesundheit.Sterberisiko(o.Krankheit!), 1e-9);
            Pruefe.Gleich(420, k.Laufend.Familie);
            Pruefe.Gleich(Math.Max(0, o.Anlage(Talent.Verhandeln) - 2), o.Talent(Talent.Verhandeln)); // −2 auf alle Talente
        }),

        ("Kräuterschnaps: vom Markt in die Hausapotheke; heilt leichte Krankheiten, lindert die Pest – gegen Ruhr hilft nur der Medicus", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (k, f) = Haus(w);
            var o = f.Oberhaupt;
            var preis = w.Gesundheit.SchnapsPreis;
            Pruefe.Wahr(w.Ausfuehren(k, new SchnapsKaufen(3)).Erfolg, "kaufen");
            Pruefe.Gleich(3, w.Gesundheit.Apotheke(k));
            Pruefe.Ungefaehr(3 * preis, k.Laufend.Familie, 1e-6);

            w.Gesundheit.Erkranken(k, o, "Ruhr");
            var e = w.Ausfuehren(k, new SchnapsGeben(o.Nummer));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("nur der Medicus"), e.Meldung);
            o.Krankheit = null;
            w.Gesundheit.Erkranken(k, o, "Husten");
            Pruefe.Wahr(w.Ausfuehren(k, new SchnapsGeben(o.Nummer)).Erfolg, "Husten");
            Pruefe.Wahr(o.Krankheit is null, "geheilt");
            Pruefe.Gleich(2, w.Gesundheit.Apotheke(k));

            o.Krankheit = Erkrankung.Aus(w.Gesundheit.Daten, Erkrankung.PestName, w.Familien.Jahr);
            Pruefe.Wahr(w.Ausfuehren(k, new SchnapsGeben(o.Nummer)).Erfolg, "Pest");
            Pruefe.Ungefaehr(0.32, w.Gesundheit.Sterberisiko(o.Krankheit!), 1e-9);
            Pruefe.Wahr(!w.Ausfuehren(k, new SchnapsGeben(o.Nummer)).Erfolg, "nur einmal");
            w.Ausfuehren(k, new MedicusRufen(o.Nummer));
            Pruefe.Ungefaehr(0.16, w.Gesundheit.Sterberisiko(o.Krankheit!), 1e-9); // beides zusammen
        }),

        ("Pest: bricht im angesetzten Jahr aus, viele erkranken, Betriebe arbeiten 30 % langsamer, manche sterben", () =>
        {
            double mitPest = 0, ohnePest = 0;
            var tote = 0;
            for (var s = 0; s < 3; s++)
                foreach (var pest in new[] { true, false })
                {
                    var sim = NeueSimulation(startwert: 70 + s);
                    var w = sim.Wirtschaft!;
                    var (k, _) = Haus(w);
                    for (var i = 0; i < 4; i++) Haus(w, $"Haus {i}");
                    var g = w.BetriebEroeffnen("Gießerei Fuchs", Betriebsart.Giesserei, k);
                    w.Einstellen(g, "Geselle", 2);
                    w.Gesundheit.NaechstePest = pest ? 1401 : 0;
                    JahrBeenden(sim); // 1400 → 1401: Die Pest bricht aus
                    Pruefe.Gleich(pest, w.Gesundheit.IstPestjahr);
                    Pruefe.Ungefaehr(pest ? 0.3 : 0, w.Gesundheit.Krankenstand, 1e-9);
                    if (pest)
                    {
                        var krank = w.Konten.SelectMany(x => w.Familien.Von(x)!.Lebende).Count(p => p.Krankheit?.IstPest == true);
                        Pruefe.Wahr(krank >= 1, "niemand hat die Pest");
                        Pruefe.Wahr(w.Gesundheit.NaechstePest >= 1401 + 25 && w.Gesundheit.NaechstePest <= 1401 + 40, $"nächste Pest {w.Gesundheit.NaechstePest}");
                    }
                    JahrBeenden(sim);
                    var hergestellt = k.Vorjahr!.Hergestellt.Values.Sum();
                    if (pest) mitPest += hergestellt; else ohnePest += hergestellt;
                    if (pest) tote += w.Familien.LetztesJahr.Count(e => e.Text.Contains("an der Pest"));
                }
            Pruefe.Ungefaehr(0.7, mitPest / ohnePest, 0.08);
            Pruefe.Wahr(tote >= 1, "niemand ist an der Pest gestorben");
        }),

        ("Krötenschleim: Das Oberhaupt des Opfers erkrankt um 21 Uhr – eine Spur „Giftmischerei“ bleibt", () =>
        {
            var sim = NeueSimulation(daten: SichererSchleim.Value);
            var w = sim.Wirtschaft!;
            var (taeter, _) = Haus(w, "Familie Fuchs");
            var (opfer, f) = Haus(w, "Haus Eisenbart", Volk.Zwerg);
            var e = w.Ausfuehren(taeter, new Beauftragen(opfer.Inhaber, Gaunerstueck.Kroetenschleim));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(3, taeter.Aktionspunkte.Vorrat); // 1 AP
            while (sim.Zeit.MinuteImTag < Reichsgilde.Core.Gauner.Unterwelt.Nachtstunde + 1) sim.Tick();
            Pruefe.Wahr(f.Oberhaupt.Krankheit is not null, "nicht erkrankt");
            Pruefe.Wahr(w.Gesundheit.Daten.Kroetenschleim.Contains(f.Oberhaupt.Krankheit!.Name), f.Oberhaupt.Krankheit.Name);
            Pruefe.Wahr(w.Justiz.SpurenVon(taeter).Any(s => s.Art == Vergehen.Giftmischerei), "keine Spur");
        }),

        ("KI: holt bei Lebensgefahr den Medicus und hält Kräuterschnaps vorrätig", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 5 },
                Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            var rivale = w.Konten.First(k => k != spieler);
            var o = w.Familien.Von(rivale)!.Oberhaupt;
            w.Gesundheit.Erkranken(rivale, o, "Lungenentzündung");
            for (var i = 0; i < 17 * 60 - Spielzeit.TagesbeginnMinute; i++) sim.Tick(); // bis nach der Politikstunde
            Pruefe.Wahr(o.Krankheit is null, "die KI hat keinen Medicus geholt");
            Pruefe.Gleich(2, w.Gesundheit.Apotheke(rivale));
        }),

        ("Speichern: Krankheiten, Hausapotheke und die nächste Pest überstehen Speichern und Laden", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 33 },
                Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            var o = w.Familien.Von(spieler)!.Oberhaupt;
            w.Ausfuehren(spieler, new SchnapsKaufen(2));
            w.Gesundheit.Erkranken(spieler, o, "Vergesslichkeit");
            o.Krankheit = o.Krankheit! with { Medicus = true };
            var (kopie, kopieSpieler) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            var g = kopie.Wirtschaft!;
            var ko = g.Familien.Von(kopieSpieler!)!.Oberhaupt;
            Pruefe.Gleich("Vergesslichkeit", ko.Krankheit?.Name);
            Pruefe.Gleich(o.Krankheit!.Bis, ko.Krankheit!.Bis);
            Pruefe.Wahr(ko.Krankheit.Medicus, "Medicus vergessen");
            Pruefe.Gleich(o.Talent(Talent.Handwerk), ko.Talent(Talent.Handwerk));
            Pruefe.Gleich(2, g.Gesundheit.Apotheke(kopieSpieler!));
            Pruefe.Gleich(w.Gesundheit.NaechstePest, g.Gesundheit.NaechstePest);
        }),
    };
}
