using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Lagen aller Rohstoffquellen (Datenversion 92, Nutzer 15.11.2026: die Erntejahre gelten für alle Warenketten, nicht nur für den Wirt).
/// Hält die Ernte (<see cref="Erntewesen"/>, erste Quelle) und die Quellen aus dem Abschnitt „lagen“ in fester Reihenfolge, setzt den
/// <see cref="Marktstand.Lagefaktor"/> jeder Ware (Produkt aller Quellen, begrenzt) und den Ertrag der eigenen Umlandbetriebe, und meldet
/// Ansage und Beginn jeder Lage außer der Ernte (die meldet sich selbst).
///
/// Ohne Abschnitt „lagen“ gibt es nur die Ernte, und alles rechnet wie in Datenversion 91 (ohne Ertragswirkung).
/// </summary>
public sealed class Lagenwesen
{
    private readonly Stadtwirtschaft _w;
    private readonly List<Rohstofflage> _quellen = new();
    private readonly double[] _ertrag = new double[Enum.GetValues<Betriebsart>().Max(a => (int)a) + 1];
    private int? _gesetztFuerJahr;

    public LagenDaten? Daten { get; }

    internal Lagenwesen(Stadtwirtschaft w, LagenDaten? daten, Erntewesen ernte, int zufallsstartwert)
    {
        _w = w;
        Daten = daten;
        if (ernte.Quelle is { } e) _quellen.Add(e);
        if (daten is not null && ernte.Daten is { } werte)
            foreach (var q in daten.Quellen)
            {
                var kennung = q.Betrieb.ToString();
                _quellen.Add(new Rohstofflage(w, kennung, werte, q.Waren, q.Jahreszeit, Rohstofflage.SalzAus(kennung), zufallsstartwert,
                    q.Betrieb, daten.Ertrag, q));
            }
    }

    /// <summary>Alle Quellen: zuerst die Ernte, dann die aus dem Abschnitt „lagen“ (leer ohne Abschnitt „ernte“).</summary>
    public IReadOnlyList<Rohstofflage> Quellen => _quellen;

    /// <summary>Die Quelle eines Umlandbetriebs (null, wenn er keine hat).</summary>
    public Rohstofflage? QuelleVon(Betriebsart art) => _quellen.FirstOrDefault(q => q.Betrieb == art);

    /// <summary>Alle Lagen, die jetzt angesagt sind (im Jahr vor ihrem Stichjahr), in der Reihenfolge der Quellen.</summary>
    public IEnumerable<Quellenlage> Angesagt => _quellen.Select(q => q.Angesagt).OfType<Quellenlage>();

    /// <summary>Alle Lagen, die jetzt gelten.</summary>
    public IEnumerable<Quellenlage> Jetzt => _quellen.Select(q => q.Jetzt).OfType<Quellenlage>();

    /// <summary>
    /// Faktor aller Lagen auf die Grundversorgung einer Ware jetzt: das Produkt der Faktoren aller Quellen, begrenzt auf die Grenzen des
    /// Abschnitts „ernte“ (1, wenn keine Quelle die Ware trifft).
    /// </summary>
    public double Faktor(string ware)
    {
        var f = 1.0;
        var betroffen = false;
        foreach (var q in _quellen)
        {
            var e = q.Faktor(ware);
            if (e == 1) continue;
            f *= e;
            betroffen = true;
        }
        if (!betroffen) return 1;
        var werte = _quellen[0].Werte;
        return Math.Clamp(f, werte.UntereGrenze, werte.ObereGrenze);
    }

    /// <summary>Welche Lagen eine Ware jetzt verschieben und wie stark (für Tooltips am Marktstand).</summary>
    public IEnumerable<(Quellenlage Lage, double Faktor)> Gruende(string ware)
    {
        foreach (var q in _quellen)
            if (q.Jetzt is { } l && q.Faktor(ware, l.Guete) is var f && f != 1)
                yield return (l, f);
    }

    /// <summary>Faktor der Lagen auf die Leistung eines Betriebs (1 für Betriebe ohne Quelle oder ohne Abschnitt „lagen“).</summary>
    public double Ertragsfaktor(Betrieb betrieb) => Ertragsfaktor(betrieb.Art);

