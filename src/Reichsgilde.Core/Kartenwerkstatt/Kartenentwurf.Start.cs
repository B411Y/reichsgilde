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
//  Wer am Start mitspielt (Abschnitt „start“ der Karte, siehe Partie/Startbelegung.cs): die Farbe des Spielers und bis zu acht KI-Häuser
//  mit Name, Volk, Geschlecht, Farbe, Wappen und Startbetrieben – die Werkstatt in der Stadt ohne Platz (wo ein Haus wohnt und arbeitet,
//  lost die Partie in den Bauzonen; seit 16.11.2026 gibt es keine Werkstatt- und Wohnhausplätze und keine Hofstellen mehr), die
//  Umlandbetriebe auf ihren Plätzen der Karte. Im Entwurf ist das ein Teil wie ein Weg (EntwurfStart): geändert nur über Befehle mit
//  Rückgängig, geschrieben mit den Feldern an ihrer alten Stelle. Die Häuser und Betriebe sind unveränderliche Werte – ein Befehl ersetzt sie.
//
//  Ausprobieren: Ohne Fehler wird der Entwurf eine Stadtkarte (ZuStadtkarte), auf der Partieaufbau eine neue Partie aufstellt, mit
//  allen KI-Häusern des Entwurfs und dem gewählten Beruf.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Startbetrieb eines KI-Hauses: Platz der Karte (Umlandbetrieb; „“ = die Werkstatt in der Stadt, sie hat keinen Platz), Betriebsart, was er zuerst herstellt.</summary>
public sealed record EntwurfStartbetrieb(string Platz, Betriebsart Art, string? Produkt = null)
{
    /// <summary>Der Eintrag aus der Datei (Reihenfolge und unbekannte Felder beim Schreiben); zählt beim Vergleich nicht.</summary>
    internal JsonObject? Vorlage { get; init; }

    public bool Equals(EntwurfStartbetrieb? o) => o is not null && Platz == o.Platz && Art == o.Art && Produkt == o.Produkt;
    public override int GetHashCode() => HashCode.Combine(Platz, Art, Produkt);

    internal JsonObject AlsJson() => EntwurfTeil.Zusammensetzen(Vorlage,
        ("platz", Platz == "" ? null : JsonValue.Create(Platz)), ("art", Art.ToString()), ("produkt", Produkt is null ? null : JsonValue.Create(Produkt)));
}

/// <summary>Ein KI-Haus am Start, wie die Kartenwerkstatt es bearbeitet.</summary>
public sealed record EntwurfHaus(string Name, Volk Volk, Geschlecht Geschlecht, Wappen Wappen, string? Farbe)
{
    /// <summary>Die Startbetriebe (zuerst die Werkstatt).</summary>
    public IReadOnlyList<EntwurfStartbetrieb> Betriebe { get; init; } = Array.Empty<EntwurfStartbetrieb>();

    /// <summary>Der Eintrag aus der Datei; zählt beim Vergleich nicht.</summary>
    internal JsonObject? Vorlage { get; init; }

    /// <summary>Gleiche Werte (die Betriebe Stück für Stück).</summary>
    public bool Equals(EntwurfHaus? o) => o is not null && Name == o.Name && Volk == o.Volk && Geschlecht == o.Geschlecht && Wappen == o.Wappen
        && Farbe == o.Farbe && Betriebe.SequenceEqual(o.Betriebe);

    public override int GetHashCode() => HashCode.Combine(Name, Volk, Geschlecht, Wappen, Farbe, Betriebe.Count);

    internal JsonObject AlsJson() => EntwurfTeil.Zusammensetzen(Vorlage,
        ("name", Name), ("volk", Volk.ToString()), ("geschlecht", Geschlecht.ToString()),
        ("wappen", new JsonObject
        {
            ["teilung"] = Wappen.Teilung.ToString(), ["farbe1"] = Wappen.Farbe1.ToString(), ["farbe2"] = Wappen.Farbe2.ToString(), ["figur"] = Wappen.Figur.ToString(),
        }),
        ("farbe", Farbe is null ? null : JsonValue.Create(Farbe)), ("wohnhaus", null), // „wohnhaus“: Feld alter Karten, fällt beim Schreiben weg
        ("betriebe", new JsonArray(Betriebe.Select(b => (JsonNode)b.AlsJson()).ToArray())));
}

