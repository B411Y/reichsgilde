using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Kirche;

/// <summary>
/// Der Stadt-Dom als Amt auf Zeit (Abschnitt <c>kirche.dom</c>, ab Datenversion 85; Nutzer, 11.11.2026). Fehlt der Abschnitt, gibt es kein Amt.
/// </summary>
public sealed class DomDaten
{
    /// <summary>So viel der Gemeinde seiner Konfession geht in den Dom statt in die eigene Kirche dieser Konfession (entschieden: 60 %).</summary>
    public double Anteil { get; init; } = 0.6;

    /// <summary>So viele Jahre dauert eine Amtszeit (entschieden: 3); danach steht das Amt wieder zur Wahl, Wiederwahl erlaubt.</summary>
    public int Amtsjahre { get; init; } = 3;

    /// <summary>Die Bekehrungspredigt des Dompredigers wirkt so viel stärker (Schätzung 1,5).</summary>
    public double PredigtFaktor { get; init; } = 1.5;

    /// <summary>Meinung der Ratsherren seiner Konfession über ihn, jedes Jahr am Jahresende (Grund „Domprediger“, Schätzung +3).</summary>
    public double Gunst { get; init; } = 3;

    public void Pruefen(List<string> f)
    {
        if (Anteil is < 0 or > 1) f.Add("kirche.dom.anteil muss zwischen 0 und 1 liegen.");
        if (Amtsjahre < 1) f.Add("kirche.dom.amtsjahre muss mindestens 1 sein.");
        if (PredigtFaktor <= 0) f.Add("kirche.dom.predigtFaktor muss größer als 0 sein.");
        if (Gunst < 0) f.Add("kirche.dom.gunst darf nicht negativ sein.");
    }
}

/// <summary>
/// Der Domprediger: Der Stadt-Dom der Karte (kein Betrieb) bekommt auf Zeit einen Inhaber.
///   • Bewerben darf sich jedes Haus mit einer eigenen Kirche (Prediger) und kirchlichem Glauben; gilt ein Stadtglaube, nur mit diesem.
///   • Der Rat wählt am Jahresende im Punkt „Domprediger“ (nach dem Stadtglauben, vor den Ämterwahlen); alle Sitze stimmen ab,
///     außer den Kandidaten selbst. Ratsherren nach Punkten (Ansehen, Meinung, Rhetorik, Amtsbonus für den Inhaber), Gleichstand: das Los.
///   • Eine Amtszeit dauert <see cref="DomDaten.Amtsjahre"/> Jahre. Gewählt wird nur, wenn das Amt frei ist oder im letzten Amtsjahr;
///     dann steht der Inhaber von selbst wieder zur Wahl (Wiederwahl), solange er die Bedingungen erfüllt.
///   • Das Amt endet vorher: bei einem anderen Stadtglauben, wenn das Haus übertritt, bei Verurteilung, Bankrott, Erlöschen und Erbfall.
///   • Kein Gehalt aus der Stadtkasse. Der Dom nimmt <see cref="DomDaten.Anteil"/> der Gemeinde seiner Konfession (siehe
///     <see cref="Kirchenwesen.Verteilen"/>); die Kollekte geht als „Domkollekte“ (steuerfrei) an das Haus.
///   • Wirkung: die Bekehrungspredigt im Dom (× <see cref="DomDaten.PredigtFaktor"/>) und Gunst bei den Ratsherren seiner Konfession.
/// Ist das Amt leer, rechnet alles wie ohne Dom-Amt.
/// </summary>
public sealed class Domamt
{
    /// <summary>Der Name des Amtes in Meldungen, Chronik und Sitzung.</summary>
    public const string Amtsname = "Domprediger";

    private readonly Stadtwirtschaft _w;
    private readonly DomDaten? _d;
    private readonly List<Konto> _bewerbungen = new();
    private readonly Dictionary<Konto, Konto> _stimmen = new(); // Wähler → Kandidat (fehlt = Vorgabe)
    private Kirchenbuch _diesesJahr = new(0, 0, 0, 0);
    private Kirchenbuch? _vorjahr;

