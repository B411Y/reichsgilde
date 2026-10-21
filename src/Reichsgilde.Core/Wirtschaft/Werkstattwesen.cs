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
public sealed record ArbeiterZeile(
    int Nummer, string Arbeitskraft, string Ware, bool Zugeteilt, double Laune, double TempoFaktor,
    long? FaelltAusBis, double Dienstjahre, string? BefoerderungVerboten);

/// <summary>Ein Stück Einrichtung, wie es die Oberfläche zeigt (gekauft oder zu kaufen).</summary>
/// <param name="Verboten">Warum man es gerade nicht kaufen kann (null: Es geht – oder es ist schon gekauft).</param>
public sealed record EinrichtungZeile(
    string Kennung, string Name, double Preis, int AbStufe, string? Voraussetzung, string Wirkung, bool Gekauft, string? Verboten);

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
        var ziel = d.Laune.Neutral + d.Stufe(b.Lohnstufe).Laune;
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

    /// <summary>Die Arbeiter des Betriebs mit Ware, Laune, Tempo, Ausfall und ob man sie befördern kann.</summary>
    public IReadOnlyList<ArbeiterZeile> Arbeiterliste(Betrieb b) => b.Arbeiter
        .Select(a => new ArbeiterZeile(a.Nummer, a.Arbeitskraft, b.WareVon(a), a.Ware is not null, a.Laune, b.TempoFaktor(a),
            a.FaelltAus(Jetzt) ? a.AusfallBis : null, (Jetzt - a.EingestelltUm) / (double)Spielzeit.MinutenProTag, BefoerdernVerboten(b, a.Nummer)))
        .ToList();

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

    /// <summary>Alle Stücke, die eine Werkstatt dieses Berufs haben kann – gekauft oder mit Preis und Verbotsgrund.</summary>
    public IReadOnlyList<EinrichtungZeile> Einrichtungsliste(Betrieb b)
    {
        if (Daten is not { } d || !b.IstWerkstatt) return Array.Empty<EinrichtungZeile>();
        return d.EinrichtungFuer(b.Artdaten.Beruf)
            .Select(e => new EinrichtungZeile(e.Kennung, e.Name, e.Preis, e.AbStufe, e.Voraussetzung, e.Wirkung.Text(),
                b.HatEinrichtung(e.Kennung), b.HatEinrichtung(e.Kennung) ? null : EinrichtungVerboten(b, e.Kennung)))
            .ToList();
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

    public string? BefoerdernVerboten(Betrieb b, int nummer)
    {
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
        if (!b.IstWerkstatt || d.Stueck(b.Artdaten.Beruf, kennung) is not { } e)
            return $"Für {b.Name} gibt es kein Stück „{kennung}“.";
        if (b.HatEinrichtung(kennung)) return $"{b.Name} hat schon: {e.Name}.";
        if (b.Stufe < e.AbStufe) return $"{e.Name} gibt es erst ab Stufe {e.AbStufe} – {b.Name} ist Stufe {b.Stufe}.";
        if (e.Voraussetzung is { } v && !b.HatEinrichtung(v))
            return $"Erst braucht es: {d.Stueck(b.Artdaten.Beruf, v)?.Name ?? v}.";
        if (b.UmbauMinuten > 0) return $"{b.Name} wird gerade umgebaut.";
        if (b.Konto.Vermoegen < e.Preis) return $"{e.Name} kostet {Zahlenformat.Ganz(e.Preis)} {Waehrung}.";
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
        var a = b.EinenEinstellen(befehl.Arbeitskraft, befehl.Ware, _w.Statut); // prüft Statut, Plätze und Namen
        return Befehlsergebnis.Ok($"{b.Name}: {a.Arbeitskraft} Nr. {a.Nummer} eingestellt" + (b.IstWerkstatt ? $", er arbeitet an {b.WareVon(a)}." : "."));
    }

    internal static Befehlsergebnis Entlassen(Betrieb b, ArbeiterEntlassen befehl)
    {
        if (b.ArbeiterNr(befehl.Arbeiter) is not { } a) return Befehlsergebnis.Abgelehnt($"In {b.Name} gibt es keinen Arbeiter Nr. {befehl.Arbeiter}.");
        b.Entlassen(a);
        return Befehlsergebnis.Ok($"{b.Name}: {a.Arbeitskraft} Nr. {a.Nummer} entlassen.");
    }

    internal Befehlsergebnis Befoerdern(Betrieb b, ArbeiterBefoerdern befehl)
    {
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
        b.Konto.Investition(e.Preis);
        b.EinrichtungHinzufuegen(e.Kennung);
        return Befehlsergebnis.Ok($"{b.Name}: {e.Name} gekauft ({Zahlenformat.Ganz(e.Preis)} {Waehrung}) – {e.Wirkung.Text()}.");
    }

    /// <summary>Was die Einrichtung des Betriebs gekostet hat – sie zählt zum Wert (Verkauf).</summary>
    public double Einrichtungswert(Betrieb b) => b.Einrichtungsstuecke.Sum(e => e.Preis);

    /// <summary>Was diese Einrichtung (Kennungen) in einem Haus dieses Berufs wert ist.</summary>
    internal double Einrichtungswert(string beruf, IEnumerable<string> kennungen) =>
        Daten is { } d ? kennungen.Sum(k => d.Stueck(beruf, k)?.Preis ?? 0) : 0;

    // ── Speichern ───────────────────────────────────────────────────────────

    internal long Wuerfe => _zufall.Wuerfe;

    internal void Wiederherstellen(long wuerfe) => _zufall = new Zufallsquelle(_zufall.Startwert, wuerfe);
}
