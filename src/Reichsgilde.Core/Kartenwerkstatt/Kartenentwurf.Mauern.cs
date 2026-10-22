using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Mauern und Tore im Kartenentwurf (Kartenwerkstatt, Schritt 3)
//
//  Eine Mauer ist eine Linie aus geraden Stücken (Ecken), offen (Köln am Rhein) oder geschlossen (Ring). Türme setzt das Spiel selbst
//  (Mauerplan: Ecktürme an jedem Knick, Mauertürme etwa alle 35 m, nicht in Toren) – der Entwurf speichert nur Ecken und Tore.
//  Ein Tor liegt immer auf der Linie: Setzen und Verschieben legen es auf die nächste Stelle der Mauer. Ändern sich die Ecken, wandern
//  die Tore mit (bleiben an derselben Stelle ihrer Strecke bzw. kommen auf die nächste Stelle der neuen Linie). Seit Schritt 4 wandert ein
//  Weg durch das Tor mit: Jeder Stützpunkt, der in der Tormitte lag, folgt dem Tor – im selben Schritt (ein Rückgängig nimmt beides zurück),
//  sonst kreuzte der Weg danach die Mauer ohne Tor.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Tor einer Mauer im Entwurf: Name, Ort (auf der Mauerlinie, auf 5 cm) und ob es nachts schließt.</summary>
public sealed record EntwurfTor(string Name, double X, double Z, bool SchliesstNachts = true)
{
    public Kartenort Punkt => new(X, Z);

    /// <summary>Der Eintrag aus der Datei (Reihenfolge und unbekannte Felder beim Schreiben); zählt beim Vergleich nicht.</summary>
    internal JsonObject? Vorlage { get; init; }

    public bool Equals(EntwurfTor? o) => o is not null && Name == o.Name && X == o.X && Z == o.Z && SchliesstNachts == o.SchliesstNachts;
    public override int GetHashCode() => HashCode.Combine(Name, X, Z, SchliesstNachts);

    internal JsonObject AlsJson() => EntwurfTeil.Zusammensetzen(Vorlage,
        ("name", Name), ("punkt", EntwurfTeil.JsonPunkt(Punkt)), ("schliesstNachts", SchliesstNachts));
}

/// <summary>
/// Eine Mauer im Entwurf: Name, Ecken, offen oder geschlossen, Mauer-Baukasten des Katalogs und ihre Tore. Wo das Spiel Türme und
/// Torburgen setzt, sagt <see cref="Plan"/>.
/// </summary>
public sealed class EntwurfMauer : EntwurfTeil
{
    public override Kartenteil Teil => Kartenteil.Mauer;
    public override string Bezeichnung => Name == "" ? "Mauer" : Name;

    public string Name { get; internal set; } = "";

    /// <summary>Kennung des Mauer-Baukastens im Katalog (Art „mauer“).</summary>
    public string Katalog { get; internal set; } = "stadtmauer";

    private bool _geschlossen;
    private List<Kartenort> _ecken = new();
    private List<EntwurfTor> _tore = new();
    private Mauerplan? _plan;

    /// <summary>Läuft die letzte Strecke zurück zur ersten Ecke (Ring)?</summary>
    public bool Geschlossen
    {
        get => _geschlossen;
        internal set => (_geschlossen, _plan) = (value, null);
    }

    /// <summary>Die Ecken der Linie (in der Datei: <c>linie</c>); jede ist ein Knick oder ein Ende.</summary>
    public IReadOnlyList<Kartenort> Ecken => _ecken;

    public IReadOnlyList<EntwurfTor> Tore => _tore;

    /// <summary>Die Linie als Punktfolge, bei einer geschlossenen Mauer mit der ersten Ecke hinten dran (zum Zeichnen und Rechnen).</summary>
    public IReadOnlyList<Kartenort> Linie => _geschlossen && _ecken.Count > 0 ? _ecken.Append(_ecken[0]).ToList() : _ecken;

