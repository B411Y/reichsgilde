using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Politik;

/// <summary>Ein Ratsherr aus der Stadt (kein Spieler, keine KI) – mit fester Haltung zum Statut.</summary>
public sealed class Ratsherr
{
    public RatsherrDaten Daten { get; }
    public string Name => Daten.Name;
    public Fronstatut Haltung => Daten.Haltung;

    internal Ratsherr(RatsherrDaten daten) => Daten = daten;
}

/// <summary>Ein Sitz im Rat. Inhaber ist entweder ein Ratsherr der Stadt oder ein Haus (Spieler/KI) – oder niemand.</summary>
public sealed class Amt
{
    public AmtDaten Daten { get; }
    public string Name => Daten.Name;
    public int Stufe => Daten.Stufe;
    public Ratsherr? Ratsherr { get; internal set; }
    public Konto? Konto { get; internal set; }
    public bool IstBesetzt => Ratsherr is not null || Konto is not null;
    public string Inhaber => Konto?.Inhaber ?? Ratsherr?.Name ?? "unbesetzt";

    internal Amt(AmtDaten daten) => Daten = daten;
}

/// <summary>
/// Wer bei einer Wahl zur Wahl steht: ein Haus (Bewerber oder bisheriger Inhaber) oder der Ratsherr im Amt.
/// <see cref="Name"/> ist der Name, den der Befehl <see cref="WahlStimme"/> erwartet (Hausname bzw. Name des Ratsherrn).
/// </summary>
/// <param name="Haus">Das Haus – null steht für den Ratsherrn im Amt.</param>
/// <param name="IstAmtsinhaber">Hält das Amt bisher (Ratsherr oder Haus).</param>
public sealed record Wahlkandidat(string Name, Konto? Haus, bool IstAmtsinhaber);

/// <summary>
/// Der Stadtrat: sieben Ämter in einer Pyramide, Wahlen, Gunst und Abstimmungen über das Statut.
///
/// Ablauf über das Jahr:
///   • Häuser ab dem Stand Bürger bewerben sich (eine Bewerbung pro Jahr, immer eine Stufe höher als ihr Amt).
///   • Mit Geschenken gewinnen sie die Gunst einzelner Ratsherren.
///   • Wer im Rat sitzt, kann eine Änderung des Fron- und Sklavenstatuts beantragen (ein Antrag pro Jahr).
///   • Häuser, die keine KI sind (der Spieler), legen ihre Stimmen vorab fest und können sie bis zur Sitzung ändern.
///     Ohne Stimme gilt die Vorgabe: bei Wahlen für den bisherigen Inhaber, beim Antrag Enthaltung.
///
/// Ratssitzung am Jahresende (in dieser Reihenfolge):
///   1. Amtseinkommen für alle Häuser im Amt (noch im alten Jahr verbucht)
///   2. Abstimmung über den Antrag – alle Ratsmitglieder, einfache Mehrheit, bei Gleichstand entscheidet der Bürgermeister
///   3. Wahlen – höchste Ämter zuerst. Es wählen die Inhaber der höheren Ämter (beim höchsten Amt alle übrigen).
///      Kandidaten wählen nicht mit. Gleichstand entscheidet das Los.
///   4. Die Gunst verblasst ein Stück.
/// </summary>
public sealed class Stadtrat
{
    private readonly Stadtwirtschaft _w;
    private readonly PolitikDaten _daten;
    private readonly List<Amt> _aemter = new();
    private readonly List<Ratsherr> _ratsherren = new();
    private readonly Dictionary<(Ratsherr, Konto), double> _gunst = new();
    private readonly Dictionary<Konto, string> _bewerbungen = new();
    private readonly List<string> _letzteSitzung = new();

    /// <summary>Festgelegte Stimmen bei Wahlen: (Wähler, Amt) → Haus oder (Haus null) der Ratsherr im Amt.</summary>
    private readonly Dictionary<(Konto Waehler, string Amt), (Konto? Haus, Ratsherr? Ratsherr)> _wahlStimmen = new();

    /// <summary>Festgelegte Stimmen zum Antrag auf ein Statut: Haus → dafür/dagegen (fehlt = Enthaltung).</summary>
    private readonly Dictionary<Konto, bool> _statutStimmen = new();
    private (Konto Antragsteller, Fronstatut Statut)? _antrag;

    public IReadOnlyList<Amt> Aemter => _aemter;
    public IReadOnlyList<Ratsherr> Ratsherren => _ratsherren;
    public PolitikDaten Daten => _daten;
    public bool IstAktiv => _aemter.Count > 0;

    /// <summary>Der Antrag, über den am Jahresende abgestimmt wird (null = keiner).</summary>
    public (Konto Antragsteller, Fronstatut Statut)? Antrag
    {
        get => _antrag;
        private set
        {
            _antrag = value;
            _statutStimmen.Clear(); // ein neuer oder kein Antrag: die Stimmen zum alten gelten nicht mehr
        }
    }

