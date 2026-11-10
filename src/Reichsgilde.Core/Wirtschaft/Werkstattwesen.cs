using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>Ein Arbeiter, wie ihn die Oberfläche zeigt.</summary>
/// <param name="Nummer">Feste Nummer im Betrieb – für die Befehle.</param>
/// <param name="Ware">Woran er arbeitet (seine zugeteilte Ware oder die des Betriebs).</param>
/// <param name="Zugeteilt">Hat er eine eigene Ware (sonst folgt er der Ware des Betriebs)?</param>
/// <param name="Laune">0–100.</param>
/// <param name="TempoFaktor">Wirkung der Laune auf sein Tempo (1 = neutral).</param>
/// <param name="FaelltAusBis">Nach einem Unfall: fehlt bis zu dieser Spielminute (null = er ist da).</param>
/// <param name="Dienstjahre">Wie lange er schon hier arbeitet, in Spieljahren.</param>
/// <param name="BefoerderungVerboten">Warum er (noch) nicht befördert werden kann – null: Er kann.</param>
/// <param name="Name">Vor- und Familienname (PR #224; leer in Städten ohne Namen).</param>
/// <param name="Aussehen">Fürs Porträt, mit <paramref name="Volk"/>, <paramref name="Geschlecht"/> und <paramref name="Menschenjahre"/> (Altersstufe).</param>
/// <param name="Stufe">1–3 in seiner Laufbahn (0: ohne Stufen – Unfreie, Orks, oder ohne Abschnitt „personal“).</param>
/// <param name="Erfahrung">Gesammelte Erfahrungsstunden.</param>
/// <param name="ErfahrungNaechste">So viele braucht er für die nächste Stufe (null: keine nächste).</param>
/// <param name="JahreBisNaechste">So viele Spieljahre noch bei der jetzigen Arbeit (0: schon genug; null: keine nächste oder er sammelt keine).</param>
/// <param name="Lohn">Lohn im Jahr bei der jetzigen Lohnstufe.</param>
/// <param name="NaechsteStufe">Wozu er befördert würde (z. B. „Geselle“; null: höher geht es nicht).</param>
/// <param name="BriefKosten">Was der Brief zur nächsten Stufe kostet (null: keine nächste).</param>
/// <param name="Eigenschaft">Als Vorarbeiter: seine Eigenschaft (null: keine).</param>
/// <param name="EigenschaftWahl">Nach der Beförderung zum Vorarbeiter: die drei zur Wahl (Befehl <see cref="EigenschaftWaehlen"/>; null: nichts zu wählen).</param>
public sealed record ArbeiterZeile(
    int Nummer, string Arbeitskraft, string Ware, bool Zugeteilt, double Laune, double TempoFaktor,
    long? FaelltAusBis, double Dienstjahre, string? BefoerderungVerboten,
    string Name = "", Volk Volk = Volk.Mensch, Dynastie.Geschlecht Geschlecht = Dynastie.Geschlecht.Mann, Dynastie.Aussehen Aussehen = default,
    double Menschenjahre = 0, int Stufe = 0, double Erfahrung = 0, double? ErfahrungNaechste = null, double? JahreBisNaechste = null,
    double Lohn = 0, string? NaechsteStufe = null, double? BriefKosten = null, EigenschaftZeile? Eigenschaft = null,
    IReadOnlyList<EigenschaftZeile>? EigenschaftWahl = null);

/// <summary>Ein Stück Einrichtung, wie es die Oberfläche zeigt (gekauft oder zu kaufen).</summary>
/// <param name="Verboten">Warum man es gerade nicht kaufen kann (null: Es geht – oder es ist schon gekauft).</param>
public sealed record EinrichtungZeile(
    string Kennung, string Name, double Preis, int AbStufe, string? Voraussetzung, string Wirkung, bool Gekauft, string? Verboten);

/// <summary>Zustand eines Stücks im Talentbaum der Einrichtung (für den Rahmen).</summary>
public enum Baumzustand
{
    /// <summary>Gekauft (bzw. im Wohnhaus vorhanden).</summary>
    Gekauft,
    /// <summary>Jetzt zu kaufen.</summary>
    Kaufbar,
    /// <summary>Die Stufe und die Voraussetzung reichen, aber gerade nicht: zu wenig Gulden, Umbau.</summary>
    NichtJetzt,
    /// <summary>Gesperrt: Die Gebäudestufe ist zu niedrig oder die Voraussetzung fehlt.</summary>
    Gesperrt,
}

/// <summary>Ein Stück im Talentbaum (PR #224) – Spalte, Reihe (= Gebäudestufe), Zustand, Voraussetzung (Linie) und Amortisation.</summary>
/// <param name="Voraussetzung">Kennung des Stücks, das vorher da sein muss (null: keine).</param>
/// <param name="Verboten">Warum man es gerade nicht kaufen kann (null: Es geht oder es ist gekauft).</param>
/// <param name="MehrgewinnJeJahr">Was es zu heutigen Preisen im Jahr mehr bringt (0: ohne Gulden-Wirkung oder gekauft).</param>
/// <param name="Amortisationsjahre">Preis ÷ Mehrgewinn (null: ohne Mehrgewinn).</param>
/// <param name="Jaehrlich">Der Preis ist ein Lohn im Jahr (Hauswächter), kein Kauf.</param>
/// <param name="Vorhanden">Im Wohnhaus: „Hausapotheke“ oder „Hauswaechter“ – eine Sache des Spiels mit eigenem Befehl (null: ein Stück zum Kaufen).</param>
public sealed record BaumStueck(string Kennung, string Name, string Spalte, int Reihe, double Preis, string? Voraussetzung, string Wirkung,
    Baumzustand Zustand, string? Verboten, double MehrgewinnJeJahr, double? Amortisationsjahre, bool Jaehrlich = false, string? Vorhanden = null);

/// <summary>Der Talentbaum der Einrichtung eines Betriebs oder des Wohnhauses: Spalten × Reihen (Gebäudestufen) und die Stücke darin.</summary>
/// <param name="Stufe">Die Stufe des Gebäudes jetzt (Reihen darüber sind gesperrt).</param>
public sealed record Einrichtungsbaum(IReadOnlyList<string> Spalten, int Reihen, int Stufe, IReadOnlyList<BaumStueck> Stuecke);

/// <summary>Eine Ware in Arbeit: wie viele daran arbeiten, wie weit das angefangene Stück ist.</summary>
/// <param name="Fortschritt">Anteil am angefangenen Stück (0 bis 1).</param>
/// <param name="MinutenJeStueck">Arbeitsminuten eines Gesellen je Stück (mit Einrichtung).</param>
/// <param name="Stillstand">Warum die Arbeit an dieser Ware gerade ruht (null: sie läuft).</param>
public sealed record WareInArbeit(string Ware, int Arbeiter, double Fortschritt, double MinutenJeStueck, string? Stillstand);

/// <summary>
/// Die Werkstatt wie in Gilde 1 (Entscheidungen zu PR #152): einzelne Arbeiter mit eigener Ware, Beförderung,
/// Lohnstufen und Prämie, Laune mit Wirkung auf das Tempo, Kündigung, Unfälle und kaufbare Einrichtung.
///
/// Ablauf je Arbeitsstunde (Werkstätten mit Ware): Unfälle würfeln (eigener Zufall mit festem Startwert),
/// dann läuft die Laune jedes Arbeiters um ein paar Punkte auf ihr Ziel zu; wer zu lange missmutig ist, kündigt.
/// Bei üblichem Lohn ohne Prämie und Unfall bleibt die Laune neutral – dann arbeitet jeder wie früher.
/// </summary>
public sealed class Werkstattwesen
{
    private readonly Stadtwirtschaft _w;
    private Zufallsquelle _zufall;

