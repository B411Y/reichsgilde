using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für den Handel von Hand: Karrenauftrag, Händlerspanne und Preiswirkung, Marktzeiten, Ware zurückhalten, Speichern.</summary>
static class HandelTests
{
    static readonly string Json = File.ReadAllText(Datenpfad.Wirtschaft());
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json));

    /// <summary>Simulation ab einer Uhrzeit (volle Stunde) im ersten Jahr.</summary>
    static Simulation Um(int stunde, Wirtschaftsdaten? daten = null, int minute = 0) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
            new Spielzeit(stunde * 60 + minute - Spielzeit.TagesbeginnMinute), daten ?? Daten.Value);

    /// <summary>Ein Haus (Mensch, Verhandeln 0) mit Gießerei, 10 Silberringen im Lager und einem Handkarren am Betrieb.</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb B) Haus(Simulation sim, double geld = 5_000)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Familie Krämer", startvermoegen: geld);
        var f = w.Familien.Gruenden(k, Volk.Mensch, Geschlecht.Mann);
        f.Oberhaupt.TalentSetzen(Talent.Verhandeln, 0);
        var b = w.BetriebEroeffnen("Gießerei Krämer", Betriebsart.Giesserei, k);
        b.Einlagern("Silberring", 10);
        return (w, k, b);
    }

    static Dictionary<string, int> D(params (string Ware, int Stueck)[] x) => x.ToDictionary(y => y.Ware, y => y.Stueck);
    static readonly Dictionary<string, int> Nichts = new();

    static void Minuten(Simulation sim, int n) { for (var i = 0; i < n; i++) sim.Tick(); }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Daten: Händlerspanne 10 %, Markt 8–20 Uhr; unsinnige Marktzeiten fallen auf", () =>
        {
            var h = Daten.Value.Handel!;
            Pruefe.Gleich(0.10, h.Aufschlag);
            Pruefe.Gleich((8, 20), (h.MarktVon, h.MarktBis));
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Replace("\"marktVon\": 8, \"marktBis\": 20", "\"marktVon\": 20, \"marktBis\": 8")), "handel.marktVon");
        }),

        ("Markt: Ein Einkauf von Hand kostet Preis × Spanne und hebt den Preis Stück für Stück; ein Verkauf senkt ihn wieder", () =>
        {
            var sim = Um(9);
            var stand = sim.Wirtschaft!.Markt["Gold"];
            var preis = stand.Preis;
            Pruefe.Ungefaehr(preis * 1.1, stand.HandKaufpreis(1, 1.1), 1e-9);
            Pruefe.Wahr(stand.HandKaufpreis(20, 1.1) > 20 * preis * 1.1, "jedes Stück soll teurer werden");
            stand.HandKaufen(20, 1.1);
            Pruefe.Wahr(stand.Preis > preis, "Preis nach dem Einkauf gestiegen");
            stand.Verkaufen(20);
            Pruefe.Ungefaehr(preis, stand.Preis, 1e-9);
        }),

        ("Auftrag: Der Karren verkauft 10 Silberringe, kauft 5 Gold (+10 %) und bringt es ins Lager; Gold wird zurückgehalten", () =>
        {
            var sim = Um(9);
            var (w, k, b) = Haus(sim);
            var preisRing = w.Markt["Silberring"].Preis;
            var kosten = w.HandKaufpreis(k, "Gold", 5);
            var umsatz = k.Laufend.Umsatz;
            var e = w.Ausfuehren(k, new Handelsauftrag(b.Name, 0, D(("Silberring", 10)), D(("Gold", 5))));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Wahr(e.Meldung.Contains("Gold wird im Lager zurückgehalten"), e.Meldung);
            Pruefe.Gleich(0, b.ImLager("Silberring"));
            Pruefe.Gleich(KarrenZustand.ZumMarkt, b.Karren[0].Zustand);
            Pruefe.WirftMitMeldung(() => throw new Exception(w.Ausfuehren(k, new Handelsauftrag(b.Name, 0, Nichts, D(("Eisen", 1)))).Meldung), "unterwegs");
            Minuten(sim, 31);
            Pruefe.Wahr(k.Laufend.Umsatz - umsatz > 9 * preisRing, "Ringe nicht verkauft");
            Pruefe.Wahr(b.Karren[0].LetzterHandel!.Contains("5 Gold"), b.Karren[0].LetzterHandel!);
            Minuten(sim, 31);
            Pruefe.Gleich(5, b.ImLager("Gold"));
            Pruefe.Wahr(b.HaeltZurueck("Gold"), "Gold zurückgehalten");
            Pruefe.Wahr(k.Laufend.Zutaten >= kosten - 1e-6, "Einkauf verbucht");
            Minuten(sim, 6 * 60);
            Pruefe.Gleich(5, b.ImLager("Gold")); // der Verwalter verkauft es nicht
        }),

        ("Ablehnungen: Marktzeit (Ankunft vor 8 oder nach 20 Uhr), Ladeflächen, Lagerplatz, Geld, leerer Auftrag", () =>
        {
            var frueh = Um(7);
            var (w1, k1, b1) = Haus(frueh);
            Pruefe.WirftMitMeldung(() => throw new Exception(w1.Ausfuehren(k1, new Handelsauftrag(b1.Name, 0, D(("Silberring", 1)), Nichts)).Meldung), "von 8 bis 20 Uhr");
            var spaet = Um(19, minute: 45);
            var (w2, k2, b2) = Haus(spaet);
            Pruefe.WirftMitMeldung(() => throw new Exception(w2.Ausfuehren(k2, new Handelsauftrag(b2.Name, 0, D(("Silberring", 1)), Nichts)).Meldung), "käme um 20:15");

            var sim = Um(9);
            var (w, k, b) = Haus(sim, geld: 200);
            Pruefe.WirftMitMeldung(() => throw new Exception(w.Ausfuehren(k, new Handelsauftrag(b.Name, 0, Nichts, D(("Eisen", 16)))).Meldung), "fasst 1 × 15");
            Pruefe.WirftMitMeldung(() => throw new Exception(w.Ausfuehren(k, new Handelsauftrag(b.Name, 0, Nichts, D(("Eisen", 1), ("Silber", 1)))).Meldung), "Zu viel zum Einkaufen");
            b.Einlagern("Eisen", 30);
            Pruefe.WirftMitMeldung(() => throw new Exception(w.Ausfuehren(k, new Handelsauftrag(b.Name, 0, Nichts, D(("Eisen", 15)))).Meldung), "Lager fasst nur 40");
            Pruefe.WirftMitMeldung(() => throw new Exception(w.Ausfuehren(k, new Handelsauftrag(b.Name, 0, Nichts, D(("Gold", 15)))).Meldung), "so viel habt ihr nicht");
            Pruefe.WirftMitMeldung(() => throw new Exception(w.Ausfuehren(k, new Handelsauftrag(b.Name, 0, D(("Silberring", 11)), Nichts)).Meldung), "nur 10 Silberring");
            Pruefe.WirftMitMeldung(() => throw new Exception(w.Ausfuehren(k, new Handelsauftrag(b.Name, 0, Nichts, Nichts)).Meldung), "leer");
            Pruefe.WirftMitMeldung(() => throw new Exception(w.Ausfuehren(k, new Handelsauftrag(b.Name, 3, D(("Silberring", 1)), Nichts)).Meldung), "keinen Karren Nr. 4");
        }),

        ("Verhandeln senkt den Einkaufspreis wie es den Erlös hebt; fehlt das Geld am Markt, kauft der Karren weniger", () =>
        {
            var sim = Um(9);
            var (w, k, b) = Haus(sim, geld: 2_000);
            w.Familien.Von(k)!.Oberhaupt.TalentSetzen(Talent.Verhandeln, 4);
            var m = w.Familien.TalentDaten.Wirkung.Verhandeln;
            Pruefe.Ungefaehr(1.1 * (1 - 4 * m), w.HandKaufFaktor(k), 1e-12);
            var e = w.Ausfuehren(k, new Handelsauftrag(b.Name, 0, Nichts, D(("Gold", 10))));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            k.Familienkosten(k.Vermoegen - 150); // das Geld ist inzwischen anders ausgegeben
            Minuten(sim, 31);
            var gekauft = b.Karren[0].Ladung.GetValueOrDefault("Gold");
            Pruefe.Wahr(gekauft is > 0 and < 10, $"gekauft {gekauft}");
            Pruefe.Wahr(k.Vermoegen >= 0, "nicht ins Minus gekauft");
        }),

        ("Zurückhalten: Der Verwalter lädt die Ware nicht mehr; ein Auftrag darf sie trotzdem verkaufen", () =>
        {
            var sim = Um(9);
            var (w, k, b) = Haus(sim);
            Pruefe.Wahr(w.Ausfuehren(k, new WareZurueckhalten(b.Name, "Silberring", true)).Erfolg, "zurückhalten");
            b.Einlagern("Silberring", 20); // 30 Stück – sonst führe der Karren sofort
            Minuten(sim, 4 * 60);
            Pruefe.Wahr(b.ImLager("Silberring") >= 30, "Ringe verkauft, obwohl zurückgehalten");
            while (b.Karren[0].Zustand != KarrenZustand.AmBetrieb) sim.Tick(); // er holt womöglich gerade Zutaten
            Pruefe.Wahr(w.Ausfuehren(k, new Handelsauftrag(b.Name, 0, D(("Silberring", 15)), Nichts)).Erfolg, "Auftrag");
            Pruefe.Wahr(w.Ausfuehren(k, new WareZurueckhalten(b.Name, "Silberring", false)).Erfolg, "freigeben");
            Pruefe.Wahr(!b.HaeltZurueck("Silberring"), "freigegeben");
        }),

        ("Speichern: Auftrag unterwegs und zurückgehaltene Waren überstehen Speichern und Laden", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                Daten.Value, new NeuePartie(Startvermoegen: 10_000), new Spielzeit(4 * 60));
            var w = sim.Wirtschaft!;
            var b = w.Betriebe.First(x => x.Konto == spieler);
            b.Einlagern("Silberring", 5);
            Pruefe.Wahr(w.Ausfuehren(spieler, new Handelsauftrag(b.Name, 0, D(("Silberring", 5)), D(("Gold", 3)))).Erfolg, "Auftrag");
            var (kopie, kopieSpieler) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            var kb = kopie.Wirtschaft!.Betriebe.First(x => x.Konto == kopieSpieler);
            Pruefe.Gleich(3, kb.Karren[0].Auftrag!.Kauf["Gold"]);
            Pruefe.Wahr(kb.HaeltZurueck("Gold"), "Zurückhalten gespeichert");
            Minuten(sim, 70); Minuten(kopie, 70);
            Pruefe.Gleich(b.ImLager("Gold"), kb.ImLager("Gold"));
            Pruefe.Gleich(3, kb.ImLager("Gold"));
            Pruefe.Ungefaehr(spieler.Vermoegen, kopieSpieler!.Vermoegen, 1e-9);
        }),

        ("Ohne Abschnitt „handel“: kein Handel von Hand", () =>
        {
            var ohne = Wirtschaftsdaten.AusJson(Json.Replace("\"handel\": {", "\"handelAus\": {"));
            var sim = Um(9, ohne);
            var (w, k, b) = Haus(sim);
            Pruefe.WirftMitMeldung(() => throw new Exception(w.Ausfuehren(k, new Handelsauftrag(b.Name, 0, D(("Silberring", 1)), Nichts)).Meldung), "nicht von Hand");
        }),
    };
}
