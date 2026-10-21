using Reichsgilde.Core.Zeit;
using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Balance-Messung (kein Test): spielt viele Partien mit verschiedenen Startwerten durch und zählt,
/// was in den Familien geschieht. Der Spieler bleibt dabei passiv (ledig, nur die Startgießerei).
/// Aufruf:  dotnet run --project tests/Reichsgilde.Core.Tests -- messen 40 20 Zwerg
///          … -- messen 30 20 Mensch gericht   (der Spieler setzt jedes Jahr einen Spitzel auf den Rivalen und klagt, sobald es reicht)
///          … -- messen 30 20 Mensch - Wagemutig   (fünftes Argument: Charakter des Rivalen)
///          … -- messen 30 20 greybox   („greybox“ an beliebiger Stelle: mit der Greybox-Karte – Bauen, Leerstand, Neubau)
/// </summary>
static class Messung
{
    static Stadtwirtschaft? w0(Simulation sim) => sim.Wirtschaft;

    public static int Dynastie(string[] args)
    {
        var daten = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
        var mitKarte = args.Contains("greybox");
        args = args.Where(a => a != "greybox").ToArray();
        var kartenpfad = Datenpfad.Karte("greybox");
        // Bauen und Verkaufen der KI-Häuser (Spiel-KI, PR #108): gezählt über ihre Meldungen
        var bauen = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var staendeKi = new SortedDictionary<string, int>(StringComparer.Ordinal);
        // Sparen auf den Stand: Jahre, die ein KI-Haus zum Jahresende spart, und die längste Folge davon (bleibt eins hängen?)
        var sparjahre = new SortedDictionary<string, (int Jahre, int Laengste)>(StringComparer.Ordinal);
        int bankrottKi = 0;
        var jahre = args.Length > 0 ? int.Parse(args[0]) : 40;
        var startwerte = args.Length > 1 ? int.Parse(args[1]) : 10;
        var volk = args.Length > 2 ? Enum.Parse<Volk>(args[2]) : Volk.Mensch;
        var gericht = args.Length > 3 && args[3] == "gericht";
        var charakter = args.Length > 4 ? Enum.Parse<KiCharakter>(args[4]) : KiCharakter.Ausgewogen;
        int gaunerstuecke = 0, kerker = 0;
        var gaunerJeArt = new SortedDictionary<string, int>(StringComparer.Ordinal); // Banden der KI je Art und Ziel
        double gaunerBilanz = 0, schadenSpieler = 0;
        int urteileSchuldig = 0, urteileFrei = 0, amtVerloren = 0;
        double bestechung = 0;

        int verheiratet = 0, erloschen = 0, buergermeister = 0;
        double heiratsjahre = 0, kinder = 0, erbfaelle = 0, bmJahre = 0, kostenSpieler = 0, vermoegen = 0;
        double talentOberhaupt = 0, ausgebildet = 0;
        int pestjahre = 0, krankheitstote = 0, pesttote = 0, krankJahre = 0;
        double familienKi = 0;
        // Die weiteren KI-Häuser (Eichwald, Kieselbach): Vermögen, Gewinn, Bürgermeister, erloschen, Partien, Waren
        var haeuser = new[] { (Partieaufbau.ZweiterRivale, "Tischler"), (Partieaufbau.DritterRivale, "Steinmetz") };
        var weitere = haeuser.ToDictionary(h => h.Item1, _ => (Vermoegen: 0.0, Gewinn: 0.0, Bm: 0, Erloschen: 0, Partien: 0, Waren: new SortedDictionary<string, int>()));
        var eisenbartWaren = new SortedDictionary<string, int>();
        var abschluesse = new SortedDictionary<string, int>();
        // Meistergrade und Gebäudestufen der KI-Häuser: Summe der Jahre, in denen ein Ziel erreicht wurde, und wie oft
        var meisterJahre = new SortedDictionary<string, (double Jahre, int Anzahl)>(StringComparer.Ordinal);
        var gelernt = new SortedDictionary<string, int>(StringComparer.Ordinal); // „Eisenbart → Alchimist" je Partie
        var glaube = new SortedDictionary<string, int>(StringComparer.Ordinal); // „Stadtglaube katholisch", „Eisenbart katholisch" am Ende
        double katholikenAnteil = 0;
        void Erreicht(string ziel, int jahr) { var (j, n) = meisterJahre.GetValueOrDefault(ziel); meisterJahre[ziel] = (j + jahr, n + 1); }
        for (var s = 1; s <= startwerte; s++)
        {
            var (sim, spieler) = Partieaufbau.Erstellen(
                Partieeinstellungen.Standard with { Zufallsstartwert = s * 101, Siegbedingung = Siegbedingung.Endlos },
                daten, new NeuePartie(Volk: volk, Rivale: charakter), karte: mitKarte ? Reichsgilde.Core.Bauen.Stadtkarte.AusDatei(kartenpfad) : null);
            if (w0(sim) is { } w1) w1.Meldung += m =>
            {
                if (m.Contains("heuert im Hinterzimmer") || m.Contains("Erpresserbrief:")) gaunerstuecke++;
                // „Eichwald: heuert im Hinterzimmer eine Bande an: Einbruch ins Wohnhaus gegen Haus Eisenbart – …“ → je Art und Ziel
                if (m.Split(": heuert im Hinterzimmer eine Bande an: ") is [_, var rest]
                    && rest.Split(" – ")[0].Split(" (")[0] is var tat && tat.Contains(" gegen "))
                    gaunerJeArt[tat] = gaunerJeArt.GetValueOrDefault(tat) + 1;
            };
            if (w0(sim) is { } w2)
            {
                var kiNamen = w2.Rivalen.Select(r => r.Konto.Inhaber + ": ").ToList();
                void Zaehlen(string was) => bauen[was] = bauen.GetValueOrDefault(was) + 1;
                w2.Meldung += m =>
                {
                    if (!kiNamen.Any(m.StartsWith)) return;
                    if (m.Contains("kauft das leerstehende")) Zaehlen("Leerstand gekauft");
                    else if (m.Contains("baut ein neues Wohnhaus, ein ")) Zaehlen($"Wohnhaus neu gebaut ({m.Split("baut ein neues Wohnhaus, ein ")[1].Split(' ')[0]})");
                    else if (m.Contains(" neu (") && m.Contains("keine freie Werkstatt")) Zaehlen("Werkstatt neu gebaut");
                    else if (m.Contains("baut das Wohnhaus zum")) Zaehlen("Wohnhaus ausgebaut");
                    else if (m.Contains("um den Bankrott abzuwenden")) Zaehlen("verkauft vor dem Bankrott");
                    else if (m.Contains("Jahre in Folge Verlust")) Zaehlen("verkauft nach Verlustjahren");
                    else if (m.Contains("wollte, durfte aber nicht")) Zaehlen("abgelehnt");
                    else if (m.Contains("erwarteter Gewinn ≈") && (m.Contains(": kauft ") || m.Contains(": pachtet "))) Zaehlen("Betrieb gekauft/gepachtet");
                };
            }
            var w = sim.Wirtschaft!;
            var rivale = w.Konten.First(k => k != spieler);
            int? heirat = null, bm = null;
            var erreicht = new HashSet<string>();
            var sparfolge = new Dictionary<string, int>();
            for (var j = 1; j <= jahre; j++)
            {
                if (gericht && spieler.IstAktiv)
                {
                    if (w.Justiz.Beweiskraft(spieler, rivale) >= w.Justiz.Daten.SichereBeweiskraft)
                        w.Ausfuehren(spieler, new Anklagen(rivale.Inhaber));
                    if (spieler.Vermoegen > 1500)
                        w.Ausfuehren(spieler, new SpitzelAnsetzen(rivale.Inhaber));
                    foreach (var eigener in w.Betriebe.Where(x => x.Konto == spieler && !x.Waechter && w.Gauner.LetzterAngriff(x.Name) is not null).ToList())
                        w.Ausfuehren(spieler, new WaechterSetzen(eigener.Name, true));
                }
                var hatteAmt = w.Rat.AmtVon(rivale) is not null;
                var jahr = sim.Zeit.Jahr;
                while (sim.Zeit.Jahr == jahr) sim.Tick();
                heirat ??= w.Familien.Von(rivale)!.Ehepartner is not null ? j : null;
                bm ??= w.Rat.AmtNamens("Bürgermeister")?.Konto == rivale ? j : null;
                kostenSpieler += spieler.Vorjahr?.Familie ?? 0;
                bestechung += rivale.Vorjahr?.Geschenke ?? 0;
                gaunerBilanz -= rivale.Vorjahr?.Gaunerei ?? 0;
                schadenSpieler += (spieler.Vorjahr?.Raubwert ?? 0) + (spieler.Vorjahr?.Schaden ?? 0);
                if (w.Justiz.IstAktiv && w.Justiz.ImKerker(rivale)) kerker++;
                if (w.Gesundheit.IstPestjahr) pestjahre++;
                if (w.Familien.Von(rivale)?.Oberhaupt.Krankheit is not null) krankJahre++;
                familienKi += rivale.Vorjahr?.Familie ?? 0;
                foreach (var r in w.Rivalen.Where(r => r.Konto.IstAktiv))
                {
                    var name = r.Konto.Inhaber.Replace("Haus ", "");
                    sparfolge[name] = r.Spart ? sparfolge.GetValueOrDefault(name) + 1 : 0;
                    var (sj, laengste) = sparjahre.GetValueOrDefault(name);
                    sparjahre[name] = (sj + (r.Spart ? 1 : 0), Math.Max(laengste, sparfolge[name]));
                }
                foreach (var ki in w.Rivalen.Select(r => r.Konto))
                {
                    var name = ki.Inhaber.Replace("Haus ", "");
                    var stufe = w.Betriebe.Where(x => x.Konto == ki && x.IstWerkstatt).Select(x => x.Stufe).DefaultIfEmpty(0).Max();
                    foreach (var ziel in new[] { (3, $"{name} Grad 3"), (5, $"{name} Grad 5"), (6, $"{name} Grad 6") })
                        if (ki.HoechsterMeistergrad >= ziel.Item1 && erreicht.Add(ziel.Item2)) Erreicht(ziel.Item2, j);
                    foreach (var ziel in new[] { (2, $"{name} Stufe 2"), (3, $"{name} Stufe 3") })
                        if (stufe >= ziel.Item1 && erreicht.Add(ziel.Item2)) Erreicht(ziel.Item2, j);
                }
                foreach (var u in w.Justiz.LetzteUrteile.Where(u => u.Beklagter == rivale))
                {
                    if (u.Schuldig) urteileSchuldig++; else urteileFrei++;
                    if (u.Schuldig && hatteAmt && u.Text.Contains("Verlust des Amtes")) amtVerloren++;
                }
            }
            foreach (var (ware, stueck) in rivale.Vorjahr?.Hergestellt ?? new())
                eisenbartWaren[ware] = eisenbartWaren.GetValueOrDefault(ware) + stueck;
            foreach (var (hausName, _) in haeuser)
            {
                if (w.Konten.FirstOrDefault(k => k.Inhaber == hausName) is not { } haus) continue;
                var x = weitere[hausName];
                x.Partien++;
                x.Vermoegen += haus.Reinvermoegen;
                x.Gewinn += haus.Vorjahr?.Gewinn ?? 0;
                if (haus.IstErloschen) x.Erloschen++;
                if (w.Rat.AmtNamens("Bürgermeister")?.Konto == haus) x.Bm++;
                foreach (var (ware, stueck) in haus.Vorjahr?.Hergestellt ?? new())
                    x.Waren[ware] = x.Waren.GetValueOrDefault(ware) + stueck;
                weitere[hausName] = x;
            }
            foreach (var ki in w.Rivalen.Select(r => r.Konto))
                foreach (var beruf in ki.Berufe.Skip(0).Where(b => !w.Betriebe.Any(x => x.Konto == ki && x.Artdaten.Beruf == b && x.Name.Contains(ki.Inhaber.Replace("Haus ", "")))))
                    gelernt[$"{ki.Inhaber.Replace("Haus ", "")} → {beruf}"] = gelernt.GetValueOrDefault($"{ki.Inhaber.Replace("Haus ", "")} → {beruf}") + 1;
            if (w.Glaube.IstAktiv)
            {
                void Zaehlen(string schluessel) => glaube[schluessel] = glaube.GetValueOrDefault(schluessel) + 1;
                Zaehlen($"Stadtglaube {Reichsgilde.Core.Glaube.KonfessionTexte.StadtglaubeText(w.Glaube.Stadtglaube)}");
                foreach (var ki in w.Rivalen.Select(r => r.Konto))
                    Zaehlen($"{ki.Inhaber.Replace("Haus ", "")} {w.Glaube.GlaubeVon(ki)?.ToString().ToLowerInvariant()}");
                katholikenAnteil += w.Glaube.Anteil(Reichsgilde.Core.Glaube.Konfession.Katholisch);
            }
            foreach (var ki in w.Rivalen.Select(r => r.Konto))
            {
                staendeKi[w.StandVon(ki).Name] = staendeKi.GetValueOrDefault(w.StandVon(ki).Name) + 1;
                if (ki.IstBankrott) bankrottKi++;
            }
            var f = w.Familien.Von(rivale)!;
            var nachkommen = f.Mitglieder.Count(p => p.Vater is not null);
            if (heirat is { } h) { verheiratet++; heiratsjahre += h; }
            if (bm is { } b) { buergermeister++; bmJahre += b; }
            if (rivale.IstErloschen) erloschen++;
            kinder += nachkommen;
            erbfaelle += f.Ahnen.Count;
            vermoegen += rivale.Reinvermoegen;
            talentOberhaupt += f.Oberhaupt.TalentSumme;
            var gestorben = f.Mitglieder.Concat(f.Ahnen).Distinct().Where(p => p.Todesursache is not null).ToList();
            krankheitstote += gestorben.Count;
            pesttote += gestorben.Count(p => p.Todesursache == "an der Pest");
            ausgebildet += f.Mitglieder.Count(p => p.Abschluss is not null);
            foreach (var p in f.Mitglieder.Where(p => p.Abschluss is not null))
                abschluesse[p.Abschluss!] = abschluesse.GetValueOrDefault(p.Abschluss!) + 1;
            Console.WriteLine($"Startwert {s * 101,5}: {f.Oberhaupt.Name} ({f.Oberhaupt.Alter(w.Familien.Jahr)} J.), " +
                              $"Heirat im Jahr {heirat?.ToString() ?? "–"}{(f.Mitglieder.FirstOrDefault(p => p.Geburtsname != f.Nachname) is { } partner ? $" mit {partner.Volk.Einzahl()}" : "")}, Nachkommen {nachkommen} (lebend {f.Lebende.Count(p => p.Vater is not null)}), " +
                              $"Erbfälle {f.Ahnen.Count}{(rivale.IstErloschen ? ", ERLOSCHEN" : "")}, {w.StandVon(rivale).Name}, " +
                              $"{Zahlenformat.Ganz(rivale.Reinvermoegen)} Gulden, Bürgermeister ab Jahr {bm?.ToString() ?? "–"}");
        }
        Console.WriteLine();
        Console.WriteLine($"Haus Eisenbart über {jahre} Jahre, {startwerte} Startwerte (Spieler: {volk.Mehrzahl()}, passiv):");
        Console.WriteLine($"  verheiratet {verheiratet}/{startwerte}, im Schnitt im Jahr {(verheiratet > 0 ? heiratsjahre / verheiratet : 0):N1}");
        Console.WriteLine($"  Nachkommen Ø {kinder / startwerte:N1}, Erbfälle Ø {erbfaelle / startwerte:N1}, erloschen {erloschen}/{startwerte}");
        Console.WriteLine($"  Bürgermeister {buergermeister}/{startwerte}, im Schnitt ab Jahr {(buergermeister > 0 ? bmJahre / buergermeister : 0):N1}");
        Console.WriteLine($"  Vermögen Ø {Zahlenformat.Ganz(vermoegen / startwerte)} Gulden; Familienkosten des Spielers Ø {Zahlenformat.Ganz(kostenSpieler / startwerte / jahre)} Gulden im Jahr");
        Console.WriteLine($"  Geschenke an Ratsherren (Bestechung) Ø {Zahlenformat.Ganz(bestechung / startwerte)} Gulden über {jahre} Jahre");
        Console.WriteLine($"  Gaunerstücke der KI ({charakter}): {gaunerstuecke} in {startwerte} Partien, Bilanz Hinterzimmer {Zahlenformat.Ganz(gaunerBilanz / startwerte)} je Partie; " +
                          $"Raub und Schäden beim Spieler Ø {Zahlenformat.Ganz(schadenSpieler / startwerte)}; Kerkerjahre der KI {kerker}");
        Console.WriteLine("  Banden der KI je Art und Ziel (Summe): " + string.Join(", ", gaunerJeArt.Select(x => $"{x.Key} {x.Value}×")));
        Console.WriteLine($"  Talente des Oberhaupts am Ende Ø {talentOberhaupt / startwerte:N1} Sterne (Summe aller fünf); ausgebildete Nachkommen Ø {ausgebildet / startwerte:N1} " +
                          $"({string.Join(", ", abschluesse.Select(x => $"{x.Key} {x.Value}"))})");
        Console.WriteLine($"  Gesundheit: {pestjahre} Pestjahre in {startwerte} Partien; Krankheitstote im Haus Eisenbart {krankheitstote} (davon Pest {pesttote}); " +
                          $"Oberhaupt zum Jahresbeginn krank in {krankJahre} von {startwerte * jahre} Jahren; Familienkosten der KI Ø {Zahlenformat.Ganz(familienKi / startwerte / jahre)} im Jahr");
        Console.WriteLine("  Haus Eisenbart hergestellt im letzten Jahr Ø " + string.Join(", ", eisenbartWaren.Select(x => $"{x.Key} {x.Value / startwerte}")));
        foreach (var (hausName, beruf) in haeuser)
        {
            var x = weitere[hausName];
            if (x.Partien == 0) continue;
            Console.WriteLine($"  {hausName} ({beruf}): Vermögen Ø {Zahlenformat.Ganz(x.Vermoegen / x.Partien)}, Gewinn im letzten Jahr Ø {Zahlenformat.Ganz(x.Gewinn / x.Partien)}, " +
                              $"am Ende Bürgermeister in {x.Bm}/{x.Partien}, erloschen {x.Erloschen}; hergestellt im letzten Jahr Ø " +
                              string.Join(", ", x.Waren.Select(w => $"{w.Key} {w.Value / x.Partien}")));
        }
        if (glaube.Count > 0)
            Console.WriteLine($"  Glaube am Ende (in n Partien): {string.Join(", ", glaube.Select(x => $"{x.Key} {x.Value}×"))}; Katholiken Ø {katholikenAnteil / startwerte * 100:0} %");
        Console.WriteLine("  Von der KI erlernte Berufe (in n Partien): " + (gelernt.Count == 0 ? "keine" : string.Join(", ", gelernt.Select(x => $"{x.Key} {x.Value}×"))));
        Console.WriteLine("  Meistergrade/Stufen der KI (erreicht in n Partien, Ø Jahr): " +
                          string.Join(", ", meisterJahre.Select(x => $"{x.Key} {x.Value.Anzahl}× ab {x.Value.Jahre / x.Value.Anzahl:N1}")));
        if (mitKarte || bauen.Count > 0)
            Console.WriteLine($"  KI-Häuser bauen und verkaufen{(mitKarte ? " (Greybox-Karte)" : "")} (Summe über alle Partien): " +
                              (bauen.Count == 0 ? "nichts" : string.Join(", ", bauen.Select(x => $"{x.Key} {x.Value}×"))));
        Console.WriteLine($"  Stand der KI-Häuser am Ende (alle Partien): {string.Join(", ", staendeKi.Select(x => $"{x.Key} {x.Value}"))}; bankrott {bankrottKi}");
        Console.WriteLine("  Sparjahre der KI zum Jahresende (Ø je Partie, längste Folge in einer Partie): " +
                          string.Join(", ", sparjahre.Select(x => $"{x.Key} {(double)x.Value.Jahre / startwerte:N1} (längste {x.Value.Laengste})")));
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
        w.Ausfuehren(Wohnhaushilfe.MitWohnhaus(w, spieler), new StandErwerben());
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

    /// <summary>
    /// Spielstand für Klicktests des Kredits: Haus Eisenbart führt die Wechselstube am Markt (ein Jahr gelaufen, die Kasse ist gefüllt),
    /// der Spieler kann dort leihen.
    /// </summary>
    public static int GeldleiherSpielstand(string datei)
    {
        var daten = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
        var (sim, spieler) = Partieaufbau.Erstellen(
            Partieeinstellungen.Standard with { Zufallsstartwert = 303, Siegbedingung = Siegbedingung.Endlos },
            daten, new NeuePartie(Startvermoegen: 10_000));
        var w = sim.Wirtschaft!;
        var rivale = w.Konten.First(k => k != spieler);
        w.BetriebEroeffnen("Wechselstube am Markt", Betriebsart.Wechselstube, rivale, "");
        var j = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == j) sim.Tick();
        for (var i = 0; i < 4 * 60; i++) sim.Tick();
        File.WriteAllText(datei, Speicherung.Speichern(sim, spieler));
        var b = w.Betriebe.First(x => x.Name == "Wechselstube am Markt");
        Console.WriteLine($"{sim.Zeit.DatumText}: Kasse {w.Bank.Kasse(b):F0}, Zins {w.Bank.Zinssatz(rivale):P0}, Höchstkredit {w.Bank.Hoechstkredit(rivale, spieler):F0}");
        Console.WriteLine($"gespeichert: {datei}");
        return 0;
    }

