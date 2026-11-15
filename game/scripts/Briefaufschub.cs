namespace Reichsgilde;

/// <summary>
/// Wann Briefe warten (Nutzer, Klicktest 4): in der ganzen Folge Stimmrunde → Ratssitzung → Jahresbericht. Der „Brief der Zunft“ kam sonst
/// zwischen Stimmrunde und Sitzung und lag dann über der Tafel der Sitzung.
/// </summary>
public static class Briefphase
{
    /// <param name="stimmrundeOffen">die Tafel der Stimmrunde steht</param>
    /// <param name="stimmrundeDiesesJahr">die Stimmrunde dieses Jahres hat begonnen, das Jahr ist aber noch nicht vorbei (die Zeit läuft zur Sitzung)</param>
    /// <param name="sitzungAnstehend">der Kern hält die Sitzung, oder sie wartet auf ihre Wiedergabe</param>
    /// <param name="sitzungOffen">die Tafel der Sitzung läuft</param>
    /// <param name="jahresberichtOffen">der Jahresbericht steht</param>
    /// <param name="jahresberichtWartet">der Jahresbericht wartet, bis die Sitzung zu Ende ist</param>
    public static bool Ruht(bool stimmrundeOffen, bool stimmrundeDiesesJahr, bool sitzungAnstehend, bool sitzungOffen, bool jahresberichtOffen, bool jahresberichtWartet) =>
        stimmrundeOffen || stimmrundeDiesesJahr || sitzungAnstehend || sitzungOffen || jahresberichtOffen || jahresberichtWartet;
}

/// <summary>
/// Briefe warten, solange die Ratssitzung oder die Stimmrunde offen ist (Nutzer, Rathaus Stufe 10: Der „Brief der Zunft“ lag sonst über der
/// Tafel), und kommen direkt danach in ihrer Reihenfolge – keiner geht verloren. Logik ohne Godot, damit sie sich testen lässt
/// (<c>BriefaufschubTests</c>); die Andockstelle ist <see cref="BriefAnzeige.Einreihen"/>.
/// </summary>
public sealed class Briefaufschub<T>
{
    private readonly List<T> _wartend = new();

    /// <summary>Wie viele Briefe gerade warten.</summary>
    public int Wartend => _wartend.Count;

    /// <summary>
    /// Ein Brief kommt an. true: Er wartet (es ruht etwas, oder ältere warten noch – die Reihenfolge bleibt); false: sofort zeigen.
    /// </summary>
    public bool Zurueckstellen(T brief, bool ruht)
    {
        if (!ruht && _wartend.Count == 0) return false;
        _wartend.Add(brief);
        return true;
    }

    /// <summary>Jedes Bild: Ruht nichts mehr, kommen alle wartenden Briefe in ihrer Reihenfolge heraus; sonst keiner.</summary>
    public IReadOnlyList<T> Freigeben(bool ruht)
    {
        if (ruht || _wartend.Count == 0) return Array.Empty<T>();
        var alle = _wartend.ToList();
        _wartend.Clear();
        return alle;
    }
}
