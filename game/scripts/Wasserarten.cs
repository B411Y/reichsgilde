using System;
using Godot;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>Wann ein Gewässer zufriert: nie, ganz (kleine Gewässer) oder nur am Rand.</summary>
public enum Eisregel { Nie, Voll, Rand }

/// <summary>
/// Eine Gewässerart als Parametersatz für <c>wasser_realistisch.gdshader</c> (Phase 2 der Wasser-Arbeit, <c>docs/wasser/phase2.md</c>).
/// Alle Zahlen stehen an dieser einen Stelle (<see cref="Wasserarten"/>); der Shader kennt keine Arten, nur Uniforms.
/// </summary>
/// <param name="WellenStaerke">Faktor auf die Steilheit der drei Wellen (0 = glatt).</param>
/// <param name="WellenLaenge">Faktor auf die Wellenlänge (Meer lang, Teich kurz).</param>
/// <param name="WindAnteil">Wie stark der Weltwind Wellen, Richtung und Kräuselung beeinflusst (0–1).</param>
/// <param name="Stroemung">Tempo in m/s in Laufrichtung; 0 = stehendes Wasser.</param>
/// <param name="Klarheit">Wert für <c>beers_law</c>: klein = klar (man sieht den Grund tief), groß = schnell tief gefärbt.</param>
/// <param name="SchaumStaerke">0 = kein Schaum.</param>
/// <param name="SchaumBreite">Breite des Saums in Metern.</param>
/// <param name="SchaumKamm">Schaumkronen auf den Wellen bei Wind (Meer).</param>
/// <param name="Vielfalt">0–1: wie stark die Stärke der Wellenzüge ortsabhängig schwankt (Gruppen großer Wellen, ruhige Stellen); 0 = überall gleich.</param>
/// <param name="ExtraWellen">0–1: Stärke der zwei zusätzlichen Wellenzüge (länger und kürzer als die drei Grundwellen).</param>
/// <param name="Kaustik">Stärke der hellen Lichtmuster auf dem Grund.</param>
/// <param name="EisReichweite">Meter vom Ufer, die das Eis im tiefsten Winter erreicht; 0 = friert nie. Bei <see cref="Eisregel.Voll"/> und
/// <see cref="Eisregel.Rand"/> setzt <see cref="Wasserarten.Fuer"/> sie aus der Größe des Gewässers.</param>
public sealed record Wasserart(
    string Name, float WellenStaerke, float WellenLaenge, float WindAnteil, float Stroemung,
    Color Flach, Color Tief, float Klarheit, float Brechung, float Rauheit,
    float SchaumStaerke, float SchaumBreite, float SchaumKamm, float Kaustik, Eisregel Eis, float EisReichweite = 0f, float Vielfalt = 0f, float ExtraWellen = 0f);

/// <summary>Die Gewässerarten, ihre Zuordnung zum Kartenwasser und der Winterfortschritt des Eises (alles nur Darstellung).</summary>
public static class Wasserarten
{
    /// <summary>Flüsse ab dieser Breite (m) haben Uferschaum; schmalere keinen.</summary>
    public const float SchaumAbBreite = 30f;
    /// <summary>Bäche und Flüsse bis zu dieser Breite (m) heißen Bach und frieren ganz zu; breitere Flüsse nie.</summary>
    public const float BachBisBreite = 5f;
    /// <summary>Teiche und Tümpel bis zu dieser Fläche (m²) frieren ganz zu; größere Seen nur am Rand (3–6 m).</summary>
    public const float TeichBisFlaeche = 2000f;
    public const float RandEisMin = 3f, RandEisMax = 6f;

