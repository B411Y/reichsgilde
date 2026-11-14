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
/// Räume je Stufe des Wohnhauses (Nutzer, 14.11.2026; Datenversion 87, Abschnitt wohnen.raeume): Fachwerkhaus nur Wohnstube,
/// Bürgerhaus dazu Hinterzimmer, Steinhaus dazu ein Raum zur Wahl. Ohne Hinterzimmer keine Banden und Erpresserbriefe – für Spieler und KI;
/// Gastmahl nur mit Festsaal, die Bibliothek (Einrichtung) nur mit dem Raum Bibliothek.
/// </summary>
static class WohnhausRaeumeTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json.Value));
    static readonly Lazy<Wirtschaftsdaten> EinVersuch = new(() => Wirtschaftsdaten.AusJson(Datenpfad.EinVersuchJeNacht(Json.Value)));

    /// <summary>Dieselben Daten ohne „wohnen.raeume“ (wie vor Datenversion 87) – für die Gegenprobe.</summary>
    static readonly Lazy<Wirtschaftsdaten> OhneRaeume = new(() =>
    {
        var json = JsonNode.Parse(Json.Value)!;
        json["wohnen"]!.AsObject().Remove("raeume");
        return Wirtschaftsdaten.AusJson(json.ToJsonString());
    });

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null, int startwert = 1400) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, wirtschaftsdaten: daten ?? Daten.Value);

    static Konto Haus(Stadtwirtschaft w, string name, int stufe, double geld = 50_000)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: geld);
        k.WohnhausStufe = stufe;
        k.Aktionspunkte.Setzen(12);
        return k;
    }

    static string Liste(IEnumerable<string> x) => string.Join(",", x);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Wohnhaus-Räume: Daten – Fachwerkhaus Wohnstube, Bürgerhaus + Hinterzimmer, Steinhaus + 1 Raum zur Wahl; Datenversion 87", () =>
        {
            var r = Daten.Value.Wohnen!.Raeume!;
            Pruefe.Gleich(87, Daten.Value.Version);
            Pruefe.Gleich("Wohnstube,Hinterzimmer,Bibliothek,Festsaal", Liste(r.Arten.Select(a => a.Kennung)));
            Pruefe.Gleich("Wohnstube|Wohnstube,Hinterzimmer|Wohnstube,Hinterzimmer", string.Join("|", r.Stufen.Select(s => Liste(s.Feste))));
            Pruefe.Gleich("0,0,1", Liste(r.Stufen.Select(s => s.Frei.ToString())));
            Pruefe.Gleich("Hinterzimmer,Bibliothek,Festsaal", Liste(r.Waehlbar));
            Pruefe.Gleich("Beauftragen,Erpressen", Liste(r.Art("Hinterzimmer")!.Befehle));
            Pruefe.Gleich("Gastmahl", Liste(r.Art("Festsaal")!.Aktivitaeten));
            Pruefe.Gleich("Bibliothek", Liste(r.Art("Bibliothek")!.Einrichtung));
            // Fehler in den Daten werden gemeldet.
            var json = JsonNode.Parse(Json.Value)!;
            var raeume = json["wohnen"]!["raeume"]!;
            raeume["arten"]![1]!["befehle"]!.AsArray().Add("Zaubern");
            raeume["stufen"]![0]!["feste"]!.AsArray().Add("Keller");
            raeume["arten"]![3]!["aktivitaeten"]!.AsArray().Add("Tanz");
            foreach (var teil in new[] { "„Zaubern“", "„Keller“", "„Tanz“" })
                Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(json.ToJsonString()), teil);
        }),

        ("Wohnhaus-Räume: Stufe 1 nur Wohnstube, Stufe 2 mit Hinterzimmer, Stufe 3 wählt einen Raum – einmal, danach kein Platz mehr", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var eins = Haus(w, "Haus Eins", 0);
            var zwei = Haus(w, "Haus Zwei", 1);
            var drei = Haus(w, "Haus Drei", 2);
            Pruefe.Gleich("Wohnstube", Liste(w.Wohnen.Raeume(eins)));
            Pruefe.Wahr(!w.Wohnen.HatRaum(eins, "Hinterzimmer"), "Fachwerkhaus ohne Hinterzimmer");
            Pruefe.Gleich("Wohnstube,Hinterzimmer", Liste(w.Wohnen.Raeume(zwei)));
            Pruefe.Wahr(w.Wohnen.HatRaum(zwei, "Hinterzimmer"), "Bürgerhaus mit Hinterzimmer");
            Pruefe.Gleich(0, w.Wohnen.WaehlbareRaeume(eins).Count);
            Pruefe.Gleich(0, w.Wohnen.WaehlbareRaeume(zwei).Count);
            var nein = w.Ausfuehren(zwei, new RaumAnbauen("Festsaal"));
            Pruefe.Wahr(!nein.Erfolg && nein.Meldung.Contains("erst im Steinhaus"), nein.Meldung);

            Pruefe.Gleich("Bibliothek,Festsaal", Liste(w.Wohnen.WaehlbareRaeume(drei))); // das Hinterzimmer hat es schon
            Pruefe.Wahr(!w.Ausfuehren(drei, new RaumAnbauen("Hinterzimmer")).Erfolg, "schon da");
            Pruefe.Wahr(!w.Ausfuehren(drei, new RaumAnbauen("Wohnstube")).Erfolg, "nicht zur Wahl");
            Pruefe.Wahr(!w.Ausfuehren(drei, new RaumAnbauen("Keller")).Erfolg, "gibt es nicht");
            var vorher = drei.Vermoegen;
            var ja = w.Ausfuehren(drei, new RaumAnbauen("Festsaal"));
            Pruefe.Wahr(ja.Erfolg, ja.Meldung);
            Pruefe.Ungefaehr(vorher, drei.Vermoegen, 1e-9); // kostet nichts (kein Preis in den Daten)
            Pruefe.Gleich("Wohnstube,Hinterzimmer,Festsaal", Liste(w.Wohnen.Raeume(drei)));
            Pruefe.Gleich(0, w.Wohnen.WaehlbareRaeume(drei).Count);
            var voll = w.Ausfuehren(drei, new RaumAnbauen("Bibliothek"));
            Pruefe.Wahr(!voll.Erfolg && voll.Meldung.Contains("kein Platz"), voll.Meldung);
            // Höhere Stufen haben die Räume der letzten eingetragenen Stufe.
            var fuenf = Haus(w, "Haus Fünf", 4);
            Pruefe.Gleich("Bibliothek,Festsaal", Liste(w.Wohnen.WaehlbareRaeume(fuenf)));
        }),

        ("Wohnhaus-Räume: Banden und Erpresserbriefe nur mit Hinterzimmer; Briefe beantworten geht immer", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var eins = Haus(w, "Haus Eins", 0);
            var zwei = Haus(w, "Haus Zwei", 1);
            var ziel = Haus(w, "Haus Ziel", 0);
            foreach (var befehl in new Befehl[] { new Beauftragen(ziel.Inhaber, Gaunerstueck.Hauseinbruch), new Erpressen(ziel.Inhaber, 100) })
            {
                var e = w.Ausfuehren(eins, befehl);
                Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("„Hinterzimmer“ (ab Bürgerhaus)"), $"{befehl.GetType().Name}: {e.Meldung}");
                Pruefe.Gleich(12, eins.Aktionspunkte.Vorrat); // abgelehnt kostet nichts
            }
            var ok = w.Ausfuehren(zwei, new Beauftragen(ziel.Inhaber, Gaunerstueck.Hauseinbruch));
            Pruefe.Wahr(ok.Erfolg, ok.Meldung);
            // Erpressen scheitert ohne Beweise – aber nicht mehr am Raum.
            var ohneBeweis = w.Ausfuehren(zwei, new Erpressen(ziel.Inhaber, 100));
            Pruefe.Wahr(!ohneBeweis.Meldung.Contains("Hinterzimmer"), ohneBeweis.Meldung);
            var antwort = w.Ausfuehren(eins, new ErpressungBeantworten(zwei.Inhaber, true));
            Pruefe.Wahr(!antwort.Meldung.Contains("Hinterzimmer"), antwort.Meldung);
        }),

        ("Wohnhaus-Räume: Gastmahl nur mit Festsaal (Sperrgrund für Tafel und KI); Bibliothek-Einrichtung nur mit dem Raum Bibliothek", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var k = Haus(w, "Haus Gast", 2);
            w.Familien.Gruenden(k, Volk.Mensch, Geschlecht.Mann, alter: 30);
            var r = w.Rat.Ratsherren[0];
            var gesperrt = w.Meinungen.Aktivitaet(k, r, "Gastmahl")!;
            Pruefe.Wahr(gesperrt.Sperrgrund?.Contains("„Festsaal“ (zur Wahl ab Steinhaus)") == true, gesperrt.Sperrgrund ?? "frei");
            Pruefe.Gleich(null, w.Meinungen.Aktivitaet(k, r, "Brettspiel")!.Sperrgrund); // andere Aktivitäten brauchen keinen Raum
            Pruefe.Wahr(!w.Ausfuehren(k, new Aktivitaet("Gastmahl", r.Kennung)).Erfolg, "ohne Festsaal");

            // Bibliothek: Stube und Wappen gekauft – es fehlt nur der Raum.
            foreach (var s in new[] { "Hauswappen", "Stube" }) Pruefe.Wahr(w.Ausfuehren(k, new WohnhausEinrichtungKaufen(s)).Erfolg, s);
            Pruefe.Wahr(w.Wohnen.EinrichtungVerboten(k, "Bibliothek")?.Contains("„Bibliothek“") == true, w.Wohnen.EinrichtungVerboten(k, "Bibliothek") ?? "frei");
            Pruefe.Gleich(Reichsgilde.Core.Wirtschaft.Baumzustand.Gesperrt, w.Wohnen.Baum(k).Stuecke.Single(x => x.Kennung == "Bibliothek").Zustand);

            Pruefe.Wahr(w.Ausfuehren(k, new RaumAnbauen("Festsaal")).Erfolg, "Festsaal");
            Pruefe.Gleich(null, w.Meinungen.Aktivitaet(k, r, "Gastmahl")!.Sperrgrund);
            Pruefe.Wahr(w.Ausfuehren(k, new Aktivitaet("Gastmahl", r.Kennung)).Erfolg, "mit Festsaal");
            Pruefe.Wahr(w.Wohnen.EinrichtungVerboten(k, "Bibliothek") is not null, "Festsaal gewählt – keine Bibliothek");

            var b = Haus(w, "Haus Buch", 2);
            Pruefe.Wahr(w.Ausfuehren(b, new RaumAnbauen("Bibliothek")).Erfolg, "Bibliothek");
            foreach (var s in new[] { "Hauswappen", "Stube", "Bibliothek" }) Pruefe.Wahr(w.Ausfuehren(b, new WohnhausEinrichtungKaufen(s)).Erfolg, s);
        }),

        ("Wohnhaus-Räume: Die KI heuert ohne Hinterzimmer keine Bande an – mit Bürgerhaus wohl (Gegenprobe)", () =>
        {
            int Anheuern(int stufe)
            {
                var sim = NeueSimulation(EinVersuch.Value);
                var w = sim.Wirtschaft!;
                var ziel = w.KontoEroeffnen("Haus Reich", startvermoegen: 30_000);
                ziel.WohnhausStufe = 2;
                var b = w.BetriebEroeffnen("Gießerei Reich", Betriebsart.Giesserei, ziel, startkarren: "");
                b.Einlagern("Goldkette", 12);
                var ki = w.KontoEroeffnen("Haus Lind", startvermoegen: 5_000);
                ki.WohnhausStufe = stufe;
                w.KiAnmelden(ki, new KiProfil(Gaunerei: 1));
                var auftraege = 0;
                var verboten = 0;
                w.Meldung += m =>
                {
                    if (m.StartsWith(ki.Inhaber + ": heuert im Hinterzimmer")) auftraege++;
                    if (m.StartsWith(ki.Inhaber) && m.Contains("Hinterzimmer“")) verboten++;
                };
                for (var i = 0; i < Spielzeit.MinutenProTag; i++) sim.Tick();
                Pruefe.Gleich(0, verboten); // die KI versucht es gar nicht erst
                return auftraege;
            }
            Pruefe.Gleich(0, Anheuern(0));
            Pruefe.Wahr(Anheuern(1) >= 1, "Bürgerhaus: die KI heuert an");
        }),

        ("Wohnhaus-Räume: Gewählter Raum übersteht Speichern und Laden; alter Spielstand ohne Feld lädt ohne Wahl; ohne Abschnitt hat jedes Haus jeden Raum", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = Haus(w, "Haus Krug", 2);
            Pruefe.Wahr(w.Ausfuehren(k, new RaumAnbauen("Bibliothek")).Erfolg, "Bibliothek");
            var json = Speicherung.Speichern(sim);
            Pruefe.Wahr(json.Contains("\"WohnhausRaeume\""), "Feld im Spielstand");
            var kg = Speicherung.Laden(json).Simulation.Wirtschaft!;
            var geladen = kg.Konten.Single(x => x.Inhaber == "Haus Krug");
            Pruefe.Gleich("Wohnstube,Hinterzimmer,Bibliothek", Liste(kg.Wohnen.Raeume(geladen)));

            // Alter Spielstand (vor Datenversion 87): kein Feld – das Steinhaus hat noch einen freien Platz.
            var alt = System.Text.RegularExpressions.Regex.Replace(json, @",?\s*""WohnhausRaeume"": \[[^\]]*\]", "");
            Pruefe.Wahr(!alt.Contains("WohnhausRaeume"), "Feld entfernt");
            var ka = Speicherung.Laden(alt).Simulation.Wirtschaft!;
            var kalt = ka.Konten.Single(x => x.Inhaber == "Haus Krug");
            Pruefe.Gleich("Wohnstube,Hinterzimmer", Liste(ka.Wohnen.Raeume(kalt)));
            Pruefe.Gleich(1, ka.Wohnen.FreiePlaetze(kalt));

            // Gegenprobe ohne Abschnitt: Banden und Gastmahl auch im Fachwerkhaus, Raum anbauen gibt es nicht.
            var o = NeueSimulation(OhneRaeume.Value).Wirtschaft!;
            var eins = Haus(o, "Haus Eins", 0);
            var ziel = Haus(o, "Haus Ziel", 0);
            Pruefe.Wahr(!o.Wohnen.HatRaumregeln && o.Wohnen.HatRaum(eins, "Hinterzimmer"), "jeder Raum");
            Pruefe.Wahr(o.Ausfuehren(eins, new Beauftragen(ziel.Inhaber, Gaunerstueck.Hauseinbruch)).Erfolg, "Bande im Fachwerkhaus");
            Pruefe.Wahr(!o.Ausfuehren(eins, new RaumAnbauen("Festsaal")).Erfolg, "kein Anbau ohne Raumregeln");
        }),
    };
}
