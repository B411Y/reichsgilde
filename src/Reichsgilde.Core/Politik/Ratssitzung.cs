using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Politik;

/// <summary>Worüber in einem Punkt der Ratssitzung abgestimmt wird – in dieser Reihenfolge (Wahlen: höchstes Amt zuerst).</summary>
public enum Sitzungsthema
{
    Statut,
    Stadtglaube,
    Pflaster,
    Steuersatz,
    Bauvorhaben,
    Fest,
    Wahl,
}

/// <summary>
/// Die Stimme eines Sitzes am Ratstisch zu einem Punkt der Sitzung.
/// </summary>
/// <param name="Sitz">Das Amt – der Platz am Tisch.</param>
/// <param name="Name">Der Ratsherr oder das Haus.</param>
/// <param name="Haus">Das Haus; null für einen Ratsherrn der Stadt.</param>
/// <param name="Ja">Bei Anträgen: true dafür, false dagegen, null Enthaltung. Bei Wahlen immer null.</param>
/// <param name="Kandidat">Bei Wahlen: für wen (Name wie in <see cref="Stadtrat.Kandidaten"/>); bei Anträgen null.</param>
/// <param name="Los">Bei Wahlen: Ein Ratsherr hat mehrere Kandidaten gleichauf – das Los entscheidet. In der Vorschau steht dann der erste
/// von ihnen in <paramref name="Kandidat"/>, im Ergebnis der gezogene.</param>
/// <param name="Begruendung">Ein kurzer Satz aus der Rechnung (Ratsherr, KI) bzw. woher die Stimme kommt (Spieler).</param>
/// <param name="Frei">Ein Haus, das keine KI ist, darf seine Stimme hier noch setzen (mit den Befehlen <c>WahlStimme</c>,
/// <c>StatutStimme</c>, <c>StadtglaubeStimme</c>, <c>PflasterStimme</c>, <c>RatsantragStimme</c>).</param>
public sealed record Sitzungsstimme(Amt Sitz, string Name, Konto? Haus, bool? Ja, string? Kandidat, bool Los, string Begruendung, bool Frei);

/// <summary>Stimmen für einen Kandidaten bei einer Wahl.</summary>
/// <param name="Haus">null = der Ratsherr im Amt.</param>
public sealed record Wahlzaehlung(string Kandidat, Konto? Haus, int Stimmen);

/// <summary>
/// Wie ein Punkt ausgeht bzw. (als Vorschau) voraussichtlich ausginge.
/// </summary>
/// <param name="Stichentscheid">Antrag: Gleichstand, der Bürgermeister entscheidet. Wahl: Gleichstand an der Spitze, das Los entscheidet
/// (in der Vorschau auch, wenn ein Ratsherr das Los ziehen muss).</param>
/// <param name="Angenommen">Nur bei Anträgen. Angenommen heißt beschlossen; ob daraus etwas wird (Kasse), steht im Protokoll.</param>
/// <param name="Gewaehlt">Nur bei Wahlen: der Gewinner (Name wie <see cref="Wahlkandidat.Name"/>); in der Vorschau null bei Gleichstand.</param>
/// <param name="Zaehlung">Nur bei Wahlen: Stimmen je Kandidat, in der Reihenfolge der Kandidaten.</param>
/// <param name="Protokoll">Was die Sitzung zu diesem Punkt ins Protokoll schrieb (Beschluss, Nachrücker); leer in der Vorschau.</param>
public sealed record Sitzungsergebnis(
    int Ja, int Nein, int Enthaltung, bool? Buergermeister, bool Stichentscheid, bool Angenommen,
    string? Gewaehlt, IReadOnlyList<Wahlzaehlung> Zaehlung, IReadOnlyList<string> Protokoll);

