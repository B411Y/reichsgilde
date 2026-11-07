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
        // Gemessen am 07.11.2026 auf main 10f77b45 + Testkürzung (--zeiten=250, NAS, Last 4–17): jeder Test hier lief allein etwa 3 s oder mehr
        // (vorher 30 Einträge; schneller geworden und gestrichen: Bauorte in Köln, Probestadtvorlage, Blitz-Startwert, Torwache seit #354).
        // Neue langsame Tests unten anhängen.
        ("KlangUmgebungTests", "Umgebungsklänge: die Selbstprüfung", 2),               // je 6–7 s (vorher 14 s)
        ("DynastieTests", "Haus Eisenbart wirbt und heiratet", 1),                      // 10 s: 10 Partien × 20 Jahre, Statistik
        ("AussehenTests", "Kinder im Spiel tragen ein Gesicht ihrer Eltern", 1),        // 5–10 s: 20 + 2 × 15 Jahre
        ("GleichheitTests", "Startwert", 4),                                            // 4 Partien, zusammen 14 s
        ("Balancing2Tests", "Gelingt in der ganzen Nacht nichts, bleibt eine Spur", 1), // 3–6 s: 200 Nächte, Statistik
        ("GesundheitTests", "Etwa 15 % der Familienmitglieder erkranken", 1),          // 3–5 s: 60 Startwerte, Statistik
        ("AktionspunkteTests", "hält sich an die AP", 1),                               // 3–5 s: 15 Jahre KI
        ("LogistikTests", "Normale Eskorte halbiert den Raub", 1),                      // 2–5 s: 300 Jahre, Statistik
        ("FahrzeitAmBauortTests", "Köln – vor dem Bau dieselbe Fahrzeit", 1),           // 3 s (vorher 22 s)
        ("GreyboxTests", "Bauorte – genau die gültigen Rasterstellen", 1),              // 3 s: jede Rasterstelle der Stadt
        ("StartwegpruefungTests", "ein Gebäude ohne Startplatz rechnet nichts neu", 1), // 3 s: Startwege in Köln
        ("BlitzTests", "Ein Feuer vom späten Abend brennt über den Jahreswechsel", 1),  // 3 s
    };

    public static bool IstLangsam(Testfall test) =>
        Liste.Any(e => string.Equals(e.Klasse, test.Klasse, StringComparison.OrdinalIgnoreCase)
                       && (e.Test is null || Mustervergleich.Enthaelt(e.Test, test.Name)));
}
