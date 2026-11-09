using System.Globalization;
using System.Text;
using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Die Pinselmessung der Kartenwerkstatt (Startoption <c>--pinselmessung=&lt;datei.md&gt;</c>, Pinselmessung.cs): Nach dem Laden malt die Werkstatt
/// mit jeder Pinselart einen Strich aus <see cref="Tupfer"/> Tupfern über die Mitte des Spielfelds, je Tupfer ein Bild – wie eine Maus, die in jedem
/// Bild ein Stück weiterzieht. Gemessen werden je Tupfer die Dauer des Bildes und die Teilschritte (Pinselmessung.Dazu an den Stellen im Code), nach
/// dem Loslassen die Bilder, bis es wieder ruhig ist (die Spitze und die Summe). Wege und Bauzonen sind kein Pinsel: Bei ihnen zählt der eine Befehl
/// (Doppelklick) mit seinen Folgebildern. Danach schreibt sie die Tabelle (Markdown) und beendet das Spiel.
/// </summary>
public partial class Kartenwerkstatt
{
    /// <summary>So viele Tupfer hat ein Strich der Messung.</summary>
    private const int Tupfer = 30;

    /// <summary>Ein gemessenes Bild: Dauer und Teilschritte (µs).</summary>
    private sealed record Messbild(ulong Usec, Dictionary<string, ulong> Teile);

    /// <summary>Was ein Pinsel gekostet hat: Bilder im Leerlauf, je Tupfer, beim Loslassen und danach.</summary>
    private sealed record Pinselergebnis(string Name, List<Messbild> Leerlauf, List<Messbild> Tupfer, Messbild Loslassen, List<Messbild> Nachlauf);

    private async Task PinselmessungLaufen()
    {
        Engine.MaxFps = 0; // ohne Bremse: die Dauer eines Bildes ist dann die Arbeit darin
        var feld = _entwurf.Kartengrenzen.Spielfeld;
        var (cx, cz) = ((float)feld.Mitte.X, (float)feld.Mitte.Z);
        var ergebnisse = new List<Pinselergebnis>();
        var strich = Enumerable.Range(0, Tupfer).Select(i => new Vector2(cx - 40 + 80f * i / (Tupfer - 1), cz + 6 * Mathf.Sin(i * 0.4f))).ToList();

        async Task Pinsel(string name, Werkstattwerkzeug werkzeug, Action einstellen)
        {
            _stadt.WerkstattWerkzeugWaehlen(werkzeug);
            einstellen();
            ergebnisse.Add(await StrichMessen(name, strich));
        }

        await Pinsel("Boden: Erde (r 8)", Werkstattwerkzeug.Boden, () => (_stadt.BodenArt, _stadt.BodenRadierer) = (Bodentextur.Erde, false));
        await Pinsel("Gelände: Heben (r 12)", Werkstattwerkzeug.Gelaende, () => _stadt.GelaendeArtWaehlen(Pinselart.Heben));
        await Pinsel("Gelände: Glätten (r 12)", Werkstattwerkzeug.Gelaende, () => _stadt.GelaendeArtWaehlen(Pinselart.Glaetten));
        await Pinsel("Fels: Streuen (r 10, Blöcke)", Werkstattwerkzeug.Fels, () =>
        {
            _stadt.FelsModus = Stadtansicht.FelsArt.Streuen;
            _stadt.FelsGruppe = null;
        });
        // Felspinsel P3: neu verteilen und ausdünnen über die gerade gestreuten Stücke, danach radieren wie bisher (dann sind es weniger Stücke)
        await Pinsel("Fels: Neu verteilen (r 10, Größe, Drehung, Einsinken)", Werkstattwerkzeug.Fels, () =>
        {
            _stadt.FelsModus = Stadtansicht.FelsArt.Verteilen;
            _stadt.FelsVerteilenWurf = Felswurf.Groesse | Felswurf.Drehung | Felswurf.Einsinken;
        });
        await Pinsel("Fels: Ausdünnen bis 10 % (r 10)", Werkstattwerkzeug.Fels, () =>
        {
            _stadt.FelsModus = Stadtansicht.FelsArt.Radieren;
            _stadt.FelsAusduennen = 0.1;
        });
        await Pinsel("Fels: Radieren (r 10)", Werkstattwerkzeug.Fels, () =>
        {
            _stadt.FelsModus = Stadtansicht.FelsArt.Radieren;
            _stadt.FelsAusduennen = 0;
        });
        await Pinsel("Natur: Bäume streuen (r 6)", Werkstattwerkzeug.Pinsel, () => { });
        await Pinsel("Natur: Radierer (r 6)", Werkstattwerkzeug.Radierer, () => { });
        _stadt.WerkstattWerkzeugWaehlen(Werkstattwerkzeug.Auswahl);
        ergebnisse.Add(await BefehlMessen("Weg anlegen (Gasse, 3 Punkte)", () =>
            _stadt.WerkstattWegAnlegen(Wegklasse.Gasse, new List<Kartenort> { new(cx - 30, cz + 24), new(cx, cz + 30), new(cx + 30, cz + 24) }, true)));
        ergebnisse.Add(await BefehlMessen("Bauzone anlegen (30 × 30 m)", () =>
            _entwurf.BauzoneAnlegen(Lage.Vorstadt, new List<Kartenort> { new(cx - 15, cz - 60), new(cx + 15, cz - 60), new(cx + 15, cz - 30), new(cx - 15, cz - 30) })));

        var text = Tabelle(ergebnisse);
        GD.Print(text);
        if (Pinselmessung.Datei is { } datei)
        {
            System.IO.File.WriteAllText(datei, text);
            GD.Print($"Pinselmessung: geschrieben nach {datei}");
        }
        GetTree().Quit();
    }