    /// <summary>Faktor der Lagen auf die Leistung einer Betriebsart im laufenden Jahr.</summary>
    public double Ertragsfaktor(Betriebsart art)
    {
        if (Daten is null) return 1;
        Aktualisieren();
        return _ertrag[(int)art];
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Ablauf
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Zu Beginn jeder Minute und nach dem Laden: der Lagefaktor an jedem Marktstand – nur wenn sich das Jahr geändert hat.</summary>
    internal void MarktSetzen()
    {
        if (_quellen.Count == 0) return;
        Aktualisieren();
    }

    private void Aktualisieren()
    {
        if (_gesetztFuerJahr == _w.Uhr.Jahr) return;
        _gesetztFuerJahr = _w.Uhr.Jahr;
        foreach (var s in _w.Markt.Staende)
            s.Lagefaktor = Faktor(s.Ware);
        Array.Fill(_ertrag, 1.0);
        foreach (var q in _quellen)
            if (q.Betrieb is { } b)
                _ertrag[(int)b] *= q.Ertragsfaktor;
    }

    /// <summary>Jede Spielminute: zu Jahresbeginn Ansage bzw. Beginn jeder Lage außer der Ernte melden (nur gute, schlechte und schwere).</summary>
    internal void Minute(Spielzeit zeit)
    {
        if (zeit.MinuteImTag != 0) return;
        foreach (var q in _quellen)
        {
            if (q.Kennung == "Ernte") continue;
            if (q.AngesagtIm(zeit.Jahr) is { Ereignis: not null } a)
                _w.Melden(Ansage(a));
            else if (q.LageVon(zeit.Jahr) is { Ereignis: not null } l)
                _w.Melden(Beginn(l));
        }
    }

    /// <summary>Die Waren einer Lage als Text, z. B. „Eisen und Silber“.</summary>
    public static string WarenText(Quellenlage l)
    {
        var namen = l.Quelle.Rohwaren(l.Guete).ToList();
        return namen.Count switch
        {
            0 => "",
            1 => namen[0],
            _ => string.Join(", ", namen.Take(namen.Count - 1)) + " und " + namen[^1],
        };
    }

    private static bool Einzeln(Quellenlage l) => l.Quelle.Rohwaren(l.Guete).Count() == 1;
    private static string Werden(Quellenlage l) => Einzeln(l) ? "wird" : "werden";
    private static string Sein(Quellenlage l) => Einzeln(l) ? "ist" : "sind";

    private static string Jahreszeitwort(Jahreszeit j) => j switch
    {
        Jahreszeit.Fruehling => "Frühling",
        Jahreszeit.Sommer => "Sommer",
        Jahreszeit.Herbst => "Herbst",
        _ => "Winter",
    };

    /// <summary>Text der Ansage, z. B. „Die Schneeschmelze drückt Wasser in die Stollen: Im Frühling 1404 droht ein Wassereinbruch …“.</summary>
    public static string Ansage(Quellenlage l)
    {
        var e = l.Ereignis!;
        var wann = $"Im {Jahreszeitwort(l.Quelle.Stichjahreszeit)} {l.Stichjahr}";
        return l.Guete == Lagegute.Gut
            ? $"{e.Vorzeichen}: {wann} kommt wohl {e.Text} – {WarenText(l)} {Werden(l)} billiger."
            : $"{e.Vorzeichen}: {wann} droht {e.Text} – {WarenText(l)} {Werden(l)} {(l.Guete == Lagegute.Schwer ? "deutlich " : "")}teurer. Wer jetzt einlagert, verkauft später besser.";
    }

    /// <summary>Text zum Beginn einer Lage, z. B. „Wassereinbruch 1404: Bis 1407 sind Eisen und Silber knapp und teurer.“</summary>
    public static string Beginn(Quellenlage l)
    {
        var e = l.Ereignis!;
        return l.Guete switch
        {
            Lagegute.Gut => $"{e.Titel} {l.Stichjahr}: Bis {l.LetztesJahr} {Sein(l)} {WarenText(l)} reichlich da und billiger.",
            Lagegute.Schwer => $"{e.Titel} {l.Stichjahr}: Bis {l.LetztesJahr} {Sein(l)} {WarenText(l)} sehr knapp und teuer.",
            _ => $"{e.Titel} {l.Stichjahr}: Bis {l.LetztesJahr} {Sein(l)} {WarenText(l)} knapp und teurer.",
        };
    }
}
