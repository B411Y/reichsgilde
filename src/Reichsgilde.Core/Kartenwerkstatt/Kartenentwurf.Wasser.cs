using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Wasser im Kartenentwurf: Flüsse und Bäche, Seen und Teiche, Fährstellen (Kartenwerkstatt, Schritt 5c)
//
//  Ein Fluss wird wie ein Weg aus Stützpunkten gezeichnet – in Fließrichtung (umkehren geht mit einem Befehl). Jeder Stützpunkt kann eine
//  eigene Breite und Tiefe haben; ohne gilt die des Flusses bzw. die Vorgabe der Art (Wasserform). Ein See ist eine Fläche wie eine
//  Naturfläche (Ecken ziehen, einfügen, löschen über die Flächenbefehle) mit Wasserstand und Tiefe. Eine Fährstelle hat zwei Anleger; jeder
//  rastet beim Setzen an einem Weg ein (der Weg bekommt dort einen Stützpunkt), und ein Weg, der an einem Anleger beginnt oder endet, rastet
//  dort ein (Fangziel Anleger). Einen Hafen setzt man aus dem Katalog („hafenplatz“, Umlandstelle mit amUfer) – mit der Vorderseite zum Wasser.
//  Brücken und Furten setzt niemand: Sie entstehen, wo ein Weg einen Fluss quert (Wasserplan).
//  Jede Änderung ist ein Befehl mit Rückgängig; Gelände und Bett folgen von selbst (Gelaendeform.WasserVeraltet).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Ein Fluss oder Bach im Entwurf: Art, Name, Stützpunkte in Fließrichtung, je Stützpunkt optional eigene Breite und Tiefe, dazu Breite, Tiefe
/// und Strömung für die übrigen Punkte.
/// </summary>
public sealed class EntwurfFluss : EntwurfTeil
{
    public override Kartenteil Teil => Kartenteil.Gewaesser;
    public override string Bezeichnung => Name == "" ? Kartenentwurf.GewaesserName(Art) : Name;

    /// <summary>„fluss“ oder „bach“.</summary>
    public string Art { get; internal set; } = "fluss";

    public string Name { get; internal set; } = "";

    /// <summary>Breite der Punkte ohne eigene (Meter; 0 = nach der Art, <see cref="Wasserform.Vorgabebreite"/>).</summary>
    public double Breite { get; internal set; }

    /// <summary>Tiefe der Punkte ohne eigene (Meter; null = aus der Breite, <see cref="Wasserform.Vorgabetiefe"/>).</summary>
    public double? Tiefe { get; internal set; }

    /// <summary>Strömung in der Mitte (m/s; null = nach der Art).</summary>
    public double? Stroemung { get; internal set; }

    private List<Kartenort> _punkte = new();
    private List<double?> _breiten = new();
    private List<double?> _tiefen = new();
    private Wegverlauf? _verlauf;

    /// <summary>Die Stützpunkte in Fließrichtung (in der Datei: <c>linie</c>).</summary>
    public IReadOnlyList<Kartenort> Punkte => _punkte;

    /// <summary>Die eigene Breite je Stützpunkt (null: die des Flusses).</summary>
    public IReadOnlyList<double?> Breiten => _breiten;

    /// <summary>Die eigene Tiefe je Stützpunkt (null: die des Flusses).</summary>
    public IReadOnlyList<double?> Tiefen => _tiefen;

    /// <summary>Die Breite der Punkte ohne eigene.</summary>
    public double BreiteWirksam => Breite > 0 ? Breite : Wasserform.Vorgabebreite(Art);

    /// <summary>Die Breite am Stützpunkt i.</summary>
    public double BreiteAn(int i) => _breiten[i] ?? BreiteWirksam;

    /// <summary>Die Tiefe am Stützpunkt i.</summary>
    public double TiefeAn(int i) => _tiefen[i] ?? Tiefe ?? Wasserform.Vorgabetiefe(BreiteAn(i));

    /// <summary>Die Strömung in der Mitte (m/s).</summary>
    public double StroemungWirksam => Stroemung ?? Wasserform.VorgabeStroemung(Art);

    /// <summary>Die Mittellinie als weiche Kurve (etwa alle 1 m ein Punkt), wie Wasserform sie rechnet.</summary>
    public IReadOnlyList<Kartenort> Verlauf => Kurve.Punkte.Select(Kartenort.Aus).ToList();

