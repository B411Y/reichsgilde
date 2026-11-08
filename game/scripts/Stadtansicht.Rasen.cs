using System.Text.Json.Nodes;
using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Rasen in den Restflächen – ein Teil der <c>partial class Stadtansicht</c> (Wunsch des Nutzers 08.11.2026: Die Stadt wirkte braun und trist).
/// <list type="bullet">
/// <item><b>Welche Themen</b>: <c>rasen.themen</c> in <c>game/daten/restflaechen.json</c> – Garten, Randgestrüpp, Kirchhof, Bildstock, Heuschober, Taubenschlag
/// haben Gras als Grund; Hof, Pferch, Werkhof, Steinmetzplatz, Holzplatz und Wasserstelle bleiben Erde (ihre <c>untergruende</c>).</item>
/// <item><b>In der Mauer</b> deckt sonst der Stadtlehm jeden Boden (Wegeschicht, grobes Bild B). Die <see cref="_rasenMaske"/> sagt, wo er weicht: Die Wegeschicht
/// zieht sie vom Stadtgrund ab (<see cref="RasenAbziehen"/>), und dort bleibt die Wiese des Bodens – mit Jahreszeit (Herbst gelblich, Winter fahl), Schnee, Nässe und
/// Grasteppich wie draußen, ohne eigenen Zeichenaufruf (es ist der Boden). Der Rand der Maske ist eingezogen und weich, damit Gassen daneben Gassen bleiben.</item>
/// <item><b>Kein Teppich</b>: Erde wird in die Kopie des Bodenrasters gestempelt (<see cref="Bodenraster.Stempeln"/>, nur auf Kosten der Wiese) – ein ausgetretener
/// <b>Pfad</b> vom nächsten Weg durch die Mitte (<see cref="Rasenflaeche.Pfad"/>), ein <b>Saum</b> innen am Rand, <b>Beete</b> unter den Krautreihen der Gärten,
/// Kreise unter <b>Bäumen</b> und großen <b>Büschen</b>; dazu die fleckigen Varianten aus <c>untergruende</c>.</item>
/// </list>
/// Alles einmal beim Laden vor dem Boden (<see cref="BodenstempelAusKarte"/>), fest aus Karte und Kennungen. Aus mit <c>--rasen=0</c> (nur für Vergleiche).
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Wo in der Mauer Rasen statt Stadtlehm liegt: ein eigenes Raster, dessen Anteil „Erde“ für den Rasen steht (0 … 255); null ohne Rasen.</summary>
    private Bodenraster? _rasenMaske;

    /// <summary>Wie viele Restflächen mit Rasen in der Mauer liegen (nur fürs Protokoll beim Laden).</summary>
    private int _rasenInDerMauer;

    private static bool RasenAus => Wasserarten.Startoption("rasen") is "0" or "aus" or "nein";

    /// <summary>Der Abschnitt „rasen“ aus <c>restflaechen.json</c>, übersetzt.</summary>
    private sealed class Rasenrezept
    {
        /// <summary>Thema → was es an Erde bekommt (saum, pfad, beet, baum, busch).</summary>
        public readonly Dictionary<string, HashSet<string>> Themen = new(StringComparer.Ordinal);
        public Bodenstempel Maske = new(Bodentextur.Erde, 1, 0.8, 0.2, 0.15, 0, 0.6);
        public Bodenstempel? Saum, Pfad, Beet, Baum, Busch;
        public double PfadBreite = 0.8, PfadEinzug = 0.3, WegHoechstens = 24, BeetZugabe = 0.25, BaumRadius = 1.4, BuschRadius = 0.8;
        public int AbFelder = 3;
    }

    private static Rasenrezept RasenLesen(JsonObject o)
    {
        static double Zahl(JsonObject? e, string feld, double vorgabe) => e?[feld]?.GetValue<double>() ?? vorgabe;
        static Bodenstempel? Stempel(JsonObject? e)
        {
            if (e is null) return null;
            if (Bodenraster.TexturVon(e["textur"]?.GetValue<string>()) is not { } t)
            {
                GD.PushWarning($"Rasen: Bodenart „{e["textur"]}“ unbekannt.");
                return null;
            }
            return new Bodenstempel(t, Zahl(e, "staerke", 1), Zahl(e, "rand", 1), Zahl(e, "rauschen", 0.3), Zahl(e, "fleck", 0), 0, Zahl(e, "einzug", 0), Zahl(e, "saum", 0));
        }
        var r = new Rasenrezept();
        if (o["themen"] is JsonObject themen)
            foreach (var (thema, teile) in themen)
                r.Themen[thema] = (teile as JsonArray ?? new JsonArray()).Select(t => t!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        if (o["maske"] is JsonObject m)
            r.Maske = new Bodenstempel(Bodentextur.Erde, 1, Zahl(m, "rand", 0.8), Zahl(m, "rauschen", 0.2), Zahl(m, "fleck", 0.15), 0, Zahl(m, "einzug", 0.6));
        r.Saum = Stempel(o["saum"] as JsonObject);
        // Beim Pfad ist „einzug“ der Abstand seiner Ecken zum Rand der Fläche (Rasenflaeche.Pfad), nicht der Einzug des Stempels
        if (o["pfad"] is JsonObject p && Stempel(p) is { } pfad)
        {
            r.Pfad = pfad with { Einzug = 0 };
            (r.PfadBreite, r.PfadEinzug, r.WegHoechstens, r.AbFelder) = (Zahl(p, "breite", 0.8), Zahl(p, "einzug", 0.3), Zahl(p, "weg_hoechstens", 24), (int)Zahl(p, "ab_felder", 3));
        }
        r.Beet = Stempel(o["beet"] as JsonObject);
        r.BeetZugabe = Zahl(o["beet"] as JsonObject, "zugabe", 0.25);
        r.Baum = Stempel(o["baum"] as JsonObject);
        r.BaumRadius = Zahl(o["baum"] as JsonObject, "radius", 1.4);
        r.Busch = Stempel(o["busch"] as JsonObject);
        r.BuschRadius = Zahl(o["busch"] as JsonObject, "radius", 0.8);
        return r;
    }

    /// <summary>Die Wege der Karte als Linien (für den Pfad: von welcher Seite er kommt).</summary>
    private static List<IReadOnlyList<(double X, double Z)>> RasenWege(Stadtkarte karte) =>
        karte.Wege.Select(w => (IReadOnlyList<(double X, double Z)>)w.Verlauf.Where(p => p is { Length: 2 }).Select(p => (p[0], p[1])).ToList())
            .Where(l => l.Count >= 2).ToList();

    /// <summary>
    /// Stempelt den Rasen einer Restfläche: die Maske (in <see cref="_rasenMaske"/>) und die Erde darin (in <paramref name="boden"/>, die Kopie des Bodenrasters).
    /// Reihenfolge = Vorrang (der Stempel nimmt nur, was die Wiese übrig lässt): Beete, Pfad, Bäume, Büsche, Saum. Gibt zurück, ob die Fläche Rasen hat.
    /// </summary>
    private bool RasenStempeln(Restflaeche r, string thema, Stadtkarte karte, Fuellrezepte rezepte, Bodenraster boden, List<IReadOnlyList<(double X, double Z)>> wege,
                               IReadOnlyList<IReadOnlyList<(double X, double Z)>> felderEcken)
    {
        if (rezepte.Rasen is not { } rasen || !rasen.Themen.TryGetValue(thema, out var teile) || felderEcken.Count == 0) return false;
        var saat = (int)Restflaechenfuellung.Wuerfel(karte.Kennung + "/" + r.Kennung, 5);
        // die Maske nur in der Mauer (draußen ist ohnehin Wiese, kein Lehm): eine Ecke eines Feldes darin genügt
        if (felderEcken.Any(f => f.Any(p => InDerStadt(new Vector3((float)p.X, 0, (float)p.Z)))))
        {
            (_rasenMaske ??= new Bodenraster()).Stempeln(felderEcken, rasen.Maske with { Saat = saat });
            _rasenInDerMauer++;
        }
        var felder = Restflaechenfuellung.Felder(r);

        // Beete und Teile aus den Kästen (dieselbe Lage wie die Füllung, Stadtansicht.Restflaechen.cs)
        var beete = new List<IReadOnlyList<(double X, double Z)>>();
        var baeume = new List<IReadOnlyList<(double X, double Z)>>();
        var buesche = new List<IReadOnlyList<(double X, double Z)>>();
        if (rezepte.Themen.TryGetValue(thema, out var formate) && (teile.Contains("beet") || teile.Contains("baum") || teile.Contains("busch")))
            foreach (var kasten in Restflaechenfuellung.Zerlegen(felder))
            {
                if (!formate.TryGetValue(kasten.Format, out var anordnung)) continue;
                if (teile.Contains("beet") && rasen.Beet is not null)
                {
                    var (lage, _) = KastenLage(karte.Kennung, r.Kennung, kasten);
                    foreach (var t in anordnung.Where(t => t.Art == "raster"))
                    {
                        var (a, b, z) = (t.FlaecheA, t.FlaecheB, (float)rasen.BeetZugabe);
                        beete.Add(new[] { (a.X - z, a.Y - z), (b.X + z, a.Y - z), (b.X + z, b.Y + z), (a.X - z, b.Y + z) }
                            .Select(p => { var w = lage * new Vector3(p.Item1, 0, p.Item2); return ((double)w.X, (double)w.Z); }).ToList());
                    }
                }
                if (!teile.Contains("baum") && !teile.Contains("busch")) continue;
                foreach (var t in KastenFuellen(anordnung, karte.Kennung, r.Kennung, kasten))
                {
                    var groesse = t.Lage.Basis.Scale.X;
                    if (t.Datei == "natur/natur_baeume" && teile.Contains("baum")) baeume.Add(Kreis(t.Lage.Origin, rasen.BaumRadius * groesse));
                    else if (t.Datei == "natur/natur_buesche" && t.Schatten && teile.Contains("busch")) buesche.Add(Kreis(t.Lage.Origin, rasen.BuschRadius * groesse));
                }
            }
        if (beete.Count > 0) boden.Stempeln(beete, rasen.Beet! with { Saat = saat + 1 });
        if (teile.Contains("pfad") && rasen.Pfad is { } pfadStempel)
        {
            var pfad = Rasenflaeche.Pfad(felder, wege, (uint)saat, rasen.PfadBreite, rasen.PfadEinzug, rasen.WegHoechstens, rasen.AbFelder);
            if (pfad.Count > 0) boden.Stempeln(pfad, pfadStempel with { Saat = saat + 2 });
            if (Wasserarten.Startoption("rasenbild") is not null)
                GD.Print($"[rasenbild] {r.Kennung} {thema}: Pfad " + string.Join(" | ", pfad.Select(v => string.Join(" ", v.Select(e => $"{e.X:F1},{e.Z:F1}")))));
        }
        if (baeume.Count > 0 && rasen.Baum is { } baum) boden.Stempeln(baeume, baum with { Saat = saat + 3 });
        if (buesche.Count > 0 && rasen.Busch is { } busch) boden.Stempeln(buesche, busch with { Saat = saat + 4 });
        if (teile.Contains("saum") && rasen.Saum is { } saum) boden.Stempeln(felderEcken, saum with { Saat = saat + 5 });
        return true;
    }

    /// <summary>Ein Kreis als Zehneck (Meter, x/z).</summary>
    private static IReadOnlyList<(double X, double Z)> Kreis(Vector3 mitte, double radius) =>
        Enumerable.Range(0, 10).Select(k => (mitte.X + radius * Math.Cos(k * Math.PI / 5), mitte.Z + radius * Math.Sin(k * Math.PI / 5))).ToList();

    /// <summary>
    /// Nur für Bilder ohne Fenster (<c>--rasenbild=&lt;ordner&gt;</c>): je Restfläche eine Draufsicht des Untergrunds (12 Bildpunkte je Meter, 6 m Rand) in Sommer, Herbst und
    /// Winter mit Schnee – ein Schema aus den Anteilen der Bodenarten, dem Stadtgrund und den Farben der Jahreszeit wie im Boden-Shader (ohne Texturen, Licht und Halme).
    /// Der Umriss der Restfläche ist dunkel eingezeichnet. Läuft nach der Wegeschicht (<see cref="KarteAufbauenSchritte"/>).
    /// </summary>
    private void RasenBildSchreiben(Stadtkarte karte)
    {
        if (Wasserarten.Startoption("rasenbild") is not { Length: > 0 } ordner || _fuellThemen is null) return;
        DirAccess.MakeDirRecursiveAbsolute(ordner);
        var raster = BodenRaster;
        // Grundfarben der Bodenarten (gemittelt aus den Texturen, ungefähr) in der Reihenfolge von Bodentextur
        var farben = new[]
        {
            new Vector3(0.30f, 0.42f, 0.17f), new Vector3(0.40f, 0.30f, 0.20f), new Vector3(0.45f, 0.43f, 0.40f), new Vector3(0.55f, 0.53f, 0.49f),
            new Vector3(0.28f, 0.22f, 0.15f), new Vector3(0.66f, 0.59f, 0.44f), new Vector3(0.27f, 0.22f, 0.17f), new Vector3(0.25f, 0.35f, 0.15f),
            new Vector3(0.55f, 0.45f, 0.33f), new Vector3(0.47f, 0.46f, 0.44f), new Vector3(0.33f, 0.27f, 0.17f),
        };
        var stadtlehm = new Vector3(0.43f, 0.38f, 0.32f);
        const float kachel = KachelZellen * 2f;
        float Stadt(float x, float z)
        {
            var (ti, tj) = (Mathf.FloorToInt(x / kachel), Mathf.FloorToInt(z / kachel));
            if (!_grasStadt.TryGetValue((ti, tj), out var info)) return 0;
            var a = Math.Clamp(Mathf.FloorToInt(x - ti * kachel + 1), 0, SchichtInfoBild - 1);
            var b = Math.Clamp(Mathf.FloorToInt(z - tj * kachel + 1), 0, SchichtInfoBild - 1);
            return info[(b * SchichtInfoBild + a) * 4 + 2] / 255f;
        }
        var n = 0;
        foreach (var r in karte.RestflaechenOderLeer())
        {
            if (!_fuellThemen.TryGetValue(r.Kennung, out var thema)) continue;
            var felder = Restflaechenfuellung.Felder(r);
            if (felder.Count == 0) continue;
            var menge = felder.ToHashSet();
            const int px = 12, rand = 6;
            var (x0, z0) = (felder.Min(f => f.I) * 2 - rand, felder.Min(f => f.J) * 2 - rand);
            var (w, h) = ((felder.Max(f => f.I) + 1) * 2 + rand - x0, (felder.Max(f => f.J) + 1) * 2 + rand - z0);
            if (w * h > 160 * 160) continue; // nur Flächen bis etwa 150 × 150 m
            foreach (var (zeit, jz) in new[] { ("sommer", 1), ("herbst", 2), ("winter", 3) })
            {
                var bild = Image.CreateEmpty(w * px, h * px, false, Image.Format.Rgb8);
                for (var bz = 0; bz < h * px; bz++)
                    for (var ax = 0; ax < w * px; ax++)
                    {
                        var (x, z) = (x0 + (ax + 0.5f) / px, z0 + (bz + 0.5f) / px);
                        // Anteile bilinear aus den vier Bodenpunkten
                        var (i, j) = (Mathf.FloorToInt(x), Mathf.FloorToInt(z));
                        var (tx, tz) = (x - i, z - j);
                        var wt = new float[Bodenraster.Texturen];
                        for (var t = 0; t < Bodenraster.Texturen; t++)
                        {
                            float G(int a, int b) => raster?.Gewicht(a, b, (Bodentextur)t) ?? (t == 0 ? 255 : 0);
                            wt[t] = Mathf.Lerp(Mathf.Lerp(G(i, j), G(i + 1, j), tx), Mathf.Lerp(G(i, j + 1), G(i + 1, j + 1), tx), tz) / 255f;
                        }
                        var stadt = Stadt(x, z);
                        wt[(int)Bodentextur.Lehm] += wt[0] * stadt;
                        wt[0] *= 1 - stadt;
                        var lehmAnteil = stadt * (1 - Mathf.Clamp(wt[(int)Bodentextur.Erde] + wt[(int)Bodentextur.Moos] + wt[(int)Bodentextur.Mist], 0, 1));
                        var c = Vector3.Zero;
                        for (var t = 0; t < wt.Length; t++)
                        {
                            var f = farben[t];
                            if (t == 0 && jz > 1)
                            {
                                // jahreszeit_wiese (wetter.gdshaderinc)
                                var l = f.Dot(new Vector3(0.3f, 0.55f, 0.15f));
                                f = jz == 2 ? f.Lerp(new Vector3(l * 1.5f, l * 1.18f, l * 0.6f), 0.72f) : f.Lerp(new Vector3(l * 1.1f, l * 1.05f, l * 0.9f), 0.78f) * 0.92f;
                            }
                            c += f * wt[t];
                        }
                        c = c.Lerp(stadtlehm, lehmAnteil);
                        if (jz == 3) c = c.Lerp(new Vector3(0.66f, 0.68f, 0.73f), 0.8f * (1 - 0.35f * stadt)); // Schnee wie wetter_boden (deckend)
                        // Umriss der Restfläche
                        var drin = menge.Contains((Mathf.FloorToInt(x / 2), Mathf.FloorToInt(z / 2)));
                        var (fx, fz) = (x / 2 - Mathf.Floor(x / 2), z / 2 - Mathf.Floor(z / 2));
                        if (drin && ((fx < 0.04f && !menge.Contains((Mathf.FloorToInt(x / 2) - 1, Mathf.FloorToInt(z / 2)))) || (fx > 0.96f && !menge.Contains((Mathf.FloorToInt(x / 2) + 1, Mathf.FloorToInt(z / 2))))
                                  || (fz < 0.04f && !menge.Contains((Mathf.FloorToInt(x / 2), Mathf.FloorToInt(z / 2) - 1))) || (fz > 0.96f && !menge.Contains((Mathf.FloorToInt(x / 2), Mathf.FloorToInt(z / 2) + 1)))))
                            c *= 0.35f;
                        bild.SetPixel(ax, bz, new Color(c.X, c.Y, c.Z));
                    }
                bild.SavePng(System.IO.Path.Combine(ordner, $"{r.Kennung}_{thema}_{zeit}.png"));
                n++;
            }
        }
        GD.Print($"[rasenbild] {n} Bilder nach {ordner}");
    }

    /// <summary>
    /// Zieht den Rasen vom Stadtgrund einer Kachel der Wegeschicht ab (grobes Bild, 1 m, Zellmitten bei <paramref name="x0"/> + a + 0,5): Dort ist kein Lehm,
    /// die Wiese bleibt. Läuft parallel je Kachel und liest das Raster nur.
    /// </summary>
    private static void RasenAbziehen(Bodenraster rasen, int x0, int z0, int groesse, float[] grund)
    {
        // nur, wenn eine Kachel der Maske (64 m) den Ausschnitt berührt
        var (k0, k1) = (Bodenraster.KachelVon(x0 - 1, z0 - 1), Bodenraster.KachelVon(x0 + groesse + 1, z0 + groesse + 1));
        if (!rasen.Kacheln.Any(k => k.I >= k0.I && k.I <= k1.I && k.J >= k0.J && k.J <= k1.J)) return;
        for (var b = 0; b < groesse; b++)
            for (var a = 0; a < groesse; a++)
            {
                var i = b * groesse + a;
                if (grund[i] <= 0) continue;
                // die Zellmitte liegt mitten zwischen vier Bodenpunkten
                var (x, z) = (x0 + a, z0 + b);
                var anteil = (rasen.Gewicht(x, z, Bodentextur.Erde) + rasen.Gewicht(x + 1, z, Bodentextur.Erde)
                            + rasen.Gewicht(x, z + 1, Bodentextur.Erde) + rasen.Gewicht(x + 1, z + 1, Bodentextur.Erde)) / (4f * 255f);
                if (anteil > 0) grund[i] *= 1f - anteil;
            }
    }
}
