using System.Globalization;
using Reichsgilde;

/// <summary>
/// Lautheit der Klänge (Ton Schritt 7): die Angleichung der Oberflächenklänge (<c>game/daten/klang_lautheit.json</c>, Rechnung
/// <c>game/scripts/KlangLautheitModell.cs</c>) und das Messprotokoll <c>docs/assets/lautheit-messwerte.csv</c> (<c>tools/ton/lautheit_messen.py</c>).
/// Gemessen wird hier nicht (kein ffmpeg in der CI): geprüft wird, dass Daten, Messwerte und Dateien zusammenpassen.
/// </summary>
static class KlangLautheitTests
{
    private static string Wurzel() => Path.GetFullPath(Path.Combine(MusikTests.Spielordner("daten"), "..", ".."));

    private static List<Dictionary<string, string>> Messwerte()
    {
        var zeilen = File.ReadAllLines(Path.Combine(Wurzel(), "docs", "assets", "lautheit-messwerte.csv"));
        var kopf = zeilen[0].Split(',');
        return zeilen.Skip(1).Where(z => z.Length > 0).Select(z =>
        {
            var t = z.Split(',');
            return kopf.Zip(t, (k, w) => (k, w)).ToDictionary(x => x.k, x => x.w);
        }).ToList();
    }

    private static double Zahl(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    public static IEnumerable<(string, Action)> Alle()
    {
        yield return ("Lautheit: das Messprotokoll nennt jede OGG-Datei unter assets/audio genau einmal", () =>
        {
            var audio = Path.Combine(MusikTests.Spielordner("assets", "audio"));
            var dateien = Directory.GetFiles(audio, "*.ogg", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(audio, f).Replace(Path.DirectorySeparatorChar, '/')).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var gemessen = Messwerte().Select(m => m["datei"]).OrderBy(x => x, StringComparer.Ordinal).ToList();
            Pruefe.Gleich(dateien.Count, gemessen.Count);
            Pruefe.Wahr(dateien.SequenceEqual(gemessen), "Dateien und Messprotokoll stimmen überein (tools/ton/lautheit_messen.py neu laufen lassen)");
        });
        yield return ("Lautheit: Musik liegt bei -17 LUFS, Ansagen bei -18 LUFS (Dateien, jeweils +-1,5 LU)", () =>
        {
            foreach (var m in Messwerte())
            {
                if (m["klasse"] == "Musik") Pruefe.Wahr(Math.Abs(Zahl(m["integriert_lufs"]) + 17) <= 1.5, $"{m["datei"]} {m["integriert_lufs"]}");
                if (m["klasse"] == "Stimmen") Pruefe.Wahr(Math.Abs(Zahl(m["integriert_lufs"]) + 18) <= 1.5, $"{m["datei"]} {m["integriert_lufs"]}");
            }
        });
        yield return ("Lautheit: Angleichung lädt, fehlende Datei = 0 dB", () =>
        {
            var t = KlangLautheitModell.Laden("{\"angleichung_db\":{\"a\":1.5,\"b\":-2}}");
            Pruefe.Gleich(1.5f, KlangLautheitModell.Angleichung(t, "a"));
            Pruefe.Gleich(-2f, KlangLautheitModell.Angleichung(t, "b"));
            Pruefe.Gleich(0f, KlangLautheitModell.Angleichung(t, "fehlt"));
            Pruefe.Gleich(0, KlangLautheitModell.Laden("{}").Count);
        });
        yield return ("Lautheit: nach der Angleichung sind alle Varianten eines Ereignisses höchstens 0,2 dB auseinander", () =>
        {
            var angleichung = KlangLautheitModell.Laden(File.ReadAllText(MusikTests.Spielordner("daten", "klang_lautheit.json")));
            var gruppen = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(Wurzel(), "tools", "ton", "lautheit_gruppen.json")))
                .RootElement.GetProperty("gruppen");
            var gemessen = Messwerte().Where(m => m["klasse"] == "Oberflaeche")
                .ToDictionary(m => Path.GetFileNameWithoutExtension(m["datei"]), m => Zahl(m["momentan_max"]));
            var anzahl = 0;
            foreach (var g in gruppen.EnumerateObject())
            {
                var ziel = g.Value.GetProperty("ziel").GetDouble();
                foreach (var d in g.Value.GetProperty("dateien").EnumerateArray())
                {
                    var datei = d.GetString()!;
                    var danach = gemessen[datei] + KlangLautheitModell.Angleichung(angleichung, datei);
                    Pruefe.Wahr(Math.Abs(danach - ziel) <= 0.2, $"{datei}: {danach:0.0} statt {ziel:0.0}");
                    Pruefe.Wahr(Math.Abs(KlangLautheitModell.Angleichung(angleichung, datei)) <= 12, $"{datei}: Angleichung über 12 dB");
                    anzahl++;
                }
            }
            Pruefe.Gleich(anzahl, angleichung.Count);
        });
    }
}
