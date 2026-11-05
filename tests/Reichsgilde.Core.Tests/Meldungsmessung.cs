using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

// ═════════════════════════════════════════════════════════════════════════════
//  Meldungsmessung – wie viele Meldungen je Spieljahr kommen (Messung, kein Test; Vorarbeit Benachrichtigungen, 5.11.2026)
//
//  Aufruf:  dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen meldungen [Jahre] [Startwerte]
//                [--karten=greybox,koeln] [--rollen=Wirt,Schmied,Ausbau] [--parallel=n] [--muster=datei.txt]
//
//  Rollen: „Wirt“ und „Schmied“ = der Spieler mit einem Betrieb (KI-geführt, keine Expansion), „Ausbau“ = Schmied mit dem Profil der KI-Häuser
//  (kauft und baut weitere Betriebe). Alle KI-Häuser der Karte spielen mit.
//
//  Jede Meldung des Kerns (Ereignis Stadtwirtschaft.Meldung, dasselbe, das die Oberfläche unten links und im Protokoll zeigt) bekommt
//  · eine Art (Steuer, Geburt, Tod … – nach dem Text, siehe Einordnen),
//  · einen Bezug: Eigen (betrifft das Haus des Spielers), Stadt (niemand Bestimmtes), Fremd (ein anderes Haus), EigenerZug (ein Zug der KI,
//    die hier den Spieler führt – im Spiel wäre das ein Befehl des Spielers mit eigener Antwort, er zählt deshalb nicht mit),
//  · eine vorgeschlagene Stufe (Verlauf, Siegel, Brief, Entscheidung) nach Art und Bezug (siehe Stufe).
//  Gezählt wird je Spieljahr (= ein Spieltag) und je Spielstunde; die Spitzen sind das höchste Jahr bzw. die höchste Stunde aller Partien.
//  Die Messung liest nur – sie ändert weder Daten noch Spiel.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Wen eine Meldung betrifft.</summary>
enum Meldungsbezug { Eigen, Stadt, Fremd, EigenerZug }

/// <summary>Vorgeschlagene Stufe im neuen Benachrichtigungssystem (Variante C).</summary>
enum Meldungsstufe { Verlauf, Siegel, Brief, Entscheidung }

static class Meldungsmessung
{
    /// <summary>Die Arten in der Reihenfolge der Ausgabe.</summary>
    public static readonly string[] Arten =
    {
        "Tod", "Geburt", "Heirat", "Krankheit", "Pest", "Familie", "Stand", "Rat", "Steuer", "Gericht", "Spitzel", "Diebstahl", "Blitz",
        "Schulden", "Bank", "Stillstand", "Personal", "Karren", "Schiff", "Jahreszeit", "Bau", "Besitz", "Kirche", "KI-Zug", "Entwickler", "Sonstiges",
    };

