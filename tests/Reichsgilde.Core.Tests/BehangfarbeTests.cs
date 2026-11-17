using Reichsgilde;
using Reichsgilde.Core.Partie;

/// <summary>Wandbehang der Steinhäuser: jede Hausfarbe der Palette hat ihren gedämpften Behangton, andere Farben nehmen den nächsten.</summary>
static class BehangfarbeTests
{
    static float Saettigung((float R, float G, float B) f)
    {
        var max = MathF.Max(f.R, MathF.Max(f.G, f.B));
        var min = MathF.Min(f.R, MathF.Min(f.G, f.B));
        return max <= 0 ? 0 : (max - min) / max;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Behangfarbe: jede Hausfarbe der Spiel-Palette steht in der Tabelle, in derselben Reihenfolge", () =>
        {
            Pruefe.Gleich(Partiebesetzung.Hausfarben.Count, Behangfarbe.Tabelle.Length);
            for (var i = 0; i < Behangfarbe.Tabelle.Length; i++)
                Pruefe.Gleich(Partiebesetzung.Hausfarben[i], Behangfarbe.Tabelle[i].Hausfarbe);
        }),
        ("Behangfarbe: der Behang ist gedämpft (dunkler und nicht kräftiger als die Hausfarbe), der Saum noch dunkler", () =>
        {
            foreach (var e in Behangfarbe.Tabelle)
            {
                var haus = Behangfarbe.Lesen(e.Hausfarbe);
                var behang = Behangfarbe.Lesen(e.Behang);
                Pruefe.Wahr(behang.R + behang.G + behang.B < haus.R + haus.G + haus.B, $"{e.Name} dunkler als {e.Hausfarbe}");
                Pruefe.Wahr(Saettigung(behang) <= Saettigung(haus) + 1e-4f, $"{e.Name} nicht kräftiger als {e.Hausfarbe}");
                var saum = Behangfarbe.Saum(haus.R, haus.G, haus.B);
                Pruefe.Wahr(saum.R < behang.R + 1e-6f && saum.G < behang.G + 1e-6f && saum.B < behang.B + 1e-6f, $"Saum von {e.Name} dunkler");
            }
        }),
        ("Behangfarbe: Rot wird Krapprot, Blau Waidblau, Gelb Resedagelb, Braun Walnussbraun; andere Farben nehmen die nächste", () =>
        {
            Pruefe.Gleich("Krapprot", Behangfarbe.Zu(0.70f, 0.20f, 0.16f).Name);       // Rivale ohne Farbe in der Karte (Stadtansicht.Rivale)
            Pruefe.Gleich("Waidblau", Behangfarbe.Zu(0.20f, 0.42f, 0.72f).Name);       // Spieler ohne Farbe in der Karte
            Pruefe.Gleich("Resedagelb", Behangfarbe.Zu(0.85f, 0.68f, 0.18f).Name);
            Pruefe.Gleich("Walnussbraun", Behangfarbe.Zu(0.50f, 0.36f, 0.20f).Name);
            Pruefe.Gleich("Eisengrau", Behangfarbe.Zu(0.36f, 0.31f, 0.25f).Name);      // Haus ohne Besitzer (verwittertes Holz): am nächsten am Grau
            var (r, g, b) = Behangfarbe.Lesen("#7a4a9e");                                // Purpur der Kartenwerkstatt
            Pruefe.Gleich("Orseillepurpur", Behangfarbe.Zu(r, g, b).Name);
            Pruefe.Gleich(Behangfarbe.Lesen("#8e2a24"), Behangfarbe.Behang(0.70f, 0.20f, 0.16f));
        }),
    };
}
