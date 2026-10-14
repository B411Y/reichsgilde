using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Gesundheit;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Politik;
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

    /// <summary>Der Stadtrat mit Ämtern, Wahlen und Abstimmungen.</summary>
    public Stadtrat Rat { get; }

    /// <summary>Familien der Häuser und die Heiratskandidaten der Stadt.</summary>
    public Familienbuch Familien { get; }

    /// <summary>Das Stadtgericht: Spuren, Spitzel, Beweise und Anklagen.</summary>
    public Stadtgericht Justiz { get; }

    /// <summary>Die Unterwelt: Hinterzimmer, Hinterhalte, Erpresserbriefe.</summary>
    public Unterwelt Gauner { get; }

    /// <summary>Krankheiten, Pest, Medicus und Hausapotheke.</summary>
    public Gesundheitswesen Gesundheit { get; }

    /// <summary>Gäste und Ausschank der Wirtshäuser, Barde, Würfeltisch, Stammtisch.</summary>
    public Gastwirtschaft Gastwirtschaft { get; }

    /// <summary>Das laufende Spieljahr (das Jahr der laufenden Jahresrechnung).</summary>
    public int Jahr => _konten.Count > 0 ? _konten[0].Laufend.Jahr : Spielzeit.StartJahr;

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
    /// Bankrotte und erloschene Häuser stehen am Ende.
    /// </summary>
    public IReadOnlyList<Konto> Rangliste => _konten
        .OrderBy(k => !k.IstAktiv)
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
        Rat = new Stadtrat(this, daten.Politik);
        Familien = new Familienbuch(this, daten.Dynastie, zufallsstartwert);
        Justiz = new Stadtgericht(this, daten.Justiz, zufallsstartwert);
        Gauner = new Unterwelt(this, daten.Gauner, zufallsstartwert);
        Gesundheit = new Gesundheitswesen(this, daten.Gesundheit, zufallsstartwert);
        Gastwirtschaft = new Gastwirtschaft(this, daten.Gastwirtschaft);
    }

    /// <summary>Legt ein Konto an – mit dem Startvermögen aus den Daten oder einem eigenen (z. B. Schwierigkeitsgrad).</summary>
    public Konto KontoEroeffnen(string inhaber, int startjahr = Spielzeit.StartJahr, double? startvermoegen = null)
    {
        var konto = new Konto(inhaber, startvermoegen ?? Daten.Allgemein.Startvermoegen, startjahr);
        if (HatAktionspunkte)
            konto.Aktionspunkte.NeueRunde(AktionspunkteJeRunde, hoechstens: Daten.Aktionspunkte!.Hoechstens);
        _konten.Add(konto);
        return konto;
    }

    // ── Aktionspunkte ────────────────────────────────────────────────────────

    /// <summary>Gibt es in dieser Stadt Aktionspunkte? (Abschnitt „aktionspunkte" in wirtschaft.json)</summary>
    public bool HatAktionspunkte => Daten.Aktionspunkte is not null;

    /// <summary>Neue AP je Runde (= je Spieljahr) – aus den Partie-Einstellungen, von der Simulation gesetzt.</summary>
    public int AktionspunkteJeRunde { get; set; } = 4;

    /// <summary>Was ein Befehl an Aktionspunkten kostet (0 = frei) – ohne Ansehen des Hauses.</summary>
    public int ApKosten(Befehl befehl) => Daten.Aktionspunkte?.KostenFuer(befehl) ?? 0;

    /// <summary>
    /// Was ein Befehl dieses Haus kostet. Fast immer wie <see cref="ApKosten(Befehl)"/>; nur das Üben hängt
    /// davon ab, wie gut das Oberhaupt das Talent schon beherrscht.
    /// </summary>
    public int ApKosten(Konto konto, Befehl befehl)
    {
        if (befehl is TalentTrainieren t && Daten.Aktionspunkte is { } d && Familien.Von(konto) is { } f)
            return Familien.TrainingVerboten(konto, t.Talent) is null ? d.TrainingKostenFuer(f.Oberhaupt.Talent(t.Talent)) : 0; // abgelehnt wird es ohnehin – mit dem richtigen Grund
        if (befehl is MeisterpruefungAblegen m)
            return HatAktionspunkte && MeisterpruefungVerboten(konto, m.Beruf) is null ? NaechstePruefung(konto, m.Beruf)!.Ap : 0;
        return ApKosten(befehl);
    }

    /// <summary>Kann das Haus sich diesen Befehl an Aktionspunkten leisten?</summary>
    public bool ApReichen(Konto konto, Befehl befehl) => konto.Aktionspunkte.Reicht(ApKosten(konto, befehl));

    /// <summary>Neue Runde: Jedes Haus bekommt seine AP, höchstens bis zum Vorrat aus den Daten – der Rest verfällt.</summary>
    private void AktionspunkteVerteilen()
    {
        if (Daten.Aktionspunkte is not { } d) return;
        foreach (var konto in _konten.Where(k => k.IstAktiv))
            konto.Aktionspunkte.NeueRunde(AktionspunkteJeRunde, ApBonus(konto), d.Hoechstens);
    }

    /// <summary>Zusätzliche AP je Runde – ein Meister des höchsten Grades hat mehr Zeit für Persönliches.</summary>
    public int ApBonus(Konto konto) =>
        Daten.Meistergrade is { } m && konto.HoechsterMeistergrad >= m.ApBonusAbGrad ? m.ApBonus : 0;

    // ── Meistergrade und Gebäudestufen ───────────────────────────────────────

    /// <summary>Die Prüfung zum nächsten Grad in diesem Beruf (null = höchster Grad erreicht, Beruf unbekannt oder keine Meistergrade).</summary>
    public Meisterpruefung? NaechstePruefung(Konto konto, string beruf) =>
        Daten.Meistergrade?.PruefungFuer(konto.Meistergrad(beruf) + 1);

    /// <summary>Warum das Haus die nächste Meisterprüfung nicht ablegen darf (null = es darf).</summary>
    public string? MeisterpruefungVerboten(Konto konto, string beruf)
    {
        if (Daten.Meistergrade is not { } m) return "In dieser Stadt gibt es keine Meisterprüfungen.";
        if (!Daten.Berufe.ContainsKey(beruf)) return $"Einen Beruf „{beruf}“ gibt es nicht.";
        if (!konto.Beherrscht(beruf)) return $"{konto.Inhaber} beherrscht das Handwerk der {beruf} nicht.";
        var grad = konto.Meistergrad(beruf);
        if (m.PruefungFuer(grad + 1) is not { } p) return $"{konto.Inhaber} ist bereits {beruf} im höchsten Meistergrad ({grad}).";
        var erfahrung = konto.Erfahrung(beruf);
        if (erfahrung < p.Erfahrung)
            return $"Für den {grad + 1}. Meistergrad braucht es {Zahlenformat.Ganz(p.Erfahrung)} Stunden Erfahrung als {beruf} – " +
                   $"es sind erst {Zahlenformat.Ganz(Math.Floor(erfahrung))}.";
        if (konto.Vermoegen < p.Gebuehr)
            return $"Die Prüfungsgebühr beträgt {Zahlenformat.Ganz(p.Gebuehr)} {Daten.Allgemein.Waehrung}.";
        return null;
    }

    /// <summary>Warum dieser Betrieb (noch) nicht ausgebaut werden kann (null = es geht).</summary>
    public string? AusbauVerboten(Konto konto, Betrieb betrieb)
    {
        if (Daten.Meistergrade is not { } m) return "In dieser Stadt gibt es keine Gebäudestufen.";
        if (betrieb.Artdaten.Ausbau is not { } ziel) return $"{betrieb.Name} ist schon auf der höchsten Stufe.";
        if (betrieb.UmbauMinuten > 0) return $"{betrieb.Name} wird gerade umgebaut.";
        var neu = Daten.Art(ziel);
        var beruf = betrieb.Artdaten.Beruf;
        var noetig = m.GradFuerStufe(neu.Stufe);
        if (konto.Meistergrad(beruf) < noetig)
            return $"Für Stufe {neu.Stufe} ({neu.Name}) braucht es {beruf} im {noetig}. Meistergrad – {konto.Inhaber} ist im {konto.Meistergrad(beruf)}.";
        if (konto.Vermoegen < betrieb.Artdaten.AusbauKosten + neu.Fixkosten)
            return $"Der Ausbau ({neu.Name}) kostet {Zahlenformat.Ganz(betrieb.Artdaten.AusbauKosten)} {Daten.Allgemein.Waehrung}, " +
                   $"dazu ein Jahr Unterhalt ({Zahlenformat.Ganz(neu.Fixkosten)}).";
        return null;
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
        konto.BerufLernen(betrieb.Artdaten.Beruf); // wer einen Betrieb führt, beherrscht sein Handwerk

        startkarren ??= betrieb.Artdaten.Startkarren;
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

        // Gäste der vergangenen Stunde kehren in die Schänken ein.
        if (zeit.MinuteImTag % 60 == 0 && zeit.VergangeneMinuten > 0)
            Gastwirtschaft.Stunde(zeit);

        // Die KI überlegt jede volle Stunde – mit den Marktpreisen dieser Stunde.
        if (zeit.MinuteImTag % 60 == 0 && zeit.VergangeneMinuten > 0)
            foreach (var rivale in _rivalen)
                rivale.Stunde(); // bankrotte Rivalen tun nichts mehr

        if (IstArbeitszeit(zeit))
            foreach (var betrieb in _betriebe)
                // Handwerkskunst des Oberhaupts; im Pestjahr fehlt ein Teil der Arbeitskräfte
                betrieb.Arbeitsminute(Melden, Familien.LeistungsFaktor(betrieb.Konto) * (1 - Gesundheit.Krankenstand));

        // Bei Nacht und Nebel: Die Banden aus dem Hinterzimmer ziehen los.
        if (zeit.MinuteImTag == Unterwelt.Nachtstunde)
            Gauner.Nacht(zeit);

        // Fairness: Wer in derselben Minute am Markt ankommt, ist in zufälliger Reihenfolge dran.
        // Sonst hätte immer der zuerst eröffnete Betrieb den besseren Preis (im Multiplayer: wer zuerst beitritt).
        _alleKarren.Clear();
        foreach (var betrieb in _betriebe.Where(b => b.Konto.IstAktiv)) // Bankrotte und erloschene Häuser fahren nicht mehr
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
        foreach (var betrieb in _betriebe.Where(b => b.Konto.IstAktiv))
            betrieb.Jahresabrechnung(_zufall, m => Meldung?.Invoke(m));
        Rat.ProtokollBeginnen();
        Justiz.Gerichtstag(); // vor dem Amtseinkommen: Wer verurteilt wird, verliert Amt und Einkommen
        Rat.EinkommenZahlen();
        Familien.UnterhaltZahlen();
        foreach (var konto in _konten)
            konto.JahrAbschliessen(neuesJahr);
        SchuldenPruefen();
        Gesundheit.Jahresende();  // Kranke sterben oder genesen – mit dem alten Jahr
        Familien.Jahreswechsel(); // Tod und Erbfall vor der Ratssitzung: Ein verwaistes Amt wird gleich neu besetzt
        Gesundheit.NeuesJahr();   // Pest und neue Krankheiten
        Gastwirtschaft.Jahresende(); // Stammtisch-Gunst vor der Ratssitzung (Wahlen)
        Rat.Sitzung(_zufall, neuesJahr - 1);
        Gauner.Jahresende();
        AktionspunkteVerteilen(); // die neue Runde beginnt
        foreach (var rivale in _rivalen.Where(r => r.Konto.IstAktiv))
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
        foreach (var konto in _konten.Where(k => k.IstAktiv && k.Schuldenjahre > 0))
        {
            if (konto.Schuldenjahre >= grenze)
            {
                konto.IstBankrott = true;
                Stilllegen(konto);
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

    /// <summary>Ein Haus scheidet aus (bankrott oder erloschen): Alle Arbeitskräfte gehen, Amt und Bewerbung sind verloren.</summary>
    internal void Stilllegen(Konto konto)
    {
        foreach (var betrieb in _betriebe.Where(b => b.Konto == konto))
            foreach (var kraft in betrieb.Belegung.Keys.ToList())
                betrieb.Einstellen(kraft, 0, Statut);
        Rat.Ausscheiden(konto);
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
        // Aktionspunkte: erst prüfen, nur bei Erfolg abbuchen – ein abgelehnter Befehl kostet nichts.
        var kosten = ApKosten(absender, befehl);
        if (kosten > 0 && absender.IstAktiv && !absender.Aktionspunkte.Reicht(kosten)
            && !(Justiz.IstAktiv && Justiz.ImKerker(absender)))
            return Befehlsergebnis.Abgelehnt($"Nicht genug Aktionspunkte: Das kostet {kosten} AP, {absender.Inhaber} hat noch {absender.Aktionspunkte.Vorrat}.");
        var ergebnis = Bearbeiten(absender, befehl);
        if (ergebnis.Erfolg && kosten > 0)
            absender.Aktionspunkte.Ausgeben(kosten);
        return ergebnis;
    }

    private Befehlsergebnis Bearbeiten(Konto absender, Befehl befehl)
    {
        if (absender.IstBankrott)
            return Befehlsergebnis.Abgelehnt($"{absender.Inhaber} ist bankrott und kann nichts mehr unternehmen.");
        if (absender.IstErloschen)
            return Befehlsergebnis.Abgelehnt($"Das Haus {absender.Inhaber} ist erloschen.");
        if (Justiz.IstAktiv && Justiz.ImKerker(absender) && befehl is not (BetriebsBefehl or ErbenBestimmen or ErpressungBeantworten or AusbildungWaehlen
                                                                     or MedicusRufen or SchnapsKaufen or SchnapsGeben))
            return Befehlsergebnis.Abgelehnt($"Das Oberhaupt von {absender.Inhaber} sitzt bis Ende {Justiz.KerkerBis(absender)} im Kerker. " +
                                             "Nur die Betriebe laufen weiter.");
        if (befehl is StandErwerben)
            return StandErwerbenAusfuehren(absender);
        if (befehl is LiegenschaftErwerben liegenschaft)
            return LiegenschaftErwerbenAusfuehren(absender, liegenschaft.Name);
        if (befehl is BerufErlernen lehre)
            return BerufErlernenAusfuehren(absender, lehre.Beruf);
        if (befehl is MeisterpruefungAblegen pruefung)
            return MeisterpruefungAusfuehren(absender, pruefung.Beruf);
        if (befehl is AmtBewerben or Schenken or StatutBeantragen && !Rat.IstAktiv)
            return Befehlsergebnis.Abgelehnt("In dieser Stadt gibt es keinen Stadtrat.");
        if (befehl is AmtBewerben bewerbung)
            return Rat.Bewerben(absender, bewerbung.Amt);
        if (befehl is Schenken geschenk)
            return Rat.Schenken(absender, geschenk.Ratsherr, geschenk.Betrag);
        if (befehl is StatutBeantragen antrag)
            return Rat.Beantragen(absender, antrag.Statut);
        if (befehl is SpitzelAnsetzen or Anklagen)
        {
            if (!Justiz.IstAktiv)
                return Befehlsergebnis.Abgelehnt("In dieser Stadt gibt es kein Gericht.");
            var haus = befehl is SpitzelAnsetzen s ? s.Haus : ((Anklagen)befehl).Haus;
            if (_konten.FirstOrDefault(k => k.Inhaber == haus) is not { } ziel)
                return Befehlsergebnis.Abgelehnt($"Ein Haus „{haus}“ gibt es nicht.");
            return befehl is SpitzelAnsetzen ? Justiz.SpitzelAnsetzen(absender, ziel) : Justiz.Anklagen(absender, ziel);
        }
        if (befehl is Beauftragen or Erpressen or ErpressungBeantworten)
        {
            if (!Gauner.IstAktiv)
                return Befehlsergebnis.Abgelehnt("In dieser Stadt gibt es keine Unterwelt.");
            var hausName = befehl switch { Beauftragen b => b.Haus, Erpressen e => e.Haus, ErpressungBeantworten a => a.Erpresser, _ => "" };
            if (_konten.FirstOrDefault(k => k.Inhaber == hausName) is not { } haus)
                return Befehlsergebnis.Abgelehnt($"Ein Haus „{hausName}“ gibt es nicht.");
            return befehl switch
            {
                Beauftragen b => Gauner.Beauftragen(absender, haus, b.Art, b.Betrieb),
                Erpressen e => Gauner.Erpressen(absender, haus, e.Betrag),
                ErpressungBeantworten a => Gauner.ErpressungBeantworten(absender, haus, a.Zahlen),
                _ => Befehlsergebnis.Abgelehnt("Unbekannter Befehl."),
            };
        }
        if (befehl is AusbildungWaehlen ausbildung)
            return Familien.AusbildungWaehlen(absender, ausbildung.Kind, ausbildung.Weg);
        if (befehl is TalentTrainieren training)
            return Familien.TalentTrainieren(absender, training.Talent);
        if (befehl is MedicusRufen medicus)
            return Gesundheit.MedicusRufen(absender, medicus.Person);
        if (befehl is SchnapsKaufen kauf)
            return Gesundheit.SchnapsKaufen(absender, kauf.Stueck);
        if (befehl is SchnapsGeben gabe)
            return Gesundheit.SchnapsGeben(absender, gabe.Person);
        if (befehl is Werben or Heiraten or ErbenBestimmen && Familien.Von(absender) is null)
            return Befehlsergebnis.Abgelehnt($"{absender.Inhaber} hat keine Familie.");
        if (befehl is Werben werben)
            return Familien.Werben(absender, werben.Kandidat, werben.Betrag);
        if (befehl is Heiraten heirat)
            return Familien.Heiraten(absender, heirat.Kandidat);
        if (befehl is ErbenBestimmen testament)
            return Familien.ErbenBestimmen(absender, testament.Person);
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
                WaechterSetzen s => WaechterAusfuehren(betrieb, s),
                BetriebAusbauen => AusbauenAusfuehren(betrieb),
                BardeEinladen => Gastwirtschaft.BardeEinladen(betrieb),
                WuerfeltischAufstellen t => Gastwirtschaft.WuerfeltischAufstellen(betrieb, t.Aufstellen),
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
        var beruf = Daten.Art(l.Art).Beruf;
        if (Daten.Berufe.Count > 0 && konto.Berufe.Count > 0 && !konto.Beherrscht(beruf)) // ohne jedes Handwerk darf man mit jedem anfangen
            return Befehlsergebnis.Abgelehnt($"{name} ist ein Betrieb der {beruf}. {konto.Inhaber} beherrscht dieses Handwerk nicht – erst erlernen.");

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

    private Befehlsergebnis BerufErlernenAusfuehren(Konto konto, string beruf)
    {
        if (!Daten.Berufe.TryGetValue(beruf, out var b))
            return Befehlsergebnis.Abgelehnt($"Einen Beruf „{beruf}“ gibt es nicht.");
        if (konto.Beherrscht(beruf))
            return Befehlsergebnis.Abgelehnt($"{konto.Inhaber} beherrscht das Handwerk der {beruf} schon.");
        var waehrung = Daten.Allgemein.Waehrung;
        if (konto.Vermoegen < b.Lehrgeld)
            return Befehlsergebnis.Abgelehnt($"Lehrgeld und Werkzeug kosten {Zahlenformat.Ganz(b.Lehrgeld)} {waehrung}.");
        konto.Investition(b.Lehrgeld);
        konto.BerufLernen(beruf);
        Melden($"{konto.Inhaber} erlernt das Handwerk der {beruf}.");
        return Befehlsergebnis.Ok($"{konto.Inhaber} beherrscht jetzt das Handwerk der {beruf} ({Zahlenformat.Ganz(b.Lehrgeld)} {waehrung} Lehrgeld). " +
                                  $"Werkstätten dieses Berufs lassen sich nun erwerben.");
    }

    private Befehlsergebnis MeisterpruefungAusfuehren(Konto konto, string beruf)
    {
        if (MeisterpruefungVerboten(konto, beruf) is { } grund)
            return Befehlsergebnis.Abgelehnt(grund);
        var grad = konto.Meistergrad(beruf) + 1;
        var p = Daten.Meistergrade!.PruefungFuer(grad)!;
        konto.Investition(p.Gebuehr); // wie das Lehrgeld: kein laufender Betriebsaufwand
        konto.GradSetzen(beruf, grad);
        var m = Daten.Meistergrade;
        var freigeschaltet = Daten.Betriebsarten.Values.FirstOrDefault(a => a.Beruf == beruf && m.GradFuerStufe(a.Stufe) == grad && a.Stufe > 1);
        var zusatz = freigeschaltet is not null ? $" Nun dürfen die Werkstätten auf Stufe {freigeschaltet.Stufe} ausgebaut werden ({freigeschaltet.Name})."
                   : grad == m.ApBonusAbGrad ? $" Höchster Meister: {m.ApBonus} Aktionspunkt mehr je Runde." : "";
        Melden($"{konto.Inhaber} legt die Meisterprüfung ab: {beruf} im {grad}. Grad.");
        return Befehlsergebnis.Ok($"{konto.Inhaber} ist nun {beruf} im {grad}. Meistergrad ({Zahlenformat.Ganz(p.Gebuehr)} {Daten.Allgemein.Waehrung} Gebühr).{zusatz}");
    }

    private Befehlsergebnis AusbauenAusfuehren(Betrieb betrieb)
    {
        if (AusbauVerboten(betrieb.Konto, betrieb) is { } grund)
            return Befehlsergebnis.Abgelehnt(grund);
        var kosten = betrieb.Artdaten.AusbauKosten;
        var neu = Daten.Art(betrieb.Artdaten.Ausbau!.Value);
        betrieb.Konto.Investition(kosten);
        betrieb.Ausbauen(betrieb.Artdaten.Ausbau.Value, Daten.Meistergrade!.UmbauMinuten);
        Melden($"{betrieb.Konto.Inhaber} baut {betrieb.Name} aus: Stufe {neu.Stufe}, {neu.Name}.");
        return Befehlsergebnis.Ok($"{betrieb.Name} wird ausgebaut – Stufe {neu.Stufe}, {neu.Name} ({Zahlenformat.Ganz(kosten)} {Daten.Allgemein.Waehrung}): " +
                                  $"{neu.Plaetze} Arbeitsplätze, neue Waren: {string.Join(", ", NeueWaren(betrieb))}.");
    }

    /// <summary>Was eine Werkstatt nach dem Ausbau zusätzlich herstellen kann (bzw. gerade dazubekommen hat).</summary>
    private IEnumerable<string> NeueWaren(Betrieb betrieb)
    {
        var vorher = Daten.Betriebsarten.Values.FirstOrDefault(a => a.Ausbau == betrieb.Art)?.Produkte ?? new List<string>();
        return betrieb.Artdaten.Produkte.Where(p => !vorher.Contains(p));
    }

    private Befehlsergebnis ProduktWaehlenAusfuehren(Betrieb betrieb, ProduktWaehlen befehl)
    {
        if (!betrieb.IstWerkstatt)
            return Befehlsergebnis.Abgelehnt($"{betrieb.Name} ist keine Werkstatt und stellt nichts her.");
        if (!betrieb.Artdaten.Produkte.Contains(befehl.Ware))
            return Befehlsergebnis.Abgelehnt($"{befehl.Ware} kann eine {betrieb.Artdaten.Name} nicht herstellen.");
        if (betrieb.Produkt == befehl.Ware)
            return Befehlsergebnis.Ok($"{betrieb.Name} stellt bereits {befehl.Ware} her.");
        betrieb.Produkt = befehl.Ware;
        return Befehlsergebnis.Ok($"{betrieb.Name} stellt nun {befehl.Ware} her.");
    }

    private Befehlsergebnis FoerderungAusfuehren(Betrieb betrieb, FoerderungAufteilen befehl)
    {
        if (betrieb.Rohstoffe.Count < 2)
            return Befehlsergebnis.Abgelehnt($"{betrieb.Name} fördert nur eine Ware – da gibt es nichts aufzuteilen.");
        var zweiter = betrieb.Rohstoffe[1];
        if (befehl.Silberanteil is < 0 or > 1 || double.IsNaN(befehl.Silberanteil))
            return Befehlsergebnis.Abgelehnt($"Der Anteil für {zweiter} muss zwischen 0 % und 100 % liegen.");
        betrieb.Silberanteil = befehl.Silberanteil;
        return Befehlsergebnis.Ok($"{betrieb.Name} fördert nun zu {Zahlenformat.Ganz(befehl.Silberanteil * 100)} % {zweiter}.");
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

    private Befehlsergebnis WaechterAusfuehren(Betrieb betrieb, WaechterSetzen befehl)
    {
        if (Daten.Gauner is not { } g)
            return Befehlsergebnis.Abgelehnt("Ohne Unterwelt braucht es keine Wächter.");
        if (betrieb.Waechter == befehl.Angestellt)
            return Befehlsergebnis.Ok($"{betrieb.Name} hat {(befehl.Angestellt ? "schon einen" : "keinen")} Wächter.");
        betrieb.Waechter = befehl.Angestellt;
        return Befehlsergebnis.Ok(befehl.Angestellt
            ? $"{betrieb.Name} stellt einen Wächter ein ({Zahlenformat.Ganz(g.WaechterKosten)} {Daten.Allgemein.Waehrung} im Jahr)."
            : $"{betrieb.Name} entlässt den Wächter.");
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
        Rat = Rat.Sichern(k => _konten.IndexOf(k)),
        Familien = Familien.IstAktiv ? Familien.Sichern(k => _konten.IndexOf(k)) : null,
        Justiz = Justiz.IstAktiv ? Justiz.Sichern(k => _konten.IndexOf(k)) : null,
        Gauner = Gauner.IstAktiv ? Gauner.Sichern(k => _konten.IndexOf(k)) : null,
        Gesundheit = Gesundheit.IstAktiv ? Gesundheit.Sichern(k => _konten.IndexOf(k)) : null,
        Gastwirtschaft = Gastwirtschaft.IstAktiv ? Gastwirtschaft.Sichern() : null,
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
            if (Markt.Staende.Any(x => x.Ware == ware))
                Markt[ware].Wiederherstellen(stand);
        foreach (var b in _betriebe) // alte Spielstände kennen keine Berufe: Wer einen Betrieb hat, beherrscht ihn
            b.Konto.BerufLernen(b.Artdaten.Beruf);
        foreach (var rs in s.Rivalen)
        {
            var rivale = new KiRivale(_konten[rs.Konto], rs.Profil, this);
            rivale.Wiederherstellen(rs);
            _rivalen.Add(rivale);
        }
        if (s.Rat is { } rat)
            Rat.Wiederherstellen(rat, _konten);
        if (s.Familien is { } familien && Familien.IstAktiv)
            Familien.Wiederherstellen(familien, _konten);
        if (s.Justiz is { } justiz && Justiz.IstAktiv)
            Justiz.Wiederherstellen(justiz, _konten);
        if (s.Gauner is { } gauner && Gauner.IstAktiv)
            Gauner.Wiederherstellen(gauner, _konten);
        if (s.Gesundheit is { } gesundheit && Gesundheit.IstAktiv)
            Gesundheit.Wiederherstellen(gesundheit, _konten);
        if (s.Gastwirtschaft is { } gast && Gastwirtschaft.IstAktiv)
            Gastwirtschaft.Wiederherstellen(gast);
    }

    public double HoechstesVermoegen => _konten.Count == 0 ? 0 : _konten.Max(k => k.Vermoegen);
}
