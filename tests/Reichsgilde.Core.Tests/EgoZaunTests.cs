using Reichsgilde;


/// <summary>Zäune der Umlandstellen als Kisten für die Ego-Perspektive: Zaun blockiert, Tor und Lücke bleiben offen (<c>game/scripts/EgoZaun.cs</c>).</summary>
public static class EgoZaunTests
{
    // Die Weide aus dem Baukasten (Spielkoordinaten x, z): 14 x 10 m, Tor vorn (z = +5) in der Mitte, 2 m breit
    private static readonly (float X, float Z)[] Weide = { (-7, 5), (-7, -5), (7, -5), (7, 5) };
    private static readonly (float X, float Z, float Breite)[] Tor = { (0f, 5f, 2f) };

    private static bool Getroffen(IEnumerable<EgoZaunKiste> kisten, float x, float z) => kisten.Any(k => k.Enthaelt(x, z));

    public static IEnumerable<(string, Action)> Alle()
    {
        yield return ("EgoZaun: ohne Lücke vier Seiten, Zaun trifft an der Mitte jeder Seite", () =>
        {
            var k = EgoZaun.Kisten(Weide, 0.12f, geschlossen: true);
            Gleich(4, k.Count);
            foreach (var (x, z) in new[] { (0f, 5f), (0f, -5f), (7f, 0f), (-7f, 0f) }) Wahr(Getroffen(k, x, z), $"Zaun bei {x},{z}");
            Wahr(!Getroffen(k, 0f, 0f), "Mitte der Koppel ist frei");
        });
        yield return ("EgoZaun: das Tor schneidet eine Lücke, der Zaun daneben bleibt", () =>
        {
            var k = EgoZaun.Kisten(Weide, 0.12f, geschlossen: true, Tor);
            Gleich(5, k.Count); // die Torseite in zwei Stücken
            foreach (var x in new[] { -0.9f, 0f, 0.9f }) Wahr(!Getroffen(k, x, 5f), $"Tor offen bei x={x}");
            foreach (var x in new[] { -1.2f, 1.2f, -5f, 5f }) Wahr(Getroffen(k, x, 5f), $"Zaun daneben bei x={x}");
            // die Lücke ist breit genug für die Figur (Halbmesser 0,45 m): von den Stücken bleiben 2 m minus nichts
            var links = k.Where(s => s.MitteX < 0 && Math.Abs(s.MitteZ - 5f) < 0.2f).Single();
            Wahr(links.MitteX + links.HalbLaenge <= -0.99f, "Stück links endet am Tor");
        });
        yield return ("EgoZaun: eine Lücke mitten in einer Seite teilt sie, eine weit entfernte ändert nichts", () =>
        {
            var gerade = new[] { (0f, 0f), (10f, 0f) };
            var mit = EgoZaun.Kisten(gerade, 0.1f, luecken: new[] { (5f, 0f, 2f) });
            Gleich(2, mit.Count);
            Wahr(!Getroffen(mit, 5f, 0f) && Getroffen(mit, 3f, 0f) && Getroffen(mit, 7f, 0f), "Lücke bei 5, Zaun bei 3 und 7");
            var fern = EgoZaun.Kisten(gerade, 0.1f, luecken: new[] { (5f, 8f, 2f) });
            Gleich(1, fern.Count);
        });
        yield return ("EgoZaun: Ecken bleiben dicht, Linienzug ohne Schluss lässt das offene Ende", () =>
        {
            var k = EgoZaun.Kisten(Weide, 0.12f, geschlossen: true);
            Wahr(Getroffen(k, -7.1f, 5.1f), "Eckpfosten hinten links");
            var offen = EgoZaun.Kisten(Weide, 0.12f, geschlossen: false);
            Gleich(3, offen.Count);
            Wahr(!Getroffen(offen, 0f, 5f), "offene Torseite");
        });
        yield return ("EgoZaun: Punkte ohne Länge und ein einzelner Punkt geben keine Kisten", () =>
        {
            Gleich(0, EgoZaun.Kisten(new[] { (1f, 1f), (1f, 1f) }, 0.1f).Count);
            Gleich(0, EgoZaun.Kisten(new[] { (1f, 1f) }, 0.1f).Count);
        });
    }

    private static void Wahr(bool b, string was) { if (!b) throw new Exception("Fehlgeschlagen: " + was); }
    private static void Gleich(int soll, int ist) { if (soll != ist) throw new Exception($"erwartet {soll}, war {ist}"); }
}
