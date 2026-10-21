using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Die Karten der Städte aus <c>game/data/karten/&lt;kennung&gt;.json</c> (res://) mit dem Gebäudekatalog <c>game/data/katalog.json</c>.
/// Wie <c>wirtschaft.json</c> liest Godot die Dateien, der Kern bekommt nur den Text (<see cref="Stadtkarte.AusJson"/>).
///
/// Jede Karte wird einmal gelesen und gemerkt: Die Hauptszene braucht sie beim Erstellen oder Laden der Partie, das Hauptmenü
/// für die Namen und Wappen der KI-Häuser, die Stadtansicht ohne Partie (Hauptmenü) zum Zeichnen. Fehlt eine Datei oder ist sie
/// kaputt, meldet <see cref="GD.PushError(string)"/> das laut – dann schlägt auch der Rauchtest der CI an –, und das Spiel läuft
/// ohne Karte weiter (ohne Stadt und ohne Bauwesen).
/// </summary>
public static class Stadtkarten
{
    /// <summary>Die Karte, mit der eine neue Partie beginnt (die Startoption <c>--karte=&lt;kennung&gt;</c> wählt eine andere).</summary>
    public const string Vorgabe = "greybox";

    /// <summary>Der Gebäudekatalog für alle Karten (was ein Baustein ist: Modell, Grundfläche).</summary>
    public const string KatalogPfad = "res://data/katalog.json";

    private static readonly Dictionary<string, Stadtkarte?> _karten = new();

    /// <summary>Die Karte der Greybox-Stadt, oder null (mit Fehlermeldung), wenn sie fehlt oder fehlerhaft ist.</summary>
    public static Stadtkarte? Greybox => Laden(Vorgabe);

    /// <summary>Die Karte <c>res://data/karten/&lt;kennung&gt;.json</c>, oder null (mit Fehlermeldung), wenn sie fehlt oder fehlerhaft ist.</summary>
    public static Stadtkarte? Laden(string kennung)
    {
        if (_karten.TryGetValue(kennung, out var gemerkt)) return gemerkt;
        Stadtkarte? karte = null;
        var pfad = $"res://data/karten/{kennung}.json";
        try
        {
            var json = Godot.FileAccess.FileExists(pfad) ? Godot.FileAccess.GetFileAsString(pfad) : "";
            var katalog = Godot.FileAccess.GetFileAsString(KatalogPfad);
            if (string.IsNullOrEmpty(json))
                GD.PushError($"Die Karte {pfad} wurde nicht gefunden – ohne sie gibt es keine Stadt und es wird nicht gebaut.");
            else if (string.IsNullOrEmpty(katalog))
                GD.PushError($"Der Gebäudekatalog {KatalogPfad} wurde nicht gefunden – ohne ihn gibt es keine Stadt und es wird nicht gebaut.");
            else
                karte = Stadtkarte.AusJson(json, Gebaeudekatalog.AusJson(katalog));
        }
        catch (KartenFehler fehler)
        {
            GD.PushError($"{pfad}: {fehler.Message}");
        }
        return _karten[kennung] = karte;
    }
}
