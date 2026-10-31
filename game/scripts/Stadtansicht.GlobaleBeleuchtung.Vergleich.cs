using System.Globalization;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Vergleich mit und ohne globale Beleuchtung (Startoption <c>--givergleich=&lt;Stufen&gt;</c>, z. B. <c>0,2</c> = Aus und Mittel) – ein Teil der
/// <c>partial class Stadtansicht</c>. Mit <c>--innen=…</c> geht er jeden Raum der Raumleiste durch, sonst misst er den Blick der Stadt
/// (dann mit <c>--sdfgi-aussen=1</c>). Je Raum und Stufe: umschalten, warten, bis das Bild wieder flüssig läuft (das erste Einschalten übersetzt
/// Shader) und sich das Licht gesetzt hat (4 s), dann 4 s lang die GPU-Zeit messen (Median), ein Bildschirmfoto speichern und die mittlere Helligkeit
/// ins Log schreiben (<c>[givergleich]</c>): Leuchtdichte nach Rec. 709 auf den sRGB-Werten 0–255, dazu das dunkelste und hellste Zehntel und das
/// Verhältnis Rot zu Blau (Farbstich). Zwei Runden (Aus, Mittel, Aus, Mittel), damit fremde Last auf der Grafikkarte nicht nur eine Stufe trifft;
/// am Ende je Raum eine Zusammenfassung, dann beendet sich das Spiel. Die Bilder liegen in <c>--givergleich-ordner=&lt;Ordner&gt;</c>
/// (Standard <c>user://givergleich</c>). Die Uhrzeit des Lichts wählt <c>--lichtuhr</c>, ob gearbeitet wird (Glut, Lampen) <c>--startstunde</c>.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Die Stufen des Vergleichs aus <c>--givergleich</c> (leer: kein Vergleich); „1“ heißt Aus und Mittel.</summary>
    private static readonly GlobaleBeleuchtung[] GiVergleichStufen = GiVergleichLesen();

    private static GlobaleBeleuchtung[] GiVergleichLesen()
    {
        var wert = Wasserarten.Startoption("givergleich");
        if (string.IsNullOrEmpty(wert)) return Array.Empty<GlobaleBeleuchtung>();
        if (wert == "1") return new[] { GlobaleBeleuchtung.Aus, GlobaleBeleuchtung.Mittel };
        return wert.Split(',', StringSplitOptions.RemoveEmptyEntries)
                   .Select(t => Enum.TryParse<GlobaleBeleuchtung>(t, true, out var s) && Enum.IsDefined(s) ? s : GlobaleBeleuchtung.Aus)
                   .ToArray();
    }

    /// <summary>Vorlauf nach dem Start, Wartezeit nach dem Umschalten (sobald das Bild flüssig läuft; SDFGI legt sein Feld in den ersten Sekunden an) und Messdauer, in Sekunden.</summary>
    private const double GiVergleichVorlauf = 10, GiVergleichRuheAus = 1.5, GiVergleichRuheGi = 4, GiVergleichMessdauer = 4;
    private const int GiVergleichRunden = 2;

    /// <summary>Ein Schritt: Gebäude (null = Stadt), Raum (Kennung, leer = Stadt), Runde und Stufe.</summary>
    private readonly record struct GiSchritt(InnenAnsicht? Gebaeude, string Raum, string Name, int Runde, GlobaleBeleuchtung Stufe);

    private List<GiSchritt>? _gvSchritte;
    private int _gvNummer = -1;
    private double _gvZeit = -GiVergleichVorlauf;
    /// <summary>Wie viele Bilder in Folge flüssig waren (unter 0,1 s), seit umgeschaltet wurde.</summary>
    private int _gvFluessig;
    private bool _gvMisst;
    private readonly List<double> _gvGpu = new();
    private readonly List<(GiSchritt Schritt, double Hell, double Dunkel, double HellZehntel, double RotZuBlau, double Gpu)> _gvErgebnis = new();

    /// <summary>Jedes Bild (aus <c>_Process</c>), nur mit <c>--givergleich</c>.</summary>
    private void GiVergleichSchritt(double delta)
    {
        if (GiVergleichStufen.Length == 0) return;
        _gvZeit += delta;
        if (_gvZeit < 0) return;
        var rid = GetTree().Root.GetViewportRid();
        if (_gvSchritte is null)
        {
            RenderingServer.ViewportSetMeasureRenderTime(rid, true);
            if (_simulation is not null) _simulation.Takt.IstPausiert = true; // Arbeit, Glut und Figuren bleiben, wie sie sind: beide Stufen zeigen dasselbe
            GetViewport().WarpMouse(new Vector2(40, 40)); // oben links, außerhalb der Randzone (sie schwenkt die Kamera): kein Schild einer Figur im Bild
            _gvSchritte = new();
            // Das betretene Gebäude, dazu mit --givergleich-auch=wohnhaus,rathaus das eigene Wohnhaus und das Rathaus
            var gebaeude = new List<InnenAnsicht?> { _innen is { Raumansicht: true } ? _innen : null };
            foreach (var weiteres in (Wasserarten.Startoption("givergleich-auch") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                if ((weiteres == "wohnhaus" ? _wohnhausAnsicht : AnsichtZu(weiteres == "rathaus" ? "Rathaus" : weiteres)) is { Raumansicht: true } ansicht)
                    gebaeude.Add(ansicht);
            if (gebaeude.Count > 1 && gebaeude[0] is null) gebaeude.RemoveAt(0); // nur die weiteren, nicht die Stadt
            foreach (var g in gebaeude)
                foreach (var (kennung, name) in g is null ? new() { ("", "Stadt") } : Zugaengliche(g).Select(r => (r.Kennung, r.Name)).ToList())
                    for (var runde = 0; runde < GiVergleichRunden; runde++)
                        foreach (var stufe in GiVergleichStufen) _gvSchritte.Add(new GiSchritt(g, kennung, name, runde, stufe));
            GD.Print($"[givergleich] {_gvSchritte.Count} Schritte in {gebaeude.Count} Gebäuden, {RenderingServer.GetVideoAdapterName()}, Lichtuhr {LichtUhrErzwungen?.ToString("0.##", CultureInfo.InvariantCulture) ?? "frei"}");
            GiVergleichWeiter();
            return;
        }

        // Einschwingen: erst flüssig (das erste Einschalten übersetzt Shader), dann ruhig, bis sich das Licht gesetzt hat
        var schritt = _gvSchritte[_gvNummer];
        if (!_gvMisst)
        {
            _gvFluessig = delta < 0.1 ? _gvFluessig + 1 : 0;
            if (_gvFluessig < 20) { _gvZeit = 0; return; }
            if (_gvZeit < (schritt.Stufe == GlobaleBeleuchtung.Aus ? GiVergleichRuheAus : GiVergleichRuheGi)) return;
            _gvMisst = true;
            _gvZeit = 0;
            _gvGpu.Clear();
            return;
        }
        _gvGpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid));
        if (_gvZeit < GiVergleichMessdauer) return;

        // Bild des letzten fertigen Bildes: Helligkeit messen und speichern
        var bild = GetViewport().GetTexture().GetImage();
        var (hell, dunkel, hellZehntel, rotZuBlau) = GiHelligkeit(bild);
        var gpu = Median(_gvGpu);
        _gvErgebnis.Add((schritt, hell, dunkel, hellZehntel, rotZuBlau, gpu));
        var ordner = Wasserarten.Startoption("givergleich-ordner") is { Length: > 0 } o ? o : "user://givergleich";
        DirAccess.MakeDirRecursiveAbsolute(ordner);
        var datei = $"{GiDateiname(schritt)}_{schritt.Stufe}_{schritt.Runde + 1}.png";
        bild.SavePng(ordner.PathJoin(datei));
        GD.Print(string.Create(CultureInfo.InvariantCulture,
            $"[givergleich] {schritt.Name} ({schritt.Raum}) | {schritt.Stufe} | Runde {schritt.Runde + 1} | Helligkeit {hell:0.0} (dunkelstes Zehntel {dunkel:0.0}, hellstes {hellZehntel:0.0}) | Rot/Blau {rotZuBlau:0.00} | GPU {gpu:0.00} ms | {datei}"));
        _gvBilder[schritt.Stufe] = bild;
        var letzterDesRaums = _gvNummer + 1 >= _gvSchritte.Count || _gvSchritte[_gvNummer + 1].Raum != schritt.Raum || _gvSchritte[_gvNummer + 1].Gebaeude != schritt.Gebaeude;
        if (letzterDesRaums) GiBildpaarSpeichern(ordner.PathJoin($"{GiDateiname(schritt)}_paar.png"));
        GiVergleichWeiter();
    }

    /// <summary>Gebäude und Raum als Dateiname, z. B. „rathaus_rat“ oder „werk“ (die Werkstatt des Spielers ohne Vorsilbe).</summary>
    private static string GiDateiname(GiSchritt s)
    {
        if (s.Raum == "") return "stadt";
        var raum = s.Raum[(s.Raum.LastIndexOf(':') + 1)..]; // „raum:werk“ → „werk“, „rathaus:kerker“ → „kerker“ (kein Doppelpunkt im Dateinamen, Windows)
        return s.Gebaeude is BetriebsAnsicht ? raum : $"{s.Gebaeude!.Kennung.Split(':')[0].ToLowerInvariant()}_{raum}";
    }

    /// <summary>Das letzte Bild je Stufe im gerade gemessenen Raum (für das Bildpaar).</summary>
    private readonly Dictionary<GlobaleBeleuchtung, Image> _gvBilder = new();

    /// <summary>Die Bilder der Stufen nebeneinander, jedes halb so groß, mit weißem Trenner – zum Ansehen ohne Bildprogramm.</summary>
    private void GiBildpaarSpeichern(string pfad)
    {
        var bilder = GiVergleichStufen.Distinct().Where(_gvBilder.ContainsKey).Select(s => _gvBilder[s]).ToList();
        _gvBilder.Clear();
        if (bilder.Count < 2) return;
        var (b, h) = (bilder[0].GetWidth() / 2, bilder[0].GetHeight() / 2);
        const int trenner = 6;
        var paar = Image.CreateEmpty(bilder.Count * b + (bilder.Count - 1) * trenner, h, false, Image.Format.Rgba8);
        paar.Fill(Colors.White);
        for (var i = 0; i < bilder.Count; i++)
        {
            var klein = (Image)bilder[i].Duplicate();
            klein.Convert(Image.Format.Rgba8);
            klein.Resize(b, h, Image.Interpolation.Bilinear);
            paar.BlitRect(klein, new Rect2I(0, 0, b, h), new Vector2I(i * (b + trenner), 0));
        }
        paar.SavePng(pfad);
    }

    /// <summary>Den nächsten Schritt einstellen; nach dem letzten die Zusammenfassung und Ende.</summary>
    private void GiVergleichWeiter()
    {
        var vorher = _gvNummer >= 0 ? _gvSchritte![_gvNummer] : (GiSchritt?)null;
        if (++_gvNummer >= _gvSchritte!.Count)
        {
            GiVergleichAusgeben();
            return;
        }
        var schritt = _gvSchritte[_gvNummer];
        if (schritt.Gebaeude is { } g && g != _innen) Betreten(g);
        if (schritt.Raum != "" && (schritt.Raum != vorher?.Raum || schritt.Gebaeude != vorher?.Gebaeude)) RaumWaehlen(schritt.Raum);
        if (schritt.Stufe != Fenster.AktuelleGlobaleBeleuchtung) Fenster.GlobaleBeleuchtungSetzen(schritt.Stufe, speichern: false);
        _gvMisst = false;
        _gvFluessig = 0;
        _gvZeit = 0;
    }

    private void GiVergleichAusgeben()
    {
        foreach (var raum in _gvErgebnis.GroupBy(e => (e.Schritt.Gebaeude, e.Schritt.Raum, e.Schritt.Name)))
        {
            var aus = raum.Where(e => e.Schritt.Stufe == GlobaleBeleuchtung.Aus).ToList();
            var hellAus = aus.Count > 0 ? aus.Average(e => e.Hell) : 0;
            var gpuAus = aus.Count > 0 ? Median(aus.Select(e => e.Gpu).ToList()) : 0;
            var teile = new List<string>();
            foreach (var stufe in GiVergleichStufen.Distinct())
            {
                var werte = raum.Where(e => e.Schritt.Stufe == stufe).ToList();
                if (werte.Count == 0) continue;
                var hell = werte.Average(e => e.Hell);
                var gpu = Median(werte.Select(e => e.Gpu).ToList());
                var anteil = hellAus > 0 && stufe != GlobaleBeleuchtung.Aus ? $" ({(hell / hellAus - 1) * 100:+0;-0;0} %)" : "";
                var mehr = stufe != GlobaleBeleuchtung.Aus && aus.Count > 0 ? $" ({gpu - gpuAus:+0.0;-0.0;0.0})" : "";
                teile.Add($"{stufe} {hell:0.0}{anteil}, Rot/Blau {werte.Average(e => e.RotZuBlau):0.00}, GPU {gpu:0.0} ms{mehr}");
            }
            GD.Print(string.Create(CultureInfo.InvariantCulture, $"[givergleich] Ergebnis {raum.Key.Gebaeude?.Name ?? "Stadt"} / {raum.Key.Name}: {string.Join(" | ", teile)}"));
        }
        GD.Print("[givergleich] fertig");
        GetTree().Quit();
    }

    /// <summary>
    /// Mittlere Helligkeit eines Bildes: Leuchtdichte nach Rec. 709 (0,2126 R + 0,7152 G + 0,0722 B) auf den sRGB-Werten 0–255, dazu die Grenzen des
    /// dunkelsten und hellsten Zehntels (aus dem Verlauf der Werte) und das Verhältnis der mittleren Rot- zu Blauwerte.
    /// </summary>
    private static (double Mittel, double Dunkel, double Hell, double RotZuBlau) GiHelligkeit(Image bild)
    {
        if (bild.GetFormat() != Image.Format.Rgba8 && bild.GetFormat() != Image.Format.Rgb8) bild.Convert(Image.Format.Rgba8);
        var schritt = bild.GetFormat() == Image.Format.Rgb8 ? 3 : 4;
        var daten = bild.GetData();
        var verlauf = new long[256];
        double summe = 0, rot = 0, blau = 0;
        long anzahl = 0;
        for (var i = 0; i + 2 < daten.Length; i += schritt)
        {
            var l = 0.2126 * daten[i] + 0.7152 * daten[i + 1] + 0.0722 * daten[i + 2];
            summe += l;
            rot += daten[i];
            blau += daten[i + 2];
            verlauf[Math.Clamp((int)Math.Round(l), 0, 255)]++;
            anzahl++;
        }
        double Grenze(double anteil)
        {
            long bis = 0;
            for (var w = 0; w < 256; w++)
                if ((bis += verlauf[w]) >= anteil * anzahl) return w;
            return 255;
        }
        return (summe / Math.Max(1, anzahl), Grenze(0.1), Grenze(0.9), rot / Math.Max(1, blau));
    }
}
