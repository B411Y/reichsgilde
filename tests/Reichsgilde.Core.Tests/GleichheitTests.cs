using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Reichsgilde.Core.Mehrspieler;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Der Gleichheitstest (Mehrspieler M1): Gleiche Befehle ergeben überall bitgenau dieselbe Partie.
///
/// Im Mehrspieler (Lockstep) gehen nur die Befehle übers Netz; jeder Rechner rechnet die Simulation selbst. Das geht nur, wenn der Kern
/// deterministisch ist: gleicher Startwert + gleiche Befehle zur gleichen Minute = gleiches Ergebnis, auf Windows wie auf Linux.
///
/// Je Startwert spielt dieselbe Partie dreimal 10 Jahre auf der Greybox-Karte (1 Mensch, 3 KI-Häuser; mehrere Menschen kennt der
/// Partieaufbau noch nicht), dazu eine Partie 4 Jahre in Köln (drei Marktplätze, Wege über das Pflaster, Fahrzeit nach Weglänge – Befund der
/// Prüfung von phase5/gehtempo, 1.11.2026), mit einem festen Befehlsskript des Menschen: kaufen und verkaufen per Karren, einen Zweitbetrieb pachten,
/// abgeben und wieder erwerben (Löschen und danach Einfügen), Stand, Bewerbung, Geschenk und Stimmen in der Ratssitzung, Antworten auf Entscheidungen
/// (Stufe 3: Fragen mit ungerader Nummer beantwortet er, die übrigen laufen ab).
///   • Lauf A durchgehend.
///   • Lauf B genauso, aber mit deutscher Ländereinstellung (eine Zahl als Text darf nirgends in den Spielstand geraten).
///   • Lauf C speichert und lädt an acht Zeitpunkten (mitten im Jahr, in der Gaunernacht, zum Jahresbeginn, am Abend, mitten in drei Bränden nach Blitzschlag, einer davon über den Jahreswechsel) und direkt nach jedem Löschen.
/// Verglichen wird die Prüfsumme des Kerns (<see cref="Zustandspruefsumme"/>: SHA-256 über den Spielstand ohne Speicherzeit, Titel, Tempo,
/// Spielerkonto und Warteschlange) nach jedem Jahr und am Ende.
/// Dazu der Wasserverbund (Befund 1, 09.11.2026, Zeile „wasserverbund“) und der Felsfall (Felspinsel F3): ein fester Ablauf der Kartenwerkstatt mit Stempeln, Streuen und Radieren ergibt dieselbe Kartendatei,
/// der Felsboden (F6): Stücke und Boden um den Fels als ein Befehl, die Felslage (R2): Zufallsbereiche, Transformieren, Einrasten, Neu würfeln,
/// das Kopieren von Felsstücken (P2) und die Pinselwerte, Ausdünnen und Neu verteilen (P3).
/// Die Prüfsummen stehen zusätzlich in einer Datei: Die CI vergleicht sie zwischen Linux und Windows (Job „Gleichheit Linux/Windows“).
/// </summary>
static class GleichheitTests
{
    static readonly DateTime Zeitpunkt = new(2026, 10, 29, 12, 0, 0);
    static long Minute(int jahr, int stunde, int minute = 0) => jahr * (long)Spielzeit.MinutenProTag + (stunde * 60 + minute - Spielzeit.TagesbeginnMinute);

    /// <summary>Eine Partie des Tests: Startwert, Schwierigkeit, Charakter der KI, Beruf des Menschen, Karte und Länge.</summary>
    sealed record Partie(int Startwert, Schwierigkeit Schwierigkeit, KiCharakter Rivale, string Beruf, string Karte = "greybox", int Jahre = 10);

    static readonly Partie[] Partien =
    {
        new(1400, Schwierigkeit.Normal, KiCharakter.Ausgewogen, "Schmied"),
        new(4711, Schwierigkeit.Schwer, KiCharakter.Wagemutig, "Tischler"),
        // bis zur gemeinsamen Strecke von Kern und Bild (31.10.2026) 2026 – dort stirbt seither das Oberhaupt des Menschen im Jahr 1 ohne Erben, das Skript hätte nichts abzugeben
        new(2027, Schwierigkeit.Normal, KiCharakter.Vorsichtig, "Steinmetz"),
        // Köln (vier Jahre: Köln rechnet etwa doppelt so lange wie die Greybox; im vierten Jahr wird der Zweitbetrieb abgegeben und wieder erworben)
        new(1401, Schwierigkeit.Schwer, KiCharakter.Ausgewogen, "Schmied", "koeln", 4),
    };

