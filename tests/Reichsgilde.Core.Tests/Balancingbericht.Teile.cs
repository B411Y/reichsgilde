using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

// Balancing-Bericht, Abschnitte 3 bis 7 (Aufruf und Abschnitte 1, 2 in Balancingbericht.cs).

static partial class Balancingbericht
{
    private static List<Partieergebnis> Gut(IEnumerable<Partieergebnis> laeufe) => laeufe.Where(r => r.Fehler is null).ToList();

    private static JsonObject Staende(IEnumerable<Hausverlauf> haeuser, Wirtschaftsdaten daten) =>
        new(haeuser.GroupBy(h => h.Jahre[^1].Stand).OrderBy(g => g.Key)
            .Select(g => new KeyValuePair<string, JsonNode?>(daten.Staende[Math.Clamp(g.Key, 0, daten.Staende.Count - 1)].Name, g.Count())));

    // ═════════════════════════════════════════════════════════════════════════
    //  3 Startfairness
    // ═════════════════════════════════════════════════════════════════════════

    private static JsonObject Startfairness(Einstellung e, Messquellen q, Wirtschaftsdaten daten, Func<Messaufbau, Partieergebnis> p, List<string> befunde)
    {
        var karten = new JsonArray();
        var letztes = e.Stich[^1];
        foreach (var (karte, anzahl) in Fairnesskarten(e))
        {
            var laeufe = Gut(e.Werte.SelectMany(s => Enumerable.Range(0, anzahl + 1).Select(r => p(new Messaufbau(karte, s, e.Jahre, Rotation: r)))));
            if (laeufe.Count == 0) continue;
            var start = new JsonArray(laeufe[0].Haeuser.Select(h => (JsonNode)new JsonObject
            {
                ["haus"] = h.IstSpieler ? "Spieler" : h.Name, ["volk"] = h.Volk.ToString(), ["beruf"] = h.Startberuf, ["startvermoegen"] = R(h.StartBar),
                ["betriebe"] = h.StartBetriebe, ["arbeiter"] = h.StartArbeiter, ["erwarteterJahresgewinn"] = R(h.StartGewinnErwartet),
            }).ToArray());
            var alle = laeufe.SelectMany(r => r.Haeuser).ToList();
            // Mittel je Haus und Jahr: Bezug für „relativ“ (Platz gegen das Mittel desselben Hauses)
            var hausMittel = alle.GroupBy(h => (h.IstSpieler, h.Name)).ToDictionary(g => g.Key, g => e.Stich.ToDictionary(j => j, j => g.Average(h => h.Im(j).Rein)));
            var jeHaus = new JsonArray(alle.GroupBy(h => (h.IstSpieler, h.Name)).OrderBy(g => g.Key.IstSpieler ? 0 : 1).Select(g => (JsonNode)new JsonObject
            {
                ["haus"] = g.Key.IstSpieler ? "Spieler (ein Betrieb, KI geführt)" : g.Key.Name, ["volk"] = g.First().Volk.ToString(), ["beruf"] = g.First().Startberuf,
                ["n"] = g.Count(),
                ["jahre"] = JeJahr(e.Stich, j => Kennzahl(g.Select(h => h.Im(j).Rein))),
                ["standAmEnde"] = Staende(g, daten), ["bankrott"] = g.Count(h => h.BankrottJahr is not null), ["erloschen"] = g.Count(h => h.ErloschenJahr is not null),
            }).ToArray());
            var jePlatz = new JsonArray(alle.Where(h => h.Wohnhausplatz is not null).GroupBy(h => h.Wohnhausplatz!).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => (JsonNode)new JsonObject
                {
                    ["wohnhausplatz"] = g.Key, ["n"] = g.Count(),
                    ["jahre"] = JeJahr(e.Stich, j => new JsonObject
                    {
                        ["reinvermoegen"] = Kennzahl(g.Select(h => h.Im(j).Rein)),
                        ["gegenHausmittelProzent"] = Kennzahl(g.Select(h => Abstand(h.Im(j).Rein, hausMittel[(h.IstSpieler, h.Name)][j])), 1),
                    }),
                    ["spielerReinvermoegen"] = JeJahr(e.Stich, j => Kennzahl(g.Where(h => h.IstSpieler).Select(h => h.Im(j).Rein))),
                }).ToArray());
            var spieler = laeufe.Select(r => r.Spieler).ToList();
            var buerger = spieler.Where(h => h.BuergerReinJahr is not null).Select(h => (double)h.BuergerReinJahr!.Value).ToList();
            karten.Add(new JsonObject
            {
                ["karte"] = karte, ["rotationen"] = anzahl + 1, ["n"] = laeufe.Count, ["start"] = start, ["jeHaus"] = jeHaus, ["jeWohnhausplatz"] = jePlatz,
                ["spielerJahreBisBuerger"] = Kennzahl(buerger, 1), ["spielerBuergerErreicht"] = buerger.Count,
                ["spielerBuergerIn8bis12"] = buerger.Count(x => x is >= 8 and <= 12),
            });

            var platzSpanne = jePlatz.Select(x => (double)x!["jahre"]![letztes.ToString()]!["gegenHausmittelProzent"]!["mittel"]!).ToList();
            var spielerRein = hausMittel[(true, laeufe[0].Spieler.Name)][letztes];
            var kiRein = hausMittel.Where(x => !x.Key.IstSpieler).Average(x => x.Value[letztes]);
            befunde.Add($"Startfairness {karte}: Wohnhausplatz verschiebt das Vermögen nach {letztes} Jahren um {Vz(platzSpanne.Min())} … {Vz(platzSpanne.Max())} % " +
                        $"(gegen das Mittel desselben Hauses, {anzahl + 1} Rotationen × {e.Startwerte} Startwerte); der Spieler mit einem Betrieb hat " +
                        $"{Text(spielerRein)} Gulden, die KI-Häuser (Werkstatt + Umland, Expansion) im Mittel {Text(kiRein)} ({Vz(Abstand(spielerRein, kiRein))} %). " +
                        $"Bürger (Reinvermögen 10.000): Ø Jahr {Zahl((buerger.Count > 0 ? buerger.Average() : double.NaN), "0.0")}, im Ziel 8–12 Jahre {buerger.Count(x => x is >= 8 and <= 12)}/{spieler.Count}.");
            Console.WriteLine($"3 Startfairness {karte}: Plätze {string.Join(", ", jePlatz.Select(x => $"{x!["wohnhausplatz"]} {Vz((double)x["jahre"]![letztes.ToString()]!["gegenHausmittelProzent"]!["mittel"]!, 1)} %"))}");
        }
        return new JsonObject
        {
            ["erklaerung"] = "Greybox und Probestadt mit allen KI-Häusern der Karte. Der Spieler (Schmied, Mensch) wird von der KI mit genau einem Betrieb geführt. " +
                             "Startfairness: Die Wohnhausplätze von Spieler und KI-Häusern rotieren (Rotation r: der i-te wohnt, wo vorher der (i + r)-te wohnte), " +
                             "jede Rotation mit allen Startwerten – so steht jedes Haus gleich oft auf jedem Platz. Die Werkstattplätze bleiben: Ihre Lage wirkt im " +
                             "Kern nicht (Fahrzeit nach Stadt/Umland, nicht nach Weglänge; Bürger kaufen am Markt). „gegenHausmittelProzent“: Vermögen auf diesem Platz " +
                             "gegen das Mittel desselben Hauses über alle Plätze – trennt Platz und Haus. Start: Startgeld, Betriebe, Arbeiter und erwarteter " +
                             "Jahresgewinn (Kalkulation, mit Völkerbonus) zu Beginn. Jahre bis Bürger: erstes Jahr mit Reinvermögen ≥ 10.000 (Ziel Z2: 8–12).",
            ["karten"] = karten,
        };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  4 Häuser: 1, 3, 5, 8 KI-Häuser in der Messstadt
    // ═════════════════════════════════════════════════════════════════════════

