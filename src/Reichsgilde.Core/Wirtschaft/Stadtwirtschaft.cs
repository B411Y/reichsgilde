using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Die Wirtschaft der Reichsstadt: Markt, Betriebe, Konten und das geltende Fronstatut.
///
/// Ablauf:
///   • jede Spielminute während der Arbeitszeit (Standard 08:00–20:00) arbeiten alle Betriebe,
///   • jede Spielminute fahren die Karren (auch nachts – ins Umland aber nur, wenn erlaubt),
///   • jede volle Spielstunde rückt das Markt-Fenster weiter,
///   • am Jahresende werden Löhne, Unterhalt und Pacht fällig, und Unfreie können ausbrechen.
///
/// Alle Zufallsereignisse kommen aus EINEM Zufallsgenerator mit festem Startwert.
/// Gleicher Startwert + gleiche Eingaben = exakt gleicher Spielverlauf.
/// Das ist die Voraussetzung für Multiplayer und für reproduzierbare Tests.
/// </summary>
public sealed class Stadtwirtschaft
{
    private readonly List<Betrieb> _betriebe = new();
    private readonly List<Konto> _konten = new();
    private Zufallsquelle _zufall;
    private Zufallsquelle _reihenfolge;
    private readonly List<Karren> _alleKarren = new();
    private readonly List<KiRivale> _rivalen = new();

    /// <summary>Der eine Zufallsgenerator der Wirtschaft (nur intern, damit niemand die Reihenfolge stört).</summary>
    internal Zufallsquelle Zufall => _zufall;

    internal void Melden(string meldung) => Meldung?.Invoke(meldung);

    public Wirtschaftsdaten Daten { get; }
    public Markt Markt { get; }
    public Fronstatut Statut { get; private set; }
    public IReadOnlyList<Betrieb> Betriebe => _betriebe;
    public IReadOnlyList<Konto> Konten => _konten;

    /// <summary>Konten, die von der KI geführt werden.</summary>
    public IReadOnlyList<KiRivale> Rivalen => _rivalen;

    /// <summary>Meldungen für den Spieler (Ausbrüche, Entlassungen …).</summary>
    public event Action<string>? Meldung;

    /// <summary>Ein Konto ist bankrott gegangen (nach der Jahresabrechnung).</summary>
    public event Action<Konto>? Bankrott;

    /// <summary>Ein Konto hat eine Werkstatt gekauft oder eine Grube gepachtet.</summary>
    public event Action<Betrieb>? BetriebErworben;

    /// <summary>Liegenschaften, die noch niemandem gehören.</summary>
    public IEnumerable<LiegenschaftDaten> FreieLiegenschaften =>
        Daten.Liegenschaften.Where(l => _betriebe.All(b => b.Name != l.Name));

    /// <summary>Ein Konto ist in einen höheren Stand aufgestiegen.</summary>
    public event Action<Konto, StandDaten>? Aufgestiegen;

    // ── Stände ──────────────────────────────────────────────────────────────

    /// <summary>Der aktuelle Stand eines Kontos.</summary>
    public StandDaten StandVon(Konto konto) => Daten.Staende[Math.Clamp(konto.Stand, 0, Daten.Staende.Count - 1)];

    /// <summary>Der nächsthöhere Stand oder null, wenn das Konto ganz oben ist.</summary>
    public StandDaten? NaechsterStand(Konto konto) =>
        konto.Stand + 1 < Daten.Staende.Count ? Daten.Staende[konto.Stand + 1] : null;

    /// <summary>
    /// Rangliste: höherer Stand vor niedrigerem, innerhalb eines Standes nach Vermögen.
    /// Bankrotte Konten stehen am Ende.
    /// </summary>
    public IReadOnlyList<Konto> Rangliste => _konten
        .OrderBy(k => k.IstBankrott)
        .ThenByDescending(k => k.Stand)
        .ThenByDescending(k => k.Vermoegen)
        .ThenBy(k => k.Inhaber, StringComparer.Ordinal)
        .ToList();

    /// <summary>Arbeitsbeginn in Minuten nach Mitternacht (08:00).</summary>
    public const int Arbeitsbeginn = 8 * 60;

