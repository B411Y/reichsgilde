using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Wohnen;

// ═════════════════════════════════════════════════════════════════════════════
//  Räume des Wohnhauses (Abschnitt „wohnen.raeume“ in wirtschaft.json, ab Datenversion 87)
//
//  Nutzerentscheidung 14.11.2026, nach der Mechanik aus „Die Gilde 1“ (nur die Mechanik):
//    Stufe 1 (Fachwerkhaus): nur die Wohnstube.
//    Stufe 2 (Bürgerhaus):  ein freier Platz – Hinterzimmer, Festsaal oder Bibliothek (Befehl RaumAnbauen, kostet Gulden, endgültig).
//    Stufe 3 (Steinhaus):   ein zweiter freier Platz, wieder aus diesen dreien, außer dem schon gewählten.
//  Höhere Stufen ohne eigenen Eintrag haben die Räume der letzten eingetragenen Stufe (keine weiteren Plätze).
//  Ausnahme Dieb (berufStattRaum): Wer einen Betrieb dieses Berufs hat, gibt die Befehle des Raums auch ohne ihn – von seinem Betrieb aus.
//  Jeder Raum schaltet vorhandene Befehle, Aktivitäten oder Einrichtungsstücke frei – fehlt er, sind sie gesperrt, für Spieler und KI.
//  Fehlt der Abschnitt, hat jedes Haus jeden Raum (alles wie vorher).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Raum des Wohnhauses, z. B. „Hinterzimmer“.</summary>
public sealed class WohnraumDaten
{
    /// <summary>Kennung (nur Buchstaben und Ziffern ohne Umlaute), z. B. „Hinterzimmer“.</summary>
    public string Kennung { get; init; } = "";

    public string Name { get; init; } = "";

    /// <summary>Was das Anbauen als Raum zur Wahl kostet (Gulden, einmalig; 0 = nichts).</summary>
    public double Preis { get; init; }

    /// <summary>Befehle (Name des Befehls, z. B. „Beauftragen“), die nur mit diesem Raum gehen.</summary>
    public List<string> Befehle { get; init; } = new();

    /// <summary>Aktivitäten (Kennung aus „meinungen.aktivitaeten“, z. B. „Gastmahl“), die nur mit diesem Raum gehen.</summary>
    public List<string> Aktivitaeten { get; init; } = new();

    /// <summary>Was der Raum bewirkt, solange das Haus ihn hat – wie ein Stück Einrichtung (Bibliothek: Ausbildung der Kinder × 1,25).</summary>
    public EinrichtungWirkung Wirkung { get; init; } = new();

    /// <summary>Wer einen Betrieb dieses Berufs hat (z. B. „Dieb“), gibt die <see cref="Befehle"/> des Raums auch ohne ihn (null = niemand).</summary>
    public string? BerufStattRaum { get; init; }
}

/// <summary>Die Räume einer Stufe des Wohnhauses: feste Räume und freie Plätze für Räume zur Wahl.</summary>
public sealed class RaumstufeDaten
{
    /// <summary>Diese Räume hat jedes Wohnhaus der Stufe.</summary>
    public List<string> Feste { get; init; } = new();

    /// <summary>So viele Räume zur Wahl (aus <see cref="RaeumeDaten.Waehlbar"/>) passen dazu.</summary>
    public int Frei { get; init; }
}

public sealed class RaeumeDaten
{
    /// <summary>Alle Räume, in der Reihenfolge der Anzeige.</summary>
    public List<WohnraumDaten> Arten { get; init; } = new();

    /// <summary>Je Stufe des Wohnhauses (0 = Startwohnhaus) die Räume; fehlt eine höhere Stufe, gilt die letzte eingetragene.</summary>
    public List<RaumstufeDaten> Stufen { get; init; } = new();

    /// <summary>Unter diesen Räumen wählt man für einen freien Platz.</summary>
    public List<string> Waehlbar { get; init; } = new();

    public WohnraumDaten? Art(string kennung) => Arten.FirstOrDefault(a => a.Kennung == kennung);