/// <summary>
/// Welches Haus einen Umlandbetrieb der Karte am Start führt: Kennung des Gebäudes, Haus (Index in <see cref="EntwurfStart.Haeuser"/>) und
/// Betrieb (Index in <see cref="EntwurfHaus.Betriebe"/>).
/// </summary>
public readonly record struct Platzbelegung(string Kennung, int Haus, int Betrieb);

/// <summary>
/// Die Startbelegung im Entwurf (Abschnitt <c>start</c>): die Farbe des Spielers und die KI-Häuser. Geändert nur über die Befehle des Entwurfs
/// (<see cref="Kartenentwurf.HausHinzufuegen"/> …); das Objekt bleibt dasselbe.
/// </summary>
public sealed class EntwurfStart : EntwurfTeil
{
    public override Kartenteil Teil => Kartenteil.Start;
    public override string Bezeichnung => "Startbelegung";

    /// <summary>Farbe des Spielerhauses („#rrggbb“) – nur noch aus älteren Karten gelesen und so wieder geschrieben; sonst null (die Farbe kommt aus dem Wappen, 17.11.2026).</summary>
    public string? SpielerFarbe { get; internal set; }

    private List<EntwurfHaus> _haeuser = new();

    /// <summary>Die KI-Häuser in Konto-Reihenfolge.</summary>
    public IReadOnlyList<EntwurfHaus> Haeuser => _haeuser;

    /// <summary>Der Eintrag „spieler“ aus der Datei (unbekannte Felder beim Schreiben).</summary>
    private JsonObject? _spielerVorlage;

    /// <summary>
    /// Gibt es den Abschnitt „start“ – aus der Datei oder weil hier schon ein KI-Haus angelegt wurde? Dann bleibt er auch ohne KI-Haus stehen
    /// (die Partie würfelt ihre Häuser). Bis zum 17.11.2026 hielt ihn die Farbe des Spielers; die steht nicht mehr in der Karte.
    /// </summary>
    public bool Angelegt { get; private set; }

    /// <summary>Steht nichts drin? Dann schreibt der Entwurf keinen Abschnitt „start“, und es gilt <see cref="Startbelegung.Ersatz"/>.</summary>
    public bool IstLeer => !Angelegt && _haeuser.Count == 0 && SpielerFarbe is null;

    internal void HaeuserSetzen(IEnumerable<EntwurfHaus> haeuser) => (_haeuser, Angelegt) = (haeuser.ToList(), true);

    /// <summary>Die Belegung, wie das Spiel sie liest.</summary>
    public Startbelegung AlsStartbelegung() => JsonSerializer.Deserialize<Startbelegung>(AlsJson().ToJsonString(), Stadtkarte.Optionen)!;

    /// <summary>
    /// Aus dem Abschnitt der Datei. Eine Karte von vor „nur noch Bauzonen“ (bis 16.11.2026) verliert dabei still, was auf ihre Werkstatt- und
    /// Wohnhausplätze zeigt (<paramref name="stadtplaetze"/>: deren Kennungen) – Wohnhaus, Startplatz des Spielers, Hofstellen, Platz der
    /// Werkstatt in der Stadt; wie <see cref="Stadtkarte.AusJson"/>.
    /// </summary>
    internal static EntwurfStart Aus(JsonObject? vorlage, Startbelegung? s, IReadOnlyCollection<string> stadtplaetze)
    {
        var e = new EntwurfStart { Vorlage = vorlage };
        if (s is null) return e;
        e.Angelegt = true;
        e._spielerVorlage = (vorlage?["spieler"] as JsonObject)?.DeepClone() as JsonObject;
        e.SpielerFarbe = s.Spieler.Farbe;
        var haeuser = vorlage?["haeuser"] as JsonArray;
        e._haeuser = s.Haeuser.Select((h, i) =>
        {
            var hv = haeuser is not null && i < haeuser.Count ? haeuser[i] as JsonObject : null;
            var bv = hv?["betriebe"] as JsonArray;
            return new EntwurfHaus(h.Name, h.Volk, h.Geschlecht, h.Wappen, h.Farbe)
            {
                Vorlage = hv?.DeepClone() as JsonObject,
                Betriebe = h.Betriebe.Select((b, j) => new EntwurfStartbetrieb(stadtplaetze.Contains(b.Platz) ? "" : b.Platz, b.Art, b.Produkt)
                {
                    Vorlage = bv is not null && j < bv.Count ? bv[j]?.DeepClone() as JsonObject : null,
                }).ToList(),
            };
        }).ToList();
        return e;
    }

