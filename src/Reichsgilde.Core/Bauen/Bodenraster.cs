using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Die Bodentexturen einer Karte (Kartenwerkstatt, Schritt 5e)
//
//  Alle 1 m ein Bodenpunkt, am Boden verankert wie das Höhenraster: Punkt (i, j) liegt bei x = i, z = j (Meter). Je Punkt ein Anteil
//  0 … 255 für jede der zehn gemalten Texturen (Erde … Mist); die Wiese ist der Grund und bekommt, was übrig bleibt (255 − Summe). Dazu
//  ein elfter Wert: die Dichte der Deko (0 = keine, 128 ≈ normal, 255 = doppelt; Bodendeko). So kann die Darstellung die Anteile direkt
//  als Splatmap nehmen (drei RGBA-Bilder, Splat) und überall weich zwischen beliebig vielen Texturen verblenden.
//
//  Gespeichert wird in Kacheln zu 64 × 64 Punkten (64 × 64 m); je Kachel nur die Werte (Kanäle), die irgendwo nicht 0 sind, und nur
//  Kacheln mit einem solchen Kanal – eine Karte ohne Bodentexturen (alles Wiese, keine Deko) hat keine einzige, ihre Datei bleibt, wie sie
//  war. Ein Kanal in der Datei ist gepackt wie eine Kachel des Höhenrasters (Vorhersage aus den Nachbarn, Halbbytes, Base64, kein Deflate:
//  auf jedem Rechner dieselbe Datei), Feld „boden“:
//    {"abstand": 1, "kachel": 64, "startwert": 1, "kacheln": [{"i": 0, "j": -1, "erde": "…", "deko": "…"}]}
//  Eine Kachel, die nicht geändert wurde, wird genau so geschrieben, wie sie gelesen wurde.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Die Bodentexturen zum Malen. Die Wiese ist der Grund (überall, wo nichts anderes gemalt ist).</summary>
public enum Bodentextur
{
    Wiese,
    Erde,
    Fels,
    Kies,
    Waldboden,
    Sand,
    Schlamm,
    Moos,
    Lehm,
    Pflaster,
    Mist,
}

/// <summary>Das Bodenraster einer Karte (siehe Kopf der Datei). Leer = überall Wiese ohne Deko.</summary>
[JsonConverter(typeof(BodenrasterJson))]
public sealed class Bodenraster
{
    /// <summary>Abstand der Bodenpunkte in Metern.</summary>
    public const double Abstand = 1;

    /// <summary>Punkte je Kachelseite (64 → 64 × 64 m).</summary>
    public const int Kachel = 64;

    /// <summary>Anzahl der Texturen (mit der Wiese).</summary>
    public const int Texturen = 11;

    /// <summary>Gespeicherte Werte je Punkt: die zehn gemalten Texturen (Kanal = Textur − 1) und die Dekodichte.</summary>
    internal const int Kanaele = 11;

    /// <summary>Der Kanal der Dekodichte.</summary>
    internal const int DekoKanal = 10;

    /// <summary>Dekodichte, die „normal“ heißt (Faktor 1 in <see cref="Dekofaktor"/>).</summary>
    public const double DekoNormal = 127.5;

    private const int Schieben = 6, Maske = Kachel - 1, Punkte = Kachel * Kachel;

    /// <summary>Je Kachel die Kanäle (null: dort überall 0), je Kanal 64 × 64 Werte, Zeile für Zeile (z), in der Zeile nach x.</summary>
    private readonly Dictionary<(int I, int J), byte[]?[]> _kacheln = new();

    /// <summary>Kacheln, wie sie in der Datei standen (Name des Kanals → Base64) – gelöscht, sobald die Kachel sich ändert.</summary>
    private readonly Dictionary<(int I, int J), JsonObject> _gepackt = new();

    /// <summary>Startwert der Deko (aus der Datei; Vorgabe 1).</summary>
    public int Startwert { get; internal set; } = 1;

    /// <summary>Zählt jede Änderung (für Zwischenspeicher, die vom Raster abhängen).</summary>
    public int Stand { get; private set; }

    /// <summary>Hat das Raster keine einzige Kachel (dann ist überall Wiese ohne Deko)?</summary>
    public bool IstLeer => _kacheln.Count == 0;

    /// <summary>Die Kacheln, in denen etwas steht (Kachelindex).</summary>
    public IReadOnlyCollection<(int I, int J)> Kacheln => _kacheln.Keys;

