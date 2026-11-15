using Reichsgilde;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Ständeleiter der Amtsstube (<c>game/scripts/Staendeleiter.cs</c>) aus den echten Spieldaten.</summary>
static class StaendeleiterTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Ständeleiter: alle Stände der Daten in ihrer Reihenfolge, vom Gemeinen bis zum höchsten (über dem Patrizier), mit Schwelle", () =>
        {
            var d = Daten.Value;
            var zeilen = Staendeleiter.Zeilen(d.Staende, 0, d.Allgemein.Waehrung);
            Pruefe.Gleich(d.Staende.Count, zeilen.Count);
            for (var i = 0; i < zeilen.Count; i++)
            {
                Pruefe.Wahr(zeilen[i].Text.Contains(d.Staende[i].Name), $"Zeile {i}: {zeilen[i].Text}");
                if (d.Staende[i].Schwelle > 0)
                    Pruefe.Wahr(zeilen[i].Text.Contains(Zahlenformat.Ganz(d.Staende[i].Schwelle)), $"Schwelle in Zeile {i}: {zeilen[i].Text}");
            }
            Pruefe.Wahr(zeilen[0].Text.Contains("Gemeiner"), "unten der Gemeine");
            Pruefe.Wahr(zeilen.Any(z => z.Text.Contains("Patrizier")), "der Patrizier steht darauf");
            Pruefe.Wahr(zeilen[^1].Text.Contains(d.Staende[^1].Name), "oben der höchste Stand der Daten: " + zeilen[^1].Text);
        }),
        ("Ständeleiter: der eigene Stand ist hervorgehoben, genau einer; Gegenprobe: anderer Stand, andere Zeile", () =>
        {
            var d = Daten.Value;
            var a = Staendeleiter.Zeilen(d.Staende, 1, d.Allgemein.Waehrung);
            var b = Staendeleiter.Zeilen(d.Staende, 2, d.Allgemein.Waehrung);
            Pruefe.Gleich(1, a.Count(z => z.Eigener));
            Pruefe.Wahr(a[1].Eigener && a[1].Text.StartsWith("» "), a[1].Text);
            Pruefe.Wahr(!b[1].Eigener && b[2].Eigener, "anderer Stand hebt eine andere Zeile hervor");
            Pruefe.Wahr(Staendeleiter.Zeilen(d.Staende, 99, "G")[^1].Eigener, "über dem obersten Stand: der oberste");
        }),
    };
}
