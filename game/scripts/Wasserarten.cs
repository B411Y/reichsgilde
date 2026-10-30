using System;
using System.Linq;
using Godot;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>Wann ein Gewässer zufriert: nie, ganz (kleine und stille Gewässer) oder nur am Rand (Seen, Flüsse, Furten).</summary>
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
/// <param name="EisAb">Ab diesem Winterfortschritt (0–1) wächst das Eis dieser Art …</param>
/// <param name="EisBis">… und hat bei diesem seine volle Reichweite: Stilles Wasser friert zuerst (Teich, Hafenbecken, Graben 0–0,5), ein Bach
/// später (0,15–1), ein Fluss bekommt erst spät Randeis (0,3–1). Siehe <see cref="Wasserarten.EisFortschritt"/>.</param>
/// <param name="Schollen">1 = im Winter treiben Eisschollen mit der Strömung (breite Flüsse, als Körper: <see cref="Schollenfeld"/>); 0 = keine.</param>
/// <param name="WindHoehe">So viel höher (Faktor − 1) werden die Wellen von leichtem Wind bis Sturm (Fluss: ruhig 5–10 cm, Sturm etwa 30 cm); 0 = nur der
/// allgemeine Windanteil.</param>
/// <param name="VielfaltSkala">Faktor auf die Feinheit der Felder aus <paramref name="Vielfalt"/>: kleiner = längere Abschnitte mit gleichen Wellen.</param>
public sealed record Wasserart(
    string Name, float WellenStaerke, float WellenLaenge, float WindAnteil, float Stroemung,
    Color Flach, Color Tief, float Klarheit, float Brechung, float Rauheit,
    float SchaumStaerke, float SchaumBreite, float SchaumKamm, float Kaustik, Eisregel Eis, float EisReichweite = 0f, float Vielfalt = 0f, float ExtraWellen = 0f,
    float EisAb = 0f, float EisBis = 1f, float Schollen = 0f, float WindHoehe = 0f, float VielfaltSkala = 1f);

/// <summary>Die Gewässerarten, ihre Zuordnung zum Kartenwasser und der Winterfortschritt des Eises (alles nur Darstellung).</summary>
public static class Wasserarten
{
    /// <summary>Flüsse ab dieser Breite (m) haben Uferschaum; schmalere keinen.</summary>
    public const float SchaumAbBreite = 30f;
    /// <summary>Bäche und Flüsse bis zu dieser Breite (m) heißen Bach und frieren ganz zu; breitere Flüsse bekommen nur Randeis und Schollen.</summary>
    public const float BachBisBreite = 5f;
    /// <summary>Teiche und Tümpel bis zu dieser Fläche (m²) frieren ganz zu; größere Seen nur am Rand (3–6 m).</summary>
    public const float TeichBisFlaeche = 2000f;
    public const float RandEisMin = 3f, RandEisMax = 6f;
    /// <summary>Randeis eines Flusses: 5 % der Breite, mindestens 1,2 m, höchstens 5 m (Rhein bei 100 m Breite: 5 m an jedem Ufer).</summary>
    public const float FlussRandEisAnteil = 0.05f, FlussRandEisMin = 1.2f, FlussRandEisMax = 5f;
    /// <summary>Ein Lauf, der langsamer fließt (m/s), ist stilles Wasser: ein Graben (friert früh und ganz zu, wie ein Teich).</summary>
    public const float GrabenBisTempo = 0.1f;
    /// <summary>
    /// Furt: Randeis vom Ufer her bis zu diesem Anteil der halben Breite (mindestens 0,3 m, höchstens 3 m), die Mitte bleibt offen – über der
    /// flachen Furt läuft das Wasser schnell.
    /// </summary>
    public const float FurtRandAnteil = 0.45f, FurtRandMin = 0.3f, FurtRandMax = 3f;

