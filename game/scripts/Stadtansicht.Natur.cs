using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die Natur vor der Stadt – ein Teil der <c>partial class Stadtansicht</c>.
///
/// Wald und Streu kommen aus den Natur-Flächen der Karte (<see cref="NaturAusKarte"/>, verteilt vom Kern); jede Art ist ein Baustein des Katalogs, ein Knoten in einem
/// der Baukästen <c>natur_baeume</c>, <c>natur_buesche</c>, <c>natur_boden</c>, <c>natur_steine</c> und <c>natur_holz</c> (je Art und Variante ein
/// Knoten, der Fuß im Nullpunkt; Konventionen in <c>docs/bereiche/darstellung.md</c>). Alles steht fest aus dem Startwert der Fläche (gleich bei
/// jedem Start), jede Art ist ein <see cref="MultiMeshInstance3D"/>: Hunderte Teile kosten nur ein paar Zeichenaufrufe.
/// Dazu stellt <see cref="JahreszeitSetzen"/> die Jahreszeit ein: Das Laub aller Modelle färbt sich um (Material „Laub“, „Laub hell“, „Laub dunkel“),
/// im Winter zeigen Bäume und Büsche ihre kahlen „…_Winter“-Fassungen bzw. „Winteraeste_…“ statt der Laubkrone.
/// </summary>
public partial class Stadtansicht
{
    // ═════════════════════════════════════════════════════════════════════════
    //  Sammeln und aufbauen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Netze der Baukästen, je Datei und Knoten einmal geholt.</summary>
    private readonly Dictionary<(string Datei, string Knoten), Mesh?> _naturNetze = new();

    /// <summary>Wo welche Art steht: Lage (Ort, Drehung, Größe) aller Kopien, bis <see cref="NaturAufbauen"/> sie in je ein MultiMesh packt.</summary>
    private readonly Dictionary<(string Datei, string Knoten), List<Transform3D>> _naturLagen = new();

    /// <summary>Die MultiMeshes der Arten mit kahler Fassung: das Netz im Sommer und das im Winter (<see cref="JahreszeitSetzen"/> tauscht).</summary>
    private readonly List<(MultiMesh Vielfach, Mesh Sommer, Mesh Winter)> _naturWinter = new();

    private Mesh? NaturNetz(string datei, string knoten, bool pflicht = true)
    {
        if (_naturNetze.TryGetValue((datei, knoten), out var netz)) return netz;
        if (Modell(datei)?.Instantiate<Node3D>() is { } kasten)
        {
            netz = (kasten.FindChild(knoten, true, false) as MeshInstance3D)?.Mesh;
            kasten.Free();
            if (netz is null) { if (pflicht) GD.PushError($"Modell unvollständig: res://assets/modelle/{datei}.glb hat keinen Knoten „{knoten}“."); }
            else LaubMelden(netz);
        }
        return _naturNetze[(datei, knoten)] = netz;
    }

    /// <summary>Merkt eine Kopie der Art vor: Drehung um die Hochachse und Größe (ein Faktor für alle Richtungen).</summary>
    private void NaturSetzen(string datei, string knoten, Vector3 ort, float drehung, float groesse)
    {
        if (!_naturLagen.TryGetValue((datei, knoten), out var lagen)) _naturLagen[(datei, knoten)] = lagen = new();
        lagen.Add(new Transform3D(new Basis(Vector3.Up, drehung).Scaled(Vector3.One * groesse), ort));
    }

