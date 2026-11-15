using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Das Strömungsfeld der Flüsse und Bäche (echtes Flowfield fürs Wasser) – ein Teil der <c>partial class Stadtansicht</c>. Je Lauf wird beim Kartenaufbau einmal ein
/// <see cref="Stroemungsfeld"/> gerechnet (Band, Ufer, Pfeiler und Joche der Brücken, Kähne am Kai, Boden über dem Spiegel im Band, Kaimauern) und als Textur
/// in den Wasserstoff des Laufs gelegt (<c>strom_karte</c>); der Shader liest sie an jedem Punkt und schiebt die Normalkarten in zwei Phasen damit. Der Kern
/// bleibt unberührt. Gebaut wird im ersten Bild nach den Flächen (dann stehen die Brücken), und wieder, wenn die Kähne des Hafens dazukommen; die Kartenwerkstatt
/// baut kein Feld (sie baut das Wasser bei jedem Pinselstrich neu). Startoption <c>--stromfeld=0</c> schaltet es aus (für Vergleichsmessungen).
/// </summary>
public partial class Stadtansicht
{
    private static readonly bool StromfeldAn = Wasserarten.Startoption("stromfeld") != "0";

    /// <summary>Startoption <c>--stromfeld=bild</c>: das Feld jedes Laufs zusätzlich als Bild nach <c>user://stromfeld_&lt;Lauf&gt;.png</c> (Tempo längs: blau = gegenläufig, grün = langsam, gelb bis rot = schnell, grau = Land, schwarz = Hindernis).</summary>
    private static readonly bool StromfeldBild = Wasserarten.Startoption("stromfeld") == "bild";

    private bool _stromFeldVeraltet = true;
    private string _stromFeldZeichen = "";
    private double _stromFeldRuhe;

    /// <summary>Gerechnete Felder je Lauf, mit dem Zeichen ihrer Eingaben (gleiche Eingaben: nichts neu rechnen, z. B. nach einem Wechsel der Wasserstufe).</summary>
    private readonly Dictionary<Wasserlauf, (string Zeichen, ImageTexture Textur, Stroemungsfeld Feld)> _stromFelder = new();

    /// <summary>Stoff je Lauf des letzten Baus der Wasserflächen.</summary>
    private readonly List<(Wasserlauf Lauf, ShaderMaterial Stoff)> _wasserLaufStoffe = new();

    private void StroemungsfeldVeraltet() => _stromFeldVeraltet = true;

    /// <summary>Jedes Bild (aus <see cref="WasserBild"/>): das Feld bauen, wenn die Flächen neu sind oder sich die Kähne am Hafen geändert haben (höchstens alle 2 s).</summary>
    private void StroemungsfeldBild(double delta)
    {
        if (!StromfeldAn || _entwurf is not null || _wasserLaufStoffe.Count == 0 || _wasserFlaechen is null || !IsInstanceValid(_wasserFlaechen)) return;
        _stromFeldRuhe -= delta;
        var kaehne = KaehneAmWasser();
        var zeichen = string.Join(";", kaehne.Select(k => $"{k.X:0.0},{k.Z:0.0}"));
        if (!_stromFeldVeraltet && zeichen == _stromFeldZeichen) return;
        if (_stromFeldRuhe > 0 && !_stromFeldVeraltet) return;
        _stromFeldRuhe = 2;
        _stromFeldVeraltet = false;
        _stromFeldZeichen = zeichen;
        StroemungsfeldBauen(kaehne);
    }

    /// <summary>Die Kähne der Häfen (Ort, halbe Ausdehnung quer und längs zur Strömung).</summary>
    private List<Stroemungsfeld.Hindernis> KaehneAmWasser()
    {
        var liste = new List<Stroemungsfeld.Hindernis>();
        foreach (var h in _haefen)
            foreach (var kahn in h.Kaehne)
                if (IsInstanceValid(kahn) && kahn.IsVisibleInTree())
                    liste.Add(new(kahn.GlobalPosition.X, kahn.GlobalPosition.Z, KahnQuer, KahnLaengs));
        return liste;
    }

