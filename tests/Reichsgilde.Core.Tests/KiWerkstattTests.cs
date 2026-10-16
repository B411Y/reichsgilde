using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests: KI-Häuser holen die Werkstatt zu einem Handwerk nach, von dem sie nur den Umlandbetrieb haben.</summary>
static class KiWerkstattTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    /// <summary>Ein reiches KI-Haus (Bürger) mit Gießerei, Grube und dem Bauernhof des Wirts – damit ist die Höchstzahl (3) erreicht.</summary>
    static (Stadtwirtschaft W, Konto K, Reichsgilde.Core.Ki.KiRivale Ki) WirtMitBauernhof()
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Krug", startvermoegen: 60_000);
        k.BerufLernen("Schmied");
        w.BetriebEroeffnen("Gießerei Krug", Betriebsart.Giesserei, k);
        w.BetriebEroeffnen("Grube Krug", Betriebsart.Mine, k);
        Pruefe.Wahr(w.Ausfuehren(Wohnhaushilfe.MitWohnhaus(w, k), new StandErwerben()).Erfolg, "Aufstieg zum Bürger");
        k.BerufLernen("Wirt");
        Pruefe.Wahr(w.Ausfuehren(k, new LiegenschaftErwerben("Bauernhof an der Mühle")).Erfolg, "Bauernhof pachten");
        Wohnhaushilfe.MitWohnhaus(w, k); // das Steinhaus steht schon: Die KI steigt gleich zum Patrizier auf
        var ki = w.KiAnmelden(k);
        Pruefe.Gleich(ki.BetriebeHoechstens, w.Betriebe.Count(b => b.Konto == k));
        return (w, k, ki);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Werkstatt: Wer den Wirt nur für den Bauernhof gelernt hat, kauft die Schänke dazu – auch über die Höchstzahl", () =>
        {
            var (w, k, ki) = WirtMitBauernhof();
            ki.Stunde();
            var schaenke = w.Betriebe.SingleOrDefault(b => b.Konto == k && b.IstSchaenke);
            Pruefe.Wahr(schaenke is not null, "Die KI hat keine Schänke gekauft");
            Pruefe.Gleich("Schänke am Fischmarkt", schaenke!.Name);
            Pruefe.Gleich(ki.BetriebeHoechstens + 1, w.Betriebe.Count(b => b.Konto == k));
        }),

        ("KI-Werkstatt: Danach kauft sie nichts mehr über die Höchstzahl – die Ausnahme gilt nur für die fehlende Werkstatt", () =>
        {
            var (w, k, ki) = WirtMitBauernhof();
            ki.Stunde();
            var anzahl = w.Betriebe.Count(b => b.Konto == k);
            for (var i = 0; i < 5; i++) ki.Stunde();
            Pruefe.Gleich(anzahl, w.Betriebe.Count(b => b.Konto == k));
        }),

        ("KI-Werkstatt: Die KI schenkt in ihrer Schänke aus – Gäste werden bewirtet", () =>
        {
            var (w, k, ki) = WirtMitBauernhof();
            ki.Stunde();
            var schaenke = w.Betriebe.Single(b => b.Konto == k && b.IstSchaenke);
            Pruefe.Wahr(schaenke.Belegt > 0, "Die KI stellt Personal ein");
            Pruefe.Wahr(w.Daten.Gastwirtschaft!.Ausschank.Contains(schaenke.Produkt), $"Die Schänke stellt eine Ausschankware her (war {schaenke.Produkt})");
        }),
    };
}
