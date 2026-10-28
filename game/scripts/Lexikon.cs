using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>Ein Eintrag des Lexikons (<c>game/daten/lexikon.json</c>).</summary>
/// <param name="Kennung">Kurzer, eindeutiger Name in Kleinbuchstaben (Ziel der Verweise, z. B. <c>aktionspunkte</c>).</param>
/// <param name="Titel">Überschrift des Eintrags.</param>
/// <param name="Gruppe">Gruppe in der Liste links (Spiel, Wirtschaft, Berufe, Waren, Stadt, Familie, Rat, Gauner …).</param>
/// <param name="Text">Der Text: Verweise als <c>[[kennung]]</c> oder <c>[[kennung|Anzeigetext]]</c>, Live-Zahlen als <c>{wert:name}</c>.</param>
/// <param name="Auch">Weitere Wortformen, die in Tooltips automatisch auf den Eintrag verweisen (z. B. Mehrzahl); sparsam, nur wo die Beugungsendungen nicht reichen.</param>
public sealed record Begriff(string Kennung, string Titel, string Gruppe, string Text, string[]? Auch = null);

/// <summary>
/// Das <b>Lexikon</b> im Spiel (Vorbild: das Lexikon in Crusader Kings 3, nur die Mechanik): Begriffe mit Verweisen untereinander und Live-Zahlen.
/// Die Einträge stehen in <c>game/daten/lexikon.json</c>. Im Text verweist <c>[[kennung]]</c> (oder <c>[[kennung|Anzeigetext]]</c>) auf einen anderen
/// Eintrag, <c>{wert:name}</c> setzt eine Zahl ein, die aus den Wirtschaftsdaten bzw. dem Stand der Stadt kommt (Namen siehe <see cref="Werte"/>) –
/// so veraltet der Text nicht, wenn die Zahlen geändert werden. Das Lexikon liest den Kern nur.
///
/// Andockstellen für andere Teile der Oberfläche (Tooltips): <see cref="Finde"/> liefert Titel und Klartext, <see cref="Oeffnen"/> öffnet die Tafel.
/// </summary>
public static class Lexikon
{
    public const string Pfad = "res://daten/lexikon.json";

    private static readonly Regex Verweis = new(@"\[\[([a-z0-9_\-]+)(?:\|([^\]]*))?\]\]", RegexOptions.Compiled);
    private static readonly Regex Platzhalter = new(@"\{wert:([a-z0-9_]+)\}", RegexOptions.Compiled);

    private static List<Begriff>? _alle;
    private static Dictionary<string, Begriff> _nachKennung = new();
    private static Wirtschaftsdaten? _daten;
    private static Stadtwirtschaft? _welt;

    /// <summary>Die Tafel setzt das beim Aufbau: öffnet das Lexikon beim Begriff mit dieser Kennung (leer: Startseite).</summary>
    public static Action<string>? AnzeigeOeffnen { get; set; }

    /// <summary>Öffnet das Lexikon bei diesem Begriff (aus Tooltips: Klick auf „Mehr im Lexikon“). Tut nichts, solange keine Tafel da ist.</summary>
    public static void Oeffnen(string kennung) => AnzeigeOeffnen?.Invoke(kennung);

