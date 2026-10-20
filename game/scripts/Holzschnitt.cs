using Godot;

namespace Reichsgilde;

/// <summary>
/// Der Holzschnitt-Stil als Grafikoption: Diese Klasse macht aus den Materialien des Spiels Holzschnitt-Materialien
/// (<c>holzschnitt.gdshader</c>) und gibt dem Spiel seine Materialien zurück, wenn der Stil ausgeschaltet wird.
/// Verdrahtet wird sie von der Stadtansicht (<c>Stadtansicht.Holzschnitt.cs</c>): Dort stehen der Schalter, der neue Knoten
/// erfasst, und der Umriss.
///
/// <b>Das Spiel behält seine Materialien.</b> Die Modelle und die Grundkörper behalten ihre <see cref="StandardMaterial3D"/>
/// (das Spiel liest ihren Namen, färbt sie um und schaltet ihr Leuchten), und der Holzschnitt-Stil legt sich beim
/// <see cref="RenderingServer"/> darüber: Je Fläche bekommt die Instanz dort ein Holzschnitt-Material
/// (<see cref="RenderingServer.InstanceSetSurfaceOverrideMaterial"/>, bei einem <see cref="GeometryInstance3D.MaterialOverride"/>
/// <see cref="RenderingServer.InstanceGeometrySetMaterialOverride"/>). Die Eigenschaften der Knoten ändern sich dabei nicht.
/// Ausschalten heißt darum nur: den Wert der Knoten wieder an den RenderingServer geben.
///
/// <b>Je Ausgangsmaterial ein Holzschnitt-Material.</b> Was sich Knoten teilen, bleibt geteilt, die Zeichenaufrufe steigen nicht
/// (<see cref="Ableiten"/>). Färbt das Spiel ein Ausgangsmaterial um (Hausfarbe, Laub je Jahreszeit, Werkstück), zieht
/// <see cref="Abgleichen"/> die Farbe nach: reihum, ein kleines Stück je Bild.
///
/// <b>MultiMesh</b> (Natur, Mauer) kennt keine Überschreibung je Fläche: Dort kommt eine Kopie des Netzes mit
/// Holzschnitt-Materialien an die Stelle des Netzes (<see cref="Netz"/>), beim Ausschalten wieder das Original.
/// </summary>
internal sealed class Holzschnitt
{
    private const string ShaderPfad = "res://shader/holzschnitt.gdshader";
    private const string FigurShaderPfad = "res://shader/figur.gdshader";
    private const string ArtenPfad = "res://assets/holzschnitt/materialarten.json";
    private static readonly string[] Texturen = { "schraffur", "papier", "struktur_a", "struktur_b" };

    /// <summary>Diese Materialien leuchten oder spiegeln und behalten ihr Aussehen (Namen wie in <c>docs/bereiche/darstellung.md</c>).</summary>
    private static readonly HashSet<string> Behalten = new() { "Glut", "Fenster", "Glas", "Wasser" };

    /// <summary>Modelle mit zusammengelegten Farben tragen Farbe und Materialart je Fläche im Netz (Eckenfarbe, UV.y).</summary>
    private static readonly HashSet<string> JeFlaeche = new() { "Farben", "Figur" };

    private readonly Shader _shader = GD.Load<Shader>(ShaderPfad);
    private readonly Texture2D[] _texturen = Texturen.Select(n => GD.Load<Texture2D>($"res://assets/holzschnitt/{n}.png")).ToArray();
    private readonly Dictionary<string, int> _artNamen = new();
    private readonly List<(string Teil, int Art)> _artTeile = new();
    private readonly int _artFigur;

    /// <summary>Die Farben einer Figur (Gewand, Beinkleid, Haut, Haar, Kopfbedeckung): Wer gleich aussieht, teilt sich ein Material.</summary>
    private readonly record struct Tracht(Color Gewand, Color Beinkleid, Color Haut, Color Haar, Color Kopf);
    private readonly Dictionary<Tracht, ShaderMaterial> _trachten = new();