    internal void Pruefen(List<string> f, int anzahlStufen, IEnumerable<EinrichtungDaten> einrichtung, IReadOnlyDictionary<string, WareDaten> waren,
        IEnumerable<string>? aktivitaeten, IEnumerable<string>? berufe)
    {
        const string p = "wohnen.raeume";
        foreach (var doppelt in Arten.GroupBy(a => a.Kennung).Where(g => g.Count() > 1))
            f.Add($"{p}.arten: „{doppelt.Key}“ ist doppelt eingetragen.");
        var befehlsarten = typeof(Befehl).Assembly.GetTypes().Where(t => typeof(Befehl).IsAssignableFrom(t) && !t.IsAbstract).Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        var stuecke = einrichtung.Select(e => e.Kennung).ToHashSet(StringComparer.Ordinal);
        var aktiv = aktivitaeten?.ToHashSet(StringComparer.Ordinal);
        foreach (var a in Arten)
        {
            var wo = $"{p}.arten.{a.Kennung}";
            if (string.IsNullOrWhiteSpace(a.Kennung) || a.Kennung.Any(c => !char.IsAsciiLetterOrDigit(c)))
                f.Add($"{p}.arten: Die Kennung „{a.Kennung}“ darf nur Buchstaben und Ziffern ohne Umlaute haben.");
            if (string.IsNullOrWhiteSpace(a.Name)) f.Add($"{wo} braucht einen Namen.");
            if (a.Preis < 0) f.Add($"{wo}.preis darf nicht negativ sein.");
            if (a.BerufStattRaum is { } beruf && berufe is not null && !berufe.Contains(beruf)) f.Add($"{wo}.berufStattRaum: Einen Beruf „{beruf}“ gibt es nicht.");
            foreach (var b in a.Befehle.Where(b => !befehlsarten.Contains(b))) f.Add($"{wo}.befehle: Einen Befehl „{b}“ gibt es nicht.");
            // Ein Raum und ein Stück Einrichtung mit derselben Kennung gäbe es doppelt (die Bibliothek ist seit Datenversion 87 ein Raum).
            if (stuecke.Contains(a.Kennung)) f.Add($"{wo}: „{a.Kennung}“ ist schon ein Stück unter wohnen.einrichtung – ein Raum oder ein Stück, nicht beides.");
            a.Wirkung.Pruefen(f, $"{wo}.wirkung", waren);
            if (aktiv is not null) // ohne Abschnitt „meinungen“ gibt es keine Aktivitäten – dann sperrt der Raum keine
                foreach (var x in a.Aktivitaeten.Where(x => !aktiv.Contains(x))) f.Add($"{wo}.aktivitaeten: „{x}“ steht nicht unter meinungen.aktivitaeten.");
        }
        // Jede Sache gehört höchstens einem Raum – sonst wäre unklar, welcher sie freischaltet.
        foreach (var d in Arten.SelectMany(a => a.Befehle).GroupBy(x => x).Where(g => g.Count() > 1)) f.Add($"{p}: Der Befehl „{d.Key}“ steht bei mehreren Räumen.");
        foreach (var d in Arten.SelectMany(a => a.Aktivitaeten).GroupBy(x => x).Where(g => g.Count() > 1)) f.Add($"{p}: Die Aktivität „{d.Key}“ steht bei mehreren Räumen.");
        if (Stufen.Count > anzahlStufen) f.Add($"{p}.stufen: Es gibt nur {anzahlStufen} Stufen des Wohnhauses, nicht {Stufen.Count}.");
        for (var i = 0; i < Stufen.Count; i++)
        {
            var s = Stufen[i];
            if (s.Frei < 0) f.Add($"{p}.stufen[{i}].frei darf nicht negativ sein.");
            foreach (var r in s.Feste.Where(r => Art(r) is null)) f.Add($"{p}.stufen[{i}].feste: Einen Raum „{r}“ gibt es nicht.");
            if (s.Frei > 0 && Waehlbar.Count == 0) f.Add($"{p}.stufen[{i}]: Freie Plätze brauchen Räume unter {p}.waehlbar.");
        }
        foreach (var r in Waehlbar.Where(r => Art(r) is null)) f.Add($"{p}.waehlbar: Einen Raum „{r}“ gibt es nicht.");
    }
}

