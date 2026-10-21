using System.Text.Json;
using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Die Karte, wie die Kartenwerkstatt (Level-Editor) sie bearbeitet
//
//  Die Stadtkarte (Bauen/Stadtkarte.cs) ist zum Spielen da: fest, geprüft, im Spielstand. Der Kartenentwurf ist zum Bearbeiten da:
//  veränderbar, darf Fehler enthalten (die Prüfung zeigt sie), und er hält ALLES aus der Datei – auch Felder, die weder Kern noch
//  Darstellung lesen (vorlage, fiktion, vorkommen, probeplan …). Gespeichert ergibt er dieselbe Datei wieder (Kartenschreiber).
//
//  Schritt 1 bearbeitet nur die Gebäude. Alles Übrige (Wege, Mauern, Bauzonen, Natur, Start, Wasser, Kamera …) liegt als JSON im
//  Entwurf und wird für die Prüfung einmal in die Typen der Stadtkarte gelesen (Kartenweg, Kartenmauer …).
//  Geändert wird nur über Befehle (Setzen, Verschieben, Drehen …), damit Rückgängig und Wiederholen immer stimmen.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Gebäude im Entwurf: dieselben Felder wie <see cref="Kartengebaeude"/>, aber veränderbar (nur über die Befehle des Entwurfs).</summary>
public sealed class EntwurfGebaeude
{
    /// <summary>Eindeutig in der Karte. Spielstände und <c>start</c> verweisen darauf – die Werkstatt ändert sie nicht.</summary>
    public string Kennung { get; internal set; } = "";

    /// <summary>Kennung des Bausteins im Gebäudekatalog.</summary>
    public string Katalog { get; internal set; } = "";

    public string Name { get; internal set; } = "";

    /// <summary>Mitte der Grundfläche (inklusive Vorplatz), Meter.</summary>
    public double X { get; internal set; }

    public double Z { get; internal set; }

    /// <summary>Grad wie Godot <c>Rotation.Y</c>: 0 = Vorderseite nach Süden (+z), 90 = Osten.</summary>
    public double Drehung { get; internal set; }

    public double Massstab { get; internal set; } = 1;
    public string? Betrieb { get; internal set; }
    public Lage? Lage { get; internal set; }
    public string? Konfession { get; internal set; }

    /// <summary>Der Baustein aus dem Katalog (null, wenn die Kennung unbekannt ist).</summary>
    public Baustein? Baustein { get; internal set; }

    /// <summary>Die Art des Bausteins oder "".</summary>
    public string Art => Baustein?.Art ?? "";

    public Kartenort Punkt => new(X, Z);

    /// <summary>Felder der Datei, die dieser Leser nicht kennt – sie werden unverändert wieder geschrieben.</summary>
    internal List<KeyValuePair<string, JsonNode?>> Weitere { get; private set; } = new();

    /// <summary>[Breite, Tiefe] der Grundfläche (Baustein mal Maßstab) oder null ohne Baustein bzw. Größe.</summary>
    public (double Breite, double Tiefe)? Groesse =>
        Baustein?.Groesse is { Length: 2 } g ? (g[0] * Massstab, g[1] * Massstab) : null;

    /// <summary>Steht die Vorderseite in einer der vier Himmelsrichtungen?</summary>
    public bool IstRechtwinklig => Math.Abs(Drehung / 90 - Math.Round(Drehung / 90)) < 1e-6;

    /// <summary>Ein Punkt im Gebäude (lokal: x nach rechts, z zur Vorderseite) in Stadtkoordinaten.</summary>
    public Kartenort Lokal(double lx, double lz)
    {
        var d = Kartengeometrie.Dreh(lx, lz, Drehung);
        return new Kartenort(X + d.X, Z + d.Z);
    }

    /// <summary>
    /// Die vier Ecken der gedrehten Grundfläche ab hinten links (für den Umriss in der Darstellung und die Prüfung); leer ohne Größe.
    /// <paramref name="ohneVorplatz"/> lässt die 2 m an der Vorderseite weg (das Haus selbst).
    /// </summary>
    public IReadOnlyList<Kartenort> Ecken(bool ohneVorplatz = false)
    {
        if (Groesse is not var (w, t)) return Array.Empty<Kartenort>();
        var vorn = t / 2 - (ohneVorplatz ? Kartenpruefung.Vorplatz : 0);
        return new[] { Lokal(-w / 2, -t / 2), Lokal(w / 2, -t / 2), Lokal(w / 2, vorn), Lokal(-w / 2, vorn) };
    }