    // Eis (Nutzer 30.10.2026): Stilles Wasser friert zuerst und ganz zu (Teich, Hafenbecken, Graben), ein Bach später und ganz (offen bleiben nur
    // Mündung und schnelle Stellen), eine Furt nur am Rand, ein breiter Fluss bekommt spät Randeis und treibende Schollen, nie eine Decke.
    // Wellen des Flusses (Nutzer 30.10.2026, Phase 3c): wie beim Meer mehrere Züge mit wandernder Stärke, je Abschnitt (Startwert aus der Karte)
    // andere Größe und anderes Tempo, längs mit der Strömung; ruhig 5–10 cm hoch, im Sturm etwa 30 cm (WindHoehe).
    public static readonly Wasserart Fluss = new("Fluss", 0.24f, 3.0f, 0.4f, 1.2f, new(0.40f, 0.45f, 0.32f), new(0.10f, 0.19f, 0.21f), 1.3f, 0.06f, 0.22f,
        SchaumStaerke: 0.9f, SchaumBreite: 0.9f, SchaumKamm: 0f, Kaustik: 0.3f, Eisregel.Rand, EisReichweite: 1.8f, Vielfalt: 0.8f, ExtraWellen: 0.5f,
        EisAb: 0.3f, EisBis: 1f, Schollen: 1f, WindHoehe: 2.2f, VielfaltSkala: 0.45f);
    public static readonly Wasserart Bach = new("Bach", 0.14f, 0.5f, 0.1f, 0.8f, new(0.55f, 0.62f, 0.52f), new(0.22f, 0.36f, 0.36f), 0.9f, 0.05f, 0.18f,
        SchaumStaerke: 0f, SchaumBreite: 0f, SchaumKamm: 0f, Kaustik: 0.8f, Eisregel.Voll, EisAb: 0.15f, EisBis: 1f);
    public static readonly Wasserart Teich = new("Teich", 0.12f, 0.5f, 0.3f, 0f, new(0.35f, 0.42f, 0.30f), new(0.12f, 0.22f, 0.20f), 1.6f, 0.05f, 0.18f,
        SchaumStaerke: 0f, SchaumBreite: 0f, SchaumKamm: 0f, Kaustik: 0.6f, Eisregel.Voll, EisAb: 0f, EisBis: 0.5f);
    /// <summary>Stiller Lauf (Strömung unter <see cref="GrabenBisTempo"/>): Stadtgraben, Mühlgraben ohne Zulauf – friert wie ein Teich früh und ganz zu.</summary>
    public static readonly Wasserart Graben = Teich with { Name = "Graben", WellenStaerke = 0.08f, Klarheit = 1.8f };
    public static readonly Wasserart See = new("See", 0.38f, 1.3f, 0.7f, 0f, new(0.48f, 0.60f, 0.58f), new(0.12f, 0.27f, 0.37f), 1.4f, 0.07f, 0.2f,
        SchaumStaerke: 0f, SchaumBreite: 0f, SchaumKamm: 0f, Kaustik: 0.6f, Eisregel.Rand, Vielfalt: 0.6f, ExtraWellen: 0.5f, EisAb: 0.1f, EisBis: 0.9f);
    public static readonly Wasserart Meer = new("Meer", 1.0f, 3.5f, 1.0f, 0f, new(0.44f, 0.60f, 0.60f), new(0.05f, 0.17f, 0.29f), 0.8f, 0.08f, 0.2f,
        SchaumStaerke: 0.8f, SchaumBreite: 1.2f, SchaumKamm: 0.35f, Kaustik: 0.5f, Eisregel.Nie, Vielfalt: 1f, ExtraWellen: 1f);
    /// <summary>Ruhiges Becken vor der Kaimauer: friert früh und ganz zu, von den Mauern her (im Spiel trägt das Netz den Abstand zur Mauer, höchstens 3 m).</summary>
    public static readonly Wasserart Hafenbecken = new("Hafenbecken", 0.12f, 0.6f, 0.2f, 0f, new(0.40f, 0.45f, 0.38f), new(0.10f, 0.17f, 0.20f), 1.8f, 0.05f, 0.2f,
        SchaumStaerke: 0f, SchaumBreite: 0f, SchaumKamm: 0f, Kaustik: 0.2f, Eisregel.Voll, EisReichweite: 4.5f, Vielfalt: 0.3f, ExtraWellen: 0.25f, EisAb: 0f, EisBis: 0.5f);
    /// <summary>Furt in der Testszene (im Spiel ist sie Teil des Bach- oder Flussbandes): Randeis bis gut 1 m, die Mitte bleibt offen.</summary>
    public static readonly Wasserart Furt = new("Furt", 0.10f, 0.4f, 0.1f, 1.0f, new(0.58f, 0.60f, 0.50f), new(0.30f, 0.40f, 0.36f), 0.6f, 0.04f, 0.18f,
        SchaumStaerke: 0f, SchaumBreite: 0f, SchaumKamm: 0f, Kaustik: 0.35f, Eisregel.Rand, EisReichweite: 1.2f, EisAb: 0.3f, EisBis: 1f);
    /// <summary>Schmaler, schwacher Strömungssaum, nur dort, wo der offene Fluss an eine Kaimauer strömt (eigener Streifen, Phase 3); im ruhigen Hafenbecken nie.</summary>
    public static readonly Wasserart KaiSaum = Fluss with { Name = "Kaisaum", SchaumStaerke = 0.3f, SchaumBreite = 0.35f, Eis = Eisregel.Nie, EisReichweite = 0f };

