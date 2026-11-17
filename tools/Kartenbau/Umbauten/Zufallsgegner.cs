using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Kartenbau.Umbauten;

// ═════════════════════════════════════════════════════════════════════════════
//  Keine festen Dynastien mehr (Entscheidung des Nutzers 17.11.2026)
//
//  Jede Partie würfelt ihre KI-Gegner (Name, Wappen, Volk, Glaube, Beruf). Die Karten nennen deshalb keine Häuser mehr. Was von ihnen bleibt:
//    • ihre Umlandbetriebe als Umland-Startplätze (Abschnitt „umland“, dieselbe Reihenfolge) – so kommen mit N Gegnern dieselben N Plätze
//      ins Spiel wie vorher, der Name des Betriebs am Platz bleibt („Grube Overstolz“);
//    • ihre Namen als Familiennamen der Stadt (Abschnitt „hausnamen“), aus denen die Partie würfelt.
//  Aufruf: dotnet run --project tools/Kartenbau -- umbauen zufallsgegner-koeln   (ebenso -greybox, -messstadt, -probestadt)
// ═════════════════════════════════════════════════════════════════════════════

public abstract class Zufallsgegner : Umbau
{
    public override string Kennung => "zufallsgegner-" + Karte;
    public override string Beschreibung => "feste KI-Häuser entfernen; ihre Umlandbetriebe werden Umland-Startplätze, ihre Namen Namen der Stadt";

    public override void Anwenden(Kartenentwurf e, TextWriter aus)
    {
        var namen = e.Start.Hausnamen.Concat(e.Start.Haeuser.Select(h => h.Name)).ToList();
        var umland = e.Start.Haeuser.SelectMany(h => h.Betriebe).Where(b => b.Platz != "").Select(b => b.Platz).ToList();
        var haeuser = e.Start.Haeuser.Count;
        for (var i = haeuser - 1; i >= 0; i--) e.HausEntfernen(i);
        foreach (var platz in umland) e.StartumlandHinzufuegen(platz);
        e.HausnamenSetzen(namen);
        aus.WriteLine($"   {haeuser} Häuser entfernt, {umland.Count} Umland-Startplätze, Namen: {string.Join(", ", e.Start.Hausnamen)}");
    }
}

public sealed class ZufallsgegnerKoeln : Zufallsgegner { public override string Karte => "koeln"; }
public sealed class ZufallsgegnerGreybox : Zufallsgegner { public override string Karte => "greybox"; }
public sealed class ZufallsgegnerMessstadt : Zufallsgegner { public override string Karte => "messstadt"; }
public sealed class ZufallsgegnerProbestadt : Zufallsgegner { public override string Karte => "probestadt"; }