    public static readonly Wasserart Fluss = new("Fluss", 0.25f, 1.6f, 0.4f, 1.2f, new(0.40f, 0.45f, 0.32f), new(0.10f, 0.19f, 0.21f), 1.3f, 0.06f, 0.22f,
        SchaumStaerke: 0.9f, SchaumBreite: 0.9f, SchaumKamm: 0f, Kaustik: 0.3f, Eisregel.Nie);
    public static readonly Wasserart Bach = new("Bach", 0.14f, 0.5f, 0.1f, 0.8f, new(0.55f, 0.62f, 0.52f), new(0.22f, 0.36f, 0.36f), 0.9f, 0.05f, 0.18f,
        SchaumStaerke: 0f, SchaumBreite: 0f, SchaumKamm: 0f, Kaustik: 0.8f, Eisregel.Voll);
    public static readonly Wasserart Teich = new("Teich", 0.12f, 0.5f, 0.3f, 0f, new(0.35f, 0.42f, 0.30f), new(0.12f, 0.22f, 0.20f), 1.6f, 0.05f, 0.18f,
        SchaumStaerke: 0f, SchaumBreite: 0f, SchaumKamm: 0f, Kaustik: 0.6f, Eisregel.Voll);
    public static readonly Wasserart See = new("See", 0.38f, 1.3f, 0.7f, 0f, new(0.48f, 0.60f, 0.58f), new(0.12f, 0.27f, 0.37f), 1.4f, 0.07f, 0.2f,
        SchaumStaerke: 0f, SchaumBreite: 0f, SchaumKamm: 0f, Kaustik: 0.6f, Eisregel.Rand, Vielfalt: 0.6f, ExtraWellen: 0.5f);
    public static readonly Wasserart Meer = new("Meer", 1.0f, 3.5f, 1.0f, 0f, new(0.44f, 0.60f, 0.60f), new(0.05f, 0.17f, 0.29f), 0.8f, 0.08f, 0.2f,
        SchaumStaerke: 0.8f, SchaumBreite: 1.2f, SchaumKamm: 0.35f, Kaustik: 0.5f, Eisregel.Nie, Vielfalt: 1f, ExtraWellen: 1f);
    public static readonly Wasserart Hafenbecken = new("Hafenbecken", 0.12f, 0.6f, 0.2f, 0f, new(0.40f, 0.45f, 0.38f), new(0.10f, 0.17f, 0.20f), 1.8f, 0.05f, 0.2f,
        SchaumStaerke: 0f, SchaumBreite: 0f, SchaumKamm: 0f, Kaustik: 0.2f, Eisregel.Nie, Vielfalt: 0.3f, ExtraWellen: 0.25f);
    public static readonly Wasserart Furt = new("Furt", 0.10f, 0.4f, 0.1f, 1.0f, new(0.58f, 0.60f, 0.50f), new(0.30f, 0.40f, 0.36f), 0.6f, 0.04f, 0.18f,
        SchaumStaerke: 0f, SchaumBreite: 0f, SchaumKamm: 0f, Kaustik: 0.35f, Eisregel.Nie);
    /// <summary>Schmaler, schwacher Strömungssaum, nur dort, wo der offene Fluss an eine Kaimauer strömt (eigener Streifen, Phase 3); im ruhigen Hafenbecken nie.</summary>
    public static readonly Wasserart KaiSaum = Fluss with { Name = "Kaisaum", SchaumStaerke = 0.3f, SchaumBreite = 0.35f, Eis = Eisregel.Nie, EisReichweite = 0f };

    /// <summary>Alle Arten in der Reihenfolge der Testszene (ohne Kaisaum, das ist nur eine Schaumstufe).</summary>
    public static readonly Wasserart[] Alle = { Fluss, Bach, Teich, See, Meer, Hafenbecken, Furt };

    public static Wasserart? Nach(string name) =>
        Array.Find(Alle, a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || (name == "hafen" && a == Hafenbecken));

    /// <summary>
    /// Kartenwasser → Art. <paramref name="kartenart"/> ist das Feld <c>art</c> der Karte: <c>fluss</c>/<c>bach</c> sind Linien mit <paramref name="breite"/>
    /// (Meter), <c>see</c>/<c>teich</c> sind Flächen mit <paramref name="flaeche"/> (m²). Ein Fluss bis 5 m ist ein Bach; ein See bis 2.000 m² ein Teich.
    /// Schaum haben nur Flüsse ab 30 m Breite und das Meer. Hafenbecken (vor der Kaimauer eines Hafens, <c>BeckenNetz</c>) und Furt (Übergang)
    /// ergeben sich nicht aus der Kartenart, sondern aus dem Ort; das Meer kennt die Karte noch nicht (<see cref="Meer"/> für spätere Küsten).
    /// </summary>
    public static Wasserart AusKarte(string kartenart, double breite, double flaeche = 0)
    {
        var art = kartenart.ToLowerInvariant();
        if (art is "fluss" or "bach")
        {
            if (art == "bach" || breite <= BachBisBreite)
                return Fuer(Bach, (float)breite, 0);
            return Fuer(Fluss, (float)breite, 0);
        }
        // see / teich (und alles Unbekannte mit Fläche)
        if (art == "teich" || flaeche <= TeichBisFlaeche)
            return Fuer(Teich, 0, (float)flaeche);
        return Fuer(See, 0, (float)flaeche);
    }

