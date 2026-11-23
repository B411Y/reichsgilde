using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Karren selbst fahren (Nutzer 23.11.2026, wie in der „Gilde 1“): Der Spieler schickt seine Karren selbst zum Markt oder zu einem eigenen
/// Betrieb derselben Stadt, handelt am Markt und lädt an den Betrieben – jede Fahrt einzeln, ohne Verwalter. Die KI-Häuser fahren immer
/// automatisch (<see cref="FaehrtAutomatisch"/>). Ob die Spieler einer Partie selbst fahren, steht in <see cref="KarrenAutomatisch"/>
/// (Partie-Einstellung <see cref="Partie.Partieeinstellungen.KarrenSelbstFahren"/>, Entwicklerhaken <see cref="Entwickler.EntwicklerKarrenAutomatisch"/>).
/// Befehle: <see cref="KarrenLosschicken"/>, <see cref="KarrenZurueck"/>, <see cref="KarrenBeladen"/>, <see cref="KarrenAbladen"/>, <see cref="KarrenHandeln"/>
/// – keine Aktionspunkte, nicht aus dem Kerker; Befehle an fahrende Karren werden abgelehnt.
/// </summary>
public sealed partial class Stadtwirtschaft
{
    /// <summary>
    /// Fahren die Karren aller Häuser nach der Verwalterregel (true, Standard des Kerns: alte Spielstände, Tests, Messung) – oder fahren die
    /// Spieler ihre Karren selbst (false)? Gilt für die ganze Partie; die KI fährt immer automatisch. Im Spielstand nur, wenn false.
    /// </summary>
    public bool KarrenAutomatisch { get; internal set; } = true;

    /// <summary>Ist dieses Haus ein KI-Haus (ein Rivale)?</summary>
    public bool IstKiHaus(Konto konto) => _rivalen.Any(r => r.Konto == konto);

    /// <summary>Fahren die Karren dieses Hauses automatisch (KI-Haus oder Haken <see cref="KarrenAutomatisch"/>)? Später kommt der Meister als dritter Fall dazu.</summary>
    public bool HausFaehrtAutomatisch(Konto konto) => KarrenAutomatisch || IstKiHaus(konto);

    /// <summary>
    /// Fährt dieser Karren nach der Verwalterregel von selbst (true) – oder fährt ihn der Spieler selbst (Entscheidung des Nutzers 23.11.2026)?
    /// Nur automatisch fahrende Karren verweilen nach dem Handel (<see cref="LogistikDaten.VerweilenMinuten"/>).
    /// </summary>
    public bool FaehrtAutomatisch(Karren karren) => HausFaehrtAutomatisch(karren.Heimat.Konto);

