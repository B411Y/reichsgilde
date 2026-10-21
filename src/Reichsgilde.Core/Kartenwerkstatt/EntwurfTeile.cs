using System.Text.Json;
using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Die bearbeitbaren Teile eines Kartenentwurfs außer den Gebäuden: Wege, Bauzonen, Naturflächen und einzelne Pflanzen
//
//  Jeder Teil hält dieselben Felder wie sein Gegenstück in der Stadtkarte (Kartenweg, Bauzone, Naturflaeche, Pflanze), ist aber
//  veränderbar – nur über die Befehle des Entwurfs, damit Rückgängig stimmt. Ein Befehl ändert eine Kopie und übernimmt deren Werte
//  ins Original: Das Objekt bleibt dasselbe, die Darstellung kann es behalten.
//  Für das Schreiben merkt sich jeder Teil seinen Eintrag aus der Datei (Vorlage): bekannte Felder stehen an ihrer alten Stelle,
//  unbekannte bleiben erhalten – eine unverändert gespeicherte Karte ist Byte für Byte dieselbe Datei.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Welche Art Teil der Karte sich geändert hat.</summary>
public enum Kartenteil
{
    Gebaeude,
    Weg,
    Bauzone,
    Naturflaeche,
    Pflanze,
}

/// <summary>Ein bearbeitbarer Teil des Entwurfs (Gebäude, Weg, Bauzone, Naturfläche, Pflanze).</summary>
public abstract class EntwurfTeil
{
    /// <summary>Welche Art Teil.</summary>
    public abstract Kartenteil Teil { get; }

    /// <summary>Kurzer Name für Befunde und den Knopf „Rückgängig: …“ („Ostgasse“, „Bauzone K1“, „Tanne“).</summary>
    public abstract string Bezeichnung { get; }

    /// <summary>Der Eintrag aus der Datei, aus dem der Teil gelesen wurde (null: neu in der Werkstatt) – für Reihenfolge und unbekannte Felder.</summary>
    internal JsonObject? Vorlage { get; set; }

    internal abstract EntwurfTeil KopieTeil();

    /// <summary>Übernimmt alle Werte (das Objekt bleibt dasselbe).</summary>
    internal abstract void UebernimmTeil(EntwurfTeil werte);

    /// <summary>Haben beide dieselben Werte? (Ein Befehl ohne Wirkung ist kein Schritt.)</summary>
    internal abstract bool GleicheWerte(EntwurfTeil anderer);

    /// <summary>Der Teil als Eintrag der Datei.</summary>
    internal abstract JsonObject AlsJson();

    /// <summary>
    /// Setzt einen Eintrag zusammen: Felder in der Reihenfolge der Vorlage (unbekannte unverändert), bekannte mit ihrem jetzigen Wert
    /// (null = weglassen), neue Felder hinten in der Reihenfolge von <paramref name="felder"/>.
    /// </summary>
    internal static JsonObject Zusammensetzen(JsonObject? vorlage, params (string Feld, JsonNode? Wert)[] felder)
    {
        var aus = new JsonObject();
        if (vorlage is not null)
            foreach (var (k, v) in vorlage)
            {
                var i = Array.FindIndex(felder, f => f.Feld == k);
                if (i < 0) aus[k] = v?.DeepClone();
                else if (felder[i].Wert is { } w) aus[k] = w;
            }
        foreach (var (k, w) in felder)
            if (w is not null && !aus.ContainsKey(k)) aus[k] = w;
        return aus;
    }

    internal static JsonArray JsonPunkt(Kartenort p) => new(JsonValue.Create(p.X), JsonValue.Create(p.Z));
    internal static JsonArray JsonPunkte(IEnumerable<Kartenort> p) => new(p.Select(x => (JsonNode)JsonPunkt(x)).ToArray());
    internal static List<Kartenort> Orte(IEnumerable<double[]>? p) => (p ?? Enumerable.Empty<double[]>()).Where(x => x is { Length: 2 }).Select(Kartenort.Aus).ToList();
    internal static List<double[]> Roh(IEnumerable<Kartenort> p) => p.Select(x => new[] { x.X, x.Z }).ToList();
}

