using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>Die Ernte eines Herbstjahres: Sie gilt von diesem Jahr bis zum Jahr vor der nächsten Ernte (vier Spieljahre).</summary>
public sealed record Erntelage(int Erntejahr, Erntegute Guete)
{
    /// <summary>Das letzte Jahr, in dem diese Ernte gilt (das Sommerjahr vor der nächsten Ernte).</summary>
    public int LetztesJahr => Erntejahr + 3;
}

/// <summary>
/// Erntejahr und Teuerung („Härte um 1400“ PR 1, Abschnitt „ernte“, Datenversion 91; Nutzer 15.11.2026):
///   • Jede Ernte im <b>Herbstjahr</b> bekommt eine Güte (gut, gewöhnlich, mager, Missernte). Sie gilt bis zur nächsten Ernte (Herbst, Winter,
///     Frühling, Sommer) und verschiebt die Grundversorgung von Gerste, Roggen und dem, was aus Getreide gemacht wird – also ihren Preis.
///   • <b>Ansage ein Jahr vorher:</b> Im Sommerjahr vor der Ernte steht fest und wird gemeldet, wie sie ausfällt (<see cref="Angesagt"/>).
///     Wer dann Korn einlagert, verkauft es nach einer schlechten Ernte teurer.
///   • <b>Keine Abwärtsspirale:</b> Nach einer Missernte folgt ein Schonjahr (die nächste Ernte ist nie mager und keine Missernte), der Faktor
///     jeder Ware bleibt zwischen <see cref="ErnteDaten.UntereGrenze"/> und <see cref="ErnteDaten.ObereGrenze"/>, dazu die Preisgrenzen des Markts.
///   • Wie oft und wie stark: <see cref="HaerteDerZeit"/> der Partie (<see cref="Stadtwirtschaft.HaerteDerZeit"/>).
///
/// Nichts davon steht im Spielstand: Die Güte jeder Ernte kommt aus dem Startwert der Partie, dem Erntejahr und der Güte der Ernte davor (eigene
/// <see cref="Zufallsquelle"/> je Ernte, sie würfelt genau einmal). Eine geladene Partie und jeder Mitspieler rechnen dieselben Ernten.
/// </summary>
public sealed class Erntewesen
{
    /// <summary>Mischt den Startwert der Partie, damit die Ernten nicht dieselben Würfe wie die Winter bekommen.</summary>
    private const int Salz = 0x4E52_7465;

    private readonly Stadtwirtschaft _w;
    private readonly int _startwert;
    private readonly List<Erntegute> _ernten = new(); // Güte der 1., 2., … Ernte der Partie (nur nach vorn gerechnet)
    private int? _gesetztFuerJahr;

    public ErnteDaten? Daten { get; }

    /// <summary>Gibt es Erntejahre? (Abschnitt „ernte“)</summary>
    public bool IstAktiv => Daten is not null;

    internal Erntewesen(Stadtwirtschaft w, ErnteDaten? daten, int zufallsstartwert)
    {
        _w = w;
        Daten = daten;
        _startwert = zufallsstartwert;
    }

    /// <summary>Das erste Herbstjahr der Partie (1402).</summary>
    public static int ErstesErntejahr => Spielzeit.StartJahr + (int)Jahreszeit.Herbst;

    // ═════════════════════════════════════════════════════════════════════════
    //  Abfragen (für Oberfläche, Darstellung, KI)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Härte der Zeit dieser Partie.</summary>
    public HaerteDerZeit Haerte => _w.HaerteDerZeit;

    /// <summary>Die Ernte, die jetzt gilt – null vor der ersten Ernte (1400, 1401) oder ohne Abschnitt.</summary>
    public Erntelage? Jetzt => GiltIm(_w.Uhr.Jahr);

    /// <summary>
    /// Die Ernte, die angesagt ist: im Sommerjahr die Ernte des kommenden Herbstjahres (ein Jahr vorher), sonst null.
    /// Für den Zeitstrahl, den Haushofmeister und die KI.
    /// </summary>
    public Erntelage? Angesagt => AngesagtIm(_w.Uhr.Jahr);

    /// <summary>Die Ernte, die im Jahr <paramref name="jahr"/> gilt (null vor der ersten Ernte oder ohne Abschnitt).</summary>
    public Erntelage? GiltIm(int jahr)
    {
        if (Daten is null || jahr < ErstesErntejahr) return null;
        var erntejahr = jahr - (jahr - ErstesErntejahr) % 4;
        return ErnteVon(erntejahr);
    }

    /// <summary>Die Ernte, die im Jahr <paramref name="jahr"/> angesagt ist: nur in einem Sommerjahr die des folgenden Herbstjahres.</summary>
    public Erntelage? AngesagtIm(int jahr) => (jahr + 1 - ErstesErntejahr) % 4 == 0 && jahr + 1 >= ErstesErntejahr ? ErnteVon(jahr + 1) : null;

    /// <summary>Die Ernte eines Herbstjahres (null, wenn das Jahr kein Erntejahr ist oder der Abschnitt fehlt).</summary>
    public Erntelage? ErnteVon(int erntejahr)
    {
        if (Daten is null || erntejahr < ErstesErntejahr || (erntejahr - ErstesErntejahr) % 4 != 0) return null;
        var nummer = (erntejahr - ErstesErntejahr) / 4;
        while (_ernten.Count <= nummer)
            _ernten.Add(Wuerfeln(ErstesErntejahr + 4 * _ernten.Count, _ernten.Count > 0 ? _ernten[^1] : Erntegute.Gewoehnlich));
        return new Erntelage(erntejahr, _ernten[nummer]);
    }

