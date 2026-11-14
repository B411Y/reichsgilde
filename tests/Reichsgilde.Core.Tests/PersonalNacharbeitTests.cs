using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Personal-Nacharbeit (Datenversion 79, <c>werkstatt.jedeWirkungRechnen</c>): Wirkungen ohne direkten Ertrag bekommen eine Gulden-Rechnung in
/// <see cref="Werkstattwesen.MehrgewinnDurch"/> – mehr Lager, Fänge, Spuren, Einbruch, Hehlerei, Razzia, Streife und Geleit. Damit wählt die KI
/// Vorarbeiter-Eigenschaften und Einrichtung von Dieb und Wache nach dem Nutzen. Ohne den Schalter bringen sie 0 wie vorher (Gegenproben).
/// </summary>
static class PersonalNacharbeitTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json.Value));

    /// <summary>Die Daten ohne den Schalter (wie bis Datenversion 78).</summary>
    static readonly Lazy<Wirtschaftsdaten> OhneSchalter = new(() =>
    {
        var knoten = JsonNode.Parse(Json.Value)!;
        Pruefe.Wahr(knoten["werkstatt"]!.AsObject().Remove("jedeWirkungRechnen"), "werkstatt.jedeWirkungRechnen fehlt in wirtschaft.json");
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    });

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: daten ?? Daten.Value);

    static (Konto K, Betrieb B) Haus(Stadtwirtschaft w, string name, Betriebsart art, int leute, string kraft)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: 10_000);
        var b = w.BetriebEroeffnen($"{w.Daten.Art(art).Name} {name}", art, k, startkarren: "");
        if (leute > 0) w.Einstellen(b, kraft, leute);
        return (k, b);
    }

    /// <summary>Der Jahresabschluss der Rechnungen, die die Gulden-Rechnung liest (Betriebe, Konten, Diebes- und Wachbuch) – ohne ein Jahr zu spielen.</summary>
    static void Abschliessen(Stadtwirtschaft w)
    {
        var jahr = w.Jahr + 1;
        foreach (var k in w.Konten) k.JahrAbschliessen(jahr);
        foreach (var b in w.Betriebe) b.JahrAbschliessen(jahr);
        w.Diebe.Jahresende();
        w.Wache.Jahresende();
    }

    /// <summary>Setzt das Diebesbuch dieses Jahres eines Hauses (Fänge) – zum Jahresende wird es das Vorjahr.</summary>
    static void Faenge(Stadtwirtschaft w, Konto k, int faenge)
    {
        var feld = typeof(Reichsgilde.Core.Gauner.Diebeswesen).GetField("_diesesJahr", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        ((Dictionary<Konto, Reichsgilde.Core.Gauner.Diebesbuch>)feld.GetValue(w.Diebe)!)[k] = new Reichsgilde.Core.Gauner.Diebesbuch(0, faenge);
    }

    static EinrichtungWirkung Stueck(Stadtwirtschaft w, string beruf, string kennung) => w.Daten.Werkstatt!.Stueck(beruf, kennung)!.Wirkung;

    static EinrichtungWirkung Eigenschaft(Stadtwirtschaft w, string kennung) =>
        w.Daten.Personal!.Laufbahnen.Select(l => l.Eigenschaft(kennung)).First(e => e is not null)!.Wirkung;

    /// <summary>Erwartete Fänge im Jahr bei dieser Chance – nachgerechnet wie im Diebeswesen (je Stunde höchstens einer).</summary>
    static double Faenge(Stadtwirtschaft w, Betrieb b, double chance)
    {
        var leute = b.Arbeiter.Sum(a => w.Daten.Arbeitskraefte[a.Arbeitskraft].TempoAm(b.Ort)) * w.VolksFaktor(b.Konto, b.Artdaten.Beruf) * b.TempoBonus;
        return b.Arbeitsminuten / 60.0 * (1 - DetMathe.Pow(1 - chance, leute));
    }

    /// <summary>Buße je Spur: Buße der Tat (Beute einer Stunde) × Spitzelchance – wie das Bestechungsrisiko der KI (ohne die Spuren des Hauses).</summary>
    static double BusseJeSpur(Stadtwirtschaft w, Betrieb b)
    {
        var leute = b.Arbeiter.Sum(a => w.Daten.Arbeitskraefte[a.Arbeitskraft].TempoAm(b.Ort)) * w.VolksFaktor(b.Konto, b.Artdaten.Beruf) * b.TempoBonus;
        var j = w.Justiz.Daten;
        return Math.Max(j.Mindestbusse, j.BusseFaktor * 60 * leute * w.Diebe.WertJeMinute(b.Stufe)) * j.SpitzelChance;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Jede Wirkung: Datenversion 79, werkstatt.jedeWirkungRechnen steht an; ohne die Angabe aus (ältere Daten)", () =>
        {
            Pruefe.Gleich(87, Daten.Value.Version);
            Pruefe.Wahr(Daten.Value.Werkstatt!.JedeWirkungRechnen, "an");
            Pruefe.Wahr(!OhneSchalter.Value.Werkstatt!.JedeWirkungRechnen, "ohne Angabe aus");
        }),

        ("Jede Wirkung: Vorsichtig und Verkleidungstruhe – weniger erwartete Fänge × (Handgeld + Buße je Spur); ohne den Schalter 0", () =>
        {
            foreach (var mitSchalter in new[] { true, false })
            {
                var w = NeueSimulation(mitSchalter ? null : OhneSchalter.Value).Wirtschaft!;
                var (_, b) = Haus(w, "Haus Elster", Betriebsart.Schmugglerloch, 4, "Taschendieb");
                var c = w.Diebe.Fangchance(b);
                Pruefe.Wahr(c > 0, $"Fangchance {c}");
                var erwartet = (Faenge(w, b, c) - Faenge(w, b, c * 0.75)) * (w.Personal.Handgeld("Taschendieb") + BusseJeSpur(w, b));
                foreach (var wirkung in new[] { Stueck(w, "Dieb", "Verkleidung"), Eigenschaft(w, "Vorsichtig") })
                {
                    var ist = w.Werkstaetten.MehrgewinnDurch(b, wirkung);
                    if (mitSchalter) Pruefe.Wahr(ist > 0 && Math.Abs(ist - erwartet) < 1e-9, $"erwartet {erwartet}, war {ist}");
                    else Pruefe.Gleich(0.0, ist, "ohne Schalter");
                }
                if (mitSchalter) Pruefe.Wahr(w.Werkstaetten.Amortisationsjahre(b, "Verkleidung") is > 1, "Die Truhe macht sich nicht im ersten Jahr bezahlt");
            }
        }),

        ("Jede Wirkung: Lederhandschuhe und Verschwiegen – nur wo jemand nach Spuren sucht (fremde Wache, fremde Diebe) oder nach eigenen Einbrüchen; nur im Versteck", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (k, b) = Haus(w, "Haus Elster", Betriebsart.Schmugglerloch, 4, "Taschendieb");
            // Allein in der Stadt: Den Beweis eines Fangs bekommt, wer ihn fasst – Handschuhe ändern daran nichts
            foreach (var wirkung in new[] { Stueck(w, "Dieb", "Handschuhe"), Eigenschaft(w, "Verschwiegen") })
                Pruefe.Gleich(0.0, w.Werkstaetten.MehrgewinnDurch(b, wirkung), "niemand sucht");
            // Eine fremde Wache macht Razzien: ein Viertel der erwarteten Buße der Fänge
            Haus(w, "Haus Wacker", Betriebsart.Wachhaus, 4, "Rekrut");
            var erwartet = Faenge(w, b, w.Diebe.Fangchance(b)) * BusseJeSpur(w, b) * 0.25;
            foreach (var wirkung in new[] { Stueck(w, "Dieb", "Handschuhe"), Eigenschaft(w, "Verschwiegen") })
            {
                var ist = w.Werkstaetten.MehrgewinnDurch(b, wirkung);
                Pruefe.Wahr(ist > 0 && Math.Abs(ist - erwartet) < 1e-9, $"erwartet {erwartet}, war {ist}");
            }
            Pruefe.Wahr(w.Werkstaetten.Amortisationsjahre(b, "Handschuhe") is < 4, "Die Handschuhe (80) machen sich in unter 4 Jahren bezahlt");
            // Die Verkleidungstruhe rechnet die Buße eines Fangs ohne die Handschuhe (der Fänger hat den Beweis)
            var truhe = w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Verkleidung");
            Pruefe.Wahr(w.Ausfuehren(k, new EinrichtungKaufen(b.Name, "Handschuhe")).Erfolg, "Handschuhe gekauft");
            Pruefe.Ungefaehr(truhe, w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Verkleidung"), 1e-9);
            Pruefe.Wahr(Math.Abs(w.Werkstaetten.MehrgewinnDurch(b, Eigenschaft(w, "Verschwiegen")) - erwartet * 0.75) < 1e-9, "Verschwiegen nach den Handschuhen");
            // ein zweiter, schlechterer Diebesbetrieb ist nicht das Versteck – Spuren dort wirken nicht
            var zweiter = w.BetriebEroeffnen("Schmugglerloch Zwei", Betriebsart.Schmugglerloch, k, startkarren: "");
            w.Einstellen(zweiter, "Taschendieb", 1);
            var versteck = w.Diebe.Versteck(k)!;
            var anderer = versteck == b ? zweiter : b;
            Pruefe.Gleich(0.0, w.Werkstaetten.MehrgewinnDurch(anderer, Eigenschaft(w, "Verschwiegen")), "nicht das Versteck");
            Pruefe.Wahr(w.Werkstaetten.MehrgewinnDurch(versteck, Eigenschaft(w, "Verschwiegen")) > 0, "das Versteck");
            // Ohne fremde Wache, aber nach eigenen Einbrüchen: die Buße für das Diebesgut (Zeugen der eigenen Leute)
            var w2 = NeueSimulation().Wirtschaft!;
            var (k2, b2) = Haus(w2, "Haus Elster", Betriebsart.Schmugglerloch, 4, "Taschendieb");
            b2.UmsatzBuchen(1_000);
            Abschliessen(w2);
            var gut = 1_000 * (1 - w2.SteuersatzMarkt(k2));
            var j = w2.Justiz.Daten;
            Pruefe.Wahr(Math.Abs(w2.Werkstaetten.MehrgewinnDurch(b2, Eigenschaft(w2, "Verschwiegen")) - Math.Max(j.Mindestbusse, j.BusseFaktor * gut) * j.SpitzelChance * 0.25) < 1e-9,
                "Verschwiegen nach eigenen Einbrüchen");
        }),

        ("Jede Wirkung: Dietriche, Einbrecher und Hehlerfreund – nach dem Diebesgut des Vorjahres (Umsatz ohne Taschendiebe); ohne Diebesgut 0", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (k, b) = Haus(w, "Haus Elster", Betriebsart.Schmugglerloch, 4, "Taschendieb");
            foreach (var kennung in new[] { "Werkzeug1" })
                Pruefe.Gleich(0.0, w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, kennung), $"{kennung} ohne Diebesgut");
            Pruefe.Gleich(0.0, w.Werkstaetten.MehrgewinnDurch(b, Eigenschaft(w, "Hehlerfreund")), "Hehlerfreund ohne Diebesgut");
            b.UmsatzBuchen(1_000); // eingebrochene Ware am Markt verkauft
            Abschliessen(w);
            var g = w.Gauner.Daten.Diebstahl.Chance + w.Diebe.Daten.Einbruch.ChanceBonus;
            var gut = 1_000 * (1 - w.SteuersatzMarkt(k)); // nach der Umsatzsteuer
            Pruefe.Wahr(gut is > 900 and < 1_000, $"Steuer {gut}");
            var dietriche = gut * (Math.Min(0.95, g + 0.1) / g - 1);
            Pruefe.Wahr(Math.Abs(w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Werkzeug1") - dietriche) < 1e-9, $"Dietriche {dietriche}");
            Pruefe.Wahr(Math.Abs(w.Werkstaetten.MehrgewinnDurch(b, Eigenschaft(w, "Einbrecher")) - dietriche) < 1e-9, "Einbrecher wie die Dietriche");
            Pruefe.Wahr(Math.Abs(w.Werkstaetten.MehrgewinnDurch(b, Eigenschaft(w, "Hehlerfreund")) - gut * 0.1) < 1e-9, "Hehlerfreund: 10 % des Diebesguts");
            // Mit den Dietrichen wirken die Meisterdietriche weniger (die Chance ist schon höher)
            w.Ausfuehren(k, new EinrichtungKaufen(b.Name, "Werkzeug1"));
            var meister = gut * (Math.Min(0.95, g + 0.2) / (g + 0.1) - 1);
            Pruefe.Wahr(Math.Abs(w.Werkstaetten.MehrgewinnDurch(b, Stueck(w, "Dieb", "Werkzeug2")) - meister) < 1e-9, $"Meisterdietriche {meister}");
        }),

        ("Jede Wirkung: Hellebarden und Spürnase – Kopfgeld aus Urteilen im Vorjahr × (Razzia-Chance mit ÷ ohne − 1); nur nach Razzien, ohne Taschendieb-Kopfgeld", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (k, b) = Haus(w, "Haus Wacker", Betriebsart.Wachhaus, 4, "Rekrut");
            var (kd, _) = Haus(w, "Haus Elster", Betriebsart.Schmugglerloch, 4, "Taschendieb");
            Pruefe.Gleich(0.0, w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Werkzeug1"), "ohne Kopfgeld");
            // Kopfgeld aus einem Urteil, aber keine Razzia: nichts (die Beweise kamen anders)
            w.Wache.KopfgeldNachUrteil(k, 1_000);
            Abschliessen(w);
            Pruefe.Gleich(0.0, w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Werkzeug1"), "ohne Razzia");
            // mit einer Razzia und zwei fremden Fängen (Kopfgeld 2 × 50, geschätzt und abgezogen)
            Pruefe.Wahr(w.Wache.Razzia(k, kd).Erfolg, "Razzia");
            w.Wache.KopfgeldNachUrteil(k, 1_000);
            Faenge(w, kd, 2);
            Abschliessen(w);
            var buch = w.Wache.Vorjahr(k)!;
            Pruefe.Wahr(buch.Razzien == 1 && buch.Kopfgeld > 2 * w.Wache.Daten.Kopfgeld.Taschendieb, $"Razzien {buch.Razzien}, Kopfgeld {buch.Kopfgeld}");
            var ausUrteilen = buch.Kopfgeld - 2 * w.Wache.Daten.Kopfgeld.Taschendieb;
            var c = w.Wache.Razziachance(k);
            var erwartet = ausUrteilen * (Math.Min(0.95, c + 0.1) / c - 1);
            Pruefe.Wahr(Math.Abs(w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Werkzeug1") - erwartet) < 1e-9, $"Hellebarden {erwartet}");
            Pruefe.Wahr(Math.Abs(w.Werkstaetten.MehrgewinnDurch(b, Eigenschaft(w, "Spuernase")) - erwartet) < 1e-9, "Spürnase wie die Hellebarden");
            // Eine Razzia und so viel Kopfgeld, wie die fremden Fänge erwarten lassen – also keins aus Urteilen: nichts
            Pruefe.Wahr(w.Wache.Razzia(k, kd).Erfolg, "zweite Razzia");
            w.Wache.KopfgeldNachUrteil(k, 4 * w.Wache.Daten.Kopfgeld.Taschendieb / w.Wache.Daten.Kopfgeld.AnteilBusse);
            Faenge(w, kd, 4);
            Abschliessen(w);
            Pruefe.Ungefaehr(4 * w.Wache.Daten.Kopfgeld.Taschendieb, w.Wache.Vorjahr(k)!.Kopfgeld, 1e-9);
            Pruefe.Gleich(0.0, w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Werkzeug1"), "nur so viel wie das Taschendieb-Kopfgeld");
        }),

        ("Jede Wirkung: Wachhunde und Streifengänger – eigener Schaden in der Stadt × mehr Schutz ÷ (1 − Schutz) und Kopfgeld für fremde Taschendiebe", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (k, b) = Haus(w, "Haus Wacker", Betriebsart.Wachhaus, 4, "Rekrut");
            Pruefe.Gleich(0.0, w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Hunde"), "ohne Schaden und ohne Fänge");
            var giesserei = w.BetriebEroeffnen("Gießerei Wacker", Betriebsart.Giesserei, k, startkarren: "");
            giesserei.RaubBuchen(1_000);
            Abschliessen(w);
            var vorher = w.Wache.Patrouilleschutz;
            var mehr = w.Wache.PatrouilleschutzMit(b, 1.25) - vorher;
            Pruefe.Wahr(vorher > 0 && mehr > 0, $"Schutz {vorher} + {mehr}");
            var erwartet = 1_000 * mehr / (1 - vorher);
            Pruefe.Wahr(Math.Abs(w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Hunde") - erwartet) < 1e-9, $"Wachhunde {erwartet}");
            var streife = 1_000 * (w.Wache.PatrouilleschutzMit(b, 1.15) - vorher) / (1 - vorher);
            Pruefe.Wahr(Math.Abs(w.Werkstaetten.MehrgewinnDurch(b, Eigenschaft(w, "Streifengaenger")) - streife) < 1e-9, "Streifengänger");
            // Fänge eines fremden Diebshauses im Vorjahr: dazu das Kopfgeld, das die Streife mehr bringt
            var (kd, _) = Haus(w, "Haus Elster", Betriebsart.Schmugglerloch, 4, "Taschendieb");
            giesserei.RaubBuchen(1_000);
            Faenge(w, kd, 3);
            Abschliessen(w);
            var fang = w.Wache.Daten.Patrouille.TaschendiebFang;
            var kopfgeld = 3 * w.Wache.Daten.Kopfgeld.Taschendieb * fang * mehr / (1 + fang * vorher);
            Pruefe.Wahr(kopfgeld > 0 && Math.Abs(w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Hunde") - erwartet - kopfgeld) < 1e-9, $"Wachhunde mit Kopfgeld {kopfgeld}");
            // Am Deckel bringt mehr Streife nichts mehr
            w.Einstellen(w.BetriebEroeffnen("Garnison Groß", Betriebsart.Wachhaus, w.KontoEroeffnen("Haus Groß", startvermoegen: 1_000), startkarren: ""), "Rekrut", 4);
            for (var i = 0; i < 20 && w.Wache.Patrouilleschutz < w.Wache.Daten.Patrouille.Hoechstens; i++)
                w.Einstellen(w.BetriebEroeffnen($"Wachhaus {i}", Betriebsart.Wachhaus, w.KontoEroeffnen($"Haus {i}", startvermoegen: 1_000), startkarren: ""), "Rekrut", 4);
            Pruefe.Gleich(w.Wache.Daten.Patrouille.Hoechstens, w.Wache.Patrouilleschutz, "am Deckel");
            Pruefe.Gleich(0.0, w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Hunde"), "am Deckel");
        }),

        ("Jede Wirkung: Kettenhemden und Geleitmann – nur die Geleitstube, nach dem Raub an den eigenen Karren mit dem Geleit der Wache", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (k, b) = Haus(w, "Haus Wacker", Betriebsart.Wachhaus, 4, "Rekrut");
            var giesserei = w.BetriebEroeffnen("Gießerei Wacker", Betriebsart.Giesserei, k, startkarren: "Handkarren");
            Pruefe.Wahr(w.Wache.Geleitstube == b, "Das Wachhaus stellt das Geleit");
            k.Raub(800); // ein überfallener Karren
            Abschliessen(w);
            Pruefe.Gleich(0.0, w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Kettenhemd"), "Der Karren fährt ohne Geleit der Wache");
            giesserei.Karren[0].Eskorte = w.Wache.Daten.Geleit.Eskorte;
            Pruefe.Wahr(Math.Abs(w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Kettenhemd") - 800 * 0.2) < 1e-9, "Kettenhemden: 20 % von 800");
            Pruefe.Wahr(Math.Abs(w.Werkstaetten.MehrgewinnDurch(b, Eigenschaft(w, "Geleitmann")) - 800 * 0.15) < 1e-9, "Geleitmann: 15 % von 800");
            giesserei.RaubBuchen(800); // Raub aus dem Lager zählt nicht zum Geleit
            Abschliessen(w);
            Pruefe.Gleich(0.0, w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Kettenhemd"), "nur Lager bestohlen");
        }),

        ("Jede Wirkung: Mehr Lager hilft nur, wenn ein Karren dann mehr lädt – Gießerei mit Pferdekarren und einer Ware ja, mit Handkarren nein; ohne Stillstand nie", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (_, b) = Haus(w, "Haus Probe", Betriebsart.Giesserei, 2, "Geselle");
            b.Produkt = "Beschlag"; // eine Zutat (Eisen): Der Pferdekarren (75) lädt höchstens das Lager (40)
            b.KarrenHinzufuegen("Pferdekarren");
            var regale = Stueck(w, "Schmied", "Regale");
            Pruefe.Gleich(0.0, w.Werkstaetten.MehrgewinnDurch(b, regale), "ohne Stillstand");
            typeof(Betrieb).GetProperty(nameof(Betrieb.StillstandMinutenVorjahr))!.SetValue(b, b.Arbeitsminuten / 4); // ein Viertel des Jahres
            var jetzt = w.Werkstaetten.Durchfluss(b, "Pferdekarren", b.LagerKapazitaetJeWare);
            var nachher = w.Werkstaetten.Durchfluss(b, "Pferdekarren", b.LagerKapazitaetJeWare + 20);
            var waren = Math.Max(b.WarenInArbeit.Count, b.Zutaten.Count);
            Pruefe.Wahr(waren == 1 && nachher > jetzt, $"Ladung {b.LagerKapazitaetJeWare} × {waren} Waren gegen 75: {jetzt} → {nachher}");
            var lager = w.Werkstaetten.MehrgewinnDurch(b, regale);
            Pruefe.Wahr(lager > 0 && Math.Abs(lager - w.Werkstaetten.LagerMehrgewinn(b, 20)) < 1e-9, $"Lager {lager}");
            Pruefe.Gleich(lager, w.Werkstaetten.MehrgewinnDurch(b, Eigenschaft(w, "Ordnungsliebend")), "Ordnungsliebend wie die Regale");
            // Ein Handkarren fasst 15 – mehr Lager ändert seine Ladung nicht
            var (_, h) = Haus(w, "Haus Hand", Betriebsart.Giesserei, 2, "Geselle");
            h.KarrenHinzufuegen("Handkarren");
            typeof(Betrieb).GetProperty(nameof(Betrieb.StillstandMinutenVorjahr))!.SetValue(h, h.Arbeitsminuten / 4);
            Pruefe.Gleich(0.0, w.Werkstaetten.MehrgewinnDurch(h, regale), "Handkarren");
        }),

        ("Jede Wirkung: Ohne den Schalter bringen Lager, Spuren, Einbruch, Hehlerei, Razzia, Streife und Geleit nichts – auch mit Vorjahr und Stillstand", () =>
        {
            var w = NeueSimulation(OhneSchalter.Value).Wirtschaft!;
            var (kd, d) = Haus(w, "Haus Elster", Betriebsart.Schmugglerloch, 4, "Taschendieb");
            var (kw, wb) = Haus(w, "Haus Wacker", Betriebsart.Wachhaus, 4, "Rekrut");
            var giesserei = w.BetriebEroeffnen("Gießerei Wacker", Betriebsart.Giesserei, kw, startkarren: "Pferdekarren");
            giesserei.Karren[0].Eskorte = w.Wache.Daten.Geleit.Eskorte;
            d.UmsatzBuchen(1_000);
            giesserei.RaubBuchen(500);
            kw.Raub(800);
            w.Wache.KopfgeldNachUrteil(kw, 1_000);
            Abschliessen(w);
            typeof(Betrieb).GetProperty(nameof(Betrieb.StillstandMinutenVorjahr))!.SetValue(giesserei, giesserei.Arbeitsminuten / 4);
            foreach (var kennung in new[] { "Werkzeug1", "Werkzeug2", "Verkleidung", "Regale", "Handschuhe" })
                Pruefe.Gleich(0.0, w.Werkstaetten.MehrgewinnDurch(d, Stueck(w, "Dieb", kennung)), $"Dieb {kennung}");
            foreach (var kennung in new[] { "Hehlerfreund", "Einbrecher", "Verschwiegen", "Vorsichtig", "Versteckkundig" })
                Pruefe.Gleich(0.0, w.Werkstaetten.MehrgewinnDurch(d, Eigenschaft(w, kennung)), $"Dieb {kennung}");
            foreach (var kennung in new[] { "Werkzeug1", "Hunde", "Kettenhemd" })
                Pruefe.Gleich(0.0, w.Werkstaetten.MehrgewinnDurch(wb, Stueck(w, "Gardist", kennung)), $"Gardist {kennung}");
            foreach (var kennung in new[] { "Spuernase", "Streifengaenger", "Geleitmann" })
                Pruefe.Gleich(0.0, w.Werkstaetten.MehrgewinnDurch(wb, Eigenschaft(w, kennung)), $"Wache {kennung}");
            Pruefe.Gleich(0.0, w.Werkstaetten.MehrgewinnDurch(giesserei, Stueck(w, "Schmied", "Regale")), "Regale");
            Pruefe.Gleich(0.0, w.Werkstaetten.LagerMehrgewinn(giesserei, 20), "Lager");
            // Die Eisentür rechnete schon immer nach dem Raub des Vorjahres
            Pruefe.Wahr(w.Werkstaetten.ErwarteterMehrgewinnJeJahr(giesserei, "Eisentuer") > 0, "Eisentür wie bisher");
        }),
    };
}
