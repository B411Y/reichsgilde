using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Karren fahren zum näheren Handelsort (Datenversion 73, Nutzer 08.11.2026, Schalter <c>schiffshandel.karrenZumHafen</c>): Ein Karren, der zum
/// Handeln aufbricht, fährt zum Hafen, wenn der offen ist und Hin- und Rückfahrt dorthin kürzer dauern als zum Marktplatz; im Winter nur zum
/// Marktplatz. Gehandelt wird mit demselben Markt. Die Proben stehen in Köln an dem Bauort mit der kürzesten Fahrt zum Hafen (der Hafen dort
/// näher als jeder Marktplatz) – die Startwerkstätten liegen alle näher am Markt.
/// </summary>
static class HafenzielTests
{
    const string Hand = "Handkarren";

    /// <summary>Die Wirtschaftsdaten mit Bauzeit 0 für Werkstätten: Die Werkstatt steht sofort.</summary>
    static string Sofort(string json)
    {
        const string werkstatt = "\"werkstatt\": { \"breite\": 16, \"tiefe\": 14, \"bauzeitJahre\": 1,";
        if (!json.Contains(werkstatt)) throw new InvalidOperationException("Werkstatt-Maße in wirtschaft.json haben sich geändert – Test anpassen.");
        return json.Replace(werkstatt, "\"werkstatt\": { \"breite\": 16, \"tiefe\": 14, \"bauzeitJahre\": 0,");
    }

    static readonly Lazy<string> Json = new(() => Sofort(File.ReadAllText(Datenpfad.Wirtschaft())));

    static long Minute(int jahr, int stunde) => jahr * (long)Spielzeit.MinutenProTag + stunde * 60 - Spielzeit.TagesbeginnMinute;

