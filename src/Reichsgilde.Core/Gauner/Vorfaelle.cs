using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Gauner;

/// <summary>Was für ein Vorfall es war (für die Darstellung: welcher Ablauf gezeigt wird).</summary>
public enum Vorfallart
{
    /// <summary>Ein Taschendieb wird am Markt erwischt (von der Wache gefasst oder von einem Bürger erkannt). Einzelne Griffe stehen nicht hier, siehe <see cref="Diebeswesen.GriffeDieserStunde"/>.</summary>
    Taschendiebstahl,
    /// <summary>Einbruch ins Lager eines Betriebs (Bande aus dem Hinterzimmer oder eigene Diebe).</summary>
    Einbruch,
    /// <summary>Einbruch ins Wohnhaus (Bande aus dem Hinterzimmer).</summary>
    Hauseinbruch,
    /// <summary>Feuer in einem Betrieb (Sabotage der Bande oder Brand der Diebe nach verweigertem Schutzgeld).</summary>
    Brandanschlag,
    /// <summary>Feuer am Wohnhaus (Bande aus dem Hinterzimmer).</summary>
    Hausbrand,
    /// <summary>Krötenschleim in der Küche des Wohnhauses (Bande aus dem Hinterzimmer).</summary>
    Giftanschlag,
    /// <summary>Entführung eines Angehörigen aus dem Wohnhaus (eigene Diebe).</summary>
    Entfuehrung,
    /// <summary>Die eigenen Diebe spähen ein Haus aus (Erfolg = mindestens ein neuer Beweis).</summary>
    Auskundschaften,
    /// <summary>Überfall auf einen beladenen Karren im Umland (bestellter Hinterhalt oder Bande der Landstraße).</summary>
    Ueberfall,
}

/// <summary>Wo der Vorfall geschah.</summary>
public enum Vorfallort
{
    /// <summary>Am Markt der Stadt.</summary>
    Markt,
    /// <summary>An einem Betrieb (<see cref="Vorfall.Betrieb"/>).</summary>
    Betrieb,
    /// <summary>Am Wohnhaus des Opfers (<see cref="Vorfall.Opfer"/>).</summary>
    Wohnhaus,
    /// <summary>Auf der Landstraße, am Karren <see cref="Vorfall.Karren"/>.</summary>
    Landstrasse,
}

/// <summary>
/// Ein Vorfall der Gauner, wie der Kern ihn gewürfelt hat – nur zum Lesen (Darstellung, Meldungen). Er ändert nichts und wird nicht gespeichert.
/// Achtung: <see cref="Taeterhaus"/> ist die Wahrheit des Kerns. Was ein Haus davon wissen darf, sagen nur Beweise und Zeuge
/// (die Darstellung zeigt fremde Diebe ohne Haus).
/// </summary>
public sealed record Vorfall
{
    /// <summary>Spielminute (<see cref="Spielzeit.VergangeneMinuten"/>) – zum Vergleichen und Ordnen. Minute, Jahr und Uhrzeit setzt das <see cref="Vorfallbuch"/>.</summary>
    public long Minute { get; init; }

    /// <summary>Spieltag (= Jahr), zu dem der Vorfall gehört. Der Versuch um 23 Uhr gehört zum alten Tag, obwohl die Uhr schon auf dem neuen steht.</summary>
    public int Jahr { get; init; }

    /// <summary>Uhrzeit in Minuten nach Mitternacht (Nachtversuche 20:00 bis 23:00; Taschendiebe zum Ende der Stunde, in der sie erwischt wurden).</summary>
    public int Uhrzeit { get; init; }

    public required Vorfallart Art { get; init; }
    public required Vorfallort Ort { get; init; }

    /// <summary>Name des Ziels: Betrieb, „Wohnhaus von …“, Karren oder „Markt“.</summary>
    public required string Ziel { get; init; }

    /// <summary>Gelang die Tat? Beim Taschendieb immer false: Er wurde erwischt.</summary>
    public required bool Erfolg { get; init; }

    /// <summary>Beute bzw. Schaden in Gulden (Taschendieb: Beute der Stunde des Betriebs; 0 bei Misserfolg).</summary>
    public double Wert { get; init; }

    /// <summary>Der wievielte Versuch der Nacht (0 = 20 Uhr … 3 = 23 Uhr), sonst null.</summary>
    public int? Versuch { get; init; }

    // ── Täter ────────────────────────────────────────────────────────────────

    /// <summary>Das Haus hinter der Tat: Dieb bzw. Auftraggeber der Bande. Null bei einer Bande der Landstraße.</summary>
    public Konto? Taeterhaus { get; init; }

    /// <summary>Versteck des Täters (eigene Diebe), sonst null.</summary>
    public string? Taeterbetrieb { get; init; }

    /// <summary>Arbeiter-Nummer im <see cref="Taeterbetrieb"/> – derselbe, den <see cref="Diebeswesen.AnwesenheitJetzt"/> auf dem Auftrag zeigt. Null bei einer Bande.</summary>
    public int? TaeterArbeiter { get; init; }

    /// <summary>Name dieses Arbeiters (der gefasste Taschendieb ist danach nicht mehr im Betrieb).</summary>
    public string? TaeterName { get; init; }