    private Wegverlauf Kurve => _verlauf ??= Wegkurve.Verlauf(Roh(_punkte), glatt: true);

    internal void PunkteSetzen(IEnumerable<(Kartenort Ort, double? Breite, double? Tiefe)> punkte)
    {
        var liste = punkte.ToList();
        (_punkte, _breiten, _tiefen, _verlauf) = (liste.Select(p => p.Ort).ToList(), liste.Select(p => p.Breite).ToList(), liste.Select(p => p.Tiefe).ToList(), null);
    }

    /// <summary>Die Stützpunkte mit ihrer eigenen Breite und Tiefe.</summary>
    internal IEnumerable<(Kartenort Ort, double? Breite, double? Tiefe)> Stuetzpunkte => _punkte.Select((p, i) => (p, _breiten[i], _tiefen[i]));

    /// <summary>Wo ein Klick auf die Linie einen Stützpunkt einfügt: Index (hinter dem Stützpunkt des Stücks), Ort auf der Kurve, Abstand.</summary>
    public (int Index, Kartenort Ort, double Abstand) NaechsteStelle(Kartenort p)
    {
        var v = Kurve.Punkte.Select(Kartenort.Aus).ToList();
        if (v.Count == 1) return (1, v[0], p.Abstand(v[0]));
        var (best, index, ort) = (double.MaxValue, 1, v.Count > 0 ? v[0] : p);
        for (var i = 0; i + 1 < v.Count; i++)
        {
            var (a, b) = (v[i], v[i + 1]);
            var (dx, dz) = (b.X - a.X, b.Z - a.Z);
            var l2 = dx * dx + dz * dz;
            var t = l2 == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Z - a.Z) * dz) / l2, 0, 1);
            var q = new Kartenort(a.X + t * dx, a.Z + t * dz);
            if (p.Abstand(q) < best) (best, index, ort) = (p.Abstand(q), Kurve.Abschnitte[i] + 1, q);
        }
        return (index, ort, best);
    }

    /// <summary>Der Fluss in den Typen der Stadtkarte: je Punkt [x, z], mit eigener Breite [x, z, b], mit eigener Tiefe [x, z, b, t].</summary>
    public Gewaesser AlsGewaesser() => new()
    {
        Art = Art, Name = Name, Breite = Breite, Tiefe = Tiefe, Stroemung = Stroemung,
        Linie = _punkte.Select((p, i) => _tiefen[i] is { } t ? new[] { p.X, p.Z, BreiteAn(i), t } : _breiten[i] is { } b ? new[] { p.X, p.Z, b } : new[] { p.X, p.Z }).ToList(),
    };

    internal static EntwurfFluss Aus(Gewaesser g, JsonObject? vorlage)
    {
        var f = new EntwurfFluss { Art = g.Art, Name = g.Name, Breite = g.Breite, Tiefe = g.Tiefe, Stroemung = g.Stroemung, Vorlage = vorlage };
        f.PunkteSetzen((g.Linie ?? new List<double[]>()).Where(p => p is { Length: >= 2 })
            .Select(p => (new Kartenort(p[0], p[1]), p.Length >= 3 ? p[2] : (double?)null, p.Length >= 4 ? p[3] : (double?)null)));
        return f;
    }

    internal override EntwurfTeil KopieTeil()
    {
        var k = new EntwurfFluss { Art = Art, Name = Name, Breite = Breite, Tiefe = Tiefe, Stroemung = Stroemung, Vorlage = Vorlage };
        k.PunkteSetzen(Stuetzpunkte);
        return k;
    }

    internal override void UebernimmTeil(EntwurfTeil werte)
    {
        var o = (EntwurfFluss)werte;
        (Art, Name, Breite, Tiefe, Stroemung, Vorlage) = (o.Art, o.Name, o.Breite, o.Tiefe, o.Stroemung, o.Vorlage);
        PunkteSetzen(o.Stuetzpunkte);
    }

    internal override bool GleicheWerte(EntwurfTeil anderer) => anderer is EntwurfFluss o && Art == o.Art && Name == o.Name && Breite == o.Breite
        && Tiefe == o.Tiefe && Stroemung == o.Stroemung && _punkte.SequenceEqual(o._punkte) && _breiten.SequenceEqual(o._breiten) && _tiefen.SequenceEqual(o._tiefen);

    internal override JsonObject AlsJson() => Zusammensetzen(Vorlage,
        ("art", Art), ("name", Name),
        ("linie", new JsonArray(AlsGewaesser().Linie!.Select(p => (JsonNode)new JsonArray(p.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray())).ToArray())),
        ("breite", Breite > 0 || Vorlage?.ContainsKey("breite") == true ? JsonValue.Create(Breite) : null),
        ("tiefe", Tiefe is { } t ? JsonValue.Create(t) : null), ("stroemung", Stroemung is { } s ? JsonValue.Create(s) : null));
}

