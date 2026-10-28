using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Die Karten der Städte aus <c>game/karten/&lt;kennung&gt;.json</c> (res://) mit dem Gebäudekatalog <c>game/daten/katalog.json</c>.
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
    public const string KatalogPfad = "res://daten/katalog.json";

    private static readonly Dictionary<string, Stadtkarte?> _karten = new();

    /// <summary>Die Karte der Greybox-Stadt, oder null (mit Fehlermeldung), wenn sie fehlt oder fehlerhaft ist.</summary>
    public static Stadtkarte? Greybox => Laden(Vorgabe);

    /// <summary>Die Kennung der Karte, mit der eine neue Partie beginnt: aus der Startoption <c>--karte=&lt;kennung&gt;</c>, sonst <see cref="Vorgabe"/>.</summary>
    public static string StartKennung => Gewaehlt ??
        (OS.GetCmdlineUserArgs().LastOrDefault(a => a.StartsWith("--karte="))?["--karte=".Length..] is { Length: > 0 } k ? k : Vorgabe);

    /// <summary>
    /// Die Karte, die der Spieler in der Kartenwahl des Hauptmenüs gewählt hat (Kennung wie bei <see cref="Laden"/>); null: keine Wahl, dann gilt
    /// <c>--karte=</c> bzw. <see cref="Vorgabe"/>. Die Startoptionen überspringen das Menü, darum kommen beide nie zusammen vor.
    /// </summary>
    public static string? Gewaehlt { get; set; }

    /// <summary>
    /// Die Karte einer neuen Partie (<see cref="StartKennung"/>) – Hauptmenü (Rivalen, Namen, Wappen) und Hauptszene nehmen dieselbe, sonst böte
    /// das Menü mit <c>--karte=probestadt</c> die drei Häuser der Greybox an.
    /// </summary>
    public static Stadtkarte? Startkarte => Laden(StartKennung);

    /// <summary>Wo die Kartenwerkstatt selbst gebaute Karten ablegt (<c>user://karten/x.json</c>); die Kennung einer solchen Karte ist der Dateiname mit „.json“.</summary>
    public const string EigeneKarten = "user://karten";

    /// <summary>Die Karte <c>res://karten/&lt;kennung&gt;.json</c> (eine Kennung auf „.json“: <c>user://karten/&lt;kennung&gt;</c>), oder null (mit Fehlermeldung), wenn sie fehlt oder fehlerhaft ist.</summary>
    public static Stadtkarte? Laden(string kennung)
    {
        if (_karten.TryGetValue(kennung, out var gemerkt)) return gemerkt;
        Stadtkarte? karte = null;
        var pfad = kennung.EndsWith(".json") ? $"{EigeneKarten}/{kennung}" : $"res://karten/{kennung}.json";
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

    /// <summary>Eine Karte in der Kartenwahl: ihre Kennung (für <see cref="Gewaehlt"/>), die Karte, ob sie selbst gebaut ist und ob sie nur eine Testkarte ist.</summary>
    public sealed record Angebot(string Kennung, Stadtkarte Karte, bool Eigene, bool Testkarte);

    /// <summary>
    /// Die Karten der Kartenwahl: die mitgelieferten mit dem Kennzeichen „spielbar“, dazu die selbst gebauten (<c>user://karten/</c>, ebenfalls nur
    /// mit dem Kennzeichen – eine neue Karte der Kartenwerkstatt hat es von Anfang an). Im Entwickler-Modus (<see cref="Entwickler.Aktiv"/>) alle,
    /// die nicht spielbaren als Testkarte. Selbst gebaute Karten werden jedes Mal neu gelesen, sie können sich in der Werkstatt geändert haben.
    /// </summary>
    public static List<Angebot> Angebote()
    {
        var liste = new List<Angebot>();
        void Aufnehmen(string kennung, bool eigene)
        {
            if (eigene) _karten.Remove(kennung);
            if (Laden(kennung) is not { } karte || !(karte.Spielbar || Entwickler.Aktiv)) return;
            liste.Add(new Angebot(kennung, karte, eigene, !karte.Spielbar));
        }
        foreach (var datei in DirAccess.GetFilesAt("res://karten").Where(d => d.EndsWith(".json")).Order())
            Aufnehmen(datei[..^".json".Length], eigene: false);
        foreach (var datei in DirAccess.GetFilesAt(EigeneKarten).Where(d => d.EndsWith(".json")).Order())
            Aufnehmen(datei, eigene: true);
        // spielbare vor Testkarten, mitgelieferte vor selbst gebauten, sonst nach Dateiname
        return liste.OrderBy(a => a.Testkarte).ThenBy(a => a.Eigene).ToList();
    }
}
