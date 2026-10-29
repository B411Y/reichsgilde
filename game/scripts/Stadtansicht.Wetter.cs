using Godot;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Wetter und Jahreszeiten (PR #195) – ein Teil der <c>partial class Stadtansicht</c>. Nur Darstellung: Der Kern weiß nichts vom Wetter.
///
/// <b>Ablauf.</b> Welches Wetter gerade ist, rechnet <see cref="Wetterablauf"/> aus dem Startwert der Partie und der Spielzeit (nichts wird
/// gespeichert, nach dem Laden läuft es genauso weiter). Hier werden die Werte der Lage (<see cref="Wetterwerte"/>) in Echtzeit weich
/// nachgeführt (<see cref="WetterNachfuehrSekunden"/>): Springt die Uhr (Jahreswechsel, Laden, Spulen), gleitet das Bild über, statt zu springen.
///
/// <b>Wirkung.</b>
/// <list type="bullet">
/// <item>Licht und Himmel (<see cref="MitWetter"/>, aus <see cref="LichtNachUhrzeit"/>): Wolken dämpfen Sonne und Schatten und färben den Himmel grau,
/// ein Blitz hellt Himmel und Umgebung auf und hat ein eigenes Licht (ab Wettereffekte Mittel; in der Stufe Hoch mit Schatten, jedes Mal aus anderer Richtung).</item>
/// <item>Nebel: Godots Entfernungsnebel, dazu wird der Morgendunst dichter (Stadtansicht.Grafik.cs).</item>
/// <item>Regen und Schnee: je ein <see cref="GPUParticles3D"/> in einem Kasten um den Blickpunkt, mit dem Zoom mitwachsend (Tropfen und Flocken
/// sind so bei jedem Zoom gleich groß im Bild).</item>
/// <item>Boden, Gras, Äcker und Modelle: globale Shader-Uniforms (<c>wetter.gdshaderinc</c>) – Nässe, Pfützen, Schneedecke auf Boden und Dächern,
/// Wind in Gras und Laub, die Farbe der Wiese je Jahreszeit. Die Fahnen flattern mit <c>wind_staerke</c>.</item>
/// <item>Wolkenschatten ziehen im Wind schneller (Stadtansicht.Grafik.cs).</item>
/// </list>
/// Die Einstellung „Wettereffekte“ (<see cref="Fenster.AktuelleWettereffekte"/>) wählt, wie viel davon zu sehen ist.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>So schnell (Sekunden Echtzeit, Zeitkonstante) folgen die gezeigten Werte dem Wetter der Uhr.</summary>
    private const float WetterNachfuehrSekunden = 1.2f;

    /// <summary>
    /// Dichte des Entfernungsnebels bei vollem Nebel: Die Kamera steht 150 m vor der Stadt; bei 0,0016 verblasst die Stadt nur um ein Viertel,
    /// die Ferne (250 m) um ein Drittel – alles bleibt erkennbar, Boden und Gebäude gleich (Godots Nebel wirkt auf jedes Material).
    /// </summary>
    private const float WetterNebelDichte = 0.0016f;

    private Wetterablauf? _wetterablauf;
    private int _wetterStartwert;
    private Wetterlage? _wetterVorgabe;
    private Jahreszeit? _jahreszeitVorgabe;
    private bool _blitzHalten;

    /// <summary>Die gezeigten Werte (in Echtzeit nachgeführt), ohne Partie klar.</summary>
    private Wetterwerte _wetter = Wetterwerte.Von(Wetterlage.Klar);
    private float _naesse, _schneeDecke;
    private Vector4 _jahreszeitAnteile = new(0, 1, 0, 0);
    private long _naesseMinute = -1;
    private float _naesseZiel;

    private GpuParticles3D _regen = null!, _schnee = null!;
    private ParticleProcessMaterial _regenStoff = null!, _schneeStoff = null!;
    private ShaderMaterial _regenFarbe = null!; // regen.gdshader: Striche in Fallrichtung
    private GpuParticles3D _regenRinge = null!; // Ringe und Spritzer am Boden (regenring.gdshader)
    private ParticleProcessMaterial _ringStoff = null!;
    private ShaderMaterial _schneeFarbe = null!; // schnee.gdshader: weiche Flocken, die pendeln und sich drehen
    private DirectionalLight3D _blitzLicht = null!;
    private float _blitzZeit = -1, _naechsterBlitz = 3;
    private float _blitz;
    private readonly Random _blitzZufall = new();
    private int _blitzPulse;
    private float _blitzAbstand, _blitzHaltenZeit;
    private Wettereffekte? _partikelStufe;
    private (float K, float Wind, float Breite)? _partikelFuer;
    private float _partikelHell = -1;

    /// <summary>Werte, die zuletzt an die globalen Shader-Uniforms gingen (gesetzt wird nur bei Änderung).</summary>
    private readonly Dictionary<string, Variant> _wetterGlobal = new();

    // ═════════════════════════════════════════════════════════════════════════
    //  Abfragen und Vorgaben
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Wetterlage gerade jetzt (ohne Partie null) – für die Wetteranzeige.</summary>
    public Wetterlage? WetterJetzt =>
        _simulation is null ? null : _wetterVorgabe ?? WetterablaufDerPartie()?.Lage(_simulation.Zeit.VergangeneMinuten);

    /// <summary>Die nächsten Wetterabschnitte des Tages (Beginn als Uhrzeit in Minuten nach Mitternacht), höchstens <paramref name="anzahl"/>.</summary>
    public IReadOnlyList<(int Uhrzeit, Wetterlage Lage)> WetterAussicht(int anzahl)
    {
        if (_simulation is null || _wetterVorgabe is not null || WetterablaufDerPartie() is not { } ablauf) return Array.Empty<(int, Wetterlage)>();
        return ablauf.Aussicht(_simulation.Zeit.VergangeneMinuten, anzahl);
    }

    /// <summary>
    /// Startoptionen <c>--wetter</c>, <c>--jahreszeit</c> und <c>--blitz=1</c>: Lage und Jahreszeit fest vorgeben (nur Darstellung, nur für diesen
    /// Start), ein Blitz bleibt stehen (für Bildschirmfotos). Die Werte stehen sofort, ohne Überblenden.
    /// </summary>
    public void WetterVorgeben(Wetterlage? lage, Jahreszeit? jahreszeit, bool blitzHalten)
    {
        _wetterVorgabe = lage;
        _jahreszeitVorgabe = jahreszeit;
        _blitzHalten = blitzHalten;
        _wetterSofort = true;
    }

    /// <summary>Startoption <c>--wetterspaeter=regen@20</c>: nach so vielen Sekunden schlägt das Wetter plötzlich um (für Messungen des ersten Regens).</summary>
    public void WetterVorgebenNach(Wetterlage lage, double sekunden) =>
        GetTree().CreateTimer(sekunden).Timeout += () => WetterVorgeben(lage, _jahreszeitVorgabe, _blitzHalten);

    private bool _wetterSofort;

    private Wetterablauf? WetterablaufDerPartie()
    {
        if (_simulation is null) return null;
        var startwert = _simulation.Einstellungen.Zufallsstartwert;
        if (_wetterablauf is null || _wetterStartwert != startwert)
        {
            _wetterablauf = new Wetterablauf(startwert);
            _wetterStartwert = startwert;
        }
        return _wetterablauf;
    }

    /// <summary>Die Jahreszeit, die die Stadt zeigt: die der Uhr oder die der Startoption.</summary>
    private Jahreszeit GezeigteJahreszeit(Jahreszeit uhr) => _jahreszeitVorgabe ?? uhr;

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Einmal beim Aufbau (aus <see cref="GrafikEinrichten"/>): Regen, Schnee und das Licht der Blitze.</summary>
    private void WetterEinrichten()
    {
        // Regen: Tropfen als Striche in Fallrichtung (regen.gdshader), unterschiedlich lang und durchscheinend, der Kopf heller als der Schweif
        _regenFarbe = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/regen.gdshader") };
        _regenStoff = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            Direction = Vector3.Down,
            Spread = 3,
            Gravity = Vector3.Zero,
        };
        _regen = new GpuParticles3D
        {
            Name = "Regen",
            Amount = 14000,
            Lifetime = 1.0,
            Preprocess = 1.0,
            LocalCoords = false,
            Emitting = false,
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ProcessMaterial = _regenStoff,
            DrawPass1 = new QuadMesh { Size = Vector2.One, Material = _regenFarbe },
        };
        AddChild(_regen);

        // Ringe und Spritzer: kurzlebige Ringe auf dem Boden, die sich weiten und ausblassen (regenring.gdshader)
        _ringStoff = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(10, 0.01f, 10),
            Gravity = Vector3.Zero,
            ScaleMin = RingGroesse,
            ScaleMax = RingGroesse * 2f,
        };
        var ringNetz = new QuadMesh { Size = Vector2.One, Orientation = PlaneMesh.OrientationEnum.Y, Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/regenring.gdshader") } };
        _regenRinge = new GpuParticles3D
        {
            Name = "Regenringe", Amount = 1000, Lifetime = 0.55, Preprocess = 0.55, LocalCoords = false, Emitting = false, Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ProcessMaterial = _ringStoff, DrawPass1 = ringNetz,
        };
        AddChild(_regenRinge);

        // Schnee: weiche, runde Flocken, die im Fallen pendeln (schnee.gdshader)
        _schneeFarbe = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/schnee.gdshader") };
        _schneeStoff = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            Direction = Vector3.Down,
            Spread = 25,
            Gravity = Vector3.Zero,
            ScaleMin = 0.6f,
            ScaleMax = 1.3f,
            TurbulenceNoiseScale = 6f,
            TurbulenceNoiseSpeedRandom = 0.3f,
        };
        _schnee = new GpuParticles3D
        {
            Name = "Schnee",
            Amount = 6000,
            Lifetime = 9.0,
            Preprocess = 9.0,
            LocalCoords = false,
            Emitting = false,
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ProcessMaterial = _schneeStoff,
            DrawPass1 = new QuadMesh { Size = new Vector2(0.5f, 0.5f), Material = _schneeFarbe },
        };
        AddChild(_schnee);

        // Blitz: ein zweites gerichtetes Licht, kühl-weiß; nur während eines Blitzes an (dann ohne Dauerkosten), Schatten nur in der Stufe Hoch
        _blitzLicht = new DirectionalLight3D
        {
            Name = "Blitz",
            LightColor = new Color(0.82f, 0.87f, 1f),
            LightEnergy = 0,
            ShadowEnabled = false,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
            DirectionalShadowFadeStart = 1f,
            Visible = false,
            RotationDegrees = new Vector3(-70, 30, 0),
        };
        AddChild(_blitzLicht);
    }

    /// <summary>Beim Verlassen (aus <c>_ExitTree</c>): Die globalen Werte gelten für das ganze Spiel – das Hauptmenü soll wieder Sommer und trocken zeigen.</summary>
    private void WetterAufgeben()
    {
        foreach (var (name, wert) in new (string, Variant)[]
                 {
                     ("wetter_naesse", 0f), ("wetter_regen", 0f), ("wetter_rinn", 0f), ("wetter_schnee", 0f), ("wetter_dach", 0f), ("wetter_flaechen", 1f),
                     ("wetter_wind", 0.25f), ("jahreszeit_anteile", new Vector4(0, 1, 0, 0)),
                     ("wind_staerke", Fenster.FahnenBewegen ? 0.5f : 0f), // der Grundwert der Fahnen (Fenster.Grafik.cs)
                 })
            RenderingServer.GlobalShaderParameterSet(name, wert);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Jedes Bild
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Jedes Bild mit Partie (aus <see cref="Aktualisieren"/>, vor dem Licht): Zielwerte aus dem Ablauf, weich nachgeführt, an Shader und Partikel.
    /// <paramref name="minute"/> = vergangene Spielminuten mit Bruchteil.
    /// </summary>
    private void WetterAktualisieren(double minute, Jahreszeit jahreszeit, double delta)
    {
        var ablauf = WetterablaufDerPartie();
        var ziel = _wetterVorgabe is { } vorgabe ? Wetterwerte.Von(vorgabe) : ablauf?.Werte(minute) ?? Wetterwerte.Von(Wetterlage.Klar);
        // Nässe: aus dem Regen der letzten Stunden (einmal je Spielminute gerechnet); mit Vorgabe so nass, wie es nach Stunden dieses Wetters wäre
        if (_wetterVorgabe is not null) _naesseZiel = Mathf.Min(1, ziel.Regen * 1.6f);
        else if ((long)minute != _naesseMinute && ablauf is not null)
        {
            _naesseMinute = (long)minute;
            _naesseZiel = ablauf.Naesse(minute);
        }
        // Schneedecke: im Winter immer, bei Schneefall dichter; schneit es (Vorgabe) außerhalb des Winters, bleibt er auch liegen
        var winter = GezeigteJahreszeit(jahreszeit) == Jahreszeit.Winter;
        var deckeZiel = Mathf.Max(winter ? 0.62f + 0.45f * ziel.Schnee : 0f, ziel.Schnee * 0.9f);
        var anteileZiel = GezeigteJahreszeit(jahreszeit) switch
        {
            Jahreszeit.Fruehling => new Vector4(1, 0, 0, 0),
            Jahreszeit.Herbst => new Vector4(0, 0, 1, 0),
            Jahreszeit.Winter => new Vector4(0, 0, 0, 1),
            _ => new Vector4(0, 1, 0, 0),
        };

        var t = _wetterSofort ? 1f : 1 - Mathf.Exp(-(float)delta / WetterNachfuehrSekunden);
        _wetterSofort = false;
        _wetter = Wetterwerte.Mischen(_wetter, ziel, t);
        _naesse = Mathf.Lerp(_naesse, _naesseZiel, t);
        _schneeDecke = Mathf.Lerp(_schneeDecke, deckeZiel, t);
        _jahreszeitAnteile = _jahreszeitAnteile.Lerp(anteileZiel, t);

        BlitzNachfuehren((float)delta);
        WetterGlobaleSetzen();
        PartikelNachfuehren();
        TraufenStellen(delta); // Stadtansicht.Traufen.cs
        PflasterReliefStellen(); // Stadtansicht.Pflasterrelief.cs
    }

    private void WetterGlobal(string name, float wert, float schwelle = 0.002f)
    {
        if (_wetterGlobal.TryGetValue(name, out var alt) && Mathf.Abs(alt.AsSingle() - wert) < schwelle) return;
        _wetterGlobal[name] = wert;
        RenderingServer.GlobalShaderParameterSet(name, wert);
    }

    private void WetterGlobaleSetzen()
    {
        var stufe = Fenster.AktuelleWettereffekte;
        var draussen = _innen is null && _raum is null;
        // Nässe und Pfützen ab Mittel; Schnee liegt in jeder Stufe (er gehört zur Jahreszeit)
        WetterGlobal("wetter_naesse", stufe == Wettereffekte.Niedrig ? 0 : _naesse);
        WetterGlobal("wetter_regen", stufe == Wettereffekte.Hoch ? _wetter.Regen : 0);
        // Rinnsale auf Dächern und Wänden: ab Mittel; bei Regen stark, danach laufen sie mit dem Abtrocknen (Nässe) langsam aus
        var rinn = Mathf.SmoothStep(0.3f, 0.9f, _naesse) * Mathf.Clamp(0.15f + 1.1f * _wetter.Regen, 0, 1);
        WetterGlobal("wetter_rinn", stufe == Wettereffekte.Niedrig || Mathf.IsZeroApprox(_naesse) ? 0 : rinn * (stufe == Wettereffekte.Hoch ? 1f : 0.6f));
        WetterGlobal("wetter_schnee", _schneeDecke);
        WetterGlobal("wetter_dach", _schneeDecke);
        WetterGlobal("wetter_flaechen", draussen ? 1 : 0);
        WetterGlobal("wetter_wind", _wetter.Wind);
        // Fahnen: 0,2 + 1,2 · Wind ergibt bei leichtem Wind (0,25) den Grundwert 0,5; mehr als 1,1 ließe sie zu wild schlagen
        WetterGlobal("wind_staerke", Fenster.FahnenBewegen ? Mathf.Min(0.2f + 1.2f * _wetter.Wind, 1.1f) : 0f);
        var himmel = _umgebung.Environment.BackgroundColor.SrgbToLinear();
        var h = new Vector3(himmel.R, himmel.G, himmel.B);
        if (!_wetterGlobal.TryGetValue("wetter_himmel", out var alterHimmel) || (alterHimmel.AsVector3() - h).Length() > 0.004f)
        {
            _wetterGlobal["wetter_himmel"] = h;
            RenderingServer.GlobalShaderParameterSet("wetter_himmel", h);
        }
        if (!_wetterGlobal.TryGetValue("jahreszeit_anteile", out var alteAnteile) || (alteAnteile.AsVector4() - _jahreszeitAnteile).Length() > 0.002f)
        {
            _wetterGlobal["jahreszeit_anteile"] = _jahreszeitAnteile;
            RenderingServer.GlobalShaderParameterSet("jahreszeit_anteile", _jahreszeitAnteile);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Licht, Himmel und Nebel
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die Lichtstimmung mit Wetter: Wolken dämpfen die Sonne (bei voller Bedeckung auf gut ein Viertel) und machen die Schatten weich, Himmel und
    /// Umgebungslicht werden grauer; Nebel hellt den Himmel zu Grau auf; ein Blitz hellt Himmel und Umgebung kurz auf. Bei klarem Wetter bleibt
    /// alles, wie es war.
    /// </summary>
    private Lichtstimmung MitWetter(Lichtstimmung s)
    {
        var bedeckt = Mathf.Clamp((_wetter.Wolken - 0.1f) / 0.9f, 0, 1);
        var nebel = _wetter.Nebel;
        if (bedeckt <= 0.001f && nebel <= 0.001f && _blitz <= 0.001f) return s;
        static Color Grau(Color c, float anteil, float hell)
        {
            var l = c.R * 0.3f + c.G * 0.55f + c.B * 0.15f;
            return c.Lerp(new Color(l * 0.96f, l, l * 1.05f), anteil) * hell;
        }
        var himmel = Grau(s.Himmel, 0.75f * bedeckt, 1 - 0.3f * bedeckt);
        himmel = himmel.Lerp(Grau(s.Himmel, 1, 0.9f + 0.25f * s.Hell), 0.6f * nebel);
        var umgebung = Grau(s.Umgebung, 0.5f * bedeckt, 1);
        himmel = himmel.Lerp(new Color(0.8f, 0.84f, 0.95f), 0.7f * _blitz);
        return s with
        {
            Farbe = s.Farbe.Lerp(new Color(0.86f, 0.89f, 0.93f), 0.5f * bedeckt),
            Staerke = s.Staerke * (1 - 0.72f * bedeckt) * (1 - 0.35f * nebel),
            Schattendeckkraft = s.Schattendeckkraft * (1 - 0.55f * bedeckt) * (1 - 0.5f * nebel),
            Himmel = himmel,
            Umgebung = umgebung,
            Umgebungsstaerke = s.Umgebungsstaerke * (1 + 0.1f * bedeckt) + 0.55f * _blitz,
        };
    }

    /// <summary>
    /// Nebel des Wetters (jedes Bild, nach dem Morgendunst): Godots Entfernungsnebel, auch wenn der Morgendunst aus ist. Regen und Schnee machen
    /// die Luft etwas dunstig. Die Farbe ist die des Himmels dieser Stunde, kühl getönt.
    /// </summary>
    private void WetterNebel()
    {
        var umgebung = _umgebung.Environment;
        var nebel = _raum is null ? Mathf.Max(_wetter.Nebel, 0.12f * _wetter.Regen + 0.15f * _wetter.Schnee) : 0f;
        var dunst = Fenster.AktuellerDunst == Morgendunst.Niedrig;
        var an = dunst || nebel > 0.01f;
        if (umgebung.FogEnabled != an) umgebung.FogEnabled = an;
        var dichte = WetterNebelDichte * nebel;
        if (Mathf.Abs(umgebung.FogDensity - dichte) > 0.00002f) umgebung.FogDensity = dichte;
        if (nebel > 0.01f)
        {
            // Farbe: der Himmel dieser Stunde (bei Nebel schon grau), noch etwas kühler – so geht der Horizont ohne Kante in den Nebel über und
            // Boden und Gebäude verblassen zum selben Ton (nicht zu einer dunklen Farbe, die helle Wände überstrahlen)
            var himmel = umgebung.BackgroundColor;
            var hell = himmel.R * 0.3f + himmel.G * 0.55f + himmel.B * 0.15f;
            var farbe = himmel.Lerp(new Color(hell * 0.92f, hell * 0.99f, hell * 1.08f), 0.7f);
            var alt = umgebung.FogLightColor;
            if (Mathf.Abs(farbe.R - alt.R) + Mathf.Abs(farbe.G - alt.G) + Mathf.Abs(farbe.B - alt.B) > 0.006f) umgebung.FogLightColor = farbe;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Blitz
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bei Gewitter blitzt es alle paar Sekunden (Echtzeit, zufällig – das gehört nicht zum festen Ablauf): 2–4 kurze, helle Pulse in etwa 0,3 s,
    /// dazwischen ein Restschein, dann klingt der Blitz ab. Jeder Blitz kommt aus einer zufälligen Himmelsrichtung und Steilheit; in der Stufe
    /// Hoch wirft er Schatten (sie springen von Blitz zu Blitz), sonst bleibt es bei Licht ohne Schatten. Mit <c>--blitz=1</c> bleibt ein Blitz
    /// auf voller Helligkeit stehen und wechselt jede Sekunde die Richtung (für Bildschirmfotos).
    /// </summary>
    private void BlitzNachfuehren(float delta)
    {
        if (_blitzHalten && _wetter.Blitze > 0.3f)
        {
            _blitz = 1;
            if ((_blitzHaltenZeit -= delta) <= 0) { _blitzHaltenZeit = 1f; BlitzRichtung(); }
        }
        else
        {
            if (_blitzZeit < 0 && _wetter.Blitze > 0.3f && (_naechsterBlitz -= delta) <= 0)
            {
                _blitzZeit = 0;
                _naechsterBlitz = (3f + 9f * (float)_blitzZufall.NextDouble()) / _wetter.Blitze;
                BlitzRichtung();
                _blitzPulse = 2 + _blitzZufall.Next(3);
                _blitzAbstand = 0.3f / _blitzPulse * (0.8f + 0.4f * (float)_blitzZufall.NextDouble());
            }
            if (_blitzZeit >= 0)
            {
                _blitzZeit += delta;
                var b = _blitzZeit;
                var ende = _blitzPulse * _blitzAbstand;
                if (b < ende)
                {
                    // je Pulse die ersten 40 % hell, den Rest ein schwacher Schein
                    var im = (b % _blitzAbstand) / _blitzAbstand;
                    _blitz = im < 0.4f ? (b < _blitzAbstand ? 1f : 0.75f + 0.25f * Mathf.Cos(b * 40f)) : 0.15f;
                }
                else _blitz = Mathf.Max(0, 0.6f * (1 - (b - ende) / 0.35f));
                if (b > ende + 0.35f) { _blitzZeit = -1; _blitz = 0; }
            }
            else _blitz = 0;
        }
        var stufe = Fenster.AktuelleWettereffekte;
        var licht = stufe != Wettereffekte.Niedrig && _raum is null && _blitz > 0.001f;
        if (_blitzLicht.Visible != licht) _blitzLicht.Visible = licht;
        if (!licht) return;
        // Schatten nur in der Stufe Hoch; die Einstellung der Sonne (Bias, Weichzeichner) gilt für dieselbe Kamera, also auch hier
        var schatten = stufe == Wettereffekte.Hoch && _sonne.ShadowEnabled;
        if (_blitzLicht.ShadowEnabled != schatten) _blitzLicht.ShadowEnabled = schatten;
        if (schatten)
        {
            _blitzLicht.ShadowBias = _sonne.ShadowBias;
            _blitzLicht.ShadowNormalBias = _sonne.ShadowNormalBias;
            _blitzLicht.ShadowBlur = _sonne.ShadowBlur;
        }
        _blitzLicht.LightEnergy = (schatten ? 2.4f : 1.5f) * _blitz;
    }

    /// <summary>Neue Richtung für den nächsten Blitz: Himmelsrichtung 0–360°, Steilheit 35–70° über dem Horizont.</summary>
    private void BlitzRichtung()
    {
        var azimut = 360f * (float)_blitzZufall.NextDouble();
        var hoehe = 35f + 35f * (float)_blitzZufall.NextDouble();
        _blitzLicht.RotationDegrees = new Vector3(-hoehe, azimut, 0);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Regen und Schnee
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Wie viele Tropfen und Flocken je Stufe höchstens fallen (die Stärke des Wetters nimmt einen Anteil davon).</summary>
    private static (int Regen, int Schnee) PartikelZahl(Wettereffekte stufe) => stufe switch
    {
        Wettereffekte.Niedrig => (2500, 1200),
        Wettereffekte.Mittel => (7000, 3000),
        _ => (14000, 6000),
    };

    /// <summary>Kleinster Durchmesser (Meter) eines Regenrings am Boden, der größte ist doppelt so groß: feste Größe in der Welt bei jedem Zoom.</summary>
    private const float RingGroesse = 0.26f;

    /// <summary>Zoom, ab dem die Regenringe auszublenden beginnen, und der Zoom, bei dem sie ganz weg sind (sonst wären es Bildpunkte, die flimmern).</summary>
    private const float RingBlassAb = 60f, RingBlassBis = 115f;

    /// <summary>Wie viele Ringe und Spritzer am Boden höchstens zugleich zu sehen sind (Niedrig: keine).</summary>
    private static int RingeZahl(Wettereffekte stufe) => stufe switch { Wettereffekte.Niedrig => 0, Wettereffekte.Mittel => 1200, _ => 3200 };

    /// <summary>
    /// Regen und Schnee fallen in einem Kasten um den Blickpunkt, der mit der Kamera dreht. Kasten, Größe und Fallgeschwindigkeit wachsen mit dem
    /// Zoom (Maßstab k = Zoom / 95): So sind Tropfen und Flocken bei jedem Zoom etwa gleich groß und gleich schnell im Bild, und gleich viele.
    /// </summary>
    private void PartikelNachfuehren()
    {
        var stufe = Fenster.AktuelleWettereffekte;
        if (_partikelStufe != stufe)
        {
            _partikelStufe = stufe;
            var (regen, schnee) = PartikelZahl(stufe);
            _regen.Amount = regen;
            _schnee.Amount = schnee;
            _regenRinge.Amount = Mathf.Max(1, RingeZahl(stufe));
            _schneeStoff.TurbulenceEnabled = stufe == Wettereffekte.Hoch; // Flocken wirbeln im Wind
        }
        var draussen = _raum is null;
        PartikelSchalten(_regen, draussen ? _wetter.Regen : 0);
        PartikelSchalten(_schnee, draussen ? _wetter.Schnee : 0);
        PartikelSchalten(_regenRinge, draussen && RingeZahl(stufe) > 0 ? _wetter.Regen : 0);
        if (!_regen.Visible && !_schnee.Visible) return;

        var k = _kamera.Size / BezugsZoom;
        var bild = GetViewport().GetVisibleRect().Size;
        var seiten = bild.Y > 0 ? bild.X / bild.Y : 16f / 9f;
        var mitte = _kameraDrehung.Position;
        var drehung = _kameraDrehung.Rotation.Y;
        // In Blickrichtung reicht der sichtbare Boden ±Zoom/2 / sin(Neigung); was in der Luft hängt, erscheint weiter vorn – darum vorn mehr Luft.
        var neigung = Mathf.DegToRad(KameraNeigung);
        var breite = _kamera.Size * seiten * 0.55f;
        var tiefe = _kamera.Size * 0.5f / Mathf.Sin(neigung);
        // Kasten, Größe und Geschwindigkeit nur bei merklicher Änderung neu (jede Änderung lädt das Material neu)
        var stand = (Mathf.Snapped(k, 0.02f), Mathf.Snapped(_wetter.Wind, 0.02f), Mathf.Snapped(breite, 1f));
        var neu = _partikelFuer != stand;
        _partikelFuer = stand;
        foreach (var (teil, stoff, hoehe) in new[] { (_regen, _regenStoff, 15f * k), (_schnee, _schneeStoff, 12f * k) })
        {
            var vor = hoehe * 0.5f / Mathf.Tan(neigung); // die Mitte des Kastens liegt so viel näher an der Kamera
            teil.GlobalPosition = mitte + new Vector3(0, hoehe * 0.5f, 0) + new Vector3(Mathf.Sin(drehung), 0, Mathf.Cos(drehung)) * vor;
            teil.Rotation = new Vector3(0, drehung, 0);
            if (!neu) continue;
            stoff.EmissionBoxExtents = new Vector3(breite, hoehe * 0.5f, tiefe + vor);
            teil.VisibilityAabb = new Aabb(new Vector3(-breite, -hoehe, -tiefe - 2 * vor), new Vector3(2 * breite, 2 * hoehe, 2 * (tiefe + 2 * vor)));
        }
        PartikelFarbe();
        if (_regenRinge.Visible) // Ringe: auf dem Boden im Bild, mit dem Kasten mitgedreht
        {
            var boden = BlickpunktAmBoden();
            _regenRinge.GlobalPosition = new Vector3(boden.X, BodenHoehe(boden.X, boden.Z) + 0.06f, boden.Z);
            _regenRinge.Rotation = new Vector3(0, drehung, 0);
            if (neu)
            {
                // Die Ringe behalten ihre Größe in der Welt (0,26–0,52 m, so groß wie bisher im nahen Zoom) und blenden beim Herauszoomen aus, statt mitzuwachsen
                _ringStoff.EmissionBoxExtents = new Vector3(_kamera.Size * seiten * 0.55f, 0.01f, tiefe * 0.8f);
                _ringStoff.ScaleMin = RingGroesse;
                _ringStoff.ScaleMax = RingGroesse * 2f;
                _regenRinge.VisibilityAabb = new Aabb(new Vector3(-breite, -2, -tiefe), new Vector3(2 * breite, 4, 2 * tiefe));
                if (_regenRinge.DrawPass1?.SurfaceGetMaterial(0) is ShaderMaterial ringe) ringe.SetShaderParameter("weit", 1f - Mathf.SmoothStep(RingBlassAb, RingBlassBis, _kamera.Size));
            }
        }
        if (!neu) return;

        // Wind treibt Regen und Schnee schräg; Größe und Geschwindigkeit mit dem Zoom
        var wind = new Vector3(_windRichtung.X, 0, _windRichtung.Y) * _wetter.Wind;
        // Der Regen fällt nach dem Wind: bei Windstille senkrecht, mit der Windstärke zunehmend schräg (quadratisch: leichter Wind kaum,
        // Sturm deutlich). Wind 0,5 (Regen) = 8°, 0,75 (Gewitter) = 18°, 1 (Sturm) = 31°. Die Tropfen der Traufen fallen immer senkrecht (Stadtansicht.Traufen.cs).
        _regenStoff.Direction = (Vector3.Down + wind * (0.6f * _wetter.Wind)).Normalized();
        _regenFarbe.SetShaderParameter("richtung", _regenStoff.Direction);
        _regenStoff.InitialVelocityMin = 14f * k;
        _regenStoff.InitialVelocityMax = 17f * k;
        _regenStoff.ScaleMin = 0.85f * k;
        _regenStoff.ScaleMax = 1.15f * k;
        _regenFarbe.SetShaderParameter("staerke", _wetter.Regen);
        _schneeStoff.Direction = (Vector3.Down + wind * 1.2f).Normalized();
        // langsam fallen; die Flocken wachsen nur mit der Wurzel des Zooms (k hoch 0,6): nah deutlich größer als ein Pixel, weit nicht zu dicht
        _schneeStoff.InitialVelocityMin = 0.8f * k;
        _schneeStoff.InitialVelocityMax = 1.4f * k;
        var gr = Mathf.Pow(k, 0.6f);
        _schneeStoff.ScaleMin = 0.6f * gr;
        _schneeStoff.ScaleMax = 1.5f * gr;
        _schneeFarbe.SetShaderParameter("massstab", k);
        _schneeStoff.TurbulenceNoiseStrength = 0.6f + 2f * _wetter.Wind;
        _schneeStoff.TurbulenceInfluenceMin = 0.05f;
        _schneeStoff.TurbulenceInfluenceMax = 0.12f + 0.15f * _wetter.Wind;
    }

    private float PartikelHelligkeit() => Mathf.Clamp(_umgebung.Environment.AmbientLightEnergy * 1.1f + _sonne.LightEnergy * 0.25f, 0.18f, 1f);

    /// <summary>Tropfen und Flocken sind unbeleuchtet, darum nach dem Licht der Stunde getönt: nachts dunkel, beim Blitz hell.</summary>
    private void PartikelFarbe()
    {
        var hell = PartikelHelligkeit();
        if (Mathf.Abs(hell - _partikelHell) < 0.01f) return;
        _partikelHell = hell;
        _regenFarbe.SetShaderParameter("farbe", new Color(0.78f * hell, 0.82f * hell, 0.9f * hell, 1f));
        if (_regenRinge.ProcessMaterial is not null && _regenRinge.DrawPass1?.SurfaceGetMaterial(0) is ShaderMaterial ring) ring.SetShaderParameter("farbe", new Color(0.8f * hell, 0.85f * hell, 0.95f * hell, 1f));
        _schneeFarbe.SetShaderParameter("farbe", new Color(0.95f * hell, 0.96f * hell, hell, 0.9f));
    }

    /// <summary>Ein Teil (Regen oder Schnee) an oder aus, <paramref name="staerke"/> 0–1 nimmt den Anteil der Tropfen.</summary>
    private static void PartikelSchalten(GpuParticles3D teil, float staerke)
    {
        var an = staerke > 0.02f;
        if (teil.Visible != an)
        {
            teil.Visible = an;
            teil.Emitting = an;
        }
        var anteil = Mathf.Snapped(Mathf.Clamp(staerke * 1.25f, 0.05f, 1f), 0.02f); // nur in Stufen setzen: jede Änderung geht an den RenderingServer
        if (an && Mathf.Abs(teil.AmountRatio - anteil) > 0.001f) teil.AmountRatio = anteil;
    }
}