    /// <summary>
    /// Passt die Art an die Größe an: Schaum beim Fluss nur ab 30 m Breite; Eisreichweite bei vollem Zufrieren bis über die Mitte (halbe Breite bzw.
    /// Halbmesser der Fläche, mit Zuschlag), bei Randeis 3–6 m (größerer See, breiterer Rand).
    /// </summary>
    public static Wasserart Fuer(Wasserart art, float breite, float flaeche)
    {
        if (art == Fluss)
            return breite >= SchaumAbBreite ? art : art with { SchaumStaerke = 0f };
        return art.Eis switch
        {
            Eisregel.Voll when breite > 0 => art with { EisReichweite = breite * 0.5f * 1.2f + 0.5f },
            Eisregel.Voll => art with { EisReichweite = (flaeche > 0 ? Mathf.Sqrt(flaeche / Mathf.Pi) : 25f) * 1.2f + 0.5f },
            Eisregel.Rand => art with { EisReichweite = RandEisMin + (RandEisMax - RandEisMin) * Mathf.Clamp((flaeche - TeichBisFlaeche) / 20000f, 0f, 1f) },
            _ => art,
        };
    }

    /// <summary>
    /// Winterfortschritt 0–1 aus Jahreszeit und Fortschritt im Spieljahr (0–1; ein Spieljahr ist eine Jahreszeit, 1140 Ticks): Im Winter wächst das Eis
    /// im ersten Drittel (bis 0,35) von 0 auf 1 und hält bis zum Jahresende; im Frühling taut es im ersten Drittel (bis 0,3) von 1 auf 0.
    /// </summary>
    public static float Winterfortschritt(Jahreszeit jahreszeit, double jahresanteil)
    {
        var f = (float)Math.Clamp(jahresanteil, 0, 1);
        return jahreszeit switch
        {
            Jahreszeit.Winter => Mathf.SmoothStep(0f, 0.35f, f),
            Jahreszeit.Fruehling => 1f - Mathf.SmoothStep(0f, 0.3f, f),
            _ => 0f,
        };
    }

    // ── Bilder des Wassers: an einer Stelle, damit andere Texturen (Blender) ohne Eingriff am Shader eingesetzt werden können ──
    private const string BilderOrdner = "res://assets/texturen/realistisches_wasser/";
    public const string NormalA = BilderOrdner + "wasser_normal_a.png";
    public const string NormalB = BilderOrdner + "wasser_normal_b.png";
    public const string Stroemungskarte = BilderOrdner + "wasser_stroemung.png";
    public const string Schaumbild = BilderOrdner + "wasser_schaum.png";
    /// <summary>Kaustik als Atlas (4 × 4 Bilder); ein fertiges <see cref="Texture2DArray"/> kann stattdessen mit <see cref="KaustikFeld"/> belegt werden.</summary>
    public const string KaustikAtlas = BilderOrdner + "wasser_kaustik.png";
    /// <summary>Optional: eigenes Textur-Feld (16 Lagen) statt des Atlas.</summary>
    public static Texture2DArray? KaustikFeld;

    private static Texture2DArray? _kaustik;

    /// <summary>Belegt die fünf Bilder (Normalkarten, Strömungskarte, Schaum, Kaustik) eines Wassermaterials.</summary>
    public static void BilderSetzen(ShaderMaterial m)
    {
        m.SetShaderParameter("normalmap_a_sampler", GD.Load<Texture2D>(NormalA));
        m.SetShaderParameter("normalmap_b_sampler", GD.Load<Texture2D>(NormalB));
        m.SetShaderParameter("uv_sampler", GD.Load<Texture2D>(Stroemungskarte));
        m.SetShaderParameter("foam_sampler", GD.Load<Texture2D>(Schaumbild));
        m.SetShaderParameter("caustic_sampler", KaustikFeld ?? (_kaustik ??= AtlasZuFeld(GD.Load<Texture2D>(KaustikAtlas))));
    }