    /// <summary>Der Name einer Textur in der Datei und im Katalog („wiese“, „erde“ …).</summary>
    public static string Kennung(Bodentextur t) => t.ToString().ToLowerInvariant();

    /// <summary>Die Textur zu einem Namen („erde“, Groß-/Kleinschreibung egal) – null, wenn es sie nicht gibt.</summary>
    public static Bodentextur? TexturVon(string? name) =>
        Enum.TryParse<Bodentextur>(name, ignoreCase: true, out var t) && !int.TryParse(name, out _) ? t : null;

    private static string KanalName(int kanal) => kanal == DekoKanal ? "deko" : Kennung((Bodentextur)(kanal + 1));

    // ── Lesen ────────────────────────────────────────────────────────────────

    /// <summary>Der Bodenpunkt links bzw. oben von einer Koordinate (Meter → Index).</summary>
    public static int Punktindex(double meter) => (int)Math.Floor(meter / Abstand + 1e-9);

    /// <summary>Die Bodenpunkte in einem Rechteck (Spielfeld und Rand): jede Kante auf den nächsten Punkt nach außen.</summary>
    public static (int I0, int J0, int I1, int J1) PunkteIn(Kartenrechteck r) =>
        ((int)Math.Floor(r.X0 / Abstand + 1e-9), (int)Math.Floor(r.Z0 / Abstand + 1e-9), (int)Math.Ceiling(r.X1 / Abstand - 1e-9), (int)Math.Ceiling(r.Z1 / Abstand - 1e-9));

    /// <summary>Die Kachel, in der ein Bodenpunkt liegt.</summary>
    public static (int I, int J) KachelVon(int i, int j) => (i >> Schieben, j >> Schieben);

    /// <summary>Die Punkte einer Kachel: der erste (i0, j0); sie reicht 64 Punkte weit.</summary>
    public static (int I0, int J0) KachelAnfang((int I, int J) k) => (k.I << Schieben, k.J << Schieben);

    /// <summary>Das Rechteck einer Kachel auf dem Boden (vom ersten bis zum letzten ihrer Punkte, Meter).</summary>
    public static Kartenrechteck KachelBereich((int I, int J) k) =>
        new(k.I * Kachel * Abstand, k.J * Kachel * Abstand, (k.I * Kachel + Maske) * Abstand, (k.J * Kachel + Maske) * Abstand);

    private static int Index(int i, int j) => ((j & Maske) << Schieben) | (i & Maske);

    /// <summary>Ein Kanal an einem Punkt (0 … 255).</summary>
    internal byte Kanal(int i, int j, int kanal) =>
        _kacheln.TryGetValue(KachelVon(i, j), out var k) && k[kanal] is { } w ? w[Index(i, j)] : (byte)0;

    /// <summary>Steht in der Kachel irgendwo eine Dekodichte über 0?</summary>
    public bool HatDeko((int I, int J) kachel) => _kacheln.TryGetValue(kachel, out var k) && k[DekoKanal] is not null;

    /// <summary>Hat die Kachel diesen Kanal (Textur − 1 bzw. <see cref="DekoKanal"/>)?</summary>
    internal bool HatKanal((int I, int J) kachel, int kanal) => _kacheln.TryGetValue(kachel, out var k) && k[kanal] is not null;

    /// <summary>Der Anteil einer Textur an einem Bodenpunkt (0 … 255; alle elf zusammen 255).</summary>
    public byte Gewicht(int i, int j, Bodentextur t)
    {
        if (!_kacheln.TryGetValue(KachelVon(i, j), out var k)) return t == Bodentextur.Wiese ? (byte)255 : (byte)0;
        var n = Index(i, j);
        if (t != Bodentextur.Wiese) return k[(int)t - 1] is { } w ? w[n] : (byte)0;
        var summe = 0;
        for (var c = 0; c < DekoKanal; c++)
            if (k[c] is { } w) summe += w[n];
        return (byte)(255 - summe);
    }

    /// <summary>Die Dekodichte an einem Bodenpunkt (0 = keine Deko, 127,5 = normal, 255 = doppelt).</summary>
    public byte Dekodichte(int i, int j) => Kanal(i, j, DekoKanal);

    /// <summary>Die Dekodichte als Faktor 0 … 2 (1 = normal).</summary>
    public static double Dekofaktor(byte dichte) => dichte / DekoNormal;

