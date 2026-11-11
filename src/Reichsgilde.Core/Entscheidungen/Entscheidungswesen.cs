using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Entscheidungen;

/// <summary>
/// Welche Frage ein Haus beantworten soll (Benachrichtigungen Stufe 3). Im Spielstand als Name gespeichert – neue Arten kommen hinten dazu,
/// umbenannt wird keine. Teil 1: die drei Forderungen der Unterwelt; Teil 2: Krankheit, Erbe, Geheimnis, Mahnung.
/// </summary>
public enum Entscheidungsart
{
    /// <summary>Ein Erpresserbrief (<see cref="Forderung"/>): Schweigegeld zahlen oder nicht.</summary>
    Erpresserbrief,
    /// <summary>Schutzgeld (<see cref="Diebesbrief"/> der Art Schutzgeld): zahlen oder es brennt.</summary>
    Schutzgeld,
    /// <summary>Lösegeld für eine Geisel (<see cref="Diebesbrief"/> der Art Lösegeld): zahlen oder die Person ist für immer fort.</summary>
    Loesegeld,
    /// <summary>Jemand im Haus ist krank: Medicus rufen, Kräuterschnaps geben oder nichts tun (Teil 2).</summary>
    Krankheit,
    /// <summary>Das Oberhaupt ist alt oder schwer krank: einen Erben im Testament bestimmen oder bei der Erbfolge bleiben (Teil 2).</summary>
    Erbe,
    /// <summary>Das Haus hat ein fremdes Geheimnis entdeckt: anzeigen, erpressen, bloßstellen oder für später merken (Teil 2).</summary>
    Geheimnis,
    /// <summary>Der Kämmerer mahnt: Wegweiser zu Kredit oder Betriebsverkauf, oder abwarten (Teil 2, ohne eigenen Befehl).</summary>
    Mahnung,
}

/// <summary>Wie eine Entscheidung zu Ende ging.</summary>
public enum Entscheidungsabschluss
{
    /// <summary>Das Haus hat gewählt – über <see cref="EntscheidungTreffen"/> oder direkt (Gauner-Tafel, Charakterfenster, Familienbuch, Bank).</summary>
    Entschieden,
    /// <summary>Die Frist lief ab: Die Voreinstellung galt (bei der Unterwelt: Schweigen heißt Nein, wie vor Stufe 3).</summary>
    Frist,
    /// <summary>Das Haus hatte schon <see cref="EntscheidungDaten.HoechstOffen"/> offene Fragen mit kurzer Frist – die älteste lief mit ihrer umkehrbaren Voreinstellung aus.</summary>
    Obergrenze,
    /// <summary>Der Anlass verschwand auf anderem Weg (oder das Haus ist nicht mehr im Spiel) – es gab nichts mehr zu entscheiden.</summary>
    Hinfaellig,
}

/// <summary>
/// Eine offene Frage an ein Haus. Verweise wie im Spielstand: <paramref name="Haus"/> fragt, <paramref name="Absender"/> fordert (Erpresser, Dieb)
/// bzw. ist das Haus, dem ein entdecktes Geheimnis gehört; <paramref name="Person"/> ist die betroffene Person als <see cref="Dynastie.Person.Nummer"/>
/// (Geisel, Kranke, Oberhaupt beim Erbe, Inhaber des Geheimnisses).
/// </summary>
/// <param name="Nummer">Fortlaufend je Partie, nie doppelt vergeben – damit zielt <see cref="EntscheidungTreffen"/>.</param>
/// <param name="Betrag">Gulden: Forderung, Kosten des Medicus, Schulden bei der Mahnung (sonst 0).</param>
/// <param name="Beginn">Spielminute, zu der die Frage gestellt wurde (<see cref="Spielzeit.VergangeneMinuten"/>).</param>
/// <param name="Frist">Spielminute, an deren Anfang die Voreinstellung gilt. Bei der Unterwelt die letzte Minute des Folgejahres, sonst Beginn + <see cref="EntscheidungDaten.FristFuer"/>.</param>
/// <param name="Voreinstellung">Die Wahl, die ohne Antwort gilt. Was gerade wirklich gälte, sagt <see cref="Entscheidungswesen.Voreinstellung"/>.</param>
/// <param name="Auswahl">Erbe: die Kinder (Personennummern), unter denen gewählt wird – Wahl 1 ist das erste.</param>
/// <param name="Geheimnis">Geheimnis: die Nummer des entdeckten Geheimnisses.</param>
/// <param name="Anlass">Teil 2: Schlüssel des Anlasses (z. B. „Krankheit:0:17:Husten:1403“); einmal entschieden, wird er nicht wieder gefragt.</param>
public sealed record Entscheidung(int Nummer, Entscheidungsart Art, Konto Haus, Konto? Absender, int? Person, double Betrag, long Beginn, long Frist, int Voreinstellung,
    IReadOnlyList<int>? Auswahl = null, int? Geheimnis = null, string? Anlass = null);