    static readonly (string Art, Func<string, bool> Passt)[] Regeln =
    {
        ("Entwickler", m => m.StartsWith("Entwicklerhilfe", StringComparison.Ordinal)),
        // Feuer nach Blitz (Brand, Übersprung, gelöscht, Reparatur), vor dem Stillstand: „Nach dem Brand wird repariert – die Arbeit ruht.“
        ("Blitz", m => m.StartsWith("Blitzschlag! ", StringComparison.Ordinal) || m.StartsWith("Funkenflug! ", StringComparison.Ordinal)
                       || m.Contains(" brennt nicht mehr – ", StringComparison.Ordinal) || m.Contains("nach dem Brand wieder instand gesetzt", StringComparison.Ordinal)
                       || m.Contains("Nach dem Brand wird repariert", StringComparison.Ordinal)),
        ("Stillstand", m => m.EndsWith("die Arbeit ruht.", StringComparison.Ordinal)),
        // Unterwelt, Diebe, Duft und Überfälle (vor Tod und Krankheit: „für immer verschwunden“, „Krötenschleim“, „Giftanschlag“)
        ("Diebstahl", m => m.StartsWith("Eure ", StringComparison.Ordinal) || m.StartsWith("Einbruch", StringComparison.Ordinal)
                           || m.StartsWith("Anschlag auf ", StringComparison.Ordinal) || m.StartsWith("Brandanschlag ", StringComparison.Ordinal)
                           || m.StartsWith("Feuer ", StringComparison.Ordinal) || m.StartsWith("Hinterhalt auf ", StringComparison.Ordinal)
                           || m.StartsWith("Überfall ", StringComparison.Ordinal) || m.StartsWith("Entführung! ", StringComparison.Ordinal)
                           || m.StartsWith("Gift im Haus ", StringComparison.Ordinal) || m.StartsWith("Giftanschlag ", StringComparison.Ordinal)
                           || m.StartsWith("Krötenschleim ", StringComparison.Ordinal) || m.StartsWith("Der Krötenschleim ", StringComparison.Ordinal)
                           || m.StartsWith("Ein Dolch ", StringComparison.Ordinal) || m.StartsWith("Der Dolch", StringComparison.Ordinal)
                           || m.Contains("wurde ein Fremder", StringComparison.Ordinal) || m.Contains("Taschendieb", StringComparison.Ordinal)
                           || m.Contains("Erpresserbrief", StringComparison.Ordinal) || m.Contains("Schweigegeld", StringComparison.Ordinal)
                           || m.Contains("Schutzgeld", StringComparison.Ordinal) || m.Contains("Lösegeld", StringComparison.Ordinal)
                           || m.Contains("forderung von", StringComparison.Ordinal) || m.StartsWith("Razzia ", StringComparison.Ordinal)
                           || m.Contains("Hetzschrift", StringComparison.Ordinal) || m.Contains(" zu sprechen.", StringComparison.Ordinal)
                           || m.Contains("ausgespäht", StringComparison.Ordinal) || m.Contains("Wegelagerer", StringComparison.Ordinal)),
        ("Tod", m => m.StartsWith("Trauer im Haus ", StringComparison.Ordinal) || m.Contains(" gestorben", StringComparison.Ordinal)),
        ("Geburt", m => m.StartsWith("Freude im Haus ", StringComparison.Ordinal)),
        ("Heirat", m => m.StartsWith("Hochzeit im Haus ", StringComparison.Ordinal) || m.Contains("anderweitig geheiratet", StringComparison.Ordinal)),
        ("Pest", m => m.StartsWith("Die Pest ist in der Stadt", StringComparison.Ordinal)),
        ("Krankheit", m => m.Contains(" ist krank: ", StringComparison.Ordinal) || m.EndsWith(" überstanden.", StringComparison.Ordinal)
                           || m.Contains(" hat die Pest! ", StringComparison.Ordinal)),
        ("Familie", m => m.Contains("hat die Ausbildung abgeschlossen", StringComparison.Ordinal) || m.Contains("ist erwachsen geworden", StringComparison.Ordinal)
                         || m.StartsWith("Neuer Charakterzug", StringComparison.Ordinal) || m.StartsWith("Streit im Haus ", StringComparison.Ordinal)),
        ("Stand", m => (m.Contains(" ist nun ", StringComparison.Ordinal) && m.Contains("(Gebühr", StringComparison.Ordinal))
                       || m.Contains("erlernt das Handwerk", StringComparison.Ordinal) || m.Contains("legt die Meisterprüfung ab", StringComparison.Ordinal)),
        ("Schulden", m => m.Contains(" ist bankrott!", StringComparison.Ordinal) || m.StartsWith("Der Kämmerer mahnt", StringComparison.Ordinal)
                          || m.Contains("die Kasse ist knapp", StringComparison.Ordinal)),
        ("Bank", m => (m.Contains(" leiht ", StringComparison.Ordinal) && m.Contains(" Zins", StringComparison.Ordinal))
                      || m.Contains("den Kredit bei", StringComparison.Ordinal) || m.StartsWith("Der Büttel pfändet", StringComparison.Ordinal)
                      || m.Contains("Ein Schuldner aus der Stadt", StringComparison.Ordinal) || m.Contains(" Kredit", StringComparison.Ordinal)),
        // Berichte der eigenen Spitzel und Lauscher am Gerichtstag – nur für den Auftraggeber (vor Gericht)
        ("Spitzel", m => m.StartsWith("Der Spitzel von ", StringComparison.Ordinal) || m.StartsWith("In der dunklen Ecke ", StringComparison.Ordinal)),
        ("Gericht", m => m.StartsWith("Gerichtstag", StringComparison.Ordinal) || m.Contains(" klagt ", StringComparison.Ordinal)
                         || m.Contains("Pranger", StringComparison.Ordinal) || m.Contains("Kerker", StringComparison.Ordinal)
                         || m.StartsWith("Der Gefängniswärter ", StringComparison.Ordinal)),
        // Steuer vor Rat: Anträge und Beschlüsse zur Steuer (Ratssitzung, Statut, Befugnis des Kämmerers)
        ("Steuer", m => (m.StartsWith("Ratssitzung ", StringComparison.Ordinal) || m.Contains("Statut", StringComparison.Ordinal) || m.Contains("Kämmerer", StringComparison.Ordinal))
                        && m.Contains("Steuer", StringComparison.Ordinal)),
        ("Personal", m => m.StartsWith("Unfall in ", StringComparison.Ordinal) || m.Contains(" kündigt – ", StringComparison.Ordinal)
                          || (m.Contains(" Nr. ", StringComparison.Ordinal) && m.Contains(" ist nun ", StringComparison.Ordinal))
                          || m.Contains("unter dem neuen Statut entlassen", StringComparison.Ordinal)
                          || m.StartsWith("Ausbruch in ", StringComparison.Ordinal) || m.StartsWith("Aufstand in ", StringComparison.Ordinal)),
        ("Rat", m => m.StartsWith("Ratssitzung ", StringComparison.Ordinal) || m.StartsWith("Wahl ", StringComparison.Ordinal)
                     || m.Contains(" in den Rat nach", StringComparison.Ordinal) || m.StartsWith("Der Rat ", StringComparison.Ordinal)
                     || m.StartsWith("Am Stammtisch ", StringComparison.Ordinal) || m.Contains("Statut", StringComparison.Ordinal)),
        ("Jahreszeit", m => m.StartsWith("Festmarkt: ", StringComparison.Ordinal) || m.Contains(" ist zugefroren!", StringComparison.Ordinal)
                            || m.StartsWith("Eisgang ", StringComparison.Ordinal) || Jahresmeldung.IsMatch(m)),
        ("Karren", m => m.Contains(") am Markt: ", StringComparison.Ordinal) || m.Contains("Handelsauftrag verfällt", StringComparison.Ordinal)),
        ("Schiff", m => m.Contains(" überfallen ", StringComparison.Ordinal) || m.Contains(" greifen ", StringComparison.Ordinal)
                        || m.Contains(" ist zurück: ", StringComparison.Ordinal) || m.Contains(" bleibt am Kai", StringComparison.Ordinal)
                        || (m.Contains(" kauft das ", StringComparison.Ordinal) && m.Contains('„'))),
        ("Bau", m => m.Contains(" baut ", StringComparison.Ordinal) || m.Contains("fertig gebaut", StringComparison.Ordinal)
                     || m.Contains(" bezieht das neue ", StringComparison.Ordinal) || m.Contains("abreißen", StringComparison.Ordinal)
                     || m.Contains(" ausbauen (", StringComparison.Ordinal) || m.Contains("Der Ausbau mit ", StringComparison.Ordinal)
                     || m.StartsWith("Die Stadt hat gebaut", StringComparison.Ordinal)),
        ("Kirche", m => m.Contains("Ablass", StringComparison.Ordinal) || m.Contains("Heiligenduft", StringComparison.Ordinal)
                        || m.Contains(" predigt ", StringComparison.Ordinal) || m.Contains(" tritt zum ", StringComparison.Ordinal)
                        || m.Contains(" taufen.", StringComparison.Ordinal)),
        ("Besitz", m => m.Contains("stehen nun zum Verkauf", StringComparison.Ordinal) || m.Contains(" pachtet ", StringComparison.Ordinal)
                        || m.Contains(" kündigt die Pacht", StringComparison.Ordinal) || m.Contains(" verkauft ", StringComparison.Ordinal)
                        || m.Contains(" kauft ", StringComparison.Ordinal)),
    };

