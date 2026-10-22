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
//  Bearbeitbar sind die Gebäude (Schritt 1), die Wege, Bauzonen, Naturflächen und einzelnen Pflanzen (Schritt 2, EntwurfTeile.cs und
//  Kartenentwurf.Wege.cs, Kartenentwurf.Flaechen.cs), die Mauern mit Toren und die Startbelegung (Schritt 3, Kartenentwurf.Mauern.cs und
//  Kartenentwurf.Start.cs). Alles Übrige (Wasser, Kamera …) liegt als JSON im Entwurf und wird für die Prüfung einmal in die Typen der
//  Stadtkarte gelesen (Gewaesser …).
//  Geändert wird nur über Befehle (Setzen, Verschieben, Drehen …), damit Rückgängig und Wiederholen immer stimmen.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Gebäude im Entwurf: dieselben Felder wie <see cref="Kartengebaeude"/>, aber veränderbar (nur über die Befehle des Entwurfs).</summary>
public sealed class EntwurfGebaeude : EntwurfTeil
{
    public override Kartenteil Teil => Kartenteil.Gebaeude;
    public override string Bezeichnung => Name;

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

    internal override EntwurfTeil KopieTeil() => Kopie();
    internal override void UebernimmTeil(EntwurfTeil werte) => Uebernimm((EntwurfGebaeude)werte);

    internal override bool GleicheWerte(EntwurfTeil anderer) => anderer is EntwurfGebaeude b && X == b.X && Z == b.Z && Drehung == b.Drehung
        && Name == b.Name && Massstab == b.Massstab && Betrieb == b.Betrieb && Lage == b.Lage && Konfession == b.Konfession && Katalog == b.Katalog;

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
    internal override JsonObject AlsJson()
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

/// <summary>Was sich am Entwurf geändert hat (für die Darstellung: nur diesen Teil neu zeichnen).</summary>
public enum Aenderungsart
{
    /// <summary>Der Teil ist neu in der Karte (auch: ein Löschen wurde zurückgenommen).</summary>
    Gesetzt,

    /// <summary>Der Teil ist weg (auch: ein Setzen wurde zurückgenommen).</summary>
    Geloescht,

    /// <summary>Punkt, Drehung, Name, Stützpunkte, Ecken … des Teils haben sich geändert (das Objekt bleibt dasselbe).</summary>
    Geaendert,
}

/// <summary>
/// Eine Änderung am Entwurf: welche Art, welcher Teil (Gebäude, Weg, Bauzone, Naturfläche, Pflanze). Ein Befehl kann mehrere Teile ändern
/// (ein Weg rastet am Nachbarn ein, der Pinsel setzt viele Pflanzen) – dann kommt je Teil eine Änderung, alle erst, wenn die Geschichte
/// den Schritt kennt.
/// </summary>
public readonly record struct Kartenaenderung(Aenderungsart Art, EntwurfTeil Objekt)
{
    public Kartenteil Teil => Objekt.Teil;

    /// <summary>Das Gebäude – oder null, wenn sich ein anderer Teil geändert hat.</summary>
    public EntwurfGebaeude? Gebaeude => Objekt as EntwurfGebaeude;

    public EntwurfWeg? Weg => Objekt as EntwurfWeg;
    public EntwurfBauzone? Bauzone => Objekt as EntwurfBauzone;
    public EntwurfNaturflaeche? Naturflaeche => Objekt as EntwurfNaturflaeche;
    public EntwurfPflanze? Pflanze => Objekt as EntwurfPflanze;
    public EntwurfMauer? Mauer => Objekt as EntwurfMauer;
    public EntwurfStart? Start => Objekt as EntwurfStart;
}

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
public sealed partial class Kartenentwurf
{
    /// <summary>Alle Felder der Datei in ihrer Reihenfolge; unter „gebaeude“ stehen die Gebäude erst beim Schreiben (<see cref="ZuJson"/>).</summary>
    private readonly JsonObject _daten;

    private readonly List<EntwurfGebaeude> _gebaeude;
    private readonly List<EntwurfWeg> _wege;
    private readonly List<EntwurfBauzone> _bauzonen;
    private readonly List<EntwurfNaturflaeche> _naturflaechen;
    private readonly List<EntwurfPflanze> _pflanzen;
    private readonly List<EntwurfMauer> _mauern;
    private readonly EntwurfStart _start;