/// <summary>Werte der Entscheidungen (Abschnitt „entscheidungen“, ab Datenversion 81). Fehlt der Abschnitt, gelten diese Vorgaben.</summary>
public sealed class EntscheidungDaten
{
    /// <summary>
    /// So viele Fragen mit kurzer Frist dürfen je Haus offen sein; kommt eine weitere, läuft die älteste mit umkehrbarer Voreinstellung aus (gibt es
    /// keine, wartet die neue). Die Forderungen der Unterwelt zählen nicht mit.
    /// </summary>
    public int HoechstOffen { get; init; } = 6;

    /// <summary>
    /// Frist in Spielminuten je Art (Name der <see cref="Entscheidungsart"/>), „standard“ für alle Arten ohne eigenen Eintrag. Die Arten der Unterwelt
    /// stehen hier nicht: Sie behalten die Jahresfrist ihrer Forderung (Entscheidung des Nutzers, 10.11.2026).
    /// </summary>
    public Dictionary<string, int> FristMinuten { get; init; } = new() { [Standard] = 120 };

    /// <summary>Erbe: gefragt wird, wenn das Oberhaupt dieses Jahr mindestens mit dieser Chance stirbt – vor Alter oder an seiner Krankheit (ab Datenversion 82).</summary>
    public double ErbeAbSterberisiko { get; init; } = 0.1;

    private const string Standard = "standard";

    /// <summary>Folgt diese Art der Frist ihrer Forderung (Jahresfrist der Unterwelt) statt <see cref="FristMinuten"/>?</summary>
    public static bool HatJahresfrist(Entscheidungsart art) => art is Entscheidungsart.Erpresserbrief or Entscheidungsart.Schutzgeld or Entscheidungsart.Loesegeld;

    /// <summary>Die Frist dieser Art in Spielminuten (ohne Eintrag „standard“, ohne „standard“ 120).</summary>
    public int FristFuer(Entscheidungsart art) =>
        FristMinuten.TryGetValue(art.ToString(), out var m) ? m : FristMinuten.TryGetValue(Standard, out var s) ? s : 120;

    internal void Pruefen(List<string> f)
    {
        if (HoechstOffen < 1) f.Add("entscheidungen.hoechstOffen muss mindestens 1 sein.");
        if (ErbeAbSterberisiko is <= 0 or > 1) f.Add("entscheidungen.erbeAbSterberisiko muss über 0 und höchstens 1 sein.");
        foreach (var (name, minuten) in FristMinuten.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (minuten < 1) f.Add($"entscheidungen.fristMinuten.{name} muss mindestens 1 Spielminute sein.");
            if (name == Standard) continue;
            if (!Enum.TryParse<Entscheidungsart>(name, out var art) || art.ToString() != name)
                f.Add($"entscheidungen.fristMinuten: Eine Art „{name}“ gibt es nicht (erlaubt: {Standard}, {string.Join(", ", Enum.GetNames<Entscheidungsart>())}).");
            else if (HatJahresfrist(art))
                f.Add($"entscheidungen.fristMinuten.{name}: Diese Art behält die Jahresfrist ihrer Forderung.");
        }
    }
}

/// <summary>
/// Entscheidungen (Benachrichtigungen Stufe 3): Fragen an ein Haus mit Frist und Voreinstellung. Die Fragen selbst gibt es schon in den
/// Teilen des Kerns (Forderungen der Unterwelt, Krankheit, Testament, Geheimnisse, Mahnung mit ihren Befehlen); das Entscheidungswesen hält
/// dazu die Liste der offenen Fragen, beantwortet sie über die bestehenden Befehle und lässt die Voreinstellung gelten, wenn die Frist abläuft.
///
/// Ablauf je Spielminute (am Anfang des Ticks, nach den Befehlen dieser Minute):
///   • Abgleich: Fragen, deren Anlass verschwand, schließen; für neue Anlässe an Häuser von Menschen eine Frage öffnen – in fester Reihenfolge:
///     Erpresserbriefe, Briefe der Diebe, dann je Haus (Kontoreihenfolge) Krankheiten, Erbe, Geheimnisse, Mahnung. Ein Anlass aus Teil 2 wird
///     nach dem Abschluss nicht wieder gefragt (<c>Erledigt</c>, im Spielstand), solange er besteht. Mehr als <see cref="EntscheidungDaten.HoechstOffen"/>
///     Fragen mit kurzer Frist je Haus: die älteste mit umkehrbarer Voreinstellung läuft aus; die Forderungen der Unterwelt zählen nicht und werden
///     nie vorzeitig ausgeführt.
///   • Fristablauf in fester Reihenfolge nach Nummer. Bei der Unterwelt gilt „Zahlen“ (Lösegeld, wenn das Geld reicht) am Anfang der letzten Minute
///     des Folgejahres; „Ablehnen“ überlässt der Kern der Jahresrechnung, die schon vor Stufe 3 Schweigen als Nein wertete – so brennt es nach
///     abgelehntem Schutzgeld wie bisher in der nächsten Nacht.
///
/// Wer direkt antwortet (Gauner-Tafel, Medicus im Charakterfenster, Testament im Familienbuch, Geheimnis nutzen, Kredit, Betriebsverkauf),
/// schließt die Frage genauso (<see cref="NachBefehl"/>). Jeder Abschluss steht als eine Meldung der Art <see cref="Meldungsart.Entscheidung"/> im Verlauf.
/// KI-Häuser bekommen keine Fragen; sie antworten wie bisher selbst.
///
/// Deterministisch: nur Spielminuten, feste Reihenfolgen (Listen, Nummern), kein Zufall. Die offenen Fragen stehen im Spielstand (Abschnitt
/// „Wirtschaft“, <see cref="EntscheidungenStand"/>) und damit in der Zustandsprüfsumme.
/// </summary>
public sealed partial class Entscheidungswesen
{
    /// <summary>Wahl „Zahlen“ bei Erpresserbrief, Schutzgeld und Lösegeld.</summary>
    public const int Zahlen = 0;