    /// <summary>Alle Arten in der Reihenfolge der Testszene (ohne Kaisaum, das ist nur eine Schaumstufe, und ohne Graben, der wie ein Teich aussieht).</summary>
    public static readonly Wasserart[] Alle = { Fluss, Bach, Teich, See, Meer, Hafenbecken, Furt };

    public static Wasserart? Nach(string name) =>
        Array.Find(Alle, a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || (name == "hafen" && a == Hafenbecken));

    /// <summary>
    /// Kartenwasser → Art. <paramref name="kartenart"/> ist das Feld <c>art</c> der Karte: <c>fluss</c>/<c>bach</c> sind Linien mit <paramref name="breite"/>
    /// (Meter, beim Einbau die größte Breite des Laufs) und <paramref name="stroemung"/> (m/s, negativ = unbekannt), <c>see</c>/<c>teich</c> sind Flächen
    /// mit <paramref name="flaeche"/> (m²). Es entscheidet die Größe, nicht der Name (Nutzer 29.10.2026): Ein Lauf bis 5 m ist ein Bach und friert ganz
    /// zu, ein breiterer ein Fluss (nur Randeis und Schollen); ein Lauf, der fast steht, ist ein Graben (friert früh und ganz zu); eine Fläche bis
    /// 2.000 m² ist ein Teich (friert früh und ganz zu), eine größere ein See (nur Randeis). Schaum haben nur Flüsse ab 30 m Breite und das Meer.
    /// Hafenbecken (vor der Kaimauer eines Hafens) und Furt (Übergang) ergeben sich nicht aus der Kartenart, sondern aus dem Ort; das Meer kennt
    /// die Karte noch nicht (<see cref="Meer"/> für spätere Küsten).
    /// </summary>
    public static Wasserart AusKarte(string kartenart, double breite, double flaeche = 0, double stroemung = -1)
    {
        if (kartenart.ToLowerInvariant() is "fluss" or "bach")
            return stroemung is >= 0 and < GrabenBisTempo ? Fuer(Graben, (float)breite, 0)
                : breite <= BachBisBreite ? Fuer(Bach, (float)breite, 0) : Fuer(Fluss, (float)breite, 0);
        // see / teich (und alles Unbekannte mit Fläche)
        return flaeche <= TeichBisFlaeche ? Fuer(Teich, 0, (float)flaeche) : Fuer(See, 0, (float)flaeche);
    }

    /// <summary>
    /// Passt die Art an die Größe an: Schaum beim Fluss nur ab 30 m Breite, sein Randeis 5 % der Breite (1,2–5 m); Eisreichweite bei vollem Zufrieren
    /// bis über die Mitte (halbe Breite bzw. Halbmesser der Fläche, mit Zuschlag), beim See 3–6 m (größerer See, breiterer Rand).
    /// </summary>
    public static Wasserart Fuer(Wasserart art, float breite, float flaeche)
    {
        if (art == Fluss)
        {
            var rand = Mathf.Clamp(breite * FlussRandEisAnteil, FlussRandEisMin, FlussRandEisMax);
            return breite >= SchaumAbBreite ? art with { EisReichweite = rand } : art with { SchaumStaerke = 0f, EisReichweite = rand };
        }
        return art.Eis switch
        {
            Eisregel.Voll when breite > 0 => art with { EisReichweite = breite * 0.5f * 1.2f + 0.5f },
            Eisregel.Voll when flaeche > 0 => art with { EisReichweite = Mathf.Sqrt(flaeche / Mathf.Pi) * 1.2f + 0.5f },
            Eisregel.Rand when flaeche > 0 => art with { EisReichweite = RandEisMin + (RandEisMax - RandEisMin) * Mathf.Clamp((flaeche - TeichBisFlaeche) / 20000f, 0f, 1f) },
            _ => art,
        };
    }

