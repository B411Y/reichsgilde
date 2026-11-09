using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Dynastie;

/// <summary>Was eine Handlung einer Person an Stress bringt (Vorschau und Buchung).</summary>
/// <param name="Ventil">Die Aktivität, deren Stress je Art einmal im Jahr wirkt (null bei anderen Quellen).</param>
public sealed record StressPosten(Person Person, double Wert, string Text, string? Ventil);

/// <summary>
/// Stress (Charaktersystem Teil 3b, Abschnitt "stress"): nur für Oberhaupt, Erbe und Ehepartner der Häuser, Ratsherren haben keinen.
///
/// Quellen: Taten gegen den eigenen Charakter (<see cref="StressDaten.Taten"/>, gebucht nach einem erfolgreichen Befehl), eine Aktivität, die man
/// nicht mag, Trauer (beim Tod eines Angehörigen), Streit im Haus, ein Jahr mit Verlust oder Bargeld im Minus, Gericht (angeklagt, verurteilt,
/// Kerker) und Krankheit (erkrankt, Pest im Haus). Ventile: die Aktivitäten aus <see cref="StressDaten.Ventile"/> (Wirtshaus, Messe, Jagd), je Art
/// einmal im Jahr, für beide Beteiligten, wenn das Gegenüber selbst Stress hat. Jedes Jahr klingt er ab (glückliche Ehe mehr, Schwermut weniger).
///
/// Folgen nach Stufe (<see cref="StressDaten.Stufen"/>): weniger Erfahrung der Lebensweise, mehr Gefahr bei Aktivitäten, ab „zermürbt“ ein halber
/// Stern weniger in jedem Talent, öfter krank, der Ehepartner denkt schlechter. Beim Höchstwert bricht die Person zusammen: ein negativer
/// erworbener Zug oder eine Krankheit, danach fällt der Stress auf <see cref="StressDaten.NachZusammenbruch"/>.
///
/// Gewürfelt wird nur beim Zusammenbruch, mit einem eigenen Generator (im Spielstand) – alle übrigen Würfe bleiben wie vorher.
/// </summary>
public sealed partial class Familienbuch
{
    private Zufallsquelle _stressZufall;

    /// <summary>Die Daten des Stresses (null = ohne Stress, etwa in alten Spielständen).</summary>
    public StressDaten? StressDaten => _w.Daten.Stress is { IstAktiv: true } d ? d : null;
    public bool HatStresswesen => IstAktiv && StressDaten is not null;

    /// <summary>Sammelt diese Person Stress? Oberhaupt, Erbe und Ehepartner eines Hauses, solange sie leben.</summary>
    public bool HatStress(Person p) =>
        HatStresswesen && p.Lebt && FamilieVon(p) is { } f && f.Konto.IstAktiv && (p == f.Oberhaupt || p == f.Ehepartner || p == Erbe(f));

    /// <summary>Stress der Person, 0 … 100 (0 ohne Stress in den Daten).</summary>
    public double Stress(Person p) => HatStresswesen ? p.Stresslage.Wert : 0;

    /// <summary>Stufe zu diesem Stress: 0 ruhig, 1 angespannt, 2 zermürbt (so viele Stufen, wie die Daten haben).</summary>
    public int StressStufe(double wert) => StressDaten is not { } d ? 0 : d.Stufen.Count(s => wert >= s.Ab - 1e-9);

    public int StressStufe(Person p) => StressStufe(Stress(p));

    /// <summary>„ruhig“, „angespannt“, „zermürbt“.</summary>
    public string StressStufenname(int stufe) =>
        StressDaten is { } d && stufe > 0 && stufe <= d.Stufen.Count ? d.Stufen[stufe - 1].Name : "ruhig";

    private StressStufeDaten? Stufendaten(Person p) =>
        StressDaten is { } d && StressStufe(p) is var s and > 0 ? d.Stufen[s - 1] : null;

    /// <summary>Faktor auf die Erfahrung der Lebensweise (1 ruhig, 0,75 angespannt, 0,5 zermürbt).</summary>
    public double StressErfahrungFaktor(Person p) => Stufendaten(p)?.Erfahrung ?? 1;

    /// <summary>So viel mehr Gefahr bei Aktivitäten dieser Person (0,05 = +5 Prozentpunkte ab „angespannt“).</summary>
    public double StressGefahr(Person p) => Stufendaten(p)?.Gefahr ?? 0;