// ═════════════════════════════════════════════════════════════════════════════
//  Weg
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Ein Weg im Entwurf: Name, Klasse, Stützpunkte, Breite und Kurve. <see cref="Verlauf"/> ist die Linie, die Spiel, Prüfung und Bauwesen
/// sehen (bei <c>kurve: "glatt"</c> die weiche Kurve durch die Stützpunkte, etwa alle Meter ein Punkt).
/// </summary>
public sealed class EntwurfWeg : EntwurfTeil
{
    public override Kartenteil Teil => Kartenteil.Weg;
    public override string Bezeichnung => Name == "" ? Kartenweg.Anzeigename(Klasse) : Name;

    public string Name { get; internal set; } = "";
    public Wegklasse Klasse { get; internal set; }
    public double Gefahr { get; internal set; } = 1;

    /// <summary>Gezeichnete Breite in Metern; 0 = nach der Klasse (<see cref="BreiteWirksam"/>).</summary>
    public double Breite { get; internal set; }

    /// <summary>„glatt“ oder null (gerade Strecken).</summary>
    public string? Kurve { get; internal set; }

    private List<Kartenort> _punkte = new();
    private Wegverlauf? _verlauf;
    private Kartenort[]? _verlaufOrte;

    /// <summary>Die Stützpunkte (in der Datei: <c>linie</c>).</summary>
    public IReadOnlyList<Kartenort> Punkte => _punkte;

    public bool IstGlatt => Kurve == Wegkurve.Glatt;
    public double BreiteWirksam => Breite > 0 ? Breite : Kartenweg.Standardbreite(Klasse);
    public double Mindestbreite => Kartenweg.Mindestbreite(Klasse);
    public bool IstKarrenstrasse => Kartenweg.TraegtKarren(Klasse);

    private Wegverlauf Kurvenlinie => _verlauf ??= Wegkurve.Verlauf(Roh(_punkte), IstGlatt);

    /// <summary>Die Mittellinie, wie alle sie sehen (gerade: die Stützpunkte; glatt: die abgetastete Kurve).</summary>
    public IReadOnlyList<Kartenort> Verlauf => _verlaufOrte ??= Kurvenlinie.Punkte.Select(Kartenort.Aus).ToArray();

    /// <summary>Für jeden Punkt des <see cref="Verlauf"/>: das Stück zwischen Stützpunkt i und i + 1, auf dem er liegt.</summary>
    public IReadOnlyList<int> Abschnitte => Kurvenlinie.Abschnitte;

    /// <summary>Die Länge des Verlaufs (Meter).</summary>
    public double Laenge => Kurvenlinie.Laenge;

    internal IReadOnlyList<double[]> VerlaufRoh => Kurvenlinie.Punkte;

    internal void PunkteSetzen(IEnumerable<Kartenort> punkte)
    {
        _punkte = punkte.ToList();
        (_verlauf, _verlaufOrte) = (null, null);
    }

    internal void KurveSetzen(string? kurve)
    {
        Kurve = kurve;
        (_verlauf, _verlaufOrte) = (null, null);
    }

    /// <summary>
    /// Wo ein Punkt auf der Linie einen neuen Stützpunkt bekäme: der Index, an dem er eingefügt würde (hinter dem Stützpunkt des Abschnitts,
    /// auf dem der nächste Punkt des Verlaufs liegt), und dieser nächste Punkt.
    /// </summary>
    public (int Index, Kartenort Ort, double Abstand) NaechsteStelle(Kartenort p)
    {
        var v = Verlauf;
        if (v.Count == 1) return (1, v[0], p.Abstand(v[0]));
        var (best, index, ort) = (double.MaxValue, 1, v.Count > 0 ? v[0] : p);
        for (var i = 0; i + 1 < v.Count; i++)
        {
            var q = AmNaechsten(p, v[i], v[i + 1]);
            var d = p.Abstand(q);
            if (d < best) (best, index, ort) = (d, Abschnitte[i] + 1, q);
        }
        return (index, ort, best);
    }