public sealed partial class Wohnungswesen
{
    /// <summary>Gibt es in dieser Stadt Räume im Wohnhaus? Sonst hat jedes Haus jeden Raum.</summary>
    public bool HatRaumregeln => Daten?.Raeume is { Stufen.Count: > 0 };

    /// <summary>Die Räume der Stufe, in der das Haus wohnt (null ohne Raumregeln).</summary>
    public RaumstufeDaten? Raumstufe(Konto k) =>
        Daten?.Raeume is { Stufen.Count: > 0 } r ? r.Stufen[Math.Clamp(k.WohnhausStufe, 0, r.Stufen.Count - 1)] : null;

    /// <summary>Der Anzeigename eines Raums (die Kennung, wenn es ihn nicht gibt).</summary>
    public string Raumname(string raum) => Daten?.Raeume?.Art(raum)?.Name ?? raum;

    /// <summary>
    /// Die Räume des Wohnhauses dieses Hauses, in der Reihenfolge der Daten: erst die festen der Stufe, dann die gewählten
    /// (nur so viele, wie die Stufe freie Plätze hat). Leer ohne Raumregeln.
    /// </summary>
    public IReadOnlyList<string> Raeume(Konto k)
    {
        if (Raumstufe(k) is not { } s) return Array.Empty<string>();
        var liste = new List<string>(s.Feste);
        foreach (var r in k.WohnhausRaeume.Take(s.Frei))
            if (!liste.Contains(r)) liste.Add(r);
        return liste;
    }

    /// <summary>Hat das Wohnhaus diesen Raum? Ohne Raumregeln immer ja.</summary>
    public bool HatRaum(Konto k, string raum) => !HatRaumregeln || Raeume(k).Contains(raum);

    /// <summary>Freie Plätze, die das Haus jetzt noch mit einem Raum zur Wahl belegen kann.</summary>
    public int FreiePlaetze(Konto k) => Raumstufe(k) is { } s ? Math.Max(0, s.Frei - k.WohnhausRaeume.Count) : 0;

    /// <summary>Unter diesen Räumen kann das Haus jetzt wählen (leer, wenn kein Platz frei ist); Räume, die es schon hat, fehlen.</summary>
    public IReadOnlyList<string> WaehlbareRaeume(Konto k)
    {
        if (FreiePlaetze(k) == 0 || Daten?.Raeume is not { } r) return Array.Empty<string>();
        var schon = Raeume(k);
        return r.Waehlbar.Where(x => !schon.Contains(x)).ToList();
    }

    /// <summary>Warum dieses Haus diesen Raum jetzt nicht anbauen kann – oder null.</summary>
    public string? RaumAnbauenVerboten(Konto k, string raum)
    {
        if (!HatRaumregeln || Daten!.Raeume!.Art(raum) is not { } art) return $"Einen Raum „{raum}“ gibt es im Wohnhaus nicht.";
        if (!k.IstAktiv) return $"{k.Inhaber} ist nicht mehr im Spiel.";
        if (!Daten.Raeume.Waehlbar.Contains(raum)) return $"{art.Name} kann man nicht wählen.";
        if (HatRaum(k, raum)) return $"{k.Inhaber} hat schon: {art.Name}.";
        if (FreiePlaetze(k) == 0)
            return Daten.Raeume.Stufen.FindIndex(s => s.Frei > 0) is var ab and >= 0 && k.WohnhausStufe < ab
                ? $"Einen Raum zur Wahl gibt es erst im {Daten.Stufen[ab].Name}."
                : $"Im Wohnhaus von {k.Inhaber} ist kein Platz mehr frei.";
        var preis = _w.Baupreis(k, art.Preis);
        if (k.Vermoegen < preis) return $"{art.Name} kostet {Zahlenformat.Ganz(preis)} {_w.Daten.Allgemein.Waehrung}.";
        return null;
    }

