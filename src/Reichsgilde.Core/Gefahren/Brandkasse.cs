using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Gefahren;

/// <summary>
/// Die städtische Brandkasse (Abschnitt <c>blitz.brandkasse</c>, Nutzer 22.11.2026, Variante E1): ein eigener Topf, nicht die Stadtkasse.
///   • Beitreten und austreten mit den Befehlen <see cref="BrandkasseBeitreten"/> und <see cref="BrandkasseAustreten"/>. Nicht, solange ein
///     eigenes Gebäude brennt oder das Wohnhaus nach einem Feuer instand gesetzt wird.
///   • Am Jahresende zahlt jedes Mitglied <see cref="BrandkasseDaten.BeitragAnteil"/> vom Bauwert seiner Betriebe und vom Wert seines
///     Wohnhauses, mindestens <see cref="BrandkasseDaten.Mindestbeitrag"/>, in den Topf. Wer ihn nicht zahlen kann, scheidet aus.
///   • Gedeckt ist ein Mitglied ab dem Jahr nach dem Beitritt. Brennt dann seine Werkstatt oder sein Wohnhaus (Blitz, Übersprung), zahlt die
///     Kasse <see cref="BrandkasseDaten.Deckung"/> der Reparatur – höchstens so viel, wie im Topf ist. Das verbrannte Lager zahlt sie nicht.
///   • Ein Austritt gibt nichts zurück. Bankrotte und erloschene Häuser scheiden zum Jahresende aus.
/// Ohne Abschnitt gibt es keine Brandkasse (Daten bis 94).
/// </summary>
public sealed class Brandkasse
{
    private sealed record Mitglied(Konto Konto, int SeitJahr);

    private readonly Stadtwirtschaft _w;
    private readonly List<Mitglied> _mitglieder = new(); // in der Reihenfolge des Beitritts

    internal Brandkasse(Stadtwirtschaft w, BrandkasseDaten? daten)
    {
        _w = w;
        Daten = daten;
    }

    public BrandkasseDaten? Daten { get; }

    /// <summary>Gibt es eine Brandkasse? (Abschnitt „blitz.brandkasse“.)</summary>
    public bool IstAktiv => Daten is not null;

    /// <summary>So viele Gulden liegen im Topf.</summary>
    public double Topf { get; private set; }

    /// <summary>Ist dieses Haus Mitglied (gedeckt oder noch nicht)?</summary>
    public bool IstMitglied(Konto k) => _mitglieder.Any(m => m.Konto == k);

    /// <summary>Zahlt die Kasse, wenn es bei diesem Haus brennt? Ab dem Jahr nach dem Beitritt.</summary>
    public bool IstGedeckt(Konto k) => IstAktiv && _mitglieder.Any(m => m.Konto == k && m.SeitJahr < _w.Jahr);

    /// <summary>Die Mitglieder in der Reihenfolge ihres Beitritts.</summary>
    public IReadOnlyList<Konto> Mitglieder => _mitglieder.Select(m => m.Konto).ToList();

    /// <summary>Der Beitrag dieses Hauses am Jahresende: Anteil vom Bauwert der Betriebe und vom Wert des Wohnhauses, mindestens der Mindestbeitrag.</summary>
    public double Beitrag(Konto k)
    {
        if (Daten is not { } d) return 0;
        var wert = _w.Wohnen.Hauswert(k);
        foreach (var b in _w.Betriebe)
            if (b.Konto == k) wert += _w.Bauwert(b);
        return Math.Round(Math.Max(d.Mindestbeitrag, wert * d.BeitragAnteil));
    }

    /// <summary>Warum dieses Haus jetzt nicht beitreten darf – oder null.</summary>
    public string? BeitrittVerboten(Konto k)
    {
        if (!IstAktiv) return "In dieser Stadt gibt es keine Brandkasse.";
        if (IstMitglied(k)) return $"{k.Inhaber} ist schon in der Brandkasse.";
        if (BrenntBei(k)) return "Wenn es schon brennt, nimmt die Brandkasse niemanden mehr auf.";
        return null;
    }

