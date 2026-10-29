using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Wohnen;

// ═════════════════════════════════════════════════════════════════════════════
//  Daten (Abschnitt „wohnen" in wirtschaft.json, ab Datenversion 31)
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Eine Stufe des Wohnhauses, z. B. „Bürgerhaus“.</summary>
public sealed class WohnhausStufe
{
    public string Name { get; init; } = "";

    /// <summary>Was der Ausbau auf diese Stufe kostet (Gulden, einmalig).</summary>
    public double Ausbau { get; init; }

    /// <summary>Unterhalt im Jahr (Gulden, mit den Haushaltskosten).</summary>
    public double Unterhalt { get; init; }

    /// <summary>So viel Ansehen bringt das Haus (Rat, Brautschau, Gericht).</summary>
    public double Ansehen { get; init; }

    /// <summary>Ab diesem Stand (0 = Gemeiner) ist die Stufe Pflicht – wer in den Stand aufsteigen will, braucht sie vorher.</summary>
    public int AbStand { get; init; }
}

public sealed class WohnenDaten
{
    /// <summary>Die Stufen von klein nach groß. Die erste ist das Startwohnhaus (kostenlos).</summary>
    public List<WohnhausStufe> Stufen { get; init; } = new();

    /// <summary>So lange dauert ein Ausbau (Spielminuten). Bis dahin gilt die alte Stufe.</summary>
    public int BauzeitMinuten { get; init; } = 240;

    /// <summary>Spalten des Talentbaums im Wohnhaus (PR #224): Haushalt, Ansehen, Schutz. Die Reihen sind die ersten drei Stufen des Wohnhauses.</summary>
    public List<string> Spalten { get; init; } = new();

    /// <summary>
    /// Einrichtung des Wohnhauses (PR #224): <see cref="EinrichtungDaten.AbStufe"/> ist die Stufe des Wohnhauses (1 = Startwohnhaus).
    /// Dazu zwei vorhandene Sachen des Spiels (<see cref="EinrichtungDaten.Vorhanden"/>): Hausapotheke und Hauswächter.
    /// </summary>
    public List<EinrichtungDaten> Einrichtung { get; init; } = new();

    public EinrichtungDaten? Stueck(string kennung) => Einrichtung.FirstOrDefault(e => e.Kennung == kennung);

    internal void Pruefen(List<string> f, int anzahlStaende, IReadOnlyDictionary<string, WareDaten> waren)
    {
        foreach (var doppelt in Einrichtung.GroupBy(e => e.Kennung).Where(g => g.Count() > 1))
            f.Add($"wohnen.einrichtung: „{doppelt.Key}“ ist doppelt eingetragen.");
        foreach (var e in Einrichtung)
        {
            var wo = $"wohnen.einrichtung.{e.Kennung}";
            if (string.IsNullOrWhiteSpace(e.Kennung) || e.Kennung.Any(c => !char.IsAsciiLetterOrDigit(c)))
                f.Add($"wohnen.einrichtung: Die Kennung „{e.Kennung}“ darf nur Buchstaben und Ziffern ohne Umlaute haben.");
            if (e.Preis < 0) f.Add($"{wo}.preis darf nicht negativ sein.");
            if (e.AbStufe < 1 || e.AbStufe > Math.Min(3, Stufen.Count)) f.Add($"{wo}.abStufe muss zwischen 1 und {Math.Min(3, Stufen.Count)} liegen.");
            if (e.Voraussetzung is { } v && Einrichtung.All(x => x.Kennung != v)) f.Add($"{wo}.voraussetzung: „{v}“ gibt es nicht.");
            if (e.Voraussetzung == e.Kennung) f.Add($"{wo}: Ein Stück kann nicht sich selbst voraussetzen.");
            if (Spalten.Count > 0 && e.Spalte is { } sp && !Spalten.Contains(sp)) f.Add($"{wo}.spalte: „{sp}“ steht nicht unter wohnen.spalten.");
            if (e.Vorhanden is { } da && da is not ("Hausapotheke" or "Hauswaechter")) f.Add($"{wo}.vorhanden: nur „Hausapotheke“ oder „Hauswaechter“.");
            e.Wirkung.Pruefen(f, $"{wo}.wirkung", waren);
        }
        if (Stufen.Count == 0) { f.Add("wohnen.stufen darf nicht leer sein."); return; }
        if (Stufen[0].AbStand != 0 || Stufen[0].Ausbau != 0)
            f.Add("wohnen.stufen: Die erste Stufe ist das Startwohnhaus (abStand 0, ausbau 0).");
        for (var i = 0; i < Stufen.Count; i++)
        {
            var s = Stufen[i];
            if (string.IsNullOrWhiteSpace(s.Name)) f.Add($"wohnen.stufen[{i}] braucht einen Namen.");
            if (s.Ausbau < 0 || s.Unterhalt < 0) f.Add($"wohnen.stufen[{i}] ({s.Name}): Ausbau und Unterhalt dürfen nicht negativ sein.");
            if (s.AbStand < 0 || s.AbStand >= anzahlStaende) f.Add($"wohnen.stufen[{i}] ({s.Name}): abStand muss ein Stand zwischen 0 und {anzahlStaende - 1} sein.");
            if (i > 0 && s.AbStand <= Stufen[i - 1].AbStand) f.Add($"wohnen.stufen[{i}] ({s.Name}): abStand muss größer sein als bei der Stufe davor.");
        }
        if (BauzeitMinuten < 0) f.Add("wohnen.bauzeitMinuten darf nicht negativ sein.");
    }
}

