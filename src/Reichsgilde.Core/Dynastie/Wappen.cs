namespace Reichsgilde.Core.Dynastie;

/// <summary>
/// Die Farben der Heraldik („Tinkturen“). Gold und Silber heißen „Metalle“, die übrigen „Farben“.
/// Alte Faustregel der Wappenkunst: nie Metall auf Metall oder Farbe auf Farbe – dann ist ein Wappen von weitem lesbar.
/// </summary>
public enum Tinktur { Gold, Silber, Rot, Blau, Gruen, Schwarz, Purpur, Ocker, Orange } // Ocker und Orange seit 17.11.2026 (acht Hausfarben für acht Häuser); neue nur hinten anfügen

/// <summary>Wie der Schild aufgeteilt ist. Die ersten fünf teilen das Feld in zwei Farben, die übrigen legen ein Band („Heroldsbild“) darüber.</summary>
public enum Teilung { Ohne, Geteilt, Gespalten, Schraeg, Geviert, Schildhaupt, Balken, Pfahl, Sparren, Bord }

/// <summary>Die Figur in der Mitte des Schildes („gemeine Figur“).</summary>
public enum Figur { Hammer, Amboss, Baum, Wellen, Stern, Schluessel, Turm, Krone, Rad, Aehre, Lilie, Rose, Fisch, Schwert, Kelch, Mond }

/// <summary>
/// Das Wappen eines Hauses: Teilung, zwei Farben und eine Figur. Die Figur steht in der zweiten Farbe auf der ersten;
/// wo sie über ein Feld oder Band der zweiten Farbe reicht, wechselt sie in die erste („verwechselte Farben“).
/// Der Kern kennt nur diese Beschreibung – gezeichnet wird in der Oberfläche (game/scripts/WappenBild.cs).
/// </summary>
public sealed record Wappen(Teilung Teilung, Tinktur Farbe1, Tinktur Farbe2, Figur Figur)
{
    /// <summary>Wappen der drei KI-Häuser – im Menü nicht wählbar.</summary>
    public static readonly Wappen Eisenbart = new(Teilung.Ohne, Tinktur.Schwarz, Tinktur.Gold, Figur.Amboss);
    public static readonly Wappen Eichwald = new(Teilung.Bord, Tinktur.Gruen, Tinktur.Silber, Figur.Baum);
    public static readonly Wappen Kieselbach = new(Teilung.Schildhaupt, Tinktur.Blau, Tinktur.Silber, Figur.Wellen);

    public static readonly IReadOnlyList<Wappen> Rivalenwappen = new[] { Eisenbart, Eichwald, Kieselbach };

    /// <summary>Fertige Wappen für das Menü „Neue Partie“. Die erste ist voreingestellt.</summary>
    public static readonly IReadOnlyList<Wappen> Vorlagen = new Wappen[]
    {
        new(Teilung.Ohne, Tinktur.Rot, Tinktur.Gold, Figur.Hammer),
        new(Teilung.Geteilt, Tinktur.Blau, Tinktur.Gold, Figur.Stern),
        new(Teilung.Gespalten, Tinktur.Silber, Tinktur.Rot, Figur.Schluessel),
        new(Teilung.Ohne, Tinktur.Blau, Tinktur.Silber, Figur.Turm),
        new(Teilung.Schraeg, Tinktur.Gold, Tinktur.Schwarz, Figur.Rad),
        new(Teilung.Ohne, Tinktur.Purpur, Tinktur.Gold, Figur.Krone),
        new(Teilung.Ohne, Tinktur.Gruen, Tinktur.Gold, Figur.Aehre),
        new(Teilung.Ohne, Tinktur.Blau, Tinktur.Gold, Figur.Lilie),
        new(Teilung.Geviert, Tinktur.Silber, Tinktur.Rot, Figur.Rose),
        new(Teilung.Pfahl, Tinktur.Rot, Tinktur.Silber, Figur.Schwert),
        new(Teilung.Sparren, Tinktur.Schwarz, Tinktur.Silber, Figur.Mond),
        new(Teilung.Balken, Tinktur.Blau, Tinktur.Silber, Figur.Fisch),
    };

    public static bool IstMetall(Tinktur t) => t is Tinktur.Gold or Tinktur.Silber;