/// <summary>Ein See oder Teich im Entwurf: eine Fläche (Ecken wie bei Naturflächen, ohne Aussparungen) mit Wasserstand und Tiefe.</summary>
public sealed class EntwurfSee : EntwurfFlaeche
{
    public override Kartenteil Teil => Kartenteil.Gewaesser;
    public override string Bezeichnung => Name == "" ? Kartenentwurf.GewaesserName(Art) : Name;

    /// <summary>„see“ oder „teich“.</summary>
    public string Art { get; internal set; } = "see";

    public string Name { get; internal set; } = "";

    /// <summary>Die Höhe des Spiegels (Meter); null = 0,4 m unter dem tiefsten Punkt des Ufers (folgt dem Gelände).</summary>
    public double? Wasserstand { get; internal set; }

    /// <summary>Die größte Tiefe (Meter); null = 2 m.</summary>
    public double? Tiefe { get; internal set; }

    internal override void VieleckSetzen(int aussparung, IEnumerable<Kartenort> ecken) => _ecken = ecken.ToList();

    /// <summary>Der See in den Typen der Stadtkarte.</summary>
    public Gewaesser AlsGewaesser() => new() { Art = Art, Name = Name, Flaeche = Roh(_ecken), Wasserstand = Wasserstand, Tiefe = Tiefe };

    internal static EntwurfSee Aus(Gewaesser g, JsonObject? vorlage) => new()
    {
        Art = g.Art, Name = g.Name, _ecken = Orte(g.Flaeche?.Select(p => p.Length >= 2 ? new[] { p[0], p[1] } : p)), Wasserstand = g.Wasserstand,
        Tiefe = g.Tiefe, Vorlage = vorlage,
    };

    internal override EntwurfTeil KopieTeil() => new EntwurfSee
    {
        Art = Art, Name = Name, _ecken = _ecken.ToList(), Wasserstand = Wasserstand, Tiefe = Tiefe, Vorlage = Vorlage,
    };

    internal override void UebernimmTeil(EntwurfTeil werte)
    {
        var o = (EntwurfSee)werte;
        (Art, Name, Wasserstand, Tiefe, Vorlage) = (o.Art, o.Name, o.Wasserstand, o.Tiefe, o.Vorlage);
        _ecken = o._ecken.ToList();
    }

    internal override bool GleicheWerte(EntwurfTeil anderer) => anderer is EntwurfSee o && Art == o.Art && Name == o.Name
        && Wasserstand == o.Wasserstand && Tiefe == o.Tiefe && _ecken.SequenceEqual(o._ecken);

    internal override JsonObject AlsJson() => Zusammensetzen(Vorlage,
        ("art", Art), ("name", Name), ("flaeche", JsonPunkte(_ecken)),
        ("wasserstand", Wasserstand is { } w ? JsonValue.Create(w) : null), ("tiefe", Tiefe is { } t ? JsonValue.Create(t) : null));
}

/// <summary>
/// Eine Fährstelle im Entwurf (Liste <c>uebergaenge</c>, art „faehre“): Name und die beiden Anleger. Einträge mit art „bruecke“ (Format 1,
/// Köln) liest der Entwurf mit und schreibt sie unverändert wieder – Brücken entstehen seit Schritt 5c von selbst.
/// </summary>
public sealed class EntwurfFaehre : EntwurfTeil
{
    public override Kartenteil Teil => Kartenteil.Faehre;
    public override string Bezeichnung => Name == "" ? (IstFaehre ? "Fähre" : "Brücke") : Name;

    /// <summary>„faehre“ (oder „bruecke“ aus Format 1).</summary>
    public string Art { get; internal set; } = Uebergang.Faehre;

