using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Entscheidungen;
using Reichsgilde.Core.Entwickler;
using Reichsgilde.Core.Geheimnisse;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Entscheidungen Teil 2 (Benachrichtigungen Stufe 3): Krankheit (Medicus, Kräuterschnaps, nichts), Erbe bestimmen, Geheimnis entdeckt,
/// Mahnung des Kämmerers – Anlass, Frist von 120 Spielminuten, Voreinstellung, Doppelweg, „einmal je Anlass“, Obergrenze, Spielstand.
/// Jede Regel mit Gegenprobe. Krankheiten und Geheimnisse kommen über das Entwicklerfenster.
/// </summary>
static class EntscheidungTeil2Tests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));
    static readonly DateTime Zeitpunkt = new(2026, 11, 10, 12, 0, 0);

    /// <summary>Ein Mensch mit Familie (Oberhaupt im Alter <paramref name="alter"/>, <paramref name="kinder"/> Kinder) und ein anderes Haus mit Familie.</summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto Mensch, Konto Rabe) Partie(double vermoegen = 10_000, int alter = 40, int kinder = 2)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value) { EntwicklerErlaubt = true };
        var w = sim.Wirtschaft!;
        var mensch = w.KontoEroeffnen("Haus Mensch", startvermoegen: vermoegen);
        w.Familien.Gruenden(mensch, Volk.Mensch, Geschlecht.Mann, alter: alter);
        for (var i = 0; i < kinder; i++) w.Familien.KindAufnehmen(mensch, 18 - i, i % 2 == 0 ? Geschlecht.Frau : Geschlecht.Mann);
        var rabe = w.KontoEroeffnen("Haus Rabe", startvermoegen: 10_000);
        w.Familien.Gruenden(rabe, Volk.Mensch, Geschlecht.Mann, alter: 40);
        return (sim, w, mensch, rabe);
    }

    static Person Kind(Stadtwirtschaft w, Konto k, int i = 0) => w.Familien.Von(k)!.Kinder.Where(p => p.Lebt).OrderBy(p => p.Nummer).ElementAt(i);

    static void Krank(Stadtwirtschaft w, Konto k, Person p, string name) =>
        Pruefe.Wahr(w.Ausfuehren(k, new EntwicklerKrankheit(p.Nummer, name)).Erfolg, $"{p.Vorname} erkrankt an {name}");

    static List<Entscheidung> Von(Stadtwirtschaft w, Konto k, Entscheidungsart art) => w.Entscheidungen.OffenFuer(k).Where(e => e.Art == art).ToList();

    static void BisFrist(Simulation sim, Entscheidung e)
    {
        while (sim.Zeit.VergangeneMinuten <= e.Frist) sim.Tick();
    }

    static List<(Entscheidung E, Entscheidungsabschluss A)> Abschluesse(Stadtwirtschaft w)
    {
        var liste = new List<(Entscheidung, Entscheidungsabschluss)>();
        w.Entscheidungen.Geschlossen += (e, a) => liste.Add((e, a));
        return liste;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Entscheidungen Teil 2: Krankheit – Frage mit 120 Minuten Frist; die Voreinstellung ruft den Medicus, wenn bezahlbar (Kind geheilt)", () =>
        {
            var (sim, w, mensch, rabe) = Partie();
            var kind = Kind(w, mensch);
            var abschluesse = Abschluesse(w);
            Krank(w, mensch, kind, "Ruhr");
            Krank(w, rabe, w.Familien.Von(rabe)!.Oberhaupt, "Ruhr");
            sim.Tick();
            var e = Von(w, mensch, Entscheidungsart.Krankheit).Single();
            Pruefe.Gleich(rabe, Von(w, rabe, Entscheidungsart.Krankheit).Single().Haus, "jedes Haus eines Menschen bekommt nur seine eigene Frage");
            Pruefe.Gleich(kind.Nummer, e.Person);
            Pruefe.Gleich(e.Beginn + 120, e.Frist);
            Pruefe.Gleich(3, w.Entscheidungen.Wahlen(e).Count);
            Pruefe.Gleich(Entscheidungswesen.Medicus, w.Entscheidungen.Voreinstellung(e));
            Pruefe.Wahr(w.Entscheidungen.Folgen(e, Entscheidungswesen.NichtsTun).Contains("4 %"), w.Entscheidungen.Folgen(e, Entscheidungswesen.NichtsTun));
            var geld = mensch.Vermoegen;
            BisFrist(sim, e);
            Pruefe.Wahr(kind.Krankheit is null, "geheilt");
            Pruefe.Ungefaehr(geld - 120, mensch.Vermoegen, 1e-6);
            Pruefe.Gleich(Entscheidungsabschluss.Frist, abschluesse.Single(x => x.E.Haus == mensch).A);
            Pruefe.Gleich(0, w.Entscheidungen.Offen.Count(x => x.Haus == mensch));
        }),

        ("Entscheidungen Teil 2: Krankheit ohne Geld – Kräuterschnaps aus der Hausapotheke, ohne Vorrat nichts; danach wird nicht wieder gefragt", () =>
        {
            foreach (var mitSchnaps in new[] { true, false })
            {
                var (sim, w, mensch, _) = Partie();
                var kind = Kind(w, mensch);
                if (mitSchnaps) Pruefe.Wahr(w.Ausfuehren(mensch, new SchnapsKaufen(1)).Erfolg, "Schnaps gekauft");
                w.Ausfuehren(mensch, new EntwicklerGulden(-(mensch.Vermoegen - 50)));
                Krank(w, mensch, kind, "Husten");
                sim.Tick();
                var e = Von(w, mensch, Entscheidungsart.Krankheit).Single();
                Pruefe.Gleich(mitSchnaps ? Entscheidungswesen.Schnaps : Entscheidungswesen.NichtsTun, w.Entscheidungen.Voreinstellung(e));
                BisFrist(sim, e);
                Pruefe.Gleich(mitSchnaps, kind.Krankheit is null, mitSchnaps ? "Schnaps heilt den Husten" : "ohne Schnaps bleibt er krank");
                for (var i = 0; i < 5; i++) sim.Tick();
                Pruefe.Gleich(0, Von(w, mensch, Entscheidungsart.Krankheit).Count, "einmal je Anlass");
            }
        }),

        ("Entscheidungen Teil 2: Krankheit – Medicus im Charakterfenster schließt die Frage (Doppelweg); bei der Pest bleibt die Person krank und wird nicht wieder gefragt", () =>
        {
            var (sim, w, mensch, _) = Partie();
            var abschluesse = Abschluesse(w);
            var kind = Kind(w, mensch);
            var sohn = Kind(w, mensch, 1);
            Krank(w, mensch, kind, "Pest");
            Krank(w, mensch, sohn, "Ruhr");
            sim.Tick();
            Pruefe.Gleich(2, Von(w, mensch, Entscheidungsart.Krankheit).Count);
            Pruefe.Wahr(w.Ausfuehren(mensch, new MedicusRufen(kind.Nummer)).Erfolg, "Medicus direkt");
            Pruefe.Wahr(kind.Krankheit is { Medicus: true }, "Pest bleibt, gepflegt");
            var (zu, grund) = abschluesse.Single();
            Pruefe.Gleich((kind.Nummer, Entscheidungsabschluss.Entschieden), (zu.Person!.Value, grund));
            Pruefe.Gleich(sohn.Nummer, Von(w, mensch, Entscheidungsart.Krankheit).Single().Person, "Gegenprobe: die Frage zum Sohn bleibt");
            for (var i = 0; i < 3; i++) sim.Tick();
            Pruefe.Wahr(Von(w, mensch, Entscheidungsart.Krankheit).All(e => e.Person == sohn.Nummer), "die Pest wird nicht wieder gefragt");
        }),

        ("Entscheidungen Teil 2: Erbe – altes Oberhaupt mit zwei Kindern: Wahl setzt das Testament; Gegenprobe: ein Kind oder junges Oberhaupt fragt nicht", () =>
        {
            var (sim, w, mensch, _) = Partie(alter: 78);
            sim.Tick();
            var e = Von(w, mensch, Entscheidungsart.Erbe).Single();
            Pruefe.Gleich(2, e.Auswahl!.Count);
            Pruefe.Gleich(3, w.Entscheidungen.Wahlen(e).Count);
            Pruefe.Gleich(Entscheidungswesen.ErbfolgeBelassen, w.Entscheidungen.Voreinstellung(e));
            Pruefe.Wahr(w.Entscheidungen.Folgen(e, 0).Contains(Kind(w, mensch).Vorname), w.Entscheidungen.Folgen(e, 0));
            Pruefe.Wahr(w.Ausfuehren(mensch, new EntscheidungTreffen(e.Nummer, 2)).Erfolg, "Testament");
            Pruefe.Gleich(e.Auswahl[1], w.Familien.Von(mensch)!.Testament!.Nummer);
            Pruefe.Gleich(0, Von(w, mensch, Entscheidungsart.Erbe).Count);
            sim.Tick();
            Pruefe.Gleich(0, Von(w, mensch, Entscheidungsart.Erbe).Count, "einmal je Anlass");

            var (sim2, w2, mensch2, _) = Partie(alter: 78, kinder: 1);
            sim2.Tick();
            Pruefe.Gleich(0, Von(w2, mensch2, Entscheidungsart.Erbe).Count, "ein Kind: nichts zu wählen");
            var (sim3, w3, mensch3, _) = Partie(alter: 40);
            sim3.Tick();
            Pruefe.Gleich(0, Von(w3, mensch3, Entscheidungsart.Erbe).Count, "junges Oberhaupt");
            Krank(w3, mensch3, w3.Familien.Von(mensch3)!.Oberhaupt, "Pest");
            sim3.Tick();
            var krank = Von(w3, mensch3, Entscheidungsart.Erbe).Single();
            Pruefe.Wahr(krank.Anlass!.Contains("krank:Pest"), krank.Anlass);
            BisFrist(sim3, krank);
            Pruefe.Gleich<Person?>(null, w3.Familien.Von(mensch3)!.Testament, "Voreinstellung: Erbfolge bleibt");
            Pruefe.Gleich(0, Von(w3, mensch3, Entscheidungsart.Erbe).Count, "nicht wieder gefragt");
        }),

        ("Entscheidungen Teil 2: Geheimnis entdeckt – Bloßstellen über die Frage, Voreinstellung Merken; direkte Nutzung schließt die Frage (Doppelweg)", () =>
        {
            var (sim, w, mensch, rabe) = Partie();
            var oberhaupt = w.Familien.Von(rabe)!.Oberhaupt;
            Pruefe.Wahr(w.Ausfuehren(mensch, new EntwicklerGeheimnis(oberhaupt.Kennung, Geheimnisart.Ehebruch)).Erfolg, "Geheimnis");
            w.Ausfuehren(mensch, new EntwicklerAktionspunkte());
            sim.Tick();
            var e = Von(w, mensch, Entscheidungsart.Geheimnis).Single();
            Pruefe.Gleich(rabe, e.Absender);
            Pruefe.Gleich(oberhaupt.Nummer, e.Person);
            Pruefe.Gleich(4, w.Entscheidungen.Wahlen(e).Count);
            Pruefe.Gleich(Entscheidungswesen.Merken, w.Entscheidungen.Voreinstellung(e));
            Pruefe.Wahr(w.Entscheidungen.Folgen(e, Entscheidungswesen.Blossstellen).Contains("nicht zurücknehmen"), w.Entscheidungen.Folgen(e, Entscheidungswesen.Blossstellen));
            Pruefe.Wahr(w.Entscheidungen.Folgen(e, Entscheidungswesen.Anzeigen).Contains("Gerichtsgebühr"), w.Entscheidungen.Folgen(e, Entscheidungswesen.Anzeigen));
            var g = w.Geheimnisse.Nummer(e.Geheimnis!.Value)!;
            Pruefe.Wahr(w.Ausfuehren(mensch, new EntscheidungTreffen(e.Nummer, Entscheidungswesen.Blossstellen)).Erfolg, "bloßgestellt");
            Pruefe.Wahr(g.Oeffentlich, "öffentlich");
            Pruefe.Gleich(0, Von(w, mensch, Entscheidungsart.Geheimnis).Count);

            // Gegenprobe Merken: Fristablauf tut nichts, das Geheimnis bleibt nutzbar und wird nicht wieder gefragt
            var (sim2, w2, mensch2, rabe2) = Partie();
            var o2 = w2.Familien.Von(rabe2)!.Oberhaupt;
            w2.Ausfuehren(mensch2, new EntwicklerGeheimnis(o2.Kennung, Geheimnisart.Schulden));
            sim2.Tick();
            var e2 = Von(w2, mensch2, Entscheidungsart.Geheimnis).Single();
            BisFrist(sim2, e2);
            sim2.Tick();
            Pruefe.Gleich(0, Von(w2, mensch2, Entscheidungsart.Geheimnis).Count, "gemerkt, nicht wieder gefragt");
            Pruefe.Wahr(w2.Geheimnisse.NutzbarFuer(mensch2).Any(x => x.Nummer == e2.Geheimnis), "weiter nutzbar");
            // Doppelweg: ein drittes Geheimnis direkt im Charakterfenster nutzen
            var (sim3, w3, mensch3, rabe3) = Partie();
            var o3 = w3.Familien.Von(rabe3)!.Oberhaupt;
            w3.Ausfuehren(mensch3, new EntwicklerGeheimnis(o3.Kennung, Geheimnisart.Schulden));
            w3.Ausfuehren(mensch3, new EntwicklerAktionspunkte());
            sim3.Tick();
            var e3 = Von(w3, mensch3, Entscheidungsart.Geheimnis).Single();
            var abschluesse = Abschluesse(w3);
            Pruefe.Wahr(w3.Ausfuehren(mensch3, new GeheimnisNutzen(e3.Geheimnis!.Value, Geheimnisnutzung.Blossstellen)).Erfolg, "direkt");
            Pruefe.Gleich(Entscheidungsabschluss.Entschieden, abschluesse.Single().A);
        }),

        ("Entscheidungen Teil 2: Mahnung des Kämmerers – nach dem Jahresabschluss im Minus eine Frage; Wegweiser schließen sie nur (gelesen); ohne Schulden keine", () =>
        {
            var (sim, w, mensch, rabe) = Partie();
            w.Ausfuehren(mensch, new EntwicklerGulden(-(mensch.Vermoegen + 5_000)));
            var jahr = sim.Zeit.Jahr;
            while (sim.Zeit.Jahr == jahr) sim.Tick();
            Pruefe.Wahr(mensch.Schuldenjahre > 0, "gemahnt");
            sim.Tick();
            var e = Von(w, mensch, Entscheidungsart.Mahnung).Single();
            Pruefe.Wahr(e.Betrag > 0, "Schulden");
            Pruefe.Gleich(0, Von(w, rabe, Entscheidungsart.Mahnung).Count, "Gegenprobe: Haus ohne Schulden");
            Pruefe.Gleich(Entscheidungswesen.Abwarten, w.Entscheidungen.Voreinstellung(e));
            var geld = mensch.Vermoegen;
            var r = w.Ausfuehren(mensch, new EntscheidungTreffen(e.Nummer, Entscheidungswesen.ZumKredit));
            Pruefe.Wahr(r.Erfolg && r.Meldung.Contains("Gelesen"), r.Meldung);
            Pruefe.Gleich(geld, mensch.Vermoegen, "ein Wegweiser ändert nichts");
            sim.Tick();
            Pruefe.Gleich(0, Von(w, mensch, Entscheidungsart.Mahnung).Count, "einmal je Mahnung");
        }),

        ("Entscheidungen Teil 2: Obergrenze – die siebte Frage verdrängt die älteste mit umkehrbarer Voreinstellung; Fragen mit Medicus als Voreinstellung verdrängt sie nie", () =>
        {
            var (sim, w, mensch, _) = Partie(kinder: 6);
            var abschluesse = Abschluesse(w);
            for (var i = 0; i < 6; i++) Krank(w, mensch, Kind(w, mensch, i), "Ruhr");
            sim.Tick();
            Pruefe.Gleich(6, Von(w, mensch, Entscheidungsart.Krankheit).Count);
            var rabe = w.Konten.First(k => k.Inhaber == "Haus Rabe");
            w.Ausfuehren(mensch, new EntwicklerGeheimnis(w.Familien.Von(rabe)!.Oberhaupt.Kennung, Geheimnisart.Schulden));
            sim.Tick();
            Pruefe.Gleich(0, abschluesse.Count, "Medicus ist nicht umkehrbar – nichts verdrängt");
            Pruefe.Gleich(0, Von(w, mensch, Entscheidungsart.Geheimnis).Count, "die neue Frage wartet");
            Pruefe.Wahr(w.Ausfuehren(mensch, new EntscheidungTreffen(Von(w, mensch, Entscheidungsart.Krankheit)[0].Nummer, Entscheidungswesen.NichtsTun)).Erfolg, "eine weniger");
            sim.Tick();
            Pruefe.Gleich(1, Von(w, mensch, Entscheidungsart.Geheimnis).Count, "jetzt offen");

            // Gegenprobe: ohne Geld und Schnaps ist „nichts tun“ die Voreinstellung – die älteste wird verdrängt
            var (sim2, w2, mensch2, rabe2) = Partie(kinder: 6);
            var abschluesse2 = Abschluesse(w2);
            w2.Ausfuehren(mensch2, new EntwicklerGulden(-(mensch2.Vermoegen - 10)));
            for (var i = 0; i < 6; i++) { Krank(w2, mensch2, Kind(w2, mensch2, i), "Ruhr"); sim2.Tick(); }
            var aelteste = w2.Entscheidungen.Offen.Where(e => e.Haus == mensch2).OrderBy(e => e.Nummer).First();
            w2.Ausfuehren(mensch2, new EntwicklerGeheimnis(w2.Familien.Von(rabe2)!.Oberhaupt.Kennung, Geheimnisart.Schulden));
            sim2.Tick();
            var (weg, grund) = abschluesse2.Single();
            Pruefe.Gleich((aelteste.Nummer, Entscheidungsabschluss.Obergrenze), (weg.Nummer, grund));
            Pruefe.Gleich(1, Von(w2, mensch2, Entscheidungsart.Geheimnis).Count);
            Pruefe.Gleich(6, w2.Entscheidungen.OffenFuer(mensch2).Count);
        }),

        ("Entscheidungen Teil 2: Spielstand – Auswahl, Geheimnis, Anlass und erledigte Anlässe überstehen Speichern und Laden; danach wird nicht neu gefragt", () =>
        {
            var (sim, w, mensch, rabe) = Partie(alter: 78);
            Krank(w, mensch, Kind(w, mensch), "Husten");
            w.Ausfuehren(mensch, new EntwicklerGeheimnis(w.Familien.Von(rabe)!.Oberhaupt.Kennung, Geheimnisart.Schulden));
            sim.Tick();
            Pruefe.Gleich(3, w.Entscheidungen.OffenFuer(mensch).Count);
            Pruefe.Wahr(w.Ausfuehren(mensch, new EntscheidungTreffen(Von(w, mensch, Entscheidungsart.Geheimnis).Single().Nummer, Entscheidungswesen.Merken)).Erfolg, "gemerkt");
            var json = Speicherung.Speichern(sim, mensch, Zeitpunkt);
            Pruefe.Wahr(JsonNode.Parse(json)!["Wirtschaft"]!["Entscheidungen"]!["Erledigt"]!.AsArray().Count == 1, "erledigt gespeichert");
            var (geladen, spieler) = Speicherung.Laden(json);
            Pruefe.Gleich(json, Speicherung.Speichern(geladen, spieler, Zeitpunkt));
            var w2 = geladen.Wirtschaft!;
            var erbe = w2.Entscheidungen.OffenFuer(spieler!).Single(e => e.Art == Entscheidungsart.Erbe);
            Pruefe.Gleich(2, erbe.Auswahl!.Count);
            geladen.Tick();
            Pruefe.Gleich(2, w2.Entscheidungen.OffenFuer(spieler!).Count, "das gemerkte Geheimnis kommt nicht wieder");
        }),

        ("Entscheidungen Teil 2: Daten – Frist je Art 120 Minuten, erbeAbSterberisiko 0,1, Datenversion 82; erbeAbSterberisiko 0 wird abgelehnt", () =>
        {
            var d = Daten.Value;
            Pruefe.Gleich(82, d.Version);
            foreach (var art in new[] { Entscheidungsart.Krankheit, Entscheidungsart.Erbe, Entscheidungsart.Geheimnis, Entscheidungsart.Mahnung })
                Pruefe.Gleich(120, d.Entscheidungen!.FristFuer(art), art.ToString());
            Pruefe.Ungefaehr(0.1, d.Entscheidungen!.ErbeAbSterberisiko, 1e-12);
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(File.ReadAllText(Datenpfad.Wirtschaft()).Replace("\"erbeAbSterberisiko\": 0.1", "\"erbeAbSterberisiko\": 0")),
                "erbeAbSterberisiko");
        }),
    };
}
