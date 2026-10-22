using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

// ═════════════════════════════════════════════════════════════════════════════
//  Messpartie – eine Partie für den Balancing-Bericht (Messung, kein Test)
//
//  Ein Messaufbau beschreibt die Partie vollständig (Karte, Startwert, Beruf und Volk des Spielers, wer den Spieler führt, wie viele
//  KI-Häuser …). Gleicher Aufbau = gleiche Partie, Wurf für Wurf – deshalb dürfen viele Partien zugleich laufen (jede liest ihre eigenen
//  Daten und ihre eigene Karte) und das Ergebnis bleibt dasselbe.
//
//  Gemessen wird am Ende jedes Spieljahres (Vermögen, Gewinn, Stand, Stufe je Haus), in den Stichjahren zusätzlich der Markt (Preise je
//  Stunde, Verkäufe der Häuser), dazu jede Nacht die Gaunerstücke (mit ihrer Erfolgschance in dem Augenblick, in dem sie ausgeführt werden).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Wer das Haus des Spielers in der Messung führt.</summary>
enum Bezugsart
{
    /// <summary>Die KI wie ein KI-Haus, aber mit genau einem Betrieb (<see cref="KiProfil.MaxBetriebe"/> = 0): Personal, Ware, Meisterprüfung,
    /// Ausbau, Stand, Familie und Politik nach den Regeln der KI – nur keine Expansion.</summary>
    EinBetrieb,
    /// <summary>Die KI mit demselben Profil wie die KI-Häuser („Bezugsspieler“: gleiche Strategie, ohne Einrichtung und Prämien).</summary>
    WieKi,
    /// <summary>Niemand: Der Spieler entlässt am Start alle Arbeiter und tut nichts (zählt nicht als Haus, z. B. in der Messstadt).</summary>
    Geist,
}

/// <summary>Eine Messpartie. Ein record: Zwei gleiche Aufbauten sind dieselbe Partie (der Bericht spielt sie nur einmal).</summary>
/// <param name="Karte">Kennung der Karte in game/karten/ („greybox“, „probestadt“, „messstadt“).</param>
/// <param name="Rivalen">Wie viele KI-Häuser der Karte mitspielen (0 = alle).</param>
/// <param name="Rotation">Die Wohnhausplätze von Spieler und KI-Häusern um so viele Plätze weiterreichen (Startfairness).</param>
/// <param name="Einrichtung">Einrichtung, die der Spieler am Start für seine Werkstatt kauft (Kennungen mit Komma).</param>
/// <param name="WachhausBeimErsten">Das erste KI-Haus bekommt am Start zusätzlich ein Wachhaus mit vier Gesellen (wie die Probe in Wirtschaftsmodell §27).</param>
/// <param name="Schwierigkeit">Normal: Die KI-Häuser starten wie der Spieler; Schwer: mit Werkstatt und Umlandbetrieb (PR #177).</param>
sealed record Messaufbau(
    string Karte,
    int Startwert,
    int Jahre,
    string Beruf = "Schmied",
    Volk Volk = Volk.Mensch,
    Bezugsart Bezug = Bezugsart.EinBetrieb,
    KiCharakter BezugCharakter = KiCharakter.Ausgewogen,
    int Rivalen = 0,
    int Rotation = 0,
    string Einrichtung = "",
    bool WachhausBeimErsten = false,
    bool MarktMessen = false,
    Schwierigkeit Schwierigkeit = Schwierigkeit.Normal);

/// <summary>Ein Haus am Ende eines Spieljahres.</summary>
sealed class Jahreswert
{
    public double Rein, Bar, Gewinn, Umsatz, Schaden, Raub;
    /// <summary>Was außerhalb der Betriebe ins Vermögen ging (Vorjahr): Amt, Familie, Geschenke, Gericht, Gaunerei, Investitionen, Stand, Zinsen.</summary>
    public double Amt, Familie, Geschenke, Gericht, Gaunerei, Investitionen, Standesgebuehren, Zinsen;
    public int Stand, Stufe, Betriebe;
    public bool Aktiv;
    /// <summary>Was der Beruf ohne Ware im Jahr brachte (Beute, Sold, Zinsen, Kollekte, Ausschank …), nur beim Spieler.</summary>
    public Dictionary<string, double> Sonder = new();
}

