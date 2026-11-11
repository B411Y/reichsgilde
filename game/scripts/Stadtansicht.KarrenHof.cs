using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Hofreihe der Karren (Teil der <c>partial class Stadtansicht</c>): Die wartenden Karren eines Betriebs stehen nebeneinander quer zum Weg, jeder nach
/// seiner eigenen Breite plus Luft (<see cref="KarrenHofLuft"/>), und weit genug vom Haus: Der Hof liegt nur 1,6 m vor der Hauswand (Anker_Karren), das Heck
/// eines Gespanns (Rad, Bordwand, Deichsel) reicht aber bis 1,3 m hinter den Ursprung und stieße an den Sockel. Die Darstellung liest dazu die
/// niedrige Geometrie des Hausmodells (Sockel, Stufen, Pfosten, 0,15 bis 1,6 m über dem Boden) und die Grundflächen der Nachbarhäuser aus den Kartendaten und
/// schiebt den Platz der Reihe auf dem Weg nach vorn, bis die Hecke frei sind (<see cref="KarrenHofVersatz"/>, höchstens <see cref="KarrenHofVersatzMax"/>).
/// Der Kern bleibt unberührt, nur der gezeigte Meter wird umgerechnet (der Karren steht und fährt den Versatz weiter vorn).
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Luft (Meter) zwischen zwei Karren am Hof nebeneinander und zwischen Heck und Hindernis.</summary>
    private const float KarrenHofLuft = 0.5f;

    /// <summary>Seitlich zählt ein Hindernis nur im Streifen des Karrens plus diese Luft (Meter): ein Pfosten daneben schiebt die Reihe nicht nach vorn.</summary>
    private const float KarrenHofSeite = 0.15f;

    /// <summary>So weit (Meter) schiebt die Hofreihe höchstens vor den Hof; mehr würde den Karren in die Gasse stellen.</summary>
    private const float KarrenHofVersatzMax = 3f;

    /// <summary>Breite des Gespanns (Meter, Karren samt Tieren) quer zur Fahrtrichtung, aus den Modellen: Karren 2,38 / 2,08 / 1,50 m, Tiere und Läufer innerhalb bzw. knapp daneben.</summary>
    internal static float KarrenBreite(string typ) => typ switch
    {
        "Pferdekarren" => 2.4f,
        "Ochsenkarren" => 2.2f,
        _ => 1.6f,
    };

    /// <summary>
    /// Querlage (Meter, + = rechts von der Fahrtrichtung) jedes wartenden Karrens: nach Gespannbreite plus <see cref="KarrenHofLuft"/> hintereinander gelegt,
    /// die ganze Reihe mittig um den Weg.
    /// </summary>
    internal static float[] KarrenHofQuer(IReadOnlyList<string> typen)
    {
        var x = new float[typen.Count];
        for (var i = 1; i < x.Length; i++) x[i] = x[i - 1] + (KarrenBreite(typen[i - 1]) + KarrenBreite(typen[i])) / 2 + KarrenHofLuft;
        if (x.Length == 0) return x;
        var mitte = (x[0] - KarrenBreite(typen[0]) / 2 + x[^1] + KarrenBreite(typen[^1]) / 2) / 2;
        for (var i = 0; i < x.Length; i++) x[i] -= mitte;
        return x;
    }

    /// <summary>
    /// Wie weit (Meter) die Reihe auf dem Weg nach vorn muss: Jedes Hindernis (<c>X</c> = Lage auf der Fahrtrichtung vom Hof, negativ = dahinter, <c>Y</c> = Querlage)
    /// im Streifen eines Karrens (Breite plus 0,15 m) darf nicht ins Heck reichen: Versatz ≥ Lage + Heck + Luft. Hindernisse weiter als 0,6 m vor dem Hof zählen nicht (der
    /// Karren steht vor ihnen).
    /// </summary>
    internal static float KarrenHofVersatz(IReadOnlyList<string> typen, IReadOnlyList<float> quer, IEnumerable<Vector2> hindernisse)
    {
        var v = 0f;
        foreach (var h in hindernisse)
        {
            if (h.X > 0.6f) continue;
            for (var i = 0; i < typen.Count; i++)
                if (Math.Abs(h.Y - quer[i]) <= KarrenBreite(typen[i]) / 2 + KarrenHofSeite)
                    v = Math.Max(v, h.X + KarrenGespann(typen[i]).Hinten + KarrenHofLuft);
        }
        return Math.Min(v, KarrenHofVersatzMax);
    }

    /// <summary>Hindernisse in der Nähe des Hofs in Koordinaten (Lage auf der Fahrtrichtung, Querlage), aus Sockel und Co. des Hausmodells und den Grundflächen der Nachbarn.</summary>
    private List<Vector2> KarrenHofHindernisse(BetriebsAnsicht ansicht, Vector3 hof, Vector3 achse, Vector3 rechts)
    {
        var liste = new List<Vector2>();
        void Punkt(Vector3 p)
        {
            var d = p - hof;
            var (a, l) = (d.X * achse.X + d.Z * achse.Z, d.X * rechts.X + d.Z * rechts.Z);
            if (a is > -12f and < 1f && Math.Abs(l) < 6f) liste.Add(new Vector2(a, l));
        }
        var boden = BodenHoehe(hof.X, hof.Z);
        if (ansicht.Modell?.Wurzel is { } wurzel && IsInstanceValid(wurzel))
        {
            var stapel = new Stack<Node>();
            stapel.Push(wurzel);
            while (stapel.Count > 0)
            {
                var knoten = stapel.Pop();
                foreach (var kind in knoten.GetChildren()) stapel.Push(kind);
                if (knoten is not MeshInstance3D { Mesh: { } netz, Visible: true } mi || !mi.IsInsideTree()) continue;
                var box = mi.GlobalTransform * netz.GetAabb();
                var hofGlobal = ToGlobal(hof);
                if (box.Position.Y > hofGlobal.Y + 1.6f || box.End.Y < hofGlobal.Y + 0.15f
                    || box.End.X < hofGlobal.X - 12 || box.Position.X > hofGlobal.X + 12 || box.End.Z < hofGlobal.Z - 12 || box.Position.Z > hofGlobal.Z + 12) continue;
                for (var f = 0; f < netz.GetSurfaceCount(); f++)
                {
                    var felder = netz.SurfaceGetArrays(f);
                    var ecken = felder[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                    var index = felder[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil ? null : felder[(int)Mesh.ArrayType.Index].AsInt32Array();
                    var dreiecke = (index?.Length ?? ecken.Length) / 3;
                    for (var t = 0; t < dreiecke; t++)
                    {
                        var d0 = mi.GlobalTransform * ecken[index?[t * 3] ?? t * 3];
                        var d1 = mi.GlobalTransform * ecken[index?[t * 3 + 1] ?? t * 3 + 1];
                        var d2 = mi.GlobalTransform * ecken[index?[t * 3 + 2] ?? t * 3 + 2];
                        if (Math.Max(d0.Y, Math.Max(d1.Y, d2.Y)) < hofGlobal.Y + 0.15f || Math.Min(d0.Y, Math.Min(d1.Y, d2.Y)) > hofGlobal.Y + 1.6f) continue;
                        // Die Kanten abtasten (ein Quader hat nur an den Ecken Eckpunkte): alle 0,4 m ein Punkt, nur in der Höhe von Rad und Bordwand
                        foreach (var (u, w) in new[] { (d0, d1), (d1, d2), (d2, d0) })
                        {
                            var schritte = Math.Clamp((int)(u.DistanceTo(w) / 0.4f), 1, 30);
                            for (var k = 0; k <= schritte; k++)
                            {
                                var welt = u.Lerp(w, k / (float)schritte);
                                var hoch = welt.Y - hofGlobal.Y;
                                if (hoch is >= 0.15f and <= 1.6f) Punkt(ToLocal(welt));
                            }
                        }
                    }
                }
            }
        }
        // Nachbarhäuser: Grundfläche (ohne den eigenen Platz), am Rand um 0,5 m verkleinert, die Ränder abgetastet
        foreach (var g in _karte?.Gebaeude ?? new List<Kartengebaeude>())
        {
            if (g.Art is not (Bausteinart.Werkstattplatz or Bausteinart.Wohnhausplatz or Bausteinart.Einwohnerplatz or Bausteinart.Kulisse)) continue;
            if (Math.Abs(g.Punkt[0] - ansicht.Mitte.X) < 0.5 && Math.Abs(g.Punkt[1] - ansicht.Mitte.Z) < 0.5) continue;
            var halb = GroesseVon(g) / 2 - new Vector2(0.5f, 0.5f);
            if (halb.X <= 0 || halb.Y <= 0) continue;
            var mitte = Punkt3(g.Punkt);
            if (new Vector2(mitte.X - hof.X, mitte.Z - hof.Z).LengthSquared() > 40f * 40f) continue;
            for (var s = -halb.X; s <= halb.X; s += 0.5f)
                foreach (var z in new[] { -halb.Y, halb.Y }) Punkt(AufFlaeche(g, new Vector3(s, 0, z)));
            for (var s = -halb.Y; s <= halb.Y; s += 0.5f)
                foreach (var x in new[] { -halb.X, halb.X }) Punkt(AufFlaeche(g, new Vector3(x, 0, s)));
        }
        return liste;
    }

    /// <summary>
    /// Lage der Hofreihe für diesen Weg und diese Karrenarten, je Weg einmal berechnet: Querlagen und Versatz auf dem Weg (Meter). Ohne Modell oder mit nur einem Karren
    /// zählt trotzdem das Heck gegen das Haus.
    /// </summary>
    private (float[] Quer, float Versatz, Vector3 Achse) KarrenHofLage(BetriebsAnsicht ansicht, Vector3[] weg, IReadOnlyList<string> typen)
    {
        var schluessel = (weg, string.Join('|', typen));
        if (ansicht.KarrenHofLagen.TryGetValue(schluessel, out var bekannt)) return bekannt;
        var achse = weg.Length > 1 ? weg[1] - weg[0] : Vector3.Forward;
        achse.Y = 0;
        achse = achse.LengthSquared() > 1e-6f ? achse.Normalized() : Vector3.Forward;
        var rechts = new Vector3(-achse.Z, 0, achse.X);
        var quer = KarrenHofQuer(typen);
        var hof = weg.Length > 0 ? weg[0] : ansicht.Hof;
        var hindernisse = KarrenHofHindernisse(ansicht, hof, achse, rechts);
        var versatz = KarrenHofVersatz(typen, quer, hindernisse);
        var lage = (quer, versatz, achse);
        ansicht.KarrenHofLagen[schluessel] = lage;
        if (KarrenMessung || KarrenHofLog)
        {
            // Messung: kleinster Abstand Heck – Hindernis (Meter) vorher (ohne Versatz) und nachher, je Karren, und der Abstand der Nachbarn nebeneinander
            var vorher = new List<string>();
            var nachher = new List<string>();
            for (var i = 0; i < typen.Count; i++)
            {
                var hinten = KarrenGespann(typen[i]).Hinten;
                var kleinste = float.MaxValue;
                foreach (var h in hindernisse)
                    if (h.X <= 0.6f && Math.Abs(h.Y - quer[i]) <= KarrenBreite(typen[i]) / 2 + KarrenHofSeite) kleinste = Math.Min(kleinste, -h.X - hinten);
                vorher.Add(kleinste == float.MaxValue ? "frei" : $"{kleinste:0.00}");
                nachher.Add(kleinste == float.MaxValue ? "frei" : $"{kleinste + versatz:0.00}");
            }
            var nachbar = typen.Count > 1 ? Enumerable.Range(1, typen.Count - 1).Select(i => quer[i] - quer[i - 1] - (KarrenBreite(typen[i]) + KarrenBreite(typen[i - 1])) / 2).Min() : float.NaN;
            GD.Print($"[karrenhof] {ansicht.Betrieb.Name}: {hindernisse.Count} Hindernispunkte, Versatz {versatz:0.00} m, Luft Heck–Sockel vorher [{string.Join(' ', vorher)}] nachher [{string.Join(' ', nachher)}] m, "
                     + $"Luft Nachbarn {(typen.Count > 1 ? $"{nachbar:0.00} m (vorher {Enumerable.Range(1, typen.Count - 1).Select(i => KarrenHofBreite - (KarrenBreite(typen[i]) + KarrenBreite(typen[i - 1])) / 2).Min():0.00} m)" : "-")}");
        }
        return lage;
    }

    private static readonly bool KarrenHofLog = OS.GetCmdlineUserArgs().Contains("--karrenhoflog=1");
}
