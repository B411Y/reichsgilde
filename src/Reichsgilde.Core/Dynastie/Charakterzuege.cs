using Reichsgilde.Core.Ki;

namespace Reichsgilde.Core.Dynastie;

/// <summary>Woher ein Charakterzug kommt.</summary>
public enum Zugart
{
    /// <summary>Drei davon bekommt jeder bei der Volljährigkeit – aus Erziehung, Eltern und Volk. Gegensätze schließen sich aus.</summary>
    Persoenlichkeit,
    /// <summary>Selten, schon bei der Geburt da und mit einer Wahrscheinlichkeit von den Eltern geerbt.</summary>
    Angeboren,
    /// <summary>Kommt im Lauf des Lebens durch ein Ereignis dazu (Universität, Pest überstanden, Trauer …).</summary>
    Erworben,
}

/// <summary>
/// Platz für die Meinungen (Charaktersystem Teil 2, PR folgt): Wie dieser Zug die Meinung anderer Personen färbt.
/// In Teil 1 wirken die Werte noch nicht – sie stehen nur schon in den Daten.
/// </summary>
public sealed class ZugMeinungDaten
{
    /// <summary>Meinung über jemanden mit demselben Zug (Gleich und Gleich gesellt sich gern).</summary>
    public double Gleich { get; init; }

    /// <summary>Meinung über jemanden mit dem gegenteiligen Zug.</summary>
    public double Gegenteil { get; init; }

    /// <summary>Was alle anderen von einem Träger dieses Zugs halten (Schön +, Trinker −).</summary>
    public double Allgemein { get; init; }
}

/// <summary>Ein Charakterzug in den Daten (Abschnitt "charakterzuege.zuege", Schlüssel = Kennung).</summary>
public sealed class CharakterzugDaten
{
    /// <summary>Anzeigename mit Umlauten („Jähzornig“) – die Kennung ist der Schlüssel ohne Umlaute („Jaehzornig“).</summary>
    public string Name { get; init; } = "";
    public Zugart Art { get; init; }

    /// <summary>Kennung des Gegenteils (nur Persönlichkeitszüge und angeborene) – beide schließen sich aus.</summary>
    public string? Gegenteil { get; init; }

    /// <summary>Ein Satz zur Person („Zählt jeden Pfennig zweimal.“).</summary>
    public string Beschreibung { get; init; } = "";

    /// <summary>Was ein KI-Haus mit diesem Oberhaupt anders macht – als Satz für das Charakterfenster.</summary>
    public string Verhalten { get; init; } = "";

    /// <summary>Sterne mehr oder weniger je Talent (halbe Sterne erlaubt).</summary>
    public Dictionary<Talent, double> Talente { get; init; } = new();

    // ── Vergabe ──────────────────────────────────────────────────────────

    /// <summary>Persönlichkeit: zusätzliches Gewicht je Ausbildungsweg (Schlüssel aus "dynastie.talente.ausbildungen").</summary>
    public Dictionary<string, double> Erziehung { get; init; } = new();

    /// <summary>Persönlichkeit: zusätzliches Gewicht je Volk.</summary>
    public Dictionary<Volk, double> Voelker { get; init; } = new();

    /// <summary>Angeboren: Chance bei der Geburt (und für Erwachsene, die neu in die Stadt kommen).</summary>
    public double Chance { get; init; }

    /// <summary>Angeboren: zusätzliche Chance je Elternteil, das den Zug hat.</summary>
    public double Vererbung { get; init; }

    /// <summary>
    /// Erworben: Chance je Anlass. Anlässe: „jahr“ (jedes Jahr für Erwachsene), „trauer“ (im Jahr, in dem ein Angehöriger starb),
    /// „schaenke“ (jedes Jahr, wenn das Haus eine Schänke führt), „abschluss:&lt;Ausbildung&gt;“, „ueberstanden:&lt;Krankheit&gt;“,
    /// „einbruch“ und „brand“ (Anschlag auf das Wohnhaus, trifft das Oberhaupt).
    /// </summary>
    public Dictionary<string, double> Anlaesse { get; init; } = new();

