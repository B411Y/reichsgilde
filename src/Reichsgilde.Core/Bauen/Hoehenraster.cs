using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Das Höhenraster einer Karte (Kartenwerkstatt, Schritt 5b: Gelände)
//
//  Alle 2 m ein Höhenpunkt, auf dem Boden fest verankert: Punkt (i, j) liegt bei x = 2·i, z = 2·j (Meter, x = Osten, z = Süden). So bleibt
//  jede Höhe an ihrem Ort, wenn Größe oder Rand der Karte sich ändern (Schritt 5a) – nur der Ausschnitt, den die Karte zeigt, wandert.
//  Höhen in ganzen Zentimetern (Int16) von −20 m bis +60 m; zwischen den Punkten wird bilinear gerechnet.
//
//  Gespeichert wird in Kacheln zu 64 × 64 Punkten (128 × 128 m), ebenfalls am Boden verankert (Kachel (ti, tj) hat die Punkte
//  i = 64·ti … 64·ti + 63). Nur Kacheln, in denen etwas nicht 0 ist, stehen im Speicher und in der Datei – eine flache Karte hat
//  keine einzige, ihre Datei bleibt, wie sie war. Eine Kachel in der Datei (Feld „hoehen“, docs/LEVELDESIGN.md):
//    1. Jeder Punkt wird aus seinen Nachbarn vorhergesagt (links + oben − links oben, wie bei PNG „Paeth“ ohne Auswahl); gespeichert
//       wird nur die Abweichung. Auf einem gleichmäßigen Hang ist sie 0.
//    2. Die Abweichungen als kleine Zahlen (Zickzack: 0, −1, 1, −2 … → 0, 1, 2, 3 …), je Zahl ein Halbbyte (4 Bit): 0 bis 13 stehen für
//       sich; 14 leitet einen Lauf von 4 bis 256 Nullen ein (zwei Halbbytes: Länge − 1); 15 eine größere Zahl (danach Zahl − 14 mit
//       3 Bit je Halbbyte, das vierte Bit heißt „es geht weiter“). Zwei Halbbytes je Byte, das erste oben; am Schluss evtl. ein Füll-Halbbyte 0.
//    3. Die Bytes als Base64-Text. (Bewusst kein Deflate: Das packt nicht auf jedem Rechner gleich, die Datei soll es aber sein.)
//  Eine Kachel, die nicht geändert wurde, wird genau so geschrieben, wie sie gelesen wurde (Byte für Byte dieselbe Datei).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Das Höhenraster einer Karte (siehe Kopf der Datei). Leer = flach.</summary>
[JsonConverter(typeof(HoehenrasterJson))]
public sealed class Hoehenraster
{
    /// <summary>Abstand der Höhenpunkte in Metern.</summary>
    public const double Abstand = 2;

    /// <summary>Tiefster und höchster erlaubter Punkt in Metern.</summary>
    public const double Tiefste = -20, Hoechste = 60;

    /// <summary>Dieselben Grenzen in Zentimetern (so steht es im Raster).</summary>
    public const short TiefsteCm = -2000, HoechsteCm = 6000;

    /// <summary>Punkte je Kachelseite (64; die Rechnung nutzt <c>i &gt;&gt; 6</c> und <c>i &amp; 63</c>).</summary>
    public const int Kachel = 64;

    private const int Schieben = 6, Maske = Kachel - 1;

    /// <summary>Die Kacheln, in denen etwas steht: je Kachel 64 × 64 Werte in cm, Zeile für Zeile (z), in der Zeile nach x.</summary>
    private readonly Dictionary<(int I, int J), short[]> _kacheln = new();

    /// <summary>Kacheln, wie sie in der Datei standen (Base64) – gelöscht, sobald die Kachel sich ändert.</summary>
    private readonly Dictionary<(int I, int J), string> _gepackt = new();

    /// <summary>Zählt jede Änderung (für Zwischenspeicher, die vom Raster abhängen).</summary>
    public int Stand { get; private set; }

    /// <summary>Hat das Raster keine einzige Kachel (dann ist überall 0)?</summary>
    public bool IstLeer => _kacheln.Count == 0;

