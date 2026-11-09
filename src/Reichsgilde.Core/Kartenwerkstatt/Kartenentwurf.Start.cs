using System.Text.Json;
using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Die Startbelegung im Kartenentwurf (Kartenwerkstatt, Schritt 3) und „Karte ausprobieren“
//
//  Wer am Start wo wohnt und arbeitet (Abschnitt „start“ der Karte, siehe Partie/Startbelegung.cs): der Startplatz des Spielers (allgemein
//  und je Beruf), sein Wohnhaus und bis zu acht KI-Häuser mit Name, Volk, Geschlecht, Farbe, Wappen, Wohnhaus und Startbetrieben auf
//  Plätzen der Karte. Im Entwurf ist das ein Teil wie ein Weg (EntwurfStart): geändert nur über Befehle mit Rückgängig, geschrieben mit
//  den Feldern an ihrer alten Stelle. Die Häuser und Betriebe sind unveränderliche Werte – ein Befehl ersetzt sie.
//
//  Ausprobieren: Ohne Fehler wird der Entwurf eine Stadtkarte (ZuStadtkarte), auf der Partieaufbau eine neue Partie aufstellt, mit
//  allen KI-Häusern des Entwurfs und dem gewählten Beruf.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Startbetrieb eines KI-Hauses: Platz der Karte (Werkstattplatz oder Umlandbetrieb), Betriebsart, was er zuerst herstellt.</summary>
public sealed record EntwurfStartbetrieb(string Platz, Betriebsart Art, string? Produkt = null)
{
    /// <summary>Der Eintrag aus der Datei (Reihenfolge und unbekannte Felder beim Schreiben); zählt beim Vergleich nicht.</summary>
    internal JsonObject? Vorlage { get; init; }

    public bool Equals(EntwurfStartbetrieb? o) => o is not null && Platz == o.Platz && Art == o.Art && Produkt == o.Produkt;
    public override int GetHashCode() => HashCode.Combine(Platz, Art, Produkt);

    internal JsonObject AlsJson() => EntwurfTeil.Zusammensetzen(Vorlage,
        ("platz", Platz), ("art", Art.ToString()), ("produkt", Produkt is null ? null : JsonValue.Create(Produkt)));
}

/// <summary>Ein KI-Haus am Start, wie die Kartenwerkstatt es bearbeitet.</summary>
public sealed record EntwurfHaus(string Name, Volk Volk, Geschlecht Geschlecht, Wappen Wappen, string? Farbe, string? Wohnhaus)
{
    /// <summary>Die Startbetriebe (zuerst die Werkstatt).</summary>
    public IReadOnlyList<EntwurfStartbetrieb> Betriebe { get; init; } = Array.Empty<EntwurfStartbetrieb>();

    /// <summary>Der Eintrag aus der Datei; zählt beim Vergleich nicht.</summary>
    internal JsonObject? Vorlage { get; init; }

    /// <summary>Gleiche Werte (die Betriebe Stück für Stück).</summary>
    public bool Equals(EntwurfHaus? o) => o is not null && Name == o.Name && Volk == o.Volk && Geschlecht == o.Geschlecht && Wappen == o.Wappen
        && Farbe == o.Farbe && Wohnhaus == o.Wohnhaus && Betriebe.SequenceEqual(o.Betriebe);

    public override int GetHashCode() => HashCode.Combine(Name, Volk, Geschlecht, Wappen, Farbe, Wohnhaus, Betriebe.Count);

    internal JsonObject AlsJson() => EntwurfTeil.Zusammensetzen(Vorlage,
        ("name", Name), ("volk", Volk.ToString()), ("geschlecht", Geschlecht.ToString()),
        ("wappen", new JsonObject
        {
            ["teilung"] = Wappen.Teilung.ToString(), ["farbe1"] = Wappen.Farbe1.ToString(), ["farbe2"] = Wappen.Farbe2.ToString(), ["figur"] = Wappen.Figur.ToString(),
        }),
        ("farbe", Farbe is null ? null : JsonValue.Create(Farbe)), ("wohnhaus", Wohnhaus is null ? null : JsonValue.Create(Wohnhaus)),
        ("betriebe", new JsonArray(Betriebe.Select(b => (JsonNode)b.AlsJson()).ToArray())));
}

/// <summary>Ein Startplatz des Spielers: Werkstattplatz der Karte und Name der Werkstatt („{art} am Weinmarkt“).</summary>
public sealed record EntwurfStartplatz(string Platz, string Name)
{
    /// <summary>Der Eintrag aus der Datei; zählt beim Vergleich nicht.</summary>
    internal JsonObject? Vorlage { get; init; }

    public bool Equals(EntwurfStartplatz? o) => o is not null && Platz == o.Platz && Name == o.Name;
    public override int GetHashCode() => HashCode.Combine(Platz, Name);

    internal JsonObject AlsJson() => EntwurfTeil.Zusammensetzen(Vorlage, ("platz", Platz), ("name", Name));
}

/// <summary>
/// Eine Hofstelle im Entwurf (seit 09.11.2026, siehe <see cref="Hofstelle"/>): Werkstattplatz und Wohnhausplatz, die zusammengehören, ihr Name und
/// ob sie als Startplatz taugt.
/// </summary>
public sealed record EntwurfHofstelle(string Kennung, string? Name, string Werkstatt, string Wohnhaus, bool Start)
{
    /// <summary>Der Eintrag aus der Datei; zählt beim Vergleich nicht.</summary>
    internal JsonObject? Vorlage { get; init; }

    public bool Equals(EntwurfHofstelle? o) => o is not null && Kennung == o.Kennung && Name == o.Name && Werkstatt == o.Werkstatt && Wohnhaus == o.Wohnhaus && Start == o.Start;
    public override int GetHashCode() => HashCode.Combine(Kennung, Name, Werkstatt, Wohnhaus, Start);