    /// <summary>Wie viele gerade Stücke die Mauer hat.</summary>
    public int Strecken => _ecken.Count < 2 ? 0 : _geschlossen ? _ecken.Count : _ecken.Count - 1;

    /// <summary>Wo das Spiel Mauerstücke, Türme, Ecktürme und Torburgen setzt (dieselbe Rechnung wie im Spiel).</summary>
    public Mauerplan Plan => _plan ??= Mauerplan.Von(Roh(_ecken), _geschlossen, _tore.Select(t => (t.Name, t.X, t.Z)).ToList());

    internal void EckenSetzen(IEnumerable<Kartenort> ecken) => (_ecken, _plan) = (ecken.ToList(), null);
    internal void ToreSetzen(IEnumerable<EntwurfTor> tore) => (_tore, _plan) = (tore.ToList(), null);

    /// <summary>Ecke a und b der Strecke i.</summary>
    public (Kartenort A, Kartenort B) Strecke(int i) => (_ecken[i], _ecken[(i + 1) % _ecken.Count]);

    /// <summary>
    /// Die nächste Stelle der Mauerlinie zu p: welche Strecke (bei Gleichstand die erste), der Punkt darauf, wie weit dort entlang (0 … 1)
    /// und der Abstand. Ohne Strecke: (−1, p, 0, ∞).
    /// </summary>
    public (int Strecke, Kartenort Ort, double Anteil, double Abstand) NaechsteStelle(Kartenort p)
    {
        var best = (Strecke: -1, Ort: p, Anteil: 0.0, Abstand: double.MaxValue);
        for (var i = 0; i < Strecken; i++)
        {
            var (a, b) = Strecke(i);
            var (dx, dz) = (b.X - a.X, b.Z - a.Z);
            var l2 = dx * dx + dz * dz;
            var t = l2 == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Z - a.Z) * dz) / l2, 0, 1);
            var q = new Kartenort(a.X + t * dx, a.Z + t * dz);
            if (p.Abstand(q) < best.Abstand) best = (i, q, t, p.Abstand(q));
        }
        return best;
    }

    /// <summary>Die Mauer in den Typen der Stadtkarte.</summary>
    public Kartenmauer AlsKartenmauer() => new()
    {
        Name = Name, Linie = Roh(_ecken), Geschlossen = _geschlossen, Katalog = Katalog,
        Tore = _tore.Select(t => new Kartentor { Name = t.Name, Punkt = new[] { t.X, t.Z }, SchliesstNachts = t.SchliesstNachts }).ToList(),
    };

    internal static EntwurfMauer Aus(Kartenmauer m, JsonObject? vorlage)
    {
        var torVorlagen = vorlage?["tore"] as JsonArray;
        var e = new EntwurfMauer { Name = m.Name, Katalog = m.Katalog, Geschlossen = m.Geschlossen, Vorlage = vorlage };
        e.EckenSetzen(Orte(m.Linie));
        e.ToreSetzen(m.Tore.Select((t, i) => new EntwurfTor(t.Name, t.Punkt is { Length: 2 } ? t.Punkt[0] : 0, t.Punkt is { Length: 2 } ? t.Punkt[1] : 0, t.SchliesstNachts)
        {
            Vorlage = torVorlagen is not null && i < torVorlagen.Count ? torVorlagen[i]?.DeepClone() as JsonObject : null,
        }));
        return e;
    }

    internal override EntwurfTeil KopieTeil()
    {
        var k = new EntwurfMauer { Name = Name, Katalog = Katalog, Geschlossen = Geschlossen, Vorlage = Vorlage };
        k.EckenSetzen(_ecken);
        k.ToreSetzen(_tore);
        return k;
    }

    internal override void UebernimmTeil(EntwurfTeil werte)
    {
        var o = (EntwurfMauer)werte;
        (Name, Katalog, Geschlossen, Vorlage) = (o.Name, o.Katalog, o.Geschlossen, o.Vorlage);
        EckenSetzen(o._ecken);
        ToreSetzen(o._tore);
    }

    internal override bool GleicheWerte(EntwurfTeil anderer) => anderer is EntwurfMauer o && Name == o.Name && Katalog == o.Katalog
        && Geschlossen == o.Geschlossen && _ecken.SequenceEqual(o._ecken) && _tore.Count == o._tore.Count && _tore.SequenceEqual(o._tore);

    internal override JsonObject AlsJson() => Zusammensetzen(Vorlage,
        ("name", Name), ("linie", JsonPunkte(_ecken)), ("geschlossen", Geschlossen), ("katalog", Katalog),
        ("tore", new JsonArray(_tore.Select(t => (JsonNode)t.AlsJson()).ToArray())));
}