    /// <summary>Die Daten (Abschnitt „werkstatt"); null: ohne Laune, Unfälle und Einrichtung.</summary>
    public WerkstattDaten? Daten { get; }

    public bool IstAktiv => Daten is not null;

    internal Werkstattwesen(Stadtwirtschaft w, WerkstattDaten? daten, int zufallsstartwert)
    {
        _w = w;
        Daten = daten;
        _zufall = new Zufallsquelle(unchecked(zufallsstartwert * 73 + 59));
    }

    private string Waehrung => _w.Daten.Allgemein.Waehrung;
    private long Jetzt => _w.Uhr.VergangeneMinuten;

    // ── Jede Arbeitsstunde ──────────────────────────────────────────────────

    /// <summary>Eine volle Stunde ist vorbei: War es eine Arbeitsstunde, gibt es Unfälle, Laune und Kündigungen.</summary>
    internal void Stunde(Spielzeit zeit)
    {
        if (Daten is not { } d) return;
        var beginn = zeit.StundeDieEndete * 60;
        var jetzt = zeit.VergangeneMinuten;
        foreach (var b in _w.Betriebe.Where(b => b.HatWerkstattregeln && b.Konto.IstAktiv && b.Belegt > 0).ToList())
        {
            if (!b.IstArbeitszeit(beginn)) continue; // jeder Betrieb in seiner Arbeitszeit (Wirtshaus bis 22 Uhr)
            if (b.UmbauMinuten == 0) Unfaelle(b, d, jetzt, b.Arbeitsminuten / 60.0);
            Laune(b, d, jetzt);
        }
    }

    /// <summary>
    /// Gefahr je Arbeiter und Jahr in diesem Betrieb (nach Beruf, mit Schürzen). Die Daten gelten für die übliche Arbeitszeit,
    /// je Stunde gleich: Wer länger arbeitet (Wirtshaus bis 22 Uhr), verletzt sich im Jahr öfter (× <see cref="Wirtschaftsdaten.Schichtfaktor"/>).
    /// </summary>
    public double UnfallGefahr(Betrieb b) =>
        Daten is { } d && b.HatWerkstattregeln ? d.Unfall.Gefahr.GetValueOrDefault(b.Artdaten.Beruf) * b.UnfallFaktor * _w.Daten.Schichtfaktor(b.Art) : 0;

    private void Unfaelle(Betrieb b, WerkstattDaten d, long jetzt, double stundenImJahr)
    {
        var gefahr = UnfallGefahr(b) / stundenImJahr;
        if (gefahr <= 0) return;
        var u = d.Unfall;
        foreach (var a in b.Arbeiter.ToList())
        {
            if (a.FaelltAus(jetzt) || _zufall.NextDouble() >= gefahr) continue;
            a.AusfallBis = (jetzt / Spielzeit.MinutenProTag + 1) * Spielzeit.MinutenProTag; // bis zum Jahreswechsel
            b.UnfallLauneBis = jetzt + u.LauneMinuten;
            _w.Melden($"Unfall in {b.Name}: {a.Arbeitskraft} Nr. {a.Nummer} hat sich verletzt und fällt für den Rest des Tages aus. " +
                      "Der Lohn läuft weiter.");
        }
    }

    /// <summary>Wohin die Laune dieses Arbeiters zieht: neutral, Lohnstufe, Prämie, Unfall im Betrieb (0–100).</summary>
    public double LauneZiel(Betrieb b, Arbeiter a)
    {
        if (Daten is not { } d || !b.HatWerkstattregeln) return 50;
        var ziel = d.Laune.Neutral + d.Stufe(b.Lohnstufe).Laune + b.LauneBonus; // Vorarbeiter: Gutes Gemüt, Antreiber
        if (a.PraemieBis is { } p && p > Jetzt) ziel += d.Praemie.Laune;
        if (b.UnfallLauneBis is { } u && u > Jetzt) ziel += d.Unfall.Laune;
        return Math.Clamp(ziel, 0, 100);
    }

    private void Laune(Betrieb b, WerkstattDaten d, long jetzt)
    {
        var l = d.Laune;
        foreach (var a in b.Arbeiter.ToList())
        {
            var ziel = LauneZiel(b, a);
            a.Laune = a.Laune < ziel ? Math.Min(ziel, a.Laune + l.JeStunde) : Math.Max(ziel, a.Laune - l.JeStunde);
            a.UnzufriedenStunden = a.Laune < l.KuendigungUnter ? a.UnzufriedenStunden + 1 : 0;
            if (a.UnzufriedenStunden < l.KuendigungNachStunden) continue;
            b.Entlassen(a);
            _w.Melden($"{b.Name}: {a.Arbeitskraft} Nr. {a.Nummer} kündigt – die Laune war zu lange schlecht.");
        }
    }

    // ── Abfragen für die Oberfläche ─────────────────────────────────────────

    /// <summary>
    /// Die Arbeiter des Betriebs mit Ware, Laune, Tempo, Ausfall und ob man sie befördern kann – seit PR #224 mit Name, Porträt, Stufe,
    /// Erfahrung bis zur nächsten Stufe, Lohn, Briefkosten und Eigenschaft (<see cref="Personalwesen"/>).
    /// </summary>
    public IReadOnlyList<ArbeiterZeile> Arbeiterliste(Betrieb b)
    {
        var p = _w.Personal;
        return b.Arbeiter
            .Select(a => new ArbeiterZeile(a.Nummer, a.Arbeitskraft, b.WareVon(a), a.Ware is not null, a.Laune, b.TempoFaktor(a),
                a.FaelltAus(Jetzt) ? a.AusfallBis : null, (Jetzt - a.EingestelltUm) / (double)Spielzeit.MinutenProTag, p.BefoerdernVerboten(b, a.Nummer),
                a.Name, a.Volk, a.Geschlecht, a.Aussehen, a.Menschenjahre(Jetzt), p.Stufe(a.Arbeitskraft), a.Erfahrung,
                p.ErfahrungFuerNaechste(b, a), p.JahreBisNaechste(b, a), b.LohnJeArbeiter(a.Arbeitskraft), p.NaechsteStufe(b, a), p.BriefKosten(b, a),
                a.Eigenschaft is { } k ? p.Eigenschaft(b, k) : null,
                a.EigenschaftWahl?.Select(k => p.Eigenschaft(b, k)).OfType<EigenschaftZeile>().ToList()))
            .ToList();
    }

    /// <summary>Die Waren in Arbeit mit Arbeitern, Fortschritt des angefangenen Stücks und ob sie gerade stillstehen.</summary>
    public IReadOnlyList<WareInArbeit> WarenInArbeit(Betrieb b)
    {
        if (!b.IstWerkstatt) return Array.Empty<WareInArbeit>();
        return b.WarenInArbeit.Select(ware =>
        {
            var minuten = b.MinutenJeStueck(ware);
            var fortschritt = Math.Clamp(b.FortschrittVon(ware) / minuten, 0, 1);
            var stillstand = fortschritt >= 1 ? StillstandVon(b, ware) : null;
            return new WareInArbeit(ware, b.Arbeiter.Count(a => b.WareVon(a) == ware), fortschritt, minuten, stillstand);
        }).ToList();
    }

    /// <summary>Ein Stück ist fertig, aber es geht nicht weiter: Lager voll oder Zutat fehlt.</summary>
    private string? StillstandVon(Betrieb b, string ware)
    {
        if (b.ImLager(ware) >= b.LagerKapazitaetJeWare) return $"Das Lager für {ware} ist voll.";
        foreach (var (zutat, menge) in _w.Daten.Waren[ware].Zutaten)
            if (b.ImLager(zutat) < menge) return $"Es fehlt {zutat}.";
        return null;
    }

