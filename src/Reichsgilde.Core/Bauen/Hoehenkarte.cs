using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Höhenkarten einlesen und ausgeben (Kartenskript und Kartenwerkstatt, Terrain3D-Übernahme Etappe 2)
//
//  Eine Höhenkarte im Format r16 (auch .raw) ist ein Bild ohne Kopf: Zeile für Zeile (von Norden nach Süden, z wächst), in der Zeile von
//  Westen nach Osten (x wächst), je Pixel eine Zahl 0 … 65535 in zwei Bytes (Vorgabe: niedriges Byte zuerst, „little endian“; Bildprogramme
//  am Mac schreiben oft das hohe zuerst). Breite und Tiefe stehen nicht in der Datei: Ist die Breite nicht angegeben, muss das Bild quadratisch
//  sein. So geben Terrain3D, World Machine, Gaea, QGIS und Blender Höhen weiter.
//
//  Einlesen (Hoehenkartenlage): Das erste Pixel liegt am Ursprung (x, z in cm), die Pixel haben einen festen Abstand (cm). Der Wert 0 hat die
//  Höhe „unten“, 65535 die Höhe „oben“ (cm), dazwischen gleichmäßig; danach kommt der Versatz dazu (Köln: Gelände 37 … 60 m über NN, Versatz
//  −37 m legt das Rheinufer auf 0). Dann für jeden Punkt des 2-m-Rasters, der in der Karte und unter dem Bild liegt:
//    1. Neuabtastung: Mittel der Pixel ringsum mit Zeltgewicht (R − |dx|) · (R − |dz|), R = der größere von Pixelabstand und 2 m. Bei
//       Pixeln ab 2 m ist das genau die bilineare Mitte der vier Nachbarn; feinere Pixel (DGM 1 m) werden über 2 m gemittelt statt einzeln
//       gepickt – sonst flimmerten kleine Buckel als Zufall ins Raster.
//    2. Quantisieren: auf ein Vielfaches der Stufe (Vorgabe 5 cm) runden. Das Packen der Kacheln (Hoehenraster: Vorhersage, Halbbytes,
//       Läufe von Nullen) lebt von gleichen Abständen – Messrauschen im Zentimeter würde die Datei ein Vielfaches größer machen.
//    3. Auf −20 … +60 m begrenzen; was abgeschnitten wird, zählt der Bericht (tiefster und höchster Wert dazu) – das ist die Warnung.
//  Punkte außerhalb des Bildes bleiben, wie sie sind; außerhalb der Karte (Spielfeld und Rand) wird nichts geschrieben. Das Einlesen ist ein
//  Schritt mit Rückgängig (Kartenentwurf.HoehenkarteEinlesen).
//  Alles ganzzahlig (long, für das Produkt Int128), Abtasten, Maßstab, Versatz und Stufe in einer einzigen Division, gerundet halb weg von 0:
//  dieselbe Datei gibt auf jedem Rechner dieselben Bytes.
//
//  Ausgeben: das Raster der Karte als r16, ein Pixel je Rasterpunkt (2 m), Ursprung am ersten Punkt; „unten“ und „oben“ ohne Angabe die
//  tiefste und höchste Höhe. Wieder eingelesen mit denselben Angaben und Stufe 1 cm ergibt das dieselben Höhen (die Spanne ist höchstens
//  80 m = 8.000 cm, 65.535 Stufen reichen für jeden Zentimeter).
//  Woher eine eingelesene Höhenkarte stammt (Quelle, Lizenz, Abruf), hält das Kartenskript fest (tools/Kartenbau/Staedte/HOEHENDATEN.md).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Wo und wie hoch eine Höhenkarte liegt (siehe Kopf der Datei): Größe in Pixeln, Pixelabstand und Ursprung (erstes Pixel) in cm, Höhe der Werte
/// 0 und 65535 in cm, Versatz in cm (wird dazugezählt) und Stufe der Quantisierung in cm.
/// </summary>
public sealed record Hoehenkartenlage(int Breite, int Tiefe, int PixelCm, int UrsprungXCm, int UrsprungZCm, int UntenCm, int ObenCm,
    int VersatzCm = 0, int StufeCm = 5)
{
    /// <summary>Kleinster und größter Pixelabstand (cm).</summary>
    public const int MinPixelCm = 10, MaxPixelCm = 10_000;

    /// <summary>Größte Stufe der Quantisierung (cm).</summary>
    public const int MaxStufeCm = 100;

    /// <summary>Größter Betrag von unten, oben und Versatz (cm, 10 km) – schützt die Rechnung vor unsinnigen Angaben.</summary>
    public const int MaxBetragCm = 1_000_000;

    /// <summary>Was an den Angaben nicht stimmt (leer: in Ordnung).</summary>
    public IReadOnlyList<string> Fehler()
    {
        var fehler = new List<string>();
        if (Breite < 1 || Tiefe < 1) fehler.Add($"Die Höhenkarte hat keine Pixel ({Breite} × {Tiefe}).");
        if (PixelCm is < MinPixelCm or > MaxPixelCm)
            fehler.Add($"Der Pixelabstand muss zwischen {Meter(MinPixelCm)} und {Meter(MaxPixelCm)} m liegen (ist {Meter(PixelCm)} m).");
        if (ObenCm <= UntenCm) fehler.Add("„oben“ (Höhe des Werts 65535) muss höher liegen als „unten“ (Höhe des Werts 0).");
        if (Math.Abs((long)UntenCm) > MaxBetragCm || Math.Abs((long)ObenCm) > MaxBetragCm || Math.Abs((long)VersatzCm) > MaxBetragCm)
            fehler.Add($"„unten“, „oben“ und der Versatz müssen zwischen −{MaxBetragCm / 100_000} und +{MaxBetragCm / 100_000} km liegen.");
        if (StufeCm is < 1 or > MaxStufeCm) fehler.Add($"Die Stufe muss zwischen 1 und {MaxStufeCm} cm liegen (ist {StufeCm} cm).");
        return fehler;
    }

    private static string Meter(int cm) => Kartenschreiber.Zahl(cm / 100.0).Replace('.', ',');
}

