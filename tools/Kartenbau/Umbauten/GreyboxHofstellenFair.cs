using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Kartenbau.Umbauten;

// ═════════════════════════════════════════════════════════════════════════════
//  Greybox: faire Start-Hofstellen (Auftrag des Kerns 9.11.2026)
//
//  Seit Spieler und KI-Häuser die Hofstellen losen, muss jede Start-Hofstelle im Band ±10 % um den Mittelwert liegen (Karrenweg der Werkstatt zum
//  nächsten Marktplatz, Regel „startweg“). Vorher: Tischlerei Eichwald (H3) 10,6 m und Maurerei Kieselbach (H4) 25,1 m gegen H1 20,3 m und H2 18,1 m.
//    • H3 Tischlerei Eichwald rückt von 22 | −13 nach 24 | −13; ihre Gasse läuft nicht mehr nach Westen zum Markt, sondern 6,5 m nach Süden auf
//      die Ostgasse und auf ihr zum Markt (die Ostgasse hat dort keinen Knoten, die Gasse mündet in ihre Mitte, wie die Domgasse in die Westgasse).
//    • H4 Maurerei Kieselbach rückt von 25 | −29 nach 24 | −29; ihre Gasse läuft nach dem Rücken von H3 nicht mehr um die Ecke bei x = 11 (zwischen
//      Rathaus und Eichwald), sondern von 24 | −22,5 nach Westen bis 15,5 | −22,5 und von dort geradeaus zum Markt (hält 2 m zur Ecke des Rathauses).
//    • Wohnhäuser, andere Gebäude und Wege bleiben. Danach „bauzonen greybox“ (die Raster bleiben, die Rechnung zur Probe).
//  Aufruf: dotnet run --project tools/Kartenbau -- umbauen greybox-hofstellen-fair   (auf der Greybox nach „hofstellen-greybox“)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class GreyboxHofstellenFair : Umbau
{
    public override string Kennung => "greybox-hofstellen-fair";
    public override string Karte => "greybox";
    public override string Beschreibung => "Tischlerei Eichwald und Maurerei Kieselbach rücken, so dass alle vier Start-Hofstellen im Band ±10 % liegen";

    public override void Anwenden(Kartenentwurf e, TextWriter aus)
    {
        // H3: Werkstatt 22 → 24, Gasse auf die Ostgasse (3 Punkte → 2)
        e.Verschieben("Tischlerei_Eichwald", 24, -13);
        var g3 = e.Wege.Single(w => w.Name == "Gasse Tischlerei Eichwald");
        if (g3.Verlauf.Count == 3)
        {
            e.StuetzpunktLoeschen(g3, 2);
            e.StuetzpunktVerschieben(g3, 0, 24, -6.5, einrasten: false);
            e.StuetzpunktVerschieben(g3, 1, 24, 0, einrasten: false);
        }

        // H4: Werkstatt 25 → 24, Gasse ohne die Ecke bei (11 | −13), geradeaus zum Markt
        e.Verschieben("Maurerei_Kieselbach", 24, -29);
        var g4 = e.Wege.Single(w => w.Name == "Gasse Maurerei Kieselbach");
        if (g4.Verlauf.Count == 4)
        {
            e.StuetzpunktLoeschen(g4, 2);
            e.StuetzpunktVerschieben(g4, 0, 24, -22.5, einrasten: false);
            e.StuetzpunktVerschieben(g4, 1, 15.5, -22.5, einrasten: false);
        }
        aus.WriteLine("   Tischlerei_Eichwald 24 | −13 (Gasse auf die Ostgasse), Maurerei_Kieselbach 24 | −29 (Gasse geradeaus zum Markt)");
    }
}