    /// <summary>Zu jedem Ausgangsmaterial sein Holzschnitt-Material; null = bleibt, wie es ist (leuchtet, ist durchsichtig …).</summary>
    private readonly Dictionary<Material, ShaderMaterial?> _abgeleitet = new();

    /// <summary>Ausgangsmaterial und Holzschnitt-Material mit der Farbe, die zuletzt übertragen wurde (<see cref="Abgleichen"/>).</summary>
    private readonly List<Spiegel> _spiegel = new();
    private int _naechster;

    /// <summary>Kopien der Netze für MultiMesh: Original → Kopie und Kopie → Original.</summary>
    private readonly Dictionary<Mesh, Mesh> _netze = new();
    private readonly Dictionary<Mesh, Mesh> _netzOriginale = new();

    private sealed class Spiegel
    {
        public required BaseMaterial3D Quelle { get; init; }
        public required ShaderMaterial Ziel { get; init; }
        public Color Zuletzt { get; set; }
    }

    public Holzschnitt()
    {
        if (Json.ParseString(Godot.FileAccess.GetFileAsString(ArtenPfad)).AsGodotDictionary() is { Count: > 0 } tabelle)
        {
            var arten = tabelle["arten"].AsGodotDictionary();
            foreach (var (name, art) in tabelle["namen"].AsGodotDictionary())
                _artNamen[name.AsString()] = arten[art.AsString()].AsInt32();
            foreach (var regel in tabelle["enthaelt"].AsGodotArray())
            {
                var paar = regel.AsGodotArray();
                _artTeile.Add((paar[0].AsString(), arten[paar[1].AsString()].AsInt32()));
            }
        }
        else GD.PushError($"Holzschnitt: {ArtenPfad} fehlt oder ist leer.");
        _artFigur = ArtVon("Figur");
    }

