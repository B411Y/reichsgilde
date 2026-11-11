using Reichsgilde;
using M = Reichsgilde.MusikModell;

/// <summary>
/// Ereignisstücke spielen +3 dB lauter (Nutzer, 11.11.2026), Tod und alles andere nicht; die Dateien bleiben unverändert, darum bleibt der True-Peak
/// einschließlich der Verstärkung unter −0,3 dBTP (Messwerte in <c>docs/assets/musik-messwerte*.csv</c>).
/// </summary>
static class MusikLautstaerkeTests
{
    static string Doku(string datei) => MusikTests.Spielordner("..", "docs", "assets", datei);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Musik Lautstärke: jedes event_*-Stück +3 dB, Tod, Tag, Nacht, Glaube und Menü 0 dB", () =>
        {
            var (s, _) = MusikTests.Liste();
            var ereignisse = s.Where(x => x.Datei.StartsWith("event/")).ToList();
            Pruefe.Gleich(10, ereignisse.Count); // 5 aus Runde 1, 5 aus Runde 2
            foreach (var x in ereignisse) Pruefe.Ungefaehr(3.0, M.LautstaerkeDb(x), 1e-9);
            Pruefe.Wahr(s.Where(x => !x.Datei.StartsWith("event/")).All(x => M.LautstaerkeDb(x) == 0), "nur Ereignisse lauter");
            foreach (var lage in new[] { "tod", "tag", "nacht", "glaube", "menue" })
                Pruefe.Wahr(s.Where(x => x.Lage == lage).All(x => M.LautstaerkeDb(x) == 0), lage);
            Pruefe.Ungefaehr(0, M.LautstaerkeDb(s.First(x => x.Lage == "tod")), 1e-9);
            Pruefe.Ungefaehr(0, M.LautstaerkeDb(s.First(x => x.Datei == "tag/tag_marktplatz.ogg")), 1e-9);
        }),

        ("Musik Lautstärke: der Anlass bestimmt es – jedes gespielte Ereignisstück +3 dB, das Tod-Stück 0 dB", () =>
        {
            var (s, _) = MusikTests.Liste();
            foreach (var a in Enum.GetValues<M.Anlass>().Where(a => a != M.Anlass.Keiner))
            {
                var erwartet = a == M.Anlass.Tod ? 0.0 : 3.0;
                foreach (var x in M.AnlassKandidaten(s, a)) Pruefe.Ungefaehr(erwartet, M.LautstaerkeDb(x), 1e-9);
            }
        }),

        ("Musik Lautstärke: der True-Peak jedes Ereignisstücks plus 3 dB bleibt unter −0,3 dBTP; die Prüfung schlägt bei einem zu lauten Stück an", () =>
        {
            var gelesen = 0;
            foreach (var datei in new[] { "musik-messwerte.csv", "musik-messwerte-runde2.csv" })
            {
                var zeilen = File.ReadAllLines(Doku(datei)).Skip(1).Select(l => l.Split(',')).Where(z => z[0].StartsWith("event/")).ToList();
                var kopf = File.ReadAllLines(Doku(datei))[0].Split(',');
                var tp = Array.IndexOf(kopf, "true_peak_dbtp");
                foreach (var z in zeilen)
                {
                    var spitze = double.Parse(z[tp], System.Globalization.CultureInfo.InvariantCulture);
                    Pruefe.Wahr(spitze + M.EreignisDb <= -0.3, $"{z[0]}: {spitze} + 3 dB über −0,3 dBTP");
                    gelesen++;
                }
            }
            Pruefe.Wahr(gelesen >= 10, "Messwerte der zehn Ereignisstücke gelesen: " + gelesen);
            // Gegenprobe: ein Stück mit −2 dBTP wäre mit +3 dB zu laut
            Pruefe.Wahr(!(-2.0 + M.EreignisDb <= -0.3), "Gegenprobe");
        }),

        ("Musik Lautstärke: unter einem Ereignisstück sinkt die Stadt um 4 dB, weich (Abfall 0,6 s, Rückkehr 2,5 s)", () =>
        {
            Pruefe.Ungefaehr(-4, M.AtmoDuckDb, 1e-9);
            Pruefe.Wahr(M.AtmoAb < M.AtmoAuf, "schnell hinunter, langsam zurück");
            Pruefe.Wahr(M.AtmoAb > 0.2 && M.AtmoAuf > 1, "sanft");
        }),
    };
}
