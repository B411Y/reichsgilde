using Reichsgilde;

/// <summary>
/// Bodenmeter langer Wege (Darstellung, 1.11.2026; <c>Passantenspur.BodenschrittLang</c>): Wege über 60 m nehmen Stützpunkte alle 0,25 m statt 0,1 m, damit
/// zur vollen Stunde in Köln kein Bild für die Tabellen hakt (über 150 m alle 0,5 m). Geprüft wird, dass die Figur dabei fast an derselben Stelle ist (gemessen 7,9 bzw. 10,1 cm längs des Weges – unsichtbar, die Bewegung bleibt gleichmäßig).
/// </summary>
static class PassantenspurLangTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Passantenspur: lange Wege mit gröberen Bodenmetern – die Figur ist längs des Weges höchstens 10 cm (0,25 m Schritt) bzw. 15 cm (0,5 m) weiter oder zurück, die Länge stimmt auf 0,2 %", () =>
        {
            Pruefe.Gleich(Passantenspur.Bodenschritt, Passantenspur.BodenschrittFuer(50));
            Pruefe.Gleich(Passantenspur.BodenschrittLang, Passantenspur.BodenschrittFuer(100));
            Pruefe.Gleich(Passantenspur.BodenschrittSehrLang, Passantenspur.BodenschrittFuer(400));
            var linie = new Passantenspur.Polylinie(new (float, float)[] { (0, 0), (0, 40), (-30, 40), (-30, 90), (10, 120) }, new[] { 2.5f, 1.0f, 2.0f, 1.5f });
            foreach (var (schritt, grenze) in new[] { (Passantenspur.BodenschrittLang, 0.10f), (Passantenspur.BodenschrittSehrLang, 0.15f) })
            for (var n = 0; n < 12; n++)
            {
                var fein = Passantenspur.Bodenmeter(linie, n, linie.Laenge, (0, 0));
                var grob = Passantenspur.Bodenmeter(linie, n, linie.Laenge, (0, 0), schritt);
                Pruefe.Wahr(MathF.Abs(fein[^1] - grob[^1]) < fein[^1] * 0.002f, $"Schritt {schritt}, Nummer {n}: Länge {fein[^1]:0.00} / {grob[^1]:0.00} m");
                var groesste = 0f;
                for (var b = 0f; b < fein[^1]; b += 0.37f)
                {
                    var (s1, s2) = (Passantenspur.StreckeZuBoden(fein, linie.Laenge, b), Passantenspur.StreckeZuBoden(grob, linie.Laenge, b * grob[^1] / fein[^1], schritt));
                    var (o1, o2) = (Passantenspur.Gehen(linie, n, linie.Laenge, s1, (0, 0), false).Ort, Passantenspur.Gehen(linie, n, linie.Laenge, s2, (0, 0), false).Ort);
                    groesste = MathF.Max(groesste, MathF.Sqrt((o1.X - o2.X) * (o1.X - o2.X) + (o1.Z - o2.Z) * (o1.Z - o2.Z)));
                }
                Pruefe.Wahr(groesste < grenze, $"Schritt {schritt}, Nummer {n}: bis {groesste:0.000} m anders");
            }
        }),
        ("Passantenspur: Bodenmeter stückweise über mehrere Bilder gefüllt – genau dieselbe Tabelle wie in einem Zug", () =>
        {
            var linie = new Passantenspur.Polylinie(new (float, float)[] { (0, 0), (0, 70), (-50, 70), (-50, 200) }, new[] { 2.5f, 1.0f, 2.0f });
            var schritt = Passantenspur.BodenschrittFuer(linie.Laenge);
            var ganz = Passantenspur.Bodenmeter(linie, 5, linie.Laenge, (0.3f, 0.1f), schritt);
            var stueckweise = new float[Passantenspur.BodenmeterAnzahl(linie.Laenge, schritt) + 1];
            Pruefe.Gleich(ganz.Length, stueckweise.Length);
            for (var von = 1; von < stueckweise.Length; von += 150)
                Passantenspur.BodenmeterFuellen(stueckweise, von, Math.Min(von + 149, stueckweise.Length - 1), linie, 5, linie.Laenge, (0.3f, 0.1f), schritt);
            for (var k = 0; k < ganz.Length; k++) Pruefe.Wahr(ganz[k] == stueckweise[k], $"Eintrag {k}: {ganz[k]} / {stueckweise[k]}");
        }),
    };
}