    /// <summary>Spielstand mit verheiratetem Oberhaupt und Kindern im Ausbildungsalter – für Klicktests der Familientafel.</summary>
    /// <summary>
    /// Spielstand für Klicktests der Meistergrade: Der Spieler hat Gießerei und Grube voll belegt und
    /// arbeitet, bis seine Erfahrung für den 3. Meistergrad reicht – Prüfungen und Ausbau liegen beim Spieler.
    /// Mit <paramref name="mindestJahre"/> läuft die Partie länger (die KI-Häuser bauen bis Stufe 3 aus).
    /// </summary>
    public static int MeisterSpielstand(string datei, int mindestJahre = 0)
    {
        var daten = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
        var (sim, spieler) = Partieaufbau.Erstellen(
            Partieeinstellungen.Standard with { Zufallsstartwert = 404, Siegbedingung = Siegbedingung.Endlos },
            daten, new NeuePartie(Startvermoegen: 20_000));
        var w = sim.Wirtschaft!;
        foreach (var b in w.Betriebe.Where(b => b.Konto == spieler))
            w.Ausfuehren(spieler, new PersonalSetzen(b.Name, b.IstWerkstatt ? "Geselle" : "Bergmann", b.Plaetze));
        var p3 = w.Daten.Meistergrade!.PruefungFuer(3)!.Erfahrung;
        while (spieler.Erfahrung("Schmied") < p3 || sim.Zeit.Jahr - Spielzeit.StartJahr < mindestJahre) { var j = sim.Zeit.Jahr; while (sim.Zeit.Jahr == j) sim.Tick(); }
        File.WriteAllText(datei, Speicherung.Speichern(sim, spieler));
        Console.WriteLine($"{sim.Zeit.DatumText}: Erfahrung {spieler.Erfahrung("Schmied"):N0} Std., Grad {spieler.Meistergrad("Schmied")}, " +
                          $"{spieler.Vermoegen:N0} Gulden, {spieler.Aktionspunkte.Vorrat} AP");
        Console.WriteLine($"gespeichert: {datei}");
        return 0;
    }