    internal Domamt(Stadtwirtschaft w, DomDaten? daten)
    {
        _w = w;
        _d = daten;
    }

    /// <summary>Gibt es das Amt? Nur mit <c>kirche.dom</c>, Kirchen, Glauben und Stadtrat.</summary>
    public bool IstAktiv => _d is not null && _w.Kirchenwesen.IstAktiv && _w.Glaube.IstAktiv && _w.Rat.IstAktiv;

    public DomDaten Daten => _d ?? throw new InvalidOperationException("Es gibt kein Dom-Amt (kirche.dom fehlt in den Daten).");

    /// <summary>Das Haus des Dompredigers (null = das Amt ist frei).</summary>
    public Konto? Inhaber { get; private set; }

    /// <summary>Das letzte Amtsjahr: In der Ratssitzung dieses Jahres steht das Amt wieder zur Wahl (0, wenn frei).</summary>
    public int Bis { get; private set; }

    /// <summary>Die Konfession des Doms = die des Dompredigers (null, wenn das Amt frei ist).</summary>
    public Konfession? Konfession => Inhaber is { } i ? _w.Glaube.GlaubeVon(i) : null;

    /// <summary>Wer sich dieses Jahr beworben hat, in der Reihenfolge der Konten.</summary>
    public IReadOnlyList<Konto> Bewerbungen => _w.Konten.Where(_bewerbungen.Contains).ToList();

    /// <summary>Gläubige und Domkollekte dieses Jahres bzw. des Vorjahres (Ablässe gibt es im Dom nicht).</summary>
    public Kirchenbuch DiesesJahr => _diesesJahr;
    public Kirchenbuch? Vorjahr => _vorjahr;

    public bool IstDomprediger(Konto konto) => Inhaber == konto && IstAktiv;

    /// <summary>Das Jahr, dessen Ratssitzung als Nächstes wählt (während der Sitzung das Jahr der Sitzung).</summary>
    private int Wahljahr => _w.Rat.LaufendeSitzung?.Jahr ?? _w.Jahr;

    /// <summary>Steht das Amt in der Sitzung dieses Jahres zur Wahl? Frei oder im letzten Amtsjahr.</summary>
    public bool IstWahljahr(int jahr) => Inhaber is null || Bis <= jahr;

    // ── Wer darf ────────────────────────────────────────────────────────────

    /// <summary>
    /// Erfüllt dieses Haus die Bedingungen des Amtes? (null = ja, sonst der Grund) Eine eigene Kirche (Prediger), ein kirchlicher Glaube
    /// und – gilt ein Stadtglaube – dieser.
    /// </summary>
    public string? EignungVerboten(Konto konto)
    {
        if (!IstAktiv) return "In dieser Stadt gibt es kein Amt des Dompredigers.";
        if (!konto.IstAktiv) return $"{konto.Inhaber} ist nicht mehr im Spiel.";
        if (!_w.Betriebe.Any(b => b.Konto == konto && _w.Kirchenwesen.IstKirche(b)))
            return "Domprediger wird nur ein Prediger mit eigener Kirche.";
        if (_w.Glaube.GlaubeVon(konto) is not { } glaube || !glaube.IstKirchlich())
            return "Domprediger wird nur, wer einem kirchlichen Glauben folgt.";
        if (_w.Glaube.Stadtglaube is { } stadt && stadt != glaube)
            return $"Der Stadtglaube ist der {stadt.Glaube()} – Domprediger wird nur, wer ihm folgt.";
        return null;
    }

    /// <summary>Darf sich dieses Haus jetzt um das Amt bewerben? (null = ja, sonst der Grund)</summary>
    public string? BewerbenVerboten(Konto konto)
    {
        if (EignungVerboten(konto) is { } grund) return grund;
        var jahr = Wahljahr;
        if (Inhaber == konto)
            return Bis <= jahr ? $"{konto.Inhaber} steht als Domprediger ohnehin wieder zur Wahl." : $"{konto.Inhaber} ist schon Domprediger bis Ende {Bis}.";
        if (!IstWahljahr(jahr))
            return $"Das Amt ist vergeben bis Ende {Bis} ({Inhaber!.Inhaber}). Bewerben kann man sich im letzten Amtsjahr.";
        if (_bewerbungen.Contains(konto)) return $"{konto.Inhaber} hat sich dieses Jahr schon um das Amt beworben.";
        return null;
    }

