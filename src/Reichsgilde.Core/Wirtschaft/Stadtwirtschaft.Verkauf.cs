using Reichsgilde.Core.Speichern;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>Ein Posten der Lagerware, die beim Verkauf mitgeht: Stück und Erlös (Marktpreis abzüglich Bearbeitungsgebühr).</summary>
public sealed record Lagerposten(string Ware, int Stueck, double Wert);

/// <summary>
/// Was der Verkauf eines Betriebs bringen würde – für die Rückfrage vor dem Verkauf.
/// Alle Beträge in Gulden; <see cref="Summe"/> fließt ins Bargeld (dazu die <see cref="Kasse"/> eines Geldleihers).
/// </summary>
/// <param name="Betrieb">Name des Betriebs.</param>
/// <param name="IstPacht">Umlandbetrieb: Die Pacht wird gekündigt, für den Betrieb selbst gibt es nichts.</param>
/// <param name="Stufe">Gebäudestufe – so steht er danach zum Verkauf.</param>
/// <param name="Wert">Voller Wert: Kaufpreis der Stufe 1 und alle Ausbauten bis zur Stufe (Pacht: 0). So viel zahlt der nächste Käufer.</param>
/// <param name="Gebaeudeerloes">Was das Haus für den Betrieb bekommt (75 % des Werts).</param>
/// <param name="Lagerware">Die mitverkaufte Ware, je Ware mit Erlös.</param>
/// <param name="Warenerloes">Summe der Lagerware (Marktpreis −20 %, ohne Wirkung auf den Markt).</param>
/// <param name="Abrechnung">Anteilige Löhne, Wächter und Unterhalt bzw. Pacht bis heute – wird abgezogen.</param>
/// <param name="Summe">Gebäude + Ware − Abrechnung.</param>
/// <param name="Kasse">Geldleiher: Die Kasse kommt zurück ins Bargeld (kein Erlös, das Geld gehört dem Haus schon).</param>
/// <param name="Personal">Arbeitskräfte, die mit dem Betrieb gehen (Art → Anzahl).</param>
/// <param name="Karren">Karren, die mit dem Betrieb gehen.</param>
/// <param name="Waechter">Geht ein Wächter mit?</param>
/// <param name="Verboten">Warum der Verkauf gerade nicht geht (null = er geht).</param>
public sealed record Verkaufsvorschau(
    string Betrieb, bool IstPacht, int Stufe, double Wert, double Gebaeudeerloes,
    IReadOnlyList<Lagerposten> Lagerware, double Warenerloes, double Abrechnung, double Summe, double Kasse,
    IReadOnlyDictionary<string, int> Personal, IReadOnlyList<string> Karren, bool Waechter, string? Verboten);

/// <summary>
/// Betriebe verkaufen und verkaufte Betriebe wieder erwerben (Entscheidung zu PR #75, Regeln vom 16.10.2026).
///
///   • Eine Werkstatt bringt 75 % ihres Werts (Kaufpreis der Stufe 1 + alle Ausbauten), eine Umlandpacht wird gekündigt.
///   • Personal, Wächter und Karren gehen mit; die Lagerware wird zum Marktpreis −20 % mitverkauft.
///   • Mindestens ein Betrieb bleibt; nicht während eines Umbaus und nicht, solange ein Karren unterwegs ist.
///   • Danach ist der Betrieb wieder eine freie Liegenschaft – mit seiner Stufe, zum vollen Wert und nur für Käufer
///     mit dem Meistergrad dieser Stufe. Das gilt auch für die Startbetriebe.
/// </summary>
public sealed partial class Stadtwirtschaft
{
    /// <summary>Verkaufte Betriebe, die wieder zu haben sind – gehen den gleichnamigen Einträgen aus den Daten vor.</summary>
    private readonly List<LiegenschaftDaten> _abgegeben = new();

    /// <summary>
    /// Liegenschaften, die noch niemandem gehören: die aus den Daten und verkaufte Betriebe
    /// (diese mit ihrer Stufe in <see cref="LiegenschaftDaten.Art"/>, dem vollen Wert als Kaufpreis, <see cref="LiegenschaftDaten.Bebaut"/>
    /// und <see cref="LiegenschaftDaten.MindestGrad"/>).
    /// </summary>
    public IEnumerable<LiegenschaftDaten> FreieLiegenschaften =>
        Daten.Liegenschaften.Where(l => _abgegeben.All(a => a.Name != l.Name))
            .Concat(_abgegeben)
            .Where(l => _betriebe.All(b => b.Name != l.Name));

