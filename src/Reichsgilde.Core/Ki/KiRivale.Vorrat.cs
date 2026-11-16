using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Ki;

/// <summary>
/// Vorrat auf Verdacht (Nutzer 16.11.2026: die KI sieht nicht in die Zukunft; Abschnitt <c>ki.vorrat</c>, <see cref="KiVorratDaten"/>).
/// Die KI fragt keine Ansage ab (<c>Ernte.Angesagt</c>, <c>Lagen.Angesagt</c>). Stattdessen würfelt jedes KI-Haus zur ersten Stunde des Jahres
/// einmal, ob es glaubt, dass es schlecht wird:
///   • Chance <see cref="KiVorratDaten.Chance"/> (10 %), mit Anzeichen <see cref="KiVorratDaten.ChanceMitAnzeichen"/> (30 %). Anzeichen ist nur,
///     was auch der Spieler im laufenden Jahr sieht: eine geltende schlechte oder schwere Lage einer Quelle (magere Ernte, Missernte samt
///     Teuerung, die Lagen der Rohstoffquellen) oder ein strenger Winter.
///   • Bei einem Treffer legt jede Werkstatt von der Grundnahrung und von ihren Zutaten je Ware einen gewürfelten Anteil (15–35 %) ihres Lagers
///     je Ware zurück, insgesamt höchstens so viel, wie das Haus zum Marktpreis bezahlen kann. Grundnahrung, die die Werkstatt nicht verarbeitet,
///     kauft ein Karren per Handelsauftrag (wie der Spieler, mit Händlerspanne) und hält sie im Lager zurück; Zutaten füllt der Verwalter bis zum
///     Vorrat auf (<see cref="Vorhalten"/>).
///   • Beim nächsten Wurf ohne Treffer gibt das Haus den Vorrat frei: Zurückgehaltene Grundnahrung verkauft der Karren wieder, Zutaten werden
///     verbraucht. Bei einem neuen Treffer gilt der neue Vorrat.
/// Der Wurf kommt aus einer eigenen <see cref="Zufallsquelle"/> aus dem Startwert der Partie, der Nummer des Hauses und dem Jahr: Er verschiebt
/// keinen anderen Wurf und ist nach dem Laden derselbe; gespeichert werden nur Vorrat und offener Einkauf.
/// </summary>
public sealed partial class KiRivale
{
    private static readonly int VorratSalz = Rohstofflage.SalzAus("ki-vorrat");

    /// <summary>Betriebsname → Ware → Bestand, den das Haus in diesem Jahr vorhält.</summary>
    private readonly SortedDictionary<string, SortedDictionary<string, int>> _vorrat = new(StringComparer.Ordinal);

    /// <summary>Betriebsname → Ware → Stück, die ein Karren noch per Handelsauftrag kaufen soll.</summary>
    private readonly SortedDictionary<string, SortedDictionary<string, int>> _vorratOffen = new(StringComparer.Ordinal);

    /// <summary>Was das Haus gerade vorhält (Betrieb → Ware → Bestand), leer ohne Vorrat.</summary>
    public IReadOnlyDictionary<string, SortedDictionary<string, int>> Vorrat => _vorrat;

    /// <summary>Gibt es im laufenden Jahr Anzeichen, die auch der Spieler sieht – eine geltende schlechte oder schwere Lage, ein strenger Winter?</summary>
    internal bool VorratsAnzeichen =>
        _w.Jahreszeiten.IstStrengerWinter || _w.Lagen.Jetzt.Any(l => l.Guete is Lagegute.Schlecht or Lagegute.Schwer);

    /// <summary>Der Wurf dieses Hauses für ein Jahr: Treffer (glaubt, dass es schlecht wird) und Anteil des Lagers (Anteil ± Streuung).</summary>
    internal (bool Treffer, double Anteil) Vorratswurf(KiVorratDaten d, int jahr, bool anzeichen)
    {
        var nummer = _w.Rivalen.ToList().IndexOf(this);
        var z = new Zufallsquelle(Jahreszeitenwesen.Mischen(_w.Zufall.Startwert ^ VorratSalz ^ (nummer * 7919), jahr));
        var treffer = z.NextDouble() < (anzeichen ? d.ChanceMitAnzeichen : d.Chance);
        var anteil = d.Anteil + (2 * z.NextDouble() - 1) * d.Streuung;
        return (treffer, anteil);
    }

