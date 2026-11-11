using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;

/// <summary>
/// Der Hof-Knopf der Kartenwerkstatt (Neue Partie, 11.11.2026): <c>HofSetzen</c> (nächste freie Nummer, höchstens 8), <c>HofDrehen</c>,
/// <c>HofLoeschen</c> (Nummern rücken nach, ein Schritt) – und die Karten des Spiels tragen ihre Hofnummern.
/// </summary>
static class KartenwerkstattHofKnopfTests
{
    static Kartenentwurf Neu() => Kartenentwurf.Neu("probe", "Probe", 400, 300, HofstellenTests.Katalog.Value);

    static string Nummern(Kartenentwurf e) => string.Join(",", e.Start.Hofstellen.Select(h => $"{h.Kennung}={h.Nummer?.ToString() ?? "-"}"));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Hof-Knopf: jeder Klick setzt einen Hof mit der nächsten Nummer (Startplatz), beim neunten kommt ein Fehler, Rückgängig nimmt Plätze und Nummer zurück", () =>
        {
            var e = Neu();
            for (var i = 0; i < 8; i++) e.HofSetzen(-150 + 40 * i, 0, 0);
            Pruefe.Gleich("H1=1,H2=2,H3=3,H4=4,H5=5,H6=6,H7=7,H8=8", Nummern(e));
            Pruefe.Wahr(e.Start.Hofstellen.All(h => h.Start), "alle Startplatz");
            Pruefe.Gleich(8, e.NummerierteHoefe);
            Pruefe.Gleich(null, e.FreieHofnummer());
            Pruefe.WirftMitMeldung(() => e.HofSetzen(0, 100, 0), "alle 8 Höfe");
            Pruefe.Gleich(8, e.Start.Hofstellen.Count); // Gegenprobe: der Fehler setzt nichts
            Pruefe.Wahr(e.Rueckgaengig() && e.Start.Hofstellen.Count == 7 && e.GebaeudeMit("H8_Werkstatt") is null && e.FreieHofnummer() == 8, "ein Schritt zurück");
        }),

        ("Hof-Knopf: Drehen dreht beide Plätze um die Mitte des Hofes (ein Schritt), die Mitte bleibt; Q und zurück ergibt die alte Lage", () =>
        {
            var e = Neu();
            var h = e.HofSetzen(0, 50, 0);
            var vorher = (e.GebaeudeMit(h.Werkstatt)!.X, e.GebaeudeMit(h.Werkstatt)!.Z, e.GebaeudeMit(h.Wohnhaus)!.X);
            e.HofDrehen(h.Kennung, 90);
            Pruefe.Gleich(90.0, e.GebaeudeMit(h.Werkstatt)!.Drehung);
            Pruefe.Gleich(90.0, e.GebaeudeMit(h.Wohnhaus)!.Drehung);
            var m = e.HofMitte(h.Kennung)!.Value;
            Pruefe.Wahr(Math.Abs(m.X) < 0.1 && Math.Abs(m.Z - 50) < 0.1, $"Mitte bleibt (0|50), ist {m.X:0.##}|{m.Z:0.##}");
            Pruefe.Wahr(e.Rueckgaengig(), "Rückgängig");
            Pruefe.Gleich(0.0, e.GebaeudeMit(h.Werkstatt)!.Drehung);
            Pruefe.Gleich(vorher, (e.GebaeudeMit(h.Werkstatt)!.X, e.GebaeudeMit(h.Werkstatt)!.Z, e.GebaeudeMit(h.Wohnhaus)!.X));
            e.HofDrehen(h.Kennung, 90);
            e.HofDrehen(h.Kennung, -90);
            Pruefe.Gleich(vorher, (e.GebaeudeMit(h.Werkstatt)!.X, e.GebaeudeMit(h.Werkstatt)!.Z, e.GebaeudeMit(h.Wohnhaus)!.X));
            Pruefe.WirftMitMeldung(() => e.HofDrehen("X9", 15), "Keine Hofstelle");
        }),

        ("Hof-Knopf: Löschen nimmt beide Plätze weg und lässt die höheren Nummern nachrücken – ein Schritt; die Prüfregel findet keine Lücke", () =>
        {
            var e = Neu();
            for (var i = 0; i < 4; i++) e.HofSetzen(-100 + 60 * i, 0, 0);
            e.HofLoeschen("H2");
            Pruefe.Gleich("H1=1,H3=2,H4=3", Nummern(e));
            Pruefe.Wahr(e.GebaeudeMit("H2_Werkstatt") is null && e.GebaeudeMit("H2_Wohnhaus") is null, "Plätze weg");
            Pruefe.Gleich(4, e.FreieHofnummer());
            Pruefe.Gleich("", string.Join(" / ", e.Befunde.Where(b => b.Regel == "hofstelle").Select(b => b.Text))); // keine Lücke, keine Doppelten
            Pruefe.Wahr(e.Rueckgaengig() && Nummern(e) == "H1=1,H2=2,H3=3,H4=4" && e.GebaeudeMit("H2_Werkstatt") is not null, "ein Schritt zurück");
            // Gegenprobe: der letzte Hof löscht, ohne dass etwas nachrückt; ein neuer bekommt wieder die freie Nummer
            e.HofLoeschen("H4");
            Pruefe.Gleich("H1=1,H2=2,H3=3", Nummern(e));
            Pruefe.Gleich(4, e.HofSetzen(0, 100, 0).Nummer);
        }),

        ("Hof-Knopf: Nummern und Lage kommen in die Datei und wieder heraus (nur Lage, Drehung, Nummer)", () =>
        {
            var e = Neu();
            e.HofSetzen(0, 0, 90);
            e.HofSetzen(60, 0, 0);
            var text = e.ZuJson();
            var neu = Kartenentwurf.AusJson(text, HofstellenTests.Katalog.Value);
            Pruefe.Gleich("H1=1,H2=2", Nummern(neu));
            Pruefe.Gleich(90.0, neu.GebaeudeMit("H1_Werkstatt")!.Drehung);
            var karte = Stadtkarte.AusJson(text, HofstellenTests.Katalog.Value);
            Pruefe.Wahr(Hofstellenvergabe.Nummeriert(karte), "die Karte ist nummeriert");
            Pruefe.Gleich("H1,H2", string.Join(",", Hofstellenvergabe.Startplaetze(karte).Select(h => h.Kennung)));
        }),

        ("Hof-Knopf: Die Greybox lässt sich mit dem Umbau-Weg nummerieren (H1–H4 = Hof 1–4): Vergabe nach Nummer, Prüfung ohne Fehler; die Karten des Spiels sind bis zur Entscheidung des Teamleiters unnummeriert", () =>
        {
            var e = Kartenentwurf.AusJson(HofstellenTests.OhneHofstellen(File.ReadAllText(Datenpfad.Karte("greybox"))), HofstellenTests.Katalog.Value);
            var n = 0;
            foreach (var h in e.Start.Hofstellen.Where(h => h.Start).ToList()) e.HofstelleNummerSetzen(h.Kennung, ++n);
            var karte = Stadtkarte.AusJson(e.ZuJson(), HofstellenTests.Katalog.Value);
            Pruefe.Wahr(Hofstellenvergabe.Nummeriert(karte), "nummeriert");
            Pruefe.Gleich("H1=1,H2=2,H3=3,H4=4", string.Join(",", Hofstellenvergabe.Startplaetze(karte).Select(h => $"{h.Kennung}={h.Nummer}")));
            Pruefe.Gleich(0, karte.Pruefen().Count, "ohne Fehler");
            foreach (var kennung in new[] { "greybox", "koeln", "probestadt", "messstadt" })
                Pruefe.Wahr(!Hofstellenvergabe.Nummeriert(Stadtkarte.AusDatei(Datenpfad.Karte(kennung))), $"{kennung} noch ohne Nummern");
        }),
    };
}
