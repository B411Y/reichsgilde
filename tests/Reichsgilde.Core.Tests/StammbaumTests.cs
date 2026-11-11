using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für Stammbaum und Hauschronik des Familienbuchs (nur lesend abgeleitet, kein Spielstand).</summary>
static class StammbaumTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    /// <summary>Haus mit Gründer, Ehepartner, einem gemeinsamen Kind und einem älteren Kind des Gründers; dann stirbt der Gründer, das ältere Kind erbt.</summary>
    static (Stadtwirtschaft W, Konto Konto, Familie F, Person Gruender, Person Partner, Person Erbe, Person Kind) ZweiGenerationen()
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Zufallsstartwert = 4711 }, wirtschaftsdaten: Daten.Value);
        var w = sim.Wirtschaft!;
        var konto = w.KontoEroeffnen("Haus Probe", startvermoegen: 100_000);
        var f = w.Familien.Gruenden(konto, Volk.Mensch, Geschlecht.Mann, alter: 40);
        var gruender = f.Oberhaupt;
        var kandidat = w.Familien.Kandidaten.First(k => k.Person.Geschlecht == Geschlecht.Frau && k.Person.Volk == Volk.Mensch);
        w.Familien.Vermaehlen(konto, kandidat, 0);
        var partner = f.Ehepartner!;
        var kind = w.Familien.EntwicklerGeburt(f, Geschlecht.Frau);
        var erbe = w.Familien.KindAufnehmen(konto, 22, Geschlecht.Mann);
        w.Familien.AnKrankheitSterben(gruender, "an der Pest");
        return (w, konto, f, gruender, partner, erbe, kind);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Stammbaum: Gründer mit Ehepartner oben, Kinder darunter, jede Person genau einmal, Erbe als zweite Ebene", () =>
        {
            var (w, _, f, gruender, partner, erbe, kind) = ZweiGenerationen();
            var baum = w.Familien.Stammbaum(f);
            Pruefe.Gleich(1, baum.Count);
            Pruefe.Wahr(baum[0].Person == gruender, "Wurzel ist der Gründer");
            Pruefe.Wahr(baum[0].Ehepartner.Contains(partner), "Ehepartner am Gründer");
            Pruefe.Gleich(2, baum[0].Kinder.Count);
            Pruefe.Wahr(baum[0].Kinder.Any(k => k.Person == erbe) && baum[0].Kinder.Any(k => k.Person == kind), "beide Kinder darunter");
            Pruefe.Gleich(f.Mitglieder.Count, Familienbuch.Personenzahl(baum));
            Pruefe.Wahr(f.Oberhaupt == erbe, "Erbe führt das Haus");
        }),

        ("Stammbaum: Ehe des Erben mit Kind – dritte Generation, Eingeheiratete mit ihrem Kind am richtigen Knoten", () =>
        {
            var (w, _, f, _, _, erbe, _) = ZweiGenerationen();
            var kandidat = w.Familien.Kandidaten.First(k => k.Person.Geschlecht == Geschlecht.Frau);
            w.Familien.Vermaehlen(f.Konto, kandidat, 0);
            var enkel = w.Familien.EntwicklerGeburt(f, Geschlecht.Mann);
            var baum = w.Familien.Stammbaum(f);
            var knotenErbe = baum[0].Kinder.First(k => k.Person == erbe);
            Pruefe.Wahr(knotenErbe.Ehepartner.Contains(kandidat.Person), "Ehepartner des Erben");
            Pruefe.Wahr(knotenErbe.Kinder.Any(k => k.Person == enkel), "Enkel unter dem Erben");
            Pruefe.Gleich(f.Mitglieder.Count, Familienbuch.Personenzahl(baum));
        }),

        ("Stammbaum: kinderlose Witwe führt das Haus (Witwenrecht) und heiratet neu – alle drei in einem Block, Kinder darunter", () =>
        {
            var sim = new Simulation(Partieeinstellungen.Standard with { Zufallsstartwert = 4711 }, wirtschaftsdaten: Daten.Value);
            var w = sim.Wirtschaft!;
            var konto = w.KontoEroeffnen("Haus Witwe", startvermoegen: 100_000);
            var f = w.Familien.Gruenden(konto, Volk.Mensch, Geschlecht.Mann, alter: 40);
            var erster = f.Oberhaupt;
            w.Familien.Vermaehlen(konto, w.Familien.Kandidaten.First(k => k.Person.Geschlecht == Geschlecht.Frau && k.Person.Volk == Volk.Mensch), 0);
            var witwe = f.Ehepartner!;
            w.Familien.AnKrankheitSterben(erster, "an der Pest");
            Pruefe.Wahr(f.Oberhaupt == witwe, "Witwenrecht: die Witwe führt das Haus");
            w.Familien.Vermaehlen(konto, w.Familien.Kandidaten.First(k => k.Person.Geschlecht == Geschlecht.Mann && k.Person.Volk == Volk.Mensch), 0);
            var zweiter = f.Ehepartner!;
            var kind = w.Familien.EntwicklerGeburt(f, Geschlecht.Mann);
            var baum = w.Familien.Stammbaum(f);
            Pruefe.Gleich(1, baum.Count);
            Pruefe.Wahr(baum[0].Person == erster && baum[0].Ehepartner.Contains(witwe) && baum[0].Ehepartner.Contains(zweiter), "Witwe und zweiter Mann am Gründer");
            Pruefe.Wahr(baum[0].Kinder.Any(k => k.Person == kind), "Kind darunter");
            Pruefe.Gleich(f.Mitglieder.Count, Familienbuch.Personenzahl(baum));
        }),

        ("Hauschronik: Gründung, Geburt, Tod, Erbfolge aus den Personen, Heirat aus dem Verlauf; neueste zuerst", () =>
        {
            var (w, konto, f, gruender, _, erbe, _) = ZweiGenerationen();
            var chronik = w.Familien.Chronik(f, konto);
            Pruefe.Wahr(chronik.Any(e => e.Art == Chronikart.Gruendung), "Gründung");
            Pruefe.Wahr(chronik.Any(e => e.Art == Chronikart.Geburt), "Geburt");
            Pruefe.Wahr(chronik.Any(e => e.Art == Chronikart.Tod && e.Person == gruender.Nummer), "Tod des Gründers");
            Pruefe.Wahr(chronik.Any(e => e.Art == Chronikart.Erbe && e.Person == erbe.Nummer), "Erbfolge");
            Pruefe.Wahr(chronik.Any(e => e.Art == Chronikart.Heirat), "Heirat aus dem Meldungsverlauf");
            for (var i = 1; i < chronik.Count; i++)
                Pruefe.Wahr(chronik[i - 1].Jahr >= chronik[i].Jahr, "neueste Jahr zuerst");
            // Gleich zweimal berechnet: dieselbe Reihenfolge (Determinismus).
            var nochmal = w.Familien.Chronik(f, konto);
            Pruefe.Gleich(string.Join("|", chronik.Select(e => e.Text)), string.Join("|", nochmal.Select(e => e.Text)));
        }),
    };
}