    public Stadtwirtschaft(Wirtschaftsdaten daten, Fronstatut statut, int zufallsstartwert)
    {
        Daten = daten;
        Statut = statut;
        Markt = new Markt(daten);
        _zufall = new Zufallsquelle(zufallsstartwert);
        // Eigener Generator nur für die Reihenfolge – so bleiben die Ereignis-Würfel unabhängig
        // davon, wie viele Karren es gibt.
        _reihenfolge = new Zufallsquelle(unchecked(zufallsstartwert * 31 + 7));
    }

    /// <summary>Legt ein Konto an – mit dem Startvermögen aus den Daten oder einem eigenen (z. B. Schwierigkeitsgrad).</summary>
    public Konto KontoEroeffnen(string inhaber, int startjahr = Spielzeit.StartJahr, double? startvermoegen = null)
    {
        var konto = new Konto(inhaber, startvermoegen ?? Daten.Allgemein.Startvermoegen, startjahr);
        _konten.Add(konto);
        return konto;
    }

    /// <summary>
    /// Eröffnet einen Betrieb mit Startausstattung: Eine Gießerei bekommt einen Handkarren,
    /// eine Mine einen Ochsenkarren (mit einem Handkarren käme sie mit dem Abtransport nicht nach).
    /// </summary>
    /// <param name="startkarren">Anderer Karrentyp als Startausstattung oder "" für keinen.</param>
    public Betrieb BetriebEroeffnen(string name, Betriebsart art, Konto konto, string? startkarren = null)
    {
        if (!_konten.Contains(konto))
            throw new ArgumentException("Das Konto gehört nicht zu dieser Stadtwirtschaft.", nameof(konto));
        // Befehle nennen Betriebe beim Namen – deshalb muss er eindeutig sein.
        if (_betriebe.Any(b => b.Name == name))
            throw new ArgumentException($"Einen Betrieb „{name}“ gibt es schon.", nameof(name));
        var betrieb = new Betrieb(name, art, konto, Daten);
        _betriebe.Add(betrieb);

        startkarren ??= art == Betriebsart.Mine ? "Ochsenkarren" : "Handkarren";
        if (startkarren != "")
            betrieb.KarrenHinzufuegen(startkarren);
        return betrieb;
    }

    /// <summary>
    /// Übergibt ein Konto samt seinen Betrieben an die KI. Sie trifft sofort erste Entscheidungen
    /// und danach jede volle Stunde sowie zum Jahresende.
    /// </summary>
    public KiRivale KiAnmelden(Konto konto, KiProfil? profil = null)
    {
        if (!_konten.Contains(konto))
            throw new ArgumentException("Das Konto gehört nicht zu dieser Stadtwirtschaft.", nameof(konto));
        if (_rivalen.Any(r => r.Konto == konto))
            throw new InvalidOperationException($"{konto.Inhaber} wird bereits von der KI geführt.");
        var rivale = new KiRivale(konto, profil ?? KiProfil.Standard, this);
        _rivalen.Add(rivale);
        rivale.Stunde();
        return rivale;
    }

    /// <summary>Ist zu dieser Uhrzeit Arbeitszeit?</summary>
    public bool IstArbeitszeit(Spielzeit zeit)
    {
        var uhr = zeit.UhrzeitInMinuten;
        return uhr >= Arbeitsbeginn && uhr < Arbeitsbeginn + Daten.Allgemein.Arbeitsminuten;
    }

    /// <summary>Eine Spielminute. Wird von der Simulation VOR dem Weiterstellen der Uhr aufgerufen.</summary>
    internal void Tick(Spielzeit zeit)
    {
        if (zeit.MinuteImTag % 60 == 0 && zeit.VergangeneMinuten > 0)
            Markt.NeueStunde();
        else if (zeit.VergangeneMinuten > 0)
            Markt.NeueMinute();

        // Die KI überlegt jede volle Stunde – mit den Marktpreisen dieser Stunde.
        if (zeit.MinuteImTag % 60 == 0 && zeit.VergangeneMinuten > 0)
            foreach (var rivale in _rivalen)
                rivale.Stunde(); // bankrotte Rivalen tun nichts mehr

        if (IstArbeitszeit(zeit))
            foreach (var betrieb in _betriebe)
                betrieb.Arbeitsminute(Melden);

        // Fairness: Wer in derselben Minute am Markt ankommt, ist in zufälliger Reihenfolge dran.
        // Sonst hätte immer der zuerst eröffnete Betrieb den besseren Preis (im Multiplayer: wer zuerst beitritt).
        _alleKarren.Clear();
        foreach (var betrieb in _betriebe.Where(b => !b.Konto.IstBankrott)) // Bankrotte Häuser fahren nicht mehr
            _alleKarren.AddRange(betrieb.Karren);
        for (var i = _alleKarren.Count - 1; i > 0; i--)
        {
            var j = _reihenfolge.Next(i + 1);
            (_alleKarren[i], _alleKarren[j]) = (_alleKarren[j], _alleKarren[i]);
        }
        foreach (var karren in _alleKarren)
            karren.Tick(zeit, this);
    }

