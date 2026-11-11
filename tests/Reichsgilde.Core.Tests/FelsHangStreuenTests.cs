using Reichsgilde.Kartenbau;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

/// <summary>
/// Felspinsel P4b: „Hang mitformen“ als Schicht beim Streuen (die gestreuten Wände tragen „hang“, Versatz gleicht den Boden aus) und die Hang-Vorschau
/// für noch nicht gesetzte Stücke (Geister des Stempels). Jeder Test mit Gegenprobe.
/// </summary>
static class FelsHangStreuenTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenort O(double x, double z) => new(x, z);

    static Kartenentwurf Huegel()
    {
        var e = Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);
        e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 40, Staerke: 12, Kante: 0.5), new[] { O(20, 0) });
        return e;
    }

    static string R(Hoehenraster r) => r.AlsJson()?.ToJsonString() ?? "";

    static Felspinsel Streu(bool hang, bool schicht) =>
        new(Formen: new[] { "wand_gross_1" }, Radius: 20, Dichte: 0.25, Streuung: 0.2, Boden: false, HangFormen: hang, HangSchicht: schicht);

    static readonly Kartenort[] Strich = { O(-50, 0), O(-30, 10), O(-10, 4) };

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Fels P4b: Streuen mit Hang als Schicht setzt „hang“ an den Wänden, hebt das Gelände, gleicht den Versatz aus und ist ein Schritt für Rückgängig", () =>
        {
            var e = Huegel();
            var dateiVorher = e.ZuJson();
            var hoeheVorher = R(e.Hoehen);
            Pruefe.Wahr(e.FelsStreuen(Streu(true, true), Strich, startwert: 5), "gestreut");
            var wand = e.Felsen.Where(f => Felsschicht.HatHang(f.Form)).ToList();
            Pruefe.Wahr(wand.Count > 1 && wand.All(f => f.Hang), $"{wand.Count} Wände mit hang");
            Pruefe.Wahr(e.HatHangschicht && e.HangschichtPunkte > 100, $"{e.HangschichtPunkte} Punkte gehoben");
            Pruefe.Wahr(R(e.Hoehen) != R(e.Grundhoehen), "wirksame Höhe ≠ Grundhöhe");
            // Versatz gleicht den Boden unter der Mitte aus: Unterkante bleibt am Boden (nicht in der Luft, nicht tiefer als das Einsinken)
            Pruefe.Gleich(0, e.FelsSchwebende(0.25).Count, "nichts schwebt");
            e.Rueckgaengig();
            Pruefe.Gleich(0, e.Felsen.Count, "Rückgängig: keine Stücke");
            Pruefe.Gleich(hoeheVorher, R(e.Hoehen), "Rückgängig: das Gelände wie vorher");
            Pruefe.Gleich(dateiVorher, e.ZuJson(), "Rückgängig: dieselbe Datei");
            // Gegenprobe: ohne Schicht (oder ohne Häkchen) streut der Strich keinen Hang
            foreach (var (hang, schicht) in new[] { (true, false), (false, true), (false, false) })
            {
                var g = Huegel();
                g.FelsStreuen(Streu(hang, schicht), Strich, startwert: 5);
                Pruefe.Wahr(g.Felsen.Count > 1 && g.Felsen.All(f => !f.Hang) && !g.HatHangschicht, $"ohne Schicht kein hang ({hang}, {schicht})");
            }
        }),

        ("Fels P4b: Gestreute Wände mit Hang folgen dem Löschen – die Grundhöhe stellt sich genau her; zweimal gestreut ist dieselbe Datei", () =>
        {
            var a = Huegel();
            var b = Huegel();
            a.FelsStreuen(Streu(true, true), Strich, startwert: 5);
            b.FelsStreuen(Streu(true, true), Strich, startwert: 5);
            Pruefe.Gleich(a.ZuJson(), b.ZuJson(), "deterministisch");
            Pruefe.Gleich(R(a.Hoehen), R(b.Hoehen), "dieselbe wirksame Höhe");
            var grund = R(a.Grundhoehen);
            a.FelsenLoeschen(a.Felsen.ToList());
            Pruefe.Gleich(grund, R(a.Hoehen), "ohne Stücke gilt die Grundhöhe (kein Wall bleibt)");
            // Wiederholen und noch einmal Rückgängig bleiben stimmig
            a.Rueckgaengig();
            Pruefe.Gleich(R(b.Hoehen), R(a.Hoehen), "Rückgängig des Löschens stellt den Hang wieder her");
        }),

        ("Fels P4b: Die Hang-Vorschau für neue Stücke zeigt, was der Stempel hebt – ohne etwas zu ändern; ohne Hang leer", () =>
        {
            var e = Huegel();
            var pinsel = new Felspinsel(Formen: new[] { "wand_gross_1" }, Streuung: 0, Boden: false, HangFormen: true, HangSchicht: true);
            var geister = e.FelsStempelVorschau(pinsel, O(-50, 0), drehung: 0, startwert: 1);
            Pruefe.Wahr(geister.Count == 1, "ein Geist");
            var vorher = R(e.Hoehen);
            var lagen = geister.Select(l => l with { Hang = Felsschicht.HatHang(l.Form) }).ToList();
            var punkte = e.FelsHangVorschau(Array.Empty<(EntwurfFels, Felslage)>(), lagen);
            Pruefe.Wahr(punkte.Count > 100, $"{punkte.Count} Punkte würden steigen");
            Pruefe.Gleich(vorher, R(e.Hoehen), "nichts geändert");
            e.FelsStempeln(pinsel, O(-50, 0), drehung: 0, startwert: 1);
            // der Stempel hebt dieselben Punkte (der Ausgleich des Versatzes ändert den Hang höchstens um einen Zentimeter)
            var abweichung = punkte.Max(p => Math.Abs(p.Hoehe - e.Hoehen.Wert(p.I, p.J) / 100.0));
            Pruefe.Wahr(abweichung <= 0.011, $"Vorschau und Stempel stimmen (Abweichung {abweichung:0.000} m)");
            // Gegenprobe: ohne „hang“ in der Lage nichts
            var l = Huegel();
            Pruefe.Gleich(0, l.FelsHangVorschau(Array.Empty<(EntwurfFels, Felslage)>(), geister).Count, "Geister ohne hang: leer");
        }),

        ("Fels P4b: Die Schicht rechnet nur nach, was sich geändert hat – nach jedem Schritt Zahl für Zahl wie ganz gerechnet (Stadtkarte aus der Datei); Gegenprobe: ganz nur beim Öffnen", () =>
        {
            var e = Huegel();
            var wand = new Felspinsel(Formen: new[] { "wand_gross_1" }, Streuung: 0, Boden: false, HangFormen: true, HangSchicht: true);
            var schritte = new List<(string, Action)>();
            for (var k = 0; k < 12; k++)
            {
                var (x, z, d) = (-150 + (k % 4) * 30.0, -60 + (k / 4) * 22.0, k * 37 % 360);
                schritte.Add(($"Stempel {k}", () => e.FelsStempeln(wand, O(x, z), drehung: d, startwert: k + 1)));
            }
            schritte.Add(("Wand hinter Wand", () => e.FelsStempeln(wand, O(-150, -74), drehung: 0, startwert: 3)));
            schritte.Add(("Streuen", () => e.FelsStreuen(Streu(true, true), Strich, startwert: 5)));
            schritte.Add(("Verschieben", () => e.FelsVerschieben(e.Felsen[2], e.Felsen[2].X + 9, e.Felsen[2].Z - 4)));
            schritte.Add(("Drehen", () => e.FelsDrehen(e.Felsen[5], 200)));
            schritte.Add(("Höher", () => e.FelsTransformieren(new[] { e.Felsen[6] }, new Felswerte(Versatz: 2.5))));
            schritte.Add(("Hang aus", () => e.FelsHangSetzen(new[] { e.Felsen[7] }, an: false)));
            schritte.Add(("Gelände unter Mitten", () => e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 14, Staerke: 2, Kante: 0.5), new[] { O(-120, -60), O(-90, -38) })));
            schritte.Add(("Gelände weit weg", () => e.GelaendePinseln(new Pinsel(Pinselart.Senken, Radius: 6, Staerke: 1, Kante: 0.5), new[] { O(150, 120) })));
            schritte.Add(("Löschen", () => e.FelsenLoeschen(new[] { e.Felsen[0], e.Felsen[9] })));
            schritte.Add(("Rückgängig", () => e.Rueckgaengig()));
            schritte.Add(("Rückgängig 2", () => e.Rueckgaengig()));
            schritte.Add(("Wiederholen", () => e.Wiederholen()));
            foreach (var (name, schritt) in schritte)
            {
                schritt();
                var ganz = Stadtkarte.AusJson(e.ZuJson(), Katalog.Value);
                Pruefe.Gleich(R(ganz.Hoehen!), R(e.Hoehen), $"nach „{name}“ wie ganz gerechnet");
            }
            Pruefe.Wahr(e.Felsen.Count(f => f.Hang) > 12, "viele Stücke mit hang");
            Pruefe.Gleich(1, e.SchichtGanzGerechnet, "ganz nur einmal (beim ersten Stück)");
            Pruefe.Wahr(e.SchichtTeilGerechnet > 20, $"sonst nur Gebiete ({e.SchichtTeilGerechnet})");
            // Gegenprobe: geöffnet wird ganz gerechnet
            var offen = Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value);
            Pruefe.Gleich(1, offen.SchichtGanzGerechnet, "geöffnet: ganz");
            Pruefe.Gleich(R(e.Hoehen), R(offen.Hoehen), "dieselbe Höhe");
        }),

        ("Fels P4b: Senken unter der Mitte großer Wände – Teilrechnung wie ganz; unter der vorderen ändern sich beide Hänge, unter der hinteren (ihr Fuß ist der Hang der vorderen) nichts außerhalb", () =>
        {
            var e = Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);
            Felspinsel P(string form, double groesse) => new(Formen: new[] { form }, Groesse: groesse, Streuung: 0, Boden: false, HangFormen: true, HangSchicht: true);
            var a = e.FelsStempeln(P("wand_gross_2", 1.5), O(0, 0), drehung: 0, startwert: 1)[0];
            var b = e.FelsStempeln(P("wand_gross_1", 1), O(0, -15), drehung: 0, startwert: 2)[0];
            e.FelsStempeln(P("wand_gross_3", 1.5), O(60, -10), drehung: 30, startwert: 3);
            Pruefe.Wahr(e.Hoehen.Hoehe(b.X, b.Z) - e.Grundhoehen.Hoehe(b.X, b.Z) > 5, "die Mitte der hinteren Wand liegt im Hang der vorderen");
            Pruefe.Gleich(R(Stadtkarte.AusJson(e.ZuJson(), Katalog.Value).Hoehen!), R(e.Hoehen), "vorher wie ganz");
            int Weit(Hoehenraster vorher, EntwurfFels w)
            {
                // Punkte, mehr als 8 m von der Mitte (außerhalb des Pinsels), die sich geändert haben
                var aus = 0;
                var (i0, j0, i1, j1) = Hoehenraster.Punkte(new Kartenrechteck(-120, -150, 120, 80));
                for (var j = j0; j <= j1; j++)
                    for (var i = i0; i <= i1; i++)
                    {
                        var (x, z) = (i * Hoehenraster.Abstand - w.X, j * Hoehenraster.Abstand - w.Z);
                        if (x * x + z * z > 64 && e.Hoehen.Wert(i, j) != vorher.Wert(i, j)) aus++;
                    }
                return aus;
            }
            // unter der hinteren: ihr Fuß ist der Hang der vorderen, er bleibt – außerhalb des Pinsels ändert sich nichts
            var stand = e.Hoehen.Kopie();
            var teil = e.SchichtTeilGerechnet;
            e.GelaendePinseln(new Pinsel(Pinselart.Senken, Radius: 3, Staerke: 2, Kante: 0), new[] { O(b.X, b.Z) });
            Pruefe.Gleich(R(Stadtkarte.AusJson(e.ZuJson(), Katalog.Value).Hoehen!), R(e.Hoehen), "unter der hinteren gesenkt: wie ganz");
            Pruefe.Wahr(e.SchichtTeilGerechnet > teil, "nur teilweise gerechnet");
            Pruefe.Gleich(0, Weit(stand, b), "unter der hinteren: außerhalb nichts");
            // Gegenprobe: unter der vorderen gesenkt sinken ihr Fuß und ihr Stand – ihr Hang und der Stand der hinteren ändern sich weit außerhalb
            stand = e.Hoehen.Kopie();
            var bVorher = Felsraum.UrsprungY(b.AlsLage(), e.Hoehen);
            e.GelaendePinseln(new Pinsel(Pinselart.Senken, Radius: 3, Staerke: 2, Kante: 0), new[] { O(a.X, a.Z) });
            Pruefe.Gleich(R(Stadtkarte.AusJson(e.ZuJson(), Katalog.Value).Hoehen!), R(e.Hoehen), "unter der vorderen gesenkt: wie ganz");
            Pruefe.Wahr(Weit(stand, a) > 10, $"unter der vorderen: außerhalb geändert ({Weit(stand, a)} Punkte)");
            Pruefe.Wahr(Felsraum.UrsprungY(b.AlsLage(), e.Hoehen) < bVorher - 0.5, "die hintere sinkt mit dem Hang der vorderen");
        }),

        ("Fels P4b: Zufällige große Wände, Gruppen, Größen – nach Verschieben und Senken unter der Mitte der letzten Wand Teilrechnung wie ganz (60 Fälle)", () =>
        {
            var formen = Felsformen.Alle.Where(f => f.HangFormt).Select(f => f.Kennung).ToList();
            var rnd = new Random(7);
            var gehoben = 0;
            for (var fall = 0; fall < 60; fall++)
            {
                var e = Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);
                if (fall % 2 == 1) e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 40, Staerke: 12, Kante: 0.5), new[] { O(20, 0) });
                var anzahl = 1 + rnd.Next(4);
                for (var s = 0; s < anzahl; s++)
                {
                    var gruppe = rnd.Next(3) == 0 ? "wandgross" : null;
                    var gr = new[] { 0.5, 1, 2, 3, 4 }[rnd.Next(5)];
                    var p = new Felspinsel(Formen: gruppe is null ? new[] { formen[rnd.Next(formen.Count)] } : null, Gruppe: gruppe, Groesse: gr, Streuung: 0, Boden: false,
                        HangFormen: true, HangSchicht: true, AchseZ: rnd.Next(2) == 0 ? null : new Felsbereich(0.25, 0.25), AchseY: rnd.Next(2) == 0 ? null : new Felsbereich(3, 3));
                    e.FelsStempeln(p, O(rnd.Next(-60, 60), rnd.Next(-40, 40)), drehung: rnd.Next(0, 360), startwert: s + 1);
                }
                if (e.Felsen.Count == 0) continue;
                e.FelsVerschieben(e.Felsen[0], e.Felsen[0].X + 7, e.Felsen[0].Z + 3);
                Pruefe.Gleich(R(Stadtkarte.AusJson(e.ZuJson(), Katalog.Value).Hoehen!), R(e.Hoehen), $"Fall {fall}: verschoben");
                var l = e.Felsen[^1];
                if (e.Hoehen.Hoehe(l.X, l.Z) - e.Grundhoehen.Hoehe(l.X, l.Z) > 0.1) gehoben++;
                e.GelaendePinseln(new Pinsel(Pinselart.Senken, Radius: 6, Staerke: 2, Kante: 0), new[] { O(l.X, l.Z) });
                Pruefe.Gleich(R(Stadtkarte.AusJson(e.ZuJson(), Katalog.Value).Hoehen!), R(e.Hoehen), $"Fall {fall}: unter der Mitte gesenkt");
            }
            Pruefe.Wahr(gehoben >= 3, $"in {gehoben} Fällen lag die Mitte der letzten Wand im Hang (der Fall, der vorher abwich)");
        }),

        ("Fels P4b: Zwei Wände Rücken an Rücken (4 m) – die Mitte heben nur Stücke davor: endliche Höhe in einem Durchgang, wie fest gestempelt; Gegenprobe ohne Reihenfolge bis 60 m", () =>
        {
            Kartenentwurf Zwei(bool schicht)
            {
                var e = Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);
                var w = new Felspinsel(Formen: new[] { "wand_gross_1" }, Streuung: 0, Boden: false, HangFormen: true, HangSchicht: schicht);
                e.FelsStempeln(w, O(0, 0), drehung: 0, startwert: 1);
                e.FelsStempeln(w, O(0, -4), drehung: 180, startwert: 1);
                return e;
            }
            var (s, f) = (Zwei(true), Zwei(false));
            var hoechste = 0;
            var (i0, j0, i1, j1) = Hoehenraster.Punkte(new Kartenrechteck(-60, -60, 60, 60));
            for (var j = j0; j <= j1; j++)
                for (var i = i0; i <= i1; i++) hoechste = Math.Max(hoechste, s.Hoehen.Wert(i, j));
            var oberkante = s.Felsen.Max(x => Felsraum.UrsprungY(x.AlsLage(), s.Hoehen) + Felsraum.Groesse(x.AlsLage()).Y);
            Pruefe.Wahr(hoechste / 100.0 < oberkante + 5 && hoechste < Hoehenraster.HoechsteCm - 100, $"endlich: höchster Punkt {hoechste / 100.0:0.00} m, Oberkante {oberkante:0.00} m");
            // wie fest gestempelt (dort hob der zweite Hang das erste Stück einmal mit, ohne Rückwirkung): außerhalb der Mittelzellen wenige cm
            // (an den weichen Seiten des zweiten Hangs mischt die Schicht als Höchstwert, fest gestempelt auf den ersten Hang: dort bis 0,6 m, wenige Punkte)
            var zellen = s.Felsen.SelectMany(x =>
            {
                var (a, b) = ((int)Math.Floor(x.X / Hoehenraster.Abstand), (int)Math.Floor(x.Z / Hoehenraster.Abstand));
                return new[] { (a, b), (a + 1, b), (a, b + 1), (a + 1, b + 1) };
            }).ToHashSet();
            var (abstand, anders) = (0, 0);
            for (var j = j0; j <= j1; j++)
                for (var i = i0; i <= i1; i++)
                {
                    if (zellen.Contains((i, j))) continue;
                    var d = Math.Abs(s.Hoehen.Wert(i, j) - f.Hoehen.Wert(i, j));
                    abstand = Math.Max(abstand, d);
                    if (d > 5) anders++;
                }
            Pruefe.Wahr(abstand <= 60 && anders <= 8, $"wie fest gestempelt außerhalb der Mittelzellen: höchstens {abstand} cm, {anders} Punkte über 5 cm");
            Pruefe.Wahr(Math.Abs(Felsraum.UrsprungY(s.Felsen[1].AlsLage(), s.Hoehen) - Felsraum.UrsprungY(f.Felsen[1].AlsLage(), f.Hoehen)) <= 0.03, "die zweite Wand steht wie fest gestempelt");
            Pruefe.Gleich(R(Stadtkarte.AusJson(s.ZuJson(), Katalog.Value).Hoehen!), R(s.Hoehen), "Entwurf wie ganz gerechnet");
            // Gegenprobe: ohne Reihenfolge heben sich beide gegenseitig bis an die Grenze des Rasters
            var lagen = s.Felsen.Select(x => x.AlsLage()).ToList();
            var (ohne, u) = Felsschicht.RechnenOhneReihenfolge(s.Grundhoehen, s.Kartengrenzen.Aussen, lagen);
            Pruefe.Wahr(ohne.Values.Max() >= Hoehenraster.HoechsteCm - 100, $"ohne Reihenfolge: {ohne.Values.Max() / 100.0:0.00} m");
            Pruefe.Wahr(u.All(x => x > oberkante), "ohne Reihenfolge: beide Ursprünge über der Oberkante mit Reihenfolge");
        }),

        ("Fels P4b: Das Kartenskript kennt --hang=schicht (Stempel und Streuen, Datei mit „hang“, Grundhöhe gespeichert); --hang=an bleibt der feste Hang", () =>
        {
            static string[] A(params string[] s) => s;
            var p = Reichsgilde.Kartenbau.Program.FelspinselVon("wandgross", A("--hang=schicht"))!;
            Pruefe.Wahr(p.HangFormen && p.HangSchicht, "schicht");
            var fest = Reichsgilde.Kartenbau.Program.FelspinselVon("wandgross", A("--hang=an"))!;
            Pruefe.Wahr(fest.HangFormen && !fest.HangSchicht, "an bleibt fest");
            Pruefe.Wahr(Reichsgilde.Kartenbau.Program.FelspinselVon("wandgross", A("--hang=quatsch")) is null, "unlesbar gilt nicht");
            Pruefe.Wahr(Reichsgilde.Kartenbau.Program.FelspinselVon("wandgross", A())!.HangSchicht == false, "Vorgabe aus");
            // der Lauf durch das Kartenskript: die Datei trägt „hang“, eine zweite Kopie mit --hang=an nicht
            var dateien = new List<string>();
            foreach (var schalter in new[] { "schicht", "an" })
            {
                var o = new Projektordner(Path.Combine(Path.GetTempPath(), "reichsgilde-felshang-" + Guid.NewGuid().ToString("N")));
                try
                {
                    Directory.CreateDirectory(o.Daten);
                    Directory.CreateDirectory(o.Karten);
                    foreach (var datei in new[] { "katalog.json", "wirtschaft.json" }) File.Copy(Path.Combine(Datenordner, datei), Path.Combine(o.Daten, datei));
                    File.Copy(Datenpfad.Karte("messstadt"), o.Karte("messstadt"));
                    var aus = new StringWriter();
                    var pinsel = Reichsgilde.Kartenbau.Program.FelspinselVon("wandgross@trachyt", A("--hang=" + schalter, "--boden=aus"))!;
                    Pruefe.Gleich(0, Reichsgilde.Kartenbau.Befehle.Felsen(o, "messstadt", true, pinsel, new[] { O(-450, 300) }, 30, 3, aus), aus.ToString());
                    dateien.Add(File.ReadAllText(o.Karte("messstadt")));
                }
                finally
                {
                    if (Directory.Exists(o.Wurzel)) Directory.Delete(o.Wurzel, recursive: true);
                }
            }
            Pruefe.Wahr(dateien[0].Contains("\"hang\""), "schicht: Feld hang in der Datei");
            Pruefe.Wahr(!dateien[1].Contains("\"hang\""), "an: kein Feld hang (fester Hang)");
            Pruefe.Wahr(dateien[0] != dateien[1], "die Karten unterscheiden sich im Gelände");
        }),
    };
}