    /// <summary>Faktor der Ernte auf die Grundversorgung dieser Ware jetzt (1, wenn die Ware nicht betroffen ist oder keine Ernte gilt).</summary>
    public double Faktor(string ware) => Jetzt is { } l ? Faktor(ware, l.Guete) : 1;

    /// <summary>
    /// Faktor einer Güte auf eine Ware in dieser Partie: 1 + (Faktor der Güte − 1) × Stärke der Stufe × Gewicht der Ware, begrenzt auf
    /// [<see cref="ErnteDaten.UntereGrenze"/>, <see cref="ErnteDaten.ObereGrenze"/>].
    /// </summary>
    public double Faktor(string ware, Erntegute guete)
    {
        if (Daten is null || !Daten.Waren.TryGetValue(ware, out var gewicht)) return 1;
        var f = 1 + (Daten.FaktorVon(guete) - 1) * Daten.Von(Haerte).Staerke * gewicht;
        return Math.Clamp(f, Daten.UntereGrenze, Daten.ObereGrenze);
    }

    /// <summary>Die betroffenen Waren in der Reihenfolge der Daten (für Anzeige und Meldung).</summary>
    public IEnumerable<string> Waren => Daten?.Waren.Keys ?? Enumerable.Empty<string>();

    /// <summary>Wort für eine Güte, z. B. „eine magere Ernte“.</summary>
    public static string Text(Erntegute g) => g switch
    {
        Erntegute.Gut => "eine gute Ernte",
        Erntegute.Mager => "eine magere Ernte",
        Erntegute.Missernte => "eine Missernte",
        _ => "eine gewöhnliche Ernte",
    };

    // ═════════════════════════════════════════════════════════════════════════
    //  Würfeln
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die Güte einer Ernte: ein Wurf aus einer eigenen <see cref="Zufallsquelle"/> (Startwert der Partie und Erntejahr). Unten liegt die
    /// Missernte, darüber die magere Ernte, ganz oben die gute, dazwischen die gewöhnliche. Nach einer Missernte ist eine schlechte Ernte
    /// gewöhnlich (Schonjahr).
    /// </summary>
    private Erntegute Wuerfeln(int erntejahr, Erntegute vorige)
    {
        var s = Daten!.Von(Haerte);
        var wurf = new Zufallsquelle(Jahreszeitenwesen.Mischen(_startwert ^ Salz, erntejahr)).NextDouble();
        var guete = wurf < s.ChanceMissernte ? Erntegute.Missernte
            : wurf < s.ChanceMissernte + s.ChanceMager ? Erntegute.Mager
            : wurf >= 1 - s.ChanceGut ? Erntegute.Gut
            : Erntegute.Gewoehnlich;
        if (vorige == Erntegute.Missernte && guete is Erntegute.Mager or Erntegute.Missernte) guete = Erntegute.Gewoehnlich;
        return guete;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Ablauf
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Zu Beginn jeder Minute und nach dem Laden: der Erntefaktor an jedem Marktstand – nur wenn sich das Jahr geändert hat.</summary>
    internal void MarktSetzen()
    {
        if (Daten is null || _gesetztFuerJahr == _w.Uhr.Jahr) return;
        _gesetztFuerJahr = _w.Uhr.Jahr;
        foreach (var s in _w.Markt.Staende)
            s.Erntefaktor = Faktor(s.Ware);
    }

    /// <summary>Jede Spielminute: zu Jahresbeginn die Ansage (Sommerjahr) bzw. die Ernte selbst (Herbstjahr) melden.</summary>
    internal void Minute(Spielzeit zeit)
    {
        if (Daten is null || zeit.MinuteImTag != 0) return;
        if (AngesagtIm(zeit.Jahr) is { } a)
            _w.Melden(Ansage(a));
        else if (ErnteVon(zeit.Jahr) is { } e)
            _w.Melden(Erntemeldung(e));
    }

    private string WarenText()
    {
        var namen = Waren.ToList();
        return namen.Count switch
        {
            0 => "",
            1 => namen[0],
            _ => string.Join(", ", namen.Take(namen.Count - 1)) + " und " + namen[^1],
        };
    }

    private string Ansage(Erntelage a) => a.Guete switch
    {
        Erntegute.Gut => $"Die Felder stehen prächtig: Im Herbst {a.Erntejahr} kommt wohl eine gute Ernte – {WarenText()} werden billiger.",
        Erntegute.Mager => $"Ein nasser Sommer: Im Herbst {a.Erntejahr} droht eine magere Ernte – {WarenText()} werden teurer. Wer jetzt einlagert, verkauft später besser.",
        Erntegute.Missernte => $"Hagel und Dauerregen auf den Feldern: Im Herbst {a.Erntejahr} droht eine Missernte – {WarenText()} werden deutlich teurer. Wer jetzt einlagert, verkauft später besser.",
        _ => $"Die Felder stehen wie in jedem Jahr: Im Herbst {a.Erntejahr} kommt wohl eine gewöhnliche Ernte.",
    };

    private string Erntemeldung(Erntelage e) => e.Guete switch
    {
        Erntegute.Gut => $"Gute Ernte {e.Erntejahr}: Bis zur nächsten Ernte sind {WarenText()} reichlich da und billiger.",
        Erntegute.Mager => $"Magere Ernte {e.Erntejahr}: Bis zur nächsten Ernte sind {WarenText()} knapp und teurer.",
        Erntegute.Missernte => $"Missernte {e.Erntejahr}: Teuerung! Bis zur nächsten Ernte sind {WarenText()} sehr knapp und teuer.",
        _ => $"Gewöhnliche Ernte {e.Erntejahr}: Die Preise für {WarenText()} bleiben, wie sie sind.",
    };
}