    /// <summary>Wahl „Ablehnen“ bei Erpresserbrief, Schutzgeld und Lösegeld.</summary>
    public const int Ablehnen = 1;

    /// <summary>Für <see cref="EntscheidungTreffen"/>: die Voreinstellung, wie sie beim Ausführen gilt (Einstellung „Automatisch“ der Oberfläche).</summary>
    public const int Voreinstellungswahl = -1;

    private readonly Stadtwirtschaft _w;
    private readonly List<Entscheidung> _offen = new();
    private readonly List<string> _erledigt = new(); // Anlässe aus Teil 2, schon entschieden – in der Reihenfolge des Abschlusses
    private int _naechste = 1;

    // Wie die gerade laufende Antwort endet (nur während Treffen oder Auslösen, nicht im Spielstand).
    private Entscheidungsabschluss _grund = Entscheidungsabschluss.Entschieden;

    internal Entscheidungswesen(Stadtwirtschaft w, EntscheidungDaten? daten)
    {
        _w = w;
        Daten = daten ?? new EntscheidungDaten();
    }

    public EntscheidungDaten Daten { get; }

    /// <summary>Alle offenen Fragen, nach Nummer (= nach Beginn).</summary>
    public IReadOnlyList<Entscheidung> Offen => _offen;

    /// <summary>Die offenen Fragen an dieses Haus, die dringendste (kürzeste Frist) zuerst, bei Gleichstand nach Nummer.</summary>
    public IReadOnlyList<Entscheidung> OffenFuer(Konto haus) =>
        _offen.Where(e => e.Haus == haus).OrderBy(e => e.Frist).ThenBy(e => e.Nummer).ToList();

    /// <summary>Die offene Frage mit dieser Nummer (null: gibt es nicht oder ist schon entschieden).</summary>
    public Entscheidung? MitNummer(int nummer) => _offen.FirstOrDefault(e => e.Nummer == nummer);

    /// <summary>Eine neue Frage ist offen (am Anfang einer Spielminute).</summary>
    public event Action<Entscheidung>? Geoeffnet;

    /// <summary>Eine Frage ist geschlossen – gleich nach der Meldung im Verlauf.</summary>
    public event Action<Entscheidung, Entscheidungsabschluss>? Geschlossen;

    /// <summary>Bekommt dieses Haus Fragen? Nur Häuser von Menschen, die noch im Spiel sind (KI-Häuser antworten selbst).</summary>
    public bool BekommtEntscheidungen(Konto haus) => haus.IstAktiv && !_w.Rivalen.Any(r => r.Konto == haus);

    /// <summary>Spielminuten bis zur Voreinstellung (0: sie gilt am Anfang der nächsten Minute).</summary>
    public long RestMinuten(Entscheidung e) => Math.Max(0, e.Frist - _w.Uhr.VergangeneMinuten);

    // ═════════════════════════════════════════════════════════════════════════
    //  Abfragen für die Oberfläche (nur lesen)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Knöpfe der Frage – der Index ist die Wahl für <see cref="EntscheidungTreffen"/>.</summary>
    public IReadOnlyList<string> Wahlen(Entscheidung e) =>
        EntscheidungDaten.HatJahresfrist(e.Art) ? new[] { $"Zahlen ({Gulden(e.Betrag)})", "Ablehnen" } : WahlenTeil2(e);

    /// <summary>
    /// Die Wahl, die jetzt gälte: Lösegeld „Zahlen“ nur, wenn das Geld reicht; Krankheit Medicus, wenn bezahlbar, sonst Kräuterschnaps aus der
    /// Hausapotheke, sonst nichts; Erbe, Geheimnis und Mahnung: nichts tun.
    /// </summary>
    public int Voreinstellung(Entscheidung e) => e.Art switch
    {
        Entscheidungsart.Krankheit => KrankheitVoreinstellung(e),
        _ when EntscheidungDaten.HatJahresfrist(e.Art) => e.Voreinstellung == Zahlen && e.Haus.Vermoegen < e.Betrag ? Ablehnen : e.Voreinstellung,
        _ => e.Voreinstellung,
    };

    /// <summary>Die Überschrift des Briefes.</summary>
    public string Titel(Entscheidung e) => e.Art switch
    {
        Entscheidungsart.Erpresserbrief => $"Erpresserbrief von {Name(e.Absender)}",
        Entscheidungsart.Schutzgeld => $"Schutzgeld für {Name(e.Absender)}",
        Entscheidungsart.Loesegeld => $"Lösegeld für {Geiselname(e)}",
        _ => TitelTeil2(e),
    };

