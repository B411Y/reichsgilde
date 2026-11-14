using System.Globalization;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Reichsgilde;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Entwickler;
using Reichsgilde.Core.Mehrspieler;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests der strukturierten Meldungen (Benachrichtigungen Stufe 2, <see cref="Meldung"/>, <c>Stadtwirtschaft.Gemeldet</c> und <c>Meldungsverlauf</c>):
///   • die Stellen des Kerns melden Art, Haus, Person und Ort (Familie, Einbruch, Brand, Hausbrand, Blitzschlag),
///   • in einer echten Partie (Köln, drei Jahre, alle KI-Häuser) sagt der Haushofmeister nach der Art genau das an, was die alte Erkennung nach dem
///     Wortlaut ansagte (für jedes Haus; neu nur Blitzschlag, Bankrott und das erloschene Haus), und Nachrichten an den Auftraggeber sind vertraulich,
///   • der Verlauf steht im Spielstand: speichern, laden und weiterspielen gleich; alte Spielstände laden ohne Verlauf; höchstens 500 Meldungen,
///   • der Verlauf ist mit deutscher Ländereinstellung derselbe (Mehrspieler: keine Zahl als Text nach der Ländereinstellung).
/// </summary>
static class MeldungTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static (Simulation Sim, Konto Spieler, Konto Rivale, List<Meldung> Meldungen) Partie(int startwert = 1400)
    {
        var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos },
            Daten.Value, new NeuePartie(Aufstellung: Aufstellungsart.Vorgabe));
        for (var i = 0; i < 300; i++) sim.Tick();
        sim.EntwicklerErlaubt = true;
        var meldungen = new List<Meldung>();
        sim.Wirtschaft!.Gemeldet += meldungen.Add;
        var rivale = sim.Wirtschaft.Konten.First(k => k != spieler && k.IstAktiv);
        return (sim, spieler, rivale, meldungen);
    }

    /// <summary>Führt den Befehl aus und gibt die einzige Meldung dieser Art zurück.</summary>
    static Meldung Einzig(Simulation sim, Konto spieler, List<Meldung> meldungen, Befehl befehl, Meldungsart art)
    {
        meldungen.Clear();
        var e = sim.Bote.SofortAusfuehren(spieler, befehl);
        Pruefe.Wahr(e.Erfolg, $"{befehl.GetType().Name} abgelehnt: {e.Meldung}");
        var passend = meldungen.Where(m => m.Art == art).ToList();
        Pruefe.Wahr(passend.Count == 1, $"{befehl.GetType().Name}: erwartet eine Meldung {art}, gemeldet: [{string.Join(" | ", meldungen.Select(m => $"{m.Art}: {m.Text}"))}]");
        var m = passend[0];
        Pruefe.Gleich(sim.Zeit.VergangeneMinuten, m.Minute, "Spielminute");
        Pruefe.Wahr(sim.Wirtschaft!.Meldungsverlauf.Any(x => ReferenceEquals(x, m)), "im Verlauf");
        return m;
    }

    // ── Die alte Erkennung nach dem Wortlaut (game/scripts/KlangAnsagenErkennung.cs bis Stufe 1, main 295ce73f) – zum Vergleich ──────────────

    enum Betroffen { Betrieb, Haus, Familie, Stadt }

    static readonly (string Id, Regex Text, Betroffen Art)[] AlteMuster =
    {
        A(KlangAnsagenErkennung.Id.TodOberhaupt, @"gestorben\. .+ \(\d+ Jahre\)\. Ämter und Gunst im Rat beginnen von vorn\.$", Betroffen.Familie),
        A(KlangAnsagenErkennung.Id.TodFamilie, @"^Trauer im Haus ", Betroffen.Familie),
        A(KlangAnsagenErkennung.Id.Krankheit, @" ist krank: | hat die Pest! Ohne Hilfe", Betroffen.Familie),
        A(KlangAnsagenErkennung.Id.Geburt, @"^Freude im Haus ", Betroffen.Familie),
        A(KlangAnsagenErkennung.Id.Heirat, @"^Hochzeit im Haus ", Betroffen.Familie),
        A(KlangAnsagenErkennung.Id.PestStadt, @"^Die Pest ist in der Stadt!", Betroffen.Stadt),
        A(KlangAnsagenErkennung.Id.Feuer, @"^Feuer am (?<haus>.+?) von (?<wer>[^!]+)!", Betroffen.Haus),
        A(KlangAnsagenErkennung.Id.Feuer, @"^Anschlag auf (?<wer>.+?): Feuer in der Werkstatt!", Betroffen.Betrieb),
        A(KlangAnsagenErkennung.Id.Feuer, @"^Feuer in (?<wer>.+?)! ", Betroffen.Betrieb),
        A(KlangAnsagenErkennung.Id.Diebstahl, @"^Einbruch ins (?<haus>.+?) von (?<wer>[^:]+): .+ gestohlen\.", Betroffen.Haus),
        A(KlangAnsagenErkennung.Id.Diebstahl, @"^Einbruch in (?<wer>.+?): .+ gestohlen\.", Betroffen.Betrieb),
        A(KlangAnsagenErkennung.Id.Ueberfall, @"^Überfall auf dem Weg von (?<wer>.+?) zum Markt: .+ erbeutet ", Betroffen.Betrieb),
        A(KlangAnsagenErkennung.Id.Ueberfall, @"^Überfall auf dem Rückweg nach (?<wer>.+?): .+ erbeutet ", Betroffen.Betrieb),
        A(KlangAnsagenErkennung.Id.Ueberfall, @"^Hinterhalt auf .+? von (?<wer>.+?): Wegelagerer erbeuten ", Betroffen.Betrieb),
        A(KlangAnsagenErkennung.Id.Ueberfall, @"^.+ überfallen .+ \((?<wer>[^)]+)\) .+ und erbeuten ", Betroffen.Haus),
        A(KlangAnsagenErkennung.Id.Anklage, @"^.+? klagt (?<wer>.+?) wegen .+ an \(", Betroffen.Haus),
        A(KlangAnsagenErkennung.Id.Schulden, @"^Der Kämmerer mahnt (?<wer>.+?): ", Betroffen.Haus),
        A(KlangAnsagenErkennung.Id.Meisterpruefung, @"^(?<wer>.+) legt die Meisterprüfung ab: ", Betroffen.Haus),
        A(KlangAnsagenErkennung.Id.KasseKnapp, @"^(?<wer>.+?): Verwalter kauft nur das Nötigste – die Kasse ist knapp\.$", Betroffen.Betrieb),
        A(KlangAnsagenErkennung.Id.LagerLeer, @"^(?<wer>.+?): Es fehlt .+ – die Arbeit ruht\.$", Betroffen.Betrieb),
        A(KlangAnsagenErkennung.Id.LagerVoll, @"^(?<wer>.+?): Das Lager für .+ ist voll – die Arbeit ruht\.$", Betroffen.Betrieb),
    };

    static (string, Regex, Betroffen) A(string id, string muster, Betroffen art) => (id, new Regex(muster, RegexOptions.CultureInvariant | RegexOptions.Singleline), art);

    static string? AlteAnsage(string text, Stadtwirtschaft w, Konto k)
    {
        foreach (var (id, muster, art) in AlteMuster)
        {
            var treffer = muster.Match(text);
            if (!treffer.Success) continue;
            var wer = treffer.Groups["wer"].Value;
            var eigen = art switch
            {
                Betroffen.Stadt => true,
                Betroffen.Haus => wer == k.Inhaber,
                Betroffen.Betrieb => w.Betriebe.Any(b => b.Konto == k && b.Name == wer),
                _ => (w.Familien.DiesesJahr.LastOrDefault(e => e.Text == text) ?? w.Familien.LetztesJahr.LastOrDefault(e => e.Text == text))?.Konto == k,
            };
            return eigen ? id : null;
        }
        return null;
    }

    /// <summary>Wo die neue Erkennung absichtlich mehr ansagt als die alte: Blitzschlag und Funkenflug, Bankrott, das erloschene Haus.</summary>
    static bool NeuAngesagt(Meldung m) =>
        m.Text.StartsWith("Blitzschlag! ", StringComparison.Ordinal) || m.Text.StartsWith("Funkenflug! ", StringComparison.Ordinal)
        || m.Text.Contains(" ist bankrott!", StringComparison.Ordinal) || m.Text.Contains(" ist erloschen", StringComparison.Ordinal);

    static string Spielstandordner() => Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
        "tests", "Reichsgilde.Core.Tests", "Spielstaende");

    static readonly DateTime Zeitpunkt = new(2026, 11, 6, 12, 0, 0);

    static void TicksBisJahr(Simulation sim)
    {
        var j = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == j) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Meldungen: Familie – Heirat, Geburt, Krankheit, Pest, Tod und Erbfall melden Art, Haus und Person", () =>
        {
            var (sim, spieler, _, meldungen) = Partie();
            var w = sim.Wirtschaft!;
            var f = w.Familien.Von(spieler)!;
            var heirat = Einzig(sim, spieler, meldungen, new EntwicklerHeirat(), Meldungsart.Heirat);
            Pruefe.Gleich(spieler, w.HausVon(heirat));
            Pruefe.Gleich(f.Ehepartner, w.PersonVon(heirat), "Person: der neue Ehepartner");
            Pruefe.Wahr(heirat.Text.StartsWith("Hochzeit im Haus "), heirat.Text);
            Pruefe.Gleich(Meldungswichtigkeit.Hoch, heirat.Wichtigkeit);
            var geburt = Einzig(sim, spieler, meldungen, new EntwicklerGeburt(Geschlecht.Mann), Meldungsart.Geburt);
            var kind = f.Mitglieder[^1];
            Pruefe.Gleich(kind, w.PersonVon(geburt), "Person: das Kind");
            var name = w.Gesundheit.Daten.Krankheiten.Keys.First(k => k != "Pest");
            var krank = Einzig(sim, spieler, meldungen, new EntwicklerKrankheit(f.Oberhaupt.Nummer, name), Meldungsart.Krankheit);
            Pruefe.Gleich(f.Oberhaupt, w.PersonVon(krank));
            Pruefe.Gleich(Meldungswichtigkeit.Normal, krank.Wichtigkeit);
            var pest = Einzig(sim, spieler, meldungen, new EntwicklerKrankheit(kind.Nummer, "Pest"), Meldungsart.Krankheit);
            Pruefe.Gleich(kind, w.PersonVon(pest), "auch die Pest ist eine Krankheit der Person");
            Pruefe.Gleich(Meldungswichtigkeit.Hoch, pest.Wichtigkeit, "die Pest im Haus ist wichtiger");
            var tod = Einzig(sim, spieler, meldungen, new EntwicklerTod(kind.Nummer), Meldungsart.TodFamilie);
            Pruefe.Gleich(kind, w.PersonVon(tod), "der Tote");
            Pruefe.Gleich(Meldungsort.Keiner, tod.Ort);
            w.Familien.KindAufnehmen(spieler, w.Familien.VolkDaten(f.Volk).Volljaehrig + 1, Geschlecht.Frau);
            var oberhaupt = f.Oberhaupt;
            var erbfall = Einzig(sim, spieler, meldungen, new EntwicklerTod(oberhaupt.Nummer), Meldungsart.TodOberhaupt);
            Pruefe.Gleich(oberhaupt, w.PersonVon(erbfall), "das verstorbene Oberhaupt");
            Pruefe.Gleich(Meldungswichtigkeit.Dringend, erbfall.Wichtigkeit);
            Pruefe.Wahr(meldungen.All(m => w.HausVon(m) == spieler || m.Art == Meldungsart.Sonstiges), "alle Familienmeldungen tragen das Haus");
        }),

        ("Meldungen: Einbruch und Brand in der Werkstatt (Ort Betrieb), Hausbrand (Ort Wohnhaus), Blitzschlag (Ort mit Lage) – für das betroffene Haus", () =>
        {
            var (sim, spieler, rivale, meldungen) = Partie();
            var w = sim.Wirtschaft!;
            var b = w.Betriebe.First(x => x.Konto == spieler && x.IstWerkstatt && x.Zutaten.Count > 0);
            var fremd = w.Betriebe.First(x => x.Konto == rivale && x.IstWerkstatt && x.Zutaten.Count > 0);
            sim.Bote.SofortAusfuehren(spieler, new EntwicklerZutaten(b.Name));
            sim.Bote.SofortAusfuehren(spieler, new EntwicklerZutaten(fremd.Name));
            foreach (var (betrieb, haus) in new[] { (b, spieler), (fremd, rivale) })
            {
                var dieb = Einzig(sim, spieler, meldungen, new EntwicklerDiebstahl(betrieb.Name), Meldungsart.Diebstahl);
                Pruefe.Gleich(Meldungsort.Betrieb, dieb.Ort);
                Pruefe.Gleich(betrieb.Name, dieb.Ortsname);
                Pruefe.Gleich(haus, w.HausVon(dieb));
                Pruefe.Gleich(Meldungswichtigkeit.Hoch, dieb.Wichtigkeit);
                Pruefe.Wahr(!dieb.Vertraulich, "ein Einbruch ist öffentlich");
                var feuer = Einzig(sim, spieler, meldungen, new EntwicklerFeuer(betrieb.Name), Meldungsart.Feuer);
                Pruefe.Gleich(Meldungsort.Betrieb, feuer.Ort);
                Pruefe.Gleich(haus, w.HausVon(feuer));
            }
            var hausbrand = Einzig(sim, spieler, meldungen, new EntwicklerHausbrand(rivale.Inhaber), Meldungsart.Feuer);
            Pruefe.Gleich(Meldungsort.Wohnhaus, hausbrand.Ort);
            Pruefe.Gleich(rivale, w.HausVon(hausbrand));
            if (w.Blitz.IstAktiv)
            {
                var blitz = Einzig(sim, spieler, meldungen, new EntwicklerBlitz(b.Name), Meldungsart.Feuer);
                Pruefe.Wahr(blitz.Text.StartsWith("Blitzschlag! "), blitz.Text);
                Pruefe.Gleich(Meldungsort.Betrieb, blitz.Ort);
                Pruefe.Gleich(spieler, w.HausVon(blitz));
                Pruefe.Wahr(blitz.X is not null && blitz.Z is not null, "Blitzschlag mit Lage");
                Pruefe.Gleich(KlangAnsagenErkennung.Id.Feuer, KlangAnsagenErkennung.Erkennen(blitz, w, spieler), "Blitzschlag ins eigene Haus wird angesagt");
                Pruefe.Wahr(KlangAnsagenErkennung.Erkennen(blitz, w, rivale) is null, "beim Rivalen nicht");
            }
        }),

        ("Meldungen: echte Partien (Köln und Greybox, je 3 Jahre) – die Ansagen nach Art sind für jedes Haus dieselben wie nach dem alten Wortlaut; Nachrichten an Auftraggeber vertraulich", () =>
        {
            var gemeldet = new List<Meldung>();
            var abweichungen = new List<string>();
            var angesagt = 0;
            foreach (var (karte, startwert) in new[] { ("koeln", 101), ("greybox", 4711) })
            {
            var angemeldet = false;
            var diese = new List<Meldung>();
            var ergebnis = Messpartie.Spielen(Meldungsmessung.Aufbau("Ausbau", karte, startwert, 3), new Messquellen(), jeMinute: w =>
            {
                if (angemeldet) return;
                angemeldet = true;
                w.Gemeldet += m =>
                {
                    gemeldet.Add(m);
                    diese.Add(m);
                    foreach (var k in w.Konten)
                    {
                        var alt = AlteAnsage(m.Text, w, k);
                        var neu = KlangAnsagenErkennung.Erkennen(m, w, k);
                        if (neu is not null) angesagt++;
                        if (alt != neu && !(alt is null && NeuAngesagt(m)))
                            abweichungen.Add($"{k.Inhaber}: alt {alt ?? "–"}, neu {neu ?? "–"} ({m.Art}, {m.Wichtigkeit}): {m.Text}");
                    }
                };
            });
            Pruefe.Wahr(ergebnis.Fehler is null, $"Partie {karte} ohne Fehler: " + ergebnis.Fehler);
            for (var i = 1; i < diese.Count; i++)
                Pruefe.Wahr(diese[i].Minute >= diese[i - 1].Minute, $"Spielminuten steigen ({karte})");
            }
            // seit den Räumen im Wohnhaus (Datenversion 87) heuern nur Häuser mit Hinterzimmer Banden an – etwas weniger Meldungen als die früheren 200
            Pruefe.Wahr(gemeldet.Count > 150, $"genug Meldungen ({gemeldet.Count})");
            Pruefe.Wahr(abweichungen.Count == 0, $"{abweichungen.Count} Abweichungen, etwa: {string.Join("\n    ", abweichungen.Take(5))}");
            Pruefe.Wahr(angesagt > 20, $"genug Ansagen verglichen ({angesagt})");
            var arten = gemeldet.Select(m => m.Art).Distinct().Count();
            Pruefe.Wahr(arten >= 6, $"mehrere Arten kommen vor ({arten})");
            // Nachrichten der Unterwelt an ihren Auftraggeber: vertraulich, mit Haus
            var geheim = gemeldet.Where(m => m.Text.StartsWith("Eure ", StringComparison.Ordinal)).ToList();
            Pruefe.Wahr(geheim.All(m => m.Vertraulich && m.Haus is not null), "„Eure …“ ist vertraulich und trägt das Haus");
            Pruefe.Wahr(gemeldet.Where(m => m.Vertraulich).All(m => m.Haus is not null), "vertraulich nur mit Haus");
            // Spitzelberichte gehören dem Auftraggeber
            foreach (var m in gemeldet.Where(m => m.Text.StartsWith("Der Spitzel von ", StringComparison.Ordinal)))
                Pruefe.Wahr(m.Vertraulich && m.Haus is not null, "Spitzelbericht vertraulich: " + m.Text);
            // Betrieb als Ort: der Name ist ein Betrieb, der Stillstand nennt ihn vorn
            foreach (var m in gemeldet.Where(m => m.Art is Meldungsart.LagerLeer or Meldungsart.LagerVoll or Meldungsart.KasseKnapp))
                Pruefe.Wahr(m.Ort == Meldungsort.Betrieb && m.Text.StartsWith(m.Ortsname + ": ", StringComparison.Ordinal), "Ort des Betriebs: " + m.Text);
        }),

        ("Meldungen: Verlauf im Spielstand – speichern, laden und gleich weiterspielen; ohne Feld oder alter Spielstand leer; höchstens 500, unbekannte Häuser fallen weg", () =>
        {
            var (sim, spieler, _, _) = Partie(4711);
            TicksBisJahr(sim);
            for (var i = 0; i < 400; i++) sim.Tick();
            var w = sim.Wirtschaft!;
            Pruefe.Wahr(w.Meldungsverlauf.Count > 20, $"Verlauf gefüllt ({w.Meldungsverlauf.Count})");
            Pruefe.Wahr(w.Meldungsverlauf.Count <= Stadtwirtschaft.MeldungsverlaufLaenge, "höchstens die Länge");
            var json = Speicherung.Speichern(sim, spieler, Zeitpunkt);
            var (geladen, _) = Speicherung.Laden(json);
            var w2 = geladen.Wirtschaft!;
            Pruefe.Gleich(w.Meldungsverlauf.Count, w2.Meldungsverlauf.Count);
            for (var i = 0; i < w.Meldungsverlauf.Count; i++)
                Pruefe.Wahr(w.Meldungsverlauf[i] == w2.Meldungsverlauf[i], $"Meldung {i} gleich: {w.Meldungsverlauf[i]} / {w2.Meldungsverlauf[i]}");
            Pruefe.Gleich(Zustandspruefsumme.Berechnen(sim), Zustandspruefsumme.Berechnen(geladen), "derselbe Zustand nach dem Laden");
            for (var i = 0; i < 700; i++) { sim.Tick(); geladen.Tick(); }
            Pruefe.Gleich(Zustandspruefsumme.Berechnen(sim), Zustandspruefsumme.Berechnen(geladen), "weitergespielt gleich");
            Pruefe.Wahr(w.Meldungsverlauf.SequenceEqual(w2.Meldungsverlauf), "weitergespielt: derselbe Verlauf");

            // Ohne das Feld (Spielstand von davor): leer, und die Wirtschaft läuft gleich weiter
            var knoten = JsonNode.Parse(json)!.AsObject();
            Pruefe.Wahr(knoten.Remove("Meldungen"), "Abschnitt „Meldungen“ steht im Spielstand");
            var (ohne, _) = Speicherung.Laden(knoten.ToJsonString());
            Pruefe.Gleich(0, ohne.Wirtschaft!.Meldungsverlauf.Count);
            ohne.Tick();
            Pruefe.Wahr(ohne.Wirtschaft.Meldungsverlauf.All(m => m.Minute == ohne.Zeit.VergangeneMinuten - 1 || m.Minute == ohne.Zeit.VergangeneMinuten), "nur Neues");

            // Alter Spielstand (Format 2, Datenversion 55): lädt ohne Verlauf, danach füllt er sich
            using var gz = new GZipStream(File.OpenRead(Path.Combine(Spielstandordner(), "format2-greybox-herbst1406.json.gz")), CompressionMode.Decompress);
            var (alt, _) = Speicherung.Laden(new StreamReader(gz).ReadToEnd());
            Pruefe.Gleich(0, alt.Wirtschaft!.Meldungsverlauf.Count);
            TicksBisJahr(alt);
            Pruefe.Wahr(alt.Wirtschaft.Meldungsverlauf.Count > 0, "nach einem Jahr gibt es Meldungen");

            // Höchstens 500 (die neuesten); eine unbekannte Kontonummer wird zu „kein Haus“
            var viele = Enumerable.Range(0, 600).Select(i => new MeldungStand { Art = Meldungsart.LagerLeer, Text = $"M{i}", Minute = i, Haus = i == 599 ? 999 : 0 }).ToList();
            w2.MeldungenWiederherstellen(viele);
            Pruefe.Gleich(Stadtwirtschaft.MeldungsverlaufLaenge, w2.Meldungsverlauf.Count);
            Pruefe.Gleich("M100", w2.Meldungsverlauf[0].Text);
            Pruefe.Gleich((int?)null, w2.Meldungsverlauf[^1].Haus);
            Pruefe.Gleich((int?)0, w2.Meldungsverlauf[^2].Haus);
            // eine neue Meldung schiebt die älteste hinaus
            w2.Melden("Noch eine.");
            Pruefe.Gleich(Stadtwirtschaft.MeldungsverlaufLaenge, w2.Meldungsverlauf.Count);
            Pruefe.Gleich("M101", w2.Meldungsverlauf[0].Text);
            Pruefe.Gleich(Meldungsart.Sonstiges, w2.Meldungsverlauf[^1].Art);
        }),

        ("Meldungen: Einstufung nach dem Haus des Kerns – eigen, fremd, vertraulich verworfen; die eigene Klage gegen ein anderes Haus bleibt eigen", () =>
        {
            var (sim, spieler, rivale, _) = Partie();
            var w = sim.Wirtschaft!;
            var ordnung = Meldungsordnung.AusJson(File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
                "game", "daten", "meldungen.json")));
            int Nr(Konto k) => w.Konten.ToList().IndexOf(k);
            Einstufung E(Meldung m) => ordnung.Einstufen(w, spieler, m.Text, m);
            var b = w.Betriebe.First(x => x.Konto == spieler);
            var eigen = E(new Meldung(Meldungsart.LagerLeer, $"{b.Name}: Es fehlt Holz – die Arbeit ruht.") { Haus = Nr(spieler), Ort = Meldungsort.Betrieb, Ortsname = b.Name });
            Pruefe.Gleich(Betroffenheit.Eigen, eigen.Bezug);
            Pruefe.Gleich("Stillstand", eigen.Art.Id, "Art aus dem Feld „kern“");
            var tod = E(new Meldung(Meldungsart.TodFamilie, "Trauer im Haus X: jemand ist gestorben.") { Haus = Nr(rivale) });
            Pruefe.Gleich(Betroffenheit.Fremd, tod.Bezug);
            Pruefe.Gleich("Tod", tod.Art.Id);
            var geheim = E(new Meldung(Meldungsart.Sonstiges, "Eure Diebe haben in X Ware erbeutet.") { Haus = Nr(rivale), Vertraulich = true });
            Pruefe.Wahr(geheim.Verwerfen, "fremde vertrauliche Nachricht verworfen");
            Pruefe.Wahr(!E(new Meldung(Meldungsart.Sonstiges, "Eure Diebe haben in X Ware erbeutet.") { Haus = Nr(spieler), Vertraulich = true }).Verwerfen, "eigene bleibt");
            var klage = E(new Meldung(Meldungsart.Anklage, $"{spieler.Inhaber} klagt {rivale.Inhaber} wegen Diebstahl an (2 Beweise).") { Haus = Nr(rivale) });
            Pruefe.Gleich(Betroffenheit.Eigen, klage.Bezug, "die eigene Klage");
            Pruefe.Wahr(KlangAnsagenErkennung.Erkennen(new Meldung(Meldungsart.Anklage, "x") { Haus = Nr(rivale), Wichtigkeit = Meldungswichtigkeit.Hoch }, w, spieler) is null,
                "angesagt wird sie nur dem Beklagten");
        }),

        ("Meldungen: derselbe Verlauf in zwei Läufen und mit deutscher Ländereinstellung (Mehrspieler)", () =>
        {
            string Lauf(string kultur)
            {
                var (vorher, vorherUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(kultur);
                    var (sim, _, _, _) = Partie(2027);
                    TicksBisJahr(sim);
                    for (var i = 0; i < 300; i++) sim.Tick();
                    var knoten = JsonNode.Parse(Speicherung.Speichern(sim, null, Zeitpunkt))!.AsObject();
                    return knoten["Meldungen"]!.ToJsonString();
                }
                finally
                {
                    (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (vorher, vorherUi);
                }
            }
            var a = Lauf("");
            Pruefe.Wahr(a.Length > 1000, "Verlauf gefüllt");
            Pruefe.Gleich(a, Lauf(""), "zweiter Lauf");
            Pruefe.Gleich(a, Lauf("de-DE"), "deutsche Ländereinstellung");
        }),
    };
}
