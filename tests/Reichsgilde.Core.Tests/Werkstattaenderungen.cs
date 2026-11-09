using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

/// <summary>
/// Zufällige Änderungen in der Kartenwerkstatt für die Tests „in Häppchen und mit Vorrat gleich der vollen Prüfung“ (KapazitaetInHaeppchenTests,
/// PruefungInkrementellTests): Wege anlegen, verschieben, löschen, umklassieren, Stützpunkte, Gebäude, Bauzonen, Gelände- und Bodenstriche,
/// Rückgängig und Wiederholen – jeweils in der Nähe von etwas, das schon auf der Karte steht. Der Zufall ist der des Tests (fester Startwert).
/// </summary>
static class Werkstattaenderungen
{
    /// <summary>Alle Arten von Änderungen.</summary>
    public static readonly string[] Arten =
    {
        "weg-neu", "weg-verschieben", "weg-loeschen", "weg-klasse", "stuetzpunkt", "gebaeude", "zone-neu", "zone-verschieben", "zone-loeschen",
        "gelaende", "boden", "rueckgaengig", "wiederholen",
    };

    static Kartenort O(double x, double z) => new(x, z);

    /// <summary>Ein Ort in der Nähe von etwas, das auf der Karte steht (Gebäude, Wegpunkt oder Ecke einer Bauzone), um bis zu ±<paramref name="streuung"/> m versetzt.</summary>
    static Kartenort Irgendwo(Kartenentwurf e, Random r, double streuung)
    {
        var orte = e.Gebaeude.Select(g => g.Punkt).Concat(e.Wege.SelectMany(w => w.Punkte.Take(1))).Concat(e.Bauzonen.SelectMany(z => z.Ecken.Take(1))).ToList();
        var o = orte.Count == 0 ? O(0, 0) : orte[r.Next(orte.Count)];
        return O(o.X + (r.NextDouble() * 2 - 1) * streuung, o.Z + (r.NextDouble() * 2 - 1) * streuung);
    }

    /// <summary>Führt eine zufällige Änderung aus (aus <paramref name="arten"/>, sonst aus allen) und sagt, welche. Geht sie nicht (z. B. nichts zum Löschen), „—“.</summary>
    public static string Zufaellig(Kartenentwurf e, Random r, IReadOnlyList<string>? arten = null)
    {
        var art = (arten ?? Arten)[r.Next((arten ?? Arten).Count)];
        try
        {
            switch (art)
            {
                case "weg-neu":
                {
                    var p = Irgendwo(e, r, 40);
                    var klassen = new[] { Wegklasse.Gasse, Wegklasse.Land, Wegklasse.Haupt, Wegklasse.Fussweg, Wegklasse.Feldweg };
                    var punkte = Enumerable.Range(0, 2 + r.Next(3)).Select(i => O(p.X + i * (10 + r.NextDouble() * 15), p.Z + (r.NextDouble() * 2 - 1) * 12)).ToList();
                    var w = e.WegAnlegen(klassen[r.Next(klassen.Length)], punkte, glatt: r.Next(2) == 0);
                    return $"{art} {w.Name}";
                }
                case "weg-verschieben" when e.Wege.Count > 0:
                {
                    var w = e.Wege[r.Next(e.Wege.Count)];
                    e.WegVerschieben(w, Math.Round((r.NextDouble() * 2 - 1) * 4, 1), Math.Round((r.NextDouble() * 2 - 1) * 4, 1));
                    return $"{art} {w.Name}";
                }
                case "weg-loeschen" when e.Wege.Count > 0:
                {
                    var w = e.Wege[r.Next(e.Wege.Count)];
                    e.WegLoeschen(w);
                    return $"{art} {w.Name}";
                }
                case "weg-klasse" when e.Wege.Count > 0:
                {
                    var w = e.Wege[r.Next(e.Wege.Count)];
                    e.WegKlasseSetzen(w, w.IstKarrenstrasse ? Wegklasse.Gasse : Wegklasse.Land);
                    return $"{art} {w.Name}";
                }
                case "stuetzpunkt" when e.Wege.Count > 0:
                {
                    var w = e.Wege[r.Next(e.Wege.Count)];
                    var i = r.Next(w.Punkte.Count);
                    e.StuetzpunktVerschieben(w, i, w.Punkte[i].X + (r.NextDouble() * 2 - 1) * 6, w.Punkte[i].Z + (r.NextDouble() * 2 - 1) * 6);
                    return $"{art} {w.Name} {i}";
                }
                case "gebaeude" when e.Gebaeude.Count > 0:
                {
                    var g = e.Gebaeude[r.Next(e.Gebaeude.Count)];
                    e.Verschieben(g.Kennung, g.X + (r.NextDouble() * 2 - 1) * 5, g.Z + (r.NextDouble() * 2 - 1) * 5);
                    return $"{art} {g.Kennung}";
                }
                case "zone-neu":
                {
                    var p = Irgendwo(e, r, 30);
                    var (x, z) = (Math.Round(p.X / e.Raster) * e.Raster, Math.Round(p.Z / e.Raster) * e.Raster);
                    var (b, t) = ((4 + r.Next(10)) * e.Raster, (4 + r.Next(10)) * e.Raster);
                    var lagen = new[] { Lage.Kern, Lage.Mauerring, Lage.Vorstadt, Lage.Umland };
                    var zone = e.BauzoneAnlegen(lagen[r.Next(lagen.Length)], new List<Kartenort> { O(x, z), O(x + b, z), O(x + b, z + t), O(x, z + t) });
                    return $"{art} {zone.Kennung}";
                }
                case "zone-verschieben" when e.Bauzonen.Count > 0:
                {
                    var zone = e.Bauzonen[r.Next(e.Bauzonen.Count)];
                    e.FlaecheVerschieben(zone, (r.Next(5) - 2) * e.Raster, (r.Next(5) - 2) * e.Raster);
                    return $"{art} {zone.Kennung}";
                }
                case "zone-loeschen" when e.Bauzonen.Count > 0:
                {
                    var zone = e.Bauzonen[r.Next(e.Bauzonen.Count)];
                    e.FlaecheLoeschen(zone);
                    return $"{art} {zone.Kennung}";
                }
                case "gelaende":
                {
                    var p = Irgendwo(e, r, 20);
                    e.GelaendeStrichBeginnen(new Pinsel(r.Next(2) == 0 ? Pinselart.Heben : Pinselart.Senken, Radius: 6 + r.Next(10), Staerke: 0.4, Kante: 0.5));
                    for (var i = 0; i < 6; i++) e.GelaendeStrichWeiter(O(p.X + i * 3, p.Z + (r.NextDouble() * 2 - 1) * 3));
                    e.GelaendeStrichBeenden();
                    return $"{art} bei {p}";
                }
                case "boden":
                {
                    var p = Irgendwo(e, r, 20);
                    e.BodenStrichBeginnen(new Bodenpinsel(Bodentextur.Erde, 6, Staerke: 1, Kante: 0.5, Deko: true, Dichte: 1));
                    for (var i = 0; i < 6; i++) e.BodenStrichWeiter(O(p.X + i * 3, p.Z));
                    e.BodenStrichBeenden();
                    return $"{art} bei {p}";
                }
                case "rueckgaengig":
                    return e.Rueckgaengig() ? art : "—";
                case "wiederholen":
                    return e.Wiederholen() ? art : "—";
            }
        }
        catch (ArgumentException)
        {
            // z. B. ein Weg, dessen Enden am selben Punkt einrasten – die Werkstatt meldet das und ändert nichts
        }
        return "—";
    }
}