    /// <summary>Was der Brief sagt, in einem Satz.</summary>
    public string Beschreibung(Entscheidung e)
    {
        var von = Name(e.Absender);
        return e.Art switch
        {
            Entscheidungsart.Erpresserbrief => Forderung(e)?.Geheimnis is not null
                ? $"{von} verlangt {Gulden(e.Betrag)} Schweigegeld – sonst kommt ein Geheimnis eures Hauses ans Licht."
                : $"{von} verlangt {Gulden(e.Betrag)} Schweigegeld – sonst bringt er seine Beweise vor Gericht.",
            Entscheidungsart.Schutzgeld => $"Die Leute von {von} verlangen {Gulden(e.Betrag)} Schutzgeld – sonst brennt es.",
            Entscheidungsart.Loesegeld => $"{von} hält {Geiselname(e)} fest und verlangt {Gulden(e.Betrag)} Lösegeld – sonst wird {Geiselname(e)} in die Fremde verkauft.",
            _ => BeschreibungTeil2(e),
        };
    }

    /// <summary>
    /// Was eine Wahl bewirkt – für den Tooltip des Knopfes, reiner Text mit Zahlen aus den Rechnungen des Kerns. Was sich nicht zurücknehmen
    /// lässt, steht ausdrücklich da. Ändert nichts.
    /// </summary>
    public string Folgen(Entscheidung e, int wahl)
    {
        if (wahl == Voreinstellungswahl) wahl = Voreinstellung(e);
        if (wahl < 0 || wahl >= Wahlen(e).Count) return $"Eine Wahl {wahl} gibt es hier nicht.";
        var text = EntscheidungDaten.HatJahresfrist(e.Art) ? FolgenUnterwelt(e, wahl) : FolgenTeil2(e, wahl);
        return wahl == Voreinstellung(e) ? $"{text} Das gilt auch, wenn die Frist abläuft." : text;
    }