    /// <summary>Faktor auf die Chance zu erkranken (1,25 ab „zermürbt“).</summary>
    public double StressKrankheitFaktor(Person p) => Stufendaten(p)?.Krankheit ?? 1;

    /// <summary>So viel Meinung hat der Ehepartner über die Person („unleidlich“, −5 ab „zermürbt“).</summary>
    public double StressMeinungEhepartner(Person p) => Stufendaten(p)?.MeinungEhepartner ?? 0;

    /// <summary>Faktor auf die Vorsicht eines KI-Hauses: nach dem Stress seines Oberhaupts (1,2 ab „zermürbt“).</summary>
    public double KiStressVorsicht(Konto konto) => Von(konto) is { } f ? Stufendaten(f.Oberhaupt)?.KiVorsicht ?? 1 : 1;

    /// <summary>„Erfahrung der Lebensweise × 0,75; Gefahr bei Aktivitäten +5 Prozentpunkte“ – die Folgen einer Stufe (leer: keine).</summary>
    public string StressFolgen(int stufe)
    {
        if (StressDaten is not { } d || stufe <= 0 || stufe > d.Stufen.Count) return "";
        var s = d.Stufen[stufe - 1];
        var teile = new List<string>();
        if (Math.Abs(s.Erfahrung - 1) > 1e-9) teile.Add($"Erfahrung der Lebensweise −{Zahlenformat.Ganz((1 - s.Erfahrung) * 100)} %");
        if (s.Gefahr > 0) teile.Add($"Gefahr bei Aktivitäten +{Zahlenformat.Ganz(s.Gefahr * 100)} Prozentpunkte");
        if (s.Talente > 0) teile.Add($"alle Talente −{TalentTexte.Sterne(s.Talente)} Stern");
        if (Math.Abs(s.Krankheit - 1) > 1e-9) teile.Add($"Krankheiten +{Zahlenformat.Ganz((s.Krankheit - 1) * 100)} %");
        if (s.MeinungEhepartner < 0) teile.Add($"Ehepartner {Zahlenformat.MitVorzeichen(s.MeinungEhepartner)} Meinung („unleidlich“)");
        return string.Join("; ", teile);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Ändern
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Stress ändern: positiv eine Quelle, negativ ein Ventil oder das Abklingen. Begrenzt auf 0 … Höchstwert; eingetragen wird die tatsächliche
    /// Änderung. Erreicht der Stress den Höchstwert, bricht die Person zusammen.
    /// </summary>
    /// <param name="nurTraeger">Nur für Stressträger (Quellen und Ventile); das Abklingen gilt für jeden mit Stress.</param>
    internal void StressAendern(Person p, double wert, string text, bool nurTraeger = true)
    {
        if (StressDaten is not { } d || !IstAktiv || Math.Abs(wert) < 1e-9 || !p.Lebt) return;
        if (nurTraeger && !HatStress(p)) return;
        var s = p.Stresslage;
        var vorher = s.Wert;
        s.Wert = Math.Clamp(vorher + wert, 0, d.Hoechstwert);
        if (Math.Abs(s.Wert - vorher) < 1e-9) return;
        s.Eintragen(new StressEintrag(Jahr, text, s.Wert - vorher));
        TalentAbzugNeu(p);
        if (s.Wert >= d.Hoechstwert - 1e-9) Zusammenbruch(p);
    }

    private void TalentAbzugNeu(Person p) => p.Stresslage.TalentAbzug = Stufendaten(p)?.Talente ?? 0;

    /// <summary>Für Tests und das Entwicklerfenster: Stress direkt setzen (ohne Zusammenbruch, ohne Eintrag).</summary>
    internal void StressSetzen(Person p, double wert)
    {
        p.Stresslage.Wert = Math.Clamp(wert, 0, StressDaten?.Hoechstwert ?? 100);
        TalentAbzugNeu(p);
    }

    /// <summary>
    /// Zusammenbruch: mit <see cref="StressZusammenbruchDaten.ZugChance"/> ein negativer erworbener Zug, sonst eine Krankheit (geht das eine
    /// nicht – alle Züge schon da bzw. schon krank –, das andere). Danach fällt der Stress auf <see cref="StressDaten.NachZusammenbruch"/>.
    /// </summary>
    private void Zusammenbruch(Person p)
    {
        var d = StressDaten!;
        var konto = FamilieVon(p)?.Konto;
        var zugZuerst = _stressZufall.NextDouble() < d.Zusammenbruch.ZugChance;
        var folge = zugZuerst ? ZusammenbruchZug(p) ?? ZusammenbruchKrankheit(konto, p) : ZusammenbruchKrankheit(konto, p) ?? ZusammenbruchZug(p);
        var vorher = p.Stresslage.Wert;
        p.Stresslage.Wert = Math.Min(vorher, d.NachZusammenbruch);
        p.Stresslage.Eintragen(new StressEintrag(Jahr, "Zusammenbruch", p.Stresslage.Wert - vorher));
        TalentAbzugNeu(p);
        if (konto is not null)
            Ereignis(konto, $"Zusammenbruch: {p.Vorname} {p.Familienname} hält es nicht mehr aus" + (folge is null ? "" : $" – {folge}") +
                            $". Der Stress sinkt auf {Zahlenformat.Ganz(p.Stresslage.Wert)}.", person: p, wichtigkeit: Meldungswichtigkeit.Hoch);
    }

    private string? ZusammenbruchZug(Person p)
    {
        var moeglich = StressDaten!.Zusammenbruch.Zuege.Select(Zug).OfType<Charakterzug>().Where(z => !p.Zuege.Contains(z)).ToList();
        if (moeglich.Count == 0) return null;
        var zug = moeglich[_stressZufall.Next(moeglich.Count)];
        p.ZugHinzufuegen(zug); // ohne eigene Meldung – die Meldung des Zusammenbruchs nennt ihn
        ZugErhalten?.Invoke(p, zug);
        return $"neuer Charakterzug {zug.Name}: {zug.Beschreibung.TrimEnd('.')}";
    }

    private string? ZusammenbruchKrankheit(Konto? konto, Person p)
    {
        if (konto is null || !_w.Gesundheit.IstAktiv || p.Krankheit is not null) return null;
        var moeglich = StressDaten!.Zusammenbruch.Krankheiten.Where(_w.Gesundheit.Daten.Krankheiten.ContainsKey).ToList();
        if (moeglich.Count == 0) return null;
        var name = moeglich[_stressZufall.Next(moeglich.Count)];
        return _w.Gesundheit.Erkranken(konto, p, name, stress: false, melden: false) && p.Krankheit is { } k ? $"krank: {k.Beschreibung}" : null;
    }

    /// <summary>Oberhaupt, Ehepartner und Erbe eines Hauses, soweit sie Stress haben (in dieser Reihenfolge).</summary>
    private List<Person> StressTraeger(Familie f) => Hauptpersonen(f).Where(HatStress).ToList();

    /// <summary>
    /// Oberhaupt, Ehepartner und Erbe eines Hauses, soweit sie leben (in dieser Reihenfolge, ohne Doppelte) – wer Lebensweise, Stress und
    /// Geheimnisse hat.
    /// </summary>
    public IReadOnlyList<Person> Hauptpersonen(Familie f)
    {
        var liste = new List<Person> { f.Oberhaupt };
        if (f.Ehepartner is { } e && !liste.Contains(e)) liste.Add(e);
        if (Erbe(f) is { } erbe && !liste.Contains(erbe)) liste.Add(erbe);
        return liste.Where(p => p.Lebt).ToList();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Taten und Befehle
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Ein Ereignis (Buße, Pranger, verfallene AP …) gegen den Charakter des Oberhaupts: Stress je passendem Eintrag in "stress.taten".</summary>
    internal void StressTat(Konto konto, string tat, double betrag = 0)
    {
        if (StressDaten is not { } d || Von(konto) is not { } f || !HatStress(f.Oberhaupt)) return;
        foreach (var (wert, text) in TatPosten(d, f.Oberhaupt, new[] { tat }, betrag).ToList()) // erst sammeln: ein Zusammenbruch gibt Züge dazu
            StressAendern(f.Oberhaupt, wert, text);
    }

    /// <summary>Die Einträge aus "stress.taten", die zu den Zügen der Person und einer dieser Taten passen.</summary>
    private static IEnumerable<(double Wert, string Text)> TatPosten(StressDaten d, Person p, IReadOnlyCollection<string> taten, double betrag)
    {
        foreach (var t in d.Taten)
            if (taten.Contains(t.Tat) && betrag + 1e-9 >= t.AbGulden && p.Zuege.FirstOrDefault(z => z.Kennung == t.Zug) is { } zug)
                yield return (t.Stress, $"{t.Name} ({zug.Name.ToLowerInvariant()})");
    }

    /// <summary>Die Namen, unter denen ein Befehl in "stress.taten" steht: Befehl, Befehl:Art und die Ereignisse, die er auslöst.</summary>
    private List<string> TatSchluessel(Konto konto, Befehl befehl)
    {
        var typ = befehl.GetType().Name;
        var liste = new List<string> { typ };
        var art = befehl switch
        {
            Aktivitaet a => a.Art,
            Beauftragen b => b.Art.ToString(),
            WohnhausAusbauen => _w.Wohnen.NaechsteStufe(konto)?.Name,
            WohnhausBauen wb => _w.Wohnen.Daten is { } wd && wb.Stufe >= 0 && wb.Stufe < wd.Stufen.Count ? wd.Stufen[wb.Stufe].Name : null,
            _ => null,
        };
        if (art is not null) liste.Add($"{typ}:{art}");
        var geschenk = befehl is Schenken || befehl is Aktivitaet g && g.Art == _w.Meinungen.GeschenkArt;
        if (geschenk) liste.Add("Geschenk");
        if (befehl is Schenken || geschenk && befehl is Aktivitaet { Mit: var mit } && mit.StartsWith("R:", StringComparison.Ordinal)) liste.Add("Bestechung");
        if (befehl is Bekehrungspredigt && _w.Glaube.IstAktiv && _w.Glaube.Stadtglaube is { } s && _w.Glaube.GlaubeVon(konto) is { } eigener && s != eigener)
            liste.Add("Ketzerei");
        if (GeheimnisTat(befehl) is { } tat) liste.Add(tat); // Erpressen bzw. Anklagen mit einem Geheimnis (Teil 3c)
        return liste;
    }

    /// <summary>
    /// Ein Geheimnis zu nutzen zählt wie die vorhandene Tat (Teil 3c): Erpressen um Gulden oder die Ratsstimme wie „Erpressen“, die Anzeige wie
    /// „Anklagen“ – für Stress und Lebensweise, ohne eigene Einträge in den Daten. Bloßstellen zählt nichts.
    /// </summary>
    internal static string? GeheimnisTat(Befehl befehl) => befehl is GeheimnisNutzen g
        ? g.Nutzung switch
        {
            Geheimnisse.Geheimnisnutzung.Gulden or Geheimnisse.Geheimnisnutzung.Ratsstimme => nameof(Erpressen),
            Geheimnisse.Geheimnisnutzung.Anzeige => nameof(Anklagen),
            _ => null,
        }
        : null;

    private static double Betrag(Befehl befehl) => befehl switch
    {
        Schenken s => s.Betrag,
        Aktivitaet a => a.Betrag,
        _ => 0,
    };

    /// <summary>
    /// Was dieser Befehl an Stress brächte, wenn er gelingt: Taten gegen die Züge des Oberhaupts und bei einer Aktivität Stress (das Oberhaupt
    /// mag sie nicht) oder Abbau (Ventil) für das Oberhaupt und ein Gegenüber, das selbst Stress hat. Ventile, die in diesem Jahr schon wirkten, zählen nicht.
    /// </summary>
    public IReadOnlyList<StressPosten> StressVorschau(Konto konto, Befehl befehl)
    {
        var liste = new List<StressPosten>();
        if (befehl is BetriebsBefehl || StressDaten is not { } d) return liste;
        if (!d.KenntBefehl(befehl.GetType().Name) && befehl is not (Aktivitaet or Schenken or Bekehrungspredigt or GeheimnisNutzen)) return liste; // schnell: bringt nie Stress
        if (Von(konto) is not { } f || !HatStress(f.Oberhaupt)) return liste;
        var o = f.Oberhaupt;
        var schluessel = TatSchluessel(konto, befehl);
        foreach (var (wert, text) in TatPosten(d, o, schluessel, Betrag(befehl)))
            liste.Add(new StressPosten(o, wert, text, null));
        if (befehl is Aktivitaet a && _w.Meinungen.Daten?.Aktivitaeten.GetValueOrDefault(a.Art) is { Geschenk: false } ad)
        {
            AktivitaetPosten(d, o, a.Art, ad, liste, handelnd: true);
            if (_w.Meinungen.Charakter(a.Mit) is Person mit && mit != o && HatStress(mit))
                AktivitaetPosten(d, mit, a.Art, ad, liste, handelnd: false);
        }
        return liste;
    }

    /// <summary>
    /// Was eine Aktivität dieser Person an Stress bringt: mag sie sie nicht, +<see cref="StressDaten.MagNicht"/> – nur, wer sie selbst unternimmt
    /// (das Gegenüber hat sie nicht gewählt; sonst ließe sich einem Rivalen gezielt Stress machen, Befund der Prüfung); ist sie ein Ventil und hat
    /// die Person Stress, Abbau (× 1,5, wenn sie sie mag). Ein Ventil ohne Stress verbraucht die Art des Jahres nicht.
    /// </summary>
    private static void AktivitaetPosten(StressDaten d, Person p, string art, AktivitaetDaten ad, List<StressPosten> liste, bool handelnd)
    {
        if (p.Stresslage.VentilGenutzt(art)) return;
        if (p.Zuege.FirstOrDefault(z => ad.MagNicht.Contains(z.Kennung)) is { } nicht)
        {
            if (handelnd && d.MagNicht > 0) liste.Add(new StressPosten(p, d.MagNicht, $"{ad.Name} – mag es nicht ({nicht.Name.ToLowerInvariant()})", art));
            return;
        }
        if (p.Stresslage.Wert <= 0 || !d.Ventile.TryGetValue(art, out var abbau)) return;
        var mag = p.Zuege.FirstOrDefault(z => ad.Mag.Contains(z.Kennung));
        liste.Add(new StressPosten(p, -abbau * (mag is null ? 1 : d.MagFaktor), mag is null ? ad.Name : $"{ad.Name} – mag es ({mag.Name.ToLowerInvariant()})", art));
    }

    /// <summary>Was ein Befehl dem Oberhaupt an Stress brächte (Summe; negativ = Abbau) – für die KI („meidet, was Stress bringt“) und die Oberfläche.</summary>
    public double StressDurch(Konto konto, Befehl befehl)
    {
        var o = Von(konto)?.Oberhaupt;
        return o is null ? 0 : StressVorschau(konto, befehl).Where(x => x.Person == o).Sum(x => x.Wert);
    }

    /// <summary>Nach einem gelungenen Befehl (aus Stadtwirtschaft.Ausfuehren): die vorher berechneten Posten buchen.</summary>
    internal void StressBuchen(IReadOnlyList<StressPosten> posten)
    {
        foreach (var x in posten)
        {
            if (x.Ventil is { } art) x.Person.Stresslage.VentilMerken(art);
            StressAendern(x.Person, x.Wert, x.Text);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Ereignisse
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Trauer: Ein Angehöriger ist gestorben (vor dem Erbfall, solange Ehepartner und Erbe noch gelten).</summary>
    private void StressTrauer(Familie f, Person tot, string wie = "Trauer um")
    {
        if (StressDaten is not { } d) return;
        foreach (var b in StressTraeger(f).Where(b => b != tot))
        {
            var ehe = b == f.Oberhaupt && tot == f.Ehepartner || b == f.Ehepartner && tot == f.Oberhaupt;
            var (wert, wer) = ehe ? (d.Trauer.Ehepartner, tot.Ehepartnertitel)
                : tot.IstKindVon(b) ? (d.Trauer.Kind, tot.SohnOderTochter)
                : b.IstKindVon(tot) ? (d.Trauer.Elternteil, tot.Geschlecht == Geschlecht.Frau ? "Mutter" : "Vater")
                : tot.Vater is { } v && v == b.Vater || tot.Mutter is { } m && m == b.Mutter ? (d.Trauer.Geschwister, tot.Geschlecht == Geschlecht.Frau ? "Schwester" : "Bruder")
                : (d.Trauer.Haus, (string?)null); // jeder andere aus dem Haus (Enkel, Schwiegertochter, zweite Frau des Vaters …)
            var text = wer is not null ? $"{wie} {wer} {tot.Vorname}" : wie == "Trauer um" ? $"Tod im Haus: {tot.Vorname}" : $"{wie} {tot.Vorname}";
            if (wert > 0) StressAendern(b, wert, text);
        }
    }

    /// <summary>
    /// Nach jeder Meldung (aus Stadtwirtschaft.Melden): Was das Haus von außen trifft, steht schon als öffentliche Meldung mit Art und Haus da –
    /// Einbruch mit Beute, Brand (Anschlag, Sabotage, Blitz, Funkenflug), ausgeraubter Karren oder Schiff. Nur ab Wichtigkeit Hoch: Vereitelte Taten
    /// und der Einbruch ins leere Lager melden „Normal“; vertrauliche Nachrichten an den Auftraggeber zählen nicht. So braucht keine dieser Stellen
    /// einen eigenen Haken. Der Ort steht dabei: der Betrieb, sonst das Wohnhaus.
    /// </summary>
    internal void StressNachMeldung(Meldung m)
    {
        if (StressDaten is not { } d || m.Vertraulich || m.Wichtigkeit < Meldungswichtigkeit.Hoch || m.Haus is not int nr || nr < 0 || nr >= _w.Konten.Count) return;
        var (wert, text) = m.Art switch
        {
            Meldungsart.Diebstahl => (d.Haus.Diebstahl, "Bestohlen"),
            Meldungsart.Feuer => (d.Haus.Brand, "Brand oder Anschlag"),
            Meldungsart.Ueberfall => (d.Haus.Ueberfall, "Überfallen"),
            _ => (0.0, ""),
        };
        var ort = m.Ort switch
        {
            Meldungsort.Betrieb => m.Ortsname,
            Meldungsort.Wohnhaus or Meldungsort.Gebaeude => "Wohnhaus", // ein Gebäude der Karte ohne Betrieb, das dem Haus gehört, ist sein Wohnhaus
            _ => null,
        };
        if (wert > 0 && Von(_w.Konten[nr]) is { } f) StressAendern(f.Oberhaupt, wert, ort is not null ? $"{text} ({ort})" : text);
    }

    /// <summary>Eine Niederlage im Rat (Ratssitzung): verlorene Wahl, abgelehnter Antrag – beim Oberhaupt.</summary>
    internal void StressRat(Konto konto, Func<StressRatDaten, double> wert, string text)
    {
        if (StressDaten is { } d && Von(konto) is { } f) StressAendern(f.Oberhaupt, wert(d.Rat), text);
    }

    /// <summary>Streit im Haus (Meinungswesen, am Jahresende): Oberhaupt und Erbe.</summary>
    internal void StressStreit(Familie f, Person erbe)
    {
        if (StressDaten is not { } d) return;
        StressAendern(f.Oberhaupt, d.Haus.Streit, $"Streit mit {erbe.Vorname}");
        StressAendern(erbe, d.Haus.Streit, $"Streit mit {f.Oberhaupt.Vorname}");
    }

    /// <summary>Vor Gericht (Stadtgericht): das Oberhaupt des beklagten Hauses.</summary>
    internal void StressGericht(Konto konto, Func<StressGerichtDaten, double> wert, string text)
    {
        if (StressDaten is { } d && Von(konto) is { } f) StressAendern(f.Oberhaupt, wert(d.Gericht), text);
    }

    /// <summary>Eine Person ist erkrankt (Gesundheitswesen; nicht beim Zusammenbruch).</summary>
    internal void StressErkrankt(Person p, string krankheit)
    {
        if (StressDaten is { } d) StressAendern(p, d.Krankheit.Erkrankt, $"Erkrankt: {krankheit}");
    }

    /// <summary>Die Pest ist im Haus (einmal im Pestjahr): alle Stressträger des Hauses.</summary>
    internal void StressPestImHaus(Konto konto)
    {
        if (StressDaten is not { } d || Von(konto) is not { } f) return;
        foreach (var p in StressTraeger(f)) StressAendern(p, d.Krankheit.Pest, "Die Pest im Haus");
    }

    /// <summary>
    /// Am Jahresende (nach der Erfahrung der Lebensweise, vor den Todesfällen): die Quellen des Jahres beim Oberhaupt (Verlust, Bargeld im Minus,
    /// Pranger, verfallene Aktionspunkte), dann klingt der Stress ab – bei jedem Lebenden, der welchen hat. Die Ventile sind wieder frei.
    /// </summary>
    internal void StressImJahreswechsel(Familie f)
    {
        if (StressDaten is not { } d) return;
        var konto = f.Konto;
        var o = f.Oberhaupt;
        if (konto.Vorjahr is { } bilanz && bilanz.Gewinn < 0) StressAendern(o, d.Haus.JahrImMinus, "Ein Jahr mit Verlust");
        if (konto.Vermoegen < 0) StressAendern(o, d.Haus.BargeldNegativ, "Bargeld im Minus");
        if (_w.Justiz.IstAktiv && _w.Justiz.PrangerBis(konto) is not null) StressTat(konto, "Pranger");
        if (_w.VerfallendeAktionspunkte(konto) > 0) StressTat(konto, "ApVerfallen");
        // Sorgen aus den Betrieben: jeder Betrieb, der das ganze Jahr lief und mit Verlust schloss; Ware einer Werkstatt im Preis verfallen (einmal)
        var eigene = _w.Betriebe.Where(b => b.Konto == konto).ToList();
        if (d.Haus.BetriebVerlust > 0)
            foreach (var b in eigene.Where(b => b.Vorjahr is { Ganzjaehrig: true } r && r.Gewinn < 0))
                StressAendern(o, d.Haus.BetriebVerlust, $"Verlust in {b.Name}");
        if (d.Haus.Preisverfall > 0 && eigene.Where(b => b.IstWerkstatt).Select(b => b.Produkt).Distinct().Where(_w.Daten.Waren.ContainsKey)
                .Select(ware => (Ware: ware, Faktor: _w.Markt[ware].Preisfaktor)).Where(x => x.Faktor < d.Haus.PreisverfallAb)
                .OrderBy(x => x.Faktor).ThenBy(x => x.Ware, StringComparer.Ordinal).FirstOrDefault() is { Ware: { } verfallen })
            StressAendern(o, d.Haus.Preisverfall, $"Preise verfallen ({verfallen})");
        // Geheimnisse (Teil 3c): Wer eines hütet, trägt schwer daran – Ehrliche und Fromme schwerer.
        foreach (var p in StressTraeger(f))
            if (_w.Geheimnisse.StressJeJahr(p) is var geheim and > 0)
                StressAendern(p, geheim, _w.Geheimnisse.GeheimeAnzahl(p) == 1 ? "Ein Geheimnis hüten" : "Geheimnisse hüten");

        foreach (var p in f.Lebende.Where(p => p.Stresslage.Wert > 0).ToList())
        {
            var glueck = GlueckEhe(f, p);
            var ab = d.Abklingen + p.Zuege.Sum(z => d.ZugAbklingen.GetValueOrDefault(z.Kennung)) + (glueck ? d.GlueckEhe : 0);
            if (ab > 0) StressAendern(p, -ab, glueck ? "Abklingen (glückliche Ehe)" : "Abklingen", nurTraeger: false);
        }
        foreach (var p in f.Lebende) p.Stresslage.JahrAbschliessen(Jahr);
    }

    /// <summary>Denkt der Ehepartner mindestens <see cref="StressDaten.GlueckEheAb"/> gut über die Person?</summary>
    private bool GlueckEhe(Familie f, Person p)
    {
        var partner = p == f.Oberhaupt ? f.Ehepartner : p == f.Ehepartner ? f.Oberhaupt : null;
        return partner is { Lebt: true } && _w.Meinungen.IstAktiv && _w.Meinungen.Meinung(partner, p) >= StressDaten!.GlueckEheAb - 1e-9;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Speichern
    // ═════════════════════════════════════════════════════════════════════════

    private StressStand? StressSichern(Person p) =>
        !HatStresswesen || p.Stresslage.IstLeer ? null : new StressStand
        {
            Wert = p.Stresslage.Wert,
            Eintraege = p.Stresslage.Eintraege.Select(e => new StressEintragStand { Jahr = e.Jahr, Text = e.Text, Wert = e.Wert }).ToList(),
            Ventile = p.Stresslage.Ventile.ToList(),
        };

    private void StressWiederherstellen(Person p, StressStand? s)
    {
        if (s is not null)
        {
            p.Stresslage.Wert = s.Wert;
            foreach (var e in s.Eintraege) p.Stresslage.Eintragen(new StressEintrag(e.Jahr, e.Text, e.Wert));
            foreach (var v in s.Ventile) p.Stresslage.VentilMerken(v);
        }
        TalentAbzugNeu(p);
    }
}
