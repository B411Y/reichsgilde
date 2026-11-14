using Reichsgilde;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Tests des Wolfsheulens bei Einbruch der Nacht (<c>KlangUmgebungModell.WolfPlaner</c>, Spielordner, kennt kein Godot): einmal je Spieltag und Jahreszeit,
/// nicht am Tag, nicht nach dem Laden mitten in der Nacht, nicht im Hintergrund, nicht beim Springen, nicht gestapelt im Zeitraffer.
/// </summary>
static class KlangWolfTests
{
    static readonly Jahreszeit[] Zeiten = { Jahreszeit.Fruehling, Jahreszeit.Sommer, Jahreszeit.Herbst, Jahreszeit.Winter };

    static KlangUmgebungModell.Eingaben Eingabe(float uhrMinute, Jahreszeit j, bool hintergrund = false) =>
        new(uhrMinute, KlangUmgebungModell.BezugsStunde(uhrMinute / 60f, Sonnenzeiten.Fuer(j)), j, 40, 140, KlangUmgebungModell.Innenart.Draussen, false, 0, hintergrund);

    /// <summary>Lässt die Uhr in Schritten von <paramref name="schritt"/> Spielminuten laufen und zählt die Heulen; <paramref name="ausgabe"/> kann die Blende setzen.</summary>
    static int Lauf(KlangUmgebungModell.WolfPlaner p, Jahreszeit j, float von, float bis, float schritt, KlangUmgebungModell.Ausgabe? ausgabe = null)
    {
        var neu = new List<KlangUmgebungModell.Wolf>();
        for (var m = von; m <= bis + 0.001f; m += schritt) p.Schritt(Eingabe(m, j), ausgabe ?? new KlangUmgebungModell.Ausgabe(), neu);
        return neu.Count;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Wolf: genau einmal je Spieltag, in jeder Jahreszeit, ganz ohne Sprung", () =>
        {
            foreach (var j in Zeiten)
                Pruefe.Gleich(1, Lauf(new KlangUmgebungModell.WolfPlaner(1), j, 4 * 60, 23 * 60 - 1, 0.8f), $"{j}: Heulen je Tag");
        }),
        ("Wolf: heult beim Einbruch der Nacht (Bezug 21 = dunkel), nicht früher und nicht am Tag", () =>
        {
            foreach (var j in Zeiten)
            {
                var dunkel = Sonnenzeiten.Fuer(j).Dunkel;
                var p = new KlangUmgebungModell.WolfPlaner(2);
                Pruefe.Gleich(0, Lauf(p, j, 4 * 60, dunkel - 2, 0.8f), $"{j}: Heulen vor dem Dunkelwerden");
                Pruefe.Gleich(1, Lauf(p, j, dunkel - 1, dunkel + 3, 0.8f), $"{j}: kein Heulen beim Dunkelwerden");
            }
        }),
        ("Wolf: nicht nach dem Laden mitten in der Nacht und nicht am Tag", () =>
        {
            var neu = new List<KlangUmgebungModell.Wolf>();
            var p = new KlangUmgebungModell.WolfPlaner(3);
            p.Schritt(Eingabe(22 * 60, Jahreszeit.Fruehling), new KlangUmgebungModell.Ausgabe(), neu); // erste Messung: nur Ausgangspunkt
            p.Schritt(Eingabe(22 * 60 + 1, Jahreszeit.Fruehling), new KlangUmgebungModell.Ausgabe(), neu);
            Pruefe.Gleich(0, neu.Count, "geladen um 22 Uhr");
            Pruefe.Gleich(0, Lauf(new KlangUmgebungModell.WolfPlaner(3), Jahreszeit.Fruehling, 10 * 60, 16 * 60, 0.8f), "am Tag");
        }),
        ("Wolf: nicht doppelt nach dem Laden kurz nach dem Heulen und nicht bei Rückdrehen am selben Abend", () =>
        {
            var j = Jahreszeit.Fruehling;
            var dunkel = Sonnenzeiten.Fuer(j).Dunkel;
            var p = new KlangUmgebungModell.WolfPlaner(4);
            Pruefe.Gleich(1, Lauf(p, j, dunkel - 20, dunkel + 5, 0.8f), "erstes Mal");
            Pruefe.Gleich(0, Lauf(p, j, dunkel - 20, dunkel + 5, 0.8f), "Uhr am selben Abend zurückgedreht: kein zweites Heulen");
            // frisches Laden (neuer Planer) kurz nach dem Dunkelwerden: nichts
            Pruefe.Gleich(0, Lauf(new KlangUmgebungModell.WolfPlaner(4), j, dunkel + 4, dunkel + 30, 0.8f), "nach dem Laden");
            // nächster Tag: wieder
            Pruefe.Gleich(1, Lauf(p, j, 4 * 60, 23 * 60 - 1, 0.8f), "am nächsten Tag");
        }),
        ("Wolf: Zeitraffer stapelt nicht, auch bei großen Schritten bis zur Sprunggrenze", () =>
        {
            foreach (var schritt in new[] { 5f, 12f, 19.9f })
                foreach (var j in Zeiten)
                    Pruefe.Wahr(Lauf(new KlangUmgebungModell.WolfPlaner(5), j, 4 * 60, 23 * 60 - 1, schritt) <= 1, $"{j}, Schritt {schritt}: mehr als ein Heulen");
        }),
        ("Wolf: ein Sprung der Uhr über die Dämmerung (Spulen, Überspringen) heult nicht", () =>
        {
            var j = Jahreszeit.Fruehling;
            var dunkel = Sonnenzeiten.Fuer(j).Dunkel;
            var neu = new List<KlangUmgebungModell.Wolf>();
            var p = new KlangUmgebungModell.WolfPlaner(6);
            p.Schritt(Eingabe(dunkel - 120, j), new KlangUmgebungModell.Ausgabe(), neu);
            p.Schritt(Eingabe(dunkel + 30, j), new KlangUmgebungModell.Ausgabe(), neu);
            Pruefe.Gleich(0, neu.Count, "Sprung über 150 Spielminuten");
            // Sprungblende (Laden, Jahreszeitwechsel) und Hintergrund
            var blende = new KlangUmgebungModell.Ausgabe { Sprungblende = true };
            Pruefe.Gleich(0, Lauf(new KlangUmgebungModell.WolfPlaner(6), j, dunkel - 5, dunkel + 5, 1f, blende), "in der Sprungblende");
            var q = new KlangUmgebungModell.WolfPlaner(6);
            q.Schritt(Eingabe(dunkel - 3, j, true), new KlangUmgebungModell.Ausgabe(), neu);
            q.Schritt(Eingabe(dunkel + 1, j, true), new KlangUmgebungModell.Ausgabe(), neu);
            Pruefe.Gleich(0, neu.Count, "im Hintergrund");
        }),
        ("Wolf: fern und leise genug, Seite seitlich, Datei liegt im Spielordner und ist nur ein Heulen", () =>
        {
            var p = new KlangUmgebungModell.WolfPlaner(7);
            var gesehen = new HashSet<int>();
            for (var i = 0; i < 40; i++)
            {
                var w = p.Erzwingen();
                Pruefe.Wahr(Math.Abs(w.Seite) >= 0.5f && Math.Abs(w.Seite) <= 0.9f, $"Seite {w.Seite}");
                Pruefe.Wahr(w.Db <= -3f && w.Db > -20f && Math.Abs(w.Tonhoehe - 1f) <= 0.02f, "Pegel oder Tonhöhe");
                gesehen.Add(w.Seite < 0 ? -1 : 1);
            }
            Pruefe.Gleich(2, gesehen.Count, "beide Seiten kommen vor");
            var ordner = new DirectoryInfo(AppContext.BaseDirectory);
            string? pfad = null;
            while (ordner is not null && pfad is null)
            {
                var kandidat = Path.Combine(ordner.FullName, "game", "assets", "audio", "umgebung", "rufe", KlangUmgebungModell.WolfPlaner.Datei + ".ogg");
                if (File.Exists(kandidat)) pfad = kandidat;
                ordner = ordner.Parent;
            }
            Pruefe.Wahr(pfad is not null, "wolf_nacht.ogg fehlt");
        }),
    };
}
