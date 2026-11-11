using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Höchstens eine Kirche je Glaube und Stadt (Nutzer, 11.11.2026; Datenwert kirche.kirchenJeGlaube, Datenversion 84): Neubau, Kauf und
/// Konvertieren mit Kirche werden abgewiesen, wenn von diesem Glauben schon eine steht oder gebaut wird; alte Spielstände mit zwei Kirchen
/// desselben Glaubens behalten beide. Dazu die Anzeigenamen Kapelle → Kirche → Stadtkirche.
/// </summary>
static class KirchengrenzeTests
{
    static readonly string Json = File.ReadAllText(Datenpfad.Wirtschaft());
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json));

    /// <summary>Dieselben Daten ohne Grenze (wie vor Datenversion 84) – für die Gegenprobe.</summary>
    static readonly Lazy<Wirtschaftsdaten> OhneGrenze = new(() => Wirtschaftsdaten.AusJson(Json.Replace("\"kirchenJeGlaube\": 1,", "")));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: daten ?? Daten.Value);

    /// <summary>Ein Haus mit Familie dieses Glaubens, Prediger, Stand Patrizier, Geld und Aktionspunkten.</summary>
    static Konto Haus(Stadtwirtschaft w, string name, Konfession glaube, double geld = 50_000)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: geld);
        w.Familien.Gruenden(k, Volk.Mensch, Geschlecht.Mann, alter: 30, glaube: glaube);
        k.BerufLernen("Prediger");
        k.Stand = 3;
        k.WohnhausStufe = 1;
        w.Wohnen.HauswertAktualisieren(k);
        k.Aktionspunkte.Setzen(12);
        return k;
    }

    /// <summary>Eine Kirche (direkt eröffnet wie aus einem Spielstand oder beim Start) mit einem Gesellen.</summary>
    static Betrieb Kirche(Stadtwirtschaft w, Konto k, string name, Betriebsart art = Betriebsart.Kirche)
    {
        var b = w.BetriebEroeffnen(name, art, k, startkarren: "");
        w.Einstellen(b, "Geselle", 1);
        return b;
    }

    static WerkstattBauen Kirchenbau => new(Betriebsart.Kirche, 0, 0, 0);

    static string Grund(Stadtwirtschaft w, Konto k) => w.Kirchenwesen.KirchengrenzeVerboten(k, Betriebsart.Kirche) ?? "";

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Kirchengrenze: Daten – eine Kirche je Glaube, Anzeigenamen Kapelle → Kirche → Stadtkirche (die Kennungen bleiben); negativ abgewiesen, fehlend = keine Grenze", () =>
        {
            var d = Daten.Value;
            Pruefe.Gleich(1, d.Kirche!.KirchenJeGlaube);
            Pruefe.Gleich("Kapelle", d.Art(Betriebsart.Kirche).Name);
            Pruefe.Gleich("Kirche", d.Art(Betriebsart.Dom).Name);
            Pruefe.Gleich("Stadtkirche", d.Art(Betriebsart.Kathedrale).Name);
            Pruefe.Gleich(Betriebsart.Dom, d.Art(Betriebsart.Kirche).Ausbau);
            Pruefe.Gleich(Betriebsart.Kirche, d.Berufe["Prediger"].Werkstatt);
            Pruefe.Gleich(0, OhneGrenze.Value.Kirche!.KirchenJeGlaube);
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Replace("\"kirchenJeGlaube\": 1,", "\"kirchenJeGlaube\": -1,")), "kirchenJeGlaube");
        }),

        ("Kirchengrenze: Neubau – steht eine katholische Kirche (gleich welcher Stufe), baut kein katholisches Haus eine zweite; ein evangelisches schon", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var psalm = Haus(w, "Haus Psalm", Konfession.Katholisch);
            var weihrauch = Haus(w, "Haus Weihrauch", Konfession.Katholisch);
            var luther = Haus(w, "Haus Luther", Konfession.Evangelisch);
            Pruefe.Gleich(null, w.Kirchenwesen.KirchengrenzeVerboten(weihrauch, Betriebsart.Kirche)); // Gegenprobe: noch keine Kirche
            Kirche(w, psalm, "Stadtkirche Psalm", Betriebsart.Kathedrale);
            Pruefe.Gleich(1, w.Kirchenwesen.KirchenDesGlaubens(Konfession.Katholisch));
            Pruefe.Gleich(0, w.Kirchenwesen.KirchenDesGlaubens(Konfession.Evangelisch));
            Pruefe.Wahr(Grund(w, weihrauch).Contains("Kirche der Katholiken"), $"Grund: {Grund(w, weihrauch)}");
            Pruefe.Wahr(Grund(w, psalm) != "", "auch das Haus mit der Kirche baut keine zweite");
            Pruefe.Gleich(null, w.Kirchenwesen.KirchengrenzeVerboten(luther, Betriebsart.Kirche)); // anderer Glaube
            Pruefe.Gleich(null, w.Kirchenwesen.KirchengrenzeVerboten(weihrauch, Betriebsart.Klosterhof)); // kein Kirchengebäude
            Pruefe.Gleich(null, w.Kirchenwesen.KirchengrenzeVerboten(weihrauch, Betriebsart.Giesserei));
        }),

        ("Kirchengrenze: Neubau mit Karte – der Befehl wird mit Grund abgewiesen; die Baustelle zählt schon mit", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            w.KarteSetzen(Stadtkarte.AusJson(BauenTests.KarteJson, Gebaeudekatalog.AusJson(BauenTests.KatalogJson)));
            var luther = Haus(w, "Haus Luther", Konfession.Evangelisch);
            var melanchthon = Haus(w, "Haus Melanchthon", Konfession.Evangelisch);
            var psalm = Haus(w, "Haus Psalm", Konfession.Katholisch);
            var ort = w.Bauen.Bauorte(luther, Kirchenbau).FirstOrDefault();
            Pruefe.Wahr(ort is not null, "kein Bauort für eine Kapelle");
            var e = w.Ausfuehren(luther, ort!);
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(1, w.Kirchenwesen.KirchenDesGlaubens(Konfession.Evangelisch)); // Baustelle
            Pruefe.Wahr(w.Bauen.BauherrVerboten(melanchthon, Kirchenbau) is { } g && g.Contains("Evangelische"), "Bauherr");
            Pruefe.Gleich(0, w.Bauen.Bauorte(melanchthon, Kirchenbau).Count);
            var abgelehnt = w.Ausfuehren(melanchthon, new WerkstattBauen(Betriebsart.Kirche, ort!.X, ort.Z, ort.Drehung));
            Pruefe.Wahr(!abgelehnt.Erfolg && abgelehnt.Meldung.Contains("Bischof"), abgelehnt.Meldung);
            Pruefe.Gleich(null, w.Bauen.BauherrVerboten(psalm, Kirchenbau)); // Gegenprobe: katholisch geht
        }),

        ("Kirchengrenze: Kauf – eine freie Kirche kauft kein Haus, dessen Glaube schon eine hat; ein Haus des anderen Glaubens kauft sie", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var psalm = Haus(w, "Haus Psalm", Konfession.Katholisch);
            var weihrauch = Haus(w, "Haus Weihrauch", Konfession.Katholisch);
            var luther = Haus(w, "Haus Luther", Konfession.Evangelisch);
            Kirche(w, psalm, "Kapelle Psalm");
            w.StartbetriebFreigeben("Kapelle am Markt", Betriebsart.Kirche);
            Pruefe.Wahr(w.LiegenschaftErwerbVerboten(weihrauch, "Kapelle am Markt") is { } g && g.Contains("Katholiken"), "Kaufgrund");
            var abgelehnt = w.Ausfuehren(weihrauch, new LiegenschaftErwerben("Kapelle am Markt"));
            Pruefe.Wahr(!abgelehnt.Erfolg && abgelehnt.Meldung.Contains("Bischof"), abgelehnt.Meldung);
            Pruefe.Wahr(w.Betriebe.All(b => b.Konto != weihrauch), "nichts gekauft");
            var e = w.Ausfuehren(luther, new LiegenschaftErwerben("Kapelle am Markt"));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(1, w.Kirchenwesen.KirchenDesGlaubens(Konfession.Evangelisch));
        }),

        ("Kirchengrenze: Konvertieren – wer eine Kirche hat, tritt nicht zu einem Glauben über, der schon eine hat; ohne Kirche oder ohne fremde Kirche schon", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var psalm = Haus(w, "Haus Psalm", Konfession.Katholisch);
            var luther = Haus(w, "Haus Luther", Konfession.Evangelisch);
            var wende = Haus(w, "Haus Wende", Konfession.Katholisch);
            Kirche(w, psalm, "Kapelle Psalm");
            Kirche(w, luther, "Kapelle Luther");
            Pruefe.Wahr(w.Glaube.KonvertierenVerboten(psalm, Konfession.Evangelisch) is { } g && g.Contains("Verkauft erst eure Kirche"), "Grund");
            var abgelehnt = w.Ausfuehren(psalm, new Konvertieren(Konfession.Evangelisch));
            Pruefe.Wahr(!abgelehnt.Erfolg, abgelehnt.Meldung);
            Pruefe.Gleich((Konfession?)Konfession.Katholisch, w.Glaube.GlaubeVon(psalm));
            var e = w.Ausfuehren(wende, new Konvertieren(Konfession.Evangelisch)); // ohne Kirche: geht
            Pruefe.Wahr(e.Erfolg, e.Meldung);

            // Gegenprobe: Steht keine evangelische Kirche, darf das Haus mit Kirche übertreten.
            var sim2 = NeueSimulation();
            var w2 = sim2.Wirtschaft!;
            var psalm2 = Haus(w2, "Haus Psalm", Konfession.Katholisch);
            Kirche(w2, psalm2, "Kapelle Psalm");
            var e2 = w2.Ausfuehren(psalm2, new Konvertieren(Konfession.Evangelisch));
            Pruefe.Wahr(e2.Erfolg, e2.Meldung);
            Pruefe.Gleich(1, w2.Kirchenwesen.KirchenDesGlaubens(Konfession.Evangelisch));
            Pruefe.Gleich(0, w2.Kirchenwesen.KirchenDesGlaubens(Konfession.Katholisch));
        }),

        ("Kirchengrenze: Bestandsschutz – zwei katholische Kirchen (alter Spielstand) bleiben nach Speichern und Laden, beide halten Messe; eine dritte nicht", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var psalm = Haus(w, "Haus Psalm", Konfession.Katholisch);
            var weihrauch = Haus(w, "Haus Weihrauch", Konfession.Katholisch);
            var dritter = Haus(w, "Haus Dritt", Konfession.Katholisch);
            Kirche(w, psalm, "Kapelle Psalm");
            Kirche(w, weihrauch, "Kirche Weihrauch", Betriebsart.Dom);

            var (kopie, _) = Speicherung.Laden(Speicherung.Speichern(sim, psalm));
            var w2 = kopie.Wirtschaft!;
            Pruefe.Gleich(2, w2.Kirchenwesen.KirchenDesGlaubens(Konfession.Katholisch));
            var kirchen = w2.Kirchenwesen.Kirchen.OrderBy(b => b.Name, StringComparer.Ordinal).ToList();
            Pruefe.Gleich("Kapelle Psalm|Kirche Weihrauch", string.Join("|", kirchen.Select(b => b.Name)));
            Pruefe.Gleich(Betriebsart.Dom, kirchen[1].Art); // die Kennung im Spielstand bleibt
            for (var i = 0; i < 10 * 60 + 1 - Spielzeit.TagesbeginnMinute; i++) kopie.Tick(); // bis nach der Messe
            Pruefe.Wahr(kirchen.All(b => w2.Kirchenwesen.DiesesJahr(b).Kollekte > 0), "beide Kirchen bekommen Kollekte");
            var k3 = w2.Konten.Single(k => k.Inhaber == "Haus Dritt");
            Pruefe.Wahr(w2.Kirchenwesen.KirchengrenzeVerboten(k3, Betriebsart.Kirche) is not null, "keine dritte");
            Pruefe.Gleich(null, w.Kirchenwesen.KirchengrenzeVerboten(dritter, Betriebsart.Giesserei));
        }),

        ("Kirchengrenze: ohne Datenwert (alte Daten) keine Grenze – ein zweites katholisches Haus kauft die freie Kirche", () =>
        {
            var sim = NeueSimulation(OhneGrenze.Value);
            var w = sim.Wirtschaft!;
            var psalm = Haus(w, "Haus Psalm", Konfession.Katholisch);
            var weihrauch = Haus(w, "Haus Weihrauch", Konfession.Katholisch);
            Kirche(w, psalm, "Kapelle Psalm");
            w.StartbetriebFreigeben("Kapelle am Markt", Betriebsart.Kirche);
            Pruefe.Gleich(null, w.Kirchenwesen.KirchengrenzeVerboten(weihrauch, Betriebsart.Kirche));
            var e = w.Ausfuehren(weihrauch, new LiegenschaftErwerben("Kapelle am Markt"));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(2, w.Kirchenwesen.KirchenDesGlaubens(Konfession.Katholisch));
        }),

        ("Kirchengrenze: KI – eine freie Kirche ist kein Kandidat, solange ihr Glaube schon eine hat (Gegenprobe: ohne fremde Kirche schon)", () =>
        {
            foreach (var mitKirche in new[] { false, true })
            {
                var sim = NeueSimulation();
                var w = sim.Wirtschaft!;
                var ki = Haus(w, "Haus Kanzel", Konfession.Katholisch, geld: 200_000);
                var rivale = w.KiAnmelden(ki, KiProfil.Standard with { Reserve = 0 });
                if (mitKirche) Kirche(w, Haus(w, "Haus Psalm", Konfession.Katholisch), "Kapelle Psalm");
                w.StartbetriebFreigeben("Kapelle am Markt", Betriebsart.Kirche);
                var kandidat = rivale.Expansionskandidaten().Any(x => x.Angebot.Name == "Kapelle am Markt");
                Pruefe.Gleich(!mitKirche, kandidat, mitKirche ? "trotz katholischer Kirche Kandidat" : "Gegenprobe: ohne Kirche kein Kandidat");
            }
        }),
    };
}