    /// <summary>Die Materialart zu einem Materialnamen: erst der genaue Name, dann das erste passende Teilwort, sonst 0 (keine Struktur).</summary>
    private int ArtVon(string name)
    {
        if (_artNamen.TryGetValue(name, out var art)) return art;
        foreach (var (teil, teilArt) in _artTeile)
            if (name.Contains(teil)) return teilArt;
        return 0;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Materialien
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Das Holzschnitt-Material zu einem Ausgangsmaterial – oder null, wenn es bleiben soll: Glut, Fenster, Glas und Wasser
    /// (Namen), alles Leuchtende, Durchsichtige und Unbeschattete (Auswahlring, Baumodus, Hilfslinien) und Materialien mit Textur.
    /// Auch fremde Shader bleiben (Fahnen); das Figurenmaterial (<c>figur.gdshader</c>) hängt von der Figur ab und kommt aus <see cref="FigurStoff"/>.
    /// Je Ausgangsmaterial wird nur einmal abgeleitet.
    /// </summary>
    public ShaderMaterial? Ableiten(Material? quelle)
    {
        if (quelle is null) return null;
        if (_abgeleitet.TryGetValue(quelle, out var vorhanden)) return vorhanden;
        return _abgeleitet[quelle] = Erzeugen(quelle);
    }

    private ShaderMaterial? Erzeugen(Material quelle)
    {
        if (quelle is not BaseMaterial3D stoff) return null; // auch das Figurenmaterial: das hängt von der Figur ab (FigurStoff)
        var name = stoff.ResourceName;
        if (Behalten.Contains(name) || stoff.Transparency != BaseMaterial3D.TransparencyEnum.Disabled
            || stoff.ShadingMode == BaseMaterial3D.ShadingModeEnum.Unshaded || stoff.EmissionEnabled || stoff.AlbedoTexture is not null)
            return null;

        var ziel = Neu(name);
        var jeFlaeche = JeFlaeche.Contains(name);
        ziel.SetShaderParameter("farbe", stoff.AlbedoColor);
        ziel.SetShaderParameter("eckenfarbe", jeFlaeche);
        ziel.SetShaderParameter("art_aus_uv", jeFlaeche);
        ziel.SetShaderParameter("art", ArtVon(name));
        ziel.SetShaderParameter("zonen", name == "Figur");
        _spiegel.Add(new Spiegel { Quelle = stoff, Ziel = ziel, Zuletzt = stoff.AlbedoColor });
        return ziel;
    }

    /// <summary>
    /// Das Holzschnitt-Material einer Figur: Zone und Grundfarbe je Fläche stehen im Netz (UV.x, Eckenfarbe), die Farben von Gewand, Haut,
    /// Haar … je Figur setzt das Spiel als Instanz-Uniform am Knoten (<c>figur.gdshader</c>). Hier werden sie zu gewöhnlichen Uniforms eines
    /// Materials je Tracht: Jede Instanz mit Instanz-Uniform belegt 16 Plätze im Puffer der Grafikkarte (4096 in Forward+, 256 im
    /// Compatibility-Renderer), und der Holzschnitt gilt für alle Knoten der Stadt.
    /// </summary>
    private ShaderMaterial FigurStoff(GeometryInstance3D knoten)
    {
        Color Farbe(string name, Color vorgabe) => knoten.GetInstanceShaderParameter(name) is { VariantType: Variant.Type.Color } v ? v.AsColor() : vorgabe;
        var tracht = new Tracht(Farbe("gewand", new Color(0.5f, 0.4f, 0.3f)), Farbe("beinkleid", new Color(0.35f, 0.3f, 0.25f)),
                                Farbe("haut", new Color(0.85f, 0.66f, 0.52f)), Farbe("haar", new Color(0.3f, 0.2f, 0.12f)), Farbe("kopfbedeckung", new Color(0.6f, 0.5f, 0.3f)));
        if (_trachten.TryGetValue(tracht, out var vorhanden)) return vorhanden;
        var figur = Neu("Figur");
        figur.SetShaderParameter("eckenfarbe", true);
        figur.SetShaderParameter("art_aus_uv", true);
        figur.SetShaderParameter("art", _artFigur);
        figur.SetShaderParameter("zonen", true);
        figur.SetShaderParameter("gewand", tracht.Gewand);
        figur.SetShaderParameter("beinkleid", tracht.Beinkleid);
        figur.SetShaderParameter("haut", tracht.Haut);
        figur.SetShaderParameter("haar", tracht.Haar);
        figur.SetShaderParameter("kopfbedeckung", tracht.Kopf);
        return _trachten[tracht] = figur;
    }

    private ShaderMaterial Neu(string name)
    {
        var material = new ShaderMaterial { Shader = _shader, ResourceName = name };
        for (var i = 0; i < Texturen.Length; i++) material.SetShaderParameter(Texturen[i], _texturen[i]);
        return material;
    }

    /// <summary>
    /// Zieht die Farbe der Holzschnitt-Materialien nach, wenn das Spiel das Ausgangsmaterial umgefärbt hat (Hausfarbe, Laub, Werkstück).
    /// Reihum, etwa alle 10 Bilder ist jedes einmal dran: eine Abfrage je Material kostet wenig, alle zugleich wären zu viel.
    /// </summary>
    public void Abgleichen()
    {
        if (_spiegel.Count == 0) return;
        var anzahl = Math.Max(128, _spiegel.Count / 10 + 1);
        for (var i = 0; i < anzahl && i < _spiegel.Count; i++)
        {
            _naechster = (_naechster + 1) % _spiegel.Count;
            var s = _spiegel[_naechster];
            var farbe = s.Quelle.AlbedoColor;
            if (farbe == s.Zuletzt) continue;
            s.Zuletzt = farbe;
            s.Ziel.SetShaderParameter("farbe", farbe);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Knoten
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gibt dem Knoten beim RenderingServer die Holzschnitt-Materialien. Maßgeblich ist, was das Spiel am Knoten eingestellt hat:
    /// ein <see cref="GeometryInstance3D.MaterialOverride"/> gilt für alle Flächen, sonst je Fläche die Überschreibung des Knotens
    /// oder das Material des Netzes. Wiederholbar: Nach einer Änderung am Knoten stellt ein neuer Aufruf alles wieder richtig.
    /// </summary>
    public void Umstellen(MeshInstance3D knoten)
    {
        if (knoten.Mesh is not { } netz) return;
        var instanz = knoten.GetInstance();
        if (knoten.MaterialOverride is { } alles)
        {
            var figur = alles is ShaderMaterial { Shader.ResourcePath: FigurShaderPfad };
            RenderingServer.InstanceGeometrySetMaterialOverride(instanz, (figur ? FigurStoff(knoten) : Ableiten(alles) ?? alles).GetRid());
            return;
        }
        RenderingServer.InstanceGeometrySetMaterialOverride(instanz, new Rid());
        for (var i = 0; i < netz.GetSurfaceCount(); i++)
        {
            var ueberschrieben = knoten.GetSurfaceOverrideMaterial(i);
            var ziel = Ableiten(ueberschrieben ?? netz.SurfaceGetMaterial(i)) ?? ueberschrieben;
            RenderingServer.InstanceSetSurfaceOverrideMaterial(instanz, i, ziel?.GetRid() ?? new Rid());
        }
    }

    /// <summary>Gibt dem RenderingServer wieder an, was das Spiel am Knoten eingestellt hat – so, als hätte es den Holzschnitt nie gegeben.</summary>
    public static void Zuruecksetzen(MeshInstance3D knoten)
    {
        var instanz = knoten.GetInstance();
        RenderingServer.InstanceGeometrySetMaterialOverride(instanz, knoten.MaterialOverride?.GetRid() ?? new Rid());
        for (var i = 0; i < (knoten.Mesh?.GetSurfaceCount() ?? 0); i++)
            RenderingServer.InstanceSetSurfaceOverrideMaterial(instanz, i, knoten.GetSurfaceOverrideMaterial(i)?.GetRid() ?? new Rid());
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  MultiMesh
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Das Netz mit Holzschnitt-Materialien: eine Kopie des Originals, einmal je Netz. Ein Netz, das schon eine Kopie ist, bleibt, wie es ist.
    /// Die Flächen ohne Holzschnitt-Material (Glut, Glas …) behalten ihr Material.
    /// </summary>
    public Mesh Netz(Mesh original)
    {
        if (_netzOriginale.ContainsKey(original)) return original;
        if (_netze.TryGetValue(original, out var kopie)) return kopie;
        var neu = new ArrayMesh();
        for (var i = 0; i < original.GetSurfaceCount(); i++)
        {
            neu.AddSurfaceFromArrays((original as ArrayMesh)?.SurfaceGetPrimitiveType(i) ?? Mesh.PrimitiveType.Triangles, original.SurfaceGetArrays(i));
            var stoff = original.SurfaceGetMaterial(i);
            neu.SurfaceSetMaterial(i, Ableiten(stoff) ?? stoff);
        }
        _netze[original] = neu;
        _netzOriginale[neu] = original;
        return neu;
    }

    /// <summary>Stellt das Netz eines MultiMesh auf die Kopie mit Holzschnitt-Materialien um (auch nach einem Netzwechsel, etwa im Winter).</summary>
    public void Umstellen(MultiMeshInstance3D knoten)
    {
        if (knoten.Multimesh is { Mesh: { } netz } vielfach) vielfach.Mesh = Netz(netz);
    }

    /// <summary>Gibt einem MultiMesh sein Original-Netz zurück.</summary>
    public void Zuruecksetzen(MultiMeshInstance3D knoten)
    {
        if (knoten.Multimesh is { Mesh: { } netz } vielfach && _netzOriginale.TryGetValue(netz, out var original)) vielfach.Mesh = original;
    }
}
