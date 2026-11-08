using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Dynastie;

/// <summary>
/// Was ein Knoten der Lebensweise bewirkt (Charaktersystem Teil 3a). Mehrere Knoten wirken zusammen: Faktoren werden multipliziert,
/// Zuschläge addiert (<see cref="Mit"/>). Werkstatt-Wirkungen stehen nicht hier, sondern als <see cref="EinrichtungWirkung"/> im Knoten
/// (<see cref="KnotenDaten.Einrichtung"/>) – sie zählen in jedem Betrieb des Hauses wie ein Stück Einrichtung.
///
/// Fast alle Wirkungen gelten für das Haus, solange der Besitzer der Knoten Oberhaupt ist. Persönlich, also immer für die Person
/// selbst, sind nur <see cref="MeinungOberhaeupter"/>, <see cref="MeinungAlle"/> und <see cref="KrankheitFaktor"/>.
/// </summary>
public sealed class LebenswegWirkung
{
    // ── Verhandeln ──────────────────────────────────────────────────────
    /// <summary>Mehr Erlös am Markt (0,01 = +1 %), zählt zum Talent Verhandeln dazu.</summary>
    public double Erloes { get; init; }
    /// <summary>Weniger Zins auf eigene Kredite (0,01 = −1 Prozentpunkt).</summary>
    public double ZinsMinus { get; init; }
    /// <summary>Faktor auf die Haushaltskosten der Familie.</summary>
    public double HaushaltFaktor { get; init; } = 1;
    /// <summary>Faktor auf die Marktsteuer eigener Verkäufe (nicht zusammen mit dem Erlass des Marktmeisters).</summary>
    public double MarktsteuerFaktor { get; init; } = 1;
    /// <summary>Faktor auf die Wirkung eigener Geschenke.</summary>
    public double GeschenkFaktor { get; init; } = 1;
    /// <summary>Persönlich: So viel mehr Meinung haben die Oberhäupter anderer Häuser über den Besitzer.</summary>
    public double MeinungOberhaeupter { get; init; }
    /// <summary>So viel mehr Ansehen im Rat.</summary>
    public double Ansehen { get; init; }

    // ── Handwerk ────────────────────────────────────────────────────────
    /// <summary>Faktor auf die Kosten für Bauen, Ausbau und Einrichtung.</summary>
    public double BauFaktor { get; init; } = 1;
    /// <summary>Faktor auf den Unterhalt des Wohnhauses (für die Betriebe: <see cref="EinrichtungWirkung.UnterhaltFaktor"/> im Knoten).</summary>
    public double UnterhaltFaktor { get; init; } = 1;

    // ── Nacht und Nebel ─────────────────────────────────────────────────
    /// <summary>Mehr Erfolg eigener Gaunerstücke (0,03 = +3 Prozentpunkte), zählt zum Talent dazu.</summary>
    public double NachtErfolg { get; init; }
    /// <summary>Faktor auf die Spuren (Zeugen) eigener Gaunerstücke.</summary>
    public double SpurenFaktor { get; init; } = 1;
    /// <summary>Faktor auf den Erlös des Diebesguts.</summary>
    public double HehlerFaktor { get; init; } = 1;
    /// <summary>Faktor auf die Beute der eigenen Taschendiebe am Markt (Leise Sohlen 1,05).</summary>
    public double BeuteFaktor { get; init; } = 1;
    /// <summary>Mehr Erfolg eigener Spitzel (0,1 = +10 Prozentpunkte).</summary>
    public double SpitzelBonus { get; init; }
    /// <summary>Chance je fremdem Geheimnis und Jahr, es ohne Spitzel zu erfahren (wirkt mit den Geheimnissen, Teil 3c).</summary>
    public double Geruechte { get; init; }
    /// <summary>Faktor auf die Chance, dass eigene Erpresserbriefe eine Spur hinterlassen.</summary>
    public double ErpressungSpurFaktor { get; init; } = 1;
    /// <summary>Faktor auf die Entdeckung der Geheimnisse der eigenen Familie (wirkt mit den Geheimnissen, Teil 3c).</summary>
    public double GeheimnisSchutz { get; init; } = 1;
    /// <summary>Faktor auf die Chance fremder Spitzel, Spuren des Hauses aufzudecken.</summary>
    public double FremdeSpitzelFaktor { get; init; } = 1;

