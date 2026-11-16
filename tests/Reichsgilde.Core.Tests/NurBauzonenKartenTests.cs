using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Kartenbau;

// ─────────────────────────────────────────────────────────────────────────────
//  „Nur noch Bauzonen“, Teil 3 (Karten, 16.11.2026): Die Bauzonen der Karten decken die Flächen der alten Werkstatt- und Wohnhausplätze
//  mit ab (Rasterbauzonen, Kartenprüfung), das Kartenskript berichtet die Startorte in den Bauzonen statt der Hofstellen, und die
//  Probepartie der Kartenwerkstatt beginnt mit der Platzwahl wie eine Partie aus dem Menü.
// ─────────────────────────────────────────────────────────────────────────────

static class NurBauzonenKartenTests
{
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Path.GetDirectoryName(Datenpfad.Wirtschaft())!, "katalog.json")));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Kartenentwurf Oeffnen(string kennung)
    {
        var e = Kartenentwurf.AusJson(File.ReadAllText(Datenpfad.Karte(kennung)), Katalog.Value);
        e.Daten = Daten.Value;
        return e;
    }

    public static IEnumerable<(string, Action)> Alle()
    {
        yield return ("Nur Bauzonen, Karten: Keine Karte führt noch Werkstatt- oder Wohnhausplätze; wo sie lagen, ist Bauzone, und die Prüfung hat keinen Fehler", () =>
        {
            // je Karte zwei Stellen, an denen bis zum 16.11.2026 ein Werkstattplatz lag: Sie liegen in einer Bauzone
            var frueher = new Dictionary<string, Kartenort[]>
            {
                ["greybox"] = new[] { new Kartenort(-23, 16), new Kartenort(22, 13) }, ["probestadt"] = new[] { new Kartenort(-30, -10), new Kartenort(32, 10) },
                ["koeln"] = new[] { new Kartenort(26.5, 79), new Kartenort(8.5, -84.5) }, ["messstadt"] = new[] { new Kartenort(-24, -10), new Kartenort(60, -10) },
            };
            foreach (var kennung in new[] { "greybox", "probestadt", "koeln", "messstadt" })
            {
                var e = Oeffnen(kennung);
                Pruefe.Gleich(0, e.Gebaeude.Count(g => g.Art is Bausteinart.Werkstattplatz or Bausteinart.Wohnhausplatz), kennung + ": keine Plätze mehr");
                foreach (var ort in frueher[kennung])
                    Pruefe.Wahr(e.Bauzonen.Any(z => Kartengeometrie.ImVieleck(ort, z.Ecken)), $"{kennung}: {ort} liegt in keiner Bauzone.");
                Pruefe.Wahr(!e.Befunde.Any(b => b.Schwere == Schwere.Fehler), kennung + ": " + string.Join(" / ", e.Befunde.Where(b => b.Schwere == Schwere.Fehler)));
            }
        });

        yield return ("Nur Bauzonen, Rasterrechnung: Ein Werkstattplatz sperrt nichts – seine Fläche gehört zur Zone, die Prüfung meldet keinen Abstand; ein festes Gebäude sperrt weiter, und auf dem Platz entsteht keine Restfläche", () =>
        {
            Kartenort O(double x, double z) => new(x, z);
            Kartenentwurf Karte(string baustein)
            {
                var e = Kartenentwurf.Neu("platzprobe", "Platzprobe", 400, 300, Katalog.Value);
                e.WegAnlegen(Wegklasse.Land, new[] { O(-150, 0), O(150, 0) }, glatt: false, name: "Landstraße");
                e.Setzen(baustein, 20, -12);
                e.LagenSetzen(new[] { new Lagegebiet(Lage.Kern, new[] { O(0, -40), O(60, -40), O(60, 40), O(0, 40) }, "Kern") });
                return e;
            }
            var mitPlatz = Karte("werkstattplatz");
            var r = mitPlatz.BauzonenAusRaster();
            var platz = mitPlatz.Gebaeude[0];
            Pruefe.Wahr(mitPlatz.Bauzonen.Any(z => Kartengeometrie.ImVieleck(platz.Punkt, z.Ecken)), "Die Mitte des Platzes liegt in einer Zone.");
            Pruefe.Wahr(!mitPlatz.Befunde.Any(b => b.Regel == "zone-gebaeude"), "kein Befund zone-gebaeude: " + string.Join(" / ", mitPlatz.Befunde));
            Pruefe.Wahr(!r.Restflaechen.Any(x => Kartengeometrie.ImVieleck(platz.Punkt, x.Flaeche.Select(Kartenort.Aus).ToList())), "keine Restfläche auf dem Platz");

            var mitHaus = Karte("leerstand");
            mitHaus.BauzonenAusRaster();
            Pruefe.Wahr(!mitHaus.Bauzonen.Any(z => Kartengeometrie.ImVieleck(mitHaus.Gebaeude[0].Punkt, z.Ecken)), "Ein festes Gebäude bleibt ausgespart.");
        });

        yield return ("Nur Bauzonen, Kartenskript: pruefen berichtet die Startorte im Stadtinneren – Probestadt, Köln und Messstadt 8 Häuser, Greybox 5 mit Hinweis – und keine Hofstellen mehr", () =>
        {
            foreach (var (kennung, haeuser) in new[] { ("greybox", 5), ("probestadt", 8), ("koeln", 8), ("messstadt", 8) })
            {
                var aus = new StringWriter();
                var e = Oeffnen(kennung);
                Pruefe.Gleich(haeuser, Befehle.StartortBericht(e, aus) ?? -1, kennung);
                var text = aus.ToString();
                Pruefe.Wahr(text.Contains($"Platz für {haeuser} von 8 Häusern"), text);
                Pruefe.Gleich(haeuser < 8, text.Contains("Hinweis: Die Bauzonen im Stadtinneren reichen nur für"), kennung + ": Hinweis nur unter 8 Häusern.\n" + text);
                var bericht = new StringWriter();
                Befehle.Bericht(e, bericht);
                Pruefe.Wahr(bericht.ToString().Contains("Startorte im Stadtinneren") && !bericht.ToString().Contains("   Hofstellen: "), bericht.ToString());
            }
        });

        yield return ("Nur Bauzonen, Probepartie: mit Startwahl setzt der Spieler Wohnhaus und Werkstatt selbst, die KI-Häuser stehen gelost in den Zonen; ohne bleibt die Probe wie bisher", () =>
        {
            var (sim, spieler) = Oeffnen("greybox").Ausprobieren(Daten.Value, "Schmied", startwahl: true);
            var w = sim.Wirtschaft!;
            Pruefe.Wahr(w.Bauen.StartwahlVon(spieler) is { WohnhausOffen: true }, "Die Platzwahl ist offen, zuerst das Wohnhaus.");
            Pruefe.Wahr(!w.Betriebe.Any(b => b.Konto == spieler), "Der Spieler hat noch keinen Betrieb.");
            Pruefe.Gleich(4, w.Konten.Count, "Spieler und die drei KI-Häuser der Greybox");
            foreach (var ki in w.Konten.Where(k => k != spieler))
                Pruefe.Wahr(w.Bauen.Grundstuecke.Any(g => g.Besitzer == ki && g.Art == Grundstuecksart.Wohnhaus && g.Kennung is null),
                    $"{ki.Inhaber}: Wohnhaus auf einem gelosten Grundstück (kein Platz der Karte).");

            var (alt, spielerAlt) = Oeffnen("greybox").Ausprobieren(Daten.Value, "Schmied");
            Pruefe.Wahr(alt.Wirtschaft!.Bauen.StartwahlVon(spielerAlt) is null, "Ohne Startwahl keine Platzwahl.");
            Pruefe.Wahr(alt.Wirtschaft!.Betriebe.Any(b => b.Konto == spielerAlt), "Ohne Startwahl hat der Spieler seine Werkstatt.");
        });
    }
}