    /// <summary>Befehl „Raum anbauen“: belegt einen freien Platz für immer mit diesem Raum.</summary>
    internal Befehlsergebnis RaumAnbauen(Konto k, string raum)
    {
        if (RaumAnbauenVerboten(k, raum) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var art = Daten!.Raeume!.Art(raum)!;
        var preis = _w.Baupreis(k, art.Preis); // Baumeister (Lebensweise), wie die Einrichtung
        if (preis > 0) k.Investition(preis);
        k.WohnhausRaumHinzufuegen(raum);
        return Befehlsergebnis.Ok(preis > 0
            ? $"{k.Inhaber} baut am Wohnhaus an: {art.Name} ({Zahlenformat.Ganz(preis)} {_w.Daten.Allgemein.Waehrung})."
            : $"{k.Inhaber} richtet im Wohnhaus ein: {art.Name}.");
    }

    // ── Sperren: Was ein Raum freischaltet ─────────────────────────────────

    /// <summary>Welcher Raum diesen Befehl freischaltet (null: keiner, der Befehl geht immer).</summary>
    public WohnraumDaten? RaumFuerBefehl(Befehl befehl) =>
        Daten?.Raeume is { } r ? r.Arten.FirstOrDefault(a => a.Befehle.Contains(befehl.GetType().Name)) : null;

    /// <summary>Warum dieses Haus den Befehl ohne den passenden Raum nicht geben kann – oder null (Raum da, oder der Befehl braucht keinen).</summary>
    public string? BefehlOhneRaum(Konto k, Befehl befehl) =>
        RaumFuerBefehl(befehl) is { } art && !HatRaum(k, art.Kennung) && !BerufStattRaum(k, art) ? Fehlt(k, art) : null;

    /// <summary>Ausnahme Dieb: Hat das Haus einen Betrieb des Berufs <see cref="WohnraumDaten.BerufStattRaum"/>, gehen die Befehle des Raums von dort aus.</summary>
    private bool BerufStattRaum(Konto k, WohnraumDaten art) =>
        art.BerufStattRaum is { } beruf && _w.Betriebe.Any(b => b.Konto == k && b.Artdaten.Beruf == beruf);

    /// <summary>Warum dieses Haus die Aktivität ohne den passenden Raum nicht unternehmen kann – oder null.</summary>
    public string? AktivitaetOhneRaum(Konto k, string aktivitaet) =>
        Daten?.Raeume?.Arten.FirstOrDefault(a => a.Aktivitaeten.Contains(aktivitaet)) is { } art && !HatRaum(k, art.Kennung) ? Fehlt(k, art) : null;

    /// <summary>Die Räume dieses Hauses mit ihren Daten (leer ohne Raumregeln) – für ihre Wirkung.</summary>
    public IEnumerable<WohnraumDaten> Raumdaten(Konto k) =>
        Daten?.Raeume is { } r ? Raeume(k).Select(r.Art).OfType<WohnraumDaten>() : Enumerable.Empty<WohnraumDaten>();

    /// <summary>„Dafür braucht das Wohnhaus von … den Raum Hinterzimmer (ab Bürgerhaus).“</summary>
    private string Fehlt(Konto k, WohnraumDaten art)
    {
        var r = Daten!.Raeume!;
        var fest = r.Stufen.FindIndex(s => s.Feste.Contains(art.Kennung));
        var wahl = r.Waehlbar.Contains(art.Kennung) ? r.Stufen.FindIndex(s => s.Frei > 0) : -1;
        var ab = fest >= 0 && (wahl < 0 || fest <= wahl) ? $" (ab {Daten.Stufen[fest].Name})"
            : wahl >= 0 ? $" (zur Wahl ab {Daten.Stufen[wahl].Name})" : "";
        return $"Dafür braucht das Wohnhaus von {k.Inhaber} den Raum „{art.Name}“{ab}.";
    }
}
