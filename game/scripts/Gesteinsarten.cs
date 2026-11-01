using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Reichsgilde;

/// <summary>Eine Gesteinsart aus <c>game/daten/gestein.json</c> (nur Optik). Die Farben bleiben sRGB; ins Lineare rechnet der Shader (<c>source_color</c>).</summary>
public sealed record Gestein(
    string Kennung, string Name, Color Hell, Color Dunkel, float BandWinkel, float BandRichtung, float BandDicke, float BandKontrast,
    float Fuge, float KluftAbstand, float Kantenrundung, float Rauheit, float Moos, float Flechten, Color FlechteFarbe, float Efeu,
    Color SchuttFarbe, string[] Formen);

/// <summary>
/// Das Fels-Material (Felspinsel F2): liest die Gesteinsvorlagen aus <c>res://daten/gestein.json</c> und baut daraus <see cref="ShaderMaterial"/>
/// mit <c>fels.gdshader</c> (Struktur dreiseitig, ab Grafikstufe Mittel) oder <c>fels_niedrig.gdshader</c> (nur von oben). Die Struktur ist
/// <c>texturen/fels/fels_struktur.png</c> (<c>tools/oberflaeche/felstexturen.py</c>). Eine Gesteinsart je Karte für den Hang-Fels, einzelne
/// Felsstücke dürfen abweichen (Nutzer 01.11.2026); das Spiel nutzt das Material ab F5, bis dahin nur die Testszene <see cref="Felstest"/>.
/// </summary>
public static class Gesteinsarten
{
    public const string Pfad = "res://daten/gestein.json";
    public const string ShaderHoch = "res://assets/shader/fels.gdshader";
    public const string ShaderNiedrig = "res://assets/shader/fels_niedrig.gdshader";
    public const string StrukturPfad = "res://assets/texturen/fels/fels_struktur.png";

    private static List<Gestein>? _alle;
    private static Godot.Collections.Dictionary? _wurzel;

    /// <summary>Alle Gesteinsarten in der Reihenfolge der Datei.</summary>
    public static IReadOnlyList<Gestein> Alle => _alle ??= Laden();

    /// <summary>Die Gesteinsart mit dieser Kennung (z. B. „grauwacke“) oder die erste, wenn es sie nicht gibt.</summary>
    public static Gestein Nach(string kennung) => Alle.FirstOrDefault(g => g.Kennung == kennung) ?? Alle[0];   // Alle ist nie leer (Vorgabe)

    private static List<Gestein> Laden()
    {
        _wurzel = Json.ParseString(Godot.FileAccess.GetFileAsString(Pfad)).AsGodotDictionary();
        var vorgabe = new Gestein("grau", "Grau", new Color(0.6f, 0.6f, 0.6f), new Color(0.35f, 0.35f, 0.35f), 0, 0, 0.4f, 0.2f, 0.4f, 0, 0.4f, 0.9f,
            0.4f, 0.4f, new Color(0.8f, 0.6f, 0.3f), 0, new Color(0.5f, 0.5f, 0.5f), Array.Empty<string>());
        if (_wurzel is not { Count: > 0 } || !_wurzel.ContainsKey("gesteine"))
        {
            GD.PushError($"[fels] {Pfad} fehlt oder ist leer.");
            return new List<Gestein> { vorgabe };
        }
        var liste = new List<Gestein>();
        foreach (var eintrag in _wurzel["gesteine"].AsGodotArray())
        {
            var d = eintrag.AsGodotDictionary();
            float Zahl(string k) => d.ContainsKey(k) ? (float)d[k].AsDouble() : 0f;
            string Text(string k) => d.ContainsKey(k) ? d[k].AsString() : "?";
            Color Farbe(string k) => d.ContainsKey(k) ? Color.FromHtml(d[k].AsString()) : Colors.Gray;
            liste.Add(new Gestein(
                Text("kennung"), Text("name"), Farbe("farbe_hell"), Farbe("farbe_dunkel"), Zahl("band_winkel"), Zahl("band_richtung"),
                Math.Max(0.05f, Zahl("band_dicke")), Zahl("band_kontrast"), Zahl("fuge"), Zahl("kluft_abstand"), Zahl("kantenrundung"), Zahl("rauheit"),
                Zahl("moos"), Zahl("flechten"), Farbe("flechte_farbe"), Zahl("efeu"), Farbe("schutt_farbe"),
                d.ContainsKey("formen") ? d["formen"].AsGodotArray().Select(f => f.AsString()).ToArray() : Array.Empty<string>()));
        }
        if (liste.Count == 0)
        {
            GD.PushError($"[fels] {Pfad} enthält keine Gesteinsart.");
            liste.Add(vorgabe);
        }
        return liste;
    }

    private static Color GemeinsameFarbe(string schluessel, Color vorgabe)
    {
        _ = Alle;
        return _wurzel is not null && _wurzel.ContainsKey(schluessel) ? Color.FromHtml(_wurzel[schluessel].AsString()) : vorgabe;
    }