    /// <summary>
    /// Kann dieser Betrieb Einrichtung haben? Werkstätten mit Ware, die Betriebe des Diebes (Schmugglerloch, Unterschlupf,
    /// Diebesgilde), die Wache in der Stadt (Wachhaus, Stadtwache, Garnison) und der Geldleiher (Wechselstube, Geldleihe,
    /// Bankhaus) – nicht das Umland (Klosterhof, Bauernhof, Wachturm …) oder der Schreiber.
    /// </summary>
    public bool KannEinrichtung(Betrieb b) =>
        b.IstWerkstatt || _w.Diebe.IstDiebesbetrieb(b) || (_w.Wache.IstWachbetrieb(b) && b.InDerStadt) || _w.Bank.IstBankbetrieb(b);

    /// <summary>Alle Stücke, die eine Werkstatt dieses Berufs haben kann – gekauft oder mit Preis und Verbotsgrund.</summary>
    public IReadOnlyList<EinrichtungZeile> Einrichtungsliste(Betrieb b)
    {
        if (Daten is not { } d || !KannEinrichtung(b)) return Array.Empty<EinrichtungZeile>();
        return d.EinrichtungFuer(b.Artdaten.Beruf)
            .Select(e => new EinrichtungZeile(e.Kennung, e.Name, e.Preis, e.AbStufe, e.Voraussetzung, e.Wirkung.Text(),
                b.HatEinrichtung(e.Kennung), b.HatEinrichtung(e.Kennung) ? null : EinrichtungVerboten(b, e.Kennung)))
            .ToList();
    }

    /// <summary>
    /// Der Talentbaum der Einrichtung dieses Betriebs (PR #224): Spalten aus <see cref="WerkstattDaten.Spalten"/>, drei Reihen (Gebäudestufen),
    /// jedes Stück mit Zustand, Voraussetzung, Mehrgewinn und Amortisation. Leer, wenn der Betrieb keine Einrichtung haben kann.
    /// </summary>
    public Einrichtungsbaum Baum(Betrieb b)
    {
        if (Daten is not { } d || !KannEinrichtung(b)) return new Einrichtungsbaum(Array.Empty<string>(), 3, b.Stufe, Array.Empty<BaumStueck>());
        var stuecke = d.EinrichtungFuer(b.Artdaten.Beruf);
        var spalten = d.Spalten.Count > 0 ? d.Spalten : stuecke.Select(e => e.Spalte ?? "").Distinct().ToList();
        return new Einrichtungsbaum(spalten, 3, b.Stufe, stuecke.Select(e =>
        {
            var gekauft = b.HatEinrichtung(e.Kennung);
            var verboten = gekauft ? null : EinrichtungVerboten(b, e.Kennung);
            var gesperrt = b.Stufe < e.AbStufe || (e.Voraussetzung is { } v && !b.HatEinrichtung(v));
            var mehr = ErwarteterMehrgewinnJeJahr(b, e.Kennung);
            return new BaumStueck(e.Kennung, e.Name, e.Spalte ?? spalten.FirstOrDefault() ?? "", e.AbStufe, e.Preis, e.Voraussetzung, e.Wirkung.Text(),
                gekauft ? Baumzustand.Gekauft : verboten is null ? Baumzustand.Kaufbar : gesperrt ? Baumzustand.Gesperrt : Baumzustand.NichtJetzt,
                verboten, mehr, mehr > 0 ? e.Preis / mehr : null);
        }).ToList());
    }

    /// <summary>Lohnfaktor und Laune-Ziel einer Lohnstufe (für die Auswahl in der Oberfläche).</summary>
    public LohnstufeDaten Lohnstufe(Lohnstufe stufe) => Daten?.Stufe(stufe) ?? LohnstufeDaten.Neutral;

    /// <summary>Was eine Prämie kostet – für einen Arbeiter (Nummer) oder alle (null).</summary>
    public double PraemieKosten(Betrieb b, int? arbeiter = null) =>
        (Daten?.Praemie.KostenJeArbeiter ?? 0) * (arbeiter is null ? b.Belegt : 1);

    // ── Verbote (null = es geht) ────────────────────────────────────────────

    public string? ZuteilenVerboten(Betrieb b, int nummer, string? ware)
    {
        if (!b.IstWerkstatt) return $"{b.Name} stellt nichts her – da gibt es nichts zuzuteilen.";
        if (b.ArbeiterNr(nummer) is null) return $"In {b.Name} gibt es keinen Arbeiter Nr. {nummer}.";
        if (ware is not null && !b.Artdaten.Produkte.Contains(ware)) return $"{ware} kann eine {b.Artdaten.Name} nicht herstellen.";
        return null;
    }

    /// <summary>Die alte Beförderung (ohne Abschnitt „personal“) – mit „personal“ gilt <see cref="Personalwesen.BefoerdernVerboten"/>.</summary>
    public string? BefoerdernVerboten(Betrieb b, int nummer)
    {
        if (_w.Personal.IstAktiv) return _w.Personal.BefoerdernVerboten(b, nummer);
        if (Daten is not { } d) return "In dieser Stadt wird nicht befördert.";
        if (b.ArbeiterNr(nummer) is not { } a) return $"In {b.Name} gibt es keinen Arbeiter Nr. {nummer}.";
        var bf = d.Befoerderung;
        if (a.Arbeitskraft != bf.Von) return $"Befördert wird nur ein {bf.Von} – er wird {bf.Zu}.";
        if (!_w.Daten.Arbeitskraefte.TryGetValue(bf.Zu, out var zu) || !zu.IstErlaubt(_w.Statut, b.Ort))
            return $"Ein {bf.Zu} darf hier nicht arbeiten.";
        var rest = bf.DienstMinuten - (Jetzt - a.EingestelltUm);
        if (rest > 0)
            return $"Ein {bf.Von} kann nach {Jahre(bf.DienstMinuten)} Dienst {bf.Zu} werden – noch {Jahre(rest)}.";
        return null;
    }

    /// <summary>Spielminuten als Jahre: „2 Jahre", „1,5 Jahre", „1 Jahr".</summary>
    private static string Jahre(long minuten)
    {
        var jahre = minuten / (double)Spielzeit.MinutenProTag;
        var text = Math.Abs(jahre - Math.Round(jahre)) < 0.05 ? Zahlenformat.Ganz(Math.Round(jahre)) : Zahlenformat.Kommazahl(jahre);
        return $"{text} {(text == "1" ? "Jahr" : "Jahre")}";
    }

    public string? LohnstufeVerboten(Betrieb b) =>
        !IstAktiv ? "In dieser Stadt gibt es keine Lohnstufen."
        : !b.HatWerkstattregeln ? $"Lohnstufen gibt es nur in Werkstätten, die Waren herstellen." : null;

    public string? PraemieVerboten(Betrieb b, int? nummer = null)
    {
        if (LohnstufeVerboten(b) is { } grund) return grund.Replace("Lohnstufen", "Prämien");
        if (nummer is { } n && b.ArbeiterNr(n) is null) return $"In {b.Name} gibt es keinen Arbeiter Nr. {n}.";
        if (b.Belegt == 0) return $"In {b.Name} arbeitet niemand.";
        var kosten = PraemieKosten(b, nummer);
        if (b.Konto.Vermoegen < kosten) return $"Die Prämie kostet {Zahlenformat.Ganz(kosten)} {Waehrung}.";
        return null;
    }