    private static Texture2DArray AtlasZuFeld(Texture2D atlasTextur)
    {
        var atlas = atlasTextur.GetImage();
        atlas.Decompress();
        var lagen = new Godot.Collections.Array<Image>();
        for (var i = 0; i < 16; i++)
            lagen.Add(atlas.GetRegion(new Rect2I(i % 4 * 256, i / 4 * 256, 256, 256)));
        var feld = new Texture2DArray();
        feld.CreateFromImages(lagen);
        return feld;
    }

    /// <summary>
    /// Setzt die Werte einer Art auf ein Wassermaterial. <paramref name="richtung"/> ist die Laufrichtung (x, z) für Strömung und Wellen;
    /// <paramref name="winter"/> der Winterfortschritt (<see cref="Winterfortschritt"/>); <paramref name="uferImNetz"/>: das Netz liefert in
    /// <c>COLOR.r</c> den Abstand zum Ufer in Metern (sonst schätzt der Shader das Eis aus der Tiefe).
    /// </summary>
    public static void Anwenden(ShaderMaterial m, Wasserart art, Vector2 richtung, float winter, bool uferImNetz)
    {
        var r = richtung.LengthSquared() > 0.0001f ? richtung.Normalized() : new Vector2(1, 0.4f).Normalized();
        m.SetShaderParameter("wellen_staerke", art.WellenStaerke);
        m.SetShaderParameter("wellen_laenge", art.WellenLaenge);
        m.SetShaderParameter("wind_anteil", art.WindAnteil);
        m.SetShaderParameter("wellen_vielfalt", art.Vielfalt);
        m.SetShaderParameter("extra_wellen", art.ExtraWellen);
        var d = r.Rotated(-1.1f);
        var e = r.Rotated(2.0f);
        m.SetShaderParameter("wave_d", new Vector4(d.X, d.Y, 0.22f, 6.3f));
        m.SetShaderParameter("wave_e", new Vector4(e.X, e.Y, 0.16f, 2.3f));
        m.SetShaderParameter("stroemung", r * art.Stroemung);
        // Drei Wellen aus leicht verschiedenen Richtungen (im Fluss enger um die Laufrichtung, sonst weiter), damit kein Waschbrett entsteht
        var faecher = art.Stroemung > 0 ? 0.5f : 0.9f;
        var b = r.Rotated(faecher);
        var c = r.Rotated(-faecher * 0.7f);
        m.SetShaderParameter("wave_a", new Vector4(r.X, r.Y, 0.35f, 3f));
        m.SetShaderParameter("wave_b", new Vector4(b.X, b.Y, 0.30f, 1.55f));
        m.SetShaderParameter("wave_c", new Vector4(c.X, c.Y, 0.25f, 0.9f));
        m.SetShaderParameter("ruhe_drift", art.Stroemung > 0 ? new Vector2(0.01f, 0.01f) : new Vector2(0.05f, 0.04f));
        m.SetShaderParameter("color_shallow", art.Flach);
        m.SetShaderParameter("color_deep", art.Tief);
        m.SetShaderParameter("beers_law", art.Klarheit);
        m.SetShaderParameter("refraction", art.Brechung);
        m.SetShaderParameter("rauheit", art.Rauheit);
        m.SetShaderParameter("schaum_staerke", art.SchaumStaerke);
        m.SetShaderParameter("schaum_breite", art.SchaumBreite);
        m.SetShaderParameter("schaum_kamm", art.SchaumKamm);
        m.SetShaderParameter("kaustik_staerke", art.Kaustik);
        m.SetShaderParameter("eis_reichweite", art.Eis == Eisregel.Nie ? 0f : art.EisReichweite);
        m.SetShaderParameter("eis", art.Eis == Eisregel.Nie ? 0f : winter);
        m.SetShaderParameter("ufer_im_netz", uferImNetz ? 1f : 0f);
    }
}
