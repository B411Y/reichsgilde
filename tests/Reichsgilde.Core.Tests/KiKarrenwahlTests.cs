using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Karrenwahl der KI nach Durchfluss (Befund H1 der Prüfung von phase5/gehtempo): Seit die Fahrzeit nach Weglänge und Karrenart rechnet
/// (Ochse 0,8, Handkarren 1,0, Pferd 1,3 m/s, Datenversion 65), wählt die KI die Karrenart nach dem Durchfluss auf dem echten Weg und darf an
/// der Grenze von <see cref="KiProfil.MaxKarrenJeBetrieb"/> Karren einen langsamen gegen einen schnelleren tauschen.
///   • Durchfluss eines Karrens = Ladung ÷ (Fahrzeit hin + zurück, wie der Kern sie rechnet – mit Belag und Höchstdauer); Ladung höchstens,
///     was das Lager fasst (Lager je Ware × Zahl der Waren).
///   • Stillstand nachher = Stillstand vorher ^ (Durchfluss nachher ÷ Durchfluss vorher).
///   • Gewählt wird der Plan mit dem größten Mehrgewinn im Jahr abzüglich Kosten ÷ Amortisationsjahre, der sich in dieser Frist bezahlt macht.
/// Alles auf der Kölner Karte, Schwierigkeit „Schwer“ (die KI-Häuser beginnen mit Werkstatt und Umlandbetrieb, wie die Karrenmessung).
/// </summary>
static class KiKarrenwahlTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    /// <summary>Die echten Daten mit geändertem Inhalt.</summary>
    static Wirtschaftsdaten DatenMit(Action<JsonObject> aendern)
    {
        var knoten = JsonNode.Parse(File.ReadAllText(Datenpfad.Wirtschaft()))!.AsObject();
        aendern(knoten);
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    }

    /// <summary>Köln mit allen KI-Häusern der Karte, „Schwer“.</summary>
    static (Simulation Sim, Stadtwirtschaft W) Koeln(Wirtschaftsdaten? daten = null)
    {
        var pfad = Datenpfad.Karte("koeln");
        var haeuser = JsonNode.Parse(File.ReadAllText(pfad))!["start"]!["haeuser"]!.AsArray().Count;
        var (sim, _) = Partieaufbau.Erstellen(
            Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = Schwierigkeit.Schwer },
            daten ?? Daten.Value, new NeuePartie(Rivalen: haeuser), karte: Stadtkarte.AusDatei(pfad));
        return (sim, sim.Wirtschaft!);
    }

    /// <summary>Ein Betrieb eines KI-Hauses und dessen KI – mit Geld genug für jeden Karren.</summary>
    static (KiRivale Ki, Betrieb B) Von(Stadtwirtschaft w, string name)
    {
        var b = w.Betriebe.Single(x => x.Name == name);
        b.Konto.Entwicklerhilfe(20_000);
        return (w.Rivalen.Single(r => r.Konto == b.Konto), b);
    }

    static double Fluss(Stadtwirtschaft w, Betrieb b, string typ)
    {
        var d = w.Daten.Logistik.Karren[typ];
        var waren = Math.Max(1, b.IstWerkstatt ? Math.Max(b.WarenInArbeit.Count, b.Zutaten.Count) : b.Rohstoffe.Count);
        return Math.Min(d.Gesamtkapazitaet, b.LagerKapazitaetJeWare * waren) / (double)(w.Fahrzeit(b, typ, true) + w.Fahrzeit(b, typ, false));
    }

    /// <summary>Wie <c>Pruefe.Ungefaehr</c>, mit Hinweis.</summary>
    static void Nahe(double erwartet, double ist, double toleranz, string hinweis) =>
        Pruefe.Wahr(Math.Abs(erwartet - ist) <= toleranz, $"{hinweis}: erwartet {erwartet}, ist {ist}");

    const string Holzschlag = "Holzschlag Hardevust"; // eine Ware (Holz), 176 m bis zum Markt
    const string Bauernhof = "Bauernhof Mommersloch"; // zwei Waren (Gerste, Roggen), 193 m

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Karrenwahl: Durchfluss = Ladung (höchstens das Lager) ÷ (hin + zurück) nach der Fahrzeit des Kerns – im Kölner Umland das Pferd vorn, der Handkarren hinten", () =>
        {
            var (_, w) = Koeln();
            foreach (var name in new[] { Holzschlag, Bauernhof })
            {
                var (ki, b) = Von(w, name);
                Pruefe.Wahr(ki.KarrenwahlNachDurchfluss(b), $"{name}: Fahrzeit nach Weglänge");
                foreach (var typ in w.Daten.Logistik.Karren.Keys)
                    Nahe(Fluss(w, b, typ), ki.Durchfluss(b, typ), 1e-12, $"{name} {typ}");
                Pruefe.Wahr(ki.Durchfluss(b, "Kriegskarren") > ki.Durchfluss(b, "Ochsenkarren") && ki.Durchfluss(b, "Ochsenkarren") > ki.Durchfluss(b, "Handkarren"),
                    $"{name}: Pferd > Ochse > Hand");
            }
            // Holz ist eine Ware: Das Lager fasst 40 – mehr lädt auch der Kriegskarren (75) nicht. Zwei Waren: 80, der Kriegskarren fährt voll.
            var (k1, holz) = Von(w, Holzschlag);
            var (k2, hof) = Von(w, Bauernhof);
            Pruefe.Ungefaehr(40.0 / (w.Fahrzeit(holz, "Kriegskarren", true) + w.Fahrzeit(holz, "Kriegskarren", false)), k1.Durchfluss(holz, "Kriegskarren"), 1e-12);
            Pruefe.Ungefaehr(75.0 / (w.Fahrzeit(hof, "Kriegskarren", true) + w.Fahrzeit(hof, "Kriegskarren", false)), k2.Durchfluss(hof, "Kriegskarren"), 1e-12);
        }),

        ("KI-Karrenwahl: Stillstand nachher = Stillstand vorher ^ (Durchfluss nachher ÷ vorher); Mehrgewinn = gesparter Anteil × Wert eines vollen Arbeitsjahres", () =>
        {
            var (_, w) = Koeln();
            var (ki, b) = Von(w, Holzschlag);
            var vorher = ki.Durchfluss(b, "Ochsenkarren");
            Pruefe.Gleich(1, b.Karren.Count);
            var jahreswert = Kalkulation.Leistung(w, b) * b.Arbeitsminuten * Kalkulation.WertJeArbeitsminute(w, b);
            Pruefe.Wahr(jahreswert > 0, $"Wert eines Arbeitsjahres {jahreswert:0}");
            var plaene = ki.Karrenplaene(b, 0.5).ToList();
            Pruefe.Gleich(3, plaene.Count); // unter der Grenze: je Art ein Kauf, kein Tausch
            foreach (var p in plaene)
            {
                Pruefe.Gleich(null, p.Tausch);
                var nachher = Math.Pow(0.5, (vorher + ki.Durchfluss(b, p.Typ)) / vorher);
                Nahe(nachher, p.StillstandNachher, 1e-12, p.Typ);
                Nahe((0.5 - nachher) * jahreswert, p.Mehrgewinn, 1e-6, p.Typ);
                Nahe(w.Daten.Logistik.Karren[p.Typ].Preis, p.Kosten, 1e-9, p.Typ);
            }
        }),

        ("KI-Karrenwahl: Ein Kölner Holzschlag mit einem Ochsen, der die Hälfte der Arbeitszeit stillstand, bekommt einen Kriegskarren statt eines zweiten Ochsen", () =>
        {
            var (_, w) = Koeln();
            var (ki, b) = Von(w, Holzschlag);
            var plan = ki.BesterKarrenplan(b, 0.5);
            Pruefe.Wahr(plan is not null, "ein Plan");
            Pruefe.Gleich("Kriegskarren", plan!.Value.Typ);
            Pruefe.Gleich(null, plan.Value.Tausch);
            // bester Wert: Mehrgewinn − Kosten ÷ Amortisationsjahre, vor dem Ochsen und dem Handkarren
            double Wert(KiRivale.Karrenplan p) => p.Mehrgewinn - p.Kosten / ki.Amortisationsjahre;
            foreach (var p in ki.Karrenplaene(b, 0.5).Where(p => p.Typ != "Kriegskarren"))
                Pruefe.Wahr(Wert(p) < Wert(plan.Value), $"{p.Typ}: {Wert(p):0} gegen {Wert(plan.Value):0}");
        }),

        ("KI-Karrenwahl: An der Grenze von drei Karren tauscht die KI einen Ochsen des Bauernhofs gegen einen Kriegskarren (Preis − Erlös des alten)", () =>
        {
            var (_, w) = Koeln();
            var (ki, b) = Von(w, Bauernhof);
            b.KarrenHinzufuegen("Ochsenkarren");
            b.KarrenHinzufuegen("Ochsenkarren");
            Pruefe.Gleich(KiProfil.Standard.MaxKarrenJeBetrieb, b.Karren.Count);
            var plaene = ki.Karrenplaene(b, 0.25).ToList();
            Pruefe.Wahr(plaene.Count > 0 && plaene.All(p => p.Tausch is not null), "an der Grenze nur Tausch");
            Pruefe.Wahr(plaene.All(p => ki.Durchfluss(b, p.Typ) > ki.Durchfluss(b, b.Karren[p.Tausch!.Value].Typ)), "nur gegen einen schnelleren");
            var plan = ki.BesterKarrenplan(b, 0.25)!.Value;
            Pruefe.Gleich("Kriegskarren", plan.Typ);
            Pruefe.Gleich("Ochsenkarren", b.Karren[plan.Tausch!.Value].Typ);
            Pruefe.Ungefaehr(900 - 400 * w.Daten.Logistik.KarrenVerkaufAnteil, plan.Kosten, 1e-9);
            Pruefe.Wahr(plan.Kosten <= plan.Mehrgewinn * ki.Amortisationsjahre, "macht sich in der Frist bezahlt");
            // ausgeführt: weiter drei Karren, einer davon der neue Kriegskarren
            ki.KarrenNachDurchfluss(b, 0.25);
            Pruefe.Gleich(3, b.Karren.Count);
            Pruefe.Gleich(1, b.Karren.Count(k => k.Typ == "Kriegskarren"));
            Pruefe.Wahr(ki.Entscheidungen.Any(e => e.Contains("tauscht") && e.Contains("der Arbeitszeit still")), string.Join(" | ", ki.Entscheidungen));
        }),

        ("KI-Karrenwahl: Ein Tausch, der sich erst nach der Frist bezahlt macht, unterbleibt – die KI behält ihre drei Karren", () =>
        {
            var (_, w) = Koeln();
            var (ki, b) = Von(w, Holzschlag);
            b.KarrenHinzufuegen("Kriegskarren");
            b.KarrenHinzufuegen("Kriegskarren");
            // ein Ochse und zwei Pferde: Der Ochse gegen ein Pferd spart bei 21 % Stillstand ≈ 127 Gulden im Jahr (700 Gulden, 5,5 Jahre – noch in
            // der Frist von 6), bei 5 % kaum etwas
            foreach (var stillstand in new[] { 0.21, 0.05 })
            {
                var plaene = ki.Karrenplaene(b, stillstand).ToList();
                Pruefe.Gleich(1, plaene.Count); // nur der Ochse ist langsamer als ein Pferd
                var p = plaene[0];
                Pruefe.Gleich(("Kriegskarren", "Ochsenkarren"), (p.Typ, b.Karren[p.Tausch!.Value].Typ));
                var lohnt = p.Kosten <= p.Mehrgewinn * ki.Amortisationsjahre;
                Pruefe.Gleich(lohnt ? p : null, ki.BesterKarrenplan(b, stillstand), $"{stillstand}: {p.Kosten:0} Gulden für {p.Mehrgewinn:0} im Jahr");
                Pruefe.Gleich(stillstand > 0.1, lohnt, $"{stillstand}: lohnt");
            }
            ki.KarrenNachDurchfluss(b, 0.05);
            Pruefe.Gleich("Ochsenkarren, Kriegskarren, Kriegskarren", string.Join(", ", b.Karren.Select(k => k.Typ)));
        }),

        ("KI-Karrenwahl: An der Höchstdauer fährt das Pferd nicht schneller als der Ochse – für eine Ware nimmt die KI dann den billigeren Ochsen", () =>
        {
            var daten = DatenMit(d => d["logistik"]!["weglaenge"]!["hoechstMinuten"] = 100);
            var (_, w) = Koeln(daten);
            var (ki, b) = Von(w, Holzschlag);
            foreach (var typ in w.Daten.Logistik.Karren.Keys)
                Pruefe.Gleich((100, 100), (w.Fahrzeit(b, typ, true), w.Fahrzeit(b, typ, false)), typ);
            Pruefe.Ungefaehr(ki.Durchfluss(b, "Ochsenkarren"), ki.Durchfluss(b, "Kriegskarren"), 1e-12); // beide 40 Stück (Lager) in 200 Minuten
            Pruefe.Gleich("Ochsenkarren", ki.BesterKarrenplan(b, 0.5)!.Value.Typ);
        }),

        ("KI-Karrenwahl: Reicht das Geld nach der Reserve nur für einen kleineren Karren, nimmt die KI den besten bezahlbaren – sonst keinen", () =>
        {
            var (_, w) = Koeln();
            var (ki, b) = Von(w, Holzschlag);
            var k = b.Konto;
            k.Investition(k.Vermoegen - ki.Profil.Reserve - 500); // 500 über der Reserve: Ochse ja, Pferd nein
            Pruefe.Gleich("Ochsenkarren", ki.BesterKarrenplan(b, 0.5)!.Value.Typ);
            k.Investition(400); // 100 über der Reserve
            Pruefe.Gleich(null, ki.BesterKarrenplan(b, 0.5));
        }),

        ("KI-Karrenwahl: Ohne Fahrzeit nach Weglänge (alte Daten) fahren alle Arten gleich lang – dann gilt die alte Regel", () =>
        {
            var daten = DatenMit(d => d["logistik"]!.AsObject().Remove("weglaenge"));
            var (_, w) = Koeln(daten);
            var (ki, b) = Von(w, Holzschlag);
            Pruefe.Wahr(!ki.KarrenwahlNachDurchfluss(b), "alte Daten");
            Pruefe.Gleich(w.Fahrzeit(b, "Handkarren", true), w.Fahrzeit(b, "Kriegskarren", true));
        }),

        ("KI-Karrenwahl: Köln, ein Jahr „Schwer“ – jeder KI-Betrieb, der zu oft stillstand und kaufte, kaufte nach Plan; nie mehr als drei Karren", () =>
        {
            var (sim, w) = Koeln();
            var vorher = w.Betriebe.ToDictionary(b => b.Name, b => b.Karren.Select(k => k.Typ).ToList());
            var jahr = sim.Zeit.Jahr;
            while (sim.Zeit.Jahr == jahr) sim.Tick();
            var gekauft = 0;
            foreach (var b in w.Betriebe.Where(b => w.Rivalen.Any(r => r.Konto == b.Konto) && vorher.ContainsKey(b.Name)))
            {
                Pruefe.Wahr(b.Karren.Count <= KiProfil.Standard.MaxKarrenJeBetrieb, b.Name);
                var neu = b.Karren.Select(k => k.Typ).ToList();
                if (neu.SequenceEqual(vorher[b.Name])) continue;
                gekauft++;
                Pruefe.Wahr(b.StillstandMinutenVorjahr >= KiProfil.Standard.StillstandFuerKarrenkauf * b.Arbeitsminuten, $"{b.Name}: nur nach Stillstand");
            }
            Pruefe.Wahr(gekauft > 0, "im ersten Jahr stehen die Kölner Umlandbetriebe mit einem Ochsen oft still");
            var holz = w.Betriebe.Single(b => b.Name == Holzschlag);
            Pruefe.Gleich(2, holz.Karren.Count);
            Pruefe.Gleich("Kriegskarren", holz.Karren[1].Typ);
        }),
    };
}
