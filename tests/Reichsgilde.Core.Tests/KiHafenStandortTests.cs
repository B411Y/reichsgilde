using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Bauort nach dem Hafen (phase5/ki-hafen, offener Punkt „Hafenziel“ aus kern.md): Seit Datenversion 73 fährt ein Karren an den Hafen, wenn der offen
/// ist und näher liegt als der Marktplatz. Die KI wertet einen Bauort jetzt nach der Rundfahrt, die sein Karren im Mittel über die Jahre macht
/// (<see cref="KiRivale.ErwarteteRundfahrt"/>: im Winter zum Marktplatz), und sucht auch unter den nächsten Bauorten am Hafen
/// (<see cref="KiRivale.Standortkandidaten"/>). In Köln liegen die besten Bauorte am Markt (10–60 Minuten); sind die schnellsten belegt, baut die KI
/// am Rheinhafen – vorher nahm sie den nächsten freien Ort nach dem Weg zum Markt, und kein Karren der KI fuhr zum Hafen.
/// </summary>
static class KiHafenStandortTests
{
    const string Hand = "Handkarren";

    /// <summary>Die Wirtschaftsdaten mit Bauzeit 0 für Werkstätten: Die Werkstatt steht sofort (wie in den Hafenziel-Tests).</summary>
    static string Sofort(string json)
    {
        const string werkstatt = "\"werkstatt\": { \"breite\": 16, \"tiefe\": 14, \"bauzeitJahre\": 1,";
        if (!json.Contains(werkstatt)) throw new InvalidOperationException("Werkstatt-Maße in wirtschaft.json haben sich geändert – Test anpassen.");
        return json.Replace(werkstatt, "\"werkstatt\": { \"breite\": 16, \"tiefe\": 14, \"bauzeitJahre\": 0,");
    }

    static readonly Lazy<string> Json = new(() => Sofort(File.ReadAllText(Datenpfad.Wirtschaft())));

    /// <summary>Minute zu Beginn dieser Stunde im Jahr <paramref name="jahr"/> (0 Frühling, 1 Sommer, 2 Herbst, 3 Winter).</summary>
    static long Minute(int jahr, int stunde) => jahr * (long)Spielzeit.MinutenProTag + stunde * 60 - Spielzeit.TagesbeginnMinute;

