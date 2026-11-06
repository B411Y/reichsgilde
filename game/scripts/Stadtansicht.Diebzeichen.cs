using Godot;

namespace Reichsgilde;

/// <summary>
/// Das <b>Zeichen über den eigenen Dieben</b> (Teil der <c>partial class Stadtansicht</c>; Wachen und Diebe, Nutzer 06.11.2026): Eigene Taschendiebe am Tag sowie eigene
/// Einbrecher und Bandenmitglieder bei Nacht tragen über dem Kopf eine kleine goldene Raute mit Dietrich (Holzschnitt: schwarzer Umriss, Schraffur).
/// <list type="bullet">
/// <item><b>Scharf</b>: Das Zeichen ist ein Control auf einer eigenen <see cref="CanvasLayer"/> (wie die Beschriftungen im Pixel-Look), also in voller Auflösung über der
/// Welt; Größe nach dem Zoom (mindestens 12 Bildpunkte, damit es bei 140 m noch zu lesen ist). Es hängt an der Kamera der Logik und liegt damit auch ohne Pixel-Look richtig.</item>
/// <item><b>Kein Licht</b>: nur eine Zeichnung, kein Lichtquelle; nachts etwas gedämpft (wie das Licht der Stadt), die Nacht bleibt dunkel.</item>
/// <item><b>Nur eigene</b>: fremde Täter und Gefasste (<see cref="Diebfigur.Eigen"/> falsch) bekommen keines und bleiben „Verdächtige Gestalt“.</item>
/// <item><b>Anklickbar</b>: <see cref="DiebBei"/> prüft zuerst die Zeichen; ein Klick wählt den Dieb aus.</item>
/// <item><b>Leistung</b>: Es werden nur die Gestalten in <see cref="_diebfiguren"/> geprüft (eine Handvoll), nie die Passanten; gezeichnet wird nur bei Änderung.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    private CanvasLayer? _zeichenEbene;
    private Control? _zeichenFlaeche;
    /// <summary>Die Zeichen dieses Bildes: Kennung der Gestalt, Mitte im Fenster, Radius in Bildpunkten.</summary>
    private readonly List<(string Kennung, Vector2 Mitte, float Radius)> _diebZeichen = new();
    private readonly List<(string Kennung, Vector2 Mitte, float Radius)> _diebZeichenGezeichnet = new();
    private Color _diebZeichenFarbe = new(0.86f, 0.68f, 0.18f);

    /// <summary>Jedes Bild, nach <c>Taschendiebe/NachtdiebeAktualisieren</c>: sammelt die Zeichen und zeichnet sie neu, wenn sie sich bewegt haben.</summary>
    private void DiebzeichenAktualisieren()
    {
        _diebZeichen.Clear();
        if (_raum is null && _innen is null && _diebfiguren.Count > 0 && Fenster.PassantenMenge > 0)
        {
            var groesse = GetViewport().GetVisibleRect().Size;
            var radius = Mathf.Clamp(0.4f * groesse.Y / _kamera.Size, 7f, 14f); // 0,8 m breit; mindestens 14 Bildpunkte
            foreach (var d in _diebfiguren.Values)
            {
                if (!d.Sichtbar || !d.Eigen || d.Fest is not null || !IsInstanceValid(d.Knoten)) continue;
                var ort = d.Knoten.GlobalPosition + new Vector3(0, 2.3f, 0);
                if (StadtKamera.IsPositionBehind(ort)) continue;
                var bild = StadtKamera.UnprojectPosition(ort).Round();
                if (bild.X < -radius || bild.Y < -radius || bild.X > groesse.X + radius || bild.Y > groesse.Y + radius) continue;
                _diebZeichen.Add((d.Kennung, bild, radius));
            }
        }
        if (_diebZeichen.Count == 0 && _diebZeichenGezeichnet.Count == 0) return;
        var gleich = _diebZeichen.Count == _diebZeichenGezeichnet.Count;
        for (var i = 0; gleich && i < _diebZeichen.Count; i++) gleich = _diebZeichen[i] == _diebZeichenGezeichnet[i];
        if (gleich) return;
        _diebZeichenGezeichnet.Clear();
        _diebZeichenGezeichnet.AddRange(_diebZeichen);
        if (_zeichenFlaeche is null)
        {
            _zeichenEbene = new CanvasLayer { Name = "Diebzeichen", Layer = 0 };
            _zeichenFlaeche = new Control { Name = "Flaeche", MouseFilter = Control.MouseFilterEnum.Ignore };
            _zeichenFlaeche.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _zeichenFlaeche.Draw += DiebzeichenZeichnen;
            _zeichenEbene.AddChild(_zeichenFlaeche);
            AddChild(_zeichenEbene);
        }
        _zeichenFlaeche.QueueRedraw();
    }

    /// <summary>Raute mit schwarzem Umriss, Schraffur unten rechts und einem kleinen Dietrich (Schaft mit Haken) in der Mitte.</summary>
    private void DiebzeichenZeichnen()
    {
        if (_zeichenFlaeche is not { } f) return;
        var dunkel = Mathf.Lerp(0.72f, 1f, Mathf.SmoothStep(PixelLichtBoden, 0.85f, PixelLichtskala())); // nachts gedämpft, nie leuchtend
        var gold = new Color(_diebZeichenFarbe.R * dunkel, _diebZeichenFarbe.G * dunkel, _diebZeichenFarbe.B * dunkel);
        var tinte = new Color(0.07f, 0.05f, 0.04f);
        foreach (var (_, m, r) in _diebZeichenGezeichnet)
        {
            var hoch = r * 1.25f;
            var raute = new[] { m + new Vector2(0, -hoch), m + new Vector2(r, 0), m + new Vector2(0, hoch), m + new Vector2(-r, 0) };
            f.DrawColoredPolygon(raute, gold);
            f.DrawLine(m + new Vector2(r * 0.15f, hoch * 0.55f), m + new Vector2(r * 0.6f, hoch * 0.1f), tinte with { A = 0.55f }, 1f); // Schraffur im Schatten
            f.DrawLine(m + new Vector2(r * 0.05f, hoch * 0.8f), m + new Vector2(r * 0.75f, hoch * 0.2f), tinte with { A = 0.55f }, 1f);
            f.DrawPolyline(new[] { raute[0], raute[1], raute[2], raute[3], raute[0] }, tinte, Mathf.Max(1.5f, r * 0.2f));
            var w = Mathf.Max(1.5f, r * 0.18f);
            f.DrawLine(m + new Vector2(-r * 0.12f, -hoch * 0.45f), m + new Vector2(-r * 0.12f, hoch * 0.4f), tinte, w); // Schaft des Dietrichs
            f.DrawLine(m + new Vector2(-r * 0.12f, -hoch * 0.45f), m + new Vector2(r * 0.3f, -hoch * 0.45f), tinte, w);  // Haken
            f.DrawLine(m + new Vector2(r * 0.3f, -hoch * 0.45f), m + new Vector2(r * 0.3f, -hoch * 0.15f), tinte, w);
        }
    }

    /// <summary>Welches Zeichen liegt an dieser Bildschirmstelle? Gibt die Kennung der Gestalt zurück (Klick wählt den Dieb).</summary>
    private string? DiebzeichenBei(Vector2 bildschirm)
    {
        foreach (var (kennung, m, r) in _diebZeichenGezeichnet)
            if (Mathf.Abs(bildschirm.X - m.X) <= r + 3 && Mathf.Abs(bildschirm.Y - m.Y) <= r * 1.25f + 3) return kennung;
        return null;
    }
}