    /// <summary>Gehört dieser Platz (Kennung) zur Hofstelle?</summary>
    public bool Umfasst(string? platz) => platz is not null && (platz == Werkstatt || platz == Wohnhaus);

    internal JsonObject AlsJson() => EntwurfTeil.Zusammensetzen(Vorlage,
        ("kennung", Kennung), ("name", string.IsNullOrWhiteSpace(Name) ? null : JsonValue.Create(Name)), ("werkstatt", Werkstatt), ("wohnhaus", Wohnhaus),
        ("start", Start ? JsonValue.Create(true) : null));
}

/// <summary>Was ein Platz der Karte am Start ist.</summary>
public enum Belegungsart
{
    /// <summary>Wohnhausplatz eines Hauses.</summary>
    Wohnhaus,

    /// <summary>Startbetrieb eines KI-Hauses (Werkstattplatz oder Umlandbetrieb).</summary>
    Betrieb,

    /// <summary>Startplatz der Werkstatt des Spielers (allgemein oder für einen Beruf).</summary>
    Startplatz,
}

/// <summary>
/// Wer einen Platz am Start nutzt: Kennung des Gebäudes, Haus (Index in <see cref="EntwurfStart.Haeuser"/>, −1 = der Spieler), wie, für
/// welchen Beruf (nur Startplätze je Beruf) und welcher Betrieb (Index in <see cref="EntwurfHaus.Betriebe"/>, sonst −1).
/// </summary>
public readonly record struct Platzbelegung(string Kennung, int Haus, Belegungsart Art, string? Beruf = null, int Betrieb = -1)
{
    public bool IstSpieler => Haus < 0;
}

/// <summary>
/// Die Startbelegung im Entwurf (Abschnitt <c>start</c>): der Spieler und die KI-Häuser. Geändert nur über die Befehle des Entwurfs
/// (<see cref="Kartenentwurf.HausHinzufuegen"/> …); das Objekt bleibt dasselbe.
/// </summary>
public sealed class EntwurfStart : EntwurfTeil
{
    public override Kartenteil Teil => Kartenteil.Start;
    public override string Bezeichnung => "Startbelegung";

    public string? SpielerWohnhaus { get; internal set; }
    public string? SpielerFarbe { get; internal set; }

    /// <summary>Die erste Werkstatt des Spielers für jeden Beruf, der unter <see cref="JeBeruf"/> nicht steht (null: fehlt).</summary>
    public EntwurfStartplatz? SpielerWerkstatt { get; internal set; }

    private List<KeyValuePair<string, EntwurfStartplatz>> _jeBeruf = new();
    private List<EntwurfHaus> _haeuser = new();
    private List<EntwurfHofstelle> _hofstellen = new();

    /// <summary>Abweichende Startplätze je Beruf, in der Reihenfolge der Datei.</summary>
    public IReadOnlyList<KeyValuePair<string, EntwurfStartplatz>> JeBeruf => _jeBeruf;

    /// <summary>Die KI-Häuser in Konto-Reihenfolge (das erste spielt immer mit).</summary>
    public IReadOnlyList<EntwurfHaus> Haeuser => _haeuser;

    /// <summary>Die Hofstellen der Karte in der Reihenfolge der Datei (Feld <c>hofstellen</c>; leer: keine).</summary>
    public IReadOnlyList<EntwurfHofstelle> Hofstellen => _hofstellen;

    /// <summary>Die Hofstelle, zu der dieser Platz gehört – oder null.</summary>
    public EntwurfHofstelle? HofstelleVon(string? platz) => _hofstellen.FirstOrDefault(h => h.Umfasst(platz));

    /// <summary>Die Hofstelle mit dieser Kennung – oder null.</summary>
    public EntwurfHofstelle? HofstelleMit(string? kennung) => _hofstellen.FirstOrDefault(h => h.Kennung == kennung);

    /// <summary>Die Felder von <c>spieler</c> aus der Datei (Reihenfolge, unbekannte Felder).</summary>
    private JsonObject? _spielerVorlage;

    /// <summary>Steht nichts darin (dann fehlt der Abschnitt <c>start</c> in der Datei, und das Spiel nimmt den Ersatz)?</summary>
    public bool IstLeer => _haeuser.Count == 0 && SpielerWerkstatt is null && _jeBeruf.Count == 0 && SpielerWohnhaus is null && SpielerFarbe is null
                           && _hofstellen.Count == 0;

    /// <summary>Der Startplatz des Spielers für diesen Beruf (der eigene oder der allgemeine).</summary>
    public EntwurfStartplatz? StartplatzFuer(string beruf) => _jeBeruf.FirstOrDefault(e => e.Key == beruf).Value ?? SpielerWerkstatt;

    internal void HaeuserSetzen(IEnumerable<EntwurfHaus> haeuser) => _haeuser = haeuser.ToList();
    internal void JeBerufSetzen(IEnumerable<KeyValuePair<string, EntwurfStartplatz>> plaetze) => _jeBeruf = plaetze.ToList();
    internal void HofstellenSetzen(IEnumerable<EntwurfHofstelle> hofstellen) => _hofstellen = hofstellen.ToList();

    /// <summary>Die Belegung in den Typen der Partie (wie der Kern sie aus der Datei liest).</summary>
    public Startbelegung AlsStartbelegung() => JsonSerializer.Deserialize<Startbelegung>(AlsJson().ToJsonString(), Stadtkarte.Optionen)!;