    /// <summary>Wartet ein Bild und gibt seine Dauer (seit <paramref name="anfang"/>) mit den Teilschritten darin zurück.</summary>
    private async Task<Messbild> BildMessen(ulong anfang)
    {
        await Ladebildschirm.Bild(GetTree());
        return new Messbild(Time.GetTicksUsec() - anfang, Pinselmessung.Abholen());
    }

    /// <summary>Wartet, bis die Bilder ruhig sind (drei in Folge unter 25 ms, höchstens 300 Bilder) – und gibt sie zurück.</summary>
    private async Task<List<Messbild>> RuhigWarten()
    {
        var bilder = new List<Messbild>();
        var ruhig = 0;
        while (ruhig < 3 && bilder.Count < 300)
        {
            var b = await BildMessen(Time.GetTicksUsec());
            bilder.Add(b);
            // ruhig erst, wenn auch die Nacharbeit und die aufgeschobene Prüfung (0,3 s nach dem Strich) gelaufen sind
            var offen = _stadt.WerkstattNacharbeitOffen || _stadt.WerkstattPruefungAufschieben || _auffrischenNachStrich || _auffrischenVorgemerkt;
            ruhig = b.Usec < 25_000 && !offen ? ruhig + 1 : 0;
        }
        return bilder;
    }

    private async Task<Pinselergebnis> StrichMessen(string name, List<Vector2> strich)
    {
        await RuhigWarten();
        var leer = new List<Messbild>();
        for (var i = 0; i < 10; i++) leer.Add(await BildMessen(Time.GetTicksUsec()));
        var tupfer = new List<Messbild>();
        for (var i = 0; i < strich.Count; i++)
        {
            var anfang = Time.GetTicksUsec();
            var uhr = Pinselmessung.Uhr();
            if (i == 0) _stadt.MessStrichBeginnen(strich[i]);
            else _stadt.MessStrichWeiter(strich[i]);
            Pinselmessung.Dazu("Kern: Tupfer (Raster, Meldungen)", uhr);
            tupfer.Add(await BildMessen(anfang));
        }
        var los = Time.GetTicksUsec();
        var uhrLos = Pinselmessung.Uhr();
        _stadt.MessStrichEnde();
        Pinselmessung.Dazu("Loslassen (Kern: Schritt, Rückgängig)", uhrLos);
        var loslassen = await BildMessen(los);
        var nach = await RuhigWarten();
        GD.Print($"Pinselmessung: {name}: Tupfer im Mittel {Ms(tupfer.Average(b => (double)b.Usec))} ms, Loslassen {Ms(loslassen.Usec)} ms");
        return new Pinselergebnis(name, leer, tupfer, loslassen, nach);
    }

    private async Task<Pinselergebnis> BefehlMessen(string name, Func<object?> befehl)
    {
        await RuhigWarten();
        var leer = new List<Messbild>();
        for (var i = 0; i < 10; i++) leer.Add(await BildMessen(Time.GetTicksUsec()));
        var anfang = Time.GetTicksUsec();
        var uhr = Pinselmessung.Uhr();
        try
        {
            befehl();
        }
        catch (ArgumentException e)
        {
            GD.PushWarning($"Pinselmessung: {name} ging nicht: {e.Message}");
        }
        Pinselmessung.Dazu("Loslassen (Kern: Schritt, Rückgängig)", uhr);
        var bild = await BildMessen(anfang);
        var nach = await RuhigWarten();
        return new Pinselergebnis(name, leer, new List<Messbild>(), bild, nach);
    }

