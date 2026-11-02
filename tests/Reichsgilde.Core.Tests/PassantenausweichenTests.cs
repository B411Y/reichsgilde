using Reichsgilde;

/// <summary>
/// Schicht 4 der Passanten (Darstellung, 1.11.2026; Modell in <c>game/scripts/Passantenausweichen.cs</c>, hier mitübersetzt): Figuren weichen sich seitlich
/// innerhalb der Wegbreite aus, werden notfalls langsamer, zittern nicht und prüfen nur Nachbarn im Raster. Die Tests lassen Figuren auf einer geraden
/// Gasse (längs x) gehen, wie die Darstellung es Bild für Bild tut.
/// </summary>
static class PassantenausweichenTests
{
    /// <summary>Eine Person auf der Testgasse: Spur quer zur Gasse (z, Meter), Richtung (+1 nach +x, −1 nach −x), Tempo, Lage längs.</summary>
    sealed class Person
    {
        public float X, Spur, Tempo, Radius = 0.22f, Seitlich, Bremse = 1, Haltezeit;
        public int Richtung = 1, Seite, Wechsel, Zittern;
        public float LetzteRate, SeitWechsel;
        public bool Steht;
        public float Z => Spur + Seitlich * Richtung; // rechts ist (−z, x) der Gehrichtung: nach +x ist rechts +z, nach −x ist rechts −z
    }

    /// <summary>
    /// Lässt die Personen <paramref name="sekunden"/> lang gehen (Bilder zu <paramref name="dt"/> s) und zählt die Bilder mit Überschneidung (Abstand der Mitten
    /// unter der Summe der Körper). Mit <paramref name="ausweichen"/> = false ohne Schicht 4 (zum Vergleich). Ergebnis: Überschneidungen (Paar-Bilder),
    /// kleinster Abstand über die Körper hinaus, größter Fehler am Rand.
    /// </summary>
    static (int Ueberschneidungen, float Kleinster, float Randfehler) Gehen(List<Person> leute, float halb, float sekunden, bool ausweichen = true, float dt = 1 / 15f,
        Passantenausweichen? rechner = null)
    {
        rechner ??= new Passantenausweichen();
        var figuren = new Passantenausweichen.Figur[leute.Count];
        var (ueber, kleinster, randfehler) = (0, float.MaxValue, 0f);
        var spielraum = halb - Passantenspur.Wandabstand;
        for (var bild = 0; bild * dt < sekunden; bild++)
        {
            for (var k = 0; k < leute.Count; k++)
            {
                var p = leute[k];
                figuren[k] = new Passantenausweichen.Figur
                {
                    X = p.X, Z = p.Z, Vx = p.Steht ? 0 : p.Richtung * p.Tempo * p.Bremse, Vz = 0, Rx = 0, Rz = p.Richtung, Radius = p.Radius,
                    // der Versatz bleibt so, dass die Mitte höchstens „halb − Wandabstand“ von der Gassenmitte weg ist
                    Links = p.Richtung > 0 ? -spielraum - p.Spur : p.Spur - spielraum,
                    Rechts = p.Richtung > 0 ? spielraum - p.Spur : p.Spur + spielraum,
                    Seitlich = p.Seitlich, Seite = p.Seite, Laeuft = !p.Steht, Bremse = p.Bremse, Haltezeit = p.Haltezeit, Nummer = k,
                };
            }
            if (ausweichen) rechner.Schritt(figuren, figuren.Length, dt);
            for (var k = 0; k < leute.Count; k++)
            {
                var p = leute[k];
                if (p.Steht) continue;
                var rate = (figuren[k].Seitlich - p.Seitlich) / dt;
                // Wechsel der Seitenrichtung; Zittern, wenn sie seit dem letzten Wechsel keine 5 cm in eine Richtung gegangen ist (hin und gleich wieder her)
                if (MathF.Abs(rate) > 0.02f && MathF.Abs(p.LetzteRate) > 0.02f && MathF.Sign(rate) != MathF.Sign(p.LetzteRate))
                {
                    p.Wechsel++;
                    if (MathF.Abs(p.SeitWechsel) < 0.05f) p.Zittern++;
                    p.SeitWechsel = 0;
                }
                p.SeitWechsel += figuren[k].Seitlich - p.Seitlich;
                if (MathF.Abs(rate) > 0.02f) p.LetzteRate = rate;
                p.Seitlich = figuren[k].Seitlich;
                (p.Seite, p.Bremse, p.Haltezeit) = (figuren[k].Seite, figuren[k].Bremse, figuren[k].Haltezeit);
                p.X += p.Richtung * p.Tempo * p.Bremse * dt;
                randfehler = MathF.Max(randfehler, MathF.Abs(p.Z) - MathF.Max(spielraum, MathF.Abs(p.Spur)));
            }
            for (var a = 0; a < leute.Count; a++)
                for (var b = a + 1; b < leute.Count; b++)
                {
                    var (dx, dz) = (leute[a].X - leute[b].X, leute[a].Z - leute[b].Z);
                    var luft = MathF.Sqrt(dx * dx + dz * dz) - leute[a].Radius - leute[b].Radius;
                    kleinster = MathF.Min(kleinster, luft);
                    if (luft < 0) ueber++;
                }
        }
        return (ueber, kleinster, randfehler);
    }

