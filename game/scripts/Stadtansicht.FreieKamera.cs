using System.Globalization;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Freie Kamera auf der Spielkarte – ein Teil der <c>partial class Stadtansicht</c> (Startoption <c>--freiekamera=1</c>, Nutzer 30.10.2026). Eine
/// <see cref="FreieKamera"/> ersetzt ab Start die schräge Spielkamera: frei fliegen, umsehen, alles aus der Nähe ansehen. Die Spielkamera bleibt im Baum
/// (nicht aktiv), denn Gras, Feinheiten, Passanten, Regen und Lichter richten sich nach ihr; sie folgt dem Punkt, auf den die freie Kamera blickt:
/// <list type="bullet">
/// <item><b>Start</b>: Solange niemand die freie Kamera bewegt hat, übernimmt sie die Ansicht der Spielkamera (Karte, <c>--blick</c>, Kamerafahrten) – mit
/// gleicher Richtung und so weit weg, dass sie etwa dasselbe zeigt. <c>--freiekamera-start=x,y,z:zx,zy,zz</c> setzt Ort und Blickziel fest (Bildschirmfotos).</item>
/// <item><b>Danach</b> folgt die Spielkamera: Blickpunkt = wo der Blick der freien Kamera den Boden trifft (flach geblickt: 60 m voraus), Drehung = ihre
/// Richtung, Zoom = was sie dort etwa sieht.</item>
/// <item><b>Schatten</b>: Die Sonne nimmt für die perspektivische Kamera vier Stufen bis <see cref="FreiSchattenWeite"/> m (sonst eine, auf den Ausschnitt der
/// Spielkamera zugeschnitten).</item>
/// <item><b>Dunst</b>: Der Morgendunst in der Luft (Stufe Hoch) reicht nur so weit wie Godots Nebelvolumen; es wächst mit der Höhe der Kamera, damit hoch
/// oben kein Loch mit harter Kante entsteht. Der Dunst am Kartenrand hat die Farbe des Hintergrunds – auch von hoch oben keine Kante.</item>
/// </list>
/// Die eigene Steuerung der Spielkamera (Mausrad, rechte Maustaste, W/A/S/D, Q/E) ruht, solange die freie Kamera aktiv ist. H öffnet auf der Spielkarte das
/// Hinterzimmer, deshalb blendet hier F2 die Hilfezeile aus.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Weite der Schatten (m) mit der freien Kamera: vier Stufen, nahe scharf, bis hierhin.</summary>
    private const float FreiSchattenWeite = 400f;

    /// <summary>Flach geblickt (Neigung über −8°): so weit voraus (m) steht der Blickpunkt der Spielkamera.</summary>
    private const float FreiBlickVoraus = 60f;

    private FreieKamera? _freieKamera;
    private bool _freiGemeldet;

    /// <summary>Hat <c>--freiekamera-start</c> den Startblick festgelegt (dann übernimmt die freie Kamera nie den Blick der Spielkamera)?</summary>
    private bool _freiStartFest;

    /// <summary>Startoption <c>--freiekamera=1</c>.</summary>
    private static bool FreieKameraGewuenscht => Wasserarten.Startoption("freiekamera") is "1" or "an" or "ja";

    /// <summary>Die Kamera, durch die man die Stadt gerade sieht (für Klicks auf Gebäude, Boden, Figuren): die freie, wenn sie aktiv ist.</summary>
    private Camera3D StadtKamera => _freieKamera is { Current: true } frei ? frei : _kamera;

    /// <summary>Ist die freie Kamera aktiv (und die eigene Steuerung der Spielkamera ruht)?</summary>
    private bool FreiAktiv => _freieKamera is { Current: true };

    /// <summary>Beim Aufbau (nach der Spielkamera): mit <c>--freiekamera=1</c> die freie Kamera einhängen und aktiv machen.</summary>
    private void FreieKameraEinrichten()
    {
        if (!FreieKameraGewuenscht) return;
        _freieKamera = new FreieKamera { HilfeTaste = Key.F2, Bodenhoehe = (x, z) => BodenHoehe(x, z) };
        AddChild(_freieKamera);
        _freieKamera.MakeCurrent();
        if (StartPose() is var (ort, ziel))
        {
            _freieKamera.Blicken(ort, ziel);
            _freiStartFest = true;
        }
        else FreiVonSpielkamera();
        GD.Print("[freiekamera] aktiv (F2: Hilfe aus)");
    }

    /// <summary><c>--freiekamera-start=x,y,z:zx,zy,zz</c> – Ort und Blickziel der freien Kamera (null ohne die Option).</summary>
    private static (Vector3, Vector3)? StartPose()
    {
        if (Wasserarten.Startoption("freiekamera-start") is not { } wert) return null;
        var teile = wert.Split(':');
        if (teile.Length != 2 || Punkt(teile[0]) is not { } ort || Punkt(teile[1]) is not { } ziel) return null;
        return (ort, ziel);

        static Vector3? Punkt(string text)
        {
            var z = text.Split(',');
            if (z.Length != 3) return null;
            var w = new float[3];
            for (var i = 0; i < 3; i++)
                if (!float.TryParse(z[i], NumberStyles.Float, CultureInfo.InvariantCulture, out w[i])) return null;
            return new Vector3(w[0], w[1], w[2]);
        }
    }

    /// <summary>
    /// Jedes Bild (aus <c>_Process</c>): Vor dem ersten Steuern übernimmt die freie Kamera die Ansicht der Spielkamera, danach folgt die Spielkamera ihr.
    /// Dazu Schatten und Dunst für die perspektivische Kamera. Gibt true zurück, wenn die freie Kamera aktiv ist (dann ruht die eigene Steuerung).
    /// </summary>
    private bool FreieKameraBild()
    {
        if (!FreiAktiv || _freieKamera is not { } frei) return false;
        if (!frei.Gesteuert && !_freiStartFest) FreiVonSpielkamera();
        else SpielkameraFolgt(frei);
        if (!_freiGemeldet && _gelaende is not null && Engine.GetProcessFrames() % 120 == 0)
        {
            // Für Klicktests: wo die Kamera steht und wie hoch der Boden darunter ist (für --freiekamera-start)
            _freiGemeldet = true;
            var o = frei.GlobalPosition;
            GD.Print($"[freiekamera] Ort {o.X:0.0},{o.Y:0.0},{o.Z:0.0}, Boden darunter {BodenHoehe(o.X, o.Z):0.0}, Blickpunkt {_kameraDrehung.Position.X:0},{_kameraDrehung.Position.Z:0} (Boden {BodenHoehe(_kameraDrehung.Position.X, _kameraDrehung.Position.Z):0.0})");
        }
        FreiSchatten();
        FreiNebel(frei);
        return true;
    }

    /// <summary>Die freie Kamera blickt aus der Richtung der Spielkamera auf ihren Blickpunkt, so weit weg, dass sie etwa dasselbe zeigt.</summary>
    private void FreiVonSpielkamera()
    {
        if (_freieKamera is not { } frei) return;
        var ziel = _kameraDrehung.GlobalPosition + new Vector3(0, _kameraNeigung?.Position.Y ?? 0, 0);
        var zurKamera = _kamera.GlobalTransform.Basis.Z.Normalized();
        var abstand = _kamera.Size / (2f * Mathf.Tan(Mathf.DegToRad(frei.Fov) / 2f));
        frei.Blicken(ziel + zurKamera * abstand, ziel);
    }

    /// <summary>Blickpunkt, Drehung und Zoom der Spielkamera nach der freien (für Gras, Feinheiten, Passanten, Regen und Lichter).</summary>
    private void SpielkameraFolgt(FreieKamera frei)
    {
        var ort = frei.GlobalPosition;
        var blick = -frei.GlobalTransform.Basis.Z;
        var boden = BodenBewegt ? BodenHoehe(ort.X, ort.Z) : 0f;
        Vector3 ziel;
        float abstand;
        if (blick.Y < -0.14f)
        {
            abstand = Mathf.Min((ort.Y - boden) / -blick.Y, 2000f);
            ziel = ort + blick * abstand;
        }
        else
        {
            var flach = new Vector3(blick.X, 0, blick.Z).Normalized();
            abstand = FreiBlickVoraus;
            ziel = ort + flach * abstand;
        }
        _kamerafahrt?.Kill();
        _kameraDrehung.Position = new Vector3(ziel.X, 0, ziel.Z);
        _zielDrehung = frei.Gier;
        _kameraDrehung.RotationDegrees = new Vector3(0, frei.Gier, 0);
        var zoom = 2f * Mathf.Max(abstand, 5f) * Mathf.Tan(Mathf.DegToRad(frei.Fov) / 2f);
        _kamera.Size = Mathf.Clamp(zoom, KameraMinZoom, KameraMaxZoom);
    }

    /// <summary>
    /// Schatten der Sonne für die perspektivische Kamera: vier Stufen bis <see cref="FreiSchattenWeite"/> m, weich ineinander übergeblendet, ausblenden auf
    /// den letzten 20 %. Godots übliche Werte für Bias und Weichzeichner (die der Spielkamera sind auf ihren Ausschnitt gerechnet).
    /// </summary>
    private void FreiSchatten()
    {
        if (_sonne.DirectionalShadowMode == DirectionalLight3D.ShadowMode.Parallel4Splits && Mathf.IsEqualApprox(_sonne.ShadowBias, 0.06f)) return;
        _sonne.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits;
        _sonne.DirectionalShadowMaxDistance = FreiSchattenWeite;
        _sonne.DirectionalShadowBlendSplits = true;
        _sonne.DirectionalShadowFadeStart = 0.8f;
        _sonne.ShadowBias = 0.06f;
        _sonne.ShadowNormalBias = 1.5f;
        _sonne.ShadowBlur = 1f;
    }

    /// <summary>
    /// Der Morgendunst in der Luft (Stufe Hoch) reicht nur <c>VolumetricFogLength</c> weit; die Spielkamera steht immer 150 m vor der Stadt (280 m genügen).
    /// Fliegt die freie Kamera hoch, wächst die Weite mit (dreimal die Höhe über dem Boden und 300 m, höchstens 1.500 m), sonst endete der Dunst am Boden
    /// mit einer harten Kante. Die Schichten liegen dann näher an der Kamera dichter (<c>DetailSpread</c> 2).
    /// </summary>
    private void FreiNebel(FreieKamera frei)
    {
        var umgebung = _umgebung.Environment;
        if (!umgebung.VolumetricFogEnabled) return;
        var hoehe = frei.GlobalPosition.Y - (BodenBewegt ? BodenHoehe(frei.GlobalPosition.X, frei.GlobalPosition.Z) : 0f);
        var laenge = Mathf.Clamp(hoehe * 3f + 300f, 280f, 1500f);
        if (Mathf.Abs(laenge - umgebung.VolumetricFogLength) < 25f && Mathf.IsEqualApprox(umgebung.VolumetricFogDetailSpread, 2f)) return;
        umgebung.VolumetricFogLength = laenge;
        umgebung.VolumetricFogDetailSpread = 2f;
    }
}
