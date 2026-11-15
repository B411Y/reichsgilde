using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Tests: Erntejahr und Teuerung mit der Partie-Einstellung „Härte der Zeit“ („Härte um 1400“ PR 1, Abschnitt „ernte“, Datenversion 91) – Güte je
/// Herbstjahr, Ansage im Sommerjahr davor, Schonjahr nach einer Missernte, Grenzen, Preiswirkung auf die Getreidewaren, Meldungen, Spielstand;
/// ohne Abschnitt bleibt alles wie bis Datenversion 89.
/// </summary>
static class ErnteTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json.Value));
    static readonly Lazy<Wirtschaftsdaten> OhneErnte = new(() => Wirtschaftsdaten.AusJson(OhneAbschnitt(Json.Value, "ernte")));

    static string OhneAbschnitt(string json, string abschnitt)
    {
        var knoten = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        knoten.Remove(abschnitt);
        return knoten.ToJsonString();
    }

    /// <summary>Die Minute seit Spielbeginn: Jahr ab 0 (= 1400), Uhrzeit.</summary>
    static long Minute(int jahr, int stunde, int minute = 0) => jahr * (long)Spielzeit.MinutenProTag + stunde * 60 + minute - Spielzeit.TagesbeginnMinute;

    static Stadtwirtschaft Wirtschaft(int startwert, HaerteDerZeit haerte, Wirtschaftsdaten? daten = null) =>
        new(daten ?? Daten.Value, Partieeinstellungen.Standard.StartStatut, startwert) { HaerteDerZeit = haerte };

    static Simulation Sim(long minute, int startwert, HaerteDerZeit haerte = HaerteDerZeit.Ueberliefert, Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert, HaerteDerZeit = haerte },
            new Spielzeit(minute), daten ?? Daten.Value);

    /// <summary>Der erste Startwert, dessen Ernte 1402 diese Güte hat.</summary>
    static int StartwertMit(Erntegute guete, HaerteDerZeit haerte = HaerteDerZeit.Ueberliefert)
    {
        for (var s = 1; s < 2000; s++)
            if (Wirtschaft(s, haerte).Ernte.ErnteVon(1402)!.Guete == guete) return s;
        throw new InvalidOperationException($"Kein Startwert mit {guete} 1402 gefunden.");
    }

    /// <summary>Die Güten der ersten n Ernten je Startwert.</summary>
    static List<Erntegute[]> Ernten(HaerteDerZeit haerte, int startwerte, int ernten = 50) =>
        Enumerable.Range(1, startwerte).Select(s =>
        {
            var w = Wirtschaft(s * 7919, haerte);
            return Enumerable.Range(0, ernten).Select(i => w.Ernte.ErnteVon(1402 + 4 * i)!.Guete).ToArray();
        }).ToList();

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Ernte: Die Ernte kommt im Herbstjahr und gilt vier Jahre bis zur nächsten; vorher (1400, 1401) keine", () =>
        {
            var e = Wirtschaft(1400, HaerteDerZeit.Ueberliefert).Ernte;
            Pruefe.Wahr(e.IstAktiv, "Abschnitt „ernte“ in den Spieldaten");
            Pruefe.Gleich(1402, Erntewesen.ErstesErntejahr);
            Pruefe.Gleich(null, e.GiltIm(1400));
            Pruefe.Gleich(null, e.GiltIm(1401));
            for (var j = 1402; j <= 1405; j++) Pruefe.Gleich(e.ErnteVon(1402), e.GiltIm(j));
            Pruefe.Gleich(e.ErnteVon(1406), e.GiltIm(1406));
            Pruefe.Gleich(1405, e.ErnteVon(1402)!.LetztesJahr);
            Pruefe.Gleich(null, e.ErnteVon(1403)); // kein Herbstjahr
        }),

        ("Ernte: Ansage ein Jahr vorher – nur im Sommerjahr, und sie trifft ein", () =>
        {
            var e = Wirtschaft(1400, HaerteDerZeit.Ueberliefert).Ernte;
            Pruefe.Gleich(null, e.AngesagtIm(1400));
            Pruefe.Gleich(e.ErnteVon(1402), e.AngesagtIm(1401));
            Pruefe.Gleich(null, e.AngesagtIm(1402));
            Pruefe.Gleich(null, e.AngesagtIm(1403));
            Pruefe.Gleich(null, e.AngesagtIm(1404));
            Pruefe.Gleich(e.ErnteVon(1406), e.AngesagtIm(1405));
        }),

        ("Ernte: gleicher Startwert, gleiche Ernten – auch wenn man eine späte Ernte zuerst fragt; anderer Startwert, andere Ernten", () =>
        {
            var a = Wirtschaft(4711, HaerteDerZeit.Hart).Ernte;
            var b = Wirtschaft(4711, HaerteDerZeit.Hart).Ernte;
            var spaet = b.ErnteVon(1402 + 4 * 40); // springt vor: die Ernten davor werden nachgerechnet
            var folgeA = Enumerable.Range(0, 41).Select(i => a.ErnteVon(1402 + 4 * i)!.Guete).ToArray();
            var folgeB = Enumerable.Range(0, 41).Select(i => b.ErnteVon(1402 + 4 * i)!.Guete).ToArray();
            Pruefe.Gleich(string.Join(",", folgeA), string.Join(",", folgeB));
            Pruefe.Gleich(folgeA[40], spaet!.Guete);
            var c = Wirtschaft(4712, HaerteDerZeit.Hart).Ernte;
            var folgeC = Enumerable.Range(0, 41).Select(i => c.ErnteVon(1402 + 4 * i)!.Guete).ToArray();
            Pruefe.Wahr(string.Join(",", folgeA) != string.Join(",", folgeC), "andere Partie, andere Ernten");
        }),

        ("Ernte: Nach einer Missernte folgt ein Schonjahr (nie mager, nie Missernte) – Gegenprobe: in „Harte Jahre“ gibt es Missernten", () =>
        {
            var missernten = 0;
            foreach (var folge in Ernten(HaerteDerZeit.Hart, 60))
                for (var i = 0; i < folge.Length; i++)
                {
                    if (folge[i] != Erntegute.Missernte) continue;
                    missernten++;
                    if (i + 1 < folge.Length)
                        Pruefe.Wahr(folge[i + 1] is Erntegute.Gut or Erntegute.Gewoehnlich, $"Nach einer Missernte kam {folge[i + 1]}");
                }
            Pruefe.Wahr(missernten > 100, $"In 3000 harten Ernten nur {missernten} Missernten");
        }),

        ("Härte der Zeit: Milde Jahre ohne Missernte, Harte Jahre mit mehr schlechten Ernten als Wie überliefert", () =>
        {
            double Schlecht(HaerteDerZeit h) => Ernten(h, 60).SelectMany(f => f).Count(g => g is Erntegute.Mager or Erntegute.Missernte) / 3000.0;
            Pruefe.Wahr(Ernten(HaerteDerZeit.Mild, 60).SelectMany(f => f).All(g => g != Erntegute.Missernte), "Milde Jahre ohne Missernte");
            var mild = Schlecht(HaerteDerZeit.Mild);
            var ueberliefert = Schlecht(HaerteDerZeit.Ueberliefert);
            var hart = Schlecht(HaerteDerZeit.Hart);
            Pruefe.Wahr(mild < ueberliefert && ueberliefert < hart, $"schlechte Ernten mild {mild:0.00}, überliefert {ueberliefert:0.00}, hart {hart:0.00}");
            Pruefe.Ungefaehr(0.15, mild, 0.04);
            Pruefe.Wahr(hart is > 0.35 and < 0.55, $"hart {hart:0.00}");
        }),

        ("Ernte: Faktor je Ware nach Güte, Stärke und Gewicht, mit Grenzen; andere Waren bleiben 1", () =>
        {
            var u = Wirtschaft(1, HaerteDerZeit.Ueberliefert).Ernte;
            Pruefe.Ungefaehr(0.5, u.Faktor("Gerste", Erntegute.Missernte), 1e-12);
            Pruefe.Ungefaehr(0.75, u.Faktor("Körnermahlzeit", Erntegute.Missernte), 1e-12); // halbes Gewicht
            Pruefe.Ungefaehr(1.25, u.Faktor("Roggen", Erntegute.Gut), 1e-12);
            Pruefe.Gleich(1.0, u.Faktor("Gerste", Erntegute.Gewoehnlich));
            Pruefe.Gleich(1.0, u.Faktor("Eisen", Erntegute.Missernte));
            var m = Wirtschaft(1, HaerteDerZeit.Mild).Ernte;
            Pruefe.Ungefaehr(0.875, m.Faktor("Gerste", Erntegute.Mager), 1e-12);
            var h = Wirtschaft(1, HaerteDerZeit.Hart).Ernte;
            Pruefe.Ungefaehr(0.375, h.Faktor("Gerste", Erntegute.Missernte), 1e-12);
            // Grenze: eine Stärke, die unter die untere Grenze führen würde, wird gekappt
            var stark = Wirtschaftsdaten.AusJson(Json.Value.Replace("\"staerke\": 1.25", "\"staerke\": 2"));
            Pruefe.Ungefaehr(0.35, Wirtschaft(1, HaerteDerZeit.Hart, stark).Ernte.Faktor("Gerste", Erntegute.Missernte), 1e-12);
            Pruefe.Ungefaehr(1.5, Wirtschaft(1, HaerteDerZeit.Hart, stark).Ernte.Faktor("Gerste", Erntegute.Gut), 1e-12);
        }),

        ("Ernte: Nach einer Missernte sind Gerste und Bier bis zur nächsten Ernte teurer – wer im Sommer davor einlagert, verkauft besser", () =>
        {
            var s = StartwertMit(Erntegute.Missernte);
            var sommer = Sim(Minute(1, 10), s); // Sommer 1401: angesagt, aber noch der alte Preis
            sommer.Tick();
            var ws = sommer.Wirtschaft!;
            Pruefe.Gleich(Erntegute.Missernte, ws.Ernte.Angesagt!.Guete);
            Pruefe.Gleich(1.0, ws.Markt["Gerste"].Erntefaktor);
            var winter = Sim(Minute(3, 10), s); // Winter 1403: die Missernte von 1402 gilt noch (ohne den Erntezuschlag des Herbstes)
            winter.Tick();
            var ww = winter.Wirtschaft!;
            Pruefe.Gleich(Erntegute.Missernte, ww.Ernte.Jetzt!.Guete);
            Pruefe.Ungefaehr(0.5, ww.Markt["Gerste"].Erntefaktor, 1e-12);
            Pruefe.Wahr(ww.Markt["Gerste"].Preis > ws.Markt["Gerste"].Preis * 1.3, $"Gerste {ws.Markt["Gerste"].Preis:0.0} → {ww.Markt["Gerste"].Preis:0.0}");
            Pruefe.Wahr(ww.Markt["Dünnbier"].Preis > ws.Markt["Dünnbier"].Preis, "Dünnbier teurer");
            Pruefe.Gleich(ws.Markt["Eisen"].Preis, ww.Markt["Eisen"].Preis); // andere Waren nicht
            // Gegenprobe: dieselbe Partie ohne Abschnitt – kein Unterschied am Gerstenpreis
            var ohne = Sim(Minute(3, 10), s, daten: OhneErnte.Value);
            ohne.Tick();
            Pruefe.Gleich(1.0, ohne.Wirtschaft!.Markt["Gerste"].Erntefaktor);
            Pruefe.Gleich(ws.Markt["Gerste"].Preis, ohne.Wirtschaft!.Markt["Gerste"].Preis);
        }),

        ("Ernte: Eine gute Ernte macht Gerste billiger", () =>
        {
            var s = StartwertMit(Erntegute.Gut);
            var a = Sim(Minute(1, 10), s); a.Tick();
            var b = Sim(Minute(3, 10), s); b.Tick();
            Pruefe.Wahr(b.Wirtschaft!.Markt["Gerste"].Preis < a.Wirtschaft!.Markt["Gerste"].Preis, "Gerste billiger");
        }),

        ("Ernte: Meldung der Ansage zu Beginn des Sommerjahres und der Ernte zu Beginn des Herbstjahres", () =>
        {
            var s = StartwertMit(Erntegute.Mager);
            var sim = Sim(Minute(1, 4), s);
            var meldungen = new List<string>();
            sim.Wirtschaft!.Meldung += meldungen.Add;
            sim.Tick();
            Pruefe.Wahr(meldungen.Any(m => m.Contains("magere Ernte") && m.Contains("1402") && m.Contains("Gerste")), string.Join(" | ", meldungen));
            meldungen.Clear();
            while (sim.Zeit.Jahr < 1402) sim.Tick();
            sim.Tick();
            Pruefe.Wahr(meldungen.Any(m => m.StartsWith("Magere Ernte 1402")), string.Join(" | ", meldungen));
        }),

        ("Ernte: ohne Abschnitt (alte Daten) keine Erntejahre, kein Faktor, keine Meldung", () =>
        {
            var sim = Sim(Minute(1, 4), 1400, HaerteDerZeit.Hart, OhneErnte.Value);
            var meldungen = new List<string>();
            sim.Wirtschaft!.Meldung += meldungen.Add;
            sim.Tick();
            var e = sim.Wirtschaft.Ernte;
            Pruefe.Wahr(!e.IstAktiv, "ohne Abschnitt");
            Pruefe.Gleich(null, e.Angesagt);
            Pruefe.Gleich(null, e.GiltIm(1406));
            Pruefe.Gleich(1.0, e.Faktor("Gerste"));
            Pruefe.Wahr(!meldungen.Any(m => m.Contains("Ernte 14")), string.Join(" | ", meldungen));
        }),

        ("Ernte: Fehlerhafte Daten werden abgewiesen", () =>
        {
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"Körnermahlzeit\": 0.5,", "\"Hirse\": 0.5,")), "ernte.waren");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"chanceMissernte\": 0.2", "\"chanceMissernte\": 0.7")), "ernte.hart");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"missernte\": 0.5,", "\"missernte\": 0.9,")), "ernte.missernte");
        }),

        ("Härte der Zeit: steht im Spielstand und bleibt nach dem Laden; ein alter Spielstand ohne Feld lädt mit dem Standard", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(
                Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, HaerteDerZeit = HaerteDerZeit.Hart }, Daten.Value, new NeuePartie());
            Pruefe.Gleich(HaerteDerZeit.Hart, sim.Wirtschaft!.HaerteDerZeit);
            var zeit = new DateTime(2026, 11, 15, 12, 0, 0);
            var json = Speicherung.Speichern(sim, spieler, zeit);
            Pruefe.Wahr(json.Contains("\"HaerteDerZeit\": \"Hart\"") || json.Contains("\"haerteDerZeit\": \"Hart\""), "Feld im Spielstand");
            var (geladen, _) = Speicherung.Laden(json);
            Pruefe.Gleich(HaerteDerZeit.Hart, geladen.Einstellungen.HaerteDerZeit);
            Pruefe.Gleich(HaerteDerZeit.Hart, geladen.Wirtschaft!.HaerteDerZeit);
            Pruefe.Gleich(sim.Wirtschaft.Ernte.ErnteVon(1430), geladen.Wirtschaft.Ernte.ErnteVon(1430));
            var alt = json.Replace("\"HaerteDerZeit\": \"Hart\",", "").Replace("\"haerteDerZeit\": \"Hart\",", "");
            Pruefe.Wahr(alt != json, "Feld entfernt");
            var (altGeladen, _) = Speicherung.Laden(alt);
            Pruefe.Gleich(HaerteDerZeit.Ueberliefert, altGeladen.Einstellungen.HaerteDerZeit);
            // Der Standard wird nicht geschrieben: Spielstände mit „Wie überliefert“ bleiben bitgleich wie vor dem Feld
            var (standard, standardSpieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, Daten.Value, new NeuePartie());
            Pruefe.Wahr(!Speicherung.Speichern(standard, standardSpieler, zeit).Contains("aerteDerZeit"), "Standard nicht im Spielstand");
        }),
    };
}