    /// <summary>Der Anteil einer Textur an einem Ort (0 … 1, bilinear zwischen den vier Punkten der Zelle).</summary>
    public double Anteil(double x, double z, Bodentextur t)
    {
        if (_kacheln.Count == 0) return t == Bodentextur.Wiese ? 1 : 0;
        var (fx, fz) = (x / Abstand, z / Abstand);
        var (i, j) = ((int)Math.Floor(fx), (int)Math.Floor(fz));
        var (tx, tz) = (fx - i, fz - j);
        double a = Gewicht(i, j, t), b = Gewicht(i + 1, j, t), c = Gewicht(i, j + 1, t), d = Gewicht(i + 1, j + 1, t);
        return ((a + (b - a) * tx) * (1 - tz) + (c + (d - c) * tx) * tz) / 255;
    }

    /// <summary>Die Textur, die an einem Ort am stärksten ist (am nächsten Bodenpunkt; bei Gleichstand die erste in der Liste).</summary>
    public Bodentextur Textur(double x, double z)
    {
        var (i, j) = ((int)Math.Round(x / Abstand, MidpointRounding.AwayFromZero), (int)Math.Round(z / Abstand, MidpointRounding.AwayFromZero));
        var beste = Bodentextur.Wiese;
        var hoechstes = -1;
        for (var t = 0; t < Texturen; t++)
        {
            int w = Gewicht(i, j, (Bodentextur)t);
            if (w > hoechstes) (beste, hoechstes) = ((Bodentextur)t, w);
        }
        return beste;
    }

    /// <summary>
    /// Ein Ausschnitt als Splatmap für die Darstellung: <paramref name="breite"/> × <paramref name="tiefe"/> Punkte ab (i0, j0), Zeile für Zeile
    /// (j), je Punkt vier Bytes (RGBA) – Gruppe 0: Wiese, Erde, Fels, Kies; 1: Waldboden, Sand, Schlamm, Moos; 2: Lehm, Pflaster, Mist, 0.
    /// Die Werte aller drei Gruppen ergeben zusammen 255.
    /// </summary>
    public byte[] Splat(int i0, int j0, int breite, int tiefe, int gruppe)
    {
        if (gruppe is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(gruppe), "Gruppe 0, 1 oder 2.");
        var aus = new byte[breite * tiefe * 4];
        for (var j = 0; j < tiefe; j++)
            for (var i = 0; i < breite; i++)
            {
                var n = (j * breite + i) * 4;
                for (var c = 0; c < 4; c++)
                {
                    var t = gruppe * 4 + c;
                    if (t < Texturen) aus[n + c] = Gewicht(i0 + i, j0 + j, (Bodentextur)t);
                }
            }
        return aus;
    }

    // ── Schreiben (für die Pinsel; die Kartenwerkstatt ändert nur über Befehle) ──

    /// <summary>Setzt die elf Werte eines Punkts (zehn Texturen ab Erde und die Dekodichte). Legt die Kachel an oder entfernt sie, wenn nötig.</summary>
    internal void WerteSetzen(int i, int j, ReadOnlySpan<byte> werte)
    {
        var schluessel = KachelVon(i, j);
        var n = Index(i, j);
        if (!_kacheln.TryGetValue(schluessel, out var k))
        {
            if (!werte.ContainsAnyExcept((byte)0)) return;
            _kacheln[schluessel] = k = new byte[]?[Kanaele];
        }
        for (var c = 0; c < Kanaele; c++)
        {
            if (k[c] is null)
            {
                if (werte[c] == 0) continue;
                k[c] = new byte[Punkte];
            }
            k[c]![n] = werte[c];
        }
        _gepackt.Remove(schluessel);
        Stand++;
    }

    /// <summary>Die elf Werte eines Punkts (siehe <see cref="WerteSetzen"/>).</summary>
    internal void WerteLesen(int i, int j, Span<byte> aus)
    {
        aus.Clear();
        if (!_kacheln.TryGetValue(KachelVon(i, j), out var k)) return;
        var n = Index(i, j);
        for (var c = 0; c < Kanaele; c++)
            if (k[c] is { } w) aus[c] = w[n];
    }

    /// <summary>Eine Kopie der Kanäle einer Kachel (null: es gibt sie nicht, dort ist alles Wiese ohne Deko).</summary>
    internal byte[]?[]? KachelKopie((int I, int J) kachel) =>
        _kacheln.TryGetValue(kachel, out var k) ? k.Select(c => (byte[]?)c?.Clone()).ToArray() : null;