/// <summary>Wo ein Punkt eine Mauer trifft: welche, welche Strecke, der Punkt auf der Linie und der Abstand.</summary>
public readonly record struct Mauerfang(EntwurfMauer Mauer, int Strecke, Kartenort Ort, double Abstand);

public sealed partial class Kartenentwurf
{
    /// <summary>So nah (Meter) muss ein Stützpunkt eines Weges an der Mitte eines Tores liegen, damit er in sie einrastet.</summary>
    public const double Torfang = 4;

    /// <summary>Die Mauern in der Reihenfolge der Datei (die erste geschlossene umgibt die Stadt).</summary>
    public IReadOnlyList<EntwurfMauer> Mauern => _mauern;

    /// <summary>Die Mauer, deren Linie dem Punkt am nächsten ist – null, wenn keine näher als <paramref name="radius"/> liegt (Klick auf die Mauer).</summary>
    public Mauerfang? MauerBei(double x, double z, double radius = 3)
    {
        Mauerfang? best = null;
        foreach (var m in _mauern)
            if (m.NaechsteStelle(new Kartenort(x, z)) is { Strecke: >= 0 } s && s.Abstand <= radius && (best is null || s.Abstand < best.Value.Abstand))
                best = new Mauerfang(m, s.Strecke, s.Ort, s.Abstand);
        return best;
    }

    /// <summary>Ein Tor, dessen Mitte näher als <see cref="Torfang"/> am Punkt liegt (das nächste) – null, wenn keins.</summary>
    public (EntwurfMauer Mauer, int Tor, double Abstand)? TorBei(double x, double z, double radius = Torfang)
    {
        (EntwurfMauer, int, double)? best = null;
        var p = new Kartenort(x, z);
        foreach (var m in _mauern)
            for (var i = 0; i < m.Tore.Count; i++)
                if (p.Abstand(m.Tore[i].Punkt) is var d && d <= radius && (best is null || d < best.Value.Item3))
                    best = (m, i, d);
        return best;
    }

    // ── Mauern ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Legt eine Mauer an: Ecken (auf 5 cm; mindestens zwei verschiedene, geschlossen mindestens drei), offen oder geschlossen, Name (Vorgabe:
    /// „Stadtmauer“ für die erste, sonst „Mauer“ mit Nummer), Mauer-Baukasten. Noch ohne Tor (die Prüfung weist darauf hin).
    /// </summary>
    /// <exception cref="ArgumentException">Mit zu wenigen Ecken oder einem Baukasten, der keine Mauer des Katalogs ist.</exception>
    public EntwurfMauer MauerAnlegen(IReadOnlyList<Kartenort> ecken, bool geschlossen, string? name = null, string katalog = "stadtmauer")
    {
        if (Katalog.Finde(katalog) is not { Art: Bausteinart.Mauer })
            throw new ArgumentException($"„{katalog}“ ist kein Mauer-Baukasten des Katalogs.", nameof(katalog));
        var liste = OhneDoppelte(ecken.Select(Gerundet));
        if (geschlossen)
            while (liste.Count > 1 && liste[^1].Abstand(liste[0]) < 1e-6) liste.RemoveAt(liste.Count - 1);
        if (liste.Count < (geschlossen ? 3 : 2))
            throw new ArgumentException(geschlossen ? "Eine geschlossene Mauer braucht mindestens drei Ecken." : "Eine Mauer braucht mindestens zwei Ecken.", nameof(ecken));
        var m = new EntwurfMauer
        {
            Name = !string.IsNullOrWhiteSpace(name) ? name : _mauern.Count == 0 ? "Stadtmauer" : FreierName("Mauer", _mauern.Select(x => x.Name)),
            Katalog = katalog, Geschlossen = geschlossen,
        };
        m.EckenSetzen(liste);
        Geschichte.Ausfuehren(new TeilSetzen(m, _mauern.Count));
        return m;
    }

