using System.Collections.Generic;
using Godot;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Das Jahr der Bäume (Recherche: <c>docs/bereiche/darstellung.md</c>, „Originalbäume mit Jahreszeiten“): Farbe, Blüte, Früchte und Schnee je Art und
/// Jahreszeit auf den Originalbäumen des Kits (<c>natur/natur_original_baeume.glb</c>), umgesetzt im Shader <c>baum_wind.gdshader</c>.
/// Die Art steht im Knotennamen („Eiche2“, „Fichte1“, „Kiefer1“, „Apfel1“, „Kirsche1“, „Buche1“, „Hasel1“, „Weissdorn1“, „Holunder1“).
/// Die Stadtansicht legt je Art und Quellmaterial ein <see cref="ShaderMaterial"/> an (<see cref="Stoff"/>) und stellt mit <see cref="Setzen"/> alle auf eine
/// Jahreszeit ein; die Testszene legt je Jahreszeit eigene an.
/// </summary>
public static class Baumjahr
{
    public enum Zeit { Fruehling, Sommer, Herbst, Winter }

    public const string Modell = "natur/natur_original_baeume";

    public static Zeit Von(Jahreszeit j) => j switch { Jahreszeit.Fruehling => Zeit.Fruehling, Jahreszeit.Herbst => Zeit.Herbst, Jahreszeit.Winter => Zeit.Winter, _ => Zeit.Sommer };

    /// <summary>Die Art eines Knotens des Baukastens („Eiche2“ → „Eiche“), „“ wenn er keiner Art gehört.</summary>
    public static string Art(string knoten)
    {
        var name = knoten.Split('_')[0].TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        return name is "Eiche" or "Jungeiche" or "Buche" or "Apfel" or "Kirsche" or "Fichte" or "Kiefer" or "Hasel" or "Weissdorn" or "Holunder" ? name : "";
    }

    /// <summary>
    /// Jahreszeitliches Aussehen einer Art (Recherche: <c>docs/bereiche/darstellung.md</c>, Tabelle „Bäume im Jahr“): Tönung und Stärke der Blattkarten,
    /// Blüten bzw. Früchte als Flecken, Schnee. Quelle der Werte: Fachquellen zu Mitteleuropa (Wikipedia-Artikel der Arten, Forstportale).
    /// </summary>
    public readonly record struct Aussehen(Color Farbe, float Toenung, float Flecken = 0f, Color FleckFarbe = default, float Schnee = 0f);

