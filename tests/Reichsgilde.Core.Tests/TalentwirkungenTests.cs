using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Die Wirkungszahlen der Talent-Tooltips (<see cref="Talentwirkungen"/>) müssen dem entsprechen, was die Simulation wirklich rechnet.</summary>
static class TalentwirkungenTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static (Stadtwirtschaft W, Konto Konto, Person Oberhaupt) Haus()
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: Daten.Value);
        var w = sim.Wirtschaft!;
        var konto = w.KontoEroeffnen("Familie Tipp", startvermoegen: 50_000);
        var f = w.Familien.Gruenden(konto, Volk.Mensch, Geschlecht.Mann);
        foreach (var t in TalentTexte.Alle) f.Oberhaupt.TalentSetzen(t, 0);
        return (w, konto, f.Oberhaupt);
    }

    static double JeStern(Stadtwirtschaft w, Talent t, int zeile) => Talentwirkungen.Zeilen(w, t)[zeile].JeStern;

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Talentwirkungen: Zahlen je Stern stimmen mit dem Familienbuch überein", () =>
        {
            var (w, k, o) = Haus();
            var b = w.Familien;
            const double s = 5;
            // Was das Talent allein ändert: Wert mit s Sternen minus Wert mit 0 Sternen (Lebensweg-Boni sind Sache der Lebensweise).
            double Delta(Talent t, Func<double> wert)
            {
                o.TalentSetzen(t, 0);
                var ohne = wert();
                var sterneOhne = o.Talent(t); // Züge können das Talent verschieben, darum die wirklichen Sterne messen
                o.TalentSetzen(t, s);
                var mit = wert();
                var sterneMit = o.Talent(t);
                o.TalentSetzen(t, 0);
                return (mit - ohne) / (sterneMit - sterneOhne);
            }
            Pruefe.Ungefaehr(JeStern(w, Talent.Verhandeln, 0), Delta(Talent.Verhandeln, () => b.VerkaufsFaktor(k)), 1e-9);
            Pruefe.Ungefaehr(JeStern(w, Talent.Verhandeln, 1), Delta(Talent.Verhandeln, () => b.EinkaufsFaktor(k)), 1e-9);
            Pruefe.Ungefaehr(JeStern(w, Talent.Handwerk, 0), Delta(Talent.Handwerk, () => b.LeistungsFaktor(k)), 1e-9);
            Pruefe.Ungefaehr(JeStern(w, Talent.NachtUndNebel, 0), Delta(Talent.NachtUndNebel, () => b.NachtBonus(k)), 1e-9);
            var zeugenOhne = b.ZeugenFaktor(k);
            Pruefe.Ungefaehr(JeStern(w, Talent.NachtUndNebel, 1), Delta(Talent.NachtUndNebel, () => b.ZeugenFaktor(k)) / zeugenOhne, 1e-9);
            Pruefe.Ungefaehr(JeStern(w, Talent.Kampf, 0), Delta(Talent.Kampf, () => b.KampfUeberfall(k)), 1e-9);
            Pruefe.Ungefaehr(JeStern(w, Talent.Kampf, 1), Delta(Talent.Kampf, () => b.KampfSchutz(k)), 1e-9);
            Pruefe.Ungefaehr(JeStern(w, Talent.Rhetorik, 0), Delta(Talent.Rhetorik, () => b.RhetorikPunkte(k)), 1e-9);
            Pruefe.Ungefaehr(JeStern(w, Talent.Rhetorik, 1), Delta(Talent.Rhetorik, () => b.PlaedoyerAllein(k)), 1e-9);
            if (w.Glaube.IstAktiv)
            {
                var anteil = w.Glaube.Daten.Bekehrung.Anteil;
                Pruefe.Ungefaehr(JeStern(w, Talent.Rhetorik, 3), Delta(Talent.Rhetorik, () => w.Glaube.Bekehrungskraft(k)) / anteil, 1e-9);
            }
        }),

        ("Talentwirkungen: jedes Talent hat Satz und mindestens eine Wirkungszeile; Zahlen lesbar", () =>
        {
            var (w, _, o) = Haus();
            foreach (var t in TalentTexte.Alle)
            {
                Pruefe.Wahr(t.Satz() != "", $"{t}: Satz fehlt");
                Pruefe.Wahr(Talentwirkungen.Zeilen(w, t).Count > 0, $"{t}: keine Wirkung");
            }
            var z = Talentwirkungen.Zeilen(w, Talent.Handwerk)[0];
            Pruefe.Gleich("+3 %", z.JeSternText);
            Pruefe.Gleich("+12 %", z.JetztText(4));
            Pruefe.Gleich("−8 %", Talentwirkungen.Zeilen(w, Talent.NachtUndNebel)[1].JeSternText);
            Pruefe.Gleich("+4 Prozentpunkte", Talentwirkungen.Zeilen(w, Talent.NachtUndNebel)[0].JeSternText);
            Pruefe.Gleich("+2 Punkte", Talentwirkungen.Zeilen(w, Talent.Rhetorik)[0].JeSternText);
            Pruefe.Gleich("+4,5 %", TalentWirkungsZeile.Formatieren(4.5, WirkungsEinheit.Prozent));
        }),

        ("Charakterzug: Meinungstext nennt die Meinungswerte aus den Daten", () =>
        {
            var (w, _, _) = Haus();
            var z = w.Familien.Zug("Gierig")!;
            Pruefe.Gleich("+10 über Träger desselben Zugs, −10 über Träger des Gegenteils", z.Meinungstext);
            Pruefe.Wahr(w.Familien.ZugDaten!.Alle.Any(x => x.Meinungstext == ""), "Zug ohne Meinungswirkung hat leeren Text");
        }),
    };
}
