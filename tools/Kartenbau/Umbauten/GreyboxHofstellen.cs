using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Kartenbau.Umbauten;

// ═════════════════════════════════════════════════════════════════════════════
//  Greybox: Hofstellen (Entscheidung des Nutzers 9.11.2026)
//
//  Die Greybox hat kein Stadtskript (ihre Datei wird von Hand gepflegt, Änderungen laufen über einen Umbau). Beim Öffnen rechnet der Kern jedes
//  Startpaar – Wohnhausplatz und Werkstattplatz des Spielers und der drei KI-Häuser – zu einer Start-Hofstelle um (H1 bis H4: der Spieler,
//  Eisenbart, Eichwald, Kieselbach). Dieser Umbau schreibt sie in die Datei (start.hofstellen) und gibt den vieren ihre Namen nach der Lage:
//  der Name der Hofstelle ist die Lage für den Namen der Spielerwerkstatt („Gießerei am Weinmarkt“) und die Anzeige im Startmenü.
//  Sonst ändert sich an der Greybox nichts (Testkarte für Tests und Messungen; die Vorgabe der Startbelegung bleibt).
//  Aufruf: dotnet run --project tools/Kartenbau -- umbauen hofstellen-greybox
// ═════════════════════════════════════════════════════════════════════════════

public sealed class GreyboxHofstellen : Umbau
{
    public override string Kennung => "hofstellen-greybox";
    public override string Karte => "greybox";
    public override string Beschreibung => "Hofstellen H1–H4 aus den vier Startpaaren, mit Namen nach der Lage";

    /// <summary>Die Namen der Hofstellen (Kennung → Lage): der Spieler am Weinmarkt, die KI-Häuser nach ihrer Straße.</summary>
    public static readonly IReadOnlyDictionary<string, string> Namen = new Dictionary<string, string>
    {
        ["H1"] = "Weinmarkt",                     // der Spieler (Platz „Weinmarkt“)
        ["H2"] = "an der Ostgasse (Süd)",         // Gießerei Eisenbart
        ["H3"] = "an der Ostgasse (Nord)",        // Tischlerei Eichwald
        ["H4"] = "an der Ringgasse",              // Maurerei Kieselbach
    };

    public override void Anwenden(Kartenentwurf e, TextWriter aus)
    {
        foreach (var h in e.Start.Hofstellen.ToList())
        {
            if (Namen.TryGetValue(h.Kennung, out var name) && h.Name != name) e.HofstelleUmbenennen(h.Kennung, name);
            var jetzt = e.Start.HofstelleMit(h.Kennung)!;
            aus.WriteLine($"   {jetzt.Kennung} „{jetzt.Name ?? "–"}“: Werkstatt {jetzt.Werkstatt}, Wohnhaus {jetzt.Wohnhaus}{(jetzt.Start ? ", Start" : "")}");
        }
    }
}
