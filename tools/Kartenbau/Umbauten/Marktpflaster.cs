using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Kartenbau.Umbauten;

// ═════════════════════════════════════════════════════════════════════════════
//  Marktpflaster (Feinschliff, PR #212, Entscheidung des Nutzers 26.10.2026)
//
//  Das Modell des Marktplatzes (marktplatz.glb) hat keinen eigenen Boden mehr (sein Plattenpflaster war fleckig, im Schnee besonders). Der
//  Platz bekommt stattdessen das Kopfsteinpflaster der Gassen vom Kartenboden: die Textur „pflaster“ über der ganzen Grundfläche des
//  Marktbausteins. Nässe, Schnee und Übergänge wirken dann wie in den Gassen (Boden-Shader).
//  Aufruf: dotnet run --project tools/Kartenbau -- umbauen marktpflaster-<karte>   (greybox, koeln, probestadt, messstadt)
//  Eine neue Karte: im Stadtskript nach E.Setzen("markt", …) die Zeile MarktPflastern() (Stadtskript.cs), in der Kartenwerkstatt den Boden
//  mit der Textur „Pflaster“ über die Grundfläche des Marktes malen (Deko aus), bei Handarbeit Pinsel Radius 1, Kante 0,25, Tupfer bis an den Rand.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Malt das Kopfsteinpflaster unter jeden Marktplatz einer Karte.</summary>
public static class Marktpflaster
{
    /// <summary>Radius und Kantenweichheit des Pinsels: Die Tupfer reichen bis an den Rand der Grundfläche: Das Pflaster endet ½ m dahinter (die Rasterpunkte am Rand sind voll Pflaster).</summary>
    private const double Radius = 1, Kante = 0.25;

    /// <summary>Malt unter jedes Gebäude des Bausteins „markt“ die Textur Pflaster (ohne Deko). Gibt die Zahl der Plätze zurück.</summary>
    public static int Malen(Kartenentwurf e, TextWriter? aus = null)
    {
        var plaetze = 0;
        foreach (var markt in e.Gebaeude.Where(g => g.Katalog == "markt" && g.Groesse is not null))
        {
            var (breite, tiefe) = markt.Groesse!.Value;
            if (Math.Abs(markt.Drehung / 90 % 2) > 0.5) (breite, tiefe) = (tiefe, breite); // um 90° gedreht
            var (x0, x1) = (markt.X - breite / 2, markt.X + breite / 2);
            var (z0, z1) = (markt.Z - tiefe / 2, markt.Z + tiefe / 2);
            var strich = new List<Kartenort>();
            var hin = true;
            for (var z = z0; z <= z1 + 1e-9; z += Radius)
            {
                strich.Add(new Kartenort(hin ? x0 : x1, z));
                strich.Add(new Kartenort(hin ? x1 : x0, z));
                hin = !hin;
            }
            e.BodenMalen(new Bodenpinsel(Bodentextur.Pflaster, Radius, 1, Kante, Deko: false), strich);
            aus?.WriteLine($"   {markt.Name}: Pflaster {breite} × {tiefe} m um {markt.X} | {markt.Z}");
            plaetze++;
        }
        return plaetze;
    }
}

/// <summary>Ein Umbau, der das Marktpflaster malt – je Karte eine Unterklasse.</summary>
public abstract class MarktpflasterUmbau : Umbau
{
    public override string Beschreibung => "Kopfsteinpflaster unter dem Marktplatz (der Platz hat keinen eigenen Boden mehr)";
    public override void Anwenden(Kartenentwurf e, TextWriter aus)
    {
        if (Marktpflaster.Malen(e, aus) == 0) throw new InvalidOperationException("Die Karte hat keinen Marktplatz.");
    }
}

public sealed class MarktpflasterGreybox : MarktpflasterUmbau { public override string Kennung => "marktpflaster-greybox"; public override string Karte => "greybox"; }
public sealed class MarktpflasterKoeln : MarktpflasterUmbau { public override string Kennung => "marktpflaster-koeln"; public override string Karte => "koeln"; }
public sealed class MarktpflasterProbestadt : MarktpflasterUmbau { public override string Kennung => "marktpflaster-probestadt"; public override string Karte => "probestadt"; }
public sealed class MarktpflasterMessstadt : MarktpflasterUmbau { public override string Kennung => "marktpflaster-messstadt"; public override string Karte => "messstadt"; }