    /// <summary>
    /// Packt alles Vorgemerkte in je ein MultiMesh je Art. Bodenpflanzen werfen keinen Schatten (zu klein, die Karte der Sonne wäre vergeudet).
    /// </summary>
    private void NaturAufbauen()
    {
        var natur = new Node3D { Name = "Natur" };
        AddChild(natur);
        foreach (var ((datei, knoten), lagen) in _naturLagen)
        {
            if (NaturNetz(datei, knoten) is not { } netz) continue;
            // Gemischt (immer gleich): Die Einstellung „Natur-Dichte“ zeigt nur die ersten Kopien (Stadtansicht.Grafik.cs), und die sollen
            // über alle Flächen verteilt sein, nicht die erste Fläche ganz und die letzte gar nicht.
            var mischer = new Random(lagen.Count * 7919 + knoten.Length);
            for (var i = lagen.Count - 1; i > 0; i--)
            {
                var j = mischer.Next(i + 1);
                (lagen[i], lagen[j]) = (lagen[j], lagen[i]);
            }
            var vielfach = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = netz, InstanceCount = lagen.Count };
            for (var i = 0; i < lagen.Count; i++) vielfach.SetInstanceTransform(i, lagen[i]);
            // Bäume und Büsche haben eine kahle Fassung „<Name>_Winter“ (Tanne, Ginster und Wacholder nicht): im Winter tauscht JahreszeitSetzen das Netz
            if ((datei is "natur/natur_baeume" or "natur/natur_buesche") && NaturNetz(datei, knoten + "_Winter", pflicht: false) is { } winter)
            {
                _naturWinter.Add((vielfach, netz, winter));
                if (_jahreszeit == Jahreszeit.Winter) vielfach.Mesh = winter;
            }
            var teil = new MultiMeshInstance3D
            {
                Name = knoten,
                Multimesh = vielfach,
                CastShadow = datei == "natur/natur_boden" ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On,
            };
            natur.AddChild(teil);
            NaturDetailMelden(teil, datei); // Natur-Dichte und Sichtweite der Feinheiten (Stadtansicht.Grafik.cs)
        }
        _naturLagen.Clear();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Natur aus der Karte
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die Natur der Karte (<c>natur</c>): Wo welche Pflanze steht, rechnet der Kern (<see cref="NaturVerteilung.Alle"/>) – dieselbe Rechnung
    /// zeigt die Kartenwerkstatt. Jede Fläche bekommt so viele Streupunkte, wie ihre Dichte sagt, gleich verteilt aus ihrem Startwert; an jedem
    /// Punkt eine Gruppe nach Gewicht, ihre Teile in einer Scheibe mit dem Radius der Gruppe. Auf Wegen, Gebäuden, Bauzonen, in Aussparungen und
    /// in der Stadt wächst nichts (<see cref="Naturfreiraum"/>); dazu die einzeln gesetzten Pflanzen. Hier wird nur noch gezeichnet.
    /// </summary>
    private void NaturAusKarte(Stadtkarte karte)
    {
        foreach (var lage in NaturVerteilung.Alle(karte, Modellmass)) NaturSetzen(lage);
        NaturAufbauen();
    }

    /// <summary>Merkt eine Pflanze des Kerns vor: Modell und Knoten der Art mit der Variante („Tanne2“), Drehung in Grad, Größe.</summary>
    private void NaturSetzen(Pflanzenlage lage)
    {
        if (lage.Art is not { Modell: { } datei, Knoten: { } knoten }) return;
        NaturSetzen(datei, knoten + lage.Variante, new Vector3((float)lage.X, 0, (float)lage.Z), Mathf.DegToRad((float)lage.Drehung), (float)lage.Massstab);
    }