    /// <summary>
    /// Wie weit das Eis dieser Art beim Winterfortschritt <paramref name="winter"/> (0–1) ist: 0 vor <see cref="Wasserart.EisAb"/>, 1 ab
    /// <see cref="Wasserart.EisBis"/>, dazwischen gleichmäßig. So friert stilles Wasser zuerst ganz zu, während der Bach erst Ränder hat.
    /// </summary>
    /// <summary>Wintertönung des offenen Wassers (kälter, dunkler, schwächere Lichtnetze): voll schon ab 0,4 des Winterfortschritts – kalt ist das Wasser,
    /// bevor es friert.</summary>
    public static float WinterTon(float winter, float tauen = 0f) =>
        Mathf.Clamp(winter * 2.5f, 0f, 1f) * (1f - Mathf.SmoothStep(0.5f, 1f, tauen)); // im Eisgang wird das Wasser wieder wärmer, am Winterende wie im Frühling

    public static float EisFortschritt(Wasserart art, float winter) =>
        art.Eis == Eisregel.Nie ? 0f : Mathf.Clamp((winter - art.EisAb) / Mathf.Max(art.EisBis - art.EisAb, 0.01f), 0f, 1f);

    /// <summary>
    /// Der Eiskanal einer Netzecke (<c>COLOR.g</c>, wie <c>wasser_realistisch.gdshaderinc</c> ihn liest): 0,5 = wie die Art; darunter bleibt das Eis um
    /// <paramref name="offen"/> zurück (0 = friert nie), darüber friert es <paramref name="still"/> wie stilles Wasser (Hafenbecken). Beides zugleich gibt es
    /// nicht: Offen geht vor.
    /// </summary>
    public static float EisKanal(float offen, float still = 0f) =>
        offen > 0.001f ? 0.5f * (1f - Mathf.Clamp(offen, 0f, 1f)) : 0.5f + 0.5f * Mathf.Clamp(still, 0f, 1f);

    /// <summary>
    /// Furt: Anteil „offen“ (0–1), der das Eis dieser Art auf <see cref="FurtRandAnteil"/> der halben Breite <paramref name="halb"/> begrenzt – das Eis
    /// reicht im Shader <c>eis · (1 − offen) · Reichweite</c> weit. 1 = friert gar nicht (Art ohne Eis).
    /// </summary>
    public static float FurtOffen(Wasserart art, float halb)
    {
        if (art.Eis == Eisregel.Nie || art.EisReichweite <= 0.01f) return 1f;
        var rand = Mathf.Clamp(halb * FurtRandAnteil, FurtRandMin, FurtRandMax);
        return 1f - Mathf.Clamp(rand / art.EisReichweite, 0f, 1f);
    }

    /// <summary>
    /// Winterfortschritt 0–1 aus Jahreszeit und Fortschritt im Spieljahr (0–1; ein Spieljahr ist eine Jahreszeit, 1140 Ticks, im Kern
    /// <c>Spielzeit.TagesFortschritt</c>): Eis gibt es **nur im Winter** (Nutzer 30.10.2026). Es wächst über die ersten 55 % des Winters von 0 auf 1,
    /// erst schnell, dann langsamer (wie Eis dicker wird), und steht dann bis zum Ende; im letzten Stück bricht es im Eisgang auf
    /// (<see cref="Tauwetter"/>). Frühling, Sommer und Herbst 0. An den Jahreswechseln springt nichts: Herbstende 0 = Winterbeginn 0, am Winterende
    /// ist das Eis im Eisgang ganz vergangen (Tauwetter 1), der Frühling beginnt ohne Eis.
    /// </summary>
    public static float Winterfortschritt(Jahreszeit jahreszeit, double jahresanteil)
    {
        if (jahreszeit != Jahreszeit.Winter) return 0f;
        var t = Mathf.Clamp((float)Math.Clamp(jahresanteil, 0, 1) / WinterWachsen, 0f, 1f);
        return 1f - (1f - t) * (1f - t);
    }