    // ═════════════════════════════════════════════════════════════════════════
    //  Fahrzeiten
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wie lange dieser Karren von <paramref name="von"/> nach <paramref name="nach"/> fährt (null: der Markt) und über welche Stücke (für die
    /// Klappbrücke; null ohne Strecke). Der gewohnte Weg Heimat ↔ Markt ist genau <see cref="Fahrzeit(Karren, bool)"/> (mit dem Hafen als Handelsort);
    /// ein anderer Betrieb ↔ Markt die Fahrzeit jenes Betriebs; Betrieb → Betrieb <see cref="FahrzeitZwischen"/>.
    /// </summary>
    public (int Minuten, IReadOnlyList<Fahrtstueck>? Strecke) KarrenFahrt(Karren karren, Betrieb? von, Betrieb? nach)
    {
        if (von == karren.Heimat && nach is null) return (Fahrzeit(karren, zumMarkt: true), Fahrten(karren)?.Hin);
        if (von is null && nach == karren.Heimat) return (Fahrzeit(karren, zumMarkt: false), Fahrten(karren)?.Zurueck);
        if (nach is null) return (Fahrzeit(von!, karren.Typ, zumMarkt: true), Fahrten(von!)?.Hin);
        if (von is null) return (Fahrzeit(nach, karren.Typ, zumMarkt: false), Fahrten(nach)?.Zurueck);
        return (FahrzeitZwischen(von, nach, karren.Typ), StreckeZwischen(von, nach));
    }

    /// <summary>
    /// Wie lange ein Karren der Art <paramref name="karrenTyp"/> von Betrieb <paramref name="von"/> zu Betrieb <paramref name="nach"/> fährt (Spielminuten):
    /// nach Weglänge und Tempo wie zum Markt (<see cref="Fahrzeit(Betrieb, string, bool)"/>: Steigung, Belag, Schlamm, Furten, Mindest- und Höchstdauer)
    /// auf der Strecke Halteplatz → Hof → Netz der Wege → Hof → Halteplatz. Ohne Strecke (keine Karte, kein Platz, kein Weg, alte Daten): so lange
    /// wie über den Markt (hin zum Markt + vom Markt zum Ziel).
    /// </summary>
    public int FahrzeitZwischen(Betrieb von, Betrieb nach, string karrenTyp)
    {
        var ueberDenMarkt = Fahrzeit(von, karrenTyp, zumMarkt: true) + Fahrzeit(nach, karrenTyp, zumMarkt: false);
        if (NachWeglaenge(karrenTyp) is not { } art || StreckeZwischen(von, nach) is not { Count: > 0 } strecke) return ueberDenMarkt;
        return NachLaenge(strecke, art, ueberDenMarkt);
    }

    /// <summary>Die Stücke der Fahrt von Betrieb zu Betrieb (null ohne Karte, ohne Platz eines der beiden, mit alten Daten oder ohne Weg).</summary>
    public IReadOnlyList<Fahrtstueck>? StreckeZwischen(Betrieb von, Betrieb nach) => FahrtZwischen(von, nach)?.Stuecke;

    /// <summary>
    /// Die Punkte der Fahrt von Betrieb zu Betrieb (Stück i von <see cref="StreckeZwischen"/> führt von Punkt i nach Punkt i + 1): vom Halteplatz
    /// des ersten über Hof, Zubringer und Wege zum Halteplatz des zweiten. Für die Darstellung, damit sie genau die Strecke des Kerns fährt;
    /// null wie <see cref="StreckeZwischen"/>.
    /// </summary>
    public IReadOnlyList<Kartenort>? KarrenverlaufZwischen(Betrieb von, Betrieb nach) => FahrtZwischen(von, nach)?.Punkte;

    private readonly Dictionary<(string, string), (IReadOnlyList<Fahrtstueck> Stuecke, IReadOnlyList<Kartenort> Punkte)?> _fahrtenZwischen = new();

    private (IReadOnlyList<Fahrtstueck> Stuecke, IReadOnlyList<Kartenort> Punkte)? FahrtZwischen(Betrieb von, Betrieb nach)
    {
        if (_fahrtenZwischen.TryGetValue((von.Name, nach.Name), out var bekannt)) return bekannt;
        if (!NachWeglaengeGerechnet || Karte is not { } karte || Karrennetz is not { } netz
            || Bauen.PlatzVon(von.Name) is not { } platzVon || Bauen.PlatzVon(nach.Name) is not { } platzNach)
            return null; // (ohne Platz nicht merken: er kann noch kommen)
        var (ankerVon, hofVon) = Karrenweg.Start(platzVon, von, karte.Katalog);
        var (ankerNach, hofNach) = Karrenweg.Start(platzNach, nach, karte.Katalog);
        var fahrt = Karrenweg.MitZubringer(netz.FahrtMitVerlauf(hofVon, hofNach), ankerVon, hofVon, karte.Gelaendeform);
        (IReadOnlyList<Fahrtstueck>, IReadOnlyList<Kartenort>)? ergebnis = null;
        if (fahrt.Stuecke.Count > 0)
        {
            var ende = Karrenweg.Gerade(new[] { fahrt.Punkte[^1], hofNach, ankerNach }, karte.Gelaendeform);
            ergebnis = (fahrt.Stuecke.Concat(ende.Stuecke).ToList(), fahrt.Punkte.Concat(ende.Punkte.Skip(1)).ToList());
        }
        return _fahrtenZwischen[(von.Name, nach.Name)] = ergebnis;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Prüfungen (für Befehle und Oberfläche)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Warum dieses Haus dem Karren Nr. <paramref name="karrenNr"/> (ab 0) dieses Betriebs keinen Fahrbefehl geben kann – oder null.</summary>
    public string? KarrenBefehlVerboten(Konto konto, Betrieb betrieb, int karrenNr)
    {
        if (betrieb.Konto != konto) return $"{betrieb.Name} gehört nicht {konto.Inhaber}.";
        if (karrenNr < 0 || karrenNr >= betrieb.Karren.Count) return $"{betrieb.Name} hat keinen Karren Nr. {karrenNr + 1}.";
        var karren = betrieb.Karren[karrenNr];
        if (FaehrtAutomatisch(karren)) return $"{karren.Name} fährt von selbst (Verwalter) – selbst fahren lassen sich die Karren in dieser Partie nicht.";
        if (karren.Unterwegs) return $"{karren.Name} ist unterwegs – Befehle erst, wenn er am Ziel steht.";
        return null;
    }

    /// <summary>
    /// Warum diese Karren nicht nach <paramref name="zielBetrieb"/> (null: zum Markt; sonst der Name eines eigenen Betriebs) losfahren können – oder null.
    /// </summary>
    public string? KarrenLosschickenVerboten(Konto konto, Betrieb betrieb, IReadOnlyList<int> karren, string? zielBetrieb, string? eskorte = null)
    {
        if (karren.Count == 0) return "Kein Karren gewählt.";
        if (karren.Distinct().Count() != karren.Count) return "Ein Karren ist doppelt gewählt.";
        Betrieb? ziel = null;
        if (zielBetrieb is not null)
        {
            ziel = _betriebe.FirstOrDefault(b => b.Name == zielBetrieb);
            if (ziel is null) return $"Einen Betrieb „{zielBetrieb}“ gibt es nicht.";
            if (ziel.Konto != konto) return $"{ziel.Name} gehört nicht {konto.Inhaber} – Karren fahren nur zu eigenen Betrieben.";
        }
        if (eskorte is not null)
        {
            if (!Daten.Logistik.Eskorten.ContainsKey(eskorte)) return $"Die Eskorte „{eskorte}“ gibt es nicht.";
            if (!Wache.EskorteVerfuegbar(eskorte)) return $"Die {eskorte} gibt Geleitschutz erst, wenn ein Haus ein Wachhaus mit Leuten führt.";
        }
        foreach (var nr in karren)
        {
            if (KarrenBefehlVerboten(konto, betrieb, nr) is { } grund) return grund;
            var k = betrieb.Karren[nr];
            if (ziel is null && k.Zustand == KarrenZustand.AmMarkt) return $"{k.Name} steht schon am Markt.";
            if (ziel is not null && k.StandortVon(this) == ziel) return $"{k.Name} steht schon an {ziel.Name}.";
        }
        return null;
    }

    /// <summary>Warum dieser Karren diese Waren nicht laden kann – oder null. Geladen wird aus dem Lager des eigenen Betriebs, an dem er steht.</summary>
    public string? KarrenBeladenVerboten(Konto konto, Betrieb betrieb, int karrenNr, IReadOnlyDictionary<string, int> waren)
    {
        if (KarrenBefehlVerboten(konto, betrieb, karrenNr) is { } grund) return grund;
        var karren = betrieb.Karren[karrenNr];
        if (karren.StandortVon(this) is not { } lager) return $"{karren.Name} steht am Markt – dort wird gehandelt, nicht geladen.";
        if (lager.Konto != konto) return $"{lager.Name} gehört nicht {konto.Inhaber}.";
        if (waren.Any(x => x.Value < 0)) return "Mengen dürfen nicht negativ sein.";
        var laden = waren.Where(x => x.Value > 0).OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
        if (laden.Count == 0) return "Nichts zum Laden gewählt.";
        foreach (var (ware, stueck) in laden)
            if (lager.ImLager(ware) < stueck) return $"Im Lager von {lager.Name} liegen nur {Zahlenformat.Ganz(lager.ImLager(ware))} {ware}.";
        var danach = new Dictionary<string, int>(karren.Ladung, StringComparer.Ordinal);
        foreach (var (ware, stueck) in laden) danach[ware] = danach.GetValueOrDefault(ware) + stueck;
        if (karren.Flaechen(danach) > karren.Daten.Sorten)
            return $"So viel fasst {karren.Name} nicht: {Zahlenformat.Ganz(karren.Daten.Sorten)} × {Zahlenformat.Ganz(karren.Daten.KapazitaetJeSorte)} Stück (je Ladefläche eine Ware).";
        return null;
    }

    /// <summary>Warum dieser Karren nicht abladen kann (<paramref name="waren"/> null: alles) – oder null. Abgeladen wird ins Lager des eigenen Betriebs, an dem er steht.</summary>
    public string? KarrenAbladenVerboten(Konto konto, Betrieb betrieb, int karrenNr, IReadOnlyDictionary<string, int>? waren = null)
    {
        if (KarrenBefehlVerboten(konto, betrieb, karrenNr) is { } grund) return grund;
        var karren = betrieb.Karren[karrenNr];
        if (karren.StandortVon(this) is not { } lager) return $"{karren.Name} steht am Markt – abladen geht nur an einem eigenen Betrieb.";
        if (lager.Konto != konto) return $"{lager.Name} gehört nicht {konto.Inhaber}.";
        if (karren.Ladung.Count == 0) return $"{karren.Name} hat nichts geladen.";
        if (waren is null) return null;
        if (waren.Any(x => x.Value < 0)) return "Mengen dürfen nicht negativ sein.";
        if (waren.All(x => x.Value == 0)) return "Nichts zum Abladen gewählt.";
        foreach (var (ware, stueck) in waren.Where(x => x.Value > 0).OrderBy(x => x.Key, StringComparer.Ordinal))
            if (karren.Ladung.GetValueOrDefault(ware) < stueck) return $"{karren.Name} hat nur {Zahlenformat.Ganz(karren.Ladung.GetValueOrDefault(ware))} {ware} geladen.";
        return null;
    }

    /// <summary>Warum dieser Karren am Markt nicht so handeln kann – oder null. Verkauft wird aus der Ladung, gekauft auf die Ladung.</summary>
    public string? KarrenHandelnVerboten(Konto konto, Betrieb betrieb, int karrenNr, IReadOnlyDictionary<string, int> verkauf, IReadOnlyDictionary<string, int> kauf)
    {
        if (KarrenBefehlVerboten(konto, betrieb, karrenNr) is { } grund) return grund;
        var karren = betrieb.Karren[karrenNr];
        if (karren.Zustand != KarrenZustand.AmMarkt) return $"{karren.Name} steht nicht am Markt – gehandelt wird erst dort.";
        if (Daten.Handel is { } h && (Uhr.UhrzeitInMinuten < h.MarktVon * 60 || Uhr.UhrzeitInMinuten >= h.MarktBis * 60))
            return $"Der Markt hat von {h.MarktVon} bis {h.MarktBis} Uhr offen.";
        if (verkauf.Concat(kauf).Any(x => x.Value < 0)) return "Mengen dürfen nicht negativ sein.";
        var verkaufen = verkauf.Where(x => x.Value > 0).OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
        var kaufen = kauf.Where(x => x.Value > 0).OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
        if (verkaufen.Count == 0 && kaufen.Count == 0) return "Nichts zum Handeln gewählt.";
        foreach (var (ware, stueck) in verkaufen)
            if (karren.Ladung.GetValueOrDefault(ware) < stueck) return $"{karren.Name} hat nur {Zahlenformat.Ganz(karren.Ladung.GetValueOrDefault(ware))} {ware} geladen.";
        foreach (var (ware, _) in kaufen)
            if (Markt.Staende.All(s => s.Ware != ware)) return $"Auf dem Markt gibt es keine Ware „{ware}“.";
        var danach = new Dictionary<string, int>(karren.Ladung, StringComparer.Ordinal);
        foreach (var (ware, stueck) in verkaufen) danach[ware] -= stueck;
        foreach (var (ware, stueck) in kaufen) danach[ware] = danach.GetValueOrDefault(ware) + stueck;
        if (karren.Flaechen(danach) > karren.Daten.Sorten)
            return $"So viel fasst {karren.Name} nicht: {Zahlenformat.Ganz(karren.Daten.Sorten)} × {Zahlenformat.Ganz(karren.Daten.KapazitaetJeSorte)} Stück (je Ladefläche eine Ware).";
        var kosten = kaufen.Sum(x => HandKaufpreis(konto, x.Key, x.Value));
        var erloes = verkaufen.Sum(x => Markt[x.Key].Preis * x.Value);
        if (kosten > konto.Vermoegen + erloes)
            return $"Die Einkäufe kosten etwa {Zahlenformat.Ganz(kosten)} {Daten.Allgemein.Waehrung} – so viel habt ihr nicht.";
        return null;
    }

    /// <summary>Warum Handelsauftrag, Vormerkung und „Ware zurückhalten“ für dieses Haus nicht gehen (selbst gefahren: der Spieler handelt am Markt) – oder null.</summary>
    public string? VerwalterhandelVerboten(Konto konto) =>
        HausFaehrtAutomatisch(konto) ? null : "Ihr fahrt eure Karren selbst – gehandelt wird am Markt (Karren hinschicken, dann handeln).";

    // ═════════════════════════════════════════════════════════════════════════
    //  Ausführen
    // ═════════════════════════════════════════════════════════════════════════

    private Befehlsergebnis KarrenLosschickenAusfuehren(Betrieb betrieb, IReadOnlyList<int> nummern, string? zielBetrieb, string? eskorte)
    {
        if (KarrenLosschickenVerboten(betrieb.Konto, betrieb, nummern, zielBetrieb, eskorte) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var ziel = zielBetrieb is null ? null : _betriebe.First(b => b.Name == zielBetrieb);
        var zeilen = new List<string>();
        foreach (var nr in nummern)
        {
            var karren = betrieb.Karren[nr];
            if (eskorte is not null) karren.Eskorte = eskorte;
            karren.Losschicken(ziel, Uhr, this);
            var an = Uhr.UhrzeitInMinuten + (int)(karren.Ankunft - Uhr.VergangeneMinuten);
            zeilen.Add($"{karren.Name} fährt {(ziel is null ? "zum Markt" : ziel == betrieb ? "zurück zu " + ziel.Name : "nach " + ziel.Name)} (an {UhrText(an)})");
        }
        return Befehlsergebnis.Ok(string.Join("; ", zeilen) + ".");
    }

    /// <summary>Uhrzeit „hh:mm“ aus Minuten nach Mitternacht (über Mitternacht hinaus weiter gezählt, wie die übrigen Ankunftszeiten).</summary>
    private static string UhrText(int minuten) => $"{Zahlenformat.Ganz(minuten / 60).PadLeft(2, '0')}:{Zahlenformat.Ganz(minuten % 60).PadLeft(2, '0')}";

    private Befehlsergebnis KarrenBeladenAusfuehren(Betrieb betrieb, KarrenBeladen befehl)
    {
        if (KarrenBeladenVerboten(betrieb.Konto, betrieb, befehl.Karren, befehl.Waren) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var karren = betrieb.Karren[befehl.Karren];
        var lager = karren.StandortVon(this)!;
        karren.Laden(lager, befehl.Waren);
        return Befehlsergebnis.Ok($"{karren.Name} lädt an {lager.Name}: {WarenText(befehl.Waren)}.");
    }

    private Befehlsergebnis KarrenAbladenAusfuehren(Betrieb betrieb, KarrenAbladen befehl)
    {
        if (KarrenAbladenVerboten(betrieb.Konto, betrieb, befehl.Karren, befehl.Waren) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var karren = betrieb.Karren[befehl.Karren];
        var lager = karren.StandortVon(this)!;
        var abgeladen = karren.AbladenIn(lager, befehl.Waren);
        return Befehlsergebnis.Ok($"{karren.Name} lädt an {lager.Name} ab: {WarenText(abgeladen)}.");
    }

    private Befehlsergebnis KarrenHandelnAusfuehren(Betrieb betrieb, KarrenHandeln befehl)
    {
        if (KarrenHandelnVerboten(betrieb.Konto, betrieb, befehl.Karren, befehl.Verkauf, befehl.Kauf) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var karren = betrieb.Karren[befehl.Karren];
        var erloes = karren.Verkaufen(befehl.Verkauf, this);
        var kosten = karren.Kaufen(befehl.Kauf, this);
        var waehrung = Daten.Allgemein.Waehrung;
        var teile = new List<string>();
        if (befehl.Verkauf.Any(x => x.Value > 0)) teile.Add($"verkauft {WarenText(befehl.Verkauf)} für {Zahlenformat.Ganz(erloes)} {waehrung}");
        if (befehl.Kauf.Any(x => x.Value > 0)) teile.Add($"gekauft {WarenText(befehl.Kauf)} für {Zahlenformat.Ganz(kosten)} {waehrung}");
        return Befehlsergebnis.Ok($"{karren.Name} am {karren.Handelsort}: {string.Join("; ", teile)}.");
    }

    /// <summary>„5 Eisen, 10 Holz“ (nach Namen geordnet, nur Mengen über 0).</summary>
    private static string WarenText(IReadOnlyDictionary<string, int> waren) =>
        string.Join(", ", waren.Where(x => x.Value > 0).OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{Zahlenformat.Ganz(x.Value)} {x.Key}"));
}