    public static Aussehen Laub(string art, Zeit z) => (art, z) switch
    {
        // Stieleiche: später, gelbgrüner Austrieb; Herbst gelbbraun; kahl (junge Bäume behalten braunes Laub)
        ("Eiche", Zeit.Fruehling) => new(new Color(0.70f, 0.78f, 0.28f), 0.62f),
        ("Eiche", Zeit.Herbst) => new(new Color(0.66f, 0.46f, 0.13f), 0.85f),
        ("Jungeiche", Zeit.Fruehling) => new(new Color(0.70f, 0.78f, 0.28f), 0.62f),
        ("Jungeiche", Zeit.Herbst) => new(new Color(0.66f, 0.46f, 0.13f), 0.85f),
        ("Jungeiche", Zeit.Winter) => new(new Color(0.46f, 0.31f, 0.15f), 0.92f, 0f, default, 0.25f),
        // Rotbuche: früher, hellgrüner Austrieb; Herbst orange bis rotbraun
        ("Buche", Zeit.Fruehling) => new(new Color(0.52f, 0.86f, 0.30f), 0.72f),
        ("Buche", Zeit.Herbst) => new(new Color(0.80f, 0.40f, 0.10f), 0.86f),
        // Apfel: weiß-rosa Blüte mit dem Laub (Mai); im Herbst gelbgrünes Laub mit roten Früchten
        ("Apfel", Zeit.Fruehling) => new(new Color(0.60f, 0.80f, 0.30f), 0.5f, 1f, new Color(0.98f, 0.84f, 0.88f)),
        ("Apfel", Zeit.Sommer) => new(new Color(0.34f, 0.54f, 0.20f), 0.92f, 0.12f, new Color(0.45f, 0.62f, 0.18f)),
        ("Apfel", Zeit.Herbst) => new(new Color(0.78f, 0.72f, 0.22f), 0.55f, 0.30f, new Color(0.78f, 0.13f, 0.10f)),
        // Kirsche: weiße Blüte zusammen mit dem (bronzenen) Laub; Herbst intensiv rot und gelb
        ("Kirsche", Zeit.Fruehling) => new(new Color(0.50f, 0.64f, 0.28f), 0.5f, 1f, new Color(1f, 1f, 1f)),
        ("Kirsche", Zeit.Sommer) => new(new Color(0.34f, 0.54f, 0.20f), 0.92f),
        ("Kirsche", Zeit.Herbst) => new(new Color(0.92f, 0.45f, 0.08f), 0.82f),
        // Fichte: immergrün, frische Triebe hellgrün, Schnee auf den Ästen
        ("Fichte", Zeit.Fruehling) => new(new Color(0.62f, 0.95f, 0.32f), 0.5f),
        ("Fichte", Zeit.Sommer) => new(new Color(0.16f, 0.32f, 0.15f), 0.4f),
        ("Fichte", Zeit.Herbst) => new(new Color(0.16f, 0.32f, 0.15f), 0.4f),
        ("Fichte", Zeit.Winter) => new(new Color(0.16f, 0.32f, 0.15f), 0.4f, 0f, default, 1f),
        // Waldkiefer: blaugrüne Nadeln das ganze Jahr, weniger Schnee
        ("Kiefer", Zeit.Winter) => new(new Color(0.40f, 0.58f, 0.46f), 0.75f, 0f, default, 0.45f),
        ("Kiefer", _) => new(new Color(0.40f, 0.58f, 0.46f), 0.75f),
        // Hasel: hellgrün, Herbst vergilbend, im Winter dürres Gespinst
        ("Hasel", Zeit.Fruehling) => new(new Color(0.58f, 0.82f, 0.28f), 0.7f),
        ("Hasel", Zeit.Sommer) => new(new Color(0.40f, 0.58f, 0.20f), 0.7f),
        ("Hasel", Zeit.Herbst) => new(new Color(0.88f, 0.76f, 0.22f), 0.85f),
        ("Hasel", Zeit.Winter) => new(new Color(0.42f, 0.34f, 0.22f), 0.98f, 0f, default, 0.2f),
        // Weißdorn: weiße Blüte Mai bis Juni, rote Früchte im Herbst, Laub gelb bis rot
        ("Weissdorn", Zeit.Fruehling) => new(new Color(0.58f, 0.80f, 0.28f), 0.7f, 1f, new Color(0.98f, 0.97f, 0.95f)),
        ("Weissdorn", Zeit.Sommer) => new(new Color(0.34f, 0.52f, 0.20f), 0.7f),
        ("Weissdorn", Zeit.Herbst) => new(new Color(0.76f, 0.34f, 0.12f), 0.8f, 0.4f, new Color(0.62f, 0.07f, 0.09f)),
        ("Weissdorn", Zeit.Winter) => new(new Color(0.42f, 0.34f, 0.22f), 0.98f, 0.25f, new Color(0.55f, 0.07f, 0.08f), 0.2f),
        // Schwarzer Holunder: früher Austrieb, Blütendolden im Sommer, Früchte dunkel im Herbst
        ("Holunder", Zeit.Fruehling) => new(new Color(0.58f, 0.80f, 0.28f), 0.7f),
        ("Holunder", Zeit.Sommer) => new(new Color(0.36f, 0.54f, 0.22f), 0.65f),
        ("Holunder", Zeit.Herbst) => new(new Color(0.62f, 0.64f, 0.22f), 0.65f),
        ("Holunder", Zeit.Winter) => new(new Color(0.42f, 0.34f, 0.22f), 0.98f, 0f, default, 0.2f),
        // Rinde und alles andere im Winter: Schnee nur auf Nadelbäumen
        _ => new(Colors.White, 0f),
    };

    public static bool Trocken(string art, Zeit z) => z == Zeit.Winter && art is "Jungeiche" or "Hasel" or "Weissdorn" or "Holunder";

    /// <summary>Windfassung (<c>baum_wind.gdshader</c>) eines Quellmaterials für Jahreszeit und Art; je Schlüssel einmal (<paramref name="cache"/>).</summary>
    public static ShaderMaterial Stoff(Dictionary<string, ShaderMaterial> cache, BaseMaterial3D quelle, Zeit z, string art, float hoehe)
    {
        art = Gleich(art);
        var schluessel = $"{quelle.ResourceName}|{z}|{art}|{hoehe:0.0}";
        if (cache.TryGetValue(schluessel, out var vorhanden)) return vorhanden;
        var m = Neu(quelle, hoehe);
        Jahr(m, art, z);
        cache[schluessel] = m;
        return m;
    }