    public string Name { get; internal set; } = "";

    /// <summary>Die beiden Anleger.</summary>
    public Kartenort Von { get; internal set; }

    public Kartenort Bis { get; internal set; }

    /// <summary>Ist es eine Fährstelle (und kein alter Brückeneintrag)?</summary>
    public bool IstFaehre => Art == Uebergang.Faehre;

    /// <summary>Anleger 0 (<see cref="Von"/>) oder 1 (<see cref="Bis"/>).</summary>
    public Kartenort Anleger(int i) => i == 0 ? Von : Bis;

    /// <summary>Die Fährstelle in den Typen der Stadtkarte.</summary>
    public Uebergang AlsUebergang() => new() { Art = Art, Name = Name, Von = new[] { Von.X, Von.Z }, Bis = new[] { Bis.X, Bis.Z } };

    internal static EntwurfFaehre Aus(Uebergang u, JsonObject? vorlage) => new()
    {
        Art = u.Art, Name = u.Name, Von = u.Von is { Length: >= 2 } v ? new Kartenort(v[0], v[1]) : default,
        Bis = u.Bis is { Length: >= 2 } b ? new Kartenort(b[0], b[1]) : default, Vorlage = vorlage,
    };

    internal override EntwurfTeil KopieTeil() => new EntwurfFaehre { Art = Art, Name = Name, Von = Von, Bis = Bis, Vorlage = Vorlage };

    internal override void UebernimmTeil(EntwurfTeil werte)
    {
        var o = (EntwurfFaehre)werte;
        (Art, Name, Von, Bis, Vorlage) = (o.Art, o.Name, o.Von, o.Bis, o.Vorlage);
    }

    internal override bool GleicheWerte(EntwurfTeil anderer) =>
        anderer is EntwurfFaehre o && Art == o.Art && Name == o.Name && Von == o.Von && Bis == o.Bis;

    internal override JsonObject AlsJson() => Zusammensetzen(Vorlage, ("art", Art), ("name", Name), ("von", JsonPunkt(Von)), ("bis", JsonPunkt(Bis)));
}

public sealed partial class Kartenentwurf
{
    /// <summary>Der Baustein des Katalogs für einen Hafen (Umlandstelle am Ufer).</summary>
    public const string HafenKatalog = "hafenplatz";

    /// <summary>So weit (Meter) darf ein Anleger höchstens vom Wasser stehen.</summary>
    public const double AnlegerUfer = 15;

    /// <summary>So weit (Meter) darf die Vorderseite eines Gebäudes am Ufer (Hafen) höchstens vom Wasser stehen.</summary>
    public const double HafenUfer = 3;

    /// <summary>Anzeigename einer Gewässerart („Fluss“, „Bach“, „See“, „Teich“).</summary>
    public static string GewaesserName(string art) => art switch
    {
        "bach" => "Bach",
        "see" => "See",
        "teich" => "Teich",
        _ => "Fluss",
    };

    /// <summary>Die Gewässer (Flüsse, Bäche, Seen, Teiche) in der Reihenfolge der Datei (<c>wasser</c>).</summary>
    public IReadOnlyList<EntwurfTeil> Gewaesser => _gewaesser;

    /// <summary>Die Flüsse und Bäche.</summary>
    public IEnumerable<EntwurfFluss> Fluesse => _gewaesser.OfType<EntwurfFluss>();

    /// <summary>Die Seen und Teiche.</summary>
    public IEnumerable<EntwurfSee> Seen => _gewaesser.OfType<EntwurfSee>();

    /// <summary>Die Fährstellen (und alte Brückeneinträge aus Format 1) in der Reihenfolge der Datei (<c>uebergaenge</c>).</summary>
    public IReadOnlyList<EntwurfFaehre> Faehren => _faehren;

    /// <summary>Die Gewässer in den Typen der Stadtkarte (für Wasserform und Prüfung).</summary>
    public IReadOnlyList<Gewaesser> Wasser => _gewaesser.Select(g => g is EntwurfFluss f ? f.AlsGewaesser() : ((EntwurfSee)g).AlsGewaesser()).ToList();

    /// <summary>Die Liste <c>uebergaenge</c> in den Typen der Stadtkarte.</summary>
    public IReadOnlyList<Uebergang> Uebergaenge => _faehren.Select(f => f.AlsUebergang()).ToList();