    private static Kartenort AmNaechsten(Kartenort p, Kartenort a, Kartenort b)
    {
        var (dx, dz) = (b.X - a.X, b.Z - a.Z);
        var l2 = dx * dx + dz * dz;
        var t = l2 == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Z - a.Z) * dz) / l2, 0, 1);
        return new Kartenort(a.X + t * dx, a.Z + t * dz);
    }

    /// <summary>Der Weg in den Typen der Stadtkarte (für Werkzeuge, die mit <see cref="Kartenweg"/> rechnen).</summary>
    public Kartenweg AlsKartenweg() => new() { Name = Name, Klasse = Klasse, Linie = Roh(_punkte), Gefahr = Gefahr, Breite = Breite, Kurve = Kurve };

    internal static EntwurfWeg Aus(Kartenweg w, JsonObject? vorlage)
    {
        var e = new EntwurfWeg { Name = w.Name, Klasse = w.Klasse, Gefahr = w.Gefahr, Breite = w.Breite, Kurve = w.Kurve, Vorlage = vorlage };
        e.PunkteSetzen(Orte(w.Linie));
        return e;
    }

    internal override EntwurfTeil KopieTeil()
    {
        var k = new EntwurfWeg { Name = Name, Klasse = Klasse, Gefahr = Gefahr, Breite = Breite, Kurve = Kurve, Vorlage = Vorlage };
        k.PunkteSetzen(_punkte);
        return k;
    }

    internal override void UebernimmTeil(EntwurfTeil werte)
    {
        var o = (EntwurfWeg)werte;
        (Name, Klasse, Gefahr, Breite, Vorlage) = (o.Name, o.Klasse, o.Gefahr, o.Breite, o.Vorlage);
        KurveSetzen(o.Kurve);
        PunkteSetzen(o._punkte);
    }

    internal override bool GleicheWerte(EntwurfTeil anderer) => anderer is EntwurfWeg o && Name == o.Name && Klasse == o.Klasse
        && Gefahr == o.Gefahr && Breite == o.Breite && Kurve == o.Kurve && _punkte.SequenceEqual(o._punkte);

    internal override JsonObject AlsJson() => Zusammensetzen(Vorlage,
        ("name", Name), ("klasse", Klasse.ToString().ToLowerInvariant()), ("linie", JsonPunkte(_punkte)),
        ("gefahr", Vorlage is null || Vorlage.ContainsKey("gefahr") || Gefahr != 1 ? JsonValue.Create(Gefahr) : null),
        ("breite", Breite > 0 ? JsonValue.Create(Breite) : null),
        ("kurve", Kurve is null ? null : JsonValue.Create(Kurve)));
}

// ═════════════════════════════════════════════════════════════════════════════
//  Flächen: Bauzone und Naturfläche
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Vieleck im Entwurf (Bauzone oder Naturfläche). Ecken in der Reihenfolge der Datei; die letzte schließt zur ersten.</summary>
public abstract class EntwurfFlaeche : EntwurfTeil
{
    private protected List<Kartenort> _ecken = new();

    public IReadOnlyList<Kartenort> Ecken => _ecken;

    /// <summary>Die Löcher (nur Naturflächen haben welche).</summary>
    public virtual IReadOnlyList<IReadOnlyList<Kartenort>> Aussparungen => Array.Empty<IReadOnlyList<Kartenort>>();

    /// <summary>Die Ecken des Umrisses (<paramref name="aussparung"/> = −1) oder einer Aussparung.</summary>
    public IReadOnlyList<Kartenort> Vieleck(int aussparung = -1) => aussparung < 0 ? _ecken : Aussparungen[aussparung];

    internal abstract void VieleckSetzen(int aussparung, IEnumerable<Kartenort> ecken);
}

/// <summary>Eine Bauzone im Entwurf: Kennung, Lage, Ecken auf dem Raster der Karte.</summary>
public sealed class EntwurfBauzone : EntwurfFlaeche
{
    public override Kartenteil Teil => Kartenteil.Bauzone;
    public override string Bezeichnung => $"Bauzone {Kennung}";

    public string Kennung { get; internal set; } = "";
    public Lage Art { get; internal set; }

    internal override void VieleckSetzen(int aussparung, IEnumerable<Kartenort> ecken) => _ecken = ecken.ToList();