    /// <summary>Änderungen des laufenden Befehls – gemeldet erst, wenn die Geschichte den Schritt kennt (<see cref="MeldungenAusloesen"/>).</summary>
    private readonly List<Kartenaenderung> _ausstehend = new();

    /// <summary>Die übrigen Teile der Karte in den Typen der Stadtkarte (für Prüfung und Darstellung), gelesen beim Öffnen.</summary>
    private readonly Stadtkarte _teile;

    private List<Befund>? _befunde;

    public Gebaeudekatalog Katalog { get; }

    /// <summary>Rückgängig und Wiederholen (höchstens <see cref="Kartengeschichte.Grenze"/> Schritte).</summary>
    public Kartengeschichte Geschichte { get; }

    /// <summary>Welche Regeln die Prüfung anlegt – nach der Kennung der Karte (<see cref="Pruefregeln.Fuer"/>), änderbar.</summary>
    public Pruefregeln Regeln { get; set; }

    /// <summary>
    /// Nach jedem Befehl, jedem Rückgängig und jedem Wiederholen: welcher Teil sich wie geändert hat. Kommt erst, wenn die Geschichte den
    /// Schritt kennt – <see cref="Kartengeschichte.KannRueckgaengig"/> und <see cref="Kartengeschichte.IstGeaendert"/> sind schon neu.
    /// </summary>
    public event Action<Kartenaenderung>? Geaendert;