/// <summary>
/// Was beim Einlesen geschah: Rasterpunkte unter dem Bild (in der Karte), davon geändert, abgeschnitten unter −20 bzw. über +60 m mit dem
/// tiefsten und höchsten Wert vor dem Begrenzen (cm), und das geänderte Rechteck.
/// </summary>
public sealed record Hoehenkartenbericht(int Abgedeckt, int Geaendert, int ZuTief, int ZuHoch, int TiefsteCm, int HoechsteCm, Kartenrechteck? Bereich)
{
    /// <summary>Wurde etwas abgeschnitten?</summary>
    public bool Abgeschnitten => ZuTief + ZuHoch > 0;

    /// <summary>Die Warnung für den Bericht (null: nichts abgeschnitten).</summary>
    public string? Warnung => !Abgeschnitten ? null
        : $"Warnung: {ZuTief + ZuHoch} Punkte abgeschnitten ({ZuTief} unter −20 m, {ZuHoch} über +60 m; die Höhenkarte reicht von {Meter(TiefsteCm)} bis {Meter(HoechsteCm)} m) – Versatz oder unten/oben anpassen.";

    private static string Meter(int cm) => Kartenschreiber.Zahl(cm / 100.0).Replace('.', ',');
}

/// <summary>Lesen, Einlesen und Ausgeben von Höhenkarten (siehe Kopf der Datei).</summary>
public static class Hoehenkarte
{
    /// <summary>Der größte Wert eines Pixels.</summary>
    public const int Groesster = ushort.MaxValue;

    /// <summary>Abstand der Rasterpunkte in cm.</summary>
    private const int RasterCm = (int)(Hoehenraster.Abstand * 100);

    // ── Datei ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Liest eine r16/raw-Datei: je Pixel zwei Bytes, Zeile für Zeile. Ohne <paramref name="breite"/> muss das Bild quadratisch sein.
    /// </summary>
    /// <exception cref="ArgumentException">Bei ungerader Länge, leerer Datei oder einer Länge, die nicht zur Breite passt.</exception>
    public static (ushort[] Werte, int Breite, int Tiefe) Lesen(byte[] daten, int breite = 0, bool hohesZuerst = false)
    {
        if (daten.Length == 0 || daten.Length % 2 != 0) throw new ArgumentException($"Eine r16-Datei hat zwei Bytes je Pixel – diese hat {daten.Length} Bytes.");
        var anzahl = daten.Length / 2;
        if (breite <= 0)
        {
            breite = (int)Math.Sqrt(anzahl);
            while ((long)breite * breite > anzahl) breite--;
            while ((long)(breite + 1) * (breite + 1) <= anzahl) breite++;
            if ((long)breite * breite != anzahl) throw new ArgumentException($"{anzahl} Pixel sind kein Quadrat – bitte die Breite angeben.");
        }
        if (anzahl % breite != 0) throw new ArgumentException($"{anzahl} Pixel lassen sich nicht in Zeilen zu {breite} teilen.");
        var werte = new ushort[anzahl];
        for (var n = 0; n < anzahl; n++)
            werte[n] = hohesZuerst ? (ushort)(daten[2 * n] << 8 | daten[2 * n + 1]) : (ushort)(daten[2 * n] | daten[2 * n + 1] << 8);
        return (werte, breite, anzahl / breite);
    }

