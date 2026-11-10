using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Hofstellen am Start (Nutzerentscheidung 09.11.2026): Der Spieler wählt eine Start-Hofstelle oder lost, die KI-Häuser bekommen geloste – aus
/// einer eigenen Zufallsquelle, deterministisch. Ohne Los (<see cref="Startplatzvergabe.Vorgabe"/>) ist der Start bitgleich mit dem alten auf
/// einer Karte ohne Hofstellen. Freie Hofstellen werden Bauplätze; Speichern und Laden behalten alles.
/// </summary>
static class HofstellenStartTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));
    static readonly Lazy<string> GreyboxText = new(() => File.ReadAllText(Datenpfad.Karte("greybox")));

    static Stadtkarte Greybox() => Stadtkarte.AusJson(GreyboxText.Value, HofstellenTests.Katalog.Value);

    static (Simulation Sim, Konto Spieler) Partie(NeuePartie neu, int startwert = 1400, Stadtkarte? karte = null) =>
        Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos },
            Daten.Value, neu, karte: karte ?? Greybox());

    /// <summary>Wer auf welcher Hofstelle sitzt (je Konto, Kennung über das Wohnhaus) – und ob die Werkstatt auf derselben steht.</summary>
    static List<string> Belegung(Stadtwirtschaft w)
    {
        var karte = w.Karte!;
        return w.Konten.Select(k =>
        {
            var h = karte.HofstelleVon(w.Bauen.Wohnhaus(k)?.Kennung);
            var werkstatt = w.Betriebe.FirstOrDefault(b => b.Konto == k && b.InDerStadt);
            var platz = werkstatt is null ? null : w.Bauen.PlatzVon(werkstatt.Name)?.Gebaeude?.Kennung;
            return $"{k.Inhaber}: {h?.Kennung ?? "-"}{(h is not null && platz == h.Werkstatt ? "" : $" (Werkstatt {platz ?? "-"})")}";
        }).ToList();
    }

    /// <summary>SHA-256 des Spielstands ohne Karte (die Partiekarte ist eine andere Datei als die alte Karte, rechnet aber gleich).</summary>
    static string Pruefsumme(Simulation sim, Konto spieler)
    {
        var n = JsonNode.Parse(Speicherung.Speichern(sim, spieler, new DateTime(2026, 1, 1)))!.AsObject();
        foreach (var k in n.Select(p => p.Key).Where(k => k.Equals("karte", StringComparison.OrdinalIgnoreCase)).ToList()) n.Remove(k);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(n.ToJsonString())));
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Hofstellen am Start: Jedes Haus bekommt eine eigene Start-Hofstelle mit Wohnhaus und Werkstatt; gleicher Startwert = gleiche Plätze, andere Startwerte losen anders", () =>
        {
            var gesehen = new HashSet<string>();
            var spielerPlaetze = new HashSet<string>();
            foreach (var startwert in Enumerable.Range(1, 12))
            {
                var (sim, _) = Partie(new NeuePartie(), startwert);
                var b = Belegung(sim.Wirtschaft!);
                Pruefe.Wahr(b.All(x => !x.Contains("Werkstatt") && !x.EndsWith(": -")), $"Startwert {startwert}: {string.Join("; ", b)}");
                var hofstellen = b.Select(x => x[(x.IndexOf(": ", StringComparison.Ordinal) + 2)..]).ToList();
                Pruefe.Gleich(hofstellen.Count, hofstellen.Distinct().Count(), "jede Hofstelle einmal");
                var (wieder, _) = Partie(new NeuePartie(), startwert);
                Pruefe.Gleich(string.Join("; ", b), string.Join("; ", Belegung(wieder.Wirtschaft!)), $"Startwert {startwert} zweimal");
                gesehen.Add(string.Join("; ", b));
                spielerPlaetze.Add(hofstellen[0]);
            }
            Pruefe.Wahr(gesehen.Count >= 4, $"12 Startwerte, {gesehen.Count} verschiedene Belegungen");
            Pruefe.Wahr(spielerPlaetze.Count >= 3, $"der Spieler lost auf {spielerPlaetze.Count} verschiedene Hofstellen");
            // Zuteilen ist dieselbe Rechnung, ohne Partie
            var karte = Greybox();
            var (s, h) = Hofstellenvergabe.Zuteilen(karte, new NeuePartie(), 5, karte.Startbelegung.Haeuser);
            var (sim5, _) = Partie(new NeuePartie(), 5);
            Pruefe.Gleich(string.Join(", ", new[] { s }.Concat(h).Select(x => x!.Kennung)),
                string.Join(", ", Belegung(sim5.Wirtschaft!).Select(x => x[(x.IndexOf(": ", StringComparison.Ordinal) + 2)..])));
        }),

        ("Hofstellen am Start: Der Spieler wählt seine Hofstelle – Wohnhaus, Werkstatt und Name dort; die KI-Häuser losen unter den übrigen; eine unbekannte oder freie Hofstelle wird abgewiesen", () =>
        {
            var karte = Greybox();
            foreach (var h in karte.Hofstellen)
            {
                var (sim, spieler) = Partie(new NeuePartie(Hofstelle: h.Kennung), 77);
                var w = sim.Wirtschaft!;
                Pruefe.Gleich(h.Wohnhaus, w.Bauen.Wohnhaus(spieler)!.Kennung);
                var werkstatt = w.Betriebe.Single(b => b.Konto == spieler);
                Pruefe.Gleich(h.Werkstatt, w.Bauen.PlatzVon(werkstatt.Name)!.Gebaeude!.Kennung);
                Pruefe.Gleich(Hofstellenvergabe.WerkstattnameSpieler(karte, h).Replace("{art}", "Gießerei"), werkstatt.Name);
                Pruefe.Wahr(w.Konten.Skip(1).All(k => w.Bauen.Wohnhaus(k)!.Kennung != h.Wohnhaus), "kein KI-Haus auf der Hofstelle des Spielers");
            }
            Pruefe.WirftMitMeldung(() => Partie(new NeuePartie(Hofstelle: "gibtsnicht")), "gibt es auf dieser Karte nicht");
            var frei = JsonNode.Parse(GreyboxText.Value)!.AsObject();
            foreach (var h in frei["start"]!["hofstellen"]!.AsArray()) if ((string)h!["kennung"]! == "H4") h["start"] = false;
            var karteFrei = Stadtkarte.AusJson(frei.ToJsonString(), HofstellenTests.Katalog.Value);
            Pruefe.WirftMitMeldung(() => Partie(new NeuePartie(Hofstelle: "H4", Rivalen: 2), karte: karteFrei), "kein Startplatz");
            // Gegenprobe: mit zwei KI-Häusern reichen drei Start-Hofstellen, mit dreien nicht
            Pruefe.Gleich(3, Partie(new NeuePartie(Rivalen: 2), karte: karteFrei).Sim.Wirtschaft!.Konten.Count);
            Pruefe.WirftMitMeldung(() => Partie(new NeuePartie(Rivalen: 3), karte: karteFrei), "zu wenig");
        }),

        ("Hofstellen am Start: Ohne Los (Vorgabe) ist die Partie bitgleich mit dem alten Start auf derselben Karte ohne Hofstellen – zwei Spieltage; gelost rechnet sie anders", () =>
        {
            var (alt, altSpieler) = Partie(new NeuePartie(), 1400, HofstellenTests.AlteKarte("greybox"));
            var (neu, neuSpieler) = Partie(new NeuePartie(Startplaetze: Startplatzvergabe.Vorgabe), 1400);
            Pruefe.Gleich(string.Join("; ", Belegung(alt.Wirtschaft!).Select(x => x.Split(':')[0])), string.Join("; ", Belegung(neu.Wirtschaft!).Select(x => x.Split(':')[0])));
            Pruefe.Gleich(Pruefsumme(alt, altSpieler), Pruefsumme(neu, neuSpieler), "am Start");
            for (var i = 0; i < Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag; i++) { alt.Tick(); neu.Tick(); }
            Pruefe.Gleich(Pruefsumme(alt, altSpieler), Pruefsumme(neu, neuSpieler), "nach einem Spieljahr");
            // Gegenprobe: ein Startwert, der anders lost als die Vorgabe, rechnet anders
            var startwert = Enumerable.Range(1, 50).First(s => Belegung(Partie(new NeuePartie(), s).Sim.Wirtschaft!)[1] != Belegung(neu.Wirtschaft!)[1]);
            var (gelost, gelostSpieler) = Partie(new NeuePartie(), startwert);
            var (vorgabe, vorgabeSpieler) = Partie(new NeuePartie(Startplaetze: Startplatzvergabe.Vorgabe), startwert);
            Pruefe.Wahr(Pruefsumme(gelost, gelostSpieler) != Pruefsumme(vorgabe, vorgabeSpieler), "gelost ≠ Vorgabe");
        }),

        ("Hofstellen am Start: Das Los hat eine eigene Zufallsquelle – Familien, Namen und Talente sind dieselben wie ohne Los", () =>
        {
            foreach (var startwert in new[] { 3, 1400 })
            {
                var (gelost, _) = Partie(new NeuePartie(), startwert);
                var (vorgabe, _) = Partie(new NeuePartie(Startplaetze: Startplatzvergabe.Vorgabe), startwert);
                string Familien(Simulation s) => string.Join("; ", s.Wirtschaft!.Konten.Select(k => s.Wirtschaft.Familien.Von(k) is { } f
                    ? $"{k.Inhaber} {f.Oberhaupt.Vorname} {f.Oberhaupt.Geburtsjahr} {f.Oberhaupt.Aussehen} {string.Join(",", f.Oberhaupt.Zuege)}" : k.Inhaber));
                Pruefe.Gleich(Familien(vorgabe), Familien(gelost), $"Startwert {startwert}");
            }
        }),

        ("Hofstellen am Start: Freie Hofstellen werden Bauplätze – Wohnhaus- und Werkstattteil; mit allen Häusern keine; ohne Hofstellen wie bisher keine", () =>
        {
            string Bauplaetze(Stadtwirtschaft w) => string.Join(", ", w.Bauen.Grundstuecke.Where(g => g.Art == Grundstuecksart.Bauplatz)
                .Select(g => $"{g.Kennung}{(g.AufWerkstattplatz ? " (Werkstatt)" : "")}"));
            var (eins, _) = Partie(new NeuePartie(Rivalen: 1, Startplaetze: Startplatzvergabe.Vorgabe));
            Pruefe.Gleich("Wohnhaus3, Tischlerei_Eichwald (Werkstatt), Wohnhaus4, Maurerei_Kieselbach (Werkstatt)", Bauplaetze(eins.Wirtschaft!));
            Pruefe.Wahr(eins.Wirtschaft!.Bauen.Grundstuecke.Where(g => g.Art == Grundstuecksart.Bauplatz).All(g => Bauwesen.IstWohnhausplatz(g) != Bauwesen.IstWerkstattplatz(g)),
                "je eins von beiden");
            Pruefe.Gleich(null, eins.Wirtschaft.Karte!.GebaeudeMit("Tischlerei_Eichwald")!.Betrieb); // kein Name ohne Haus
            var (alle, _) = Partie(new NeuePartie(Rivalen: 3));
            Pruefe.Gleich("", Bauplaetze(alle.Wirtschaft!));
            var (alt, _) = Partie(new NeuePartie(Rivalen: 1), karte: HofstellenTests.AlteKarte("greybox"));
            Pruefe.Gleich("", Bauplaetze(alt.Wirtschaft!));
            Pruefe.Gleich("Tischlerei Eichwald", alt.Wirtschaft!.Karte!.GebaeudeMit("Tischlerei_Eichwald")!.Betrieb); // Gegenprobe: alte Karte unverändert
        }),

        ("Hofstellen am Start: Die Werkstatt eines KI-Hauses nimmt ihren Namen mit an die geloste Hofstelle; die Partiekarte steht im Spielstand, die Kartendatei bleibt", () =>
        {
            var karte = Greybox();
            var startwert = Enumerable.Range(1, 50).First(s => Hofstellenvergabe.Zuteilen(karte, new NeuePartie(), s, karte.Startbelegung.Haeuser).Haeuser[0]!.Kennung != "H2");
            var (sim, spieler) = Partie(new NeuePartie(), startwert, karte);
            var w = sim.Wirtschaft!;
            var eisenbart = w.Konten.Single(k => k.Inhaber == "Haus Eisenbart");
            var ziel = w.Karte!.HofstelleVon(w.Bauen.Wohnhaus(eisenbart)!.Kennung)!;
            Pruefe.Wahr(ziel.Kennung != "H2", "anderswo gelost");
            Pruefe.Gleich("Gießerei Eisenbart", w.Karte.GebaeudeMit(ziel.Werkstatt)!.Betrieb);
            Pruefe.Gleich(ziel.Werkstatt, w.Bauen.PlatzVon("Gießerei Eisenbart")!.Gebaeude!.Kennung);
            Pruefe.Wahr(w.Karte.Gebaeude.Count(g => g.Betrieb == "Gießerei Eisenbart") == 1, "der Name steht an genau einem Platz");
            Pruefe.Gleich("Gießerei Eisenbart", karte.GebaeudeMit("Giesserei_Eisenbart")!.Betrieb); // die gelesene Karte ist unverändert
            Pruefe.Wahr(!ReferenceEquals(karte, w.Karte), "eigene Partiekarte");
            // Speichern und Laden: dieselben Plätze, und es rechnet weiter wie das Original
            var (geladen, geladenSpieler) = Speicherung.Laden(Speicherung.Speichern(sim, spieler, new DateTime(2026, 1, 1)));
            Pruefe.Gleich(string.Join("; ", Belegung(w)), string.Join("; ", Belegung(geladen.Wirtschaft!)));
            for (var i = 0; i < 300; i++) { sim.Tick(); geladen.Tick(); }
            Pruefe.Gleich(Pruefsumme(sim, spieler), Pruefsumme(geladen, geladenSpieler!), "nach 300 Minuten");
        }),

        ("Hofstellen am Start: Ein alter Spielstand (Karte ohne Hofstellen) lädt wie bisher – keine Hofstellen, keine Werkstattplätze als Bauplatz, es läuft weiter", () =>
        {
            var datei = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
                "tests", "Reichsgilde.Core.Tests", "Spielstaende", "format2-greybox-herbst1406.json.gz");
            using var gz = new System.IO.Compression.GZipStream(File.OpenRead(datei), System.IO.Compression.CompressionMode.Decompress);
            var (sim, _) = Speicherung.Laden(new StreamReader(gz).ReadToEnd());
            var w = sim.Wirtschaft!;
            Pruefe.Wahr(!w.Karte!.HatHofstellen, "alte Karte ohne Hofstellen");
            Pruefe.Wahr(w.Bauen.Grundstuecke.All(g => !g.AufWerkstattplatz), "kein Grundstück auf einem Werkstattplatz");
            Pruefe.Wahr(w.Betriebe.Where(b => b.InDerStadt).All(b => w.Bauen.PlatzVon(b.Name) is not null), "jeder Betrieb hat seinen Platz");
            for (var i = 0; i < 200; i++) sim.Tick();
        }),
    };
}
