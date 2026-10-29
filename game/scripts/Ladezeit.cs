using Godot;

namespace Reichsgilde;

/// <summary>
/// Messausgabe der Ladezeit (Startoption <c>--ladezeit=1</c>, mit <c>--ladezeit=ende</c> beendet sich das Spiel danach): schreibt Zeilen
/// „Ladezeit: …“ ins Log – an jedem Übergang die Millisekunden seit dem letzten (<see cref="Marke"/>), Summen wiederkehrender Arbeit wie das
/// Laden der Modelle (<see cref="Zaehlen"/>) und danach die Dauer der ersten Bilder samt den Pipelines, die der Grafiktreiber dabei
/// übersetzt (Shader). Ohne die Option tut die Klasse nichts. Gemacht für die Messung der Ladezeiten vor dem Ladebildschirm.
/// <c>--ladezeitbilder=&lt;ordner&gt;</c> speichert dazu Bildschirmfotos beim ersten Bild, nach 1 und 3 Sekunden und wenn es ruhig ist.
/// </summary>
public static class Ladezeit
{
    private static readonly string? Wert = OS.GetCmdlineUserArgs().LastOrDefault(a => a.StartsWith("--ladezeit="))?["--ladezeit=".Length..];
    private static readonly string? Bilderordner = OS.GetCmdlineUserArgs().LastOrDefault(a => a.StartsWith("--ladezeitbilder="))?["--ladezeitbilder=".Length..];

    /// <summary>Ist die Messung an?</summary>
    public static bool An => Wert is "1" or "ende";

    private static ulong _letzte;
    private static readonly Dictionary<string, (ulong Usec, int Anzahl)> _summen = new();

    /// <summary>Ein Übergang: Millisekunden seit der letzten Marke und seit Programmstart.</summary>
    public static void Marke(string abschnitt)
    {
        if (!An) return;
        var jetzt = Time.GetTicksMsec();
        GD.Print($"Ladezeit: {abschnitt}: {jetzt - _letzte} ms (bei {jetzt} ms)");
        _letzte = jetzt;
        Summen();
    }

    /// <summary>Misst <paramref name="arbeit"/> und zählt sie zur Summe <paramref name="name"/> (z. B. jedes geladene Modell).</summary>
    public static T Zaehlen<T>(string name, Func<T> arbeit)
    {
        if (!An) return arbeit();
        var anfang = Time.GetTicksUsec();
        var ergebnis = arbeit();
        var (usec, anzahl) = _summen.GetValueOrDefault(name);
        _summen[name] = (usec + Time.GetTicksUsec() - anfang, anzahl + 1);
        return ergebnis;
    }

    /// <summary>Für Arbeit in jedem Bild (ohne Zuweisung eines Lambdas): <c>var t = Ladezeit.Uhr(); …; Ladezeit.Dazu("Gras", t);</c></summary>
    public static ulong Uhr() => An ? Time.GetTicksUsec() : 0;

    /// <summary>Zählt die Zeit seit <paramref name="anfang"/> (<see cref="Uhr"/>) zur Summe <paramref name="name"/>.</summary>
    public static void Dazu(string name, ulong anfang)
    {
        if (!An) return;
        var (usec, anzahl) = _summen.GetValueOrDefault(name);
        _summen[name] = (usec + Time.GetTicksUsec() - anfang, anzahl + 1);
    }

    /// <summary>Schreibt die Summen seit der letzten Marke (sie gehören zu deren Abschnitt) und setzt sie zurück.</summary>
    private static void Summen()
    {
        foreach (var (name, (usec, anzahl)) in _summen)
            GD.Print($"Ladezeit: davon {name}: {usec / 1000} ms ({anzahl}×)");
        _summen.Clear();
    }

    // ── die ersten Bilder ─────────────────────────────────────────────────

    /// <summary>Lange Bilder (über so viele ms) stehen einzeln im Log; „ruhig“ heißt so viele Bilder in Folge darunter.</summary>
    private const ulong LangesBild = 50;
    private const int RuhigeBilder = 60;

    private static SceneTree? _baum;
    private static ulong _bildStart, _vorigesBild;
    private static int _bild, _ruhig, _fotoStufe;
    private static ulong _langeSumme, _letztesLanges;
    private static int _langeAnzahl;
    private static long _pipelinesStart;

