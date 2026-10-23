using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Speichern;

/// <summary>
/// Kartenwerkstatt, Schritt 5c: Das Hafenbecken – vor der Kaimauer (Rückseite des Hafens) ist das Ufer bis ins Wasser ausgehoben, wie im Modell
/// (die Spundwand steht im Wasser, davor liegen die Kähne). Prüfung, Kern (Boden) und Bild (Becken mit Spiegel) stimmen überein.
/// </summary>
static class HafenbeckenTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenort O(double x, double z) => new(x, z);
    static string Liste(IEnumerable<Befund> befunde) => string.Join("\n  ", befunde.Select(b => b.ToString()));

    /// <summary>Eine leere Karte mit einem Fluss 16 m breit von West nach Ost entlang z = 0 (Wasser von z = −8 bis 8, Ufer 2 m).</summary>
    static Kartenentwurf MitFluss()
    {
        var e = Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);
        e.FlussAnlegen(new[] { O(-150, 0), O(0, 0), O(150, 0) }, "fluss", 16);
        return e;
    }

    /// <summary>
    /// Die Orte, an denen die Kähne des Modells liegen (lokal: Kahn1 x −9 … 1, Kahn2 x 1,5 … 8,5, beide z −7,35 … −10,15), in Stadtkoordinaten, und wie tief
    /// das Wasser dort mindestens sein muss: unter dem Kiel (Mittellinie z −8,75) 0,8 m, an den Bordwänden 0,3 m (der Boden ist nur alle 2 m bekannt, an der
    /// Kaimauer steigt er auf einer Zelle an).
    /// </summary>
    static IEnumerable<(Kartenort Ort, double Tiefe)> Kahnorte(EntwurfGebaeude hafen)
    {
        foreach (var x in new[] { -8, -4, 0.5, 2.5, 5, 7.5 })
            foreach (var (z, tiefe) in new[] { (-7.6, 0.3), (-8.75, 0.8), (-9.9, 0.3) })
                yield return (hafen.Lokal(x, z), tiefe);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Hafenbecken: Vor der Kaimauer ist das Ufer bis ins Wasser ausgehoben – die Kähne liegen auf dem Spiegel, der Hof bleibt stehen (Drehung 0 und 180)", () =>
        {
            // am Nordufer (Drehung 180: der Kai nach Süden) und am Südufer (Drehung 0: der Kai nach Norden), die Rückseite 2,5 m vom Wasser
            foreach (var (z, drehung) in new[] { (-17.5, 180.0), (17.5, 0.0) })
            {
                var e = MitFluss();
                var hafen = e.Setzen(Kartenentwurf.HafenKatalog, 20, z, drehung);
                Pruefe.Wahr(!e.Befunde.Any(b => b.Gebaeude.Contains(hafen.Kennung)), Liste(e.Befunde));
                var form = e.Gelaendeform;
                var becken = form.Wasser.Becken.Single();
                var spiegel = form.Wasser.Laeufe.Single().Punkte[0].Spiegel;
                Pruefe.Ungefaehr(spiegel, becken.Spiegel, 1e-9);
                // vor der Kaimauer: der Boden unter dem Spiegel (auch dort, wo vorher das Ufer stand)
                foreach (var (p, tiefe) in Kahnorte(hafen))
                {
                    Pruefe.Wahr(becken.Enthaelt(p) || form.Wasser.IstWasser(p.X, p.Z), $"Kahn bei {p} weder im Becken noch im Fluss");
                    Pruefe.Wahr(form.Hoehe(p.X, p.Z) < spiegel - tiefe, $"Kahn bei {p}: Boden {form.Hoehe(p.X, p.Z):F2} (Spiegel {spiegel:F2})");
                }
                // die Kaimauer selbst steht im Wasser: Boden an ihrer Mitte unter dem Spiegel
                var kai = hafen.Lokal(0, -7);
                Pruefe.Wahr(form.Hoehe(kai.X, kai.Z) < spiegel, $"Kaimauer bei {kai}: Boden {form.Hoehe(kai.X, kai.Z):F2}, Spiegel {spiegel:F2}");
                // der Hof (Vorderseite) und der Weg davor bleiben, wie sie sind
                foreach (var p in new[] { hafen.Lokal(0, 5), hafen.Lokal(-6, 0), hafen.Lokal(6, -4), hafen.Lokal(0, 9) })
                    Pruefe.Ungefaehr(0, form.Hoehe(p.X, p.Z), 1e-9);
                // das Becken ist kein Gewässer: Prüfung und Wege sehen nur den Fluss
                var mitte = hafen.Lokal(0, -8);
                Pruefe.Wahr(!form.Wasser.IstWasser(mitte.X, mitte.Z) && becken.Enthaelt(mitte), "Becken zählt nicht als Gewässer");
                // dasselbe im Spiel (Stadtkarte aus dem Entwurf)
                var karte = e.ZuStadtkarte();
                var imSpiel = karte.Gelaendeform.Wasser.Becken.Single();
                Pruefe.Wahr(imSpiel.Umriss.Zip(becken.Umriss).All(x => x.First.Abstand(x.Second) < 1e-9), "Becken im Spiel wie im Entwurf");
                Pruefe.Ungefaehr(form.Hoehe(mitte.X, mitte.Z), karte.Gelaendeform.Hoehe(mitte.X, mitte.Z), 1e-9);
            }
        }),

        ("Hafenbecken: folgt dem Hafen beim Versetzen (mit Rückgängig); ohne Wasser vor der Kaimauer gibt es keins", () =>
        {
            var e = MitFluss();
            var hafen = e.Setzen(Kartenentwurf.HafenKatalog, 20, -16, 180); // Rückseite bei z = −9: 1 m vom Wasser
            var vorn = hafen.Lokal(0, -7.5); // zwischen Kaimauer und Wasser, wo sonst das Ufer stünde
            Pruefe.Wahr(e.Gelaendeform.Hoehe(vorn.X, vorn.Z) < e.Gelaendeform.Wasser.Becken.Single().Spiegel, "Becken vor dem Kai");
            e.Verschieben(hafen.Kennung, -40, -16);
            Pruefe.Ungefaehr(-40, e.Gelaendeform.Wasser.Becken.Single().Umriss.Average(p => p.X), 1e-9);
            Pruefe.Wahr(e.Gelaendeform.Hoehe(vorn.X, vorn.Z) > e.Gelaendeform.Wasser.Becken.Single().Spiegel, "am alten Ort steht das Ufer wieder");
            e.Rueckgaengig();
            Pruefe.Ungefaehr(20, e.Gelaendeform.Wasser.Becken.Single().Umriss.Average(p => p.X), 1e-9);
            // weit vom Wasser (die Prüfung meldet „ufer“): kein Becken, der Boden bleibt
            e.Verschieben(hafen.Kennung, 20, -40);
            Pruefe.Wahr(e.Befunde.Any(b => b.Regel == "ufer"), Liste(e.Befunde));
            Pruefe.Gleich(0, e.Gelaendeform.Wasser.Becken.Count);
            var weit = hafen.Lokal(0, -9);
            Pruefe.Ungefaehr(0, e.Gelaendeform.Hoehe(weit.X, weit.Z), 1e-9);
            e.Rueckgaengig();
            Pruefe.Gleich(1, e.Gelaendeform.Wasser.Becken.Count);
        }),

        ("Hafenbecken: Probestadt – der Hafen am Sandfluss steht mit der Kaimauer im Wasser, seine Kähne liegen im Becken oder im Fluss", () =>
        {
            var e = Kartenentwurf.AusJson(File.ReadAllText(Datenpfad.Karte("probestadt")), Katalog.Value);
            var hafen = e.Gebaeude.Single(g => g.Katalog == Kartenentwurf.HafenKatalog);
            var form = e.Gelaendeform;
            var becken = form.Wasser.Becken.Single();
            Pruefe.Gleich("Hafen am Sandfluss", becken.Name);
            foreach (var (p, tiefe) in Kahnorte(hafen))
                Pruefe.Wahr(form.Hoehe(p.X, p.Z) < becken.Spiegel - tiefe, $"Kahn bei {p}: Boden {form.Hoehe(p.X, p.Z):F2}, Spiegel {becken.Spiegel:F2}");
        }),
    };
}
