using Reichsgilde;
using Reichsgilde.Core;
using Reichsgilde.Core.Zeit;
using M = Reichsgilde.MusikModell;

/// <summary>
/// Tests der dynamischen Musik (Spielordner <c>MusikModell.cs</c>, hier mitübersetzt – sie kennt kein Godot): Daten (<c>daten/musik.json</c> und die Dateien),
/// Lage Tag/Nacht aus den Spielzeit-Konstanten, Auswahl (Jahreszeit, Volk, nie dasselbe Stück zweimal), Pausen je Häufigkeit und der Ablauf der Steuerung
/// über Stunden Echtzeit (Pausen, Überblenden bei Lagewechsel, Ereignisse mit Vorrang und danach wieder die Grundmusik).
/// </summary>
static class MusikTests
{
    internal static string Spielordner(params string[] teile)
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null)
        {
            var pfad = Path.Combine(new[] { ordner.FullName, "game" }.Concat(teile).ToArray());
            if (File.Exists(pfad) || Directory.Exists(pfad)) return pfad;
            ordner = ordner.Parent;
        }
        throw new FileNotFoundException("game/" + string.Join("/", teile) + " wurde nicht gefunden.");
    }

    internal static (List<M.Stueck> Stuecke, string Menue) Liste() => M.Laden(File.ReadAllText(Spielordner("daten", "musik.json")));

    /// <summary>Spielt die Steuerung in Schritten von <c>dt</c> Sekunden durch; ein „Spieler“ lässt jedes Stück seine Dauer laufen.</summary>
    internal sealed class Lauf
    {
        public readonly M.Steuerung Steuerung;
        public readonly List<(double Zeit, M.Befehl Befehl)> Befehle = new();
        public readonly List<(double Start, double Ende, M.Stueck Stueck, M.Anlass Anlass)> Gespielt = new();
        public double Zeit;
        public double Uhr = 12 * 60;
        public M.Haeufigkeit Haeufigkeit = M.Haeufigkeit.Immer;
        public M.Anlass Zustand;
        public Jahreszeit Jahreszeit = Jahreszeit.Fruehling;
        public Volk? Volk = Reichsgilde.Core.Volk.Elb;
        public M.Berufsgruppe Beruf = M.Berufsgruppe.Keine;
        public string? Glaube, KirchenGlaube;
        public M.Ort Ort = M.Ort.Draussen;
        public bool Messe;
        double _endeZeit = double.MaxValue;
        (double Start, M.Stueck Stueck, M.Anlass Anlass)? _aktuell;

        public Lauf(IReadOnlyList<M.Stueck> stuecke, int saat = 1)
        {
            var r = new Random(saat);
            Steuerung = new M.Steuerung(stuecke, r.NextDouble);
        }

        public void Schritte(double sekunden, double dt = 0.5)
        {
            for (var t = 0.0; t < sekunden; t += dt)
            {
                Zeit += dt;
                var zuEnde = Zeit >= _endeZeit && _aktuell is not null;
                if (zuEnde)
                {
                    Gespielt.Add((_aktuell!.Value.Start, _endeZeit, _aktuell.Value.Stueck, _aktuell.Value.Anlass));
                    _aktuell = null;
                    _endeZeit = double.MaxValue;
                }
                foreach (var b in Steuerung.Schritt(dt, new M.Eingabe(Uhr, Jahreszeit, Volk, Haeufigkeit, Zustand, Beruf, Glaube, Ort, KirchenGlaube, Messe), zuEnde))
                {
                    Befehle.Add((Zeit, b));
                    if (_aktuell is { } a) // abgelöst (Wechsel, Ereignis, Ausblenden)
                    {
                        Gespielt.Add((a.Start, Zeit, a.Stueck, a.Anlass));
                        _aktuell = null;
                        _endeZeit = double.MaxValue;
                    }
                    if (b.Art == M.BefehlArt.Starten)
                    {
                        _aktuell = (Zeit, b.Stueck!, b.Anlass);
                        _endeZeit = Zeit + b.Stueck!.Dauer;
                    }
                }
            }
        }
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Musik: die Liste (daten/musik.json) hat 48 Stücke, jede Datei liegt da, jede Lage ist besetzt, das Menüstück steht darin", () =>
        {
            var (s, menue) = Liste();
            Pruefe.Gleich(48, s.Count);
            foreach (var x in s) Pruefe.Wahr(File.Exists(Spielordner("assets", "audio", "musik", x.Datei.Replace('/', Path.DirectorySeparatorChar))), x.Datei + " fehlt");
            Pruefe.Wahr(s.Any(x => x.Datei == menue), "Menüstück");
            Pruefe.Wahr(s.All(x => (x.Lage is "tag" or "nacht" or "tod" or "event" or "glaube" or "menue") && x.Dauer > 10), "Lage/Dauer");
            foreach (var a in Enum.GetValues<M.Anlass>().Where(a => a != M.Anlass.Keiner)) Pruefe.Wahr(M.AnlassKandidaten(s, a).Count > 0, "Anlass " + a);
            Pruefe.Wahr(M.NachtKandidaten(s).Count >= 2, "Nacht");
            Pruefe.Wahr(s.Count(x => x.Volk is not null) == 8, "acht Volksstücke (je Volk zwei)");
        }),

        ("Musik: kaputte oder leere Liste ergibt keine Stücke und wirft nicht", () =>
        {
            Pruefe.Gleich(0, M.Laden("{ kaputt").Stuecke.Count);
            Pruefe.Gleich(0, M.Laden("").Stuecke.Count);
            Pruefe.Gleich(0, M.Laden("{}").Stuecke.Count);
        }),

        ("Musik: Tag und Nacht nach den Konstanten der Spielzeit (Nacht ab der dunklen Nacht 20 Uhr bis zum Morgen 6 Uhr)", () =>
        {
            Pruefe.Wahr(!M.IstNacht(12 * 60), "Mittag");
            Pruefe.Wahr(!M.IstNacht(19 * 60 + 59), "19:59");
            Pruefe.Wahr(M.IstNacht(20 * 60), "20:00");
            Pruefe.Wahr(M.IstNacht(22 * 60 + 59), "22:59");
            Pruefe.Wahr(M.IstNacht(Spielzeit.TagesbeginnMinute), "04:00 (Tagesbeginn, noch dunkel)");
            Pruefe.Wahr(M.IstNacht(5 * 60 + 59), "05:59");
            Pruefe.Wahr(!M.IstNacht(6 * 60), "06:00");
            Pruefe.Wahr(!M.IstNacht(8 * 60 + 0.5), "08:00");
        }),

        ("Musik: Tagesstücke = ohne Jahreszeit und Volk + die der Jahreszeit + das des eigenen Volkes", () =>
        {
            var (s, _) = Liste();
            var fruehElb = M.TagKandidaten(s, Jahreszeit.Fruehling, Volk.Elb);
            Pruefe.Gleich(10, fruehElb.Count); // 5 neutrale + 3 Frühling + 2 Elben
            Pruefe.Wahr(fruehElb.Count(x => x.Volk == "Elben") == 2 && fruehElb.Count(x => x.Zusatz == "fruehling") == 3, "Elb, Frühling");
            Pruefe.Wahr(fruehElb.All(x => x.Zusatz is null or "fruehling") && fruehElb.All(x => x.Beruf is null && x.Glaube is null), "nur Frühling, ohne Beruf und Glaube");
            Pruefe.Gleich(7, M.TagKandidaten(s, Jahreszeit.Sommer, Volk.Ork).Count); // 5 + 2 Sommer, kein Volk
            Pruefe.Gleich(10, M.TagKandidaten(s, Jahreszeit.Winter, Volk.Zwerg).Count); // 5 + 3 Winter + 2 Zwerge
            Pruefe.Wahr(M.TagKandidaten(s, Jahreszeit.Herbst, Volk.Mensch).Any(x => x.Volk == "Menschen"), "Menschen");
            Pruefe.Wahr(M.TagKandidaten(s, Jahreszeit.Herbst, null).All(x => x.Volk is null), "ohne Volk");
        }),

        ("Musik: nie dasselbe Stück zweimal hintereinander, auch bei nur zwei Kandidaten oder einem", () =>
        {
            var (s, _) = Liste();
            var r = new Random(5);
            foreach (var kandidaten in new[] { M.TagKandidaten(s, Jahreszeit.Sommer, null), M.NachtKandidaten(s), M.AnlassKandidaten(s, M.Anlass.Sklaven) })
            {
                var zuletzt = new List<string>();
                string? vorher = null;
                for (var i = 0; i < 300; i++)
                {
                    var wahl = M.Waehlen(kandidaten, zuletzt, r.NextDouble)!;
                    if (kandidaten.Count > 1) Pruefe.Wahr(wahl.Datei != vorher, "doppelt: " + wahl.Datei);
                    vorher = wahl.Datei;
                    zuletzt.Add(wahl.Datei);
                }
                Pruefe.Wahr(zuletzt.Distinct().Count() == kandidaten.Count, "jedes kommt vor");
            }
            Pruefe.Gleich((M.Stueck?)null, M.Waehlen(new List<M.Stueck>(), new List<string>(), () => 0.5));
        }),

        ("Musik: Pausen nach der Häufigkeit (immer 1–3 min, oft 3–6, selten 8–15), Überblendung 4–8 s", () =>
        {
            Pruefe.Gleich((60.0, 180.0), M.PauseGrenzen(M.Haeufigkeit.Immer));
            Pruefe.Ungefaehr(60, M.PauseSekunden(M.Haeufigkeit.Immer, 0), 1e-9);
            Pruefe.Ungefaehr(180, M.PauseSekunden(M.Haeufigkeit.Immer, 1), 1e-9);
            Pruefe.Ungefaehr(900, M.PauseSekunden(M.Haeufigkeit.Selten, 1), 1e-9);
            Pruefe.Wahr(M.PauseSekunden(M.Haeufigkeit.Oft, 0.5) is > 180 and < 360, "oft");
            Pruefe.Ungefaehr(4, M.WechselBlende(0), 1e-9);
            Pruefe.Ungefaehr(8, M.WechselBlende(1), 1e-9);
        }),

        ("Musik: Ablauf über 6 Stunden Echtzeit – Pause zwischen den Stücken, nie dasselbe zweimal, Nacht nur Nachtstücke", () =>
        {
            var (s, _) = Liste();
            var l = new Lauf(s);
            l.Schritte(3 * 3600);
            var zeitNacht = l.Zeit;
            l.Uhr = 22 * 60;
            l.Schritte(3 * 3600);
            var g = l.Gespielt.Where(x => x.Anlass == M.Anlass.Keiner).ToList();
            Pruefe.Wahr(g.Count > 40, "zu wenige Stücke: " + g.Count);
            for (var i = 1; i < g.Count; i++)
            {
                Pruefe.Wahr(g[i].Stueck.Datei != g[i - 1].Stueck.Datei, "doppelt");
                // zwischen zwei Stücken liegt mindestens die kürzeste Pause (1 min), außer das Stück wurde durch den Lagewechsel abgelöst
                if (g[i].Start > g[i - 1].Ende + 0.01 && g[i - 1].Ende < zeitNacht - 1 == g[i].Start < zeitNacht)
                    Pruefe.Wahr(g[i].Start - g[i - 1].Ende >= 59, $"Pause {g[i].Start - g[i - 1].Ende:0} s zu kurz");
            }
            Pruefe.Wahr(g.Where(x => x.Start > zeitNacht + 20).All(x => x.Stueck.Lage == "nacht"), "Nachts nur Nachtstücke");
            Pruefe.Wahr(g.Where(x => x.Start < zeitNacht).All(x => x.Stueck.Lage == "tag" && (x.Stueck.Zusatz is null or "fruehling") && (x.Stueck.Volk is null or "Elben")), "Tag: Frühling und Elben");
        }),

        ("Musik: Wechsel Tag zu Nacht blendet 4–8 s über (altes Stück aus, neues Nachtstück ein)", () =>
        {
            var (s, _) = Liste();
            var l = new Lauf(s, 3);
            l.Schritte(30); // erstes Tagesstück läuft
            Pruefe.Wahr(l.Steuerung.Laufend is { Lage: "tag" }, "Tag läuft");
            var vorher = l.Befehle.Count;
            l.Uhr = 20 * 60 + 5;
            l.Schritte(1);
            var wechsel = l.Befehle.Skip(vorher).First().Befehl;
            Pruefe.Wahr(wechsel.Art == M.BefehlArt.Starten && wechsel.Wechsel && wechsel.Stueck!.Lage == "nacht", "Nachtstück");
            Pruefe.Wahr(wechsel.Einblende is >= 4 and <= 8 && wechsel.Einblende == wechsel.AltAus, $"Blende {wechsel.Einblende:0.0}");
        }),

        ("Musik: in der Pause bleibt der Lagewechsel still (ein Stück der neuen Lage beginnt erst nach der Pause)", () =>
        {
            var (s, _) = Liste();
            var l = new Lauf(s, 4) { Haeufigkeit = M.Haeufigkeit.Selten };
            l.Schritte(10);
            while (l.Steuerung.Laufend is not null) l.Schritte(5);
            Pruefe.Wahr(l.Steuerung.PauseRest > 30, "in der Pause");
            var n = l.Befehle.Count;
            l.Uhr = 21 * 60;
            l.Schritte(10);
            Pruefe.Gleich(n, l.Befehle.Count);
            while (l.Steuerung.Laufend is null) l.Schritte(5);
            Pruefe.Gleich("nacht", l.Steuerung.Laufend!.Lage);
        }),

        ("Musik: ein Ereignis (Hochzeit) blendet die Grundmusik aus, spielt zu Ende, danach wieder Grundmusik; Sperre gegen Wiederholung", () =>
        {
            var (s, _) = Liste();
            var l = new Lauf(s, 6);
            l.Schritte(60);
            l.Steuerung.Melden(M.Anlass.Hochzeit);
            l.Schritte(1);
            var b = l.Befehle.Last().Befehl;
            Pruefe.Wahr(b.Art == M.BefehlArt.Starten && b.Anlass == M.Anlass.Hochzeit && b.Stueck!.Zusatz == "hochzeit", "Hochzeitsstück");
            Pruefe.Wahr(b.AltAus > 0, "Grundmusik wird ausgeblendet");
            Pruefe.Gleich(M.Anlass.Hochzeit, l.Steuerung.LaufenderAnlass);
            l.Steuerung.Melden(M.Anlass.Hochzeit); // zweite Meldung: kein zweites Stück
            l.Schritte(b.Stueck!.Dauer + 30);
            Pruefe.Wahr(l.Steuerung.Laufend is { } n && n.Lage == "tag", "danach wieder Grundmusik");
            l.Steuerung.Melden(M.Anlass.Hochzeit);
            var anzahl = l.Gespielt.Count(x => x.Anlass == M.Anlass.Hochzeit);
            l.Schritte(5);
            Pruefe.Gleich(anzahl, l.Gespielt.Count(x => x.Anlass == M.Anlass.Hochzeit) + (l.Steuerung.LaufenderAnlass == M.Anlass.Hochzeit ? 1 : 0)); // gesperrt: es läuft keine neue
            Pruefe.Wahr(l.Steuerung.LaufenderAnlass != M.Anlass.Hochzeit, "gesperrt");
        }),

        ("Musik: Tod geht vor Hochzeit; Hochzeit unterbricht den Tod nicht", () =>
        {
            var (s, _) = Liste();
            var l = new Lauf(s, 7);
            l.Schritte(20);
            l.Steuerung.Melden(M.Anlass.Hochzeit);
            l.Schritte(1);
            Pruefe.Gleich(M.Anlass.Hochzeit, l.Steuerung.LaufenderAnlass);
            l.Steuerung.Melden(M.Anlass.Tod);
            l.Schritte(1);
            Pruefe.Gleich(M.Anlass.Tod, l.Steuerung.LaufenderAnlass);
            Pruefe.Gleich("tod", l.Steuerung.Laufend!.Lage);
            l.Steuerung.Melden(M.Anlass.Hochzeit);
            l.Schritte(30);
            Pruefe.Gleich(M.Anlass.Tod, l.Steuerung.LaufenderAnlass);
        }),

        ("Musik: Zustand Sklaven spielt, solange er gilt (mindestens 20 s), und endet 6 s weich danach; Aufstand geht vor Sklaven", () =>
        {
            var (s, _) = Liste();
            var l = new Lauf(s, 8);
            l.Schritte(20);
            l.Zustand = M.Anlass.Sklaven;
            l.Schritte(2);
            Pruefe.Gleich(M.Anlass.Sklaven, l.Steuerung.LaufenderAnlass);
            l.Zustand = M.Anlass.Keiner;
            l.Schritte(5); // unter der Mindestzeit: läuft weiter
            Pruefe.Gleich(M.Anlass.Sklaven, l.Steuerung.LaufenderAnlass);
            l.Schritte(25);
            Pruefe.Gleich(M.Anlass.Keiner, l.Steuerung.LaufenderAnlass);
            Pruefe.Wahr(l.Befehle.Any(x => x.Befehl.Art == M.BefehlArt.Ausblenden && x.Befehl.AltAus == 6), "6 s ausblenden");
            var l2 = new Lauf(s, 9);
            l2.Schritte(20);
            l2.Zustand = M.Anlass.Sklaven;
            l2.Schritte(2);
            l2.Zustand = M.Anlass.Aufstand;
            l2.Schritte(30);
            Pruefe.Gleich(M.Anlass.Aufstand, l2.Steuerung.LaufenderAnlass);
        }),

        ("Musik: „nur Ereignisse“ spielt keine Grundmusik, aber Ereignisse; „aus“ nichts und blendet Laufendes weich aus", () =>
        {
            var (s, _) = Liste();
            var l = new Lauf(s, 10) { Haeufigkeit = M.Haeufigkeit.NurEreignisse };
            l.Schritte(1200);
            Pruefe.Gleich(0, l.Befehle.Count);
            l.Steuerung.Melden(M.Anlass.Kampf);
            l.Schritte(2);
            Pruefe.Gleich(M.Anlass.Kampf, l.Steuerung.LaufenderAnlass);
            var aus = new Lauf(s, 11) { Haeufigkeit = M.Haeufigkeit.Aus };
            aus.Schritte(1200);
            aus.Steuerung.Melden(M.Anlass.Tod);
            aus.Schritte(60);
            Pruefe.Gleich(0, aus.Befehle.Count);
            var l3 = new Lauf(s, 12);
            l3.Schritte(30);
            l3.Haeufigkeit = M.Haeufigkeit.Aus;
            l3.Schritte(1);
            Pruefe.Wahr(l3.Befehle.Last().Befehl.Art == M.BefehlArt.Ausblenden, "ausgeblendet");
        }),

        ("Musik: ohne Stücke (Dateien fehlen) passiert nichts, es wirft nichts", () =>
        {
            var l = new Lauf(new List<M.Stueck>(), 13);
            l.Schritte(900);
            l.Steuerung.Melden(M.Anlass.Tod);
            l.Zustand = M.Anlass.Sklaven;
            l.Schritte(900);
            Pruefe.Gleich(0, l.Befehle.Count);
        }),

        ("Musik: Meldungen des Kerns ergeben die Anlässe (Tod, Heirat, Geburt, Einbruch, Überfall/Feuer = Gefahr), sonst keiner", () =>
        {
            Pruefe.Gleich(M.Anlass.Tod, M.AnlassAusAnsage(KlangAnsagenErkennung.Id.TodOberhaupt));
            Pruefe.Gleich(M.Anlass.Tod, M.AnlassAusAnsage(KlangAnsagenErkennung.Id.TodFamilie));
            Pruefe.Gleich(M.Anlass.Hochzeit, M.AnlassAusAnsage(KlangAnsagenErkennung.Id.Heirat));
            Pruefe.Gleich(M.Anlass.Gefahr, M.AnlassAusAnsage(KlangAnsagenErkennung.Id.Ueberfall));
            Pruefe.Gleich(M.Anlass.Einbruch, M.AnlassAusAnsage(KlangAnsagenErkennung.Id.Diebstahl));
            Pruefe.Gleich(M.Anlass.Gefahr, M.AnlassAusAnsage(KlangAnsagenErkennung.Id.Feuer));
            Pruefe.Gleich(M.Anlass.Geburt, M.AnlassAusAnsage(KlangAnsagenErkennung.Id.Geburt));
            Pruefe.Gleich(M.Anlass.Keiner, M.AnlassAusAnsage(KlangAnsagenErkennung.Id.Krankheit));
            Pruefe.Gleich(M.Anlass.Keiner, M.AnlassAusAnsage(null));
        }),
    };
}
