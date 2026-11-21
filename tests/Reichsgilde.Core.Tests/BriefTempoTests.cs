using Reichsgilde;

/// <summary>Der große Brief faltet sich schneller auf (Meldungen nur als Brief, 21.11.2026): Tempo-Faktor in <c>BriefAblauf</c>, ohne Bewegung sofort, Schließen sofort.</summary>
static class BriefTempoTests
{
    static BriefMasse Masse(double tempo) => new(0.9, 0.9, 0.733, 0.067, 0.1, 0.15, tempo);

    static double BisOffen(BriefAblauf a)
    {
        a.Oeffnen(true);
        var t = 0.0;
        while (a.Zustand != BriefZustand.Offen && t < 5) { a.Weiter(1 / 60.0, true); t += 1 / 60.0; }
        return t;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Brief-Tempo: vierfaches Tempo öffnet in etwa einem Viertel der Zeit, Tempo 1 wie bisher", () =>
        {
            var langsam = BisOffen(new BriefAblauf(Masse(1)));
            var schnell = BisOffen(new BriefAblauf(Masse(4)));
            if (Math.Abs(langsam - 0.9) > 0.05) throw new Exception($"Tempo 1: erwartet 0,9 s, war {langsam}");
            if (schnell > 0.26) throw new Exception($"Tempo 4: erwartet höchstens 0,26 s, war {schnell}");
        }),
        ("Brief-Tempo: ohne Bewegung sofort offen und sofort weg", () =>
        {
            var a = new BriefAblauf(Masse(4));
            a.Oeffnen(false);
            if (a.Zustand != BriefZustand.Offen) throw new Exception("nicht sofort offen");
            a.Schliessen(false);
            if (a.Sichtbar) throw new Exception("nicht sofort weg");
        }),
        ("Brief-Tempo: Schließen ohne Bewegung mitten im Auffalten ist sofort weg", () =>
        {
            var a = new BriefAblauf(Masse(4));
            a.Oeffnen(true);
            a.Weiter(0.05, true);
            a.Schliessen(false);
            if (a.Sichtbar) throw new Exception("noch sichtbar");
        }),
    };
}
