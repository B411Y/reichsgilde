using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Politik;

/// <summary>Die vier Viertel der Stadt, von der Mitte der Karte aus gesehen (Norden = −z).</summary>
public enum Stadtviertel
{
    Nord,
    Ost,
    Sued,
    West,
}

/// <summary>Was der Marktmeister über eine Ware sieht: den Preis jetzt und in einer Stunde, wenn bis dahin niemand verkauft.</summary>
public sealed record Marktvorschau(string Ware, double Nachfrage, double PreisJetzt, double PreisNaechsteStunde);

/// <summary>
/// Die Befugnisse der Ämter (PR #200; Abschnitt „politik.befugnisse“). Sie gelten für Häuser im Amt – ein Ratsherr nutzt sie nicht.
/// Alle setzen eine Stadtkasse voraus (<see cref="Stadtkasse.IstAktiv"/>); ohne sie verläuft eine Partie genau wie zuvor.
///   • Bürgermeister: einmal im Jahr einen Antrag ohne Aktionspunkte; gibt bei Gleichstand den Ausschlag (wie bisher).
///   • Kämmerer: sieht den Haushalt (<see cref="Stadtkasse.HaushaltVerboten"/>), beantragt den Steuersatz ohne Aktionspunkte.
///   • Stadtrichter: ändert im Vorsitz die Buße eines Verfahrens um bis zu ±25 % (Befehl <see cref="StrafmassSetzen"/>).
///   • Stadthauptmann: verlegt die Streife in ein Viertel – dort wirkt der Schutz der Wache doppelt (Befehl <see cref="StreifeVerlegen"/>).
///   • Marktmeister: sieht die Preise der nächsten Stunde (<see cref="Marktvorschau"/>) und zahlt auf eigene Marktverkäufe nur die
///     Hälfte der Umsatzsteuer.
///   • Gefängniswärter: lässt Gefangene gegen eine Kaution vorzeitig frei – das Geld bekommt er (Befehl <see cref="GefangenenFreilassen"/>).
///   • Zöllner: zahlt am eigenen Hafen keinen Zoll (die erste Zollstelle jeder Fahrt gehört der Stadt).
/// </summary>
public sealed class Amtsbefugnisse
{
    public const string Buergermeister = "Bürgermeister";
    public const string Kaemmerer = "Kämmerer";
    public const string Stadtrichter = "Stadtrichter";
    public const string Stadthauptmann = "Stadthauptmann";
    public const string Marktmeister = "Marktmeister";
    public const string Gefaengniswaerter = "Gefängniswärter";
    public const string Zoellner = "Zöllner";

    private readonly Stadtwirtschaft _w;
    private readonly BefugnisDaten _d;
    private (Konto Konto, int Jahr)? _freierAntrag;
    private (Konto Konto, Stadtviertel Viertel)? _streife;
    private readonly Dictionary<(Konto Anklaeger, Konto Beklagter), (Konto Richter, int Prozent)> _strafmass = new();
    private (double X, double Z)? _mitte;

    internal Amtsbefugnisse(Stadtwirtschaft w, BefugnisDaten daten)
    {
        _w = w;
        _d = daten;
    }

    public BefugnisDaten Daten => _d;
    public bool IstAktiv => _w.Kasse.IstAktiv;

    /// <summary>Hält dieses Haus das Amt? (nur mit Stadtkasse – ohne sie gibt es keine Befugnisse)</summary>
    public bool HatAmt(Konto konto, string amt) => IstAktiv && _w.Rat.AmtNamens(amt)?.Konto == konto;

    private string? AmtFehlt(Konto konto, string amt) =>
        !IstAktiv ? "In dieser Stadt gibt es keine Stadtkasse und keine Befugnisse der Ämter."
        : HatAmt(konto, amt) ? null : $"Das darf nur der {amt}.";

    // ── Bürgermeister und Kämmerer: Anträge ohne Aktionspunkte ──────────────

    /// <summary>Ist dieser Befehl ein Antrag im Rat (Statut, Stadtglaube, Pflaster, Steuersatz, Bauvorhaben, Fest)?</summary>
    public static bool IstAntrag(Befehl befehl) =>
        befehl is StatutBeantragen or StadtglaubeBeantragen or WegPflasterBeantragen or SteuersatzBeantragen or BauvorhabenBeantragen or FestBeantragen;

