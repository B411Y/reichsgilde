using Reichsgilde.Core;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

// ─────────────────────────────────────────────────────────────────────────────
//  Balancing-Bericht, neue Kennzahlen (PR #208): der passive Spieler und die Partie ohne Schiffshandel (Exportnachfrage), Stadtkasse,
//  Steuer und Spitzel je Haus, Karrenverkäufe der KI und die Rechnung zu Statut C (Z6).
// ─────────────────────────────────────────────────────────────────────────────

static class BalancingberichtNeuTests
{
    static readonly Lazy<Messquellen> Quellen = new(() => new Messquellen());

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Bericht: Passiver Spieler arbeitet mit den Startgesellen; Stadtkasse und Steuer je Jahr gemessen", () =>
        {
            var r = Messpartie.Spielen(new Messaufbau("greybox", 101, 2, Bezug: Bezugsart.Passiv), Quellen.Value);
            Pruefe.Wahr(r.Fehler is null, r.Fehler ?? "");
            Pruefe.Gleich(2, r.Spieler.StartArbeiter);
            Pruefe.Wahr(r.Spieler.Jahre.All(j => j.Betriebe == 1 && j.Gewinn > 0), "die Werkstatt verdient ohne Führung");
            Pruefe.Wahr(r.Haeuser.All(h => h.Jahre[^1].Steuer > 0), "jedes Haus zahlt Umsatzsteuer");
            Pruefe.Gleich(2, r.Kassenstand.Count);
            Pruefe.Gleich(2, r.Steuersatz.Count);
            Pruefe.Wahr(r.Kassenposten.GetValueOrDefault(Reichsgilde.Core.Politik.Kassenposten.Umsatzsteuer) > 0, "Steuer in der Kasse");
            // Die Steuer der Häuser geht in die Kasse (im zweiten Jahr: Vorjahr der Häuser = Kassenbuch des Vorjahres)
            Pruefe.Ungefaehr(r.Haeuser.Sum(h => h.Jahre[^1].Steuer), r.Kassenposten[Reichsgilde.Core.Politik.Kassenposten.Umsatzsteuer]
                - SteuerImErstenJahr(), 1e-6);
        }),

        ("Bericht: Ohne Schiffshandel kauft niemand ein Schiff, die Daten sind sonst gleich", () =>
        {
            var ohne = Wirtschaftsdaten.AusJson(Messpartie.OhneAbschnitt(Quellen.Value.Wirtschaft, "schiffshandel"));
            var mit = Wirtschaftsdaten.AusJson(Quellen.Value.Wirtschaft);
            Pruefe.Wahr(ohne.Schiffshandel is null && mit.Schiffshandel is not null, "Abschnitt entfernt");
            Pruefe.Gleich(mit.Version, ohne.Version);
            Pruefe.Gleich(mit.Waren.Count, ohne.Waren.Count);
            Pruefe.Wahr(Wirtschaftsdaten.AusJson(new Messquellen("schiffshandel").Wirtschaft).Schiffshandel is null, "Diagnose --ohne=schiffshandel");
            var r = Messpartie.Spielen(new Messaufbau("greybox", 101, 6, Bezug: Bezugsart.Passiv, OhneSchiffshandel: true), Quellen.Value);
            Pruefe.Wahr(r.Fehler is null, r.Fehler ?? "");
            Pruefe.Wahr(r.Haeuser.All(h => h.Jahre.All(j => j.Schiffe == 0)) && r.Schiffsjahre == 0 && r.ErstesSchiffJahr is null, "keine Schiffe");
        }),

        ("Bericht: Spitzel je Haus und Jahr – Vielfache der Spitzelkosten, mit den Schiffen der KI", () =>
        {
            var r = Messpartie.Spielen(new Messaufbau("greybox", 101, 10, Bezug: Bezugsart.Passiv), Quellen.Value);
            Pruefe.Wahr(r.Fehler is null, r.Fehler ?? "");
            var kosten = Wirtschaftsdaten.AusJson(Quellen.Value.Wirtschaft).Justiz!.SpitzelKosten;
            var alle = r.Haeuser.SelectMany(h => h.Jahre).ToList();
            Pruefe.Wahr(alle.All(j => Math.Abs(j.Spitzel / kosten - Math.Round(j.Spitzel / kosten)) < 1e-9), "ganze Spitzel");
            Pruefe.Wahr(alle.Any(j => j.Spitzel > 0), "die KI setzt in zehn Jahren Spitzel an");
            Pruefe.Wahr(r.Ki.Sum(h => h.Jahre[^1].Schiffe) > 0 && r.ErstesSchiffJahr is not null && r.Schiffsjahre > 0, "die KI kauft Schiffe");
            Pruefe.Gleich(0, r.Spieler.Jahre[^1].Schiffe, "der passive Spieler kauft keins");
        }),

        ("Bericht: Karrenverkäufe der KI werden aus ihren Meldungen gezählt", () =>
        {
            var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                wirtschaftsdaten: Wirtschaftsdaten.AusJson(Quellen.Value.Wirtschaft));
            var w = sim.Wirtschaft!;
            var e = new Partieergebnis { Aufbau = new Messaufbau("ohne", 1, 2) };
            w.Meldung += m => Messpartie.KarrenZaehlen(e, m);
            var k = w.KontoEroeffnen("Haus Lind", startvermoegen: 50_000);
            var grube = w.BetriebEroeffnen("Grube", Betriebsart.Mine, k); // ohne Personal fährt kein Karren
            w.Ausfuehren(k, new KarrenKaufen(grube.Name, "Ochsenkarren"));
            w.KiAnmelden(k, new KiProfil(Reserve: 1e9));
            for (var j = 0; j < 2; j++)
            {
                var jahr = sim.Zeit.Jahr;
                while (sim.Zeit.Jahr == jahr) sim.Tick();
            }
            sim.Tick();
            Pruefe.Gleich(1, e.KarrenUngenutzt);
            Pruefe.Gleich(0, e.KarrenNotverkauf);
            Messpartie.KarrenZaehlen(e, "Haus Lind: verkauft Handkarren 1 von Grube (40 Gulden) – das Geld ist knapp, der Kämmerer hat gemahnt.");
            Messpartie.KarrenZaehlen(e, "Haus Lind: verkauft Grube, um den Bankrott abzuwenden.");
            Pruefe.Gleich(1, e.KarrenNotverkauf);
        }),

        ("Bericht: Statut C – Ertrag je Kopf wie die Kalkulation, Gleichstand Leibeigener/Geselle", () =>
        {
            var daten = Wirtschaftsdaten.AusJson(Quellen.Value.Wirtschaft);
            var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: daten);
            var w = sim.Wirtschaft!;
            var art = Betriebsart.Giesserei;
            var kopfB = new[] { "Lehrling", "Geselle" }.Max(k => Balancingbericht.KopfErtrag(w, art, k));
            var kopfC = new[] { "Lehrling", "Geselle", "Leibeigener", "Ork" }.Max(k => Balancingbericht.KopfErtrag(w, art, k));
            Pruefe.Ungefaehr(kopfB * daten.Plaetze(art) - daten.Fixkosten(art), Kalkulation.ErwarteterJahresgewinn(w, art, Fronstatut.Bestienfron), 1e-6);
            Pruefe.Ungefaehr(kopfC * daten.Plaetze(art) - daten.Fixkosten(art), Kalkulation.ErwarteterJahresgewinn(w, art, Fronstatut.Leibeigenschaft), 1e-6);
            // (220 − 50 − 6 % × 600) ÷ (1 − 0,85) = 893,3 Gulden Wertschöpfung je Kopf und Jahr
            Pruefe.Ungefaehr(893.333, Balancingbericht.KostenGleichstand(daten), 0.01);
            // Z6 neu: Kosten je Arbeit im Umland – Bergmann 150, Ork (110 + 4 % × 800) ÷ 1,25 = 113,6, Leibeigener (50 + 6 % × 600) ÷ 0,85 = 101,2
            Pruefe.Ungefaehr(150, Balancingbericht.UmlandKostenJeArbeit(daten, "Altknecht"), 1e-9);
            Pruefe.Ungefaehr(113.6, Balancingbericht.UmlandKostenJeArbeit(daten, "Ork"), 1e-9);
            Pruefe.Ungefaehr(86 / 0.85, Balancingbericht.UmlandKostenJeArbeit(daten, "Leibeigener"), 1e-9);
        }),
    };

    /// <summary>Umsatzsteuer, die im ersten Jahr der Partie „greybox“ (Startwert 101, passiv) in die Kasse ging.</summary>
    static double SteuerImErstenJahr()
    {
        var r = Messpartie.Spielen(new Messaufbau("greybox", 101, 1, Bezug: Bezugsart.Passiv), Quellen.Value);
        return r.Kassenposten.GetValueOrDefault(Reichsgilde.Core.Politik.Kassenposten.Umsatzsteuer);
    }
}
