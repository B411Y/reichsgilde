using System.Text.RegularExpressions;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Partie;

/// <summary>Eine Hofstelle zur Wahl im Startmenü: Kennung, Name, Karrenweg der Werkstatt zum nächsten Marktplatz (null: kein Weg) und dessen Name.</summary>
public sealed record Hofstellenangebot(string Kennung, string Name, double? Meter, string? Markt);

/// <summary>
/// Vergibt die Hofstellen einer neuen Partie (Nutzerentscheidung 09.11.2026, siehe <see cref="Hofstelle"/>):
///
///   • Der Spieler bekommt die gewählte Start-Hofstelle (<see cref="NeuePartie.Hofstelle"/>), ohne Wahl eine geloste.
///   • Jedes mitspielende KI-Haus bekommt eine geloste unter den übrigen Start-Hofstellen – in Kontoreihenfolge, je eine aus der Liste in
///     der Reihenfolge der Karte. Gelost wird mit einer eigenen Zufallsquelle aus dem Startwert der Partie (<see cref="Losquelle"/>), nur hier: Alle
///     übrigen Würfe der Partie bleiben, wie sie ohne Hofstellen wären, und jeder Rechner einer Mehrspielerpartie lost dasselbe.
///   • Mit <see cref="Startplatzvergabe.Vorgabe"/> sitzt jedes Haus, wo die Startbelegung der Karte es hinsetzt.
///
/// Das Ergebnis ist die <b>Partiekarte</b> (<see cref="Belegen"/>): die Karte mit der Startbelegung auf den vergebenen Hofstellen. Die Werkstatt
/// eines KI-Hauses nimmt ihren Namen mit an den neuen Werkstattplatz, nicht mitspielende Häuser fallen heraus. Die Partiekarte steht wie jede
/// Karte im Spielstand; danach rechnet der Kern wie mit festen Startplätzen (Wohnhausplätze in Kontoreihenfolge, Betriebe nach Namen am Platz).
/// Die freien Hofstellen werden am Ende des Aufbaus Bauplätze (<see cref="Bauwesen.FreieHofstellenFreigeben"/>).
/// </summary>
public static class Hofstellenvergabe
{
    /// <summary>
    /// Die eigene Zufallsquelle des Loses: ihr Startwert ist der Startwert der Partie, mit <see cref="LosSchluessel"/> gemischt
    /// (<c>Zugwuerfel.Mischen</c>). Nicht einfach Startwert · k + c wie bei den Wesen: Der erste Wurf von <see cref="Random"/> hängt bei nahen
    /// Startwerten kaum vom Startwert ab (die Startwerte 1 bis 12 losten den Spieler auf nur zwei der vier Hofstellen der Greybox) – und das Los
    /// besteht aus genau diesen ersten Würfen.
    /// </summary>
    public const int LosSchluessel = 0x486F66; // „Hof“

    internal static Zufallsquelle Losquelle(int zufallsstartwert) => new(Zugwuerfel.Mischen(zufallsstartwert, LosSchluessel, 89));

    /// <summary>Die Hofstellen, die als Startplatz taugen, in der Reihenfolge der Karte.</summary>
    public static IReadOnlyList<Hofstelle> Startplaetze(Stadtkarte karte) => karte.Hofstellen.Where(h => h.Start).ToList();

    /// <summary>Die Werkstatt eines KI-Hauses, die auf seine Hofstelle gehört: der erste Startbetrieb auf einem Werkstattplatz (null: keiner).</summary>
    internal static Startbetrieb? Hofwerkstatt(Stadtkarte karte, Starthaus h) =>
        h.Betriebe.FirstOrDefault(b => karte.GebaeudeMit(b.Platz) is { Art: Bausteinart.Werkstattplatz });

    /// <summary>Die Hofstelle, auf die die Startbelegung den Spieler setzt (über seinen Werkstattplatz, sonst sein Wohnhaus) – oder null.</summary>
    public static Hofstelle? VorgabeSpieler(Stadtkarte karte) =>
        karte.HofstelleVon(karte.Startbelegung.Spieler.Werkstatt.Platz) ?? karte.HofstelleVon(karte.Startbelegung.Spieler.Wohnhaus);

    /// <summary>Die Hofstelle, auf die die Startbelegung dieses KI-Haus setzt (über seine Werkstatt, sonst sein Wohnhaus) – oder null.</summary>
    public static Hofstelle? VorgabeVon(Stadtkarte karte, Starthaus h) =>
        karte.HofstelleVon(Hofwerkstatt(karte, h)?.Platz) ?? karte.HofstelleVon(h.Wohnhaus);

