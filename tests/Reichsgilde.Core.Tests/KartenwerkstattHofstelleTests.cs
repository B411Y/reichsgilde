using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

/// <summary>
/// Das Werkzeug „Hofstelle“ der Kartenwerkstatt (09.11.2026): Hofstelle setzen (Werkstatt- und Wohnhausplatz nebeneinander), bilden, Start-
/// Kennzeichen, Name, auflösen, löschen; Löschen und Umbenennen eines Platzes ziehen die Hofstelle nach; Spieler und KI-Häuser auf eine Hofstelle
/// setzen. Jeder Befehl ist ein Schritt mit Rückgängig.
/// </summary>
static class KartenwerkstattHofstelleTests
{
    static Kartenentwurf Neu() => Kartenentwurf.Neu("probe", "Probe", 400, 300, HofstellenTests.Katalog.Value);

    static Kartenentwurf Greybox() =>
        Kartenentwurf.AusJson(HofstellenTests.OhneHofstellen(File.ReadAllText(Datenpfad.Karte("greybox"))), HofstellenTests.Katalog.Value);

    static string Ort(EntwurfGebaeude g) => $"{g.X:0.##}|{g.Z:0.##}|{g.Drehung:0.#}";

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Werkzeug Hofstelle: setzt Werkstattplatz (16 × 14) und Wohnhausplatz (8 × 10) nebeneinander, vorn bündig, in einer Drehung – ein Schritt", () =>
        {
            var e = Neu();
            var h = e.HofstelleSetzen(0, 50, 0, start: true);
            Pruefe.Gleich(new EntwurfHofstelle("H1", null, "H1_Werkstatt", "H1_Wohnhaus", true), h);
            Pruefe.Gleich("-4|50|0", Ort(e.GebaeudeMit("H1_Werkstatt")!));
            Pruefe.Gleich("8|52|0", Ort(e.GebaeudeMit("H1_Wohnhaus")!)); // vorn bündig: beide Vorderkanten bei z = 57
            Pruefe.Gleich("werkstattplatz wohnhausplatz", $"{e.GebaeudeMit("H1_Werkstatt")!.Katalog} {e.GebaeudeMit("H1_Wohnhaus")!.Katalog}");
            var paar = e.Befunde.Where(b => b.Schwere == Schwere.Fehler && b.Gebaeude.Contains("H1_Werkstatt") && b.Gebaeude.Contains("H1_Wohnhaus")).ToList();
            Pruefe.Gleich("", string.Join(" / ", paar.Select(b => b.Text))); // die beiden Plätze stören sich nicht
            // Gedreht nach Osten (90°): die Werkstatt liegt dann nördlich – links von vorn gesehen
            var g = e.HofstelleSetzen(100, 0, 90);
            Pruefe.Gleich("H2", g.Kennung);
            Pruefe.Gleich("100|4|90", Ort(e.GebaeudeMit("H2_Werkstatt")!));
            Pruefe.Gleich("102|-8|90", Ort(e.GebaeudeMit("H2_Wohnhaus")!));
            Pruefe.Wahr(e.Rueckgaengig() && e.GebaeudeMit("H2_Werkstatt") is null && e.GebaeudeMit("H2_Wohnhaus") is null && e.Start.Hofstellen.Count == 1, "ein Schritt zurück");
            Pruefe.Wahr(e.Wiederholen() && e.Start.Hofstellen.Count == 2 && e.GebaeudeMit("H2_Wohnhaus") is not null, "wiederholt");
        }),

        ("Werkzeug Hofstelle: bilden aus vorhandenen Plätzen, Start-Kennzeichen und Name setzen, auflösen – Fehler bei falschen Plätzen", () =>
        {
            var e = Neu();
            var w = e.Setzen("werkstattplatz", 0, 0);
            var h = e.Setzen("wohnhausplatz", 20, 0);
            Pruefe.WirftMitMeldung(() => e.HofstelleBilden(h.Kennung, w.Kennung), "kein Werkstattplatz");
            var k = e.HofstelleBilden(w.Kennung, h.Kennung, start: true, name: "am Brunnen");
            Pruefe.Gleich("H1", k);
            Pruefe.WirftMitMeldung(() => e.HofstelleBilden(w.Kennung, h.Kennung), "gehört schon zur Hofstelle H1");
            e.HofstelleStartSetzen("H1", false);
            e.HofstelleUmbenennen("H1", "  Unter Linden ");
            Pruefe.Gleich(new EntwurfHofstelle("H1", "Unter Linden", w.Kennung, h.Kennung, false), e.Start.HofstelleMit("H1"));
            e.HofstelleAufloesen("H1");
            Pruefe.Gleich(0, e.Start.Hofstellen.Count);
            Pruefe.Wahr(e.GebaeudeMit(w.Kennung) is not null && e.GebaeudeMit(h.Kennung) is not null, "Plätze bleiben");
            for (var i = 0; i < 3; i++) e.Rueckgaengig();
            Pruefe.Gleich(new EntwurfHofstelle("H1", "am Brunnen", w.Kennung, h.Kennung, true), e.Start.HofstelleMit("H1"));
            Pruefe.WirftMitMeldung(() => e.HofstelleStartSetzen("X", true), "Keine Hofstelle");
        }),

        ("Werkzeug Hofstelle: Ein gelöschter Platz nimmt die Hofstelle mit, eine neue Kennung zieht den Verweis nach, Hofstelle löschen nimmt beide Plätze – je ein Schritt", () =>
        {
            var e = Neu();
            e.HofstelleSetzen(0, 50);
            e.KennungSetzen("H1_Werkstatt", "Schmiede_Werkstatt");
            Pruefe.Gleich("Schmiede_Werkstatt", e.Start.HofstelleMit("H1")!.Werkstatt);
            e.Rueckgaengig();
            Pruefe.Gleich("H1_Werkstatt", e.Start.HofstelleMit("H1")!.Werkstatt);
            Pruefe.Wahr(e.GebaeudeMit("H1_Werkstatt") is not null, "Kennung zurück");
            e.Loeschen("H1_Wohnhaus");
            Pruefe.Gleich(0, e.Start.Hofstellen.Count);
            Pruefe.Wahr(e.GebaeudeMit("H1_Werkstatt") is not null, "der andere Platz bleibt");
            e.Rueckgaengig();
            Pruefe.Wahr(e.Start.HofstelleMit("H1") is not null && e.GebaeudeMit("H1_Wohnhaus") is not null, "beides zurück");
            var vorher = e.ZuJson();
            e.HofstelleLoeschen("H1");
            Pruefe.Wahr(e.Gebaeude.Count == 0 && e.Start.Hofstellen.Count == 0, "alles weg");
            e.Rueckgaengig();
            Pruefe.Gleich(vorher, e.ZuJson());
        }),

        ("Werkzeug Hofstelle: Spieler und KI-Haus auf eine Hofstelle setzen – Wohnhaus, Werkstatt und Name des Betriebs ziehen mit, ein Schritt; ZuJson und wieder öffnen behält alles", () =>
        {
            var e = Greybox();
            var neu = e.HofstelleSetzen(-60, 70, 0, start: true);
            e.HausHofstelleSetzen(0, neu.Kennung);
            var eisenbart = e.Start.Haeuser[0];
            Pruefe.Gleich(neu.Wohnhaus, eisenbart.Wohnhaus);
            Pruefe.Gleich(neu.Werkstatt, eisenbart.Betriebe[0].Platz);
            Pruefe.Gleich("Grube_Eisenbart", eisenbart.Betriebe[1].Platz); // das Umland bleibt
            Pruefe.Gleich("Gießerei Eisenbart", e.GebaeudeMit(neu.Werkstatt)!.Betrieb);
            Pruefe.Gleich(null, e.GebaeudeMit("Giesserei_Eisenbart")!.Betrieb);
            Pruefe.Wahr(e.FreieHofstellen().Select(h => h.Kennung).SequenceEqual(new[] { "H2" }), "H2 ist jetzt frei");
            e.Rueckgaengig();
            Pruefe.Gleich("Wohnhaus2", e.Start.Haeuser[0].Wohnhaus);
            Pruefe.Gleich("Gießerei Eisenbart", e.GebaeudeMit("Giesserei_Eisenbart")!.Betrieb);
            Pruefe.Gleich(null, e.GebaeudeMit(neu.Werkstatt)!.Betrieb);
            e.SpielerHofstelleSetzen(neu.Kennung);
            Pruefe.Gleich((neu.Wohnhaus, neu.Werkstatt, "{art} am Weinmarkt"), (e.Start.SpielerWohnhaus, e.Start.SpielerWerkstatt!.Platz, e.Start.SpielerWerkstatt.Name));
            var wieder = Kartenentwurf.AusJson(e.ZuJson(), HofstellenTests.Katalog.Value);
            Pruefe.Wahr(wieder.Start.Hofstellen.SequenceEqual(e.Start.Hofstellen), "dieselben Hofstellen");
            Pruefe.Gleich(e.ZuJson(), wieder.ZuJson());
        }),
    };
}
