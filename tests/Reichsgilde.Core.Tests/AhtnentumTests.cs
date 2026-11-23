using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests für den dritten Glauben, das Ahtnentum (Prediger-PR 5, Nutzer 11.11. und 23.11.2026): Bekenntnisse altahtnisch und neuahtnisch, wer sie
/// haben darf (Hebel a), Gunst unter Glaubensverwandten (Hebel b), Daten, Startaufstellung, Stadtglaube, Kirchengrenze, Dom und alte Spielstände.
/// </summary>
static class AhtnentumTests
{
    static readonly string Json = File.ReadAllText(Datenpfad.Wirtschaft());
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json));

    static Simulation NeueSimulation() =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: Daten.Value);

    static (Stadtwirtschaft W, Konto K, Familie F) Haus(Simulation sim, string name, Konfession glaube, Volk volk, double geld = 20_000)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen(name, startvermoegen: geld);
        var f = w.Familien.Gruenden(k, volk, Geschlecht.Mann, alter: 30, glaube: glaube);
        k.Aktionspunkte.Setzen(12);
        return (w, k, f);
    }

    static Konto Prediger(Simulation sim, string name, Konfession glaube, Volk volk)
    {
        var (w, k, _) = Haus(sim, name, glaube, volk);
        var b = w.BetriebEroeffnen($"Kirche {name}", Betriebsart.Kirche, k, startkarren: "");
        w.Einstellen(b, "Geselle", 1);
        return k;
    }

    static readonly Volk[] Anderlinge = { Volk.Elb, Volk.Zwerg, Volk.Halbling };

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Ahtnentum Texte: jede Konfession hat eigenen Namen, Glauben, Gemeinde und Kurznamen (kein stiller Restfall); Religion je Bekenntnis", () =>
        {
            var alle = Enum.GetValues<Konfession>();
            foreach (var art in new Func<Konfession, string>[] { k => k.Name(), k => k.Glaube(), k => k.Gemeinde(), k => k.Kurzname() })
                Pruefe.Gleich(alle.Length, alle.Select(art).Distinct().Count());
            Pruefe.Gleich("altahtnisch", Konfession.Altahtnisch.Name());
            Pruefe.Gleich("Neuahtnen", Konfession.Neuahtnisch.Gemeinde());
            Pruefe.Gleich(Religion.Ahtnentum, Konfession.Altahtnisch.Religion());
            Pruefe.Gleich(Religion.Ahtnentum, Konfession.Neuahtnisch.Religion());
            Pruefe.Gleich(Religion.Christentum, Konfession.Evangelisch.Religion());
            Pruefe.Gleich(Religion.Naturglaube, Konfession.Naturglaube.Religion());
            Pruefe.Gleich(4, KonfessionTexte.Kirchliche.Count);
            Pruefe.Wahr(KonfessionTexte.Kirchliche.All(k => k.IstKirchlich()) && alle.Count(k => k.IstKirchlich()) == 4, "vier kirchliche Bekenntnisse");
        }),

        ("Ahtnentum Daten: Version 98; etwa die Hälfte der Anderlinge ahtnisch, Zwerge eher alt, Halblinge eher neu, Menschen nie; Startglaube je Volk bleibt christlich; Stadt vier Anteile", () =>
        {
            var d = Daten.Value.Glaube!;
            Pruefe.Gleich(98, Daten.Value.Version);
            foreach (var volk in Anderlinge)
            {
                var g = d.Voelker[volk];
                var ahtn = (g.GetValueOrDefault(Konfession.Altahtnisch) + g.GetValueOrDefault(Konfession.Neuahtnisch)) / g.Values.Sum();
                Pruefe.Wahr(ahtn is >= 0.4 and <= 0.6, $"{volk}: ahtnisch {ahtn}");
            }
            Pruefe.Wahr(d.Voelker[Volk.Zwerg][Konfession.Altahtnisch] > d.Voelker[Volk.Zwerg][Konfession.Neuahtnisch], "Zwerge eher alt");
            Pruefe.Wahr(d.Voelker[Volk.Halbling][Konfession.Neuahtnisch] > d.Voelker[Volk.Halbling][Konfession.Altahtnisch], "Halblinge eher neu");
            Pruefe.Wahr(d.Voelker[Volk.Mensch].GetValueOrDefault(Konfession.Altahtnisch) == 0, "Menschen nie altahtnisch gewürfelt");
            Pruefe.Gleich(Konfession.Katholisch, d.Startglaube(Volk.Mensch));
            Pruefe.Gleich(Konfession.Katholisch, d.Startglaube(Volk.Elb));
            Pruefe.Gleich(Konfession.Evangelisch, d.Startglaube(Volk.Zwerg));
            Pruefe.Gleich(Konfession.Evangelisch, d.Startglaube(Volk.Halbling));
            Pruefe.Gleich(4, d.Stadt.Count);
            Pruefe.Wahr(d.Stadt.Values.All(a => a >= d.MindestAnteil), "jeder Anteil ≥ Mindestanteil");

            // Prüfung: altahtnisches Gewicht bei Menschen und ein altahtnischer Menschen-Ratsherr fallen auf.
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Replace("\"Mensch\": { \"Katholisch\": 0.6, \"Evangelisch\": 0.4 }",
                "\"Mensch\": { \"Katholisch\": 0.6, \"Evangelisch\": 0.3, \"Altahtnisch\": 0.1 }")), "nehmen keine Mensch auf");
            var knoten = JsonNode.Parse(Json)!;
            var ratsherr = knoten["politik"]!["ratsherren"]!.AsArray().First(r => (string?)r!["volk"] == "Mensch")!;
            ratsherr["glaube"] = "Altahtnisch";
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(knoten.ToJsonString()), "Altahtnen nehmen keine Mensch auf");
        }),

        ("Ahtnentum Würfel: Heiratskandidaten der Anderlinge etwa zur Hälfte ahtnisch, Menschen nie, Orks nur Naturglaube", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            foreach (var volk in Anderlinge)
            {
                var wuerfe = Enumerable.Range(0, 2000).Select(_ => w.Glaube.Wuerfeln(volk)).ToList();
                var ahtn = wuerfe.Count(k => k.Religion() == Religion.Ahtnentum) / 2000.0;
                Pruefe.Wahr(ahtn is > 0.4 and < 0.6, $"{volk}: {ahtn}");
                Pruefe.Wahr(wuerfe.Contains(Konfession.Altahtnisch) && wuerfe.Contains(Konfession.Neuahtnisch), $"{volk}: beide Bekenntnisse");
            }
            Pruefe.Wahr(Enumerable.Range(0, 500).All(_ => w.Glaube.Wuerfeln(Volk.Mensch).Religion() == Religion.Christentum), "Menschen");
            Pruefe.Wahr(Enumerable.Range(0, 100).All(_ => w.Glaube.Wuerfeln(Volk.Ork) == Konfession.Naturglaube), "Orks");
        }),

        ("Ahtnentum Start: Startglauben je Volk passen zu den Daten (Menschen nur neuahtnisch, Orks und Oger nur Naturglaube); Start als Mensch altahtnisch verboten", () =>
        {
            var d = Daten.Value.Glaube!;
            Pruefe.Wahr(Startaufstellung.Startglauben(Volk.Mensch).SequenceEqual(new[] { Konfession.Katholisch, Konfession.Evangelisch, Konfession.Neuahtnisch }), "Menschen");
            foreach (var volk in Anderlinge)
                Pruefe.Gleich(4, Startaufstellung.Startglauben(volk).Count);
            foreach (var volk in Startaufstellung.KiVoelker)
                Pruefe.Wahr(Startaufstellung.Startglauben(volk).SequenceEqual(KonfessionTexte.Kirchliche.Where(k => d.NimmtAuf(k, volk))), $"{volk} passt zu offenFuer");
            foreach (var volk in new[] { Volk.Ork, Volk.Oger })
            {
                Pruefe.Wahr(Startaufstellung.Startglauben(volk).SequenceEqual(new[] { Konfession.Naturglaube }), $"{volk} nur Naturglaube");
                Pruefe.Wahr(!d.NimmtAuf(Konfession.Altahtnisch, volk) && !d.NimmtAuf(Konfession.Neuahtnisch, volk), $"{volk} nie ins Ahtnentum");
            }
            var w = NeueSimulation().Wirtschaft!;
            Pruefe.Wahr(w.Glaube.StartglaubeVerboten(Volk.Mensch, Konfession.Altahtnisch) is { } grund && grund.Contains("Altahtnen"), "Mensch altahtnisch");
            Pruefe.Wahr(w.Glaube.StartglaubeVerboten(Volk.Mensch, Konfession.Neuahtnisch) is null, "Mensch neuahtnisch");
            Pruefe.Wahr(w.Glaube.StartglaubeVerboten(Volk.Zwerg, Konfession.Altahtnisch) is null, "Zwerg altahtnisch");
            Pruefe.Wahr(w.Glaube.StartglaubeVerboten(Volk.Ork, Konfession.Neuahtnisch) is not null, "Ork neuahtnisch");
            Pruefe.WirftMitMeldung(() => Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value,
                new NeuePartie(Glaube: Konfession.Altahtnisch, Aufstellung: Aufstellungsart.Vorgabe)), "Altahtnen");
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value,
                new NeuePartie(Glaube: Konfession.Neuahtnisch, Aufstellung: Aufstellungsart.Vorgabe));
            Pruefe.Gleich((Konfession?)Konfession.Neuahtnisch, sim.Wirtschaft!.Glaube.GlaubeVon(spieler));
        }),

        ("Ahtnentum Übertritt (Hebel a): Menschen nur neuahtnisch, Anderlinge beides, Orks und Oger nie ins Ahtnentum (christliche Taufe bleibt)", () =>
        {
            var sim = NeueSimulation();
            var (w, mensch, _) = Haus(sim, "Haus Kreuz", Konfession.Katholisch, Volk.Mensch);
            var nein = w.Ausfuehren(mensch, new Konvertieren(Konfession.Altahtnisch));
            Pruefe.Wahr(!nein.Erfolg && nein.Meldung.Contains("Altahtnen nehmen nur"), nein.Meldung);
            Pruefe.Wahr(w.Ausfuehren(mensch, new Konvertieren(Konfession.Neuahtnisch)).Erfolg, "Mensch → neuahtnisch");
            Pruefe.Gleich((Konfession?)Konfession.Neuahtnisch, w.Glaube.GlaubeVon(mensch));

            var (_, zwerg, _) = Haus(sim, "Haus Amboss", Konfession.Evangelisch, Volk.Zwerg);
            Pruefe.Wahr(w.Ausfuehren(zwerg, new Konvertieren(Konfession.Altahtnisch)).Erfolg, "Zwerg → altahtnisch");

            // Orks und Oger haben keine Häuser; die Regel steht in den Daten bzw. NimmtAuf: nie ins Ahtnentum, die christliche Taufe bleibt.
            var d = w.Glaube.Daten;
            foreach (var volk in new[] { Volk.Ork, Volk.Oger })
            {
                Pruefe.Wahr(!d.NimmtAuf(Konfession.Altahtnisch, volk) && !d.NimmtAuf(Konfession.Neuahtnisch, volk), $"{volk} → Ahtnentum");
                Pruefe.Wahr(d.NimmtAuf(Konfession.Katholisch, volk) && d.NimmtAuf(Konfession.Evangelisch, volk), $"{volk} → Taufe");
            }
            // Ein Heide (Mensch mit Naturglauben, wie im Taufe-Test) darf sich neuahtnisch, aber nicht altahtnisch taufen lassen.
            var (_, heide, hf) = Haus(sim, "Haus Grunz", Konfession.Katholisch, Volk.Mensch);
            hf.Oberhaupt.Glaube = Konfession.Naturglaube;
            Pruefe.Wahr(!w.Ausfuehren(heide, new Konvertieren(Konfession.Altahtnisch)).Erfolg, "Heide → altahtnisch");
            var taufe = w.Ausfuehren(heide, new Konvertieren(Konfession.Neuahtnisch));
            Pruefe.Wahr(taufe.Erfolg && taufe.Meldung.Contains("neuahtnisch taufen"), taufe.Meldung);
        }),

        ("Ahtnentum Gunst (Hebel b): Neuahtnische geben Altahtnischen −1, Altahtnische Neuahtnischen −3, Ahtnen und Christen −3 – im Rat und in der Meinung", () =>
        {
            var sim = NeueSimulation();
            var (w, alt, _) = Haus(sim, "Haus Tiefstein", Konfession.Altahtnisch, Volk.Zwerg);
            var (_, neu, _) = Haus(sim, "Haus Apfelhang", Konfession.Neuahtnisch, Volk.Halbling);
            var (_, kath, _) = Haus(sim, "Haus Kreuz", Konfession.Katholisch, Volk.Mensch);
            var d = w.Glaube.Daten;
            Pruefe.Gleich(-1.0, d.GunstFuer(Konfession.Neuahtnisch, Konfession.Altahtnisch));
            Pruefe.Gleich(-3.0, d.GunstFuer(Konfession.Altahtnisch, Konfession.Neuahtnisch));
            Pruefe.Gleich(-3.0, d.GunstFuer(Konfession.Neuahtnisch, Konfession.Katholisch));
            Pruefe.Gleich(-3.0, d.GunstFuer(Konfession.Katholisch, Konfession.Evangelisch));
            Pruefe.Gleich(5.0, d.GunstFuer(Konfession.Altahtnisch, Konfession.Altahtnisch));

            var neuRat = w.Rat.RatsherrNamens("Bertram Kesselflick")!;   // neuahtnisch
            var altRat = w.Rat.RatsherrNamens("Thorgrim Erzhammer")!;    // altahtnisch
            Pruefe.Gleich(Konfession.Neuahtnisch, w.Glaube.GlaubeVon(neuRat));
            Pruefe.Gleich(Konfession.Altahtnisch, w.Glaube.GlaubeVon(altRat));
            Pruefe.Gleich(-1.0, w.Glaube.Naehe(neuRat, alt));
            Pruefe.Gleich(5.0, w.Glaube.Naehe(neuRat, neu));
            Pruefe.Gleich(-3.0, w.Glaube.Naehe(altRat, neu));
            Pruefe.Gleich(-3.0, w.Glaube.Naehe(neuRat, kath));
            Pruefe.Wahr(w.Meinungen.Gruende(neuRat, w.Familien.Von(alt)!.Oberhaupt).Any(g => g.Text.Contains("Glaubensverwandt") && g.Wert == -1),
                string.Join(" | ", w.Meinungen.Gruende(neuRat, w.Familien.Von(alt)!.Oberhaupt).Select(g => $"{g.Text} {g.Wert}")));
            Pruefe.Wahr(w.Meinungen.Gruende(altRat, w.Familien.Von(neu)!.Oberhaupt).Any(g => g.Text.Contains("Anderer Glaube") && g.Wert == -3), "alt über neu");
        }),

        ("Ahtnentum Kirche und Dom: je Bekenntnis höchstens eine Kirche; Ahtnentum kann Stadtglaube werden, dann wird nur ein Ahtne seines Bekenntnisses Domprediger", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var neu = Prediger(sim, "Familie Apfelhang", Konfession.Neuahtnisch, Volk.Halbling);
            var kath = Prediger(sim, "Familie Rom", Konfession.Katholisch, Volk.Mensch);
            var (_, zweiter, _) = Haus(sim, "Haus Distel", Konfession.Neuahtnisch, Volk.Mensch);
            Pruefe.Wahr(w.Kirchenwesen.KirchengrenzeVerboten(zweiter, Betriebsart.Kirche) is { } g && g.Contains("Neuahtnen"), "zweite neuahtnische Kirche");
            var (_, alt, _) = Haus(sim, "Haus Tiefstein", Konfession.Altahtnisch, Volk.Zwerg);
            Pruefe.Wahr(w.Kirchenwesen.KirchengrenzeVerboten(alt, Betriebsart.Kirche) is null, "altahtnische Kirche ist frei");
            Pruefe.Wahr(w.Kirchenwesen.KirchengrenzeKonvertieren(kath, Konfession.Neuahtnisch) is not null, "katholische Kirche tritt nicht neuahtnisch über");

            w.Glaube.Stadtglaube = Konfession.Neuahtnisch;
            Pruefe.Wahr(w.Domamt.EignungVerboten(neu) is null, w.Domamt.EignungVerboten(neu) ?? "");
            Pruefe.Wahr(w.Domamt.EignungVerboten(kath) is { } d && d.Contains("neuahtnischen Glauben"), "katholisch unter neuahtnischem Stadtglauben");
            Pruefe.Wahr(w.Glaube.IstAndersglaeubig(kath) && !w.Glaube.IstAndersglaeubig(neu), "Glaubenszins");
            Pruefe.Wahr(w.Glaube.IstAndersglaeubig(alt), "Altahtnische sind unter neuahtnischem Stadtglauben andersgläubig");
        }),

        ("Ahtnentum Stadtglaube: ein neuahtnischer Ratsherr kann seinen Glauben beantragen; die Abstimmung kennt die vier Bekenntnisse", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (_, k, _) = Haus(sim, "Haus Apfelhang", Konfession.Neuahtnisch, Volk.Halbling);
            var amt = w.Rat.AmtNamens("Zöllner")!;
            amt.Ratsherr = null;
            amt.Konto = k;
            var e = w.Ausfuehren(k, new StadtglaubeBeantragen(Konfession.Neuahtnisch));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich((Konfession?)Konfession.Neuahtnisch, w.Glaube.Antrag?.Glaube);
            var p = w.Glaube.AbstimmungsPrognose(k, Konfession.Neuahtnisch);
            Pruefe.Wahr(p.Ja >= 2, $"Ja {p.Ja}: der Antragsteller und Bertram Kesselflick");
        }),

        ("Ahtnentum Heirat: Ein Mensch heiratet in ein altahtnisches Haus – Haus, Ehepartner und Kinder werden neuahtnisch (Meldung); Zwerg bleibt altahtnisch; Kirchengrenze sperrt die Hochzeit", () =>
        {
            var sim = NeueSimulation();
            var (w, k, f) = Haus(sim, "Haus Tiefstein", Konfession.Altahtnisch, Volk.Zwerg, geld: 50_000);
            var kind = w.Familien.KindAufnehmen(k, 3);
            Pruefe.Gleich(Konfession.Altahtnisch, kind.Glaube);
            var kandidat = w.Familien.Kandidaten.FirstOrDefault(x => x.Person.Volk == Volk.Mensch && w.Familien.WerbenVerboten(k, x) is null)
                           ?? throw new Exception("kein menschlicher Heiratskandidat");
            Pruefe.Wahr(w.Familien.HeiratMachtNeuahtnisch(f, kandidat.Person), "Regel greift");
            w.Familien.ZuneigungSchenken(k, kandidat.Person.Nummer, 500);
            var e = w.Ausfuehren(k, new Heiraten(kandidat.Person.Nummer));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(Konfession.Neuahtnisch, f.Oberhaupt.Glaube);
            Pruefe.Gleich(Konfession.Neuahtnisch, f.Ehepartner!.Glaube);
            Pruefe.Gleich(Konfession.Neuahtnisch, kind.Glaube);
            Pruefe.Wahr(f.Lebende.All(x => x.Glaube == Konfession.Neuahtnisch), "alle Lebenden des Hauses");
            Pruefe.Gleich(Konfession.Altahtnisch, f.Oberhaupt.Herkunftsglaube);
            Pruefe.Wahr(w.Familien.DiesesJahr.Any(x => x.Text.Contains("wird neuahtnisch")), "Meldung");
            Pruefe.Gleich(Konfession.Neuahtnisch, w.Familien.KindAufnehmen(k, 1).Glaube);

            // Gegenprobe: ein Zwerg als Partner – das Haus bleibt altahtnisch.
            var (_, k2, f2) = Haus(sim, "Haus Erzgrund", Konfession.Altahtnisch, Volk.Zwerg, geld: 50_000);
            var zwerg = w.Familien.Kandidaten.FirstOrDefault(x => x.Person.Volk != Volk.Mensch && w.Familien.WerbenVerboten(k2, x) is null)
                        ?? throw new Exception("kein Heiratskandidat der Anderlinge");
            Pruefe.Wahr(!w.Familien.HeiratMachtNeuahtnisch(f2, zwerg.Person), "kein Mensch – keine Folge");
            w.Familien.ZuneigungSchenken(k2, zwerg.Person.Nummer, 500);
            var e2 = w.Ausfuehren(k2, new Heiraten(zwerg.Person.Nummer));
            Pruefe.Wahr(e2.Erfolg, e2.Meldung);
            Pruefe.Wahr(f2.Lebende.All(x => x.Glaube == Konfession.Altahtnisch), "Haus und Partner bleiben altahtnisch");
            Pruefe.Wahr(!w.Familien.DiesesJahr.Any(x => x.Konto == k2 && x.Text.Contains("wird neuahtnisch")), "keine Meldung");

            // Kirchengrenze: Das altahtnische Haus hat eine Kirche, eine neuahtnische steht schon – die Hochzeit mit einem Menschen ist gesperrt.
            var (_, k3, f3) = Haus(sim, "Haus Hallstein", Konfession.Altahtnisch, Volk.Zwerg, geld: 50_000);
            w.BetriebEroeffnen("Kirche Hallstein", Betriebsart.Kirche, k3, startkarren: "");
            Prediger(sim, "Familie Apfelhang", Konfession.Neuahtnisch, Volk.Halbling);
            var mensch = w.Familien.Kandidaten.FirstOrDefault(x => x.Person.Volk == Volk.Mensch && x.Person.Geschlecht != f3.Oberhaupt.Geschlecht && x.Stand <= k3.Stand)
                         ?? throw new Exception("kein menschlicher Heiratskandidat");
            Pruefe.Wahr(w.Familien.WerbenVerboten(k3, mensch) is { } grund && grund.Contains("neuahtnisch"), w.Familien.WerbenVerboten(k3, mensch) ?? "frei");
        }),

        ("Ahtnentum Übertritt: Lebt ein Mensch im Haus (Ehepartner, Witwe nach dem Erbfall), ist der Übertritt ins Altahtnische gesperrt; neuahtnisch geht", () =>
        {
            var sim = NeueSimulation();
            var (w, k, f) = Haus(sim, "Haus Amboss", Konfession.Evangelisch, Volk.Zwerg, geld: 50_000);
            var mensch = w.Familien.Kandidaten.FirstOrDefault(x => x.Person.Volk == Volk.Mensch && w.Familien.WerbenVerboten(k, x) is null)
                         ?? throw new Exception("kein menschlicher Heiratskandidat");
            w.Familien.ZuneigungSchenken(k, mensch.Person.Nummer, 500);
            Pruefe.Wahr(w.Ausfuehren(k, new Heiraten(mensch.Person.Nummer)).Erfolg, "Heirat");
            var nein = w.Glaube.KonvertierenVerboten(k, Konfession.Altahtnisch);
            Pruefe.Wahr(nein is { } n && n.Contains(mensch.Person.Vorname) && n.Contains("Altahtnen"), nein ?? "frei");
            Pruefe.Wahr(!w.Ausfuehren(k, new Konvertieren(Konfession.Altahtnisch)).Erfolg, "Befehl abgelehnt");
            Pruefe.Gleich(Konfession.Evangelisch, f.Ehepartner!.Glaube);

            // Erbfall: Das Oberhaupt stirbt, der zwergische Sohn erbt, die menschliche Witwe lebt im Haus – weiter gesperrt.
            var sohn = w.Familien.KindAufnehmen(k, 31, Geschlecht.Mann); // Zwerge sind mit 30 volljährig
            f.Testament = sohn;
            w.Familien.AnKrankheitSterben(f.Oberhaupt, "am Fieber");
            Pruefe.Gleich(sohn, f.Oberhaupt);
            Pruefe.Wahr(f.Lebende.Any(x => x.Volk == Volk.Mensch), "Witwe lebt im Haus");
            Pruefe.Wahr(w.Glaube.KonvertierenVerboten(k, Konfession.Altahtnisch) is not null, "Erbe mit menschlicher Witwe");
            Pruefe.Wahr(w.Glaube.KonvertierenVerboten(k, Konfession.Neuahtnisch) is null, "neuahtnisch nimmt Menschen auf");

            // Gegenprobe: ohne Menschen im Haus darf der Zwerg altahtnisch werden.
            var (_, k2, _) = Haus(sim, "Haus Erzgrund", Konfession.Evangelisch, Volk.Zwerg);
            Pruefe.Wahr(w.Glaube.KonvertierenVerboten(k2, Konfession.Altahtnisch) is null, "Zwerg allein");
            // Fest im Code: Menschen nie altahtnisch, auch wenn die Daten es erlaubten.
            var offen = Wirtschaftsdaten.AusJson(Json.Replace("\"Altahtnisch\": { \"offenFuer\": [\"Elb\", \"Zwerg\", \"Halbling\"] },", ""));
            Pruefe.Wahr(!offen.Glaube!.NimmtAuf(Konfession.Altahtnisch, Volk.Mensch), "Menschen nie altahtnisch ohne offenFuer");
        }),

        ("Ahtnentum KI: Unter altahtnischem Stadtglauben tritt ein KI-Haus mit menschlichem Ehepartner nicht über; eine KI-Heirat mit einem Menschen macht das altahtnische Haus neuahtnisch", () =>
        {
            var sim = NeueSimulation();
            var (w, ki, f) = Haus(sim, "Haus Amboss", Konfession.Evangelisch, Volk.Zwerg, geld: 50_000);
            var mensch = w.Familien.Kandidaten.First(x => x.Person.Volk == Volk.Mensch && w.Familien.WerbenVerboten(ki, x) is null);
            w.Familien.ZuneigungSchenken(ki, mensch.Person.Nummer, 500);
            Pruefe.Wahr(w.Ausfuehren(ki, new Heiraten(mensch.Person.Nummer)).Erfolg, "Heirat");
            w.KiAnmelden(ki, Reichsgilde.Core.Ki.KiProfil.Standard);
            w.Glaube.Stadtglaube = Konfession.Altahtnisch;
            var (_, ki2, f2) = Haus(sim, "Haus Tiefstein", Konfession.Altahtnisch, Volk.Zwerg, geld: 50_000);
            // Der Heiratsmarkt hat für das zweite Haus nur noch Menschen (die KI geht denselben Weg wie der Spieler).
            var markt = (List<Kandidat>)w.Familien.Kandidaten;
            markt.RemoveAll(x => x.Person.Volk != Volk.Mensch);
            Pruefe.Wahr(markt.Any(x => w.Familien.WerbenVerboten(ki2, x) is null), "ein Mensch für die KI");
            foreach (var x in markt.Where(x => w.Familien.WerbenVerboten(ki2, x) is null)) w.Familien.ZuneigungSchenken(ki2, x.Person.Nummer, 500);
            w.KiAnmelden(ki2, Reichsgilde.Core.Ki.KiProfil.Standard);
            for (var i = 0; i < 12 * 60 - Reichsgilde.Core.Zeit.Spielzeit.TagesbeginnMinute; i++) sim.Tick(); // über die Politikstunde
            Pruefe.Gleich((Konfession?)Konfession.Evangelisch, w.Glaube.GlaubeVon(ki));
            Pruefe.Wahr(f2.Ehepartner is { Volk: Volk.Mensch }, "KI hat einen Menschen geheiratet: " + string.Join(" | ", w.Rivalen.Single(r => r.Konto == ki2).Entscheidungen));
            Pruefe.Wahr(f2.Lebende.All(x => x.Glaube == Konfession.Neuahtnisch), "KI-Haus neuahtnisch");
        }),

        ("Ahtnentum Dom: Ein altahtnischer Domprediger heiratet einen Menschen – das Haus wird neuahtnisch und verliert das Amt wie beim Übertritt", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var dom = Prediger(sim, "Haus Tiefstein", Konfession.Altahtnisch, Volk.Zwerg);
            w.Domamt.Einsetzen(dom, sim.Zeit.Jahr);
            Pruefe.Gleich(dom, w.Domamt.Inhaber);
            var mensch = w.Familien.Kandidaten.First(x => x.Person.Volk == Volk.Mensch && w.Familien.WerbenVerboten(dom, x) is null);
            w.Familien.ZuneigungSchenken(dom, mensch.Person.Nummer, 500);
            var e = w.Ausfuehren(dom, new Heiraten(mensch.Person.Nummer));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich((Konfession?)Konfession.Neuahtnisch, w.Glaube.GlaubeVon(dom));
            Pruefe.Gleich(null, w.Domamt.Inhaber);
        }),

        ("Ahtnentum Spielstand: Glaube und Anteile überstehen Speichern und Laden; ein alter Stand ohne ahtnische Anteile bekommt sie dazu (Summe 1)", () =>
        {
            var sim = NeueSimulation();
            var (w, k, _) = Haus(sim, "Haus Tiefstein", Konfession.Altahtnisch, Volk.Zwerg);
            var text = Speicherung.Speichern(sim, k);
            var (kopie, kk) = Speicherung.Laden(text);
            Pruefe.Gleich((Konfession?)Konfession.Altahtnisch, kopie.Wirtschaft!.Glaube.GlaubeVon(kk));
            foreach (var x in KonfessionTexte.Kirchliche)
                Pruefe.Ungefaehr(w.Glaube.Anteil(x), kopie.Wirtschaft.Glaube.Anteil(x), 1e-12);

            // Alter Stand: nur katholisch 0,55 und evangelisch 0,45.
            var knoten = JsonNode.Parse(text)!;
            var anteile = AnteileFinden(knoten) ?? throw new Exception("Anteile im Spielstand nicht gefunden");
            foreach (var name in anteile.Select(x => x.Key).ToList()) anteile.Remove(name);
            anteile["Katholisch"] = 0.55;
            anteile["Evangelisch"] = 0.45;
            var (alt, _) = Speicherung.Laden(knoten.ToJsonString());
            var g = alt.Wirtschaft!.Glaube;
            Pruefe.Gleich(4, g.Anteile.Count);
            Pruefe.Ungefaehr(1.0, g.Anteile.Values.Sum(), 1e-9);
            Pruefe.Ungefaehr(Daten.Value.Glaube!.Stadt[Konfession.Altahtnisch], g.Anteil(Konfession.Altahtnisch), 1e-12);
            // Die alten geben nur ab, was über dem Mindestanteil liegt: frei = 0,75 − 2 × 0,1, über = 0,45 + 0,35.
            Pruefe.Ungefaehr(0.1 + 0.45 * 0.55 / 0.8, g.Anteil(Konfession.Katholisch), 1e-9);
            Pruefe.Ungefaehr(0.1 + 0.35 * 0.55 / 0.8, g.Anteil(Konfession.Evangelisch), 1e-9);

            // Ein alter Stand mit kleiner Gemeinde: kein Anteil fällt unter den Mindestanteil.
            anteile["Katholisch"] = 0.88;
            anteile["Evangelisch"] = 0.12;
            var (klein, _) = Speicherung.Laden(knoten.ToJsonString());
            var gk = klein.Wirtschaft!.Glaube;
            Pruefe.Ungefaehr(1.0, gk.Anteile.Values.Sum(), 1e-9);
            Pruefe.Wahr(gk.Anteile.Values.All(a => a >= 0.1 - 1e-12), string.Join(", ", gk.Anteile.Select(x => $"{x.Key} {x.Value}")));
        }),
    };

    /// <summary>Das Anteile-Objekt des Glaubens im Spielstand (Name unabhängig von Groß-/Kleinschreibung).</summary>
    static JsonObject? AnteileFinden(JsonNode? n)
    {
        if (n is JsonObject o)
        {
            foreach (var (name, wert) in o)
            {
                if (name.Equals("anteile", StringComparison.OrdinalIgnoreCase) && wert is JsonObject a && a.Any(x => x.Key == "Katholisch")) return a;
                if (AnteileFinden(wert) is { } t) return t;
            }
        }
        else if (n is JsonArray arr)
            foreach (var x in arr)
                if (AnteileFinden(x) is { } t) return t;
        return null;
    }
}