    /// <summary>Werkstattplatz: der Hof (im Vorplatz, 0,5 m hinter der Vorderkante) – dort beginnt der Weg; null ohne Größe.</summary>
    public Kartenort? Hof => Groesse is var (_, t) ? Lokal(0, t / 2 - Kartenpruefung.HofHinterKante) : null;

    /// <summary>Mitte der Vorderkante (Eingang eines Wohnhausplatzes).</summary>
    public Kartenort? Eingang => Groesse is var (_, t) ? Lokal(0, t / 2) : null;

    internal EntwurfGebaeude Kopie() => new()
    {
        Kennung = Kennung, Katalog = Katalog, Name = Name, X = X, Z = Z, Drehung = Drehung, Massstab = Massstab, Betrieb = Betrieb,
        Lage = Lage, Konfession = Konfession, Baustein = Baustein, Weitere = Weitere.Select(e => new KeyValuePair<string, JsonNode?>(e.Key, e.Value?.DeepClone())).ToList(),
    };

    /// <summary>Übernimmt alle Werte (das Objekt bleibt dasselbe, damit die Darstellung es behalten kann).</summary>
    internal void Uebernimm(EntwurfGebaeude o)
    {
        (Kennung, Katalog, Name, X, Z, Drehung, Massstab) = (o.Kennung, o.Katalog, o.Name, o.X, o.Z, o.Drehung, o.Massstab);
        (Betrieb, Lage, Konfession, Baustein) = (o.Betrieb, o.Lage, o.Konfession, o.Baustein);
        Weitere = o.Weitere.Select(e => new KeyValuePair<string, JsonNode?>(e.Key, e.Value?.DeepClone())).ToList();
    }

    /// <summary>Das Gebäude als Eintrag der Datei (Felder in der Reihenfolge des Werkzeugs, freiwillige nur, wenn gesetzt).</summary>
    internal JsonObject AlsJson()
    {
        var o = new JsonObject
        {
            ["kennung"] = Kennung, ["katalog"] = Katalog, ["name"] = Name,
            ["punkt"] = new JsonArray(JsonValue.Create(X), JsonValue.Create(Z)), ["drehung"] = Drehung,
        };
        if (Massstab != 1) o["massstab"] = Massstab;
        if (Betrieb is not null) o["betrieb"] = Betrieb;
        if (Lage is { } l) o["lage"] = l.ToString().ToLowerInvariant();
        if (Konfession is not null) o["konfession"] = Konfession;
        foreach (var (k, v) in Weitere) o[k] = v?.DeepClone();
        return o;
    }

    internal static EntwurfGebaeude AusJson(JsonObject o, int nummer)
    {
        var g = new EntwurfGebaeude();
        foreach (var (k, v) in o)
        {
            try
            {
                switch (k)
                {
                    case "kennung": g.Kennung = v!.GetValue<string>(); break;
                    case "katalog": g.Katalog = v!.GetValue<string>(); break;
                    case "name": g.Name = v!.GetValue<string>(); break;
                    case "punkt": (g.X, g.Z) = (v![0]!.GetValue<double>(), v[1]!.GetValue<double>()); break;
                    case "drehung": g.Drehung = v!.GetValue<double>(); break;
                    case "massstab": g.Massstab = v!.GetValue<double>(); break;
                    case "betrieb": g.Betrieb = v?.GetValue<string>(); break;
                    case "lage": g.Lage = v is null ? null : Enum.Parse<Lage>(v.GetValue<string>(), ignoreCase: true); break;
                    case "konfession": g.Konfession = v?.GetValue<string>(); break;
                    default: g.Weitere.Add(new(k, v?.DeepClone())); break;
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException or ArgumentException or NullReferenceException)
            {
                throw new KartenFehler(new[] { $"Gebäude Nr. {nummer} ({g.Kennung}): Das Feld „{k}“ ist nicht lesbar." });
            }
        }
        return g;
    }
}

/// <summary>Was sich am Entwurf geändert hat (für die Darstellung: nur dieses Gebäude neu zeichnen).</summary>
public enum Aenderungsart
{
    /// <summary>Das Gebäude ist neu in der Karte (auch: ein Löschen wurde zurückgenommen).</summary>
    Gesetzt,

