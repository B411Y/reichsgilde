using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Tests: KI-Häuser verkaufen einen Betrieb nach zwei ganzen Verlustjahren und vor dem Bankrott – und kaufen ihn nicht gleich zurück
/// (Spiel-KI, PR #108). Jahre ohne Personal zählen nicht, der Notverkauf nimmt einen Betrieb, der reicht. Ohne Karte (wie Köln).
/// </summary>
static class KiVerkaufTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));

    /// <summary>Wie die echten Daten, aber bankrott schon nach zwei Jahresabschlüssen im Minus – so ist die Not im zweiten Jahr da.</summary>
    static readonly Lazy<Wirtschaftsdaten> SchnellBankrott = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte(
        File.ReadAllText(Datenpfad.Wirtschaft()).Replace("\"jahreBisBankrott\": 3", "\"jahreBisBankrott\": 2"))));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: daten ?? Daten.Value);

    /// <summary>Ein Haus mit zwei Gießereien ohne Personal und ohne Karren – jede macht im Jahr genau ihren Unterhalt Verlust.</summary>
    static (Konto K, Betrieb Nord, Betrieb Sued) ZweiLeereGiessereien(Stadtwirtschaft w, double vermoegen)
    {
        var k = w.KontoEroeffnen("Haus Lind", startvermoegen: vermoegen);
        var nord = w.BetriebEroeffnen("Gießerei Nord", Betriebsart.Giesserei, k, "");
        var sued = w.BetriebEroeffnen("Gießerei Süd", Betriebsart.Giesserei, k, "");
        return (k, nord, sued);
    }

    /// <summary>Wie <see cref="ZweiLeereGiessereien"/>, aber mit je einem Gesellen: Jede macht im Jahr Lohn und Unterhalt Verlust.</summary>
    static (Konto K, Betrieb Nord, Betrieb Sued) ZweiGiessereienMitGesellen(Stadtwirtschaft w, double vermoegen)
    {
        var (k, nord, sued) = ZweiLeereGiessereien(w, vermoegen);
        w.Einstellen(nord, "Geselle", 1);
        w.Einstellen(sued, "Geselle", 1);
        return (k, nord, sued);
    }

    /// <summary>So viele Spielstunden ticken.</summary>
    static void Stunden(Simulation sim, int stunden)
    {
        for (var i = 0; i < stunden * 60; i++) sim.Tick();
    }

    /// <summary>Bis zum Jahreswechsel (die Jahresabrechnung ist gelaufen), dann noch so viele Minuten.</summary>
    static void Jahr(Simulation sim, int danach = 0)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
        for (var i = 0; i < danach; i++) sim.Tick();
    }

    static bool Besitzt(Stadtwirtschaft w, Konto k, string name) => w.Betriebe.Any(b => b.Konto == k && b.Name == name);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Verkauf: Ein Betrieb mit zwei ganzen Verlustjahren in Folge wird verkauft – nach einem noch nicht; der letzte bleibt", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (k, nord, sued) = ZweiGiessereienMitGesellen(w, 50_000);
            var ki = w.KiAnmelden(k, new KiProfil(Reserve: 1e9)); // stellt niemanden ein, kauft und baut nichts
            Jahr(sim, danach: 120);
            Pruefe.Gleich((1, 1), (ki.Verlustjahre(nord), ki.Verlustjahre(sued)));
            Pruefe.Wahr(Besitzt(w, k, "Gießerei Nord") && Besitzt(w, k, "Gießerei Süd"), "nach einem Verlustjahr wird nichts verkauft");
            Jahr(sim, danach: 120);
            Pruefe.Wahr(!Besitzt(w, k, "Gießerei Nord"), "Gießerei Nord ist verkauft");
            Pruefe.Wahr(Besitzt(w, k, "Gießerei Süd"), "der letzte Betrieb bleibt");
            Pruefe.Gleich(2, ki.Verlustjahre(sued));
            Pruefe.Wahr(ki.Entscheidungen.Any(e => e.Contains("verkauft Gießerei Nord") && e.Contains("2 Jahre in Folge Verlust")), "Begründung");
            Pruefe.Wahr(w.FreieLiegenschaften.Any(l => l.Name == "Gießerei Nord"), "steht wieder zum Verkauf");
        }),

        ("KI-Verkauf: Wächst das Lager um mehr, als der Betrieb verliert, ist es kein Verlustjahr – ein Jahr mit Gewinn setzt die Folge zurück", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (k, nord, sued) = ZweiGiessereienMitGesellen(w, 50_000);
            var ki = w.KiAnmelden(k, new KiProfil(Reserve: 1e9));
            Jahr(sim);
            Pruefe.Gleich((1, 1), (ki.Verlustjahre(nord), ki.Verlustjahre(sued)));
            nord.Einlagern("Silberring", 10); // mehr wert als 220 Gulden Lohn und 80 Gulden Unterhalt
            Jahr(sim);
            Pruefe.Gleich((0, 2), (ki.Verlustjahre(nord), ki.Verlustjahre(sued)));
            for (var i = 0; i < 120; i++) sim.Tick();
            Pruefe.Wahr(Besitzt(w, k, "Gießerei Nord") && !Besitzt(w, k, "Gießerei Süd"), "Süd ist verkauft, Nord nicht");
        }),

        ("KI-Verkauf: Ein Jahr mit mindestens einem Viertel der Stunden ohne Personal zählt nicht und unterbricht die Folge nicht", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (k, nord, sued) = ZweiGiessereienMitGesellen(w, 50_000);
            var ki = w.KiAnmelden(k, new KiProfil(Reserve: 1e9)); // stellt nicht wieder ein
            Jahr(sim);
            Pruefe.Gleich((1, 1), (ki.Verlustjahre(nord), ki.Verlustjahre(sued)));

            // Süd steht sechs der 17 Stunden ohne Gesellen (mehr als ein Viertel): Das Jahr zählt nicht – die Folge bleibt bei 1.
            w.Einstellen(sued, "Geselle", 0);
            Stunden(sim, 6);
            w.Einstellen(sued, "Geselle", 1);
            Jahr(sim);
            Pruefe.Gleich((2, 1), (ki.Verlustjahre(nord), ki.Verlustjahre(sued)));
            Stunden(sim, 2);
            Pruefe.Wahr(!Besitzt(w, k, "Gießerei Nord") && Besitzt(w, k, "Gießerei Süd"), "Nord nach zwei Verlustjahren verkauft, Süd nicht");

            // Zwei Stunden ohne Gesellen (weniger als ein Viertel): Das Jahr zählt – jetzt hat auch Süd zwei Verlustjahre in Folge.
            w.Einstellen(sued, "Geselle", 0);
            Stunden(sim, 2);
            w.Einstellen(sued, "Geselle", 1);
            Jahr(sim);
            Pruefe.Gleich(2, ki.Verlustjahre(sued));
        }),

        ("KI-Verkauf: Ein Betrieb ganz ohne Personal hat keine Verlustjahre – auch wenn er danach besetzt wird", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (k, nord, sued) = ZweiLeereGiessereien(w, 50_000);
            var ki = w.KiAnmelden(k, new KiProfil(Reserve: 1e9));
            Jahr(sim);
            Jahr(sim);
            Pruefe.Gleich((0, 0), (ki.Verlustjahre(nord), ki.Verlustjahre(sued)));
            w.Einstellen(nord, "Geselle", 1); // endlich Geld für Löhne: Der Betrieb fängt bei null an
            Jahr(sim, danach: 120);
            Pruefe.Gleich((1, 0), (ki.Verlustjahre(nord), ki.Verlustjahre(sued)));
            Pruefe.Wahr(Besitzt(w, k, "Gießerei Nord") && Besitzt(w, k, "Gießerei Süd"), "nichts verkauft");
        }),

        ("KI-Verkauf: Vor dem Bankrott verkauft die KI ab der Jahresmitte den schwächsten Betrieb – und bleibt im Spiel", () =>
        {
            foreach (var mitKi in new[] { false, true })
            {
                var sim = NeueSimulation(SchnellBankrott.Value);
                var w = sim.Wirtschaft!;
                var (k, _, _) = ZweiLeereGiessereien(w, -500);
                var ki = mitKi ? w.KiAnmelden(k) : null;
                Jahr(sim, danach: 60);
                Pruefe.Gleich(1, k.Schuldenjahre); // der Kämmerer mahnt: noch ein Jahresabschluss im Minus, dann bankrott
                for (var i = 0; i < Spielzeit.MinutenProTag / 2 - 120; i++) sim.Tick();
                Pruefe.Gleich(2, w.Betriebe.Count(b => b.Konto == k)); // vor der Jahresmitte wird nicht verkauft
                Jahr(sim);
                if (!mitKi)
                {
                    Pruefe.Wahr(k.IstBankrott, "Ohne Verkauf ist das Haus bankrott");
                    continue;
                }
                Pruefe.Wahr(!k.IstBankrott, "Die KI hat den Bankrott abgewendet");
                Pruefe.Gleich(0, k.Schuldenjahre);
                Pruefe.Wahr(!Besitzt(w, k, "Gießerei Nord") && Besitzt(w, k, "Gießerei Süd"), "Nord verkauft (gleich schwach, nach dem Namen)");
                Pruefe.Wahr(ki!.Entscheidungen.Any(e => e.Contains("verkauft Gießerei Nord") && e.Contains("Bankrott abzuwenden")), "Begründung");
            }
        }),

        ("KI-Verkauf: Vor dem Bankrott nimmt die KI einen Betrieb, dessen Verkauf reicht – nicht den schwächsten, wenn der zu wenig bringt", () =>
        {
            foreach (var regel in new[] { false, true })
            {
                var sim = NeueSimulation(SchnellBankrott.Value);
                var w = sim.Wirtschaft!;
                var (k, nord, sued) = ZweiLeereGiessereien(w, -500);
                sued.Einlagern("Silberring", 60); // Süd bringt mit der Lagerware weit mehr
                nord.SchadenBuchen(50);           // Nord ist der schwächere Betrieb
                var ki = w.KiAnmelden(k);
                Jahr(sim, danach: 60);
                Pruefe.Gleich(1, k.Schuldenjahre);
                for (var i = 0; i < Spielzeit.MinutenProTag / 2 - 120; i++) sim.Tick();
                // Das Minus zum Jahresende: 2.000 mehr, als Nord bringt – oder (zum Vergleich) weniger als Nord bringt.
                var nordErloes = w.VerkaufVorschau(nord).Summe;
                Pruefe.Wahr(w.VerkaufVorschau(sued).Summe > nordErloes + 3_000, "Süd bringt deutlich mehr");
                var minus = regel ? nordErloes + 2_000 : nordErloes - 1_000;
                k.Schaden(ki.ReinvermoegenAmJahresende + minus);
                Jahr(sim);
                Pruefe.Wahr(!k.IstBankrott, "Die KI hat den Bankrott abgewendet");
                if (regel)
                {
                    Pruefe.Wahr(Besitzt(w, k, "Gießerei Nord") && !Besitzt(w, k, "Gießerei Süd"), "Süd verkauft – Nord allein hätte nicht gereicht");
                    Pruefe.Wahr(ki.Entscheidungen.Any(e => e.Contains("verkauft Gießerei Süd") && e.Contains("Bankrott abzuwenden")), "Begründung");
                }
                else
                    Pruefe.Wahr(!Besitzt(w, k, "Gießerei Nord") && Besitzt(w, k, "Gießerei Süd"), "Reichen beide, geht der schwächere");
            }
        }),

        ("KI-Verkauf: Keine Rückkauf-Schleife – Handwerk und Ort eines verkauften Betriebs meidet die KI fünf Jahre, danach darf sie wieder", () =>
        {
            var sim = NeueSimulation(SchnellBankrott.Value);
            var w = sim.Wirtschaft!;
            var (k, _, sued) = ZweiLeereGiessereien(w, -500);
            var ki = w.KiAnmelden(k);
            Jahr(sim);
            Jahr(sim);
            Pruefe.Wahr(!Besitzt(w, k, "Gießerei Nord") && !k.IstBankrott, "Notverkauf im zweiten Jahr");
            k.Verkauf(50_000); // jetzt ist Geld da: Die freie Gießerei Nord (und die der Stadt) rechnen sich
            // Gießerei Süd stand zwei Jahre ohne Personal (kein Geld für Löhne): keine Verlustjahre – jetzt besetzt, wird sie nicht verkauft.
            Pruefe.Gleich(0, ki.Verlustjahre(sued));
            // Solange die Sperre seit dem letzten Verkauf läuft, kommt keine Gießerei dazu (andere Handwerke darf sie kaufen).
            int Werkstaetten() => w.Betriebe.Count(b => b.Konto == k && b.Ort == Arbeitsort.Werkstatt && b.Artdaten.Beruf == "Schmied");
            var anzahl = Werkstaetten();
            var sperreBis = sim.Zeit.Jahr - 1 + KiRivale.SperreNachVerkauf;
            while (sim.Zeit.Jahr < sperreBis)
            {
                sim.Tick();
                var jetzt = Werkstaetten();
                if (sim.Zeit.Jahr >= sperreBis) break; // der Jahreswechsel: Ab jetzt darf sie wieder
                Pruefe.Wahr(jetzt <= anzahl, $"{sim.Zeit.DatumText}: Gießerei zurückgekauft");
                Pruefe.Wahr(!Besitzt(w, k, "Gießerei Nord"), "Nord zurückgekauft");
                Pruefe.Wahr(ki.Gesperrt(Betriebsart.Giesserei), "Gießereien gesperrt");
                if (jetzt < anzahl) sperreBis = sim.Zeit.Jahr + KiRivale.SperreNachVerkauf;
                anzahl = jetzt;
            }
            Pruefe.Wahr(Besitzt(w, k, "Gießerei Süd"), "Süd ist geblieben");
            Pruefe.Wahr(!ki.Gesperrt(Betriebsart.Giesserei), "Nach der Sperre darf die KI wieder eine Gießerei kaufen");
        }),

        ("KI-Verkauf: Verlustjahre, Lagerwerte, Stunden ohne Personal und Sperren kommen in den Spielstand; alte Spielstände ohne sie laden", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (k, _, sued) = ZweiGiessereienMitGesellen(w, 50_000);
            var ki = w.KiAnmelden(k, new KiProfil(Reserve: 1e9));
            Jahr(sim);
            Jahr(sim, danach: 120); // Nord ist verkauft
            w.Einstellen(sued, "Geselle", 0);
            Stunden(sim, 2);
            w.Einstellen(sued, "Geselle", 1);
            sued.Einlagern("Silberring", 3);
            var json = Speicherung.Speichern(sim, zeitpunkt: DateTime.UnixEpoch);
            var stand = JsonNode.Parse(json)!["Wirtschaft"]!["Rivalen"]![0]!;
            Pruefe.Gleich(2, (int)stand["Verlustjahre"]!["Gießerei Süd"]!);
            Pruefe.Wahr(stand["Lagerwert"]!["Gießerei Süd"] is not null, "Lagerwert gespeichert");
            Pruefe.Wahr((int)stand["StundenOhnePersonal"]!["Gießerei Süd"]! >= 2, "Stunden ohne Personal gespeichert");
            Pruefe.Gleich(sim.Zeit.Jahr + KiRivale.SperreNachVerkauf, (int)stand["Gesperrt"]!["Schmied/Werkstatt"]!);

            var (sim2, _) = Speicherung.Laden(json);
            Pruefe.Gleich(json, Speicherung.Speichern(sim2, zeitpunkt: DateTime.UnixEpoch));
            var ki2 = sim2.Wirtschaft!.Rivalen.Single();
            Pruefe.Gleich(ki.Verlustjahre(sued), ki2.Verlustjahre(sim2.Wirtschaft.Betriebe.Single(b => b.Name == "Gießerei Süd")));

            var alt = JsonNode.Parse(json)!;
            var rivale = alt["Wirtschaft"]!["Rivalen"]![0]!.AsObject();
            foreach (var feld in new[] { "Verlustjahre", "Lagerwert", "StundenOhnePersonal", "Gesperrt", "KeinWerkstattplatzJahr" }) rivale.Remove(feld);
            var (sim3, _) = Speicherung.Laden(alt.ToJsonString());
            Pruefe.Gleich(0, sim3.Wirtschaft!.Rivalen.Single().Verlustjahre(sim3.Wirtschaft.Betriebe.Single(b => b.Name == "Gießerei Süd")));
            Jahr(sim3, danach: 60); // läuft weiter
        }),
    };
}