    /// <summary>
    /// Die halben Maße eines Modells um seinen Ursprung (ohne Maßstab) – für den Kern, der die Modelle nicht kennt: Umlandbetriebe und
    /// -stellen sind größer als ihre Fläche im Katalog (Gelände, Zaun, Beete), und um das ganze Modell bleibt die Natur frei.
    /// </summary>
    private (double X, double Z)? Modellmass(Baustein b)
    {
        if (b.Modell is not { } datei) return null;
        var masse = UmlandAnkerImModell(datei).Masse;
        return (Mathf.Max(-masse.Position.X, masse.End.X), Mathf.Max(-masse.Position.Z, masse.End.Z));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Jahreszeit
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Das Laub aller geladenen Modelle: das Material und seine Sommerfarbe (so wie das Modell es mitbringt).</summary>
    private readonly Dictionary<BaseMaterial3D, Color> _laubStoffe = new();
    private Jahreszeit _jahreszeit = Jahreszeit.Sommer;

    /// <summary>Herbstfarben des Laubs (sRGB): „Laub hell“ gelb, „Laub“ orange, „Laub dunkel“ braun.</summary>
    private static readonly Dictionary<string, Color> Herbstlaub = new()
    {
        ["Laub hell"] = new Color(0.86f, 0.70f, 0.22f),
        ["Laub"] = new Color(0.78f, 0.40f, 0.12f),
        ["Laub dunkel"] = new Color(0.50f, 0.28f, 0.13f),
    };

    /// <summary>
    /// Winterfarben (sRGB) für das Laub, das im Winter noch dran ist, wenn die Kronen kahl sind – Buchshecken, Beete, Weide, Grasbüschel und Marktware:
    /// verdorrt und matt (Stroh, Lehm, Moor) statt herbstlich bunt.
    /// </summary>
    private static readonly Dictionary<string, Color> Winterlaub = new()
    {
        ["Laub hell"] = new Color(0.60f, 0.54f, 0.36f),
        ["Laub"] = new Color(0.40f, 0.34f, 0.23f),
        ["Laub dunkel"] = new Color(0.29f, 0.25f, 0.19f),
    };

    /// <summary>Merkt ein Laubmaterial vor und färbt es sofort nach der gerade geltenden Jahreszeit. Andere Materialien bleiben unberührt.</summary>
    private void LaubMelden(BaseMaterial3D stoff)
    {
        if (!Herbstlaub.ContainsKey(stoff.ResourceName) || _laubStoffe.ContainsKey(stoff)) return;
        _laubStoffe[stoff] = stoff.AlbedoColor;
        LaubFaerben(stoff, _laubStoffe[stoff]);
    }

    /// <summary>Wie <see cref="LaubMelden(BaseMaterial3D)"/> für alle Flächen eines Netzes.</summary>
    private void LaubMelden(Mesh netz)
    {
        for (var i = 0; i < netz.GetSurfaceCount(); i++)
            if (netz.SurfaceGetMaterial(i) is BaseMaterial3D stoff) LaubMelden(stoff);
    }

    private void LaubFaerben(BaseMaterial3D stoff, Color sommer) =>
        stoff.AlbedoColor = _jahreszeit switch
        {
            Jahreszeit.Herbst => Herbstlaub[stoff.ResourceName],
            Jahreszeit.Winter => Winterlaub[stoff.ResourceName],
            _ => sommer,
        };

    // ═════════════════════════════════════════════════════════════════════════
    //  Kronen im Winter
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die Laubkronen („Laubkrone_*“) und kahlen Äste („Winteraeste_*“) der eingesetzten Modelle: Beide sind Kinder des Knotens, in dem der Baum steht
    /// (Stufe, Haus, Wald), und folgen so dessen Sichtbarkeit. Im Winter zeigt das Spiel die Äste statt der Krone.
    /// </summary>
    private readonly List<(MeshInstance3D Knoten, bool Aeste)> _kronen = new();
    private int _kronenSchwelle = 64;

    /// <summary>
    /// Meldet alle Kronen und Winteräste unterhalb von <paramref name="wurzel"/> an und stellt sie auf die gerade geltende Jahreszeit ein – auch Modelle,
    /// die erst später gebaut werden, haben so sofort den richtigen Stand. Die Laubmaterialien der Kronen melden sich dabei mit.
    /// </summary>
    private void KronenMelden(Node wurzel)
    {
        var winter = _jahreszeit == Jahreszeit.Winter;
        foreach (var teil in wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            var name = teil.Name.ToString();
            var aeste = name.StartsWith("Winteraeste_");
            if (!aeste && !name.StartsWith("Laubkrone_")) continue;
            if (_kronen.Count >= _kronenSchwelle) // abgerissene und ersetzte Häuser nehmen ihre Kronen mit
            {
                _kronen.RemoveAll(k => !IsInstanceValid(k.Knoten));
                _kronenSchwelle = Math.Max(64, _kronen.Count * 2);
            }
            _kronen.Add((teil, aeste));
            teil.Visible = aeste == winter;
            if (teil.Mesh is { } netz) LaubMelden(netz);
        }
    }

    /// <summary>
    /// Stellt die Jahreszeit ein (jedes Bild gefragt, angefasst wird nur beim Wechsel). Frühling und Sommer zeigen das Laub, wie die Modelle es
    /// mitbringen, Herbst gelb, orange und braun. Im Winter zeigen Bäume und Büsche der Natur ihre kahlen Fassungen („…_Winter“), in den Modellen
    /// wechseln „Laubkrone_*“ zu „Winteraeste_*“, und das übrige Laub (Hecken, Beete, Weide) verdorrt zu Stroh und Braun.
    /// </summary>
    private void JahreszeitSetzen(Jahreszeit jahreszeit)
    {
        if (jahreszeit == _jahreszeit) return;
        _jahreszeit = jahreszeit;
        foreach (var (stoff, sommer) in _laubStoffe) LaubFaerben(stoff, sommer);
        FeldJahreszeit(); // die Äcker liegen im Winter kahl da (Stadtansicht.Rand.cs)

        var winter = jahreszeit == Jahreszeit.Winter;
        foreach (var (vielfach, sommerNetz, winterNetz) in _naturWinter) vielfach.Mesh = OberflaecheNetz(winter ? winterNetz : sommerNetz); // mit Struktur, wenn die Oberflächendetails an sind
        _kronen.RemoveAll(k => !IsInstanceValid(k.Knoten));
        foreach (var (knoten, aeste) in _kronen) knoten.Visible = aeste == winter;
    }
}