    /// <summary>
    /// Bis zu welchem Bestand der Verwalter einer KI-Werkstatt diese Zutat jetzt auffüllt – null, wenn er nichts Besonderes vorhält: der Vorrat vor
    /// dem Winter (<see cref="WinterVorrat"/>) oder der Vorrat auf Verdacht, was größer ist.
    /// </summary>
    internal int? Vorhalten(Betrieb betrieb, string zutat)
    {
        var winter = WinterVorrat(betrieb, zutat);
        int? jahr = Konto.IstAktiv && _vorrat.TryGetValue(betrieb.Name, out var waren) && waren.TryGetValue(zutat, out var ziel) ? ziel : null;
        return winter is null ? jahr : jahr is null ? winter : Math.Max(winter.Value, jahr.Value);
    }

    /// <summary>Zur ersten Stunde des Jahres: würfeln, den alten Vorrat freigeben oder einen neuen anlegen.</summary>
    private void JahresvorratWuerfeln()
    {
        if (_w.Daten.Ki?.Vorrat is not { } d) return;
        var anzeichen = VorratsAnzeichen;
        var (treffer, anteil) = Vorratswurf(d, _w.Uhr.Jahr, anzeichen);
        var alt = _vorrat.ToDictionary(x => x.Key, x => x.Value.Keys.ToList());
        _vorrat.Clear();
        _vorratOffen.Clear();
        if (treffer) VorratPlanen(d, anteil);
        // Freigeben: Was zurückgehalten war und jetzt nicht mehr vorgehalten wird, verkauft der Karren wieder.
        var freigegeben = false;
        foreach (var (name, waren) in alt.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (Betriebe.FirstOrDefault(b => b.Name == name) is not { } b) continue;
            foreach (var ware in waren)
                if (b.HaeltZurueck(ware) && !(_vorrat.TryGetValue(name, out var neu) && neu.ContainsKey(ware)))
                    freigegeben |= StillBefehlen(new WareZurueckhalten(name, ware, false));
        }
        if (treffer)
            Entscheiden($"glaubt, dass es schlecht wird{(anzeichen ? " (es gibt Anzeichen)" : "")}, und legt {Prozent(anteil)} des Lagers zurück: " +
                        (_vorrat.Count == 0 ? "nichts – kein Geld oder keine Werkstatt."
                            : string.Join("; ", _vorrat.Select(x => $"{x.Key}: {string.Join(", ", x.Value.Select(v => $"{v.Value} {v.Key}"))}")) + "."));
        else if (alt.Count > 0)
            Entscheiden(freigegeben ? "glaubt nicht mehr, dass es schlecht wird, und gibt den Vorrat zum Verkauf frei."
                : "glaubt nicht mehr, dass es schlecht wird, und verbraucht den Vorrat.");
    }

    /// <summary>
    /// Den Vorrat jeder Werkstatt festlegen: Grundnahrung (ohne eigene Produkte; nur mit Handel von Hand) und Zutaten, je Ware Anteil × Lager je Ware,
    /// der Einkauf (was fehlt) zusammen höchstens so teuer, wie das Haus Geld hat.
    /// </summary>
    private void VorratPlanen(KiVorratDaten d, double anteil)
    {
        var geld = Konto.Vermoegen;
        foreach (var b in Betriebe.Where(b => b.IstWerkstatt).ToList())
        {
            var ziel = (int)(anteil * b.LagerKapazitaetJeWare + 0.5);
            var grund = _w.Daten.Handel is null ? new List<string>()
                : d.Grundnahrung.Where(g => _w.Daten.Waren.ContainsKey(g) && !b.Zutaten.ContainsKey(g) && !b.Artdaten.Produkte.Contains(g)).ToList();
            var waren = grund.Concat(b.Zutaten.Keys.Order(StringComparer.Ordinal)).Distinct().ToList();
            foreach (var ware in waren)
            {
                var zutat = b.Zutaten.ContainsKey(ware);
                var stueck = Math.Max(0, ziel - b.ImLager(ware));
                double Kosten(int n) => zutat ? _w.Markt[ware].Kaufen(n) : _w.HandKaufpreis(Konto, ware, n);
                while (stueck > 0 && Kosten(stueck) > geld) stueck--;
                if (stueck <= 0 && b.ImLager(ware) < ziel) continue; // kein Geld mehr: diese Ware nicht vorhalten
                geld -= stueck > 0 ? Kosten(stueck) : 0;
                Eintragen(_vorrat, b.Name, ware, b.ImLager(ware) + stueck);
                if (!zutat && stueck > 0) Eintragen(_vorratOffen, b.Name, ware, stueck);
            }
        }
    }

