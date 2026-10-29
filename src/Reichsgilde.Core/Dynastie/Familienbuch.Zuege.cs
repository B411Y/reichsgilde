using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Dynastie;

/// <summary>
/// Charakterzüge der Personen (PR #227, Teil 1 des Charaktersystems).
///
/// Wann eine Person Züge bekommt:
///   • Geburt: angeborene (Klug, Stark …) – Chance + Vererbung je Elternteil mit dem Zug.
///   • Volljährigkeit (im Jahreswechsel, in dem sie volljährig wird): drei Persönlichkeitszüge aus Erziehung, Volk und Eltern.
///   • Erwachsene, die neu ins Spiel kommen (Oberhaupt bei Partiebeginn, Heiratskandidaten): beides sofort.
///   • Erworbene bei ihrem Anlass: Universität abgeschlossen, Krankheit überstanden, Anschlag aufs Wohnhaus, Trauer, jedes Jahr.
///
/// Würfe: Angeborene und Persönlichkeit würfelt ein eigener Würfel je Person (aus Startwert und Personennummer) –
/// so ändern die Züge keinen anderen Wurf, und alte Spielstände bekommen beim Laden genau die Züge, die eine neue Partie
/// mit demselben Startwert gegeben hätte. Erworbene Züge würfelt ein eigener Generator des Familienbuchs (im Spielstand).
///
/// Für Teil 2 (Meinungen) stehen die Werte schon in <see cref="CharakterzugDaten.Meinung"/>; Teil 3 (Lebensweise, Stress,
/// Geheimnisse) dockt an <see cref="ZugErhalten"/> und <see cref="ZugAnlass(Konto?, Person, string)"/> an.
/// </summary>
public sealed partial class Familienbuch
{
    private Zufallsquelle _zugZufall;

    /// <summary>Die Daten der Charakterzüge (null = ohne Züge).</summary>
    public CharakterzuegeDaten? ZugDaten => _w.Daten.Charakterzuege is { IstAktiv: true } d ? d : null;
    public bool HatZuege => ZugDaten is not null;

    /// <summary>Alle Züge, die es gibt (Persönlichkeit, angeboren, erworben).</summary>
    public IReadOnlyList<Charakterzug> Zugkatalog => ZugDaten?.Alle ?? Array.Empty<Charakterzug>();

    public Charakterzug? Zug(string kennung) => ZugDaten?.Zug(kennung);

    /// <summary>Eine Person hat einen erworbenen Zug bekommen (nicht bei Geburt und Volljährigkeit).</summary>
    public event Action<Person, Charakterzug>? ZugErhalten;

    /// <summary>Faktor auf Haushalt bzw. Unterhalt der Person (Bescheiden spart, Hochmütig prasst).</summary>
    public double HaushaltFaktor(Person p) => p.Zuege.Aggregate(1.0, (f, z) => f * z.Daten.Haushalt);

    /// <summary>Faktor auf die Chance zu erkranken (Kränklich, Stark, Trinker).</summary>
    public double KrankheitFaktor(Person p) => p.Zuege.Aggregate(1.0, (f, z) => f * z.Daten.Krankheit);

    /// <summary>Wie ein KI-Haus mit diesem Oberhaupt handelt (neutral ohne Familie oder ohne Züge).</summary>
    public Ki.ZugNeigung KiNeigung(Konto konto) =>
        HatZuege && Von(konto) is { } f ? Ki.ZugNeigung.Aus(f.Oberhaupt.Zuege) : Ki.ZugNeigung.Neutral;

    // ═════════════════════════════════════════════════════════════════════════
    //  Auskunft für das Charakterfenster
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Eine Person der Häuser oder der Brautschau nach ihrer Nummer (auch Verstorbene) – oder null.</summary>
    public Person? PersonNummer(int nummer) =>
        _familien.Values.SelectMany(f => f.MitgliederListe).Concat(_kandidaten.Select(k => k.Person)).FirstOrDefault(p => p.Nummer == nummer);