    /// <summary>Schreibt Werte als r16 (zwei Bytes je Pixel, Vorgabe: niedriges Byte zuerst).</summary>
    public static byte[] Schreiben(ushort[] werte, bool hohesZuerst = false)
    {
        var daten = new byte[werte.Length * 2];
        for (var n = 0; n < werte.Length; n++)
        {
            var (hoch, tief) = ((byte)(werte[n] >> 8), (byte)werte[n]);
            (daten[2 * n], daten[2 * n + 1]) = hohesZuerst ? (hoch, tief) : (tief, hoch);
        }
        return daten;
    }

    // ── Einlesen ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Rechnet die Höhenkarte auf das Raster und schreibt sie in eine Änderung (für Rückgängig): Punkte in <paramref name="karte"/> unter dem
    /// Bild bekommen ihre Höhe, alle anderen bleiben (siehe Kopf der Datei).
    /// </summary>
    /// <exception cref="ArgumentException">Wenn die Lage nicht stimmt oder die Zahl der Werte nicht zu ihr passt.</exception>
    public static Hoehenkartenbericht Einlesen(Hoehenraster raster, ushort[] werte, Hoehenkartenlage lage, Kartenrechteck karte, out Gelaendeaenderung aenderung)
    {
        if (lage.Fehler() is { Count: > 0 } fehler) throw new ArgumentException(string.Join(" ", fehler));
        if (werte.Length != (long)lage.Breite * lage.Tiefe) throw new ArgumentException($"Die Höhenkarte hat {werte.Length} Pixel, die Lage nennt {lage.Breite} × {lage.Tiefe}.");
        aenderung = new Gelaendeaenderung(raster, karte);
        var p = lage.PixelCm;
        // Rasterpunkte unter dem Bild: von Ursprung bis Ursprung + (Breite − 1) · Pixel, jeweils mit Rand eingeschlossen
        var abdeckung = aenderung.PunkteIn(new Kartenrechteck(lage.UrsprungXCm / 100.0, lage.UrsprungZCm / 100.0,
            (lage.UrsprungXCm + (long)(lage.Breite - 1) * p) / 100.0, (lage.UrsprungZCm + (long)(lage.Tiefe - 1) * p) / 100.0));
        var (abgedeckt, geaendert, zuTief, zuHoch, tiefste, hoechste) = (0, 0, 0, 0, int.MaxValue, int.MinValue);
        for (var j = abdeckung.J0; j <= abdeckung.J1; j++)
        {
            var dz = (long)j * RasterCm - lage.UrsprungZCm;
            if (dz < 0 || dz > (long)(lage.Tiefe - 1) * p) continue;
            for (var i = abdeckung.I0; i <= abdeckung.I1; i++)
            {
                var dx = (long)i * RasterCm - lage.UrsprungXCm;
                if (dx < 0 || dx > (long)(lage.Breite - 1) * p) continue;
                var cm = HoeheAm(werte, lage, dx, dz);
                abgedeckt++;
                (tiefste, hoechste) = (Math.Min(tiefste, cm), Math.Max(hoechste, cm));
                if (cm < Hoehenraster.TiefsteCm) (zuTief, cm) = (zuTief + 1, Hoehenraster.TiefsteCm);
                else if (cm > Hoehenraster.HoechsteCm) (zuHoch, cm) = (zuHoch + 1, Hoehenraster.HoechsteCm);
                if (cm != raster.Wert(i, j)) geaendert++;
                aenderung.Setzen(i, j, cm);
            }
        }
        return new Hoehenkartenbericht(abgedeckt, geaendert, zuTief, zuHoch, abgedeckt == 0 ? 0 : tiefste, abgedeckt == 0 ? 0 : hoechste, aenderung.Bereich);
    }