    private static string Gleich(string art) => art switch { "Tanne" => "Fichte", "Busch" => "Hasel", _ => art };

    /// <summary>Ein Material mit Textur, Maske und Wind aus dem Quellmaterial des Modells; die Jahreszeit stellt <see cref="Jahr"/> ein.</summary>
    public static ShaderMaterial Neu(BaseMaterial3D quelle, float hoehe)
    {
        var name = quelle.ResourceName;
        var m = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/baum_wind.gdshader"), ResourceName = name };
        m.SetShaderParameter("farbe", Colors.White);
        if (quelle.AlbedoTexture is { } tex) m.SetShaderParameter("albedo_tex", tex);
        m.SetShaderParameter("normale", quelle.NormalEnabled && quelle.NormalTexture is not null);
        if (quelle.NormalTexture is { } nt) m.SetShaderParameter("normal_tex", nt);
        m.SetShaderParameter("normal_staerke", quelle.NormalScale);
        m.SetShaderParameter("rauheit", 0.9f);
        var blatt = name.StartsWith("Leaves") || name == "Flowers";
        m.SetShaderParameter("maske", blatt);
        m.SetShaderParameter("schwelle", 0.45f);
        m.SetShaderParameter("hoehe", Mathf.Max(hoehe, 1f));
        m.SetShaderParameter("flattern", blatt ? 1f : 0f);
        return m;
    }

    /// <summary>Stellt ein Material (aus <see cref="Neu"/>) für Art und Jahreszeit ein: alle jahreszeitlichen Werte, auch die auf 0.</summary>
    public static void Jahr(ShaderMaterial m, string art, Zeit z)
    {
        art = Gleich(art);
        var name = m.ResourceName;
        var blatt = name.StartsWith("Leaves") || name == "Flowers";
        var a = Laub(art, z);
        if (name == "Flowers")
        {
            // Holunder: Dolden cremeweiß im Sommer, dunkle Früchte im Herbst; sonst unsichtbar (Strauch ohne Blütenkarte hat keine)
            var sichtbar = art == "Holunder" && z is Zeit.Sommer or Zeit.Herbst;
            m.SetShaderParameter("farbe", new Color(1, 1, 1, sichtbar ? 1f : 0f));
            m.SetShaderParameter("laub_farbe", z == Zeit.Herbst ? new Color(0.18f, 0.08f, 0.20f) : new Color(0.97f, 0.94f, 0.78f));
            m.SetShaderParameter("laub_toenung", 1f);
        }
        else if (blatt)
        {
            m.SetShaderParameter("laub_farbe", a.Farbe);
            // Die Blattkarte der Obstbäume und Sträucher (TwistedTree) ist im Original rot und dunkel: immer kräftig umfärben, heller
            var rot = name == "Leaves_TwistedTree";
            m.SetShaderParameter("laub_toenung", rot ? Mathf.Max(a.Toenung, 0.92f) : a.Toenung);
            m.SetShaderParameter("hell_faktor", rot ? 7.0f : 3.5f);
            m.SetShaderParameter("bluete", a.Flecken);
            m.SetShaderParameter("bluete_farbe", a.FleckFarbe);
            m.SetShaderParameter("schnee", a.Schnee);
            m.SetShaderParameter("luecke", Trocken(art, z) ? 0.8f : 0f);
        }
        else
        {
            m.SetShaderParameter("schnee", z != Zeit.Winter ? 0f : art == "Fichte" ? 0.9f : art == "Kiefer" ? 0.5f : 0.45f); // Rinde: Schnee auf den Oberseiten der Äste
            m.SetShaderParameter("rinde_oben", art == "Kiefer" ? 1f : 0f);
            switch (art)
            {
                case "Eiche" or "Jungeiche": m.SetShaderParameter("rinde_farbe", new Color(0.52f, 0.47f, 0.40f)); m.SetShaderParameter("rinde_toenung", 0.65f); break; // graubraun
                case "Buche": m.SetShaderParameter("rinde_farbe", new Color(0.62f, 0.60f, 0.58f)); m.SetShaderParameter("rinde_toenung", 0.75f); break;           // glatt, silbergrau
                case "Kirsche": m.SetShaderParameter("rinde_farbe", new Color(0.50f, 0.36f, 0.34f)); m.SetShaderParameter("rinde_toenung", 0.6f); break;           // rötlichgrau
                default: m.SetShaderParameter("rinde_toenung", 0f); break;
            }
        }
    }
}