    /// <summary>Eine belebte Gasse: <paramref name="anzahl"/> Leute in beide Richtungen, fest gestreut (Spur auf der rechten Hälfte, Tempo 1,0 bis 1,6 m/s).</summary>
    static List<Person> Gedraenge(int anzahl, float halb, float laenge)
    {
        var leute = new List<Person>();
        for (var k = 0; k < anzahl; k++)
        {
            var richtung = k % 2 == 0 ? 1 : -1;
            var spur = Passantenspur.GrundspurBei(k, halb) * richtung; // rechte Hälfte der eigenen Richtung
            leute.Add(new Person
            {
                X = Passantenspur.Bruchteil(k, 20) * laenge, Spur = spur, Richtung = richtung, Tempo = 1.0f + 0.6f * Passantenspur.Bruchteil(k, 21),
                Radius = k % 7 == 0 ? 0.32f : 0.22f, // dabei ein Oger
            });
        }
        return leute;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Passantenausweichen: zwei Leute auf derselben Linie im Gegenverkehr weichen sich in der 2-m-Gasse aus, ohne sich zu überschneiden", () =>
        {
            var leute = new List<Person>
            {
                new() { X = 0, Spur = 0, Richtung = 1, Tempo = 1.3f },
                new() { X = 12, Spur = 0, Richtung = -1, Tempo = 1.3f },
            };
            var ohne = Gehen(leute.Select(p => new Person { X = p.X, Spur = p.Spur, Richtung = p.Richtung, Tempo = p.Tempo }).ToList(), 1f, 10f, ausweichen: false);
            Pruefe.Wahr(ohne.Ueberschneidungen > 0, "ohne Ausweichen müssten sie sich treffen (Testaufbau)");
            var mit = Gehen(leute, 1f, 10f);
            Pruefe.Gleich(0, mit.Ueberschneidungen, $"Überschneidungen (kleinster Abstand {mit.Kleinster:0.00} m)");
            Pruefe.Wahr(mit.Randfehler <= 0.01f, $"am Rand um {mit.Randfehler:0.00} m zu weit");
            // beide gehen nach rechts (Rechtsverkehr), nicht in dieselbe Richtung
            Pruefe.Wahr(leute.All(p => p.LetzteRate != 0), "beide sind ausgewichen");
            Pruefe.Wahr(leute[0].X > 11 && leute[1].X < 1, $"vorbei: {leute[0].X:0.0} / {leute[1].X:0.0}");
        }),

