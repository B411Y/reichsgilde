using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Neue Partie und Höfe (Nutzerentscheidungen 11.11.2026, <see cref="Partiebesetzung"/>): nummerierte Höfe (Hof 1 = Spieler 1, höchstens acht),
/// gewürfelte KI-Häuser (Name, Wappen, Volk, Glaube, Beruf), „Erweitert“ mit KI-Vorgaben, Würfeln für den Spieler, Züge des Spielers mit dem
/// Befehl „Züge neu würfeln“, KI-Stärke Leicht/Normal/Schwer. Jede Prüfung mit Gegenprobe.
/// </summary>
static class NeuePartieTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Stadtkarte.KatalogZu(Datenpfad.Karte("greybox"))));

    /// <summary>
    /// Eine Karte im knappen Format: nur Hofstellen mit Nummer (in Kartenreihenfolge 1, 2, …), ohne „haeuser“ und „spieler“ – wie die Kartenwerkstatt
    /// sie künftig schreibt. <paramref name="nummern"/>: abweichende Nummern je Start-Hofstelle (null = ohne Nummer, Startplatz wie bisher).
    /// </summary>
    static string KnappJson(string kennung, Func<int, int?>? nummern = null)
    {
        var k = JsonNode.Parse(File.ReadAllText(Datenpfad.Karte(kennung)))!.AsObject();
        var start = k["start"]!.AsObject();
        start.Remove("haeuser");
        start.Remove("spieler");
        start.Remove("jeBeruf");
        var i = 0;
        foreach (var h in start["hofstellen"]!.AsArray().Select(x => x!.AsObject()).ToList())
        {
            if (h["start"] is null) continue; // freie Hofstellen bleiben ohne Nummer
            h.Remove("start");
            var n = nummern is null ? i + 1 : nummern(i);
            if (n is { } nr) h["nummer"] = nr;
            else h["start"] = true; // ohne Nummer ein Startplatz wie bisher
            i++;
        }
        return k.ToJsonString();
    }

    static Stadtkarte Knapp(string kennung, Func<int, int?>? nummern = null) => Stadtkarte.AusJson(KnappJson(kennung, nummern), Katalog.Value);

    static (Stadtwirtschaft W, Konto Spieler) Partie(Stadtkarte? karte, NeuePartie neu, int startwert = 1400, Wirtschaftsdaten? daten = null)
    {
        var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos },
            daten ?? Daten.Value, neu, karte: karte);
        return (sim.Wirtschaft!, spieler);
    }

    /// <summary>Je Haus (Kontoreihenfolge) der Beruf seiner ersten Werkstatt in der Stadt.</summary>
    static List<string> Berufe(Stadtwirtschaft w) =>
        w.Konten.Select(k => w.Betriebe.Where(b => b.Konto == k && w.Daten.Art(b.Art).InDerStadt).Select(b => w.Daten.Art(b.Art).Beruf).First()).ToList();

    static List<Charakterzug> Persoenlichkeit(Stadtwirtschaft w, Konto k) =>
        w.Familien.Von(k)!.Oberhaupt.Zuege.Where(z => z.Art == Zugart.Persoenlichkeit).ToList();

    static bool OhneGegenteile(IReadOnlyList<Charakterzug> zuege) => !zuege.Any(z => zuege.Any(x => x.Kennung == z.Gegenteil));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Neue Partie: knappe Karte – Hof 1 der Spieler, Hof 2 … n die KI-Häuser der Reihe nach; Namen, Wappen, Volk, Glaube und Beruf gewürfelt, alle verschieden", () =>
        {
            var karte = Knapp("messstadt", i => i < 8 ? i + 1 : null);
            Pruefe.Gleich(8, Partiebesetzung.Hoefe(karte));
            Pruefe.Gleich(7, Partiebesetzung.HoechstensKi(karte));
            var hoefe = Hofstellenvergabe.Startplaetze(karte);
            var (w, spieler) = Partie(karte, new NeuePartie(Rivalen: 7));
            var start = w.Karte!.Startbelegung;
            Pruefe.Gleich(hoefe[0].Werkstatt, start.Spieler.Werkstatt.Platz, "Spieler auf Hof 1");
            Pruefe.Gleich(7, start.Haeuser.Count);
            for (var i = 0; i < 7; i++)
                Pruefe.Gleich(hoefe[i + 1].Wohnhaus, start.Haeuser[i].Wohnhaus, $"KI-Haus {i + 1} auf Hof {i + 2}");
            var namen = w.Konten.Select(k => k.Inhaber).ToList();
            Pruefe.Gleich(8, namen.Distinct().Count(), string.Join(", ", namen));
            Pruefe.Wahr(start.Haeuser.All(h => h.Name.StartsWith("Haus ") && !Partieaufbau.Rivalennamen.Contains(h.Name)), string.Join(", ", namen));
            Pruefe.Gleich(8, w.Konten.Select(k => k.Wappen).Distinct().Count(), "Wappen verschieden");
            Pruefe.Gleich(7, start.Haeuser.Select(h => h.Farbe).OfType<string>().Distinct().Count(), "Farben verschieden");
            var berufe = Berufe(w);
            Pruefe.Gleich(8, berufe.Distinct().Count(), string.Join(", ", berufe));
            foreach (var h in start.Haeuser)
            {
                Pruefe.Wahr(Startaufstellung.KiVoelker.Contains(h.Volk), $"{h.Name}: {h.Volk}");
                Pruefe.Wahr(h.Glaube is { } g && Startaufstellung.Startglauben(h.Volk).Contains(g), $"{h.Name}: {h.Glaube}");
                var familiennamen = w.Daten.Dynastie.Voelker[h.Volk].Familiennamen;
                Pruefe.Wahr(familiennamen.Contains(Familie.NachnameAus(h.Name)), $"{h.Name} ist ein Name der {h.Volk.Mehrzahl()}");
            }
            // Gegenprobe: dieselbe Karte ohne Nummern (Startplätze wie bisher) lost die Höfe – nicht immer sitzen alle der Reihe nach
            var gelost = Knapp("messstadt", _ => null);
            Pruefe.Wahr(!Hofstellenvergabe.Nummeriert(gelost), "ohne Nummern");
            var plaetze = Hofstellenvergabe.Startplaetze(gelost);
            Pruefe.Wahr(Enumerable.Range(1, 5).Any(s =>
            {
                var (wg, _) = Partie(gelost, new NeuePartie(Rivalen: 7), s);
                return wg.Karte!.Startbelegung.Spieler.Werkstatt.Platz != plaetze[0].Werkstatt;
            }), "gelost: der Spieler nicht immer auf der ersten");
        }),

        ("Neue Partie: gewürfelt ist deterministisch – gleicher Startwert, gleiche Häuser; ein anderer Startwert würfelt andere Namen", () =>
        {
            var karte = Knapp("messstadt", i => i < 8 ? i + 1 : null);
            string Bild(int s)
            {
                var (w, _) = Partie(karte, new NeuePartie(Rivalen: 7), s);
                return string.Join("|", w.Karte!.Startbelegung.Haeuser.Select(h => $"{h.Name}:{h.Volk}:{h.Glaube}:{h.Geschlecht}:{h.Wappen}:{h.Betriebe[0].Art}"));
            }
            Pruefe.Gleich(Bild(7), Bild(7));
            Pruefe.Wahr(new[] { 8, 9, 10 }.Any(s => Bild(s) != Bild(7)), "andere Startwerte, andere Häuser");
        }),

        ("Neue Partie: nie zwei gleiche Berufe – 60 Startwerte × 0 bis 7 KI auf der knappen Karte, jeder Spielerberuf; Gegenprobe: zwei gleiche KI-Vorgaben werden abgewiesen", () =>
        {
            var karte = Knapp("messstadt", i => i < 8 ? i + 1 : null);
            var alle = Startaufstellung.Berufe(Daten.Value);
            for (var s = 1; s <= 60; s++)
            {
                var beruf = alle[s % alle.Count];
                var (w, _) = Partie(karte, new NeuePartie(Beruf: beruf, Rivalen: s % 8), s);
                var berufe = Berufe(w);
                Pruefe.Gleich(1 + s % 8, berufe.Count, $"Startwert {s}");
                Pruefe.Gleich(berufe.Count, berufe.Distinct().Count(), $"Startwert {s}: {string.Join(", ", berufe)}");
            }
            Pruefe.WirftMitMeldung(() => Partie(karte, new NeuePartie(Rivalen: 3,
                KiVorgaben: new Hausvorgabe?[] { new(Beruf: "Tischler"), null, new(Beruf: "Tischler") })), "Tischler");
            Partie(karte, new NeuePartie(Rivalen: 3, KiVorgaben: new Hausvorgabe?[] { new(Beruf: "Tischler"), null, new(Beruf: "Steinmetz") }));
        }),

        ("Neue Partie: die Spielerwahl hat Vorrang – kein KI-Haus bekommt seinen Beruf, eine KI-Vorgabe mit seinem Beruf wird abgewiesen, Würfeln lässt ihm seine Wahl", () =>
        {
            var karte = Knapp("messstadt", i => i < 8 ? i + 1 : null);
            for (var s = 1; s <= 30; s++)
            {
                var (w, _) = Partie(karte, new NeuePartie(Beruf: "Tischler", Rivalen: 7), s);
                Pruefe.Gleich(1, Berufe(w).Count(b => b == "Tischler"), $"Startwert {s}");
                Pruefe.Gleich("Tischler", Berufe(w)[0]);
            }
            Pruefe.WirftMitMeldung(() => Partie(karte, new NeuePartie(Beruf: "Tischler", Rivalen: 2, KiVorgaben: new Hausvorgabe?[] { new(Beruf: "Tischler") })), "Tischler");
            // Gegenprobe: mit einem anderen Spielerberuf bekommt das KI-Haus den Tischler
            var (w2, _) = Partie(karte, new NeuePartie(Beruf: "Schmied", Rivalen: 2, KiVorgaben: new Hausvorgabe?[] { new(Beruf: "Tischler") }));
            Pruefe.Gleich("Tischler", Berufe(w2)[1]);
            // Würfeln für den Spieler: sein festgelegter Beruf gilt, auch wenn ihn eine KI-Vorgabe nennt (die fällt dann beim Aufbau durch);
            // gewürfelt bekommt er nie einen Beruf der KI-Vorgaben
            var d = Daten.Value;
            Pruefe.Gleich("Tischler", Partiebesetzung.SpielerWuerfeln(d, karte, 5, new Hausvorgabe(Beruf: "Tischler"), new Hausvorgabe?[] { new(Beruf: "Tischler") }).Beruf);
            var vorgaben = Startaufstellung.Berufe(d).Where(b => b != "Schmied").Select(b => (Hausvorgabe?)new Hausvorgabe(Beruf: b)).ToList();
            for (var s = 1; s <= 30; s++)
                Pruefe.Gleich("Schmied", Partiebesetzung.SpielerWuerfeln(d, karte, s, kiVorgaben: vorgaben).Beruf, $"Startwert {s}");
        }),

        ("Neue Partie: höchstens acht Höfe – neun nummerierte zählen als acht (Hof 9 ist ein Fehler der Prüfung), höchstens sieben KI; die Kartenwerkstatt nimmt keine 9", () =>
        {
            var neun = Knapp("messstadt"); // S1 … S9 mit Nummer 1 … 9
            Pruefe.Gleich(9, neun.Hofstellen.Count(h => h.Nummer is not null));
            Pruefe.Gleich(8, Partiebesetzung.Hoefe(neun));
            Pruefe.Gleich(7, Partiebesetzung.HoechstensKi(neun));
            Pruefe.WirftMitMeldung(() => Partie(neun, new NeuePartie(Rivalen: 8)), "null bis sieben");
            Partie(neun, new NeuePartie(Rivalen: 7));
            var befunde = Kartenpruefung.Pruefen(Kartenentwurf.AusJson(KnappJson("messstadt"), Katalog.Value));
            Pruefe.Wahr(befunde.Any(b => b.Regel == "hofstelle" && b.Text.Contains("nicht 9")), "Hof 9 ist ein Fehler");
            // Gegenprobe: acht Nummern ohne Lücke sind kein Fehler; eine Lücke ist einer
            var acht = Kartenpruefung.Pruefen(Kartenentwurf.AusJson(KnappJson("messstadt", i => i < 8 ? i + 1 : null), Katalog.Value));
            Pruefe.Wahr(!acht.Any(b => b.Regel == "hofstelle" && b.Schwere == Schwere.Fehler), string.Join("; ", acht.Where(b => b.Regel == "hofstelle").Select(b => b.Text)));
            var luecke = Kartenpruefung.Pruefen(Kartenentwurf.AusJson(KnappJson("messstadt", i => i == 0 ? 1 : i == 1 ? 3 : null), Katalog.Value));
            Pruefe.Wahr(luecke.Any(b => b.Regel == "hofstelle" && b.Schwere == Schwere.Fehler && b.Text.Contains("Hof 2 fehlt")), "Lücke");
            var e = Kartenentwurf.AusJson(KnappJson("messstadt", i => i < 8 ? i + 1 : null), Katalog.Value);
            var frei = e.Start.Hofstellen[8].Kennung; // S9 ohne Nummer
            Pruefe.Wirft(() => e.HofstelleNummerSetzen(frei, 9));
            Pruefe.Wirft(() => e.HofstelleNummerSetzen(frei, 3)); // Hof 3 gibt es schon
            e.HofstelleNummerSetzen(e.Start.Hofstellen[7].Kennung, null);
            e.HofstelleNummerSetzen(frei, 8);
            Pruefe.Gleich(8, e.Start.Hofstellen[8].Nummer);
            Pruefe.Gleich((int?)8, Stadtkarte.AusJson(e.ZuJson(), Katalog.Value).HofstelleMit(frei)!.Nummer, "Nummer wird geschrieben und gelesen");
        }),

        ("Neue Partie: alte Karten im ausführlichen Format laden wie bisher – Häuser der Karte mit ihren Namen, Höfe und KI-Höchstzahl unverändert, keine Nummern", () =>
        {
            foreach (var (kennung, hoefe, ki) in new[] { ("greybox", 4, 3), ("koeln", 8, 7), ("messstadt", 9, 8), ("probestadt", 3, 2) })
            {
                var karte = Stadtkarte.AusDatei(Datenpfad.Karte(kennung));
                Pruefe.Wahr(!Hofstellenvergabe.Nummeriert(karte), kennung);
                Pruefe.Gleich(hoefe, Partiebesetzung.Hoefe(karte), kennung);
                Pruefe.Gleich(ki, Partiebesetzung.HoechstensKi(karte), kennung);
                Pruefe.Gleich(karte.Startbelegung.Haeuser.Count, ki, kennung);
                var (w, _) = Partie(karte, new NeuePartie(Rivalen: ki), 11);
                var namen = karte.Startbelegung.Haeuser.Select(h => h.Name).ToList();
                Pruefe.Gleich(string.Join(",", namen), string.Join(",", w.Konten.Skip(1).Select(k => k.Inhaber)), $"{kennung}: die Häuser der Karte");
            }
            Pruefe.Gleich(4, Partiebesetzung.Hoefe(null));
            Pruefe.Gleich(3, Partiebesetzung.HoechstensKi(null));
            // Gegenprobe: dieselbe alte Karte nimmt kein Haus mehr, als sie Startplätze hat
            Pruefe.WirftMitMeldung(() => Partie(Stadtkarte.AusDatei(Datenpfad.Karte("greybox")), new NeuePartie(Rivalen: 4)), "null bis drei");
        }),

        ("Neue Partie: ohne KI-Gegner – null Rivalen gehen, der Spieler ist allein; Gegenprobe −1 wird abgewiesen", () =>
        {
            var (w, _) = Partie(Stadtkarte.AusDatei(Datenpfad.Karte("greybox")), new NeuePartie(Rivalen: 0));
            Pruefe.Gleich(1, w.Konten.Count);
            Pruefe.Gleich(0, w.Rivalen.Count);
            Pruefe.Wirft(() => Partie(null, new NeuePartie(Rivalen: -1)));
        }),

        ("Neue Partie: Erweitert – jedes KI-Haus einzeln festgelegt (Name, Wappen, Volk, Glaube, Beruf), auch ein Haus einer alten Karte; Unpassendes wird abgewiesen", () =>
        {
            var wappen = new Wappen(Teilung.Geviert, Tinktur.Rot, Tinktur.Silber, Figur.Rad);
            var vorgaben = new Hausvorgabe?[]
            {
                new("Familie Brenner", wappen, Volk.Mensch, Konfession.Evangelisch, "Tischler"),
                null,
                new(Volk: Volk.Zwerg, Beruf: "Steinmetz"),
            };
            foreach (var karte in new[] { Knapp("messstadt", i => i < 8 ? i + 1 : null), Stadtkarte.AusDatei(Datenpfad.Karte("koeln")) })
            {
                var (w, _) = Partie(karte, new NeuePartie(Rivalen: 3, KiVorgaben: vorgaben));
                var h = w.Karte!.Startbelegung.Haeuser;
                Pruefe.Gleich("Haus Brenner", h[0].Name);
                Pruefe.Gleich(wappen, h[0].Wappen);
                Pruefe.Gleich(Volk.Mensch, h[0].Volk);
                Pruefe.Gleich(Konfession.Evangelisch, h[0].Glaube);
                Pruefe.Gleich(Volk.Zwerg, h[2].Volk);
                Pruefe.Gleich("Tischler,Steinmetz", $"{Berufe(w)[1]},{Berufe(w)[3]}");
                Pruefe.Wahr(w.Betriebe.Any(b => b.Konto == w.Konten[1] && b.Name.EndsWith("Brenner")), "die Werkstatt heißt nach dem Haus");
            }
            var knapp = Knapp("messstadt", i => i < 8 ? i + 1 : null);
            Pruefe.WirftMitMeldung(() => Partie(knapp, new NeuePartie(Rivalen: 1, KiVorgaben: new Hausvorgabe?[] { new(Volk: Volk.Ork) })), "Bürgerrecht");
            Pruefe.Wirft(() => Partie(knapp, new NeuePartie(Rivalen: 1, KiVorgaben: new Hausvorgabe?[] { new(Volk: Volk.Mensch, Glaube: Konfession.Naturglaube) })));
            Pruefe.WirftMitMeldung(() => Partie(knapp, new NeuePartie(Rivalen: 2, KiVorgaben: new Hausvorgabe?[] { new("Hammerschmid") })), "zwei Häuser");
            Pruefe.WirftMitMeldung(() => Partie(knapp, new NeuePartie(Rivalen: 2, KiVorgaben: new Hausvorgabe?[] { new(Wappen: wappen), new(Wappen: wappen) })), "Wappen");
            Pruefe.Wirft(() => Partie(knapp, new NeuePartie(Rivalen: 1, KiVorgaben: new Hausvorgabe?[] { null, null })));
            Pruefe.Wirft(() => Partie(Stadtkarte.AusDatei(Datenpfad.Karte("greybox")), new NeuePartie(Rivalen: 1, Aufstellung: Aufstellungsart.Vorgabe,
                KiVorgaben: new Hausvorgabe?[] { new(Beruf: "Tischler") })));
            // ein festgelegter Glaube ohne Volk würfelt ein passendes Volk
            for (var s = 1; s <= 20; s++)
            {
                var (w, _) = Partie(knapp, new NeuePartie(Rivalen: 1, KiVorgaben: new Hausvorgabe?[] { new(Glaube: Konfession.Evangelisch) }), s);
                Pruefe.Gleich(Konfession.Evangelisch, w.Karte!.Startbelegung.Haeuser[0].Glaube);
            }
        }),

        ("Neue Partie: Würfeln für den Spieler – nur, was er offen lässt; Volk mit Bürgerrecht, Glaube zum Volk, Name seines Volkes, Wappen keines KI-Hauses", () =>
        {
            var d = Daten.Value;
            var koeln = Stadtkarte.AusDatei(Datenpfad.Karte("koeln"));
            var voelker = new HashSet<Volk>();
            for (var s = 1; s <= 80; s++)
            {
                var b = Partiebesetzung.SpielerWuerfeln(d, koeln, s);
                voelker.Add(b.Volk);
                Pruefe.Wahr(Startaufstellung.KiVoelker.Contains(b.Volk), $"{s}: {b.Volk}");
                Pruefe.Wahr(Startaufstellung.Startglauben(b.Volk).Contains(b.Glaube), $"{s}: {b.Volk} {b.Glaube}");
                Pruefe.Wahr(d.Dynastie.Voelker[b.Volk].Familiennamen.Contains(b.Familienname), $"{s}: {b.Familienname}");
                Pruefe.Wahr(!koeln.Startbelegung.Rivalennamen.Contains(Partieaufbau.Hausname(b.Familienname)), b.Familienname);
                Pruefe.Wahr(!koeln.Startbelegung.Rivalenwappen.Contains(b.Wappen) && b.Wappen.Fehler() is null, $"{s}: {b.Wappen}");
                Pruefe.Wahr(Startaufstellung.Berufe(d).Contains(b.Beruf), b.Beruf);
                Pruefe.Gleich(b, Partiebesetzung.SpielerWuerfeln(d, koeln, s), "deterministisch");
                // die Partie nimmt das Gewürfelte
                if (s <= 3)
                    Partie(koeln, new NeuePartie(b.Familienname, Volk: b.Volk, Beruf: b.Beruf, Glaube: b.Glaube, Wappen: b.Wappen, Rivalen: 7), s);
            }
            Pruefe.Gleich(4, voelker.Count, "alle vier Völker kommen vor");
            // Festgelegtes bleibt, das übrige wird gewürfelt wie ohne Festlegung (dieselbe Reihe von Würfen)
            var frei = Partiebesetzung.SpielerWuerfeln(d, koeln, 9);
            var fest = Partiebesetzung.SpielerWuerfeln(d, koeln, 9, new Hausvorgabe(Name: "Haus Kranich", Beruf: "Wirt"));
            Pruefe.Gleich("Kranich", fest.Familienname);
            Pruefe.Gleich("Wirt", fest.Beruf);
            Pruefe.Gleich(frei.Wappen, fest.Wappen);
            Pruefe.Gleich(frei.Volk, fest.Volk);
            // Gegenprobe: Orks sind nicht spielbar, ein unpassender Glaube auch nicht
            Pruefe.Wirft(() => Partiebesetzung.SpielerWuerfeln(d, koeln, 1, new Hausvorgabe(Volk: Volk.Oger)));
            Pruefe.Wirft(() => Partiebesetzung.SpielerWuerfeln(d, koeln, 1, new Hausvorgabe(Glaube: Konfession.Naturglaube)));
        }),

        ("Neue Partie: Züge des Spielers – immer drei, nie zwei Gegenteile, über 400 Würfe und alle Völker; die Partie gibt ihm genau diese; Gegenprobe: Gegenteile werden abgewiesen", () =>
        {
            var d = Daten.Value;
            var je = d.Charakterzuege!.JePerson;
            Pruefe.Gleich(3, je);
            foreach (var volk in Startaufstellung.KiVoelker)
                for (var wurf = 0; wurf < 100; wurf++)
                {
                    var z = Partiebesetzung.SpielerZuege(d, 1400 + wurf, volk, wurf);
                    Pruefe.Gleich(je, z.Count, $"{volk} {wurf}");
                    Pruefe.Gleich(je, z.Distinct().Count(), $"{volk} {wurf}");
                    Pruefe.Wahr(z.All(x => x.Art == Zugart.Persoenlichkeit), $"{volk} {wurf}");
                    Pruefe.Wahr(OhneGegenteile(z), $"{volk} {wurf}: {string.Join(", ", z)}");
                }
            var (w, spieler) = Partie(null, new NeuePartie(ZugWurf: 4), 77);
            var erwartet = Partiebesetzung.SpielerZuege(d, 77, Volk.Mensch, 4);
            Pruefe.Gleich(string.Join(",", erwartet.Select(x => x.Kennung)), string.Join(",", Persoenlichkeit(w, spieler).Select(x => x.Kennung)));
            foreach (var k in w.Konten)
            {
                var p = Persoenlichkeit(w, k);
                Pruefe.Gleich(je, p.Count, k.Inhaber);
                Pruefe.Wahr(OhneGegenteile(p), k.Inhaber);
            }
            // Gegenprobe: ein Paar Gegenteile lässt sich nicht vorgeben
            var paar = d.Charakterzuege.Alle.First(x => x.Art == Zugart.Persoenlichkeit && x.Gegenteil is not null);
            var gegen = d.Charakterzuege.Zug(paar.Gegenteil!)!;
            var (w2, _) = Partie(null, new NeuePartie(Rivalen: 1), 5);
            var konto = w2.KontoEroeffnen("Haus Probe");
            Pruefe.Wirft(() => w2.Familien.Gruenden(konto, Volk.Mensch, Geschlecht.Mann, zuege: new[] { paar, gegen }));
        }),

        ("Neue Partie: Befehl „Züge neu würfeln“ – zählt den Wurf weiter, gleicher Startwert und Wurf = gleiche Züge, ein neuer Wurf andere; ohne Wurf wie bisher", () =>
        {
            var d = Daten.Value;
            var neu = new NeuePartie();
            Pruefe.Gleich((int?)null, neu.ZugWurf);
            var eins = Partiebesetzung.Ausfuehren(neu, new ZuegeNeuWuerfeln());
            var zwei = Partiebesetzung.Ausfuehren(eins, new ZuegeNeuWuerfeln());
            Pruefe.Gleich((int?)1, eins.ZugWurf);
            Pruefe.Gleich((int?)2, zwei.ZugWurf);
            Pruefe.Gleich(Partiebesetzung.Ausfuehren(neu, new ZuegeNeuWuerfeln()), eins);
            string Bild(int s, int wurf) => string.Join(",", Partiebesetzung.SpielerZuege(d, s, Volk.Mensch, wurf).Select(x => x.Kennung));
            Pruefe.Gleich(Bild(1400, 2), Bild(1400, 2));
            var verschieden = Enumerable.Range(0, 20).Select(wurf => Bild(1400, wurf)).Distinct().Count();
            Pruefe.Wahr(verschieden >= 10, $"20 Würfe, {verschieden} verschiedene");
            // zwei Partien mit demselben Wurf: dieselben Züge; die Züge der KI-Häuser ändert der Wurf nicht
            string Zuege(Stadtwirtschaft w, Konto k) => string.Join(",", Persoenlichkeit(w, k).Select(x => x.Kennung));
            var (a, sa) = Partie(null, zwei, 1400);
            var (b, sb) = Partie(null, zwei, 1400);
            var (c, sc) = Partie(null, eins, 1400);
            Pruefe.Gleich(Zuege(a, sa), Zuege(b, sb));
            Pruefe.Gleich(Bild(1400, 2), Zuege(a, sa));
            Pruefe.Gleich(Bild(1400, 1), Zuege(c, sc));
            for (var i = 1; i < a.Konten.Count; i++)
                Pruefe.Gleich(Zuege(a, a.Konten[i]), Zuege(c, c.Konten[i]), a.Konten[i].Inhaber);
            // Gegenprobe: ohne Wurf (alte Aufrufe) würfelt die Familie wie bisher – nicht die Züge des Startbildschirms
            var (o, so) = Partie(null, new NeuePartie(), 1400);
            Pruefe.Wahr(Enumerable.Range(0, 4).Any(wurf => Bild(1400, wurf) != Zuege(o, so)), "ohne Wurf andere Quelle");
        }),

        ("Neue Partie: KI-Stärke – Normal wie bisher (Startgeld, Profil, Verlauf wie ohne Abschnitt „kiStaerke“); Leicht weniger Geld und vorsichtiger, Schwer mehr Geld und wagemutiger", () =>
        {
            var d = Daten.Value;
            Pruefe.Gleich(KiStaerke.Normal, new NeuePartie().KiStaerke);
            Pruefe.Gleich((double?)null, Partiebesetzung.Startgeld(d, KiStaerke.Normal));
            Pruefe.Gleich(KiProfil.Standard, Partiebesetzung.Profil(KiProfil.Standard, d, KiStaerke.Normal));
            var ohne = Wirtschaftsdaten.AusJson(System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(Datenpfad.Wirtschaft()),
                "\"kiStaerke\": \\{[^}]*\\}[^}]*\\}\\s*\\},", ""));
            Pruefe.Wahr(ohne.KiStaerke is null && d.KiStaerke is not null, "Abschnitt entfernt");
            string Verlauf(Wirtschaftsdaten daten, KiStaerke staerke)
            {
                var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, daten,
                    new NeuePartie(KiStaerke: staerke), karte: Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));
                for (var t = 0; t < 600; t++) sim.Tick();
                var w = sim.Wirtschaft!;
                return string.Join("|", w.Konten.Select(k => Zahlenformat.Kommazahl(k.Vermoegen))) + "#" + string.Join("|", w.Rivalen.Select(r => r.Profil));
            }
            var normal = Verlauf(d, KiStaerke.Normal);
            Pruefe.Gleich(normal, Verlauf(ohne, KiStaerke.Normal), "Normal wie ohne Abschnitt");
            Pruefe.Gleich(normal, Verlauf(ohne, KiStaerke.Schwer), "ohne Abschnitt spielt jede Stufe wie Normal");
            Pruefe.Wahr(Verlauf(d, KiStaerke.Schwer) != normal, "Gegenprobe: Schwer ändert etwas");

            var startgeld = d.Allgemein.Startvermoegen;
            var (wl, _) = Partie(null, new NeuePartie(KiStaerke: KiStaerke.Leicht));
            var (wn, _) = Partie(null, new NeuePartie(KiStaerke: KiStaerke.Normal));
            var (ws, _) = Partie(null, new NeuePartie(KiStaerke: KiStaerke.Schwer));
            // gleiche Ausgaben am Start (Lehrling) – der Unterschied ist genau der Faktor auf das Startgeld
            Pruefe.Ungefaehr(startgeld * (d.KiStaerke!.Schwer!.Startgeld - 1), ws.Konten[1].Vermoegen - wn.Konten[1].Vermoegen, 0.001);
            Pruefe.Ungefaehr(startgeld * (1 - d.KiStaerke.Leicht!.Startgeld), wn.Konten[1].Vermoegen - wl.Konten[1].Vermoegen, 0.001);
            Pruefe.Wahr(wl.Konten[1].Vermoegen < wn.Konten[1].Vermoegen && wn.Konten[1].Vermoegen < ws.Konten[1].Vermoegen,
                $"{wl.Konten[1].Vermoegen} < {wn.Konten[1].Vermoegen} < {ws.Konten[1].Vermoegen}");
            Pruefe.Gleich(wn.Konten[0].Vermoegen, ws.Konten[0].Vermoegen, "der Spieler hat sein Startgeld, gleich welche Stärke");
            var (l, n, s) = (wl.Rivalen[0].Profil, wn.Rivalen[0].Profil, ws.Rivalen[0].Profil);
            Pruefe.Wahr(l.Reserve > n.Reserve && l.Vorsicht > n.Vorsicht && l.MaxAmortisationJahre < n.MaxAmortisationJahre, $"Leicht vorsichtiger: {l}");
            Pruefe.Wahr(s.Reserve < n.Reserve && s.Vorsicht < n.Vorsicht && s.MaxAmortisationJahre > n.MaxAmortisationJahre, $"Schwer wagemutiger: {s}");
            Pruefe.Gleich(Partieaufbau.ProfilFuer(KiCharakter.Ausgewogen), n);
            // Datenprüfung: Leicht mit mehr Geld ist ein Fehler
            var falsch = new KiStaerkeDaten { Leicht = new KiStaerkeStufe { Startgeld = 1.5 } };
            var f = new List<string>();
            typeof(KiStaerkeDaten).GetMethod("Pruefen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(falsch, new object[] { f });
            Pruefe.Wahr(f.Count > 0, "Leicht mit mehr Startgeld wird abgewiesen");
        }),
    };
}