    /// <summary>Jahresende für alle Betriebe und Konten.</summary>
    internal void Jahresabrechnung(int neuesJahr)
    {
        foreach (var betrieb in _betriebe.Where(b => !b.Konto.IstBankrott))
            betrieb.Jahresabrechnung(_zufall, m => Meldung?.Invoke(m));
        foreach (var konto in _konten)
            konto.JahrAbschliessen(neuesJahr);
        SchuldenPruefen();
        foreach (var rivale in _rivalen.Where(r => !r.Konto.IstBankrott))
            rivale.Jahresende();
    }

    /// <summary>
    /// Schulden haben Folgen: Beim ersten Jahresabschluss im Minus warnt der Kämmerer,
    /// nach <see cref="SchuldenDaten.JahreBisBankrott"/> Jahren in Folge ist das Konto bankrott.
    /// Ein bankrotter Betrieb entlässt alle Arbeitskräfte und nimmt keine Befehle mehr an.
    /// </summary>
    private void SchuldenPruefen()
    {
        var grenze = Daten.Schulden.JahreBisBankrott;
        foreach (var konto in _konten.Where(k => !k.IstBankrott && k.Schuldenjahre > 0))
        {
            if (konto.Schuldenjahre >= grenze)
            {
                konto.IstBankrott = true;
                foreach (var betrieb in _betriebe.Where(b => b.Konto == konto))
                    foreach (var kraft in betrieb.Belegung.Keys.ToList())
                        betrieb.Einstellen(kraft, 0, Statut);
                Melden($"{konto.Inhaber} ist bankrott! Die Gläubiger lassen die Betriebe schließen.");
                Bankrott?.Invoke(konto);
            }
            else
            {
                var rest = grenze - konto.Schuldenjahre;
                Melden($"Der Kämmerer mahnt {konto.Inhaber}: {Zahlenformat.Ganz(-konto.Vermoegen)} {Daten.Allgemein.Waehrung} Schulden. " +
                       $"Noch {rest} {(rest == 1 ? "Jahresabschluss" : "Jahresabschlüsse")} im Minus, dann droht der Bankrott.");
            }
        }
    }

    /// <summary>
    /// Der Stadtrat ändert das Fron- und Sklavenstatut. Arbeitskräfte, die nun verboten sind,
    /// werden sofort entlassen (später: Verstoß → Anklage vor Gericht).
    /// </summary>
    public void StatutAendern(Fronstatut neu)
    {
        if (neu == Statut) return;
        Statut = neu;
        Meldung?.Invoke($"Der Rat hat ein neues Statut beschlossen: {neu.Kuerzel()} – {neu.Name()}.");
        foreach (var betrieb in _betriebe)
            foreach (var (kraft, anzahl) in betrieb.UnerlaubteEntlassen(neu))
                Meldung?.Invoke($"{betrieb.Name}: {anzahl}× {kraft} musste unter dem neuen Statut entlassen werden.");
    }

    /// <summary>Hilfsfunktion: Einstellen unter dem aktuell geltenden Statut.</summary>
    public void Einstellen(Betrieb betrieb, string arbeitskraft, int anzahl) =>
        betrieb.Einstellen(arbeitskraft, anzahl, Statut);

