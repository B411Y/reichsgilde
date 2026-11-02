using Reichsgilde;

/// <summary>
/// Wegbreite und Wegeinstieg der Passanten (Darstellung, 1.11.2026; <c>game/scripts/Passantenspur.cs</c> und <c>game/scripts/Wegeinstieg.cs</c>, hier
/// mitübersetzt): Die Spur nutzt die Breite des Weges aus der Karte und hält Abstand zur Wand; wer unterwegs einen neuen Weg bekommt, steigt auf dem
/// Wegstück ein, auf dem er ist, und geht gleich in Zielrichtung – kein Hin und Zurück.
/// </summary>
static class PassantenwegeTests
{
    static float Abstand((float X, float Z) a, (float X, float Z) b) => MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));

    /// <summary>Wie weit ein Punkt von der Strecke a–b entfernt ist.</summary>
    static float ZurStrecke((float X, float Z) p, (float X, float Z) a, (float X, float Z) b)
    {
        var (dx, dz) = (b.X - a.X, b.Z - a.Z);
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Z - a.Z) * dz) / (dx * dx + dz * dz), 0, 1);
        return Abstand(p, (a.X + dx * t, a.Z + dz * t));
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Passantenwege: die Spur nutzt die Breite des Weges – breite Straßen bis weit nach außen, enge Gassen eng, im Grundmaß wie bisher", () =>
        {
            // Grundmaß 2 m: genau die bisherigen Spuren
            foreach (var n in Enumerable.Range(0, 200)) Pruefe.Gleich(Passantenspur.Grundspur(n), Passantenspur.GrundspurBei(n, 1f));
            // 5-m-Straße: die Spuren reichen von 0,35 bis 1,8 m und verteilen sich über die ganze rechte Hälfte
            var breit = Enumerable.Range(0, 400).Select(n => Passantenspur.GrundspurBei(n, 2.5f)).ToList();
            Pruefe.Wahr(breit.Min() < 0.45f && breit.Max() > 1.7f, $"5-m-Straße: Spuren {breit.Min():0.00} bis {breit.Max():0.00} m");
            foreach (var halb in new[] { 0.5f, 0.6f, 1f, 1.25f, 1.75f, 2.5f, 4f })
                foreach (var n in Enumerable.Range(0, 400))
                    for (var s = 0f; s < 20f; s += 0.25f)
                    {
                        var spur = Passantenspur.Spur(n, s, halb);
                        Pruefe.Wahr(spur > 0, $"halbe Breite {halb}: Nummer {n} links der Mitte");
                        // Körper (0,25 m) bleibt im Weg; ab dem Grundmaß bleibt zur Wand mindestens ein Zehntel Meter Luft (auf breiten Wegen mehr)
                        var luft = halb - (spur + 0.25f);
                        Pruefe.Wahr(luft >= MathF.Max(0, 0.2f * (halb - 1)) - 1e-4f, $"halbe Breite {halb}: Nummer {n} bei {s} m nur {luft:0.00} m bis zur Wand");
                    }
            // ein schmaler Türweg (1,2 m) bleibt schmal
            Pruefe.Wahr(Enumerable.Range(0, 400).Max(n => Passantenspur.Spur(n, 3, 0.6f)) <= 0.45f, "Türweg zu weit außen");
        }),

        ("Passantenwege: die geglättete Breite wird vor einer engen Stelle rechtzeitig schmal, nie breiter als das eigene Stück und ohne Sprung", () =>
        {
            // 20 m breite Straße (halb 2,5), dann 10 m enge Gasse (halb 1), dann wieder breit
            var anfaenge = new[] { 0f, 20f, 30f };
            var halbe = new[] { 2.5f, 1f, 2.5f };
            var vorher = Passantenspur.Huellbreite(anfaenge, halbe, 50, 0);
            for (var s = 0f; s <= 50f; s += 0.05f)
            {
                var h = Passantenspur.Huellbreite(anfaenge, halbe, 50, s);
                var eigenes = s < 20 ? 2.5f : s < 30 ? 1f : 2.5f;
                Pruefe.Wahr(h <= eigenes + 1e-5f, $"bei {s} m breiter ({h}) als das eigene Stück ({eigenes})");
                Pruefe.Wahr(MathF.Abs(h - vorher) <= Passantenspur.BreiteSteigung * 0.05f + 1e-4f, $"bei {s} m springt die Breite um {h - vorher}");
                vorher = h;
            }
            Pruefe.Ungefaehr(1, Passantenspur.Huellbreite(anfaenge, halbe, 50, 20), 1e-5);
            Pruefe.Ungefaehr(2.5, Passantenspur.Huellbreite(anfaenge, halbe, 50, 5), 1e-5); // weit davor die volle Breite
            Pruefe.Ungefaehr(2.0, Passantenspur.Huellbreite(anfaenge, halbe, 50, 18), 1e-5); // 2 m davor schon schmaler
        }),

        ("Passantenwege: an der Einmündung in eine enge Gasse kommt der Körper der Figur der Wand nie zu nah, auch nicht an der Ecke", () =>
        {
            // breite Straße längs z (5 m), an der Kreuzung rechts in eine 2-m-Gasse (längs −x); die Häuser stehen außerhalb der Wege
            var linie = new Passantenspur.Polylinie(new (float, float)[] { (0, 0), (0, 30), (-20, 30) }, new[] { 2.5f, 1f });
            foreach (var n in Enumerable.Range(0, 120))
                for (var s = 0f; s <= linie.Laenge; s += 0.05f)
                {
                    var ort = Passantenspur.Gehen(linie, n, linie.Laenge, s, (0, 0), false).Ort;
                    // im Weg ist, wer in der Straße (|x| ≤ 2,5, z ≤ 32,5) oder in der Gasse (x ≤ 0, |z − 30| ≤ 1) steht; der Körper (0,22 m) muss hineinpassen
                    var inStrasse = MathF.Abs(ort.X) <= 2.5f - 0.22f && ort.Z <= 30 + 2.5f - 0.22f;
                    var inGasse = ort.X <= 2.5f && MathF.Abs(ort.Z - 30) <= 1f - 0.22f;
                    Pruefe.Wahr(inStrasse || inGasse, $"Nummer {n} bei {s:0.00} m: ({ort.X:0.00} | {ort.Z:0.00}) steckt in einem Haus");
                }
        }),

        ("Passantenwege: an einer Ecke der breiten Straße geht die Figur nach Bodenmetern gleichmäßig weiter – innen nicht rückwärts und nicht stehend, außen nicht gehetzt", () =>
        {
            foreach (var rechtsUm in new[] { true, false })
            {
                // Ecke nach rechts (rechts geht innen) oder nach links (außen)
                var linie = new Passantenspur.Polylinie(new (float, float)[] { (0, 0), (0, 30), (rechtsUm ? -30 : 30, 30) }, new[] { 2.5f, 2.5f });
                foreach (var n in Enumerable.Range(0, 120))
                {
                    var tabelle = Passantenspur.Bodenmeter(linie, n, linie.Laenge, (0, 0));
                    // auf gerader Strecke ist der Bodenweg kaum länger als die Mittellinie (nur das Schwanken)
                    Pruefe.Wahr(tabelle[200] - tabelle[100] < 10.5f && tabelle[200] - tabelle[100] > 9.99f, $"Nummer {n}: 10 m Mittellinie sind {tabelle[200] - tabelle[100]:0.00} m Boden");
                    var (b0, b1) = (tabelle[250], tabelle[350]); // 25 bis 35 m Mittellinie: um die Ecke
                    var vorher = Passantenspur.Gehen(linie, n, linie.Laenge, Passantenspur.StreckeZuBoden(tabelle, linie.Laenge, b0), (0, 0), false).Ort;
                    for (var b = b0 + 0.02f; b <= b1; b += 0.02f)
                    {
                        var s = Passantenspur.StreckeZuBoden(tabelle, linie.Laenge, b);
                        var ort = Passantenspur.Gehen(linie, n, linie.Laenge, s, (0, 0), false).Ort;
                        var schritt = Abstand(ort, vorher);
                        // je 2 cm Boden zwischen 1,2 und 2,8 cm wirklich gegangen (zwischen den Stützpunkten der Tabelle gerade): gleichmäßig, weder stehend noch hetzend
                        Pruefe.Wahr(schritt >= 0.012f && schritt <= 0.028f, $"{(rechtsUm ? "innen" : "außen")}, Nummer {n} bei {s:0.00} m: {schritt * 100:0.0} cm statt 2 cm");
                        // nicht rückwärts: vor der Ecke nach +z, danach weg von der Straße, an der Ecke wenigstens in eine der beiden Richtungen
                        var (dx, dz) = (ort.X - vorher.X, ort.Z - vorher.Z);
                        var weg = rechtsUm ? -dx : dx;
                        Pruefe.Wahr(s < 28 ? dz > 0 : s > 32 ? weg > 0 : dz > -0.002f || weg > 0.002f, $"{(rechtsUm ? "innen" : "außen")}, Nummer {n} bei {s:0.00} m: rückwärts ({dx:0.000} | {dz:0.000})");
                        vorher = ort;
                    }
                }
            }
        }),

        ("Passantenwege: Losgehen gestreut – je Person fest 0 bis 45 Minuten, gleichmäßig verteilt", () =>
        {
            var versaetze = Enumerable.Range(0, 1000).Select(Passantenspur.Losgehversatz).ToList();
            Pruefe.Wahr(versaetze.All(v => v >= 0 && v <= Passantenspur.LosgehenStreuung), "außerhalb 0 bis 45");
            Pruefe.Gleich(Passantenspur.Losgehversatz(17), Passantenspur.Losgehversatz(17));
            for (var viertel = 0; viertel < 4; viertel++)
            {
                var anzahl = versaetze.Count(v => v >= viertel * 11.25 && v < (viertel + 1) * 11.25);
                Pruefe.Wahr(anzahl > 200, $"im {viertel + 1}. Viertel nur {anzahl}");
            }
            // unabhängig von der Spur (sonst gingen die, die zusammen losgehen, auch auf derselben Spur)
            var spur = Enumerable.Range(0, 1000).Select(n => (double)Passantenspur.Grundspur(n)).ToList();
            var (mv, ms) = (versaetze.Average(), spur.Average());
            var r = versaetze.Zip(spur, (v, s) => (v - mv) * (s - ms)).Sum() / Math.Sqrt(versaetze.Sum(v => (v - mv) * (v - mv)) * spur.Sum(s => (s - ms) * (s - ms)));
            Pruefe.Wahr(Math.Abs(r) < 0.1, $"Versatz und Spur hängen zusammen (r = {r:0.00})");
        }),

        ("Wegeinstieg: wer unterwegs einen neuen Weg bekommt, steigt vorwärts oder rückwärts am nächsten Knoten seines Wegstücks ein", () =>
        {
            // alter Pfad: Tür (kein Knoten) → A → B → C → Ziel (kein Knoten); die Figur ist zwischen A und B, 2 m hinter A
            var pfad = new (float X, float Z)[] { (0, -3), (0, 0), (0, 10), (0, 20), (2, 20) };
            int Knoten(int i) => i is 1 or 2 or 3 ? i : -1;
            var einstiege = Wegeinstieg.Einstiege(pfad, 5f, Knoten);
            Pruefe.Gleich(2, einstiege.Count);
            Pruefe.Gleich(2, einstiege[0].Knoten); Pruefe.Ungefaehr(8, einstiege[0].Laenge, 1e-5); Pruefe.Gleich("2", string.Join(",", einstiege[0].Folge));
            Pruefe.Gleich(1, einstiege[1].Knoten); Pruefe.Ungefaehr(2, einstiege[1].Laenge, 1e-5); Pruefe.Gleich("1", string.Join(",", einstiege[1].Folge));
            // auf dem letzten Stück (zum Ziel, kein Knoten): vorwärts keiner, rückwärts C
            var amEnde = Wegeinstieg.Einstiege(pfad, 24f, Knoten);
            Pruefe.Gleich(1, amEnde.Count);
            Pruefe.Gleich(3, amEnde[0].Knoten); Pruefe.Ungefaehr(1, amEnde[0].Laenge, 1e-5);
            // auf der Tür-Strecke: vorwärts A, rückwärts keiner
            var amAnfang = Wegeinstieg.Einstiege(pfad, 1f, Knoten);
            Pruefe.Gleich(1, amAnfang.Count); Pruefe.Gleich(1, amAnfang[0].Knoten); Pruefe.Ungefaehr(2, amAnfang[0].Laenge, 1e-5);
            // Ausstiege: jeder Knoten mit dem Rest bis zum Ende
            var aus = Wegeinstieg.Ausstiege(pfad, Knoten).OrderBy(a => a.Knoten).ToList();
            Pruefe.Gleich(3, aus.Count);
            Pruefe.Ungefaehr(22, aus[0].Laenge, 1e-5); Pruefe.Ungefaehr(12, aus[1].Laenge, 1e-5); Pruefe.Ungefaehr(2, aus[2].Laenge, 1e-5);
            Pruefe.Gleich("3,4", string.Join(",", aus[2].Folge));
        }),

        ("Wegeinstieg: ein Weg, der bis zu einem Punkt und auf derselben Linie wieder zurück geht, verliert den Umweg; Ecken und spitze Gabeln bleiben", () =>
        {
            // Stelle (0,5) → zurück zum Knoten (0,0) → wieder vorwärts (0,10) → weiter: der Knoten (0,0) fällt weg
            var hinUndZurueck = new (float X, float Z)[] { (0, 5), (0, 0), (0, 10), (5, 10) };
            Pruefe.Gleich("0,2,3", string.Join(",", Wegeinstieg.OhneUmkehr(hinUndZurueck)));
            // am Ziel: bis zum Knoten (0,20), dann zurück zum Platz auf derselben Gasse (0,15)
            var amZiel = new (float X, float Z)[] { (0, 0), (0, 10), (0, 20), (0, 15) };
            Pruefe.Gleich("0,1,3", string.Join(",", Wegeinstieg.OhneUmkehr(amZiel)));
            // eine rechtwinklige Ecke bleibt
            Pruefe.Gleich("0,1,2", string.Join(",", Wegeinstieg.OhneUmkehr(new (float, float)[] { (0, 0), (0, 10), (10, 10) })));
            // eine spitze Gabelung zweier Gassen (160°) bleibt: die Abkürzung ginge durch die Häuser
            var gabel = new (float X, float Z)[] { (0, 0), (0, 20), (6.8f, 1.2f) };
            Pruefe.Gleich("0,1,2", string.Join(",", Wegeinstieg.OhneUmkehr(gabel)));
            // doppelte Punkte fallen weg, Anfang und Ende bleiben
            Pruefe.Gleich("0,2,3", string.Join(",", Wegeinstieg.OhneUmkehr(new (float, float)[] { (0, 0), (0, 0.01f), (0, 5), (3, 5) })));
            Pruefe.Gleich("0,2", string.Join(",", Wegeinstieg.OhneUmkehr(new (float, float)[] { (0, 0), (0, 5), (0, 5.01f) })));
        }),

        ("Wegeinstieg: zusammengesetzt mit dem kürzesten Netzweg geht die Figur vom Wegstück direkt weiter, ohne erst zum Knoten hinter ihr zu laufen", () =>
        {
            // Netz: eine Gasse A(0,0) – B(0,10) – C(0,20), bei B zweigt D(10,10) ab. Alter Weg A → C, die Figur ist bei (0,12) (hinter ihr B).
            // Neuer Weg (ab dem alten Ziel C): C → B → D. Erwartet: von (0,12) zurück nach B und gleich nach D – nicht erst nach C und zurück.
            var knoten = new (float X, float Z)[] { (0, 0), (0, 10), (0, 20), (10, 10) };
            var kanten = new[] { (0, 1), (1, 2), (1, 3) };
            float Netzweg(int a, int b, out List<int> folge)
            {
                // kleiner Dijkstra für den Test
                var abstand = Enumerable.Repeat(float.MaxValue, knoten.Length).ToArray();
                var vor = Enumerable.Repeat(-1, knoten.Length).ToArray();
                abstand[a] = 0;
                var offen = new List<int> { a };
                while (offen.Count > 0)
                {
                    var k = offen.OrderBy(x => abstand[x]).First();
                    offen.Remove(k);
                    foreach (var (x, y) in kanten)
                    {
                        var z = x == k ? y : y == k ? x : -1;
                        if (z < 0 || abstand[k] + Abstand(knoten[k], knoten[z]) >= abstand[z]) continue;
                        abstand[z] = abstand[k] + Abstand(knoten[k], knoten[z]);
                        vor[z] = k;
                        offen.Add(z);
                    }
                }
                folge = new List<int>();
                for (var k = b; k >= 0; k = vor[k]) folge.Insert(0, k);
                return abstand[b];
            }
            var alt = new[] { knoten[0], knoten[1], knoten[2] };
            var neu = new[] { knoten[2], knoten[1], knoten[3] };
            var einstiege = Wegeinstieg.Einstiege(alt, 12f, i => i);
            var ausstiege = Wegeinstieg.Ausstiege(neu, i => new[] { 2, 1, 3 }[i]);
            var beste = (Laenge: float.MaxValue, Punkte: new List<(float X, float Z)>());
            foreach (var ein in einstiege)
                foreach (var aus in ausstiege)
                {
                    var gesamt = ein.Laenge + Netzweg(ein.Knoten, aus.Knoten, out var folge) + aus.Laenge;
                    if (gesamt >= beste.Laenge) continue;
                    var punkte = new List<(float X, float Z)> { (0, 12) };
                    punkte.AddRange(ein.Folge.Select(i => alt[i]));
                    punkte.AddRange(folge.Select(k => knoten[k]));
                    punkte.AddRange(aus.Folge.Select(i => neu[i]));
                    beste = (gesamt, punkte);
                }
            var weg = Wegeinstieg.OhneUmkehr(beste.Punkte).Select(i => beste.Punkte[i]).ToList();
            Pruefe.Ungefaehr(12, beste.Laenge, 1e-4);
            Pruefe.Gleich("(0, 12) (0, 10) (10, 10)", string.Join(" ", weg));
            // die alte Art (zum nächsten Knoten, dann an den Anfang des neuen Weges) wäre 12 m länger gewesen: über C und zurück
            Pruefe.Wahr(weg.All(p => ZurStrecke(p, (0, 0), (0, 12)) < 1e-4f || p == (10, 10)), "der Weg führt nicht über C");
        }),
    };
}
