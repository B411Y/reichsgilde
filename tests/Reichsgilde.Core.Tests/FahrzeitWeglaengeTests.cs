using System.IO.Compression;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;
using Reichsgilde.Kartenbau.Staedte;

/// <summary>
/// Fahrzeit der Karren nach Weglänge und Karrenart (Datenversion 65, Entscheidung des Nutzers vom 31.10.2026): Länge ÷ Tempo der Art
/// (Ochse 0,8, Handkarren 1,0, Pferd 1,3 m/s bei Tempo 1), Steigung und Belag je Stück, Ausbau höchstens 15 % schneller, Furten und Fähren,
/// Mindest- und Höchstdauer; ohne Karte, Platz oder Weg und mit alten Daten die feste Fahrzeit. Dass die Darstellung genau in dieser Zeit fährt,
/// prüft <c>GehtempoTests</c>.
/// </summary>
static class FahrzeitWeglaengeTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    /// <summary>Die echten Daten mit geändertem Inhalt.</summary>
    static Wirtschaftsdaten DatenMit(Action<JsonObject> aendern)
    {
        var knoten = JsonNode.Parse(File.ReadAllText(Datenpfad.Wirtschaft()))!.AsObject();
        aendern(knoten);
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    }

    /// <summary>Meter je Spielminute bei 1 m/s: 900 Sekunden je Spieltag ÷ 1140 Spielminuten (hier ausgeschrieben, nicht aus dem Kern).</summary>
    const double MeterJeMinuteBei1 = 900.0 / 1140;

    /// <summary>
    /// Die Fahrzeit nach der Formel, unabhängig vom Kern gerechnet: Summe der Stücke Länge × Steigungsfaktor × Belagfaktor ÷ (Tempo × 900/1140),
    /// dazu Furt und Fähre, auf ganze Minuten, zwischen Mindest- und Höchstdauer. Für die Tests der Steigung, des Wassers und des Belags.
    /// </summary>
    internal static int NachFormel(Stadtwirtschaft w, Betrieb b, string typ, bool hin)
    {
        var (h, z) = w.Fahrten(b)!.Value;
        var fahrt = hin ? h : z;
        var l = w.Daten.Logistik;
        var v = l.Karren[typ].Tempo * MeterJeMinuteBei1;
        var steigung = w.Karte!.Gelaendeform.HatHoehen ? l.Steigung : null;
        var minuten = fahrt.Sum(s => s.Laenge * (steigung?.Faktor(s.Steigung) ?? 1) * (w.Wegebau.IstAktiv ? w.Wegebau.Faktor(s) : 1)) / v;
        // Furten und Fähren: mit Datenversion 65 hält auch eine Furt an einer Gasse oder einem Fußweg auf (Netz aller Wege)
        if ((w.Karte.Wasserplan.HaeltAuf || w.Karte.Wasserplan.Querungen.Any(q => q.Art == Uebergangsart.Furt)) && l.Wasser is { } wasser) minuten += wasser.Aufschlag(fahrt);
        var gerundet = Math.Max(1, (int)Math.Round(minuten, MidpointRounding.AwayFromZero));
        return Math.Clamp(gerundet, l.Weglaenge!.MindestMinuten, l.Weglaenge.HoechstMinuten);
    }

    /// <summary>Probepartie in der Probestadt (Spieler Schmied, Werkstatt bei (−30 | −10) an der Hauptstraße, Markt bei (0 | 0)).</summary>
    static (Simulation Sim, Konto Spieler, Betrieb Werkstatt) Partie(Wirtschaftsdaten? daten = null, Partieeinstellungen? einstellungen = null)
    {
        var (sim, spieler) = new Probestadt().Bauen(Katalog.Value, Daten.Value).Ausprobieren(daten ?? Daten.Value, "Schmied", einstellungen);
        return (sim, spieler, sim.Wirtschaft!.Betriebe.Single(b => b.Konto == spieler));
    }

    static readonly string[] Arten = { "Handkarren", "Ochsenkarren", "Kriegskarren" };

    /// <summary>Ein Spielstand von vor Datenversion 65 (Greybox, Herbst 1406, mit festen Fahrzeiten gespeichert; siehe UhrumstellungTests).</summary>
    static readonly Lazy<string> AlterStand = new(() =>
    {
        var datei = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
            "tests", "Reichsgilde.Core.Tests", "Spielstaende", "format2-greybox-herbst1406.json.gz");
        using var gz = new GZipStream(File.OpenRead(datei), CompressionMode.Decompress);
        return new StreamReader(gz).ReadToEnd();
    });

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Fahrzeit nach Weglänge: Daten – Ochse 0,8, Handkarren 1,0, Pferd 1,3 m/s; 1 m/s = 0,79 m je Spielminute; mindestens 10, höchstens 360 Minuten; Ausbau höchstens 1,15", () =>
        {
            var d = Daten.Value;
            Pruefe.Gleich(65, d.Version);
            Pruefe.Gleich((1.0, 0.8, 1.3), (d.Logistik.Karren["Handkarren"].Tempo, d.Logistik.Karren["Ochsenkarren"].Tempo, d.Logistik.Karren["Kriegskarren"].Tempo));
            Pruefe.Ungefaehr(MeterJeMinuteBei1, Taktgeber.SekundenJeSpielminute, 1e-12);
            Pruefe.Ungefaehr(0.8 * MeterJeMinuteBei1, d.Logistik.Karren["Ochsenkarren"].MeterJeMinute, 1e-12);
            Pruefe.Gleich((10, 360), (d.Logistik.Weglaenge!.MindestMinuten, d.Logistik.Weglaenge.HoechstMinuten));
            Pruefe.Gleich(1.15, d.Wegebau!.AusbauHoechstens);
            Pruefe.Gleich(30, d.Logistik.FahrzeitAb(Arbeitsort.Werkstatt));
            Pruefe.Gleich(120, d.Logistik.FahrzeitAb(Arbeitsort.Umland));
        }),

        ("Fahrzeit nach Weglänge: Länge ÷ Tempo der Karrenart – der Ochse braucht am längsten, das Pferd am kürzesten; hin und zurück, auch nach dem Laden", () =>
        {
            var (sim, spieler, werkstatt) = Partie();
            var w = sim.Wirtschaft!;
            var (hin, zurueck) = w.Fahrten(werkstatt)!.Value;
            var laenge = hin.Sum(s => s.Laenge);
            Pruefe.Wahr(laenge > 20, $"Weg der Werkstatt zum Markt {laenge:0.0} m");
            Pruefe.Wahr(hin.All(s => w.Zeitfaktor(s) == 1), "die Probestadt ist an der Werkstatt flach und Fahrweg");
            foreach (var typ in Arten)
            {
                var erwartet = (int)Math.Round(laenge / (w.Daten.Logistik.Karren[typ].Tempo * MeterJeMinuteBei1), MidpointRounding.AwayFromZero);
                Pruefe.Gleich(erwartet, w.Fahrzeit(werkstatt, typ, zumMarkt: true), typ);
                Pruefe.Gleich(NachFormel(w, werkstatt, typ, false), w.Fahrzeit(werkstatt, typ, zumMarkt: false), typ + " zurück");
            }
            Pruefe.Wahr(w.Fahrzeit(werkstatt, "Ochsenkarren", true) > w.Fahrzeit(werkstatt, "Handkarren", true)
                        && w.Fahrzeit(werkstatt, "Handkarren", true) > w.Fahrzeit(werkstatt, "Kriegskarren", true), "Ochse > Hand > Pferd");
            var karren = werkstatt.Karren[0];
            Pruefe.Gleich("Handkarren", karren.Typ);
            Pruefe.Gleich(w.Fahrzeit(werkstatt, "Handkarren", true), w.Fahrzeit(karren, true));
            Pruefe.Gleich(w.Uhr.UhrzeitInMinuten + w.Fahrzeit(karren, true), w.AnkunftAmMarkt(karren));
            var (geladen, _) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            var w2 = geladen.Wirtschaft!.Betriebe.Single(b => b.Name == werkstatt.Name);
            foreach (var typ in Arten)
                Pruefe.Gleich((w.Fahrzeit(werkstatt, typ, true), w.Fahrzeit(werkstatt, typ, false)), (geladen.Wirtschaft.Fahrzeit(w2, typ, true), geladen.Wirtschaft.Fahrzeit(w2, typ, false)), typ);
        }),

        ("Fahrzeit nach Weglänge: Ein Karren, der losfährt, kommt nach genau seiner Fahrzeit an (Ankunft im Kern)", () =>
        {
            var (sim, _, werkstatt) = Partie();
            var w = sim.Wirtschaft!;
            var karren = werkstatt.Karren[0];
            for (var i = 0; i < 2 * Spielzeit.MinutenProTag && karren.Zustand == KarrenZustand.AmBetrieb; i++) sim.Tick();
            Pruefe.Gleich(KarrenZustand.ZumMarkt, karren.Zustand);
            var los = sim.Zeit.VergangeneMinuten - 1; // im Tick davor losgefahren
            Pruefe.Gleich(los + w.Fahrzeit(karren, zumMarkt: true), karren.Ankunft);
        }),

        ("Fahrzeit nach Weglänge: rechnet mit der Standard-Rundendauer, nicht mit der eingestellten (gleiche Zeiten bei 15 und 60 Minuten je Runde)", () =>
        {
            var (simA, _, a) = Partie();
            var (simB, _, b) = Partie(einstellungen: Partieeinstellungen.Standard with { RundenDauerMinuten = 60 });
            Pruefe.Gleich(60 * 60.0, simB.Takt.RundenDauerSekunden);
            foreach (var typ in Arten)
                Pruefe.Gleich(simA.Wirtschaft!.Fahrzeit(a, typ, true), simB.Wirtschaft!.Fahrzeit(b, typ, true), typ);
        }),

        ("Fahrzeit nach Weglänge: Mindest- und Höchstdauer begrenzen jede Fahrt", () =>
        {
            // sehr schnelle Handkarren (10 m/s): der Weg dauerte unter 10 Minuten – es bleiben 10
            var schnell = DatenMit(d => d["logistik"]!["karren"]!["Handkarren"]!["tempo"] = 10);
            var (sim, _, werkstatt) = Partie(schnell);
            var laenge = sim.Wirtschaft!.Fahrten(werkstatt)!.Value.Hin.Sum(s => s.Laenge);
            Pruefe.Wahr(laenge / (10 * MeterJeMinuteBei1) < 10, "ohne Grenze kürzer als 10 Minuten");
            Pruefe.Gleich(10, sim.Wirtschaft.Fahrzeit(werkstatt, "Handkarren", true));
            // Höchstdauer 20: der Ochse bräuchte länger
            var kurz = DatenMit(d => d["logistik"]!["weglaenge"]!["hoechstMinuten"] = 20);
            var (simK, _, wk) = Partie(kurz);
            Pruefe.Wahr(simK.Wirtschaft!.Fahrten(wk)!.Value.Hin.Sum(s => s.Laenge) / (0.8 * MeterJeMinuteBei1) > 20, "ohne Grenze länger als 20 Minuten");
            Pruefe.Gleich(20, simK.Wirtschaft.Fahrzeit(wk, "Ochsenkarren", true));
        }),

        ("Fahrzeit: ohne Karte, ohne Platz und mit alten Daten (ohne logistik.weglaenge) die feste Fahrzeit wie bisher", () =>
        {
            // ohne Karte: 30 Minuten in der Stadt für jede Karrenart
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            var b = w.Betriebe.First(x => x.Konto == spieler);
            foreach (var typ in Arten) Pruefe.Gleich((30, 30), (w.Fahrzeit(b, typ, true), w.Fahrzeit(b, typ, false)), typ);
            // alte Daten: die feste Fahrzeit (die Werkstatt der Probestadt liegt flach auf Fahrweg)
            var alt = DatenMit(d => d["logistik"]!.AsObject().Remove("weglaenge"));
            Pruefe.Wahr(alt.Pruefen().Count == 0, "alte Daten gültig");
            var (simA, _, wa) = Partie(alt);
            foreach (var typ in Arten) Pruefe.Gleich((30, 30), (simA.Wirtschaft!.Fahrzeit(wa, typ, true), simA.Wirtschaft.Fahrzeit(wa, typ, false)), typ);
            // alte Daten mit Pflaster auf der Hauptstraße: die feste Zeit mal dem Mittel der Belagfaktoren (0,85, ohne Ausbau-Grenze in alten Daten)
            var e = new Probestadt().Bauen(Katalog.Value, Daten.Value);
            e.BelagSetzen(e.Wege.Single(x => x.Name == "Hauptstraße"), Belagstufe.Pflaster);
            var ganzAlt = DatenMit(d => { d["logistik"]!.AsObject().Remove("weglaenge"); d["wegebau"]!.AsObject().Remove("ausbauHoechstens"); });
            var (simP, spielerP) = e.Ausprobieren(ganzAlt, "Schmied");
            var wp = simP.Wirtschaft!.Betriebe.Single(b => b.Konto == spielerP);
            var hinP = simP.Wirtschaft.Fahrten(wp)!.Value.Hin;
            Pruefe.Ungefaehr(0.85, simP.Wirtschaft.Wegebau.Faktor(hinP[0]), 1e-12);
            var erwartet = (int)Math.Round(30 * hinP.Sum(s => s.Laenge * simP.Wirtschaft.Wegebau.Faktor(s)) / hinP.Sum(s => s.Laenge), MidpointRounding.AwayFromZero);
            Pruefe.Wahr(erwartet < 30, $"Pflaster kürzer: {erwartet}");
            foreach (var typ in Arten) Pruefe.Gleich(erwartet, simP.Wirtschaft.Fahrzeit(wp, typ, true), typ);
        }),

        ("Fahrzeit: Ein alter Spielstand bringt seine Daten mit (ohne logistik.weglaenge) – er fährt weiter mit der festen Fahrzeit, Karren unterwegs kommen zur gespeicherten Zeit an", () =>
        {
            var (sim, _) = Speicherung.Laden(AlterStand.Value);
            var w = sim.Wirtschaft!;
            Pruefe.Wahr(w.Daten.Logistik.Weglaenge is null && w.Daten.Version < 65, $"Daten des Spielstands (Version {w.Daten.Version})");
            // die feste Zeit mit Steigung und Belag gemittelt, wie bis Datenversion 64 – für alle Karrenarten gleich
            foreach (var b in w.Betriebe.Where(b => w.Fahrten(b) is { Hin.Count: > 0 }))
            {
                var hin = w.Fahrten(b)!.Value.Hin;
                var alt = (int)Math.Round(w.Daten.Logistik.FahrzeitAb(b.Ort) * hin.Sum(s => s.Laenge * w.Zeitfaktor(s)) / hin.Sum(s => s.Laenge), MidpointRounding.AwayFromZero);
                foreach (var typ in Arten) Pruefe.Gleich(Math.Max(1, alt), w.Fahrzeit(b, typ, true), $"{b.Name} {typ}");
            }
            var alle = w.Betriebe.SelectMany(b => b.Karren).ToList();
            var unterwegs = alle.Where(k => k.Zustand != KarrenZustand.AmBetrieb).ToDictionary(k => k, k => (k.Zustand, k.Ankunft));
            Pruefe.Wahr(unterwegs.Count > 0, "im alten Stand sind Karren unterwegs");
            var zustand = alle.ToDictionary(k => k, k => k.Zustand);
            var (angekommen, neu) = (0, 0);
            for (var i = 0; i < 400; i++)
            {
                sim.Tick();
                var jetzt = sim.Zeit.VergangeneMinuten;
                foreach (var k in alle)
                {
                    if (k.Zustand == zustand[k]) continue;
                    if (unterwegs.Remove(k, out var alt))
                    {
                        Pruefe.Wahr(jetzt - 1 >= alt.Ankunft, $"{k.Name}: nicht vor der gespeicherten Ankunft {alt.Ankunft} (jetzt {jetzt - 1})");
                        angekommen++;
                    }
                    if (k.Zustand == KarrenZustand.ZumMarkt && k.TorWartenBis is null)
                    {
                        Pruefe.Gleich(jetzt - 1 + w.Fahrzeit(k, zumMarkt: true), k.Ankunft, $"{k.Name}: neue Fahrt mit der Zeit des Kerns");
                        neu++;
                    }
                    zustand[k] = k.Zustand;
                }
            }
            Pruefe.Wahr(angekommen > 0 && neu > 0, $"angekommen {angekommen}, neu losgefahren {neu}");
        }),

        ("Ausbau höchstens 15 % schneller: Pflaster 0,87 statt 0,85 der Zeit, Kies und Trampelpfad unverändert; ohne Angabe wie vorher", () =>
        {
            var wb = Daten.Value.Wegebau!;
            Pruefe.Ungefaehr(1 / 1.15, wb.Zeitfaktor(wb.TempoVon(Belagstufe.Pflaster)), 1e-12);
            Pruefe.Ungefaehr(0.85 / 0.95, wb.Zeitfaktor(wb.TempoVon(Belagstufe.Kies)), 1e-12);
            Pruefe.Ungefaehr(1.0, wb.Zeitfaktor(wb.TempoVon(Belagstufe.Fahrweg)), 1e-12);
            Pruefe.Ungefaehr(0.85 / 0.70, wb.Zeitfaktor(wb.TempoVon(Belagstufe.Trampelpfad)), 1e-12);
            var ohne = DatenMit(d => d["wegebau"]!.AsObject().Remove("ausbauHoechstens")).Wegebau!;
            Pruefe.Ungefaehr(0.85, ohne.Zeitfaktor(ohne.TempoVon(Belagstufe.Pflaster)), 1e-12);
        }),

        ("Fahrzeit nach Weglänge: Datenprüfung – ohne Tempo, Höchst- unter Mindestdauer, Ausbau unter 1", () =>
        {
            // fehlerhafte Daten lädt der Kern gar nicht erst (Wirtschaftsdaten.AusJson prüft und nennt den Fehler)
            Pruefe.WirftMitMeldung(() => DatenMit(d => d["logistik"]!["karren"]!["Ochsenkarren"]!.AsObject().Remove("tempo")), "Karren Ochsenkarren: tempo");
            Pruefe.WirftMitMeldung(() => DatenMit(d => d["logistik"]!["weglaenge"]!["hoechstMinuten"] = 5), "hoechstMinuten");
            Pruefe.WirftMitMeldung(() => DatenMit(d => d["logistik"]!["weglaenge"]!["mindestMinuten"] = 0), "mindestMinuten");
            Pruefe.WirftMitMeldung(() => DatenMit(d => d["wegebau"]!["ausbauHoechstens"] = 0.9), "ausbauHoechstens");
            // ohne logistik.weglaenge braucht ein Karren kein Tempo (alte Daten)
            var alt = DatenMit(d => { d["logistik"]!.AsObject().Remove("weglaenge"); d["logistik"]!["karren"]!["Ochsenkarren"]!.AsObject().Remove("tempo"); });
            Pruefe.Gleich(0.0, alt.Logistik.Karren["Ochsenkarren"].Tempo);
            Pruefe.Gleich(0, Daten.Value.Pruefen().Count);
        }),

        ("Gesparte Minuten eines Ausbaus: Fahrzeit vorher minus nachher wie im Spiel (ganze Minuten, Höchstdauer); alte Daten: feste Zeit auf die Strecke verteilt", () =>
        {
            var (sim, _, werkstatt) = Partie();
            var w = sim.Wirtschaft!;
            var hin = w.Fahrten(werkstatt)!.Value.Hin;
            var laenge = hin.Sum(s => s.Laenge);
            var v = MeterJeMinuteBei1; // ein Handkarren
            int Gerundet(double m) => Math.Clamp((int)Math.Round(m, MidpointRounding.AwayFromZero), 10, 360);
            Pruefe.Ungefaehr(0, w.GesparteMinuten(werkstatt, hin, 0), 1e-12);
            Pruefe.Ungefaehr(w.Fahrzeit(werkstatt.Karren[0], true) - Gerundet((laenge - 10) / v), w.GesparteMinuten(werkstatt, hin, 10), 1e-12);
            Pruefe.Wahr(w.GesparteMinuten(werkstatt, hin, 10) >= 12, "10 m Fahrweg weniger sparen mit dem Handkarren etwa 13 Minuten");
            // an der Höchstdauer (hier 10 Minuten für jede Fahrt) spart ein Ausbau nichts
            var (simH, _, wh) = Partie(DatenMit(d => d["logistik"]!["weglaenge"]!["hoechstMinuten"] = 10));
            Pruefe.Gleich(10, simH.Wirtschaft!.Fahrzeit(wh.Karren[0], true));
            Pruefe.Ungefaehr(0, simH.Wirtschaft.GesparteMinuten(wh, simH.Wirtschaft.Fahrten(wh)!.Value.Hin, 10), 1e-12);
            // alte Daten: die feste Zeit auf die Länge verteilt
            var (simA, _, wa) = Partie(DatenMit(d => d["logistik"]!.AsObject().Remove("weglaenge")));
            var hinA = simA.Wirtschaft!.Fahrten(wa)!.Value.Hin;
            Pruefe.Ungefaehr(30.0 * 10 / hinA.Sum(s => s.Laenge), simA.Wirtschaft.GesparteMinuten(wa, hinA, 10), 1e-12);
        }),

    };
}
