using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Wirtschaft;

// ═════════════════════════════════════════════════════════════════════════════
//  Klappbrückenmessung – was die Klappbrücke über den Rhein die Karren kostet (Messung, kein Test; Datenversion 90, Nutzer 15.11.2026)
//
//  Aufruf:  dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen klappbruecke [Jahre] [Startwerte]
//
//  Je Startwert drei Partien „Schwer“ mit allen KI-Häusern: Köln wie es ist, die Prüfkarte (Köln ohne den Neumarkt in Deutz – die Häuser
//  in Deutz fahren über die Rheinbrücke) mit Klappbrücke und dieselbe ohne (Abschnitt logistik.klappbruecke entfernt). Gezählt: Fahrten zum
//  Markt über die Brücke (hin und zurück je zwei Querungen), davon gewartet, die Wartezeit, und das Vermögen der drei Häuser in Deutz am Ende.
// ═════════════════════════════════════════════════════════════════════════════

static class Klappbrueckenmessung
{
    static readonly string[] Deutz = { "Gießerei Scherfgin", "Schänke Mommersloch", "Tischlerei Kleingedank" };

    public static int Erstellen(string[] args)
    {
        var jahre = args.Length > 0 ? int.Parse(args[0]) : 10;
        var startwerte = args.Length > 1 ? int.Parse(args[1]) : 3;
        var mit = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
        var ohne = KlappbrueckeTests.OhneKlappbruecke();
        Console.WriteLine($"Klappbrücke: {jahre} Jahre, {startwerte} Startwerte, Öffnung {mit.Logistik.Klappbruecke!.OeffnungMinuten} min");
        Console.WriteLine("Lauf                 Querungen  gewartet  Anteil  Wartezeit gesamt  je Wartefall  Fahrten Deutz/Jahr  Reinvermögen Deutz");
        foreach (var (name, karte, daten) in new (string, Func<Stadtkarte>, Wirtschaftsdaten)[]
                 {
                     ("Köln", () => Stadtkarte.AusDatei(Datenpfad.Karte("koeln")), mit),
                     ("Prüfkarte mit", KlappbrueckeTests.Pruefkarte, mit),
                     ("Prüfkarte ohne", KlappbrueckeTests.Pruefkarte, ohne),
                 })
        {
            long querungen = 0, gewartet = 0, minuten = 0, fahrtenDeutz = 0;
            double vermoegen = 0;
            for (var s = 0; s < startwerte; s++)
            {
                var sim = KlappbrueckeTests.Partie(karte(), daten, 1400 + s);
                var w = sim.Wirtschaft!;
                var zustand = new Dictionary<Karren, (KarrenZustand Z, long? Ab)>(ReferenceEqualityComparer.Instance);
                for (var i = 0; i < jahre * Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag; i++)
                {
                    sim.Tick();
                    foreach (var k in w.Betriebe.SelectMany(b => b.Karren))
                    {
                        var (z, ab) = zustand.GetValueOrDefault(k, (KarrenZustand.AmBetrieb, null));
                        zustand[k] = (k.Zustand, k.BrueckeWartenAb);
                        if (k.BrueckeWartenAb is { } neu && neu != ab) { gewartet++; minuten += k.BrueckeWartenBis!.Value - neu; }
                        if (z == KarrenZustand.AmBetrieb && k.Zustand == KarrenZustand.ZumMarkt)
                        {
                            if (w.Fahrten(k) is var (hin, zurueck))
                                querungen += (w.Klappbruecke.FuehrtUeber(hin) ? 1 : 0) + (w.Klappbruecke.FuehrtUeber(zurueck) ? 1 : 0);
                            if (Deutz.Contains(k.Heimat.Name)) fahrtenDeutz++;
                        }
                    }
                }
                vermoegen += w.Betriebe.Where(b => Deutz.Contains(b.Name)).Select(b => b.Konto).Distinct().Sum(k => k.Reinvermoegen);
            }
            Console.WriteLine($"{name,-20} {querungen,9}  {gewartet,8}  {(querungen > 0 ? 100.0 * gewartet / querungen : 0),5:0.0}%  {minuten,16}  " +
                              $"{(gewartet > 0 ? (double)minuten / gewartet : 0),12:0.0}  {(double)fahrtenDeutz / jahre / startwerte,18:0.0}  {vermoegen / startwerte,14:0}");
        }
        return 0;
    }
}