    /// <summary>Erworben: Faktor auf die Chance, wenn die Person schon diesen Zug hat (Fromm × 4 für den Pilger).</summary>
    public Dictionary<string, double> ZugFaktor { get; init; } = new();

    // ── Wirkung ──────────────────────────────────────────────────────────

    /// <summary>Faktor auf Haushalt bzw. Unterhalt dieser Person (Bescheiden 0,85).</summary>
    public double Haushalt { get; init; } = 1;

    /// <summary>Faktor auf die Chance zu erkranken (Kränklich 2).</summary>
    public double Krankheit { get; init; } = 1;

    /// <summary>Nur Ratsherren: Faktor auf die Gunst je geschenktem Gulden (Gierig 1,3, Ehrlich 0,7).</summary>
    public double Bestechlich { get; init; } = 1;

    /// <summary>Wie ein KI-Haus mit diesem Oberhaupt handelt.</summary>
    public ZugKiDaten Ki { get; init; } = new();

    /// <summary>Platz für Teil 2 (Meinungen) – wirkt noch nicht.</summary>
    public ZugMeinungDaten Meinung { get; init; } = new();
}

/// <summary>
/// Die Charakterzüge (Abschnitt "charakterzuege" in wirtschaft.json, ab Datenversion 60). Fehlt der Abschnitt, hat niemand Züge.
///
/// Vergabe:
///   • Angeborene bei der Geburt: Chance + Vererbung × Zahl der Eltern mit dem Zug (Erwachsene von außen: nur die Chance).
///   • Persönlichkeit bei der Volljährigkeit (Erwachsene von außen sofort): <see cref="JePerson"/> Züge, nacheinander gezogen mit
///     Gewicht = Grundgewicht + Erziehung (Abschluss) + Volk + Elterngewicht × Zahl der Eltern mit dem Zug; ein gezogener Zug
///     nimmt sein Gegenteil aus dem Topf.
///   • Erworbene bei ihrem Anlass (siehe <see cref="CharakterzugDaten.Anlaesse"/>).
/// </summary>
public sealed class CharakterzuegeDaten
{
    /// <summary>So viele Persönlichkeitszüge bekommt jede Person.</summary>
    public int JePerson { get; init; } = 3;

    /// <summary>Gewicht jedes Persönlichkeitszugs ohne Erziehung, Volk und Eltern.</summary>
    public double Grundgewicht { get; init; } = 1;

    /// <summary>Zusätzliches Gewicht je Elternteil mit diesem Zug (Kinder ähneln ihren Eltern).</summary>
    public double ElternGewicht { get; init; } = 1.5;

    public Dictionary<string, CharakterzugDaten> Zuege { get; init; } = new();

    public bool IstAktiv => Zuege.Count > 0;

    /// <summary>Liste und Nachschlagetabelle zusammen in einem Objekt – so sieht ein anderer Faden immer beide oder keins.</summary>
    private sealed class Zugliste(List<Charakterzug> alle)
    {
        public readonly List<Charakterzug> Alle = alle;
        public readonly Dictionary<string, Charakterzug> NachKennung = alle.ToDictionary(z => z.Kennung, StringComparer.Ordinal);
    }

    /// <summary>
    /// Einmal gebaut, dann geteilt: Dieselben Daten nutzen mehrere Partien, die Messung sogar gleichzeitig.
    /// <see cref="Lazy{T}"/> baut die Liste genau einmal (threadsicher), damit jeder Zug genau ein Objekt bleibt.
    /// </summary>
    private readonly Lazy<Zugliste> _katalog;

    public CharakterzuegeDaten() =>
        _katalog = new Lazy<Zugliste>(() => new Zugliste(Zuege.Select(x => new Charakterzug(x.Key, x.Value)).OrderBy(z => z.Art).ToList()),
            LazyThreadSafetyMode.ExecutionAndPublication);

    private Zugliste Katalog => _katalog.Value;