/// <summary>
/// Ein Punkt der Ratssitzung: wer was beantragt (oder welche Wahl), mit einer Zeile Begründung, die Stimmen aller Sitze mit Begründung,
/// die Vorschau des Ausgangs und – sobald abgehalten – das Ergebnis.
/// </summary>
/// <param name="Nummer">Laufende Nummer in der Sitzung, ab 1.</param>
/// <param name="Titel">z. B. „Antrag auf ein Statut“, „Wahl zum Bürgermeister“.</param>
/// <param name="Text">Worum es geht, z. B. „Statut B – Bestienfron“ oder „Steuersatz 4 % (bisher 5 %)“.</param>
/// <param name="Antragsteller">Null bei Wahlen.</param>
/// <param name="Begruendung">Die Begründung des Antragstellers in einer Zeile; bei Wahlen, wer antritt.</param>
/// <param name="Amt">Nur bei Wahlen: das Amt.</param>
/// <param name="Kandidaten">Nur bei Wahlen: die Kandidaten wie in <see cref="Stadtrat.Kandidaten"/>.</param>
/// <param name="Stimmen">Alle Sitze, die abstimmen, in der Reihenfolge der Ämter (höchstes zuerst). Bei Wahlen ohne die Kandidaten.</param>
/// <param name="Vorschau">Der voraussichtliche Ausgang nach den Stimmen oben.</param>
/// <param name="Ergebnis">Null, solange der Punkt nicht abgehalten ist.</param>
public sealed record Sitzungspunkt(
    int Nummer, Sitzungsthema Thema, string Titel, string Text, Konto? Antragsteller, string Begruendung,
    Amt? Amt, IReadOnlyList<Wahlkandidat> Kandidaten, IReadOnlyList<Sitzungsstimme> Stimmen,
    Sitzungsergebnis Vorschau, Sitzungsergebnis? Ergebnis)
{
    /// <summary>Die Antragsart für <c>RatsantragStimme</c> (nur Steuersatz, Bauvorhaben, Fest).</summary>
    public Antragsart? Antragsart => Thema switch
    {
        Sitzungsthema.Steuersatz => Politik.Antragsart.Steuersatz,
        Sitzungsthema.Bauvorhaben => Politik.Antragsart.Bauvorhaben,
        Sitzungsthema.Fest => Politik.Antragsart.Fest,
        _ => null,
    };
}

/// <summary>
/// Die Ratssitzung am Jahresende, Punkt für Punkt (PR #202).
///
/// Ablauf: Am Jahresende beginnt die Sitzung (<see cref="Stadtrat.SitzungBeginnen"/>). Ohne Oberfläche läuft sie in einem Zug durch
/// (<see cref="AllesAbhalten"/>) – genau wie früher. Verlangt die Oberfläche einen Halt (<see cref="Stadtwirtschaft.RatssitzungAnhalten"/>),
/// steht die Partie vor dem ersten Punkt still: <see cref="AktuellerPunkt"/> zeigt ihn mit den Stimmen nach dem aktuellen Stand, der Spieler
/// setzt seine Stimme mit den üblichen Befehlen, und <see cref="Simulation.RatssitzungWeiter"/> hält den Punkt ab. Nach dem letzten Punkt
/// endet die Sitzung, und die Jahresabrechnung läuft weiter.
///
/// Die Stimmen kommen aus denselben Regeln wie die Prognosen (<c>…StimmeVonSitz</c>); vor dem Abhalten eines Punktes wird nichts mehr
/// verändert – die Vorschau des aktuellen Punktes ist also das Ergebnis, nur das Los (Gleichstand bei einer Wahl) steht noch aus.
/// Spätere Punkte rechnet die Vorschau nach dem heutigen Stand; frühere Beschlüsse können sie noch ändern (z. B. das Ansehen aus einem Fest
/// bei der Wahl, ein neuer Amtsinhaber bei der nächsten Wahl).
/// Eine laufende Sitzung lässt sich nicht speichern (<see cref="Speichern.Speicherung.SpeichernVerboten"/>).
/// </summary>
public sealed class Ratssitzung
{
    /// <summary>Ein Schritt der Sitzung: ein Punkt mit Abstimmung oder eine Aktion ohne Abstimmung (z. B. die Stadt pflastert).</summary>
    private sealed record Schritt(Sitzungsthema? Thema, Amt? Amt, Action? Aktion);

    private readonly Stadtwirtschaft _w;
    private readonly Zufallsquelle? _zufall;
    private readonly List<Schritt> _schritte = new();
    private readonly List<Sitzungspunkt> _abgehalten = new();
    private int _naechster;

    /// <summary>Das Jahr, das die Sitzung abschließt (wie im Protokoll „Ratssitzung 1402“).</summary>
    public int Jahr { get; }

    /// <summary>Nur eine Vorschau (<see cref="Stadtrat.Sitzungsvorschau"/>) – sie wird nie abgehalten.</summary>
    public bool IstVorschau => _zufall is null;

    /// <summary>Alle Punkte sind abgehalten, die Sitzung ist vorbei.</summary>
    public bool IstBeendet => !IstVorschau && _naechster >= _schritte.Count;

