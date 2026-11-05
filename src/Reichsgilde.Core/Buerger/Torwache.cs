using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Buerger;

/// <summary>Was ein Torwächter gerade tut – nach dem Schichtplan, fest aus der Uhr.</summary>
public enum Wachdienst
{
    /// <summary>Nicht im Dienst (daheim, nicht zu sehen).</summary>
    Frei,
    /// <summary>Kommt aus der Stadt zum Tor (Schichtbeginn).</summary>
    Anmarsch,
    /// <summary>Steht am Tor.</summary>
    Posten,
    /// <summary>Geht vom Tor zurück in die Stadt (Schichtende).</summary>
    Abmarsch,
}

/// <summary>
/// Ein Torwächter: die Person (ein <see cref="Einwohner"/> mit Herkunft <see cref="Herkunft.Torwache"/>), sein Tor,
/// seine Mannschaft (0 oder 1, sie wechseln sich täglich mit Früh- und Spätschicht ab) und sein Platz in der Mannschaft
/// (Platz 0 bis jeTorNacht − 1 bleibt in der Nacht).
/// </summary>
public sealed class Torwaechter
{
    public Einwohner Person { get; internal set; }
    public string Tor { get; }
    public int Mannschaft { get; }
    public int Platz { get; }

    internal Torwaechter(Einwohner person, string tor, int mannschaft, int platz)
    {
        Person = person;
        Tor = tor;
        Mannschaft = mannschaft;
        Platz = platz;
    }
}

/// <summary>
/// Die Torwache der Stadt (Spiel-KI, P1 „Wachen und Diebe“, 5.11.2026): je Stadttor der Karte eine kleine Mannschaft echter, benannter
/// Einwohner. Sie stehen am Tor; tagsüber <see cref="TorwacheDaten.JeTorTag"/>, ab <see cref="TorwacheDaten.NachtAb"/> Uhr nur noch
/// <see cref="TorwacheDaten.JeTorNacht"/>. Um <see cref="TorwacheDaten.Wechsel"/> Uhr löst die Spätschicht die Frühschicht ab: Die neue
/// kommt aus der Stadt, die alte geht. Die zwei Mannschaften tauschen täglich Früh- und Spätschicht.
///
/// <b>Ohne Gewicht für die Wirtschaft:</b> Die Torwächter stehen nicht in <see cref="Einwohnerschaft.Einwohner"/>, zählen für keinen Markt,
/// keine Schänke, keine Kirche und keine Diebe, bekommen keinen Sold und nehmen keine Nummer aus der Zählung der Einwohner. Sie würfeln
/// nur mit ihrem eigenen Generator (Namen, Volk, Alter) – der Lauf aller übrigen Generatoren bleibt Wurf für Wurf derselbe (Test).
/// Der Dienst folgt nur aus der Uhr, ganz ohne Zufall.
/// </summary>
public sealed class Torwache
{
    /// <summary>Die Nummern der Torwächter beginnen hier – weit weg von den Nummern der Einwohner (die zählen ab 1).</summary>
    public const int ErsteNummer = 1_000_000;

    /// <summary>So viele Minuten später als der Vordermann bricht jeder weitere Platz einer Mannschaft auf (sie gehen nicht im Gleichschritt).</summary>
    public const int Versatz = 3;

    private readonly Stadtwirtschaft _w;
    private readonly Einwohnerschaft _ew;
    private Zufallsquelle _zufall;
    private readonly List<Torwaechter> _waechter = new();
    private int _naechsteNummer = ErsteNummer;

    internal Torwache(Stadtwirtschaft w, Einwohnerschaft ew, int zufallsstartwert)
    {
        _w = w;
        _ew = ew;
        _zufall = new Zufallsquelle(unchecked(zufallsstartwert * 131 + 7));
    }

    private TorwacheDaten? Daten => _ew.IstAktiv ? _ew.Daten.Torwache : null;

