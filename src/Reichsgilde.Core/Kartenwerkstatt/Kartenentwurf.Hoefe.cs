using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ─────────────────────────────────────────────────────────────────────────────
//  Der Hof-Knopf der Kartenwerkstatt (Neue Partie, Nutzerwunsch 11.11.2026)
//
//  Ein Klick setzt einen Hof = Wohnhaus + Betrieb (Hofstelle) mit der nächsten freien Nummer: der erste Hof ist Hof 1 = Spieler 1, der zweite
//  Hof 2 …, höchstens 8 (Partiebesetzung.HoechstensHoefe). Die Karte speichert nur Lage, Drehung und Nummer. Drehen und Löschen sind je ein
//  Schritt für Rückgängig; Löschen lässt die höheren Nummern nachrücken, damit keine Lücke bleibt (die Prüfregel „hofstelle“ verlangt 1 … n
//  ohne Lücke). Die ausführliche Bearbeitung (Hofstelle bilden, Namen, Häuser) bleibt in den Eigenschaften und unter „Erweitert“.
// ─────────────────────────────────────────────────────────────────────────────
public sealed partial class Kartenentwurf
{
    /// <summary>Die kleinste freie Hofnummer (1 … 8) – null, wenn alle Höfe vergeben sind.</summary>
    public int? FreieHofnummer()
    {
        var belegt = _start.Hofstellen.Where(h => h.Nummer is not null).Select(h => h.Nummer!.Value).ToHashSet();
        for (var n = 1; n <= Partiebesetzung.HoechstensHoefe; n++)
            if (!belegt.Contains(n)) return n;
        return null;
    }

    /// <summary>Wie viele Höfe schon eine Nummer haben (der Hof-Knopf zählt „n von 8“).</summary>
    public int NummerierteHoefe => _start.Hofstellen.Count(h => h.Nummer is not null);

    /// <summary>
    /// Setzt einen Hof (Wohnhaus + Betrieb) mit der Mitte (x, z) und der Drehung und gibt ihm die kleinste freie Nummer; ein Schritt.
    /// </summary>
    /// <exception cref="ArgumentException">Wenn schon alle 8 Höfe gesetzt sind.</exception>
    public EntwurfHofstelle HofSetzen(double x, double z, double drehung = 0)
    {
        var nummer = FreieHofnummer() ?? throw new ArgumentException($"Es sind schon alle {Partiebesetzung.HoechstensHoefe} Höfe gesetzt.");
        return HofstelleSetzen(x, z, drehung, start: true, nummer: nummer);
    }

    /// <summary>Die Mitte des Hofes (die Mitte des Ganzen aus <see cref="HofstelleLagen"/>), errechnet aus der Lage seines Werkstattplatzes.</summary>
    public (double X, double Z, double Drehung)? HofMitte(string kennung)
    {
        if (_start.HofstelleMit(kennung) is not { } h || GebaeudeMit(h.Werkstatt) is not { } w) return null;
        var (pw, _, _) = HofstelleLagen(0, 0, w.Drehung);
        return (w.X - pw.X, w.Z - pw.Z, w.Drehung);
    }

    /// <summary>Dreht den Hof um seine Mitte um <paramref name="grad"/> (beide Plätze, ein Schritt); die Plätze stehen danach wieder nebeneinander.</summary>
    /// <exception cref="ArgumentException">Wenn es den Hof oder einen seiner Plätze nicht gibt.</exception>
    public void HofDrehen(string kennung, double grad)
    {
        PruefeHofstelle(kennung);
        var m = HofMitte(kennung) ?? throw new ArgumentException($"Der Hof „{kennung}“ hat keinen Werkstattplatz mehr.", nameof(kennung));
        var (pw, ph, d) = HofstelleLagen(m.X, m.Z, m.Drehung + grad);
        var h = _start.HofstelleMit(kennung)!;
        var befehle = new List<IKartenbefehl>();
        foreach (var (platz, ort) in new[] { (GebaeudeMit(h.Werkstatt), pw), (GebaeudeMit(h.Wohnhaus), ph) })
            if (platz is not null && Aenderung(platz, n => { n.X = Kartenraster.Punkt(ort.X); n.Z = Kartenraster.Punkt(ort.Z); n.Drehung = d; }) is { } b) befehle.Add(b);
        if (befehle.Count > 0) Geschichte.Ausfuehren(new Sammelbefehl($"Hof {kennung} gedreht", befehle));
    }

    /// <summary>
    /// Löscht den Hof mit beiden Plätzen (wie <see cref="HofstelleLoeschen"/>) und lässt die Höfe mit höherer Nummer nachrücken (Hof 3 wird Hof 2 …);
    /// alles ein Schritt für Rückgängig.
    /// </summary>
    public void HofLoeschen(string kennung)
    {
        var h = PruefeHofstelle(kennung);
        var weg = h.Nummer;
        var befehle = new List<IKartenbefehl>();
        if (Aenderung(_start, t => t.HofstellenSetzen(t.Hofstellen.Where(x => x.Kennung != kennung)
                .Select(x => weg is { } w && x.Nummer is { } n && n > w ? x with { Nummer = n - 1 } : x))) is { } eintrag) befehle.Add(eintrag);
        foreach (var g in new[] { GebaeudeMit(h.Werkstatt), GebaeudeMit(h.Wohnhaus) }.OfType<EntwurfGebaeude>().OrderByDescending(g => _gebaeude.IndexOf(g)))
            befehle.Add(new TeilLoeschen(g, _gebaeude.IndexOf(g)));
        Geschichte.Ausfuehren(new Sammelbefehl($"Hof {kennung} gelöscht", befehle));
    }
}
