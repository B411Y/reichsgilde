using Reichsgilde;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Feste Geschwindigkeiten von Figuren und Karren (Darstellung, 31.10.2026; Modell in <c>game/scripts/Gehtempo.cs</c>, hier mitübersetzt):
/// Passanten gehen 1,3 m/s (±10 %) und kommen, wenn der Kern weniger Zeit gibt, später an. Karren fahren 0,8 bis 1,3 m/s – seit Datenversion 65
/// rechnet der Kern ihre Fahrzeit mit demselben Tempo (<c>FahrzeitWeglaengeTests</c>), und das Profil der Darstellung dauert genau die Kernzeit.
/// Dazu die sichtbare Fahrt der Karren (<see cref="Karrenzug"/>), die einen Rückstand auflöst.
/// </summary>
static class GehtempoTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static readonly string[] Arten = { "Handkarren", "Ochsenkarren", "Kriegskarren" };

    /// <summary>Ein ebener Weg von 100 m mit 1 m/s = 0,79 m je Spielminute (Rundendauer 15 min).</summary>
    static Fahrprofil Eben(double laenge = 100, double meterJeMinute = 1) =>
        new(new List<Karrentempo.Stueck> { new(1, 1) }, laenge, meterJeMinute);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Gehtempo: 1,3 m/s ±10 %; Alte, Bettler und Witwer 1,0; Boten 1,8; wer trägt, ist langsamer", () =>
        {
            Pruefe.Gleich(1.3f, Gehtempo.Grund("Schmied", false, false));
            Pruefe.Gleich(1.0f, Gehtempo.Grund("Schmied", true, false));
            Pruefe.Gleich(1.0f, Gehtempo.Grund("Bettler", false, false));
            Pruefe.Gleich(1.0f, Gehtempo.Grund("Witwer", false, false));
            Pruefe.Gleich(1.8f, Gehtempo.Grund("Bote", false, false));
            Pruefe.Gleich(1.8f, Gehtempo.Grund("Bote", false, true), "ein Bote trägt nur Briefe");
            Pruefe.Ungefaehr(1.3 * 0.9, Gehtempo.Grund("Bauer", false, true), 1e-6);
            Pruefe.Ungefaehr(1.17, Gehtempo.Mit(1.3f, 0), 1e-6);
            Pruefe.Ungefaehr(1.43, Gehtempo.Mit(1.3f, 1), 1e-6);
            Pruefe.Ungefaehr(1.3, Gehtempo.Mit(1.3f, 0.5f), 1e-6);
            for (var i = 0; i <= 100; i++)
            {
                var v = Gehtempo.Mit(Gehtempo.Grund("Bauer", false, false), i / 100f);
                Pruefe.Wahr(v >= 1.17f - 1e-5f && v <= 1.43f + 1e-5f, $"{v} liegt außerhalb ±10 %");
            }
        }),

        ("Gehtempo: Schlendern und Streife sind gemächlicher als das Gehen; Eilen höchstens das 1,3-fache", () =>
        {
            Pruefe.Wahr(Gehtempo.Schlendern < Gehtempo.Gehen && Gehtempo.Streife < Gehtempo.Gehen, "gemächlich");
            Pruefe.Gleich(1.3, Gehtempo.EilenHoechstens);
            // schneller als 1,8 × 1,1 × 1,3 = 2,6 m/s ist keine Figur, auch nicht in Eile
            Pruefe.Wahr(Gehtempo.Bote * (1 + Gehtempo.Streubreite) * Gehtempo.EilenHoechstens < 2.6, "Höchsttempo");
        }),

        ("Gehtempo.Planen: reicht die Zeit, kommt die Figur pünktlich; sonst geht sie früh los, eilt höchstens 1,3-fach und kommt später als der Kern", () =>
        {
            // 60 m bei 1 m je Minute = 60 Minuten; der Kern gibt 100: spät losgehen, pünktlich da
            var (s, e) = Gehtempo.Planen(60, 1, 1000, 1100);
            Pruefe.Ungefaehr(1040, s, 1e-9);
            Pruefe.Ungefaehr(1100, e, 1e-9);
            // der Kern gibt 50: 60/1,3 = 46 Minuten reichen, also kommt sie nach 50 an
            (s, e) = Gehtempo.Planen(60, 1, 1000, 1050);
            Pruefe.Ungefaehr(1000, s, 1e-9);
            Pruefe.Ungefaehr(1050, e, 1e-9);
            // der Kern gibt 10: sie braucht 60/1,3 = 46,15 Minuten – kommt später an als der Kern
            (s, e) = Gehtempo.Planen(60, 1, 1000, 1010);
            Pruefe.Ungefaehr(1000, s, 1e-9);
            Pruefe.Ungefaehr(1000 + 60 / 1.3, e, 1e-9);
            Pruefe.Wahr(e > 1010, "später als der Kern");
            // nie schneller als das 1,3-fache des eigenen Tempos
            foreach (var kern in new[] { 1.0, 5, 20, 40, 100 })
            {
                var (a, b) = Gehtempo.Planen(60, 1, 0, kern);
                Pruefe.Wahr(60 / (b - a) <= 1.3 + 1e-9, $"Kern {kern}: Tempo {60 / (b - a):0.00}");
            }
        }),

        ("Gehtempo.Weitergehen: die Figur geht sofort weiter und holt auf, ohne Wartezeit", () =>
        {
            var (s, e) = Gehtempo.Weitergehen(60, 1, 500, 800); // viel Zeit: im eigenen Tempo
            Pruefe.Gleich((500.0, 560.0), (s, e));
            (s, e) = Gehtempo.Weitergehen(60, 1, 500, 520); // wenig Zeit: 1,3-fach
            Pruefe.Ungefaehr(500, s, 1e-9);
            Pruefe.Ungefaehr(500 + 60 / 1.3, e, 1e-9);
        }),

        ("Karrentempo: Ochse 0,8, Pferd 1,3, Handkarren 1,0 m/s aus dem Kern; Geschwindigkeit = 1 ÷ Zeitfaktor des Kerns (der Kern begrenzt den Ausbau auf 15 %)", () =>
        {
            var karren = Daten.Value.Logistik.Karren;
            Pruefe.Gleich(0.8, karren["Ochsenkarren"].Tempo);
            Pruefe.Gleich(1.3, karren["Kriegskarren"].Tempo);
            Pruefe.Gleich(1.0, karren["Handkarren"].Tempo);
            Pruefe.Gleich(1.0, Karrentempo.Geschwindigkeitsfaktor(1));
            Pruefe.Ungefaehr(1 / 0.9, Karrentempo.Geschwindigkeitsfaktor(0.9), 1e-9);
            Pruefe.Ungefaehr(0.5, Karrentempo.Geschwindigkeitsfaktor(2), 1e-9);
            Pruefe.Ungefaehr(1.0 / 3, Karrentempo.Geschwindigkeitsfaktor(3), 1e-9);
            // der beste Belag (Pflaster) gibt im Kern den Zeitfaktor 1 ÷ 1,15 – schneller als 15 % wird kein Karren
            var wb = Daten.Value.Wegebau!;
            Pruefe.Ungefaehr(1.15, Karrentempo.Geschwindigkeitsfaktor(wb.Zeitfaktor(wb.TempoVon(Belagstufe.Pflaster))), 1e-9);
        }),

        ("Fahrprofil: Dauer = Länge ÷ Tempo; Weg und Zeit sind Umkehrungen; Fähre hält auf, Furt bremst", () =>
        {
            var p = Eben(100, 1);
            Pruefe.Ungefaehr(100, p.Dauer, 1e-9);
            Pruefe.Ungefaehr(0.25, p.WegBei(25), 1e-9);
            Pruefe.Ungefaehr(25, p.ZeitBei(0.25), 1e-9);
            Pruefe.Ungefaehr(1, p.MeterJeMinute(10), 1e-9);
            // halb eben, halb doppelt so langsam (Hang): 50 + 100 Minuten
            var hang = new Fahrprofil(new List<Karrentempo.Stueck> { new(0.5, 1), new(0.5, 0.5) }, 100, 1);
            Pruefe.Ungefaehr(150, hang.Dauer, 1e-9);
            Pruefe.Ungefaehr(0.5, hang.WegBei(50), 1e-9);
            Pruefe.Ungefaehr(0.5, hang.MeterJeMinute(100), 1e-9);
            Pruefe.Ungefaehr(1.0, hang.MeterJeMinute(10), 1e-9);
            // Fähre in der Mitte: 30 Minuten Stillstand, danach geht es weiter
            var faehre = new Fahrprofil(new List<Karrentempo.Stueck> { new(0.5, 1), new(0.5, 1, WartenMinuten: 30) }, 100, 1);
            Pruefe.Ungefaehr(130, faehre.Dauer, 1e-9);
            Pruefe.Ungefaehr(0.5, faehre.WegBei(65), 1e-9);
            Pruefe.Ungefaehr(0, faehre.MeterJeMinute(65), 1e-9);
            Pruefe.Ungefaehr(50, faehre.ZeitBei(0.5), 1e-9);
            // Furt: 5 Minuten mehr auf dem Stück
            var furt = new Fahrprofil(new List<Karrentempo.Stueck> { new(1, 1, ZusatzMinuten: 5) }, 100, 1);
            Pruefe.Ungefaehr(105, furt.Dauer, 1e-9);
        }),

        ("Karrentempo.Stuecke: ohne Stücke ein ebenes; Furt und Fähre halten nur beim Wechsel der Fahrbahn auf", () =>
        {
            var leer = Karrentempo.Stuecke(null, _ => 1, 5, 30);
            Pruefe.Gleich(1, leer.Count);
            Pruefe.Gleich(1.0, leer[0].Anteil);
            var fahrt = new List<Fahrtstueck>
            {
                new(40, 0), new(10, 0, Fahrbahn.Faehre), new(10, 0, Fahrbahn.Faehre), new(20, 0), new(20, 0, Fahrbahn.Furt),
            };
            var s = Karrentempo.Stuecke(fahrt, _ => 1, 5, 30);
            Pruefe.Gleich(5, s.Count);
            Pruefe.Ungefaehr(1.0, s.Sum(x => x.Anteil), 1e-9);
            Pruefe.Ungefaehr(30, s[1].WartenMinuten, 1e-9);
            Pruefe.Ungefaehr(0, s[2].WartenMinuten, 1e-9);
            Pruefe.Ungefaehr(5, s[4].ZusatzMinuten, 1e-9);
        }),

        ("Karrenzug: fährt eine Fahrt des Kerns in fester Geschwindigkeit, kommt später an und wartet am Markt auf den Kern", () =>
        {
            var hin = Eben(100, 1); // 100 Minuten
            var zurueck = Eben(100, 1);
            var z = new Karrenzug();
            z.Weiter(10, KarrenZustand.AmBetrieb, hin, zurueck);
            Pruefe.Gleich(Etappe.Hof, z.Etappe);
            Pruefe.Ungefaehr(0, z.Weg(hin, zurueck), 1e-9);
            // der Kern schickt den Karren los: er fährt (der Kern rechnet 30 Minuten, der Zug braucht 100)
            z.Weiter(0, KarrenZustand.ZumMarkt, hin, zurueck);
            Pruefe.Gleich(Etappe.Hin, z.Etappe);
            z.Weiter(30, KarrenZustand.ZumMarkt, hin, zurueck);
            Pruefe.Ungefaehr(0.3, z.Weg(hin, zurueck), 1e-9);
            Pruefe.Ungefaehr(1, z.MeterJeMinute(hin, zurueck), 1e-9);
            // der Kern ist längst zurück (AmBetrieb), der Zug fährt zu Ende, kehrt am Markt um und fährt nach Hause
            z.Weiter(70, KarrenZustand.AmBetrieb, hin, zurueck);
            Pruefe.Gleich(Etappe.Zurueck, z.Etappe);
            Pruefe.Ungefaehr(1, z.Weg(hin, zurueck), 1e-9);
            z.Weiter(50, KarrenZustand.AmBetrieb, hin, zurueck);
            Pruefe.Ungefaehr(0.5, z.Weg(hin, zurueck), 1e-9);
            z.Weiter(60, KarrenZustand.AmBetrieb, hin, zurueck);
            Pruefe.Gleich(Etappe.Hof, z.Etappe);
        }),

        ("Karrenzug: ist der Zug früher am Markt als der Kern, wartet er dort; kehrt um, sobald der Kern verkauft hat", () =>
        {
            var kurz = Eben(10, 1); // 10 Minuten
            var z = new Karrenzug();
            z.Weiter(0, KarrenZustand.ZumMarkt, kurz, kurz);
            z.Weiter(50, KarrenZustand.ZumMarkt, kurz, kurz);
            Pruefe.Gleich(Etappe.Markt, z.Etappe);
            Pruefe.Ungefaehr(1, z.Weg(kurz, kurz), 1e-9);
            Pruefe.Ungefaehr(0, z.MeterJeMinute(kurz, kurz), 1e-9);
            z.Weiter(10, KarrenZustand.ZumMarkt, kurz, kurz);
            Pruefe.Gleich(Etappe.Markt, z.Etappe);
            z.Weiter(3, KarrenZustand.ZumBetrieb, kurz, kurz);
            Pruefe.Gleich(Etappe.Zurueck, z.Etappe);
            Pruefe.Ungefaehr(0.7, z.Weg(kurz, kurz), 1e-9); // 3 von 10 Minuten zurück
        }),

        ("Karrenzug: verpasste Fahrten verfallen – höchstens eine bleibt offen", () =>
        {
            var hin = Eben(100, 1);
            var z = new Karrenzug();
            z.Weiter(0, KarrenZustand.ZumMarkt, hin, hin);
            Pruefe.Gleich(Etappe.Hin, z.Etappe);
            // der Kern fährt dreimal weiter, während der Zug noch unterwegs ist
            for (var i = 0; i < 3; i++)
            {
                z.Weiter(5, KarrenZustand.ZumBetrieb, hin, hin);
                z.Weiter(5, KarrenZustand.AmBetrieb, hin, hin);
                z.Weiter(5, KarrenZustand.ZumMarkt, hin, hin);
            }
            Pruefe.Wahr(z.FahrtOffen, "eine Fahrt wartet");
            // Zug fährt hin und zurück zu Ende, danach genau eine weitere Fahrt
            var fahrten = 0;
            for (var i = 0; i < 60; i++)
            {
                var offen = z.FahrtOffen;
                z.Weiter(10, KarrenZustand.AmBetrieb, hin, hin);
                if (offen && !z.FahrtOffen) fahrten++; // die offene Fahrt wurde angetreten
            }
            Pruefe.Gleich(1, fahrten, "genau eine weitere Fahrt, nicht drei");
            Pruefe.Gleich(Etappe.Hof, z.Etappe);
            Pruefe.Wahr(!z.FahrtOffen, "nichts mehr offen");
        }),

        ("Karrenzug.Abgleichen: setzt den Zug auf den Ort des Kerns (Betrieb, Hinweg, Rückweg) und löscht die offene Fahrt", () =>
        {
            var hin = Eben(100, 1);
            var zurueck = Eben(100, 2); // schneller zurück (bergab)
            var z = new Karrenzug();
            z.Abgleichen(KarrenZustand.ZumMarkt, 0.4, hin, zurueck);
            Pruefe.Gleich(Etappe.Hin, z.Etappe);
            Pruefe.Ungefaehr(0.4, z.Weg(hin, zurueck), 1e-9);
            z.Abgleichen(KarrenZustand.ZumBetrieb, 0.75, hin, zurueck); // 75 % vom Hof, also noch 25 % bis zum Markt zurückgelegt
            Pruefe.Gleich(Etappe.Zurueck, z.Etappe);
            Pruefe.Ungefaehr(0.75, z.Weg(hin, zurueck), 1e-9);
            z.Abgleichen(KarrenZustand.AmBetrieb, 0, hin, zurueck);
            Pruefe.Gleich(Etappe.Hof, z.Etappe);
            z.Abgleichen(KarrenZustand.ZumMarkt, 1, hin, zurueck);
            Pruefe.Gleich(Etappe.Markt, z.Etappe);
            Pruefe.Wahr(!z.FahrtOffen, "keine offene Fahrt");
        }),

        ("Karrenzug.Umstellen: ändert sich der Belag, bleibt der Karren an derselben Stelle des Weges", () =>
        {
            var altHin = Eben(100, 1);
            var altZurueck = Eben(100, 1);
            var neuHin = Eben(100, 1.15); // besserer Belag
            var neuZurueck = Eben(100, 1.15);
            var z = new Karrenzug();
            z.Abgleichen(KarrenZustand.ZumMarkt, 0.5, altHin, altZurueck);
            z.Umstellen(altHin, altZurueck, neuHin, neuZurueck);
            Pruefe.Ungefaehr(0.5, z.Weg(neuHin, neuZurueck), 1e-9);
        }),

        ("Fahrprofil ohne Kernzeit: fester Tempo auf ebenem Weg, kein Strecken – der Karren ist früher oder später da als der Kern und der Zug gleicht das aus", () =>
        {
            // Kern 27 min, Weg im Bild nur 15 m (Hof bis Pflasterrand): bei 0,79 m je Minute 19 min – früher da, wartet am Markt
            var kurz = Eben(15, 0.79);
            Pruefe.Ungefaehr(15 / 0.79, kurz.Dauer, 1e-9);
            for (var t = 0.0; t < kurz.Dauer; t += kurz.Dauer / 20)
                Pruefe.Ungefaehr(0.79, kurz.MeterJeMinute(t), 1e-9); // überall das feste Tempo
            var zug = new Karrenzug();
            zug.Weiter(0, KarrenZustand.ZumMarkt, kurz, kurz);
            zug.Weiter(25, KarrenZustand.ZumMarkt, kurz, kurz);
            Pruefe.Gleich(Etappe.Markt, zug.Etappe, "nach 25 Minuten am Markt, der Kern sagt noch „zum Markt“ (27 min)");
            zug.Weiter(5, KarrenZustand.ZumMarkt, kurz, kurz);
            Pruefe.Gleich(Etappe.Markt, zug.Etappe, "wartet auf den Kern");
            // Weg im Bild 40 m = 51 min bei Kern 27: später da – der Kern hat schon verkauft, der Karren kehrt am Markt sofort um
            var lang = Eben(40, 0.79);
            var spaet = new Karrenzug();
            spaet.Weiter(0, KarrenZustand.ZumMarkt, lang, lang);
            spaet.Weiter(30, KarrenZustand.ZumMarkt, lang, lang);
            Pruefe.Gleich(Etappe.Hin, spaet.Etappe);
            spaet.Weiter(25, KarrenZustand.ZumBetrieb, lang, lang);
            Pruefe.Wahr(spaet.Etappe == Etappe.Zurueck || spaet.Etappe == Etappe.Hof, $"kehrt sofort um, ist aber {spaet.Etappe}");
        }),

        ("Köln: Karren fahren zu verschiedenen Marktplätzen, jeder Betrieb zu einem Platz der Karte, mit der Fahrzeit des Kerns auf seinem Weg", () =>
        {
            var karte = Stadtkarte.AusDatei(Datenpfad.Karte("koeln"));
            var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, Daten.Value, new NeuePartie(), karte: karte);
            var w = sim.Wirtschaft!;
            Pruefe.Gleich(3, karte.Marktplaetze.Count, "Heumarkt, Alter Markt, Neumarkt");
            var plaetze = new HashSet<string>();
            foreach (var b in w.Betriebe.Where(b => w.Fahrten(b) is { Hin.Count: > 0 }))
            {
                var platz = w.MarktplatzVon(b);
                Pruefe.Wahr(platz is not null && karte.Marktplaetze.Contains(platz), $"{b.Name}: kein Marktplatz der Karte");
                plaetze.Add(platz!.Name);
                foreach (var typ in Arten)
                {
                    var fahrt = w.Fahrten(b)!.Value.Hin;
                    var jeMinute = w.Daten.Logistik.Karren[typ].MeterJeMinute;
                    var frei = new Fahrprofil(Karrentempo.Stuecke(fahrt, w.Zeitfaktor, w.Daten.Logistik.Wasser!.FurtMinuten, w.Daten.Logistik.Wasser.FaehreMinuten), fahrt.Sum(s => s.Laenge), jeMinute);
                    var grenzen = w.Daten.Logistik.Weglaenge!;
                    if (frei.Dauer >= grenzen.MindestMinuten && frei.Dauer <= grenzen.HoechstMinuten)
                        Pruefe.Wahr(Math.Abs(frei.Dauer - w.Fahrzeit(b, typ, true)) <= 0.5 + 1e-9, $"{b.Name}/{typ}: im festen Tempo {frei.Dauer:0.0} min, Kern {w.Fahrzeit(b, typ, true)} min");
                }
            }
            Pruefe.Wahr(plaetze.Count >= 2, $"alle Karren zu einem Platz ({string.Join(", ", plaetze)})");
        }),

        ("Karren auf Greybox und Köln: nie schneller als Grundtempo ×1,15, auch auf den Karrenstraßen der Karten", () =>
        {
            foreach (var kennung in new[] { "greybox", "koeln" })
            {
                var karte = Stadtkarte.AusDatei(Datenpfad.Karte(kennung));
                var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, Daten.Value, new NeuePartie(), karte: karte);
                var w = sim.Wirtschaft!;
                var netz = w.Karrennetz!;
                var m = karte.Gebaeude.First(g => g.Katalog == "markt");
                var markt = new Kartenort(m.Punkt[0], m.Punkt[1]);
                var gepruft = 0;
                foreach (var g in karte.Gebaeude.Where(g => g.Katalog == "werkstattplatz"))
                {
                    var fahrt = netz.Fahrt(new Kartenort(g.Punkt[0], g.Punkt[1]), markt);
                    var laenge = fahrt.Sum(s => s.Laenge);
                    var stuecke = Karrentempo.Stuecke(fahrt, w.Zeitfaktor, 5, 30);
                    foreach (var typ in new[] { "Handkarren", "Ochsenkarren", "Kriegskarren" })
                    {
                        var jeMinute = Daten.Value.Logistik.Karren[typ].Tempo * 900.0 / 1140;
                        var p = new Fahrprofil(stuecke, laenge, jeMinute);
                        Pruefe.Wahr(p.Dauer >= laenge / (jeMinute * 1.15) - 1e-6, $"{kennung}/{typ}: Dauer {p.Dauer:0.0} min zu kurz");
                        for (var t = 0.0; t <= p.Dauer; t += p.Dauer / 50)
                            Pruefe.Wahr(p.MeterJeMinute(t) <= jeMinute * 1.15 + 1e-6, $"{kennung}/{typ}: {p.MeterJeMinute(t):0.00} m/min bei {t:0}");
                        gepruft++;
                    }
                }
                Pruefe.Wahr(gepruft >= 3, $"{kennung}: zu wenige Werkstattplätze geprüft ({gepruft})");
            }
        }),

        ("Karren auf Greybox und Köln: Die Darstellung fährt im Tempo des Kerns und kommt genau mit dem Kern an (Fahrprofil mit der Kernzeit)", () =>
        {
            foreach (var kennung in new[] { "greybox", "koeln" })
            {
                var karte = Stadtkarte.AusDatei(Datenpfad.Karte(kennung));
                var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, Daten.Value, new NeuePartie(), karte: karte);
                var w = sim.Wirtschaft!;
                var gepruft = 0;
                var grenzen = w.Daten.Logistik.Weglaenge!;
                foreach (var b in w.Betriebe.Where(b => w.Fahrten(b) is { Hin.Count: > 0 }))
                    foreach (var typ in Arten)
                        foreach (var zumMarkt in new[] { true, false })
                        {
                            var fahrt = zumMarkt ? w.Fahrten(b)!.Value.Hin : w.Fahrten(b)!.Value.Zurueck;
                            var laenge = fahrt.Sum(s => s.Laenge);
                            var jeMinute = w.Daten.Logistik.Karren[typ].MeterJeMinute;
                            var stuecke = Karrentempo.Stuecke(fahrt, w.Zeitfaktor, w.Daten.Logistik.Wasser!.FurtMinuten, w.Daten.Logistik.Wasser.FaehreMinuten);
                            var kern = w.Fahrzeit(b, typ, zumMarkt);
                            Pruefe.Gleich(FahrzeitWeglaengeTests.NachFormel(w, b, typ, zumMarkt), kern, $"{kennung}/{b.Name}/{typ}");
                            // Auf dem Weg des Kerns fährt die Darstellung von sich aus so lange wie der Kern (bis auf die Rundung), außer an den Grenzen
                            var frei = new Fahrprofil(stuecke, laenge, jeMinute);
                            var begrenzt = frei.Dauer < grenzen.MindestMinuten || frei.Dauer > grenzen.HoechstMinuten;
                            if (!begrenzt)
                                Pruefe.Wahr(Math.Abs(frei.Dauer - kern) <= 0.5 + 1e-9, $"{kennung}/{b.Name}/{typ}: sichtbar {frei.Dauer:0.00}, Kern {kern}");
                            // nie schneller als das Tempo der Art × 1,15 (Ausbau), wo die Grenzen nicht eingreifen
                            for (var t = 0.0; !begrenzt && t <= frei.Dauer; t += frei.Dauer / 40)
                                Pruefe.Wahr(frei.MeterJeMinute(t) <= jeMinute * 1.15 + 1e-6, $"{kennung}/{typ}: {frei.MeterJeMinute(t):0.00} m/min bei {t:0}");
                            // Mit der Kernzeit (auch auf dem kürzeren Weg bis zum Rand des Platzes) dauert das Profil genau so lange wie die Fahrt im Kern
                            var gezeigt = new Fahrprofil(stuecke, Math.Max(1, laenge - 10), jeMinute, kern);
                            Pruefe.Ungefaehr(kern, gezeigt.Dauer, 1e-9);
                            Pruefe.Ungefaehr(1, gezeigt.WegBei(kern), 1e-9);
                            gepruft++;
                        }
                Pruefe.Wahr(gepruft >= 18, $"{kennung}: zu wenige Fahrten geprüft ({gepruft})");
            }
        }),
    };
}