    /// <summary>Gibt es eine Torwache (Abschnitt „buerger.torwache“, eine Karte mit Toren)?</summary>
    public bool IstAktiv => _waechter.Count > 0;

    /// <summary>Alle Torwächter, nach Tor (Reihenfolge der Karte), Mannschaft und Platz.</summary>
    public IReadOnlyList<Torwaechter> Waechter => _waechter;

    /// <summary>Die Tore der Karte in fester Reihenfolge (Mauern, dann Tore wie in der Datei).</summary>
    private List<string> Tore() => _w.Karte is { } k ? k.Mauern.SelectMany(m => m.Tore).Select(t => t.Name).ToList() : new List<string>();

    /// <summary>Der Torwächter mit dieser Nummer (null, wenn es keinen gibt).</summary>
    public Torwaechter? Nummer(int nummer) => nummer < ErsteNummer ? null : _waechter.FirstOrDefault(t => t.Person.Nummer == nummer);

    /// <summary>
    /// Welche Mannschaft hat an diesem Tag die Frühschicht? Sie wechseln sich täglich ab (ein Spieltag ist ein Spieljahr:
    /// <paramref name="tag"/> = <see cref="Spielzeit.Jahr"/>).
    /// </summary>
    public static int FruehMannschaft(int tag) => ((tag % 2) + 2) % 2;

    /// <summary>
    /// Was der Torwächter zur Uhrzeit <paramref name="minuteImTag"/> (Minuten seit Mitternacht, mit Bruchteil) am Tag <paramref name="tag"/> tut,
    /// und wie weit sein Weg ist (0 = eben aufgebrochen, 1 = angekommen; auf dem Posten und frei 0). Fest aus der Uhr:
    ///   • Frühschicht: vom Tagesbeginn auf dem Posten, ab „wechsel“ (+ Versatz je Platz) Abmarsch, danach frei.
    ///   • Spätschicht: „weg“ Minuten vor dem Wechsel Anmarsch, dann auf dem Posten; wer nicht für die Nacht eingeteilt ist, geht ab „nachtAb“.
    /// </summary>
    public (Wachdienst Dienst, double Anteil) Dienst(Torwaechter t, double minuteImTag, int tag)
    {
        if (Daten is not { } d) return (Wachdienst.Frei, 0);
        var versatz = t.Platz * Versatz;
        var wechsel = d.Wechsel * 60 + versatz;
        if (t.Mannschaft == FruehMannschaft(tag))
            return Abschnitt(minuteImTag, Spielzeit.TagesbeginnMinute, wechsel, d.Weg);
        var anmarsch = wechsel - d.Weg;
        if (minuteImTag < anmarsch) return (Wachdienst.Frei, 0);
        if (minuteImTag < wechsel) return (Wachdienst.Anmarsch, (minuteImTag - anmarsch) / d.Weg);
        if (t.Platz < d.JeTorNacht) return (Wachdienst.Posten, 0);
        return Abschnitt(minuteImTag, wechsel, d.NachtAb * 60 + versatz, d.Weg);
    }

    /// <summary>Auf dem Posten von <paramref name="von"/> bis <paramref name="bis"/>, danach <paramref name="weg"/> Minuten Abmarsch, dann frei.</summary>
    private static (Wachdienst, double) Abschnitt(double minute, double von, double bis, int weg)
    {
        if (minute < bis) return minute >= von ? (Wachdienst.Posten, 0) : (Wachdienst.Frei, 0);
        if (minute < bis + weg) return (Wachdienst.Abmarsch, (minute - bis) / weg);
        return (Wachdienst.Frei, 0);
    }

    /// <summary>Was er jetzt tut (Uhr der Partie, ganze Minute).</summary>
    public (Wachdienst Dienst, double Anteil) DienstJetzt(Torwaechter t) => Dienst(t, _w.Uhr.UhrzeitInMinuten, _w.Uhr.Jahr);

    /// <summary>Wer an diesem Tor gerade auf dem Posten steht.</summary>
    public IEnumerable<Torwaechter> AufPosten(string tor) => _waechter.Where(t => t.Tor == tor && DienstJetzt(t).Dienst == Wachdienst.Posten);