    private static JsonObject Haeuser(Einstellung e, Func<Messaufbau, Partieergebnis> p, List<string> befunde)
    {
        var stufen = new JsonArray();
        var letztes = e.Stich[^1];
        var mittelJeHaus = new Dictionary<int, Dictionary<int, double>>();
        var gewinnJeHaus = new Dictionary<int, Dictionary<int, double>>();
        var preisindex = new Dictionary<int, double>();
        Console.WriteLine($"4 Häuser (Messstadt) – Vermögen je KI-Haus nach {letztes} Jahren:");
        foreach (var n in Hausstufen)
        {
            var laeufe = Gut(e.Werte.Select(s => p(new Messaufbau("messstadt", s, e.Jahre, Bezug: Bezugsart.Geist, Rivalen: n, MarktMessen: true))));
            if (laeufe.Count == 0) continue;
            var ki = laeufe.SelectMany(r => r.Ki).ToList();
            mittelJeHaus[n] = e.Stich.ToDictionary(j => j, j => ki.Average(h => h.Im(j).Rein));
            gewinnJeHaus[n] = e.Stich.ToDictionary(j => j, j => ki.Average(h => h.Im(j).Gewinn));
            var jeHaus = new JsonArray(ki.GroupBy(h => h.Name).Select(g => (JsonNode)new JsonObject
            {
                ["haus"] = g.Key, ["volk"] = g.First().Volk.ToString(), ["beruf"] = g.First().Startberuf, ["n"] = g.Count(),
                ["jahre"] = JeJahr(e.Stich, j => new JsonObject
                {
                    ["reinvermoegen"] = Kennzahl(g.Select(h => h.Im(j).Rein)), ["gewinn"] = Kennzahl(g.Select(h => h.Im(j).Gewinn)),
                }),
                ["bankrott"] = g.Count(h => h.BankrottJahr is not null), ["erloschen"] = g.Count(h => h.ErloschenJahr is not null),
            }).ToArray());

            // Markt im letzten Stichjahr: je Ware Mittel über die Startwerte
            var waren = laeufe.Where(r => r.Markt.ContainsKey(letztes)).SelectMany(r => r.Markt[letztes]).GroupBy(x => x.Ware)
                .Where(g => g.Any(x => x.VerkauftHaeuser > 0.5 || x.Hergestellt.Count > 0)).OrderBy(g => g.Key, StringComparer.Ordinal).ToList();
            var markt = new JsonArray(waren.Select(g =>
            {
                var verkauft = g.Average(x => x.VerkauftHaeuser);
                var hergestellt = g.SelectMany(x => x.Hergestellt).GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.Sum(y => y.Value) / (double)g.Count());
                var summe = hergestellt.Values.Sum();
                return (JsonNode)new JsonObject
                {
                    ["ware"] = g.Key, ["typ"] = g.First().Typ, ["grundpreis"] = R(g.First().Grundpreis, 1),
                    ["preisfaktor"] = R(g.Average(x => x.Preisfaktor), 3), ["nachfrage"] = R(g.Average(x => x.Nachfrage), 1), ["npcAngebot"] = R(g.First().Npc, 1),
                    ["verkauftHaeuser"] = R(verkauft, 1), ["anteilHaeuserAmAngebotProzent"] = R(g.Average(x => x.VerkauftHaeuser / (x.Npc + x.VerkauftHaeuser) * 100), 1),
                    ["hergestellt"] = R(summe, 1),
                    ["anteileJeHausProzent"] = new JsonObject(hergestellt.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal)
                        .Select(x => new KeyValuePair<string, JsonNode?>(x.Key, R(x.Value / summe * 100, 1)))),
                };
            }).ToArray());
            var gewicht = waren.Sum(g => g.Sum(x => x.VerkauftHaeuser));
            preisindex[n] = gewicht > 0 ? waren.Sum(g => g.Sum(x => x.Preisfaktor * x.VerkauftHaeuser)) / gewicht : double.NaN;
            stufen.Add(new JsonObject
            {
                ["haeuser"] = n, ["n"] = laeufe.Count,
                ["alleHaeuser"] = JeJahr(e.Stich, j => new JsonObject
                {
                    ["reinvermoegen"] = Kennzahl(ki.Select(h => h.Im(j).Rein)), ["gewinn"] = Kennzahl(ki.Select(h => h.Im(j).Gewinn)),
                    ["summeReinvermoegen"] = Kennzahl(laeufe.Select(r => r.Ki.Sum(h => h.Im(j).Rein))),
                }),
                ["jeJahrImMittel"] = Zerlegung(ki),
                ["jeHaus"] = jeHaus,
                ["bankrott"] = ki.Count(h => h.BankrottJahr is not null), ["erloschen"] = ki.Count(h => h.ErloschenJahr is not null),
                ["preisindex"] = R(preisindex[n], 3),
                ["markt"] = new JsonObject { ["jahr"] = letztes, ["waren"] = markt },
            });
            Console.WriteLine($"  {n} Häuser: Ø {Text(mittelJeHaus[n][letztes])} je Haus, Gewinn Ø {Text(gewinnJeHaus[n][letztes])}, Preisindex {Zahl(preisindex[n], "0.000")}");
        }
        var druck = new JsonObject();
        if (mittelJeHaus.ContainsKey(1))
            foreach (var n in mittelJeHaus.Keys)
                druck[n.ToString()] = JeJahr(e.Stich, j => new JsonObject
                {
                    ["vermoegenJeHausGegen1Prozent"] = R(Abstand(mittelJeHaus[n][j], mittelJeHaus[1][j]), 1),
                    ["vermoegenJeHausGegen3Prozent"] = mittelJeHaus.ContainsKey(3) ? R(Abstand(mittelJeHaus[n][j], mittelJeHaus[3][j]), 1) : null,
                    ["gewinnJeHausGegen1Prozent"] = R(Abstand(gewinnJeHaus[n][j], gewinnJeHaus[1][j]), 1),
                });
        if (mittelJeHaus.ContainsKey(1) && mittelJeHaus.ContainsKey(8))
            befunde.Add($"8 Häuser (Messstadt, feste Nachfrage): Vermögen je KI-Haus nach {letztes} Jahren {Text(mittelJeHaus[8][letztes])} statt " +
                        $"{Text(mittelJeHaus[1][letztes])} allein ({Vz(Abstand(mittelJeHaus[8][letztes], mittelJeHaus[1][letztes]))} %)" +
                        (mittelJeHaus.ContainsKey(3) ? $" und {Text(mittelJeHaus[3][letztes])} bei 3 Häusern ({Vz(Abstand(mittelJeHaus[8][letztes], mittelJeHaus[3][letztes]))} %)" : "") +
                        $"; Preisindex der verkauften Waren {Zahl(preisindex.GetValueOrDefault(1), "0.00")} → {Zahl(preisindex.GetValueOrDefault(8), "0.00")}.");
        return new JsonObject
        {
            ["erklaerung"] = "Messstadt (tools/Kartenbau/Staedte/Messstadt.cs) mit den ersten n KI-Häusern der Karte (1: Schmied/Zwerg, 3: dazu Tischler/Elb " +
                             "und Steinmetz/Halbling wie in der Greybox, 5: dazu Alchimist/Mensch und Schmied/Mensch, 8: dazu Wirt/Halbling, Tischler/Mensch, " +
                             "Steinmetz/Zwerg), jedes mit Werkstatt und Umlandbetrieb, Profil ausgewogen. Der Spieler entlässt am Start seine Arbeiter und zählt " +
                             "nicht. Markt im letzten Stichjahr: Preisfaktor = Marktpreis ÷ Grundpreis im Mittel der Stunden; verkauft = Verkäufe aller Häuser " +
                             "im Jahr; Anteil der Häuser am Angebot = verkauft ÷ (Grundversorgung + verkauft); Anteile je Haus nach hergestellten Stück. " +
                             "Preisindex = Preisfaktor gewichtet mit den Verkäufen der Häuser. Druck: Vermögen bzw. Gewinn je Haus gegen 1 (und 3) Häuser.",
            ["stufen"] = stufen,
            ["druck"] = druck,
        };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  5 KI-Stärke
    // ═════════════════════════════════════════════════════════════════════════

    private static JsonObject KiStaerke(Einstellung e, Wirtschaftsdaten daten, IReadOnlyDictionary<Messaufbau, Partieergebnis> alle,
        Func<Messaufbau, Partieergebnis> p, List<string> befunde)
    {
        var laeufe = Gut(e.Werte.Select(s => p(new Messaufbau(e.Karte, s, e.Jahre, Bezug: Bezugsart.WieKi))));
        var letztes = e.Stich[^1];
        var ki = laeufe.SelectMany(r => r.Ki).ToList();
        double Verhaeltnis(Partieergebnis r, Hausverlauf h, int j) => r.Spieler.Im(j).Rein > 0 ? h.Im(j).Rein / r.Spieler.Im(j).Rein : double.NaN;
        var verhaeltnisse = laeufe.SelectMany(r => r.Ki.Select(h => Verhaeltnis(r, h, letztes))).Where(x => !double.IsNaN(x)).ToList();
        var rang = laeufe.Select(r => 1.0 + r.Ki.Count(h => h.Im(letztes).Rein > r.Spieler.Im(letztes).Rein)).ToList();
        // Zum Vergleich: der Spieler mit genau einem Betrieb (Abschnitt Berufe, Schmied) – falls gemessen
        var einBetrieb = e.Werte.Select(s => alle.GetValueOrDefault(Beruf(e, s, "Schmied"))).OfType<Partieergebnis>().Where(r => r.Fehler is null).ToList();
        var davon = verhaeltnisse.Count(x => x > 2);
        var abgeschlagen = verhaeltnisse.Count(x => x < 0.5);
        var bankrott = ki.Count(h => h.BankrottJahr is not null);
        var erloschen = ki.Count(h => h.ErloschenJahr is not null);
        befunde.Add($"KI-Stärke ({e.Karte}, {e.Jahre} Jahre): Bezugsspieler mit der Strategie der KI {Text(laeufe.Average(r => r.Spieler.Im(letztes).Rein))} Gulden, " +
                    $"KI-Häuser Ø {Text(ki.Average(h => h.Im(letztes).Rein))} (Verhältnis KI ÷ Bezug Median {Zahl(Quantil(verhaeltnisse.OrderBy(x => x).ToList(), 0.5), "0.00")}); " +
                    $"davongezogen (> 2×) {davon}/{verhaeltnisse.Count}, abgeschlagen (< ½) {abgeschlagen}/{verhaeltnisse.Count}, bankrott {bankrott}, erloschen {erloschen}" +
                    (einBetrieb.Count > 0 ? $"; gegen den Spieler mit einem Betrieb sind die KI-Häuser {Zahl(ki.Average(h => h.Im(letztes).Rein) / einBetrieb.Average(r => r.Spieler.Im(letztes).Rein), "0.0")}× so reich." : "."));
        Console.WriteLine($"5 KI-Stärke: Bezug Ø {Text(laeufe.Average(r => r.Spieler.Im(letztes).Rein))}, KI Ø {Text(ki.Average(h => h.Im(letztes).Rein))}, Rang des Bezugs Ø {Zahl(rang.Average(), "0.0")}");
        return new JsonObject
        {
            ["erklaerung"] = "Der Spieler (Schmied, Mensch) wird von der KI mit demselben Profil wie die KI-Häuser geführt („Bezugsspieler“: gleiche Strategie, " +
                             "keine Einrichtung, keine Prämien, üblicher Lohn – so spielt heute jedes KI-Haus). Unterschied zu den KI-Häusern bleibt nur der Start: " +
                             "Die KI-Häuser beginnen mit Werkstatt und Umlandbetrieb (5 Arbeiter), der Spieler mit einer Werkstatt (2 Gesellen). Verhältnis = " +
                             "Reinvermögen eines KI-Hauses ÷ Reinvermögen des Bezugsspielers derselben Partie; davongezogen > 2, abgeschlagen < 0,5. " +
                             "Rang des Bezugs: 1 = reichstes Haus. Hinweis: Die KI kauft keine Einrichtung, zahlt keine Prämien und nimmt keine Kredite.",
            ["n"] = laeufe.Count,
            ["bezug"] = JeJahr(e.Stich, j => Kennzahl(laeufe.Select(r => r.Spieler.Im(j).Rein))),
            ["bezugStandAmEnde"] = Staende(laeufe.Select(r => r.Spieler), daten),
            ["bezugRang"] = Kennzahl(rang, 2),
            ["bezugBetriebe"] = JeJahr(e.Stich, j => Kennzahl(laeufe.Select(r => (double)r.Spieler.Im(j).Betriebe), 2)),
            ["kiBetriebe"] = JeJahr(e.Stich, j => Kennzahl(ki.Select(h => (double)h.Im(j).Betriebe), 2)),
            ["bezugJeJahrImMittel"] = Zerlegung(laeufe.Select(r => r.Spieler)),
            ["kiJeJahrImMittel"] = Zerlegung(ki),
            ["kiHaeuser"] = JeJahr(e.Stich, j => Kennzahl(ki.Select(h => h.Im(j).Rein))),
            ["jeHaus"] = new JsonArray(ki.GroupBy(h => h.Name).Select(g => (JsonNode)new JsonObject
            {
                ["haus"] = g.Key, ["volk"] = g.First().Volk.ToString(), ["beruf"] = g.First().Startberuf,
                ["jahre"] = JeJahr(e.Stich, j => Kennzahl(g.Select(h => h.Im(j).Rein))),
                ["standAmEnde"] = Staende(g, daten), ["bankrott"] = g.Count(h => h.BankrottJahr is not null), ["erloschen"] = g.Count(h => h.ErloschenJahr is not null),
            }).ToArray()),
            ["verhaeltnisKiZuBezug"] = JeJahr(e.Stich, j => Kennzahl(laeufe.SelectMany(r => r.Ki.Select(h => Verhaeltnis(r, h, j))), 2)),
            ["davongezogen"] = new JsonObject { ["schwelle"] = 2, ["anzahl"] = davon, ["von"] = verhaeltnisse.Count },
            ["abgeschlagen"] = new JsonObject { ["schwelle"] = 0.5, ["anzahl"] = abgeschlagen, ["von"] = verhaeltnisse.Count },
            ["kiBankrott"] = bankrott, ["kiErloschen"] = erloschen, ["kiStandAmEnde"] = Staende(ki, daten),
            ["spielerMitEinemBetrieb"] = einBetrieb.Count == 0 ? null : JeJahr(e.Stich, j => Kennzahl(einBetrieb.Select(r => r.Spieler.Im(j).Rein))),
        };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  6 Einrichtung
    // ═════════════════════════════════════════════════════════════════════════

    private static JsonObject Einrichtung(Einstellung e, Messquellen q, Wirtschaftsdaten daten, IReadOnlyDictionary<Messaufbau, Partieergebnis> alle,
        Func<Messaufbau, Partieergebnis> p, List<string> befunde)
    {
        // Gemessen: Wie oft trifft eine Sabotage (erwartete Erfolge) bzw. wie viel wird gestohlen – je Betrieb und Jahr, mit der Strategie der KI.
        var bezugslaeufe = Gut(e.Werte.Select(s => p(new Messaufbau(e.Karte, s, e.Jahre, Bezug: Bezugsart.WieKi))));
        var betriebsjahre = bezugslaeufe.Sum(r => r.Haeuser.Sum(h => h.Jahre.Sum(x => (double)x.Betriebe)));
        var sabotagen = bezugslaeufe.Sum(r => r.Gauner.GetValueOrDefault("Hinterzimmer: Sabotage")?.Chancen ?? 0) / Math.Max(1, betriebsjahre);
        var raub = bezugslaeufe.Sum(r => r.Haeuser.Sum(h => h.Jahre.Sum(x => x.Raub))) / Math.Max(1, betriebsjahre);

        var (w, mess) = Startmarkt(q, e.Karte);
        var statisch = new JsonArray();
        var probe = 0;
        foreach (var beruf in daten.Berufe.Keys)
        {
            var stufen = Stufen(daten, beruf);
            foreach (var stueck in daten.Werkstatt?.EinrichtungFuer(beruf) ?? Array.Empty<EinrichtungDaten>())
            {
                // ab der Stufe des Stücks – und, wenn es nur einige Waren schneller macht, ab der ersten Stufe mit einer davon (Glasflakons: Duftwerkstatt)
                var art = stufen.Skip(Math.Clamp(stueck.AbStufe, 1, stufen.Count) - 1)
                    .FirstOrDefault(a => stueck.Wirkung.ZeitFaktor >= 1 || daten.Art(a).Produkte.Any(stueck.Wirkung.GiltFuer), stufen[Math.Clamp(stueck.AbStufe, 1, stufen.Count) - 1]);
                var d = daten.Art(art);
                var vorher = new List<string>();
                for (var v = stueck.Voraussetzung; v is not null; v = daten.Werkstatt!.Stueck(beruf, v)?.Voraussetzung) vorher.Insert(0, v);
                Betrieb Probe(IEnumerable<string> einrichtung)
                {
                    var b = w.BetriebEroeffnen($"Probe {++probe}", art, mess);
                    w.Einstellen(b, "Geselle", d.Plaetze);
                    foreach (var k in einrichtung) w.Ausfuehren(mess, new EinrichtungKaufen(b.Name, k));
                    return b;
                }
                var ohne = Probe(vorher);
                var mit = Probe(vorher.Append(stueck.Kennung));
                var minuten = daten.Arbeitsminuten(art);
                var wi = stueck.Wirkung;
                // Ein Stück, das nur einige Waren schneller macht (Blasebalg …), zählt mit der besten dieser Waren
                var waren = d.Produkte.Where(x => wi.ZeitFaktor >= 1 || wi.GiltFuer(x)).ToList();
                if (waren.Count == 0) waren = d.Produkte.ToList(); // Betriebe ohne Ware
                double Wert(Betrieb b) => d.OhneProduktion
                    ? (w.Bank.IstAktiv && w.Bank.IstBankbetrieb(b) ? w.Bank.WertJeMinute(b) : w.WertOhneWare(d))
                    : waren.Max(x => Kalkulation.GewinnJeArbeitsminute(w, x) / b.ZeitFaktor(x));
                double Jahreswert(Betrieb b) => d.Plaetze * minuten * Wert(b);
                double? mehr = null;
                string rechnung;
                if (Math.Abs(Jahreswert(mit) - Jahreswert(ohne)) > 1e-6)
                {
                    mehr = Jahreswert(mit) - Jahreswert(ohne);
                    rechnung = "Plätze × Arbeitsminuten × Wert je Minute mit − ohne das Stück (bestes Produkt" +
                               (waren.Count < d.Produkte.Count ? $" unter den begünstigten Waren: {string.Join(", ", waren)}" : "") +
                               " bzw. Bankgeschäft), Startpreise, Gesellen";
                }
                else if (wi.LohnFaktor < 1)
                {
                    mehr = d.Plaetze * daten.Lohn("Geselle", art) * (1 - wi.LohnFaktor);
                    rechnung = "Plätze × Lohn eines Gesellen × (1 − Lohnfaktor)";
                }
                else if (wi.UnfallFaktor < 1)
                {
                    var gefahr = daten.Werkstatt!.Unfall.Gefahr.GetValueOrDefault(beruf);
                    var stunden = minuten / 60.0;
                    mehr = Jahreswert(ohne) * gefahr * (stunden - 1) / 2 / stunden * (1 - wi.UnfallFaktor);
                    rechnung = "Jahreswert × Unfallgefahr × verlorener Teil des Tages ((Stunden − 1) / 2 / Stunden) × (1 − Unfallfaktor)";
                }
                else if (wi.BrandFaktor < 1)
                {
                    mehr = sabotagen * w.Gauner.Sabotageschaden(ohne) * (1 - wi.BrandFaktor);
                    rechnung = $"gemessene Sabotagen je Betrieb und Jahr ({Zahl(sabotagen, "0.000")}, erwartete Erfolge) × Schaden (Reparatur + Stillstand) × (1 − Brandfaktor)";
                }
                else if (wi.EinbruchFaktor < 1)
                {
                    mehr = raub * (1 - wi.EinbruchFaktor);
                    rechnung = $"gemessener Raub je Betrieb und Jahr ({Zahl(raub, "0.0")} Gulden) × (1 − Einbruchfaktor)";
                }
                else
                    rechnung = Wirkungstext(wi);
                statisch.Add(new JsonObject
                {
                    ["beruf"] = beruf, ["kennung"] = stueck.Kennung, ["name"] = stueck.Name, ["preis"] = R(stueck.Preis), ["abStufe"] = stueck.AbStufe,
                    ["betrieb"] = d.Name, ["mehrgewinnJeJahr"] = mehr is { } m ? R(m) : null,
                    ["amortisationJahre"] = mehr is > 0 ? R(stueck.Preis / mehr.Value, 1) : null, ["rechnung"] = rechnung,
                });
            }
        }

        // Simuliert: alle Ertragsstücke der Stufe 1 am Start gekauft, gegen dieselbe Partie ohne Einrichtung (gleicher Startwert)
        var simuliert = new JsonArray();
        var y = e.Einrichtungsjahre;
        foreach (var beruf in daten.Berufe.Keys)
        {
            var stuecke = Ertragsstuecke(daten, beruf);
            if (stuecke.Count == 0) continue;
            var paare = e.Werte.Select(s => (Mit: alle.GetValueOrDefault(Beruf(e, s, beruf) with { Jahre = y, Einrichtung = string.Join(',', stuecke.Select(x => x.Kennung)) }),
                    Ohne: alle.GetValueOrDefault(Beruf(e, s, beruf))))
                .Where(x => x.Mit is { Fehler: null } && x.Ohne is { Fehler: null }).Select(x => (Mit: x.Mit!, Ohne: x.Ohne!)).ToList();
            if (paare.Count == 0) continue;
            var kosten = stuecke.Sum(x => x.Preis);
            var mehr = paare.Select(x => Enumerable.Range(1, y).Average(j => x.Mit.Spieler.Im(j).Gewinn - x.Ohne.Spieler.Im(j).Gewinn)).ToList();
            simuliert.Add(new JsonObject
            {
                ["beruf"] = beruf, ["stuecke"] = string.Join(", ", stuecke.Select(x => x.Name)), ["kosten"] = R(kosten), ["n"] = paare.Count, ["jahre"] = y,
                ["mehrgewinnJeJahr"] = Kennzahl(mehr),
                ["mehrReinvermoegen"] = JeJahr(Stichjahre(y), j => Kennzahl(paare.Select(x => x.Mit.Spieler.Im(j).Rein - x.Ohne.Spieler.Im(j).Rein))),
                ["amortisationJahre"] = mehr.Average() > 0 ? R(kosten / mehr.Average(), 1) : null,
            });
        }
        var lange = statisch.Select(x => x!.AsObject()).Where(x => x["amortisationJahre"] is { } a && (double)a! > 5)
            .Select(x => $"{x["beruf"]}/{x["name"]} {Zahl((double)x["amortisationJahre"]!, "0")} J.").ToList();
        var ohneGulden = statisch.Count(x => x!["mehrgewinnJeJahr"] is null);
        befunde.Add($"Einrichtung statisch: {statisch.Count} Stücke, {ohneGulden} ohne direkte Gulden-Wirkung; Amortisation über 5 Jahre: " +
                    (lange.Count == 0 ? "keine." : string.Join(", ", lange.Take(12)) + (lange.Count > 12 ? $" … ({lange.Count})." : ".")));
        foreach (var z in simuliert.Select(x => x!.AsObject()))
            Console.WriteLine($"6 Einrichtung {z["beruf"],-10} {z["stuecke"],-50} {Text((double)z["kosten"]!),6} → Mehrgewinn Ø {Text((double)z["mehrgewinnJeJahr"]!["mittel"]!),6}/Jahr, " +
                              $"Amortisation {Zahl((double?)z["amortisationJahre"], "0.0")} J.");
        return new JsonObject
        {
            ["erklaerung"] = "Statisch je Stück: Mehrgewinn im Jahr für einen neuen Betrieb der Stufe, ab der es das Stück gibt, voll mit Gesellen, zu Startpreisen " +
                             "(Rechnung je Zeile). Brand und Einbruch mit den gemessenen Anschlägen je Betrieb und Jahr unter der Strategie der KI (Abschnitt KI-Stärke). " +
                             "Stücke ohne Gulden-Wirkung (Lager, Razzia, Streife, Geleit, Spuren, Kreditrahmen …) ohne Zahl. Simuliert: Der Spieler kauft alle " +
                             "Ertragsstücke der Stufe 1 am Start (kürzere Zeit, weniger Lohn, mehr Rahmen oder Wechsel) und wird wie im Abschnitt Berufe geführt; " +
                             "Vergleich mit derselben Partie ohne Einrichtung (gleicher Startwert): Mehrgewinn = Mittel der Jahre 1 bis " + y + ". " +
                             "Die KI-Häuser kaufen keine Einrichtung.",
            ["gemessenSabotagenJeBetriebUndJahr"] = R(sabotagen, 4),
            ["gemessenRaubJeBetriebUndJahr"] = R(raub, 1),
            ["statisch"] = statisch,
            ["simuliert"] = simuliert,
        };
    }

    private static string Wirkungstext(EinrichtungWirkung w)
    {
        var teile = new List<string>();
        if (w.LagerPlus > 0) teile.Add($"Lager +{w.LagerPlus} je Ware (weniger Stillstand bei vollem Lager)");
        if (w.EinbruchBonus > 0) teile.Add($"Einbruchchance der eigenen Diebe +{Zahl(w.EinbruchBonus, "0.00")}");
        if (w.FangFaktor < 1) teile.Add($"Fangchance der Taschendiebe × {Zahl(w.FangFaktor, "0.00")}");
        if (w.SpurenFaktor < 1) teile.Add($"Spuren × {Zahl(w.SpurenFaktor, "0.00")}");
        if (w.RazziaBonus > 0) teile.Add($"Razzia-Chance +{Zahl(w.RazziaBonus, "0.00")}");
        if (w.StreifenFaktor > 1) teile.Add($"Streife × {Zahl(w.StreifenFaktor, "0.00")}");
        if (w.GeleitFaktor < 1) teile.Add($"Überfälle auf Karren im Geleit × {Zahl(w.GeleitFaktor, "0.00")}");
        return (teile.Count == 0 ? "wirkt nicht direkt in Gulden (z. B. Kreditrahmen für Häuser)" : string.Join("; ", teile)) + " – keine Gulden-Rechnung";
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  7 Gauner, Wache, Bank
    // ═════════════════════════════════════════════════════════════════════════

    private static JsonObject GaunerWacheBank(Einstellung e, Wirtschaftsdaten daten, IReadOnlyDictionary<Messaufbau, Partieergebnis> alle,
        Func<Messaufbau, Partieergebnis> p, List<string> befunde)
    {
        var letztes = e.Stich[^1];
        double JeJahrMittel(Hausverlauf h, Func<Jahreswert, double> f) => h.Jahre.Average(f);

        // Dieb: ausgewogen (nur Taschendiebe) und wagemutig (Einbruch, Schutzgeld …), ohne und mit Wachhaus in der Stadt
        var diebe = new JsonArray();
        var diebGewinn = new Dictionary<(KiCharakter, bool), double>();
        foreach (var c in new[] { KiCharakter.Ausgewogen, KiCharakter.Wagemutig })
            foreach (var wache in new[] { false, true })
            {
                var laeufe = Gut(e.Werte.Select(s => p(Beruf(e, s, "Dieb") with { BezugCharakter = c, WachhausBeimErsten = wache })));
                if (laeufe.Count == 0) continue;
                diebGewinn[(c, wache)] = laeufe.Average(r => JeJahrMittel(r.Spieler, x => x.Gewinn));
                diebe.Add(new JsonObject
                {
                    ["fuehrung"] = c.ToString(), ["wachhausInDerStadt"] = wache, ["n"] = laeufe.Count,
                    ["gewinnJeJahr"] = Kennzahl(laeufe.Select(r => JeJahrMittel(r.Spieler, x => x.Gewinn))),
                    ["beuteJeJahr"] = Kennzahl(laeufe.Select(r => JeJahrMittel(r.Spieler, x => x.Sonder.GetValueOrDefault("Beute")))),
                    ["faengeJeJahr"] = Kennzahl(laeufe.Select(r => JeJahrMittel(r.Spieler, x => x.Sonder.GetValueOrDefault("Faenge"))), 2),
                    ["reinvermoegen"] = JeJahr(e.Stich, j => Kennzahl(laeufe.Select(r => r.Spieler.Im(j).Rein))),
                    ["jahreMitWacheJePartie"] = R(laeufe.Average(r => (double)r.JahreMitWache), 1),
                    ["patrouilleschutz"] = R(laeufe.Sum(r => r.Patrouilleschutz) / Math.Max(1, laeufe.Sum(r => r.JahreMitWache)), 3),
                    ["diebesauftraegeJePartie"] = R(laeufe.Average(r => r.Gauner.Where(x => x.Key.StartsWith("Diebe:")).Sum(x => x.Value.Anzahl)), 1),
                });
            }
        foreach (var c in new[] { KiCharakter.Ausgewogen, KiCharakter.Wagemutig })
            if (diebGewinn.ContainsKey((c, false)) && diebGewinn.ContainsKey((c, true)))
                befunde.Add($"Dieb ({c}): Gewinn je Jahr {Text(diebGewinn[(c, false)])} ohne, {Text(diebGewinn[(c, true)])} mit Wachhaus in der Stadt " +
                            $"({Vz(Abstand(diebGewinn[(c, true)], diebGewinn[(c, false)]))} %).");

        // Wache: der Spieler als Gardist (Abschnitt Berufe) – Sold, Razzien; Schaden der anderen Häuser mit und ohne Wachhaus
        var gardist = Gut(e.Werte.Select(s => alle.GetValueOrDefault(Beruf(e, s, "Gardist"))).OfType<Partieergebnis>());
        var schmied = Gut(e.Werte.Select(s => alle.GetValueOrDefault(Beruf(e, s, "Schmied"))).OfType<Partieergebnis>());
        double SchadenAndere(Partieergebnis r) => r.Ki.Average(h => JeJahrMittel(h, x => x.Raub + x.Schaden));
        var wacheJson = new JsonObject
        {
            ["n"] = gardist.Count,
            ["soldJeJahr"] = Kennzahl(gardist.Select(r => JeJahrMittel(r.Spieler, x => x.Sonder.GetValueOrDefault("Sold")))),
            ["kopfgeldJeJahr"] = Kennzahl(gardist.Select(r => JeJahrMittel(r.Spieler, x => x.Sonder.GetValueOrDefault("Kopfgeld")))),
            ["geleitgeldJeJahr"] = Kennzahl(gardist.Select(r => JeJahrMittel(r.Spieler, x => x.Sonder.GetValueOrDefault("Geleitgeld")))),
            ["razzienJeJahr"] = Kennzahl(gardist.Select(r => JeJahrMittel(r.Spieler, x => x.Sonder.GetValueOrDefault("Razzien"))), 2),
            ["patrouilleschutz"] = R(gardist.Sum(r => r.Patrouilleschutz) / Math.Max(1, gardist.Sum(r => r.JahreMitWache)), 3),
            ["schadenAndererHaeuserJeJahr"] = new JsonObject
            {
                ["mitWachhausDesSpielers"] = Kennzahl(gardist.Select(SchadenAndere)),
                ["ohne (Spieler Schmied)"] = Kennzahl(schmied.Select(SchadenAndere)),
            },
        };
        if (gardist.Count > 0 && schmied.Count > 0)
            befunde.Add($"Wache: Raub und Schaden je KI-Haus und Jahr {Text(schmied.Average(SchadenAndere))} ohne, {Text(gardist.Average(SchadenAndere))} mit Wachhaus " +
                        $"des Spielers ({Vz(Abstand(gardist.Average(SchadenAndere), schmied.Average(SchadenAndere)))} %); Razzien Ø " +
                        $"{Zahl(gardist.Average(r => JeJahrMittel(r.Spieler, x => x.Sonder.GetValueOrDefault("Razzien"))), "0.00")} im Jahr.");

        // Bank: der Spieler als Geldleiher (Abschnitt Berufe); Kredite und Wucher in allen Partien
        var geld = Gut(e.Werte.Select(s => alle.GetValueOrDefault(Beruf(e, s, "Geldleiher"))).OfType<Partieergebnis>());
        var allePartien = alle.Values.Where(r => r.Fehler is null).ToList();
        var bank = new JsonObject
        {
            ["n"] = geld.Count,
            ["geldleiher"] = new JsonObject(new[] { "Stadtzinsen", "Wechsel", "Hauszinsen", "Ausfall", "Kasse" }.Select(k =>
                new KeyValuePair<string, JsonNode?>(k, JeJahr(e.Stich, j => Kennzahl(geld.Select(r => r.Spieler.Im(j).Sonder.GetValueOrDefault(k))))))),
            ["krediteAllePartien"] = allePartien.Sum(r => r.Kredite),
            ["wucherAllePartien"] = allePartien.Sum(r => r.Wucherkredite),
            ["partien"] = allePartien.Count,
            ["hinweis"] = "Kredite nimmt nur, wer den Befehl gibt – die KI nimmt keine Kredite, der Spieler in der Messung auch nicht.",
        };
        befunde.Add($"Bank: Kredite in {allePartien.Count} Partien: {allePartien.Sum(r => r.Kredite)} (Wucher {allePartien.Sum(r => r.Wucherkredite)}) – " +
                    "die KI nimmt keine Kredite, Wucher kommt deshalb nur mit Spielern vor" +
                    (geld.Count > 0 ? $"; der Geldleiher (ein Betrieb) hat nach {letztes} Jahren Ø {Text(geld.Average(r => r.Spieler.Im(letztes).Sonder.GetValueOrDefault("Kasse")))} " +
                                      $"in der Kasse, Zins {Text(geld.Average(r => r.Spieler.Im(letztes).Sonder.GetValueOrDefault("Stadtzinsen")))} und Wechsel " +
                                      $"{Text(geld.Average(r => r.Spieler.Im(letztes).Sonder.GetValueOrDefault("Wechsel")))} im Jahr." : "."));

        // Gaunerei je Auftrag: Hinterzimmer aus den Partien mit dem Bezugsspieler (Strategie der KI), Diebe aus den wagemutigen Dieben
        var bezugslaeufe = Gut(e.Werte.Select(s => alle.GetValueOrDefault(new Messaufbau(e.Karte, s, e.Jahre, Bezug: Bezugsart.WieKi))).OfType<Partieergebnis>());
        var diebeslaeufe = Gut(e.Werte.SelectMany(s => new[] { false, true }.Select(wache =>
            alle.GetValueOrDefault(Beruf(e, s, "Dieb") with { BezugCharakter = KiCharakter.Wagemutig, WachhausBeimErsten = wache }))).OfType<Partieergebnis>());
        JsonArray Gaunerei(List<Partieergebnis> laeufe, string vorsilbe) => new(laeufe.SelectMany(r => r.Gauner).Where(x => x.Key.StartsWith(vorsilbe))
            .GroupBy(x => x.Key).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g =>
            {
                var anzahl = g.Sum(x => x.Value.Anzahl);
                var mitChance = g.Sum(x => x.Value.MitChance);
                return (JsonNode)new JsonObject
                {
                    ["art"] = g.Key, ["auftraege"] = anzahl, ["jePartieUndJahr"] = R(anzahl / (double)(laeufe.Count * e.Jahre), 3),
                    ["erfolgschanceMittel"] = mitChance > 0 ? R(g.Sum(x => x.Value.Chancen) / mitChance, 3) : null,
                };
            }).ToArray());
        var hinterzimmer = Gaunerei(bezugslaeufe, "Hinterzimmer:");
        var eigeneDiebe = Gaunerei(diebeslaeufe, "Diebe:");
        if (hinterzimmer.Count > 0)
            befunde.Add("Gaunerei (Hinterzimmer, Strategie der KI): " + string.Join(", ", hinterzimmer.Select(x =>
                $"{((string)x!["art"]!).Replace("Hinterzimmer: ", "")} {Zahl((double)x["jePartieUndJahr"]!, "0.00")}/Jahr" +
                (x["erfolgschanceMittel"] is { } c ? $" zu {Zahl((double)c! * 100, "0")} %" : ""))) + ".");
        Console.WriteLine($"7 Gauner, Wache, Bank: Dieb-Gewinn {string.Join(", ", diebGewinn.Select(x => $"{x.Key.Item1}{(x.Key.Item2 ? " mit Wache" : "")} {Text(x.Value)}"))}");
        return new JsonObject
        {
            ["erklaerung"] = "Dieb: der Spieler als Dieb mit einem Betrieb, KI-geführt ausgewogen (nur Taschendiebe) bzw. wagemutig (dazu Einbruch, Auskundschaften, " +
                             "Schutzgeld, Entführung), jeweils ohne und mit einem Wachhaus (vier Gesellen) beim ersten KI-Haus wie die Probe in §27. Wache: der Spieler " +
                             "als Gardist (Abschnitt Berufe); Schaden der KI-Häuser (Raub + Schaden je Jahr) gegen die Partien mit dem Spieler als Schmied. Bank: der " +
                             "Spieler als Geldleiher; Kredite und Wucher über alle Partien des Berichts. Gaunerei: Aufträge im Hinterzimmer aus den Partien des " +
                             "Bezugsspielers (Abschnitt KI-Stärke, vier KI-geführte Häuser), eigene Diebe aus den wagemutigen Dieben; Erfolgschance = Mittel der " +
                             "Chancen in der Minute vor der Ausführung (Überfälle ohne: sie hängt am Karren).",
            ["dieb"] = diebe,
            ["wache"] = wacheJson,
            ["bank"] = bank,
            ["gaunereiHinterzimmer"] = hinterzimmer,
            ["gaunereiEigeneDiebe"] = eigeneDiebe,
        };
    }
}
