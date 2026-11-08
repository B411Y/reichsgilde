using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests: Pfadwahl der KI in der Lebensweise (Nachsteuerung 8.11.2026) – der Pfad, der zur Arbeit des Hauses passt, und der Pfad der Schatten
/// nur für ein Haus mit einem Betrieb des Diebes.
/// </summary>
static class KiLebensweiseTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Simulation NeueSimulation() =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: Daten.Value);

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    /// <summary>Ein KI-Haus (ein Betrieb, keine Expansion) mit einem Halbling als Oberhaupt: Nacht und Nebel 4, Rhetorik 2,5, Verhandeln 2, sonst 1; ohne Züge.</summary>
    static (Konto Konto, Person Oberhaupt) KiHaus(Stadtwirtschaft w, string name, Betriebsart art)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: 20_000);
        var f = w.Familien.Gruenden(k, Volk.Halbling, Geschlecht.Mann, alter: 30);
        var o = f.Oberhaupt;
        o.ZuegeLeeren();
        foreach (var t in TalentTexte.Alle) o.TalentSetzen(t, 1);
        o.TalentSetzen(Talent.NachtUndNebel, 4);
        o.TalentSetzen(Talent.Verhandeln, 2);
        o.TalentSetzen(Talent.Rhetorik, 2.5);
        w.BetriebEroeffnen($"{w.Daten.Art(art).Name} {name}", art, k, startkarren: "");
        w.KiAnmelden(k, Partieaufbau.ProfilFuer(KiCharakter.Ausgewogen) with { MaxBetriebe = 0 });
        return (k, o);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Lebensweise: Ein Haus mit Werkstatt folgt dem Pfad des Meisters, eines mit Diebesbetrieb den Schatten, eines ohne beides nicht den Schatten – " +
         "obwohl Nacht und Nebel überall das stärkste Talent ist", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (_, schmied) = KiHaus(w, "Haus Amboss", Betriebsart.Schmiede);
            var (_, dieb) = KiHaus(w, "Haus Elster", Betriebsart.Schmugglerloch);
            var (_, geldleiher) = KiHaus(w, "Haus Gulden", Betriebsart.Wechselstube);
            JahrBeenden(sim);
            // Werkstatt: Handwerk 1 + 2 = 3 schlägt Rhetorik 2,5 – Schatten scheidet aus.
            Pruefe.Gleich<Talent?>(Talent.Handwerk, schmied.Lebensweg.Pfad);
            // Gegenprobe: Ein Haus in der Unterwelt nimmt den Pfad der Schatten (4 + 2).
            Pruefe.Gleich<Talent?>(Talent.NachtUndNebel, dieb.Lebensweg.Pfad);
            // Ohne Werkstatt und Diebesbetrieb: das stärkste übrige Talent.
            Pruefe.Gleich<Talent?>(Talent.Rhetorik, geldleiher.Lebensweg.Pfad);
            Pruefe.Wahr(schmied.Lebensweg.Erfahrung(Talent.Handwerk) > 0 && schmied.Lebensweg.Erfahrung(Talent.NachtUndNebel) == 0,
                "Erfahrung des ersten Jahres im Pfad des Meisters");
        }),

        ("KI-Lebensweise: Ein Pfad ohne eigenen Knoten wird neu gewählt (z. B. beim ersten Buchen festgehalten), einer mit Knoten bleibt – " +
         "außer dem Pfad der Schatten ohne Diebesbetrieb", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var d = w.Familien.LebensweiseDaten!;
            var (_, ohneKnoten) = KiHaus(w, "Haus Amboss", Betriebsart.Schmiede);
            var (kMit, mitKnoten) = KiHaus(w, "Haus Hammer", Betriebsart.Schmiede);
            var (kSchatten, schatten) = KiHaus(w, "Haus Esse", Betriebsart.Schmiede);
            ohneKnoten.Lebensweg.Pfad = Talent.NachtUndNebel;
            mitKnoten.Lebensweg.Pfad = Talent.Verhandeln;
            mitKnoten.Lebensweg.ErfahrungSetzen(Talent.Verhandeln, d.ErfahrungFuer(1));
            Pruefe.Wahr(w.Ausfuehren(kMit, new KnotenWaehlen(mitKnoten.Nummer, "Marktschreier")).Erfolg, "Marktschreier");
            schatten.Lebensweg.Pfad = Talent.NachtUndNebel;
            schatten.Lebensweg.ErfahrungSetzen(Talent.NachtUndNebel, d.ErfahrungFuer(1));
            Pruefe.Wahr(w.Ausfuehren(kSchatten, new KnotenWaehlen(schatten.Nummer, "LeiseSohlen")).Erfolg, "Leise Sohlen");
            JahrBeenden(sim);
            Pruefe.Gleich<Talent?>(Talent.Handwerk, ohneKnoten.Lebensweg.Pfad);
            Pruefe.Gleich<Talent?>(Talent.Verhandeln, mitKnoten.Lebensweg.Pfad); // Gegenprobe: mit Knoten bleibt der Pfad
            Pruefe.Gleich<Talent?>(Talent.Handwerk, schatten.Lebensweg.Pfad);
            Pruefe.Wahr(schatten.Lebensweg.HatKnoten("LeiseSohlen"), "der Knoten bleibt beim Wechsel");
        }),
    };
}