    /// <summary>Die Gewässer mit Spiegel und Bett (<see cref="Gelaendeform.Wasser"/>): Flussläufe, Seen, IstWasser, Strömung …</summary>
    public Wasserform Wasserform => _gelaende.Wasser;

    private Wasserplan? _wasserplan;

    /// <summary>Brücken und Furten (wo Wege Flüsse queren), Fähren und Wege im See – nach jeder Änderung neu (<see cref="Bauen.Wasserplan"/>).</summary>
    public Wasserplan Wasserplan => _wasserplan ??= new Wasserplan(_gelaende,
        _wege.Select(w => new Planweg(w.Name, w.Punkte.Count >= 2 ? w.Verlauf : Array.Empty<Kartenort>(), w.BreiteWirksam, w.IstKarrenstrasse)).ToList(),
        Uebergaenge);

    // ── Flüsse ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Legt einen Fluss (oder Bach) an: Stützpunkte in Fließrichtung (mindestens zwei verschiedene), Breite (0 = nach der Art; mindestens 0,5 m),
    /// Name (Vorgabe „Fluss“, „Fluss 2“ …). Ein Schritt.
    /// </summary>
    /// <exception cref="ArgumentException">Mit weniger als zwei verschiedenen Punkten oder einer unbekannten Art.</exception>
    public EntwurfFluss FlussAnlegen(IReadOnlyList<Kartenort> punkte, string art = "fluss", double breite = 0, string? name = null)
    {
        if (!Wasserform.IstFliessend(art)) throw new ArgumentException($"„{art}“ ist kein Fluss und kein Bach.", nameof(art));
        var liste = OhneDoppelte(punkte.Select(Gerundet));
        if (liste.Count < 2) throw new ArgumentException("Ein Fluss braucht mindestens zwei verschiedene Punkte.", nameof(punkte));
        var f = new EntwurfFluss
        {
            Art = art, Name = string.IsNullOrWhiteSpace(name) ? FreierName(GewaesserName(art), GewaesserNamen) : name,
            Breite = Wassermass(breite > 0 ? breite : Wasserform.Vorgabebreite(art), Wasserform.MindestBreite, Wasserform.HoechstBreite),
        };
        f.PunkteSetzen(liste.Select(p => (p, (double?)null, (double?)null)));
        Geschichte.Ausfuehren(new TeilSetzen(f, _gewaesser.Count));
        return f;
    }

    /// <summary>Verschiebt einen Stützpunkt (Breite und Tiefe bleiben).</summary>
    public void FlussPunktVerschieben(EntwurfFluss f, int index, double x, double z)
    {
        Pruefen(f, index, f.Punkte.Count);
        var ort = Gerundet(new Kartenort(x, z));
        Aendern(f, "Stützpunkt verschoben", n => n.PunkteSetzen(n.Stuetzpunkte.Select((p, i) => i == index ? (ort, p.Breite, p.Tiefe) : p)));
    }

    /// <summary>
    /// Fügt einen Stützpunkt an der Stelle <paramref name="index"/> ein (0 = vor dem ersten, Anzahl = hinter dem letzten). Haben die Nachbarn eine
    /// eigene Breite oder Tiefe, bekommt er ihr Mittel – so springt der Fluss nicht.
    /// </summary>
    public void FlussPunktEinfuegen(EntwurfFluss f, int index, double x, double z)
    {
        Pruefen(f, index, f.Punkte.Count + 1);
        var ort = Gerundet(new Kartenort(x, z));
        double? Mittel(Func<int, double?> eigen, Func<int, double> wirksam)
        {
            var nachbarn = new[] { index - 1, index }.Where(i => i >= 0 && i < f.Punkte.Count).ToList();
            return nachbarn.Any(i => eigen(i) is not null) ? Math.Round(nachbarn.Average(wirksam), 2) : null;
        }
        var breite = Mittel(i => f.Breiten[i], f.BreiteAn);
        var tiefe = Mittel(i => f.Tiefen[i], f.TiefeAn);
        Aendern(f, "Stützpunkt eingefügt", n => n.PunkteSetzen(n.Stuetzpunkte.Take(index).Append((ort, breite, tiefe)).Concat(n.Stuetzpunkte.Skip(index))));
    }

