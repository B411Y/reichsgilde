using System.Text.Json;
using System.Text.RegularExpressions;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>Wie wichtig eine Meldung ist (Benachrichtigungen, Stufe 1). <c>Aus</c>: gar nicht zeigen; <c>Verlauf</c>: nur zum Nachlesen; <c>Siegel</c>: Siegel in der Ecke unten rechts; <c>Brief</c>: Brief-Fenster.</summary>
public enum Meldestufe { Aus, Verlauf, Siegel, Brief }

/// <summary>Wen eine Meldung betrifft: das eigene Haus, niemand Bestimmtes (die Stadt) oder ein anderes Haus.</summary>
public enum Betroffenheit { Eigen, Stadt, Fremd }

/// <summary>Ein Thema bündelt Meldungsarten zu einem Stapel in der Ecke und gibt dem Siegel seine Farbe (<c>game/daten/meldungen.json</c>).</summary>
public sealed record Meldethema(string Id, string Name, string Farbe);

/// <summary>
/// Eine Art von Meldung (Tod, Diebstahl, Stillstand …) mit ihren Textmustern, ihrer Stufe je nach Betroffenheit und dem Absender des Briefes.
/// Das erste passende Muster der Reihenfolge in <c>meldungen.json</c> gewinnt.
/// </summary>
public sealed class Meldeart
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Thema { get; init; } = "stadt";
    public Meldestufe Eigen { get; init; }
    public Meldestufe Stadt { get; init; }
    public Meldestufe Fremd { get; init; }
    public string Titel { get; init; } = "";
    public string Absender { get; init; } = "";
    public string Erklaerung { get; init; } = "";
    /// <summary>Wohin der Knopf im Brief führt: <c>familie</c>, <c>rat</c>, <c>bericht</c>, <c>betrieb</c> oder leer.</summary>
    public string Ziel { get; init; } = "";
    /// <summary><c>jahresbericht</c> (zum Jahreswechsel zu einem Brief), <c>nacht</c> (nachts, ein Brief am Morgen), <c>stillstand</c> (ein Siegel je Betrieb) oder leer.</summary>
    public string Sammeln { get; init; } = "";
    internal List<Muster> Muster { get; init; } = new();
    internal List<(Muster Muster, Meldestufe Hoechstens)> Ausnahmen { get; init; } = new();
    /// <summary>Texte, die trotz <see cref="Sammeln"/> einzeln bleiben (z. B. eine Anklage gegen das eigene Haus).</summary>
    public IReadOnlyList<string> Einzeln { get; init; } = Array.Empty<string>();

    /// <summary>Kann der Spieler diese Art in den Einstellungen wählen? Rivalen und Sonstiges nur Verlauf oder aus.</summary>
    public bool NurVerlauf => Id is "KI-Zug" or "Sonstiges" or "Bau" or "Kirche" or "Besitz" or "Entwickler";

    internal bool Passt(string text) => Muster.Any(m => m.Passt(text));

    /// <summary>Die vorgegebene Stufe für diese Betroffenheit.</summary>
    public Meldestufe Standard(Betroffenheit bezug) => bezug switch { Betroffenheit.Eigen => Eigen, Betroffenheit.Stadt => Stadt, _ => Fremd };
}

/// <summary>Ein Textmuster: alle genannten Bedingungen müssen zugleich zutreffen.</summary>
internal sealed class Muster
{
    public string? Beginnt, Endet;
    public string[] Enthaelt = Array.Empty<string>();
    public Regex? Regex;

    public bool Passt(string text) =>
        (Beginnt is null || text.StartsWith(Beginnt, StringComparison.Ordinal))
        && (Endet is null || text.EndsWith(Endet, StringComparison.Ordinal))
        && Enthaelt.All(e => text.Contains(e, StringComparison.Ordinal))
        && (Regex is null || Regex.IsMatch(text));
}