    private static string Ms(double usec) => (usec / 1000).ToString("0.0", CultureInfo.GetCultureInfo("de-DE"));

    private static double Median(IEnumerable<double> werte)
    {
        var l = werte.OrderBy(x => x).ToList();
        return l.Count == 0 ? 0 : l.Count % 2 == 1 ? l[l.Count / 2] : (l[l.Count / 2 - 1] + l[l.Count / 2]) / 2;
    }

    /// <summary>Die Tabelle: je Pinsel eine Zeile, darunter je Pinsel die Teilschritte.</summary>
    private string Tabelle(List<Pinselergebnis> ergebnisse)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## Pinselmessung {_entwurf.Stadt} ({_entwurf.Kennung})");
        sb.AppendLine();
        sb.AppendLine($"Strich aus {Tupfer} Tupfern (je Tupfer ein Bild) über 80 m durch die Mitte des Spielfelds; headless, nur Rechenzeit (ms).");
        sb.AppendLine();
        sb.AppendLine("| Pinsel | Leerlauf je Bild | Bild je Tupfer: Median | Mittel | Max | Loslassen (Bild) | danach: Spitze | danach: Summe (Bilder) |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var e in ergebnisse)
        {
            var leer = Median(e.Leerlauf.Select(b => (double)b.Usec));
            var t = e.Tupfer.Select(b => (double)b.Usec).ToList();
            var nachSpitze = e.Nachlauf.Count == 0 ? 0 : e.Nachlauf.Max(b => (double)b.Usec);
            var nachSumme = e.Nachlauf.Sum(b => (double)b.Usec);
            sb.AppendLine($"| {e.Name} | {Ms(leer)} | {(t.Count == 0 ? "–" : Ms(Median(t)))} | {(t.Count == 0 ? "–" : Ms(t.Average()))} | {(t.Count == 0 ? "–" : Ms(t.Max()))} | "
                          + $"{Ms(e.Loslassen.Usec)} | {Ms(nachSpitze)} | {Ms(nachSumme)} ({e.Nachlauf.Count}) |");
        }
        sb.AppendLine();
        sb.AppendLine("Teilschritte (ms): je Tupfer im Mittel und das teuerste Bild; beim Loslassen und in den Bildern danach als Summe.");
        foreach (var e in ergebnisse)
        {
            sb.AppendLine();
            sb.AppendLine($"### {e.Name}");
            sb.AppendLine();
            sb.AppendLine("| Teilschritt | je Tupfer: Mittel | je Tupfer: Max | Loslassen | danach (Summe) |");
            sb.AppendLine("|---|---:|---:|---:|---:|");
            var namen = e.Tupfer.SelectMany(b => b.Teile.Keys).Concat(e.Loslassen.Teile.Keys).Concat(e.Nachlauf.SelectMany(b => b.Teile.Keys))
                .Distinct().OrderBy(n => n.TrimStart(), StringComparer.Ordinal).ToList();
            foreach (var n in namen)
            {
                var je = e.Tupfer.Select(b => (double)b.Teile.GetValueOrDefault(n)).ToList();
                var nach = e.Nachlauf.Sum(b => (double)b.Teile.GetValueOrDefault(n));
                if (je.Sum() + nach + e.Loslassen.Teile.GetValueOrDefault(n) < 50) continue; // nie mehr als 0,05 ms: weglassen
                sb.AppendLine($"| {n} | {(je.Count == 0 ? "–" : Ms(je.Average()))} | {(je.Count == 0 ? "–" : Ms(je.Max()))} | {Ms(e.Loslassen.Teile.GetValueOrDefault(n))} | {Ms(nach)} |");
            }
            // der Rest des Bildes: Godot selbst (Szene, Physik, Zeichnen) und der Leerlauf von headless (etwa 6,9 ms je Bild)
            static double Rest(Messbild b) => b.Usec - (double)b.Teile.Values.Aggregate(0UL, (a, x) => a + x);
            var rest = e.Tupfer.Select(Rest).ToList();
            sb.AppendLine($"| (übriges Bild: Godot, Leerlauf) | {(rest.Count == 0 ? "–" : Ms(rest.Average()))} | {(rest.Count == 0 ? "–" : Ms(rest.Max()))} | {Ms(Rest(e.Loslassen))} | {Ms(e.Nachlauf.Sum(Rest))} |");
        }
        return sb.ToString();
    }
}
