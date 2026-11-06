using Reichsgilde;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests der Meldungsordnung (Benachrichtigungen Stufe 1, <c>game/scripts/Meldungsordnung.cs</c>, hier mitübersetzt): Die Datei
/// <c>game/daten/meldungen.json</c> ist stimmig, die Textmuster treffen die Wortlaute einer echten Partie wie die Messung (<c>messen meldungen</c>),
/// Rivalenzüge, fremde Unterwelt und fremde Spitzelberichte kommen nie in die Ecke, Stufen lassen sich überschreiben.
/// </summary>
static class MeldungsordnungTests
{

    static readonly Lazy<Meldungsordnung> Ordnung = new(() => Meldungsordnung.AusJson(File.ReadAllText(
        Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!, "game", "daten", "meldungen.json"))));

    /// <summary>Drei Jahre Köln mit KI-Häusern (wie die Messung); jede Meldung wird im Augenblick der Meldung eingestuft (wie die Oberfläche, mit der Meldung des Kerns).</summary>
    static List<(string Text, Einstufung E)> Partie()
    {
        var liste = new List<(string, Einstufung)>();
        var angemeldet = false;
        var ergebnis = Messpartie.Spielen(Meldungsmessung.Aufbau("Ausbau", "koeln", 101, 3), new Messquellen(), jeMinute: w =>
        {
            if (angemeldet) return;
            angemeldet = true;
            var spieler = w.Konten[0];
            w.Gemeldet += m => liste.Add((m.Text, Ordnung.Value.Einstufen(w, spieler, m.Text, m))); // mit Art und Haus des Kerns (Stufe 2)
            foreach (var rivale in w.Rivalen)
                foreach (var zug in rivale.Entscheidungen.Reverse()) liste.Add((zug, Ordnung.Value.Einstufen(w, spieler, zug)));
        });
        Pruefe.Wahr(ergebnis.Fehler is null, "Partie ohne Fehler: " + ergebnis.Fehler);
        return liste;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Meldungsordnung: meldungen.json ist stimmig (Arten, Themen, Ziele, Sammelregeln)", () =>
        {
            var o = Ordnung.Value;
            Pruefe.Gleich(o.Arten.Count, o.Arten.Select(a => a.Id).Distinct().Count(), "Ids einmalig");
            foreach (var a in o.Arten)
            {
                Pruefe.Wahr(o.Themen.Any(t => t.Id == a.Thema), $"{a.Id}: Thema {a.Thema} fehlt");
                Pruefe.Wahr(a.Ziel is "" or "familie" or "rat" or "bericht" or "betrieb", $"{a.Id}: Ziel {a.Ziel}");
                Pruefe.Wahr(a.Sammeln is "" or "jahresbericht" or "nacht" or "stillstand", $"{a.Id}: Sammeln {a.Sammeln}");
                Pruefe.Wahr(a.Titel != "" && a.Absender != "", $"{a.Id}: Titel und Absender");
            }
            foreach (var t in o.Themen) Pruefe.Wahr(System.Text.RegularExpressions.Regex.IsMatch(t.Farbe, "^#[0-9a-fA-F]{6}$"), $"Farbe {t.Id}");
            foreach (var id in new[] { "Sonstiges", "KI-Zug", "Fehler" }) Pruefe.Wahr(o.Arten.Any(a => a.Id == id), id);
        }),

        ("Meldungsordnung: Wortlaute (Stillstand, Fehler, Warnzeichen, Nacht, Jahresende, Rivalenzüge) werden eingeordnet", () =>
        {
            var o = Ordnung.Value;
            Pruefe.Gleich("Stillstand", o.Einordnen("Tischlerei Eichwald: Es fehlt Holz – die Arbeit ruht.").Id);
            Pruefe.Gleich("Stillstand", o.Einordnen("Tischlerei Eichwald: Das Lager für Holz ist voll – die Arbeit ruht.").Id);
            Pruefe.Gleich("Blitz", o.Einordnen("⚠ Blitzschlag! Das Haus brennt.").Id);
            Pruefe.Gleich("Blitz", o.Einordnen("Schmiede: Nach dem Brand wird repariert – die Arbeit ruht.").Id, "Brand vor Stillstand");
            Pruefe.Gleich("Fehler", o.Einordnen("✗ Dafür fehlt das Geld.").Id);
            Pruefe.Gleich("Tod", o.Einordnen("Trauer im Haus Brendel: Anna ist gestorben.").Id);
            Pruefe.Gleich("Diebstahl", o.Einordnen("Einbruch ins Fachwerkhaus von Haus Brendel: 40 Gulden aus der Truhe gestohlen.").Id);
            Pruefe.Gleich("Diebstahl", o.Einordnen("Überfall auf dem Weg von Schmiede zum Markt: Die Eskorte schlägt eine Bande „Wegelagerer“ (4 Menschen) in die Flucht.").Id);
            Pruefe.Gleich("Steuer", o.Einordnen("Ratssitzung 1403: Antrag von Haus Brendel – Steuer auf 12 % – ANGENOMMEN (3 dafür, 1 dagegen).").Id);
            Pruefe.Gleich("Rat", o.Einordnen("Wahl 1403 – Marktmeister: Haus Brendel bleibt im Amt.").Id);
            Pruefe.Gleich("Jahreszeit", o.Einordnen("Frühling 1403: die Erdwege sind aufgeweicht.").Id);
            Pruefe.Gleich("Sonstiges", o.Einordnen("Jahresabrechnung 1402 abgeschlossen.").Id);
            Pruefe.Gleich("Tischlerei Eichwald", Meldungsordnung.Stillstandsbetrieb("Tischlerei Eichwald: Es fehlt Holz – die Arbeit ruht.")!);
            Pruefe.Wahr(Meldungsordnung.IstMangel("Es fehlt Holz – die Arbeit ruht."), "Mangel");
            Pruefe.Wahr(!Meldungsordnung.IstMangel("Umbau (Stufe 2: Schmiede) – die Arbeit ruht."), "Umbau ist kein Mangel");
            Pruefe.Gleich("Haus Brendel", Meldungsordnung.Auftraggeber("Der Spitzel von Haus Brendel berichtet über Haus Eisenbart: …")!);
        }),

        ("Meldungsordnung: Stufen lassen sich überschreiben, Fremdes bleibt im Verlauf, abgewehrte Überfälle sind höchstens ein Siegel", () =>
        {
            var o = Ordnung.Value;
            var tod = o.Art("Tod");
            Pruefe.Gleich(Meldestufe.Brief, Meldungsordnung.Stufe(tod, Betroffenheit.Eigen, "Trauer im Haus X", null));
            Pruefe.Gleich(Meldestufe.Siegel, Meldungsordnung.Stufe(tod, Betroffenheit.Eigen, "Trauer im Haus X", Meldestufe.Siegel));
            Pruefe.Gleich(Meldestufe.Aus, Meldungsordnung.Stufe(tod, Betroffenheit.Eigen, "Trauer im Haus X", Meldestufe.Aus));
            Pruefe.Gleich(Meldestufe.Verlauf, Meldungsordnung.Stufe(tod, Betroffenheit.Fremd, "Trauer im Haus X", Meldestufe.Brief), "Fremdes höchstens Verlauf");
            var dieb = o.Art("Diebstahl");
            Pruefe.Gleich(Meldestufe.Brief, Meldungsordnung.Stufe(dieb, Betroffenheit.Eigen, "Einbruch ins Fachwerkhaus von X: 40 Gulden gestohlen.", null));
            Pruefe.Gleich(Meldestufe.Siegel, Meldungsordnung.Stufe(dieb, Betroffenheit.Eigen, "Überfall auf dem Weg: Die Eskorte schlägt eine Bande in die Flucht.", null));
            Pruefe.Gleich(Meldestufe.Verlauf, Meldungsordnung.Stufe(o.Art("KI-Zug"), Betroffenheit.Fremd, "Haus X: kauft", null));
        }),

        ("Meldungsordnung: echte Partie – gleiche Arten wie die Messung, Rivalen nur im Verlauf, fremde Geheimnisse verworfen, wenige Siegel und Briefe", () =>
        {
            var liste = Partie();
            Pruefe.Wahr(liste.Count > 100, $"genug Meldungen ({liste.Count})");
            var rivalen = liste.Where(x => x.E.RivalenZug).ToList();
            Pruefe.Wahr(rivalen.Count > 20, $"Züge der Rivalen erkannt ({rivalen.Count})");
            foreach (var (text, e) in liste)
            {
                if (!e.RivalenZug) Pruefe.Gleich(Meldungsmessung.Einordnen(text), e.Art.Id, "Art wie die Messung: " + text);
                var stufe = Meldungsordnung.Stufe(e.Art, e.Bezug, e.Text, null);
                if (e.RivalenZug) Pruefe.Gleich(Meldestufe.Verlauf, stufe, "Rivalenzug");
                if (e.Verwerfen) Pruefe.Wahr(e.Art.Id is "Diebstahl" or "Spitzel", "Verworfen nur Unterwelt und Spitzel: " + text);
            }
            var sonstiges = liste.Count(x => x.E.Art.Id == "Sonstiges");
            Pruefe.Wahr(sonstiges <= liste.Count / 50, $"höchstens 2 % Sonstiges ({sonstiges} von {liste.Count})");
            // Gemessen: etwa 9 Siegel und Briefe je Jahr; hier großzügig (3 Jahre, ein Lauf)
            var sichtbar = liste.Count(x => !x.E.Verwerfen && Meldungsordnung.Stufe(x.E.Art, x.E.Bezug, x.E.Text, null) >= Meldestufe.Siegel);
            Pruefe.Wahr(sichtbar <= 3 * 40, $"Siegel und Briefe in 3 Jahren ({sichtbar}) nicht zu viele");
        }),
    };
}
