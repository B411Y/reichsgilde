using System.Text.Json.Nodes;

namespace Reichsgilde.Core.Speichern;

/// <summary>
/// Spielstände aus Format 2 (Spieltag 05–22 Uhr, 1020 Minuten je Jahr) für den Spieltag 04–23 Uhr (1140 Minuten, Format 3) umrechnen.
///
/// Grundsatz: gleiche Uhrzeit im gleichen Jahr. Eine Spielminute t von früher (Jahr t ÷ 1020, Minute t mod 1020 nach 05:00) wird zu
/// Jahr × 1140 + 60 + Minute (dieselbe Minute nach 04:00 + der neuen ersten Stunde). Das gilt für die Uhr und für jeden Zeitpunkt im
/// Spielstand (Ankunft eines Karrens, Ende eines Ausbaus …); Dauern in Minuten bleiben, wie sie sind.
///
/// Dazu kommen die Ringe je Stunde (Verkäufe am Markt, Einkäufe der Bürger), die jetzt 19 statt 17 Stunden fassen, und die Daten im
/// Spielstand, deren Werte an der Länge des Jahres hängen (Datenversion bis 55).
/// Gerechnet wird am JSON-Text, bevor er gelesen wird – so bleiben die Klassen des Spielstands unberührt.
/// </summary>
public static class Uhrumstellung
{
    /// <summary>Minuten je Spieljahr in Format 2.</summary>
    public const int AltMinutenProTag = 1020;

    /// <summary>Stunden je Spieljahr in Format 2.</summary>
    public const int AltStundenProTag = 17;

    /// <summary>So viele Minuten liegt der alte Tagesbeginn (05:00) nach dem neuen (04:00).</summary>
    public const int Versatz = 5 * 60 - Zeit.Spielzeit.TagesbeginnMinute;

    /// <summary>Felder, die einen Zeitpunkt (Spielminute seit Spielbeginn) enthalten.</summary>
    private static readonly HashSet<string> Zeitpunkte = new(StringComparer.Ordinal)
    {
        "VergangeneMinuten", "FertigAb", "Beginn", "Ende", "LeerSeit", "BisMinute", "WohnhausFertigAb", "WohnhausInstandBis",
        "UnfallLauneBis", "AusfallBis", "EingestelltUm", "PraemieBis", "Ankunft", "Abmarsch", "TorWartenBis", "WareWartetSeit",
    };

    /// <summary>Ein Zeitpunkt aus Format 2 auf der neuen Uhr.</summary>
    public static long Zeitpunkt(long alt) =>
        alt / AltMinutenProTag * Zeit.Spielzeit.MinutenProTag + Versatz + alt % AltMinutenProTag;

    /// <summary>Eine ganze Zahl aus dem JSON (gelesen oder im Speicher gebaut), sonst null.</summary>
    private static long? Ganz(JsonValue v) =>
        long.TryParse(v.ToJsonString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var x) ? x : null;

    /// <summary>Rechnet einen Spielstand in Format 2 auf Format 3 um (andere bleiben, wie sie sind).</summary>
    public static void Umrechnen(JsonObject stand)
    {
        if ((int?)stand["Format"] != 2) return;
        if (stand["VergangeneMinuten"] is JsonValue v && Ganz(v) is { } t) stand["VergangeneMinuten"] = Zeitpunkt(t);
        if (stand["Wirtschaft"] is JsonNode w) Wirtschaft(w);
        if (stand["Daten"] is JsonObject d) Daten(d);
        stand["Format"] = 3;
    }

    private static void Wirtschaft(JsonNode knoten)
    {
        switch (knoten)
        {
            case JsonObject o:
                foreach (var (name, wert) in o.ToList())
                {
                    if (wert is JsonValue z && Zeitpunkte.Contains(name) && Ganz(z) is { } t)
                        o[name] = Zeitpunkt(t);
                    else if (wert is not null)
                        Wirtschaft(wert);
                }
                if (o["VerkaufteStueckProStunde"] is JsonArray verkauft && o["AktuellesFach"] is JsonValue fach)
                    MarktRing(o, verkauft, (int)fach);
                if (o["Einkaeufe"] is JsonArray einkaeufe && o["EinkaufFach"] is JsonValue naechstes)
                    EinkaufRing(o, einkaeufe, o["Warenkorb"] as JsonArray, (int)naechstes);
                // Die KI zählt die Stunden seit Tagesbeginn: Um dieselbe Uhrzeit ist jetzt eine Stunde mehr vergangen.
                if (o["StundenImJahr"] is JsonValue stunden && o.ContainsKey("Profil"))
                    o["StundenImJahr"] = (int)stunden + Versatz / 60;
                break;
            case JsonArray a:
                foreach (var e in a)
                    if (e is not null) Wirtschaft(e);
                break;
        }
    }

