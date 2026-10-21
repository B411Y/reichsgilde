using Godot;

namespace Reichsgilde;

/// <summary>
/// Zwei Filter über dem fertigen Bild, die das Fenster (<see cref="Fenster"/>, Autoload) über alle Szenen hält:
/// <list type="bullet">
/// <item><b>Nachschärfen</b> (schaerfe.gdshader) auf einer Ebene ganz unten: Sie liest nur das 3D-Bild, alle Tafeln und Schriften
/// werden danach darübergezeichnet und bleiben, wie sie sind. Nur ohne FSR 2.2 – mit FSR schärft dessen eigener Schritt.</item>
/// <item><b>Farbfilter</b> für Farbsehschwäche (farbfilter.gdshader) auf der obersten Ebene: Er gilt für das ganze Bild mit der
/// Oberfläche, denn auch Wappen, Besitzerfarben und Warensymbole tragen Farbunterschiede.</item>
/// </list>
/// Beide sind ein Rechteck über den ganzen Bildschirm mit einem Shader, der den Bildschirm liest (<c>hint_screen_texture</c>):
/// Godot kopiert dafür das bis dahin gezeichnete Bild einmal – das kostet etwas, darum ist jeder Filter nur da, wenn er gebraucht wird.
/// </summary>
public partial class Bildfilter : Node
{
    private ColorRect _schaerfe = null!;
    private ShaderMaterial _schaerfeStoff = null!;
    private ColorRect _farbe = null!;
    private ShaderMaterial _farbStoff = null!;

    public override void _Ready()
    {
        (_schaerfe, _schaerfeStoff) = Ebene(-100, "res://assets/shader/schaerfe.gdshader");
        (_farbe, _farbStoff) = Ebene(128, "res://assets/shader/farbfilter.gdshader");
        Auffrischen();
    }

    private (ColorRect, ShaderMaterial) Ebene(int ebene, string shader)
    {
        var schicht = new CanvasLayer { Layer = ebene };
        AddChild(schicht);
        var stoff = new ShaderMaterial { Shader = GD.Load<Shader>(shader) };
        var flaeche = new ColorRect { Material = stoff, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        flaeche.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        schicht.AddChild(flaeche);
        return (flaeche, stoff);
    }

    /// <summary>Schaltet die Filter nach den Einstellungen an oder aus (bei jeder Änderung von Schärfe, FSR oder Farbfilter).</summary>
    public void Auffrischen()
    {
        if (!IsNodeReady()) return;
        _schaerfe.Visible = Fenster.Schaerfe > 0 && !Fenster.FsrAn;
        _schaerfeStoff.SetShaderParameter("staerke", Fenster.Schaerfe / 100f);
        _farbe.Visible = Fenster.AktuellerFarbfilter != Farbfilter.Aus;
        _farbStoff.SetShaderParameter("art", (int)Fenster.AktuellerFarbfilter);
    }
}
