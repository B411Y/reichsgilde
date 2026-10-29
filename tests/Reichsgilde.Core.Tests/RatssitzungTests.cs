using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Kartenbau.Staedte;

/// <summary>
/// Tests für die sichtbare Ratssitzung (PR #202): Tagesordnung mit Stimmen und Begründungen, Halt am Jahresende, Abstimmen live
/// Punkt für Punkt, Ergebnis je Punkt – und ohne Halt alles wie vorher.
/// </summary>
static class RatssitzungTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));

    static Simulation NeueSimulation(int startwert = 7) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, wirtschaftsdaten: Daten.Value);

    static void Ok(Stadtwirtschaft w, Konto k, Befehl befehl)
    {
        var e = w.Ausfuehren(k, befehl);
        Pruefe.Wahr(e.Erfolg, e.Meldung);
    }

    static void Abgelehnt(Stadtwirtschaft w, Konto k, Befehl befehl, string grund)
    {
        var e = w.Ausfuehren(k, befehl);
        Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains(grund), $"erwartet abgelehnt mit „{grund}“, war: {e.Erfolg} – {e.Meldung}");
    }

    /// <summary>Tickt bis zum Jahresende; mit Halt steht die Partie danach vor der Ratssitzung.</summary>
    static void BisJahresende(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    /// <summary>
    /// Ein voller Rat: Reformer (Gefängniswärter) beantragt Statut A, katholischen Stadtglauben, 4 % Steuer und ein Fest; ein Bewerber will
    /// Zöllner werden (hat zwei Ratsherren beschenkt); der Spieler sitzt als Marktmeister, eine KI als Stadthauptmann.
    /// Ohne den Spieler steht es beim Statut 3 zu 3 (der Bürgermeister ist dafür), bei der Wahl 2 zu 3 für den Ratsherrn im Amt.
    /// Läuft ein Jahr, stellt die KI womöglich eigene Anträge oder bewirbt sich – Tests, die ticken, zählen die Punkte deshalb nicht fest.
    /// </summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto Spieler, Konto Reformer, Konto Bewerber, Konto Ki) Aufbau(int startwert = 7)
    {
        var sim = NeueSimulation(startwert);
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
        Ok(w, reformer, new StadtglaubeBeantragen(Konfession.Katholisch));
        StadtkasseTests.KasseAuf(w, 9_000);
        Ok(w, reformer, new SteuersatzBeantragen(4));
        Ok(w, reformer, new FestBeantragen());
        return (sim, w, spieler, reformer, bewerber, ki);
    }

    /// <summary>Bewerbung um den Zöllner mit Geschenken an zwei Ratsherren – erst nach dem Antrag, damit die Gunst dort nicht zählt.</summary>
    static void Bewerben(Stadtwirtschaft w, Konto bewerber)
    {
        Ok(w, bewerber, new AmtBewerben("Zöllner"));
        foreach (var name in new[] { "Ulrich Wendelstein", "Aelindra vom Silberhain" })
            Ok(w, bewerber, new Schenken(name, 1000));
    }

    static string Zaehlung(Sitzungsergebnis e) =>
        $"{e.Ja}:{e.Nein}:{e.Enthaltung}:{e.Buergermeister}:{e.Angenommen}:{e.Gewaehlt}:{string.Join(",", e.Zaehlung.Select(z => $"{z.Kandidat}={z.Stimmen}"))}";

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Ratssitzung: Die Tagesordnung nennt alle Punkte in der Reihenfolge der Sitzung – mit Antragsteller, Begründung und je Sitz Stimme und Grund", () =>
        {
            var (_, w, spieler, reformer, bewerber, ki) = Aufbau();
            Bewerben(w, bewerber);
            var punkte = w.Rat.Sitzungsvorschau();
            Pruefe.Gleich("Statut|Stadtglaube|Steuersatz|Fest|Wahl", string.Join("|", punkte.Select(p => p.Thema)));
            Pruefe.Gleich("1|2|3|4|5", string.Join("|", punkte.Select(p => p.Nummer)));
            Pruefe.Gleich("Wahl zum Zöllner", punkte[4].Titel);
            Pruefe.Gleich("Bewerber|Hinnerk Tannenkamp", string.Join("|", punkte[4].Kandidaten.Select(k => k.Name)));
            foreach (var p in punkte)
            {
                Pruefe.Wahr(p.Begruendung.Length > 10 && p.Text.Length > 3, $"{p.Titel}: „{p.Text}“ – „{p.Begruendung}“");
                Pruefe.Wahr(p.Stimmen.Count > 0 && p.Stimmen.All(s => s.Begruendung.Length > 5), $"{p.Titel}: {string.Join(" | ", p.Stimmen.Select(s => s.Begruendung))}");
                Pruefe.Gleich<Sitzungsergebnis?>(null, p.Ergebnis);
                if (p.Thema != Sitzungsthema.Wahl)
                {
                    Pruefe.Gleich(reformer, p.Antragsteller);
                    Pruefe.Gleich(7, p.Stimmen.Count);
                    // Die Vorschau zählt dieselben Stimmen wie die Liste.
                    Pruefe.Gleich((p.Stimmen.Count(s => s.Ja == true), p.Stimmen.Count(s => s.Ja == false), p.Stimmen.Count(s => s.Ja is null)),
                                  (p.Vorschau.Ja, p.Vorschau.Nein, p.Vorschau.Enthaltung));
                    Pruefe.Gleich("Eigener Antrag.", p.Stimmen.Single(s => s.Haus == reformer).Begruendung);
                }
            }
            // Wie die Prognosen der Ratskammer.
            Pruefe.Gleich((3, 3, 1, (bool?)true), (punkte[0].Vorschau.Ja, punkte[0].Vorschau.Nein, punkte[0].Vorschau.Enthaltung, punkte[0].Vorschau.Buergermeister));
            Pruefe.Gleich(w.Rat.AbstimmungsPrognose(reformer, Fronstatut.FreieStadt), (punkte[0].Vorschau.Ja, punkte[0].Vorschau.Nein, punkte[0].Vorschau.Enthaltung, punkte[0].Vorschau.Buergermeister));
            Pruefe.Gleich(w.Glaube.AbstimmungsPrognose(reformer, Konfession.Katholisch), (punkte[1].Vorschau.Ja, punkte[1].Vorschau.Nein, punkte[1].Vorschau.Enthaltung, punkte[1].Vorschau.Buergermeister));
            Pruefe.Gleich(w.Ratsantraege.Prognose(w.Ratsantraege.Antrag(Antragsart.Fest)!), (punkte[3].Vorschau.Ja, punkte[3].Vorschau.Nein, punkte[3].Vorschau.Enthaltung, punkte[3].Vorschau.Buergermeister));
            // Der Spieler darf stimmen (Frei), die KI und die Ratsherren nicht; die KI begründet aus ihrer Rechnung.
            var statut = punkte[0];
            Pruefe.Wahr(statut.Stimmen.Single(s => s.Haus == spieler).Frei, "Spieler frei");
            Pruefe.Wahr(statut.Stimmen.Where(s => s.Haus != spieler).All(s => !s.Frei), "nur der Spieler frei");
            Pruefe.Gleich("Keine Stimme gesetzt – Enthaltung.", statut.Stimmen.Single(s => s.Haus == spieler).Begruendung);
            Pruefe.Wahr(statut.Stimmen.Single(s => s.Haus == ki).Begruendung.Contains("Gulden"), statut.Stimmen.Single(s => s.Haus == ki).Begruendung);
            Pruefe.Wahr(statut.Stimmen.Where(s => s.Haus is null).All(s => s.Begruendung.Contains("Haltung") || s.Begruendung.Contains("Meinung")),
                        string.Join(" | ", statut.Stimmen.Select(s => s.Begruendung)));
            // Wahl: Es wählen die höheren Ämter (ohne Kandidaten); der Spieler wählt nach der Vorgabe.
            var wahl = punkte[4];
            Pruefe.Gleich(5, wahl.Stimmen.Count);
            Pruefe.Wahr(wahl.Stimmen.Single(s => s.Haus == spieler).Begruendung.Contains("Vorgabe"), wahl.Stimmen.Single(s => s.Haus == spieler).Begruendung);
            Pruefe.Gleich("Hinnerk Tannenkamp", wahl.Stimmen.Single(s => s.Haus == spieler).Kandidat);
            Pruefe.Wahr(wahl.Stimmen.Where(s => s.Haus is null && !s.Los).All(s => s.Begruendung.Contains("Punkte")), string.Join(" | ", wahl.Stimmen.Select(s => s.Begruendung)));
            Pruefe.Gleich("Bewerber=2,Hinnerk Tannenkamp=3", string.Join(",", wahl.Vorschau.Zaehlung.Select(z => $"{z.Kandidat}={z.Stimmen}")));
            Pruefe.Gleich("Hinnerk Tannenkamp", wahl.Vorschau.Gewaehlt);
            // Ohne Rat keine Punkte.
            var ohne = new Simulation(Partieeinstellungen.Standard, wirtschaftsdaten: Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));
            Pruefe.Wahr(ohne.Wirtschaft!.Rat.Sitzungsvorschau().Count == 0 || ohne.Wirtschaft.Rat.IstAktiv, "ohne Anträge keine Punkte");
        }),

        ("Ratssitzung: Mit Halt steht die Partie am Jahresende vor dem ersten Punkt; die Vorschau jedes Punktes ist sein Ergebnis", () =>
        {
            var (sim, w, spieler, reformer, bewerber, _) = Aufbau();
            Bewerben(w, bewerber);
            Ratssitzung? gefragt = null;
            w.RatssitzungAnhalten = s => { gefragt = s; return true; };
            var jahre = new List<int>();
            sim.JahrBeendet += jahre.Add;
            var jahr = sim.Zeit.Jahr;
            BisJahresende(sim);
            var sitzung = sim.Ratssitzung!;
            Pruefe.Gleich(sitzung, gefragt);
            Pruefe.Gleich(jahr, sitzung.Jahr);
            Pruefe.Gleich(0, jahre.Count, "JahrBeendet erst nach der Sitzung");
            var anzahl = sitzung.Anzahl;
            Pruefe.Wahr(anzahl >= 5, $"{anzahl} Punkte");
            Pruefe.Gleich(anzahl, sitzung.Punkte.Count);
            Pruefe.Gleich("Statut|Stadtglaube|Steuersatz", string.Join("|", sitzung.Punkte.Take(3).Select(p => p.Thema)));
            var n = 0;
            while (sitzung.AktuellerPunkt is { } vorher)
            {
                n++;
                Pruefe.Gleich(n, vorher.Nummer);
                var nachher = sim.RatssitzungWeiter()!;
                Pruefe.Gleich(vorher.Thema, nachher.Thema);
                Pruefe.Wahr(nachher.Ergebnis is not null && nachher.Ergebnis.Protokoll.Count > 0, $"{nachher.Titel}: Ergebnis mit Protokoll");
                if (!vorher.Stimmen.Any(s => s.Los) && !vorher.Vorschau.Stichentscheid || vorher.Thema != Sitzungsthema.Wahl)
                    Pruefe.Gleich(Zaehlung(vorher.Vorschau), Zaehlung(nachher.Ergebnis! with { Protokoll = Array.Empty<string>() }), vorher.Titel);
                Pruefe.Gleich(n, sitzung.Abgehalten.Count);
                Pruefe.Wahr(sitzung.StimmeVerboten(nachher, spieler)!.Contains("schon abgestimmt"), "abgehalten");
            }
            Pruefe.Gleich(anzahl, n);
            Pruefe.Gleich<Ratssitzung?>(null, sim.Ratssitzung);
            Pruefe.Gleich($"{jahr}", string.Join(",", jahre));
            Pruefe.Gleich(sitzung, w.Rat.LetzteRatssitzung);
            Pruefe.Gleich(Fronstatut.FreieStadt, w.Statut);
            Pruefe.Gleich((Konfession?)Konfession.Katholisch, w.Glaube.Stadtglaube);
            Pruefe.Gleich("Hinnerk Tannenkamp", w.Rat.AmtNamens("Zöllner")!.Inhaber);
            Pruefe.Gleich("Hinnerk Tannenkamp", sitzung.Abgehalten.Single(p => p.Amt?.Name == "Zöllner").Ergebnis!.Gewaehlt);
            // Danach läuft die Zeit wieder.
            var minuten = sim.Zeit.VergangeneMinuten;
            sim.Tick();
            Pruefe.Gleich(minuten + 1, sim.Zeit.VergangeneMinuten);
        }),

        ("Ratssitzung: Der Spieler stimmt live – sein Nein kippt Statut und Glauben, seine Stimme entscheidet die Wahl", () =>
        {
            var (sim, w, spieler, reformer, bewerber, _) = Aufbau();
            Bewerben(w, bewerber);
            w.RatssitzungAnhalten = _ => true;
            BisJahresende(sim);
            var sitzung = sim.Ratssitzung!;

            var statut = sitzung.AktuellerPunkt!;
            Pruefe.Gleich(true, statut.Vorschau.Angenommen);
            Pruefe.Gleich<string?>(null, sitzung.StimmeVerboten(statut, spieler));
            Ok(w, spieler, new StatutStimme(false));
            statut = sitzung.AktuellerPunkt!;
            Pruefe.Gleich((bool?)false, statut.Stimmen.Single(s => s.Haus == spieler).Ja);
            Pruefe.Gleich("Stimme des Hauses.", statut.Stimmen.Single(s => s.Haus == spieler).Begruendung);
            Pruefe.Gleich(false, statut.Vorschau.Angenommen);
            // Die Stimme zum nächsten Punkt geht schon jetzt; alles andere wartet, bis die Sitzung vorbei ist.
            Ok(w, spieler, new StadtglaubeStimme(false));
            Ok(w, spieler, new WahlStimme("Zöllner", "Bewerber"));
            Abgelehnt(w, spieler, new AmtBewerben("Kämmerer"), "Ratssitzung läuft");
            Abgelehnt(w, reformer, new Schenken("Ulrich Wendelstein", 10), "Ratssitzung läuft");
            Pruefe.Gleich(4, sim.RatssitzungWeiter()!.Ergebnis!.Nein);
            var glaubeVorher = sitzung.AktuellerPunkt!;
            Pruefe.Gleich((bool?)false, glaubeVorher.Stimmen.Single(s => s.Haus == spieler).Ja);
            Pruefe.Gleich(glaubeVorher.Vorschau.Nein, sim.RatssitzungWeiter()!.Ergebnis!.Nein);
            Abgelehnt(w, spieler, new StatutStimme(true), "kein Antrag zum Statut"); // schon abgestimmt
            sim.RatssitzungAbschliessen();
            Pruefe.Gleich<Ratssitzung?>(null, sim.Ratssitzung);
            Pruefe.Gleich(Fronstatut.Bestienfron, w.Statut);
            Pruefe.Gleich(bewerber, w.Rat.AmtNamens("Zöllner")!.Konto);
            var wahl = w.Rat.LetzteRatssitzung!.Abgehalten.Single(p => p.Amt?.Name == "Zöllner");
            Pruefe.Gleich("Bewerber", wahl.Ergebnis!.Gewaehlt);
            Pruefe.Gleich(3, wahl.Ergebnis.Zaehlung.Single(z => z.Haus == bewerber).Stimmen);
        }),

        ("Ratssitzung: Ohne Halt (Tests, Messung) läuft alles wie mit Halt und sofortigem „Überspringen“ – Spielstand zeichengleich", () =>
        {
            var texte = new List<string>();
            foreach (var halt in new[] { false, true })
            {
                var (sim, w, spieler, reformer, bewerber, _) = Aufbau(startwert: 11);
                Bewerben(w, bewerber);
                Ok(w, spieler, new StatutStimme(false));
                if (halt) w.RatssitzungAnhalten = _ => true;
                for (var j = 0; j < 2; j++)
                {
                    BisJahresende(sim);
                    if (!halt || j == 0) Pruefe.Gleich(halt, sim.Ratssitzung is not null, $"Halt im Jahr {j}");
                    sim.RatssitzungAbschliessen();
                }
                texte.Add(Speicherung.Speichern(sim, spieler, new DateTime(2026, 10, 25)));
            }
            Pruefe.Gleich(texte[0], texte[1]);
        }),

        ("Ratssitzung: Ohne Punkte (keine Anträge, keine Wahl) hält die Partie nicht an; während der Sitzung ruht die Zeit und Speichern ist gesperrt", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var gefragt = 0;
            w.RatssitzungAnhalten = _ => { gefragt++; return true; };
            BisJahresende(sim);
            Pruefe.Gleich(0, gefragt);
            Pruefe.Gleich<Ratssitzung?>(null, sim.Ratssitzung);

            var (sim2, w2, spieler, _, _, _) = Aufbau();
            w2.RatssitzungAnhalten = _ => true;
            BisJahresende(sim2);
            var minuten = sim2.Zeit.VergangeneMinuten;
            sim2.Tick();
            Pruefe.Gleich(0, sim2.Aktualisieren(60));
            Pruefe.Gleich(minuten, sim2.Zeit.VergangeneMinuten);
            Pruefe.Wahr(Speicherung.SpeichernVerboten(sim2)!.Contains("Ratssitzung"), "Speichern gesperrt");
            var geworfen = false;
            try { Speicherung.Speichern(sim2, spieler); } catch (InvalidOperationException) { geworfen = true; }
            Pruefe.Wahr(geworfen, "Speichern wirft");
            // Die Oberfläche kann auch nein sagen – dann läuft die Sitzung durch wie ohne Halt.
            var (sim3, w3, _, _, _, _) = Aufbau();
            w3.RatssitzungAnhalten = s => s.Anzahl > 10;
            BisJahresende(sim3);
            Pruefe.Gleich<Ratssitzung?>(null, sim3.Ratssitzung);
            Pruefe.Wahr(w3.Rat.LetzteRatssitzung!.Abgehalten.Count >= 4 && w3.Rat.LetzteRatssitzung.IstBeendet, "durchgelaufen");
            Pruefe.Gleich<string?>(null, Speicherung.SpeichernVerboten(sim3));
        }),

        ("Ratssitzung: Ein Gleichstand bei der Wahl zeigt die Vorschau als Los; im Ergebnis steht der gezogene Kandidat", () =>
        {
            // Zwei Bewerber ohne Geschenke um ein freies Amt ohne Ratsherrn: Jeder Ratsherr sieht beide gleichauf.
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var a = w.KontoEroeffnen("Anna", startvermoegen: 1_000_000);
            var b = w.KontoEroeffnen("Berta", startvermoegen: 1_000_000);
            foreach (var k in new[] { a, b })
            {
                Ok(w, Wohnhaushilfe.MitWohnhaus(w, k), new StandErwerben());
                k.Schaden(k.Vermoegen - 20_000);
                Ok(w, k, new AmtBewerben("Zöllner"));
            }
            var zoellner = w.Rat.AmtNamens("Zöllner")!;
            zoellner.Ratsherr = null;
            var vorschau = w.Rat.Sitzungsvorschau().Single();
            Pruefe.Wahr(vorschau.Stimmen.All(s => s.Los) && vorschau.Vorschau.Stichentscheid, string.Join(" | ", vorschau.Stimmen.Select(s => s.Begruendung)));
            Pruefe.Wahr(vorschau.Stimmen.All(s => s.Begruendung.Contains("Los")), vorschau.Stimmen[0].Begruendung);
            w.RatssitzungAnhalten = _ => true;
            BisJahresende(sim);
            var ergebnis = sim.RatssitzungWeiter()!;
            Pruefe.Wahr(ergebnis.Ergebnis!.Gewaehlt is "Anna" or "Berta", ergebnis.Ergebnis.Gewaehlt ?? "niemand");
            Pruefe.Gleich(ergebnis.Ergebnis.Gewaehlt, zoellner.Inhaber);
            Pruefe.Gleich(ergebnis.Stimmen.Count, ergebnis.Ergebnis.Zaehlung.Sum(z => z.Stimmen));
            foreach (var z in ergebnis.Ergebnis.Zaehlung)
                Pruefe.Gleich(z.Stimmen, ergebnis.Stimmen.Count(s => s.Kandidat == z.Kandidat), z.Kandidat);
            Pruefe.Wahr(ergebnis.Stimmen.All(s => s.Begruendung.Contains("Das Los fiel auf")), ergebnis.Stimmen[0].Begruendung);
        }),

        ("Ratssitzung: Zum Pflaster stimmt der Spieler nur live in der Sitzung – sein Nein kippt den Antrag", () =>
        {
            var katalog = Gebaeudekatalog.AusDatei(Path.Combine(Path.GetDirectoryName(Datenpfad.Wirtschaft())!, "katalog.json"));
            var daten = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
            foreach (var nein in new[] { false, true })
            {
                var (sim, spieler) = new Probestadt().Bauen(katalog, daten).Ausprobieren(daten, "Schmied");
                var w = sim.Wirtschaft!;
                // Der Spieler beantragt (er hat Aktionspunkte), ein zweites Haus ohne KI stimmt – es spielt hier den Spieler am Tisch.
                var nachbar = w.KontoEroeffnen("Nachbar", startvermoegen: 20_000);
                var nr = w.Karte!.Wege.FindIndex(x => x.Name == "Hauptstraße");
                var aemter = w.Rat.Aemter.OrderBy(a => a.Stufe).ToList();
                (aemter[0].Ratsherr, aemter[0].Konto) = (null, spieler);
                (aemter[1].Ratsherr, aemter[1].Konto) = (null, nachbar);
                Ok(w, spieler, new WegPflasterBeantragen(nr, 0, 40));
                Abgelehnt(w, nachbar, new PflasterStimme(false), "erst in der Ratssitzung");
                Abgelehnt(w, spieler, new PflasterStimme(false), "stimmt ohnehin dafür");
                w.RatssitzungAnhalten = _ => true;
                BisJahresende(sim);
                var sitzung = sim.Ratssitzung!;
                var punkt = sitzung.Punkte.Single(p => p.Thema == Sitzungsthema.Pflaster);
                Pruefe.Wahr(punkt.Text.Contains("Hauptstraße") && punkt.Begruendung.Contains("Karren"), $"{punkt.Text} – {punkt.Begruendung}");
                while (sitzung.AktuellerPunkt!.Thema != Sitzungsthema.Pflaster) sim.RatssitzungWeiter();
                var vorher = sitzung.AktuellerPunkt!;
                Pruefe.Wahr(vorher.Stimmen.Single(s => s.Haus == nachbar).Frei, "Nachbar frei");
                Pruefe.Wahr(!vorher.Stimmen.Single(s => s.Haus == spieler).Frei, "Antragsteller nicht frei");
                if (nein) Ok(w, nachbar, new PflasterStimme(false));
                var p2 = sitzung.AktuellerPunkt!;
                var ergebnis = sim.RatssitzungWeiter()!.Ergebnis!;
                Pruefe.Gleich(Zaehlung(p2.Vorschau), Zaehlung(ergebnis with { Protokoll = Array.Empty<string>() }));
                Pruefe.Gleich(nein ? 1 : 0, ergebnis.Nein - vorher.Vorschau.Nein, "das Nein des Spielers zählt");
                sim.RatssitzungAbschliessen();
                Pruefe.Wahr(w.Wegebau.Antrag is null, "erledigt");
            }
        }),
    };
}