    /// <summary>Fügt einen Stützpunkt auf der Linie ein, wo (x, z) ihr am nächsten liegt (Klick auf den Fluss); −1: dort steht schon einer.</summary>
    public int FlussPunktEinfuegenBei(EntwurfFluss f, double x, double z)
    {
        Pruefen(f, 0, 1);
        var (index, ort, _) = f.NaechsteStelle(new Kartenort(x, z));
        ort = Gerundet(ort);
        if (f.Punkte.Any(p => p.Abstand(ort) < 1e-3)) return -1;
        FlussPunktEinfuegen(f, index, ort.X, ort.Z);
        return index;
    }

    /// <summary>Löscht einen Stützpunkt; false (kein Schritt), wenn der Fluss dann weniger als zwei hätte.</summary>
    public bool FlussPunktLoeschen(EntwurfFluss f, int index)
    {
        Pruefen(f, index, f.Punkte.Count);
        if (f.Punkte.Count <= 2) return false;
        Aendern(f, "Stützpunkt gelöscht", n => n.PunkteSetzen(n.Stuetzpunkte.Where((_, i) => i != index)));
        return true;
    }

    /// <summary>
    /// Setzt die Breite am Stützpunkt <paramref name="index"/> (Meter, auf 5 cm, 0,5 bis 300 m); null nimmt die eigene Breite weg. Mit
    /// <paramref name="index"/> = −1 die Breite des Flusses (für alle Punkte ohne eigene; null = nach der Art).
    /// </summary>
    public void FlussBreiteSetzen(EntwurfFluss f, int index, double? breite)
    {
        var wert = breite is { } b ? Wassermass(b, Wasserform.MindestBreite, Wasserform.HoechstBreite) : (double?)null;
        if (index < 0)
        {
            Aendern(f, "Breite geändert", n => n.Breite = wert ?? 0);
            return;
        }
        Pruefen(f, index, f.Punkte.Count);
        Aendern(f, "Breite geändert", n => n.PunkteSetzen(n.Stuetzpunkte.Select((p, i) => i == index ? (p.Ort, wert, p.Tiefe) : p)));
    }

    /// <summary>Setzt die Tiefe am Stützpunkt (Meter, auf 5 cm, 0,1 bis 20 m; null = die des Flusses); −1: die Tiefe des Flusses (null = aus der Breite).</summary>
    public void FlussTiefeSetzen(EntwurfFluss f, int index, double? tiefe)
    {
        var wert = tiefe is { } t ? Wassermass(t, Wasserform.MindestTiefe, Wasserform.HoechstTiefe) : (double?)null;
        if (index < 0)
        {
            Aendern(f, "Tiefe geändert", n => n.Tiefe = wert);
            return;
        }
        Pruefen(f, index, f.Punkte.Count);
        Aendern(f, "Tiefe geändert", n => n.PunkteSetzen(n.Stuetzpunkte.Select((p, i) => i == index ? (p.Ort, p.Breite, wert) : p)));
    }

    /// <summary>Kehrt die Fließrichtung um (die Stützpunkte mit ihrer Breite und Tiefe in umgekehrter Reihenfolge).</summary>
    public void FlussUmkehren(EntwurfFluss f) => Aendern(f, "umgekehrt", n => n.PunkteSetzen(n.Stuetzpunkte.Reverse().ToList()));

    /// <summary>Setzt die Strömung in der Mitte (m/s, auf 0,05, 0 bis 5; null = nach der Art).</summary>
    public void FlussStroemungSetzen(EntwurfFluss f, double? stroemung) =>
        Aendern(f, "Strömung geändert", n => n.Stroemung = stroemung is { } s ? Math.Round(Math.Clamp(s, 0, 5) / 0.05) * 0.05 : null);

    /// <summary>Verschiebt den ganzen Fluss um (dx, dz) Meter.</summary>
    public void FlussVerschieben(EntwurfFluss f, double dx, double dz) =>
        Aendern(f, "verschoben", n => n.PunkteSetzen(n.Stuetzpunkte.Select(p => (Gerundet(new Kartenort(p.Ort.X + dx, p.Ort.Z + dz)), p.Breite, p.Tiefe)).ToList()));

