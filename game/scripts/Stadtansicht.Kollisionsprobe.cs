using Godot;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Reichsgilde;
public partial class Stadtansicht
{
    // NUR ZUM MESSEN (kein Commit): --kollisionsprobe=1
    private double _kpZeit, _kpSumme; private int _kpBilder, _kpDrin, _kpSumDrin, _kpSumFigur; private bool _kpStatisch;
    private readonly HashSet<(int, int)> _kpGesehen = new();
    private readonly Dictionary<int,int> _kpJeFigur = new();

    private readonly List<EgoHindernis> _kpHaeuser = new();
    private const float Koerper = 0.25f;
    private int _kpBildWarte = -1, _kpBildNr; private double _kpBildSperre; private Passant? _kpBildFigur; private string _kpBildArt = "";
    private string KpArt(Vector2 p, out int index, out float tiefe)
    {
        var fuss = FussHoehe(p.X, p.Y);
        var (bestArt, bestT, bestI) = ("", 0f, -1);
        void Pruefe(EgoHindernis h, string art, int i) { if (EgoEindringen(h, p, fuss) is { } t && t.Tiefe > bestT) (bestArt, bestT, bestI) = (art, t.Tiefe, i); }
        // die letzten Einträge sind die Marktbrunnen (Rahmen um Becken und Trog); dort gehen Figuren absichtlich ringsum – nicht zählen
        for (var i = 0; i < _egoKlickHindernisse.Count - _brunnen.Count; i++) Pruefe(_egoKlickHindernisse[i], "Haus", i);
        for (var i = 0; i < _egoMauern.Count; i++) Pruefe(_egoMauern[i], "Mauer", i);
        for (var i = 0; i < _kpHaeuser.Count; i++) Pruefe(_kpHaeuser[i], "Wohnhaus", i);
        foreach (var h in EgoKleine(p)) Pruefe(h, h.Rund ? "Rundes(Baum/Stein/Stand)" : "Kleines(Zaun/Karren)", -2);
        (index, tiefe) = (bestI, bestT);
        return bestArt;
    }

    private static float KpAbstandPfad(Passant p)
    {
        var d = float.MaxValue;
        for (var i = 0; i + 1 < p.Pfad.Length; i++) d = Mathf.Min(d, Geometry3D.GetClosestPointToSegment(p.Knoten.Position, p.Pfad[i], p.Pfad[i + 1]).DistanceTo(p.Knoten.Position));
        return d;
    }