/// <summary>Ein Haus über die ganze Partie.</summary>
sealed class Hausverlauf
{
    public string Name = "";
    public bool IstSpieler;
    public Volk Volk;
    public string Startberuf = "";
    public string? Wohnhausplatz;
    public readonly List<Jahreswert> Jahre = new();
    /// <summary>Das erste Jahr mit Reinvermögen ab der Schwelle des Bürgers (Ziel Z2) bzw. im Stand Bürger.</summary>
    public int? BuergerReinJahr, BuergerStandJahr, Stufe2Jahr, Stufe3Jahr, BankrottJahr, ErloschenJahr;
    public double StartGewinnErwartet, StartBar;
    public int StartArbeiter, StartBetriebe;

    public Jahreswert Im(int jahr) => Jahre[Math.Min(jahr, Jahre.Count) - 1];
}

/// <summary>Eine Ware im Markt eines Stichjahres.</summary>
sealed class Warenjahr
{
    public string Ware = "", Typ = "";
    public double Grundpreis, Preisfaktor, Nachfrage, Npc, VerkauftHaeuser;
    public readonly Dictionary<string, int> Hergestellt = new();
}

/// <summary>Gaunerstücke einer Art: wie viele ausgeführt wurden und die Summe ihrer Erfolgschancen.</summary>
sealed class Gaunerzaehler
{
    public int Anzahl, MitChance;
    public double Chancen;
}

sealed class Partieergebnis
{
    public required Messaufbau Aufbau;
    public readonly List<Hausverlauf> Haeuser = new();
    public readonly Dictionary<int, List<Warenjahr>> Markt = new();
    /// <summary>„Hinterzimmer: Diebstahl“, „Diebe: Einbruch“ … – je Art.</summary>
    public readonly SortedDictionary<string, Gaunerzaehler> Gauner = new(StringComparer.Ordinal);
    public int Kredite, Wucherkredite, JahreMitWache;
    public double Patrouilleschutz;
    public string? Fehler;

    public Hausverlauf Spieler => Haeuser[0];
    public IEnumerable<Hausverlauf> Ki => Haeuser.Skip(1);
}

/// <summary>Die Dateien, die jede Messpartie liest – einmal als Text geladen, jede Partie baut daraus ihre eigenen Objekte.</summary>
sealed class Messquellen
{
    public string Wirtschaft { get; }
    public string Katalog { get; }
    private readonly Dictionary<string, string> _karten = new();

    public Messquellen()
    {
        var pfad = Datenpfad.Wirtschaft();
        Wirtschaft = File.ReadAllText(pfad);
        Katalog = File.ReadAllText(Path.Combine(Path.GetDirectoryName(pfad)!, "katalog.json"));
    }

    public string Karte(string kennung)
    {
        lock (_karten)
        {
            if (!_karten.TryGetValue(kennung, out var text))
                _karten[kennung] = text = File.ReadAllText(Datenpfad.Karte(kennung));
            return text;
        }
    }
}

static class Messpartie
{
    /// <summary>
    /// Die Karte mit weitergereichten Wohnhausplätzen: Bei Rotation r wohnt der i-te (Spieler = 0, dann die KI-Häuser) dort, wo vorher der
    /// (i + r)-te wohnte. Werkstattplätze bleiben – ihre Lage wirkt im Kern nicht (Fahrzeit nach Ort, nicht nach Weg).
    /// </summary>
    public static string KarteRotiert(string json, int rotation, int haeuser)
    {
        if (rotation == 0) return json;
        var knoten = JsonNode.Parse(json)!.AsObject();
        var start = knoten["start"]!.AsObject();
        var stellen = new List<JsonObject> { start["spieler"]!.AsObject() };
        stellen.AddRange(start["haeuser"]!.AsArray().Take(haeuser).Select(h => h!.AsObject()));
        var plaetze = stellen.Select(s => (string?)s["wohnhaus"]).ToList();
        for (var i = 0; i < stellen.Count; i++)
            stellen[i]["wohnhaus"] = plaetze[(i + rotation) % stellen.Count];
        return knoten.ToJsonString();
    }

