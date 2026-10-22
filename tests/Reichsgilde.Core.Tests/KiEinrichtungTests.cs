using System.Globalization;
using Reichsgilde.Core;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests: Die KI-Häuser kaufen zum Jahresende Einrichtung, die sich in höchstens 4 Jahren bezahlt macht
/// (Entscheidung des Nutzers, Balancing 1, PR #177) – je Betrieb höchstens ein Stück, die doppelte Reserve bleibt.
/// </summary>
static class KiEinrichtungTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json.Value));

    const string KiZeile = "\"einrichtung\": { \"hoechstensJahre\": 4, \"charakterJahre\": 0.5, \"reserveFaktor\": 2 }";

    /// <summary>Die Daten mit einer anderen Grenze in Jahren (oder ohne Abschnitt „ki", wenn null).</summary>
    static Wirtschaftsdaten DatenMit(double? hoechstensJahre)
    {
        Pruefe.Wahr(Json.Value.Contains(KiZeile), "Der Abschnitt „ki“ steht nicht wie erwartet in wirtschaft.json");
        var neu = hoechstensJahre is { } j
            ? KiZeile.Replace("\"hoechstensJahre\": 4", $"\"hoechstensJahre\": {j.ToString(CultureInfo.InvariantCulture)}")
            : "\"aus\": {}";
        return Wirtschaftsdaten.AusJson(Json.Value.Replace(KiZeile, neu));
    }

    /// <summary>Ein KI-Haus mit einer Gießerei, zwei Gesellen an Rapieren, ohne Karren.</summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto K, Betrieb B, KiRivale Ki) Giesserei(
        double vermoegen = 10_000, Wirtschaftsdaten? daten = null, KiProfil? profil = null)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: daten ?? Daten.Value);
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Probe", startvermoegen: vermoegen);
        var b = w.BetriebEroeffnen("Gießerei Probe", Betriebsart.Giesserei, k, startkarren: "");
        b.Produkt = "Rapier";
        w.Einstellen(b, "Geselle", 2);
        return (sim, w, k, b, w.KiAnmelden(k, profil));
    }

    static int Gekauft(Betrieb b) => b.Einrichtungsstuecke.Count();

    /// <summary>Das Stück mit der kürzesten Amortisation, das der Betrieb jetzt kaufen darf.</summary>
    static (string Kennung, double Jahre) Kuerzestes(Stadtwirtschaft w, Betrieb b) =>
        w.Werkstaetten.Einrichtungsliste(b)
            .Where(e => !e.Gekauft && e.Verboten is null && w.Werkstaetten.Amortisationsjahre(b, e.Kennung) is not null)
            .Select(e => (e.Kennung, Jahre: w.Werkstaetten.Amortisationsjahre(b, e.Kennung)!.Value))
            .OrderBy(x => x.Jahre)
            .First();

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Einrichtung: Zum Jahresende kauft die KI das Stück mit der kürzesten Amortisation (Gutes Werkzeug) – eines je Betrieb", () =>
        {
            var (_, w, k, b, ki) = Giesserei();
            var (kennung, jahre) = Kuerzestes(w, b);
            Pruefe.Gleich("Werkzeug1", kennung, "Das Werkzeug hilft bei jeder Ware und ist das billigste mit Zeitgewinn");
            Pruefe.Wahr(jahre <= 4, $"Gutes Werkzeug macht sich in {jahre:0.0} Jahren bezahlt");
            var vorher = k.Vermoegen;
            ki.Jahresende();
            Pruefe.Wahr(b.HatEinrichtung("Werkzeug1"), "Die KI hat kein Werkzeug gekauft");
            Pruefe.Gleich(1, Gekauft(b), "höchstens ein Stück je Betrieb und Jahr");
            Pruefe.Ungefaehr(vorher - 300, k.Vermoegen, 1e-6);
            Pruefe.Wahr(ki.Entscheidungen[0].Contains("kauft Einrichtung für Gießerei Probe: Gutes Werkzeug (300 Gulden)"), ki.Entscheidungen[0]);
        }),

        ("KI-Einrichtung: Über der Grenze kauft sie nichts – knapp darunter schon; ohne Abschnitt „ki“ nie", () =>
        {
            var (_, w0, _, b0, _) = Giesserei();
            var (kennung, jahre) = Kuerzestes(w0, b0);

            var (_, _, _, b1, ki1) = Giesserei(daten: DatenMit(jahre * 0.99));
            ki1.Jahresende();
            Pruefe.Gleich(0, Gekauft(b1), $"Grenze knapp unter {jahre:0.00} Jahren ({kennung})");

            var (_, _, _, b2, ki2) = Giesserei(daten: DatenMit(jahre * 1.01));
            ki2.Jahresende();
            Pruefe.Wahr(b2.HatEinrichtung(kennung), $"Grenze knapp über {jahre:0.00} Jahren: {kennung} gekauft");

            var (_, _, _, b3, ki3) = Giesserei(daten: DatenMit(null));
            Pruefe.Gleich<double?>(null, ki3.EinrichtungHoechstensJahre);
            ki3.Jahresende();
            Pruefe.Gleich(0, Gekauft(b3), "ohne Abschnitt „ki“");
        }),

        ("KI-Einrichtung: Die Grenze nach Charakter – vorsichtig 3,5, ausgewogen 4, wagemutig 4,5 Jahre", () =>
        {
            Pruefe.Gleich<double?>(3.5, Giesserei(profil: Partieaufbau.ProfilFuer(KiCharakter.Vorsichtig)).Ki.EinrichtungHoechstensJahre);
            Pruefe.Gleich<double?>(4.0, Giesserei(profil: Partieaufbau.ProfilFuer(KiCharakter.Ausgewogen)).Ki.EinrichtungHoechstensJahre);
            Pruefe.Gleich<double?>(4.5, Giesserei(profil: Partieaufbau.ProfilFuer(KiCharakter.Wagemutig)).Ki.EinrichtungHoechstensJahre);
        }),

        ("KI-Einrichtung: Nach dem Kauf bleibt die doppelte Reserve – sonst wartet die KI", () =>
        {
            var reserve = 2 * KiProfil.Standard.Reserve;
            var (_, _, k1, b1, ki1) = Giesserei(vermoegen: reserve + 299);
            ki1.Jahresende();
            Pruefe.Gleich(0, Gekauft(b1), "Nach dem Werkzeug blieben nur 2.999 Gulden");
            Pruefe.Wahr(k1.Vermoegen >= reserve, "nichts ausgegeben");

            var (_, _, _, b2, ki2) = Giesserei(vermoegen: reserve + 300);
            ki2.Jahresende();
            Pruefe.Wahr(b2.HatEinrichtung("Werkzeug1"), "Mit genau der doppelten Reserve danach kauft sie");
        }),

        ("KI-Einrichtung: Kein Stück doppelt – im nächsten Jahr das nächstbeste; je Betrieb eines, auch bei zwei Betrieben", () =>
        {
            var (_, w, k, b, ki) = Giesserei();
            var zweite = w.BetriebEroeffnen("Gießerei Zwei", Betriebsart.Giesserei, k, startkarren: "");
            zweite.Produkt = "Rapier";
            w.Einstellen(zweite, "Geselle", 2);
            ki.Jahresende();
            Pruefe.Gleich(1, Gekauft(b));
            Pruefe.Gleich(1, Gekauft(zweite));
            var naechstes = Kuerzestes(w, b).Kennung;
            ki.Jahresende();
            Pruefe.Gleich(2, Gekauft(b));
            Pruefe.Wahr(b.HatEinrichtung("Werkzeug1") && b.HatEinrichtung(naechstes), $"Werkzeug, dann {naechstes}");
            var stuecke = Gekauft(b);
            for (var i = 0; i < 6; i++) ki.Jahresende();
            Pruefe.Gleich(b.Einrichtungsstuecke.Count(), b.Einrichtungsstuecke.Select(e => e.Kennung).Distinct().Count(), "kein Stück doppelt");
            Pruefe.Wahr(Gekauft(b) >= stuecke, "nichts verloren");
            Pruefe.Wahr(ki.Entscheidungen.All(e => !e.Contains("wollte, durfte aber nicht")), "kein abgelehnter Befehl");
        }),

        ("KI-Einrichtung: Nichts Verbotenes – Meisterwerkzeug erst ab Stufe 2, kein Stück ohne Gulden-Wirkung, keine Eisentür ohne Raub", () =>
        {
            // Mit 20 Jahren als Grenze würde sich das Meisterwerkzeug lohnen – es ist aber erst ab Stufe 2 zu haben.
            var (_, w, k, b, ki) = Giesserei(daten: DatenMit(20));
            Pruefe.Wahr(w.Werkstaetten.Amortisationsjahre(b, "Werkzeug2") is <= 20, "Das Meisterwerkzeug würde sich lohnen");
            for (var i = 0; i < 8; i++) ki.Jahresende();
            Pruefe.Wahr(!b.HatEinrichtung("Werkzeug2"), "Meisterwerkzeug in Stufe 1");
            Pruefe.Wahr(w.Werkstaetten.EinrichtungVerboten(b, "Werkzeug2") is not null, "Meisterwerkzeug bleibt verboten");
            Pruefe.Wahr(!b.HatEinrichtung("Regale"), "Regale bringen keine Gulden");
            Pruefe.Wahr(!b.HatEinrichtung("Eisentuer") && !b.HatEinrichtung("Loeschfass"), "nie bestohlen, nie gebrannt");
            Pruefe.Wahr(ki.Entscheidungen.All(e => !e.Contains("wollte, durfte aber nicht")), "kein abgelehnter Befehl");
            Pruefe.Wahr(k.Vermoegen >= 2 * KiProfil.Standard.Reserve, "Reserve gehalten");
        }),

        ("KI-Einrichtung: Wer bestohlen wurde, kauft die Eisentür, wenn sie sich mit dem Raub des Vorjahres bezahlt macht", () =>
        {
            var (sim, w, k, b, ki) = Giesserei(vermoegen: 50_000);
            // Werkzeug und Blasebalg stehen schon – sie hätten die kürzere Amortisation.
            Pruefe.Wahr(w.Ausfuehren(k, new EinrichtungKaufen(b.Name, "Werkzeug1")).Erfolg, "Werkzeug kaufen");
            Pruefe.Wahr(w.Ausfuehren(k, new EinrichtungKaufen(b.Name, "Blasebalg")).Erfolg, "Blasebalg kaufen");
            b.RaubBuchen(1_200);
            var jahr = sim.Zeit.Jahr;
            while (sim.Zeit.Jahr == jahr) sim.Tick(); // das Jahresende der KI läuft mit
            Pruefe.Ungefaehr(1_200, b.Vorjahr!.Raub, 1e-9);
            Pruefe.Wahr(b.HatEinrichtung("Eisentuer"), "Eisentür nach 1.200 Gulden Raub (0,5 Jahre)");
        }),
    };
}