    // ── Seen ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Legt einen See (oder Teich) an: Ecken (mindestens drei verschiedene), Art, Name, Wasserstand (Meter; null = 0,4 m unter dem tiefsten
    /// Ufer, folgt dem Gelände) und Tiefe (null = 2 m). Ecken ziehen, einfügen und löschen wie bei jeder Fläche (<see cref="EckeVerschieben"/> …).
    /// </summary>
    /// <exception cref="ArgumentException">Mit weniger als drei verschiedenen Ecken oder einer unbekannten Art.</exception>
    public EntwurfSee SeeAnlegen(IReadOnlyList<Kartenort> ecken, string art = "see", string? name = null, double? wasserstand = null, double? tiefe = null)
    {
        if (art is not ("see" or "teich")) throw new ArgumentException($"„{art}“ ist kein See und kein Teich.", nameof(art));
        var s = new EntwurfSee
        {
            Art = art, Name = string.IsNullOrWhiteSpace(name) ? FreierName(GewaesserName(art), GewaesserNamen) : name,
            Wasserstand = wasserstand is { } w ? Stand(w) : null,
            Tiefe = tiefe is { } t ? Wassermass(t, Wasserform.MindestTiefe, Wasserform.HoechstTiefe) : null,
        };
        var liste = Ecken(ecken, s);
        if (liste.Count < 3) throw new ArgumentException("Ein See braucht mindestens drei verschiedene Ecken.", nameof(ecken));
        s.VieleckSetzen(-1, liste);
        Geschichte.Ausfuehren(new TeilSetzen(s, _gewaesser.Count));
        return s;
    }

    /// <summary>Setzt den Wasserstand (Meter, auf 1 cm, −20 bis 60); null: 0,4 m unter dem tiefsten Ufer (folgt dem Gelände).</summary>
    public void SeeWasserstandSetzen(EntwurfSee s, double? wasserstand) =>
        Aendern(s, "Wasserstand geändert", n => n.Wasserstand = wasserstand is { } w ? Stand(w) : null);

    /// <summary>Setzt die größte Tiefe (Meter, auf 5 cm, 0,1 bis 20; null = 2 m).</summary>
    public void SeeTiefeSetzen(EntwurfSee s, double? tiefe) =>
        Aendern(s, "Tiefe geändert", n => n.Tiefe = tiefe is { } t ? Wassermass(t, Wasserform.MindestTiefe, Wasserform.HoechstTiefe) : null);

    // ── Für beide ────────────────────────────────────────────────────────────

    public void GewaesserUmbenennen(EntwurfTeil g, string name) => Aendern(g, "umbenannt", n =>
    {
        if (n is EntwurfFluss f) f.Name = name;
        else ((EntwurfSee)n).Name = name;
    });

    /// <summary>Ändert die Art: Fluss ↔ Bach bzw. See ↔ Teich (ein Fluss wird kein See).</summary>
    /// <exception cref="ArgumentException">Bei einer Art der anderen Sorte.</exception>
    public void GewaesserArtSetzen(EntwurfTeil g, string art)
    {
        if (g is EntwurfFluss && !Wasserform.IstFliessend(art) || g is EntwurfSee && art is not ("see" or "teich"))
            throw new ArgumentException($"„{g.Bezeichnung}“ kann nicht zur Art „{art}“ werden.", nameof(art));
        Aendern(g, "Art geändert", n =>
        {
            if (n is EntwurfFluss f) f.Art = art;
            else ((EntwurfSee)n).Art = art;
        });
    }

    /// <summary>Löscht einen Fluss oder See (Rückgängig setzt ihn an dieselbe Stelle zurück).</summary>
    public void GewaesserLoeschen(EntwurfTeil g)
    {
        if (g is not (EntwurfFluss or EntwurfSee)) throw new ArgumentException($"„{g.Bezeichnung}“ ist kein Gewässer.", nameof(g));
        TeilEntfernen(g);
    }

    private IEnumerable<string> GewaesserNamen => _gewaesser.Select(g => g is EntwurfFluss f ? f.Name : ((EntwurfSee)g).Name);

    /// <summary>Breite oder Tiefe auf 5 cm in ihren Grenzen.</summary>
    private static double Wassermass(double meter, double min, double max) => Math.Clamp(Kartenraster.Punkt(meter), min, max);

