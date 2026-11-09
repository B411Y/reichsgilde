using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Geheimnisse;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests: Geheimnisse in den Entscheidungen der KI-Häuser (Charaktersystem Teil 3c) – anzeigen, erpressen, schweigen, Ratsstimme, Spitzel auf
/// Ratsherren, zahlen als Opfer. Jeweils mit Gegenprobe. Die Daten würfeln keine Geheimnisse von selbst (<see cref="GeheimnisTests.DatenMit"/>).
/// </summary>
static class KiGeheimnisTests
{
    static Simulation NeueSimulation() =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: GeheimnisTests.DatenMit());

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    /// <summary>Ein KI-Haus (eine Schmiede, keine Expansion) mit diesen Zügen des Oberhaupts (immer mit einem der Persönlichkeit), katholisch, 12 AP.</summary>
    static (Konto Konto, Person Oberhaupt, KiRivale Ki) KiHaus(Stadtwirtschaft w, string name, params string[] zuege)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: 20_000);
        var f = w.Familien.Gruenden(k, Volk.Mensch, Geschlecht.Mann, alter: 30, glaube: Konfession.Katholisch);
        var o = f.Oberhaupt;
        o.ZuegeLeeren();
        foreach (var z in zuege.Append("Geduldig").Distinct()) o.ZugHinzufuegen(w.Daten.Charakterzuege!.Zug(z)!);
        k.Aktionspunkte.Setzen(12);
        w.BetriebEroeffnen($"Schmiede {name}", Betriebsart.Schmiede, k, startkarren: "");
        var ki = w.KiAnmelden(k, Partieaufbau.ProfilFuer(KiCharakter.Ausgewogen) with { MaxBetriebe = 0 });
        return (k, o, ki);
    }

    /// <summary>Ein Haus ohne KI (wie der Spieler).</summary>
    static (Konto Konto, Person Oberhaupt) Haus(Stadtwirtschaft w, string name)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: 20_000);
        var f = w.Familien.Gruenden(k, Volk.Mensch, Geschlecht.Mann, alter: 30, glaube: Konfession.Katholisch);
        f.Oberhaupt.ZuegeLeeren();
        f.Oberhaupt.ZugHinzufuegen(w.Daten.Charakterzuege!.Zug("Geduldig")!);
        k.Aktionspunkte.Setzen(12);
        return (k, f.Oberhaupt);
    }

    static Geheimnis Wissen(Stadtwirtschaft w, ICharakter inhaber, Geheimnisart art, Konto wisser)
    {
        var e = w.Geheimnisse.Entwickler(wisser, inhaber.Kennung, art, wissen: true);
        Pruefe.Wahr(e.Erfolg, e.Meldung);
        return w.Geheimnisse.Von(inhaber).First(g => g.Art == art);
    }

    static List<string> Mitschreiben(Stadtwirtschaft w)
    {
        var liste = new List<string>();
        w.Meldung += m => liste.Add(m);
        return liste;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Geheimnisse: Einen Mord zeigt die KI immer an; ein Haus, über das ihr Oberhaupt gut denkt, verschont sie", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var meldungen = Mitschreiben(w);
            var (v, vo) = Haus(w, "Haus Taeter");
            var (_, opfer) = Haus(w, "Haus Opfer");
            w.Geheimnisse.Mord(vo.Nummer, opfer);
            var (k1, _, _) = KiHaus(w, "Haus Kalt");
            var (k2, o2, _) = KiHaus(w, "Haus Freund");
            var g = Wissen(w, vo, Geheimnisart.Mord, k1);
            Wissen(w, vo, Geheimnisart.Mord, k2);
            w.Meinungen.TatEintragen(o2.Kennung, vo.Kennung, "probe", "Probe", 100);
            JahrBeenden(sim);
            Pruefe.Wahr(meldungen.Any(m => m.StartsWith("Haus Kalt:") && m.Contains("vor Gericht an")), string.Join(" | ", meldungen.Where(m => m.StartsWith("Haus "))));
            Pruefe.Wahr(w.Justiz.LetzteUrteile.Any(u => u.Anklaeger == k1 && u.Beklagter == v && u.Schuldig), "verurteilt");
            Pruefe.Wahr(g.Oeffentlich, "öffentlich");
            Pruefe.Wahr(!meldungen.Any(m => m.StartsWith("Haus Freund:") && m.Contains("vor Gericht an")), "Gegenprobe: verschont");
        }),

        ("KI-Geheimnisse: Gierig schickt die KI einen Erpresserbrief (10 % vom Bargeld, höchstens 1.500); ohne Gier, Hass und Wahl schweigt sie", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (v, vo) = Haus(w, "Haus Ziel");
            var (gierig, _, _) = KiHaus(w, "Haus Gier", "Gierig");
            var (still, _, _) = KiHaus(w, "Haus Still");
            var g = Wissen(w, vo, Geheimnisart.Ehebruch, gierig);
            Wissen(w, vo, Geheimnisart.Ehebruch, still);
            JahrBeenden(sim);
            var brief = w.Gauner.ForderungAn(v, gierig);
            Pruefe.Wahr(brief is { Geheimnis: var n } && n == g.Nummer, "Brief mit dem Geheimnis");
            Pruefe.Wahr(brief!.Betrag is > 0 and <= 1500, $"Betrag {brief.Betrag}");
            Pruefe.Wahr(w.Gauner.ForderungAn(v, still) is null && w.Geheimnisse.KenntNutzbar(still, g), "Gegenprobe: schweigt");
            Pruefe.Wahr(!g.Oeffentlich, "nicht öffentlich");
            // Das Opfer zahlt nicht: Im nächsten Jahr macht die KI die Drohung wahr
            Pruefe.Wahr(w.Ausfuehren(v, new ErpressungBeantworten(gierig.Inhaber, false)).Erfolg, "abgelehnt");
            JahrBeenden(sim);
            Pruefe.Wahr(g.Oeffentlich, "nach der Ablehnung angezeigt oder bloßgestellt");
        }),

        ("KI-Geheimnisse: Fehlt ihr die Stimme eines Ratsherrn beim eigenen Antrag, erpresst die KI sie mit seinem Geheimnis – ehrlich nie; " +
         "kennt sie keins, setzt sie einen Spitzel auf ihn an", () =>
        {
            foreach (var (zug, erwartet) in new[] { ("Verschlagen", true), ("Ehrlich", false) })
            {
                var sim = NeueSimulation();
                var w = sim.Wirtschaft!;
                var meldungen = Mitschreiben(w);
                var (k, _, _) = KiHaus(w, "Haus Hebel", zug);
                var amt = w.Rat.AmtNamens("Zöllner")!;
                amt.Ratsherr = null;
                amt.Konto = k;
                var sitz = w.Rat.AmtNamens("Bürgermeister")!;
                var r = sitz.Ratsherr!;
                Pruefe.Gleich(Fronstatut.FreieStadt, r.Haltung);
                Pruefe.Wahr(w.Ausfuehren(k, new StatutBeantragen(Fronstatut.Leibeigenschaft)).Erfolg, "Antrag");
                Wissen(w, r, Geheimnisart.Schulden, k);
                var ohne = w.Rat.AmtNamens("Stadtrichter")!.Ratsherr!; // auch dagegen, kein Geheimnis bekannt
                var vorher = w.Rat.AbstimmungsPrognose(k, Fronstatut.Leibeigenschaft).Ja;
                var jahr = sim.Zeit.Jahr;
                while (sim.Zeit.Jahr == jahr && w.Rat.LaufendeSitzung is null && !meldungen.Any(m => m.Contains("Ratsstimme") || m.Contains("Spitzel auf"))) sim.Tick();
                Pruefe.Gleich(erwartet, w.Rat.Erpresser(sitz) == k, $"{zug}: {string.Join(" | ", meldungen.Where(m => m.StartsWith("Haus Hebel")))}");
                if (erwartet)
                {
                    Pruefe.Gleich(vorher + 1, w.Rat.AbstimmungsPrognose(k, Fronstatut.Leibeigenschaft).Ja);
                    JahrBeenden(sim);
                    Pruefe.Wahr(meldungen.Any(m => m.StartsWith("Haus Hebel:") && m.Contains($"Spitzel auf {ohne.Name}")), "Spitzel auf den anderen");
                }
            }
        }),

        ("KI-Geheimnisse: Als Opfer zahlt die KI einen Brief, der billiger ist als Gericht und Schande – einen teuren nicht", () =>
        {
            foreach (var (betrag, zahlt) in new[] { 100.0, 9000.0 }.Select(b => (b, b < 1000)))
            {
                var sim = NeueSimulation();
                var w = sim.Wirtschaft!;
                var meldungen = Mitschreiben(w);
                var (k, o, ki) = KiHaus(w, "Haus Schuld");
                var (_, opfer) = Haus(w, "Haus Opfer");
                w.Geheimnisse.Mord(o.Nummer, opfer);
                var (h, _) = Haus(w, "Haus Brief");
                var g = Wissen(w, o, Geheimnisart.Mord, h);
                Pruefe.Wahr(ki.GeheimnisSchaden(g.Nummer) > 1000, $"Schaden {ki.GeheimnisSchaden(g.Nummer)}");
                Pruefe.Wahr(w.Ausfuehren(h, new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Gulden, betrag)).Erfolg, "Brief");
                JahrBeenden(sim);
                Pruefe.Wahr(w.Gauner.ForderungAn(k, h) is null, "beantwortet");
                Pruefe.Gleich(!zahlt, w.Geheimnisse.KenntNutzbar(h, g), $"Betrag {betrag}");
                Pruefe.Gleich(zahlt, meldungen.Any(m => m.StartsWith("Haus Schuld: zahlt Haus Brief")), string.Join(" | ", meldungen.Where(m => m.StartsWith("Haus Schuld"))));
            }
        }),
    };
}
