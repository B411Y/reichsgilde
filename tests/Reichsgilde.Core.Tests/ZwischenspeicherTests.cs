using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Spiel und Tests schneller (14.11.2026): drei Stellen rechnen jetzt mit Zwischenspeicher bzw. besserer Suche – und müssen Bit für Bit dasselbe
/// ergeben wie vorher. Je Stelle der Vergleich mit der Rechnung ohne Speicher (bzw. der alten Suche, hier im Test nachgebaut) und die Gegenprobe,
/// dass die Ungültig-Machung nötig ist: Mit einer anderen Karte bzw. einem neuen Netz wäre die gemerkte Antwort falsch – und Bauten und Abrisse
/// ändern die Bauorte, ohne dass der Speicher (der nur an der Karte hängt) etwas Falsches liefert.
///   • <see cref="Bauwesen.Bauorte"/> behält die <see cref="Bauwesen.Platzsuche"/> der Karte (Zone und „an der Straße“ je Punkt) über alle Abfragen;
///   • <see cref="Karrennetz"/> merkt sich jede gesuchte Fahrt (das Netz ändert sich nach dem Aufbau nicht);
///   • <see cref="Wasserlauf"/> sucht das nächste Stück der Mittellinie über Blöcke statt Stück für Stück.
/// </summary>
static class ZwischenspeicherTests
{
    static readonly string[] KartenMitWasser = { "koeln", "greybox", "probestadt", "messstadt" };

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Zwischenspeicher: Wasserlauf.Naechste findet Bit für Bit dasselbe Stück wie die alte Suche über die ganze Rasterzelle (Raster, Zufallspunkte, Stützpunkte, Gleichstand)",
            WasserlaufGleich),
        ("Zwischenspeicher: Karrennetz – die gemerkte Fahrt ist Bit für Bit die neu gesuchte; ein neues Netz (Weg weg) beginnt leer und fährt anders",
            KarrennetzGleich),
        ("Zwischenspeicher: Bauorte in einer Greybox-Partie (drei Jahre, der Spieler baut Werkstätten und zieht um – Neubau, Abriss, Bauplatz) nach jeder Änderung und alle vier Stunden gleich der Rechnung ohne Speicher – und die Bauten ändern sie",
            () => BauorteInDerPartie("greybox", 3, 240, mussWechseln: true)),
        ("Zwischenspeicher: Bauorte in einer Köln-Partie (zwei Jahre, alle KI-Häuser) nach jeder Änderung und zweimal am Tag gleich der Rechnung ohne Speicher",
            () => BauorteInDerPartie("koeln", 2, 570, mussWechseln: false)),
        ("Zwischenspeicher: Bauorte nach einem Bau des Spielers auf der Greybox neu – die Liste von vorher wäre falsch",
            BauorteNachBau),
        ("Zwischenspeicher: PlatzVerboten mit gemerkter Platzsuche (Raster der festen Bauten, Zonen je Punkt) gibt an jeder Stelle denselben Grund wie ohne – Greybox und Köln, alle Drehungen",
            PlatzVerbotenGleich),
        ("Zwischenspeicher: Platzsuche gilt nur für ihre Karte – ohne eine Bauzone bzw. einen Weg antwortet eine neue Suche anders, die alte erkennt die fremde Karte",
            PlatzsucheJeKarte),
    };

    // ── Wasserlauf ──────────────────────────────────────────────────────────

    /// <summary>Die Suche bis 14.11.2026, Zeile für Zeile: Rasterzelle von 16 m, darin alle Stücke aufsteigend, das erste mit dem kleinsten Abstand.</summary>
    sealed class AlteSuche
    {
        const double Feld = 16;
        readonly Wasserlauf _lauf;
        readonly Dictionary<(int, int), List<int>> _raster = new();

        public AlteSuche(Wasserlauf lauf)
        {
            _lauf = lauf;
            var punkte = lauf.Punkte;
            for (var k = 0; k + 1 < punkte.Count; k++)
            {
                var (a, b) = (punkte[k].Ort, punkte[k + 1].Ort);
                var r = Math.Max(punkte[k].Breite, punkte[k + 1].Breite) / 2 + Wasserform.Uferbreite(Math.Max(punkte[k].Breite, punkte[k + 1].Breite));
                for (var i = (int)Math.Floor((Math.Min(a.X, b.X) - r) / Feld); i <= (int)Math.Floor((Math.Max(a.X, b.X) + r) / Feld); i++)
                    for (var j = (int)Math.Floor((Math.Min(a.Z, b.Z) - r) / Feld); j <= (int)Math.Floor((Math.Max(a.Z, b.Z) + r) / Feld); j++)
                    {
                        if (!_raster.TryGetValue((i, j), out var liste)) _raster[(i, j)] = liste = new List<int>();
                        liste.Add(k);
                    }
            }
        }

        public (int K, double T, double Abstand)? Naechste(Kartenort p)
        {
            var punkte = _lauf.Punkte;
            if (punkte.Count < 2 || !_raster.TryGetValue(((int)Math.Floor(p.X / Feld), (int)Math.Floor(p.Z / Feld)), out var liste)) return null;
            var best = (K: -1, T: 0.0, Abstand: double.MaxValue);
            foreach (var k in liste)
            {
                var (a, b) = (punkte[k].Ort, punkte[k + 1].Ort);
                var (dx, dz) = (b.X - a.X, b.Z - a.Z);
                var l2 = dx * dx + dz * dz;
                var t = l2 == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Z - a.Z) * dz) / l2, 0, 1);
                var d = p.Abstand(new Kartenort(a.X + t * dx, a.Z + t * dz));
                if (d < best.Abstand) best = (k, t, d);
            }
            return best.K < 0 ? null : best;
        }
    }

    static string Bits((int K, double T, double Abstand)? x) =>
        x is { } v ? $"{v.K}/{BitConverter.DoubleToInt64Bits(v.T):X16}/{BitConverter.DoubleToInt64Bits(v.Abstand):X16}" : "null";

    static void WasserlaufGleich()
    {
        var zufall = new Zufallsquelle(1411);
        var (laeufe, punkte, treffer) = (0, 0, 0);
        foreach (var kennung in KartenMitWasser)
        {
            var karte = Stadtkarte.AusDatei(Datenpfad.Karte(kennung));
            foreach (var lauf in karte.Gelaendeform.Wasser.Laeufe)
            {
                laeufe++;
                var alt = new AlteSuche(lauf);
                void Vergleichen(Kartenort p)
                {
                    punkte++;
                    var (neu, vorher) = (Bits(lauf.Naechste(p)), Bits(alt.Naechste(p)));
                    if (neu != vorher) throw new Exception($"{kennung}, {lauf.Name}: bei ({p.X:R}; {p.Z:R}) {neu} statt {vorher}");
                    if (neu != "null") treffer++;
                }
                var b = lauf.Bereich;
                // ein festes Raster über den ganzen Bereich (Schritt nicht auf dem 16-m-Feld, damit Zellränder in allen Lagen vorkommen)
                for (var x = b.X0 - 5; x <= b.X1 + 5; x += 3.7)
                    for (var z = b.Z0 - 5; z <= b.Z1 + 5; z += 3.7)
                        Vergleichen(new Kartenort(x, z));
                // Zufallspunkte nahe der Mittellinie und genau auf den Stützpunkten und Mitten (Gleichstand zweier Stücke)
                for (var k = 0; k < lauf.Punkte.Count; k++)
                {
                    var o = lauf.Punkte[k].Ort;
                    Vergleichen(o);
                    if (k + 1 < lauf.Punkte.Count)
                    {
                        var n = lauf.Punkte[k + 1].Ort;
                        Vergleichen(new Kartenort((o.X + n.X) / 2, (o.Z + n.Z) / 2));
                    }
                    var weite = lauf.Punkte[k].Breite + 20;
                    Vergleichen(new Kartenort(o.X + (zufall.NextDouble() - 0.5) * weite, o.Z + (zufall.NextDouble() - 0.5) * weite));
                }
            }
        }
        Pruefe.Wahr(laeufe >= 1 && treffer > 10_000, $"Zu wenig geprüft: {laeufe} Läufe, {punkte} Punkte, {treffer} im Ufer");
        Console.WriteLine($"          {laeufe} Wasserläufe, {punkte} Punkte, davon {treffer} mit Stück");
    }

    // ── Karrennetz ──────────────────────────────────────────────────────────

    static string Bits(Netzfahrt f) =>
        string.Join(";", f.Stuecke.Select(s => $"{BitConverter.DoubleToInt64Bits(s.Laenge):X}/{BitConverter.DoubleToInt64Bits(s.Anstieg):X}/{s.Art}/{s.Weg}/" +
            $"{BitConverter.DoubleToInt64Bits(s.Stelle):X}/{BitConverter.DoubleToInt64Bits(s.Kostenfaktor):X}")) + "|" +
        string.Join(";", f.Punkte.Select(p => $"{BitConverter.DoubleToInt64Bits(p.X):X}/{BitConverter.DoubleToInt64Bits(p.Z):X}"));

    static void KarrennetzGleich()
    {
        var karte = Stadtkarte.AusDatei(Datenpfad.Karte("koeln"));
        var netz = Karrennetz.Von(karte, marktlinien: true, alleWege: true);
        var maerkte = karte.Marktplaetze.Select(m => new Kartenort(m.Punkt[0], m.Punkt[1])).ToList();
        var ecken = karte.Bauzonen.SelectMany(z => z.Flaeche).ToList();
        var (x0, x1, z0, z1) = (ecken.Min(e => e[0]), ecken.Max(e => e[0]), ecken.Min(e => e[1]), ecken.Max(e => e[1]));
        var zufall = new Zufallsquelle(77);
        var orte = Enumerable.Range(0, 60).Select(_ => new Kartenort(x0 + zufall.NextDouble() * (x1 - x0), z0 + zufall.NextDouble() * (z1 - z0))).ToList();
        orte.Add(new Kartenort(0.0, 0.0));
        orte.Add(new Kartenort(-0.0, 0.0)); // −0 ist ein eigener Schlüssel
        var fahrten = 0;
        // zweimal: beim zweiten Mal kommt jede Fahrt aus dem Speicher
        for (var runde = 0; runde < 2; runde++)
            foreach (var von in orte)
                foreach (var nach in maerkte)
                {
                    Pruefe.Gleich(Bits(netz.SuchenOhneSpeicher(von, nach, true)), Bits(netz.FahrtMitVerlauf(von, nach)), $"Fahrt mit Verlauf, Runde {runde + 1}");
                    Pruefe.Gleich(Bits(netz.SuchenOhneSpeicher(nach, von, false)), Bits(new Netzfahrt(netz.Fahrt(nach, von), netz.SuchenOhneSpeicher(nach, von, false).Punkte)),
                        $"Fahrt zurück, Runde {runde + 1}");
                    fahrten += 2;
                }

        // Gegenprobe: Ohne den meistbefahrenen Weg der ersten Fahrt ist das Netz ein anderes. Merkte sich ein Speicher die Fahrt über Netze hinweg,
        // käme hier die alte Fahrt heraus.
        var (start, ziel) = (orte[0], maerkte[0]);
        var vorher = netz.FahrtMitVerlauf(start, ziel);
        var weg = vorher.Stuecke.Where(s => s.Weg >= 0).GroupBy(s => s.Weg).OrderByDescending(g => g.Sum(s => s.Laenge)).ThenBy(g => g.Key).First().Key;
        var ohne = Stadtkarte.AusDatei(Datenpfad.Karte("koeln"));
        ohne.Wege.RemoveAt(weg);
        var netz2 = Karrennetz.Von(ohne, marktlinien: true, alleWege: true);
        var nachher = netz2.FahrtMitVerlauf(start, ziel);
        Pruefe.Wahr(Bits(nachher) != Bits(vorher), "Ohne den Weg fährt der Karren genauso – die Gegenprobe prüft nichts");
        Pruefe.Gleich(Bits(netz2.SuchenOhneSpeicher(start, ziel, true)), Bits(nachher), "neues Netz: gemerkt gleich neu gesucht");
        Pruefe.Gleich(Bits(vorher), Bits(netz.FahrtMitVerlauf(start, ziel)), "altes Netz bleibt, wie es war");
        Console.WriteLine($"          {fahrten} Fahrten verglichen");
    }

    // ── Bauorte ─────────────────────────────────────────────────────────────

    static string Text(IReadOnlyList<BauBefehl> orte) => string.Join(";", orte.Select(o =>
        $"{o}/{BitConverter.DoubleToInt64Bits(o.X):X}/{BitConverter.DoubleToInt64Bits(o.Z):X}"));

    /// <param name="schritt">Ohne Änderung alle so viele Minuten verglichen (in Köln kostet eine Rechnung ohne Speicher etwa eine halbe Sekunde).</param>
    /// <param name="mussWechseln">Muss sich die Liste dabei ändern (die Gegenprobe: ohne Ungültig-Machung käme die alte)?</param>
    static void BauorteInDerPartie(string kennung, int jahre, int schritt, bool mussWechseln)
    {
        var daten = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
        var haeuser = JsonNode.Parse(File.ReadAllText(Datenpfad.Karte(kennung)))!["start"]?["haeuser"]?.AsArray().Count ?? 1;
        var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 101, Siegbedingung = Siegbedingung.Endlos },
            daten, new NeuePartie(Rivalen: haeuser, Startvermoegen: 200_000, Beruf: "Schmied"), karte: Stadtkarte.AusDatei(Datenpfad.Karte(kennung)));
        var w = sim.Wirtschaft!;
        var bauen = w.Bauen;
        Pruefe.Wahr(bauen.IstAktiv, "In Köln wird gebaut");
        var vorlagen = new BauBefehl[] { new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0), new WohnhausBauen(2, 0, 0, 0), new WohnhausBauen(3, 0, 0, 0) };
        var zuletzt = new string?[vorlagen.Length];
        var (vergleiche, wechsel, aenderungen) = (0, 0, 0);
        var geaendert = false;
        bauen.BauBegonnen += _ => geaendert = true;
        bauen.BauFertig += _ => geaendert = true;
        var abgerissen = 0;
        bauen.Abgerissen += _ => { geaendert = true; abgerissen++; };
        bauen.BauplatzBebaut += _ => geaendert = true;

        void Vergleichen(string wann)
        {
            for (var i = 0; i < vorlagen.Length; i++)
            {
                var gemerkt = Text(bauen.FreieOrte(vorlagen[i]));
                var neu = Text(bauen.FreieOrteRechnen(vorlagen[i]));
                if (gemerkt != neu) throw new Exception($"{wann}, {vorlagen[i]}: gemerkte Bauorte anders als neu gerechnet");
                Pruefe.Gleich(gemerkt, Text(bauen.FreieOrte(vorlagen[i])), "zweimal gefragt");
                if (zuletzt[i] is { } z && z != neu) wechsel++; // die Liste hängt an den Grundstücken – gemerkt wird davon nichts
                zuletzt[i] = neu;
                vergleiche++;
            }
        }

        // Mit Gegenprobe baut der Spieler selbst (die KI baut in den ersten Jahren selten): je Jahr eine Gießerei am ersten freien Ort und im
        // ersten Jahr ein größeres Wohnhaus – nach dem Umzug wird das alte abgerissen und zum Bauplatz.
        var gebaut = new List<string>();
        void SpielerBaut(BauBefehl vorlage)
        {
            if (w.Bauen.Bauorte(spieler, vorlage).FirstOrDefault() is { } ort && w.Ausfuehren(spieler, ort).Erfolg) gebaut.Add(vorlage.GetType().Name);
        }

        Vergleichen("Start");
        for (var t = 0; t < jahre * Spielzeit.MinutenProTag; t++)
        {
            if (mussWechseln && t % Spielzeit.MinutenProTag == 120)
            {
                spieler.Stand = Math.Max(spieler.Stand, 3);
                SpielerBaut(new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0));
                if (t < Spielzeit.MinutenProTag) SpielerBaut(new WohnhausBauen(spieler.WohnhausStufe + 1, 0, 0, 0));
            }
            sim.Tick();
            if (geaendert) { aenderungen++; geaendert = false; Vergleichen($"Minute {t}, nach einer Änderung"); }
            else if (t % schritt == 0) Vergleichen($"Minute {t}");
        }
        if (mussWechseln)
        {
            Pruefe.Wahr(gebaut.Contains(nameof(WohnhausBauen)) && gebaut.Count(g => g == nameof(WerkstattBauen)) >= 2, $"Der Spieler baute nur {string.Join(", ", gebaut)}");
            Pruefe.Wahr(abgerissen > 0, "Kein Abriss – der Test prüft die Bauorte nach dem Abriss nicht");
            Pruefe.Wahr(wechsel > 0, "Die Bauorte änderten sich nie – der Test prüft nicht, dass der Speicher nichts von den Grundstücken hält");
        }
        Console.WriteLine($"          {vergleiche} Vergleiche, {aenderungen} Änderungen an Grundstücken, {wechsel}× andere Bauorte als die gemerkten");
    }

    static void BauorteNachBau()
    {
        var daten = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
        var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 5 }, daten,
            new NeuePartie(Startvermoegen: 100_000, Beruf: "Schmied"), karte: Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));
        var w = sim.Wirtschaft!;
        spieler.Stand = 1; // Bürger: darf eine Gießerei bauen
        var vorlage = new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0);
        var vorher = w.Bauen.Bauorte(spieler, vorlage);
        Pruefe.Wahr(vorher.Count > 0, $"Auf der Greybox gibt es Bauorte für eine Gießerei ({w.Bauen.BauherrVerboten(spieler, vorlage)}; frei {w.Bauen.FreieOrte(vorlage).Count})");
        var textVorher = Text(vorher);
        var ergebnis = w.Ausfuehren(spieler, vorher[0]);
        Pruefe.Wahr(ergebnis.Erfolg, $"Bau abgelehnt: {ergebnis.Meldung}");
        var nachher = w.Bauen.Bauorte(spieler, vorlage);
        Pruefe.Gleich(Text(w.Bauen.FreieOrteRechnen(vorlage)), Text(nachher), "nach dem Bau: gemerkt gleich neu gerechnet");
        Pruefe.Wahr(Text(nachher) != textVorher, "Der Bau änderte die Bauorte nicht – die Gegenprobe prüft nichts");
        Pruefe.Wahr(!nachher.Contains(vorher[0]), "Der eben bebaute Ort ist noch frei");
    }

    static void PlatzsucheJeKarte()
    {
        var karte = Stadtkarte.AusDatei(Datenpfad.Karte("koeln"));
        var suche = new Bauwesen.Platzsuche(karte);
        var ohneZone = Stadtkarte.AusDatei(Datenpfad.Karte("koeln"));
        var weg = ohneZone.Bauzonen[0];
        ohneZone.Bauzonen.RemoveAt(0);
        var ohneWeg = Stadtkarte.AusDatei(Datenpfad.Karte("koeln"));
        var strasse = ohneWeg.Wege.FindIndex(w => w.IstKarrenstrasse);
        var verlauf = ohneWeg.Wege[strasse].Verlauf;
        ohneWeg.Wege.RemoveAt(strasse);
        Pruefe.Wahr(suche.Fuer(karte), "eigene Karte");
        Pruefe.Wahr(!suche.Fuer(ohneZone) && !suche.Fuer(ohneWeg) && !suche.Fuer(Stadtkarte.AusDatei(Datenpfad.Karte("koeln"))), "fremde Karte erkannt");
        // die Gegenprobe: an einem Punkt der entfernten Zone bzw. auf dem entfernten Weg antwortet die alte (gemerkte) Suche anders als eine neue
        var (zx, zz) = (weg.Flaeche.Average(p => p[0]), weg.Flaeche.Average(p => p[1]));
        var innen = Enumerable.Range(0, 400).Select(i => (X: zx + (i % 20 - 10) * 2.0, Z: zz + (i / 20 - 10) * 2.0)).First(p => weg.Enthaelt(p.X, p.Z));
        Pruefe.Wahr(suche.ZoneBei(innen.X, innen.Z) is not null, "Die Zone liegt in der alten Karte");
        Pruefe.Wahr(suche.ZoneBei(innen.X, innen.Z) != new Bauwesen.Platzsuche(ohneZone).ZoneBei(innen.X, innen.Z), "Ohne die Zone dieselbe Antwort – die Gegenprobe prüft nichts");
        var neu = new Bauwesen.Platzsuche(ohneWeg);
        var abweichend = verlauf.Count(p => suche.AnDerStrasse(p[0], p[1], 0.5, true) != neu.AnDerStrasse(p[0], p[1], 0.5, true));
        Pruefe.Wahr(verlauf.All(p => suche.AnDerStrasse(p[0], p[1], 0.5, true)), "Der Verlauf liegt an der Straße");
        Pruefe.Wahr(abweichend > 0, "Ohne den Weg überall dieselbe Antwort – die Gegenprobe prüft nichts");
    }

    static void PlatzVerbotenGleich()
    {
        var daten = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
        var stellen = 0;
        var gruende = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var kennung in new[] { "greybox", "koeln" })
        {
            var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 3 }, daten, new NeuePartie(),
                karte: Stadtkarte.AusDatei(Datenpfad.Karte(kennung)));
            var bauen = sim.Wirtschaft!.Bauen;
            var karte = bauen.Karte!;
            var suche = new Bauwesen.Platzsuche(karte); // eine für alle Stellen, wie in FreieOrte
            var ecken = karte.Bauzonen.SelectMany(z => z.Flaeche).ToList();
            var schritt = kennung == "koeln" ? 9.0 : 3.0;
            foreach (var vorlage in new BauBefehl[] { new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0), new WohnhausBauen(2, 0, 0, 0) })
                for (var x = Math.Floor(ecken.Min(e => e[0])) - 10; x <= ecken.Max(e => e[0]) + 10; x += schritt)
                    for (var z = Math.Floor(ecken.Min(e => e[1])) - 10; z <= ecken.Max(e => e[1]) + 10; z += schritt)
                        for (var drehung = 0; drehung < 4; drehung++)
                        {
                            var befehl = bauen.Einrasten(vorlage with { X = x, Z = z, Drehung = drehung });
                            var ohne = bauen.PlatzVerboten(befehl);
                            Pruefe.Gleich(ohne, bauen.PlatzVerboten(befehl, suche), $"{kennung}: {befehl}");
                            if (ohne is not null) gruende.Add(ohne.Split(' ')[0] + " " + ohne.Split(' ').ElementAtOrDefault(1));
                            stellen++;
                        }
        }
        Pruefe.Wahr(gruende.Any(g => g.StartsWith("Hier steht", StringComparison.Ordinal)), "Keine Stelle auf einem festen Bau – das Raster der Bauten ist nicht geprüft");
        Console.WriteLine($"          {stellen} Stellen; Gründe: {string.Join(" | ", gruende)}");
    }
}
