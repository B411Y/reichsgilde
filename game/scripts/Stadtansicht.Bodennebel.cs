using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Bodennebel-Schwaden – ein Teil der <c>partial class Stadtansicht</c>. Morgens liegen flache, weiche Nebelbänke über Wiesen, in Senken und
/// am Wasser; sie ziehen langsam mit dem Wind und reißen bis zum Vormittag auf. Billig: ein Netz (bei Hoch zwei übereinander) knapp über dem
/// Boden mit dem Shader <c>bodennebel.gdshader</c>, kein Volumen. Wie gern Nebel an einer Stelle liegt, steht als Eckenwert im Netz
/// (<see cref="NebelNeigung"/>): Senken gegenüber der Umgebung, Nähe zum Wasser, in der Stadt weniger.
/// Wann: Die Stärke folgt dem Bezugstag der Lichtstimmung (<see cref="_bezugStunde"/>, Stadtansicht.Licht.cs) und damit der Jahreszeit – dicht bis
/// kurz nach dem Hellwerden, bis zum Vormittag verflogen, am Abend ein Hauch. Bei Wetter „Nebel“ bleibt er den ganzen Tag. Aus bei Morgendunst „Aus“.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Abstand der Netzpunkte (Meter) und Höhe der beiden Schichten über dem Boden.</summary>
    private const float NebelZelle = 6f, NebelHoeheUnten = 0.35f, NebelHoeheOben = 1.6f;

    private static Shader? _nebelShader;
    private readonly List<(MeshInstance3D Netz, ShaderMaterial Stoff)> _nebelSchichten = new();
    private Node3D? _nebelKnoten;
    private (Kartengrenzen? Grenzen, int Mauern, bool Hoch) _nebelFuer;
    private float _nebelStaerke = -1;
    private Color _nebelFarbe = new(-1, 0, 0);

    /// <summary>Wie stark der Nebel um diese Stunde des Bezugstags liegt (0–1): voll bis kurz nach dem Hellwerden, bis 10 Uhr verflogen, abends ein Hauch.</summary>
    private static float NebelUm(float bezug) =>
        Mathf.Max(1f - Mathf.SmoothStep(7.5f, 10f, bezug), 0.3f * Mathf.SmoothStep(18.2f, 19.6f, bezug) * (1f - Mathf.SmoothStep(20.5f, 21.2f, bezug)));

    /// <summary>Jedes Bild (aus <see cref="DunstNachUhrzeit"/>): Schwaden anlegen, wenn die Karte steht, Stärke und Farbe nachführen.</summary>
    private void BodennebelNachfuehren()
    {
        var stufe = Fenster.AktuellerDunst;
        var staerke = OS.GetEnvironment("RG_OHNE_NEBEL") == "1" || stufe == Morgendunst.Aus || _raum is not null || _entwurf is not null || _grenzen is null
            ? 0f
            : Mathf.Max(NebelUm(_bezugStunde), 0.85f * _wetter.Nebel);
        if (staerke < 0.01f)
        {
            if (_nebelKnoten is { Visible: true }) _nebelKnoten.Visible = false;
            _nebelStaerke = -1;
            return;
        }
        var hoch = stufe == Morgendunst.Hoch;
        var schluessel = (_grenzen, _mauerFlaechen.Count, hoch);
        if (_nebelKnoten is null || schluessel != _nebelFuer) NebelBauen(hoch);
        _nebelFuer = schluessel;
        _nebelKnoten!.Visible = true;
        // der Nebel hat die Farbe des Horizonts (Himmel dieser Stunde), etwas kühler und aufgehellt: derselbe Ton wie der Dunstring am Rand
        var himmel = _umgebung.Environment.BackgroundColor;
        var hell = himmel.R * 0.3f + himmel.G * 0.55f + himmel.B * 0.15f;
        var farbe = himmel.Lerp(new Color(hell * 0.94f, hell, hell * 1.07f), 0.6f).Lerp(Colors.White * Mathf.Min(1f, hell + 0.1f), 0.15f);
        if (Mathf.Abs(staerke - _nebelStaerke) < 0.004f && farbe.IsEqualApprox(_nebelFarbe)) return;
        _nebelStaerke = staerke;
        _nebelFarbe = farbe;
        foreach (var (_, stoff) in _nebelSchichten)
        {
            stoff.SetShaderParameter("staerke", staerke);
            stoff.SetShaderParameter("farbe", farbe);
        }
    }

    private void NebelBauen(bool hoch)
    {
        if (_nebelKnoten is not null) KnotenEntfernen(_nebelKnoten);
        _nebelSchichten.Clear();
        _nebelKnoten = new Node3D { Name = "Bodennebel" };
        AddChild(_nebelKnoten);
        var rahmen = _grenzen!.Spielfeld.Vergroessert(40);
        _nebelShader ??= GD.Load<Shader>("res://assets/shader/bodennebel.gdshader");
        var (nx, nz) = (Math.Max(1, (int)Math.Ceiling(rahmen.Breite / NebelZelle)), Math.Max(1, (int)Math.Ceiling(rahmen.Tiefe / NebelZelle)));
        var (dx, dz) = (rahmen.Breite / nx, rahmen.Tiefe / nz);
        var mauern = _mauerFlaechen.ToArray();
        var wasser = GewaesserForm is { IstLeer: false } w ? w : null;
        // die Neigung je Punkt einmal rechnen, die Höhen der Schichten daraus
        var neigung = new float[(nx + 1) * (nz + 1)];
        var boden = new float[neigung.Length];
        for (var j = 0; j <= nz; j++)
            for (var i = 0; i <= nx; i++)
            {
                var (x, z) = ((float)(rahmen.X0 + i * dx), (float)(rahmen.Z0 + j * dz));
                neigung[j * (nx + 1) + i] = NebelNeigung(x, z, mauern, wasser, out boden[j * (nx + 1) + i]);
            }
        var schichten = hoch ? new[] { (NebelHoeheUnten, 0f), (NebelHoeheOben, 1f) } : new[] { (NebelHoeheUnten + 0.4f, 0f) };
        foreach (var (hoehe, versatz) in schichten)
        {
            var ecken = new List<Vector3>();
            var farben = new List<Color>();
            var indizes = new List<int>();
            for (var j = 0; j <= nz; j++)
                for (var i = 0; i <= nx; i++)
                {
                    var k = j * (nx + 1) + i;
                    ecken.Add(new Vector3((float)(rahmen.X0 + i * dx), boden[k] + hoehe, (float)(rahmen.Z0 + j * dz)));
                    farben.Add(new Color(neigung[k], 0, 0));
                }
            for (var j = 0; j < nz; j++)
                for (var i = 0; i < nx; i++)
                {
                    var a = j * (nx + 1) + i;
                    indizes.AddRange(new[] { a, a + 1, a + nx + 2, a, a + nx + 2, a + nx + 1 });
                }
            var felder = new Godot.Collections.Array();
            felder.Resize((int)Mesh.ArrayType.Max);
            felder[(int)Mesh.ArrayType.Vertex] = ecken.ToArray();
            felder[(int)Mesh.ArrayType.Color] = farben.ToArray();
            felder[(int)Mesh.ArrayType.Index] = indizes.ToArray();
            var netz = new ArrayMesh();
            netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, felder);
            var stoff = new ShaderMaterial { Shader = _nebelShader, ResourceName = "Bodennebel" };
            stoff.SetShaderParameter("versatz", versatz);
            var instanz = new MeshInstance3D { Name = "Nebelschicht", Mesh = netz, MaterialOverride = stoff, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _nebelKnoten.AddChild(instanz);
            _nebelSchichten.Add((instanz, stoff));
        }
        _nebelStaerke = -1;
    }

    /// <summary>
    /// Wie gern Nebel an dieser Stelle liegt (0–1) und die Höhe des Bodens (Wasser: des Spiegels): Senken gegenüber dem Ring 20 m ringsum, am Wasser
    /// (bis 20 m vom Ufer), auf der Wiese ein Grundwert; in der Stadt innerhalb der Mauer nur ein Viertel davon.
    /// </summary>
    private float NebelNeigung(float x, float z, Vector2[][] mauern, Wasserform? wasser, out float hoehe)
    {
        hoehe = BodenHoehe(x, z);
        var mittel = 0f;
        for (var k = 0; k < 8; k++)
        {
            var w = k * Mathf.Pi / 4f;
            mittel += BodenHoehe(x + 20f * Mathf.Cos(w), z + 20f * Mathf.Sin(w));
        }
        var senke = Mathf.Clamp((mittel / 8f - hoehe) / 1.2f, 0f, 1f);
        var neigung = Mathf.Max(0.3f, senke);
        if (wasser is not null)
        {
            if (wasser.Spiegel(x, z) is { } spiegel) hoehe = Mathf.Max(hoehe, (float)spiegel);
            var ufer = (float)wasser.Uferabstand(x, z, 20);
            if (ufer < 20) neigung = Mathf.Max(neigung, 1f - ufer / 20f * 0.6f);
        }
        var p = new Vector2(x, z);
        foreach (var m in mauern)
            if (Geometry2D.IsPointInPolygon(p, m)) { neigung *= 0.25f; break; }
        return neigung;
    }
}