    /// <summary>Eine Kachel gepackt (je Kanal Base64 oder null) – null, wenn es sie nicht gibt oder alles 0 ist. Für Rückgängig.</summary>
    internal string?[]? KachelGepackt((int I, int J) kachel) => KachelPacken(_kacheln.GetValueOrDefault(kachel));

    /// <summary>Packt die Kanäle einer Kachel (je Kanal Base64 oder null) – null, wenn alles 0 ist.</summary>
    internal static string?[]? KachelPacken(byte[]?[]? k)
    {
        if (k is null) return null;
        var aus = new string?[Kanaele];
        var etwas = false;
        for (var c = 0; c < Kanaele; c++)
            if (k[c] is { } w && w.AsSpan().ContainsAnyExcept((byte)0))
                (aus[c], etwas) = (Packen(w), true);
        return etwas ? aus : null;
    }

    /// <summary>Ersetzt eine Kachel durch gepackte Werte (null: weg, dort ist alles Wiese ohne Deko).</summary>
    internal void KachelSetzen((int I, int J) kachel, string?[]? gepackt)
    {
        _gepackt.Remove(kachel);
        Stand++;
        if (gepackt is null)
        {
            _kacheln.Remove(kachel);
            return;
        }
        var k = new byte[]?[Kanaele];
        for (var c = 0; c < Kanaele; c++)
            if (gepackt[c] is { } text) k[c] = Entpacken(text) ?? throw new InvalidOperationException("Rückgängig: Kachel nicht lesbar.");
        _kacheln[kachel] = k;
    }

    /// <summary>Entfernt eine Kachel, in der alles 0 ist (alles Wiese ohne Deko); Kanäle, die ganz 0 sind, fallen weg.</summary>
    internal void Aufraeumen((int I, int J) kachel)
    {
        if (!_kacheln.TryGetValue(kachel, out var k)) return;
        for (var c = 0; c < Kanaele; c++)
            if (k[c] is { } w && !w.AsSpan().ContainsAnyExcept((byte)0)) k[c] = null;
        if (Array.TrueForAll(k, c => c is null)) _kacheln.Remove(kachel);
    }

    /// <summary>Eine Kopie des ganzen Rasters.</summary>
    public Bodenraster Kopie()
    {
        var r = new Bodenraster { Startwert = Startwert };
        foreach (var (k, w) in _kacheln) r._kacheln[k] = w.Select(c => (byte[]?)c?.Clone()).ToArray();
        foreach (var (k, t) in _gepackt) r._gepackt[k] = (JsonObject)t.DeepClone();
        return r;
    }

    // ── Datei ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Das Raster als Feld <c>boden</c> der Kartendatei – null, wenn nichts darin steht (dann fällt das Feld weg). Mit <paramref name="nur"/>
    /// nur die Kacheln, die Punkte in diesem Rechteck haben (die Karte mit Rand). Reihenfolge: nach j, dann i; je Kachel die Kanäle in der
    /// Reihenfolge der Texturen, dann „deko“.
    /// </summary>
    public JsonObject? AlsJson(Kartenrechteck? nur = null)
    {
        var bereich = nur is { } r ? PunkteIn(r) : ((int I0, int J0, int I1, int J1)?)null;
        var liste = new JsonArray();
        foreach (var k in _kacheln.Keys.OrderBy(k => k.J).ThenBy(k => k.I))
        {
            if (bereich is { } b && (k.I * Kachel > b.I1 || k.I * Kachel + Maske < b.I0 || k.J * Kachel > b.J1 || k.J * Kachel + Maske < b.J0)) continue;
            if (!_gepackt.TryGetValue(k, out var kachel))
            {
                if (KachelGepackt(k) is not { } gepackt) continue;
                kachel = new JsonObject { ["i"] = k.I, ["j"] = k.J };
                for (var c = 0; c < Kanaele; c++)
                    if (gepackt[c] is { } text) kachel[KanalName(c)] = text;
                _gepackt[k] = kachel;
            }
            liste.Add(kachel.DeepClone());
        }
        if (liste.Count == 0) return null;
        return new JsonObject { ["abstand"] = 1, ["kachel"] = Kachel, ["startwert"] = Startwert, ["kacheln"] = liste };
    }

