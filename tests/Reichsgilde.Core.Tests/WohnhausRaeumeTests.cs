using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Räume je Stufe des Wohnhauses (Nutzer, 14.11.2026; Datenversion 87, Abschnitt wohnen.raeume): Fachwerkhaus nur Wohnstube, Bürgerhaus ein
/// Platz zur Wahl (Hinterzimmer, Festsaal, Bibliothek), Steinhaus ein zweiter ohne Doppelung; Anbauen kostet 700 Gulden, endgültig.
/// Ohne Hinterzimmer keine Banden und Erpresserbriefe – außer für den Dieb (von seinem Betrieb aus); Gastmahl nur mit Festsaal; die Lehrschrift
/// (Buchweg, 1 AP) nur mit Bibliothek, die auch die frühere Wirkung des Einrichtungsstücks trägt. Für Spieler und KI.
/// </summary>
static class WohnhausRaeumeTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json.Value));
    static readonly Lazy<Wirtschaftsdaten> EinVersuch = new(() => Wirtschaftsdaten.AusJson(Datenpfad.EinVersuchJeNacht(Json.Value)));

    /// <summary>Dieselben Daten ohne „wohnen.raeume“ (wie vor Datenversion 87) – für die Gegenprobe.</summary>
    static readonly Lazy<Wirtschaftsdaten> OhneRaeume = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneRaeume(Json.Value)));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null, int startwert = 1400) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, wirtschaftsdaten: daten ?? Daten.Value);

    static Konto Haus(Stadtwirtschaft w, string name, int stufe, double geld = 50_000, params string[] raeume)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: geld);
        k.WohnhausStufe = stufe;
        k.Aktionspunkte.Setzen(12);
        foreach (var r in raeume) k.WohnhausRaumHinzufuegen(r);
        return k;
    }

    static string Liste(IEnumerable<string> x) => string.Join(",", x);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Wohnhaus-Räume: Daten – Fachwerkhaus Wohnstube, Bürgerhaus 1 und Steinhaus 2 Plätze zur Wahl, je 700 Gulden, Dieb ohne Hinterzimmer; Datenversion 87", () =>
        {
            var r = Daten.Value.Wohnen!.Raeume!;
            Pruefe.Gleich(87, Daten.Value.Version);
            Pruefe.Gleich("Wohnstube,Hinterzimmer,Bibliothek,Festsaal", Liste(r.Arten.Select(a => a.Kennung)));
            Pruefe.Gleich("Wohnstube|Wohnstube|Wohnstube", string.Join("|", r.Stufen.Select(s => Liste(s.Feste))));
            Pruefe.Gleich("0,1,2", Liste(r.Stufen.Select(s => s.Frei.ToString())));
            Pruefe.Gleich("Hinterzimmer,Bibliothek,Festsaal", Liste(r.Waehlbar));
            foreach (var raum in r.Waehlbar) Pruefe.Gleich(700.0, r.Art(raum)!.Preis, raum);
            Pruefe.Gleich("Beauftragen,Erpressen", Liste(r.Art("Hinterzimmer")!.Befehle));
            Pruefe.Gleich("Dieb", r.Art("Hinterzimmer")!.BerufStattRaum);
            Pruefe.Gleich("Gastmahl", Liste(r.Art("Festsaal")!.Aktivitaeten));
            Pruefe.Gleich("LehrschriftLesen", Liste(r.Art("Bibliothek")!.Befehle));
            Pruefe.Ungefaehr(1.25, r.Art("Bibliothek")!.Wirkung.AusbildungFaktor, 1e-12);
            // Die Bibliothek ist kein Stück Einrichtung mehr – es gibt sie nur einmal.
            Pruefe.Gleich(null, Daten.Value.Wohnen!.Stueck("Bibliothek"));
            // Fehler in den Daten werden gemeldet.
            var json = JsonNode.Parse(Json.Value)!;
            var raeume = json["wohnen"]!["raeume"]!;
            raeume["arten"]![1]!["befehle"]!.AsArray().Add("Zaubern");
            raeume["arten"]![1]!["berufStattRaum"] = "Zauberer";
            raeume["stufen"]![0]!["feste"]!.AsArray().Add("Keller");
            raeume["arten"]![3]!["aktivitaeten"]!.AsArray().Add("Tanz");
            json["wohnen"]!["einrichtung"]!.AsArray().Add(JsonNode.Parse("""{ "kennung": "Bibliothek", "name": "Bibliothek", "preis": 1500, "spalte": "Ansehen", "abStufe": 3 }"""));
            foreach (var teil in new[] { "„Zaubern“", "„Zauberer“", "„Keller“", "„Tanz“", "nicht beides" })
                Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(json.ToJsonString()), teil);
        }),

        ("Wohnhaus-Räume: Stufe 1 nur Wohnstube; Stufe 2 wählt einen Raum (700 Gulden); Stufe 3 einen zweiten ohne Doppelung; danach kein Platz, kein Umbau", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var eins = Haus(w, "Haus Eins", 0);
            Pruefe.Gleich("Wohnstube", Liste(w.Wohnen.Raeume(eins)));
            Pruefe.Wahr(!w.Wohnen.HatRaum(eins, "Hinterzimmer"), "Fachwerkhaus ohne Hinterzimmer");
            Pruefe.Gleich(0, w.Wohnen.WaehlbareRaeume(eins).Count);
            var nein = w.Ausfuehren(eins, new RaumAnbauen("Festsaal"));
            Pruefe.Wahr(!nein.Erfolg && nein.Meldung.Contains("erst im Bürgerhaus"), nein.Meldung);

            var zwei = Haus(w, "Haus Zwei", 1);
            Pruefe.Gleich("Wohnstube", Liste(w.Wohnen.Raeume(zwei)));
            Pruefe.Wahr(!w.Wohnen.HatRaum(zwei, "Hinterzimmer"), "Bürgerhaus: das Hinterzimmer ist kein fester Raum");
            Pruefe.Gleich("Hinterzimmer,Bibliothek,Festsaal", Liste(w.Wohnen.WaehlbareRaeume(zwei)));
            Pruefe.Wahr(!w.Ausfuehren(zwei, new RaumAnbauen("Wohnstube")).Erfolg, "nicht zur Wahl");
            Pruefe.Wahr(!w.Ausfuehren(zwei, new RaumAnbauen("Keller")).Erfolg, "gibt es nicht");
            var arm = Haus(w, "Haus Arm", 1, geld: 600);
            var teuer = w.Ausfuehren(arm, new RaumAnbauen("Festsaal"));
            Pruefe.Wahr(!teuer.Erfolg && teuer.Meldung.Contains("700"), teuer.Meldung);
            var vorher = zwei.Vermoegen;
            var ja = w.Ausfuehren(zwei, new RaumAnbauen("Hinterzimmer"));
            Pruefe.Wahr(ja.Erfolg, ja.Meldung);
            Pruefe.Ungefaehr(vorher - 700, zwei.Vermoegen, 1e-9);
            Pruefe.Gleich("Wohnstube,Hinterzimmer", Liste(w.Wohnen.Raeume(zwei)));
            Pruefe.Gleich(0, w.Wohnen.WaehlbareRaeume(zwei).Count);
            var voll = w.Ausfuehren(zwei, new RaumAnbauen("Bibliothek"));
            Pruefe.Wahr(!voll.Erfolg && voll.Meldung.Contains("kein Platz"), voll.Meldung);

            // Ausbau zum Steinhaus: ein zweiter Platz, das Hinterzimmer steht nicht mehr zur Wahl.
            zwei.WohnhausStufe = 2;
            Pruefe.Gleich("Bibliothek,Festsaal", Liste(w.Wohnen.WaehlbareRaeume(zwei)));
            var doppelt = w.Ausfuehren(zwei, new RaumAnbauen("Hinterzimmer"));
            Pruefe.Wahr(!doppelt.Erfolg && doppelt.Meldung.Contains("schon"), doppelt.Meldung);
            Pruefe.Wahr(w.Ausfuehren(zwei, new RaumAnbauen("Festsaal")).Erfolg, "zweiter Raum");
            Pruefe.Gleich("Wohnstube,Hinterzimmer,Festsaal", Liste(w.Wohnen.Raeume(zwei)));
            Pruefe.Wahr(!w.Ausfuehren(zwei, new RaumAnbauen("Bibliothek")).Erfolg, "kein dritter Platz im Steinhaus");
            // Stadtpalais und Stadthof: keine weiteren Plätze.
            zwei.WohnhausStufe = 4;
            Pruefe.Gleich(0, w.Wohnen.FreiePlaetze(zwei));
            Pruefe.Gleich(2, w.Wohnen.FreiePlaetze(Haus(w, "Haus Fünf", 4)));
        }),

        ("Wohnhaus-Räume: Banden und Erpresserbriefe nur mit Hinterzimmer; Briefe beantworten geht immer", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var ohne = Haus(w, "Haus Ohne", 2, 50_000, "Festsaal", "Bibliothek");
            var mit = Haus(w, "Haus Mit", 1, 50_000, "Hinterzimmer");
            var ziel = Haus(w, "Haus Ziel", 0);
            foreach (var befehl in new Befehl[] { new Beauftragen(ziel.Inhaber, Gaunerstueck.Hauseinbruch), new Erpressen(ziel.Inhaber, 100) })
            {
                var e = w.Ausfuehren(ohne, befehl);
                Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("„Hinterzimmer“ (zur Wahl ab Bürgerhaus)"), $"{befehl.GetType().Name}: {e.Meldung}");
                Pruefe.Gleich(12, ohne.Aktionspunkte.Vorrat); // abgelehnt kostet nichts
            }
            var ok = w.Ausfuehren(mit, new Beauftragen(ziel.Inhaber, Gaunerstueck.Hauseinbruch));
            Pruefe.Wahr(ok.Erfolg, ok.Meldung);
            // Erpressen scheitert ohne Beweise – aber nicht mehr am Raum.
            var ohneBeweis = w.Ausfuehren(mit, new Erpressen(ziel.Inhaber, 100));
            Pruefe.Wahr(!ohneBeweis.Meldung.Contains("Hinterzimmer"), ohneBeweis.Meldung);
            var antwort = w.Ausfuehren(ohne, new ErpressungBeantworten(mit.Inhaber, true));
            Pruefe.Wahr(!antwort.Meldung.Contains("Hinterzimmer"), antwort.Meldung);
        }),

        ("Wohnhaus-Räume: Der Dieb heuert von seinem Betrieb aus Banden an – auch im Fachwerkhaus ohne Hinterzimmer; ohne Diebesbetrieb nicht", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var dieb = Haus(w, "Haus Dieb", 0);
            var ziel = Haus(w, "Haus Ziel", 0);
            var vorher = w.Ausfuehren(dieb, new Beauftragen(ziel.Inhaber, Gaunerstueck.Hauseinbruch));
            Pruefe.Wahr(!vorher.Erfolg && vorher.Meldung.Contains("Hinterzimmer"), vorher.Meldung);
            w.BetriebEroeffnen("Schmugglerloch Dieb", Betriebsart.Schmugglerloch, dieb, startkarren: "");
            Pruefe.Wahr(!w.Wohnen.HatRaum(dieb, "Hinterzimmer"), "kein Hinterzimmer");
            Pruefe.Gleich(null, w.Wohnen.BefehlOhneRaum(dieb, new Erpressen(ziel.Inhaber, 100)));
            var ok = w.Ausfuehren(dieb, new Beauftragen(ziel.Inhaber, Gaunerstueck.Hauseinbruch));
            Pruefe.Wahr(ok.Erfolg, ok.Meldung);
            // Die Ausnahme gilt nur für die Befehle des Hinterzimmers: Gastmahl braucht weiter den Festsaal.
            w.Familien.Gruenden(dieb, Volk.Mensch, Geschlecht.Mann, alter: 30);
            Pruefe.Wahr(w.Meinungen.Aktivitaet(dieb, w.Rat.Ratsherren[0], "Gastmahl")!.Sperrgrund?.Contains("Festsaal") == true, "Gastmahl");
        }),

        ("Wohnhaus-Räume: Gastmahl nur mit Festsaal (Sperrgrund für Tafel und KI)", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var k = Haus(w, "Haus Gast", 1);
            w.Familien.Gruenden(k, Volk.Mensch, Geschlecht.Mann, alter: 30);
            var r = w.Rat.Ratsherren[0];
            var gesperrt = w.Meinungen.Aktivitaet(k, r, "Gastmahl")!;
            Pruefe.Wahr(gesperrt.Sperrgrund?.Contains("„Festsaal“ (zur Wahl ab Bürgerhaus)") == true, gesperrt.Sperrgrund ?? "frei");
            Pruefe.Gleich(null, w.Meinungen.Aktivitaet(k, r, "Brettspiel")!.Sperrgrund); // andere Aktivitäten brauchen keinen Raum
            Pruefe.Wahr(!w.Ausfuehren(k, new Aktivitaet("Gastmahl", r.Kennung)).Erfolg, "ohne Festsaal");
            Pruefe.Wahr(w.Ausfuehren(k, new RaumAnbauen("Festsaal")).Erfolg, "Festsaal");
            Pruefe.Gleich(null, w.Meinungen.Aktivitaet(k, r, "Gastmahl")!.Sperrgrund);
            Pruefe.Wahr(w.Ausfuehren(k, new Aktivitaet("Gastmahl", r.Kennung)).Erfolg, "mit Festsaal");
        }),

        ("Wohnhaus-Räume: Lehrschrift (Buchweg, 1 AP) nur mit Bibliothek – Üben (2 AP) immer; die Bibliothek trägt Ausbildung × 1,25 und Erfahrung × 1,1", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var k = Haus(w, "Haus Buch", 1);
            var f = w.Familien.Gruenden(k, Volk.Mensch, Geschlecht.Mann, alter: 30);
            f.Oberhaupt.TalentSetzen(Talent.Handwerk, 1);
            var grund = w.Kirchenwesen.LesenVerboten(k, Talent.Handwerk);
            Pruefe.Wahr(grund?.Contains("„Bibliothek“ (zur Wahl ab Bürgerhaus)") == true, grund ?? "frei");
            var e = w.Ausfuehren(k, new LehrschriftLesen(Talent.Handwerk));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("Bibliothek"), e.Meldung);
            Pruefe.Gleich(null, w.Familien.TrainingVerboten(k, Talent.Handwerk)); // der normale Weg bleibt
            Pruefe.Ungefaehr(1, w.Wohnen.AusbildungFaktor(k), 1e-12);
            var erfahrung = w.Familien.ErfahrungFaktor(f.Oberhaupt, Talent.Handwerk);

            Pruefe.Wahr(w.Ausfuehren(k, new RaumAnbauen("Bibliothek")).Erfolg, "Bibliothek");
            var nachher = w.Kirchenwesen.LesenVerboten(k, Talent.Handwerk);
            Pruefe.Wahr(nachher is null || !nachher.Contains("Bibliothek"), nachher ?? "frei");
            Pruefe.Gleich(1, w.ApKosten(k, new LehrschriftLesen(Talent.Handwerk)));
            Pruefe.Ungefaehr(1.25, w.Wohnen.AusbildungFaktor(k), 1e-12);
            if (w.Familien.LebensweiseDaten is not null)
                Pruefe.Ungefaehr(erfahrung * 1.1, w.Familien.ErfahrungFaktor(f.Oberhaupt, Talent.Handwerk), 1e-12);
        }),

        ("Wohnhaus-Räume: Die KI heuert ohne Hinterzimmer keine Bande an – mit Hinterzimmer wohl (Gegenprobe)", () =>
        {
            int Anheuern(params string[] raeume)
            {
                var sim = NeueSimulation(EinVersuch.Value);
                var w = sim.Wirtschaft!;
                var ziel = w.KontoEroeffnen("Haus Reich", startvermoegen: 30_000);
                ziel.WohnhausStufe = 2;
                var b = w.BetriebEroeffnen("Gießerei Reich", Betriebsart.Giesserei, ziel, startkarren: "");
                b.Einlagern("Goldkette", 12);
                var ki = w.KontoEroeffnen("Haus Lind", startvermoegen: 5_000);
                ki.WohnhausStufe = 1;
                foreach (var r in raeume) ki.WohnhausRaumHinzufuegen(r);
                w.KiAnmelden(ki, new KiProfil(Gaunerei: 1));
                var auftraege = 0;
                w.Meldung += m => { if (m.StartsWith(ki.Inhaber + ": heuert im Hinterzimmer")) auftraege++; };
                for (var i = 0; i < Spielzeit.MinutenProTag; i++) sim.Tick();
                return auftraege;
            }
            Pruefe.Gleich(0, Anheuern());
            Pruefe.Gleich(0, Anheuern("Festsaal"));
            Pruefe.Wahr(Anheuern("Hinterzimmer") >= 1, "mit Hinterzimmer heuert die KI an");
        }),

        ("Wohnhaus-Räume: Die KI wählt zum Jahresende – Gaunerei → Hinterzimmer, Politik → Festsaal, sonst Bibliothek; nur über der Reserve", () =>
        {
            string Wahl(KiProfil profil, double geld = 20_000, int stufe = 1, params string[] schon)
            {
                var w = NeueSimulation().Wirtschaft!;
                var k = Haus(w, "Haus Wahl", stufe, geld, schon);
                var ki = w.KiAnmelden(k, profil);
                ki.Jahresende();
                return Liste(w.Wohnen.Raeume(k));
            }
            Pruefe.Gleich("Wohnstube,Hinterzimmer", Wahl(new KiProfil(Gaunerei: 1)));
            Pruefe.Gleich("Wohnstube,Festsaal", Wahl(new KiProfil(Gaunerei: 0)));
            Pruefe.Gleich("Wohnstube,Bibliothek", Wahl(new KiProfil(Gaunerei: 0, PolitikAnteil: 0)));
            Pruefe.Gleich("Wohnstube,Hinterzimmer,Festsaal", Wahl(new KiProfil(Gaunerei: 1), stufe: 2, schon: "Hinterzimmer"));
            Pruefe.Gleich("Wohnstube", Wahl(new KiProfil(Gaunerei: 1), geld: 2_000)); // 2.000 − 700 unter der Reserve von 1.500
            Pruefe.Gleich("Wohnstube", Wahl(new KiProfil(Gaunerei: 1), stufe: 0));
        }),

        ("Wohnhaus-Räume: Gewählte Räume überstehen Speichern und Laden; alter Spielstand ohne Feld lädt ohne Wahl; ohne Abschnitt hat jedes Haus jeden Raum", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = Haus(w, "Haus Krug", 2);
            Pruefe.Wahr(w.Ausfuehren(k, new RaumAnbauen("Bibliothek")).Erfolg, "Bibliothek");
            Pruefe.Wahr(w.Ausfuehren(k, new RaumAnbauen("Hinterzimmer")).Erfolg, "Hinterzimmer");
            var json = Speicherung.Speichern(sim);
            Pruefe.Wahr(json.Contains("\"WohnhausRaeume\""), "Feld im Spielstand");
            var kg = Speicherung.Laden(json).Simulation.Wirtschaft!;
            var geladen = kg.Konten.Single(x => x.Inhaber == "Haus Krug");
            Pruefe.Gleich("Wohnstube,Bibliothek,Hinterzimmer", Liste(kg.Wohnen.Raeume(geladen)));

            // Alter Spielstand (vor Datenversion 87): kein Feld – das Steinhaus hat zwei freie Plätze.
            var alt = System.Text.RegularExpressions.Regex.Replace(json, @",?\s*""WohnhausRaeume"": \[[^\]]*\]", "");
            Pruefe.Wahr(!alt.Contains("WohnhausRaeume"), "Feld entfernt");
            var ka = Speicherung.Laden(alt).Simulation.Wirtschaft!;
            var kalt = ka.Konten.Single(x => x.Inhaber == "Haus Krug");
            Pruefe.Gleich("Wohnstube", Liste(ka.Wohnen.Raeume(kalt)));
            Pruefe.Gleich(2, ka.Wohnen.FreiePlaetze(kalt));

            // Gegenprobe ohne Abschnitt: Banden auch im Fachwerkhaus, Raum anbauen gibt es nicht.
            var o = NeueSimulation(OhneRaeume.Value).Wirtschaft!;
            var eins = Haus(o, "Haus Eins", 0);
            var ziel = Haus(o, "Haus Ziel", 0);
            Pruefe.Wahr(!o.Wohnen.HatRaumregeln && o.Wohnen.HatRaum(eins, "Hinterzimmer"), "jeder Raum");
            Pruefe.Wahr(o.Ausfuehren(eins, new Beauftragen(ziel.Inhaber, Gaunerstueck.Hauseinbruch)).Erfolg, "Bande im Fachwerkhaus");
            Pruefe.Wahr(!o.Ausfuehren(eins, new RaumAnbauen("Festsaal")).Erfolg, "kein Anbau ohne Raumregeln");
        }),
    };
}
