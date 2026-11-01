using Reichsgilde;

/// <summary>
/// Die Spuren der Passanten (Darstellung, 1.11.2026; Modell in <c>game/scripts/Passantenspur.cs</c>, hier mitübersetzt): jede Person hat ihre eigene Spur auf
/// der rechten Gassenhälfte (Rechtsverkehr), schwankt sanft darum (±0,15 m, Wellenlänge 3 bis 5 m) und gleicht sich an Anfang und Ende über einen Meter an.
/// </summary>
static class PassantenspurTests
{
    /// <summary>Alle Nummern, die die Tests durchgehen (die Einwohner einer großen Stadt).</summary>
    static IEnumerable<int> Nummern() => Enumerable.Range(0, 400);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Passantenspur: die Grundspur liegt auf der rechten Gassenhälfte, ist je Person fest und es gibt viele verschiedene (nicht fünf)", () =>
        {
            foreach (var n in Nummern())
            {
                var spur = Passantenspur.Grundspur(n);
                Pruefe.Wahr(spur >= Passantenspur.Spurnah - 1e-6f && spur <= Passantenspur.Spurfern + 1e-6f, $"Nummer {n}: Spur {spur} außerhalb");
                Pruefe.Wahr(spur > 0 && spur < Passantenspur.Gassenbreite / 2, $"Nummer {n}: Spur {spur} nicht auf der rechten Hälfte");
                Pruefe.Gleich(spur, Passantenspur.Grundspur(n), "kein Zufall: dieselbe Nummer, dieselbe Spur");
            }
            var verschiedene = Nummern().Select(n => MathF.Round(Passantenspur.Grundspur(n), 2)).Distinct().Count();
            Pruefe.Wahr(verschiedene >= 25, $"nur {verschiedene} verschiedene Spuren auf zwei Stellen");
            // gleichmäßig verteilt: in jedem Drittel der Breite liegen mindestens ein Viertel der Personen
            var drittel = (Passantenspur.Spurfern - Passantenspur.Spurnah) / 3;
            for (var i = 0; i < 3; i++)
            {
                var von = Passantenspur.Spurnah + i * drittel;
                var anzahl = Nummern().Count(n => Passantenspur.Grundspur(n) >= von && Passantenspur.Grundspur(n) < von + drittel + (i == 2 ? 1 : 0));
                Pruefe.Wahr(anzahl > Nummern().Count() / 4, $"im {i + 1}. Drittel nur {anzahl} Personen");
            }
            // aufeinanderfolgende Nummern liegen nicht nebeneinander (Nachbarn im Haus gehen nicht im Gleichschritt)
            var gleichauf = Nummern().Zip(Nummern().Skip(1), (a, b) => MathF.Abs(Passantenspur.Grundspur(a) - Passantenspur.Grundspur(b))).Count(d => d < 0.01f);
            Pruefe.Wahr(gleichauf < 10, $"{gleichauf} Nachbarn mit fast derselben Spur");
        }),

        ("Passantenspur: das Schwanken bleibt bei ±0,15 m, die Wellen sind 3 bis 5 m lang, die Phase kommt aus der Nummer", () =>
        {
            foreach (var n in Nummern())
            {
                var (kurz, lang) = Passantenspur.Wellenlaengen(n);
                Pruefe.Wahr(kurz >= 3f - 1e-5f && kurz <= 5f && lang >= 3f && lang <= 5f + 1e-5f, $"Nummer {n}: Wellenlängen {kurz}/{lang}");
                var groesste = 0f;
                for (var strecke = 0f; strecke <= 60f; strecke += 0.05f)
                    groesste = MathF.Max(groesste, MathF.Abs(Passantenspur.Schwankung(n, strecke)));
                Pruefe.Wahr(groesste <= Passantenspur.Schwanken + 1e-5f, $"Nummer {n}: schwankt bis {groesste} m");
                Pruefe.Wahr(groesste >= 0.05f, $"Nummer {n}: schwankt kaum ({groesste} m)");
            }
            // verschiedene Personen schwanken zugleich verschieden (nicht im Gleichschritt)
            var bei = Nummern().Select(n => MathF.Round(Passantenspur.Schwankung(n, 2f), 2)).Distinct().Count();
            Pruefe.Wahr(bei >= 20, $"auf 2 m nur {bei} verschiedene Auslenkungen");
            Pruefe.Gleich(Passantenspur.Schwankung(7, 3.3f), Passantenspur.Schwankung(7, 3.3f), "reproduzierbar");
        }),

        ("Passantenspur: unterwegs bleibt die ganze Figur (Schultern ±0,25 m) auf der rechten Hälfte, nie über die Gassenbreite von 2 m hinaus", () =>
        {
            foreach (var n in Nummern())
                for (var strecke = 1f; strecke <= 59f; strecke += 0.05f)
                {
                    var seite = Passantenspur.Seite(n, strecke, 60f - strecke);
                    Pruefe.Wahr(seite - 0.25f >= -0.01f, $"Nummer {n} bei {strecke} m: Seite {seite}, die Schulter ragt links über die Mitte");
                    Pruefe.Wahr(seite + 0.25f <= Passantenspur.Gassenbreite / 2 + 0.2f, $"Nummer {n} bei {strecke} m: Seite {seite}, zu nah am rechten Rand");
                }
        }),

        ("Passantenspur: Anfang und Ende gleichen sich über einen Meter an – von der Mitte an, ohne Sprung, am Ende zurück auf die Mitte", () =>
        {
            foreach (var n in new[] { 0, 1, 2, 17, 99, 250 })
            {
                Pruefe.Ungefaehr(0, Passantenspur.Seite(n, 0, 30), 1e-6);
                Pruefe.Ungefaehr(0, Passantenspur.Seite(n, 30, 0), 1e-6);
                // nach dem Übergangsmaß ist sie ganz auf ihrer Spur
                var ziel = Passantenspur.Grundspur(n) + Passantenspur.Schwankung(n, Passantenspur.Uebergang);
                Pruefe.Ungefaehr(ziel, Passantenspur.Seite(n, Passantenspur.Uebergang, 30), 1e-5);
                // Steigung der Spur: nirgends steiler als etwa 2 m je Meter (Übergang) und im Lauf nie ein Sprung über 3 cm je Zentimeter Weg
                var vorher = Passantenspur.Seite(n, 0, 30);
                for (var s = 0.01f; s <= 30f; s += 0.01f)
                {
                    var jetzt = Passantenspur.Seite(n, s, 30 - s);
                    Pruefe.Wahr(MathF.Abs(jetzt - vorher) <= 0.02f, $"Nummer {n} bei {s} m: springt um {jetzt - vorher} m je Zentimeter");
                    vorher = jetzt;
                }
            }
        }),

        ("Passantenspur: der Anschluss an einen laufenden Weg übernimmt das alte Maß und gleitet in die neue Spur", () =>
        {
            var n = 5;
            Pruefe.Ungefaehr(1.4, Passantenspur.Seite(n, 0, 30, 1.4f), 1e-6);
            var vorher = 1.4f;
            for (var s = 0.01f; s <= 5f; s += 0.01f)
            {
                var jetzt = Passantenspur.Seite(n, s, 30 - s, 1.4f);
                Pruefe.Wahr(MathF.Abs(jetzt - vorher) <= 0.02f, $"bei {s} m: springt um {jetzt - vorher} m");
                vorher = jetzt;
            }
            Pruefe.Ungefaehr(Passantenspur.Seite(n, 4f, 26f), Passantenspur.Seite(n, 4f, 26f, 1.4f), 1e-5);
            // ein sehr kurzer Weg (unter zwei Metern) bleibt trotzdem stetig und nie über der Spur
            var kurz = Enumerable.Range(0, 151).Select(i => Passantenspur.Seite(3, i * 0.01f, 1.5f - i * 0.01f)).ToList();
            Pruefe.Wahr(kurz.Max() <= Passantenspur.Spurfern + Passantenspur.Schwanken + 1e-5f, "kurzer Weg: zu weit rechts");
            for (var i = 1; i < kurz.Count; i++) Pruefe.Wahr(MathF.Abs(kurz[i] - kurz[i - 1]) <= 0.02f, "kurzer Weg: Sprung");
        }),

        ("Passantenspur: das seitliche Pendeln ist so flach, dass die Füße nicht rutschen (Weg wird höchstens 3 % länger)", () =>
        {
            foreach (var n in Nummern())
            {
                var weg = 0.0;
                var vorher = Passantenspur.Seite(n, 1f, 59f);
                for (var s = 1.01f; s <= 40f; s += 0.01f)
                {
                    var jetzt = Passantenspur.Seite(n, s, 60f - s);
                    weg += Math.Sqrt(0.01 * 0.01 + (jetzt - vorher) * (jetzt - vorher));
                    vorher = jetzt;
                }
                var laenge = (40f - 1f);
                Pruefe.Wahr(weg / laenge <= 1.04, $"Nummer {n}: Weg {weg / laenge:0.000}-mal so lang");
            }
        }),

        ("Passantenspur: die Rechtsrichtung kommt aus den Punkten vor und hinter der Figur – an der Ecke gerundet, bei Umkehr der Rückfall", () =>
        {
            // geradeaus nach +z: rechts ist −x (wie bisher (−z, x))
            var (x, z) = Passantenspur.Rechts((0, -0.5f), (0, 0.5f), (0, 1));
            Pruefe.Ungefaehr(-1, x, 1e-6); Pruefe.Ungefaehr(0, z, 1e-6);
            // nach +x: rechts ist +z
            (x, z) = Passantenspur.Rechts((-0.5f, 0), (0.5f, 0), (1, 0));
            Pruefe.Ungefaehr(0, x, 1e-6); Pruefe.Ungefaehr(1, z, 1e-6);
            // an der Ecke (von −z nach +x kommend): die Mitte der beiden Richtungen, Länge 1
            (x, z) = Passantenspur.Rechts((0, -0.5f), (0.5f, 0), (1, 0));
            Pruefe.Ungefaehr(1, Math.Sqrt(x * x + z * z), 1e-6);
            Pruefe.Ungefaehr(-Math.Sqrt(0.5), x, 1e-6); Pruefe.Ungefaehr(Math.Sqrt(0.5), z, 1e-6);
            // Umkehr auf der Stelle: der Rückfall
            (x, z) = Passantenspur.Rechts((0, 0), (0.01f, 0), (0, 1));
            Pruefe.Ungefaehr(-1, x, 1e-6);
            // gar keine Richtung: null statt NaN
            (x, z) = Passantenspur.Rechts((0, 0), (0, 0), (0, 0));
            Pruefe.Gleich((0f, 0f), (x, z));
        }),
    };
}
