using System.Collections.Generic;

namespace Reichsgilde;

/// <summary>Welche Tafel ein Gegenstand im neuen Rathaus öffnet (Hauptszene.RaumAngeklickt).</summary>
public enum RathausTafelArt { Uebersicht, Gericht, Ratskammer }

/// <summary>Ziel eines Gegenstands: die Tafel und bei der Ratskammer der Reiter (Name aus <c>RatAnzeige.Reiter</c>).</summary>
public sealed record RathausGegenstandZiel(RathausTafelArt Tafel, string? Reiter = null);

/// <summary>
/// Gegenstände, Wege und Klicks im neuen Rathaus – ohne Godot, damit die Zuordnung im Test steht (RathausGegenstaendeTests).
/// Jeder Gegenstand öffnet eine Tafel, die es heute schon gibt (nichts Neues): Anschlagbrett, Kanzleipult und Ständetafel die
/// Rathaus-Übersicht (wie der Klick aufs Rathaus bzw. die Tafel der Amtsstube), Richtertisch und Zellengitter das Stadtgericht (wie der
/// Klick in Gerichtssaal und Kerker), Ratsbuch, Ämtertafel und Stadtkasse die Ratskammer auf ihrem Reiter.
/// </summary>
public static class RathausGegenstaende
{
    public static readonly IReadOnlyDictionary<string, RathausGegenstandZiel> Ziele = new Dictionary<string, RathausGegenstandZiel>
    {
        ["Anschlagbrett"] = new(RathausTafelArt.Uebersicht),
        ["Kanzleipult"] = new(RathausTafelArt.Uebersicht),
        ["Staendetafel"] = new(RathausTafelArt.Uebersicht),
        ["Richtertisch"] = new(RathausTafelArt.Gericht),
        ["Zellengitter"] = new(RathausTafelArt.Gericht),
        ["Ratsbuch"] = new(RathausTafelArt.Ratskammer, "Antraege"),
        ["Aemtertafel"] = new(RathausTafelArt.Ratskammer, "Aemter"),
        ["Stadtkasse"] = new(RathausTafelArt.Ratskammer, "Kasse"),
    };

    /// <summary>Das Ziel eines Gegenstands (Name ohne „Gegenstand_“), null für Unbekanntes.</summary>
    public static RathausGegenstandZiel? ZielVon(string gegenstand) => Ziele.TryGetValue(gegenstand, out var z) ? z : null;

    /// <summary>Kennungen der sieben Räume (Stadtansicht.Rathausneubau.cs), dazu „stadt“ fürs Portal.</summary>
    public static readonly IReadOnlyList<string> Raeume = new[]
        { "rathaus:halle", "rathaus:amt", "rathaus:gericht", "rathaus:kerker", "rathaus:rat", "rathaus:turm", "rathaus:spitze" };

    /// <summary>Vorsatz der Klickflächen auf Türen, Treppen und Leitern.</summary>
    public const string WegVorsatz = "rathausneu:weg:";

    /// <summary>Kennung der Klickfläche eines Wegs („rathausneu:weg:tuer|rathaus:gericht“).</summary>
    public static string WegKennung(bool tuer, string ziel) => WegVorsatz + (tuer ? "tuer|" : "treppe|") + ziel;

    /// <summary>Liest eine Weg-Kennung: Tür oder Treppe und das Ziel; null, wenn es keine ist.</summary>
    public static (bool Tuer, string Ziel)? WegLesen(string kennung)
    {
        if (!kennung.StartsWith(WegVorsatz)) return null;
        var teile = kennung[WegVorsatz.Length..].Split('|');
        if (teile.Length != 2 || teile[0] is not ("tuer" or "treppe")) return null;
        if (teile[1] != "stadt" && !((IList<string>)Raeume).Contains(teile[1])) return null;
        return (teile[0] == "tuer", teile[1]);
    }

    /// <summary>
    /// Ob ein Klick zählt: Steht die Kamera in einem Raum im Haus (Neubau), zählen nur die Klickflächen dieses Hauses – sonst fiele ein
    /// Klick ins Leere durch Boden oder Fenster auf die Stadt dahinter (z. B. den Marktplatz). Draußen zählt alles.
    /// </summary>
    public static bool KlickZaehlt(bool imHaus, bool istRaumklick) => !imHaus || istRaumklick;
}