    /// <summary>
    /// Die Hausfarbe zu diesem Wappen („#rrggbb“, Fahnen, Dächer, Wandbehang; Nutzer 17.11.2026): die Palettenfarbe der ersten Wappenfarbe
    /// (<see cref="Farbe1"/>) aus <see cref="Partie.Partiebesetzung.Hausfarben"/> – acht Farben für acht Häuser. Silber hat dort keine Farbe – dann zählt die zweite
    /// (<see cref="Farbe2"/>). Eine feste Tabelle, kein Zufall: dasselbe Wappen gibt auf jedem Rechner dieselbe Farbe.
    /// </summary>
    public string Hausfarbe() => Palettenfarbe(Farbe1) ?? Palettenfarbe(Farbe2) ?? Palettenfarbe(Tinktur.Schwarz)!;

    /// <summary>Die Palettenfarbe einer Wappenfarbe („#rrggbb“) – null bei Silber (keine Hausfarbe ist weiß).</summary>
    public static string? Palettenfarbe(Tinktur t) => t switch
    {
        Tinktur.Rot => "#b33329",
        Tinktur.Blau => "#336bb8",
        Tinktur.Gruen => "#388c47",
        Tinktur.Gold => "#d9ad2e",
        Tinktur.Purpur => "#7a3b8f",
        Tinktur.Schwarz => "#5c5c5c",
        Tinktur.Ocker => "#a88a3a",
        Tinktur.Orange => "#c4672a",
        _ => null,
    };

    /// <summary>Die acht Hausfarben, die ein Wappen geben kann (in der Reihenfolge der Wappenfarben; Silber hat keine).</summary>
    public static readonly IReadOnlyList<string> Wappenhausfarben = Enum.GetValues<Tinktur>().Select(Palettenfarbe).OfType<string>().ToList();

    /// <summary>Was an diesem Wappen nicht stimmt, oder null. Gleiche Farben verschwimmen zu einer Fläche.</summary>
    public string? Fehler() => Farbe1 == Farbe2 ? "Ein Wappen braucht zwei verschiedene Farben." : null;

    /// <summary>
    /// Ein Wappen aus dem Namen gewürfelt – für Häuser ohne gewähltes Wappen (alte Spielstände, Tests).
    /// Immer dasselbe für denselben Namen, auf jedem Rechner (eigene Prüfsumme statt string.GetHashCode,
    /// das .NET bei jedem Programmstart anders würfelt).
    /// </summary>
    public static Wappen AusName(string name)
    {
        uint h = 2166136261; // FNV-1a
        foreach (var c in name) { h ^= c; h *= 16777619; }
        var zufall = new Random((int)(h & 0x7FFFFFFF));
        return Zufall((von, bis) => von == 0 ? zufall.Next(bis) : zufall.Next(von, bis), AlteFarben); // wie vor Ocker und Orange: derselbe Name, dasselbe Wappen
    }

    /// <summary>Bis hierher (ausschließlich) reichten die Wappenfarben bis zum 17.11.2026 – ohne Ocker und Orange.</summary>
    private const int AlteFarben = (int)Tinktur.Purpur + 1;

    /// <summary>Ein zufälliges, gut lesbares Wappen (je ein Metall und eine Farbe), das keinem KI-Haus gehört.</summary>
    public static Wappen Zufall(Random zufall) => Zufall((von, bis) => von == 0 ? zufall.Next(bis) : zufall.Next(von, bis), Enum.GetValues<Tinktur>().Length);

    /// <summary>
    /// Dasselbe aus einer <see cref="Zufallsquelle"/> (Neue Partie, 11.11.2026): ein Wappen, das keinem KI-Haus der Vorlagen gehört und nicht
    /// in <paramref name="belegt"/> steht. Je Versuch genau fünf Würfe.
    /// <paramref name="vergebeneFarben"/> (Nutzer 17.11.2026): Hausfarben, die schon ein Haus trägt („#rrggbb“) – solange eine der acht
    /// <see cref="Wappenhausfarben"/> frei ist, bekommt das Wappen keine vergebene Hauptfarbe (es wird neu gewürfelt, aus derselben Quelle).
    /// </summary>
    public static Wappen Zufall(Zufallsquelle zufall, IReadOnlyCollection<Wappen>? belegt = null, IReadOnlyCollection<string>? vergebeneFarben = null)
    {
        var meiden = vergebeneFarben is not null && Wappenhausfarben.Any(f => !vergebeneFarben.Contains(f));
        while (true)
        {
            var w = Zufall((von, bis) => von + zufall.Next(bis - von), Enum.GetValues<Tinktur>().Length);
            if (belegt is not null && belegt.Contains(w)) continue;
            if (meiden && vergebeneFarben!.Contains(w.Hausfarbe())) continue;
            return w;
        }
    }