    /// <summary>
    /// Lauf C lädt vor diesen Minuten neu (dazu direkt nach jedem Löschen): mitten im Jahr, mitten in der Gaunernacht (zwischen zwei
    /// Nachtversuchen – bis PR #251 fehlte dort der Erfolg je Auftrag im Spielstand, 4711 lief im Jahr 3 auseinander), zum Jahresbeginn, am Abend.
    /// Dazu mitten im Eis des strengen Winters 1407 (Startwert 1400: der Grenzfluss ist von 7 bis 19 Uhr zu, Datenversion 69)
    /// und mitten in einem Brand nach Blitzschlag (Datenversion 68; jedes Feuer brennt mindestens eine Stunde): bei 4711 in Minute 863 (Jahr 0,
    /// 18:23 Uhr), bei 2027 in Minute 6380 (Jahr 5, 15:20) und 10231 (Jahr 8, 22:31 – es brennt über den Jahreswechsel, geladen wird um 4:01 im Jahr 9).
    /// Die Einschläge (samt übergesprungener Feuer und anstehender Sprünge) stehen seit dem Blitzschaden (3.11.2026) im Spielstand; an jedem
    /// Ladepunkt vergleicht der Test zusätzlich Lager und Vermögen der Häuser, deren Gebäude gerade brennt (<see cref="Brandlage"/>).
    /// </summary>
    static readonly long[] Ladepunkte = { Minute(0, 18, 50), Minute(1, 12, 20), Minute(2, 21, 37), Minute(5, 4), Minute(5, 4, 1), Minute(5, 15, 50), Minute(7, 12, 30), Minute(8, 19, 0), Minute(9, 4, 1) };

    /// <summary>Wann der Zweitbetrieb abgegeben (gelöscht) und wieder erworben (eingefügt) wird.</summary>
    static readonly int[] Abgabejahre = { 3, 7 };

    /// <summary>Was ein Lauf ergibt: Prüfsumme nach jedem Jahr, am Ende, und die Befehle mit Erfolg.</summary>
    /// <param name="Erloschen">Ab welchem Jahr (0 = erstes) das Haus des Spielers nicht mehr aktiv ist (gestorben ohne Erben, bankrott) – sonst null.</param>
    /// <param name="Stress">Wie viele Stressquellen (Einträge mit Wert über 0) die Personen der Häuser nach jedem Jahr tragen, zusammen – der Stress steht im Spielstand und damit in der Prüfsumme.</param>
    /// <param name="Geheimnisse">Wie viele Geheimnisse es nach jedem Jahr gibt, zusammen (Teil 3c) – sie stehen im Spielstand.</param>
    /// <param name="Wissen">Was die Häuser nach jedem Jahr von fremden Geheimnissen wissen, zusammen (Teil 3c) – steht im Spielstand.</param>
    /// <param name="GeheimMeldungen">Minute, Art und Text jeder Meldung mit Geheimnisart (Siegel) – der Verlauf steht im Spielstand und in der Prüfsumme.</param>
    /// <param name="Entscheidungen">Minute, Nummer, Art und Ausgang jeder Entscheidung des Menschen (Stufe 3: geöffnet, entschieden, Frist, Obergrenze, hinfällig) – sie stehen im Spielstand.</param>
    sealed record Lauf(IReadOnlyList<string> Jahre, string Ende, IReadOnlyList<string> Befehle, IReadOnlyList<string> Blitze, int? Erloschen, IReadOnlyList<string> Braende, int Stress,
        int Geheimnisse, int Wissen, IReadOnlyList<string> GeheimMeldungen, IReadOnlyList<string> Entscheidungen, IReadOnlyDictionary<long, int> OffenAnLadepunkten);

    /// <summary>Alle Zeilen für die Datei der CI, gesammelt über die Partien.</summary>
    static readonly SortedDictionary<int, IReadOnlyList<string>> Zeilen = new();

    public static IEnumerable<(string, Action)> Alle() => Partien.Select(p => (
        $"Gleichheit: Startwert {p.Startwert} ({p.Schwierigkeit}, {p.Rivale}, {p.Beruf}{(p.Karte == "greybox" ? "" : $", {p.Karte}")}) – {p.Jahre} Jahre durchgehend, mit deutscher Ländereinstellung " +
        "und mit Speichern/Laden (auch direkt nach dem Löschen eines Betriebs) ergeben dieselbe Prüfsumme",
        (Action)(() => Pruefen(p)))).Append((
        "Gleichheit: Felspinsel (Stempel, Streuen mit Filter, Radieren, Rückgängig) ergibt zweimal und mit deutscher Ländereinstellung dieselbe Kartendatei",
        FelsenPruefen)).Append((
        "Gleichheit: Felsboden (Felsstrich und Boden als ein Befehl, Rückgängig) ergibt zweimal und mit deutscher Ländereinstellung dieselbe Kartendatei",
        FelsbodenPruefen)).Append((
        "Gleichheit: Felslage R2 (Zufallsbereiche, Transformieren um alle Achsen, Einrasten, Neu würfeln) ergibt zweimal und mit deutscher Ländereinstellung dieselbe Kartendatei",
        FelslagePruefen)).Append((
        "Gleichheit: Felswand-Satz R3/R5 (Hang mitformen, Drehen und Skalieren mehrerer Stücke in Weltlage, Rückgängig) ergibt zweimal und mit deutscher Ländereinstellung dieselbe Kartendatei",
        FelswandPruefen)).Append((
        "Gleichheit: Felsen kopieren und schieben P2 (versetzt und an Ort, Kopien gedreht, geschoben und aufgesetzt, Rückgängig) ergibt zweimal und mit deutscher Ländereinstellung dieselbe Kartendatei",
        FelskopiePruefen)).Append((
        "Gleichheit: Wasserverbund (Fluss und See als eine Fläche, Boden, Bett, Ufer, Spiegel, Strömung auf einem Gitter) ergibt zweimal und mit deutscher Ländereinstellung dieselben Bits",
        WasserverbundPruefen)).Append((
        "Gleichheit: Felspinsel P3 (Abstand, Streuung des Einsinkens, Ausdünnen, Neu verteilen, Radierer nur für Formen, Höhenfilter, Rückgängig) ergibt zweimal und mit deutscher Ländereinstellung dieselbe Kartendatei",
        FelsP3Pruefen));