    /// <summary>
    /// Ein Haus hat einen Betrieb verkauft oder eine Pacht gekündigt. Der Betrieb gehört niemandem mehr
    /// (er steht nicht mehr in <see cref="Betriebe"/>); die Liegenschaft ist wieder frei.
    /// </summary>
    public event Action<Betrieb, LiegenschaftDaten>? BetriebAbgegeben;

    private LiegenschaftDaten? Liegenschaft(string name) =>
        _abgegeben.FirstOrDefault(x => x.Name == name) ?? Daten.Liegenschaften.FirstOrDefault(x => x.Name == name);

    /// <summary>Warum dieses Haus die Liegenschaft nicht erwerben kann (null = es geht).</summary>
    public string? LiegenschaftErwerbVerboten(Konto konto, string name)
    {
        if (Liegenschaft(name) is not { } l)
            return $"Eine Liegenschaft „{name}“ gibt es nicht.";
        if (_betriebe.FirstOrDefault(b => b.Name == name) is { } vergeben)
            return $"{name} gehört bereits {vergeben.Konto.Inhaber}.";
        if (konto.Stand < Daten.StandStufe(l.MindestStand))
            return $"{name} ist erst ab dem Stand {l.MindestStand} zu haben – {konto.Inhaber} ist {StandVon(konto).Name}.";
        var art = Daten.Art(l.Art);
        if (Daten.Berufe.Count > 0 && konto.Berufe.Count > 0 && !konto.Beherrscht(art.Beruf)) // ohne jedes Handwerk darf man mit jedem anfangen
            return $"{name} ist ein Betrieb der {art.Beruf}. {konto.Inhaber} beherrscht dieses Handwerk nicht – erst erlernen.";
        if (l.MindestGrad > 1 && konto.Meistergrad(art.Beruf) < l.MindestGrad)
            return $"{name} ist eine {art.Name} (Stufe {art.Stufe}) – dafür braucht es {art.Beruf} im {l.MindestGrad}. Meistergrad, " +
                   $"{konto.Inhaber} ist im {konto.Meistergrad(art.Beruf)}.";
        // Kaufpreis plus die Kosten des ersten Jahres müssen gedeckt sein – sonst droht sofort die Schuldenfalle.
        if (konto.Vermoegen < l.Kaufpreis + art.Fixkosten)
            return $"Für {name} braucht es {Zahlenformat.Ganz(l.Kaufpreis + art.Fixkosten)} {Daten.Allgemein.Waehrung} " +
                   $"({(l.IstPacht ? "erste Jahrespacht" : $"Kaufpreis {Zahlenformat.Ganz(l.Kaufpreis)} und ein Jahr Unterhalt")}).";
        return null;
    }

    private Befehlsergebnis LiegenschaftErwerbenAusfuehren(Konto konto, string name)
    {
        if (LiegenschaftErwerbVerboten(konto, name) is { } grund)
            return Befehlsergebnis.Abgelehnt(grund);
        var l = Liegenschaft(name)!;
        var waehrung = Daten.Allgemein.Waehrung;
        if (l.Kaufpreis > 0) konto.Investition(l.Kaufpreis);
        var betrieb = BetriebEroeffnen(l.Name, l.Art, konto); // leer, mit dem Startkarren – auch ein gebrauchter Betrieb
        _abgegeben.RemoveAll(x => x.Name == name);
        Melden(l.IstPacht
            ? $"{konto.Inhaber} pachtet {name} ({Zahlenformat.Ganz(Daten.Fixkosten(l.Art))} {waehrung} Pacht im Jahr)."
            : $"{konto.Inhaber} kauft {name} für {Zahlenformat.Ganz(l.Kaufpreis)} {waehrung}.");
        BetriebErworben?.Invoke(betrieb);
        return Befehlsergebnis.Ok($"{name} gehört nun {konto.Inhaber}. Jetzt Personal einstellen!");
    }

    // ── Wert eines Betriebs ─────────────────────────────────────────────────