    /// <summary>Die Zahl der Punkte (Anträge und Wahlen).</summary>
    public int Anzahl => _schritte.Count(s => s.Thema is not null);

    /// <summary>Die abgehaltenen Punkte mit Ergebnis.</summary>
    public IReadOnlyList<Sitzungspunkt> Abgehalten => _abgehalten;

    /// <summary>Der Punkt, über den als Nächstes abgestimmt wird – mit den Stimmen nach dem aktuellen Stand (null: keiner mehr).</summary>
    public Sitzungspunkt? AktuellerPunkt =>
        IstVorschau ? Punkte.FirstOrDefault()
        : _naechster < _schritte.Count && _schritte[_naechster].Thema is not null ? Punkt(_schritte[_naechster], _abgehalten.Count + 1) : null;

    /// <summary>Alle Punkte: die abgehaltenen mit Ergebnis, die übrigen mit den Stimmen nach dem aktuellen Stand.</summary>
    public IReadOnlyList<Sitzungspunkt> Punkte
    {
        get
        {
            var liste = new List<Sitzungspunkt>(_abgehalten);
            foreach (var s in _schritte.Skip(IstVorschau ? 0 : _naechster).Where(s => s.Thema is not null))
                liste.Add(Punkt(s, liste.Count + 1));
            return liste;
        }
    }

    internal Ratssitzung(Stadtwirtschaft w, Zufallsquelle? zufall, int jahr)
    {
        _w = w;
        _zufall = zufall;
        Jahr = jahr;
        var rat = w.Rat;
        var protokoll = (Action<string>)rat.Protokollieren;

        // Dieselbe Reihenfolge wie bisher: Statut, Stadtglaube, Pflaster, Steuersatz, Bauvorhaben, Fest, Wahlen, Abschluss.
        if (rat.Antrag is not null) _schritte.Add(new(Sitzungsthema.Statut, null, null));
        _schritte.Add(new(null, null, rat.StatutAntragSchliessen));
        if (w.Glaube.Antrag is not null) _schritte.Add(new(Sitzungsthema.Stadtglaube, null, null));
        if (w.Wegebau.IstAktiv)
        {
            if (w.Wegebau.Antrag is not null) _schritte.Add(new(Sitzungsthema.Pflaster, null, null));
            _schritte.Add(new(null, null, () => w.Wegebau.NachDemAntrag(protokoll)));
        }
        if (w.Ratsantraege.IstAktiv)
            foreach (var a in w.Ratsantraege.Antraege)
                _schritte.Add(new(Thema(a.Art), null, null));
        _schritte.Add(new(null, null, w.Ratsantraege.SitzungAbschliessen));
        foreach (var amt in rat.Aemter.OrderByDescending(a => a.Stufe).ThenBy(a => a.Name, StringComparer.Ordinal))
            if (rat.KandidatenListe(amt).Count > 0)
                _schritte.Add(new(Sitzungsthema.Wahl, amt, null));
        _schritte.Add(new(null, null, rat.SitzungBeenden));
    }

    private static Sitzungsthema Thema(Antragsart art) => art switch
    {
        Antragsart.Steuersatz => Sitzungsthema.Steuersatz,
        Antragsart.Bauvorhaben => Sitzungsthema.Bauvorhaben,
        _ => Sitzungsthema.Fest,
    };

    // ═════════════════════════════════════════════════════════════════════════
    //  Ablauf
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Bis vor den ersten Punkt: die Schritte ohne Abstimmung davor.</summary>
    internal void Beginnen() => AktionenAusfuehren();

