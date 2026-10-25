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
public sealed partial class Stadtwirtschaft
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

    /// <summary>Die Stadtkasse: Stand, Einnahmen und Ausgaben, Steuersatz (PR #200).</summary>
    public Politik.Stadtkasse Kasse { get; }

    /// <summary>Anträge zu Steuer, Bau und Fest; Bauvorhaben und Fest der Stadt (PR #200).</summary>
    public Politik.Ratsantraege Ratsantraege { get; }

    /// <summary>Die Befugnisse der Ämter (PR #200).</summary>
    public Politik.Amtsbefugnisse Befugnisse { get; }

    /// <summary>Der geltende Steuersatz auf alle Umsätze (0,05 = 5 %) – ohne Stadtkasse <c>allgemein.umsatzsteuer</c>.</summary>
    public double Umsatzsteuer => Kasse.Umsatzsteuer;

    /// <summary>Steuersatz auf Verkäufe dieses Hauses am Markt (der Marktmeister zahlt weniger).</summary>
    public double SteuersatzMarkt(Konto konto) => Befugnisse.SteuersatzMarkt(konto);

    /// <summary>Familien der Häuser und die Heiratskandidaten der Stadt.</summary>
    public Familienbuch Familien { get; }

    /// <summary>Das Stadtgericht: Spuren, Spitzel, Beweise und Anklagen.</summary>
    public Stadtgericht Justiz { get; }

    /// <summary>Die Unterwelt: Hinterzimmer, Hinterhalte, Erpresserbriefe.</summary>
    public Unterwelt Gauner { get; }

    /// <summary>Taschendiebe, Einbruch, Auskundschaften, Schutzgeld und Entführung – die Leute des Diebes.</summary>
    public Diebeswesen Diebe { get; }

    /// <summary>Sold, Patrouille, Wachturm, Razzia, Kopfgeld und Geleitschutz – die Wache des Gardisten.</summary>
    public Ordnung.Wachwesen Wache { get; }

    /// <summary>Kasse, Wechsel und Kredite der Geldleiher.</summary>
    public Bank.Bankwesen Bank { get; }

    /// <summary>Düfte mit Wirkung (Moschus, Zwietracht, Heiligenduft) und Giftdolche – Waren des Parfumeurs.</summary>
    public Duft.Duftwesen Duft { get; }

    /// <summary>Konfessionen, Glaubensnähe im Rat, Bekehrung und Stadtglaube.</summary>
    public Glaube.Glaubenswesen Glaube { get; }

    /// <summary>Wohnhäuser der Häuser (Stufe, Ausbau, Unterhalt, Pflicht für den Stand).</summary>
    public Wohnen.Wohnungswesen Wohnen { get; }

    /// <summary>Grundstücke, Leerstand kaufen und abreißen, Neubau von Wohnhäusern und Werkstätten (braucht die <see cref="Karte"/>).</summary>
    public Bauen.Bauwesen Bauen { get; }

    /// <summary>Belag der Wege: Austritt, Verfall, Bauaufträge, Pflaster des Stadtrats (braucht die <see cref="Karte"/> und „wegebau“).</summary>
    public Bauen.Wegebau Wegebau { get; }

    /// <summary>Schiffe der Häuser, ihre Fahrten zu den fernen Städten am Fluss und deren Märkte (braucht die <see cref="Karte"/> mit Hafen und „schiffshandel“).</summary>
    public Schiffahrt Schiffahrt { get; }

    /// <summary>Die Karte der Stadt: Wege, Bauzonen, feste Bauten, Wohnhausplätze (null: ohne Karte – dann wird nicht gebaut).</summary>
    public Bauen.Stadtkarte? Karte { get; private set; }

    /// <summary>
    /// Legt die Karte der Stadt fest (einmal, gleich nach dem Erzeugen). Häuser, die es schon gibt, bekommen ihre Startplätze in Konto-Reihenfolge.
    /// </summary>
    public void KarteSetzen(Bauen.Stadtkarte karte)
    {
        if (Karte is not null) throw new InvalidOperationException("Die Karte der Stadt steht schon fest.");
        Karte = karte;
        Kasse.KarteGesetzt(karte.Kennung);
        for (var i = 0; i < _konten.Count; i++)
            Bauen.StartplatzVergeben(_konten[i], i);
    }

    /// <summary>Völkerbonus als Faktor auf die Leistung: 1 + Bonus des Berufs für das Volk des Oberhaupts (1 ohne Familie).</summary>
    public double VolksFaktor(Konto konto, string beruf) => 1 + VolksBonus(konto, beruf);

    /// <summary>Völkerbonus (0,05 = +5 %) für das Volk des Oberhaupts in diesem Beruf.</summary>
    public double VolksBonus(Konto konto, string beruf) =>
        Familien.Von(konto) is { } f && Daten.Berufe.TryGetValue(beruf, out var b) ? b.Voelker.GetValueOrDefault(f.Oberhaupt.Volk) : 0;

    /// <summary>Was eine Arbeitsminute in einem Betrieb ohne Ware wert ist (Dieb: Beute, Gardist: Sold nach Steuer).</summary>
    public double WertOhneWare(BetriebsartDaten d) =>
        Diebe.IstAktiv && d.Beruf == Diebe.Daten.Beruf ? Diebe.WertJeMinute(d.Stufe)
        : Wache.IstAktiv && d.Beruf == Wache.Daten.Beruf ? Wache.WertJeMinute(d.Stufe)
        : Bank.IstAktiv && d.Beruf == Bank.Daten.Beruf ? Bank.WertJeMinute(d.Stufe)
        : 0;

    /// <summary>Krankheiten, Pest, Medicus und Hausapotheke.</summary>
    public Gesundheitswesen Gesundheit { get; }

    /// <summary>Gäste und Ausschank der Wirtshäuser, Barde, Würfeltisch, Stammtisch.</summary>
    public Gastwirtschaft Gastwirtschaft { get; }

    /// <summary>Werkstatt wie in Gilde 1: Arbeiter mit eigener Ware, Lohnstufen, Prämie, Laune, Unfälle, Beförderung und Einrichtung.</summary>
    public Werkstattwesen Werkstaetten { get; }

    /// <summary>Messe und Kollekte, Ablass, Beichte, Lehr- und Hetzschriften.</summary>
    public Kirche.Kirchenwesen Kirchenwesen { get; }

    /// <summary>Die Einwohner der Stadt – Bürger-KI (Bereich Spiel-KI). Ohne Abschnitt „buerger" leer.</summary>
    public Buerger.Einwohnerschaft Einwohnerschaft { get; }

    /// <summary>Das laufende Spieljahr (das Jahr der laufenden Jahresrechnung).</summary>
    public int Jahr => _konten.Count > 0 ? _konten[0].Laufend.Jahr : Spielzeit.StartJahr;

    /// <summary>Konten, die von der KI geführt werden.</summary>
    public IReadOnlyList<KiRivale> Rivalen => _rivalen;

    /// <summary>Meldungen für den Spieler (Ausbrüche, Entlassungen …).</summary>
    public event Action<string>? Meldung;

    /// <summary>Ein Konto ist bankrott gegangen (nach der Jahresabrechnung).</summary>
    public event Action<Konto>? Bankrott;

    /// <summary>Ein Konto hat eine Werkstatt gekauft oder eine Grube gepachtet (Gegenstück: <see cref="BetriebAbgegeben"/>).</summary>
    public event Action<Betrieb>? BetriebErworben;

    /// <summary>Ein Konto ist in einen höheren Stand aufgestiegen.</summary>
    public event Action<Konto, StandDaten>? Aufgestiegen;

    // ── Stände ──────────────────────────────────────────────────────────────

    /// <summary>Der aktuelle Stand eines Kontos.</summary>
    public StandDaten StandVon(Konto konto) => Daten.Staende[Math.Clamp(konto.Stand, 0, Daten.Staende.Count - 1)];

    /// <summary>Der nächsthöhere Stand oder null, wenn das Konto ganz oben ist.</summary>
    public StandDaten? NaechsterStand(Konto konto) =>
        konto.Stand + 1 < Daten.Staende.Count ? Daten.Staende[konto.Stand + 1] : null;

    /// <summary>
    /// Rangliste: höherer Stand vor niedrigerem, innerhalb eines Standes nach Reinvermögen.
    /// Bankrotte und erloschene Häuser stehen am Ende.
    /// </summary>
    public IReadOnlyList<Konto> Rangliste => _konten
        .OrderBy(k => !k.IstAktiv)
        .ThenByDescending(k => k.Stand)
        .ThenByDescending(k => k.Reinvermoegen)
        .ThenBy(k => k.Inhaber, StringComparer.Ordinal)
        .ToList();

    /// <summary>Die Uhr der Simulation (für Befehle, die von der Uhrzeit abhängen – etwa die Marktzeiten).</summary>
    public Spielzeit Uhr { get; internal set; } = new();

    // ── Handel von Hand ─────────────────────────────────────────────────────

    /// <summary>Faktor auf den Marktpreis beim Einkauf von Hand: Händlerspanne × Verhandeln.</summary>
    public double HandKaufFaktor(Konto konto) => (1 + (Daten.Handel?.Aufschlag ?? 0)) * Familien.EinkaufsFaktor(konto);

    /// <summary>Was ein Einkauf von Hand jetzt kosten würde (jedes Stück hebt den Preis).</summary>
    public double HandKaufpreis(Konto konto, string ware, int stueck) => Markt[ware].HandKaufpreis(stueck, HandKaufFaktor(konto));

    /// <summary>Wann käme ein Karren dieses Betriebs am Markt an, wenn er jetzt losführe? (Uhrzeit in Minuten nach Mitternacht)</summary>
    public int AnkunftAmMarkt(Betrieb betrieb) => Uhr.UhrzeitInMinuten + Fahrzeit(betrieb, zumMarkt: true);

    /// <summary>Warum dieser Handelsauftrag nicht geht – oder null.</summary>
    /// <param name="vormerken">Für einen Karren, der unterwegs ist: Zustand und Marktzeit werden erst beim Start geprüft.</param>
    public string? HandelsauftragVerboten(Betrieb betrieb, int karrenNr, IReadOnlyDictionary<string, int> verkauf, IReadOnlyDictionary<string, int> kauf,
        bool vormerken = false)
    {
        if (Daten.Handel is not { } h) return "In dieser Stadt wird nicht von Hand gehandelt.";
        if (karrenNr < 0 || karrenNr >= betrieb.Karren.Count) return $"{betrieb.Name} hat keinen Karren Nr. {karrenNr + 1}.";
        var karren = betrieb.Karren[karrenNr];
        if (karren.Zustand != KarrenZustand.AmBetrieb && !vormerken) return $"{karren.Name} ist unterwegs – erst wenn er wieder am Betrieb steht.";
        var verkaufen = verkauf.Where(x => x.Value > 0).ToList();
        var kaufen = kauf.Where(x => x.Value > 0).ToList();
        if (verkaufen.Count == 0 && kaufen.Count == 0) return "Der Auftrag ist leer.";
        if (verkauf.Concat(kauf).Any(x => x.Value < 0)) return "Mengen dürfen nicht negativ sein.";
        foreach (var (ware, stueck) in verkaufen)
            if (betrieb.ImLager(ware) < stueck) return $"Im Lager liegen nur {betrieb.ImLager(ware)} {ware}.";
        foreach (var (ware, _) in kaufen)
            if (Markt.Staende.All(s => s.Ware != ware)) return $"Auf dem Markt gibt es keine Ware „{ware}“.";
        int Flaechen(IEnumerable<KeyValuePair<string, int>> teil) => teil.Sum(x => (x.Value + karren.Daten.KapazitaetJeSorte - 1) / karren.Daten.KapazitaetJeSorte);
        var fasst = $"{karren.Name} fasst {karren.Daten.Sorten} × {karren.Daten.KapazitaetJeSorte} Stück (je Ladefläche eine Ware).";
        if (Flaechen(verkaufen) > karren.Daten.Sorten) return "Zu viel zum Verkaufen: " + fasst;
        if (Flaechen(kaufen) > karren.Daten.Sorten) return "Zu viel zum Einkaufen: " + fasst;
        foreach (var (ware, stueck) in kaufen)
        {
            var danach = betrieb.ImLager(ware) - verkauf.GetValueOrDefault(ware) + stueck;
            if (danach > betrieb.LagerKapazitaetJeWare)
                return $"Das Lager fasst nur {betrieb.LagerKapazitaetJeWare} {ware} – danach wären es {danach}.";
        }
        var ankunft = AnkunftAmMarkt(betrieb);
        if (!vormerken && (ankunft < h.MarktVon * 60 || ankunft > h.MarktBis * 60))
            return $"Der Markt hat von {h.MarktVon} bis {h.MarktBis} Uhr offen – der Karren käme um {ankunft / 60:00}:{ankunft % 60:00} an.";
        var kosten = kaufen.Sum(x => HandKaufpreis(betrieb.Konto, x.Key, x.Value));
        if (kosten > betrieb.Konto.Vermoegen + verkaufen.Sum(x => Markt[x.Key].Preis * x.Value))
            return $"Die Einkäufe kosten etwa {Zahlenformat.Ganz(kosten)} {Daten.Allgemein.Waehrung} – so viel habt ihr nicht.";
        return null;
    }

    private Befehlsergebnis HandelsauftragAusfuehren(Betrieb betrieb, Handelsauftrag befehl)
    {
        // Ist der Karren unterwegs, wird der Auftrag vorgemerkt: Er startet, sobald der Karren zurück ist.
        var unterwegs = befehl.Karren >= 0 && befehl.Karren < betrieb.Karren.Count && betrieb.Karren[befehl.Karren].Zustand != KarrenZustand.AmBetrieb;
        if (HandelsauftragVerboten(betrieb, befehl.Karren, befehl.Verkauf, befehl.Kauf, vormerken: unterwegs) is { } grund)
            return Befehlsergebnis.Abgelehnt(grund);
        var karren = betrieb.Karren[befehl.Karren];
        if (unterwegs)
        {
            var zurueck = karren.Zustand == KarrenZustand.ZumBetrieb ? karren.Ankunft
                : karren.Ankunft + Fahrzeit(betrieb, zumMarkt: false);
            var um = Uhr.UhrzeitInMinuten + (int)(zurueck - Uhr.VergangeneMinuten);
            karren.Vormerkung = new Vormerkung(new Dictionary<string, int>(befehl.Verkauf), new Dictionary<string, int>(befehl.Kauf));
            return Befehlsergebnis.Ok($"{karren.Name} ist unterwegs – der Auftrag ist vorgemerkt und startet, sobald er zurück ist (etwa {um / 60:00}:{um % 60:00}).");
        }
        return HandelsauftragStarten(betrieb, karren, befehl.Verkauf, befehl.Kauf);
    }

    /// <summary>Der Karren ist zurück: den vorgemerkten Auftrag starten – oder melden, warum er verfällt.</summary>
    internal bool VorgemerktenAuftragStarten(Karren karren, Vormerkung v)
    {
        var betrieb = karren.Heimat;
        var nr = betrieb.Karren.ToList().IndexOf(karren);
        if (!betrieb.Konto.IstAktiv) return false;
        if (HandelsauftragVerboten(betrieb, nr, v.Verkauf, v.Kauf) is { } grund)
        {
            Melden($"{karren.Name} ({betrieb.Name}): Der vorgemerkte Handelsauftrag verfällt – {grund}");
            return false;
        }
        Melden(HandelsauftragStarten(betrieb, karren, v.Verkauf, v.Kauf).Meldung);
        return true;
    }

    private Befehlsergebnis HandelsauftragStarten(Betrieb betrieb, Karren karren, IReadOnlyDictionary<string, int> verkauf, IReadOnlyDictionary<string, int> kauf)
    {
        var waehrung = Daten.Allgemein.Waehrung;
        // Was man kauft und nicht selbst verarbeitet, soll der Karren nicht gleich wieder verkaufen.
        var gehalten = kauf.Where(x => x.Value > 0 && !betrieb.Zutaten.ContainsKey(x.Key) && !betrieb.HaeltZurueck(x.Key)).Select(x => x.Key).ToList();
        foreach (var ware in gehalten) betrieb.Zurueckhalten(ware, true);
        var schaetzung = kauf.Where(x => x.Value > 0).Sum(x => HandKaufpreis(betrieb.Konto, x.Key, x.Value));
        karren.AuftragAnnehmen(verkauf, kauf, Uhr, this);
        var ankunft = Uhr.UhrzeitInMinuten + (int)(karren.Ankunft - Uhr.VergangeneMinuten);
        return Befehlsergebnis.Ok($"{karren.Name} fährt mit dem Auftrag zum Markt (an {ankunft / 60:00}:{ankunft % 60:00})." +
                                  (schaetzung > 0 ? $" Einkauf heute etwa {Zahlenformat.Ganz(schaetzung)} {waehrung}." : "") +
                                  (gehalten.Count > 0 ? $" {string.Join(", ", gehalten)} wird im Lager zurückgehalten." : ""));
    }

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
        Kasse = new Politik.Stadtkasse(this, daten.Politik.Kasse);
        Ratsantraege = new Politik.Ratsantraege(this, daten.Politik);
        Befugnisse = new Politik.Amtsbefugnisse(this, daten.Politik.Befugnisse);
        Familien = new Familienbuch(this, daten.Dynastie, zufallsstartwert);
        Justiz = new Stadtgericht(this, daten.Justiz, zufallsstartwert);
        Gauner = new Unterwelt(this, daten.Gauner, zufallsstartwert);
        Diebe = new Diebeswesen(this, daten.Dieb, zufallsstartwert);
        Wache = new Ordnung.Wachwesen(this, daten.Wache, zufallsstartwert);
        Bank = new Bank.Bankwesen(this, daten.Geldleiher, zufallsstartwert);
        Duft = new Duft.Duftwesen(this, daten.Parfum, zufallsstartwert);
        Glaube = new Glaube.Glaubenswesen(this, daten.Glaube, zufallsstartwert);
        Wohnen = new Wohnen.Wohnungswesen(this, daten.Wohnen);
        Bauen = new Bauen.Bauwesen(this, daten.Bauen);
        Wegebau = new Bauen.Wegebau(this, daten.Wegebau);
        Schiffahrt = new Schiffahrt(this, daten.Schiffshandel);
        Gesundheit = new Gesundheitswesen(this, daten.Gesundheit, zufallsstartwert);
        Gastwirtschaft = new Gastwirtschaft(this, daten.Gastwirtschaft);
        Werkstaetten = new Werkstattwesen(this, daten.Werkstatt, zufallsstartwert);
        Kirchenwesen = new Kirche.Kirchenwesen(this, daten.Kirche, zufallsstartwert);
        Einwohnerschaft = new Buerger.Einwohnerschaft(this, daten.Buerger, zufallsstartwert);
    }

    // ── Einwohner und Nachfrage (PR #177) ───────────────────────────────────

    /// <summary>Häuser, die in der Stadt wirtschaften: im Spiel und mit mindestens einem Arbeiter.</summary>
    public int WirtschaftendeHaeuser => _konten.Count(k => k.IstAktiv && _betriebe.Any(b => b.Konto == k && b.Belegt > 0));

    /// <summary>Einwohner der Stadt nach <see cref="StadtDaten"/> (0 ohne Abschnitt „stadt“).</summary>
    public double Einwohner => Daten.Stadt?.Einwohner(WirtschaftendeHaeuser) ?? 0;

    /// <summary>
    /// Setzt den <see cref="Markt.Einwohnerfaktor"/> – zu Beginn jeder Minute und nach dem Laden. So gilt in einer Minute überall
    /// derselbe Wert, und eine geladene Partie rechnet genau wie das Original weiter.
    /// </summary>
    internal void EinwohnerZaehlen()
    {
        Markt.Einwohnerfaktor = Daten.Stadt?.Faktor(WirtschaftendeHaeuser) ?? 1;
        Markt.Ratsfaktor = Ratsantraege.Nachfragefaktor; // größerer Marktplatz, Fest (PR #200)
    }

    /// <summary>Legt ein Konto an – mit dem Startvermögen aus den Daten oder einem eigenen (z. B. Schwierigkeitsgrad).</summary>
    public Konto KontoEroeffnen(string inhaber, int startjahr = Spielzeit.StartJahr, double? startvermoegen = null)
    {
        var konto = new Konto(inhaber, startvermoegen ?? Daten.Allgemein.Startvermoegen, startjahr) { Stadtkasse = Kasse.Buchen };
        if (HatAktionspunkte)
            konto.Aktionspunkte.NeueRunde(AktionspunkteJeRunde, hoechstens: Daten.Aktionspunkte!.Hoechstens);
        _konten.Add(konto);
        Bauen.StartplatzVergeben(konto, _konten.Count - 1);
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
        if (Befugnisse.OhneAktionspunkte(konto, befehl)) return 0; // Kämmerer: Steuersatz; Bürgermeister: ein Antrag im Jahr
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
        (Daten.Meistergrade is { } m && konto.HoechsterMeistergrad >= m.ApBonusAbGrad ? m.ApBonus : 0) + Bank.ApBonus(konto);

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
        var betrieb = new Betrieb(name, art, konto, Daten, ganzjaehrig: Uhr.MinuteImTag == 0, jetzt: () => Uhr.VergangeneMinuten); // zum Jahresbeginn: ganzes Jahr
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

    /// <summary>Ist zu dieser Uhrzeit die übliche Arbeitszeit (8–20 Uhr; Markt, Rathaus, Kontor)?</summary>
    public bool IstArbeitszeit(Spielzeit zeit)
    {
        var uhr = zeit.UhrzeitInMinuten;
        return uhr >= Arbeitsbeginn && uhr < Arbeitsbeginn + Daten.Allgemein.Arbeitsminuten;
    }

    /// <summary>Ist zu dieser Uhrzeit Arbeitszeit in diesem Betrieb? Im Wirtshaus bis 22 Uhr (<see cref="Betrieb.Arbeitsende"/>).</summary>
    public bool IstArbeitszeit(Betrieb betrieb, Spielzeit zeit) => betrieb.IstArbeitszeit(zeit.UhrzeitInMinuten);

    /// <summary>Eine Spielminute. Wird von der Simulation VOR dem Weiterstellen der Uhr aufgerufen.</summary>
    internal void Tick(Spielzeit zeit)
    {
        EinwohnerZaehlen();
        if (zeit.MinuteImTag % 60 == 0 && zeit.VergangeneMinuten > 0)
            Markt.NeueStunde();
        else if (zeit.VergangeneMinuten > 0)
            Markt.NeueMinute();

        // Jede volle Stunde im Tag (die letzte Stunde des Jahres rechnet die Jahresrechnung ab, bevor sie beginnt).
        if (zeit.MinuteImTag % 60 == 0 && zeit.MinuteImTag > 0)
            StundeVorbei(zeit);

        // Jeder Betrieb in seiner Arbeitszeit (im Wirtshaus bis 22 Uhr).
        // Handwerkskunst des Oberhaupts; im Pestjahr fehlt ein Teil der Arbeitskräfte
        foreach (var betrieb in _betriebe)
            if (IstArbeitszeit(betrieb, zeit))
            {
                var volk = VolksFaktor(betrieb.Konto, betrieb.Artdaten.Beruf); // Zwerge schmieden besser, Elben predigen besser …
                betrieb.Arbeitsminute(Melden, Familien.LeistungsFaktor(betrieb.Konto) * volk * (1 - Gesundheit.Krankenstand), volk);
            }

        // Bei Nacht und Nebel: Die Banden aus dem Hinterzimmer ziehen los.
        if (zeit.MinuteImTag == Unterwelt.Nachtstunde)
        {
            Gauner.Nacht(zeit);
            Diebe.Nacht(); // die eigenen Leute der Diebe nach den angeheuerten Banden
            Duft.Nacht();  // Giftanschläge
        }

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
        Schiffahrt.Minute(zeit); // Schiffe legen ab, kommen an, kehren heim

        Wohnen.Minute(zeit); // fertige Ausbauten der Wohnhäuser
        Bauen.Minute(zeit);  // fertige Baustellen: Umzug, neue Werkstätten, Bauplätze
        Wegebau.Minute(zeit); // fertig ausgebaute Wege
        Ratsantraege.Minute(zeit); // fertige Bauvorhaben der Stadt

        // Bürger-KI: Die Einwohner planen jede volle Stunde ihren Tag (lesen nur, eigener Zufall).
        Einwohnerschaft.Minute(zeit);

        ToteBetriebeFreigeben(); // wer in dieser Minute erlosch (Giftanschlag), dessen Werkstätten stehen nun zum Verkauf
    }

    /// <summary>
    /// Eine volle Stunde ist vorbei: Gäste, Messe, Taschendiebe, Sold, Zinsen und Wechsel dieser Stunde –
    /// dann überlegt die KI, mit den Marktpreisen dieser Stunde.
    /// </summary>
    private void StundeVorbei(Spielzeit zeit)
    {
        Gastwirtschaft.Stunde(zeit);
        Kirchenwesen.Stunde(zeit); // zur Messe wird gespendet
        Diebe.Stunde(zeit);        // die Taschendiebe der vergangenen Stunde
        Wache.Stunde(zeit);        // Sold für die Wachleute
        Bank.Stunde(zeit);         // Zinsen der Stadtkunden, Wechsel; Kreditzinsen laufen auf
        Werkstaetten.Stunde(zeit); // Unfälle, Laune und Kündigungen in den Werkstätten
        Schiffahrt.Stunde();       // die Märkte der fernen Städte erholen sich
        foreach (var rivale in _rivalen)
            rivale.Stunde(); // bankrotte Rivalen tun nichts mehr
    }

    /// <summary>Jahresende für alle Betriebe und Konten.</summary>
    /// <param name="zeit">Die Uhr steht schon auf dem neuen Jahr (05:00) – zuerst wird noch die letzte Stunde (21–22 Uhr) abgerechnet.</param>
    internal void Jahresabrechnung(int neuesJahr, Spielzeit zeit)
    {
        StundeVorbei(zeit);
        foreach (var betrieb in _betriebe.Where(b => b.Konto.IstAktiv))
            betrieb.Jahresabrechnung(_zufall, m => Meldung?.Invoke(m));
        Rat.ProtokollBeginnen();
        Justiz.Gerichtstag(); // vor dem Amtseinkommen: Wer verurteilt wird, verliert Amt und Einkommen
        Rat.EinkommenZahlen();
        Familien.UnterhaltZahlen();
        Wohnen.UnterhaltZahlen(); // Unterhalt der Wohnhäuser, mit den Haushaltskosten
        Glaube.AbgabenZahlen(); // Glaubenszins der Andersgläubigen – noch ins alte Jahr
        Bank.Jahresende(); // Ausfälle, Zins und Rate der Kredite – noch in die alte Jahresrechnung
        Schiffahrt.Jahresende(); // Unterhalt der Schiffe – noch ins alte Jahr
        foreach (var konto in _konten)
            konto.JahrAbschliessen(neuesJahr);
        foreach (var betrieb in _betriebe)
            betrieb.JahrAbschliessen(neuesJahr);
        SchuldenPruefen();
        Wegebau.Jahresende(); // Verkehr des Jahres: Austritt, Verfall, Wegekasse – vor der Ratssitzung
        Gesundheit.Jahresende();  // Kranke sterben oder genesen – mit dem alten Jahr
        Familien.Jahreswechsel(); // Tod und Erbfall vor der Ratssitzung: Ein verwaistes Amt wird gleich neu besetzt
        Gesundheit.NeuesJahr();   // Pest und neue Krankheiten
        Gastwirtschaft.Jahresende(); // Stammtisch-Gunst vor der Ratssitzung (Wahlen)
        Glaube.Jahresende();        // Ketzerkirchen (mit dem Kirchenbuch des Jahres), Anteile kehren zurück
        Kirchenwesen.Jahresende();  // die Beichten des Jahres, neues Kirchenbuch
        Rat.Sitzung(_zufall, neuesJahr - 1);
        Kasse.JahrAbschliessen(neuesJahr); // nach der Sitzung: Bau und Fest stehen noch im alten Jahr
        Gauner.Jahresende();
        Diebe.Jahresende(); // unbeantwortete Forderungen: Feuer oder die Geisel wird verkauft
        Wache.Jahresende();
        Duft.Jahresende();
        AktionspunkteVerteilen(); // die neue Runde beginnt
        foreach (var rivale in _rivalen.Where(r => r.Konto.IstAktiv))
            rivale.Jahresende();
        Einwohnerschaft.Jahresende(neuesJahr); // Bürger-KI: altern, sterben, zuziehen, ausschlafen
        ToteBetriebeFreigeben(); // Betriebe der Häuser, die bankrott gingen oder erloschen, stehen zum Verkauf
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
                Melden($"Der Kämmerer mahnt {konto.Inhaber}: {Zahlenformat.Ganz(-konto.Reinvermoegen)} {Daten.Allgemein.Waehrung} Schulden. " +
                       $"Noch {rest} {(rest == 1 ? "Jahresabschluss" : "Jahresabschlüsse")} im Minus, dann droht der Bankrott.");
            }
        }
    }

    /// <summary>Ein Haus scheidet aus (bankrott oder erloschen): Alle Arbeitskräfte gehen, Amt und Bewerbung sind verloren, das Wohnhaus steht leer.</summary>
    internal void Stilllegen(Konto konto)
    {
        Bauen.LeerstandBeginnt(konto);
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
        var frei = Politik.Amtsbefugnisse.IstAntrag(befehl) && Befugnisse.OhneAktionspunkte(absender, befehl);
        var ergebnis = Bearbeiten(absender, befehl);
        if (ergebnis.Erfolg && kosten > 0)
            absender.Aktionspunkte.Ausgeben(kosten);
        if (ergebnis.Erfolg && frei)
            Befugnisse.AntragGestellt(absender, befehl);
        return ergebnis;
    }

    private Befehlsergebnis Bearbeiten(Konto absender, Befehl befehl)
    {
        if (absender.IstBankrott)
            return Befehlsergebnis.Abgelehnt($"{absender.Inhaber} ist bankrott und kann nichts mehr unternehmen.");
        if (absender.IstErloschen)
            return Befehlsergebnis.Abgelehnt($"Das Haus {absender.Inhaber} ist erloschen.");
        if (Justiz.IstAktiv && Justiz.ImKerker(absender) && befehl is not (BetriebsBefehl or ErbenBestimmen or ErpressungBeantworten or DiebesforderungBeantworten or AusbildungWaehlen or KreditTilgen
                                                                     or MedicusRufen or SchnapsKaufen or SchnapsGeben or GegengiftGeben or HauswaechterSetzen
                                                                     or SchiffahrtBefehl))
            return Befehlsergebnis.Abgelehnt($"Das Oberhaupt von {absender.Inhaber} sitzt bis Ende {Justiz.KerkerBis(absender)} im Kerker. " +
                                             "Nur die Betriebe laufen weiter.");
        if (befehl is StandErwerben)
            return StandErwerbenAusfuehren(absender);
        if (befehl is WohnhausAusbauen)
            return Wohnen.Ausbauen(absender, Uhr);
        if (befehl is HauswaechterSetzen hauswaechter)
            return Wohnen.HauswaechterSetzen(absender, hauswaechter.Angestellt);
        if (befehl is LiegenschaftErwerben liegenschaft)
            return LiegenschaftErwerbenAusfuehren(absender, liegenschaft.Name);
        if (befehl is BauBefehl bau)
            return Bauen.Bauen(absender, bau, Uhr);
        if (befehl is WegAusbauen ausbau)
            return Wegebau.Ausbauen(absender, ausbau.Weg, ausbau.Von, ausbau.Bis, ausbau.Stufe, Uhr);
        if (befehl is WegPflasterBeantragen pflaster)
            return Wegebau.Beantragen(absender, pflaster.Weg, pflaster.Von, pflaster.Bis);
        if (befehl is LeerstandKaufen or LeerstandAbreissen)
        {
            var hausName = befehl is LeerstandKaufen lk ? lk.Haus : ((LeerstandAbreissen)befehl).Haus;
            if (_konten.FirstOrDefault(k => k.Inhaber == hausName) is not { } leer)
                return Befehlsergebnis.Abgelehnt($"Ein Haus „{hausName}“ gibt es nicht.");
            return befehl is LeerstandKaufen ? Bauen.LeerstandKaufen(absender, leer, Uhr) : Bauen.LeerstandAbreissen(absender, leer, Uhr);
        }
        if (befehl is AblassKaufen or BeichtgeheimnisBrechen or LehrschriftLesen or HetzschriftVerbreiten && !Kirchenwesen.IstAktiv)
            return Befehlsergebnis.Abgelehnt("In dieser Stadt gibt es keine Kirchen.");
        if (befehl is AblassKaufen ablass)
            return _betriebe.FirstOrDefault(b => b.Name == ablass.Kirche) is { } kirche
                ? Kirchenwesen.AblassKaufen(absender, kirche)
                : Befehlsergebnis.Abgelehnt($"Eine Kirche „{ablass.Kirche}“ gibt es nicht.");
        if (befehl is BeichtgeheimnisBrechen beichte)
            return _konten.FirstOrDefault(k => k.Inhaber == beichte.Haus) is { } beichtHaus
                ? Kirchenwesen.BeichtgeheimnisBrechen(absender, beichtHaus)
                : Befehlsergebnis.Abgelehnt($"Ein Haus „{beichte.Haus}“ gibt es nicht.");
        if (befehl is LehrschriftLesen lesen)
            return Kirchenwesen.LehrschriftLesen(absender, lesen.Talent);
        if (befehl is HetzschriftVerbreiten hetze)
            return _konten.FirstOrDefault(k => k.Inhaber == hetze.Haus) is { } hetzZiel
                ? Kirchenwesen.HetzschriftVerbreiten(absender, hetzZiel)
                : Befehlsergebnis.Abgelehnt($"Ein Haus „{hetze.Haus}“ gibt es nicht.");
        if (befehl is BerufErlernen lehre)
            return BerufErlernenAusfuehren(absender, lehre.Beruf);
        if (befehl is MeisterpruefungAblegen pruefung)
            return MeisterpruefungAusfuehren(absender, pruefung.Beruf);
        if (befehl is AmtBewerben or Schenken or StatutBeantragen or WahlStimme or StatutStimme && !Rat.IstAktiv)
            return Befehlsergebnis.Abgelehnt("In dieser Stadt gibt es keinen Stadtrat.");
        if (befehl is AmtBewerben bewerbung)
            return Rat.Bewerben(absender, bewerbung.Amt);
        if (befehl is Schenken geschenk)
            return Rat.Schenken(absender, geschenk.Ratsherr, geschenk.Betrag);
        if (befehl is StatutBeantragen antrag)
            return Rat.Beantragen(absender, antrag.Statut);
        if (befehl is WahlStimme wahlStimme)
            return Rat.WahlStimmeFestlegen(absender, wahlStimme.Amt, wahlStimme.Kandidat);
        if (befehl is StatutStimme statutStimme)
            return Rat.StatutStimmeFestlegen(absender, statutStimme.Ja);
        if (befehl is SteuersatzBeantragen steuer)
            return Ratsantraege.Beantragen(new Politik.Ratsantrag(Politik.Antragsart.Steuersatz, absender, Prozent: steuer.Prozent));
        if (befehl is BauvorhabenBeantragen bauvorhaben)
            return Ratsantraege.Beantragen(new Politik.Ratsantrag(Politik.Antragsart.Bauvorhaben, absender, Bauvorhaben: bauvorhaben.Kennung));
        if (befehl is FestBeantragen)
            return Ratsantraege.Beantragen(new Politik.Ratsantrag(Politik.Antragsart.Fest, absender));
        if (befehl is RatsantragStimme ratsStimme)
            return Ratsantraege.StimmeFestlegen(absender, ratsStimme.Art, ratsStimme.Ja);
        if (befehl is StrafmassSetzen strafmass)
            return Befugnisse.StrafmassSetzen(absender, strafmass.Anklaeger, strafmass.Beklagter, strafmass.Prozent);
        if (befehl is StreifeVerlegen streife)
            return Befugnisse.StreifeVerlegen(absender, streife.Viertel);
        if (befehl is GefangenenFreilassen freilassen)
            return Befugnisse.GefangenenFreilassen(absender, freilassen.Haus);
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
        if (befehl is KreditAufnehmen kredit)
            return _konten.FirstOrDefault(k => k.Inhaber == kredit.Geldleiher) is { } geber
                ? Bank.KreditAufnehmen(absender, geber, kredit.Betrag, kredit.Jahre)
                : Befehlsergebnis.Abgelehnt($"Ein Haus „{kredit.Geldleiher}“ gibt es nicht.");
        if (befehl is KreditTilgen)
            return Bank.IstAktiv ? Bank.KreditTilgen(absender) : Befehlsergebnis.Abgelehnt("In dieser Stadt gibt es keine Geldleiher.");
        if (befehl is MoschusSchenken or MoschusRatsherr or ZwietrachtSaeen or HeiligenduftStiften or Giftanschlag && !Duft.IstAktiv)
            return Befehlsergebnis.Abgelehnt("In dieser Stadt gibt es keine Düfte.");
        if (befehl is MoschusSchenken moschus)
            return Duft.MoschusSchenken(absender, moschus.Kandidat);
        if (befehl is MoschusRatsherr moschusRat)
            return Duft.MoschusRatsherr(absender, moschusRat.Ratsherr);
        if (befehl is HeiligenduftStiften)
            return Duft.HeiligenduftStiften(absender);
        if (befehl is ZwietrachtSaeen zwietracht)
            return _konten.FirstOrDefault(k => k.Inhaber == zwietracht.Haus) is { } zwietrachtZiel
                ? Duft.ZwietrachtSaeen(absender, zwietracht.Ratsherr, zwietrachtZiel)
                : Befehlsergebnis.Abgelehnt($"Ein Haus „{zwietracht.Haus}“ gibt es nicht.");
        if (befehl is Giftanschlag gift)
            return _konten.FirstOrDefault(k => k.Inhaber == gift.Haus) is { } giftZiel
                ? Duft.Giftanschlag(absender, giftZiel, gift.Dolch)
                : Befehlsergebnis.Abgelehnt($"Ein Haus „{gift.Haus}“ gibt es nicht.");
        if (befehl is Konvertieren konvertieren)
            return Glaube.Konvertieren(absender, konvertieren.Ziel);
        if (befehl is Bekehrungspredigt)
            return Glaube.Bekehrungspredigt(absender);
        if (befehl is StadtglaubeBeantragen glaubensantrag)
            return Glaube.Beantragen(absender, glaubensantrag.Glaube);
        if (befehl is StadtglaubeStimme glaubensStimme)
            return Glaube.StimmeFestlegen(absender, glaubensStimme.Ja);
        if (befehl is GegengiftGeben gegengift)
            return Gesundheit.IstAktiv ? Gesundheit.GegengiftGeben(absender, gegengift.Person) : Befehlsergebnis.Abgelehnt("In dieser Stadt wird niemand krank.");
        if (befehl is Razzia razzia)
            return _konten.FirstOrDefault(k => k.Inhaber == razzia.Haus) is { } durchsucht
                ? Wache.Razzia(absender, durchsucht)
                : Befehlsergebnis.Abgelehnt($"Ein Haus „{razzia.Haus}“ gibt es nicht.");
        if (befehl is Einbrechen or Auskundschaften or SchutzgeldFordern or Entfuehren or DiebesforderungBeantworten)
        {
            if (!Diebe.IstAktiv)
                return Befehlsergebnis.Abgelehnt("In dieser Stadt gibt es keine Diebe.");
            var hausName = befehl switch
            {
                Einbrechen e => e.Haus, Auskundschaften a => a.Haus, SchutzgeldFordern s => s.Haus, Entfuehren e => e.Haus,
                DiebesforderungBeantworten d => d.Dieb, _ => "",
            };
            if (_konten.FirstOrDefault(k => k.Inhaber == hausName) is not { } haus)
                return Befehlsergebnis.Abgelehnt($"Ein Haus „{hausName}“ gibt es nicht.");
            return befehl switch
            {
                Einbrechen e => Diebe.Einbrechen(absender, haus, e.Betrieb),
                Auskundschaften => Diebe.Auskundschaften(absender, haus),
                SchutzgeldFordern s => Diebe.SchutzgeldFordern(absender, haus, s.Betrag),
                Entfuehren e => Diebe.Entfuehren(absender, haus, e.Person, e.Betrag),
                DiebesforderungBeantworten d => Diebe.Beantworten(absender, haus, d.Art, d.Zahlen),
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
        if (befehl is SchiffahrtBefehl schiffahrt)
            return Schiffahrt.Ausfuehren(absender, schiffahrt);
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
                ArbeiterZuteilen z => Werkstaetten.Zuteilen(betrieb, z),
                ArbeiterEinstellen e => Werkstaetten.Einstellen(betrieb, e),
                ArbeiterEntlassen e => Werkstattwesen.Entlassen(betrieb, e),
                ArbeiterBefoerdern f => Werkstaetten.Befoerdern(betrieb, f),
                LohnstufeSetzen l => Werkstaetten.LohnstufeSetzen(betrieb, l),
                PraemieZahlen p => Werkstaetten.PraemieZahlen(betrieb, p),
                EinrichtungKaufen e => Werkstaetten.EinrichtungKaufen(betrieb, e),
                FoerderungAufteilen f => FoerderungAusfuehren(betrieb, f),
                PersonalSetzen p => PersonalAusfuehren(betrieb, p),
                KarrenKaufen k => KarrenKaufenAusfuehren(betrieb, k),
                EskorteWaehlen e => EskorteAusfuehren(betrieb, e),
                NachtfahrtErlauben n => NachtfahrtAusfuehren(betrieb, n),
                WaechterSetzen s => WaechterAusfuehren(betrieb, s),
                BetriebAusbauen => AusbauenAusfuehren(betrieb),
                BetriebVerkaufen => VerkaufenAusfuehren(betrieb),
                BardeEinladen => Gastwirtschaft.BardeEinladen(betrieb),
                WuerfeltischAufstellen t => Gastwirtschaft.WuerfeltischAufstellen(betrieb, t.Aufstellen),
                KasseEinlegen e => Bank.KasseEinlegen(betrieb, e.Betrag),
                KasseAbheben a => Bank.KasseAbheben(betrieb, a.Betrag),
                ZinsSetzen z => Bank.ZinsSetzen(betrieb, z.Satz),
                Handelsauftrag h => HandelsauftragAusfuehren(betrieb, h),
                WareZurueckhalten z => ZurueckhaltenAusfuehren(betrieb, z),
                HandelsauftragVerwerfen v => VormerkungVerwerfen(betrieb, v.Karren),
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
        if (konto.Reinvermoegen < neu.Schwelle)
            return Befehlsergebnis.Abgelehnt(
                $"Für den Stand {neu.Name} braucht es {Zahlenformat.Ganz(neu.Schwelle)} {waehrung} Vermögen – " +
                $"es fehlen noch {Zahlenformat.Ganz(neu.Schwelle - konto.Reinvermoegen)} {waehrung}.");
        if (Wohnen.StandVerboten(konto, konto.Stand + 1) is { } ohneHaus)
            return Befehlsergebnis.Abgelehnt(ohneHaus);

        konto.Standesgebuehr(neu.Gebuehr);
        Kasse.Buchen(Politik.Kassenposten.Gebuehren, neu.Gebuehr); // die Gebühr geht ans Rathaus
        konto.Stand++;
        Melden($"{konto.Inhaber} ist nun {neu.Name}! (Gebühr {Zahlenformat.Ganz(neu.Gebuehr)} {waehrung})");
        Aufgestiegen?.Invoke(konto, neu);
        return Befehlsergebnis.Ok($"{konto.Inhaber} steigt in den Stand {neu.Name} auf.");
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
        if (betrieb.Produkt == befehl.Ware && betrieb.Arbeiter.All(a => a.Ware is null))
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

    private static Befehlsergebnis VormerkungVerwerfen(Betrieb betrieb, int nr)
    {
        if (nr < 0 || nr >= betrieb.Karren.Count) return Befehlsergebnis.Abgelehnt($"{betrieb.Name} hat keinen Karren Nr. {nr + 1}.");
        var karren = betrieb.Karren[nr];
        if (karren.Vormerkung is null) return Befehlsergebnis.Abgelehnt($"Für {karren.Name} ist nichts vorgemerkt.");
        karren.Vormerkung = null;
        return Befehlsergebnis.Ok($"Der vorgemerkte Auftrag für {karren.Name} ist verworfen.");
    }

    private static Befehlsergebnis ZurueckhaltenAusfuehren(Betrieb betrieb, WareZurueckhalten befehl)
    {
        betrieb.Zurueckhalten(befehl.Ware, befehl.Zurueckhalten);
        return Befehlsergebnis.Ok(befehl.Zurueckhalten
            ? $"{befehl.Ware} bleibt im Lager von {betrieb.Name} – der Karren verkauft es nicht mehr von selbst."
            : $"{befehl.Ware} darf wieder zum Markt.");
    }

    private Befehlsergebnis EskorteAusfuehren(Betrieb betrieb, EskorteWaehlen befehl)
    {
        if (befehl.Karren < 0 || befehl.Karren >= betrieb.Karren.Count)
            return Befehlsergebnis.Abgelehnt($"{betrieb.Name} hat keinen Karren Nr. {befehl.Karren + 1}.");
        if (!Daten.Logistik.Eskorten.ContainsKey(befehl.Eskorte))
            return Befehlsergebnis.Abgelehnt($"Die Eskorte „{befehl.Eskorte}“ gibt es nicht.");
        if (!Wache.EskorteVerfuegbar(befehl.Eskorte))
            return Befehlsergebnis.Abgelehnt($"Die {befehl.Eskorte} gibt Geleitschutz erst, wenn ein Haus ein Wachhaus mit Leuten führt.");
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
        Kirche = Kirchenwesen.IstAktiv ? Kirchenwesen.Sichern(k => _konten.IndexOf(k)) : null,
        Diebe = Diebe.IstAktiv ? Diebe.Sichern(k => _konten.IndexOf(k)) : null,
        Wache = Wache.IstAktiv ? Wache.Sichern(k => _konten.IndexOf(k)) : null,
        Bank = Bank.IstAktiv ? Bank.Sichern(k => _konten.IndexOf(k)) : null,
        Parfum = Duft.IstAktiv ? Duft.Sichern(k => _konten.IndexOf(k)) : null,
        Glaube = Glaube.IstAktiv ? Glaube.Sichern(k => _konten.IndexOf(k)) : null,
        Einwohner = Einwohnerschaft.IstAktiv ? Einwohnerschaft.Sichern() : null,
        Abgegeben = AbgegebeneSichern(),
        Bauen = Bauen.Sichern(k => _konten.IndexOf(k)),
        WerkstattWuerfe = Werkstaetten.Wuerfe,
        Wegebau = Wegebau.Sichern(k => _konten.IndexOf(k)),
        Schiffahrt = Schiffahrt.Sichern(k => _konten.IndexOf(k)),
        Stadtkasse = Kasse.Sichern(),
        Ratsantraege = Kasse.IstAktiv ? Ratsantraege.Sichern(k => _konten.IndexOf(k)) : null,
        Befugnisse = Kasse.IstAktiv ? Befugnisse.Sichern(k => _konten.IndexOf(k)) : null,
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
        foreach (var k in _konten) k.Stadtkasse = Kasse.Buchen;
        Wohnen.AlteStaendeErgaenzen(_konten.Where((_, i) => s.Konten[i].WohnhausStufe is null));
        Bauen.Wiederherstellen(s.Bauen, _konten); // vor den Hauswerten: Ein laufender Umzug zählt schon
        Wohnen.HauswerteNachDemLaden(_konten);
        foreach (var bs in s.Betriebe)
        {
            var betrieb = new Betrieb(bs.Name, bs.Art, _konten[bs.Konto], Daten, jetzt: () => Uhr.VergangeneMinuten);
            betrieb.Wiederherstellen(bs);
            _betriebe.Add(betrieb);
        }
        AbgegebeneWiederherstellen(s.Abgegeben);
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
        if (s.Kirche is { } kirchenStand && Kirchenwesen.IstAktiv)
            Kirchenwesen.Wiederherstellen(kirchenStand, _konten);
        if (s.Diebe is { } diebeStand && Diebe.IstAktiv)
            Diebe.Wiederherstellen(diebeStand, _konten);
        if (s.Wache is { } wacheStand && Wache.IstAktiv)
            Wache.Wiederherstellen(wacheStand, _konten);
        if (s.Bank is { } bankStand && Bank.IstAktiv)
            Bank.Wiederherstellen(bankStand, _konten);
        if (s.Parfum is { } parfumStand && Duft.IstAktiv)
            Duft.Wiederherstellen(parfumStand, _konten);
        if (s.Glaube is { } glaubeStand && Glaube.IstAktiv)
            Glaube.Wiederherstellen(glaubeStand, _konten);
        if (s.Einwohner is { } einwohnerStand && Einwohnerschaft.IstAktiv)
            Einwohnerschaft.Wiederherstellen(einwohnerStand);
        Werkstaetten.Wiederherstellen(s.WerkstattWuerfe);
        Wegebau.Wiederherstellen(s.Wegebau, _konten);
        Schiffahrt.Wiederherstellen(s.Schiffahrt, _konten);
        Kasse.Wiederherstellen(s.Stadtkasse); // alte Spielstände: Startbestand der Karte, Steuersatz aus den Daten
        Ratsantraege.Wiederherstellen(s.Ratsantraege, _konten);
        Befugnisse.Wiederherstellen(s.Befugnisse, _konten);
        ToteBetriebeFreigeben(); // alte Spielstände: Betriebe toter Häuser standen still – nun stehen sie zum Verkauf
        EinwohnerZaehlen();
    }

    public double HoechstesVermoegen => _konten.Count == 0 ? 0 : _konten.Max(k => k.Reinvermoegen);
}