/// <summary>Das Ergebnis der Einstufung einer Meldung.</summary>
/// <param name="Art">Die Art (nie null; „Sonstiges“, wenn nichts passt).</param>
/// <param name="Bezug">Eigen, Stadt oder Fremd.</param>
/// <param name="Text">Der Text ohne Warnzeichen „⚠ “.</param>
/// <param name="Warnung">Mit „✗“ oder „⚠“ begonnen.</param>
/// <param name="Verwerfen">Eine fremde Nachricht der Unterwelt oder eines Spitzels, die nicht für den Spieler bestimmt ist – gar nicht zeigen.</param>
/// <param name="RivalenZug">Ein Zug der KI-Häuser.</param>
public sealed record Einstufung(Meldeart Art, Betroffenheit Bezug, string Text, bool Warnung, bool Verwerfen, bool RivalenZug);

/// <summary>
/// Ordnet die Textmeldungen des Kerns (<c>Stadtwirtschaft.Meldung</c>) Arten, Themen und Stufen zu (Benachrichtigungen, Stufe 1). Die Zuordnung
/// steht in <c>game/daten/meldungen.json</c>; Stufe 2 (strukturierte Ereignisse aus dem Kern) löst sie ab. Kennt Godot nicht, damit der Test sie mit
/// echten Meldungen einer Partie prüfen kann (<c>MeldungsordnungTests</c>).
///
/// <b>Wen betrifft es?</b> Zug eines Rivalen: Fremd. Familienereignis und Nachricht der Unterwelt tragen ihr Haus (<c>Familien.DiesesJahr</c>, <c>Gauner.DiesesJahr</c>),
/// ein Spitzelbericht gehört dem Auftraggeber; sonst nennt der Text das Haus (Inhaber, Nachname, Betrieb, Schiff). Fremde Geheimnisse (Unterwelt, Spitzel,
/// die an ein anderes Haus gehen) werden verworfen: Der Kern meldet sie an alle, der Spieler soll sie nicht lesen.
/// </summary>
public sealed class Meldungsordnung
{
    private readonly List<Meldeart> _arten;
    private readonly Meldeart _sonstiges, _kiZug, _fehler;

    public IReadOnlyList<Meldethema> Themen { get; }
    public IReadOnlyList<Meldeart> Arten => _arten;

    private Meldungsordnung(List<Meldethema> themen, List<Meldeart> arten)
    {
        Themen = themen;
        _arten = arten;
        _sonstiges = arten.First(a => a.Id == "Sonstiges");
        _kiZug = arten.First(a => a.Id == "KI-Zug");
        _fehler = arten.First(a => a.Id == "Fehler");
    }

    public Meldeart Art(string id) => _arten.First(a => a.Id == id);

    public Meldethema Thema(string id) => Themen.FirstOrDefault(t => t.Id == id) ?? Themen[0];

    private static Meldestufe Stufe(JsonElement e, string name) => e.GetProperty(name).GetString() switch
    {
        "aus" => Meldestufe.Aus,
        "verlauf" => Meldestufe.Verlauf,
        "siegel" => Meldestufe.Siegel,
        "brief" => Meldestufe.Brief,
        var andere => throw new FormatException($"meldungen.json: unbekannte Stufe „{andere}“ bei {name}."),
    };

    private static Muster MusterLesen(JsonElement e)
    {
        var m = new Muster();
        if (e.TryGetProperty("beginnt", out var b)) m.Beginnt = b.GetString();
        if (e.TryGetProperty("endet", out var en)) m.Endet = en.GetString();
        if (e.TryGetProperty("enthaelt", out var h))
            m.Enthaelt = h.ValueKind == JsonValueKind.Array ? h.EnumerateArray().Select(x => x.GetString()!).ToArray() : new[] { h.GetString()! };
        if (e.TryGetProperty("regex", out var r)) m.Regex = new Regex(r.GetString()!, RegexOptions.CultureInvariant);
        return m;
    }