    /// <summary>Felspinsel P3: die neuen Pinselwerte, Ausdünnen und Neu verteilen – gleiche Bytes (Zeile „felsp3 …“ in der Datei der CI).</summary>
    static void FelsP3Pruefen()
    {
        var a = Sha256(FelspinselP3Tests.Ablauf());
        Pruefe.Gleich(a, Sha256(FelspinselP3Tests.Ablauf()), "Fels P3: zweiter Lauf");
        Pruefe.Gleich(a, MitKultur("de-DE", () => Sha256(FelspinselP3Tests.Ablauf())), "Fels P3: de-DE");
        Zeilen[-6] = new[] { $"felsp3 {a}" };
        DateiSchreiben();
    }

    /// <summary>Felsen kopieren (Fels P2, Strg+D): gleiche Auswahl, gleiche Kopien – gleiche Bytes (Zeile „felskopie …“ in der Datei der CI).</summary>
    static void FelskopiePruefen()
    {
        var a = Sha256(FelsKopierenTests.Ablauf());
        Pruefe.Gleich(a, Sha256(FelsKopierenTests.Ablauf()), "Felskopie: zweiter Lauf");
        Pruefe.Gleich(a, MitKultur("de-DE", () => Sha256(FelsKopierenTests.Ablauf())), "Felskopie: de-DE");
        Zeilen[-5] = new[] { $"felskopie {a}" };
        DateiSchreiben();
    }

    /// <summary>Der Wasserverbund (Befund 1, 09.11.2026): Boden, Wasser, Spiegel und Strömung eines festen Ablaufs – gleiche Bits (Zeile „wasserverbund …“ in der Datei der CI).</summary>
    static void WasserverbundPruefen()
    {
        var a = Sha256(WasserverbundTests.Ablauf());
        Pruefe.Gleich(a, Sha256(WasserverbundTests.Ablauf()), "Wasserverbund: zweiter Lauf");
        Pruefe.Gleich(a, MitKultur("de-DE", () => Sha256(WasserverbundTests.Ablauf())), "Wasserverbund: de-DE");
        Zeilen[-7] = new[] { $"wasserverbund {a}" };
        DateiSchreiben();
    }

    /// <summary>
    /// Die Felslage (Fels-Plan R2): Zufallsbereiche, Drehen um alle Achsen (Euler über DetMathe), Einrasten und Neu würfeln – gleiche Bytes (Zeile
    /// „felslage …“ in der Datei der CI).
    /// </summary>
    static void FelslagePruefen()
    {
        var a = Sha256(FelslageTests.Ablauf());
        Pruefe.Gleich(a, Sha256(FelslageTests.Ablauf()), "Felslage: zweiter Lauf");
        Pruefe.Gleich(a, MitKultur("de-DE", () => Sha256(FelslageTests.Ablauf())), "Felslage: de-DE");
        Zeilen[-3] = new[] { $"felslage {a}" };
        DateiSchreiben();
    }

    /// <summary>Der Felswand-Satz (Fels R3/R5): Hang mitformen und Mehrfach-Transformationen in Weltlage – gleiche Bytes (Zeile „felswand …“ in der Datei der CI).</summary>
    static void FelswandPruefen()
    {
        var a = Sha256(FelsgizmoTests.Ablauf());
        Pruefe.Gleich(a, Sha256(FelsgizmoTests.Ablauf()), "Felswand: zweiter Lauf");
        Pruefe.Gleich(a, MitKultur("de-DE", () => Sha256(FelsgizmoTests.Ablauf())), "Felswand: de-DE");
        Zeilen[-4] = new[] { $"felswand {a}" };
        DateiSchreiben();
    }

    /// <summary>Der Felsboden (F6): Stücke und Boden um den Fels in einem Befehl – gleiche Bytes (Zeile „felsboden …“ in der Datei der CI).</summary>
    static void FelsbodenPruefen()
    {
        var a = Sha256(FelsbodenTests.Ablauf());
        Pruefe.Gleich(a, Sha256(FelsbodenTests.Ablauf()), "Felsboden: zweiter Lauf");
        Pruefe.Gleich(a, MitKultur("de-DE", () => Sha256(FelsbodenTests.Ablauf())), "Felsboden: de-DE");
        Zeilen[-2] = new[] { $"felsboden {a}" };
        DateiSchreiben();
    }

