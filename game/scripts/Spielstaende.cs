using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Was das Hauptmenü der Spielszene mitgibt: eine neue Partie oder einen Spielstand zum Laden.
///
/// Warum statisch? Beim Szenenwechsel (Menü → Stadt) wird die alte Szene gelöscht. Statische Felder
/// gehören zu keiner Szene und überleben den Wechsel – ein einfacher „Briefkasten" zwischen Szenen.
/// </summary>
public static class Partiewahl
{
    public static Partieeinstellungen? Einstellungen { get; private set; }
    public static NeuePartie? Neu { get; private set; }
    public static string? LadePfad { get; private set; }

    public static void NeuePartieWaehlen(Partieeinstellungen einstellungen, NeuePartie neu)
    {
        Einstellungen = einstellungen;
        Neu = neu;
        LadePfad = null;
    }

    public static void LadenWaehlen(string pfad)
    {
        LadePfad = pfad;
        Einstellungen = null;
        Neu = null;
    }

    /// <summary>Holt den Auftrag ab und leert den Briefkasten.</summary>
    public static (Partieeinstellungen? Einstellungen, NeuePartie? Neu, string? LadePfad) Abholen()
    {
        var auftrag = (Einstellungen, Neu, LadePfad);
        Einstellungen = null;
        Neu = null;
        LadePfad = null;
        return auftrag;
    }
}

/// <summary>
/// Spielstände auf der Festplatte. Godot legt sie unter „user://" ab:
///   Windows: %APPDATA%\Godot\app_userdata\Reichsgilde\spielstaende
///   Linux:   ~/.local/share/godot/app_userdata/Reichsgilde/spielstaende
/// </summary>
public static class Spielstaende
{
    public const string Ordner = "user://spielstaende";
    public const string AutoDatei = "automatisch.json";

    /// <summary>Wo die Spielstände auf diesem Rechner wirklich liegen (zur Anzeige).</summary>
    public static string OrdnerAufDerPlatte => ProjectSettings.GlobalizePath(Ordner);

    /// <summary>Speichert die Partie. Ohne Dateinamen entsteht ein neuer, z. B. „Familie_Hammerschmid_1405_2026-10-13_14-30.json".</summary>
    /// <returns>Der Pfad der Datei oder null bei einem Fehler (mit Meldung).</returns>
    public static (string? Pfad, string Meldung) Speichern(Simulation simulation, Konto? spieler, string? dateiname = null)
    {
        DirAccess.MakeDirRecursiveAbsolute(Ordner);
        dateiname ??= $"{Bereinigen(spieler?.Inhaber ?? "Partie")}_{simulation.Zeit.Jahr}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
        var pfad = $"{Ordner}/{dateiname}";
        using var datei = Godot.FileAccess.Open(pfad, Godot.FileAccess.ModeFlags.Write);
        if (datei is null)
            return (null, $"Speichern fehlgeschlagen: {Godot.FileAccess.GetOpenError()}");
        datei.StoreString(Speicherung.Speichern(simulation, spieler));
        return (pfad, $"Gespeichert: {dateiname}");
    }

    /// <summary>Alle Spielstände, neueste zuerst – mit Titel und Zeitpunkt aus dem Dateikopf.</summary>
    public static List<(string Pfad, Speicherstand Kopf)> Liste()
    {
        var liste = new List<(string, Speicherstand)>();
        using var ordner = DirAccess.Open(Ordner);
        if (ordner is null) return liste;
        foreach (var name in ordner.GetFiles().Where(n => n.EndsWith(".json")))
        {
            var pfad = $"{Ordner}/{name}";
            try
            {
                liste.Add((pfad, Speicherung.Kopf(Godot.FileAccess.GetFileAsString(pfad))));
            }
            catch (SpielstandFehler)
            {
                // Kaputte Dateien tauchen in der Liste nicht auf.
            }
        }
        return liste.OrderByDescending(x => x.Item2.Gespeichert).ToList();
    }

    /// <summary>Lädt einen Spielstand.</summary>
    /// <exception cref="SpielstandFehler">Wenn die Datei fehlt oder nicht passt.</exception>
    public static (Simulation Simulation, Konto? Spieler) Laden(string pfad)
    {
        var json = Godot.FileAccess.GetFileAsString(pfad);
        if (string.IsNullOrEmpty(json))
            throw new SpielstandFehler($"Die Datei {pfad} gibt es nicht oder sie ist leer.");
        return Speicherung.Laden(json);
    }

    /// <summary>Nur Buchstaben, Ziffern und Unterstriche – Dateinamen sollen auf Windows und Linux gültig sein.</summary>
    private static string Bereinigen(string name) =>
        new(name.Select(z => char.IsLetterOrDigit(z) ? z : '_').ToArray());
}
