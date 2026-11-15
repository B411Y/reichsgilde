using Reichsgilde;
using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests: die Lagen-Texte der Oberfläche (<c>LagenTexte</c>): Tooltip je Ware, Übersicht für Markt und Jahresbericht (nur geltende Lagen); gewöhnliche Lagen und Ernte kommen nicht vor.</summary>
static class LagenTexteTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(File.ReadAllText(Datenpfad.Wirtschaft())));

    static long Minute(int jahr, int stunde) => jahr * (long)Spielzeit.MinutenProTag + stunde * 60 - Spielzeit.TagesbeginnMinute;

    static Simulation Sim(long minute, int startwert) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, new Spielzeit(minute), Daten.Value);

    static int StartwertMit(Betriebsart quelle, Lagegute guete)
    {
        for (var s = 1; s < 3000; s++)
        {
            var w = new Stadtwirtschaft(Daten.Value, Partieeinstellungen.Standard.StartStatut, s);
            var q = w.Lagen.QuelleVon(quelle)!;
            if (q.LageVon(q.ErstesStichjahr)!.Guete == guete) return s;
        }
        throw new InvalidOperationException("Kein Startwert gefunden.");
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("LagenTexte: Im Jahr davor nichts – weder Übersicht noch Tooltip zeigen die kommende Lage (Gegenprobe: im Stichjahr schon)", () =>
        {
            var sim = Sim(Minute(3, 4), StartwertMit(Betriebsart.Mine, Lagegute.Schlecht));
            sim.Tick();
            var w = sim.Wirtschaft!;
            Pruefe.Wahr(!LagenTexte.Uebersicht(w).Any(z => z.Contains("Wassereinbruch") || z.Contains("angesagt")), string.Join(" | ", LagenTexte.Uebersicht(w)));
            Pruefe.Gleich(0, LagenTexte.Tipp(w, "Eisen").Count);
            Pruefe.Gleich(0, LagenTexte.Tipp(w, "Gerste").Count);
        }),

        ("LagenTexte: Gilt die Lage, steht Faktor in der Tooltip-Zeile „Quelle Jahr: Ereignis – Ware ×Faktor“", () =>
        {
            var sim = Sim(Minute(4, 10), StartwertMit(Betriebsart.Mine, Lagegute.Schlecht));
            sim.Tick();
            var w = sim.Wirtschaft!;
            var tipp = LagenTexte.Tipp(w, "Eisen");
            Pruefe.Wahr(tipp.Any(z => z.Contains("1404: Wassereinbruch – Eisen ×0,8")), string.Join(" | ", tipp));
            Pruefe.Wahr(LagenTexte.Uebersicht(w).Any(z => z.Contains("1404: Wassereinbruch") && z.EndsWith("bis 1407")), string.Join(" | ", LagenTexte.Uebersicht(w)));
        }),

        ("LagenTexte: Gewöhnliche Lage – keine Zeile (Ernte zählt hier nicht)", () =>
        {
            var sim = Sim(Minute(4, 10), StartwertMit(Betriebsart.Mine, Lagegute.Gewoehnlich));
            sim.Tick();
            var w = sim.Wirtschaft!;
            Pruefe.Wahr(!LagenTexte.Aktuelle(w).Any(l => l.Quelle.Betrieb == Betriebsart.Mine), "Mine");
            Pruefe.Wahr(!LagenTexte.Tipp(w, "Eisen").Any(z => z.Contains("Eisen ×")), "Tipp");
        }),
    };
}