    /// <summary>
    /// Tauwetter 0–1 (Eisgang) im letzten Stück des Winters, von <see cref="TauAb"/> bis <see cref="TauBis"/> des Jahres: Die Decken von Bach,
    /// Teich und Hafenbecken brechen in Platten auf, die nach und nach vergehen; auf dem Fluss treiben erst mehr Schollen, dann schmelzen sie; der
    /// Schnee schmilzt mit (Stadtansicht.Wetter.cs). Ab <see cref="TauBis"/> ist kein Eis mehr zu sehen – spätestens am ersten Frühlingstag. Wie in
    /// der Geschichte: Eisgang auf dem Rhein lag meist im Januar und Februar, nicht im Frühling. Außerhalb des Winters 0 (dort gibt es kein Eis).
    /// </summary>
    public static float Tauwetter(Jahreszeit jahreszeit, double jahresanteil) =>
        jahreszeit == Jahreszeit.Winter ? Mathf.SmoothStep(TauAb, TauBis, (float)Math.Clamp(jahresanteil, 0, 1)) : 0f;

    /// <summary>Bis zu diesem Anteil des Winters wächst das Eis; von <see cref="TauAb"/> bis <see cref="TauBis"/> geht es im Eisgang.</summary>
    public const float WinterWachsen = 0.55f, TauAb = 0.72f, TauBis = 0.96f;

    // ── Bilder des Wassers: an einer Stelle, damit andere Texturen (Blender) ohne Eingriff am Shader eingesetzt werden können ──
    private const string BilderOrdner = "res://assets/texturen/realistisches_wasser/";
    public const string NormalA = BilderOrdner + "wasser_normal_a.png";

    /// <summary>Relief des Eises (Normalkarte, ambientCG „Snow 014“, CC0; Phase 3d): gemeinsam für das Eis auf dem Wasser und die Schollen.</summary>
    public const string EisRelief = "res://assets/texturen/eis/eis_relief_normal.jpg";

    /// <summary>
    /// Anteil klaren (schwarzen) Eises je Art (Uniform <c>eis_klar</c> des gemeinsamen Eismaterials): Stilles Wasser friert klar (Teich, Hafenbecken,
    /// See), ein Bach etwas, ein Fluss kaum – sein Eis ist gefrorener Eisbrei (Fotos von Rhein und Elbe, Phase 3d).
    /// </summary>
    public static float EisKlar(Wasserart art) =>
        art.Name == Teich.Name || art.Name == Hafenbecken.Name ? 0.5f : art.Name == See.Name ? 0.45f : art.Name == Bach.Name || art.Name == Furt.Name ? 0.32f : 0.12f;

    /// <summary>Das gemeinsame Eismaterial auf einem Stoff der Schollen: Relief und Anteil klaren Eises wie das Eis der Art auf dem Wasser.</summary>
    public static void EisStoffSetzen(ShaderMaterial m, Wasserart art)
    {
        m.SetShaderParameter("eis_relief", GD.Load<Texture2D>(EisRelief));
        m.SetShaderParameter("eis_klar", EisKlar(art));
    }
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
        m.SetShaderParameter("eis_relief", GD.Load<Texture2D>(EisRelief));
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
    /// <paramref name="winter"/> der Winterfortschritt (<see cref="Winterfortschritt"/>; das Eis der Art folgt ihm nach <see cref="EisFortschritt"/>, das
    /// offene Wasser wird mit ihm kälter und dunkler); <paramref name="uferImNetz"/>: das Netz liefert in
    /// <c>COLOR</c> Uferabstand, offen, Schaumanteil und Deckkraft (sonst schätzt der Shader das Eis aus der Tiefe). <paramref name="lauf"/>: Netz
    /// eines Flusses mit Laufachsen in <c>UV</c> und Strömung je Ecke in <c>UV2</c> (Stadtansicht.Wasser.cs); <paramref name="tempo"/> ersetzt dann
    /// das Tempo der Art (aus der Karte). <paramref name="netzAbstand"/>: Abstand der Netzecken in m (0 = fein genug für alle Wellen).
    /// </summary>
    public static void Anwenden(ShaderMaterial m, Wasserart art, Vector2 richtung, float winter, bool uferImNetz, bool lauf = false, float? tempo = null,
        float netzAbstand = 0f, float saat = 0f, float tauen = 0f)
    {
        var r = richtung.LengthSquared() > 0.0001f ? richtung.Normalized() : new Vector2(1, 0.4f).Normalized();
        var fliesst = (tempo ?? art.Stroemung) > 0;
        WellenSetzen(m, art, r, lauf, fliesst, netzAbstand, saat);
        // Ein Fluss rechnet die Strömung in seinen Laufachsen (quer, längs): (0, Tempo); sonst in Weltachsen
        m.SetShaderParameter("stroemung", lauf ? new Vector2(0, tempo ?? art.Stroemung) : r * (tempo ?? art.Stroemung));
        m.SetShaderParameter("ruhe_drift", fliesst ? new Vector2(0.01f, 0.01f) : new Vector2(0.05f, 0.04f));
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
        m.SetShaderParameter("eis", EisFortschritt(art, winter));
        m.SetShaderParameter("winter_ton", WinterTon(winter, tauen));
        m.SetShaderParameter("eis_still", EisFortschritt(Hafenbecken, winter));
        m.SetShaderParameter("eis_tauen", tauen);
        m.SetShaderParameter("rand_unruhe", art.Eis == Eisregel.Voll ? 0.5f : 1f);
        m.SetShaderParameter("eis_klar", EisKlar(art));
        // Wo das Eis vor dem Ufer unregelmäßig aufhört (dort liegt der Grund, keine gerade Naht): am Fluss und See bis 1,3 m, am Bach und Teich
        // nach der Größe, am Hafenbecken nicht (das Eis friert an die Kaimauer)
        m.SetShaderParameter("ufer_saum", art.Name == Hafenbecken.Name ? 0f : art.Eis == Eisregel.Voll ? Mathf.Clamp(art.EisReichweite * 0.15f, 0.25f, 1f) : 1.3f);
        m.SetShaderParameter("still_reichweite", Hafenbecken.EisReichweite);
        m.SetShaderParameter("ufer_im_netz", uferImNetz ? 1f : 0f);
    }

