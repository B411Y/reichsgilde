// ─────────────────────────────────────────────────────────────────────────────
//  Langsame Tests: werden mit  --ohne-langsam  übersprungen (nicht abgeschaltet –
//  ohne Schalter und in der CI laufen sie wie alle anderen).
//
//  Ein Eintrag ist eine Testklasse (alle ihre Tests) oder Klasse plus Muster für
//  den Testnamen (ohne Groß- und Kleinschreibung, * für beliebigen Text, das
//  Muster darf irgendwo im Namen stehen; ein Komma im Muster ist hier erlaubt). TestfilterTests prüft, dass jeder
//  Eintrag genau so viele Tests trifft wie angegeben – ein umbenannter Test fällt so
//  auf, ebenso ein Muster, das plötzlich auch schnelle Tests mitnimmt.
//
//  Messen: dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- --zeiten=60
// ─────────────────────────────────────────────────────────────────────────────

static class LangsameTests
{
    /// <summary>Klasse, Namensmuster (null = alle Tests der Klasse) und wie viele Tests der Eintrag trifft.</summary>
    public static readonly (string Klasse, string? Test, int Anzahl)[] Liste =
    {
        // Gemessen am 07.11.2026 auf main 36e57cd6 (--zeiten=250, NAS, Last 4,5–12,5): jeder Test hier lief
        // allein mindestens 3 s; zusammen etwa 7,6 der 11 Minuten. Neue langsame Tests unten anhängen.
        ("BuergerTorwacheTests", "Spielstand geladen – dieselben Wächter", 1),          // 233 s
        ("FahrzeitAmBauortTests", "Köln – vor dem Bau dieselbe Fahrzeit", 1),           // 22 s
        ("BauzoneNurTests", "Bauorte für Wohnhäuser meiden die Werkstatt-Bauplätze", 1), // 21 s
        ("KlangUmgebungTests", "Umgebungsklänge: die Selbstprüfung", 2),               // 17 s + 16 s
        ("KiStandortTests", "Die Fahrzeit eines geplanten Bauorts", 1),                 // 15 s
        ("BlitzTests", "Mit Chance 1 schlägt es mitten im ersten Gewitter ein", 1),     // 13 s
        ("BlitzTests", "Nach Speichern und Laden mitten im Brand", 1),                  // 11 s
        ("BlitzTests", "Chance 0 – zwanzig Jahre Greybox", 1),                          // 5 s
        ("BlitzTests", "Ein Feuer vom späten Abend brennt über den Jahreswechsel", 1),  // 3 s
        ("GleichheitTests", "Startwert", 4),                                            // 4 Partien, zusammen 26 s
        ("DynastieTests", "Haus Eisenbart wirbt und heiratet", 1),                      // 10 s
        ("AussehenTests", "Kinder im Spiel tragen ein Gesicht ihrer Eltern", 1),        // 7 s
        ("GreyboxTests", "Bauorte – genau die gültigen Rasterstellen", 1),              // 6 s
        ("GesundheitTests", "Etwa 15 % der Familienmitglieder erkranken", 1),          // 5 s
        ("FahrwegeAmBauortTests", "Köln – der beste Weg wie FahrzeitAmBauort", 1),      // 5 s
        ("FahrwegeAmBauortTests", "nach dem Bau fahren die Karren genau den besten Weg", 1), // 5 s
        ("FahrzeitWeglaengeTests", "Gesparte Minuten eines Ausbaus", 1),                // 5 s
        ("PassantenspurTests", "die Füße rutschen nicht", 1),                           // 4 s
        ("KartenbauTests", "schreibt nicht über eine vorhandene Datei", 1),             // 4 s
        ("GelaendeTests", "bergauf zum Markt länger", 1),                               // 4 s
        ("AktionspunkteTests", "hält sich an die AP", 1),                               // 4 s
        ("Balancing2Tests", "Gelingt in der ganzen Nacht nichts, bleibt eine Spur", 1), // 4 s
        ("LogistikTests", "Normale Eskorte halbiert den Raub", 1),                      // 3 s
        ("WegebauTests", "Austreten und Verwildern", 1),                                // 3 s
        ("KiSchiffTests", "Zwei Jahre KI-Schiffshandel", 1),                            // 3 s
        ("WasserTests", "Eine Furt auf dem Weg zum Markt kostet", 1),                   // 3 s
    };

    public static bool IstLangsam(Testfall test) =>
        Liste.Any(e => string.Equals(e.Klasse, test.Klasse, StringComparison.OrdinalIgnoreCase)
                       && (e.Test is null || Mustervergleich.Enthaelt(e.Test, test.Name)));
}
