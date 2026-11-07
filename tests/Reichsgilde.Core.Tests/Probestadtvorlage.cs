using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Kartenbau.Staedte;

/// <summary>
/// Die Probestadt als Testkarte (Testzeit, 07.11.2026): Das Kartenskript (<see cref="Probestadt"/>) läuft einmal, jeder Test bekommt einen frischen
/// Entwurf aus seinem Text – das Öffnen dauert etwa ein Zehntel, das Skript je Lauf ein bis zwei Sekunden. Gleich wie nach dem Skript: der Text Byte
/// für Byte (er ist auch die Datei im Spiel, siehe KartengrenzenTests), die Befunde und die Partie daraus. Anders: Die Geschichte ist leer – die
/// gut hundert Schritte des Skripts lassen sich nicht zurücknehmen. Tests, die das Skript selbst prüfen, rufen weiter <see cref="Probestadt"/> auf.
/// </summary>
static class Probestadtvorlage
{
    static readonly Lazy<string> Text = new(() =>
    {
        var datenordner = Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
        // Die Daten ändern am Text nichts (sie dienen der Prüfung der Startbelegung); das Skript braucht welche.
        return new Probestadt().Bauen(Gebaeudekatalog.AusDatei(Path.Combine(datenordner, "katalog.json")), Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft())).ZuJson();
    });

    /// <summary>Ein frischer Entwurf der Probestadt mit diesen Daten (wie <c>new Probestadt().Bauen(katalog, daten)</c>, nur ohne Geschichte).</summary>
    public static Kartenentwurf Entwurf(Gebaeudekatalog katalog, Wirtschaftsdaten daten)
    {
        var e = Kartenentwurf.AusJson(Text.Value, katalog);
        e.Daten = daten;
        return e;
    }
}
