using System.IO.Compression;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Entwickler;
using Reichsgilde.Core.Mehrspieler;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Tests fürs Entwicklerfenster im Kern: jeder Entwicklerbefehl, die Sperre, das Kennzeichen „mit Entwicklerhilfe“ im Spielstand,
/// Vorspulen, die Prüfsumme des Zustands, der Einblick in die KI und ein Befehlsprotokoll mit Entwicklerbefehlen.
/// </summary>
static class EntwicklerTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));
    static readonly DateTime Zeitpunkt = new(2026, 10, 29, 12, 0, 0);

    static readonly Lazy<string> AlterStand = new(() =>
    {
        var datei = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
            "tests", "Reichsgilde.Core.Tests", "Spielstaende", "format2-greybox-herbst1406.json.gz");
        using var gz = new GZipStream(File.OpenRead(datei), CompressionMode.Decompress);
        return new StreamReader(gz).ReadToEnd();
    });

    /// <summary>Eine Partie mit eingeschalteten Entwickler-Optionen (<paramref name="erlaubt"/>), nach 300 Minuten (09:00).</summary>
    static (Simulation Sim, Konto Spieler) NeuePartie(bool erlaubt = true, int startwert = 1400)
    {
        var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos },
            Daten.Value, new NeuePartie());
        for (var i = 0; i < 300; i++) sim.Tick();
        sim.EntwicklerErlaubt = erlaubt;
        return (sim, spieler);
    }

    static Befehlsergebnis Ok(Simulation sim, Konto absender, Befehl befehl)
    {
        var e = sim.Bote.SofortAusfuehren(absender, befehl);
        Pruefe.Wahr(e.Erfolg, $"{befehl.GetType().Name} abgelehnt: {e.Meldung}");
        return e;
    }

    static void Abgelehnt(Simulation sim, Konto absender, Befehl befehl, string textInMeldung)
    {
        var e = sim.Bote.SofortAusfuehren(absender, befehl);
        Pruefe.Wahr(!e.Erfolg, $"{befehl.GetType().Name} hätte abgelehnt werden sollen: {e.Meldung}");
        Pruefe.Wahr(e.Meldung.Contains(textInMeldung, StringComparison.Ordinal), $"Meldung ohne „{textInMeldung}“: {e.Meldung}");
    }

    static Betrieb Werkstatt(Simulation sim, Konto konto) => sim.Wirtschaft!.Betriebe.First(b => b.Konto == konto && b.IstWerkstatt && b.Zutaten.Count > 0);
    static Familie Familie(Simulation sim, Konto konto) => sim.Wirtschaft!.Familien.Von(konto)!;
    static string Stand(Simulation sim, Konto? spieler = null) => Speicherung.Speichern(sim, spieler, Zeitpunkt);

    static void GleicherStand(string a, string b)
    {
        if (a == b) return;
        var za = a.Split('\n');
        var zb = b.Split('\n');
        var erste = Enumerable.Range(0, Math.Min(za.Length, zb.Length)).FirstOrDefault(i => za[i] != zb[i]);
        throw new Exception($"Spielstände weichen ab, erste Abweichung in Zeile {erste + 1}:\n  {za[erste].Trim()}\n  {zb[erste].Trim()}");
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Entwickler: Gesperrt lehnt der Kern jeden Entwicklerbefehl ab und ändert nichts – auch Vorspulen nicht", () =>
        {
            var (sim, spieler) = NeuePartie(erlaubt: false);
            var vorher = Stand(sim);
            Abgelehnt(sim, spieler, new EntwicklerGulden(1000), "gesperrt");
            Abgelehnt(sim, spieler, new EntwicklerTod(Familie(sim, spieler).Oberhaupt.Nummer), "gesperrt");
            Pruefe.Wahr(!sim.Vorspulen(10).Erfolg, "Vorspulen hätte gesperrt sein sollen.");
            Pruefe.Gleich(false, sim.MitEntwicklerhilfe);
            GleicherStand(vorher, Stand(sim));
            // direkt an der Wirtschaft vorbei am Boten genauso
            Pruefe.Wahr(!sim.Wirtschaft!.Ausfuehren(spieler, new EntwicklerGulden(1000)).Erfolg, "Ausfuehren hätte gesperrt sein sollen.");
            // ohne Wirtschaft gibt es keine Erlaubnis
            var leer = new Simulation { EntwicklerErlaubt = true };
            Pruefe.Gleich(false, leer.EntwicklerErlaubt);
        }),

        ("Entwickler: Erlaubt wirkt ein Befehl, kostet keine Aktionspunkte und setzt das Kennzeichen", () =>
        {
            var (sim, spieler) = NeuePartie();
            Pruefe.Gleich(false, sim.MitEntwicklerhilfe);
            var ap = spieler.Aktionspunkte.Vorrat;
            Abgelehnt(sim, spieler, new EntwicklerGulden(0), "ungleich 0"); // abgelehnt: kein Kennzeichen
            Pruefe.Gleich(false, sim.MitEntwicklerhilfe);
            Ok(sim, spieler, new EntwicklerGulden(10));
            Pruefe.Gleich(true, sim.MitEntwicklerhilfe);
            Pruefe.Gleich(ap, spieler.Aktionspunkte.Vorrat);
            sim.EntwicklerErlaubt = false; // abschalten nimmt das Kennzeichen nicht zurück
            Pruefe.Gleich(true, sim.MitEntwicklerhilfe);
        }),

        ("Entwickler: Gulden geben und nehmen – ohne Buchung in der Jahresrechnung, auch für ein anderes Haus", () =>
        {
            var (sim, spieler) = NeuePartie();
            var gewinn = spieler.Laufend.Gewinn;
            var vorher = spieler.Vermoegen;
            Ok(sim, spieler, new EntwicklerGulden(5000));
            Ok(sim, spieler, new EntwicklerGulden(-1250.5));
            Pruefe.Ungefaehr(vorher + 3749.5, spieler.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(gewinn, spieler.Laufend.Gewinn, 1e-9);
            var rivale = sim.Wirtschaft!.Konten.First(k => k != spieler);
            var r = rivale.Vermoegen;
            Ok(sim, spieler, new EntwicklerGulden(777, rivale.Inhaber));
            Pruefe.Ungefaehr(r + 777, rivale.Vermoegen, 1e-9);
            Abgelehnt(sim, spieler, new EntwicklerGulden(1, "Haus Niemand"), "gibt es nicht");
        }),

        ("Entwickler: Aktionspunkte auffüllen (Höchstwert) und auf eine Zahl setzen", () =>
        {
            var (sim, spieler) = NeuePartie();
            Ok(sim, spieler, new EntwicklerAktionspunkte(0));
            Pruefe.Gleich(0, spieler.Aktionspunkte.Vorrat);
            Ok(sim, spieler, new EntwicklerAktionspunkte());
            Pruefe.Gleich(Daten.Value.Aktionspunkte!.Hoechstens, spieler.Aktionspunkte.Vorrat);
            Ok(sim, spieler, new EntwicklerAktionspunkte(30));
            Pruefe.Gleich(30, spieler.Aktionspunkte.Vorrat);
            Abgelehnt(sim, spieler, new EntwicklerAktionspunkte(-1), "negativ");
        }),

        ("Entwickler: Stand setzen – jede Stufe, auch abwärts; ungültige abgelehnt", () =>
        {
            var (sim, spieler) = NeuePartie();
            var hoechster = Daten.Value.Staende.Count - 1;
            Ok(sim, spieler, new EntwicklerStand(hoechster));
            Pruefe.Gleich(hoechster, spieler.Stand);
            Ok(sim, spieler, new EntwicklerStand(0));
            Pruefe.Gleich(0, spieler.Stand);
            Abgelehnt(sim, spieler, new EntwicklerStand(hoechster + 1), "gibt es nicht");
        }),

        ("Entwickler: Ansehen setzen – gilt sofort und übersteht Speichern und Laden", () =>
        {
            var (sim, spieler) = NeuePartie();
            var w = sim.Wirtschaft!;
            Ok(sim, spieler, new EntwicklerAnsehen(42.5));
            Pruefe.Ungefaehr(42.5, w.Rat.Ansehen(spieler), 1e-9);
            Ok(sim, spieler, new EntwicklerAnsehen(-7));
            Pruefe.Ungefaehr(-7, w.Rat.Ansehen(spieler), 1e-9);
            var (geladen, gs) = Speicherung.Laden(Stand(sim, spieler));
            Pruefe.Ungefaehr(-7, geladen.Wirtschaft!.Rat.Ansehen(gs!), 1e-9);
            Pruefe.Ungefaehr(spieler.EntwicklerAnsehen, gs!.EntwicklerAnsehen, 0);
        }),

        ("Entwickler: Meinung eines Ratsherrn und einer Person über das Haus setzen", () =>
        {
            var (sim, spieler) = NeuePartie();
            var w = sim.Wirtschaft!;
            var ratsherr = w.Rat.Ratsherren[0];
            Ok(sim, spieler, new EntwicklerMeinung(ratsherr.Kennung, 37));
            Pruefe.Ungefaehr(37, w.Meinungen.Meinung(ratsherr, spieler), 1e-9);
            Ok(sim, spieler, new EntwicklerMeinung(ratsherr.Kennung, -100));
            Pruefe.Ungefaehr(-100, w.Meinungen.Meinung(ratsherr, spieler), 1e-9);
            Pruefe.Wahr(w.Meinungen.Gruende(ratsherr, Familie(sim, spieler).Oberhaupt).Any(g => g.Text == "Entwicklerhilfe"), "Der Grund fehlt im Tooltip.");
            var rivale = w.Konten.First(k => k != spieler && w.Familien.Von(k) is not null);
            var andere = Familie(sim, rivale).Oberhaupt;
            Ok(sim, spieler, new EntwicklerMeinung(andere.Kennung, 55));
            Pruefe.Ungefaehr(55, w.Meinungen.Meinung(andere, spieler), 1e-9);
            Abgelehnt(sim, spieler, new EntwicklerMeinung("P99999", 10), "gibt es nicht");
            Abgelehnt(sim, spieler, new EntwicklerMeinung(Familie(sim, spieler).Oberhaupt.Kennung, 10), "selbst");
            // im Spielstand steht der Grund unter den Taten
            var (geladen, gs) = Speicherung.Laden(Stand(sim, spieler));
            Pruefe.Ungefaehr(-100, geladen.Wirtschaft!.Meinungen.Meinung(geladen.Wirtschaft.Rat.Ratsherren[0], gs!), 1e-9);
        }),

        ("Entwickler: Heirat sofort und kostenlos, danach Geburt eines Kindes beider Eheleute", () =>
        {
            var (sim, spieler) = NeuePartie();
            var f = Familie(sim, spieler);
            Pruefe.Wahr(f.Ehepartner is null, "Das Oberhaupt sollte ledig beginnen.");
            var vermoegen = spieler.Vermoegen;
            Ok(sim, spieler, new EntwicklerHeirat());
            Pruefe.Wahr(f.Ehepartner is not null, "Keine Hochzeit.");
            Pruefe.Ungefaehr(vermoegen, spieler.Vermoegen, 1e-9);
            Abgelehnt(sim, spieler, new EntwicklerHeirat(), "verheiratet");
            var vorher = f.Mitglieder.Count;
            Ok(sim, spieler, new EntwicklerGeburt(Geschlecht.Mann));
            Pruefe.Gleich(vorher + 1, f.Mitglieder.Count);
            var kind = f.Mitglieder[^1];
            Pruefe.Gleich((Geschlecht.Mann, 0), (kind.Geschlecht, kind.Alter(sim.Wirtschaft!.Familien.Jahr)));
            var eltern = new[] { f.Oberhaupt.Nummer, f.Ehepartner!.Nummer };
            Pruefe.Wahr(kind.Vater is int v && eltern.Contains(v) && kind.Mutter is int m && eltern.Contains(m), "Das Kind hat nicht beide Eheleute als Eltern.");
        }),

        ("Entwickler: Geburt ohne Ehepartner – ein Kind des Oberhaupts", () =>
        {
            var (sim, spieler) = NeuePartie();
            var f = Familie(sim, spieler);
            Ok(sim, spieler, new EntwicklerGeburt(Geschlecht.Frau));
            var kind = f.Mitglieder[^1];
            Pruefe.Gleich(Geschlecht.Frau, kind.Geschlecht);
            Pruefe.Wahr(kind.Vater == f.Oberhaupt.Nummer || kind.Mutter == f.Oberhaupt.Nummer, "Das Kind gehört nicht zum Oberhaupt.");
        }),

        ("Entwickler: Krankheit auslösen, wechseln und heilen", () =>
        {
            var (sim, spieler) = NeuePartie();
            var o = Familie(sim, spieler).Oberhaupt;
            var name = sim.Wirtschaft!.Gesundheit.Daten.Krankheiten.Keys.First();
            Ok(sim, spieler, new EntwicklerKrankheit(o.Nummer, name));
            Pruefe.Gleich(name, o.Krankheit?.Name);
            Ok(sim, spieler, new EntwicklerKrankheit(o.Nummer, "Pest"));
            Pruefe.Gleich("Pest", o.Krankheit?.Name);
            Ok(sim, spieler, new EntwicklerKrankheit(o.Nummer, null));
            Pruefe.Wahr(o.Krankheit is null, "Nicht geheilt.");
            Abgelehnt(sim, spieler, new EntwicklerKrankheit(o.Nummer, null), "gesund");
            Abgelehnt(sim, spieler, new EntwicklerKrankheit(o.Nummer, "Schnupfen der Elben"), "gibt es nicht");
        }),

        ("Entwickler: Tod eines Kindes, dann des Oberhaupts – das volljährige Kind erbt", () =>
        {
            var (sim, spieler) = NeuePartie();
            var w = sim.Wirtschaft!;
            var f = Familie(sim, spieler);
            Ok(sim, spieler, new EntwicklerGeburt(Geschlecht.Mann));
            var saeugling = f.Mitglieder[^1];
            Ok(sim, spieler, new EntwicklerTod(saeugling.Nummer));
            Pruefe.Wahr(!saeugling.Lebt, "Das Kind lebt noch.");
            Abgelehnt(sim, spieler, new EntwicklerTod(saeugling.Nummer), "lebende Person");
            var erbe = w.Familien.KindAufnehmen(spieler, w.Familien.VolkDaten(f.Volk).Volljaehrig + 1, Geschlecht.Frau); // volljährig
            var altes = f.Oberhaupt;
            Ok(sim, spieler, new EntwicklerTod(altes.Nummer));
            Pruefe.Wahr(!altes.Lebt, "Das Oberhaupt lebt noch.");
            Pruefe.Gleich(erbe, f.Oberhaupt);
            Pruefe.Wahr(spieler.IstAktiv, "Das Haus sollte weiterleben.");
        }),

        ("Entwickler: Charakterzug hinzufügen (Gegenteil fällt weg) und entfernen", () =>
        {
            var (sim, spieler) = NeuePartie();
            var o = Familie(sim, spieler).Oberhaupt;
            var (erst, dann) = o.HatZug("Gierig") ? ("Grosszuegig", "Gierig") : ("Gierig", "Grosszuegig"); // erst: hat er nicht
            Ok(sim, spieler, new EntwicklerZug(o.Nummer, erst));
            Pruefe.Wahr(o.HatZug(erst) && !o.HatZug(dann), $"{erst} fehlt oder {dann} ist noch da.");
            Ok(sim, spieler, new EntwicklerZug(o.Nummer, dann));
            Pruefe.Wahr(o.HatZug(dann) && !o.HatZug(erst), "Das Gegenteil ist nicht weggefallen.");
            Abgelehnt(sim, spieler, new EntwicklerZug(o.Nummer, dann), "schon");
            Ok(sim, spieler, new EntwicklerZug(o.Nummer, dann, Hinzufuegen: false));
            Pruefe.Wahr(!o.HatZug(dann), "Nicht entfernt.");
            Abgelehnt(sim, spieler, new EntwicklerZug(o.Nummer, dann, Hinzufuegen: false), "nicht");
            Abgelehnt(sim, spieler, new EntwicklerZug(o.Nummer, "Fliegend"), "gibt es nicht");
        }),

        ("Entwickler: Ware ins Lager legen und herausnehmen, Zutaten auffüllen", () =>
        {
            var (sim, spieler) = NeuePartie();
            var b = Werkstatt(sim, spieler);
            var ware = b.Zutaten.Keys.First();
            var vorher = b.ImLager(ware);
            Ok(sim, spieler, new EntwicklerWare(b.Name, ware, 7));
            Pruefe.Gleich(vorher + 7, b.ImLager(ware));
            Ok(sim, spieler, new EntwicklerWare(b.Name, ware, -3));
            Pruefe.Gleich(vorher + 4, b.ImLager(ware));
            Abgelehnt(sim, spieler, new EntwicklerWare(b.Name, "Einhornhorn", 1), "gibt es nicht");
            Abgelehnt(sim, spieler, new EntwicklerWare("Werkstatt Nirgendwo", ware, 1), "gibt es nicht");
            Ok(sim, spieler, new EntwicklerZutaten(b.Name));
            foreach (var z in b.Zutaten.Keys)
                Pruefe.Wahr(b.ImLager(z) >= b.LagerKapazitaetJeWare, $"{z} nicht aufgefüllt: {b.ImLager(z)}.");
        }),

        ("Entwickler: Diebstahl leert das Lager, Feuer bringt Brandschaden und Stillstand, Hausbrand beschädigt das Wohnhaus", () =>
        {
            var (sim, spieler) = NeuePartie();
            var b = Werkstatt(sim, spieler);
            Ok(sim, spieler, new EntwicklerZutaten(b.Name));
            var lager = b.Lager.Values.Sum();
            Ok(sim, spieler, new EntwicklerDiebstahl(b.Name));
            Pruefe.Wahr(b.Lager.Values.Sum() < lager, "Nichts gestohlen.");
            Pruefe.Wahr(b.Laufend.Raub > 0, "Kein Raub gebucht.");
            var schaden = b.Laufend.Schaden;
            Ok(sim, spieler, new EntwicklerFeuer(b.Name));
            Pruefe.Wahr(b.Laufend.Brandschaden > 0 && b.Laufend.Schaden > schaden, "Kein Brandschaden.");
            Pruefe.Wahr(spieler.WohnhausInstandBis is null, "Das Wohnhaus war schon beschädigt.");
            Ok(sim, spieler, new EntwicklerHausbrand());
            Pruefe.Wahr(sim.Wirtschaft!.Wohnen.IstBeschaedigt(spieler), "Das Wohnhaus brennt nicht.");
            Abgelehnt(sim, spieler, new EntwicklerHausbrand(), "instand");
        }),

        ("Entwickler: Kennzeichen beim Speichern und Laden – auch im Kopf; alte und nie geholfene Spielstände sagen nein", () =>
        {
            var (sim, spieler) = NeuePartie();
            var ohne = Stand(sim, spieler);
            Pruefe.Wahr(!ohne.Contains("MitEntwicklerhilfe", StringComparison.Ordinal), "Ohne Hilfe soll das Feld fehlen (Spielstand wie bisher).");
            Pruefe.Gleich(false, Speicherung.Kopf(ohne).MitEntwicklerhilfe);
            Pruefe.Gleich(true, Speicherung.Kopf(ohne).EntwicklerErlaubt);
            Ok(sim, spieler, new EntwicklerAktionspunkte());
            var mit = Stand(sim, spieler);
            Pruefe.Gleich(true, Speicherung.Kopf(mit).MitEntwicklerhilfe);
            var (geladen, gs) = Speicherung.Laden(mit);
            Pruefe.Gleich((true, true), (geladen.MitEntwicklerhilfe, geladen.EntwicklerErlaubt));
            Pruefe.Gleich(mit, Stand(geladen, gs));
            // Die Oberfläche schaltet die Sperre nach dem Laden ab – das Kennzeichen bleibt
            geladen.EntwicklerErlaubt = false;
            Pruefe.Gleich(true, Speicherung.Kopf(Stand(geladen, gs)).MitEntwicklerhilfe);
            // Ein alter Spielstand (Format 2) kennt beides nicht
            var (alt, _) = Speicherung.Laden(AlterStand.Value);
            Pruefe.Gleich((false, false), (alt.MitEntwicklerhilfe, alt.EntwicklerErlaubt));
            Pruefe.Gleich(false, Speicherung.Kopf(AlterStand.Value).MitEntwicklerhilfe);
        }),

        ("Entwickler: Kein Entwicklerbefehl während der Ratssitzung", () =>
        {
            var (sim, spieler) = NeuePartie();
            sim.Wirtschaft!.RatssitzungAnhalten = _ => true;
            Ok(sim, spieler, new EntwicklerGulden(1)); // vor der Sitzung geht es
            var e = sim.VorspulenBisRatssitzung();
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            if (sim.Ratssitzung is null) return; // ohne Tagesordnung hält keine Sitzung an
            Abgelehnt(sim, spieler, new EntwicklerGulden(1), "Ratssitzung");
            Pruefe.Wahr(!sim.Vorspulen(5).Erfolg, "Vorspulen in der Sitzung hätte abgelehnt werden sollen.");
            sim.RatssitzungAbschliessen();
            Ok(sim, spieler, new EntwicklerGulden(1));
        }),

        ("Entwickler: Vorspulen – Minuten, bis zu einer Uhrzeit, bis zum Jahresende und über den Jahreswechsel; kein Kennzeichen", () =>
        {
            var (sim, spieler) = NeuePartie();
            var start = sim.Zeit.VergangeneMinuten;
            Pruefe.Wahr(sim.Vorspulen(60).Erfolg, "Vorspulen um 60 abgelehnt.");
            Pruefe.Gleich(start + 60, sim.Zeit.VergangeneMinuten);
            Pruefe.Wahr(sim.VorspulenBisUhrzeit(20, 30).Erfolg, "Bis 20:30 abgelehnt.");
            Pruefe.Gleich("20:30", sim.Zeit.UhrzeitText);
            Pruefe.Wahr(!sim.VorspulenBisUhrzeit(20, 30).Erfolg, "Es ist schon 20:30 – abgelehnt erwartet.");
            Pruefe.Wahr(!sim.VorspulenBisUhrzeit(3, 0).Erfolg, "03:00 liegt vor dem Spieltag.");
            Pruefe.Wahr(!sim.VorspulenBisUhrzeit(23, 0).Erfolg, "23:00 liegt nach dem Spieltag.");
            var jahr = sim.Zeit.Jahr;
            Pruefe.Wahr(sim.VorspulenBisJahresende().Erfolg, "Bis Jahresende abgelehnt.");
            Pruefe.Gleich(("22:59", jahr), (sim.Zeit.UhrzeitText, sim.Zeit.Jahr));
            var berichte = 0;
            sim.JahrBeendet += _ => berichte++;
            Pruefe.Wahr(sim.VorspulenBisRatssitzung().Erfolg, "Über den Jahreswechsel abgelehnt.");
            Pruefe.Gleich(("04:00", jahr + 1, 1), (sim.Zeit.UhrzeitText, sim.Zeit.Jahr, berichte));
            Pruefe.Wahr(sim.VorspulenBisUhrzeit(9, 0).Erfolg, "Bis 09:00 abgelehnt.");
            Pruefe.Wahr(sim.VorspulenBisUhrzeit(8, 0).Erfolg, "Bis 08:00 (nächstes Jahr) abgelehnt.");
            Pruefe.Gleich(("08:00", jahr + 2), (sim.Zeit.UhrzeitText, sim.Zeit.Jahr));
            Pruefe.Wahr(!sim.Vorspulen(0).Erfolg && !sim.Vorspulen(Simulation.VorspulenHoechstens + 1).Erfolg, "Grenzen nicht geprüft.");
            Pruefe.Gleich(false, sim.MitEntwicklerhilfe);
            // Vorspulen rechnet genau wie Ticks
            var (b, _) = NeuePartie();
            while (b.Zeit.VergangeneMinuten < sim.Zeit.VergangeneMinuten) b.Tick();
            GleicherStand(Stand(b), Stand(sim));
        }),

        ("Prüfsumme: gleich bei gleicher Partie, ohne Speicherzeit, Titel, Tempo, Spielerkonto und Warteschlange; anders nach einer Änderung", () =>
        {
            var (a, spielerA) = NeuePartie();
            var (b, _) = NeuePartie();
            var summe = Zustandspruefsumme.Berechnen(a)!;
            Pruefe.Gleich(64, summe.Length);
            Pruefe.Wahr(summe.All(c => char.IsAsciiHexDigitLower(c) || char.IsAsciiDigit(c)), "Keine kleine Hexadezimalzahl: " + summe);
            Pruefe.Gleich(summe, Zustandspruefsumme.Berechnen(b));
            Pruefe.Gleich(summe[..8], Zustandspruefsumme.Kurz(a));
            b.Bote.Verzoegerung = 50;
            b.Bote.Einreihen(b.Wirtschaft!.Konten[0], new EntwicklerGulden(1)); // wartet noch
            b.Takt.Tempo = 4;
            b.Takt.IstPausiert = true;
            Pruefe.Gleich(summe, Zustandspruefsumme.Berechnen(b));
            Pruefe.Gleich(summe, Zustandspruefsumme.Berechnen(Speicherung.Laden(Speicherung.Speichern(a, spielerA)).Simulation));
            a.Tick();
            Pruefe.Wahr(summe != Zustandspruefsumme.Berechnen(a), "Nach einem Tick sollte sich die Prüfsumme ändern.");
            var nachTick = Zustandspruefsumme.Berechnen(a);
            Ok(a, spielerA, new EntwicklerGulden(1));
            Pruefe.Wahr(nachTick != Zustandspruefsumme.Berechnen(a), "Nach einem Befehl sollte sich die Prüfsumme ändern.");
        }),

        ("KI-Einblick: alle KI-Häuser mit Profil, Betrieben und Entscheidungen – nur lesen", () =>
        {
            var (sim, _) = NeuePartie();
            for (var i = 0; i < 2000; i++) sim.Tick();
            var w = sim.Wirtschaft!;
            var vorher = Stand(sim);
            var einblick = KiEinblick.Alle(w);
            Pruefe.Gleich(w.Rivalen.Count, einblick.Count);
            Pruefe.Wahr(einblick.Count > 0, "Keine KI-Häuser.");
            Pruefe.Wahr(einblick.Any(e => e.Betriebe.Count > 0 && e.Entscheidungen.Count > 0), "Keine KI mit Betrieb und Entscheidung.");
            foreach (var e in einblick)
            {
                Pruefe.Wahr(e.Zeilen.Count >= 2 + e.Betriebe.Count + e.Entscheidungen.Count, $"Zeilen fehlen bei {e.Haus}.");
                Pruefe.Wahr(e.Zeilen[0].StartsWith(e.Haus, StringComparison.Ordinal), "Kopfzeile ohne Haus: " + e.Zeilen[0]);
            }
            GleicherStand(vorher, Stand(sim));
        }),

        ("Befehlsprotokoll: Aufzeichnen und Abspielen mit Entwicklerbefehlen und Vorspulen ergibt denselben Spielstand", () =>
        {
            var (sim, spieler) = NeuePartie(startwert: 4711);
            var rivale = sim.Wirtschaft!.Konten.First(k => k != spieler && k.IstAktiv);
            sim.Bote.AufzeichnungBeginnen();
            var b = Werkstatt(sim, spieler);
            var o = Familie(sim, spieler).Oberhaupt;
            sim.Bote.Einreihen(spieler, new EntwicklerGulden(2500));
            sim.Bote.Einreihen(spieler, new EntwicklerHeirat());
            sim.Bote.Einreihen(spieler, new EntwicklerZutaten(b.Name));
            Pruefe.Wahr(sim.Vorspulen(120).Erfolg, "Vorspulen abgelehnt.");
            sim.Bote.Verzoegerung = 2;
            sim.Bote.Einreihen(spieler, new EntwicklerGeburt(Geschlecht.Frau));
            sim.Bote.Einreihen(spieler, new EntwicklerKrankheit(o.Nummer, "Pest"));
            sim.Bote.Einreihen(spieler, new EntwicklerMeinung(sim.Wirtschaft.Rat.Ratsherren[0].Kennung, 80));
            sim.Bote.Einreihen(spieler, new EntwicklerFeuer(Werkstatt(sim, rivale).Name));
            sim.Bote.Einreihen(spieler, new EntwicklerAnsehen(25, rivale.Inhaber));
            Pruefe.Wahr(sim.VorspulenBisRatssitzung().Erfolg, "Über den Jahreswechsel abgelehnt.");
            sim.Bote.Einreihen(spieler, new EntwicklerDiebstahl(b.Name));
            sim.Bote.Einreihen(spieler, new EntwicklerStand(2)); // wartet noch am Ende
            var protokoll = sim.Bote.AufzeichnungBeenden();
            Pruefe.Wahr(protokoll.IstVollstaendig, "Protokoll unvollständig: " + protokoll.Luecke);
            Pruefe.Gleich(10, protokoll.Befehle.Count);
            Pruefe.Wahr(sim.MitEntwicklerhilfe, "Kennzeichen fehlt.");
            var nachgerechnet = Befehlsprotokoll.AusJson(protokoll.AlsJson()).Abspielen();
            GleicherStand(Stand(sim), Stand(nachgerechnet));
            Pruefe.Gleich(Zustandspruefsumme.Berechnen(sim), Zustandspruefsumme.Berechnen(nachgerechnet));
            // Gegenprobe: gesperrt abgespielt liefe es anders – die Sperre gehört darum in den Anfangszustand
            Pruefe.Wahr(protokoll.Anfangszustand.Contains("\"EntwicklerErlaubt\": true", StringComparison.Ordinal), "Die Sperre fehlt im Anfangszustand.");
        }),
    };
}