    // ═════════════════════════════════════════════════════════════════════════
    //  Ablauf (aus der Einwohnerschaft)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Beim Einzug der Einwohner: je Tor zwei Mannschaften zu je jeTorTag Wächtern.</summary>
    internal void Gruenden(int jahr)
    {
        if (Daten is not { } d || _waechter.Count > 0) return;
        foreach (var tor in Tore())
            for (var mannschaft = 0; mannschaft < 2; mannschaft++)
                for (var platz = 0; platz < d.JeTorTag; platz++)
                    _waechter.Add(new Torwaechter(Neuer(jahr, jung: false), tor, mannschaft, platz));
        Stellen();
    }

    /// <summary>Jede Spielminute: Ort und Handlung der Person nach dem Dienst setzen (für Auswahltafel und Auskunft).</summary>
    internal void Minute() => Stellen();

    private void Stellen()
    {
        foreach (var t in _waechter)
        {
            var (dienst, _) = DienstJetzt(t);
            var e = t.Person;
            e.Handlung = dienst == Wachdienst.Frei ? Handlung.Ruhen : Handlung.Arbeiten;
            e.Ort = dienst == Wachdienst.Frei ? Stadtort.Heim : Stadtort.Tor;
            e.Betrieb = dienst == Wachdienst.Frei ? null : t.Tor;
        }
    }

    /// <summary>Jahresende = Ende des Spieltags: Wer zu alt für den Dienst ist, geht in den Ruhestand; ein junger Wächter tritt an seine Stelle.</summary>
    internal void Jahresende(int neuesJahr)
    {
        if (Daten is null) return;
        foreach (var t in _waechter)
            if (t.Person.Alter(neuesJahr) > Ruhestand(t.Person.Volk))
                t.Person = Neuer(neuesJahr, jung: true);
        Stellen();
    }

    /// <summary>Bis zu welchem Alter einer Dienst tut: volljährig plus gut die Hälfte der Jahre bis zum üblichen Sterbealter.</summary>
    private int Ruhestand(Volk volk)
    {
        var (von, sterbealter) = Alter(volk);
        return von + (int)((sterbealter - von) * 0.6);
    }

    private (int Von, int Sterbealter) Alter(Volk volk)
    {
        var vd = _w.Familien.IstAktiv ? _w.Familien.VolkDaten(volk) : null;
        return (vd?.Volljaehrig ?? 16, (int)(vd?.Sterbealter ?? 64));
    }

    /// <summary>Ein neuer Torwächter (eigener Generator): Volk wie die Einwohner, meist ein Mann, jung oder im besten Alter.</summary>
    private Einwohner Neuer(int jahr, bool jung)
    {
        var d = _ew.Daten;
        var volk = Gewichtet(d.Voelker.Where(v => v.Value > 0).OrderBy(v => v.Key).Select(v => (v.Key, v.Value)).ToList());
        var geschlecht = _zufall.Next(6) == 0 ? Geschlecht.Frau : Geschlecht.Mann;
        var (von, sterbealter) = Alter(volk);
        var spanne = Math.Max(1, (int)((sterbealter - von) * (jung ? 0.15 : 0.5)));
        var alter = von + 1 + _zufall.Next(spanne);
        var nummer = _naechsteNummer++;
        var (vorname, familienname) = Namen(volk, geschlecht, nummer);
        var glaube = _w.Glaube.Startglaube(volk);
        return new Einwohner(nummer, Herkunft.Torwache, "", vorname, familienname, volk, geschlecht, jahr - alter, glaube, TorwacheDaten.Rolle);
    }

