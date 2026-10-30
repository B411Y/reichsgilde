using Godot;

namespace Reichsgilde;

/// <summary>
/// Messung der Kosten des Wassers im Spiel (Startoption <c>--wasservergleich=1</c>, <c>docs/wasser/phase3.md</c>) – ein Teil der
/// <c>partial class Stadtansicht</c>. Auf dem Rechner der Entwicklung teilen sich mehrere Aufgaben die Grafikkarte; einzelne Läufe schwanken
/// darum um das Doppelte. Diese Messung wechselt deshalb <b>im selben Prozess</b> reihum zwischen „ohne Wasser“ und den vier Stufen des Wassers
/// (je 4 s, die erste Sekunde nach dem Wechsel zählt nicht, die erste Runde übersetzt nur die Shader), drei Runden lang, und schreibt je
/// Zustand den Median der GPU-Zeit und der Bildzeit ins Log (<c>[wassermessung]</c>); danach beendet sich das Spiel. Was die Ansicht zeigt,
/// bestimmen die übrigen Startoptionen (<c>--karte</c>, <c>--blick</c>, <c>--wetter</c> …).
/// </summary>
public partial class Stadtansicht
{
    private const double WassermessungVorlauf = 10, WassermessungDauer = 4, WassermessungEinschwingen = 1;
    private const int WassermessungRunden = 3;

    private static readonly bool WasservergleichAn = Wasserarten.Startoption("wasservergleich") == "1";

    /// <summary>Erzwungene Stufe während der Messung (sonst null: die aus der Voreinstellung).</summary>
    private Wasserstufe? _wasserStufeMessung;

    private double _wmZeit = -WassermessungVorlauf;
    private int _wmZustand = -1, _wmRunde;
    private readonly List<double> _wmGpu = new(), _wmBild = new();
    private readonly Dictionary<string, List<(double Gpu, double Bild)>> _wmErgebnis = new();

    private static readonly string[] WassermessungZustaende = { "ohne Wasser", "Niedrig", "Mittel", "Hoch", "Ultra" };

    /// <summary>Jedes Bild (aus <see cref="WasserBild"/>), nur mit <c>--wasservergleich=1</c>.</summary>
    private void WassermessungSchritt(double delta)
    {
        if (!WasservergleichAn || _gewaesserKnoten is null || GewaesserForm is not { IstLeer: false } form) return;
        var rid = GetTree().Root.GetViewportRid();
        if (_wmZustand < 0)
        {
            RenderingServer.ViewportSetMeasureRenderTime(rid, true);
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        }
        _wmZeit += delta;
        if (_wmZeit < 0) return;
        if (_wmZustand >= 0 && _wmZeit > WassermessungEinschwingen)
        {
            _wmGpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid));
            _wmBild.Add(delta * 1000);
        }
        if (_wmZustand >= 0 && _wmZeit < WassermessungDauer) return;

        // Zustand abschließen (Runde 0 übersetzt nur), nächsten setzen
        if (_wmZustand >= 0 && _wmRunde > 0 && _wmGpu.Count > 0)
        {
            var name = WassermessungZustaende[_wmZustand];
            if (!_wmErgebnis.TryGetValue(name, out var liste)) _wmErgebnis[name] = liste = new();
            liste.Add((Median(_wmGpu), Median(_wmBild)));
        }
        _wmGpu.Clear();
        _wmBild.Clear();
        _wmZeit = 0;
        if (++_wmZustand >= WassermessungZustaende.Length)
        {
            _wmZustand = 0;
            if (++_wmRunde > WassermessungRunden)
            {
                WassermessungAusgeben();
                return;
            }
        }
        if (_wmZustand == 0)
        {
            if (_wasserFlaechen is not null) _wasserFlaechen.Visible = false;
            return;
        }
        _wasserStufeMessung = (Wasserstufe)(_wmZustand - 1);
        WasserflaechenBauen(form);
    }

    private void WassermessungAusgeben()
    {
        var ohne = _wmErgebnis.TryGetValue("ohne Wasser", out var o) ? Median(o.Select(x => x.Gpu).ToList()) : 0;
        foreach (var name in WassermessungZustaende)
        {
            if (!_wmErgebnis.TryGetValue(name, out var werte)) continue;
            var gpu = Median(werte.Select(x => x.Gpu).ToList());
            var bild = Median(werte.Select(x => x.Bild).ToList());
            GD.Print($"[wassermessung] {name}: GPU {gpu:0.00} ms (Wasser {gpu - ohne:+0.00;-0.00} ms), Bildzeit {bild:0.0} ms = {1000 / bild:0.0} Bilder/s, " +
                     $"GPU je Runde {string.Join(" / ", werte.Select(x => x.Gpu.ToString("0.00")))}");
        }
        GD.Print($"[wassermessung] fertig: {RenderingServer.GetVideoAdapterName()}, Voreinstellung {Fenster.AktuelleVoreinstellung}");
        GetTree().Quit();
    }

    private static double Median(List<double> werte)
    {
        if (werte.Count == 0) return 0;
        var s = werte.OrderBy(x => x).ToList();
        return s[s.Count / 2];
    }
}