    public string? EinrichtungVerboten(Betrieb b, string kennung)
    {
        if (Daten is not { } d) return "In dieser Stadt gibt es keine Einrichtung zu kaufen.";
        if (!KannEinrichtung(b) || d.Stueck(b.Artdaten.Beruf, kennung) is not { } e)
            return $"Für {b.Name} gibt es kein Stück „{kennung}“.";
        if (b.HatEinrichtung(kennung)) return $"{b.Name} hat schon: {e.Name}.";
        if (b.Stufe < e.AbStufe) return $"{e.Name} gibt es erst ab Stufe {e.AbStufe} – {b.Name} ist Stufe {b.Stufe}.";
        if (e.Voraussetzung is { } v && !b.HatEinrichtung(v))
            return $"Erst braucht es: {d.Stueck(b.Artdaten.Beruf, v)?.Name ?? v}.";
        if (b.UmbauMinuten > 0) return $"{b.Name} wird gerade umgebaut.";
        if (b.Konto.Vermoegen < _w.Baupreis(b.Konto, e.Preis)) return $"{e.Name} kostet {Zahlenformat.Ganz(_w.Baupreis(b.Konto, e.Preis))} {Waehrung}.";
        return null;
    }

    // ── Befehle ─────────────────────────────────────────────────────────────