    private Kartenentwurf(JsonObject daten, List<EntwurfGebaeude> gebaeude, Stadtkarte teile, Gebaeudekatalog katalog,
        List<EntwurfWeg> wege, List<EntwurfBauzone> bauzonen, List<EntwurfNaturflaeche> naturflaechen, List<EntwurfPflanze> pflanzen,
        List<EntwurfMauer> mauern, EntwurfStart start)
    {
        (_daten, _gebaeude, _teile, Katalog) = (daten, gebaeude, teile, katalog);
        (_wege, _bauzonen, _naturflaechen, _pflanzen, _mauern, _start) = (wege, bauzonen, naturflaechen, pflanzen, mauern, start);
        foreach (var g in _gebaeude) g.Baustein = katalog.Finde(g.Katalog);
        foreach (var p in _pflanzen) p.Baustein = katalog.Finde(p.Katalog);
        Geschichte = new Kartengeschichte(this);
        Regeln = Pruefregeln.Fuer(Kennung);
        _kamera = teile.Kamera;
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

    /// <summary>Die Wege in der Reihenfolge der Datei.</summary>
    public IReadOnlyList<EntwurfWeg> Wege => _wege;

    public IReadOnlyList<EntwurfBauzone> Bauzonen => _bauzonen;

    /// <summary>Die Wald- und Streuflächen (<c>natur.flaechen</c>).</summary>
    public IReadOnlyList<EntwurfNaturflaeche> Naturflaechen => _naturflaechen;

    /// <summary>Die einzeln gesetzten Pflanzen (<c>natur.pflanzen</c>).</summary>
    public IReadOnlyList<EntwurfPflanze> Pflanzen => _pflanzen;

    public IReadOnlyList<Gewaesser> Wasser => _teile.Wasser;
    public IReadOnlyList<Gelaende> Gelaende => _teile.Gelaende;
    public IReadOnlyList<Hafen> Haefen => _teile.Haefen;
    public IReadOnlyList<Uebergang> Uebergaenge => _teile.Uebergaenge;
    public Kartenkamera? Kamera => _kamera;
    public IReadOnlyList<Kartenvorkommen> Vorkommen => _vorkommen ??= LiesVorkommen();
    public IReadOnlyList<Probeplatz> Probeplan => _probeplan ??= LiesProbeplan();

    /// <summary>Der Text <c>vorlage</c> (wonach die Karte entstanden ist) oder "".</summary>
    public string Vorlage => Text("vorlage");

    /// <summary>Die Abweichungen von der Vorlage (<c>fiktion</c>).</summary>
    public IReadOnlyList<string> Fiktion => (_daten["fiktion"] as JsonArray ?? new JsonArray()).Select(f => (string?)f ?? "").ToList();

    private Kartenkamera? _kamera;
    private List<Kartenvorkommen>? _vorkommen;
    private List<Probeplatz>? _probeplan;

    /// <summary>Das Gebäude mit dieser Kennung – oder null.</summary>
    public EntwurfGebaeude? GebaeudeMit(string? kennung) => kennung is null ? null : _gebaeude.FirstOrDefault(g => g.Kennung == kennung);

    /// <summary>
    /// Die Befunde der vollen Prüfung (<see cref="Kartenpruefung.Pruefen"/>) – nach jeder Änderung neu, sonst aus dem Zwischenspeicher.
    /// Die Darstellung liest sie nach <see cref="Geaendert"/>.
    /// </summary>
    public IReadOnlyList<Befund> Befunde => _befunde ??= Kartenpruefung.Pruefen(this);

    /// <summary>
    /// Die Kapazitätsprüfung dieses Entwurfs (<see cref="Kapazitaetspruefung"/>) – aus dem Zwischenspeicher, bis sich ein Gebäude, ein Weg oder
    /// eine Bauzone ändert (nur davon hängt sie ab; in Köln dauert sie einige 10 ms).
    /// </summary>
    public Kapazitaet Kapazitaetsrechnung(Schwere schwere = Schwere.Fehler) =>
        _kapazitaet is { } k && k.Schwere == schwere ? k.Ergebnis : (_kapazitaet = (schwere, Kapazitaetspruefung.Pruefen(this, schwere))).Value.Ergebnis;

    private (Schwere Schwere, Kapazitaet Ergebnis)? _kapazitaet;

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
            .Concat(_wege.SelectMany(w => w.Punkte)).Concat(_mauern.SelectMany(m => m.Ecken))
            .Concat(Gelaende.SelectMany(g => g.Flaeche).Concat(Wasser.SelectMany(w => (w.Linie ?? new()).Concat(w.Flaeche ?? new()))).Select(Kartenort.Aus))
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

        // Wege, Bauzonen und Natur: je Eintrag in den Typen der Stadtkarte gelesen, der Eintrag selbst bleibt als Vorlage fürs Schreiben
        var wege = Teilleser.Liste<Kartenweg>(daten["wege"], "Weg").Select(x => EntwurfWeg.Aus(x.Wert, x.Vorlage)).ToList();
        var bauzonen = Teilleser.Liste<Bauzone>(daten["bauzonen"], "Bauzone").Select(x => EntwurfBauzone.Aus(x.Wert, x.Vorlage)).ToList();
        if (daten["natur"] is not (null or JsonObject)) throw new KartenFehler(new[] { "natur ist kein Objekt." });
        var flaechen = Teilleser.Liste<Naturflaeche>(daten["natur"]?["flaechen"], "Naturfläche").Select(x => EntwurfNaturflaeche.Aus(x.Wert, x.Vorlage)).ToList();
        var pflanzen = Teilleser.Liste<Pflanze>(daten["natur"]?["pflanzen"], "Pflanze").Select(x => EntwurfPflanze.Aus(x.Wert, x.Vorlage)).ToList();
        var mauern = Teilleser.Liste<Kartenmauer>(daten["mauern"], "Mauer").Select(x => EntwurfMauer.Aus(x.Wert, x.Vorlage)).ToList();
        if (daten["start"] is not (null or JsonObject)) throw new KartenFehler(new[] { "start ist kein Objekt." });
        var start = EntwurfStart.Aus(daten["start"]?.DeepClone() as JsonObject, Teilleser.Einzeln<Startbelegung>(daten["start"], "start"));

        // Die übrigen Teile mit dem Leser der Stadtkarte (ohne Prüfung)
        var rest = daten.DeepClone().AsObject();
        foreach (var k in new[] { "gebaeude", "wege", "bauzonen", "natur", "mauern", "start" }) rest.Remove(k);
        Stadtkarte teile;
        try
        {
            teile = JsonSerializer.Deserialize<Stadtkarte>(rest.ToJsonString(), Stadtkarte.Optionen)
                    ?? throw new KartenFehler(new[] { "Die Datei ist leer." });
        }
        catch (JsonException ex)
        {
            throw new KartenFehler(new[] { $"Die Karte ist nicht lesbar ({ex.Path}): {ex.Message}" });
        }
        return new Kartenentwurf(MitAbschnitten(daten), gebaeude, teile, katalog, wege, bauzonen, flaechen, pflanzen, mauern, start);
    }