    internal static EntwurfStart Aus(JsonObject? vorlage, Startbelegung? s)
    {
        var e = new EntwurfStart { Vorlage = vorlage };
        if (s is null) return e;
        var spieler = vorlage?["spieler"] as JsonObject;
        e._spielerVorlage = spieler?.DeepClone() as JsonObject;
        (e.SpielerWohnhaus, e.SpielerFarbe) = (s.Spieler.Wohnhaus, s.Spieler.Farbe);
        if (spieler?["werkstatt"] is JsonObject w)
            e.SpielerWerkstatt = new EntwurfStartplatz(s.Spieler.Werkstatt.Platz, s.Spieler.Werkstatt.Name) { Vorlage = (JsonObject)w.DeepClone() };
        var jeBeruf = spieler?["jeBeruf"] as JsonObject;
        e._jeBeruf = s.Spieler.JeBeruf.Select(x => new KeyValuePair<string, EntwurfStartplatz>(x.Key,
            new EntwurfStartplatz(x.Value.Platz, x.Value.Name) { Vorlage = jeBeruf?[x.Key]?.DeepClone() as JsonObject })).ToList();
        var haeuser = vorlage?["haeuser"] as JsonArray;
        e._haeuser = s.Haeuser.Select((h, i) =>
        {
            var hv = haeuser is not null && i < haeuser.Count ? haeuser[i] as JsonObject : null;
            var bv = hv?["betriebe"] as JsonArray;
            return new EntwurfHaus(h.Name, h.Volk, h.Geschlecht, h.Wappen, h.Farbe, h.Wohnhaus)
            {
                Vorlage = hv?.DeepClone() as JsonObject,
                Betriebe = h.Betriebe.Select((b, j) => new EntwurfStartbetrieb(b.Platz, b.Art, b.Produkt)
                {
                    Vorlage = bv is not null && j < bv.Count ? bv[j]?.DeepClone() as JsonObject : null,
                }).ToList(),
            };
        }).ToList();
        var hofstellen = vorlage?["hofstellen"] as JsonArray;
        e._hofstellen = (s.Hofstellen ?? new List<Hofstelle>()).Select((h, i) => new EntwurfHofstelle(h.Kennung, h.Name, h.Werkstatt, h.Wohnhaus, h.Start)
        {
            Vorlage = hofstellen is not null && i < hofstellen.Count ? hofstellen[i]?.DeepClone() as JsonObject : null,
        }).ToList();
        return e;
    }

    internal override EntwurfTeil KopieTeil()
    {
        var k = new EntwurfStart
        {
            SpielerWohnhaus = SpielerWohnhaus, SpielerFarbe = SpielerFarbe, SpielerWerkstatt = SpielerWerkstatt, Vorlage = Vorlage, _spielerVorlage = _spielerVorlage,
        };
        (k._jeBeruf, k._haeuser, k._hofstellen) = (_jeBeruf.ToList(), _haeuser.ToList(), _hofstellen.ToList());
        return k;
    }

    internal override void UebernimmTeil(EntwurfTeil werte)
    {
        var o = (EntwurfStart)werte;
        (SpielerWohnhaus, SpielerFarbe, SpielerWerkstatt, Vorlage, _spielerVorlage) = (o.SpielerWohnhaus, o.SpielerFarbe, o.SpielerWerkstatt, o.Vorlage, o._spielerVorlage);
        (_jeBeruf, _haeuser, _hofstellen) = (o._jeBeruf.ToList(), o._haeuser.ToList(), o._hofstellen.ToList());
    }

    internal override bool GleicheWerte(EntwurfTeil anderer) => anderer is EntwurfStart o && AlsJson().ToJsonString() == o.AlsJson().ToJsonString();

    internal override JsonObject AlsJson()
    {
        var jeBeruf = _jeBeruf.Count == 0 ? null : new JsonObject(_jeBeruf.Select(e => new KeyValuePair<string, JsonNode?>(e.Key, e.Value.AlsJson())));
        var spieler = Zusammensetzen(_spielerVorlage,
            ("wohnhaus", SpielerWohnhaus is null ? null : JsonValue.Create(SpielerWohnhaus)), ("farbe", SpielerFarbe is null ? null : JsonValue.Create(SpielerFarbe)),
            ("werkstatt", SpielerWerkstatt?.AlsJson()), ("jeBeruf", jeBeruf));
        var hofstellen = _hofstellen.Count == 0 ? null : new JsonArray(_hofstellen.Select(h => (JsonNode)h.AlsJson()).ToArray());
        return Zusammensetzen(Vorlage, ("hofstellen", hofstellen), ("spieler", spieler), ("haeuser", new JsonArray(_haeuser.Select(h => (JsonNode)h.AlsJson()).ToArray())));
    }
}

public sealed partial class Kartenentwurf
{
    /// <summary>So viele KI-Häuser kann eine Karte haben (mit dem Spieler bis zu neun Häuser).</summary>
    public const int HoechstensHaeuser = 8;

    /// <summary>Namen für neue KI-Häuser (der erste freie).</summary>
    public static readonly IReadOnlyList<string> Hausnamen = new[]
    {
        "Haus Ahlbeck", "Haus Brendel", "Haus Dornbusch", "Haus Falkner", "Haus Grünwald", "Haus Hollerbach", "Haus Lindenau", "Haus Rabenstein",
    };

    /// <summary>Farben für neue KI-Häuser (die erste freie; der Spieler hat in der Greybox #336bb8).</summary>
    public static readonly IReadOnlyList<string> Hausfarben = new[] { "#b33329", "#388c47", "#d9ad2e", "#7a4a9e", "#2e8c8c", "#c26a1f", "#5c5c5c", "#a3476b" };

    /// <summary>Die Startbelegung (immer da; leer, wenn die Datei keinen Abschnitt <c>start</c> hat).</summary>
    public EntwurfStart Start => _start;

    /// <summary>
    /// Die Wirtschaftsdaten (<c>wirtschaft.json</c>) für die Startbelegung: welche Betriebsarten auf einen Werkstattplatz passen, welche Waren und
    /// Berufe es gibt. Ohne Daten prüft die Werkstatt das nicht und kennt keine Werkstätten (<see cref="BetriebsartenFuer"/>).
    /// </summary>
    public Wirtschaftsdaten? Daten
    {
        get => _wirtschaftsdaten;
        set
        {
            _wirtschaftsdaten = value;
            BefundeVerwerfen();
        }
    }

    private Wirtschaftsdaten? _wirtschaftsdaten;

