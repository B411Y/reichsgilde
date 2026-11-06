using Reichsgilde;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Streife lebendiger (Wachen und Diebe, Paket P5): die Wahl der Runde (<see cref="Streifengang"/>, reine Rechnung) und das Viertel eines Punktes der Karte.</summary>
static class StreifengangTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Streifengang: ohne Verlegung reihum Markt, Viertel, Mauer; ohne Mauerweg an Stelle der Mauer das Viertel", () =>
        {
            var arten = new HashSet<Streifenart>();
            for (var nummer = 0; nummer < 3; nummer++) arten.Add(Streifengang.Waehlen(7, nummer, false, true));
            Pruefe.Gleich(3, arten.Count); // je Schicht gehen drei Wachleute drei verschiedene Runden
            for (var nummer = 0; nummer < 30; nummer++)
                Pruefe.Wahr(Streifengang.Waehlen(3, nummer, false, false) != Streifenart.Mauer, "ohne Mauerweg keine Mauerrunde");
        }),

        ("Streifengang: fest aus Wachwechsel und Nummer, ohne Zufall; die Runde wechselt mit der Schicht", () =>
        {
            for (var wechsel = 2; wechsel < 12; wechsel++)
                for (var nummer = 100; nummer < 110; nummer++)
                    Pruefe.Gleich(Streifengang.Waehlen(wechsel, nummer, false, true), Streifengang.Waehlen(wechsel, nummer, false, true));
            var verschieden = Enumerable.Range(2, 12).Select(w => Streifengang.Waehlen(w, 101, false, true)).Distinct().Count();
            Pruefe.Gleich(3, verschieden);
        }),

        ("Streifengang: verlegt der Stadthauptmann die Streife, gehen drei von vier ins Viertel, keiner an die Mauer", () =>
        {
            var viertel = 0;
            var markt = 0;
            for (var nummer = 0; nummer < 400; nummer++)
            {
                var art = Streifengang.Waehlen(5, nummer, true, true);
                Pruefe.Wahr(art != Streifenart.Mauer, "verlegt: keine Mauerrunde");
                if (art == Streifenart.Viertel) viertel++; else markt++;
            }
            Pruefe.Gleich(300, viertel);
            Pruefe.Gleich(100, markt);
        }),

        ("Viertel: ViertelBei stimmt mit ViertelVon überein; ohne Karte keines", () =>
        {
            var sim = StadtkasseTests.NeueSimulation();
            var w = sim.Wirtschaft!;
            Pruefe.Gleich<Stadtviertel?>(null, w.Befugnisse.ViertelBei(10, 10));
            var karte = Stadtkarte.AusDatei(Datenpfad.Karte("probestadt"));
            w.KarteSetzen(karte);
            var haus = StadtkasseTests.Haus(w, "Haus");
            var gesehen = new HashSet<Stadtviertel>();
            foreach (var g in karte.Gebaeude.Where(g => g.Betrieb is not null))
            {
                var b = w.BetriebEroeffnen(g.Betrieb!, Betriebsart.Giesserei, haus);
                var bei = w.Befugnisse.ViertelBei(g.Punkt[0], g.Punkt[1]);
                Pruefe.Gleich<Stadtviertel?>(w.Befugnisse.ViertelVon(b), bei);
                gesehen.Add(bei!.Value);
            }
            Pruefe.Wahr(gesehen.Count >= 2, $"Viertel: {string.Join(", ", gesehen)}");
        }),
    };
}
