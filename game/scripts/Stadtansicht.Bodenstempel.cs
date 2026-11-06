using System.Text.Json.Nodes;
using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Bodenstempel statt Bodenplatten (Boden Stufe 2) – ein Teil der <c>partial class Stadtansicht</c>. Höfe, Wege und Gartenbeete der Umlandbetriebe sind keine
/// Platten über dem Gelände mehr (sie hatten harte Kanten, schwebten am Hang und nahmen dem Gras die Wiese). Stattdessen schreibt das Spiel beim Laden die
/// Bodenart in eine <b>Kopie</b> des Bodenrasters (<see cref="Bodenraster.Stempeln"/>), und der Boden-Shader zeichnet sie wie gemalte Bodenarten:
/// <list type="bullet">
/// <item><b>Umlandmodelle</b>: <c>game/daten/bodenstempel.json</c> sagt je Modell (bei Baukästen mit Knoten), welche Fläche welche Bodenart bekommt – Hof und Wege
/// des Bauernhofs, Wege der Gärten, Klosterhof, Wachturm, Steinbruch, Grube, Holzschlag, dazu Feld und Wald der Einwohner. Die Umrisse stehen im Modell (x, z) und werden mit
/// <see cref="ModellLage"/> auf die Karte gelegt; die Datei ist je Art einstellbar (Rand, Rauschen, Fleck, Stärke).</item>
/// <item><b>Restflächen</b>: <c>untergruende</c> in <c>game/daten/restflaechen.json</c> gibt dem Thema den Boden (Garten Erde mit Wiese dazwischen, Werkhof Erde oder
/// Kies, Randgestrüpp nichts); der Stempel deckt die Felder der ganzen Restfläche, damit keine Nähte zwischen den Kästen entstehen.</item>
/// <item><b>Nur Wiese</b>: Der Stempel nimmt nur, was die Wiese übrig lässt; was das Leveldesign gemalt hat, gewinnt. Der Grasteppich liest die Wiese aus demselben Raster und
/// wächst darum nicht auf gestempelten Flächen (<see cref="BodenRaster"/>), die Grasbüschel am Rand der Wiese gehen mit.</item>
/// <item><b>Einmal</b>: gestempelt wird einmal vor dem Bau des Bodens (<see cref="KarteAufbauenSchritte"/>); nichts davon steht im Spielstand oder in der Karte, nichts kostet je Bild.
/// In der Kartenwerkstatt gibt es keine Stempel (dort malt man auf dem Raster der Karte). Inneres der Mauer: Dort deckt der Stadtlehm den Boden, wie bisher.</item>
/// </list>
/// Aus mit <c>--bodenstempel=0</c> (nur für Vergleiche; die Platten sind dann trotzdem aus den Modellen).
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Die Kopie des Bodenrasters der Karte mit den Stempeln darin; null, wenn nichts gestempelt wurde (dann gilt das Raster der Karte).</summary>
    private Bodenraster? _stempelBoden;

    /// <summary>Unter diesem Höhenunterschied (Meter) baut <see cref="SockelBauen"/> keinen Sockel – dort gibt es auch keine Erde darum.</summary>
    internal const float SockelMindestTiefe = 0.12f;

    private static bool BodenstempelAus => Wasserarten.Startoption("bodenstempel") is "0" or "aus" or "nein";

    /// <summary>Ein Eintrag aus den Dateien: die Art des Stempels und seine Umrisse (leer bei den Restflächen, dort die Felder der Fläche).</summary>
    private sealed record StempelAngabe(Bodenstempel Stempel, List<List<(double X, double Z)>> Flaechen);

    private static Dictionary<string, List<StempelAngabe>>? _stempelModelle;
    private static bool _stempelGelesen;

    /// <summary>Liest <c>bodenstempel.json</c> einmal; die Stempel je Modell (Schlüssel wie in der Datei). Leer, wenn die Datei fehlt oder kaputt ist.</summary>
    private static Dictionary<string, List<StempelAngabe>> StempelModelle()
    {
        if (_stempelModelle is not null) return _stempelModelle;
        var modelle = new Dictionary<string, List<StempelAngabe>>(StringComparer.Ordinal);
        const string pfad = "res://daten/bodenstempel.json";
        if (Godot.FileAccess.FileExists(pfad))
        {
            try
            {
                var wurzel = JsonNode.Parse(Godot.FileAccess.GetFileAsString(pfad))!;
                var standard = wurzel["standard"] as JsonObject;
                foreach (var (name, liste) in wurzel["modelle"]!.AsObject())
                    modelle[name] = StempelLesen(liste!.AsArray(), standard);
            }
            catch (Exception e) { GD.PushError("Bodenstempel: " + pfad + " ist kaputt: " + e.Message); modelle.Clear(); }
        }
        else GD.PushWarning("Bodenstempel: " + pfad + " fehlt.");
        return _stempelModelle = modelle;
    }

    /// <summary>Wie weit (Meter, vom Ursprung des Modells, samt halbem Rand) die Stempel des Bausteins reichen – der Natur-Freiraum richtet sich auch danach, damit nichts in den Hof wächst.</summary>
    private static (float X, float Z) StempelAusdehnung(Baustein b)
    {
        if (b.Modell is not { } datei) return (0, 0);
        var tabelle = StempelModelle();
        if (!tabelle.TryGetValue(datei + "#" + (b.Knoten ?? ""), out var angaben) && !tabelle.TryGetValue(datei, out angaben)) return (0, 0);
        double x = 0, z = 0;
        foreach (var a in angaben)
            foreach (var f in a.Flaechen)
                foreach (var (px, pz) in f)
                    (x, z) = (Math.Max(x, Math.Abs(px) + a.Stempel.Rand / 2), Math.Max(z, Math.Abs(pz) + a.Stempel.Rand / 2));
        return ((float)x, (float)z);
    }

    /// <summary>Übersetzt eine Liste von Stempeln aus der Datei; unbekannte Bodenarten werden gemeldet und ausgelassen.</summary>
    private static List<StempelAngabe> StempelLesen(JsonArray liste, JsonObject? standard)
    {
        double Zahl(JsonObject o, string feld, double vorgabe) =>
            o[feld]?.GetValue<double>() ?? standard?[feld]?.GetValue<double>() ?? vorgabe;
        var aus = new List<StempelAngabe>();
        foreach (var e in liste.OfType<JsonObject>())
        {
            if (Bodenraster.TexturVon(e["textur"]?.GetValue<string>()) is not { } textur)
            {
                GD.PushWarning($"Bodenstempel: Bodenart „{e["textur"]}“ unbekannt.");
                continue;
            }
            var flaechen = new List<List<(double, double)>>();
            foreach (var f in e["flaechen"]?.AsArray() ?? new JsonArray())
            {
                if (f is not JsonArray a) continue;
                if (a.Count == 4 && a[0] is JsonValue) // Rechteck x0, z0, x1, z1
                {
                    double x0 = a[0]!.GetValue<double>(), z0 = a[1]!.GetValue<double>(), x1 = a[2]!.GetValue<double>(), z1 = a[3]!.GetValue<double>();
                    flaechen.Add(new() { (x0, z0), (x1, z0), (x1, z1), (x0, z1) });
                }
                else flaechen.Add(a.Select(p => (p![0]!.GetValue<double>(), p[1]!.GetValue<double>())).ToList());
            }
            aus.Add(new StempelAngabe(new Bodenstempel(textur, Zahl(e, "staerke", 1), Zahl(e, "rand", 1.5), Zahl(e, "rauschen", 0.5), Zahl(e, "fleck", 0), 0), flaechen));
        }
        return aus;
    }

    /// <summary>
    /// Vor dem Bau des Bodens: legt die Stempel der Umlandmodelle und Restflächen in eine Kopie des Bodenrasters (<see cref="BodenRaster"/> zeigt dann auf sie).
    /// </summary>
    private void BodenstempelAusKarte(Stadtkarte karte)
    {
        _stempelBoden = null;
        if (_entwurf is not null || BodenstempelAus) return;
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var kopie = karte.Boden.Kopie();
        var (modelle, restflaechen) = (0, 0);
        var tabelle = StempelModelle();
        foreach (var g in karte.Gebaeude)
        {
            if (g.Baustein is not { Modell: { } datei } b) continue;
            if (!tabelle.TryGetValue(datei + "#" + (b.Knoten ?? g.Name), out var angaben) && !tabelle.TryGetValue(datei, out angaben)) continue;
            var lage = ModellLage(g);
            var saat = (int)Restflaechenfuellung.Wuerfel(karte.Kennung + "/" + g.Kennung, 3);
            foreach (var a in angaben)
            {
                var welt = a.Flaechen.Select(f => (IReadOnlyList<(double X, double Z)>)f.Select(p => { var w = lage * new Vector3((float)p.X, 0, (float)p.Z); return ((double)w.X, (double)w.Z); }).ToList()).ToList();
                kopie.Stempeln(welt, a.Stempel with { Saat = saat });
            }
            modelle++;
        }
        // Auf hängigem Gelände steht ein Sockel unter dem Platz (SockelBauen, Erde oben): Erde ins Gelände darum, damit der Sockel dort, wo er im Hang steckt, ohne Kante in den Boden übergeht
        if (BodenBewegt)
            foreach (var g in karte.Gebaeude.Where(g => g.Art is Bausteinart.Umlandbetrieb or Bausteinart.Umlandstelle && g.Baustein?.Modell?.StartsWith("wasser/") != true))
            {
                if (FundamentVon(g).Tiefe < SockelMindestTiefe) continue; // nur dort, wo SockelBauen wirklich einen Sockel baut
                var ecken = FlaechenEcken(Punkt3(g.Punkt), DrehungVon(g), GroesseVon(g)).Select(e => (e.X, e.Z)).ToList();
                kopie.Stempeln(new IReadOnlyList<(double X, double Z)>[] { ecken }, new Bodenstempel(Bodentextur.Erde, 0.9, 1.5, 0.4, 0, 5));
            }
        if (!FuellungAus && FuellRezepte() is { Untergruende.Count: > 0 } rezepte)
            foreach (var r in karte.Restflaechen)
            {
                if (FuellThemaWaehlen(r, karte, rezepte) is not { } thema || !rezepte.Untergruende.TryGetValue(thema, out var varianten) || varianten.Count == 0) continue;
                var saat = (int)Restflaechenfuellung.Wuerfel(karte.Kennung + "/" + r.Kennung, 4);
                var felder = Restflaechenfuellung.Felder(r)
                    .Select(f => (IReadOnlyList<(double X, double Z)>)new List<(double, double)>
                    {
                        (f.I * Restflaechenfuellung.Raster, f.J * Restflaechenfuellung.Raster), ((f.I + 1) * Restflaechenfuellung.Raster, f.J * Restflaechenfuellung.Raster),
                        ((f.I + 1) * Restflaechenfuellung.Raster, (f.J + 1) * Restflaechenfuellung.Raster), (f.I * Restflaechenfuellung.Raster, (f.J + 1) * Restflaechenfuellung.Raster),
                    }).ToList();
                if (felder.Count == 0) continue;
                foreach (var a in varianten[(saat & 0xFFFF) % varianten.Count])
                    kopie.Stempeln(felder, a.Stempel with { Saat = saat });
                restflaechen++;
            }
        if (modelle + restflaechen == 0) return;
        _stempelBoden = kopie;
        Ladezeit.Marke("Szene: Bodenstempel");
        GD.Print($"Bodenstempel: {modelle} Umlandplätze, {restflaechen} Restflächen, {uhr.ElapsedMilliseconds} ms");
    }
}