    /// <summary>
    /// Eine Stadt zu dieser Minute, ohne Häuser: Alle Liegenschaften gehören „Haus Stadt“, „Haus Gold“ (Schmied, 15.000 Gulden) ist ein KI-Haus mit
    /// höchstens einem Betrieb – es kann also nur eine Gießerei bauen. „Haus Bau“ (reich) belegt Bauorte.
    /// </summary>
    static (Simulation Sim, Stadtwirtschaft W, KiRivale Ki, Konto Gold, Konto Bau) Stadt(string karte, string json, long minute)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 },
            new Spielzeit(minute), Wirtschaftsdaten.AusJson(json));
        var w = sim.Wirtschaft!;
        w.KarteSetzen(Stadtkarte.AusDatei(Datenpfad.Karte(karte)));
        w.EinwohnerZaehlen();
        var stadt = w.KontoEroeffnen("Haus Stadt");
        foreach (var l in w.FreieLiegenschaften.ToList()) w.BetriebEroeffnen(l.Name, l.Art, stadt, "");
        var gold = w.KontoEroeffnen("Haus Gold", startvermoegen: 15_000);
        gold.Stand = 1;
        gold.WohnhausStufe = 1;
        w.Wohnen.HauswertAktualisieren(gold);
        gold.BerufLernen("Schmied");
        var bau = w.KontoEroeffnen("Haus Bau", startvermoegen: 10_000_000);
        bau.Stand = 3;
        bau.BerufLernen("Schmied");
        var ki = w.KiAnmelden(gold, new KiProfil(MaxBetriebe: 1));
        return (sim, w, ki, gold, bau);
    }

    /// <summary>Die freien Bauorte einer Gießerei, nach Luftlinie zum nächsten Marktplatz (wie die KI sie zuerst ordnet).</summary>
    static List<BauBefehl> NachLuftlinie(Stadtwirtschaft w, Konto k) =>
        w.Bauen.Bauorte(k, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0)).OrderBy(o => w.Karte!.MarktAbstandQuadrat(o.X, o.Z)).ToList();

    static int Rund((int Hin, int Zurueck) z) => z.Hin + z.Zurueck;

    /// <summary>
    /// Köln im Sommer: „Haus Bau“ baut dort, wo die KI jetzt bauen würde, bis ihre Wahl am Hafen liegt. Gibt die belegten Orte zurück (zum
    /// Nachbauen in einer zweiten Stadt) – je Bau eine Gießerei, die die Stelle und ihre Nachbarn sperrt.
    /// </summary>
    static readonly Lazy<List<BauBefehl>> Belegt = new(() =>
    {
        var (_, w, ki, gold, bau) = Stadt("koeln", Json.Value, Minute(1, 4));
        var belegt = new List<BauBefehl>();
        for (var n = 0; n < 40; n++)
        {
            var (ort, wahl) = ki.Bauort(NachLuftlinie(w, gold), Betriebsart.Giesserei);
            Pruefe.Wahr(ort is not null && wahl is not null, "kein Bauort");
            if (wahl!.Value.ZumHafen) return belegt;
            var e = w.Ausfuehren(bau, ort!);
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            belegt.Add(ort!);
        }
        throw new InvalidOperationException("Nach 40 Bauten wählt die KI noch immer keinen Ort am Hafen.");
    });

    /// <summary>Köln im Sommer mit den belegten Orten; die KI baut ihre Gießerei (bis sie steht, höchstens ein Tag).</summary>
    static (Simulation Sim, Stadtwirtschaft W, KiRivale Ki, Betrieb B, BauBefehl Ort) KiBaut(string json)
    {
        var (sim, w, ki, gold, bau) = Stadt("koeln", json, Minute(1, 4));
        foreach (var o in Belegt.Value)
        {
            var e = w.Ausfuehren(bau, o);
            Pruefe.Wahr(e.Erfolg, e.Meldung);
        }
        var (ort, _) = ki.Bauort(NachLuftlinie(w, gold), Betriebsart.Giesserei); // was die KI gleich wählen wird
        for (var i = 0; i < Spielzeit.MinutenProTag && !w.Betriebe.Any(b => b.Konto == gold); i++) sim.Tick();
        var betrieb = w.Betriebe.Single(b => b.Konto == gold);
        var platz = w.Bauen.Grundstuecke.Single(g => g.Betrieb == betrieb.Name).Grundriss;
        Pruefe.Gleich((ort!.X, ort.Z, ort.Drehung), (platz.X, platz.Z, platz.Drehung), "gebaut, wo Bauort wählt");
        return (sim, w, ki, betrieb, ort);
    }

    /// <summary>Tickt, bis der Karren in diesem Zustand ist (höchstens ein Spieljahr).</summary>
    static void Bis(Simulation sim, Karren karren, KarrenZustand zustand)
    {
        for (var i = 0; i < Spielzeit.MinutenProTag && karren.Zustand != zustand; i++) sim.Tick();
        Pruefe.Gleich(zustand, karren.Zustand, $"{karren.Name} nach einem Jahr");
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Hafen: Erwartete Rundfahrt – zum Hafen nur, wenn er näher ist, mit dem Anteil der Jahre, in denen er offen ist (3/4 mit Jahreszeiten, sonst 1); " +
         "der Anteil passt zum Kern (im Winter zu)", () =>
        {
            var (_, w, ki, _, _) = Stadt("koeln", Json.Value, Minute(1, 9));
            Pruefe.Ungefaehr(0.75, ki.HafenOffenAnteil, 1e-12);
            Pruefe.Ungefaehr(234 - 0.75 * (234 - 130), ki.ErwarteteRundfahrt(234, 130), 1e-9);
            Pruefe.Ungefaehr(80, ki.ErwarteteRundfahrt(80, 80), 1e-12);   // Gleichstand: zum Marktplatz (wie der Kern)
            Pruefe.Ungefaehr(80, ki.ErwarteteRundfahrt(80, 200), 1e-12);  // Hafen weiter
            Pruefe.Ungefaehr(80, ki.ErwarteteRundfahrt(80, null), 1e-12); // kein Weg zum Hafen
            // Anker im Kern: offen im Frühling, Sommer, Herbst, zu im Winter – drei von vier Jahren
            var offen = Enumerable.Range(0, 4).Count(jahr => Stadt("koeln", Json.Value, Minute(jahr, 9)).W.Schiffahrt.HafenOffen);
            Pruefe.Gleich(3, offen);
            Pruefe.Wahr(w.Schiffahrt.KarrenZumHafen, "Köln: Karren zum Hafen");
            // ohne Jahreszeiten immer offen
            var (_, wo, kio, _, _) = Stadt("koeln", Datenpfad.OhneJahreszeiten(Json.Value), Minute(3, 9));
            Pruefe.Wahr(wo.Schiffahrt.HafenOffen, "ohne Jahreszeiten offen");
            Pruefe.Ungefaehr(1, kio.HafenOffenAnteil, 1e-12);
            Pruefe.Ungefaehr(130, kio.ErwarteteRundfahrt(234, 130), 1e-12);
        }),

        ("KI-Hafen: Die Fahrzeit zum Hafen an einem geplanten Bauort ist die, die der Kern nach dem Bau rechnet; Greybox (Hafen überall weiter) und " +
         "Messstadt (kein Hafen) wählen wie bisher nur nach dem Weg zum Markt", () =>
        {
            // Köln: an den Orten der Kandidatenliste, die zum Hafen führen, nach dem Bau dieselbe Zeit
            var (_, w, ki, gold, bau) = Stadt("koeln", Json.Value, Minute(1, 9));
            var kandidaten = ki.Standortkandidaten(NachLuftlinie(w, gold), Hand);
            Pruefe.Gleich(KiRivale.StandortKandidaten, kandidaten.Count(x => x.Rang < KiRivale.StandortKandidaten), "Köln: die 20 am Markt vorn");
            var amHafen = kandidaten.Where(x => x.Rang >= KiRivale.StandortKandidaten).ToList();
            Pruefe.Wahr(amHafen.Count > 0, $"Köln: {amHafen.Count} Orte am Hafen");
            foreach (var x in amHafen) // nach den Abfragen des Kerns, nicht nach der Rechnung der KI: der Hafen ist dort der nähere Handelsort
            {
                var o = (WerkstattBauen)x.Ort;
                var hafen = w.FahrzeitZumHafenAmBauort(o, Hand)!.Value + w.FahrzeitZumHafenAmBauort(o, Hand, hin: false)!.Value;
                var markt = w.FahrzeitAmBauort(o, Hand)!.Value + w.FahrzeitAmBauort(o, Hand, zumMarkt: false)!.Value;
                Pruefe.Wahr(hafen < markt, $"({o.X}, {o.Z}, {o.Drehung}): Hafen {hafen}, Markt {markt}");
                Pruefe.Ungefaehr(markt - 0.75 * (markt - hafen), x.Rundfahrt, 1e-9);
            }
            var probe = amHafen.OrderBy(x => x.Rundfahrt).First();
            Pruefe.Wahr(w.Ausfuehren(bau, probe.Ort).Erfolg, "gebaut");
            var b = w.Betriebe.Last(x => x.Konto == bau);
            foreach (var typ in w.Daten.Logistik.Karren.Keys)
            {
                Pruefe.Gleich((w.FahrzeitZumHafen(b, typ, true), w.FahrzeitZumHafen(b, typ, false)),
                    ki.FahrzeitZumHafenAmBauort((WerkstattBauen)probe.Ort, typ) is { } z ? ((int?)z.Hin, (int?)z.Zurueck) : (null, null), $"Köln {typ}");
            }
            Pruefe.Wahr(w.FaehrtZumHafen(b, Hand), "der Karren dieses Orts fährt im Sommer zum Hafen");

            foreach (var karte in new[] { "greybox", "messstadt" })
            {
                var (_, wk, kik, goldk, _) = Stadt(karte, Json.Value, Minute(1, 9));
                var liste = NachLuftlinie(wk, goldk);
                var k = kik.Standortkandidaten(liste, Hand);
                Pruefe.Wahr(k.Count > 0 && k.All(x => x.Rang < KiRivale.StandortKandidaten && !x.ZumHafen), $"{karte}: nur die {KiRivale.StandortKandidaten} am Markt");
                Pruefe.Wahr(k.All(x => x.Rundfahrt == Rund(x.Markt)), $"{karte}: Rundfahrt = Markt");
                Pruefe.Gleich(karte == "greybox", k.Any(x => x.Hafen is not null), $"{karte}: Weg zum Hafen bekannt");
                // die alte Regel: kürzeste Fahrt zum Markt unter den 20 nächsten, bei Gleichstand die nähere
                var alt = liste.Take(KiRivale.StandortKandidaten).Select((o, i) => (Ort: o, Rang: i, Zeit: kik.FahrzeitAmBauort((WerkstattBauen)o, Hand)))
                    .Where(x => x.Zeit is not null).OrderBy(x => Rund(x.Zeit!.Value)).ThenBy(x => x.Rang).First().Ort;
                Pruefe.Wahr(ReferenceEquals(alt, kik.Bauort(liste, Betriebsart.Giesserei).Ort), $"{karte}: wie bisher");
            }
        }),

        ("KI-Hafen: Köln – sind die schnellsten Bauorte am Markt belegt, baut die KI am Rheinhafen, und ihr Karren fährt im Sommer zum Hafen; " +
         "Gegenprobe ohne Karren zum Hafen: sie baut am Markt mit längerer Fahrt, ihr Karren fährt zum Marktplatz", () =>
        {
            Pruefe.Wahr(Belegt.Value.Count >= 3, $"belegt: {Belegt.Value.Count}");
            var (sim, w, ki, b, ort) = KiBaut(Json.Value);
            var markt = (w.Fahrzeit(b, Hand, true), w.Fahrzeit(b, Hand, false));
            var hafen = (w.FahrzeitZumHafen(b, Hand, true)!.Value, w.FahrzeitZumHafen(b, Hand, false)!.Value);
            Pruefe.Wahr(Rund(hafen) < Rund(markt), $"Hafen {hafen} kürzer als Markt {markt}");
            Pruefe.Gleich("Rheinhafen", w.HafenVon(b)!.Kennung);
            Pruefe.Wahr(ki.Entscheidungen.Any(e => e.Contains($"baut {b.Name} neu") && e.Contains($"{hafen.Item1} Minuten zum Hafen (im Winter {markt.Item1} zum Markt)")),
                string.Join(" | ", ki.Entscheidungen));
            var karren = b.Karren[0];
            Bis(sim, karren, KarrenZustand.ZumMarkt);
            Pruefe.Wahr(karren.ZielHafen, "der Karren der KI fährt zum Hafen");
            var erwartet = ki.ErwarteteRundfahrt(Rund(markt), Rund(hafen), ki.Ausfuhranteil(Betriebsart.Giesserei));

            // Gegenprobe: dieselben belegten Orte, ohne Karren zum Hafen – die KI wählt nur nach dem Weg zum Markt (wie vorher)
            var (simO, wO, kiO, bO, ortO) = KiBaut(Datenpfad.OhneHafenziel(Json.Value));
            Pruefe.Wahr((ortO.X, ortO.Z, ortO.Drehung) != (ort.X, ort.Z, ort.Drehung), "anderer Ort");
            var marktO = Rund((wO.Fahrzeit(bO, Hand, true), wO.Fahrzeit(bO, Hand, false)));
            Pruefe.Wahr(marktO > erwartet, $"Markt dort {marktO} Minuten, am Hafen im Mittel {erwartet:0}");
            Pruefe.Wahr(kiO.Entscheidungen.Any(e => e.Contains($"baut {bO.Name} neu") && e.Contains("Minuten zum Markt")), string.Join(" | ", kiO.Entscheidungen));
            var karrenO = bO.Karren[0];
            Bis(simO, karrenO, KarrenZustand.ZumMarkt);
            Pruefe.Wahr(!karrenO.ZielHafen, "ohne Schalter zum Marktplatz");
        }),

        ("KI-Hafen: Aufschlag am Hafen (Datenversion 80) – die Fahrt zum Hafen zählt um 1 + Aufschlag × Anteil der Ausfuhrware kürzer; Anteil nach dem " +
         "einträglichsten Produkt (Tischlerei und Siederei ohne Ausfuhrware 0); ohne Aufschlag in den Daten wie vorher", () =>
        {
            string MitAufschlag(double a)
            {
                var knoten = System.Text.Json.Nodes.JsonNode.Parse(Json.Value)!;
                knoten["schiffshandel"]!["hafenAufschlag"] = a;
                return knoten.ToJsonString();
            }
            var (_, w5, ki5, gold5, _) = Stadt("koeln", MitAufschlag(0.05), Minute(1, 9));
            var (_, w0, ki0, gold0, _) = Stadt("koeln", MitAufschlag(0), Minute(1, 9));
            // Formel: mit 5 % und nur Ausfuhrware zählt die Fahrt zum Hafen wie eine um 5 % kürzere, ohne Ausfuhrware wie vorher
            Pruefe.Ungefaehr(234 - 0.75 * (234 - 130 / 1.05), ki5.ErwarteteRundfahrt(234, 130, 1), 1e-9);
            Pruefe.Ungefaehr(234 - 0.75 * (234 - 130 / 1.025), ki5.ErwarteteRundfahrt(234, 130, 0.5), 1e-9);
            Pruefe.Ungefaehr(ki0.ErwarteteRundfahrt(234, 130), ki5.ErwarteteRundfahrt(234, 130, 0), 1e-12);
            Pruefe.Ungefaehr(ki0.ErwarteteRundfahrt(234, 130), ki0.ErwarteteRundfahrt(234, 130, 1), 1e-12); // ohne Aufschlag zählt der Anteil nicht
            Pruefe.Ungefaehr(80, ki5.ErwarteteRundfahrt(80, 80, 1), 1e-12); // Gleichstand: der Karren fährt zum Marktplatz, kein Aufschlag
            Pruefe.Ungefaehr(80, ki5.ErwarteteRundfahrt(80, null, 1), 1e-12);

            // Anteil: das einträglichste Produkt zu heutigen Preisen
            foreach (var art in new[] { Betriebsart.Giesserei, Betriebsart.Tischlerei, Betriebsart.Siederei, Betriebsart.Maurerei, Betriebsart.Kirche })
            {
                var produkte = w5.Daten.Art(art).Produkte;
                var best = produkte.First(p => Kalkulation.GewinnJeArbeitsminute(w5, p) == produkte.Max(q => Kalkulation.GewinnJeArbeitsminute(w5, q)));
                Pruefe.Gleich(w5.Schiffahrt.Daten!.IstAusfuhrware(best) ? 1.0 : 0.0, ki5.Ausfuhranteil(art), $"{art}: {best}");
            }
            // fest: Die Gießerei macht zu Startpreisen Dolch oder Silberring (beides Ausfuhrware, nicht Beschlag); Tischlerei und Siederei haben
            // keine Ausfuhrware; die Schänke schenkt aus, ihr Karren fährt nur den Rest – 0, obwohl Körnermahlzeit und Dünnbier Ausfuhrware sind
            Pruefe.Gleich(1.0, ki5.Ausfuhranteil(Betriebsart.Giesserei));
            Pruefe.Gleich(0.0, ki5.Ausfuhranteil(Betriebsart.Tischlerei));
            Pruefe.Gleich(0.0, ki5.Ausfuhranteil(Betriebsart.Siederei));
            Pruefe.Gleich(0.0, ki5.Ausfuhranteil(Betriebsart.Schaenke));

            // Kandidaten in Köln: an den Orten am Hafen (mit Ausfuhrware) kürzer um genau den Aufschlag, die übrigen gleich
            var k5 = ki5.Standortkandidaten(NachLuftlinie(w5, gold5), Hand, 1);
            var k0 = ki0.Standortkandidaten(NachLuftlinie(w0, gold0), Hand, 1);
            Pruefe.Gleich(k0.Count, k5.Count);
            Pruefe.Wahr(k5.Any(x => x.ZumHafen), "Köln: Orte am Hafen");
            for (var i = 0; i < k5.Count; i++)
            {
                Pruefe.Gleich((k0[i].Ort.X, k0[i].Ort.Z, k0[i].Ort.Drehung), (k5[i].Ort.X, k5[i].Ort.Z, k5[i].Ort.Drehung));
                var erwartet = k5[i].ZumHafen ? Rund(k5[i].Markt) - 0.75 * (Rund(k5[i].Markt) - Rund(k5[i].Hafen!.Value) / 1.05) : Rund(k5[i].Markt);
                Pruefe.Ungefaehr(erwartet, k5[i].Rundfahrt, 1e-9);
                Pruefe.Wahr(k5[i].ZumHafen ? k5[i].Rundfahrt < k0[i].Rundfahrt : k5[i].Rundfahrt == k0[i].Rundfahrt, $"#{i}");
            }
        }),
    };
}
