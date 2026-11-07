using Reichsgilde;
using Reichsgilde.Core;
using Reichsgilde.Core.Entwickler;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Taschendiebe und Festnahme in der Darstellung (Wachen und Diebe, Paket P3): der Ablauf als reine Rechnung (<see cref="Taschendiebgang"/>, ohne Godot)
/// und der Entwicklerbefehl für die Testhilfe <c>--diebtest</c>.
/// </summary>
static class TaschendiebgangTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Taschendiebgang: Abschnitte eines Griffs folgen der Zeit, eigene Diebe kommen früher als fremde", () =>
        {
            const double griff = 600;
            foreach (var eigen in new[] { true, false })
            {
                Pruefe.Gleich(Taschendiebgang.Abschnitt.Vorher, Taschendiebgang.AbschnittBei(griff - Taschendiebgang.Vorlauf(eigen) - 0.1, griff, eigen, out _), "vorher");
                Pruefe.Gleich(Taschendiebgang.Abschnitt.Anschleichen, Taschendiebgang.AbschnittBei(griff - 1, griff, eigen, out var a), "anschleichen");
                Pruefe.Wahr(a is > 0.8 and < 1, "Anteil kurz vor dem Griff");
                Pruefe.Gleich(Taschendiebgang.Abschnitt.Griff, Taschendiebgang.AbschnittBei(griff + 0.5, griff, eigen, out _), "griff");
                Pruefe.Gleich(Taschendiebgang.Abschnitt.Abgang, Taschendiebgang.AbschnittBei(griff + Taschendiebgang.GriffDauer + 1, griff, eigen, out _), "abgang");
                Pruefe.Gleich(Taschendiebgang.Abschnitt.Nachher, Taschendiebgang.AbschnittBei(griff + Taschendiebgang.GriffDauer + Taschendiebgang.Abgang(eigen) + 0.1, griff, eigen, out _), "nachher");
            }
            Pruefe.Wahr(Taschendiebgang.Vorlauf(true) > Taschendiebgang.Vorlauf(false), "Eigene früher");
            Pruefe.Gleich(0.0, Taschendiebgang.Neigung(0));
            Pruefe.Gleich(1.0, Taschendiebgang.Neigung(0.5));
            Pruefe.Gleich(Taschendiebgang.Streu(3, 77, 1), Taschendiebgang.Streu(3, 77, 1), "Hash fest");
            Pruefe.Wahr(Taschendiebgang.Streu(3, 77, 1) != Taschendiebgang.Streu(4, 77, 1), "Hash streut");
        }),

        ("Taschendiebgang: Festnahme läuft, packt zu, führt ab und ist dann fertig", () =>
        {
            Pruefe.Gleich(Taschendiebgang.Fest.Lauf, Taschendiebgang.FestnahmeBei(0, 5, 40, out _));
            Pruefe.Gleich(Taschendiebgang.Fest.Zugriff, Taschendiebgang.FestnahmeBei(5.5, 5, 40, out _));
            Pruefe.Gleich(Taschendiebgang.Fest.Abfuehren, Taschendiebgang.FestnahmeBei(5 + Taschendiebgang.ZugriffDauer + 20, 5, 40, out var a));
            Pruefe.Wahr(a is > 0.4 and < 0.6, "halber Weg");
            Pruefe.Gleich(Taschendiebgang.Fest.Fertig, Taschendiebgang.FestnahmeBei(1000, 5, 40, out _));
        }),

        ("Taschendiebgang: Ein Torwächter fasst nur nahe am Tor und wenn ein Ersatzmann bleibt; danach geht er zurück auf den Posten", () =>
        {
            Pruefe.Wahr(Taschendiebgang.TorwaechterFasst(40, 2), "nahes Tor, zwei am Posten");
            Pruefe.Wahr(Taschendiebgang.TorwaechterFasst(Taschendiebgang.TorwaechterNaehe, 3), "genau an der Grenze");
            Pruefe.Wahr(!Taschendiebgang.TorwaechterFasst(Taschendiebgang.TorwaechterNaehe + 1, 2), "zu weit");
            Pruefe.Wahr(!Taschendiebgang.TorwaechterFasst(40, 1), "keiner bliebe als Ersatzmann");
            Pruefe.Wahr(!Taschendiebgang.TorwaechterFasst(40, 0), "keiner am Posten");
            // mit Rückkehr: Lauf, Zugriff, Abführen, dann zurück, dann fertig; ohne Rückkehr (Gardist) unverändert
            Pruefe.Gleich(Taschendiebgang.Fest.Rueckkehr, Taschendiebgang.FestnahmeBei(5 + Taschendiebgang.ZugriffDauer + 40 + 3, 5, 40, out var a, 10));
            Pruefe.Wahr(a is > 0.25 and < 0.35, "dreißig Prozent der Rückkehr");
            Pruefe.Gleich(Taschendiebgang.Fest.Fertig, Taschendiebgang.FestnahmeBei(5 + Taschendiebgang.ZugriffDauer + 40 + 10.1, 5, 40, out _, 10));
            Pruefe.Gleich(Taschendiebgang.Fest.Fertig, Taschendiebgang.FestnahmeBei(5 + Taschendiebgang.ZugriffDauer + 40.1, 5, 40, out _), "Gardist: nach dem Abführen fertig");
        }),

        ("Entwicklerbefehl Diebeshaus: Versteck mit Dieben, die laut Kern am Markt greifen; ohne Sperre abgelehnt", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos }, Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            for (var i = 0; i < 300; i++) sim.Tick(); // 09:00
            Pruefe.Wahr(!sim.Bote.SofortAusfuehren(spieler, new EntwicklerDiebeshaus()).Erfolg, "Ohne Entwickler-Sperre erlaubt");
            sim.EntwicklerErlaubt = true;
            var e = sim.Bote.SofortAusfuehren(spieler, new EntwicklerDiebeshaus());
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            var versteck = w.Betriebe.First(b => b.Konto == spieler && w.Diebe.IstDiebesbetrieb(b));
            Pruefe.Gleich(versteck.Plaetze, versteck.Belegt, "voll besetzt");
            Pruefe.Wahr(sim.Bote.SofortAusfuehren(spieler, new EntwicklerDiebeshaus()).Erfolg, "zweimal erlaubt");
            Pruefe.Gleich(1, w.Betriebe.Count(b => b.Konto == spieler && w.Diebe.IstDiebesbetrieb(b)), "kein zweites Versteck");
            Pruefe.Wahr(w.Diebe.AnwesenheitJetzt(versteck).Any(s => s.Ort == Diebesort.Markt), "Niemand am Markt");
            Pruefe.Wahr(w.Diebe.GriffeDieserStunde(versteck).Count >= 2, "Keine Griffe");
        }),

        ("Entwicklerbefehl Diebeshaus: ist der Name des Verstecks schon vergeben, lehnt der Kern ab statt zu werfen", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos }, Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            sim.EntwicklerErlaubt = true;
            var fremd = w.KontoEroeffnen("Haus Gast", startvermoegen: 1000);
            w.BetriebEroeffnen($"Unterschlupf {fremd.Inhaber}", Betriebsart.Unterschlupf, spieler, startkarren: ""); // der Name gehört schon einem anderen
            var e = sim.Bote.SofortAusfuehren(spieler, new EntwicklerDiebeshaus(fremd.Inhaber));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("gibt es schon", StringComparison.Ordinal), "Nicht abgelehnt: " + e.Meldung);
        }),

        ("Taschendiebgang: Jeder Griff des Kerns wird gezeigt, keiner springt ins Bild – auch über den Stundenwechsel (Kernformel, ein Tag)", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos }, Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            for (var i = 0; i < 300; i++) sim.Tick(); // 09:00
            sim.EntwicklerErlaubt = true;
            Pruefe.Wahr(sim.Bote.SofortAusfuehren(spieler, new EntwicklerDiebeshaus()).Erfolg, "Diebeshaus");
            var versteck = w.Betriebe.First(b => b.Konto == spieler && w.Diebe.IstDiebesbetrieb(b));
            var gesehen = new HashSet<(int, long)>();
            var gezeigt = new HashSet<(int, long)>();
            var erste = new Dictionary<(int, long), double>();
            for (var t = 0; t < 600; t++)
            {
                sim.Tick();
                var jetzt = (double)sim.Zeit.VergangeneMinuten;
                foreach (var g in w.Diebe.GriffeDieserStunde(versteck))
                {
                    var minute = sim.Zeit.VergangeneMinuten + (g.Uhrzeit - sim.Zeit.UhrzeitInMinuten);
                    var schluessel = (g.Dieb.Nummer, minute);
                    // gemeldet gilt ein Griff erst, wenn der Dieb in dem Moment noch im Haus ist, in dem sein Vorlauf beginnt (wer zum Stundenende gefasst wird, ist ein Fall der Festnahme)
                    if (Taschendiebgang.Zeigen(jetzt, minute, true)) { gesehen.Add(schluessel); erste.TryAdd(schluessel, jetzt); }
                    if (gezeigt.Contains(schluessel) || !Taschendiebgang.Zeigen(jetzt, minute, true)) continue;
                    var vorlauf = Taschendiebgang.StartVorlauf(jetzt, minute, true, out var nah);
                    // später als der volle Vorlauf bekannt: Der Dieb beginnt dicht beim Opfer, nicht am Rand des Marktes
                    if (vorlauf < Taschendiebgang.VorlaufEigen - 0.5) Pruefe.Wahr(nah < Taschendiebgang.EntfernungEigenMin, $"Griff {minute}: Dieb beginnt {nah} m entfernt, springt ins Bild");
                    Taschendiebgang.AbschnittBei(jetzt, minute, true, vorlauf, out var anteil);
                    Pruefe.Wahr(anteil <= 1, "Anteil");
                    gezeigt.Add(schluessel);
                }
            }
            Pruefe.Wahr(gesehen.Count >= 20, $"Zu wenige Griffe im Test: {gesehen.Count}");
            foreach (var g in gesehen) Pruefe.Wahr(gezeigt.Contains(g), $"Griff {g} des Kerns wurde nicht gezeigt (zuerst gesehen bei {erste[g]})");
            // Der alte Fehler: zwei Griffe desselben Diebes in kurzem Abstand (Szene je Dieb ließ den zweiten fallen) – so etwas gibt es und es wird gezeigt
            var dicht = gezeigt.GroupBy(g => g.Item1).Sum(grp => grp.Select(x => x.Item2).OrderBy(x => x).Zip(grp.Select(x => x.Item2).OrderBy(x => x).Skip(1), (a, b) => b - a).Count(d => d < Taschendiebgang.VorlaufEigen));
            Pruefe.Wahr(dicht > 0, "Kein Paar dicht beieinander liegender Griffe im Testtag");
        }),
    };
}