    /// <summary>Die Kacheln, in denen etwas steht (Kachelindex, nicht Punktindex).</summary>
    public IReadOnlyCollection<(int I, int J)> Kacheln => _kacheln.Keys;

    // ── Lesen ────────────────────────────────────────────────────────────────

    /// <summary>Der Rasterpunkt links bzw. oben von einer Koordinate (Meter → Index).</summary>
    public static int Punktindex(double meter) => (int)Math.Floor(meter / Abstand + 1e-9);

    /// <summary>
    /// Die Rasterpunkte, die eine Karte mit diesem Rechteck braucht (Spielfeld und Rand): jede Kante auf den nächsten Punkt nach außen –
    /// liegt die Kante zwischen zwei Punkten, gehören beide dazu.
    /// </summary>
    public static (int I0, int J0, int I1, int J1) Punkte(Kartenrechteck r) =>
        ((int)Math.Floor(r.X0 / Abstand + 1e-9), (int)Math.Floor(r.Z0 / Abstand + 1e-9), (int)Math.Ceiling(r.X1 / Abstand - 1e-9), (int)Math.Ceiling(r.Z1 / Abstand - 1e-9));

    /// <summary>Die Höhe eines Rasterpunkts in cm.</summary>
    public short Wert(int i, int j) => _kacheln.TryGetValue((i >> Schieben, j >> Schieben), out var k) ? k[((j & Maske) << Schieben) | (i & Maske)] : (short)0;

    /// <summary>Die Höhe in Metern an einem Ort (bilinear zwischen den vier Punkten der Zelle).</summary>
    public double Hoehe(double x, double z)
    {
        if (_kacheln.Count == 0) return 0;
        var (fx, fz) = (x / Abstand, z / Abstand);
        var (i, j) = ((int)Math.Floor(fx), (int)Math.Floor(fz));
        var (tx, tz) = (fx - i, fz - j);
        double a = Wert(i, j), b = Wert(i + 1, j), c = Wert(i, j + 1), d = Wert(i + 1, j + 1);
        return ((a + (b - a) * tx) * (1 - tz) + (c + (d - c) * tx) * tz) / 100;
    }

    /// <summary>Die Steigung an einem Ort: Höhenänderung je Meter nach Osten (x) und nach Süden (z), aus der Zelle (0,1 = 10 %).</summary>
    public (double X, double Z) Steigung(double x, double z)
    {
        if (_kacheln.Count == 0) return (0, 0);
        var (fx, fz) = (x / Abstand, z / Abstand);
        var (i, j) = ((int)Math.Floor(fx), (int)Math.Floor(fz));
        var (tx, tz) = (fx - i, fz - j);
        double a = Wert(i, j), b = Wert(i + 1, j), c = Wert(i, j + 1), d = Wert(i + 1, j + 1);
        return (((b - a) * (1 - tz) + (d - c) * tz) / (100 * Abstand), ((c - a) * (1 - tx) + (d - b) * tx) / (100 * Abstand));
    }

    /// <summary>
    /// Liest einen Ausschnitt der Rasterpunkte in Metern: <paramref name="breite"/> × <paramref name="tiefe"/> Punkte ab (i0, j0), Zeile für
    /// Zeile (j), in der Zeile nach i – für die Kacheln der Darstellung.
    /// </summary>
    public float[] Ausschnitt(int i0, int j0, int breite, int tiefe)
    {
        var aus = new float[breite * tiefe];
        if (_kacheln.Count == 0) return aus;
        for (var j = 0; j < tiefe; j++)
        {
            var i = 0;
            while (i < breite)
            {
                // ein Lauf bis zum Ende der Kachel
                var gi = i0 + i;
                var lauf = Math.Min(breite - i, Kachel - (gi & Maske));
                if (_kacheln.TryGetValue((gi >> Schieben, (j0 + j) >> Schieben), out var k))
                {
                    var basis = (((j0 + j) & Maske) << Schieben) | (gi & Maske);
                    for (var n = 0; n < lauf; n++) aus[j * breite + i + n] = k[basis + n] / 100f;
                }
                i += lauf;
            }
        }
        return aus;
    }