// ═════════════════════════════════════════════════════════════════════════════
//  Teilsystem
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Das Wohnhaus jedes Hauses – getrennt von den Betrieben (Entscheidung Leveldesign/Kern, 16.10.2026).
/// Es wird am selben Ort Stufe für Stufe ausgebaut (Fachwerkhaus → Bürgerhaus → Steinhaus → Stadtpalais → Stadthof);
/// wo gebaut wird, entstehen Stadtpalais und Stadthof nur als Neubau oder durch Kauf eines Leerstands (<see cref="Bauen.Bauwesen.NurNeubau"/>),
/// bringt Ansehen, kostet Unterhalt und ist <b>Voraussetzung für den Stand</b>: Wer Bürger werden will, braucht vorher ein Bürgerhaus.
/// Ausbauen darf man höchstens eine Stufe über dem eigenen Stand.
///
/// Der Zustand (Stufe, Bauende) liegt am <see cref="Konto"/>, weil das Wohnhaus dem Haus gehört.
/// Fehlt der Abschnitt „wohnen“, gibt es keine Wohnhäuser – alles wie vorher.
///
/// Gauner (Abschnitt gauner.wohnhaus): Einbruch holt Bargeld, Brand kostet Reparatur und das Ansehen des Hauses,
/// bis es instand gesetzt ist – die Stufe und damit der Stand sind nie in Gefahr. Ein Hauswächter schützt wie im Betrieb.
/// </summary>
public sealed class Wohnungswesen
{
    private readonly Stadtwirtschaft _w;
    public WohnenDaten? Daten { get; }
    public bool IstAktiv => Daten is not null;

    internal Wohnungswesen(Stadtwirtschaft w, WohnenDaten? daten)
    {
        _w = w;
        Daten = daten;
    }

    // ── Auskunft ─────────────────────────────────────────────────────────────

    /// <summary>Die Stufe, in der das Haus jetzt wohnt (null ohne Wohnhäuser).</summary>
    public WohnhausStufe? Stufe(Konto k) => Daten?.Stufen[Math.Clamp(k.WohnhausStufe, 0, Daten.Stufen.Count - 1)];

    /// <summary>Die nächste Stufe (null: schon die größte oder keine Wohnhäuser).</summary>
    public WohnhausStufe? NaechsteStufe(Konto k) =>
        Daten is { } d && k.WohnhausStufe + 1 < d.Stufen.Count ? d.Stufen[k.WohnhausStufe + 1] : null;