    // ── Kampf ───────────────────────────────────────────────────────────
    /// <summary>Mehr Schutz der eigenen Karren (0,05 = +5 Prozentpunkte), zählt zum Talent dazu.</summary>
    public double KarrenSchutz { get; init; }
    /// <summary>Faktor auf Überfälle gegen eigene Karren.</summary>
    public double UeberfallFaktor { get; init; } = 1;
    /// <summary>Stärkere eigene Wegelagerer (0,1 = +10 Prozentpunkte), zählt zum Talent dazu.</summary>
    public double WegelagererBonus { get; init; }
    /// <summary>Faktor auf Einbruch und Brand am Wohnhaus.</summary>
    public double WohnhausFaktor { get; init; } = 1;
    /// <summary>Persönlich: Faktor auf die eigene Krankheitsgefahr.</summary>
    public double KrankheitFaktor { get; init; } = 1;
    /// <summary>Weniger Gefahr bei der Jagd (0,05 = −5 Prozentpunkte).</summary>
    public double JagdGefahrMinus { get; init; }
    /// <summary>Jagd schon ab diesem Stand (statt dem der Aktivität).</summary>
    public string? JagdAbStand { get; init; }
    /// <summary>So viele AP weniger kostet die Jagd (höchstens bis 1).</summary>
    public int JagdApMinus { get; init; }

    // ── Rhetorik ────────────────────────────────────────────────────────
    /// <summary>Mehr Wahlpunkte im Rat.</summary>
    public double Wahlpunkte { get; init; }
    /// <summary>So viel niedriger liegen die Meinungsschwellen eigener Anträge.</summary>
    public double AntragSchwelleMinus { get; init; }
    /// <summary>Mehr Überzeugung jedes Richters zugunsten des Hauses.</summary>
    public double Plaedoyer { get; init; }
    /// <summary>Faktor auf die Gerichtsgebühr eigener Klagen.</summary>
    public double GebuehrFaktor { get; init; } = 1;
    /// <summary>Faktor auf die Entschädigung, die das Haus als freigesprochener Beklagter bekommt.</summary>
    public double EntschaedigungFaktor { get; init; } = 1;
    /// <summary>Faktor auf die Buße bei einer Verurteilung.</summary>
    public double BusseFaktor { get; init; } = 1;
    /// <summary>So viele Jahre kürzer steht das Haus am Pranger.</summary>
    public int PrangerMinus { get; init; }
    /// <summary>Faktor auf die Wirkung eigener Aktivitäten (Gastmahl, Zechen, Messe, Brettspiel, Jagd).</summary>
    public double AktivitaetFaktor { get; init; } = 1;
    /// <summary>Faktor auf die Zuneigung je Gulden beim Werben.</summary>
    public double WerbeFaktor { get; init; } = 1;
    /// <summary>Persönlich: So viel mehr Meinung haben alle wichtigen Personen über den Besitzer.</summary>
    public double MeinungAlle { get; init; }

    /// <summary>Ohne Wirkung.</summary>
    public static readonly LebenswegWirkung Keine = new();

