using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Erlös-Vorschau mit dem Aufschlag am Hafen: Vorschau (Handelsauftrag im Marktfenster, <see cref="Stadtwirtschaft.KarrenErloes"/>,
/// <see cref="Kalkulation.GewinnJeStueck"/>, Einrichtung) und Abrechnung des Karrens rechnen über dieselbe Funktion
/// (<see cref="Stadtwirtschaft.KarrenVerkaufsFaktor(Konto, string, bool)"/>). Aufschlag nur für Ausfuhrware, nur am offenen Hafen, nie am
/// Marktplatz. Die Proben stehen in Köln am Bauort nächst dem Hafen (wie <see cref="HafenAufschlagTests"/>).
/// </summary>
static class ErloesVorschauTests
{
    /// <summary>
    /// Köln zu dieser Minute, eine Gießerei am Hafen mit <paramref name="stueck"/> Stück dieser Ware im Lager. Vor jeder Minute die Vorschau des
    /// Erlöses (<see cref="Stadtwirtschaft.KarrenErloes"/>); zurück: die Vorschau vor dem Aufbruch und unmittelbar vor dem Verkauf, der gebuchte
    /// Umsatz, das Ziel, ob der Hafen beim Verkauf offen war und die Vorschau ohne Aufschlag (alte Rechnung) unmittelbar vor dem Verkauf.
    /// </summary>
    static (double FaktorVorher, double FaktorUnterwegs, double Vorschau, double Umsatz, bool ZielHafen, bool HafenOffen, double OhneAufschlag)
        Probe(string json, long minute, string ware, int stueck = 15, bool handelsauftrag = false, WerkstattBauen? ort = null)
    {
        var (sim, w, k) = HafenAufschlagTests.Koeln(json, minute);
        var e = w.Ausfuehren(k, ort ?? HafenAufschlagTests.NahAmHafen.Value);
        Pruefe.Wahr(e.Erfolg, e.Meldung);
        var name = w.Bauen.Grundstuecke.Last(g => g.Art == Grundstuecksart.Werkstatt).Betrieb!;
        var b = w.Betriebe.Single(x => x.Name == name);
        b.Einlagern(ware, stueck);
        var karren = b.Karren[0];
        Pruefe.Gleich(KarrenZustand.AmBetrieb, karren.Zustand);
        var faktorVorher = w.KarrenVerkaufsFaktor(karren, ware);
        if (handelsauftrag)
        {
            var a = w.Ausfuehren(k, new Handelsauftrag(b.Name, 0, new Dictionary<string, int> { [ware] = stueck }, new Dictionary<string, int>()));
            Pruefe.Wahr(a.Erfolg, a.Meldung);
        }
        for (var i = 0; i < Spielzeit.MinutenProTag && karren.Zustand != KarrenZustand.ZumMarkt; i++) sim.Tick();
        Pruefe.Gleich(KarrenZustand.ZumMarkt, karren.Zustand, "aufgebrochen");
        var faktorUnterwegs = w.KarrenVerkaufsFaktor(karren, ware);
        double vorschau = 0, ohne = 0;
        var offen = false;
        for (var i = 0; i < Spielzeit.MinutenProTag && karren.Zustand != KarrenZustand.ZumBetrieb; i++)
        {
            vorschau = w.KarrenErloes(karren, ware, stueck);
            ohne = w.Markt[ware].Verkaufswert(stueck) * w.Familien.VerkaufsFaktor(k) * b.PreisFaktor;
            offen = w.Schiffahrt.HafenOffen;
            sim.Tick();
        }
        Pruefe.Gleich(KarrenZustand.ZumBetrieb, karren.Zustand, "verkauft und auf dem Rückweg");
        Pruefe.Wahr(b.Laufend.Umsatz > 0, "verkauft");
        return (faktorVorher, faktorUnterwegs, vorschau, b.Laufend.Umsatz, karren.ZielHafen, offen, ohne);
    }

    /// <summary>Der Bauort in Köln mit der kürzesten Fahrt zum Marktplatz, an dem der Marktplatz näher ist als der Hafen (Karren zum Hafen an).</summary>
    static readonly Lazy<WerkstattBauen> NahAmMarkt = new(() =>
    {
        const string hand = "Handkarren";
        var (_, w, k) = HafenAufschlagTests.Koeln(HafenAufschlagTests.Daten(0.03), HafenAufschlagTests.Minute(1, 9));
        int Rund(int? hin, int? zurueck) => (hin ?? 9999) + (zurueck ?? 9999);
        return w.Bauen.Bauorte(k, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0)).Cast<WerkstattBauen>()
            .Select(o => (Ort: o, Hafen: Rund(w.FahrzeitZumHafenAmBauort(o, hand), w.FahrzeitZumHafenAmBauort(o, hand, false)),
                Markt: Rund(w.FahrzeitAmBauort(o, hand), w.FahrzeitAmBauort(o, hand, false))))
            .Where(x => x.Markt < x.Hafen)
            .OrderBy(x => x.Markt).ThenBy(x => x.Ort.X).ThenBy(x => x.Ort.Z).ThenBy(x => x.Ort.Drehung)
            .First().Ort;
    });

    /// <summary>Die Vorschau unmittelbar vor dem Verkauf trifft den gebuchten Umsatz (bis auf Rundung).</summary>
    static void Trifft(double vorschau, double umsatz, string fall) =>
        Pruefe.Wahr(Math.Abs(vorschau - umsatz) <= 1e-9 * umsatz, $"{fall}: Vorschau {vorschau} = Umsatz {umsatz}");

    /// <summary>Eine Gießerei am Hafen in Köln zu dieser Minute mit zwei Gesellen an dieser Ware (für Stückrechnung und Einrichtung).</summary>
    static (Stadtwirtschaft W, Betrieb B) Werkstatt(string json, long minute, string ware, WerkstattBauen? ort = null)
    {
        var (_, w, k) = HafenAufschlagTests.Koeln(json, minute);
        var e = w.Ausfuehren(k, ort ?? HafenAufschlagTests.NahAmHafen.Value);
        Pruefe.Wahr(e.Erfolg, e.Meldung);
        var name = w.Bauen.Grundstuecke.Last(g => g.Art == Grundstuecksart.Werkstatt).Betrieb!;
        var b = w.Betriebe.Single(x => x.Name == name);
        b.Produkt = ware;
        w.Einstellen(b, "Geselle", 2);
        return (w, b);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Erlös-Vorschau: Silberringe (Ausfuhrware) am offenen Hafen – Vorschau vor dem Aufbruch und unterwegs mit 3 % Aufschlag, unmittelbar vor " +
         "dem Verkauf gleich dem gebuchten Umsatz; Gegenprobe: die alte Vorschau ohne Aufschlag liegt 3 % daneben; Handelsauftrag ebenso", () =>
        {
            foreach (var hand in new[] { false, true })
            {
                var p = Probe(HafenAufschlagTests.Daten(0.03), HafenAufschlagTests.Minute(1, 9), "Silberring", handelsauftrag: hand);
                Pruefe.Wahr(p.ZielHafen && p.HafenOffen, "am offenen Hafen verkauft");
                Pruefe.Ungefaehr(p.FaktorVorher, p.FaktorUnterwegs, 1e-12);
                Trifft(p.Vorschau, p.Umsatz, hand ? "Handelsauftrag" : "Verwalter");
                Pruefe.Ungefaehr(1.03, p.Vorschau / p.OhneAufschlag, 1e-12); // der Aufschlag ist genau 3 %
                Pruefe.Wahr(Math.Abs(p.OhneAufschlag - p.Umsatz) > 0.02 * p.Umsatz, "Gegenprobe: ohne Aufschlag 3 % zu niedrig");
            }
        }),

        ("Erlös-Vorschau: ohne Aufschlag – Beschläge (keine Ausfuhrware) am Hafen, Silberringe am Marktplatz (ohne Karren zum Hafen bzw. von " +
         "einem Betrieb näher am Marktplatz) und im Winter (Hafen zu, der Karren fährt zum Marktplatz); jeweils Vorschau = Umsatz und gleich der alten Rechnung", () =>
        {
            var beschlag = Probe(HafenAufschlagTests.Daten(0.03), HafenAufschlagTests.Minute(1, 9), "Beschlag");
            Pruefe.Wahr(beschlag.ZielHafen, "Beschläge am Hafen");
            var markt = Probe(HafenAufschlagTests.Daten(0.03, zumHafen: false), HafenAufschlagTests.Minute(1, 9), "Silberring");
            Pruefe.Wahr(!markt.ZielHafen, "zum Marktplatz");
            // Karren zum Hafen an, Hafen offen, aber dieser Betrieb liegt näher am Marktplatz: dort kein Aufschlag
            var naeher = Probe(HafenAufschlagTests.Daten(0.03), HafenAufschlagTests.Minute(1, 9), "Silberring", ort: NahAmMarkt.Value);
            Pruefe.Wahr(!naeher.ZielHafen && naeher.HafenOffen, "Hafen offen, Karren fährt zum näheren Marktplatz");
            var winter = Probe(HafenAufschlagTests.Daten(0.03), HafenAufschlagTests.Minute(3, 9), "Silberring");
            Pruefe.Wahr(!winter.ZielHafen && !winter.HafenOffen, "Winter: Hafen zu, zum Marktplatz");
            foreach (var (p, fall) in new[] { (beschlag, "Beschlag"), (markt, "Marktplatz"), (naeher, "näher am Marktplatz"), (winter, "Winter") })
            {
                Trifft(p.Vorschau, p.Umsatz, fall);
                Pruefe.Gleich(p.OhneAufschlag, p.Vorschau, fall);
                Pruefe.Gleich(p.FaktorVorher, p.FaktorUnterwegs, fall);
            }
        }),

        ("Erlös-Vorschau: Gewinn je Stück und Mehrgewinn einer Einrichtung (Preis +10 %) rechnen für Silberringe am Hafen 3 % mehr Erlös als " +
         "ohne Aufschlag; Beschläge, Marktplatz (ohne Karren zum Hafen bzw. näher am Marktplatz) und Winter gleich", () =>
        {
            var preisPlus = new EinrichtungWirkung { PreisFaktor = 1.1 };
            (double Preis, double Mehr) Rechnung(string json, long minute, string ware, WerkstattBauen? ort = null)
            {
                var (w, b) = Werkstatt(json, minute, ware, ort);
                return (Kalkulation.GewinnJeStueck(w, b, ware).Preis, w.Werkstaetten.MehrgewinnDurch(b, preisPlus));
            }
            var sommer = HafenAufschlagTests.Minute(1, 9);
            var mit = Rechnung(HafenAufschlagTests.Daten(0.03), sommer, "Silberring");
            var ohne = Rechnung(HafenAufschlagTests.Daten(0), sommer, "Silberring");
            Pruefe.Wahr(ohne.Mehr > 0, $"Mehrgewinn {ohne.Mehr}");
            Pruefe.Ungefaehr(ohne.Preis * 1.03, mit.Preis, 1e-9 * mit.Preis);
            Pruefe.Ungefaehr(ohne.Mehr * 1.03, mit.Mehr, 1e-9 * mit.Mehr);

            foreach (var (json, minute, ware, ort, fall) in new (string, long, string, WerkstattBauen?, string)[]
                     {
                         (HafenAufschlagTests.Daten(0.03), sommer, "Beschlag", null, "Beschlag"),
                         (HafenAufschlagTests.Daten(0.03, zumHafen: false), sommer, "Silberring", null, "Marktplatz"),
                         (HafenAufschlagTests.Daten(0.03), sommer, "Silberring", NahAmMarkt.Value, "näher am Marktplatz"),
                         (HafenAufschlagTests.Daten(0.03), HafenAufschlagTests.Minute(3, 9), "Silberring", null, "Winter"),
                     })
            {
                var a = Rechnung(json, minute, ware, ort);
                var b = Rechnung(json.Replace("\"hafenAufschlag\":0.03", "\"hafenAufschlag\":0"), minute, ware, ort);
                Pruefe.Wahr(json.Contains("\"hafenAufschlag\":0.03"), "Aufschlag in den Testdaten");
                Pruefe.Gleich(b.Preis, a.Preis, fall);
                Pruefe.Gleich(b.Mehr, a.Mehr, fall);
            }
        }),
    };
}