    /// <summary>
    /// Was ein Betrieb wert ist: Kaufpreis der Stufe 1 plus alle Ausbauten bis zu seiner Stufe
    /// (Gießerei 3.500, Schmiede 6.000, Goldschmiede 11.000). Eine Pacht ist nichts wert.
    /// </summary>
    public double Betriebswert(Betrieb betrieb) => Wert(betrieb.Name, betrieb.Art);

    private double Wert(string name, Betriebsart art)
    {
        var (grundart, ausbauten) = Grundart(art);
        return Grundpreis(name, grundart) is var preis and > 0 ? preis + ausbauten : 0;
    }

    /// <summary>Die Stufe-1-Art, aus der diese Art durch Ausbau entsteht, und was die Ausbauten bis hierher gekostet haben.</summary>
    private (Betriebsart Art, double Ausbauten) Grundart(Betriebsart art)
    {
        double summe = 0;
        for (var i = 0; i < 10; i++) // die Prüfung der Daten schließt Kreise aus; die Grenze ist nur Vorsicht
        {
            var vorher = Daten.Betriebsarten.Where(x => x.Value.Ausbau == art).Select(x => (Art: x.Key, Daten: x.Value)).FirstOrDefault();
            if (vorher.Daten is null) break;
            summe += vorher.Daten.AusbauKosten;
            art = vorher.Art;
        }
        return (art, summe);
    }

    /// <summary>
    /// Die Liegenschaft aus den Daten, nach der sich ein Betrieb richtet: die gleichnamige, sonst die günstigste derselben Art.
    /// So kostet die Startgießerei am Weinmarkt so viel wie die Gießerei an der Südgasse – ohne neues Feld in den Daten.
    /// </summary>
    internal LiegenschaftDaten? Vorbild(string name, Betriebsart grundart) =>
        Daten.Liegenschaften.FirstOrDefault(l => l.Name == name)
        ?? Daten.Liegenschaften.Where(l => l.Art == grundart).OrderBy(l => l.Kaufpreis).ThenBy(l => l.Name, StringComparer.Ordinal).FirstOrDefault();

    /// <summary>Kaufpreis der Stufe 1. Gibt es keine Liegenschaft dieser Art, gilt die günstigste Werkstatt der Stadt; Umland wird gepachtet (0).</summary>
    internal double Grundpreis(string name, Betriebsart grundart)
    {
        if (Vorbild(name, grundart) is { } l) return l.Kaufpreis;
        if (!Daten.Art(grundart).InDerStadt) return 0;
        return Daten.Liegenschaften.Where(x => x.Kaufpreis > 0).Select(x => x.Kaufpreis).DefaultIfEmpty(0).Min();
    }

    /// <summary>Welcher Meistergrad für eine Werkstatt dieser Art (Stufe) nötig ist.</summary>
    private int MindestGrad(Betriebsart art) => Daten.Meistergrade?.GradFuerStufe(Daten.Art(art).Stufe) ?? 1;

    private LiegenschaftDaten FreieLiegenschaft(string name, Betriebsart art, double kaufpreis, string mindestStand) => new()
    {
        Name = name, Art = art, Kaufpreis = kaufpreis, MindestStand = mindestStand, Bebaut = true, MindestGrad = MindestGrad(art),
    };

    // ── Verkaufen ───────────────────────────────────────────────────────────

    /// <summary>Warum dieses Haus den Betrieb (gerade) nicht verkaufen kann (null = es geht).</summary>
    public string? VerkaufVerboten(Konto konto, Betrieb betrieb)
    {
        if (betrieb.Konto != konto || !_betriebe.Contains(betrieb))
            return $"{betrieb.Name} gehört nicht {konto.Inhaber}.";
        if (!konto.IstAktiv)
            return $"{konto.Inhaber} kann nichts mehr verkaufen.";
        if (_betriebe.Count(b => b.Konto == konto) <= 1)
            return $"{betrieb.Name} ist der letzte Betrieb von {konto.Inhaber} – einer muss bleiben.";
        if (betrieb.UmbauMinuten > 0)
            return $"{betrieb.Name} wird gerade umgebaut – verkaufen erst, wenn der Umbau fertig ist.";
        if (betrieb.Karren.FirstOrDefault(k => k.Zustand != KarrenZustand.AmBetrieb) is { } unterwegs)
            return $"{unterwegs.Name} ist unterwegs – verkaufen erst, wenn alle Karren am Betrieb stehen.";
        return null;
    }

