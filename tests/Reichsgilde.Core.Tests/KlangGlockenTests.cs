using Reichsgilde;

/// <summary>Tests der Kirchenglocken nach Entfernung (<c>KlangGlockenModell.cs</c>, kennt kein Godot): Kurve, Summe mehrerer Kirchen, Dom, Tiefpass.</summary>
static class KlangGlockenTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Glocken: Selbsttest mit dB-Werten ist in Ordnung", () =>
        {
            var zeilen = new List<string>();
            Pruefe.Wahr(KlangGlockenModell.Selbsttest(zeilen.Add), string.Join(" | ", zeilen));
        }),
        ("Glocken: Pegel einer Kirche fällt mit dem Abstand, im Nahbereich voll", () =>
        {
            Pruefe.Wahr(KlangGlockenModell.Pegel(5f, false) == 0f && KlangGlockenModell.Pegel(30f, false) == 0f, "Nahbereich nicht voll");
            var vorher = 0f;
            foreach (var d in new[] { 60f, 120f, 240f, 480f, 960f })
            {
                var p = KlangGlockenModell.Pegel(d, false);
                Pruefe.Wahr(p < vorher, $"{d} m nicht leiser");
                vorher = p;
            }
            // je Verdopplung 9 dB
            Pruefe.Wahr(Math.Abs(KlangGlockenModell.Pegel(120f, false) - KlangGlockenModell.Pegel(60f, false) + 9f) < 0.01f, "nicht 9 dB je Verdopplung");
        }),
        ("Glocken: Dom ist lauter und trägt weiter als eine Pfarrkirche", () =>
        {
            foreach (var d in new[] { 10f, 60f, 150f, 300f, 600f })
                Pruefe.Wahr(KlangGlockenModell.Pegel(d, true) > KlangGlockenModell.Pegel(d, false), $"Dom bei {d} m nicht lauter");
            // der Dom ist bei 300 m so laut wie die Pfarrkirche bei etwa 60 m weniger Abfall: mindestens 10 dB Vorsprung
            Pruefe.Wahr(KlangGlockenModell.Pegel(300f, true) - KlangGlockenModell.Pegel(300f, false) >= 10f, "Reichweitenvorsprung des Doms unter 10 dB");
            Pruefe.Wahr(KlangGlockenModell.Tiefpass(300f, true) > KlangGlockenModell.Tiefpass(300f, false), "Dom bei 300 m nicht heller");
        }),
        ("Glocken: Summe mehrerer Kirchen ist energetisch und gedeckelt", () =>
        {
            // zwei gleiche Kirchen im selben Abstand: +3,01 dB (Leistungssumme), nicht +6
            var eine = KlangGlockenModell.Mischen(new[] { new KlangGlockenModell.Quelle(240f, 0f, false) }).Db;
            var zwei = KlangGlockenModell.Mischen(new[] { new KlangGlockenModell.Quelle(240f, 0f, false), new KlangGlockenModell.Quelle(240f, 0f, false) }).Db;
            Pruefe.Wahr(Math.Abs(zwei - eine - 3.01f) < 0.05f, $"zwei Kirchen {zwei - eine:0.00} dB lauter statt 3,01");
            // zehn Kirchen ganz nah: nie über der Obergrenze
            var nah = Enumerable.Range(0, 10).Select(_ => new KlangGlockenModell.Quelle(5f, 0f, true)).ToList();
            Pruefe.Wahr(KlangGlockenModell.Mischen(nah).Db <= KlangGlockenModell.Obergrenze, "Obergrenze überschritten");
            // eine ferne Kirche zusätzlich ändert eine nahe kaum
            var einNah = KlangGlockenModell.Mischen(new[] { new KlangGlockenModell.Quelle(30f, 0f, false) }).Db;
            var mitFern = KlangGlockenModell.Mischen(new[] { new KlangGlockenModell.Quelle(30f, 0f, false), new KlangGlockenModell.Quelle(900f, 0f, false) }).Db;
            Pruefe.Wahr(Math.Abs(einNah - mitFern) < 0.1f, "ferne Kirche verändert die nahe");
        }),
        ("Glocken: Mindestpegel im weiten Umland (leise, nicht weg)", () =>
        {
            var e = KlangGlockenModell.Mischen(new[] { new KlangGlockenModell.Quelle(5000f, 0f, true) });
            Pruefe.Wahr(e.Db == KlangGlockenModell.Mindestpegel, $"{e.Db} dB statt Mindestpegel");
            Pruefe.Wahr(e.Tiefpass <= KlangGlockenModell.TiefpassFern + 1f && e.Tiefpass > 1000f, $"Tiefpass {e.Tiefpass}");
        }),
        ("Glocken: Seite folgt der lautesten Kirche, ohne Kirche Ersatz in der Mitte", () =>
        {
            var e = KlangGlockenModell.Mischen(new[] { new KlangGlockenModell.Quelle(40f, -0.8f, false), new KlangGlockenModell.Quelle(800f, 0.9f, false) });
            Pruefe.Wahr(e.Seite < -0.6f, $"Seite {e.Seite}");
            var leer = KlangGlockenModell.Mischen(Array.Empty<KlangGlockenModell.Quelle>());
            Pruefe.Wahr(leer.Seite == 0f && leer.Db <= 0f && leer.Db > -10f, $"Ersatz {leer.Db} dB");
        }),
    };
}
