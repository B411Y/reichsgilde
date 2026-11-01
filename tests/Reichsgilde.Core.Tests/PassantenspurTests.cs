using Reichsgilde;

/// <summary>
/// Die Spuren der Passanten (Darstellung, 1.11.2026; Modell in <c>game/scripts/Passantenspur.cs</c>, hier mitübersetzt): jede Person hat ihre eigene Spur auf
/// der rechten Gassenhälfte (Rechtsverkehr), schwankt sanft darum (±0,15 m, Wellenlänge 3 bis 5 m) und gleicht sich an Anfang und Ende über einen Meter an.
/// </summary>
static class PassantenspurTests
{
    /// <summary>Alle Nummern, die die Tests durchgehen (die Einwohner einer großen Stadt).</summary>
    static IEnumerable<int> Nummern() => Enumerable.Range(0, 400);

    /// <summary>Eine Mittellinie aus einer Punktfolge (x, z), wie das Gassennetz sie liefert: Punkt nach so vielen Metern Weg.</summary>
    sealed class Linie : Passantenspur.IMittellinie
    {
        readonly (float X, float Z)[] _punkte;
        public Linie(params (float X, float Z)[] punkte) => _punkte = punkte;
        public float Laenge => _punkte.Zip(_punkte.Skip(1), (a, b) => MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z))).Sum();
        public (float X, float Z) Bei(float strecke)
        {
            var rest = Math.Clamp(strecke, 0, Laenge);
            for (var i = 0; i + 1 < _punkte.Length; i++)
            {
                var (a, b) = (_punkte[i], _punkte[i + 1]);
                var stueck = MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
                if (rest <= stueck) return stueck > 0 ? (a.X + (b.X - a.X) * rest / stueck, a.Z + (b.Z - a.Z) * rest / stueck) : a;
                rest -= stueck;
            }
            return _punkte[^1];
        }
    }

    static double Korrelation(IEnumerable<float> a, IEnumerable<float> b)
    {
        var (x, y) = (a.ToArray(), b.ToArray());
        double mx = x.Average(), my = y.Average(), sxy = 0, sxx = 0, syy = 0;
        for (var i = 0; i < x.Length; i++) { sxy += (x[i] - mx) * (y[i] - my); sxx += (x[i] - mx) * (x[i] - mx); syy += (y[i] - my) * (y[i] - my); }
        return sxy / Math.Sqrt(sxx * syy);
    }

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
            var verschiedene = Nummern().Select(n => MathF.Round(Passantenspur.Grundspur(n), 3)).Distinct().Count();
            Pruefe.Wahr(verschiedene >= 100, $"nur {verschiedene} verschiedene Spuren auf drei Stellen");
            // gleichmäßig verteilt: in jedem Drittel der Breite liegen mindestens ein Viertel der Personen
            var drittel = (Passantenspur.Spurfern - Passantenspur.Spurnah) / 3;
            for (var i = 0; i < 3; i++)
            {
                var von = Passantenspur.Spurnah + i * drittel;
                var anzahl = Nummern().Count(n => Passantenspur.Grundspur(n) >= von && Passantenspur.Grundspur(n) < von + drittel + (i == 2 ? 1 : 0));
                Pruefe.Wahr(anzahl > Nummern().Count() / 4, $"im {i + 1}. Drittel nur {anzahl} Personen");
            }
            // aufeinanderfolgende Nummern liegen nicht nebeneinander (Nachbarn im Haus gehen nicht auf derselben Spur)
            var gleichauf = Nummern().Zip(Nummern().Skip(1), (a, b) => MathF.Abs(Passantenspur.Grundspur(a) - Passantenspur.Grundspur(b))).Count(d => d < 0.03f);
            Pruefe.Wahr(gleichauf < 10, $"{gleichauf} Nachbarn mit fast derselben Spur");
        }),

        ("Passantenspur: das Schwanken bleibt bei ±0,15 m, die Wellen sind 3–4 und 4–5 m lang, die Phasen sind von Spur und Wellenlänge unabhängig", () =>
        {
            foreach (var n in Nummern())
            {
                var (kurz, lang) = Passantenspur.Wellenlaengen(n);
                Pruefe.Wahr(kurz >= 3f && kurz <= 4f && lang >= 4f && lang <= 5f, $"Nummer {n}: Wellenlängen {kurz}/{lang}");
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
            // Grundspur, Wellenlängen und Phasen hängen nicht voneinander ab (sonst schwankten Nachbarn auf ähnlicher Spur im Gleichschritt)
            var werte = new List<List<float>> { Nummern().Select(Passantenspur.Grundspur).ToList() };
            for (var k = 1; k <= 4; k++) werte.Add(Nummern().Select(n => Passantenspur.Bruchteil(n, k)).ToList());
            for (var a = 0; a < werte.Count; a++)
                for (var b = a + 1; b < werte.Count; b++)
                    Pruefe.Wahr(Math.Abs(Korrelation(werte[a], werte[b])) < 0.2, $"Größe {a} und {b} hängen zusammen (r = {Korrelation(werte[a], werte[b]):0.00})");
        }),

        ("Passantenspur: unterwegs bleibt die Mitte der Figur auf der rechten Hälfte und der Körper (Schultern ±0,25 m) innerhalb der 2-m-Gasse", () =>
        {
            foreach (var n in Nummern())
                for (var strecke = 1f; strecke <= 59f; strecke += 0.05f)
                {
                    var seite = Passantenspur.Seite(n, strecke, 60f - strecke);
                    Pruefe.Wahr(seite >= 0.2f - 1e-4f, $"Nummer {n} bei {strecke} m: Seite {seite}, die Mitte der Figur nah an der Gassenmitte");
                    Pruefe.Wahr(seite - 0.25f >= -0.06f, $"Nummer {n} bei {strecke} m: Seite {seite}, die Schulter ragt links über die Mitte");
                    Pruefe.Wahr(seite + 0.25f <= Passantenspur.Gassenbreite / 2 + 1e-3f, $"Nummer {n} bei {strecke} m: Seite {seite}, der Körper ragt über den rechten Rand");
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
                // Steigung der Spur: nirgends steiler als 1,5 · 0,75 + Schwanken (≈ 1,41 m je Meter), im Lauf also höchstens 1,5 cm je Zentimeter Weg
                var vorher = Passantenspur.Seite(n, 0, 30);
                for (var s = 0.01f; s <= 30f; s += 0.01f)
                {
                    var jetzt = Passantenspur.Seite(n, s, 30 - s);
                    Pruefe.Wahr(MathF.Abs(jetzt - vorher) <= 0.015f, $"Nummer {n} bei {s} m: springt um {jetzt - vorher} m je Zentimeter");
                    vorher = jetzt;
                }
            }
        }),

        ("Passantenspur: der Anschluss an einen laufenden Weg übernimmt den alten Versatz samt Richtung und gleitet ohne Sprung in die neue Spur", () =>
        {
            // der Versatz war nach links (±x) oder in Gehrichtung des alten Weges (z), der neue Weg liegt anders: kein Sprung, wie groß auch der Unterschied
            foreach (var (start, rechts) in new[] { ((1.4f, 0f), (0f, 1f)), ((0f, 0.6f), (1f, 0f)), ((-0.7f, 0.7f), (0.7071f, 0.7071f)), ((0.6f, 0f), (1f, 0f)) })
            {
                var n = 5;
                var anfang = Passantenspur.Versatz(n, 0, 30, start, rechts);
                Pruefe.Ungefaehr(start.Item1, anfang.X, 1e-6); Pruefe.Ungefaehr(start.Item2, anfang.Z, 1e-6);
                var vorher = anfang;
                for (var s = 0.01f; s <= 6f; s += 0.01f)
                {
                    var jetzt = Passantenspur.Versatz(n, s, 30 - s, start, rechts);
                    var schritt = MathF.Sqrt((jetzt.X - vorher.X) * (jetzt.X - vorher.X) + (jetzt.Z - vorher.Z) * (jetzt.Z - vorher.Z));
                    Pruefe.Wahr(schritt <= 0.015f, $"Anschluss {start}: springt um {schritt} m je Zentimeter bei {s} m");
                    vorher = jetzt;
                }
                // danach ist sie ganz auf der neuen Spur
                var spaet = Passantenspur.Versatz(n, 20, 10, start, rechts);
                var spur = Passantenspur.Grundspur(n) + Passantenspur.Schwankung(n, 20);
                Pruefe.Ungefaehr(rechts.Item1 * spur, spaet.X, 1e-4); Pruefe.Ungefaehr(rechts.Item2 * spur, spaet.Z, 1e-4);
            }
            // ein sehr kurzer Weg (unter zwei Metern) bleibt trotzdem stetig und nie über der Spur
            var kurz = Enumerable.Range(0, 151).Select(i => Passantenspur.Seite(3, i * 0.01f, 1.5f - i * 0.01f)).ToList();
            Pruefe.Wahr(kurz.Max() <= Passantenspur.Spurfern + Passantenspur.Schwanken + 1e-5f, "kurzer Weg: zu weit rechts");
            for (var i = 1; i < kurz.Count; i++) Pruefe.Wahr(MathF.Abs(kurz[i] - kurz[i - 1]) <= 0.015f, "kurzer Weg: Sprung");
        }),

        ("Passantenspur: die Füße rutschen nicht – Bewegung und Takt folgen dem Weg auf dem Boden, geradeaus, an einer Ecke und beim Anschluss", () =>
        {
            var gerade = new Linie((0, 0), (0, 40));
            var ecke = new Linie((0, 0), (0, 20), (20, 20)); // 90°-Ecke nach links: die Figur geht rechts, also außen
            var eckeRechts = new Linie((0, 0), (0, 20), (-20, 20)); // nach rechts: die Figur geht innen
            foreach (var (linie, start, name) in new[] { (gerade, (0f, 0f), "gerade"), (ecke, (0f, 0f), "Ecke links (außen)"), (eckeRechts, (0f, 0f), "Ecke rechts (innen)"), (gerade, (-0.7f, 0f), "Anschluss") })
                foreach (var n in Nummern().Where(n => n % 11 == 0))
                {
                    var laenge = linie.Laenge;
                    var vorher = Passantenspur.Gehen(linie, n, laenge, 0, start, false).Ort;
                    var (summeTakt, summeBoden, schritte) = (0.0, 0.0, 0);
                    for (var s = 0.01f; s <= laenge; s += 0.01f)
                    {
                        var gang = Passantenspur.Gehen(linie, n, laenge, s, start, true);
                        var wirklich = MathF.Sqrt((gang.Ort.X - vorher.X) * (gang.Ort.X - vorher.X) + (gang.Ort.Z - vorher.Z) * (gang.Ort.Z - vorher.Z)) / 0.01f;
                        var faktor = MathF.Sqrt(gang.Bewegung.X * gang.Bewegung.X + gang.Bewegung.Z * gang.Bewegung.Z);
                        // Takt (Länge der Bewegung) und gemessener Weg auf dem Boden stimmen überein: geradeaus Schritt für Schritt, sonst (an den Rändern des
                        // Eckenfensters ändert sich die Steigung sprunghaft, die Schrittweite von 0,3 m mittelt das) über jedes Stück von einem Meter
                        if (name == "gerade") Pruefe.Wahr(MathF.Abs(faktor - wirklich) <= 0.12f, $"{name}, Nummer {n}, bei {s} m: Takt {faktor}, Weg auf dem Boden {wirklich}");
                        (summeTakt, summeBoden, schritte) = (summeTakt + faktor, summeBoden + wirklich, schritte + 1);
                        if (schritte == 100)
                        {
                            Pruefe.Wahr(Math.Abs(summeTakt - summeBoden) / 100 <= 0.04, $"{name}, Nummer {n}, Meter bei {s} m: Takt {summeTakt / 100:0.000} im Mittel, Weg auf dem Boden {summeBoden / 100:0.000}");
                            (summeTakt, summeBoden, schritte) = (0, 0, 0);
                        }
                        // und die Figur springt nirgends (auch an der Ecke nicht): höchstens 3 cm je Zentimeter Weg
                        Pruefe.Wahr(wirklich <= 3f, $"{name}, Nummer {n}, bei {s} m: springt ({wirklich} cm je cm)");
                        // im Gehen auf gerader Strecke ohne Übergang höchstens 4,5 % mehr, in den Übergängen bis 1,6
                        if (name == "gerade" && s > 1.5f && laenge - s > 1.5f) Pruefe.Wahr(faktor <= 1.045f, $"Nummer {n} bei {s} m: Faktor {faktor} im Gehen");
                        if (name != "Anschluss") Pruefe.Wahr(faktor <= 2.5f, $"{name}, Nummer {n}, bei {s} m: Faktor {faktor}");
                        vorher = gang.Ort;
                    }
                }
            // Ecke: außen läuft die Figur weiter als die Mittellinie, innen kürzer – der Takt folgt
            var aussen = Passantenspur.Gehen(ecke, 3, ecke.Laenge, 20f, (0, 0), true).Bewegung;
            var innen = Passantenspur.Gehen(eckeRechts, 3, eckeRechts.Laenge, 20f, (0, 0), true).Bewegung;
            Pruefe.Wahr(MathF.Sqrt(innen.X * innen.X + innen.Z * innen.Z) < MathF.Sqrt(aussen.X * aussen.X + aussen.Z * aussen.Z), "innen kürzer als außen");
            // am Anfang geht sie schräg auf die Spur zu (Blick nach rechts, rechts ist −x), am Ende zurück zur Mitte
            var anfang = Passantenspur.Gehen(gerade, 1, gerade.Laenge, 0.5f, (0, 0), true).Bewegung;
            Pruefe.Wahr(anfang.X < -0.3f && anfang.Z > 0.5f, $"Anfang: Bewegung {anfang}");
            var ende = Passantenspur.Gehen(gerade, 1, gerade.Laenge, gerade.Laenge - 0.5f, (0, 0), true).Bewegung;
            Pruefe.Wahr(ende.X > 0.3f, $"Ende: Bewegung {ende}");
            // Ohne verlangte Bewegung bleibt sie null
            Pruefe.Gleich(((float, float))(0, 0), Passantenspur.Gehen(gerade, 1, 40, 10, (0, 0), false).Bewegung);
        }),

        ("Passantenspur: auch auf einem sehr kurzen neuen Weg bleibt der Anschluss ohne Sprung (der alte Versatz verschwindet erst, wenn die Spur erreicht ist)", () =>
        {
            foreach (var laenge in new[] { 0.2f, 0.5f, 0.9f, 1.5f })
            {
                var start = (0.7f, -0.3f);
                var anfang = Passantenspur.Versatz(4, 0, laenge, start, (-1, 0));
                Pruefe.Ungefaehr(start.Item1, anfang.X, 1e-6); Pruefe.Ungefaehr(start.Item2, anfang.Z, 1e-6);
                var vorher = anfang;
                for (var s = 0.01f; s <= laenge; s += 0.01f)
                {
                    var jetzt = Passantenspur.Versatz(4, s, laenge - s, start, (-1, 0));
                    Pruefe.Wahr(MathF.Sqrt((jetzt.X - vorher.X) * (jetzt.X - vorher.X) + (jetzt.Z - vorher.Z) * (jetzt.Z - vorher.Z)) <= 0.03f, $"Länge {laenge} bei {s} m: Sprung");
                    vorher = jetzt;
                }
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
