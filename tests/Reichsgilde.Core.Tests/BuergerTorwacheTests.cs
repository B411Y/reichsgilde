using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Torwache der Stadt (Spiel-KI, P1 „Wachen und Diebe“, 5.11.2026): je Tor zwei Mannschaften zu je zwei Wächtern, Wechsel um 14 Uhr,
/// ab 20 Uhr einer. Echte Figuren mit Namen, aber ohne Gewicht für die Wirtschaft – die Partie läuft mit und ohne Torwache bitgleich.
/// </summary>
static class BuergerTorwacheTests
{
    static readonly Lazy<string> Text = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));

    static Wirtschaftsdaten Daten(bool mitTorwache = true)
    {
        var json = JsonNode.Parse(Text.Value)!.AsObject();
        if (!mitTorwache) json["buerger"]!.AsObject().Remove("torwache");
        return Wirtschaftsdaten.AusJson(json.ToJsonString());
    }

    static (Simulation Sim, Stadtwirtschaft W) Partie(Wirtschaftsdaten daten, string? karte, int startwert = 1400)
    {
        var rivalen = karte is null ? 0 : JsonNode.Parse(File.ReadAllText(Datenpfad.Karte(karte)))!["start"]!["haeuser"]!.AsArray().Count;
        var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos },
            daten, rivalen > 0 ? new NeuePartie(Rivalen: rivalen) : new NeuePartie(), karte: karte is null ? null : Stadtkarte.AusDatei(Datenpfad.Karte(karte)));
        sim.Tick(); // die Einwohner und die Torwache ziehen ein
        return (sim, sim.Wirtschaft!);
    }

    static void Bis(Simulation sim, int uhrzeit)
    {
        while (sim.Zeit.UhrzeitInMinuten != uhrzeit) sim.Tick();
    }

    static int Zahl(Torwache tw, string tor, Wachdienst dienst) => tw.Waechter.Count(t => t.Tor == tor && tw.DienstJetzt(t).Dienst == dienst);

    /// <summary>Der Spielstand ohne Daten, Zeitstempel und Torwache – alles, was die Wirtschaft ausmacht.</summary>
    static string OhneTorwache(Simulation sim, Konto spieler)
    {
        var json = JsonNode.Parse(Speicherung.Speichern(sim, spieler, new DateTime(2026, 11, 5)))!.AsObject();
        json.Remove("Daten");
        json.Remove("daten");
        void Weg(JsonNode? n)
        {
            if (n is JsonObject o)
            {
                o.Remove("Torwache");
                o.Remove("torwache");
                foreach (var (_, kind) in o) Weg(kind);
            }
            else if (n is JsonArray a)
                foreach (var kind in a) Weg(kind);
        }
        Weg(json);
        return json.ToJsonString();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Torwache: je Tor zwei Mannschaften zu je zwei Wächtern (Köln 10 Tore = 40, Greybox 3 Tore = 12); ohne Karte oder ohne Daten keine", () =>
        {
            var (_, koeln) = Partie(Daten(), "koeln");
            var (_, greybox) = Partie(Daten(), "greybox");
            Pruefe.Gleich(40, koeln.Einwohnerschaft.Torwache.Waechter.Count);
            Pruefe.Gleich(12, greybox.Einwohnerschaft.Torwache.Waechter.Count);
            Pruefe.Gleich(10, koeln.Einwohnerschaft.Torwache.Waechter.Select(t => t.Tor).Distinct().Count());
            var (_, ohneKarte) = Partie(Daten(), karte: null);
            Pruefe.Gleich(0, ohneKarte.Einwohnerschaft.Torwache.Waechter.Count);
            var (_, ohneDaten) = Partie(Daten(mitTorwache: false), "koeln");
            Pruefe.Gleich(0, ohneDaten.Einwohnerschaft.Torwache.Waechter.Count);
            Pruefe.Wahr(!ohneDaten.Einwohnerschaft.Torwache.IstAktiv, "ohne Daten keine Torwache");
        }),

        ("Torwache: echte Einwohner mit Namen und eigener Nummer, nicht in der Liste der Einwohner, Rolle Torwächter(in), per Nummer auffindbar", () =>
        {
            var (_, w) = Partie(Daten(), "koeln");
            var ew = w.Einwohnerschaft;
            var namen = new HashSet<string>();
            var haeuser = w.Konten.Select(k => w.Familien.Von(k)?.Nachname).ToHashSet();
            foreach (var t in ew.Torwache.Waechter)
            {
                var e = t.Person;
                Pruefe.Wahr(e.Nummer >= Torwache.ErsteNummer, $"Nummer {e.Nummer}");
                Pruefe.Gleich(Herkunft.Torwache, e.Herkunft);
                Pruefe.Gleich(TorwacheDaten.Rolle, e.Rolle);
                Pruefe.Wahr(namen.Add(e.Name), $"Name doppelt: {e.Name}");
                Pruefe.Wahr(!(haeuser.Contains(e.Familienname)), $"{e.Name} trägt den Namen eines Hauses");
                Pruefe.Wahr(!(ew.Einwohner.Contains(e)), $"{e.Name} steht in der Einwohnerliste");
                Pruefe.Wahr(ReferenceEquals(e, ew.Nummer(e.Nummer)), $"{e.Name} per Nummer");
                Pruefe.Wahr(e.Alter(w.Uhr.Jahr) >= 16, $"{e.Name} ist zu jung");
                Pruefe.Gleich(e.Geschlecht == Reichsgilde.Core.Dynastie.Geschlecht.Frau ? "Torwächterin" : "Torwächter", ew.RollenName(e));
            }
            Pruefe.Wahr(!(ew.Einwohner.Any(e => e.Rolle == TorwacheDaten.Rolle || e.Herkunft == Herkunft.Torwache)), "Kein gewöhnlicher Einwohner ist Torwächter");
            Pruefe.Wahr(ew.Torwache.Waechter.Select(t => t.Person.Volk).Distinct().Count() >= 2, "Mehrere Völker in der Torwache");
        }),

        ("Torwache: Schichtplan – am Vormittag 2 je Tor, vor 14 Uhr kommt die Spätschicht, danach geht die Frühschicht, ab 20 Uhr einer, am nächsten Tag tauschen die Mannschaften", () =>
        {
            var (sim, w) = Partie(Daten(), "greybox");
            var tw = w.Einwohnerschaft.Torwache;
            var tor = tw.Waechter[0].Tor;
            var frueh = Torwache.FruehMannschaft(sim.Zeit.Jahr);
            Bis(sim, 4 * 60 + 1);
            Pruefe.Gleich(2, Zahl(tw, tor, Wachdienst.Posten), "4 Uhr");
            Bis(sim, 10 * 60);
            Pruefe.Gleich(2, Zahl(tw, tor, Wachdienst.Posten), "10 Uhr");
            Pruefe.Wahr(tw.Waechter.Where(t => t.Tor == tor && tw.DienstJetzt(t).Dienst == Wachdienst.Posten).All(t => t.Mannschaft == frueh), "Frühschicht");
            Pruefe.Gleich(Stadtort.Tor, tw.Waechter.First(t => t.Tor == tor && t.Mannschaft == frueh).Person.Ort);
            Bis(sim, 13 * 60 + 50);
            Pruefe.Gleich(2, Zahl(tw, tor, Wachdienst.Posten), "13:50 Posten");
            Pruefe.Gleich(2, Zahl(tw, tor, Wachdienst.Anmarsch), "13:50 im Anmarsch");
            Bis(sim, 14 * 60 + 5);
            Pruefe.Gleich(2, Zahl(tw, tor, Wachdienst.Posten), "14:05 Posten");
            Pruefe.Wahr(Zahl(tw, tor, Wachdienst.Abmarsch) >= 1, "14:05 im Abmarsch");
            Pruefe.Wahr(tw.Waechter.Where(t => t.Tor == tor && tw.DienstJetzt(t).Dienst == Wachdienst.Posten).All(t => t.Mannschaft != frueh), "Spätschicht");
            Bis(sim, 15 * 60);
            Pruefe.Gleich(2, Zahl(tw, tor, Wachdienst.Frei), "15 Uhr: Frühschicht daheim");
            Pruefe.Gleich(Stadtort.Heim, tw.Waechter.First(t => t.Tor == tor && t.Mannschaft == frueh).Person.Ort);
            Bis(sim, 21 * 60);
            Pruefe.Gleich(1, Zahl(tw, tor, Wachdienst.Posten), "21 Uhr");
            Bis(sim, 22 * 60 + 59);
            Pruefe.Gleich(1, Zahl(tw, tor, Wachdienst.Posten), "22:59");
            Bis(sim, 5 * 60);
            Pruefe.Gleich(2, Zahl(tw, tor, Wachdienst.Posten), "Nächster Tag 5 Uhr");
            Pruefe.Wahr(tw.Waechter.Where(t => t.Tor == tor && tw.DienstJetzt(t).Dienst == Wachdienst.Posten).All(t => t.Mannschaft != frueh), "Die Mannschaften tauschen");
        }),

        ("Torwache: Der Weg zum Wechsel läuft von 0 bis 1, je Platz ein paar Minuten versetzt", () =>
        {
            var (_, w) = Partie(Daten(), "greybox");
            var tw = w.Einwohnerschaft.Torwache;
            var tag = 1401;
            var spaet = tw.Waechter.Where(t => t.Mannschaft != Torwache.FruehMannschaft(tag)).Take(2).ToList();
            var (d0, a0) = tw.Dienst(spaet[0], 13 * 60 + 45, tag);
            Pruefe.Gleich(Wachdienst.Anmarsch, d0);
            Pruefe.Gleich(0.0, a0);
            var (d1, a1) = tw.Dienst(spaet[0], 13 * 60 + 52.5, tag);
            Pruefe.Gleich(Wachdienst.Anmarsch, d1);
            Pruefe.Gleich(0.5, a1);
            Pruefe.Gleich(Wachdienst.Frei, tw.Dienst(spaet[1], 13 * 60 + 46, tag).Dienst, "der zweite bricht später auf");
            Pruefe.Gleich(Wachdienst.Posten, tw.Dienst(spaet[0], 14 * 60, tag).Dienst);
        }),

        ("Torwache berührt die Wirtschaft nicht – Köln und Greybox, drei Jahre mit und ohne: der ganze Spielstand außer der Torwache gleich", () =>
        {
            foreach (var karte in new[] { "koeln", "greybox" })
            {
                var (simA, a) = Partie(Daten(mitTorwache: false), karte, startwert: 4711);
                var (simB, b) = Partie(Daten(), karte, startwert: 4711);
                Pruefe.Wahr(b.Einwohnerschaft.Torwache.Waechter.Count > 0, $"{karte}: mit Torwache");
                for (var jahr = 0; jahr < 3; jahr++)
                {
                    for (var minute = 0; minute < Spielzeit.MinutenProTag; minute++)
                    {
                        simA.Tick();
                        simB.Tick();
                    }
                    for (var i = 0; i < a.Konten.Count; i++)
                        Pruefe.Gleich(a.Konten[i].Vermoegen, b.Konten[i].Vermoegen, $"{karte}, Jahr {jahr + 1}: Vermögen von {a.Konten[i].Inhaber}");
                    Pruefe.Gleich(OhneTorwache(simA, a.Konten[0]), OhneTorwache(simB, b.Konten[0]), $"{karte}, Jahr {jahr + 1}: Spielstand");
                }
            }
        }),

        ("Torwache: Spielstand geladen – dieselben Wächter, derselbe Dienst; nach Jahren gehen Alte in den Ruhestand", () =>
        {
            var (sim, w) = Partie(Daten(), "koeln");
            Bis(sim, 13 * 60 + 50);
            var (geladen, _) = Speicherung.Laden(Speicherung.Speichern(sim, w.Konten[0]), Stadtkarte.AusDatei(Datenpfad.Karte("koeln")));
            var a = w.Einwohnerschaft.Torwache;
            var b = geladen.Wirtschaft!.Einwohnerschaft.Torwache;
            Pruefe.Gleich(a.Waechter.Count, b.Waechter.Count);
            for (var i = 0; i < a.Waechter.Count; i++)
            {
                Pruefe.Gleich(a.Waechter[i].Person.Name, b.Waechter[i].Person.Name);
                Pruefe.Gleich(a.Waechter[i].Person.Nummer, b.Waechter[i].Person.Nummer);
                Pruefe.Gleich(a.Waechter[i].Tor, b.Waechter[i].Tor);
                Pruefe.Gleich(a.DienstJetzt(a.Waechter[i]), b.DienstJetzt(b.Waechter[i]));
            }
            var namen = a.Waechter.Select(t => t.Person.Name).ToList();
            for (var jahr = 0; jahr < 30; jahr++)
                for (var minute = 0; minute < Spielzeit.MinutenProTag; minute++)
                {
                    sim.Tick();
                    geladen.Tick();
                }
            Pruefe.Wahr(a.Waechter.Count(t => !namen.Contains(t.Person.Name)) >= 5, "Nach 30 Jahren sollten neue Wächter da sein");
            Pruefe.Wahr(a.Waechter.All(t => t.Person.Alter(sim.Zeit.Jahr) < 60 || t.Person.Volk == Volk.Elb), "Keine Greise am Tor");
            for (var i = 0; i < a.Waechter.Count; i++)
                Pruefe.Gleich(a.Waechter[i].Person.Name, b.Waechter[i].Person.Name, "auch nach dem Laden dieselben Neuen");
            Pruefe.Wahr(a.Waechter.Select(t => t.Person.Nummer).Distinct().Count() == a.Waechter.Count, "Nummern eindeutig");
        }),

        ("Torwache: Daten werden geprüft (Rolle mit Gewicht 0, Zeiten in der richtigen Reihenfolge)", () =>
        {
            var json = JsonNode.Parse(Text.Value)!.AsObject();
            json["buerger"]!["torwache"]!["nachtAb"] = 12;
            Pruefe.Wirft(() => Wirtschaftsdaten.AusJson(json.ToJsonString()));
            json = JsonNode.Parse(Text.Value)!.AsObject();
            json["buerger"]!["rollen"]!["Torwächter"]!["gewicht"] = 1;
            Pruefe.Wirft(() => Wirtschaftsdaten.AusJson(json.ToJsonString()));
        }),
    };
}