    // ── Abfragen ─────────────────────────────────────────────────────────────

    /// <summary>Wer welchen Platz am Start nutzt (Wohnhäuser, Betriebe, Startplätze), in der Reihenfolge: Spieler, dann die Häuser.</summary>
    public IReadOnlyList<Platzbelegung> Belegung()
    {
        var s = _start;
        var liste = new List<Platzbelegung>();
        if (s.SpielerWohnhaus is { } w) liste.Add(new Platzbelegung(w, -1, Belegungsart.Wohnhaus));
        if (s.SpielerWerkstatt is { } sw) liste.Add(new Platzbelegung(sw.Platz, -1, Belegungsart.Startplatz));
        foreach (var (beruf, p) in s.JeBeruf) liste.Add(new Platzbelegung(p.Platz, -1, Belegungsart.Startplatz, beruf));
        for (var i = 0; i < s.Haeuser.Count; i++)
        {
            if (s.Haeuser[i].Wohnhaus is { } hw) liste.Add(new Platzbelegung(hw, i, Belegungsart.Wohnhaus));
            for (var j = 0; j < s.Haeuser[i].Betriebe.Count; j++) liste.Add(new Platzbelegung(s.Haeuser[i].Betriebe[j].Platz, i, Belegungsart.Betrieb, Betrieb: j));
        }
        return liste;
    }

    /// <summary>Wer diesen Platz am Start nutzt (leer: niemand; mehrere: doppelt vergeben, ein Fehler).</summary>
    public IReadOnlyList<Platzbelegung> BelegungVon(string kennung) => Belegung().Where(b => b.Kennung == kennung).ToList();

    /// <summary>Die Wohnhausplätze, die am Start niemand bewohnt (Reihenfolge der Datei).</summary>
    public IReadOnlyList<EntwurfGebaeude> FreieWohnhausplaetze()
    {
        var belegt = Belegung().Select(b => b.Kennung).ToHashSet();
        return _gebaeude.Where(g => g.Art == Bausteinart.Wohnhausplatz && !belegt.Contains(g.Kennung)).ToList();
    }

    /// <summary>Die Werkstattplätze und Umlandbetriebe, die am Start niemand nutzt (Reihenfolge der Datei).</summary>
    public IReadOnlyList<EntwurfGebaeude> FreieBetriebsplaetze()
    {
        var belegt = Belegung().Select(b => b.Kennung).ToHashSet();
        return _gebaeude.Where(g => g.Art is Bausteinart.Werkstattplatz or Bausteinart.Umlandbetrieb && !belegt.Contains(g.Kennung)).ToList();
    }

    /// <summary>
    /// Welche Betriebsarten auf diesem Platz am Start stehen dürfen: auf einem Werkstattplatz jede Werkstatt der Stufe 1 in der Stadt (aus
    /// <see cref="Daten"/>; ohne Daten keine), auf einem Umlandbetrieb seine Art. Sonst leer.
    /// </summary>
    public IReadOnlyList<Betriebsart> BetriebsartenFuer(EntwurfGebaeude platz) => platz.Art switch
    {
        Bausteinart.Werkstattplatz => Daten is { } d ? d.Betriebsarten.Where(x => x.Value.InDerStadt && x.Value.Stufe == 1).Select(x => x.Key).ToList() : Array.Empty<Betriebsart>(),
        Bausteinart.Umlandbetrieb when platz.Baustein?.BetriebsartWert is { } art => new[] { art },
        _ => Array.Empty<Betriebsart>(),
    };

    // ── Befehle: KI-Häuser ───────────────────────────────────────────────────

    /// <summary>
    /// Fügt ein KI-Haus hinten an (bis <see cref="HoechstensHaeuser"/>): erster freier Name aus <see cref="Hausnamen"/> (oder
    /// <paramref name="name"/>), Mensch, Mann, ein freies Wappen und eine freie Farbe, das Wohnhaus der ersten freien Start-Hofstelle (ohne sie der
    /// erste freie Wohnhausplatz), noch ohne Betrieb. Gibt den Index zurück; −1 (kein Schritt), wenn schon acht Häuser da sind.
    /// </summary>
    public int HausHinzufuegen(string? name = null)
    {
        var s = _start;
        if (s.Haeuser.Count >= HoechstensHaeuser) return -1;
        var namen = s.Haeuser.Select(h => h.Name).ToHashSet();
        name = !string.IsNullOrWhiteSpace(name) ? name : Hausnamen.FirstOrDefault(n => !namen.Contains(n)) ?? FreierName("Haus Neuburg", namen);
        var wappen = s.Haeuser.Select(h => h.Wappen).ToHashSet();
        var neu = new EntwurfHaus(name, Volk.Mensch, Geschlecht.Mann,
            Wappen.Vorlagen.Skip(1).FirstOrDefault(w => !wappen.Contains(w)) ?? Wappen.AusName(name),
            Hausfarben.FirstOrDefault(f => f != s.SpielerFarbe && s.Haeuser.All(h => h.Farbe != f)),
            FreieHofstellen().FirstOrDefault(h => h.Start)?.Wohnhaus ?? FreieWohnhausplaetze().FirstOrDefault()?.Kennung);
        Aendern(s, $"{name} hinzugefügt", n => n.HaeuserSetzen(n.Haeuser.Append(neu)));
        return s.Haeuser.Count - 1;
    }

    /// <summary>Entfernt ein KI-Haus (die Häuser dahinter rücken auf; ihre Betriebe und Wohnhäuser werden frei).</summary>
    public void HausEntfernen(int haus)
    {
        PruefeHaus(haus);
        Aendern(_start, $"{_start.Haeuser[haus].Name} entfernt", n => n.HaeuserSetzen(n.Haeuser.Where((_, i) => i != haus)));
    }