    /// <summary>Das Gebäude ist weg (auch: ein Setzen wurde zurückgenommen).</summary>
    Geloescht,

    /// <summary>Punkt, Drehung, Name, Betrieb … des Gebäudes haben sich geändert.</summary>
    Geaendert,
}

/// <summary>Eine Änderung am Entwurf: welche Art, welches Gebäude.</summary>
public readonly record struct Kartenaenderung(Aenderungsart Art, EntwurfGebaeude Gebaeude);

/// <summary>Ein Fundort eines Rohstoffbetriebs (<c>vorkommen</c> der Datei; nur das Werkzeug und die Prüfung lesen ihn).</summary>
public sealed record Kartenvorkommen(string Kennung, string Betrieb, Kartenort Punkt);

/// <summary>Ein Platz des Probeplans (<c>probeplan</c> der Greybox): ein Gebäude, das zur Probe in eine Bauzone passen muss.</summary>
public sealed record Probeplatz(string Art, string Zone, Kartenort Punkt, double Breite, double Tiefe, double Drehung);

/// <summary>
/// Die bearbeitbare Karte der Kartenwerkstatt. Entsteht aus dem JSON-Text einer Kartendatei (<see cref="AusJson"/>) oder neu und leer
/// (<see cref="Neu"/>), wird nur über die Befehle geändert (<see cref="Setzen"/>, <see cref="Verschieben"/>, <see cref="Drehen"/>,
/// <see cref="Loeschen"/>, <see cref="Umbenennen"/> …, jeweils mit <see cref="Rueckgaengig"/> und <see cref="Wiederholen"/>) und
/// mit <see cref="ZuJson"/> wieder geschrieben. Kennt Godot nicht.
/// </summary>
public sealed class Kartenentwurf
{
    /// <summary>Alle Felder der Datei in ihrer Reihenfolge; unter „gebaeude“ stehen die Gebäude erst beim Schreiben (<see cref="ZuJson"/>).</summary>
    private readonly JsonObject _daten;

    private readonly List<EntwurfGebaeude> _gebaeude;

    /// <summary>Die übrigen Teile der Karte in den Typen der Stadtkarte (für Prüfung und Darstellung), gelesen beim Öffnen.</summary>
    private readonly Stadtkarte _teile;

    private List<Befund>? _befunde;

    public Gebaeudekatalog Katalog { get; }

    /// <summary>Rückgängig und Wiederholen (höchstens <see cref="Kartengeschichte.Grenze"/> Schritte).</summary>
    public Kartengeschichte Geschichte { get; }

    /// <summary>Welche Regeln die Prüfung anlegt – nach der Kennung der Karte (<see cref="Pruefregeln.Fuer"/>), änderbar.</summary>
    public Pruefregeln Regeln { get; set; }

    /// <summary>Nach jedem Befehl, jedem Rückgängig und jedem Wiederholen: welches Gebäude sich wie geändert hat.</summary>
    public event Action<Kartenaenderung>? Geaendert;

    private Kartenentwurf(JsonObject daten, List<EntwurfGebaeude> gebaeude, Stadtkarte teile, Gebaeudekatalog katalog)
    {
        (_daten, _gebaeude, _teile, Katalog) = (daten, gebaeude, teile, katalog);
        foreach (var g in _gebaeude) g.Baustein = katalog.Finde(g.Katalog);
        Geschichte = new Kartengeschichte(this);
        Regeln = Pruefregeln.Fuer(Kennung);
        Vorkommen = LiesVorkommen();
        Probeplan = LiesProbeplan();
    }

    // ── Kopf der Karte ───────────────────────────────────────────────────────

    public string Kennung => Text("kennung");

    /// <summary>Anzeigename der Stadt (Feld <c>stadt</c>).</summary>
    public string Stadt => Text("stadt");

    public double Raster => _teile.Raster;

    /// <summary>
    /// Größe der Karte [Breite in x, Tiefe in z] um den Ursprung (Feld <c>groesse</c>, nur bei Karten aus der Werkstatt). Fehlt sie,
    /// null – dann zeigt <see cref="Grenzen"/>, wie weit die Karte reicht.
    /// </summary>
    public (double Breite, double Tiefe)? Groesse =>
        _daten["groesse"] is JsonArray { Count: 2 } g ? (g[0]!.GetValue<double>(), g[1]!.GetValue<double>()) : null;

