using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Kartenbau;

/// <summary>
/// Felspinsel P6: Fläche füllen nach Regeln (Bauen/Felsfuellung.cs, Kartenwerkstatt/Kartenentwurf.Felsfuellung.cs) – Umriss und Kreis, ein Schritt mit
/// Rückgängig, Vorschau gleich Füllen, Schätzung, Obergrenze als Warnung (kein Verbot), die Regelkette (Steigung, Höhe in der Fläche, Rauschen,
/// groß vor klein, auf Fels), Aussparen, eigene Gruppen und die Voreinstellungen. Jeder Test mit Gegenprobe.
/// </summary>
static class FelsfuellungTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenort O(double x, double z) => new(x, z);

    static Kartenentwurf Leer() => Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);

    /// <summary>Ein steiler Berg um (0, 0): Hänge über 60 %, oben flach, unten flach.</summary>
    static Kartenentwurf Berg()
    {
        var e = Leer();
        e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 45, Staerke: 30, Kante: 0.3), new[] { O(0, 0) });
        e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 30, Staerke: 20, Kante: 0.2), new[] { O(0, 0) });
        return e;
    }

    /// <summary>Ein Quadrat von <paramref name="seite"/> Metern um (mx, mz).</summary>
    static Felsflaeche Quadrat(double mx, double mz, double seite) =>
        new(new[] { O(mx - seite / 2, mz - seite / 2), O(mx + seite / 2, mz - seite / 2), O(mx + seite / 2, mz + seite / 2), O(mx - seite / 2, mz + seite / 2) });

    static Felspinsel Bloecke(double dichte = 0.2) => new(Formen: new[] { "block", "platte" }, Dichte: dichte, Boden: false);

    static double Steigung(Kartenentwurf e, EntwurfFels f) => Pinselfilter.Steigung(e.Hoehen, f.X, f.Z);

    /// <summary>Ein fester Ablauf für den Gleichheitstest (Zeile „felsfuellung …“): Umriss, Kreis, Voreinstellungen, auf Fels, Rückgängig – Kartendatei am Ende.</summary>
    internal static string Ablauf()
    {
        var e = Berg();
        e.GrundgesteinSetzen("grauwacke");
        e.FelsFuellen(Felsfuellung.Einfach(Bloecke() with { Boden = true }, luecken: 0.3), Quadrat(-120, -90, 50), startwert: 11);
        e.FelsFuellen(Felsfuellung.Schutthalde, Felsflaeche.Kreis(O(0, 0), 60), startwert: 12);
        e.FelsFuellen(Felsfuellung.Felskuppe.MitPinsel(new Felspinsel(Gestein: "trachyt", Spiegeln: true, Verbinden: true)), Quadrat(120, 80, 70), startwert: 13);
        e.FelsFuellen(Felsfuellung.Steinbruch, new Felsflaeche(new[] { O(-150, 60), O(-90, 40), O(-70, 120), O(-140, 130) }), startwert: 14);
        e.Geschichte.Rueckgaengig();
        e.Geschichte.Wiederholen();
        e.FelsFuellen(Felsfuellung.Einfach(Bloecke(), aufFels: true), Felsflaeche.Kreis(O(0, 0), 60), startwert: 15);
        e.Geschichte.Rueckgaengig();
        return e.ZuJson();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Fels P6: Füllen im Umriss setzt nur Stücke mit der Mitte im Umriss, ein Schritt – Rückgängig stellt die Datei Byte für Byte her, Wiederholen dieselben Stücke", () =>
        {
            var e = Leer();
            var vorher = e.ZuJson();
            var schritte = e.Geschichte.Schritte;
            var dreieck = new Felsflaeche(new[] { O(-40, -30), O(50, -20), O(0, 40) });
            var neue = e.FelsFuellen(Felsfuellung.Einfach(Bloecke() with { Boden = true }), dreieck, startwert: 3);
            Pruefe.Wahr(neue.Count > 20, $"Stücke gesetzt ({neue.Count})");
            Pruefe.Gleich(neue.Count, e.Felsen.Count, "alle neu");
            Pruefe.Wahr(e.Felsen.All(f => dreieck.Enthaelt(f.X, f.Z)), "alle Mitten im Umriss");
            Pruefe.Gleich(schritte + 1, e.Geschichte.Schritte, "ein Schritt (mit dem Boden)");
            var nachher = e.ZuJson();
            Pruefe.Wahr(e.Geschichte.Rueckgaengig(), "Rückgängig");
            Pruefe.Gleich(vorher, e.ZuJson(), "gleiche Bytes wie vorher");
            e.Geschichte.Wiederholen();
            Pruefe.Gleich(nachher, e.ZuJson(), "Wiederholen: dieselben Stücke");
            // Gegenprobe: ein größerer Kreis um dieselbe Stelle setzt auch außerhalb des Dreiecks
            var k = Leer();
            k.FelsFuellen(Felsfuellung.Einfach(Bloecke()), Felsflaeche.Kreis(O(0, 0), 70), startwert: 3);
            Pruefe.Wahr(k.Felsen.Any(f => !dreieck.Enthaelt(f.X, f.Z)), "Gegenprobe: Kreis reicht über das Dreieck");
        }),

        ("Fels P6: Die Vorschau ist genau das Füllen und ändert nichts; gleicher Startwert gleiche Bytes, anderer Startwert andere", () =>
        {
            var e = Berg();
            var flaeche = Felsflaeche.Kreis(O(0, 0), 55);
            var (vorher, schritte) = (e.ZuJson(), e.Geschichte.Schritte);
            var vorschau = e.FelsFuellenVorschau(Felsfuellung.Steinbruch, flaeche, startwert: 7);
            Pruefe.Gleich(vorher, e.ZuJson(), "Vorschau ändert nichts");
            Pruefe.Gleich(schritte, e.Geschichte.Schritte, "kein Schritt");
            var neue = e.FelsFuellen(Felsfuellung.Steinbruch, flaeche, startwert: 7);
            Pruefe.Gleich(vorschau.Count, neue.Count, "gleiche Zahl");
            Pruefe.Wahr(vorschau.Select(s => s.Lage).SequenceEqual(neue.Select(f => f.AlsLage())), "gleiche Lagen");
            var zweiter = Berg();
            zweiter.FelsFuellen(Felsfuellung.Steinbruch, flaeche, startwert: 7);
            Pruefe.Gleich(e.ZuJson(), zweiter.ZuJson(), "zweiter Lauf: gleiche Bytes");
            var anders = Berg();
            anders.FelsFuellen(Felsfuellung.Steinbruch, flaeche, startwert: 8);
            Pruefe.Wahr(e.ZuJson() != anders.ZuJson(), "Gegenprobe: anderer Startwert, andere Stücke");
            // ohne Startwert: aus der ersten Ecke – Vorschau und Füllen gleich
            var ohne = Berg();
            var v2 = ohne.FelsFuellenVorschau(Felsfuellung.Steinbruch, flaeche);
            Pruefe.Wahr(v2.Select(s => s.Lage).SequenceEqual(ohne.FelsFuellen(Felsfuellung.Steinbruch, flaeche).Select(f => f.AlsLage())), "ohne Startwert gleich");
        }),

        ("Fels P6: Die Schätzung liegt nahe an der Zahl; über der Obergrenze 1500 füllt der Befehl trotzdem (Warnung, kein Verbot)", () =>
        {
            var e = Leer();
            var klein = Quadrat(0, 0, 60);
            var schaetzung = e.FelsFuellenSchaetzen(Felsfuellung.Einfach(Bloecke()), klein);
            var zahl = e.FelsFuellenVorschau(Felsfuellung.Einfach(Bloecke()), klein, startwert: 1).Count;
            Pruefe.Wahr(zahl > 0 && Math.Abs(schaetzung - zahl) <= 0.4 * zahl, $"Schätzung {schaetzung} nahe an {zahl}");
            Pruefe.Wahr(schaetzung < Felsfuellung.Obergrenze, "kleine Fläche unter der Obergrenze");
            var gross = Quadrat(0, 0, 260);
            var g = e.FelsFuellenSchaetzen(Felsfuellung.Einfach(Bloecke(0.3)), gross);
            Pruefe.Wahr(g > Felsfuellung.Obergrenze, $"große Fläche über der Obergrenze ({g})");
            var neue = e.FelsFuellen(Felsfuellung.Einfach(Bloecke(0.3)), gross, startwert: 2);
            Pruefe.Wahr(neue.Count > Felsfuellung.Obergrenze, $"gefüllt trotz Obergrenze ({neue.Count})");
            Pruefe.Wahr(Math.Abs(g - neue.Count) <= 0.4 * neue.Count, $"Schätzung {g} nahe an {neue.Count}");
            // ungültige Fläche: nichts, kein Schritt, Schätzung 0
            var linie = new Felsflaeche(new[] { O(0, 0), O(10, 0) });
            Pruefe.Gleich(0, e.FelsFuellenSchaetzen(Felsfuellung.Steinbruch, linie), "zwei Ecken: Schätzung 0");
            var schritte = e.Geschichte.Schritte;
            Pruefe.Gleich(0, e.FelsFuellen(Felsfuellung.Steinbruch, linie).Count, "zwei Ecken: nichts");
            Pruefe.Gleich(schritte, e.Geschichte.Schritte, "kein Schritt");
        }),

        ("Fels P6: Kette – Steigung und Höhe in der Fläche gelten je Regel (Gegenprobe ohne Regel), Lücken lassen Fläche frei", () =>
        {
            var flaeche = Felsflaeche.Kreis(O(0, 0), 70);
            var steil = Berg();
            steil.FelsFuellen(Felsfuellung.Einfach(Bloecke() with { Filter = new Pinselfilter(SteigungAb: 40) }), flaeche, startwert: 4);
            Pruefe.Wahr(steil.Felsen.Count > 10, $"am Steilhang gesetzt ({steil.Felsen.Count})");
            Pruefe.Wahr(steil.Felsen.All(f => Steigung(steil, f) > 39.9), "alle am Hang ab 40 %");
            var alle = Berg();
            alle.FelsFuellen(Felsfuellung.Einfach(Bloecke()), flaeche, startwert: 4);
            Pruefe.Wahr(alle.Felsen.Any(f => Steigung(alle, f) < 20), "Gegenprobe: ohne Filter auch im Flachen");

            var oben = Berg();
            var regel = new Felsregel(Bloecke(), HoeheAnteil: new Felsbereich(0.6, 1));
            oben.FelsFuellen(new Felsfuellung("probe", "Probe", new[] { regel }), flaeche, startwert: 5);
            var (min, max) = (alle.Felsen.Min(f => alle.Hoehen.Hoehe(f.X, f.Z)), alle.Felsen.Max(f => alle.Hoehen.Hoehe(f.X, f.Z)));
            Pruefe.Wahr(oben.Felsen.Count > 5, $"oben gesetzt ({oben.Felsen.Count})");
            Pruefe.Wahr(oben.Felsen.All(f => oben.Hoehen.Hoehe(f.X, f.Z) >= min + 0.5 * (max - min)), "alle in der oberen Hälfte der Höhe");
            Pruefe.Wahr(alle.Felsen.Any(f => alle.Hoehen.Hoehe(f.X, f.Z) < min + 0.3 * (max - min)), "Gegenprobe: ohne Regel auch unten");

            var luecken = Leer();
            var quadrat = Quadrat(0, 0, 120);
            luecken.FelsFuellen(Felsfuellung.Einfach(Bloecke(), luecken: 0.5, rauschweite: 20), quadrat, startwert: 6);
            var voll = Leer();
            voll.FelsFuellen(Felsfuellung.Einfach(Bloecke()), quadrat, startwert: 6);
            Pruefe.Wahr(luecken.Felsen.Count > 10 && luecken.Felsen.Count < 0.85 * voll.Felsen.Count, $"Lücken: {luecken.Felsen.Count} statt {voll.Felsen.Count}");
            var salz = unchecked(6 * 31 + 0 * 977 + 13);
            Pruefe.Wahr(luecken.Felsen.All(f => Felsfuellung.Rauschen(f.X, f.Z, 20, salz) > 0.5), "nur wo das Rauschen über den Lücken liegt");
            Pruefe.Wahr(voll.Felsen.Any(f => Felsfuellung.Rauschen(f.X, f.Z, 20, salz) < 0.45), "Gegenprobe: ohne Lücken auch dort");
        }),

        ("Fels P6: Groß vor klein – die Kette ordnet nach dem größten Stück, die Reihenfolge der Regeln ändert das Ergebnis nicht", () =>
        {
            var gross = new Felsregel(new Felspinsel(Formen: new[] { "wand_mittel_1" }, Dichte: 0.3, Boden: false));
            var klein = new Felsregel(new Felspinsel(Formen: new[] { "schuttbrocken" }, Dichte: 0.1, Boden: false));
            var kette = new Felsfuellung("a", "A", new[] { klein, gross }).Kette();
            Pruefe.Gleich(1, kette[0].Nr, "die große Regel zuerst");
            var (a, b) = (Leer(), Leer());
            var flaeche = Quadrat(0, 0, 80);
            a.FelsFuellen(new Felsfuellung("a", "A", new[] { klein, gross }), flaeche, startwert: 9);
            b.FelsFuellen(new Felsfuellung("b", "B", new[] { gross, klein }), flaeche, startwert: 9);
            Pruefe.Gleich(a.Felsen.Count, b.Felsen.Count, "gleiche Zahl");
            Pruefe.Wahr(a.Felsen.Select(f => (f.Form, f.X, f.Z)).SequenceEqual(b.Felsen.Select(f => (f.Form, f.X, f.Z))), "gleiche Stücke in gleicher Reihenfolge");
            Pruefe.Gleich("wand_mittel_1", a.Felsen[0].Form, "erst die Wände");
            // Gegenprobe: erst der Schutt, dann die Wände (zwei Befehle) – der Schutt nimmt den Wänden Platz
            var erstKlein = Leer();
            erstKlein.FelsFuellen(new Felsfuellung("c", "C", new[] { klein }), flaeche, startwert: 9);
            erstKlein.FelsFuellen(new Felsfuellung("d", "D", new[] { gross }), flaeche, startwert: 9);
            Pruefe.Wahr(erstKlein.Felsen.Count(f => f.Form == "wand_mittel_1") < a.Felsen.Count(f => f.Form == "wand_mittel_1"), "Gegenprobe: klein vor groß, weniger Wände");
        }),

        ("Fels P6: Auf Fels setzen – kleine Stücke liegen oben auf großen (Versatz über dem Boden), ohne große nichts", () =>
        {
            var e = Leer();
            var flaeche = Quadrat(0, 0, 100);
            var gross = new Felsregel(new Felspinsel(Formen: new[] { "kuppe" }, Dichte: 0.25, Streuung: 0, Boden: false));
            var auf = new Felsregel(new Felspinsel(Formen: new[] { "schuttbrocken" }, Dichte: 0.3, Boden: false), AufFels: true);
            e.FelsFuellen(new Felsfuellung("k", "K", new[] { auf, gross }), flaeche, startwert: 21);
            var kuppen = e.Felsen.Where(f => f.Form == "kuppe").ToList();
            var oben = e.Felsen.Where(f => f.Form == "schuttbrocken").ToList();
            Pruefe.Wahr(kuppen.Count > 3 && oben.Count > 3, $"Kuppen {kuppen.Count}, oben {oben.Count}");
            foreach (var s in oben)
            {
                Pruefe.Wahr(kuppen.Any(k => Math.Sqrt((k.X - s.X) * (k.X - s.X) + (k.Z - s.Z) * (k.Z - s.Z)) <= Felsfuellung.AufFelsInnen * k.Radius + 0.02), "über einer Kuppe");
                Pruefe.Wahr(s.Versatz > 1, $"oben auf (Versatz {s.Versatz})");
                Pruefe.Gleich(0.0, s.Neigung, "ohne Neigung");
            }
            // Gegenprobe: ohne große Stücke setzt „auf Fels“ nichts
            var leer = Leer();
            Pruefe.Gleich(0, leer.FelsFuellen(new Felsfuellung("k", "K", new[] { auf }), flaeche, startwert: 21).Count, "ohne Träger nichts");
            // vorhandene große Stücke tragen auch
            leer.FelsStempeln(new Felspinsel(Formen: new[] { "kuppe" }, Streuung: 0, Boden: false), O(0, 0), drehung: 0, startwert: 1);
            Pruefe.Wahr(leer.FelsFuellen(new Felsfuellung("k", "K", new[] { auf }), flaeche, startwert: 21).Count > 0, "auf einer gestempelten Kuppe");
        }),

        ("Fels P6: Aussparen hält Gebäude und Wege frei (Gegenprobe ohne), eigene Gruppen kommen ganz und verbunden", () =>
        {
            Kartenentwurf Strasse()
            {
                var e = Leer();
                e.Setzen("werkstattplatz", -30, 0);
                e.WegAnlegen(Wegklasse.Land, new[] { O(-80, 40), O(80, 40) }, glatt: false, einrasten: false);
                return e;
            }
            var flaeche = Quadrat(-10, 20, 100);
            var mit = Strasse();
            mit.FelsFuellen(Felsfuellung.Einfach(Bloecke(0.4) with { Aussparen = true }), flaeche, startwert: 5);
            var aus = Felsaussparung.Von(mit, false);
            Pruefe.Wahr(mit.Felsen.Count > 10, "gesetzt");
            Pruefe.Wahr(mit.Felsen.All(f => !aus.Belegt(f.X, f.Z, f.Radius)), "kein Stück berührt Haus oder Weg");
            Pruefe.Gleich(mit.FelsFuellenSchaetzen(Felsfuellung.Einfach(Bloecke(0.4) with { Aussparen = true }), flaeche) < mit.FelsFuellenSchaetzen(Felsfuellung.Einfach(Bloecke(0.4)), flaeche), true, "Schätzung ohne Haus und Weg kleiner");
            var ohne = Strasse();
            ohne.FelsFuellen(Felsfuellung.Einfach(Bloecke(0.4)), flaeche, startwert: 5);
            Pruefe.Wahr(ohne.Felsen.Any(f => aus.Belegt(f.X, f.Z, f.Radius)), "Gegenprobe: ohne Häkchen auf Haus oder Weg");

            var formation = Leer();
            var einzel = new Felspinsel(Formen: new[] { "block" }, Streuung: 0, Boden: false);
            formation.FelsStempeln(einzel, O(0, 0), drehung: 0, startwert: 1);
            formation.FelsStempeln(einzel with { Formen = new[] { "schuttbrocken" } }, O(3, 2), drehung: 0, startwert: 2);
            formation.FelsStempeln(einzel with { Formen = new[] { "schuttbrocken" } }, O(-3, 2), drehung: 0, startwert: 3);
            var k = Felskatalog.Leer.Ablegen("Drei", formation.FelsGruppenstuecke(formation.Felsen), out var g);
            var e = Leer();
            e.FelsFuellen(Felsfuellung.Einfach(new Felspinsel(Eigengruppe: g, Dichte: 0.15, Boden: false, Verbinden: true)), Quadrat(0, 0, 80), startwert: 3);
            Pruefe.Wahr(e.Felsen.Count >= 9 && e.Felsen.Count % 3 == 0, $"ganze Gruppen ({e.Felsen.Count})");
            Pruefe.Gleich(e.Felsen.Count / 3, e.Felsen.Select(f => f.Gruppe).Distinct().Count(), "je Treffer eine Nummer");
            Pruefe.Wahr(e.Felsen.All(f => f.Gruppe > 0), "alle verbunden");
            var nicht = Leer();
            nicht.FelsFuellen(Felsfuellung.Einfach(new Felspinsel(Eigengruppe: g, Dichte: 0.15, Boden: false)), Quadrat(0, 0, 80), startwert: 3);
            Pruefe.Wahr(nicht.Felsen.All(f => f.Gruppe == 0), "Gegenprobe: ohne Verbinden keine Nummer");
        }),

        ("Fels P6: Schutthalde – Wände am Steilhang, Schutt am Fuß; die Voreinstellungen füllen den Berg, ohne Hang nur Flachstücke", () =>
        {
            var e = Berg();
            var flaeche = Felsflaeche.Kreis(O(0, 0), 70);
            e.FelsFuellen(Felsfuellung.Schutthalde, flaeche, startwert: 31);
            var waende = e.Felsen.Where(f => f.Form is "wand" or "rippe" or "felsnase").ToList();
            var schutt = e.Felsen.Where(f => f.Form == "schuttbrocken").ToList();
            Pruefe.Wahr(waende.Count > 3 && schutt.Count > 10, $"Wände {waende.Count}, Schutt {schutt.Count}");
            Pruefe.Wahr(waende.All(f => Steigung(e, f) > 59.9), "Wände nur am Steilhang");
            var (min, max) = (e.Felsen.Min(f => e.Hoehen.Hoehe(f.X, f.Z)), e.Felsen.Max(f => e.Hoehen.Hoehe(f.X, f.Z)));
            Pruefe.Wahr(schutt.Average(f => e.Hoehen.Hoehe(f.X, f.Z)) < waende.Average(f => e.Hoehen.Hoehe(f.X, f.Z)), "Schutt im Mittel unter den Wänden");
            foreach (var v in Felsfuellung.Voreinstellungen)
                Pruefe.Wahr(Berg().FelsFuellenVorschau(v, flaeche, startwert: 1).Count > 10, $"{v.Name} füllt den Berg");
            // Gegenprobe: im Flachen gibt es keine Wände
            var flach = Leer();
            flach.FelsFuellen(Felsfuellung.Schutthalde, flaeche, startwert: 31);
            Pruefe.Wahr(flach.Felsen.Count > 0 && flach.Felsen.All(f => f.Form is not ("wand" or "rippe" or "felsnase")), "flach: keine Wände");
            Pruefe.Gleich(Felsfuellung.Steinbruch, Felsfuellung.Finde("steinbruch"), "Finde");
            Pruefe.Gleich(null, Felsfuellung.Finde("umriss"), "„im Umriss“ ist keine Voreinstellung");
        }),

        ("Fels P6: Kartenskript felsen --fuellen füllt die Drachenfels-Probe (Hügel, Schutthalde, Kreis mit Vorlage); zweimal dieselben Bytes, unlesbares gilt nicht", () =>
        {
            string Lauf()
            {
                var o = new Projektordner(Path.Combine(Path.GetTempPath(), "reichsgilde-fuellen-" + Guid.NewGuid().ToString("N")));
                try
                {
                    Directory.CreateDirectory(o.Daten);
                    Directory.CreateDirectory(o.Karten);
                    foreach (var datei in new[] { "katalog.json", "wirtschaft.json" }) File.Copy(Path.Combine(Datenordner, datei), Path.Combine(o.Daten, datei));
                    File.Copy(Datenpfad.Karte("messstadt"), o.Karte("messstadt"));
                    var aus = new StringWriter();
                    Pruefe.Gleich(0, Befehle.Gelaende(o, "messstadt", new Pinsel(Pinselart.Heben, 45, 30, Kante: 0.3), new[] { O(-450, 300) }, aus), aus.ToString());
                    var halde = Reichsgilde.Kartenbau.Program.FuellungVon("schutthalde@trachyt", new[] { "--boden=aus" })!;
                    Pruefe.Gleich("schutthalde", halde.Kennung, "Voreinstellung");
                    Pruefe.Wahr(halde.Regeln.All(r => r.Pinsel.Gestein == "trachyt" && !r.Pinsel.Boden), "Gestein und Boden aus den Schaltern");
                    Pruefe.Gleich(0, Befehle.FelsenFuellen(o, "messstadt", halde, new[] { O(-510, 240), O(-390, 240), O(-390, 360), O(-510, 360) }, 30, 5, aus), aus.ToString());
                    var kreis = Reichsgilde.Kartenbau.Program.FuellungVon("block+platte", new[] { "--dichte=0.15", "--luecken=0.3" })!;
                    Pruefe.Gleich(0, Befehle.FelsenFuellen(o, "messstadt", kreis, new[] { O(-450, 420) }, 25, 6, aus), aus.ToString());
                    Pruefe.Wahr(aus.ToString().Contains("Fläche gefüllt (Schutthalde"), aus.ToString());
                    var e = Kartenentwurf.AusJson(File.ReadAllText(o.Karte("messstadt")), Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));
                    Pruefe.Wahr(e.Felsen.Count(f => f.Form is "wand" or "rippe" or "felsnase") > 0 && e.Felsen.Count(f => f.Form == "schuttbrocken") > 5, $"Halde gefüllt ({e.Felsen.Count})");
                    Pruefe.Wahr(e.Felsen.Any(f => Math.Abs(f.X + 450) < 26 && Math.Abs(f.Z - 420) < 26 && f.Form is "block" or "platte"), "Kreis gefüllt");
                    return File.ReadAllText(o.Karte("messstadt"));
                }
                finally
                {
                    if (Directory.Exists(o.Wurzel)) Directory.Delete(o.Wurzel, recursive: true);
                }
            }
            Pruefe.Gleich(Lauf(), Lauf(), "zweimal dieselben Bytes");
            Pruefe.Gleich(null, Reichsgilde.Kartenbau.Program.FuellungVon("schutthalde", new[] { "--luecken=viel" }), "Gegenprobe: unlesbare Zahl");
            Pruefe.Gleich(null, Reichsgilde.Kartenbau.Program.FuellungVon("zinne", Array.Empty<string>()), "Gegenprobe: unbekannte Vorlage");
            Pruefe.Gleich(null, Reichsgilde.Kartenbau.Program.FuellungVon("block", new[] { "--auffels=ja" }), "Gegenprobe: auffels nur an/aus");
        }),

        ("Fels P6: Kreis als Fläche – Inhalt etwa π r², Ecken gerundet, Rauschen deterministisch zwischen 0 und 1", () =>
        {
            var k = Felsflaeche.Kreis(O(10, -5), 20);
            Pruefe.Gleich(Felsflaeche.KreisEcken, k.Umriss.Count, "Ecken");
            Pruefe.Wahr(Math.Abs(k.Flaeche - Math.PI * 400) < 0.02 * Math.PI * 400, $"Inhalt {k.Flaeche}");
            Pruefe.Wahr(k.Enthaelt(10, -5) && !k.Enthaelt(31, -5), "innen und außen");
            Pruefe.Wahr(k.Umriss.All(p => p.X == Kartenraster.Punkt(p.X) && p.Z == Kartenraster.Punkt(p.Z)), "gerundet wie in der Datei");
            var werte = Enumerable.Range(0, 400).Select(i => Felsfuellung.Rauschen(i * 1.7 - 300, i * 0.9 - 100, 24, 5)).ToList();
            Pruefe.Wahr(werte.All(w => w is >= 0 and <= 1), "0 … 1");
            Pruefe.Wahr(werte.Max() - werte.Min() > 0.5, "streut");
            Pruefe.Gleich(Felsfuellung.Rauschen(12.3, -4.5, 24, 5), Felsfuellung.Rauschen(12.3, -4.5, 24, 5), "gleich");
            Pruefe.Wahr(Felsfuellung.Rauschen(12.3, -4.5, 24, 5) != Felsfuellung.Rauschen(12.3, -4.5, 24, 6), "Gegenprobe: anderes Salz");
        }),
    };
}
