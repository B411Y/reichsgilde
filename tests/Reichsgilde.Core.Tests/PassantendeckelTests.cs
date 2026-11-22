using Reichsgilde;

/// <summary>
/// Passantendeckel (Nutzer 22.11.2026): Dichte nach Zoom, stabile Teilmenge über die Nummer, Deckel 400, Skelett höchstens 60.
/// Die Figuren und das Bild setzt die Stadtansicht (Stadtansicht.Passanten.cs); hier die reine Rechnung.
/// </summary>
static class PassantendeckelTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Passantendeckel: Dichte 100 % bei Zoom 25, 60 % bei 50, 30 % bei 100, dazwischen linear, außerhalb gleich dem Rand", () =>
        {
            Pruefe.Wahr(Passantendeckel.ZoomProzent(25f) == 100, "Zoom 25");
            Pruefe.Wahr(Passantendeckel.ZoomProzent(10f) == 100, "näher als 25 bleibt 100");
            Pruefe.Wahr(Passantendeckel.ZoomProzent(50f) == 60, "Zoom 50");
            Pruefe.Wahr(Passantendeckel.ZoomProzent(100f) == 30, "Zoom 100");
            Pruefe.Wahr(Passantendeckel.ZoomProzent(150f) == 30, "weiter als 100 bleibt 30");
            Pruefe.Wahr(Passantendeckel.ZoomProzent(37.5f) == 80, $"Mitte zwischen 25 und 50: {Passantendeckel.ZoomProzent(37.5f)}");
            Pruefe.Wahr(Passantendeckel.ZoomProzent(75f) == 45, $"Mitte zwischen 50 und 100: {Passantendeckel.ZoomProzent(75f)}");
            var vorher = 101;
            for (var z = 25f; z <= 100f; z += 1f)
            {
                var p = Passantendeckel.ZoomProzent(z);
                Pruefe.Wahr(p <= vorher, $"nie dichter beim Herauszoomen (Zoom {z})");
                vorher = p;
            }
        }),

        ("Passantendeckel: Der Grafikregler wirkt zusätzlich (der kleinere Anteil gilt)", () =>
        {
            Pruefe.Wahr(Passantendeckel.Promille(25f, 100) == 1000, "alles");
            Pruefe.Wahr(Passantendeckel.Promille(25f, 40) == 400, "Regler 40 % bei Zoom 25");
            Pruefe.Wahr(Passantendeckel.Promille(100f, 100) == 300, "Zoom 100 mit Regler 100");
            Pruefe.Wahr(Passantendeckel.Promille(100f, 25) == 250, "Regler kleiner als die Zoomdichte");
        }),

        ("Passantendeckel: Die Teilmenge ist stabil und nested – bei kleinerer Schwelle bleibt jede gezeigte Figur gezeigt oder fällt weg, nie umgekehrt", () =>
        {
            for (var n = 0; n < 5000; n++)
            {
                var r = Passantendeckel.Rang(n);
                Pruefe.Wahr(r is >= 0 and < 1000, $"Rang im Bereich ({n}: {r})");
                Pruefe.Wahr(r == Passantendeckel.Rang(n), "dieselbe Nummer, derselbe Rang");
                if (Passantendeckel.Gezeigt(n, 300)) Pruefe.Wahr(Passantendeckel.Gezeigt(n, 600), $"Figur {n}: 30 % gezeigt heißt auch bei 60 % gezeigt");
            }
        }),

        ("Passantendeckel: Der Anteil trifft die Dichte (über viele Nummern)", () =>
        {
            foreach (var schwelle in new[] { 300, 600, 1000 })
            {
                var gezeigt = Enumerable.Range(0, 4000).Count(n => Passantendeckel.Gezeigt(n, schwelle));
                var soll = 4000 * schwelle / 1000;
                Pruefe.Wahr(Math.Abs(gezeigt - soll) < 4000 * 0.04, $"Schwelle {schwelle}: {gezeigt} statt etwa {soll}");
            }
        }),

        ("Passantendeckel: Unter dem Deckel bleibt die Schwelle, wie sie gewollt ist", () =>
        {
            var imBild = new int[1000];
            for (var n = 0; n < 200; n++) imBild[Passantendeckel.Rang(n)]++; // 200 im Bild, weit unter 400
            Pruefe.Wahr(Passantendeckel.Schwelle(1000, 1000, imBild) == 1000, "nichts zu kürzen");
            Pruefe.Wahr(Passantendeckel.Schwelle(1000, 300, imBild) == 300, "Zoom und Regler kürzen sofort");
        }),

        ("Passantendeckel: Über dem Deckel sinkt die Schwelle sofort auf die höchste, bei der es noch reicht", () =>
        {
            var imBild = new int[1000];
            for (var n = 0; n < 1000; n++) imBild[Passantendeckel.Rang(n)]++; // 1000 Figuren im Bild
            var t = Passantendeckel.Schwelle(1000, 1000, imBild);
            var darunter = imBild.Take(t).Sum();
            Pruefe.Wahr(darunter <= Passantendeckel.Deckel, $"höchstens der Deckel darunter: {darunter}");
            Pruefe.Wahr(t < 1000 && t > 0, $"Schwelle gesenkt: {t}");
            Pruefe.Wahr(imBild.Take(t + 1).Sum() > Passantendeckel.Deckel, "und die nächsthöhere Schwelle wäre zu viel");
            // gleiche Eingabe, gleiches Ergebnis (nichts flackert)
            Pruefe.Wahr(Passantendeckel.Schwelle(1000, 1000, imBild) == t, "stabil");
        }),

        ("Passantendeckel: Ist wieder Platz, wächst die Schwelle schrittweise, nie über den Wunsch", () =>
        {
            var imBild = new int[1000];
            for (var n = 0; n < 100; n++) imBild[Passantendeckel.Rang(n)]++;
            var t = 100;
            t = Passantendeckel.Schwelle(t, 1000, imBild);
            Pruefe.Wahr(t == 100 + Passantendeckel.Wachstum, $"ein Schritt: {t}");
            for (var i = 0; i < 100; i++) t = Passantendeckel.Schwelle(t, 1000, imBild);
            Pruefe.Wahr(t == 1000, $"am Ende der Wunsch: {t}");
            Pruefe.Wahr(Passantendeckel.Schwelle(t, 500, imBild) == 500, "sinkt der Wunsch, folgt sie sofort");
        }),

        ("Passantendeckel: Skelett höchstens 60 – darüber gebacken, zurück erst mit Spielraum", () =>
        {
            Pruefe.Wahr(!Passantendeckel.Gebacken(false, false, 60), "60 im Bild: noch Skelett");
            Pruefe.Wahr(Passantendeckel.Gebacken(false, false, 61), "61 im Bild: gebacken");
            Pruefe.Wahr(Passantendeckel.Gebacken(false, true, 60), "bleibt gebacken, solange mehr als 45");
            Pruefe.Wahr(Passantendeckel.Gebacken(false, true, 46), "46: noch gebacken");
            Pruefe.Wahr(!Passantendeckel.Gebacken(false, true, 45), "45: zurück zum Skelett");
            Pruefe.Wahr(Passantendeckel.Gebacken(true, false, 0), "ab der mittleren Zoomstufe immer gebacken");
        }),
    };
}
