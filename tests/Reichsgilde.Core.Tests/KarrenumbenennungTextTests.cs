using System.Text.Json.Nodes;
using Reichsgilde.Core.Speichern;

/// <summary>
/// Tests: Karrenumbenennung in Texten und Randfällen alter Spielstände – „einen Pferdekarren“ wird zu „einen Großen Pferdekarren“ (Akkusativ),
/// ein Stand ohne Feld „Format“ bleibt, wie er ist, und ein umgeschriebener Stand ändert sich beim zweiten Lauf nicht mehr.
/// </summary>
static class KarrenumbenennungTextTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Karrenart: „einen Pferdekarren“ und „einen Kriegskarren“ werden „einen Großen Pferdekarren“, „einen Ochsenkarren“ wird „einen Pferdekarren“", () =>
        {
            Pruefe.Gleich("Für einen Großen Pferdekarren fehlen 300 Gulden.", Karrenumbenennung.Text("Für einen Pferdekarren fehlen 300 Gulden."));
            Pruefe.Gleich("verkauft Pferdekarren 1 für einen Großen Pferdekarren.", Karrenumbenennung.Text("verkauft Ochsenkarren 1 für einen Pferdekarren."));
            Pruefe.Gleich("tauscht einen Pferdekarren gegen einen Großen Pferdekarren", Karrenumbenennung.Text("tauscht einen Ochsenkarren gegen einen Kriegskarren"));
            Pruefe.Gleich("Einen Großen Pferdekarren gekauft", Karrenumbenennung.Text("Einen Pferdekarren gekauft"));
            Pruefe.Gleich("keinen Großer Pferdekarren", Karrenumbenennung.Text("keinen Pferdekarren")); // nur „einen“ als eigenes Wort
            Pruefe.Gleich("Großer Pferdekarren 2 ist da", Karrenumbenennung.Text("Pferdekarren 2 ist da"));
        }),

        ("Karrenart: Meldungen eines alten Spielstands (Format 3) stehen nach dem Umschreiben im Akkusativ", () =>
        {
            var stand = JsonNode.Parse("""{ "Format": 3, "Meldungen": [ { "Text": "Für einen Pferdekarren fehlen 300 Gulden." } ] }""")!.AsObject();
            Karrenumbenennung.Umrechnen(stand);
            Pruefe.Gleich("Für einen Großen Pferdekarren fehlen 300 Gulden.", (string)stand["Meldungen"]![0]!["Text"]!);
        }),

        ("Karrenart: Ein Stand ohne Feld „Format“ wird nicht umbenannt (er wird als aktuelles Format gelesen)", () =>
        {
            const string text = """{"Karren":["Pferdekarren 1","Großer Pferdekarren 2"]}""";
            var stand = JsonNode.Parse(text)!.AsObject();
            Karrenumbenennung.Umrechnen(stand);
            Pruefe.Gleich(JsonNode.Parse(text)!.ToJsonString(), stand.ToJsonString());
        }),

        ("Karrenart: Ein umgeschriebener Stand (Format 3) trägt danach Format 4, ein zweiter Lauf ändert nichts", () =>
        {
            var stand = JsonNode.Parse("""{"Format":3,"Karren":["Ochsenkarren 1","Pferdekarren 2"]}""")!.AsObject();
            Karrenumbenennung.Umrechnen(stand);
            var einmal = stand.ToJsonString();
            Pruefe.Gleich(JsonNode.Parse("""{"Format":4,"Karren":["Pferdekarren 1","Großer Pferdekarren 2"]}""")!.ToJsonString(), einmal);
            Karrenumbenennung.Umrechnen(stand);
            Pruefe.Gleich(einmal, stand.ToJsonString());
        }),
    };
}