    internal override EntwurfTeil KopieTeil()
    {
        var k = new EntwurfStart { SpielerFarbe = SpielerFarbe, Vorlage = Vorlage, _spielerVorlage = _spielerVorlage, Angelegt = Angelegt };
        k._haeuser = _haeuser.ToList();
        return k;
    }

    internal override void UebernimmTeil(EntwurfTeil werte)
    {
        var o = (EntwurfStart)werte;
        (SpielerFarbe, Vorlage, _spielerVorlage, _haeuser, Angelegt) = (o.SpielerFarbe, o.Vorlage, o._spielerVorlage, o._haeuser.ToList(), o.Angelegt);
    }

    internal override bool GleicheWerte(EntwurfTeil anderer) => anderer is EntwurfStart o && IstLeer == o.IstLeer && AlsJson().ToJsonString() == o.AlsJson().ToJsonString();

    internal override JsonObject AlsJson()
    {
        // „wohnhaus“, „werkstatt“, „jeBeruf“ und „hofstellen“: Felder alter Karten, fallen beim Schreiben weg
        var spieler = Zusammensetzen(_spielerVorlage, ("wohnhaus", null), ("farbe", SpielerFarbe is null ? null : JsonValue.Create(SpielerFarbe)),
            ("werkstatt", null), ("jeBeruf", null));
        return Zusammensetzen(Vorlage, ("hofstellen", null), ("spieler", spieler.Count == 0 ? null : spieler),
            ("haeuser", new JsonArray(_haeuser.Select(h => (JsonNode)h.AlsJson()).ToArray())));
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

    /// <summary>Farben für neue KI-Häuser (die erste freie; der Spieler trägt die Farbe seines Wappens, 17.11.2026).</summary>
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
        for (var i = 0; i < s.Haeuser.Count; i++)
            for (var j = 0; j < s.Haeuser[i].Betriebe.Count; j++)
                if (s.Haeuser[i].Betriebe[j].Platz != "") liste.Add(new Platzbelegung(s.Haeuser[i].Betriebe[j].Platz, i, j));
        return liste;
    }

    /// <summary>Wer dieses Gebäude am Start nutzt (leer: niemand).</summary>
    public IReadOnlyList<Platzbelegung> BelegungVon(string kennung) => Belegung().Where(b => b.Kennung == kennung).ToList();

    /// <summary>Die Umlandbetriebe der Karte, die noch kein Haus am Start führt.</summary>
    public IReadOnlyList<EntwurfGebaeude> FreieBetriebsplaetze()
    {
        var belegt = Belegung().Select(b => b.Kennung).ToHashSet();
        return _gebaeude.Where(g => g.Art == Bausteinart.Umlandbetrieb && !belegt.Contains(g.Kennung)).ToList();
    }

    /// <summary>Die Betriebsart eines Umlandbetriebs der Karte (leer: kein Umlandbetrieb).</summary>
    public IReadOnlyList<Betriebsart> BetriebsartenFuer(EntwurfGebaeude platz) =>
        platz.Art == Bausteinart.Umlandbetrieb && platz.Baustein?.BetriebsartWert is { } art ? new[] { art } : Array.Empty<Betriebsart>();

    /// <summary>Die Werkstätten in der Stadt, mit denen ein Haus beginnen kann (Stufe 1; leer ohne <see cref="Daten"/>).</summary>
    public IReadOnlyList<Betriebsart> Stadtwerkstaetten() =>
        Daten is { } d ? d.Betriebsarten.Where(x => x.Value.InDerStadt && x.Value.Stufe == 1).Select(x => x.Key).ToList() : Array.Empty<Betriebsart>();

    // ── Befehle: KI-Häuser ───────────────────────────────────────────────────