    /// <summary>
    /// Der Felsfall (Felspinsel F3): Ein fester Ablauf der Kartenwerkstatt (<see cref="FelsenTests.Ablauf"/>) – gleicher Strich, gleiche Bytes.
    /// Die Prüfsumme der Kartendatei steht in der Datei der CI (Zeile „fels …“) und wird so zwischen Linux und Windows verglichen.
    /// </summary>
    static void FelsenPruefen()
    {
        var a = Sha256(FelsenTests.Ablauf());
        Pruefe.Gleich(a, Sha256(FelsenTests.Ablauf()), "Felsen: zweiter Lauf");
        Pruefe.Gleich(a, MitKultur("de-DE", () => Sha256(FelsenTests.Ablauf())), "Felsen: de-DE");
        Zeilen[-1] = new[] { $"fels {a}" };
        DateiSchreiben();
    }

    static void Pruefen(Partie p)
    {
        // Der Winterfall: Startwert 1400 hat 1407 einen strengen Winter mit Eis (Köln 1401: 1403) – sonst prüft der Test die Jahreszeiten nicht.
        if (p.Startwert is 1400 or 1401)
        {
            var winter = new Stadtwirtschaft(Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()), Partieeinstellungen.Standard.StartStatut, p.Startwert)
                .Jahreszeiten.WinterVon(p.Startwert == 1400 ? 1407 : 1403);
            Pruefe.Wahr(winter is { Streng: true }, $"Startwert {p.Startwert}: kein strenger Winter im Testzeitraum – Winterfall anpassen");
        }
        var a = Spielen(p, laden: false);
        var b = MitKultur("de-DE", () => Spielen(p, laden: false));
        var c = Spielen(p, laden: true);

        Vergleichen("Lauf B (de-DE)", a, b);
        Vergleichen("Lauf C (Speichern und Laden)", a, c);
        Pruefe.Gleich(string.Join("\n", a.Blitze), string.Join("\n", c.Blitze), "Blitzschläge (Minute, Ziel, Schaden) mit Speichern und Laden");
        Pruefe.Gleich(string.Join("\n", a.Braende), string.Join("\n", c.Braende), "Lager und Vermögen der brennenden Häuser an den Ladepunkten");
        // Ein Blitzfall (Datenversion 68): bei 4711 (Jahr 0) und 2027 (Jahre 5 und 8) schlägt der Blitz ein; 1400 und Köln bleiben in ihren Jahren verschont
        if (p.Startwert is 4711 or 2027) Pruefe.Wahr(a.Blitze.Count >= 1, $"Kein Blitzschlag in {p.Jahre} Jahren (Startwert {p.Startwert}) – Ladepunkte im Brand prüfen");
        // Stress (Datenversion 77) soll im Spiel vorkommen – sonst prüft die Prüfsumme ihn nicht mit.
        Pruefe.Wahr(a.Stress > 0, $"Kein Stress in {p.Jahre} Jahren (Startwert {p.Startwert})");
        Pruefe.Gleich(a.Stress, c.Stress, "Stressquellen mit Speichern und Laden");
        // Geheimnisse (Datenversion 78) sollen vorkommen – Ratsherren haben schon zu Partiebeginn welche, das Gerede verbreitet sie.
        Pruefe.Wahr(a.Geheimnisse > 0, $"Keine Geheimnisse in {p.Jahre} Jahren (Startwert {p.Startwert})");
        Pruefe.Gleich(a.Geheimnisse, c.Geheimnisse, "Geheimnisse mit Speichern und Laden");
        // Die Häuser sollen auch etwas wissen (Gerede, Spitzel der KI) – sonst prüfte der Lauf ihre Pfade nicht.
        Pruefe.Wahr(a.Wissen > 0, $"Kein Wissen über Geheimnisse in {p.Jahre} Jahren (Startwert {p.Startwert})");
        Pruefe.Gleich(a.Wissen, c.Wissen, "Wissen über Geheimnisse mit Speichern und Laden");
        // Meldungen mit Geheimnisart (Siegel) sollen vorkommen – sonst prüfte die Prüfsumme das Feld im Verlauf nicht mit.
        Pruefe.Wahr(a.GeheimMeldungen.Count > 0, $"Keine Meldung mit Geheimnisart in {p.Jahre} Jahren (Startwert {p.Startwert})");
        Pruefe.Gleich(string.Join("\n", a.GeheimMeldungen), string.Join("\n", c.GeheimMeldungen), "Meldungen mit Geheimnisart mit Speichern und Laden");
        // Entscheidungen (Stufe 3) stehen im Spielstand: Öffnen, Befehl und Fristablauf laufen mit Speichern und Laden gleich. Bei 4711 schreibt
        // ein KI-Haus dem Menschen 1402 zwei Erpresserbriefe – der Mensch zahlt den ersten (EntscheidungTreffen), der zweite läuft Ende 1403 ab
        // (Lauf C lädt dazwischen, Minute(2, 21, 37), mit offener Frage).
        Pruefe.Gleich(string.Join("\n", a.Entscheidungen), string.Join("\n", b.Entscheidungen), "Entscheidungen mit deutscher Ländereinstellung");
        Pruefe.Gleich(string.Join("\n", a.Entscheidungen), string.Join("\n", c.Entscheidungen), "Entscheidungen mit Speichern und Laden");
        Pruefe.Gleich(string.Join(",", a.OffenAnLadepunkten.OrderBy(x => x.Key)), string.Join(",", c.OffenAnLadepunkten.OrderBy(x => x.Key)), "offene Entscheidungen an den Ladepunkten");
        if (p.Startwert == 4711)
        {
            Pruefe.Wahr(a.Befehle.Any(z => z.Contains(" EntscheidungTreffen ok")), $"Kein erfolgreicher Befehl EntscheidungTreffen ({string.Join(" | ", a.Entscheidungen)})");
            Pruefe.Wahr(a.Entscheidungen.Any(z => z.Contains($" {Reichsgilde.Core.Entscheidungen.Entscheidungsabschluss.Frist} ")),
                $"Kein Fristablauf einer Entscheidung ({string.Join(" | ", a.Entscheidungen)})");
            Pruefe.Wahr(c.OffenAnLadepunkten.GetValueOrDefault(Minute(2, 21, 37)) > 0,
                $"Lauf C lädt bei Minute(2, 21, 37) ohne offene Entscheidung ({string.Join(" | ", a.Entscheidungen)})");
        }

