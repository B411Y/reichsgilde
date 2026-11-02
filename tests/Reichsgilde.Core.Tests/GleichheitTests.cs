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
/// abgeben und wieder erwerben (Löschen und danach Einfügen), Stand, Bewerbung, Geschenk und Stimmen in der Ratssitzung.
///   • Lauf A durchgehend.
///   • Lauf B genauso, aber mit deutscher Ländereinstellung (eine Zahl als Text darf nirgends in den Spielstand geraten).
///   • Lauf C speichert und lädt an fünf Zeitpunkten (mitten im Jahr, in der Gaunernacht, zum Jahresbeginn, am Abend) und direkt nach jedem Löschen.
/// Verglichen wird die Prüfsumme des Kerns (<see cref="Zustandspruefsumme"/>: SHA-256 über den Spielstand ohne Speicherzeit, Titel, Tempo,
/// Spielerkonto und Warteschlange) nach jedem Jahr und am Ende.
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
    /// Dazu mitten im Eis des strengen Winters 1407 (Startwert 1400: der Grenzfluss ist von 7 bis 19 Uhr zu, Datenversion 67).
    /// </summary>
    static readonly long[] Ladepunkte = { Minute(1, 12, 20), Minute(2, 21, 37), Minute(5, 4), Minute(5, 4, 1), Minute(7, 12, 30), Minute(8, 19, 0) };

    /// <summary>Wann der Zweitbetrieb abgegeben (gelöscht) und wieder erworben (eingefügt) wird.</summary>
    static readonly int[] Abgabejahre = { 3, 7 };

    /// <summary>Was ein Lauf ergibt: Prüfsumme nach jedem Jahr, am Ende, und die Befehle mit Erfolg.</summary>
    sealed record Lauf(IReadOnlyList<string> Jahre, string Ende, IReadOnlyList<string> Befehle);

    /// <summary>Alle Zeilen für die Datei der CI, gesammelt über die Partien.</summary>
    static readonly SortedDictionary<int, IReadOnlyList<string>> Zeilen = new();

    public static IEnumerable<(string, Action)> Alle() => Partien.Select(p => (
        $"Gleichheit: Startwert {p.Startwert} ({p.Schwierigkeit}, {p.Rivale}, {p.Beruf}{(p.Karte == "greybox" ? "" : $", {p.Karte}")}) – {p.Jahre} Jahre durchgehend, mit deutscher Ländereinstellung " +
        "und mit Speichern/Laden (auch direkt nach dem Löschen eines Betriebs) ergeben dieselbe Prüfsumme",
        (Action)(() => Pruefen(p))));

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

        // Das Skript soll seine Wege wirklich gehen – sonst prüft der Test weniger, als er verspricht.
        string Erfolge(string art) => $"{a.Befehle.Count(z => z.Contains($" {art} ok"))} × {art}; alle: " +
            string.Join(", ", a.Befehle.GroupBy(z => z[(z.IndexOf(' ') + 1)..]).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Count()}"));
        foreach (var art in new[] { "Handelsauftrag", "BetriebVerkaufen", "LiegenschaftErwerben" })
            Pruefe.Wahr(a.Befehle.Any(z => z.Contains($" {art} ok")), $"Kein erfolgreicher Befehl {art} im Skript – Skript anpassen ({Erfolge(art)})");
        var abgaben = Abgabejahre.Count(j => j < p.Jahre);
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
        var befehle = new List<string>();
        var jahre = new List<string>();
        for (long t = 0; t < p.Jahre * (long)Spielzeit.MinutenProTag; t++)
        {
            if (laden && Ladepunkte.Contains(t)) (sim, spieler) = SpeichernUndLaden(sim, spieler);
            foreach (var befehl in skript.Befehle(t, sim.Wirtschaft!, spieler))
            {
                var ergebnis = sim.Wirtschaft!.Ausfuehren(spieler, befehl);
                befehle.Add($"{t} {befehl.GetType().Name} {(ergebnis.Erfolg ? "ok" : "abgelehnt")}");
                if (laden && ergebnis.Erfolg && befehl is BetriebVerkaufen) (sim, spieler) = SpeichernUndLaden(sim, spieler); // direkt nach dem Löschen
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
            if (sim.Zeit.Jahr != jahr) jahre.Add(Pruefsumme(sim));
        }
        return new Lauf(jahre, Pruefsumme(sim), befehle);
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

            // Löschen und danach Einfügen: den Zweitbetrieb ab 12 Uhr abgeben, sobald es geht (kein Karren unterwegs), und eine Stunde
            // später wieder erwerben (Lauf C lädt direkt nach dem Löschen)
            if (_zweitbetrieb is not null && Abgabejahre.Contains(jahr) && _abgegebenIm != jahr && uhr >= Minute(0, 12)
                && w.Betriebe.FirstOrDefault(b => b.Name == _zweitbetrieb && b.Konto == spieler) is { } abgeben
                && w.VerkaufVerboten(spieler, abgeben) is null)
            {
                liste.Add(new BetriebVerkaufen(_zweitbetrieb));
                _abgegebenIm = jahr;
                _wiederUm = t + 60;
            }
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
