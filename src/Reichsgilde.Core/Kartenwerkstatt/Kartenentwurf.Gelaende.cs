using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Das Gelände im Kartenentwurf (Kartenwerkstatt, Schritt 5b)
//
//  Das Höhenraster (Hoehenraster, Feld „hoehen“) ändern nur die Pinsel (Gelaendepinsel) und die Rampe – jeder Strich ist ein Schritt mit
//  Rückgängig, und der Schritt merkt sich nur die Kacheln (128 × 128 m), die der Strich berührt hat. Beim Ziehen ändert sich das Raster
//  sofort (die Darstellung zeichnet mit); erst „Beenden“ legt den Schritt in die Geschichte. Jede Änderung wird als Kartenteil „Gelände“
//  mit ihrem Rechteck gemeldet, damit die Darstellung nur dort neu zeichnet.
//  Größe und Rand der Karte ändern das Raster nicht: Es ist am Boden verankert. Geschrieben werden nur Kacheln mit Punkten in der Karte.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Das Gelände als Teil der Karte – nur für die Meldung <see cref="Kartenaenderung"/> (das Rechteck steht in ihrem Bereich).</summary>
public sealed class EntwurfGelaende : EntwurfTeil
{
    public override Kartenteil Teil => Kartenteil.Gelaende;
    public override string Bezeichnung => "Gelände";

    private static InvalidOperationException Kacheln() => new("Das Gelände ändert sich kachelweise (Kartenentwurf.Gelaende.cs), nicht als Teil.");
    internal override EntwurfTeil KopieTeil() => throw Kacheln();
    internal override void UebernimmTeil(EntwurfTeil werte) => throw Kacheln();
    internal override bool GleicheWerte(EntwurfTeil anderer) => ReferenceEquals(this, anderer);
    internal override JsonObject AlsJson() => throw Kacheln();
}

/// <summary>
/// Kacheln des Höhenrasters vorher und nachher – ein Pinselstrich oder eine Rampe als Schritt. Die Kacheln liegen gepackt im Speicher (wie in
/// der Datei, meist ein Achtel der 8 KB): 200 Schritte mit großen Pinseln sollen nicht Hunderte Megabyte halten.
/// </summary>
internal sealed class GelaendeAendern : IKartenbefehl
{
    private readonly string _beschreibung;
    private readonly List<((int I, int J) Kachel, string? Vorher, string? Nachher)> _kacheln;
    private readonly Kartenrechteck _bereich;

    public GelaendeAendern(string beschreibung, IEnumerable<((int I, int J) Kachel, short[]? Vorher, short[]? Nachher)> kacheln, Kartenrechteck bereich)
    {
        (_beschreibung, _bereich) = (beschreibung, bereich);
        _kacheln = kacheln.Select(k => (k.Kachel, Packen(k.Vorher), Packen(k.Nachher))).ToList();
    }

    public string Beschreibung => _beschreibung;
    public void Ausfuehren(Kartenentwurf entwurf) => entwurf.KachelnSetzen(_kacheln.Select(k => (k.Kachel, Entpacken(k.Nachher))), _bereich);
    public void Zuruecknehmen(Kartenentwurf entwurf) => entwurf.KachelnSetzen(_kacheln.Select(k => (k.Kachel, Entpacken(k.Vorher))), _bereich);

    private static string? Packen(short[]? w) => w is null ? null : Hoehenraster.Packen(w);
    private static short[]? Entpacken(string? text) => text is null ? null : Hoehenraster.Entpacken(text);
}

public sealed partial class Kartenentwurf
{
    /// <summary>Das gespeicherte Höhenraster (Feld <c>hoehen</c>). Nur lesen – ändern über die Pinsel.</summary>
    public Hoehenraster Hoehen => _hoehen;

    /// <summary>
    /// Das Gelände, wie die Darstellung es zeichnet: Höhe des Bodens mit dem Bett der Wege und Mauern, Profile, Fundamente, Hangneigung
    /// (<see cref="Bauen.Gelaendeform"/>). Folgt jeder Änderung von selbst.
    /// </summary>
    public Gelaendeform Gelaendeform => _gelaende;

    /// <summary>Der Teil, mit dem Änderungen am Gelände gemeldet werden.</summary>
    public EntwurfGelaende GelaendeTeil => _gelaendeTeil;

    /// <summary>Wege (Verlauf, Breite) und Mauern (Linie, 4 m), unter denen das Gelände eingeebnet wird.</summary>
    private IReadOnlyList<Bettlinie> Bettlinien() =>
        _wege.Where(w => w.Punkte.Count >= 2).Select(w => new Bettlinie(w.Verlauf, w.BreiteWirksam))
            .Concat(_mauern.Where(m => m.Ecken.Count >= 2).Select(m => new Bettlinie(m.Ecken, Gelaendeform.Mauerbett, m.Geschlossen))).ToList();

    // ── Pinsel ───────────────────────────────────────────────────────────────

    private Gelaendestrich? _strich;

    /// <summary>Läuft gerade ein Pinselstrich (zwischen <see cref="GelaendeStrichBeginnen"/> und <see cref="GelaendeStrichBeenden"/>)?</summary>
    public bool StrichLaeuft => _strich is not null;

    /// <summary>Beginnt einen Pinselstrich (ein laufender wird vorher beendet). Danach <see cref="GelaendeStrichWeiter"/> je Mausbewegung.</summary>
    public void GelaendeStrichBeginnen(Pinsel pinsel)
    {
        GelaendeStrichBeenden();
        _strich = Gelaendepinsel.Beginnen(_hoehen, pinsel, Kartengrenzen.Aussen);
    }

