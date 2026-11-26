using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Einebnung der Bauplätze (24.11.2026): Unter einem Gebäude, dessen Boden um höchstens 0,5 m schwankt, setzt die Gelaendeform das Gelände auf die
/// Mitte zwischen höchstem und tiefstem Punkt (Grundfläche mit 2 m Rand eben, 4 m Böschung); darüber bleibt der Sockel. Wege gewinnen, ihr Profil
/// und die Weghöhe (Fahrzeiten) bleiben ohne Einebnung; Bauen, Abriss und Verschieben rechnen nur dort neu, wo sich eine Einebnung ändert.
/// </summary>
static class EinebnungTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenort O(double x, double z) => new(x, z);

    static void Ungefaehr(double erwartet, double ist, double toleranz, string text = "") =>
        Pruefe.Wahr(Math.Abs(ist - erwartet) <= toleranz, $"{text}: {ist} statt {erwartet}");

    /// <summary>Eine leere Karte 400 × 300 m (wie in den Geländetests).</summary>
    static Kartenentwurf Leer() => Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);

    /// <summary>Ein gleichmäßiger Hang nach Osten von −100 bis 100 m: Höhe = Gefälle · x.</summary>
    static void Hang(Kartenentwurf e, double gefaelle)
    {
        e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 1, Staerke: gefaelle * 100, Kante: 0), new[] { O(100, 0) });
        e.GelaendePinseln(new Pinsel(Pinselart.Senken, Radius: 1, Staerke: gefaelle * 100, Kante: 0), new[] { O(-100, 0) });
        e.Rampe(O(-100, 0), O(100, 0), breite: 400, kante: 0);
    }

    /// <summary>Dieselbe Karte frisch geöffnet (ohne jeden Zwischenstand der Gelaendeform).</summary>
    static Kartenentwurf Frisch(Kartenentwurf e) => Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value);

    /// <summary>Der erste Rasterpunkt im Rechteck, an dem zwei Gelände verschieden hoch sind (oder null).</summary>
    static string? Unterschied(Gelaendeform a, Gelaendeform b, Kartenrechteck r)
    {
        var (i0, j0, i1, j1) = Hoehenraster.Punkte(r);
        for (var j = j0; j <= j1; j++)
            for (var i = i0; i <= i1; i++)
                if (a.Punkthoehe(i, j) != b.Punkthoehe(i, j))
                    return $"({i}, {j}): {a.Punkthoehe(i, j)} statt {b.Punkthoehe(i, j)}";
        return null;
    }

    static readonly Kartenrechteck Umgebung = new(-20, -40, 100, 40);

    /// <summary>Ein fester Ablauf (Hang, Häuser setzen, verschieben, löschen, Weg) – die Höhen als Bits, für den Gleichheitstest.</summary>
    internal static string Ablauf()
    {
        var e = Leer();
        Hang(e, 0.025);
        var a = e.Setzen("leerstand", 40, 0);
        e.Setzen("einwohnerplatz_ring", 62, 3, 30);
        e.Setzen("einwohnerplatz_ring", 70.5, 3, 30);
        e.WegAnlegen(Wegklasse.Land, new[] { O(20, -30), O(56, 30) }, glatt: false, name: "Schräg");
        var text = new System.Text.StringBuilder();
        void Schreiben()
        {
            var (i0, j0, i1, j1) = Hoehenraster.Punkte(Umgebung);
            for (var j = j0; j <= j1; j++)
                for (var i = i0; i <= i1; i++)
                    text.Append(BitConverter.DoubleToInt64Bits(e.Gelaendeform.Punkthoehe(i, j)).ToString("x")).Append(' ');
            text.Append('\n');
        }
        Schreiben();
        e.Verschieben(a.Kennung, 30, 4);
        Schreiben();
        e.Loeschen(a.Kennung);
        Schreiben();
        return text.ToString();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Einebnung: Zielhöhe = Mitte zwischen höchstem und tiefstem Punkt, Grundfläche mit 2 m Rand eben, Fundament ohne Sockel; das Raster und die Weghöhe bleiben", () =>
        {
            var e = Leer();
            Hang(e, 0.02);                                    // 16 m breit: 0,32 m Unterschied
            var g = e.Setzen("leerstand", 40, 0);        // 16 × 14 m: x 32 … 48, z −7 … 7
            var f = e.Fundament(g);
            Ungefaehr(0.8, f.Hoehe, 1e-9);            // (0,64 + 0,96) / 2
            Ungefaehr(0, f.Tiefe, 1e-9, "eben: kein Sockel");
            var form = e.Gelaendeform;
            for (var j = -4; j <= 4; j++)                     // z −8 … 8 (bis 2 m Rand)
                for (var i = 15; i <= 25; i++)                // x 30 … 50
                    Ungefaehr(0.8, form.Punkthoehe(i, j), 1e-9, $"eben bei ({i}, {j})");
            Pruefe.Gleich(64, (int)e.Hoehen.Wert(16, 0), "das Raster bleibt");
            Ungefaehr(0.64, form.Weghoehe(32, 0), 1e-9, "die Weghöhe kennt keine Einebnung");
            Ungefaehr(0.8, form.Hoehe(33.3, -6.1), 1e-9, "zwischen den Rasterpunkten eben");
        }),

        ("Einebnung: über 0,5 m Unterschied bleibt alles wie bisher (Haus auf dem höchsten Punkt, Sockel bis zum tiefsten); bis 0,5 m eingeebnet", () =>
        {
            foreach (var (gefaelle, eben) in new[] { (0.03, true), (0.035, false), (0.05, false) })
            {
                var e = Leer();
                Hang(e, gefaelle);
                var g = e.Setzen("leerstand", 40, 0);
                var f = e.Fundament(g);
                var unterschied = 16 * gefaelle;
                if (eben)
                {
                    Ungefaehr(0, f.Tiefe, 1e-9, $"{gefaelle}: eingeebnet");
                    Ungefaehr(40 * gefaelle, f.Hoehe, 1e-9, $"{gefaelle}: Mitte");
                }
                else
                {
                    Ungefaehr(unterschied, f.Tiefe, 1e-9, $"{gefaelle}: Sockel");
                    Ungefaehr(48 * gefaelle, f.Hoehe, 1e-9, $"{gefaelle}: höchster Punkt");
                    for (var i = 10; i <= 30; i++)
                        Ungefaehr(e.Hoehen.Wert(i, 0) / 100.0, e.Gelaendeform.Punkthoehe(i, 0), 1e-9, $"{gefaelle}: Gelände unverändert bei x = {2 * i}");
                }
            }
            Pruefe.Gleich(0.5, Gelaendeform.EinebnenBis);
        }),

        ("Einebnung: die Böschung läuft über 4 m weich ins Gelände aus (Smoothstep), auf beiden Seiten; dahinter das Raster", () =>
        {
            var e = Leer();
            Hang(e, 0.02);
            e.Setzen("leerstand", 40, 0);
            var form = e.Gelaendeform;
            // bergseitig (Osten): Rand bis x = 50, Böschung bis 54
            Ungefaehr(0.8, form.Punkthoehe(25, 0), 1e-9);
            Ungefaehr(0.8 + (1.04 - 0.8) * 0.5, form.Punkthoehe(26, 0), 0.006, "x = 52: halb");  // Weich(0,5) = 0,5
            Ungefaehr(1.08, form.Punkthoehe(27, 0), 1e-9, "x = 54: das Raster");
            // talseitig (Westen): Rand bis x = 30, Böschung bis 26
            Ungefaehr(0.8 + (0.56 - 0.8) * 0.5, form.Punkthoehe(14, 0), 0.006, "x = 28: halb");
            Ungefaehr(0.52, form.Punkthoehe(13, 0), 1e-9, "x = 26: das Raster");
            // stetig: entlang z = 0 kein Sprung über eine Rasterzelle von mehr als 0,15 m
            for (var i = 5; i < 35; i++)
                Pruefe.Wahr(Math.Abs(form.Punkthoehe(i + 1, 0) - form.Punkthoehe(i, 0)) <= 0.2, $"Sprung bei x = {2 * i}");
        }),

        ("Einebnung: Nachbarn – überlappende Böschungen gehen ohne Sprung ineinander über, jeder Bauplatz bleibt auf seiner Höhe eben", () =>
        {
            var e = Leer();
            Hang(e, 0.02);
            var a = e.Setzen("leerstand", 40, 0);   // x 32 … 48, Ziel 0,8
            var b = e.Setzen("leerstand", 66, 0);   // x 58 … 74, Ziel 1,32; Böschungen 50 … 54 und 52 … 56 überlappen
            Ungefaehr(0.8, e.Fundament(a).Hoehe, 1e-9);
            Ungefaehr(1.32, e.Fundament(b).Hoehe, 1e-9);
            Ungefaehr(0, e.Fundament(a).Tiefe, 1e-9);
            Ungefaehr(0, e.Fundament(b).Tiefe, 1e-9);
            var form = e.Gelaendeform;
            var zwischen = Enumerable.Range(25, 4).Select(i => form.Punkthoehe(i, 0)).ToList();   // x 50 … 56
            Ungefaehr(0.8, zwischen[0], 1e-9);
            Ungefaehr(1.32, zwischen[3], 1e-9);
            for (var k = 0; k + 1 < zwischen.Count; k++)
                Pruefe.Wahr(zwischen[k + 1] > zwischen[k] && zwischen[k + 1] - zwischen[k] < 0.3, $"stetig steigend: {string.Join(" ", zwischen)}");
            // Reihenhäuser Wand an Wand: jedes auf seiner Höhe, in der Fläche eben; an der gemeinsamen Wand mischt das 2-m-Raster beide Höhen –
            // das Fundament kennt die Einebnung und bleibt auf der Zielhöhe ohne Sockel
            var r = Leer();
            Hang(r, 0.02);
            var h1 = r.Setzen("einwohnerplatz_ring", 40, 0);   // 8,5 m breit
            var h2 = r.Setzen("einwohnerplatz_ring", 48.5, 0);
            Ungefaehr(0.8, r.Fundament(h1).Hoehe, 1e-9, "Reihenhaus 1");
            Ungefaehr(0.97, r.Fundament(h2).Hoehe, 1e-9, "Reihenhaus 2");
            Pruefe.Wahr(r.Fundament(h1).Tiefe < 0.12 && r.Fundament(h2).Tiefe < 0.12, $"kein Sockel: {r.Fundament(h1)} {r.Fundament(h2)}");
            Ungefaehr(0.8, r.Gelaendeform.Punkthoehe(20, 0), 1e-9);
            Ungefaehr(0.97, r.Gelaendeform.Punkthoehe(24, 0), 1e-9);
        }),

        ("Einebnung: Bauen, Verschieben und Abriss rechnen nur dort neu, wo sich eine Einebnung ändert – dasselbe Gelände wie frisch gerechnet; die Darstellung holt die Rechtecke ab", () =>
        {
            var e = Leer();
            Hang(e, 0.025);
            e.WegAnlegen(Wegklasse.Land, new[] { O(55, -30), O(57, 30) }, glatt: false, name: "Quer");
            var form = e.Gelaendeform;
            _ = form.Punkthoehe(0, 0);
            Pruefe.Gleich(0, form.EinebnungAbholen().Count, "das erste Rechnen meldet nichts");
            var g = e.Setzen("leerstand", 40, 0);
            Pruefe.Wahr(form.EinebnungOffen, "nach dem Setzen offen");
            var gemeldet = form.EinebnungAbholen();
            Pruefe.Wahr(gemeldet.Any(r => r.Enthaelt(O(40, 0)) && r.Enthaelt(O(53.9, 0))), "Rechteck mit Böschung gemeldet");
            Pruefe.Wahr(!form.EinebnungOffen, "abgeholt");
            Pruefe.Gleich(null, Unterschied(form, Frisch(e).Gelaendeform, Umgebung), "gesetzt");
            Ungefaehr(0, e.Fundament(g).Tiefe, 1e-9);
            e.Verschieben(g.Kennung, 70, 10);
            Pruefe.Gleich(null, Unterschied(form, Frisch(e).Gelaendeform, Umgebung), "verschoben");
            Ungefaehr(e.Hoehen.Wert(20, 0) / 100.0, form.Punkthoehe(20, 0), 1e-9, "am alten Ort wieder das Raster");
            Pruefe.Wahr(form.EinebnungAbholen().Count == 2, "alter und neuer Ort gemeldet");
            e.Loeschen(g.Kennung);
            Pruefe.Gleich(null, Unterschied(form, Frisch(e).Gelaendeform, Umgebung), "gelöscht");
            Ungefaehr(e.Hoehen.Wert(35, 5) / 100.0, form.Punkthoehe(35, 5), 1e-9, "nach dem Abriss wieder das Raster");
            e.Rueckgaengig();
            Ungefaehr(0, e.Fundament(e.Gebaeude.Single()).Tiefe, 1e-9, "Rückgängig: wieder eingeebnet");
            Pruefe.Gleich(null, Unterschied(form, Frisch(e).Gelaendeform, Umgebung), "Rückgängig");
            // ein Pinselstrich unter dem Haus: neue Zielhöhe, wie frisch gerechnet
            e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 3, Staerke: 0.1, Kante: 0), new[] { O(72, 10) });
            Pruefe.Gleich(null, Unterschied(form, Frisch(e).Gelaendeform, Umgebung), "Pinsel unter dem Haus");
        }),

        ("Einebnung im Spiel: Grundstücke des Bauwesens werden eingeebnet (Neubau auf der Greybox), Stadtkarte mit eigenen Bauflächen: dazu und weg", () =>
        {
            // die Stadtkarte mit einer veränderlichen Liste von Bauflächen
            var e = Leer();
            Hang(e, 0.02);
            var karte = Stadtkarte.AusJson(e.ZuJson(), Katalog.Value, alteStadtplaetze: true);
            var flaechen = new List<Hausflaeche>();
            karte.BauflaechenSetzen(() => flaechen.Select(f => f.Ecken()));
            var form = karte.Gelaendeform;
            Ungefaehr(0.64, form.Punkthoehe(16, 0), 1e-9);
            flaechen.Add(new Hausflaeche(40, 0, 16, 14, 90));   // gedreht: 14 × 16 m, x 33 … 47
            karte.BauflaechenGeaendert();
            Ungefaehr(0.8, form.Punkthoehe(17, 0), 1e-9, "eingeebnet");
            Ungefaehr(0, form.Fundament(flaechen[0].Ecken()).Tiefe, 1e-9);
            Pruefe.Wahr(form.EinebnungAbholen().Count == 1, "gemeldet");
            flaechen.Clear();
            karte.BauflaechenGeaendert();
            Ungefaehr(0.68, form.Punkthoehe(17, 0), 1e-9, "abgerissen: das Raster");
            // die Greybox: ein Neubau wird Grundstück – das Gelände darunter ist eben (oder bleibt auf dem Sockel, wenn es zu steil ist)
            var daten = Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte());
            var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: daten);
            var w = sim.Wirtschaft!;
            w.KarteSetzen(Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));
            var gelaende = w.Karte!.Gelaendeform;
            _ = gelaende.Punkthoehe(0, 0);
            _ = gelaende.EinebnungAbholen();
            var k = w.KontoEroeffnen("Haus Esse", startvermoegen: 100_000);
            k.Stand = 4;
            var vorher = w.Bauen.Grundstuecke.Count;
            var befehl = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Datenpfad.Karte("greybox")))!["probeplan"]!.AsArray()
                .Select(p => (Art: (string)p!["art"]!, X: (double)p["punkt"]![0]!, Z: (double)p["punkt"]![1]!, Grad: (int)p["drehung"]!))
                .Where(p => p.Art == "Wohnhaus").Select(p => (BauBefehl)new WohnhausBauen(1, p.X, p.Z, p.Grad / 90 % 4))
                .First(b => w.Bauen.PlatzVerboten(b) is null);
            var ergebnis = w.Ausfuehren(k, befehl);
            Pruefe.Wahr(ergebnis.Erfolg, ergebnis.Meldung);
            Pruefe.Gleich(vorher + 1, w.Bauen.Grundstuecke.Count);
            var neu = w.Bauen.Grundstuecke[^1];
            var ecken = w.Bauen.Flaeche(neu).Ecken();
            Pruefe.Wahr(gelaende.EinebnungAbholen().Count >= 1, "der Neubau meldet seine Einebnung");
            var hang = gelaende.Hang(ecken);
            Pruefe.Wahr(gelaende.Fundament(ecken).Tiefe < 0.01, $"eben unter dem Neubau (Gefälle {hang.Gefaelle:0.000})");
        }),

        ("Einebnung: ein Weg über die Böschung – sein Profil bleibt, im Bett liegt der Boden auf dem Profil; der Rand des Hauses gibt dem Weg nach", () =>
        {
            var ohne = Leer();
            Hang(ohne, 0.02);
            var weg = ohne.WegAnlegen(Wegklasse.Land, new[] { O(52, -40), O(52, 40) }, glatt: false, name: "Quer");
            var profilVorher = ohne.Profillinie(weg).ToList();
            var e = Frisch(ohne);
            var g = e.Setzen("leerstand", 40, 0);
            var form = e.Gelaendeform;
            var profil = e.Profillinie(e.Wege.Single()).ToList();
            Pruefe.Gleich(profilVorher.Count, profil.Count);
            for (var k = 0; k < profil.Count; k++)
                Pruefe.Gleich(profilVorher[k].Hoehe, profil[k].Hoehe, $"Profil unverändert bei z = {profil[k].Ort.Z}");
            Pruefe.Wahr(profil.All(p => Math.Abs(p.Hoehe - form.Hoehe(p.Ort.X, p.Ort.Z)) < 0.01), "das Band liegt auf dem Boden");
            Ungefaehr(0, e.Fundament(g).Tiefe, 1e-9, "das Haus bleibt eben");
            // die Weghöhe (Karren) ist überall die Höhe ohne Einebnung
            var vorher = ohne.Gelaendeform;
            for (var z = -20.0; z <= 20; z += 3.5)
                for (var x = 20.0; x <= 60; x += 2.5)
                    Pruefe.Gleich(vorher.Hoehe(x, z), form.Weghoehe(x, z), $"Weghöhe bei ({x} | {z})");
        }),

        ("Einebnung: derselbe Ablauf ergibt zweimal dieselben Bits; Häuser auf der Karte Köln: an Hanglagen weniger Sockel", () =>
        {
            Pruefe.Gleich(Ablauf(), Ablauf());
            var koeln = Kartenentwurf.AusJson(File.ReadAllText(Datenpfad.Karte("koeln")), Katalog.Value);
            var (sockel, eingeebnet) = (0, 0);
            foreach (var g in koeln.Gebaeude.Where(g => Gelaendeform.WirdEingeebnet(g.Baustein)))
            {
                var f = koeln.Fundament(g);
                if (f.Tiefe >= 0.12) sockel++;
                if (f.Tiefe < 0.01 && koeln.Gelaendeform.Hang(g.Ecken()).Gefaelle > 0.005) eingeebnet++;
            }
            Console.WriteLine($"          Köln: {eingeebnet} Häuser am Hang eingeebnet, {sockel} mit Sockel ab 12 cm");
            Pruefe.Wahr(eingeebnet > 0, "in Köln wird eingeebnet");
        }),
    };
}