    // ── Inhalt ───────────────────────────────────────────────────────────────

    /// <summary>Die Gebäude in der Reihenfolge der Datei (die Reihenfolge zählt bei Einwohnerplätzen).</summary>
    public IReadOnlyList<EntwurfGebaeude> Gebaeude => _gebaeude;

    public IReadOnlyList<Kartenweg> Wege => _teile.Wege;
    public IReadOnlyList<Kartenmauer> Mauern => _teile.Mauern;
    public IReadOnlyList<Bauzone> Bauzonen => _teile.Bauzonen;
    public Kartennatur Natur => _teile.Natur;
    public IReadOnlyList<Gewaesser> Wasser => _teile.Wasser;
    public IReadOnlyList<Gelaende> Gelaende => _teile.Gelaende;
    public IReadOnlyList<Hafen> Haefen => _teile.Haefen;
    public IReadOnlyList<Uebergang> Uebergaenge => _teile.Uebergaenge;
    public Startbelegung? Start => _teile.Start;
    public Kartenkamera? Kamera => _teile.Kamera;
    public IReadOnlyList<Kartenvorkommen> Vorkommen { get; }
    public IReadOnlyList<Probeplatz> Probeplan { get; }

    /// <summary>Das Gebäude mit dieser Kennung – oder null.</summary>
    public EntwurfGebaeude? GebaeudeMit(string? kennung) => kennung is null ? null : _gebaeude.FirstOrDefault(g => g.Kennung == kennung);

    /// <summary>
    /// Die Befunde der vollen Prüfung (<see cref="Kartenpruefung.Pruefen"/>) – nach jeder Änderung neu, sonst aus dem Zwischenspeicher.
    /// Die Darstellung liest sie nach <see cref="Geaendert"/>.
    /// </summary>
    public IReadOnlyList<Befund> Befunde => _befunde ??= Kartenpruefung.Pruefen(this);

    /// <summary>Hat die Karte Fehler (Hinweise zählen nicht)?</summary>
    public bool HatFehler => Befunde.Any(b => b.Schwere == Schwere.Fehler);

    /// <summary>Die Kennungen aller Gebäude mit einem Fehler (rot zeichnen).</summary>
    public IReadOnlySet<string> FehlerhafteGebaeude =>
        Befunde.Where(b => b.Schwere == Schwere.Fehler).SelectMany(b => b.Gebaeude).ToHashSet();

    /// <summary>Wie weit die Karte reicht (x0, z0, x1, z1): aus <see cref="Groesse"/>, sonst aus Wegen, Gebäuden, Wasser und Gelände.</summary>
    public (double X0, double Z0, double X1, double Z1) Grenzen()
    {
        if (Groesse is var (b, t)) return (-b / 2, -t / 2, b / 2, t / 2);
        var punkte = _gebaeude.SelectMany(g => g.Ecken() is { Count: > 0 } e ? e : new[] { g.Punkt })
            .Concat(Wege.SelectMany(w => w.Linie).Concat(Gelaende.SelectMany(g => g.Flaeche))
                .Concat(Wasser.SelectMany(w => (w.Linie ?? new()).Concat(w.Flaeche ?? new()))).Select(Kartenort.Aus))
            .ToList();
        if (punkte.Count == 0) return (-50, -50, 50, 50);
        return (punkte.Min(p => p.X) - 10, punkte.Min(p => p.Z) - 10, punkte.Max(p => p.X) + 10, punkte.Max(p => p.Z) + 10);
    }

    // ── Öffnen, Neu, Schreiben ───────────────────────────────────────────────

    /// <summary>
    /// Öffnet eine Kartendatei (JSON-Text) zum Bearbeiten. Anders als <see cref="Stadtkarte.AusJson"/> darf sie Regelfehler enthalten –
    /// die zeigt die Prüfung. Nur was sich gar nicht lesen lässt (kein JSON, falsche Typen), wirft.
    /// </summary>
    /// <exception cref="KartenFehler">Wenn der Text kein gültiges JSON ist oder ein Feld den falschen Typ hat.</exception>
    public static Kartenentwurf AusJson(string json, Gebaeudekatalog katalog)
    {
        JsonObject daten;
        try
        {
            daten = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })
                as JsonObject ?? throw new KartenFehler(new[] { "Die Datei ist kein JSON-Objekt." });
        }
        catch (JsonException ex)
        {
            throw new KartenFehler(new[] { $"Kein gültiges JSON (Zeile {ex.LineNumber + 1}): {ex.Message}" });
        }

