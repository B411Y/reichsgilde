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
    /// Das Erbgut der Personen (Allele für den 3D-Kopf): Die Simulation liest es nie, es hat eigene Würfel je Person (kein Wurf der gemeinsamen
    /// Zufallsquelle). Ändert sich die Gen-Vorlage (Bart der Zwerginnen, 11.11.2026), ändern sich nur diese Texte – die Partie rechnet gleich.
    /// </summary>
    public static readonly string[] Erbgut = { "Erbgut" };

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

    /// <summary>
    /// Karrenarten ohne Ochsen (Datenversion 98, Spielstand-Format 4): Nur die Namen ändern sich (Pferdekarren war der Ochsenkarren, Großer
    /// Pferdekarren der Pferdekarren), die Partie rechnet gleich. Für den Vergleich mit Sollwerten eines älteren Kerns: Format 4 → 3 und die alten
    /// Namen zurück, in einem Durchgang (Schlüssel, Werte, Texte).
    /// </summary>
    public static void MitAltenKarrennamen(JsonObject stand)
    {
        if ((int?)stand["Format"] == 4) stand["Format"] = 3;
        AlteNamen(stand);
    }

    static string AlterName(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, "Großer Pferdekarren|Pferdekarren", m => m.Value == "Pferdekarren" ? "Ochsenkarren" : "Pferdekarren");

    static void AlteNamen(JsonNode? knoten)
    {
        switch (knoten)
        {
            case JsonObject o:
                var eintraege = o.ToList();
                if (eintraege.Any(e => AlterName(e.Key) != e.Key))
                {
                    o.Clear();
                    foreach (var (name, wert) in eintraege) o[AlterName(name)] = wert;
                }
                foreach (var (name, wert) in o.ToList())
                    if (wert is JsonValue v && v.TryGetValue<string>(out var t)) { if (AlterName(t) != t) o[name] = AlterName(t); }
                    else AlteNamen(wert);
                break;
            case JsonArray a:
                for (var i = 0; i < a.Count; i++)
                    if (a[i] is JsonValue v && v.TryGetValue<string>(out var t)) { if (AlterName(t) != t) a[i] = AlterName(t); }
                    else AlteNamen(a[i]);
                break;
        }
    }
}
