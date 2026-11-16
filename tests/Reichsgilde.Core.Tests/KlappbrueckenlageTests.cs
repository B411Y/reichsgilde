using Reichsgilde.Core.Bauen;

namespace Reichsgilde.Core.Tests;

public static class KlappbrueckenlageTests
{
    public static IEnumerable<(string, Action)> Alle()
    {
        yield return ("Klappbrückenlage: Köln (64,1 m, Pfeiler bei −21,3 / −8,5 / 8,5 / 21,3 von der Mitte) – vier Felder zu 9,8 m, Zwischenpfeiler bei 10,75 und 53,35 m", () =>
        {
            const double weite = 64.1, m = 32.05;
            var l = Klappbrueckenlage.Aus(weite, new[] { m - 21.3, m - 8.5, m + 8.5, m + 21.3 }, 13, 4, 2);
            Pruefe.Wahr(l is not null, "die Pfeiler des Wasserplans passen");
            Pruefe.Wahr(l!.Felder.Count == 4 && l.Zwischenpfeiler.Count == 2, "vier Felder, zwei Zwischenpfeiler");
            Pruefe.Wahr(Math.Abs(l.Mitte - m) < 1e-9, "das Mittelteil liegt in der Mitte");
            Pruefe.Wahr(l.Felder.All(f => Math.Abs(f.Laenge - 9.775) < 0.03), "alle Felder rund 9,8 m (die Pfeiler des Wasserplans sind auf 0,1 m gerundet)");
            Pruefe.Wahr(Math.Abs(l.Felder[0].Mitte - 4.875) < 1e-9 && Math.Abs(l.Felder[1].Mitte - 16.65) < 1e-9, "Feldmitten Seite A");
            // Gegenprobe: die Felder füllen die Brücke außerhalb von Mittelteil (21 m) und Zwischenpfeilern (2 × 2 m je Seite … zwei Pfeiler gesamt) ohne Rest
            Pruefe.Wahr(Math.Abs(l.Felder.Sum(f => f.Laenge) + 2 * 2 + 21 - weite) < 1e-9, "Felder + Zwischenpfeiler + Mittelteil = Weite");
        });
        yield return ("Klappbrückenlage: ein Feld je Seite ohne Zwischenpfeiler; falsche Pfeiler ergeben null", () =>
        {
            var eins = Klappbrueckenlage.Aus(40, new[] { 20 - 8.5, 20 + 8.5 }, 13, 4, 2);
            Pruefe.Wahr(eins is not null && eins.Felder.Count == 2 && eins.Zwischenpfeiler.Count == 0 && Math.Abs(eins.Felder[0].Laenge - 9.5) < 1e-9, "ein Feld je Seite zu 9,5 m");
            Pruefe.Wahr(Klappbrueckenlage.Aus(40, new[] { 12.0, 30.0 }, 13, 4, 2) is null, "Torhauspfeiler im falschen Abstand");
            Pruefe.Wahr(Klappbrueckenlage.Aus(40, new[] { 20.0 }, 13, 4, 2) is null, "ein einzelner Pfeiler");
            Pruefe.Wahr(Klappbrueckenlage.Aus(40, new[] { 5.0, 11.5, 28.5 }, 13, 4, 2) is null, "ungerade Zahl");
            Pruefe.Wahr(Klappbrueckenlage.Aus(21.8, new[] { 10.9 - 8.5, 10.9 + 8.5 }, 13, 4, 2) is null, "zu kurz: Felder ohne Länge");
            Pruefe.Wahr(Klappbrueckenlage.Aus(40, new[] { 11.5, 28.5 }, 0, 4, 2) is null, "ohne Durchfahrt keine Klappbrücke");
        });
        yield return ("Klappbrücke Ablauf: Offenheit der Klappen – fahren mit der Öffnung auf und bis zum Ende zu; zu kurze Öffnungen öffnen nur halb; außerhalb zu", () =>
        {
            // Öffnung 100 bis 120, eine Fahrt dauert 6 Minuten
            Pruefe.Ungefaehr(0.0, Klappbrueckenlage.Offenheit(99.9, 100, 120, 6), 1e-6);
            Pruefe.Ungefaehr(0.5, Klappbrueckenlage.Offenheit(103, 100, 120, 6), 1e-6);
            Pruefe.Ungefaehr(1.0, Klappbrueckenlage.Offenheit(110, 100, 120, 6), 1e-6);
            Pruefe.Ungefaehr(0.5, Klappbrueckenlage.Offenheit(117, 100, 120, 6), 1e-6);
            Pruefe.Ungefaehr(0.0, Klappbrueckenlage.Offenheit(120, 100, 120, 6), 1e-6);   // bei Bis wieder zu
            Pruefe.Ungefaehr(0.5, Klappbrueckenlage.Offenheit(105, 100, 110, 10), 1e-6);  // Öffnung kürzer als zwei Fahrten: nur halb auf
            // Gegenprobe: Auf- und Zufahrt sind spiegelbildlich (Schließen = Öffnen rückwärts)
            for (var m = 100.0; m < 120; m += 0.5)
                Pruefe.Wahr(Math.Abs(Klappbrueckenlage.Offenheit(m, 100, 120, 6) - Klappbrueckenlage.Offenheit(220 - m - 0.0001, 100, 120, 6)) < 1e-3, $"Minute {m}: Auf- und Zufahrt spiegelbildlich");
        });
        yield return ("Klappbrücke Schranken: der Baum ist zu, bevor die Klappen aufgehen, bleibt zu, bis sie wieder zu sind, und geht danach hoch; Gegenprobe außerhalb und Spiegelbild", () =>
        {
            // Öffnung 100 bis 120, Baum braucht 2 Minuten, Klappen 6
            Pruefe.Ungefaehr(0.0, Klappbrueckenlage.Schrankenstand(97.9, 100, 120, 2), 1e-6);
            Pruefe.Ungefaehr(0.5, Klappbrueckenlage.Schrankenstand(99, 100, 120, 2), 1e-6);
            Pruefe.Ungefaehr(1.0, Klappbrueckenlage.Schrankenstand(100, 100, 120, 2), 1e-6);
            Pruefe.Ungefaehr(1.0, Klappbrueckenlage.Schrankenstand(120, 100, 120, 2), 1e-6);
            Pruefe.Ungefaehr(0.5, Klappbrueckenlage.Schrankenstand(121, 100, 120, 2), 1e-6);
            Pruefe.Wahr(Klappbrueckenlage.Schrankenstand(122.5, 100, 120, 2) == 0f, "danach oben");
            // Zuordnung: Sind die Klappen auch nur ein bisschen offen, ist der Baum ganz zu (nie ein Karren in die offene Lücke)
            for (var m = 90.0; m < 130; m += 0.25)
                if (Klappbrueckenlage.Offenheit(m, 100, 120, 6) > 0)
                    Pruefe.Wahr(Klappbrueckenlage.Schrankenstand(m, 100, 120, 2) >= 0.999f, $"Minute {m}: Klappen offen, Baum nicht zu");
            // Gegenprobe: ohne Öffnung (Winter) bleibt der Baum oben; eine zu kurze Öffnung schließt ihn trotzdem ganz
            Pruefe.Wahr(Enumerable.Range(0, 200).All(m => Klappbrueckenlage.Schrankenstand(m, 1e9, 1e9 + 20, 2) == 0f), "keine Öffnung, kein geschlossener Baum");
            Pruefe.Ungefaehr(1.0, Klappbrueckenlage.Schrankenstand(104, 100, 108, 2), 1e-6);
        });
    }
}
