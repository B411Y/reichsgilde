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
/// <item>Gezeichnet wird wie die übrige Natur: je Art und Zelle (64 m) ein MultiMesh. Kleinteile hängen an den Natur-Details (Natur-Dichte); gezeigt wird die Füllung nur in der
/// nahen Zoomstufe (<see cref="FuellKleinBisZoom"/>), Sommerteile (Blumen) fehlen im Winter, Büsche tauschen gegen ihre kahle Fassung.</item>
/// <item>Steht auf einem Kasten ein Gebäude, fällt seine Füllung weg (<see cref="Restflaechenfuellung.Bebaut"/>).</item>
/// </list>
/// Aus mit der Startoption <c>--fuellung=0</c>.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Ein gesetztes Teil: Art (Datei, Knoten), Lage, ob es Schatten wirft, ob es ein Kleinteil ist und ob es im Winter fehlt.</summary>
    private readonly record struct Fuellteil(string Datei, string Knoten, Transform3D Lage, bool Schatten, bool Klein, bool Sommer);

    /// <summary>Ein Teil eines Rezepts, aus der Datei übersetzt (Maße in Metern im Kasten, x entlang der langen Seite).</summary>
    private sealed class Rezeptteil
    {
        public string Art = "punkt", Datei = "";
        public string[] Knoten = Array.Empty<string>();
        public Vector2 Ort, Von, Bis, FlaecheA, FlaecheB;
        public float? Dreh;
        public float GroesseVon = 1, GroesseBis = 1, Schritt = 1, Abstand = 1;
        public int Anzahl = 1;
        public bool Schatten, Klein, Sommer;
    }

    /// <summary>Die Rezepte: Nachbarart → Themen (nach Namen sortiert) mit Gewichten, Thema → Format → Teile, dazu die benutzten Baukästen.</summary>
    private sealed class Fuellrezepte
    {
        public readonly Dictionary<string, (string[] Themen, int[] Gewichte)> Nachbarn = new();
        public readonly Dictionary<string, Dictionary<string, List<Rezeptteil>>> Themen = new();
        public readonly SortedSet<string> Baukaesten = new(StringComparer.Ordinal);
        /// <summary>Thema → Varianten des Untergrunds (je Variante die Stempel, siehe Stadtansicht.Bodenstempel.cs); ein Thema ohne Eintrag lässt den Boden, wie er ist.</summary>
        public readonly Dictionary<string, List<List<StempelAngabe>>> Untergruende = new();
    }

    private const float FuellZelle = 64f;

    /// <summary>
    /// Bis zu diesem Zoom (sichtbare Höhe in Metern) zeigt die Füllung ihre Teile: Kleinteile (Beete, Gras, Steine) bis 45 m, Mittelteile (Zäune, Fässer, Büsche) bis 55 m –
    /// also nur in der nahen Pixel-Zoomstufe (35 m). Die Füllung kostet je Zelle und Art einen Zeichenaufruf, und in den Stufen 70 und 140 m wäre das ein Mehrfaches ohne Gewinn.
    /// </summary>
    private const float FuellKleinBisZoom = 45f, FuellMittelBisZoom = 55f;

    private Node3D? _fuellWurzel;
    private readonly List<MultiMeshInstance3D> _fuellKlein = new(), _fuellMittel = new();
    private (bool Klein, bool Mittel)? _fuellSichtbar;
    private Node3D? _fuellSommer;

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
        FuellVorgeladenAbholen();
        var logik = uhr.ElapsedMilliseconds;
        FuellungAufbauen(karte);
        var (nk, nt) = FuellZahlen;
        GD.Print($"Restflächen: {_fuellFlaechen.Count} Flächen, {nk} Kästen, {nt} Teile; Themen: "
            + string.Join(", ", _fuellThemen.GroupBy(t => t.Value).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Count()}"))
            + $"; {logik} ms Rechnung + {uhr.ElapsedMilliseconds - logik} ms Aufbau ({_fuellWurzel?.GetChildCount() ?? 0}+{_fuellSommer?.GetChildCount() ?? 0} MultiMeshes)");
    }

    /// <summary>Das Thema einer Restfläche (Garten, Werkhof, Randgestrüpp): nach den Gewichten der Nachbarart und dem festen Würfel; null, wenn die Datei keins kennt.</summary>
    private static string? FuellThemaWaehlen(Restflaeche r, Stadtkarte karte, Fuellrezepte rezepte)
    {
        if (rezepte.Nachbarn.GetValueOrDefault(Restflaechenfuellung.Nachbarart(r, karte)) is not { Themen: { Length: > 0 } namen } wahl) return null;
        var nummer = Restflaechenfuellung.Waehlen(wahl.Gewichte, Restflaechenfuellung.Wuerfel(karte.Kennung + "/" + r.Kennung, 1));
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
                teile.Add(new Fuellteil(teil.Datei, name, welt, teil.Schatten, teil.Klein, teil.Sommer));
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

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Baut die MultiMeshes aus dem Gesetzten neu; Kästen, auf denen ein Gebäude steht, bleiben leer. Auch nach Änderungen der Karte aufrufbar
    /// (Kartenwerkstatt) – es wird nichts neu gewürfelt, nur neu gezeichnet.
    /// </summary>
    internal void FuellungAufbauen(Stadtkarte karte)
    {
        if (_fuellWurzel is not null && IsInstanceValid(_fuellWurzel))
        {
            _naturWinter.RemoveAll(w => !IsInstanceValid(w.Vielfach));
            _fuellWurzel.QueueFree();
        }
        _fuellKlein.Clear();
        _fuellMittel.Clear();
        _fuellSichtbar = null;
        _fuellWurzel = new Node3D { Name = "Restflaechenfuellung" };
        AddChild(_fuellWurzel);
        _fuellSommer = new Node3D { Name = "Sommerteile", Visible = _jahreszeit != Jahreszeit.Winter };
        _fuellWurzel.AddChild(_fuellSommer);
        if (_fuellFlaechen.Count == 0) return;

        var frei = Naturfreiraum.Aus(karte, Modellmass);
        var gruppen = new SortedDictionary<(string Datei, string Knoten, bool Schatten, bool Klein, bool Sommer, int Zx, int Zz), List<Transform3D>>(
            Comparer<(string, string, bool, bool, bool, int, int)>.Create((a, b) => a.CompareTo(b)));
        var weggelassen = 0;
        foreach (var kaesten in _fuellFlaechen.Values)
            foreach (var (kasten, teile) in kaesten)
            {
                if (Restflaechenfuellung.Bebaut(kasten, frei)) { weggelassen++; continue; }
                foreach (var t in teile)
                {
                    var schluessel = (t.Datei, t.Knoten, t.Schatten, t.Klein, t.Sommer, Mathf.FloorToInt(t.Lage.Origin.X / FuellZelle), Mathf.FloorToInt(t.Lage.Origin.Z / FuellZelle));
                    if (!gruppen.TryGetValue(schluessel, out var liste)) gruppen[schluessel] = liste = new();
                    liste.Add(t.Lage);
                }
            }
        if (weggelassen > 0) GD.Print($"Restflächen: {weggelassen} Kästen unter Gebäuden ohne Füllung.");

        FuellNetzeVorladen(gruppen.Keys.Select(k => (k.Datei, k.Knoten)));
        foreach (var (schluessel, lagen) in gruppen)
        {
            if (NaturNetz(schluessel.Datei, schluessel.Knoten) is not { } netz) continue;
            // gemischt (fest aus der Lage): Die Natur-Dichte zeigt nur die ersten Kopien, und die sollen über alle Flächen verteilt sein
            var geordnet = lagen.OrderBy(l => Restflaechenfuellung.Wuerfel("f", (int)(l.Origin.X * 10), (int)(l.Origin.Z * 10))).ToList();
            var vielfach = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = netz, InstanceCount = geordnet.Count };
            for (var i = 0; i < geordnet.Count; i++) vielfach.SetInstanceTransform(i, geordnet[i]);
            if (schluessel.Datei == "natur/natur_buesche" && NaturNetz(schluessel.Datei, schluessel.Knoten + "_Winter", pflicht: false) is { } winter)
            {
                _naturWinter.Add((vielfach, netz, winter));
                if (_jahreszeit == Jahreszeit.Winter) vielfach.Mesh = winter;
            }
            var teil = new MultiMeshInstance3D
            {
                Name = $"{schluessel.Knoten}_{schluessel.Zx}_{schluessel.Zz}",
                Multimesh = vielfach,
                CastShadow = schluessel.Schatten ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
            };
            (schluessel.Sommer ? _fuellSommer : _fuellWurzel).AddChild(teil);
            if (schluessel.Klein)
            {
                _fuellKlein.Add(teil);
                var detail = new NaturDetail(teil, teil.Multimesh.InstanceCount, false, false); // nur Natur-Dichte; den Zoom regelt FuellungNachZoom
                _naturDetails.Add(detail);
                NaturDichteSetzen(detail);
            }
            else _fuellMittel.Add(teil);
        }
    }

    /// <summary>
    /// Holt die Netze aller gebrauchten Knoten (und die kahlen Fassungen der Büsche) mit einmal Instanziieren je Baukasten – <see cref="NaturNetz"/> allein
    /// instanziiert den ganzen Baukasten für jeden einzelnen Knoten.
    /// </summary>
    private void FuellNetzeVorladen(IEnumerable<(string Datei, string Knoten)> gebraucht)
    {
        foreach (var gruppe in gebraucht.Distinct().GroupBy(g => g.Datei))
        {
            var namen = gruppe.Select(g => g.Knoten).ToList();
            if (gruppe.Key == "natur/natur_buesche") namen.AddRange(namen.Select(n => n + "_Winter").ToList());
            namen = namen.Where(n => !_naturNetze.ContainsKey((gruppe.Key, n))).Distinct().ToList();
            if (namen.Count == 0 || Modell(gruppe.Key)?.Instantiate<Node3D>() is not { } kasten) continue;
            foreach (var n in namen)
            {
                var netz = (kasten.FindChild(n, true, false) as MeshInstance3D)?.Mesh;
                if (netz is not null) LaubMelden(netz);
                _naturNetze[(gruppe.Key, n)] = netz;
            }
            kasten.Free();
        }
    }

    /// <summary>Jedes Bild (aus <see cref="FeinheitenNachZoom"/>): Die Füllung nur in der nahen Zoomstufe zeigen (siehe <see cref="FuellKleinBisZoom"/>).</summary>
    private void FuellungNachZoom()
    {
        var innen = _raum is not null;
        var sichtbar = (innen || _kamera.Size <= Math.Min(FuellKleinBisZoom, FeinheitenBisZoom(Fenster.AktuelleFeinheiten)), innen || _kamera.Size <= FuellMittelBisZoom);
        if (_fuellSichtbar == sichtbar) return;
        _fuellSichtbar = sichtbar;
        foreach (var teil in _fuellKlein)
            if (IsInstanceValid(teil)) teil.Visible = sichtbar.Item1;
        foreach (var teil in _fuellMittel)
            if (IsInstanceValid(teil)) teil.Visible = sichtbar.Item2;
    }

    /// <summary>Die Jahreszeit hat gewechselt: Blumen gibt es im Winter nicht (Büsche tauschen in <see cref="JahreszeitSetzen"/> gegen die kahle Fassung).</summary>
    private void FuellungJahreszeit(bool winter)
    {
        if (_fuellSommer is not null && IsInstanceValid(_fuellSommer)) _fuellSommer.Visible = !winter;
    }
}
