using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Welche Meldungen des Kerns der Haushofmeister ansagt (<see cref="KlangAnsagen"/>), und nur, wenn sie das <b>eigene Haus</b> betreffen
/// (Mehrspieler: fremde Häuser sagt er nie an). Seit den Benachrichtigungen Stufe 2 meldet der Kern Art und Haus selbst (<see cref="Meldung"/>,
/// <c>Stadtwirtschaft.Gemeldet</c>) – die Erkennung liest nur noch <see cref="Meldung.Art"/>, <see cref="Meldung.Wichtigkeit"/> und
/// <see cref="Meldung.Haus"/>, keinen Wortlaut mehr. Feuer, Einbruch und Überfall nur, wenn sie gelangen (Wichtigkeit ab „hoch“; vereitelt oder
/// abgewehrt bleibt still). Die Ereignisse, die nicht als Meldung kommen (Aufstieg im Stand, Wahl, Ratssitzung, Jahresende), löst <c>Hauptszene</c>
/// direkt aus. Ohne Godot, damit der Test die Datei mitübersetzen kann (<c>AnsagenErkennungTests</c>).
/// </summary>
public static class KlangAnsagenErkennung
{
    /// <summary>Die Kennungen der Ereignisse – wie in <c>game/daten/ansagen.json</c>.</summary>
    public static class Id
    {
        public const string TodOberhaupt = "tod_oberhaupt", TodFamilie = "tod_familie", Feuer = "feuer", PestStadt = "pest_stadt",
            Krankheit = "krankheit", Ueberfall = "ueberfall", Diebstahl = "diebstahl", Anklage = "anklage", Schulden = "schulden",
            WahlVerloren = "wahl_verloren", WahlGewonnen = "wahl_gewonnen", StandAufstieg = "stand_aufstieg",
            Meisterpruefung = "meisterpruefung", Geburt = "geburt", Heirat = "heirat", Ratssitzung = "ratssitzung",
            KasseKnapp = "kasse_knapp", LagerLeer = "lager_leer", LagerVoll = "lager_voll", Jahresende = "jahresende";
    }

    /// <summary>Die Ansage zu einer Art (null: keine). Aufstieg, Wahl und Ratssitzung kommen nicht von hier (eigene Auslöser, sonst doppelt).</summary>
    public static string? Ansage(Meldung m) => m.Art switch
    {
        Meldungsart.TodOberhaupt => Id.TodOberhaupt,
        Meldungsart.TodFamilie => Id.TodFamilie,
        Meldungsart.Geburt => Id.Geburt,
        Meldungsart.Heirat => Id.Heirat,
        Meldungsart.Krankheit => Id.Krankheit,
        Meldungsart.Pest => Id.PestStadt,
        Meldungsart.Feuer when m.Wichtigkeit >= Meldungswichtigkeit.Hoch => Id.Feuer,
        Meldungsart.Diebstahl when m.Wichtigkeit >= Meldungswichtigkeit.Hoch => Id.Diebstahl,
        Meldungsart.Ueberfall when m.Wichtigkeit >= Meldungswichtigkeit.Hoch => Id.Ueberfall,
        Meldungsart.Anklage => Id.Anklage,
        Meldungsart.Schulden => Id.Schulden,
        Meldungsart.Meisterpruefung => Id.Meisterpruefung,
        Meldungsart.KasseKnapp => Id.KasseKnapp,
        Meldungsart.LagerLeer => Id.LagerLeer,
        Meldungsart.LagerVoll => Id.LagerVoll,
        _ => null,
    };

    /// <summary>
    /// Welches Ereignis ist das, und betrifft es das Haus <paramref name="spieler"/>? <c>null</c>, wenn die Meldung nichts Ansagbares ist
    /// oder ein fremdes Haus trifft. Die Pest trifft die ganze Stadt.
    /// </summary>
    public static string? Erkennen(Meldung m, Stadtwirtschaft w, Konto spieler)
    {
        if (Ansage(m) is not { } kennung) return null;
        if (m.Art == Meldungsart.Pest) return kennung;
        return w.HausVon(m) == spieler ? kennung : null;
    }

    /// <summary>Ist das Oberhaupt des Hauses eine Frau? Dann heißt die Anrede „Meine Herrin“ (sonst „Mein Herr“).</summary>
    public static bool Herrin(Stadtwirtschaft w, Konto spieler) =>
        w.Familien.Von(spieler)?.Oberhaupt.Geschlecht == Geschlecht.Frau;
}