    internal Befehlsergebnis Bewerben(Konto konto)
    {
        if (BewerbenVerboten(konto) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        _bewerbungen.Add(konto);
        return Befehlsergebnis.Ok($"{konto.Inhaber} bewirbt sich um das Amt des Dompredigers. Der Rat wählt am Jahresende.");
    }

    // ── Kandidaten und Stimmen ──────────────────────────────────────────────

    /// <summary>
    /// Wer bei der Wahl dieses Jahres zur Wahl steht: die Bewerber, die die Bedingungen noch erfüllen, und im Wahljahr der Inhaber, wenn er
    /// sie noch erfüllt – in der Reihenfolge der Konten. Leer = keine Wahl.
    /// </summary>
    internal List<Konto> KandidatenListe(int jahr)
    {
        if (!IstAktiv || !IstWahljahr(jahr)) return new();
        return _w.Konten.Where(k => (_bewerbungen.Contains(k) || k == Inhaber) && EignungVerboten(k) is null).ToList();
    }

    /// <summary>Die Kandidaten der nächsten Wahl (wie in der Sitzung), der Inhaber markiert.</summary>
    public IReadOnlyList<Wahlkandidat> Kandidaten() =>
        KandidatenListe(Wahljahr).Select(k => new Wahlkandidat(k.Inhaber, k, k == Inhaber)).ToList();

    /// <summary>Wer abstimmt: alle besetzten Sitze im Rat, außer den Häusern, die selbst zur Wahl stehen.</summary>
    internal List<Amt> Waehler(IReadOnlyList<Konto> kandidaten) =>
        _w.Rat.Aemter.Where(a => a.IstBesetzt && (a.Konto is null || !kandidaten.Contains(a.Konto))).ToList();

    /// <summary>Punkte eines Ratsherrn für einen Kandidaten: Ansehen, Meinung über das Oberhaupt, Rhetorik und der Amtsbonus für den Inhaber.</summary>
    public double Punkte(Ratsherr waehler, Konto kandidat) =>
        _w.Rat.Ansehen(kandidat) + _w.Rat.Meinung(waehler, kandidat) + _w.Familien.RhetorikPunkte(kandidat)
        + (kandidat == Inhaber ? _w.Rat.Daten.Gunst.Amtsbonus : 0);

    /// <summary>
    /// Für wen dieser Sitz stimmt – in Vorschau und Sitzung dieselbe Regel. Ein Ratsherr nach Punkten (bei Gleichstand alle gleichauf, in der
    /// Sitzung zieht das Los), eine KI nach ihrem Charakter, sonst die festgelegte Stimme oder die Vorgabe. Erpresst: wie der Sitz des
    /// Erpressers (wie bei den Anträgen, <see cref="Stadtrat.StimmSitz"/>).
    /// </summary>
    internal List<Konto> StimmeVonSitz(Amt sitz, IReadOnlyList<Konto> kandidaten)
    {
        var stimmSitz = _w.Rat.StimmSitz(sitz);
        if (stimmSitz.Ratsherr is { } r)
        {
            var beste = kandidaten.Max(k => Punkte(r, k));
            return kandidaten.Where(k => Punkte(r, k) >= beste - 1e-9).ToList();
        }
        var haus = stimmSitz.Konto!;
        if (kandidaten.Contains(haus)) return new List<Konto> { haus }; // der Erpresser steht selbst zur Wahl
        if (_w.Rivalen.FirstOrDefault(x => x.Konto == haus) is { } ki)
            return new List<Konto> { ki.Wahlstimme(Inhaber, kandidaten.Select(k => (Konto?)k).ToList()) ?? kandidaten[0] };
        return new List<Konto> { _stimmen.TryGetValue(haus, out var wahl) && kandidaten.Contains(wahl) ? wahl : Vorgabe(kandidaten) };
    }

    /// <summary>Ohne festgelegte Stimme: der bisherige Inhaber, sonst der erste Kandidat (Reihenfolge der Konten).</summary>
    private Konto Vorgabe(IReadOnlyList<Konto> kandidaten) => Inhaber is { } i && kandidaten.Contains(i) ? i : kandidaten[0];

    /// <summary>Für wen ein Haus ohne festgelegte Stimme stimmt (null = keine Wahl).</summary>
    public Wahlkandidat? VorgabeStimme()
    {
        var kandidaten = KandidatenListe(Wahljahr);
        if (kandidaten.Count == 0) return null;
        var v = Vorgabe(kandidaten);
        return new Wahlkandidat(v.Inhaber, v, v == Inhaber);
    }

    /// <summary>Darf dieses Haus bei der Wahl des Dompredigers eine Stimme festlegen? (null = ja, sonst der Grund)</summary>
    public string? StimmeVerboten(Konto konto)
    {
        if (!IstAktiv) return "In dieser Stadt gibt es kein Amt des Dompredigers.";
        if (_w.Rat.StimmeVerboten(konto) is { } grund) return grund; // Mitglied des Rates, keine KI, nicht erpresst
        var kandidaten = KandidatenListe(Wahljahr);
        if (kandidaten.Count == 0) return "Um das Amt des Dompredigers wird dieses Jahr nicht gewählt.";
        if (kandidaten.Contains(konto)) return $"{konto.Inhaber} steht bei dieser Wahl selbst zur Wahl und stimmt nicht mit.";
        return null;
    }

    /// <summary>Die festgelegte Stimme dieses Hauses (null = keine; dann gilt die Vorgabe).</summary>
    public Wahlkandidat? StimmeVon(Konto konto)
    {
        if (StimmeVerboten(konto) is not null || !_stimmen.TryGetValue(konto, out var wahl)) return null;
        return KandidatenListe(Wahljahr).Contains(wahl) ? new Wahlkandidat(wahl.Inhaber, wahl, wahl == Inhaber) : null;
    }

    internal bool HatStimme(Konto konto, IReadOnlyList<Konto> kandidaten) => _stimmen.TryGetValue(konto, out var w) && kandidaten.Contains(w);

    /// <summary>Stimme festlegen (Name eines Kandidaten) oder zurücknehmen (null) – gilt bis zur Sitzung.</summary>
    internal Befehlsergebnis StimmeFestlegen(Konto konto, string? kandidatName)
    {
        if (StimmeVerboten(konto) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var kandidaten = KandidatenListe(Wahljahr);
        if (kandidatName is null)
        {
            _stimmen.Remove(konto);
            return Befehlsergebnis.Ok($"{konto.Inhaber} nimmt die Stimme zur Wahl des Dompredigers zurück – es gilt die Vorgabe: für {Vorgabe(kandidaten).Inhaber}.");
        }
        if (kandidaten.FirstOrDefault(k => k.Inhaber == kandidatName) is not { } wahl)
            return Befehlsergebnis.Abgelehnt($"„{kandidatName}“ steht bei der Wahl des Dompredigers nicht zur Wahl. Zur Wahl stehen: {string.Join(", ", kandidaten.Select(k => k.Inhaber))}.");
        _stimmen[konto] = wahl;
        return Befehlsergebnis.Ok($"{konto.Inhaber} stimmt bei der Wahl des Dompredigers für {wahl.Inhaber}. Gewählt wird am Jahresende.");
    }

    // ── Die Wahl in der Ratssitzung ─────────────────────────────────────────

    /// <summary>
    /// Der Punkt „Domprediger“: null, wenn niemand zur Wahl steht. Gleichstand bei einem Ratsherrn bzw. an der Spitze: das Los aus der Quelle
    /// der Sitzung. Der Gewinner ist <see cref="DomDaten.Amtsjahre"/> Jahre im Amt (bis Ende <paramref name="jahr"/> + Amtsjahre).
    /// </summary>
    internal Stadtrat.Wahlausgang? Waehlen(Zufallsquelle zufall, int jahr)
    {
        var kandidaten = KandidatenListe(jahr);
        if (kandidaten.Count == 0) return null;
        var stimmen = kandidaten.Select(_ => 0).ToArray();
        var einzeln = new List<(Amt, Konto?, bool)>();
        foreach (var waehler in Waehler(kandidaten))
        {
            var gleichauf = StimmeVonSitz(waehler, kandidaten);
            var wahl = gleichauf.Count == 1 ? gleichauf[0] : gleichauf[zufall.Next(gleichauf.Count)];
            einzeln.Add((waehler, wahl, gleichauf.Count > 1));
            stimmen[kandidaten.IndexOf(wahl)]++;
        }
        var hoechste = stimmen.Max();
        var sieger = Enumerable.Range(0, kandidaten.Count).Where(i => stimmen[i] == hoechste).ToList();
        var gewinner = kandidaten[sieger.Count == 1 ? sieger[0] : sieger[zufall.Next(sieger.Count)]];
        var zaehlung = string.Join(", ", kandidaten.Select((k, i) => $"{k.Inhaber} {stimmen[i]}"));
        var ausgang = new Stadtrat.Wahlausgang(einzeln, kandidaten.Select((k, i) => ((Konto?)k, stimmen[i])).ToList(), gewinner, sieger.Count > 1);

        var vorher = Inhaber;
        Inhaber = gewinner;
        Bis = jahr + Daten.Amtsjahre;
        if (gewinner == vorher)
            _w.Rat.Protokollieren($"Wahl {jahr} – {Amtsname}: {gewinner.Inhaber} bleibt im Amt bis Ende {Bis} ({zaehlung}).");
        else
        {
            _w.Rat.Protokollieren($"Wahl {jahr} – {Amtsname}: {gewinner.Inhaber} wird Domprediger bis Ende {Bis} ({zaehlung}).");
            if (vorher is not null) _w.Familien.AmtVerloren(vorher, Amtsname, "abgewählt");
            _w.Familien.AmtErhalten(gewinner, Amtsname);
        }
        return ausgang;
    }

    /// <summary>
    /// Nach dem Punkt (bzw. ohne Punkt): Ist die Amtszeit um und wurde niemand gewählt (der Inhaber erfüllt die Bedingungen nicht mehr,
    /// niemand bewirbt sich), wird das Amt frei.
    /// </summary>
    internal void AmtszeitPruefen(int jahr)
    {
        if (Inhaber is { } i && Bis <= jahr)
            Ausscheiden(i, "Amtszeit abgelaufen", _w.Rat.Protokollieren);
    }

    /// <summary>Letzter Schritt der Sitzung: Bewerbungen und Stimmen verfallen; beginnt das letzte Amtsjahr, eine Meldung.</summary>
    internal void SitzungVorbei(int jahr)
    {
        _bewerbungen.Clear();
        _stimmen.Clear();
        if (Inhaber is { } i && Bis == jahr + 1)
            _w.Melden(Meldungsart.Wahl, $"Die Amtszeit von {i.Inhaber} als Domprediger endet dieses Jahr – am Jahresende wählt der Rat neu.", haus: i);
    }

    /// <summary>Setzt einen Domprediger ohne Wahl ein (Tests; ohne Prüfung der Bedingungen).</summary>
    internal void Einsetzen(Konto konto, int bis)
    {
        Inhaber = konto;
        Bis = bis;
    }

    // ── Ende des Amtes ──────────────────────────────────────────────────────

    /// <summary>
    /// Ein Haus scheidet aus (Bankrott, Erlöschen, Erbfall, Verurteilung, Übertritt): Ist es Domprediger, ist das Amt frei; seine Bewerbung und
    /// seine Stimme verfallen, Stimmen für es auch.
    /// </summary>
    internal void Ausscheiden(Konto konto, string wie, Action<string>? protokoll = null)
    {
        _bewerbungen.Remove(konto);
        _stimmen.Remove(konto);
        foreach (var w in _stimmen.Where(s => s.Value == konto).Select(s => s.Key).ToList()) _stimmen.Remove(w);
        if (Inhaber != konto) return;
        Inhaber = null;
        Bis = 0;
        _w.Familien.AmtVerloren(konto, Amtsname, wie);
        var text = $"{konto.Inhaber} ist nicht mehr Domprediger ({wie}) – das Amt ist frei.";
        if (protokoll is not null) protokoll(text);
        else _w.Melden(Meldungsart.Wahl, text, haus: konto);
    }

    /// <summary>Der Rat hat einen Stadtglauben beschlossen: Folgt der Domprediger ihm nicht, verliert er das Amt (Neuwahl in derselben Sitzung).</summary>
    internal void StadtglaubeGeaendert(Action<string> protokoll)
    {
        if (Inhaber is { } i && _w.Glaube.Stadtglaube is { } stadt && _w.Glaube.GlaubeVon(i) != stadt)
            Ausscheiden(i, "anderer Stadtglaube", protokoll);
    }

    // ── Kollekte und Gunst ──────────────────────────────────────────────────

    /// <summary>Zur Messe: der Anteil des Doms (aus <see cref="Kirchenwesen.Verteilen"/>) – die Kollekte geht steuerfrei an das Haus.</summary>
    internal void KollekteBuchen(double glaeubige, double kollekte)
    {
        if (Inhaber is not { } i || glaeubige <= 0) return;
        i.Domkollekte(kollekte);
        _diesesJahr = _diesesJahr with { Glaeubige = _diesesJahr.Glaeubige + glaeubige, Kollekte = _diesesJahr.Kollekte + kollekte };
    }

    /// <summary>Jahresende (vor der Ratssitzung): Gunst bei den Ratsherren seiner Konfession, neues Kirchenbuch des Doms.</summary>
    internal void Jahresende()
    {
        if (IstAktiv && Inhaber is { } i && Konfession is { } k && Daten.Gunst != 0)
            foreach (var r in _w.Rat.Ratsherren.Where(r => _w.Glaube.GlaubeVon(r) == k))
                _w.Rat.GunstAendern(r, i, "domprediger", Amtsname, Daten.Gunst);
        _vorjahr = _diesesJahr.Glaeubige > 0 ? _diesesJahr : null; // ohne Domprediger kein Kirchenbuch
        _diesesJahr = new Kirchenbuch(0, 0, 0, 0);
    }

    // ── Speichern ───────────────────────────────────────────────────────────

    /// <summary>null, solange es nichts zu speichern gibt (alte Spielstände bleiben gleich).</summary>
    internal DomStand? Sichern(Func<Konto, int> nr) =>
        Inhaber is null && _bewerbungen.Count == 0 && _stimmen.Count == 0 && _diesesJahr.Glaeubige == 0 && _vorjahr is null ? null : new()
        {
            Inhaber = Inhaber is { } i ? nr(i) : null,
            Bis = Bis,
            Bewerbungen = _bewerbungen.Select(nr).OrderBy(x => x).ToList(),
            Stimmen = _stimmen.OrderBy(x => nr(x.Key)).ToDictionary(x => nr(x.Key).ToString(), x => nr(x.Value)),
            DiesesJahr = _diesesJahr,
            Vorjahr = _vorjahr,
        };

    internal void Wiederherstellen(DomStand? s, IReadOnlyList<Konto> konten)
    {
        _bewerbungen.Clear();
        _stimmen.Clear();
        Inhaber = s?.Inhaber is int i ? konten[i] : null;
        Bis = s?.Bis ?? 0;
        _diesesJahr = s?.DiesesJahr ?? new Kirchenbuch(0, 0, 0, 0);
        _vorjahr = s?.Vorjahr;
        if (s is null) return;
        foreach (var b in s.Bewerbungen) _bewerbungen.Add(konten[b]);
        foreach (var (w, k) in s.Stimmen) _stimmen[konten[int.Parse(w)]] = konten[k];
    }
}
