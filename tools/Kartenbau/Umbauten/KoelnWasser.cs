using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Kartenbau.Umbauten;

// ═════════════════════════════════════════════════════════════════════════════
//  Köln auf das Wasser von Schritt 5c umstellen (Kartenwerkstatt, Schritt 5c)
//
//  Köln hatte Rhein, Duffesbach, Strunde und den Altrheinarm schon in seiner Liste „wasser“ – bisher nur als Bild, ohne Wirkung. Seit 5c ist
//  Wasser ein Hindernis: Nichts steht darin, Brücken entstehen, wo ein Weg quert, und ein Fluss fließt in Zeichenrichtung. So passte Köln nicht
//  mehr:
//    • Der Rhein lief in Zeichenrichtung nach Süden (er fließt nach Norden) und war mit 30 m so breit, dass Heumarkt und Hafen darin standen
//      und der westliche Anleger der Fähre Mülheim im Wasser lag. Jetzt: umgekehrt, 21 m breit (Köln ist ohnehin gestaucht: Deutz liegt
//      40 m vom Ufer) und bis z = 60 um 11 m nach Osten verschoben, nach Süden bis z = 120 auslaufend (dort liegt die Bauzone U4 am Ufer).
//    • Der Duffesbach lief mitten durch die Bauzonen K5, K6, M8 und den Werkstattplatz S6 (im Mittelalter floss er überwölbt durch die Stadt).
//      Er fällt weg, bis das Stadtskript von Köln ihn neu führt.
//    • Die „Rheinbrücke“ war ein Eintrag in „uebergaenge“; jetzt ist sie eine Hauptstraße von der Brückenauffahrt nach Deutz – die
//      Steinbrücke entsteht von selbst.
//  Aufruf: dotnet run --project tools/Kartenbau -- umbauen koeln-wasser
// ═════════════════════════════════════════════════════════════════════════════

public sealed class KoelnWasser : Umbau
{
    public override string Kennung => "koeln-wasser";
    public override string Karte => "koeln";
    public override string Beschreibung => "Rhein umgekehrt, 21 m breit, bis 11 m nach Osten; Duffesbach weg; Rheinbrücke als Straße (Schritt 5c)";

    /// <summary>Um so viel rückt der Rhein bei z nach Osten: 11 m bis z = 60, dann bis z = 120 auf 0.</summary>
    private static double Versatz(double z) => 11 * Math.Clamp((120 - z) / 60, 0, 1);

    public override void Anwenden(Kartenentwurf e, TextWriter aus)
    {
        var rhein = e.Fluesse.Single(f => f.Name == "Rhein");
        e.FlussUmkehren(rhein);
        for (var i = 0; i < rhein.Punkte.Count; i++)
            e.FlussPunktVerschieben(rhein, i, rhein.Punkte[i].X + Versatz(rhein.Punkte[i].Z), rhein.Punkte[i].Z);
        e.FlussBreiteSetzen(rhein, -1, 21);
        aus.WriteLine("   Rhein: umgekehrt (fließt nach Norden), 21 m breit, bis z = 60 um 11 m nach Osten (bis z = 120 auslaufend)");

        e.GewaesserLoeschen(e.Fluesse.Single(f => f.Name == "Duffesbach"));
        aus.WriteLine("   Duffesbach entfernt (lief durch K5, K6, M8 und S6)");

        var alt = e.Faehren.Single(f => f.Name == "Rheinbrücke" && !f.IstFaehre);
        var (von, bis) = (alt.Von, alt.Bis);
        e.FaehreLoeschen(alt);
        e.WegAnlegen(Wegklasse.Haupt, new[] { von, bis }, glatt: false, name: "Rheinbrücke");
        var bruecke = e.Wasserplan.Bruecken.Single(q => q.WegName == "Rheinbrücke");
        aus.WriteLine($"   Rheinbrücke: Hauptstraße von {von} nach {bis} – {bruecke.Art}, {Kartenschreiber.Zahl(Math.Round(bruecke.Laenge, 1))} m");
    }
}
