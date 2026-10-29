using System.Reflection;
using Reichsgilde.Core;
using Reichsgilde.Core.Mehrspieler;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für den Befehlsboten (Mehrspieler M2): Warteschlange mit Ziel-Tick, Rückruf, Spielstand, Aufzeichnen und Abspielen.</summary>
static class BefehlsboteTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));
    static readonly DateTime Zeitpunkt = new(2026, 10, 29, 12, 0, 0);

    static (Simulation Sim, Konto Spieler) NeuePartie(int startwert = 1400) =>
        Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos },
            Daten.Value, new NeuePartie());

    static void Ticken(Simulation sim, long minuten)
    {
        for (long i = 0; i < minuten; i++) sim.Tick();
    }

    /// <summary>Die erste Werkstatt in der Stadt, die diesem Haus gehört.</summary>
    static Betrieb Werkstatt(Simulation sim, Konto konto) => sim.Wirtschaft!.Betriebe.First(b => b.Konto == konto && b.InDerStadt);

    static Konto Rivale(Simulation sim, Konto spieler) => sim.Wirtschaft!.Konten.First(k => k != spieler && k.IstAktiv);

    static string Stand(Simulation sim) => Speicherung.Speichern(sim, zeitpunkt: Zeitpunkt);

    /// <summary>Vergleicht zwei Spielstände und nennt die erste abweichende Zeile.</summary>
    static void GleicherStand(string a, string b)
    {
        if (a == b) return;
        var za = a.Split('\n');
        var zb = b.Split('\n');
        var erste = Enumerable.Range(0, Math.Min(za.Length, zb.Length)).FirstOrDefault(i => za[i] != zb[i]);
        throw new Exception($"Spielstände weichen ab, erste Abweichung in Zeile {erste + 1}:\n  {za[erste].Trim()}\n  {zb[erste].Trim()}");
    }

    /// <summary>Ein Beispielwert für jeden Typ, den ein Befehl trägt (für den Test „jede Befehlsart“).</summary>
    static object? Beispiel(Type typ)
    {
        if (Nullable.GetUnderlyingType(typ) is { } innen) return Beispiel(innen);
        if (typ == typeof(string)) return "Probe „ä“";
        if (typ == typeof(int)) return 3;
        if (typ == typeof(long)) return 4L;
        if (typ == typeof(double)) return 0.1 + 0.2; // keine glatte Zahl: muss exakt zurückkommen
        if (typ == typeof(bool)) return true;
        if (typ.IsEnum) return Enum.GetValues(typ).GetValue(Enum.GetValues(typ).Length - 1);
        if (typ == typeof(IReadOnlyDictionary<string, int>)) return new Dictionary<string, int> { ["Eisen"] = 2, ["Holz"] = 5 };
        throw new Exception($"Für den Typ {typ.Name} kennt der Test keinen Beispielwert – bitte ergänzen.");
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Befehlsbote: Einzelspiel wirkt sofort, Rückruf vor der Rückkehr, gleicher Stand wie Ausfuehren", () =>
        {
            var (a, spielerA) = NeuePartie();
            var (b, spielerB) = NeuePartie();
            Ticken(a, 300);
            Ticken(b, 300);
            Pruefe.Gleich(0, a.Bote.Verzoegerung);
            Befehlsergebnis? ergebnis = null;
            var aufrufe = 0;
            a.Bote.Einreihen(spielerA, new LohnstufeSetzen(Werkstatt(a, spielerA).Name, Lohnstufe.Gut), e => { ergebnis = e; aufrufe++; });
            Pruefe.Wahr(ergebnis is { Erfolg: true }, "Rückruf nicht oder mit Ablehnung: " + ergebnis?.Meldung);
            Pruefe.Gleich(Lohnstufe.Gut, Werkstatt(a, spielerA).Lohnstufe);
            Pruefe.Gleich(0, a.Bote.Warteschlange.Count);
            var direkt = b.Wirtschaft!.Ausfuehren(spielerB, new LohnstufeSetzen(Werkstatt(b, spielerB).Name, Lohnstufe.Gut));
            Pruefe.Gleich(direkt, ergebnis);
            Ticken(a, 2000);
            Ticken(b, 2000);
            Pruefe.Gleich(1, aufrufe);
            GleicherStand(Stand(b), Stand(a));
        }),

        ("Befehlsbote: Ziel-Tick in der Zukunft wirkt am Anfang dieses Ticks – wie ein Befehl nach so vielen Ticks", () =>
        {
            var (a, spielerA) = NeuePartie(1401);
            var (b, spielerB) = NeuePartie(1401);
            Ticken(a, 500);
            Ticken(b, 500);
            a.Bote.Verzoegerung = 3;
            var start = a.Zeit.VergangeneMinuten;
            Befehlsergebnis? ergebnis = null;
            var nummer = a.Bote.Einreihen(spielerA, new WaechterSetzen(Werkstatt(a, spielerA).Name, true), e => ergebnis = e);
            Pruefe.Gleich(start + 3, a.Bote.Warteschlange.Single().ZielTick);
            Pruefe.Gleich(nummer, a.Bote.Warteschlange.Single().Nummer);
            Ticken(a, 3);
            Pruefe.Wahr(ergebnis is null, "Der Befehl wirkte vor seinem Ziel-Tick.");
            Pruefe.Gleich(false, Werkstatt(a, spielerA).Waechter);
            Ticken(a, 1);
            Pruefe.Wahr(ergebnis is { Erfolg: true }, "Der Befehl wirkte nicht im Ziel-Tick: " + ergebnis?.Meldung);
            Pruefe.Gleich(0, a.Bote.Warteschlange.Count);
            // Gegenstück: drei Ticks, dann direkt ausgeführt
            Ticken(b, 3);
            b.Wirtschaft!.Ausfuehren(spielerB, new WaechterSetzen(Werkstatt(b, spielerB).Name, true));
            Ticken(b, 1);
            Ticken(a, 1500);
            Ticken(b, 1500);
            GleicherStand(Stand(b), Stand(a));
        }),

        ("Befehlsbote: Reihenfolge im selben Tick – Ziel-Tick, dann Haus, dann laufende Nummer", () =>
        {
            var (sim, spieler) = NeuePartie();
            var rivale = Rivale(sim, spieler);
            Ticken(sim, 200);
            sim.Bote.Verzoegerung = 2;
            var reihenfolge = new List<string>();
            sim.Bote.Ausgefuehrt += (e, _) => reihenfolge.Add($"{e.ZielTick - 200}:{e.Konto}:{e.Nummer}");
            var werkstattR = Werkstatt(sim, rivale).Name;
            var werkstattS = Werkstatt(sim, spieler).Name;
            sim.Bote.Einreihen(rivale, new LohnstufeSetzen(werkstattR, Lohnstufe.Gut));   // Nr. 0, Haus 1, Tick +2
            sim.Bote.Einreihen(spieler, new LohnstufeSetzen(werkstattS, Lohnstufe.Gut));  // Nr. 1, Haus 0, Tick +2
            sim.Bote.Verzoegerung = 1;
            sim.Bote.Einreihen(rivale, new LohnstufeSetzen(werkstattR, Lohnstufe.Knapp)); // Nr. 2, Haus 1, Tick +1
            sim.Bote.Verzoegerung = 2;
            sim.Bote.Einreihen(spieler, new LohnstufeSetzen(werkstattS, Lohnstufe.Knapp)); // Nr. 3, Haus 0, Tick +2
            Pruefe.Gleich(0, Array.IndexOf(sim.Wirtschaft!.Konten.ToArray(), spieler));
            Pruefe.Gleich("1:1:2,2:0:1,2:0:3,2:1:0", string.Join(",", sim.Bote.Warteschlange.Select(e => $"{e.ZielTick - 200}:{e.Konto}:{e.Nummer}")));
            Ticken(sim, 3);
            Pruefe.Gleich("1:1:2,2:0:1,2:0:3,2:1:0", string.Join(",", reihenfolge));
            Pruefe.Gleich(Lohnstufe.Knapp, Werkstatt(sim, spieler).Lohnstufe); // der spätere Befehl des Hauses gewinnt
            Pruefe.Gleich(Lohnstufe.Gut, Werkstatt(sim, rivale).Lohnstufe);
        }),

        ("Befehlsbote: In der Pause (und nach dem Ende) wirkt ein Befehl auch mit Verzögerung sofort", () =>
        {
            var (sim, spieler) = NeuePartie();
            Ticken(sim, 100);
            sim.Bote.Verzoegerung = 2;
            var werkstatt = Werkstatt(sim, spieler);
            sim.Bote.Einreihen(spieler, new LohnstufeSetzen(werkstatt.Name, Lohnstufe.Knapp)); // läuft: wartet
            Pruefe.Gleich(1, sim.Bote.Warteschlange.Count);
            Pruefe.Wirft(() => sim.Bote.SofortAusfuehren(spieler, new LohnstufeSetzen(werkstatt.Name, Lohnstufe.Gut)));

            sim.Takt.IstPausiert = true;
            Pruefe.Wahr(sim.Bote.WirktSofort, "In der Pause muss ein Befehl sofort wirken.");
            Pruefe.Gleich(0, sim.Takt.Voranschreiten(5)); // die Pause bleibt die Pause
            var ergebnis = sim.Bote.SofortAusfuehren(spieler, new LohnstufeSetzen(werkstatt.Name, Lohnstufe.Gut));
            Pruefe.Wahr(ergebnis.Erfolg, ergebnis.Meldung);
            Pruefe.Gleich(Lohnstufe.Gut, werkstatt.Lohnstufe);
            Pruefe.Gleich(1, sim.Bote.Warteschlange.Count); // der ältere Befehl wartet weiter auf seinen Tick (+2)
            sim.Takt.IstPausiert = false;
            Ticken(sim, 3);
            Pruefe.Gleich(Lohnstufe.Knapp, werkstatt.Lohnstufe);

            sim.Bote.SofortWennZeitSteht = false; // so später im Gleichschritt
            sim.Takt.IstPausiert = true;
            sim.Bote.Einreihen(spieler, new LohnstufeSetzen(werkstatt.Name, Lohnstufe.Ueblich));
            Pruefe.Gleich(1, sim.Bote.Warteschlange.Count);
            sim.Bote.SofortWennZeitSteht = true;

            sim.Beenden("Testende");
            Pruefe.Wahr(sim.Bote.WirktSofort, "Nach dem Ende steht die Zeit.");
        }),

        ("Befehlsbote: Absender muss ein Haus der Stadt sein, Verzögerung nicht negativ", () =>
        {
            var (sim, _) = NeuePartie();
            Pruefe.WirftMitMeldung(() => sim.Bote.Einreihen(new Konto("Haus Fremd", 100, 1400), new StandErwerben()), "kein Haus");
            Pruefe.Wirft(() => sim.Bote.Verzoegerung = -1);
            Pruefe.WirftMitMeldung(() => new Simulation().Bote.Einreihen(new Konto("Haus Fremd", 100, 1400), new StandErwerben()), "Ohne Wirtschaft");
        }),

        ("Befehlsbote: Speichern und Laden mit voller Warteschlange – beide laufen gleich weiter", () =>
        {
            var (sim, spieler) = NeuePartie(4711);
            var rivale = Rivale(sim, spieler);
            Ticken(sim, 700);
            sim.Bote.Verzoegerung = 10;
            sim.Bote.Einreihen(spieler, new WaechterSetzen(Werkstatt(sim, spieler).Name, true));
            sim.Bote.Einreihen(rivale, new LohnstufeSetzen(Werkstatt(sim, rivale).Name, Lohnstufe.Knapp));
            sim.Bote.Verzoegerung = 4;
            sim.Bote.Einreihen(spieler, new Handelsauftrag(Werkstatt(sim, spieler).Name, 0,
                new Dictionary<string, int>(), new Dictionary<string, int> { ["Eisen"] = 2 }));
            Ticken(sim, 2);
            var json = Speicherung.Speichern(sim, spieler, Zeitpunkt);
            Pruefe.Wahr(json.Contains("\"Warteschlange\"") && json.Contains("\"Befehl\": \"Handelsauftrag\""), "Die Warteschlange fehlt im Spielstand.");
            var (geladen, geladenerSpieler) = Speicherung.Laden(json);
            Pruefe.Gleich(3, geladen.Bote.Warteschlange.Count);
            Pruefe.Gleich(json, Speicherung.Speichern(geladen, geladenerSpieler, Zeitpunkt));
            Ticken(sim, 1200);
            Ticken(geladen, 1200);
            Pruefe.Gleich(0, geladen.Bote.Warteschlange.Count);
            Pruefe.Gleich(true, Werkstatt(geladen, geladenerSpieler!).Waechter);
            GleicherStand(Speicherung.Speichern(sim, spieler, Zeitpunkt), Speicherung.Speichern(geladen, geladenerSpieler, Zeitpunkt));
        }),

        ("Befehlsbote: Spielstand ohne Warteschlange (leer oder alt) – kein Feld, leere Schlange", () =>
        {
            var (sim, spieler) = NeuePartie();
            Ticken(sim, 50);
            var json = Speicherung.Speichern(sim, spieler, Zeitpunkt);
            Pruefe.Wahr(!json.Contains("Warteschlange"), "Eine leere Schlange gehört nicht in den Spielstand (alte Form bleibt gleich).");
            var (geladen, _) = Speicherung.Laden(json);
            Pruefe.Gleich(0, geladen.Bote.Warteschlange.Count);
            // Ein eingereihter Befehl eines Hauses, das es nicht gibt, ist ein Fehler im Spielstand
            sim.Bote.Verzoegerung = 5;
            sim.Bote.Einreihen(spieler, new StandErwerben());
            var mitSchlange = Speicherung.Speichern(sim, spieler, Zeitpunkt);
            var schlange = mitSchlange.IndexOf("\"Warteschlange\"", StringComparison.Ordinal);
            var kaputt = mitSchlange[..schlange] + mitSchlange[schlange..].Replace("\"Konto\": 0,", "\"Konto\": 99,");
            Pruefe.WirftMitMeldung(() => Speicherung.Laden(kaputt), "keinem Haus");
            var unbekannt = Speicherung.Speichern(sim, spieler, Zeitpunkt).Replace("\"Befehl\": \"StandErwerben\"", "\"Befehl\": \"Zaubern\"");
            Pruefe.WirftMitMeldung(() => Speicherung.Laden(unbekannt), "kein gültiger Spielstand");
        }),

        ("Befehlsbote: Jede Befehlsart lässt sich speichern und lesen (Daten, kein Lambda)", () =>
        {
            var arten = typeof(Befehl).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(Befehl).IsAssignableFrom(t)).ToList();
            Pruefe.Wahr(arten.Count > 80, $"Nur {arten.Count} Befehlsarten gefunden.");
            Pruefe.Gleich(arten.Count, arten.Select(t => t.Name).Distinct().Count()); // die Art steht als Name im Spielstand
            var (sim, spieler) = NeuePartie();
            sim.Bote.Verzoegerung = 1_000_000; // nur einreihen, nie ausführen
            foreach (var art in arten)
            {
                var ctor = art.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
                var befehl = (Befehl)ctor.Invoke(ctor.GetParameters().Select(p => Beispiel(p.ParameterType)).ToArray());
                foreach (var feld in art.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    Pruefe.Wahr(!typeof(Delegate).IsAssignableFrom(feld.PropertyType), $"{art.Name}.{feld.Name} ist ein Lambda.");
                sim.Bote.Einreihen(spieler, befehl);
            }
            var json = Speicherung.Speichern(sim, spieler, Zeitpunkt);
            var (geladen, geladenerSpieler) = Speicherung.Laden(json);
            Pruefe.Gleich(arten.Count, geladen.Bote.Warteschlange.Count);
            Pruefe.Gleich(json, Speicherung.Speichern(geladen, geladenerSpieler, Zeitpunkt));
            foreach (var (vorher, nachher) in sim.Bote.Warteschlange.Zip(geladen.Bote.Warteschlange))
            {
                Pruefe.Gleich(vorher.Befehl.GetType(), nachher.Befehl.GetType());
                if (vorher.Befehl is not (Handelsauftrag or SchiffsauftragErteilen)) // Wörterbücher vergleicht ein record nur als Verweis
                    Pruefe.Gleich(vorher.Befehl, nachher.Befehl);
            }
        }),

        ("Befehlsprotokoll: Aufzeichnen und Abspielen ergibt denselben Spielstand (über den Jahreswechsel, mit Pause)", () =>
        {
            var (sim, spieler) = NeuePartie(4711);
            var rivale = Rivale(sim, spieler);
            Ticken(sim, 900);
            sim.Bote.AufzeichnungBeginnen();
            var werkstatt = Werkstatt(sim, spieler).Name;
            sim.Bote.Einreihen(spieler, new LohnstufeSetzen(werkstatt, Lohnstufe.Gut)); // sofort
            Ticken(sim, 40);
            sim.Bote.Verzoegerung = 2;
            sim.Bote.Einreihen(spieler, new WaechterSetzen(werkstatt, true));
            sim.Bote.Einreihen(rivale, new LohnstufeSetzen(Werkstatt(sim, rivale).Name, Lohnstufe.Knapp));
            Ticken(sim, 400); // über den Jahreswechsel (Minute 1140)
            sim.Takt.IstPausiert = true;
            sim.Bote.Einreihen(spieler, new PraemieZahlen(werkstatt)); // in der Pause: sofort
            sim.Takt.IstPausiert = false;
            sim.Bote.Einreihen(spieler, new LohnstufeSetzen(werkstatt, Lohnstufe.Knapp));
            Ticken(sim, 1);
            sim.Bote.Einreihen(spieler, new WaechterSetzen(werkstatt, false)); // wartet noch, wenn die Aufzeichnung endet
            sim.Takt.IstPausiert = true;
            sim.Bote.Einreihen(spieler, new LohnstufeSetzen(werkstatt, Lohnstufe.Gut)); // nach dem letzten Tick sofort
            var protokoll = sim.Bote.AufzeichnungBeenden();
            Pruefe.Wahr(protokoll.IstVollstaendig, "Protokoll unvollständig: " + protokoll.Luecke);
            Pruefe.Gleich(7, protokoll.Befehle.Count);
            Pruefe.Gleich(2, sim.Bote.Warteschlange.Count); // „knapp“ (+2) und der Wächter (+3) warten noch
            Pruefe.Wahr(protokoll.Befehle[^1].Sofort, "Der letzte Befehl wirkte nach dem letzten Tick sofort.");
            Pruefe.Wahr(sim.Zeit.Jahr == 1401, "Die Aufzeichnung sollte über den Jahreswechsel gehen.");

            var nachgerechnet = protokoll.Abspielen();
            GleicherStand(Stand(sim), Stand(nachgerechnet));
            // auch nach dem Weg über den JSON-Text
            var gelesen = Befehlsprotokoll.AusJson(protokoll.AlsJson());
            GleicherStand(Stand(sim), Stand(gelesen.Abspielen()));
            Pruefe.Gleich(protokoll.AlsJson(), gelesen.AlsJson());
        }),

        ("Befehlsprotokoll: Wartende Befehle des Endticks wirken beim Abspielen nicht zu früh", () =>
        {
            var (sim, spieler) = NeuePartie();
            Ticken(sim, 100);
            sim.Bote.AufzeichnungBeginnen();
            sim.Bote.Verzoegerung = 1;
            sim.Bote.Einreihen(spieler, new LohnstufeSetzen(Werkstatt(sim, spieler).Name, Lohnstufe.Knapp)); // Ziel-Tick 101 = Endtick
            Ticken(sim, 1);
            var protokoll = sim.Bote.AufzeichnungBeenden();
            Pruefe.Gleich(1, sim.Bote.Warteschlange.Count);
            Pruefe.Gleich(protokoll.Endtick, sim.Bote.Warteschlange[0].ZielTick);
            GleicherStand(Stand(sim), Stand(protokoll.Abspielen()));
        }),

        ("Befehlsprotokoll: Eine angehaltene Ratssitzung macht es unvollständig – abstimmen geht dort sofort", () =>
        {
            var sim = StadtkasseTests.NeueSimulation();
            var w = sim.Wirtschaft!;
            var reformer = StadtkasseTests.Haus(w, "Reformer", 50_000);
            StadtkasseTests.Sitz(w, "Gefängniswärter", reformer);
            var spieler = StadtkasseTests.Haus(w, "Spieler", 50_000);
            StadtkasseTests.Sitz(w, "Marktmeister", spieler);
            sim.Bote.AufzeichnungBeginnen();
            var antrag = sim.Bote.SofortAusfuehren(reformer, new StatutBeantragen(Fronstatut.FreieStadt));
            Pruefe.Wahr(antrag.Erfolg, antrag.Meldung);
            w.RatssitzungAnhalten = _ => true;
            sim.Bote.Verzoegerung = 2;
            var jahr = sim.Zeit.Jahr;
            while (sim.Zeit.Jahr == jahr) sim.Tick();
            Pruefe.Wahr(sim.Ratssitzung is not null, "Die Sitzung sollte anhalten.");
            Pruefe.Wahr(sim.Bote.WirktSofort, "Während der Sitzung steht die Zeit – abstimmen muss sofort gehen.");
            var stimme = sim.Bote.SofortAusfuehren(spieler, new StatutStimme(false));
            Pruefe.Wahr(stimme.Erfolg, stimme.Meldung);
            sim.RatssitzungAbschliessen();
            var protokoll = sim.Bote.AufzeichnungBeenden();
            Pruefe.Wahr(!protokoll.IstVollstaendig, "Die Sitzung fehlt im Protokoll, es muss als unvollständig gelten.");
            Pruefe.WirftMitMeldung(() => protokoll.Abspielen(), "unvollständig");
        }),
        ("Befehlsbote: Während eines Ticks wirkt nichts sofort – der Befehl wartet auf den nächsten Tick, das Abspielen stimmt", () =>
        {
            var (sim, spieler) = NeuePartie(4711);
            Ticken(sim, Spielzeit.MinutenProTag - 30);
            var werkstatt = Werkstatt(sim, spieler).Name;
            sim.Bote.AufzeichnungBeginnen();
            var imTick = new List<string>();
            var stufe = Lohnstufe.Knapp;
            void Befehlen(string anlass)
            {
                var minute = sim.Zeit.VergangeneMinuten;
                Pruefe.Wahr(sim.Bote.ImTick && !sim.Bote.WirktSofort, anlass + ": im Tick darf nichts sofort wirken.");
                Pruefe.WirftMitMeldung(() => sim.Bote.SofortAusfuehren(spieler, new StandErwerben()), "Während eines Ticks");
                sim.Bote.Einreihen(spieler, new LohnstufeSetzen(werkstatt, stufe), e => imTick.Add($"{anlass}:{e.Erfolg}"));
                stufe = stufe == Lohnstufe.Knapp ? Lohnstufe.Gut : Lohnstufe.Knapp;
                Pruefe.Gleich(sim.Bote.FruehesterTick, sim.Bote.Warteschlange[^1].ZielTick);
                Pruefe.Wahr(!sim.Bote.Warteschlange[^1].Sofort && sim.Bote.Warteschlange[^1].ZielTick >= minute, anlass + ": Ziel-Tick zu früh.");
            }
            sim.PhaseBegonnen += _ => Befehlen("Phase");
            sim.JahrBeendet += _ => Befehlen("Jahr");
            var ausMeldungen = 0;
            sim.Wirtschaft!.Meldung += _ => { if (ausMeldungen < 3 && sim.Bote.ImTick) { ausMeldungen++; Befehlen("Meldung"); } };
            Ticken(sim, 60);
            Pruefe.Wahr(!sim.Bote.ImTick, "Nach dem Tick ist der Merker wieder aus.");
            Pruefe.Wahr(imTick.Any(t => t.StartsWith("Jahr")), "Der Befehl aus JahrBeendet fehlt: " + string.Join(",", imTick));
            Pruefe.Gleich(0, sim.Bote.Warteschlange.Count);
            var protokoll = sim.Bote.AufzeichnungBeenden();
            Pruefe.Wahr(protokoll.Befehle.All(e => !e.Sofort), "Im Tick eingereiht ist nie sofort.");
            GleicherStand(Stand(sim), Stand(protokoll.Abspielen()));
        }),

        ("Befehlsbote: In der angehaltenen Ratssitzung wirkt nur die neue Stimme sofort, wartende Befehle bleiben bis nach der Sitzung", () =>
        {
            var sim = StadtkasseTests.NeueSimulation();
            var w = sim.Wirtschaft!;
            var reformer = StadtkasseTests.Haus(w, "Reformer", 50_000);
            StadtkasseTests.Sitz(w, "Gefängniswärter", reformer);
            var spieler = StadtkasseTests.Haus(w, "Spieler", 50_000);
            StadtkasseTests.Sitz(w, "Marktmeister", spieler);
            Pruefe.Wahr(sim.Bote.SofortAusfuehren(reformer, new StatutBeantragen(Fronstatut.FreieStadt)).Erfolg, "Antrag abgelehnt");
            w.RatssitzungAnhalten = _ => true;
            Ticken(sim, Spielzeit.MinutenProTag - 1);
            sim.Bote.Verzoegerung = 1;
            Befehlsergebnis? wartend = null;
            sim.Bote.Einreihen(spieler, new HauswaechterSetzen(true), e => wartend = e); // Ziel-Tick: gleich nach dem Jahreswechsel
            sim.Tick();
            Pruefe.Wahr(sim.Ratssitzung is not null, "Die Sitzung sollte anhalten.");
            Pruefe.Gleich(sim.Zeit.VergangeneMinuten, sim.Bote.Warteschlange.Single().ZielTick);
            var stimme = sim.Bote.SofortAusfuehren(spieler, new StatutStimme(false));
            Pruefe.Wahr(stimme.Erfolg, stimme.Meldung);
            Pruefe.Wahr(wartend is null, "Der wartende Befehl wurde in die Sitzung vorgezogen: " + wartend?.Meldung);
            sim.RatssitzungAbschliessen();
            sim.Tick();
            Pruefe.Wahr(wartend is not null && !wartend.Meldung.Contains("Ratssitzung"), "Nach der Sitzung: " + wartend?.Meldung);
        }),

        ("Befehlsbote: Spielende verwirft wartende Befehle mit Meldung; die Siegbedingung lässt das Protokoll vollständig, Beenden nicht", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 9, Siegbedingung = Siegbedingung.Endjahr, Endjahr = 1401 },
                Daten.Value, new NeuePartie());
            var werkstatt = Werkstatt(sim, spieler).Name;
            Ticken(sim, Spielzeit.MinutenProTag - 50);
            sim.Bote.AufzeichnungBeginnen();
            sim.Bote.Verzoegerung = 200;
            Befehlsergebnis? verworfen = null;
            sim.Bote.Einreihen(spieler, new LohnstufeSetzen(werkstatt, Lohnstufe.Gut), e => verworfen = e);
            sim.Bote.Verzoegerung = 10;
            sim.Bote.Einreihen(spieler, new LohnstufeSetzen(werkstatt, Lohnstufe.Knapp));
            Ticken(sim, 60);
            Pruefe.Wahr(sim.IstBeendet, "Die Partie sollte 1401 enden.");
            Pruefe.Wahr(verworfen is { Erfolg: false } && verworfen.Meldung.Contains("vorbei"), "Rückruf beim Spielende: " + verworfen?.Meldung);
            Pruefe.Gleich(0, sim.Bote.Warteschlange.Count);
            var nachher = sim.Bote.SofortAusfuehren(spieler, new WaechterSetzen(werkstatt, true)); // nach dem Ende steht die Zeit: sofort
            Pruefe.Wahr(nachher.Erfolg, nachher.Meldung);
            var protokoll = sim.Bote.AufzeichnungBeenden();
            Pruefe.Wahr(protokoll.IstVollstaendig, "Die Siegbedingung rechnet jede Partie selbst nach: " + protokoll.Luecke);
            GleicherStand(Stand(sim), Stand(protokoll.Abspielen()));

            var (zweite, spieler2) = NeuePartie();
            Ticken(zweite, 10);
            zweite.Bote.AufzeichnungBeginnen();
            zweite.Beenden("Testende");
            Pruefe.Wahr(!zweite.Bote.AufzeichnungBeenden().IstVollstaendig, "Beenden von außen ist kein Befehl – Lücke erwartet.");
        }),

        ("Befehlsbote: Eine Ausnahme im Rückruf oder Empfänger hält die anderen Befehle nicht auf und kommt danach", () =>
        {
            var (sim, spieler) = NeuePartie();
            Ticken(sim, 100);
            var werkstatt = Werkstatt(sim, spieler).Name;
            sim.Bote.Verzoegerung = 1;
            var gemeldet = new List<long>();
            var zweiter = 0;
            sim.Bote.Einreihen(spieler, new LohnstufeSetzen(werkstatt, Lohnstufe.Gut), _ => throw new InvalidOperationException("Rückruf kaputt"));
            sim.Bote.Einreihen(spieler, new LohnstufeSetzen(werkstatt, Lohnstufe.Knapp), _ => zweiter++);
            sim.Bote.Ausgefuehrt += (e, _) => { gemeldet.Add(e.Nummer); if (gemeldet.Count == 2) throw new Exception("Empfänger kaputt"); };
            Ticken(sim, 1); // Ziel-Tick 101 noch nicht
            var minute = sim.Zeit.VergangeneMinuten;
            Pruefe.WirftMitMeldung(() => sim.Tick(), "Rückruf kaputt");
            Pruefe.Gleich(1, zweiter);
            Pruefe.Gleich(2, gemeldet.Count);
            Pruefe.Gleich(0, sim.Bote.Warteschlange.Count);
            Pruefe.Gleich(Lohnstufe.Knapp, Werkstatt(sim, spieler).Lohnstufe);
            Pruefe.Gleich(minute, sim.Zeit.VergangeneMinuten); // der abgebrochene Tick läuft beim nächsten Aufruf
            Pruefe.Wahr(!sim.Bote.ImTick, "Auch nach dem Abbruch ist der Merker wieder aus.");
            sim.Tick();
            Pruefe.Gleich(minute + 1, sim.Zeit.VergangeneMinuten);
            Pruefe.Gleich(1, zweiter); // jeder Rückruf genau einmal
            // Sofort: Die Ausnahme kommt beim Einreihen, der Befehl hat trotzdem gewirkt
            sim.Bote.Verzoegerung = 0;
            Pruefe.WirftMitMeldung(() => sim.Bote.Einreihen(spieler, new LohnstufeSetzen(werkstatt, Lohnstufe.Gut), _ => throw new Exception("sofort kaputt")), "sofort kaputt");
            Pruefe.Gleich(Lohnstufe.Gut, Werkstatt(sim, spieler).Lohnstufe);
        }),

        ("Befehlsbote: Wiedereintritt – ein Befehl aus dem Rückruf wirkt danach, das Abspielen stimmt", () =>
        {
            var (sim, spieler) = NeuePartie(4711);
            Ticken(sim, 300);
            var werkstatt = Werkstatt(sim, spieler).Name;
            sim.Bote.AufzeichnungBeginnen();
            var reihe = new List<string>();
            sim.Bote.Ausgefuehrt += (e, _) => reihe.Add($"{e.Nummer}{(e.Sofort ? "s" : "w")}");
            // sofort (Pause): Der Rückruf reiht zwei weitere ein – sie wirken nach ihm, in der Reihenfolge der Nummern
            sim.Takt.IstPausiert = true;
            sim.Bote.Einreihen(spieler, new LohnstufeSetzen(werkstatt, Lohnstufe.Gut), _ =>
            {
                Pruefe.WirftMitMeldung(() => sim.Bote.SofortAusfuehren(spieler, new StandErwerben()), "erst danach");
                sim.Bote.Einreihen(spieler, new WaechterSetzen(werkstatt, true));
                sim.Bote.Einreihen(spieler, new LohnstufeSetzen(werkstatt, Lohnstufe.Knapp));
            });
            Pruefe.Gleich("0s,1s,2s", string.Join(",", reihe));
            Pruefe.Gleich(Lohnstufe.Knapp, Werkstatt(sim, spieler).Lohnstufe);
            // am Anfang eines Ticks: Der Rückruf eines wartenden Befehls reiht für den nächsten Tick ein
            sim.Takt.IstPausiert = false;
            sim.Bote.Verzoegerung = 1;
            sim.Bote.Einreihen(spieler, new LohnstufeSetzen(werkstatt, Lohnstufe.Gut), _ => sim.Bote.Einreihen(spieler, new PraemieZahlen(werkstatt)));
            Ticken(sim, 2);
            Pruefe.Gleich(1, sim.Bote.Warteschlange.Count);
            Pruefe.Gleich(sim.Zeit.VergangeneMinuten + 1, sim.Bote.Warteschlange[0].ZielTick); // Tick +1, dann + Verzögerung 1
            Ticken(sim, 2);
            Pruefe.Gleich("0s,1s,2s,3w,4w", string.Join(",", reihe));
            GleicherStand(Stand(sim), Stand(sim.Bote.AufzeichnungBeenden().Abspielen()));
        }),

        ("Befehlsbote: Reihenfolge im Tick – sofortige vor wartenden, dann Haus vor Nummer (auch beim Abspielen)", () =>
        {
            // Hält das jetzige Verhalten fest: Wirken zwei Häuser im selben Tick sofort, ordnet das Abspielen sie nach dem Haus, nicht nach
            // der Ankunft. Im Einzelspiel gibt es nur ein Haus am Boten; im Gleichschritt kommt der Pausen-Takt (M3).
            var (sim, spieler) = NeuePartie();
            var rivale = Rivale(sim, spieler);
            var r = Array.IndexOf(sim.Wirtschaft!.Konten.ToArray(), rivale);
            var t = sim.Zeit.VergangeneMinuten;
            sim.Bote.Empfangen(new EingereihterBefehl(t, 0, 7, new StandErwerben()));           // wartend, Haus 0
            sim.Bote.Empfangen(new EingereihterBefehl(t, r, 1, new StandErwerben(), Sofort: true)); // sofort, Rivale, früher angekommen
            sim.Bote.Empfangen(new EingereihterBefehl(t, 0, 2, new StandErwerben(), Sofort: true)); // sofort, Haus 0, später angekommen
            sim.Bote.Empfangen(new EingereihterBefehl(t, 0, 0, new StandErwerben()));           // wartend, Haus 0, kleinere Nummer
            Pruefe.Gleich($"0:2:s,{r}:1:s,0:0:w,0:7:w", string.Join(",", sim.Bote.Warteschlange.Select(e => $"{e.Konto}:{e.Nummer}:{(e.Sofort ? "s" : "w")}")));
            Pruefe.Gleich(8L, sim.Bote.NaechsteNummer);
        }),

        ("Befehlsbote: Empfangen lehnt einen Ziel-Tick in der Vergangenheit ab; die Nummer zählt nach dem Laden hinter der Schlange weiter", () =>
        {
            var (sim, spieler) = NeuePartie();
            Ticken(sim, 50);
            Pruefe.WirftMitMeldung(() => sim.Bote.Empfangen(new EingereihterBefehl(49, 0, 0, new StandErwerben())), "zu spät");
            sim.Bote.Empfangen(new EingereihterBefehl(50, 0, 0, new StandErwerben())); // der nächste Tick geht noch
            sim.Bote.SofortAusfuehren(spieler, new StandErwerben());                   // Nr. 1, nur im Protokoll
            Pruefe.Gleich(2L, sim.Bote.NaechsteNummer);
            sim.Bote.Verzoegerung = 5;
            sim.Bote.Einreihen(spieler, new StandErwerben()); // Nr. 2
            sim.Bote.Einreihen(spieler, new StandErwerben()); // Nr. 3
            Pruefe.Gleich(4L, sim.Bote.NaechsteNummer);
            var (geladen, _) = Speicherung.Laden(Speicherung.Speichern(sim, spieler, Zeitpunkt));
            Pruefe.Gleich(4L, geladen.Bote.NaechsteNummer);
            Pruefe.Gleich("0,2,3", string.Join(",", geladen.Bote.Warteschlange.Select(e => e.Nummer)));
            geladen.Bote.Verzoegerung = 5;
            Pruefe.Gleich(4L, geladen.Bote.Einreihen(geladen.Wirtschaft!.Konten[0], new StandErwerben()));
            Ticken(geladen, 10);
            var (leer, _) = Speicherung.Laden(Speicherung.Speichern(geladen, null, Zeitpunkt));
            Pruefe.Gleich(0L, leer.Bote.NaechsteNummer);
        }),
    };
}
