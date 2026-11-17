using System.Text.Json.Nodes;

/// <summary>
/// Wie viele KI-Häuser Tests und Messungen auf einer Karte aufstellen, wenn sie „alle Häuser der Karte“ meinen. Bis zum 17.11.2026 nannte die
/// Karte ihre KI-Häuser fest (Köln sieben, Greybox drei, Messstadt acht, Probestadt zwei); seitdem würfelt jede Partie ihre Gegner, und die
/// Karte nennt nur noch die Namen der Stadt („hausnamen“) – je früherem Haus einen. Damit Messreihen und Tests vergleichbar bleiben, zählen
/// sie weiter so viele Häuser: die festen einer älteren Karte, sonst die Namen der Stadt; ohne beides eines.
/// </summary>
static class Kartenhaeuser
{
    public static int Anzahl(string kartenText)
    {
        var start = JsonNode.Parse(kartenText)!["start"];
        if (start?["haeuser"] is JsonArray { Count: > 0 } haeuser) return haeuser.Count;
        return start?["hausnamen"] is JsonArray { Count: > 0 } namen ? namen.Count : 1;
    }
}

/// <summary>
/// Die Karten des Spiels, wie sie bis zum 17.11.2026 waren: mit festen KI-Häusern im Abschnitt „start“ (Köln sieben, Greybox drei, Messstadt
/// acht, Probestadt zwei; die Abschnitte liegen unter Kartenvorlagen/). Für Tests, die bestimmte Häuser brauchen (feste Aufstellung
/// <c>Aufstellungsart.Vorgabe</c>, Haus Eisenbart mit seiner Grube …) – und zugleich der Beleg, dass eine ältere Karte weiter lädt und läuft.
/// Alles außer „start“ ist die heutige Karte.
/// </summary>
static class Altkarte
{
    static readonly object Schloss = new();

    /// <summary>Der Text der Karte mit den festen Häusern von früher.</summary>
    public static string Text(string kennung)
    {
        var neu = File.ReadAllText(Datenpfad.Karte(kennung));
        var ordner = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
            "tests", "Reichsgilde.Core.Tests", "Kartenvorlagen");
        var start = File.ReadAllText(Path.Combine(ordner, $"start-{kennung}.json"));
        var von = neu.IndexOf("\n \"start\": {", StringComparison.Ordinal) + 1;
        var bis = System.Text.RegularExpressions.Regex.Match(neu[(von + 5)..], "\n \"[a-z]").Index + von + 5 + 1;
        return neu[..von] + start + neu[bis..];
    }

    /// <summary>
    /// Die Karte als Datei (im Ausgabeordner der Tests, mit dem Katalog daneben wie im Spiel: altkarten/karten/ und altkarten/daten/) – für
    /// alles, was einen Pfad braucht.
    /// </summary>
    public static string Pfad(string kennung)
    {
        var wurzel = Path.Combine(AppContext.BaseDirectory, "altkarten");
        var pfad = Path.Combine(wurzel, "karten", kennung + ".json");
        lock (Schloss)
        {
            var text = Text(kennung);
            if (!File.Exists(pfad) || File.ReadAllText(pfad) != text)
            {
                Directory.CreateDirectory(Path.Combine(wurzel, "karten"));
                File.WriteAllText(pfad, text);
            }
            var katalog = Path.Combine(wurzel, "daten", "katalog.json");
            var echt = Path.Combine(Path.GetDirectoryName(Datenpfad.Wirtschaft())!, "katalog.json");
            if (!File.Exists(katalog) || File.ReadAllText(katalog) != File.ReadAllText(echt))
            {
                Directory.CreateDirectory(Path.Combine(wurzel, "daten"));
                File.Copy(echt, katalog, overwrite: true);
            }
        }
        return pfad;
    }
}