    /// <summary>Alle Züge, nach Art und dann in der Reihenfolge der Daten.</summary>
    public IReadOnlyList<Charakterzug> Alle => Katalog.Alle;

    public Charakterzug? Zug(string kennung) => Katalog.NachKennung.GetValueOrDefault(kennung);

    internal void Pruefen(List<string> f, ICollection<string> ausbildungen, ICollection<string> krankheiten)
    {
        if (!IstAktiv) return;
        const string p = "charakterzuege";
        if (JePerson < 0) f.Add($"{p}.jePerson darf nicht negativ sein.");
        if (Grundgewicht < 0 || ElternGewicht < 0) f.Add($"{p}: grundgewicht und elternGewicht dürfen nicht negativ sein.");
        var paare = Zuege.Count(z => z.Value.Art == Zugart.Persoenlichkeit && z.Value.Gegenteil is not null) / 2;
        var persoenlich = Zuege.Count(z => z.Value.Art == Zugart.Persoenlichkeit);
        if (persoenlich - paare < JePerson) f.Add($"{p}: Für {JePerson} Persönlichkeitszüge je Person gibt es zu wenige, die sich nicht ausschließen.");
        foreach (var (kennung, z) in Zuege)
        {
            var q = $"{p}.zuege.{kennung}";
            if (string.IsNullOrWhiteSpace(z.Name)) f.Add($"{q}: name fehlt.");
            if (z.Gegenteil is { } g)
            {
                if (z.Art == Zugart.Erworben) f.Add($"{q}: Erworbene Züge haben kein Gegenteil.");
                if (!Zuege.TryGetValue(g, out var gegen)) f.Add($"{q}: Gegenteil „{g}“ gibt es nicht.");
                else if (gegen.Gegenteil != kennung) f.Add($"{q}: Das Gegenteil „{g}“ muss auf „{kennung}“ zurückzeigen.");
                else if (gegen.Art != z.Art) f.Add($"{q}: Gegenteil „{g}“ muss dieselbe Art haben.");
            }
            foreach (var (t, w) in z.Talente)
                if (Math.Abs(w * 2 - Math.Round(w * 2)) > 1e-9) f.Add($"{q}.talente.{t}: nur ganze und halbe Sterne.");
            foreach (var weg in z.Erziehung.Keys.Where(w => !ausbildungen.Contains(w)))
                f.Add($"{q}.erziehung: Ausbildung „{weg}“ gibt es nicht.");
            if (z.Erziehung.Values.Concat(z.Voelker.Values).Any(w => w < 0)) f.Add($"{q}: Gewichte dürfen nicht negativ sein.");
            if (z.Chance is < 0 or > 1 || z.Vererbung is < 0 or > 1) f.Add($"{q}: chance und vererbung müssen zwischen 0 und 1 liegen.");
            if (z.Art != Zugart.Angeboren && (z.Chance > 0 || z.Vererbung > 0)) f.Add($"{q}: chance und vererbung gibt es nur für angeborene Züge.");
            if (z.Art != Zugart.Persoenlichkeit && (z.Erziehung.Count > 0 || z.Voelker.Count > 0)) f.Add($"{q}: erziehung und voelker gibt es nur für Persönlichkeitszüge.");
            if (z.Art != Zugart.Erworben && z.Anlaesse.Count > 0) f.Add($"{q}: anlaesse gibt es nur für erworbene Züge.");
            foreach (var (anlass, chance) in z.Anlaesse)
            {
                if (chance is < 0 or > 1) f.Add($"{q}.anlaesse.{anlass}: Chance muss zwischen 0 und 1 liegen.");
                var (art, wert) = Anlass(anlass);
                if (art is not ("jahr" or "trauer" or "schaenke" or "einbruch" or "brand" or "abschluss" or "ueberstanden"))
                    f.Add($"{q}.anlaesse: Anlass „{anlass}“ gibt es nicht.");
                else if (art == "abschluss" && !ausbildungen.Contains(wert ?? "")) f.Add($"{q}.anlaesse: Ausbildung „{wert}“ gibt es nicht.");
                else if (art == "ueberstanden" && !krankheiten.Contains(wert ?? "")) f.Add($"{q}.anlaesse: Krankheit „{wert}“ gibt es nicht.");
            }
            foreach (var (k, faktor) in z.ZugFaktor)
                if (!Zuege.ContainsKey(k) || faktor < 0) f.Add($"{q}.zugFaktor.{k}: Zug fehlt oder Faktor negativ.");
            if (z.Haushalt < 0 || z.Krankheit < 0 || z.Bestechlich <= 0) f.Add($"{q}: haushalt und krankheit ≥ 0, bestechlich > 0.");
            z.Ki.Pruefen(f, q + ".ki");
        }
    }