    /// <summary>
    /// Die Höhe (cm, vor dem Begrenzen) an einer Stelle <paramref name="dx"/>, <paramref name="dz"/> (cm ab dem Ursprung, im Bild): Zeltmittel der
    /// Pixel ringsum, dann unten/oben, Versatz und Stufe.
    /// </summary>
    internal static int HoeheAm(ushort[] werte, Hoehenkartenlage lage, long dx, long dz)
    {
        var p = lage.PixelCm;
        long r = Math.Max(p, RasterCm);
        // Pixel mit |Abstand| < R: a von floor((dx − R) / p) + 1 bis ceil((dx + R) / p) − 1, im Bild
        var (a0, a1) = (Math.Max(0, AbRunden(dx - r, p) + 1), Math.Min(lage.Breite - 1, AufRunden(dx + r, p) - 1));
        var (b0, b1) = (Math.Max(0, AbRunden(dz - r, p) + 1), Math.Min(lage.Tiefe - 1, AufRunden(dz + r, p) - 1));
        long summeGewicht = 0, summeWert = 0;
        for (var b = b0; b <= b1; b++)
        {
            var wz = r - Math.Abs((long)b * p - dz);
            if (wz <= 0) continue;
            for (var a = a0; a <= a1; a++)
            {
                var wx = r - Math.Abs((long)a * p - dx);
                if (wx <= 0) continue;
                summeGewicht += wx * wz;
                summeWert += wx * wz * werte[(long)b * lage.Breite + a];
            }
        }
        // (unten + Versatz) + Mittel · (oben − unten) / 65535, auf die Stufe – alles in einer Division, also genau einmal halb weg von 0 gerundet
        var nenner = (Int128)summeGewicht * Groesster;
        var zaehler = ((Int128)lage.UntenCm + lage.VersatzCm) * nenner + (Int128)summeWert * ((long)lage.ObenCm - lage.UntenCm);
        var stufe = Math.Max(1, lage.StufeCm);
        return (int)(RundDiv(zaehler, nenner * stufe) * stufe);
    }

    // ── Ausgeben ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Das Raster in <paramref name="bereich"/> (die Karte mit Rand) als Höhenkarte: ein Pixel je Rasterpunkt (2 m). Ohne Angabe ist „unten“ die
    /// tiefste und „oben“ die höchste Höhe (bei einer flachen Karte 1 cm darüber). Werte außerhalb von unten … oben werden begrenzt und gezählt.
    /// </summary>
    public static (ushort[] Werte, Hoehenkartenlage Lage, int Begrenzt) Ausgeben(Hoehenraster raster, Kartenrechteck bereich, int? untenCm = null, int? obenCm = null)
    {
        var (i0, j0, i1, j1) = Hoehenraster.Punkte(bereich);
        var (breite, tiefe) = (i1 - i0 + 1, j1 - j0 + 1);
        var hoehen = new int[breite * tiefe];
        var (tiefste, hoechste) = (int.MaxValue, int.MinValue);
        for (var j = 0; j < tiefe; j++)
            for (var i = 0; i < breite; i++)
            {
                int h = raster.Wert(i0 + i, j0 + j);
                hoehen[j * breite + i] = h;
                (tiefste, hoechste) = (Math.Min(tiefste, h), Math.Max(hoechste, h));
            }
        var unten = untenCm ?? tiefste;
        var oben = obenCm ?? Math.Max(hoechste, unten + 1);
        if (oben <= unten) throw new ArgumentException("„oben“ muss höher liegen als „unten“.");
        var werte = new ushort[hoehen.Length];
        var begrenzt = 0;
        for (var n = 0; n < hoehen.Length; n++)
        {
            var v = RundDiv((Int128)(hoehen[n] - unten) * Groesster, oben - unten);
            if (v < 0 || v > Groesster) begrenzt++;
            werte[n] = (ushort)Math.Clamp((long)v, 0, Groesster);
        }
        return (werte, new Hoehenkartenlage(breite, tiefe, RasterCm, i0 * RasterCm, j0 * RasterCm, unten, oben, 0, 1), begrenzt);
    }

    // ── Ganzzahlige Hilfen ───────────────────────────────────────────────────

    /// <summary>Ganzzahlige Division, gerundet halb weg von 0 (Nenner größer als 0).</summary>
    internal static Int128 RundDiv(Int128 zaehler, Int128 nenner) =>
        zaehler >= 0 ? (2 * zaehler + nenner) / (2 * nenner) : -((-2 * zaehler + nenner) / (2 * nenner));

    private static int AbRunden(long a, long b) => (int)(a >= 0 ? a / b : -((-a + b - 1) / b));
    private static int AufRunden(long a, long b) => (int)(a >= 0 ? (a + b - 1) / b : -(-a / b));
}
