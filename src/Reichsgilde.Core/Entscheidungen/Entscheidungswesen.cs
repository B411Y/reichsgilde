using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Entscheidungen;

/// <summary>
/// Welche Frage ein Haus beantworten soll (Benachrichtigungen Stufe 3). Im Spielstand als Name gespeichert – neue Arten kommen hinten dazu,
/// umbenannt wird keine. Teil 1 kennt die drei Forderungen der Unterwelt; Medicus, Erbe und Geheimnis folgen.
/// </summary>
public enum Entscheidungsart
{
    /// <summary>Ein Erpresserbrief (<see cref="Forderung"/>): Schweigegeld zahlen oder nicht.</summary>
    Erpresserbrief,
    /// <summary>Schutzgeld (<see cref="Diebesbrief"/> der Art Schutzgeld): zahlen oder es brennt.</summary>
    Schutzgeld,
    /// <summary>Lösegeld für eine Geisel (<see cref="Diebesbrief"/> der Art Lösegeld): zahlen oder die Person ist für immer fort.</summary>
    Loesegeld,
}

/// <summary>Wie eine Entscheidung zu Ende ging.</summary>
public enum Entscheidungsabschluss
{
    /// <summary>Das Haus hat gewählt – über <see cref="EntscheidungTreffen"/> oder direkt in der Gauner-Tafel.</summary>
    Entschieden,
    /// <summary>Die Frist lief ab: Die Voreinstellung galt (bei der Unterwelt: Schweigen heißt Nein, wie vor Stufe 3).</summary>
    Frist,
    /// <summary>Das Haus hatte schon <see cref="EntscheidungDaten.HoechstOffen"/> offene Fragen – die älteste lief mit der Voreinstellung aus.</summary>
    Obergrenze,
    /// <summary>Die Forderung verschwand auf anderem Weg (oder das Haus ist nicht mehr im Spiel) – es gab nichts mehr zu entscheiden.</summary>
    Hinfaellig,
}

/// <summary>
/// Eine offene Frage an ein Haus. Verweise wie im Spielstand: <paramref name="Haus"/> fragt, <paramref name="Absender"/> fordert (Erpresser, Dieb),
/// <paramref name="Person"/> ist die betroffene Person (Geisel) als <see cref="Dynastie.Person.Nummer"/>.
/// </summary>
/// <param name="Nummer">Fortlaufend je Partie, nie doppelt vergeben – damit zielt <see cref="EntscheidungTreffen"/>.</param>
/// <param name="Betrag">Was gefordert wird (Gulden).</param>
/// <param name="Beginn">Spielminute, zu der die Frage gestellt wurde (<see cref="Spielzeit.VergangeneMinuten"/>).</param>
/// <param name="Frist">Spielminute, an deren Anfang die Voreinstellung gilt. Bei der Unterwelt die letzte Minute des Folgejahres (die Jahresfrist der Forderung).</param>
/// <param name="Voreinstellung">Die Wahl, die ohne Antwort gilt (<see cref="Entscheidungswesen.Zahlen"/> oder <see cref="Entscheidungswesen.Ablehnen"/>).
/// Was gerade wirklich gälte, sagt <see cref="Entscheidungswesen.Voreinstellung"/> (Lösegeld: nur, wenn das Geld reicht).</param>
public sealed record Entscheidung(int Nummer, Entscheidungsart Art, Konto Haus, Konto? Absender, int? Person, double Betrag, long Beginn, long Frist, int Voreinstellung);

/// <summary>Werte der Entscheidungen (Abschnitt „entscheidungen“, ab Datenversion 81). Fehlt der Abschnitt, gelten diese Vorgaben.</summary>
public sealed class EntscheidungDaten
{
    /// <summary>So viele Fragen dürfen je Haus offen sein; kommt eine weitere, läuft die älteste mit ihrer Voreinstellung aus.</summary>
    public int HoechstOffen { get; init; } = 6;

    /// <summary>
    /// Frist in Spielminuten je Art (Name der <see cref="Entscheidungsart"/>), „standard“ für alle Arten ohne eigenen Eintrag. Die Arten der Unterwelt
    /// stehen hier nicht: Sie behalten die Jahresfrist ihrer Forderung (Entscheidung des Nutzers, 10.11.2026).
    /// </summary>
    public Dictionary<string, int> FristMinuten { get; init; } = new() { [Standard] = 120 };

    private const string Standard = "standard";

