using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Gauner;

// ─────────────────────────────────────────────────────────────────────────────
//  Daten (Abschnitt „dieb" in wirtschaft.json, ab Datenversion 18)
// ─────────────────────────────────────────────────────────────────────────────

public sealed class TaschendiebDaten
{
    /// <summary>Gulden je Arbeitsminute eines Gesellen am Markt (ein Lehrling schafft weniger).</summary>
    public double BeuteJeMinute { get; init; } = 0.8;

    /// <summary>Faktor je Gebäudestufe (Schmugglerloch, Unterschlupf, Diebesgilde) – geübtere Leute, bessere Plätze.</summary>
    public List<double> BeuteJeStufe { get; init; } = new() { 1, 1.1, 1.3 };

    /// <summary>Chance je Diebesstunde (ein Geselle eine Stunde am Markt), dass einer erwischt wird.</summary>
    public double FangJeStunde { get; init; } = 0.012;

    /// <summary>Faktor auf die Fangchance je Gebäudestufe – in der Gilde lernt man, nicht erwischt zu werden.</summary>
    public List<double> FangJeStufe { get; init; } = new() { 1, 0.8, 0.6 };
}

public sealed class EinbruchDaten
{
    public int AbStufe { get; init; } = 1;

    /// <summary>So viel höher ist die Chance als bei einer angeheuerten Bande.</summary>
    public double ChanceBonus { get; init; } = 0.1;

    /// <summary>Je Stufe über 1 tragen die eigenen Leute so viele Stück mehr fort.</summary>
    public int StueckJeStufe { get; init; } = 6;

    /// <summary>Faktor auf die Zeugenchance – eigene Leute sind geschickter als angeheuerte.</summary>
    public double ZeugenFaktor { get; init; } = 0.75;
}

public sealed class AuskundschaftenDaten
{
    public int AbStufe { get; init; } = 1;

    /// <summary>Chance je unbekannter Tat des Hauses, dass die Diebe einen Beweis finden.</summary>
    public double Chance { get; init; } = 0.4;
}

public sealed class SchutzgeldDaten
{
    public int AbStufe { get; init; } = 2;

    /// <summary>Wer nicht zahlt, dem legen die Diebe Feuer: Chance wie bei einer Sabotage plus diesen Bonus.</summary>
    public double SabotageBonus { get; init; } = 0.1;
}

public sealed class EntfuehrungDaten
{
    public int AbStufe { get; init; } = 3;
    public double Chance { get; init; } = 0.55;
}

/// <summary>Der Dieb: Taschendiebe am Markt, Einbruch, Auskundschaften, Schutzgeld und Entführung.</summary>
public sealed class DiebDaten
{
    public string Beruf { get; init; } = "Dieb";

    /// <summary>So viel Ansehen verliert ein Haus, solange es einen Betrieb des Diebes führt (ehrbare Bürger meiden es).</summary>
    public double Ansehen { get; init; } = 3;

    /// <summary>So viele eigene Aufträge (Einbruch, Auskundschaften, Entführung) je Nacht – nach Stufe.</summary>
    public List<int> AuftraegeJeStufe { get; init; } = new() { 1, 2, 3 };

    public TaschendiebDaten Taschendiebstahl { get; init; } = new();
    public EinbruchDaten Einbruch { get; init; } = new();
    public AuskundschaftenDaten Auskundschaften { get; init; } = new();
    public SchutzgeldDaten Schutzgeld { get; init; } = new();
    public EntfuehrungDaten Entfuehrung { get; init; } = new();

    public void Pruefen(List<string> f, IReadOnlyDictionary<string, BerufDaten> berufe)
    {
        if (berufe.Count > 0 && !berufe.ContainsKey(Beruf)) f.Add($"dieb.beruf: Den Beruf „{Beruf}“ gibt es nicht.");
        var t = Taschendiebstahl;
        if (t.BeuteJeMinute < 0 || Ansehen < 0) f.Add("dieb: beuteJeMinute und ansehen dürfen nicht negativ sein.");
        if (t.BeuteJeStufe.Count < 3 || t.FangJeStufe.Count < 3 || AuftraegeJeStufe.Count < 3)
            f.Add("dieb: beuteJeStufe, fangJeStufe und auftraegeJeStufe brauchen je einen Wert für Stufe 1, 2 und 3.");
        if (t.BeuteJeStufe.Concat(t.FangJeStufe).Any(x => x < 0) || AuftraegeJeStufe.Any(x => x < 0))
            f.Add("dieb: Faktoren und Aufträge je Stufe dürfen nicht negativ sein.");
        foreach (var (name, wert) in new[] { ("taschendiebstahl.fangJeStunde", t.FangJeStunde), ("einbruch.chanceBonus", Einbruch.ChanceBonus),
                     ("einbruch.zeugenFaktor", Einbruch.ZeugenFaktor), ("auskundschaften.chance", Auskundschaften.Chance),
                     ("schutzgeld.sabotageBonus", Schutzgeld.SabotageBonus), ("entfuehrung.chance", Entfuehrung.Chance) })
            if (wert is < 0 or > 1) f.Add($"dieb.{name} muss zwischen 0 und 1 liegen.");
        foreach (var (name, stufe) in new[] { ("einbruch", Einbruch.AbStufe), ("auskundschaften", Auskundschaften.AbStufe),
                     ("schutzgeld", Schutzgeld.AbStufe), ("entfuehrung", Entfuehrung.AbStufe) })
            if (stufe is < 1 or > 3) f.Add($"dieb.{name}.abStufe muss zwischen 1 und 3 liegen.");
        if (Einbruch.StueckJeStufe < 0) f.Add("dieb.einbruch.stueckJeStufe darf nicht negativ sein.");
    }
}