    /// <summary>„Frühling 1403.“ bzw. „Winter 1406: … sind gefragt; …“ – die Meldung zu Beginn eines Jahres (Jahreszeitenwesen).</summary>
    static readonly Regex Jahresmeldung = new(@"^(Frühling|Sommer|Herbst|Winter) \d{4}[:.]", RegexOptions.CultureInvariant);

    /// <summary>Die Art einer Meldung nach ihrem Text (ohne Kenntnis der Partie; „KI-Zug“ erkennt erst die Messung).</summary>
    public static string Einordnen(string meldung)
    {
        foreach (var (art, passt) in Regeln)
            if (passt(meldung)) return art;
        return "Sonstiges";
    }

    /// <summary>Vorgeschlagene Stufe nach Art und Bezug. Fremde Meldungen kommen nur in den Verlauf (außer den Gefahren der ganzen Stadt).</summary>
    public static Meldungsstufe Stufe(string art, Meldungsbezug bezug)
    {
        var eigen = bezug == Meldungsbezug.Eigen;
        return art switch
        {
            "Tod" or "Geburt" or "Heirat" or "Stand" => eigen ? Meldungsstufe.Brief : Meldungsstufe.Verlauf,
            "Pest" => Meldungsstufe.Brief,
            "Blitz" => eigen ? Meldungsstufe.Brief : Meldungsstufe.Verlauf,
            "Krankheit" or "Familie" or "Spitzel" => eigen ? Meldungsstufe.Siegel : Meldungsstufe.Verlauf,
            "Diebstahl" or "Gericht" or "Schulden" => eigen ? Meldungsstufe.Brief : Meldungsstufe.Verlauf,
            "Rat" => eigen ? Meldungsstufe.Brief : Meldungsstufe.Verlauf,
            "Steuer" => bezug == Meldungsbezug.Fremd ? Meldungsstufe.Verlauf : Meldungsstufe.Siegel,
            "Stillstand" or "Karren" or "Schiff" or "Personal" or "Bank" => eigen ? Meldungsstufe.Siegel : Meldungsstufe.Verlauf,
            "Jahreszeit" => Meldungsstufe.Siegel,
            _ => Meldungsstufe.Verlauf, // Bau, Besitz, Kirche, KI-Zug, Entwickler, Sonstiges
        };
    }

