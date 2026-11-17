using System.Globalization;
using System.Text.RegularExpressions;
using Reichsgilde;
using Reichsgilde.Core;

/// <summary>Steinebenen je Volk: Zuordnung Volk/Stufe → Ebene, Tabelle im Shader, Größe und Format der Arrays.</summary>
static class SteinebenenTests
{
    static string Spielordner() => Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft()))!;

    /// <summary>Breite, Höhe und Farbtyp aus dem Kopf einer PNG-Datei (ohne Bildbibliothek).</summary>
    static (int Breite, int Hoehe, int Farbtyp, int Tiefe) PngKopf(string pfad)
    {
        var b = new byte[29];
        using (var f = File.OpenRead(pfad)) f.ReadExactly(b);
        int Zahl(int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];
        return (Zahl(16), Zahl(20), b[25], b[24]);
    }

    /// <summary>Die Tabelle STEIN_EBENE aus dem Shader als Liste von vier Zahlen je Ebene.</summary>
    static List<float[]> ShaderTabelle(string text)
    {
        var block = Regex.Match(text, @"const vec4 STEIN_EBENE\[(\d+)\] = vec4\[\d+\]\((.*?)\);", RegexOptions.Singleline);
        Pruefe.Wahr(block.Success, "STEIN_EBENE im Shader");
        return Regex.Matches(block.Groups[2].Value, @"vec4\(([^)]*)\)")
            .Select(m => m.Groups[1].Value.Split(',').Select(z => float.Parse(z.Trim(), CultureInfo.InvariantCulture)).ToArray()).ToList();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Steinebenen: Volk und Stufe wählen die Ebene, Orks und Oger wohnen wie Menschen", () =>
        {
            Pruefe.Gleich(0, Steinebenen.Ebene(Volk.Mensch, 1));
            Pruefe.Gleich(2, Steinebenen.Ebene(Volk.Mensch, 3));
            Pruefe.Gleich(3, Steinebenen.Ebene(Volk.Zwerg, 1));
            Pruefe.Gleich(5, Steinebenen.Ebene(Volk.Zwerg, 3));
            Pruefe.Gleich(6, Steinebenen.Ebene(Volk.Elb, 1));
            Pruefe.Gleich(8, Steinebenen.Ebene(Volk.Elb, 2));   // Stufen 2 und 3 der Elben hinter dem Halbling angehängt
            Pruefe.Gleich(9, Steinebenen.Ebene(Volk.Elb, 3));
            Pruefe.Gleich(7, Steinebenen.Ebene(Volk.Halbling, 1));
            Pruefe.Gleich(1, Steinebenen.Ebene(Volk.Ork, 2));
            Pruefe.Gleich(0, Steinebenen.Ebene(Volk.Mensch, 0));   // außerhalb 1–3: geklemmt
            Pruefe.Gleich(2, Steinebenen.Ebene(Volk.Mensch, 9));
            // Gegenprobe: jedes Volk mit eigenen Häusern hat eine andere Ebene als die Menschen derselben Stufe
            foreach (var v in new[] { Volk.Zwerg, Volk.Elb, Volk.Halbling })
                for (var s = 1; s <= 3; s++)
                    Pruefe.Wahr(Steinebenen.Ebene(v, s) != Steinebenen.Ebene(Volk.Mensch, s), $"{v} Stufe {s} nicht auf der Ebene der Menschen");
            for (var s = 1; s <= 3; s++)
                foreach (var v in Enum.GetValues<Volk>())
                    Pruefe.Wahr(Steinebenen.Ebene(v, s) >= 0 && Steinebenen.Ebene(v, s) < Steinebenen.Haeuser.Length, $"{v} Stufe {s} im Array");
        }),
        ("Steinebenen: Dateiname eines Außenhauses → Ebene und AO-Ebene, sonst keine", () =>
        {
            Pruefe.Wahr(Steinebenen.AusDatei("gebaeude/neubau/wohnhaus_mensch_s1_g1") == (0, 0), "Mensch S1");
            Pruefe.Wahr(Steinebenen.AusDatei("gebaeude/neubau/wohnhaus_mensch_s3_g4") == (2, 3), "Mensch S3 (eigener AO-Atlas)");
            Pruefe.Wahr(Steinebenen.AusDatei("gebaeude/neubau/wohnhaus_zwerg_s1_g3") == (3, 1), "Zwerg S1");
            Pruefe.Wahr(Steinebenen.AusDatei("gebaeude/neubau/wohnhaus_zwerg_s2_g1") == (4, 4), "Zwerg S2 (eigener AO-Atlas)");
            Pruefe.Wahr(Steinebenen.AusDatei("gebaeude/neubau/wohnhaus_zwerg_s3_g4") == (5, 5), "Zwerg S3 (eigener AO-Atlas)");
            Pruefe.Wahr(Steinebenen.AusDatei("gebaeude/neubau/wohnhaus_elb_s1_g2") == (6, 6), "Elb S1 (eigener AO-Atlas)");
            Pruefe.Wahr(Steinebenen.AusDatei("gebaeude/neubau/wohnhaus_elb_s2_g1") == (8, 7), "Elb S2 (eigener AO-Atlas)");
            Pruefe.Wahr(Steinebenen.AusDatei("gebaeude/neubau/wohnhaus_elb_s3_g4") == (9, 8), "Elb S3 (eigener AO-Atlas)");
            Pruefe.Wahr(Steinebenen.AusDatei("gebaeude/neubau/wohnhaus_halbling_s2_g1") == (7, 0), "Halbling: Platzhalter ohne Atlas");
            Pruefe.Wahr(Steinebenen.AusDatei("gebaeude/neubau/wohnhaus_mensch_s1_g1_innen") is null, "innen gehört nicht ins Häuser-Array");
            Pruefe.Wahr(Steinebenen.AusDatei("gebaeude/neubau/rathaus") is null, "Rathaus hat ein eigenes Array");
        }),
        ("Steinebenen: Tabelle STEIN_EBENE im Shader gleich Steinebenen.Haeuser (Gegenprobe: vertauschte Ebene fällt auf)", () =>
        {
            var text = File.ReadAllText(Path.Combine(Spielordner(), "assets", "shader", "building_trim.gdshaderinc")); // Rumpf von building_trim.gdshader und _kante
            Pruefe.Wahr(text.Contains("uniform sampler2DArray albedo_tex") && text.Contains("instance uniform int ebene")
                        && text.Contains("instance uniform int ao_ebene"), "Shader liest Arrays und wählt die Ebene je Instanz");
            var tabelle = ShaderTabelle(text);
            Pruefe.Gleich(Steinebenen.Haeuser.Length, tabelle.Count);
            bool Gleich(float[] t, Steinebenen.Baender b) =>
                Math.Abs(t[0] - b.A0) < 1e-4 && Math.Abs(t[1] - b.A1) < 1e-4 && Math.Abs(t[2] - b.B0) < 1e-4 && Math.Abs(t[3] - b.B1) < 1e-4;
            for (var i = 0; i < tabelle.Count; i++)
                Pruefe.Wahr(Gleich(tabelle[i], Steinebenen.Haeuser[i].Stein), $"Ebene {i} ({Steinebenen.Haeuser[i].Name}) gleich im Shader");
            Pruefe.Wahr(!Gleich(tabelle[0], Steinebenen.Haeuser[2].Stein), "Gegenprobe: Mensch S1 und S3 unterscheiden sich");
            // Steinbänder: Bruchstein immer, Putzband nur, wo Stein darin liegt
            Pruefe.Wahr(Steinebenen.Haeuser.All(e => e.Stein.Enthaelt(0.9f)), "Bruchstein ist überall Stein");
            Pruefe.Wahr(!Steinebenen.Haeuser[0].Stein.Enthaelt(0.6f) && Steinebenen.Haeuser[3].Stein.Enthaelt(0.6f), "Putz Mensch S1 nein, Zwerg ja");
            Pruefe.Wahr(!Steinebenen.Haeuser[Steinebenen.Ebene(Volk.Elb, 1)].Stein.Enthaelt(0.6f), "Elb S1: im Putzband Kalkputz, kein Stein");
            Pruefe.Wahr(!Steinebenen.Haeuser[Steinebenen.Ebene(Volk.Elb, 2)].Stein.Enthaelt(0.6f), "Elb S2: Putzsockel und Gefache Kalkputz");
            Pruefe.Wahr(Steinebenen.Haeuser[Steinebenen.Ebene(Volk.Elb, 3)].Stein.Enthaelt(0.6f), "Elb S3: Werkstein (K5) im Putzband");
        }),
        ("Steinebenen: Arrays gleich groß, eine Ebene je Eintrag, 2048 breit, Import als Texture2DArray mit Mipmaps", () =>
        {
            var ordner = Path.Combine(Spielordner(), "assets", "texturen", "neubau");
            foreach (var (array, ebenen) in new[] { ("haeuser", Steinebenen.Haeuser.Length), ("innen", Steinebenen.InnenBlaetter.Length) })
            {
                var groessen = new List<(int, int)>();
                foreach (var art in new[] { "albedo", "normal", "orm" })
                {
                    var pfad = Path.Combine(ordner, $"{array}_ebenen_{art}.png");
                    var k = PngKopf(pfad);
                    Pruefe.Gleich(2048, k.Breite);
                    Pruefe.Gleich(2048 * ebenen, k.Hoehe);
                    Pruefe.Gleich(8, k.Tiefe);
                    Pruefe.Wahr(art == "albedo" ? k.Farbtyp == 6 : k.Farbtyp == 2, $"{array} {art}: Farbtyp {k.Farbtyp}");
                    groessen.Add((k.Breite, k.Hoehe));
                    var imp = File.ReadAllText(pfad + ".import");
                    Pruefe.Wahr(imp.Contains("importer=\"2d_array_texture\""), $"{array} {art}: Import als Array");
                    Pruefe.Wahr(imp.Contains("slices/horizontal=1") && Regex.IsMatch(imp, $"slices/vertical={ebenen}\\r?\\n"), $"{array} {art}: {ebenen} Ebenen senkrecht");
                    Pruefe.Wahr(imp.Contains("compress/mode=2") && imp.Contains("mipmaps/generate=true"), $"{array} {art}: VRAM-Kompression mit Mipmaps");
                }
                Pruefe.Wahr(groessen.Distinct().Count() == 1, $"{array}: Albedo, Normale, ORM gleich groß");
            }
            var ao = PngKopf(Path.Combine(ordner, "haeuser_ao_ebenen.png"));
            Pruefe.Gleich(2048 * Steinebenen.AoGruppen.Length, ao.Hoehe);
            var innenAo = PngKopf(Path.Combine(ordner, "innen_ao_ebenen.png"));
            Pruefe.Gleich(2048 * Steinebenen.InnenAoGruppen.Length, innenAo.Hoehe);
            Pruefe.Gleich((0, 0), Steinebenen.AusDatei("gebaeude/neubau/wohnhaus_mensch_s1_g1")!.Value);
            Pruefe.Gleich((1, 2), Steinebenen.AusDatei("gebaeude/neubau/wohnhaus_mensch_s2_g3")!.Value);
            Pruefe.Gleich((2, 3), Steinebenen.AusDatei("gebaeude/neubau/wohnhaus_mensch_s3_g1")!.Value);
            Pruefe.Gleich((2, 2), Steinebenen.InnenAusBlatt("raeume_innen_trim", "wohnhaus_mensch_raeume_innen"));
            Pruefe.Gleich((4, 3), Steinebenen.InnenAusBlatt("innen_trim_s23_zwerg", "wohnhaus_zwerg_innen"));
            Pruefe.Gleich((5, 4), Steinebenen.InnenAusBlatt("raeume_innen_trim_zwerg", "wohnhaus_zwerg_dachraeume_innen"));
            Pruefe.Gleich((6, 6), Steinebenen.InnenAusBlatt("innen_trim_elb", "wohnhaus_elb_innen"));
            Pruefe.Gleich((7, 6), Steinebenen.InnenAusBlatt("innen_trim_s23_elb", "wohnhaus_elb_innen"));
            Pruefe.Gleich((8, 7), Steinebenen.InnenAusBlatt("raeume_innen_trim_elb", "wohnhaus_elb_raeume_innen"));
            // Gegenprobe des Kopflesers: ein Blatt mit einer Ebene ist nicht so hoch wie das Häuser-Array
            Pruefe.Wahr(PngKopf(Path.Combine(ordner, "trim_albedo.png")).Hoehe != 2048 * Steinebenen.Haeuser.Length, "Gegenprobe Höhe");
        }),
    };
}