/// <summary>Was die eigenen Diebe nachts tun.</summary>
public enum Diebesstueck
{
    /// <summary>In ein fremdes Lager einsteigen – die Beute kommt ins eigene Lager (der Rest zum Hehler).</summary>
    Einbruch,
    /// <summary>Ein Haus ausspähen: Beweise für seine Taten.</summary>
    Auskundschaften,
    /// <summary>Einen Angehörigen verschleppen und Lösegeld fordern.</summary>
    Entfuehrung,
    /// <summary>Strafe für verweigertes Schutzgeld: Feuer im wertvollsten Betrieb.</summary>
    Brandstiftung,
}

public enum Diebesbriefart
{
    Schutzgeld,
    Loesegeld,
}

public static class DiebesTexte
{
    public static string Name(this Diebesstueck s) => s switch
    {
        Diebesstueck.Entfuehrung => "Entführung",
        _ => s.ToString(),
    };

    public static string Name(this Diebesbriefart a) => a == Diebesbriefart.Loesegeld ? "Lösegeld" : "Schutzgeld";
}

/// <summary>Ein Auftrag an die eigenen Diebe für die nächste Nacht.</summary>
public sealed record Diebesauftrag(Konto Dieb, Konto Ziel, Diebesstueck Art, string? Betrieb, int? Person, double Betrag);

/// <summary>Eine Forderung der Diebe: Schutzgeld (sonst brennt es) oder Lösegeld für eine Geisel (sonst wird sie verkauft).</summary>
public sealed record Diebesbrief(Diebesbriefart Art, Konto Dieb, Konto Opfer, double Betrag, int Jahr, int? Geisel);

/// <summary>Was die Taschendiebe eines Hauses in einem Jahr gemacht haben.</summary>
public sealed record Diebesbuch(double Beute, int Faenge);

// ─────────────────────────────────────────────────────────────────────────────
//  Teilsystem
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Die Diebe der Stadt – ein Beruf ohne Ware:
///   • Tagsüber (Arbeitszeit) ziehen die Leute im Schmugglerloch als Taschendiebe über den Markt. Jede volle Stunde
///     bringen sie Gulden (steuerfrei); mit etwas Pech wird einer erwischt: Er landet im Turm (der Platz wird frei),
///     und ein zufälliges anderes Haus erfährt, wem er diente (Spur mit einem Beweis für dieses Haus).
///   • Nachts (20 Uhr, nach den Banden aus dem Hinterzimmer) führen sie eigene Aufträge aus – je Stufe mehr:
///     Einbruch (besser als eine Bande, Beute ins eigene Lager), Auskundschaften (Beweise), ab Stufe 3 Entführung.
///   • Ab Stufe 2 verlangen sie Schutzgeld. Wer nicht zahlt, dem brennt nachts ein Betrieb.
///   • Lösegeld: Zahlt das Haus nicht bis zum Ende des Folgejahres, wird die Geisel in die Fremde verkauft.
///   • Ehrbare Bürger meiden ein Haus mit Dieben: weniger Ansehen.
/// Eigener Zufall (×47+29) – Wirtschaft, Gericht und Hinterzimmer bleiben unberührt.
/// </summary>
public sealed class Diebeswesen
{
    private readonly Stadtwirtschaft _w;
    private readonly DiebDaten? _d;
    private Zufallsquelle _zufall;
    private readonly List<Diebesauftrag> _auftraege = new();
    private readonly List<Diebesbrief> _briefe = new();
    private readonly Dictionary<Konto, Diebesbuch> _diesesJahr = new();
    private readonly Dictionary<Konto, Diebesbuch> _vorjahr = new();

    public Diebeswesen(Stadtwirtschaft w, DiebDaten? daten, int zufallsstartwert)
    {
        _w = w;
        _d = daten;
        _zufall = new Zufallsquelle(unchecked(zufallsstartwert * 47 + 29));
    }

    public bool IstAktiv => _d is not null;
    public DiebDaten Daten => _d ?? throw new InvalidOperationException("Es gibt keine Diebe (dieb fehlt in den Daten).");

    public IReadOnlyList<Diebesauftrag> Auftraege => _auftraege;
    public IReadOnlyList<Diebesbrief> Briefe => _briefe;

    public Diebesbuch DiesesJahr(Konto k) => _diesesJahr.GetValueOrDefault(k, new Diebesbuch(0, 0));
    public Diebesbuch? Vorjahr(Konto k) => _vorjahr.GetValueOrDefault(k);

    // ── Abfragen ────────────────────────────────────────────────────────────

    /// <summary>Ein Betrieb des Diebes (Schmugglerloch, Unterschlupf, Diebesgilde).</summary>
    public bool IstDiebesbetrieb(Betrieb b) => IstAktiv && b.OhneProduktion && b.Artdaten.Beruf == Daten.Beruf;

