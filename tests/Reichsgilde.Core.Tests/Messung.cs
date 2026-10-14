using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Balance-Messung (kein Test): spielt viele Partien mit verschiedenen Startwerten durch und zählt,
/// was in den Familien geschieht. Der Spieler bleibt dabei passiv (ledig, nur die Startgießerei).
/// Aufruf:  dotnet run --project tests/Reichsgilde.Core.Tests -- messen 40 20 Zwerg
///          … -- messen 30 20 Mensch gericht   (der Spieler setzt jedes Jahr einen Spitzel auf den Rivalen und klagt, sobald es reicht)
/// </summary>
static class Messung
{
    public static int Dynastie(string[] args)
    {
        var daten = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
        var jahre = args.Length > 0 ? int.Parse(args[0]) : 40;
        var startwerte = args.Length > 1 ? int.Parse(args[1]) : 10;
        var volk = args.Length > 2 ? Enum.Parse<Volk>(args[2]) : Volk.Mensch;
        var gericht = args.Length > 3 && args[3] == "gericht";
        int urteileSchuldig = 0, urteileFrei = 0, amtVerloren = 0;
        double bestechung = 0;

        int verheiratet = 0, erloschen = 0, buergermeister = 0;
        double heiratsjahre = 0, kinder = 0, erbfaelle = 0, bmJahre = 0, kostenSpieler = 0, vermoegen = 0;
        for (var s = 1; s <= startwerte; s++)
        {
            var (sim, spieler) = Partieaufbau.Erstellen(
                Partieeinstellungen.Standard with { Zufallsstartwert = s * 101, Siegbedingung = Siegbedingung.Endlos },
                daten, new NeuePartie(Volk: volk));
            var w = sim.Wirtschaft!;
            var rivale = w.Konten.First(k => k != spieler);
            int? heirat = null, bm = null;
            for (var j = 1; j <= jahre; j++)
            {
                if (gericht && spieler.IstAktiv)
                {
                    if (w.Justiz.Beweiskraft(spieler, rivale) >= w.Justiz.Daten.SichereBeweiskraft)
                        w.Ausfuehren(spieler, new Anklagen(rivale.Inhaber));
                    if (spieler.Vermoegen > 1500)
                        w.Ausfuehren(spieler, new SpitzelAnsetzen(rivale.Inhaber));
                }
                var hatteAmt = w.Rat.AmtVon(rivale) is not null;
                var jahr = sim.Zeit.Jahr;
                while (sim.Zeit.Jahr == jahr) sim.Tick();
                heirat ??= w.Familien.Von(rivale)!.Ehepartner is not null ? j : null;
                bm ??= w.Rat.AmtNamens("Bürgermeister")?.Konto == rivale ? j : null;
                kostenSpieler += spieler.Vorjahr?.Familie ?? 0;
                bestechung += rivale.Vorjahr?.Geschenke ?? 0;
                foreach (var u in w.Justiz.LetzteUrteile.Where(u => u.Beklagter == rivale))
                {
                    if (u.Schuldig) urteileSchuldig++; else urteileFrei++;
                    if (u.Schuldig && hatteAmt && u.Text.Contains("Verlust des Amtes")) amtVerloren++;
                }
            }
            var f = w.Familien.Von(rivale)!;
            var nachkommen = f.Mitglieder.Count(p => p.Vater is not null);
            if (heirat is { } h) { verheiratet++; heiratsjahre += h; }
            if (bm is { } b) { buergermeister++; bmJahre += b; }
            if (rivale.IstErloschen) erloschen++;
            kinder += nachkommen;
            erbfaelle += f.Ahnen.Count;
            vermoegen += rivale.Vermoegen;
            Console.WriteLine($"Startwert {s * 101,5}: {f.Oberhaupt.Name} ({f.Oberhaupt.Alter(w.Familien.Jahr)} J.), " +
                              $"Heirat im Jahr {heirat?.ToString() ?? "–"}{(f.Mitglieder.FirstOrDefault(p => p.Geburtsname != f.Nachname) is { } partner ? $" mit {partner.Volk.Einzahl()}" : "")}, Nachkommen {nachkommen} (lebend {f.Lebende.Count(p => p.Vater is not null)}), " +
                              $"Erbfälle {f.Ahnen.Count}{(rivale.IstErloschen ? ", ERLOSCHEN" : "")}, {w.StandVon(rivale).Name}, " +
                              $"{Zahlenformat.Ganz(rivale.Vermoegen)} Gulden, Bürgermeister ab Jahr {bm?.ToString() ?? "–"}");
        }
        Console.WriteLine();
        Console.WriteLine($"Haus Eisenbart über {jahre} Jahre, {startwerte} Startwerte (Spieler: {volk.Mehrzahl()}, passiv):");
        Console.WriteLine($"  verheiratet {verheiratet}/{startwerte}, im Schnitt im Jahr {(verheiratet > 0 ? heiratsjahre / verheiratet : 0):N1}");
        Console.WriteLine($"  Nachkommen Ø {kinder / startwerte:N1}, Erbfälle Ø {erbfaelle / startwerte:N1}, erloschen {erloschen}/{startwerte}");
        Console.WriteLine($"  Bürgermeister {buergermeister}/{startwerte}, im Schnitt ab Jahr {(buergermeister > 0 ? bmJahre / buergermeister : 0):N1}");
        Console.WriteLine($"  Vermögen Ø {Zahlenformat.Ganz(vermoegen / startwerte)} Gulden; Familienkosten des Spielers Ø {Zahlenformat.Ganz(kostenSpieler / startwerte / jahre)} Gulden im Jahr");
        Console.WriteLine($"  Geschenke an Ratsherren (Bestechung) Ø {Zahlenformat.Ganz(bestechung / startwerte)} Gulden über {jahre} Jahre");
        if (gericht)
            Console.WriteLine($"  vor Gericht: {urteileSchuldig} Schuldsprüche, {urteileFrei} Freisprüche, davon {amtVerloren} mit Amtsverlust (Summe über alle Startwerte)");
        return 0;
    }