        // Das Skript soll seine Wege wirklich gehen – sonst prüft der Test weniger, als er verspricht.
        string Erfolge(string art) => $"{a.Befehle.Count(z => z.Contains($" {art} ok"))} × {art}; alle: " +
            string.Join(", ", a.Befehle.GroupBy(z => z[(z.IndexOf(' ') + 1)..]).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Count()}"));
        foreach (var art in new[] { "Handelsauftrag", "BetriebVerkaufen", "LiegenschaftErwerben" })
            Pruefe.Wahr(a.Befehle.Any(z => z.Contains($" {art} ok")), $"Kein erfolgreicher Befehl {art} im Skript – Skript anpassen ({Erfolge(art)})");
        // Abgeben kann nur ein Haus, das noch lebt (Startwert 2027 mit den Festmärkten für Fertigwaren: Der Patriarch stirbt Ende 1406 ohne Erben)
        var abgaben = Abgabejahre.Count(j => j < p.Jahre && j < (a.Erloschen ?? int.MaxValue));
        Pruefe.Wahr(abgaben >= 1, $"Das Haus des Spielers erlischt vor dem ersten Abgabejahr (Jahr {a.Erloschen}) – Skript oder Startwert anpassen");
        Pruefe.Wahr(a.Befehle.Count(z => z.Contains(" BetriebVerkaufen ok")) == abgaben
                    && a.Befehle.Count(z => z.Contains(" LiegenschaftErwerben ok")) >= abgaben + 1,
            $"Löschen und Einfügen in jedem Abgabejahr ({Erfolge("BetriebVerkaufen")}, {Erfolge("LiegenschaftErwerben")})");