    /// <summary>Verschiebt eine Ecke; die Tore der beiden Strecken daneben bleiben an derselben Stelle ihrer Strecke (gleicher Anteil).</summary>
    public void MauerEckeVerschieben(EntwurfMauer m, int ecke, double x, double z)
    {
        Pruefen(m, ecke, m.Ecken.Count);
        var ort = Gerundet(new Kartenort(x, z));
        MauerAendern(m, "Ecke verschoben", n =>
        {
            var anteile = n.Tore.Select(t => n.NaechsteStelle(t.Punkt)).ToList();
            n.EckenSetzen(n.Ecken.Select((p, i) => i == ecke ? ort : p));
            n.ToreSetzen(n.Tore.Select((t, i) => AufStrecke(n, t, anteile[i].Strecke, anteile[i].Anteil)));
        });
    }

    /// <summary>Fügt eine Ecke an der Stelle <paramref name="index"/> ein (0 … Anzahl); die Tore kommen auf die nächste Stelle der neuen Linie.</summary>
    public void MauerEckeEinfuegen(EntwurfMauer m, int index, double x, double z)
    {
        Pruefen(m, index, m.Ecken.Count + 1);
        var ort = Gerundet(new Kartenort(x, z));
        MauerAendern(m, "Ecke eingefügt", n =>
        {
            n.EckenSetzen(n.Ecken.Take(index).Append(ort).Concat(n.Ecken.Skip(index)));
            ToreAufLinie(n);
        });
    }

    /// <summary>Fügt eine Ecke auf der nächsten Strecke ein (Klick auf die Mauer) und gibt ihren Index zurück (−1: dort ist schon eine Ecke).</summary>
    public int MauerEckeEinfuegenBei(EntwurfMauer m, double x, double z)
    {
        Pruefen(m, 0, 1);
        var (strecke, _, _, _) = m.NaechsteStelle(new Kartenort(x, z));
        var ort = Gerundet(new Kartenort(x, z));
        if (strecke < 0 || m.Ecken.Any(q => q.Abstand(ort) < 1e-3)) return -1;
        MauerEckeEinfuegen(m, strecke + 1, ort.X, ort.Z);
        return strecke + 1;
    }

    /// <summary>Löscht eine Ecke; false (kein Schritt), wenn die Mauer dann zu wenige hätte (offen zwei, geschlossen drei).</summary>
    public bool MauerEckeLoeschen(EntwurfMauer m, int ecke)
    {
        Pruefen(m, ecke, m.Ecken.Count);
        if (m.Ecken.Count <= (m.Geschlossen ? 3 : 2)) return false;
        MauerAendern(m, "Ecke gelöscht", n =>
        {
            n.EckenSetzen(n.Ecken.Where((_, i) => i != ecke));
            ToreAufLinie(n);
        });
        return true;
    }