        ("Passantenausweichen: wer hinter einem Langsameren nicht vorbeikommt, wird langsamer statt hineinzulaufen", () =>
        {
            // enge Gasse (Türweg, 1,2 m): kein Platz zum Überholen
            var leute = new List<Person>
            {
                new() { X = 3, Spur = 0, Richtung = 1, Tempo = 0.8f },
                new() { X = 0, Spur = 0, Richtung = 1, Tempo = 1.6f },
            };
            var ergebnis = Gehen(leute, 0.6f, 12f);
            Pruefe.Gleich(0, ergebnis.Ueberschneidungen, $"Überschneidungen (kleinster Abstand {ergebnis.Kleinster:0.00} m)");
            Pruefe.Wahr(leute[1].X < leute[0].X, "der Schnellere bleibt hinten");
            Pruefe.Wahr(leute[0].X - leute[1].X < 1.2f, $"er geht dicht dahinter her ({leute[0].X - leute[1].X:0.00} m), statt stehen zu bleiben");
            // in der breiten Straße überholt er dagegen
            var breit = new List<Person>
            {
                new() { X = 3, Spur = 0.6f, Richtung = 1, Tempo = 0.8f },
                new() { X = 0, Spur = 0.6f, Richtung = 1, Tempo = 1.6f },
            };
            var ueberholt = Gehen(breit, 2.5f, 12f);
            Pruefe.Gleich(0, ueberholt.Ueberschneidungen, "Überschneidungen beim Überholen");
            Pruefe.Wahr(breit[1].X > breit[0].X + 2, "auf der breiten Straße überholt er");
        }),

        ("Passantenausweichen: wer steht, wird umgangen (die Laufende weicht ganz aus)", () =>
        {
            var leute = new List<Person>
            {
                new() { X = 5, Spur = 0.5f, Richtung = 1, Steht = true },
                new() { X = 0, Spur = 0.5f, Richtung = 1, Tempo = 1.3f },
            };
            var ergebnis = Gehen(leute, 2.5f, 8f);
            Pruefe.Gleich(0, ergebnis.Ueberschneidungen, $"Überschneidungen (kleinster Abstand {ergebnis.Kleinster:0.00} m)");
            Pruefe.Wahr(leute[1].X > 9, "sie ist vorbeigegangen");
            Pruefe.Ungefaehr(5, leute[0].X, 1e-6);
        }),

        ("Passantenausweichen: eine belebte Gasse in beiden Richtungen – kaum Überschneidungen (ohne Ausweichen viele), der Rand hält, kein Zittern", () =>
        {
            foreach (var (halb, anzahl) in new[] { (1.0f, 16), (1.75f, 24), (2.5f, 40) })
            {
                var ohne = Gehen(Gedraenge(anzahl, halb, 60), halb, 40f, ausweichen: false);
                var leute = Gedraenge(anzahl, halb, 60);
                var mit = Gehen(leute, halb, 40f);
                // Paar-Bilder mit Überschneidung: höchstens ein Zwanzigstel von vorher (Ziel: nahezu null)
                Pruefe.Wahr(mit.Ueberschneidungen * 20 <= Math.Max(ohne.Ueberschneidungen, 20), $"Breite {2 * halb} m: {mit.Ueberschneidungen} Überschneidungen (ohne Ausweichen {ohne.Ueberschneidungen})");
                Pruefe.Wahr(mit.Randfehler <= 0.01f, $"Breite {2 * halb} m: am Rand um {mit.Randfehler:0.00} m zu weit");
                // kein Zittern: hin und gleich wieder her (weniger als 5 cm) kommt kaum vor, und die Seitenrichtung wechselt im Mittel selten
                // (in 40 s höchstens dreimal je Person – jedes Ausweichen und Zurückgleiten ist ein Wechsel)
                Pruefe.Wahr(leute.Sum(p => p.Zittern) <= leute.Count / 8, $"Breite {2 * halb} m: {leute.Sum(p => p.Zittern)}-mal Zittern (höchstens {leute.Max(p => p.Zittern)} bei einer Person)");
                Pruefe.Wahr(leute.Average(p => p.Wechsel) <= 3, $"Breite {2 * halb} m: im Mittel {leute.Average(p => p.Wechsel):0.0} Wechsel");
            }
        }),