    /// <summary>
    /// Eltern, Ehepartner und Kinder. Eltern fehlen bei Personen, die von außen kamen (Oberhaupt bei Spielbeginn, Angeheiratete).
    /// Ehepartner: der lebende, sonst der andere Elternteil der Kinder (verwitwet).
    /// </summary>
    public Verwandtschaft Verwandte(Person p)
    {
        var vater = p.Vater is int v ? PersonNummer(v) : null;
        var mutter = p.Mutter is int m ? PersonNummer(m) : null;
        var f = FamilieVon(p);
        var kinder = f?.KinderVon(p).ToList() ?? new List<Person>();
        Person? partner = null;
        if (f is not null && f.Oberhaupt == p) partner = f.Ehepartner;
        else if (f is not null && f.Ehepartner == p) partner = f.Oberhaupt;
        if (partner is null && kinder.Count > 0 && (kinder[^1].Vater == p.Nummer ? kinder[^1].Mutter : kinder[^1].Vater) is int n)
            partner = PersonNummer(n);
        return new Verwandtschaft(vater, mutter, partner, kinder);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Vergabe
    // ═════════════════════════════════════════════════════════════════════════

    private const int ZweckAngeboren = 1, ZweckPersoenlichkeit = 2;

    /// <summary>Der eigene Würfel einer Person – fest aus Startwert, Nummer und Zweck.</summary>
    private Zufallsquelle Personenwuerfel(int nummer, int zweck) => new(Zugwuerfel.Mischen(_zugZufall.Startwert, nummer, zweck));

    private static bool HatPersoenlichkeit(Person p) => p.Zuege.Any(z => z.Art == Zugart.Persoenlichkeit);

    /// <summary>Angeborene Züge eines Neugeborenen (ohne Eltern: nur die Grundchance).</summary>
    private void ZuegeBeiGeburt(Person kind, Person? vater = null, Person? mutter = null)
    {
        if (ZugDaten is not { } d) return;
        kind.TalentHoechstwert = _d.Talente.Hoechstwert;
        var eltern = new[] { vater, mutter }.OfType<Person>().SelectMany(e => e.Zuege.Where(z => z.Art == Zugart.Angeboren)).ToList();
        foreach (var z in Zugwuerfel.Angeboren(d, Personenwuerfel(kind.Nummer, ZweckAngeboren), eltern))
            kind.ZugHinzufuegen(z);
    }

    /// <summary>Persönlichkeitszüge bei der Volljährigkeit – aus Abschluss, Volk und den Zügen der Eltern.</summary>
    private List<Charakterzug> ZuegeAlsErwachsener(Person p, Func<int, Person?>? person = null)
    {
        if (ZugDaten is not { } d) return new List<Charakterzug>();
        p.TalentHoechstwert = _d.Talente.Hoechstwert;
        person ??= PersonNummer;
        var eltern = new[] { p.Vater, p.Mutter }.OfType<int>().Select(person).OfType<Person>()
            .SelectMany(e => e.Zuege.Where(z => z.Art == Zugart.Persoenlichkeit)).ToList();
        var neu = Zugwuerfel.Persoenlichkeit(d, Personenwuerfel(p.Nummer, ZweckPersoenlichkeit), p.Volk, p.Abschluss, eltern, p.Zuege);
        foreach (var z in neu) p.ZugHinzufuegen(z);
        return neu;
    }

    /// <summary>Wer erwachsen neu ins Spiel kommt (Oberhaupt, Heiratskandidat), bekommt alles auf einmal.</summary>
    private void ZuegeFuerErwachsenen(Person p)
    {
        ZuegeBeiGeburt(p);
        ZuegeAlsErwachsener(p);
    }

    private static string Aufzaehlung(IReadOnlyList<Charakterzug> zuege) => zuege.Count switch
    {
        0 => "",
        1 => zuege[0].Name,
        _ => string.Join(", ", zuege.Take(zuege.Count - 1).Select(z => z.Name)) + " und " + zuege[^1].Name,
    };

    /// <summary>
    /// Im Jahreswechsel, nach Ausbildung und Geburt: Wer im neuen Jahr volljährig ist, bekommt seine Persönlichkeit.
    /// Dann würfelt jeder Erwachsene die erworbenen Züge, die jedes Jahr kommen können (Anlässe „jahr“, „trauer“, „schaenke“).
    /// </summary>
    private void ZuegeImJahreswechsel(Familie f)
    {
        if (ZugDaten is not { } d) return;
        foreach (var p in f.Lebende.Where(p => !HatPersoenlichkeit(p) && p.Alter(Jahr + 1) >= _d.Volk(p.Volk).Volljaehrig).ToList())
            if (ZuegeAlsErwachsener(p) is { Count: > 0 } neu)
                Ereignis(f.Konto, $"{p.Vorname} {f.Nachname} ist erwachsen geworden: {Aufzaehlung(neu)}.");

        var trauer = f.Mitglieder.Any(m => m.Todesjahr == Jahr);
        var schaenke = _w.Betriebe.Any(b => b.Konto == f.Konto && b.IstSchaenke);
        var erworbene = d.Alle.Where(z => z.Art == Zugart.Erworben).ToList();
        foreach (var p in f.Lebende.Where(IstVolljaehrig).ToList())
            foreach (var zug in erworbene)
            {
                if (p.Zuege.Contains(zug)) continue;
                var a = zug.Daten.Anlaesse;
                var chance = (a.GetValueOrDefault("jahr") + (trauer ? a.GetValueOrDefault("trauer") : 0)
                                                         + (schaenke ? a.GetValueOrDefault("schaenke") : 0)) * Zugfaktor(zug, p);
                if (chance > 0 && _zugZufall.NextDouble() < chance)
                    Erhalten(f.Konto, p, zug);
            }
    }

    /// <summary>Faktor auf die Chance eines erworbenen Zugs aus den Zügen, die die Person schon hat (Fromm × 4 für den Pilger).</summary>
    private static double Zugfaktor(Charakterzug zug, Person p) =>
        p.Zuege.Aggregate(1.0, (f, z) => f * zug.Daten.ZugFaktor.GetValueOrDefault(z.Kennung, 1));

    /// <summary>
    /// Ein einmaliger Anlass im Leben der Person („abschluss:Universität“, „ueberstanden:Pest“, „einbruch“, „brand“):
    /// Jeder erworbene Zug mit diesem Anlass kommt mit seiner Chance (bei 1 ohne Wurf).
    /// </summary>
    internal void ZugAnlass(Konto? konto, Person p, string anlass)
    {
        if (ZugDaten is not { } d || !p.Lebt) return;
        foreach (var zug in d.Alle.Where(z => z.Art == Zugart.Erworben && z.Daten.Anlaesse.ContainsKey(anlass)).ToList())
        {
            if (p.Zuege.Contains(zug)) continue;
            var chance = zug.Daten.Anlaesse[anlass] * Zugfaktor(zug, p);
            if (chance >= 1 || (chance > 0 && _zugZufall.NextDouble() < chance))
                Erhalten(konto, p, zug);
        }
    }

    /// <summary>Ein Anlass, der das Oberhaupt eines Hauses trifft (Einbruch, Brand im Wohnhaus).</summary>
    internal void ZugAnlass(Konto konto, string anlass)
    {
        if (HatZuege && Von(konto) is { } f) ZugAnlass(konto, f.Oberhaupt, anlass);
    }

    /// <summary>Entwicklerfenster: Die Person bekommt den Zug sofort; sein Gegenteil fällt weg.</summary>
    internal void EntwicklerZugGeben(Person p, Charakterzug zug)
    {
        if (zug.Gegenteil is { } g && p.Zuege.FirstOrDefault(z => z.Kennung == g) is { } gegenteil)
            p.ZugEntfernen(gegenteil);
        Erhalten(FamilieVon(p)?.Konto, p, zug);
    }

    private void Erhalten(Konto? konto, Person p, Charakterzug zug)
    {
        p.ZugHinzufuegen(zug);
        if (konto is not null)
            Ereignis(konto, $"Neuer Charakterzug für {p.Vorname} {p.Familienname}: {zug.Name} – {zug.Beschreibung}");
        ZugErhalten?.Invoke(p, zug);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Laden
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Züge aus dem Spielstand – oder, in Spielständen von vor den Charakterzügen, nachgewürfelt (nach Nummer, Eltern zuerst):
    /// angeborene für alle, Persönlichkeit für Volljährige, „Gelehrt“ für Absolventen der Universität.
    /// </summary>
    private void ZuegeWiederherstellen(FamilienbuchStand s, IReadOnlyDictionary<int, Person> personen)
    {
        if (ZugDaten is not { } d) return;
        Person? Nummer(int n) => personen.GetValueOrDefault(n);
        foreach (var ps in s.Personen.OrderBy(p => p.Nummer))
        {
            var p = personen[ps.Nummer];
            p.TalentHoechstwert = _d.Talente.Hoechstwert;
            if (ps.Zuege is { } kennungen)
            {
                foreach (var z in kennungen.Select(d.Zug).OfType<Charakterzug>()) // Züge, die es nicht mehr gibt, fallen weg
                    p.ZugHinzufuegen(z);
                continue;
            }
            ZuegeBeiGeburt(p, p.Vater is int v ? Nummer(v) : null, p.Mutter is int m ? Nummer(m) : null);
            if (p.Alter(Jahr) >= _d.Volk(p.Volk).Volljaehrig)
                ZuegeAlsErwachsener(p, Nummer);
            if (p.Abschluss is { } weg)
                foreach (var zug in d.Alle.Where(z => z.Art == Zugart.Erworben && z.Daten.Anlaesse.GetValueOrDefault($"abschluss:{weg}") >= 1))
                    p.ZugHinzufuegen(zug);
        }
    }
}