    internal Befehlsergebnis Zuteilen(Betrieb b, ArbeiterZuteilen befehl)
    {
        if (ZuteilenVerboten(b, befehl.Arbeiter, befehl.Ware) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var a = b.ArbeiterNr(befehl.Arbeiter)!;
        b.Zuteilen(a, befehl.Ware);
        return Befehlsergebnis.Ok($"{b.Name}: {a.Arbeitskraft} Nr. {a.Nummer} arbeitet nun an {b.WareVon(a)}" +
                                  (befehl.Ware is null ? " (der Ware des Betriebs)." : "."));
    }

    internal Befehlsergebnis Einstellen(Betrieb b, ArbeiterEinstellen befehl)
    {
        if (befehl.Ware is not null && (!b.IstWerkstatt || !b.Artdaten.Produkte.Contains(befehl.Ware)))
            return Befehlsergebnis.Abgelehnt($"{befehl.Ware} kann eine {b.Artdaten.Name} nicht herstellen.");
        if (_w.Personal.EinstellenVerboten(b, befehl.Arbeitskraft) is { } nurAnfaenger) return Befehlsergebnis.Abgelehnt(nurAnfaenger);
        var a = b.EinenEinstellen(befehl.Arbeitskraft, befehl.Ware, _w.Statut); // prüft Statut, Plätze und Namen
        var handgeld = _w.Personal.HandgeldZahlen(b, befehl.Arbeitskraft, 1); // erst nach dem Einstellen: Ein abgelehnter kostet nichts
        return Befehlsergebnis.Ok($"{b.Name}: {Personalwesen.Wer(a)} eingestellt" + (b.IstWerkstatt ? $", arbeitet an {b.WareVon(a)}" : "")
                                  + (handgeld > 0 ? $" (Handgeld {Zahlenformat.Ganz(handgeld)} {Waehrung})." : "."));
    }

    internal static Befehlsergebnis Entlassen(Betrieb b, ArbeiterEntlassen befehl)
    {
        if (b.ArbeiterNr(befehl.Arbeiter) is not { } a) return Befehlsergebnis.Abgelehnt($"In {b.Name} gibt es keinen Arbeiter Nr. {befehl.Arbeiter}.");
        b.Entlassen(a);
        return Befehlsergebnis.Ok($"{b.Name}: {a.Arbeitskraft} Nr. {a.Nummer} entlassen.");
    }

    internal Befehlsergebnis Befoerdern(Betrieb b, ArbeiterBefoerdern befehl)
    {
        if (_w.Personal.IstAktiv) return _w.Personal.Befoerdern(b, befehl);
        if (BefoerdernVerboten(b, befehl.Arbeiter) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var a = b.ArbeiterNr(befehl.Arbeiter)!;
        var zu = Daten!.Befoerderung.Zu;
        b.Befoerdern(a, zu);
        var lohn = _w.Daten.Lohn(zu, b.Art) * b.LohnFaktor;
        _w.Melden($"{b.Name}: {Daten.Befoerderung.Von} Nr. {a.Nummer} ist nun {zu}.");
        return Befehlsergebnis.Ok($"{b.Name}: Nr. {a.Nummer} ist nun {zu} – Lohn ab jetzt {Zahlenformat.Ganz(lohn)} {Waehrung} im Jahr.");
    }

    internal Befehlsergebnis LohnstufeSetzen(Betrieb b, LohnstufeSetzen befehl)
    {
        if (LohnstufeVerboten(b) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        if (!Enum.IsDefined(befehl.Stufe)) return Befehlsergebnis.Abgelehnt("Diese Lohnstufe gibt es nicht.");
        if (b.Lohnstufe == befehl.Stufe) return Befehlsergebnis.Ok($"{b.Name} zahlt schon {befehl.Stufe.Name()}.");
        b.Lohnstufe = befehl.Stufe;
        var faktor = Daten!.Stufe(befehl.Stufe).LohnFaktor;
        return Befehlsergebnis.Ok($"{b.Name} zahlt nun {befehl.Stufe.Name()} ({Zahlenformat.Ganz(faktor * 100)} % des Lohns, " +
                                  $"{Zahlenformat.Ganz(b.Lohnkosten)} {Waehrung} im Jahr).");
    }

    internal Befehlsergebnis PraemieZahlen(Betrieb b, PraemieZahlen befehl)
    {
        if (PraemieVerboten(b, befehl.Arbeiter) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var d = Daten!;
        var kosten = PraemieKosten(b, befehl.Arbeiter);
        var bis = Jetzt + d.Praemie.Minuten;
        foreach (var a in b.Arbeiter.Where(a => befehl.Arbeiter is null || a.Nummer == befehl.Arbeiter))
            a.PraemieBis = bis;
        if (kosten > 0) b.PersonalBuchen(kosten);
        return Befehlsergebnis.Ok($"{b.Name}: Prämie für {(befehl.Arbeiter is { } n ? $"Nr. {n}" : "alle")} " +
                                  $"({Zahlenformat.Ganz(kosten)} {Waehrung}) – die Laune steigt.");
    }

    internal Befehlsergebnis EinrichtungKaufen(Betrieb b, EinrichtungKaufen befehl)
    {
        if (EinrichtungVerboten(b, befehl.Kennung) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var e = Daten!.Stueck(b.Artdaten.Beruf, befehl.Kennung)!;
        var preis = _w.Baupreis(b.Konto, e.Preis); // Baumeister (Lebensweise)
        b.Konto.Investition(preis);
        b.EinrichtungHinzufuegen(e.Kennung);
        return Befehlsergebnis.Ok($"{b.Name}: {e.Name} gekauft ({Zahlenformat.Ganz(preis)} {Waehrung}) – {e.Wirkung.Text()}.");
    }

    /// <summary>Was die Einrichtung des Betriebs gekostet hat – sie zählt zum Wert (Verkauf).</summary>
    public double Einrichtungswert(Betrieb b) => b.Einrichtungsstuecke.Sum(e => e.Preis);

    /// <summary>Was diese Einrichtung (Kennungen) in einem Haus dieses Berufs wert ist.</summary>
    internal double Einrichtungswert(string beruf, IEnumerable<string> kennungen) =>
        Daten is { } d ? kennungen.Sum(k => d.Stueck(beruf, k)?.Preis ?? 0) : 0;

    // ── Was ein Stück einbringt (für die KI und die Anzeige) ────────────────

    /// <summary>
    /// Was dieses Stück dem Betrieb im Jahr mehr einbringt – zu heutigen Preisen, mit den Arbeitern, die jetzt da sind, und ihren Waren.
    /// 0: schon gekauft, kein Stück dieses Betriebs, ohne Wirkung in Gulden (Kassenschrank …; Regale, Dietriche und andere nur ohne den Schalter
    /// <see cref="JedeWirkungRechnen"/>, siehe <see cref="MehrgewinnDurch"/>) oder kein Vorteil
    /// bei der jetzigen Arbeit (der Blasebalg hilft nur, wenn eine begünstigte Ware gemacht wird). Die Rechnung je Wirkung:
    /// <list type="bullet">
    /// <item>kürzere Zeit je Stück: je Arbeiter Leistung (mit Völkerbonus) × Arbeitsminuten × Gewinn je Minute seiner Ware
    /// × (1 ÷ Zeitfaktor mit dem Stück − 1 ÷ Zeitfaktor ohne)</item>
    /// <item>weniger Lohn (Waffenkammer): Lohnkosten × (1 − Lohnfaktor)</item>
    /// <item>seltener Unfälle (Schürzen): Wert der Arbeit × Unfallgefahr × verlorener Teil des Tages ((Stunden − 1) ÷ 2 ÷ Stunden) × (1 − Unfallfaktor)</item>
    /// <item>Eisentür, Löschfass: was Diebe bzw. Feuer diesem Betrieb im Vorjahr genommen haben (<see cref="Betriebsrechnung.Raub"/>,
    /// <see cref="Betriebsrechnung.Brandschaden"/>) × (1 − Faktor) – wer nie bestohlen wurde, hat nichts davon</item>
    /// <item>Geldleiher: mehr Rahmen (nur so weit die Kasse reicht), mehr Wechselgebühr, weniger Ausfall der Kasse (<see cref="Bank.Bankwesen"/>)</item>
    /// </list>
    /// Mehr Stück drücken den Preis – das rechnet die Abfrage nicht mit (wie <see cref="Kalkulation.ErwarteterJahresgewinn"/>).
    /// </summary>
    public double ErwarteterMehrgewinnJeJahr(Betrieb b, string kennung)
    {
        if (Daten is not { } d || !KannEinrichtung(b) || b.HatEinrichtung(kennung) || d.Stueck(b.Artdaten.Beruf, kennung) is not { } e) return 0;
        return MehrgewinnDurch(b, e.Wirkung);
    }

    /// <summary>
    /// Was diese Wirkung (ein Stück Einrichtung oder die Eigenschaft eines Vorarbeiters) dem Betrieb im Jahr mehr einbrächte – Rechnung wie
    /// bei <see cref="ErwarteterMehrgewinnJeJahr"/>, dazu (PR #224): mehr Erlös (Preis, Ladentheke) = Erlös der Jahresmenge × Aufschlag;
    /// weniger Zutaten = Zutaten der Jahresmenge zum Marktpreis × (1 − Faktor); schneller (Antreiber, Laune, Förderzeit im Umland) und länger
    /// (Frühaufsteher) = Wert der Arbeit × Anteil; mehr Erfahrung der Anfänger = je Anfänger (Ertrag der nächsten Stufe − Ertrag jetzt)
    /// × (1 − 1 ÷ Faktor), solange es Anfänger gibt; seltener Ausbrüche der Unfreien = Risiko × Schaden × (1 − Faktor).
    /// </summary>
    public double MehrgewinnDurch(Betrieb b, EinrichtungWirkung wi)
    {
        var mehr = 0.0;
        if (b.IstWerkstatt && wi.ZeitFaktor < 1)
            mehr += Arbeitswert(b, ware => wi.GiltFuer(ware) ? 1 / wi.ZeitFaktor - 1 : 0);
        if (!b.IstWerkstatt && !b.OhneProduktion && wi.ZeitFaktor < 1)
            mehr += Umlandwert(b) * (1 / wi.ZeitFaktor - 1);
        var aufschlag = wi.PreisFaktor * (1 + wi.LadenAnteil * wi.LadenAufschlag) - 1;
        if (aufschlag > 0 && !b.OhneProduktion)
            mehr += Erloeswert(b) * aufschlag;
        if (wi.RohstoffFaktor < 1 && b.IstWerkstatt)
            mehr += Jahresmengen(b).Sum(x => x.Menge * _w.Daten.Waren[x.Ware].Zutaten.Sum(z => _w.Markt[z.Key].Preis * z.Value)) * (1 - wi.RohstoffFaktor);
        var schneller = wi.TempoFaktor - 1 + (b.HatWerkstattregeln ? LauneTempo(b, wi.Laune) : 0);
        if (Math.Abs(schneller) > 1e-9)
            mehr += Gesamtwert(b) * schneller;
        if (wi.ArbeitsminutenPlus > 0 && !b.OhneProduktion)
        {
            var plus = Math.Min(wi.ArbeitsminutenPlus, Spielzeit.TagesendeMinute - Stadtwirtschaft.Arbeitsbeginn - b.Arbeitsminuten);
            if (plus > 0) mehr += Gesamtwert(b) * plus / b.Arbeitsminuten;
        }
        if (wi.ErfahrungFaktor > 1 && _w.Personal.IstAktiv)
            foreach (var a in b.Arbeiter.Where(_w.Personal.IstAnfaenger))
                if (_w.Personal.NaechsteStufe(b, a) is { } zu)
                    mehr += Math.Max(0, Kalkulation.Jahresertrag(_w, b, zu) - Kalkulation.Jahresertrag(_w, b, a.Arbeitskraft)) * (1 - 1 / wi.ErfahrungFaktor);
        if (wi.RisikoFaktor < 1)
            mehr += b.Belegung.Sum(x => x.Value * _w.Daten.Arbeitskraefte[x.Key].Risiko * _w.Daten.Arbeitskraefte[x.Key].Schaden) * (1 - wi.RisikoFaktor);
        if (wi.LohnFaktor < 1)
            mehr += b.Lohnkosten * (1 - wi.LohnFaktor);
        if (wi.UnfallFaktor < 1 && b.IstWerkstatt)
        {
            var stunden = b.Arbeitsminuten / 60.0;
            mehr += Arbeitswert(b, _ => 1) * UnfallGefahr(b) * (stunden - 1) / 2 / stunden * (1 - wi.UnfallFaktor);
        }
        if (wi.EinbruchFaktor < 1)
            mehr += (b.Vorjahr?.Raub ?? 0) * (1 - wi.EinbruchFaktor);
        if (wi.BrandFaktor < 1)
            mehr += (b.Vorjahr?.Brandschaden ?? 0) * (1 - wi.BrandFaktor);
        if (_w.Bank.IstBankbetrieb(b))
            mehr += _w.Bank.MehrgewinnJeJahr(b, wi);
        if (JedeWirkungRechnen)
            mehr += MehrgewinnOhneErtrag(b, wi);
        return Math.Max(0, mehr);
    }

    // ── Wirkungen ohne direkten Ertrag (Datenversion 79, werkstatt.jedeWirkungRechnen) ──

    /// <summary>Rechnen auch Lager, Fänge, Spuren, Einbruch, Hehlerei, Razzia, Streife und Geleit in Gulden (<see cref="WerkstattDaten.JedeWirkungRechnen"/>)?</summary>
    public bool JedeWirkungRechnen => Daten?.JedeWirkungRechnen == true;

    /// <summary>
    /// Was die Wirkungen ohne direkten Ertrag dem Betrieb im Jahr einbringen (Personal-Nacharbeit, Datenversion 79) – je eine Rechnung,
    /// erwartet aus dem Stand von jetzt oder „nach dem Vorjahr“ wie die Eisentür:
    /// <list type="bullet">
    /// <item>mehr Lager (Ordnungsliebend, Regale; Betriebe mit Ware bzw. Förderung und Karren): <see cref="LagerMehrgewinn"/></item>
    /// <item>Dieb, seltener gefasst (Vorsichtig, Verkleidungstruhe): weniger erwartete Fänge × Kosten eines Fangs (<see cref="KostenJeFang"/>)</item>
    /// <item>Dieb, weniger Spuren (Verschwiegen, Lederhandschuhe; nur das Versteck): <see cref="SpurenGefahr"/> × (1 − Faktor) – die Spuren wirken
    /// nur dort, wo jemand nach Beweisen sucht (Razzien und Auskundschafter anderer Häuser, Zeugen eigener Einbrüche), nicht auf den Beweis dessen,
    /// der einen Taschendieb fasst</item>
    /// <item>Dieb, Einbruch (Einbrecher, Dietriche; nur das Versteck): Diebesgut des Vorjahres × (Chance mit ÷ ohne − 1)</item>
    /// <item>Dieb, mehr Erlös am Markt (Hehlerfreund; nur das Versteck): Diebesgut des Vorjahres × Aufschlag</item>
    /// <item>Wache, Razzia (Spürnase, Hellebarden; nur die Wachstube, nur nach Razzien im Vorjahr): Kopfgeld aus Urteilen im Vorjahr
    /// × (Chance mit ÷ ohne − 1) – das Kopfgeld für gefasste Taschendiebe (geschätzt wie bei der Streife) zählt nicht</item>
    /// <item>Wache, Streife (Streifengänger, Wachhunde): <see cref="StreifeMehrgewinn"/></item>
    /// <item>Wache, Geleit (Geleitmann, Kettenhemden; nur die Geleitstube): Raub an den eigenen Karren mit dem Geleit der Wache × (1 − Faktor)</item>
    /// </list>
    /// Annahmen, die eher zu viel versprechen: Brandschaden zählt auch Blitze (Streife), der Raub des Hauses ohne die Lager auch Schiffe und
    /// Beschlagnahmen (Geleit), Stillstand auch fehlende Zutaten (Lager – dort aber nur, wenn ein Karren dann mehr lädt).
    /// </summary>
    private double MehrgewinnOhneErtrag(Betrieb b, EinrichtungWirkung wi)
    {
        var mehr = 0.0;
        if (wi.LagerPlus > 0) mehr += LagerMehrgewinn(b, wi.LagerPlus);
        var diebe = _w.Diebe;
        if (diebe.IstDiebesbetrieb(b))
        {
            if (wi.FangFaktor < 1)
                mehr += (FaengeJeJahr(b, diebe.Fangchance(b)) - FaengeJeJahr(b, diebe.Fangchance(b) * wi.FangFaktor)) * KostenJeFang(b);
            if (diebe.Versteck(b.Konto) == b)
            {
                if (wi.SpurenFaktor < 1) mehr += SpurenGefahr(b.Konto) * (1 - wi.SpurenFaktor);
                var diebesgut = DiebesgutVorjahr(b.Konto);
                if (wi.EinbruchBonus > 0 && diebesgut > 0 && _w.Gauner.IstAktiv)
                {
                    var ohne = Math.Min(0.95, _w.Gauner.Daten.Diebstahl.Chance + diebe.Daten.Einbruch.ChanceBonus + b.EinbruchBonus);
                    if (ohne > 0) mehr += diebesgut * (Math.Min(0.95, ohne + wi.EinbruchBonus) / ohne - 1);
                }
                var aufschlag = wi.PreisFaktor * (1 + wi.LadenAnteil * wi.LadenAufschlag) - 1;
                if (aufschlag > 0) mehr += diebesgut * aufschlag;
            }
        }
        var wache = _w.Wache;
        if (wache.IstWachbetrieb(b) && b.InDerStadt)
        {
            if (wi.RazziaBonus > 0 && wache.Wachstube(b.Konto) == b && wache.Vorjahr(b.Konto) is { Kopfgeld: > 0, Razzien: > 0 } buch)
            {
                var ausUrteilen = buch.Kopfgeld - TaschendiebKopfgeldVorjahr(b.Konto);
                var ohne = wache.Razziachance(b.Konto);
                if (ausUrteilen > 0 && ohne > 0) mehr += ausUrteilen * (Math.Min(0.95, ohne + wi.RazziaBonus) / ohne - 1);
            }
            if (wi.StreifenFaktor > 1) mehr += StreifeMehrgewinn(b, wi.StreifenFaktor);
            if (wi.GeleitFaktor < 1 && wache.Geleitstube == b) mehr += GeleitRaubVorjahr(b.Konto) * (1 - wi.GeleitFaktor);
        }
        return mehr;
    }

    /// <summary>
    /// Was mehr Lager je Ware dem Betrieb im Jahr einbringt – dieselbe Rechnung wie die KI beim Karrenkauf: Hat er im Vorjahr den Anteil s der
    /// Arbeitszeit stillgestanden, sinkt der Stillstand auf <c>s ^ (Durchfluss nachher ÷ vorher)</c> (<see cref="Durchfluss"/>: Ein Karren lädt höchstens
    /// das Lager); Mehrgewinn = (vorher − nachher) × Wert eines vollen Arbeitsjahres. Lädt kein Karren mehr, weil er schon weniger fasst als das Lager
    /// (Handkarren 15, Ochsenkarren 2 × 20 bei Lager 40), bringt mehr Lager nichts. 0 ohne Schalter, ohne Karren, ohne Ware oder ohne Stillstand.
    /// </summary>
    public double LagerMehrgewinn(Betrieb b, int plus)
    {
        if (!JedeWirkungRechnen || plus <= 0 || b.OhneProduktion || b.Arbeitsminuten <= 0 || b.Karren.Count == 0) return 0;
        var vorher = Math.Clamp(b.StillstandMinutenVorjahr / (double)b.Arbeitsminuten, 0, 1);
        if (vorher <= 0) return 0;
        var jetzt = b.Karren.Sum(k => Durchfluss(b, k.Typ, b.LagerKapazitaetJeWare));
        var nachher = b.Karren.Sum(k => Durchfluss(b, k.Typ, b.LagerKapazitaetJeWare + plus));
        if (jetzt <= 0 || nachher <= jetzt) return 0;
        return (vorher - DetMathe.Pow(vorher, nachher / jetzt)) * Gesamtwert(b);
    }

    /// <summary>
    /// Wie viele Stück ein Karren dieser Art je Minute seiner Fahrt bewegt: Ladung ÷ (Fahrzeit hin + zurück, <see cref="Stadtwirtschaft.Fahrzeit(Betrieb, string, bool)"/>).
    /// Ladung: was der Karren fasst, aber höchstens, was das Lager hergibt – <paramref name="lagerJeWare"/> × Zahl der Waren (Umland: seine Rohstoffe;
    /// Werkstatt: Waren in Arbeit oder Zutaten-Sorten, was mehr ist). Dieselbe Rechnung nutzt die KI beim Karrenkauf.
    /// </summary>
    public double Durchfluss(Betrieb b, string typ, int lagerJeWare)
    {
        var daten = _w.Daten.Logistik.Karren[typ];
        var minuten = _w.Fahrzeit(b, typ, zumMarkt: true) + _w.Fahrzeit(b, typ, zumMarkt: false);
        var waren = Math.Max(1, b.IstWerkstatt ? Math.Max(b.WarenInArbeit.Count, b.Zutaten.Count) : b.Rohstoffe.Count);
        return Math.Min(daten.Gesamtkapazitaet, lagerJeWare * waren) / (double)Math.Max(1, minuten);
    }

    /// <summary>Leute eines Betriebs ohne Ware je Minute (Gesellen-Leistung, mit Völkerbonus und Antreiber) – so viele Diebesstunden sammelt er je Stunde.</summary>
    private double LeuteJeMinute(Betrieb b) =>
        b.Arbeiter.Sum(a => _w.Daten.Arbeitskraefte[a.Arbeitskraft].TempoAm(b.Ort)) * _w.VolksFaktor(b.Konto, b.Artdaten.Beruf) * b.TempoBonus;

    /// <summary>
    /// Erwartete Fänge der Taschendiebe dieses Betriebs im Jahr bei dieser Fangchance je Diebesstunde – wie in <see cref="Gauner.Diebeswesen"/>:
    /// je Arbeitsstunde höchstens einer, Chance 1 − (1 − c) ^ Diebesstunden.
    /// </summary>
    private double FaengeJeJahr(Betrieb b, double chance)
    {
        var leute = LeuteJeMinute(b);
        if (leute <= 0 || chance <= 0) return 0;
        return b.Arbeitsminuten / 60.0 * (1 - DetMathe.Pow(1 - Math.Min(1, chance), leute));
    }

    /// <summary>Beute der Taschendiebe dieses Betriebs in einer Arbeitsstunde (ohne die Passanten der Bürger-KI) – der Wert einer Tat vor Gericht.</summary>
    private double BeuteJeStunde(Betrieb b) =>
        60 * LeuteJeMinute(b) * _w.Diebe.WertJeMinute(b.Stufe) * _w.Familien.Lebenswirkung(b.Konto).BeuteFaktor;

    /// <summary>
    /// Was eine Spur im Mittel an Buße kostet: Buße der Tat (Bußfaktor × Wert, mindestens die Mindestbuße) × Spitzelchance – so rechnet die KI auch
    /// das Risiko einer Bestechung –, mit dem Gnadengesuch des Hauses. Annahme: je Tat eine Buße (das Gericht rechnet eine je Anklage über alle Taten,
    /// gedeckelt am Vermögen) – nachgerechnet ≈ 490 gegen gemessen 400–410 Gulden Gericht je Fang. 0 ohne Gericht.
    /// </summary>
    private double BusseJeSpur(Konto k, double wert)
    {
        if (!_w.Justiz.IstAktiv) return 0;
        var d = _w.Justiz.Daten;
        return Math.Max(d.Mindestbusse, d.BusseFaktor * wert) * d.SpitzelChance * _w.Familien.Lebenswirkung(k).BusseFaktor;
    }

    /// <summary>
    /// Was ein gefasster Taschendieb kostet: das Handgeld für den neuen Anfänger und die Buße für die Spur (<see cref="BusseJeSpur"/>). Den Beweis
    /// bekommt der, der ihn fasst – Lederhandschuhe und Verschwiegen ändern daran nichts.
    /// </summary>
    private double KostenJeFang(Betrieb b)
    {
        var anfaenger = _w.Personal.AnfaengerFuer(b.Art, "");
        return (anfaenger == "" ? 0 : _w.Personal.Handgeld(anfaenger)) + BusseJeSpur(b.Konto, BeuteJeStunde(b));
    }

    /// <summary>
    /// Die erwartete Buße im Jahr, die weniger Spuren (Verschwiegen, Lederhandschuhe) senken können – mit den Spuren von jetzt (<see cref="Gauner.Diebeswesen.SpurenFaktor"/>).
    /// Spuren wirken auf Razzien und Auskundschafter anderer Häuser und auf Zeugen eigener nächtlicher Aufträge, nicht auf den Beweis dessen, der
    /// einen Taschendieb fasst:
    /// <list type="bullet">
    /// <item>Hat ein anderes Haus eine Wachstube (Razzia) oder ein Versteck mit Leuten (Auskundschaften), zählen die erwarteten Fänge aller
    /// Diebesbetriebe des Hauses × Buße je Spur – Annahme: Die Buße sinkt im selben Maß wie die Spuren (eher zu viel versprochen).</item>
    /// <item>Hat das Haus im Vorjahr eingebrochen (Diebesgut), zählt die Buße für dieses Diebesgut (Zeugen der eigenen Leute).</item>
    /// </list>
    /// </summary>
    private double SpurenGefahr(Konto k)
    {
        var suchende = _w.Konten.Any(x => x != k && x.IstAktiv && (_w.Wache.Wachstube(x) is not null || _w.Diebe.Versteck(x) is not null));
        var faenge = suchende
            ? _w.Betriebe.Where(x => x.Konto == k && _w.Diebe.IstDiebesbetrieb(x)).Sum(x => FaengeJeJahr(x, _w.Diebe.Fangchance(x)) * BusseJeSpur(k, BeuteJeStunde(x)))
            : 0;
        var diebesgut = DiebesgutVorjahr(k);
        var einbrueche = diebesgut > 0 ? BusseJeSpur(k, diebesgut) : 0;
        return (faenge + einbrueche) * _w.Diebe.SpurenFaktor(k);
    }

    /// <summary>
    /// Was die Diebe dieses Hauses im Vorjahr an eingebrochener Ware am Markt verkauft haben, nach der Umsatzsteuer: Umsatz der Diebesbetriebe ohne die
    /// Beute der Taschendiebe (<see cref="Gauner.Diebeswesen.Vorjahr"/>) × (1 − Steuersatz). Was zum Hehler ging oder aus einer Kasse kam, zählt nicht.
    /// </summary>
    private double DiebesgutVorjahr(Konto k)
    {
        var umsatz = _w.Betriebe.Where(x => x.Konto == k && _w.Diebe.IstDiebesbetrieb(x)).Sum(x => x.Vorjahr?.Umsatz ?? 0);
        var diebesgut = (umsatz - (_w.Diebe.Vorjahr(k)?.Beute ?? 0)) * (1 - _w.SteuersatzMarkt(k));
        return diebesgut >= 1 ? diebesgut : 0;
    }

    /// <summary>
    /// Kopfgeld für gefasste fremde Taschendiebe, das dieses Wachhaus im Vorjahr erwarten konnte: Fänge der anderen Häuser × Kopfgeld je Taschendieb
    /// ÷ Zahl der Wachhäuser (es geht an eines von ihnen, <see cref="Ordnung.Wachwesen.TaschendiebGefasst"/>). Eine Schätzung – das Wachbuch trennt
    /// das Kopfgeld nicht nach Taschendieben und Urteilen.
    /// </summary>
    private double TaschendiebKopfgeldVorjahr(Konto k)
    {
        if (!_w.Diebe.IstAktiv) return 0;
        var wache = _w.Wache;
        var fremdeFaenge = _w.Konten.Where(x => x != k).Sum(x => _w.Diebe.Vorjahr(x)?.Faenge ?? 0);
        return fremdeFaenge * wache.Daten.Kopfgeld.Taschendieb / Math.Max(1, wache.Wachhaushaeuser.Count());
    }

    /// <summary>
    /// Was mehr Streife (Wachhunde, Streifengänger) dem eigenen Haus einbringt – sie schützt alle, Gulden bringt sie dem Haus nur so:
    /// <list type="bullet">
    /// <item>Kopfgeld: Die Streife fasst fremde Taschendiebe öfter (× 1 + taschendiebFang × Schutz). Fänge der anderen Häuser im Vorjahr × Kopfgeld
    /// ÷ Zahl der Wachhäuser (das Kopfgeld geht an eines von ihnen) × relativ mehr Fänge.</item>
    /// <item>eigener Schaden in der Stadt: Raub und Brand der eigenen Betriebe im Vorjahr × relativ weniger Erfolg der Täter (Erfolg × (1 − Schutz)).
    /// Annahme: Der Brandschaden zählt auch Blitze, gegen die keine Streife hilft.</item>
    /// </list>
    /// </summary>
    private double StreifeMehrgewinn(Betrieb b, double faktor)
    {
        var wache = _w.Wache;
        var vorher = wache.Patrouilleschutz;
        var mehrSchutz = wache.PatrouilleschutzMit(b, faktor) - vorher;
        if (mehrSchutz <= 0) return 0;
        var k = b.Konto;
        var p = wache.Daten.Patrouille;
        var kopfgeld = TaschendiebKopfgeldVorjahr(k) * p.TaschendiebFang * mehrSchutz / (1 + p.TaschendiebFang * vorher);
        var schaden = _w.Betriebe.Where(x => x.Konto == k && x.InDerStadt).Sum(x => (x.Vorjahr?.Raub ?? 0) + (x.Vorjahr?.Brandschaden ?? 0));
        return kopfgeld + schaden * mehrSchutz / Math.Max(0.05, 1 - vorher);
    }

    /// <summary>
    /// Was Banden im Vorjahr den Karren dieses Hauses nahmen, die mit dem Geleit der Wache fahren: Raub des Hauses ohne den aus den Lagern der Betriebe
    /// (also Karren und Schiffe), im Anteil der eigenen Karren, die jetzt mit dem Geleit der Wache fahren. 0 ohne eigene Karren mit diesem Geleit.
    /// Annahme: Schiffe und Beschlagnahmen einer Razzia stecken mit darin (eher zu viel versprochen).
    /// </summary>
    private double GeleitRaubVorjahr(Konto k)
    {
        var eigene = _w.Betriebe.Where(x => x.Konto == k).ToList();
        var karren = eigene.Sum(x => x.Karren.Count);
        if (karren == 0) return 0;
        var mitGeleit = eigene.Sum(x => x.Karren.Count(c => c.Eskorte == _w.Wache.Daten.Geleit.Eskorte));
        if (mitGeleit == 0) return 0;
        var raub = (k.Vorjahr?.Raubwert ?? 0) - eigene.Sum(x => x.Vorjahr?.Raub ?? 0);
        return Math.Max(0, raub) * mitGeleit / karren;
    }

    /// <summary>Wie viel schneller die Leute bei so viel mehr Laune arbeiten (Anteil; linear wie <see cref="Betrieb.TempoFaktor"/>, um neutral).</summary>
    private double LauneTempo(Betrieb b, double laune)
    {
        if (laune == 0 || Daten is not { } d) return 0;
        var l = d.Laune;
        return l.TempoBis * laune / (laune > 0 ? 100 - l.Neutral : l.Neutral);
    }

    /// <summary>Wert der Arbeit eines Jahres: Werkstatt <see cref="Arbeitswert"/>, Umland die Förderung, sonst Leistung × Arbeitsminuten × Wert je Minute.</summary>
    private double Gesamtwert(Betrieb b) =>
        b.IstWerkstatt ? Arbeitswert(b, _ => 1)
        : b.OhneProduktion ? b.Arbeiter.Sum(a => _w.Daten.Arbeitskraefte[a.Arbeitskraft].TempoAm(b.Ort)) * _w.VolksFaktor(b.Konto, b.Artdaten.Beruf)
                             * b.Arbeitsminuten * Kalkulation.WertJeArbeitsminute(_w, b) * b.TempoBonus
        : Umlandwert(b);

    /// <summary>Wert der Förderung eines Jahres im Umland: Leistung × Arbeitsminuten × Wert je Minute (mit der Förderzeit von jetzt).</summary>
    private double Umlandwert(Betrieb b) =>
        b.Arbeiter.Sum(a => _w.Daten.Arbeitskraefte[a.Arbeitskraft].TempoAm(b.Ort)) * _w.VolksFaktor(b.Konto, b.Artdaten.Beruf) * b.TempoBonus
        * b.Arbeitsminuten * Kalkulation.WertJeArbeitsminute(_w, b) / (b.Rohstoffe.Count > 0 ? b.ZeitFaktor(b.Rohstoffe[0]) : 1);

    /// <summary>Stück je Jahr und Ware in dieser Werkstatt (Umland: je Rohstoff) bei der Belegung von jetzt.</summary>
    private IEnumerable<(string Ware, double Menge)> Jahresmengen(Betrieb b)
    {
        var volk = _w.VolksFaktor(b.Konto, b.Artdaten.Beruf) * b.TempoBonus * b.Arbeitsminuten;
        if (b.IstWerkstatt)
            return b.Arbeiter.GroupBy(b.WareVon, StringComparer.Ordinal)
                .Select(g => (g.Key, g.Sum(a => _w.Daten.Arbeitskraefte[a.Arbeitskraft].TempoAm(b.Ort)) * volk / b.MinutenJeStueck(g.Key)));
        var leistung = b.Arbeiter.Sum(a => _w.Daten.Arbeitskraefte[a.Arbeitskraft].TempoAm(b.Ort)) * volk;
        return b.Rohstoffe.Count switch
        {
            0 => Enumerable.Empty<(string, double)>(),
            1 => new[] { (b.Rohstoffe[0], leistung / (_w.Daten.Waren[b.Rohstoffe[0]].Zeit * b.ZeitFaktor(b.Rohstoffe[0]))) },
            _ => new[]
            {
                (b.Rohstoffe[0], leistung * (1 - b.Silberanteil) / (_w.Daten.Waren[b.Rohstoffe[0]].Zeit * b.ZeitFaktor(b.Rohstoffe[0]))),
                (b.Rohstoffe[1], leistung * b.Silberanteil / (_w.Daten.Waren[b.Rohstoffe[1]].Zeit * b.ZeitFaktor(b.Rohstoffe[1]))),
            },
        };
    }

    /// <summary>Erlös der Jahresmenge am Markt nach Steuer, mit dem Preisfaktor von jetzt und dem Aufschlag am Hafen (<see cref="Stadtwirtschaft.HafenFaktor"/>).</summary>
    private double Erloeswert(Betrieb b) =>
        Jahresmengen(b).Sum(x => x.Menge * _w.Markt[x.Ware].Preis * _w.HafenFaktor(b, x.Ware)) * (1 - _w.SteuersatzMarkt(b.Konto)) * b.PreisFaktor * _w.Familien.VerkaufsFaktor(b.Konto);

    /// <summary>Nach wie vielen Jahren sich das Stück bezahlt macht: Preis ÷ <see cref="ErwarteterMehrgewinnJeJahr"/> – null ohne Mehrgewinn.</summary>
    public double? Amortisationsjahre(Betrieb b, string kennung) =>
        Daten?.Stueck(b.Artdaten.Beruf, kennung) is { } e && ErwarteterMehrgewinnJeJahr(b, kennung) is var mehr and > 0 ? e.Preis / mehr : null;

    /// <summary>
    /// Wert der Arbeit eines Jahres in dieser Werkstatt, je Ware gewichtet: Σ je Arbeiter Leistung (mit Völkerbonus) × Arbeitsminuten
    /// × Gewinn je Minute seiner Ware (mit der Einrichtung) × <paramref name="gewicht"/>(Ware).
    /// </summary>
    private double Arbeitswert(Betrieb b, Func<string, double> gewicht)
    {
        var volk = _w.VolksFaktor(b.Konto, b.Artdaten.Beruf);
        return b.Arbeiter.Sum(a =>
        {
            var ware = b.WareVon(a);
            return _w.Daten.Arbeitskraefte[a.Arbeitskraft].TempoAm(b.Ort) * volk * b.Arbeitsminuten * b.TempoBonus
                   * Kalkulation.GewinnJeArbeitsminute(_w, ware) / b.ZeitFaktor(ware) * gewicht(ware);
        });
    }

    // ── Speichern ───────────────────────────────────────────────────────────

    internal long Wuerfe => _zufall.Wuerfe;

    internal void Wiederherstellen(long wuerfe) => _zufall = new Zufallsquelle(_zufall.Startwert, wuerfe);
}
