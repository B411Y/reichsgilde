using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Tests für den Schiffsverkehr in der Schiffsrechnung (<c>schiffshandel.verkehrBeachten</c>, Datenversion 67): Ein Schiff rechnet mit dem, was die
/// Schiffe vor ihm zur selben Stadt bringen, dort kaufen und heimbringen – ohne den Schalter (alte Spielstände mit ihren Daten) wie bisher.
/// Gespielt wird in der Probestadt (Hafen am Sandfluss), ohne Piraten.
/// </summary>
static class SchiffsverkehrTests
{
    static readonly Lazy<string> Json = new(() => Datenpfad.OhneJahreszeiten());

    /// <summary>Die Daten ohne Piraten, mit oder ohne den Schalter.</summary>
    static Wirtschaftsdaten Daten(bool verkehr)
    {
        var knoten = JsonNode.Parse(Json.Value)!.AsObject();
        var s = knoten["schiffshandel"]!.AsObject();
        Pruefe.Wahr(s["verkehrBeachten"]?.GetValue<bool>() == true, "verkehrBeachten steht nicht in wirtschaft.json");
        if (!verkehr) s.Remove("verkehrBeachten");
        s["piraten"]!["chance"] = 0;
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    }

    static (Simulation Sim, Stadtwirtschaft W, Konto Erster, Konto Zweiter) Stadt(bool verkehr)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
            new Spielzeit(9 * 60 - Spielzeit.TagesbeginnMinute), Daten(verkehr));
        var w = sim.Wirtschaft!;
        w.KarteSetzen(Stadtkarte.AusDatei(Datenpfad.Karte("probestadt")));
        return (sim, w, w.KontoEroeffnen("Haus Vorne", startvermoegen: 30_000), w.KontoEroeffnen("Haus Hinten", startvermoegen: 30_000));
    }

    static Dictionary<string, int> D(params (string Ware, int Stueck)[] x) => x.ToDictionary(y => y.Ware, y => y.Stueck);

    static readonly Dictionary<string, int> Hin = D(("Silberring", 10)), Rueck = D(("Gold", 10));

    /// <summary>Das erste Haus schickt sein Schiff mit <see cref="Hin"/> und <see cref="Rueck"/> nach Weidenmünd.</summary>
    static Schiff Vorausschicken(Stadtwirtschaft w, Konto k)
    {
        Pruefe.Wahr(w.Ausfuehren(k, new SchiffKaufen()).Erfolg, "Schiff kaufen");
        var schiff = w.Schiffahrt.SchiffeVon(k).Single();
        var e = w.Ausfuehren(k, new SchiffsauftragErteilen(schiff.Name, "Weidenmünd", Hin, Rueck));
        Pruefe.Wahr(e.Erfolg, e.Meldung);
        return schiff;
    }

    static Fahrtrechnung Rechnung(Stadtwirtschaft w, Konto k) =>
        Kalkulation.Schiffsfahrt(w, k, w.Schiffahrt.Daten!.Schiffe["Flussschiff"], w.Schiffahrt.Stadt("Weidenmünd")!, Hin, Rueck);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Schiffsverkehr: Ein Schiff auf dem Weg nach Weidenmünd zählt mit – dort verkauft und gekauft, daheim verkauft; ohne Schalter wie bisher", () =>
        {
            foreach (var verkehr in new[] { true, false })
            {
                var (_, w, vorne, hinten) = Stadt(verkehr);
                var stadt = w.Schiffahrt.Stadt("Weidenmünd")!;
                var vorher = Rechnung(w, hinten);
                var schiff = Vorausschicken(w, vorne);
                Pruefe.Gleich(SchiffZustand.Beladen, schiff.Zustand);
                var nachher = Rechnung(w, hinten);
                var vf = w.Familien.VerkaufsFaktor(hinten);
                var ef = w.Familien.EinkaufsFaktor(hinten);
                var steuer = w.SteuersatzMarkt(hinten);
                if (!verkehr)
                {
                    Pruefe.Gleich(vorher.ErloesFern, nachher.ErloesFern, "ohne Schalter: Erlös dort");
                    Pruefe.Gleich(vorher.EinkaufFern, nachher.EinkaufFern, "ohne Schalter: Einkauf dort");
                    Pruefe.Gleich(vorher.ErloesHeim, nachher.ErloesHeim, "ohne Schalter: Erlös daheim");
                    continue;
                }
                Pruefe.Gleich(stadt.Erloes("Silberring", 10, 10) * vf, nachher.ErloesFern, "die 10 Silberringe vorne sind dort schon verkauft");
                Pruefe.Gleich(stadt.Kosten("Gold", 10, 10) * ef, nachher.EinkaufFern, "die 10 Gold vorne sind dort schon gekauft");
                Pruefe.Gleich(w.Markt["Gold"].Verkaufswert(10, 10) * (1 - steuer) * vf, nachher.ErloesHeim, "das Gold vorne ist daheim schon verkauft");
                Pruefe.Wahr(nachher.Gewinn < vorher.Gewinn - 50, $"das zweite Schiff erwartet weniger ({vorher.Gewinn:0} → {nachher.Gewinn:0})");
            }
        }),

        ("Schiffsverkehr: In der fernen Stadt zählt die verkaufte Hinfracht nicht doppelt, die Rückfracht bis zum Verkauf daheim", () =>
        {
            var (sim, w, vorne, hinten) = Stadt(verkehr: true);
            var stadt = w.Schiffahrt.Stadt("Weidenmünd")!;
            var schiff = Vorausschicken(w, vorne);
            for (var i = 0; i < 3000 && schiff.Zustand != SchiffZustand.InFernerStadt; i++) sim.Tick();
            Pruefe.Gleich(SchiffZustand.InFernerStadt, schiff.Zustand);
            Pruefe.Wahr(stadt.Druck("Silberring") > 0, "dort verkauft");
            var r = Rechnung(w, hinten);
            var vf = w.Familien.VerkaufsFaktor(hinten);
            Pruefe.Gleich(stadt.Erloes("Silberring", 10) * vf, r.ErloesFern, "der Druck der Stadt zählt, nicht noch einmal die Ladung");
            Pruefe.Gleich(stadt.Kosten("Gold", 10) * w.Familien.EinkaufsFaktor(hinten), r.EinkaufFern, "das Gold ist dort gekauft und steht im Druck");
            Pruefe.Gleich(w.Markt["Gold"].Verkaufswert(10, schiff.Ladung["Gold"]) * (1 - w.SteuersatzMarkt(hinten)) * vf, r.ErloesHeim,
                "die geladene Rückfracht kommt vorher heim");
            for (var i = 0; i < 3000 && schiff.Zustand != SchiffZustand.AmKai; i++) sim.Tick();
            Pruefe.Gleich(SchiffZustand.AmKai, schiff.Zustand);
            Pruefe.Gleich(w.Markt["Gold"].Verkaufswert(10) * (1 - w.SteuersatzMarkt(hinten)) * vf, Rechnung(w, hinten).ErloesHeim, "heimgekehrt: nichts mehr unterwegs");
        }),

        ("Schiffsverkehr: Die KI schickt ein zweites Schiff nicht mehr mit derselben Fracht hinterher, wenn sie dadurch nichts mehr brächte", () =>
        {
            var (_, w, vorne, hinten) = Stadt(verkehr: true);
            Vorausschicken(w, vorne);
            var typ = w.Schiffahrt.Daten!.Schiffe["Flussschiff"];
            var mit = Kalkulation.BesteSchiffsfahrt(w, hinten, typ, 30_000, ziel: "Weidenmünd");
            var (_, w2, vorne2, hinten2) = Stadt(verkehr: false);
            Vorausschicken(w2, vorne2);
            var ohne = Kalkulation.BesteSchiffsfahrt(w2, hinten2, typ, 30_000, ziel: "Weidenmünd");
            Pruefe.Wahr(ohne is not null, "ohne Schalter gibt es einen Vorschlag");
            Pruefe.Wahr(mit is null || mit.Rechnung.Gewinn < ohne!.Rechnung.Gewinn, "mit Schalter erwartet der Vorschlag weniger");
            if (mit is not null)
            {
                Pruefe.Wahr(mit.Hinfracht.GetValueOrDefault("Silberring") <= ohne!.Hinfracht.GetValueOrDefault("Silberring"), "nicht mehr Silberringe hinterher");
                Pruefe.Gleich(mit.Rechnung.Gewinn, Rechnung2(w, hinten, typ, mit).Gewinn, "Vorschlag und Rechnung stimmen überein");
            }
        }),
    };

    static Fahrtrechnung Rechnung2(Stadtwirtschaft w, Konto k, SchiffstypDaten typ, Fahrtvorschlag v) =>
        Kalkulation.Schiffsfahrt(w, k, typ, w.Schiffahrt.Stadt(v.Ziel)!, v.Hinfracht, v.Rueckfracht);
}