    /// <summary>Die Abschnitte in der Reihenfolge, in der das Werkzeug sie schreibt (karte.py) – bis <c>start</c>, dem letzten, den der Entwurf anlegen kann.</summary>
    private static readonly string[] Reihenfolge =
    {
        "format", "kennung", "stadt", "vorlage", "einheit", "achsen", "raster", "groesse", "gebaeude", "mauern", "wege", "bauzonen", "natur",
        "wasser", "gelaende", "haefen", "uebergaenge", "start", "kamera", "vorkommen", "fiktion", "probeplan",
    };

    /// <summary>
    /// Fehlen <c>mauern</c>, <c>wege</c>, <c>bauzonen</c>, <c>natur</c> oder <c>start</c>, bekommen sie einen leeren Platz an ihrer Stelle der
    /// Reihenfolge (null) – geschrieben werden sie nur, wenn sie etwas enthalten. So landet ein erster Weg in Köln-artigen Dateien an der
    /// richtigen Stelle.
    /// </summary>
    private static JsonObject MitAbschnitten(JsonObject daten)
    {
        var liste = daten.Select(e => (e.Key, Wert: e.Value?.DeepClone())).ToList();
        foreach (var fehlt in new[] { "mauern", "wege", "bauzonen", "natur", "start" }.Where(k => !daten.ContainsKey(k)))
        {
            var davor = Reihenfolge.TakeWhile(k => k != fehlt).ToHashSet();
            var i = liste.FindLastIndex(e => davor.Contains(e.Key));
            liste.Insert(i < 0 ? liste.Count : i + 1, (fehlt, null));
        }
        return new JsonObject(liste.Select(e => new KeyValuePair<string, JsonNode?>(e.Key, e.Wert)));
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
        return new Kartenentwurf(MitAbschnitten(daten), new List<EntwurfGebaeude>(), new Stadtkarte { Kennung = kennung, Stadt = stadt }, katalog,
            new List<EntwurfWeg>(), new List<EntwurfBauzone>(), new List<EntwurfNaturflaeche>(), new List<EntwurfPflanze>(), new List<EntwurfMauer>(),
            new EntwurfStart());
    }