    /// <summary>Beide Wirkungen zusammen: Faktoren multipliziert, Zuschläge addiert, beim Stand der niedrigere Eintrag des ersten.</summary>
    public LebenswegWirkung Mit(LebenswegWirkung b) => new()
    {
        Erloes = Erloes + b.Erloes, ZinsMinus = ZinsMinus + b.ZinsMinus, HaushaltFaktor = HaushaltFaktor * b.HaushaltFaktor,
        MarktsteuerFaktor = MarktsteuerFaktor * b.MarktsteuerFaktor, GeschenkFaktor = GeschenkFaktor * b.GeschenkFaktor,
        MeinungOberhaeupter = MeinungOberhaeupter + b.MeinungOberhaeupter, Ansehen = Ansehen + b.Ansehen,
        BauFaktor = BauFaktor * b.BauFaktor, UnterhaltFaktor = UnterhaltFaktor * b.UnterhaltFaktor,
        NachtErfolg = NachtErfolg + b.NachtErfolg, SpurenFaktor = SpurenFaktor * b.SpurenFaktor, HehlerFaktor = HehlerFaktor * b.HehlerFaktor,
        BeuteFaktor = BeuteFaktor * b.BeuteFaktor,
        SpitzelBonus = SpitzelBonus + b.SpitzelBonus, Geruechte = Geruechte + b.Geruechte,
        ErpressungSpurFaktor = ErpressungSpurFaktor * b.ErpressungSpurFaktor, GeheimnisSchutz = GeheimnisSchutz * b.GeheimnisSchutz,
        FremdeSpitzelFaktor = FremdeSpitzelFaktor * b.FremdeSpitzelFaktor,
        KarrenSchutz = KarrenSchutz + b.KarrenSchutz, UeberfallFaktor = UeberfallFaktor * b.UeberfallFaktor,
        WegelagererBonus = WegelagererBonus + b.WegelagererBonus, WohnhausFaktor = WohnhausFaktor * b.WohnhausFaktor,
        KrankheitFaktor = KrankheitFaktor * b.KrankheitFaktor, JagdGefahrMinus = JagdGefahrMinus + b.JagdGefahrMinus,
        JagdAbStand = JagdAbStand ?? b.JagdAbStand, JagdApMinus = JagdApMinus + b.JagdApMinus,
        Wahlpunkte = Wahlpunkte + b.Wahlpunkte, AntragSchwelleMinus = AntragSchwelleMinus + b.AntragSchwelleMinus,
        Plaedoyer = Plaedoyer + b.Plaedoyer, GebuehrFaktor = GebuehrFaktor * b.GebuehrFaktor,
        EntschaedigungFaktor = EntschaedigungFaktor * b.EntschaedigungFaktor, BusseFaktor = BusseFaktor * b.BusseFaktor,
        PrangerMinus = PrangerMinus + b.PrangerMinus, AktivitaetFaktor = AktivitaetFaktor * b.AktivitaetFaktor,
        WerbeFaktor = WerbeFaktor * b.WerbeFaktor, MeinungAlle = MeinungAlle + b.MeinungAlle,
    };

    internal void Pruefen(List<string> f, string wo, Func<string, int> standStufe)
    {
        foreach (var (name, faktor) in new[] { ("haushaltFaktor", HaushaltFaktor), ("marktsteuerFaktor", MarktsteuerFaktor), ("bauFaktor", BauFaktor),
                     ("unterhaltFaktor", UnterhaltFaktor), ("spurenFaktor", SpurenFaktor), ("erpressungSpurFaktor", ErpressungSpurFaktor),
                     ("geheimnisSchutz", GeheimnisSchutz), ("fremdeSpitzelFaktor", FremdeSpitzelFaktor), ("ueberfallFaktor", UeberfallFaktor),
                     ("wohnhausFaktor", WohnhausFaktor), ("krankheitFaktor", KrankheitFaktor), ("gebuehrFaktor", GebuehrFaktor), ("busseFaktor", BusseFaktor) })
            if (faktor is < 0 or > 1) f.Add($"{wo}.{name} muss zwischen 0 und 1 liegen.");
        foreach (var (name, faktor) in new[] { ("geschenkFaktor", GeschenkFaktor), ("hehlerFaktor", HehlerFaktor), ("beuteFaktor", BeuteFaktor), ("entschaedigungFaktor", EntschaedigungFaktor),
                     ("aktivitaetFaktor", AktivitaetFaktor), ("werbeFaktor", WerbeFaktor) })
            if (faktor is < 1 or > 3) f.Add($"{wo}.{name} muss zwischen 1 und 3 liegen.");
        foreach (var (name, wert) in new[] { ("erloes", Erloes), ("zinsMinus", ZinsMinus), ("nachtErfolg", NachtErfolg), ("spitzelBonus", SpitzelBonus),
                     ("geruechte", Geruechte), ("karrenSchutz", KarrenSchutz), ("wegelagererBonus", WegelagererBonus), ("jagdGefahrMinus", JagdGefahrMinus) })
            if (wert is < 0 or > 0.5) f.Add($"{wo}.{name} muss zwischen 0 und 0,5 liegen.");
        foreach (var (name, wert) in new[] { ("meinungOberhaeupter", MeinungOberhaeupter), ("ansehen", Ansehen), ("wahlpunkte", Wahlpunkte),
                     ("antragSchwelleMinus", AntragSchwelleMinus), ("plaedoyer", Plaedoyer), ("meinungAlle", MeinungAlle) })
            if (wert is < 0 or > 30) f.Add($"{wo}.{name} muss zwischen 0 und 30 liegen.");
        if (JagdApMinus is < 0 or > 5 || PrangerMinus is < 0 or > 5) f.Add($"{wo}: jagdApMinus und prangerMinus liegen zwischen 0 und 5.");
        if (JagdAbStand is { } st && standStufe(st) < 0) f.Add($"{wo}.jagdAbStand: Den Stand „{st}“ gibt es nicht.");
    }
}

