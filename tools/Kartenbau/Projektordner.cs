namespace Reichsgilde.Kartenbau;

/// <summary>Wo die Dateien des Spiels liegen: der Ordner „reichsgilde“ mit game/daten/katalog.json, gesucht vom Arbeitsordner und vom Programm aus nach oben.</summary>
public sealed record Projektordner(string Wurzel)
{
    public string Daten => Path.Combine(Wurzel, "game", "daten");
    public string Katalog => Path.Combine(Daten, "katalog.json");
    public string Wirtschaft => Path.Combine(Daten, "wirtschaft.json");
    public string Karten => Path.Combine(Wurzel, "game", "karten");
    public string Modelle => Path.Combine(Wurzel, "game", "assets", "modelle");
    public string Vorschauen => Path.Combine(Wurzel, "docs", "karten");

    /// <summary>Die Kartendatei <c>game/karten/&lt;kennung&gt;.json</c>.</summary>
    public string Karte(string kennung) => Path.Combine(Karten, kennung + ".json");

    /// <summary>Sucht den Projektordner; null, wenn es keinen gibt.</summary>
    public static Projektordner? Finden()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var ordner = new DirectoryInfo(start); ordner is not null; ordner = ordner.Parent)
                if (File.Exists(Path.Combine(ordner.FullName, "game", "daten", "katalog.json")))
                    return new Projektordner(ordner.FullName);
        return null;
    }
}
