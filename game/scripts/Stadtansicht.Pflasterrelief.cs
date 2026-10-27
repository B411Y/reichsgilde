using Godot;

namespace Reichsgilde;

/// <summary>
/// Pflaster mit Tiefe (PR #218, Teil der <c>partial class Stadtansicht</c>): In der Bodenqualität Hoch (<see cref="Fenster.AktuelleBodenqualitaet"/>) rechnet
/// <c>boden.gdshaderinc</c> für das Pflaster eine Parallax-Okklusion – Steine erhaben, Fugen tief –, damit Kopfsteinpflaster und Gassen nicht wie eine
/// glatte Schicht wirken. Der Shader braucht zwei globale Werte: die Tiefe des Reliefs (<c>boden_pom</c>, Meter, 0 = aus) und die Blickrichtung
/// (<c>boden_blick</c>, vom Boden zur Kamera; die Kamera hat keine Perspektive, die Richtung gilt überall). Niedrig und Mittel: 0, die Steine bleiben
/// bei der Normalenabbildung. Die Startoption <c>--pflasterrelief=0</c> schaltet es für diesen Start aus (Vergleich, Messung).
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Wie tief die Fugen unter den Steinkuppen liegen (Meter) – ein Kopfstein ragt etwa so weit heraus.</summary>
    private const float PflasterTiefe = 0.09f;

    private bool _pflasterReliefAus;
    private float _pomGestellt = -1;
    private Vector3 _blickGestellt = new(9, 9, 9);

    /// <summary>Startoption <c>--pflasterrelief=0</c>.</summary>
    public void PflasterReliefVorgeben(bool an) => _pflasterReliefAus = !an;

    /// <summary>Jedes Bild: Tiefe und Blickrichtung an die Boden-Shader geben, nur bei Änderung.</summary>
    private void PflasterReliefStellen()
    {
        var tiefe = !_pflasterReliefAus && Fenster.AktuelleBodenqualitaet == Bodenqualitaet.Hoch && _raum is null && _innen is null ? PflasterTiefe : 0f;
        if (!Mathf.IsEqualApprox(tiefe, _pomGestellt))
        {
            _pomGestellt = tiefe;
            RenderingServer.GlobalShaderParameterSet("boden_pom", tiefe);
        }
        if (tiefe <= 0) return;
        var blick = _kamera.GlobalTransform.Basis.Z.Normalized();
        if ((blick - _blickGestellt).LengthSquared() < 1e-6f) return;
        _blickGestellt = blick;
        RenderingServer.GlobalShaderParameterSet("boden_blick", blick);
    }
}
