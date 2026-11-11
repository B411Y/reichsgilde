using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Entscheidungen;
using Reichsgilde.Core.Mehrspieler;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für die dauerhafte Hauschronik (Datenversion 83): nur das Wichtigste, für immer, im Spielstand und in der Prüfsumme.</summary>
static class HauschronikTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    /// <summary>Ein Haus mit Familie; das Oberhaupt heiratet (ein Eintrag „Heirat“ in der Chronik).</summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto Konto, Familie F) Verheiratet(int startwert = 4711)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Zufallsstartwert = startwert }, wirtschaftsdaten: Daten.Value);
        var w = sim.Wirtschaft!;
        var konto = w.KontoEroeffnen("Haus Chronik", startvermoegen: 100_000);
        var f = w.Familien.Gruenden(konto, Volk.Mensch, Geschlecht.Mann, alter: 30);
        w.Familien.Vermaehlen(konto, w.Familien.Kandidaten.First(k => k.Person.Geschlecht == Geschlecht.Frau && k.Person.Volk == Volk.Mensch), 0);
        return (sim, w, konto, f);
    }

    static string Bild(Familie f) => string.Join("\n", f.Hauschronik.Select(e => $"{e.Minute} {e.Art} {e.Person} {e.Vertraulich} {e.Text}"));

    /// <summary>Den Spielstand ohne Hauschronik schreiben – so sah er vor Datenversion 83 aus.</summary>
    static string OhneChronik(string json)
    {
        var n = JsonNode.Parse(json)!;
        var fb = n["Wirtschaft"]!["Familien"]!.AsObject();
        fb.Remove("Hauschronik");
        foreach (var f in fb["Familien"]!.AsArray()) f!.AsObject().Remove("Chronik");
        return n.ToJsonString();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Hauschronik: Die Heirat bleibt nach 600 weiteren Meldungen – der Verlauf hat sie vergessen, die Chronik nicht", () =>
        {
            var (_, w, konto, f) = Verheiratet();
            Pruefe.Gleich(1, f.Hauschronik.Count(e => e.Art == Chronikart.Heirat));
            Pruefe.Wahr(w.Meldungsverlauf.Any(m => m.Art == Meldungsart.Heirat), "die Heirat steht zunächst im Verlauf");
            for (var i = 0; i < 600; i++) w.Melden(Meldungsart.Sonstiges, $"Füllmeldung {i}", haus: konto);
            // Gegenprobe: Der Verlauf hält nur die letzten 500 – die Heirat ist dort fort.
            Pruefe.Wahr(!w.Meldungsverlauf.Any(m => m.Art == Meldungsart.Heirat), "der Verlauf hält die Heirat noch");
            Pruefe.Wahr(w.Familien.Chronik(f, konto).Any(e => e.Art == Chronikart.Heirat && e.Text.Contains(" heiratet ")), "Heirat in der Chronik");
        }),

        ("Hauschronik: Unwichtige Meldungen kommen nicht hinein, ein vereitelter Brand auch nicht – ein echter Brand schon (Gegenprobe)", () =>
        {
            var (_, w, konto, f) = Verheiratet();
            var vorher = Bild(f);
            foreach (var art in new[] { Meldungsart.Sonstiges, Meldungsart.LagerLeer, Meldungsart.LagerVoll, Meldungsart.KasseKnapp, Meldungsart.Ratssitzung,
                         Meldungsart.Diebstahl, Meldungsart.Krankheit, Meldungsart.Schulden, Meldungsart.Meisterpruefung, Meldungsart.Anklage })
                w.Melden(art, $"Unwichtig: {art}", haus: konto);
            w.Melden(Meldungsart.Feuer, "Ein Brandanschlag wurde vereitelt.", haus: konto, wohnhaus: true, wichtigkeit: Meldungswichtigkeit.Normal);
            w.Melden(Meldungsart.Entscheidung, "Eine Entscheidung ohne Folgen.", haus: konto, vertraulich: true);
            Pruefe.Gleich(vorher, Bild(f), "unwichtige Meldungen in der Chronik");
            w.Melden(Meldungsart.Feuer, "Das Wohnhaus brennt!", haus: konto, wohnhaus: true);
            Pruefe.Wahr(f.Hauschronik.Count(e => e.Art == Chronikart.Brand) == 1, "der echte Brand fehlt");
            Pruefe.Wahr(f.Hauschronik.Single(e => e.Art == Chronikart.Brand).Text == "Feuer im Haus Chronik.", Bild(f));
            // Eine Meldung für ein fremdes Haus trifft diese Chronik nicht.
            var fremd = w.KontoEroeffnen("Haus Fremd", startvermoegen: 1000);
            w.Familien.Gruenden(fremd, Volk.Zwerg, Geschlecht.Frau, alter: 30);
            w.Melden(Meldungsart.Feuer, "Beim Nachbarn brennt es!", haus: fremd, wohnhaus: true);
            Pruefe.Gleich(1, f.Hauschronik.Count(e => e.Art == Chronikart.Brand));
            Pruefe.Gleich(1, w.Familien.Von(fremd)!.Hauschronik.Count(e => e.Art == Chronikart.Brand));
        }),

        ("Hauschronik: Vertrauliches sieht nur das Haus selbst (Gegenprobe mit fremdem Konto); Ämter, Stand und Entscheidungen mit Folgen", () =>
        {
            var (_, w, konto, f) = Verheiratet();
            var fremd = w.KontoEroeffnen("Haus Fremd", startvermoegen: 1000);
            w.Familien.ChronikEintragen(konto, Chronikart.Entscheidung, "GEHEIM-ENTSCHEIDUNG", vertraulich: true);
            Pruefe.Wahr(w.Familien.Chronik(f, konto).Any(e => e.Text == "GEHEIM-ENTSCHEIDUNG"), "das Haus selbst sieht sie");
            Pruefe.Wahr(!w.Familien.Chronik(f, fremd).Any(e => e.Text == "GEHEIM-ENTSCHEIDUNG"), "ein fremdes Haus nicht");
            // Ein Amt geht beim Tod des Oberhaupts verloren (Rat.Neuanfang) – das steht in der Chronik, mit Grund.
            var amt = w.Rat.Aemter.First();
            amt.Konto = konto;
            w.Familien.AmtErhalten(konto, amt.Name);
            w.Rat.Neuanfang(konto);
            Pruefe.Wahr(f.Hauschronik.Any(e => e.Art == Chronikart.Amt && e.Text == $"{f.Oberhaupt.Name} wird {amt.Name}."), Bild(f));
            Pruefe.Wahr(f.Hauschronik.Any(e => e.Art == Chronikart.Amt && e.Text == $"Das Haus Chronik verliert das Amt {amt.Name} (Tod des Oberhaupts)."), Bild(f));
            // Ohne Amt verliert es keins (Gegenprobe).
            var anzahl = f.Hauschronik.Count;
            w.Rat.Neuanfang(konto);
            Pruefe.Gleich(anzahl, f.Hauschronik.Count);
            // Entscheidungen: nur mit Folgen.
            var frage = new Entscheidung(1, Entscheidungsart.Erpresserbrief, konto, fremd, null, 100, 0, 10, Entscheidungswesen.Ablehnen);
            Pruefe.Wahr(Entscheidungswesen.HatFolgen(frage, Entscheidungsabschluss.Entschieden, Entscheidungswesen.Zahlen), "gezahlt");
            Pruefe.Wahr(Entscheidungswesen.HatFolgen(frage, Entscheidungsabschluss.Frist, null), "geschwiegen");
            Pruefe.Wahr(!Entscheidungswesen.HatFolgen(frage, Entscheidungsabschluss.Hinfaellig, null), "hinfällig");
            var erbe = frage with { Art = Entscheidungsart.Erbe };
            Pruefe.Wahr(!Entscheidungswesen.HatFolgen(erbe, Entscheidungsabschluss.Entschieden, Entscheidungswesen.ErbfolgeBelassen), "Erbfolge belassen");
            Pruefe.Wahr(Entscheidungswesen.HatFolgen(erbe, Entscheidungsabschluss.Entschieden, 1), "Testament geändert");
            var geheimnis = frage with { Art = Entscheidungsart.Geheimnis };
            Pruefe.Wahr(!Entscheidungswesen.HatFolgen(geheimnis, Entscheidungsabschluss.Frist, Entscheidungswesen.Merken), "gemerkt");
            Pruefe.Wahr(Entscheidungswesen.HatFolgen(geheimnis, Entscheidungsabschluss.Entschieden, Entscheidungswesen.Blossstellen), "bloßgestellt");
            foreach (var art in new[] { Entscheidungsart.Krankheit, Entscheidungsart.Mahnung })
                Pruefe.Wahr(!Entscheidungswesen.HatFolgen(frage with { Art = art }, Entscheidungsabschluss.Entschieden, 1), $"{art}");
        }),

        ("Hauschronik: Obergrenze je Haus – darüber fällt der älteste Eintrag der niedrigsten Wichtigkeit, Wichtiges bleibt", () =>
        {
            var (_, w, konto, f) = Verheiratet();
            var grenze = w.Familien.ChronikObergrenze;
            Pruefe.Gleich(400, grenze);
            for (var i = 0; i < grenze; i++)
                w.Familien.ChronikEintragen(konto, i % 2 == 0 ? Chronikart.Brand : Chronikart.Amt, $"Eintrag {i}");
            Pruefe.Gleich(grenze, f.Hauschronik.Count);
            // Die Heirat (wichtig) bleibt, der älteste Brand (unwichtig) ist weg, die Grenze hält.
            Pruefe.Wahr(f.Hauschronik.Any(e => e.Art == Chronikart.Heirat), "Heirat verdrängt");
            Pruefe.Wahr(!f.Hauschronik.Any(e => e.Text == "Eintrag 0"), "ältester Brand noch da");
            Pruefe.Wahr(f.Hauschronik.Any(e => e.Text == "Eintrag 2"), "zweitältester Brand schon weg");
            for (var i = 0; i < 300; i++) w.Familien.ChronikEintragen(konto, Chronikart.Amt, $"Amt {i}");
            Pruefe.Gleich(grenze, f.Hauschronik.Count);
            Pruefe.Gleich(0, f.Hauschronik.Count(e => e.Art == Chronikart.Brand)); // erst alle Brände, dann …
            Pruefe.Wahr(f.Hauschronik.Any(e => e.Text == "Amt 299") && !f.Hauschronik.Any(e => e.Text == "Eintrag 1"), "… die ältesten Ämter");
            // Lange Sätze werden gekürzt.
            w.Familien.ChronikEintragen(konto, Chronikart.Amt, new string('x', 500));
            Pruefe.Gleich(Familienbuch.ChronikTextLaenge, f.Hauschronik[^1].Text.Length);
            Pruefe.Wahr(f.Hauschronik[^1].Text.EndsWith('…'), "ohne Auslassungszeichen");
        }),

        ("Hauschronik: Speichern und Laden behält jeden Eintrag, die Prüfsumme sieht die Chronik (Gegenprobe: ein Eintrag mehr)", () =>
        {
            var (sim, w, konto, f) = Verheiratet();
            w.Familien.ChronikEintragen(konto, Chronikart.Entscheidung, "Ihr habt gezahlt.", f.Oberhaupt, vertraulich: true);
            var json = Speicherung.Speichern(sim, konto);
            var (kopie, _) = Speicherung.Laden(json);
            var fk = kopie.Wirtschaft!.Familien.Von(kopie.Wirtschaft.Konten[w.Konten.ToList().IndexOf(konto)])!;
            Pruefe.Gleich(Bild(f), Bild(fk));
            Pruefe.Gleich(Zustandspruefsumme.Berechnen(sim), Zustandspruefsumme.Berechnen(kopie));
            kopie.Wirtschaft.Familien.ChronikEintragen(fk.Konto, Chronikart.Amt, "Nur in der Kopie.");
            Pruefe.Wahr(Zustandspruefsumme.Berechnen(sim) != Zustandspruefsumme.Berechnen(kopie), "die Prüfsumme sieht die Chronik nicht");
        }),

        ("Hauschronik: Ein alter Spielstand ergänzt sie aus dem Verlauf; ein neuer mit leerer Chronik nicht (Gegenprobe); die alte Greybox lädt", () =>
        {
            var (sim, w, konto, f) = Verheiratet();
            w.Melden(Meldungsart.StandAufstieg, "Haus Chronik ist nun Bürger! (Gebühr 500 Gulden)", haus: konto);
            w.Melden(Meldungsart.LagerLeer, "Das Lager ist leer.", haus: konto);
            var json = Speicherung.Speichern(sim, konto);
            // Alt: ohne „Hauschronik“ und „Chronik“ – Heirat und Stand kommen aus dem Verlauf (nur der erste Satz), das Lager nicht.
            var (alt, _) = Speicherung.Laden(OhneChronik(json));
            var fa = alt.Wirtschaft!.Familien.Von(alt.Wirtschaft.Konten[w.Konten.ToList().IndexOf(konto)])!;
            Pruefe.Gleich(1, fa.Hauschronik.Count(e => e.Art == Chronikart.Heirat));
            Pruefe.Wahr(fa.Hauschronik.Any(e => e.Art == Chronikart.Amt && e.Text == "Haus Chronik ist nun Bürger!"), Bild(fa));
            Pruefe.Gleich(2, fa.Hauschronik.Count);
            // Neu, aber mit leerer Chronik: nichts aus dem Verlauf nachtragen.
            f.ChronikListe.Clear();
            var (neu, _) = Speicherung.Laden(Speicherung.Speichern(sim, konto));
            Pruefe.Gleich(0, neu.Wirtschaft!.Familien.Von(neu.Wirtschaft.Konten[w.Konten.ToList().IndexOf(konto)])!.Hauschronik.Count);
            // Der echte alte Spielstand (Greybox, Herbst 1406, Datenversion 55) lädt und speichert die Chronik danach mit.
            var datei = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
                "tests", "Reichsgilde.Core.Tests", "Spielstaende", "format2-greybox-herbst1406.json.gz");
            using var gz = new System.IO.Compression.GZipStream(File.OpenRead(datei), System.IO.Compression.CompressionMode.Decompress);
            var (greybox, _) = Speicherung.Laden(new StreamReader(gz).ReadToEnd());
            var gw = greybox.Wirtschaft!;
            Pruefe.Wahr(gw.Konten.Select(gw.Familien.Von).OfType<Familie>().Any(), "keine Familien im alten Spielstand");
            var wieder = JsonNode.Parse(Speicherung.Speichern(greybox))!;
            Pruefe.Gleich(1, (int)wieder["Wirtschaft"]!["Familien"]!["Hauschronik"]!);
        }),
    };
}