    /// <summary>
    /// Die Karte als Text der Kartendatei (Format 2), im Zeilenformat des Werkzeugs: Eine unverändert gespeicherte Karte ist Byte für Byte
    /// dieselbe Datei. Leere freiwillige Abschnitte (wasser, start …) fallen weg wie beim Werkzeug.
    /// </summary>
    public string ZuJson()
    {
        var aus = new JsonObject();
        static JsonArray Liste(IEnumerable<EntwurfTeil> teile) => new(teile.Select(t => (JsonNode)t.AlsJson()).ToArray());
        foreach (var (k, v) in _daten)
        {
            switch (k)
            {
                case "gebaeude":
                    aus[k] = Liste(_gebaeude);
                    continue;
                case "wege" or "bauzonen" or "mauern":
                    // ein leerer Platz (die Datei hatte den Abschnitt nicht) bleibt leer, solange nichts darin steht
                    var teile = k switch
                    {
                        "wege" => _wege.Cast<EntwurfTeil>().ToList(),
                        "bauzonen" => _bauzonen.Cast<EntwurfTeil>().ToList(),
                        _ => _mauern.Cast<EntwurfTeil>().ToList(),
                    };
                    if (v is not null || teile.Count > 0) aus[k] = Liste(teile);
                    continue;
                case "start":
                    // ohne Inhalt fällt start weg (dann gilt im Spiel der Ersatz) – wie beim Werkzeug
                    if (!_start.IstLeer) aus[k] = _start.AlsJson();
                    continue;
                case "natur":
                    if (_naturflaechen.Count == 0 && _pflanzen.Count == 0) continue; // wie das Werkzeug: leere Natur fällt weg
                    var natur = new JsonObject();
                    foreach (var (nk, nv) in v as JsonObject ?? new JsonObject())
                        natur[nk] = nk is "flaechen" or "pflanzen" ? null : nv?.DeepClone();
                    natur["flaechen"] = Liste(_naturflaechen);
                    natur["pflanzen"] = Liste(_pflanzen);
                    aus[k] = natur;
                    continue;
            }
            if (Freiwillig.Contains(k) && v is null or JsonArray { Count: 0 } or JsonObject { Count: 0 }) continue;
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
        Geschichte.Ausfuehren(new TeilSetzen(g, _gebaeude.Count));
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
        Geschichte.Ausfuehren(new TeilLoeschen(g, _gebaeude.IndexOf(g)));
    }

    public bool Rueckgaengig() => Geschichte.Rueckgaengig();
    public bool Wiederholen() => Geschichte.Wiederholen();

    /// <summary>Ändert ein Gebäude über eine Kopie; ändert sich nichts, entsteht kein Schritt.</summary>
    private void Aendern(string kennung, string was, Action<EntwurfGebaeude> aenderung)
    {
        var g = GebaeudeMit(kennung) ?? throw new ArgumentException($"Kein Gebäude „{kennung}“.", nameof(kennung));
        Aendern(g, was, aenderung);
    }

    /// <summary>Ändert einen Teil über eine Kopie (ein Schritt „{Bezeichnung} {was}“); ändert sich nichts, entsteht kein Schritt.</summary>
    private void Aendern<T>(T teil, string was, Action<T> aenderung) where T : EntwurfTeil
    {
        if (Aenderung(teil, aenderung) is { } befehl) Geschichte.Ausfuehren(befehl.Benannt($"{teil.Bezeichnung} {was}"));
    }

    /// <summary>Der Befehl, der einen Teil wie angegeben ändert – null, wenn sich nichts ändert (zum Bündeln in einem Schritt).</summary>
    private TeilAendern? Aenderung<T>(T teil, Action<T> aenderung) where T : EntwurfTeil
    {
        if (!Enthaelt(teil)) throw new ArgumentException($"„{teil.Bezeichnung}“ gehört nicht (mehr) zu diesem Entwurf.", nameof(teil));
        var nachher = (T)teil.KopieTeil();
        aenderung(nachher);
        return nachher.GleicheWerte(teil) ? null : new TeilAendern("", teil, teil.KopieTeil(), nachher);
    }

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

    /// <summary>
    /// Kennung aus einem Namen wie im Werkzeug (<c>kennung_von</c>): Umlaute umschrieben, sonst nur Buchstaben und Ziffern, alles andere „_“ –
    /// mehrere Zeichen hintereinander werden ein „_“ („Bürgerhaus (Kulisse)“ → „Buergerhaus_Kulisse“). Bestehende Kennungen ändert das nicht.
    /// </summary>
    public static string KennungVon(string name)
    {
        foreach (var (a, b) in new[] { ("ä", "ae"), ("ö", "oe"), ("ü", "ue"), ("ß", "ss"), ("Ä", "Ae"), ("Ö", "Oe"), ("Ü", "Ue") })
            name = name.Replace(a, b);
        var t = new System.Text.StringBuilder();
        foreach (var c in name)
            if (char.IsLetterOrDigit(c)) t.Append(c);
            else if (t.Length > 0 && t[^1] != '_') t.Append('_');
        return t.ToString().Trim('_');
    }

    // ── Für die Befehle ──────────────────────────────────────────────────────

    /// <summary>Die Liste, in der ein Teil dieser Art steht.</summary>
    private System.Collections.IList Liste(EntwurfTeil t) => t.Teil switch
    {
        Kartenteil.Gebaeude => _gebaeude,
        Kartenteil.Weg => _wege,
        Kartenteil.Bauzone => _bauzonen,
        Kartenteil.Naturflaeche => _naturflaechen,
        Kartenteil.Mauer => _mauern,
        Kartenteil.Pflanze => _pflanzen,
        _ => throw new ArgumentException($"„{t.Bezeichnung}“ steht in keiner Liste.", nameof(t)),
    };

    /// <summary>Steht der Teil (dieses Objekt) im Entwurf?</summary>
    public bool Enthaelt(EntwurfTeil t) => IndexVon(t) >= 0;

    /// <summary>Die Stelle des Teils in seiner Liste (−1: nicht im Entwurf).</summary>
    internal int IndexVon(EntwurfTeil t)
    {
        if (t is EntwurfStart) return ReferenceEquals(t, _start) ? 0 : -1;
        var l = Liste(t);
        for (var i = 0; i < l.Count; i++)
            if (ReferenceEquals(l[i], t)) return i;
        return -1;
    }

    internal void Einfuegen(EntwurfTeil t, int index)
    {
        var l = Liste(t);
        l.Insert(Math.Min(index, l.Count), t);
        Vormerken(new Kartenaenderung(Aenderungsart.Gesetzt, t));
    }

    internal void Entfernen(EntwurfTeil t)
    {
        Liste(t).RemoveAt(IndexVon(t));
        Vormerken(new Kartenaenderung(Aenderungsart.Geloescht, t));
    }

    internal void Uebernehmen(EntwurfTeil t, EntwurfTeil werte)
    {
        t.UebernimmTeil(werte);
        if (t is EntwurfGebaeude g) g.Baustein = Katalog.Finde(g.Katalog);
        if (t is EntwurfPflanze p) p.Baustein = Katalog.Finde(p.Katalog);
        Vormerken(new Kartenaenderung(Aenderungsart.Geaendert, t));
    }

    private void Vormerken(Kartenaenderung a)
    {
        _befunde = null;
        if (a.Teil is Kartenteil.Gebaeude or Kartenteil.Weg or Kartenteil.Bauzone) _kapazitaet = null;
        _ausstehend.Add(a);
    }

    /// <summary>Meldet die Änderungen des letzten Befehls (aus der Geschichte, nachdem sie den Schritt eingetragen hat).</summary>
    internal void MeldungenAusloesen()
    {
        var liste = _ausstehend.ToList();
        _ausstehend.Clear();
        foreach (var a in liste) Geaendert?.Invoke(a);
    }

    // ── Kopf der Karte für Kartenskripte ─────────────────────────────────────
    //  Vorlage, Fiktion, Kamera und Vorkommen bearbeitet die Kartenwerkstatt (noch) nicht; ein Kartenskript (tools/Kartenbau) setzt sie beim
    //  Anlegen einer Stadt. Diese Aufrufe sind kein Schritt in der Geschichte (kein Rückgängig) und melden keine Änderung.

    /// <summary>Wonach die Karte entstanden ist (Feld <c>vorlage</c>, z. B. „das spätmittelalterliche Köln am linken Rheinufer“).</summary>
    public void VorlageSetzen(string text) => FeldSetzen("vorlage", text);

    /// <summary>Hängt eine Abweichung von der Vorlage an (Feld <c>fiktion</c>).</summary>
    public void FiktionHinzufuegen(string text)
    {
        if (_daten["fiktion"] is not JsonArray liste) FeldSetzen("fiktion", liste = new JsonArray());
        liste.Add(text);
    }

    /// <summary>Der Blick beim Start: Punkt der Stadt, Zoom (Größe des Bildausschnitts in Metern) und Drehung (0 = wie heute).</summary>
    public void KameraSetzen(Kartenort punkt, double zoom, double drehung = 0)
    {
        var p = Gerundet(punkt);
        FeldSetzen("kamera", new JsonObject { ["punkt"] = EntwurfTeil.JsonPunkt(p), ["zoom"] = zoom, ["drehung"] = drehung });
        _kamera = new Kartenkamera { Punkt = new[] { p.X, p.Z }, Zoom = zoom, Drehung = drehung };
    }

    /// <summary>Ein fester Ort für einen Rohstoffbetrieb (Mine, Steinbruch, Holzschlag) – die Prüfung verlangt dort einen Weg (Feld <c>vorkommen</c>).</summary>
    public void VorkommenHinzufuegen(string kennung, string betrieb, Kartenort punkt)
    {
        if (_daten["vorkommen"] is not JsonArray liste) FeldSetzen("vorkommen", liste = new JsonArray());
        liste.Add(new JsonObject { ["kennung"] = kennung, ["betrieb"] = betrieb, ["punkt"] = EntwurfTeil.JsonPunkt(Gerundet(punkt)) });
        (_vorkommen, _befunde) = (null, null);
    }

    /// <summary>Setzt ein Feld des Kopfes; ein neues kommt an seine Stelle der <see cref="Reihenfolge"/> (wie <see cref="MitAbschnitten"/>).</summary>
    private void FeldSetzen(string feld, JsonNode? wert)
    {
        if (_daten.ContainsKey(feld))
        {
            _daten[feld] = wert;
            return;
        }
        var liste = _daten.ToList();
        _daten.Clear();
        var davor = Reihenfolge.TakeWhile(k => k != feld).ToHashSet();
        var i = liste.FindLastIndex(e => davor.Contains(e.Key));
        liste.Insert(i < 0 ? liste.Count : i + 1, new(feld, wert));
        foreach (var (k, v) in liste) _daten[k] = v;
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
