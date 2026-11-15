using System.Text;
using System.Text.Json;
using Reichsgilde;

/// <summary>
/// Neues Rathaus (<c>game/scripts/RathausGegenstaende.cs</c>): Gegenstand → Tafel, Wege und die Klicksperre im Haus – dazu die Modelle selbst:
/// Jeder Knoten „Gegenstand_…“ in <c>rathaus_*_innen.glb</c> hat ein Ziel, jedes Ziel einen Knoten, jeder Weg ein gültiges Ziel.
/// </summary>
static class RathausGegenstaendeTests
{
    static string Modellordner => Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())!)!, "assets", "modelle", "gebaeude", "neubau");

    /// <summary>Knotennamen und extras aus dem JSON-Teil einer GLB.</summary>
    static List<(string Name, JsonElement? Extras)> Knoten(string datei)
    {
        var b = File.ReadAllBytes(datei);
        var laenge = BitConverter.ToInt32(b, 12);
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(b, 20, laenge));
        var liste = new List<(string, JsonElement?)>();
        foreach (var k in doc.RootElement.GetProperty("nodes").EnumerateArray())
            liste.Add((k.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "", k.TryGetProperty("extras", out var e) ? e.Clone() : null));
        return liste;
    }

    static IEnumerable<string> Raumdateien() => Directory.GetFiles(Modellordner, "rathaus_*_innen.glb").OrderBy(x => x, StringComparer.Ordinal);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Rathaus neu: jeder Gegenstand öffnet die Tafel von heute (Ratsbuch → Anträge, Ämtertafel → Ämter, Stadtkasse → Kasse, Gericht, Übersicht)", () =>
        {
            Pruefe.Gleich(new RathausGegenstandZiel(RathausTafelArt.Ratskammer, "Antraege"), RathausGegenstaende.ZielVon("Ratsbuch"));
            Pruefe.Gleich(new RathausGegenstandZiel(RathausTafelArt.Ratskammer, "Aemter"), RathausGegenstaende.ZielVon("Aemtertafel"));
            Pruefe.Gleich(new RathausGegenstandZiel(RathausTafelArt.Ratskammer, "Kasse"), RathausGegenstaende.ZielVon("Stadtkasse"));
            Pruefe.Gleich(RathausTafelArt.Gericht, RathausGegenstaende.ZielVon("Richtertisch")!.Tafel);
            Pruefe.Gleich(RathausTafelArt.Gericht, RathausGegenstaende.ZielVon("Zellengitter")!.Tafel);
            foreach (var u in new[] { "Anschlagbrett", "Kanzleipult", "Staendetafel" })
                Pruefe.Gleich(RathausTafelArt.Uebersicht, RathausGegenstaende.ZielVon(u)!.Tafel);
            Pruefe.Gleich((RathausGegenstandZiel?)null, RathausGegenstaende.ZielVon("Familienbuch"));   // Gegenprobe: Wohnhaus, nicht Rathaus
            Pruefe.Gleich((RathausGegenstandZiel?)null, RathausGegenstaende.ZielVon(""));
        }),
        ("Rathaus neu: Wege lesen (Tür, Treppe, Portal) und Unsinn ablehnen", () =>
        {
            Pruefe.Gleich(((bool, string)?)(true, "rathaus:gericht"), RathausGegenstaende.WegLesen(RathausGegenstaende.WegKennung(true, "rathaus:gericht")));
            Pruefe.Gleich(((bool, string)?)(false, "rathaus:rat"), RathausGegenstaende.WegLesen("rathausneu:weg:treppe|rathaus:rat"));
            Pruefe.Gleich(((bool, string)?)(true, "stadt"), RathausGegenstaende.WegLesen("rathausneu:weg:tuer|stadt"));
            Pruefe.Gleich(((bool, string)?)null, RathausGegenstaende.WegLesen("rathausneu:weg:tuer|markt"));
            Pruefe.Gleich(((bool, string)?)null, RathausGegenstaende.WegLesen("rathausneu:weg:fenster|rathaus:rat"));
            Pruefe.Gleich(((bool, string)?)null, RathausGegenstaende.WegLesen("Marktplatz"));
        }),
        ("Rathaus neu: im Haus zählt nur ein Klick auf eine Klickfläche des Hauses (kein Durchfallen auf den Marktplatz)", () =>
        {
            Pruefe.Wahr(RathausGegenstaende.KlickZaehlt(true, true), "Gegenstand im Raum");
            Pruefe.Wahr(!RathausGegenstaende.KlickZaehlt(true, false), "Klick ins Leere trifft hinter der Wand den Marktplatz: zählt nicht");
            Pruefe.Wahr(RathausGegenstaende.KlickZaehlt(false, false), "draußen zählt jeder Klick");
        }),
        ("Rathaus neu: die Modelle – sieben Räume, jeder Gegenstand mit Ziel, jedes Ziel im Modell, jeder Weg mit gültigem Ziel und Gegenweg", () =>
        {
            var dateien = Raumdateien().ToList();
            Pruefe.Gleich(7, dateien.Count);
            var gegenstaende = new List<string>();
            var wege = new List<(string Von, string Nach)>();
            foreach (var datei in dateien)
            {
                var raum = Path.GetFileName(datei);
                foreach (var (name, extras) in Knoten(datei))
                {
                    if (name.StartsWith("Gegenstand_"))
                    {
                        var g = name["Gegenstand_".Length..];
                        Pruefe.Wahr(RathausGegenstaende.ZielVon(g) is not null, $"{raum}: {name} ohne Ziel");
                        gegenstaende.Add(g);
                    }
                    // Anker der Wege tragen ein Ziel (der Netzknoten „Treppe_Halle“ ist die Treppe selbst, kein Weg)
                    if ((name.StartsWith("Tuer_") || name.StartsWith("Treppe_") || name.StartsWith("Leiter_")) && extras is { } e && e.ValueKind == JsonValueKind.Object && e.TryGetProperty("ziel", out var z))
                    {
                        var ziel = z.GetString() ?? "";
                        Pruefe.Wahr(RathausGegenstaende.WegLesen(RathausGegenstaende.WegKennung(name.StartsWith("Tuer_"), ziel)) is not null, $"{raum}: {name} mit Ziel „{ziel}“");
                        wege.Add((raum, ziel));
                    }
                }
            }
            foreach (var g in RathausGegenstaende.Ziele.Keys)
                Pruefe.Wahr(gegenstaende.Contains(g), $"{g} fehlt in den Modellen");
            // Von jedem Raum außer der Halle führt ein Weg; die Halle führt zu Gericht, Amt, Kerker, Turm, Ratshalle und zur Stadt
            foreach (var ziel in new[] { "stadt", "rathaus:gericht", "rathaus:amt", "rathaus:kerker", "rathaus:turm", "rathaus:rat" })
                Pruefe.Wahr(wege.Any(w => w.Von == "rathaus_eingangshalle_innen.glb" && w.Nach == ziel), $"Halle → {ziel}");
            Pruefe.Wahr(wege.Any(w => w.Von == "rathaus_turm_treppe_innen.glb" && w.Nach == "rathaus:spitze"), "Turm → Spitze");
            Pruefe.Wahr(wege.Any(w => w.Von == "rathaus_turm_spitze_innen.glb" && w.Nach == "rathaus:turm"), "Spitze → Turm");
            foreach (var r in new[] { "amtsstube", "gerichtssaal", "kerker", "ratshalle", "turm_treppe" })
                Pruefe.Wahr(wege.Any(w => w.Von == $"rathaus_{r}_innen.glb" && w.Nach == "rathaus:halle"), $"{r} → Halle");
        }),
    };
}
