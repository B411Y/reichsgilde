using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Die Kulissenberge am Kartenrand (Felspinsel F7) – ein Teil der <c>partial class Stadtansicht</c>, nur im Spiel. Was hinter der Karte steht, sagt
/// <c>game/daten/berge.json</c>: die Karte (<c>karte</c>), das Modell (<c>modell</c>, drei Staffeln <c>Staffel_Nah/Mitte/Fern</c>, Ursprung = Ursprung der
/// Karte, genordet) und die Berge mit Namen und Ort im Spiel. Die Berge sind Kulisse: keine Kollision, kein Schatten, die Maus trifft sie nicht, sie
/// stehen nicht im Höhenraster. Auf der Stufe „Niedrig“ (Boden) nur die nahe Staffel, mit weicherer Silhouette (Shader-Uniform <c>allein</c>).
/// <list type="bullet">
/// <item><b>Dunst</b> (<c>kulisse.gdshader</c>): je weiter hinten und je höher, desto mehr Himmelsfarbe; die Farbe ist die des Dunstrings am Rand
/// (<see cref="DunstFarbeNachfuehren"/>), mehr bei Nebel und Regen, weniger in der Nacht (die Berge stehen dann dunkel vor dem Nachthimmel).</item>
/// <item><b>Namen</b>: Fährt die Maus über einen Gipfel, zeigt ein Schild an der Maus seinen Namen, das Gebiet und die Höhe über NN (Gipfel vor Gebieten).</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    private const string BergePfad = "res://daten/berge.json";

    /// <summary>So nah (Bildpunkte) muss die Maus einem Gipfel kommen bzw. der Mitte eines Gebiets, damit sein Name erscheint.</summary>
    private const float GipfelFang = 36f, GebietFang = 110f;

    private readonly record struct Kulissenberg(string Name, bool Gipfel, string? Gebiet, int? HoeheNN, Vector3 Ort);

    private Node3D? _kulisse;
    private ShaderMaterial? _kulisseStoff;
    private readonly List<Kulissenberg> _kulissenberge = new();
    private PanelContainer? _bergSchild;
    private Label? _bergSchildName, _bergSchildHinweis;
    private string? _bergGezeigt;
    private (Color Himmel, float Dunst, float Nacht) _kulisseStand = (new Color(-1, 0, 0), -1, -1);

    /// <summary>Die Kulisse der Karte, wenn <c>berge.json</c> zu ihr gehört (nur im Spiel; die Werkstatt zeigt den Rand ohne).</summary>
    private void KulisseAusKarte(Stadtkarte karte)
    {
        if (_entwurf is not null || !Godot.FileAccess.FileExists(BergePfad)) return;
        var wurzel = Json.ParseString(Godot.FileAccess.GetFileAsString(BergePfad)).AsGodotDictionary();
        if (wurzel is null || !wurzel.ContainsKey("karte") || wurzel["karte"].AsString() != karte.Kennung || !wurzel.ContainsKey("modell")) return;
        if (Modell(wurzel["modell"].AsString())?.Instantiate<Node3D>() is not { } modell)
        {
            GD.PushError($"[kulisse] Modell {wurzel["modell"]} fehlt.");
            return;
        }
        _kulisseStoff = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/kulisse.gdshader"), ResourceName = "Kulisse" };
        foreach (var teil in modell.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            teil.MaterialOverride = _kulisseStoff;
            teil.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            teil.GIMode = GeometryInstance3D.GIModeEnum.Disabled;
        }
        modell.Name = "Kulisse";
        _kulisse = modell;
        AddChild(modell);
        KulisseStufeNachfuehren();

        if (wurzel.ContainsKey("berge"))
            foreach (var eintrag in wurzel["berge"].AsGodotArray())
            {
                var d = eintrag.AsGodotDictionary();
                if (!d.ContainsKey("name") || !d.ContainsKey("spiel") || d["spiel"].AsGodotArray() is not { Count: 3 } o) continue;
                _kulissenberge.Add(new Kulissenberg(d["name"].AsString(), d.ContainsKey("art") && d["art"].AsString() == "gipfel",
                    d.ContainsKey("gebiet") ? d["gebiet"].AsString() : null, d.ContainsKey("hoehe_nn") ? d["hoehe_nn"].AsInt32() : null,
                    new Vector3((float)o[0].AsDouble(), (float)o[1].AsDouble(), (float)o[2].AsDouble())));
            }
    }

    /// <summary>Auf der Stufe „Niedrig“ (Boden) nur die nahe Staffel.</summary>
    private void KulisseStufeNachfuehren()
    {
        if (_kulisse is null) return;
        var alle = Fenster.AktuelleBodenqualitaet != Bodenqualitaet.Niedrig;
        foreach (var teil in _kulisse.FindChildren("Staffel_*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            teil.Visible = alle || teil.Name == "Staffel_Nah";
        _kulisseStoff?.SetShaderParameter("allein", alle ? 0f : 1f); // allein: weichere Silhouette der nahen Staffel (kulisse.gdshader)
    }

    /// <summary>Jedes Bild: Himmelsfarbe, Dunst des Wetters und Nacht in den Shader (nur bei Änderung), dann das Schild der Gipfel.</summary>
    private void KulisseBild()
    {
        if (_kulisse is null || _kulisseStoff is null) return;
        var himmel = _dunstRing?.GetShaderParameter("farbe").AsColor() ?? _umgebung.Environment.BackgroundColor;
        var (nebelLand, _) = NebelStaerkeJetzt();
        var dunst = Mathf.Clamp(Mathf.Max(_wetter.Nebel, Mathf.Max(0.6f * _wetter.Regen, 0.5f * nebelLand)), 0, 1);
        var nacht = NebelDunkel();
        var stand = (himmel, dunst, nacht);
        if (!himmel.IsEqualApprox(_kulisseStand.Himmel) || Mathf.Abs(dunst - _kulisseStand.Dunst) > 0.005f || Mathf.Abs(nacht - _kulisseStand.Nacht) > 0.005f)
        {
            _kulisseStand = stand;
            _kulisseStoff.SetShaderParameter("himmel", himmel); // wie der Ring (source_color)
            _kulisseStoff.SetShaderParameter("dunst", dunst);
            _kulisseStoff.SetShaderParameter("nacht", nacht);
        }
        BergSchildZeigen();
    }

    /// <summary>Der Berg nahe der Maus (Gipfel vor Gebieten) – null: keiner, über einer Tafel oder in einem Raum.</summary>
    private Kulissenberg? BergBeiMaus()
    {
        if (_kulissenberge.Count == 0 || _raum is not null || UnterTafel || GetViewport().GuiGetHoveredControl() is not null) return null;
        var maus = GetViewport().GetMousePosition();
        if (!GetViewport().GetVisibleRect().HasPoint(maus)) return null;
        var kamera = StadtKamera;
        Kulissenberg? beste = null;
        var bester = float.MaxValue;
        foreach (var berg in _kulissenberge)
        {
            if (kamera.IsPositionBehind(berg.Ort)) continue;
            if (!berg.Gipfel && beste is { Gipfel: true }) continue;
            var abstand = kamera.UnprojectPosition(berg.Ort).DistanceTo(maus);
            if (abstand > (berg.Gipfel ? GipfelFang : GebietFang)) continue;
            if ((berg.Gipfel && beste is not { Gipfel: true }) || abstand < bester) (beste, bester) = (berg, abstand);
        }
        return beste;
    }

    private void BergSchildZeigen()
    {
        var berg = BergBeiMaus();
        if (berg?.Name != _bergGezeigt)
        {
            _bergGezeigt = berg?.Name;
            if (berg is { } b)
            {
                BergSchildAnlegen();
                _bergSchildName!.Text = b.Name;
                var teile = new List<string>();
                if (b.Gipfel && b.Gebiet is { } gebiet) teile.Add(gebiet);
                if (b.HoeheNN is { } h) teile.Add(b.Gipfel ? $"{h} m über dem Meer" : $"bis {h} m über dem Meer");
                _bergSchildHinweis!.Text = string.Join(" · ", teile);
                _bergSchildHinweis.Visible = teile.Count > 0;
                _bergSchild!.ResetSize();
            }
            if (_bergSchild is not null) _bergSchild.Visible = berg is not null;
        }
        if (_bergSchild is { Visible: true })
        {
            var fenster = GetViewport().GetVisibleRect().Size;
            var ort = GetViewport().GetMousePosition() + new Vector2(18, 22);
            _bergSchild.Position = new Vector2(Mathf.Min(ort.X, fenster.X - _bergSchild.Size.X - 8), Mathf.Min(ort.Y, fenster.Y - _bergSchild.Size.Y - 8));
        }
    }

    /// <summary>Das Schild an der Maus wie in den Räumen (Stadtansicht.Innenkamera.cs): dunkles Holz mit Messingrand, Name in Gold, darunter klein Gebiet und Höhe.</summary>
    private void BergSchildAnlegen()
    {
        if (_bergSchild is not null) return;
        var ebene = new CanvasLayer { Name = "Bergschild", Layer = 5 };
        AddChild(ebene);
        _bergSchild = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _bergSchild.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.10f, 0.08f, 0.95f),
            BorderColor = new Color(0.62f, 0.50f, 0.32f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 6, ContentMarginBottom = 6,
        });
        var spalte = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        spalte.AddThemeConstantOverride("separation", 0);
        _bergSchild.AddChild(spalte);
        _bergSchildName = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        _bergSchildName.AddThemeFontSizeOverride("font_size", 17);
        _bergSchildName.AddThemeColorOverride("font_color", new Color(0.95f, 0.82f, 0.45f));
        spalte.AddChild(_bergSchildName);
        _bergSchildHinweis = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        _bergSchildHinweis.AddThemeFontSizeOverride("font_size", 12);
        _bergSchildHinweis.AddThemeColorOverride("font_color", new Color(0.78f, 0.74f, 0.66f));
        spalte.AddChild(_bergSchildHinweis);
        ebene.AddChild(_bergSchild);
    }
}