    /// <summary>Was der Verkauf dieses Betriebs jetzt bringen würde – und ob er geht (für die Rückfrage in der Oberfläche).</summary>
    public Verkaufsvorschau VerkaufVorschau(Betrieb betrieb)
    {
        var v = Daten.Verkauf;
        var wert = Betriebswert(betrieb);
        var lager = betrieb.Lager.Where(x => x.Value > 0)
            .Select(x => new Lagerposten(x.Key, x.Value, x.Value * Marktpreis(x.Key) * (1 - v.WarenAbschlag)))
            .ToList();
        var (loehne, fix) = Abrechnung(betrieb);
        var gebaeude = wert * v.Anteil;
        var waren = lager.Sum(x => x.Wert);
        return new Verkaufsvorschau(
            betrieb.Name, wert <= 0, betrieb.Stufe, wert, gebaeude, lager, waren, loehne + fix, gebaeude + waren - loehne - fix,
            Bank.IstAktiv ? Bank.Kasse(betrieb) : 0,
            new Dictionary<string, int>(betrieb.Belegung), betrieb.Karren.Select(k => k.Name).ToList(), betrieb.Waechter,
            VerkaufVerboten(betrieb.Konto, betrieb));
    }

    /// <summary>Marktpreis einer Ware – Waren ohne Marktstand zum Grundpreis.</summary>
    private double Marktpreis(string ware) =>
        Markt.Staende.Any(s => s.Ware == ware) ? Markt[ware].Preis : Daten.Waren.GetValueOrDefault(ware)?.Grundpreis ?? 0;

    /// <summary>
    /// Löhne (mit Wächter) und Unterhalt bzw. Pacht, anteilig bis heute. Sonst würden sie erst am Jahresende fällig –
    /// und wer kurz davor verkauft, hätte das ganze Jahr umsonst arbeiten lassen.
    /// </summary>
    private (double Loehne, double Fixkosten) Abrechnung(Betrieb betrieb)
    {
        var anteil = Uhr.TagesFortschritt;
        var loehne = betrieb.Belegung.Sum(x => x.Value * Daten.Arbeitskraefte[x.Key].Kosten)
                     + (betrieb.Waechter && Daten.Gauner is { } g ? g.WaechterKosten : 0);
        return (loehne * anteil, betrieb.Artdaten.Fixkosten * anteil);
    }

    private Befehlsergebnis VerkaufenAusfuehren(Betrieb betrieb)
    {
        var konto = betrieb.Konto;
        if (VerkaufVerboten(konto, betrieb) is { } grund)
            return Befehlsergebnis.Abgelehnt(grund);
        var vorschau = VerkaufVorschau(betrieb);
        var waehrung = Daten.Allgemein.Waehrung;

        var (loehne, fix) = Abrechnung(betrieb);
        if (loehne > 0) betrieb.PersonalBuchen(loehne);
        if (fix > 0) betrieb.FixkostenBuchen(fix);
        if (vorschau.Gebaeudeerloes + vorschau.Warenerloes > 0) konto.Verkauf(vorschau.Gebaeudeerloes + vorschau.Warenerloes);
        var frei = Freigeben(betrieb, vorschau.Wert);

        var ware = vorschau.Lagerware.Count > 0 ? $", die Lagerware {Zahlenformat.Ganz(vorschau.Warenerloes)}" : "";
        var abzug = vorschau.Abrechnung > 0.5 ? $", abzüglich {Zahlenformat.Ganz(vorschau.Abrechnung)} Löhne und {(vorschau.IstPacht ? "Pacht" : "Unterhalt")} bis heute" : "";
        Melden(vorschau.IstPacht
            ? $"{konto.Inhaber} kündigt die Pacht für {betrieb.Name}."
            : $"{konto.Inhaber} verkauft {betrieb.Name} ({betrieb.Artdaten.Name}) für {Zahlenformat.Ganz(vorschau.Gebaeudeerloes)} {waehrung}.");
        BetriebAbgegeben?.Invoke(betrieb, frei);
        return Befehlsergebnis.Ok(vorschau.IstPacht
            ? $"Die Pacht für {betrieb.Name} ist gekündigt{ware}{abzug}. Personal und Karren gehen mit."
            : $"{betrieb.Name} ist verkauft: {Zahlenformat.Ganz(vorschau.Gebaeudeerloes)} {waehrung} für das Haus{ware}{abzug}. " +
              "Personal, Wächter und Karren gehen mit.");
    }

