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
/// <item>Regen und Schnee: je ein MultiMesh (Ort im Shader) in einem Kasten um den Blickpunkt, mit dem Zoom mitwachsend (Tropfen und Flocken
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
    /// Seegang (Wasser Phase 3d, Nutzer 30.10.2026): So träge folgen die Wellen dem Wind (Zeitkonstante in Sekunden Echtzeit bei Tempo 1; bei
    /// höherem Tempo geteilt durch dessen Wurzel). Aufbau etwa 40 s, Abklingen doppelt so lang; Regen, Schaum und Farbe folgen schneller.
    /// </summary>
    private const float SeegangAufbauSekunden = 40f, SeegangAbklingSekunden = 80f;

    private float _seegang;
    private bool _seegangGesetzt;
    private double _seegangMeldung;
    private float _seegangZielZuletzt = -1;

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

    private MultiMeshInstance3D _regen = null!, _schnee = null!;
    private ShaderMaterial _regenFarbe = null!; // regen.gdshader: Striche in Fallrichtung
    private ShaderMaterial _schneeFarbe = null!; // schnee.gdshader: weiche Flocken, die pendeln und sich drehen
    private DirectionalLight3D _blitzLicht = null!;
    private float _blitzZeit = -1, _naechsterBlitz = 3;

    /// <summary>
    /// Abstand des laufenden (oder letzten) Blitzes in Kilometern und seine Helligkeit (0–1). EIN Wurf je Blitz (<see cref="KlangUmgebungModell.BlitzAbstandWuerfeln"/>):
    /// Die Helligkeit (<see cref="KlangUmgebungModell.BlitzHelligkeit"/>) skaliert Himmel, Umgebungslicht und Blitzlicht, der Ton liest denselben Abstand
    /// (<see cref="KlangBlitzAbstandKm"/>) für Verzögerung (343 m/s), Pegel und Klang des Donners – Bild und Ton stimmen so überein.
    /// </summary>
    private float _blitzAbstandKm = 1f, _blitzStaerke = 1f;
    private float? _gewitterVorgabe;

    /// <summary>Gesetzt, wenn der laufende Blitz ein direkter Einschlag ist (<see cref="BlitzEinschlag"/>): Abstand zum Blickpunkt in km und Seite im Bild (−1…+1); beim nächsten normalen Blitz wieder null.</summary>
    private (float Km, float Seite)? _einschlag;
    private float _blitz;
    private readonly Random _blitzZufall = new();
    private int _blitzPulse;
    private float _blitzAbstand, _blitzHaltenZeit;
    private Wettereffekte? _partikelStufe;
    // Niederschlag ohne Teilchen-Zustand (Shader rechnen den Ort jeder Flocke aus Nummer, Versatz und Fallweg): Versatz als Bruchteil des Kastens, umlaufend
    private Vector3 _regenVersatz, _schneeVersatz;
    private float _schneeFall; // in Kastenhöhen, umlaufend 0–20 (schnee.gdshader)
    private Vector3? _niederschlagLetzteMitte;
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

    /// <summary>
    /// Startoption <c>--naesse=0.6</c> (nur Darstellung, für Bildschirmfotos): Die Nässe von Boden, Dächern und Wänden steht fest auf diesem Wert, egal
    /// welches Wetter ist – so lässt sich „Sonne nach dem Regen“ (fleckig abtrocknende Dächer) mit <c>--wetter=klar</c> zeigen.
    /// </summary>
    private readonly float? _naesseVorgabe = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--naesse=")) is { } n
        && float.TryParse(n["--naesse=".Length..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w)
        ? Mathf.Clamp(w, 0f, 1f) : null;

    /// <summary>
    /// Shader vorwärmen hinter dem Ladebildschirm (Hauptszene.ErsteBilderAbwarten): <paramref name="probe"/> zeigt diese Wetterlage sofort und ganz,
    /// null kehrt zum Wetter davor zurück (Vorgabe der Startoptionen oder Wetter der Partie, ebenfalls sofort, ohne die Nässe der Probe).
    /// </summary>
    public void WetterProbe(Wetterlage? probe)
    {
        if (probe is { } lage)
        {
            _wetterVorProbe ??= (_wetterVorgabe, _jahreszeitVorgabe, _blitzHalten);
            WetterVorgeben(lage, _wetterVorProbe.Value.Jahreszeit, false);
            return;
        }
        if (_wetterVorProbe is not { } vorher) return;
        _wetterVorProbe = null;
        WetterVorgeben(vorher.Lage, vorher.Jahreszeit, vorher.Blitz);
        _naesseMinute = -1; // die Nässe wieder aus dem Wetter der Partie, nicht aus der Probe
    }

    private (Wetterlage? Lage, Jahreszeit? Jahreszeit, bool Blitz)? _wetterVorProbe;

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
    private Jahreszeit GezeigteJahreszeit(Jahreszeit uhr) => _jahreszeitVorgabe ?? (WinterAusStartoption ? Jahreszeit.Winter : uhr); // --winter > 0 zeigt den Winter (Stadtansicht.Wasser.cs)

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Einmal beim Aufbau (aus <see cref="GrafikEinrichten"/>): Regen, Schnee und das Licht der Blitze.</summary>
    private void WetterEinrichten()
    {
        // Regen: Tropfen als Striche in Fallrichtung (regen.gdshader), unterschiedlich lang und durchscheinend, der Kopf heller als der Schweif.
        // Kein Teilchensystem: Ein MultiMesh aus Quads, deren Ort der Shader aus Nummer, Kasten und Versatz rechnet (siehe PartikelNachfuehren);
        // so ist der Kasten bei jedem Zoom und nach jedem schnellen Schwenk sofort gleichmäßig gefüllt (Teilchen mit Lebensdauer blieben als Inseln liegen).
        _regenFarbe = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/regen.gdshader") };
        _regen = NiederschlagBauen("Regen", _regenFarbe, Vector2.One);

        // Ringe der Regentropfen am Boden zeichnen Boden und Modelle selbst (regentropfen in wetter.gdshaderinc, Dichte über wetter_ringe): Die Ringe als
        // Partikel lagen auf einer Ebene in Höhe der Bildmitte und verschwanden, wo das Gelände höher lag, hinter einer geraden Linie (Regen 4).

        // Schnee: weiche, runde Flocken, die wie Federn schwingend fallen (schnee.gdshader)
        _schneeFarbe = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/schnee.gdshader") };
        _schnee = NiederschlagBauen("Schnee", _schneeFarbe, Vector2.One);

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

    /// <summary>Ein Niederschlags-MultiMesh: nur Quads, die Lage kommt aus dem Shader (INSTANCE_ID), darum kein Platzhalter-Transform und keine Sichtbarkeitsprüfung.</summary>
    private MultiMeshInstance3D NiederschlagBauen(string name, ShaderMaterial stoff, Vector2 groesse)
    {
        var teil = new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = new QuadMesh { Size = groesse, Material = stoff },
                InstanceCount = 1,
            },
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            CustomAabb = new Aabb(new Vector3(-4000, -4000, -4000), new Vector3(8000, 8000, 8000)), // der Shader setzt die Orte, Godot kennt sie nicht
        };
        AddChild(teil);
        return teil;
    }

    /// <summary>Beim Verlassen (aus <c>_ExitTree</c>): Die globalen Werte gelten für das ganze Spiel – das Hauptmenü soll wieder Sommer und trocken zeigen.</summary>
    private void WetterAufgeben()
    {
        foreach (var (name, wert) in new (string, Variant)[]
                 {
                     ("wetter_naesse", 0f), ("wetter_regen", 0f), ("wetter_ringe", 0f), ("wetter_rinn", 0f), ("wetter_schnee", 0f), ("wetter_dach", 0f), ("wetter_flaechen", 1f),
                     ("wetter_wind", 0.25f), ("wellen_seegang", 0.25f), ("jahreszeit_anteile", new Vector4(0, 1, 0, 0)), ("traufe_feld", Vector4.Zero),
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
        if (_naesseVorgabe is { } feste) _naesseZiel = feste;
        else if (_wetterVorgabe is not null) _naesseZiel = Mathf.Min(1, ziel.Regen * 1.6f);
        else if ((long)minute != _naesseMinute && ablauf is not null)
        {
            _naesseMinute = (long)minute;
            _naesseZiel = ablauf.Naesse(minute);
        }
        // Schneedecke: im Winter immer, bei Schneefall dichter; im Eisgang am Winterende schmilzt sie mit dem Eis (Wasser Phase 3d, Nutzer
        // 30.10.2026: im Frühling kein Schnee mehr); schneit es (Vorgabe) außerhalb des Winters, bleibt er auch liegen
        var winter = GezeigteJahreszeit(jahreszeit) == Jahreszeit.Winter;
        var tauen = winter ? WinterJetzt().Tauen : 0f;
        var deckeZiel = Mathf.Max(winter ? (0.62f + 0.45f * ziel.Schnee) * (1f - Mathf.SmoothStep(0.15f, 0.9f, tauen)) : 0f,
            ziel.Schnee * 0.9f * (1f - Mathf.SmoothStep(0.6f, 1f, tauen)));
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
        if (_naesseZiel <= 0f && _naesse < 0.002f) _naesse = 0f; // ganz trocken statt eines Rests, der nie ankommt
        _schneeDecke = Mathf.Lerp(_schneeDecke, deckeZiel, t);
        _jahreszeitAnteile = _jahreszeitAnteile.Lerp(anteileZiel, t);

        SeegangNachfuehren(ziel.Wind, delta);
        BlitzNachfuehren((float)delta);
        WetterGlobaleSetzen();
        PartikelNachfuehren();
        TraufenStellen(delta); // Stadtansicht.Traufen.cs
        PflasterReliefStellen(); // Stadtansicht.Pflasterrelief.cs
    }

    /// <summary>
    /// Der Seegang folgt dem Wind des Wetters träge (<see cref="SeegangAufbauSekunden"/>): Ein Sturm baut die Wellen über Sekunden auf, statt sie
    /// umspringen zu lassen; beim ersten Bild steht er sofort. Im Übergang alle 5 s eine Zeile ins Protokoll (für Messungen des Wetterwechsels).
    /// </summary>
    private void SeegangNachfuehren(float windZiel, double delta)
    {
        if (!_seegangGesetzt)
        {
            _seegang = _seegangZielZuletzt = windZiel;
            _seegangGesetzt = true;
            return;
        }
        if (Mathf.Abs(windZiel - _seegangZielZuletzt) > 0.05f)
        {
            GD.Print($"[wetter] {Time.GetTicksMsec() / 1000.0:0.0} s: Wind springt {_seegangZielZuletzt:0.00} → {windZiel:0.00}, Seegang folgt träge");
            _seegangZielZuletzt = windZiel;
            _seegangMeldung = 5.0;
        }
        var tempo = Mathf.Max(1f, (float)(_simulation?.Takt.Tempo ?? 1.0));
        var tau = (windZiel > _seegang ? SeegangAufbauSekunden : SeegangAbklingSekunden) / Mathf.Sqrt(tempo);
        _seegang = Mathf.Lerp(_seegang, windZiel, 1f - Mathf.Exp(-(float)delta / tau));
        _seegangMeldung += delta;
        if (Mathf.Abs(_seegang - windZiel) > 0.01f && _seegangMeldung >= 5.0)
        {
            _seegangMeldung = 0;
            GD.Print($"[wetter] {Time.GetTicksMsec() / 1000.0:0.0} s: Seegang {_seegang:0.000} (Wind {windZiel:0.00}, gezeigter Wind {_wetter.Wind:0.00})");
        }
    }

    private void WetterGlobal(string name, float wert, float schwelle = 0.002f)
    {
        // Nur merkliche Änderungen senden – aber 0 immer: Sonst bliebe nach dem Abtrocknen ein Rest unter der Schwelle stehen, und alles, was „nass“
        // nur bei mehr als 0 abschaltet, bliebe nass (Regen 4: Rinnen und Fugen der Dächer)
        if (_wetterGlobal.TryGetValue(name, out var alt) && Mathf.Abs(alt.AsSingle() - wert) < schwelle && (wert != 0f || alt.AsSingle() == 0f)) return;
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
        // Regentropfen auf Boden und flachen Flächen (wetter.gdshaderinc, regentropfen): Hoch so dicht wie der Regen, Mittel gut die Hälfte, Niedrig keine
        WetterGlobal("wetter_ringe", !draussen ? 0 : stufe switch { Wettereffekte.Hoch => _wetter.Regen, Wettereffekte.Mittel => 0.55f * _wetter.Regen, _ => 0 });
        // Rinnsale auf Dächern und Wänden: ab Mittel; bei Regen stark, danach laufen sie mit dem Abtrocknen (Nässe) langsam aus
        var rinn = Mathf.SmoothStep(0.3f, 0.9f, _naesse) * Mathf.Clamp(0.15f + 1.1f * _wetter.Regen, 0, 1);
        WetterGlobal("wetter_rinn", stufe == Wettereffekte.Niedrig || Mathf.IsZeroApprox(_naesse) ? 0 : rinn * (stufe == Wettereffekte.Hoch ? 1f : 0.6f));
        WetterGlobal("wetter_schnee", _schneeDecke);
        WetterGlobal("wetter_dach", _schneeDecke);
        WetterGlobal("wetter_flaechen", draussen ? 1 : 0);
        WetterGlobal("wetter_wind", _wetter.Wind);
        WetterGlobal("wellen_seegang", _seegang, 0.0005f);
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
        var dunst = DunstWirksam == Morgendunst.Niedrig; // Höhennebel des Morgendunsts (Hoch ohne Volumennebel unter OpenGL ebenso, Stadtansicht.Grafik.cs)
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
                var fortschritt = KlangGewitterFortschritt;
                _naechsterBlitz = (3f + 9f * (float)_blitzZufall.NextDouble()) / _wetter.Blitze * (fortschritt < 0 ? 1f : 1f + 1.2f * Mathf.Abs(2f * fortschritt - 1f)); // über der Stadt dicht, am Rand seltener
                _einschlag = null;
                BlitzAbstandWuerfeln(fortschritt);
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
                    _blitz = _blitzStaerke * (im < 0.4f ? (b < _blitzAbstand ? 1f : 0.75f + 0.25f * Mathf.Cos(b * 40f)) : 0.15f);
                }
                else _blitz = _blitzStaerke * Mathf.Max(0, 0.6f * (1 - (b - ende) / 0.35f));
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

    /// <summary>Würfelt den Abstand des neuen Blitzes (eine gemeinsame Quelle für Bild und Ton) und setzt seine Helligkeit.</summary>
    private void BlitzAbstandWuerfeln(float fortschritt)
    {
        var km = KlangUmgebungModell.BlitzAbstandWuerfeln(_blitzZufall, fortschritt);
        _blitzAbstandKm = (float)km;
        _blitzStaerke = KlangUmgebungModell.BlitzHelligkeit(km);
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

    /// <summary>
    /// Regen und Schnee füllen einen Kasten um den Blickpunkt, der mit der Kamera dreht. Kasten, Größe und Fallgeschwindigkeit wachsen mit dem
    /// Zoom (Maßstab k = Zoom / 95): So sind Tropfen und Flocken bei jedem Zoom etwa gleich groß und gleich schnell im Bild, und gleich viele.
    /// Die Lage jeder Flocke rechnet der Shader aus ihrer Nummer als Bruchteil des Kastens (umlaufend): Jedes Bild gibt der Kasten neu (Mitte, Größe,
    /// Drehung), die Kamerabewegung schiebt den Versatz mit (die Flocken bleiben in der Welt), Wind und Fall schieben ihn weiter. Es gibt keine
    /// Lebensdauer und keinen Zustand, der nach schnellem Zoomen oder Schwenken fleckig zurückbliebe.
    /// </summary>
    private void PartikelNachfuehren()
    {
        var stufe = Fenster.AktuelleWettereffekte;
        if (_partikelStufe != stufe)
        {
            _partikelStufe = stufe;
            var (regen, schnee) = PartikelZahl(stufe);
            _regen.Multimesh.InstanceCount = regen;
            _schnee.Multimesh.InstanceCount = schnee;
            _regen.Multimesh.VisibleInstanceCount = -1;
            _schnee.Multimesh.VisibleInstanceCount = -1;
            _anteilRegen = _anteilSchnee = -1;
        }
        var draussen = _raum is null;
        PartikelSchalten(_regen, ref _anteilRegen, draussen ? _wetter.Regen : 0);
        PartikelSchalten(_schnee, ref _anteilSchnee, draussen ? _wetter.Schnee : 0);
        if (!_regen.Visible && !_schnee.Visible) { _niederschlagLetzteMitte = null; return; }

        var dt = (float)GetProcessDeltaTime();
        var k = _kamera.Size / BezugsZoom;
        var bild = GetViewport().GetVisibleRect().Size;
        var seiten = bild.Y > 0 ? bild.X / bild.Y : 16f / 9f;
        var mitte = _kameraDrehung.Position;
        var drehung = _kameraDrehung.Rotation.Y;
        // In Blickrichtung reicht der sichtbare Boden ±Zoom/2 / sin(Neigung); was in der Luft hängt, erscheint weiter vorn – darum vorn mehr Luft.
        var neigung = Mathf.DegToRad(KameraNeigung);
        var breite = _kamera.Size * seiten * 0.55f;
        var tiefe = _kamera.Size * 0.5f / Mathf.Sin(neigung);
        var (cd, sd) = (Mathf.Cos(drehung), Mathf.Sin(drehung));
        // Welt -> Kasten (Drehung um die Hochachse zurück)
        Vector3 InKasten(Vector3 v) => new(cd * v.X - sd * v.Z, v.Y, sd * v.X + cd * v.Z);

        // Wind treibt Regen und Schnee schräg (Schräge je Wetterart: Regen (Wind 0,5) senkrecht, Gewitter (0,75) und Sturm (1) gleich leicht schräg (18°),
        // dazwischen weich – Nutzer 04.11.2026; der Wind selbst bleibt unverändert: Fahnen, Gras, Bäume, Wolken, Seegang. Traufentropfen fallen immer senkrecht.)
        var wind = new Vector3(_windRichtung.X, 0, _windRichtung.Y) * _wetter.Wind;
        var windRichtung = wind.LengthSquared() > 1e-6f ? wind.Normalized() : Vector3.Zero;
        var schraege = Mathf.Clamp((_wetter.Wind - 0.5f) / 0.25f, 0f, 1f) * 0.325f; // tan 18°
        var regenRichtung = (Vector3.Down + windRichtung * schraege).Normalized();
        var schneeRichtung = (Vector3.Down + wind * 0.4f).Normalized();

        // Kamerabewegung (Blickpunkt) in den Kasten legen: Die Flocken bleiben in der Welt, der Kasten wandert über sie. Nach Sprüngen (Neuaufbau) gilt das
        // ebenso – umlaufend ist jede Lage gleich gut gefüllt.
        var bewegung = _niederschlagLetzteMitte is { } alt ? InKasten(mitte - alt) : Vector3.Zero;
        _niederschlagLetzteMitte = mitte;

        // Regen: Kasten 15 k hoch, Fall 14–17 k je Sekunde (eine Kastenhöhe je Sekunde); die Tropfen laufen mit dem Wind schräg
        var rHoehe = 15f * k;
        var rGanz = new Vector3(2 * breite, rHoehe, 2 * (tiefe + rHoehe * 0.5f / Mathf.Tan(neigung)));
        var rTempo = 15.5f * k; // m/s
        var rWeg = InKasten(regenRichtung) * rTempo * dt; // Meter in dieser Zeit
        _regenVersatz = Umlauf(_regenVersatz + new Vector3((rWeg.X - bewegung.X) / rGanz.X, (rWeg.Y - bewegung.Y) / rGanz.Y, (rWeg.Z - bewegung.Z) / rGanz.Z));
        NiederschlagKasten(_regen, _regenFarbe, mitte, rHoehe, neigung, drehung, rGanz, _regenVersatz, k);
        _regenFarbe.SetShaderParameter("richtung", regenRichtung);
        _regenFarbe.SetShaderParameter("staerke", _wetter.Regen);

        // Schnee: Kasten 12 k hoch, Fall 2–3 k je Sekunde (stetig sichtbar, Nutzer 04.11.2026); Flocken wachsen nur mit k hoch 0,6 (nah deutlich größer als
        // ein Bildpunkt, weit nicht zu dicht). Der Fall läuft über den Fallweg (je Flocke mit eigenem Faktor), nur Wind und Kamera über den Versatz.
        var sHoehe = 12f * k;
        var sGanz = new Vector3(2 * breite, sHoehe, 2 * (tiefe + sHoehe * 0.5f / Mathf.Tan(neigung)));
        var sTempo = 2.5f * k;
        var sWeg = InKasten(schneeRichtung) * sTempo * dt;
        _schneeVersatz = Umlauf(_schneeVersatz + new Vector3((sWeg.X - bewegung.X) / sGanz.X, -bewegung.Y / sGanz.Y, (sWeg.Z - bewegung.Z) / sGanz.Z));
        _schneeFall = Mathf.PosMod(_schneeFall + sTempo * dt / sHoehe, 20f);
        NiederschlagKasten(_schnee, _schneeFarbe, mitte, sHoehe, neigung, drehung, sGanz, _schneeVersatz, k);
        _schneeFarbe.SetShaderParameter("fall", _schneeFall);
        _schneeFarbe.SetShaderParameter("groesse", 0.5f * Mathf.Pow(k, 0.6f) * 1.2f);
        PartikelFarbe();
    }

    private static Vector3 Umlauf(Vector3 v) => new(Mathf.PosMod(v.X, 1f), Mathf.PosMod(v.Y, 1f), Mathf.PosMod(v.Z, 1f));

    /// <summary>Kasten eines Niederschlags an Shader und Knoten: Mitte (die Hälfte der Höhe über dem Blickpunkt, vorn mehr), Drehung, Kantenlängen, Versatz.</summary>
    private static void NiederschlagKasten(MultiMeshInstance3D teil, ShaderMaterial stoff, Vector3 blick, float hoehe, float neigung, float drehung, Vector3 ganz, Vector3 versatz, float k)
    {
        var vor = hoehe * 0.5f / Mathf.Tan(neigung); // die Mitte des Kastens liegt so viel näher an der Kamera
        var mitte = blick + new Vector3(0, hoehe * 0.5f, 0) + new Vector3(Mathf.Sin(drehung), 0, Mathf.Cos(drehung)) * vor;
        stoff.SetShaderParameter("mitte", mitte);
        stoff.SetShaderParameter("drehung", drehung);
        stoff.SetShaderParameter("ganz", ganz);
        stoff.SetShaderParameter("versatz", versatz);
        stoff.SetShaderParameter("massstab", k);
    }

    private float PartikelHelligkeit() => Mathf.Clamp(_umgebung.Environment.AmbientLightEnergy * 1.1f + _sonne.LightEnergy * 0.25f, 0.18f, 1f);

    /// <summary>Tropfen und Flocken sind unbeleuchtet, darum nach dem Licht der Stunde getönt: nachts dunkel, beim Blitz hell.</summary>
    private void PartikelFarbe()
    {
        var hell = PartikelHelligkeit();
        if (Mathf.Abs(hell - _partikelHell) < 0.01f) return;
        _partikelHell = hell;
        _regenFarbe.SetShaderParameter("farbe", new Color(0.78f * hell, 0.82f * hell, 0.9f * hell, 1f));
        _schneeFarbe.SetShaderParameter("farbe", new Color(0.95f * hell, 0.96f * hell, hell, 0.9f));
    }

    private float _anteilRegen = -1, _anteilSchnee = -1;

    /// <summary>Ein Niederschlag (Regen oder Schnee) an oder aus, <paramref name="staerke"/> 0–1 nimmt den Anteil der Tropfen.</summary>
    private static void PartikelSchalten(MultiMeshInstance3D teil, ref float anteilAlt, float staerke)
    {
        var an = staerke > 0.02f;
        if (teil.Visible != an) teil.Visible = an;
        var anteil = Mathf.Snapped(Mathf.Clamp(staerke * 1.25f, 0.05f, 1f), 0.02f); // nur in Stufen setzen: jede Änderung geht an den RenderingServer
        if (!an || Mathf.Abs(anteilAlt - anteil) <= 0.001f) return;
        anteilAlt = anteil;
        teil.Multimesh.VisibleInstanceCount = Mathf.Max(1, (int)(teil.Multimesh.InstanceCount * anteil));
    }
}
