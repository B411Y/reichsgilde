using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Tests: Der strenge Winter hängt an der Partie-Einstellung „Härte der Zeit“ („Härte um 1400“ Teil 2, Datenversion 94; Nutzer 17.11.2026) –
/// Chance 15 / 20 / 30 % und Nachfragezuschlag 0,25 / 0,5 / 0,75 für Mild / Wie überliefert / Hart. Die Würfe bleiben dieselben: Ein Winter,
/// der bei einer milderen Stufe streng ist, ist es auch bei jeder härteren. Daten ohne Stufen rechnen wie bis Datenversion 93.
/// </summary>
static class WinterHaerteTests
{
    static readonly HaerteDerZeit[] Stufen = { HaerteDerZeit.Mild, HaerteDerZeit.Ueberliefert, HaerteDerZeit.Hart };

    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json.Value));

    /// <summary>Die Daten wie bis Datenversion 93: nur eine Chance (0,3) und ein Zuschlag (0,5), keine Stufen.</summary>
    static readonly Lazy<Wirtschaftsdaten> OhneStufen = new(() => Wirtschaftsdaten.AusJson(OhneStufenJson()));

    static string OhneStufenJson()
    {
        var wurzel = System.Text.Json.Nodes.JsonNode.Parse(Json.Value)!.AsObject();
        var winter = wurzel["jahreszeiten"]!["strengerWinter"]!.AsObject();
        winter.Remove("mild");
        winter.Remove("ueberliefert");
        winter.Remove("hart");
        winter["chance"] = 0.3;
        winter["nachfrageZuschlag"] = 0.5;
        wurzel["version"] = 93;
        return wurzel.ToJsonString();
    }

    static Stadtwirtschaft Wirtschaft(int startwert, HaerteDerZeit haerte, Wirtschaftsdaten? daten = null) =>
        new(daten ?? Daten.Value, Partieeinstellungen.Standard.StartStatut, startwert) { HaerteDerZeit = haerte };

    /// <summary>Die ersten n Winter (1403, 1407, …) je Startwert.</summary>
    static List<Winterlage> Winter(HaerteDerZeit haerte, int startwerte, int winter = 50, Wirtschaftsdaten? daten = null) =>
        Enumerable.Range(1, startwerte).SelectMany(s =>
        {
            var w = Wirtschaft(s * 7919, haerte, daten);
            return Enumerable.Range(0, winter).Select(i => w.Jahreszeiten.WinterVon(1403 + 4 * i)!);
        }).ToList();

    static double Anteil(List<Winterlage> winter) => winter.Count(l => l.Streng) / (double)winter.Count;

    /// <summary>Der erste Startwert, dessen Winter 1403 bei dieser Härte streng ist.</summary>
    static int StrengerStartwert(HaerteDerZeit haerte)
    {
        for (var s = 1; s < 5000; s++)
            if (Wirtschaft(s, haerte).Jahreszeiten.WinterVon(1403) is { Streng: true }) return s;
        throw new InvalidOperationException("Kein Startwert mit strengem Winter 1403 gefunden.");
    }

    /// <summary>Die Minute seit Spielbeginn: Jahr ab 0 (= 1400), Uhrzeit.</summary>
    static long Minute(int jahr, int stunde) => jahr * (long)Spielzeit.MinutenProTag + stunde * 60 - Spielzeit.TagesbeginnMinute;

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Winterhärte: Die Daten nennen je Stufe Chance und Nachfragezuschlag (Datenversion 94)", () =>
        {
            var s = Daten.Value.Jahreszeiten!.StrengerWinter!;
            Pruefe.Gleich(98, Daten.Value.Version);
            Pruefe.Gleich(0.15, s.Von(HaerteDerZeit.Mild).Chance);
            Pruefe.Gleich(0.2, s.Von(HaerteDerZeit.Ueberliefert).Chance);
            Pruefe.Gleich(0.3, s.Von(HaerteDerZeit.Hart).Chance);
            Pruefe.Gleich(0.25, s.Von(HaerteDerZeit.Mild).NachfrageZuschlag);
            Pruefe.Gleich(0.5, s.Von(HaerteDerZeit.Ueberliefert).NachfrageZuschlag);
            Pruefe.Gleich(0.75, s.Von(HaerteDerZeit.Hart).NachfrageZuschlag);
        }),

        ("Winterhärte: Über viele Partien ist etwa jeder 7., 5. bzw. 3. Winter streng (15 / 20 / 30 %)", () =>
        {
            // 400 Startwerte × 50 Winter = 20 000 Winter je Stufe; die Streuung liegt bei rund 0,3 Prozentpunkten.
            Pruefe.Wahr(Math.Abs(Anteil(Winter(HaerteDerZeit.Mild, 400)) - 0.15) < 0.015, "Mild nahe 15 %");
            Pruefe.Wahr(Math.Abs(Anteil(Winter(HaerteDerZeit.Ueberliefert, 400)) - 0.2) < 0.015, "Wie überliefert nahe 20 %");
            Pruefe.Wahr(Math.Abs(Anteil(Winter(HaerteDerZeit.Hart, 400)) - 0.3) < 0.015, "Hart nahe 30 %");
        }),

        ("Winterhärte: Die Kälte ist bei jeder Stufe dieselbe – was milder schon streng ist, ist es härter erst recht", () =>
        {
            var mild = Winter(HaerteDerZeit.Mild, 40);
            var mittel = Winter(HaerteDerZeit.Ueberliefert, 40);
            var hart = Winter(HaerteDerZeit.Hart, 40);
            for (var i = 0; i < mild.Count; i++)
            {
                Pruefe.Gleich(mild[i].Kaelte, mittel[i].Kaelte);
                Pruefe.Gleich(mild[i].Kaelte, hart[i].Kaelte);
                Pruefe.Wahr(!mild[i].Streng || mittel[i].Streng, "streng bei Mild heißt streng bei Wie überliefert");
                Pruefe.Wahr(!mittel[i].Streng || hart[i].Streng, "streng bei Wie überliefert heißt streng bei Hart");
                Pruefe.Gleich(mittel[i].Streng, mittel[i].Kaelte >= 1 - 0.2);
            }
            Pruefe.Wahr(hart.Count(l => l.Streng) > mittel.Count(l => l.Streng) && mittel.Count(l => l.Streng) > mild.Count(l => l.Streng), "härter heißt öfter");
        }),

        ("Winterhärte: Der Fluss bleibt bei jeder Stufe 6 bis 14 Stunden zu und friert zwischen 7 und 10 Uhr zu", () =>
        {
            var s = Daten.Value.Jahreszeiten!.StrengerWinter!;
            foreach (var h in Stufen)
            {
                var strenge = Winter(h, 100).Where(l => l.Streng).ToList();
                Pruefe.Wahr(strenge.Count > 100, "genug strenge Winter");
                foreach (var l in strenge)
                {
                    var stunde = (l.ZufrierenMinute + Spielzeit.TagesbeginnMinute) / 60;
                    Pruefe.Wahr(stunde >= s.ZufrierenVon && stunde <= s.ZufrierenBis, "Stunde des Zufrierens");
                    var dauer = l.TauwetterMinute - l.ZufrierenMinute;
                    Pruefe.Wahr(dauer >= Math.Min(s.DauerMin * 60, Spielzeit.MinutenProTag - l.ZufrierenMinute) && dauer <= s.DauerMax * 60, "Dauer");
                }
                // Die Dauer wächst mit der Kälte innerhalb der Stufe: Kürzeste und längste kommen beide vor.
                Pruefe.Wahr(strenge.Any(l => l.TauwetterMinute - l.ZufrierenMinute == s.DauerMin * 60), $"{h}: kürzeste Dauer kommt vor");
                Pruefe.Wahr(strenge.Any(l => l.TauwetterMinute - l.ZufrierenMinute >= (s.DauerMax - 1) * 60 || l.BisJahresende), $"{h}: lange Dauer kommt vor");
            }
        }),

        ("Winterhärte: Im strengen Winter wächst die Nachfrage nach Holz auf 1,5 / 1,6 / 1,7 – im gewöhnlichen Winter bleibt sie 1,4", () =>
        {
            var soll = new Dictionary<HaerteDerZeit, double> { [HaerteDerZeit.Mild] = 1.5, [HaerteDerZeit.Ueberliefert] = 1.6, [HaerteDerZeit.Hart] = 1.7 };
            foreach (var h in Stufen)
            {
                var j = Wirtschaft(1, h).Jahreszeiten;
                Pruefe.Wahr(Math.Abs(j.NachfrageFaktor("Holz", Jahreszeit.Winter, true, null) - soll[h]) < 1e-9, $"{h}: Holz im strengen Winter");
                Pruefe.Wahr(Math.Abs(j.NachfrageFaktor("Holz", Jahreszeit.Winter, false, null) - 1.4) < 1e-9, $"{h}: Holz im gewöhnlichen Winter");
                Pruefe.Gleich(1.0, j.NachfrageFaktor("Holz", Jahreszeit.Sommer, true, null));
            }
        }),

        ("Winterhärte: Die Einstellung der Partie erreicht den Winter und den Markt", () =>
        {
            foreach (var h in Stufen)
            {
                var startwert = StrengerStartwert(h);
                var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert, HaerteDerZeit = h },
                    new Spielzeit(Minute(3, 5)), Daten.Value);
                var w = sim.Wirtschaft!;
                Pruefe.Gleich(h, w.HaerteDerZeit);
                Pruefe.Wahr(w.Jahreszeiten.IstStrengerWinter, $"{h}: strenger Winter 1403");
                var zuschlag = Daten.Value.Jahreszeiten!.StrengerWinter!.Von(h).NachfrageZuschlag;
                Pruefe.Wahr(Math.Abs(w.Jahreszeiten.Nachfragefaktor("Holz") - (1 + 0.4 * (1 + zuschlag))) < 1e-9, $"{h}: Nachfragefaktor Holz");
            }
        }),

        ("Winterhärte: Die mittlere Stufe sinkt von 30 auf 20 % – ein Winter mit Kälte zwischen 0,7 und 0,8 ist nur noch bei Hart streng", () =>
        {
            for (var s = 1; s < 5000; s++)
            {
                if (Wirtschaft(s, HaerteDerZeit.Hart).Jahreszeiten.WinterVon(1403) is not { Kaelte: >= 0.7 and < 0.8 } hart) continue;
                Pruefe.Wahr(hart.Streng, "bei Hart streng");
                Pruefe.Wahr(!Wirtschaft(s, HaerteDerZeit.Ueberliefert).Jahreszeiten.WinterVon(1403)!.Streng, "bei Wie überliefert nicht mehr streng");
                Pruefe.Wahr(Wirtschaft(s, HaerteDerZeit.Ueberliefert, OhneStufen.Value).Jahreszeiten.WinterVon(1403)!.Streng, "mit den Daten bis Version 93 streng");
                return;
            }
            throw new InvalidOperationException("Kein Startwert mit Kälte zwischen 0,7 und 0,8 gefunden.");
        }),

        ("Winterhärte: Daten ohne Stufen rechnen bei jeder Härte wie bis Datenversion 93 (30 %, Zuschlag 0,5)", () =>
        {
            var alt = OhneStufen.Value.Jahreszeiten!.StrengerWinter!;
            Pruefe.Wahr(alt.Mild is null && alt.Ueberliefert is null && alt.Hart is null, "keine Stufen");
            var soll = Winter(HaerteDerZeit.Hart, 40); // Hart mit Stufen = 30 % wie die alten Daten
            foreach (var h in Stufen)
            {
                Pruefe.Gleich(0.3, alt.Von(h).Chance);
                Pruefe.Gleich(0.5, alt.Von(h).NachfrageZuschlag);
                var ist = Winter(h, 40, daten: OhneStufen.Value);
                for (var i = 0; i < ist.Count; i++) Pruefe.Gleich(soll[i], ist[i]);
                Pruefe.Wahr(Math.Abs(Wirtschaft(1, h, OhneStufen.Value).Jahreszeiten.NachfrageFaktor("Holz", Jahreszeit.Winter, true, null) - 1.6) < 1e-9, "Holz 1,6");
            }
        }),

        ("Winterhärte: Ein Spielstand mit den Daten der Version 93 lädt ohne Stufen – bei jeder Härte weiter 30 % und Zuschlag 0,5, dieselben Winter", () =>
        {
            foreach (var h in Stufen)
            {
                var (sim, spieler) = Partieaufbau.Erstellen(
                    Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, HaerteDerZeit = h }, OhneStufen.Value, new NeuePartie());
                var json = Speicherung.Speichern(sim, spieler, new DateTime(2026, 11, 17, 12, 0, 0));
                var (geladen, _) = Speicherung.Laden(json);
                var w = geladen.Wirtschaft!;
                Pruefe.Gleich(93, w.Daten.Version);
                Pruefe.Gleich(h, w.HaerteDerZeit);
                var winter = w.Daten.Jahreszeiten!.StrengerWinter!;
                Pruefe.Wahr(winter.Mild is null && winter.Ueberliefert is null && winter.Hart is null, "keine Stufen nach dem Laden");
                Pruefe.Gleich(0.3, winter.Von(h).Chance);
                Pruefe.Gleich(0.5, winter.Von(h).NachfrageZuschlag);
                var streng = 0;
                for (var jahr = 1403; jahr < 1403 + 4 * 200; jahr += 4)
                {
                    var l = w.Jahreszeiten.WinterVon(jahr)!;
                    Pruefe.Gleich(sim.Wirtschaft!.Jahreszeiten.WinterVon(jahr), l);
                    Pruefe.Gleich(l.Kaelte >= 1 - 0.3, l.Streng);
                    if (l.Streng) streng++;
                }
                Pruefe.Wahr(streng is > 40 and < 80, $"{h}: etwa 30 % strenge Winter ({streng} von 200)");
                Pruefe.Wahr(Math.Abs(w.Jahreszeiten.NachfrageFaktor("Holz", Jahreszeit.Winter, true, null) - 1.6) < 1e-9, $"{h}: Holz 1,6");
            }
        }),

        ("Winterhärte: Fehlerhafte Stufen werden abgewiesen", () =>
        {
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"hart\": { \"chance\": 0.3, \"nachfrageZuschlag\": 0.75 }", "\"hart\": { \"chance\": 1.3, \"nachfrageZuschlag\": 0.75 }")),
                "jahreszeiten.strengerWinter.hart.chance");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"mild\": { \"chance\": 0.15, \"nachfrageZuschlag\": 0.25 }", "\"mild\": { \"chance\": 0.15, \"nachfrageZuschlag\": 2.5 }")),
                "jahreszeiten.strengerWinter.mild.nachfrageZuschlag");
        }),
    };
}