    public static Partieergebnis Spielen(Messaufbau a, Messquellen q)
    {
        var ergebnis = new Partieergebnis { Aufbau = a };
        var daten = Wirtschaftsdaten.AusJson(q.Wirtschaft);
        var kartenText = q.Karte(a.Karte);
        var anzahl = a.Rivalen > 0 ? a.Rivalen : (JsonNode.Parse(kartenText)!["start"]?["haeuser"]?.AsArray().Count ?? 1);
        var karte = Stadtkarte.AusJson(KarteRotiert(kartenText, a.Rotation, anzahl), Gebaeudekatalog.AusJson(q.Katalog));
        var (sim, spieler) = Partieaufbau.Erstellen(
            Partieeinstellungen.Standard with { Zufallsstartwert = a.Startwert, Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = a.Schwierigkeit },
            daten, new NeuePartie(Volk: a.Volk, Beruf: a.Beruf, Rivalen: anzahl), karte: karte);
        var w = sim.Wirtschaft!;

        if (a.WachhausBeimErsten && w.Konten.Count > 1)
        {
            var erstes = w.Konten[1];
            var wache = w.BetriebEroeffnen($"Wachhaus {erstes.Inhaber.Replace("Haus ", "")}", Betriebsart.Wachhaus, erstes);
            w.Einstellen(wache, "Geselle", daten.Plaetze(Betriebsart.Wachhaus));
        }
        var werkstatt = w.Betriebe.First(b => b.Konto == spieler);
        foreach (var kennung in a.Einrichtung.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var e = w.Ausfuehren(spieler, new EinrichtungKaufen(werkstatt.Name, kennung));
            if (!e.Erfolg) ergebnis.Fehler = $"Einrichtung {kennung}: {e.Meldung}";
        }
        switch (a.Bezug)
        {
            case Bezugsart.EinBetrieb:
                w.KiAnmelden(spieler, Partieaufbau.ProfilFuer(a.BezugCharakter) with { MaxBetriebe = 0 });
                break;
            case Bezugsart.WieKi:
                w.KiAnmelden(spieler, Partieaufbau.ProfilFuer(a.BezugCharakter));
                break;
            case Bezugsart.Geist:
                foreach (var kraft in werkstatt.Belegung.Keys.ToList())
                    w.Ausfuehren(spieler, new PersonalSetzen(werkstatt.Name, kraft, 0));
                break;
        }

        // Die Häuser in Konto-Reihenfolge (Spieler zuerst) mit dem, was sie am Start haben.
        foreach (var k in w.Konten)
        {
            var eigene = w.Betriebe.Where(b => b.Konto == k).ToList();
            ergebnis.Haeuser.Add(new Hausverlauf
            {
                Name = k.Inhaber, IstSpieler = k == spieler, Volk = w.Familien.Von(k)?.Volk ?? Volk.Mensch,
                Startberuf = eigene.FirstOrDefault()?.Artdaten.Beruf ?? "",
                Wohnhausplatz = w.Bauen.IstAktiv ? w.Bauen.Grundstuecke.FirstOrDefault(g => g.Besitzer == k && g.Art == Grundstuecksart.Wohnhaus)?.Kennung : null,
                StartBetriebe = eigene.Count, StartArbeiter = eigene.Sum(b => b.Belegt), StartBar = k.Vermoegen,
                StartGewinnErwartet = eigene.Sum(b => Kalkulation.ErwarteterJahresgewinn(w, b.Art, leistung: w.VolksFaktor(k, b.Artdaten.Beruf))),
            });
        }

        var stichjahre = a.MarktMessen ? Balancingbericht.Stichjahre(a.Jahre).ToHashSet() : new HashSet<int>();
        var krediteGesehen = new HashSet<int>();
        var buergerSchwelle = daten.Staende.Count > 1 ? daten.Staende[1].Schwelle : double.MaxValue;
        for (var j = 1; j <= a.Jahre; j++)
        {
            var jahr = sim.Zeit.Jahr;
            var marktJahr = stichjahre.Contains(j);
            var preise = new Dictionary<string, double>();
            var nachfrage = new Dictionary<string, double>();
            var verkauft = new Dictionary<string, double>();
            var stunden = 0;
            while (sim.Zeit.Jahr == jahr)
            {
                var minute = sim.Zeit.MinuteImTag;
                if (minute == Unterwelt.Nachtstunde) GaunerZaehlen(w, ergebnis);
                if (marktJahr && minute % 60 == 0 && minute > 0)
                {
                    stunden++;
                    foreach (var s in w.Markt.Staende)
                    {
                        preise[s.Ware] = preise.GetValueOrDefault(s.Ware) + s.Preisfaktor;
                        nachfrage[s.Ware] = nachfrage.GetValueOrDefault(s.Ware) + s.Nachfrage;
                    }
                }
                if (marktJahr && minute == Spielzeit.MinutenProTag - 1)
                    foreach (var s in w.Markt.Staende)
                        verkauft[s.Ware] = s.SpielerAngebotImJahr;
                sim.Tick();
            }

            foreach (var c in w.Bank.IstAktiv ? w.Bank.Kredite : Array.Empty<Reichsgilde.Core.Bank.Kredit>())
                if (krediteGesehen.Add(c.Nummer))
                {
                    ergebnis.Kredite++;
                    if (c.IstWucher) ergebnis.Wucherkredite++;
                }
            if (w.Wache.IstAktiv && w.Betriebe.Any(b => w.Wache.IstWachbetrieb(b) && b.Konto.IstAktiv && b.Belegt > 0))
            {
                ergebnis.JahreMitWache++;
                ergebnis.Patrouilleschutz += w.Wache.Patrouilleschutz;
            }

            for (var i = 0; i < w.Konten.Count && i < ergebnis.Haeuser.Count; i++)
            {
                var k = w.Konten[i];
                var h = ergebnis.Haeuser[i];
                var eigene = w.Betriebe.Where(b => b.Konto == k).ToList();
                var stufe = eigene.Where(b => b.InDerStadt).Select(b => b.Stufe).DefaultIfEmpty(0).Max();
                var wert = new Jahreswert
                {
                    Rein = k.Reinvermoegen, Bar = k.Vermoegen, Gewinn = k.Vorjahr?.Gewinn ?? 0, Umsatz = k.Vorjahr?.Umsatz ?? 0,
                    Schaden = k.Vorjahr?.Schaden ?? 0, Raub = k.Vorjahr?.Raubwert ?? 0,
                    Amt = k.Vorjahr?.Amtseinkommen ?? 0, Familie = k.Vorjahr?.Familie ?? 0, Geschenke = k.Vorjahr?.Geschenke ?? 0,
                    Gericht = k.Vorjahr?.Gericht ?? 0, Gaunerei = k.Vorjahr?.Gaunerei ?? 0, Investitionen = k.Vorjahr?.Investitionen ?? 0,
                    Standesgebuehren = k.Vorjahr?.Standesgebuehren ?? 0, Zinsen = k.Vorjahr?.Zinsen ?? 0,
                    Stand = k.Stand, Stufe = stufe, Betriebe = eigene.Count, Aktiv = k.IstAktiv,
                };
                if (h.IstSpieler) Sonderwerte(w, k, eigene, wert.Sonder);
                h.Jahre.Add(wert);
                if (k.Reinvermoegen >= buergerSchwelle) h.BuergerReinJahr ??= j;
                if (k.Stand >= 1) h.BuergerStandJahr ??= j;
                if (stufe >= 2) h.Stufe2Jahr ??= j;
                if (stufe >= 3) h.Stufe3Jahr ??= j;
                if (k.IstBankrott) h.BankrottJahr ??= j;
                if (k.IstErloschen) h.ErloschenJahr ??= j;
            }

            if (marktJahr && stunden > 0)
                ergebnis.Markt[j] = w.Markt.Staende.Select(s =>
                {
                    var wj = new Warenjahr
                    {
                        Ware = s.Ware, Typ = s.Typ, Grundpreis = s.Grundpreis, Preisfaktor = preise[s.Ware] / stunden,
                        Nachfrage = nachfrage[s.Ware] / stunden, Npc = s.Grundnachfrage * daten.Waren[s.Ware].NpcAnteil, // mit den Einwohnern (PR #177)
                        VerkauftHaeuser = verkauft.GetValueOrDefault(s.Ware),
                    };
                    foreach (var k in w.Konten)
                        if (k.Vorjahr?.Hergestellt.GetValueOrDefault(s.Ware) is > 0 and var stueck)
                            wj.Hergestellt[k.Inhaber] = stueck;
                    return wj;
                }).ToList();
        }
        return ergebnis;
    }

