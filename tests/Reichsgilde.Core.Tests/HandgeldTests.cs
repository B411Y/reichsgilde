using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Handgeld beim Einstellen (Datenversion 73, Nutzer 08.11.2026, <c>personal.handgeld</c>): Wer einen Anfänger einstellt (Lehrling, Knecht,
/// Taschendieb, Rekrut), zahlt einmal das Handgeld – Spieler und KI gleich, über beide Einstellbefehle; die Startbelegung ist frei. Reicht das
/// Bargeld nicht, wird abgelehnt. Eigener Posten der Jahres- und der Betriebsrechnung, Teil des Gewinns. Die KI stellt nur ein, wenn der
/// erwartete Jahresertrag (nach dem Lohn) das Handgeld übersteigt.
/// </summary>
static class HandgeldTests
{
    const double Betrag = 60;

    static string Json(double handgeld) => Datenpfad.MitHandgeld(File.ReadAllText(Datenpfad.Wirtschaft()), handgeld);

    static (Simulation Sim, Konto Spieler) Partie(string json, double startvermoegen = 4000) =>
        Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos },
            Wirtschaftsdaten.AusJson(json), new NeuePartie(Beruf: "Schmied", Startvermoegen: startvermoegen), karte: Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Handgeld: Einen Lehrling einstellen kostet einmal das Handgeld – beide Befehle, eigener Posten in Jahres- und Betriebsrechnung, Teil des " +
         "Gewinns; der Startlehrling ist frei; Gegenprobe ohne Handgeld", () =>
        {
            var (sim, spieler) = Partie(Json(Betrag));
            var w = sim.Wirtschaft!;
            var b = w.Betriebe.Single(x => x.Konto == spieler);
            Pruefe.Gleich(Betrag, w.Personal.Handgeld("Lehrling"));
            Pruefe.Gleich(1, b.Belegt);
            Pruefe.Gleich(0.0, spieler.Laufend.Handgeld, "der Startlehrling kostet nichts");

            var vorher = (Bar: spieler.Vermoegen, Gewinn: spieler.Laufend.Gewinn, Betrieb: b.Laufend.Gewinn);
            var e = w.Ausfuehren(spieler, new ArbeiterEinstellen(b.Name, "Lehrling"));
            Pruefe.Wahr(e.Erfolg && e.Meldung.Contains("Handgeld 60"), e.Meldung);
            Pruefe.Gleich(2, b.Belegt);
            Pruefe.Ungefaehr(vorher.Bar - Betrag, spieler.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(Betrag, spieler.Laufend.Handgeld, 1e-9);
            Pruefe.Ungefaehr(Betrag, b.Laufend.Handgeld, 1e-9);
            Pruefe.Ungefaehr(vorher.Gewinn - Betrag, spieler.Laufend.Gewinn, 1e-9);
            Pruefe.Ungefaehr(vorher.Betrieb - Betrag, b.Laufend.Gewinn, 1e-9);
            Pruefe.Ungefaehr(0, spieler.Laufend.Personal, 1e-9); // der Lohn kommt erst zum Jahresende, das Handgeld steht nicht darin

            // Entlassen kostet nichts, wieder einstellen über PersonalSetzen (zwei auf einmal) zweimal das Handgeld
            Pruefe.Wahr(w.Ausfuehren(spieler, new PersonalSetzen(b.Name, "Lehrling", 0)).Erfolg, "entlassen");
            Pruefe.Ungefaehr(Betrag, spieler.Laufend.Handgeld, 1e-9);
            var e2 = w.Ausfuehren(spieler, new PersonalSetzen(b.Name, "Lehrling", 2));
            Pruefe.Wahr(e2.Erfolg && e2.Meldung.Contains("Handgeld 120"), e2.Meldung);
            Pruefe.Ungefaehr(3 * Betrag, spieler.Laufend.Handgeld, 1e-9);
            Pruefe.Ungefaehr(3 * Betrag, b.Laufend.Handgeld, 1e-9);

            // Zum Jahresende wird der Posten zum Vorjahr
            var jahr = sim.Zeit.Jahr;
            while (sim.Zeit.Jahr == jahr) sim.Tick();
            Pruefe.Ungefaehr(3 * Betrag, spieler.Vorjahr!.Handgeld, 1e-9);
            Pruefe.Gleich(0.0, spieler.Laufend.Handgeld);

            // Gegenprobe: ohne Handgeld kostet das Einstellen nichts
            var (sim0, spieler0) = Partie(Json(0));
            var w0 = sim0.Wirtschaft!;
            var b0 = w0.Betriebe.Single(x => x.Konto == spieler0);
            var bar0 = spieler0.Vermoegen;
            var e0 = w0.Ausfuehren(spieler0, new ArbeiterEinstellen(b0.Name, "Lehrling"));
            Pruefe.Wahr(e0.Erfolg && !e0.Meldung.Contains("Handgeld"), e0.Meldung);
            Pruefe.Gleich(bar0, spieler0.Vermoegen);
            Pruefe.Gleich(0.0, spieler0.Laufend.Handgeld);
        }),

        ("Handgeld: Reicht das Bargeld nicht, lehnen beide Befehle ab und nennen den Betrag; Ork und Leibeigener bekommen es ebenso (Nutzer 08.11.: " +
         "gleiche Regel für alle außer dem Startlehrling), Gesellen stellt man nicht ein; die Daten lehnen ein negatives Handgeld ab", () =>
        {
            var (sim, spieler) = Partie(Json(Betrag));
            var w = sim.Wirtschaft!;
            var b = w.Betriebe.Single(x => x.Konto == spieler);
            spieler.Entwicklerhilfe(-spieler.Vermoegen + Betrag - 1); // 59 Gulden in der Kasse
            Pruefe.Wahr(w.Personal.EinstellenVerboten(b, "Lehrling") is { } grund && grund.Contains("60"), w.Personal.EinstellenVerboten(b, "Lehrling") ?? "null");
            var e = w.Ausfuehren(spieler, new ArbeiterEinstellen(b.Name, "Lehrling"));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("Handgeld"), e.Meldung);
            var e2 = w.Ausfuehren(spieler, new PersonalSetzen(b.Name, "Lehrling", 2));
            Pruefe.Wahr(!e2.Erfolg && e2.Meldung.Contains("Handgeld"), e2.Meldung);
            Pruefe.Gleich(1, b.Belegt);
            Pruefe.Ungefaehr(Betrag - 1, spieler.Vermoegen, 1e-9);
            // mit genug für einen, aber nicht für zwei
            spieler.Entwicklerhilfe(2); // 61
            var e3 = w.Ausfuehren(spieler, new PersonalSetzen(b.Name, "Lehrling", 3));
            Pruefe.Wahr(!e3.Erfolg && e3.Meldung.Contains("120"), e3.Meldung);
            Pruefe.Wahr(w.Ausfuehren(spieler, new PersonalSetzen(b.Name, "Lehrling", 2)).Erfolg, "einer geht");
            Pruefe.Ungefaehr(1, spieler.Vermoegen, 1e-9);

            Pruefe.Gleich(0.0, w.Personal.Handgeld("Geselle"));
            foreach (var kraft in new[] { "Lehrling", "Knecht", "Taschendieb", "Rekrut", "Ork", "Leibeigener" }) Pruefe.Gleich(Betrag, w.Personal.Handgeld(kraft), kraft);
            Pruefe.Wahr(w.Personal.EinstellenVerboten(b, "Ork") is { } ork && ork.Contains("Handgeld"), "ohne Geld auch kein Ork");

            // Unter Statut C (Unfreie in allen Gewerben) kostet ein Ork bzw. Leibeigener in der Werkstatt das Handgeld wie ein Lehrling
            var simC = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, StartStatut = Fronstatut.FreieStadt + 2 },
                wirtschaftsdaten: Wirtschaftsdaten.AusJson(Json(Betrag)));
            var wC = simC.Wirtschaft!;
            var kC = wC.KontoEroeffnen("Haus Fron", startvermoegen: 1000);
            var gC = wC.BetriebEroeffnen("Gießerei Fron", Betriebsart.Giesserei, kC, startkarren: "");
            foreach (var kraft in new[] { "Ork", "Leibeigener" })
            {
                var vorher = kC.Vermoegen;
                var eC = wC.Ausfuehren(kC, new PersonalSetzen(gC.Name, kraft, 1));
                Pruefe.Wahr(eC.Erfolg && eC.Meldung.Contains($"Handgeld {Zahlenformat.Ganz(Betrag)}"), $"{kraft}: {eC.Meldung}");
                Pruefe.Ungefaehr(vorher - Betrag, kC.Vermoegen, 1e-9);
            }
            Pruefe.Ungefaehr(2 * Betrag, gC.Laufend.Handgeld, 1e-9);
            Pruefe.Gleich(0.0, Wirtschaftsdaten.AusJson(File.ReadAllText(Datenpfad.Wirtschaft()).Replace("\"handgeld\":", "\"handgeldAlt\":")).Personal!.Handgeld, "ohne Angabe 0");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json(-1)), "personal.handgeld");
        }),

        ("Handgeld: Spielstand – der Posten bleibt nach Speichern und Laden (Jahres- und Betriebsrechnung); ein alter Spielstand ohne das Feld lädt mit 0", () =>
        {
            var (sim, spieler) = Partie(Json(Betrag));
            var w = sim.Wirtschaft!;
            var b = w.Betriebe.Single(x => x.Konto == spieler);
            Pruefe.Wahr(w.Ausfuehren(spieler, new ArbeiterEinstellen(b.Name, "Lehrling")).Erfolg, "eingestellt");
            var text = Speicherung.Speichern(sim, spieler, new DateTime(2026, 1, 1));
            var (geladen, s2) = Speicherung.Laden(text);
            Pruefe.Ungefaehr(Betrag, s2!.Laufend.Handgeld, 1e-9);
            Pruefe.Ungefaehr(Betrag, geladen.Wirtschaft!.Betriebe.Single(x => x.Konto == s2).Laufend.Handgeld, 1e-9);
            Pruefe.Ungefaehr(spieler.Laufend.Gewinn, s2.Laufend.Gewinn, 1e-9);

            var alt = JsonNode.Parse(text)!;
            Entfernen(alt, "Handgeld");
            var (altSim, s3) = Speicherung.Laden(alt.ToJsonString());
            Pruefe.Gleich(0.0, s3!.Laufend.Handgeld);
            Pruefe.Gleich(0.0, altSim.Wirtschaft!.Betriebe.Single(x => x.Konto == s3).Laufend.Handgeld);

            static void Entfernen(JsonNode? n, string feld)
            {
                if (n is JsonObject o) { o.Remove(feld); foreach (var (_, v) in o.ToList()) Entfernen(v, feld); }
                else if (n is JsonArray a) foreach (var e in a) Entfernen(e, feld);
            }
        }),

        ("Handgeld: Die KI zahlt es wie der Spieler, wenn sie beim Anmelden den freien Platz füllt, und stellt nicht ein, wenn das Handgeld den " +
         "erwarteten Ertrag der Bleibedauer übersteigt (personal.handgeldJahre, auch mit Geld genug); mit drei Jahren lohnt es, ohne Handgeld ohnehin", () =>
        {
            var (sim, spieler) = Partie(Json(Betrag));
            var w = sim.Wirtschaft!;
            Pruefe.Gleich(3.0, w.Personal.HandgeldJahre, "Vorgabe: etwa die Lehrzeit");
            var kiBetriebe = w.Betriebe.Where(x => x.Konto != spieler).ToList();
            Pruefe.Wahr(kiBetriebe.Count > 0, "KI-Häuser");
            foreach (var b in kiBetriebe)
            {
                var neu = b.Belegt - w.Personal.StartAnzahl(b.Art, 2);
                Pruefe.Wahr(neu > 0, $"{b.Name}: die KI stellt ein");
                Pruefe.Ungefaehr(neu * Betrag, b.Konto.Laufend.Handgeld, 1e-9);
                Pruefe.Ungefaehr(neu * Betrag, b.Laufend.Handgeld, 1e-9);
            }

            // Ein Handgeld über dem Jahresertrag eines Lehrlings (1.000 Gulden, die Kasse reicht dafür), aber unter dem Ertrag von drei Jahren:
            // Soll es sich in einem Jahr bezahlt machen (handgeldJahre 1), stellt die KI niemanden ein, mit der Vorgabe 3 schon.
            const double teuer = 1000;
            int Eingestellt(double jahre, bool pruefen)
            {
                var knoten = JsonNode.Parse(Json(teuer))!;
                knoten["personal"]!["handgeldJahre"] = jahre;
                var (simT, spielerT) = Partie(knoten.ToJsonString());
                var wT = simT.Wirtschaft!;
                var summe = 0;
                foreach (var b in wT.Betriebe.Where(x => x.Konto != spielerT))
                {
                    var ertrag = wT.Rivalen.Single(r => r.Konto == b.Konto).Jahresertrag(b, "Lehrling");
                    if (pruefen)
                        Pruefe.Wahr(ertrag is > 0 and < teuer && ertrag * 3 > teuer && wT.Personal.EinstellenVerboten(b, "Lehrling") is null,
                            $"{b.Name}: Ertrag {ertrag:0}, Kasse {b.Konto.Vermoegen:0}");
                    summe += b.Belegt - wT.Personal.StartAnzahl(b.Art, 2);
                }
                return summe;
            }
            Pruefe.Gleich(0, Eingestellt(1, pruefen: true), "ein Jahr: niemand eingestellt");
            Pruefe.Wahr(Eingestellt(3, pruefen: false) > 0, "drei Jahre: eingestellt");
            // Gegenprobe ohne Handgeld: dieselben Häuser stellen ein
            var (sim0, spieler0) = Partie(Json(0));
            foreach (var b in sim0.Wirtschaft!.Betriebe.Where(x => x.Konto != spieler0))
                Pruefe.Wahr(b.Belegt > w.Personal.StartAnzahl(b.Art, 2), $"{b.Name}: ohne Handgeld eingestellt");
            // Die Daten lehnen eine Bleibedauer von 0 ab
            var null0 = JsonNode.Parse(Json(Betrag))!;
            null0["personal"]!["handgeldJahre"] = 0;
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(null0.ToJsonString()), "personal.handgeldJahre");
        }),

        ("Handgeld: Betriebe ohne Ware stellen weiter ein – das Wachhaus Rekruten, das Schmugglerloch Taschendiebe; die KI zahlt das Handgeld", () =>
        {
            foreach (var (art, kraft) in new[] { (Betriebsart.Wachhaus, "Rekrut"), (Betriebsart.Schmugglerloch, "Taschendieb") })
            {
                var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 },
                    wirtschaftsdaten: Wirtschaftsdaten.AusJson(Json(Betrag)));
                var w = sim.Wirtschaft!;
                var k = w.KontoEroeffnen("Haus Ohneware", startvermoegen: 10_000);
                var b = w.BetriebEroeffnen($"{w.Daten.Art(art).Name} Ohneware", art, k, startkarren: "");
                Pruefe.Wahr(b.Artdaten.OhneProduktion && b.Belegt == 0, $"{b.Name}: ohne Ware, leer");
                var ki = w.KiAnmelden(k);
                Pruefe.Wahr(b.Belegt > 0 && b.Arbeiter.All(a => a.Arbeitskraft == kraft),
                    $"{b.Name}: {b.Belegt} eingestellt (Jahresertrag eines {kraft}: {ki.Jahresertrag(b, kraft):0})");
                Pruefe.Ungefaehr(b.Belegt * Betrag, b.Laufend.Handgeld, 1e-9);
            }
        }),
    };
}
