using Reichsgilde;

/// <summary>Steinbänder der Dom-Blätter (<c>game/scripts/SteinMip.cs</c>, Bänder wie in <c>tools/modelle/neubau/dom_trimsheet.py</c>).</summary>
static class DomSteinMipTests
{
    public static IEnumerable<(string, Action)> Alle()
    {
        yield return ("Dom außen: Werkstein und Sandstein lesen im Pixel-Look den kleinen Mip-Zuschlag", () =>
        {
            var d = SteinMip.Von("dom_trim");
            Pruefe.Gleich(1f, SteinMip.Zuschlag(0.50f, d, true));       // grauer Werkstein (0,35–0,65)
            Pruefe.Gleich(1f, SteinMip.Zuschlag(0.70f, d, true));       // Sandstein (0,65–0,80)
            Pruefe.Gleich(2.5f, SteinMip.Zuschlag(0.10f, d, true));     // Gegenprobe Schiefer
            Pruefe.Gleich(2.5f, SteinMip.Zuschlag(0.30f, d, true));     // Gegenprobe Holz
            Pruefe.Gleich(2.5f, SteinMip.Zuschlag(0.85f, d, true));     // Gegenprobe glatter Werkstein
            Pruefe.Gleich(0f, SteinMip.Zuschlag(0.50f, d, false));      // ohne Pixel-Look keine Verschiebung
        });
        yield return ("Dom innen: Werkstein und Sandstein sind Steinbänder, Boden und Holz nicht", () =>
        {
            var d = SteinMip.Von("dom_innen_trim");
            Pruefe.Gleich(1f, SteinMip.Zuschlag(0.30f, d, true));
            Pruefe.Gleich(1f, SteinMip.Zuschlag(0.50f, d, true));
            Pruefe.Gleich(2.5f, SteinMip.Zuschlag(0.05f, d, true));     // Schachbrettboden
            Pruefe.Gleich(2.5f, SteinMip.Zuschlag(0.80f, d, true));     // Eiche
        });
    }
}
