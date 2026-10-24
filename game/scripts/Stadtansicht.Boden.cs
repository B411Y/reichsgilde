using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Die Bodentexturen – ein Teil der <c>partial class Stadtansicht</c> (Kartenwerkstatt, Schritt 5e). Der Kern führt ein Bodenraster (alle 1 m ein
/// Punkt, elf Texturen, <see cref="Bodenraster"/>); hier wird es gezeichnet:
/// <list type="bullet">
/// <item><b>Boden</b>: Die Geländekacheln (128 m, Stadtansicht.Gelaende.cs) bekommen, wo gemalt wurde, den Boden-Shader (<c>boden.gdshader</c>) statt der
/// einfarbigen Wiese. Er mischt die elf Texturen (<c>boden.png</c>, <c>tools/oberflaeche/bodentexturen.py</c>) nach drei Gewichtsbildern je Kachel
/// (<see cref="Bodenraster.Splat"/>). Eine Karte <b>ohne</b> Boden (Greybox, Köln) hat weder Bilder noch Shader und zeichnet wie bisher; auch eine Kachel
/// ohne gemalten Punkt behält ihre Wiese. Ändert sich der Boden, werden nur die Bilder der betroffenen Kacheln ersetzt (<see cref="BodenStoffeNeu"/>).</item>
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

    private Shader? _bodenShader;
    private TextureLayered? _bodenTexturen;
    private bool _bodenShaderGeprueft;

    /// <summary>Der Stoff und die drei Gewichtsbilder je Geländekachel, die Texturen trägt.</summary>
    private readonly Dictionary<(int I, int J), (ShaderMaterial Stoff, ImageTexture[] Bilder)> _bodenStoffe = new();

    private bool BodenShaderLaden()
    {
        if (_bodenShaderGeprueft) return _bodenShader is not null;
        _bodenShaderGeprueft = true;
        const string shader = "res://assets/shader/boden.gdshader", texturen = "res://assets/texturen/boden.png";
        if (!ResourceLoader.Exists(shader) || !ResourceLoader.Exists(texturen)) return false;
        _bodenShader = GD.Load<Shader>(shader);
        _bodenTexturen = GD.Load<TextureLayered>(texturen);
        return _bodenShader is not null && _bodenTexturen is not null;
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
    /// Der Stoff einer Geländekachel: die Wiese, oder der Boden-Shader mit den Gewichten der Kachel und der Wegeschicht (Stadtansicht.Wegeschicht.cs).
    /// Hat die Karte Wege, tragen alle Kacheln den Shader (eine Kachel ohne gemalten Punkt zeigt die Textur der Wiese) – sonst gäbe es Kanten zwischen Kacheln.
    /// </summary>
    private Material KachelStoff(int ti, int tj)
    {
        var raster = BodenRaster;
        var gemalt = raster is { IstLeer: false } && BodenShaderLaden() && BodenKachelGemalt(raster, ti, tj);
        if (!gemalt && !WegeschichtAn) return Material(Wiese);
        if (!_bodenStoffe.TryGetValue((ti, tj), out var eintrag))
        {
            var stoff = new ShaderMaterial { Shader = _bodenShader, ResourceName = $"Boden_{ti}_{tj}" };
            stoff.SetShaderParameter("texturen", _bodenTexturen);
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
                if (_gelaendeKacheln.TryGetValue((i, j), out var kachel)) kachel.MaterialOverride = KachelStoff(i, j);
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
                CastShadow = art.Key.Datei == "natur/natur_boden" ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On,
            });
        }
        return lagen.Count;
    }
}
