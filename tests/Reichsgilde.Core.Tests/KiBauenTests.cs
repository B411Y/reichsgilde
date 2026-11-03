using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Tests: KI-Häuser kaufen Leerstand nur bei Bedarf und erst nach einem Jahr, bauen Stadtpalais und Stadthof neu
/// und bauen Werkstätten neu, wenn keine freie sich besser rechnet (Spiel-KI, PR #108). Alles auf der Greybox-Karte.
/// </summary>
static class KiBauenTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));

    static string Kartenpfad => Datenpfad.Karte("greybox");

    static (Simulation Sim, Stadtwirtschaft W) Greybox(Wirtschaftsdaten? daten = null)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: daten ?? Daten.Value);
        sim.Wirtschaft!.KarteSetzen(Stadtkarte.AusDatei(Kartenpfad));
        return (sim, sim.Wirtschaft);
    }

    static void Ticks(Simulation sim, int minuten)
    {
        for (var i = 0; i < minuten; i++) sim.Tick();
    }

    /// <summary>Ein Haus mit diesem Stand und Wohnhaus (Hauswert passend).</summary>
    static Konto Haus(Stadtwirtschaft w, string name, int stand, int wohnhaus, double vermoegen)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: vermoegen);
        k.Stand = stand;
        k.WohnhausStufe = wohnhaus;
        w.Wohnen.HauswertAktualisieren(k);
        return k;
    }

    /// <summary>Ein bankrottes Haus, dessen Wohnhaus (diese Stufe) ab der nächsten Minute leer steht.</summary>
    static Konto Pleite(Stadtwirtschaft w, string name, int wohnhaus)
    {
        var tot = w.KontoEroeffnen(name);
        tot.WohnhausStufe = wohnhaus;
        tot.IstBankrott = true;
        return tot;
    }

    static bool BautWohnhaus(Stadtwirtschaft w, Konto k) => w.Bauen.Umzug(k) is { Arbeit: Bauarbeit.Neubau };

    static int Baustellen(Stadtwirtschaft w, Konto k) =>
        w.Bauen.Grundstuecke.Count(g => g.Art == Grundstuecksart.Werkstatt && g.Arbeit == Bauarbeit.Neubau && g.Besitzer == k);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Bauen: Ein leeres Stadtpalais kauft die KI erst, wenn es ein ganzes Jahr leer stand – bis dahin wartet sie, statt neu zu bauen", () =>
        {
            var (sim, w) = Greybox();
            var tot = Pleite(w, "Haus Pleite", wohnhaus: 3);
            var k = Haus(w, "Haus Lind", stand: 2, wohnhaus: 2, vermoegen: 80_000); // Patrizier im Steinhaus, reich genug für den Edlen
            w.KiAnmelden(k, new KiProfil(MaxBetriebe: 0));
            sim.Tick();
            var seit = w.Bauen.LeerSeit(tot)!.Value;
            Pruefe.Wahr(w.Wohnen.AusbauVerboten(k)!.Contains("Neubau"), "Ein Stadtpalais gibt es nicht als Ausbau");
            while (w.Uhr.VergangeneMinuten < seit + Spielzeit.MinutenProTag - 1)
            {
                sim.Tick();
                Pruefe.Wahr(w.Bauen.Umzug(k) is null, $"{w.Uhr.DatumText} {w.Uhr.UhrzeitText}: weder Kauf noch Neubau vor Ablauf des Jahres");
            }
            Ticks(sim, 70); // die nächste volle Stunde
            Pruefe.Gleich(Bauarbeit.Sanierung, w.Bauen.Umzug(k)?.Arbeit);
            Pruefe.Wahr(w.Bauen.Leerstand(tot) is null, "Das Haus von Haus Pleite ist verkauft");
            Ticks(sim, w.Wohnen.Daten!.BauzeitMinuten + 120); // Sanierung, Umzug, dann der Aufstieg
            Pruefe.Gleich(3, k.WohnhausStufe);
            Pruefe.Gleich("Edler", w.StandVon(k).Name);
        }),

        ("KI-Bauen: Ohne Bedarf kauft die KI keinen Leerstand – auch kein Schnäppchen, und sie reißt nichts ab", () =>
        {
            var (sim, w) = Greybox();
            var tot = Pleite(w, "Haus Pleite", wohnhaus: 3);
            var k = Haus(w, "Haus Lind", stand: 2, wohnhaus: 2, vermoegen: 20_000); // weit unter dem Edlen (50.000)
            w.KiAnmelden(k, new KiProfil(MaxBetriebe: 0));
            Ticks(sim, 2 * Spielzeit.MinutenProTag);
            Pruefe.Wahr(w.Bauen.Leerstand(tot) is not null, "Der Leerstand steht noch");
            Pruefe.Wahr(w.Bauen.Umzug(k) is null, "kein Umzug");
            Pruefe.Gleich(2, k.WohnhausStufe);
        }),

        ("KI-Bauen: Braucht die KI ein Steinhaus, kauft sie einen Leerstand (ein Jahr leer), wenn er billiger ist als der Ausbau", () =>
        {
            var (sim, w) = Greybox();
            var tot = Pleite(w, "Haus Pleite", wohnhaus: 2);
            var k = Haus(w, "Haus Lind", stand: 1, wohnhaus: 1, vermoegen: 30_000); // Bürger im Bürgerhaus, reich genug für den Patrizier
            Ticks(sim, Spielzeit.MinutenProTag + 1); // der Leerstand steht ein Jahr leer (Haus Lind ist noch keine KI)
            var v = w.Bauen.LeerstandVorschau(k, tot);
            Pruefe.Wahr(v.KaufSumme < w.Wohnen.NaechsteStufe(k)!.Ausbau, $"Leerstand {v.KaufSumme} billiger als der Ausbau");
            var vorher = k.Vermoegen;
            var ki = w.KiAnmelden(k, new KiProfil(MaxBetriebe: 0));
            Pruefe.Gleich(Bauarbeit.Sanierung, w.Bauen.Umzug(k)?.Arbeit);
            Pruefe.Ungefaehr(vorher - v.KaufSumme, k.Vermoegen, 1e-6);
            Pruefe.Wahr(ki.Entscheidungen.Any(e => e.Contains("kauft das leerstehende Steinhaus von Haus Pleite")), "Begründung");
            Ticks(sim, w.Wohnen.Daten!.BauzeitMinuten + 60);
            Pruefe.Gleich(2, k.WohnhausStufe);
            Pruefe.Gleich("Patrizier", w.StandVon(k).Name);
        }),

        ("KI-Bauen: In der Greybox-Partie baut die KI ein Stadtpalais neu und steigt danach zum Edlen auf (vorher hing sie am Steinhaus)", () =>
        {
            var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, Daten.Value,
                new NeuePartie(), karte: Stadtkarte.AusDatei(Kartenpfad));
            var w = sim.Wirtschaft!;
            var ki = w.Rivalen[0];
            var k = ki.Konto;
            k.Stand = 2;
            k.WohnhausStufe = 2;
            w.Wohnen.HauswertAktualisieren(k);
            k.Verkauf(60_000);
            Ticks(sim, Spielzeit.MinutenProTag);
            Pruefe.Wahr(BautWohnhaus(w, k), "Die KI baut neu");
            Pruefe.Gleich(3, w.Bauen.UmzugZiel(k));
            Pruefe.Wahr(ki.Entscheidungen.Any(e => e.Contains("baut ein neues Wohnhaus, ein Stadtpalais")), "Begründung");
            Ticks(sim, 2 * Spielzeit.MinutenProTag);
            Pruefe.Gleich(3, k.WohnhausStufe);
            Pruefe.Gleich("Edler", w.StandVon(k).Name);
        }),

        ("KI-Bauen: Gibt es keine freie Werkstatt, baut die KI eine neu – nahe am Markt; die Baustelle zählt zu den Betrieben", () =>
        {
            // Zwei Jahre Bauzeit: So steht die Baustelle zur nächsten Baustunde noch.
            var (sim, w) = Greybox(Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte(File.ReadAllText(Datenpfad.Wirtschaft())
                .Replace("\"breite\": 16, \"tiefe\": 14, \"bauzeitJahre\": 1", "\"breite\": 16, \"tiefe\": 14, \"bauzeitJahre\": 2"))));
            var stadt = w.KontoEroeffnen("Haus Stadt");
            foreach (var l in w.FreieLiegenschaften.ToList()) w.BetriebEroeffnen(l.Name, l.Art, stadt, "");
            var k = Haus(w, "Haus Lind", stand: 1, wohnhaus: 1, vermoegen: 15_000); // Bürger – als Patrizier dürfte sie einen Betrieb mehr führen
            k.BerufLernen("Schmied");
            var ki = w.KiAnmelden(k, new KiProfil(MaxBetriebe: 1));
            Ticks(sim, Spielzeit.MinutenProTag);
            Pruefe.Gleich(1, ki.BetriebeHoechstens);
            Pruefe.Gleich(1, Baustellen(w, k));
            var baustelle = w.Bauen.Grundstuecke.Single(g => g.Besitzer == k && g.Art == Grundstuecksart.Werkstatt);
            Pruefe.Gleich(Betriebsart.Giesserei, baustelle.Betriebsart);
            Pruefe.Wahr(ki.Entscheidungen.Any(e => e.Contains($"baut {baustelle.Betrieb} neu")), "Begründung");
            // Nah am Markt (Ursprung der Karte): unter den nächsten Stellen nach Luftlinie – seit phase5/gehtempo-ki die mit dem kürzesten Karrenweg
            // (KiStandortTests), vorher die nächste.
            var abstand = baustelle.Grundriss.X * baustelle.Grundriss.X + baustelle.Grundriss.Z * baustelle.Grundriss.Z;
            Pruefe.Wahr(w.Bauen.Bauorte(k, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0)).Count(o => o.X * o.X + o.Z * o.Z < abstand - 1e-6) < KiRivale.StandortKandidaten,
                "unter den nächsten Stellen zum Markt");
            Ticks(sim, Spielzeit.MinutenProTag);
            Pruefe.Gleich(1, Baustellen(w, k)); // zur zweiten Baustunde: voll – keine zweite Baustelle
            Ticks(sim, Spielzeit.MinutenProTag);
            Pruefe.Gleich(0, Baustellen(w, k));
            Pruefe.Wahr(w.Betriebe.Any(b => b.Konto == k && b.Name == baustelle.Betrieb), "Die Werkstatt ist fertig und gehört der KI");
        }),

        ("KI-Bauen: Steht eine gleichwertige Werkstatt frei, kauft die KI sie, statt zu bauen – das Baujahr ohne Einnahmen zählt mit", () =>
        {
            var (sim, w) = Greybox();
            Werkstatthilfe.FreieWerkstatt(w, "Gießerei an der Südgasse", Betriebsart.Giesserei); // gleicher Preis wie ein Neubau
            var k = Haus(w, "Haus Lind", stand: 1, wohnhaus: 1, vermoegen: 30_000);
            k.BerufLernen("Schmied");
            w.KiAnmelden(k, new KiProfil(MaxBetriebe: 1));
            Ticks(sim, Spielzeit.MinutenProTag);
            Pruefe.Gleich(0, Baustellen(w, k));
            Pruefe.Gleich(1, w.Betriebe.Count(b => b.Konto == k && b.Ort == Arbeitsort.Werkstatt));
            Pruefe.Wahr(w.Betriebe.Any(b => b.Konto == k && b.Name == "Gießerei an der Südgasse"), "gekauft, nicht gebaut");
        }),

        ("KI-Bauen: Ohne Karte (Köln) baut die KI nicht und kauft keinen Leerstand – der Ausbau zum Stadtpalais geht dort wie bisher", () =>
        {
            var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);
            var w = sim.Wirtschaft!;
            var k = Haus(w, "Haus Lind", stand: 2, wohnhaus: 2, vermoegen: 80_000);
            w.KiAnmelden(k, new KiProfil(MaxBetriebe: 0)); // die KI überlegt sofort
            Pruefe.Wahr(k.WohnhausFertigAb is not null, "Ausbau zum Stadtpalais");
            Pruefe.Wahr(!w.Bauen.Grundstuecke.Any(g => g.Arbeit == Bauarbeit.Neubau), "kein Neubau");
        }),
    };
}