    /// <summary>Name aus den Namenslisten des Volkes – kein ganzer Name doppelt, kein Familienname eines Hauses.</summary>
    private (string, string) Namen(Volk volk, Geschlecht geschlecht, int nummer)
    {
        var vd = _w.Familien.IstAktiv ? _w.Familien.VolkDaten(volk) : null;
        if (vd is null) return ("Torwächter", (nummer - ErsteNummer + 1).ToString());
        var vornamen = geschlecht == Geschlecht.Frau ? vd.VornamenFrau : vd.VornamenMann;
        var haeuser = _w.Konten.Select(k => _w.Familien.Von(k)?.Nachname).Where(n => n is not null).ToHashSet();
        var namen = _ew.Einwohner.Select(e => e.Name).Concat(_waechter.Select(t => t.Person.Name)).ToHashSet();
        (string, string) wahl = default;
        for (var versuch = 0; versuch < 10; versuch++)
        {
            wahl = (vornamen[_zufall.Next(vornamen.Count)], vd.Familiennamen[_zufall.Next(vd.Familiennamen.Count)]);
            if (!haeuser.Contains(wahl.Item2) && !namen.Contains($"{wahl.Item1} {wahl.Item2}")) break;
        }
        return wahl;
    }

    private T Gewichtet<T>(List<(T Wert, double Gewicht)> liste)
    {
        var wurf = _zufall.NextDouble() * liste.Sum(x => x.Gewicht);
        foreach (var (wert, gewicht) in liste)
        {
            wurf -= gewicht;
            if (wurf < 0) return wert;
        }
        return liste[^1].Wert;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Speichern
    // ═════════════════════════════════════════════════════════════════════════

    internal TorwacheStand? Sichern() => _waechter.Count == 0 ? null : new TorwacheStand
    {
        ZufallWuerfe = _zufall.Wuerfe,
        NaechsteNummer = _naechsteNummer,
        Waechter = _waechter.Select(t => new TorwaechterStand
        {
            Tor = t.Tor, Mannschaft = t.Mannschaft, Platz = t.Platz,
            Nummer = t.Person.Nummer, Vorname = t.Person.Vorname, Familienname = t.Person.Familienname, Volk = t.Person.Volk,
            Geschlecht = t.Person.Geschlecht, Geburtsjahr = t.Person.Geburtsjahr, Glaube = t.Person.Glaube,
        }).ToList(),
    };

    /// <summary>
    /// Aus dem Spielstand. Fehlt die Torwache (Spielstand von vor P1, oder Daten ohne „torwache“), bleibt sie leer – mit Daten und Karte zieht
    /// sie dann sofort neu ein (eigener Generator, ohne Einfluss auf die übrigen).
    /// </summary>
    internal void Wiederherstellen(TorwacheStand? s, bool gegruendet, int jahr)
    {
        _waechter.Clear();
        if (s is null)
        {
            if (gegruendet) Gruenden(jahr);
            return;
        }
        _zufall = new Zufallsquelle(_zufall.Startwert, s.ZufallWuerfe);
        _naechsteNummer = s.NaechsteNummer;
        foreach (var x in s.Waechter)
            _waechter.Add(new Torwaechter(new Einwohner(x.Nummer, Herkunft.Torwache, "", x.Vorname, x.Familienname, x.Volk, x.Geschlecht,
                x.Geburtsjahr, x.Glaube, TorwacheDaten.Rolle), x.Tor, x.Mannschaft, x.Platz));
        Stellen();
    }
}

/// <summary>Die Torwache im Spielstand (null ohne Torwache).</summary>
public sealed record TorwacheStand
{
    public long ZufallWuerfe { get; init; }
    public int NaechsteNummer { get; init; } = Torwache.ErsteNummer;
    public List<TorwaechterStand> Waechter { get; init; } = new();
}

public sealed record TorwaechterStand
{
    public string Tor { get; init; } = "";
    public int Mannschaft { get; init; }
    public int Platz { get; init; }
    public int Nummer { get; init; }
    public string Vorname { get; init; } = "";
    public string Familienname { get; init; } = "";
    public Volk Volk { get; init; }
    public Geschlecht Geschlecht { get; init; }
    public int Geburtsjahr { get; init; }
    public Glaube.Konfession Glaube { get; init; }
}
