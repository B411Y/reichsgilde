using System.Text.Json.Nodes;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Ein Modell (.glb) ohne Godot lesen – nur die Beschreibung, nicht die Netze
//
//  Eine GLB-Datei ist ein Kopf (12 Byte), dann ein JSON-Teil (Länge an Byte 12, Text ab Byte 20) und ein Binärteil mit den Netzen. Für
//  die Katalogprüfung und die Kartenskripte genügt der JSON-Teil: Er nennt die Knoten (Name, Lage, Kinder) und je Netz die Grenzen der
//  Positionen (min/max). Daraus ergeben sich die Ausdehnung eines Modells und die Lage von Ankern wie „Anker_Karren“ – so, wie die
//  Stadtansicht sie liest (Eltern eingerechnet). Bis Schritt 4 tat das tools/modelle/pruefen.py (lesen, _welt).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Die Beschreibung (JSON-Teil) einer GLB-Datei mit der Weltmatrix je Knoten.</summary>
public sealed class Modelldatei
{
    private readonly JsonObject _gltf;
    private readonly double[][,] _welt;

    private Modelldatei(JsonObject gltf)
    {
        _gltf = gltf;
        var knoten = Knoten;
        var eltern = new Dictionary<int, int>();
        for (var i = 0; i < knoten.Count; i++)
            foreach (var k in knoten[i]?["children"] as JsonArray ?? new JsonArray())
                eltern[(int)k!] = i;
        _welt = new double[knoten.Count][,];
        double[,] Rechne(int i) => _welt[i] ??= eltern.TryGetValue(i, out var e) ? Mal(Rechne(e), Matrix(knoten[i]!.AsObject())) : Matrix(knoten[i]!.AsObject());
        for (var i = 0; i < knoten.Count; i++) Rechne(i);
    }

    /// <summary>Liest die Beschreibung aus einer GLB-Datei.</summary>
    /// <exception cref="InvalidDataException">Wenn die Datei keine GLB-Datei ist.</exception>
    public static Modelldatei Lesen(string pfad)
    {
        var daten = File.ReadAllBytes(pfad);
        if (daten.Length < 20 || BitConverter.ToUInt32(daten, 0) != 0x46546C67) // „glTF“
            throw new InvalidDataException($"{pfad} ist keine GLB-Datei.");
        var laenge = (int)BitConverter.ToUInt32(daten, 12);
        return new Modelldatei(JsonNode.Parse(new ReadOnlySpan<byte>(daten, 20, laenge))!.AsObject());
    }

    private JsonArray Knoten => _gltf["nodes"] as JsonArray ?? new JsonArray();

    /// <summary>Der Index des ersten Knotens mit diesem Namen – oder null.</summary>
    public int? KnotenIndex(string name)
    {
        var k = Knoten;
        for (var i = 0; i < k.Count; i++)
            if ((string?)k[i]?["name"] == name) return i;
        return null;
    }

    public bool HatKnoten(string name) => KnotenIndex(name) is not null;

    /// <summary>Lage (x, z) des ersten Knotens <paramref name="name"/> im Modell (Eltern eingerechnet) – oder null.</summary>
    public Kartenort? Anker(string name) => KnotenIndex(name) is { } i ? new Kartenort(_welt[i][0, 3], _welt[i][2, 3]) : null;

    /// <summary>
    /// (x0, z0, x1, z1) aller Netze der Datei bzw. des Knotens samt Kindern, in den Koordinaten des Modells – oder null (Knoten fehlt, keine
    /// Netze). Zählt die Grenzen jedes Netzes (alle acht Ecken des Kastens) mit seiner Lage.
    /// </summary>
    public (double X0, double Z0, double X1, double Z1)? Ausdehnung(string? knoten = null)
    {
        var alle = Knoten;
        IEnumerable<int> auswahl;
        if (knoten is null) auswahl = Enumerable.Range(0, alle.Count);
        else
        {
            if (KnotenIndex(knoten) is not { } start) return null;
            var liste = new List<int>();
            var offen = new Stack<int>(new[] { start });
            while (offen.Count > 0)
            {
                var i = offen.Pop();
                liste.Add(i);
                foreach (var k in alle[i]?["children"] as JsonArray ?? new JsonArray()) offen.Push((int)k!);
            }
            auswahl = liste;
        }
        double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
        var netze = _gltf["meshes"] as JsonArray;
        var zugriffe = _gltf["accessors"] as JsonArray;
        foreach (var i in auswahl)
        {
            if (alle[i]?["mesh"] is not JsonValue netz || netze is null || zugriffe is null) continue;
            var m = _welt[i];
            foreach (var prim in netze[(int)netz]?["primitives"] as JsonArray ?? new JsonArray())
            {
                var acc = zugriffe[(int)prim!["attributes"]!["POSITION"]!]!;
                var (min, max) = (acc["min"]!.AsArray(), acc["max"]!.AsArray());
                foreach (var x in new[] { (double)min[0]!, (double)max[0]! })
                    foreach (var y in new[] { (double)min[1]!, (double)max[1]! })
                        foreach (var z in new[] { (double)min[2]!, (double)max[2]! })
                        {
                            var wx = m[0, 0] * x + m[0, 1] * y + m[0, 2] * z + m[0, 3];
                            var wz = m[2, 0] * x + m[2, 1] * y + m[2, 2] * z + m[2, 3];
                            (x0, z0, x1, z1) = (Math.Min(x0, wx), Math.Min(z0, wz), Math.Max(x1, wx), Math.Max(z1, wz));
                        }
            }
        }
        return x0 == double.MaxValue ? null : (x0, z0, x1, z1);
    }

    /// <summary>4×4-Matrix (Zeilen) eines glTF-Knotens aus <c>matrix</c> oder translation/rotation/scale.</summary>
    private static double[,] Matrix(JsonObject k)
    {
        if (k["matrix"] is JsonArray a)
        {
            var mm = new double[4, 4];
            for (var r = 0; r < 4; r++)
                for (var c = 0; c < 4; c++)
                    mm[r, c] = (double)a[c * 4 + r]!;
            return mm;
        }
        double[] Werte(string feld, params double[] vorgabe) => k[feld] is JsonArray w ? w.Select(v => (double)v!).ToArray() : vorgabe;
        var (t, q, s) = (Werte("translation", 0, 0, 0), Werte("rotation", 0, 0, 0, 1), Werte("scale", 1, 1, 1));
        var (x, y, z, w) = (q[0], q[1], q[2], q[3]);
        var rr = new[,]
        {
            { 1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w) },
            { 2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w) },
            { 2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y) },
        };
        var m = new double[4, 4];
        for (var r = 0; r < 3; r++)
        {
            for (var c = 0; c < 3; c++) m[r, c] = rr[r, c] * s[c];
            m[r, 3] = t[r];
        }
        m[3, 3] = 1;
        return m;
    }

    private static double[,] Mal(double[,] a, double[,] b)
    {
        var m = new double[4, 4];
        for (var i = 0; i < 4; i++)
            for (var j = 0; j < 4; j++)
            {
                var s = 0.0;
                for (var k = 0; k < 4; k++) s += a[i, k] * b[k, j];
                m[i, j] = s;
            }
        return m;
    }
}
