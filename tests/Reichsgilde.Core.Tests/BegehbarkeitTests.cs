using Reichsgilde;

/// <summary>
/// Sperrflächen der Passanten (<c>game/scripts/Begehbarkeit.cs</c>, hier mitübersetzt; Nutzer, 14.11.2026): Abstand zur Wand, Zusammenrücken der
/// seitlichen Lage bis auf 0,1 m zwischen Körper und Wand (nie hinein), Prüfung von Strecken, Anfragen im Gitter.
/// </summary>
static class BegehbarkeitTests
{
    /// <summary>Eine Hauswand: Rechteck 10 × 6 m, Mitte im Ursprung, Kanten parallel zu den Achsen (Wand an z = 3).</summary>
    static Begehbarkeit Haus()
    {
        var b = new Begehbarkeit();
        b.Hinzu(0, 0, 1, 0, 0, 1, 5, 3);
        return b;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Begehbarkeit: Abstand zur Wand, negativ im Haus, 8 m weit weg", () =>
        {
            var b = Haus();
            Pruefe.Ungefaehr(2, b.Abstand(0, 5), 1e-4);
            Pruefe.Ungefaehr(-3, b.Abstand(0, 0), 1e-4);
            Pruefe.Ungefaehr(Math.Sqrt(2), b.Abstand(6, 4), 1e-4); // an der Ecke
            Pruefe.Gleich(8f, b.Abstand(200, 200));
        }),
        ("Begehbarkeit: ein gedrehtes Haus (45 Grad) wird richtig gemessen", () =>
        {
            var b = new Begehbarkeit();
            var w = (float)Math.Sqrt(0.5);
            b.Hinzu(0, 0, w, w, -w, w, 2, 2); // Quadrat 4 × 4, um 45 Grad gedreht: Ecken bei (±2,83, 0) und (0, ±2,83)
            Pruefe.Ungefaehr(1, b.Abstand(3.83f, 0), 0.01);
            Pruefe.Wahr(b.Abstand(2, 2) > 0.2f, "die Ecke des Rechtecks ist leer"); // außerhalb der gedrehten Raute
        }),
        ("Begehbarkeit: Zusammenrücken – die Figur bleibt mindestens 0,1 m (Körper 0,22 m) von der Wand, nie darin", () =>
        {
            var b = Haus();
            // Mittellinie 1 m vor der Wand, die Spur drückt 1 m zur Wand hin
            var anteil = b.Anteil(0, 4, 0, 3.0f);
            var z = 4 + (3.0f - 4) * anteil;
            Pruefe.Wahr(z >= 3 + Begehbarkeit.Luft - 0.02f, $"Abstand zur Wand {z - 3:0.000} m, verlangt mindestens {Begehbarkeit.Luft}");
            Pruefe.Wahr(z <= 3 + Begehbarkeit.Luft + 0.05f, "sie rückt nicht weiter weg als nötig");
            // seitlich frei: nichts wird gekürzt
            Pruefe.Gleich(1f, b.Anteil(0, 6, 0.5f, 6));
        }),
        ("Begehbarkeit: liegt die Mittellinie selbst an der Wand (Tür), bleibt die Lage unverändert", () =>
        {
            var b = Haus();
            Pruefe.Gleich(1f, b.Anteil(0, 3.1f, 0.4f, 3.1f));
        }),
        ("Begehbarkeit: eine Strecke durch das Haus ist verletzt, eine daran vorbei nicht; Türenden (2,5 m) zählen nicht", () =>
        {
            var b = Haus();
            Pruefe.Wahr(b.Verletzt(-8, 0, 8, 0, (-8, 0), (8, 0), out _, out _) > 5, "die Gerade durchs Haus: viele Meter drin");
            Pruefe.Gleich(0f, b.Verletzt(-8, 6, 8, 6, (-8, 6), (8, 6), out _, out _));
            // Tür: die letzten 2,5 m vor der Wand liegen im Haus und zählen nicht
            Pruefe.Gleich(0f, b.Verletzt(0, 6, 0, 2.6f, (0, 6), (0, 2.6f), out _, out _));
            var tief = b.Verletzt(0, 6, 0, 0, (0, 6), (0, 0), out _, out var erstesZ);
            Pruefe.Wahr(tief > 0.5f && erstesZ < 3.3f, "ins Haus hinein gezählt, erste Stelle an der Wand");
        }),
        ("Begehbarkeit: ein Kreis (Baum) wird gemessen; ein Durchgang macht Mauerteile frei, andere Häuser nicht", () =>
        {
            var b = new Begehbarkeit();
            b.HinzuRund(10, 10, 1);
            Pruefe.Ungefaehr(2, b.Abstand(13, 10), 1e-4);
            Pruefe.Ungefaehr(-1, b.Abstand(10, 10), 1e-4);
            b.Hinzu(0, 0, 1, 0, 0, 1, 5, 1, "Mauerstück", false, true); // Mauer von x −5 bis 5 an z 0
            b.Hinzu(0, 20, 1, 0, 0, 1, 5, 1, "Haus"); // ein Haus dahinter, nicht Mauer
            Pruefe.Wahr(b.Abstand(0, 0) < 0, "ohne Durchgang liegt die Mitte in der Mauer");
            b.Durchgang(0, 0, 0, 1, 1.6f, 9f); // Gang in z-Richtung
            Pruefe.Wahr(b.Abstand(0, 0) > 1, "im Durchgang zählt die Mauer nicht");
            Pruefe.Wahr(b.Abstand(4, 0) < 0, "neben dem Durchgang bleibt sie ein Hindernis");
            Pruefe.Wahr(b.Abstand(0, 20) < 0, "Häuser bleiben Hindernisse");
        }),
        ("Begehbarkeit: viele Häuser – eine Anfrage bleibt billig (Gitter) und richtig", () =>
        {
            var b = new Begehbarkeit();
            for (var i = 0; i < 400; i++) b.Hinzu((i % 20) * 30, (i / 20) * 30, 1, 0, 0, 1, 5, 4);
            Pruefe.Gleich(400, b.Anzahl);
            Pruefe.Ungefaehr(-4, b.Abstand(30 * 7, 30 * 3), 1e-4);
            Pruefe.Ungefaehr(11, 11 + b.Abstand(15, 15) - b.Abstand(15, 15), 1e-4);
            var uhr = System.Diagnostics.Stopwatch.StartNew();
            var summe = 0f;
            for (var i = 0; i < 100000; i++) summe += b.Abstand(i % 600, (i / 600) % 600);
            Pruefe.Wahr(uhr.ElapsedMilliseconds < 500, $"100 000 Anfragen in {uhr.ElapsedMilliseconds} ms (Summe {summe})");
        }),
    };
}