    /// <summary>Steht irgendwo eine Höhe ungleich 0?</summary>
    public bool HatHoehen => _kacheln.Values.Any(k => Array.Exists(k, v => v != 0));

    // ── Schreiben (für die Pinsel; die Kartenwerkstatt ändert nur über Befehle) ──

    /// <summary>Setzt die Höhe eines Rasterpunkts in cm (auf −20 … +60 m begrenzt). Legt die Kachel an, wenn nötig.</summary>
    internal void WertSetzen(int i, int j, int cm)
    {
        var w = (short)Math.Clamp(cm, TiefsteCm, HoechsteCm);
        var schluessel = (i >> Schieben, j >> Schieben);
        if (!_kacheln.TryGetValue(schluessel, out var k))
        {
            if (w == 0) return;
            _kacheln[schluessel] = k = new short[Kachel * Kachel];
        }
        k[((j & Maske) << Schieben) | (i & Maske)] = w;
        _gepackt.Remove(schluessel);
        Stand++;
    }

    /// <summary>Setzt alle Punkte von (i0, j0) bis (i1, j1) (einschließlich) auf 0; Kacheln, die danach ganz 0 sind, fallen weg.</summary>
    internal void BereichLeeren(int i0, int j0, int i1, int j1)
    {
        foreach (var k in _kacheln.Keys.ToList())
        {
            var (ki0, kj0) = (k.I << Schieben, k.J << Schieben);
            if (ki0 > i1 || ki0 + Maske < i0 || kj0 > j1 || kj0 + Maske < j0) continue;
            var w = _kacheln[k];
            for (var j = Math.Max(j0, kj0); j <= Math.Min(j1, kj0 + Maske); j++)
                for (var i = Math.Max(i0, ki0); i <= Math.Min(i1, ki0 + Maske); i++)
                    w[((j & Maske) << Schieben) | (i & Maske)] = 0;
            if (Array.TrueForAll(w, v => v == 0)) _kacheln.Remove(k);
            _gepackt.Remove(k);
        }
        Stand++;
    }

    /// <summary>Eine Kopie der Werte einer Kachel (null: es gibt sie nicht, dort ist alles 0).</summary>
    internal short[]? KachelKopie((int I, int J) kachel) => _kacheln.TryGetValue(kachel, out var k) ? (short[])k.Clone() : null;

    /// <summary>Ersetzt eine Kachel durch eine Kopie der Werte (null: weg, dort ist alles 0).</summary>
    internal void KachelSetzen((int I, int J) kachel, short[]? werte)
    {
        if (werte is null) _kacheln.Remove(kachel);
        else _kacheln[kachel] = (short[])werte.Clone();
        _gepackt.Remove(kachel);
        Stand++;
    }

    /// <summary>Die Kachel, in der ein Rasterpunkt liegt.</summary>
    public static (int I, int J) KachelVon(int i, int j) => (i >> Schieben, j >> Schieben);

    /// <summary>Das Rechteck einer Kachel auf dem Boden (vom ersten bis zum letzten ihrer Punkte, Meter).</summary>
    public static Kartenrechteck KachelBereich((int I, int J) k) =>
        new(k.I * Kachel * Abstand, k.J * Kachel * Abstand, (k.I * Kachel + Maske) * Abstand, (k.J * Kachel + Maske) * Abstand);

    /// <summary>Eine Kopie des ganzen Rasters.</summary>
    public Hoehenraster Kopie()
    {
        var r = new Hoehenraster();
        foreach (var (k, w) in _kacheln) r._kacheln[k] = (short[])w.Clone();
        foreach (var (k, t) in _gepackt) r._gepackt[k] = t;
        return r;
    }

