using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Justiz;

/// <summary>Was vor Gericht kommen kann.</summary>
public enum Vergehen
{
    /// <summary>Geschenke an Ratsherren.</summary>
    Bestechung,
    /// <summary>Einbruch in ein fremdes Lager (Hinterzimmer).</summary>
    Diebstahl,
    /// <summary>Anschlag auf eine fremde Werkstatt (Hinterzimmer).</summary>
    Sabotage,
    /// <summary>Angeheuerte Wegelagerer überfallen einen fremden Karren (Hinterzimmer).</summary>
    Raub,
    /// <summary>Schweigegeld gegen Beweise verlangen.</summary>
    Erpressung,
    /// <summary>Krötenschleim ins Essen eines fremden Oberhaupts (Hinterzimmer).</summary>
    Giftmischerei,
    /// <summary>Ein Prediger verrät, was ihm in der Beichte anvertraut wurde.</summary>
    Beichtbruch,
    /// <summary>Eine Hetzschrift gegen ein anderes Haus verbreiten.</summary>
    Verleumdung,
    /// <summary>Ein Dieb des Hauses wird am Markt beim Taschendiebstahl erwischt.</summary>
    Taschendiebstahl,
    /// <summary>Schutzgeld von einem anderen Haus verlangen (Diebe im Unterschlupf).</summary>
    Schutzgeld,
    /// <summary>Einen Angehörigen eines anderen Hauses verschleppen und Lösegeld fordern.</summary>
    Entfuehrung,
}

public static class VergehenTexte
{
    public static string Name(this Vergehen v) => v switch
    {
        Vergehen.Raub => "Überfall",
        Vergehen.Schutzgeld => "Schutzgelderpressung",
        Vergehen.Entfuehrung => "Entführung",
        _ => v.ToString(),
    };

    /// <summary>„Bestechung und Diebstahl" – für Anklage und Urteil.</summary>
    public static string Aufzaehlung(IEnumerable<Vergehen> vergehen)
    {
        var namen = vergehen.Distinct().OrderBy(v => v).Select(v => v.Name()).ToList();
        return namen.Count <= 1 ? namen.FirstOrDefault() ?? "—" : string.Join(", ", namen.Take(namen.Count - 1)) + " und " + namen[^1];
    }
}

/// <summary>
/// Die Spur einer Tat. Sie entsteht bei der Tat selbst und bleibt unsichtbar, bis ein Spitzel sie aufdeckt.
/// Nach <see cref="JustizDaten.VerjaehrungJahre"/> Jahren verfällt sie.
/// </summary>
public sealed class Spur
{
    public int Nummer { get; }
    public Konto Taeter { get; }
    public Vergehen Art { get; }

    /// <summary>Wen die Tat betraf, z. B. der bestochene Ratsherr.</summary>
    public string Betroffener { get; }
    public double Betrag { get; }
    public int Jahr { get; }

    internal Spur(int nummer, Konto taeter, Vergehen art, string betroffener, double betrag, int jahr)
    {
        Nummer = nummer;
        Taeter = taeter;
        Art = art;
        Betroffener = betroffener;
        Betrag = betrag;
        Jahr = jahr;
    }

    public string Beschreibung => Art switch
    {
        Vergehen.Bestechung => $"{Jahr}: Bestechung – {Zahlenformat.Ganz(Betrag)} Gulden an {Betroffener}",
        Vergehen.Diebstahl => $"{Jahr}: Diebstahl in {Betroffener} – Ware für {Zahlenformat.Ganz(Betrag)} Gulden",
        Vergehen.Sabotage => $"{Jahr}: Sabotage in {Betroffener} – Schaden {Zahlenformat.Ganz(Betrag)} Gulden",
        Vergehen.Raub => $"{Jahr}: Überfall auf {Betroffener} – Beute für {Zahlenformat.Ganz(Betrag)} Gulden",
        Vergehen.Erpressung => $"{Jahr}: Erpressung von {Betroffener} – {Zahlenformat.Ganz(Betrag)} Gulden verlangt",
        Vergehen.Beichtbruch => $"{Jahr}: Beichtgeheimnis gebrochen – gegen {Betroffener}",
        Vergehen.Verleumdung => $"{Jahr}: Hetzschrift gegen {Betroffener}",
        Vergehen.Taschendiebstahl => $"{Jahr}: Taschendiebstahl am Markt – {Zahlenformat.Ganz(Betrag)} Gulden",
        Vergehen.Schutzgeld => $"{Jahr}: Schutzgeld von {Betroffener} verlangt – {Zahlenformat.Ganz(Betrag)} Gulden",
        Vergehen.Entfuehrung => $"{Jahr}: Entführung bei {Betroffener} – {Zahlenformat.Ganz(Betrag)} Gulden Lösegeld",
        _ => $"{Jahr}: {Art.Name()}",
    };
}