    /// <summary>Hält den aktuellen Punkt ab und führt die Schritte bis zum nächsten Punkt aus. Null, wenn kein Punkt mehr ansteht.</summary>
    internal Sitzungspunkt? PunktAbhalten()
    {
        if (IstVorschau || _naechster >= _schritte.Count) return null;
        var schritt = _schritte[_naechster];
        var vorher = Punkt(schritt, _abgehalten.Count + 1);
        var rat = _w.Rat;
        var zeilen = rat.LetzteSitzung.Count;
        var protokoll = (Action<string>)rat.Protokollieren;

        Sitzungspunkt punkt;
        if (schritt.Thema == Sitzungsthema.Wahl)
        {
            var ausgang = rat.Waehlen(schritt.Amt!, _zufall!, Jahr);
            punkt = MitWahlergebnis(vorher, ausgang, Neu(zeilen));
            // Die Bewerber merken sich, wer für sie stimmte und wer für einen anderen (Meinung, PR #231).
            foreach (var bewerber in vorher.Kandidaten.Select(k => k.Haus).OfType<Konto>())
                foreach (var (waehler, wahl, _) in ausgang?.Stimmen ?? Array.Empty<(Amt, Konto?, bool)>())
                    _w.Meinungen.StimmeGemerkt(waehler, bewerber, wahl == bewerber);
        }
        else
        {
            var (ja, nein, enthaltung, bm, angenommen) = schritt.Thema switch
            {
                Sitzungsthema.Statut => rat.StatutAbstimmen(Jahr),
                Sitzungsthema.Stadtglaube => _w.Glaube.Abstimmen(Jahr, protokoll),
                Sitzungsthema.Pflaster => _w.Wegebau.AntragAbstimmen(Jahr, protokoll),
                _ => _w.Ratsantraege.Abstimmen(vorher.Antragsart!.Value, Jahr, protokoll),
            };
            punkt = vorher with
            {
                Ergebnis = new Sitzungsergebnis(ja, nein, enthaltung, bm, ja == nein, angenommen, null, Array.Empty<Wahlzaehlung>(), Neu(zeilen)),
            };
            // Der Antragsteller merkt sich, wer dafür und wer dagegen stimmte (Enthaltung zählt nicht).
            if (vorher.Antragsteller is { } antragsteller)
                foreach (var st in vorher.Stimmen.Where(st => st.Ja is not null))
                    _w.Meinungen.StimmeGemerkt(st.Sitz, antragsteller, st.Ja == true);
        }
        _abgehalten.Add(punkt);
        _naechster++;
        AktionenAusfuehren();
        return punkt;

        IReadOnlyList<string> Neu(int ab) => rat.LetzteSitzung.Skip(ab).ToList();
    }

    /// <summary>Alle übrigen Punkte abhalten – ohne Halt.</summary>
    internal void AllesAbhalten()
    {
        while (!IstBeendet)
            PunktAbhalten();
    }