    /// <summary>Was der Beruf ohne Ware dem Spieler im Vorjahr brachte – aus den Büchern von Diebe, Wache, Bank, Kirche und Schänke.</summary>
    private static void Sonderwerte(Stadtwirtschaft w, Konto k, List<Betrieb> eigene, Dictionary<string, double> s)
    {
        if (w.Diebe.IstAktiv && w.Diebe.Vorjahr(k) is { } dieb) { s["Beute"] = dieb.Beute; s["Faenge"] = dieb.Faenge; }
        if (w.Wache.IstAktiv && w.Wache.Vorjahr(k) is { } wache)
        {
            s["Sold"] = wache.Sold; s["Kopfgeld"] = wache.Kopfgeld; s["Geleitgeld"] = wache.Geleitgeld; s["Razzien"] = wache.Razzien;
        }
        if (w.Bank.IstAktiv && w.Bank.Vorjahr(k) is { } bank)
        {
            s["Stadtzinsen"] = bank.Stadtzinsen; s["Wechsel"] = bank.Wechsel; s["Hauszinsen"] = bank.Hauszinsen; s["Ausfall"] = bank.Ausfall;
            s["Kasse"] = eigene.Where(w.Bank.IstBankbetrieb).Sum(w.Bank.Kasse);
        }
        if (w.Kirchenwesen.IstAktiv)
            foreach (var b in eigene.Where(w.Kirchenwesen.IstKirche))
                if (w.Kirchenwesen.Vorjahr(b) is { } buch)
                {
                    s["Kollekte"] = s.GetValueOrDefault("Kollekte") + buch.Kollekte;
                    s["Ablassgeld"] = s.GetValueOrDefault("Ablassgeld") + buch.Ablassgeld;
                }
        if (w.Gastwirtschaft.IstAktiv)
            foreach (var b in eigene.Where(b => b.IstSchaenke))
                if (w.Gastwirtschaft.Vorjahr(b) is { } gaeste)
                {
                    s["Ausschank"] = s.GetValueOrDefault("Ausschank") + gaeste.Einnahmen;
                    s["Gaeste"] = s.GetValueOrDefault("Gaeste") + gaeste.Bewirtet;
                }
    }

