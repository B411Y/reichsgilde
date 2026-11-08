using System.Collections.Generic;
using System.Linq;
using Godot;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Der Marktbrunnen (Teil der <c>partial class Stadtansicht</c>): Das Modell des Marktplatzes (<c>marktplatz.glb</c>, gebaut von <c>tools/modelle/brunnen.py</c>) hat den Brunnen
/// in fünf Netzen: <c>Brunnen</c> und <c>Brunnen_Trog</c> (Stein), <c>Brunnen_Wasser</c> (Becken und Trog), <c>Brunnen_Strahl</c> (vier Strahlen und der Überlauf) und
/// <c>Brunnen_Eis</c> (Winter). Hier bekommen Wasser und Strahl ihre Shader (<c>brunnenwasser.gdshader</c>, <c>brunnenstrahl.gdshader</c>), und die Netze folgen der Jahreszeit
/// und dem Zoom:
/// <list type="bullet">
/// <item><b>Sommer:</b> Wasser und Strahl sichtbar, das Eis nicht. <b>Winter:</b> Eis (mit Eiszapfen und Schnee), kein Wasser, kein Strahl.</item>
/// <item><b>Zoom über 60 m:</b> der Strahl ist aus (man sähe ihn nicht) und das Wasser ohne Wellenringe.</item>
/// <item><b>Grafikstufe:</b> beide Shader folgen der Einstellung „Boden“ (wie die Pfützen): Niedrig ruhig und glatt, Mittel mit Himmelsspiegelung, Hoch mit laufenden Streifen und Ringen.</item>
/// </list>
/// Wasser und Strahl sind je <b>ein</b> Stoff mit eigenem Shader und werden nicht zusammengelegt (die Schattenproxys fassen nur Netze mit mehreren Flächen zusammen; der Strahl und das
/// Wasser haben je eine). Strahl und Wasser werfen keinen Schatten. Der ganze Brunnen kostet etwa 14 Zeichenaufrufe (vier Stoffe Stein, Trog, Wasser, Strahl, je zwei).
/// </summary>
public partial class Stadtansicht
{
    private const string BrunnenWasserPfad = "res://assets/shader/brunnenwasser.gdshader";
    private const string BrunnenStrahlPfad = "res://assets/shader/brunnenstrahl.gdshader";
    /// <summary>Ab diesem Zoom (Meter, Bildhöhe) ist der Strahl aus und das Wasser ohne Ringe.</summary>
    private const float BrunnenFernAbZoom = 60f;

    private ShaderMaterial? _brunnenWasserStoff, _brunnenStrahlStoff;
    private readonly List<(MeshInstance3D Wasser, MeshInstance3D? Strahl, MeshInstance3D? Eis)> _brunnen = new();
    private bool? _brunnenNah;

    private ShaderMaterial? BrunnenStoff(ref ShaderMaterial? zeiger, string pfad, string name)
    {
        if (zeiger is not null) return zeiger;
        if (!ResourceLoader.Exists(pfad)) return null;
        zeiger = new ShaderMaterial { Shader = GD.Load<Shader>(pfad), ResourceName = name };
        zeiger.SetShaderParameter("stufe", (int)Fenster.AktuelleBodenqualitaet);
        return zeiger;
    }

    /// <summary>Die Stufe der Brunnen-Shader folgt der Einstellung „Boden“ (aus <see cref="BodenqualitaetNachfuehren"/>).</summary>
    private void BrunnenStufeSetzen()
    {
        _brunnenWasserStoff?.SetShaderParameter("stufe", (int)Fenster.AktuelleBodenqualitaet);
        _brunnenStrahlStoff?.SetShaderParameter("stufe", (int)Fenster.AktuelleBodenqualitaet);
    }

    /// <summary>Belegt die Netze „Brunnen_Wasser“ und „Brunnen_Strahl“ des frisch eingesetzten Marktplatzes mit den Shadern und merkt sich die Netze für Jahreszeit und Zoom.</summary>
    private void BrunnenEinrichten(Node3D wurzel)
    {
        var teile = wurzel.GetChildren().OfType<MeshInstance3D>().ToDictionary(t => t.Name.ToString(), t => t);
        if (!teile.TryGetValue("Brunnen_Wasser", out var wasser)) return;
        var wStoff = BrunnenStoff(ref _brunnenWasserStoff, BrunnenWasserPfad, "Brunnenwasser");
        for (var i = 0; i < wasser.Mesh.GetSurfaceCount() && wStoff is not null; i++) wasser.SetSurfaceOverrideMaterial(i, wStoff);
        wasser.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; // das Wasser liegt im Becken, sein Schatten wäre nur ein unnützer Aufruf in der Schattenkarte
        teile.TryGetValue("Brunnen_Strahl", out var strahl);
        if (strahl is not null)
        {
            var sStoff = BrunnenStoff(ref _brunnenStrahlStoff, BrunnenStrahlPfad, "Brunnenstrahl");
            for (var i = 0; i < strahl.Mesh.GetSurfaceCount() && sStoff is not null; i++) strahl.SetSurfaceOverrideMaterial(i, sStoff);
            strahl.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        teile.TryGetValue("Brunnen_Eis", out var eis);
        _brunnen.Add((wasser, strahl, eis));
        _brunnenNah = null;
        BrunnenSichtbar();
    }

    /// <summary>Jahreszeit und Zoom: Sommer Wasser und Strahl (nah), Winter das Eis; Strahl und Ringe nur nah.</summary>
    private void BrunnenSichtbar()
    {
        _brunnen.RemoveAll(b => !IsInstanceValid(b.Wasser));
        var winter = _jahreszeit == Jahreszeit.Winter;
        var nah = _kamera is not null && _kamera.Size <= BrunnenFernAbZoom && _raum is null;
        foreach (var (wasser, strahl, eis) in _brunnen)
        {
            wasser.Visible = !winter;
            if (strahl is not null) strahl.Visible = !winter && nah;
            if (eis is not null) eis.Visible = winter;
        }
        _brunnenWasserStoff?.SetShaderParameter("ringe", nah ? 1f : 0f);
        _brunnenNah = nah;
    }

    /// <summary>Jedes Bild (aus <see cref="FuellungNachZoom"/>): bei Wechsel zwischen nah und fern die Netze nachführen.</summary>
    private void BrunnenNachZoom()
    {
        if (_brunnen.Count == 0 || _kamera is null) return;
        if (_brunnenNah != (_kamera.Size <= BrunnenFernAbZoom && _raum is null)) BrunnenSichtbar();
    }
}