    /// <summary>Der beste eigene Diebesbetrieb mit Leuten (höchste Stufe) – oder null.</summary>
    public Betrieb? Versteck(Konto k) => _w.Betriebe
        .Where(b => b.Konto == k && IstDiebesbetrieb(b) && b.Belegt > 0)
        .OrderByDescending(b => b.Stufe).ThenBy(b => b.Name, StringComparer.Ordinal)
        .FirstOrDefault();

    /// <summary>Gulden je Arbeitsminute eines Gesellen am Markt (für Kalkulation, Personal und KI).</summary>
    public double WertJeMinute(int stufe) =>
        IstAktiv ? Daten.Taschendiebstahl.BeuteJeMinute * Faktor(Daten.Taschendiebstahl.BeuteJeStufe, stufe) : 0;

    private static double Faktor(List<double> jeStufe, int stufe) => jeStufe.Count == 0 ? 1 : jeStufe[Math.Clamp(stufe - 1, 0, jeStufe.Count - 1)];

    /// <summary>Chance je Diebesstunde, erwischt zu werden – „Bei Nacht und Nebel" des Oberhaupts hilft.</summary>
    public double Fangchance(Konto k, int stufe) =>
        Daten.Taschendiebstahl.FangJeStunde * Faktor(Daten.Taschendiebstahl.FangJeStufe, stufe) * _w.Familien.ZeugenFaktor(k);

    /// <summary>Ansehensverlust für ein Haus, das einen Betrieb des Diebes führt.</summary>
    public double AnsehenAbzug(Konto k) => IstAktiv && _w.Betriebe.Any(b => b.Konto == k && IstDiebesbetrieb(b)) ? Daten.Ansehen : 0;

    /// <summary>Sitzt diese Person als Geisel bei Dieben?</summary>
    public bool IstGeisel(Dynastie.Person p) => _briefe.Any(b => b.Art == Diebesbriefart.Loesegeld && b.Geisel == p.Nummer);

    /// <summary>Wer in diesem Haus entführt werden kann: alle lebenden Angehörigen außer dem Oberhaupt, die nicht schon Geisel sind.</summary>
    public IEnumerable<Dynastie.Person> Entfuehrbar(Konto ziel) =>
        _w.Familien.Von(ziel) is { } f ? f.Lebende.Where(p => p != f.Oberhaupt && !IstGeisel(p) && !_auftraege.Any(a => a.Person == p.Nummer)) : Enumerable.Empty<Dynastie.Person>();

    public Diebesbrief? BriefAn(Konto opfer, Konto dieb, Diebesbriefart art) =>
        _briefe.FirstOrDefault(b => b.Opfer == opfer && b.Dieb == dieb && b.Art == art);

    /// <summary>Wie viele eigene Aufträge dieses Haus heute Nacht noch vergeben kann.</summary>
    public int FreieAuftraege(Konto k) =>
        Versteck(k) is { } v ? Math.Max(0, Daten.AuftraegeJeStufe[Math.Clamp(v.Stufe - 1, 0, Daten.AuftraegeJeStufe.Count - 1)]
                                            - _auftraege.Count(a => a.Dieb == k && a.Art != Diebesstueck.Brandstiftung)) : 0;

    /// <summary>Warum dieses Haus das jetzt nicht kann (null = es geht).</summary>
    public string? Verboten(Konto k, Diebesstueck art)
    {
        if (!IstAktiv) return "In dieser Stadt gibt es keine Diebe.";
        if (!_w.Gauner.IstAktiv || !_w.Justiz.IstAktiv) return "Ohne Unterwelt und Gericht gibt es nichts zu holen.";
        if (Versteck(k) is not { } v) return "Dafür braucht ihr ein Schmugglerloch mit eigenen Leuten.";
        var ab = art switch
        {
            Diebesstueck.Einbruch => Daten.Einbruch.AbStufe,
            Diebesstueck.Auskundschaften => Daten.Auskundschaften.AbStufe,
            _ => Daten.Entfuehrung.AbStufe,
        };
        if (v.Stufe < ab) return $"{art.Name()} geht erst ab Stufe {ab} ({StufenName(ab)}).";
        if (FreieAuftraege(k) <= 0) return $"Eure Leute haben heute Nacht schon genug zu tun ({v.Artdaten.Name}: {Daten.AuftraegeJeStufe[v.Stufe - 1]} je Nacht).";
        return null;
    }

    private string StufenName(int stufe) =>
        _w.Daten.Betriebsarten.Values.FirstOrDefault(a => a.Beruf == Daten.Beruf && a.Stufe == stufe)?.Name ?? $"Stufe {stufe}";

    /// <summary>Was die eigenen Leute bei einem Einbruch tragen können.</summary>
    public int EinbruchStueck(Konto k) =>
        _w.Gauner.Daten.Diebstahl.HoechstensStueck + Daten.Einbruch.StueckJeStufe * Math.Max(0, (Versteck(k)?.Stufe ?? 1) - 1);

    /// <summary>Erfolgschance eines eigenen Einbruchs – besser als eine Bande, Wächter halbieren sie trotzdem.</summary>
    public double Einbruchchance(Betrieb b, Konto dieb)
    {
        var g = _w.Gauner.Daten;
        var schutz = b.Waechter ? g.WaechterSchutz : 1;
        return Math.Min(0.95, (g.Diebstahl.Chance + Daten.Einbruch.ChanceBonus) * schutz + _w.Familien.NachtBonus(dieb)) * _w.Wache.Patrouillefaktor(b);
    }

