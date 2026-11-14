using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Gewinnschätzung mit dem Aufschlag am Hafen: <see cref="Kalkulation.GewinnJeArbeitsminute"/> (mit Betrieb), <see cref="Kalkulation.WertJeArbeitsminute"/>
/// und <see cref="Kalkulation.ErwarteterJahresgewinn"/> (beim Ausbau mit Betrieb) rechnen den Erlös mit demselben Faktor wie die Vorschau
/// (<see cref="Kalkulation.GewinnJeStueck"/>) und die Abrechnung des Karrens (<see cref="Stadtwirtschaft.KarrenVerkaufsFaktor(Konto, string, bool)"/>).
/// Verglichen wird je Rechnung der Faktor auf den Marktpreis: Schätzung = Vorschau = Buchung, am offenen Hafen für Ausfuhrware 1,03, sonst 1.
/// Die Proben stehen in Köln wie in <see cref="HafenAufschlagTests"/>.
/// </summary>
static class GewinnschaetzungHafenTests
{
    /// <summary>Der Bauort in Köln mit der kürzesten Fahrt zum Marktplatz, an dem der Marktplatz näher ist als der Hafen (Karren zum Hafen an) – gerechnet in <see cref="HafenAufschlagTests"/> (einmal für alle Hafen-Tests).</summary>
    static Lazy<WerkstattBauen> NahAmMarkt => HafenAufschlagTests.NahAmMarkt;

    /// <summary>Faktoren auf den Marktpreis einer Probe: Schätzung, Vorschau (Gewinn je Stück) und Buchung; dazu Jahresgewinn mit und ohne Betrieb.</summary>
    readonly record struct Faktoren(double Schaetzung, double Vorschau, double Buchung, double JahrMit, double JahrOhne, double WertMit, double WertOhne,
        bool ZielHafen, bool HafenOffen);

    /// <summary>
    /// Köln zu dieser Minute, eine Gießerei an <paramref name="ort"/> (ohne Angabe nächst dem Hafen), die diese Ware macht und 15 Stück davon
    /// im Lager hat. Vor dem Aufbruch des Karrens: Schätzung (aus <see cref="Kalkulation.GewinnJeArbeitsminute"/> zurückgerechnet auf den Erlös je
    /// Stück) und Vorschau (<see cref="Kalkulation.GewinnJeStueck"/>), je als Faktor auf den Preis ohne Aufschlag. Dann fährt der Karren; die
    /// Buchung ist der gebuchte Umsatz ÷ dem, was dieselbe Ladung unmittelbar vor dem Verkauf ohne Aufschlag gebracht hätte.
    /// </summary>
    static Faktoren Probe(string json, long minute, string ware, WerkstattBauen? ort = null)
    {
        const int stueck = 15;
        var (sim, w, k) = HafenAufschlagTests.Koeln(json, minute);
        var e = w.Ausfuehren(k, ort ?? HafenAufschlagTests.NahAmHafen.Value);
        Pruefe.Wahr(e.Erfolg, e.Meldung);
        var name = w.Bauen.Grundstuecke.Last(g => g.Art == Grundstuecksart.Werkstatt).Betrieb!;
        var b = w.Betriebe.Single(x => x.Name == name);
        b.Produkt = ware;
        b.Einlagern(ware, stueck);
        var karren = b.Karren[0];
        Pruefe.Gleich(KarrenZustand.AmBetrieb, karren.Zustand);

        // Schätzung: Erlös je Stück nach Steuer = Gewinn je Minute × Zeit + Zutaten; geteilt durch Preis nach Steuer
        var daten = w.Daten.Waren[ware];
        var zutaten = daten.Zutaten.Sum(z => w.Markt[z.Key].Preis * z.Value);
        var schaetzung = (Kalkulation.GewinnJeArbeitsminute(w, ware, b) * daten.Zeit + zutaten) / (w.Markt[ware].Preis * (1 - w.Umsatzsteuer));
        var vorschau = Kalkulation.GewinnJeStueck(w, b, ware).Preis / (w.Markt[ware].Preis * w.Familien.VerkaufsFaktor(k));
        var jahrMit = Kalkulation.ErwarteterJahresgewinn(w, b.Art, betrieb: b);
        var jahrOhne = Kalkulation.ErwarteterJahresgewinn(w, b.Art);
        w.Einstellen(b, "Geselle", 1); // für den Wert je Minute: eine Ware in Arbeit
        var wertMit = Kalkulation.WertJeArbeitsminute(w, b);
        var wertOhne = Kalkulation.GewinnJeArbeitsminute(w, ware) / b.ZeitFaktor(ware);

        for (var i = 0; i < Spielzeit.MinutenProTag && karren.Zustand != KarrenZustand.ZumMarkt; i++) sim.Tick();
        Pruefe.Gleich(KarrenZustand.ZumMarkt, karren.Zustand, "aufgebrochen");
        double ohne = 0;
        var offen = false;
        for (var i = 0; i < Spielzeit.MinutenProTag && karren.Zustand != KarrenZustand.ZumBetrieb; i++)
        {
            ohne = w.Markt[ware].Verkaufswert(stueck) * w.Familien.VerkaufsFaktor(k) * b.PreisFaktor;
            offen = w.Schiffahrt.HafenOffen;
            sim.Tick();
        }
        Pruefe.Gleich(KarrenZustand.ZumBetrieb, karren.Zustand, "verkauft und auf dem Rückweg");
        Pruefe.Wahr(b.Laufend.Umsatz > 0, "verkauft");
        return new Faktoren(schaetzung, vorschau, b.Laufend.Umsatz / ohne, jahrMit, jahrOhne, wertMit, wertOhne, karren.ZielHafen, offen);
    }