    private static float FrischWert(string schluessel, float vorgabe)
    {
        _ = Alle;
        return _wurzel is not null && _wurzel.ContainsKey("frisch_gebrochen")
            && _wurzel["frisch_gebrochen"].AsGodotDictionary() is { } f && f.ContainsKey(schluessel) ? (float)f[schluessel].AsDouble() : vorgabe;
    }

    /// <summary>
    /// Richtung senkrecht auf den Schichten (Welt): <paramref name="winkel"/> Grad gegen die Waagerechte gekippt, abtauchend nach
    /// <paramref name="richtung"/> Grad (0 Norden = −z, 90 Osten = +x).
    /// </summary>
    public static Vector3 BandNormale(float winkel, float richtung)
    {
        var w = Mathf.DegToRad(winkel);
        var r = Mathf.DegToRad(richtung);
        var abtauchen = new Vector3(Mathf.Sin(r), 0, -Mathf.Cos(r));
        // Die Schicht fällt in Richtung „abtauchen“ ab: ihre Normale neigt sich dorthin
        return (Vector3.Up * Mathf.Cos(w) + abtauchen * Mathf.Sin(w)).Normalized();
    }

    /// <summary>Ein neues Fels-Material für <paramref name="gestein"/>. <paramref name="dreiseitig"/>: Struktur von drei Seiten (Mittel und höher) oder nur von oben (Niedrig).</summary>
    public static ShaderMaterial Material(Gestein gestein, bool dreiseitig, bool frisch = false, float frischBis = 1000f)
    {
        var m = new ShaderMaterial { Shader = GD.Load<Shader>(dreiseitig ? ShaderHoch : ShaderNiedrig) };
        m.SetShaderParameter("struktur", GD.Load<Texture2D>(StrukturPfad));
        Anwenden(m, gestein);
        FrischSetzen(m, frisch, frischBis);
        return m;
    }

    /// <summary>Setzt die Werte einer Gesteinsart in ein Fels-Material.</summary>
    public static void Anwenden(ShaderMaterial m, Gestein g)
    {
        m.SetShaderParameter("farbe_hell", g.Hell);
        m.SetShaderParameter("farbe_dunkel", g.Dunkel);
        m.SetShaderParameter("band_normale", BandNormale(g.BandWinkel, g.BandRichtung));
        m.SetShaderParameter("band_dicke", g.BandDicke);
        m.SetShaderParameter("band_kontrast", g.BandKontrast);
        m.SetShaderParameter("fuge", g.Fuge);
        m.SetShaderParameter("kluft_abstand", g.KluftAbstand);
        m.SetShaderParameter("kantenrundung", g.Kantenrundung);
        m.SetShaderParameter("rauheit", g.Rauheit);
        m.SetShaderParameter("moos", g.Moos);
        m.SetShaderParameter("flechten", g.Flechten);
        m.SetShaderParameter("flechte_farbe", g.FlechteFarbe);
        m.SetShaderParameter("efeu", g.Efeu);
        m.SetShaderParameter("moos_farbe", GemeinsameFarbe("moos_farbe", new Color(0.31f, 0.36f, 0.13f)));
        m.SetShaderParameter("moos_trocken", GemeinsameFarbe("moos_farbe_trocken", new Color(0.44f, 0.42f, 0.17f)));
        m.SetShaderParameter("flechte_hell", GemeinsameFarbe("flechte_hell", new Color(0.79f, 0.78f, 0.74f)));
        m.SetShaderParameter("efeu_farbe", GemeinsameFarbe("efeu_farbe", new Color(0.18f, 0.29f, 0.11f)));
    }

    /// <summary>Schalter „frisch gebrochen“ (Steinbruch): unter <paramref name="bis"/> (Welt-y) kein Bewuchs, heller, glatter, mit Spaltrillen.</summary>
    public static void FrischSetzen(ShaderMaterial m, bool frisch, float bis = 1000f)
    {
        m.SetShaderParameter("frisch", frisch);
        m.SetShaderParameter("frisch_bis", bis);
        m.SetShaderParameter("frisch_aufhellen", FrischWert("aufhellen", 0.22f));
        m.SetShaderParameter("frisch_rauheit", FrischWert("rauheit", 0.72f));
        m.SetShaderParameter("spaltrillen", FrischWert("spaltrillen", 0.5f));
        m.SetShaderParameter("rillen_abstand", Math.Max(0.05f, FrischWert("rillen_abstand", 0.35f)));
    }

    /// <summary>Wechselt ein Fels-Material zwischen dreiseitig (Mittel und höher) und nur von oben (Niedrig); die Werte bleiben.</summary>
    public static void StufeSetzen(ShaderMaterial m, bool dreiseitig) => m.Shader = GD.Load<Shader>(dreiseitig ? ShaderHoch : ShaderNiedrig);
}
