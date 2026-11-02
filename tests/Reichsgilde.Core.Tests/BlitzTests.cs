using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Entwickler;
using Reichsgilde.Core.Gefahren;
using Reichsgilde.Core.Mehrspieler;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Blitzschlag im Gewitter (Wunsch des Nutzers vom 2.11.2026, Datenversion 69): der Wetterplan im Kern (würfelt wie vorher die Darstellung),
/// Einschlag mitten im Gewitter, Ziele nach Höhe und Anhöhe, Brand 1–3 Stunden, Schaden als Platzhalter (Werkstatt steht still, Wohnhaus
/// ohne Ansehen), Nachrechnen nach dem Laden ohne Spielstandfeld, Entwicklerbefehl.
/// </summary>
static class BlitzTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly DateTime Zeitpunkt = new(2026, 11, 2, 12, 0, 0);

    /// <summary>Die echten Daten mit einer anderen Chance je Gewitter (1 = jedes Gewitter schlägt ein).</summary>
    static Wirtschaftsdaten MitChance(double chance)
    {
        var json = Json.Value;
        const string alt = "\"blitz\": {\n    \"chance\": 0.25,";
        Pruefe.Wahr(json.Contains(alt), "blitz.chance 0,25 in wirtschaft.json");
        return Wirtschaftsdaten.AusJson(json.Replace(alt, $"\"blitz\": {{\n    \"chance\": {chance.ToString(System.Globalization.CultureInfo.InvariantCulture)},"));
    }

    static (Simulation Sim, Konto Spieler) Partie(Wirtschaftsdaten daten, int startwert = 1400, string karte = "greybox")
    {
        var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos },
            daten, new NeuePartie(), karte: Stadtkarte.AusDatei(Datenpfad.Karte(karte)));
        return (sim, spieler);
    }

    /// <summary>Das erste Gewitter der Partie (Spielminute seit Beginn, wann es über der Stadt steht).</summary>
    static long ErstesGewitter(Stadtwirtschaft w)
    {
        for (var jahr = 0; jahr < 40; jahr++)
            if (w.Blitz.Gewitterminuten(jahr).FirstOrDefault(m => m > 0) is var m and > 0)
                return m;
        throw new Exception("Kein Gewitter in 40 Jahren");
    }

    static void BisMinute(Simulation sim, long minute)
    {
        while (sim.Zeit.VergangeneMinuten < minute)
        {
            sim.Tick();
            while (sim.Ratssitzung is not null) sim.RatssitzungWeiter();
        }
    }

    static string Gross(string t) => t.Length == 0 ? t : char.ToUpperInvariant(t[0]) + t[1..];

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Blitz: Wetterplan im Kern würfelt wie der alte Wetterablauf der Darstellung (Startwert 1400, Jahre 0–3)", () =>
        {
            // Erwartete Abschnitte, gerechnet mit dem Wetterablauf aus game/scripts/Wetter.cs bis 2.11.2026 (new Random, Next(150, 331))
            var erwartet = new[]
            {
                "0Klar 267Sturm 522Regen 823Gewitter 1022Regen",
                "0Klar 166Klar 360Wolkig 561Klar 743Wolkig 964Wolkig",
                "0Wolkig 245Regen 559Nebel 840Gewitter 1004Klar",
                "0Wolkig 324Schnee 578Wolkig 758Sturm 1086Schnee",
            };
            var plan = new Wetterplan(1400);
            for (var j = 0; j < erwartet.Length; j++)
                Pruefe.Gleich(erwartet[j], string.Join(" ", plan.Abschnitte(j).Select(a => $"{a.Beginn}{a.Lage}")), $"Jahr {j}");
            var a3 = plan.Abschnitte(0)[3];
            Pruefe.Gleich((823, 1022, 922), (a3.Beginn, a3.Ende, a3.Mitte));
            Pruefe.Gleich(Spielzeit.MinutenProTag, plan.Abschnitte(0)[^1].Ende);
        }),

        ("Blitz: Daten – Chance 0,25 je Gewitter, Brand 60–180 Minuten, Stillstand mindestens so lang wie das längste Feuer, Kirchen hoch, Markt 0", () =>
        {
            var d = Wirtschaftsdaten.AusJson(Json.Value).Blitz!;
            Pruefe.Gleich(0.25, d.Chance);
            Pruefe.Gleich((60, 180), (d.DauerMinuten[0], d.DauerMinuten[1]));
            Pruefe.Wahr(d.StillstandMinuten >= d.DauerMinuten[1], "Stillstand ≥ längstes Feuer");
            Pruefe.Wahr(d.Hoehen["kirche"] > 3 * d.Hoehen["einwohnerplatz_ring"] && d.Hoehen["dom"] > d.Hoehen["kirche"], "Kirchen und Dom hoch");
            Pruefe.Gleich(0.0, d.Hoehen["markt"]);
            var f = new List<string>();
            new BlitzDaten { DauerMinuten = new[] { 60, 400 } }.Pruefen(f);
            Pruefe.Wahr(f.Any(x => x.Contains("stillstandMinuten")), "Stillstand kürzer als das Feuer wird abgelehnt");
        }),

        ("Blitz: Gewicht – ein Kirchturm (40 m) zieht den Blitz gut 16-mal so oft an wie ein Bürgerhaus (10 m), ein Haus 10 m über dem Mittel 4-mal", () =>
        {
            Blitzziel Ziel(double hoehe, double boden) => new("x", "x", 0, 0, hoehe, boden, null, null);
            Pruefe.Ungefaehr(16, Blitzwesen.Gewicht(Ziel(40, 0), 0) / Blitzwesen.Gewicht(Ziel(10, 0), 0), 1e-9);
            Pruefe.Ungefaehr(4, Blitzwesen.Gewicht(Ziel(10, 15), 5) / Blitzwesen.Gewicht(Ziel(10, 5), 5), 1e-9);
            Pruefe.Gleich(1.0, Blitzwesen.Gewicht(Ziel(2, -20), 0)); // tief im Tal: mindestens 1
            var ziele = new[] { Ziel(40, 0) with { Kennung = "kirche" }, Ziel(10, 0) with { Kennung = "haus" } };
            var kirche = Enumerable.Range(0, 1000).Count(i => Blitzwesen.Waehlen(ziele, (i + 0.5) / 1000)!.Kennung == "kirche");
            Pruefe.Wahr(kirche is > 930 and < 960, $"Kirche {kirche} von 1000 (erwartet 1600/1700 ≈ 941)");
        }),

        ("Blitz: Ziele in der Greybox – Werkstätten mit Betrieb, Wohnhäuser der Häuser, Dom, Rathaus, Einwohnerhäuser; kein Markt, kein Fels, kein Feld", () =>
        {
            var (sim, spieler) = Partie(MitChance(0.25));
            var w = sim.Wirtschaft!;
            var ziele = w.Blitz.Ziele();
            Pruefe.Wahr(ziele.Any(z => z.Kennung == "Dom") && ziele.Any(z => z.Name == "Rathaus"), "Dom und Rathaus");
            Pruefe.Wahr(ziele.Any(z => z.Betrieb?.Konto == spieler) && ziele.Any(z => z.Bewohner == spieler), "Werkstatt und Wohnhaus des Spielers");
            Pruefe.Wahr(ziele.Count(z => z.Name == "ein Bürgerhaus") >= 30, "Einwohner- und Kulissenhäuser");
            var karte = w.Karte!;
            foreach (var z in ziele)
                if (karte.GebaeudeMit(z.Kennung) is { } g)
                    Pruefe.Wahr(g.Katalog is not ("markt" or "felsberg" or "weide" or "feld" or "sammelwald" or "hafenplatz"), $"{g.Kennung} ({g.Katalog}) ist kein Ziel");
            Pruefe.Gleich(ziele.Count, ziele.Select(z => z.Kennung).Distinct().Count(), "jede Kennung einmal");
        }),

        ("Blitz: Mit Chance 1 schlägt es mitten im ersten Gewitter ein – Meldung, Brand 60–180 Minuten, Schaden am Gebäude; danach „aus“", () =>
        {
            var (sim, _) = Partie(MitChance(1));
            var w = sim.Wirtschaft!;
            var meldungen = new List<string>();
            w.Meldung += m => meldungen.Add(m);
            var gewitter = ErstesGewitter(w);
            BisMinute(sim, gewitter);
            Pruefe.Gleich(0, w.Blitz.Einschlaege.Count, "vor der Mitte des Gewitters");
            BisMinute(sim, gewitter + 1);
            var e = w.Blitz.Einschlaege.Single();
            Pruefe.Gleich(gewitter, e.Minute);
            Pruefe.Wahr(e.BrandEnde - e.Minute is >= 60 and <= 180, $"Dauer {e.BrandEnde - e.Minute}");
            Pruefe.Wahr(meldungen.Any(m => m.StartsWith("Blitzschlag! ") && m.Contains(" brennt.")), "Meldung Blitzschlag");
            Pruefe.Wahr(w.Blitz.Brand(e.Ziel.Kennung) == e, "brennt");
            // Startwert 1400: Das erste Gewitter trifft eine Werkstatt (der Schaden muss geprüft werden, nicht nur „wenn“)
            Pruefe.Wahr(e.Ziel.Betrieb is { } b && b.SabotageMinuten == w.Daten.Blitz!.StillstandMinuten, $"Werkstatt repariert ({e.Ziel.Kennung})");
            if (e.Ziel.Bewohner is { } k) Pruefe.Wahr(k.WohnhausInstandBis == e.BrandEnde + w.Daten.Blitz!.InstandMinuten, "Wohnhaus instand bis");
            BisMinute(sim, e.BrandEnde + 1);
            Pruefe.Wahr(w.Blitz.Brand(e.Ziel.Kennung) is null, "gelöscht");
            Pruefe.Wahr(meldungen.Any(m => m.Contains(" brennt nicht mehr – es bleiben Ruß und Schaden")), "Meldung aus");
        }),

        ("Blitz: Chance 0 – zwanzig Jahre Greybox ohne Einschlag; ohne Abschnitt „blitz“ ist das Blitzwesen aus", () =>
        {
            var (sim, _) = Partie(MitChance(0));
            var w = sim.Wirtschaft!;
            var zaehler = 0;
            w.Meldung += m => { if (m.StartsWith("Blitzschlag!")) zaehler++; };
            Pruefe.Wahr(Enumerable.Range(0, 20).Sum(j => w.Blitz.Gewitterminuten(j).Count()) >= 4, "Gewitter gibt es");
            BisMinute(sim, 20L * Spielzeit.MinutenProTag);
            Pruefe.Gleich(0, zaehler);
            var ohne = Wirtschaftsdaten.AusJson(Json.Value.Replace("\"blitz\": {", "\"blitzAus\": {"));
            Pruefe.Wahr(ohne.Blitz is null, "ohne Abschnitt");
            Pruefe.Wahr(!Partie(ohne).Sim.Wirtschaft!.Blitz.IstAktiv, "aus");
        }),

        ("Blitz: Die Werkstatt arbeitet nicht, solange es brennt (Entwicklerbefehl auf die eigene Werkstatt um 9 Uhr), danach repariert sie", () =>
        {
            var (sim, spieler) = Partie(MitChance(0));
            BisMinute(sim, 300); // 9 Uhr
            sim.EntwicklerErlaubt = true;
            var w = sim.Wirtschaft!;
            var b = w.Betriebe.First(x => x.Konto == spieler && x.IstWerkstatt && x.Zutaten.Count > 0);
            var e = sim.Bote.SofortAusfuehren(spieler, new EntwicklerBlitz("werkstatt"));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            var brand = w.Blitz.Einschlaege.Single();
            Pruefe.Wahr(brand.Ziel.Betrieb == b, "die eigene Werkstatt");
            Pruefe.Gleich(w.Daten.Blitz!.StillstandMinuten, b.SabotageMinuten);
            for (var t = brand.Minute; t < brand.BrandEnde; t++)
            {
                BisMinute(sim, t + 1);
                Pruefe.Wahr(b.Stillstand is not null, $"Stillstand um Minute {t}");
            }
            Pruefe.Wahr(b.SabotageMinuten > 0, "nach dem Brand wird noch repariert");
            Pruefe.Wahr(!sim.Bote.SofortAusfuehren(spieler, new EntwicklerBlitz("Gibtsnicht")).Erfolg, "unbekanntes Gebäude");
            sim.EntwicklerErlaubt = false;
            Pruefe.Wahr(!sim.Bote.SofortAusfuehren(spieler, new EntwicklerBlitz()).Erfolg, "gesperrt");
        }),

        ("Blitz: Wohnhaus – bis zur Instandsetzung (Ende des Feuers + 1140 Minuten) fehlt das Ansehen des Hauses", () =>
        {
            var (sim, spieler) = Partie(MitChance(0));
            BisMinute(sim, 300);
            sim.EntwicklerErlaubt = true;
            var w = sim.Wirtschaft!;
            Pruefe.Wahr(sim.Bote.SofortAusfuehren(spieler, new EntwicklerBlitz("haus")).Erfolg, "Blitz ins Wohnhaus");
            var e = w.Blitz.Einschlaege.Single();
            Pruefe.Wahr(e.Ziel.Bewohner == spieler, "eigenes Wohnhaus");
            Pruefe.Gleich(e.BrandEnde + w.Daten.Blitz!.InstandMinuten, spieler.WohnhausInstandBis);
            Pruefe.Wahr(w.Wohnen.IstBeschaedigt(spieler) && w.Wohnen.AnsehenBonus(spieler) == 0, "kein Ansehen des Hauses");
            Pruefe.Wahr(!sim.Bote.SofortAusfuehren(spieler, new EntwicklerBlitz("haus")).Erfolg, "brennt schon");
        }),

        ("Blitz: Nach Speichern und Laden mitten im Brand rechnet der Kern das Feuer nach – gleicher Einschlag, gleiche Prüfsumme danach", () =>
        {
            // Startwert 1400: Das erste Gewitter trifft eine Werkstatt – der Schaden darf nach dem Laden nicht noch einmal wirken
            var (sim, spieler) = Partie(MitChance(1), startwert: 1400);
            var w = sim.Wirtschaft!;
            var gewitter = ErstesGewitter(w);
            BisMinute(sim, gewitter + 30);
            var e = w.Blitz.Einschlaege.Single();
            Pruefe.Wahr(e.Ziel.Betrieb is not null, $"eine Werkstatt ({e.Ziel.Kennung})");
            var reparatur = e.Ziel.Betrieb!.SabotageMinuten;
            var json = Speicherung.Speichern(sim, spieler, Zeitpunkt);
            var (geladen, _) = Speicherung.Laden(json);
            var g = geladen.Wirtschaft!.Blitz.Einschlaege.Single();
            Pruefe.Gleich((e.Minute, e.BrandEnde, e.Ziel.Kennung), (g.Minute, g.BrandEnde, g.Ziel.Kennung));
            Pruefe.Wahr(geladen.Wirtschaft.Blitz.Brand(e.Ziel.Kennung) is not null, "brennt nach dem Laden weiter");
            BisMinute(geladen, gewitter + 31);
            BisMinute(sim, gewitter + 31);
            Pruefe.Gleich(e.Ziel.Betrieb.SabotageMinuten, geladen.Wirtschaft.Betriebe.Single(b => b.Name == e.Ziel.Betrieb.Name).SabotageMinuten, "Reparatur nicht doppelt");
            Pruefe.Wahr(e.Ziel.Betrieb.SabotageMinuten <= reparatur, "Reparatur nur einmal");
            BisMinute(sim, e.BrandEnde + 1);
            BisMinute(geladen, e.BrandEnde + 1);
            Pruefe.Wahr(geladen.Wirtschaft.Blitz.Einschlaege.Count <= 1 && geladen.Wirtschaft.Blitz.Brand(e.Ziel.Kennung) is null, "nicht doppelt, gelöscht");
            BisMinute(sim, gewitter + 600);
            BisMinute(geladen, gewitter + 600);
            Pruefe.Gleich(Zustandspruefsumme.Berechnen(sim), Zustandspruefsumme.Berechnen(geladen), "Prüfsumme");
        }),

        ("Blitz: Ein Feuer vom späten Abend brennt über den Jahreswechsel – mit Meldung „brennt nicht mehr“, und nach dem Laden um 4 Uhr brennt es weiter", () =>
        {
            var (sim, spieler) = Partie(MitChance(1));
            var w = sim.Wirtschaft!;
            var meldungen = new List<string>();
            w.Meldung += m => meldungen.Add(m);
            Blitzeinschlag? spaet = null;
            for (var jahr = 0; jahr < 40 && spaet is null; jahr++)
                foreach (var m in w.Blitz.Gewitterminuten(jahr))
                {
                    BisMinute(sim, m + 1);
                    if (w.Blitz.Einschlaege.LastOrDefault() is { } e && e.Minute == m && e.BrandEnde > (jahr + 1L) * Spielzeit.MinutenProTag) { spaet = e; break; }
                }
            Pruefe.Wahr(spaet is not null, "kein spätes Feuer in 40 Jahren");
            var neujahr = (spaet!.Minute / Spielzeit.MinutenProTag + 1) * Spielzeit.MinutenProTag;
            var ab = meldungen.Count; // ältere Feuer (auch in „ein Bürgerhaus“) zählen nicht
            BisMinute(sim, neujahr + 1);
            Pruefe.Wahr(w.Blitz.Brand(spaet.Ziel.Kennung) is not null, "brennt nach 4 Uhr weiter");
            var (geladen, _) = Speicherung.Laden(Speicherung.Speichern(sim, spieler, Zeitpunkt));
            Pruefe.Wahr(geladen.Wirtschaft!.Blitz.Brand(spaet.Ziel.Kennung) is { } g && g.Minute == spaet.Minute, "nach dem Laden brennt es weiter");
            BisMinute(sim, spaet.BrandEnde + 1);
            var aus = meldungen.Skip(ab).Count(m => m.StartsWith(Gross(spaet.Ziel.Name)) && m.Contains(" brennt nicht mehr"));
            Pruefe.Gleich(1, aus, $"Meldung „brennt nicht mehr“ genau einmal ({spaet.Ziel.Name}, Ende {spaet.BrandEnde})");
        }),

        ("Blitz: Gleicher Startwert, gleiche Einschläge (zwei Läufe, je acht Jahre Köln mit Chance 1)", () =>
        {
            string Lauf()
            {
                var (sim, _) = Partie(MitChance(1), startwert: 1401, karte: "koeln");
                var liste = new List<string>();
                sim.Wirtschaft!.Meldung += m => { if (m.StartsWith("Blitzschlag!")) liste.Add($"{sim.Zeit.VergangeneMinuten} {m}"); };
                BisMinute(sim, 8L * Spielzeit.MinutenProTag);
                Pruefe.Wahr(liste.Count >= 1, "mindestens ein Einschlag");
                return string.Join("\n", liste);
            }
            Pruefe.Gleich(Lauf(), Lauf());
        }),
    };
}