/// <summary>Ein Knoten im Baum eines Pfads (Abschnitt "lebensweise.pfade.&lt;Talent&gt;.knoten", Schlüssel = Kennung).</summary>
public sealed class KnotenDaten
{
    public string Name { get; init; } = "";

    /// <summary>Spalte im Baum (eine der <see cref="PfadDaten.Spalten"/>).</summary>
    public string Spalte { get; init; } = "";

    /// <summary>Reihe 1–3. Ab Reihe 2 braucht der Knoten den Knoten darüber in derselben Spalte.</summary>
    public int Reihe { get; init; } = 1;

    /// <summary>Die Wirkung in Worten („Erlös am Markt +1 %“).</summary>
    public string Beschreibung { get; init; } = "";

    public LebenswegWirkung Wirkung { get; init; } = new();

    /// <summary>Werkstatt-Wirkung: zählt in jedem Betrieb des Hauses wie ein Stück Einrichtung (null = keine).</summary>
    public EinrichtungWirkung? Einrichtung { get; init; }

    /// <summary>Die Werkstatt-Wirkung gilt nur in Betrieben ab dieser Gebäudestufe (Meisterstück: 3).</summary>
    public int AbStufe { get; init; } = 1;

    /// <summary>Wie gern ein KI-Haus diesen Knoten nimmt (höher zuerst; bei Gleichstand die niedrigere Reihe, dann die Spalte von links).</summary>
    public double Ki { get; init; } = 1;
}

/// <summary>Ein Pfad der Lebensweise – einer je Talent (Abschnitt "lebensweise.pfade", Schlüssel = Talent).</summary>
public sealed class PfadDaten
{
    /// <summary>„Pfad des Kaufmanns“.</summary>
    public string Name { get; init; } = "";
    public string Beschreibung { get; init; } = "";

    /// <summary>Die drei Spalten des Baums von links nach rechts.</summary>
    public List<string> Spalten { get; init; } = new();

    /// <summary>Züge, die schneller lernen lassen (je Zug × <see cref="LebensweiseDaten.ZugPassend"/>).</summary>
    public List<string> Passend { get; init; } = new();

    /// <summary>Züge, die bremsen (je Zug × <see cref="LebensweiseDaten.ZugHemmend"/>).</summary>
    public List<string> Hemmend { get; init; } = new();

    /// <summary>
    /// Handlungen, die als Taten dieses Pfads zählen: Name des Befehls („KreditAufnehmen“) oder Befehl und Art
    /// („Aktivitaet:Jagd“, „Beauftragen:Ueberfall“). Je ausgegebenem AP gibt es <see cref="LebensweiseDaten.JeAp"/> Erfahrung.
    /// </summary>
    public List<string> Taten { get; init; } = new();

    public Dictionary<string, KnotenDaten> Knoten { get; init; } = new();
}

