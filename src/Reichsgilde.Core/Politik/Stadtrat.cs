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
/// Der Stadtrat: sieben Ämter in einer Pyramide, Wahlen, Gunst und Abstimmungen über das Statut.
///
/// Ablauf über das Jahr:
///   • Häuser ab dem Stand Bürger bewerben sich (eine Bewerbung pro Jahr, immer eine Stufe höher als ihr Amt).
///   • Mit Geschenken gewinnen sie die Gunst einzelner Ratsherren.
///   • Wer im Rat sitzt, kann eine Änderung des Fron- und Sklavenstatuts beantragen (ein Antrag pro Jahr).
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

    public IReadOnlyList<Amt> Aemter => _aemter;
    public IReadOnlyList<Ratsherr> Ratsherren => _ratsherren;
    public PolitikDaten Daten => _daten;
    public bool IstAktiv => _aemter.Count > 0;

    /// <summary>Der Antrag, über den am Jahresende abgestimmt wird (null = keiner).</summary>
    public (Konto Antragsteller, Fronstatut Statut)? Antrag { get; private set; }

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
        return Math.Min(g.AnsehenHoechstwert, konto.Stand * g.AnsehenJeStand + Math.Max(0, konto.Vermoegen) / 1000 * g.AnsehenJe1000Gulden)
               - _w.Justiz.Schande(konto)               // wer am Pranger stand, verliert Ansehen
               - _w.Gesundheit.AnsehenAbzug(konto)      // Haarausfall, Lachanfälle des Oberhaupts
               - _w.Gastwirtschaft.AnsehenAbzug(konto); // ein Würfeltisch im eigenen Wirtshaus
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

        int sicher = 0, unsicher = 0, waehlerzahl = 0;
        foreach (var sitz in Waehler(amt).Where(w => w.Konto != bewerber && !mitbewerber.Contains(w.Konto)))
        {
            waehlerzahl++;
            if (sitz.Ratsherr is not { } r) continue; // Häuser im Rat stimmen für den bisherigen Inhaber
            var eigene = Punkte(r, amt, bewerber);
            var beste = gegner.Select(g => Punkte(r, amt, g)).DefaultIfEmpty(double.MinValue).Max();
            if (eigene > beste + 1e-9) sicher++;
            else if (Math.Abs(eigene - beste) <= 1e-9) unsicher++;
        }
        return (sicher, unsicher, waehlerzahl);
    }

    /// <summary>Wie ginge die Abstimmung über ein Statut heute aus? Enthaltungen sind Häuser, die keine KI sind.</summary>
    public (int Ja, int Nein, int Enthaltung, bool? Buergermeister) AbstimmungsPrognose(Konto antragsteller, Fronstatut statut)
    {
        int ja = 0, nein = 0, enthaltung = 0;
        bool? buergermeister = null;
        foreach (var amt in _aemter.Where(a => a.IstBesetzt))
        {
            bool? stimme = amt.Ratsherr is { } r ? StimmtZu(r, statut, antragsteller)
                : amt.Konto == antragsteller ? true
                : _w.Rivalen.FirstOrDefault(k => k.Konto == amt.Konto) is { } ki ? ki.StimmtFuerStatut(statut)
                : null; // Spieler ohne eigenen Antrag: enthält sich (Abstimmen im Rat folgt später)
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

    /// <summary>Ein bankrottes Haus verliert sein Amt, seine Bewerbung und seinen Antrag.</summary>
    internal void Ausscheiden(Konto konto)
    {
        if (AmtVon(konto) is { } amt)
        {
            amt.Konto = null;
            Nachruecken(amt);
        }
        _bewerbungen.Remove(konto);
        if (Antrag?.Antragsteller == konto) Antrag = null;
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

        foreach (var amt in _aemter.OrderByDescending(a => a.Stufe).ThenBy(a => a.Name, StringComparer.Ordinal).ToList())
            Waehlen(amt, zufall, jahr);
        _bewerbungen.Clear();

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
        var bewerber = _bewerbungen.Where(b => b.Value == amt.Name).Select(b => b.Key)
            .Where(k => k.IstAktiv && k.Stand >= _w.Daten.StandStufe(amt.Daten.MindestStand))
            .OrderBy(k => k.Inhaber, StringComparer.Ordinal)
            .ToList();
        if (bewerber.Count == 0) return;

        // Kandidaten: alle Bewerber und der bisherige Inhaber (null steht für den Ratsherrn im Amt).
        var kandidaten = new List<Konto?>(bewerber);
        if (amt.Ratsherr is not null) kandidaten.Add(null);
        else if (amt.Konto is { } inhaber && !kandidaten.Contains(inhaber)) kandidaten.Add(inhaber);

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
            {
                // Ein Haus im Rat stimmt (noch) für den bisherigen Inhaber – Wahlabsprachen folgen später.
                wahl = kandidaten.Contains(amt.Konto) ? amt.Konto : null;
                if (wahl is null && !kandidaten.Contains(null)) wahl = kandidaten[0];
            }
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
        Antrag = s.AntragKonto is int ak && s.AntragStatut is { } st ? (konten[ak], st) : null;
        _letzteSitzung.Clear();
        _letzteSitzung.AddRange(s.LetzteSitzung);
    }
}
