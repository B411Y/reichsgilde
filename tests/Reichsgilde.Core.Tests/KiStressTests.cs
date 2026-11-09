using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests: Stress in den Entscheidungen der KI-Häuser (Charaktersystem Teil 3b) – angespannt meidet sie, was ihrem Oberhaupt Stress bringt,
/// mit übrigen Aktionspunkten nutzt sie ein Ventil, zermürbt rechnet sie vorsichtiger. Jeweils mit Gegenprobe.
/// </summary>
static class KiStressTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Simulation NeueSimulation() =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: Daten.Value);

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    /// <summary>Ein KI-Haus (eine Schmiede, keine Expansion) mit diesen Zügen des Oberhaupts, katholisch, 12 AP.</summary>
    static (Konto Konto, Person Oberhaupt, KiRivale Ki) KiHaus(Stadtwirtschaft w, string name, params string[] zuege)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: 20_000);
        var f = w.Familien.Gruenden(k, Volk.Mensch, Geschlecht.Mann, alter: 30, glaube: Konfession.Katholisch);
        var o = f.Oberhaupt;
        o.ZuegeLeeren();
        foreach (var z in zuege) o.ZugHinzufuegen(w.Daten.Charakterzuege!.Zug(z)!);
        k.Aktionspunkte.Setzen(12);
        w.BetriebEroeffnen($"Schmiede {name}", Betriebsart.Schmiede, k, startkarren: "");
        var ki = w.KiAnmelden(k, Partieaufbau.ProfilFuer(KiCharakter.Ausgewogen) with { MaxBetriebe = 0 });
        return (k, o, ki);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Stress: Angespannt (ab 40) meidet die KI, was ihrem Oberhaupt Stress bringt – ehrlich keine Bande, scheu kein Gastmahl; " +
         "ruhig oder verschlagen schon; ein Ventil bleibt erlaubt", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var ziel = w.KontoEroeffnen("Haus Ziel", startvermoegen: 5_000);
            w.Familien.Gruenden(ziel, Volk.Mensch, Geschlecht.Mann, alter: 30);
            var (_, ehrlich, kiEhrlich) = KiHaus(w, "Haus Redlich", "Ehrlich", "Scheu");
            var (_, schlau, kiSchlau) = KiHaus(w, "Haus Schlau", "Verschlagen");
            var bande = new Beauftragen(ziel.Inhaber, Gaunerstueck.Kroetenschleim);
            var r = w.Rat.Ratsherren[0];
            w.Familien.StressSetzen(ehrlich, 50);
            w.Familien.StressSetzen(schlau, 50);
            Pruefe.Wahr(kiEhrlich.StressMeiden(bande), "ehrlich und angespannt: keine Bande");
            Pruefe.Wahr(kiEhrlich.StressMeiden(new Aktivitaet("Gastmahl", r.Kennung)), "scheu und angespannt: kein Gastmahl");
            Pruefe.Wahr(!kiEhrlich.StressMeiden(new Aktivitaet("Messe", r.Kennung)), "ein Ventil bleibt erlaubt");
            Pruefe.Wahr(!kiSchlau.StressMeiden(bande), "Gegenprobe: verschlagen heuert an");
            w.Familien.StressSetzen(ehrlich, 30);
            Pruefe.Wahr(!kiEhrlich.StressMeiden(bande), "Gegenprobe: ruhig heuert auch ein Ehrlicher an (und bekommt Stress)");
        }),

        ("KI-Stress: Mit Stress ab 20 und übrigen Aktionspunkten nutzt die KI in der Politikstunde ein Ventil, das ihr Oberhaupt mag (gesellig: Wirtshaus); " +
         "darunter nicht", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var ruhe = new List<string>();
            w.Meldung += m => { if (m.Contains("sucht Ruhe vom Stress", StringComparison.Ordinal)) ruhe.Add(m); };
            var (_, gestresst, _) = KiHaus(w, "Haus Unruh", "Gesellig");
            var (_, wenig, _) = KiHaus(w, "Haus Stille", "Gesellig");
            w.Familien.StressSetzen(gestresst, 30);
            w.Familien.StressSetzen(wenig, 15);
            JahrBeenden(sim);
            Pruefe.Wahr(ruhe.Any(m => m.StartsWith("Haus Unruh:", StringComparison.Ordinal) && m.Contains("Im Wirtshaus zechen")), $"Ventil: {string.Join(" | ", ruhe)}");
            var ventil = gestresst.Stresslage.Eintraege.FirstOrDefault(e => e.Wert < 0 && !e.Text.StartsWith("Abklingen", StringComparison.Ordinal));
            Pruefe.Wahr(ventil is not null && ventil.Text.Contains("mag es (gesellig)"), $"Abbau: {string.Join(" | ", gestresst.Stresslage.Eintraege)}");
            Pruefe.Wahr(!ruhe.Any(m => m.StartsWith("Haus Stille:", StringComparison.Ordinal)), "Gegenprobe: unter 20 sucht sie keine Ruhe");
        }),

        ("KI-Stress: Zermürbt (ab 70) rechnet die KI vorsichtiger – Raubfurcht × 1,2; darunter wie vorher", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (_, o, ki) = KiHaus(w, "Haus Sorge");
            var vorher = ki.ErwarteteFahrtkosten("Keine", 1000);
            var erwartet = Kalkulation.ErwarteteFahrtkosten(w, "Keine", 1000, ki.Profil.Vorsicht * 1.2);
            w.Familien.StressSetzen(o, 50);
            Pruefe.Ungefaehr(vorher, ki.ErwarteteFahrtkosten("Keine", 1000), 1e-9); // angespannt: wie vorher
            w.Familien.StressSetzen(o, 75);
            Pruefe.Ungefaehr(erwartet, ki.ErwarteteFahrtkosten("Keine", 1000), 1e-9);
            Pruefe.Wahr(erwartet > vorher, $"vorsichtiger: {erwartet} > {vorher}");
        }),
    };
}