    private string FolgenUnterwelt(Entscheidung e, int wahl)
    {
        var von = Name(e.Absender);
        var teile = new List<string>();
        if (wahl == Zahlen)
        {
            teile.Add($"Kostet {Gulden(e.Betrag)} (ihr habt {Gulden(e.Haus.Vermoegen)}).");
            if (e.Haus.Vermoegen < e.Betrag) teile.Add($"Dafür reicht euer Geld nicht – es fehlen {Gulden(e.Betrag - e.Haus.Vermoegen)}.");
            teile.Add(e.Art switch
            {
                Entscheidungsart.Erpresserbrief => Forderung(e)?.Geheimnis is not null
                    ? $"{von} schweigt und vergisst das Geheimnis."
                    : $"{von} verbrennt seine {Beweise(e.Absender, e.Haus)} gegen euch.",
                Entscheidungsart.Schutzgeld => $"{von} lässt euch in Ruhe.",
                _ => $"{Geiselname(e)} kehrt nach Hause zurück.",
            });
        }
        else
        {
            teile.Add("Kostet nichts.");
            switch (e.Art)
            {
                case Entscheidungsart.Erpresserbrief:
                    teile.Add(Forderung(e)?.Geheimnis is not null
                        ? $"{von} kann das Geheimnis vor Gericht anzeigen oder in der Stadt bloßstellen."
                        : $"{von} hat {Beweise(e.Absender, e.Haus)} gegen euch und kann klagen.");
                    break;
                case Entscheidungsart.Schutzgeld:
                    teile.Add(e.Absender is { } dieb && _w.Diebe.IstAktiv && _w.Diebe.Versteck(dieb) is not null && _w.Diebe.Brandziel(e.Haus) is { } ziel
                        ? $"In der nächsten Nacht legen die Leute von {von} Feuer in {ziel.Name} (erwarteter Schaden ≈ {Gulden(_w.Diebe.DrohenderBrand(e.Haus, dieb))}). Wächter helfen."
                        : $"{von} hat gerade keine Leute für einen Brand.");
                    break;
                default:
                    teile.Add($"{Geiselname(e)} wird in die Fremde verkauft – ihr seht {Geiselname(e)} nie wieder. Das lässt sich nicht zurücknehmen.");
                    break;
            }
        }
        return string.Join(" ", teile);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Befehl
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="EntscheidungTreffen"/>: wählt über den bestehenden Befehl mit allen seinen Prüfungen. Schlägt er fehl (etwa zu wenig Geld),
    /// bleibt die Frage offen. Wahlen ohne Befehl (nichts tun, merken, Erbfolge belassen, Wegweiser der Mahnung) schließen die Frage sofort.
    /// </summary>
    internal Befehlsergebnis Treffen(Konto absender, int nummer, int wahl)
    {
        if (MitNummer(nummer) is not { } e)
            return Befehlsergebnis.Abgelehnt($"Eine offene Entscheidung Nr. {nummer} gibt es nicht – vielleicht ist sie schon entschieden.");
        if (e.Haus != absender)
            return Befehlsergebnis.Abgelehnt($"Diese Entscheidung steht {e.Haus.Inhaber} zu.");
        if (wahl == Voreinstellungswahl) wahl = Voreinstellung(e);
        var anzahl = Wahlen(e).Count;
        if (wahl < 0 || wahl >= anzahl)
            return Befehlsergebnis.Abgelehnt($"Eine Wahl {wahl} gibt es hier nicht (0 bis {anzahl - 1}).");
        if (Sperrgrund(e) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        return Antworten(e, wahl, Entscheidungsabschluss.Entschieden);
    }

    /// <summary>Warum diese Frage gerade nicht über ihren Befehl zu beantworten ist (null = sie ist es).</summary>
    private string? Sperrgrund(Entscheidung e)
    {
        // Zwei Geiseln beim selben Dieb: Der Befehl beantwortet die ältere Forderung zuerst.
        if (e.Art == Entscheidungsart.Loesegeld && e.Absender is { } dieb
            && _w.Diebe.BriefAn(e.Haus, dieb, Diebesbriefart.Loesegeld) is { } erster && erster.Geisel != e.Person)
            return $"Beantwortet zuerst die ältere Lösegeldforderung von {dieb.Inhaber} (für {GeiselnameVon(e.Haus, erster.Geisel)}).";
        return null;
    }

    /// <summary>Der bestehende Befehl zu dieser Wahl – null: Die Wahl braucht keinen (nichts tun, merken, Erbfolge belassen, Wegweiser).</summary>
    private Befehl? BefehlFuer(Entscheidung e, int wahl)
    {
        if (!EntscheidungDaten.HatJahresfrist(e.Art)) return BefehlTeil2(e, wahl);
        if (e.Absender is not { } von) return null;
        return e.Art == Entscheidungsart.Erpresserbrief
            ? new ErpressungBeantworten(von.Inhaber, wahl == Zahlen)
            : new DiebesforderungBeantworten(von.Inhaber, e.Art == Entscheidungsart.Schutzgeld ? Diebesbriefart.Schutzgeld : Diebesbriefart.Loesegeld, wahl == Zahlen);
    }

    private Befehlsergebnis Antworten(Entscheidung e, int wahl, Entscheidungsabschluss grund)
    {
        if (BefehlFuer(e, wahl) is not { } befehl)
        {
            if (EntscheidungDaten.HatJahresfrist(e.Art)) return Befehlsergebnis.Abgelehnt("Diese Forderung hat keinen Absender.");
            _offen.Remove(e);
            Schliessen(e, grund, wahl);
            return Befehlsergebnis.Ok(OhneBefehlText(e, wahl));
        }
        var vorher = _grund;
        _grund = grund;
        try
        {
            var ergebnis = _w.Ausfuehren(e.Haus, befehl); // schließt die Frage über NachBefehl
            if (ergebnis.Erfolg && _offen.Remove(e)) Schliessen(e, grund, wahl); // falls NachBefehl sie nicht erkannte (Teil 2)
            return ergebnis;
        }
        finally { _grund = vorher; }
    }

    /// <summary>
    /// Nach jedem erfolgreichen Befehl (aus <see cref="Stadtwirtschaft.Ausfuehren"/>): Hat die Antwort eine Frage erledigt – über eine
    /// Entscheidung oder direkt (Gauner-Tafel, Charakterfenster, Familienbuch, Bank, Betriebsverkauf) –, schließt das sie.
    /// </summary>
    internal void NachBefehl(Konto absender, Befehl befehl)
    {
        if (befehl is ErpressungBeantworten or DiebesforderungBeantworten)
        {
            // Nur die Frage, die zum Befehl passt (Art und Absender) – was sonst verschwand, schließt der Abgleich mit seinem eigenen Grund.
            var (zahlen, absenderName, art) = befehl is ErpressungBeantworten a
                ? (a.Zahlen, a.Erpresser, Entscheidungsart.Erpresserbrief)
                : befehl is DiebesforderungBeantworten d
                    ? (d.Zahlen, d.Dieb, d.Art == Diebesbriefart.Schutzgeld ? Entscheidungsart.Schutzgeld : Entscheidungsart.Loesegeld)
                    : (false, "", Entscheidungsart.Erpresserbrief);
            for (var i = 0; i < _offen.Count; i++)
            {
                var e = _offen[i];
                if (e.Haus != absender || e.Art != art || e.Absender?.Inhaber != absenderName || Besteht(e)) continue;
                _offen.RemoveAt(i);
                Schliessen(e, _grund, zahlen ? Zahlen : Ablehnen);
                i--;
            }
            return;
        }
        if (_offen.Count == 0) return;
        for (var i = 0; i < _offen.Count; i++)
        {
            var e = _offen[i];
            if (e.Haus != absender || WahlAusBefehl(e, befehl) is not int wahl) continue;
            _offen.RemoveAt(i);
            Schliessen(e, _grund, wahl);
            i--;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Jede Spielminute
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Am Anfang jeder Spielminute (nach den Befehlen dieser Minute): Abgleich, dann Fristablauf nach Nummer.</summary>
    internal void Minute(Spielzeit zeit)
    {
        Abgleichen();
        if (_offen.Count == 0) return;
        var jetzt = zeit.VergangeneMinuten;
        foreach (var e in _offen.Where(x => x.Frist <= jetzt).ToList()) // _offen ist nach Nummer geordnet
        {
            if (!_offen.Contains(e)) continue;
            Ausloesen(e, Entscheidungsabschluss.Frist, jetzt);
        }
    }

    /// <summary>Schließt Fragen ohne Anlass und öffnet neue – in fester Reihenfolge.</summary>
    private void Abgleichen()
    {
        var jetzt = _w.Uhr.VergangeneMinuten;
        var anlaesse = AnlaesseTeil2(); // je Schlüssel die Frage, die er stellen würde (Reihenfolge fest)
        for (var i = 0; i < _offen.Count; i++)
        {
            var e = _offen[i];
            var besteht = e.Anlass is null ? Besteht(e) : anlaesse.Any(a => a.Anlass == e.Anlass);
            if (BekommtEntscheidungen(e.Haus) && besteht) continue;
            _offen.RemoveAt(i);
            i--;
            if (!BekommtEntscheidungen(e.Haus)) { Geschlossen?.Invoke(e, Entscheidungsabschluss.Hinfaellig); continue; } // ohne Meldung: das Haus fragt niemand mehr
            Schliessen(e, e.Anlass is null && jetzt > e.Frist ? Entscheidungsabschluss.Frist : Entscheidungsabschluss.Hinfaellig, null);
        }
        if (_w.Gauner.IstAktiv && _w.Gauner.Forderungen.Count > 0)
            foreach (var f in _w.Gauner.Forderungen.ToList())
                if (BekommtEntscheidungen(f.Opfer) && _w.Gauner.Forderungen.Contains(f)
                    && !_offen.Any(e => e.Art == Entscheidungsart.Erpresserbrief && e.Haus == f.Opfer && e.Absender == f.Erpresser))
                    Oeffnen(new Entscheidung(0, Entscheidungsart.Erpresserbrief, f.Opfer, f.Erpresser, null, f.Betrag, jetzt, Jahresfrist(f.Jahr), Ablehnen));
        if (_w.Diebe.IstAktiv && _w.Diebe.Briefe.Count > 0)
            foreach (var b in _w.Diebe.Briefe.ToList())
            {
                var art = b.Art == Diebesbriefart.Schutzgeld ? Entscheidungsart.Schutzgeld : Entscheidungsart.Loesegeld;
                if (BekommtEntscheidungen(b.Opfer) && _w.Diebe.Briefe.Contains(b)
                    && !_offen.Any(e => e.Art == art && e.Haus == b.Opfer && e.Absender == b.Dieb && e.Person == b.Geisel))
                    Oeffnen(new Entscheidung(0, art, b.Opfer, b.Dieb, b.Geisel, b.Betrag, jetzt, Jahresfrist(b.Jahr),
                        art == Entscheidungsart.Loesegeld ? Zahlen : Ablehnen));
            }
        // Teil 2: erledigte Anlässe vergessen, die es nicht mehr gibt; neue Anlässe fragen.
        if (_erledigt.Count > 0) _erledigt.RemoveAll(k => !anlaesse.Any(a => a.Anlass == k));
        foreach (var a in anlaesse)
            if (!_erledigt.Contains(a.Anlass!) && !_offen.Any(e => e.Anlass == a.Anlass) && BekommtEntscheidungen(a.Haus))
                Oeffnen(a with { Beginn = jetzt, Frist = jetzt + Daten.FristFuer(a.Art) });
    }

    /// <summary>Die Jahresfrist der Unterwelt: die letzte Spielminute des Folgejahres (die Jahresrechnung am Ende dieser Minute wertet Schweigen als Nein).</summary>
    private static long Jahresfrist(int jahr) => (jahr + 2 - Spielzeit.StartJahr) * (long)Spielzeit.MinutenProTag - 1;

    /// <summary>
    /// Zählt diese Frage zur Obergrenze? Die Forderungen der Unterwelt (Jahresfrist) nie: Die Obergrenze darf nichts Unumkehrbares auslösen
    /// (Entscheidung des Teamleiters, 10.11.2026) – sie werden nie vorzeitig ausgeführt und verdrängen nichts.
    /// </summary>
    private static bool ZaehltZurObergrenze(Entscheidung e) => !EntscheidungDaten.HatJahresfrist(e.Art);

    /// <summary>
    /// Darf die Obergrenze diese Frage verdrängen? Nur eine mit kurzer Frist, deren Voreinstellung jetzt keinen Befehl braucht (nichts tun, merken,
    /// Erbfolge belassen, abwarten) – eine Krankheit mit Medicus oder Kräuterschnaps als Voreinstellung bleibt offen.
    /// </summary>
    private bool Verdraengbar(Entscheidung e) => ZaehltZurObergrenze(e) && BefehlFuer(e, Voreinstellung(e)) is null;

    private void Oeffnen(Entscheidung neu)
    {
        // Obergrenze je Haus: die älteste verdrängbare Frage läuft mit ihrer (umkehrbaren) Voreinstellung aus. Gibt es keine, wartet die neue
        // Frage auf eine spätere Minute.
        if (ZaehltZurObergrenze(neu) && _offen.Count(e => e.Haus == neu.Haus && ZaehltZurObergrenze(e)) >= Daten.HoechstOffen)
        {
            if (_offen.Where(e => e.Haus == neu.Haus && Verdraengbar(e)).OrderBy(e => e.Beginn).ThenBy(e => e.Nummer).FirstOrDefault() is not { } aelteste) return;
            Ausloesen(aelteste, Entscheidungsabschluss.Obergrenze, _w.Uhr.VergangeneMinuten);
            if (_offen.Count(e => e.Haus == neu.Haus && ZaehltZurObergrenze(e)) >= Daten.HoechstOffen) return;
        }
        var e = neu with { Nummer = _naechste++ };
        _offen.Add(e);
        Geoeffnet?.Invoke(e);
    }

    /// <summary>
    /// Die Voreinstellung gilt (Fristablauf oder Obergrenze). Schlägt ihr Befehl fehl, gilt der Rückfall: bei der Unterwelt „Ablehnen“, bei der
    /// Krankheit erst Kräuterschnaps, dann nichts; sonst nichts tun. Bei der Unterwelt überlässt der Fristablauf das Ablehnen der Jahresrechnung
    /// in derselben Minute (wie vor Stufe 3); die Frage schließt dann der Abgleich der nächsten Minute.
    /// </summary>
    private void Ausloesen(Entscheidung e, Entscheidungsabschluss grund, long jetzt)
    {
        var wahl = Voreinstellung(e);
        if (!EntscheidungDaten.HatJahresfrist(e.Art))
        {
            if (Antworten(e, wahl, grund).Erfolg) return;
            if (e.Art == Entscheidungsart.Krankheit && wahl == Medicus && SchnapsMoeglich(e) && Antworten(e, Schnaps, grund).Erfolg) return;
            if (_offen.Contains(e)) Antworten(e, Rueckfall(e), grund); // ohne Befehl: schließt immer
            return;
        }
        if (grund == Entscheidungsabschluss.Frist && jetzt != e.Frist) return; // die Jahresrechnung steht noch aus (angehaltene Ratssitzung)
        if (wahl == Zahlen && Sperrgrund(e) is null && Antworten(e, Zahlen, grund).Erfolg) return;
        if (grund == Entscheidungsabschluss.Frist) return; // Schweigen heißt Nein – in der Jahresrechnung
        if (Sperrgrund(e) is null) Antworten(e, Ablehnen, grund);
    }

    private void Schliessen(Entscheidung e, Entscheidungsabschluss grund, int? wahl)
    {
        if (e.Anlass is { } anlass && grund != Entscheidungsabschluss.Hinfaellig && !_erledigt.Contains(anlass)) _erledigt.Add(anlass);
        var satz = Abschlusssatz(e, grund, wahl);
        var person = e.Person is int n ? _w.Familien.PersonNummer(n) : null;
        _w.Melden(Meldungsart.Entscheidung, Abschlusstext(satz, grund, wahl), haus: e.Haus, person: person, vertraulich: true);
        if (HatFolgen(e, grund, wahl))
            _w.Familien.ChronikEintragen(e.Haus, Dynastie.Chronikart.Entscheidung, satz, person, vertraulich: true);
        Geschlossen?.Invoke(e, grund);
    }

    /// <summary>
    /// Kommt die Entscheidung in die Hauschronik? Nur mit Folgen: jede Antwort an die Unterwelt (gezahlt, zurückgewiesen, geschwiegen),
    /// ein geändertes Testament, ein genutztes fremdes Geheimnis (anzeigen, erpressen, bloßstellen). Nicht: Hinfälliges, Krankheit, Mahnung,
    /// „bei der Erbfolge bleiben“, „Geheimnis für später merken“.
    /// </summary>
    internal static bool HatFolgen(Entscheidung e, Entscheidungsabschluss grund, int? wahl) => grund != Entscheidungsabschluss.Hinfaellig && e.Art switch
    {
        Entscheidungsart.Erpresserbrief or Entscheidungsart.Schutzgeld or Entscheidungsart.Loesegeld => true,
        Entscheidungsart.Erbe => wahl is int w && w != ErbfolgeBelassen,
        Entscheidungsart.Geheimnis => wahl is Anzeigen or Erpressen or Blossstellen,
        _ => false,
    };

    private string Abschlusssatz(Entscheidung e, Entscheidungsabschluss grund, int? wahl) =>
        EntscheidungDaten.HatJahresfrist(e.Art) ? AbschlussUnterwelt(e, grund, wahl) : AbschlussTeil2(e, grund, wahl);

    private static string Abschlusstext(string satz, Entscheidungsabschluss grund, int? wahl)
    {
        if (wahl is null) return satz;
        return grund switch
        {
            Entscheidungsabschluss.Frist => $"{satz} Die Frist lief ab – es galt die Voreinstellung.",
            Entscheidungsabschluss.Obergrenze => $"{satz} Zu viele offene Entscheidungen – die älteste lief mit ihrer Voreinstellung aus.",
            _ => satz,
        };
    }

    private string AbschlussUnterwelt(Entscheidung e, Entscheidungsabschluss grund, int? wahl)
    {
        var von = Name(e.Absender);
        if (wahl is null) // die Forderung verschwand ohne Befehl
            return grund == Entscheidungsabschluss.Frist
                ? e.Art switch
                {
                    Entscheidungsart.Erpresserbrief => $"Auf den Erpresserbrief von {von} habt ihr nicht geantwortet – Schweigen galt als Nein.",
                    Entscheidungsart.Schutzgeld => $"Auf die Schutzgeldforderung von {von} habt ihr nicht geantwortet – Schweigen galt als Nein.",
                    _ => $"Für {Geiselname(e)} habt ihr bis zur Frist kein Lösegeld gezahlt – Schweigen galt als Nein.",
                }
                : $"Die Forderung von {von} ({Titel(e)}) ist hinfällig – es gibt nichts mehr zu entscheiden.";
        return (e.Art, wahl == Zahlen) switch
        {
            (Entscheidungsart.Erpresserbrief, true) => $"Ihr habt {von} {Gulden(e.Betrag)} Schweigegeld gezahlt.",
            (Entscheidungsart.Erpresserbrief, false) => $"Ihr habt den Erpresserbrief von {von} zurückgewiesen.",
            (Entscheidungsart.Schutzgeld, true) => $"Ihr habt {von} {Gulden(e.Betrag)} Schutzgeld gezahlt.",
            (Entscheidungsart.Schutzgeld, false) => $"Ihr habt die Schutzgeldforderung von {von} zurückgewiesen.",
            (_, true) => $"Ihr habt {von} {Gulden(e.Betrag)} Lösegeld für {Geiselname(e)} gezahlt.",
            _ => $"Ihr habt kein Lösegeld für {Geiselname(e)} gezahlt.",
        };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Hilfen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Gibt es die Forderung dieser Frage der Unterwelt noch?</summary>
    private bool Besteht(Entscheidung e) => e.Art switch
    {
        Entscheidungsart.Erpresserbrief => Forderung(e) is not null,
        Entscheidungsart.Schutzgeld or Entscheidungsart.Loesegeld =>
            _w.Diebe.IstAktiv && _w.Diebe.Briefe.Any(b => b.Art == (e.Art == Entscheidungsart.Schutzgeld ? Diebesbriefart.Schutzgeld : Diebesbriefart.Loesegeld)
                                                        && b.Opfer == e.Haus && b.Dieb == e.Absender && b.Geisel == e.Person),
        _ => false,
    };

    private Forderung? Forderung(Entscheidung e) =>
        _w.Gauner.IstAktiv && e.Absender is { } von ? _w.Gauner.ForderungAn(e.Haus, von) : null;

    private string Gulden(double betrag) => $"{Zahlenformat.Ganz(betrag)} {_w.Daten.Allgemein.Waehrung}";

    private static string Name(Konto? k) => k?.Inhaber ?? "Unbekannt";

    private string Beweise(Konto? von, Konto gegen)
    {
        var n = von is not null && _w.Justiz.IstAktiv ? _w.Justiz.Beweise(von, gegen).Count() : 0;
        return n == 1 ? "1 Beweis" : $"{n} Beweise";
    }

    private string Geiselname(Entscheidung e) => GeiselnameVon(e.Haus, e.Person);

    private string GeiselnameVon(Konto haus, int? nummer) =>
        nummer is int n && _w.Familien.Von(haus)?.Lebende.FirstOrDefault(p => p.Nummer == n) is { } p ? p.Vorname : "die Geisel";

    // ═════════════════════════════════════════════════════════════════════════
    //  Speichern
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Null, solange es nie eine Frage gab – so bleibt ein Spielstand ohne Entscheidungen Byte für Byte wie vorher.</summary>
    internal EntscheidungenStand? Sichern(Func<Konto, int> nr) => _naechste == 1 && _offen.Count == 0 && _erledigt.Count == 0 ? null : new()
    {
        NaechsteNummer = _naechste,
        Offen = _offen.Select(e => new EntscheidungStand
        {
            Nummer = e.Nummer, Art = e.Art, Haus = nr(e.Haus), Absender = e.Absender is null ? null : nr(e.Absender), Person = e.Person,
            Betrag = e.Betrag, Beginn = e.Beginn, Frist = e.Frist, Voreinstellung = e.Voreinstellung,
            Auswahl = e.Auswahl?.ToList(), Geheimnis = e.Geheimnis, Anlass = e.Anlass,
        }).ToList(),
        Erledigt = _erledigt.Count == 0 ? null : _erledigt.ToList(),
    };

    /// <summary>Stellt die offenen Fragen her (nach den Konten). Alte Spielstände: leer – der Abgleich der ersten Minute legt sie aus den Anlässen neu an.</summary>
    internal void Wiederherstellen(EntscheidungenStand? s, IReadOnlyList<Konto> konten)
    {
        _offen.Clear();
        _erledigt.Clear();
        _naechste = 1;
        if (s is null) return;
        _naechste = Math.Max(1, s.NaechsteNummer);
        foreach (var e in s.Offen)
        {
            if (e.Haus < 0 || e.Haus >= konten.Count) continue;
            var absender = e.Absender is int a && a >= 0 && a < konten.Count ? konten[a] : null;
            _offen.Add(new Entscheidung(e.Nummer, e.Art, konten[e.Haus], absender, e.Person, e.Betrag, e.Beginn, e.Frist, e.Voreinstellung,
                e.Auswahl, e.Geheimnis, e.Anlass));
            _naechste = Math.Max(_naechste, e.Nummer + 1);
        }
        if (s.Erledigt is { } erledigt) _erledigt.AddRange(erledigt);
    }
}
