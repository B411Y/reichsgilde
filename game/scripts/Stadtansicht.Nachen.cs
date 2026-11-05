using Godot;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Nachen (Teil der <c>partial class Stadtansicht</c>): Zwei kleine Boote (<c>wasser/wasser_nachen</c>, 5 × 1,3 m) beim Hafensteg des ersten Hafens, reine Zierde ohne Wirkung im Kern.
///
/// Der erste liegt immer festgemacht neben dem Zugang des Stegs (Stange liegt im Boot, kein Schiffer). Der zweite liegt daneben und pendelt tagsüber (8–20 Uhr)
/// langsam am Ufer entlang: hin und zurück auf einer geraden Bahn parallel zur Uferkante, auf der Seite des Stegs, die vom Zierschiff abgewandt ist, so dass er
/// weder durch den Steg noch durch das Schiff fährt; ein Schiffer steht achtern und staket. Die Bahn gibt es nur, wo wirklich Wasser ist (Prüfung in Schritten
/// von 2 m); ist sie kürzer als 12 m, bleibt er festgemacht. Bei geschlossenem Hafen (Winter) und nachts fährt er noch heim und bleibt dann liegen.
/// Die Bahn ist Darstellung (Wasserzeit, nicht die Spielminute) und steht nicht im Spielstand.
/// </summary>
public partial class Stadtansicht
{
    private const float NachenAbstand = 3.5f, NachenLiege1 = -4.5f, NachenLiege2 = -10f, NachenTempo = 0.6f, NachenPauseMeter = 3f;

    private SchiffAnsicht? _nachenFest, _nachenPendler;
    private string? _nachenHafen;
    private float _nachenBahn;      // Länge der Bahn (m) vom zweiten Liegeplatz weg, 0 = keine
    private bool _nachenFestOk;
    private float _nachenAbstandGemessen = NachenAbstand;
    private double _nachenWeg;
    private int _nachenVersuch;

    /// <summary>Punkt am Steg: <paramref name="s"/> Meter in Stegrichtung (+X des Steg-Modells, zum Kran hin), <paramref name="d"/> Meter vor der Uferkante ins Wasser.</summary>
    private static Vector3 NachenPunkt(Hafenplatz hafen, float s, float d)
    {
        var nach = new Vector3((float)hafen.RichtungX, 0, (float)hafen.RichtungZ).Normalized();
        var t = new Vector3(-nach.Z, 0, nach.X);
        return HafenKante(hafen) + t * s + nach * d;
    }

    private bool NachenWasser(Hafenplatz hafen, float s, float d) => SpiegelMitBecken(NachenPunkt(hafen, s, d).X, NachenPunkt(hafen, s, d).Z) is not null;

    /// <summary>Prüft einmal je Hafen, wo Wasser ist: Abstand für beide Liegeplätze und Länge der Bahn.</summary>
    private void NachenVermessen(Hafenplatz hafen)
    {
        foreach (var d in new[] { NachenAbstand, 5f, 6.5f })
        {
            bool Frei(float s) => NachenWasser(hafen, s, d) && NachenWasser(hafen, s, d - 1.2f) && NachenWasser(hafen, s, d + 1.2f);
            if (!Frei(NachenLiege1) || !Frei(NachenLiege1 - 2.5f) || !Frei(NachenLiege1 + 2.5f)) continue;
            _nachenAbstandGemessen = d;
            _nachenFestOk = true;
            _nachenBahn = 0;
            if (Frei(NachenLiege2) && Frei(NachenLiege2 - 2.5f))
            {
                var s = NachenLiege2;
                while (s > NachenLiege2 - 50f && Frei(s - 2f)) s -= 2f;
                _nachenBahn = Math.Max(0, NachenLiege2 - s - 2.5f); // 2,5 m Halbe Länge bis zum Ende der Bahn
            }
            return;
        }
        _nachenFestOk = false;
    }

    private SchiffAnsicht NachenErzeugen(string name, int nummer, bool mitSchiffer)
    {
        var a = SchiffErzeugen(name, new Color(0.5f, 0.42f, 0.3f), nummer, false, NachenModell);
        a.Wurzel.Visible = false;
        if (!mitSchiffer && a.Schiffer is { } f) f.Modell.Visible = false;
        return a;
    }

    private static void NachenStange(SchiffAnsicht a, bool staken)
    {
        if (a.Wurzel.FindChild("Stange", true, false) is Node3D s) s.Visible = staken;
        if (a.Wurzel.FindChild("Stange_Liegend", true, false) is Node3D l) l.Visible = !staken;
    }