    /// <summary>„abschluss:Universität“ → („abschluss“, „Universität“), „jahr“ → („jahr“, null).</summary>
    internal static (string Art, string? Wert) Anlass(string anlass) =>
        anlass.IndexOf(':') is var i and >= 0 ? (anlass[..i], anlass[(i + 1)..]) : (anlass, null);
}

/// <summary>
/// Ein Charakterzug, wie ihn das Charakterfenster zeigt: Kennung (für Icon und Spielstand), Name, Art und was er bewirkt.
/// </summary>
public sealed class Charakterzug
{
    public string Kennung { get; }
    public CharakterzugDaten Daten { get; }
    public string Name => Daten.Name;
    public Zugart Art => Daten.Art;
    public string? Gegenteil => Daten.Gegenteil;
    public string Beschreibung => Daten.Beschreibung;

    /// <summary>Sterne mehr (oder weniger) in diesem Talent.</summary>
    public double Talent(Talent t) => Daten.Talente.GetValueOrDefault(t);

    /// <summary>„Verhandeln +1, Rhetorik −½. Haushalt −15 %. Als KI-Haus: …“ – für den Tooltip.</summary>
    public string Wirkungstext { get; }

    /// <summary>„Persönlichkeit“, „angeboren“ oder „erworben“.</summary>
    public string Artname => Art switch
    {
        Zugart.Persoenlichkeit => "Persönlichkeit",
        Zugart.Angeboren => "angeboren",
        _ => "erworben",
    };

    internal Charakterzug(string kennung, CharakterzugDaten daten)
    {
        Kennung = kennung;
        Daten = daten;
        Wirkungstext = Texten(daten);
    }

    private static string Texten(CharakterzugDaten d)
    {
        var teile = new List<string>();
        var talente = TalentTexte.Alle.Where(t => Math.Abs(d.Talente.GetValueOrDefault(t)) > 1e-9)
            .Select(t => $"{t.Kurz()} {(d.Talente[t] > 0 ? "+" : "−")}{TalentTexte.Sterne(Math.Abs(d.Talente[t]))}").ToList();
        if (talente.Count > 0) teile.Add(string.Join(", ", talente));
        string Prozent(double faktor) => $"{(faktor > 1 ? "+" : "−")}{Zahlenformat.Ganz(Math.Abs(faktor - 1) * 100)} %";
        if (Math.Abs(d.Haushalt - 1) > 1e-9) teile.Add($"Haushalt {Prozent(d.Haushalt)}");
        if (Math.Abs(d.Krankheit - 1) > 1e-9) teile.Add($"Krankheiten {Prozent(d.Krankheit)}");
        if (Math.Abs(d.Bestechlich - 1) > 1e-9) teile.Add($"als Ratsherr: Geschenke wirken {Prozent(d.Bestechlich)}");
        if (!string.IsNullOrWhiteSpace(d.Verhalten)) teile.Add($"Als KI-Haus: {d.Verhalten}");
        return teile.Count == 0 ? "" : string.Join(". ", teile) + (teile[^1].EndsWith('.') ? "" : ".");
    }

    public override string ToString() => Name;

