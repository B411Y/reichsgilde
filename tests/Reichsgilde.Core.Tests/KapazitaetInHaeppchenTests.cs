using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Kapazitätsprüfung in Häppchen und mit Vorrat (<see cref="Kartenentwurf.KapazitaetSchritte"/>, <see cref="Abstandsvorrat"/>, 9.11.2026): In der
/// Kartenwerkstatt rechnet sie über mehrere Bilder, und nach einer Änderung nur die Abstände zur Straße in der Nähe geänderter Wege neu. Das Ergebnis
/// muss dasselbe sein wie eine Rechnung ohne Vorrat in einem Zug (<see cref="Kapazitaetspruefung.Pruefen"/>) – Befunde, Bericht, gesetzte Gebäude und
/// die Abstände Zahl für Zahl –, auf den mitgelieferten Karten und nach vielen zufälligen Änderungen.
/// </summary>
static class KapazitaetInHaeppchenTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenentwurf Lade(string kennung) => Kartenentwurf.AusJson(File.ReadAllText(Datenpfad.Karte(kennung)), Katalog.Value);

    /// <summary>Alles, was die Kapazitätsprüfung liefert, als Text: Befunde, Bericht und jedes gesetzte Gebäude mit seinen Feldern.</summary>
    static string Text(Kapazitaet k) =>
        string.Join("\n", k.Befunde.Select(b => $"{b.Schwere} {b.Regel}: {b.Text}")) + "\n--\n" + string.Join("\n", k.Bericht) + "\n--\n"
        + string.Join("\n", k.Plaetze.Select(p => p.Art + ": " + string.Join(";", p.Felder.Select(f => $"{f.I},{f.J}"))));

    /// <summary>Ohne Vorrat in einem Zug gerechnet – mit dem Vorrat, den dieser Lauf hinterlässt.</summary>
    static (Kapazitaet Ergebnis, Abstandsvorrat Vorrat) OhneVorrat(Kartenentwurf e, Schwere schwere, Abstandsvorrat? vorrat = null)
    {
        (Kapazitaet, Abstandsvorrat)? aus = null;
        foreach (var _ in Kapazitaetspruefung.Schritte(e, schwere, vorrat, (k, v) => aus = (k, v))) { }
        return aus!.Value;
    }

    /// <summary>Gleiches Raster und Zahl für Zahl (Bits) gleiche Abstände?</summary>
    static string? Unterschied(Abstandsvorrat a, Abstandsvorrat b)
    {
        if (a.Zonen.I0 != b.Zonen.I0 || a.Zonen.J0 != b.Zonen.J0 || a.Zonen.Breite != b.Zonen.Breite || a.Zonen.Hoehe != b.Zonen.Hoehe
            || !a.Zonen.Zone.SequenceEqual(b.Zonen.Zone) || !a.Zonen.NurWerkstatt.SequenceEqual(b.Zonen.NurWerkstatt) || !a.Zonen.Felder.SequenceEqual(b.Zonen.Felder))
            return "Raster der Zonen";
        for (var n = 0; n < a.Karren.Length; n++)
        {
            if (BitConverter.DoubleToInt64Bits(a.Karren[n]) != BitConverter.DoubleToInt64Bits(b.Karren[n])) return $"Karrenstraße, Feld {n}: {a.Karren[n]} statt {b.Karren[n]}";
            if (BitConverter.DoubleToInt64Bits(a.Alle[n]) != BitConverter.DoubleToInt64Bits(b.Alle[n])) return $"Weg, Feld {n}: {a.Alle[n]} statt {b.Alle[n]}";
        }
        return null;
    }

    static readonly Schwere Schwere = Schwere.Hinweis;

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Kapazität in Häppchen: auf Köln, Greybox, Probestadt und Messstadt dasselbe wie in einem Zug, in vielen kleinen Stücken", () =>
        {
            foreach (var kennung in new[] { "koeln", "greybox", "probestadt", "messstadt" })
            {
                var e = Lade(kennung);
                var voll = Text(Kapazitaetspruefung.Pruefen(e, Schwere));
                var teile = e.KapazitaetSchritte(Schwere).ToList();
                Pruefe.Gleich(voll, Text(e.Kapazitaetsrechnung(Schwere)), $"{kennung}: Ergebnis");
                Pruefe.Wahr(e.KapazitaetSchritte(Schwere).Count() == 0, $"{kennung}: danach aus dem Zwischenspeicher");
                if (kennung == "koeln")
                {
                    Pruefe.Wahr(teile.Count > 20, $"Köln: {teile.Count} Häppchen");
                    foreach (var t in new[] { "Kapazität: Zonen", "Kapazität: Abstände", "Kapazität: Gebäude der Karte", "Kapazität: Reihenfolge", "Kapazität: Pflicht", "Kapazität: Reserve" })
                        Pruefe.Wahr(teile.Contains(t), $"Köln: Teil „{t}“");
                }
            }
        }),

        ("Kapazität mit Vorrat: nach je 40 zufälligen Änderungen auf Köln, Probestadt und Greybox Zahl für Zahl wie ohne Vorrat", () =>
        {
            foreach (var (kennung, startwert) in new[] { ("koeln", 11), ("probestadt", 12), ("greybox", 13) })
            {
                var e = Lade(kennung);
                var r = new Random(startwert);
                _ = e.Kapazitaetsrechnung(Schwere);
                var mitVorrat = 0;
                for (var i = 0; i < 40; i++)
                {
                    var was = Werkstattaenderungen.Zufaellig(e, r);
                    var vorrat = e.KapazitaetVorrat;
                    var inkrementell = Text(e.Kapazitaetsrechnung(Schwere));
                    var (frisch, frischerVorrat) = OhneVorrat(e, Schwere);
                    Pruefe.Gleich(Text(frisch), inkrementell, $"{kennung}, Änderung {i + 1} ({was}): Ergebnis");
                    if (Unterschied(e.KapazitaetVorrat!, frischerVorrat) is { } u) throw new Exception($"{kennung}, Änderung {i + 1} ({was}): {u}");
                    if (vorrat is not null && ReferenceEquals(vorrat.Zonen, e.KapazitaetVorrat!.Zonen)) mitVorrat++;
                }
                Pruefe.Wahr(mitVorrat >= 10, $"{kennung}: nur {mitVorrat} Läufe mit dem Raster aus dem Vorrat");
            }
        }),

        ("Kapazität mit Vorrat: ein Weg weit weg von den Zonen rechnet keinen Block neu; Gegenprobe: ein Vorrat, der eine Änderung nicht kennt, gibt andere Abstände", () =>
        {
            var e = Lade("koeln");
            _ = e.Kapazitaetsrechnung(Schwere);
            int Abstaende(IEnumerable<string> teile) => teile.Count(t => t == "Kapazität: Abstände");
            var ohne = Abstaende(Kapazitaetspruefung.Schritte(e, Schwere, null, (_, _) => { }));
            var rand = e.Kartengrenzen.Spielfeld;
            e.WegAnlegen(Wegklasse.Gasse, new List<Kartenort> { new(rand.X0 + 5, rand.Z0 + 5), new(rand.X0 + 25, rand.Z0 + 8) }, einrasten: false);
            var mit = Abstaende(e.KapazitaetSchritte(Schwere).ToList());
            Pruefe.Wahr(ohne >= 5, $"ohne Vorrat {ohne} Häppchen der Abstände");
            Pruefe.Wahr(mit <= 2, $"mit Vorrat {mit} Häppchen der Abstände (nur das Fach der Wege und das Ende)");
            Pruefe.Gleich(Text(OhneVorrat(e, Schwere).Ergebnis), Text(e.Kapazitaetsrechnung(Schwere)), "dasselbe Ergebnis");

            // Gegenprobe: ein Weg mitten in der Stadt, aber ein Vorrat, der ihn schon zu kennen glaubt – die Abstände dort bleiben alt und falsch
            var vorher = e.KapazitaetVorrat!;
            var markt = e.Gebaeude.First(g => g.Katalog == Stadtkarte.MarktKatalog);
            e.WegAnlegen(Wegklasse.Land, new List<Kartenort> { new(markt.X - 40, markt.Z + 30), new(markt.X + 40, markt.Z + 34) }, einrasten: false);
            var jetzt = e.Wege.Where(w => w.Punkte.Count > 0).Select(w => new Wegstand(w, w.Verlauf, w.IstKarrenstrasse)).ToList();
            var blind = new Abstandsvorrat(vorher.Zonen, jetzt, vorher.Karren, vorher.Alle);
            var (_, ergebnisBlind) = OhneVorrat(e, Schwere, blind);
            var (_, richtig) = OhneVorrat(e, Schwere);
            Pruefe.Wahr(Unterschied(ergebnisBlind, richtig) is not null, "ein blinder Vorrat muss auffallen");
            Pruefe.Wahr(Unterschied(OhneVorrat(e, Schwere, vorher).Vorrat, richtig) is null, "mit dem echten Vorrat gleich");
        }),

        ("Kapazität in Häppchen: ein halber Lauf rechnet nach einem Bodenstrich weiter, nach einem Weg von vorn – beides wie ohne Vorrat", () =>
        {
            var e = Lade("koeln");
            var alle = e.KapazitaetSchritte(Schwere).Count();
            var f = Lade("koeln");
            using (var lauf = f.KapazitaetSchritte(Schwere).GetEnumerator())
                for (var i = 0; i < alle / 2; i++) lauf.MoveNext();
            f.BodenStrichBeginnen(new Bodenpinsel(Bodentextur.Erde, 6, Staerke: 1, Kante: 0.5, Deko: false, Dichte: 1));
            f.BodenStrichWeiter(new Kartenort(0, 0));
            f.BodenStrichBeenden();
            var rest = f.KapazitaetSchritte(Schwere).Count();
            Pruefe.Wahr(rest < alle - alle / 2 + 2, $"weitergerechnet: {rest} von {alle} Häppchen");
            Pruefe.Gleich(Text(Kapazitaetspruefung.Pruefen(f, Schwere)), Text(f.Kapazitaetsrechnung(Schwere)), "nach dem Bodenstrich");

            f.WegAnlegen(Wegklasse.Haupt, new List<Kartenort> { new(-20, 40), new(0, 44), new(20, 40) });
            using (var lauf = f.KapazitaetSchritte(Schwere).GetEnumerator())
                for (var i = 0; i < 5; i++) lauf.MoveNext();
            f.WegAnlegen(Wegklasse.Gasse, new List<Kartenort> { new(-20, 60), new(20, 60) });
            Pruefe.Gleich(Text(Kapazitaetspruefung.Pruefen(f, Schwere)), Text(f.Kapazitaetsrechnung(Schwere)), "nach zwei Wegen, einer mitten im Lauf");
        }),
    };
}