    /// <summary>Verschiebt die ganze Mauer mit ihren Toren (und den Wegen durch die Tore, nur dort) um (dx, dz) Meter.</summary>
    public void MauerVerschieben(EntwurfMauer m, double dx, double dz) => MauerAendern(m, "verschoben", n =>
    {
        n.EckenSetzen(n.Ecken.Select(p => Gerundet(new Kartenort(p.X + dx, p.Z + dz))));
        n.ToreSetzen(n.Tore.Select(t => t with { X = Kartenraster.Punkt(t.X + dx), Z = Kartenraster.Punkt(t.Z + dz) }));
    });

    /// <summary>
    /// Schließt die Mauer zum Ring oder öffnet sie (die letzte Strecke fällt weg; ihre Tore kommen auf die nächste Stelle). False (kein
    /// Schritt), wenn sie mit weniger als drei Ecken geschlossen werden soll.
    /// </summary>
    public bool MauerGeschlossenSetzen(EntwurfMauer m, bool geschlossen)
    {
        Pruefen(m, 0, 1);
        if (geschlossen && m.Ecken.Count < 3) return false;
        MauerAendern(m, geschlossen ? "geschlossen" : "geöffnet", n =>
        {
            n.Geschlossen = geschlossen;
            ToreAufLinie(n);
        });
        return true;
    }

    public void MauerUmbenennen(EntwurfMauer m, string name) => Aendern(m, "umbenannt", n => n.Name = name);

    /// <summary>Wählt den Mauer-Baukasten (Kennung einer Mauer des Katalogs; die Prüfung meldet eine unbekannte).</summary>
    public void MauerKatalogSetzen(EntwurfMauer m, string katalog) => Aendern(m, "Baukasten geändert", n => n.Katalog = katalog);

    /// <summary>Löscht eine Mauer mit ihren Toren (Rückgängig setzt sie an dieselbe Stelle zurück).</summary>
    public void MauerLoeschen(EntwurfMauer m) => TeilEntfernen(m);

    // ── Tore ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Setzt ein Tor auf die Stelle der Mauer, die (x, z) am nächsten ist (auf 5 cm), und gibt seinen Index zurück. Name: Vorgabe „Tor“ mit
    /// Nummer (frei in der ganzen Karte).
    /// </summary>
    /// <exception cref="ArgumentException">Wenn die Mauer keine Strecke hat.</exception>
    public int TorSetzen(EntwurfMauer m, double x, double z, string? name = null, bool schliesstNachts = true)
    {
        Pruefen(m, 0, 1);
        if (m.Strecken == 0) throw new ArgumentException("Die Mauer hat keine Strecke für ein Tor.", nameof(m));
        var ort = Gerundet(m.NaechsteStelle(new Kartenort(x, z)).Ort);
        var tor = new EntwurfTor(!string.IsNullOrWhiteSpace(name) ? name : FreierName("Tor", _mauern.SelectMany(n => n.Tore).Select(t => t.Name)), ort.X, ort.Z, schliesstNachts);
        Aendern(m, $"{tor.Name} gesetzt", n => n.ToreSetzen(n.Tore.Append(tor)));
        return m.Tore.Count - 1;
    }

    /// <summary>Verschiebt ein Tor entlang der Mauer: auf die Stelle der Linie, die (x, z) am nächsten ist (ein Weg durch das Tor folgt).</summary>
    public void TorVerschieben(EntwurfMauer m, int tor, double x, double z)
    {
        Pruefen(m, tor, m.Tore.Count);
        var ort = Gerundet(m.NaechsteStelle(new Kartenort(x, z)).Ort);
        TorAendern(m, tor, "verschoben", t => t with { X = ort.X, Z = ort.Z });
    }

    public void TorUmbenennen(EntwurfMauer m, int tor, string name) => TorAendern(m, tor, "umbenannt", t => t with { Name = name });

    /// <summary>Schließt das Tor nachts (Gauner kommen dann nicht hindurch) oder bleibt es offen?</summary>
    public void TorSchliesstNachtsSetzen(EntwurfMauer m, int tor, bool schliesstNachts) =>
        TorAendern(m, tor, schliesstNachts ? "schließt nachts" : "bleibt nachts offen", t => t with { SchliesstNachts = schliesstNachts });