    // ── Datei ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Das Raster als Feld <c>hoehen</c> der Kartendatei – null, wenn nichts darin steht (dann fällt das Feld weg). Mit <paramref name="nur"/>
    /// nur die Kacheln, die Punkte in diesem Rechteck haben (die Karte mit Rand); Kacheln, die ganz 0 sind, nie. Reihenfolge: nach j, dann i.
    /// </summary>
    public JsonObject? AlsJson(Kartenrechteck? nur = null)
    {
        var bereich = nur is { } r ? Punkte(r) : ((int I0, int J0, int I1, int J1)?)null;
        var liste = new JsonArray();
        foreach (var k in _kacheln.Keys.OrderBy(k => k.J).ThenBy(k => k.I))
        {
            if (bereich is { } b && (k.I * Kachel > b.I1 || k.I * Kachel + Maske < b.I0 || k.J * Kachel > b.J1 || k.J * Kachel + Maske < b.J0)) continue;
            var werte = _kacheln[k];
            if (Array.TrueForAll(werte, v => v == 0)) continue;
            if (!_gepackt.TryGetValue(k, out var text)) _gepackt[k] = text = Packen(werte);
            liste.Add(new JsonObject { ["i"] = k.I, ["j"] = k.J, ["daten"] = text });
        }
        if (liste.Count == 0) return null;
        return new JsonObject { ["abstand"] = 2, ["einheit"] = "cm", ["kachel"] = Kachel, ["kacheln"] = liste };
    }

    /// <summary>Liest das Feld <c>hoehen</c> (null: flach).</summary>
    /// <exception cref="KartenFehler">Wenn das Feld nicht dem Format entspricht.</exception>
    public static Hoehenraster AusJson(JsonNode? json)
    {
        var r = new Hoehenraster();
        if (json is null) return r;
        static KartenFehler Fehler(string text) => new(new[] { "hoehen: " + text });
        if (json is not JsonObject o) throw Fehler("ist kein Objekt.");
        double? Zahl(string feld) => o[feld] is JsonValue v && v.TryGetValue<double>(out var x) ? x : null;
        if (Zahl("abstand") != Abstand) throw Fehler($"abstand muss {Abstand} sein.");
        if (Zahl("kachel") != Kachel) throw Fehler($"kachel muss {Kachel} sein.");
        if (o["einheit"] is not JsonValue ve || !ve.TryGetValue<string>(out var einheit) || einheit != "cm") throw Fehler("einheit muss „cm“ sein.");
        if (o["kacheln"] is not JsonArray liste) throw Fehler("kacheln fehlt oder ist keine Liste.");
        foreach (var e in liste)
        {
            if (e is not JsonObject k || k["i"] is not JsonValue vi || !vi.TryGetValue<int>(out var i) || k["j"] is not JsonValue vj
                || !vj.TryGetValue<int>(out var j) || k["daten"] is not JsonValue vd || !vd.TryGetValue<string>(out var daten))
                throw Fehler("Jede Kachel hat i, j (ganze Zahlen) und daten (Text).");
            if (r._kacheln.ContainsKey((i, j))) throw Fehler($"Die Kachel ({i}, {j}) steht doppelt.");
            r._kacheln[(i, j)] = Entpacken(daten) ?? throw Fehler($"Die Kachel ({i}, {j}) ist nicht lesbar.");
            r._gepackt[(i, j)] = daten;
        }
        return r;
    }

    /// <summary>Halbbyte für einen Lauf von Nullen (danach zwei Halbbytes: Länge − 1) bzw. für eine größere Zahl (danach die Zahl − 14 in Halbbytes).</summary>
    private const int Lauf = 14, Gross = 15;

    /// <summary>Ab so vielen Nullen hintereinander lohnt ein Lauf (3 Halbbytes statt einer je Null).</summary>
    private const int LaufAb = 4;