    /// <summary>Schätzung, Vorschau und Buchung rechnen mit genau diesem Faktor auf den Marktpreis.</summary>
    static void GleicherFaktor(Faktoren f, double faktor, string fall)
    {
        foreach (var (wert, was) in new[] { (f.Schaetzung, "Schätzung"), (f.Vorschau, "Vorschau"), (f.Buchung, "Buchung") })
            Pruefe.Wahr(Math.Abs(wert - faktor) <= 1e-9, $"{fall}: {was} {wert} statt {faktor}");
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Gewinnschätzung: Silberringe (Ausfuhrware) einer Werkstatt am offenen Hafen – Gewinn je Minute rechnet den Erlös wie Vorschau und " +
         "Buchung mit 3 % Aufschlag; Wert je Minute und Jahresgewinn mit Betrieb höher als ohne; Gegenprobe: die alte Schätzung ohne Betrieb liegt daneben", () =>
        {
            var p = Probe(HafenAufschlagTests.Daten(0.03), HafenAufschlagTests.Minute(1, 9), "Silberring");
            Pruefe.Wahr(p.ZielHafen && p.HafenOffen, "am offenen Hafen verkauft");
            GleicherFaktor(p, 1.03, "Hafen");
            Pruefe.Wahr(p.WertMit > p.WertOhne, $"Wert je Minute mit Aufschlag {p.WertMit} > ohne {p.WertOhne}");
            Pruefe.Wahr(p.JahrMit >= p.JahrOhne, $"Jahresgewinn mit Betrieb {p.JahrMit} >= ohne {p.JahrOhne}");

            // Ohne Aufschlag in den Daten ist die Schätzung derselben Werkstatt genau die alte
            var o = Probe(HafenAufschlagTests.Daten(0), HafenAufschlagTests.Minute(1, 9), "Silberring");
            GleicherFaktor(o, 1, "ohne Aufschlag");
            Pruefe.Gleich(o.WertOhne, o.WertMit);
            Pruefe.Gleich(o.JahrOhne, o.JahrMit);
        }),