    /// <summary>
    /// Fügt ein KI-Haus hinten an (bis <see cref="HoechstensHaeuser"/>): erster freier Name aus <see cref="Hausnamen"/> (oder
    /// <paramref name="name"/>), Mensch, Mann, ein freies Wappen und eine freie Farbe, noch ohne Betrieb. Gibt den Index zurück; −1 (kein Schritt), wenn schon acht Häuser da sind.
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
            Hausfarben.FirstOrDefault(f => f != s.SpielerFarbe && s.Haeuser.All(h => h.Farbe != f)));
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

    /// <summary>
    /// Gibt dem Haus einen Startbetrieb: die Werkstatt in der Stadt (<paramref name="platz"/> „“, eine Art aus <see cref="Stadtwerkstaetten"/>)
    /// oder einen Umlandbetrieb der Karte (seine Kennung; hat er noch keinen Namen, bekommt er einen). Liefert den Index des Betriebs.
    /// </summary>
    public int BetriebHinzufuegen(int haus, string platz, Betriebsart art, string? produkt = null)
    {
        PruefeHaus(haus);
        var h = _start.Haeuser[haus];
        // Die Werkstatt in der Stadt hat keinen Platz (platz „“); ein Platz ist ein Umlandbetrieb der Karte.
        var g = platz == "" ? null : GebaeudeMit(platz) ?? throw new ArgumentException($"Kein Gebäude „{platz}“.", nameof(platz));
        var befehle = new List<IKartenbefehl>();
        if (Aenderung(_start, n => n.HaeuserSetzen(n.Haeuser.Select((x, i) => i == haus ? x with { Betriebe = x.Betriebe.Append(new EntwurfStartbetrieb(platz, art, produkt)).ToList() } : x)))
            is { } eigener)
            befehle.Add(eigener);
        if (g is { Art: Bausteinart.Umlandbetrieb } && string.IsNullOrWhiteSpace(g.Betrieb))
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

    /// <summary>Die Farbe des Spielerhauses als „#rrggbb“ (null: keine).</summary>
    public void SpielerFarbeSetzen(string? farbe) =>
        Aendern(_start, "Farbe des Spielers geändert", n => n.SpielerFarbe = string.IsNullOrWhiteSpace(farbe) ? null : farbe);

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
        // Nennt die Karte keine KI-Häuser, würfelt die Partie sie wie im Spiel – so viele, wie hineinpassen (Ausprobieren begrenzt die Zahl)
        if (start.Haeuser.Count == 0)
            return new NeuePartie(Familienname: familie, Beruf: beruf, Rivalen: Partiebesetzung.HoechstensHoefe - 1, Wappen: wappen);
        // Jedes KI-Haus mit Beruf, Volk und Betrieben der Startbelegung – so, wie die Karte eingestellt ist (im Spiel wird die
        // Startaufstellung gelost). Wo die Häuser stehen, lost in jedem Fall das Startortlos in den Bauzonen.
        return new NeuePartie(Familienname: familie, Beruf: beruf, Rivalen: start.Haeuser.Count, Wappen: wappen, Aufstellung: Aufstellungsart.Vorgabe);
    }

    /// <summary>
    /// „Karte ausprobieren“: stellt eine neue Partie auf genau diesem Entwurf auf (ohne Datei), mit dem gewählten Beruf und allen KI-Häusern
    /// (<see cref="Probepartie"/>). Nur ohne Fehler; fehlen dem Entwurf noch <see cref="Daten"/>, prüft er mit <paramref name="daten"/>.
    /// Mit <paramref name="startwahl"/> (die Kartenwerkstatt im Spiel, „nur noch Bauzonen“ 15.11.2026) beginnt die Probe wie eine Partie aus dem
    /// Menü: Der Spieler setzt Wohnhaus und Werkstatt selbst (<see cref="NeuePartie.Startwahl"/>), die KI-Häuser werden in die Bauzonen gelost –
    /// die Plätze der Karte gelten nicht, und es spielen höchstens so viele KI-Häuser mit, wie in die Zonen passen.
    /// </summary>
    /// <exception cref="InvalidOperationException">Wenn der Entwurf Fehler hat (<see cref="HatFehler"/>).</exception>
    /// <exception cref="KartenFehler">Wenn der Leser des Kerns die Karte ablehnt (sollte ohne Fehler nicht vorkommen).</exception>
    /// <exception cref="ArgumentException">Wenn es den Beruf in den Daten nicht gibt.</exception>
    public (Simulation Simulation, Konto Spieler) Ausprobieren(Wirtschaftsdaten daten, string beruf = "Schmied", Partieeinstellungen? einstellungen = null,
        Spielzeit? startzeit = null, bool startwahl = false)
    {
        Daten ??= daten;
        if (HatFehler) throw new InvalidOperationException("Die Karte hat Fehler – erst beheben, dann ausprobieren.");
        var karte = ZuStadtkarte();
        var neu = Probepartie(beruf);
        // höchstens so viele KI-Häuser, wie mit dem Spieler in die Bauzonen passen
        neu = neu with { Startwahl = startwahl, Rivalen = Math.Min(neu.Rivalen, Partiebesetzung.HoechstensKi(karte, neu.Aufstellung, daten)) };
        return Partieaufbau.Erstellen(einstellungen ?? Partieeinstellungen.Standard, daten, neu, startzeit, karte);
    }
}
