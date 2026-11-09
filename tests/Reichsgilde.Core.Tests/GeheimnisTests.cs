using System.IO.Compression;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Entwickler;
using Reichsgilde.Core.Geheimnisse;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Mehrspieler;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests für die Geheimnisse (Charaktersystem Teil 3c): Daten, Entstehen (Ratsherren, Ehebruch, Ketzerei, Schulden, Mord), Entdecken (Spitzel,
/// Gerede, Beichte, Ehepartner), Nutzen (Gulden, Ratsstimme, Anzeige, Bloßstellen), Folgen (Stress, Meinung, Ansehen), Entwicklerfenster und
/// Spielstand – jeweils mit Gegenprobe. Die meisten Tests würfeln nicht: Sie schalten den Zufall über die Daten ab (Chance 0) oder sicher an (1).
/// </summary>
static class GeheimnisTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json.Value));

    /// <summary>Ohne den Abschnitt "geheimnisse" – wie vor Teil 3c (alte Spielstände bringen solche Daten mit).</summary>
    static readonly Lazy<Wirtschaftsdaten> Ohne = new(() =>
    {
        var knoten = JsonNode.Parse(Json.Value)!.AsObject();
        knoten.Remove("geheimnisse");
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    });

    /// <summary>Ersetzt Texte der Daten – jeder muss genau einmal vorkommen (sonst prüfte der Test nichts).</summary>
    static string Ersetzen(string json, params (string Alt, string Neu)[] paare)
    {
        foreach (var (alt, neu) in paare)
        {
            var erst = json.IndexOf(alt, StringComparison.Ordinal);
            Pruefe.Wahr(erst >= 0 && json.IndexOf(alt, erst + 1, StringComparison.Ordinal) < 0, $"„{alt}“ genau einmal in den Daten");
            json = json.Replace(alt, neu);
        }
        return json;
    }

    /// <summary>Kein Zufall: keine Geheimnisse der Ratsherren, kein Gerede, keine Beichte, kein Ehepartner, keine jährlichen Ehebrüche und Ketzer.</summary>
    static readonly (string, string)[] Ruhig =
    {
        ("\"start\": 0.25, \"jeJahr\": 0.02", "\"start\": 0, \"jeJahr\": 0"),
        ("\"gerede\": 0.02", "\"gerede\": 0"),
        ("\"beichte\": 0.25", "\"beichte\": 0"),
        ("\"ehepartner\": 0.1,", "\"ehepartner\": 0,"),
        ("\"Hintergeht heimlich den Ehepartner.\", \"chance\": 0.015", "\"Hintergeht heimlich den Ehepartner.\", \"chance\": 0"),
        ("\"chance\": 0.01, \"nurMit\"", "\"chance\": 0, \"nurMit\""),
    };

    internal static Wirtschaftsdaten DatenMit(params (string, string)[] mehr) => Wirtschaftsdaten.AusJson(Ersetzen(Json.Value, Ruhig.Concat(mehr).ToArray()));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null, int startwert = 1400) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, wirtschaftsdaten: daten ?? DatenMit());

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    static (Konto Konto, Familie Familie) Haus(Stadtwirtschaft w, string name, params string[] zuege) => Haus(w, name, Konfession.Katholisch, zuege);

    static (Konto Konto, Familie Familie) Haus(Stadtwirtschaft w, string name, Konfession glaube, params string[] zuege)
    {
        var konto = w.KontoEroeffnen(name, startvermoegen: 20_000);
        var f = w.Familien.Gruenden(konto, Volk.Mensch, Geschlecht.Mann, alter: 30, glaube: glaube);
        konto.Aktionspunkte.Setzen(12);
        Zuege(w, f.Oberhaupt, zuege);
        return (konto, f);
    }

    static void Zuege(Stadtwirtschaft w, Person p, params string[] kennungen)
    {
        p.ZuegeLeeren();
        foreach (var k in kennungen) p.ZugHinzufuegen(w.Daten.Charakterzuege!.Zug(k)!);
    }

    static Person Heiraten(Stadtwirtschaft w, Konto konto)
    {
        var k = w.Familien.Kandidaten.First(k => k.Person.Geschlecht == Geschlecht.Frau);
        Zuege(w, k.Person, "Geduldig"); // mit Persönlichkeit, sonst bekäme sie am Jahresende eine (ohne Wirkung auf Geheimnisse)
        w.Familien.Vermaehlen(konto, k, 0);
        return w.Familien.Von(konto)!.Ehepartner!;
    }

    /// <summary>Gibt <paramref name="inhaber"/> ein Geheimnis dieser Art; mit <paramref name="wisser"/> weiß dieses Haus davon.</summary>
    static Geheimnis GeheimnisGeben(Stadtwirtschaft w, ICharakter inhaber, Geheimnisart art, Konto? wisser = null)
    {
        var e = w.Geheimnisse.Entwickler(wisser ?? w.Konten[0], inhaber.Kennung, art, wissen: wisser is not null);
        Pruefe.Wahr(e.Erfolg, e.Meldung);
        return w.Geheimnisse.Von(inhaber).First(g => g.Art == art);
    }

    static Befehlsergebnis Ok(Stadtwirtschaft w, Konto konto, Befehl befehl)
    {
        var e = w.Ausfuehren(konto, befehl);
        Pruefe.Wahr(e.Erfolg, $"{befehl.GetType().Name}: {e.Meldung}");
        return e;
    }

    static void Nein(Stadtwirtschaft w, Konto konto, Befehl befehl, string textInGrund)
    {
        var e = w.Ausfuehren(konto, befehl);
        Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains(textInGrund, StringComparison.Ordinal), $"{befehl.GetType().Name} abgelehnt mit „{textInGrund}“: {e.Meldung}");
    }

    static string Gruende(Stadtwirtschaft w, ICharakter von, ICharakter ueber) =>
        string.Join(" | ", w.Meinungen.Gruende(von, ueber).Select(g => $"{g.Text} {g.Wert}"));

    /// <summary>Setzt das Haus auf den Sitz des Zöllners (statt des Ratsherrn dort).</summary>
    static Amt InDenRat(Stadtwirtschaft w, Konto k, string amtName = "Zöllner")
    {
        var amt = w.Rat.AmtNamens(amtName)!;
        amt.Ratsherr = null;
        amt.Konto = k;
        return amt;
    }

    static bool GeheimnisseImSpielstand(string json)
    {
        var n = JsonNode.Parse(json)!.AsObject();
        foreach (var k in n.Select(p => p.Key).Where(k => k.Equals("daten", StringComparison.OrdinalIgnoreCase)).ToList()) n.Remove(k);
        var text = n.ToJsonString();
        return text.Contains("\"Geheimnisse\"", StringComparison.Ordinal) || TaeterDerKrankheit(text);
    }

    /// <summary>Steht bei einer Krankheit ein Täter? (Spuren haben ihr eigenes Feld „Taeter“.)</summary>
    static bool TaeterDerKrankheit(string json) => System.Text.RegularExpressions.Regex.IsMatch(json, "\"Krankheit\":\\s*\\{[^}]*\"Taeter\"");

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Geheimnisse: Daten – Datenversion 78, vier Arten (Mord 25, Ehebruch und Ketzerei 20 Beweiskraft, Schulden kein Vergehen), AP 2/2/2/1; " +
         "Fehler fallen auf; ohne Abschnitt keine Geheimnisse", () =>
        {
            var d = Daten.Value.Geheimnisse!;
            Pruefe.Gleich(78, Daten.Value.Version);
            Pruefe.Wahr(d.IstAktiv, "aktiv");
            Pruefe.Gleich("Ehebruch,Ketzerei,Schulden,Mord", string.Join(",", d.Arten.Keys.OrderBy(k => k)));
            Pruefe.Gleich("Gulden=2,Ratsstimme=2,Anzeige=2,Blossstellen=1", string.Join(",", Enum.GetValues<Geheimnisnutzung>().Select(n => $"{n}={d.Ap[n]}")));
            Pruefe.Gleich("Ehebruch=20,Ketzerei=20,Schulden=0,Mord=25", string.Join(",", Enum.GetValues<Geheimnisart>().Select(a => $"{a}={d.Art(a).Beweiskraft}")));
            Pruefe.Gleich(1000.0, d.Art(Geheimnisart.Mord).Busse);
            Pruefe.Gleich(1, d.Art(Geheimnisart.Mord).KerkerJahre);
            Pruefe.Wahr(d.Art(Geheimnisart.Ketzerei).NurMitStadtglaube, "Ketzerei nur mit Stadtglauben");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"Gesellig\": 1.5, \"Schoen\": 1.5", "\"Bucklig\": 1.5, \"Schoen\": 1.5")), "„Bucklig“");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"arten\": { \"Ehebruch\": 1, \"Schulden\": 1", "\"arten\": { \"Mord\": 1, \"Schulden\": 1")), "morden nicht");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"gerede\": 0.02", "\"gerede\": 2")), "gerede");
            // Gegenprobe: ohne Abschnitt
            Pruefe.Wahr(Ohne.Value.Geheimnisse is null, "ohne Abschnitt");
            var w = NeueSimulation(Ohne.Value).Wirtschaft!;
            Pruefe.Wahr(!w.Geheimnisse.IstAktiv && w.Geheimnisse.Alle.Count == 0, "ohne Abschnitt keine Geheimnisse");
        }),

        ("Geheimnisse: Ratsherren haben zu Partiebeginn mit 25 % eines (Ehebruch, Schulden oder Ketzerei), gleich bei gleichem Startwert; ohne Abschnitt keiner", () =>
        {
            int mit = 0, alle = 0;
            for (var s = 1; s <= 40; s++)
            {
                var w = NeueSimulation(Daten.Value, s).Wirtschaft!;
                alle += w.Rat.Ratsherren.Count;
                mit += w.Rat.Ratsherren.Count(r => w.Geheimnisse.Von(r).Any());
                Pruefe.Wahr(w.Geheimnisse.Alle.All(g => g.Art is Geheimnisart.Ehebruch or Geheimnisart.Schulden or Geheimnisart.Ketzerei && g.Inhaber.StartsWith("R:")),
                    "nur drei Arten, nur Ratsherren");
            }
            var anteil = (double)mit / alle;
            Pruefe.Wahr(anteil is > 0.17 and < 0.33, $"Anteil {anteil:0.00}");
            string Liste(int s) => string.Join(",", NeueSimulation(Daten.Value, s).Wirtschaft!.Geheimnisse.Alle.Select(g => $"{g.Inhaber}:{g.Art}"));
            Pruefe.Gleich(Liste(7), Liste(7));
            Pruefe.Wahr(Enumerable.Range(1, 5).Select(Liste).Distinct().Count() > 1, "andere Startwerte, andere Geheimnisse");
            Pruefe.Gleich(0, NeueSimulation(Ohne.Value, 7).Wirtschaft!.Geheimnisse.Alle.Count);
        }),

        ("Geheimnisse: Ehebruch – nur verheiratet, 1,5 % × Züge (gesellig × 1,5, ehrlich × 0,3), schlechte Meinung über den Ehepartner × 2, zermürbt × 1,5; " +
         "mit Chance 1 entsteht er am Jahresende", () =>
        {
            var basis = Daten.Value.Geheimnisse!.Art(Geheimnisart.Ehebruch).Chance;
            Pruefe.Wahr(basis > 0, "Grundchance");
            var echt = NeueSimulation(Daten.Value).Wirtschaft!; // die echten Daten
            var (_, ledig) = Haus(echt, "Haus Ledig", "Gesellig");
            Pruefe.Gleich(0.0, echt.Geheimnisse.EhebruchChance(ledig.Oberhaupt));
            var (ek, ef) = Haus(echt, "Haus Echt", "Gesellig");
            var ee = Heiraten(echt, ek);
            Pruefe.Ungefaehr(basis * 1.5, echt.Geheimnisse.EhebruchChance(ef.Oberhaupt), 1e-12);
            Pruefe.Ungefaehr(basis, echt.Geheimnisse.EhebruchChance(ee), 1e-12); // der Ehepartner ohne Züge
            Zuege(echt, ef.Oberhaupt, "Ehrlich");
            Pruefe.Ungefaehr(basis * 0.3, echt.Geheimnisse.EhebruchChance(ef.Oberhaupt), 1e-12);
            echt.Meinungen.TatEintragen(ef.Oberhaupt.Kennung, ee.Kennung, "probe", "Probe", -100);
            Pruefe.Ungefaehr(basis * 0.3 * 2, echt.Geheimnisse.EhebruchChance(ef.Oberhaupt), 1e-12);
            echt.Familien.StressSetzen(ef.Oberhaupt, 75);
            Pruefe.Ungefaehr(basis * 0.3 * 2 * 1.5, echt.Geheimnisse.EhebruchChance(ef.Oberhaupt), 1e-12);
            // Mit Chance 1: beide Eheleute, nicht der Ledige
            var sicher = NeueSimulation(Wirtschaftsdaten.AusJson(Ersetzen(Json.Value, Ruhig.Where(x => !x.Item1.Contains("Hintergeht")).Append(
                ("\"Hintergeht heimlich den Ehepartner.\", \"chance\": 0.015", "\"Hintergeht heimlich den Ehepartner.\", \"chance\": 1")).ToArray())));
            var ws = sicher.Wirtschaft!;
            var (sk, sf) = Haus(ws, "Haus Hold", "Geduldig");
            var se = Heiraten(ws, sk);
            var (_, lf) = Haus(ws, "Haus Ledig", "Geduldig");
            JahrBeenden(sicher);
            Pruefe.Wahr(ws.Geheimnisse.Hat(sf.Oberhaupt, Geheimnisart.Ehebruch) && ws.Geheimnisse.Hat(se, Geheimnisart.Ehebruch), "beide Eheleute");
            Pruefe.Wahr(!ws.Geheimnisse.Hat(lf.Oberhaupt, Geheimnisart.Ehebruch), "Gegenprobe: der Ledige nicht");
        }),

        ("Geheimnisse: Ketzerei – beim Konvertieren und beim Übertritt durch Heirat (Chance 1: fromm ja, zynisch nie); Zyniker jedes Jahr; sonst nicht", () =>
        {
            var daten = DatenMit(("\"wechsel\": 0.2", "\"wechsel\": 1"), ("\"chance\": 0, \"nurMit\"", "\"chance\": 1, \"nurMit\""));
            var sim = NeueSimulation(daten);
            var w = sim.Wirtschaft!;
            var (k1, f1) = Haus(w, "Haus Fromm", "Fromm");
            var (k2, f2) = Haus(w, "Haus Spott", "Zynisch");
            Ok(w, k1, new Konvertieren(Konfession.Evangelisch));
            Ok(w, k2, new Konvertieren(Konfession.Evangelisch));
            Pruefe.Wahr(w.Geheimnisse.Hat(f1.Oberhaupt, Geheimnisart.Ketzerei), "fromm: heimlich beim alten Glauben");
            Pruefe.Wahr(!w.Geheimnisse.Hat(f2.Oberhaupt, Geheimnisart.Ketzerei), "zynisch: kein Wurf beim Wechsel (Faktor 0)");
            // Übertritt bei der Heirat: Braut eines anderen Glaubens
            var (k3, f3) = Haus(w, "Haus Braut", Konfession.Katholisch);
            var kandidat = w.Familien.Kandidaten.First(k => k.Person.Geschlecht == Geschlecht.Frau);
            kandidat.Person.ZuegeLeeren();
            kandidat.Person.Glaube = Konfession.Evangelisch;
            w.Familien.Vermaehlen(k3, kandidat, 0);
            Pruefe.Wahr(w.Geheimnisse.Hat(f3.Ehepartner!, Geheimnisart.Ketzerei), "Übertritt bei der Heirat");
            var (k4, f4) = Haus(w, "Haus Gleich", Konfession.Katholisch, "Fromm");
            var gleich = w.Familien.Kandidaten.First(k => k.Person.Geschlecht == Geschlecht.Frau);
            gleich.Person.Glaube = Konfession.Katholisch;
            w.Familien.Vermaehlen(k4, gleich, 0);
            Pruefe.Wahr(!w.Geheimnisse.Hat(f4.Ehepartner!, Geheimnisart.Ketzerei), "Gegenprobe: gleicher Glaube, kein Übertritt");
            // Jährlich nur der Zyniker
            JahrBeenden(sim);
            Pruefe.Wahr(w.Geheimnisse.Hat(f2.Oberhaupt, Geheimnisart.Ketzerei), "Zyniker: jährlich");
            Pruefe.Wahr(!w.Geheimnisse.Hat(f4.Oberhaupt, Geheimnisart.Ketzerei), "Gegenprobe: kein Zyniker, kein Wechsel");
        }),

        ("Geheimnisse: Schulden – Bargeld im Minus am Jahresende: das Oberhaupt hütet sie; sind sie getilgt, ist das Geheimnis vorbei", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (k1, f1) = Haus(w, "Haus Pump");
            var (_, f2) = Haus(w, "Haus Reich");
            k1.Familienkosten(25_000);
            JahrBeenden(sim);
            Pruefe.Wahr(w.Geheimnisse.Hat(f1.Oberhaupt, Geheimnisart.Schulden), "Schulden");
            Pruefe.Wahr(!w.Geheimnisse.Hat(f2.Oberhaupt, Geheimnisart.Schulden), "Gegenprobe: reich");
            k1.Gaunerkosten(-50_000);
            JahrBeenden(sim);
            Pruefe.Wahr(!w.Geheimnisse.Hat(f1.Oberhaupt, Geheimnisart.Schulden), "getilgt: vorbei");
        }),

        ("Geheimnisse: Mord – stirbt das Opfer an einer Krankheit aus Krötenschleim oder Giftdolch (Täter vermerkt), hütet das Oberhaupt des Auftraggebers " +
         "einen Mord; ohne Täter keiner", () =>
        {
            var daten = DatenMit(("\"Lungenentzündung\": { \"chance\": 0.02, \"talente\": { \"Handwerk\": 1, \"Kampf\": 1 }, \"sterben\": 0.08 }",
                "\"Lungenentzündung\": { \"chance\": 0.02, \"talente\": { \"Handwerk\": 1, \"Kampf\": 1 }, \"sterben\": 1 }"));
            var sim = NeueSimulation(daten);
            var w = sim.Wirtschaft!;
            var (_, ft) = Haus(w, "Haus Gift");
            var (ko, fo) = Haus(w, "Haus Opfer");
            var erbe = w.Familien.KindAufnehmen(ko, 25);
            var (kn, fn) = Haus(w, "Haus Natur");
            w.Familien.KindAufnehmen(kn, 25);
            var natur = fn.Oberhaupt; // nach dem Tod ist der Erbe Oberhaupt
            var opfer = fo.Oberhaupt;
            Pruefe.Wahr(w.Gesundheit.Erkranken(ko, opfer, "Lungenentzündung", taeter: ft.Oberhaupt), "erkrankt");
            Pruefe.Gleich(ft.Oberhaupt.Nummer, opfer.Krankheit!.Taeter);
            Pruefe.Wahr(w.Gesundheit.Erkranken(kn, natur, "Lungenentzündung"), "erkrankt ohne Täter");
            JahrBeenden(sim);
            Pruefe.Wahr(!opfer.Lebt && !natur.Lebt, "beide tot");
            var mord = w.Geheimnisse.Von(ft.Oberhaupt).Single(g => g.Art == Geheimnisart.Mord);
            Pruefe.Gleich(opfer.Nummer, mord.Opfer);
            Pruefe.Wahr(w.Geheimnisse.Beschreibung(mord).Contains(opfer.Name), w.Geheimnisse.Beschreibung(mord));
            Pruefe.Gleich(1, w.Geheimnisse.Alle.Count(g => g.Art == Geheimnisart.Mord)); // Gegenprobe: ohne Täter kein Mord
            _ = erbe;
            // Ohne Abschnitt wird kein Täter vermerkt
            var ohne = NeueSimulation(Ohne.Value).Wirtschaft!;
            var (_, ot) = Haus(ohne, "Haus Gift");
            var (oo, of) = Haus(ohne, "Haus Opfer");
            ohne.Gesundheit.Erkranken(oo, of.Oberhaupt, "Ruhr", taeter: ot.Oberhaupt);
            Pruefe.Gleich((int?)null, of.Oberhaupt.Krankheit!.Taeter);
        }),

        ("Geheimnisse: Spitzel auf ein Haus findet am Gerichtstag Geheimnisse von Oberhaupt, Ehepartner und Erbe (Chance 1), mit 0 nichts; " +
         "Spitzel auf einen Ratsherrn (R:Name, 300 Gulden, 1 AP) findet seine", () =>
        {
            foreach (var sicher in new[] { true, false })
            {
                var daten = DatenMit(sicher
                    ? new[] { ("\"spitzel\": 0.3, \"spitzelJeStern\": 0.03, \"spitzelMin\": 0.1, \"spitzelMax\": 0.6, \"spitzelRatsherr\": 0.4",
                              "\"spitzel\": 1, \"spitzelJeStern\": 0, \"spitzelMin\": 1, \"spitzelMax\": 1, \"spitzelRatsherr\": 1") }
                    : new[] { ("\"spitzel\": 0.3, \"spitzelJeStern\": 0.03, \"spitzelMin\": 0.1, \"spitzelMax\": 0.6, \"spitzelRatsherr\": 0.4",
                              "\"spitzel\": 0, \"spitzelJeStern\": 0, \"spitzelMin\": 0, \"spitzelMax\": 0, \"spitzelRatsherr\": 0") });
                var sim = NeueSimulation(daten);
                var w = sim.Wirtschaft!;
                var (a, _) = Haus(w, "Haus Spaeher");
                var (b, fb) = Haus(w, "Haus Ziel");
                var e = Heiraten(w, b);
                var g1 = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Ehebruch);
                var g2 = GeheimnisGeben(w, e, Geheimnisart.Ehebruch);
                var r = w.Rat.Ratsherren[0];
                var g3 = GeheimnisGeben(w, r, Geheimnisart.Schulden);
                Ok(w, a, new SpitzelAnsetzen(b.Inhaber));
                var vorher = (a.Vermoegen, a.Aktionspunkte.Vorrat);
                Ok(w, a, new SpitzelAnsetzen(r.Kennung));
                Pruefe.Gleich((vorher.Vermoegen - 300, vorher.Vorrat - 1), (a.Vermoegen, a.Aktionspunkte.Vorrat));
                Nein(w, a, new SpitzelAnsetzen(r.Kennung), "schon ein Spitzel");
                Nein(w, a, new SpitzelAnsetzen("R:Niemand"), "Niemand");
                JahrBeenden(sim);
                Pruefe.Gleich(sicher, w.Geheimnisse.KenntNutzbar(a, g1), $"Oberhaupt ({sicher})");
                Pruefe.Gleich(sicher, w.Geheimnisse.KenntNutzbar(a, g2), $"Ehepartner ({sicher})");
                Pruefe.Gleich(sicher, w.Geheimnisse.KenntNutzbar(a, g3), $"Ratsherr ({sicher})");
                Pruefe.Wahr(!sicher || w.Justiz.LetzterGerichtstag.Any(z => z.Contains("2 Geheimnisse") && z.Contains("Ehebruch")), "Bericht nennt es");
            }
        }),

        ("Geheimnisse: Gerede – mit Chance 1 erfährt jedes Haus jedes fremde Geheimnis (nicht das eigene, das kennt es ohnehin); mit 0 keines", () =>
        {
            foreach (var chance in new[] { "1", "0" })
            {
                var sim = NeueSimulation(Wirtschaftsdaten.AusJson(Ersetzen(Json.Value, Ruhig.Where(x => x.Item1 != "\"gerede\": 0.02").Append(("\"gerede\": 0.02", $"\"gerede\": {chance}")).ToArray())));
                var w = sim.Wirtschaft!;
                var (a, _) = Haus(w, "Haus Ohr");
                var (b, fb) = Haus(w, "Haus Mund");
                var g = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Ketzerei);
                JahrBeenden(sim);
                Pruefe.Gleich(chance == "1", w.Geheimnisse.KenntNutzbar(a, g), $"Gerede {chance}");
                Pruefe.Wahr(w.Geheimnisse.Wissen(b, g) is null, "das eigene Haus bekommt keinen Eintrag");
                Pruefe.Wahr(w.Geheimnisse.Sichtbar(b, fb.Oberhaupt).Contains(g), "das eigene Haus sieht es trotzdem");
            }
        }),

        ("Geheimnisse: Beichte – Fromme beichten, eine Kirche desselben Glaubens erfährt es (Chance 1), nutzen erst nach dem Beichtbruch (ein Vergehen); " +
         "nicht fromm oder anderer Glaube: nichts", () =>
        {
            var sim = NeueSimulation(Wirtschaftsdaten.AusJson(Ersetzen(Json.Value, Ruhig.Where(x => x.Item1 != "\"beichte\": 0.25").Append(("\"beichte\": 0.25", "\"beichte\": 1")).ToArray())));
            var w = sim.Wirtschaft!;
            var (p, _) = Haus(w, "Haus Psalm", Konfession.Katholisch);
            var kirche = w.BetriebEroeffnen("Kirche Psalm", Betriebsart.Kirche, p, startkarren: "");
            w.Einstellen(kirche, "Geselle", 1);
            var (b, fb) = Haus(w, "Haus Reue", Konfession.Katholisch, "Fromm");
            var (_, fc) = Haus(w, "Haus Kalt", Konfession.Katholisch, "Zynisch"); // mit Persönlichkeit, sonst bekäme er am Jahresende eine
            var (_, fd) = Haus(w, "Haus Fremd", Konfession.Evangelisch, "Fromm");
            var g = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Ehebruch);
            var gc = GeheimnisGeben(w, fc.Oberhaupt, Geheimnisart.Ehebruch);
            var gd = GeheimnisGeben(w, fd.Oberhaupt, Geheimnisart.Ehebruch);
            JahrBeenden(sim);
            Pruefe.Wahr(w.Geheimnisse.Wissen(p, g) is { AusBeichte: true }, "aus der Beichte");
            Pruefe.Wahr(w.Geheimnisse.Wissen(p, gc) is null, $"Gegenprobe: nicht fromm ({w.Geheimnisse.Wissen(p, gc)?.Quelle}; {string.Join(",", fc.Oberhaupt.Zuege.Select(z => z.Kennung))})");
            Pruefe.Wahr(w.Geheimnisse.Wissen(p, gd) is null, "Gegenprobe: anderer Glaube");
            Pruefe.Wahr(w.Geheimnisse.NutzenVerboten(p, g, Geheimnisnutzung.Blossstellen)!.Contains("Beichte"), "erst brechen");
            p.Aktionspunkte.Setzen(12);
            Ok(w, p, new BeichtgeheimnisBrechen(b.Inhaber));
            Pruefe.Wahr(w.Geheimnisse.KenntNutzbar(p, g), "nach dem Beichtbruch nutzbar");
            Pruefe.Wahr(w.Justiz.SpurenVon(p).Any(s => s.Art == Vergehen.Beichtbruch), "ein Vergehen");
        }),

        ("Geheimnisse: Ratsherren – auch ein Ratsherr beichtet (Beichtgeheimnis brechen mit „R:Name“ macht es nutzbar, Gegenprobe ohne Bruch); " +
         "bloßgestellt bekommt er als Amtsinhaber weniger Wahlpunkte (zwei Jahre)", () =>
        {
            // Ein Ratsherr mit Glauben und Zügen; beichten lassen wir seinen ersten Zug (die Züge der Ratsherren stehen fest)
            var probe = NeueSimulation().Wirtschaft!;
            var r0 = probe.Rat.Aemter.Select(x => x.Ratsherr).OfType<Ratsherr>().First(x => x.Zuege.Count > 0);
            var glaube = probe.Glaube.GlaubeVon(r0);
            var daten = Wirtschaftsdaten.AusJson(Ersetzen(Json.Value, Ruhig.Where(x => x.Item1 != "\"beichte\": 0.25")
                .Append(("\"beichte\": 0.25", "\"beichte\": 1")).Append(("\"beichteZuege\": [ \"Fromm\", \"Pilger\" ]", $"\"beichteZuege\": [ \"{r0.Zuege[0].Kennung}\" ]")).ToArray()));
            var sim = NeueSimulation(daten);
            var w = sim.Wirtschaft!;
            var r = w.Rat.RatsherrNamens(r0.Name)!;
            var amt = w.Rat.Aemter.First(x => x.Ratsherr == r);
            var (p, _) = Haus(w, "Haus Psalm", glaube);
            var kirche = w.BetriebEroeffnen("Kirche Psalm", Betriebsart.Kirche, p, startkarren: "");
            w.Einstellen(kirche, "Geselle", 1);
            var g = GeheimnisGeben(w, r, Geheimnisart.Ehebruch);
            JahrBeenden(sim);
            Pruefe.Wahr(w.Geheimnisse.Wissen(p, g) is { AusBeichte: true }, "aus der Beichte");
            Pruefe.Wahr(!w.Geheimnisse.KenntNutzbar(p, g), "Gegenprobe: ohne Bruch nicht nutzbar");
            p.Aktionspunkte.Setzen(12);
            Ok(w, p, new BeichtgeheimnisBrechen(r.Kennung));
            Pruefe.Wahr(w.Geheimnisse.KenntNutzbar(p, g), "nach dem Beichtbruch nutzbar");
            Pruefe.Wahr(w.Justiz.SpurenVon(p).Any(s => s.Art == Vergehen.Beichtbruch), "ein Vergehen");
            // Bloßstellen: Der Ratsherr im Amt verliert Wahlpunkte (Ehebruch −5), ein anderer Ratsherr nicht
            var waehler = w.Rat.Aemter.Select(x => x.Ratsherr).OfType<Ratsherr>().First(x => x != r);
            var anderesAmt = w.Rat.Aemter.First(x => x.Ratsherr is { } y && y != r && y != waehler);
            var (vorher, anderes) = (w.Rat.Punkte(waehler, amt, null), w.Rat.Punkte(waehler, anderesAmt, null));
            Ok(w, p, new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Blossstellen));
            Pruefe.Ungefaehr(vorher - 5, w.Rat.Punkte(waehler, amt, null), 1e-9);
            Pruefe.Ungefaehr(anderes, w.Rat.Punkte(waehler, anderesAmt, null), 1e-9);
            JahrBeenden(sim);
            JahrBeenden(sim);
            Pruefe.Gleich(0.0, w.Geheimnisse.AnsehenAbzug(r));
        }),

        ("Geheimnisse: Der Ehepartner entdeckt einen Ehebruch (Chance 1) – privat: Meinung −40, beide +15 Stress, nicht öffentlich; mit 0 nichts", () =>
        {
            foreach (var chance in new[] { "1", "0" })
            {
                var sim = NeueSimulation(Wirtschaftsdaten.AusJson(Ersetzen(Json.Value, Ruhig.Where(x => x.Item1 != "\"ehepartner\": 0.1,").Append(("\"ehepartner\": 0.1,", $"\"ehepartner\": {chance},")).ToArray())));
                var w = sim.Wirtschaft!;
                var (k, f) = Haus(w, "Haus Hold");
                var e = Heiraten(w, k);
                var g = GeheimnisGeben(w, f.Oberhaupt, Geheimnisart.Ehebruch);
                JahrBeenden(sim);
                var entdeckt = chance == "1";
                Pruefe.Gleich(entdeckt, g.EhepartnerWeiss);
                Pruefe.Wahr(!g.Oeffentlich, "privat");
                // −40, am Jahresende nach der Ratssitzung schon um ein Stück verblasst
                Pruefe.Gleich(entdeckt, w.Meinungen.Gruende(e, f.Oberhaupt).Any(x => x.Text == "Ehebruch entdeckt" && x.Wert is < -20 and >= -40), Gruende(w, e, f.Oberhaupt));
                Pruefe.Gleich(entdeckt, e.Stresslage.Eintraege.Any(x => x.Wert == 15 && x.Text.Contains("Ehebruch")), string.Join("|", e.Stresslage.Eintraege));
            }
        }),

        ("Geheimnisse: Erpressen um Gulden – ein Erpresserbrief mit dem Geheimnis (2 AP, Spur, +10 Stress); wer zahlt, dessen Geheimnis vergisst der Erpresser; " +
         "wer ablehnt, bei dem bleibt es; Ratsherr, eigenes und unbekanntes abgelehnt", () =>
        {
            foreach (var zahlen in new[] { true, false })
            {
                var w = NeueSimulation().Wirtschaft!;
                var (a, fa) = Haus(w, "Haus Hand");
                var (b, fb) = Haus(w, "Haus Fuss");
                var g = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Ehebruch, wisser: a);
                var eigen = GeheimnisGeben(w, fa.Oberhaupt, Geheimnisart.Schulden);
                var r = w.Rat.Ratsherren[0];
                var rg = GeheimnisGeben(w, r, Geheimnisart.Ehebruch, wisser: a);
                var unbekannt = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Schulden);
                var stress = w.Familien.Stress(fb.Oberhaupt);
                Ok(w, a, new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Gulden, 500));
                Pruefe.Gleich(10, a.Aktionspunkte.Vorrat);
                Pruefe.Gleich(g.Nummer, w.Gauner.ForderungAn(b, a)!.Geheimnis);
                Pruefe.Gleich(stress + 10, w.Familien.Stress(fb.Oberhaupt));
                Pruefe.Wahr(w.Justiz.Beweise(b, a).Any(s => s.Art == Vergehen.Erpressung), "der Brief ist ein Beweis");
                Nein(w, a, new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Gulden, 500), "schon einen Brief");
                Nein(w, a, new GeheimnisNutzen(rg.Nummer, Geheimnisnutzung.Gulden, 500), "Ratsherrn");
                Nein(w, a, new GeheimnisNutzen(eigen.Nummer, Geheimnisnutzung.Gulden, 500), "eigenen Hauses");
                Nein(w, a, new GeheimnisNutzen(unbekannt.Nummer, Geheimnisnutzung.Gulden, 500), "nichts");
                var (va, vb) = (a.Vermoegen, b.Vermoegen);
                Ok(w, b, new ErpressungBeantworten(a.Inhaber, zahlen));
                Pruefe.Gleich(zahlen ? va + 500 : va, a.Vermoegen);
                Pruefe.Gleich(zahlen ? vb - 500 : vb, b.Vermoegen);
                Pruefe.Gleich(!zahlen, w.Geheimnisse.KenntNutzbar(a, g), zahlen ? "gezahlt: vergessen" : "abgelehnt: er weiß es noch");
                Pruefe.Wahr(zahlen || w.Geheimnisse.Wissen(a, g)!.BriefIm == w.Jahr, "der Brief ist vermerkt");
            }
        }),

        ("Geheimnisse: Erpressen um die Ratsstimme – in der nächsten Sitzung stimmt der Ratsherr wie der Erpresser (Antrag, Wahl); einmal je Geheimnis und Jahr, " +
         "Spur, −20 Meinung; ohne eigenen Sitz des Erpressers stimmt er wie sonst; nach der Sitzung ist es vorbei", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (a, _) = Haus(w, "Haus Hebel");
            InDenRat(w, a);
            // ein Ratsherr mit Amt (möglichst hoch, damit er bei der Wahl unten mitwählt), der dem Antrag nicht zustimmt: Haltung an einem Rand, Antrag am anderen
            var sitz = w.Rat.Aemter.OrderByDescending(x => x.Stufe).First(x => x.Ratsherr is { } rh && rh.Haltung != Fronstatut.Bestienfron && rh.Haltung != w.Statut);
            var r = sitz.Ratsherr!;
            var statut = r.Haltung == Fronstatut.FreieStadt ? Fronstatut.Leibeigenschaft : Fronstatut.FreieStadt;
            Pruefe.Wahr(statut != w.Statut, "ein Antrag ist möglich");
            Ok(w, a, new StatutBeantragen(statut));
            var g = GeheimnisGeben(w, r, Geheimnisart.Ehebruch, wisser: a);
            Pruefe.Gleich(false, w.Rat.StatutStimmeVonSitz(w.Rat.StimmSitz(sitz), a, statut));
            var (ja, _, _, _) = w.Rat.AbstimmungsPrognose(a, statut);
            Ok(w, a, new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Ratsstimme));
            Pruefe.Gleich(a, w.Rat.Erpresser(sitz));
            Pruefe.Gleich(true, w.Rat.StatutStimmeVonSitz(w.Rat.StimmSitz(sitz), a, statut));
            Pruefe.Gleich(ja + 1, w.Rat.AbstimmungsPrognose(a, statut).Ja);
            Pruefe.Wahr(w.Rat.Sitzungsvorschau().First(p => p.Thema == Sitzungsthema.Statut).Stimmen.Any(s => s.Sitz == sitz && s.Begruendung.StartsWith("Erpresst von")),
                "Begründung in der Vorschau");
            Nein(w, a, new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Ratsstimme), "schon");
            Pruefe.Wahr(w.Justiz.SpurenVon(a).Any(s => s.Art == Vergehen.Erpressung && s.Betrag == 0), "Spur Erpressung");
            Pruefe.Wahr(Gruende(w, r, w.Familien.Von(a)!.Oberhaupt).Contains("Hat ihn erpresst -20"), Gruende(w, r, w.Familien.Von(a)!.Oberhaupt));
            // Wahl: Er stimmt für den Erpresser, wenn der kandidiert
            var amt = w.Rat.WaehlbareAemter(a).FirstOrDefault();
            if (amt is null) { a.Stand = 4; amt = w.Rat.WaehlbareAemter(a).First(); }
            Ok(w, a, new AmtBewerben(amt.Name));
            var kandidaten = w.Rat.KandidatenListe(amt);
            Pruefe.Wahr(w.Rat.WahlWaehler(amt, kandidaten).Contains(sitz), $"{sitz.Name} wählt den {amt.Name} mit");
            Pruefe.Gleich(a, w.Rat.WahlStimmeVonSitz(sitz, amt, kandidaten).Single());
            // Gegenprobe: Ein Erpresser ohne eigenen Sitz – der Ratsherr stimmt beim Antrag eines anderen wie sonst
            var (c, _) = Haus(w, "Haus Draussen");
            var r2sitz = w.Rat.Aemter.First(x => x.Ratsherr is not null && x != sitz);
            var g2 = GeheimnisGeben(w, r2sitz.Ratsherr!, Geheimnisart.Schulden, wisser: c);
            var vorher = w.Rat.StatutStimmeVonSitz(r2sitz, a, statut);
            Ok(w, c, new GeheimnisNutzen(g2.Nummer, Geheimnisnutzung.Ratsstimme));
            Pruefe.Gleich(r2sitz, w.Rat.StimmSitz(r2sitz));
            Pruefe.Gleich(vorher, w.Rat.StatutStimmeVonSitz(w.Rat.StimmSitz(r2sitz), a, statut));
            // nach der Sitzung vorbei – im nächsten Jahr wieder möglich
            JahrBeenden(sim);
            Pruefe.Gleich(0, w.Geheimnisse.ErpressteStimmen.Count);
            Pruefe.Gleich((Konto?)null, w.Rat.Erpresser(sitz));
        }),

        ("Geheimnisse: Eine erpresste Ratsstimme eines Hauses mit Sitz – das Haus darf in der Sitzung nicht selbst stimmen (Grund genannt), " +
         "aber nur, wo der Erpresser die Stimme bestimmt: ohne Sitz des Erpressers und bei einer Wahl ohne ihn stimmt es selbst", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (a, _) = Haus(w, "Haus Hebel");
            var (b, fb) = Haus(w, "Haus Sitz");
            var zoellner = InDenRat(w, a);
            var sitzB = InDenRat(w, b, "Stadthauptmann");
            var statut = w.Statut == Fronstatut.FreieStadt ? Fronstatut.Bestienfron : Fronstatut.FreieStadt;
            Ok(w, a, new StatutBeantragen(statut));
            Pruefe.Gleich((string?)null, w.Rat.StatutStimmeVerboten(b));
            var g = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Ehebruch, wisser: a);
            Ok(w, a, new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Ratsstimme));
            Pruefe.Wahr(w.Rat.StatutStimmeVerboten(b)?.Contains("erpresst") == true, w.Rat.StatutStimmeVerboten(b) ?? "");
            Nein(w, b, new StatutStimme(false), "erpresst");
            Pruefe.Gleich(true, w.Rat.StatutStimmeVonSitz(w.Rat.StimmSitz(sitzB), a, statut));
            Pruefe.Wahr(w.Justiz.Beweise(b, a).Any(s => s.Art == Vergehen.Erpressung), "das Haus weiß, wer es zwingt");
            // Wahl zum Gefängniswärter (Stufe 1): Es wählen nur höhere Ämter – der Stadthauptmann ja, der Erpresser (Zöllner) nicht. Er kandidiert
            // nicht, also wählt das erpresste Haus selbst (die Gegenprobe mit kandidierendem Erpresser unten).
            var (d, _) = Haus(w, "Haus Bewerber");
            d.Stand = 4;
            var wahl = w.Rat.AmtNamens("Gefängniswärter")!;
            Pruefe.Wahr(w.Rat.Waehler(wahl).Contains(sitzB) && !w.Rat.Waehler(wahl).Contains(zoellner), "der Stadthauptmann wählt mit, der Zöllner nicht");
            Ok(w, d, new AmtBewerben(wahl.Name));
            Pruefe.Gleich((string?)null, w.Rat.WahlStimmeVerboten(b, wahl));
            Ok(w, b, new WahlStimme(wahl.Name, d.Inhaber));
            Pruefe.Gleich(d, w.Rat.WahlStimmeVonSitz(sitzB, wahl, w.Rat.KandidatenListe(wahl)).Single());

            // Ein Erpresser ohne Sitz: Beim Antrag stimmt das erpresste Haus selbst (seine Stimme zählt); kandidiert er bei einer Wahl, stimmt es für ihn
            var w2 = NeueSimulation().Wirtschaft!;
            var (a2, _) = Haus(w2, "Haus Antrag");
            var (b2, fb2) = Haus(w2, "Haus Sitz");
            var (c2, _) = Haus(w2, "Haus Draussen");
            InDenRat(w2, a2);
            var sitzB2 = InDenRat(w2, b2, "Stadthauptmann");
            Ok(w2, a2, new StatutBeantragen(statut));
            var g2 = GeheimnisGeben(w2, fb2.Oberhaupt, Geheimnisart.Ehebruch, wisser: c2);
            Ok(w2, c2, new GeheimnisNutzen(g2.Nummer, Geheimnisnutzung.Ratsstimme));
            Pruefe.Gleich(c2, w2.Rat.Erpresser(sitzB2));
            Pruefe.Gleich(sitzB2, w2.Rat.StimmSitz(sitzB2));
            Pruefe.Gleich((string?)null, w2.Rat.StatutStimmeVerboten(b2));
            Ok(w2, b2, new StatutStimme(false));
            Pruefe.Gleich(false, w2.Rat.StatutStimmeVonSitz(w2.Rat.StimmSitz(sitzB2), a2, statut));
            c2.Stand = 4;
            var wahl2 = w2.Rat.AmtNamens("Gefängniswärter")!;
            Ok(w2, c2, new AmtBewerben(wahl2.Name));
            Pruefe.Wahr(w2.Rat.WahlStimmeVerboten(b2, wahl2)?.Contains("für Haus Draussen") == true, w2.Rat.WahlStimmeVerboten(b2, wahl2) ?? "");
            Pruefe.Gleich(c2, w2.Rat.WahlStimmeVonSitz(sitzB2, wahl2, w2.Rat.KandidatenListe(wahl2)).Single());
        }),

        ("Geheimnisse: Anzeige – gegen ein Haus ein Verfahren, Mord 25 Beweiskraft: schuldig, Buße 1.000, ein Jahr Kerker, Pranger; danach öffentlich. " +
         "Ketzerei ohne Stadtglauben und Schulden nicht anzeigbar", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (a, _) = Haus(w, "Haus Klaeger");
            var (b, fb) = Haus(w, "Haus Taeter");
            var (_, fc) = Haus(w, "Haus Opfer");
            w.Geheimnisse.Mord(fb.Oberhaupt.Nummer, fc.Oberhaupt);
            var mord = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Mord, wisser: a);
            var ketzer = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Ketzerei, wisser: a);
            var schulden = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Schulden, wisser: a);
            Nein(w, a, new GeheimnisNutzen(ketzer.Nummer, Geheimnisnutzung.Anzeige), "Stadtglaube");
            Nein(w, a, new GeheimnisNutzen(schulden.Nummer, Geheimnisnutzung.Anzeige), "kein Vergehen");
            var stress = w.Familien.Stress(fb.Oberhaupt);
            Ok(w, a, new GeheimnisNutzen(mord.Nummer, Geheimnisnutzung.Anzeige));
            Pruefe.Wahr(mord.Oeffentlich, "öffentlich");
            Pruefe.Gleich(stress + 15, w.Familien.Stress(fb.Oberhaupt));
            var v = w.Justiz.VerfahrenGegen(a, b)!;
            Pruefe.Gleich(Geheimnisart.Mord, v.GeheimnisArt);
            Pruefe.Gleich(25.0, w.Justiz.Beweiskraft(v));
            Pruefe.Wahr(Stadtgericht.IstSchuldig(w.Justiz.Prognose(v)), "sicher schuldig");
            Nein(w, a, new GeheimnisNutzen(mord.Nummer, Geheimnisnutzung.Blossstellen), "ganze Stadt");
            Nein(w, a, new GeheimnisNutzen(ketzer.Nummer, Geheimnisnutzung.Gulden, 500), "Anklage"); // wie der Brief mit Beweisen: zu spät
            JahrBeenden(sim);
            var urteil = w.Justiz.LetzteUrteile.Single(u => u.Beklagter == b);
            Pruefe.Wahr(urteil.Schuldig && urteil.Text.Contains("Mord") && urteil.Text.Contains("Kerker"), urteil.Text);
            Pruefe.Wahr(w.Justiz.ImKerker(b) && w.Justiz.PrangerBis(b) is not null, "Kerker und Pranger");
            Pruefe.Wahr(urteil.Text.Contains("Buße 1.000 "), urteil.Text); // Mord ohne weitere Beweise: genau die Buße der Art
        }),

        ("Geheimnisse: Anzeige gegen einen Ratsherrn – schuldig verliert er seinen Sitz und rückt drei Jahre in kein Amt nach; freigesprochen zahlt der Ankläger", () =>
        {
            foreach (var schuldig in new[] { true, false })
            {
                var daten = DatenMit(("\"beweiskraft\": 20, \"busse\": 300, \"ansehen\": 5, \"meinungEhepartner\"",
                    $"\"beweiskraft\": {(schuldig ? 40 : 1)}, \"busse\": 300, \"ansehen\": 5, \"meinungEhepartner\""));
                var sim = NeueSimulation(daten);
                var w = sim.Wirtschaft!;
                var (a, _) = Haus(w, "Haus Klaeger");
                var sitz = w.Rat.AmtNamens("Kämmerer")!;
                var r = sitz.Ratsherr!;
                var g = GeheimnisGeben(w, r, Geheimnisart.Ehebruch, wisser: a);
                Ok(w, a, new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Anzeige));
                Pruefe.Gleich(1, w.Geheimnisse.RatsherrVerfahren.Count);
                JahrBeenden(sim);
                Pruefe.Gleich(!schuldig, sitz.Ratsherr == r, $"Sitz ({schuldig})");
                Pruefe.Gleich(schuldig, w.Geheimnisse.IstGesperrt(r));
                Pruefe.Wahr(w.Rat.Aemter.All(x => x.Ratsherr != r) == schuldig, "kein anderes Amt");
                Pruefe.Wahr(w.Justiz.LetzterGerichtstag.Any(z => z.Contains(r.Name) && z.Contains(schuldig ? "SCHULDIG" : "freigesprochen")),
                    string.Join(" | ", w.Justiz.LetzterGerichtstag));
                if (!schuldig) Pruefe.Wahr(w.Justiz.LetzterGerichtstag.Any(z => z.Contains($"{a.Inhaber} zahlt 200 ") && z.Contains("in die Stadtkasse")),
                    "Entschädigung: " + string.Join(" | ", w.Justiz.LetzterGerichtstag));
                if (!schuldig) continue;
                // Die Sperre gilt drei Jahre nach dem Gerichtstag, danach darf er wieder nachrücken
                for (var i = 2; i <= 3; i++)
                {
                    JahrBeenden(sim);
                    Pruefe.Wahr(w.Geheimnisse.IstGesperrt(r), $"im {i}. Jahr gesperrt");
                }
                JahrBeenden(sim);
                Pruefe.Wahr(!w.Geheimnisse.IstGesperrt(r), "nach drei Jahren frei");
            }
        }),

        ("Geheimnisse: Bloßstellen – öffentlich, ohne Spur; Ansehen −5 zwei Jahre, Ehepartner −40, der Inhaber −20 über den Bloßsteller, +15 Stress; " +
         "Schulden: Kreditrahmen × 0,75; Mord: Angehörige des Opfers −50", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (a, fa) = Haus(w, "Haus Zunge");
            var (b, fb) = Haus(w, "Haus Schande");
            var e = Heiraten(w, b);
            var (c, fc) = Haus(w, "Haus Opfer");
            var kind = w.Familien.KindAufnehmen(c, 20);
            var g = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Ehebruch, wisser: a);
            var ansehen = w.Rat.Ansehen(b);
            var stress = w.Familien.Stress(fb.Oberhaupt);
            var spuren = w.Justiz.SpurenVon(a).Count();
            Ok(w, a, new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Blossstellen));
            Pruefe.Gleich(11, a.Aktionspunkte.Vorrat);
            Pruefe.Wahr(g.Oeffentlich, "öffentlich");
            Pruefe.Gleich(spuren, w.Justiz.SpurenVon(a).Count());
            Pruefe.Ungefaehr(ansehen - 5, w.Rat.Ansehen(b), 1e-9);
            Pruefe.Wahr(Gruende(w, e, fb.Oberhaupt).Contains("Ehebruch -40"), Gruende(w, e, fb.Oberhaupt));
            Pruefe.Wahr(Gruende(w, fb.Oberhaupt, fa.Oberhaupt).Contains("Hat ihn bloßgestellt -20"), Gruende(w, fb.Oberhaupt, fa.Oberhaupt));
            Pruefe.Gleich(stress + 15, w.Familien.Stress(fb.Oberhaupt));
            Pruefe.Wahr(w.Geheimnisse.Sichtbar(c, fb.Oberhaupt).Contains(g), "jeder sieht es");
            // Schulden und Mord
            var s = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Schulden, wisser: a);
            Pruefe.Gleich(1.0, w.Geheimnisse.KreditFaktor(b));
            Ok(w, a, new GeheimnisNutzen(s.Nummer, Geheimnisnutzung.Blossstellen));
            Pruefe.Gleich(0.75, w.Geheimnisse.KreditFaktor(b));
            w.Geheimnisse.Mord(fb.Oberhaupt.Nummer, fc.Oberhaupt);
            var m = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Mord, wisser: a);
            Ok(w, a, new GeheimnisNutzen(m.Nummer, Geheimnisnutzung.Blossstellen));
            Pruefe.Wahr(Gruende(w, kind, fb.Oberhaupt).Contains("-50"), Gruende(w, kind, fb.Oberhaupt));
            // Zwei Jahre (dieses und das nächste), dann ist die Schande vorbei
            JahrBeenden(sim);
            Pruefe.Wahr(w.Geheimnisse.AnsehenAbzug(b) > 0, "im zweiten Jahr noch");
            JahrBeenden(sim);
            Pruefe.Gleich(0.0, w.Geheimnisse.AnsehenAbzug(b));
            Pruefe.Gleich(1.0, w.Geheimnisse.KreditFaktor(b));
        }),

        ("Geheimnisse: Stress – wer ein Geheimnis hütet, +2 im Jahr (ehrlich 5), öffentlich nichts mehr; Erpressen zählt wie „Erpressen“ (ehrlich +15), " +
         "die Anzeige wie „Anklagen“ (feige +5)", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (_, f1) = Haus(w, "Haus Last", "Ehrlich");
            var (_, f2) = Haus(w, "Haus Leicht");
            var (k3, f3) = Haus(w, "Haus Offen");
            GeheimnisGeben(w, f1.Oberhaupt, Geheimnisart.Ehebruch);
            GeheimnisGeben(w, f2.Oberhaupt, Geheimnisart.Ehebruch);
            var offen = GeheimnisGeben(w, f3.Oberhaupt, Geheimnisart.Ehebruch);
            offen.Oeffentlich = true;
            JahrBeenden(sim);
            double Hueten(Person p) => p.Stresslage.Eintraege.Where(e => e.Text == "Ein Geheimnis hüten").Sum(e => e.Wert);
            Pruefe.Gleich(5.0, Hueten(f1.Oberhaupt));
            Pruefe.Gleich(2.0, Hueten(f2.Oberhaupt));
            Pruefe.Gleich(0.0, Hueten(f3.Oberhaupt));
            _ = k3;

            var w2 = NeueSimulation().Wirtschaft!;
            var (a, fa) = Haus(w2, "Haus Redlich", "Ehrlich", "Feige");
            var (b, fb) = Haus(w2, "Haus Ziel");
            var g = GeheimnisGeben(w2, fb.Oberhaupt, Geheimnisart.Ehebruch, wisser: a);
            Pruefe.Wahr(w2.Familien.StressDurch(a, new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Gulden, 100)) >= 15, "ehrlich: Erpressen");
            Pruefe.Wahr(w2.Familien.StressDurch(a, new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Anzeige)) >= 5, "feige: Anklagen");
            Pruefe.Gleich(0.0, w2.Familien.StressDurch(a, new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Blossstellen)));
            Ok(w2, a, new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Gulden, 100));
            Pruefe.Wahr(fa.Oberhaupt.Stresslage.Eintraege.Any(e => e.Text.StartsWith("Erpresserbrief geschrieben")), string.Join("|", fa.Oberhaupt.Stresslage.Eintraege));
            _ = b;
        }),

        ("Geheimnisse: Entwicklerfenster – EntwicklerGeheimnis gibt ein Geheimnis (und das Wissen); gesperrt, beim Kind und für einen neuen Mord abgelehnt", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (a, _) = Haus(w, "Haus Tester");
            var (b, fb) = Haus(w, "Haus Probe");
            var kind = w.Familien.KindAufnehmen(b, 5);
            var befehl = new EntwicklerGeheimnis(fb.Oberhaupt.Kennung, Geheimnisart.Ehebruch);
            Nein(w, a, befehl, "gesperrt");
            w.Entwicklerhilfe.Erlaubt = true;
            Ok(w, a, befehl);
            Pruefe.Wahr(w.Geheimnisse.KenntNutzbar(a, w.Geheimnisse.Von(fb.Oberhaupt).Single()), "gewusst");
            Nein(w, a, new EntwicklerGeheimnis(kind.Kennung, Geheimnisart.Ehebruch), "keine Geheimnisse");
            Nein(w, a, new EntwicklerGeheimnis(fb.Oberhaupt.Kennung, Geheimnisart.Mord), "Opfer");
            Ok(w, a, new EntwicklerGeheimnis(w.Rat.Ratsherren[0].Kennung, Geheimnisart.Ketzerei, Wissen: false));
            Pruefe.Wahr(!w.Geheimnisse.KenntNutzbar(a, w.Geheimnisse.Von(w.Rat.Ratsherren[0]).First(g => g.Art == Geheimnisart.Ketzerei)), "ohne Wissen");
        }),

        ("Geheimnisse: Spielstand – Geheimnisse, Wissen (Beichte, Brief, Ratsstimme), erpresste Stimmen, Spitzel, Verfahren, Schande, Brief und Täter bleiben; " +
         "die Partie läuft gleich weiter", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 41 },
                Daten.Value, new NeuePartie());
            JahrBeenden(sim);
            var w = sim.Wirtschaft!;
            spieler.Aktionspunkte.Setzen(12);
            var andere = w.Konten.Where(k => k != spieler && k.IstAktiv && w.Familien.Von(k) is not null).ToList();
            var b = andere[0];
            var fb = w.Familien.Von(b)!;
            var g1 = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Ehebruch, wisser: spieler);
            var g2 = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Schulden, wisser: spieler);
            Ok(w, spieler, new GeheimnisNutzen(g1.Nummer, Geheimnisnutzung.Gulden, 300));
            Ok(w, spieler, new GeheimnisNutzen(g2.Nummer, Geheimnisnutzung.Blossstellen));
            var sitz = w.Rat.Aemter.First(x => x.Ratsherr is not null);
            var g3 = GeheimnisGeben(w, sitz.Ratsherr!, Geheimnisart.Ketzerei, wisser: spieler);
            Ok(w, spieler, new GeheimnisNutzen(g3.Nummer, Geheimnisnutzung.Ratsstimme));
            var r2 = w.Rat.Ratsherren.First(r => r != sitz.Ratsherr);
            var g4 = GeheimnisGeben(w, r2, Geheimnisart.Ehebruch, wisser: spieler);
            Ok(w, spieler, new GeheimnisNutzen(g4.Nummer, Geheimnisnutzung.Anzeige));
            Ok(w, spieler, new SpitzelAnsetzen(w.Rat.Ratsherren.Last().Kennung));
            Pruefe.Wahr(w.Gesundheit.Erkranken(b, fb.Lebende.First(p => p.Krankheit is null), "Ruhr", taeter: w.Familien.Von(spieler)!.Oberhaupt), "vergiftet");
            var zeitpunkt = new DateTime(2026, 11, 9, 12, 0, 0);
            var json = Speicherung.Speichern(sim, spieler, zeitpunkt);
            Pruefe.Wahr(GeheimnisseImSpielstand(json) && json.Contains("\"BriefIm\"") && json.Contains("\"GenutztIm\"") && TaeterDerKrankheit(json), "im Spielstand");
            var (geladen, geladenerSpieler) = Speicherung.Laden(json);
            Pruefe.Gleich(json, Speicherung.Speichern(geladen, geladenerSpieler, zeitpunkt));
            var gw = geladen.Wirtschaft!.Geheimnisse;
            Pruefe.Gleich(w.Geheimnisse.Alle.Count, gw.Alle.Count);
            Pruefe.Gleich(1, gw.ErpressteStimmen.Count);
            Pruefe.Gleich(1, gw.RatsherrVerfahren.Count);
            Pruefe.Gleich(Zustandspruefsumme.Berechnen(sim), Zustandspruefsumme.Berechnen(geladen));
            for (var i = 0; i < 2; i++) { JahrBeenden(sim); JahrBeenden(geladen); }
            Pruefe.Gleich(Speicherung.Speichern(sim, spieler, zeitpunkt), Speicherung.Speichern(geladen, geladenerSpieler, zeitpunkt));
        }),

        ("Geheimnisse: Spielstand ohne Geheimnisse (Daten ohne Abschnitt) lädt, läuft ein Jahr, speichert und lädt gleich – kein Feld", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 42 },
                Ohne.Value, new NeuePartie());
            var json = Speicherung.Speichern(sim, spieler);
            var (geladen, geladenerSpieler) = Speicherung.Laden(json);
            Pruefe.Wahr(!geladen.Wirtschaft!.Geheimnisse.IstAktiv, "keine Geheimnisse");
            JahrBeenden(geladen);
            var zeitpunkt = new DateTime(2026, 11, 9, 12, 0, 0);
            var nachJahr = Speicherung.Speichern(geladen, geladenerSpieler, zeitpunkt);
            Pruefe.Wahr(!GeheimnisseImSpielstand(nachJahr), "kein Feld ohne Geheimnisse");
            var (wieder, wiederSpieler) = Speicherung.Laden(nachJahr);
            Pruefe.Gleich(nachJahr, Speicherung.Speichern(wieder, wiederSpieler, zeitpunkt));
            // Gegenprobe: mit Abschnitt steht das Feld da
            var (mit, mitSpieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 42 },
                Daten.Value, new NeuePartie());
            Pruefe.Wahr(GeheimnisseImSpielstand(Speicherung.Speichern(mit, mitSpieler)), "mit Abschnitt ein Feld");
        }),

        ("Geheimnisse: alter Spielstand (Greybox 1406, Datenversion 55) lädt mit seinen Daten, läuft ein Jahr, speichert und lädt gleich – ohne Geheimnisse", () =>
        {
            var datei = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
                "tests", "Reichsgilde.Core.Tests", "Spielstaende", "format2-greybox-herbst1406.json.gz");
            using var gz = new GZipStream(File.OpenRead(datei), CompressionMode.Decompress);
            var (sim, spieler) = Speicherung.Laden(new StreamReader(gz).ReadToEnd());
            Pruefe.Wahr(!sim.Wirtschaft!.Geheimnisse.IstAktiv, "Daten 55 ohne Geheimnisse");
            JahrBeenden(sim);
            var zeitpunkt = new DateTime(2026, 11, 9, 12, 0, 0);
            var json = Speicherung.Speichern(sim, spieler, zeitpunkt);
            Pruefe.Wahr(!GeheimnisseImSpielstand(json), "kein Feld");
            var (wieder, wiederSpieler) = Speicherung.Laden(json);
            Pruefe.Gleich(json, Speicherung.Speichern(wieder, wiederSpieler, zeitpunkt));
        }),
    };
}
