namespace Reichsgilde.Core.Dynastie;

/// <summary>
/// Die Farben der Heraldik („Tinkturen“). Gold und Silber heißen „Metalle“, die übrigen „Farben“.
/// Alte Faustregel der Wappenkunst: nie Metall auf Metall oder Farbe auf Farbe – dann ist ein Wappen von weitem lesbar.
/// </summary>
public enum Tinktur { Gold, Silber, Rot, Blau, Gruen, Schwarz, Purpur }

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
        new(Teilung.Balken, Tinktur.Gruen, Tinktur.Gold, Figur.Aehre),
        new(Teilung.Ohne, Tinktur.Blau, Tinktur.Gold, Figur.Lilie),
        new(Teilung.Geviert, Tinktur.Silber, Tinktur.Rot, Figur.Rose),
        new(Teilung.Pfahl, Tinktur.Rot, Tinktur.Silber, Figur.Schwert),
        new(Teilung.Sparren, Tinktur.Schwarz, Tinktur.Silber, Figur.Fisch),
        new(Teilung.Ohne, Tinktur.Rot, Tinktur.Gold, Figur.Kelch),
    };

    public static bool IstMetall(Tinktur t) => t is Tinktur.Gold or Tinktur.Silber;

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
        return Zufall(new Random((int)(h & 0x7FFFFFFF)));
    }

    /// <summary>Ein zufälliges, gut lesbares Wappen (je ein Metall und eine Farbe), das keinem KI-Haus gehört.</summary>
    public static Wappen Zufall(Random zufall)
    {
        while (true)
        {
            var metall = zufall.Next(2) == 0 ? Tinktur.Gold : Tinktur.Silber;
            var farbe = (Tinktur)zufall.Next(2, 7);
            var (f1, f2) = zufall.Next(3) == 0 ? (metall, farbe) : (farbe, metall);
            var w = new Wappen((Teilung)zufall.Next(Enum.GetValues<Teilung>().Length), f1, f2,
                (Figur)zufall.Next(Enum.GetValues<Figur>().Length));
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
        Tinktur.Gruen => "Grün", Tinktur.Schwarz => "Schwarz", _ => "Purpur",
    };

    private static string Adjektiv(Tinktur t) => t switch
    {
        Tinktur.Gold => "golden", Tinktur.Silber => "silbern", Tinktur.Rot => "rot", Tinktur.Blau => "blau",
        Tinktur.Gruen => "grün", Tinktur.Schwarz => "schwarz", _ => "purpurn",
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
