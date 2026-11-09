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
///
/// Eigene Torburgen und Ecktürme (Kölner Mauerbaukasten <c>stadtmauer_koeln.glb</c>, Paket 3 der Kölner Wahrzeichen): Ein Teil „Tor_&lt;Wort&gt;“
/// ersetzt das allgemeine „Tor“ an jedem Tor, dessen Name mit dem Wort beginnt („Tor_Eigelstein“ → „Eigelsteintor“; das längste Wort gewinnt),
/// mit seinen Ankern „Anker_Tuer_&lt;Wort&gt;“ und „Anker_Feuer_&lt;Wort&gt;_&lt;n&gt;“. Ein Teil „Eckturm_&lt;Wort&gt;“ ersetzt den Eckturm, der einem solchen
/// Tor am nächsten steht (höchstens <see cref="EigenerEckturmReichweite"/> m), ausgerichtet wie das nächste Mauerstück (der Bayenturm ist
/// viereckig und steht längs der Mauer). Der Kern braucht dafür nichts: Er setzt nur Lagen, das Bild wählt das Teil nach dem Namen.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Eine Torburg eines Baukastens: das Netz, der Anker in der Durchfahrt und die Feuerkörbe daneben.</summary>
    private sealed record Torteil(Mesh Netz, Vector3? Anker, Transform3D[] Feuer);

    /// <summary>
    /// Die vier Teile eines Mauer-Baukastens (das allgemeine Tor mit seinen Ankern) und die eigenen Torburgen und Ecktürme nach dem Wort
    /// ihres Namens – je Datei einmal aus dem Modell gelesen.
    /// </summary>
    private sealed record Mauerteile(Mesh Stueck, Mesh Turm, Mesh Eckturm, Torteil Tor, Dictionary<string, Torteil> Tore, Dictionary<string, Mesh> Ecktuerme);

    /// <summary>So weit darf ein Eckturm von seinem Tor entfernt sein, damit ihn ein eigener „Eckturm_&lt;Wort&gt;“ ersetzt (Meter).</summary>
    private const float EigenerEckturmReichweite = 25f;

    private readonly Dictionary<string, Mauerteile?> _mauerteile = new();

    private Mauerteile? MauerteileVon(string datei)
    {
        if (_mauerteile.TryGetValue(datei, out var teile)) return teile;
        if (Modell(datei)?.Instantiate<Node3D>() is not { } kasten) return _mauerteile[datei] = null;
        Mesh? Netz(string name) => (kasten.FindChild(name, true, false) as MeshInstance3D)?.Mesh;
        var stueck = Netz("Mauerstueck");
        var turm = Netz("Mauerturm");
        var eck = Netz("Eckturm");
        var tor = Netz("Tor");
        if (stueck is null || turm is null || eck is null || tor is null)
        {
            kasten.Free();
            GD.PushError($"Modell unvollständig: res://assets/modelle/{datei}.glb braucht Mauerstueck, Mauerturm, Eckturm und Tor.");
            return _mauerteile[datei] = null;
        }
        var tore = new Dictionary<string, Torteil>(StringComparer.Ordinal);
        var ecktuerme = new Dictionary<string, Mesh>(StringComparer.Ordinal);
        foreach (var k in kasten.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            var name = k.Name.ToString();
            if (k.Mesh is null) continue;
            if (name.StartsWith("Tor_", StringComparison.Ordinal)) tore[name[4..]] = TorteilLesen(kasten, k.Mesh, name[4..]);
            else if (name.StartsWith("Eckturm_", StringComparison.Ordinal)) ecktuerme[name[8..]] = k.Mesh;
        }
        var allgemein = TorteilLesen(kasten, tor, null);
        kasten.Free();
        return _mauerteile[datei] = new Mauerteile(stueck, turm, eck, allgemein, tore, ecktuerme);
    }

    /// <summary>
    /// Anker eines Tors: „Anker_Tuer“ und „Anker_Feuer_&lt;n&gt;“ (allgemeines Tor) bzw. „Anker_Tuer_&lt;Wort&gt;“ und „Anker_Feuer_&lt;Wort&gt;_&lt;n&gt;“.
    /// Anker_Feuer_&lt;n&gt;: Feuerkörbe neben der Durchfahrt (Nachtlichter, Stadtansicht.Nachtlichter.cs), im Maßstab des Teils.
    /// </summary>
    private static Torteil TorteilLesen(Node3D kasten, Mesh netz, string? wort)
    {
        var anker = (kasten.FindChild(wort is null ? "Anker_Tuer" : "Anker_Tuer_" + wort, true, false) as Node3D)?.Position;
        var vor = wort is null ? "Anker_Feuer_" : $"Anker_Feuer_{wort}_";
        var feuer = kasten.FindChildren(vor + "*", nameof(Node3D), true, false).OfType<Node3D>()
                          .Where(k => k.Name.ToString()[vor.Length..].All(char.IsAsciiDigit))
                          .OrderBy(k => k.Name.ToString(), StringComparer.Ordinal).Select(k => k.Transform).ToArray();
        return new Torteil(netz, anker, feuer);
    }

    /// <summary>Das Wort des eigenen Teils für ein Tor: das längste, mit dem sein Name beginnt (Groß- und Kleinschreibung egal); null = allgemein.</summary>
    private static string? TorWort(IEnumerable<string> woerter, string torname) =>
        woerter.Where(w => torname.StartsWith(w, StringComparison.OrdinalIgnoreCase)).OrderByDescending(w => w.Length).ThenBy(w => w, StringComparer.Ordinal)
               .FirstOrDefault();

    private readonly Dictionary<string, Vector3[]> _torfeuerWaende = new();

    /// <summary>
    /// Je Feuerkorb der Torburg die Wand daneben (Turm, Torbogen) im Raum seiner Flamme, damit der Rauch an ihr hochsteigt statt durch sie hindurch
    /// (<see cref="WandFuerLicht"/>); gerechnet mit dem Netz der Torburg, einmal je Baukasten und Torburg.
    /// </summary>
    private Vector3[] TorfeuerWaende(string schluessel, Torteil tor)
    {
        if (_torfeuerWaende.TryGetValue(schluessel, out var bekannt)) return bekannt;
        LichterLaden();
        var netze = new[] { (tor.Netz, Transform3D.Identity) };
        var waende = _lichtNetze.TryGetValue(Lichtart.Feuerkorb, out var korb)
            ? tor.Feuer.Select(f => WandFuerLicht(Lichtart.Feuerkorb, f * korb.Lage, netze)).ToArray()
            : new Vector3[tor.Feuer.Length];
        GD.Print($"[feuer] Torfeuer {schluessel}: {waende.Count(w => w != Vector3.Zero)} von {waende.Length} an einer Wand");
        return _torfeuerWaende[schluessel] = waende;
    }

    /// <summary>
    /// Zeichnet den Plan einer Mauer aus dem Baukasten <paramref name="datei"/> (<c>stadtmauer.glb</c>) unter <paramref name="eltern"/>. Im Spiel
    /// (<paramref name="spiel"/>) bekommt jede Torburg ihren Anker „Tor:&lt;Name&gt;“, eine Klickfläche und ein Schild; die Werkstatt zeichnet
    /// nur die Modelle. Eigene Torburgen und Ecktürme des Baukastens (Kopf der Datei) stehen als eigene Knoten da.
    /// </summary>
    private void MauerAufbauen(string datei, Mauerplan plan, Node3D eltern, bool spiel)
    {
        if (MauerteileVon(datei) is not { } teile) return;
        var feuerlagen = new List<Transform3D>();
        var feuerwaende = new List<Vector3>();
        var eigeneTore = new List<(string Wort, Vector3 Ort)>();
        foreach (var tor in plan.Tore)
        {
            var wort = TorWort(teile.Tore.Keys.Concat(teile.Ecktuerme.Keys), tor.Name);
            var teil = wort is not null && teile.Tore.TryGetValue(wort, out var eigen) ? eigen : teile.Tor;
            var lage = new Transform3D(MauerBasis(tor.Lage), MauerOrt(tor.Lage));
            if (wort is not null) eigeneTore.Add((wort, lage.Origin));
            if (spiel)
            {
                feuerlagen.AddRange(teil.Feuer.Select(f => lage * f));
                feuerwaende.AddRange(TorfeuerWaende(teil == teile.Tor ? datei : datei + "|" + wort, teil));
            }
            var knoten = new MeshInstance3D { Name = tor.Name, Mesh = teil.Netz, Transform = lage, Layers = 1 | TorErdeEbene };
            eltern.AddChild(knoten);
            TorErdeMerken(knoten); // Erde im Torweg, wenn ein Erdweg hindurchführt (Stadtansicht.Torerde.cs)
            if (spiel) TorEinrichten(tor.Name, knoten, lage, teil);
        }
        if (spiel) FeuerkoerbeVielfach(feuerlagen, eltern, feuerwaende); // Nachtlichter: ein Feuerkorb links und rechts jeder Durchfahrt
        Vielfach("Mauerstuecke", teile.Stueck, plan.Stuecke.Select(s => new Transform3D(MauerStueckBasis(s), MauerOrt(s))), eltern);
        Vielfach("Mauertuerme", teile.Turm, plan.Tuerme.Select(t => new Transform3D(MauerBasis(t), MauerOrt(t))), eltern);
        // Ecktürme: ein eigener (Eckturm_<Wort>) ersetzt den, der seinem Tor am nächsten steht
        var ersetzt = new Dictionary<int, string>();
        foreach (var (wort, ort) in eigeneTore)
        {
            if (!teile.Ecktuerme.ContainsKey(wort) || plan.Ecktuerme.Count == 0) continue;
            var naechster = Enumerable.Range(0, plan.Ecktuerme.Count).MinBy(i => MauerOrt(plan.Ecktuerme[i]).DistanceTo(ort));
            if (MauerOrt(plan.Ecktuerme[naechster]).DistanceTo(ort) <= EigenerEckturmReichweite) ersetzt[naechster] = wort;
        }
        foreach (var (i, wort) in ersetzt.OrderBy(e => e.Key))
        {
            var eck = plan.Ecktuerme[i];
            var stueck = plan.Stuecke.Count > 0 ? plan.Stuecke.MinBy(s => (s.X - eck.X) * (s.X - eck.X) + (s.Z - eck.Z) * (s.Z - eck.Z)) : eck;
            eltern.AddChild(new MeshInstance3D { Name = "Eckturm_" + wort, Mesh = teile.Ecktuerme[wort], Transform = new Transform3D(MauerBasis(stueck), MauerOrt(eck)) });
            GD.Print($"[mauer] Eckturm_{wort} bei ({eck.X:0.#} | {eck.Z:0.#})");
        }
        Vielfach("Eckturme", teile.Eckturm, plan.Ecktuerme.Where((_, i) => !ersetzt.ContainsKey(i)).Select(t => new Transform3D(MauerBasis(t), MauerOrt(t))), eltern);
        if (spiel) EgoMauerMerken(teile, plan); // Kollision der Ego-Perspektive (Stadtansicht.Ego.cs)
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

    /// <summary>
    /// Die Torburg im Spiel: Anker „Anker_Tuer“ in der Durchfahrt (Kennung „Tor:&lt;Name&gt;“), Klickfläche und Schild wie beim alten Torhaus –
    /// so hoch wie die Torburg (die Severinstorburg ist 22 m hoch).
    /// </summary>
    private void TorEinrichten(string name, Node3D tor, Transform3D lage, Torteil teil)
    {
        if (teil.Anker is { } anker)
        {
            var knoten = new Node3D { Name = "Anker_Tuer", Position = anker };
            tor.AddChild(knoten);
            _modellAnker["Tor:" + name] = (knoten, null);
        }
        // Die Klickfläche liegt längs der Mauer: Torburg 16,5 m breit und 7 m tief
        var hoehe = Mathf.Max(14f, teil.Netz.GetAabb().End.Y);
        var breit = new Vector3(Mathf.Abs(lage.Basis.X.X), 0, Mathf.Abs(lage.Basis.X.Z));
        var tief = new Vector3(Mathf.Abs(lage.Basis.Z.X), 0, Mathf.Abs(lage.Basis.Z.Z));
        Klickflaeche(name, lage.Origin + new Vector3(0, hoehe / 2, 0), breit * 16.5f + tief * 7 + new Vector3(0, hoehe, 0)).GetParent().SetMeta("ego_frei", true); // die Durchfahrt bleibt für die Ego-Perspektive frei (Stadtansicht.Ego.cs)
        Beschriftung(name, lage.Origin + new Vector3(0, Mathf.Max(20f, hoehe + 4f), 0), 48, kennung: name);
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