    /// <summary>
    /// Spielstand für Klicktests des Gerichtssaals: Der Spieler (Bürger) bespitzelt Haus Eisenbart Jahr für Jahr,
    /// bis er Beweise hat, und erhebt Anklage. Gespeichert wird mitten im Jahr – das Verfahren läuft.
    /// </summary>
    public static int GerichtsSpielstand(string datei)
    {
        var daten = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
        var (sim, spieler) = Partieaufbau.Erstellen(
            Partieeinstellungen.Standard with { Zufallsstartwert = 202, Siegbedingung = Siegbedingung.Endlos },
            daten, new NeuePartie(Startvermoegen: 20_000));
        var w = sim.Wirtschaft!;
        var rivale = w.Konten.First(k => k != spieler);
        w.Ausfuehren(spieler, new StandErwerben());
        for (var jahr = 0; jahr < 25 && w.Justiz.Beweise(spieler, rivale).Count() < 3; jahr++)
        {
            w.Ausfuehren(spieler, new SpitzelAnsetzen(rivale.Inhaber));
            var j = sim.Zeit.Jahr;
            while (sim.Zeit.Jahr == j) sim.Tick();
        }
        for (var i = 0; i < 3 * 60; i++) sim.Tick();
        var e = w.Ausfuehren(spieler, new Anklagen(rivale.Inhaber));
        File.WriteAllText(datei, Speicherung.Speichern(sim, spieler));
        Console.WriteLine($"{sim.Zeit.DatumText}: {w.Justiz.Beweise(spieler, rivale).Count()} Beweise, Anklage: {e.Meldung}");
        Console.WriteLine($"gespeichert: {datei}");
        return 0;
    }
}
