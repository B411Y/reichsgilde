using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Gauner;

/// <summary>Ein Auftrag im Hinterzimmer – ausgeführt in der nächsten Nacht.</summary>
public sealed record Gaunerauftrag(Konto Auftraggeber, Konto Ziel, Gaunerstueck Art, string? Betrieb);

/// <summary>Wegelagerer auf der Lauer: Die nächste beladene Umlandfahrt des Ziels wird überfallen.</summary>
public sealed record Hinterhalt(Konto Auftraggeber, Konto Ziel, long BisMinute);

/// <summary>Ein Erpresserbrief: Schweigegeld gegen die Beweise des Erpressers.</summary>
public sealed record Forderung(Konto Erpresser, Konto Opfer, double Betrag, int Jahr);

/// <summary>
/// Was nachts geschah. <see cref="Fuer"/> = null: Die ganze Stadt weiß es (Einbruch, Brand, Überfall).
/// Sonst nur dieses Haus (z. B. der Auftraggeber erfährt, was der Hehler zahlt).
/// </summary>
public sealed record Nachtereignis(Konto? Fuer, string Text);

/// <summary>
/// Die Unterwelt der Stadt: Aufträge im Hinterzimmer, Hinterhalte im Umland, Erpresserbriefe.
///
/// Über das Jahr:
///   • Ein Haus gibt ein Gaunerstück gegen ein anderes in Auftrag (Befehl Beauftragen) und zahlt sofort.
///   • Mit genug Beweisen verlangt man Schweigegeld (Befehl Erpressen), das Opfer zahlt oder nicht.
///
/// Jede Nacht um 21:00 (Beginn der Dunklen Nacht):
///   • Diebstahl und Sabotage werden ausgeführt, Wegelagerer legen sich auf die Lauer.
///   • Jedes Gaunerstück hinterlässt eine Spur fürs Gericht. Mit etwas Glück (oder dank eines Wächters)
///     gibt es einen Zeugen – dann hat das Opfer sofort einen Beweis.
///
/// Alles Würfeln geschieht mit einem eigenen Zufallsgenerator – Wirtschaft, Familie und Gericht bleiben unberührt.
/// </summary>
public sealed class Unterwelt
{
    /// <summary>Spielminute im Tag, zu der die Gauner losziehen (21:00 = Beginn der Dunklen Nacht).</summary>
    public const int Nachtstunde = 900;

    private readonly Stadtwirtschaft _w;
    private readonly GaunerDaten? _d;
    private Zufallsquelle _zufall;
    private readonly List<Gaunerauftrag> _auftraege = new();
    private readonly List<Hinterhalt> _hinterhalte = new();
    private readonly List<Forderung> _forderungen = new();
    private List<Nachtereignis> _diesesJahr = new();
    private List<Nachtereignis> _letztesJahr = new();
    private readonly Dictionary<string, int> _letzterAngriff = new();

    public bool IstAktiv => _d is not null;
    public GaunerDaten Daten => _d ?? throw new InvalidOperationException("Es gibt keine Unterwelt (gauner fehlt in den Daten).");

    public IReadOnlyList<Gaunerauftrag> Auftraege => _auftraege;
    public IReadOnlyList<Hinterhalt> Hinterhalte => _hinterhalte;
    public IReadOnlyList<Forderung> Forderungen => _forderungen;
    public IReadOnlyList<Nachtereignis> DiesesJahr => _diesesJahr;
    public IReadOnlyList<Nachtereignis> LetztesJahr => _letztesJahr;

    /// <summary>Die Banden haben ihre Aufträge ausgeführt (für Oberflächen, die sich dann neu aufbauen).</summary>
    public event Action? NachtVorbei;

    /// <summary>Was dieses Haus über die Nächte weiß: alles Öffentliche und seine eigenen Berichte.</summary>
    public IEnumerable<string> NachrichtenFuer(Konto konto, bool letztesJahr) =>
        (letztesJahr ? _letztesJahr : _diesesJahr).Where(e => e.Fuer is null || e.Fuer == konto).Select(e => e.Text);