/// <summary>
/// Die Lebensweise (Abschnitt "lebensweise" in wirtschaft.json, ab Datenversion 73, Charaktersystem Teil 3a). Fehlt der Abschnitt,
/// gibt es keine Lebensweise (alte Spielstände bringen ihre Daten mit).
///
/// Erfahrung je Jahr (gebucht am Jahresende, nur Oberhaupt, Erbe und Ehepartner):
/// <code>
/// (grund + jeStern × Sterne im Talent des Pfads) × Anteil + Taten
///   × zugPassend je passendem Zug × zugHemmend je hemmendem Zug × Gelehrt × Einrichtung des Wohnhauses (Bibliothek)
/// Anteil: Oberhaupt 1, Erbe und Ehepartner anteilFamilie (0,5). Taten: jeAp je AP für eine Handlung des Pfads (nur das Oberhaupt, sofort gebucht).
/// </code>
/// Ein Punkt kostet <see cref="Kosten"/>[n] Erfahrung im selben Pfad; mit ihm nimmt man einen Knoten.
/// </summary>
public sealed class LebensweiseDaten
{
    public double Grund { get; init; } = 30;
    public double JeStern { get; init; } = 5;
    public double AnteilFamilie { get; init; } = 0.5;
    public double JeAp { get; init; } = 10;
    public double ZugPassend { get; init; } = 1.2;
    public double ZugHemmend { get; init; } = 0.8;

    /// <summary>Faktoren aus erworbenen oder sonstigen Zügen (Gelehrt 1,1).</summary>
    public Dictionary<string, double> Zugfaktoren { get; init; } = new();

    /// <summary>Faktoren aus der Einrichtung des Wohnhauses (Bibliothek 1,1).</summary>
    public Dictionary<string, double> Wohnhaus { get; init; } = new();

    /// <summary>Anteil der Erfahrung des Verstorbenen in seinem Pfad, den der Erbe als Vermächtnis bekommt.</summary>
    public double Vermaechtnis { get; init; } = 0.2;

    /// <summary>Was der 1., 2., … Punkt eines Pfads an Erfahrung kostet (je Pfad neu gezählt).</summary>
    public List<double> Kosten { get; init; } = new();

    public Dictionary<Talent, PfadDaten> Pfade { get; init; } = new();

    public bool IstAktiv => Pfade.Count > 0 && Kosten.Count > 0;

    /// <summary>Alle Knoten mit Pfad, Kennung und Daten – fest geordnet nach Pfad (Talent), Spalte und Reihe.</summary>
    public IEnumerable<(Talent Pfad, string Kennung, KnotenDaten Daten)> AlleKnoten =>
        TalentTexte.Alle.Where(Pfade.ContainsKey).SelectMany(t => Pfade[t].Knoten
            .OrderBy(k => Pfade[t].Spalten.IndexOf(k.Value.Spalte)).ThenBy(k => k.Value.Reihe).ThenBy(k => k.Key, StringComparer.Ordinal)
            .Select(k => (t, k.Key, k.Value)));

    /// <summary>Der Knoten mit dieser Kennung (null = gibt es nicht).</summary>
    public (Talent Pfad, KnotenDaten Daten)? Knoten(string kennung)
    {
        foreach (var t in TalentTexte.Alle)
            if (Pfade.TryGetValue(t, out var p) && p.Knoten.TryGetValue(kennung, out var k)) return (t, k);
        return null;
    }

    /// <summary>Die Voraussetzung eines Knotens: der Knoten eine Reihe darüber in derselben Spalte (null in Reihe 1).</summary>
    public string? Voraussetzung(Talent pfad, KnotenDaten k) =>
        k.Reihe <= 1 || !Pfade.TryGetValue(pfad, out var p) ? null
            : p.Knoten.Where(x => x.Value.Spalte == k.Spalte && x.Value.Reihe == k.Reihe - 1).Select(x => x.Key).FirstOrDefault();

    /// <summary>Erfahrung, die man für <paramref name="punkte"/> Punkte in einem Pfad insgesamt braucht.</summary>
    public double ErfahrungFuer(int punkte)
    {
        var summe = 0.0;
        for (var i = 0; i < punkte; i++) summe += Kosten[Math.Min(i, Kosten.Count - 1)];
        return summe;
    }

