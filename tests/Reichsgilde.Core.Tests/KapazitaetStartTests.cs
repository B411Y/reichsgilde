using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Tests;

/// <summary>
/// Die Kapazitätsprüfung zählt die Startgebäude zur Pflicht (Nutzer 16.11.2026, „Platzprüfung ja“): je Haus ein Wohnhaus und eine Werkstatt,
/// so groß wie im Startortlos und wie dort nur im Stadtinneren (Kern und Mauerring).
/// </summary>
static class KapazitaetStartTests
{
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Path.GetDirectoryName(Datenpfad.Wirtschaft())!, "katalog.json")));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));

    static Kartenentwurf Oeffnen(string kennung)
    {
        var e = Kartenentwurf.AusJson(File.ReadAllText(Datenpfad.Karte(kennung)), Katalog.Value);
        e.Daten = Daten.Value;
        return e;
    }

    static Kartenort O(double x, double z) => new(x, z);

    /// <summary>Die zwei Berichtszeilen der Startgebäude.</summary>
    static string Startzeilen(Kapazitaet k) => string.Join("\n", k.Bericht.Where(z => z.Contains("am Start")));

    /// <summary>Eine kleine Karte: eine Landstraße, darüber ein Streifen Bauzone der Lage <paramref name="lage"/>.</summary>
    static Kartenentwurf Streifen(Lage lage)
    {
        var e = Kartenentwurf.Neu("neu", "Neu", 300, 300, Katalog.Value);
        e.WegAnlegen(Wegklasse.Land, new[] { O(-100, 0), O(100, 0) }, glatt: false);
        e.BauzoneAnlegen(lage, new[] { O(-40, 4), O(40, 4), O(40, 20), O(-40, 20) });
        return e;
    }

    public static IEnumerable<(string, Action)> Alle()
    {
        yield return ("Kapazität Startgebäude: je Haus eine Werkstatt und ein Wohnhaus, so groß wie im Startortlos", () =>
        {
            Pruefe.Gleich("Werkstatt 8, Wohnhaus 8", string.Join(", ", Kapazitaetspruefung.Start.Select(x => $"{x.Art} {x.Anzahl}")));
            Pruefe.Gleich(Partiebesetzung.HoechstensHoefe, Kapazitaetspruefung.Haeuser);
            var bauen = Daten.Value.Bauen!;
            var r = bauen.Raster;
            var (wb, wt) = Kapazitaetspruefung.Groessen["Werkstatt"];
            var (hb, ht) = Kapazitaetspruefung.Groessen["Wohnhaus"];
            Pruefe.Gleich($"{bauen.Werkstatt.Breite} × {bauen.Werkstatt.Tiefe}", $"{wb * r} × {wt * r}");
            Pruefe.Gleich($"{bauen.Wohnhaus[0].Breite} × {bauen.Wohnhaus[0].Tiefe}", $"{hb * r} × {ht * r}");
        });

        yield return ("Kapazität Startgebäude: nur im Stadtinneren – eine Karte nur mit Vorstadt meldet sie, die übrige Pflicht sucht unabhängig davon", () =>
        {
            var vorstadt = Kapazitaetspruefung.Pruefen(Streifen(Lage.Vorstadt), Schwere.Hinweis);
            Pruefe.Gleich("Pflicht Werkstatt am Start (Kern und Mauerring): 0 von 8 passen\nPflicht Wohnhaus am Start (Kern und Mauerring): 0 von 8 passen", Startzeilen(vorstadt));
            // Die Werkstätten der übrigen Pflicht dürfen in die Vorstadt – ihr Suchstand hängt nicht an dem der Startwerkstätten
            Pruefe.Wahr(vorstadt.Bericht.Contains("Pflicht Werkstatt: 5 von 16 passen"), string.Join("\n", vorstadt.Bericht));
            Pruefe.Wahr(vorstadt.Befunde.Count(b => b.Text.Contains("am Start") && b.Schwere == Schwere.Hinweis && b.Regel == "kapazitaet") == 2,
                string.Join("\n", vorstadt.Befunde.Select(b => b.ToString())));
            // Derselbe Streifen als Kern: Die Startwerkstätten kommen zuerst und nehmen den Platz
            var kern = Kapazitaetspruefung.Pruefen(Streifen(Lage.Kern), Schwere.Hinweis);
            Pruefe.Wahr(Startzeilen(kern).StartsWith("Pflicht Werkstatt am Start (Kern und Mauerring): 5 von 8 passen"), Startzeilen(kern));
            Pruefe.Wahr(kern.Bericht.Contains("Pflicht Werkstatt: 0 von 16 passen"), string.Join("\n", kern.Bericht));
        });

        yield return ("Kapazität Startgebäude: die vier Karten (Köln, Probestadt, Messstadt passen; die Greybox hat Platz für 5 Startwerkstätten wie das Startortlos)", () =>
        {
            foreach (var (karte, werkstaetten) in new[] { ("koeln", 8), ("probestadt", 8), ("messstadt", 8), ("greybox", 5) })
            {
                var e = Oeffnen(karte);
                var k = Kapazitaetspruefung.Pruefen(e);
                Pruefe.Gleich($"{karte}\nPflicht Werkstatt am Start (Kern und Mauerring): {werkstaetten} von 8 passen\nPflicht Wohnhaus am Start (Kern und Mauerring): 8 von 8 passen",
                    karte + "\n" + Startzeilen(k));
                // Die Startgebäude werden zuerst gesetzt und liegen ganz im Stadtinneren
                var r = k.Raster;
                foreach (var p in k.Plaetze.Where(p => p.Art != "fest").Take(werkstaetten + 8))
                    foreach (var (i, j) in p.Felder)
                    {
                        var mitte = O((i + 0.5) * r, (j + 0.5) * r);
                        var zone = e.Bauzonen.FirstOrDefault(z => Kartengeometrie.ImVieleck(mitte, z.Ecken));
                        Pruefe.Wahr(zone is { Art: Lage.Kern or Lage.Mauerring }, $"{karte}: {p.Art} auf ({i}, {j}) liegt in {zone?.Art.ToString() ?? "keiner Zone"}");
                    }
            }
        });
    }
}