    public static int FamilienSpielstand(string datei) => FamilienSpielstand(datei, pest: false);

    /// <summary>Wie <see cref="FamilienSpielstand(string)"/>, aber im Pestjahr – mit kranken Familienmitgliedern (Klicktest Gesundheit).</summary>
    public static int FamilienSpielstand(string datei, bool pest)
    {
        var daten = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
        var (sim, spieler) = Partieaufbau.Erstellen(
            Partieeinstellungen.Standard with { Zufallsstartwert = 303, Siegbedingung = Siegbedingung.Endlos },
            daten, new NeuePartie(Startvermoegen: 20_000));
        var w = sim.Wirtschaft!;
        var buch = w.Familien;
        var f = buch.Von(spieler)!;
        void Jahr() { var j = sim.Zeit.Jahr; while (sim.Zeit.Jahr == j) sim.Tick(); }
        for (var jahr = 0; jahr < 6 && f.Ehepartner is null; jahr++)
        {
            var k = buch.Kandidaten.FirstOrDefault(x => buch.WerbenVerboten(spieler, x) is null);
            if (k is null) { Jahr(); continue; }
            w.Ausfuehren(spieler, new Werben(k.Person.Nummer, 1000));
            if (buch.Bereitschaft(spieler, k) >= buch.Heiratsschwelle(k))
                w.Ausfuehren(spieler, new Heiraten(k.Person.Nummer));
            else
                Jahr();
        }
        for (var jahr = 0; jahr < 40 && !f.Lebende.Any(p => p.Vater is not null && p.Alter(buch.Jahr) >= buch.AusbildungAb(p.Volk)); jahr++)
            Jahr();
        if (pest)
        {
            w.Gesundheit.NaechstePest = buch.Jahr + 1;
            Jahr();
            w.Gesundheit.Erkranken(spieler, f.Oberhaupt, "Lungenentzündung");
            foreach (var kind in f.Lebende.Where(p => p.Vater is not null && p.Krankheit is null).Take(1))
                w.Gesundheit.Erkranken(spieler, kind, "Husten");
            w.Ausfuehren(spieler, new SchnapsKaufen(1));
        }
        File.WriteAllText(datei, Speicherung.Speichern(sim, spieler));
        Console.WriteLine($"{sim.Zeit.DatumText}: {f.Oberhaupt.Name}, Kinder: {string.Join(", ", f.Lebende.Where(p => p.Vater is not null).Select(p => $"{p.Vorname} ({p.Alter(buch.Jahr)})"))}");
        Console.WriteLine($"gespeichert: {datei}");
        return 0;
    }
}
