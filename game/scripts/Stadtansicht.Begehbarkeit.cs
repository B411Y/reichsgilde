using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// <b>Begehbarkeit für Passanten</b> – ein Teil der <c>partial class Stadtansicht</c> (Nutzer, 14.11.2026). Liest die Gebäude (Klickflächen aller Häuser,
/// Werkstätten, Kulissen, Rathaus, Dom), die gebauten Wohnhäuser (Netze) und die Mauerteile als Sperrflächen in eine <see cref="Begehbarkeit"/>.
/// Zweierlei geschieht damit: Die seitliche Lage jeder Figur (Spur, Schwanken, Ausweichen) rückt bei einer Wand zur Mittellinie zusammen, bis auf
/// <see cref="Begehbarkeit.Spalt"/> (0,1 m) Abstand zwischen Körper und Wand, nie hinein; und jeder neu gebaute Pfad wird geprüft (Zahl der Verstöße
/// in <c>--kollisionsprobe=1</c> und im Protokoll). Die Mittellinie selbst legen die Wege der Karte; sie ist Sache der Wegebau-Reparaturen.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Alle Reparaturen dieser Datei und der Wegebau-Fixes (14.11.2026) ein; <c>--kollisionsfix=0</c> schaltet sie für Vorher-Nachher-Messungen ab.</summary>
    private static readonly bool KollisionsFix = Wasserarten.Startoption("kollisionsfix") != "0";

    private Begehbarkeit? _begehbar;
    private int _begehbarStand = -1;
    private double _begehbarZeit;
    private int _pfadVerstoesse, _pfadGeprueft;
    private long _begehbarTicks, _begehbarAufrufe, _begehbarGekuerzt;

    /// <summary>Baut die Sperrflächen neu, wenn die Stadt sich geändert hat (nach dem Laden einmal, danach höchstens alle 3 s bei neuen Gebäuden).</summary>
    private void BegehbarkeitPflegen(double delta)
    {
        if (!_wohnhaeuserGebaut || _karte is null) return;
        _begehbarZeit += delta;
        if (_begehbar is not null && _begehbarZeit < 3) return;
        _begehbarZeit = 0;
        var stand = _wegStrecken.Count * 7919 + _anschlussStrecken.Count * 31 + _gebautePlaetze.Count + _egoMauern.Count * 3;
        if (_begehbar is not null && stand == _begehbarStand) return;
        _begehbarStand = stand;
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var b = _begehbar ??= new Begehbarkeit();
        b.Leeren();
        foreach (var knoten in FindChildren("*", "StaticBody3D", true, false))
        {
            if (knoten is not StaticBody3D { Visible: true } koerper || !koerper.HasMeta("kennung") || koerper.HasMeta("ego_frei") || !koerper.IsVisibleInTree()) continue;
            // Schiffe und der Hafen (die Klickfläche umfasst Kai, Steg und Lagerhaus, in dem die Arbeiter arbeiten) sind keine Sperre
            if (koerper.GetMeta("kennung").AsString() is { } kn && (kn.StartsWith("schiff:") || kn.StartsWith("hafen:"))) continue;
            foreach (var kind in koerper.GetChildren())
                if (kind is CollisionShape3D { Disabled: false, Shape: BoxShape3D kiste } form)
                    KastenAufnehmen(b, koerper.GlobalTransform * form.Transform, kiste.Size / 2f, koerper.GetMeta("kennung").AsString());
        }
        foreach (var kind in GetChildren())
        {
            if (kind is not Node3D g || !g.Name.ToString().StartsWith("Wohnhaus_")) continue;
            Aabb? box = null;
            foreach (var n in g.FindChildren("*", "MeshInstance3D", true, false))
            {
                if (n is not MeshInstance3D m || m.Mesh is null || !m.IsVisibleInTree()) continue;
                var ab = m.GetAabb();
                if (ab.Size.Y < 1.5f) continue;
                var t = g.GlobalTransform.AffineInverse() * m.GlobalTransform * ab;
                box = box is null ? t : box.Value.Merge(t);
            }
            if (box is { } bx) KastenAufnehmen(b, g.GlobalTransform * new Transform3D(Basis.Identity, bx.GetCenter()), bx.Size / 2f, g.Name.ToString());
        }
        for (var i = 0; i < _egoMauern.Count; i++)
        {
            var m = _egoMauern[i];
            b.Hinzu(m.Mitte.X, m.Mitte.Y, m.AchseX.X, m.AchseX.Y, m.AchseZ.X, m.AchseZ.Y, m.Hx, m.Hz, i < _egoMauerNamen.Count ? _egoMauerNamen[i] : "Mauer", false, true);
        }
        // Bäume, Steine, Zäune, Stände, stehende Karren und Tränken: dieselben kleinen Hindernisse wie in der Ego-Perspektive (ohne die Kreise des Marktpflasters,
        // die die Marktfläche regelt, und ohne bewegte)
        var kleinStand = (_fuellWurzel, Fenster.NaturDichte, _jahreszeit == Jahreszeit.Winter);
        if (_egoKleineZahl == 0 || kleinStand != _egoKleinStand) EgoKleineLesen(); // nur beim ersten Mal und wenn Natur, Füllung oder Jahreszeit wechseln (rund 100 ms)
        var klein = 0;
        foreach (var h in _egoGitter.Values.SelectMany(l => l).Concat(_egoGross))
        {
            if (h.Oben - h.Unten < 0.6f) continue; // niedriges Zeug (Stümpfe, Steine) hindert niemanden
            if (h.Rund && _marktHindernisse.Any(m => Mathf.Abs(m.Mitte.X - h.Mitte.X) < 0.05f && Mathf.Abs(m.Mitte.Z - h.Mitte.Y) < 0.05f)) continue;
            if (h.Rund) b.HinzuRund(h.Mitte.X, h.Mitte.Y, h.Hx, "klein");
            else b.Hinzu(h.Mitte.X, h.Mitte.Y, h.AchseX.X, h.AchseX.Y, h.AchseZ.X, h.AchseZ.Y, h.Hx, h.Hz, "klein");
            klein++;
        }
        // Torbögen: 3,2 m breit, so lang wie die Torburg tief ist, frei von Mauerteilen
        b.DurchgaengeLeeren();
        foreach (var tor in _tore)
        {
            var richtung = tor.Aussen.Normalized();
            b.Durchgang(tor.Ort.X, tor.Ort.Z, richtung.X, richtung.Z, 1.6f, 9f);
        }
        GD.Print($"[begehbar] {b.Anzahl} Sperrflächen (davon {klein} kleine) in {uhr.Elapsed.TotalMilliseconds:0.0} ms");
    }

    private static void KastenAufnehmen(Begehbarkeit b, Transform3D lage, Vector3 halb, string name)
    {
        var h = EgoHindernisVon(lage, halb);
        b.Hinzu(h.Mitte.X, h.Mitte.Y, h.AchseX.X, h.AchseX.Y, h.AchseZ.X, h.AchseZ.Y, h.Hx, h.Hz, name);
    }

    /// <summary>Prüft einen fertigen Pfad gegen die Sperrflächen (nur Zählung und Protokoll; Türen an Anfang und Ende gelten nicht).</summary>
    private void PfadPruefen(Vector3[] pfad, int nummer)
    {
        if (!KollisionsFix || _begehbar is not { } b || pfad.Length < 2) return;
        _pfadGeprueft++;
        float summe = 0;
        Vector3 ort = default; var segment = "";
        for (var i = 0; i + 1 < pfad.Length; i++)
        {
            var (a, c) = (pfad[i], pfad[i + 1]);
            var laenge = new Vector2(c.X - a.X, c.Z - a.Z).Length();
            if (laenge < 0.01f) continue;
            var teil = b.Verletzt(a.X, a.Z, c.X, c.Z, (pfad[0].X, pfad[0].Z), (pfad[^1].X, pfad[^1].Z), out var ex, out var ez);
            if (summe == 0 && teil > 0) { ort = new Vector3(ex, 0, ez); segment = $"Stück {a.X:0.0},{a.Z:0.0} nach {c.X:0.0},{c.Z:0.0} ({laenge:0} m, {(_netz.KanteBei(a.Lerp(c, 0.5f), 1f) is null ? "kein Netz" : "Netz")})"; }
            summe += teil;
        }
        if (summe < 0.5f) return;
        _pfadVerstoesse++;
        if (_pfadVerstoesse <= 40)
            GD.Print($"[begehbar] Pfad von Figur {nummer} liegt {summe:0.0} m in Sperrflächen, zuerst bei ({ort.X:0.0},{ort.Z:0.0}) in {b.Naechstes(ort.X, ort.Z).Name} auf {segment}: ({pfad[0].X:0},{pfad[0].Z:0}) … ({pfad[^1].X:0},{pfad[^1].Z:0}), {pfad.Length} Punkte");
    }
}
