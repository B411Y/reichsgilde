using Reichsgilde.Core.Gefahren;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Entwickler;

/// <summary>Das Gebäude, das der Entwickler im Spiel angeklickt hat – mit dem, was die Befehle <see cref="EntwicklerBlitz"/>, <see cref="EntwicklerFeuer"/> und <see cref="EntwicklerHausbrand"/> brauchen.</summary>
/// <param name="Kennung">Kennung des Blitzziels (Karte, „grundstueck:7“ …): geht an <see cref="EntwicklerBlitz"/>.</param>
/// <param name="Name">Anzeigename („das Wohnhaus von …“, Betriebsname).</param>
/// <param name="Betrieb">Name des Betriebs darin (Werkstatt, Umlandbetrieb) oder null.</param>
/// <param name="Haus">Inhaber des Hauses, das darin wohnt, oder null.</param>
public sealed record Entwicklerziel(string Kennung, string Name, string? Betrieb, string? Haus)
{
    /// <summary>
    /// Welcher Befehl brennt hier ohne Blitz? Betrieb: <see cref="EntwicklerFeuer"/>; bewohntes Wohnhaus: <see cref="EntwicklerHausbrand"/>;
    /// alles andere (Rathaus, Kirche, leeres Haus, Bürgerhaus): null – es gibt dort keinen Brandbefehl, nur den Blitz.
    /// </summary>
    public EntwicklerBefehl? Brandbefehl() =>
        Betrieb is { } b ? new EntwicklerFeuer(b) : Haus is { } h ? new EntwicklerHausbrand(h) : null;

    /// <summary>
    /// Sucht zu einer Kennung der Oberfläche („Rathaus“, „grundstueck:7“, „betrieb:Gerberei“, „wohnhaus:Anna“) das Blitzziel. Null: kein Ziel
    /// (Markt, Hafen, Bauplatz, Baustelle, Figur …).
    /// </summary>
    public static Entwicklerziel? Finden(string? szene, IReadOnlyList<Blitzziel> ziele)
    {
        if (string.IsNullOrEmpty(szene)) return null;
        var z = ziele.FirstOrDefault(t => t.Kennung == szene)
                ?? (szene.StartsWith("betrieb:") ? ziele.FirstOrDefault(t => t.Betrieb?.Name == szene["betrieb:".Length..]) : null)
                ?? (szene.StartsWith("wohnhaus:") ? ziele.FirstOrDefault(t => t.Bewohner?.Inhaber == szene["wohnhaus:".Length..]) : null);
        return z is null ? null : new Entwicklerziel(z.Kennung, z.Name, z.Betrieb?.Name, z.Bewohner?.Inhaber);
    }
}
