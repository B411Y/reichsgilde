using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Politik;

/// <summary>Ein Posten der Stadtkasse: woher das Geld kommt bzw. wofür es ausgegeben wird.</summary>
public enum Kassenposten
{
    // Einnahmen
    Umsatzsteuer,
    Glaubenszins,
    Strafen,
    Zoelle,
    Gebuehren,

    // Ausgaben
    Amtsgehaelter,
    Bauvorhaben,
    Feste,
}

public static class KassenpostenTexte
{
    public static bool IstEinnahme(this Kassenposten p) => p < Kassenposten.Amtsgehaelter;

    public static string Name(this Kassenposten p) => p switch
    {
        Kassenposten.Umsatzsteuer => "Umsatzsteuer",
        Kassenposten.Glaubenszins => "Glaubenszins",
        Kassenposten.Strafen => "Bußen",
        Kassenposten.Zoelle => "Hafenzoll",
        Kassenposten.Gebuehren => "Gebühren (Gericht, Stand)",
        Kassenposten.Amtsgehaelter => "Amtsgehälter",
        Kassenposten.Bauvorhaben => "Bauvorhaben",
        _ => "Feste",
    };
}

/// <summary>Das Kassenbuch eines Jahres: Beträge je Posten (Einnahmen und Ausgaben positiv).</summary>
public sealed class Kassenbuch
{
    private readonly Dictionary<Kassenposten, double> _posten = new();

    public int Jahr { get; }

    internal Kassenbuch(int jahr) => Jahr = jahr;

    /// <summary>Betrag eines Postens (0, wenn nichts gebucht ist).</summary>
    public double this[Kassenposten p] => _posten.GetValueOrDefault(p);

    /// <summary>Alle gebuchten Posten in der Reihenfolge der Aufzählung (erst Einnahmen, dann Ausgaben).</summary>
    public IReadOnlyList<(Kassenposten Posten, double Betrag)> Posten =>
        _posten.Where(x => Math.Abs(x.Value) > 1e-9).OrderBy(x => x.Key).Select(x => (x.Key, x.Value)).ToList();

    public double Einnahmen => _posten.Where(x => x.Key.IstEinnahme()).Sum(x => x.Value);
    public double Ausgaben => _posten.Where(x => !x.Key.IstEinnahme()).Sum(x => x.Value);
    public double Saldo => Einnahmen - Ausgaben;

    internal void Buchen(Kassenposten p, double betrag) => _posten[p] = this[p] + betrag;

    internal KassenbuchStand Sichern() => new() { Jahr = Jahr, Posten = _posten.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value) };

    internal static Kassenbuch Laden(KassenbuchStand s)
    {
        var b = new Kassenbuch(s.Jahr);
        foreach (var (p, betrag) in s.Posten) b._posten[p] = betrag;
        return b;
    }
}

/// <summary>
/// Die Stadtkasse (PR #200; Abschnitt „politik.kasse“, ab Datenversion 54).
///
///   • Einnahmen: die Umsatzsteuer aller Häuser (jede Buchung „Steuer“ eines Kontos), der Glaubenszins, Bußen des Gerichts,
///     Gerichts- und Standesgebühren und der Zoll an der ersten Zollstelle jeder Schiffsfahrt (sie gehört der Stadt).
///   • Ausgaben: Amtsgehälter aller besetzten Ämter (auch der Ratsherren), Bauvorhaben und Feste.
///   • Der Steuersatz gilt für alle Umsätze (<see cref="Stadtwirtschaft.Umsatzsteuer"/>) und ändert sich nur durch einen Ratsbeschluss.
///   • Eine leere Kasse hat keine Folgen: Die Gehälter werden trotzdem gezahlt (die Kasse darf ins Minus); nur Bau und Fest brauchen Deckung.
/// Ohne den Abschnitt gibt es keine Kasse: Der Steuersatz bleibt <c>allgemein.umsatzsteuer</c>, gebucht wird nichts.
/// </summary>
public sealed class Stadtkasse
{
    private readonly Stadtwirtschaft _w;
    private readonly KasseDaten? _d;
    private bool _beruehrt; // schon gebucht oder geladen – dann setzt eine Karte den Startbestand nicht mehr

    internal Stadtkasse(Stadtwirtschaft w, KasseDaten? daten)
    {
        _w = w;
        _d = daten;
        Stand = daten?.Startbestand ?? 0;
        SteuersatzProzent = (int)Math.Round(w.Daten.Allgemein.Umsatzsteuer * 100);
        DiesesJahr = new Kassenbuch(Zeit.Spielzeit.StartJahr);
    }

    /// <summary>Gibt es eine Stadtkasse? (Abschnitt „politik.kasse“ und ein Stadtrat.)</summary>
    public bool IstAktiv => _d is not null && _w.Rat.IstAktiv;

    public KasseDaten Daten => _d ?? throw new InvalidOperationException("Es gibt keine Stadtkasse (politik.kasse fehlt in den Daten).");