        ("Passantenausweichen: an einer Kreuzung lässt eine der anderen den Vortritt – keine Überschneidung, keiner bleibt stehen", () =>
        {
            // zwei Wege kreuzen sich rechtwinklig bei (0 | 0); beide kämen zugleich an. Seitlich weichen sie quer zu ihrem Weg aus (eng: 0,3 m Raum)
            foreach (var schraeg in new[] { 0f, 0.3f })
            {
                var rechner = new Passantenausweichen();
                var (ax, bz) = (-6f, -6f - schraeg);
                var figuren = new Passantenausweichen.Figur[2];
                var (abremse, bbremse, ahalt, bhalt, aseit, bseit, aseite, bseite) = (1f, 1f, 0f, 0f, 0f, 0f, 0, 0);
                var kleinster = float.MaxValue;
                for (var bild = 0; bild < 15 * 12; bild++)
                {
                    // A geht nach +x (rechts ist +z), B nach +z (rechts ist −x)
                    figuren[0] = new() { X = ax, Z = aseit, Vx = 1.3f * abremse, Rx = 0, Rz = 1, Radius = 0.22f, Links = -0.3f, Rechts = 0.3f, Seitlich = aseit, Seite = aseite, Bremse = abremse, Haltezeit = ahalt, Laeuft = true, Nummer = 1 };
                    figuren[1] = new() { X = -bseit, Z = bz, Vz = 1.3f * bbremse, Rx = -1, Rz = 0, Radius = 0.22f, Links = -0.3f, Rechts = 0.3f, Seitlich = bseit, Seite = bseite, Bremse = bbremse, Haltezeit = bhalt, Laeuft = true, Nummer = 2 };
                    rechner.Schritt(figuren, 2, 1 / 15f);
                    (abremse, bbremse, ahalt, bhalt, aseit, bseit, aseite, bseite) = (figuren[0].Bremse, figuren[1].Bremse, figuren[0].Haltezeit, figuren[1].Haltezeit, figuren[0].Seitlich, figuren[1].Seitlich, figuren[0].Seite, figuren[1].Seite);
                    ax += 1.3f * abremse / 15;
                    bz += 1.3f * bbremse / 15;
                    var (dx, dz) = (ax + bseit, aseit - bz);
                    kleinster = MathF.Min(kleinster, MathF.Sqrt(dx * dx + dz * dz));
                }
                Pruefe.Wahr(kleinster >= 0.44f, $"versetzt {schraeg} m: kleinster Abstand {kleinster:0.00} m (Körper 0,44 m)");
                Pruefe.Wahr(ax > 6 && bz > 6, $"versetzt {schraeg} m: nicht beide durchgekommen ({ax:0.0} | {bz:0.0})");
            }
        }),

        ("Passantenausweichen: die Kosten wachsen mit der Dichte, nicht mit dem Quadrat der Anzahl (Raster statt jeder gegen jeden)", () =>
        {
            var rechner = new Passantenausweichen();
            foreach (var anzahl in new[] { 100, 400, 1600 })
            {
                // gleich dicht: die Fläche wächst mit der Anzahl (je Figur 25 m²)
                var seite = MathF.Sqrt(anzahl * 25f);
                var figuren = Enumerable.Range(0, anzahl).Select(k => new Passantenausweichen.Figur
                {
                    X = Passantenspur.Bruchteil(k, 30) * seite, Z = Passantenspur.Bruchteil(k, 31) * seite, Vx = 1.3f, Rz = 1, Radius = 0.22f,
                    Links = -1, Rechts = 1, Laeuft = true,
                }).ToArray();
                rechner.Schritt(figuren, anzahl, 1 / 15f);
                Pruefe.Wahr(rechner.Paare <= anzahl * 12, $"{anzahl} Figuren: {rechner.Paare} Paare verglichen");
            }
        }),

        ("Passantenausweichen: ohne Nachbarn bleibt alles, wie es ist, und ein alter Versatz gleitet langsam zurück zur Spur", () =>
        {
            var figuren = new[] { new Passantenausweichen.Figur { X = 0, Z = 0, Vx = 1.3f, Rz = 1, Radius = 0.22f, Links = -1, Rechts = 1, Seitlich = 0.4f, Laeuft = true, Bremse = 1 } };
            var rechner = new Passantenausweichen();
            rechner.Schritt(figuren, 1, 0.1f);
            Pruefe.Ungefaehr(1, figuren[0].Bremse, 1e-6);
            Pruefe.Ungefaehr(0.4 - Passantenausweichen.Zurueck * 0.1, figuren[0].Seitlich, 1e-5);
            for (var k = 0; k < 40; k++) rechner.Schritt(figuren, 1, 0.1f);
            Pruefe.Wahr(MathF.Abs(figuren[0].Seitlich) < 1e-5f, $"nach 4 s noch {figuren[0].Seitlich} m neben der Spur");
        }),
    };
}