    /// <summary>Packt eine Kachel (siehe Kopf der Datei).</summary>
    internal static string Packen(short[] w)
    {
        var abweichung = new uint[Kachel * Kachel];
        for (var j = 0; j < Kachel; j++)
            for (var i = 0; i < Kachel; i++)
            {
                var r = w[(j << Schieben) | i] - Vorhersage(w, i, j);
                abweichung[(j << Schieben) | i] = (uint)((r << 1) ^ (r >> 31)); // Zickzack
            }
        var halb = new List<byte>(Kachel * Kachel);
        for (var k = 0; k < abweichung.Length;)
        {
            var u = abweichung[k];
            if (u == 0)
            {
                var n = 1;
                while (k + n < abweichung.Length && n < 256 && abweichung[k + n] == 0) n++;
                if (n >= LaufAb)
                {
                    halb.Add(Lauf);
                    halb.Add((byte)((n - 1) >> 4));
                    halb.Add((byte)((n - 1) & 15));
                    k += n;
                    continue;
                }
            }
            if (u < Lauf) halb.Add((byte)u);
            else
            {
                // größere Zahlen: 3 Bit je Halbbyte, das oberste Bit sagt „es geht weiter“
                halb.Add(Gross);
                for (var v = u - Lauf; ; v >>= 3)
                {
                    if (v < 8)
                    {
                        halb.Add((byte)v);
                        break;
                    }
                    halb.Add((byte)((v & 7) | 8));
                }
            }
            k++;
        }
        var bytes = new byte[(halb.Count + 1) / 2];
        for (var k = 0; k < halb.Count; k++) bytes[k / 2] |= (byte)(k % 2 == 0 ? halb[k] << 4 : halb[k]);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>Entpackt eine Kachel – null, wenn der Text kein gültiges Paket ist (falsche Länge, Werte außerhalb von −20 … +60 m).</summary>
    internal static short[]? Entpacken(string text)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(text);
        }
        catch (FormatException)
        {
            return null;
        }
        var (pos, ende) = (0, bytes.Length * 2);
        int Halb()
        {
            if (pos >= ende) return -1;
            var h = pos % 2 == 0 ? bytes[pos / 2] >> 4 : bytes[pos / 2] & 15;
            pos++;
            return h;
        }
        var abweichung = new uint[Kachel * Kachel];
        for (var k = 0; k < abweichung.Length;)
        {
            var h = Halb();
            if (h < 0) return null;
            if (h == Lauf)
            {
                var (a, b) = (Halb(), Halb());
                if (a < 0 || b < 0) return null;
                var n = (a << 4 | b) + 1;
                if (k + n > abweichung.Length) return null;
                k += n; // die Nullen stehen schon da
                continue;
            }
            if (h < Lauf)
            {
                abweichung[k++] = (uint)h;
                continue;
            }
            uint v = 0;
            for (var schiebe = 0; ; schiebe += 3)
            {
                var x = Halb();
                if (x < 0 || schiebe > 27) return null;
                v |= (uint)(x & 7) << schiebe;
                if (x < 8) break;
            }
            abweichung[k++] = v + Lauf;
        }
        // höchstens ein Füll-Halbbyte (0) am Schluss
        if (ende - pos > 1 || (ende - pos == 1 && Halb() != 0)) return null;
        var w = new short[Kachel * Kachel];
        for (var j = 0; j < Kachel; j++)
            for (var i = 0; i < Kachel; i++)
            {
                var u = abweichung[(j << Schieben) | i];
                var v = (long)((int)(u >> 1) ^ -(int)(u & 1)) + Vorhersage(w, i, j);
                if (v < TiefsteCm || v > HoechsteCm) return null;
                w[(j << Schieben) | i] = (short)v;
            }
        return w;
    }

    /// <summary>Die Vorhersage eines Punkts aus den schon bekannten Nachbarn: links + oben − links oben (am Rand der Nachbar, den es gibt).</summary>
    private static int Vorhersage(short[] w, int i, int j)
    {
        if (i > 0 && j > 0) return w[(j << Schieben) | (i - 1)] + w[((j - 1) << Schieben) | i] - w[((j - 1) << Schieben) | (i - 1)];
        if (i > 0) return w[(j << Schieben) | (i - 1)];
        return j > 0 ? w[((j - 1) << Schieben) | i] : 0;
    }
}

/// <summary>Schreibt und liest das Höhenraster im Spielstand genau wie in der Kartendatei (Feld <c>hoehen</c>).</summary>
public sealed class HoehenrasterJson : JsonConverter<Hoehenraster>
{
    public override Hoehenraster Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var json = JsonNode.Parse(ref reader);
        try
        {
            return Hoehenraster.AusJson(json);
        }
        catch (KartenFehler ex)
        {
            throw new JsonException(string.Join(" ", ex.Fehler));
        }
    }

    public override void Write(Utf8JsonWriter writer, Hoehenraster value, JsonSerializerOptions options)
    {
        if (value.AlsJson() is { } json) json.WriteTo(writer);
        else writer.WriteNullValue();
    }
}