    /// <summary>
    /// Die Start-Hofstellen zur Wahl im Startmenü, in der Reihenfolge der Karte, mit dem Karrenweg ihrer Werkstatt zum nächsten Marktplatz
    /// (gemessen wie die Kartenprüfung, <see cref="Startwegpruefung"/>). Leer ohne Hofstellen. Rechnet das Wegenetz einmal (Köln einige 10 ms).
    /// </summary>
    public static IReadOnlyList<Hofstellenangebot> Angebot(Stadtkarte karte)
    {
        var plaetze = Startplaetze(karte);
        if (plaetze.Count == 0) return Array.Empty<Hofstellenangebot>();
        var wege = Startwegpruefung.Hofstellen(karte).ToDictionary(w => w.Platz, StringComparer.Ordinal); // nur nachgeschlagen
        return plaetze.Select(h => wege.TryGetValue(h.Werkstatt, out var w)
                ? new Hofstellenangebot(h.Kennung, h.Anzeigename, w.Meter, w.Markt)
                : new Hofstellenangebot(h.Kennung, h.Anzeigename, null, null))
            .ToList();
    }

    /// <summary>
    /// Wer bekommt welche Hofstelle: der Spieler und die mitspielenden KI-Häuser (in deren Reihenfolge). Null heißt (nur ohne Los): Das Haus
    /// bleibt genau, wie die Startbelegung es setzt (auch ein Wohnhaus oder Startplatz je Beruf außerhalb der Hofstellen). Wirft, wenn die Wahl
    /// nicht geht oder die Start-Hofstellen nicht reichen.
    /// </summary>
    public static (Hofstelle? Spieler, IReadOnlyList<Hofstelle?> Haeuser) Zuteilen(Stadtkarte karte, NeuePartie neu, int zufallsstartwert,
        IReadOnlyList<Starthaus> mitspielend)
    {
        var plaetze = Startplaetze(karte);
        Hofstelle? Gewaehlt()
        {
            if (neu.Hofstelle is not { } k) return null;
            var h = karte.HofstelleMit(k) ?? throw new ArgumentException($"Die Hofstelle „{k}“ gibt es auf dieser Karte nicht.");
            return h.Start ? h : throw new ArgumentException($"„{h.Anzeigename}“ ist kein Startplatz – dort wird erst später gebaut.");
        }
        var gewaehlt = Gewaehlt();

        if (neu.Startplaetze == Startplatzvergabe.Vorgabe)
        {
            // Ohne Los bleibt jedes Haus, wie die Startbelegung es setzt (null). Wählt der Spieler die Hofstelle eines KI-Hauses, bekommt das Haus
            // die Vorgabe des Spielers (getauscht) – oder, hat er keine, die erste unbelegte Start-Hofstelle.
            var vorgaben = mitspielend.Select(h => VorgabeVon(karte, h)).ToList();
            var vorgabe = VorgabeSpieler(karte);
            if (gewaehlt is null || gewaehlt == vorgabe) return (null, vorgaben.Select(_ => (Hofstelle?)null).ToList());
            var haeuser = vorgaben.Select(v => v == gewaehlt
                ? vorgabe ?? plaetze.FirstOrDefault(p => p != gewaehlt && !vorgaben.Contains(p))
                  ?? throw new ArgumentException("Die Karte hat keine freie Start-Hofstelle für das Haus, dessen Hofstelle der Spieler wählt.")
                : null).ToList();
            return (gewaehlt, haeuser);
        }

        if (plaetze.Count < 1 + mitspielend.Count)
            throw new ArgumentException($"Die Karte hat {plaetze.Count} Startplätze (Hofstellen) – für den Spieler und {mitspielend.Count} KI-Häuser zu wenig.");
        var frei = plaetze.ToList();
        var los = Losquelle(zufallsstartwert);
        var eigene = gewaehlt ?? frei[los.Next(frei.Count)];
        frei.Remove(eigene);
        var gelost = new List<Hofstelle?>();
        foreach (var _ in mitspielend)
        {
            var h = frei[los.Next(frei.Count)];
            frei.Remove(h);
            gelost.Add(h);
        }
        return (eigene, gelost);
    }