    /// <summary>Köln zu dieser Minute, ohne Häuser, und ein Bürger mit Geld, der bauen darf.</summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto K) Koeln(string json, long minute)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 },
            new Spielzeit(minute), Wirtschaftsdaten.AusJson(json));
        var w = sim.Wirtschaft!;
        w.KarteSetzen(Stadtkarte.AusDatei(Datenpfad.Karte("koeln")));
        w.EinwohnerZaehlen();
        var k = w.KontoEroeffnen("Haus Probe", startvermoegen: 1_000_000);
        k.Stand = 1;
        k.WohnhausStufe = 1;
        k.BerufLernen("Schmied");
        w.Wohnen.HauswertAktualisieren(k);
        return (sim, w, k);
    }

    /// <summary>Der Bauort in Köln mit der kürzesten Fahrt (hin + zurück) zum Hafen, an dem der Hafen näher ist als jeder Marktplatz – gerechnet in <see cref="HafenAufschlagTests"/> (einmal für alle Hafen-Tests).</summary>
    static Lazy<WerkstattBauen> NahAmHafen => HafenAufschlagTests.NahAmHafen;

    /// <summary>Köln mit einer Gießerei am Bauort nahe dem Hafen (ohne Zutaten: ihr Karren bricht gleich zum Einkauf auf).</summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto K, Betrieb B, Karren Karren) Werkstatt(string json, long minute)
    {
        var (sim, w, k) = Koeln(json, minute);
        var e = w.Ausfuehren(k, NahAmHafen.Value);
        Pruefe.Wahr(e.Erfolg, e.Meldung);
        var name = w.Bauen.Grundstuecke.Last(g => g.Art == Grundstuecksart.Werkstatt).Betrieb!;
        var b = w.Betriebe.Single(x => x.Name == name);
        Pruefe.Wahr(b.Karren.Count > 0, $"{b.Name} hat keinen Karren");
        return (sim, w, k, b, b.Karren[0]);
    }

    /// <summary>Tickt, bis der Karren in diesem Zustand ist (höchstens ein Spieljahr); gibt die Minute zurück, in der es geschah.</summary>
    static long Bis(Simulation sim, Karren karren, KarrenZustand zustand)
    {
        for (var i = 0; i < Spielzeit.MinutenProTag && karren.Zustand != zustand; i++) sim.Tick();
        Pruefe.Gleich(zustand, karren.Zustand, $"{karren.Name} nach einem Jahr");
        return sim.Zeit.VergangeneMinuten;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Hafenziel: Werkstatt nahe dem Kölner Hafen im Sommer – der Karren fährt zum Hafen (kürzer als zum Marktplatz), kauft dort am Markt der Stadt " +
         "ein und fährt die Zeit des Kerns zurück; Gegenprobe ohne Schalter: zum Marktplatz", () =>
        {
            var (sim, w, _, b, karren) = Werkstatt(Json.Value, Minute(1, 9));
            Pruefe.Gleich(Jahreszeit.Sommer, w.Uhr.Jahreszeit);
            Pruefe.Wahr(w.Schiffahrt.KarrenZumHafen && w.Schiffahrt.HafenOffen, "Köln: Karren zum Hafen, Hafen offen");
            var (hafenHin, hafenZurueck) = (w.FahrzeitZumHafen(b, Hand, true)!.Value, w.FahrzeitZumHafen(b, Hand, false)!.Value);
            var (marktHin, marktZurueck) = (w.Fahrzeit(b, Hand, true), w.Fahrzeit(b, Hand, false));
            Pruefe.Wahr(hafenHin + hafenZurueck < marktHin + marktZurueck, $"Hafen {hafenHin}/{hafenZurueck} kürzer als Markt {marktHin}/{marktZurueck}");
            Pruefe.Wahr(w.FaehrtZumHafen(b, Hand), "führe zum Hafen");
            Pruefe.Gleich("Rheinhafen", w.HafenVon(b)!.Kennung);
            Pruefe.Gleich(w.Uhr.UhrzeitInMinuten + hafenHin, w.AnkunftAmMarkt(karren));

            var los = Bis(sim, karren, KarrenZustand.ZumMarkt) - 1; // (in dieser Minute aufgebrochen)
            Pruefe.Wahr(karren.ZielHafen, "Ziel Hafen");
            Pruefe.Gleich(los + hafenHin, karren.Ankunft);
            Pruefe.Gleich(hafenHin, w.Fahrzeit(karren, zumMarkt: true));
            Pruefe.Wahr(karren.ZustandText(w.Uhr).StartsWith("unterwegs zum Hafen"), karren.ZustandText(w.Uhr));
            Pruefe.Gleich(string.Join(";", w.KarrenverlaufZumHafen(b)!), string.Join(";", w.Karrenverlauf(karren)!));
            Pruefe.Gleich("Rheinhafen", w.HandelsortVon(karren)!.Kennung);
            Pruefe.Wahr(ReferenceEquals(w.Markt, w.Schiffahrt.HafenMarkt), "derselbe Markt");
            var zutaten = b.Zutaten.Keys.ToList();
            var preis = zutaten.ToDictionary(z => z, z => w.Markt[z].Preis);
            var zutatenVorher = b.Laufend.Zutaten;

            var an = Bis(sim, karren, KarrenZustand.ZumBetrieb) - 1;
            Pruefe.Gleich(los + hafenHin, an);
            Pruefe.Wahr(karren.ZielHafen, "auf dem Rückweg weiter Ziel Hafen");
            Pruefe.Wahr(karren.Ladung.Count > 0 && karren.Ladung.Keys.All(zutaten.Contains), $"Zutaten geladen: {string.Join(", ", karren.Ladung.Keys)}");
            // bezahlt zu den Preisen des Stadtmarkts (in der Minute der Ankunft; der Verwalter kauft zum Marktpreis)
            Pruefe.Ungefaehr(karren.Ladung.Sum(x => x.Value * preis[x.Key]), b.Laufend.Zutaten - zutatenVorher, 0.05 * (b.Laufend.Zutaten - zutatenVorher) + 1e-9);
            Pruefe.Gleich(an + hafenZurueck, karren.Ankunft);
            Pruefe.Gleich(an + hafenZurueck, Bis(sim, karren, KarrenZustand.AmBetrieb) - 1);

            // Gegenprobe: ohne Schalter fährt derselbe Karren zum Marktplatz
            var (simO, wO, _, bO, karrenO) = Werkstatt(Datenpfad.OhneHafenziel(Json.Value), Minute(1, 9));
            Pruefe.Gleich(null, wO.FahrzeitZumHafen(bO, Hand, true));
            Pruefe.Wahr(!wO.FaehrtZumHafen(bO, Hand), "ohne Schalter nicht zum Hafen");
            var losO = Bis(simO, karrenO, KarrenZustand.ZumMarkt) - 1;
            Pruefe.Wahr(!karrenO.ZielHafen, "ohne Schalter Ziel Marktplatz");
            Pruefe.Gleich(losO + marktHin, karrenO.Ankunft);
            Pruefe.Gleich(wO.MarktplatzVon(bO)!.Kennung, wO.HandelsortVon(karrenO)!.Kennung);
        }),

        ("Hafenziel: Im Winter (Eisschollen) fährt derselbe Karren zum Marktplatz; schließt der Hafen, während er zum Hafen unterwegs ist, " +
         "fährt er die Fahrt dorthin zu Ende", () =>
        {
            var (sim, w, _, b, karren) = Werkstatt(Json.Value, Minute(3, 9));
            Pruefe.Gleich(Jahreszeit.Winter, w.Uhr.Jahreszeit);
            Pruefe.Wahr(!w.Schiffahrt.HafenOffen && !w.FaehrtZumHafen(b, Hand), "im Winter zu");
            var los = Bis(sim, karren, KarrenZustand.ZumMarkt) - 1;
            Pruefe.Wahr(!karren.ZielHafen, "im Winter zum Marktplatz");
            Pruefe.Gleich(los + w.Fahrzeit(b, Hand, true), karren.Ankunft);

            // Herbst, kurz vor dem Jahreswechsel: Aufbruch zum Hafen, unterwegs beginnt der Winter
            var (simH, wH, _, bH, karrenH) = Werkstatt(Json.Value, (2 + 1) * (long)Spielzeit.MinutenProTag - 10);
            Pruefe.Gleich(Jahreszeit.Herbst, wH.Uhr.Jahreszeit);
            var hin = wH.FahrzeitZumHafen(bH, Hand, true)!.Value;
            var losH = Bis(simH, karrenH, KarrenZustand.ZumMarkt) - 1;
            Pruefe.Wahr(karrenH.ZielHafen && hin > 20, $"zum Hafen, {hin} Minuten");
            while (wH.Uhr.Jahreszeit != Jahreszeit.Winter) simH.Tick();
            Pruefe.Wahr(!wH.Schiffahrt.HafenOffen, "der Hafen ist zu");
            Pruefe.Wahr(karrenH.ZielHafen && karrenH.Zustand == KarrenZustand.ZumMarkt, "fährt weiter zum Hafen");
            Pruefe.Gleich(losH + hin, karrenH.Ankunft);
            var an = Bis(simH, karrenH, KarrenZustand.ZumBetrieb) - 1;
            Pruefe.Gleich(losH + hin, an);
            Pruefe.Gleich(an + wH.FahrzeitZumHafen(bH, Hand, false)!.Value, karrenH.Ankunft);
            // nach der Heimkehr bricht er zum Marktplatz auf
            Bis(simH, karrenH, KarrenZustand.AmBetrieb);
            Bis(simH, karrenH, KarrenZustand.ZumMarkt);
            Pruefe.Wahr(!karrenH.ZielHafen, "nächste Fahrt im Winter zum Marktplatz");
        }),

        ("Hafenziel: Die Startbetriebe in Greybox und Köln fahren zum Marktplatz (der Hafen ist weiter); eine Karte ohne Hafen hat keinen Weg zum Hafen", () =>
        {
            foreach (var kennung in new[] { "greybox", "koeln" })
            {
                var haeuser = JsonNode.Parse(File.ReadAllText(Datenpfad.Karte(kennung)))!["start"]?["haeuser"]?.AsArray().Count ?? 1;
                var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = Schwierigkeit.Schwer },
                    Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()), new NeuePartie(Rivalen: haeuser), karte: Stadtkarte.AusDatei(Datenpfad.Karte(kennung)));
                var w = sim.Wirtschaft!;
                Pruefe.Wahr(w.Schiffahrt.KarrenZumHafen && w.Schiffahrt.HafenOffen, $"{kennung}: Karren zum Hafen, offen");
                var mitWeg = w.Betriebe.Where(b => w.FahrtenZumHafen(b) is not null).ToList();
                Pruefe.Wahr(mitWeg.Count >= 4, $"{kennung}: {mitWeg.Count} Betriebe mit Weg zum Hafen");
                foreach (var b in w.Betriebe)
                    foreach (var typ in new[] { Hand, "Ochsenkarren", "Pferdekarren" })
                        Pruefe.Wahr(!w.FaehrtZumHafen(b, typ), $"{kennung}: {b.Name} {typ} fährt zum Marktplatz");
            }
            var (simM, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()), new NeuePartie(Rivalen: 1), karte: Stadtkarte.AusDatei(Datenpfad.Karte("messstadt")));
            var m = simM.Wirtschaft!;
            Pruefe.Wahr(!m.Schiffahrt.HatHafen && !m.Schiffahrt.KarrenZumHafen, "Messstadt ohne Hafen");
            Pruefe.Wahr(m.Betriebe.All(b => m.FahrtenZumHafen(b) is null && m.HafenVon(b) is null && !m.FaehrtZumHafen(b, Hand)), "kein Weg zum Hafen");
        }),

        ("Hafenziel: Spielstand – ein Karren unterwegs zum Hafen wird gespeichert, geladen und fährt bitgleich weiter; ein alter Spielstand ohne das Feld " +
         "fährt zum Marktplatz zurück", () =>
        {
            var (sim, w, k, b, karren) = Werkstatt(Json.Value, Minute(1, 9));
            Bis(sim, karren, KarrenZustand.ZumMarkt);
            Pruefe.Wahr(karren.ZielHafen, "unterwegs zum Hafen");
            var text = Speicherung.Speichern(sim, k, new DateTime(2026, 1, 1));
            var (geladen, _) = Speicherung.Laden(text);
            var karren2 = geladen.Wirtschaft!.Betriebe.Single(x => x.Name == b.Name).Karren[0];
            Pruefe.Wahr(karren2.ZielHafen && karren2.Ankunft == karren.Ankunft, "geladen: Ziel und Ankunft");
            for (var i = 0; i < 400; i++) { sim.Tick(); geladen.Tick(); }
            Pruefe.Gleich(Speicherung.Speichern(sim, k, new DateTime(2026, 1, 1)), Speicherung.Speichern(geladen, geladen.Wirtschaft.Konten.Single(x => x.Inhaber == k.Inhaber), new DateTime(2026, 1, 1)));

            // Alter Spielstand: ohne Feld ZielHafen fährt er (auf der Hinfahrt geladen) zurück, als käme er vom Marktplatz
            var alt = JsonNode.Parse(text)!;
            Entfernen(alt, "ZielHafen");
            var (altSim, _) = Speicherung.Laden(alt.ToJsonString());
            var wa = altSim.Wirtschaft!;
            var ba = wa.Betriebe.Single(x => x.Name == b.Name);
            var karrenA = ba.Karren[0];
            Pruefe.Wahr(!karrenA.ZielHafen, "alter Stand: Ziel Marktplatz");
            var an = Bis(altSim, karrenA, KarrenZustand.ZumBetrieb) - 1;
            Pruefe.Gleich(an + wa.Fahrzeit(ba, Hand, false), karrenA.Ankunft);

            static void Entfernen(JsonNode? n, string feld)
            {
                if (n is JsonObject o) { o.Remove(feld); foreach (var (_, v) in o.ToList()) Entfernen(v, feld); }
                else if (n is JsonArray a) foreach (var e in a) Entfernen(e, feld);
            }
        }),

        ("Hafenziel: Der Wegebau zählt die Fahrten zum Hafen auf deren Strecke – auf dem Stück, das nur zum Marktplatz führt, fahren dann keine Karren " +
         "der Werkstatt; Gegenprobe ohne Schalter: dort ihr Verkehr", () =>
        {
            (Stadtwirtschaft W, Betrieb B) EinJahr(string json)
            {
                var (sim, w, _, b, _) = Werkstatt(json, Minute(1, 4));
                var jahr = sim.Zeit.Jahr;
                while (sim.Zeit.Jahr == jahr) sim.Tick();
                return (w, b);
            }
            var (w, b) = EinJahr(Json.Value);
            Pruefe.Wahr(w.Wegebau.IstAktiv, "Wegebau aktiv");
            var alle = w.Wegebau.FahrtenVorjahr(b.Name);
            Pruefe.Wahr(alle > 0 && w.Wegebau.FahrtenHafenVorjahr(b.Name) == alle, $"alle {alle} Fahrten im Sommer zum Hafen ({w.Wegebau.FahrtenHafenVorjahr(b.Name)})");
            // Stellen auf Wegen der Karte, die nur die eine der beiden Strecken befährt (mehr als 10 m von jedem Stück der anderen auf demselben Weg).
            // Hier liegt der Hafen am Weg zum Marktplatz: Die Strecke zum Hafen endet vorher, das Stück dahinter befährt nur die zum Marktplatz.
            List<Fahrtstueck> Nur(IReadOnlyList<Fahrtstueck> diese, IReadOnlyList<Fahrtstueck> andere) => diese
                .Where(s => s.Art == Fahrbahn.Strasse && s.Weg >= 0 && !andere.Any(a => a.Weg == s.Weg && Math.Abs(a.Stelle - s.Stelle) < 10)).ToList();
            var hafenHin = w.FahrtenZumHafen(b)!.Value.Hin;
            var marktHin = w.Fahrten(b)!.Value.Hin;
            var nurMarkt = Nur(marktHin, hafenHin);
            var beide = hafenHin.Where(s => s.Art == Fahrbahn.Strasse && s.Weg >= 0 && marktHin.Any(a => a.Weg == s.Weg && Math.Abs(a.Stelle - s.Stelle) < 0.5)).ToList();
            Pruefe.Wahr(nurMarkt.Count > 0 && beide.Count > 0, $"Stücke nur zum Marktplatz {nurMarkt.Count}, auf beiden Strecken {beide.Count}");
            double Verkehr(Stadtwirtschaft x, List<Fahrtstueck> stuecke) => stuecke.Average(s => x.Wegebau.Verkehr(s.Weg, s.Stelle));

            var (wO, bO) = EinJahr(Datenpfad.OhneHafenziel(Json.Value));
            Pruefe.Gleich(0, wO.Wegebau.FahrtenHafenVorjahr(bO.Name));
            var alleO = wO.Wegebau.FahrtenVorjahr(bO.Name);
            Pruefe.Wahr(alleO > 0, "ohne Schalter Fahrten zum Marktplatz");
            // auf dem Stück nur zum Marktplatz fehlen genau die Fahrten der Werkstatt, auf dem gemeinsamen zählen alle
            Pruefe.Ungefaehr(Verkehr(wO, nurMarkt) - alleO, Verkehr(w, nurMarkt), 1e-9);
            Pruefe.Ungefaehr(Verkehr(wO, beide) - alleO + alle, Verkehr(w, beide), 1e-9);
        }),
    };
}