    /// <summary>Wird gerade ausgebaut? Dann steht hier die Spielminute, ab der die neue Stufe gilt.</summary>
    public long? FertigAb(Konto k) => k.WohnhausFertigAb;

    /// <summary>Welche Stufe braucht ein Haus mindestens, um in diesem Stand zu sein?</summary>
    public int NoetigeStufe(int stand)
    {
        if (Daten is not { } d) return 0;
        var stufe = 0;
        for (var i = 0; i < d.Stufen.Count; i++)
            if (d.Stufen[i].AbStand <= stand) stufe = i;
        return stufe;
    }

    /// <summary>Warum dieses Haus nicht in den Stand <paramref name="neuerStand"/> aufsteigen darf – oder null.</summary>
    public string? StandVerboten(Konto k, int neuerStand)
    {
        if (Daten is not { } d) return null;
        var noetig = NoetigeStufe(neuerStand);
        if (k.WohnhausStufe >= noetig) return null;
        var stand = _w.Daten.Staende[Math.Min(neuerStand, _w.Daten.Staende.Count - 1)].Name;
        if (_w.Bauen.UmzugZiel(k) >= noetig)
            return $"Für den Stand {stand} braucht {k.Inhaber} ein {d.Stufen[noetig].Name} – das neue Haus ist noch nicht bezogen.";
        return k.WohnhausFertigAb is not null && k.WohnhausStufe + 1 >= noetig
            ? $"Für den Stand {stand} braucht {k.Inhaber} ein {d.Stufen[noetig].Name} – der Ausbau läuft noch."
            : $"Für den Stand {stand} braucht {k.Inhaber} erst ein {d.Stufen[noetig].Name} " +
              (_w.Bauen.NurNeubau(noetig) ? "(neu bauen oder einen Leerstand kaufen)." : "(Wohnhaus ausbauen).");
    }

    /// <summary>
    /// Was das Wohnhaus wert ist: die Ausbaupreise aller Stufen bis zur jetzigen, dazu ein Ausbau, der schon bezahlt ist und noch läuft.
    /// Es zählt ganz zum Reinvermögen – wer baut, tauscht Bargeld gegen Hauswert und fällt nicht unter die Schwelle seines Standes.
    /// Läuft ein Neubau oder die Sanierung eines gekauften Hauses, zählt schon das neue (das alte ist angerechnet und wird abgerissen).
    /// Ist das Wohnhaus eines toten Hauses verkauft oder abgerissen, ist es nichts mehr wert.
    /// </summary>
    public double Hauswert(Konto k)
    {
        if (Daten is not { } d) return 0;
        if (_w.Bauen.UmzugZiel(k) is { } ziel) return WertDerStufe(ziel);
        if (_w.Bauen.HausAufgegeben(k)) return 0;
        return WertDerStufe(Math.Min(k.WohnhausStufe + (k.WohnhausFertigAb is not null ? 1 : 0), d.Stufen.Count - 1));
    }

    /// <summary>Hauswert eines Wohnhauses dieser Stufe: die Ausbaupreise aller Stufen bis dahin (Fachwerkhaus 0, Bürgerhaus 1.500 …).</summary>
    public double WertDerStufe(int stufe)
    {
        if (Daten is not { } d) return 0;
        var wert = 0.0;
        for (var i = 1; i <= Math.Min(stufe, d.Stufen.Count - 1); i++) wert += d.Stufen[i].Ausbau;
        return wert;
    }

    /// <summary>Den Hauswert am Konto neu setzen (nach Ausbau und beim Laden).</summary>
    internal void HauswertAktualisieren(Konto k) => k.Hauswert = Hauswert(k);

    /// <summary>Ansehen aus dem Wohnhaus – nach einem Brand fehlt es, bis das Haus instand gesetzt ist.</summary>
    public double AnsehenBonus(Konto k) => IstBeschaedigt(k) ? 0 : Stufe(k)?.Ansehen ?? 0;

    /// <summary>Hat es gebrannt, und wird das Haus noch instand gesetzt? (Bis wann: <see cref="Konto.WohnhausInstandBis"/>.)</summary>
    public bool IstBeschaedigt(Konto k) => k.WohnhausInstandBis is not null;