    public void HausUmbenennen(int haus, string name) => HausAendern(haus, "umbenannt", h => h with { Name = name });
    public void HausVolkSetzen(int haus, Volk volk) => HausAendern(haus, "Volk geändert", h => h with { Volk = volk });
    public void HausGeschlechtSetzen(int haus, Geschlecht geschlecht) => HausAendern(haus, "Geschlecht geändert", h => h with { Geschlecht = geschlecht });

    /// <summary>Farbe des Hauses in der Stadt (Fahnen, Dächer) als „#rrggbb“; null entfernt sie.</summary>
    public void HausFarbeSetzen(int haus, string? farbe) => HausAendern(haus, "Farbe geändert", h => h with { Farbe = string.IsNullOrWhiteSpace(farbe) ? null : farbe });

    /// <summary>Das Wappen aus den Teilen von <see cref="Wappen"/> (Teilung, zwei Tinkturen, Figur).</summary>
    public void HausWappenSetzen(int haus, Wappen wappen) => HausAendern(haus, "Wappen geändert", h => h with { Wappen = wappen });

    /// <summary>Weist dem Haus einen Wohnhausplatz zu (Kennung; null: keiner).</summary>
    public void HausWohnhausSetzen(int haus, string? wohnhaus) => HausAendern(haus, "Wohnhaus gewählt", h => h with { Wohnhaus = wohnhaus });

    /// <summary>
    /// Gibt dem Haus einen Startbetrieb auf einem Platz der Karte (Kennung eines Werkstattplatzes oder Umlandbetriebs) mit Betriebsart und
    /// erster Ware. Hat der Platz noch keinen Betriebsnamen, bekommt er im selben Schritt einen („Gießerei Eisenbart“, aus dem Namen der Art in
    /// <see cref="Daten"/> und dem Hausnamen; frei in der Karte). Gibt den Index des Betriebs im Haus zurück.
    /// </summary>
    /// <exception cref="ArgumentException">Wenn es den Platz nicht gibt.</exception>
    public int BetriebHinzufuegen(int haus, string platz, Betriebsart art, string? produkt = null)
    {
        PruefeHaus(haus);
        var g = GebaeudeMit(platz) ?? throw new ArgumentException($"Kein Gebäude „{platz}“.", nameof(platz));
        var h = _start.Haeuser[haus];
        var befehle = new List<IKartenbefehl>();
        if (Aenderung(_start, n => n.HaeuserSetzen(n.Haeuser.Select((x, i) => i == haus ? x with { Betriebe = x.Betriebe.Append(new EntwurfStartbetrieb(platz, art, produkt)).ToList() } : x)))
            is { } eigener)
            befehle.Add(eigener);
        if (string.IsNullOrWhiteSpace(g.Betrieb) && g.Art is Bausteinart.Werkstattplatz or Bausteinart.Umlandbetrieb)
        {
            var artName = Daten is { } d && d.KenntArt(art) ? d.Art(art).Name : art.ToString();
            var name = FreierName($"{artName} {Familie.NachnameAus(h.Name)}", _gebaeude.Select(x => x.Betrieb).OfType<string>());
            if (Aenderung(g, x => x.Betrieb = name) is { } benennen) befehle.Add(benennen);
        }
        Buendeln($"{h.Name}: Betrieb hinzugefügt", befehle.FirstOrDefault(), befehle.Skip(1));
        return _start.Haeuser[haus].Betriebe.Count - 1;
    }

    /// <summary>Nimmt dem Haus einen Startbetrieb (der Betriebsname am Platz bleibt stehen).</summary>
    public void BetriebEntfernen(int haus, int betrieb)
    {
        PruefeHaus(haus);
        Pruefen(_start, betrieb, _start.Haeuser[haus].Betriebe.Count);
        HausAendern(haus, "Betrieb entfernt", h => h with { Betriebe = h.Betriebe.Where((_, i) => i != betrieb).ToList() });
    }

    /// <summary>Was der Startbetrieb zuerst herstellt (null: die Voreinstellung der Betriebsart).</summary>
    public void BetriebProduktSetzen(int haus, int betrieb, string? produkt)
    {
        PruefeHaus(haus);
        Pruefen(_start, betrieb, _start.Haeuser[haus].Betriebe.Count);
        HausAendern(haus, "Ware geändert", h => h with
        {
            Betriebe = h.Betriebe.Select((b, i) => i == betrieb ? b with { Produkt = string.IsNullOrWhiteSpace(produkt) ? null : produkt } : b).ToList(),
        });
    }

    // ── Befehle: Spieler ─────────────────────────────────────────────────────

    /// <summary>Der Wohnhausplatz des Spielers (Kennung; null: keiner).</summary>
    public void SpielerWohnhausSetzen(string? wohnhaus) => Aendern(_start, "Wohnhaus des Spielers gewählt", n => n.SpielerWohnhaus = wohnhaus);

    /// <summary>Die Farbe des Spielerhauses als „#rrggbb“ (null: keine).</summary>
    public void SpielerFarbeSetzen(string? farbe) =>
        Aendern(_start, "Farbe des Spielers geändert", n => n.SpielerFarbe = string.IsNullOrWhiteSpace(farbe) ? null : farbe);

    /// <summary>
    /// Der Startplatz der Werkstatt des Spielers (Kennung eines Werkstattplatzes): allgemein (<paramref name="beruf"/> null) oder nur für einen
    /// Beruf. Name der Werkstatt: <paramref name="name"/>, sonst der bisherige, sonst „{art} am &lt;Name des Platzes&gt;“.
    /// </summary>
    /// <exception cref="ArgumentException">Wenn es den Platz nicht gibt.</exception>
    public void SpielerStartplatzSetzen(string platz, string? beruf = null, string? name = null)
    {
        var g = GebaeudeMit(platz) ?? throw new ArgumentException($"Kein Gebäude „{platz}“.", nameof(platz));
        var alt = beruf is null ? _start.SpielerWerkstatt : _start.JeBeruf.FirstOrDefault(e => e.Key == beruf).Value;
        var neu = (alt ?? new EntwurfStartplatz("", "")) with { Platz = platz, Name = !string.IsNullOrWhiteSpace(name) ? name : alt?.Name ?? $"{{art}} am {g.Name}" };
        Aendern(_start, beruf is null ? "Startplatz des Spielers gewählt" : $"Startplatz ({beruf}) gewählt", n =>
        {
            if (beruf is null) n.SpielerWerkstatt = neu;
            else if (n.JeBeruf.Any(e => e.Key == beruf)) n.JeBerufSetzen(n.JeBeruf.Select(e => e.Key == beruf ? new KeyValuePair<string, EntwurfStartplatz>(beruf, neu) : e));
            else n.JeBerufSetzen(n.JeBeruf.Append(new(beruf, neu)));
        });
    }

