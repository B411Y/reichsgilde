using Godot;

namespace Reichsgilde;

/// <summary>
/// Lichtflecken (PR #218, Teil der <c>partial class Stadtansicht</c>): Jede brennende Fackel, Laterne und jeder Feuerkorb leuchtet immer, auch wo
/// kein echtes Licht mehr steht. Echte <see cref="OmniLight3D"/> gibt es nur für die nächsten Lichter vor der Kamera (Stadtansicht.Nachtlichter.cs,
/// Anzahl nach der Grafikstufe); alle übrigen bekommen hier einen <b>Heiligenschein</b> um die Flamme und einen <b>Lichtfleck auf dem Boden</b>
/// (<c>lichtfleck.gdshader</c>, je ein Vielfach-Zeichenaufruf, additiv, ohne Beleuchtung – also ohne Dauerkosten je Licht). Wo ein echtes Licht steht,
/// blendet der Bodenfleck weich aus (kein doppeltes Licht), der Heiligenschein bleibt. Weiter herausgezoomt wachsen beide mit, damit sie im Bild
/// gleich groß bleiben.
/// </summary>
public partial class Stadtansicht
{
    private const int FleckeMaximal = 600;

    private MultiMeshInstance3D? _fleckBoden, _fleckSchein;
    private MultiMesh? _fleckBodenViele, _fleckScheinViele;

    private void FleckenEinrichten()
    {
        var shader = GD.Load<Shader>("res://assets/shader/lichtfleck.gdshader");
        MultiMeshInstance3D Bauen(string name, bool boden, out MultiMesh viele)
        {
            var stoff = new ShaderMaterial { Shader = shader, RenderPriority = 2 };
            stoff.SetShaderParameter("boden", boden);
            var netz = new QuadMesh { Size = Vector2.One, Material = stoff };
            if (boden) netz.Orientation = PlaneMesh.OrientationEnum.Y; // liegt waagerecht
            viele = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = netz, InstanceCount = FleckeMaximal, VisibleInstanceCount = 0 };
            var knoten = new MultiMeshInstance3D
            {
                Name = name, Multimesh = viele, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                ExtraCullMargin = 16384, Visible = false,
            };
            AddChild(knoten);
            return knoten;
        }
        _fleckBoden = Bauen("Lichtflecken", true, out _fleckBodenViele);
        _fleckSchein = Bauen("Lichtschein", false, out _fleckScheinViele);
    }

    /// <summary>Jedes Bild: die Flecken aller brennenden Lichter im Bild setzen (höchstens <see cref="FleckeMaximal"/>, die nächsten zuerst).</summary>
    private void LichtfleckenStellen()
    {
        var an = _lampenAn > 0.02f && _raum is null && _innen is null && OS.GetEnvironment("RG_OHNEFLECKEN") == "";
        if (_fleckBoden is null || _fleckSchein is null) return;
        if (!an)
        {
            if (_fleckBoden.Visible) _fleckBoden.Visible = _fleckSchein.Visible = false;
            return;
        }
        var fokus = BlickpunktAmBoden();
        var zoom = _kamera.Size;
        var reichweite = Mathf.Max(30f, zoom * 1.6f);
        var mass = Mathf.Max(1f, zoom / 45f); // herausgezoomt wachsen die Flecken mit
        var zeit = Time.GetTicksMsec() / 1000.0;
        var n = 0;
        foreach (var q in _lichtquellen)
        {
            if (n >= FleckeMaximal) break;
            if (!IsInstanceValid(q.Knoten) || !q.Knoten.IsVisibleInTree()) continue;
            var ort = q.Knoten.GlobalTransform * q.Versatz;
            if (new Vector2(ort.X - fokus.X, ort.Z - fokus.Z).Length() > reichweite) continue;
            var (farbe, _, licht) = PunktLicht(q.Art);
            var tag = q.Art == Lichtart.EwigesLicht ? Mathf.Max(_lampenAn, 0.22f) : _lampenAn;
            var t = zeit * (q.Art == Lichtart.Feuerkorb || q.Art == Lichtart.Fackel ? 1.6 : 1.0) + q.Phase * 7;
            var flackern = (float)(1 + 0.12 * (0.55 * Math.Sin(t * 9.1) + 0.3 * Math.Sin(t * 23.7 + 1.3) + 0.15 * Math.Sin(t * 41.0 + 2.1)));
            var stand = 0f; // wie voll ein echtes Licht an dieser Stelle schon steht
            foreach (var p in _lichtPool)
                if (p.Quelle == q) { stand = p.Stand; break; }

            var grund = new Vector3(ort.X, FussHoehe(ort.X, ort.Z) + 0.07f, ort.Z);
            var durchmesser = licht * 0.75f * Mathf.Pow(mass, 0.6f);
            var boden = new Transform3D(Basis.FromScale(new Vector3(durchmesser, 1, durchmesser)), grund);
            _fleckBodenViele!.SetInstanceTransform(n, boden);
            _fleckBodenViele.SetInstanceColor(n, new Color(farbe.R, farbe.G, farbe.B, 0.38f * tag * flackern * (1 - 0.85f * stand)));
            var schein = (q.Art == Lichtart.Feuerkorb ? 1.6f : q.Art == Lichtart.EwigesLicht ? 0.7f : 1.0f) * Mathf.Pow(mass, 0.8f);
            _fleckScheinViele!.SetInstanceTransform(n, new Transform3D(Basis.FromScale(Vector3.One * schein), ort));
            _fleckScheinViele.SetInstanceColor(n, new Color(farbe.R, farbe.G, farbe.B, 0.9f * tag * flackern));
            n++;
        }
        _fleckBodenViele!.VisibleInstanceCount = n;
        _fleckScheinViele!.VisibleInstanceCount = n;
        _fleckBoden.Visible = _fleckSchein.Visible = n > 0;
    }
}
