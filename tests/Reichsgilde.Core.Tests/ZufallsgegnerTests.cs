using System.IO.Compression;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Keine festen Dynastien mehr (Nutzer 17.11.2026): Die Karten nennen keine KI-Häuser. Jede Partie würfelt genau so viele KI-Gegner, wie gewählt
/// sind – Name, Wappen, Geschlecht, Volk, Glaube und Beruf –, nach den Regeln der Startaufstellung. Erst der Spieler, dann bekommt jedes KI-Haus
/// eine noch freie Hauptfarbe; kein Name doppelt. Die früheren Häuser bleiben als Familiennamen der Stadt („hausnamen“), ihre Umlandbetriebe als
/// Umland-Startplätze („umland“). Eine ältere Karte mit festen Häusern ergibt dieselbe Partie; ein älterer Spielstand lädt unverändert.
/// </summary>
static class ZufallsgegnerTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static readonly string[] Karten = { "greybox", "koeln", "probestadt", "messstadt" };
    static readonly string[] Koelner = { "Overstolz", "Hardevust", "Lyskirchen", "Gryn", "Scherfgin", "Mommersloch", "Kleingedank" };

    static string Text(string kennung) => File.ReadAllText(Datenpfad.Karte(kennung));
    static Stadtkarte Karte(string kennung) => Stadtkarte.AusJson(Text(kennung), Katalog.Value);
    static readonly Lazy<Stadtkarte> Koeln = new(() => Karte("koeln"));

    /// <summary>Köln, wie die Karte bis zum 17.11.2026 war: sieben feste Häuser mit Wappen und ihrem Umlandbetrieb, ohne „umland“ und „hausnamen“.</summary>
    static string AltesKoeln()
    {
        var k = JsonNode.Parse(Text("koeln"))!.AsObject();
        var start = k["start"]!.AsObject();
        var umland = start["umland"]!.AsArray();
        var haeuser = new JsonArray();
        for (var i = 0; i < Koelner.Length; i++)
        {
            var w = Wappen.Vorlagen[i + 1];
            haeuser.Add(new JsonObject
            {
                ["name"] = "Haus " + Koelner[i], ["volk"] = "Zwerg", ["geschlecht"] = "Mann",
                ["wappen"] = new JsonObject { ["teilung"] = w.Teilung.ToString(), ["farbe1"] = w.Farbe1.ToString(), ["farbe2"] = w.Farbe2.ToString(), ["figur"] = w.Figur.ToString() },
                ["betriebe"] = new JsonArray(new JsonObject { ["art"] = "Giesserei" }, umland[i]!.DeepClone()),
            });
        }
        k["start"] = new JsonObject { ["haeuser"] = haeuser };
        return k.ToJsonString();
    }

    static (Simulation Sim, Konto Spieler) Partie(Stadtkarte karte, int rivalen, int startwert = 1, Wappen? wappen = null, string familie = "Hammerschmid",
        IReadOnlyList<Hausvorgabe?>? vorgaben = null, Schwierigkeit schwierigkeit = Schwierigkeit.Normal, Aufstellungsart art = Aufstellungsart.Gelost) =>
        Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Schwierigkeit = schwierigkeit }, Daten.Value,
            new NeuePartie(Familienname: familie, Rivalen: rivalen, Wappen: wappen, KiVorgaben: vorgaben, Aufstellung: art), karte: karte);

    static IReadOnlyList<Starthaus> Haeuser(Simulation sim) => sim.Wirtschaft!.Karte!.Startbelegung.Haeuser;

    static string Beruf(Starthaus h) => Daten.Value.Art(h.Betriebe[0].Art).Beruf;

    static string Bild(Simulation sim) =>
        string.Join(" | ", Haeuser(sim).Select(h => $"{h.Name}, {h.Volk}, {h.Glaube}, {h.Geschlecht}, {Beruf(h)}, {h.Wappen}, {h.Farbe}, {string.Join("+", h.Betriebe.Select(b => b.Platz))}"));

    static string AlterStand(string name)
    {
        var datei = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
            "tests", "Reichsgilde.Core.Tests", "Spielstaende", name);
        using var gz = new GZipStream(File.OpenRead(datei), CompressionMode.Decompress);
        return new StreamReader(gz).ReadToEnd();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Zufallsgegner: Keine Karte des Spiels nennt mehr ein festes Haus – nur Umland-Startplätze und die Namen der Stadt (Köln: sieben Plätze, sieben Geschlechter)", () =>
        {
            foreach (var kennung in Karten)
            {
                var start = Karte(kennung).Startbelegung;
                Pruefe.Gleich(0, start.Haeuser.Count, kennung);
                Pruefe.Wahr(!Text(kennung).Contains("\"haeuser\"") && !Text(kennung).Contains("\"wappen\""), $"{kennung}: kein Abschnitt „haeuser“");
                Pruefe.Wahr(start.Hausnamen is { Count: > 0 }, $"{kennung}: Namen der Stadt");
            }
            var koeln = Koeln.Value.Startbelegung;
            Pruefe.Gleich(string.Join(", ", Koelner), string.Join(", ", koeln.Hausnamen!));
            Pruefe.Gleich("Grube2, Holzschlag2, Steinbruch2, Kraeutergarten2, Grube4, Bauernhof1, Holzschlag3", string.Join(", ", koeln.Umland!.Select(b => b.Platz)));
            Pruefe.Gleich("Grube Overstolz", Koeln.Value.GebaeudeMit("Grube2")!.Betrieb); // der Betrieb heißt weiter nach dem Geschlecht
            Pruefe.Gleich(3, Karte("greybox").Startbelegung.Umland!.Count);
            Pruefe.Gleich(8, Karte("messstadt").Startbelegung.Umland!.Count);
            Pruefe.Wahr(Karte("probestadt").Startbelegung.Umland is null, "Probestadt hat kein Umland");
        }),

        ("Zufallsgegner: Köln mit drei Gegnern – genau drei gewürfelte Häuser, Namen, Wappen und Hausfarben alle verschieden (auch vom Spieler), Volk mit Bürgerrecht, Glaube zum Volk, jeder Beruf einmal", () =>
        {
            for (var startwert = 1; startwert <= 25; startwert++)
            {
                var (sim, spieler) = Partie(Koeln.Value, 3, startwert);
                var h = Haeuser(sim);
                Pruefe.Gleich(3, h.Count, $"Startwert {startwert}");
                Pruefe.Gleich(4, sim.Wirtschaft!.Konten.Count, "Spieler und drei KI-Häuser");
                var namen = h.Select(x => x.Name).Append(spieler.Inhaber).ToList();
                Pruefe.Gleich(4, namen.Distinct().Count(), $"Startwert {startwert}: {string.Join(", ", namen)}");
                Pruefe.Wahr(namen.All(n => n.StartsWith("Haus ") && n.Length > 5), string.Join(", ", namen));
                Pruefe.Gleich(4, h.Select(x => x.Wappen).Append(spieler.Wappen!).Distinct().Count(), $"Startwert {startwert}: Wappen");
                var farben = h.Select(x => x.Farbe).Append(sim.Wirtschaft.Karte!.Startbelegung.Farbe(spieler.Inhaber, istSpieler: true)).ToList();
                Pruefe.Wahr(farben.All(f => f is not null) && farben.Distinct().Count() == 4, $"Startwert {startwert}: Farben {string.Join(", ", farben)}");
                foreach (var x in h)
                {
                    Pruefe.Gleich(x.Wappen.Hausfarbe(), x.Farbe, x.Name);
                    Pruefe.Wahr(Startaufstellung.KiVoelker.Contains(x.Volk), $"{x.Name}: {x.Volk}");
                    Pruefe.Wahr(x.Glaube is { } g && Startaufstellung.Startglauben(x.Volk).Contains(g), $"{x.Name}: {x.Volk} mit {x.Glaube}");
                    Pruefe.Gleich(x.Wappen, sim.Wirtschaft.Konten.First(k => k.Inhaber == x.Name).Wappen, x.Name);
                }
                Pruefe.Gleich(4, h.Select(Beruf).Append("Schmied").Distinct().Count(), $"Startwert {startwert}: Berufe {string.Join(", ", h.Select(Beruf))}");
            }
        }),

        ("Zufallsgegner: So viele Gegner wie gewählt – null bis zum Höchstwert der Karte, auf allen vier Karten; mit acht Häusern trägt jedes eine andere der acht Hausfarben", () =>
        {
            foreach (var kennung in Karten)
            {
                var karte = Karte(kennung);
                var hoechstens = Partiebesetzung.HoechstensKi(karte, daten: Daten.Value);
                Pruefe.Wahr(hoechstens >= 1, $"{kennung}: {hoechstens} KI-Häuser");
                for (var n = 0; n <= hoechstens; n++)
                {
                    var (sim, spieler) = Partie(karte, n, 7);
                    var h = Haeuser(sim);
                    Pruefe.Gleich(n, h.Count, $"{kennung}, {n} gewählt");
                    var farben = h.Select(x => x.Farbe).Append(sim.Wirtschaft!.Karte!.Startbelegung.Farbe(spieler.Inhaber, istSpieler: true)).ToList();
                    Pruefe.Gleich(Math.Min(n + 1, 8), farben.Distinct().Count(), $"{kennung}, {n} KI: Farben {string.Join(", ", farben)}");
                    Pruefe.Gleich(n + 1, h.Select(x => x.Name).Append(spieler.Inhaber).Distinct().Count(), $"{kennung}, {n} KI: Namen");
                    Pruefe.Gleich(n, h.Select(Beruf).Distinct().Count(), $"{kennung}, {n} KI: Berufe");
                }
                Pruefe.Wirft(() => Partie(karte, hoechstens + 1));
            }
        }),

        ("Zufallsgegner: Die Farbe des Spielers ist vergeben – wählt er Blau, Grün oder Ocker, trägt sie kein KI-Haus (sieben Gegner in Köln, 20 Startwerte)", () =>
        {
            var wahl = new[]
            {
                new Wappen(Teilung.Geteilt, Tinktur.Blau, Tinktur.Gold, Figur.Stern), new Wappen(Teilung.Ohne, Tinktur.Gruen, Tinktur.Gold, Figur.Aehre),
                new Wappen(Teilung.Ohne, Tinktur.Ocker, Tinktur.Silber, Figur.Turm),
            };
            var hoechstens = Partiebesetzung.HoechstensKi(Koeln.Value, daten: Daten.Value);
            for (var startwert = 1; startwert <= 20; startwert++)
            {
                var w = wahl[startwert % wahl.Length];
                var (sim, _) = Partie(Koeln.Value, hoechstens, startwert, w);
                Pruefe.Wahr(Haeuser(sim).All(h => h.Farbe != w.Hausfarbe()), $"Startwert {startwert}: {Bild(sim)}");
                Pruefe.Gleich(hoechstens, Haeuser(sim).Select(h => h.Farbe).Distinct().Count(), $"Startwert {startwert}");
            }
        }),

        ("Zufallsgegner: Ein Wappen aus „Erweitert“ sperrt nur seine eigene Farbe – mit sieben Gegnern bleiben alle acht Hausfarben verschieden", () =>
        {
            var hoechstens = Partiebesetzung.HoechstensKi(Koeln.Value, daten: Daten.Value);
            var fest = new Wappen(Teilung.Ohne, Tinktur.Purpur, Tinktur.Gold, Figur.Krone);
            for (var startwert = 1; startwert <= 20; startwert++)
            {
                var (sim, spieler) = Partie(Koeln.Value, hoechstens, startwert, vorgaben: new Hausvorgabe?[] { null, new Hausvorgabe(Wappen: fest) });
                Pruefe.Gleich(fest, Haeuser(sim)[1].Wappen);
                var farben = Haeuser(sim).Select(h => h.Farbe).Append(sim.Wirtschaft!.Karte!.Startbelegung.Farbe(spieler.Inhaber, istSpieler: true)).ToList();
                Pruefe.Gleich(Math.Min(hoechstens + 1, 8), farben.Distinct().Count(), $"Startwert {startwert}: {string.Join(", ", farben)}");
            }
        }),

        ("Zufallsgegner: Gleicher Startwert, gleiche Gegner; ein anderer Startwert würfelt andere", () =>
        {
            var a = Bild(Partie(Koeln.Value, 3, 11).Sim);
            Pruefe.Gleich(a, Bild(Partie(Koeln.Value, 3, 11).Sim));
            var bilder = Enumerable.Range(1, 12).Select(s => Bild(Partie(Koeln.Value, 3, s).Sim)).Distinct().Count();
            Pruefe.Wahr(bilder >= 11, $"{bilder} verschiedene Besetzungen aus 12 Startwerten");
        }),

        ("Zufallsgegner: Namen – die Kölner Geschlechter kommen als Zufallsnamen vor, daneben die Namen der Völker; nie heißt ein KI-Haus wie der Spieler, auch wenn der „Overstolz“ heißt", () =>
        {
            var gesehen = new List<string>();
            for (var startwert = 1; startwert <= 60; startwert++)
            {
                var (sim, spieler) = Partie(Koeln.Value, 3, startwert, familie: "Overstolz");
                Pruefe.Gleich("Haus Overstolz", spieler.Inhaber);
                Pruefe.Wahr(Haeuser(sim).All(h => h.Name != "Haus Overstolz"), $"Startwert {startwert}: {Bild(sim)}");
                gesehen.AddRange(Haeuser(sim).Select(h => Familie.NachnameAus(h.Name)));
            }
            var stadt = gesehen.Where(Koelner.Contains).Distinct().ToList();
            Pruefe.Gleich(Koelner.Length - 1, stadt.Count, "alle Geschlechter außer dem des Spielers: " + string.Join(", ", stadt));
            Pruefe.Wahr(gesehen.Any(n => !Koelner.Contains(n)), "auch Namen der Völker");
            var voelker = Daten.Value.Dynastie.Voelker.Values.SelectMany(v => v.Familiennamen).ToList();
            Pruefe.Wahr(gesehen.All(n => Koelner.Contains(n) || voelker.Contains(n)), "nur Namen der Stadt und der Völker");
        }),

        ("Zufallsgegner: Namenswurf – Stadtnamen und Volksnamen in einem Topf, jeder einmal; sind alle vergeben, mit Zahl", () =>
        {
            var daten = Daten.Value;
            var volk = daten.Dynastie.Voelker[Volk.Zwerg].Familiennamen;
            var gezogen = new List<string>();
            var z = new Zufallsquelle(5);
            var stadt = new[] { "Haus Overstolz", "Gryn", volk[0] }; // „Haus“ davor fällt weg, ein Volksname zählt nicht doppelt
            for (var i = 0; i < volk.Count + 2; i++)
            {
                var n = Partiebesetzung.NameWuerfeln(daten, z, Volk.Zwerg, gezogen.Select(Partieaufbau.Hausname).ToList(), stadt);
                Pruefe.Wahr(!gezogen.Contains(n), $"{n} doppelt");
                gezogen.Add(n);
            }
            Pruefe.Wahr(gezogen.Contains("Overstolz") && gezogen.Contains("Gryn") && volk.All(gezogen.Contains), string.Join(", ", gezogen));
            var mehr = Partiebesetzung.NameWuerfeln(daten, z, Volk.Zwerg, gezogen.Select(Partieaufbau.Hausname).ToList(), stadt);
            Pruefe.Wahr(mehr.EndsWith(" 2"), mehr);
        }),

        ("Zufallsgegner: Umland-Startplätze – mit N Gegnern kommen die ersten N der Karte ins Spiel; auf „Schwer“ führt ein Haus den seiner Art unter eigenem Namen", () =>
        {
            for (var startwert = 1; startwert <= 15; startwert++)
            {
                var (sim, _) = Partie(Koeln.Value, 4, startwert, schwierigkeit: Schwierigkeit.Schwer);
                var erlaubt = Koeln.Value.Startbelegung.Umland!.Take(4).Select(b => b.Platz).ToList();
                foreach (var h in Haeuser(sim))
                    foreach (var b in h.Betriebe.Where(b => b.Platz != ""))
                    {
                        Pruefe.Wahr(erlaubt.Contains(b.Platz), $"{h.Name}: {b.Platz}");
                        Pruefe.Gleich(Daten.Value.Berufe[Beruf(h)].Umland, b.Art, h.Name);
                        var name = $"{Daten.Value.Art(b.Art).Name} {Familie.NachnameAus(h.Name)}";
                        Pruefe.Gleich(name, sim.Wirtschaft!.Karte!.GebaeudeMit(b.Platz)!.Betrieb);
                        Pruefe.Wahr(sim.Wirtschaft.Betriebe.Any(x => x.Name == name && x.Konto.Inhaber == h.Name), $"{name} gehört {h.Name}");
                    }
                var plaetze = Haeuser(sim).SelectMany(h => h.Betriebe.Select(b => b.Platz)).Where(p => p != "").ToList();
                Pruefe.Gleich(plaetze.Count, plaetze.Distinct().Count(), "kein Platz doppelt");
            }
            var mit = Enumerable.Range(1, 15).Sum(s => Haeuser(Partie(Koeln.Value, 4, s).Sim).Count(h => h.Betriebe.Count > 1));
            Pruefe.Wahr(mit > 0, "in 15 Partien bekommt wenigstens ein Haus seinen Umland-Startplatz");
        }),

        ("Zufallsgegner: Eine ältere Karte mit festen Häusern ergibt dieselbe Partie – die Häuser spielen nicht mehr fest mit, geben aber Umlandplätze und Namen her; zur Probe (Vorgabe) stehen sie noch da", () =>
        {
            var alt = Stadtkarte.AusJson(AltesKoeln(), Katalog.Value);
            Pruefe.Gleich(7, alt.Startbelegung.Haeuser.Count);
            foreach (var schwierigkeit in new[] { Schwierigkeit.Normal, Schwierigkeit.Schwer })
                for (var startwert = 1; startwert <= 6; startwert++)
                {
                    var (neu, neuSpieler) = Partie(Koeln.Value, 5, startwert, schwierigkeit: schwierigkeit);
                    var (alte, altSpieler) = Partie(alt, 5, startwert, schwierigkeit: schwierigkeit);
                    Pruefe.Gleich(Bild(neu), Bild(alte), $"Startwert {startwert}, {schwierigkeit}");
                    Pruefe.Gleich(string.Join(", ", neu.Wirtschaft!.Betriebe.Select(b => $"{b.Name} ({b.Konto.Inhaber})")),
                        string.Join(", ", alte.Wirtschaft!.Betriebe.Select(b => $"{b.Name} ({b.Konto.Inhaber})")));
                    for (var i = 0; i < 200; i++) { neu.Tick(); alte.Tick(); }
                    Pruefe.Gleich(string.Join(", ", neu.Wirtschaft.Konten.Select(k => k.Vermoegen)), string.Join(", ", alte.Wirtschaft.Konten.Select(k => k.Vermoegen)));
                }
            // Der Spieler darf heißen wie ein Haus der alten Karte, und sein Wappen darf eines von dort sein
            var (sim, spieler) = Partie(alt, 3, 2, wappen: Wappen.Vorlagen[1], familie: "Gryn");
            Pruefe.Wahr(Haeuser(sim).All(h => h.Name != spieler.Inhaber && h.Wappen != spieler.Wappen), Bild(sim));
            // Zur Probe (Vorgabe, Kartenwerkstatt) stehen die festen Häuser der alten Karte wie früher
            var probe = Partie(alt, 3, 2, art: Aufstellungsart.Vorgabe).Sim.Wirtschaft!.Konten;
            Pruefe.Gleich("Haus Hammerschmid, Haus Overstolz, Haus Hardevust, Haus Lyskirchen", string.Join(", ", probe.Select(k => k.Inhaber)));
            Pruefe.Gleich(Wappen.Vorlagen[1], probe[1].Wappen);
        }),

        ("Zufallsgegner: Ein alter Spielstand mit festen Dynastien (Köln, main f7890512) lädt unverändert – Overstolz, Hardevust, Lyskirchen, Gryn mit ihren Wappen, und läuft weiter", () =>
        {
            var json = AlterStand("hofstellen-koeln-1401.json.gz");
            var knoten = JsonNode.Parse(json)!;
            var (sim, spieler) = Speicherung.Laden(json);
            var w = sim.Wirtschaft!;
            var alteKonten = knoten["Wirtschaft"]!["Konten"]!.AsArray();
            Pruefe.Gleich(alteKonten.Count, w.Konten.Count);
            Pruefe.Gleich("Haus Hammerschmid, Haus Overstolz, Haus Hardevust, Haus Lyskirchen, Haus Gryn", string.Join(", ", w.Konten.Take(5).Select(k => k.Inhaber)));
            var kartenhaeuser = w.Karte!.Startbelegung.Haeuser;
            Pruefe.Wahr(kartenhaeuser.Count >= 4 && kartenhaeuser[0].Name == "Haus Overstolz", "die Karte im Spielstand nennt ihre festen Häuser");
            foreach (var k in w.Konten.Skip(1).Take(4))
                Pruefe.Gleich(kartenhaeuser.First(x => x.Name == k.Inhaber).Wappen, k.Wappen, k.Inhaber);
            var konten = w.Konten.Count;
            Pruefe.Wahr(w.Karte.Startbelegung.Umland is null && w.Karte.Startbelegung.Hausnamen is null, "die Karte im Spielstand bleibt, wie sie war");
            for (var i = 0; i < 300; i++) sim.Tick();
            Pruefe.Gleich(konten, w.Konten.Count);
        }),

        ("Zufallsgegner: Der Spielstand einer neuen Partie nennt die gewürfelten Häuser, nicht die Listen der Karte – gespeichert und geladen dieselben Gegner", () =>
        {
            var (sim, spieler) = Partie(Koeln.Value, 3, 4);
            var json = Speicherung.Speichern(sim, spieler);
            Pruefe.Wahr(!json.Contains("\"hausnamen\"", StringComparison.OrdinalIgnoreCase), "keine Namensliste der Karte im Spielstand");
            var (geladen, _) = Speicherung.Laden(json);
            Pruefe.Wahr(geladen.Wirtschaft!.Karte!.Startbelegung is { Umland: null, Hausnamen: null }, "keine Kartenlisten im Spielstand");
            Pruefe.Gleich(Bild(sim), Bild(geladen));
        }),

        ("Zufallsgegner: Kartenwerkstatt – Umland-Startplätze und Hausnamen lesen, ändern, schreiben; Rückgängig; die Prüfung meldet falsche und doppelte Plätze und doppelte Namen", () =>
        {
            var e = Kartenentwurf.AusJson(Text("koeln"), Katalog.Value);
            e.Daten = Daten.Value;
            Pruefe.Gleich(Text("koeln"), e.ZuJson(), "gelesen und geschrieben: dieselbe Datei");
            Pruefe.Gleich(7, e.Start.Umland.Count);
            Pruefe.Gleich(string.Join(", ", Koelner), string.Join(", ", e.Start.Hausnamen));
            Pruefe.Wahr(!e.Start.IstLeer && e.Start.Haeuser.Count == 0, "Start ohne Häuser");
            Pruefe.Wahr(e.FreieBetriebsplaetze().All(g => g.Kennung != "Grube2"), "ein Umland-Startplatz ist nicht frei");

            e.StartumlandEntfernen(0);
            Pruefe.Gleich("Holzschlag2", e.Start.Umland[0].Platz);
            Pruefe.Wahr(e.FreieBetriebsplaetze().Any(g => g.Kennung == "Grube2"), "wieder frei");
            Pruefe.Gleich(6, e.StartumlandHinzufuegen("Grube2"));
            Pruefe.Gleich(Betriebsart.Mine, e.Start.Umland[6].Art);
            Pruefe.Wirft(() => e.StartumlandHinzufuegen("Grube2"));
            Pruefe.Wirft(() => e.StartumlandHinzufuegen("gibtsnicht"));
            e.HausnamenSetzen(new[] { "Haus Overstolz", " Gryn ", "" });
            Pruefe.Gleich("Overstolz, Gryn", string.Join(", ", e.Start.Hausnamen));
            Pruefe.Wahr(!e.Befunde.Any(b => b.Schwere == Schwere.Fehler && b.Regel == "start"), "ohne Fehler am Start");
            var karte = e.ZuStadtkarte().Startbelegung;
            Pruefe.Gleich("Grube2", karte.Umland![6].Platz);
            Pruefe.Gleich("Overstolz, Gryn", string.Join(", ", karte.Hausnamen!));
            e.Geschichte.Rueckgaengig();
            e.Geschichte.Rueckgaengig();
            e.Geschichte.Rueckgaengig();
            Pruefe.Gleich(Text("koeln"), e.ZuJson(), "dreimal Rückgängig: wie die Datei");

            // Prüfung: doppelter Name; ein Platz, der kein Umlandbetrieb ist; ein Platz zweimal
            e.HausnamenSetzen(new[] { "Gryn", "Haus Gryn" });
            Pruefe.Wahr(e.Befunde.Any(b => b.Schwere == Schwere.Fehler && b.Text.Contains("Hausname „Gryn“ steht doppelt")), "doppelter Name");
            var k = JsonNode.Parse(Text("koeln"))!.AsObject();
            k["start"]!["umland"]!.AsArray().Add(new JsonObject { ["platz"] = "Grube2", ["art"] = "Mine" });
            k["start"]!["umland"]!.AsArray().Add(new JsonObject { ["platz"] = "Dom", ["art"] = "Mine" });
            var falsch = Kartenentwurf.AusJson(k.ToJsonString(), Katalog.Value);
            Pruefe.Wahr(falsch.Befunde.Any(b => b.Schwere == Schwere.Fehler && b.Text.Contains("Der Platz „Grube2“ ist mehrfach vergeben")), "doppelter Platz");
            Pruefe.Wahr(falsch.Befunde.Any(b => b.Schwere == Schwere.Fehler && b.Text.Contains("„Dom“ ist kein Umlandbetrieb der Karte")), "kein Umlandbetrieb");
            Pruefe.Wirft(() => Stadtkarte.AusJson(k.ToJsonString(), Katalog.Value));
        }),

        ("Zufallsgegner: Probe in der Kartenwerkstatt – ohne feste Häuser würfelt auch sie ihre KI-Häuser", () =>
        {
            var e = Kartenentwurf.AusJson(Text("probestadt"), Katalog.Value);
            var (sim, _) = e.Ausprobieren(Daten.Value);
            var h = Haeuser(sim);
            Pruefe.Gleich(Partiebesetzung.HoechstensKi(Karte("probestadt"), daten: Daten.Value), h.Count);
            Pruefe.Gleich(h.Count, h.Select(x => x.Farbe).Distinct().Count());
        }),
    };
}