    /// <summary>„Bande“ (aus dem Hinterzimmer) oder der Name der Bande der Landstraße; null, wenn eigene Diebe es taten.</summary>
    public string? Bande { get; init; }

    // ── Opfer ────────────────────────────────────────────────────────────────

    /// <summary>Das getroffene Haus (null beim Taschendieb – dort ist ein Einwohner das Opfer).</summary>
    public Konto? Opfer { get; init; }

    /// <summary>Getroffener Betrieb: Lager oder Werkstatt, beim Überfall die Heimat des Karrens.</summary>
    public string? Betrieb { get; init; }

    /// <summary>Beim Überfall: der Karren.</summary>
    public string? Karren { get; init; }

    /// <summary>Taschendieb: Nummer des bestohlenen Einwohners (Bürger-KI), sonst null.</summary>
    public int? OpferEinwohner { get; init; }

    /// <summary>Taschendieb: Name des Bestohlenen („Bürger am Markt“ ohne Bürger-KI). Entführung: Vorname der Person.</summary>
    public string? OpferName { get; init; }

    // ── Zeugen und Wache ─────────────────────────────────────────────────────

    /// <summary>Hat jemand die Täter erkannt (Zeuge in der Nacht bzw. ein Bürger am Markt)?</summary>
    public bool Zeuge { get; init; }

    /// <summary>Stand ein Wächter am Betrieb bzw. ein Hauswächter am Wohnhaus?</summary>
    public bool Waechter { get; init; }

    /// <summary>Das Haus der Wache, die gefasst hat (Taschendieb) bzw. Geleitschutz stellte (Überfall), sonst null.</summary>
    public Konto? Wache { get; init; }

    /// <summary>Das Haus, das aus dem Vorfall einen Beweis bekam (Zeuge, Wache, Lösegeldbrief), sonst null.</summary>
    public Konto? Beweis { get; init; }

    /// <summary>Taten aus dem Hinterzimmer und der Landstraße: namenlose Bandenmitglieder statt Arbeiter.</summary>
    public bool IstBande => Bande is not null;

    /// <summary>Ein Taschendieb, den die Wache gefasst hat (Abführen in die Arrestzelle).</summary>
    public bool IstFestnahme => Art == Vorfallart.Taschendiebstahl && Wache is not null;
}

/// <summary>
/// Die Liste der Vorfälle des laufenden und des letzten Spieltags. Unterwelt, Diebe und Überfälle tragen hier ein, was sie ohnehin
/// würfeln – ohne eigenen Wurf, ohne Wirkung auf das Spiel. Nicht im Spielstand: Nach dem Laden ist sie leer (es fehlt nur die
/// laufende Szene). Die Darstellung fragt <see cref="AmTag"/> bzw. <see cref="Zwischen"/> oder hört auf <see cref="Neu"/>.
/// </summary>
public sealed class Vorfallbuch
{
    private readonly Stadtwirtschaft _w;
    private readonly List<Vorfall> _liste = new();

    internal Vorfallbuch(Stadtwirtschaft w) => _w = w;

    /// <summary>Schreibt das Buch mit? Nur zum Abschalten im Test (Spielergebnis mit und ohne Liste bitgleich).</summary>
    public bool Mitschreiben { get; set; } = true;

    /// <summary>Ein Vorfall ist eben geschehen (mitten im Tick des Kerns).</summary>
    public event Action<Vorfall>? Neu;

    /// <summary>Alle gehaltenen Vorfälle, nach der Zeit geordnet (laufender und letzter Spieltag).</summary>
    public IReadOnlyList<Vorfall> Alle => _liste;

    /// <summary>Vorfälle dieses Spieltags (= Jahres), nach der Zeit geordnet.</summary>
    public IReadOnlyList<Vorfall> AmTag(int jahr) => _liste.Where(v => v.Jahr == jahr).ToList();

    /// <summary>Vorfälle mit <paramref name="von"/> ≤ Minute &lt; <paramref name="bis"/> (Spielminuten), nach der Zeit geordnet.</summary>
    public IReadOnlyList<Vorfall> Zwischen(long von, long bis) => _liste.Where(v => v.Minute >= von && v.Minute < bis).ToList();

    /// <summary>
    /// Trägt einen Vorfall ein; Minute, Jahr und Uhrzeit setzt das Buch nach der Uhr der Stadt. <paramref name="stundenende"/>: Der Aufruf
    /// kommt zum Ende einer Stunde (Nachtversuch, Taschendiebe) – steht die Uhr dann auf Minute 0 im Tag, ist es die Jahresrechnung, also
    /// 23 Uhr des alten Tags. Danach fallen Vorfälle heraus, die älter als der letzte Tag sind.
    /// </summary>
    internal void Eintragen(Vorfall vorfall, bool stundenende)
    {
        if (!Mitschreiben) return;
        var uhr = _w.Uhr;
        var (jahr, uhrzeit) = stundenende && uhr.MinuteImTag == 0
            ? (uhr.Jahr - 1, Spielzeit.TagesendeMinute)
            : (uhr.Jahr, uhr.UhrzeitInMinuten);
        var v = vorfall with { Minute = uhr.VergangeneMinuten, Jahr = jahr, Uhrzeit = uhrzeit };
        _liste.Add(v);
        _liste.RemoveAll(x => x.Jahr < jahr - 1);
        Neu?.Invoke(v);
    }
}