    /// <summary>
    /// Die Wellen einer Art (<c>wasser_wellen.gdshaderinc</c>) – für die Wasserfläche und für die Eisschollen darauf (<see cref="Schollenfeld"/>), damit
    /// beide dieselbe Fläche sehen. <paramref name="richtung"/>: Laufrichtung (Einheitsvektor, Welt); bei <paramref name="lauf"/> rechnen die Wellen in
    /// den Laufachsen des Flusses, die Hauptrichtung ist dann längs (0, 1) – sie folgt den Kurven. <paramref name="saat"/>: Startwert je Gewässer.
    /// </summary>
    public static void WellenSetzen(ShaderMaterial m, Wasserart art, Vector2 richtung, bool lauf, bool fliesst, float netzAbstand, float saat)
    {
        var r = lauf ? new Vector2(0, 1) : richtung;
        m.SetShaderParameter("wellen_staerke", art.WellenStaerke);
        m.SetShaderParameter("wellen_laenge", art.WellenLaenge);
        m.SetShaderParameter("wind_anteil", art.WindAnteil);
        m.SetShaderParameter("wellen_vielfalt", art.Vielfalt);
        m.SetShaderParameter("vielfalt_skala", art.VielfaltSkala);
        m.SetShaderParameter("wellen_saat", saat);
        m.SetShaderParameter("wind_hoehe", art.WindHoehe);
        m.SetShaderParameter("extra_wellen", art.ExtraWellen);
        m.SetShaderParameter("lauf_netz", lauf ? 1f : 0f);
        m.SetShaderParameter("netz_abstand", netzAbstand);
        var d = r.Rotated(-1.1f);
        var e = r.Rotated(2.0f);
        m.SetShaderParameter("wave_d", new Vector4(d.X, d.Y, 0.22f, 6.3f));
        m.SetShaderParameter("wave_e", new Vector4(e.X, e.Y, 0.16f, 2.3f));
        // Drei Wellen aus leicht verschiedenen Richtungen (im Fluss enger um die Laufrichtung, sonst weiter), damit kein Waschbrett entsteht
        var faecher = fliesst ? 0.5f : 0.9f;
        var b = r.Rotated(faecher);
        var c = r.Rotated(-faecher * 0.7f);
        m.SetShaderParameter("wave_a", new Vector4(r.X, r.Y, 0.35f, 3f));
        m.SetShaderParameter("wave_b", new Vector4(b.X, b.Y, 0.30f, 1.55f));
        m.SetShaderParameter("wave_c", new Vector4(c.X, c.Y, 0.25f, 0.9f));
    }