    /// <summary>Brennt ein Gebäude dieses Hauses, oder wird sein Wohnhaus nach einem Feuer instand gesetzt?</summary>
    private bool BrenntBei(Konto k)
    {
        if (k.WohnhausInstandBis is not null) return true;
        var blitz = _w.Blitz;
        if (!blitz.IstAktiv) return false;
        var jetzt = _w.Uhr.VergangeneMinuten;
        return blitz.Einschlaege.Any(e => e.Ziel.Besitzer == k && e.BrenntUm(jetzt));
    }

    internal Befehlsergebnis Beitreten(Konto k)
    {
        if (BeitrittVerboten(k) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        _mitglieder.Add(new Mitglied(k, _w.Jahr));
        var waehrung = _w.Daten.Allgemein.Waehrung;
        return Befehlsergebnis.Ok($"{k.Inhaber} tritt der Brandkasse bei: am Jahresende {Zahlenformat.Ganz(Beitrag(k))} {waehrung} Beitrag, " +
                                  $"gedeckt ab {_w.Jahr + 1}.");
    }

    internal Befehlsergebnis Austreten(Konto k)
    {
        if (!IstAktiv) return Befehlsergebnis.Abgelehnt("In dieser Stadt gibt es keine Brandkasse.");
        if (_mitglieder.RemoveAll(m => m.Konto == k) == 0) return Befehlsergebnis.Abgelehnt($"{k.Inhaber} ist nicht in der Brandkasse.");
        return Befehlsergebnis.Ok($"{k.Inhaber} tritt aus der Brandkasse aus. Die Beiträge bleiben im Topf.");
    }

    /// <summary>
    /// Ein Feuer bei diesem Haus: Ist es gedeckt, zahlt die Kasse ihren Anteil an der Reparatur (ganze Gulden, höchstens der Topf). Gibt den
    /// erstatteten Betrag zurück; der Aufrufer bucht nur den Rest als Schaden.
    /// </summary>
    internal double Erstatten(Konto k, double reparatur)
    {
        if (!IstGedeckt(k) || reparatur <= 0) return 0;
        var betrag = Math.Min(Math.Round(reparatur * Daten!.Deckung), Math.Floor(Topf));
        if (betrag <= 0) return 0;
        Topf -= betrag;
        return betrag;
    }

    /// <summary>Am Jahresende (noch ins alte Jahr): Beiträge einziehen; wer nicht zahlen kann, bankrott oder erloschen ist, scheidet aus.</summary>
    internal void Jahresende()
    {
        if (!IstAktiv) return;
        var waehrung = _w.Daten.Allgemein.Waehrung;
        foreach (var m in _mitglieder.ToList())
        {
            var k = m.Konto;
            if (!k.IstAktiv)
            {
                _mitglieder.Remove(m);
                continue;
            }
            var beitrag = Beitrag(k);
            if (k.Vermoegen < beitrag)
            {
                _mitglieder.Remove(m);
                _w.Melden(Meldungsart.Sonstiges, $"{k.Inhaber} kann den Beitrag zur Brandkasse ({Zahlenformat.Ganz(beitrag)} {waehrung}) nicht zahlen und scheidet aus.", haus: k);
                continue;
            }
            k.Fixkosten(beitrag);
            Topf += beitrag;
        }
    }

    // ── Spielstand ──────────────────────────────────────────────────────────

    internal BrandkasseStand? Sichern(Func<Konto, int> nummer) => !IstAktiv ? null : new BrandkasseStand
    {
        Topf = Topf,
        Mitglieder = _mitglieder.Select(m => new BrandkassenmitgliedStand { Konto = nummer(m.Konto), SeitJahr = m.SeitJahr }).ToList(),
    };

    internal void Wiederherstellen(BrandkasseStand? s, IReadOnlyList<Konto> konten)
    {
        _mitglieder.Clear();
        Topf = s?.Topf ?? 0;
        if (s is null) return;
        foreach (var m in s.Mitglieder)
            if (m.Konto >= 0 && m.Konto < konten.Count) _mitglieder.Add(new Mitglied(konten[m.Konto], m.SeitJahr));
    }
}