    private void StroemungsfeldBauen(List<Stroemungsfeld.Hindernis> kaehne)
    {
        if (GewaesserForm is not { IstLeer: false } form) return;
        var uhr = Time.GetTicksUsec();
        var gebaut = 0;
        var meldung = new List<string>();
        foreach (var (lauf, stoff) in _wasserLaufStoffe)
        {
            if (lauf.Punkte.Count < 2 || !IsInstanceValid(stoff)) continue;
            var hindernisse = new List<Stroemungsfeld.Hindernis>();
            foreach (var (ort, halbQuer, halbLaengs, _) in _wasserPfeiler) hindernisse.Add(new(ort.X, ort.Y, halbQuer, halbLaengs));
            hindernisse.AddRange(kaehne);
            var zeichen = $"{lauf.Punkte.Count}|{lauf.Stroemung:R}|{string.Join(";", hindernisse.Select(o => $"{o.X:0.0},{o.Z:0.0},{o.HalbQuer},{o.HalbLaengs}"))}";
            if (!_stromFelder.TryGetValue(lauf, out var alt) || alt.Zeichen != zeichen)
            {
                var kaiStellen = OffeneStellen(form, lauf.Nummer, lauf.Name).Where(s => s.Art == Offenart.Kai).ToList();
                var punkte = lauf.Punkte.Select(p => new Stroemungsfeld.Punkt((float)p.Ort.X, (float)p.Ort.Z, (float)p.RichtungX, (float)p.RichtungZ, (float)p.Breite, (float)p.Spiegel)).ToList();
                var feld = Stroemungsfeld.Rechnen(new Stroemungsfeld.Eingabe
                {
                    Mittellinie = punkte, Tempo = (float)lauf.Stroemung, RandFaktor = FlussRandFaktor, Hindernisse = hindernisse,
                    Zelle = Mathf.Clamp(punkte.Max(q => q.Breite) / 24f, 0.5f, 1f), // schmale Bäche fein, breite Flüsse grob (Rhein: 1 m, ein Pfeiler hat 2,8 m)
                    Boden = (x, z) => BodenHoehe(x, z),
                    Kai = kaiStellen.Count == 0 ? null : (x, z) => Offenheit(kaiStellen, new Vector2(x, z), 0f).Kai,
                });
                if (feld is null) continue;
                var bild = Image.CreateFromData(feld.Breite, feld.Hoehe, false, Image.Format.Rgba8, feld.Bilder);
                if (StromfeldBild) StromfeldSpeichern(lauf, feld, punkte);
                alt = (zeichen, ImageTexture.CreateFromImage(bild), feld);
                _stromFelder[lauf] = alt;
                gebaut++;
                var z = feld.Zahlen;
                meldung.Add($"{lauf.Name} {feld.Breite}x{feld.Hoehe} Zellen à {feld.Zelle:0.0#} m, {hindernisse.Count} Hindernisse ({z.Hindernis} Zellen), " +
                            $"Kehrwasser {z.Kehrwasser} Zellen, Tempo längs {z.Kleinst:0.00} bis {z.Groesst:0.00} m/s (Band {lauf.Stroemung:0.00})");
            }
            var (_, textur, f) = alt;
            stoff.SetShaderParameter("strom_karte", textur);
            stoff.SetShaderParameter("strom_ort", new Vector4(f.X0, f.Z0, 1f / (f.Breite * f.Zelle), 1f / (f.Hoehe * f.Zelle)));
            stoff.SetShaderParameter("strom_skala", f.Skala);
            stoff.SetShaderParameter("strom_aktiv", 1f);
        }
        foreach (var alt in _stromFelder.Keys.Where(k => _wasserLaufStoffe.All(l => l.Lauf != k)).ToList()) _stromFelder.Remove(alt);
        if (gebaut > 0)
            GD.Print($"[wasser] Strömungsfeld: {gebaut} Läufe in {(Time.GetTicksUsec() - uhr) / 1000.0:0.0} ms; {string.Join("; ", meldung)}");
    }

    private static void StromfeldSpeichern(Wasserlauf lauf, Stroemungsfeld feld, List<Stroemungsfeld.Punkt> punkte)
    {
        var bild = Image.CreateEmpty(feld.Breite, feld.Hoehe, false, Image.Format.Rgb8);
        for (var j = 0; j < feld.Hoehe; j++)
            for (var i = 0; i < feld.Breite; i++)
            {
                if (!feld.IstWasser(i, j)) { bild.SetPixel(i, j, new Color(0.25f, 0.25f, 0.25f)); continue; }
                var (vx, vz) = feld.StromBei(i, j);
                if (vx == 0 && vz == 0) { bild.SetPixel(i, j, Colors.Black); continue; }
                // Tempo längs der Mittellinie (Richtung der nächsten Zelle ist hier nicht bekannt: Betrag mit Vorzeichen der Hauptrichtung des Laufs)
                var rx = punkte[punkte.Count / 2].RichtungX; var rz = punkte[punkte.Count / 2].RichtungZ;
                var v = vx * rx + vz * rz;
                bild.SetPixel(i, j, v < 0 ? new Color(0.2f, 0.3f, Mathf.Min(1f, 0.5f - v)) : Color.FromHsv(Mathf.Clamp(0.33f - 0.33f * v / (float)lauf.Stroemung / 1.4f, 0f, 0.33f), 0.9f, 0.9f));
            }
        var pfad = $"user://stromfeld_{lauf.Name}.png";
        bild.SavePng(pfad);
        GD.Print($"[wasser] Strömungsfeld als Bild: {ProjectSettings.GlobalizePath(pfad)}");
    }
}