    /// <summary>Hat der Bürgermeister seinen freien Antrag dieses Jahr noch?</summary>
    public bool FreierAntragOffen(Konto konto) => HatAmt(konto, Buergermeister) && _freierAntrag != (konto, _w.Jahr);

    /// <summary>
    /// Kostet dieser Antrag das Haus keine Aktionspunkte? Der Kämmerer beantragt den Steuersatz immer frei, der Bürgermeister
    /// einen Antrag im Jahr.
    /// </summary>
    public bool OhneAktionspunkte(Konto konto, Befehl befehl) =>
        befehl is SteuersatzBeantragen && HatAmt(konto, Kaemmerer) || IstAntrag(befehl) && FreierAntragOffen(konto);

    /// <summary>Nach einem erfolgreichen Antrag: Hat ihn der Bürgermeister frei gestellt, ist sein freier Antrag für dieses Jahr verbraucht.</summary>
    internal void AntragGestellt(Konto konto, Befehl befehl)
    {
        if (befehl is SteuersatzBeantragen && HatAmt(konto, Kaemmerer)) return; // der Kämmerer zahlt nie – der Bürgermeister spart seinen
        if (IstAntrag(befehl) && FreierAntragOffen(konto) && _w.ApKosten(befehl) > 0)
            _freierAntrag = (konto, _w.Jahr);
    }

    // ── Stadtrichter ────────────────────────────────────────────────────────

    /// <summary>Darf dieses Haus im Verfahren die Buße ändern? Nur der Stadtrichter im Vorsitz (null = ja, sonst der Grund).</summary>
    public string? StrafmassVerboten(Konto konto, Konto anklaeger, Konto beklagter, int prozent)
    {
        if (AmtFehlt(konto, Stadtrichter) is { } grund) return grund;
        if (!_w.Justiz.IstAktiv || _w.Justiz.VerfahrenGegen(anklaeger, beklagter) is null)
            return $"Ein Verfahren {anklaeger.Inhaber} gegen {beklagter.Inhaber} ist nicht anhängig.";
        if (_w.Justiz.VorsitzFuer(anklaeger, beklagter)?.Konto != konto) return "Im eigenen Verfahren führt der Bürgermeister den Vorsitz.";
        var spanne = (int)Math.Round(_d.RichterSpanne * 100);
        if (Math.Abs(prozent) > spanne) return $"Die Buße lässt sich um höchstens ±{spanne} % ändern.";
        return null;
    }

    /// <summary>Um wie viel Prozent der Stadtrichter die Buße in diesem Verfahren ändert (0 = gar nicht; gilt nur, solange er den Vorsitz hat).</summary>
    public int Strafmass(Konto anklaeger, Konto beklagter) =>
        _strafmass.TryGetValue((anklaeger, beklagter), out var s) && HatAmt(s.Richter, Stadtrichter)
        && _w.Justiz.IstAktiv && _w.Justiz.VorsitzFuer(anklaeger, beklagter)?.Konto == s.Richter ? s.Prozent : 0;