    /// <summary><paramref name="farben"/>: Die Farbe kommt aus den Wappenfarben 2 bis hierher (ausschließlich).</summary>
    private static Wappen Zufall(Func<int, int, int> next, int farben)
    {
        while (true)
        {
            var metall = next(0, 2) == 0 ? Tinktur.Gold : Tinktur.Silber;
            var farbe = (Tinktur)next(2, farben);
            if (farbe is Tinktur.Ocker or Tinktur.Orange) metall = Tinktur.Silber; // neben Gold kaum zu lesen
            var (f1, f2) = next(0, 3) == 0 ? (metall, farbe) : (farbe, metall);
            var w = new Wappen((Teilung)next(0, Enum.GetValues<Teilung>().Length), f1, f2,
                (Figur)next(0, Enum.GetValues<Figur>().Length));
            if (!Rivalenwappen.Contains(w)) return w;
        }
    }

    // ── Wappenbeschreibung („Blasonierung“) ──────────────────────────────────

    /// <summary>
    /// Die Beschreibung in der Sprache der Herolde, z. B. „In Rot ein goldener Hammer.“ –
    /// für Tooltips. „Verwechselte Farben“: Die Figur wechselt auf dem andersfarbigen Teil die Farbe.
    /// </summary>
    public string Beschreibung()
    {
        var figur = Figurname(Figur);
        var geschlecht = FigurGeschlecht(Figur);
        var artikel = geschlecht == 'f' ? "eine" : "ein";
        string Farbig(Tinktur t, char g) => Adjektiv(t) + (g switch { 'm' => "er", 'f' => "e", _ => "es" });
        string Band(string name, char g) => $"In {Name(Farbe1)} {(g == 'f' ? "eine" : "ein")} {Farbig(Farbe2, g)} {name}, darüber {artikel} {figur} in verwechselten Farben.";

        return Teilung switch
        {
            Teilung.Ohne => $"In {Name(Farbe1)} {artikel} {Farbig(Farbe2, geschlecht)} {figur}.",
            Teilung.Geteilt => $"Von {Name(Farbe1)} und {Name(Farbe2)} geteilt, darin {artikel} {figur} in verwechselten Farben.",
            Teilung.Gespalten => $"Von {Name(Farbe1)} und {Name(Farbe2)} gespalten, darin {artikel} {figur} in verwechselten Farben.",
            Teilung.Schraeg => $"Von {Name(Farbe1)} und {Name(Farbe2)} schräg geteilt, darin {artikel} {figur} in verwechselten Farben.",
            Teilung.Geviert => $"Von {Name(Farbe1)} und {Name(Farbe2)} geviert, darin {artikel} {figur} in verwechselten Farben.",
            Teilung.Schildhaupt => $"In {Name(Farbe1)} unter {Adjektiv(Farbe2)}em Schildhaupt {artikel} {Farbig(Farbe2, geschlecht)} {figur}.",
            Teilung.Bord => $"In {Name(Farbe1)} mit {Adjektiv(Farbe2)}em Bord {artikel} {Farbig(Farbe2, geschlecht)} {figur}.",
            Teilung.Balken => Band("Balken", 'm'),
            Teilung.Pfahl => Band("Pfahl", 'm'),
            _ => Band("Sparren", 'm'),
        };
    }

    public static string Name(Tinktur t) => t switch
    {
        Tinktur.Gold => "Gold", Tinktur.Silber => "Silber", Tinktur.Rot => "Rot", Tinktur.Blau => "Blau",
        Tinktur.Gruen => "Grün", Tinktur.Schwarz => "Schwarz", Tinktur.Ocker => "Ocker", Tinktur.Orange => "Orange", _ => "Purpur",
    };

    private static string Adjektiv(Tinktur t) => t switch
    {
        Tinktur.Gold => "golden", Tinktur.Silber => "silbern", Tinktur.Rot => "rot", Tinktur.Blau => "blau",
        Tinktur.Gruen => "grün", Tinktur.Schwarz => "schwarz", Tinktur.Ocker => "ockerfarben", Tinktur.Orange => "orangefarben", _ => "purpurn",
    };

    public static string Figurname(Figur f) => f switch
    {
        Figur.Schluessel => "Schlüssel", Figur.Aehre => "Ähre", Figur.Mond => "Halbmond", Figur.Wellen => "Wellenbalken",
        _ => f.ToString(),
    };

    /// <summary>Grammatisches Geschlecht der Figur – für „ein goldener Hammer“, „eine goldene Krone“, „ein goldenes Rad“.</summary>
    private static char FigurGeschlecht(Figur f) => f switch
    {
        Figur.Krone or Figur.Aehre or Figur.Lilie or Figur.Rose => 'f',
        Figur.Rad or Figur.Schwert => 'n',
        _ => 'm',
    };
}