    /// <summary>Die laufende Partie: Die Live-Zahlen kommen aus ihr (null: aus der Datei <c>wirtschaft.json</c>).</summary>
    public static void Verbinden(Stadtwirtschaft? welt)
    {
        _welt = welt;
        if (welt is not null) _daten = welt.Daten;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Laden und Suchen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Alle Begriffe in der Reihenfolge der Datei (leer, wenn die Datei fehlt – der Grund steht dann in <see cref="Fehler"/>).</summary>
    public static IReadOnlyList<Begriff> Alle
    {
        get
        {
            Laden();
            return _alle!;
        }
    }

    /// <summary>Warum das Lexikon nicht geladen werden konnte (null: alles gut).</summary>
    public static string? Fehler { get; private set; }

    /// <summary>Die Gruppen in der Reihenfolge ihres ersten Auftretens in der Datei.</summary>
    public static IReadOnlyList<string> Gruppen => Alle.Select(b => b.Gruppe).Where(g => g != "").Distinct().ToList();

    private static void Laden()
    {
        if (_alle is not null) return;
        _alle = new List<Begriff>();
        try
        {
            var json = Godot.FileAccess.GetFileAsString(Pfad);
            if (string.IsNullOrEmpty(json))
            {
                Fehler = $"{Pfad} fehlt.";
                return;
            }
            _alle = Lesen(json);
            _nachKennung = _alle.ToDictionary(b => b.Kennung);
        }
        catch (Exception fehler) when (fehler is JsonException or ArgumentException)
        {
            Fehler = $"{Pfad}: {fehler.Message}";
        }
    }

    /// <summary>Liest den Inhalt der Datei (auch für die Prüfung).</summary>
    public static List<Begriff> Lesen(string json)
    {
        var optionen = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<Begriff>>(json, optionen) ?? new List<Begriff>();
    }

    /// <summary>Der Begriff mit dieser Kennung (Groß- und Kleinschreibung egal), sonst null.</summary>
    public static Begriff? FindeBegriff(string kennung)
    {
        Laden();
        return _nachKennung.GetValueOrDefault(kennung.Trim().ToLowerInvariant());
    }

    /// <summary>
    /// Für Tooltips: Titel und Text des Begriffs als Klartext (Zahlen eingesetzt, Verweise nur als Wörter), oder null, wenn es ihn nicht gibt.
    /// Ein Tooltip nimmt diese Methode als <c>BegriffSuchen</c>.
    /// </summary>
    public static (string Titel, string Text)? Finde(string kennung)
        => FindeBegriff(kennung) is { } b ? (b.Titel, Aufloesen(b.Text, mitVerweisen: false)) : null;

    /// <summary>
    /// Für Tooltips: wie <see cref="Finde"/>, aber die Verweise bleiben als <c>[[kennung|Anzeigetext]]</c> stehen, damit der Tooltip sie
    /// unterstreicht und weitere Tooltips öffnet. Verweise auf unbekannte Kennungen werden zu bloßem Text.
    /// </summary>
    public static (string Titel, string Text)? FindeMitVerweisen(string kennung)
    {
        if (FindeBegriff(kennung) is not { } b) return null;
        var text = Platzhalter.Replace(b.Text, m => Wert(m.Groups[1].Value) ?? "?");
        text = Verweis.Replace(text, m =>
        {
            var ziel = FindeBegriff(m.Groups[1].Value);
            var anzeige = m.Groups[2].Success ? m.Groups[2].Value : (ziel?.Titel ?? m.Groups[1].Value);
            return ziel is null ? anzeige : $"[[{ziel.Kennung}|{anzeige}]]";
        });
        return (b.Titel, text);
    }

    private static readonly Regex Vorhandene = new(@"\[\[[^\]]*\]\]", RegexOptions.Compiled);
    private static List<(Regex Muster, Begriff Begriff)>? _muster;

    /// <summary>
    /// Für Tooltips (Haken <c>Tooltips.VerweiseErgaenzen</c>): macht aus dem ersten Vorkommen jedes Lexikon-Titels einen Verweis <c>[[kennung|Wort]]</c>.
    /// Ganzes Wort, Groß- und Kleinschreibung wie im Titel; dazu die Endungen -s, -n, -en, -e, -es und die Wortformen aus dem Feld <c>auch</c>.
    /// Längere Titel zuerst, nie innerhalb eines vorhandenen <c>[[…]]</c>, höchstens <paramref name="hoechstens"/> je Text, nie der Begriff <paramref name="eigene"/> selbst.
    /// </summary>
    public static string VerweiseEinsetzen(string text, string? eigene = null, int hoechstens = 6)
    {
        Laden();
        if (_alle!.Count == 0 || string.IsNullOrEmpty(text)) return text;
        _muster ??= _alle
            .Where(b => b.Titel.Length >= 3)
            .OrderByDescending(b => b.Titel.Length)
            .Select(b =>
            {
                var formen = new[] { b.Titel }.Concat(b.Auch ?? Array.Empty<string>()).Select(Regex.Escape);
                return (new Regex($@"(?<![\p{{L}}\p{{N}}\[|])(?:{string.Join("|", formen)})(?:en|es|e|n|s)?(?![\p{{L}}\p{{N}}\]|])", RegexOptions.Compiled), b);
            })
            .ToList();
        var belegt = Vorhandene.Matches(text).Select(m => (m.Index, Ende: m.Index + m.Length)).ToList();
        var neue = new List<(int Anfang, int Ende, string Ersatz)>();
        foreach (var (muster, begriff) in _muster)
        {
            if (neue.Count >= hoechstens) break;
            if (string.Equals(begriff.Kennung, eigene, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (Match m in muster.Matches(text))
            {
                var ende = m.Index + m.Length;
                if (belegt.Any(b => m.Index < b.Ende && ende > b.Index)) continue;
                belegt.Add((m.Index, ende));
                neue.Add((m.Index, ende, $"[[{begriff.Kennung}|{m.Value}]]"));
                break; // nur das erste Vorkommen
            }
        }
        var sb = new StringBuilder();
        var pos = 0;
        foreach (var (anfang, ende, ersatz) in neue.OrderBy(n => n.Anfang))
        {
            sb.Append(text, pos, anfang - pos).Append(ersatz);
            pos = ende;
        }
        return sb.Append(text, pos, text.Length - pos).ToString();
    }

    /// <summary>
    /// Sucht in Titel und Text (ohne Rücksicht auf Groß- und Kleinschreibung, alle Wörter müssen vorkommen). Treffer im Titel stehen vorn, dann
    /// die, deren Titel mit dem Suchwort beginnt, zuerst.
    /// </summary>
    public static List<Begriff> Suche(string suchtext)
    {
        var woerter = suchtext.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(w => w.ToLowerInvariant()).ToList();
        if (woerter.Count == 0) return Alle.ToList();
        var treffer = new List<(Begriff Begriff, int Rang)>();
        foreach (var b in Alle)
        {
            var titel = b.Titel.ToLowerInvariant();
            var text = Aufloesen(b.Text, mitVerweisen: false).ToLowerInvariant();
            if (!woerter.All(w => titel.Contains(w) || text.Contains(w))) continue;
            var rang = titel.StartsWith(woerter[0]) ? 0 : woerter.All(titel.Contains) ? 1 : 2;
            treffer.Add((b, rang));
        }
        return treffer.OrderBy(t => t.Rang).Select(t => t.Begriff).ToList();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Text aufbereiten
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Setzt die Live-Zahlen ein und macht aus den Verweisen entweder anklickbare BBCode-Verweise (<paramref name="mitVerweisen"/>, für ein RichTextLabel
    /// mit <c>meta_clicked</c>) oder bloßen Text. Ein Verweis auf eine unbekannte Kennung bleibt als roter Text stehen, damit er auffällt.
    /// </summary>
    public static string Aufloesen(string text, bool mitVerweisen)
    {
        text = Platzhalter.Replace(text, m => Wert(m.Groups[1].Value) ?? "?");
        return Verweis.Replace(text, m =>
        {
            var kennung = m.Groups[1].Value;
            var anzeige = m.Groups[2].Success ? m.Groups[2].Value : (FindeBegriff(kennung)?.Titel ?? kennung);
            if (!mitVerweisen) return anzeige;
            return FindeBegriff(kennung) is null
                ? $"[color=#d9695f]{anzeige}[/color]"
                : $"[url={kennung}][color=#e8c872]{anzeige}[/color][/url]";
        });
    }

    private static Wirtschaftsdaten? Daten()
    {
        if (_daten is not null) return _daten;
        try
        {
            var json = Godot.FileAccess.GetFileAsString(Hauptszene.WirtschaftsdatenPfad);
            if (!string.IsNullOrEmpty(json)) _daten = Wirtschaftsdaten.AusJson(json);
        }
        catch (WirtschaftsdatenFehler)
        {
            // ohne Daten bleiben die Zahlen „?“
        }
        return _daten;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Live-Zahlen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Der Wert zu <c>{wert:name}</c>, oder null, wenn der Name unbekannt ist oder die Daten ihn nicht hergeben.</summary>
    public static string? Wert(string name)
    {
        if (!Werte.TryGetValue(name, out var holen) || Daten() is not { } d) return null;
        try
        {
            return holen(d, _welt);
        }
        catch (Exception fehler) when (fehler is ArgumentOutOfRangeException or KeyNotFoundException or NullReferenceException)
        {
            return null; // der Abschnitt fehlt in den Daten
        }
    }

    private static string Ganz(double x) => Zahlenformat.Ganz(x);
    private static string Prozent(double x) => $"{Zahlenformat.Kommazahl(x * 100).Replace(",0", "")} %";

    /// <summary>Alle bekannten Platzhalter. Neue Namen hier eintragen; die Prüfung (<see cref="Pruefen"/>) meldet unbekannte.</summary>
    public static readonly IReadOnlyDictionary<string, Func<Wirtschaftsdaten, Stadtwirtschaft?, string?>> Werte = WerteBauen();

    private static Dictionary<string, Func<Wirtschaftsdaten, Stadtwirtschaft?, string?>> WerteBauen()
    {
        var w = new Dictionary<string, Func<Wirtschaftsdaten, Stadtwirtschaft?, string?>>
        {
            ["waehrung"] = (d, _) => d.Allgemein.Waehrung,
            ["startvermoegen"] = (d, _) => Ganz(d.Allgemein.Startvermoegen),
            ["buerger_schwelle"] = (d, _) => Ganz(d.Allgemein.SchwelleBuerger),
            ["steuersatz"] = (d, s) => $"{(s is not null ? s.Kasse.SteuersatzProzent : (int)Math.Round(d.Allgemein.Umsatzsteuer * 100))} %",
            ["steuer_min"] = (d, _) => d.Politik.Kasse is { } k ? $"{k.SteuerMin} %" : null,
            ["steuer_max"] = (d, _) => d.Politik.Kasse is { } k ? $"{k.SteuerMax} %" : null,
            ["ap_max"] = (d, _) => d.Aktionspunkte?.Hoechstens.ToString(),
            ["bankrott_jahre"] = (d, _) => d.Schulden.JahreBisBankrott.ToString(),
            ["haendlerspanne"] = (d, _) => d.Handel is { } h ? Prozent(h.Aufschlag) : null,
            ["markt_von"] = (d, _) => d.Handel?.MarktVon.ToString(),
            ["markt_bis"] = (d, _) => d.Handel?.MarktBis.ToString(),
            ["verkauf_anteil"] = (d, _) => Prozent(d.Verkauf.Anteil),
            ["verkauf_abschlag"] = (d, _) => Prozent(d.Verkauf.WarenAbschlag),
            ["karren_verkauf_anteil"] = (d, _) => Prozent(d.Logistik.KarrenVerkaufAnteil),
            ["einwohner_grund"] = (d, _) => d.Stadt is { } st ? Ganz(st.EinwohnerGrund) : null,
            ["einwohner_haus"] = (d, _) => d.Stadt is { } st ? Ganz(st.EinwohnerJeHaus) : null,
            ["tag_beginn"] = (_, _) => (Spielzeit.TagesbeginnMinute / 60).ToString(),
            ["tag_ende"] = (_, _) => (Spielzeit.TagesendeMinute / 60).ToString(),
            ["schiff_preis"] = (d, _) => Ganz(d.Schiffshandel!.Schiffe["Flussschiff"].Preis),
            ["schiff_laderaeume"] = (d, _) => d.Schiffshandel!.Schiffe["Flussschiff"].Sorten.ToString(),
            ["schiff_je_laderaum"] = (d, _) => d.Schiffshandel!.Schiffe["Flussschiff"].KapazitaetJeSorte.ToString(),
            ["leerstand_kauf"] = (d, _) => Prozent(d.Bauen!.Leerstand.Kauf),
            ["leerstand_sanierung"] = (d, _) => Prozent(d.Bauen!.Leerstand.Sanierung),
            ["leerstand_abriss"] = (d, _) => Prozent(d.Bauen!.Leerstand.Abriss),
            ["gerichtsgebuehr"] = (d, _) => d.Justiz is { } j ? Ganz(j.Gerichtsgebuehr) : null,
            ["busse_faktor"] = (d, _) => d.Justiz is { } j ? Zahlenformat.Kommazahl(j.BusseFaktor).Replace(",0", "") : null,
            ["zuege_liste"] = (d, _) => d.Charakterzuege is { IstAktiv: true } z
                ? string.Join("\n", z.Alle.Select(x => $"• {x.Name} ({x.Artname}){(x.Wirkungstext == "" ? "" : ": " + x.Wirkungstext)}"))
                : null,
            ["meister_umbau_stunden"] = (d, _) => d.Meistergrade is { } m ? (m.UmbauMinuten / 60.0).ToString("0.#") : null,
        };
        foreach (var art in new[] { "Handkarren", "Ochsenkarren", "Kriegskarren" })
        {
            var schluessel = art.ToLowerInvariant();
            var name = art;
            w[$"karren_preis_{schluessel}"] = (d, _) => Ganz(d.Logistik.Karren[name].Preis);
            w[$"karren_last_{schluessel}"] = (d, _) => d.Logistik.Karren[name].Gesamtkapazitaet.ToString();
        }
        foreach (var kraft in new[] { "Lehrling", "Geselle", "Vorarbeiter", "Knecht", "Altknecht", "Großknecht", "Ork", "Leibeigener" })
        {
            var name = kraft;
            w[$"lohn_{kraft.ToLowerInvariant().Replace("ß", "ss")}"] = (d, _) => Ganz(d.Arbeitskraefte[name].Kosten);
        }
        for (var i = 1; i <= 5; i++) // Wohnhausstufen 1 (Fachwerkhaus) bis 5 (Stadthof)
        {
            var n = i;
            w[$"haus_name_{n}"] = (d, _) => d.Wohnen!.Stufen[n - 1].Name;
            w[$"haus_ausbau_{n}"] = (d, _) => Ganz(d.Wohnen!.Stufen[n - 1].Ausbau);
            w[$"haus_unterhalt_{n}"] = (d, _) => Ganz(d.Wohnen!.Stufen[n - 1].Unterhalt);
            w[$"haus_ansehen_{n}"] = (d, _) => Ganz(d.Wohnen!.Stufen[n - 1].Ansehen);
        }
        for (var i = 1; i <= 5; i++) // Stände 2 (Bürger) bis 6 (Reichsgraf)
        {
            var n = i;
            w[$"stand_name_{n + 1}"] = (d, _) => d.Staende[n].Name;
            w[$"stand_schwelle_{n + 1}"] = (d, _) => Ganz(d.Staende[n].Schwelle);
            w[$"stand_gebuehr_{n + 1}"] = (d, _) => Ganz(d.Staende[n].Gebuehr);
        }
        for (var grad = 2; grad <= 6; grad++)
        {
            var g = grad;
            w[$"meister_gebuehr_{g}"] = (d, _) => Ganz(d.Meistergrade!.Pruefungen[g - 2].Gebuehr);
            w[$"meister_ap_{g}"] = (d, _) => d.Meistergrade!.Pruefungen[g - 2].Ap.ToString();
        }
        for (var stufe = 2; stufe <= 3; stufe++)
        {
            var s = stufe;
            w[$"meister_stufe_{s}_grad"] = (d, _) => d.Meistergrade!.GradFuerStufe(s).ToString();
        }
        return w;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Prüfung
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Prüft die Datei: Kennungen eindeutig und in Kleinbuchstaben, jeder Verweis zeigt auf eine vorhandene Kennung, jeder Platzhalter ist bekannt
    /// und liefert mit den Wirtschaftsdaten einen Wert, kein Eintrag ist leer. Gibt die Fehler zurück (leer: alles in Ordnung).
    /// </summary>
    public static List<string> Pruefen(IReadOnlyList<Begriff> begriffe, Wirtschaftsdaten? daten)
    {
        var fehler = new List<string>();
        var kennungen = new HashSet<string>();
        foreach (var b in begriffe)
        {
            if (b.Kennung == "" || b.Kennung != b.Kennung.ToLowerInvariant() || !Regex.IsMatch(b.Kennung, "^[a-z0-9_-]+$"))
                fehler.Add($"„{b.Kennung}“: Die Kennung muss aus Kleinbuchstaben, Ziffern, _ und - bestehen.");
            if (!kennungen.Add(b.Kennung)) fehler.Add($"„{b.Kennung}“: Die Kennung gibt es doppelt.");
            if (b.Titel.Trim() == "" || b.Text.Trim() == "") fehler.Add($"„{b.Kennung}“: Titel und Text dürfen nicht leer sein.");
            if (b.Gruppe.Trim() == "") fehler.Add($"„{b.Kennung}“: Die Gruppe fehlt.");
        }
        foreach (var b in begriffe)
        {
            foreach (Match m in Verweis.Matches(b.Text))
                if (!kennungen.Contains(m.Groups[1].Value)) fehler.Add($"„{b.Kennung}“: Der Verweis auf „{m.Groups[1].Value}“ führt ins Leere.");
            foreach (Match m in Platzhalter.Matches(b.Text))
            {
                var name = m.Groups[1].Value;
                if (!Werte.TryGetValue(name, out var holen)) fehler.Add($"„{b.Kennung}“: Der Platzhalter „{name}“ ist unbekannt.");
                else if (daten is not null)
                {
                    string? wert;
                    try { wert = holen(daten, null); }
                    catch (Exception e) when (e is ArgumentOutOfRangeException or KeyNotFoundException or NullReferenceException) { wert = null; }
                    if (wert is null) fehler.Add($"„{b.Kennung}“: Der Platzhalter „{name}“ hat in den Wirtschaftsdaten keinen Wert.");
                }
            }
            // Übrig gebliebene eckige oder geschweifte Klammern sind meist Tippfehler ([[kennung] oder {wert:...}).
            var rest = Platzhalter.Replace(Verweis.Replace(b.Text, ""), "");
            if (rest.Contains("[[") || rest.Contains("]]") || rest.Contains("{wert")) fehler.Add($"„{b.Kennung}“: Verweis oder Platzhalter ist falsch geschrieben.");
            if (rest.Contains('[') || rest.Contains(']')) fehler.Add($"„{b.Kennung}“: Eckige Klammern im Text sind für Verweise reserviert.");
        }
        return fehler;
    }

    /// <summary>
    /// Startoption <c>--lexikon-pruefen=1</c>: prüft die Datei gegen <c>wirtschaft.json</c>, schreibt das Ergebnis in die Ausgabe und beendet das Spiel
    /// (Rückgabewert des Prozesses 0 oder 1). Gibt true zurück, wenn alles in Ordnung ist.
    /// </summary>
    public static bool PruefenUndMelden()
    {
        Laden();
        var fehler = new List<string>();
        if (Fehler is not null) fehler.Add(Fehler);
        Wirtschaftsdaten? daten = null;
        try
        {
            daten = Wirtschaftsdaten.AusJson(Godot.FileAccess.GetFileAsString(Hauptszene.WirtschaftsdatenPfad));
        }
        catch (WirtschaftsdatenFehler e)
        {
            fehler.Add(e.Message);
        }
        fehler.AddRange(Pruefen(Alle, daten));
        var sb = new StringBuilder();
        sb.Append($"Lexikon: {Alle.Count} Begriffe in {Gruppen.Count} Gruppen, {Werte.Count} Platzhalter bekannt – ");
        sb.Append(fehler.Count == 0 ? "alles in Ordnung." : $"{fehler.Count} Fehler:");
        Godot.GD.Print(sb.ToString());
        foreach (var f in fehler) Godot.GD.Print("  " + f);
        return fehler.Count == 0;
    }
}