    // ═════════════════════════════════════════════════════════════════════════
    //  Befehle
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Prüft einen Befehl und führt ihn aus. Wirft nie – Fehler kommen als abgelehntes Ergebnis
    /// mit verständlicher Begründung zurück, damit die Oberfläche sie einfach anzeigen kann.
    /// </summary>
    /// <param name="absender">Wessen Befehl ist es? Nur eigene Betriebe dürfen verändert werden.</param>
    public Befehlsergebnis Ausfuehren(Konto absender, Befehl befehl)
    {
        if (absender.IstBankrott)
            return Befehlsergebnis.Abgelehnt($"{absender.Inhaber} ist bankrott und kann nichts mehr unternehmen.");
        if (befehl is StandErwerben)
            return StandErwerbenAusfuehren(absender);
        if (befehl is LiegenschaftErwerben liegenschaft)
            return LiegenschaftErwerbenAusfuehren(absender, liegenschaft.Name);
        if (befehl is not BetriebsBefehl betriebsBefehl)
            return Befehlsergebnis.Abgelehnt($"Unbekannter Befehl: {befehl.GetType().Name}");

        var betrieb = _betriebe.FirstOrDefault(b => b.Name == betriebsBefehl.Betrieb);
        if (betrieb is null)
            return Befehlsergebnis.Abgelehnt($"Einen Betrieb „{betriebsBefehl.Betrieb}“ gibt es nicht.");
        if (betrieb.Konto != absender)
            return Befehlsergebnis.Abgelehnt($"{betrieb.Name} gehört nicht {absender.Inhaber}, sondern {betrieb.Konto.Inhaber}.");

        try
        {
            return befehl switch
            {
                ProduktWaehlen p => ProduktWaehlenAusfuehren(betrieb, p),
                FoerderungAufteilen f => FoerderungAusfuehren(betrieb, f),
                PersonalSetzen p => PersonalAusfuehren(betrieb, p),
                KarrenKaufen k => KarrenKaufenAusfuehren(betrieb, k),
                EskorteWaehlen e => EskorteAusfuehren(betrieb, e),
                NachtfahrtErlauben n => NachtfahrtAusfuehren(betrieb, n),
                _ => Befehlsergebnis.Abgelehnt($"Unbekannter Befehl: {befehl.GetType().Name}"),
            };
        }
        catch (Exception fehler) when (fehler is ArgumentException or InvalidOperationException)
        {
            // Die Regeln selbst stehen in Betrieb (z. B. Statut und Arbeitsplätze) – hier wird nur übersetzt.
            return Befehlsergebnis.Abgelehnt(fehler.Message);
        }
    }

    private Befehlsergebnis StandErwerbenAusfuehren(Konto konto)
    {
        var neu = NaechsterStand(konto);
        var waehrung = Daten.Allgemein.Waehrung;
        if (neu is null)
            return Befehlsergebnis.Abgelehnt($"{konto.Inhaber} steht bereits im höchsten Stand ({StandVon(konto).Name}).");
        if (konto.Vermoegen < neu.Schwelle)
            return Befehlsergebnis.Abgelehnt(
                $"Für den Stand {neu.Name} braucht es {Zahlenformat.Ganz(neu.Schwelle)} {waehrung} Vermögen – " +
                $"es fehlen noch {Zahlenformat.Ganz(neu.Schwelle - konto.Vermoegen)} {waehrung}.");

        konto.Standesgebuehr(neu.Gebuehr);
        konto.Stand++;
        Melden($"{konto.Inhaber} ist nun {neu.Name}! (Gebühr {Zahlenformat.Ganz(neu.Gebuehr)} {waehrung})");
        Aufgestiegen?.Invoke(konto, neu);
        return Befehlsergebnis.Ok($"{konto.Inhaber} steigt in den Stand {neu.Name} auf.");
    }

