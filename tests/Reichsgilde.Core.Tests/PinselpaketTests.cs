using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Kartenwerkstatt, Pinsel-Paket 1 (Terrain3D-Übernahme, Etappe 1): Filter nach Steigung und Boden, nur Mulden heben / nur Spitzen kappen,
/// Ebnen auf eine eingegebene Höhe mit Pipette, Terrassen, die Abfragen der Anzeige unter dem Pinsel – und dass gleicher Strich gleiche Bytes
/// gibt, Rückgängig Byte für Byte zurückführt und die alten Pinsel unverändert rechnen.
/// </summary>
static class PinselpaketTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenort O(double x, double z) => new(x, z);

    /// <summary>Eine leere Karte 400 × 300 m mit 300 m Rand (x −500 … 500, z −450 … 450).</summary>
    static Kartenentwurf Leer() => Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);

    /// <summary>
    /// Westlich von x = 0 eben (0 m), von x = 0 bis 100 ein gleichmäßiger Hang mit 30 % nach Osten (Rasterpunkt i hat 60 · i cm), für |z| &lt; 200.
    /// Die Steigung: 0 % bis Punkt −1, 15 % am Punkt 0 (Mittendifferenz über die Kante), 30 % ab Punkt 1.
    /// </summary>
    static Kartenentwurf HangMitEbene()
    {
        var e = Leer();
        e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 1, Staerke: 30, Kante: 0), new[] { O(100, 0) });
        e.Rampe(O(0, 0), O(100, 0), breite: 400, kante: 0);
        return e;
    }

    /// <summary>Die Höhe eines Rasterpunkts in cm am Ort (x, z) (ganze gerade Meter).</summary>
    static int Cm(Kartenentwurf e, double x, double z) => e.Hoehen.Wert((int)Math.Floor(x / 2), (int)Math.Floor(z / 2));

    static Bodenpinsel Malen(Bodentextur t, double radius, Pinselfilter? filter = null, double kante = 0) =>
        new(t, radius, Staerke: 1, Kante: kante, Deko: false, Filter: filter);

    /// <summary>Die Summe der zehn gemalten Anteile eines Bodenpunkts (die Wiese ist 255 minus sie).</summary>
    static int Summe(Bodenraster b, int i, int j) =>
        Enumerable.Range(1, Bodenraster.Texturen - 1).Sum(t => (int)b.Gewicht(i, j, (Bodentextur)t));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Pinselfilter: Steigung innen an den Grenzen weich (2 Prozentpunkte), bis nie unter ab, Schwelle des Bodenfilters nach dem Rand", () =>
        {
            var ab = new Pinselfilter(SteigungAb: 10);
            Pruefe.Gleich(0.0, ab.SteigungFaktor(9.9));
            Pruefe.Gleich(0.0, ab.SteigungFaktor(10));
            Pruefe.Ungefaehr(0.5, ab.SteigungFaktor(11), 1e-12);
            Pruefe.Gleich(1.0, ab.SteigungFaktor(12));
            Pruefe.Gleich(1.0, ab.SteigungFaktor(400));
            var bis = new Pinselfilter(SteigungBis: 8);
            Pruefe.Gleich(1.0, bis.SteigungFaktor(0));
            Pruefe.Gleich(1.0, bis.SteigungFaktor(6));
            Pruefe.Ungefaehr(0.5, bis.SteigungFaktor(7), 1e-12);
            Pruefe.Gleich(0.0, bis.SteigungFaktor(8), "genau an der Grenze wirkt nichts mehr – 8 % (Hinweis der Hangsperre) bleiben unberührt");
            Pruefe.Gleich(15.0, new Pinselfilter(SteigungAb: 15, SteigungBis: 5).Begrenzt().SteigungBis, "bis nie unter ab");
            Pruefe.Gleich(Pinselfilter.HoechsteSteigung, new Pinselfilter(SteigungAb: 5000).Begrenzt().SteigungAb);
            Pruefe.Gleich(128, new Pinselfilter(Boden: Bodentextur.Sand).BodenSchwelle);
            Pruefe.Gleich(64, new Pinselfilter(Boden: Bodentextur.Sand, BodenRand: 0.5).BodenSchwelle);
            Pruefe.Gleich(1, new Pinselfilter(Boden: Bodentextur.Sand, BodenRand: 1).BodenSchwelle);
            Pruefe.Wahr(Pinselfilter.Keiner.IstLeer && !new Pinselfilter(Boden: Bodentextur.Wiese).IstLeer && new Pinselfilter(SteigungBis: 50).MitSteigung, "leer / nicht leer");
            Pruefe.Gleich(null, new Pinsel(Pinselart.Heben, Filter: Pinselfilter.Keiner).Begrenzt().Filter, "ein leerer Filter fällt weg");
            Pruefe.Gleich(null, new Bodenpinsel(Bodentextur.Erde, Filter: new Pinselfilter()).Begrenzt().Filter);
        }),

        ("Steigung für Filter und Anzeige: Mittendifferenz am Höhenpunkt, bilinear dazwischen; leeres Raster 0", () =>
        {
            var e = HangMitEbene();
            Pruefe.Ungefaehr(30, Pinselfilter.Steigung(e.Hoehen, 40, 0), 1e-9);
            Pruefe.Ungefaehr(30, Pinselfilter.Steigung(e.Hoehen, 41, 13), 1e-9);
            Pruefe.Ungefaehr(15, Pinselfilter.Steigung(e.Hoehen, 0, 0), 1e-9);
            Pruefe.Ungefaehr(7.5, Pinselfilter.Steigung(e.Hoehen, -1, 0), 1e-9);
            Pruefe.Gleich(0.0, Pinselfilter.Steigung(e.Hoehen, -40, 0));
            Pruefe.Gleich(0.0, Pinselfilter.Steigung(new Hoehenraster(), 10, 10));
        }),

        ("Steigungsfilter am Texturpinsel: Fels nur ab 21 % – am Hang ganz, auf der Ebene nichts, am Hangfuß (22,5 %) weich", () =>
        {
            var e = HangMitEbene();
            Pruefe.Wahr(e.BodenMalen(Malen(Bodentextur.Fels, 30, new Pinselfilter(SteigungAb: 21)), new[] { O(-60, 0), O(90, 0) }), "gemalt");
            for (var x = -60; x <= 90; x++)
                for (var z = -20; z <= 20; z += 5)
                {
                    var fels = e.Boden.Gewicht(x, z, Bodentextur.Fels);
                    if (x <= 0) Pruefe.Gleich((byte)0, fels, $"Ebene und Hangkante ({x}, {z})");
                    else if (x >= 2) Pruefe.Gleich((byte)255, fels, $"Hang ({x}, {z})");
                    else Pruefe.Gleich((byte)Math.Round(255 * 0.84375, MidpointRounding.AwayFromZero), fels, $"weich am Fuß ({x}, {z}): Weich(0,75)");
                }
            // Wiese nur in der Ebene (bis 8 %): auf den Fels am Hang kommt nichts, die Ebene bleibt Wiese
            var f = HangMitEbene();
            f.BodenMalen(Malen(Bodentextur.Fels, 30), new[] { O(-60, 0), O(90, 0) });
            f.BodenMalen(Malen(Bodentextur.Waldboden, 30, new Pinselfilter(SteigungBis: 8)), new[] { O(-60, 0), O(90, 0) });
            Pruefe.Gleich((byte)255, f.Boden.Gewicht(-30, 0, Bodentextur.Waldboden));
            Pruefe.Gleich((byte)255, f.Boden.Gewicht(40, 0, Bodentextur.Fels), "am Hang bleibt der Fels");
            Pruefe.Gleich((byte)0, f.Boden.Gewicht(40, 0, Bodentextur.Waldboden));
        }),

        ("Steigungsfilter am Geländepinsel: Heben nur bis 8 % hebt die Ebene, der Hang bleibt Zentimeter für Zentimeter", () =>
        {
            var e = HangMitEbene();
            e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 30, Staerke: 1, Kante: 0, Filter: new Pinselfilter(SteigungBis: 8)), new[] { O(-60, 0), O(90, 0) });
            for (var i = 0; i <= 45; i++) Pruefe.Gleich((short)(60 * i), e.Hoehen.Wert(i, 0), $"Hang, Punkt {i}");
            for (var i = -28; i <= -2; i++) Pruefe.Wahr(e.Hoehen.Wert(i, 0) >= 100, $"Ebene gehoben, Punkt {i}: {e.Hoehen.Wert(i, 0)}");
            // gemessen am Stand vor dem Strich: Die gehobene Ebene wird nicht selbst zum Hang, der den Rest sperrt
            var g = HangMitEbene();
            g.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 10, Staerke: 2, Kante: 0, Filter: new Pinselfilter(SteigungBis: 8)), new[] { O(-60, 0), O(-20, 0) });
            for (var i = -34; i <= -11; i++) Pruefe.Wahr(g.Hoehen.Wert(i, 0) >= 200, $"jeder Punkt des Strichs gehoben, Punkt {i}: {g.Hoehen.Wert(i, 0)}");
        }),

        ("Bodenfilter: Moos nur auf Sand – Rand 0 % braucht die halbe Deckung, Rand 100 % einen Hauch; die Anteile bleiben unter 255", () =>
        {
            var e = Leer();
            e.BodenMalen(Malen(Bodentextur.Sand, 14, kante: 0.5), new[] { O(-30, 0) });
            e.BodenMalen(Malen(Bodentextur.Erde, 6, kante: 0.5) with { Staerke = 0.5 }, new[] { O(-24, 0) });
            var vorher = e.Boden.Kopie();
            var halb = e.Boden.Kopie();
            e.BodenMalen(Malen(Bodentextur.Moos, 40, new Pinselfilter(Boden: Bodentextur.Sand)), new[] { O(-30, 0) });
            var (mitRand0, mitRand1) = (0, 0);
            for (var x = -70; x <= 10; x++)
                for (var z = -40; z <= 40; z++)
                {
                    var sand = vorher.Gewicht(x, z, Bodentextur.Sand);
                    var moos = e.Boden.Gewicht(x, z, Bodentextur.Moos);
                    if (sand < 128) Pruefe.Gleich((byte)0, moos, $"ohne Sand ({x}, {z}): Sand {sand}");
                    else Pruefe.Gleich((byte)255, moos, $"auf Sand ({x}, {z})");
                    if (moos > 0) mitRand0++;
                    Pruefe.Wahr(Summe(e.Boden, x, z) <= 255, $"Summe ({x}, {z})");
                }
            var f = Leer();
            f.BodenMalen(Malen(Bodentextur.Sand, 14, kante: 0.5), new[] { O(-30, 0) });
            f.BodenMalen(Malen(Bodentextur.Erde, 6, kante: 0.5) with { Staerke = 0.5 }, new[] { O(-24, 0) });
            f.BodenMalen(Malen(Bodentextur.Moos, 40, new Pinselfilter(Boden: Bodentextur.Sand, BodenRand: 1)) with { Staerke = 0.6 }, new[] { O(-30, 0) });
            for (var x = -70; x <= 10; x++)
                for (var z = -40; z <= 40; z++)
                {
                    var moos = f.Boden.Gewicht(x, z, Bodentextur.Moos);
                    Pruefe.Gleich(halb.Gewicht(x, z, Bodentextur.Sand) == 0, moos == 0, $"Rand 100 %: Moos genau dort, wo Sand war ({x}, {z})");
                    if (moos > 0) mitRand1++;
                    Pruefe.Wahr(Summe(f.Boden, x, z) <= 255, $"Summe ({x}, {z})");
                }
            Pruefe.Wahr(mitRand1 > mitRand0 && mitRand0 > 100, $"der Rand erweitert die Fläche: {mitRand0} → {mitRand1}");
            // Wiese als Filter: nur auf unbemaltem Boden
            var g = Leer();
            g.BodenMalen(Malen(Bodentextur.Sand, 10), new[] { O(0, 0) });
            g.BodenMalen(Malen(Bodentextur.Kies, 30, new Pinselfilter(Boden: Bodentextur.Wiese)), new[] { O(0, 0) });
            Pruefe.Gleich((byte)255, g.Boden.Gewicht(0, 0, Bodentextur.Sand), "der Sand bleibt");
            Pruefe.Gleich((byte)255, g.Boden.Gewicht(20, 0, Bodentextur.Kies), "die Wiese wird Kies");
            // ein Geländepinsel mit Bodenfilter: nur auf dem Sand gehoben
            g.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 30, Staerke: 1, Kante: 0, Filter: new Pinselfilter(Boden: Bodentextur.Sand)), new[] { O(0, 0) });
            Pruefe.Gleich((short)100, g.Hoehen.Wert(0, 0));
            Pruefe.Gleich((short)0, g.Hoehen.Wert(10, 0), "auf dem Kies nichts");
        }),

        ("Nur Mulden heben, nur Spitzen kappen: Kuppe bzw. Mulde bleiben; mit der Mitte in der Mulde bleibt die Ebene, wie sie ist", () =>
        {
            Kartenentwurf Huegel()
            {
                var e = Leer();
                e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 6, Staerke: 4, Kante: 0), new[] { O(50, 0) });
                e.GelaendePinseln(new Pinsel(Pinselart.Senken, Radius: 6, Staerke: 4, Kante: 0), new[] { O(-50, 0) });
                return e;
            }
            var heben = new Pinsel(Pinselart.Heben, Radius: 80, Staerke: 1, Kante: 0);
            var a = Huegel();
            a.GelaendePinseln(heben with { NurAusgleich = true }, new[] { O(0, 0) });
            Pruefe.Gleich(400, Cm(a, 50, 0), "die Kuppe bleibt");
            Pruefe.Gleich(-300, Cm(a, -50, 0), "die Mulde steigt um den Hub");
            Pruefe.Gleich(100, Cm(a, 20, 0), "die Ebene unter der Mitte steigt bis Mitte + Hub");
            Pruefe.Gleich("Mulden gehoben", a.Geschichte.RueckgaengigText);
            var b = Huegel();
            b.GelaendePinseln(heben, new[] { O(0, 0) });
            Pruefe.Gleich(500, Cm(b, 50, 0), "ohne „nur Mulden“ steigt auch die Kuppe");
            var c = Huegel();
            c.GelaendePinseln(new Pinsel(Pinselart.Senken, Radius: 80, Staerke: 1, Kante: 0, NurAusgleich: true), new[] { O(0, 0) });
            Pruefe.Gleich(300, Cm(c, 50, 0), "die Spitze wird gekappt");
            Pruefe.Gleich(-400, Cm(c, -50, 0), "die Mulde bleibt");
            Pruefe.Gleich("Spitzen gekappt", c.Geschichte.RueckgaengigText);
            // die Mitte in der Mulde: nur die Mulde füllt sich, die Ebene rundum bleibt
            var d = Huegel();
            d.GelaendePinseln(heben with { Radius = 20, NurAusgleich = true }, new[] { O(-50, 0) });
            Pruefe.Gleich(-300, Cm(d, -50, 0));
            Pruefe.Gleich(0, Cm(d, -36, 0), "die Ebene bleibt");
            // ein Strich hebt die Mulde Tupfer für Tupfer, nie über ihren Rand + Hub
            var s = Huegel();
            s.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 10, Staerke: 0.5, Kante: 0, NurAusgleich: true), new[] { O(-52, 0), O(-48, 0), O(-52, 0), O(-48, 0) });
            Pruefe.Wahr(Cm(s, -50, 0) is > -400 and <= -100, $"gefüllt, höchstens um den Hub je Tupfer: {Cm(s, -50, 0)}");
            Pruefe.Gleich(0, Cm(s, -58, 0), "die Ebene neben der Mulde bleibt (die Mitte liegt tiefer als sie minus den Hub)");
        }),

        ("Ebnen auf eine eingegebene Zielhöhe (auf cm, −20 … +60 m) und Höhen-Pipette; Rückgängig Byte für Byte", () =>
        {
            var e = HangMitEbene();
            Pruefe.Gleich(12.0, Gelaendepinsel.Pipette(e.Hoehen, O(40, 0)));
            Pruefe.Gleich(12.3, Gelaendepinsel.Pipette(e.Hoehen, O(41, 0)));
            Pruefe.Gleich(0.0, Gelaendepinsel.Pipette(new Hoehenraster(), O(41, 0)));
            var vorher = e.ZuJson();
            Pruefe.Wahr(e.GelaendePinseln(new Pinsel(Pinselart.Ebnen, Radius: 10, Staerke: 1, Kante: 0, Zielhoehe: 3.456), new[] { O(40, 0) }), "geebnet");
            Pruefe.Gleich("Gelände auf 3,46 m geebnet", e.Geschichte.RueckgaengigText);
            for (var x = 32; x <= 48; x += 2) Pruefe.Gleich(346, Cm(e, x, 0), $"x = {x}");
            Pruefe.Gleich(60 * 26, Cm(e, 52, 0), "draußen bleibt es");
            var nachher = e.ZuJson();
            e.Rueckgaengig();
            Pruefe.Gleich(vorher, e.ZuJson(), "Rückgängig: Byte für Byte wie vorher");
            e.Wiederholen();
            Pruefe.Gleich(nachher, e.ZuJson(), "Wiederholen: Byte für Byte wie nachher");
            Pruefe.Gleich(60.0, new Pinsel(Pinselart.Ebnen, Zielhoehe: 99).Begrenzt().Zielhoehe);
            Pruefe.Gleich(-20.0, new Pinsel(Pinselart.Ebnen, Zielhoehe: -25).Begrenzt().Zielhoehe);
            var f = Leer();
            f.GelaendePinseln(new Pinsel(Pinselart.Ebnen, Radius: 5, Staerke: 1, Kante: 0, Zielhoehe: 99), new[] { O(0, 0) });
            Pruefe.Gleich(6000, Cm(f, 0, 0));
            // halbe Stärke: halb zur Zielhöhe
            var g = Leer();
            g.GelaendePinseln(new Pinsel(Pinselart.Ebnen, Radius: 5, Staerke: 0.5, Kante: 0, Zielhoehe: 2), new[] { O(0, 0) });
            Pruefe.Gleich(100, Cm(g, 0, 0));
        }),

        ("Terrassen: Stufen bei Bezugshöhe + k · Stufenhöhe, harte und weiche Böschung, nie mehr als eine halbe Stufe; Stufe() ohne Sprung", () =>
        {
            var hart = new Pinsel(Pinselart.Terrassen, Radius: 20, Staerke: 1, Kante: 0, Stufenhoehe: 2, Boeschung: 0);
            Pruefe.Gleich(0.0, hart.Stufe(99));
            Pruefe.Gleich(200.0, hart.Stufe(100));
            Pruefe.Gleich(0.0, hart.Stufe(-99));
            Pruefe.Gleich(-200.0, hart.Stufe(-101));
            Pruefe.Gleich(250.0, (hart with { Zielhoehe = 0.5 }).Stufe(260));
            var weich = hart with { Boeschung = 0.5 };
            double alt = weich.Stufe(-1000);
            for (var cm = -1000; cm <= 1000; cm++)
            {
                var s = weich.Stufe(cm);
                Pruefe.Wahr(s >= alt && s - alt <= 1.5 / 0.5 + 1e-9, $"ohne Sprung bei {cm}: {alt} → {s}");
                alt = s;
            }
            var e = HangMitEbene();
            e.GelaendePinseln(hart, new[] { O(50, 0) });
            Pruefe.Gleich("Terrassen angelegt", e.Geschichte.RueckgaengigText);
            for (var i = 16; i <= 34; i++)
            {
                var h = e.Hoehen.Wert(i, 0);
                Pruefe.Gleich(0, h % 200, $"Punkt {i}: {h} auf einer Stufe");
                Pruefe.Wahr(Math.Abs(h - 60 * i) <= 100, $"Punkt {i}: höchstens eine halbe Stufe ({60 * i} → {h})");
            }
            var b = HangMitEbene();
            b.GelaendePinseln(hart with { Zielhoehe = 0.5 }, new[] { O(50, 0) });
            for (var i = 16; i <= 34; i++) Pruefe.Gleich(0, ((b.Hoehen.Wert(i, 0) - 50) % 200 + 200) % 200, $"Bezugshöhe 0,5 m, Punkt {i}: {b.Hoehen.Wert(i, 0)}");
            var c = HangMitEbene();
            c.GelaendePinseln(weich with { Stufenhoehe = 3 }, new[] { O(50, 0) });
            var reihe = Enumerable.Range(16, 19).Select(i => (int)c.Hoehen.Wert(i, 0)).ToList();
            Pruefe.Wahr(reihe.Zip(reihe.Skip(1)).All(p => p.Second >= p.First), "weiche Böschung: steigt weiter an " + string.Join(" ", reihe));
            Pruefe.Wahr(reihe.Count(h => h % 300 == 0) >= 6, "flache Stufen dazwischen: " + string.Join(" ", reihe));
        }),

        ("Gleicher Strich, gleiche Bytes – für jeden neuen Pinsel und Filter; Rückgängig und Wiederholen Byte für Byte", () =>
        {
            Kartenentwurf Vorbereitet()
            {
                var e = HangMitEbene();
                e.GelaendePinseln(new Pinsel(Pinselart.Rauschen, Radius: 60, Staerke: 1.5, Kante: 0.4, Startwert: 3, Massstab: 12), new[] { O(20, 0), O(60, 10) });
                e.BodenMalen(Malen(Bodentextur.Sand, 14, kante: 0.6), new[] { O(10, -10), O(40, 10) });
                return e;
            }
            var strich = new[] { O(-30, -12), O(25, 4), O(70, -8.5) };
            var filter = new Pinselfilter(SteigungAb: 10, SteigungBis: 40, Boden: Bodentextur.Sand, BodenRand: 0.4);
            var pinsel = new (Action<Kartenentwurf> Strich, string Name)[]
            {
                (e => e.GelaendePinseln(new Pinsel(Pinselart.Terrassen, 25, 0.8, 0.5, Stufenhoehe: 1.5, Boeschung: 0.4, Zielhoehe: 0.3), strich), "Terrassen"),
                (e => e.GelaendePinseln(new Pinsel(Pinselart.Ebnen, 20, 0.7, 0.5, Zielhoehe: 7.25), strich), "Ebnen mit Zielhöhe"),
                (e => e.GelaendePinseln(new Pinsel(Pinselart.Heben, 18, 0.4, 0.5, NurAusgleich: true, Filter: new Pinselfilter(SteigungBis: 35)), strich), "Mulden mit Steigungsfilter"),
                (e => e.GelaendePinseln(new Pinsel(Pinselart.Senken, 18, 0.4, 0.5, NurAusgleich: true, Filter: filter), strich), "Spitzen mit Steigungs- und Bodenfilter"),
                (e => e.GelaendePinseln(new Pinsel(Pinselart.Glaetten, 22, 0.6, 0.5, Filter: filter), strich), "Glätten mit Filter"),
                (e => e.BodenMalen(new Bodenpinsel(Bodentextur.Moos, 16, 0.7, 0.5, Filter: filter), strich), "Moos mit Filter"),
                (e => e.BodenMalen(Bodenpinsel.Radierer(12, 0.5, new Pinselfilter(SteigungAb: 25)), strich), "Radierer am Hang"),
            };
            foreach (var (malen, name) in pinsel)
            {
                var (a, b) = (Vorbereitet(), Vorbereitet());
                var vorher = a.ZuJson();
                malen(a);
                malen(b);
                var nachher = a.ZuJson();
                Pruefe.Wahr(nachher != vorher, $"{name}: ändert etwas");
                Pruefe.Gleich(nachher, b.ZuJson(), $"{name}: gleicher Strich, gleiche Bytes");
                a.Rueckgaengig();
                Pruefe.Gleich(vorher, a.ZuJson(), $"{name}: Rückgängig Byte für Byte");
                a.Wiederholen();
                Pruefe.Gleich(nachher, a.ZuJson(), $"{name}: Wiederholen Byte für Byte");
            }
        }),

        ("Ohne die neuen Angaben rechnen die Pinsel wie vorher (leerer Filter, „nur Mulden“ an Glätten und Rauschen ohne Wirkung)", () =>
        {
            var strich = new[] { O(-30, -12), O(25, 4), O(70, -8.5) };
            foreach (var art in new[] { Pinselart.Heben, Pinselart.Senken, Pinselart.Glaetten, Pinselart.Ebnen, Pinselart.Rauschen })
            {
                var p = new Pinsel(art, 20, art is Pinselart.Glaetten or Pinselart.Ebnen ? 0.7 : 0.4, 0.5, 5, 15);
                var (a, b, c) = (HangMitEbene(), HangMitEbene(), HangMitEbene());
                a.GelaendePinseln(p, strich);
                b.GelaendePinseln(p with { Filter = Pinselfilter.Keiner }, strich);
                Pruefe.Gleich(a.ZuJson(), b.ZuJson(), $"{art}: leerer Filter");
                if (art is Pinselart.Heben or Pinselart.Senken) continue;
                c.GelaendePinseln(p with { NurAusgleich = true, Stufenhoehe = 5, Boeschung = 1 }, strich);
                Pruefe.Gleich(a.ZuJson(), c.ZuJson(), $"{art}: Angaben anderer Pinsel ohne Wirkung");
            }
            var (d, f) = (HangMitEbene(), HangMitEbene());
            d.BodenMalen(new Bodenpinsel(Bodentextur.Erde, 12, 0.6, 0.5), strich);
            f.BodenMalen(new Bodenpinsel(Bodentextur.Erde, 12, 0.6, 0.5, Filter: new Pinselfilter()), strich);
            Pruefe.Gleich(d.ZuJson(), f.ZuJson(), "Texturpinsel: leerer Filter");
        }),

        ("Anzeige unter dem Pinsel: die Anteile am nächsten Bodenpunkt, stärkste zuerst, zusammen 255; Wiese allein auf leerem Boden", () =>
        {
            var e = Leer();
            Pruefe.Gleich("Wiese 255", string.Join(", ", e.Boden.Anteile(3, 4).Select(a => $"{a.Textur} {a.Anteil}")));
            e.BodenMalen(new Bodenpinsel(Bodentextur.Erde, 5, 0.5, 0, Deko: false), new[] { O(0, 0) });
            Pruefe.Gleich("Erde 128, Wiese 127", string.Join(", ", e.Boden.Anteile(0.4, -0.3).Select(a => $"{a.Textur} {a.Anteil}")));
            e.BodenMalen(new Bodenpinsel(Bodentextur.Sand, 5, 1, 0, Deko: false), new[] { O(0, 0) });
            Pruefe.Gleich("Sand 255", string.Join(", ", e.Boden.Anteile(0, 0).Select(a => $"{a.Textur} {a.Anteil}")));
            Pruefe.Gleich(e.Boden.Textur(0, 0), e.Boden.Anteile(0, 0)[0].Textur, "die erste ist die Bodenart-Pipette");
        }),
    };
}
