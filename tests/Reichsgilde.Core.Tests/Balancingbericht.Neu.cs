using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

// Balancing-Bericht, Abschnitte 8 und 9 (PR #208): was seit Messung 3 neu ist – Hafen, Stadtkasse, Spitzel, Karren verkaufen – und Ziel Z6.

static partial class Balancingbericht
{
    // ═════════════════════════════════════════════════════════════════════════
    //  8 Hafen und Stadtkasse
    // ═════════════════════════════════════════════════════════════════════════

    private static JsonObject HafenKasse(Einstellung e, Wirtschaftsdaten daten, IReadOnlyDictionary<Messaufbau, Partieergebnis> alle,
        Func<Messaufbau, Partieergebnis> p, List<string> befunde)
    {
        var letztes = e.Stich[^1];
        double JahrMittel(Hausverlauf h, Func<Jahreswert, double> f) => h.Jahre.Average(f);
        JsonObject Lasten(IEnumerable<Hausverlauf> haeuser)
        {
            var h = haeuser.ToList();
            return new JsonObject
            {
                ["n"] = h.Count,
                ["steuerJeJahr"] = Kennzahl(h.Select(x => JahrMittel(x, j => j.Steuer))),
                ["steuerAnteilAmUmsatzProzent"] = R(h.Sum(x => x.Jahre.Sum(j => j.Steuer)) / Math.Max(1, h.Sum(x => x.Jahre.Sum(j => j.Umsatz))) * 100, 2),
                ["spitzelJeJahr"] = Kennzahl(h.Select(x => JahrMittel(x, j => j.Spitzel))),
                ["gerichtJeJahr"] = Kennzahl(h.Select(x => JahrMittel(x, j => j.Gericht))),
                ["gewinnJeJahr"] = Kennzahl(h.Select(x => JahrMittel(x, j => j.Gewinn))),
            };
        }

        var karten = new JsonArray();
        foreach (var karte in Hafenkarten)
        {
            var paare = e.Werte.Select(s => (Mit: p(new Messaufbau(karte, s, e.Jahre, Bezug: Bezugsart.Passiv)),
                    Ohne: p(new Messaufbau(karte, s, e.Jahre, Bezug: Bezugsart.Passiv, OhneSchiffshandel: true))))
                .Where(x => x.Mit.Fehler is null && x.Ohne.Fehler is null).ToList();
            if (paare.Count == 0) continue;
            var mit = paare.Select(x => x.Mit).ToList();

            // Exportnachfrage: der passive Spieler (ohne Schiff) und die KI-Häuser mit und ohne Schiffshandel, gleiche Startwerte
            JsonObject Vergleich(Func<Partieergebnis, double> wert) => new()
            {
                ["mit"] = Kennzahl(paare.Select(x => wert(x.Mit))), ["ohne"] = Kennzahl(paare.Select(x => wert(x.Ohne))),
                ["abstandProzent"] = R(Abstand(paare.Average(x => wert(x.Mit)), paare.Average(x => wert(x.Ohne))), 1),
                ["abstandJePartie"] = Kennzahl(paare.Select(x => Abstand(wert(x.Mit), wert(x.Ohne))), 1),
            };
            var spieler = Vergleich(r => r.Spieler.Im(letztes).Rein);
            var ki = Vergleich(r => r.Ki.Average(h => h.Im(letztes).Rein));
            var jeHaus = new JsonArray(mit[0].Haeuser.Select((h, i) => (JsonNode)new JsonObject
            {
                ["haus"] = h.IstSpieler ? "Spieler (passiv)" : h.Name, ["beruf"] = h.Startberuf,
                ["vergleich"] = Vergleich(r => r.Haeuser[i].Im(letztes).Rein),
            }).ToArray());

            var schiffsjahre = mit.Sum(r => r.Schiffsjahre);
            var schiffe = new JsonObject
            {
                ["schiffeAmEndeJePartie"] = Kennzahl(mit.Select(r => (double)r.Haeuser.Sum(h => h.Jahre[^1].Schiffe)), 2),
                ["schiffeDesSpielers"] = mit.Sum(r => r.Spieler.Jahre[^1].Schiffe),
                ["erstesSchiffJahr"] = Kennzahl(mit.Where(r => r.ErstesSchiffJahr is not null).Select(r => (double)r.ErstesSchiffJahr!.Value), 1),
                ["schiffsjahre"] = schiffsjahre,
                ["ertragJeSchiffUndJahr"] = schiffsjahre > 0 ? R(mit.Sum(r => r.Schiffsertrag) / schiffsjahre) : null,
            };

            // Stadtkasse (aus den Partien mit Schiffshandel): Stand je Stichjahr, Steuersatz am Ende, Posten im Mittel je Jahr
            var mitKasse = mit.Where(r => r.Kassenstand.Count >= letztes).ToList();
            var posten = mitKasse.SelectMany(r => r.Kassenposten).GroupBy(x => x.Key).OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Value) / (mitKasse.Count * (double)e.Jahre));
            var kasse = new JsonObject
            {
                ["n"] = mitKasse.Count,
                ["standAmJahresende"] = JeJahr(new[] { 1 }.Concat(e.Stich).Distinct(), j => Kennzahl(mitKasse.Select(r => r.Kassenstand[j - 1]))),
                ["standMittelUeberDieJahre"] = Kennzahl(mitKasse.Select(r => r.Kassenstand.Average())),
                ["jahreImMinusJePartie"] = Kennzahl(mitKasse.Select(r => (double)r.Kassenstand.Count(x => x < 0)), 1),
                ["steuersatzAmEnde"] = new JsonObject(mitKasse.GroupBy(r => r.Steuersatz[^1]).OrderBy(g => g.Key)
                    .Select(g => new KeyValuePair<string, JsonNode?>($"{g.Key} %", g.Count()))),
                ["steuersatzMittelUeberDieJahre"] = mitKasse.Count > 0 ? R(mitKasse.Average(r => r.Steuersatz.Average()), 2) : null,
                ["postenJeJahr"] = new JsonObject(posten.Select(x => new KeyValuePair<string, JsonNode?>(Reichsgilde.Core.Politik.KassenpostenTexte.Name(x.Key), R(x.Value)))),
            };

