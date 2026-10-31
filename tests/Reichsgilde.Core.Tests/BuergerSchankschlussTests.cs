using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Schankschluss: Das Wirtshaus schenkt bis 22 Uhr aus (<see cref="Gastwirtschaft.SchliesstUm"/>). Danach gehen die Gäste heim,
/// und in der letzten Stunde des Spieltags sitzt niemand mehr in einer Schänke.
///
/// Anlass: Eine Sonde zählte „114–116 von 123 Einwohnern 22–23 Uhr in der Schänke“. Sie hatte <see cref="Einwohner.Wo"/> mit der
/// Uhrzeit (<see cref="Spielzeit.UhrzeitInMinuten"/>) statt mit den vergangenen Minuten aufgerufen – ab dem zweiten Tag liegt die Uhrzeit
/// immer vor dem Abmarsch, also kam der Startort des letzten Weges heraus (die Schänke, aus der alle heimgingen). Der Kern war richtig;
/// dieser Test hält das fest, damit es so bleibt.
/// </summary>
static class BuergerSchankschlussTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Wirtschaftsdaten Abgewandelt(Action<JsonObject> aendern)
    {
        var json = JsonNode.Parse(File.ReadAllText(Datenpfad.Wirtschaft()), documentOptions: new() { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!.AsObject();
        aendern(json);
        return Wirtschaftsdaten.AusJson(json.ToJsonString());
    }

    /// <summary>Bis die Uhr genau diese Uhrzeit zeigt (Minuten nach Mitternacht) – im laufenden Spieltag.</summary>
    static void Bis(Simulation sim, int uhrzeit)
    {
        while (sim.Zeit.UhrzeitInMinuten != uhrzeit) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Bürger-KI: Nach Schankschluss (22 Uhr) gehen die Gäste heim – kurz danach ist jede Schänke leer, vor 23 Uhr sind alle daheim (Wirt, Startwerte 1400 und 99, drei Tage)", () =>
        {
            foreach (var startwert in new[] { 1400, 99 })
            {
                var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos },
                    Daten.Value, new NeuePartie(Beruf: "Wirt")); // der Spieler als Wirt: es gibt eine Schänke
                var w = sim.Wirtschaft!;
                var ew = w.Einwohnerschaft;
                var wege = ew.Daten.Wege;
                var schluss = Gastwirtschaft.SchliesstUm * 60;
                var alleWeg = schluss + wege.Verzoegerung + 1;                        // spätester Abmarsch: Verzögerung nach der vollen Stunde
                var alleDaheim = alleWeg + wege.Stadt + wege.Streuung;                // längster Weg in der Stadt (Schänke → Wohnhaus)
                Pruefe.Wahr(alleDaheim < Spielzeit.TagesendeMinute, "Der Heimweg muss vor 23 Uhr zu Ende sein (Daten der Wege)");
                for (var tag = 1; tag <= 3; tag++)
                {
                    var hinweis = $"Startwert {startwert}, Tag {tag}";
                    Bis(sim, schluss - 15); // 21:45: der Abend in der Schänke
                    var vorher = ew.Am(Stadtort.Schaenke).Count();
                    Pruefe.Wahr(vorher >= 20, $"{hinweis}: Um 21:45 sollten viele in der Schänke sitzen (waren {vorher}) – sonst prüft der Test nichts");

                    Bis(sim, schluss + 1); // die Planung um 22:00 ist gelaufen
                    Pruefe.Wahr(ew.Einwohner.All(e => e.Handlung == Handlung.Ruhen && e.Ort == Stadtort.Heim && e.Betrieb is null),
                                $"{hinweis}: Ab 22 Uhr wählen alle Ruhen daheim");
                    foreach (var b in w.Gastwirtschaft.Schaenken)
                        Pruefe.Gleich(0, ew.Einkehrende(b), $"{hinweis}: Einkehrende in {b.Name} nach Schankschluss");
                    // Wer jetzt geht, kommt aus der Schänke und geht in sein eigenes Haus.
                    foreach (var e in ew.Einwohner.Where(e => e.Weg is { } g && g.Abmarsch >= sim.Zeit.VergangeneMinuten - 1))
                    {
                        Pruefe.Gleich(Stadtort.Schaenke, e.Weg!.Von.Ort, $"{hinweis}: Startort von {e.Name} um 22 Uhr");
                        Pruefe.Gleich(new Aufenthalt(Stadtort.Heim, null, e.Wohnhaus, null), e.Weg.Nach, $"{hinweis}: Ziel von {e.Name} um 22 Uhr");
                    }

                    Bis(sim, alleWeg);
                    Pruefe.Gleich(0, ew.Am(Stadtort.Schaenke).Count(), $"{hinweis}: Um {alleWeg / 60}:{alleWeg % 60:00} sitzt noch jemand in der Schänke");

                    Bis(sim, alleDaheim);
                    var jetzt = sim.Zeit.VergangeneMinuten;
                    Pruefe.Gleich(0, ew.Unterwegs.Count(), $"{hinweis}: Um {alleDaheim / 60}:{alleDaheim % 60:00} ist noch jemand unterwegs");
                    Pruefe.Wahr(ew.Einwohner.All(e => e.Wo(jetzt) is { Ort: Stadtort.Heim } a && a.Haus == e.Wohnhaus),
                                $"{hinweis}: Um {alleDaheim / 60}:{alleDaheim % 60:00} sind alle in ihrem Haus");

                    // Bis zum Ende des Spieltags bleibt die Schänke leer.
                    while (sim.Zeit.UhrzeitInMinuten < Spielzeit.TagesendeMinute - 1)
                    {
                        sim.Tick();
                        Pruefe.Gleich(0, ew.Am(Stadtort.Schaenke).Count(), $"{hinweis}: um {sim.Zeit.UhrzeitText} in der Schänke");
                    }
                    sim.Tick(); // Jahresende: alle schlafen aus
                }
            }
        }),

        ("Bürger-KI: Datenprüfung – Einkehren nicht über den Schankschluss hinaus (handlungen.Einkehren.bis ≤ 22)", () =>
        {
            Pruefe.WirftMitMeldung(() => Abgewandelt(j => j["buerger"]!["handlungen"]!["Einkehren"]!["bis"] = Gastwirtschaft.SchliesstUm + 1), "Schankschluss");
            Abgewandelt(j => j["buerger"]!["handlungen"]!["Einkehren"]!["bis"] = Gastwirtschaft.SchliesstUm - 1); // früher heimgehen ist erlaubt
        }),
    };
}
