using Godot;

namespace Reichsgilde;

/// <summary>
/// Lichtflecken (PR #218, Teil der <c>partial class Stadtansicht</c>): Jede brennende Fackel, Laterne und jeder Feuerkorb leuchtet immer, auch wo
/// kein echtes Licht mehr steht. Echte <see cref="OmniLight3D"/> gibt es nur für die nächsten Lichter vor der Kamera (Stadtansicht.Nachtlichter.cs,
/// Anzahl nach der Grafikstufe); alle übrigen bekommen hier einen <b>Heiligenschein</b> um die Flamme und einen <b>Lichtfleck auf dem Boden</b>
/// (<c>lichtfleck.gdshader</c>, je ein Vielfach-Zeichenaufruf, ohne Beleuchtung – also ohne Dauerkosten je Licht). Seit Phase 2 „Feuer und Flammen“ ist
/// der Fleck ein Ersatzlicht: eine Kugel um die Flamme, in der der Shader Boden und Mauern nach dem Abstand aufhellt (aus der Tiefe der Szene), statt
/// einer flachen Scheibe, die an Stufen und Sockeln hart abbrach. Wo ein echtes Licht steht, blendet er ganz aus (kein doppeltes Licht), der
/// Heiligenschein wird schwächer. Beide flackern im Takt der Flamme. Weiter herausgezoomt wachsen beide mit, damit sie im Bild gleich groß bleiben.
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
            var netz = new QuadMesh { Size = Vector2.One, Material = stoff }; // beide zur Kamera gedreht (im Shader)
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
        var n = 0;
        foreach (var q in _lichtquellen)
        {
            if (n >= FleckeMaximal) break;
            if (!IsInstanceValid(q.Knoten) || !q.Knoten.IsVisibleInTree()) continue;
            var ort = q.Knoten.GlobalTransform * q.Versatz;
            if (new Vector2(ort.X - fokus.X, ort.Z - fokus.Z).Length() > reichweite) continue;
            var (farbe, _, licht) = PunktLicht(q.Art);
            var tag = q.Art == Lichtart.EwigesLicht ? Mathf.Max(_lampenAn, 0.22f) : _lampenAn;
            var flackern = Flackern(q); // im Takt der Flamme, wie das echte Licht (Stadtansicht.Nachtlichter.cs)
            var stand = 0f; // wie voll ein echtes Licht an dieser Stelle schon steht
            foreach (var p in _lichtPool)
                if (p.Quelle == q) { stand = p.Stand; break; }

            // Ersatzlicht: eine Kugel um die Flamme (Durchmesser), der Shader hellt alles darin nach dem Abstand auf; wo ein echtes Licht steht, ganz aus
            var durchmesser = licht * 0.8f * Mathf.Pow(mass, 0.6f);
            _fleckBodenViele!.SetInstanceTransform(n, new Transform3D(Basis.FromScale(Vector3.One * durchmesser), ort));
            _fleckBodenViele.SetInstanceColor(n, new Color(farbe.R, farbe.G, farbe.B, 1.0f * tag * flackern * (1f - stand)));
            // Heiligenschein: die echte Flamme leuchtet selbst (Bloom), der Hof ist nur noch ein Hauch, unter einem echten Licht schwächer
            var schein = (q.Art == Lichtart.Feuerkorb ? 1.0f : q.Art == Lichtart.EwigesLicht ? 0.4f : 0.6f) * Mathf.Pow(mass, 0.8f);
            _fleckScheinViele!.SetInstanceTransform(n, new Transform3D(Basis.FromScale(Vector3.One * schein), ort));
            _fleckScheinViele.SetInstanceColor(n, new Color(farbe.R, farbe.G, farbe.B, (q.Flamme is null ? 0.45f : 0.25f) * tag * flackern * (1f - 0.5f * stand)));
            n++;
        }
        _fleckBodenViele!.VisibleInstanceCount = n;
        _fleckScheinViele!.VisibleInstanceCount = n;
        _fleckBoden.Visible = _fleckSchein.Visible = n > 0;
    }
}