    internal Unterwelt(Stadtwirtschaft w, GaunerDaten? daten, int zufallsstartwert)
    {
        _w = w;
        _d = daten;
        _zufall = new Zufallsquelle(unchecked(zufallsstartwert * 29 + 11));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Rechnungen – dieselben, die die Hinterzimmer-Tafel zeigt und nach denen die KI entscheidet
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die wertvollste Ware im Lager, höchstens so viel, wie die Bande tragen kann.</summary>
    public List<(string Ware, int Stueck, double Wert)> MoeglicheBeute(Betrieb b)
    {
        var rest = Daten.Diebstahl.HoechstensStueck;
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

    /// <summary>Erfolgschance eines Gaunerstücks gegen diesen Betrieb (Wächter halbieren sie).</summary>
    public double Erfolgschance(Gaunerstueck art, Betrieb? b)
    {
        var d = Daten;
        var schutz = b?.Waechter == true ? d.WaechterSchutz : 1;
        return art switch
        {
            Gaunerstueck.Diebstahl => d.Diebstahl.Chance * schutz,
            Gaunerstueck.Sabotage => d.Sabotage.Chance * schutz,
            _ => d.Ueberfall.Staerke, // vor der Eskorte des jeweiligen Karrens
        };
    }

    /// <summary>Was ein Diebstahl im Mittel einbringt: Hehlerpreis × Chance − Kosten.</summary>
    public double ErwarteterGewinn(Betrieb b) =>
        MoeglicheBeute(b).Sum(x => x.Wert) * Daten.HehlerAnteil * Erfolgschance(Gaunerstueck.Diebstahl, b) - Daten.Diebstahl.Kosten;

    /// <summary>Was eine Sabotage dem Opfer im Mittel kostet: entgangene Arbeit + Reparatur.</summary>
    public double ErwarteterSchaden(Betrieb b) =>
        Erfolgschance(Gaunerstueck.Sabotage, b) * SabotageSchaden(b);

    private double SabotageSchaden(Betrieb b) =>
        Daten.Sabotage.Reparatur + Daten.Sabotage.StillstandMinuten * Math.Max(0, Kalkulation.WertJeArbeitsminute(_w, b));

    /// <summary>Betriebe im Umland, deren beladene Karren man überfallen könnte.</summary>
    public bool HatUmlandfahrten(Konto ziel) => _w.Betriebe.Any(b => b.Konto == ziel && b.Ort == Arbeitsort.Umland && b.Karren.Count > 0);

    public double Zeugenchance(Betrieb? b, bool erfolgreich)
    {
        var d = Daten;
        var chance = b?.Waechter == true ? d.ZeugenChanceWaechter : d.ZeugenChance;
        return Math.Min(1, erfolgreich ? chance : chance * d.ZeugenFaktorMisserfolg);
    }

    /// <summary>In welchem Jahr dieser Betrieb zuletzt angegriffen wurde (öffentlich – die ganze Stadt redet davon).</summary>
    public int? LetzterAngriff(string betrieb) => _letzterAngriff.TryGetValue(betrieb, out var jahr) ? jahr : null;

    public Forderung? ForderungAn(Konto opfer, Konto erpresser) => _forderungen.FirstOrDefault(f => f.Opfer == opfer && f.Erpresser == erpresser);

    // ═════════════════════════════════════════════════════════════════════════
    //  Befehle (über Stadtwirtschaft.Ausfuehren)
    // ═════════════════════════════════════════════════════════════════════════

    internal Befehlsergebnis Beauftragen(Konto auftraggeber, Konto ziel, Gaunerstueck art, string? betriebName)
    {
        var d = Daten;
        var waehrung = _w.Daten.Allgemein.Waehrung;
        if (ziel == auftraggeber) return Befehlsergebnis.Abgelehnt("Gegen das eigene Haus heuert man keine Bande an.");
        if (!ziel.IstAktiv) return Befehlsergebnis.Abgelehnt($"{ziel.Inhaber} ist nicht mehr im Spiel.");
        if (_auftraege.Any(a => a.Auftraggeber == auftraggeber && a.Ziel == ziel && a.Art == art))
            return Befehlsergebnis.Abgelehnt($"Für heute Nacht ist gegen {ziel.Inhaber} schon ein {art.Name()} bestellt.");

        if (art == Gaunerstueck.Ueberfall)
        {
            if (!HatUmlandfahrten(ziel))
                return Befehlsergebnis.Abgelehnt($"{ziel.Inhaber} hat keine Karren auf Umlandwegen – in der Stadt lauert keine Bande.");
            if (_hinterhalte.Any(h => h.Auftraggeber == auftraggeber && h.Ziel == ziel))
                return Befehlsergebnis.Abgelehnt($"Eure Wegelagerer liegen schon auf der Lauer gegen {ziel.Inhaber}.");
            betriebName = null;
        }
        else
        {
            var betrieb = _w.Betriebe.FirstOrDefault(b => b.Name == betriebName);
            if (betrieb is null || betrieb.Konto != ziel)
                return Befehlsergebnis.Abgelehnt($"{ziel.Inhaber} hat keinen Betrieb „{betriebName}“.");
        }

        var kosten = d.Kosten(art);
        if (auftraggeber.Vermoegen < kosten)
            return Befehlsergebnis.Abgelehnt($"Die Bande will {Zahlenformat.Ganz(kosten)} {waehrung} im Voraus.");
        auftraggeber.Gaunerkosten(kosten);
        _auftraege.Add(new Gaunerauftrag(auftraggeber, ziel, art, betriebName));
        return Befehlsergebnis.Ok($"Die Bande nimmt das Geld ({Zahlenformat.Ganz(kosten)} {waehrung}). " +
                                  (art == Gaunerstueck.Ueberfall ? $"Heute Nacht legen sich Wegelagerer gegen {ziel.Inhaber} auf die Lauer."
                                      : $"{art.Name()} in {betriebName} – heute Nacht um 21 Uhr."));
    }

    internal Befehlsergebnis Erpressen(Konto erpresser, Konto opfer, double betrag)
    {
        var d = Daten;
        if (!_w.Justiz.IstAktiv) return Befehlsergebnis.Abgelehnt("Ohne Gericht schreckt keine Drohung mit Beweisen.");
        if (opfer == erpresser || !opfer.IstAktiv) return Befehlsergebnis.Abgelehnt("Dieses Haus lässt sich nicht erpressen.");
        if (betrag <= 0 || double.IsNaN(betrag)) return Befehlsergebnis.Abgelehnt("Wie viel Schweigegeld soll es denn sein?");
        var beweise = _w.Justiz.Beweise(erpresser, opfer).Count();
        if (beweise < d.ErpressungMindestBeweise)
            return Befehlsergebnis.Abgelehnt($"Mit {beweise} {(beweise == 1 ? "Beweis" : "Beweisen")} lacht {opfer.Inhaber} euch aus – es braucht mindestens {d.ErpressungMindestBeweise}.");
        if (ForderungAn(opfer, erpresser) is not null)
            return Befehlsergebnis.Abgelehnt($"{opfer.Inhaber} hat euren Brief schon – wartet die Antwort ab.");
        if (_w.Justiz.VerfahrenGegen(erpresser, opfer) is not null)
            return Befehlsergebnis.Abgelehnt($"Die Anklage gegen {opfer.Inhaber} läuft schon – zum Erpressen ist es zu spät.");

        _forderungen.Add(new Forderung(erpresser, opfer, betrag, _w.Jahr));
        // Der Brief selbst ist ein Beweis – das Opfer weiß, wer schreibt.
        if (_w.Justiz.TatMelden(erpresser, Vergehen.Erpressung, opfer.Inhaber, betrag) is { } spur)
            _w.Justiz.BeweisGeben(opfer, spur);
        _w.Melden($"{opfer.Inhaber} erhält einen Erpresserbrief von {erpresser.Inhaber}: " +
                  $"{Zahlenformat.Ganz(betrag)} {_w.Daten.Allgemein.Waehrung} Schweigegeld, sonst kommt es vor Gericht.");
        return Befehlsergebnis.Ok($"Der Brief ist zugestellt. {opfer.Inhaber} hat bis zum Jahresende Zeit. Aber Vorsicht: Der Brief ist selbst ein Beweis gegen euch.");
    }

    internal Befehlsergebnis ErpressungBeantworten(Konto opfer, Konto erpresser, bool zahlen)
    {
        if (ForderungAn(opfer, erpresser) is not { } f)
            return Befehlsergebnis.Abgelehnt($"{erpresser.Inhaber} hat euch nicht erpresst.");
        var waehrung = _w.Daten.Allgemein.Waehrung;
        if (zahlen)
        {
            if (opfer.Vermoegen < f.Betrag)
                return Befehlsergebnis.Abgelehnt($"So viel Geld hat {opfer.Inhaber} nicht.");
            opfer.Gaunerkosten(f.Betrag);
            erpresser.Gaunerkosten(-f.Betrag);
            _w.Justiz.BeweiseVerbrennen(erpresser, opfer);
            _forderungen.Remove(f);
            Ereignis(null, $"{opfer.Inhaber} zahlt {erpresser.Inhaber} Schweigegeld – die Beweise gehen ins Feuer.");
            return Befehlsergebnis.Ok($"Ihr zahlt {Zahlenformat.Ganz(f.Betrag)} {waehrung}. {erpresser.Inhaber} verbrennt seine Beweise gegen euch.");
        }
        _forderungen.Remove(f);
        Ereignis(null, $"{opfer.Inhaber} weist den Erpresserbrief von {erpresser.Inhaber} zurück.");
        return Befehlsergebnis.Ok($"Ihr zahlt nicht. Nun kann {erpresser.Inhaber} klagen – und ihr habt seinen Brief als Beweis gegen ihn.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Nacht und Jahresende
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Um 21:00: alle Aufträge ausführen, abgelaufene Hinterhalte auflösen.</summary>
    internal void Nacht(Spielzeit zeit)
    {
        if (!IstAktiv) return;
        foreach (var a in _auftraege)
        {
            if (!a.Auftraggeber.IstAktiv || !a.Ziel.IstAktiv) continue;
            switch (a.Art)
            {
                case Gaunerstueck.Diebstahl: Stehlen(a); break;
                case Gaunerstueck.Sabotage: Sabotieren(a); break;
                default:
                    _hinterhalte.Add(new Hinterhalt(a.Auftraggeber, a.Ziel, zeit.VergangeneMinuten + Daten.Ueberfall.GueltigMinuten));
                    Ereignis(a.Auftraggeber, $"Eure Wegelagerer legen sich gegen {a.Ziel.Inhaber} auf die Lauer – bis morgen Nacht.");
                    break;
            }
        }
        _auftraege.Clear();

        foreach (var h in _hinterhalte.Where(h => h.BisMinute < zeit.VergangeneMinuten).ToList())
        {
            _hinterhalte.Remove(h);
            Ereignis(h.Auftraggeber, $"Eure Wegelagerer haben vergeblich auf einen Karren von {h.Ziel.Inhaber} gewartet und ziehen ab.");
        }
        NachtVorbei?.Invoke();
    }

    private void Stehlen(Gaunerauftrag a)
    {
        var d = Daten;
        var b = _w.Betriebe.First(x => x.Name == a.Betrieb);
        var erfolg = _zufall.NextDouble() < Erfolgschance(Gaunerstueck.Diebstahl, b);
        double wert = 0;
        string oeffentlich;
        if (erfolg)
        {
            var beute = MoeglicheBeute(b);
            foreach (var (ware, stueck, _) in beute)
                b.Auslagern(ware, stueck);
            wert = beute.Sum(x => x.Wert);
            if (wert > 0)
            {
                a.Ziel.Raub(wert);
                a.Auftraggeber.Gaunerkosten(-wert * d.HehlerAnteil);
                oeffentlich = $"Einbruch in {b.Name}: {string.Join(" und ", beute.Select(x => $"{x.Stueck} {x.Ware}"))} gestohlen (Wert {Zahlenformat.Ganz(wert)}).";
                Ereignis(a.Auftraggeber, $"Eure Diebe haben in {b.Name} Ware für {Zahlenformat.Ganz(wert)} erbeutet – der Hehler zahlt {Zahlenformat.Ganz(wert * d.HehlerAnteil)}.");
            }
            else
                oeffentlich = $"Einbruch in {b.Name} – doch das Lager war leer.";
        }
        else
            oeffentlich = $"Einbruchsversuch in {b.Name} – die Diebe wurden verscheucht{(b.Waechter ? " (der Wächter war wachsam)" : "")}.";
        Spur(a, Vergehen.Diebstahl, b.Name, wert, b, erfolg, oeffentlich);
    }

    private void Sabotieren(Gaunerauftrag a)
    {
        var d = Daten;
        var b = _w.Betriebe.First(x => x.Name == a.Betrieb);
        var erfolg = _zufall.NextDouble() < Erfolgschance(Gaunerstueck.Sabotage, b);
        double schaden = 0;
        string oeffentlich;
        if (erfolg)
        {
            schaden = SabotageSchaden(b);
            b.Sabotieren(d.Sabotage.StillstandMinuten);
            a.Ziel.Schaden(d.Sabotage.Reparatur);
            oeffentlich = $"Anschlag auf {b.Name}: Feuer in der Werkstatt! {d.Sabotage.StillstandMinuten / 60} Arbeitsstunden Stillstand, " +
                          $"Reparatur {Zahlenformat.Ganz(d.Sabotage.Reparatur)}.";
        }
        else
            oeffentlich = $"Anschlag auf {b.Name} vereitelt{(b.Waechter ? " – der Wächter hat das Feuer bemerkt" : "")}.";
        Spur(a, Vergehen.Sabotage, b.Name, schaden, b, erfolg, oeffentlich);
    }

    /// <summary>
    /// Wird beim Losfahren eines beladenen Umlandkarrens gefragt. Liegt ein Hinterhalt gegen sein Haus,
    /// greifen die angeheuerten Wegelagerer an – statt der zufälligen Banden.
    /// </summary>
    /// <returns>true, wenn ein Hinterhalt zuschlug (dann entfällt der gewöhnliche Überfall-Wurf).</returns>
    internal bool HinterhaltAusloesen(Karren karren, EskorteDaten eskorte, Spielzeit zeit)
    {
        if (!IstAktiv) return false;
        var h = _hinterhalte.FirstOrDefault(x => x.Ziel == karren.Heimat.Konto && x.BisMinute >= zeit.VergangeneMinuten && x.Auftraggeber.IstAktiv);
        if (h is null) return false;
        _hinterhalte.Remove(h);

        var d = Daten;
        var erfolg = _zufall.NextDouble() < d.Ueberfall.Staerke * (1 - eskorte.Schutz);
        double wert = 0;
        string oeffentlich;
        if (erfolg)
        {
            var verloren = karren.LadungVerlieren();
            wert = verloren.Sum(v => _w.Markt[v.Key].Preis * v.Value);
            karren.Heimat.Konto.Raub(wert);
            h.Auftraggeber.Gaunerkosten(-wert * d.HehlerAnteil);
            oeffentlich = $"Hinterhalt auf {karren.Name} von {karren.Heimat.Name}: Wegelagerer erbeuten {string.Join(" und ", verloren.Select(v => $"{v.Value} {v.Key}"))} " +
                          $"(Wert {Zahlenformat.Ganz(wert)}).";
            Ereignis(h.Auftraggeber, $"Eure Wegelagerer haben {karren.Heimat.Name} um Ware für {Zahlenformat.Ganz(wert)} erleichtert – der Hehler zahlt {Zahlenformat.Ganz(wert * d.HehlerAnteil)}.");
        }
        else
            oeffentlich = $"Hinterhalt auf {karren.Name} von {karren.Heimat.Name} – die Eskorte schlägt die Wegelagerer in die Flucht.";
        Spur(new Gaunerauftrag(h.Auftraggeber, h.Ziel, Gaunerstueck.Ueberfall, karren.Heimat.Name), Vergehen.Raub, karren.Heimat.Name, wert, null, erfolg, oeffentlich);
        return true;
    }

    /// <summary>Spur fürs Gericht, öffentliche Nachricht – und vielleicht ein Zeuge, der den Auftraggeber erkannt hat.</summary>
    private void Spur(Gaunerauftrag a, Vergehen vergehen, string betroffener, double betrag, Betrieb? b, bool erfolg, string oeffentlich)
    {
        _letzterAngriff[betroffener] = _w.Jahr;
        var spur = _w.Justiz.TatMelden(a.Auftraggeber, vergehen, betroffener, betrag);
        var zeuge = _zufall.NextDouble() < Zeugenchance(b, erfolg);
        if (zeuge && spur is not null)
        {
            _w.Justiz.BeweisGeben(a.Ziel, spur);
            oeffentlich += $" Ein Zeuge hat Leute von {a.Auftraggeber.Inhaber} erkannt!";
        }
        Ereignis(null, oeffentlich);
    }

    private void Ereignis(Konto? fuer, string text)
    {
        _diesesJahr.Add(new Nachtereignis(fuer, text));
        _w.Melden(text);
    }

    /// <summary>
    /// Jahresende (nach dem Jahresabschluss): Erpresserbriefe gelten bis zum Ende des Folgejahres –
    /// sonst verfiele ein spät im Jahr geschickter Brief, bevor jemand antworten kann. Danach heißt Schweigen Nein.
    /// </summary>
    internal void Jahresende()
    {
        if (!IstAktiv) return;
        foreach (var f in _forderungen.Where(f => f.Jahr < _w.Jahr - 1).ToList())
        {
            _forderungen.Remove(f);
            Ereignis(null, $"{f.Opfer.Inhaber} hat auf den Erpresserbrief von {f.Erpresser.Inhaber} nicht geantwortet – das gilt als Nein.");
        }
        _letztesJahr = _diesesJahr;
        _diesesJahr = new List<Nachtereignis>();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Speichern
    // ═════════════════════════════════════════════════════════════════════════

    internal UnterweltStand Sichern(Func<Konto, int> nr) => new()
    {
        ZufallWuerfe = _zufall.Wuerfe,
        Auftraege = _auftraege.Select(a => new AuftragStand { Auftraggeber = nr(a.Auftraggeber), Ziel = nr(a.Ziel), Art = a.Art, Betrieb = a.Betrieb }).ToList(),
        Hinterhalte = _hinterhalte.Select(h => new HinterhaltStand { Auftraggeber = nr(h.Auftraggeber), Ziel = nr(h.Ziel), BisMinute = h.BisMinute }).ToList(),
        Forderungen = _forderungen.Select(f => new ForderungStand { Erpresser = nr(f.Erpresser), Opfer = nr(f.Opfer), Betrag = f.Betrag, Jahr = f.Jahr }).ToList(),
        DiesesJahr = _diesesJahr.Select(e => new NachtereignisStand { Fuer = e.Fuer is null ? null : nr(e.Fuer), Text = e.Text }).ToList(),
        LetztesJahr = _letztesJahr.Select(e => new NachtereignisStand { Fuer = e.Fuer is null ? null : nr(e.Fuer), Text = e.Text }).ToList(),
        LetzterAngriff = _letzterAngriff.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Value),
    };

    internal void Wiederherstellen(UnterweltStand s, IReadOnlyList<Konto> k)
    {
        _zufall = new Zufallsquelle(_zufall.Startwert, s.ZufallWuerfe);
        _auftraege.Clear();
        _auftraege.AddRange(s.Auftraege.Select(a => new Gaunerauftrag(k[a.Auftraggeber], k[a.Ziel], a.Art, a.Betrieb)));
        _hinterhalte.Clear();
        _hinterhalte.AddRange(s.Hinterhalte.Select(h => new Hinterhalt(k[h.Auftraggeber], k[h.Ziel], h.BisMinute)));
        _forderungen.Clear();
        _forderungen.AddRange(s.Forderungen.Select(f => new Forderung(k[f.Erpresser], k[f.Opfer], f.Betrag, f.Jahr)));
        _diesesJahr = s.DiesesJahr.Select(e => new Nachtereignis(e.Fuer is int n ? k[n] : null, e.Text)).ToList();
        _letztesJahr = s.LetztesJahr.Select(e => new Nachtereignis(e.Fuer is int n ? k[n] : null, e.Text)).ToList();
        _letzterAngriff.Clear();
        foreach (var (betrieb, jahr) in s.LetzterAngriff) _letzterAngriff[betrieb] = jahr;
    }
}
