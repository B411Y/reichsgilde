using Godot;

namespace Reichsgilde;

/// <summary>
/// Das <b>Zeichen über den eigenen Dieben</b> (Teil der <c>partial class Stadtansicht</c>; Wachen und Diebe, Nutzer 06.11.2026): Eigene Taschendiebe am Tag sowie eigene
/// Einbrecher und Bandenmitglieder bei Nacht tragen über dem Kopf eine kleiner grauer Kapuzenkopf mit Augenmaske (Holzschnitt: schwarzer Umriss, Schraffur).
/// <list type="bullet">
/// <item><b>Scharf</b>: Das Zeichen ist ein Control auf einer eigenen <see cref="CanvasLayer"/> (wie die Beschriftungen im Pixel-Look), also in voller Auflösung über der
/// Welt; Größe nach dem Zoom (mindestens 12 Bildpunkte, damit es bei 100 m noch zu lesen ist). Es hängt an der Kamera der Logik und liegt damit auch ohne Pixel-Look richtig.</item>
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

    /// <summary>
    /// Ein Kopf mit Kapuze als Silhouette (grau, zur Hälfte durchsichtig): Die Kapuze umschließt das Gesicht, darunter der Schulteransatz, im hellen Gesicht eine
    /// schmale dunkle Augenmaske mit zwei hellen Augenschlitzen. Die Teile (Kapuze, Schultern, Gesicht oben und unten, Maske) liegen nebeneinander, ohne sich zu
    /// überdecken – sonst wäre die Durchsicht an den Überdeckungen schwächer. Maße in Radien <c>r</c> um die Mitte (Breite 2 r, Höhe 2,5 r).
    /// </summary>
    private void DiebzeichenZeichnen()
    {
        if (_zeichenFlaeche is not { } f) return;
        var dunkel = Mathf.Lerp(0.72f, 1f, Mathf.SmoothStep(PixelLichtBoden, 0.85f, PixelLichtskala())); // nachts gedämpft, nie leuchtend
        Color Grau(float h) => new(h * dunkel, h * dunkel, h * dunkel, 0.5f); // halb durchsichtig
        var kapuze = Grau(0.42f);
        var rand = Grau(0.1f);
        var gesicht = Grau(0.86f);
        var maske = Grau(0.14f);
        foreach (var (_, m, r) in _diebZeichenGezeichnet)
        {
            Vector2[] P(params (float X, float Y)[] punkte) => punkte.Select(q => m + new Vector2(q.X * r, q.Y * r)).ToArray();
            var haube = P((0, -1.25f), (0.5f, -1.0f), (0.78f, -0.5f), (0.74f, 0.0f), (0.5f, 0.35f), (0.2f, 0.3f), (0.42f, -0.02f), (0.42f, -0.52f), (0, -0.78f),
                          (-0.42f, -0.52f), (-0.42f, -0.02f), (-0.2f, 0.3f), (-0.5f, 0.35f), (-0.74f, 0.0f), (-0.78f, -0.5f), (-0.5f, -1.0f));
            var schultern = P((-0.5f, 0.35f), (-0.2f, 0.3f), (0.2f, 0.3f), (0.5f, 0.35f), (1.0f, 0.6f), (1.0f, 1.25f), (-1.0f, 1.25f), (-1.0f, 0.6f));
            f.DrawColoredPolygon(haube, kapuze);
            f.DrawColoredPolygon(schultern, kapuze);
            f.DrawColoredPolygon(P((0, -0.78f), (0.42f, -0.52f), (0.42f, -0.42f), (-0.42f, -0.42f), (-0.42f, -0.52f)), gesicht);                        // Stirn
            f.DrawColoredPolygon(P((-0.42f, -0.12f), (0.42f, -0.12f), (0.42f, -0.02f), (0.2f, 0.3f), (-0.2f, 0.3f), (-0.42f, -0.02f)), gesicht);        // Wangen und Kinn
            f.DrawColoredPolygon(P((-0.42f, -0.42f), (0.42f, -0.42f), (0.42f, -0.31f), (-0.42f, -0.31f)), maske);                                       // Augenmaske oben
            f.DrawColoredPolygon(P((-0.42f, -0.22f), (0.42f, -0.22f), (0.42f, -0.12f), (-0.42f, -0.12f)), maske);                                       // und unten
            f.DrawColoredPolygon(P((-0.42f, -0.31f), (-0.28f, -0.31f), (-0.28f, -0.22f), (-0.42f, -0.22f)), maske);                                     // Maske zwischen den Schlitzen
            f.DrawColoredPolygon(P((-0.1f, -0.31f), (0.1f, -0.31f), (0.1f, -0.22f), (-0.1f, -0.22f)), maske);
            f.DrawColoredPolygon(P((0.28f, -0.31f), (0.42f, -0.31f), (0.42f, -0.22f), (0.28f, -0.22f)), maske);
            f.DrawColoredPolygon(P((-0.28f, -0.31f), (-0.1f, -0.31f), (-0.1f, -0.22f), (-0.28f, -0.22f)), gesicht);                                     // die beiden Augenschlitze
            f.DrawColoredPolygon(P((0.1f, -0.31f), (0.28f, -0.31f), (0.28f, -0.22f), (0.1f, -0.22f)), gesicht);
            var aussen = P((0, -1.25f), (0.5f, -1.0f), (0.78f, -0.5f), (0.74f, 0.0f), (0.5f, 0.35f), (1.0f, 0.6f), (1.0f, 1.25f), (-1.0f, 1.25f), (-1.0f, 0.6f), (-0.5f, 0.35f), (-0.74f, 0.0f), (-0.78f, -0.5f), (-0.5f, -1.0f));
            f.DrawPolyline(aussen.Append(aussen[0]).ToArray(), rand, Mathf.Max(1f, r * 0.12f));
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