    /// <summary>So viele Punkte bringt diese Erfahrung in einem Pfad (höchstens so viele, wie der Pfad Knoten hat).</summary>
    public int PunkteAus(Talent pfad, double erfahrung)
    {
        var hoechstens = Pfade.TryGetValue(pfad, out var p) ? p.Knoten.Count : 0;
        var n = 0;
        while (n < hoechstens && erfahrung + 1e-9 >= ErfahrungFuer(n + 1)) n++;
        return n;
    }

    internal void Pruefen(List<string> f, Func<string, int> standStufe, ICollection<string>? zuege, IEnumerable<string> befehle)
    {
        const string q = "lebensweise";
        if (!IstAktiv) return;
        if (Grund < 0 || JeStern < 0 || JeAp < 0) f.Add($"{q}: grund, jeStern und jeAp dürfen nicht negativ sein.");
        if (AnteilFamilie is < 0 or > 1 || Vermaechtnis is < 0 or > 1) f.Add($"{q}: anteilFamilie und vermaechtnis liegen zwischen 0 und 1.");
        if (ZugPassend < 1 || ZugHemmend is <= 0 or > 1) f.Add($"{q}: zugPassend ≥ 1, zugHemmend zwischen 0 und 1.");
        if (Kosten.Any(k => k <= 0)) f.Add($"{q}.kosten: Jeder Punkt kostet mehr als 0.");
        foreach (var (z, faktor) in Zugfaktoren)
        {
            if (zuege is not null && !zuege.Contains(z)) f.Add($"{q}.zugfaktoren: Den Charakterzug „{z}“ gibt es nicht.");
            if (faktor <= 0) f.Add($"{q}.zugfaktoren.{z} muss größer als 0 sein.");
        }
        foreach (var (e, faktor) in Wohnhaus)
            if (faktor <= 0) f.Add($"{q}.wohnhaus.{e} muss größer als 0 sein.");
        var befehlsnamen = befehle.ToHashSet(StringComparer.Ordinal);
        var kennungen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (t, p) in Pfade)
        {
            var wo = $"{q}.pfade.{t}";
            if (string.IsNullOrWhiteSpace(p.Name)) f.Add($"{wo}: name fehlt.");
            if (p.Spalten.Count == 0 || p.Spalten.Distinct().Count() != p.Spalten.Count) f.Add($"{wo}.spalten: mindestens eine, keine doppelt.");
            foreach (var z in p.Passend.Concat(p.Hemmend).Where(z => zuege is not null && !zuege.Contains(z)))
                f.Add($"{wo}: Den Charakterzug „{z}“ gibt es nicht.");
            foreach (var tat in p.Taten)
                if (!befehlsnamen.Contains(tat.Split(':')[0])) f.Add($"{wo}.taten: Einen Befehl „{tat}“ gibt es nicht.");
            foreach (var (kennung, k) in p.Knoten)
            {
                var kw = $"{wo}.knoten.{kennung}";
                if (!kennungen.Add(kennung)) f.Add($"{kw}: Die Kennung gibt es in mehreren Pfaden.");
                if (string.IsNullOrWhiteSpace(k.Name)) f.Add($"{kw}: name fehlt.");
                if (!p.Spalten.Contains(k.Spalte)) f.Add($"{kw}: Die Spalte „{k.Spalte}“ gibt es im Pfad nicht.");
                if (k.Reihe < 1) f.Add($"{kw}: reihe ab 1.");
                if (k.Reihe > 1 && Voraussetzung(t, k) is null) f.Add($"{kw}: In der Reihe darüber fehlt ein Knoten derselben Spalte.");
                if (p.Knoten.Count(x => x.Value.Spalte == k.Spalte && x.Value.Reihe == k.Reihe) > 1) f.Add($"{kw}: Spalte und Reihe sind doppelt belegt.");
                if (k.AbStufe is < 1 or > 3) f.Add($"{kw}.abStufe liegt zwischen 1 und 3.");
                if (k.Ki < 0) f.Add($"{kw}.ki darf nicht negativ sein.");
                k.Wirkung.Pruefen(f, kw + ".wirkung", standStufe);
            }
        }
    }
}