    private void KollisionsprobeTakt(double delta)
    {
        if (Wasserarten.Startoption("kollisionsprobe") is not "1") return;
        _kpZeit += delta;
        if (_kpZeit < 6) return;
        if (!_kpStatisch)
        {
            _kpStatisch = true;
            EgoHindernisseLesen();
            foreach (var kind in GetChildren())
            {
                if (kind is not Node3D g || !g.Name.ToString().StartsWith("Wohnhaus_")) continue;
                Aabb? box = null;
                foreach (var n in g.FindChildren("*", "MeshInstance3D", true, false))
                {
                    if (n is not MeshInstance3D m || m.Mesh is null || !m.IsVisibleInTree()) continue;
                    var ab = m.GetAabb(); if (ab.Size.Y < 1.5f) continue;
                    var lok = g.GlobalTransform.AffineInverse() * m.GlobalTransform;
                    var t = lok * ab;
                    box = box is null ? t : box.Value.Merge(t);
                }
                if (box is { } bx)
                    _kpHaeuser.Add(EgoHindernisVon(g.GlobalTransform * new Transform3D(Basis.Identity, bx.GetCenter()), bx.Size / 2f));
            }
            GD.Print($"[kp] Hindernisse {_egoKlickHindernisse.Count} Haus, {_egoMauern.Count} Mauer, {_kpHaeuser.Count} Wohnhaus-Plätze");
            var kanten = _netz.Kanten.ToList();
            int blockiert = 0, ausAnschluss = 0, laengeDrin = 0; float gesamt = 0;
            foreach (var (v, b) in kanten)
            {
                var l = v.DistanceTo(b); gesamt += l;
                var n = Mathf.Max(1, (int)(l / 0.25f)); var drin = 0; Vector3 ort = default; string art = "";
                for (var i = 0; i <= n; i++)
                {
                    var q = v.Lerp(b, i / (float)n);
                    var a = KpArt(new Vector2(q.X, q.Z), out _, out var t);
                    if (a != "" && t > EgoKamera.Radius - Koerper) { drin++; ort = q; art = a; }
                }
                if (drin > 0)
                {
                    blockiert++; laengeDrin += drin;
                    var anschluss = _anschlussStrecken.Any(s => (s.Von.DistanceTo(v) < 0.7f || s.Von.DistanceTo(b) < 0.7f) && (s.Bis.DistanceTo(v) < 0.7f || s.Bis.DistanceTo(b) < 0.7f));
                    if (anschluss) ausAnschluss++;
                    GD.Print($"[kp] Kante ({v.X:0.0},{v.Z:0.0})-({b.X:0.0},{b.Z:0.0}) Länge {l:0.0} m: {drin * 0.25f:0.0} m im {art}, z.B. bei ({ort.X:0.0},{ort.Z:0.0}){(anschluss ? " [Anschluss]" : "")}");
                }
            }
            GD.Print($"[kp] Netz: {kanten.Count} Kanten, {gesamt:0} m; {blockiert} Kanten durch Hindernis ({ausAnschluss} davon Anschlussstücke), {laengeDrin * 0.25f:0} m im Hindernis");
        }
        var drinJetzt = 0; var figuren = 0;
        foreach (var (nr, p) in _passanten)
        {
            if (!p.Knoten.Visible || !ImBild(p.Knoten.Position, 3f)) continue; // nur was man sieht
            figuren++;
            var pos = new Vector2(p.Knoten.Position.X, p.Knoten.Position.Z);
            var art = KpArt(pos, out var idx, out var tiefe);
            if (art == "" || tiefe < EgoKamera.Radius - Koerper) continue;
            if (_begehbar is { } bg && art is "Haus" or "Mauer" or "Wohnhaus" && bg.Abstand(pos.X, pos.Y) >= Koerper + 0.03f) continue; // Zweitmeinung: Hafen und Torbögen sind frei
 // Körper der Figur berührt das Hindernis
            drinJetzt++;
            _kpJeFigur[nr] = _kpJeFigur.GetValueOrDefault(nr) + 1;
            if (Wasserarten.Startoption("kollisionsbild") is { } ordner && _kpBildWarte < 0 && _kpBildNr < 8 && _kpZeit > _kpBildSperre && tiefe - EgoKamera.Radius + Koerper > 0.5f)
            {
                _kpBildFigur = p; _kpBildWarte = 3; _kpBildArt = $"{_kpBildNr}_{art}_{nr}"; _kpBildSperre = _kpZeit + 9;
                _kamera.Size = 10;
            }
            var zelle = ((int)(pos.X / 2), (int)(pos.Y / 2));
            if (_kpGesehen.Add(zelle) && _kpGesehen.Count <= 400)
            {
                // Abstand zur nächsten Mittellinie des Netzes
                var nah = float.MaxValue;
                foreach (var (v, b) in _netz.Kanten) nah = Mathf.Min(nah, Geometry3D.GetClosestPointToSegment(p.Knoten.Position, v, b).DistanceTo(p.Knoten.Position));
                if (nah > 5) GD.Print("[kp] FERN Pfad " + string.Join(" ", p.Pfad.Select(q => $"({q.X:0},{q.Z:0})")) + $" Weg {(p.FuerWeg is null ? "-" : p.FuerWeg.ToString())} Verzoeg {p.Verzoegerung:0}");
                GD.Print($"[kp] Figur {nr} in {art} #{idx} bei ({pos.X:0.0},{pos.Y:0.0}), Körper {tiefe - EgoKamera.Radius + Koerper:0.00} m tief, Abstand zur Mittellinie {nah:0.00} m, Stand {(p.Stand is null ? "geht" : "steht")}, Seitlich {p.Seitlich:0.0}, Pfadlänge {p.Laenge:0}, Weg {(p.FuerWeg is { } fw ? fw.Von.Ort + ">" + fw.Nach.Ort : "-")}, Pfadpunkte {p.Pfad.Length} ({(p.Pfad.Length > 0 ? $"{p.Pfad[0].X:0},{p.Pfad[0].Z:0} bis {p.Pfad[^1].X:0},{p.Pfad[^1].Z:0}" : "-")}), Bummel {p.ImBummel}, Kreist {p.KreistAuf}, Marktstelle {p.Marktstelle}, PfadMitte {(p.Mitte is null ? "nein" : "ja")}, Abstand eigener Pfad {KpAbstandPfad(p):0.0} m");
            }
        }
        if (_kpBildWarte >= 0 && _kpBildFigur is { } bf)
        {
            _kameraDrehung.Position = new Vector3(bf.Knoten.Position.X, bf.Knoten.Position.Y, bf.Knoten.Position.Z);
            if (_kpBildWarte-- == 0)
            {
                var ordner = Wasserarten.Startoption("kollisionsbild")!;
                GetViewport().GetTexture().GetImage().SavePng($"{ordner}/kp_{_kpBildArt}.png");
                GD.Print($"[kp] Bild gespeichert kp_{_kpBildArt}.png bei {bf.Knoten.Position.X:0.0},{bf.Knoten.Position.Z:0.0}");
                _kpBildNr++; _kpBildWarte = -1;
            }
        }
        _kpBilder++; _kpSumDrin += drinJetzt; _kpSumFigur += figuren; _kpDrin = Math.Max(_kpDrin, drinJetzt);
        if (_kpBilder % 120 == 0)
            GD.Print($"[kp] Begehbarkeit: Pfade geprüft {_pfadGeprueft}, mit Verstoß {_pfadVerstoesse}; {_begehbarAufrufe} Prüfungen, davon {_begehbarGekuerzt} zusammengerückt, {(_begehbarAufrufe > 0 ? _begehbarTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / _kpBilder : 0):0.000} ms je Bild");
        if (_kpBilder % 120 == 0)
            GD.Print($"[kp] Bild {_kpBilder}: sichtbar Ø {_kpSumFigur / (double)_kpBilder:0.0}, davon im Hindernis Ø {_kpSumDrin / (double)_kpBilder:0.00} (höchstens {_kpDrin}); verschiedene Figuren betroffen {_kpJeFigur.Count}");
    }
}