    internal static EntwurfBauzone Aus(Bauzone z, JsonObject? vorlage) =>
        new() { Kennung = z.Kennung, Art = z.Art, _ecken = Orte(z.Flaeche), Vorlage = vorlage };

    /// <summary>Die Zone in den Typen der Stadtkarte.</summary>
    public Bauzone AlsBauzone() => new() { Kennung = Kennung, Art = Art, Flaeche = Roh(_ecken) };

    internal override EntwurfTeil KopieTeil() => new EntwurfBauzone { Kennung = Kennung, Art = Art, _ecken = _ecken.ToList(), Vorlage = Vorlage };

    internal override void UebernimmTeil(EntwurfTeil werte)
    {
        var o = (EntwurfBauzone)werte;
        (Kennung, Art, _ecken, Vorlage) = (o.Kennung, o.Art, o._ecken.ToList(), o.Vorlage);
    }

    internal override bool GleicheWerte(EntwurfTeil anderer) =>
        anderer is EntwurfBauzone o && Kennung == o.Kennung && Art == o.Art && _ecken.SequenceEqual(o._ecken);

    internal override JsonObject AlsJson() => Zusammensetzen(Vorlage,
        ("kennung", Kennung), ("art", Art.ToString().ToLowerInvariant()), ("flaeche", JsonPunkte(_ecken)));
}

/// <summary>
/// Eine Wald- oder Streufläche im Entwurf: Umriss, Aussparungen, Dichte (Streupunkte je 1.000 m²), Startwert und Gruppen (welche Arten
/// wie gemischt). Die Pflanzen rechnet <see cref="NaturVerteilung"/> (über <see cref="Kartenentwurf.NaturVon"/>).
/// </summary>
public sealed class EntwurfNaturflaeche : EntwurfFlaeche
{
    public override Kartenteil Teil => Kartenteil.Naturflaeche;
    public override string Bezeichnung => Name == "" ? Art : Name;

    public string Name { get; internal set; } = "";

    /// <summary>„wald“ oder „streu“ (nur zur Beschreibung, die Wirkung kommt aus den Gruppen).</summary>
    public string Art { get; internal set; } = "wald";

    public double Dichte { get; internal set; }
    public int Startwert { get; internal set; }

    private List<List<Kartenort>> _aussparungen = new();
    private List<Naturgruppe> _gruppen = new();

    /// <summary>Die Gruppen, wie sie in der Datei standen – geschrieben wird dieser Text, solange die Gruppen nicht geändert sind.</summary>
    private JsonNode? _gruppenText;

    public override IReadOnlyList<IReadOnlyList<Kartenort>> Aussparungen => _aussparungen;
    public IReadOnlyList<Naturgruppe> Gruppen => _gruppen;

    /// <summary>
    /// Die Mischung der Arten über alle Gruppen und Teile (Art → Anteil, zusammen 1), gewichtet mit dem Gewicht der Gruppe und der mittleren
    /// Anzahl – zum Anzeigen. Ändern: <see cref="Kartenentwurf.MischungSetzen"/>.
    /// </summary>
    public IReadOnlyDictionary<string, double> Mischung
    {
        get
        {
            var summe = new Dictionary<string, double>();
            var gewichtGruppen = _gruppen.Sum(g => Math.Max(0, g.Gewicht));
            foreach (var g in _gruppen)
                foreach (var t in g.Teile)
                {
                    var arten = t.Arten.Sum(a => Math.Max(0, a.Value));
                    var anzahl = t.Anzahl is { Length: 2 } a2 ? (a2[0] + a2[1]) / 2.0 : 1;
                    if (arten <= 0 || gewichtGruppen <= 0) continue;
                    foreach (var (art, w) in t.Arten)
                        summe[art] = summe.GetValueOrDefault(art) + Math.Max(0, g.Gewicht) / gewichtGruppen * anzahl * Math.Max(0, w) / arten;
                }
            var gesamt = summe.Values.Sum();
            return gesamt <= 0 ? summe : summe.ToDictionary(e => e.Key, e => e.Value / gesamt);
        }
    }

