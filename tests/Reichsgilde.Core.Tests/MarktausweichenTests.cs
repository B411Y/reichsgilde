using Reichsgilde;

/// <summary>
/// Ausweichen auf der freien Fläche eines Marktplatzes (Darstellung, Markt v3 vom 1.11.2026): Auf dem Platz kreuzen sich Wege in jedem Winkel; Schicht 4
/// (<c>Passantenausweichen</c>) gilt dort unverändert, nur mit dem Raum der Fläche (hier 1,5 m je Seite). Die Tests lassen Leute auf geraden Linien quer und entgegen über eine offene Fläche gehen, wie die Darstellung es Bild
/// für Bild tut (seitlicher Versatz quer zur eigenen Linie, Tempo mal Bremse), und zählen Überschneidungen der Körper.
/// </summary>
static class MarktausweichenTests
{
    sealed class Person
    {
        public float X0, Z0, Fx, Fz, Tempo = 1.2f, Weg, Seitlich, Bremse = 1, Haltezeit, Radius = 0.22f;
        public int Seite, Nummer;
        /// <summary>Wo sie ist: auf ihrer Linie, seitlich um <see cref="Seitlich"/> nach rechts (rechts = (−z, x) der Gehrichtung).</summary>
        public (float X, float Z) Lage => (X0 + Fx * Weg - Fz * Seitlich, Z0 + Fz * Weg + Fx * Seitlich);
    }

    /// <summary>Geht <paramref name="sekunden"/> lang; Ergebnis: Paar-Bilder mit Überschneidung und kleinster Abstand über die Körper hinaus.</summary>
    static (int Ueber, float Kleinster) Gehen(List<Person> leute, float sekunden, float dt = 1 / 15f)
    {
        var rechner = new Passantenausweichen();
        var figuren = new Passantenausweichen.Figur[leute.Count];
        var (ueber, kleinster) = (0, float.MaxValue);
        for (var t = 0f; t < sekunden; t += dt)
        {
            for (var k = 0; k < leute.Count; k++)
            {
                var p = leute[k];
                var (x, z) = p.Lage;
                figuren[k] = new Passantenausweichen.Figur
                {
                    X = x, Z = z, Vx = p.Fx * p.Tempo * p.Bremse, Vz = p.Fz * p.Tempo * p.Bremse, Rx = -p.Fz, Rz = p.Fx, Radius = p.Radius,
                    Links = -1.5f, Rechts = 1.5f, Seitlich = p.Seitlich, Seite = p.Seite, Bremse = p.Bremse, Haltezeit = p.Haltezeit, Laeuft = true,
                    Nummer = p.Nummer,
                };
            }
            rechner.Schritt(figuren, leute.Count, dt);
            for (var k = 0; k < leute.Count; k++)
            {
                var p = leute[k];
                (p.Seitlich, p.Seite, p.Bremse, p.Haltezeit) = (figuren[k].Seitlich, figuren[k].Seite, figuren[k].Bremse, figuren[k].Haltezeit);
                p.Weg += p.Tempo * p.Bremse * dt;
            }
            for (var a = 0; a < leute.Count; a++)
                for (var b = a + 1; b < leute.Count; b++)
                {
                    var (pa, pb) = (leute[a].Lage, leute[b].Lage);
                    var luft = MathF.Sqrt((pa.X - pb.X) * (pa.X - pb.X) + (pa.Z - pb.Z) * (pa.Z - pb.Z)) - leute[a].Radius - leute[b].Radius;
                    kleinster = MathF.Min(kleinster, luft);
                    if (luft < 0) ueber++;
                }
        }
        return (ueber, kleinster);
    }

    /// <summary>Leute, die zur selben Zeit auf die Mitte (0|0) zugehen, aus Richtungen im Abstand von <paramref name="schritt"/> Grad, 6 m davor.</summary>
    static List<Person> Stern(int anzahl, float schritt, float versatz = 0)
    {
        var leute = new List<Person>();
        for (var i = 0; i < anzahl; i++)
        {
            var w = (versatz + i * schritt) * MathF.PI / 180;
            var (fx, fz) = (-MathF.Cos(w), -MathF.Sin(w));
            leute.Add(new Person { X0 = MathF.Cos(w) * 6, Z0 = MathF.Sin(w) * 6, Fx = fx, Fz = fz, Nummer = 10 + i * 7 % 13, Tempo = 1.0f + 0.1f * (i % 4) });
        }
        return leute;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Marktausweichen: zwei kreuzen sich schräg (60°, 120°) über die Fläche, ohne sich zu überschneiden", () =>
        {
            foreach (var winkel in new[] { 60f, 120f })
            {
                var (ueber, kleinster) = Gehen(Stern(2, winkel), 10);
                Pruefe.Wahr(ueber == 0, $"{winkel}°: {ueber} Paar-Bilder mit Überschneidung (kleinster Abstand {kleinster:0.00} m)");
            }
        }),
        ("Marktausweichen: sechs von allen Seiten zugleich durch dieselbe Mitte – ohne Überschneidung, und keine bleibt stecken", () =>
        {
            var leute = Stern(6, 60, 7);
            var (ueber, kleinster) = Gehen(leute, 20);
            Pruefe.Wahr(ueber == 0, $"{ueber} Paar-Bilder mit Überschneidung (kleinster Abstand {kleinster:0.00} m)");
            Pruefe.Wahr(leute.All(p => p.Weg > 9), $"stecken geblieben: {string.Join(", ", leute.Select(p => p.Weg.ToString("0.0")))} m");
        }),
    };
}