    /// <summary>
    /// Die Partiekarte (siehe Kopf): die Karte mit der Startbelegung auf den Hofstellen aus <see cref="Zuteilen"/>. Ohne Hofstellen die Karte
    /// selbst. <paramref name="mitspielend"/>: die KI-Häuser, die mitspielen (nur sie stehen in der Partiekarte).
    /// </summary>
    internal static Stadtkarte Belegen(Stadtkarte karte, Wirtschaftsdaten daten, NeuePartie neu, int zufallsstartwert, IReadOnlyList<Starthaus> mitspielend)
    {
        if (!karte.HatHofstellen) return karte;
        var start = karte.Startbelegung;
        var (spieler, haeuser) = Zuteilen(karte, neu, zufallsstartwert, mitspielend);

        // Erst alle Werkstattplätze der Hofstellen und die Plätze der Hofwerkstätten ohne Namen, dann die Namen an die vergebenen Plätze.
        var betriebe = new Dictionary<string, string?>(StringComparer.Ordinal); // nur nachgeschlagen (MitBelegung geht die Gebäude der Reihe nach durch)
        foreach (var h in karte.Hofstellen) betriebe[h.Werkstatt] = null;
        var neueHaeuser = new List<Starthaus>();
        var namen = new List<(string Platz, string Name)>();
        for (var i = 0; i < mitspielend.Count; i++)
        {
            var h = mitspielend[i];
            var hw = Hofwerkstatt(karte, h);
            var name = hw is null ? null : karte.GebaeudeMit(hw.Platz)?.Betrieb ?? $"{daten.Art(hw.Art).Name} {Familie.NachnameAus(h.Name)}";
            if (hw is not null) betriebe[hw.Platz] = null;
            if (haeuser[i] is not { } ziel)
            {
                // ohne Hofstelle bleibt das Haus, wie die Startbelegung es setzt
                neueHaeuser.Add(h);
                if (hw is not null && name is not null) namen.Add((hw.Platz, name));
                continue;
            }
            neueHaeuser.Add(new Starthaus
            {
                Name = h.Name, Volk = h.Volk, Geschlecht = h.Geschlecht, Wappen = h.Wappen, Farbe = h.Farbe, Wohnhaus = ziel.Wohnhaus,
                Betriebe = h.Betriebe.Select(b => b == hw ? new Startbetrieb { Platz = ziel.Werkstatt, Art = b.Art, Produkt = b.Produkt } : b).ToList(),
            });
            if (hw is not null && name is not null) namen.Add((ziel.Werkstatt, name));
        }
        foreach (var (platz, name) in namen) betriebe[platz] = name;

        var belegung = new Startbelegung
        {
            // ohne Los auf seiner Vorgabe: genau wie die Startbelegung (mit den Startplätzen je Beruf)
            Spieler = spieler is null ? start.Spieler : new Spielerstart
            {
                Farbe = start.Spieler.Farbe, Wohnhaus = spieler.Wohnhaus,
                Werkstatt = new Startwerkstatt { Platz = spieler.Werkstatt, Name = WerkstattnameSpieler(karte, spieler) },
            },
            Haeuser = neueHaeuser,
            Hofstellen = start.Hofstellen,
        };
        return karte.MitBelegung(belegung, betriebe);
    }

    /// <summary>
    /// Wie die Werkstatt des Spielers auf dieser Hofstelle heißt (mit „{art}“ für die Betriebsart): auf seiner Hofstelle aus der Startbelegung
    /// wie dort („{art} am Weinmarkt“), sonst nach dem Namen der Hofstelle („Heumarkt“ → „{art} am Heumarkt“; „an der Rheingasse“ bleibt), ohne
    /// Namen nach der nächsten Karrenstraße wie eine neu gebaute Werkstatt, ohne Straße nach der Kennung.
    /// </summary>
    public static string WerkstattnameSpieler(Stadtkarte karte, Hofstelle h)
    {
        if (VorgabeSpieler(karte) == h && karte.Start is not null && karte.Startbelegung.Spieler.Werkstatt.Name != "") return karte.Startbelegung.Spieler.Werkstatt.Name;
        if (!string.IsNullOrWhiteSpace(h.Name))
            return "{art} " + (MitPraeposition(h.Name) ? h.Name : Bauwesen.Lagebezeichnung(h.Name));
        if (karte.GebaeudeMit(h.Werkstatt) is { } g)
        {
            var weg = karte.Wege.Where(w => w.IstKarrenstrasse && w.Name != "")
                .OrderBy(w => w.Abstand(g.Punkt[0], g.Punkt[1])).ThenBy(w => w.Name, StringComparer.Ordinal).FirstOrDefault();
            if (weg is not null) return "{art} " + Bauwesen.Lagebezeichnung(weg.Name);
        }
        return "{art} " + h.Kennung;
    }

