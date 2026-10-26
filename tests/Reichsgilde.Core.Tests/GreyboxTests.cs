using Reichsgilde.Core.Zeit;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Die Greybox-Karte (game/karten/greybox.json, Bereich Leveldesign) mit den Bauregeln des Kerns:
/// Wohnhausplätze in Konto-Reihenfolge, der Probeplan des Kartenwerkzeugs ist gültig, Abwandlungen davon nicht.
/// </summary>
static class GreyboxTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));

    static string Pfad => Datenpfad.Karte("greybox");

    static Stadtkarte Karte() => Stadtkarte.AusDatei(Pfad);

    /// <summary>Ein Gebäude des Probeplans: „Werkstatt“, „Wohnhaus“ (8 × 10) oder „Stadthof“, Mitte und Drehung in Grad wie Godot.</summary>
    sealed record Probe(string Art, string Zone, double X, double Z, int Grad)
    {
        public BauBefehl Befehl(double dx = 0, double dz = 0, int? grad = null)
        {
            var d = (grad ?? Grad) / 90 % 4;
            return Art switch
            {
                "Werkstatt" => new WerkstattBauen(Betriebsart.Giesserei, X + dx, Z + dz, d),
                "Stadthof" => new WohnhausBauen(4, X + dx, Z + dz, d),
                _ => new WohnhausBauen(1, X + dx, Z + dz, d),
            };
        }

        /// <summary>Richtung zur Straße (Einheitsvektor) – für „auf die Straße geschoben“.</summary>
        public (double X, double Z) Vorne => new Grundriss(0, 0, 1, 1, Grad / 90 % 4).Vorne;
    }

    static List<Probe> Probeplan() =>
        JsonNode.Parse(File.ReadAllText(Pfad))!["probeplan"]!.AsArray()
            .Select(p => new Probe((string)p!["art"]!, (string)p["zone"]!, (double)p["punkt"]![0]!, (double)p["punkt"]![1]!, (int)p["drehung"]!))
            .ToList();

    static (Simulation Sim, Stadtwirtschaft W) MitKarte()
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);
        sim.Wirtschaft!.KarteSetzen(Karte());
        return (sim, sim.Wirtschaft);
    }

    static Konto Reich(Stadtwirtschaft w, string name)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: 100_000);
        k.Stand = 4; // Freiherr: darf jedes Wohnhaus bauen
        k.BerufLernen("Schmied");
        return k;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Greybox: Neue Werkstätten heißen nach der Straße – grammatisch richtig und kurz, für jede Karrenstraße der Karte", () =>
        {
            var erwartet = new Dictionary<string, string>
            {
                ["Ostgasse"] = "an der Ostgasse",
                ["Landstraße"] = "an der Landstraße",
                ["Ringgasse"] = "an der Ringgasse",
                ["Weg zum Abzweig Steinbruch am Felsenhang"] = "am Felsenhang",
                ["Weg zu Bauernhof an der Mühle"] = "an der Mühle",
                ["Weg zu Grube im Tannenwald"] = "im Tannenwald",
                ["Weg zu Grube Eisenbart"] = "am Weg zur Grube Eisenbart",
                ["Weg zum Abzweig Steinbruch Kieselbach"] = "am Weg zum Steinbruch Kieselbach",
                ["Heumarkt"] = "am Heumarkt",
            };
            foreach (var (strasse, lage) in erwartet)
                Pruefe.Gleich(lage, Bauwesen.Lagebezeichnung(strasse), strasse);
            foreach (var weg in Karte().Wege.Where(x => x.IstKarrenstrasse))
            {
                var lage = Bauwesen.Lagebezeichnung(weg.Name);
                Pruefe.Wahr(lage.StartsWith("am ") || lage.StartsWith("an der ") || lage.StartsWith("im "), $"{weg.Name} → {lage}");
                Pruefe.Wahr(!lage.Contains("an der Weg") && !lage.Contains("Abzweig") && !lage.Contains("Weg zu "), $"{weg.Name} → {lage}");
                Pruefe.Wahr(lage.Length <= 40, $"zu lang: {weg.Name} → {lage}");
            }

            // Am Stück: die Werkstatt des Probeplans in V8 liegt am Weg zum Steinbruch am Felsenhang.
            var (_, w) = MitKarte();
            var v8 = Probeplan().First(p => p.Art == "Werkstatt" && p.Zone == "V8");
            var name = w.Bauen.Vorschau(Reich(w, "Haus Esse"), v8.Befehl()).Betrieb!;
            Pruefe.Wahr(name.StartsWith("Gießerei ") && !name.Contains("Weg zu"), name);
        }),

        ("Greybox: Karte wird gelesen; Spieler und drei Rivalen bekommen Wohnhaus 1–4", () =>
        {
            var k = Karte();
            Pruefe.Gleich("greybox", k.Kennung);
            Pruefe.Gleich(8, k.Wohnhausplaetze.Count);
            Pruefe.Gleich(11, k.Bauzonen.Count); // dazu K1–K3 auf den Plätzen der früheren Werkstätten zum Kauf (PR #162)
            Pruefe.Gleich(Lage.Mauerring, k.Bauzonen.Single(z => z.Kennung == "M1").Art); // vor der Westmauer, bewusst Mauerring (siehe „fiktion“)
            Pruefe.Wahr(k.GebaeudeDerArt(Bausteinart.Werkstattplatz).All(f => f.Grundriss is not null), "jeder Werkstattplatz hat eine Fläche");

            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie(), karte: k);
            var w = sim.Wirtschaft!;
            for (var i = 0; i < w.Konten.Count; i++)
                Pruefe.Gleich($"Wohnhaus{i + 1}", w.Bauen.Wohnhaus(w.Konten[i])!.Kennung);
            Pruefe.Gleich(new Grundriss(0, -44.3, 17, 8, 0), w.Bauen.Wohnhaus(spieler)!.Grundriss);
            Pruefe.Gleich(2, w.Bauen.Wohnhaus(w.Konten[1])!.Grundriss.Drehung); // 180° → Eingang nach Norden
            Pruefe.Gleich(1, w.Bauen.Wohnhaus(w.Konten[2])!.Grundriss.Drehung); //  90° → Osten
            Pruefe.Gleich(3, w.Bauen.Wohnhaus(w.Konten[3])!.Grundriss.Drehung); // 270° → Westen
            foreach (var f in k.Wohnhausplaetze.Where(f => f.Kennung is not null))
            {
                var grad = (int)(System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Pfad))!["gebaeude"]!.AsArray()
                    .Single(x => (string)x!["kennung"]! == f.Kennung)!["drehung"]!);
                Pruefe.Gleich((grad / 90 % 4 + 4) % 4, f.Grundriss!.Value.Drehung, $"Drehung von {f.Kennung} = Grad / 90");
            }
        }),

        ("Greybox: Jedes Gebäude des Probeplans darf gebaut werden – auch alle zusammen", () =>
        {
            var (_, w) = MitKarte();
            var plan = Probeplan();
            Pruefe.Gleich(21, plan.Count);
            foreach (var p in plan)
                Pruefe.Gleich(null, w.Bauen.PlatzVerboten(p.Befehl()), $"{p.Art} in {p.Zone} bei {p.X}/{p.Z}, {p.Grad}°");
            foreach (var p in plan.Where(p => p.Art == "Stadthof"))
                Pruefe.Gleich(Lage.Mauerring, Karte().ZoneBei(p.X, p.Z)!.Art);

            var werkstatt = Reich(w, "Haus Esse");
            var n = 0;
            foreach (var p in plan)
            {
                var bauherr = p.Art == "Werkstatt" ? werkstatt : Reich(w, $"Haus Bau {++n}");
                var e = w.Ausfuehren(bauherr, p.Befehl());
                Pruefe.Wahr(e.Erfolg, $"{p.Art} in {p.Zone}: {e.Meldung}");
            }
            Pruefe.Gleich(8, w.Bauen.Grundstuecke.Count(g => g.Art == Grundstuecksart.Werkstatt));
            Pruefe.Gleich(8, w.Bauen.Grundstuecke.Select(g => g.Betrieb).Where(b => b is not null).Distinct().Count()); // eindeutige Namen
        }),

        ("Greybox: Abwandlungen des Probeplans werden abgewiesen – auf der Straße, auf einem Gebäude, außerhalb, Stadthof in der Vorstadt, Werkstatt an der Gasse", () =>
        {
            var (_, w) = MitKarte();
            var b = w.Bauen;
            foreach (var p in Probeplan())
            {
                var (vx, vz) = p.Vorne;
                Pruefe.Wahr(b.PlatzVerboten(p.Befehl(8 * vx, 8 * vz)) is not null, $"{p.Art} in {p.Zone} 8 m auf die Straße geschoben");
                Pruefe.Wahr(b.PlatzVerboten(p.Befehl(300, 0)) is not null, $"{p.Art} in {p.Zone} weit draußen");
                if (p.Art == "Werkstatt" && Karte().ZoneBei(p.X, p.Z)!.Art != Lage.Kern) // in Kernlage darf dort auch ein Stadthof stehen
                    Pruefe.Gleich("Ein Stadthof steht nur in Kernlage oder am Mauerring.", b.PlatzVerboten(new WohnhausBauen(4, p.X, p.Z, p.Grad / 90)));
            }
            // Auf einer Werkstatt der Karte
            var giesserei = Karte().PlatzVon("Gießerei Eisenbart")!.Grundriss!.Value;
            Pruefe.Wahr(b.PlatzVerboten(new WohnhausBauen(1, giesserei.X, giesserei.Z, 0)) is not null, "auf der Gießerei Eisenbart");

            // Rücken zur Straße: mindestens ein Gebäude des Probeplans hat hinter sich keine Straße – umgedreht wird es abgewiesen.
            var umgedreht = Probeplan().Select(p => b.PlatzVerboten(p.Befehl(grad: (p.Grad + 180) % 360))).ToList();
            Pruefe.Wahr(umgedreht.Any(g => g?.StartsWith("Die Vorderseite") == true), "ohne Straßenanschluss: " + string.Join(" | ", umgedreht));

            // Werkstatt an einer Gasse: Die Karrenstraßen vor der Werkstatt in V1 (Weststraße und Weg zum Holzschlag) werden probehalber
            // zu Gassen – dann darf die Werkstatt des Probeplans dort nicht mehr stehen, das Wohnhaus daneben schon.
            var plan = Probeplan();
            var werkstatt = plan.First(p => p.Art == "Werkstatt" && p.Zone == "V1");
            var wohnhaus = plan.First(p => p.Art == "Wohnhaus" && p.Zone == "V1");
            var vorplatz = b.Grundriss(werkstatt.Befehl()).Felder(2, nurVorplatz: true).ToList();
            var strassen = Karte().Wege.Where(x => x.IstKarrenstrasse && vorplatz.Any(f => x.Abstand(f.X, f.Z) <= 7)).Select(x => x.Name).ToHashSet();
            var json = JsonNode.Parse(File.ReadAllText(Pfad))!;
            foreach (var weg in json["wege"]!.AsArray().Where(x => strassen.Contains((string)x!["name"]!)))
                weg!["klasse"] = "gasse";
            var sim2 = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);
            sim2.Wirtschaft!.KarteSetzen(Stadtkarte.AusJson(json.ToJsonString(), Gebaeudekatalog.AusDatei(Stadtkarte.KatalogZu(Pfad))));
            Pruefe.Wahr(strassen.Contains("Weststraße"), "Weststraße");
            Pruefe.Wahr(sim2.Wirtschaft.Bauen.PlatzVerboten(werkstatt.Befehl())?.Contains("Karrenstraße") == true, "Werkstatt an der Gasse");
            Pruefe.Gleich(null, sim2.Wirtschaft.Bauen.PlatzVerboten(wohnhaus.Befehl()));
        }),

        ("Greybox: Ein Wohnhausplatz wird nach dem Abriss Bauplatz für jedes Wohnhaus (17 × 8, eingepasst), nicht für eine Werkstatt", () =>
        {
            var (sim, w) = MitKarte();
            var tot = w.KontoEroeffnen("Haus Pleite");
            var k = Reich(w, "Haus Neu");
            tot.IstBankrott = true;
            Pruefe.Wahr(w.Ausfuehren(k, new LeerstandAbreissen(tot.Inhaber)).Erfolg, "abreißen");
            for (var i = 0; i < 241; i++) sim.Tick();
            var platz = w.Bauen.Bauplaetze.Single();
            Pruefe.Gleich("Wohnhaus1", platz.Kennung);
            Pruefe.Gleich(Lage.Mauerring, platz.Lage);
            for (var stufe = 0; stufe < 5; stufe++)
            {
                var befehl = w.Bauen.Einrasten(new WohnhausBauen(stufe, 3, -45, 2));
                Pruefe.Gleich(new WohnhausBauen(stufe, 0, -44.3, 0), befehl);
                Pruefe.Gleich(platz.Grundriss, w.Bauen.Grundriss(befehl));
                Pruefe.Gleich(null, w.Bauen.PlatzVerboten(befehl), $"Stufe {stufe}"); // auch der Stadthof: der Platz liegt laut Karte am Mauerring
            }
            Pruefe.Gleich("Auf einem Wohnhausplatz stehen nur Wohnhäuser.", w.Bauen.PlatzVerboten(new WerkstattBauen(Betriebsart.Giesserei, 0, -45, 0)));
            var palais = new WohnhausBauen(3, 0, -44.3, 0);
            var v = w.Bauen.Vorschau(k, palais);
            Pruefe.Gleich((15_500.0, 2 * Spielzeit.MinutenProTag), (v.Kosten, v.BauzeitMinuten));
            Pruefe.Wahr(w.Ausfuehren(k, palais).Erfolg, "Stadtpalais auf dem Wohnhausplatz");
            Pruefe.Wahr(!w.Bauen.Bauplaetze.Any(), "Platz vergeben");
        }),

        ("Greybox: Bauorte – genau die gültigen Rasterstellen (gegen eine Suche über die ganze Stadt), in fester Reihenfolge und schnell genug für die KI", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie(), karte: Karte());
            var w = sim.Wirtschaft!;
            var k = Reich(w, "Haus Reich");
            var vorlagen = new BauBefehl[]
            {
                new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0), new WohnhausBauen(1, 0, 0, 0), new WohnhausBauen(3, 0, 0, 0), new WohnhausBauen(4, 0, 0, 0),
            };
            var orte = vorlagen.Select(v => w.Bauen.Bauorte(k, v)).ToList(); // einmal vorweg (der erste Aufruf übersetzt den Code)
            var zeitJeAbfrage = double.MaxValue; // die beste von drei Runden – der Rechner tut nebenher anderes
            for (var runde = 0; runde < 3; runde++)
            {
                var uhr = System.Diagnostics.Stopwatch.StartNew();
                orte = vorlagen.Select(v => w.Bauen.Bauorte(k, v)).ToList();
                zeitJeAbfrage = Math.Min(zeitJeAbfrage, uhr.Elapsed.TotalMilliseconds / vorlagen.Length);
            }
            Pruefe.Wahr(zeitJeAbfrage < 500, $"Bauorte braucht {zeitJeAbfrage:0} ms je Abfrage");

            for (var i = 0; i < vorlagen.Length; i++)
            {
                Pruefe.Wahr(orte[i].Count > 0, $"{vorlagen[i]}: kein Bauort");
                Pruefe.Wahr(orte[i].All(o => w.Bauen.BauVerboten(k, o) is null && w.Bauen.Einrasten(o) == o), $"{vorlagen[i]}: ungültiger Bauort");
                Pruefe.Wahr(orte[i].SequenceEqual(w.Bauen.Bauorte(k, vorlagen[i] with { X = 55, Z = -3, Drehung = 2 })), "Lage der Vorlage zählt nicht, Reihenfolge fest");
                // Gegenprobe: jede Rasterstelle der ganzen Stadt (Rand 30 m um die Bauzonen), jede Drehung
                var gefunden = orte[i].Select(o => (o.X, o.Z, o.Drehung)).ToHashSet();
                var karte = w.Karte!;
                var punkte = karte.Bauzonen.SelectMany(z => z.Flaeche).ToList();
                for (var x = punkte.Min(p => p[0]) - 30; x <= punkte.Max(p => p[0]) + 30; x += 2)
                    for (var z = punkte.Min(p => p[1]) - 30; z <= punkte.Max(p => p[1]) + 30; z += 2)
                        for (var d = 0; d < 4; d++)
                        {
                            var g = w.Bauen.Grundriss(vorlagen[i] with { Drehung = d });
                            var befehl = vorlagen[i] with { X = x + g.HalbX, Z = z + g.HalbZ, Drehung = d };
                            Pruefe.Gleich(w.Bauen.PlatzVerboten(befehl) is null, gefunden.Contains((befehl.X, befehl.Z, d)), $"{befehl}");
                        }
            }
            // Ein Stadthof nur am Mauerring oder in Kernlage (K1–K3), nie in der Vorstadt.
            Pruefe.Wahr(orte[3].All(o => w.Karte!.ZoneBei(o.X, o.Z)!.Art is Lage.Mauerring or Lage.Kern), "Stadthof in der Vorstadt");
            // Wer nicht bauen darf, bekommt keine Bauorte.
            Pruefe.Gleich(0, w.Bauen.Bauorte(spieler, new WohnhausBauen(4, 0, 0, 0)).Count);
        }),

        ("Greybox: So viele Stadtpalais und Stadthöfe passen gleichzeitig in die Bauzonen (gierig gezählt) – und vorn stehen die Bauplätze", () =>
        {
            int Anzahl(int stufe)
            {
                var (sim, w) = MitKarte();
                var n = 0;
                for (; n < 100; n++)
                {
                    var k = Reich(w, $"Haus {n}");
                    var orte = w.Bauen.Bauorte(k, new WohnhausBauen(stufe, 0, 0, 0));
                    if (orte.Count == 0) break;
                    Pruefe.Wahr(w.Ausfuehren(k, orte[0]).Erfolg, $"Stufe {stufe}, Bau {n + 1}");
                }
                return n;
            }
            var palais = Anzahl(3);
            var hoefe = Anzahl(4);
            Pruefe.Wahr(palais >= 3, $"nur {palais} Stadtpalais");
            Pruefe.Wahr(hoefe >= 1, $"nur {hoefe} Stadthöfe");

            // Ein Wohnhausplatz wird frei (Abriss): Er steht für jedes Wohnhaus vorn in der Liste, in seiner Drehung.
            var (sim2, w2) = MitKarte();
            var tot = w2.KontoEroeffnen("Haus Pleite");
            var bauherr = Reich(w2, "Haus Neu");
            tot.IstBankrott = true;
            Pruefe.Wahr(w2.Ausfuehren(bauherr, new LeerstandAbreissen(tot.Inhaber)).Erfolg, "abreißen");
            for (var i = 0; i < 241; i++) sim2.Tick();
            Pruefe.Gleich<BauBefehl>(new WohnhausBauen(4, 0, -44.3, 0), w2.Bauen.Bauorte(bauherr, new WohnhausBauen(4, 0, 0, 0))[0]);
            var platz = w2.Bauen.Bauplaetze.Single().Grundriss;
            Pruefe.Wahr(w2.Bauen.Bauorte(bauherr, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0)).All(o => !w2.Bauen.Grundriss(o).Ueberlappt(platz)), "keine Werkstatt auf dem Wohnhausplatz");
        }),
    };
}
