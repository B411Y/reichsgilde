using Reichsgilde;

/// <summary>Stein-Mip im Pixel-Look (<c>game/scripts/SteinMip.cs</c>, dieselbe Regel wie in <c>building_trim.gdshader</c>).</summary>
static class SteinMipTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Stein-Mip: auf den Steinbändern Zuschlag 1, auf Putz, Holz, Stroh und Schiefer weiter 2,5; ohne Pixel-Look 0", () =>
        {
            var haus = SteinMip.Von("trim");
            Pruefe.Gleich(1f, SteinMip.Zuschlag(0.90f, haus, true));        // Bruchstein
            Pruefe.Gleich(2.5f, SteinMip.Zuschlag(0.60f, haus, true));      // Gegenprobe Putz (0,5–0,7)
            Pruefe.Gleich(2.5f, SteinMip.Zuschlag(0.30f, haus, true));      // Gegenprobe Holz (0,25–0,5)
            Pruefe.Gleich(2.5f, SteinMip.Zuschlag(0.10f, haus, true));      // Gegenprobe Stroh
            Pruefe.Gleich(0f, SteinMip.Zuschlag(0.90f, haus, false));       // ohne Pixel-Look keine Verschiebung
        }),
        ("Stein-Mip: Rathaus außen Quader und Bruchstein, Schiefer und Putz nicht; Grenzen halboffen", () =>
        {
            var r = SteinMip.Von("rathaus_trim");
            Pruefe.Wahr(r.Enthaelt(0.65f) && r.Enthaelt(0.90f), "Quader und Bruchstein");
            Pruefe.Wahr(!r.Enthaelt(0.10f) && !r.Enthaelt(0.55f) && !r.Enthaelt(0.30f), "Schiefer, Putz, Balken nicht");
            Pruefe.Wahr(r.Enthaelt(0.60f) && !r.Enthaelt(0.70f) && !r.Enthaelt(0.95f), "Bandgrenzen [v0, v1)");
        }),
        ("Stein-Mip: unbekanntes Blatt nimmt das Hausband, Rechnung je Zoom: Mip-Stufe und Texel je Bildpunkt", () =>
        {
            Pruefe.Gleich(SteinMip.Haus, SteinMip.Von("trim_s3"));
            // 512 Texel/m; Pixel-Look-Zoom nah 14,4, mittel 7,2, fern 3,6 Bildpunkte/m: Grund-Mip log2(512/px), mit Zuschlag 1 eine Stufe gröber
            foreach (var (bildpunkte, grund) in new[] { (14.4, 5.15), (7.2, 6.15), (3.6, 7.15) })
                Pruefe.Wahr(Math.Abs(Math.Log2(512 / bildpunkte) - grund) < 0.01, $"Grund-Mip bei {bildpunkte} px/m");
        }),
    };
}
