using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Tests: Lagen aller Rohstoffquellen (Datenversion 92, Abschnitt „lagen“): Die Ernte ist die erste Quelle und rechnet bitgleich wie in #475;
/// sechs weitere Quellen mit gleichen Stufen, Chancen und Grenzen, eigener Jahreszeit, Ansage, Schonjahr; Faktor auf die Waren (Folgewaren
/// 0,25), Ertrag der eigenen Umlandbetriebe halb; Meldungen; Spielstand; ohne Abschnitt wie Datenversion 91.
/// </summary>
static class LagenTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json.Value));
    static readonly Lazy<Wirtschaftsdaten> OhneLagen = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneLagen(Json.Value)));

    static long Minute(int jahr, int stunde, int minute = 0) => jahr * (long)Spielzeit.MinutenProTag + stunde * 60 + minute - Spielzeit.TagesbeginnMinute;

    static Stadtwirtschaft Wirtschaft(int startwert, HaerteDerZeit haerte = HaerteDerZeit.Ueberliefert, Wirtschaftsdaten? daten = null) =>
        new(daten ?? Daten.Value, Partieeinstellungen.Standard.StartStatut, startwert) { HaerteDerZeit = haerte };

    static Simulation Sim(long minute, int startwert, Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert },
            new Spielzeit(minute), daten ?? Daten.Value);

    /// <summary>Der erste Startwert, dessen Lage der Quelle im ersten Stichjahr diese Güte hat.</summary>
    static int StartwertMit(Betriebsart quelle, Lagegute guete)
    {
        for (var s = 1; s < 3000; s++)
        {
            var q = Wirtschaft(s).Lagen.QuelleVon(quelle)!;
            if (q.LageVon(q.ErstesStichjahr)!.Guete == guete) return s;
        }
        throw new InvalidOperationException($"Kein Startwert mit {guete} für {quelle} gefunden.");
    }

    static string Folge(Rohstofflage q, int n = 40) =>
        string.Concat(Enumerable.Range(0, n).Select(i => (int)q.LageVon(q.ErstesStichjahr + 4 * i)!.Guete));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Lagen: Jeder Umlandbetrieb mit Rohwaren hat genau eine Quelle (die Ernte zuerst), alle mit denselben Stufen – Wachturm keine", () =>
        {
            var w = Wirtschaft(1);
            Pruefe.Gleich(92, Daten.Value.Version);
            Pruefe.Gleich(7, w.Lagen.Quellen.Count);
            Pruefe.Gleich("Ernte", w.Lagen.Quellen[0].Kennung);
            Pruefe.Gleich(Betriebsart.Bauernhof, w.Lagen.Quellen[0].Betrieb);
            foreach (var (art, d) in Daten.Value.Betriebsarten.Where(p => p.Value.Ort == Arbeitsort.Umland))
            {
                var anzahl = w.Lagen.Quellen.Count(q => q.Betrieb == art);
                Pruefe.Gleich(d.Rohstoffe.Count > 0 ? 1 : 0, anzahl, $"{art}");
                if (anzahl == 0) continue;
                var q = w.Lagen.QuelleVon(art)!;
                Pruefe.Wahr(ReferenceEquals(Daten.Value.Ernte, q.Werte), $"{art}: Stufen und Grenzen aus „ernte“");
                foreach (var roh in d.Rohstoffe) Pruefe.Wahr(q.Waren.ContainsKey(roh), $"{art}: {roh} betroffen");
            }
            Pruefe.Gleich(null, w.Lagen.QuelleVon(Betriebsart.Wachturm));
        }),

        ("Lagen: Die Ernte würfelt bitgleich wie #475 (Folgen von main d798a9cc), mit und ohne Abschnitt „lagen“", () =>
        {
            var gold = new (int, HaerteDerZeit, string)[]
            {
                (1, HaerteDerZeit.Ueberliefert, "1211011131131201022110311021121111201122"),
                (4711, HaerteDerZeit.Hart, "1121131313120230313103111130231311112113"),
                (2026, HaerteDerZeit.Mild, "1121211110111122000111121110111202100212"),
                (99, HaerteDerZeit.Ueberliefert, "1202111210000212312012121011111112101112"),
            };
            foreach (var (s, h, folge) in gold)
            foreach (var daten in new[] { Daten.Value, OhneLagen.Value })
            {
                var e = Wirtschaft(s, h, daten).Ernte;
                Pruefe.Gleich(folge, string.Concat(Enumerable.Range(0, 40).Select(i => (int)e.ErnteVon(1402 + 4 * i)!.Guete)), $"Startwert {s}");
            }
        }),

        ("Lagen: Stichjahr nach Jahreszeit (Frühling 1404, Sommer 1401, Herbst 1402, Winter 1403), gilt vier Jahre, Ansage im Jahr davor", () =>
        {
            var w = Wirtschaft(7);
            foreach (var (art, erstes) in new[] { (Betriebsart.Mine, 1404), (Betriebsart.Kraeutergarten, 1401), (Betriebsart.Steinbruch, 1402),
                         (Betriebsart.Holzfaellerei, 1403), (Betriebsart.Klosterhof, 1404), (Betriebsart.Duftgarten, 1401) })
            {
                var q = w.Lagen.QuelleVon(art)!;
                Pruefe.Gleich(erstes, q.ErstesStichjahr, $"{art}");
                Pruefe.Gleich(null, q.GiltIm(erstes - 1));
                for (var j = erstes; j < erstes + 4; j++) Pruefe.Gleich(q.LageVon(erstes), q.GiltIm(j));
                Pruefe.Gleich(q.LageVon(erstes + 4), q.GiltIm(erstes + 4));
                Pruefe.Gleich(null, q.LageVon(erstes + 1));
                Pruefe.Gleich(q.LageVon(erstes), q.AngesagtIm(erstes - 1));
                Pruefe.Gleich(null, q.AngesagtIm(erstes));
                Pruefe.Gleich(q.LageVon(erstes + 4), q.AngesagtIm(erstes + 3));
            }
        }),

        ("Lagen: gleicher Startwert, gleiche Lagen (auch beim Vorspringen); jede Quelle würfelt anders; Schonjahr nach „schwer“", () =>
        {
            var a = Wirtschaft(4711, HaerteDerZeit.Hart);
            var b = Wirtschaft(4711, HaerteDerZeit.Hart);
            foreach (var q in b.Lagen.Quellen) q.LageVon(q.ErstesStichjahr + 4 * 39);
            var folgen = a.Lagen.Quellen.Select(q => Folge(q)).ToList();
            Pruefe.Gleich(string.Join("|", folgen), string.Join("|", b.Lagen.Quellen.Select(q => Folge(q))));
            Pruefe.Gleich(folgen.Count, folgen.Distinct().Count(), "jede Quelle eigene Würfe");
            var schwer = 0;
            for (var s = 1; s <= 40; s++)
                foreach (var q in Wirtschaft(s * 7919, HaerteDerZeit.Hart).Lagen.Quellen)
                {
                    var f = Folge(q, 50);
                    for (var i = 0; i < f.Length - 1; i++)
                        if (f[i] == '3')
                        {
                            schwer++;
                            Pruefe.Wahr(f[i + 1] is '0' or '1', $"{q.Kennung}: nach „schwer“ kam {f[i + 1]}");
                        }
                }
            Pruefe.Wahr(schwer > 300, $"nur {schwer} schwere Lagen");
        }),

        ("Lagen: Jede Quelle ist etwa gleich oft schlecht oder schwer (Wie überliefert, 60 × 50 Lagen je Quelle)", () =>
        {
            var anteile = new Dictionary<string, double>();
            var welten = Enumerable.Range(1, 60).Select(s => Wirtschaft(s * 104729)).ToList();
            for (var i = 0; i < welten[0].Lagen.Quellen.Count; i++)
            {
                var schlecht = welten.Sum(w => Folge(w.Lagen.Quellen[i], 50).Count(c => c is '2' or '3'));
                anteile[welten[0].Lagen.Quellen[i].Kennung] = schlecht / 3000.0;
            }
            foreach (var (k, a) in anteile) Pruefe.Wahr(a is > 0.27 and < 0.38, $"{k}: {a:0.000}");
        }),

        ("Lagen: Faktor je Ware – Rohwaren voll, Folgewaren 0,25, eigene Waren eines Ereignisses; andere Waren 1", () =>
        {
            var g = Wirtschaft(1).Lagen.QuelleVon(Betriebsart.Mine)!;
            Pruefe.Ungefaehr(0.5, g.Faktor("Eisen", Lagegute.Schwer), 1e-12);
            Pruefe.Ungefaehr(0.75, g.Faktor("Silber", Lagegute.Schlecht), 1e-12);
            Pruefe.Ungefaehr(0.875, g.Faktor("Beschlag", Lagegute.Schwer), 1e-12);
            Pruefe.Ungefaehr(1.5, g.Faktor("Silber", Lagegute.Gut), 1e-12); // reiche Ader: Silber voll
            Pruefe.Ungefaehr(1.25, g.Faktor("Eisen", Lagegute.Gut), 1e-12); // Eisen halb
            Pruefe.Gleich(1.0, g.Faktor("Eisen", Lagegute.Gewoehnlich));
            Pruefe.Gleich(1.0, g.Faktor("Holz", Lagegute.Schwer));
            Pruefe.Gleich(1.0, g.Faktor("Gerste", Lagegute.Schwer));
            var k = Wirtschaft(1).Lagen.QuelleVon(Betriebsart.Kraeutergarten)!;
            Pruefe.Ungefaehr(0.875, k.Faktor("Kröte", Lagegute.Schlecht), 1e-12); // Dürre: Kröte halb
            Pruefe.Ungefaehr(0.375, Wirtschaft(1, HaerteDerZeit.Hart).Lagen.QuelleVon(Betriebsart.Mine)!.Faktor("Eisen", Lagegute.Schwer), 1e-12);
            Pruefe.Ungefaehr(0.875, Wirtschaft(1, HaerteDerZeit.Mild).Lagen.QuelleVon(Betriebsart.Mine)!.Faktor("Eisen", Lagegute.Schlecht), 1e-12);
        }),

        ("Lagen: Treffen zwei Quellen eine Ware, gilt das Produkt, begrenzt auf die Grenzen der Ernte", () =>
        {
            // Prüfdaten: der Klosterhof trifft auch Holz
            var json = Json.Value.Replace("\"waren\": { \"Wachs\": 1, \"Lumpen\": 0.5,", "\"waren\": { \"Wachs\": 1, \"Lumpen\": 0.5, \"Holz\": 1,");
            Pruefe.Wahr(json != Json.Value, "Prüfdaten");
            var daten = Wirtschaftsdaten.AusJson(json);
            var beide = 0;
            foreach (var h in new[] { HaerteDerZeit.Ueberliefert, HaerteDerZeit.Hart })
            for (var s = 1; s <= 30; s++)
            {
                var w = Wirtschaft(s, h, daten);
                var holz = w.Lagen.QuelleVon(Betriebsart.Holzfaellerei)!;
                var kloster = w.Lagen.QuelleVon(Betriebsart.Klosterhof)!;
                for (var j = 1404; j < 1440; j++)
                {
                    w.Uhr = new Spielzeit(Minute(j - Spielzeit.StartJahr, 10));
                    var erwartet = Math.Clamp(holz.Faktor("Holz") * kloster.Faktor("Holz"), 0.35, 1.5);
                    Pruefe.Ungefaehr(erwartet, w.Lagen.Faktor("Holz"), 1e-12);
                    if (holz.Faktor("Holz") != 1 && kloster.Faktor("Holz") != 1) beide++;
                    Pruefe.Gleich(w.Lagen.Gruende("Holz").Count(), (holz.Faktor("Holz") != 1 ? 1 : 0) + (kloster.Faktor("Holz") != 1 ? 1 : 0));
                }
            }
            Pruefe.Wahr(beide > 50, $"nur {beide} Jahre mit zwei Lagen auf Holz");
        }),

        ("Lagen: Der eigene Umlandbetrieb ist halb getroffen (schwer ×0,75, gut ×1,25); Werkstätten und ohne „lagen“ nicht", () =>
        {
            var s = StartwertMit(Betriebsart.Mine, Lagegute.Schwer);
            var w = Wirtschaft(s);
            w.Uhr = new Spielzeit(Minute(4, 10)); // Frühling 1404
            Pruefe.Ungefaehr(0.75, w.Lagen.Ertragsfaktor(Betriebsart.Mine), 1e-12);
            Pruefe.Gleich(1.0, w.Lagen.Ertragsfaktor(Betriebsart.Giesserei));
            Pruefe.Gleich(1.0, w.Lagen.Ertragsfaktor(Betriebsart.Wachturm));
            w.Uhr = new Spielzeit(Minute(3, 10)); // 1403: angesagt, wirkt noch nicht
            Pruefe.Gleich(1.0, w.Lagen.Ertragsfaktor(Betriebsart.Mine));
            var gut = Wirtschaft(StartwertMit(Betriebsart.Mine, Lagegute.Gut));
            gut.Uhr = new Spielzeit(Minute(4, 10));
            Pruefe.Ungefaehr(1.25, gut.Lagen.Ertragsfaktor(Betriebsart.Mine), 1e-12);
            // Die Ernte trifft den Bauernhof nur mit Abschnitt „lagen“
            var m = 1;
            while (Wirtschaft(m).Ernte.ErnteVon(1402)!.Guete != Erntegute.Missernte) m++;
            var mit = Wirtschaft(m); mit.Uhr = new Spielzeit(Minute(2, 10));
            var ohne = Wirtschaft(m, daten: OhneLagen.Value); ohne.Uhr = new Spielzeit(Minute(2, 10));
            Pruefe.Ungefaehr(0.75, mit.Lagen.Ertragsfaktor(Betriebsart.Bauernhof), 1e-12);
            Pruefe.Gleich(1.0, ohne.Lagen.Ertragsfaktor(Betriebsart.Bauernhof));
        }),

        ("Lagen: In der Partie – schwere Lage der Grube: Eisen knapp und teurer, Leistung einer Grube ×0,75; Gerste bleibt", () =>
        {
            var s = StartwertMit(Betriebsart.Mine, Lagegute.Schwer);
            var mit = Sim(Minute(4, 10), s); mit.Tick();
            var ohne = Sim(Minute(4, 10), s, OhneLagen.Value); ohne.Tick();
            var wm = mit.Wirtschaft!; var wo = ohne.Wirtschaft!;
            Pruefe.Ungefaehr(0.5, wm.Markt["Eisen"].Lagefaktor, 1e-12);
            Pruefe.Wahr(wm.Markt["Eisen"].Preis > wo.Markt["Eisen"].Preis * 1.3, $"Eisen {wo.Markt["Eisen"].Preis:0.0} → {wm.Markt["Eisen"].Preis:0.0}");
            Pruefe.Wahr(wm.Markt["Beschlag"].Preis > wo.Markt["Beschlag"].Preis, "Beschlag teurer");
            Pruefe.Gleich(wo.Markt["Gerste"].Preis, wm.Markt["Gerste"].Preis);
            // Leistung einer Grube (Uhr auf Frühling 1404): mit Lage drei Viertel, eine Gießerei bleibt
            (Stadtwirtschaft W, Betrieb Grube, Betrieb Giesserei) MitGrube(Wirtschaftsdaten d)
            {
                var w = Wirtschaft(s, daten: d);
                var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: 5_000);
                var giesserei = w.BetriebEroeffnen("Gießerei Fuchs", Betriebsart.Giesserei, k);
                var grube = w.BetriebEroeffnen("Grube Fuchs", Betriebsart.Mine, k);
                w.Einstellen(giesserei, "Geselle", 2);
                w.Einstellen(grube, "Ork", 3);
                w.Uhr = new Spielzeit(Minute(4, 10));
                return (w, grube, giesserei);
            }
            var pm = MitGrube(Daten.Value);
            var po = MitGrube(OhneLagen.Value);
            Pruefe.Wahr(Kalkulation.Leistung(po.W, po.Grube) > 0, "Grube arbeitet");
            Pruefe.Ungefaehr(0.75 * Kalkulation.Leistung(po.W, po.Grube), Kalkulation.Leistung(pm.W, pm.Grube), 1e-9);
            Pruefe.Ungefaehr(Kalkulation.Leistung(po.W, po.Giesserei), Kalkulation.Leistung(pm.W, pm.Giesserei), 1e-12);
        }),

        ("Lagen: Meldung der Ansage im Jahr davor und zum Beginn – nicht bei „gewöhnlich“", () =>
        {
            var s = StartwertMit(Betriebsart.Mine, Lagegute.Schlecht);
            var sim = Sim(Minute(3, 4), s);
            var meldungen = new List<string>();
            sim.Wirtschaft!.Meldung += meldungen.Add;
            sim.Tick();
            Pruefe.Wahr(meldungen.Any(m => m.Contains("Wassereinbruch in den Gruben") && m.Contains("Frühling 1404") && m.Contains("Eisen und Silber werden teurer")),
                string.Join(" | ", meldungen));
            meldungen.Clear();
            while (sim.Zeit.Jahr < 1404) sim.Tick();
            sim.Tick();
            Pruefe.Wahr(meldungen.Any(m => m.StartsWith("Wassereinbruch 1404: Bis 1407 sind Eisen und Silber knapp")), string.Join(" | ", meldungen));
            // Holz allein: „wird“ statt „werden“
            var h = Wirtschaft(StartwertMit(Betriebsart.Holzfaellerei, Lagegute.Schwer)).Lagen.QuelleVon(Betriebsart.Holzfaellerei)!;
            Pruefe.Wahr(Lagenwesen.Ansage(h.LageVon(1403)!).Contains("Holz wird deutlich teurer"), Lagenwesen.Ansage(h.LageVon(1403)!));
            // Gewöhnlich: keine Meldung der Grube
            var g = Sim(Minute(3, 4), StartwertMit(Betriebsart.Mine, Lagegute.Gewoehnlich));
            var leise = new List<string>();
            g.Wirtschaft!.Meldung += leise.Add;
            g.Tick();
            Pruefe.Wahr(!leise.Any(m => m.Contains("1404") && (m.Contains("Eisen") || m.Contains("Silber"))), string.Join(" | ", leise));
        }),

        ("Lagen: ohne Abschnitt nur die Ernte (wie Datenversion 91), ohne „ernte“ gar keine Quelle", () =>
        {
            var w = Wirtschaft(5, daten: OhneLagen.Value);
            Pruefe.Gleich(1, w.Lagen.Quellen.Count);
            Pruefe.Gleich(null, w.Lagen.QuelleVon(Betriebsart.Mine));
            w.Uhr = new Spielzeit(Minute(5, 10));
            Pruefe.Gleich(1.0, w.Lagen.Faktor("Eisen"));
            Pruefe.Gleich(1.0, w.Lagen.Ertragsfaktor(Betriebsart.Mine));
            var leer = Wirtschaft(5, daten: Wirtschaftsdaten.AusJson(Datenpfad.OhneErnte(Json.Value)));
            Pruefe.Gleich(0, leer.Lagen.Quellen.Count);
            Pruefe.Gleich(1.0, leer.Lagen.Faktor("Gerste"));
        }),

        ("Lagen: Fehlerhafte Daten werden abgewiesen", () =>
        {
            var ohneErnte = System.Text.Json.Nodes.JsonNode.Parse(Json.Value)!.AsObject();
            ohneErnte.Remove("ernte");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(ohneErnte.ToJsonString()), "lagen: braucht");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"ertrag\": 0.5,", "\"ertrag\": 1.5,")), "lagen.ertrag");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"betrieb\": \"Steinbruch\"", "\"betrieb\": \"Mine\"")), "gibt es schon");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"betrieb\": \"Steinbruch\"", "\"betrieb\": \"Maurerei\"")), "Umlandbetrieb");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"Lavendel\": 1, \"Jasmin\": 1,", "\"Rosmarin\": 1, \"Jasmin\": 1,")), "Rosmarin");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"titel\": \"Felssturz\"", "\"titel\": \"\"")), "titel, text");
        }),

        ("Lagen: Spielstand – nach dem Laden dieselben Lagen und Faktoren, Speichern danach bitgleich; alter Spielstand ohne „lagen“ bleibt ohne", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 77 },
                Daten.Value, new NeuePartie());
            for (var i = 0; i < 200; i++) sim.Tick();
            var zeit = new DateTime(2026, 11, 15, 12, 0, 0);
            var json = Speicherung.Speichern(sim, spieler, zeit);
            var (geladen, gs) = Speicherung.Laden(json);
            Pruefe.Gleich(sim.Wirtschaft!.Lagen.Quellen.Count, geladen.Wirtschaft!.Lagen.Quellen.Count);
            for (var i = 0; i < sim.Wirtschaft.Lagen.Quellen.Count; i++)
            {
                var a = sim.Wirtschaft.Lagen.Quellen[i];
                var b = geladen.Wirtschaft.Lagen.Quellen[i];
                Pruefe.Gleich(Folge(a), Folge(b), a.Kennung);
            }
            Pruefe.Gleich(sim.Wirtschaft.Markt["Eisen"].Lagefaktor, geladen.Wirtschaft.Markt["Eisen"].Lagefaktor);
            Pruefe.Gleich(json, Speicherung.Speichern(geladen, gs, zeit));
            // Alter Spielstand: Daten ohne „lagen“ (Datenversion 91) – nur die Ernte, auch nach dem Laden
            var (alt, altSpieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, OhneLagen.Value, new NeuePartie());
            var (altGeladen, _) = Speicherung.Laden(Speicherung.Speichern(alt, altSpieler, zeit));
            Pruefe.Gleich(1, altGeladen.Wirtschaft!.Lagen.Quellen.Count);
        }),
    };
}