    /// <summary>Ein Wasserstand auf 1 cm in den Grenzen des Höhenrasters.</summary>
    private static double Stand(double meter) => Math.Round(Math.Clamp(meter, Hoehenraster.Tiefste, Hoehenraster.Hoechste), 2) + 0.0;

    // ── Fährstellen ──────────────────────────────────────────────────────────

    /// <summary>
    /// Setzt eine Fährstelle mit zwei Anlegern (an beiden Ufern). Mit <paramref name="einrasten"/> rastet jeder Anleger an einem Weg im
    /// Fangradius ein (auf dessen Stützpunkt, sonst bekommt der Weg dort im selben Schritt einen). Name: Vorgabe „Fähre“, „Fähre 2“ …
    /// </summary>
    /// <exception cref="ArgumentException">Wenn beide Anleger auf demselben Punkt liegen.</exception>
    public EntwurfFaehre FaehreSetzen(Kartenort von, Kartenort bis, string? name = null, bool einrasten = true)
    {
        var nachbarn = new Dictionary<EntwurfWeg, EntwurfWeg>(ReferenceEqualityComparer.Instance);
        var (a, b) = (Gerundet(von), Gerundet(bis));
        if (einrasten) (a, b) = (AnWegRasten(a, nachbarn), AnWegRasten(b, nachbarn));
        if (a.Abstand(b) < 1e-3) throw new ArgumentException("Die beiden Anleger liegen auf demselben Punkt.", nameof(bis));
        var f = new EntwurfFaehre
        {
            Name = string.IsNullOrWhiteSpace(name) ? FreierName("Fähre", _faehren.Select(x => x.Name)) : name, Von = a, Bis = b,
        };
        var befehle = new List<IKartenbefehl> { new TeilSetzen(f, _faehren.Count) };
        befehle.AddRange(NachbarBefehle(nachbarn));
        Geschichte.Ausfuehren(befehle.Count == 1 ? befehle[0] : new Sammelbefehl($"{f.Name} gesetzt", befehle));
        return f;
    }

    /// <summary>Verschiebt einen Anleger (0 = <see cref="EntwurfFaehre.Von"/>, 1 = <see cref="EntwurfFaehre.Bis"/>); rastet wie beim Setzen an einem Weg ein.</summary>
    public void FaehreAnlegerVerschieben(EntwurfFaehre f, int anleger, double x, double z, bool einrasten = true)
    {
        Pruefen(f, anleger, 2);
        var nachbarn = new Dictionary<EntwurfWeg, EntwurfWeg>(ReferenceEqualityComparer.Instance);
        var ort = Gerundet(new Kartenort(x, z));
        if (einrasten) ort = AnWegRasten(ort, nachbarn);
        var eigener = Aenderung(f, n =>
        {
            if (anleger == 0) n.Von = ort;
            else n.Bis = ort;
        });
        Buendeln($"{f.Bezeichnung}: Anleger verschoben", eigener, NachbarBefehle(nachbarn));
    }

    public void FaehreUmbenennen(EntwurfFaehre f, string name) => Aendern(f, "umbenannt", n => n.Name = name);

    /// <summary>Löscht eine Fährstelle (oder einen alten Brückeneintrag).</summary>
    public void FaehreLoeschen(EntwurfFaehre f) => TeilEntfernen(f);

    /// <summary>Ein Anleger rastet an einem Weg ein: auf dessen Stützpunkt, sonst auf seine Linie (eine Kopie in <paramref name="nachbarn"/> bekommt dort einen Stützpunkt).</summary>
    private Kartenort AnWegRasten(Kartenort p, Dictionary<EntwurfWeg, EntwurfWeg> nachbarn)
    {
        if (WegFangen(p, null, Fangradius) is not { Weg: { } weg } fang) return p;
        if (fang.Stuetzpunkt is not null) return fang.Ort;
        if (!nachbarn.TryGetValue(weg, out var kopie)) nachbarn[weg] = kopie = (EntwurfWeg)weg.KopieTeil();
        var (index, ort, _) = kopie.NaechsteStelle(fang.Ort);
        ort = Gerundet(ort);
        if (!kopie.Punkte.Any(q => q.Abstand(ort) < 1e-3))
            kopie.PunkteSetzen(kopie.Punkte.Take(index).Append(ort).Concat(kopie.Punkte.Skip(index)));
        return ort;
    }
}
