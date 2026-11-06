using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Hafenarbeit (Spiel-KI, 6.11.2026, Entladen Stufe 3): Tagelöhner tragen die Ladung des Zierschiffs zwischen Stapel und Lagerschuppen –
/// echte Einwohner mit Ort Hafen, ohne Wirkung auf die Wirtschaft. Dazu der Fahrplan des Zierschiffs, der aus der Darstellung in den Kern zog.
/// </summary>
static class BuergerHafenarbeitTests
{
    static readonly Lazy<string> Text = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));

    static Wirtschaftsdaten Daten(bool mitHafenarbeit = true)
    {
        var json = JsonNode.Parse(Text.Value)!.AsObject();
        if (!mitHafenarbeit) json["buerger"]!.AsObject().Remove("hafenarbeit");
        return Wirtschaftsdaten.AusJson(json.ToJsonString());
    }

    static (Simulation Sim, Stadtwirtschaft W) Partie(Wirtschaftsdaten daten, string karte, int startwert = 1400)
    {
        var rivalen = JsonNode.Parse(File.ReadAllText(Datenpfad.Karte(karte)))!["start"]!["haeuser"]!.AsArray().Count;
        var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos },
            daten, rivalen > 0 ? new NeuePartie(Rivalen: rivalen) : new NeuePartie(), karte: Stadtkarte.AusDatei(Datenpfad.Karte(karte)));
        sim.Tick();
        return (sim, sim.Wirtschaft!);
    }

    static void BisMinute(Simulation sim, long minute)
    {
        while (sim.Zeit.VergangeneMinuten < minute) sim.Tick();
    }

    static List<Einwohner> AmHafen(Stadtwirtschaft w) => w.Einwohnerschaft.Am(Stadtort.Hafen).ToList();

    /// <summary>Der Fahrplan, wie ihn die Darstellung bis zu diesem PR rechnete (Stadtansicht.Zierschiff.cs, wörtlich) – zum Vergleich.</summary>
    static (int Phase, double Anteil, bool VonAuf, bool NachAuf, double LiegeBeginn) AlterFahrplan(double minute, int fahrt)
    {
        const int zierFenster = Spielzeit.MinutenProTag / 2;
        var fenster = (long)Math.Floor(minute / zierFenster);
        var im = minute - fenster * zierFenster;
        var z = unchecked((uint)(fenster * 2654435761L + 40503) ^ 0x9E3779B9u);
        unchecked { z ^= z >> 15; z *= 0x2C1B3C6Du; z ^= z >> 12; z *= 0x297A2D39u; z ^= z >> 15; }
        var ankunft = 70 + (int)(z % 61);
        var liegen = 60 + (int)((z >> 8) % 91);
        var vonAuf = (z & 0x10000) != 0;
        var nachAuf = (z & 0x20000) != 0;
        var anfang = ankunft - fahrt;
        var ablegen = ankunft + liegen;
        if (im < anfang || im >= ablegen + fahrt) return (0, 0, vonAuf, nachAuf, 0);
        var besuch = (double)(fenster * zierFenster + ankunft);
        if (im < ankunft) return (1, (im - anfang) / fahrt, vonAuf, nachAuf, besuch);
        if (im < ablegen) return (2, (im - ankunft) / liegen, vonAuf, nachAuf, besuch);
        return (3, (im - ablegen) / fahrt, vonAuf, nachAuf, besuch);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Zierfahrplan: im Kern genau wie bisher in der Darstellung (40 Jahre, jede Minute und Zwischenminuten, Fahrt 20–70)", () =>
        {
            foreach (var fahrt in new[] { 20, 35, 70 })
                for (var m = 0.0; m < 40 * Spielzeit.MinutenProTag; m += 0.75)
                {
                    var alt = AlterFahrplan(m, fahrt);
                    var neu = Zierfahrplan.Plan(m, fahrt);
                    Pruefe.Gleich(alt.Phase, (int)neu.Phase, $"Minute {m}");
                    Pruefe.Gleich(alt.Anteil, neu.Anteil, $"Minute {m}");
                    Pruefe.Gleich(alt.VonAuf, neu.VonFlussauf);
                    Pruefe.Gleich(alt.NachAuf, neu.NachFlussauf);
                    Pruefe.Gleich(alt.LiegeBeginn, neu.LiegeBeginn);
                }
            Pruefe.Wahr(Zierfahrplan.Plan(100, 30).Nieder, "morgens der Niederländer");
            Pruefe.Wahr(!Zierfahrplan.Plan(Zierfahrplan.Fenster + 100, 30).Nieder, "nachmittags der Oberländer");
        }),

        ("Hafenarbeit: zwei Besuche je Tag in Köln – Träger arbeiten Hand in Hand mit dem Kran (Entladen nach dem Absetzen, Beladen vor dem Heben), Stapel nur Puffer, danach leer; im Winter keine", () =>
        {
            var (_, w) = Partie(Daten(), "koeln");
            var ha = w.Einwohnerschaft.Hafenarbeit;
            Pruefe.Wahr(ha.IstAktiv, "Köln hat einen Hafen, der Schiffshandel ruht");
            var zahlen = new HashSet<int>();
            var groessterStapel = 0;
            var nachlauf = 0.0;
            const int fahrtVorlauf = 120;
            for (var jahr = Spielzeit.StartJahr; jahr < Spielzeit.StartJahr + 40; jahr++)
            {
                var s = ha.Schichten(jahr);
                var winter = (Jahreszeit)((jahr - Spielzeit.StartJahr) % 4) == Jahreszeit.Winter && w.Jahreszeiten.IstAktiv;
                Pruefe.Gleich(winter ? 0 : 2, s.Count, $"{jahr}");
                if (winter) continue;
                Pruefe.Wahr(s[0].Entladen && !s[1].Entladen, "morgens Entladen, nachmittags Beladen");
                foreach (var x in s)
                {
                    var (ankunft, liegen, _, _) = Zierfahrplan.Streuung(x.Fenster);
                    var anlegen = Zierfahrplan.Anfang(x.Fenster) + ankunft;
                    var tagesanfang = (jahr - Spielzeit.StartJahr) * (long)Spielzeit.MinutenProTag;
                    Pruefe.Wahr(x.Traeger is >= 2 and <= 4, $"{jahr}: {x.Traeger} Träger");
                    Pruefe.Gleich(18, x.Stuecke);
                    Pruefe.Wahr(x.VonStunde >= 4 && x.BisStunde <= 23, $"{jahr}: Stunden {x.VonStunde}–{x.BisStunde}");
                    var da = tagesanfang + x.VonStunde * 60 - Spielzeit.TagesbeginnMinute + 49;
                    Pruefe.Wahr(x.Beginn >= da - 1e-9, $"{jahr}: alle sind da, bevor getragen wird");
                    Pruefe.Wahr(tagesanfang + x.BisStunde * 60 - Spielzeit.TagesbeginnMinute >= x.Ende - 1e-9, $"{jahr}: sie bleiben bis zum Ende");
                    zahlen.Add(x.Traeger);
                    // jeder Träger macht einen Gang nach dem anderen, Gänge dauern eine Runde, der Stapel hält Reihenfolge Kran ↔ Träger ein
                    foreach (var g in x.Gaenge)
                    {
                        Pruefe.Wahr(g.AufStapel < g.VomStapel, $"{jahr}: Stück {g.Stueck} erst auf, dann vom Stapel");
                        Pruefe.Gleich((double)x.Runde, g.Zurueck - (x.Entladen ? g.VomStapel : g.ImSchuppen), "eine Runde je Gang");
                    }
                    foreach (var platz in x.Gaenge.GroupBy(g => g.Platz))
                    {
                        var reihe = platz.OrderBy(g => g.Zurueck).ToList();
                        for (var i = 1; i < reihe.Count; i++)
                            Pruefe.Wahr((x.Entladen ? reihe[i].VomStapel : reihe[i].ImSchuppen) >= reihe[i - 1].Zurueck - 1e-9, $"{jahr}: Träger {platz.Key} ist nicht an zwei Stellen");
                    }
                    Pruefe.Wahr(x.Gaenge.Count(g => g.Platz == 0) >= 18 / x.Traeger, "die Gänge sind verteilt");
                    for (var m = anlegen - 60.0; m <= x.Ende + 60; m += 0.5)
                        groessterStapel = Math.Max(groessterStapel, x.StapelUm(m));
                    // vor dem Niederländer ist der Steg leer; für den Oberländer tragen sie schon vor dem Anlegen heraus
                    if (x.Entladen) Pruefe.Gleich(0, x.StapelUm(anlegen - 1), $"{jahr}: vor dem Anlegen ist der Steg leer");
                    else Pruefe.Wahr(x.StapelUm(anlegen - fahrtVorlauf) == 0, $"{jahr}: lange vor dem Anlegen steht noch nichts da");
                    Pruefe.Gleich(0, x.StapelUm(x.Ende), $"{jahr}: danach ist der Steg leer");
                    Pruefe.Gleich(x.Entladen ? 18 : 0, x.SchuppenUm(x.Ende), "die Ware im Stapelhaus");
                    if (x.Entladen) nachlauf = Math.Max(nachlauf, x.Ende - (anlegen + liegen));
                    else Pruefe.Wahr(x.Ende <= anlegen + liegen, $"{jahr}: Beladen ist vor dem Ablegen fertig");
                }
            }
            Pruefe.Wahr(zahlen.Count == 3, $"Mal 2, mal 3, mal 4 Träger ({string.Join(",", zahlen)})");
            Pruefe.Wahr(groessterStapel is > 0 and <= 18, $"Der Stapel ist Puffer (höchstens {groessterStapel} Stücke)");
            Pruefe.Wahr(nachlauf <= 60, $"Nach dem Ablegen tragen sie höchstens eine Stunde nach ({nachlauf:0} min)");
        }),

        ("Hafenarbeit: Während der Schicht sind n Tagelöhner und ein Vorarbeiter am Hafen (echte Einwohner), auch vor 8 Uhr; davor und danach niemand; jeden Tag andere Träger", () =>
        {
            var (sim, w) = Partie(Daten(), "koeln");
            var ha = w.Einwohnerschaft.Hafenarbeit;
            var traegerJeSchicht = new List<string>();
            var frueh = false;
            for (var tag = 0; tag < 3; tag++)
            {
                var jahr = Spielzeit.StartJahr + tag;
                foreach (var s in ha.Schichten(jahr))
                {
                    var stundenAnfang = (long)tag * Spielzeit.MinutenProTag + s.VonStunde * 60 - Spielzeit.TagesbeginnMinute;
                    if (stundenAnfang > sim.Zeit.VergangeneMinuten) BisMinute(sim, stundenAnfang - 1);
                    Pruefe.Gleich(0, w.Einwohnerschaft.Einwohner.Count(e => e.Ort == Stadtort.Hafen), $"{jahr}: vor der Schicht ist niemand am Hafen");
                    BisMinute(sim, (long)Math.Ceiling(s.Beginn));
                    if (s.VonStunde < 8) frueh = true;
                    var da = w.Einwohnerschaft.Einwohner.Where(e => e.Ort == Stadtort.Hafen).ToList();
                    var va = ha.Vorarbeiter;
                    Pruefe.Wahr(va is not null && Hafenarbeit.IstVorarbeiter(va), $"{jahr}: ein Vorarbeiter");
                    Pruefe.Gleich(s.Traeger + 1, da.Count, $"{jahr}: Träger und Vorarbeiter eingeteilt");
                    Pruefe.Wahr(AmHafen(w).Count >= Math.Min(3, s.Traeger + 1), $"{jahr}: die meisten sind schon da ({AmHafen(w).Count}) {s.Beginn} {sim.Zeit.UhrzeitText} " + string.Join(" | ", da.Select(e => e.Nummer + " " + e.Weg)));
                    Pruefe.Wahr(da.All(e => e.Rolle == "Tagelöhner" && e.Handlung is Handlung.Arbeiten or Handlung.Ruhen or Handlung.Flanieren or Handlung.Einkaufen),
                                "nur Tagelöhner, die nicht einkehren, beten oder einkaufen");
                    Pruefe.Wahr(da.All(e => e.Weg is { Nach.Ort: Stadtort.Hafen }), "mit Weg zum Hafen");
                    Pruefe.Wahr(da.All(Hafenarbeit.ArbeitetAmHafen), "arbeitet am Hafen");
                    Pruefe.Wahr(!ha.Traeger.Contains(va!), "der Vorarbeiter trägt nicht");
                    Pruefe.Wahr(da.Where(e => e != va).All(e => e.Geburtsjahr >= va!.Geburtsjahr), "der Vorarbeiter ist der Älteste");
                    Pruefe.Gleich(string.Join(",", da.Where(e => e != va).Select(e => e.Nummer).OrderBy(n => n)), string.Join(",", ha.Traeger.Select(e => e.Nummer).OrderBy(n => n)));
                    traegerJeSchicht.Add(string.Join(",", ha.Traeger.Select(e => e.Nummer)));
                    var vaWeg = va!.Weg!;
                    Pruefe.Wahr(da.Where(e => e != va && e.Weg!.Abmarsch == vaWeg.Abmarsch).All(e => e.Weg!.Ankunft >= vaWeg.Ankunft), "der Vorarbeiter kommt vor den Trägern seiner Stunde");
                    // nach der Schicht gehen sie zur nächsten vollen Stunde, der Vorarbeiter als Letzter
                    var stundeEnde = (long)tag * Spielzeit.MinutenProTag + s.BisStunde * 60 - Spielzeit.TagesbeginnMinute;
                    BisMinute(sim, stundeEnde - 1);
                    da = w.Einwohnerschaft.Einwohner.Where(e => e.Ort == Stadtort.Hafen).ToList();
                    va = ha.Vorarbeiter!;
                    BisMinute(sim, stundeEnde + 1);
                    Pruefe.Gleich(0, w.Einwohnerschaft.Einwohner.Count(e => e.Ort == Stadtort.Hafen), $"{jahr}: nach der Schicht trägt keiner mehr");
                    Pruefe.Wahr(da.All(e => e.Weg is { Von.Ort: Stadtort.Hafen }), "sie gehen vom Hafen weg");
                    Pruefe.Wahr(da.Where(e => e != va).All(e => e.Weg!.Abmarsch < va.Weg!.Abmarsch), "der Vorarbeiter geht als Letzter");
                    BisMinute(sim, stundeEnde + 60);
                    Pruefe.Wahr(da.All(e => e.Wo(sim.Zeit.VergangeneMinuten) is { } a && a.Ort != Stadtort.Hafen), "eine Stunde später sind alle fort");
                }
            }
            Pruefe.Wahr(frueh, "Morgens arbeiten sie vor 8 Uhr");
            Pruefe.Wahr(traegerJeSchicht.Count >= 4, $"{traegerJeSchicht.Count} Schichten");
            Pruefe.Wahr(traegerJeSchicht.Distinct().Count() == traegerJeSchicht.Count, "jede Schicht andere Träger: " + string.Join(" | ", traegerJeSchicht));
        }),

        ("Hafenarbeit berührt die Wirtschaft nicht – Köln und Greybox, drei Jahre mit und ohne: Vermögen aller Häuser und der Spielstand außer Ort und Weg gleich", () =>
        {
            foreach (var karte in new[] { "koeln", "greybox" })
            {
                var (simA, a) = Partie(Daten(mitHafenarbeit: false), karte, startwert: 4711);
                var (simB, b) = Partie(Daten(), karte, startwert: 4711);
                Pruefe.Wahr(b.Einwohnerschaft.Hafenarbeit.IstAktiv, $"{karte}: mit Hafenarbeit");
                var gesehen = 0;
                for (var jahr = 0; jahr < 3; jahr++)
                {
                    for (var minute = 0; minute < Spielzeit.MinutenProTag; minute++)
                    {
                        simA.Tick();
                        simB.Tick();
                        if (minute % 30 == 0) gesehen = Math.Max(gesehen, AmHafen(b).Count);
                        if (minute % 60 == 0) Pruefe.Gleich(0, AmHafen(a).Count);
                    }
                    for (var i = 0; i < a.Konten.Count; i++)
                        Pruefe.Gleich(a.Konten[i].Vermoegen, b.Konten[i].Vermoegen, $"{karte}, Jahr {jahr + 1}: Vermögen von {a.Konten[i].Inhaber}");
                    Pruefe.Gleich(OhneOrte(simA, a.Konten[0]), OhneOrte(simB, b.Konten[0]), $"{karte}, Jahr {jahr + 1}: Spielstand");
                }
                Pruefe.Wahr(gesehen >= 3, $"{karte}: mit Hafenarbeit standen Träger am Hafen");
            }
        }),

        ("Hafenarbeit: Solange getragen wird, sind in jeder Minute alle n Träger und der Vorarbeiter am Hafen (Ablösung wartet auf den Neuen) – Köln, sechs Tage", () =>
        {
            var (sim, w) = Partie(Daten(), "koeln", startwert: 99);
            var besuche = 0;
            for (var m = 0; m < 6 * Spielzeit.MinutenProTag; m++)
            {
                sim.Tick();
                var jetzt = sim.Zeit.VergangeneMinuten;
                foreach (var s in w.Einwohnerschaft.Hafenarbeit.Schichten(sim.Zeit.Jahr))
                {
                    if (jetzt < s.Beginn || jetzt > s.Ende) continue;
                    if (jetzt == (long)Math.Ceiling(s.Beginn)) besuche++;
                    var traeger = w.Einwohnerschaft.Einwohner.Count(e => e.Wo(jetzt) is { Ort: Stadtort.Hafen, Stelle: Hafenarbeit.StelleTraeger });
                    var vorarbeiter = w.Einwohnerschaft.Einwohner.Count(e => e.Wo(jetzt) is { Ort: Stadtort.Hafen, Stelle: Hafenarbeit.StelleVorarbeiter });
                    Pruefe.Wahr(traeger >= s.Traeger, $"{sim.Zeit.Jahr} {sim.Zeit.UhrzeitText}: {traeger} von {s.Traeger} Trägern da");
                    Pruefe.Wahr(vorarbeiter >= 1, $"{sim.Zeit.Jahr} {sim.Zeit.UhrzeitText}: kein Vorarbeiter da");
                }
            }
            Pruefe.Wahr(besuche >= 8, $"{besuche} Besuche mit Hafenarbeit");
        }),

        ("Hafenarbeit: Spielstand mitten in der Schicht geladen – dieselben Träger am Hafen, danach derselbe Lauf", () =>
        {
            var (sim, w) = Partie(Daten(), "koeln");
            var s = w.Einwohnerschaft.Hafenarbeit.Schichten(sim.Zeit.Jahr)[1];
            BisMinute(sim, (long)s.Beginn + 3);
            var (geladen, _) = Speicherung.Laden(Speicherung.Speichern(sim, w.Konten[0]), Stadtkarte.AusDatei(Datenpfad.Karte("koeln")));
            var g = geladen.Wirtschaft!;
            Pruefe.Gleich(string.Join(",", w.Einwohnerschaft.Hafenarbeit.Traeger.Select(e => e.Nummer)),
                          string.Join(",", g.Einwohnerschaft.Hafenarbeit.Traeger.Select(e => e.Nummer)), "Träger in derselben Reihenfolge");
            Pruefe.Gleich(AmHafen(w).Count, AmHafen(g).Count, "am Hafen");
            Pruefe.Gleich(s.Traeger + 1, g.Einwohnerschaft.Einwohner.Count(e => e.Ort == Stadtort.Hafen), "eingeteilt");
            Pruefe.Gleich(w.Einwohnerschaft.Hafenarbeit.Vorarbeiter?.Nummer, g.Einwohnerschaft.Hafenarbeit.Vorarbeiter?.Nummer, "derselbe Vorarbeiter");
            for (var m = 0; m < 3 * 60; m++)
            {
                sim.Tick();
                geladen.Tick();
            }
            Pruefe.Gleich(string.Join(",", w.Einwohnerschaft.Einwohner.Select(e => e.Aufenthalt)), string.Join(",", g.Einwohnerschaft.Einwohner.Select(e => e.Aufenthalt)));
        }),

        ("Hafenarbeit: ohne Abschnitt niemand am Hafen; Greybox hat sie genau, wenn sie einen Hafen hat", () =>
        {
            var (sim, w) = Partie(Daten(mitHafenarbeit: false), "koeln");
            Pruefe.Wahr(!w.Einwohnerschaft.Hafenarbeit.IstAktiv, "ohne Abschnitt keine Hafenarbeit");
            Pruefe.Gleich(0, w.Einwohnerschaft.Hafenarbeit.Schichten(sim.Zeit.Jahr).Count);
            var (simG, g) = Partie(Daten(), "greybox");
            var mitHafen = g.Schiffahrt.Haefen.Count > 0 && g.Schiffahrt.Ruht;
            Pruefe.Gleich(mitHafen, g.Einwohnerschaft.Hafenarbeit.IstAktiv, "Greybox: aktiv genau mit Hafen");
            for (var m = 0; m < Spielzeit.MinutenProTag; m++)
            {
                sim.Tick();
                if (m % 60 == 0) Pruefe.Gleich(0, AmHafen(w).Count);
            }
        }),

        ("Hafenarbeit: Daten werden geprüft (Rolle vorhanden und am Markt, Zahlen im Rahmen, Schichten passen in den Spieltag)", () =>
        {
            void Falsch(Action<JsonObject> aendern)
            {
                var json = JsonNode.Parse(Text.Value)!.AsObject();
                aendern(json["buerger"]!["hafenarbeit"]!.AsObject());
                Pruefe.Wirft(() => Wirtschaftsdaten.AusJson(json.ToJsonString()));
            }
            Falsch(h => h["rolle"] = "Fährmann");
            Falsch(h => h["traegerVon"] = 0);
            Falsch(h => h["traegerBis"] = 1);
            Falsch(h => h["stuecke"] = 0);
            Falsch(h => h["runde"] = 31);
            Falsch(h => { h["traegerVon"] = 1; h["traegerBis"] = 1; h["runde"] = 30; h["stuecke"] = 40; });
        }),
    };

    /// <summary>Der Spielstand ohne Daten und ohne die Orte und Wege der Einwohner – alles, was die Wirtschaft ausmacht.</summary>
    static string OhneOrte(Simulation sim, Konto spieler)
    {
        var json = JsonNode.Parse(Speicherung.Speichern(sim, spieler, new DateTime(2026, 11, 6)))!.AsObject();
        json.Remove("Daten");
        json.Remove("daten");
        void Weg(JsonNode? n)
        {
            if (n is JsonObject o)
            {
                foreach (var k in new[] { "Ort", "Weg", "Betrieb", "Haus", "Stelle" })
                    if (o.ContainsKey("Handlung")) o.Remove(k); // nur bei Einwohnern (die haben eine Handlung)
                foreach (var (_, kind) in o) Weg(kind);
            }
            else if (n is JsonArray a)
                foreach (var kind in a) Weg(kind);
        }
        Weg(json);
        return json.ToJsonString();
    }
}
