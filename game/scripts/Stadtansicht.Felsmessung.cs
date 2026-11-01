using Godot;

namespace Reichsgilde;

/// <summary>
/// Messung der Kosten von Felsen und Kulissenbergen im Spiel (Startoption <c>--felsvergleich=1</c>, Felspinsel F5/F7) – ein Teil der
/// <c>partial class Stadtansicht</c>, gebaut wie <c>--wasservergleich</c> (Stadtansicht.Wassermessung.cs): im selben Prozess reihum „ohne“, „Felsen“,
/// „Kulisse“ und „beides“, je 4 s (die erste Sekunde nach dem Wechsel zählt nicht, die erste Runde übersetzt nur die Shader), drei Runden; je Zustand
/// der Median der GPU-Zeit und der Bildzeit ins Log (<c>[felsmessung]</c>), dann beendet sich das Spiel. Was die Ansicht zeigt, bestimmen die übrigen
/// Startoptionen (<c>--karte</c>, <c>--blick</c>, <c>--voreinstellung</c>, <c>--vsync=Aus</c>).
/// </summary>
public partial class Stadtansicht
{
    private static readonly bool FelsvergleichAn = Wasserarten.Startoption("felsvergleich") == "1";
    private static readonly string[] FelsmessungZustaende = { "ohne", "Felsen", "Kulisse", "beides" };

    private double _fmZeit = -WassermessungVorlauf;
    private int _fmZustand = -1, _fmRunde;
    private readonly List<double> _fmGpu = new(), _fmBild = new();
    private readonly Dictionary<string, List<(double Gpu, double Bild)>> _fmErgebnis = new();

    /// <summary>Jedes Bild (aus <see cref="GrafikBild"/>), nur mit <c>--felsvergleich=1</c>.</summary>
    private void FelsmessungSchritt(double delta)
    {
        if (!FelsvergleichAn || (_felsenKnoten is null && _kulisse is null)) return;
        var rid = GetTree().Root.GetViewportRid();
        if (_fmZustand < 0 && _fmZeit <= -WassermessungVorlauf + 1e-9)
        {
            RenderingServer.ViewportSetMeasureRenderTime(rid, true);
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        }
        _fmZeit += delta;
        if (_fmZeit < 0) return;
        if (_fmZustand >= 0 && _fmZeit > WassermessungEinschwingen)
        {
            _fmGpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid));
            _fmBild.Add(delta * 1000);
        }
        if (_fmZustand >= 0 && _fmZeit < WassermessungDauer) return;
        if (_fmZustand >= 0 && _fmRunde > 0 && _fmGpu.Count > 0)
        {
            var name = FelsmessungZustaende[_fmZustand];
            if (!_fmErgebnis.TryGetValue(name, out var liste)) _fmErgebnis[name] = liste = new();
            liste.Add((Median(_fmGpu), Median(_fmBild)));
        }
        _fmGpu.Clear();
        _fmBild.Clear();
        _fmZeit = 0;
        if (++_fmZustand >= FelsmessungZustaende.Length)
        {
            _fmZustand = 0;
            if (++_fmRunde > WassermessungRunden)
            {
                FelsmessungAusgeben();
                return;
            }
        }
        if (_felsenKnoten is not null) _felsenKnoten.Visible = _fmZustand is 1 or 3;
        if (_kulisse is not null) _kulisse.Visible = _fmZustand is 2 or 3;
    }

    private void FelsmessungAusgeben()
    {
        var ohne = _fmErgebnis.TryGetValue("ohne", out var o) ? Median(o.Select(x => x.Gpu).ToList()) : 0;
        foreach (var name in FelsmessungZustaende)
        {
            if (!_fmErgebnis.TryGetValue(name, out var werte)) continue;
            var gpu = Median(werte.Select(x => x.Gpu).ToList());
            var bild = Median(werte.Select(x => x.Bild).ToList());
            GD.Print($"[felsmessung] {name}: GPU {gpu:0.00} ms ({gpu - ohne:+0.00;-0.00} ms), Bildzeit {bild:0.0} ms = {1000 / bild:0.0} Bilder/s, " +
                     $"GPU je Runde {string.Join(" / ", werte.Select(x => x.Gpu.ToString("0.00")))}");
        }
        GD.Print($"[felsmessung] fertig: {FelsAnzahl} Felsstücke in {_felsZellen.Count} Zellen, Kulisse {(_kulisse is null ? "keine" : "ja")}, " +
                 $"{RenderingServer.GetVideoAdapterName()}, Voreinstellung {Fenster.AktuelleVoreinstellung}, Boden {Fenster.AktuelleBodenqualitaet}");
        GetTree().Quit();
    }
}