    /// <summary>Startwert der Wellen eines Gewässers aus seinem Namen in der Karte (fest: gleiche Karte, gleiche Abschnitte), 0–100.</summary>
    public static float Saat(string name)
    {
        var h = 2166136261u;
        foreach (var z in name) h = (h ^ z) * 16777619u;
        return h % 10000 / 100f;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Grafikstufen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Kaimauer: Anteil des Flussschaums (Stärke und Breite), der dort bleibt, wo der offene Fluss am Hafen vorbeiströmt.</summary>
    public static float KaiSchaumAnteil => KaiSaum.SchaumStaerke / Fluss.SchaumStaerke;

    private static readonly string[] ShaderDateien =
    {
        "res://assets/shader/wasser_niedrig.gdshader",     // Niedrig
        "res://assets/shader/wasser_mittel.gdshader",      // Mittel
        "res://assets/shader/wasser_realistisch.gdshader", // Hoch
        "res://assets/shader/wasser_realistisch.gdshader", // Ultra (feineres Netz)
    };

    /// <summary>Der Shader der Stufe (Niedrig: ohne Brechung, Tiefentextur, Kaustik und Regenringe; Mittel: ohne Brechung und Regenringe; Hoch und Ultra: alles).</summary>
    public static Shader ShaderFuer(Wasserstufe stufe) => GD.Load<Shader>(ShaderDateien[(int)stufe]);

    /// <summary>
    /// Abstand der Netzecken im Spiel (m): 2 m in Niedrig bis Hoch (der Abstand des Höhenrasters), 1 m in Ultra – dort zeigen sich auch die
    /// kürzeren Wellen als Bewegung der Fläche, sonst nur in den Normalkarten.
    /// </summary>
    public static float NetzAbstand(Wasserstufe stufe) => stufe == Wasserstufe.Ultra ? 1f : 2f;

    /// <summary>Startoption <c>--wasserstufe=niedrig|mittel|hoch|ultra</c>: diese Stufe statt der aus der Grafik-Voreinstellung (nur für diesen Start).</summary>
    public static Wasserstufe? StufeVorgabe =>
        Startoption("wasserstufe") is { } s && Enum.TryParse<Wasserstufe>(s, true, out var stufe) ? stufe : null;

    /// <summary>
    /// Die Stufe des Wassers aus der Grafik-Voreinstellung (kein eigener Regler): Niedrig, Mittel, Hoch, Ultra wie die Voreinstellung. Steht sie
    /// auf „Eigene“, gilt die Voreinstellung, mit der die meisten Regler übereinstimmen (bei Gleichstand die niedrigere).
    /// </summary>
    public static Wasserstufe StufeAusGrafik()
    {
        if (StufeVorgabe is { } vorgabe) return vorgabe;
        var v = Fenster.AktuelleVoreinstellung;
        if (v != Voreinstellung.Eigene) return (Wasserstufe)(int)v;
        var jetzt = Fenster.AktuelleStufe;
        var beste = Wasserstufe.Hoch;
        var besteZahl = -1;
        foreach (var stufe in new[] { Voreinstellung.Niedrig, Voreinstellung.Mittel, Voreinstellung.Hoch, Voreinstellung.Ultra })
        {
            var s = Fenster.StufeVon(stufe);
            var gleich = new[]
            {
                s.Kanten == jetzt.Kanten, s.Schatten == jetzt.Schatten, s.Verdeckung == jetzt.Verdeckung, s.Indirekt == jetzt.Indirekt,
                s.Bloom == jetzt.Bloom, s.Natur == jetzt.Natur, s.Passanten == jetzt.Passanten, s.Feinheiten == jetzt.Feinheiten,
                s.Unschaerfe == jetzt.Unschaerfe, s.Wolken == jetzt.Wolken, s.Dunst == jetzt.Dunst, s.Boden == jetzt.Boden, s.Gras == jetzt.Gras,
                s.Wetter == jetzt.Wetter,
            }.Count(x => x);
            if (gleich > besteZahl) (beste, besteZahl) = ((Wasserstufe)(int)stufe, gleich);
        }
        return beste;
    }

    /// <summary>Eine Startoption <c>--name=wert</c> (null, wenn sie fehlt).</summary>
    public static string? Startoption(string name)
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--" + name + "=", StringComparison.Ordinal)) return arg[(name.Length + 3)..];
        return null;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Vorwärmen (für den Ladebildschirm)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Übersetzt den Wasser-Shader im Voraus, damit das erste Bild mit Wasser nicht stockt (beim allerersten Start ohne Shader-Zwischenspeicher
    /// 20–30 s). Für den Ladebildschirm: aufrufen, solange er steht, und auf <paramref name="fertig"/> warten (oder nicht – dann wird eben dort
    /// übersetzt, wo das Wasser zuerst zu sehen ist). Baut unter <paramref name="eltern"/> ein kleines unsichtbares Bild (eigener
    /// <see cref="SubViewport"/> mit eigener Welt, 64 × 64) mit je einem Stück Wasser der Stufe (bzw. aller Stufen), lässt es einige Bilder lang
    /// zeichnen, entfernt es wieder und meldet die Zeit im Log. Schaden kann es nicht: Ist das Wasser schon übersetzt, kostet es nur diese Bilder.
    /// Die Eisschollen (<see cref="Schollenfeld"/>) der Stufe werden mit übersetzt.
    /// </summary>
    /// <param name="eltern">Ein Knoten im Baum (z. B. der Ladebildschirm selbst).</param>
    /// <param name="alleStufen">true: alle drei Shader (Niedrig, Mittel, Hoch/Ultra), etwa wenn der Spieler die Stufe gleich wechseln könnte.</param>
    /// <param name="fertig">Wird gerufen, wenn das Vorwärmen fertig ist.</param>
    public static void Vorwaermen(Node eltern, bool alleStufen = false, Action? fertig = null)
    {
        var start = Time.GetTicksMsec();
        var stufen = alleStufen ? new[] { Wasserstufe.Niedrig, Wasserstufe.Mittel, Wasserstufe.Hoch } : new[] { StufeAusGrafik() };
        var bild = new SubViewport
        {
            Name = "WasserVorwaermen", Size = new Vector2I(64, 64), OwnWorld3D = true, TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        var kamera = new Camera3D { Current = true };
        bild.AddChild(kamera);
        kamera.LookAtFromPosition(new Vector3(0, 4, 4), Vector3.Zero);
        bild.AddChild(new DirectionalLight3D { ShadowEnabled = true, RotationDegrees = new Vector3(-50, 30, 0) }); // wie im Spiel: Sonne mit Schatten
        for (var i = 0; i < stufen.Length; i++)
        {
            var stoff = new ShaderMaterial { Shader = ShaderFuer(stufen[i]) };
            BilderSetzen(stoff);
            Anwenden(stoff, Fluss, Vector2.Up, 0f, uferImNetz: true, lauf: true, tempo: 1f, netzAbstand: NetzAbstand(stufen[i]));
            bild.AddChild(new MeshInstance3D
            {
                Mesh = new PlaneMesh { Size = new Vector2(1, 1) }, MaterialOverride = stoff, Position = new Vector3(i * 0.3f - 0.3f, 0, 0),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
        // dazu die Eisschollen (Phase 3c) der Stufe: eine Scholle im MultiMesh, damit genau diese Fassung des Shaders übersetzt wird
        foreach (var stufe in stufen)
        {
            var schollen = new ShaderMaterial
            {
                Shader = GD.Load<Shader>(stufe >= Wasserstufe.Hoch ? "res://assets/shader/eis_scholle.gdshader" : "res://assets/shader/eis_scholle_einfach.gdshader"),
            };
            EisStoffSetzen(schollen, Fluss);
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true,
                Mesh = Schollenfeld.ScheibenNetz(24, fein: true), InstanceCount = 1,
            };
            mm.SetInstanceTransform(0, new Transform3D(Basis.Identity.Scaled(Vector3.One * 0.2f), Vector3.Zero));
            mm.SetInstanceColor(0, new Color(1, 0.2f, 0, 0));
            mm.SetInstanceCustomData(0, new Color(0, 0, 0, 1));
            bild.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = schollen, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
        eltern.AddChild(bild);
        var baum = eltern.GetTree();
        var bilder = 0;
        void Schritt()
        {
            if (++bilder < 4) return; // einige Bilder: Shader und Pipelines sind dann übersetzt
            baum.ProcessFrame -= Schritt;
            if (GodotObject.IsInstanceValid(bild)) bild.QueueFree();
            GD.Print($"[wasser] vorgewärmt ({string.Join(", ", stufen)}) in {Time.GetTicksMsec() - start} ms");
            fertig?.Invoke();
        }
        baum.ProcessFrame += Schritt;
    }
}

/// <summary>Die Grafikstufe des Wassers, aus der Grafik-Voreinstellung (<see cref="Wasserarten.StufeAusGrafik"/>).</summary>
public enum Wasserstufe { Niedrig, Mittel, Hoch, Ultra }