    /// <summary>Folgt diese Art der Frist ihrer Forderung (Jahresfrist der Unterwelt) statt <see cref="FristMinuten"/>?</summary>
    public static bool HatJahresfrist(Entscheidungsart art) => art is Entscheidungsart.Erpresserbrief or Entscheidungsart.Schutzgeld or Entscheidungsart.Loesegeld;

    /// <summary>Die Frist dieser Art in Spielminuten (ohne Eintrag „standard“, ohne „standard“ 120).</summary>
    public int FristFuer(Entscheidungsart art) =>
        FristMinuten.TryGetValue(art.ToString(), out var m) ? m : FristMinuten.TryGetValue(Standard, out var s) ? s : 120;

    internal void Pruefen(List<string> f)
    {
        if (HoechstOffen < 1) f.Add("entscheidungen.hoechstOffen muss mindestens 1 sein.");
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
/// Entscheidungen (Benachrichtigungen Stufe 3, Teil 1): Fragen an ein Haus mit Frist und Voreinstellung. Die Fragen selbst gibt es schon in den
/// Teilen des Kerns (Erpresserbrief, Schutzgeld, Lösegeld mit ihren Befehlen); das Entscheidungswesen hält dazu die Liste der offenen Fragen,
/// beantwortet sie über die bestehenden Befehle und lässt die Voreinstellung gelten, wenn die Frist abläuft.
///
/// Ablauf je Spielminute (am Anfang des Ticks, nach den Befehlen dieser Minute):
///   • Abgleich: Fragen, deren Forderung verschwand, schließen; für neue Forderungen an Häuser von Menschen eine Frage öffnen (in der Reihenfolge
///     der Listen: erst die Erpresserbriefe, dann die Briefe der Diebe). Mehr als <see cref="EntscheidungDaten.HoechstOffen"/> je Haus: die älteste
///     läuft mit ihrer Voreinstellung aus.
///   • Fristablauf in fester Reihenfolge nach Nummer. Bei der Unterwelt gilt „Zahlen“ (Lösegeld, wenn das Geld reicht) am Anfang der letzten Minute
///     des Folgejahres; „Ablehnen“ überlässt der Kern der Jahresrechnung, die schon vor Stufe 3 Schweigen als Nein wertete – so brennt es nach
///     abgelehntem Schutzgeld wie bisher in der nächsten Nacht.
///
/// Wer direkt in der Gauner-Tafel antwortet (<see cref="ErpressungBeantworten"/>, <see cref="DiebesforderungBeantworten"/>), schließt die Frage
/// genauso (<see cref="NachBefehl"/>). Jeder Abschluss steht als eine Meldung der Art <see cref="Meldungsart.Entscheidung"/> im Verlauf.
/// KI-Häuser bekommen keine Fragen; sie antworten wie bisher selbst.
///
/// Deterministisch: nur Spielminuten, feste Reihenfolgen (Listen, Nummern), kein Zufall. Die offenen Fragen stehen im Spielstand (Abschnitt
/// „Wirtschaft“, <see cref="EntscheidungenStand"/>) und damit in der Zustandsprüfsumme.
/// </summary>
public sealed class Entscheidungswesen
{
    /// <summary>Wahl „Zahlen“ bei Erpresserbrief, Schutzgeld und Lösegeld.</summary>
    public const int Zahlen = 0;

    /// <summary>Wahl „Ablehnen“ bei Erpresserbrief, Schutzgeld und Lösegeld.</summary>
    public const int Ablehnen = 1;

    /// <summary>Für <see cref="EntscheidungTreffen"/>: die Voreinstellung, wie sie beim Ausführen gilt (Einstellung „Automatisch“ der Oberfläche).</summary>
    public const int Voreinstellungswahl = -1;

    private readonly Stadtwirtschaft _w;
    private readonly List<Entscheidung> _offen = new();
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
        new[] { $"Zahlen ({Gulden(e.Betrag)})", "Ablehnen" };

    /// <summary>Die Wahl, die jetzt gälte: Lösegeld „Zahlen“ nur, wenn das Geld reicht – sonst „Ablehnen“.</summary>
    public int Voreinstellung(Entscheidung e) =>
        e.Voreinstellung == Zahlen && e.Haus.Vermoegen < e.Betrag ? Ablehnen : e.Voreinstellung;

    /// <summary>Die Überschrift des Briefes.</summary>
    public string Titel(Entscheidung e) => e.Art switch
    {
        Entscheidungsart.Erpresserbrief => $"Erpresserbrief von {Name(e.Absender)}",
        Entscheidungsart.Schutzgeld => $"Schutzgeld für {Name(e.Absender)}",
        _ => $"Lösegeld für {Geiselname(e)}",
    };

    /// <summary>Was der Brief verlangt, in einem Satz.</summary>
    public string Beschreibung(Entscheidung e)
    {
        var von = Name(e.Absender);
        return e.Art switch
        {
            Entscheidungsart.Erpresserbrief => Forderung(e)?.Geheimnis is not null
                ? $"{von} verlangt {Gulden(e.Betrag)} Schweigegeld – sonst kommt ein Geheimnis eures Hauses ans Licht."
                : $"{von} verlangt {Gulden(e.Betrag)} Schweigegeld – sonst bringt er seine Beweise vor Gericht.",
            Entscheidungsart.Schutzgeld => $"Die Leute von {von} verlangen {Gulden(e.Betrag)} Schutzgeld – sonst brennt es.",
            _ => $"{von} hält {Geiselname(e)} fest und verlangt {Gulden(e.Betrag)} Lösegeld – sonst wird {Geiselname(e)} in die Fremde verkauft.",
        };
    }

    /// <summary>
    /// Was eine Wahl bewirkt – für den Tooltip des Knopfes, reiner Text mit Zahlen aus den Rechnungen des Kerns. Was sich nicht zurücknehmen
    /// lässt, steht ausdrücklich da. Ändert nichts.
    /// </summary>
    public string Folgen(Entscheidung e, int wahl)
    {
        if (wahl == Voreinstellungswahl) wahl = Voreinstellung(e);
        if (wahl is not (Zahlen or Ablehnen)) return $"Eine Wahl {wahl} gibt es hier nicht.";
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
        if (wahl == Voreinstellung(e)) teile.Add("Das gilt auch, wenn die Frist abläuft.");
        return string.Join(" ", teile);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Befehl
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="EntscheidungTreffen"/>: wählt über den bestehenden Befehl mit allen seinen Prüfungen. Schlägt er fehl (etwa zu wenig Geld),
    /// bleibt die Frage offen.
    /// </summary>
    internal Befehlsergebnis Treffen(Konto absender, int nummer, int wahl)
    {
        if (MitNummer(nummer) is not { } e)
            return Befehlsergebnis.Abgelehnt($"Eine offene Entscheidung Nr. {nummer} gibt es nicht – vielleicht ist sie schon entschieden.");
        if (e.Haus != absender)
            return Befehlsergebnis.Abgelehnt($"Diese Entscheidung steht {e.Haus.Inhaber} zu.");
        if (wahl == Voreinstellungswahl) wahl = Voreinstellung(e);
        if (wahl is not (Zahlen or Ablehnen))
            return Befehlsergebnis.Abgelehnt($"Eine Wahl {wahl} gibt es hier nicht (0 = Zahlen, 1 = Ablehnen).");
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

    private Befehlsergebnis Antworten(Entscheidung e, int wahl, Entscheidungsabschluss grund)
    {
        if (e.Absender is not { } von) return Befehlsergebnis.Abgelehnt("Diese Forderung hat keinen Absender.");
        Befehl befehl = e.Art == Entscheidungsart.Erpresserbrief
            ? new ErpressungBeantworten(von.Inhaber, wahl == Zahlen)
            : new DiebesforderungBeantworten(von.Inhaber, e.Art == Entscheidungsart.Schutzgeld ? Diebesbriefart.Schutzgeld : Diebesbriefart.Loesegeld, wahl == Zahlen);
        var vorher = _grund;
        _grund = grund;
        try { return _w.Ausfuehren(e.Haus, befehl); } // schließt die Frage über NachBefehl
        finally { _grund = vorher; }
    }

    /// <summary>
    /// Nach jedem erfolgreichen Befehl (aus <see cref="Stadtwirtschaft.Ausfuehren"/>): Hat die Antwort eine Forderung erledigt – über eine
    /// Entscheidung oder direkt in der Gauner-Tafel –, schließt das ihre Frage.
    /// </summary>
    internal void NachBefehl(Konto absender, Befehl befehl)
    {
        bool zahlen;
        if (befehl is ErpressungBeantworten a) zahlen = a.Zahlen;
        else if (befehl is DiebesforderungBeantworten d) zahlen = d.Zahlen;
        else return;
        for (var i = 0; i < _offen.Count; i++)
        {
            var e = _offen[i];
            if (e.Haus != absender || Besteht(e)) continue;
            _offen.RemoveAt(i);
            Schliessen(e, _grund, zahlen ? Zahlen : Ablehnen);
            i--;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Jede Spielminute
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Am Anfang jeder Spielminute (nach den Befehlen dieser Minute): Abgleich, dann Fristablauf nach Nummer.</summary>
    internal void Minute(Spielzeit zeit)
    {
        if (_offen.Count == 0 && _w.Gauner.Forderungen.Count == 0 && _w.Diebe.Briefe.Count == 0) return;
        Abgleichen();
        var jetzt = zeit.VergangeneMinuten;
        foreach (var e in _offen.Where(x => x.Frist <= jetzt).ToList()) // _offen ist nach Nummer geordnet
        {
            if (!_offen.Contains(e)) continue;
            Ausloesen(e, Entscheidungsabschluss.Frist, jetzt);
        }
    }

    /// <summary>Schließt verschwundene Fragen und öffnet neue – in der festen Reihenfolge der Listen.</summary>
    private void Abgleichen()
    {
        var jetzt = _w.Uhr.VergangeneMinuten;
        for (var i = 0; i < _offen.Count; i++)
        {
            var e = _offen[i];
            if (BekommtEntscheidungen(e.Haus) && Besteht(e)) continue;
            _offen.RemoveAt(i);
            i--;
            if (!BekommtEntscheidungen(e.Haus)) { Geschlossen?.Invoke(e, Entscheidungsabschluss.Hinfaellig); continue; } // ohne Meldung: das Haus fragt niemand mehr
            Schliessen(e, jetzt > e.Frist ? Entscheidungsabschluss.Frist : Entscheidungsabschluss.Hinfaellig, null);
        }
        if (_w.Gauner.IstAktiv)
            foreach (var f in _w.Gauner.Forderungen.ToList())
                if (BekommtEntscheidungen(f.Opfer) && _w.Gauner.Forderungen.Contains(f)
                    && !_offen.Any(e => e.Art == Entscheidungsart.Erpresserbrief && e.Haus == f.Opfer && e.Absender == f.Erpresser))
                    Oeffnen(new Entscheidung(0, Entscheidungsart.Erpresserbrief, f.Opfer, f.Erpresser, null, f.Betrag, jetzt, Jahresfrist(f.Jahr), Ablehnen));
        if (_w.Diebe.IstAktiv)
            foreach (var b in _w.Diebe.Briefe.ToList())
            {
                var art = b.Art == Diebesbriefart.Schutzgeld ? Entscheidungsart.Schutzgeld : Entscheidungsart.Loesegeld;
                if (BekommtEntscheidungen(b.Opfer) && _w.Diebe.Briefe.Contains(b)
                    && !_offen.Any(e => e.Art == art && e.Haus == b.Opfer && e.Absender == b.Dieb && e.Person == b.Geisel))
                    Oeffnen(new Entscheidung(0, art, b.Opfer, b.Dieb, b.Geisel, b.Betrag, jetzt, Jahresfrist(b.Jahr),
                        art == Entscheidungsart.Loesegeld ? Zahlen : Ablehnen));
            }
    }

    /// <summary>Die Jahresfrist der Unterwelt: die letzte Spielminute des Folgejahres (die Jahresrechnung am Ende dieser Minute wertet Schweigen als Nein).</summary>
    private static long Jahresfrist(int jahr) => (jahr + 2 - Spielzeit.StartJahr) * (long)Spielzeit.MinutenProTag - 1;

    private void Oeffnen(Entscheidung neu)
    {
        // Obergrenze je Haus: die älteste läuft mit ihrer Voreinstellung aus. Geht auch das nicht, wartet die neue Frage auf die nächste Minute.
        if (_offen.Count(e => e.Haus == neu.Haus) >= Daten.HoechstOffen)
        {
            var aelteste = _offen.Where(e => e.Haus == neu.Haus).OrderBy(e => e.Beginn).ThenBy(e => e.Nummer).First();
            Ausloesen(aelteste, Entscheidungsabschluss.Obergrenze, _w.Uhr.VergangeneMinuten);
            if (_offen.Count(e => e.Haus == neu.Haus) >= Daten.HoechstOffen) return;
        }
        var e = neu with { Nummer = _naechste++ };
        _offen.Add(e);
        Geoeffnet?.Invoke(e);
    }

    /// <summary>
    /// Die Voreinstellung gilt (Fristablauf oder Obergrenze). Schlägt „Zahlen“ fehl, gilt „Ablehnen“. Bei der Unterwelt überlässt der Fristablauf
    /// das Ablehnen der Jahresrechnung in derselben Minute (wie vor Stufe 3); die Frage schließt dann der Abgleich der nächsten Minute.
    /// </summary>
    private void Ausloesen(Entscheidung e, Entscheidungsabschluss grund, long jetzt)
    {
        var jahresfrist = EntscheidungDaten.HatJahresfrist(e.Art);
        if (grund == Entscheidungsabschluss.Frist && jahresfrist && jetzt != e.Frist) return; // die Jahresrechnung steht noch aus (angehaltene Ratssitzung)
        var wahl = Voreinstellung(e);
        if (wahl == Zahlen && Sperrgrund(e) is null && Antworten(e, Zahlen, grund).Erfolg) return;
        if (grund == Entscheidungsabschluss.Frist && jahresfrist) return; // Schweigen heißt Nein – in der Jahresrechnung
        if (Sperrgrund(e) is null) Antworten(e, Ablehnen, grund);
    }

    private void Schliessen(Entscheidung e, Entscheidungsabschluss grund, int? wahl)
    {
        var text = Abschlusstext(e, grund, wahl);
        _w.Melden(Meldungsart.Entscheidung, text, haus: e.Haus, person: e.Person is int n ? _w.Familien.PersonNummer(n) : null, vertraulich: true);
        Geschlossen?.Invoke(e, grund);
    }

    private string Abschlusstext(Entscheidung e, Entscheidungsabschluss grund, int? wahl)
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
        var satz = (e.Art, wahl == Zahlen) switch
        {
            (Entscheidungsart.Erpresserbrief, true) => $"Ihr habt {von} {Gulden(e.Betrag)} Schweigegeld gezahlt.",
            (Entscheidungsart.Erpresserbrief, false) => $"Ihr habt den Erpresserbrief von {von} zurückgewiesen.",
            (Entscheidungsart.Schutzgeld, true) => $"Ihr habt {von} {Gulden(e.Betrag)} Schutzgeld gezahlt.",
            (Entscheidungsart.Schutzgeld, false) => $"Ihr habt die Schutzgeldforderung von {von} zurückgewiesen.",
            (_, true) => $"Ihr habt {von} {Gulden(e.Betrag)} Lösegeld für {Geiselname(e)} gezahlt.",
            _ => $"Ihr habt kein Lösegeld für {Geiselname(e)} gezahlt.",
        };
        return grund switch
        {
            Entscheidungsabschluss.Frist => $"{satz} Die Frist lief ab – es galt die Voreinstellung.",
            Entscheidungsabschluss.Obergrenze => $"{satz} Zu viele offene Entscheidungen – die älteste lief mit ihrer Voreinstellung aus.",
            _ => satz,
        };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Hilfen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Gibt es die Forderung dieser Frage noch?</summary>
    private bool Besteht(Entscheidung e) => e.Art switch
    {
        Entscheidungsart.Erpresserbrief => Forderung(e) is not null,
        _ => _w.Diebe.IstAktiv && _w.Diebe.Briefe.Any(b => b.Art == (e.Art == Entscheidungsart.Schutzgeld ? Diebesbriefart.Schutzgeld : Diebesbriefart.Loesegeld)
                                                            && b.Opfer == e.Haus && b.Dieb == e.Absender && b.Geisel == e.Person),
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
    internal EntscheidungenStand? Sichern(Func<Konto, int> nr) => _naechste == 1 && _offen.Count == 0 ? null : new()
    {
        NaechsteNummer = _naechste,
        Offen = _offen.Select(e => new EntscheidungStand
        {
            Nummer = e.Nummer, Art = e.Art, Haus = nr(e.Haus), Absender = e.Absender is null ? null : nr(e.Absender), Person = e.Person,
            Betrag = e.Betrag, Beginn = e.Beginn, Frist = e.Frist, Voreinstellung = e.Voreinstellung,
        }).ToList(),
    };

    /// <summary>Stellt die offenen Fragen her (nach den Konten). Alte Spielstände: leer – der Abgleich der ersten Minute legt sie aus den Forderungen neu an.</summary>
    internal void Wiederherstellen(EntscheidungenStand? s, IReadOnlyList<Konto> konten)
    {
        _offen.Clear();
        _naechste = 1;
        if (s is null) return;
        _naechste = Math.Max(1, s.NaechsteNummer);
        foreach (var e in s.Offen)
        {
            if (e.Haus < 0 || e.Haus >= konten.Count) continue;
            var absender = e.Absender is int a && a >= 0 && a < konten.Count ? konten[a] : null;
            _offen.Add(new Entscheidung(e.Nummer, e.Art, konten[e.Haus], absender, e.Person, e.Betrag, e.Beginn, e.Frist, e.Voreinstellung));
            _naechste = Math.Max(_naechste, e.Nummer + 1);
        }
    }
}
