using System.IO.Compression;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// „Nur noch Bauzonen“ (Entscheidung des Nutzers vom 15.11.2026), Teil 1 im Kern: Es gibt keine Hofstellen, Startplätze und Bauplätze mehr.
/// Das Startortlos stellt jedes Haus mit Wohnhaus und Werkstatt auf Grundstücke in den Bauzonen im Stadtinneren (<see cref="Startortlos"/>),
/// der Spieler kann beides selbst setzen (Startwahl: der Baubefehl als Startform, sofort und kostenlos), und alte Spielstände mit Hofstellen
/// laden weiter – ihre Werkstätten werden Grundstücke derselben Fläche, freie Bauplätze fallen weg.
/// </summary>
static class StartortlosTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));
    static readonly Lazy<Stadtkarte> Koeln = new(() => Stadtkarte.AusDatei(Datenpfad.Karte("koeln")));
    static readonly Lazy<Stadtkarte> Greybox = new(() => Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));

    static (Simulation Sim, Konto Spieler) Partie(Stadtkarte karte, int startwert = 7, int rivalen = 3, bool startwahl = false) =>
        Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, Daten.Value,
            new NeuePartie(Rivalen: rivalen, Startwahl: startwahl), karte: karte);

    /// <summary>Liegt die ganze Fläche in Bauzonen im Stadtinneren (Kern, Mauerring)?</summary>
    static bool Innen(Stadtkarte karte, Grundriss g) =>
        g.Felder(2).All(f => karte.Bauzonen.FirstOrDefault(z => z.Enthaelt(f.X, f.Z)) is { } zone && Bauwesen.ImStadtinneren(zone.Art));

    /// <summary>Wohnhaus und Werkstatt jedes Hauses als Text – zum Vergleichen zweier Partien.</summary>
    static string Orte(Stadtwirtschaft w, bool ohneSpieler = false) => string.Join("; ", w.Konten.Skip(ohneSpieler ? 1 : 0).Select(k =>
    {
        var haus = w.Bauen.Wohnhaus(k)?.Grundriss;
        var werk = w.Betriebe.FirstOrDefault(b => b.Konto == k && b.InDerStadt) is { } b ? w.Bauen.PlatzVon(b.Name)?.Grundstueck?.Grundriss : null;
        return $"{k.Inhaber}: {haus} / {werk}";
    }));

    static string AlterStand(string name)
    {
        var datei = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
            "tests", "Reichsgilde.Core.Tests", "Spielstaende", name);
        using var gz = new GZipStream(File.OpenRead(datei), CompressionMode.Decompress);
        return new StreamReader(gz).ReadToEnd();
    }

    /// <summary>
    /// Lädt einen Spielstand von main f7890512 (mit Hofstellen: Werkstätten auf Werkstattplätzen der Karte, freie Hofstellen als Bauplätze, ein
    /// Leerstand) und prüft die Umwandlung.
    /// </summary>
    /// <param name="betriebe">Betrieb → Haus, Kennung des Werkstattplatzes (wie auf main gespeichert).</param>
    /// <param name="wohnhaeuser">Haus → Kennung des Wohnhausplatzes.</param>
    /// <param name="leerstand">Das bankrotte Haus, dessen Wohnhaus leer steht.</param>
    /// <param name="freieWerkstatt">Der Werkstattplatz des bankrotten Hauses: Die Werkstatt steht zum Verkauf.</param>
    static void AlterSpielstand(string datei, (string Betrieb, string Haus, string Platz)[] betriebe, (string Haus, string Platz)[] wohnhaeuser,
        string leerstand, string freieWerkstatt)
    {
        var json = AlterStand(datei);
        var knoten = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        var alteGrundstuecke = knoten["Wirtschaft"]!["Bauen"]!["Grundstuecke"]!.AsArray();
        var alteBetriebe = knoten["Wirtschaft"]!["Betriebe"]!.AsArray();
        var (sim, spieler) = Speicherung.Laden(json);
        var w = sim.Wirtschaft!;
        Pruefe.Gleich(betriebe[0].Haus, spieler!.Inhaber);
        Pruefe.Gleich(string.Join(", ", betriebe.Select(b => b.Betrieb)), string.Join(", ", w.Betriebe.Where(b => b.InDerStadt).Select(b => b.Name)), "dieselben Betriebe");
        Pruefe.Gleich(alteBetriebe.Count, w.Betriebe.Count);
        for (var i = 0; i < alteBetriebe.Count; i++)
        {
            // gleicher Betrieb: Name und Besitzer wie im Spielstand (die Art, also die Stufe, liest der Spielstand unverändert)
            var (alt, neu) = (alteBetriebe[i]!, w.Betriebe[i]);
            Pruefe.Gleich((string)alt["Name"]!, neu.Name);
            Pruefe.Gleich((int)alt["Konto"]!, w.Konten.ToList().IndexOf(neu.Konto), neu.Name);
        }
        foreach (var (name, haus, kennung) in betriebe)
        {
            var b = w.Betriebe.Single(x => x.Name == name);
            Pruefe.Gleich(haus, b.Konto.Inhaber);
            var platz = w.Karte!.GebaeudeMit(kennung)!;
            var g = w.Bauen.Werkstatt(name)!;
            Pruefe.Wahr(g is { Art: Grundstuecksart.Werkstatt, Zustand: Bauzustand.Werkstatt, AufWerkstattplatz: true, Besitzer: null } && g.Kennung == kennung,
                $"{name}: ein Grundstück mit der Kennung des Platzes");
            Pruefe.Gleich(platz.Grundriss!.Value, g.Grundriss, name + ": dieselbe Fläche");
            Pruefe.Gleich(platz, w.Bauen.PlatzVon(name)!.Gebaeude, name + ": steht weiter am Platz der Karte (Fahrzeit, Bild)");
        }
        foreach (var (haus, kennung) in wohnhaeuser)
        {
            var k = w.Konten.Single(x => x.Inhaber == haus);
            var g = w.Bauen.Wohnhaus(k)!;
            var alt = alteGrundstuecke.Single(x => (string?)x!["Kennung"] == kennung)!;
            Pruefe.Gleich(kennung, g.Kennung, haus);
            Pruefe.Gleich((int)alt["Nummer"]!, g.Nummer, haus);
            Pruefe.Gleich(k.WohnhausStufe, g.Stufe, haus + ": gleiche Stufe");
            Pruefe.Gleich(w.Karte!.GebaeudeMit(kennung)!.Grundriss!.Value, g.Grundriss, haus + ": dieselbe Fläche");
        }
        // Ein Leerstand bleibt Leerstand; die Werkstatt des bankrotten Hauses steht weiter zum Verkauf, jetzt auf einem Grundstück.
        var tot = w.Konten.Single(x => x.Inhaber == leerstand);
        Pruefe.Wahr(w.Bauen.Leerstand(tot) is { Zustand: Bauzustand.Leerstand, Kennung: not null }, "der Leerstand bleibt");
        Pruefe.Wahr(w.Bauen.Grundstuecke.Any(g => g.Kennung == freieWerkstatt && g.Art == Grundstuecksart.Werkstatt && w.FreieLiegenschaften.Any(l => l.Name == g.Betrieb)),
            "die freie Werkstatt hat ihr Grundstück und ist zu haben");
        // Freie Bauplätze und Hofstellen fallen ersatzlos weg.
        Pruefe.Wahr(alteGrundstuecke.Count > wohnhaeuser.Length + 1, "der alte Spielstand hat Bauplätze");
        Pruefe.Wahr(w.Bauen.Grundstuecke.All(g => g.Art != Grundstuecksart.Bauplatz), "kein Bauplatz mehr");
        Pruefe.Gleich(wohnhaeuser.Length + 1 + betriebe.Length + 1, w.Bauen.Grundstuecke.Count); // Wohnhäuser, Leerstand, Werkstätten, freie Werkstatt

        // Es läuft weiter; nach Speichern und Laden steht alles gleich (keine zweite Umwandlung).
        for (var i = 0; i < 300; i++) sim.Tick();
        var gespeichert = Speicherung.Speichern(sim, spieler, DateTime.UnixEpoch);
        var (sim2, spieler2) = Speicherung.Laden(gespeichert);
        Pruefe.Gleich(gespeichert, Speicherung.Speichern(sim2, spieler2, DateTime.UnixEpoch));
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Startortlos: Wie viele Häuser beginnen können, sagen die Bauzonen im Stadtinneren – Köln, Probestadt und Messstadt 8, Greybox 5 (seit 16.11.2026: Hof vor der Westmauer ist Vorstadt, K4 ohne die Gassen der alten Werkstattplätze); in die Vorstadt weicht kein Haus aus", () =>
        {
            foreach (var (kennung, innen) in new[] { ("koeln", 8), ("probestadt", 8), ("greybox", 5), ("messstadt", 8) })
            {
                var karte = Stadtkarte.AusDatei(Datenpfad.Karte(kennung));
                Pruefe.Gleich(innen, Startortlos.Haeuser(karte, Daten.Value), kennung);
                Pruefe.Gleich(innen - 1, Partiebesetzung.HoechstensKi(karte, daten: Daten.Value), kennung + ": der Spieler und so viele KI-Häuser");
                Pruefe.Wahr(Startortlos.Startstellen(karte, Daten.Value, werkstatt: false).All(g => Innen(karte, g))
                            && Startortlos.Startstellen(karte, Daten.Value, werkstatt: true).All(g => Innen(karte, g)), kennung + ": alle Startstellen im Stadtinneren");
            }
            Pruefe.Wahr(Startortlos.Startstellen(Koeln.Value, Daten.Value, werkstatt: true).Count > 100, "Köln hat viele Stellen für Werkstätten");
            // Gegenprobe: ein Haus zu viel wird abgewiesen; ohne Karte gilt der Ersatz mit drei KI-Häusern
            Pruefe.Wirft(() => Startortlos.Losen(Koeln.Value, Daten.Value, 1, 9));
            Pruefe.Wirft(() => Partie(Koeln.Value, rivalen: 8));
            Pruefe.Gleich(3, Partiebesetzung.HoechstensKi(null, daten: Daten.Value));
            Pruefe.Gleich(0, Startortlos.Haeuser(null, Daten.Value));
        }),

        ("Startortlos: Jedes Haus beginnt mit Wohnhaus und Werkstatt auf eigenen Grundstücken im Stadtinneren (Köln, 7 KI) – gültige Stellen, nichts überdeckt sich, kein Platz der Karte", () =>
        {
            var (sim, spieler) = Partie(Koeln.Value, rivalen: 7);
            var w = sim.Wirtschaft!;
            Pruefe.Gleich(8, w.Konten.Count);
            var leer = new Simulation(Partieeinstellungen.Standard, null, Daten.Value).Wirtschaft!; // eine leere Stadt: Dort muss jede Stelle bebaubar sein
            leer.KarteSetzen(Koeln.Value);
            foreach (var k in w.Konten)
            {
                var haus = w.Bauen.Wohnhaus(k)!;
                Pruefe.Wahr(haus is { Kennung: null, Zustand: Bauzustand.Bewohnt, Stufe: 0 } && Innen(Koeln.Value, haus.Grundriss), k.Inhaber + ": Wohnhaus im Stadtinneren");
                Pruefe.Gleich(null, leer.Bauen.PlatzVerboten(new WohnhausBauen(0, haus.Grundriss.X, haus.Grundriss.Z, haus.Grundriss.Drehung)), k.Inhaber);
                var betrieb = w.Betriebe.Single(b => b.Konto == k && b.InDerStadt);
                var platz = w.Bauen.PlatzVon(betrieb.Name)!;
                Pruefe.Wahr(platz is { Gebaeude: null, Grundstueck: { Kennung: null, Art: Grundstuecksart.Werkstatt, Zustand: Bauzustand.Werkstatt } }
                            && Innen(Koeln.Value, platz.Grundstueck.Grundriss), betrieb.Name + ": Werkstatt auf einem Grundstück im Stadtinneren");
                var gr = platz.Grundstueck!.Grundriss;
                Pruefe.Gleich(null, leer.Bauen.PlatzVerboten(new WerkstattBauen(Betriebsart.Giesserei, gr.X, gr.Z, gr.Drehung)), betrieb.Name);
                Pruefe.Wahr(betrieb.Arbeiter.Count > 0, betrieb.Name + ": mit Startleuten");
            }
            Pruefe.Gleich(16, w.Bauen.Grundstuecke.Count);
            var flaechen = w.Bauen.Grundstuecke.Select(g => g.Grundriss).ToList();
            for (var i = 0; i < flaechen.Count; i++)
                for (var j = i + 1; j < flaechen.Count; j++)
                    Pruefe.Wahr(!flaechen[i].Ueberlappt(flaechen[j]), $"Grundstück {i + 1} und {j + 1} überdecken sich");
            // Die Karte der Partie setzt niemanden mehr auf einen Platz: keine Hofstellen, kein Betrieb an einem Werkstattplatz, kein Bauplatz.
            Pruefe.Wahr(w.Karte is { Startbelegung.OhnePlaetze: true }, "Partiekarte ohne Plätze");
            Pruefe.Wahr(!w.Karte!.GebaeudeDerArt(Bausteinart.Werkstattplatz).Any() && !w.Karte.GebaeudeDerArt(Bausteinart.Wohnhausplatz).Any(), "keine Werkstatt- und Wohnhausplätze");
            Pruefe.Wahr(w.Bauen.Grundstuecke.All(x => x.Art != Grundstuecksart.Bauplatz), "keine Bauplätze");
            // Die Werkstatt des Spielers heißt nach ihrer Straße, die der KI nach dem Haus; die Umlandbetriebe bleiben feste Plätze der Karte.
            Pruefe.Wahr(w.Betriebe.First(b => b.Konto == spieler).Name.StartsWith("Gießerei a", StringComparison.Ordinal), w.Betriebe.First(b => b.Konto == spieler).Name);
            Pruefe.Wahr(w.Betriebe.Where(b => !b.InDerStadt).All(b => w.Bauen.PlatzVon(b.Name) is { Gebaeude.Art: Bausteinart.Umlandbetrieb }), "Umlandbetriebe am festen Platz");
            // Die Karte selbst bleibt, wie sie war (die Partie spielt auf einer Kopie).
            Pruefe.Wahr(!Koeln.Value.Startbelegung.OhnePlaetze, "die Kartendatei bleibt unverändert");
            // Speichern und Laden: dieselben Grundstücke, die Partiekarte lädt fehlerfrei
            var (sim2, _) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            Pruefe.Gleich(Orte(w), Orte(sim2.Wirtschaft!));
            for (var i = 0; i < 2 * Spielzeit.MinutenProTag; i++) sim.Tick(); // zwei Jahre laufen durch
            Pruefe.Wahr(w.Betriebe.Where(b => b.InDerStadt).All(b => w.Bauen.PlatzVon(b.Name) is not null), "jeder Betrieb in der Stadt hat einen Platz");
        }),

        ("Startortlos: Gleicher Startwert = gleiche Orte, anderer Startwert = andere; das Los hat eine eigene Zufallsquelle (Familien wie ohne Bauwesen)", () =>
        {
            var a = Partie(Koeln.Value, startwert: 21).Sim.Wirtschaft!;
            var b = Partie(Koeln.Value, startwert: 21).Sim.Wirtschaft!;
            Pruefe.Gleich(Orte(a), Orte(b));
            // Gegenprobe: zwölf Startwerte geben dem Spieler mindestens zehn verschiedene Wohnhäuser (ganz zufällig, kein fester Platz)
            var haeuser = Enumerable.Range(1, 12).Select(s => Startortlos.Losen(Koeln.Value, Daten.Value, s, 4)![0].Wohnhaus).Distinct().Count();
            Pruefe.Wahr(haeuser >= 10, $"nur {haeuser} verschiedene Wohnhäuser in 12 Partien");
            Pruefe.Wahr(Orte(a) != Orte(Partie(Koeln.Value, startwert: 22).Sim.Wirtschaft!), "anderer Startwert, andere Orte");
            // Eigene Zufallsquelle: Dieselbe Partie ohne Karte (ohne Los) würfelt dieselbe Familie des Spielers.
            string Familie(Stadtwirtschaft w) => string.Join(",", w.Familien.Von(w.Konten[0])!.Mitglieder.Select(p => p.Name));
            var ohne = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 21 }, Daten.Value, new NeuePartie())
                .Simulation.Wirtschaft!;
            Pruefe.Gleich(Familie(ohne), Familie(a));
        }),

        ("Startortlos: kein Ausweichen in die Vorstadt – auf der Greybox stehen bis zu fünf Häuser ganz im Stadtinneren, ein sechstes wird abgewiesen; in Köln und der Messstadt alle acht", () =>
        {
            var messstadt = Stadtkarte.AusDatei(Datenpfad.Karte("messstadt"));
            for (var startwert = 1; startwert <= 6; startwert++)
            {
                var greybox = Startortlos.Losen(Greybox.Value, Daten.Value, startwert, 5)!;
                Pruefe.Gleich(5, greybox.Count);
                Pruefe.Wahr(greybox.All(o => Innen(Greybox.Value, o.Wohnhaus) && Innen(Greybox.Value, o.Werkstatt)), $"Startwert {startwert}: fünf Häuser ganz im Stadtinneren");
                Pruefe.WirftMitMeldung(() => Startortlos.Losen(Greybox.Value, Daten.Value, startwert, 6), "Platz für 5 Häuser");
                foreach (var karte in new[] { Koeln.Value, messstadt })
                {
                    var acht = Startortlos.Losen(karte, Daten.Value, startwert, 8)!;
                    Pruefe.Wahr(acht.Count == 8 && acht.All(o => Innen(karte, o.Wohnhaus) && Innen(karte, o.Werkstatt)), $"{karte.Kennung}, Startwert {startwert}: alle acht im Stadtinneren");
                }
            }
            var (sim, _) = Partie(Greybox.Value, rivalen: 4);
            Pruefe.Gleich(10, sim.Wirtschaft!.Bauen.Grundstuecke.Count);
            Pruefe.WirftMitMeldung(() => Partie(Greybox.Value, rivalen: 5), "null bis vier");
        }),

        ("Startwahl: Der Spieler setzt erst das Wohnhaus, dann die Werkstatt mit dem Baubefehl – sofort, kostenlos, einmalig; die KI-Häuser stehen wie ohne Wahl", () =>
        {
            var (sim, spieler) = Partie(Koeln.Value, startwahl: true);
            var w = sim.Wirtschaft!;
            Pruefe.Gleich(Orte(Partie(Koeln.Value).Sim.Wirtschaft!, ohneSpieler: true), Orte(w, ohneSpieler: true)); // die KI-Häuser wie ohne Wahl
            Pruefe.Wahr(w.Bauen.Wohnhaus(spieler) is null && w.Betriebe.All(b => b.Konto != spieler), "der Spieler beginnt ohne Wohnhaus und Werkstatt");
            var (geld, ap) = (spieler.Vermoegen, spieler.Aktionspunkte.Vorrat);

            // 1. das Wohnhaus: nur die eigene Stufe, nicht zuerst die Werkstatt
            var wahl = w.Bauen.StartwahlVon(spieler)!;
            Pruefe.Wahr(wahl is { WohnhausOffen: true, Werkstatt: Betriebsart.Giesserei } && wahl.Vorlage == new WohnhausBauen(0, 0, 0, 0), "zuerst das Wohnhaus");
            var hausorte = w.Bauen.Startorte(spieler);
            Pruefe.Wahr(hausorte.Count > 100 && hausorte.All(o => o is WohnhausBauen { Stufe: 0 } && w.Bauen.BauVerboten(spieler, o) is null), "Startorte für das Wohnhaus");
            Pruefe.Gleich(0, w.Bauen.Bauorte(spieler, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0)).Count);
            Pruefe.Wahr(w.Ausfuehren(spieler, new WerkstattBauen(Betriebsart.Giesserei, hausorte[0].X, hausorte[0].Z, 0)).Meldung.Contains("zuerst den Platz für das Wohnhaus"), "Reihenfolge");
            Pruefe.Wahr(!w.Ausfuehren(spieler, hausorte[0] with { X = hausorte[0].X + 1 }).Erfolg, "neben dem Raster geht auch die Startform nicht");
            var hausort = hausorte[hausorte.Count / 2];
            var v = w.Bauen.Vorschau(spieler, hausort);
            Pruefe.Gleich((0.0, 0, (string?)null), (v.Kosten, v.BauzeitMinuten, v.Verboten));
            var e = w.Ausfuehren(spieler, hausort);
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Wahr(w.Bauen.Wohnhaus(spieler) is { Zustand: Bauzustand.Bewohnt, Arbeit: null, Kennung: null } haus && haus.Grundriss == w.Bauen.Grundriss(hausort), "das Wohnhaus steht sofort");

            // 2. die Werkstatt des Berufs: kein zweites Wohnhaus, keine andere Werkstatt
            wahl = w.Bauen.StartwahlVon(spieler)!;
            Pruefe.Wahr(wahl is { WohnhausOffen: false } && wahl.Vorlage == new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0), "dann die Werkstatt");
            Pruefe.Wahr(w.Ausfuehren(spieler, hausorte[0]).Meldung.Contains("jetzt den Platz für die Werkstatt"), "kein zweites Wohnhaus");
            var werkorte = w.Bauen.Startorte(spieler);
            Pruefe.Wahr(werkorte.Count > 50 && werkorte.All(o => !w.Bauen.Grundriss(o).Ueberlappt(w.Bauen.Wohnhaus(spieler)!.Grundriss)), "Startorte für die Werkstatt, nicht auf dem Wohnhaus");
            Pruefe.Wahr(!w.Ausfuehren(spieler, new WerkstattBauen(Betriebsart.Tischlerei, werkorte[0].X, werkorte[0].Z, werkorte[0].Drehung)).Erfolg, "nur die Werkstatt des eigenen Berufs");

            // mittendrin speichern und laden: Die Wahl bleibt offen
            var (sim2, spieler2) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            Pruefe.Wahr(sim2.Wirtschaft!.Bauen.StartwahlVon(spieler2!) is { WohnhausOffen: false, Werkstatt: Betriebsart.Giesserei }, "die offene Wahl steht im Spielstand");

            var werkort = werkorte[werkorte.Count / 2];
            var vw = w.Bauen.Vorschau(spieler, werkort);
            Pruefe.Gleich((0.0, 0, (string?)null), (vw.Kosten, vw.BauzeitMinuten, vw.Verboten));
            e = w.Ausfuehren(spieler, werkort);
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            var betrieb = w.Betriebe.Single(b => b.Konto == spieler);
            Pruefe.Gleich(vw.Betrieb, betrieb.Name);
            Pruefe.Wahr(betrieb.Art == Betriebsart.Giesserei && betrieb.Arbeiter.Count == w.Personal.StartAnzahl(Betriebsart.Giesserei, 2), "der Betrieb steht mit seinen Startleuten");
            Pruefe.Wahr(w.Bauen.PlatzVon(betrieb.Name) is { Grundstueck: { Zustand: Bauzustand.Werkstatt } g } && g.Grundriss == w.Bauen.Grundriss(werkort), "auf dem gewählten Grundstück");
            Pruefe.Gleich((geld, ap), (spieler.Vermoegen, spieler.Aktionspunkte.Vorrat)); // kostenlos: kein Geld, keine Aktionspunkte

            // einmalig: Danach ist der Baubefehl wieder ein gewöhnlicher Neubau (Geld, Bauzeit, Handwerk).
            Pruefe.Wahr(w.Bauen.StartwahlVon(spieler) is null && w.Bauen.Startorte(spieler).Count == 0, "die Startwahl ist erledigt");
            var nochmal = w.Bauen.Vorschau(spieler, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0));
            Pruefe.Wahr(nochmal.Kosten > 0 && nochmal.BauzeitMinuten > 0, "eine zweite Werkstatt kostet und dauert");
            // Gegenprobe: Ein KI-Haus (ohne Startwahl) baut nie kostenlos.
            var ki = w.Konten[1];
            Pruefe.Wahr(w.Bauen.StartwahlVon(ki) is null && w.Bauen.Vorschau(ki, werkorte[0]).Kosten > 0, "die KI zahlt");
            for (var i = 0; i < Spielzeit.MinutenProTag; i++) sim.Tick(); // ein Jahr läuft durch
            Pruefe.Wahr(spieler.IstAktiv, "der Spieler ist im Spiel");
        }),

        ("Startwahl: Ohne Wahl läuft die Partie trotzdem (der Kern hält die Zeit nicht an); ohne Karte gibt es keine Startwahl", () =>
        {
            var (sim, spieler) = Partie(Greybox.Value, startwahl: true);
            for (var i = 0; i < 300; i++) sim.Tick();
            Pruefe.Wahr(sim.Wirtschaft!.Bauen.StartwahlVon(spieler) is { WohnhausOffen: true }, "die Wahl bleibt offen");
            var (ohne, k) = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie(Startwahl: true));
            Pruefe.Wahr(ohne.Wirtschaft!.Bauen.StartwahlVon(k) is null && ohne.Wirtschaft.Betriebe.Any(b => b.Konto == k), "ohne Karte beginnt der Spieler wie bisher mit seiner Werkstatt");
        }),

        ("Alter Spielstand (Köln, main f7890512, mit Hofstellen): Werkstätten und Wohnhäuser bleiben, wo sie waren – gleicher Betrieb, gleiche Stufe; Bauplätze fallen weg, der Leerstand bleibt", () =>
            AlterSpielstand("hofstellen-koeln-1401.json.gz",
                new[]
                {
                    ("Gießerei am Heumarkt", "Haus Hammerschmid", "S1_Werkstatt"), ("Tinkturei Overstolz", "Haus Overstolz", "S2_Werkstatt"),
                    ("Schmugglerloch Hardevust", "Haus Hardevust", "S3_Werkstatt"), ("Siederei Lyskirchen", "Haus Lyskirchen", "S4_Werkstatt"),
                    ("Maurerei Gryn", "Haus Gryn", "S5_Werkstatt"),
                },
                new[]
                {
                    ("Haus Hammerschmid", "S1_Wohnhaus"), ("Haus Overstolz", "S2_Wohnhaus"), ("Haus Hardevust", "S3_Wohnhaus"), ("Haus Lyskirchen", "S4_Wohnhaus"),
                    ("Haus Gryn", "S5_Wohnhaus"),
                },
                leerstand: "Haus Scherfgin", freieWerkstatt: "S6_Werkstatt")),

        ("Alter Spielstand (Greybox, main f7890512, mit Hofstellen): Werkstätten und Wohnhäuser bleiben, wo sie waren – gleicher Betrieb, gleiche Stufe; Bauplätze fallen weg, der Leerstand bleibt", () =>
            AlterSpielstand("hofstellen-greybox-1401.json.gz",
                new[] { ("Gießerei am Weinmarkt", "Haus Hammerschmid", "Weinmarkt"), ("Kapelle Eisenbart", "Haus Eisenbart", "Giesserei_Eisenbart") },
                new[] { ("Haus Hammerschmid", "Wohnhaus1"), ("Haus Eisenbart", "Wohnhaus2") },
                leerstand: "Haus Eichwald", freieWerkstatt: "Tischlerei_Eichwald")),
    };
}