    private void AktionenAusfuehren()
    {
        while (_naechster < _schritte.Count && _schritte[_naechster].Aktion is { } aktion)
        {
            _naechster++;
            aktion();
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Stimmrecht
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Darf dieses Haus zu diesem Punkt (noch) seine Stimme setzen? (null = ja, sonst der Grund)</summary>
    public string? StimmeVerboten(Sitzungspunkt punkt, Konto konto)
    {
        if (punkt.Ergebnis is not null) return "Darüber hat der Rat schon abgestimmt.";
        return StimmeVerboten(punkt.Thema, punkt.Amt, konto);
    }

    private string? StimmeVerboten(Sitzungsthema thema, Amt? amt, Konto konto) => thema switch
    {
        Sitzungsthema.Statut => _w.Rat.StatutStimmeVerboten(konto),
        Sitzungsthema.Stadtglaube => _w.Glaube.StimmeVerboten(konto),
        Sitzungsthema.Pflaster => _w.Wegebau.StimmeVerboten(konto),
        Sitzungsthema.Wahl => _w.Rat.WahlStimmeVerboten(konto, amt!),
        _ => _w.Ratsantraege.StimmeVerboten(konto, thema switch
        {
            Sitzungsthema.Steuersatz => Antragsart.Steuersatz,
            Sitzungsthema.Bauvorhaben => Antragsart.Bauvorhaben,
            _ => Antragsart.Fest,
        }),
    };

    private bool IstKi(Konto konto) => _w.Rivalen.Any(r => r.Konto == konto);

    // ═════════════════════════════════════════════════════════════════════════
    //  Punkte bilden
    // ═════════════════════════════════════════════════════════════════════════

    private Sitzungspunkt Punkt(Schritt s, int nummer) =>
        s.Thema == Sitzungsthema.Wahl ? Wahlpunkt(s.Amt!, nummer) : Antragspunkt(s.Thema!.Value, nummer);

    private Sitzungspunkt Antragspunkt(Sitzungsthema thema, int nummer)
    {
        var rat = _w.Rat;
        string titel, text, begruendung;
        Konto antragsteller;
        Func<Amt, bool?> stimme;
        Func<Amt, bool?, string> grund;
        var waehrung = _w.Daten.Allgemein.Waehrung;
        switch (thema)
        {
            case Sitzungsthema.Statut:
            {
                var (von, statut) = rat.Antrag!.Value;
                (antragsteller, titel, text) = (von, "Antrag auf ein Statut", $"Statut {statut.Kuerzel()} – {statut.Name()}");
                begruendung = KiVon(von) is { } ki ? ki.AntragsgrundStatut(statut) : $"Die Stadt soll unter „{statut.Name()}“ wirtschaften.";
                stimme = sitz => rat.StatutStimmeVonSitz(sitz, von, statut);
                grund = (sitz, ja) => sitz.Ratsherr is { } r ? GrundStatut(r, statut, von, ja) : KiVon(sitz.Konto!)!.GrundStatut(statut, ja);
                break;
            }
            case Sitzungsthema.Stadtglaube:
            {
                var (von, glaube) = _w.Glaube.Antrag!.Value;
                (antragsteller, titel, text) = (von, "Antrag auf einen Stadtglauben", $"Stadtglaube: {KonfessionTexte.StadtglaubeText(glaube)}");
                begruendung = glaube is { } g ? $"Die Stadt soll dem {g.Glaube()} folgen." : "Jeder soll in dieser Stadt glauben dürfen, was er will.";
                stimme = sitz => _w.Glaube.StimmeVonSitz(sitz, von, glaube);
                grund = (sitz, ja) => sitz.Ratsherr is { } r ? GrundGlaube(r, glaube, von, ja) : GrundGlaubeKi(sitz.Konto!, glaube, ja);
                break;
            }
            case Sitzungsthema.Pflaster:
            {
                var wb = _w.Wegebau;
                var (von, weg, ab, bis) = wb.Antrag!.Value;
                var verkehr = wb.MittlererVerkehr(weg, ab, bis);
                (antragsteller, titel) = (von, "Antrag: Weg pflastern");
                text = $"{wb.WegName(weg)} pflastern ({Zahlenformat.Ganz(Math.Abs(bis - ab))} m, aus der Wegekasse)";
                begruendung = $"Dort fahren im Jahr etwa {Zahlenformat.Ganz(verkehr)} Karren – Pflaster spart Zeit und Achsen.";
                stimme = sitz => wb.StimmeVonSitz(sitz, von, weg, ab, bis, verkehr);
                grund = (sitz, ja) => sitz.Ratsherr is { } r ? GrundPflaster(r, von, verkehr, ja) : KiVon(sitz.Konto!)!.GrundWegantrag(weg, ab, bis, ja);
                break;
            }
            default:
            {
                var ra = _w.Ratsantraege;
                var a = ra.Antrag(thema == Sitzungsthema.Steuersatz ? Antragsart.Steuersatz : thema == Sitzungsthema.Bauvorhaben ? Antragsart.Bauvorhaben : Antragsart.Fest)!;
                antragsteller = a.Antragsteller;
                titel = a.Art switch
                {
                    Antragsart.Steuersatz => "Antrag auf einen neuen Steuersatz",
                    Antragsart.Bauvorhaben => "Antrag auf ein Bauvorhaben der Stadt",
                    _ => "Antrag auf ein Fest der Stadt",
                };
                text = ra.Beschreibung(a);
                begruendung = KiVon(a.Antragsteller) is { } ki ? ki.AntragsgrundRatsantrag(a) : a.Art switch
                {
                    Antragsart.Steuersatz when a.Prozent < _w.Kasse.SteuersatzProzent => "Die Umsatzsteuer drückt Handel und Gewerbe – sie soll sinken.",
                    Antragsart.Steuersatz => "Die Stadtkasse braucht mehr Geld.",
                    Antragsart.Bauvorhaben => ra.BauvorhabenNamens(a.Bauvorhaben) is { Beschreibung.Length: > 0 } b ? b.Beschreibung : "Das nützt der ganzen Stadt.",
                    _ => "Ein Fest bringt Käufer auf den Markt und Gäste in die Wirtshäuser.",
                };
                stimme = sitz => ra.StimmeVonSitz(sitz, a);
                grund = (sitz, ja) => sitz.Ratsherr is { } r ? GrundRatsantrag(r, a, ja, waehrung) : KiVon(sitz.Konto!)!.GrundRatsantrag(a, ja);
                break;
            }
        }

        var stimmen = new List<Sitzungsstimme>();
        foreach (var sitz in rat.Aemter.Where(a => a.IstBesetzt))
        {
            var ja = stimme(sitz);
            var haus = sitz.Konto;
            string text2 = sitz.Ratsherr is not null ? grund(sitz, ja)
                : haus == antragsteller ? "Eigener Antrag."
                : IstKi(haus!) ? grund(sitz, ja)
                : ja is null ? "Keine Stimme gesetzt – Enthaltung."
                : "Stimme des Hauses.";
            var frei = haus is not null && !IstKi(haus) && StimmeVerboten(thema, null, haus) is null;
            stimmen.Add(new Sitzungsstimme(sitz, sitz.Inhaber, haus, ja, null, false, text2, frei));
        }
        var (nja, nnein, nenth, bm) = rat.Zaehlen(stimme);
        var vorschau = new Sitzungsergebnis(nja, nnein, nenth, bm, nja == nnein, Stadtrat.IstAngenommen(nja, nnein, bm),
            null, Array.Empty<Wahlzaehlung>(), Array.Empty<string>());
        return new Sitzungspunkt(nummer, thema, titel, text, antragsteller, begruendung, null, Array.Empty<Wahlkandidat>(), stimmen, vorschau, null);
    }

    private Sitzungspunkt Wahlpunkt(Amt amt, int nummer)
    {
        var rat = _w.Rat;
        var kandidaten = rat.KandidatenListe(amt);
        var namen = rat.Kandidaten(amt);
        string NameVon(Konto? k) => k?.Inhaber ?? amt.Ratsherr?.Name ?? "—";

        var stimmen = new List<Sitzungsstimme>();
        var zaehlung = kandidaten.Select(k => 0).ToArray();
        var los = false;
        foreach (var sitz in rat.WahlWaehler(amt, kandidaten))
        {
            var gleichauf = rat.WahlStimmeVonSitz(sitz, amt, kandidaten);
            var wahl = gleichauf[0];
            zaehlung[kandidaten.IndexOf(wahl)]++;
            los |= gleichauf.Count > 1;
            string grund;
            if (sitz.Ratsherr is { } r)
                grund = gleichauf.Count > 1
                    ? $"Gleichstand zwischen {string.Join(" und ", gleichauf.Select(NameVon))} ({Zahlenformat.Ganz(rat.Punkte(r, amt, wahl))} Punkte) – das Los entscheidet."
                    : GrundWahl(r, amt, kandidaten, wahl, NameVon);
            else if (KiVon(sitz.Konto!) is { } ki)
                grund = ki.GrundWahl(amt, kandidaten, wahl);
            else
                grund = rat.HatWahlStimme(sitz.Konto!, amt, kandidaten)
                    ? "Stimme des Hauses."
                    : $"Keine Stimme gesetzt – die Vorgabe: {(wahl == (amt.Ratsherr is null ? amt.Konto : null) ? "der bisherige Inhaber" : "der erste Bewerber")}.";
            var frei = sitz.Konto is { } haus && !IstKi(haus) && rat.WahlStimmeVerboten(haus, amt) is null;
            stimmen.Add(new Sitzungsstimme(sitz, sitz.Inhaber, sitz.Konto, null, NameVon(wahl), gleichauf.Count > 1, grund, frei));
        }

        var hoechste = zaehlung.DefaultIfEmpty(0).Max();
        var spitze = Enumerable.Range(0, kandidaten.Count).Where(i => zaehlung[i] == hoechste).ToList();
        var vorschau = new Sitzungsergebnis(0, 0, 0, null, spitze.Count > 1 || los, false,
            spitze.Count == 1 ? NameVon(kandidaten[spitze[0]]) : null,
            kandidaten.Select((k, i) => new Wahlzaehlung(NameVon(k), k, zaehlung[i])).ToList(), Array.Empty<string>());

        var bewerber = kandidaten.Where(k => k is not null && k != amt.Konto).Select(NameVon).ToList();
        var bisher = amt.IstBesetzt ? $" – bisher im Amt: {amt.Inhaber}" : " – das Amt ist frei";
        var begruendung = $"{(bewerber.Count == 1 ? "Es bewirbt sich" : "Es bewerben sich")} {string.Join(", ", bewerber)}{bisher}.";
        return new Sitzungspunkt(nummer, Sitzungsthema.Wahl, $"Wahl zum {amt.Name}", $"{amt.Name} (Stufe {amt.Stufe})", null, begruendung,
            amt, namen, stimmen, vorschau, null);
    }

    /// <summary>Das Ergebnis einer Wahl in den Punkt eintragen – die Stimmen der Ratsherren mit dem gezogenen Los.</summary>
    private static Sitzungspunkt MitWahlergebnis(Sitzungspunkt vorher, Stadtrat.Wahlausgang? ausgang, IReadOnlyList<string> protokoll)
    {
        if (ausgang is null)
            return vorher with { Ergebnis = new Sitzungsergebnis(0, 0, 0, null, false, false, null, Array.Empty<Wahlzaehlung>(), protokoll) };
        var amt = vorher.Amt!;
        string NameVon(Konto? k) => k?.Inhaber ?? vorher.Kandidaten.FirstOrDefault(x => x.Haus is null)?.Name ?? "—";
        var stimmen = vorher.Stimmen.Select(s =>
            ausgang.Stimmen.FirstOrDefault(x => x.Waehler == s.Sitz) is { Waehler: not null } x && x.Los
                ? s with { Kandidat = NameVon(x.Wahl), Begruendung = $"{s.Begruendung} Das Los fiel auf {NameVon(x.Wahl)}." }
                : s).ToList();
        var ergebnis = new Sitzungsergebnis(0, 0, 0, null, ausgang.LosEntschied, false, NameVon(ausgang.Gewinner),
            ausgang.Zaehlung.Select(z => new Wahlzaehlung(NameVon(z.Kandidat), z.Kandidat, z.Stimmen)).ToList(), protokoll);
        return vorher with { Stimmen = stimmen, Ergebnis = ergebnis };
    }

    private Ki.KiRivale? KiVon(Konto konto) => _w.Rivalen.FirstOrDefault(r => r.Konto == konto);

    // ═════════════════════════════════════════════════════════════════════════
    //  Begründungen der Ratsherren (aus derselben Rechnung wie ihre Stimme)
    // ═════════════════════════════════════════════════════════════════════════

    private string GrundStatut(Ratsherr r, Fronstatut statut, Konto antragsteller, bool? ja)
    {
        var abstand = Math.Abs((int)statut - (int)r.Haltung);
        var gunst = _w.Rat.Meinung(r, antragsteller);
        var schwelle = _w.Rat.Daten.Gunst.SchwelleNachbarstatut;
        if (abstand == 0) return $"Das ist seine Haltung ({r.Haltung.Name()}).";
        if (abstand > 1) return $"Zu weit von seiner Haltung ({r.Haltung.Name()}).";
        return ja == true
            ? $"Nah an seiner Haltung ({r.Haltung.Name()}), und er hält viel von {antragsteller.Inhaber} (Meinung {Zahlenformat.Ganz(gunst)})."
            : $"Er hält es mit {r.Haltung.Name()} – seine Meinung über {antragsteller.Inhaber} ist zu schlecht ({Zahlenformat.Ganz(gunst)} von {Zahlenformat.Ganz(schwelle)}).";
    }

    private string GrundGlaube(Ratsherr r, Konfession? glaube, Konto antragsteller, bool? ja)
    {
        var gl = _w.Glaube;
        var eigener = gl.GlaubeVon(r);
        var gunst = _w.Rat.Meinung(r, antragsteller);
        var d = gl.Daten.Stadtglaube;
        if (glaube == eigener) return $"Sein eigener Glaube ({eigener.Name()}).";
        if (glaube is null)
            return gl.Stadtglaube != eigener ? $"Der Stadtglaube ist nicht seiner ({eigener.Name()}) – lieber Freiheit."
                : ja == true ? $"Er will den Stadtglauben behalten, aber er hält viel von {antragsteller.Inhaber} (Meinung {Zahlenformat.Ganz(gunst)})."
                : $"Er will den Stadtglauben behalten – seine Meinung ist zu schlecht ({Zahlenformat.Ganz(gunst)} von {Zahlenformat.Ganz(d.SchwelleFrei)}).";
        return ja == true
            ? $"Nicht sein Glaube, aber er hält viel von {antragsteller.Inhaber} (Meinung {Zahlenformat.Ganz(gunst)})."
            : $"Nicht sein Glaube ({eigener.Name()}) – seine Meinung ist zu schlecht ({Zahlenformat.Ganz(gunst)} von {Zahlenformat.Ganz(d.SchwelleFremd)}).";
    }

    private string GrundGlaubeKi(Konto ki, Konfession? glaube, bool? ja)
    {
        var eigener = _w.Glaube.GlaubeVon(ki);
        if (glaube == eigener) return "Der eigene Glaube.";
        if (glaube is null)
            return ja == true ? "Der Stadtglaube ist nicht der eigene – lieber Freiheit." : "Der Stadtglaube ist der eigene.";
        return eigener is { } e ? $"Nicht der eigene Glaube ({e.Name()})." : "Nicht der eigene Glaube.";
    }

    private string GrundPflaster(Ratsherr r, Konto antragsteller, double verkehr, bool? ja)
    {
        var d = _w.Wegebau.Daten!.Rat;
        var gunst = _w.Rat.Meinung(r, antragsteller);
        if (gunst >= d.AntragGunst) return $"Er hält viel von {antragsteller.Inhaber} (Meinung {Zahlenformat.Ganz(gunst)}).";
        if (ja == true) return $"Viel Verkehr ({Zahlenformat.Ganz(verkehr)} Fahrten im Jahr).";
        return $"Zu wenig Verkehr ({Zahlenformat.Ganz(verkehr)} von {Zahlenformat.Ganz(d.AntragVerkehr)} Fahrten) und seine Meinung zu schlecht ({Zahlenformat.Ganz(gunst)}).";
    }

    private string GrundRatsantrag(Ratsherr r, Ratsantrag a, bool? ja, string waehrung)
    {
        var kasse = _w.Kasse;
        var k = kasse.Daten;
        var gunst = _w.Rat.Meinung(r, a.Antragsteller);
        if (gunst >= k.AntragGunst) return $"Er hält viel von {a.Antragsteller.Inhaber} (Meinung {Zahlenformat.Ganz(gunst)}).";
        var reserve = k.ReserveVon(r.Daten.Volk);
        var stand = Zahlenformat.Ganz(kasse.Stand);
        var res = Zahlenformat.Ganz(reserve);
        switch (a.Art)
        {
            case Antragsart.Steuersatz when a.Prozent > kasse.SteuersatzProzent:
                if (ja != true) return $"Die Kasse reicht ({stand} {waehrung}, Reserve {res}), und der Haushalt ist im Plus.";
                return kasse.Stand < reserve ? $"Die Kasse ist zu leer ({stand} {waehrung}, Reserve {res})." : "Der Haushalt ist im Minus.";
            case Antragsart.Steuersatz:
                if (ja == true) return $"Die Kasse ist voll genug ({stand} {waehrung}), und der Haushalt bleibt im Plus.";
                return kasse.Stand <= 2 * reserve
                    ? $"Für weniger Steuer ist die Kasse zu leer ({stand} {waehrung}, er will {Zahlenformat.Ganz(2 * reserve)})."
                    : $"Mit {a.Prozent} % käme der Haushalt ins Minus.";
            default:
                var nachher = Zahlenformat.Ganz(kasse.Stand - _w.Ratsantraege.Kosten(a));
                return ja == true
                    ? $"Danach bleibt die Reserve ({nachher} {waehrung}, Reserve {res})."
                    : $"Die Kasse ist zu leer – danach blieben {nachher} {waehrung}, Reserve {res}.";
        }
    }

    private string GrundWahl(Ratsherr r, Amt amt, IReadOnlyList<Konto?> kandidaten, Konto? wahl, Func<Konto?, string> name)
    {
        var rat = _w.Rat;
        var punkte = rat.Punkte(r, amt, wahl);
        var zweiter = kandidaten.Where(k => k != wahl).Select(k => (K: k, P: rat.Punkte(r, amt, k))).OrderByDescending(x => x.P).FirstOrDefault();
        var abstand = kandidaten.Count > 1 ? $" gegen {Zahlenformat.Ganz(zweiter.P)} für {name(zweiter.K)}" : "";
        if (wahl is null) return $"Hält zum Ratsherrn im Amt ({Zahlenformat.Ganz(punkte)} Punkte{abstand}).";
        var ansehen = rat.Ansehen(wahl);
        var meinung = rat.Meinung(r, wahl);
        var rest = punkte - ansehen - meinung;
        return $"{name(wahl)}: {Zahlenformat.Ganz(punkte)} Punkte{abstand} – Ansehen {Zahlenformat.Ganz(ansehen)}, Meinung {Zahlenformat.Ganz(meinung)}" +
               (Math.Abs(rest) >= 0.5 ? $", Rede{(amt.Konto == wahl ? " und Amt" : "")} {Zahlenformat.Ganz(rest)}." : ".");
    }
}
