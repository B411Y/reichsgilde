using Reichsgilde;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Was man Arbeitern im Innenraum ansieht (Darstellung, Personal und Talentbaum PR #224; Modell in <c>game/scripts/Arbeitertracht.cs</c>, hier mitübersetzt):
/// die Tracht richtet sich nach der Stufe der Laufbahn (<c>Personalwesen.Stufe</c>), nicht nach dem Namen der Arbeitskraft, und Volk, Geschlecht und Haar
/// sind die des Arbeiters im Kern.
/// </summary>
static class ArbeitertrachtTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Arbeitertracht: Stufe 1 bis 3 aus der Laufbahn – Knecht, Taschendieb und Rekrut wie Lehrlinge, Altknecht, Dieb und Gefreiter wie Gesellen, Großknecht, Meisterdieb und Feldwebel wie Vorarbeiter; Orks ohne Stufe", () =>
        {
            var w = new Simulation(Partieeinstellungen.Standard, wirtschaftsdaten: Daten.Value).Wirtschaft!;
            var stufen = new[]
            {
                ("Lehrling", 1), ("Knecht", 1), ("Taschendieb", 1), ("Rekrut", 1),
                ("Geselle", 2), ("Altknecht", 2), ("Dieb", 2), ("Gefreiter", 2),
                ("Vorarbeiter", 3), ("Großknecht", 3), ("Meisterdieb", 3), ("Feldwebel", 3),
                ("Ork", 0), ("Leibeigener", 0),
            };
            foreach (var (kraft, soll) in stufen)
                Pruefe.Gleich(soll, Arbeiterbild.StufeVon(w.Personal.Stufe(kraft), kraft), kraft);
        }),

        ("Arbeitertracht: ohne Abschnitt „personal“ nach dem Namen der alten Laufbahn – Lehrling 1, Geselle 2, sonst 0", () =>
        {
            Pruefe.Gleich(1, Arbeiterbild.StufeVon(0, "Lehrling"));
            Pruefe.Gleich(2, Arbeiterbild.StufeVon(0, "Geselle"));
            Pruefe.Gleich(0, Arbeiterbild.StufeVon(0, "Ork"));
            Pruefe.Gleich(3, Arbeiterbild.StufeVon(3, "Feldwebel")); // die Stufe des Kerns gewinnt
        }),

        ("Arbeitertracht: echte Arbeiter – Volk, Geschlecht und Haar aus dem Kern, Anfänger und Vorarbeiter aus der Stufe", () =>
        {
            var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: Daten.Value);
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: 50_000);
            var g = w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, k, startkarren: "");
            var g2 = w.BetriebEroeffnen("Zweite Gießerei", Betriebsart.Giesserei, k, startkarren: "");
            var h = w.BetriebEroeffnen("Wache", Betriebsart.Wachhaus, k, startkarren: "");
            w.Einstellen(g, "Lehrling", 1);
            w.Einstellen(g, "Geselle", 1);
            w.Einstellen(g2, "Vorarbeiter", 1);
            w.Einstellen(h, "Rekrut", 1);
            w.Einstellen(h, "Feldwebel", 1);
            foreach (var a in g.Arbeiter.Concat(g2.Arbeiter).Concat(h.Arbeiter))
            {
                var bild = Arbeiterbild.Von(a, w.Personal);
                Pruefe.Gleich(a.Volk, bild.Volk, a.Arbeitskraft);
                Pruefe.Gleich(a.Geschlecht == Geschlecht.Frau && a.Volk is not (Volk.Ork or Volk.Oger), bild.Frau, a.Arbeitskraft);
                Pruefe.Gleich(a.Aussehen.Haar, bild.Haar, a.Arbeitskraft);
                Pruefe.Gleich(w.Personal.Stufe(a.Arbeitskraft), bild.Stufe, a.Arbeitskraft);
                Pruefe.Gleich(w.Personal.Stufe(a.Arbeitskraft) == 1, bild.Anfaenger, a.Arbeitskraft);
                Pruefe.Gleich(w.Personal.Stufe(a.Arbeitskraft) == 3, bild.Vorarbeiter, a.Arbeitskraft);
            }
            // dieselbe Auskunft aus der Arbeiterliste, die die Oberfläche und der Innenraum lesen
            foreach (var z in new[] { g, g2, h }.SelectMany(b => w.Werkstaetten.Arbeiterliste(b)))
            {
                var bild = Arbeiterbild.Von(z);
                Pruefe.Gleich(z.Volk, bild.Volk, z.Arbeitskraft);
                Pruefe.Gleich(z.Stufe, bild.Stufe, z.Arbeitskraft);
                Pruefe.Gleich(z.Aussehen.Haar, bild.Haar, z.Arbeitskraft);
            }
            Pruefe.Wahr(w.Werkstaetten.Arbeiterliste(g).Any(z => Arbeiterbild.Von(z).Anfaenger) && w.Werkstaetten.Arbeiterliste(g2).Any(z => Arbeiterbild.Von(z).Vorarbeiter), "Anfänger in der einen, Vorarbeiter in der anderen Gießerei");
        }),

        ("Arbeitertracht: Ersatzfiguren (ein ertappter Dieb, Nachtdiebe) – Mensch, Stufe 2, Geschlecht und Haar aus der Nummer; „Ork“ ist ein Ork", () =>
        {
            var e = Arbeiterbild.Ersatz();
            Pruefe.Gleich(Volk.Mensch, e.Volk);
            Pruefe.Gleich(2, e.Stufe);
            Pruefe.Wahr(e.Frau is null && e.Haar is null && !e.Anfaenger && !e.Vorarbeiter, "weder Geschlecht noch Haar festgelegt");
            Pruefe.Gleich(Volk.Ork, Arbeiterbild.Ersatz("Ork", 0).Volk);
        }),
    };
}