    /// <summary>Liest das Feld <c>boden</c> (null: alles Wiese).</summary>
    /// <exception cref="KartenFehler">Wenn das Feld nicht dem Format entspricht.</exception>
    public static Bodenraster AusJson(JsonNode? json)
    {
        var r = new Bodenraster();
        if (json is null) return r;
        static KartenFehler Fehler(string text) => new(new[] { "boden: " + text });
        if (json is not JsonObject o) throw Fehler("ist kein Objekt.");
        double? Zahl(JsonObject x, string feld) => x[feld] is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;
        if (Zahl(o, "abstand") != Abstand) throw Fehler($"abstand muss {Abstand} sein.");
        if (Zahl(o, "kachel") != Kachel) throw Fehler($"kachel muss {Kachel} sein.");
        if (o["startwert"] is not null)
            r.Startwert = o["startwert"] is JsonValue vs && vs.TryGetValue<int>(out var s) ? s : throw Fehler("startwert ist eine ganze Zahl.");
        if (o["kacheln"] is not JsonArray liste) throw Fehler("kacheln fehlt oder ist keine Liste.");
        foreach (var e in liste)
        {
            if (e is not JsonObject k || k["i"] is not JsonValue vi || !vi.TryGetValue<int>(out var i) || k["j"] is not JsonValue vj || !vj.TryGetValue<int>(out var j))
                throw Fehler("Jede Kachel hat i und j (ganze Zahlen).");
            if (r._kacheln.ContainsKey((i, j))) throw Fehler($"Die Kachel ({i}, {j}) steht doppelt.");
            var kanaele = new byte[]?[Kanaele];
            foreach (var (name, wert) in k)
            {
                if (name is "i" or "j") continue;
                var c = name == "deko" ? DekoKanal : TexturVon(name) is { } t && t != Bodentextur.Wiese ? (int)t - 1 : -1;
                if (c < 0) throw Fehler($"Kachel ({i}, {j}): „{name}“ ist keine Bodentextur (erde, fels, kies, waldboden, sand, schlamm, moos, lehm, pflaster, mist, deko).");
                if (wert is not JsonValue vd || !vd.TryGetValue<string>(out var daten)) throw Fehler($"Kachel ({i}, {j}): {name} ist kein Text.");
                kanaele[c] = Entpacken(daten) ?? throw Fehler($"Die Kachel ({i}, {j}) ist nicht lesbar ({name}).");
            }
            for (var n = 0; n < Punkte; n++)
            {
                var summe = 0;
                for (var c = 0; c < DekoKanal; c++) summe += kanaele[c]?[n] ?? 0;
                if (summe > 255) throw Fehler($"Kachel ({i}, {j}): Die Anteile eines Punkts ergeben mehr als 255.");
            }
            r._kacheln[(i, j)] = kanaele;
            r._gepackt[(i, j)] = (JsonObject)k.DeepClone();
        }
        return r;
    }

    /// <summary>Packt einen Kanal wie eine Kachel des Höhenrasters (<see cref="Hoehenraster.Packen"/>).</summary>
    internal static string Packen(byte[] w)
    {
        var s = new short[Punkte];
        for (var n = 0; n < Punkte; n++) s[n] = w[n];
        return Hoehenraster.Packen(s);
    }

    /// <summary>Entpackt einen Kanal – null, wenn der Text kein gültiges Paket ist oder Werte außerhalb von 0 … 255 enthält.</summary>
    internal static byte[]? Entpacken(string text)
    {
        if (Hoehenraster.Entpacken(text) is not { } s) return null;
        var w = new byte[Punkte];
        for (var n = 0; n < Punkte; n++)
        {
            if (s[n] is < 0 or > 255) return null;
            w[n] = (byte)s[n];
        }
        return w;
    }
}

/// <summary>Schreibt und liest das Bodenraster im Spielstand genau wie in der Kartendatei (Feld <c>boden</c>).</summary>
public sealed class BodenrasterJson : JsonConverter<Bodenraster>
{
    public override Bodenraster Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var json = JsonNode.Parse(ref reader);
        try
        {
            return Bodenraster.AusJson(json);
        }
        catch (KartenFehler ex)
        {
            throw new JsonException(string.Join(" ", ex.Fehler));
        }
    }

    public override void Write(Utf8JsonWriter writer, Bodenraster value, JsonSerializerOptions options)
    {
        if (value.AlsJson() is { } json) json.WriteTo(writer);
        else writer.WriteNullValue();
    }
}