    /// <summary>Nach dem Aufbau der Szene: misst die Bilder, bis es ruhig ist (oder nach 30 s).</summary>
    public static void BilderMessen(SceneTree baum, string was)
    {
        if (!An) return;
        Marke($"{was} aufgebaut, warte auf die Bilder");
        _baum = baum;
        _bildStart = _vorigesBild = Time.GetTicksMsec();
        _bild = _ruhig = _fotoStufe = _langeAnzahl = 0;
        _langeSumme = _letztesLanges = 0;
        _pipelinesStart = Pipelines().Summe;
        RenderingServer.FramePostDraw += BildFertig;
    }

    private static (long Summe, string Text) Pipelines()
    {
        long Wert(RenderingServer.RenderingInfo info) => (long)RenderingServer.GetRenderingInfo(info);
        var (canvas, netz, flaeche, zeichnen, spezial) = (Wert(RenderingServer.RenderingInfo.PipelineCompilationsCanvas),
            Wert(RenderingServer.RenderingInfo.PipelineCompilationsMesh), Wert(RenderingServer.RenderingInfo.PipelineCompilationsSurface),
            Wert(RenderingServer.RenderingInfo.PipelineCompilationsDraw), Wert(RenderingServer.RenderingInfo.PipelineCompilationsSpecialization));
        return (canvas + netz + flaeche + zeichnen + spezial,
            $"Oberfläche {canvas}, beim Laden {netz + flaeche}, beim Zeichnen {zeichnen}, Spezialisierung {spezial}");
    }

    private static void BildFertig()
    {
        var jetzt = Time.GetTicksMsec();
        var dauer = jetzt - _vorigesBild;
        _vorigesBild = jetzt;
        _bild++;
        var seit = jetzt - _bildStart;
        if (_bild == 1)
        {
            GD.Print($"Ladezeit: erstes Bild nach {dauer} ms (bei {jetzt} ms), Pipelines: {Pipelines().Text}");
            Summen();
        }
        else if (dauer > LangesBild)
        {
            _langeSumme += dauer;
            _langeAnzahl++;
            _letztesLanges = seit;
            GD.Print($"Ladezeit: Bild {_bild} dauert {dauer} ms (bei {seit} ms nach dem Aufbau), Pipelines bisher +{Pipelines().Summe - _pipelinesStart}");
            Summen();
        }
        else _summen.Clear();
        _ruhig = _bild > 1 && dauer <= LangesBild ? _ruhig + 1 : 0;
        if (Bilderordner is { } ordner && _baum?.Root is { } fenster)
        {
            var stufe = _bild == 1 ? 1 : seit >= 3000 ? 3 : seit >= 1000 ? 2 : 0;
            if (stufe > _fotoStufe)
            {
                _fotoStufe = stufe;
                Foto(fenster, ordner, stufe == 1 ? "1-erstes-bild" : stufe == 2 ? "2-nach-1s" : "3-nach-3s");
            }
        }
        if (_ruhig < RuhigeBilder && seit < 30000) return;
        RenderingServer.FramePostDraw -= BildFertig;
        var (summe, text) = Pipelines();
        GD.Print($"Ladezeit: ruhig, letztes langes Bild bei {_letztesLanges} ms nach dem Aufbau ({_bild} Bilder, {_langeAnzahl} lange mit {_langeSumme} ms, " +
                 $"{Engine.GetFramesPerSecond():0} Bilder/s), Pipelines in den Bildern +{summe - _pipelinesStart}, insgesamt: {text}");
        if (Bilderordner is { } o && _baum?.Root is { } f) Foto(f, o, "4-ruhig");
        GD.Print($"Ladezeit: fertig bei {jetzt} ms");
        if (Wert == "ende") _baum?.Quit();
    }

    private static void Foto(Viewport fenster, string ordner, string name)
    {
        DirAccess.MakeDirRecursiveAbsolute(ordner);
        var bild = fenster.GetTexture().GetImage();
        var fehler = bild?.SavePng(System.IO.Path.Combine(ordner, name + ".png"));
        GD.Print($"Ladezeit: Foto {name}: {fehler?.ToString() ?? "kein Bild"}");
    }
}