        var zeilen = a.Jahre.Select((s, i) => $"startwert {p.Startwert} jahr {i + 1}: {s}")
            .Append($"startwert {p.Startwert} ende: {a.Ende}")
            .Append($"startwert {p.Startwert} befehle: {Sha256(string.Join("\n", a.Befehle))}")
            .ToList();
        Zeilen[p.Startwert] = zeilen;
        DateiSchreiben();
        Console.WriteLine($"          Prüfsumme Startwert {p.Startwert}: {a.Ende}");
        Console.WriteLine($"          Entscheidungen Startwert {p.Startwert}: {a.Entscheidungen.Count} Einträge – {string.Join(", ", a.Entscheidungen.Select(z => z.Split(' ')[3]).GroupBy(x => x).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Count()}"))}");
        if (a.Braende.Count > 0) Console.WriteLine($"          Brennende Häuser an Ladepunkten: {string.Join(" | ", a.Braende.Select(z => z.Split(' ')[1] + " " + z.Split(' ')[2]))}");
    }

    static void Vergleichen(string name, Lauf a, Lauf x)
    {
        for (var i = 0; i < Math.Min(a.Jahre.Count, x.Jahre.Count); i++)
            if (a.Jahre[i] != x.Jahre[i])
                throw new Exception($"{name} läuft im Jahr {i + 1} auseinander: {x.Jahre[i][..12]}… statt {a.Jahre[i][..12]}…");
        for (var i = 0; i < Math.Min(a.Befehle.Count, x.Befehle.Count); i++)
            if (a.Befehle[i] != x.Befehle[i])
                throw new Exception($"{name}: Befehl {i + 1} anders: „{x.Befehle[i]}“ statt „{a.Befehle[i]}“");
        Pruefe.Gleich(a.Jahre.Count, x.Jahre.Count, $"{name}: Jahre");
        Pruefe.Gleich(a.Befehle.Count, x.Befehle.Count, $"{name}: Befehle");
        Pruefe.Gleich(a.Ende, x.Ende, $"{name}: Prüfsumme am Ende");
    }

    /// <summary>Eine Partie 10 Jahre lang spielen – <paramref name="laden"/>: zwischendurch speichern und neu laden (Lauf C).</summary>
    static Lauf Spielen(Partie p, bool laden)
    {
        // Daten und Karte frisch aus der Datei: Die Partie verändert ihre Karte (Bauten), jeder Lauf beginnt wie neu.
        var daten = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
        var karte = Stadtkarte.AusDatei(Datenpfad.Karte(p.Karte));
        var (sim, spieler) = Partieaufbau.Erstellen(
            Partieeinstellungen.Standard with { Zufallsstartwert = p.Startwert, Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = p.Schwierigkeit },
            daten, new NeuePartie(Startvermoegen: 15_000, Rivale: p.Rivale, Beruf: p.Beruf), karte: karte); // genug für Bürgerhaus, Stand und Pacht
        sim.Wirtschaft!.RatssitzungAnhalten = _ => true; // wie die Oberfläche: Die Partie hält vor der Sitzung, der Mensch stimmt mit

        var skript = new Befehlsskript(sim.Wirtschaft!.Betriebe.First(b => b.Konto == spieler).Name);
        var blitze = new List<string>(); // Minute und Text jeder Meldung „Blitzschlag!“ (Ziel und Schaden stehen darin)
        var geheimMeldungen = new List<string>(); // Minute, Art und Text jeder Meldung mit Geheimnisart (Siegel)
        var entscheidungen = new List<string>(); // Minute, Nummer, Art und Ausgang jeder Entscheidung (Stufe 3)
        var offenAnLadepunkten = new Dictionary<long, int>(); // offene Entscheidungen an jedem Ladepunkt
        void BlitzeZaehlen(Simulation s)
        {
            s.Wirtschaft!.Meldung += m =>
            {
                if (m.StartsWith("Blitzschlag!", StringComparison.Ordinal) || m.StartsWith("Funkenflug!", StringComparison.Ordinal)) blitze.Add($"{s.Zeit.VergangeneMinuten} {m}");
            };
            s.Wirtschaft.Gemeldet += m => { if (m.GeheimnisArt is { } art) geheimMeldungen.Add($"{m.Minute} {art} {m.Text}"); };
            s.Wirtschaft.Entscheidungen.Geoeffnet += e => entscheidungen.Add($"{s.Zeit.VergangeneMinuten} {e.Nummer} {e.Art} offen {e.Haus.Inhaber}");
            s.Wirtschaft.Entscheidungen.Geschlossen += (e, a) => entscheidungen.Add($"{s.Zeit.VergangeneMinuten} {e.Nummer} {e.Art} {a} {e.Haus.Inhaber}");
        }
        var braende = new List<string>(); // an jedem Ladepunkt: Lager und Vermögen der Häuser, deren Gebäude brennt (Lauf C: nach dem Laden)
        BlitzeZaehlen(sim);
        var befehle = new List<string>();
        var jahre = new List<string>();
        int? erloschen = null;
        var stress = 0;
        var geheimnisse = 0;
        var wissen = 0;
        for (long t = 0; t < p.Jahre * (long)Spielzeit.MinutenProTag; t++)
        {
            if (laden && Ladepunkte.Contains(t)) { (sim, spieler) = SpeichernUndLaden(sim, spieler); BlitzeZaehlen(sim); }
            if (Ladepunkte.Contains(t)) braende.AddRange(Brandlage(sim.Wirtschaft!, t));
            if (Ladepunkte.Contains(t)) offenAnLadepunkten[t] = sim.Wirtschaft!.Entscheidungen.Offen.Count; // Lauf C: nach dem Laden
            foreach (var befehl in skript.Befehle(t, sim.Wirtschaft!, spieler))
            {
                var ergebnis = sim.Wirtschaft!.Ausfuehren(spieler, befehl);
                befehle.Add($"{t} {befehl.GetType().Name} {(ergebnis.Erfolg ? "ok" : "abgelehnt")}");
                if (laden && ergebnis.Erfolg && befehl is BetriebVerkaufen) { (sim, spieler) = SpeichernUndLaden(sim, spieler); BlitzeZaehlen(sim); } // direkt nach dem Löschen
            }

            var jahr = sim.Zeit.Jahr;
            sim.Tick();
            while (sim.Ratssitzung is { } sitzung) // am Jahresende: Punkt für Punkt, der Mensch stimmt mit
            {
                if (sitzung.AktuellerPunkt is { } punkt && skript.Stimme(punkt, spieler, jahr) is { } stimme)
                {
                    var ergebnis = sim.Wirtschaft!.Ausfuehren(spieler, stimme);
                    befehle.Add($"{t} {stimme.GetType().Name} {(ergebnis.Erfolg ? "ok" : "abgelehnt")}");
                }
                sim.RatssitzungWeiter();
            }
            Pruefe.Gleich(t + 1, sim.Zeit.VergangeneMinuten, "Die Uhr läuft mit dem Skript");
            if (sim.Zeit.Jahr != jahr)
            {
                jahre.Add(Pruefsumme(sim));
                if (erloschen is null && !spieler.IstAktiv) erloschen = jahre.Count;
                var fb = sim.Wirtschaft!.Familien;
                stress += sim.Wirtschaft.Konten.Select(fb.Von).OfType<Reichsgilde.Core.Dynastie.Familie>()
                    .Sum(f => f.Lebende.Sum(x => x.Stresslage.Eintraege.Count(e => e.Wert > 0)));
                geheimnisse += sim.Wirtschaft.Geheimnisse.Alle.Count;
                wissen += sim.Wirtschaft.Geheimnisse.AllesWissen.Count;
            }
        }
        return new Lauf(jahre, Pruefsumme(sim), befehle, blitze, erloschen, braende, stress, geheimnisse, wissen, geheimMeldungen, entscheidungen, offenAnLadepunkten);
    }

    /// <summary>Je brennendes Gebäude mit Besitzer: Kennung, Ursache, Lager des Betriebs (nach Ware, ordinal) und Vermögen des Hauses.</summary>
    static IEnumerable<string> Brandlage(Stadtwirtschaft w, long t)
    {
        foreach (var e in w.Blitz.Einschlaege.Concat(w.Blitz.Anstehend))
        {
            if (e.Ziel.Besitzer is not { } k || e.BrandEnde <= t) continue;
            var lager = e.Ziel.Betrieb is { } b
                ? string.Join(",", b.Lager.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key}={x.Value}"))
                : "-";
            yield return $"{t} {e.Ziel.Kennung} {e.Ursache} {e.Minute}-{e.BrandEnde} lager {lager} vermoegen {k.Vermoegen.ToString("R", CultureInfo.InvariantCulture)}";
        }
    }

    /// <summary>Wie ein Spieler, der speichert und gleich wieder lädt – mit allem, was die Oberfläche danach wieder einstellt.</summary>
    static (Simulation, Konto) SpeichernUndLaden(Simulation sim, Konto spieler)
    {
        var json = Speicherung.Speichern(sim, spieler, Zeitpunkt);
        var (geladen, geladenerSpieler) = Speicherung.Laden(json);
        geladen.Wirtschaft!.RatssitzungAnhalten = _ => true;
        Pruefe.Wahr(geladenerSpieler is not null, "Spieler nach dem Laden");
        return (geladen, geladenerSpieler!);
    }

    /// <summary>Die Prüfsumme des Kerns (<see cref="Zustandspruefsumme"/>): SHA-256 über den Spielstand ohne Speicherzeit, Titel, Tempo, Spielerkonto und Warteschlange, kompakt.</summary>
    static string Pruefsumme(Simulation sim) =>
        Zustandspruefsumme.Berechnen(sim) ?? throw new Exception("Keine Prüfsumme – die Ratssitzung steht noch an.");

    static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    static T MitKultur<T>(string kultur, Func<T> lauf)
    {
        var (vorher, vorherUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(kultur);
            return lauf();
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (vorher, vorherUi);
        }
    }

    /// <summary>
    /// Die Prüfsummen für die CI: Pfad aus der Umgebungsvariable GLEICHHEIT_PRUEFSUMME_DATEI, sonst neben dem Testprogramm
    /// (bin/…/gleichheit-pruefsumme.txt). Zeilenende immer \n, damit die Dateien von Linux und Windows Byte für Byte gleich sind.
    /// </summary>
    static void DateiSchreiben()
    {
        var pfad = Environment.GetEnvironmentVariable("GLEICHHEIT_PRUEFSUMME_DATEI") is { Length: > 0 } p
            ? p : Path.Combine(AppContext.BaseDirectory, "gleichheit-pruefsumme.txt");
        var text = new StringBuilder("# Gleichheitstest (Mehrspieler M1): SHA-256 über den Spielstand nach jedem Jahr, am Ende und über die Befehle\n");
        foreach (var zeile in Zeilen.Values.SelectMany(z => z))
            text.Append(zeile).Append('\n');
        File.WriteAllText(pfad, text.ToString());
    }

    /// <summary>
    /// Was der Mensch tut – fest nach der Spielminute, die Mengen nach dem, was er sieht (wie ein Spieler am Bildschirm).
    /// Es merkt sich nur Namen (keine Objekte), denn Lauf C tauscht beim Laden alle Objekte aus.
    /// </summary>
    sealed class Befehlsskript
    {
        private readonly string _werkstatt;
        private string? _zweitbetrieb;
        private long _einstellenUm = -1, _wiederUm = -1;
        private int _abgegebenIm = -1;

        public Befehlsskript(string werkstatt) => _werkstatt = werkstatt;

        /// <summary>Die Befehle dieser Minute – alle vorab aus dem Stand zu Beginn der Minute, wie Daten, die übers Netz kämen.</summary>
        public IReadOnlyList<Befehl> Befehle(long t, Stadtwirtschaft w, Konto spieler)
        {
            var jahr = (int)(t / Spielzeit.MinutenProTag);
            var uhr = t % Spielzeit.MinutenProTag; // Minuten seit 04:00
            var liste = new List<Befehl>();

            // Aufstieg: im ersten Jahr das Wohnhaus zum Bürgerhaus ausbauen (Voraussetzung für den Bürger), dann jede volle Stunde der nächste Stand
            if (jahr == 0 && uhr == Minute(0, 4, 30)) liste.Add(new WohnhausAusbauen());
            if (uhr % 60 == 0) liste.Add(new StandErwerben());

            if (uhr == Minute(0, 9) || uhr == Minute(0, 15))
                if (Handel(w, spieler, verkaufen: uhr == Minute(0, 9)) is { } handel) liste.Add(handel);

            // Ein Zweitbetrieb (der Startbetrieb muss bleiben): der erste, den es zu haben gibt – Pacht oder Kauf; versucht jede halbe Stunde
            if (_zweitbetrieb is null && jahr < Abgabejahre[0] && uhr % 60 == 30
                && w.FreieLiegenschaften.Select(l => l.Name).Order(StringComparer.Ordinal)
                    .FirstOrDefault(name => w.LiegenschaftErwerbVerboten(spieler, name) is null) is { } frei)
            {
                _zweitbetrieb = frei;
                _einstellenUm = t + 1;
                liste.Add(new LiegenschaftErwerben(frei));
            }
            if (t == _einstellenUm && w.Betriebe.FirstOrDefault(b => b.Name == _zweitbetrieb && b.Konto == spieler) is { } zweit)
                liste.Add(new PersonalSetzen(zweit.Name, w.Personal.AnfaengerFuer(zweit.Art, zweit.InDerStadt ? "Geselle" : "Altknecht"), 2));

            // In den Rat: um ein Amt bewerben und die Ratsherren beschenken, die darüber abstimmen (je 1 AP)
            if (uhr == Minute(0, 10) && w.Rat.WaehlbareAemter(spieler).OrderBy(a => a.Name, StringComparer.Ordinal).FirstOrDefault() is { } amt)
                liste.Add(new AmtBewerben(amt.Name));
            if (uhr == Minute(0, 11) && w.Rat.Bewerbungen.TryGetValue(spieler, out var amtsname) && w.Rat.AmtNamens(amtsname) is { } bewerbung)
                foreach (var waehler in w.Rat.Waehler(bewerbung).Where(a => a.Ratsherr is not null).Take(3))
                    liste.Add(new Schenken(waehler.Ratsherr!.Name, 250));

            // Löschen und danach Einfügen: den Zweitbetrieb ab 12 Uhr abgeben, sobald es geht (kein Karren unterwegs), und zehn Minuten
            // später wieder erwerben (Lauf C lädt direkt nach dem Löschen; nach einer Stunde hatte bei 2027 mit dem Landhandel im Jahr 7 schon die KI zugegriffen)
            if (_zweitbetrieb is not null && Abgabejahre.Contains(jahr) && _abgegebenIm != jahr && uhr >= Minute(0, 12)
                && w.Betriebe.FirstOrDefault(b => b.Name == _zweitbetrieb && b.Konto == spieler) is { } abgeben
                && w.VerkaufVerboten(spieler, abgeben) is null)
            {
                liste.Add(new BetriebVerkaufen(_zweitbetrieb));
                _abgegebenIm = jahr;
                _wiederUm = t + 10;
            }
            // Entscheidungen (Stufe 3): um 12:30 die Fragen mit ungerader Nummer beantworten (abwechselnd zahlen und ablehnen), die übrigen laufen ab
            if (uhr == Minute(0, 12, 30))
                foreach (var e in w.Entscheidungen.OffenFuer(spieler).Where(e => e.Nummer % 2 == 1).OrderBy(e => e.Nummer))
                    liste.Add(new EntscheidungTreffen(e.Nummer, e.Nummer % 4 == 1 ? Reichsgilde.Core.Entscheidungen.Entscheidungswesen.Zahlen
                        : Reichsgilde.Core.Entscheidungen.Entscheidungswesen.Ablehnen));
            if (t == _wiederUm && _zweitbetrieb is not null)
            {
                liste.Add(new LiegenschaftErwerben(_zweitbetrieb));
                _einstellenUm = t + 1;
            }
            return liste;
        }

        /// <summary>Morgens verkaufen (bis 10 Stück der Ware) und Rohstoff kaufen, nachmittags nur kaufen – wenn der Karren am Betrieb steht.</summary>
        private Handelsauftrag? Handel(Stadtwirtschaft w, Konto spieler, bool verkaufen)
        {
            if (w.Betriebe.FirstOrDefault(b => b.Name == _werkstatt && b.Konto == spieler) is not { Karren.Count: > 0 } b) return null;
            var verkauf = new Dictionary<string, int>();
            var stueck = Math.Min(10, b.Lager.GetValueOrDefault(b.Produkt));
            if (verkaufen && stueck > 0) verkauf[b.Produkt] = stueck;
            var kauf = new Dictionary<string, int>();
            if (b.Rohstoffe.Count > 0) kauf[b.Rohstoffe[0]] = 5;
            return verkauf.Count + kauf.Count > 0 ? new Handelsauftrag(b.Name, 0, verkauf, kauf) : null;
        }

        /// <summary>In der Ratssitzung: Darf der Mensch hier stimmen, stimmt er abwechselnd dafür und dagegen, bei Wahlen für den letzten Kandidaten.</summary>
        public Befehl? Stimme(Sitzungspunkt punkt, Konto spieler, int jahr)
        {
            if (!punkt.Stimmen.Any(s => s.Haus == spieler && s.Frei)) return null;
            var ja = (jahr + punkt.Nummer) % 2 == 0;
            return punkt.Thema switch
            {
                Sitzungsthema.Statut => new StatutStimme(ja),
                Sitzungsthema.Stadtglaube => new StadtglaubeStimme(ja),
                Sitzungsthema.Pflaster => new PflasterStimme(ja),
                Sitzungsthema.Wahl when punkt.Amt is { } amt && punkt.Kandidaten.Count > 0 => new WahlStimme(amt.Name, punkt.Kandidaten[^1].Name),
                _ when punkt.Antragsart is { } art => new RatsantragStimme(art, ja),
                _ => null,
            };
        }
    }
}
