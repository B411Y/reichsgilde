using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

/// <summary>
/// Wasserverbund (Befund 1, Nutzer 09.11.2026; Vorgabe Teamleiter „C1“): Ein Lauf und ein See, die sich berühren (Lücke bis 4 m, Spiegel bis 0,25 m
/// auseinander), sind eine Wasserfläche – kein Grasstreifen dazwischen. Ein See ohne Wasserstand nimmt den Spiegel des Laufs an (Entscheidung
/// Teamleiter 09.11.2026). Ohne Verbund rechnet jeder Ort genau wie vorher.
/// </summary>
static class WasserverbundTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));
    static Kartenort O(double x, double z) => new(x, z);
    static Kartenentwurf Leer() => Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);

    /// <summary>Ein Fluss (12 m) entlang z = 0 von West nach Ost (bzw. von Ost nach West).</summary>
    static EntwurfFluss Fluss(Kartenentwurf e, bool nachWesten = false) =>
        e.FlussAnlegen(nachWesten ? new[] { O(150, 0), O(0, 0), O(-150, 0) } : new[] { O(-150, 0), O(0, 0), O(150, 0) }, "fluss", 12);

    /// <summary>Ein See nördlich des Flusses (x −60 … 60), dessen Südufer <paramref name="luecke"/> Meter vor dem Ufer des Flusses (z = −6) liegt.</summary>
    static EntwurfSee See(Kartenentwurf e, double luecke, double? wasserstand = null) =>
        e.SeeAnlegen(new[] { O(-60, -40), O(60, -40), O(60, -6 - luecke), O(-60, -6 - luecke) }, "see", null, wasserstand);

    /// <summary>Ein gleichmäßiger Hang nach Osten: Höhe = Gefälle · x (wie in den Wassertests).</summary>
    static void Hang(Kartenentwurf e, double gefaelle)
    {
        e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 1, Staerke: gefaelle * 100, Kante: 0), new[] { O(100, 0) });
        e.GelaendePinseln(new Pinsel(Pinselart.Senken, Radius: 1, Staerke: gefaelle * 100, Kante: 0), new[] { O(-100, 0) });
        e.Rampe(O(-100, 0), O(100, 0), breite: 400, kante: 0);
    }

    /// <summary>
    /// Ein fester Ablauf für den Gleichheitstest: Fluss mit See in 2 m Abstand (Wasserstand automatisch) und ein Teich mit festem Wasserstand an einem
    /// fallenden Bach, dazu Boden, Wasser, Spiegel, Strömung und Uferabstand auf einem Gitter – als Bits der Zahlen (unabhängig von der Ländereinstellung).
    /// </summary>
    internal static string Ablauf() => Ablauf(-0.45);

    /// <summary>Der Ablauf mit diesem festen Wasserstand des Teichs (Gegenprobe: ein anderer Wasserstand ergibt andere Bits).</summary>
    internal static string Ablauf(double teichStand)
    {
        var e = Leer();
        Hang(e, 0.004);
        Fluss(e, nachWesten: true);
        See(e, 2);
        e.FlussAnlegen(new[] { O(-140, 60), O(0, 70), O(140, 60) }, "bach", 3);
        e.SeeAnlegen(new[] { O(-30, 74), O(30, 74), O(30, 100), O(-30, 100) }, "teich", null, teichStand);
        var g = e.Gelaendeform;
        var w = g.Wasser;
        var text = new System.Text.StringBuilder();
        foreach (var v in w.Verbuende)
            text.Append($"{v.Lauf.Name}|{v.See.Name}|{string.Join(";", v.Stuecke.Select(s => $"{Bits(s.Von)},{Bits(s.Bis)}"))}|{Bits(v.See.Wasserstand)}\n");
        for (var z = -45.0; z <= 105; z += 1.5)
            for (var x = -80.0; x <= 80; x += 2.5)
            {
                var stelle = w.An(x, z);
                text.Append(Bits(w.Boden(x, z, 0.004 * x, false))).Append(' ').Append(Bits(w.Uferabstand(x, z))).Append(' ')
                    .Append(stelle is { } s ? $"{s.Nummer} {Bits(s.Spiegel)} {Bits(s.StroemungX)} {Bits(s.StroemungZ)} {Bits(s.Uferabstand)}" : "-")
                    .Append(' ').Append(Bits(w.Verbundanteil(x, z))).Append(' ').Append(w.ImUfer(x, z) ? 'u' : '-')
                    .Append(' ').Append(Bits(g.Hoehe(x, z))) // das Bett, wie die Gelaendeform es im Raster speichert (Zentimeter)
                    .Append(' ').Append(w.Ueberlappt(new[] { O(x - 1.5, z - 1), O(x + 1.5, z - 1), O(x + 1.5, z + 1), O(x - 1.5, z + 1) })?.Name ?? "-").Append('\n');
            }
        return text.ToString();
    }

    static string Bits(double d) => BitConverter.DoubleToInt64Bits(d).ToString("x16");

    /// <summary>
    /// Vorlauf V2 (für den Bericht): je Karte und Paar (Lauf, See) die kleinste Lücke, der Unterschied der Spiegel an den Berührstellen und die Verbünde.
    /// </summary>
    static string Vorlauf(string kennung)
    {
        var karte = Stadtkarte.AusJson(File.ReadAllText(Datenpfad.Karte(kennung)), Katalog.Value);
        var w = karte.Gelaendeform.Wasser;
        var zeilen = new List<string> { $"{kennung}: {w.Laeufe.Count} Läufe, {w.Seen.Count} Seen, {w.Verbuende.Count} Verbünde" };
        foreach (var s in w.Seen)
            foreach (var l in w.Laeufe)
            {
                var (luecke, dSpiegel) = (double.MaxValue, 0.0);
                foreach (var p in l.Punkte)
                {
                    var rand = s.Randabstand(p.Ort);
                    var g = (s.Enthaelt(p.Ort) ? -rand : rand) - p.Breite / 2;
                    if (g < luecke) (luecke, dSpiegel) = (g, p.Spiegel - s.Wasserstand);
                }
                zeilen.Add($"  {l.Name} – {s.Name}: kleinste Lücke {luecke:0.0} m, Spiegel dort {dSpiegel:+0.00;-0.00} m gegen den See " +
                           $"(Wasserstand {s.Wasserstand:0.00}, {(s.StandAutomatisch ? "automatisch" : "fest")})");
            }
        return string.Join("\n", zeilen);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Wasserverbund: Fluss 2 m neben dem See – der See nimmt den Spiegel des Flusses an, die Lücke ist Wasser ohne Strömung, das Bett liegt unter dem Spiegel", () =>
        {
            var e = Leer();
            Fluss(e);
            See(e, 2);
            var w = e.Gelaendeform.Wasser;
            Pruefe.Gleich(1, w.Verbuende.Count, "ein Verbund");
            var v = w.Verbuende[0];
            var spiegel = w.Laeufe[0].Punkte[0].Spiegel;
            Pruefe.Ungefaehr(-Wasserform.Freibord(12), spiegel, 1e-9);
            Pruefe.Ungefaehr(spiegel, v.See.Wasserstand, 1e-9);
            Pruefe.Wahr(v.See.StandAutomatisch, "bleibt automatisch");
            Pruefe.Wahr(v.Stuecke.Count == 1 && v.Stuecke[0].Von < 150 - 60 && v.Stuecke[0].Bis > 150 + 60, $"Stück um den See: {v.Stuecke[0]}");
            // die Lücke (z −8 … −6) ist Wasser, ihr Bett unter dem Spiegel, die Strömung dort 0
            foreach (var x in new[] { -50.0, 0, 37.3 })
            {
                Pruefe.Wahr(w.IstWasser(x, -7), $"Lücke bei x {x} ist Wasser");
                Pruefe.Wahr(w.Boden(x, -7, 0, false) < spiegel - 0.05, $"Bett der Lücke {w.Boden(x, -7, 0, false):0.00} unter dem Spiegel");
                Pruefe.Ungefaehr(0, w.An(x, -7)!.Value.Tempo, 1e-9);
                Pruefe.Ungefaehr(1, w.Verbundanteil(x, -7), 1e-9);
                Pruefe.Ungefaehr(0, w.Verbundanteil(x, 0), 1e-9);
                Pruefe.Ungefaehr(spiegel, w.Spiegel(x, -7)!.Value, 1e-9);
            }
            // in der Mitte des Flusses wie ohne See; am anderen Ufer auch
            var ohne = Leer();
            Fluss(ohne);
            var w0 = ohne.Gelaendeform.Wasser;
            foreach (var z in new[] { 0.0, 2, 5.5, 6.5, 7.5 })
            {
                Pruefe.Ungefaehr(w0.Boden(10, z, 0, false), w.Boden(10, z, 0, false), 1e-12);
                Pruefe.Ungefaehr(w0.An(10, z)?.Tempo ?? -1, w.An(10, z)?.Tempo ?? -1, 1e-12);
            }
            // weit weg vom Fluss bleibt der See ein See
            Pruefe.Ungefaehr(0, w.An(0, -35)!.Value.Tempo, 1e-12);
            Pruefe.Wahr(w.An(0, -35)!.Value.See is not null, "im See gilt der See");
        }),

        ("Wasserverbund: Lücke 10 m – kein Verbund; Boden, Wasser, Uferabstand auf einem 0,5-m-Gitter genau wie Fluss und See je allein", () =>
        {
            var (beide, fluss, see) = (Leer(), Leer(), Leer());
            Fluss(beide);
            See(beide, 10);
            Fluss(fluss);
            See(see, 10);
            var (w, wf, ws) = (beide.Gelaendeform.Wasser, fluss.Gelaendeform.Wasser, see.Gelaendeform.Wasser);
            Pruefe.Gleich(0, w.Verbuende.Count);
            Pruefe.Ungefaehr(ws.Seen[0].Wasserstand, w.Seen[0].Wasserstand, 0);
            for (var z = -45.0; z <= 12; z += 0.5)
                for (var x = -70.0; x <= 70; x += 3.5)
                {
                    Pruefe.Gleich(Math.Min(wf.Boden(x, z, 0, false), ws.Boden(x, z, 0, false)), w.Boden(x, z, 0, false), $"Boden bei {x}, {z}");
                    Pruefe.Gleich(wf.IstWasser(x, z) || ws.IstWasser(x, z), w.IstWasser(x, z), $"Wasser bei {x}, {z}");
                    Pruefe.Gleich(Math.Min(wf.Uferabstand(x, z), ws.Uferabstand(x, z)), w.Uferabstand(x, z), $"Uferabstand bei {x}, {z}");
                    Pruefe.Gleich(0.0, w.Verbundanteil(x, z));
                }
        }),

        ("Wasserverbund: Fester Wasserstand 1 m über dem Fluss – kein Verbund; 0,2 m – Verbund, der Wasserstand bleibt", () =>
        {
            var e = Leer();
            Fluss(e);
            See(e, 2, wasserstand: 0.1);
            Pruefe.Gleich(0, e.Gelaendeform.Wasser.Verbuende.Count, "1 m Unterschied");
            Pruefe.Ungefaehr(0.1, e.Gelaendeform.Wasser.Seen[0].Wasserstand, 0);
            var f = Leer();
            Fluss(f);
            See(f, 2, wasserstand: -0.7);
            Pruefe.Gleich(1, f.Gelaendeform.Wasser.Verbuende.Count, "0,2 m Unterschied");
            Pruefe.Ungefaehr(-0.7, f.Gelaendeform.Wasser.Seen[0].Wasserstand, 0);
            Pruefe.Wahr(f.Gelaendeform.Wasser.IstWasser(0, -7), "die Lücke ist Wasser");
        }),

        ("Wasserverbund: Ein Fluss endet im See – am Übergang durchgehend Wasser", () =>
        {
            var e = Leer();
            e.FlussAnlegen(new[] { O(-150, 0), O(-60, 0), O(0, 0) }, "fluss", 12);
            e.SeeAnlegen(new[] { O(-20, -30), O(60, -30), O(60, 30), O(-20, 30) }, "see");
            var w = e.Gelaendeform.Wasser;
            Pruefe.Gleich(1, w.Verbuende.Count);
            for (var x = -40.0; x <= 20; x += 0.25) Pruefe.Wahr(w.IstWasser(x, 0) && w.IstWasser(x, 4), $"Wasser bei x {x}");
            // das Ende des Flusses geht im See auf: tief im See ist der Anteil des Sees 1, im Lauf vor dem See 0
            Pruefe.Ungefaehr(1, w.Verbundanteil(-1, 0), 1e-9);
            Pruefe.Ungefaehr(0, w.Verbundanteil(-40, 0), 1e-9);
            // am Rand der Mündung bis an die Ecke des Sees keine Landzunge
            for (var x = -24.0; x <= -20; x += 0.25) Pruefe.Wahr(w.IstWasser(x, 6.5), $"Kehle bei x {x}");
        }),

        ("Wasserverbund: Fallender Spiegel – Verbund nur auf einem Stück, der Boden längs stetig (Sprung je 2 cm unter 5 cm)", () =>
        {
            var e = Leer();
            Hang(e, 0.01);
            Fluss(e, nachWesten: true);
            var spiegelMitte = e.Gelaendeform.Wasser.Laeufe[0].AnStelle(150).Spiegel;
            See(e, 2, wasserstand: Math.Round(spiegelMitte, 2));
            var w = e.Gelaendeform.Wasser;
            Pruefe.Gleich(1, w.Verbuende.Count);
            var v = w.Verbuende[0];
            var laenge = v.Stuecke.Sum(s => s.Bis - s.Von);
            Pruefe.Wahr(laenge > 30 && laenge < 110, $"Verbund auf {laenge:0} m (der See berührt den Fluss auf gut 130 m)");
            Pruefe.Wahr(w.IstWasser(0, -7), "Lücke in der Mitte Wasser");
            // wo der Spiegel 0,4 m vom Wasserstand abweicht, bleibt die Lücke Land
            var fern = w.Laeufe[0].Punkte.Where(q => Math.Abs(q.Ort.X) < 55 && Math.Abs(q.Spiegel - v.See.Wasserstand) > 0.4).ToList();
            Pruefe.Wahr(fern.Count > 0, "ein Stück mit großem Unterschied");
            Pruefe.Wahr(fern.All(q => !w.IstWasser(q.Ort.X, -7)), "dort ist die Lücke Land");
            foreach (var z in new[] { -7.0, -6.5, -4.0, -9.0 })
            {
                // fein abgetastet (2 cm): eine Böschung ist höchstens etwa 2 : 1 steil (unter 5 cm je Schritt), eine Stufe am Ende des Verbunds wäre größer
                var (groesster, wo) = (0.0, 0.0);
                var vorher = w.Boden(-70, z, -0.7, false);
                for (var n = 1; n <= 7000; n++)
                {
                    var x = -70 + 0.02 * n;
                    var b = w.Boden(x, z, 0.01 * x, false);
                    if (Math.Abs(b - vorher) > groesster) (groesster, wo) = (Math.Abs(b - vorher), x);
                    vorher = b;
                }
                Pruefe.Wahr(groesster < 0.05, $"z {z}: größter Sprung {groesster:0.000} m bei x {wo:0.00}");
            }
        }),

        ("Wasserverbund: Wasserstand über die Toleranz verschoben – an, dann aus; beide Zustände nur aus der Karte (zweimal gleich)", () =>
        {
            foreach (var (stand, verbund) in new[] { (-0.9, true), (-0.66, true), (-0.6, false), (-1.2, false) })
                for (var mal = 0; mal < 2; mal++)
                {
                    var e = Leer();
                    Fluss(e);
                    See(e, 2, wasserstand: stand);
                    Pruefe.Gleich(verbund, e.Gelaendeform.Wasser.Verbuende.Count == 1, $"Wasserstand {stand}");
                }
        }),

        ("Wasserverbund: Ein Geländestrich am Fluss senkt dessen Spiegel – der See folgt (Bett neu wie frisch gerechnet), die Anteile sind stetig", () =>
        {
            var e = Leer();
            Fluss(e);
            See(e, 2);
            Pruefe.Wahr(e.Gelaendeform.Hoehe(0, -30) < 0, "Bett des Sees vorher");
            var vorherBett = Enumerable.Range(0, 7).Select(i => e.Gelaendeform.Hoehe(-60 + 20 * i, -30)).ToList();
            e.GelaendePinseln(new Pinsel(Pinselart.Senken, Radius: 10, Staerke: 0.5, Kante: 0.5), new[] { O(-120, 0) });
            var w = e.Gelaendeform.Wasser;
            var frisch = Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value).Gelaendeform;
            Pruefe.Wahr(w.Seen[0].Wasserstand < -Wasserform.Freibord(12) - 0.05, $"der See folgt dem gesenkten Spiegel: {w.Seen[0].Wasserstand:0.00}");
            Pruefe.Ungefaehr(frisch.Wasser.Seen[0].Wasserstand, w.Seen[0].Wasserstand, 1e-12);
            for (var z = -40.0; z <= 8; z += 2)
                for (var x = -60.0; x <= 60; x += 10)
                    Pruefe.Ungefaehr(frisch.Hoehe(x, z), e.Gelaendeform.Hoehe(x, z), 1e-9);
            // Gegenprobe: Der Strich (60 m vom See entfernt) hat das Bett im See wirklich verändert – ohne das Nachrechnen des Sees in
            // Gelaendeform.Veraltet bliebe es auf dem alten Stand und der Vergleich oben schlüge fehl
            for (var i = 0; i < 7; i++)
                Pruefe.Wahr(e.Gelaendeform.Hoehe(-60 + 20 * i, -30) < vorherBett[i] - 0.3, $"Bett im See bei x {-60 + 20 * i} gesunken");
            // Anteil und Abstand je Verbund: stetig quer über Fluss, Ufer und Lücke
            Pruefe.Gleich(1, w.Verbuende.Count, "ein Verbund");
            var v = w.Verbuende[0];
            Pruefe.Wahr(v.AnteilAm(0, 8) is not null, $"Verbund wirkt bei 0, 8: Stücke {string.Join(", ", v.Stuecke)}, Bereich {v.Bereich}");
            var vorher = v.AnteilAm(0, 8)!.Value.Anteil;
            for (var z = 7.9; z >= -16; z -= 0.1)
            {
                var a = v.AnteilAm(0, z)!.Value.Anteil;
                Pruefe.Wahr(Math.Abs(a - vorher) < 0.12, $"Anteil springt bei z {z:0.0}: {vorher:0.00} → {a:0.00}");
                vorher = a;
            }
            Pruefe.Ungefaehr(0, v.AnteilAm(0, 0)!.Value.Anteil, 1e-9);
            Pruefe.Ungefaehr(1, v.AnteilAm(0, -7)!.Value.Anteil, 1e-9);
            Pruefe.Ungefaehr(1, v.AnteilAm(0, -7)!.Value.Gewicht, 0);
            Pruefe.Ungefaehr(w.Verbundabstand(0, -7)!.Value, v.AbstandAm(0, -7)!.Value, 0);
        }),

        ("Wasserverbund: Eine Schleife desselben Flusses neben dem Verbund (nah, entlang des Laufs weit weg) behält ihr Bett", () =>
        {
            // Der Fluss läuft am See entlang nach Osten, wendet und kommt 22 m südlich zurück: Die Rückkehr liegt im Bereich des Verbunds, aber gut 300 m weiter
            List<Kartenort> Schleife() => new() { O(-150, 0), O(0, 0), O(100, 0), O(125, 11), O(100, 22), O(0, 22), O(-150, 22) };
            var (mit, ohne) = (Leer(), Leer());
            mit.FlussAnlegen(Schleife(), "fluss", 12);
            ohne.FlussAnlegen(Schleife(), "fluss", 12);
            See(mit, 2, wasserstand: -0.9);
            var (w, w0) = (mit.Gelaendeform.Wasser, ohne.Gelaendeform.Wasser);
            Pruefe.Gleich(1, w.Verbuende.Count);
            Pruefe.Wahr(w.Verbuende[0].AbstandAm(0, 22) is not null, "der Verbund wirkt an der Rückkehr (sonst prüft der Test nichts)");
            foreach (var x in new[] { -40.0, 0, 30 })
                foreach (var z in new[] { 17.0, 20, 22, 25, 27 })
                {
                    Pruefe.Ungefaehr(w0.Boden(x, z, 0, false), w.Boden(x, z, 0, false), 1e-12);
                    Pruefe.Gleich(w0.IstWasser(x, z), w.IstWasser(x, z), $"Wasser bei {x}, {z}");
                }
            Pruefe.Wahr(w.Boden(0, 22, 0, false) < -1.5, "Bett der Rückkehr");
        }),

        ("Wasserverbund: Der Ablauf des Gleichheitstests erfasst Boden, Bett, Ufer und Überlappung – ein anderer Wasserstand gibt andere Bits (Gegenprobe)", () =>
        {
            var a = Ablauf();
            Pruefe.Wahr(a.Contains(" u ") && a.Contains("Fluss"), "ImUfer und Ueberlappt stehen im Ablauf");
            Pruefe.Wahr(a != Ablauf(-0.46), "1 cm Wasserstand ändert das Ergebnis");
        }),

        ("Wasserverbund: Spiegel und Strömung am Ende des Auslaufs ohne Sprung – auch wo der Verbund mitten am See endet (fallender Spiegel, fein abgetastet)", () =>
        {
            // wie „Fallender Spiegel“: der Verbund endet, wo Fluss und See 0,25 m auseinanderliegen – dort ist der See noch nah
            var e = Leer();
            Hang(e, 0.01);
            Fluss(e, nachWesten: true);
            See(e, 2, wasserstand: Math.Round(e.Gelaendeform.Wasser.Laeufe[0].AnStelle(150).Spiegel, 2));
            var w = e.Gelaendeform.Wasser;
            Pruefe.Gleich(1, w.Verbuende.Count);
            foreach (var z in new[] { -20.0, -12.0, -5.5, -5.0, -3.0, 0.0 })
            {
                var (vSp, vT) = (w.An(-60, z)!.Value.Spiegel, w.An(-60, z)!.Value.Tempo);
                for (var x = -59.95; x <= 60; x += 0.05)
                {
                    var st = w.An(x, z)!.Value;
                    Pruefe.Wahr(Math.Abs(st.Spiegel - vSp) < 0.004, $"Spiegel springt bei {x:0.00}, {z}: {vSp:0.000} → {st.Spiegel:0.000}");
                    Pruefe.Wahr(Math.Abs(st.Tempo - vT) < 0.02, $"Strömung springt bei {x:0.00}, {z}: {vT:0.000} → {st.Tempo:0.000}");
                    (vSp, vT) = (st.Spiegel, st.Tempo);
                }
            }
            // quer vom Fluss über die Lücke weit in den See (über die Grenze des Rasters, in dem der Verbund sucht): die Strömung geht stetig auf 0 – mitten
            // im Verbund (x 0) und dort, wo er nicht mehr wirkt (x −50). Im Auslauf dazwischen bleibt am Ufer des Flusses der Sprung des Flusses allein
            // (0,5 · Strömung am Ufer, dahinter Land), mit (1 − gV) gedämpft
            foreach (var x in new[] { 0.0, -50.0 })
            {
                double? vorher = null;
                for (var z = 5.0; z >= -39; z -= 0.01)
                {
                    var t = w.An(x, z)?.Tempo;
                    if (t is { } tt && vorher is { } vv) Pruefe.Wahr(Math.Abs(tt - vv) < 0.012, $"Strömung springt bei {x}, {z:0.00}: {vv:0.000} → {tt:0.000}");
                    vorher = t;
                }
                Pruefe.Ungefaehr(0, w.An(x, -30)!.Value.Tempo, 1e-12);
            }
        }),

        ("Wasserverbund: Lücke und altes Ufer liegen auf Seetiefe (kein flacher Schleier), das geschlossene Ufer und das andere Ufer bleiben", () =>
        {
            var e = Leer();
            Fluss(e);
            See(e, 2);
            var w = e.Gelaendeform.Wasser;
            var v = w.Verbuende[0];
            var spiegel = v.See.Wasserstand;
            foreach (var x in new[] { -30.0, 0, 25 })
                foreach (var z in new[] { -2.0, -5.0, -6.0, -7.0, -8.0, -10.0, -14.0 })
                {
                    var tiefe = spiegel - w.Boden(x, z, 0, false);
                    Pruefe.Wahr(tiefe >= 1.2, $"Tiefe bei {x}, {z}: {tiefe:0.00} m");
                    // Gegenprobe: die Böschung vom gemeinsamen Ufer (Stand c_v2) gäbe dort nur flaches Wasser – der neue Grund ist deutlich tiefer
                    if (z is -6.0 or -7.0 or -8.0)
                    {
                        var alt = Math.Min(v.See.Tiefe, -w.Verbundabstand(x, z)!.Value * Wasserform.Seeboeschung);
                        Pruefe.Wahr(alt < 0.7 && tiefe > alt + 0.5, $"bei {x}, {z}: vorher {alt:0.00} m, jetzt {tiefe:0.00} m");
                    }
                }
            // das geschlossene Ufer des Sees (die Seiten) fällt auch dort, wo der Verbund wirkt, wie bisher mit 30 % (Prüfer: die Orte müssen im Verbund liegen)
            var allein = Leer();
            See(allein, 2, wasserstand: spiegel);
            foreach (var (x, z) in new[] { (-59.0, -12.0), (-59.0, -16.0), (58.0, -14.0) })
            {
                Pruefe.Wahr(v.AnteilAm(x, z) is not null, $"der Verbund wirkt bei {x}, {z}");
                Pruefe.Ungefaehr(allein.Gelaendeform.Wasser.Boden(x, z, 0, false), w.Boden(x, z, 0, false), 1e-9);
            }
            Pruefe.Wahr(v.SeeuferAbstand(0, -7) > 25, $"das Südufer am Fluss zählt nicht als Ufer: {v.SeeuferAbstand(0, -7):0.0} m");
            Pruefe.Ungefaehr(1, v.SeeuferAbstand(0, -39), 1e-9);
        }),

        ("Wasserverbund: Ein tiefer See (8 m) – der Grund quer vom Fluss weit in den See ohne Stufe (1 cm fein), am Rand des Verbunds wie der See allein", () =>
        {
            var e = Leer();
            Fluss(e);
            e.SeeAnlegen(new[] { O(-60, -120), O(60, -120), O(60, -8), O(-60, -8) }, "see", null, null, tiefe: 8);
            var w = e.Gelaendeform.Wasser;
            Pruefe.Gleich(1, w.Verbuende.Count);
            var allein = Leer();
            allein.SeeAnlegen(new[] { O(-60, -120), O(60, -120), O(60, -8), O(-60, -8) }, "see", null, w.Seen[0].Wasserstand, tiefe: 8);
            foreach (var x in new[] { 0.0, -30.0, 41.3 })
            {
                var vorher = w.Boden(x, 0, 0, false);
                var groesster = (Sprung: 0.0, Z: 0.0);
                for (var n = 1; n <= 4500; n++)
                {
                    var z = -0.01 * n;
                    var b = w.Boden(x, z, 0, false);
                    if (Math.Abs(b - vorher) > groesster.Sprung) groesster = (Math.Abs(b - vorher), z);
                    vorher = b;
                }
                Pruefe.Wahr(groesster.Sprung < 0.03, $"x {x}: größter Sprung {groesster.Sprung:0.000} m bei z {groesster.Z:0.00} (Stand vor der Rückblendung: 3,8 m bei z −22)");
                // ab 16 m hinter dem Ufer des Flusses genau der See allein
                foreach (var z in new[] { -22.5, -25.0, -30.0 })
                    Pruefe.Ungefaehr(allein.Gelaendeform.Wasser.Boden(x, z, 0, false), w.Boden(x, z, 0, false), 1e-9);
                Pruefe.Wahr(w.Seen[0].Wasserstand - w.Boden(x, -9, 0, false) > 1.9, "am alten Ufer tief");
            }
        }),

        ("Wasserverbund: Ein Haus in der geschlossenen Lücke steht im Wasser", () =>
        {
            var e = Leer();
            Fluss(e);
            See(e, 3);
            var w = e.Gelaendeform.Wasser;
            Pruefe.Wahr(w.Ueberlappt(new[] { O(-2, -8.5), O(2, -8.5), O(2, -7.5), O(-2, -7.5) }) is not null, "Umriss in der Lücke");
            Pruefe.Wahr(w.Ueberlappt(new[] { O(-2, -60), O(2, -60), O(2, -50), O(-2, -50) }) is null, "Umriss an Land");
        }),

        ("Wasserverbund: Greybox und Köln ohne See bleiben ohne Verbund; Vorlauf V2 aller Karten im Protokoll", () =>
        {
            foreach (var kennung in new[] { "greybox", "koeln", "probestadt" })
            {
                var text = Vorlauf(kennung);
                Console.WriteLine("  [V2] " + text.Replace("\n", "\n  [V2] "));
                if (kennung != "probestadt")
                    Pruefe.Gleich(0, Stadtkarte.AusJson(File.ReadAllText(Datenpfad.Karte(kennung)), Katalog.Value).Gelaendeform.Wasser.Verbuende.Count, kennung);
            }
        }),

        ("Wasserverbund: Der feste Ablauf ergibt zweimal dasselbe", () => Pruefe.Gleich(Ablauf(), Ablauf())),
    };
}