    private Befehlsergebnis LiegenschaftErwerbenAusfuehren(Konto konto, string name)
    {
        var waehrung = Daten.Allgemein.Waehrung;
        var l = Daten.Liegenschaften.FirstOrDefault(x => x.Name == name);
        if (l is null)
            return Befehlsergebnis.Abgelehnt($"Eine Liegenschaft „{name}“ gibt es nicht.");
        if (_betriebe.FirstOrDefault(b => b.Name == name) is { } vergeben)
            return Befehlsergebnis.Abgelehnt($"{name} gehört bereits {vergeben.Konto.Inhaber}.");
        if (konto.Stand < Daten.StandStufe(l.MindestStand))
            return Befehlsergebnis.Abgelehnt($"{name} ist erst ab dem Stand {l.MindestStand} zu haben – {konto.Inhaber} ist {StandVon(konto).Name}.");

        // Kaufpreis plus die Kosten des ersten Jahres müssen gedeckt sein – sonst droht sofort die Schuldenfalle.
        var fixkosten = Daten.Fixkosten(l.Art);
        if (konto.Vermoegen < l.Kaufpreis + fixkosten)
            return Befehlsergebnis.Abgelehnt(
                $"Für {name} braucht es {Zahlenformat.Ganz(l.Kaufpreis + fixkosten)} {waehrung} " +
                $"({(l.IstPacht ? "erste Jahrespacht" : $"Kaufpreis {Zahlenformat.Ganz(l.Kaufpreis)} und ein Jahr Unterhalt")}).");

        if (l.Kaufpreis > 0) konto.Investition(l.Kaufpreis);
        var betrieb = BetriebEroeffnen(l.Name, l.Art, konto);
        Melden(l.IstPacht
            ? $"{konto.Inhaber} pachtet {name} ({Zahlenformat.Ganz(fixkosten)} {waehrung} Pacht im Jahr)."
            : $"{konto.Inhaber} kauft {name} für {Zahlenformat.Ganz(l.Kaufpreis)} {waehrung}.");
        BetriebErworben?.Invoke(betrieb);
        return Befehlsergebnis.Ok($"{name} gehört nun {konto.Inhaber}. Jetzt Personal einstellen!");
    }

    private Befehlsergebnis ProduktWaehlenAusfuehren(Betrieb betrieb, ProduktWaehlen befehl)
    {
        if (betrieb.Art != Betriebsart.Giesserei)
            return Befehlsergebnis.Abgelehnt($"{betrieb.Name} ist keine Werkstatt und stellt nichts her.");
        if (betrieb.Produkt == befehl.Ware)
            return Befehlsergebnis.Ok($"{betrieb.Name} stellt bereits {befehl.Ware} her.");
        betrieb.Produkt = befehl.Ware;
        return Befehlsergebnis.Ok($"{betrieb.Name} stellt nun {befehl.Ware} her.");
    }

    private Befehlsergebnis FoerderungAusfuehren(Betrieb betrieb, FoerderungAufteilen befehl)
    {
        if (betrieb.Art != Betriebsart.Mine)
            return Befehlsergebnis.Abgelehnt($"{betrieb.Name} ist keine Mine.");
        if (befehl.Silberanteil is < 0 or > 1 || double.IsNaN(befehl.Silberanteil))
            return Befehlsergebnis.Abgelehnt("Der Silberanteil muss zwischen 0 % und 100 % liegen.");
        betrieb.Silberanteil = befehl.Silberanteil;
        return Befehlsergebnis.Ok($"{betrieb.Name} fördert nun zu {Zahlenformat.Ganz(befehl.Silberanteil * 100)} % Silber.");
    }

    private Befehlsergebnis PersonalAusfuehren(Betrieb betrieb, PersonalSetzen befehl)
    {
        var vorher = betrieb.Belegung.GetValueOrDefault(befehl.Arbeitskraft);
        betrieb.Einstellen(befehl.Arbeitskraft, befehl.Anzahl, Statut); // prüft Statut, Plätze und Namen
        var diff = befehl.Anzahl - vorher;
        return Befehlsergebnis.Ok(diff switch
        {
            > 0 => $"{betrieb.Name}: {diff}× {befehl.Arbeitskraft} eingestellt.",
            < 0 => $"{betrieb.Name}: {-diff}× {befehl.Arbeitskraft} entlassen.",
            _ => $"{betrieb.Name}: Belegung unverändert.",
        });
    }

    private Befehlsergebnis KarrenKaufenAusfuehren(Betrieb betrieb, KarrenKaufen befehl)
    {
        if (!Daten.Logistik.Karren.TryGetValue(befehl.Typ, out var daten))
            return Befehlsergebnis.Abgelehnt($"Den Karrentyp „{befehl.Typ}“ gibt es nicht.");
        if (betrieb.Konto.Vermoegen < daten.Preis)
            return Befehlsergebnis.Abgelehnt(
                $"Für einen {befehl.Typ} fehlen {Zahlenformat.Ganz(daten.Preis - betrieb.Konto.Vermoegen)} {Daten.Allgemein.Waehrung}.");
        var karren = betrieb.KarrenKaufen(befehl.Typ);
        return Befehlsergebnis.Ok($"{karren.Name} für {betrieb.Name} gekauft ({Zahlenformat.Ganz(daten.Preis)} {Daten.Allgemein.Waehrung}).");
    }

