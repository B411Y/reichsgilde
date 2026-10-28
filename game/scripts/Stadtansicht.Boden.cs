using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Die Bodentexturen – ein Teil der <c>partial class Stadtansicht</c> (Kartenwerkstatt, Schritt 5e). Der Kern führt ein Bodenraster (alle 1 m ein
/// Punkt, elf Texturen, <see cref="Bodenraster"/>); hier wird es gezeichnet:
/// <list type="bullet">
/// <item><b>Boden</b>: Die Geländekacheln (128 m, Stadtansicht.Gelaende.cs) tragen den Boden-Shader (<c>boden.gdshaderinc</c>) statt der einfarbigen Wiese.
/// Er mischt die elf Texturen (<c>boden_farbe_hoehe.webp</c>) nach drei Gewichtsbildern je Kachel (<see cref="Bodenraster.Splat"/>); eine Kachel ohne gemalten Punkt
/// zeigt die Wiese (ohne Bilder). Ändert sich der Boden, werden nur die Bilder der betroffenen Kacheln ersetzt (<see cref="BodenStoffeNeu"/>).
/// Nur eine ganz flache Karte ohne Wege und Texturen bleibt eine einfarbige Ebene.</item>
/// <item><b>Qualität</b> (Schritt 5h, Einstellung „Boden“, <see cref="Fenster.AktuelleBodenqualitaet"/>): je Stufe ein Shader – <c>boden_niedrig</c>,
/// <c>boden_mittel</c>, <c>boden</c> (Hoch, auch Ultra); sie binden denselben Code mit einer anderen <c>BODEN_STUFE</c> ein. Wechselt die Einstellung,
/// bekommen alle Kacheln einen neuen Stoff (<see cref="BodenqualitaetNachfuehren"/>). In Hoch kommen Normalen und Rauheit aus <c>boden_normal_rauheit.webp</c>,
/// wenn es sie gibt, dazu die Grasbüschel an den Grenzen der Wiese (Stadtansicht.Bueschel.cs).</item>
/// <item><b>Deko</b> (<see cref="Bodendeko"/>): kleine Teile, die jede Textur streut (Gras, Steine, Farn, Schilf …) – dieselben Natur-Modelle und
/// MultiMeshes wie die übrige Natur; im Spiel mit ihr im Aufbau (<see cref="NaturAusKarte"/>), in der Werkstatt je Bodenkachel (64 m) neu, wo sich etwas ändert.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Das Bodenraster, das die Ansicht zeichnet: im Spiel das der Karte, in der Werkstatt das des Entwurfs (null: keins).</summary>
    private Bodenraster? BodenRaster => _entwurf?.Boden ?? _karte?.Boden;

    /// <summary>Ist irgendwo eine Textur gemalt? Sonst bleibt alles wie bisher.</summary>
    private bool BodenGemalt => BodenRaster is { IstLeer: false };

    /// <summary>Muss der Boden in Kacheln gezeichnet werden (Höhen oder Texturen)? Sonst genügt die eine Ebene.</summary>
    private bool BodenKacheln => BodenBewegt || BodenGemalt || WegeschichtAn;

    /// <summary>Bild einer Geländekachel: ihre 128 Punkte plus je einer davor und dahinter, damit Übergänge über die Kante glatt bleiben.</summary>
    private const int BodenBild = KachelZellen * 2 + 2;

    /// <summary>Die Shader je Stufe der Bodenqualität (Niedrig, Mittel, Hoch).</summary>
    private readonly Shader?[] _bodenShader = new Shader?[3];
    private TextureLayered? _bodenTexturen;
    private TextureLayered? _bodenNormalen;
    private bool _bodenShaderGeprueft;

    /// <summary>Die Stufe, mit der die Stoffe der Kacheln gebaut sind.</summary>
    private Bodenqualitaet _bodenStufe = Bodenqualitaet.Hoch;

    /// <summary>Der Stoff der flachen Streifen um die Kacheln (nur Wiese): sonst endete die Textur der Wiese an der letzten Kachel.</summary>
    private ShaderMaterial? _bodenRandStoff;

    /// <summary>Texturen des Bodens (Texture2DArray je Datei, elf Schichten untereinander, <c>tools/oberflaeche/bodentexturen_blender.py</c>): RGB Farbe
    /// und A Höhe, dazu RG Normale und B Rauheit (fehlt diese Datei, nimmt Hoch die Höhe als Unebenheit).</summary>
    private const string BodenTexturDatei = "res://assets/texturen/boden/boden_farbe_hoehe.webp",
                         BodenNormalenDatei = "res://assets/texturen/boden/boden_normal_rauheit.webp";

    /// <summary>Der Stoff und die drei Gewichtsbilder je Geländekachel, die Texturen trägt.</summary>
    private readonly Dictionary<(int I, int J), (ShaderMaterial Stoff, ImageTexture[] Bilder)> _bodenStoffe = new();

    private bool BodenShaderLaden()
    {
        if (_bodenShaderGeprueft) return _bodenShader[0] is not null;
        _bodenShaderGeprueft = true;
        string[] shader = { "res://assets/shader/boden_niedrig.gdshader", "res://assets/shader/boden_mittel.gdshader", "res://assets/shader/boden.gdshader" };
        if (shader.Any(d => !ResourceLoader.Exists(d)) || !ResourceLoader.Exists(BodenTexturDatei)) return false;
        for (var i = 0; i < shader.Length; i++) _bodenShader[i] = GD.Load<Shader>(shader[i]);
        _bodenTexturen = GD.Load<TextureLayered>(BodenTexturDatei);
        if (ResourceLoader.Exists(BodenNormalenDatei)) _bodenNormalen = GD.Load<TextureLayered>(BodenNormalenDatei);
        _bodenStufe = Fenster.AktuelleBodenqualitaet;
        if (_bodenShader.Any(x => x is null) || _bodenTexturen is null)
        {
            _bodenShader[0] = null;
            return false;
        }
        return true;
    }

    /// <summary>Ein neuer Stoff mit dem Boden-Shader der aktuellen Stufe und den Texturen (die Werte der Kachel setzt der Aufrufer).</summary>
    private ShaderMaterial BodenStoffNeu(string name)
    {
        var stoff = new ShaderMaterial { Shader = _bodenShader[(int)_bodenStufe], ResourceName = name };
        stoff.SetShaderParameter("texturen", _bodenTexturen);
        stoff.SetShaderParameter("teppich", GrasAn ? 1f : 0f); // die Wiese unter dem Grasteppich (Stadtansicht.Gras.cs)
        if (_bodenStufe == Bodenqualitaet.Hoch && _bodenNormalen is not null)
        {
            stoff.SetShaderParameter("texturen_nr", _bodenNormalen);
            stoff.SetShaderParameter("nr_da", true);
        }
        return stoff;
    }

    /// <summary>Der Stoff der flachen Streifen um die Kacheln: die Wiese des Boden-Shaders (ohne Bilder), oder die einfarbige, wenn er fehlt.</summary>
    private Material BodenRandStoff()
    {
        if (!BodenShaderLaden()) return Material(Wiese);
        if (_bodenRandStoff is null)
        {
            _bodenRandStoff = BodenStoffNeu("Boden_Rand");
            _bodenRandStoff.SetShaderParameter("gemalt", false);
            _bodenRandStoff.SetShaderParameter("teppich", 0f); // jenseits der Kacheln steht kein Grasteppich
        }
        return _bodenRandStoff;
    }

    /// <summary>
    /// Die Einstellung „Boden“ hat sich geändert (aus <see cref="GrafikNachfuehren"/>): Alle Kacheln und die Streifen bekommen einen Stoff mit dem Shader
    /// der neuen Stufe (die Bilder der Kacheln werden neu geschrieben), die Grasbüschel erscheinen nur in Hoch.
    /// </summary>
    private void BodenqualitaetNachfuehren()
    {
        BueschelSichtbarkeit(); // Stadtansicht.Bueschel.cs
        GrasNachfuehren();      // Werte des Grasteppichs (Stadtansicht.Gras.cs)
        if (!_bodenShaderGeprueft || _bodenShader[0] is null || _bodenStufe == Fenster.AktuelleBodenqualitaet) return;
        _bodenStufe = Fenster.AktuelleBodenqualitaet;
        _bodenStoffe.Clear();
        foreach (var ((i, j), kachel) in _gelaendeKacheln)
            if (IsInstanceValid(kachel)) kachel.MaterialOverride = KachelStoff(i, j);
        if (_bodenRandStoff is { } alt)
        {
            _bodenRandStoff = null;
            var neu = BodenRandStoff();
            foreach (var streifen in _bodenStreifen.Where(IsInstanceValid)) streifen.MaterialOverride = neu;
            alt.Dispose();
        }
    }

    /// <summary>Hat der Boden Punkte in der Geländekachel (samt dem Rand des Bildes)?</summary>
    private static bool BodenKachelGemalt(Bodenraster raster, int ti, int tj)
    {
        var (i0, j0) = (ti * KachelZellen * 2 - 1, tj * KachelZellen * 2 - 1);
        var (a0, b0) = Bodenraster.KachelVon(i0, j0);
        var (a1, b1) = Bodenraster.KachelVon(i0 + BodenBild - 1, j0 + BodenBild - 1);
        for (var b = b0; b <= b1; b++)
            for (var a = a0; a <= a1; a++)
                if (raster.Kacheln.Contains((a, b))) return true;
        return false;
    }

    /// <summary>
    /// Der Stoff einer Geländekachel: der Boden-Shader mit den Gewichten der Kachel und der Wegeschicht (Stadtansicht.Wegeschicht.cs). Alle Kacheln tragen
    /// ihn (eine Kachel ohne gemalten Punkt zeigt die Textur der Wiese, am Hang Fels) – sonst gäbe es Kanten zwischen Kacheln. Nur wenn der Shader fehlt
    /// (kaputter Export), die einfarbige Wiese.
    /// </summary>
    private Material KachelStoff(int ti, int tj)
    {
        if (!BodenShaderLaden()) return Material(Wiese);
        var raster = BodenRaster;
        var gemalt = raster is { IstLeer: false } && BodenKachelGemalt(raster, ti, tj);
        if (!_bodenStoffe.TryGetValue((ti, tj), out var eintrag))
        {
            var stoff = BodenStoffNeu($"Boden_{ti}_{tj}");
            stoff.SetShaderParameter("ursprung", new Vector2(ti * KachelZellen * 2 - 1, tj * KachelZellen * 2 - 1));
            stoff.SetShaderParameter("bildgroesse", (float)BodenBild);
            eintrag = (stoff, new ImageTexture[3]);
            _bodenStoffe[(ti, tj)] = eintrag;
        }
        eintrag.Stoff.SetShaderParameter("gemalt", gemalt);
        if (gemalt) BodenBilderSetzen(raster!, ti, tj, eintrag);
        SchichtSetzen(ti, tj, eintrag.Stoff);
        return eintrag.Stoff;
    }

    /// <summary>Schreibt die drei Gewichtsbilder der Kachel neu (beim ersten Mal legt es die Bilder an).</summary>
    private static void BodenBilderSetzen(Bodenraster raster, int ti, int tj, (ShaderMaterial Stoff, ImageTexture[] Bilder) eintrag)
    {
        var (i0, j0) = (ti * KachelZellen * 2 - 1, tj * KachelZellen * 2 - 1);
        for (var gruppe = 0; gruppe < 3; gruppe++)
        {
            var bild = Image.CreateFromData(BodenBild, BodenBild, false, Image.Format.Rgba8, raster.Splat(i0, j0, BodenBild, BodenBild, gruppe));
            if (eintrag.Bilder[gruppe] is { } alt) alt.Update(bild);
            else
            {
                eintrag.Bilder[gruppe] = ImageTexture.CreateFromImage(bild);
                eintrag.Stoff.SetShaderParameter("splat" + gruppe, eintrag.Bilder[gruppe]);
            }
        }
    }

    /// <summary>
    /// Der Boden hat sich im Rechteck geändert: Die Bilder der Kacheln darin werden ersetzt (das Netz bleibt). Hat sich nur geändert, <i>ob</i> irgendwo
    /// gemalt ist (erster Strich, Rückgängig bis zum Anfang), wird der ganze Boden neu gebaut.
    /// </summary>
    private void BodenStoffeNeu(Kartenrechteck bereich)
    {
        if (_randKnoten is null || _grenzen is null) return;
        if (BodenKacheln != _bodenGebaut)
        {
            RandNeuBauen();
            return;
        }
        if (!_bodenGebaut) return;
        var (i0, j0, i1, j1) = KachelBereich(bereich.Vergroessert(2));
        var (a0, b0, a1, b1) = KachelBereich(_grenzen.Aussen);
        for (var j = Math.Max(j0, b0); j <= Math.Min(j1, b1); j++)
            for (var i = Math.Max(i0, a0); i <= Math.Min(i1, a1); i++)
            {
                if (_gelaendeKacheln.TryGetValue((i, j), out var kachel)) kachel.MaterialOverride = KachelStoff(i, j);
                BueschelBodenGeaendert(i, j); // Grasbüschel an den Grenzen (Stadtansicht.Bueschel.cs)
            }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Deko
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Im Spiel: die Deko des Bodens in den Aufbau der Natur (ein MultiMesh je Art über die ganze Karte).</summary>
    private void BodendekoAusKarte(Stadtkarte karte)
    {
        if (karte.Boden is not { IstLeer: false }) return;
        foreach (var lage in Bodendeko.Von(karte, Modellmass).Alle()) NaturSetzen(lage);
    }

    /// <summary>Zeichnet Pflanzenlagen als ein MultiMesh je Art (Modell und Knoten mit Variante) unter <paramref name="knoten"/>; gibt die Zahl zurück.</summary>
    private int LagenZeichnen(Node3D knoten, IReadOnlyCollection<Pflanzenlage> lagen)
    {
        foreach (var art in lagen.Where(l => l.Art is { Modell: not null, Knoten: not null }).GroupBy(l => (Datei: l.Art.Modell!, Knoten: l.Art.Knoten + l.Variante)))
        {
            if (NaturNetz(art.Key.Datei, art.Key.Knoten) is not { } netz) continue;
            var liste = art.ToList();
            var vielfach = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = netz, InstanceCount = liste.Count };
            for (var i = 0; i < liste.Count; i++) vielfach.SetInstanceTransform(i, PflanzenLage(liste[i]));
            knoten.AddChild(new MultiMeshInstance3D
            {
                Name = art.Key.Knoten, Multimesh = vielfach,
                CastShadow = IstBodenDatei(art.Key.Datei) ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On,
            });
        }
        return lagen.Count;
    }
}