    // Gleich ist, was dieselbe Kennung hat – auch wenn zwei Partien ihre Daten getrennt geladen haben.
    public override bool Equals(object? obj) => obj is Charakterzug z && z.Kennung == Kennung;
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Kennung);
    public static bool operator ==(Charakterzug? a, Charakterzug? b) => a?.Kennung == b?.Kennung;
    public static bool operator !=(Charakterzug? a, Charakterzug? b) => !(a == b);
}

/// <summary>Eltern, Ehepartner und Kinder einer Person – für das Charakterfenster.</summary>
/// <param name="Ehepartner">Der lebende Ehepartner – sonst der andere Elternteil der Kinder (auch verstorben); null bei Ledigen.</param>
public sealed record Verwandtschaft(Person? Vater, Person? Mutter, Person? Ehepartner, IReadOnlyList<Person> Kinder);

/// <summary>Würfelt Züge – für Personen der Häuser und für Ratsherren dieselben Regeln.</summary>
internal static class Zugwuerfel
{
    /// <summary>
    /// Ein Startwert aus drei Zahlen, gut durchmischt: Der .NET-Generator liefert für benachbarte Startwerte
    /// ähnliche erste Würfe – Personennummern liegen aber dicht beieinander.
    /// </summary>
    internal static int Mischen(int a, int b, int c)
    {
        unchecked
        {
            var h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u ^ (uint)c * 0xC2B2AE3Du;
            h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
            return (int)(h & 0x7FFFFFFF);
        }
    }

    /// <summary>Angeborene Züge: je Zug ein Wurf (immer, damit die Reihe der Würfe fest bleibt).</summary>
    internal static List<Charakterzug> Angeboren(CharakterzuegeDaten d, Zufallsquelle z, IReadOnlyCollection<Charakterzug> eltern)
    {
        var neu = new List<Charakterzug>();
        foreach (var zug in d.Alle.Where(x => x.Art == Zugart.Angeboren))
        {
            var chance = Math.Min(0.95, zug.Daten.Chance + zug.Daten.Vererbung * eltern.Count(e => e == zug));
            if (z.NextDouble() < chance && !neu.Any(n => n.Gegenteil == zug.Kennung))
                neu.Add(zug);
        }
        return neu;
    }

    /// <summary>Persönlichkeitszüge, bis die Person <see cref="CharakterzuegeDaten.JePerson"/> hat.</summary>
    /// <param name="eltern">Persönlichkeitszüge beider Eltern zusammen (ein Zug beider Eltern zählt doppelt).</param>
    internal static List<Charakterzug> Persoenlichkeit(CharakterzuegeDaten d, Zufallsquelle z, Volk volk, string? erziehung,
        IReadOnlyCollection<Charakterzug> eltern, IEnumerable<Charakterzug> schon)
    {
        var vorhanden = schon.ToList();
        var neu = new List<Charakterzug>();
        var topf = d.Alle.Where(x => x.Art == Zugart.Persoenlichkeit
                                     && !vorhanden.Any(v => v == x || v.Gegenteil == x.Kennung)).ToList();
        var fehlt = d.JePerson - vorhanden.Count(v => v.Art == Zugart.Persoenlichkeit);
        while (fehlt-- > 0 && topf.Count > 0)
        {
            var gewichte = topf.Select(x => Math.Max(0, d.Grundgewicht
                                                        + (erziehung is null ? 0 : x.Daten.Erziehung.GetValueOrDefault(erziehung))
                                                        + x.Daten.Voelker.GetValueOrDefault(volk)
                                                        + d.ElternGewicht * eltern.Count(e => e == x))).ToList();
            var summe = gewichte.Sum();
            var wurf = z.NextDouble() * summe;
            var wahl = topf.Count - 1;
            for (var i = 0; i < topf.Count; i++)
            {
                wurf -= gewichte[i];
                if (wurf < 0) { wahl = i; break; }
            }
            if (summe <= 0) wahl = 0;
            var zug = topf[wahl];
            neu.Add(zug);
            topf.RemoveAll(x => x == zug || x.Kennung == zug.Gegenteil);
        }
        return neu;
    }
}