    /// <summary>Geld in der Kasse (Gulden). Kann negativ werden – das hat keine Folgen außer, dass Bau und Fest warten müssen.</summary>
    public double Stand { get; private set; }

    /// <summary>Der geltende Steuersatz in ganzen Prozent.</summary>
    public int SteuersatzProzent { get; private set; }

    /// <summary>Der geltende Steuersatz als Anteil (0,05 = 5 %). Ohne Kasse: <c>allgemein.umsatzsteuer</c>.</summary>
    public double Umsatzsteuer => IstAktiv ? SteuersatzProzent / 100.0 : _w.Daten.Allgemein.Umsatzsteuer;

    /// <summary>Einnahmen und Ausgaben des laufenden Jahres.</summary>
    public Kassenbuch DiesesJahr { get; private set; }

    /// <summary>Einnahmen und Ausgaben des Vorjahres (null im ersten Jahr).</summary>
    public Kassenbuch? Vorjahr { get; private set; }

    /// <summary>Den Haushalt mit allen Posten sieht nur der Kämmerer (null = er darf, sonst der Grund). Stand, Einnahmen und Ausgaben sieht jeder.</summary>
    public string? HaushaltVerboten(Konto konto)
    {
        if (!IstAktiv) return "In dieser Stadt gibt es keine Stadtkasse.";
        if (_w.Rat.AmtNamens(Amtsbefugnisse.Kaemmerer)?.Konto != konto) return "Den Haushalt mit allen Posten sieht nur der Kämmerer.";
        return null;
    }

    /// <summary>Startbestand für diese Karte (<c>startJeKarte</c>, sonst <c>startbestand</c>).</summary>
    public double StartbestandFuer(string? karte) =>
        karte is not null && Daten.StartJeKarte.TryGetValue(karte, out var x) ? x : Daten.Startbestand;

    // ── Buchen ──────────────────────────────────────────────────────────────

    /// <summary>Bucht eine Einnahme oder Ausgabe (Betrag positiv). Ohne Kasse geschieht nichts.</summary>
    internal void Buchen(Kassenposten posten, double betrag)
    {
        if (!IstAktiv || Math.Abs(betrag) < 1e-12) return;
        _beruehrt = true;
        DiesesJahr.Buchen(posten, betrag);
        Stand += posten.IstEinnahme() ? betrag : -betrag;
    }

    /// <summary>Die Karte steht fest: Noch unberührt, beginnt die Kasse mit dem Startbestand dieser Karte.</summary>
    internal void KarteGesetzt(string kennung)
    {
        if (_d is null || _beruehrt) return;
        Stand = StartbestandFuer(kennung);
    }

    /// <summary>Der Rat hat einen neuen Steuersatz beschlossen.</summary>
    internal void SteuersatzSetzen(int prozent) => SteuersatzProzent = prozent;

    /// <summary>
    /// Amtsgehälter: jedes besetzte Amt (Häuser bekommen ihr Gehalt aufs Konto, die Ratsherren auch – sie sind nur nicht im Spiel).
    /// Ohne Kasse wie bisher: nur die Häuser, aus dem Nichts.
    /// </summary>
    internal void GehaelterZahlen()
    {
        foreach (var amt in _w.Rat.Aemter.Where(a => a.IstBesetzt))
        {
            amt.Konto?.Amtseinkommen(amt.Daten.Einkommen);
            Buchen(Kassenposten.Amtsgehaelter, amt.Daten.Einkommen);
        }
    }

    /// <summary>Jahresabschluss: Das laufende Kassenbuch wird zum Vorjahr.</summary>
    internal void JahrAbschliessen(int neuesJahr)
    {
        Vorjahr = DiesesJahr;
        DiesesJahr = new Kassenbuch(neuesJahr);
    }

    // ── Speichern ───────────────────────────────────────────────────────────

    internal StadtkasseStand? Sichern() => _d is null ? null : new StadtkasseStand
    {
        Stand = Stand,
        SteuersatzProzent = SteuersatzProzent,
        DiesesJahr = DiesesJahr.Sichern(),
        Vorjahr = Vorjahr?.Sichern(),
    };

    /// <summary>Alte Spielstände ohne Kasse: Sie beginnt mit dem Startbestand der Karte und dem Steuersatz aus den Daten.</summary>
    internal void Wiederherstellen(StadtkasseStand? s)
    {
        if (_d is null) return;
        if (s is null)
        {
            Stand = StartbestandFuer(_w.Karte?.Kennung);
            DiesesJahr = new Kassenbuch(_w.Jahr);
            _beruehrt = true;
            return;
        }
        Stand = s.Stand;
        SteuersatzProzent = s.SteuersatzProzent;
        DiesesJahr = Kassenbuch.Laden(s.DiesesJahr);
        Vorjahr = s.Vorjahr is null ? null : Kassenbuch.Laden(s.Vorjahr);
        _beruehrt = true;
    }
}
