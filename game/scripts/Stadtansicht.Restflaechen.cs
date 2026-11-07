using System.Text.Json.Nodes;
using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die Füllung der Restflächen – ein Teil der <c>partial class Stadtansicht</c> (Plan: Paket 1 von „Restflächen füllen“, 4.11.2026).
///
/// Restflächen sind freie Rasterfelder in der Stadt, die keine Bauzone wurden (Kern: <see cref="Stadtkarte.Restflaechen"/>). Hier bekommen sie Gärten, Werkhöfe
/// und Gestrüpp – reine Darstellung ohne Wirkung aufs Spiel, gleich bei jedem Laden (alles aus Karte und Kennungen, kein Zufall).
/// <list type="number">
/// <item>Der Kern zerlegt die Felder lückenlos in Kästen der Formate 1×1 … 3×3 (<see cref="Restflaechenfuellung.Zerlegen"/>) und nennt die Nachbarart (Wohnen, Werkstatt, Rand …).</item>
/// <item>Das Thema (Garten, Werkhof, Randgestrüpp) wird je Restfläche nach den Gewichten der Nachbarart gewürfelt; die Rezepte stehen in <c>game/daten/restflaechen.json</c>
/// (je Thema und Format eine Anordnung aus Einzelteil, Reihe, Streuung oder Raster; einmal gelesen und in <see cref="Rezeptteil"/> übersetzt).</item>
/// <item>Gezeichnet wird als gebackene Netze mit einer Fläche je Stoff (<c>Stadtansicht.Fuellbacken.cs</c>, <see cref="Fuellnetz"/>), in allen drei Zoomstufen; Kleinteile hängen an der
/// Natur-Dichte, Sommerteile (Blumen) fehlen im Winter, Büsche tauschen gegen ihre kahle Fassung.</item>
/// <item>Steht auf einem Kasten ein Gebäude, fällt seine Füllung weg (<see cref="Restflaechenfuellung.Bebaut"/>).</item>
/// </list>
/// Aus mit der Startoption <c>--fuellung=0</c>.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Ein gesetztes Teil: Art (Datei, Knoten), Lage, ob es Schatten wirft, ob es ein Kleinteil ist und ob es im Winter fehlt.</summary>
    private readonly record struct Fuellteil(string Datei, string Knoten, Transform3D Lage, bool Schatten, bool Klein, bool Sommer, bool Gross);

    /// <summary>Ein Teil eines Rezepts, aus der Datei übersetzt (Maße in Metern im Kasten, x entlang der langen Seite).</summary>
    private sealed class Rezeptteil
    {
        public string Art = "punkt", Datei = "";
        public string[] Knoten = Array.Empty<string>();
        public Vector2 Ort, Von, Bis, FlaecheA, FlaecheB;
        public float? Dreh;
        public float GroesseVon = 1, GroesseBis = 1, Schritt = 1, Abstand = 1;
        public int Anzahl = 1;
        public bool Schatten, Klein, Sommer, Gross;
    }

    /// <summary>Die Rezepte: Nachbarart → Themen (nach Namen sortiert) mit Gewichten, Thema → Format → Teile, dazu die benutzten Baukästen.</summary>
    private sealed class Fuellrezepte
    {
        public readonly Dictionary<string, (string[] Themen, int[] Gewichte)> Nachbarn = new();
        public readonly Dictionary<string, Dictionary<string, List<Rezeptteil>>> Themen = new();
        public readonly SortedSet<string> Baukaesten = new(StringComparer.Ordinal);
        /// <summary>Thema → größte Fläche (in Feldern), auf der es noch gewählt wird (der Bildstock steht nur an kleinen Ecken, nicht auf großen Plätzen).</summary>
        public readonly Dictionary<string, int> HoechstensFelder = new();
        /// <summary>Thema → Varianten des Untergrunds (je Variante die Stempel, siehe Stadtansicht.Bodenstempel.cs); ein Thema ohne Eintrag lässt den Boden, wie er ist.</summary>
        public readonly Dictionary<string, List<List<StempelAngabe>>> Untergruende = new();
        /// <summary>Stoffname → Ersatzstoff für die weit entfernten Bildnetze (Zoom 70 und 140): seltene oder nur um einen Farbton verschiedene Stoffe nehmen einen häufigen, damit die ganze Füllung höchstens rund 30 Flächen (Zeichenaufrufe ×2 durch den Tiefenvorlauf) hat.</summary>
        public readonly Dictionary<string, string> FernStoffe = new(StringComparer.Ordinal);
    }

    /// <summary>Was zuletzt für die Karte gesetzt wurde: je Restfläche die Kästen mit ihren Teilen (für den Neuaufbau, wenn gebaut wurde).</summary>
    private readonly SortedDictionary<string, List<(Fuellkasten Kasten, List<Fuellteil> Teile)>> _fuellFlaechen = new(StringComparer.Ordinal);
    private Dictionary<string, string>? _fuellThemen;

    private static Fuellrezepte? _fuellRezepte;

    private static bool FuellungAus => Wasserarten.Startoption("fuellung") is "0" or "aus" or "nein";

    /// <summary>Liest die Rezepte einmal; null, wenn die Datei fehlt oder kaputt ist (dann bleibt alles leer).</summary>
    private static Fuellrezepte? FuellRezepte()
    {
        if (_fuellRezepte is not null) return _fuellRezepte;
        const string pfad = "res://daten/restflaechen.json";
        if (!Godot.FileAccess.FileExists(pfad)) { GD.PushWarning("Restflächen: " + pfad + " fehlt."); return null; }
        try
        {
            var wurzel = JsonNode.Parse(Godot.FileAccess.GetFileAsString(pfad))!;
            var modelle = wurzel["modelle"]!.AsObject();
            var r = new Fuellrezepte();
            foreach (var (art, gewichte) in wurzel["nachbarn"]!.AsObject())
            {
                var namen = gewichte!.AsObject().Select(g => g.Key).OrderBy(n => n, StringComparer.Ordinal).ToArray(); // feste Reihenfolge, unabhängig von der Datei
                r.Nachbarn[art] = (namen, namen.Select(n => gewichte[n]!.GetValue<int>()).ToArray());
            }
            foreach (var (thema, inhalt) in wurzel["themen"]!.AsObject())
            {
                var formate = new Dictionary<string, List<Rezeptteil>>();
                foreach (var (format, teile) in inhalt!["formate"]!.AsObject())
                    formate[format] = teile!.AsArray().Select(t => TeilLesen(t!.AsObject(), modelle, r.Baukaesten)).ToList();
                r.Themen[thema] = formate;
            }
            if (wurzel["hoechstens_felder"] is JsonObject grenzen)
                foreach (var (thema, felder) in grenzen) r.HoechstensFelder[thema] = felder!.GetValue<int>();
            if (wurzel["fern_stoffe"] is JsonObject fern)
                foreach (var (stoff, ersatz) in fern) r.FernStoffe[stoff] = ersatz!.GetValue<string>();
            if (wurzel["untergruende"] is JsonObject untergruende)
                foreach (var (thema, varianten) in untergruende)
                    r.Untergruende[thema] = varianten!.AsArray().Select(v => StempelLesen(v!.AsArray(), wurzel["standard_untergrund"] as JsonObject)).ToList();
            return _fuellRezepte = r;
        }
        catch (Exception e) { GD.PushError("Restflächen: " + pfad + " ist kaputt: " + e.Message); return null; }
    }

    private static Rezeptteil TeilLesen(JsonObject t, JsonObject modelle, SortedSet<string> baukaesten)
    {
        var kurz = t["m"]!.GetValue<string>();
        var datei = modelle[kurz]?.GetValue<string>() ?? kurz;
        baukaesten.Add(datei);
        var groesse = t["groesse"];
        var teil = new Rezeptteil
        {
            Art = t["art"]?.GetValue<string>() ?? "punkt",
            Datei = datei,
            Knoten = t["k"] is JsonArray a ? a.Select(n => n!.GetValue<string>()).ToArray() : new[] { t["k"]!.GetValue<string>() },
            Ort = new Vector2(t["x"]?.GetValue<float>() ?? 0, t["z"]?.GetValue<float>() ?? 0),
            Von = Punkt(t["von"]), Bis = Punkt(t["bis"]),
            Dreh = t["dreh"]?.GetValue<float>(),
            Schritt = Math.Max(0.1f, t["schritt"]?.GetValue<float>() ?? 1f),
            Abstand = Math.Max(0.2f, t["abstand"]?.GetValue<float>() ?? 1f),
            Anzahl = t["anzahl"]?.GetValue<int>() ?? 1,
            Schatten = t["schatten"]?.GetValue<bool>() ?? false,
            Sommer = t["winter"]?.GetValue<string>() == "weg",
            Gross = t["gross"]?.GetValue<bool>() ?? false,
        };
        if (t["flaeche"] is JsonArray { Count: >= 4 } f)
            (teil.FlaecheA, teil.FlaecheB) = (new Vector2(f[0]!.GetValue<float>(), f[1]!.GetValue<float>()), new Vector2(f[2]!.GetValue<float>(), f[3]!.GetValue<float>()));
        (teil.GroesseVon, teil.GroesseBis) = groesse switch
        {
            JsonArray { Count: >= 2 } g => (g[0]!.GetValue<float>(), g[1]!.GetValue<float>()),
            null => (1f, 1f),
            _ => (groesse.GetValue<float>(), groesse.GetValue<float>()),
        };
        // Kleinteil: Boden, Steine, Holz (wie die Feinheiten der Natur), sonst nur, wenn das Rezept es sagt
        teil.Klein = t["klein"]?.GetValue<bool>() ?? (IstBodenDatei(datei) || datei is "natur/natur_steine" or "natur/natur_holz" or "natur/natur_kit_steine");
        return teil;
    }

    private static Vector2 Punkt(JsonNode? n) => n is JsonArray { Count: >= 2 } a ? new Vector2(a[0]!.GetValue<float>(), a[1]!.GetValue<float>()) : Vector2.Zero;

    /// <summary>Welches Thema eine Restfläche hat (Kennung → Thema) – für Messung und Klicktest.</summary>
    internal IReadOnlyDictionary<string, string> FuellThemen => _fuellThemen ?? new Dictionary<string, string>();

    /// <summary>Wie viele Kästen und Teile die Füllung hat (für die Messausgabe).</summary>
    internal (int Kaesten, int Teile) FuellZahlen => (_fuellFlaechen.Values.Sum(l => l.Count), _fuellFlaechen.Values.Sum(l => l.Sum(k => k.Teile.Count)));

    /// <summary>
    /// Zu Beginn des Kartenaufbaus: lässt die Baukästen der Rezepte schon im Hintergrund laden (<see cref="ResourceLoader.LoadThreadedRequest"/>); sie brauchen
    /// beim ersten Mal rund eine Sekunde, und der Aufbau der Stadt hat bis zur Füllung genug zu tun. <see cref="Modell"/> bekommt sie später aus dem Speicher.
    /// </summary>
    private void RestflaechenVorladen(Stadtkarte karte)
    {
        if (FuellungAus || karte.Restflaechen is not { Count: > 0 } || FuellRezepte() is not { } rezepte) return;
        foreach (var datei in rezepte.Baukaesten)
        {
            var pfad = $"res://assets/modelle/{datei}.glb";
            if (_modellSzenen.ContainsKey(datei) || !ResourceLoader.Exists(pfad)) continue;
            ResourceLoader.LoadThreadedRequest(pfad);
            _fuellVorgeladen.Add(datei);
        }
    }

    /// <summary>Die Baukästen, die <see cref="RestflaechenVorladen"/> angefordert hat.</summary>
    private readonly List<string> _fuellVorgeladen = new();

    /// <summary>
    /// Holt die vorgeladenen Baukästen ab (<see cref="ResourceLoader.LoadThreadedGet"/>) und merkt sie für <see cref="Modell"/>. Jede Anforderung muss abgeholt werden, auch wenn die Karte
    /// nichts daraus braucht – sonst bliebe sie beim Beenden als Leck stehen („ObjectDB instances were leaked“, Rauchtest der CI).
    /// </summary>
    private void FuellVorgeladenAbholen()
    {
        foreach (var datei in _fuellVorgeladen)
        {
            var szene = ResourceLoader.LoadThreadedGet($"res://assets/modelle/{datei}.glb") as PackedScene; // immer abholen, auch wenn Modell() die Datei schon geholt hat
            if (!_modellSzenen.ContainsKey(datei)) _modellSzenen[datei] = szene;
        }
        _fuellVorgeladen.Clear();
    }

    /// <summary>Beim Aufbau der Karte (nach der Natur): setzt die Füllung aller Restflächen.</summary>
    private void RestflaechenAusKarte(Stadtkarte karte)
    {
        _fuellFlaechen.Clear();
        _fuellThemen = new();
        if (FuellungAus || karte.Restflaechen is not { Count: > 0 } liste || FuellRezepte() is not { } rezepte) return;
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        foreach (var r in liste)
        {
            if (FuellThemaWaehlen(r, karte, rezepte) is not { } thema || !rezepte.Themen.TryGetValue(thema, out var formate)) continue;
            _fuellThemen[r.Kennung] = thema;
            var kaesten = new List<(Fuellkasten, List<Fuellteil>)>();
            foreach (var kasten in Restflaechenfuellung.Zerlegen(Restflaechenfuellung.Felder(r)))
                kaesten.Add((kasten, formate.TryGetValue(kasten.Format, out var teile) ? KastenFuellen(teile, karte.Kennung, r.Kennung, kasten) : new List<Fuellteil>()));
            _fuellFlaechen[r.Kennung] = kaesten;
        }
        if (Wasserarten.Startoption("fuellung") == "liste") // nur für Bilder: wo welches Thema liegt
            foreach (var r in liste.Where(r => _fuellThemen.ContainsKey(r.Kennung)))
            {
                var felder = Restflaechenfuellung.Felder(r);
                GD.Print($"[fuellliste] {_fuellThemen[r.Kennung]} {r.Kennung} {Restflaechenfuellung.Nachbarart(r, karte)} Felder {felder.Count} Mitte {felder.Average(f => (f.I + 0.5) * 2):F0},{felder.Average(f => (f.J + 0.5) * 2):F0}");
            }
        FuellVorgeladenAbholen();
        var logik = uhr.ElapsedMilliseconds;
        FuellungAufbauen(karte);
        var (nk, nt) = FuellZahlen;
        GD.Print($"Restflächen: {_fuellFlaechen.Count} Flächen, {nk} Kästen, {nt} Teile; Themen: "
            + string.Join(", ", _fuellThemen.GroupBy(t => t.Value).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Count()}"))
            + $"; {logik} ms Rechnung + {uhr.ElapsedMilliseconds - logik} ms Aufbau ({_fuellZellen.Count} Zellen, gebacken wird beim Hinsehen)");
    }

    /// <summary>Das Thema einer Restfläche (Garten, Werkhof, Randgestrüpp): nach den Gewichten der Nachbarart und dem festen Würfel; null, wenn die Datei keins kennt.</summary>
    private static string? FuellThemaWaehlen(Restflaeche r, Stadtkarte karte, Fuellrezepte rezepte)
    {
        if (Wasserarten.Startoption("fuellthema") is { } erzwungen && rezepte.Themen.ContainsKey(erzwungen)) return erzwungen; // nur für Bilder: jede Fläche bekommt dieses Thema
        if (rezepte.Nachbarn.GetValueOrDefault(Restflaechenfuellung.Nachbarart(r, karte)) is not { Themen: { Length: > 0 } namen } wahl) return null;
        var gewichte = wahl.Gewichte;
        if (rezepte.HoechstensFelder.Count > 0)
        {
            var felder = Restflaechenfuellung.Felder(r).Count;
            gewichte = gewichte.Select((g, i) => rezepte.HoechstensFelder.TryGetValue(namen[i], out var grenze) && felder > grenze ? 0 : g).ToArray();
        }
        var nummer = Restflaechenfuellung.Waehlen(gewichte, Restflaechenfuellung.Wuerfel(karte.Kennung + "/" + r.Kennung, 1));
        return nummer < 0 ? null : namen[nummer];
    }

    /// <summary>Die Teile eines Kastens: Rezept des Themas für sein Format, gedreht und gewendet, in Weltlage.</summary>
    private List<Fuellteil> KastenFuellen(List<Rezeptteil> anordnung, string karte, string flaeche, Fuellkasten kasten)
    {
        var teile = new List<Fuellteil>(anordnung.Count * 3);
        var wuerfel = Restflaechenfuellung.Wuerfel(karte + "/" + flaeche, kasten.I, kasten.J);
        // Hochkant: die lange Seite liegt in z, der Satz wird um 90° gedreht; gewendet wird in 180°-Schritten, quadratische Kästen in 90°-Schritten
        var winkel = kasten.Hochkant ? Mathf.Pi / 2 : 0f;
        winkel += kasten.Breite == kasten.Tiefe ? Mathf.Pi / 2 * (wuerfel % 4) : Mathf.Pi * ((wuerfel >> 3) & 1);
        var (mx, mz) = kasten.Mitte;
        var kastenLage = new Transform3D(new Basis(Vector3.Up, winkel), new Vector3((float)mx, 0, (float)mz));
        var nummer = 0;
        foreach (var teil in anordnung)
        {
            nummer++;
            var folge = wuerfel + (uint)nummer * 7919u;
            void Setzen(int lfd, Vector2 ort, float dreh, float groesse)
            {
                var name = teil.Knoten[(int)(Restflaechenfuellung.Zufall(folge, lfd * 5 + 1) * teil.Knoten.Length)];
                var lokal = new Transform3D(new Basis(Vector3.Up, dreh).Scaled(Vector3.One * groesse), new Vector3(ort.X, 0, ort.Y));
                var welt = kastenLage * lokal;
                welt.Origin = new Vector3(welt.Origin.X, BodenHoehe(welt.Origin.X, welt.Origin.Z), welt.Origin.Z);
                teile.Add(new Fuellteil(teil.Datei, name, welt, teil.Schatten, teil.Klein, teil.Sommer, teil.Gross));
            }
            float Gr(int lfd, float streuung) =>
                Mathf.Lerp(teil.GroesseVon, teil.GroesseBis, (float)Restflaechenfuellung.Zufall(folge, lfd * 5 + 2)) * (1f + streuung * (float)(Restflaechenfuellung.Zufall(folge, lfd * 5 + 3) * 2 - 1));
            float Dreh(int lfd) => teil.Dreh is { } d ? Mathf.DegToRad(d) : (float)(Restflaechenfuellung.Zufall(folge, lfd * 5 + 4) * Mathf.Tau);
            switch (teil.Art)
            {
                case "reihe":
                {
                    var laenge = teil.Von.DistanceTo(teil.Bis);
                    var anzahl = Math.Max(1, Mathf.RoundToInt(laenge / teil.Schritt));
                    var richtung = (teil.Bis - teil.Von) / Math.Max(0.001f, laenge);
                    var entlang = Mathf.Atan2(-richtung.Y, richtung.X);
                    for (var i = 0; i < anzahl; i++)
                        Setzen(i, teil.Von + richtung * ((i + 0.5f) * laenge / anzahl), teil.Dreh is null ? entlang : Dreh(i), Gr(i, 0f));
                    break;
                }
                case "streu":
                    for (var i = 0; i < teil.Anzahl; i++)
                        Setzen(i, new Vector2(Mathf.Lerp(teil.FlaecheA.X, teil.FlaecheB.X, (float)Restflaechenfuellung.Zufall(folge, i * 5 + 100)),
                                              Mathf.Lerp(teil.FlaecheA.Y, teil.FlaecheB.Y, (float)Restflaechenfuellung.Zufall(folge, i * 5 + 101))), Dreh(i), Gr(i, 0.12f));
                    break;
                case "raster":
                {
                    var (a, b) = (teil.FlaecheA, teil.FlaecheB);
                    var (nx, nz) = (Mathf.FloorToInt((b.X - a.X) / teil.Abstand + 0.001f) + 1, Mathf.FloorToInt((b.Y - a.Y) / teil.Abstand + 0.001f) + 1);
                    var lfd = 0;
                    for (var iz = 0; iz < nz; iz++)
                        for (var ix = 0; ix < nx; ix++, lfd++)
                        {
                            var ort = new Vector2(nx == 1 ? (a.X + b.X) / 2 : a.X + ix * (b.X - a.X) / (nx - 1), nz == 1 ? (a.Y + b.Y) / 2 : a.Y + iz * (b.Y - a.Y) / (nz - 1));
                            ort += new Vector2((float)Restflaechenfuellung.Zufall(folge, lfd * 5 + 200) - 0.5f, (float)Restflaechenfuellung.Zufall(folge, lfd * 5 + 201) - 0.5f) * teil.Abstand * 0.3f;
                            Setzen(lfd, ort, Dreh(lfd), Gr(lfd, 0.1f));
                        }
                    break;
                }
                default:
                    Setzen(0, teil.Ort, Dreh(0), Gr(0, 0f));
                    break;
            }
        }
        return teile;
    }
}