    /// <summary>Unterhalt des Wohnhauses im Jahr.</summary>
    public double Unterhalt(Konto k) => Stufe(k)?.Unterhalt ?? 0;

    /// <summary>
    /// Häuser, deren Wohnhaus leer steht (erloschen oder bankrott) und noch nicht verkauft oder abgerissen ist.
    /// Kaufen und abreißen: <see cref="Bauen.Bauwesen.LeerstandVorschau"/>.
    /// </summary>
    public IEnumerable<Konto> Leerstand => IstAktiv ? _w.Konten.Where(k => !k.IstAktiv && !_w.Bauen.HausAufgegeben(k)) : Enumerable.Empty<Konto>();

    /// <summary>Warum dieses Haus jetzt nicht ausbauen kann – oder null.</summary>
    public string? AusbauVerboten(Konto k)
    {
        if (Daten is null) return "In dieser Stadt gibt es keine Wohnhäuser.";
        if (!k.IstAktiv) return $"{k.Inhaber} ist nicht mehr im Spiel.";
        if (k.WohnhausFertigAb is not null) return $"Am Wohnhaus von {k.Inhaber} wird schon gebaut.";
        if (_w.Bauen.Umzug(k) is not null) return $"{k.Inhaber} zieht bald in ein neues Wohnhaus – ausbauen erst danach.";
        if (IstBeschaedigt(k)) return $"Das Wohnhaus von {k.Inhaber} wird nach dem Brand noch instand gesetzt.";
        if (NaechsteStufe(k) is not { } naechste) return $"{k.Inhaber} wohnt schon im größten Haus ({Stufe(k)!.Name}).";
        if (_w.Bauen.NurNeubau(k.WohnhausStufe + 1))
            return $"Ein {naechste.Name} entsteht nur als Neubau – im Baumodus neu bauen oder einen Leerstand kaufen.";
        if (k.Stand < naechste.AbStand - 1)
            return $"Ein {naechste.Name} darf erst bauen, wer mindestens {_w.Daten.Staende[naechste.AbStand - 1].Name} ist.";
        if (k.Vermoegen < naechste.Ausbau)
            return $"Der Ausbau zum {naechste.Name} kostet {Zahlenformat.Ganz(naechste.Ausbau)} {_w.Daten.Allgemein.Waehrung} – " +
                   $"es fehlen {Zahlenformat.Ganz(naechste.Ausbau - k.Vermoegen)}.";
        return null;
    }

    // ── Handeln ──────────────────────────────────────────────────────────────

