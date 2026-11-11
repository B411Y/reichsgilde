using Godot;

namespace Reichsgilde;

// Haltung Text: die Hand muss im Spielbild aufrecht stehen (Stulpe unten, Finger oben, wie im Blender-Vergleichsbild). Statt die Drehung zu raten, wird sie
// einmal beim Laden aus der Pose Text der Datei berechnet (Richtung Handgelenk zu Fingerspitze im Bild der Bühnenkamera, HandschuhModell.AufrechtDrehungGrad).
public partial class HandschuhZeiger
{
    /// <summary>Drehung der Hand um die Hochachse (Grad) in der Haltung Text; aus der Datei berechnet, bis dahin die Kameradrehung.</summary>
    private double _textDrehung = HandschuhModell.KameraDrehungGrad;

    /// <summary>Rechnet die Drehung für die Haltung Text aus der gerade ausgewerteten Pose Text (Handgelenk und Fingerspitze projiziert) und schreibt die Bildlage ins Log.</summary>
    private void TextAufrichten(int handgelenk)
    {
        if (_skelett is null || _knochen < 0 || handgelenk < 0) return;
        var welt = _skelett.GlobalTransform;
        var gelenk = welt * _skelett.GetBoneGlobalPose(handgelenk).Origin;
        var spitze = welt * _skelett.GetBoneGlobalPose(_knochen).Origin; // Zeigefinger, letztes Glied
        Vector2 Bild(Vector3 p, double grad) => _kamera.UnprojectPosition(BuehnenMitte + new Basis(Vector3.Up, Mathf.DegToRad((float)grad)) * (p - BuehnenMitte));
        (double X, double Y) Richtung(double grad) { var v = Bild(spitze, grad) - Bild(gelenk, grad); return (v.X, v.Y); }
        _textDrehung = HandschuhModell.AufrechtDrehungGrad(Richtung);
        var hoehe = Math.Max(_bild.Size.Y, 1);
        var anker = _ankerText is null ? float.NaN : Bild(welt * (_skelett.GetBoneGlobalPose(handgelenk) * _ankerText.Transform).Origin, _textDrehung).Y / hoehe * 100f;
        GD.Print($"[handschuh] Text aufrecht: Drehung {_textDrehung:0.0} Grad (Kamera {HandschuhModell.KameraDrehungGrad:0} Grad); im Bild (oben 0, unten 100): Stulpe x {Bild(gelenk, _textDrehung).X / hoehe * 100f:0.0} y {Bild(gelenk, _textDrehung).Y / hoehe * 100f:0.0}, Fingerspitze x {Bild(spitze, _textDrehung).X / hoehe * 100f:0.0} y {Bild(spitze, _textDrehung).Y / hoehe * 100f:0.0}, Anker_Text y {anker:0.0}; ohne Drehung Stulpe y {Bild(gelenk, 0).Y / hoehe * 100f:0.0}, Fingerspitze y {Bild(spitze, 0).Y / hoehe * 100f:0.0}");
    }
}