    /// <summary>Entfernt den Startplatz (allgemein: <paramref name="beruf"/> null – dann fehlt er, ein Fehler; sonst gilt für den Beruf wieder der allgemeine).</summary>
    public void SpielerStartplatzEntfernen(string? beruf = null) =>
        Aendern(_start, beruf is null ? "Startplatz des Spielers entfernt" : $"Startplatz ({beruf}) entfernt", n =>
        {
            if (beruf is null) n.SpielerWerkstatt = null;
            else n.JeBerufSetzen(n.JeBeruf.Where(e => e.Key != beruf));
        });

    // ── Befehle: Hofstellen (seit 09.11.2026) ───────────────────────────────

    /// <summary>Die Hofstellen, die die Startbelegung niemandem gibt (Reihenfolge der Datei) – zur Wahl für Spieler und Häuser.</summary>
    public IReadOnlyList<EntwurfHofstelle> FreieHofstellen()
    {
        var belegt = Belegung().Select(b => b.Kennung).ToHashSet();
        return _start.Hofstellen.Where(h => !belegt.Contains(h.Werkstatt) && !belegt.Contains(h.Wohnhaus)).ToList();
    }

    /// <summary>
    /// Wo <see cref="HofstelleSetzen"/> die beiden Plätze einer Hofstelle mit der Mitte (x, z) hinsetzt: die Mitten von Werkstatt- und Wohnhausplatz
    /// (noch nicht auf 5 cm gerundet) und die Drehung (auf 0,1°) – für die Vorschau des Werkzeugs, dieselbe Rechnung.
    /// </summary>
    public (Kartenort Werkstatt, Kartenort Wohnhaus, double Drehung) HofstelleLagen(double x, double z, double drehung = 0)
    {
        var (ww, wt) = Katalog.Finde("werkstattplatz")?.Groesse is { Length: 2 } a ? (a[0], a[1]) : (16, 14);
        var (hw, ht) = Katalog.Finde("wohnhausplatz")?.Groesse is { Length: 2 } b ? (b[0], b[1]) : (8, 10);
        var d = Kartenraster.Drehung(drehung);
        var w = d * Math.PI / 180;
        var (c, s) = (DetMathe.Cos(w), DetMathe.Sin(w));
        Kartenort Ort(double lx, double lz) => new(x + lx * c + lz * s, z - lx * s + lz * c);
        var breite = ww + hw;
        return (Ort(-breite / 2 + ww / 2, 0), Ort(breite / 2 - hw / 2, (wt - ht) / 2), d);
    }

    /// <summary>
    /// Setzt eine neue Hofstelle (Werkzeug „Hofstelle“): Werkstattplatz und Wohnhausplatz nebeneinander, vorn bündig, die Werkstatt links (von vorn
    /// gesehen), beide in der Drehung <paramref name="drehung"/>; (x, z) ist die Mitte des Ganzen (16 + 8 = 24 m breit, 14 m tief). Kennung „H1“,
    /// „H2“ …, die Plätze „H1_Werkstatt“ und „H1_Wohnhaus“. Ein Schritt (Rückgängig nimmt alles zurück).
    /// </summary>
    public EntwurfHofstelle HofstelleSetzen(double x, double z, double drehung = 0, bool start = false)
    {
        var bw = Katalog.Finde("werkstattplatz") ?? throw new InvalidOperationException("Der Katalog kennt keinen Werkstattplatz.");
        var bh = Katalog.Finde("wohnhausplatz") ?? throw new InvalidOperationException("Der Katalog kennt keinen Wohnhausplatz.");
        var n = 1;
        while (_start.HofstelleMit($"H{n}") is not null || GebaeudeMit($"H{n}_Werkstatt") is not null || GebaeudeMit($"H{n}_Wohnhaus") is not null) n++;
        var kennung = $"H{n}";
        var (pw, ph, d) = HofstelleLagen(x, z, drehung);
        var werkstatt = new EntwurfGebaeude
        {
            Kennung = $"{kennung}_Werkstatt", Katalog = "werkstattplatz", Name = $"Hofstelle {kennung}", Baustein = bw,
            X = Kartenraster.Punkt(pw.X), Z = Kartenraster.Punkt(pw.Z), Drehung = d,
        };
        var wohnhaus = new EntwurfGebaeude
        {
            Kennung = $"{kennung}_Wohnhaus", Katalog = "wohnhausplatz", Name = $"Hofstelle {kennung}", Baustein = bh,
            X = Kartenraster.Punkt(ph.X), Z = Kartenraster.Punkt(ph.Z), Drehung = d,
        };
        var hof = new EntwurfHofstelle(kennung, null, werkstatt.Kennung, wohnhaus.Kennung, start);
        var befehle = new List<IKartenbefehl> { new TeilSetzen(werkstatt, _gebaeude.Count), new TeilSetzen(wohnhaus, _gebaeude.Count + 1) };
        if (Aenderung(_start, t => t.HofstellenSetzen(t.Hofstellen.Append(hof))) is { } eintrag) befehle.Add(eintrag);
        Geschichte.Ausfuehren(new Sammelbefehl($"Hofstelle {kennung} gesetzt", befehle));
        return hof;
    }