/// <summary>Ein Richter: Ratsherr der Stadt oder – wenn ein Haus den Stuhl des Stadtrichters hält – dieses Haus.</summary>
public sealed record Richter(string Rolle, Ratsherr? Ratsherr, Konto? Konto)
{
    public string Name => Ratsherr?.Name ?? Konto?.Inhaber ?? "—";
}

/// <summary>Eine erhobene Anklage. Verhandelt wird am Gerichtstag zum Jahresende.</summary>
public sealed class Verfahren
{
    public Konto Anklaeger { get; }
    public Konto Beklagter { get; }
    public int Jahr { get; }

    /// <summary>Vorsitz zuerst, dann die beiden Schöffen.</summary>
    public IReadOnlyList<Richter> Richter { get; }

    internal Verfahren(Konto anklaeger, Konto beklagter, int jahr, IReadOnlyList<Richter> richter)
    {
        Anklaeger = anklaeger;
        Beklagter = beklagter;
        Jahr = jahr;
        Richter = richter;
    }
}

/// <summary>Wie ein Richter heute urteilen würde.</summary>
public sealed record Stimme(Richter Richter, double Ueberzeugung, bool Schuldig);

/// <summary>Ein Urteil des letzten Gerichtstags.</summary>
public sealed record Urteil(Konto Anklaeger, Konto Beklagter, bool Schuldig, int Schuldstimmen, string Text);

/// <summary>
/// Das Stadtgericht: Spuren, Spitzel, Beweise, Anklagen und der Gerichtstag am Jahresende.
///
/// Über das Jahr:
///   • Jede Tat hinterlässt eine Spur: jedes Geschenk an einen Ratsherrn (Bestechung) und jedes Gaunerstück.
///   • Ein Haus setzt einen Spitzel auf ein anderes an (Befehl SpitzelAnsetzen, gilt bis Jahresende).
///   • Wer Beweise hat, erhebt Anklage (Befehl Anklagen). Zwei Schöffen werden sofort ausgelost.
///
/// Am Gerichtstag (Jahresende, vor Amtseinkommen und Ratssitzung):
///   1. Jedes Verfahren wird entschieden: drei Richter, die Mehrheit entscheidet.
///      Schuldig → Buße, Amtsverlust, Pranger. Freispruch → der Ankläger entschädigt den Beklagten.
///   2. Die Spitzel berichten: Jede noch unbekannte Spur des beobachteten Hauses wird mit einer Chance aufgedeckt.
///   3. Alte Taten verjähren.
///
/// Spitzel und Schöffen würfelt ein eigener Zufallsgenerator – so bleiben Wirtschaft und Familie unberührt.
/// </summary>
public sealed class Stadtgericht
{
    private readonly Stadtwirtschaft _w;
    private readonly JustizDaten? _d;
    private Zufallsquelle _zufall;
    private readonly List<Spur> _spuren = new();
    private readonly HashSet<(Konto Inhaber, int Spur)> _beweise = new();
    private readonly List<(Konto Auftraggeber, Konto Ziel)> _spitzel = new();
    private readonly List<Verfahren> _verfahren = new();
    private readonly Dictionary<Konto, int> _prangerBis = new();
    private readonly Dictionary<Konto, int> _kerkerBis = new();
    private readonly List<Urteil> _letzteUrteile = new();
    private readonly List<string> _letzterGerichtstag = new();
    private readonly List<Konto> _beobachtet = new();
    private int _naechsteSpur = 1;

