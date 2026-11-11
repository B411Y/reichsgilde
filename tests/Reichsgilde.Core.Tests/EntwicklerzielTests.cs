using Reichsgilde.Core.Entwickler;
using Reichsgilde.Core.Gefahren;

/// <summary>Kennung eines angeklickten Gebäudes → Blitzziel und Brandbefehl (Entwicklerfenster „Blitz auf Gebäude“, „Brand hier“).</summary>
static class EntwicklerzielTests
{
    static readonly IReadOnlyList<Blitzziel> Ziele = new[]
    {
        new Blitzziel("Rathaus", "das Rathaus", 0, 0, 16, 0, null, null),
        new Blitzziel("grundstueck:7", "ein leeres Wohnhaus", 0, 0, 8, 0, null, null),
    };

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Entwicklerziel: Kartenkennung und Grundstück werden gefunden", () =>
        {
            Pruefe.Gleich("Rathaus", Entwicklerziel.Finden("Rathaus", Ziele)?.Kennung, "Rathaus");
            Pruefe.Gleich("grundstueck:7", Entwicklerziel.Finden("grundstueck:7", Ziele)?.Kennung, "Grundstück");
        }),
        ("Entwicklerziel: Markt, leer und unbekannt geben null; Rathaus hat keinen Brandbefehl", () =>
        {
            Pruefe.Wahr(Entwicklerziel.Finden("Marktplatz", Ziele) is null, "Markt");
            Pruefe.Wahr(Entwicklerziel.Finden(null, Ziele) is null, "null");
            Pruefe.Wahr(Entwicklerziel.Finden("betrieb:Nirgends", Ziele) is null, "Betrieb unbekannt");
            Pruefe.Wahr(Entwicklerziel.Finden("Rathaus", Ziele)!.Brandbefehl() is null, "Rathaus brennt nur per Blitz");
        }),
        ("Entwicklerziel: Brandbefehl je Gebäudeart", () =>
        {
            Pruefe.Gleich(new EntwicklerFeuer("Gerberei"), new Entwicklerziel("k", "n", "Gerberei", null).Brandbefehl(), "Betrieb");
            Pruefe.Gleich(new EntwicklerHausbrand("Anna"), new Entwicklerziel("k", "n", null, "Anna").Brandbefehl(), "Wohnhaus");
        }),
    };
}