    /// <summary>Liest <c>meldungen.json</c>.</summary>
    public static Meldungsordnung AusJson(string json)
    {
        using var dok = JsonDocument.Parse(json);
        var wurzel = dok.RootElement;
        var themen = wurzel.GetProperty("themen").EnumerateArray()
            .Select(t => new Meldethema(t.GetProperty("id").GetString()!, t.GetProperty("name").GetString()!, t.GetProperty("farbe").GetString()!)).ToList();
        var arten = new List<Meldeart>();
        foreach (var a in wurzel.GetProperty("arten").EnumerateArray())
        {
            string Text(string name) => a.TryGetProperty(name, out var p) ? p.GetString() ?? "" : "";
            arten.Add(new Meldeart
            {
                Id = Text("id"), Name = Text("name"), Thema = Text("thema"), Titel = Text("titel"), Absender = Text("absender"), Erklaerung = Text("erklaerung"),
                Ziel = Text("ziel"), Sammeln = Text("sammeln"),
                Eigen = Stufe(a, "eigen"), Stadt = Stufe(a, "stadt"), Fremd = Stufe(a, "fremd"),
                Muster = a.GetProperty("muster").EnumerateArray().Select(MusterLesen).ToList(),
                Ausnahmen = a.TryGetProperty("ausnahmen", out var ausn)
                    ? ausn.EnumerateArray().Select(x => (MusterLesen(x), Stufe(x, "stufe"))).ToList() : new(),
                Einzeln = a.TryGetProperty("einzeln", out var einz) ? einz.EnumerateArray().Select(x => x.GetString()!).ToList() : Array.Empty<string>(),
            });
        }
        return new Meldungsordnung(themen, arten);
    }

    /// <summary>Die Art nach dem Text allein (ohne Kenntnis der Partie; Züge der Rivalen erkennt erst <see cref="Einstufen"/>).</summary>
    public Meldeart Einordnen(string text)
    {
        text = Ohne(text);
        foreach (var art in _arten)
            if (art.Passt(text)) return art;
        return _sonstiges;
    }

    /// <summary>Der Text ohne das Warnzeichen „⚠ “ (Blitz und Piraten stellen es der Meldung des Kerns voran).</summary>
    public static string Ohne(string text) => text.StartsWith("⚠ ", StringComparison.Ordinal) ? text[2..] : text;

    /// <summary>
    /// Die Stufe einer Meldung. <paramref name="gewaehlt"/> ist die Wahl des Spielers für die Art (null = Vorgabe). Fremdes bleibt höchstens im Verlauf;
    /// eine Ausnahme der Art (abgewehrter Überfall) senkt die Stufe auf höchstens Siegel.
    /// </summary>
    public static Meldestufe Stufe(Meldeart art, Betroffenheit bezug, string text, Meldestufe? gewaehlt)
    {
        var stufe = gewaehlt ?? art.Standard(bezug);
        if (gewaehlt is not null && bezug == Betroffenheit.Fremd && stufe > Meldestufe.Verlauf) stufe = Meldestufe.Verlauf;
        foreach (var (muster, hoechstens) in art.Ausnahmen)
            if (stufe > hoechstens && muster.Passt(Ohne(text))) stufe = hoechstens;
        return stufe;
    }