    public void TorLoeschen(EntwurfMauer m, int tor)
    {
        Pruefen(m, tor, m.Tore.Count);
        Aendern(m, $"{m.Tore[tor].Name} gelöscht", n => n.ToreSetzen(n.Tore.Where((_, i) => i != tor)));
    }

    private void TorAendern(EntwurfMauer m, int tor, string was, Func<EntwurfTor, EntwurfTor> aenderung)
    {
        Pruefen(m, tor, m.Tore.Count);
        MauerAendern(m, was, n => n.ToreSetzen(n.Tore.Select((t, i) => i == tor ? aenderung(t) : t)), $"{m.Tore[tor].Name} {was}");
    }

    /// <summary>
    /// Ändert eine Mauer über eine Kopie (ein Schritt „{Mauer} {was}“ bzw. <paramref name="beschreibung"/>). Wandert dabei ein Tor, folgt
    /// jeder Stützpunkt eines Weges, der in seiner Mitte lag (auf 1 mm), im selben Schritt. Ändert sich nichts, entsteht kein Schritt.
    /// </summary>
    private void MauerAendern(EntwurfMauer m, string was, Action<EntwurfMauer> aenderung, string? beschreibung = null)
    {
        if (Aenderung(m, aenderung) is not { } eigener) return;
        var vorher = m.Tore;
        var nachher = ((EntwurfMauer)eigener.Nachher).Tore;
        var wege = new Dictionary<EntwurfWeg, EntwurfWeg>(ReferenceEqualityComparer.Instance);
        // Die Befehle hier ändern die Zahl der Tore nicht; ihre Reihenfolge bleibt.
        for (var i = 0; i < Math.Min(vorher.Count, nachher.Count); i++)
        {
            var (alt, neu) = (vorher[i].Punkt, nachher[i].Punkt);
            if (alt.Abstand(neu) < 1e-6) continue;
            foreach (var w in _wege.Where(w => w.Punkte.Any(p => p.Abstand(alt) < 1e-3)))
            {
                if (!wege.TryGetValue(w, out var kopie)) wege[w] = kopie = (EntwurfWeg)w.KopieTeil();
                kopie.PunkteSetzen(kopie.Punkte.Select(p => p.Abstand(alt) < 1e-3 ? neu : p).ToList());
            }
        }
        var weitere = wege.Select(x => (IKartenbefehl)new TeilAendern($"{x.Key.Bezeichnung}: Weg im Tor", x.Key, x.Key.KopieTeil(), x.Value));
        Buendeln(beschreibung ?? $"{m.Bezeichnung} {was}", eigener, weitere);
    }

    /// <summary>Das Tor an derselben Stelle (Anteil) seiner Strecke – gibt es die Strecke nicht mehr, auf die nächste Stelle der Linie.</summary>
    private static EntwurfTor AufStrecke(EntwurfMauer m, EntwurfTor t, int strecke, double anteil)
    {
        if (strecke < 0 || strecke >= m.Strecken) return AufLinie(m, t);
        var (a, b) = m.Strecke(strecke);
        var ort = Gerundet(new Kartenort(a.X + (b.X - a.X) * anteil, a.Z + (b.Z - a.Z) * anteil));
        return t with { X = ort.X, Z = ort.Z };
    }

    private static EntwurfTor AufLinie(EntwurfMauer m, EntwurfTor t)
    {
        if (m.Strecken == 0) return t;
        var ort = Gerundet(m.NaechsteStelle(t.Punkt).Ort);
        return t with { X = ort.X, Z = ort.Z };
    }

    private static void ToreAufLinie(EntwurfMauer m) => m.ToreSetzen(m.Tore.Select(t => AufLinie(m, t)).ToList());
}
