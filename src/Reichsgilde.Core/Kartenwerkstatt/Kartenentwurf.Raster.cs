using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Bauzonen aus dem Raster im Entwurf (Rechnung: Rasterbauzonen.cs)
//
//  Die groben Lage-Gebiete stehen in „lagen“ ([{ "art": "kern", "name": "…", "flaeche": [[x, z], …] }, …], „art“ auch „keine“), die
//  Restflächen der letzten Rechnung in „restflaechen“ ([{ "kennung": "R1", "art": "kern", "grund": "klein", "felder": 2, "flaeche": … }]).
//  Beide gehören zu einem Teil (EntwurfRasterzonen, einmal je Karte); ohne Inhalt fehlen sie in der Datei, alte Karten bleiben gleich.
//  BauzonenAusRaster ersetzt in einem Schritt (Rückgängig) alle berechneten Zonen und die Restflächen; von Hand gezeichnete Zonen bleiben
//  und gehen vor. Die berechneten stehen vor den von Hand gezeichneten (wie in Köln: erst die Rasterzonen, dann die Werkstatt-Bauplätze).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Lage-Gebiete und Restflächen der Karte (nur einmal je Karte).</summary>
public sealed class EntwurfRasterzonen : EntwurfTeil
{
    public override Kartenteil Teil => Kartenteil.Rasterzonen;
    public override string Bezeichnung => "Bauzonen aus dem Raster";

    public IReadOnlyList<Lagegebiet> Lagen { get; internal set; } = Array.Empty<Lagegebiet>();
    public IReadOnlyList<Restflaeche> Restflaechen { get; internal set; } = Array.Empty<Restflaeche>();

    internal override EntwurfTeil KopieTeil() => new EntwurfRasterzonen { Lagen = Lagen.ToList(), Restflaechen = Restflaechen.ToList() };

    internal override void UebernimmTeil(EntwurfTeil werte)
    {
        var o = (EntwurfRasterzonen)werte;
        (Lagen, Restflaechen) = (o.Lagen.ToList(), o.Restflaechen.ToList());
    }

    internal override bool GleicheWerte(EntwurfTeil anderer) =>
        anderer is EntwurfRasterzonen o && LagenJson(Lagen) is var a && LagenJson(o.Lagen) is var b && JsonNode.DeepEquals(a, b)
        && JsonNode.DeepEquals(RestJson(Restflaechen), RestJson(o.Restflaechen));

    internal override JsonObject AlsJson() => throw new InvalidOperationException("Lagen und Restflächen stehen in „lagen“ und „restflaechen“ (Kartenentwurf.ZuJson).");

    internal static JsonArray LagenJson(IEnumerable<Lagegebiet> lagen) => new(lagen.Select(g => (JsonNode)Zusammensetzen(null,
        ("art", g.ArtText), ("name", g.Name == "" ? null : JsonValue.Create(g.Name)), ("flaeche", JsonPunkte(g.Ecken)))).ToArray());

    internal static JsonArray RestJson(IEnumerable<Restflaeche> rest) => new(rest.Select(r => (JsonNode)new JsonObject
    {
        ["kennung"] = r.Kennung, ["art"] = r.Art.ToString().ToLowerInvariant(), ["grund"] = r.Grund, ["felder"] = r.Felder,
        ["flaeche"] = JsonPunkte(Orte(r.Flaeche)),
    }).ToArray());

    /// <summary>Liest „lagen“ (fehlt es, keine Gebiete).</summary>
    /// <exception cref="KartenFehler">Bei einem Eintrag ohne lesbare Art oder mit weniger als drei Ecken.</exception>
    internal static List<Lagegebiet> LagenAus(JsonNode? knoten)
    {
        var liste = new List<Lagegebiet>();
        if (knoten is null) return liste;
        if (knoten is not JsonArray a) throw new KartenFehler(new[] { "lagen ist keine Liste." });
        for (var n = 0; n < a.Count; n++)
        {
            var o = a[n] as JsonObject ?? throw new KartenFehler(new[] { $"Lage-Gebiet Nr. {n + 1} ist kein Objekt." });
            var text = (o["art"] as JsonValue)?.TryGetValue<string>(out var t) == true ? t : "";
            Lage? art = text == Lagegebiet.Keine ? null
                : Enum.TryParse<Lage>(text, ignoreCase: true, out var l) ? l
                : throw new KartenFehler(new[] { $"Lage-Gebiet Nr. {n + 1}: unbekannte Art „{text}“ (kern, mauerring, vorstadt, umland oder keine)." });
            var ecken = (o["flaeche"] as JsonArray ?? new JsonArray()).OfType<JsonArray>().Where(p => p.Count == 2)
                .Select(p => new Kartenort((double)p[0]!, (double)p[1]!)).ToList();
            if (ecken.Count < 3) throw new KartenFehler(new[] { $"Lage-Gebiet Nr. {n + 1}: Die Fläche braucht mindestens drei Ecken." });
            var name = (o["name"] as JsonValue)?.TryGetValue<string>(out var nm) == true ? nm : "";
            liste.Add(new Lagegebiet(art, ecken, name));
        }
        return liste;
    }
}

