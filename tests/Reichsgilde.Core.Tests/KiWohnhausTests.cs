using Reichsgilde.Core;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests: KI-Häuser bauen das Wohnhaus zur rechten Zeit und sparen nach Charakter auf den nächsten Stand.</summary>
static class KiWohnhausTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    /// <summary>
    /// Ein Bürgerhaus (Stand Bürger, Bürgerhaus steht) mit Schmied gelernt, aber ohne Betrieb.
    /// Für den Patrizier (Schwelle 20.000) fehlt das Steinhaus (Ausbau 4.000).
    /// </summary>
    static (Stadtwirtschaft W, Konto K, KiRivale Ki) Buerger(double vermoegen, KiProfil? profil = null)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);
        var w = sim.Wirtschaft!;
        var buerger = w.Daten.Staende[1];
        var k = w.KontoEroeffnen("Haus Lind", startvermoegen: vermoegen + buerger.Gebuehr);
        Pruefe.Wahr(w.Ausfuehren(Wohnhaushilfe.MitWohnhaus(w, k), new StandErwerben()).Erfolg, "Aufstieg zum Bürger");
        k.BerufLernen("Schmied");
        Pruefe.Wahr(w.Wohnen.StandVerboten(k, k.Stand + 1) is not null, "Für den Patrizier fehlt noch das Haus");
        return (w, k, w.KiAnmelden(k, profil));
    }

    static double Steinhaus(Stadtwirtschaft w, Konto k) => w.Wohnen.NaechsteStufe(k)!.Ausbau;

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Wohnhaus: Über der Schwelle baut die KI das Haus gleich – der Hauswert zählt zum Reinvermögen (Kern PR #85)", () =>
        {
            var (w, k, ki) = Buerger(21_000);
            var neu = w.NaechsterStand(k)!;
            Pruefe.Wahr(k.Reinvermoegen >= neu.Schwelle, "Schwelle erreicht");
            ki.Stunde();
            Pruefe.Wahr(k.WohnhausFertigAb is not null, "Die KI baut das Steinhaus");
            Pruefe.Ungefaehr(21_000 - Steinhaus(w, k), k.Vermoegen, 1e-6); // nur das Haus bezahlt, noch keine Gebühr
            Pruefe.Wahr(k.Reinvermoegen >= neu.Schwelle, "Mit dem Bau bleibt sie über der Schwelle");
        }),

        ("KI-Wohnhaus: Reicht es für Schwelle, Haus, Gebühr und Reserve, baut sie das Haus", () =>
        {
            var (w, k, ki) = Buerger(24_500); // Schwelle 20.000 + Steinhaus 4.000 + 500 – Gebühr und Reserve sind darin enthalten
            var neu = w.NaechsterStand(k)!;
            Pruefe.Gleich(24_500.0, neu.Schwelle + Steinhaus(w, k) + 500);
            ki.Stunde();
            Pruefe.Wahr(k.WohnhausFertigAb is not null, "Die KI baut das Steinhaus");
            Pruefe.Wahr(k.Reinvermoegen >= neu.Schwelle, "Nach dem Bau bleibt sie über der Schwelle");
        }),

        ("KI-Wohnhaus: Die Vorsichtige spart kurz vor dem Ziel – keine neue Werkstatt", () =>
        {
            var (w, k, ki) = Buerger(21_000, new KiProfil(Gaunerei: 0));
            Pruefe.Wahr(ki.Spart, "Sie spart");
            for (var i = 0; i < 3; i++) ki.Stunde();
            Pruefe.Gleich(0, w.Betriebe.Count(b => b.Konto == k));
        }),

        ("KI-Wohnhaus: Weit vom Ziel spart die Vorsichtige nicht – sie kauft eine Werkstatt", () =>
        {
            var (w, k, ki) = Buerger(12_000, new KiProfil(Gaunerei: 0));
            Pruefe.Wahr(!ki.Spart, "Sie spart noch nicht");
            for (var i = 0; i < 3; i++) ki.Stunde();
            Pruefe.Wahr(w.Betriebe.Any(b => b.Konto == k), "Ohne Sparen expandiert sie");
        }),

        ("KI-Wohnhaus: Das Sparziel ist die Schwelle selbst – ob das Haus schon steht, ändert nichts", () =>
        {
            var (w, k, ki) = Buerger(17_000);
            Pruefe.Wahr(ki.Spart, "17.000 ≥ 80 % von 20.000");
            Wohnhaushilfe.MitWohnhaus(w, k);
            Pruefe.Wahr(ki.Spart, "mit Haus ebenso");
            var (_, _, arm) = Buerger(15_000);
            Pruefe.Wahr(!arm.Spart, "15.000 < 80 % von 20.000");
        }),
    };
}