    /// <summary>Die wertvollste Ware im Lager – so viel, wie die eigenen Leute tragen.</summary>
    public List<(string Ware, int Stueck, double Wert)> MoeglicheBeute(Betrieb b, Konto dieb)
    {
        var rest = EinbruchStueck(dieb);
        var beute = new List<(string, int, double)>();
        foreach (var (ware, stueck) in b.Lager.Where(l => l.Value > 0)
                     .OrderByDescending(l => _w.Markt[l.Key].Preis).ThenBy(l => l.Key, StringComparer.Ordinal))
        {
            if (rest <= 0) break;
            var n = Math.Min(rest, stueck);
            beute.Add((ware, n, n * _w.Markt[ware].Preis));
            rest -= n;
        }
        return beute;
    }

    /// <summary>Was ein eigener Einbruch im Mittel bringt: Warenwert × Chance (verkauft am Markt, nicht beim Hehler).</summary>
    public double ErwarteterEinbruch(Betrieb b, Konto dieb) => MoeglicheBeute(b, dieb).Sum(x => x.Wert) * Einbruchchance(b, dieb);

    /// <summary>Der Betrieb, in dem ein Feuer das Opfer am meisten kostet.</summary>
    public Betrieb? Brandziel(Konto opfer) => _w.Betriebe.Where(b => b.Konto == opfer)
        .OrderByDescending(b => _w.Gauner.Sabotageschaden(b)).ThenBy(b => b.Name, StringComparer.Ordinal).FirstOrDefault();

    public double Brandchance(Betrieb b, Konto dieb)
    {
        var g = _w.Gauner.Daten;
        var schutz = b.Waechter ? g.WaechterSchutz : 1;
        return Math.Min(0.95, (g.Sabotage.Chance + Daten.Schutzgeld.SabotageBonus) * schutz + _w.Familien.NachtBonus(dieb)) * _w.Wache.Patrouillefaktor(b);
    }

    /// <summary>Was ein verweigertes Schutzgeld das Opfer im Mittel kostet (für die Abwägung der KI und die Anzeige).</summary>
    public double DrohenderBrand(Konto opfer, Konto dieb) =>
        Brandziel(opfer) is { } b ? Brandchance(b, dieb) * _w.Gauner.Sabotageschaden(b) : 0;

    public double Entfuehrungschance(Konto dieb) => Math.Min(0.95, Daten.Entfuehrung.Chance + _w.Familien.NachtBonus(dieb)) * _w.Wache.Patrouillefaktor(null);

    // ── Taschendiebe (jede volle Stunde) ────────────────────────────────────