    /// <summary>
    /// Kurz vor der Nacht: die Aufträge im Hinterzimmer und an die eigenen Diebe – mit der Erfolgschance, mit der sie gleich gewürfelt werden.
    /// Überfälle (Hinterhalte) zählen ohne Chance: Sie hängt am Karren, der erst noch kommt.
    /// </summary>
    private static void GaunerZaehlen(Stadtwirtschaft w, Partieergebnis e)
    {
        void Zaehlen(string art, double? chance)
        {
            if (!e.Gauner.TryGetValue(art, out var z)) e.Gauner[art] = z = new Gaunerzaehler();
            z.Anzahl++;
            if (chance is { } c) { z.MitChance++; z.Chancen += c; }
        }
        Betrieb? BetriebNamens(string? name) => name is null ? null : w.Betriebe.FirstOrDefault(b => b.Name == name);
        if (w.Gauner.IstAktiv)
            foreach (var a in w.Gauner.Auftraege.Where(a => a.Auftraggeber.IstAktiv && a.Ziel.IstAktiv))
                Zaehlen($"Hinterzimmer: {a.Art.Name()}", a.Art switch
                {
                    Gaunerstueck.Diebstahl or Gaunerstueck.Sabotage when BetriebNamens(a.Betrieb) is { } b => w.Gauner.Erfolgschance(a.Art, b, a.Auftraggeber),
                    Gaunerstueck.Kroetenschleim => w.Gauner.Erfolgschance(a.Art, null, a.Auftraggeber),
                    Gaunerstueck.Hauseinbruch or Gaunerstueck.Brandstiftung => w.Gauner.ErfolgschanceWohnhaus(a.Art, a.Ziel, a.Auftraggeber),
                    _ => null,
                });
        if (w.Diebe.IstAktiv)
            foreach (var a in w.Diebe.Auftraege.Where(a => a.Dieb.IstAktiv && a.Ziel.IstAktiv))
                Zaehlen($"Diebe: {a.Art.Name()}", a.Art switch
                {
                    Diebesstueck.Einbruch when BetriebNamens(a.Betrieb) is { } b => w.Diebe.Einbruchchance(b, a.Dieb),
                    Diebesstueck.Auskundschaften => w.Diebe.Auskundschaftchance(a.Dieb, a.Ziel),
                    Diebesstueck.Entfuehrung => w.Diebe.Entfuehrungschance(a.Dieb),
                    Diebesstueck.Brandstiftung when w.Diebe.Brandziel(a.Ziel) is { } b => w.Diebe.Brandchance(b, a.Dieb),
                    _ => null,
                });
    }
}