    private Befehlsergebnis EskorteAusfuehren(Betrieb betrieb, EskorteWaehlen befehl)
    {
        if (befehl.Karren < 0 || befehl.Karren >= betrieb.Karren.Count)
            return Befehlsergebnis.Abgelehnt($"{betrieb.Name} hat keinen Karren Nr. {befehl.Karren + 1}.");
        if (!Daten.Logistik.Eskorten.ContainsKey(befehl.Eskorte))
            return Befehlsergebnis.Abgelehnt($"Die Eskorte „{befehl.Eskorte}“ gibt es nicht.");
        var karren = betrieb.Karren[befehl.Karren];
        karren.Eskorte = befehl.Eskorte;
        return Befehlsergebnis.Ok($"{karren.Name} von {betrieb.Name} fährt nun mit Eskorte „{befehl.Eskorte}“.");
    }

    private Befehlsergebnis NachtfahrtAusfuehren(Betrieb betrieb, NachtfahrtErlauben befehl)
    {
        if (befehl.Karren < 0 || befehl.Karren >= betrieb.Karren.Count)
            return Befehlsergebnis.Abgelehnt($"{betrieb.Name} hat keinen Karren Nr. {befehl.Karren + 1}.");
        var karren = betrieb.Karren[befehl.Karren];
        karren.NachtsFahren = befehl.Erlaubt;
        return Befehlsergebnis.Ok($"{karren.Name} von {betrieb.Name} fährt {(befehl.Erlaubt ? "nun auch" : "nicht mehr")} nachts.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Speichern
    // ═════════════════════════════════════════════════════════════════════════

    internal WirtschaftStand Sichern() => new()
    {
        Statut = Statut,
        ZufallWuerfe = _zufall.Wuerfe,
        ReihenfolgeWuerfe = _reihenfolge.Wuerfe,
        Konten = _konten.Select(k => k.Sichern()).ToList(),
        Betriebe = _betriebe.Select(b => b.Sichern(_konten.IndexOf(b.Konto))).ToList(),
        Markt = Markt.Staende.ToDictionary(s => s.Ware, s => s.Sichern()),
        Rivalen = _rivalen.Select(r => r.Sichern(_konten.IndexOf(r.Konto))).ToList(),
    };

    /// <summary>
    /// Stellt den gespeicherten Zustand her. Wird direkt nach dem Erzeugen aufgerufen,
    /// solange die Stadtwirtschaft noch leer ist. Die Reihenfolge der Listen bleibt erhalten –
    /// sie bestimmt z. B., in welcher Reihenfolge die Karren gemischt werden.
    /// </summary>
    internal void Wiederherstellen(WirtschaftStand s)
    {
        if (_konten.Count > 0 || _betriebe.Count > 0)
            throw new InvalidOperationException("Wiederherstellen geht nur in eine leere Stadtwirtschaft.");

        Statut = s.Statut;
        _zufall = new Zufallsquelle(_zufall.Startwert, s.ZufallWuerfe);
        _reihenfolge = new Zufallsquelle(_reihenfolge.Startwert, s.ReihenfolgeWuerfe);

        _konten.AddRange(s.Konten.Select(Konto.Wiederherstellen));
        foreach (var bs in s.Betriebe)
        {
            var betrieb = new Betrieb(bs.Name, bs.Art, _konten[bs.Konto], Daten);
            betrieb.Wiederherstellen(bs);
            _betriebe.Add(betrieb);
        }
        foreach (var (ware, stand) in s.Markt)
            Markt[ware].Wiederherstellen(stand);
        foreach (var rs in s.Rivalen)
        {
            var rivale = new KiRivale(_konten[rs.Konto], rs.Profil, this);
            rivale.Wiederherstellen(rs);
            _rivalen.Add(rivale);
        }
    }

    public double HoechstesVermoegen => _konten.Count == 0 ? 0 : _konten.Max(k => k.Vermoegen);
}
