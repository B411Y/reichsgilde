using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Bodennebel – ein Teil der <c>partial class Stadtansicht</c>. An manchen Morgen liegen flache, weiche Nebelfetzen über dem Wasser, über Wiesen
/// und in Senken; sie ziehen langsam mit dem Wind und lösen sich nach Sonnenaufgang von den Rändern her auf. Billig: ein Netz (bei Morgendunst
/// Hoch zwei übereinander) knapp über dem Boden mit dem Shader <c>bodennebel.gdshader</c>, kein Volumen. Wie gern Nebel an einer Stelle liegt,
/// steht als Eckenwert im Netz (<see cref="NebelNeigung"/>): an Land Senken und Wiese, getrennt davon die Nähe zum Wasser, in der Stadt weniger.
///
/// <b>Wann</b> (nach der Recherche zu Strahlungs- und Flussnebel, docs/bereiche/darstellung.md, „Nebel“):
/// <list type="bullet">
/// <item>Nicht jeden Morgen: Ob sich in der Nacht Nebel bildet, würfelt <see cref="NebelTag"/> je Spieljahr aus dem Startwert der Partie, nach
/// der Jahreszeit (im Herbst am häufigsten und dichtesten) und nur nach einer klaren, ruhigen Nacht (Wetter um 4 Uhr). Weht es oder regnet es
/// gerade, löst er sich auf (<see cref="NebelWetterRuhe"/>).</item>
/// <item>Über den Tag (<see cref="NebelVerlauf"/>, nach den Sonnenzeiten der Jahreszeit): am dichtesten um den Sonnenaufgang, danach in
/// 1¾ (Sommer) bis 3 Stunden (Herbst, Winter) aufgelöst, über dem Wasser eine gute halbe Stunde länger; abends erste Fetzen über dem Wasser.</item>
/// <item>Nachts (<see cref="Nachtnebel"/>): an = der Nebel wächst nach Einbruch der Dunkelheit weiter, in der Farbe der Nacht (nie heller als eine
/// unbeleuchtete Wand); aus = Lösung A vom 29.10.2026, nachts klare Luft.</item>
/// <item>Bei Wetter „Nebel“ liegt er den ganzen Tag. Aus bei Morgendunst „Aus“.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Abstand der Netzpunkte (Meter) und Höhe der beiden Schichten über dem Boden.</summary>
    private const float NebelZelle = 6f, NebelHoeheUnten = 0.35f, NebelHoeheOben = 1.6f;

    /// <summary>Nachts ist der Nebel so hell wie eine Fläche dieser Helligkeit (Albedo, linear) im Umgebungslicht – dunkler als Putz und Stein.</summary>
    private const float NebelNachtAlbedo = 0.2f;

    /// <summary>
    /// So stark hellt der Mond den Nebel auf, wo er ihn erreicht (wie eine Fläche dieser Albedo, ohne Winkel); in den Schatten der Häuser gar nicht.
    /// Doppelt so hell wie die mondbeschienene Wiese (Albedo etwa 0,1), damit man den Nebel sieht, aber dunkler als eine mondbeschienene Wand aus
    /// Putz oder Stein (0,3–0,5 mal dem Winkel zum Mond): kein heller Saum am Fuß.
    /// </summary>
    private const float NebelMond = 0.18f;

    /// <summary>Lichthof der Laternen, Fackeln und Feuer im Nebel (Anteil ihres Lichts).</summary>
    private const float NebelHof = 0.35f;

    private static Shader? _nebelShader;
    private readonly List<(MeshInstance3D Netz, ShaderMaterial Stoff)> _nebelSchichten = new();
    private Node3D? _nebelKnoten;
    private (Kartengrenzen? Grenzen, int Mauern, bool Hoch) _nebelFuer;
    private (float Land, float Wasser, float Mond) _nebelGesetzt = (-1, -1, -1);
    private Color _nebelFarbe = new(-1, 0, 0);

    /// <summary>
    /// Nebel auch in der Nacht (Entwicklerfenster „Wetter und Darstellung“, Startoption <c>--nachtnebel=an|aus</c>; nur für diesen Start). An: Nach
    /// Einbruch der Dunkelheit wächst der Nebel einer ruhigen Nacht weiter, dunkel gefärbt, mit Lichthof um Laternen und Feuer. Aus: wie Lösung A
    /// (29.10.2026) – nachts klare Luft, der Nebel kommt erst mit der Morgendämmerung.
    /// </summary>
    public bool Nachtnebel { get; set; } = true;

    /// <summary>Nebel des Tages fest vorgeben (0–1, Entwicklerfenster und <c>--morgennebel</c>); null = gewürfelt (<see cref="NebelTag"/>).</summary>
    public float? MorgennebelVorgabe { get; set; }

    /// <summary>Die Nebelneigung des laufenden Spieltags (0–1) und woher sie kommt – für das Entwicklerfenster.</summary>
    public (float Staerke, string Grund) MorgennebelHeute => NebelTagMitGrund(_letzteLichtJahreszeit ?? Jahreszeit.Fruehling);

    /// <summary>Die Stärke gerade jetzt (Land, Wasser; 0–1, ohne Grafikstufe) – auch für den Dunst (Stadtansicht.Grafik.cs).</summary>
    private (float Land, float Wasser) _nebelJetzt;

    private (int Jahr, int Startwert, Wetterlage? Vorgabe, Jahreszeit Zeit, float Wert, string Grund)? _nebelTagFuer;

    /// <summary>
    /// Wie dicht der Nebel dieses Spieltags wird (0–1): in einer klaren, ruhigen Nacht bildet sich Strahlungsnebel, über dem Wasser am meisten.
    /// Je Spieljahr fest gewürfelt aus Startwert und Jahr (nach dem Laden gleich, zwei Spieler sehen dasselbe). Die Nacht selbst wird nicht gespielt
    /// (23 → 4 Uhr); für sie steht das Wetter zu Beginn des Spieltags.
    /// <list type="bullet">
    /// <item>Herbst: an drei von vier ruhigen Morgen, Stärke 0,55–1 (die Nächte sind lang, das Wasser noch warm).</item>
    /// <item>Frühling 45 %, 0,35–0,8 · Winter 45 %, 0,4–0,9 · Sommer 35 %, 0,25–0,6 (kurze Nacht, schnell verflogen).</item>
    /// <item>Sonst höchstens ein Hauch (bis 0,12) über dem Wasser.</item>
    /// <item>Nacht klar oder neblig: voll · wolkig: gut ein Drittel · Schnee: wenig · Regen, Sturm, Gewitter: keiner.</item>
    /// </list>
    /// </summary>
    private float NebelTag(Jahreszeit jahreszeit) => NebelTagMitGrund(jahreszeit).Staerke;

    private (float Staerke, string Grund) NebelTagMitGrund(Jahreszeit jahreszeit)
    {
        if (MorgennebelVorgabe is { } vorgabe) return (vorgabe, "fest vorgegeben");
        if (_simulation is null) return (0, "ohne Partie");
        var startwert = _simulation.Einstellungen.Zufallsstartwert;
        var jahr = (int)(_simulation.Zeit.VergangeneMinuten / Spielzeit.MinutenProTag);
        if (_nebelTagFuer is { } f && f.Jahr == jahr && f.Startwert == startwert && f.Vorgabe == _wetterVorgabe && f.Zeit == jahreszeit) return (f.Wert, f.Grund);
        var nacht = _wetterVorgabe ?? WetterablaufDerPartie()?.Lage((long)jahr * Spielzeit.MinutenProTag) ?? Wetterlage.Klar;
        var ruhe = nacht switch
        {
            Wetterlage.Klar or Wetterlage.Nebel => 1f,
            Wetterlage.Wolkig => 0.35f,
            Wetterlage.Schnee => 0.15f,
            _ => 0f,
        };
        var (chance, kleinste, groesste) = jahreszeit switch
        {
            Jahreszeit.Herbst => (0.75, 0.55, 1.0),
            Jahreszeit.Winter => (0.45, 0.4, 0.9),
            Jahreszeit.Sommer => (0.35, 0.25, 0.6),
            _ => (0.45, 0.35, 0.8),
        };
        var zufall = new Random(unchecked(startwert * 48271 + jahr * 69621 + 31));
        var (wurf, hoehe) = (zufall.NextDouble(), zufall.NextDouble());
        var nebelmorgen = wurf < chance;
        var wert = ruhe * (float)(nebelmorgen ? kleinste + (groesste - kleinste) * hoehe : 0.12 * hoehe);
        var grund = $"{(nebelmorgen ? "Nebelmorgen" : "kein Nebelmorgen")}, Nacht {Wetterablauf.Name(nacht).ToLowerInvariant()}";
        _nebelTagFuer = (jahr, startwert, _wetterVorgabe, jahreszeit, wert, grund);
        return (wert, grund);
    }

    /// <summary>Wie lange (Stunden) der Nebel nach Sonnenaufgang braucht, bis er an Land aufgelöst ist: Je flacher die Sonne, desto länger.</summary>
    private static float NebelAufloesung(Jahreszeit jahreszeit) => jahreszeit switch
    {
        Jahreszeit.Sommer => 1.75f,
        Jahreszeit.Herbst or Jahreszeit.Winter => 3f,
        _ => 2.25f,
    };

    /// <summary>
    /// Der Verlauf über den Tag (0–1, Land und Wasser) zur Uhrzeit <paramref name="stunde"/> (mit Bruchteil) nach den Sonnenzeiten der Jahreszeit:
    /// Vor Sonnenaufgang dicht (mit Nachtnebel schon ab 4 Uhr zu 85 %, ohne erst ab der Morgendämmerung), bis eine Viertelstunde nach dem Aufgang
    /// voll, dann in <see cref="NebelAufloesung"/> Stunden aufgelöst (über dem Wasser eine gute halbe Stunde später). Abends nach Sonnenuntergang
    /// erste Fetzen (bis 0,3); mit Nachtnebel wächst er in der Nacht weiter (zur Dunkelheit + 3½ Stunden bis 0,65), ohne endet er mit der Dunkelheit.
    /// </summary>
    private static (float Land, float Wasser) NebelVerlauf(float stunde, Jahreszeit jahreszeit, bool nachtnebel)
    {
        var lauf = Sonnenzeiten.Fuer(jahreszeit);
        float daemmerung = lauf.Daemmerung / 60f, aufgang = lauf.Aufgang / 60f, untergang = lauf.Untergang / 60f, dunkel = lauf.Dunkel / 60f;
        if (stunde < 12f)
        {
            if (stunde < aufgang)
            {
                var vorher = nachtnebel ? 0.85f + 0.15f * Mathf.SmoothStep(daemmerung - 1f, aufgang, stunde) : Mathf.SmoothStep(daemmerung, aufgang, stunde);
                return (vorher, vorher);
            }
            var dauer = NebelAufloesung(jahreszeit);
            return (1f - Mathf.SmoothStep(aufgang + 0.25f, aufgang + dauer, stunde), 1f - Mathf.SmoothStep(aufgang + 0.5f, aufgang + dauer + 0.6f, stunde));
        }
        var abend = 0.3f * Mathf.SmoothStep(untergang - 0.3f, dunkel, stunde);
        var wert = nachtnebel ? abend + 0.35f * Mathf.SmoothStep(dunkel, dunkel + 3.5f, stunde) : abend * (1f - Mathf.SmoothStep(dunkel - 0.2f, dunkel + 0.5f, stunde));
        return (wert * 0.7f, wert); // abends zuerst über dem Wasser
    }

    /// <summary>Wie ruhig das Wetter gerade ist (0–1): Wind ab 0,3 und Regen oder Schneefall lösen den Nebel auf.</summary>
    private float NebelWetterRuhe() =>
        (1f - Mathf.SmoothStep(0.3f, 0.75f, _wetter.Wind)) * (1f - Mathf.SmoothStep(0.1f, 0.5f, _wetter.Regen)) * (1f - Mathf.SmoothStep(0.2f, 0.6f, _wetter.Schnee));

    /// <summary>Die Stärke des Bodennebels gerade jetzt (Land, Wasser), ohne Grafikstufe: Tag × Verlauf × Ruhe, mindestens der Nebel des Wetters.</summary>
    private (float Land, float Wasser) NebelStaerkeJetzt()
    {
        var jahreszeit = _letzteLichtJahreszeit ?? Jahreszeit.Fruehling;
        var stunde = _letzteLichtUhrzeit >= 0 ? _letzteLichtUhrzeit / 60f : 12f; // im Hauptmenü (ohne Uhr): Mittag
        var tag = NebelTag(jahreszeit) * NebelWetterRuhe();
        var (land, wasser) = NebelVerlauf(stunde, jahreszeit, Nachtnebel);
        return (Mathf.Max(tag * land, 0.85f * _wetter.Nebel), Mathf.Max(tag * wasser, 0.9f * _wetter.Nebel));
    }

    /// <summary>
    /// Wie dunkel es ist (0 = Tag, 1 = Nacht), für die Farbe von Nebel und Dunst: nach der Helligkeit der Lichtstimmung (kurz nach Sonnenaufgang
    /// schon Tag, vor der Morgendämmerung und nach der Abenddämmerung Nacht).
    /// </summary>
    private float NebelDunkel() => 1f - Mathf.SmoothStep(0.05f, 0.4f, _gezeigteStimmung?.Hell ?? 1f);

    /// <summary>
    /// Die Farbe des Nebels in der Nacht: so hell wie eine Fläche mit <see cref="NebelNachtAlbedo"/> im Umgebungslicht (linear gerechnet, dann
    /// als sRGB-Farbe) – nie heller als eine unbeleuchtete Wand. Der alte Dunst hatte die volle Farbe des Umgebungslichts (dazu 35 % Mond) und
    /// war damit fünf- bis zehnmal so hell wie dunkle Wände: der helle Saum am Fuß von Häusern und Mauern (Lösung A, 29.10.2026).
    /// </summary>
    private Color NebelNachtfarbe()
    {
        var umgebung = _umgebung.Environment;
        var licht = umgebung.AmbientLightColor.SrgbToLinear() * umgebung.AmbientLightEnergy * NebelNachtAlbedo;
        return new Color(licht.R, licht.G, licht.B).LinearToSrgb();
    }

    /// <summary>Jedes Bild (aus <see cref="GrafikBild"/>): Stärke ausrechnen, Schwaden anlegen, wenn die Karte steht, Stärke und Farbe nachführen.</summary>
    private void BodennebelNachfuehren()
    {
        var stufe = Fenster.AktuellerDunst;
        _nebelJetzt = _raum is not null || _entwurf is not null || _grenzen is null ? (0f, 0f) : NebelStaerkeJetzt();
        var (land, wasser) = stufe == Morgendunst.Aus ? (0f, 0f) : _nebelJetzt;
        if (Mathf.Max(land, wasser) < 0.01f)
        {
            if (_nebelKnoten is { Visible: true }) _nebelKnoten.Visible = false;
            _nebelGesetzt = (-1, -1, -1);
            return;
        }
        var hoch = stufe == Morgendunst.Hoch;
        var schluessel = (_grenzen, _mauerFlaechen.Count, hoch);
        if (_nebelKnoten is null || schluessel != _nebelFuer) NebelBauen(hoch);
        _nebelFuer = schluessel;
        _nebelKnoten!.Visible = true;
        // Tags die Farbe des Horizonts (Himmel dieser Stunde), etwas kühler und aufgehellt: derselbe Ton wie der Dunstring am Rand. Nachts dunkel
        // (NebelNachtfarbe), dazu ein wenig Mondlicht, wo der Mond hinkommt (im Shader mit seinen Schatten).
        var himmel = _umgebung.Environment.BackgroundColor;
        var hell = himmel.R * 0.3f + himmel.G * 0.55f + himmel.B * 0.15f;
        var tagfarbe = himmel.Lerp(new Color(hell * 0.94f, hell, hell * 1.07f), 0.6f).Lerp(Colors.White * Mathf.Min(1f, hell + 0.1f), 0.15f);
        var dunkel = NebelDunkel();
        var farbe = tagfarbe.Lerp(NebelNachtfarbe(), dunkel);
        var mond = NebelMond * dunkel;
        if (Mathf.Abs(land - _nebelGesetzt.Land) < 0.004f && Mathf.Abs(wasser - _nebelGesetzt.Wasser) < 0.004f && Mathf.Abs(mond - _nebelGesetzt.Mond) < 0.002f
            && farbe.IsEqualApprox(_nebelFarbe)) return;
        _nebelGesetzt = (land, wasser, mond);
        _nebelFarbe = farbe;
        foreach (var (_, stoff) in _nebelSchichten)
        {
            stoff.SetShaderParameter("staerke_land", land);
            stoff.SetShaderParameter("staerke_wasser", wasser);
            stoff.SetShaderParameter("farbe", farbe);
            stoff.SetShaderParameter("mond", mond);
            stoff.SetShaderParameter("hof", NebelHof * dunkel);
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
        var land = new float[(nx + 1) * (nz + 1)];
        var nass = new float[land.Length];
        var stadt = new float[land.Length];
        var boden = new float[land.Length];
        for (var j = 0; j <= nz; j++)
            for (var i = 0; i <= nx; i++)
            {
                var k = j * (nx + 1) + i;
                var (x, z) = ((float)(rahmen.X0 + i * dx), (float)(rahmen.Z0 + j * dz));
                (land[k], nass[k], stadt[k]) = NebelNeigung(x, z, mauern, wasser, out boden[k]);
            }
        // Weich zeichnen: Die Wassernähe zweimal über 7 × 7 Punkte (gut 20 m), so läuft sie am Ufer weich aus, und ein schmaler Bach kommt
        // nur auf etwa ein Drittel (vorher lag an jedem Bach ein 40 m breites, gleichmäßig dichtes Band mit gerader Kante). Das Land einmal über 3 × 3.
        Weichzeichnen(nass, nx + 1, nz + 1, 3);
        Weichzeichnen(nass, nx + 1, nz + 1, 3);
        Weichzeichnen(land, nx + 1, nz + 1, 1);
        for (var j = 0; j <= nz; j++)
            for (var i = 0; i <= nx; i++)
            {
                var k = j * (nx + 1) + i;
                // In der Stadt weniger (weich über die Mauer, siehe NebelNeigung); zum Rand des Netzes (40 m über das Spielfeld hinaus) weich auslaufen
                var randAbstand = (float)Math.Min(Math.Min(i * dx, (nx - i) * dx), Math.Min(j * dz, (nz - j) * dz));
                var rand = Mathf.SmoothStep(0f, 30f, randAbstand);
                land[k] *= stadt[k] * rand;
                nass[k] = Mathf.Clamp(nass[k] * 1.15f, 0f, 1f) * stadt[k] * rand;
            }
        // Bei Hoch zwei Schichten: die untere ist meist über die Tiefe ausgeblendet (zu nah am Boden), zusammen etwa so dicht wie eine
        var schichten = hoch ? new[] { (NebelHoeheUnten, 0f, 0.6f), (NebelHoeheOben, 1f, 0.85f) } : new[] { (NebelHoeheUnten + 0.4f, 0f, 1f) };
        foreach (var (hoehe, versatz, deckkraft) in schichten)
        {
            var ecken = new List<Vector3>();
            var farben = new List<Color>();
            var indizes = new List<int>();
            for (var j = 0; j <= nz; j++)
                for (var i = 0; i <= nx; i++)
                {
                    var k = j * (nx + 1) + i;
                    ecken.Add(new Vector3((float)(rahmen.X0 + i * dx), boden[k] + hoehe, (float)(rahmen.Z0 + j * dz)));
                    farben.Add(new Color(land[k], nass[k], 0));
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
            stoff.SetShaderParameter("deckkraft", deckkraft);
            var instanz = new MeshInstance3D { Name = "Nebelschicht", Mesh = netz, MaterialOverride = stoff, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _nebelKnoten.AddChild(instanz);
            _nebelSchichten.Add((instanz, stoff));
        }
        _nebelGesetzt = (-1, -1, -1);
    }

    /// <summary>Ein Kastenfilter über (2·<paramref name="r"/>+1)² Punkte eines Rasters (<paramref name="breite"/> × <paramref name="hoehe"/>), getrennt nach Zeilen und Spalten; am Rand wird nur über das gemittelt, was da ist.</summary>
    private static void Weichzeichnen(float[] werte, int breite, int hoehe, int r)
    {
        var zwischen = new float[werte.Length];
        for (var j = 0; j < hoehe; j++)
            for (var i = 0; i < breite; i++)
            {
                var (summe, n) = (0f, 0);
                for (var a = Math.Max(0, i - r); a <= Math.Min(breite - 1, i + r); a++, n++) summe += werte[j * breite + a];
                zwischen[j * breite + i] = summe / n;
            }
        for (var j = 0; j < hoehe; j++)
            for (var i = 0; i < breite; i++)
            {
                var (summe, n) = (0f, 0);
                for (var b = Math.Max(0, j - r); b <= Math.Min(hoehe - 1, j + r); b++, n++) summe += zwischen[b * breite + i];
                werte[j * breite + i] = summe / n;
            }
    }

    /// <summary>
    /// Wie gern Nebel an dieser Stelle liegt und die Höhe des Bodens (Wasser: des Spiegels). <c>Land</c>: Senken gegenüber dem Ring 20 m ringsum,
    /// auf der Wiese ein Grundwert 0,3. <c>Wasser</c>: 1 im Wasser und bis 3 m vor dem Ufer, bis 9 m auslaufend (wird danach weichgezeichnet).
    /// <c>Stadt</c>: Faktor für beide, innerhalb der Mauer ein Viertel.
    /// </summary>
    private (float Land, float Wasser, float Stadt) NebelNeigung(float x, float z, Vector2[][] mauern, Wasserform? wasser, out float hoehe)
    {
        hoehe = BodenHoehe(x, z);
        var mittel = 0f;
        for (var k = 0; k < 8; k++)
        {
            var w = k * Mathf.Pi / 4f;
            mittel += BodenHoehe(x + 20f * Mathf.Cos(w), z + 20f * Mathf.Sin(w));
        }
        var senke = Mathf.Clamp((mittel / 8f - hoehe) / 1.2f, 0f, 1f);
        var land = Mathf.Max(0.3f, senke);
        var nass = 0f;
        if (wasser is not null)
        {
            if (wasser.Spiegel(x, z) is { } spiegel) hoehe = Mathf.Max(hoehe, (float)spiegel);
            var ufer = (float)wasser.Uferabstand(x, z, 20);
            if (ufer < 20) nass = 1f - Mathf.SmoothStep(3f, 9f, ufer);
        }
        var p = new Vector2(x, z);
        // In der Stadt nur ein Viertel – aber als weicher Übergang: an der Mauer liegt der Nebel noch fast voll, 30 m hinter ihr ist es das Viertel. Ein Sprung
        // an der Mauer zeigte sich als harte Nebelkante (Bildschirmfoto 04:08 Uhr): Die Mauer verdeckt, was hinter ihr liegt, man sah nur das abrupte Ende der Schwade.
        var abstand = float.MaxValue;
        var innen = false;
        foreach (var m in mauern)
        {
            for (var i = 0; i < m.Length; i++) abstand = Mathf.Min(abstand, AbstandZurStrecke(p, m[i], m[(i + 1) % m.Length]));
            innen |= Geometry2D.IsPointInPolygon(p, m);
        }
        var stadt = 1f;
        if (abstand < float.MaxValue)
        {
            var d = innen ? -abstand : abstand; // vor der Mauer positiv, in der Stadt negativ
            stadt = 1f - 0.75f * (1f - Mathf.SmoothStep(-30f, 6f, d));
        }
        return (land, nass, stadt);
    }
}