    internal Befehlsergebnis StrafmassSetzen(Konto konto, string anklaegerName, string beklagterName, int prozent)
    {
        if (_w.Konten.FirstOrDefault(k => k.Inhaber == anklaegerName) is not { } anklaeger)
            return Befehlsergebnis.Abgelehnt($"Ein Haus „{anklaegerName}“ gibt es nicht.");
        if (_w.Konten.FirstOrDefault(k => k.Inhaber == beklagterName) is not { } beklagter)
            return Befehlsergebnis.Abgelehnt($"Ein Haus „{beklagterName}“ gibt es nicht.");
        if (StrafmassVerboten(konto, anklaeger, beklagter, prozent) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        if (prozent == 0) _strafmass.Remove((anklaeger, beklagter));
        else _strafmass[(anklaeger, beklagter)] = (konto, prozent);
        return Befehlsergebnis.Ok(prozent == 0
            ? $"Im Verfahren {anklaeger.Inhaber} gegen {beklagter.Inhaber} gilt die gewöhnliche Buße."
            : $"Im Verfahren {anklaeger.Inhaber} gegen {beklagter.Inhaber} fällt die Buße bei einem Schuldspruch {Math.Abs(prozent)} % " +
              $"{(prozent > 0 ? "höher" : "niedriger")} aus.");
    }

    /// <summary>Nach dem Gerichtstag: Die Verfahren sind entschieden.</summary>
    internal void GerichtstagVorbei() => _strafmass.Clear();

    // ── Stadthauptmann ──────────────────────────────────────────────────────

    /// <summary>Wohin die Streife verlegt ist (null = gleichmäßig über die Stadt). Gilt, solange ihr Hauptmann im Amt ist.</summary>
    public Stadtviertel? Streife => _streife is { } s && HatAmt(s.Konto, Stadthauptmann) ? s.Viertel : null;

    public string? StreifeVerboten(Konto konto) => AmtFehlt(konto, Stadthauptmann);

    internal Befehlsergebnis StreifeVerlegen(Konto konto, Stadtviertel? viertel)
    {
        if (StreifeVerboten(konto) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        _streife = viertel is { } v ? (konto, v) : null;
        return Befehlsergebnis.Ok(viertel is { } x
            ? $"Die Streife geht ab sofort vor allem im {ViertelName(x)} – dort schützt die Wache doppelt."
            : "Die Streife geht wieder gleichmäßig durch die ganze Stadt.");
    }

    public static string ViertelName(Stadtviertel v) => v switch
    {
        Stadtviertel.Nord => "Nordviertel",
        Stadtviertel.Ost => "Ostviertel",
        Stadtviertel.Sued => "Südviertel",
        _ => "Westviertel",
    };

    /// <summary>
    /// In welchem Viertel ein Betrieb liegt: von der Mitte aller Gebäude der Karte aus nach der Himmelsrichtung (Norden = −z).
    /// Ohne Karte oder Platz fest nach dem Namen (immer dasselbe Viertel).
    /// </summary>
    public Stadtviertel ViertelVon(Betrieb b)
    {
        if (_w.Bauen.PlatzVon(b.Name) is { } platz && Mitte() is { } m)
        {
            var (dx, dz) = (platz.Punkt.X - m.X, platz.Punkt.Z - m.Z);
            return Math.Abs(dx) > Math.Abs(dz) ? (dx > 0 ? Stadtviertel.Ost : Stadtviertel.West) : (dz < 0 ? Stadtviertel.Nord : Stadtviertel.Sued);
        }
        var summe = 0;
        foreach (var c in b.Name) summe += c;
        return (Stadtviertel)(summe % 4);
    }

    private (double X, double Z)? Mitte()
    {
        if (_mitte is not null) return _mitte;
        if (_w.Karte is not { Gebaeude.Count: > 0 } karte) return null;
        _mitte = (karte.Gebaeude.Average(g => g.Punkt[0]), karte.Gebaeude.Average(g => g.Punkt[1]));
        return _mitte;
    }

    /// <summary>
    /// Schutz der Streife für diesen Betrieb (Anteil, um den nächtliche Taten seltener gelingen): im Viertel der Streife
    /// <c>streifeFaktor</c>-mal so viel, mindestens <c>streifeMindestens</c>, höchstens <c>streifeHoechstens</c>; sonst wie gewöhnlich.
    /// </summary>
    public double StreifeSchutz(Betrieb? b, double gewoehnlich)
    {
        if (b is null || Streife is not { } viertel || ViertelVon(b) != viertel) return gewoehnlich;
        return Math.Max(gewoehnlich, Math.Min(_d.StreifeHoechstens, Math.Max(_d.StreifeMindestens, gewoehnlich * _d.StreifeFaktor)));
    }

    // ── Marktmeister ────────────────────────────────────────────────────────

    public string? MarktvorschauVerboten(Konto konto) => AmtFehlt(konto, Marktmeister);

    /// <summary>Preise jetzt und in einer Stunde je Ware (nur für den Marktmeister; sonst leer).</summary>
    public IReadOnlyList<Marktvorschau> Marktvorschau(Konto konto) =>
        MarktvorschauVerboten(konto) is not null ? Array.Empty<Marktvorschau>()
        : _w.Markt.Staende.OrderBy(s => s.Ware, StringComparer.Ordinal)
            .Select(s => new Marktvorschau(s.Ware, s.Nachfrage, s.Preis, s.PreisNaechsteStunde)).ToList();

    /// <summary>Steuersatz auf eigene Verkäufe am Markt: für den Marktmeister nur <c>marktmeisterSteuer</c> davon.</summary>
    public double SteuersatzMarkt(Konto konto) => _w.Umsatzsteuer * (HatAmt(konto, Marktmeister) ? _d.MarktmeisterSteuer : 1);

    // ── Gefängniswärter ─────────────────────────────────────────────────────

    /// <summary>Kaution für die vorzeitige Freilassung: je Jahr, das der Gefangene noch sitzen müsste.</summary>
    public double Kaution(Konto gefangener) =>
        _w.Justiz.IstAktiv && _w.Justiz.KerkerBis(gefangener) is int bis ? _d.KautionJeJahr * (bis - _w.Jahr + 1) : 0;

    public string? FreilassenVerboten(Konto waerter, Konto gefangener)
    {
        if (AmtFehlt(waerter, Gefaengniswaerter) is { } grund) return grund;
        if (!_w.Justiz.IstAktiv || !_w.Justiz.ImKerker(gefangener)) return $"{gefangener.Inhaber} sitzt nicht im Kerker.";
        if (gefangener == waerter) return "Sich selbst lässt der Wärter nicht frei.";
        var kaution = Kaution(gefangener);
        if (gefangener.Vermoegen < kaution)
            return $"Die Kaution beträgt {Zahlenformat.Ganz(kaution)} {_w.Daten.Allgemein.Waehrung} – so viel hat {gefangener.Inhaber} nicht.";
        return null;
    }

    internal Befehlsergebnis GefangenenFreilassen(Konto waerter, string haus)
    {
        if (_w.Konten.FirstOrDefault(k => k.Inhaber == haus) is not { } gefangener)
            return Befehlsergebnis.Abgelehnt($"Ein Haus „{haus}“ gibt es nicht.");
        if (FreilassenVerboten(waerter, gefangener) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var kaution = Kaution(gefangener);
        gefangener.Gerichtskosten(kaution);
        waerter.Gerichtskosten(-kaution);
        _w.Justiz.Freilassen(gefangener);
        var text = $"Der Gefängniswärter {waerter.Inhaber} lässt das Oberhaupt von {gefangener.Inhaber} gegen " +
                   $"{Zahlenformat.Ganz(kaution)} {_w.Daten.Allgemein.Waehrung} Kaution frei.";
        _w.Melden(text);
        return Befehlsergebnis.Ok(text);
    }

    // ── Zöllner ─────────────────────────────────────────────────────────────

    /// <summary>Zahlt dieses Haus den Zoll der Stadt (erste Zollstelle) nicht? Nur der Zöllner.</summary>
    public bool IstZollfrei(Konto konto) => HatAmt(konto, Zoellner);

    // ── Ausscheiden und Speichern ───────────────────────────────────────────

    internal void Ausscheiden(Konto konto)
    {
        if (_streife?.Konto == konto) _streife = null;
        foreach (var s in _strafmass.Where(s => s.Value.Richter == konto).Select(s => s.Key).ToList()) _strafmass.Remove(s);
    }

    internal BefugnisseStand Sichern(Func<Konto, int> nr) => new()
    {
        FreierAntragKonto = _freierAntrag is { } f ? nr(f.Konto) : null,
        FreierAntragJahr = _freierAntrag?.Jahr ?? 0,
        StreifeKonto = _streife is { } s ? nr(s.Konto) : null,
        StreifeViertel = _streife?.Viertel,
        Strafmass = _strafmass.OrderBy(x => nr(x.Key.Anklaeger)).ThenBy(x => nr(x.Key.Beklagter))
            .Select(x => new StrafmassStand { Anklaeger = nr(x.Key.Anklaeger), Beklagter = nr(x.Key.Beklagter), Richter = nr(x.Value.Richter), Prozent = x.Value.Prozent })
            .ToList(),
    };

    internal void Wiederherstellen(BefugnisseStand? s, IReadOnlyList<Konto> konten)
    {
        if (s is null) return;
        _freierAntrag = s.FreierAntragKonto is int f ? (konten[f], s.FreierAntragJahr) : null;
        _streife = s.StreifeKonto is int k && s.StreifeViertel is { } v ? (konten[k], v) : null;
        foreach (var x in s.Strafmass)
            _strafmass[(konten[x.Anklaeger], konten[x.Beklagter])] = (konten[x.Richter], x.Prozent);
    }
}
