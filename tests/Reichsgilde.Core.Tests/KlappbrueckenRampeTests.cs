using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Die Rampen der Klappbrücke (<see cref="Wasserplan.KlappRampe"/>): je Ufer 12 m, damit Karren das Deck (1,4 m über dem Ufer) mit höchstens
/// 12 % Steigung erreichen; endet der Weg vorher, geht die Rampe geradeaus weiter. Andere Brücken und Karten bleiben ohne Rampe (0) und
/// sonst bitgleich (das prüfen die Gegenproben in <see cref="KlappbrueckenPfeilerTests"/>).
/// </summary>
static class KlappbrueckenRampeTests
{
    static Stadtkarte Karte(Wirtschaftsdaten daten, string karte)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, new Spielzeit(0), daten);
        sim.Wirtschaft!.KarteSetzen(Stadtkarte.AusDatei(Datenpfad.Karte(karte)));
        return sim.Wirtschaft.Karte!;
    }

    static double Abstand(Kartenort a, Kartenort b) => a.Abstand(b);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Klappbrücke Köln: Rampen je Ufer 12 m auf der Linie der Brücke, höchstens 12 % Steigung; Rampe B reicht über das Ende der " +
         "Rheinbrücke hinaus; Fahrbahn (Anfang, Ende, Stellen) unverändert", () =>
        {
            var mit = Karte(KlappbrueckeTests.DatenMit(_ => { }), "koeln");
            var ohne = Karte(KlappbrueckeTests.OhneKlappbruecke(), "koeln");
            var q = mit.Wasserplan.Bruecken.Single(b => b.Karren && b.GewaesserName == "Rhein");
            var alt = ohne.Wasserplan.Bruecken.Single(b => b.Karren && b.GewaesserName == "Rhein");
            Pruefe.Gleich(Wasserplan.KlappRampe, q.Rampe);
            Pruefe.Gleich(0.0, alt.Rampe);
            Pruefe.Gleich(default(Kartenort), alt.RampeAnfang);
            Pruefe.Ungefaehr(12.0, Abstand(q.RampeAnfang, q.UferA), 1e-9);
            Pruefe.Ungefaehr(12.0, Abstand(q.RampeEnde, q.UferB), 1e-9);
            // auf der Geraden der Brücke (die Rheinbrücke ist ein gerades Stück): Ufer A liegt zwischen Rampenfuß A und Ufer B
            Pruefe.Ungefaehr(Abstand(q.RampeAnfang, q.UferB), 12 + Abstand(q.UferA, q.UferB), 1e-9);
            Pruefe.Ungefaehr(Abstand(q.UferA, q.RampeEnde), 12 + Abstand(q.UferA, q.UferB), 1e-9);
            // Rampe B reicht über das Ende des Weges hinaus (die Rheinbrücke endet an der Deutzer Freiheit)
            Pruefe.Wahr(Abstand(q.UferB, q.Ende) < 12, $"Weg endet {Abstand(q.UferB, q.Ende):F2} m hinter Ufer B");
            Pruefe.Wahr(q.RampeEnde.X > q.Ende.X, "Rampe B geht geradeaus weiter");
            // Steigung je Ufer höchstens 12 %
            foreach (var fuss in new[] { q.RampeAnfangHoehe, q.RampeEndeHoehe })
                Pruefe.Wahr((q.Deckhoehe - fuss) / q.Rampe <= 0.12, $"Steigung {(q.Deckhoehe - fuss) / q.Rampe:P1}");
            // nur Darstellung: Fahrbahn und Stellen für das Karrennetz wie ohne Klappbrücke
            Pruefe.Gleich(alt.VonStelle, q.VonStelle);
            Pruefe.Gleich(alt.BisStelle, q.BisStelle);
            Pruefe.Gleich(alt.Anfang, q.Anfang);
            Pruefe.Gleich(alt.Ende, q.Ende);
            Pruefe.Gleich(alt.AnfangHoehe, q.AnfangHoehe);
            Pruefe.Gleich(alt.EndeHoehe, q.EndeHoehe);
        }),
        ("Klappbrücke: alle anderen Querungen (Köln, Greybox, anderes Gewässer) ohne Rampe", () =>
        {
            foreach (var k in new[] { "koeln", "greybox" })
            {
                var plan = Karte(KlappbrueckeTests.DatenMit(_ => { }), k).Wasserplan;
                Pruefe.Gleich(k == "koeln" ? 1 : 0, plan.Querungen.Count(q => q.Rampe > 0), k);
                foreach (var q in plan.Querungen.Where(q => q.Durchfahrt == 0))
                {
                    Pruefe.Gleich(0.0, q.Rampe, $"{k} {q.WegName}");
                    Pruefe.Gleich(default(Kartenort), q.RampeEnde, $"{k} {q.WegName}");
                }
            }
            var duffes = Karte(KlappbrueckeTests.DatenMit(_ => { }), "koeln");
            duffes.KlappbrueckeSetzen("Duffesbach");
            Pruefe.Wahr(duffes.Wasserplan.Querungen.All(q => q.Rampe == 0), "Duffesbach: keine Steinbrücke mit 21 m Weite");
        }),
    };
}