    /// <summary>Jedes Bild (aus <see cref="SchiffeAktualisieren"/>): die Nachen beim Steg des ersten Hafens setzen.</summary>
    private void NachenAktualisieren(Stadtwirtschaft w, double minute, double delta)
    {
        var sf = w.Schiffahrt;
        var hafen = sf.Haefen.Count > 0 ? sf.Haefen[0] : null;
        if (hafen is null || !_stege.TryGetValue(hafen.Kennung, out var steg) || !IsInstanceValid(steg))
        {
            if (_nachenFest is not null) _nachenFest.Wurzel.Visible = false;
            if (_nachenPendler is not null) _nachenPendler.Wurzel.Visible = false;
            return;
        }
        if (_nachenHafen != hafen.Kennung || (!_nachenFestOk && _nachenVersuch < 3 && Engine.GetFramesDrawn() % 60 == 0))
        {
            if (_nachenHafen != hafen.Kennung) _nachenVersuch = 0;
            _nachenHafen = hafen.Kennung;
            _nachenVersuch++;
            NachenVermessen(hafen);
        }
        if (!_nachenFestOk)
        {
            if (_nachenFest is not null) _nachenFest.Wurzel.Visible = false;
            if (_nachenPendler is not null) _nachenPendler.Wurzel.Visible = false;
            return;
        }
        var d = _nachenAbstandGemessen;
        var nach = new Vector3((float)hafen.RichtungX, 0, (float)hafen.RichtungZ).Normalized();
        var t = new Vector3(-nach.Z, 0, nach.X);
        float Dreh(Vector3 r) => Mathf.Atan2(-r.Z, r.X);

        // der festgemachte
        _nachenFest ??= NachenErzeugen("Nachen am Steg", 11, false);
        NachenStange(_nachenFest, false);
        var p1 = NachenPunkt(hafen, NachenLiege1, d);
        _nachenFest.Wurzel.Visible = true;
        SchiffPoseSetzen(_nachenFest, new Vector3(p1.X, 0, p1.Z), Dreh(t), delta);

        // der zweite: pendelt, wenn die Bahn reicht
        _nachenPendler ??= NachenErzeugen("Nachen Pendler", 12, true);
        var tageszeit = Spielzeit.TagesbeginnMinute / 60.0 + minute % Spielzeit.MinutenProTag / 60.0;
        var pendeln = _nachenBahn >= 12f && sf.HafenOffen && tageszeit >= 8 && tageszeit < 20;
        var bahn = _nachenBahn;
        var zyklus = 2.0 * (bahn + NachenPauseMeter);
        var heim = 2.0 * bahn + NachenPauseMeter;   // ab hier steht er wieder am Liegeplatz (zweite Pause)
        if (bahn >= 12f)
        {
            var c = _nachenWeg % zyklus;
            var lauf = _simulation?.Takt.IstPausiert == true ? 0 : delta * NachenTempo;
            if (pendeln) _nachenWeg += lauf;
            else if (c < heim) { _nachenWeg += lauf; if (_nachenWeg % zyklus >= heim || _nachenWeg % zyklus < c) _nachenWeg = Math.Floor(_nachenWeg / zyklus) * zyklus + heim; }
            else _nachenWeg = Math.Floor(_nachenWeg / zyklus) * zyklus + heim;
        }
        var cc = bahn >= 12f ? _nachenWeg % zyklus : heim;
        double x;
        var dir = 1;
        if (cc < bahn) x = cc;
        else if (cc < bahn + NachenPauseMeter) x = bahn;
        else if (cc < 2 * bahn + NachenPauseMeter) { x = bahn - (cc - bahn - NachenPauseMeter); dir = -1; }
        else { x = 0; dir = -1; }
        var unterwegs = bahn >= 12f && (x > 0.05 || cc < bahn + NachenPauseMeter && cc > 0);
        var p2 = NachenPunkt(hafen, NachenLiege2 - (float)x, d);
        NachenStange(_nachenPendler, unterwegs);
        if (_nachenPendler.Schiffer is { } f) f.Modell.Visible = unterwegs;
        _nachenPendler.Wurzel.Visible = true;
        SchiffPoseSetzen(_nachenPendler, new Vector3(p2.X, 0, p2.Z), Dreh(dir > 0 ? -t : t), delta);
    }
}