    /// <summary>
    /// Bringt der Name seine Präposition schon mit („am Malzbüchel“, „an der Rheingasse“, „Unter Fettenhennen“)? Dann steht er so hinter der
    /// Betriebsart; sonst sucht <see cref="Bauwesen.Lagebezeichnung"/> sie („Heumarkt“ → „am Heumarkt“).
    /// </summary>
    internal static bool MitPraeposition(string name)
    {
        var erstes = name.Trim().Split(' ')[0];
        return char.IsLower(erstes[0]) || Praepositionen.Contains(erstes.ToLowerInvariant());
    }

    private static readonly HashSet<string> Praepositionen = new(StringComparer.Ordinal)
    {
        "am", "an", "auf", "aus", "bei", "beim", "hinter", "im", "in", "neben", "über", "unter", "vor", "vorm", "zum", "zur",
    };

    /// <summary>
    /// Eine Karte von vor den Hofstellen: Jedes Startpaar (Spieler und jedes KI-Haus: Wohnhausplatz und Werkstattplatz der Startbelegung) wird
    /// eine Start-Hofstelle, in der Reihenfolge Spieler, KI-Häuser. Kennung aus den Plätzen („S1_Werkstatt“ und „S1_Wohnhaus“ → „S1“), sonst
    /// „H1“, „H2“ …; die des Spielers heißt nach seiner Werkstatt („{art} am Weinmarkt“ → „Weinmarkt“). Ein Paar, dessen Plätze fehlen oder
    /// schon vergeben sind, fällt weg. Null, wenn kein Paar bleibt. <paramref name="artVon"/>: die Bausteinart eines Platzes (Kennung).
    /// </summary>
    internal static List<Hofstelle>? AusStartpaaren(Startbelegung s, Func<string?, string?> artVon)
    {
        var paare = new List<(string Werkstatt, string Wohnhaus, string? Name)>();
        string? Werkstatt(IEnumerable<string?> plaetze) => plaetze.FirstOrDefault(p => artVon(p) == Bausteinart.Werkstattplatz);
        if (s.Spieler.Wohnhaus is { } sw && artVon(sw) == Bausteinart.Wohnhausplatz && Werkstatt(new[] { s.Spieler.Werkstatt.Platz }) is { } swk)
        {
            var m = Regex.Match(s.Spieler.Werkstatt.Name, @"^\{art\}\s+(?:am|an der|an den|im|in der|beim|bei der|auf dem|auf der|vor dem|vor der|zum|zur|unter den)\s+(?<lage>.+)$");
            paare.Add((swk, sw, m.Success ? m.Groups["lage"].Value : null));
        }
        foreach (var h in s.Haeuser)
            if (h.Wohnhaus is { } hw && artVon(hw) == Bausteinart.Wohnhausplatz && Werkstatt(h.Betriebe.Select(b => (string?)b.Platz)) is { } hwk)
                paare.Add((hwk, hw, null));

        var liste = new List<Hofstelle>();
        var vergeben = new HashSet<string>(StringComparer.Ordinal); // nur nachgeschlagen
        foreach (var (werkstatt, wohnhaus, name) in paare)
        {
            if (vergeben.Contains(werkstatt) || vergeben.Contains(wohnhaus)) continue;
            vergeben.Add(werkstatt);
            vergeben.Add(wohnhaus);
            var a = Regex.Match(werkstatt, "^(?<k>.+)_Werkstatt$");
            var kennung = a.Success && wohnhaus == a.Groups["k"].Value + "_Wohnhaus" ? a.Groups["k"].Value : null;
            if (kennung is null || liste.Any(h => h.Kennung == kennung))
            {
                var n = 1;
                while (liste.Any(h => h.Kennung == $"H{n}") || paare.Any(p => p.Werkstatt == $"H{n}_Werkstatt")) n++;
                kennung = $"H{n}";
            }
            liste.Add(new Hofstelle { Kennung = kennung, Name = name, Werkstatt = werkstatt, Wohnhaus = wohnhaus, Start = true });
        }
        return liste.Count > 0 ? liste : null;
    }
}