    internal override void VieleckSetzen(int aussparung, IEnumerable<Kartenort> ecken)
    {
        if (aussparung < 0) _ecken = ecken.ToList();
        else _aussparungen[aussparung] = ecken.ToList();
    }

    internal void AussparungenSetzen(IEnumerable<IEnumerable<Kartenort>> aussparungen) => _aussparungen = aussparungen.Select(a => a.ToList()).ToList();

    internal void GruppenSetzen(IEnumerable<Naturgruppe> gruppen)
    {
        _gruppen = gruppen.ToList();
        _gruppenText = null;
    }

    /// <summary>Die Fläche in den Typen der Stadtkarte (für <see cref="NaturVerteilung"/>).</summary>
    public Naturflaeche AlsNaturflaeche() => new()
    {
        Name = Name, Art = Art, Flaeche = Roh(_ecken), Aussparungen = _aussparungen.Select(Roh).ToList(), Dichte = Dichte, Startwert = Startwert,
        Gruppen = _gruppen,
    };

    internal static EntwurfNaturflaeche Aus(Naturflaeche n, JsonObject? vorlage) => new()
    {
        Name = n.Name, Art = n.Art, _ecken = Orte(n.Flaeche), _aussparungen = n.Aussparungen.Select(Orte).ToList(), Dichte = n.Dichte,
        Startwert = n.Startwert, _gruppen = n.Gruppen.ToList(), _gruppenText = vorlage?["gruppen"]?.DeepClone(), Vorlage = vorlage,
    };

    internal override EntwurfTeil KopieTeil() => new EntwurfNaturflaeche
    {
        Name = Name, Art = Art, _ecken = _ecken.ToList(), _aussparungen = _aussparungen.Select(a => a.ToList()).ToList(), Dichte = Dichte,
        Startwert = Startwert, _gruppen = _gruppen.ToList(), _gruppenText = _gruppenText, Vorlage = Vorlage,
    };

    internal override void UebernimmTeil(EntwurfTeil werte)
    {
        var o = (EntwurfNaturflaeche)werte;
        (Name, Art, Dichte, Startwert, Vorlage, _gruppenText) = (o.Name, o.Art, o.Dichte, o.Startwert, o.Vorlage, o._gruppenText);
        _ecken = o._ecken.ToList();
        _aussparungen = o._aussparungen.Select(a => a.ToList()).ToList();
        _gruppen = o._gruppen.ToList();
    }

    internal override bool GleicheWerte(EntwurfTeil anderer) => anderer is EntwurfNaturflaeche o && Name == o.Name && Art == o.Art
        && Dichte == o.Dichte && Startwert == o.Startwert && _ecken.SequenceEqual(o._ecken)
        && _aussparungen.Count == o._aussparungen.Count && _aussparungen.Zip(o._aussparungen).All(x => x.First.SequenceEqual(x.Second))
        && GruppenJson(_gruppen).ToJsonString() == GruppenJson(o._gruppen).ToJsonString();

    internal override JsonObject AlsJson() => Zusammensetzen(Vorlage,
        ("name", Name), ("art", Art), ("flaeche", JsonPunkte(_ecken)),
        ("aussparungen", _aussparungen.Count == 0 ? null : new JsonArray(_aussparungen.Select(a => (JsonNode)JsonPunkte(a)).ToArray())),
        ("dichte", Dichte), ("startwert", Startwert), ("gruppen", _gruppenText?.DeepClone() ?? GruppenJson(_gruppen)));

    /// <summary>Gruppen als JSON in der Schreibweise des Werkzeugs (gewicht, radius, teile mit arten und anzahl).</summary>
    internal static JsonArray GruppenJson(IEnumerable<Naturgruppe> gruppen) => new(gruppen.Select(g => (JsonNode)new JsonObject
    {
        ["gewicht"] = g.Gewicht, ["radius"] = g.Radius,
        ["teile"] = new JsonArray(g.Teile.Select(t => (JsonNode)new JsonObject
        {
            ["arten"] = new JsonObject(t.Arten.Select(a => new KeyValuePair<string, JsonNode?>(a.Key, JsonValue.Create(a.Value)))),
            ["anzahl"] = new JsonArray(t.Anzahl.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()),
        }).ToArray()),
    }).ToArray());
}

