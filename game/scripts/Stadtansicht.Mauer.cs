using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Die Stadtmauer – elfter Teil der <c>partial class Stadtansicht</c>.
///
/// Der Mauer-Baukasten des Katalogs (<c>stadtmauer.glb</c>, siehe <c>docs/bereiche/darstellung.md</c>): „Mauerstueck“, „Mauerturm“, „Eckturm“ und „Tor“,
/// jedes mit dem Ursprung im Nullpunkt, lokal X entlang der Mauer, +Z zur Feldseite. Wo die Teile stehen, rechnet der Kern
/// (<see cref="Mauerplan"/>: Mauerstücke entlang jeder Strecke, ein Eckturm an jedem Knick ab 12° und an den Enden einer offenen Mauer,
/// dazwischen etwa alle 35 m ein Mauerturm, an jedem Tor die Torburg) – dieselbe Regel für Spiel und Kartenwerkstatt. Hier wird der Plan
/// nur gezeichnet (<see cref="MauerAufbauen"/>): jede Mauer der Karte im Spiel (<see cref="MauerAusKarte"/>), jede Mauer des Entwurfs in der
/// Werkstatt (Stadtansicht.Kartenwerkstatt.Mauern.cs, nach jeder Änderung neu).
///
/// Die vielen gleichen Teile (Mauerstücke, Mauertürme, Ecktürme) sind je Teil ein <see cref="MultiMeshInstance3D"/>: Die Grafikkarte zeichnet
/// alle Kopien in einem Aufruf je Material, statt hundert einzelner Knoten.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Die vier Teile eines Mauer-Baukastens und der Anker in der Durchfahrt des Tors – je Datei einmal aus dem Modell gelesen.</summary>
    private sealed record Mauerteile(Mesh Stueck, Mesh Turm, Mesh Eckturm, Mesh Tor, Vector3? TorAnker);

    private readonly Dictionary<string, Mauerteile?> _mauerteile = new();

    private Mauerteile? MauerteileVon(string datei)
    {
        if (_mauerteile.TryGetValue(datei, out var teile)) return teile;
        if (Modell(datei)?.Instantiate<Node3D>() is not { } kasten) return _mauerteile[datei] = null;
        var stueck = (kasten.FindChild("Mauerstueck", true, false) as MeshInstance3D)?.Mesh;
        var turm = (kasten.FindChild("Mauerturm", true, false) as MeshInstance3D)?.Mesh;
        var eck = (kasten.FindChild("Eckturm", true, false) as MeshInstance3D)?.Mesh;
        var tor = (kasten.FindChild("Tor", true, false) as MeshInstance3D)?.Mesh;
        var anker = (kasten.FindChild("Anker_Tuer", true, false) as Node3D)?.Position;
        kasten.Free();
        if (stueck is null || turm is null || eck is null || tor is null)
        {
            GD.PushError($"Modell unvollständig: res://assets/modelle/{datei}.glb braucht Mauerstueck, Mauerturm, Eckturm und Tor.");
            return _mauerteile[datei] = null;
        }
        return _mauerteile[datei] = new Mauerteile(stueck, turm, eck, tor, anker);
    }

    /// <summary>
    /// Zeichnet den Plan einer Mauer aus dem Baukasten <paramref name="datei"/> (<c>stadtmauer.glb</c>) unter <paramref name="eltern"/>. Im Spiel
    /// (<paramref name="spiel"/>) bekommt jede Torburg ihren Anker „Tor:&lt;Name&gt;“, eine Klickfläche und ein Schild; die Werkstatt zeichnet
    /// nur die Modelle.
    /// </summary>
    private void MauerAufbauen(string datei, Mauerplan plan, Node3D eltern, bool spiel)
    {
        if (MauerteileVon(datei) is not { } teile) return;
        foreach (var tor in plan.Tore)
        {
            var lage = new Transform3D(MauerBasis(tor.Lage), MauerOrt(tor.Lage));
            var knoten = new MeshInstance3D { Name = tor.Name, Mesh = teile.Tor, Transform = lage, Layers = 1 | TorErdeEbene };
            eltern.AddChild(knoten);
            TorErdeMerken(knoten); // Erde im Torweg, wenn ein Erdweg hindurchführt (Stadtansicht.Torerde.cs)
            if (spiel) TorEinrichten(tor.Name, knoten, lage, teile.TorAnker);
        }
        Vielfach("Mauerstuecke", teile.Stueck, plan.Stuecke.Select(s => new Transform3D(MauerStueckBasis(s), MauerOrt(s))), eltern);
        Vielfach("Mauertuerme", teile.Turm, plan.Tuerme.Select(t => new Transform3D(MauerBasis(t), MauerOrt(t))), eltern);
        Vielfach("Eckturme", teile.Eckturm, plan.Ecktuerme.Select(t => new Transform3D(MauerBasis(t), MauerOrt(t))), eltern);
    }

    /// <summary>Wo ein Teil der Mauer steht: auf dem Boden (die Mauer liegt in ihrem Bett, Stadtansicht.Gelaende.cs).</summary>
    private Vector3 MauerOrt(Mauerlage l) => new((float)l.X, BodenHoehe((float)l.X, (float)l.Z), (float)l.Z);

    /// <summary>Die Ausrichtung eines Mauerstücks (auf 4 m gestreckt wie der Plan sagt); am Hang entlang des Gefälles geneigt.</summary>
    private Basis MauerStueckBasis(Mauerlage l)
    {
        var streckung = (float)(l.Laenge / Mauerplan.MauerstueckLaenge);
        var basis = MauerBasis(l, streckung);
        if (!BodenBewegt) return basis;
        var halb = (float)l.Laenge / 2;
        var entlang = new Vector3((float)l.EntlangX, 0, (float)l.EntlangZ);
        var (x, z) = ((float)l.X, (float)l.Z);
        var steigung = (BodenHoehe(x + entlang.X * halb, z + entlang.Z * halb) - BodenHoehe(x - entlang.X * halb, z - entlang.Z * halb)) / Mathf.Max(0.01f, (float)l.Laenge);
        var neu = (entlang + new Vector3(0, steigung, 0)).Normalized();
        var feld = new Vector3((float)l.FeldX, 0, (float)l.FeldZ);
        return new Basis(neu * streckung, feld.Cross(neu), feld); // y = z × x
    }

    /// <summary>Die Ausrichtung eines Teils: lokal X entlang der Mauer (gestreckt um <paramref name="streckung"/>), Y oben, Z zur Feldseite.</summary>
    private static Basis MauerBasis(Mauerlage l, float streckung = 1) =>
        new(new Vector3((float)l.EntlangX, 0, (float)l.EntlangZ) * streckung, Vector3.Up, new Vector3((float)l.FeldX, 0, (float)l.FeldZ));

    /// <summary>
    /// Die Ausrichtung eines Teils auf einer Strecke: lokal X entlang der Strecke, Y oben, Z (Feldseite) zur Außenseite. Die Strecke selbst hat
    /// keine feste Richtung, darum dreht sich <paramref name="entlang"/> bei Bedarf um (dieselbe Regel wie im <see cref="Mauerplan"/>) – hier nur
    /// noch für die Richtung nach draußen an den Toren (<see cref="LagenLesen"/>).
    /// </summary>
    private static Basis Ausrichtung(Vector3 entlang, Vector3 aussen)
    {
        entlang.Y = 0;
        entlang = entlang.Normalized();
        var feld = entlang.Cross(Vector3.Up); // X × Y = Z: bei +X als Richtung zeigt die Feldseite nach +Z
        if (feld.Dot(aussen) < 0)
        {
            entlang = -entlang;
            feld = -feld;
        }
        return new Basis(entlang, Vector3.Up, feld);
    }

    /// <summary>Die Strecke der Linie, der <paramref name="ort"/> am nächsten liegt (für die Tore).</summary>
    private static int NaechsteStrecke(IReadOnlyList<Vector3> linie, bool geschlossen, Vector3 ort)
    {
        var beste = 0;
        var kleinster = float.MaxValue;
        for (var i = 0; i < (geschlossen ? linie.Count : linie.Count - 1); i++)
        {
            var a = linie[i];
            var b = linie[(i + 1) % linie.Count];
            var abstand = ort.DistanceTo(Geometry3D.GetClosestPointToSegment(ort, a, b));
            if (abstand < kleinster) { kleinster = abstand; beste = i; }
        }
        return beste;
    }

    /// <summary>Die Torburg im Spiel: Anker „Anker_Tuer“ in der Durchfahrt (Kennung „Tor:&lt;Name&gt;“), Klickfläche und Schild wie beim alten Torhaus.</summary>
    private void TorEinrichten(string name, Node3D tor, Transform3D lage, Vector3? ankerLage)
    {
        if (ankerLage is { } anker)
        {
            var knoten = new Node3D { Name = "Anker_Tuer", Position = anker };
            tor.AddChild(knoten);
            _modellAnker["Tor:" + name] = (knoten, null);
        }
        // Die Klickfläche liegt längs der Mauer: Torburg 16,5 m breit und 7 m tief
        var breit = new Vector3(Mathf.Abs(lage.Basis.X.X), 0, Mathf.Abs(lage.Basis.X.Z));
        var tief = new Vector3(Mathf.Abs(lage.Basis.Z.X), 0, Mathf.Abs(lage.Basis.Z.Z));
        Klickflaeche(name, lage.Origin + new Vector3(0, 7, 0), breit * 16.5f + tief * 7 + new Vector3(0, 14, 0));
        Beschriftung(name, lage.Origin + new Vector3(0, 20, 0), 48, kennung: name);
    }

    /// <summary>Viele Kopien eines Teils in einem Zeichenaufruf je Material (<see cref="MultiMeshInstance3D"/>).</summary>
    private static void Vielfach(string name, Mesh netz, IEnumerable<Transform3D> lagen, Node3D eltern)
    {
        var liste = lagen.ToList();
        if (liste.Count == 0) return;
        var vielfach = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = netz, InstanceCount = liste.Count };
        for (var i = 0; i < liste.Count; i++) vielfach.SetInstanceTransform(i, liste[i]);
        eltern.AddChild(new MultiMeshInstance3D { Name = name, Multimesh = vielfach });
    }
}