    /// <summary>
    /// Verkäufe am Markt: 18 Stundenfächer (das laufende und die 17 davor) werden 20. Die Reihenfolge bleibt, die zwei ältesten Fächer
    /// sind leer (vor 04:00 bzw. 05:00 hat der Markt zu); das laufende Fach ist das letzte.
    /// </summary>
    private static void MarktRing(JsonObject o, JsonArray alt, int fach)
    {
        var n = alt.Count;
        var neu = Zeit.Spielzeit.StundenProTag + 1;
        if (n == 0 || n >= neu) return;
        var werte = Enumerable.Range(1, n).Select(i => (double)alt[(fach + i) % n]!).ToList(); // vom ältesten bis zum laufenden Fach
        o["VerkaufteStueckProStunde"] = new JsonArray(Enumerable.Repeat(0.0, neu - n).Concat(werte).Select(x => (JsonNode)x).ToArray());
        o["AktuellesFach"] = neu - 1;
    }

    /// <summary>
    /// Einkäufe der Bürger: 17 Stunden (Ring; <c>EinkaufFach</c> ist das nächste, also älteste Fach) werden 19 – die zwei ältesten leer,
    /// geschrieben wird als Nächstes in das älteste.
    /// </summary>
    private static void EinkaufRing(JsonObject o, JsonArray einkaeufe, JsonArray? korb, int naechstes)
    {
        var n = einkaeufe.Count;
        var neu = Zeit.Spielzeit.StundenProTag;
        if (n == 0 || n >= neu) return;
        JsonArray Ring(JsonArray alt, Func<JsonNode?> leer) => new(Enumerable.Range(0, neu - n).Select(_ => leer())
            .Concat(Enumerable.Range(0, n).Select(i => alt[(naechstes + i) % n]?.DeepClone())).ToArray());
        o["Einkaeufe"] = Ring(einkaeufe, () => 0);
        if (korb is { Count: var k } && k == n) o["Warenkorb"] = Ring(korb, () => new JsonObject());
        o["EinkaufFach"] = 0;
    }

    /// <summary>
    /// Die Wirtschaftsdaten im Spielstand (bis Datenversion 55): Dauern von einem Jahr bzw. Werte je Stunde, die über ein Jahr gelten,
    /// auf das längere Jahr – wie in Datenversion 56 und (Fahrtzeiten der Schiffe) 57. Ruhen der Bürger den ganzen Tag (sonst schlüge die Prüfung der Daten fehl).
    /// </summary>
    private static void Daten(JsonObject d)
    {
        if ((int?)d["Version"] is not { } version || version >= 56) return;
        static void Minuten(JsonNode? o, string feld)
        {
            if (o is JsonObject x && x[feld] is JsonValue v && v.TryGetValue<double>(out var alt))
                x[feld] = (int)Math.Round(alt * Zeit.Spielzeit.MinutenProTag / AltMinutenProTag);
        }
        Minuten(d["Werkstatt"]?["Praemie"], "Minuten");
        Minuten(d["Werkstatt"]?["Unfall"], "LauneMinuten");
        Minuten(d["Werkstatt"]?["Befoerderung"], "DienstMinuten");
        Minuten(d["Gauner"]?["Ueberfall"], "GueltigMinuten");
        Minuten(d["Gauner"]?["Wohnhaus"]?["Brand"], "InstandMinuten");
        if (d["Politik"]?["Bauvorhaben"] is JsonArray bau)
            foreach (var b in bau.OfType<JsonObject>())
                if (b["BauzeitStunden"] is JsonValue h)
                    b["BauzeitStunden"] = (int)Math.Round((int)h * (double)Zeit.Spielzeit.StundenProTag / AltStundenProTag);
        if (d["Schiffshandel"] is JsonObject s && s["ErholungJeStunde"] is JsonValue e && e.TryGetValue<double>(out var erholung))
            s["ErholungJeStunde"] = Math.Round(1 - Math.Pow(1 - erholung, AltStundenProTag / (double)Zeit.Spielzeit.StundenProTag), 4);
        // Fahrtzeiten der Schiffe wachsen mit dem Spieltag (Datenversion 57): je Jahr gleich viele Fahrten wie vorher.
        if (d["Schiffshandel"]?["Schiffe"] is JsonObject schiffe)
            foreach (var (_, typ) in schiffe)
                foreach (var feld in new[] { "MinutenFlussab", "MinutenFlussauf", "LadeMinuten", "LiegeMinuten" })
                    Minuten(typ, feld);
        if (d["Buerger"]?["Handlungen"]?["Ruhen"] is JsonObject ruhen)
        {
            ruhen["Von"] = Zeit.Spielzeit.TagesbeginnMinute / 60;
            ruhen["Bis"] = Zeit.Spielzeit.TagesendeMinute / 60;
        }
    }
}