public sealed partial class Kartenentwurf
{
    /// <summary>Die groben Lage-Gebiete (Feld <c>lagen</c>), in Reihenfolge: das erste, das eine Feldmitte enthält, gilt.</summary>
    public IReadOnlyList<Lagegebiet> Lagen => _rasterzonen.Lagen;

    /// <summary>Die Restflächen der letzten Rechnung (Feld <c>restflaechen</c>, <see cref="Restflaeche"/>).</summary>
    public IReadOnlyList<Restflaeche> Restflaechen => _rasterzonen.Restflaechen;

    /// <summary>Lage-Gebiete und Restflächen als Teil (für die Meldung <see cref="Kartenteil.Rasterzonen"/>).</summary>
    public EntwurfRasterzonen Rasterzonen => _rasterzonen;

    /// <summary>Ersetzt die Lage-Gebiete (Ecken auf das Raster gerundet). Ein Schritt mit Rückgängig; die Zonen rechnet erst <see cref="BauzonenAusRaster"/> neu.</summary>
    /// <exception cref="ArgumentException">Bei einem Gebiet mit weniger als drei verschiedenen Ecken.</exception>
    public void LagenSetzen(IEnumerable<Lagegebiet> lagen)
    {
        var liste = lagen.Select(g => g with { Ecken = g.Ecken.Select(p => new Kartenort(RasterRunden(p.X), RasterRunden(p.Z))).Distinct().ToList() }).ToList();
        if (liste.FirstOrDefault(g => g.Ecken.Count < 3) is { } falsch)
            throw new ArgumentException($"Das Lage-Gebiet „{falsch.Name}“ braucht mindestens drei verschiedene Ecken.", nameof(lagen));
        Aendern(_rasterzonen, "– Lage-Gebiete gesetzt", r => r.Lagen = liste);
    }

    private double RasterRunden(double wert) => Math.Round(wert / Raster) * Raster + 0.0;

    /// <summary>
    /// Rechnet die Bauzonen aus dem Raster neu (<see cref="Rasterbauzonen.Rechnen"/>) und ersetzt in einem Schritt alle berechneten Zonen
    /// (<see cref="EntwurfBauzone.Berechnet"/>) und die Restflächen; von Hand gezeichnete Zonen bleiben. Ohne <see cref="Rastervorgaben.LageVon"/>
    /// gelten die Lage-Gebiete der Karte (ohne Gebiete entsteht keine Zone). Kennungen: Anfangsbuchstabe der Lage mit der kleinsten freien Nummer.
    /// </summary>
    public Rasterergebnis BauzonenAusRaster(Rastervorgaben? vorgaben = null)
    {
        var v = vorgaben ?? Rastervorgaben.Standard;
        var alt = _bauzonen.Select((z, i) => (Zone: z, Index: i)).Where(x => x.Zone.Berechnet).ToList();
        var ergebnis = Rasterbauzonen.Rechnen(this, v);

        var befehle = new List<IKartenbefehl>();
        foreach (var (zone, index) in Enumerable.Reverse(alt)) befehle.Add(new TeilLoeschen(zone, index));
        var belegt = _bauzonen.Where(z => !z.Berechnet).Select(z => z.Kennung).ToHashSet();
        for (var k = 0; k < ergebnis.Zonen.Count; k++)
        {
            var z = ergebnis.Zonen[k];
            var basis = z.Art.ToString()[..1];
            var n = 1;
            while (belegt.Contains(basis + n)) n++;
            belegt.Add(basis + n);
            var zone = new EntwurfBauzone { Art = z.Art, Kennung = basis + n, Berechnet = true };
            zone.VieleckSetzen(-1, z.Umriss);
            befehle.Add(new TeilSetzen(zone, k));
        }
        if (Aenderung(_rasterzonen, r => r.Restflaechen = ergebnis.Restflaechen.ToList()) is { } rest) befehle.Add(rest);
        if (befehle.Count > 0) Geschichte.Ausfuehren(new Sammelbefehl("Bauzonen aus dem Raster berechnet", befehle));
        return ergebnis;
    }
}

public sealed partial class Kartenentwurf
{
    /// <summary>
    /// Ordnet jedem Platz des Probeplans (<c>probeplan</c>, Greybox) die Zone zu, in der seine Mitte liegt – nach einer neuen Rechnung heißen
    /// die Zonen anders. Gibt die Plätze zurück, deren Mitte in keiner Zone liegt (sie behalten ihre alte Zone, die Prüfung meldet sie). Kein
    /// Schritt der Geschichte: Der Probeplan ist ein Feld der Kartenskripte, kein Teil der Werkstatt.
    /// </summary>
    public IReadOnlyList<Probeplatz> ProbeplanZonenZuordnen()
    {
        var ohne = new List<Probeplatz>();
        if (_daten["probeplan"] is not JsonArray plan) return ohne;
        var liste = Probeplan;
        for (var k = 0; k < plan.Count && k < liste.Count; k++)
        {
            var p = liste[k];
            var zone = _bauzonen.FirstOrDefault(z => Kartengeometrie.ImVieleck(p.Punkt, z.Ecken));
            if (zone is null) ohne.Add(p);
            else if (plan[k] is JsonObject o) o["zone"] = zone.Kennung;
        }
        _probeplan = null;
        _befunde = null;
        return ohne;
    }
}
