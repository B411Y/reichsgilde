using System.Security.Cryptography;
using System.Text;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

/// <summary>
/// Felspinsel P3: alle Pinselwerte in der Tafel (Abstand, Streuung des Einsinkens, Höhenfilter), Radierer nur für die angehakten Formen,
/// Ausdünnen bis zu einer Dichte und „Neu verteilen“ als Strich (Felspinsel.Verteilen). Jeder neue Wert hat eine Gegenprobe (ohne ihn wirkt er
/// nicht), jeder Strich ist ein Schritt und Rückgängig gibt dieselbe Datei. Alte Striche bleiben Byte für Byte gleich (Sollwerte von main 4fa25271; „felswand“ seit der Großen Wand mit mittleren Wänden von main 5a7f6c51+, Wert auf main nachgemessen).
/// </summary>
static class FelspinselP3Tests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenort O(double x, double z) => new(x, z);

    /// <summary>Ein Hügel (bis etwa 10 m) in der Mitte einer leeren Karte.</summary>
    static Kartenentwurf Huegel()
    {
        var e = Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);
        e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 40, Staerke: 10, Kante: 0.5), new[] { O(0, 0) });
        return e;
    }

    /// <summary>Ein Strich quer über den Hügel.</summary>
    static readonly Kartenort[] Strich = Enumerable.Range(0, 9).Select(i => O(-60 + i * 15, 4)).ToArray();

    /// <summary>Blöcke und Platten dicht gestreut (ohne Boden, damit die Datei klein bleibt).</summary>
    static Felspinsel Streu => new(Formen: new[] { "block", "platte" }, Radius: 14, Dichte: 0.5, Boden: false);

    static Kartenentwurf Gestreut()
    {
        var e = Huegel();
        e.FelsStreuen(Streu, Strich, startwert: 7);
        return e;
    }

    static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    /// <summary>Wie viel der Kreisfläche die Grundflächen der Stücke mit der Mitte im Kreis decken (wie der Pinsel zählt).</summary>
    static double Deckung(Kartenentwurf e, Kartenort m, double r) =>
        e.Felsen.Where(f => (f.X - m.X) * (f.X - m.X) + (f.Z - m.Z) * (f.Z - m.Z) <= r * r).Sum(f => Math.PI * f.Radius * f.Radius) / (Math.PI * r * r);

    /// <summary>
    /// Ein fester Ablauf für den Gleichheitstest (Zeile „felsp3 …“): Streuen mit Abstand und Streuung des Einsinkens, Ausdünnen, Neu verteilen,
    /// Radierer nur für Platten mit Höhenfilter, Stempel mit Höhenfilter, Rückgängig und Wiederholen – die Kartendatei am Ende.
    /// </summary>
    internal static string Ablauf()
    {
        var e = Gestreut();
        e.FelsStreuen(Streu with { Formen = new[] { "block", "schuttbrocken" }, Abstand = 1.5, EinsinkenStreuung = 0.2, Boden = true, Dichte = 0.3 },
            new[] { O(-30, -60), O(30, -60) }, startwert: 11);
        e.FelsStreuen(Streu with { Radierer = true, Ausduennen = true, Dichte = 0.15 }, new[] { O(-50, 4), O(-5, 4) }, startwert: 3);
        e.FelsStreuen(Streu with { Verteilen = Felswurf.Einsinken | Felswurf.Drehung | Felswurf.Groesse, Einsinken = 0.45, Groesse = 1.3 },
            new[] { O(0, 4), O(40, 4) }, startwert: 5);
        e.FelsStreuen(Streu with { Radierer = true, NurFormen = true, Formen = new[] { "platte" }, Hoehenbereich = new Felsbereich(4, 60) },
            new[] { O(-20, 4), O(20, 4) });
        e.FelsStempeln(new Felspinsel(Gruppe: "kuppe", Hoehenbereich: new Felsbereich(-5, 3)), O(120, 90), startwert: 2);
        e.FelsStempeln(new Felspinsel(Gruppe: "kuppe", Hoehenbereich: new Felsbereich(-5, 3)), O(0, 0), startwert: 2); // oben auf dem Hügel: nichts
        e.Rueckgaengig();
        e.Wiederholen();
        return e.ZuJson();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Fels P3: Alte Abläufe (F3, F6, R2, R3/R5, P2) geben dieselbe Datei wie auf main 4fa25271; die neuen Werte mit ihrer Vorgabe ändern nichts", () =>
        {
            Pruefe.Gleich("976a922583daccc2cc59f4d00f93471b5ac046e819bcec409f8cd9fdd61364ba", Sha(FelsenTests.Ablauf()), "fels");
            Pruefe.Gleich("6f093ff6e830fe19702659bad6f8c0a4cf3f76ffdbb0fce5690f4ff9483cb461", Sha(FelsbodenTests.Ablauf()), "felsboden");
            Pruefe.Gleich("d0394ef5baaac59159760545afd76187151ea2cd794037f47160f81b268b2da5", Sha(FelslageTests.Ablauf()), "felslage");
            Pruefe.Gleich("ff567d61546cc50b35ba891ce092b2c2351aea199c8ae49d8ead6ded8fcc203a", Sha(FelsgizmoTests.Ablauf()), "felswand");
            Pruefe.Gleich("a9ba0ec6148012bb578173b9ac85f5e9e919e30b41e9714341248a8092098f90", Sha(FelsKopierenTests.Ablauf()), "felskopie");
            var a = Gestreut();
            var b = Huegel();
            b.FelsStreuen(Streu with { Abstand = 0, EinsinkenStreuung = 0.05, Hoehenbereich = null, NurFormen = false, Ausduennen = false, Verteilen = null }, Strich, startwert: 7);
            Pruefe.Gleich(a.ZuJson(), b.ZuJson(), "Vorgaben ausgeschrieben");
            // Gegenprobe: eine andere Streuung des Einsinkens ändert die Datei (dieselben Würfe, andere Werte)
            var c = Huegel();
            c.FelsStreuen(Streu with { EinsinkenStreuung = 0.2 }, Strich, startwert: 7);
            Pruefe.Wahr(c.ZuJson() != a.ZuJson(), "Streuung 0,2 ändert die Datei");
            Pruefe.Gleich(a.Felsen.Count, c.Felsen.Count, "… aber nicht die Lage und Zahl der Stücke");
        }),

        ("Fels P3: Abstand hält die Grundflächen beim Streuen so viele Meter mehr auseinander; ohne Abstand stehen Stücke näher", () =>
        {
            const double abstand = 3;
            bool AlleWeit(Kartenentwurf e, double mehr)
            {
                var l = e.Felsen;
                for (var i = 0; i < l.Count; i++)
                    for (var j = i + 1; j < l.Count; j++)
                    {
                        var ue = Math.Max(l[i].Felsform!.Ueberlappung, l[j].Felsform!.Ueberlappung);
                        var d = (1 - ue) * (l[i].Radius + l[j].Radius) + mehr - 0.1; // die Datei rundet die Lage
                        if ((l[i].X - l[j].X) * (l[i].X - l[j].X) + (l[i].Z - l[j].Z) * (l[i].Z - l[j].Z) < d * d) return false;
                    }
                return true;
            }
            var mit = Huegel();
            mit.FelsStreuen(Streu with { Abstand = abstand }, Strich, startwert: 7);
            Pruefe.Wahr(mit.Felsen.Count > 5, $"es stehen Stücke: {mit.Felsen.Count}");
            Pruefe.Wahr(AlleWeit(mit, abstand), "alle mindestens den Abstand weiter");
            var ohne = Gestreut();
            Pruefe.Wahr(!AlleWeit(ohne, abstand), "Gegenprobe: ohne Abstand stehen welche näher");
            Pruefe.Wahr(mit.Felsen.Count < ohne.Felsen.Count, $"mit Abstand weniger Stücke: {mit.Felsen.Count} gegen {ohne.Felsen.Count}");
            Pruefe.Gleich(Felspinsel.AbstandMax, new Felspinsel(Abstand: 999).Begrenzt().Abstand, "begrenzt");
        }),

        ("Fels P3: Streuung des Einsinkens 0 setzt jedes Stück gleich tief, 0,3 streut weit; die Vorgabe streut ± 0,05", () =>
        {
            double Spanne(double streuung)
            {
                var e = Huegel();
                e.FelsStreuen(Streu with { Formen = new[] { "block" }, EinsinkenStreuung = streuung }, Strich, startwert: 7);
                return e.Felsen.Max(f => f.Einsinken) - e.Felsen.Min(f => f.Einsinken);
            }
            Pruefe.Gleich(0.0, Spanne(0), "Streuung 0");
            Pruefe.Wahr(Spanne(0.3) > 0.3, $"Streuung 0,3: Spanne {Spanne(0.3)}");
            var vorgabe = Spanne(Felspinsel.EinsinkenStreuungVorgabe);
            Pruefe.Wahr(vorgabe is > 0 and <= 0.1 + 1e-9, $"Vorgabe: Spanne {vorgabe}");
        }),

        ("Fels P3: Höhenfilter – Streuen, Stempel und Radierer wirken nur, wo das Gelände an der Mitte so hoch ist; ohne Filter auch darüber und darunter", () =>
        {
            var e = Huegel();
            var bereich = new Felsbereich(3, 8);
            e.FelsStreuen(Streu with { Hoehenbereich = bereich }, Strich, startwert: 7);
            Pruefe.Wahr(e.Felsen.Count > 3, $"es stehen Stücke: {e.Felsen.Count}");
            // gefiltert wird am gewürfelten Punkt, die Datei rundet die Lage danach auf 5 cm: am steilen Hang bis zu wenige cm Höhe
            Pruefe.Wahr(e.Felsen.All(f => e.Hoehen.Hoehe(f.X, f.Z) is >= 2.95 and <= 8.05), "alle zwischen 3 und 8 m");
            var ohne = Gestreut();
            Pruefe.Wahr(ohne.Felsen.Any(f => ohne.Hoehen.Hoehe(f.X, f.Z) < 3) && ohne.Felsen.Any(f => ohne.Hoehen.Hoehe(f.X, f.Z) > 8), "Gegenprobe: ohne Filter darunter und darüber");
            // Radierer nur oben auf dem Hügel
            var vorher = ohne.Felsen.Count(f => ohne.Hoehen.Hoehe(f.X, f.Z) <= 8);
            ohne.FelsStreuen(Felspinsel.Radiergummi(30) with { Hoehenbereich = new Felsbereich(8, 60) }, Strich);
            Pruefe.Wahr(ohne.Felsen.All(f => ohne.Hoehen.Hoehe(f.X, f.Z) <= 8), "oben ist alles weg");
            Pruefe.Gleich(vorher, ohne.Felsen.Count, "unten bleibt alles");
            // Stempel: auf dem Hügel (gut 10 m) nichts, unten schon
            var s = Huegel();
            Pruefe.Gleich(0, s.FelsStempeln(new Felspinsel(Gruppe: "kuppe", Hoehenbereich: new Felsbereich(-5, 3)), O(0, 0), startwert: 1).Count, "oben nicht");
            Pruefe.Wahr(s.FelsStempeln(new Felspinsel(Gruppe: "kuppe", Hoehenbereich: new Felsbereich(-5, 3)), O(120, 90), startwert: 1).Count > 0, "unten schon");
            Pruefe.Wahr(s.FelsStempeln(new Felspinsel(Gruppe: "kuppe"), O(0, 0), startwert: 1).Count > 0, "Gegenprobe: ohne Filter auch oben");
        }),

        ("Fels P3: Radierer mit „nur diese Formen“ nimmt nur Platten, die Blöcke bleiben; ohne den Haken ist alles weg", () =>
        {
            var e = Gestreut();
            var bloecke = e.Felsen.Count(f => f.Form == "block");
            Pruefe.Wahr(bloecke > 0 && e.Felsen.Any(f => f.Form == "platte"), "Blöcke und Platten gestreut");
            e.FelsStreuen(Felspinsel.Radiergummi(30) with { NurFormen = true, Formen = new[] { "platte" } }, Strich);
            Pruefe.Gleich(bloecke, e.Felsen.Count, "nur die Blöcke bleiben");
            Pruefe.Wahr(e.Felsen.All(f => f.Form == "block"), "keine Platte mehr");
            var g = Gestreut();
            g.FelsStreuen(Felspinsel.Radiergummi(30) with { Formen = new[] { "platte" } }, Strich);
            Pruefe.Gleich(0, g.Felsen.Count, "Gegenprobe: ohne „nur diese Formen“ alles weg");
            // eine Gruppe als Vorlage: ihre Formen
            Pruefe.Wahr(new Felspinsel(Gruppe: "kuppe", NurFormen: true).NimmtForm("block"), "Kuppe hat Blöcke");
            Pruefe.Wahr(!new Felspinsel(Gruppe: "kuppe", NurFormen: true).NimmtForm("wand_gross_1"), "aber keine große Wand");
        }),

        ("Fels P3: Ausdünnen nimmt Stücke weg, bis die Dichte erreicht ist (nicht alle), ein Schritt; Rückgängig gibt dieselbe Datei; eine höhere Dichte nimmt nichts", () =>
        {
            var e = Gestreut();
            var vorher = e.ZuJson();
            var (zahl, schritte) = (e.Felsen.Count, e.Geschichte.Schritte);
            var mitte = O(0, 4);
            var deckung = Deckung(e, mitte, 14);
            Pruefe.Wahr(deckung > 0.3, $"vorher dicht: {deckung:0.00}");
            Pruefe.Wahr(e.FelsStreuen(Streu with { Radierer = true, Ausduennen = true, Dichte = 0.15 }, new[] { mitte }, startwert: 3), "geändert");
            Pruefe.Wahr(Deckung(e, mitte, 14) <= 0.15 + 1e-9, $"nachher höchstens 15 %: {Deckung(e, mitte, 14):0.00}");
            Pruefe.Wahr(Deckung(e, mitte, 14) > 0.05, "nicht alles weg");
            Pruefe.Wahr(e.Felsen.Count < zahl, "weniger Stücke");
            Pruefe.Gleich(schritte + 1, e.Geschichte.Schritte, "ein Schritt");
            Pruefe.Gleich($"{zahl - e.Felsen.Count} Felsstücke ausgedünnt", e.Geschichte.RueckgaengigText);
            var nachher = e.ZuJson();
            e.Rueckgaengig();
            Pruefe.Gleich(vorher, e.ZuJson(), "Rückgängig: dieselbe Datei");
            e.Wiederholen();
            Pruefe.Gleich(nachher, e.ZuJson(), "Wiederholen: dieselbe Datei");
            // Gegenprobe: Dichte über der Deckung nimmt nichts (kein Schritt)
            var g = Gestreut();
            Pruefe.Wahr(!g.FelsStreuen(Streu with { Radierer = true, Ausduennen = true, Dichte = 0.95 }, new[] { mitte }, startwert: 3), "nichts zu dünnen");
            Pruefe.Gleich(zahl, g.Felsen.Count);
            // gleicher Strich, gleiches Ergebnis; anderer Startwert, andere Wahl
            var h = Gestreut();
            h.FelsStreuen(Streu with { Radierer = true, Ausduennen = true, Dichte = 0.15 }, new[] { mitte }, startwert: 3);
            Pruefe.Gleich(nachher, h.ZuJson(), "gleicher Strich");
            var k = Gestreut();
            k.FelsStreuen(Streu with { Radierer = true, Ausduennen = true, Dichte = 0.15 }, new[] { mitte }, startwert: 4);
            Pruefe.Wahr(k.ZuJson() != nachher, "anderer Startwert: andere Stücke");
        }),

        ("Fels P3: Ausdünnen mit „nur diese Formen“ nimmt nur Platten weg; Blöcke bleiben, auch wenn die Dichte dann nicht erreicht wird", () =>
        {
            var e = Gestreut();
            var bloecke = e.Felsen.Where(f => f.Form == "block").ToList();
            // Radius 20 statt 14: die Datei rundet die Lage, ein Stück am Rand des Streukreises läge sonst knapp außerhalb
            e.FelsStreuen(Streu with { Radierer = true, Ausduennen = true, Dichte = 0, NurFormen = true, Formen = new[] { "platte" }, Radius = 20 }, Strich, startwert: 3);
            Pruefe.Wahr(bloecke.All(b => e.Felsen.Contains(b)), "alle Blöcke stehen noch");
            Pruefe.Wahr(e.Felsen.All(f => f.Form == "block"), "Dichte 0: alle Platten im Strich weg");
        }),

        ("Fels P3: Neu verteilen als Strich würfelt nur die angehakten Werte der Stücke unter dem Pinsel neu, jedes einmal, ein Schritt; die übrigen bleiben", () =>
        {
            var e = Gestreut();
            var vorher = e.ZuJson();
            var lagen = e.Felsen.ToDictionary<EntwurfFels, EntwurfFels, Felslage>(f => f, f => f.AlsLage(), ReferenceEqualityComparer.Instance);
            var strich = new[] { O(-30, 4), O(30, 4) };
            var schritte = e.Geschichte.Schritte;
            var pinsel = Streu with { Verteilen = Felswurf.Einsinken, Einsinken = 0.5, EinsinkenStreuung = 0 };
            Pruefe.Wahr(e.FelsStreuen(pinsel, strich, startwert: 5), "geändert");
            Pruefe.Gleich(schritte + 1, e.Geschichte.Schritte, "ein Schritt");
            Pruefe.Gleich(lagen.Count, e.Felsen.Count, "keine Stücke dazu oder weg");
            // die Tupfer liegen alle halben Radius (7 m) entlang des Strichs: −30, −23 … 26
            var tupfer = Enumerable.Range(0, 9).Select(k => -30.0 + 7 * k).ToList();
            bool UnterDemPinsel(EntwurfFels f) => tupfer.Any(x => (f.X - x) * (f.X - x) + (f.Z - 4) * (f.Z - 4) <= 14 * 14);
            var geaendert = 0;
            foreach (var f in e.Felsen)
            {
                var alt = lagen[f];
                Pruefe.Gleich(alt with { Einsinken = f.Einsinken }, f.AlsLage(), "nur das Einsinken ändert sich");
                if (UnterDemPinsel(f))
                {
                    Pruefe.Gleich(Math.Round(0.5 * f.Felsform!.EinsinkenFaktor, 2), f.Einsinken, "unter dem Pinsel: das Einsinken des Pinsels");
                    geaendert++;
                }
                else Pruefe.Gleich(alt.Einsinken, f.Einsinken, "außerhalb: unverändert");
            }
            Pruefe.Wahr(e.Felsen.Any(f => !UnterDemPinsel(f)), "Gegenprobe: es gibt Stücke außerhalb");
            Pruefe.Wahr(geaendert > 3, $"es wurden Stücke verteilt: {geaendert}");
            Pruefe.Wahr(e.Geschichte.RueckgaengigText!.EndsWith("Felsstücke neu verteilt"), e.Geschichte.RueckgaengigText);
            e.Rueckgaengig();
            Pruefe.Gleich(vorher, e.ZuJson(), "Rückgängig: dieselbe Datei");
            // Hin und zurück: jedes Stück nur einmal je Strich (so viele Änderungen, wie Stücke berührt wurden)
            var h = Gestreut();
            h.FelsStreuen(pinsel with { Verteilen = Felswurf.Drehung }, new[] { O(-30, 4), O(30, 4), O(-30, 4) }, startwert: 5);
            var zahl = int.Parse(h.Geschichte.RueckgaengigText!.Split(' ')[0]);
            Pruefe.Wahr(zahl <= h.Felsen.Count, $"höchstens jedes Stück einmal: {zahl} von {h.Felsen.Count}");
            // Gegenprobe: ohne Verteilen streut derselbe Strich neue Stücke
            var g = Gestreut();
            g.FelsStreuen(pinsel with { Verteilen = null, Dichte = 0.9 }, strich, startwert: 5);
            Pruefe.Wahr(g.Felsen.Count > lagen.Count, "ohne Verteilen kommen Stücke dazu");
        }),

        ("Fels P3: Neu verteilen nimmt mit „nur diese Formen“ und Filter nur passende Stücke; Verteilen ohne Wert gilt nicht (streut)", () =>
        {
            var e = Gestreut();
            var alt = e.Felsen.ToDictionary<EntwurfFels, EntwurfFels, double>(f => f, f => f.Drehung, ReferenceEqualityComparer.Instance);
            e.FelsStreuen(Streu with { Verteilen = Felswurf.Drehung, NurFormen = true, Formen = new[] { "block" }, Radius = 40 }, new[] { O(0, 4) }, startwert: 2);
            Pruefe.Wahr(e.Felsen.Where(f => f.Form == "platte").All(f => alt[f] == f.Drehung), "Platten unverändert");
            Pruefe.Wahr(e.Felsen.Where(f => f.Form == "block").Count(f => alt[f] != f.Drehung) > 2, "Blöcke neu gedreht");
            Pruefe.Gleich(null, new Felspinsel(Verteilen: Felswurf.Keins).Begrenzt().Verteilen, "Keins: kein Verteilen");
        }),

        ("Fels P3: Der Ablauf des Gleichheitstests ergibt zweimal dieselbe Datei; Ausdünnen, Verteilen und der Höhenfilter haben gewirkt", () =>
        {
            var a = Ablauf();
            Pruefe.Gleich(a, Ablauf());
            var e = Kartenentwurf.AusJson(a, Katalog.Value);
            Pruefe.Wahr(e.Felsen.Count > 10, $"Stücke: {e.Felsen.Count}");
            Pruefe.Wahr(e.Felsen.Any(f => f.Einsinken >= 0.44), "verteilt mit Einsinken 0,45");
        }),
    };
}
