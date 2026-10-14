using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Balance-Messung (kein Test): spielt viele Partien mit verschiedenen Startwerten durch und zählt,
/// was in den Familien geschieht. Der Spieler bleibt dabei passiv (ledig, nur die Startgießerei).
/// Aufruf:  dotnet run --project tests/Reichsgilde.Core.Tests -- messen 40 20 Zwerg
/// </summary>
static class Messung
{
    public static int Dynastie(string[] args)
    {
        var daten = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
        var jahre = args.Length > 0 ? int.Parse(args[0]) : 40;
        var startwerte = args.Length > 1 ? int.Parse(args[1]) : 10;
        var volk = args.Length > 2 ? Enum.Parse<Volk>(args[2]) : Volk.Mensch;

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
                var jahr = sim.Zeit.Jahr;
                while (sim.Zeit.Jahr == jahr) sim.Tick();
                heirat ??= w.Familien.Von(rivale)!.Ehepartner is not null ? j : null;
                bm ??= w.Rat.AmtNamens("Bürgermeister")?.Konto == rivale ? j : null;
                kostenSpieler += spieler.Vorjahr?.Familie ?? 0;
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
                              $"Heirat im Jahr {heirat?.ToString() ?? "–"}, Nachkommen {nachkommen} (lebend {f.Lebende.Count(p => p.Vater is not null)}), " +
                              $"Erbfälle {f.Ahnen.Count}{(rivale.IstErloschen ? ", ERLOSCHEN" : "")}, {w.StandVon(rivale).Name}, " +
                              $"{Zahlenformat.Ganz(rivale.Vermoegen)} Gulden, Bürgermeister ab Jahr {bm?.ToString() ?? "–"}");
        }
        Console.WriteLine();
        Console.WriteLine($"Haus Eisenbart über {jahre} Jahre, {startwerte} Startwerte (Spieler: {volk.Mehrzahl()}, passiv):");
        Console.WriteLine($"  verheiratet {verheiratet}/{startwerte}, im Schnitt im Jahr {(verheiratet > 0 ? heiratsjahre / verheiratet : 0):N1}");
        Console.WriteLine($"  Nachkommen Ø {kinder / startwerte:N1}, Erbfälle Ø {erbfaelle / startwerte:N1}, erloschen {erloschen}/{startwerte}");
        Console.WriteLine($"  Bürgermeister {buergermeister}/{startwerte}, im Schnitt ab Jahr {(buergermeister > 0 ? bmJahre / buergermeister : 0):N1}");
        Console.WriteLine($"  Vermögen Ø {Zahlenformat.Ganz(vermoegen / startwerte)} Gulden; Familienkosten des Spielers Ø {Zahlenformat.Ganz(kostenSpieler / startwerte / jahre)} Gulden im Jahr");
        return 0;
    }
}
