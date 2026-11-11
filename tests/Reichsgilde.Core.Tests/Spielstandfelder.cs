using System.Text.Json.Nodes;

/// <summary>
/// Felder, die neuere Kerne in jeden Spielstand schreiben, auch wenn sie nichts ändern – für die Sollwerttests, die den Spielstand mit einem
/// älteren Kern vergleichen (SHA-256). Sie werden nur entfernt, solange sie den Wert „nichts“ tragen (false, 0, leer oder null); trägt eins
/// einen anderen Wert, bleibt es stehen, und der Vergleich schlägt zu Recht fehl.
/// </summary>
static class Spielstandfelder
{
    /// <summary>
    /// Datenversion 73 (Karren zum Hafen, Handgeld): Karren.ZielHafen, Handgeld in Jahres- und Betriebsrechnung, Fahrten zum Hafen im Wegebau.
    /// </summary>
    public static readonly string[] Hafenziel = { "ZielHafen", "Handgeld", "FahrtenHafen", "FahrtenHafenVorjahr" };

    /// <summary>
    /// Datenversion 75: Felder, die nur das Laden braucht – eine durchgehende Partie rechnet mit und ohne sie gleich (das Übliche der Einkäufe,
    /// mit dem der Faktor am Markt bis zur nächsten vollen Stunde nach dem Jahresende noch gerechnet ist).
    /// </summary>
    public static readonly string[] NurLaden = { "EinkaufErwartetImMarkt" };

    /// <summary>
    /// Die dauerhafte Hauschronik (Datenversion 83): Die Simulation liest sie nie, sie hält nur fest, was geschah – „Hauschronik“ (Format) und
    /// „Chronik“ (Einträge je Familie) fallen wie der Verlauf der Meldungen aus dem Vergleich.
    /// </summary>
    public static readonly string[] Hauschronik = { "Hauschronik", "Chronik" };

    /// <summary>Entfernt diese Felder überall im Spielstand, auch wenn sie etwas tragen (für Felder aus <see cref="NurLaden"/>).</summary>
    public static void Ohne(JsonNode? knoten, IReadOnlyCollection<string> felder)
    {
        switch (knoten)
        {
            case JsonObject o:
                foreach (var (name, wert) in o.ToList())
                {
                    if (felder.Contains(name)) o.Remove(name);
                    else Ohne(wert, felder);
                }
                break;
            case JsonArray a:
                foreach (var e in a) Ohne(e, felder);
                break;
        }
    }

    /// <summary>Entfernt diese Felder überall im Spielstand, wo sie „nichts“ tragen.</summary>
    public static void OhneLeere(JsonNode? knoten, IReadOnlyCollection<string> felder)
    {
        switch (knoten)
        {
            case JsonObject o:
                foreach (var (name, wert) in o.ToList())
                {
                    if (felder.Contains(name) && IstLeer(wert)) o.Remove(name);
                    else OhneLeere(wert, felder);
                }
                break;
            case JsonArray a:
                foreach (var e in a) OhneLeere(e, felder);
                break;
        }
    }

    static bool IstLeer(JsonNode? wert) => wert switch
    {
        null => true,
        JsonObject o => o.Count == 0,
        JsonArray a => a.Count == 0,
        JsonValue v when v.TryGetValue<bool>(out var b) => !b,
        JsonValue v when v.TryGetValue<double>(out var d) => d == 0,
        _ => false,
    };
}
