namespace Reichsgilde;

/// <summary>
/// Zufall je Elbenhaus für das Efeu (Nutzer 21.11.2026): aus der festen Platz-Saat des Hauses (<c>NeubauPlatzSaat</c>) wird
/// (Versatz, Dichte, Farbton) abgeleitet und als Instanz-Uniform <c>efeu_zufall</c> an die Teile des Hauses gegeben. Der Shader
/// (building_trim.gdshaderinc) blendet damit Bereiche der Blattkarten aus und verschiebt das Grün. Rein deterministisch, kein Godot.
/// </summary>
public static class EfeuZufall
{
    /// <summary>Dichte untere Grenze: Anteil des Raums, der bewachsen bleibt (mindestens etwa die Hälfte bewachsen, 0,95 = fast alles wie im Modell; Nutzer 22.11.2026, Efeu B).</summary>
    public const float DichteMin = 0.50f, DichteMax = 0.95f;

    /// <summary>Wert, der nichts ausblendet (Dichte über 1): Häuser ohne Zufall, Halblinge, Zeilen.</summary>
    public const float KeineAuswahl = 2.0f;

    private static uint Mische(uint h)
    {
        unchecked
        {
            h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
            return h;
        }
    }

    private static float Einheit(uint h) => (h >> 8) / 16777216f;

    /// <summary>Versatz (0..1, verschiebt das Rauschfeld), Dichte (DichteMin..DichteMax), Farbton (-1..1; Grün gelber bzw. bläulicher).</summary>
    public static (float Versatz, float Dichte, float Farbton) Ableiten(uint saat)
    {
        var versatz = Einheit(Mische(saat ^ 0xA511E9B3u));
        var dichte = DichteMin + (DichteMax - DichteMin) * Einheit(Mische(saat ^ 0x63D83595u));
        var farbton = Einheit(Mische(saat ^ 0x1B873593u)) * 2f - 1f;
        return (versatz, dichte, farbton);
    }
}