        var gebaeude = new List<EntwurfGebaeude>();
        if (daten["gebaeude"] is JsonArray liste)
            for (var i = 0; i < liste.Count; i++)
                gebaeude.Add(EntwurfGebaeude.AusJson(liste[i] as JsonObject ?? throw new KartenFehler(new[] { $"Gebäude Nr. {i + 1} ist kein Objekt." }), i + 1));
        else if (daten.ContainsKey("gebaeude"))
            throw new KartenFehler(new[] { "gebaeude ist keine Liste." });

        // Die übrigen Teile mit dem Leser der Stadtkarte (ohne Gebäude, ohne Prüfung)
        var ohneGebaeude = daten.DeepClone().AsObject();
        ohneGebaeude.Remove("gebaeude");
        Stadtkarte teile;
        try
        {
            teile = JsonSerializer.Deserialize<Stadtkarte>(ohneGebaeude.ToJsonString(), Stadtkarte.Optionen)
                    ?? throw new KartenFehler(new[] { "Die Datei ist leer." });
        }
        catch (JsonException ex)
        {
            throw new KartenFehler(new[] { $"Die Karte ist nicht lesbar ({ex.Path}): {ex.Message}" });
        }
        return new Kartenentwurf(daten, gebaeude, teile, katalog);
    }

    /// <summary>
    /// Eine neue, leere Karte (Format 2) mit Kennung, Anzeigename und Größe in Metern (um den Ursprung = Marktplatz).
    /// </summary>
    public static Kartenentwurf Neu(string kennung, string stadt, double breite, double tiefe, Gebaeudekatalog katalog)
    {
        var daten = new JsonObject
        {
            ["format"] = Stadtkarte.BekanntesFormat, ["kennung"] = kennung, ["stadt"] = stadt, ["einheit"] = "Meter",
            ["achsen"] = "x = Osten, z = Süden (−z = Norden), Ursprung am Marktplatz", ["raster"] = 2,
            ["groesse"] = new JsonArray(JsonValue.Create(breite), JsonValue.Create(tiefe)),
            ["gebaeude"] = new JsonArray(), ["mauern"] = new JsonArray(), ["wege"] = new JsonArray(), ["bauzonen"] = new JsonArray(),
        };
        return new Kartenentwurf(daten, new List<EntwurfGebaeude>(), new Stadtkarte { Kennung = kennung, Stadt = stadt }, katalog);
    }

    /// <summary>
    /// Die Karte als Text der Kartendatei (Format 2), im Zeilenformat des Werkzeugs: Eine unverändert gespeicherte Karte ist Byte für Byte
    /// dieselbe Datei. Leere freiwillige Abschnitte (wasser, start …) fallen weg wie beim Werkzeug.
    /// </summary>
    public string ZuJson()
    {
        var aus = new JsonObject();
        foreach (var (k, v) in _daten)
        {
            if (k == "gebaeude")
            {
                aus[k] = new JsonArray(_gebaeude.Select(g => (JsonNode)g.AlsJson()).ToArray());
                continue;
            }
            if (Freiwillig.Contains(k) && v is null or JsonArray { Count: 0 } or JsonObject { Count: 0 }) continue;
            if (k == "natur" && v is JsonObject n && n["flaechen"] is JsonArray { Count: 0 } && n["pflanzen"] is JsonArray { Count: 0 }) continue;
            aus[k] = v?.DeepClone();
        }
        return Kartenschreiber.Kompakt(aus);
    }

    /// <summary>Abschnitte, die das Werkzeug weglässt, wenn sie leer sind.</summary>
    private static readonly HashSet<string> Freiwillig = new()
    {
        "wasser", "gelaende", "haefen", "uebergaenge", "start", "kamera", "vorkommen", "fiktion", "probeplan",
    };

    /// <summary>
    /// Die Karte zum Spielen (geprüft wie beim Laden einer Partie). Für „Karte ausprobieren“ (Schritt 3).
    /// </summary>
    /// <exception cref="KartenFehler">Mit den Fehlern, die der Kern beim Lesen findet.</exception>
    public Stadtkarte ZuStadtkarte() => Stadtkarte.AusJson(ZuJson(), Katalog);

    // ── Befehle (je ein Schritt in der Geschichte) ───────────────────────────

    /// <summary>
    /// Setzt einen Baustein des Katalogs mit der Mitte seiner Grundfläche auf (x, z), gedreht um <paramref name="drehung"/> Grad.
    /// Bekommt eine eindeutige Kennung aus dem Namen des Bausteins („Werkstattplatz1“) und dessen Namen als Vorschlag.
    /// Punkt auf 5 cm, Drehung auf 0,1° (das Raster von 1 m bzw. 15° wählt die Darstellung, siehe <see cref="Kartenraster"/>).
    /// </summary>
    /// <exception cref="ArgumentException">Wenn es den Baustein nicht gibt oder er nicht unter <c>gebaeude</c> stehen darf.</exception>
    public EntwurfGebaeude Setzen(string katalog, double x, double z, double drehung = 0)
    {
        var b = Katalog.Finde(katalog) ?? throw new ArgumentException($"Den Baustein „{katalog}“ gibt es im Katalog nicht.", nameof(katalog));
        if (!Bausteinart.ImGebaeude.Contains(b.Art))
            throw new ArgumentException($"Ein Baustein der Art „{b.Art}“ steht nicht unter gebaeude.", nameof(katalog));
        var g = new EntwurfGebaeude
        {
            Kennung = NeueKennung(b), Katalog = katalog, Name = NamensVorschlag(b), Baustein = b,
            X = Kartenraster.Punkt(x), Z = Kartenraster.Punkt(z), Drehung = Kartenraster.Drehung(drehung),
        };
        Geschichte.Ausfuehren(new GebaeudeSetzen(g, _gebaeude.Count));
        return g;
    }

    /// <summary>Verschiebt ein Gebäude (Mitte der Grundfläche nach x, z).</summary>
    public void Verschieben(string kennung, double x, double z) =>
        Aendern(kennung, "verschoben", g => (g.X, g.Z) = (Kartenraster.Punkt(x), Kartenraster.Punkt(z)));

    /// <summary>Dreht ein Gebäude auf <paramref name="drehung"/> Grad (nicht: um).</summary>
    public void Drehen(string kennung, double drehung) => Aendern(kennung, "gedreht", g => g.Drehung = Kartenraster.Drehung(drehung));

    /// <summary>Verschiebt und dreht in einem Schritt (Ziehen mit Q/E: ein Rückgängig nimmt beides zurück).</summary>
    public void Versetzen(string kennung, double x, double z, double drehung) =>
        Aendern(kennung, "versetzt", g => (g.X, g.Z, g.Drehung) = (Kartenraster.Punkt(x), Kartenraster.Punkt(z), Kartenraster.Drehung(drehung)));

    public void Umbenennen(string kennung, string name) => Aendern(kennung, "umbenannt", g => g.Name = name);

    /// <summary>Name des Betriebs, der hier steht (Werkstattplatz eines KI-Hauses, Umlandbetrieb); null oder leer entfernt ihn.</summary>
    public void BetriebSetzen(string kennung, string? betrieb) =>
        Aendern(kennung, "Betrieb geändert", g => g.Betrieb = string.IsNullOrWhiteSpace(betrieb) ? null : betrieb);

    /// <summary>Lage eines Wohnhausplatzes (null: die nächste Bauzone gilt).</summary>
    public void LageSetzen(string kennung, Lage? lage) => Aendern(kennung, "Lage geändert", g => g.Lage = lage);

    public void MassstabSetzen(string kennung, double massstab) => Aendern(kennung, "Maßstab geändert", g => g.Massstab = Math.Round(massstab, 3));

    public void KonfessionSetzen(string kennung, string? konfession) =>
        Aendern(kennung, "Konfession geändert", g => g.Konfession = string.IsNullOrWhiteSpace(konfession) ? null : konfession);

    /// <summary>Löscht ein Gebäude (Rückgängig setzt es an dieselbe Stelle der Reihenfolge zurück).</summary>
    public void Loeschen(string kennung)
    {
        var g = GebaeudeMit(kennung) ?? throw new ArgumentException($"Kein Gebäude „{kennung}“.", nameof(kennung));
        Geschichte.Ausfuehren(new GebaeudeLoeschen(g, _gebaeude.IndexOf(g)));
    }

    public bool Rueckgaengig() => Geschichte.Rueckgaengig();
    public bool Wiederholen() => Geschichte.Wiederholen();

    /// <summary>Ändert ein Gebäude über eine Kopie; ändert sich nichts, entsteht kein Schritt.</summary>
    private void Aendern(string kennung, string was, Action<EntwurfGebaeude> aenderung)
    {
        var g = GebaeudeMit(kennung) ?? throw new ArgumentException($"Kein Gebäude „{kennung}“.", nameof(kennung));
        var nachher = g.Kopie();
        aenderung(nachher);
        if (Gleich(g, nachher)) return;
        Geschichte.Ausfuehren(new GebaeudeAendern($"{g.Name} {was}", g, g.Kopie(), nachher));
    }

    private static bool Gleich(EntwurfGebaeude a, EntwurfGebaeude b) =>
        a.X == b.X && a.Z == b.Z && a.Drehung == b.Drehung && a.Name == b.Name && a.Massstab == b.Massstab && a.Betrieb == b.Betrieb
        && a.Lage == b.Lage && a.Konfession == b.Konfession && a.Katalog == b.Katalog;

    /// <summary>„Werkstattplatz1“, „Werkstattplatz2“ … – die kleinste freie Nummer hinter der Kennung aus dem Namen des Bausteins.</summary>
    private string NeueKennung(Baustein b)
    {
        var basis = KennungVon(b.Anzeigename);
        var belegt = _gebaeude.Select(g => g.Kennung).ToHashSet();
        var n = 1;
        while (belegt.Contains(basis + n)) n++;
        return basis + n;
    }

    /// <summary>Der Name des Bausteins, ab dem zweiten gleichen mit Nummer („Werkstattplatz 2“).</summary>
    private string NamensVorschlag(Baustein b)
    {
        var namen = _gebaeude.Select(g => g.Name).ToHashSet();
        if (!namen.Contains(b.Anzeigename)) return b.Anzeigename;
        var n = 2;
        while (namen.Contains($"{b.Anzeigename} {n}")) n++;
        return $"{b.Anzeigename} {n}";
    }

    /// <summary>Kennung aus einem Namen wie im Werkzeug (<c>kennung_von</c>): Umlaute umschrieben, sonst nur Buchstaben und Ziffern.</summary>
    public static string KennungVon(string name)
    {
        foreach (var (a, b) in new[] { ("ä", "ae"), ("ö", "oe"), ("ü", "ue"), ("ß", "ss"), ("Ä", "Ae"), ("Ö", "Oe"), ("Ü", "Ue") })
            name = name.Replace(a, b);
        return new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
    }

    // ── Für die Befehle ──────────────────────────────────────────────────────

    internal void Einfuegen(EntwurfGebaeude g, int index)
    {
        _gebaeude.Insert(Math.Min(index, _gebaeude.Count), g);
        Melden(new Kartenaenderung(Aenderungsart.Gesetzt, g));
    }

    internal void Entfernen(EntwurfGebaeude g)
    {
        _gebaeude.Remove(g);
        Melden(new Kartenaenderung(Aenderungsart.Geloescht, g));
    }

    internal void Uebernehmen(EntwurfGebaeude g, EntwurfGebaeude werte)
    {
        g.Uebernimm(werte);
        g.Baustein = Katalog.Finde(g.Katalog);
        Melden(new Kartenaenderung(Aenderungsart.Geaendert, g));
    }

    private void Melden(Kartenaenderung a)
    {
        _befunde = null;
        Geaendert?.Invoke(a);
    }

    // ── Lesen der Felder, die die Stadtkarte nicht kennt ─────────────────────

    private string Text(string feld) => _daten[feld] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";

    private List<Kartenvorkommen> LiesVorkommen() =>
        (_daten["vorkommen"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()
        .Select(v => new Kartenvorkommen((string?)v["kennung"] ?? "", (string?)v["betrieb"] ?? "", OrtVon(v["punkt"]))).ToList();

    private List<Probeplatz> LiesProbeplan() =>
        (_daten["probeplan"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()
        .Select(p => new Probeplatz((string?)p["art"] ?? "", (string?)p["zone"] ?? "", OrtVon(p["punkt"]),
            (double?)p["groesse"]?[0] ?? 0, (double?)p["groesse"]?[1] ?? 0, (double?)p["drehung"] ?? 0)).ToList();

    private static Kartenort OrtVon(JsonNode? p) => p is JsonArray { Count: 2 } a ? new Kartenort((double)a[0]!, (double)a[1]!) : default;
}