    /// <summary>Bewerbungen für die Wahl am Jahresende: Haus → Amt.</summary>
    public IReadOnlyDictionary<Konto, string> Bewerbungen => _bewerbungen;

    /// <summary>Was in der letzten Ratssitzung geschah (für Jahresbericht und Rathaus).</summary>
    public IReadOnlyList<string> LetzteSitzung => _letzteSitzung;

    internal Stadtrat(Stadtwirtschaft w, PolitikDaten daten)
    {
        _w = w;
        _daten = daten;
        foreach (var a in daten.Aemter.OrderByDescending(a => a.Stufe))
            _aemter.Add(new Amt(a));
        foreach (var r in daten.Ratsherren)
        {
            var ratsherr = new Ratsherr(r);
            _ratsherren.Add(ratsherr);
            if (r.Amt is { } amt && AmtNamens(amt) is { } sitz)
                sitz.Ratsherr = ratsherr;
        }
        foreach (var leer in _aemter.Where(a => !a.IstBesetzt))
            Nachruecken(leer);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Auskunft
    // ═════════════════════════════════════════════════════════════════════════

    public Amt? AmtNamens(string name) => _aemter.FirstOrDefault(a => a.Name == name);
    public Amt? AmtVon(Konto konto) => _aemter.FirstOrDefault(a => a.Konto == konto);
    public Ratsherr? RatsherrNamens(string name) => _ratsherren.FirstOrDefault(r => r.Name == name);

    public double Gunst(Ratsherr ratsherr, Konto konto) => _gunst.GetValueOrDefault((ratsherr, konto));

    /// <summary>Ansehen eines Hauses: Stand und Vermögen, gedeckelt – minus Schande, solange es am Pranger steht.</summary>
    public double Ansehen(Konto konto)
    {
        var g = _daten.Gunst;
        return Math.Min(g.AnsehenHoechstwert, konto.Stand * g.AnsehenJeStand + Math.Max(0, konto.Reinvermoegen) / 1000 * g.AnsehenJe1000Gulden)
               - _w.Justiz.Schande(konto)               // wer am Pranger stand, verliert Ansehen
               - _w.Gesundheit.AnsehenAbzug(konto)      // Haarausfall, Lachanfälle des Oberhaupts
               - _w.Gastwirtschaft.AnsehenAbzug(konto)  // ein Würfeltisch im eigenen Wirtshaus
               - _w.Kirchenwesen.AnsehenAbzug(konto)    // eine Hetzschrift gegen das Haus
               - _w.Diebe.AnsehenAbzug(konto)           // ein Haus von Dieben
               + _w.Wache.AnsehenBonus(konto)           // ein Haus der Wache
               - _w.Bank.AnsehenAbzug(konto)            // ein Wucherkredit läuft
               + _w.Duft.AnsehenBonus(konto)            // Heiligenduft in der Kirche gestiftet
               + _w.Wohnen.AnsehenBonus(konto)          // ein stattliches Wohnhaus
               + _w.Wegebau.AnsehenBonus(konto)         // ein Weg auf Antrag gepflastert
               - _w.Glaube.AnsehenAbzug(konto);         // gerade erst die Konfession gewechselt
    }

    /// <summary>Auf welche Ämter sich ein Haus jetzt bewerben darf: genau eine Stufe über seinem Amt, Stand erfüllt.</summary>
    public IEnumerable<Amt> WaehlbareAemter(Konto konto)
    {
        var stufe = (AmtVon(konto)?.Stufe ?? 0) + 1;
        return _aemter.Where(a => a.Stufe == stufe && konto.Stand >= _w.Daten.StandStufe(a.Daten.MindestStand));
    }

    /// <summary>Wer über dieses Amt abstimmt: die Inhaber höherer Ämter, beim höchsten Amt alle übrigen.</summary>
    public IEnumerable<Amt> Waehler(Amt amt) =>
        amt.Stufe >= _daten.HoechsteStufe
            ? _aemter.Where(a => a != amt && a.IstBesetzt)
            : _aemter.Where(a => a.Stufe > amt.Stufe && a.IstBesetzt);

    /// <summary>Wie viele Punkte ein Ratsherr einem Kandidaten gibt (Konto oder bisheriger Ratsherr).</summary>
    public double Punkte(Ratsherr waehler, Amt amt, Konto? kandidat)
    {
        var g = _daten.Gunst;
        if (kandidat is null) // der bisherige Ratsherr im Amt
            return g.AnsehenRatsherr + amt.Stufe * g.AnsehenRatsherrJeStufe + (amt.Ratsherr is not null ? g.Amtsbonus : 0);
        return Ansehen(kandidat) + Gunst(waehler, kandidat) + _w.Familien.Volksnaehe(waehler.Daten.Volk, kandidat)
               + _w.Glaube.Naehe(waehler, kandidat)
               + _w.Familien.RhetorikPunkte(kandidat)
               + (amt.Konto == kandidat ? g.Amtsbonus : 0);
    }

    /// <summary>Würde dieser Ratsherr dem Statut zustimmen, das dieses Haus beantragt?</summary>
    public bool StimmtZu(Ratsherr ratsherr, Fronstatut statut, Konto antragsteller)
    {
        var abstand = Math.Abs((int)statut - (int)ratsherr.Haltung);
        return abstand == 0 || abstand == 1 && Gunst(ratsherr, antragsteller) >= _daten.Gunst.SchwelleNachbarstatut;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Stimmen der Häuser, die keine KI sind (der Spieler)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Wer bei der Wahl zu diesem Amt zur Wahl steht – so wie die Sitzung sie bildet. Leer = keine Wahl (niemand bewirbt sich).</summary>
    public IReadOnlyList<Wahlkandidat> Kandidaten(Amt amt) =>
        KandidatenListe(amt).Select(k => new Wahlkandidat(k?.Inhaber ?? amt.Ratsherr!.Name, k, k is null || k == amt.Konto)).ToList();

    /// <summary>Darf dieses Haus bei der Wahl zu diesem Amt eine Stimme festlegen? (null = ja, sonst der Grund)</summary>
    public string? WahlStimmeVerboten(Konto konto, Amt amt)
    {
        if (StimmeVerboten(konto) is { } grund) return grund;
        var kandidaten = KandidatenListe(amt);
        if (kandidaten.Count == 0) return $"Um das Amt {amt.Name} bewirbt sich dieses Jahr niemand – es gibt keine Wahl.";
        if (kandidaten.Contains(konto)) return $"{konto.Inhaber} steht bei dieser Wahl selbst zur Wahl und stimmt nicht mit.";
        if (!Waehler(amt).Any(s => s.Konto == konto)) return $"Über das Amt {amt.Name} stimmen nur die Inhaber höherer Ämter ab.";
        return null;
    }

    /// <summary>Die festgelegte Stimme dieses Hauses bei der Wahl zu diesem Amt – null, wenn keine (mehr) gilt; dann stimmt es nach der Vorgabe.</summary>
    public Wahlkandidat? WahlStimmeVon(Konto konto, Amt amt)
    {
        if (WahlStimmeVerboten(konto, amt) is not null || !_wahlStimmen.TryGetValue((konto, amt.Name), out var stimme)) return null;
        var kandidaten = KandidatenListe(amt);
        return IstGueltig(amt, stimme, kandidaten) ? Kandidaten(amt).First(k => k.Haus == stimme.Haus) : null;
    }

    /// <summary>Für wen dieses Haus ohne festgelegte Stimme stimmt: den bisherigen Inhaber (sonst den ersten Bewerber nach dem Namen).</summary>
    public Wahlkandidat? VorgabeStimme(Amt amt)
    {
        var kandidaten = KandidatenListe(amt);
        if (kandidaten.Count == 0) return null;
        var vorgabe = Vorgabe(amt, kandidaten);
        return Kandidaten(amt).First(k => k.Haus == vorgabe);
    }

    /// <summary>Darf dieses Haus zum Antrag auf ein Statut eine Stimme festlegen? (null = ja, sonst der Grund)</summary>
    public string? StatutStimmeVerboten(Konto konto)
    {
        if (StimmeVerboten(konto) is { } grund) return grund;
        if (Antrag is not { } antrag) return "Dieses Jahr liegt kein Antrag zum Statut vor.";
        if (antrag.Antragsteller == konto) return $"{konto.Inhaber} hat den Antrag gestellt und stimmt ohnehin dafür.";
        return null;
    }

    /// <summary>Die festgelegte Stimme dieses Hauses zum vorliegenden Antrag (true = dafür, false = dagegen, null = Enthaltung).</summary>
    public bool? StatutStimmeVon(Konto konto) =>
        StatutStimmeVerboten(konto) is null && _statutStimmen.TryGetValue(konto, out var ja) ? ja : null;

    /// <summary>Gemeinsame Prüfung für alle Stimmen im Rat: Rat vorhanden, keine KI, Mitglied des Rates.</summary>
    internal string? StimmeVerboten(Konto konto)
    {
        if (!IstAktiv) return "In dieser Stadt gibt es keinen Stadtrat.";
        if (_w.Rivalen.Any(r => r.Konto == konto)) return $"{konto.Inhaber} stimmt nach eigenem Ermessen ab.";
        if (AmtVon(konto) is null) return "Abstimmen dürfen nur Mitglieder des Rates.";
        return null;
    }

    /// <summary>Kandidaten einer Wahl: die Bewerber (aktiv, Stand erfüllt, nach Namen) und der bisherige Inhaber (null = der Ratsherr im Amt).</summary>
    private List<Konto?> KandidatenListe(Amt amt)
    {
        var kandidaten = _bewerbungen.Where(b => b.Value == amt.Name).Select(b => b.Key)
            .Where(k => k.IstAktiv && k.Stand >= _w.Daten.StandStufe(amt.Daten.MindestStand))
            .OrderBy(k => k.Inhaber, StringComparer.Ordinal)
            .Select(k => (Konto?)k)
            .ToList();
        if (kandidaten.Count == 0) return kandidaten;
        if (amt.Ratsherr is not null) kandidaten.Add(null);
        else if (amt.Konto is { } inhaber && !kandidaten.Contains(inhaber)) kandidaten.Add(inhaber);
        return kandidaten;
    }

    /// <summary>
    /// Die Stimme eines Hauses im Rat bei einer Wahl: eine KI nach ihrem Charakter, sonst die festgelegte Stimme,
    /// solange sie gültig ist – und ohne sie die Vorgabe.
    /// </summary>
    private Konto? HausStimme(Konto haus, Amt amt, IReadOnlyList<Konto?> kandidaten)
    {
        if (_w.Rivalen.FirstOrDefault(r => r.Konto == haus) is { } ki)
            return ki.Wahlstimme(amt, kandidaten); // Spiel-KI: wählt nach ihrem Charakter
        if (_wahlStimmen.TryGetValue((haus, amt.Name), out var stimme) && IstGueltig(amt, stimme, kandidaten))
            return stimme.Haus;
        return Vorgabe(amt, kandidaten);
    }

    /// <summary>Vorgabe ohne festgelegte Stimme: der bisherige Inhaber, ist das Amt unbesetzt, der erste Bewerber nach dem Namen.</summary>
    private static Konto? Vorgabe(Amt amt, IReadOnlyList<Konto?> kandidaten)
    {
        var inhaber = amt.Ratsherr is not null ? null : amt.Konto;
        if (kandidaten.Contains(inhaber)) return inhaber;
        return kandidaten.Where(k => k is not null).OrderBy(k => k!.Inhaber, StringComparer.Ordinal).FirstOrDefault();
    }

    /// <summary>Steht der Gewählte noch zur Wahl? (Ein Haus unter den Kandidaten bzw. derselbe Ratsherr noch im Amt.)</summary>
    private static bool IstGueltig(Amt amt, (Konto? Haus, Ratsherr? Ratsherr) stimme, IReadOnlyList<Konto?> kandidaten) =>
        stimme.Haus is { } haus ? kandidaten.Contains(haus)
        : stimme.Ratsherr is not null && amt.Ratsherr == stimme.Ratsherr && kandidaten.Contains(null);

    // ═════════════════════════════════════════════════════════════════════════
    //  Prognosen – dieselben Regeln wie in der Sitzung, nur ohne Los
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wie stünde die Wahl heute? Zählt nur Wähler, die das Haus sicher wählen würden
    /// (bei Gleichstand entscheidet in der Sitzung das Los – das zählt hier als „unsicher").
    /// </summary>
    public (int Sicher, int Unsicher, int Waehler) WahlPrognose(Amt amt, Konto bewerber)
    {
        var mitbewerber = _bewerbungen.Where(b => b.Value == amt.Name && b.Key != bewerber).Select(b => (Konto?)b.Key).ToList();
        var gegner = new List<Konto?>(mitbewerber);
        if (amt.Ratsherr is not null) gegner.Add(null);
        else if (amt.Konto is { } inhaber && inhaber != bewerber) gegner.Add(inhaber);

        var alle = new List<Konto?>(gegner) { bewerber };
        int sicher = 0, unsicher = 0, waehlerzahl = 0;
        foreach (var sitz in Waehler(amt).Where(w => w.Konto != bewerber && !mitbewerber.Contains(w.Konto)))
        {
            waehlerzahl++;
            if (sitz.Ratsherr is not { } r)
            {
                // Häuser nach derselben Regel wie in der Wahl: KI nach Charakter, sonst die festgelegte Stimme oder die Vorgabe.
                if (HausStimme(sitz.Konto!, amt, alle) == bewerber) sicher++;
                continue;
            }
            var eigene = Punkte(r, amt, bewerber);
            var beste = gegner.Select(g => Punkte(r, amt, g)).DefaultIfEmpty(double.MinValue).Max();
            if (eigene > beste + 1e-9) sicher++;
            else if (Math.Abs(eigene - beste) <= 1e-9) unsicher++;
        }
        return (sicher, unsicher, waehlerzahl);
    }

    /// <summary>
    /// Wie ginge die Abstimmung über ein Statut heute aus? Häuser, die keine KI sind, stimmen wie festgelegt
    /// (nur beim vorliegenden Antrag) und enthalten sich sonst.
    /// </summary>
    public (int Ja, int Nein, int Enthaltung, bool? Buergermeister) AbstimmungsPrognose(Konto antragsteller, Fronstatut statut)
    {
        int ja = 0, nein = 0, enthaltung = 0;
        bool? buergermeister = null;
        foreach (var amt in _aemter.Where(a => a.IstBesetzt))
        {
            bool? stimme = amt.Ratsherr is { } r ? StimmtZu(r, statut, antragsteller)
                : amt.Konto == antragsteller ? true
                : _w.Rivalen.FirstOrDefault(k => k.Konto == amt.Konto) is { } ki ? ki.StimmtFuerStatut(statut)
                : Antrag == (antragsteller, statut) ? StatutStimmeVon(amt.Konto!) // die festgelegte Stimme gilt nur dem vorliegenden Antrag
                : null;
            if (stimme == true) ja++;
            else if (stimme == false) nein++;
            else enthaltung++;
            if (amt.Stufe == _daten.HoechsteStufe) buergermeister = stimme;
        }
        return (ja, nein, enthaltung, buergermeister);
    }

    /// <summary>Angenommen bei mehr Ja als Nein; bei Gleichstand entscheidet die Stimme des Bürgermeisters.</summary>
    public static bool IstAngenommen(int ja, int nein, bool? buergermeister) => ja > nein || ja == nein && buergermeister == true;

    // ═════════════════════════════════════════════════════════════════════════
    //  Befehle (aufgerufen über Stadtwirtschaft.Ausfuehren)
    // ═════════════════════════════════════════════════════════════════════════

    internal Befehlsergebnis Bewerben(Konto konto, string amtName)
    {
        if (AmtNamens(amtName) is not { } amt)
            return Befehlsergebnis.Abgelehnt($"Ein Amt „{amtName}“ gibt es nicht.");
        if (_bewerbungen.TryGetValue(konto, out var schon))
            return Befehlsergebnis.Abgelehnt($"{konto.Inhaber} hat sich dieses Jahr schon beworben (als {schon}). Nur eine Bewerbung pro Jahr.");
        var stand = _w.Daten.StandStufe(amt.Daten.MindestStand);
        if (konto.Stand < stand)
            return Befehlsergebnis.Abgelehnt($"Für das Amt {amt.Name} braucht es mindestens den Stand {amt.Daten.MindestStand}.");
        var eigene = AmtVon(konto)?.Stufe ?? 0;
        if (amt.Stufe != eigene + 1)
            return Befehlsergebnis.Abgelehnt(eigene == 0
                ? $"Die Laufbahn beginnt unten: zuerst ein Amt der Stufe 1 ({string.Join(" oder ", _aemter.Where(a => a.Stufe == 1).Select(a => a.Name))})."
                : $"Bewerben geht nur eine Stufe höher – {konto.Inhaber} ist {AmtVon(konto)!.Name} (Stufe {eigene}).");
        _bewerbungen[konto] = amt.Name;
        return Befehlsergebnis.Ok($"{konto.Inhaber} bewirbt sich um das Amt {amt.Name}. Gewählt wird am Jahresende.");
    }

    /// <summary>Gunst ohne Geschenk (Stammtisch im Gasthaus) – hinterlässt keine Spur.</summary>
    internal void GunstHinzufuegen(Ratsherr ratsherr, Konto konto, double wert) =>
        _gunst[(ratsherr, konto)] = Math.Min(_daten.Gunst.Hoechstwert, Gunst(ratsherr, konto) + wert);

    /// <summary>Gunst sinkt (Blume der Zwietracht) – höchstens bis zum negativen Höchstwert.</summary>
    internal void GunstMindern(Ratsherr ratsherr, Konto konto, double wert) =>
        _gunst[(ratsherr, konto)] = Math.Max(-_daten.Gunst.Hoechstwert, Gunst(ratsherr, konto) - wert);

    internal Befehlsergebnis Schenken(Konto konto, string ratsherrName, double betrag)
    {
        if (RatsherrNamens(ratsherrName) is not { } ratsherr)
            return Befehlsergebnis.Abgelehnt($"Einen Ratsherrn „{ratsherrName}“ gibt es nicht.");
        if (betrag <= 0 || double.IsNaN(betrag))
            return Befehlsergebnis.Abgelehnt("Ein Geschenk muss etwas wert sein.");
        if (konto.Vermoegen < betrag)
            return Befehlsergebnis.Abgelehnt($"So viel Geld hat {konto.Inhaber} nicht.");
        konto.Geschenk(betrag);
        var neu = Math.Min(_daten.Gunst.Hoechstwert, Gunst(ratsherr, konto) + betrag * _daten.Gunst.JeGulden);
        _gunst[(ratsherr, konto)] = neu;
        _w.Justiz.TatMelden(konto, Justiz.Vergehen.Bestechung, ratsherr.Name, betrag); // jedes Geschenk ist Bestechung
        return Befehlsergebnis.Ok($"{ratsherr.Name} nimmt das Geschenk an. Gunst für {konto.Inhaber}: {Zahlenformat.Ganz(neu)}.");
    }

    internal Befehlsergebnis Beantragen(Konto konto, Fronstatut statut)
    {
        if (AmtVon(konto) is null)
            return Befehlsergebnis.Abgelehnt("Anträge stellen dürfen nur Mitglieder des Rates.");
        if (statut == _w.Statut)
            return Befehlsergebnis.Abgelehnt($"Das Statut {statut.Kuerzel()} gilt bereits.");
        if (Antrag is { } vorliegend)
            return Befehlsergebnis.Abgelehnt($"Für dieses Jahr liegt schon ein Antrag vor ({vorliegend.Antragsteller.Inhaber}: Statut {vorliegend.Statut.Kuerzel()}).");
        Antrag = (konto, statut);
        return Befehlsergebnis.Ok($"{konto.Inhaber} beantragt das Statut {statut.Kuerzel()} – {statut.Name()}. Der Rat stimmt am Jahresende ab.");
    }

    /// <summary>Stimme bei einer Wahl festlegen (Name eines Kandidaten) oder zurücknehmen (null) – gilt bis zur Sitzung.</summary>
    internal Befehlsergebnis WahlStimmeFestlegen(Konto konto, string amtName, string? kandidatName)
    {
        if (AmtNamens(amtName) is not { } amt)
            return Befehlsergebnis.Abgelehnt($"Ein Amt „{amtName}“ gibt es nicht.");
        if (WahlStimmeVerboten(konto, amt) is { } grund)
            return Befehlsergebnis.Abgelehnt(grund);
        var kandidaten = Kandidaten(amt);
        if (kandidatName is null)
        {
            _wahlStimmen.Remove((konto, amt.Name));
            return Befehlsergebnis.Ok($"{konto.Inhaber} nimmt die Stimme zur Wahl {amt.Name} zurück – es gilt die Vorgabe: für {VorgabeStimme(amt)!.Name}.");
        }
        if (kandidaten.FirstOrDefault(k => k.Name == kandidatName) is not { } wahl)
            return Befehlsergebnis.Abgelehnt($"„{kandidatName}“ steht bei der Wahl {amt.Name} nicht zur Wahl. Zur Wahl stehen: {string.Join(", ", kandidaten.Select(k => k.Name))}.");
        _wahlStimmen[(konto, amt.Name)] = (wahl.Haus, wahl.Haus is null ? amt.Ratsherr : null);
        return Befehlsergebnis.Ok($"{konto.Inhaber} stimmt bei der Wahl {amt.Name} für {wahl.Name}. Gewählt wird am Jahresende.");
    }

    /// <summary>Stimme zum Antrag auf ein Statut festlegen (true/false) oder sich enthalten (null) – gilt bis zur Sitzung.</summary>
    internal Befehlsergebnis StatutStimmeFestlegen(Konto konto, bool? ja)
    {
        if (StatutStimmeVerboten(konto) is { } grund)
            return Befehlsergebnis.Abgelehnt(grund);
        var antrag = Antrag!.Value;
        var worum = $"den Antrag von {antrag.Antragsteller.Inhaber} auf Statut {antrag.Statut.Kuerzel()} – {antrag.Statut.Name()}";
        if (ja is not { } stimme)
        {
            _statutStimmen.Remove(konto);
            return Befehlsergebnis.Ok($"{konto.Inhaber} enthält sich bei der Abstimmung über {worum}.");
        }
        _statutStimmen[konto] = stimme;
        return Befehlsergebnis.Ok($"{konto.Inhaber} stimmt {(stimme ? "für" : "gegen")} {worum}. Abgestimmt wird am Jahresende.");
    }

    /// <summary>Ein bankrottes Haus verliert sein Amt, seine Bewerbung, seinen Antrag und seine Stimmen; Stimmen für es verfallen.</summary>
    internal void Ausscheiden(Konto konto)
    {
        foreach (var schluessel in _wahlStimmen.Where(s => s.Key.Waehler == konto || s.Value.Haus == konto).Select(s => s.Key).ToList())
            _wahlStimmen.Remove(schluessel);
        _statutStimmen.Remove(konto);
        if (AmtVon(konto) is { } amt)
        {
            amt.Konto = null;
            Nachruecken(amt);
        }
        _bewerbungen.Remove(konto);
        if (Antrag?.Antragsteller == konto) Antrag = null;
        _w.Glaube.Ausscheiden(konto);
        _w.Wegebau.Ausscheiden(konto);
    }

    /// <summary>Nach einer Verurteilung rückt der bestochene Ratsherr öffentlich vom Haus ab.</summary>
    internal void GunstEntziehen(Ratsherr ratsherr, Konto konto) => _gunst.Remove((ratsherr, konto));

    /// <summary>Zu Beginn der Jahresabrechnung: Das Protokoll der Ratssitzung beginnt leer.
    /// (Früher leerte erst die Sitzung selbst – dabei gingen Nachrücker aus Bankrott und Urteil verloren.)</summary>
    internal void ProtokollBeginnen() => _letzteSitzung.Clear();

    /// <summary>Erbfall: Das neue Oberhaupt beginnt im Rat von vorn – ohne Amt, ohne Bewerbung, ohne Gunst.</summary>
    internal void Neuanfang(Konto konto)
    {
        Ausscheiden(konto);
        foreach (var schluessel in _gunst.Keys.Where(s => s.Item2 == konto).ToList())
            _gunst.Remove(schluessel);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Ratssitzung am Jahresende
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Amtseinkommen – vor dem Jahresabschluss der Konten, damit es im abgelaufenen Jahr steht.</summary>
    internal void EinkommenZahlen()
    {
        foreach (var amt in _aemter.Where(a => a.Konto is not null))
            amt.Konto!.Amtseinkommen(amt.Daten.Einkommen);
    }

    /// <summary>Die Ratssitzung – nach dem Jahresabschluss: Antrag, Wahlen, Verfall der Gunst.</summary>
    internal void Sitzung(Zufallsquelle zufall, int jahr)
    {
        if (!IstAktiv) return;

        if (Antrag is { } antrag)
            Abstimmen(antrag.Antragsteller, antrag.Statut, jahr);
        Antrag = null;
        _w.Glaube.Abstimmen(jahr, _letzteSitzung.Add); // der Stadtglaube nach dem Statut
        _w.Wegebau.RatSitzung(jahr, _letzteSitzung.Add); // Antrag „Weg pflastern“, Pflaster der Stadt

        foreach (var amt in _aemter.OrderByDescending(a => a.Stufe).ThenBy(a => a.Name, StringComparer.Ordinal).ToList())
            Waehlen(amt, zufall, jahr);
        _bewerbungen.Clear();
        _wahlStimmen.Clear(); // neues Jahr, neue Wahl

        var verfall = 1 - _daten.Gunst.VerfallJeJahr;
        foreach (var schluessel in _gunst.Keys.ToList())
            _gunst[schluessel] *= verfall;

        foreach (var eintrag in _letzteSitzung)
            _w.Melden(eintrag);
    }

    private void Abstimmen(Konto antragsteller, Fronstatut statut, int jahr)
    {
        var (ja, nein, _, buergermeister) = AbstimmungsPrognose(antragsteller, statut);
        var angenommen = IstAngenommen(ja, nein, buergermeister);
        _letzteSitzung.Add($"Ratssitzung {jahr}: Antrag von {antragsteller.Inhaber} auf Statut {statut.Kuerzel()} – {statut.Name()} " +
                           $"{(angenommen ? "ANGENOMMEN" : "abgelehnt")} ({ja} dafür, {nein} dagegen{(ja == nein ? ", der Bürgermeister entscheidet" : "")}).");
        if (angenommen)
            _w.StatutAendern(statut);
    }

    private void Waehlen(Amt amt, Zufallsquelle zufall, int jahr)
    {
        var kandidaten = KandidatenListe(amt);
        if (kandidaten.Count == 0) return;

        var stimmen = kandidaten.ToDictionary(k => k ?? Platzhalter, _ => 0);
        foreach (var waehler in Waehler(amt).Where(w => w.Konto is null || !kandidaten.Contains(w.Konto)).ToList())
        {
            Konto? wahl;
            if (waehler.Ratsherr is { } r)
            {
                var beste = kandidaten.Max(k => Punkte(r, amt, k));
                var gleichauf = kandidaten.Where(k => Punkte(r, amt, k) >= beste - 1e-9).ToList();
                wahl = gleichauf.Count == 1 ? gleichauf[0] : gleichauf[zufall.Next(gleichauf.Count)];
            }
            else
                wahl = HausStimme(waehler.Konto!, amt, kandidaten); // KI nach Charakter, der Spieler wie festgelegt
            stimmen[wahl ?? Platzhalter]++;
        }

        var hoechste = stimmen.Values.Max();
        var sieger = stimmen.Where(s => s.Value == hoechste).Select(s => s.Key).ToList();
        var gewinner = sieger.Count == 1 ? sieger[0] : sieger[zufall.Next(sieger.Count)];
        var zaehlung = string.Join(", ", stimmen.Select(s => $"{(s.Key == Platzhalter ? amt.Ratsherr?.Name : s.Key.Inhaber)} {s.Value}"));

        if (gewinner == Platzhalter || gewinner == amt.Konto)
        {
            _letzteSitzung.Add($"Wahl {jahr} – {amt.Name}: {amt.Inhaber} bleibt im Amt ({zaehlung}).");
            return;
        }

        // Neuer Amtsinhaber: Er gibt sein bisheriges Amt ab, das ein Nachrücker übernimmt.
        if (AmtVon(gewinner) is { } altesAmt)
        {
            altesAmt.Konto = null;
            Nachruecken(altesAmt);
        }
        var vorher = amt.Inhaber;
        amt.Ratsherr = null;
        amt.Konto = gewinner;
        _letzteSitzung.Add($"Wahl {jahr} – {amt.Name}: {gewinner.Inhaber} löst {vorher} ab ({zaehlung}).");
    }

    /// <summary>Platzhalter-Konto für „der Ratsherr im Amt" in der Stimmenzählung.</summary>
    private static readonly Konto Platzhalter = new("—", 0, 0);

    /// <summary>Ein freier Sitz geht an den ersten Ratsherrn ohne Amt.</summary>
    private void Nachruecken(Amt amt)
    {
        var frei = _ratsherren.FirstOrDefault(r => _aemter.All(a => a.Ratsherr != r));
        if (frei is null) return; // keine Nachrücker mehr – der Sitz bleibt leer
        amt.Ratsherr = frei;
        _letzteSitzung.Add($"{frei.Name} rückt als {amt.Name} in den Rat nach.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Speichern
    // ═════════════════════════════════════════════════════════════════════════

    internal RatStand Sichern(Func<Konto, int> kontoNummer) => new()
    {
        Aemter = _aemter.ToDictionary(a => a.Name, a => new SitzStand
        {
            Ratsherr = a.Ratsherr?.Name,
            Konto = a.Konto is null ? null : kontoNummer(a.Konto),
        }),
        Gunst = _gunst.OrderBy(g => kontoNummer(g.Key.Item2)).ThenBy(g => g.Key.Item1.Name, StringComparer.Ordinal).Select(g => new GunstStand { Ratsherr = g.Key.Item1.Name, Konto = kontoNummer(g.Key.Item2), Wert = g.Value }).ToList(),
        Bewerbungen = _bewerbungen.ToDictionary(b => kontoNummer(b.Key).ToString(), b => b.Value),
        AntragKonto = Antrag is { } a1 ? kontoNummer(a1.Antragsteller) : null,
        AntragStatut = Antrag?.Statut,
        LetzteSitzung = new(_letzteSitzung),
        WahlStimmen = _wahlStimmen.OrderBy(s => kontoNummer(s.Key.Waehler)).ThenBy(s => s.Key.Amt, StringComparer.Ordinal)
            .Select(s => new WahlStimmeStand
            {
                Konto = kontoNummer(s.Key.Waehler),
                Amt = s.Key.Amt,
                Kandidat = s.Value.Haus is null ? null : kontoNummer(s.Value.Haus),
                Ratsherr = s.Value.Ratsherr?.Name,
            }).ToList(),
        StatutStimmen = _statutStimmen.OrderBy(s => kontoNummer(s.Key)).ToDictionary(s => kontoNummer(s.Key).ToString(), s => s.Value),
    };

    internal void Wiederherstellen(RatStand s, IReadOnlyList<Konto> konten)
    {
        foreach (var amt in _aemter)
        {
            amt.Ratsherr = null;
            amt.Konto = null;
            if (!s.Aemter.TryGetValue(amt.Name, out var sitz)) continue;
            amt.Ratsherr = sitz.Ratsherr is { } r ? RatsherrNamens(r) : null;
            amt.Konto = sitz.Konto is int k ? konten[k] : null;
        }
        _gunst.Clear();
        foreach (var g in s.Gunst)
            if (RatsherrNamens(g.Ratsherr) is { } r)
                _gunst[(r, konten[g.Konto])] = g.Wert;
        _bewerbungen.Clear();
        foreach (var (nummer, amt) in s.Bewerbungen)
            _bewerbungen[konten[int.Parse(nummer)]] = amt;
        Antrag = s.AntragKonto is int ak && s.AntragStatut is { } st ? (konten[ak], st) : null; // leert die Stimmen zum Statut
        _wahlStimmen.Clear();
        foreach (var w in s.WahlStimmen)
        {
            var ratsherr = w.Ratsherr is { } name ? RatsherrNamens(name) : null;
            if (w.Kandidat is int kandidat) _wahlStimmen[(konten[w.Konto], w.Amt)] = (konten[kandidat], null);
            else if (ratsherr is not null) _wahlStimmen[(konten[w.Konto], w.Amt)] = (null, ratsherr);
        }
        foreach (var (nummer, ja) in s.StatutStimmen)
            _statutStimmen[konten[int.Parse(nummer)]] = ja;
        _letzteSitzung.Clear();
        _letzteSitzung.AddRange(s.LetzteSitzung);
    }
}
