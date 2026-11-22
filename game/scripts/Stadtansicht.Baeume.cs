using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die Originalbäume im Spiel (<c>natur/natur_original_baeume.glb</c>, Quaternius CC0; Entscheidung des Nutzers 22.11.2026 „Die Bäume kannst du voll ersetzen“):
/// Eiche, Buche, Apfel, Kirsche, Fichte, Kiefer, Hasel, Weißdorn, Holunder – Wind (<c>baum_wind.gdshader</c>), Farbe, Blüte und Schnee je Jahreszeit
/// (<see cref="Baumjahr"/>), im Winter die Laubbäume kahl, fallendes Laub im Herbst (Hoch und Ultra).
/// <para>Zwei Fassungen je Baum: das Originalmodell (nah) und eine ausgedünnte (<c>_Fern</c>: Rinde vereinfacht, vom Laub ein Teil der Karten, dafür größer).
/// Weil die Spielkamera immer gleich weit (150 m) vom Blickpunkt steht, taugen Godots Sichtbereiche nach Abstand hier nicht; die Stufe folgt dem Zoom
/// (<see cref="BaumNahBis"/>, mit Spielraum gegen Flackern am Rand). Die Perspektive der Ego-Ansicht zeigt immer die nahe Fassung.</para>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Bis zu diesem Zoom (sichtbare Höhe in Metern) zeigt das Spiel die Originalbäume, darüber die ausgedünnte Fassung (Zoomstufen 25 und 50 nah, 75 und 100 fern).</summary>
    internal const float BaumNahBis = 60f;
    private const float BaumSpielraum = 6f;

    /// <summary>
    /// Die Originalbäume für die Marker alter Modelle: „Kiefer3“ (Kit) → eine der fünf Nadelbäume (vier Fichten, eine Waldkiefer), „Laubbaum2“ → Eiche; null für alles andere
    /// (Totbaum bleibt beim Kit).
    /// </summary>
    internal static string? OriginalKnoten(string art)
    {
        if (BaumAlt) return null;
        var nummer = art.Length > 0 && char.IsDigit(art[^1]) ? art[^1] - '1' : 0;
        if (nummer < 0) nummer = 0;
        if (art.StartsWith("Kiefer")) return new[] { "Fichte1", "Fichte2", "Fichte3", "Kiefer1", "Fichte4" }[nummer % 5];
        if (art.StartsWith("Laubbaum")) return "Eiche" + (nummer % 3 + 1);
        return null;
    }

    /// <summary>
    /// Startoption <c>--baumalt=1</c> (nur Vergleiche und Messungen): die vereinfachten Kit-Bäume und eigenen Büsche von vor dem 22.11.2026 statt der Originalbäume.
    /// </summary>
    internal static readonly bool BaumAlt = Wasserarten.Startoption("baumalt") == "1";

    /// <summary>Altes Gegenstück eines Originalbaums („Eiche2“ → Kit „Laubbaum2“, „Fichte3“ → „Kiefer3“, „Hasel1“ → eigener Busch).</summary>
    private static (string Datei, string Knoten) AltBaum(string knoten)
    {
        var nr = char.IsDigit(knoten[^1]) ? knoten[^1] - '0' : 1;
        return Baumjahr.Art(knoten) switch
        {
            "Eiche" or "Buche" or "Apfel" or "Kirsche" => ("natur/natur_kit_baeume", "Laubbaum" + Math.Clamp(nr, 1, 3)),
            "Fichte" => ("natur/natur_kit_baeume", "Kiefer" + Math.Clamp(nr, 1, 5)),
            "Kiefer" => ("natur/natur_kit_baeume", "Kiefer4"),
            "Holunder" => ("natur/natur_buesche", "Holunder1"),
            _ => ("natur/natur_buesche", "Hasel1"),
        };
    }

    internal static bool IstOriginalBaum(string datei) => datei == Baumjahr.Modell;

    private readonly Dictionary<string, (ShaderMaterial Stoff, string Art)> _baumStoffe = new();
    private readonly List<(MultiMeshInstance3D Nah, MultiMeshInstance3D? Fern)> _baumStufen = new();
    private bool _baumNah = true;
    private readonly List<Vector3> _laubOrte = new();
    private GpuParticles3D? _fallLaub;
    private double _fallLaubUhr;

    /// <summary>Tauscht die Materialien eines Baum-Netzes gegen die jahreszeitlichen Windfassungen der Art (je Art, Quellmaterial und Höhe einmal).</summary>
    private void BaumNetzFassen(Mesh netz, string knoten)
    {
        var art = Baumjahr.Art(knoten);
        if (art.Length == 0) return;
        var hoehe = Mathf.Max(netz.GetAabb().End.Y, 1f);
        var zeit = Baumjahr.Von(_jahreszeit);
        for (var i = 0; i < netz.GetSurfaceCount(); i++)
        {
            if (netz.SurfaceGetMaterial(i) is not BaseMaterial3D quelle) continue;
            var schluessel = $"{quelle.ResourceName}|{art}|{hoehe:0.0}";
            if (!_baumStoffe.TryGetValue(schluessel, out var eintrag))
            {
                var m = Baumjahr.Neu(quelle, hoehe);
                Baumjahr.Jahr(m, art, zeit);
                _baumStoffe[schluessel] = eintrag = (m, art);
            }
            netz.SurfaceSetMaterial(i, eintrag.Stoff);
        }
    }

    /// <summary>Stellt alle Baummaterialien auf die Jahreszeit ein (Farben, Blüte, Schnee).</summary>
    private void BaumJahrSetzen(Jahreszeit j)
    {
        var zeit = Baumjahr.Von(j);
        foreach (var (stoff, art) in _baumStoffe.Values) Baumjahr.Jahr(stoff, art, zeit);
    }

    /// <summary>
    /// Baut die Kopien einer Baumart zu zwei MultiMeshes (nah und fern) unter <paramref name="eltern"/>. <paramref name="marker"/>: für Bäume an Markern von Modellen
    /// (die Jahreszeit tauscht <see cref="JahreszeitSetzen"/> über <see cref="_markerBaeume"/>), sonst für die Zellen der Natur (<see cref="_naturWinter"/>).
    /// Gibt den nahen Teil zurück (null, wenn der Knoten fehlt).
    /// </summary>
    private MultiMeshInstance3D? BaumStufenBauen(Node3D eltern, string datei, string knoten, string name, List<Transform3D> lagen, bool marker)
    {
        if (NaturNetz(datei, knoten) is not { } netz) return null;
        var winterNah = NaturNetz(datei, knoten + "_Winter", pflicht: false);
        var fernNetz = NaturNetz(datei, knoten + "_Fern", pflicht: false);
        var winterFern = NaturNetz(datei, knoten + "_FernWinter", pflicht: false) ?? winterNah;
        var winter = _jahreszeit == Jahreszeit.Winter;

        MultiMeshInstance3D Teil(string suffix, Mesh sommer, Mesh? kahl, bool sichtbar)
        {
            var vielfach = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = sommer, InstanceCount = lagen.Count };
            for (var i = 0; i < lagen.Count; i++) vielfach.SetInstanceTransform(i, lagen[i]);
            if (kahl is not null && winter) vielfach.Mesh = kahl;
            var t = new MultiMeshInstance3D { Name = name + suffix, Multimesh = vielfach, Visible = sichtbar, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
            eltern.AddChild(t);
            if (marker)
            {
                if (_markerBaeume.Count > 64) _markerBaeume.RemoveAll(b => !IsInstanceValid(b.Knoten));
                _markerBaeume.Add((t, sommer, kahl));
            }
            else
            {
                if (kahl is not null) _naturWinter.Add((vielfach, sommer, kahl));
                NaturDetailMelden(t, datei);
            }
            return t;
        }

        var nah = Teil("", netz, winterNah, _baumNah);
        var fern = fernNetz is null ? null : Teil("_fern", fernNetz, winterFern, !_baumNah);
        if (_baumStufen.Count > 96) _baumStufen.RemoveAll(s => !IsInstanceValid(s.Nah));
        _baumStufen.Add((nah, fern));
        if (!marker && Baumjahr.Art(knoten) is "Eiche" or "Buche" or "Kirsche" or "Apfel")
            foreach (var l in lagen) _laubOrte.Add(l.Origin);
        return nah;
    }

    /// <summary>Jedes Bild: Stufe der Bäume nach dem Zoom (Spielraum gegen Flackern) und fallendes Laub im Herbst.</summary>
    private void BaeumeAktualisieren(double delta)
    {
        var nah = _kamera.Projection == Camera3D.ProjectionType.Perspective || _kamera.Size <= (_baumNah ? BaumNahBis + BaumSpielraum : BaumNahBis - BaumSpielraum);
        if (nah != _baumNah)
        {
            _baumNah = nah;
            _baumStufen.RemoveAll(s => !IsInstanceValid(s.Nah));
            foreach (var (n, f) in _baumStufen)
            {
                n.Visible = nah;
                if (f is not null && IsInstanceValid(f)) f.Visible = !nah;
            }
        }
        FallendesLaubAktualisieren(delta);
    }

    /// <summary>
    /// Fallendes Laub (nur Grafik Hoch und Ultra, nur im Herbst): ein Teilchensystem am Blickpunkt, nur solange ein Laubbaum in der Nähe steht
    /// (alle halbe Sekunde geprüft). Kleine Blattkarten, die im Wind abdriften; Menge 60 bzw. 120.
    /// </summary>
    private void FallendesLaubAktualisieren(double delta)
    {
        var an = _jahreszeit == Jahreszeit.Herbst && Fenster.AktuelleVoreinstellung is Voreinstellung.Hoch or Voreinstellung.Ultra && _raum is null && _innen is null;
        if (!an && _fallLaub is null) return;
        _fallLaubUhr -= delta;
        if (_fallLaubUhr > 0) return;
        _fallLaubUhr = 0.5;
        if (_fallLaub is null)
        {
            if (!an) return;
            var stoff = new StandardMaterial3D { AlbedoColor = Colors.White, CullMode = BaseMaterial3D.CullModeEnum.Disabled, Roughness = 1f, VertexColorUseAsAlbedo = true };
            var verlauf = new Gradient { Colors = new[] { new Color(0.80f, 0.45f, 0.12f), new Color(0.66f, 0.36f, 0.10f), new Color(0.86f, 0.68f, 0.20f) }, Offsets = new[] { 0f, 0.5f, 1f } };
            _fallLaub = new GpuParticles3D
            {
                Name = "FallendesLaub", Amount = Fenster.AktuelleVoreinstellung == Voreinstellung.Ultra ? 120 : 60, Lifetime = 7.0, Emitting = false,
                DrawPass1 = new QuadMesh { Size = new Vector2(0.18f, 0.14f), Material = stoff },
                VisibilityAabb = new Aabb(new Vector3(-20, -10, -20), new Vector3(40, 16, 40)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                ProcessMaterial = new ParticleProcessMaterial
                {
                    EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(14f, 0.3f, 14f),
                    Direction = new Vector3(1, -0.4f, 0.15f), Spread = 25f, InitialVelocityMin = 0.2f, InitialVelocityMax = 0.8f,
                    Gravity = new Vector3(1.0f, -0.9f, 0.2f), DampingMin = 0.2f, DampingMax = 0.6f, AngularVelocityMin = -180f, AngularVelocityMax = 180f,
                    TurbulenceEnabled = true, TurbulenceNoiseStrength = 0.6f, TurbulenceNoiseScale = 1.5f, ScaleMin = 0.7f, ScaleMax = 1.3f,
                    ColorRamp = new GradientTexture1D { Gradient = verlauf },
                },
            };
            AddChild(_fallLaub);
        }
        var fokus = _kameraDrehung.Position;
        var nahe = an && _laubOrte.Any(o => (o.X - fokus.X) * (o.X - fokus.X) + (o.Z - fokus.Z) * (o.Z - fokus.Z) < 30f * 30f);
        _fallLaub.Position = new Vector3(fokus.X, 7f, fokus.Z);
        _fallLaub.Emitting = nahe;
    }
}
