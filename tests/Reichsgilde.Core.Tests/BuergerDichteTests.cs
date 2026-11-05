using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Mehr Bürger in großen Städten (Spiel-KI, 5.11.2026):
///   • „jeHausplatz“: Mit Karte bekommt jeder Hausplatz für Einwohner (Einwohnerplätze, Kulissenhäuser) einen Haushalt – Köln hat mehr
///     Einwohner als die Greybox. Ohne Karte und ohne das Feld bleibt es bei „jeHaus“ je Haus.
///   • „tagesplan“: Wer tagsüber ruht oder im Dienst arbeitet, verbringt einen Teil der Stunden am Markt. Nur der Ort ändert sich, nie die
///     Handlung, und wer draußen ist, folgt fest aus Nummer, Tag und Doppelstunde (ohne Würfel).
/// </summary>
static class BuergerDichteTests
{
    static readonly Lazy<string> Text = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));

    static Wirtschaftsdaten Daten(Action<JsonObject>? aendern = null)
    {
        var json = JsonNode.Parse(Text.Value)!.AsObject();
        aendern?.Invoke(json["buerger"]!.AsObject());
        return Wirtschaftsdaten.AusJson(json.ToJsonString());
    }

    static void OhneNeues(JsonObject b)
    {
        foreach (var feld in Buergermessung.NeueFelder) b.Remove(feld);
    }

    /// <summary>Eine Partie mit allen KI-Häusern der Karte (Köln: 7, Greybox: 3) – wie im Spiel.</summary>
    static (Simulation Sim, Stadtwirtschaft W) Partie(Wirtschaftsdaten daten, string? karte, int startwert = 1400)
    {
        var rivalen = karte is null ? 0 : JsonNode.Parse(File.ReadAllText(Datenpfad.Karte(karte)))!["start"]!["haeuser"]!.AsArray().Count;
        var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos },
            daten, rivalen > 0 ? new NeuePartie(Rivalen: rivalen) : new NeuePartie(), karte: karte is null ? null : Stadtkarte.AusDatei(Datenpfad.Karte(karte)));
        sim.Tick(); // die Einwohner ziehen ein
        return (sim, sim.Wirtschaft!);
    }

    static int Gewoehnliche(Stadtwirtschaft w) => w.Einwohnerschaft.Einwohner.Count(e => e.Herkunft == Herkunft.Stadt);

    static void Bis(Simulation sim, int uhrzeit)
    {
        while (sim.Zeit.UhrzeitInMinuten != uhrzeit) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Bürgerdichte: Ohne Karte bleibt es bei jeHaus × Häuser, auch mit „jeHausplatz“ in den Daten", () =>
        {
            var (_, w) = Partie(Daten(), karte: null);
            Pruefe.Wahr(w.Einwohnerschaft.Daten.JeHausplatz > 0, "Die Daten sollten jeHausplatz setzen");
            Pruefe.Gleich(0, w.Einwohnerschaft.Hausplaetze);
            Pruefe.Gleich(w.Einwohnerschaft.Daten.JeHaus * w.Konten.Count, Gewoehnliche(w));
        }),

        ("Bürgerdichte: Ohne „jeHausplatz“ (wie bis Datenversion 72) zählt auch mit Karte nur jeHaus × Häuser (Köln: 8 Häuser, 200 Einwohner)", () =>
        {
            var (_, w) = Partie(Daten(OhneNeues), "koeln");
            Pruefe.Gleich(8, w.Konten.Count);
            Pruefe.Gleich(200, Gewoehnliche(w));
        }),

        ("Bürgerdichte: Mit Karte hat jeder Hausplatz einen Haushalt – Köln (82 Plätze) etwa doppelt so viele Einwohner wie vorher und mehr als doppelt so viele wie die Greybox", () =>
        {
            var (_, koeln) = Partie(Daten(), "koeln");
            var (_, greybox) = Partie(Daten(), "greybox");
            var d = koeln.Einwohnerschaft.Daten;
            Pruefe.Gleich(82, koeln.Einwohnerschaft.Hausplaetze, "Köln: 68 Einwohnerplätze und 14 Kulissenhäuser");
            Pruefe.Gleich(40, greybox.Einwohnerschaft.Hausplaetze, "Greybox: 36 Einwohnerplätze und 4 Kulissenhäuser");
            var frei = 82 - d.Buergerhaeuser - koeln.Rat.Ratsherren.Count;
            var n = Gewoehnliche(koeln);
            Pruefe.Wahr(n >= (d.JeHausplatz - 1) * frei && n <= (d.JeHausplatz + 1) * frei,
                        $"Köln: {n} Einwohner für {frei} freie Plätze zu je {d.JeHausplatz} ± 1");
            Pruefe.Wahr(n >= 2 * 200 * 0.9, $"Köln sollte etwa doppelt so viele gewöhnliche Einwohner haben wie vorher (200), hat {n}");
            Pruefe.Wahr(n > 2 * Gewoehnliche(greybox), $"Köln ({n}) sollte mehr als doppelt so viele haben wie die Greybox ({Gewoehnliche(greybox)})");
        }),

        ("Wirtschaftsgewicht: Für die Wirtschaft zählen genau jeHaus × Häuser gewöhnliche Einwohner, über alle Haushalte verteilt; Ratsherren und Kandidaten immer; ohne Karte alle", () =>
        {
            foreach (var karte in new[] { "koeln", "greybox" })
            {
                var (sim, w) = Partie(Daten(), karte);
                var ew = w.Einwohnerschaft;
                var stadt = ew.Einwohner.Where(e => e.Herkunft == Herkunft.Stadt).ToList();
                Pruefe.Gleich(ew.Daten.JeHaus * w.Konten.Count, stadt.Count(e => e.ZaehltFuerWirtschaft), karte);
                Pruefe.Wahr(ew.Einwohner.Where(e => e.Herkunft != Herkunft.Stadt).All(e => e.ZaehltFuerWirtschaft), $"{karte}: Ratsherren und Kandidaten zählen");
                var haeuserMit = stadt.Where(e => e.ZaehltFuerWirtschaft).Select(e => e.Wohnhaus).Distinct().Count();
                Pruefe.Wahr(haeuserMit >= ew.Wohnhaeuser.Count(h => h.Art == Hausart.Wohnhaus) * 0.9, $"{karte}: Zählende in fast allen Häusern ({haeuserMit})");
                // Nach Jahren mit Sterben und Zuzug bleibt die Zahl
                for (var i = 0; i < 3 * Spielzeit.MinutenProTag; i++) sim.Tick();
                Pruefe.Gleich(ew.Daten.JeHaus * w.Konten.Count, ew.Einwohner.Count(e => e.Herkunft == Herkunft.Stadt && e.ZaehltFuerWirtschaft), $"{karte} nach drei Jahren");
                // Zählungen der Kopplung nur über die Zählenden
                Pruefe.Gleich(ew.Einwohner.Count(e => e.ZaehltFuerWirtschaft && e.Handlung == Handlung.Einkaufen), ew.Einkaufende, $"{karte}: Einkaufende");
            }
            var (_, ohne) = Partie(Daten(), karte: null);
            Pruefe.Wahr(ohne.Einwohnerschaft.Einwohner.All(e => e.ZaehltFuerWirtschaft), "Ohne Karte zählen alle");
        }),

        ("Bürgerdichte: Je freiem Hausplatz genau ein einfaches Wohnhaus mit jeHausplatz ± 1 Bewohnern (Köln, Greybox)", () =>
        {
            foreach (var karte in new[] { "koeln", "greybox" })
            {
                var (_, w) = Partie(Daten(), karte);
                var ew = w.Einwohnerschaft;
                var einfach = ew.Wohnhaeuser.Count(h => h.Art == Hausart.Wohnhaus);
                var gross = ew.Wohnhaeuser.Count - einfach;
                var frei = ew.Hausplaetze - gross;
                Pruefe.Gleich(frei, einfach, $"{karte}: ein einfaches Wohnhaus je freiem Hausplatz");
                foreach (var h in ew.Wohnhaeuser.Where(h => h.Art == Hausart.Wohnhaus))
                {
                    var bewohner = ew.Bewohner(h.Nummer).Count(e => e.Herkunft == Herkunft.Stadt);
                    Pruefe.Wahr(Math.Abs(bewohner - ew.Daten.JeHausplatz) <= 1, $"{karte}: Haus {h.Nummer} mit {bewohner} Bewohnern");
                }
            }
        }),

        ("Tagesplan: Ändert nur den Ort, nie die Handlung (erster Tag ohne Rauschen gleich), und nur Heim → Markt", () =>
        {
            void OhneZufall(JsonObject b)
            {
                b["rauschen"] = 0;
                b["startStreuung"] = 0;
            }
            var (simA, a) = Partie(Daten(b => { OhneZufall(b); b.Remove("tagesplan"); }), "koeln");
            var (simB, b2) = Partie(Daten(OhneZufall), "koeln");
            var verschoben = 0;
            for (var stunde = 5; stunde <= 22; stunde++)
            {
                Bis(simA, stunde * 60 + 1);
                Bis(simB, stunde * 60 + 1);
                var ea = a.Einwohnerschaft.Einwohner;
                var eb = b2.Einwohnerschaft.Einwohner;
                Pruefe.Gleich(ea.Count, eb.Count);
                for (var i = 0; i < ea.Count; i++)
                {
                    Pruefe.Gleich(ea[i].Handlung, eb[i].Handlung, $"{stunde} Uhr: Handlung von {ea[i].Name}");
                    if (ea[i].Ort == eb[i].Ort) continue;
                    Pruefe.Gleich(Stadtort.Heim, ea[i].Ort, $"{stunde} Uhr: {ea[i].Name} war nicht daheim");
                    Pruefe.Gleich(Stadtort.Markt, eb[i].Ort, $"{stunde} Uhr: {eb[i].Name} sollte am Markt sein");
                    Pruefe.Wahr(stunde is >= 8 and < 20, $"{stunde} Uhr: draußen nur zwischen 8 und 20 Uhr");
                    verschoben++;
                }
            }
            Pruefe.Wahr(verschoben > 500, $"Es sollten viele Stunden draußen verbracht werden (waren {verschoben})");
        }),

        ("Tagesplan: Tagsüber deutlich mehr am Markt, ab 20 Uhr rastet niemand mehr draußen (Köln, drei Tage)", () =>
        {
            var (simA, a) = Partie(Daten(b => b.Remove("tagesplan")), "koeln");
            var (simB, b2) = Partie(Daten(), "koeln");
            double amMarktA = 0, amMarktB = 0;
            for (var tag = 0; tag < 3; tag++)
                foreach (var stunde in new[] { 10, 15, 18, 20, 21 })
                {
                    var minute = stunde * 60 + 1;
                    Bis(simA, minute);
                    Bis(simB, minute);
                    var ruhendDraussen = b2.Einwohnerschaft.Einwohner.Count(e => e.Handlung == Handlung.Ruhen && e.Ort != Stadtort.Heim);
                    if (stunde >= 20)
                    {
                        Pruefe.Gleich(0, ruhendDraussen, $"Tag {tag + 1}, {stunde} Uhr: draußen Rastende");
                        continue;
                    }
                    amMarktA += a.Einwohnerschaft.Einwohner.Count(e => e.Ort == Stadtort.Markt);
                    amMarktB += b2.Einwohnerschaft.Einwohner.Count(e => e.Ort == Stadtort.Markt);
                }
            Pruefe.Wahr(amMarktB > 1.5 * amMarktA, $"Mit Tagesplan sollten tagsüber viel mehr am Markt sein ({amMarktB} gegen {amMarktA})");
        }),

        ("Bürgerdichte und Tagesplan berühren die Wirtschaft nicht – Köln und Greybox, zwei Jahre mit und ohne: Vermögen, Nachfrage, Passanten gleich", () =>
        {
            foreach (var karte in new[] { "koeln", "greybox" })
            {
                var (simA, a) = Partie(Daten(OhneNeues), karte, startwert: 4711);
                var (simB, b2) = Partie(Daten(), karte, startwert: 4711);
                Pruefe.Wahr(b2.Einwohnerschaft.Einwohner.Count > a.Einwohnerschaft.Einwohner.Count + 40, $"{karte}: mit Bürgerdichte mehr Einwohner");
                var anders = 0;
                for (var minute = 0; minute < 2 * Spielzeit.MinutenProTag; minute++)
                {
                    simA.Tick();
                    simB.Tick();
                    if (simA.Zeit.UhrzeitInMinuten % 60 != 1) continue;
                    Pruefe.Gleich(a.Einwohnerschaft.Passanten.Count(), b2.Einwohnerschaft.Passanten.Count(), $"{karte}: Passanten um {simA.Zeit.UhrzeitText}");
                    Pruefe.Gleich(a.Einwohnerschaft.Marktfaktor, b2.Einwohnerschaft.Marktfaktor, $"{karte}: Marktfaktor um {simA.Zeit.UhrzeitText}");
                    Pruefe.Gleich(a.Einwohnerschaft.Einkaufende, b2.Einwohnerschaft.Einkaufende, $"{karte}: Einkaufende um {simA.Zeit.UhrzeitText}");
                    var zaehlend = b2.Einwohnerschaft.Einwohner.Where(e => e.ZaehltFuerWirtschaft).ToList();
                    Pruefe.Gleich(a.Einwohnerschaft.Einwohner.Count, zaehlend.Count, $"{karte}: Zählende");
                    for (var i = 0; i < zaehlend.Count; i++)
                    {
                        Pruefe.Gleich(a.Einwohnerschaft.Einwohner[i].Name, zaehlend[i].Name, $"{karte}: Name");
                        Pruefe.Gleich(a.Einwohnerschaft.Einwohner[i].Handlung, zaehlend[i].Handlung, $"{karte}: Handlung von {zaehlend[i].Name}");
                        if (a.Einwohnerschaft.Einwohner[i].Ort != zaehlend[i].Ort) anders++;
                    }
                }
                Pruefe.Wahr(anders > 100, $"{karte}: Der Tagesplan sollte Orte ändern (waren {anders})");
                for (var i = 0; i < a.Konten.Count; i++)
                    Pruefe.Gleich(a.Konten[i].Vermoegen, b2.Konten[i].Vermoegen, $"{karte}: Vermögen von {a.Konten[i].Inhaber}");
                foreach (var (sa, sb) in a.Markt.Staende.Zip(b2.Markt.Staende))
                    Pruefe.Gleich(sa.Nachfrage, sb.Nachfrage, $"{karte}: Nachfrage {sa.Ware}");
            }
        }),

        ("Tagesplan: Der Wurf ist fest (gleiche Eingaben, gleiche Zahl), liegt in [0, 1) und trifft den Anteil", () =>
        {
            Pruefe.Gleich(Einwohnerschaft.Wurf(17, 1403, 5), Einwohnerschaft.Wurf(17, 1403, 5));
            var treffer = 0;
            var n = 0;
            for (var nummer = 1; nummer <= 400; nummer++)
                for (var tag = 1400; tag < 1405; tag++)
                    for (var stunde = 4; stunde < 12; stunde++)
                    {
                        var x = Einwohnerschaft.Wurf(nummer, tag, stunde);
                        Pruefe.Wahr(x is >= 0 and < 1, $"Wurf {x} außerhalb [0, 1)");
                        if (x < 0.7) treffer++;
                        n++;
                    }
            Pruefe.Wahr(Math.Abs(treffer / (double)n - 0.7) < 0.02, $"Anteil unter 0,7: {treffer / (double)n:0.000}");
        }),

        ("Tagesplan: Spielstand nach dem Tagesplan geladen läuft gleich weiter (Köln, Mittag)", () =>
        {
            var (sim, w) = Partie(Daten(), "koeln");
            Bis(sim, 12 * 60 + 30);
            var (geladen, _) = Reichsgilde.Core.Speichern.Speicherung.Laden(Reichsgilde.Core.Speichern.Speicherung.Speichern(sim, w.Konten[0]),
                Stadtkarte.AusDatei(Datenpfad.Karte("koeln")));
            Bis(sim, 16 * 60 + 1);
            Bis(geladen, 16 * 60 + 1);
            var a = w.Einwohnerschaft.Einwohner;
            var b = geladen.Wirtschaft!.Einwohnerschaft.Einwohner;
            Pruefe.Gleich(a.Count, b.Count);
            for (var i = 0; i < a.Count; i++)
                Pruefe.Gleich(a[i].Aufenthalt, b[i].Aufenthalt, $"Aufenthalt von {a[i].Name}");
        }),
    };
}
