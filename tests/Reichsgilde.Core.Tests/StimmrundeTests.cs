using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Mehrspieler;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Tests für die Stimmrunde vor der Ratssitzung (Ratssitzung PR 8, Entscheidung F5): Die Menschen stimmen in den letzten 120 Minuten
/// des Jahres mit Befehlen ab, die Zeit läuft weiter; am Jahresende hält der Kern die Sitzung in einem Zug ab, die Szene spielt das
/// feste Ergebnis nur noch ab. Jeder Test mit Gegenprobe.
/// </summary>
static class StimmrundeTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));

    static void Ok(Stadtwirtschaft w, Konto k, Befehl befehl)
    {
        var e = w.Ausfuehren(k, befehl);
        Pruefe.Wahr(e.Erfolg, e.Meldung);
    }

    /// <summary>
    /// Ein voller Rat wie in den Tests der Ratssitzung: Reformer (Gefängniswärter, ein Mensch) beantragt Statut A; ohne den Spieler steht
    /// es 3 zu 3, der Bürgermeister ist dafür. Der Spieler sitzt als Marktmeister, eine KI als Stadthauptmann. Ein Bewerber will Zöllner werden.
    /// </summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto Spieler, Konto Reformer, Konto Bewerber) Aufbau(int startwert = 7)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert },
                                 wirtschaftsdaten: Daten.Value);
        var w = sim.Wirtschaft!;
        var reformer = StadtkasseTests.Haus(w, "Reformer", 50_000);
        StadtkasseTests.Sitz(w, "Gefängniswärter", reformer);
        var spieler = StadtkasseTests.Haus(w, "Spieler", 50_000);
        StadtkasseTests.Sitz(w, "Marktmeister", spieler);
        var ki = StadtkasseTests.Haus(w, "KI-Haus", 50_000);
        w.KiAnmelden(ki, KiProfil.Standard);
        StadtkasseTests.Sitz(w, "Stadthauptmann", ki);
        var bewerber = w.KontoEroeffnen("Bewerber", startvermoegen: 1_000_000);
        Ok(w, Wohnhaushilfe.MitWohnhaus(w, bewerber), new StandErwerben());
        bewerber.Schaden(bewerber.Vermoegen - 20_000);
        Ok(w, reformer, new StatutBeantragen(Fronstatut.FreieStadt));
        Ok(w, bewerber, new AmtBewerben("Zöllner"));
        return (sim, w, spieler, reformer, bewerber);
    }

    /// <summary>Tickt bis zum Beginn der Stimmrunde (die Zeit läuft, keine Sitzung hält an).</summary>
    static void BisStimmrunde(Simulation sim)
    {
        while (!sim.Wirtschaft!.Rat.StimmrundeLaeuft) sim.Tick();
    }

    /// <summary>Tickt bis ins neue Jahr – ohne Halt: Danach ist die Sitzung abgehalten.</summary>
    static void BisJahresende(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr)
        {
            sim.Tick();
            Pruefe.Wahr(sim.Ratssitzung is null, "die Partie hält nie vor der Sitzung an");
        }
    }

    static Sitzungspunkt Punkt(Stadtwirtschaft w, Sitzungsthema thema) => w.Rat.LetzteRatssitzung!.Punkte.Single(p => p.Thema == thema);

    static Sitzungsstimme StimmeVon(Sitzungspunkt p, Konto k) => p.Stimmen.Single(s => s.Haus == k);

    /// <summary>Alles, was die Szene von einer abgehaltenen Sitzung liest, als Text.</summary>
    static string Wiedergabe(Ratssitzung s) => string.Join("\n", s.Punkte.Select(p =>
        $"{p.Nummer} {p.Thema} {p.Text} | {string.Join(",", p.Stimmen.Select(x => $"{x.Name}:{x.Ja}:{x.Kandidat}:{x.Los}"))} | " +
        $"{p.Ergebnis!.Ja}:{p.Ergebnis.Nein}:{p.Ergebnis.Enthaltung}:{p.Ergebnis.Buergermeister}:{p.Ergebnis.Angenommen}:{p.Ergebnis.Gewaehlt}:" +
        string.Join(",", p.Ergebnis.Zaehlung.Select(z => $"{z.Kandidat}={z.Stimmen}"))));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Stimmrunde: Sie läuft in den letzten Minuten des Jahres (Frist „standard“) und zeigt dem Menschen seine freien Punkte, der KI keine", () =>
        {
            var (sim, w, spieler, _, _) = Aufbau();
            var ki = w.Rivalen.Single().Konto;
            Pruefe.Gleich(120, w.Entscheidungen.Daten.FristStandard);
            Pruefe.Gleich((long)Spielzeit.MinutenProTag, w.Rat.StimmrundeFrist);
            Pruefe.Gleich((long)Spielzeit.MinutenProTag - 120, w.Rat.StimmrundeBeginn);
            // Gegenprobe: vor der Stimmrunde keine Punkte.
            Pruefe.Wahr(!w.Rat.StimmrundeLaeuft, "nicht am Jahresanfang");
            Pruefe.Gleich(0, w.Rat.Stimmrunde(spieler).Count);
            BisStimmrunde(sim);
            Pruefe.Gleich(w.Rat.StimmrundeBeginn, sim.Zeit.VergangeneMinuten);
            Pruefe.Gleich(21 * 60, sim.Zeit.UhrzeitInMinuten);
            var punkte = w.Rat.Stimmrunde(spieler);
            Pruefe.Wahr(punkte.Any(p => p.Thema == Sitzungsthema.Statut) && punkte.Any(p => p.Thema == Sitzungsthema.Wahl),
                        string.Join("|", punkte.Select(p => p.Thema)));
            Pruefe.Wahr(punkte.All(p => StimmeVon(p, spieler).Frei), "nur freie Punkte");
            Pruefe.Gleich(0, w.Rat.Stimmrunde(ki).Count, "die KI stimmt selbst");
            Pruefe.Wahr(sim.Ratssitzung is null, "die Zeit läuft in der Stimmrunde");
            BisJahresende(sim);
            // Gegenprobe: im neuen Jahr ist die Stimmrunde vorbei.
            Pruefe.Wahr(!w.Rat.StimmrundeLaeuft, "nach der Sitzung vorbei");
            Pruefe.Gleich(2L * Spielzeit.MinutenProTag, w.Rat.StimmrundeFrist);
        }),

        ("Stimmrunde: Eine Stimme vorab zählt – im Jahr gesetzt (Wahl) wie in der Stimmrunde (Statut); Gegenprobe ohne Stimme", () =>
        {
            Sitzungspunkt StatutOhne, WahlOhne;
            {
                var (sim, w, _, _, _) = Aufbau();
                BisJahresende(sim);
                (StatutOhne, WahlOhne) = (Punkt(w, Sitzungsthema.Statut), Punkt(w, Sitzungsthema.Wahl));
            }
            {
                var (sim, w, spieler, _, bewerber) = Aufbau();
                Ok(w, spieler, new WahlStimme("Zöllner", bewerber.Inhaber)); // früh im Jahr
                BisStimmrunde(sim);
                Ok(w, spieler, new StatutStimme(false)); // in der Stimmrunde, die Zeit läuft
                BisJahresende(sim);
                var statut = Punkt(w, Sitzungsthema.Statut);
                var wahl = Punkt(w, Sitzungsthema.Wahl);
                Pruefe.Gleich<bool?>(false, StimmeVon(statut, spieler).Ja);
                Pruefe.Gleich(StatutOhne.Ergebnis!.Nein + 1, statut.Ergebnis!.Nein, "das Nein zählt");
                Pruefe.Gleich(StatutOhne.Ergebnis.Enthaltung - 1, statut.Ergebnis.Enthaltung);
                Pruefe.Wahr(StatutOhne.Ergebnis.Angenommen && !statut.Ergebnis.Angenommen, "das Nein kippt den Antrag (3:3 mit Bürgermeister → 3:4)");
                Pruefe.Gleich(bewerber.Inhaber, StimmeVon(wahl, spieler).Kandidat);
                var vorher = WahlOhne.Ergebnis!.Zaehlung.Single(z => z.Kandidat == bewerber.Inhaber).Stimmen;
                Pruefe.Gleich(vorher + 1, wahl.Ergebnis!.Zaehlung.Single(z => z.Kandidat == bewerber.Inhaber).Stimmen, "die Wahlstimme zählt");
            }
        }),

        ("Stimmrunde: Ohne Stimme gilt die Voreinstellung – Enthaltung beim Antrag, Vorgabe bei der Wahl; Gegenprobe: die Vorschau der Stimmrunde nennt sie", () =>
        {
            var (sim, w, spieler, _, _) = Aufbau();
            BisStimmrunde(sim);
            var vorschau = w.Rat.Stimmrunde(spieler);
            var statutVorschau = StimmeVon(vorschau.Single(p => p.Thema == Sitzungsthema.Statut), spieler);
            var wahlVorschau = StimmeVon(vorschau.Single(p => p.Thema == Sitzungsthema.Wahl), spieler);
            Pruefe.Gleich<bool?>(null, statutVorschau.Ja);
            Pruefe.Gleich("Keine Stimme gesetzt – Enthaltung.", statutVorschau.Begruendung);
            Pruefe.Wahr(wahlVorschau.Begruendung.Contains("Vorgabe"), wahlVorschau.Begruendung);
            BisJahresende(sim);
            var statut = StimmeVon(Punkt(w, Sitzungsthema.Statut), spieler);
            var wahl = StimmeVon(Punkt(w, Sitzungsthema.Wahl), spieler);
            Pruefe.Gleich<bool?>(null, statut.Ja);
            Pruefe.Gleich(wahlVorschau.Kandidat, wahl.Kandidat, "die Vorgabe gilt");
            Pruefe.Wahr(Punkt(w, Sitzungsthema.Statut).Ergebnis!.Angenommen, "3:3, der Bürgermeister entscheidet");
        }),

        ("Stimmrunde: Das Ergebnis steht vor der Szene fest – alle Punkte abgehalten, eine Stimme nach der Frist ändert es nicht", () =>
        {
            var (sim, w, spieler, _, _) = Aufbau();
            BisStimmrunde(sim);
            BisJahresende(sim);
            var sitzung = w.Rat.LetzteRatssitzung!;
            Pruefe.Wahr(sitzung.IstBeendet && sitzung.Punkte.Count > 0 && sitzung.Punkte.All(p => p.Ergebnis is not null), "alle Punkte mit Ergebnis");
            Pruefe.Gleich(sitzung.Punkte.Count, sitzung.Abgehalten.Count);
            var vorher = Wiedergabe(sitzung);
            // Gegenprobe: Nach der Frist gilt eine Stimme erst der nächsten Sitzung – die abgehaltene bleibt.
            var nachher = w.Ausfuehren(spieler, new StatutStimme(false));
            Pruefe.Gleich(vorher, Wiedergabe(sitzung));
            Pruefe.Wahr(!nachher.Erfolg || w.Rat.Antrag is not null, nachher.Meldung);
        }),

        ("Stimmrunde: Die Wiedergabe ändert nichts – die Szene liest die Sitzung beliebig oft, Prüfsumme und Ergebnis bleiben", () =>
        {
            var (sim, w, spieler, _, _) = Aufbau();
            BisStimmrunde(sim);
            Ok(w, spieler, new StatutStimme(false));
            BisJahresende(sim);
            var summe = Zustandspruefsumme.Berechnen(sim);
            Pruefe.Wahr(summe is not null, "Prüfsumme nach der Sitzung");
            var sitzung = w.Rat.LetzteRatssitzung!;
            var erste = Wiedergabe(sitzung);
            for (var i = 0; i < 3; i++) // wie die Szene: Punkt für Punkt, dann noch einmal „Letzte Ratssitzung ansehen“
            {
                foreach (var p in sitzung.Punkte) _ = (p.Stimmen.Count, p.Vorschau, p.Ergebnis, sitzung.StimmeVerboten(p, spieler));
                _ = (sitzung.AktuellerPunkt, sitzung.Anzahl, w.Rat.Sitzungsvorschau(), w.Rat.Stimmrunde(spieler));
                Pruefe.Gleich(erste, Wiedergabe(sitzung));
            }
            Pruefe.Gleich(summe, Zustandspruefsumme.Berechnen(sim), "die Wiedergabe ändert den Zustand nicht");
            // Gegenprobe: Die Prüfsumme bemerkt eine echte Änderung.
            sim.Tick();
            Pruefe.Wahr(summe != Zustandspruefsumme.Berechnen(sim), "ein Tick ändert die Prüfsumme");
        }),

        ("Stimmrunde: Zum Pflaster stimmt man in der Stimmrunde; Speichern und Laden mittendrin behält die Stimme, ein alter Stand ohne Feld lädt mit Enthaltung", () =>
        {
            var katalog = Gebaeudekatalog.AusDatei(Path.Combine(Path.GetDirectoryName(Datenpfad.Wirtschaft())!, "katalog.json"));
            var daten = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
            var (sim, spieler) = Probestadtvorlage.Entwurf(katalog, daten).Ausprobieren(daten, "Schmied");
            var w = sim.Wirtschaft!;
            var nachbar = w.KontoEroeffnen("Nachbar", startvermoegen: 20_000);
            var nr = w.Karte!.Wege.FindIndex(x => x.Name == "Hauptstraße");
            var aemter = w.Rat.Aemter.OrderBy(a => a.Stufe).ToList();
            (aemter[0].Ratsherr, aemter[0].Konto) = (null, spieler);
            (aemter[1].Ratsherr, aemter[1].Konto) = (null, nachbar);
            Ok(w, spieler, new WegPflasterBeantragen(nr, 0, 40));
            // Gegenprobe: vor der Stimmrunde noch nicht.
            var frueh = w.Ausfuehren(nachbar, new PflasterStimme(false));
            Pruefe.Wahr(!frueh.Erfolg && frueh.Meldung.Contains("erst in der Stimmrunde"), frueh.Meldung);
            BisStimmrunde(sim);
            Pruefe.Wahr(w.Rat.Stimmrunde(nachbar).Any(p => p.Thema == Sitzungsthema.Pflaster), "Pflaster in der Stimmrunde");
            Ok(w, nachbar, new PflasterStimme(false));
            Pruefe.Gleich<bool?>(false, w.Wegebau.StimmeVon(nachbar));

            var json = Speicherung.Speichern(sim, spieler);
            var (geladen, _) = Speicherung.Laden(json);
            var gw = geladen.Wirtschaft!;
            var gNachbar = gw.Konten.Single(k => k.Inhaber == nachbar.Inhaber);
            Pruefe.Gleich<bool?>(false, gw.Wegebau.StimmeVon(gNachbar), "die Stimme übersteht Speichern und Laden");
            Pruefe.Gleich(Zustandspruefsumme.Berechnen(sim), Zustandspruefsumme.Berechnen(geladen));

            // Alter Spielstand: ohne das Feld PflasterStimmen – Enthaltung, der Rest lädt.
            var knoten = JsonNode.Parse(json)!;
            Pruefe.Wahr(Entfernen(knoten, "PflasterStimmen") == 1, "das Feld stand genau einmal im Spielstand");
            var (alt, _) = Speicherung.Laden(knoten.ToJsonString());
            var aNachbar = alt.Wirtschaft!.Konten.Single(k => k.Inhaber == nachbar.Inhaber);
            Pruefe.Gleich<bool?>(null, alt.Wirtschaft.Wegebau.StimmeVon(aNachbar));
            Pruefe.Wahr(alt.Wirtschaft.Wegebau.Antrag is not null, "der Antrag bleibt");

            // Die geladene Partie hält die Sitzung mit dem Nein ab, die alte mit Enthaltung.
            BisJahresende(geladen);
            BisJahresende(alt);
            var mitNein = Punkt(gw, Sitzungsthema.Pflaster).Ergebnis!;
            var ohne = Punkt(alt.Wirtschaft, Sitzungsthema.Pflaster).Ergebnis!;
            Pruefe.Gleich(ohne.Nein + 1, mitNein.Nein);
        }),

        ("Stimmrunde: Mehrspieler – zwei Menschen stimmen über den Boten mit Verzögerung, zwei Rechner kommen zur selben Prüfsumme; Gegenprobe mit anderer Stimme", () =>
        {
            string Lauf(bool andersrum)
            {
                var (sim, w, spieler, _, bewerber) = Aufbau();
                var zweiter = StadtkasseTests.Haus(w, "Zweiter", 50_000); // der zweite Mensch: Stadtrichter, wählt den Zöllner mit
                StadtkasseTests.Sitz(w, "Stadtrichter", zweiter);
                sim.Bote.Verzoegerung = 3; // wie im Lockstep: Befehle wirken erst einige Ticks später
                BisStimmrunde(sim);
                sim.Bote.Einreihen(spieler, new StatutStimme(false));
                sim.Bote.Einreihen(spieler, new WahlStimme("Zöllner", bewerber.Inhaber));
                for (var i = 0; i < 30; i++) sim.Tick();
                // Der zweite Mensch stimmt später, noch in der Frist.
                var gewuenscht = andersrum ? w.Rat.AmtNamens("Zöllner")!.Ratsherr!.Name : bewerber.Inhaber;
                sim.Bote.Einreihen(zweiter, new WahlStimme("Zöllner", gewuenscht));
                BisJahresende(sim);
                var wahl = Punkt(w, Sitzungsthema.Wahl);
                Pruefe.Gleich(bewerber.Inhaber, StimmeVon(wahl, spieler).Kandidat);
                Pruefe.Gleich(gewuenscht, StimmeVon(wahl, zweiter).Kandidat);
                Pruefe.Gleich<bool?>(false, StimmeVon(Punkt(w, Sitzungsthema.Statut), spieler).Ja);
                return Zustandspruefsumme.Berechnen(sim)! + "\n" + Wiedergabe(w.Rat.LetzteRatssitzung!);
            }
            var a = Lauf(false);
            var b = Lauf(false);
            Pruefe.Gleich(a, b, "zwei Rechner, dieselben Befehle");
            Pruefe.Wahr(a != Lauf(true), "eine andere Stimme ergibt einen anderen Stand");
        }),
    };

    /// <summary>Entfernt jede Eigenschaft dieses Namens (beliebige Schreibweise) im ganzen Baum; gibt die Zahl zurück.</summary>
    static int Entfernen(JsonNode knoten, string name)
    {
        var n = 0;
        if (knoten is JsonObject o)
        {
            foreach (var key in o.Select(x => x.Key).Where(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                o.Remove(key);
                n++;
            }
            foreach (var (_, wert) in o.ToList())
                if (wert is not null) n += Entfernen(wert, name);
        }
        else if (knoten is JsonArray a)
            foreach (var wert in a)
                if (wert is not null) n += Entfernen(wert, name);
        return n;
    }
}
