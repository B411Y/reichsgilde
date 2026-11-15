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
///   • <b>Keine Ansage:</b> Im Sommerjahr davor steht die Güte im Kern schon fest (<see cref="Angesagt"/>, nur für Rechnungen), gemeldet und gezeigt
///     wird sie erst, wenn die Ernte gilt (Nutzer 15.11.2026: der Spieler sieht nie in die Zukunft).
///   • <b>Keine Abwärtsspirale:</b> Nach einer Missernte folgt ein Schonjahr (die nächste Ernte ist nie mager und keine Missernte), der Faktor
///     jeder Ware bleibt zwischen <see cref="ErnteDaten.UntereGrenze"/> und <see cref="ErnteDaten.ObereGrenze"/>, dazu die Preisgrenzen des Markts.
///   • Wie oft und wie stark: <see cref="HaerteDerZeit"/> der Partie (<see cref="Stadtwirtschaft.HaerteDerZeit"/>).
///
/// Seit Datenversion 92 ist die Ernte die erste Quelle der <see cref="Lagenwesen"/>: Würfel und Abfragen stecken in einer <see cref="Rohstofflage"/>
/// (Herbst, gleiches Salz, gleiche Würfe wie Datenversion 91); dieses Wesen behält seine Abfragen und Texte.
///
/// Nichts davon steht im Spielstand: Die Güte jeder Ernte kommt aus dem Startwert der Partie, dem Erntejahr und der Güte der Ernte davor (eigene
/// <see cref="Zufallsquelle"/> je Ernte, sie würfelt genau einmal). Eine geladene Partie und jeder Mitspieler rechnen dieselben Ernten.
/// </summary>
public sealed class Erntewesen
{
    /// <summary>Mischt den Startwert der Partie, damit die Ernten nicht dieselben Würfe wie die Winter bekommen.</summary>
    private const int Salz = 0x4E52_7465;

    private readonly Stadtwirtschaft _w;

    public ErnteDaten? Daten { get; }

    /// <summary>Die Ernte als Quelle (null ohne Abschnitt „ernte“).</summary>
    public Rohstofflage? Quelle { get; }

    /// <summary>Gibt es Erntejahre? (Abschnitt „ernte“)</summary>
    public bool IstAktiv => Daten is not null;

    internal Erntewesen(Stadtwirtschaft w, ErnteDaten? daten, LagenDaten? lagen, int zufallsstartwert)
    {
        _w = w;
        Daten = daten;
        if (daten is not null)
            Quelle = new Rohstofflage(w, "Ernte", daten, daten.Waren, Jahreszeit.Herbst, Salz, zufallsstartwert,
                lagen?.ErnteBetrieb, lagen?.Ertrag ?? 0, quelle: null);
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
    public Erntelage? GiltIm(int jahr) => Als(Quelle?.GiltIm(jahr));

    /// <summary>Die Ernte, die im Jahr <paramref name="jahr"/> angesagt ist: nur in einem Sommerjahr die des folgenden Herbstjahres.</summary>
    public Erntelage? AngesagtIm(int jahr) => Als(Quelle?.AngesagtIm(jahr));

    /// <summary>Die Ernte eines Herbstjahres (null, wenn das Jahr kein Erntejahr ist oder der Abschnitt fehlt).</summary>
    public Erntelage? ErnteVon(int erntejahr) => Als(Quelle?.LageVon(erntejahr));

    private static Erntelage? Als(Quellenlage? l) => l is null ? null : new Erntelage(l.Stichjahr, (Erntegute)l.Guete);

    /// <summary>Faktor der Ernte auf die Grundversorgung dieser Ware jetzt (1, wenn die Ware nicht betroffen ist oder keine Ernte gilt).</summary>
    public double Faktor(string ware) => Jetzt is { } l ? Faktor(ware, l.Guete) : 1;

    /// <summary>
    /// Faktor einer Güte auf eine Ware in dieser Partie: 1 + (Faktor der Güte − 1) × Stärke der Stufe × Gewicht der Ware, begrenzt auf
    /// [<see cref="ErnteDaten.UntereGrenze"/>, <see cref="ErnteDaten.ObereGrenze"/>].
    /// </summary>
    public double Faktor(string ware, Erntegute guete) => Quelle?.Faktor(ware, (Lagegute)guete) ?? 1;

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
    //  Ablauf
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Jede Spielminute: zu Jahresbeginn die Ernte melden, wenn sie zu gelten beginnt (Herbstjahr). Eine Ansage vorab gibt es nicht (Nutzer 15.11.2026: der Spieler sieht nie in die Zukunft).</summary>
    internal void Minute(Spielzeit zeit)
    {
        if (Daten is null || zeit.MinuteImTag != 0) return;
        if (ErnteVon(zeit.Jahr) is { } e)
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

    private string Erntemeldung(Erntelage e) => e.Guete switch
    {
        Erntegute.Gut => $"Gute Ernte {e.Erntejahr}: Bis zur nächsten Ernte sind {WarenText()} reichlich da und billiger.",
        Erntegute.Mager => $"Magere Ernte {e.Erntejahr}: Bis zur nächsten Ernte sind {WarenText()} knapp und teurer.",
        Erntegute.Missernte => $"Missernte {e.Erntejahr}: Teuerung! Bis zur nächsten Ernte sind {WarenText()} sehr knapp und teuer.",
        _ => $"Gewöhnliche Ernte {e.Erntejahr}: Die Preise für {WarenText()} bleiben, wie sie sind.",
    };
}