    /// <summary>
    /// Führt den Strich bis zu diesem Punkt weiter (der erste Punkt setzt den ersten Tupfer). Das Raster ändert sich sofort; gemeldet wird
    /// das geänderte Rechteck (Kartenteil Gelände). Gibt es zurück – null: nichts geändert.
    /// </summary>
    /// <exception cref="InvalidOperationException">Ohne <see cref="GelaendeStrichBeginnen"/>.</exception>
    public Kartenrechteck? GelaendeStrichWeiter(Kartenort p)
    {
        var strich = _strich ?? throw new InvalidOperationException("Es läuft kein Pinselstrich (erst GelaendeStrichBeginnen).");
        var bereich = strich.Weiter(p);
        if (bereich is { } b)
        {
            GelaendeGeaendert(b);
            MeldungenAusloesen();
        }
        return bereich;
    }

    /// <summary>Beendet den Strich: Er wird ein Schritt in der Geschichte (Rückgängig nimmt den ganzen Strich zurück). False: es lief keiner oder er änderte nichts.</summary>
    public bool GelaendeStrichBeenden()
    {
        if (_strich is not { } strich) return false;
        _strich = null;
        return Eintragen(PinselText(strich.Pinsel.Art), strich.Aenderung);
    }

    /// <summary>Ein ganzer Strich in einem Aufruf (Kartenskript, Tests): Beginnen, alle Punkte, Beenden – ein Schritt.</summary>
    public bool GelaendePinseln(Pinsel pinsel, IReadOnlyList<Kartenort> strich)
    {
        GelaendeStrichBeginnen(pinsel);
        foreach (var p in strich) GelaendeStrichWeiter(p);
        return GelaendeStrichBeenden();
    }

    /// <summary>
    /// Eine Rampe von <paramref name="von"/> nach <paramref name="nach"/>: ein Band von <paramref name="breite"/> Metern mit gleichmäßigem
    /// Gefälle von der Höhe unter dem ersten zur Höhe unter dem zweiten Punkt, die Seiten laufen über <paramref name="kante"/> Meter weich
    /// ins Gelände aus. Ein Schritt; false, wenn sich nichts ändert.
    /// </summary>
    public bool Rampe(Kartenort von, Kartenort nach, double breite, double kante = 4)
    {
        GelaendeStrichBeenden();
        return Eintragen("Rampe angelegt", Gelaendepinsel.Rampe(_hoehen, von, nach, breite, kante, Kartengrenzen.Aussen));
    }

    /// <summary>Eine Kachel, in der überall 0 steht (zum Vergleichen; nicht ändern).</summary>
    private static readonly short[] LeereKachel = new short[Hoehenraster.Kachel * Hoehenraster.Kachel];

    private static string PinselText(Pinselart art) => art switch
    {
        Pinselart.Heben => "Gelände gehoben",
        Pinselart.Senken => "Gelände gesenkt",
        Pinselart.Glaetten => "Gelände geglättet",
        Pinselart.Ebnen => "Gelände geebnet",
        _ => "Gelände aufgeraut",
    };

    /// <summary>Legt die Änderung als Schritt in die Geschichte (Kacheln, die gleich blieben, fallen weg).</summary>
    private bool Eintragen(string beschreibung, Gelaendeaenderung aenderung)
    {
        var kacheln = aenderung.Kacheln().Where(k => !Gleich(k.Vorher, k.Nachher)).ToList();
        if (kacheln.Count == 0 || aenderung.Bereich is not { } bereich) return false;
        Geschichte.Ausfuehren(new GelaendeAendern(beschreibung, kacheln, bereich));
        return true;

        static bool Gleich(short[]? a, short[]? b) => (a ?? LeereKachel).AsSpan().SequenceEqual(b ?? LeereKachel);
    }

    /// <summary>Setzt Kacheln des Rasters (für den Befehl) und meldet das Rechteck.</summary>
    internal void KachelnSetzen(IEnumerable<((int I, int J) Kachel, short[]? Werte)> kacheln, Kartenrechteck bereich)
    {
        foreach (var (k, w) in kacheln) _hoehen.KachelSetzen(k, w);
        GelaendeGeaendert(bereich);
    }

    private void GelaendeGeaendert(Kartenrechteck bereich)
    {
        _gelaende.Veraltet(bereich);
        Vormerken(new Kartenaenderung(Aenderungsart.Geaendert, _gelaendeTeil, bereich));
    }

    // ── Abfragen für die Darstellung ─────────────────────────────────────────

    /// <summary>Das Fundament eines Gebäudes (ganze Grundfläche mit Vorplatz): Höhe des Bodens im Haus und Tiefe des Sockels.</summary>
    public Fundament Fundament(EntwurfGebaeude g) => _gelaende.Fundament(g.Ecken());

    /// <summary>Das Höhenprofil eines Weges an den Punkten seines Verlaufs (<see cref="EntwurfWeg.Verlauf"/>).</summary>
    public double[] Profil(EntwurfWeg w) => _gelaende.Profil(w.Verlauf);

    /// <summary>Das Längsprofil eines Weges höchstens alle 1 m (<see cref="Gelaendeform.Profillinie"/>) – darauf liegt das Wegband.</summary>
    public IReadOnlyList<(Kartenort Ort, double Hoehe)> Profillinie(EntwurfWeg w) => _gelaende.Profillinie(w.Verlauf);

    /// <summary>Das Höhenprofil einer Mauer an ihren Ecken (ein Ring ohne den ersten Punkt am Schluss).</summary>
    public double[] Profil(EntwurfMauer m) => _gelaende.Profil(m.Ecken, m.Geschlossen);
}