    internal void Stunde(Spielzeit zeit)
    {
        if (!IstAktiv) return;
        // Bürger-KI: Wie viele Einwohner diese Stunde draußen unterwegs waren, verschiebt die Beute (Faktor um 1).
        // Ohne Kopplung (Abschnitt „buerger" oder „kopplung.diebe" fehlt) ist der Faktor genau 1.
        var passanten = _w.Einwohnerschaft.Diebesfaktor(zeit.StundeDieEndete);
        foreach (var b in _w.Betriebe.Where(IstDiebesbetrieb).ToList())
        {
            var minuten = b.ArbeitsminutenAbholen();
            if (minuten <= 0 || !b.Konto.IstAktiv) continue;
            var k = b.Konto;
            var beute = minuten * WertJeMinute(b.Stufe);
            if (passanten != 1) beute *= passanten;
            b.UmsatzBuchen(beute); // gestohlenes Geld zahlt keine Steuer
            var buch = DiesesJahr(k);
            _diesesJahr[k] = buch with { Beute = buch.Beute + beute };

            var fang = 1 - Math.Pow(1 - Math.Min(1, Fangchance(k, b.Stufe)), minuten / 60);
            if (_zufall.NextDouble() >= fang) continue;

            // Einer wird erwischt: Er kommt in den Turm – und ein Bürger, der ihn erkannt hat, erzählt einem Haus, wem er dient.
            var kraft = b.Belegung.ContainsKey("Lehrling") ? "Lehrling"
                : b.Belegung.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).First().Key;
            b.Einstellen(kraft, b.Belegung[kraft] - 1, _w.Statut);
            _diesesJahr[k] = DiesesJahr(k) with { Faenge = DiesesJahr(k).Faenge + 1 };
            // Gibt es eine Wache, hat sie ihn gefasst (Kopfgeld) – sonst erkennt ihn ein Bürger und erzählt es einem Haus.
            var wache = _w.Wache.TaschendiebGefasst(k);
            var zeugen = _w.Konten.Where(x => x != k && x.IstAktiv).ToList();
            var wisser = wache ?? (zeugen.Count > 0 ? zeugen[_zufall.Next(zeugen.Count)] : null);
            // Bürger-KI: Mit Kopplung ist der Bestohlene ein echter Einwohner (sonst ein namenloser „Bürger am Markt").
            var opfer = _w.Einwohnerschaft.Bestohlen();
            var opferName = opfer is null ? "Bürger am Markt" : $"{opfer.Name} ({_w.Einwohnerschaft.RollenName(opfer)})";
            if (_w.Justiz.IstAktiv && _w.Justiz.TatMelden(k, Vergehen.Taschendiebstahl, opferName, beute) is { } spur && wisser is not null)
                _w.Justiz.BeweisGeben(wisser, spur);
            _w.Gauner.Melden(null, $"Am Markt wird ein Taschendieb erwischt – ein {kraft} aus {b.Name}" +
                                   (opfer is null ? "." : $", die Hand in der Tasche von {opferName}.") +
                                   (wache is not null ? $" Die Wache von {wache.Inhaber} hat ihn gefasst."
                                       : wisser is null ? "" : $" Ein Bürger hat ihn erkannt und erzählt es {wisser.Inhaber}."));
        }
    }

    // ── Befehle ─────────────────────────────────────────────────────────────

    internal Befehlsergebnis Einbrechen(Konto dieb, Konto ziel, string betriebName)
    {
        if (ziel == dieb || !ziel.IstAktiv) return Befehlsergebnis.Abgelehnt("In dieses Haus steigen eure Leute nicht ein.");
        if (Verboten(dieb, Diebesstueck.Einbruch) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        if (_w.Betriebe.FirstOrDefault(b => b.Name == betriebName) is not { } b || b.Konto != ziel)
            return Befehlsergebnis.Abgelehnt($"{ziel.Inhaber} hat keinen Betrieb „{betriebName}“.");
        if (_auftraege.Any(a => a.Dieb == dieb && a.Art == Diebesstueck.Einbruch && a.Betrieb == betriebName))
            return Befehlsergebnis.Abgelehnt($"In {betriebName} steigt heute Nacht schon jemand von euch ein.");
        _auftraege.Add(new Diebesauftrag(dieb, ziel, Diebesstueck.Einbruch, betriebName, null, 0));
        return Befehlsergebnis.Ok($"Heute Nacht um 20 Uhr steigen eure Leute in {betriebName} ein (Chance {Prozent(Einbruchchance(b, dieb))}, " +
                                  $"bis zu {EinbruchStueck(dieb)} Stück – die Beute kommt in euer Lager).");
    }

    internal Befehlsergebnis Auskundschaften(Konto dieb, Konto ziel)
    {
        if (ziel == dieb || !ziel.IstAktiv) return Befehlsergebnis.Abgelehnt("Dieses Haus müsst ihr nicht ausspähen.");
        if (Verboten(dieb, Diebesstueck.Auskundschaften) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        if (_auftraege.Any(a => a.Dieb == dieb && a.Ziel == ziel && a.Art == Diebesstueck.Auskundschaften))
            return Befehlsergebnis.Abgelehnt($"Eure Leute spähen {ziel.Inhaber} heute Nacht schon aus.");
        _auftraege.Add(new Diebesauftrag(dieb, ziel, Diebesstueck.Auskundschaften, null, null, 0));
        return Befehlsergebnis.Ok($"Heute Nacht schleichen eure Leute um das Haus {ziel.Inhaber} – je Tat {Prozent(Daten.Auskundschaften.Chance)} Chance auf einen Beweis.");
    }

    internal Befehlsergebnis Entfuehren(Konto dieb, Konto ziel, int person, double betrag)
    {
        if (ziel == dieb || !ziel.IstAktiv) return Befehlsergebnis.Abgelehnt("Aus diesem Haus entführt ihr niemanden.");
        if (Verboten(dieb, Diebesstueck.Entfuehrung) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        if (betrag <= 0 || double.IsNaN(betrag)) return Befehlsergebnis.Abgelehnt("Wie viel Lösegeld soll es denn sein?");
        if (Entfuehrbar(ziel).FirstOrDefault(p => p.Nummer == person) is not { } p)
            return Befehlsergebnis.Abgelehnt($"Diese Person kann man {ziel.Inhaber} nicht entführen (das Oberhaupt nie, eine Geisel nicht zweimal).");
        _auftraege.Add(new Diebesauftrag(dieb, ziel, Diebesstueck.Entfuehrung, null, person, betrag));
        return Befehlsergebnis.Ok($"Heute Nacht lauern eure Leute {p.Vorname} auf (Chance {Prozent(Entfuehrungschance(dieb))}). " +
                                  $"Gelingt es, verlangt ihr {Zahlenformat.Ganz(betrag)} {_w.Daten.Allgemein.Waehrung} Lösegeld. Ein schweres Vergehen – vor Gericht droht der Kerker.");
    }

    internal Befehlsergebnis SchutzgeldFordern(Konto dieb, Konto opfer, double betrag)
    {
        if (!IstAktiv) return Befehlsergebnis.Abgelehnt("In dieser Stadt gibt es keine Diebe.");
        if (opfer == dieb || !opfer.IstAktiv) return Befehlsergebnis.Abgelehnt("Von diesem Haus verlangt ihr kein Schutzgeld.");
        if (!_w.Gauner.IstAktiv || !_w.Justiz.IstAktiv) return Befehlsergebnis.Abgelehnt("Ohne Unterwelt und Gericht schreckt keine Drohung.");
        if (Versteck(dieb) is not { } v || v.Stufe < Daten.Schutzgeld.AbStufe)
            return Befehlsergebnis.Abgelehnt($"Schutzgeld verlangt erst, wer einen {StufenName(Daten.Schutzgeld.AbStufe)} mit eigenen Leuten hat.");
        if (betrag <= 0 || double.IsNaN(betrag)) return Befehlsergebnis.Abgelehnt("Wie viel Schutzgeld soll es denn sein?");
        if (BriefAn(opfer, dieb, Diebesbriefart.Schutzgeld) is not null)
            return Befehlsergebnis.Abgelehnt($"{opfer.Inhaber} hat eure Forderung schon – wartet die Antwort ab.");
        if (Brandziel(opfer) is null) return Befehlsergebnis.Abgelehnt($"{opfer.Inhaber} hat nichts, was brennen könnte.");
        _briefe.Add(new Diebesbrief(Diebesbriefart.Schutzgeld, dieb, opfer, betrag, _w.Jahr, null));
        // Wer zahlen soll, muss wissen, an wen – der Brief ist ein Beweis.
        if (_w.Justiz.TatMelden(dieb, Vergehen.Schutzgeld, opfer.Inhaber, betrag) is { } spur)
            _w.Justiz.BeweisGeben(opfer, spur);
        _w.Melden($"{opfer.Inhaber} erhält Besuch von Leuten aus {v.Name}: {Zahlenformat.Ganz(betrag)} {_w.Daten.Allgemein.Waehrung} Schutzgeld – sonst brennt es.");
        return Befehlsergebnis.Ok($"Eure Leute überbringen die Forderung. Zahlt {opfer.Inhaber} nicht, legen sie Feuer " +
                                  $"(erwarteter Schaden ≈ {Zahlenformat.Ganz(DrohenderBrand(opfer, dieb))}). Aber: Die Forderung ist ein Beweis gegen euch.");
    }

    internal Befehlsergebnis Beantworten(Konto opfer, Konto dieb, Diebesbriefart art, bool zahlen)
    {
        if (BriefAn(opfer, dieb, art) is not { } brief)
            return Befehlsergebnis.Abgelehnt($"Von {dieb.Inhaber} liegt keine Forderung über {art.Name()} vor.");
        var waehrung = _w.Daten.Allgemein.Waehrung;
        if (zahlen)
        {
            if (opfer.Vermoegen < brief.Betrag) return Befehlsergebnis.Abgelehnt($"So viel Geld hat {opfer.Inhaber} nicht.");
            opfer.Gaunerkosten(brief.Betrag);
            dieb.Gaunerkosten(-brief.Betrag);
            _briefe.Remove(brief);
            if (art == Diebesbriefart.Loesegeld)
            {
                var name = Geiselname(brief);
                _w.Gauner.Melden(null, $"{opfer.Inhaber} zahlt Lösegeld – {name} ist wieder frei.");
                return Befehlsergebnis.Ok($"Ihr zahlt {Zahlenformat.Ganz(brief.Betrag)} {waehrung}. {name} kehrt nach Hause zurück.");
            }
            _w.Gauner.Melden(dieb, $"{opfer.Inhaber} zahlt euch {Zahlenformat.Ganz(brief.Betrag)} {waehrung} Schutzgeld.");
            return Befehlsergebnis.Ok($"Ihr zahlt {Zahlenformat.Ganz(brief.Betrag)} {waehrung} Schutzgeld – {dieb.Inhaber} lässt euch in Ruhe.");
        }
        _briefe.Remove(brief);
        return Ablehnen(brief);
    }

    private Befehlsergebnis Ablehnen(Diebesbrief brief)
    {
        if (brief.Art == Diebesbriefart.Loesegeld)
        {
            var name = Geiselname(brief);
            if (Geisel(brief) is { } p)
                _w.Familien.Verschwinden(brief.Opfer, p, $"Kein Lösegeld für {p.Vorname}: Die Entführer von {brief.Dieb.Inhaber} haben {p.Vorname} in die Fremde verkauft.");
            _w.Gauner.Melden(null, $"{brief.Opfer.Inhaber} zahlt kein Lösegeld – {name} ist für immer verschwunden.");
            return Befehlsergebnis.Ok($"Ihr zahlt nicht. {name} wird in die Fremde verkauft – ihr seht {name} nie wieder. Den Brief habt ihr als Beweis.");
        }
        // Schutzgeld verweigert: nächste Nacht brennt es – wenn die Diebe noch Leute haben.
        if (Versteck(brief.Dieb) is not null && Brandziel(brief.Opfer) is { } ziel)
            _auftraege.Add(new Diebesauftrag(brief.Dieb, brief.Opfer, Diebesstueck.Brandstiftung, ziel.Name, null, 0));
        _w.Gauner.Melden(null, $"{brief.Opfer.Inhaber} weist die Schutzgeldforderung von {brief.Dieb.Inhaber} zurück.");
        return Befehlsergebnis.Ok($"Ihr zahlt nicht. Stellt Wächter auf – {brief.Dieb.Inhaber} wird sich rächen. Die Forderung habt ihr als Beweis.");
    }

    private Dynastie.Person? Geisel(Diebesbrief brief) =>
        brief.Geisel is int nr ? _w.Familien.Von(brief.Opfer)?.Lebende.FirstOrDefault(p => p.Nummer == nr) : null;

    private string Geiselname(Diebesbrief brief) => Geisel(brief)?.Vorname ?? "die Geisel";

    // ── Nacht (20 Uhr, nach den Banden) ─────────────────────────────────────

    internal void Nacht()
    {
        if (!IstAktiv || !_w.Gauner.IstAktiv || !_w.Justiz.IstAktiv) { _auftraege.Clear(); return; }
        foreach (var a in _auftraege.ToList())
        {
            if (!a.Dieb.IstAktiv || !a.Ziel.IstAktiv) continue;
            switch (a.Art)
            {
                case Diebesstueck.Einbruch: Einbruch(a); break;
                case Diebesstueck.Auskundschaften: Ausspaehen(a); break;
                case Diebesstueck.Entfuehrung: Verschleppen(a); break;
                default: Brand(a); break;
            }
        }
        _auftraege.Clear();
    }

    private void Einbruch(Diebesauftrag a)
    {
        if (_w.Betriebe.FirstOrDefault(x => x.Name == a.Betrieb) is not { } b || b.Konto != a.Ziel) return;
        var versteck = Versteck(a.Dieb);
        var erfolg = versteck is not null && _zufall.NextDouble() < Einbruchchance(b, a.Dieb);
        double wert = 0;
        string oeffentlich;
        if (erfolg)
        {
            var beute = MoeglicheBeute(b, a.Dieb);
            double hehler = 0;
            foreach (var (ware, stueck, _) in beute)
            {
                b.Auslagern(ware, stueck);
                // Ins eigene Lager, so weit Platz ist – der Karren verkauft es am Markt. Der Rest geht zum Hehler.
                var platz = Math.Max(0, versteck!.LagerKapazitaetJeWare - versteck.ImLager(ware));
                var behalten = Math.Min(platz, stueck);
                if (behalten > 0) versteck.Einlagern(ware, behalten);
                hehler += (stueck - behalten) * _w.Markt[ware].Preis * _w.Gauner.Daten.HehlerAnteil;
            }
            wert = beute.Sum(x => x.Wert);
            if (hehler > 0) a.Dieb.Gaunerkosten(-hehler);
            if (wert > 0)
            {
                a.Ziel.Raub(wert);
                oeffentlich = $"Einbruch in {b.Name}: {string.Join(" und ", beute.Select(x => $"{x.Stueck} {x.Ware}"))} gestohlen (Wert {Zahlenformat.Ganz(wert)}).";
                _w.Gauner.Melden(a.Dieb, $"Eure Leute bringen aus {b.Name} Ware für {Zahlenformat.Ganz(wert)} ins {versteck!.Artdaten.Name}" +
                                         (hehler > 0 ? $" – was nicht ins Lager passt, nimmt der Hehler für {Zahlenformat.Ganz(hehler)}." : "."));
            }
            else
                oeffentlich = $"Einbruch in {b.Name} – doch das Lager war leer.";
        }
        else
            oeffentlich = $"Einbruchsversuch in {b.Name} – die Diebe wurden verscheucht{(b.Waechter ? " (der Wächter war wachsam)" : "")}.";
        Spur(a, Vergehen.Diebstahl, b.Name, wert, b, erfolg, oeffentlich, Daten.Einbruch.ZeugenFaktor);
    }

    private void Ausspaehen(Diebesauftrag a)
    {
        var chance = Math.Min(0.95, Daten.Auskundschaften.Chance + _w.Familien.NachtBonus(a.Dieb));
        var gefunden = 0;
        foreach (var spur in _w.Justiz.SpurenVon(a.Ziel).OrderBy(s => s.Nummer).ToList())
        {
            if (_w.Justiz.KenntSpur(a.Dieb, spur)) continue;
            if (_zufall.NextDouble() < chance)
            {
                _w.Justiz.BeweisGeben(a.Dieb, spur);
                gefunden++;
            }
        }
        _w.Gauner.Melden(a.Dieb, gefunden > 0
            ? $"Eure Leute haben {a.Ziel.Inhaber} ausgespäht: {gefunden} {(gefunden == 1 ? "neuer Beweis" : "neue Beweise")}."
            : $"Eure Leute haben {a.Ziel.Inhaber} ausgespäht – ohne Neues zu finden.");
    }

    private void Verschleppen(Diebesauftrag a)
    {
        var waehrung = _w.Daten.Allgemein.Waehrung;
        var p = Entfuehrbar(a.Ziel).FirstOrDefault(x => x.Nummer == a.Person)
                ?? _w.Familien.Von(a.Ziel)?.Lebende.FirstOrDefault(x => x.Nummer == a.Person && x != _w.Familien.Von(a.Ziel)!.Oberhaupt && !IstGeisel(x));
        if (p is null || Versteck(a.Dieb) is null) return;
        var erfolg = _zufall.NextDouble() < Entfuehrungschance(a.Dieb);
        if (erfolg)
        {
            _briefe.Add(new Diebesbrief(Diebesbriefart.Loesegeld, a.Dieb, a.Ziel, a.Betrag, _w.Jahr, p.Nummer));
            // Der Lösegeldbrief verrät, an wen zu zahlen ist – er ist ein Beweis.
            if (_w.Justiz.TatMelden(a.Dieb, Vergehen.Entfuehrung, a.Ziel.Inhaber, a.Betrag) is { } spur)
                _w.Justiz.BeweisGeben(a.Ziel, spur);
            _w.Gauner.Melden(null, $"Entführung! {p.Vorname} aus dem Haus {a.Ziel.Inhaber} ist verschwunden. Ein Brief von {a.Dieb.Inhaber} " +
                                   $"verlangt {Zahlenformat.Ganz(a.Betrag)} {waehrung} Lösegeld bis Ende nächsten Jahres.");
            return;
        }
        Spur(a, Vergehen.Entfuehrung, a.Ziel.Inhaber, a.Betrag, null, false,
            $"Im Haus {a.Ziel.Inhaber} wurde ein Entführungsversuch vereitelt – {p.Vorname} konnte fliehen.");
    }

    private void Brand(Diebesauftrag a)
    {
        if (_w.Betriebe.FirstOrDefault(x => x.Name == a.Betrieb) is not { } b || b.Konto != a.Ziel) return;
        var g = _w.Gauner.Daten;
        var erfolg = _zufall.NextDouble() < Brandchance(b, a.Dieb);
        double schaden = 0;
        string oeffentlich;
        if (erfolg)
        {
            schaden = _w.Gauner.Sabotageschaden(b);
            b.Sabotieren(g.Sabotage.StillstandMinuten);
            b.SchadenBuchen(g.Sabotage.Reparatur);
            oeffentlich = $"Feuer in {b.Name}! Wer kein Schutzgeld zahlt … {g.Sabotage.StillstandMinuten / 60} Arbeitsstunden Stillstand, Reparatur {Zahlenformat.Ganz(g.Sabotage.Reparatur)}.";
        }
        else
            oeffentlich = $"Brandanschlag auf {b.Name} vereitelt{(b.Waechter ? " – der Wächter hat das Feuer bemerkt" : "")}.";
        Spur(a, Vergehen.Sabotage, b.Name, schaden, b, erfolg, oeffentlich, 1);
    }

    /// <summary>Spur fürs Gericht, öffentliche Nachricht – und vielleicht ein Zeuge.</summary>
    private void Spur(Diebesauftrag a, Vergehen vergehen, string betroffener, double betrag, Betrieb? b, bool erfolg, string oeffentlich, double zeugenFaktor = 1)
    {
        if (b is not null) _w.Gauner.AngriffMerken(b.Name);
        var spur = _w.Justiz.TatMelden(a.Dieb, vergehen, betroffener, betrag);
        var zeuge = _zufall.NextDouble() < _w.Gauner.Zeugenchance(b, erfolg, a.Dieb) * zeugenFaktor;
        if (zeuge && spur is not null)
        {
            _w.Justiz.BeweisGeben(a.Ziel, spur);
            oeffentlich += $" Ein Zeuge hat Leute von {a.Dieb.Inhaber} erkannt!";
        }
        _w.Gauner.Melden(null, oeffentlich);
    }

    // ── Jahresende ──────────────────────────────────────────────────────────

    /// <summary>Forderungen gelten bis zum Ende des Folgejahres – danach heißt Schweigen Nein. Neues Diebesbuch.</summary>
    internal void Jahresende()
    {
        if (!IstAktiv) return;
        foreach (var brief in _briefe.Where(b => b.Jahr < _w.Jahr - 1).ToList())
        {
            _briefe.Remove(brief);
            _w.Gauner.Melden(null, $"{brief.Opfer.Inhaber} hat auf die {brief.Art.Name()}forderung von {brief.Dieb.Inhaber} nicht geantwortet – das gilt als Nein.");
            Ablehnen(brief);
        }
        _vorjahr.Clear();
        foreach (var (k, buch) in _diesesJahr) _vorjahr[k] = buch;
        _diesesJahr.Clear();
    }

    private static string Prozent(double x) => $"{Zahlenformat.Ganz(x * 100)} %";

    // ── Speichern ───────────────────────────────────────────────────────────

    internal DiebeStand Sichern(Func<Konto, int> nr) => new()
    {
        ZufallWuerfe = _zufall.Wuerfe,
        Auftraege = _auftraege.Select(a => new DiebesauftragStand
        {
            Dieb = nr(a.Dieb), Ziel = nr(a.Ziel), Art = a.Art, Betrieb = a.Betrieb, Person = a.Person, Betrag = a.Betrag,
        }).ToList(),
        Briefe = _briefe.Select(b => new DiebesbriefStand
        {
            Art = b.Art, Dieb = nr(b.Dieb), Opfer = nr(b.Opfer), Betrag = b.Betrag, Jahr = b.Jahr, Geisel = b.Geisel,
        }).ToList(),
        DiesesJahr = _diesesJahr.OrderBy(x => nr(x.Key)).ToDictionary(x => nr(x.Key).ToString(), x => x.Value),
        Vorjahr = _vorjahr.OrderBy(x => nr(x.Key)).ToDictionary(x => nr(x.Key).ToString(), x => x.Value),
    };

    internal void Wiederherstellen(DiebeStand s, IReadOnlyList<Konto> k)
    {
        _zufall = new Zufallsquelle(_zufall.Startwert, s.ZufallWuerfe);
        _auftraege.Clear();
        _auftraege.AddRange(s.Auftraege.Select(a => new Diebesauftrag(k[a.Dieb], k[a.Ziel], a.Art, a.Betrieb, a.Person, a.Betrag)));
        _briefe.Clear();
        _briefe.AddRange(s.Briefe.Select(b => new Diebesbrief(b.Art, k[b.Dieb], k[b.Opfer], b.Betrag, b.Jahr, b.Geisel)));
        foreach (var (nr, buch) in s.DiesesJahr) _diesesJahr[k[int.Parse(nr)]] = buch;
        foreach (var (nr, buch) in s.Vorjahr) _vorjahr[k[int.Parse(nr)]] = buch;
    }
}