    /// <summary>Ordnet eine Meldung in der laufenden Partie ein (Art und Betroffenheit).</summary>
    public Einstufung Einstufen(Stadtwirtschaft w, Konto spieler, string text)
    {
        var warnung = text.StartsWith('✗') || text.StartsWith('⚠');
        var ohne = Ohne(text);
        // Züge der KI („Haus Brendel: …“): der Kern legt den Satz vor der Meldung in Entscheidungen[0] ab
        foreach (var r in w.Rivalen)
            if (r.Entscheidungen.Count > 0 && ReferenceEquals(r.Entscheidungen[0], text))
                return new Einstufung(_kiZug, Betroffenheit.Fremd, ohne, false, false, true);
        var art = Einordnen(ohne);
        // Familienereignis oder Nachricht der Unterwelt an ein bestimmtes Haus: der Kern kennt das Haus
        if (w.Familien.DiesesJahr is { Count: > 0 } fam && ReferenceEquals(fam[^1].Text, text))
            return new Einstufung(art, fam[^1].Konto == spieler ? Betroffenheit.Eigen : Betroffenheit.Fremd, ohne, warnung, false, false);
        if (w.Gauner.DiesesJahr is { Count: > 0 } nacht && ReferenceEquals(nacht[^1].Text, text) && nacht[^1].Fuer is { } fuer)
            return new Einstufung(art, fuer == spieler ? Betroffenheit.Eigen : Betroffenheit.Fremd, ohne, warnung, fuer != spieler, false);
        // Spitzelbericht und Gerüchte gehören dem, der lauschen lässt – nicht dem Belauschten
        if (art.Id == "Spitzel" && Auftraggeber(ohne) is { } wer)
        {
            var eigen = wer == spieler.Inhaber;
            return new Einstufung(art, eigen ? Betroffenheit.Eigen : Betroffenheit.Fremd, ohne, warnung, !eigen, false);
        }
        if (art == _fehler) return new Einstufung(art, Betroffenheit.Eigen, ohne, true, false, false);
        if (Nennt(w, spieler, ohne) || Nennt(w, spieler, text)) return new Einstufung(art, Betroffenheit.Eigen, ohne, warnung, false, false);
        foreach (var k in w.Konten)
            if (k != spieler && Nennt(w, k, ohne)) return new Einstufung(art, Betroffenheit.Fremd, ohne, warnung, false, false);
        return new Einstufung(art, Betroffenheit.Stadt, ohne, warnung, false, false);
    }

    /// <summary>„Der Spitzel von X berichtet …“ bzw. „In der dunklen Ecke von … hört X Gerüchte …“ → X.</summary>
    public static string? Auftraggeber(string m)
    {
        const string spitzel = "Der Spitzel von ", berichtet = " berichtet ", hoert = " hört ", geruechte = " Gerüchte";
        if (m.StartsWith(spitzel, StringComparison.Ordinal) && m.IndexOf(berichtet, StringComparison.Ordinal) is > 0 and var e)
            return m[spitzel.Length..e];
        if (m.IndexOf(hoert, StringComparison.Ordinal) is > 0 and var h && m.IndexOf(geruechte, h, StringComparison.Ordinal) is > 0 and var g)
            return m[(h + hoert.Length)..g];
        return null;
    }

    private static bool Nennt(Stadtwirtschaft w, Konto k, string m)
    {
        if (m.Contains(k.Inhaber, StringComparison.Ordinal)) return true;
        if (w.Familien.Von(k) is { } f && Regex.IsMatch(m, $@"\b{Regex.Escape(f.Nachname)}\b")) return true;
        foreach (var b in w.Betriebe)
            if (b.Konto == k && m.Contains(b.Name, StringComparison.Ordinal)) return true;
        foreach (var s in w.Schiffahrt.SchiffeVon(k))
            if (m.Contains(s.Name, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>Der Betrieb, von dem ein Stillstand spricht („Tischlerei Eichwald: Es fehlt Holz – die Arbeit ruht.“ → „Tischlerei Eichwald“).</summary>
    public static string? Stillstandsbetrieb(string text)
    {
        var doppelpunkt = text.IndexOf(':');
        return doppelpunkt > 0 ? text[..doppelpunkt] : null;
    }

    /// <summary>Ein Stillstand, der zählt: es fehlt eine Zutat oder das Lager ist voll (nicht der gewollte Umbau und nicht die Reparatur nach einem Brand).</summary>
    public static bool IstMangel(string? grund) =>
        grund is not null && (grund.StartsWith("Es fehlt ", StringComparison.Ordinal) || grund.StartsWith("Das Lager ", StringComparison.Ordinal));
}