// ═════════════════════════════════════════════════════════════════════════════
//  Einzelne Pflanze
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Eine einzeln gesetzte Pflanze (oder ein Stein …) im Entwurf: Natur-Baustein, Punkt, Drehung (Grad), Maßstab, Variante (0 = vom Ort).</summary>
public sealed class EntwurfPflanze : EntwurfTeil
{
    public override Kartenteil Teil => Kartenteil.Pflanze;
    public override string Bezeichnung => Baustein?.Anzeigename ?? Katalog;

    public string Katalog { get; internal set; } = "";
    public double X { get; internal set; }
    public double Z { get; internal set; }
    public double Drehung { get; internal set; }
    public double Massstab { get; internal set; } = 1;

    /// <summary>Welche Variante (1 … Varianten des Bausteins); 0 = fest aus dem Ort gewürfelt.</summary>
    public int Variante { get; internal set; }

    /// <summary>Der Natur-Baustein (null, wenn es ihn nicht gibt).</summary>
    public Baustein? Baustein { get; internal set; }

    public Kartenort Punkt => new(X, Z);

    /// <summary>Die Pflanze in den Typen der Stadtkarte.</summary>
    public Pflanze AlsPflanze() => new() { Katalog = Katalog, Punkt = new[] { X, Z }, Drehung = Drehung, Massstab = Massstab, Variante = Variante };

    internal static EntwurfPflanze Aus(Pflanze p, JsonObject? vorlage) => new()
    {
        Katalog = p.Katalog, X = p.Punkt is { Length: 2 } ? p.Punkt[0] : 0, Z = p.Punkt is { Length: 2 } ? p.Punkt[1] : 0, Drehung = p.Drehung,
        Massstab = p.Massstab, Variante = p.Variante, Vorlage = vorlage,
    };

    internal override EntwurfTeil KopieTeil() => new EntwurfPflanze
    {
        Katalog = Katalog, X = X, Z = Z, Drehung = Drehung, Massstab = Massstab, Variante = Variante, Baustein = Baustein, Vorlage = Vorlage,
    };

    internal override void UebernimmTeil(EntwurfTeil werte)
    {
        var o = (EntwurfPflanze)werte;
        (Katalog, X, Z, Drehung, Massstab, Variante, Baustein, Vorlage) = (o.Katalog, o.X, o.Z, o.Drehung, o.Massstab, o.Variante, o.Baustein, o.Vorlage);
    }

    internal override bool GleicheWerte(EntwurfTeil anderer) => anderer is EntwurfPflanze o && Katalog == o.Katalog && X == o.X && Z == o.Z
        && Drehung == o.Drehung && Massstab == o.Massstab && Variante == o.Variante;

    internal override JsonObject AlsJson() => Zusammensetzen(Vorlage,
        ("katalog", Katalog), ("punkt", JsonPunkt(new Kartenort(X, Z))), ("drehung", Drehung), ("massstab", Massstab), ("variante", Variante));
}

/// <summary>Liest die Teile eines Abschnitts der Datei in die Typen der Stadtkarte (und behält je Eintrag die Vorlage).</summary>
internal static class Teilleser
{
    public static List<(T Wert, JsonObject Vorlage)> Liste<T>(JsonNode? knoten, string wo)
    {
        var aus = new List<(T, JsonObject)>();
        if (knoten is null) return aus;
        if (knoten is not JsonArray liste) throw new KartenFehler(new[] { $"{wo} ist keine Liste." });
        for (var i = 0; i < liste.Count; i++)
        {
            var o = liste[i] as JsonObject ?? throw new KartenFehler(new[] { $"{wo} Nr. {i + 1} ist kein Objekt." });
            try
            {
                var wert = o.Deserialize<T>(Stadtkarte.Optionen) ?? throw new KartenFehler(new[] { $"{wo} Nr. {i + 1} ist leer." });
                aus.Add((wert, (JsonObject)o.DeepClone()));
            }
            catch (JsonException ex)
            {
                throw new KartenFehler(new[] { $"{wo} Nr. {i + 1} ist nicht lesbar ({ex.Path}): {ex.Message}" });
            }
        }
        return aus;
    }
}
