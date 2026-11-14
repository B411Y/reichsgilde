using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Entwickler;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Versehrungen (Datenversion 88): bleibende Verletzungen als erworbene Züge mit dem Feld „versehrung“ – Narbe, Entstellt, Einäugig,
/// Lahm, Verkrüppelte Hand. Sie entstehen nur aus Anlässen des Spiels (Einbruch, Brand auch durch Blitz, Rauferei beim Zechen,
/// Knochenbruch ohne Medicus, Kerker, Lösegeld), bleiben und werden nicht vererbt.
/// </summary>
static class VersehrtTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json.Value));

    /// <summary>Die echten Daten, Texte ersetzt (jeder muss genau einmal vorkommen).</summary>
    static Wirtschaftsdaten Mit(params (string Alt, string Neu)[] ersetzen)
    {
        var json = Json.Value;
        foreach (var (alt, neu) in ersetzen)
        {
            Pruefe.Wahr(json.Split(alt).Length == 2, $"„{alt}“ genau einmal in wirtschaft.json");
            json = json.Replace(alt, neu);
        }
        return Wirtschaftsdaten.AusJson(json);
    }

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null, int startwert = 1400) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, wirtschaftsdaten: daten ?? Daten.Value);

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr)
        {
            sim.Tick();
            while (sim.Ratssitzung is not null) sim.RatssitzungWeiter();
        }
    }

    static (Konto Konto, Familie Familie) Haus(Stadtwirtschaft w, string name, params string[] zuege)
    {
        var konto = w.KontoEroeffnen(name, startvermoegen: 50_000);
        var f = w.Familien.Gruenden(konto, Volk.Mensch, Geschlecht.Mann, alter: 30);
        konto.Aktionspunkte.Setzen(12);
        f.Oberhaupt.ZuegeLeeren();
        foreach (var k in zuege) f.Oberhaupt.ZugHinzufuegen(w.Familien.Zug(k)!);
        return (konto, f);
    }

    static readonly string[] Versehrungen = { "Narbe", "Entstellt", "Einaeugig", "Lahm", "Verkrueppelt" };

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Versehrungen: Daten – fünf Versehrungen, alle erworben, ohne Gegenteil und Vererbung, mit Kennung fürs Bild; Lahm mag die Jagd nicht", () =>
        {
            var d = Daten.Value.Charakterzuege!;
            Pruefe.Gleich(88, Daten.Value.Version);
            var bild = new Dictionary<string, string>
                { ["Narbe"] = "narbe", ["Entstellt"] = "brandmal", ["Einaeugig"] = "augenklappe", ["Lahm"] = "hinken", ["Verkrueppelt"] = "hand" };
            Pruefe.Gleich(string.Join(",", Versehrungen), string.Join(",", d.Alle.Where(z => z.IstVersehrung).Select(z => z.Kennung)));
            foreach (var k in Versehrungen)
            {
                var z = d.Zug(k)!;
                Pruefe.Gleich(Zugart.Erworben, z.Art);
                Pruefe.Gleich(bild[k], z.Versehrung);
                Pruefe.Wahr(z.Gegenteil is null && z.Daten.Chance == 0 && z.Daten.Vererbung == 0 && !z.Daten.Anlaesse.ContainsKey("jahr") || k == "Narbe",
                    $"{k}: nur aus Anlässen, nicht vererbt");
                Pruefe.Wahr(z.Daten.Talente.Values.All(t => t is >= -1 and <= 1), $"{k}: höchstens ±1 Stern");
            }
            Pruefe.Gleich("Schoen", d.Zug("Entstellt")!.Daten.Verdraengt.Single());
            Pruefe.Gleich(-10.0, d.Zug("Entstellt")!.Daten.Meinung.Allgemein);
            Pruefe.Gleich(0.25, d.Zug("Lahm")!.Daten.Anlaesse["ueberstanden:Knochenbruch"]);
            Pruefe.Wahr(Daten.Value.Meinungen!.Aktivitaeten["Jagd"].MagNicht.Contains("Lahm"), "Lahm mag die Jagd nicht");
            Pruefe.Wahr(!d.Alle.Any(z => z.Kennung == "Einarmig"), "Einarmig erst mit einem Kampf-Ereignis");
        }),

        ("Versehrungen: Daten werden geprüft – unbekannte Aktivität, Anlass ohne Wert, verdraengt bei Angeborenen, unbekannter Zug in verdraengt", () =>
        {
            Pruefe.WirftMitMeldung(() => Mit(("\"unglueck:Zechen\": 0.05", "\"unglueck:Tanzen\": 0.05")), "Aktivität „Tanzen“");
            Pruefe.WirftMitMeldung(() => Mit(("\"unglueck:Zechen\": 0.05", "\"unglueck\": 0.05")), "Anlass „unglueck“");
            Pruefe.WirftMitMeldung(() => Mit(("\"kerker\": 0.05", "\"kerker:Turm\": 0.05")), "Anlass „kerker:Turm“");
            Pruefe.WirftMitMeldung(() => Mit(("\"verdraengt\": [ \"Schoen\" ]", "\"verdraengt\": [ \"Bucklig\" ]")), "Zug „Bucklig“");
            Pruefe.WirftMitMeldung(() => Mit(("\"talente\": { \"Rhetorik\": 1 }, \"chance\": 0.04,", "\"talente\": { \"Rhetorik\": 1 }, \"verdraengt\": [ \"Klug\" ], \"chance\": 0.04,")),
                "nur für erworbene Züge");
        }),

        ("Versehrungen: Entstellt nimmt „Schön“ – Gegenprobe: eine Narbe lässt es", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var buch = w.Familien;
            var (k, f) = Haus(w, "Haus Probe", "Schoen");
            var o = f.Oberhaupt;
            buch.EntwicklerZugGeben(o, buch.Zug("Narbe")!);
            Pruefe.Wahr(o.HatZug("Schoen") && o.HatZug("Narbe"), "Narbe allein lässt die Schönheit");
            buch.EntwicklerZugGeben(o, buch.Zug("Entstellt")!);
            Pruefe.Wahr(!o.HatZug("Schoen") && o.HatZug("Entstellt"), "Entstellt nimmt sie");
            Pruefe.Gleich("Narbe,Entstellt", string.Join(",", buch.Versehrungen(o).Select(z => z.Kennung)));
            Pruefe.Gleich("narbe,brandmal", string.Join(",", buch.Versehrungen(o).Select(z => z.Versehrung)));
            Pruefe.Gleich(0, buch.Versehrungen(Haus(w, "Haus Heil").Familie.Oberhaupt).Count);
        }),

        ("Versehrungen: Knochenbruch ohne Medicus macht etwa jeden Vierten lahm, mit Medicus niemanden; Stark halbiert", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var ohne = new List<Person>();
            var stark = new List<Person>();
            var mit = new List<Person>();
            for (var i = 0; i < 1200; i++)
            {
                var gruppe = (i % 3) switch { 0 => ohne, 1 => stark, _ => mit };
                var (k, f) = Haus(w, $"Haus Bruch {i}", gruppe == stark ? new[] { "Stark" } : Array.Empty<string>());
                Pruefe.Wahr(w.Gesundheit.Erkranken(k, f.Oberhaupt, "Knochenbruch", stress: false, melden: false), "Knochenbruch");
                if (gruppe == mit) Pruefe.Wahr(w.Gesundheit.MedicusRufen(k, f.Oberhaupt.Nummer).Erfolg, "Medicus");
                gruppe.Add(f.Oberhaupt);
            }
            w.Gesundheit.Jahresende();
            Pruefe.Wahr(ohne.All(p => p.Krankheit is null), "der Bruch ist verheilt");
            var lahm = ohne.Count(p => p.HatZug("Lahm"));
            var lahmStark = stark.Count(p => p.HatZug("Lahm"));
            Pruefe.Wahr(lahm is > 70 and < 130, $"{lahm} von 400 lahm (Chance 25 %)");
            Pruefe.Wahr(lahmStark is > 25 and < 75, $"{lahmStark} von 400 Starken lahm (Chance 12,5 %)");
            Pruefe.Gleich(0, mit.Count(p => p.HatZug("Lahm")));
        }),

        ("Versehrungen: Rauferei beim Zechen kann ein Auge kosten – Gegenprobe ohne Unglück", () =>
        {
            foreach (var (gefahr, erwartet) in new[] { ("1", true), ("0", false) })
            {
                var w = NeueSimulation(Mit(("\"unglueck:Zechen\": 0.05", "\"unglueck:Zechen\": 1"),
                    ("\"gefahr\": 0.15, \"unglueck\": \"Rausch und Rauferei\"", $"\"gefahr\": {gefahr}, \"unglueck\": \"Rausch und Rauferei\""))).Wirtschaft!;
                var raufereien = 0;
                for (var i = 0; i < 5; i++)
                {
                    var (k, f) = Haus(w, $"Haus Zecher {i}");
                    var e = w.Ausfuehren(k, new Aktivitaet("Zechen", w.Rat.Ratsherren[i].Kennung));
                    Pruefe.Wahr(e.Erfolg, e.Meldung);
                    var rauferei = e.Meldung.Contains("Rauferei");
                    if (rauferei) raufereien++;
                    Pruefe.Gleich(rauferei, f.Oberhaupt.HatZug("Einaeugig")); // ein Auge nur nach der Rauferei
                }
                Pruefe.Gleich(erwartet, raufereien > 0);
            }
        }),

        ("Versehrungen: Lösegeld bezahlt – die Geisel kommt vielleicht mit verkrüppelter Hand heim; Gegenprobe Chance 0", () =>
        {
            foreach (var (chance, erwartet) in new[] { ("1", true), ("0", false) })
            {
                var w = NeueSimulation(Mit(("\"entfuehrung\": 0.1", $"\"entfuehrung\": {chance}"))).Wirtschaft!;
                var dieb = w.KontoEroeffnen("Familie Elster", startvermoegen: 1000);
                var (opfer, f) = Haus(w, "Haus Opfer");
                var tochter = w.Familien.KindAufnehmen(opfer, 20);
                tochter.ZuegeLeeren();
                w.Diebe.EntwicklerBrief(Diebesbriefart.Loesegeld, dieb, opfer, 300, tochter.Nummer);
                Pruefe.Wahr(w.Ausfuehren(opfer, new DiebesforderungBeantworten(dieb.Inhaber, Diebesbriefart.Loesegeld, true)).Erfolg, "Lösegeld");
                Pruefe.Gleich(erwartet, tochter.HatZug("Verkrueppelt"));
                Pruefe.Wahr(!f.Oberhaupt.HatZug("Verkrueppelt"), "nur die Geisel");
            }
        }),

        ("Versehrungen: Wer aus dem Kerker kommt (Gerichtstag oder Kaution), wird vielleicht lahm – Gegenprobe Chance 0", () =>
        {
            foreach (var (chance, erwartet) in new[] { ("1", true), ("0", false) })
            {
                var sim = NeueSimulation(Mit(("\"kerker\": 0.05", $"\"kerker\": {chance}")));
                var w = sim.Wirtschaft!;
                var (a, fa) = Haus(w, "Haus Absitzen");
                var (b, fb) = Haus(w, "Haus Kaution");
                var konten = w.Konten.ToList();
                var stand = w.Justiz.Sichern(k => konten.IndexOf(k));
                stand.KerkerBis[konten.IndexOf(a).ToString()] = w.Jahr;
                stand.KerkerBis[konten.IndexOf(b).ToString()] = w.Jahr + 3;
                w.Justiz.Wiederherstellen(stand, konten);
                Pruefe.Wahr(w.Justiz.ImKerker(a) && w.Justiz.ImKerker(b), "beide im Kerker");
                w.Justiz.Freilassen(b);
                Pruefe.Gleich(erwartet, fb.Oberhaupt.HatZug("Lahm"));
                Pruefe.Wahr(!fa.Oberhaupt.HatZug("Lahm"), "noch drin");
                JahrBeenden(sim);
                Pruefe.Wahr(!w.Justiz.ImKerker(a), "Zeit abgesessen");
                Pruefe.Gleich(erwartet, fa.Oberhaupt.HatZug("Lahm"));
            }
        }),

        ("Versehrungen: Blitzbrand am Wohnhaus ist ein Brand (Narbe, Entstellt) – Gegenprobe Chance 0", () =>
        {
            foreach (var (chance, erwartet) in new[] { ("1", true), ("0", false) })
            {
                var daten = Mit(("\"brand\": 0.1, \"blitz\": 0.1", $"\"brand\": 0.1, \"blitz\": {chance}"), ("\"chance\": 0.25,\n    \"dauerMinuten\"", "\"chance\": 0,\n    \"dauerMinuten\""),
                    ("\"chanceJeStunde\": 0.15", "\"chanceJeStunde\": 0"));
                var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos },
                    daten, new NeuePartie(), karte: Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));
                sim.EntwicklerErlaubt = true;
                while (sim.Zeit.VergangeneMinuten < 300) sim.Tick();
                var o = sim.Wirtschaft!.Familien.Von(spieler)!.Oberhaupt;
                o.ZuegeLeeren();
                o.ZugHinzufuegen(sim.Wirtschaft.Familien.Zug("Schoen")!);
                Pruefe.Wahr(sim.Bote.SofortAusfuehren(spieler, new EntwicklerBlitz("haus")).Erfolg, "Blitz");
                Pruefe.Gleich(erwartet, o.HatZug("Entstellt"));
                Pruefe.Gleich(!erwartet, o.HatZug("Schoen"));
            }
        }),

        ("Versehrungen: bleiben über Jahre, werden nicht vererbt und stehen nach Speichern und Laden wieder da", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 21 },
                Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            var buch = w.Familien;
            var o = buch.Von(spieler)!.Oberhaupt;
            foreach (var k in new[] { "Entstellt", "Einaeugig", "Lahm", "Verkrueppelt" }) buch.EntwicklerZugGeben(o, buch.Zug(k)!);
            var kind = buch.KindAufnehmen(spieler, 0);
            Pruefe.Wahr(!kind.Zuege.Any(z => z.IstVersehrung), "nicht vererbt");
            for (var i = 0; i < 3; i++) JahrBeenden(sim);
            Pruefe.Gleich(4, buch.Versehrungen(o).Count(z => z.Kennung != "Narbe"));
            var zeitpunkt = new DateTime(2026, 11, 14, 12, 0, 0);
            var json = Speicherung.Speichern(sim, spieler, zeitpunkt);
            var (geladen, geladenerSpieler) = Speicherung.Laden(json);
            Pruefe.Gleich(json, Speicherung.Speichern(geladen, geladenerSpieler, zeitpunkt));
            var og = geladen.Wirtschaft!.Familien.Von(geladenerSpieler)!.Oberhaupt;
            Pruefe.Gleich(string.Join(",", buch.Versehrungen(o).Select(z => z.Kennung)),
                string.Join(",", geladen.Wirtschaft.Familien.Versehrungen(og).Select(z => z.Kennung)));
        }),
    };
}
