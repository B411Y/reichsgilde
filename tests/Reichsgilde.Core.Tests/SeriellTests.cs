// ─────────────────────────────────────────────────────────────────────────────
//  Serielle Testklassen: laufen mit  --parallel  am Ende allein, eine nach der anderen
//  (ohne den Schalter und in der CI läuft ohnehin alles nacheinander).
//
//  Hierher gehört eine Klasse, deren Tests eine Zeitgrenze prüfen („unter 150 ms“, „schneller als …“)
//  oder etwas teilen, das für den ganzen Prozess gilt (Umgebungsvariable, aktuelles Verzeichnis,
//  Datei mit festem Namen außerhalb des eigenen Ausgabeordners, Console.SetOut). Unter Volllast
//  aller Kerne würde eine Zeitgrenze sonst zufällig reißen.
//  ParallellaufTests prüft, dass jede Klasse hier noch Tests hat (ein Umbenennen fällt auf).
// ─────────────────────────────────────────────────────────────────────────────

static class SeriellTests
{
    /// <summary>Klassenname (ganzer Name) und Grund.</summary>
    public static readonly (string Klasse, string Grund)[] Liste =
    {
        ("KlangUmgebungTests", "Raster jeder Karte unter 1.500 ms; die Selbstprüfung tauscht kurz das statische KlangUmgebungModell.Gespraeche"),
        ("GreyboxTests", "Bauorte unter 500 ms je Abfrage"),
        ("KartenbauTests", "Kapazität Köln unter 150 ms (Median)"),
        ("KartenwerkstattTests", "volle Prüfung unter 20 ms, eine Stelle unter 2 ms (Median)"),
        ("KartenwerkstattWegeTests", "Tannenwald unter 10 ms, volle Prüfung unter 20 ms (Median)"),
        ("MarktnetzTests", "Netz schneller als Raster (zwei Stoppuhren im Wechsel)"),
        ("BodenTests", "Bodendeko unter 2 s bzw. 10 s"),
    };

    public static bool IstSeriell(string klasse) =>
        Liste.Any(e => string.Equals(e.Klasse, klasse, StringComparison.OrdinalIgnoreCase));
}