    /// <summary>
    /// Macht aus einem Werkstattplatz und einem Wohnhausplatz, die schon in der Karte stehen, eine Hofstelle (Kartenskripte, alte Karten).
    /// Kennung: <paramref name="kennung"/>, sonst „H1“, „H2“ … Gibt die Kennung zurück.
    /// </summary>
    /// <exception cref="ArgumentException">Wenn ein Platz fehlt, die falsche Art hat, schon zu einer Hofstelle gehört oder die Kennung vergeben ist.</exception>
    public string HofstelleBilden(string werkstatt, string wohnhaus, bool start = false, string? name = null, string? kennung = null)
    {
        if (GebaeudeMit(werkstatt) is not { Art: Bausteinart.Werkstattplatz }) throw new ArgumentException($"„{werkstatt}“ ist kein Werkstattplatz.", nameof(werkstatt));
        if (GebaeudeMit(wohnhaus) is not { Art: Bausteinart.Wohnhausplatz }) throw new ArgumentException($"„{wohnhaus}“ ist kein Wohnhausplatz.", nameof(wohnhaus));
        if (_start.HofstelleVon(werkstatt) is { } hw) throw new ArgumentException($"„{werkstatt}“ gehört schon zur Hofstelle {hw.Kennung}.", nameof(werkstatt));
        if (_start.HofstelleVon(wohnhaus) is { } hh) throw new ArgumentException($"„{wohnhaus}“ gehört schon zur Hofstelle {hh.Kennung}.", nameof(wohnhaus));
        if (kennung is null)
        {
            var n = 1;
            while (_start.HofstelleMit($"H{n}") is not null) n++;
            kennung = $"H{n}";
        }
        else if (_start.HofstelleMit(kennung) is not null) throw new ArgumentException($"Die Hofstelle „{kennung}“ gibt es schon.", nameof(kennung));
        var hof = new EntwurfHofstelle(kennung, string.IsNullOrWhiteSpace(name) ? null : name, werkstatt, wohnhaus, start);
        Aendern(_start, $"Hofstelle {kennung} gebildet", t => t.HofstellenSetzen(t.Hofstellen.Append(hof)));
        return kennung;
    }

    /// <summary>Taugt die Hofstelle als Startplatz (Fairness ±10 %, nur unter diesen wird gelost)?</summary>
    public void HofstelleStartSetzen(string kennung, bool start) => HofstelleAendern(kennung, start ? "als Startplatz" : "nicht mehr Startplatz", h => h with { Start = start });

    /// <summary>Name der Hofstelle (Lage für den Namen der Spielerwerkstatt; null oder leer: die nächste Karrenstraße).</summary>
    public void HofstelleUmbenennen(string kennung, string? name) =>
        HofstelleAendern(kennung, "umbenannt", h => h with { Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim() });

    /// <summary>Löst die Hofstelle auf – die beiden Plätze bleiben als einzelne Plätze stehen.</summary>
    public void HofstelleAufloesen(string kennung)
    {
        PruefeHofstelle(kennung);
        Aendern(_start, $"Hofstelle {kennung} aufgelöst", t => t.HofstellenSetzen(t.Hofstellen.Where(h => h.Kennung != kennung)));
    }

    /// <summary>Löscht die Hofstelle mit beiden Plätzen (ein Schritt). Wer sie in der Startbelegung hatte, hat danach keinen Platz – das meldet die Prüfung.</summary>
    public void HofstelleLoeschen(string kennung)
    {
        var h = PruefeHofstelle(kennung);
        var befehle = new List<IKartenbefehl>();
        if (Aenderung(_start, t => t.HofstellenSetzen(t.Hofstellen.Where(x => x.Kennung != kennung))) is { } eintrag) befehle.Add(eintrag);
        // erst den hinteren Platz, damit Rückgängig beide an ihre alte Stelle der Reihenfolge setzt
        foreach (var g in new[] { GebaeudeMit(h.Werkstatt), GebaeudeMit(h.Wohnhaus) }.OfType<EntwurfGebaeude>().OrderByDescending(g => _gebaeude.IndexOf(g)))
            befehle.Add(new TeilLoeschen(g, _gebaeude.IndexOf(g)));
        Geschichte.Ausfuehren(new Sammelbefehl($"Hofstelle {kennung} gelöscht", befehle));
    }

    /// <summary>
    /// Setzt den Spieler (ohne Los) auf diese Hofstelle: sein Wohnhaus und den Startplatz seiner Werkstatt in einem Schritt. Der Name der Werkstatt
    /// bleibt; ohne bisherigen Namen „{art} am &lt;Name der Hofstelle&gt;“.
    /// </summary>
    public void SpielerHofstelleSetzen(string kennung)
    {
        var h = PruefeHofstelle(kennung);
        var alt = _start.SpielerWerkstatt;
        var lage = new[] { h.Name, GebaeudeMit(h.Werkstatt)?.Name, h.Kennung }.First(x => !string.IsNullOrWhiteSpace(x))!;
        var neu = (alt ?? new EntwurfStartplatz("", "")) with
        {
            Platz = h.Werkstatt,
            Name = alt?.Name is { Length: > 0 } n ? n : "{art} " + (Hofstellenvergabe.MitPraeposition(lage) ? lage : Bauwesen.Lagebezeichnung(lage)),
        };
        Aendern(_start, $"Spieler auf Hofstelle {kennung}", t => (t.SpielerWohnhaus, t.SpielerWerkstatt) = (h.Wohnhaus, neu));
    }