    public bool IstAktiv => _d is not null;
    public JustizDaten Daten => _d ?? throw new InvalidOperationException("Es gibt kein Gericht (justiz fehlt in den Daten).");

    public IReadOnlyList<Verfahren> Verfahren => _verfahren;
    public IReadOnlyList<Urteil> LetzteUrteile => _letzteUrteile;

    /// <summary>Alles, was am letzten Gerichtstag geschah (Urteile und Spitzelberichte) – für Jahresbericht und Gerichtssaal.</summary>
    public IReadOnlyList<string> LetzterGerichtstag => _letzterGerichtstag;

    internal Stadtgericht(Stadtwirtschaft w, JustizDaten? daten, int zufallsstartwert)
    {
        _w = w;
        _d = daten;
        _zufall = new Zufallsquelle(unchecked(zufallsstartwert * 23 + 5));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Auskunft
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die noch nicht verjährten Spuren eines Hauses (nur das Haus selbst kennt sie alle).</summary>
    public IEnumerable<Spur> SpurenVon(Konto taeter) => _spuren.Where(s => s.Taeter == taeter);

    /// <summary>Was <paramref name="inhaber"/> gegen <paramref name="gegen"/> in der Hand hat.</summary>
    public IEnumerable<Spur> Beweise(Konto inhaber, Konto gegen) =>
        _spuren.Where(s => s.Taeter == gegen && _beweise.Contains((inhaber, s.Nummer)));

    /// <summary>Wie viele eigene Spuren andere schon kennen (Beweise gegen dieses Haus).</summary>
    public int BekannteSpuren(Konto taeter) =>
        _spuren.Count(s => s.Taeter == taeter && _beweise.Any(b => b.Spur == s.Nummer && b.Inhaber != taeter));

    public bool HatSpitzel(Konto auftraggeber, Konto ziel) => _spitzel.Contains((auftraggeber, ziel));

    /// <summary>Hat am letzten Gerichtstag ein Spitzel über dieses Haus berichtet? (Spricht sich herum – öffentliches Wissen.)</summary>
    public bool WurdeBeobachtet(Konto konto) => _beobachtet.Contains(konto);

    public Verfahren? VerfahrenGegen(Konto anklaeger, Konto beklagter) =>
        _verfahren.FirstOrDefault(v => v.Anklaeger == anklaeger && v.Beklagter == beklagter);

    /// <summary>Steht das Haus am Pranger? Dann so viel weniger Ansehen.</summary>
    public double Schande(Konto konto) =>
        _d is not null && _prangerBis.TryGetValue(konto, out var bis) && _w.Jahr <= bis ? _d.PrangerAnsehen : 0;

    public int? PrangerBis(Konto konto) => Schande(konto) > 0 ? _prangerBis[konto] : null;

    /// <summary>Sitzt das Oberhaupt im Kerker? Dann sind persönliche Befehle gesperrt (siehe Stadtwirtschaft.Ausfuehren).</summary>
    public bool ImKerker(Konto konto) => _kerkerBis.TryGetValue(konto, out var bis) && _w.Jahr <= bis;

    public int? KerkerBis(Konto konto) => ImKerker(konto) ? _kerkerBis[konto] : null;

    public double Beweiskraft(Konto anklaeger, Konto beklagter) => Beweise(anklaeger, beklagter).Count() * Daten.BeweiskraftJeBeweis;

    /// <summary>
    /// Überzeugung eines Richters. Ein Ratsherr lässt sich von seiner Gunst leiten (Geschenke wirken also auch hier),
    /// ein Haus auf dem Richterstuhl urteilt nach den Beweisen.
    /// </summary>
    public double Ueberzeugung(Richter richter, Konto anklaeger, Konto beklagter)
    {
        var kraft = Beweiskraft(anklaeger, beklagter);
        if (richter.Ratsherr is not { } r) return kraft;
        return kraft + Gunstwirkung(r, anklaeger, beklagter);
    }

    /// <summary>
    /// Wie stark Gunst (Geschenke) und Plädoyer (Rhetorik) das Urteil eines Ratsherrn verschieben –
    /// zusammen gedeckelt auf ± GunstHoechstwirkung. So bleiben 3 Beweise ein sicherer Schuldspruch.
    /// </summary>
    public double Gunstwirkung(Ratsherr r, Konto anklaeger, Konto beklagter)
    {
        var d = Daten;
        var einfluss = d.GunstFaktor * (_w.Rat.Gunst(r, anklaeger) - _w.Rat.Gunst(r, beklagter)) + _w.Familien.Plaedoyer(anklaeger, beklagter);
        return Math.Clamp(einfluss, -d.GunstHoechstwirkung, d.GunstHoechstwirkung);
    }

    /// <summary>Welche Buße bei einem Schuldspruch fällig würde (nach den Beweisen, die der Ankläger heute hat).</summary>
    public double DrohendeBusse(Konto anklaeger, Konto beklagter) =>
        Math.Max(Daten.Mindestbusse, Daten.BusseFaktor * Beweise(anklaeger, beklagter).Sum(b => b.Betrag));

    /// <summary>Führten die Beweise in den Kerker?</summary>
    public bool DrohtKerker(Konto anklaeger, Konto beklagter) =>
        Daten.KerkerJahre > 0 && Beweise(anklaeger, beklagter).Any(b => Daten.KerkerFuer.Contains(b.Art));

    /// <summary>Wie das Urteil heute ausfiele – dieselbe Rechnung wie am Gerichtstag.</summary>
    public IReadOnlyList<Stimme> Prognose(Verfahren v) => v.Richter
        .Select(r => { var u = Ueberzeugung(r, v.Anklaeger, v.Beklagter); return new Stimme(r, u, u >= Daten.SchuldSchwelle - 1e-9); })
        .ToList();

    public static bool IstSchuldig(IReadOnlyList<Stimme> stimmen) => stimmen.Count(s => s.Schuldig) * 2 > stimmen.Count;

    /// <summary>Wer den Vorsitz hätte: der Stadtrichter – ist er selbst Partei, der Bürgermeister.</summary>
    public Richter? VorsitzFuer(Konto anklaeger, Konto beklagter)
    {
        foreach (var (amtName, rolle) in new[] { ("Stadtrichter", "Vorsitz (Stadtrichter)"), ("Bürgermeister", "Vorsitz (Bürgermeister)") })
        {
            if (_w.Rat.AmtNamens(amtName) is not { IstBesetzt: true } amt) continue;
            if (amt.Konto is { } k && (k == anklaeger || k == beklagter)) continue;
            return new Richter(rolle, amt.Ratsherr, amt.Konto);
        }
        return null;
    }

    /// <summary>
    /// Wie stünde es vor einer Anklage? Der Vorsitz steht fest, die Schöffen nicht –
    /// für sie zählt die Rechnung ohne Gunst („neutrale Schöffen").
    /// </summary>
    public (Stimme? Vorsitz, bool NeutralSchuldig) PrognoseVorAnklage(Konto anklaeger, Konto beklagter)
    {
        var vorsitz = VorsitzFuer(anklaeger, beklagter);
        Stimme? stimme = null;
        if (vorsitz is not null)
        {
            var u = Ueberzeugung(vorsitz, anklaeger, beklagter);
            stimme = new Stimme(vorsitz, u, u >= Daten.SchuldSchwelle - 1e-9);
        }
        return (stimme, Beweiskraft(anklaeger, beklagter) >= Daten.SchuldSchwelle - 1e-9);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Taten und Befehle
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Eine Tat hinterlässt eine Spur (aufgerufen z. B. vom Stadtrat beim Geschenk).</summary>
    internal Spur? TatMelden(Konto taeter, Vergehen art, string betroffener, double betrag)
    {
        if (!IstAktiv) return null;
        var spur = new Spur(_naechsteSpur++, taeter, art, betroffener, betrag, _w.Jahr);
        _spuren.Add(spur);
        return spur;
    }

    /// <summary>Ablass: Die Spur verfällt sofort – mit allen Beweisen, die andere dafür hatten.</summary>
    internal void Lossprechen(Spur spur) => SpurenEntfernen(s => s.Nummer == spur.Nummer);

    /// <summary>Wer gegen dieses Haus Beweise für diese Spur hat (ohne den Täter selbst).</summary>
    public int BeweiseFuer(Spur spur) => _beweise.Count(b => b.Spur == spur.Nummer);

    /// <summary>Hat <paramref name="inhaber"/> schon einen Beweis für diese Spur?</summary>
    public bool KenntSpur(Konto inhaber, Spur spur) => _beweise.Contains((inhaber, spur.Nummer));

    /// <summary>Ein Zeuge (oder ein Erpresserbrief): <paramref name="inhaber"/> hat sofort einen Beweis für diese Spur.</summary>
    internal void BeweisGeben(Konto inhaber, Spur spur)
    {
        if (inhaber != spur.Taeter) _beweise.Add((inhaber, spur.Nummer));
    }

    /// <summary>Ein Erpresser hält Wort: Seine Beweise gegen das Opfer verbrennen.</summary>
    internal void BeweiseVerbrennen(Konto inhaber, Konto gegen) =>
        _beweise.RemoveWhere(b => b.Inhaber == inhaber && _spuren.Any(s => s.Nummer == b.Spur && s.Taeter == gegen));

    internal Befehlsergebnis SpitzelAnsetzen(Konto auftraggeber, Konto ziel)
    {
        if (ziel == auftraggeber) return Befehlsergebnis.Abgelehnt("Auf das eigene Haus setzt man keinen Spitzel an.");
        if (!ziel.IstAktiv) return Befehlsergebnis.Abgelehnt($"{ziel.Inhaber} ist nicht mehr im Spiel.");
        if (HatSpitzel(auftraggeber, ziel)) return Befehlsergebnis.Abgelehnt($"Auf {ziel.Inhaber} ist dieses Jahr schon ein Spitzel angesetzt.");
        var kosten = Daten.SpitzelKosten;
        if (auftraggeber.Vermoegen < kosten)
            return Befehlsergebnis.Abgelehnt($"Ein Spitzel kostet {Zahlenformat.Ganz(kosten)} {_w.Daten.Allgemein.Waehrung} – so viel hat {auftraggeber.Inhaber} nicht.");
        auftraggeber.Gerichtskosten(kosten);
        _spitzel.Add((auftraggeber, ziel));
        return Befehlsergebnis.Ok($"Ein Spitzel heftet sich an die Fersen von {ziel.Inhaber}. Bericht am Jahresende.");
    }

    internal Befehlsergebnis Anklagen(Konto anklaeger, Konto beklagter)
    {
        if (beklagter == anklaeger) return Befehlsergebnis.Abgelehnt("Sich selbst klagt man nicht an.");
        if (!beklagter.IstAktiv) return Befehlsergebnis.Abgelehnt($"{beklagter.Inhaber} ist nicht mehr im Spiel.");
        if (VerfahrenGegen(anklaeger, beklagter) is not null)
            return Befehlsergebnis.Abgelehnt($"Die Anklage gegen {beklagter.Inhaber} ist schon erhoben. Verhandelt wird am Jahresende.");
        var beweise = Beweise(anklaeger, beklagter).Count();
        if (beweise == 0)
            return Befehlsergebnis.Abgelehnt($"Ohne Beweise gegen {beklagter.Inhaber} nimmt das Gericht keine Anklage an. Setzt einen Spitzel an!");
        if (anklaeger.Vermoegen < Daten.Gerichtsgebuehr)
            return Befehlsergebnis.Abgelehnt($"Die Gerichtsgebühr beträgt {Zahlenformat.Ganz(Daten.Gerichtsgebuehr)} {_w.Daten.Allgemein.Waehrung}.");
        if (VorsitzFuer(anklaeger, beklagter) is not { } vorsitz)
            return Befehlsergebnis.Abgelehnt("Es gibt niemanden, der den Vorsitz führen könnte.");

        anklaeger.Gerichtskosten(Daten.Gerichtsgebuehr);
        var richter = new List<Richter> { vorsitz };
        richter.AddRange(SchoeffenAuslosen(vorsitz).Select(r => new Richter("Schöffe", r, null)));
        var v = new Verfahren(anklaeger, beklagter, _w.Jahr, richter);
        _verfahren.Add(v);
        _w.Melden($"{anklaeger.Inhaber} klagt {beklagter.Inhaber} wegen {VergehenTexte.Aufzaehlung(Beweise(anklaeger, beklagter).Select(b => b.Art))} an " +
                  $"({beweise} {(beweise == 1 ? "Beweis" : "Beweise")}). " +
                  $"Es richten {string.Join(", ", richter.Select(r => r.Name))}. Gerichtstag ist am Jahresende.");
        return Befehlsergebnis.Ok($"Anklage gegen {beklagter.Inhaber} erhoben. Schöffen: {string.Join(" und ", richter.Skip(1).Select(r => r.Name))}.");
    }

    /// <summary>Zwei Schöffen: zuerst aus den Ratsherren ohne Amt, sonst aus den übrigen Ratsherren.</summary>
    private List<Ratsherr> SchoeffenAuslosen(Richter vorsitz)
    {
        var rat = _w.Rat;
        var ohneAmt = rat.Ratsherren.Where(r => rat.Aemter.All(a => a.Ratsherr != r) && r != vorsitz.Ratsherr).ToList();
        var mitAmt = rat.Ratsherren.Where(r => !ohneAmt.Contains(r) && r != vorsitz.Ratsherr).ToList();
        var gewaehlt = new List<Ratsherr>();
        foreach (var topf in new[] { ohneAmt, mitAmt })
            while (gewaehlt.Count < 2 && topf.Count > 0)
            {
                var r = topf[_zufall.Next(topf.Count)];
                topf.Remove(r);
                gewaehlt.Add(r);
            }
        return gewaehlt;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Gerichtstag
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Am Jahresende, vor Amtseinkommen und Ratssitzung: Urteile, Spitzelberichte, Verjährung.</summary>
    internal void Gerichtstag()
    {
        if (!IstAktiv) return;
        var d = Daten;
        var jahr = _w.Jahr;
        var waehrung = _w.Daten.Allgemein.Waehrung;
        _letzterGerichtstag.Clear();
        _letzteUrteile.Clear();

        foreach (var v in _verfahren)
        {
            if (!v.Beklagter.IstAktiv || !v.Anklaeger.IstAktiv)
            {
                Eintrag($"Gerichtstag {jahr}: Das Verfahren {v.Anklaeger.Inhaber} gegen {v.Beklagter.Inhaber} wird eingestellt.");
                continue;
            }
            var beweise = Beweise(v.Anklaeger, v.Beklagter).ToList();
            var stimmen = Prognose(v);
            var schuldig = IstSchuldig(stimmen);
            var zaehlung = $"Anklage: {v.Anklaeger.Inhaber}; {stimmen.Count(s => s.Schuldig)} von {stimmen.Count} Richtern: schuldig";
            string text;
            if (schuldig)
            {
                var busse = Math.Max(d.Mindestbusse, d.BusseFaktor * beweise.Sum(b => b.Betrag));
                v.Beklagter.Gerichtskosten(busse);
                var folgen = new List<string> { $"Buße {Zahlenformat.Ganz(busse)} {waehrung}" };
                if (d.Amtsverlust && _w.Rat.AmtVon(v.Beklagter) is { } amt)
                {
                    _w.Rat.Ausscheiden(v.Beklagter);
                    folgen.Add($"Verlust des Amtes {amt.Name}");
                }
                if (d.PrangerJahre > 0)
                {
                    _prangerBis[v.Beklagter] = jahr + d.PrangerJahre;
                    folgen.Add($"{d.PrangerJahre} Jahre am Pranger");
                }
                if (d.KerkerJahre > 0 && beweise.Any(b => d.KerkerFuer.Contains(b.Art)))
                {
                    _kerkerBis[v.Beklagter] = jahr + d.KerkerJahre;
                    folgen.Add($"{d.KerkerJahre} {(d.KerkerJahre == 1 ? "Jahr" : "Jahre")} Kerker");
                }
                // Die bestochenen Ratsherren rücken öffentlich ab.
                foreach (var b in beweise.Where(b => b.Art == Vergehen.Bestechung))
                    if (_w.Rat.RatsherrNamens(b.Betroffener) is { } r)
                        _w.Rat.GunstEntziehen(r, v.Beklagter);
                text = $"Gerichtstag {jahr}: {v.Beklagter.Inhaber} ist {VergehenTexte.Aufzaehlung(beweise.Select(b => b.Art))} SCHULDIG ({zaehlung}) – {string.Join(", ", folgen)}.";
            }
            else
            {
                v.Anklaeger.Gerichtskosten(d.Entschaedigung);
                v.Beklagter.Gerichtskosten(-d.Entschaedigung);
                text = $"Gerichtstag {jahr}: {v.Beklagter.Inhaber} wird freigesprochen ({zaehlung}). " +
                       $"{v.Anklaeger.Inhaber} zahlt {Zahlenformat.Ganz(d.Entschaedigung)} {waehrung} Entschädigung.";
            }
            Eintrag(text);
            _letzteUrteile.Add(new Urteil(v.Anklaeger, v.Beklagter, schuldig, stimmen.Count(s => s.Schuldig), text));

            // Niemand wird zweimal für dieselbe Tat angeklagt: verhandelte Spuren sind erledigt.
            var erledigt = beweise.Select(b => b.Nummer).ToHashSet();
            SpurenEntfernen(s => erledigt.Contains(s.Nummer));
        }
        _verfahren.Clear();

        // Spitzelberichte
        _beobachtet.Clear();
        foreach (var (auftraggeber, ziel) in _spitzel)
        {
            var gefunden = 0;
            foreach (var spur in _spuren.Where(s => s.Taeter == ziel && !_beweise.Contains((auftraggeber, s.Nummer))).ToList())
                if (_zufall.NextDouble() < d.SpitzelChance)
                {
                    _beweise.Add((auftraggeber, spur.Nummer));
                    gefunden++;
                }
            if (!_beobachtet.Contains(ziel)) _beobachtet.Add(ziel);
            var gesamt = Beweise(auftraggeber, ziel).Count();
            Eintrag($"Der Spitzel von {auftraggeber.Inhaber} berichtet über {ziel.Inhaber}: " +
                    (gefunden == 0 ? "nichts Neues" : $"{gefunden} {(gefunden == 1 ? "neuer Beweis" : "neue Beweise")}") +
                    $" (insgesamt {gesamt}).");
        }
        _spitzel.Clear();

        // Die dunkle Ecke der Schänken: Wer ausschenkt, hört Gerüchte über alle anderen Häuser.
        if (_w.Gastwirtschaft.IstAktiv)
        {
            var chance = _w.Gastwirtschaft.Daten.DunkleEcke.Chance;
            foreach (var (lauscher, schaenke) in _w.Gastwirtschaft.DunkleEcken().ToList())
            {
                var gefunden = 0;
                foreach (var spur in _spuren.Where(s => s.Taeter != lauscher && !_beweise.Contains((lauscher, s.Nummer))).ToList())
                    if (_zufall.NextDouble() < chance)
                    {
                        _beweise.Add((lauscher, spur.Nummer));
                        gefunden++;
                    }
                if (gefunden > 0)
                    Eintrag($"In der dunklen Ecke von {schaenke.Name} hört {lauscher.Inhaber} Gerüchte: " +
                            $"{gefunden} {(gefunden == 1 ? "neuer Beweis" : "neue Beweise")}.");
            }
        }

        // Verjährung: Eine Tat aus dem Jahr J verfällt nach dem Gerichtstag des Jahres J + Verjährung − 1.
        SpurenEntfernen(s => s.Jahr <= jahr + 1 - d.VerjaehrungJahre);
        foreach (var k in _prangerBis.Where(p => p.Value <= jahr).Select(p => p.Key).ToList())
            _prangerBis.Remove(k);
        foreach (var k in _kerkerBis.Where(p => p.Value <= jahr).Select(p => p.Key).ToList())
            _kerkerBis.Remove(k);
    }

    private void Eintrag(string text)
    {
        _letzterGerichtstag.Add(text);
        _w.Melden(text);
    }

    private void SpurenEntfernen(Func<Spur, bool> bedingung)
    {
        var weg = _spuren.Where(bedingung).Select(s => s.Nummer).ToHashSet();
        if (weg.Count == 0) return;
        _spuren.RemoveAll(s => weg.Contains(s.Nummer));
        _beweise.RemoveWhere(b => weg.Contains(b.Spur));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Speichern
    // ═════════════════════════════════════════════════════════════════════════

    internal JustizStand Sichern(Func<Konto, int> nr) => new()
    {
        ZufallWuerfe = _zufall.Wuerfe,
        NaechsteSpur = _naechsteSpur,
        Spuren = _spuren.Select(s => new SpurStand
        {
            Nummer = s.Nummer, Taeter = nr(s.Taeter), Art = s.Art, Betroffener = s.Betroffener, Betrag = s.Betrag, Jahr = s.Jahr,
        }).ToList(),
        Beweise = _beweise.OrderBy(b => nr(b.Inhaber)).ThenBy(b => b.Spur)
            .Select(b => new BeweisStand { Inhaber = nr(b.Inhaber), Spur = b.Spur }).ToList(),
        Spitzel = _spitzel.Select(s => new SpitzelStand { Auftraggeber = nr(s.Auftraggeber), Ziel = nr(s.Ziel) }).ToList(),
        Verfahren = _verfahren.Select(v => new VerfahrenStand
        {
            Anklaeger = nr(v.Anklaeger), Beklagter = nr(v.Beklagter), Jahr = v.Jahr,
            Richter = v.Richter.Select(r => new RichterStand { Rolle = r.Rolle, Ratsherr = r.Ratsherr?.Name, Konto = r.Konto is null ? null : nr(r.Konto) }).ToList(),
        }).ToList(),
        PrangerBis = _prangerBis.OrderBy(p => nr(p.Key)).ToDictionary(p => nr(p.Key).ToString(), p => p.Value),
        KerkerBis = _kerkerBis.OrderBy(p => nr(p.Key)).ToDictionary(p => nr(p.Key).ToString(), p => p.Value),
        LetzterGerichtstag = new(_letzterGerichtstag),
        Beobachtet = _beobachtet.Select(nr).ToList(),
        LetzteUrteile = _letzteUrteile.Select(u => new UrteilStand
        {
            Anklaeger = nr(u.Anklaeger), Beklagter = nr(u.Beklagter), Schuldig = u.Schuldig, Schuldstimmen = u.Schuldstimmen, Text = u.Text,
        }).ToList(),
    };

    internal void Wiederherstellen(JustizStand s, IReadOnlyList<Konto> konten)
    {
        _zufall = new Zufallsquelle(_zufall.Startwert, s.ZufallWuerfe);
        _naechsteSpur = s.NaechsteSpur;
        _spuren.Clear();
        _spuren.AddRange(s.Spuren.Select(x => new Spur(x.Nummer, konten[x.Taeter], x.Art, x.Betroffener, x.Betrag, x.Jahr)));
        _beweise.Clear();
        foreach (var b in s.Beweise) _beweise.Add((konten[b.Inhaber], b.Spur));
        _spitzel.Clear();
        _spitzel.AddRange(s.Spitzel.Select(x => (konten[x.Auftraggeber], konten[x.Ziel])));
        _verfahren.Clear();
        _verfahren.AddRange(s.Verfahren.Select(v => new Verfahren(konten[v.Anklaeger], konten[v.Beklagter], v.Jahr,
            v.Richter.Select(r => new Richter(r.Rolle, r.Ratsherr is { } n ? _w.Rat.RatsherrNamens(n) : null, r.Konto is int k ? konten[k] : null)).ToList())));
        _prangerBis.Clear();
        foreach (var (nummer, bis) in s.PrangerBis) _prangerBis[konten[int.Parse(nummer)]] = bis;
        _kerkerBis.Clear();
        foreach (var (nummer, bis) in s.KerkerBis) _kerkerBis[konten[int.Parse(nummer)]] = bis;
        _letzterGerichtstag.Clear();
        _letzterGerichtstag.AddRange(s.LetzterGerichtstag);
        _beobachtet.Clear();
        _beobachtet.AddRange(s.Beobachtet.Select(n => konten[n]));
        _letzteUrteile.Clear();
        _letzteUrteile.AddRange(s.LetzteUrteile.Select(u => new Urteil(konten[u.Anklaeger], konten[u.Beklagter], u.Schuldig, u.Schuldstimmen, u.Text)));
    }
}
