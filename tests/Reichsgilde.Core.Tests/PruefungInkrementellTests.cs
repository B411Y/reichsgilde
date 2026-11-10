using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Die Kartenprüfung in Häppchen und mit dem, was der Entwurf aufhebt (9.11.2026: Kapazität mit Vorrat, Startwege und ihr Netz in Häppchen über
/// Änderungen hinweg, das Bett des Geländes nach einem Weg nur um ihn herum, Gewässer und Wasserplan in Häppchen): Nach beliebigen Änderungen muss sie
/// dieselben Befunde in derselben Reihenfolge liefern wie die volle Prüfung einer frisch geöffneten Karte in einem Zug – auf Köln, Greybox, Probestadt
/// und Messstadt, nach vielen zufälligen Änderungen, auch wenn eine Änderung mitten in einen Lauf fällt.
/// </summary>
static class PruefungInkrementellTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenentwurf Lade(string kennung) => Kartenentwurf.AusJson(File.ReadAllText(Datenpfad.Karte(kennung)), Katalog.Value);

    static string Text(IEnumerable<Befund> befunde) =>
        string.Join("\n", befunde.Select(b => $"{b.Schwere} {b.Regel}: {b.Text} @ {b.Ort} [{string.Join(",", b.Gebaeude)}] ({string.Join(",", b.Andere.Select(t => t.Bezeichnung))})"));

    /// <summary>Die volle Prüfung der Karte, wie sie jetzt ist, frisch geöffnet (ohne jeden Zwischenspeicher) und in einem Zug.</summary>
    static string Frisch(Kartenentwurf e) => Text(Kartenpruefung.Pruefen(Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value)));

    /// <summary>Der erste Rasterpunkt, an dem der Boden zweier Gelände verschieden hoch ist (oder null), im Rechteck um alle Wege und Mauern.</summary>
    static string? Bettunterschied(Kartenentwurf a, Kartenentwurf b)
    {
        var orte = a.Wege.SelectMany(w => w.Verlauf).Concat(a.Mauern.SelectMany(m => m.Ecken)).Concat(b.Wege.SelectMany(w => w.Verlauf)).ToList();
        if (orte.Count == 0) return null;
        var (i0, j0, i1, j1) = Hoehenraster.Punkte(Kartenrechteck.Um(orte)!.Value.Vergroessert(20));
        for (var j = j0; j <= j1; j++)
            for (var i = i0; i <= i1; i++)
                if (a.Gelaendeform.Punkthoehe(i, j) != b.Gelaendeform.Punkthoehe(i, j))
                    return $"Boden bei ({i}, {j}): {a.Gelaendeform.Punkthoehe(i, j)} statt {b.Gelaendeform.Punkthoehe(i, j)}";
        return null;
    }

    /// <summary>
    /// Rechnet die Prüfung in Häppchen fertig; mit <paramref name="dazwischen"/> fällt nach einer zufälligen Zahl von Häppchen eine Änderung mitten in den
    /// Lauf (so überleben halbe Läufe von Kapazität, Startwegen und Netz eine Änderung bzw. fangen neu an).
    /// </summary>
    static List<string> Fertigrechnen(Kartenentwurf e, Random r, bool dazwischen)
    {
        var aenderungen = new List<string>();
        var nach = dazwischen ? r.Next(400) : -1;
        for (var n = 0; !e.BefundeSchritt(); n++)
        {
            if (n == nach) aenderungen.Add(Werkstattaenderungen.Zufaellig(e, r));
            if (n > 200_000) throw new Exception("Die Prüfung endet nicht.");
        }
        return aenderungen;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Prüfung inkrementell: nach 15 bis 25 zufälligen Änderungen (auch mitten im Lauf) auf Köln, Greybox, Probestadt und Messstadt dieselben Befunde wie frisch geöffnet in einem Zug", () =>
        {
            foreach (var (kennung, startwert, anzahl) in new[] { ("koeln", 21, 15), ("greybox", 22, 25), ("probestadt", 23, 25), ("messstadt", 24, 25) })
            {
                var e = Lade(kennung);
                var r = new Random(startwert);
                Pruefe.Gleich(Frisch(e), Text(e.Befunde), $"{kennung}: geöffnet");
                for (var i = 0; i < anzahl; i++)
                {
                    var was = new List<string> { Werkstattaenderungen.Zufaellig(e, r) };
                    was.AddRange(Fertigrechnen(e, r, dazwischen: r.Next(3) == 0));
                    var wo = $"{kennung}, Änderung {i + 1} ({string.Join(", ", was)})";
                    Pruefe.Gleich(Frisch(e), Text(e.Befunde), $"{wo}: Befunde");
                    if (i % 5 == 4 && Bettunterschied(e, Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value)) is { } u) throw new Exception($"{wo}: {u}");
                }
            }
        }),

        ("Prüfung inkrementell: Startwege in Häppchen Zahl für Zahl wie auf der gespeicherten Karte (ZuStadtkarte, alles in einem Zug) – geöffnet und nach Änderungen an Wegen und Plätzen", () =>
        {
            static string Wege(IEnumerable<Startweg> wege) =>
                string.Join("\n", wege.Select(w => $"{w.Wer} {w.Platz} {w.Ort} {(w.Meter is { } m ? BitConverter.DoubleToInt64Bits(m) : -1)} {w.Markt}"));
            var e = Lade("koeln");
            var r = new Random(31);
            for (var i = 0; i < 12; i++)
            {
                var teile = e.StartwegSchritte().ToList();
                Pruefe.Gleich(Wege(Startwegpruefung.Rechnen(e.ZuStadtkarte())), Wege(e.Startwegrechnung()), $"Köln, Runde {i}");
                if (i == 0)
                    foreach (var t in new[] { "Startwege: Karte schreiben", "Startwege: Karte lesen", "Startwege: Höhen", "Startwege: Gewässer", "Startwege: Gelände",
                                 "Startwege: Wasserplan", "Startwege: Karrennetz", "Startwege: Wege der Startbetriebe" })
                        Pruefe.Wahr(teile.Contains(t), $"Teil „{t}“");
                Werkstattaenderungen.Zufaellig(e, r, new[] { "weg-neu", "weg-verschieben", "stuetzpunkt", "weg-klasse", "gebaeude", "rueckgaengig" });
            }
            foreach (var kennung in new[] { "greybox", "probestadt", "messstadt" })
            {
                var f = Lade(kennung);
                Pruefe.Gleich(Wege(Startwegpruefung.Rechnen(f.ZuStadtkarte())), Wege(f.Startwegrechnung()), kennung);
            }
        }),

        ("Prüfung inkrementell: ein halbes Netz der Startwege rechnet nach einem Bodenstrich weiter, nach einem Weg von vorn – beides wie in einem Zug", () =>
        {
            var e = Lade("koeln");
            _ = e.Befunde.Count;
            var markt = e.Gebaeude.First(g => g.Katalog == Stadtkarte.MarktKatalog);
            e.WegAnlegen(Wegklasse.Haupt, new List<Kartenort> { new(markt.X - 30, markt.Z + 40), new(markt.X + 30, markt.Z + 44) });
            var alle = e.StartwegSchritte().Count();
            e.WegAnlegen(Wegklasse.Gasse, new List<Kartenort> { new(markt.X - 30, markt.Z + 60), new(markt.X + 30, markt.Z + 64) });
            using (var lauf = e.StartwegSchritte().GetEnumerator())
                for (var i = 0; i < alle / 2; i++) lauf.MoveNext();
            e.BodenStrichBeginnen(new Bodenpinsel(Bodentextur.Erde, 6, Staerke: 1, Kante: 0.5, Deko: false, Dichte: 1));
            e.BodenStrichWeiter(new Kartenort(markt.X, markt.Z));
            e.BodenStrichBeenden();
            var rest = e.StartwegSchritte().Count();
            Pruefe.Wahr(rest < alle - alle / 2 + 3, $"weitergerechnet: {rest} von {alle} Häppchen");
            Pruefe.Gleich(Frisch(e), Text(e.Befunde), "nach dem Bodenstrich");

            e.WegVerschieben(e.Wege[^1], 2, 0);
            using (var lauf = e.StartwegSchritte().GetEnumerator())
                for (var i = 0; i < alle / 3; i++) lauf.MoveNext();
            e.Verschieben(markt.Kennung, markt.X, markt.Z + 3); // mitten im Netz: der Markt rückt – das Netz muss von vorn (alle Wege werden länger oder kürzer)
            static string Wege(IEnumerable<Startweg> wege) => string.Join("\n", wege.Select(w => $"{w.Platz} {(w.Meter is { } m ? BitConverter.DoubleToInt64Bits(m) : -1)}"));
            Pruefe.Gleich(Wege(Startwegpruefung.Rechnen(e.ZuStadtkarte())), Wege(e.Startwegrechnung()), "Startwege nach dem Markt mitten im Netz");
            Pruefe.Gleich(Frisch(e), Text(e.Befunde), "Befunde nach einem Weg und dem Markt mitten im Netz");
        }),

        ("Gelände: das Bett nach einem Weg oder einer Mauer nur um sie herum neu – Punkt für Punkt wie ganz neu; Gegenprobe: ohne den alten Ort bleibt Bett zurück", () =>
        {
            var e = Lade("koeln");
            var r = new Random(41);
            _ = e.Gelaendeform.Punkthoehe(0, 0);
            for (var i = 0; i < 20; i++)
            {
                var was = Werkstattaenderungen.Zufaellig(e, r, new[] { "weg-neu", "weg-verschieben", "weg-loeschen", "weg-breite", "stuetzpunkt", "mauer", "rueckgaengig", "wiederholen" });
                if (Bettunterschied(e, Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value)) is { } u) throw new Exception($"Änderung {i + 1} ({was}): {u}");
            }
            // weite Sprünge: dort, wo der Weg vorher lag, muss das Bett weg (die zufälligen Änderungen oben bleiben in der Nähe)
            static double Ausdehnung(EntwurfWeg w) => Kartenrechteck.Um(w.Verlauf) is { } r ? r.X1 - r.X0 + r.Z1 - r.Z0 : double.MaxValue;
            var weg = e.Wege.Where(w => w.Punkte.Count >= 2).OrderBy(Ausdehnung).First(); // ein kurzer: der neue Ort deckt den alten nicht
            e.WegVerschieben(weg, 80, 0);
            Pruefe.Wahr(Bettunterschied(e, Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value)) is null, "Weg 80 m verschoben");
            e.StuetzpunktVerschieben(weg, 0, weg.Punkte[0].X - 60, weg.Punkte[0].Z + 40, einrasten: false);
            Pruefe.Wahr(Bettunterschied(e, Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value)) is null, "Stützpunkt 70 m verschoben");
            e.Rueckgaengig();
            e.Rueckgaengig();
            Pruefe.Wahr(Bettunterschied(e, Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value)) is null, "zweimal Rückgängig");
            var mauer = e.Mauern[0];
            e.MauerVerschieben(mauer, 30, 0);
            Pruefe.Wahr(Bettunterschied(e, Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value)) is null, "Mauer 30 m verschoben");

            // Gegenprobe am Gelände selbst: ein Weg wandert, gemeldet wird nur der neue Ort – am alten bleibt das Bett stehen
            var roh = new Hoehenraster();
            for (var x = -40; x <= 40; x += 2)
                for (var z = -40; z <= 40; z += 2)
                    roh.WertSetzen((int)(x / Hoehenraster.Abstand), (int)(z / Hoehenraster.Abstand), (int)(100 * (x + 40) / 10 + 30 * z / 10));
            var linie = new List<Kartenort> { new(-30, 0), new(30, 4) };
            var form = new Gelaendeform(roh, () => new[] { new Bettlinie(linie, 4) });
            var vorher = form.Hoehe(0, 1);
            var alt = Kartenrechteck.Um(linie)!.Value.Vergroessert(2 + Gelaendeform.Bankett);
            linie = new List<Kartenort> { new(-30, 20), new(30, 24) };
            var neu = Kartenrechteck.Um(linie)!.Value.Vergroessert(2 + Gelaendeform.Bankett);
            form.LinienVeraltet(neu);
            var nurNeu = form.Hoehe(0, 1);
            form.LinienVeraltet(alt);
            Pruefe.Wahr(nurNeu == vorher && form.Hoehe(0, 1) != vorher, "ohne den alten Ort bliebe das Bett des alten Weges stehen");
            var ganz = new Gelaendeform(roh, () => new[] { new Bettlinie(linie, 4) });
            for (var x = -40; x <= 40; x += 2)
                for (var z = -40; z <= 40; z += 2)
                    Pruefe.Gleich(ganz.Hoehe(x, z), form.Hoehe(x, z), $"bei ({x}, {z})");
        }),

        ("Gelände in Häppchen: ein liegengebliebener Durchgang hinterlässt kein geleertes Bett, auch wenn danach der breiteste Weg schmaler wird (Befund der Prüfung)", () =>
        {
            // synthetisch: ein Durchgang leert seinen Bereich mit der Reichweite des breitesten Weges (20 m), bleibt liegen, dann fällt dieser Weg weg
            var roh = new Hoehenraster();
            for (var x = -100; x <= 100; x += 2)
                for (var z = -100; z <= 100; z += 2)
                    roh.WertSetzen(x / 2, z / 2, 100 * (x + 100) / 10 + 30 * z / 10);
            var a = new List<Kartenort> { new(-30, 0), new(30, 0) };
            var b = new List<Kartenort> { new(-30, 18), new(30, 18) };
            var w = new List<Kartenort> { new(-30, 75), new(30, 75) };
            var linien = new List<Bettlinie> { new(w, 20), new(a, 4), new(b, 4) };
            var form = new Gelaendeform(roh, () => linien.ToList());
            _ = form.Hoehe(0, 0);
            var altA = Kartenrechteck.Um(a)!.Value.Vergroessert(2 + Gelaendeform.Bankett);
            a = new List<Kartenort> { new(-30, 1), new(30, 1) };
            linien[1] = new Bettlinie(a, 4);
            form.LinienVeraltet(altA);
            form.LinienVeraltet(Kartenrechteck.Um(a)!.Value.Vergroessert(2 + Gelaendeform.Bankett));
            using (var lauf = form.AktualisierenSchritte().GetEnumerator())
                Pruefe.Wahr(lauf.MoveNext(), "ein Häppchen, dann bleibt der Lauf liegen");
            var altW = Kartenrechteck.Um(w)!.Value.Vergroessert(10 + Gelaendeform.Bankett);
            linien.RemoveAt(0); // die Reichweite sinkt von 19 auf 11 m
            form.LinienVeraltet(altW);
            var ganz = new Gelaendeform(roh, () => linien.ToList());
            for (var x = -40; x <= 40; x += 2)
                for (var z = -40; z <= 40; z += 2)
                    if (form.Hoehe(x, z) != ganz.Hoehe(x, z))
                        throw new Exception($"bei ({x}, {z}): {form.Hoehe(x, z)} statt {ganz.Hoehe(x, z)}");

            // Köln: die Prüfung bleibt in „Wasser: Gelände“ liegen, dann wird der breiteste Weg wieder schmal
            var e = Lade("koeln");
            var breit = e.Wege.Where(x => x.Punkte.Count >= 2).OrderByDescending(x => x.BreiteWirksam).First();
            e.WegBreiteSetzen(breit, 20);
            _ = e.Befunde.Count;
            var markt = e.Gebaeude.First(g => g.Katalog == Stadtkarte.MarktKatalog);
            var nah = e.Wege.Where(x => x != breit && x.Punkte.Count >= 2).OrderBy(x => x.Verlauf.Min(p => p.Abstand(markt.Punkt))).First();
            e.WegVerschieben(nah, 1, 0);
            while (!e.BefundeSchritt() && e.BefundeTeil != "Wasser: Gelände") { }
            Pruefe.Gleich("Wasser: Gelände", e.BefundeTeil, "liegengeblieben im Bett");
            e.WegBreiteSetzen(breit, 0);
            Pruefe.Wahr(Bettunterschied(e, Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value)) is null, "Köln: Bett wie neu");
            Pruefe.Gleich(Frisch(e), Text(e.Befunde), "Köln: Befunde wie neu");
        }),

        ("Gelände in Häppchen: wer mitten im Lauf liest, bekommt das fertige Bett; der Lauf bricht danach ab – auch nach einer Änderung dazwischen", () =>
        {
            var e = Lade("koeln");
            _ = e.Gelaendeform.Punkthoehe(0, 0);
            var markt = e.Gebaeude.First(g => g.Katalog == Stadtkarte.MarktKatalog);
            e.WegAnlegen(Wegklasse.Haupt, new List<Kartenort> { new(markt.X - 60, markt.Z + 40), new(markt.X + 60, markt.Z + 44) });
            using (var lauf = e.Gelaendeform.AktualisierenSchritte().GetEnumerator())
            {
                lauf.MoveNext();
                var mitten = e.Gelaendeform.Hoehe(markt.X, markt.Z + 42); // liest: rechnet selbst fertig
                Pruefe.Wahr(!lauf.MoveNext() || !lauf.MoveNext(), "der halbe Lauf bricht ab");
                Pruefe.Wahr(Bettunterschied(e, Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value)) is null, "fertig wie neu");
                Pruefe.Gleich(mitten, e.Gelaendeform.Hoehe(markt.X, markt.Z + 42), "dieselbe Höhe");
            }
            e.WegVerschieben(e.Wege[^1], 0, 6);
            using (var lauf = e.Gelaendeform.AktualisierenSchritte().GetEnumerator())
            {
                lauf.MoveNext();
                e.WegVerschieben(e.Wege[^1], 0, 6); // eine Änderung dazwischen
                while (lauf.MoveNext()) { }
            }
            foreach (var _ in e.Gelaendeform.AktualisierenSchritte()) { }
            Pruefe.Wahr(Bettunterschied(e, Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value)) is null, "nach der Änderung im Lauf wie neu");
        }),
    };
}
