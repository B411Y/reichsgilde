using Godot;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Was die Umgebungsklänge (<see cref="KlangUmgebung"/>, Ton Schritt 4) von der Stadtansicht lesen – ein Teil der <c>partial class Stadtansicht</c>.
/// Nur Getter (die Darstellung bleibt unberührt), dazu ein Setzer des Zooms für die Hörprobe <c>--umgebungfahrt=1</c>.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Kameragröße (sichtbare Bildhöhe in Metern, 25–140), auch mit freier Kamera (deren Größe folgt ihr ungefähr).</summary>
    public float KlangZoom => Mathf.Clamp(_kamera.Size, KameraMinZoom, KameraMaxZoom);

    /// <summary>Der wirkliche Höchstzoom dieser Karte und dieses Seitenverhältnisses (<see cref="KameraGroessterZoom"/>).</summary>
    public float KlangZoomGroesst => KameraGroessterZoom();

    /// <summary>Die Bezugsstunde des Lichts (5 = Dämmerung beginnt … 22 = Nacht, je Jahreszeit gestreckt, Stadtansicht.Licht.cs).</summary>
    public float KlangBezugStunde => _bezugStunde;

    /// <summary>Die gezeigte Uhr in Minuten mit Bruchteil (berücksichtigt <c>--lichtuhr</c>); ohne Partie 12 Uhr.</summary>
    public float KlangUhrMinute => _lichtUhrVorgabe is { } l ? l * 60f
        : _simulation is { } s ? s.Zeit.UhrzeitInMinuten + (float)s.Takt.AngebrocheneMinute : 12 * 60f;

    /// <summary>Die gezeigte Jahreszeit (auch <c>--jahreszeit</c>, <c>--winter</c>).</summary>
    public Jahreszeit KlangJahreszeit => GezeigteJahreszeit(_simulation?.Zeit.Jahreszeit ?? Jahreszeit.Fruehling);

    /// <summary>
    /// Draußen, im Dom (ein Diorama, die Kamera bleibt oben: „innen leicht“) oder in einem Raum. Jede andere Innenansicht zählt als Raum, auch in
    /// den Bildern des Übergangs, bevor der Raum gesetzt ist.
    /// </summary>
    public KlangUmgebungModell.Innenart KlangInnenart =>
        _raum is not null ? KlangUmgebungModell.Innenart.Raum
        : _innen is null ? KlangUmgebungModell.Innenart.Draussen
        : _innen.Kennung == "Dom" ? KlangUmgebungModell.Innenart.InnenLeicht
        : KlangUmgebungModell.Innenart.Raum;

    /// <summary>Nur für die Hörprobe: Zoom fest vorgeben (wie das Mausrad, in den Grenzen der Karte).</summary>
    public void KlangZoomVorgeben(float groesse) => _kamera.Size = Mathf.Clamp(groesse, KameraMinZoom, KameraGroessterZoom());
}