    /// <summary>
    /// Setzt ein KI-Haus (ohne Los) auf diese Hofstelle: sein Wohnhaus und seine Werkstatt (der erste Startbetrieb auf einem Werkstattplatz) in
    /// einem Schritt. Der Name der Werkstatt zieht mit an den neuen Platz (ohne Namen: „{art} &lt;Nachname&gt;“). Hat das Haus keine Werkstatt,
    /// bekommt es nur das Wohnhaus.
    /// </summary>
    public void HausHofstelleSetzen(int haus, string kennung)
    {
        PruefeHaus(haus);
        var h = PruefeHofstelle(kennung);
        var alt = _start.Haeuser[haus];
        var befehle = new List<IKartenbefehl>();
        var j = alt.Betriebe.ToList().FindIndex(b => GebaeudeMit(b.Platz) is { Art: Bausteinart.Werkstattplatz });
        if (j >= 0 && alt.Betriebe[j].Platz != h.Werkstatt)
        {
            var b = alt.Betriebe[j];
            var vorher = GebaeudeMit(b.Platz)!;
            var artName = Daten is { } d && d.KenntArt(b.Art) ? d.Art(b.Art).Name : b.Art.ToString();
            var name = vorher.Betrieb ?? FreierName($"{artName} {Familie.NachnameAus(alt.Name)}", _gebaeude.Select(x => x.Betrieb).OfType<string>());
            // Der alte Platz behält den Namen nur, wenn ihn noch ein anderes Haus als Startplatz hat
            var andere = _start.Haeuser.Where((x, i) => i != haus).Any(x => x.Betriebe.Any(y => y.Platz == vorher.Kennung));
            if (!andere && Aenderung(vorher, x => x.Betrieb = null) is { } frei) befehle.Add(frei);
            if (GebaeudeMit(h.Werkstatt) is { } ziel && Aenderung(ziel, x => x.Betrieb = name) is { } benennen) befehle.Add(benennen);
        }
        if (Aenderung(_start, t => t.HaeuserSetzen(t.Haeuser.Select((x, i) => i != haus ? x : x with
            {
                Wohnhaus = h.Wohnhaus,
                Betriebe = x.Betriebe.Select((b, k) => k == j ? b with { Platz = h.Werkstatt } : b).ToList(),
            }))) is { } eigener)
            befehle.Insert(0, eigener);
        Buendeln($"{alt.Name} auf Hofstelle {kennung}", befehle.FirstOrDefault(), befehle.Skip(1));
    }

    private void HofstelleAendern(string kennung, string was, Func<EntwurfHofstelle, EntwurfHofstelle> aenderung)
    {
        PruefeHofstelle(kennung);
        Aendern(_start, $"Hofstelle {kennung} {was}", t => t.HofstellenSetzen(t.Hofstellen.Select(h => h.Kennung == kennung ? aenderung(h) : h)));
    }

    private EntwurfHofstelle PruefeHofstelle(string kennung) =>
        _start.HofstelleMit(kennung) ?? throw new ArgumentException($"Keine Hofstelle „{kennung}“.", nameof(kennung));

    private void HausAendern(int haus, string was, Func<EntwurfHaus, EntwurfHaus> aenderung)
    {
        PruefeHaus(haus);
        var name = _start.Haeuser[haus].Name;
        if (Aenderung(_start, n => n.HaeuserSetzen(n.Haeuser.Select((h, i) => i == haus ? aenderung(h) : h))) is { } befehl)
            Geschichte.Ausfuehren(befehl.Benannt($"{name} {was}"));
    }

    private void PruefeHaus(int haus)
    {
        if (haus < 0 || haus >= _start.Haeuser.Count) throw new ArgumentOutOfRangeException(nameof(haus), haus, $"zwischen 0 und {_start.Haeuser.Count - 1}");
    }

    // ── Karte ausprobieren ───────────────────────────────────────────────────

    /// <summary>
    /// Die Einstellungen einer Probepartie: der gewählte Beruf, alle KI-Häuser der Karte (bei leerer Startbelegung die drei des Ersatzes),
    /// ein Hausname und ein Wappen, die kein KI-Haus führt.
    /// </summary>
    public NeuePartie Probepartie(string beruf = "Schmied")
    {
        var start = _start.IstLeer ? Startbelegung.Ersatz : _start.AlsStartbelegung();
        var namen = start.Rivalennamen.ToHashSet();
        var familie = new[] { "Hammerschmid", "Probst", "Kartenmacher", "Feldmesser" }.FirstOrDefault(n => !namen.Contains(Partieaufbau.Hausname(n))) ?? "Neubürger";
        var wappen = Wappen.Vorlagen.FirstOrDefault(w => !start.Rivalenwappen.Contains(w)) ?? Wappen.AusName(familie);
        // Mit Hofstellen ohne Los: jedes Haus, wo die Startbelegung es hinsetzt – so, wie die Karte eingestellt ist (im Spiel wird gelost)
        return new NeuePartie(Familienname: familie, Beruf: beruf, Rivalen: Math.Max(1, start.Haeuser.Count), Wappen: wappen,
            Startplaetze: Startplatzvergabe.Vorgabe);
    }

    /// <summary>
    /// „Karte ausprobieren“: stellt eine neue Partie auf genau diesem Entwurf auf (ohne Datei), mit dem gewählten Beruf und allen KI-Häusern
    /// (<see cref="Probepartie"/>). Nur ohne Fehler; fehlen dem Entwurf noch <see cref="Daten"/>, prüft er mit <paramref name="daten"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Wenn der Entwurf Fehler hat (<see cref="HatFehler"/>).</exception>
    /// <exception cref="KartenFehler">Wenn der Leser des Kerns die Karte ablehnt (sollte ohne Fehler nicht vorkommen).</exception>
    /// <exception cref="ArgumentException">Wenn es den Beruf in den Daten nicht gibt.</exception>
    public (Simulation Simulation, Konto Spieler) Ausprobieren(Wirtschaftsdaten daten, string beruf = "Schmied", Partieeinstellungen? einstellungen = null,
        Spielzeit? startzeit = null)
    {
        Daten ??= daten;
        if (HatFehler) throw new InvalidOperationException("Die Karte hat Fehler – erst beheben, dann ausprobieren.");
        return Partieaufbau.Erstellen(einstellungen ?? Partieeinstellungen.Standard, daten, Probepartie(beruf), startzeit, ZuStadtkarte());
    }
}