    /// <summary>
    /// Der Betrieb gehört niemandem mehr: Kasse zurück ins Bargeld, aus Gästebuch, Kirchenbuch und Hinterzimmer streichen,
    /// und er steht als freie Liegenschaft zum Verkauf – mit seiner Stufe und dem vollen Wert.
    /// </summary>
    private LiegenschaftDaten Freigeben(Betrieb betrieb, double wert)
    {
        if (Bank.IstAktiv) Bank.KasseAufloesen(betrieb);
        Gastwirtschaft.BetriebAufgegeben(betrieb);
        Kirchenwesen.BetriebAufgegeben(betrieb);
        Gauner.BetriebAufgegeben(betrieb);
        _betriebe.Remove(betrieb);

        var vorbild = Vorbild(betrieb.Name, Grundart(betrieb.Art).Art);
        var stand = vorbild?.MindestStand ?? Daten.Staende[Math.Min(1, Daten.Staende.Count - 1)].Name;
        var frei = FreieLiegenschaft(betrieb.Name, betrieb.Art, wert, stand);
        _abgegeben.RemoveAll(x => x.Name == betrieb.Name);
        _abgegeben.Add(frei);
        return frei;
    }

    /// <summary>
    /// Betriebe bankrotter oder erloschener Häuser stehen zum Verkauf wie verkaufte Werkstätten (Entscheidung vom 17.10.2026) –
    /// ohne Erlös: Die Gläubiger bzw. die Stadt nehmen sie. Personal ist schon fort (<see cref="Stilllegen"/>), Lager und Karren gehen mit.
    /// </summary>
    private void ToteBetriebeFreigeben()
    {
        if (_betriebe.All(b => b.Konto.IstAktiv)) return;
        foreach (var gruppe in _betriebe.Where(b => !b.Konto.IstAktiv).GroupBy(b => b.Konto).ToList())
        {
            foreach (var betrieb in gruppe)
            {
                var frei = Freigeben(betrieb, Betriebswert(betrieb)); // nicht im Aufruf: „?.Invoke“ wertet ohne Zuhörer die Argumente nicht aus
                BetriebAbgegeben?.Invoke(betrieb, frei);
            }
            Melden($"Die Betriebe von {gruppe.Key.Inhaber} stehen nun zum Verkauf: {string.Join(", ", gruppe.Select(b => b.Name))}.");
        }
    }

    /// <summary>Eine neu gebaute Werkstatt, deren Bauherr nicht mehr im Spiel ist: Sie steht zum Verkauf (Stufe 1, Neubaupreis).</summary>
    internal void NeubauFreigeben(string name, Betriebsart art, string mindestStand)
    {
        _abgegeben.RemoveAll(x => x.Name == name);
        _abgegeben.Add(FreieLiegenschaft(name, art, Grundpreis("", art), mindestStand));
    }

    /// <summary>Eine neue Werkstatt ist fertig gebaut – für die Darstellung wie ein Kauf (<see cref="BetriebErworben"/>).</summary>
    internal void ErworbenMelden(Betrieb betrieb) => BetriebErworben?.Invoke(betrieb);

    // ── Speichern ───────────────────────────────────────────────────────────

    private List<FreieLiegenschaftStand>? AbgegebeneSichern() => _abgegeben.Count == 0 ? null
        : _abgegeben.Select(l => new FreieLiegenschaftStand { Name = l.Name, Art = l.Art, Kaufpreis = l.Kaufpreis, MindestStand = l.MindestStand }).ToList();

    private void AbgegebeneWiederherstellen(List<FreieLiegenschaftStand>? s)
    {
        foreach (var l in s ?? new())
            _abgegeben.Add(FreieLiegenschaft(l.Name, l.Art, l.Kaufpreis, l.MindestStand));
    }
}