    /// <summary>Zähler einer Partie: je Spieljahr und Art/Bezug, je Spielstunde und Stufe, Muster der Texte.</summary>
    sealed class Zaehler
    {
        public bool Angemeldet;
        public int Jahr, Stunde;
        /// <summary>[Jahr-1][(Art, Bezug)]</summary>
        public readonly List<SortedDictionary<(string, Meldungsbezug), int>> JeJahr = new();
        /// <summary>[(Jahr, Stunde)][Stufe] – nur, was der Spieler sieht (ohne eigene Züge).</summary>
        public readonly SortedDictionary<(int, int), int[]> JeStunde = new();
        /// <summary>[(Jahr, Stunde)][Art] – Eigen und Stadt.</summary>
        public readonly SortedDictionary<(int, int, string), int> ArtJeStunde = new();
        public readonly SortedDictionary<string, (string Art, int Anzahl)> Muster = new(StringComparer.Ordinal);
    }

    public static int Erstellen(string[] args)
    {
        string? Schalter(string name) => args.FirstOrDefault(a => a.StartsWith($"--{name}=", StringComparison.Ordinal))?.Split('=', 2)[1];
        var worte = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
        var jahre = worte.Count > 0 ? int.Parse(worte[0], CultureInfo.InvariantCulture) : 20;
        var startwerte = worte.Count > 1 ? int.Parse(worte[1], CultureInfo.InvariantCulture) : 8;
        var karten = (Schalter("karten") ?? "greybox,koeln").Split(',');
        var rollen = (Schalter("rollen") ?? "Wirt,Schmied,Ausbau").Split(',');
        var parallel = int.Parse(Schalter("parallel") ?? "2", CultureInfo.InvariantCulture);
        var musterDatei = Schalter("muster");
        var quellen = new Messquellen();

        var laeufe = new List<(string Rolle, Messaufbau A)>();
        foreach (var karte in karten)
            foreach (var rolle in rollen)
                for (var s = 1; s <= startwerte; s++)
                    laeufe.Add((rolle, Aufbau(rolle, karte, s * 101, jahre)));
        Console.Error.WriteLine($"Meldungsmessung: {laeufe.Count} Partien zu {jahre} Jahren, {parallel} parallel …");
        var ergebnisse = new ConcurrentDictionary<(string, Messaufbau), (Partieergebnis P, Zaehler Z)>();
        var fertig = 0;
        Parallel.ForEach(laeufe, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, parallel) }, l =>
        {
            var z = new Zaehler();
            Partieergebnis r;
            try { r = Messpartie.Spielen(l.A, quellen, jeMinute: w => Takt(w, z)); }
            catch (Exception ex) { r = new Partieergebnis { Aufbau = l.A, Fehler = ex.ToString() }; }
            ergebnisse[l] = (r, z);
            var n = Interlocked.Increment(ref fertig);
            if (n % 4 == 0 || n == laeufe.Count) Console.Error.WriteLine($"  {n}/{laeufe.Count}");
        });
        foreach (var (a, (r, _)) in ergebnisse.Where(x => x.Value.P.Fehler is not null).Take(5)) Console.Error.WriteLine($"FEHLER in {a}: {r.Fehler}");
        var gut = ergebnisse.Where(x => x.Value.P.Fehler is null)
            .OrderBy(x => x.Key.Item2.Karte, StringComparer.Ordinal).ThenBy(x => x.Key.Item1, StringComparer.Ordinal).ThenBy(x => x.Key.Item2.Startwert)
            .Select(x => (Karte: x.Key.Item2.Karte, Rolle: x.Key.Item1, x.Value.Z)).ToList();

        Console.WriteLine($"Datenversion {Wirtschaftsdaten.AusJson(quellen.Wirtschaft).Version}, {jahre} Jahre, {startwerte} Startwerte, Karten {string.Join(",", karten)}, " +
                          $"main {Balancingbericht.Git("rev-parse --short origin/main")}, Stand {Balancingbericht.Git("rev-parse --short HEAD")}");
        Console.WriteLine();
        Bericht(gut.Select(x => x.Z).ToList(), "alle Karten und Rollen");
        foreach (var g in gut.GroupBy(x => x.Rolle).OrderBy(g => Array.IndexOf(rollen, g.Key)))
            Summen(g.Select(x => x.Z).ToList(), $"Rolle {g.Key}");
        foreach (var g in gut.GroupBy(x => x.Karte).OrderBy(g => Array.IndexOf(karten, g.Key)))
            Summen(g.Select(x => x.Z).ToList(), $"Karte {g.Key}");

        if (musterDatei is not null)
        {
            var alle = new SortedDictionary<string, (string Art, int Anzahl)>(StringComparer.Ordinal);
            foreach (var (_, _, z) in gut)
                foreach (var (m, (art, n)) in z.Muster)
                    alle[m] = (art, (alle.TryGetValue(m, out var alt) ? alt.Anzahl : 0) + n);
            var sb = new StringBuilder();
            foreach (var (m, (art, n)) in alle.OrderBy(x => x.Value.Art, StringComparer.Ordinal).ThenByDescending(x => x.Value.Anzahl).ThenBy(x => x.Key, StringComparer.Ordinal))
                sb.Append(art).Append('\t').Append(n).Append('\t').Append(m).Append('\n');
            File.WriteAllText(musterDatei, sb.ToString());
            Console.Error.WriteLine($"Muster: {alle.Count} nach {musterDatei}");
        }
        return ergebnisse.Values.Any(x => x.P.Fehler is not null) ? 1 : 0;
    }

    /// <summary>Wirt und Schmied: ein Betrieb; Ausbau: Schmied mit dem Profil der KI-Häuser (kauft und baut weitere Betriebe).</summary>
    public static Messaufbau Aufbau(string rolle, string karte, int startwert, int jahre) => rolle switch
    {
        "Wirt" => new Messaufbau(karte, startwert, jahre, Beruf: "Wirt"),
        "Schmied" => new Messaufbau(karte, startwert, jahre, Beruf: "Schmied"),
        "Ausbau" => new Messaufbau(karte, startwert, jahre, Beruf: "Schmied", Bezug: Bezugsart.WieKi),
        _ => throw new ArgumentException($"Unbekannte Rolle „{rolle}“ (Wirt, Schmied, Ausbau)."),
    };

    /// <summary>Vor jedem Takt: Uhrzeit merken; beim ersten Takt die Meldungen abonnieren und die Startzüge der KI nachtragen (wie die Oberfläche).</summary>
    static void Takt(Stadtwirtschaft w, Zaehler z)
    {
        // Was während des Takts gemeldet wird, gehört zur Uhrzeit davor – auch die Jahresrechnung im letzten Takt des Tages (22:59)
        z.Jahr = w.Uhr.Jahr - Spielzeit.StartJahr;
        z.Stunde = Math.Min(w.Uhr.MinuteImTag / 60, Spielzeit.StundenProTag - 1);
        if (z.Angemeldet) return;
        z.Angemeldet = true;
        w.Meldung += m => Zaehlen(w, z, m);
        foreach (var rivale in w.Rivalen)
            foreach (var zug in rivale.Entscheidungen.Reverse())
                Zaehlen(w, z, zug);
    }

    /// <summary>Eine Meldung einordnen und zählen.</summary>
    static void Zaehlen(Stadtwirtschaft w, Zaehler z, string m)
    {
        var jahr = z.Jahr; // 0 = erstes Jahr
        while (z.JeJahr.Count <= jahr) z.JeJahr.Add(new SortedDictionary<(string, Meldungsbezug), int>());
        var spieler = w.Konten[0];
        var (art, bezug) = Einordnen(w, spieler, m);
        var schluessel = (art, bezug);
        z.JeJahr[jahr][schluessel] = z.JeJahr[jahr].GetValueOrDefault(schluessel) + 1;
        if (bezug != Meldungsbezug.EigenerZug)
        {
            if (!z.JeStunde.TryGetValue((jahr, z.Stunde), out var stufen)) z.JeStunde[(jahr, z.Stunde)] = stufen = new int[4];
            stufen[(int)Stufe(art, bezug)]++;
            if (bezug != Meldungsbezug.Fremd)
                z.ArtJeStunde[(jahr, z.Stunde, art)] = z.ArtJeStunde.GetValueOrDefault((jahr, z.Stunde, art)) + 1;
        }
        var muster = Muster(w, m);
        z.Muster[muster] = (art, (z.Muster.TryGetValue(muster, out var alt) ? alt.Anzahl : 0) + 1);
    }

    /// <summary>Art und Bezug einer Meldung in der laufenden Partie.</summary>
    static (string Art, Meldungsbezug Bezug) Einordnen(Stadtwirtschaft w, Konto spieler, string m)
    {
        // Züge der KI („Haus Brendel: …“) – beim Spieler wären es seine eigenen Befehle
        foreach (var r in w.Rivalen)
            if (r.Entscheidungen.Count > 0 && ReferenceEquals(r.Entscheidungen[0], m))
                return ("KI-Zug", r.Konto == spieler ? Meldungsbezug.EigenerZug : Meldungsbezug.Fremd);
        var art = Einordnen(m);
        // Familienereignis oder Nachricht der Unterwelt an ein bestimmtes Haus: der Kern kennt das Haus
        if (w.Familien.DiesesJahr is { Count: > 0 } fam && ReferenceEquals(fam[^1].Text, m))
            return (art, fam[^1].Konto == spieler ? Meldungsbezug.Eigen : Meldungsbezug.Fremd);
        if (w.Gauner.DiesesJahr is { Count: > 0 } nacht && ReferenceEquals(nacht[^1].Text, m) && nacht[^1].Fuer is { } fuer)
            return (art, fuer == spieler ? Meldungsbezug.Eigen : Meldungsbezug.Fremd);
        // Spitzelbericht und Gerüchte gehören dem, der lauschen lässt – nicht dem Belauschten
        if (art == "Spitzel" && Auftraggeber(m) is { } wer)
            return (art, wer == spieler.Inhaber ? Meldungsbezug.Eigen : Meldungsbezug.Fremd);
        // Sonst nach dem Text: Name des Hauses, Nachname, Betriebe, Schiffe – der Spieler zuerst
        if (Nennt(w, spieler, m)) return (art, Meldungsbezug.Eigen);
        foreach (var k in w.Konten)
            if (k != spieler && Nennt(w, k, m)) return (art, Meldungsbezug.Fremd);
        return (art, Meldungsbezug.Stadt);
    }

    /// <summary>„Der Spitzel von X berichtet …“ bzw. „In der dunklen Ecke von … hört X Gerüchte …“ → X.</summary>
    public static string? Auftraggeber(string m)
    {
        const string spitzel = "Der Spitzel von ", berichtet = " berichtet ", hoert = " hört ", geruechte = " Gerüchte";
        if (m.StartsWith(spitzel, StringComparison.Ordinal) && m.IndexOf(berichtet, StringComparison.Ordinal) is > 0 and var e)
            return m[spitzel.Length..e];
        if (m.IndexOf(hoert, StringComparison.Ordinal) is > 0 and var h && m.IndexOf(geruechte, h, StringComparison.Ordinal) is > 0 and var g)
            return m[(h + hoert.Length)..g];
        return null;
    }

    static bool Nennt(Stadtwirtschaft w, Konto k, string m)
    {
        if (m.Contains(k.Inhaber, StringComparison.Ordinal)) return true;
        if (w.Familien.Von(k) is { } f && Regex.IsMatch(m, $@"\b{Regex.Escape(f.Nachname)}\b")) return true;
        foreach (var b in w.Betriebe)
            if (b.Konto == k && m.Contains(b.Name, StringComparison.Ordinal)) return true;
        foreach (var s in w.Schiffahrt.SchiffeVon(k))
            if (m.Contains(s.Name, StringComparison.Ordinal)) return true;
        return false;
    }

    static readonly Regex Zahlen = new(@"\d[\d\.,]*", RegexOptions.CultureInvariant);

    /// <summary>Text ohne Namen und Zahlen (zum Prüfen der Einordnung, <c>--muster=</c>).</summary>
    static string Muster(Stadtwirtschaft w, string m)
    {
        foreach (var b in w.Betriebe.OrderByDescending(b => b.Name.Length)) m = m.Replace(b.Name, "<Betrieb>", StringComparison.Ordinal);
        foreach (var k in w.Konten) m = m.Replace(k.Inhaber, "<Haus>", StringComparison.Ordinal);
        foreach (var k in w.Konten)
            if (w.Familien.Von(k) is { } f) m = m.Replace(f.Nachname, "<Name>", StringComparison.Ordinal);
        m = Zahlen.Replace(m, "#");
        return m.Length > 160 ? m[..160] + "…" : m;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Ausgabe
    // ═════════════════════════════════════════════════════════════════════════

    static string F(double x, string format = "0.0") => x.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>Tabelle je Art: Mittel je Jahr nach Bezug, Spitze je Jahr und je Stunde (Eigen + Stadt), Stufe.</summary>
    static void Bericht(List<Zaehler> partien, string titel)
    {
        var jahre = partien.Sum(z => z.JeJahr.Count);
        Console.WriteLine($"## Je Art – {titel} ({partien.Count} Partien, {jahre} Spieljahre)");
        Console.WriteLine();
        Console.WriteLine("| Art | Eigen/Jahr | Stadt/Jahr | Fremd/Jahr | eigene Züge/Jahr | Spitze Jahr (Eigen+Stadt) | Spitze Stunde (Eigen+Stadt) | Stufe Eigen | Stufe Stadt | Stufe Fremd |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|");
        foreach (var art in Arten)
        {
            double Mittel(Meldungsbezug b) => jahre == 0 ? 0 : partien.Sum(z => z.JeJahr.Sum(j => j.GetValueOrDefault((art, b)))) / (double)jahre;
            var spitzeJahr = partien.SelectMany(z => z.JeJahr.Select(j => j.GetValueOrDefault((art, Meldungsbezug.Eigen)) + j.GetValueOrDefault((art, Meldungsbezug.Stadt)))).DefaultIfEmpty(0).Max();
            var spitzeStunde = partien.SelectMany(z => z.ArtJeStunde.Where(x => x.Key.Item3 == art).Select(x => x.Value)).DefaultIfEmpty(0).Max();
            var summe = Mittel(Meldungsbezug.Eigen) + Mittel(Meldungsbezug.Stadt) + Mittel(Meldungsbezug.Fremd) + Mittel(Meldungsbezug.EigenerZug);
            if (summe == 0) continue;
            Console.WriteLine($"| {art} | {F(Mittel(Meldungsbezug.Eigen))} | {F(Mittel(Meldungsbezug.Stadt))} | {F(Mittel(Meldungsbezug.Fremd))} | {F(Mittel(Meldungsbezug.EigenerZug))} | " +
                              $"{spitzeJahr} | {spitzeStunde} | {Stufe(art, Meldungsbezug.Eigen)} | {Stufe(art, Meldungsbezug.Stadt)} | {Stufe(art, Meldungsbezug.Fremd)} |");
        }
        Console.WriteLine();
        Summen(partien, titel);
    }

    /// <summary>Je Stufe: Mittel je Jahr, Spitze je Jahr, Spitze je Stunde, Mittel und Spitze in der Stunde des Jahreswechsels (22–23 Uhr).</summary>
    static void Summen(List<Zaehler> partien, string titel)
    {
        var jahre = partien.Sum(z => z.JeJahr.Count);
        Console.WriteLine($"### Je Stufe – {titel} ({partien.Count} Partien, {jahre} Spieljahre)");
        Console.WriteLine();
        Console.WriteLine("| Stufe | je Jahr (Mittel) | Spitze Jahr | Spitze Stunde | Stunde 22 Uhr + Jahreswechsel (Mittel) | Spitze 22 Uhr + Jahreswechsel |");
        Console.WriteLine("|---|---|---|---|---|---|");
        var letzte = Spielzeit.StundenProTag - 1;
        foreach (var stufe in Enum.GetValues<Meldungsstufe>())
        {
            var s = (int)stufe;
            var jeJahr = partien.SelectMany(z => z.JeStunde.GroupBy(x => x.Key.Item1).Select(g => g.Sum(x => x.Value[s]))).ToList();
            var mittel = jahre == 0 ? 0 : jeJahr.Sum() / (double)jahre;
            var spitzeStunde = partien.SelectMany(z => z.JeStunde.Values.Select(v => v[s])).DefaultIfEmpty(0).Max();
            var ende = partien.SelectMany(z => z.JeStunde.Where(x => x.Key.Item2 == letzte).Select(x => x.Value[s])).ToList();
            Console.WriteLine($"| {stufe} | {F(mittel)} | {jeJahr.DefaultIfEmpty(0).Max()} | {spitzeStunde} | {F(jahre == 0 ? 0 : ende.Sum() / (double)jahre)} | {ende.DefaultIfEmpty(0).Max()} |");
        }
        Console.WriteLine();
    }
}