            var kiHaeuser = mit.SelectMany(r => r.Ki).ToList();
            karten.Add(new JsonObject
            {
                ["karte"] = karte, ["n"] = paare.Count,
                ["exportnachfrage"] = new JsonObject { ["spieler"] = spieler, ["kiHaeuserMittel"] = ki, ["jeHaus"] = jeHaus },
                ["schiffe"] = schiffe,
                ["kasse"] = kasse,
                ["lastenKiHaeuser"] = Lasten(kiHaeuser),
                ["lastenSpieler"] = Lasten(mit.Select(r => r.Spieler)),
            });

            befunde.Add($"Exportnachfrage {karte} ({paare.Count} Startwerte, {e.Jahre} Jahre): passiver Spieler ohne Schiff {Text(paare.Average(x => x.Mit.Spieler.Im(letztes).Rein))} " +
                        $"mit, {Text(paare.Average(x => x.Ohne.Spieler.Im(letztes).Rein))} ohne Schiffshandel ({Vz((double)spieler["abstandProzent"]!, 1)} %); KI-Häuser " +
                        $"{Vz((double)ki["abstandProzent"]!, 1)} %; Ertrag je Schiff und Jahr nach Unterhalt {Text(schiffsjahre > 0 ? mit.Sum(r => r.Schiffsertrag) / schiffsjahre : 0)}.");
            if (mitKasse.Count > 0)
                befunde.Add($"Stadtkasse {karte}: Stand Ø {string.Join(" / ", e.Stich.Select(j => Text(mitKasse.Average(r => r.Kassenstand[j - 1]))))} " +
                            $"(Jahr {string.Join("/", e.Stich)}), im Mittel aller Jahre {Text(mitKasse.Average(r => r.Kassenstand.Average()))}, Steuersatz am Ende {string.Join(", ", mitKasse.GroupBy(r => r.Steuersatz[^1]).OrderBy(g => g.Key).Select(g => $"{g.Key} % in {g.Count()}"))}; " +
                            $"Steuer je KI-Haus Ø {Text(kiHaeuser.Average(h => JahrMittel(h, j => j.Steuer)))} im Jahr, Spitzel {Text(kiHaeuser.Average(h => JahrMittel(h, j => j.Spitzel)))}, " +
                            $"Gericht gesamt {Text(kiHaeuser.Average(h => JahrMittel(h, j => j.Gericht)))}.");
            Console.WriteLine($"8 Hafen {karte}: Spieler passiv {Vz((double)spieler["abstandProzent"]!, 1)} %, KI {Vz((double)ki["abstandProzent"]!, 1)} % mit Schiffshandel");
        }

        // Spitzel und Gericht beim Spieler mit einem Betrieb (Abschnitt Berufe, falls gemessen)
        var berufe = new JsonArray();
        foreach (var beruf in daten.Berufe.Keys)
        {
            var laeufe = Gut(e.Werte.Select(s => alle.GetValueOrDefault(Beruf(e, s, beruf))).OfType<Partieergebnis>());
            if (laeufe.Count == 0) continue;
            var l = Lasten(laeufe.Select(r => r.Spieler));
            l["beruf"] = beruf;
            berufe.Add(l);
        }
        if (berufe.Count > 0)
        {
            var z = berufe.Select(x => x!.AsObject()).ToList();
            double M(JsonObject o, string k) => (double)o[k]!["mittel"]!;
            befunde.Add($"Spitzel und Gericht (ein Betrieb, KI geführt, {e.Jahre} Jahre): Spitzel Ø {Text(z.Average(o => M(o, "spitzelJeJahr")))} im Jahr, Gericht gesamt " +
                        $"{Text(z.Average(o => M(o, "gerichtJeJahr")))} (Spanne {string.Join(", ", z.OrderByDescending(o => M(o, "gerichtJeJahr")).Take(3).Select(o => $"{o["beruf"]} {Text(M(o, "gerichtJeJahr"))}"))} …); " +
                        $"Steuer Ø {Text(z.Average(o => M(o, "steuerJeJahr")))} im Jahr bei Gewinn Ø {Text(z.Average(o => M(o, "gewinnJeJahr")))}.");
        }

        // Karren verkaufen (PR #205): über alle Partien des Berichts
        var gut = alle.Values.Where(r => r.Fehler is null).ToList();
        var karren = new JsonObject
        {
            ["partien"] = gut.Count, ["inNot"] = gut.Sum(r => r.KarrenNotverkauf), ["ungenutzt"] = gut.Sum(r => r.KarrenUngenutzt),
            ["partienMitVerkauf"] = gut.Count(r => r.KarrenNotverkauf + r.KarrenUngenutzt > 0),
        };
        befunde.Add($"Karren verkaufen: in {gut.Count} Partien verkauft die KI {gut.Sum(r => r.KarrenNotverkauf)} Karren in Not und {gut.Sum(r => r.KarrenUngenutzt)} " +
                    $"nach einem Jahr ohne Fahrt (in {gut.Count(r => r.KarrenNotverkauf + r.KarrenUngenutzt > 0)} Partien).");

        return new JsonObject
        {
            ["erklaerung"] = "Exportnachfrage: Greybox und Probestadt (beide mit Fluss und Hafen), der Spieler passiv (seine Werkstatt arbeitet mit den " +
                             "Startgesellen, niemand führt ihn – er kauft kein Schiff), die KI-Häuser ausgewogen; dieselben Startwerte einmal mit und einmal ohne " +
                             "Abschnitt „schiffshandel“ (wie „messen schiffe“, PR #199). Schiffe: Ertrag je Schiff und Jahr nach Unterhalt (Schiff.ErtragVorjahr). " +
                             "Stadtkasse (PR #200) aus den Partien mit Schiffshandel: Stand am Jahresende, Steuersatz, Posten im Mittel je Jahr. Lasten je Haus: " +
                             "Umsatzsteuer (Teil des Betriebsgewinns, geht in die Stadtkasse), Spitzel (300 je Spitzel, Teil des Postens Gericht) und Gericht gesamt " +
                             "(Spitzel, Gebühren, Bußen, Entschädigungen; negativ = Einnahme) im Mittel je Jahr; beim Spieler mit einem Betrieb aus dem Abschnitt Berufe. " +
                             "Karren verkaufen (PR #205): Meldungen der KI über alle Partien des Berichts. Wetter: wirkt nicht auf Wirtschaft und Simulation " +
                             "(Entscheidung zu PR #195; der Kern kennt nur die Jahreszeit als Name) – nichts zu messen.",
            ["karten"] = karten,
            ["lastenEinBetriebJeBeruf"] = berufe,
            ["karrenVerkauft"] = karren,
            ["wetter"] = "ohne Wirkung im Kern (Entscheidung zu PR #195)",
        };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  9 Statut C (Ziel Z6)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ertrag eines Kopfes im Jahr in einem neuen Betrieb dieser Art – dieselbe Rechnung wie <see cref="Kalkulation.ErwarteterJahresgewinn"/>:
    /// Tempo × Arbeitsminuten × Wert je Minute − Lohn (mit der Schicht) − Risiko × Schaden.
    /// </summary>
    public static double KopfErtrag(Stadtwirtschaft w, Betriebsart art, string kraft)
    {
        var d = w.Daten.Art(art);
        var k = w.Daten.Arbeitskraefte[kraft];
        var wert = (d.IstWerkstatt ? d.Produkte : d.Rohstoffe).Max(x => Kalkulation.GewinnJeArbeitsminute(w, x));
        return k.TempoAm(d.Ort) * w.Daten.Arbeitsminuten(art) * wert - k.Kosten * w.Daten.Schichtfaktor(art) - k.Risiko * k.Schaden;
    }

    private static JsonObject StatutC(Einstellung e, Messquellen q, Wirtschaftsdaten daten, List<string> befunde)
    {
        var (w, _) = Startmarkt(q, e.Karte);
        var zeilen = new JsonArray();
        var werkstatt = new List<double>();
        foreach (var (art, d) in daten.Betriebsarten.Where(x => !x.Value.OhneProduktion).OrderBy(x => x.Value.Ort).ThenBy(x => x.Value.Beruf, StringComparer.Ordinal).ThenBy(x => x.Key))
        {
            var b = Kalkulation.ErwarteterJahresgewinn(w, art, Fronstatut.Bestienfron);
            var c = Kalkulation.ErwarteterJahresgewinn(w, art, Fronstatut.Leibeigenschaft);
            var koepfe = new JsonObject(daten.Arbeitskraefte.Keys.Where(k => daten.Arbeitskraefte[k].IstErlaubt(Fronstatut.Leibeigenschaft, d.Ort))
                .Select(k => new KeyValuePair<string, JsonNode?>(k, R(KopfErtrag(w, art, k)))));
            var leib = daten.Arbeitskraefte["Leibeigener"];
            // Ab welcher Wertschöpfung je Kopf und Jahr (Tempo 1) der Leibeigene gleichauf mit der besten Kraft unter B liegt
            var besteB = daten.Arbeitskraefte.Where(x => x.Value.IstErlaubt(Fronstatut.Bestienfron, d.Ort)).MaxBy(x => KopfErtrag(w, art, x.Key));
            var tempoUnterschied = besteB.Value.TempoAm(d.Ort) - leib.TempoAm(d.Ort);
            var kostenUnterschied = besteB.Value.Kosten * daten.Schichtfaktor(art) + besteB.Value.Risiko * besteB.Value.Schaden
                                    - leib.Kosten * daten.Schichtfaktor(art) - leib.Risiko * leib.Schaden;
            var wertschoepfung = KopfErtrag(w, art, "Geselle") + daten.Arbeitskraefte["Geselle"].Kosten * daten.Schichtfaktor(art);
            zeilen.Add(new JsonObject
            {
                ["betrieb"] = d.Name, ["beruf"] = d.Beruf, ["ort"] = d.Ort.ToString(), ["plaetze"] = d.Plaetze,
                ["gewinnStatutB"] = R(b), ["gewinnStatutC"] = R(c), ["abstandCzuBProzent"] = R(Abstand(c, b), 1),
                ["besteKraftB"] = besteB.Key, ["kopfErtrag"] = koepfe,
                ["wertschoepfungJeKopfMitTempo1"] = d.Ort == Arbeitsort.Werkstatt ? R(wertschoepfung) : null,
                ["gleichstandBeiWertschoepfung"] = tempoUnterschied > 0 ? R(kostenUnterschied / tempoUnterschied) : null,
            });
            if (d.Ort == Arbeitsort.Werkstatt) werkstatt.Add(Abstand(c, b));
        }
        // Z6 neu gefasst (Entscheidung des Nutzers, 26.10.2026): Statut C senkt die Lohnkosten bei gleicher Arbeit in einfachen Betrieben
        // (Umland). Gemessen: Kosten je Arbeit = (Lohn + Risiko × Schaden) ÷ Tempo im Umland, je erlaubter Kraft.
        var umland = new JsonObject(daten.Arbeitskraefte.Where(x => x.Value.IstErlaubt(Fronstatut.Leibeigenschaft, Arbeitsort.Umland))
            .Select(x => new KeyValuePair<string, JsonNode?>(x.Key, R(UmlandKostenJeArbeit(daten, x.Key), 1))));
        var bergmann = UmlandKostenJeArbeit(daten, "Altknecht");
        var leibeigener = UmlandKostenJeArbeit(daten, "Leibeigener");
        var ork = UmlandKostenJeArbeit(daten, "Ork");
        befunde.Add($"Z6 neu (Umland, Lohnkosten je Arbeit = Lohn und Risiko ÷ Tempo): Leibeigener {Text(leibeigener)} statt Altknecht {Text(bergmann)} " +
                    $"({Vz(Abstand(leibeigener, bergmann))} %; unter Statut A die einzige Kraft) und Ork {Text(ork)} ({Vz(Abstand(leibeigener, ork))} %; unter Statut B die beste).");
        var g = Kalkulation.ErwarteterJahresgewinn(w, Betriebsart.Giesserei, Fronstatut.Bestienfron);
        var gc = Kalkulation.ErwarteterJahresgewinn(w, Betriebsart.Giesserei, Fronstatut.Leibeigenschaft);
        befunde.Add($"Z6 Statut C (Startpreise): Gießerei {Text(g)} unter B, {Text(gc)} unter C ({Vz(Abstand(gc, g), 1)} %, Ziel +15–25 %); " +
                    $"Werkstätten C gegen B {Vz(werkstatt.Min(), 1)} … {Vz(werkstatt.Max(), 1)} %, in {werkstatt.Count(x => x > 0.5)} von {werkstatt.Count} mehr. " +
                    $"Gleichstand Leibeigener/Geselle bei einer Wertschöpfung von {Text(KostenGleichstand(daten))} je Kopf und Jahr.");
        Console.WriteLine($"9 Statut C: Gießerei {Vz(Abstand(gc, g), 1)} %, Werkstätten {Vz(werkstatt.Min(), 1)} … {Vz(werkstatt.Max(), 1)} %");
        return new JsonObject
        {
            ["erklaerung"] = "Ziel Z6: Statut C lohnt sich in Gulden mäßig (+15–25 %). Statisch zu Startpreisen (erste Minute auf der Karte des Berichts): " +
                             "Kalkulation.ErwarteterJahresgewinn eines neuen Betriebs unter B und unter C (voll belegt mit der besten erlaubten Kraft), dazu der " +
                             "Ertrag je Kopf und Jahr jeder unter C erlaubten Kraft (Tempo × Arbeitsminuten × Wert je Minute − Lohn − Risiko × Schaden). " +
                             "„gleichstandBeiWertschoepfung“: Wertschöpfung je Kopf und Jahr (Tempo 1, vor Lohn), bei der der Leibeigene gleich viel bringt wie die " +
                             "beste Kraft unter B – darüber gewinnt die schnellere Kraft. Nicht in Gulden: Ansehen, Aufstand, Reichsvogt.",
            ["zeilen"] = zeilen,
            ["umlandKostenJeArbeit"] = umland,
            ["umlandErklaerung"] = "Z6 neu gefasst (26.10.2026): Statut C senkt die Lohnkosten bei gleicher Arbeit in einfachen Betrieben (Umland). " +
                                   "Kosten je Arbeit = (Lohn + Risiko × Schaden) ÷ Tempo im Umland – was eine Arbeitsminute mit Tempo 1 kostet, je Jahr.",
        };
    }

    /// <summary>Was die Arbeit einer Kraft im Umland je Jahr bei Tempo 1 kostet: (Lohn + Risiko × Schaden) ÷ Tempo im Umland.</summary>
    public static double UmlandKostenJeArbeit(Wirtschaftsdaten daten, string kraft)
    {
        var k = daten.Arbeitskraefte[kraft];
        return (k.Kosten + k.Risiko * k.Schaden) / k.TempoAm(Arbeitsort.Umland);
    }

    /// <summary>Wertschöpfung je Kopf und Jahr (Tempo 1, vor Lohn), bei der ein Leibeigener in der Werkstatt so viel bringt wie ein Geselle (Schicht 1).</summary>
    public static double KostenGleichstand(Wirtschaftsdaten daten)
    {
        var g = daten.Arbeitskraefte["Geselle"];
        var l = daten.Arbeitskraefte["Leibeigener"];
        return (g.Kosten + g.Risiko * g.Schaden - l.Kosten - l.Risiko * l.Schaden) / (g.TempoWerkstatt - l.TempoWerkstatt);
    }
}