    private static void Eintragen(SortedDictionary<string, SortedDictionary<string, int>> ziel, string betrieb, string ware, int stueck)
    {
        if (!ziel.TryGetValue(betrieb, out var waren)) ziel[betrieb] = waren = new(StringComparer.Ordinal);
        waren[ware] = stueck;
    }

    /// <summary>Stündlich: Steht ein Karren am Betrieb und hat der Markt bei seiner Ankunft offen, kauft er die offene Grundnahrung (Handelsauftrag).</summary>
    private void VorratEinkaufen()
    {
        if (_vorratOffen.Count == 0 || _w.Daten.Handel is null) return;
        var leer = new Dictionary<string, int>();
        foreach (var (name, offen) in _vorratOffen.ToList())
        {
            if (Betriebe.FirstOrDefault(b => b.Name == name) is not { } b) { _vorratOffen.Remove(name); continue; }
            for (var nr = 0; nr < b.Karren.Count; nr++)
            {
                var karren = b.Karren[nr];
                if (karren.Zustand != KarrenZustand.AmBetrieb) continue;
                var flaechen = karren.Daten.Sorten;
                var kauf = new Dictionary<string, int>();
                foreach (var (ware, stueck) in offen)
                {
                    var n = Math.Min(Math.Min(stueck, flaechen * karren.Daten.KapazitaetJeSorte), b.LagerKapazitaetJeWare - b.ImLager(ware));
                    if (n <= 0 || flaechen <= 0) continue;
                    kauf[ware] = n;
                    flaechen -= (n + karren.Daten.KapazitaetJeSorte - 1) / karren.Daten.KapazitaetJeSorte;
                }
                if (kauf.Count == 0) { _vorratOffen.Remove(name); break; }
                if (_w.HandelsauftragVerboten(b, nr, leer, kauf) is not null) continue;
                if (!StillBefehlen(new Handelsauftrag(name, nr, leer, kauf))) break;
                foreach (var (ware, n) in kauf)
                    if ((offen[ware] -= n) <= 0) offen.Remove(ware);
                if (offen.Count == 0) _vorratOffen.Remove(name);
                break;
            }
        }
    }

    /// <summary>
    /// Wie <see cref="Befehlen"/>, aber ohne Eintrag bei Erfolg: Einkauf und Freigabe des Vorrats sind Folgen der einen Entscheidung zu Jahresbeginn
    /// (die steht schon in den Entscheidungen). Ein abgelehnter Befehl wird wie dort gemeldet.
    /// </summary>
    private bool StillBefehlen(Befehl befehl)
    {
        var ergebnis = _w.Ausfuehren(Konto, befehl);
        if (!ergebnis.Erfolg) Entscheiden($"wollte, durfte aber nicht: {ergebnis.Meldung}");
        return ergebnis.Erfolg;
    }

    private static Dictionary<string, Dictionary<string, int>>? Kopie(SortedDictionary<string, SortedDictionary<string, int>> d) =>
        d.Count == 0 ? null : d.ToDictionary(x => x.Key, x => new Dictionary<string, int>(x.Value)); // leer: nicht im Spielstand

    private static void Laden(SortedDictionary<string, SortedDictionary<string, int>> ziel, Dictionary<string, Dictionary<string, int>>? stand)
    {
        if (stand is null) return;
        foreach (var (betrieb, waren) in stand)
            foreach (var (ware, stueck) in waren)
                Eintragen(ziel, betrieb, ware, stueck);
    }

    private void VorratWiederherstellen(RivaleStand s)
    {
        Laden(_vorrat, s.Vorrat);
        Laden(_vorratOffen, s.VorratOffen);
    }
}
