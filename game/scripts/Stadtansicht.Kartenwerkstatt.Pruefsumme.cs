using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Prüfsummen für die Pinselmessung (<c>--pinselmessung</c>): Nach jedem Messfall schreibt die Messung eine Zeile mit den Summen von Gassennetz, Anschlüssen,
/// Wasserflächen samt Übergängen, Geländekacheln und Wegeschicht. Wer Wege, Wasser oder Netz umbaut, vergleicht die Zeilen vorher und nachher: gleiche
/// Summen heißen gleiches Ergebnis (der Bau in Häppchen oder nur der betroffenen Teile darf nichts ändern). Reihenfolge der Kinder und Zahlen werden so
/// genommen, wie sie sind (Zentimeter), Reihen von Knoten bleiben in ihrer Reihenfolge, Mengen werden vorher sortiert.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Die Summe der Bytes je Kachel, wie die Wegeschicht sie zuletzt gerechnet hat (nur mit der Prüfsumme der Messung).</summary>
    private readonly Dictionary<(int I, int J), string> _schichtSummen = new();

    private static string Kurz(IEnumerable<string> zeilen, bool sortiert)
    {
        var liste = sortiert ? zeilen.OrderBy(z => z, StringComparer.Ordinal).ToList() : zeilen.ToList();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", liste)));
        return $"{liste.Count}:{Convert.ToHexString(bytes)[..10]}";
    }

    private static string Kurz(ReadOnlySpan<byte> daten) => Convert.ToHexString(SHA256.HashData(daten))[..10];

    private static string Cm(Vector3 v) => $"{Mathf.RoundToInt(v.X * 100)},{Mathf.RoundToInt(v.Y * 100)},{Mathf.RoundToInt(v.Z * 100)}";

    /// <summary>Die Prüfsummen der Werkstatt (Text: „Teil=Anzahl:Summe …“).</summary>
    public string WerkstattPruefsumme()
    {
        var teile = new List<string>();
        if (_wsNetzHoeheVeraltet || _wsNetzNeu || _wsNetzLauf is not null) NetzBauen();
        // Gassennetz: Knoten in ihrer Reihenfolge, Kanten nach Orten sortiert, Fähren
        teile.Add("Netzknoten=" + Kurz(_wsNetz.Knoten.Select(Cm), false));
        teile.Add("Netzkanten=" + Kurz(_wsNetz.Kanten.Select(k => string.CompareOrdinal(Cm(k.Von), Cm(k.Bis)) <= 0 ? Cm(k.Von) + ">" + Cm(k.Bis) : Cm(k.Bis) + ">" + Cm(k.Von)), true));
        teile.Add("Faehrkanten=" + Kurz(_wsNetz.FaehrKanten.Select(k => Cm(k.Von) + ">" + Cm(k.Bis)), true));
        teile.Add("Anschluesse=" + Kurz(_schichtAnschluesse.Select(a => $"{a.Von.X:0.00},{a.Von.Y:0.00}>{a.Bis.X:0.00},{a.Bis.Y:0.00} {a.Breite:0.00}"), true));
        // Wasser: jede Instanz und jeder Knoten mit Namen, Lage und Netz; sortiert (die Reihenfolge der Kinder darf wechseln)
        var wasser = new List<string>();
        // ohne den Namen der Wurzel: Godot nummeriert sie um, wenn der alte Knoten noch nicht freigegeben ist
        if (_gewaesserKnoten is not null && IsInstanceValid(_gewaesserKnoten))
            foreach (var kind in _gewaesserKnoten.GetChildren()) if (kind is Node3D k) KnotenSummen(k, Transform3D.Identity, wasser, "");
        teile.Add("Wasser=" + Kurz(wasser, true));
        if (Pinselmessung.Einzelsummen) foreach (var zeile in wasser.OrderBy(z => z, StringComparer.Ordinal)) GD.Print("WASSERZEILE " + zeile);
        teile.Add("Plan=" + (Uebergangsplan is { } plan
            ? Kurz(plan.Querungen.Select(q => $"{q.Art} {q.Weg} {q.GewaesserName} {q.VonStelle:0.000} {q.BisStelle:0.000} {q.Spiegel:0.000} {q.Deckhoehe:0.000}")
                .Concat(plan.Faehren.Select(f => f.Name + f.Von + f.Bis)), true) : "keiner"));
        var kacheln = new List<string>();
        foreach (var ((i, j), k) in _gelaendeKacheln.OrderBy(e => e.Key))
            kacheln.Add($"{i},{j} {NetzSumme(k.Mesh as ArrayMesh)}");
        teile.Add("Gelaende=" + Kurz(kacheln, false));
        // die Bilder der Wegeschicht: die Summen der gerechneten Bytes (die Textur selbst gibt headless nichts her)
        teile.Add("Wegeschicht=" + Kurz(_schichtSummen.OrderBy(e => e.Key).Select(e => $"{e.Key.I},{e.Key.J} {e.Value}"), false));
        var wege = new List<string>();
        if (_wsWegeKnoten is not null)
            foreach (var w in _wsWegeKnoten.GetChildren()) if (w is Node3D n) KnotenSummen(n, Transform3D.Identity, wege, "");
        teile.Add("Wegknoten=" + Kurz(wege, true));
        return string.Join(" ", teile);
    }

    private static string NetzSumme(ArrayMesh? netz)
    {
        if (netz is null) return "kein Netz";
        var sb = new StringBuilder();
        for (var s = 0; s < netz.GetSurfaceCount(); s++)
        {
            var a = netz.SurfaceGetArrays(s);
            for (var k = 0; k < (int)Mesh.ArrayType.Max; k++)
            {
                var wert = a[k];
                if (wert.VariantType == Variant.Type.Nil) { sb.Append('-'); continue; }
                sb.Append(k).Append(':');
                switch (wert.VariantType)
                {
                    case Variant.Type.PackedVector3Array: sb.Append(Kurz(MemoryMarshal.AsBytes(wert.AsVector3Array().AsSpan()))); break;
                    case Variant.Type.PackedVector2Array: sb.Append(Kurz(MemoryMarshal.AsBytes(wert.AsVector2Array().AsSpan()))); break;
                    case Variant.Type.PackedColorArray: sb.Append(Kurz(MemoryMarshal.AsBytes(wert.AsColorArray().AsSpan()))); break;
                    case Variant.Type.PackedInt32Array: sb.Append(Kurz(MemoryMarshal.AsBytes(wert.AsInt32Array().AsSpan()))); break;
                    case Variant.Type.PackedFloat32Array: sb.Append(Kurz(MemoryMarshal.AsBytes(wert.AsFloat32Array().AsSpan()))); break;
                    case Variant.Type.PackedByteArray: sb.Append(Kurz(wert.AsByteArray())); break;
                    default: sb.Append(wert.VariantType); break;
                }
                sb.Append(' ');
            }
        }
        return Kurz(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    private void KnotenSummen(Node3D knoten, Transform3D eltern, List<string> aus, string pfad)
    {
        var lage = eltern * knoten.Transform;
        var name = knoten.Name.ToString();
        // was sich von selbst bewegt oder erst später kommt, gehört nicht in die Summe: der Prahm und die Seile der Fähre (sie pendelt), die Eisschollen
        // (die Werkstatt baut sie erst, wenn Eis da ist)
        if (name is "Faehre" or "SeilA" or "SeilB" || name.StartsWith("Eisschollen_")) return;
        if (name.StartsWith('@')) name = name.Split('@', StringSplitOptions.RemoveEmptyEntries)[0]; // von Godot vergebene Nummern ändern sich mit der Baureihenfolge
        var zeile = $"{pfad}/{name} {Cm(lage.Origin)} {Cm(lage.Basis.X)} {Cm(lage.Basis.Y)} {Cm(lage.Basis.Z)} {knoten.Visible}";
        if (knoten is MeshInstance3D m)
            zeile += " " + (m.Mesh is ArrayMesh am ? NetzSumme(am) : m.Mesh?.GetType().Name ?? "ohne Mesh") + " " + (m.MaterialOverride?.ResourceName ?? "");
        aus.Add(zeile);
        foreach (var kind in knoten.GetChildren()) if (kind is Node3D k) KnotenSummen(k, lage, aus, pfad + "/" + name);
    }
}
