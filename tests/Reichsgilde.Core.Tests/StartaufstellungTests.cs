using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Die Startaufstellung (Nutzer, 11.11.2026, <see cref="Startaufstellung"/>): jeder Beruf höchstens einmal je Partie, Beruf, Volk und Glaube der
/// KI-Häuser gelost (deterministisch aus dem Startwert), Glaubensliste je Volk, Mehrspieler-Regel „wer zuerst wählt“, Notausweg.
/// </summary>
static class StartaufstellungTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));
    static readonly Lazy<Stadtkarte> Greybox = new(() => Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));
    static readonly Lazy<Stadtkarte> Messstadt = new(() => Stadtkarte.AusDatei(Datenpfad.Karte("messstadt")));
    static readonly Lazy<Stadtkarte> Koeln = new(() => Stadtkarte.AusDatei(Datenpfad.Karte("koeln")));

    static Stadtwirtschaft Partie(Stadtkarte? karte, NeuePartie neu, int startwert, Schwierigkeit s = Schwierigkeit.Normal) =>
        Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = s },
            Daten.Value, neu, karte: karte).Simulation.Wirtschaft!;

    /// <summary>Die Berufe der Häuser (je Haus die Berufe seiner Betriebe, ohne Doppel im Haus), in Kontoreihenfolge.</summary>
    static List<string> BerufeDerHaeuser(Stadtwirtschaft w) =>
        w.Konten.SelectMany(k => w.Betriebe.Where(b => b.Konto == k).Select(b => w.Daten.Art(b.Art).Beruf).Distinct()).ToList();

    static bool OhneDoppel(IReadOnlyList<string> berufe) => berufe.Count == berufe.Distinct().Count();

    /// <summary>Was die Aufstellung festlegt, als Text: je KI-Haus Volk, Glaube und Startbetriebe.</summary>
    static string Bild(Startaufstellung.Ergebnis e) =>
        string.Join("|", e.Belegung.Haeuser.Select(h => $"{h.Name}:{h.Volk}:{h.Glaube}:" + string.Join(",", h.Betriebe.Select(b => $"{b.Art}@{b.Platz}"))))
        + "#" + string.Join(",", e.Frei.Select(f => f.Name));

    static Startaufstellung.Ergebnis Losen(Stadtkarte? karte, string beruf, int haeuser, int startwert)
    {
        var start = karte?.Startbelegung ?? Startbelegung.Ersatz;
        return Startaufstellung.Losen(karte, start, Daten.Value, beruf, Konfession.Katholisch, start.Haeuser.Take(haeuser).ToList(), startwert);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Startaufstellung: jeder Beruf höchstens einmal – 200 Startwerte × 2 bis 8 Häuser (Messstadt, Köln, Greybox, ohne Karte), jeder Spielerberuf; Gegenprobe ohne Regel Doppelungen", () =>
        {
            var d = Daten.Value;
            var berufe = Startaufstellung.Berufe(d);
            Pruefe.Gleich(10, berufe.Count, string.Join(", ", berufe));
            var gezogen = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var (karte, max) in new (Stadtkarte?, int)[] { (Messstadt.Value, 8), (Koeln.Value, 7), (Greybox.Value, 3), (null, 3) })
                for (var s = 1; s <= 200; s++)
                {
                    var spieler = berufe[s % berufe.Count];
                    var n = 1 + s % max;
                    var e = Losen(karte, spieler, n, s);
                    Pruefe.Gleich(n, e.Belegung.Haeuser.Count, $"Startwert {s}: alle Häuser spielen mit");
                    Pruefe.Wahr(e.Meldung is null, e.Meldung ?? "");
                    var alle = e.Belegung.Haeuser.Select(h => d.Art(h.Betriebe[0].Art).Beruf).Prepend(spieler).ToList();
                    Pruefe.Wahr(OhneDoppel(alle), $"Startwert {s}, {karte?.Kennung ?? "ohne Karte"}: {string.Join(", ", alle)}");
                    // ein Haus hat nur Betriebe seines einen Berufs
                    foreach (var h in e.Belegung.Haeuser)
                        Pruefe.Gleich(1, h.Betriebe.Select(b => d.Art(b.Art).Beruf).Distinct().Count(), h.Name);
                    foreach (var b in alle.Skip(1)) gezogen.Add(b);
                }
            Pruefe.Gleich(berufe.Count, gezogen.Count, "jeder Beruf kommt bei einem KI-Haus vor: " + string.Join(", ", gezogen));

            // Ganze Partien (Spieler und KI-Häuser mit ihren Betrieben, „Schwer“: auch das Umland), Messstadt mit acht KI-Häusern
            for (var s = 1; s <= 10; s++)
            {
                var w = Partie(Messstadt.Value, new NeuePartie(Beruf: berufe[s % berufe.Count], Rivalen: 8), s, Schwierigkeit.Schwer);
                Pruefe.Gleich(9, w.Konten.Count);
                Pruefe.Wahr(OhneDoppel(BerufeDerHaeuser(w)), $"Startwert {s}: {string.Join(", ", BerufeDerHaeuser(w))}");
            }
            // Gegenprobe: ohne die Regel (Vorgabe = Karte wie geschrieben) beginnen zwei Häuser als Schmiede
            var v = Partie(Messstadt.Value, new NeuePartie(Rivalen: 8, Aufstellung: Aufstellungsart.Vorgabe), 1);
            Pruefe.Wahr(!OhneDoppel(BerufeDerHaeuser(v)), "ohne Regel Doppelungen: " + string.Join(", ", BerufeDerHaeuser(v)));
        }),

        ("Startaufstellung: deterministisch – gleicher Startwert, gleiche Aufstellung (Volk, Glaube, Betriebe, Plätze); andere Startwerte losen anders", () =>
        {
            string Partiebild(int s)
            {
                var w = Partie(Koeln.Value, new NeuePartie(Rivalen: 7), s, Schwierigkeit.Schwer);
                return string.Join("|", w.Konten.Select(k => $"{k.Inhaber}:{w.Familien.Von(k)!.Oberhaupt.Volk}:{w.Familien.Von(k)!.Oberhaupt.Glaube}:"
                    + string.Join(",", w.Betriebe.Where(b => b.Konto == k).Select(b => $"{b.Name}/{b.Art}/{w.Karte!.PlatzVon(b.Name)?.Kennung}"))));
            }
            Pruefe.Gleich(Partiebild(1400), Partiebild(1400));
            Pruefe.Gleich(Bild(Losen(Messstadt.Value, "Schmied", 8, 77)), Bild(Losen(Messstadt.Value, "Schmied", 8, 77)));
            var bilder = Enumerable.Range(1, 20).Select(s => Bild(Losen(Messstadt.Value, "Schmied", 8, s))).Distinct().Count();
            Pruefe.Wahr(bilder >= 19, $"nur {bilder} verschiedene Aufstellungen aus 20 Startwerten");
            // Die übrigen Würfe der Partie hängen nicht am Los (eigene Zufallsquelle): Das Oberhaupt des Spielers (vor den KI-Häusern gegründet) ist
            // mit und ohne Aufstellung dasselbe
            string Oberhaupt(Aufstellungsart a)
            {
                var w = Partie(Greybox.Value, new NeuePartie(Aufstellung: a), 5);
                var o = w.Familien.Von(w.Konten[0])!.Oberhaupt;
                return $"{o.Vorname} {o.Geburtsjahr} {o.Aussehen}";
            }
            Pruefe.Gleich(Oberhaupt(Aufstellungsart.Vorgabe), Oberhaupt(Aufstellungsart.Gelost));
        }),

        ("Startaufstellung: Glaube je Volk – Naturglaube nur bei Orks und Ogern (und sie nur mit ihm), Menschen nie altahtnisch; KI-Häuser haben Bürgerrecht", () =>
        {
            foreach (var volk in Enum.GetValues<Volk>())
            {
                var g = Startaufstellung.Startglauben(volk);
                Pruefe.Wahr(g.Count > 0 && g.Distinct().Count() == g.Count, $"{volk}: {string.Join(", ", g)}");
                if (volk is Volk.Ork or Volk.Oger)
                    Pruefe.Wahr(g.SequenceEqual(new[] { Konfession.Naturglaube }), $"{volk}: nur Naturglaube");
                else
                    Pruefe.Wahr(!g.Contains(Konfession.Naturglaube), $"{volk}: kein Naturglaube");
                // Platzhalter fürs Ahtnentum (Prediger-PR 5): Kommt eine Konfession „Altahtnisch“, darf sie bei Menschen nicht stehen (nur „Neuahtnisch“).
                if (volk == Volk.Mensch)
                    Pruefe.Wahr(!g.Any(k => k.ToString().StartsWith("Altahtn", StringComparison.Ordinal)), "Menschen nie altahtnisch");
            }
            Pruefe.Wahr(!Startaufstellung.KiVoelker.Contains(Volk.Ork) && !Startaufstellung.KiVoelker.Contains(Volk.Oger), "Orks und Oger haben kein Bürgerrecht");

            var d = Daten.Value;
            var voelker = new SortedSet<Volk>();
            var glauben = new SortedSet<Konfession>();
            for (var s = 1; s <= 200; s++)
                foreach (var h in Losen(Messstadt.Value, "Schmied", 8, s).Belegung.Haeuser)
                {
                    Pruefe.Wahr(Startaufstellung.KiVoelker.Contains(h.Volk), h.Volk.ToString());
                    Pruefe.Wahr(h.Glaube is { } g && Startaufstellung.Startglauben(h.Volk).Contains(g), $"{h.Volk} {h.Glaube}");
                    voelker.Add(h.Volk);
                    glauben.Add(h.Glaube!.Value);
                }
            Pruefe.Gleich(4, voelker.Count, "alle vier Völker kommen vor");
            Pruefe.Wahr(glauben.SetEquals(new[] { Konfession.Katholisch, Konfession.Evangelisch }), string.Join(", ", glauben));

            // In der Partie trägt das Oberhaupt Volk und Glauben der Aufstellung
            var w = Partie(Greybox.Value, new NeuePartie(), 9);
            var e = Losen(Greybox.Value, "Schmied", 3, 9);
            foreach (var h in e.Belegung.Haeuser)
            {
                var o = w.Familien.Von(w.Konten.Single(k => k.Inhaber == h.Name))!.Oberhaupt;
                Pruefe.Gleich(h.Volk, o.Volk, h.Name);
                Pruefe.Gleich(h.Glaube, o.Glaube, h.Name);
            }
        }),

        ("Startaufstellung: Mehrspieler – wer zuerst wählt, bekommt den Beruf; FreieBerufe ohne die vergebenen, ein zweiter Griff wird abgewiesen", () =>
        {
            var d = Daten.Value;
            var vergeben = new List<string>();
            Pruefe.Gleich(10, Startaufstellung.FreieBerufe(d, vergeben).Count);
            Pruefe.Gleich(null, Startaufstellung.BerufVergeben(d, vergeben, "Schmied"), "der erste bekommt ihn");
            var grund = Startaufstellung.BerufVergeben(d, vergeben, "Schmied");
            Pruefe.Wahr(grund is not null && grund.Contains("schon"), grund ?? "");
            Pruefe.Wahr(!Startaufstellung.FreieBerufe(d, vergeben).Contains("Schmied"), "gesperrt");
            Pruefe.Gleich(9, Startaufstellung.FreieBerufe(d, vergeben).Count);
            Pruefe.Gleich(null, Startaufstellung.BerufVergeben(d, vergeben, "Prediger"));
            Pruefe.Wahr(Startaufstellung.BerufVergeben(d, vergeben, "Bäcker")!.Contains("gibt es nicht"), "unbekannt");
            Pruefe.Wahr(vergeben.SequenceEqual(new[] { "Schmied", "Prediger" }), string.Join(", ", vergeben));
            // Die KI-Häuser losen nur aus den freien Berufen
            for (var s = 1; s <= 50; s++)
                Pruefe.Wahr(Losen(Messstadt.Value, "Prediger", 8, s).Belegung.Haeuser.All(h => d.Art(h.Betriebe[0].Art).Beruf != "Prediger"), $"Startwert {s}");
        }),

        ("Startaufstellung: Plätze – neue Werkstatt auf dem Werkstattplatz des Hauses, Umland auf einem freien Umland-Startplatz (Name des Hauses); ungenutzte stehen frei zur Pacht", () =>
        {
            var d = Daten.Value;
            var gepruefte = 0;
            for (var s = 1; s <= 40; s++)
            {
                var e = Losen(Messstadt.Value, "Wirt", 8, s);
                var karte = e.Karte!;
                var original = Messstadt.Value.Startbelegung.Haeuser;
                foreach (var h in e.Belegung.Haeuser)
                {
                    var alt = original.Single(o => o.Name == h.Name);
                    Pruefe.Gleich(alt.Betriebe[0].Platz, h.Betriebe[0].Platz, $"{h.Name}: Werkstatt auf ihrem Platz");
                    Pruefe.Wahr(karte.GebaeudeMit(h.Betriebe[0].Platz) is { Art: Bausteinart.Werkstattplatz }, h.Name);
                    var beruf = d.Berufe[d.Art(h.Betriebe[0].Art).Beruf];
                    if (h.Betriebe.Count > 1)
                    {
                        var u = h.Betriebe[1];
                        Pruefe.Gleich(beruf.Umland, u.Art, h.Name);
                        Pruefe.Wahr(karte.GebaeudeMit(u.Platz) is { Art: Bausteinart.Umlandbetrieb } g && g.Baustein!.BetriebsartWert == u.Art, $"{h.Name}: {u.Platz}");
                        if (!alt.Betriebe.SequenceEqual(h.Betriebe))
                        {
                            Pruefe.Gleich($"{d.Art(u.Art).Name} {h.Name.Replace("Haus ", "")}", karte.GebaeudeMit(u.Platz)!.Betrieb, "Name am Umlandplatz");
                            gepruefte++;
                        }
                    }
                    else
                        // ohne Umland nur, wenn der Beruf keins hat oder kein Umland-Startplatz seiner Art frei ist (Messstadt: keine Duftgärten, Klosterhöfe, Wachtürme)
                        Pruefe.Wahr(beruf.Umland is not { } um || !e.Frei.Any(f => f.Art == um), $"{h.Name}: {beruf.Umland}");
                }
                // Jeder Umland-Startplatz gehört genau einem Haus oder steht frei
                var umland = original.SelectMany(o => o.Betriebe.Skip(1)).Select(b => b.Platz).ToList();
                var vergeben = e.Belegung.Haeuser.SelectMany(h => h.Betriebe.Skip(1)).Select(b => b.Platz).ToList();
                Pruefe.Gleich(vergeben.Count, vergeben.Distinct().Count(), "kein Platz doppelt");
                Pruefe.Gleich(umland.Count, vergeben.Count + e.Frei.Count, $"Startwert {s}: vergeben + frei");
            }
            Pruefe.Wahr(gepruefte > 0, "kein Haus hat ein neues Umland bekommen");

            // In der Partie: frei gewordene Umland-Startplätze stehen zur Pacht
            for (var s = 1; s <= 20; s++)
            {
                var e = Losen(Messstadt.Value, "Schmied", 8, s);
                if (e.Frei.Count == 0) continue;
                var w = Partie(Messstadt.Value, new NeuePartie(Rivalen: 8), s, Schwierigkeit.Schwer);
                foreach (var (name, art) in e.Frei)
                    Pruefe.Wahr(w.FreieLiegenschaften.Any(l => l.Name == name && l.Art == art), $"Startwert {s}: {name} frei");
                return;
            }
            throw new InvalidOperationException("Testannahme: in 20 Startwerten bleibt ein Umland-Startplatz frei");
        }),

        ("Startaufstellung: Notausweg – mehr KI-Häuser als freie Berufe oder ein Haus ohne Werkstattplatz mit vergebenem Beruf: weniger Häuser und eine Meldung, nie zwei gleiche Berufe", () =>
        {
            var d = Daten.Value;
            var vorlage = Startbelegung.Ersatz.Haeuser;
            var viele = Enumerable.Range(0, 11).Select(i => new Starthaus
            {
                Name = $"Haus Probe{i}", Volk = Volk.Mensch, Wappen = vorlage[i % 3].Wappen, Betriebe = vorlage[i % 3].Betriebe,
            }).ToList();
            var start = new Startbelegung { Spieler = Startbelegung.Ersatz.Spieler, Haeuser = viele };
            var e = Startaufstellung.Losen(null, start, d, "Schmied", Konfession.Katholisch, viele, 3);
            Pruefe.Gleich(9, e.Belegung.Haeuser.Count, "10 Berufe: Spieler und neun KI-Häuser");
            Pruefe.Wahr(e.Meldung is { } m && m.Contains("Haus Probe9") && m.Contains("Haus Probe10") && m.Contains("nicht mit"), e.Meldung ?? "");
            var alle = e.Belegung.Haeuser.Select(h => d.Art(h.Betriebe[0].Art).Beruf).Prepend("Schmied").ToList();
            Pruefe.Wahr(OhneDoppel(alle), string.Join(", ", alle));

            // Ein Haus nur mit Umlandbetrieb (alte eigene Karte) behält seinen Beruf – ist er schon vergeben, spielt es nicht mit
            var grube = new Starthaus { Name = "Haus Stollen", Volk = Volk.Zwerg, Betriebe = { new Startbetrieb { Art = Betriebsart.Mine } } };
            var nurUmland = new Startbelegung { Spieler = Startbelegung.Ersatz.Spieler, Haeuser = { grube } };
            var x = Startaufstellung.Losen(null, nurUmland, d, "Schmied", Konfession.Katholisch, nurUmland.Haeuser, 3);
            Pruefe.Gleich(0, x.Belegung.Haeuser.Count);
            Pruefe.Wahr(x.Meldung?.Contains("Haus Stollen") == true, x.Meldung ?? "");
            var y = Startaufstellung.Losen(null, nurUmland, d, "Tischler", Konfession.Katholisch, nurUmland.Haeuser, 3);
            Pruefe.Gleich(1, y.Belegung.Haeuser.Count);
            Pruefe.Wahr(y.Belegung.Haeuser[0].Betriebe.SequenceEqual(grube.Betriebe), "bleibt Schmied mit seiner Grube");
            Pruefe.Wahr(y.Meldung is null, y.Meldung ?? "");
        }),
    };
}