    /// <summary>Befehl „Wohnhaus ausbauen“: bezahlen, die Bauzeit läuft.</summary>
    internal Befehlsergebnis Ausbauen(Konto k, Spielzeit zeit)
    {
        if (AusbauVerboten(k) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var naechste = NaechsteStufe(k)!;
        k.Investition(naechste.Ausbau);
        k.WohnhausFertigAb = zeit.VergangeneMinuten + Daten!.BauzeitMinuten;
        HauswertAktualisieren(k); // das Geld steckt jetzt im Haus

        if (Daten.BauzeitMinuten == 0) Minute(zeit);
        var dauer = Daten.BauzeitMinuten >= 60 ? $"{Zahlenformat.Knapp(Daten.BauzeitMinuten / 60.0)} Stunden" : $"{Daten.BauzeitMinuten} Minuten";
        return Befehlsergebnis.Ok($"{k.Inhaber} baut das Wohnhaus zum {naechste.Name} aus " +
                                  $"({Zahlenformat.Ganz(naechste.Ausbau)} {_w.Daten.Allgemein.Waehrung}, fertig in {dauer}).");
    }

    // ── Einrichtung des Wohnhauses (PR #224) ────────────────────────────────

    /// <summary>Die gekaufte Einrichtung dieses Hauses mit ihren Daten (Hausapotheke und Hauswächter zählen hier nicht – sie wirken auf eigenem Weg).</summary>
    public IEnumerable<EinrichtungDaten> Einrichtungsstuecke(Konto k) =>
        Daten is not { } d || k.WohnhausEinrichtung.Count == 0 ? Enumerable.Empty<EinrichtungDaten>()
            : d.Einrichtung.Where(e => e.Vorhanden is null && k.WohnhausEinrichtung.Contains(e.Kennung));

    /// <summary>Faktor auf Haushalt und Unterhalt der Familie (Vorratskammer).</summary>
    public double HaushaltFaktor(Konto k) => Einrichtungsstuecke(k).Aggregate(1.0, (f, e) => f * e.Wirkung.HaushaltFaktor);

    /// <summary>Faktor auf die Chance der Familie zu erkranken (Küche).</summary>
    public double KrankheitFaktor(Konto k) => Einrichtungsstuecke(k).Aggregate(1.0, (f, e) => f * e.Wirkung.KrankheitFaktor);

    /// <summary>So viel Ansehen mehr (Wappen über der Tür).</summary>
    public double EinrichtungAnsehen(Konto k) => Einrichtungsstuecke(k).Sum(e => e.Wirkung.AnsehenPlus);

    /// <summary>Faktor auf die Zuneigung je Gulden beim Werben (Stube).</summary>
    public double WerbeFaktor(Konto k) => Einrichtungsstuecke(k).Aggregate(1.0, (f, e) => f * e.Wirkung.WerbeFaktor);

    /// <summary>Faktor auf die Talente aus der Ausbildung der Kinder (Bibliothek).</summary>
    public double AusbildungFaktor(Konto k) => Einrichtungsstuecke(k).Aggregate(1.0, (f, e) => f * e.Wirkung.AusbildungFaktor);

    /// <summary>Faktor auf die Erfolgschance von Einbruch ins Wohnhaus (Riegel, Hund).</summary>
    public double EinbruchFaktor(Konto k) => Einrichtungsstuecke(k).Aggregate(1.0, (f, e) => f * e.Wirkung.EinbruchFaktor);

    /// <summary>Faktor auf die Erfolgschance von Brand am Wohnhaus (Hund).</summary>
    public double BrandFaktor(Konto k) => Einrichtungsstuecke(k).Aggregate(1.0, (f, e) => f * e.Wirkung.BrandFaktor);

    /// <summary>Ist dieses Stück im Haus? Hausapotheke immer, Hauswächter, wenn einer angestellt ist, sonst gekauft.</summary>
    public bool HatEinrichtung(Konto k, EinrichtungDaten e) => e.Vorhanden switch
    {
        "Hausapotheke" => true,
        "Hauswaechter" => k.Hauswaechter,
        _ => k.WohnhausEinrichtung.Contains(e.Kennung),
    };

    /// <summary>Warum dieses Haus das Stück jetzt nicht kaufen kann (null: Es geht). Hausapotheke und Hauswächter kauft man nicht.</summary>
    public string? EinrichtungVerboten(Konto k, string kennung)
    {
        if (Daten is not { } d || d.Stueck(kennung) is not { } e) return $"Für das Wohnhaus gibt es kein Stück „{kennung}“.";
        if (e.Vorhanden == "Hausapotheke") return "Die Hausapotheke hat jedes Haus – Kräuterschnaps kauft man am Markt.";
        if (e.Vorhanden == "Hauswaechter")
            return k.Hauswaechter ? $"{k.Inhaber} hat schon einen Hauswächter." : null; // eingestellt wird er mit „Hauswächter“
        if (k.WohnhausEinrichtung.Contains(kennung)) return $"{k.Inhaber} hat schon: {e.Name}.";
        var stufe = k.WohnhausStufe + 1;
        if (stufe < e.AbStufe) return $"{e.Name} passt erst in ein {d.Stufen[e.AbStufe - 1].Name} – {k.Inhaber} wohnt im {d.Stufen[k.WohnhausStufe].Name}.";
        if (e.Voraussetzung is { } v && d.Stueck(v) is { } vor && !HatEinrichtung(k, vor)) return $"Erst braucht es: {vor.Name}.";
        if (k.Vermoegen < e.Preis) return $"{e.Name} kostet {Zahlenformat.Ganz(e.Preis)} {_w.Daten.Allgemein.Waehrung}.";
        return null;
    }

    internal Befehlsergebnis EinrichtungKaufen(Konto k, string kennung)
    {
        if (EinrichtungVerboten(k, kennung) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var e = Daten!.Stueck(kennung)!;
        if (e.Vorhanden == "Hauswaechter") return HauswaechterSetzen(k, true);
        k.Investition(e.Preis); // wie Einrichtung der Betriebe: ein Kauf, kein Rückverkauf
        k.WohnhausEinrichtungHinzufuegen(kennung);
        return Befehlsergebnis.Ok($"{k.Inhaber}: {e.Name} fürs Wohnhaus gekauft ({Zahlenformat.Ganz(e.Preis)} {_w.Daten.Allgemein.Waehrung}) – {e.Wirkung.Text()}.");
    }

    /// <summary>
    /// Was ein Stück dem Haus im Jahr spart (für KI und Anzeige): Vorratskammer = Haushalt und Unterhalt der Familie × (1 − Faktor);
    /// Riegel und Hund = was Einbrecher im Vorjahr aus dem Wohnhaus holten × (1 − Faktor). Sonst (Ansehen, Werben, Ausbildung, Krankheit) 0.
    /// </summary>
    public double MehrgewinnJeJahr(Konto k, string kennung)
    {
        if (Daten?.Stueck(kennung) is not { } e || e.Vorhanden is not null || k.WohnhausEinrichtung.Contains(kennung)) return 0;
        var mehr = 0.0;
        if (e.Wirkung.HaushaltFaktor < 1 && _w.Familien.Von(k) is { } f)
            mehr += _w.Familien.Haushaltskosten(f) * HaushaltFaktor(k) * (1 - e.Wirkung.HaushaltFaktor);
        // Riegel und Hund: nur, wer im Vorjahr (oder dieses Jahr) heimgesucht wurde – mit dem, was ein Einbruch jetzt holte.
        if (e.Wirkung.EinbruchFaktor < 1 && _w.Gauner.GegenWohnhaus && _w.Gauner.LetzterAngriffWohnhaus(k) is { } jahr && jahr >= _w.Familien.Jahr - 1)
            mehr += _w.Gauner.Hausbeute(k) * EinbruchFaktor(k) * (1 - e.Wirkung.EinbruchFaktor);
        return mehr;
    }

    /// <summary>Der Talentbaum der Einrichtung des Wohnhauses (Spalten Haushalt, Ansehen, Schutz; Reihen die ersten drei Stufen des Wohnhauses).</summary>
    public Einrichtungsbaum Baum(Konto k)
    {
        if (Daten is not { } d || d.Einrichtung.Count == 0) return new Einrichtungsbaum(Array.Empty<string>(), 3, k.WohnhausStufe + 1, Array.Empty<BaumStueck>());
        var spalten = d.Spalten.Count > 0 ? d.Spalten : d.Einrichtung.Select(e => e.Spalte ?? "").Distinct().ToList();
        var stufe = k.WohnhausStufe + 1;
        return new Einrichtungsbaum(spalten, Math.Min(3, d.Stufen.Count), stufe, d.Einrichtung.Select(e =>
        {
            var da = HatEinrichtung(k, e);
            var verboten = da ? null : EinrichtungVerboten(k, e.Kennung);
            var gesperrt = stufe < e.AbStufe || (e.Voraussetzung is { } v && d.Stueck(v) is { } vor && !HatEinrichtung(k, vor));
            var jaehrlich = e.Vorhanden == "Hauswaechter";
            var preis = jaehrlich ? _w.Daten.Gauner?.WaechterKosten ?? 0 : e.Preis;
            var mehr = MehrgewinnJeJahr(k, e.Kennung);
            var text = e.Vorhanden switch
            {
                "Hausapotheke" => "Kräuterschnaps für die Familie (vom Markt) – heilt leichte Krankheiten, lindert die Pest",
                "Hauswaechter" => "Einbruch und Brand am Wohnhaus halb so oft, mehr Zeugen – Lohn im Jahr",
                _ => e.Wirkung.Text(),
            };
            return new BaumStueck(e.Kennung, e.Name, e.Spalte ?? spalten.FirstOrDefault() ?? "", e.AbStufe, preis, e.Voraussetzung, text,
                da ? Baumzustand.Gekauft : verboten is null ? Baumzustand.Kaufbar : gesperrt ? Baumzustand.Gesperrt : Baumzustand.NichtJetzt,
                verboten, mehr, mehr > 0 ? e.Preis / mehr : null, jaehrlich, e.Vorhanden);
        }).ToList());
    }

    /// <summary>Befehl „Hauswächter“: einstellen oder entlassen. Er kostet im Jahr so viel wie ein Wächter im Betrieb.</summary>
    internal Befehlsergebnis HauswaechterSetzen(Konto k, bool angestellt)
    {
        if (Daten is null || _w.Daten.Gauner?.Wohnhaus is null)
            return Befehlsergebnis.Abgelehnt("In dieser Stadt bricht niemand in Wohnhäuser ein – ein Hauswächter wäre umsonst.");
        if (k.Hauswaechter == angestellt)
            return Befehlsergebnis.Ok($"{k.Inhaber} hat {(angestellt ? "schon einen" : "keinen")} Hauswächter.");
        k.Hauswaechter = angestellt;
        return Befehlsergebnis.Ok(angestellt
            ? $"{k.Inhaber} stellt einen Hauswächter ein ({Zahlenformat.Ganz(_w.Daten.Gauner.WaechterKosten)} {_w.Daten.Allgemein.Waehrung} im Jahr)."
            : $"{k.Inhaber} entlässt den Hauswächter.");
    }

    /// <summary>Ein Brand (Gaunerstück „Brandstiftung“): Bis zur angegebenen Minute fehlt das Ansehen des Hauses.</summary>
    internal void Brand(Konto k, long instandBis) => k.WohnhausInstandBis = instandBis;

    /// <summary>Jede Spielminute: fertige Ausbauten übernehmen, abgebrannte Häuser wieder instand setzen.</summary>
    internal void Minute(Spielzeit zeit)
    {
        if (Daten is null) return;
        foreach (var k in _w.Konten)
        {
            if (k.WohnhausFertigAb is { } ab && zeit.VergangeneMinuten >= ab)
            {
                k.WohnhausFertigAb = null;
                k.WohnhausStufe++;
                _w.Melden($"{k.Inhaber} bezieht das neue {Stufe(k)!.Name}.");
            }
            if (k.WohnhausInstandBis is { } bis && zeit.VergangeneMinuten >= bis)
            {
                k.WohnhausInstandBis = null;
                if (k.IstAktiv) _w.Melden($"Das {Stufe(k)!.Name} von {k.Inhaber} ist nach dem Brand wieder instand gesetzt.");
            }
        }
    }

    /// <summary>Jahresende: Unterhalt der Wohnhäuser (mit den Haushaltskosten).</summary>
    internal void UnterhaltZahlen()
    {
        if (Daten is null) return;
        foreach (var k in _w.Konten.Where(k => k.IstAktiv))
        {
            if (Unterhalt(k) is var betrag and > 0)
                k.Familienkosten(betrag);
            if (k.Hauswaechter && _w.Daten.Gauner is { } g)
                k.Personal(g.WaechterKosten);
        }
    }

    /// <summary>Ein Haus ohne gespeicherte Stufe (alter Spielstand): das Wohnhaus, das sein Stand verlangt.</summary>
    internal void AlteStaendeErgaenzen(IEnumerable<Konto> ohneStufe)
    {
        foreach (var k in ohneStufe)
            k.WohnhausStufe = NoetigeStufe(k.Stand);
    }

    /// <summary>Nach dem Laden: Hauswerte aus Stufe und laufendem Ausbau berechnen (sie werden nicht gespeichert).</summary>
    internal void HauswerteNachDemLaden(IEnumerable<Konto> konten)
    {
        foreach (var k in konten) HauswertAktualisieren(k);
    }
}
