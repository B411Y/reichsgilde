using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für Befehle: Was ein Spieler an seinen Betrieben ändern darf – und was nicht.</summary>
static class BefehlTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static (Stadtwirtschaft W, Konto Spieler, Betrieb Giesserei, Betrieb Mine) Aufbau(Fronstatut statut = Fronstatut.Bestienfron)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { StartStatut = statut, Siegbedingung = Siegbedingung.Endlos },
            wirtschaftsdaten: Daten.Value);
        var w = sim.Wirtschaft!;
        var spieler = w.KontoEroeffnen("Spieler");
        return (w, spieler, w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, spieler), w.BetriebEroeffnen("Grube", Betriebsart.Mine, spieler));
    }

    static void Erfolg(Befehlsergebnis e) => Pruefe.Wahr(e.Erfolg, "abgelehnt: " + e.Meldung);

    static void Abgelehnt(Befehlsergebnis e, string textInMeldung)
    {
        Pruefe.Wahr(!e.Erfolg, "wurde ausgeführt, sollte aber abgelehnt werden: " + e.Meldung);
        Pruefe.Wahr(e.Meldung.Contains(textInMeldung), $"Begründung enthält nicht „{textInMeldung}“: {e.Meldung}");
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Befehl: Produkt wählen", () =>
        {
            var (w, s, g, _) = Aufbau();
            Erfolg(w.Ausfuehren(s, new ProduktWaehlen("Gießerei", "Rapier")));
            Pruefe.Gleich("Rapier", g.Produkt);
            Abgelehnt(w.Ausfuehren(s, new ProduktWaehlen("Gießerei", "Eisen")), "nicht herstellen");
            Abgelehnt(w.Ausfuehren(s, new ProduktWaehlen("Grube", "Rapier")), "keine Werkstatt");
        }),

        ("Befehl: Fremde Betriebe sind tabu", () =>
        {
            var (w, _, g, _) = Aufbau();
            var rivale = w.KontoEroeffnen("Rivale");
            Abgelehnt(w.Ausfuehren(rivale, new ProduktWaehlen("Gießerei", "Rapier")), "gehört nicht Rivale");
            Pruefe.Gleich("Silberring", g.Produkt);
            Abgelehnt(w.Ausfuehren(rivale, new ProduktWaehlen("Schloss", "Rapier")), "gibt es nicht");
        }),

        ("Befehl: Personal einstellen und entlassen – mit Statut und Platzzahl", () =>
        {
            var (w, s, g, m) = Aufbau(Fronstatut.FreieStadt);
            Abgelehnt(w.Ausfuehren(s, new PersonalSetzen("Gießerei", "Geselle", 2)), "nur Lehrlinge"); // Gesellen durch Beförderung (PR #224)
            Erfolg(w.Ausfuehren(s, new PersonalSetzen("Gießerei", "Lehrling", 2)));
            Pruefe.Gleich(2, g.Belegt);
            Abgelehnt(w.Ausfuehren(s, new PersonalSetzen("Gießerei", "Lehrling", 3)), "nur 2 Arbeitsplätze");
            Abgelehnt(w.Ausfuehren(s, new PersonalSetzen("Grube", "Ork", 1)), "darf unter dem Statut");
            var ergebnis = w.Ausfuehren(s, new PersonalSetzen("Gießerei", "Lehrling", 1));
            Erfolg(ergebnis);
            Pruefe.Wahr(ergebnis.Meldung.Contains("1× Lehrling entlassen"), ergebnis.Meldung);
            Pruefe.Gleich(0, m.Belegt);
        }),

        ("Befehl: Karren kaufen nur mit genug Geld", () =>
        {
            var (w, s, g, _) = Aufbau();
            Erfolg(w.Ausfuehren(s, new KarrenKaufen("Gießerei", "Kriegskarren")));
            Pruefe.Gleich(2, g.Karren.Count);
            Pruefe.Ungefaehr(4000 - 900, s.Vermoegen, 1e-9);
            for (var i = 0; i < 3; i++) w.Ausfuehren(s, new KarrenKaufen("Gießerei", "Kriegskarren"));
            Pruefe.Ungefaehr(400, s.Vermoegen, 1e-9);
            Abgelehnt(w.Ausfuehren(s, new KarrenKaufen("Gießerei", "Kriegskarren")), "fehlen 500");
            Abgelehnt(w.Ausfuehren(s, new KarrenKaufen("Gießerei", "Sänfte")), "gibt es nicht");
        }),

        ("Befehl: Eskorte und Nachtfahrt je Karren", () =>
        {
            var (w, s, _, m) = Aufbau();
            Erfolg(w.Ausfuehren(s, new EskorteWaehlen("Grube", 0, "Schwer")));
            Pruefe.Gleich("Schwer", m.Karren[0].Eskorte);
            Erfolg(w.Ausfuehren(s, new NachtfahrtErlauben("Grube", 0, true)));
            Pruefe.Gleich(true, m.Karren[0].NachtsFahren);
            Abgelehnt(w.Ausfuehren(s, new EskorteWaehlen("Grube", 3, "Normal")), "keinen Karren Nr. 4");
            Abgelehnt(w.Ausfuehren(s, new EskorteWaehlen("Grube", 0, "Leibgarde")), "gibt es nicht");
        }),

        ("Befehl: Förderung der Mine aufteilen", () =>
        {
            var (w, s, _, m) = Aufbau();
            Erfolg(w.Ausfuehren(s, new FoerderungAufteilen("Grube", 0.8)));
            Pruefe.Ungefaehr(0.8, m.Silberanteil, 1e-9);
            Abgelehnt(w.Ausfuehren(s, new FoerderungAufteilen("Grube", 1.5)), "zwischen 0 % und 100 %");
        }),

        ("Betriebsnamen sind eindeutig (Befehle nennen Betriebe beim Namen)", () =>
        {
            var (w, s, _, _) = Aufbau();
            Pruefe.WirftMitMeldung(() => w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, s), "gibt es schon");
        }),
    };
}