        ("Gewinnschätzung: ohne Aufschlag – Beschläge (keine Ausfuhrware) am Hafen, Silberringe am Marktplatz (ohne Karren zum Hafen bzw. von einem " +
         "Betrieb näher am Marktplatz) und im Winter (Hafen zu); Schätzung = Vorschau = Buchung = Preis, Zahl für Zahl wie ohne Betrieb " +
         "(Jahresgewinn abseits des Hafens ebenso)", () =>
        {
            var sommer = HafenAufschlagTests.Minute(1, 9);
            foreach (var (json, minute, ware, ort, fall, hafen) in new (string, long, string, WerkstattBauen?, string, bool)[]
                     {
                         (HafenAufschlagTests.Daten(0.03), sommer, "Beschlag", null, "Beschlag", true),
                         (HafenAufschlagTests.Daten(0.03, zumHafen: false), sommer, "Silberring", null, "Marktplatz", false),
                         (HafenAufschlagTests.Daten(0.03), sommer, "Silberring", NahAmMarkt.Value, "näher am Marktplatz", false),
                         (HafenAufschlagTests.Daten(0.03), HafenAufschlagTests.Minute(3, 9), "Silberring", null, "Winter", false),
                     })
            {
                var p = Probe(json, minute, ware, ort);
                Pruefe.Gleich(hafen, p.ZielHafen, $"{fall}: Ziel");
                GleicherFaktor(p, 1, fall);
                Pruefe.Gleich(p.WertOhne, p.WertMit, $"{fall}: Wert je Minute");
                // Der Jahresgewinn nimmt die beste Ware der Art – am Hafen auch Silberringe, dort also mit Aufschlag (dritter Test)
                if (!hafen) Pruefe.Gleich(p.JahrOhne, p.JahrMit, $"{fall}: Jahresgewinn");
            }
        }),

        ("Gewinnschätzung: Jahresgewinn und Mehrgewinn des Ausbaus einer Werkstatt am offenen Hafen rechnen mit dem Aufschlag, wenn ihre beste " +
         "Ware eine Ausfuhrware ist; ohne Betrieb (neuer Betrieb an unbekanntem Ort) wie bisher ohne", () =>
        {
            var (_, w, k) = HafenAufschlagTests.Koeln(HafenAufschlagTests.Daten(0.03), HafenAufschlagTests.Minute(1, 9));
            var e = w.Ausfuehren(k, HafenAufschlagTests.NahAmHafen.Value);
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            var name = w.Bauen.Grundstuecke.Last(g => g.Art == Grundstuecksart.Werkstatt).Betrieb!;
            var b = w.Betriebe.Single(x => x.Name == name);
            var schiff = w.Schiffahrt.Daten!;
            foreach (var art in new[] { b.Art }.Concat(b.Artdaten.Ausbau is { } neu ? new[] { neu } : Array.Empty<Betriebsart>()))
            {
                var produkte = w.Daten.Art(art).Produkte;
                var best = produkte.OrderByDescending(x => Kalkulation.GewinnJeArbeitsminute(w, x, b)).ThenBy(x => x, StringComparer.Ordinal).First();
                var mit = Kalkulation.ErwarteterJahresgewinn(w, art, betrieb: b);
                var ohne = Kalkulation.ErwarteterJahresgewinn(w, art);
                if (schiff.IstAusfuhrware(best)) Pruefe.Wahr(mit > ohne, $"{art}: beste Ware {best} am Hafen, Jahresgewinn {mit} > {ohne}");
                else Pruefe.Gleich(ohne, mit, $"{art}: beste Ware {best} ohne Aufschlag");
            }
            if (b.Artdaten.Ausbau is { } stufe)
                Pruefe.Gleich(Kalkulation.ErwarteterJahresgewinn(w, stufe, betrieb: b) - Kalkulation.ErwarteterJahresgewinn(w, b.Art, betrieb: b),
                    Kalkulation.AusbauMehrgewinn(w, b), "Ausbau mit dem Aufschlag des Betriebs");
        }),
    };
}
