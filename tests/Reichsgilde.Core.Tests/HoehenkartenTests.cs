using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Kartenbau;

/// <summary>
/// Höhenkarten einlesen und ausgeben (Terrain3D-Übernahme, Etappe 2): r16/raw lesen, Lage und Maßstab, Neuabtastung auf 2 m, Quantisieren,
/// Abschneiden mit Warnung, Abdeckung, Rückgängig, gleiche Datei gleiche Bytes, Ausgabe und Wiedereinlesen, Kartenskript mit Herkunft.
/// </summary>
static class HoehenkartenTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenort O(double x, double z) => new(x, z);

    /// <summary>Eine leere Karte 400 × 300 m mit 300 m Rand (x −500 … 500, z −450 … 450).</summary>
    static Kartenentwurf Leer() => Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);

    /// <summary>Höhe eines Rasterpunkts am Ort (x, z) in cm (gerade Meter).</summary>
    static int Cm(Kartenentwurf e, double x, double z) => e.Hoehen.Wert((int)Math.Floor(x / 2), (int)Math.Floor(z / 2));

    /// <summary>Werte Zeile für Zeile aus einer Formel (a = Spalte, b = Zeile).</summary>
    static ushort[] Bild(int breite, int tiefe, Func<int, int, int> wert)
    {
        var w = new ushort[breite * tiefe];
        for (var b = 0; b < tiefe; b++)
            for (var a = 0; a < breite; a++) w[b * breite + a] = (ushort)Math.Clamp(wert(a, b), 0, Hoehenkarte.Groesster);
        return w;
    }

    /// <summary>Wert 0 = 0 cm, 65535 = 65.535 cm: ein Wert ist genau ein Zentimeter (Stufe 1).</summary>
    static Hoehenkartenlage Zentimeter(int breite, int tiefe, int pixelCm, int x0 = 0, int z0 = 0) =>
        new(breite, tiefe, pixelCm, x0, z0, 0, Hoehenkarte.Groesster, 0, 1);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Höhenkarte lesen: r16 mit niedrigem oder hohem Byte zuerst, quadratisch ohne Breite, rechteckig mit Breite; falsche Längen melden", () =>
        {
            var werte = new ushort[] { 1, 258, 65535, 0 };
            var le = Hoehenkarte.Schreiben(werte);
            Pruefe.Gleich("01-00-02-01-FF-FF-00-00", BitConverter.ToString(le));
            var (gelesen, breite, tiefe) = Hoehenkarte.Lesen(le);
            Pruefe.Wahr(gelesen.SequenceEqual(werte) && breite == 2 && tiefe == 2, $"{string.Join(",", gelesen)} {breite}×{tiefe}");
            var be = Hoehenkarte.Schreiben(werte, hohesZuerst: true);
            Pruefe.Gleich("00-01-01-02-FF-FF-00-00", BitConverter.ToString(be));
            Pruefe.Wahr(Hoehenkarte.Lesen(be, hohesZuerst: true).Werte.SequenceEqual(werte), "hohes Byte zuerst");
            var sechs = Hoehenkarte.Schreiben(new ushort[] { 1, 2, 3, 4, 5, 6 });
            Pruefe.Gleich((3, 2), (Hoehenkarte.Lesen(sechs, 3).Breite, Hoehenkarte.Lesen(sechs, 3).Tiefe));
            Pruefe.WirftMitMeldung(() => Hoehenkarte.Lesen(sechs), "kein Quadrat");
            Pruefe.WirftMitMeldung(() => Hoehenkarte.Lesen(sechs, 4), "Zeilen zu 4");
            Pruefe.WirftMitMeldung(() => Hoehenkarte.Lesen(new byte[] { 1, 2, 3 }), "zwei Bytes je Pixel");
            Pruefe.WirftMitMeldung(() => Hoehenkarte.Lesen(Array.Empty<byte>()), "zwei Bytes je Pixel");
        }),

        ("Höhenkarte einlesen: Wert 0 = unten, 65535 = oben, gerundet; 2-m-Pixel 1:1 auf die Rasterpunkte; Versatz; falsche Lage meldet", () =>
        {
            var e = Leer();
            var werte = new ushort[] { 0, 65535, 32768, 13107, 52428, 1 };
            var lage = new Hoehenkartenlage(3, 2, 200, 0, 0, -1000, 5000, 0, 1);
            var bericht = e.HoehenkarteEinlesen(werte, lage);
            // −1000 + Wert · 6000 / 65535, halb weg von 0 gerundet
            Pruefe.Gleich("-1000 5000 2000 / 200 3800 -1000", $"{Cm(e, 0, 0)} {Cm(e, 2, 0)} {Cm(e, 4, 0)} / {Cm(e, 0, 2)} {Cm(e, 2, 2)} {Cm(e, 4, 2)}");
            Pruefe.Gleich(6, bericht.Abgedeckt);
            Pruefe.Gleich(6, bericht.Geaendert);
            Pruefe.Wahr(!bericht.Abgeschnitten && bericht.Warnung is null, "nichts abgeschnitten");
            Pruefe.Gleich((-1000, 5000), (bericht.TiefsteCm, bericht.HoechsteCm));
            Pruefe.Gleich("Höhenkarte eingelesen", e.Geschichte.RueckgaengigText);
            // Köln: Gelände 37 … 60 m über NN, Versatz −37 m legt den tiefsten Punkt auf 0
            var k = Leer();
            k.HoehenkarteEinlesen(new ushort[] { 0, 65535, 0, 65535 }, new Hoehenkartenlage(2, 2, 200, -200, -200, 3700, 6000, -3700, 5));
            Pruefe.Gleich("0 2300", $"{Cm(k, -2, -2)} {Cm(k, 0, 0)}");
            Pruefe.WirftMitMeldung(() => e.HoehenkarteEinlesen(werte, lage with { Breite = 2 }), "6 Pixel");
            Pruefe.WirftMitMeldung(() => e.HoehenkarteEinlesen(werte, lage with { ObenCm = -1000 }), "höher liegen");
            Pruefe.WirftMitMeldung(() => e.HoehenkarteEinlesen(werte, lage with { PixelCm = 5 }), "Pixelabstand");
            Pruefe.WirftMitMeldung(() => e.HoehenkarteEinlesen(werte, lage with { StufeCm = 0 }), "Stufe");
        }),

        ("Neuabtastung auf 2 m: 1-m-Pixel im Zelt über 2 m gemittelt (eine Spitze wird zum Viertel), 4-m-Pixel bilinear, eine Rampe bleibt eine Rampe", () =>
        {
            // Rampe nach Osten mit 1-m-Pixeln: Pixel a hat a · 100 cm, also der Rasterpunkt bei x = 2·i genau 200 · i (im Inneren)
            var e = Leer();
            e.HoehenkarteEinlesen(Bild(41, 9, (a, _) => a * 100), Zentimeter(41, 9, 100));
            for (var x = 2; x <= 38; x += 2) Pruefe.Gleich(x * 100, Cm(e, x, 4), $"Rampe bei x = {x}");
            // eine Spitze von 10 m auf einem Pixel: Gewichte 100, 200, 100 je Richtung → 200² / 400² = ein Viertel
            var f = Leer();
            f.HoehenkarteEinlesen(Bild(21, 21, (a, b) => a == 10 && b == 10 ? 1000 : 0), Zentimeter(21, 21, 100));
            Pruefe.Gleich(250, Cm(f, 10, 10));
            Pruefe.Gleich(0, Cm(f, 14, 10), "zwei Rasterpunkte weiter nichts mehr");
            // 4-m-Pixel: zwischen zwei Pixeln genau die Mitte
            var g = Leer();
            g.HoehenkarteEinlesen(Bild(3, 3, (a, b) => a * 400 + b * 40), Zentimeter(3, 3, 400));
            Pruefe.Gleich("200 600 620 220", $"{Cm(g, 2, 0)} {Cm(g, 6, 0)} {Cm(g, 6, 2)} {Cm(g, 2, 2)}");
            // 3-m-Pixel mit Ursprung zwischen den Rasterpunkten: bilinear, gerundet
            var h = Leer();
            h.HoehenkarteEinlesen(Bild(5, 2, (a, _) => a * 300), Zentimeter(5, 2, 300, 100, 0));
            Pruefe.Gleich("100 300 500 700 900 1100", string.Join(" ", new[] { 2, 4, 6, 8, 10, 12 }.Select(x => Cm(h, x, 0))));
        }),

        ("Quantisieren auf 5 cm (Vorgabe): alle Höhen auf der Stufe; ebenes Land mit Messrauschen packt danach viel kleiner; Abschneiden mit Warnung", () =>
        {
            // ebenes Land in 10 m Höhe mit Messrauschen von ±2 cm (feste Streuung, kein Zufall) – so sieht ein DGM in der Ebene aus
            ushort Rauschig(int a, int b) => (ushort)(1000 + (int)((uint)(a * 7919 + b * 104729) * 2654435761u % 5) - 2);
            var werte = Bild(120, 120, (a, b) => Rauschig(a, b));
            var (fein, grob) = (Leer(), Leer());
            fein.HoehenkarteEinlesen(werte, Zentimeter(120, 120, 200, -12000, -12000));
            grob.HoehenkarteEinlesen(werte, Zentimeter(120, 120, 200, -12000, -12000) with { StufeCm = 5 });
            for (var x = -120; x < 118; x += 2)
                for (var z = -120; z < 118; z += 14)
                    Pruefe.Gleich(0, Cm(grob, x, z) % 5, $"auf 5 cm ({x}, {z}): {Cm(grob, x, z)}");
            var (laengeFein, laengeGrob) = (fein.Hoehen.AlsJson()!.ToJsonString().Length, grob.Hoehen.AlsJson()!.ToJsonString().Length);
            Pruefe.Wahr(laengeGrob * 4 < laengeFein, $"5 cm packt viel kleiner: {laengeGrob} gegen {laengeFein} Zeichen");
            Pruefe.Wahr(Enumerable.Range(-60, 119).All(i => grob.Hoehen.Wert(i, 0) == 1000), "das Rauschen ist weg");
            // −50 … +100 m: Wert 0 und 65535 liegen außerhalb von −20 … +60 m
            var e = Leer();
            var bericht = e.HoehenkarteEinlesen(new ushort[] { 0, 65535, 21845, 43690 }, new Hoehenkartenlage(2, 2, 200, 0, 0, -5000, 10000, 0, 1));
            Pruefe.Gleich((1, 1), (bericht.ZuTief, bericht.ZuHoch));
            Pruefe.Gleich((-5000, 10000), (bericht.TiefsteCm, bericht.HoechsteCm));
            Pruefe.Gleich("-2000 6000 0 5000", $"{Cm(e, 0, 0)} {Cm(e, 2, 0)} {Cm(e, 0, 2)} {Cm(e, 2, 2)}");
            Pruefe.Wahr(bericht.Warnung!.Contains("2 Punkte abgeschnitten (1 unter −20 m, 1 über +60 m") && bericht.Warnung.Contains("von -50 bis 100 m"), bericht.Warnung);
        }),

        ("Abdeckung: außerhalb des Bildes bleibt das Gelände, außerhalb der Karte wird nichts geschrieben; ein Schritt, Rückgängig Byte für Byte; gleiche Datei gleiche Bytes", () =>
        {
            Kartenentwurf MitHuegel()
            {
                var e = Leer();
                e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 20, Staerke: 3, Kante: 0), new[] { O(-300, 0) });
                return e;
            }
            var werte = Bild(30, 10, (a, b) => 500 + a * 10);
            var lage = Zentimeter(30, 10, 200, 48000, 0); // x 480 … 538 m: über den Rand der Karte (500) hinaus
            var (a, b) = (MitHuegel(), MitHuegel());
            var vorher = a.ZuJson();
            var bericht = a.HoehenkarteEinlesen(werte, lage);
            b.HoehenkarteEinlesen(werte, lage);
            Pruefe.Gleich(a.ZuJson(), b.ZuJson(), "gleiche Datei, gleiche Bytes");
            Pruefe.Gleich(11 * 10, bericht.Abgedeckt, "nur x 480 … 500 (11 Punkte je Zeile) liegt in der Karte");
            Pruefe.Gleich(500 + 10 * 10, Cm(a, 500, 0));
            Pruefe.Gleich(0, Cm(a, 502, 0), "außerhalb der Karte nichts");
            Pruefe.Gleich(0, Cm(a, 478, 0), "vor dem Bild nichts");
            Pruefe.Gleich(300, Cm(a, -300, 0), "der Hügel bleibt");
            var nachher = a.ZuJson();
            Pruefe.Wahr(a.Rueckgaengig(), "Rückgängig");
            Pruefe.Gleich(vorher, a.ZuJson(), "Rückgängig: Byte für Byte");
            a.Wiederholen();
            Pruefe.Gleich(nachher, a.ZuJson(), "Wiederholen: Byte für Byte");
            var schritte = a.Geschichte.Schritte;
            var draussen = a.HoehenkarteEinlesen(werte, lage with { UrsprungXCm = 90000 });
            Pruefe.Gleich((0, 0), (draussen.Abgedeckt, draussen.Geaendert));
            Pruefe.Gleich(schritte, a.Geschichte.Schritte, "ganz draußen: kein Schritt");
        }),

        ("Ausgeben und wieder einlesen: dieselben Höhen (Stufe 1 cm), ein Pixel je Rasterpunkt der ganzen Karte; engeres unten/oben begrenzt und zählt", () =>
        {
            var e = Leer();
            e.GelaendePinseln(new Pinsel(Pinselart.Rauschen, Radius: 150, Staerke: 6, Kante: 0.3, Startwert: 4, Massstab: 30), new[] { O(-100, 0), O(150, 80) });
            e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 60, Staerke: 20, Kante: 0.8), new[] { O(300, -200) });
            e.GelaendePinseln(new Pinsel(Pinselart.Senken, Radius: 40, Staerke: 15, Kante: 0.8), new[] { O(-350, 250) });
            var (werte, lage, begrenzt) = e.HoehenkarteAusgeben();
            Pruefe.Gleich((501, 451, 200, -50000, -45000), (lage.Breite, lage.Tiefe, lage.PixelCm, lage.UrsprungXCm, lage.UrsprungZCm));
            Pruefe.Gleich(0, begrenzt);
            var datei = Hoehenkarte.Schreiben(werte);
            Pruefe.Gleich(2 * 501 * 451, datei.Length);
            var f = Leer();
            f.HoehenkarteEinlesen(Hoehenkarte.Lesen(datei, lage.Breite).Werte, lage);
            Pruefe.Gleich(e.Hoehen.AlsJson()!.ToJsonString(), f.Hoehen.AlsJson()!.ToJsonString(), "wieder eingelesen: dieselben Kacheln");
            var (_, _, eng) = e.HoehenkarteAusgeben(0, 500);
            Pruefe.Wahr(eng > 1000, $"außerhalb von 0 … 5 m: {eng} Punkte begrenzt");
            Pruefe.WirftMitMeldung(() => e.HoehenkarteAusgeben(500, 500), "höher liegen");
        }),

        ("Kartenskript: hoehenkarte-einlesen braucht Quelle und Lizenz, meldet Größe vorher/nachher und Abschneiden, trägt die Herkunft ein; hoehenkarte-ausgeben nennt die Angaben", () =>
        {
            var ordner = Path.Combine(Path.GetTempPath(), "reichsgilde-hoehenkarte-" + Guid.NewGuid().ToString("N"));
            try
            {
                var o = new Projektordner(ordner);
                Directory.CreateDirectory(o.Daten);
                Directory.CreateDirectory(o.Karten);
                foreach (var name in new[] { "katalog.json", "wirtschaft.json" }) File.Copy(Path.Combine(Datenordner, name), Path.Combine(o.Daten, name));
                File.Copy(Datenpfad.Karte("messstadt"), o.Karte("messstadt"));
                // eine Kuppe im Randstreifen (x −480 … −420, z 280 … 340), 0 … 20 m
                var bild = Path.Combine(ordner, "kuppe.r16");
                File.WriteAllBytes(bild, Hoehenkarte.Schreiben(Bild(31, 31, (a, b) => Math.Max(0, 65535 - ((a - 15) * (a - 15) + (b - 15) * (b - 15)) * 300))));
                var vorher = File.ReadAllText(o.Karte("messstadt"));
                var aus = new StringWriter();
                var herkunft = new Hoehendatenherkunft("Testmuster (Kuppe, eigenes Skript)", "eigene Daten", "2026-10-31");
                Pruefe.Gleich(2, Befehle.HoehenkarteEinlesen(o, "messstadt", bild, 2, O(-480, 280), 0, 20, 0, 0, 5, false, herkunft with { Lizenz = null }, aus));
                Pruefe.Wahr(aus.ToString().Contains("--lizenz fehlt"), aus.ToString());
                Pruefe.Gleich(vorher, File.ReadAllText(o.Karte("messstadt")), "ohne Herkunft nichts geschrieben");
                Pruefe.Wahr(!File.Exists(Befehle.HoehendatenPfad(o)), "keine Herkunft eingetragen");
                aus = new StringWriter();
                Pruefe.Gleich(0, Befehle.HoehenkarteEinlesen(o, "messstadt", bild, 2, O(-480, 280), 0, 20, 0, 0, 5, false, herkunft, aus), aus.ToString());
                var text = aus.ToString();
                Pruefe.Wahr(text.Contains("961 Rasterpunkte abgedeckt") && text.Contains("hoehen: ") && text.Contains("(vorher 0)") && text.Contains("Kartendatei:"), text);
                Pruefe.Wahr(!text.Contains("Warnung"), "nichts abgeschnitten: " + text);
                var e = Kartenentwurf.AusJson(File.ReadAllText(o.Karte("messstadt")), Katalog.Value);
                Pruefe.Gleich(2000, Cm(e, -450, 310), "Kuppe 20 m");
                var eintrag = File.ReadAllText(Befehle.HoehendatenPfad(o));
                var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(bild))).ToLowerInvariant();
                Pruefe.Wahr(eintrag.StartsWith("# Höhendaten der Stadtskripte") && eintrag.Contains($"| messstadt | kuppe.r16 | `{sha}` | 31 × 31 px zu 2 m, Ursprung (-480, 280)")
                    && eintrag.Contains("| Testmuster (Kuppe, eigenes Skript) | eigene Daten | 2026-10-31 |"), eintrag);
                // zu hoch: 0 … 100 m schneidet die Kuppe bei 60 m ab – mit Warnung, geschrieben wird trotzdem
                aus = new StringWriter();
                Pruefe.Gleich(0, Befehle.HoehenkarteEinlesen(o, "messstadt", bild, 2, O(-480, 280), 0, 100, 0, 0, 5, false, herkunft, aus), aus.ToString());
                Pruefe.Wahr(aus.ToString().Contains("Warnung:") && aus.ToString().Contains("über +60 m"), aus.ToString());
                Pruefe.Gleich(2, File.ReadAllText(Befehle.HoehendatenPfad(o)).Split('\n').Count(z => z.Contains("| messstadt |")), "zwei Einträge");
                // ohne Lage: falscher Aufruf
                Pruefe.Gleich(2, Befehle.HoehenkarteEinlesen(o, "messstadt", bild, null, O(-480, 280), 0, 20, 0, 0, 5, false, herkunft, new StringWriter()));
                // ausgeben
                aus = new StringWriter();
                var ausgabe = Path.Combine(ordner, "messstadt.r16");
                Pruefe.Gleich(0, Befehle.HoehenkarteAusgeben(o, "messstadt", ausgabe, null, null, false, aus), aus.ToString());
                var g = Kartenentwurf.AusJson(File.ReadAllText(o.Karte("messstadt")), Katalog.Value);
                var (_, lage, _) = g.HoehenkarteAusgeben();
                Pruefe.Gleich(2L * lage.Breite * lage.Tiefe, new FileInfo(ausgabe).Length);
                Pruefe.Wahr(aus.ToString().Contains($"Wieder einlesen: hoehenkarte-einlesen messstadt {ausgabe} --breite={lage.Breite} --pixel=2"), aus.ToString());
            }
            finally
            {
                if (Directory.Exists(ordner)) Directory.Delete(ordner, recursive: true);
            }
        }),
    };
}
