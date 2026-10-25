using System.Numerics;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Kartenwerkstatt, Schritt 3 (PR #172): Mauern und Tore mit Rückgängig, die Turmregel im Kern (gleich der Zeichnung im Spiel), Wege rasten
/// in Tore und an Höfen ein, die Startbelegung bearbeiten und prüfen, „Karte ausprobieren“ (eine Partie direkt aus dem Entwurf), Streu-Gruppen
/// einzeln bearbeiten.
/// </summary>
static class KartenwerkstattStartTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));

    static string Text(string kennung) => File.ReadAllText(Datenpfad.Karte(kennung));
    static Kartenentwurf Oeffnen(string kennung) => Kartenentwurf.AusJson(Text(kennung), Katalog.Value);

    static Kartenentwurf GreyboxMit(Action<JsonObject> aenderung)
    {
        var json = JsonNode.Parse(Text("greybox"))!.AsObject();
        aenderung(json);
        return Kartenentwurf.AusJson(json.ToJsonString(), Katalog.Value);
    }

    static string Liste(IEnumerable<Befund> befunde) => string.Join("\n  ", befunde.Select(b => b.ToString()));
    static List<Befund> Fehler(Kartenentwurf e) => e.Befunde.Where(b => b.Schwere == Schwere.Fehler).ToList();
    static Kartenort O(double x, double z) => new(x, z);

    static void Folge<T>(IEnumerable<T> erwartet, IEnumerable<T> tatsaechlich, string? hinweis = null) =>
        Pruefe.Wahr(erwartet.SequenceEqual(tatsaechlich), $"{hinweis}: erwartet [{string.Join(", ", erwartet)}], war aber [{string.Join(", ", tatsaechlich)}]");

    /// <summary>
    /// Die Rechnung von Stadtansicht.Mauer.cs (Stand PR #170) in float wie in Godot – als Vergleich, dass der Kern dieselben Türme setzt.
    /// Gibt Mauertürme, Ecktürme und Tore als Punkte zurück.
    /// </summary>
    static (List<Vector3> Tuerme, List<Vector3> Ecken, List<Vector3> Tore) WieImSpiel(Kartenmauer m)
    {
        var linie = m.Linie.Select(p => new Vector3((float)p[0], 0, (float)p[1])).ToList();
        var tore = m.Tore.Select(t => new Vector3((float)t.Punkt[0], 0, (float)t.Punkt[1])).ToList();
        var mitte = linie.Aggregate(Vector3.Zero, (s, p) => s + p) / linie.Count;
        var (tuerme, ecken, torOrte) = (new List<Vector3>(), new List<Vector3>(), new List<Vector3>());
        var strecken = m.Geschlossen ? linie.Count : linie.Count - 1;
        int NaechsteStrecke(Vector3 ort)
        {
            var (beste, kleinster) = (0, float.MaxValue);
            for (var i = 0; i < strecken; i++)
            {
                var (a, b) = (linie[i], linie[(i + 1) % linie.Count]);
                var d = b - a;
                var t = Math.Clamp(Vector3.Dot(ort - a, d) / d.LengthSquared(), 0f, 1f);
                var abstand = Vector3.Distance(ort, a + d * t);
                if (abstand < kleinster) (kleinster, beste) = (abstand, i);
            }
            return beste;
        }
        var zuStrecke = tore.Select(t => (t, s: NaechsteStrecke(t))).ToList();
        for (var i = 0; i < strecken; i++)
        {
            var (von, bis) = (linie[i], linie[(i + 1) % linie.Count]);
            var laenge = Vector3.Distance(von, bis);
            var entlang = (bis - von) / laenge;
            var schnitte = new List<(float, float)>();
            var anfang = 0f;
            foreach (var (tor, _) in zuStrecke.Where(x => x.s == i).OrderBy(x => Vector3.Dot(x.t - von, entlang)))
            {
                var t = Math.Clamp(Vector3.Dot(tor - von, entlang), 0, laenge);
                schnitte.Add((anfang, t - 7.6f));
                anfang = t + 7.6f;
                torOrte.Add(von + entlang * t);
            }
            schnitte.Add((anfang, laenge));
            foreach (var (s0, s1) in schnitte)
            {
                var abschnitt = s1 - s0;
                if (abschnitt < 0.5f) continue;
                var innere = (int)Math.Round(abschnitt / 35f) - 1;
                for (var k = 1; k <= innere; k++) tuerme.Add(von + entlang * (s0 + abschnitt * k / (innere + 1)));
            }
        }
        for (var i = 0; i < linie.Count; i++)
        {
            var offenesEnde = !m.Geschlossen && (i == 0 || i == linie.Count - 1);
            if (!offenesEnde)
            {
                var (u, v) = (Vector3.Normalize(linie[i] - linie[(i + linie.Count - 1) % linie.Count]), Vector3.Normalize(linie[(i + 1) % linie.Count] - linie[i]));
                var knick = MathF.Atan2(Vector3.Cross(u, v).Length(), Vector3.Dot(u, v)) * 180 / MathF.PI;
                if (knick < 12) continue;
            }
            ecken.Add(linie[i]);
        }
        return (tuerme, ecken, torOrte);
    }

    static void GleicheOrte(IReadOnlyList<Vector3> spiel, IEnumerable<Mauerlage> kern, string was)
    {
        var k = kern.ToList();
        Pruefe.Gleich(spiel.Count, k.Count, $"{was}: Anzahl");
        for (var i = 0; i < spiel.Count; i++)
            Pruefe.Wahr(Math.Abs(spiel[i].X - k[i].X) < 1e-3 && Math.Abs(spiel[i].Z - k[i].Z) < 1e-3,
                $"{was} {i}: Spiel ({spiel[i].X} | {spiel[i].Z}), Kern ({k[i].X} | {k[i].Z})");
    }

    /// <summary>
    /// Eine kleine neue Stadt ohne Fehler: Markt, Mauer (Quadrat ±60 m) mit Osttor, eine Landstraße hindurch, zwei Werkstattplätze, zwei
    /// Wohnhausplätze; Start: der Spieler und ein KI-Haus mit einer Tischlerei.
    /// </summary>
    static Kartenentwurf Kleinstadt()
    {
        var e = Kartenentwurf.Neu("probestadt", "Probestadt", 400, 300, Katalog.Value);
        e.Daten = Daten.Value;
        e.Setzen("markt", -30, -30);
        var mauer = e.MauerAnlegen(new[] { O(-60, -60), O(60, -60), O(60, 60), O(-60, 60) }, geschlossen: true);
        e.TorSetzen(mauer, 61.3, 10, "Osttor");
        e.WegAnlegen(Wegklasse.Land, new[] { O(-50, 10), O(100, 10) }, glatt: false, name: "Landstraße");
        var w1 = e.Setzen("werkstattplatz", 20, 23, 180);
        var w2 = e.Setzen("werkstattplatz", 45, 23, 180);
        var h1 = e.Setzen("wohnhausplatz", -30, 2);
        var h2 = e.Setzen("wohnhausplatz", -15, 2);
        e.SpielerWohnhausSetzen(h1.Kennung);
        e.SpielerStartplatzSetzen(w2.Kennung);
        var haus = e.HausHinzufuegen();
        e.BetriebHinzufuegen(haus, w1.Kennung, Betriebsart.Tischlerei);
        Pruefe.Gleich(h2.Kennung, e.Start.Haeuser[haus].Wohnhaus, "das neue Haus bekommt den ersten freien Wohnhausplatz");
        return e;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        // ── Mauer und Tore ───────────────────────────────────────────────────

        ("Mauerplan: Greybox und Köln – Türme, Ecktürme und Tore genau wie bisher im Spiel gezeichnet (Greybox: 4 Ecktürme, 3 Mauertürme, 3 Tore)", () =>
        {
            foreach (var kennung in new[] { "greybox", "koeln" })
                foreach (var m in Stadtkarte.AusDatei(Datenpfad.Karte(kennung)).Mauern)
                {
                    var plan = Mauerplan.Von(m);
                    var (tuerme, ecken, tore) = WieImSpiel(m);
                    GleicheOrte(tuerme, plan.Tuerme, $"{kennung}, {m.Name}: Mauertürme");
                    GleicheOrte(ecken, plan.Ecktuerme, $"{kennung}, {m.Name}: Ecktürme");
                    GleicheOrte(tore, plan.Tore.Select(t => t.Lage), $"{kennung}, {m.Name}: Tore");
                }
            var greybox = Mauerplan.Von(Stadtkarte.AusDatei(Datenpfad.Karte("greybox")).Mauern[0]);
            Pruefe.Wahr(greybox.Stuecke.All(s => s.Laenge is >= 3 and <= 5), "Greybox: Mauerstücke 3 bis 5 m");
            Pruefe.Gleich(4, greybox.Ecktuerme.Count);
            // Nordseite ohne Tor zwei Türme (100 m: round(100 / 35) − 1), seit PR #199 auf der Südseite das Südtor und ein Turm
            Pruefe.Gleich(3, greybox.Tuerme.Count, "Mauertürme");
            Pruefe.Wahr(greybox.Tuerme.Any(t => Math.Abs(t.X - (-50 + 100 / 3.0)) < 1e-9 && t.Z == -50), "Nordseite bei einem Drittel");
            Folge(new[] { "Osttor", "Südtor", "Westtor" }, greybox.Tore.Select(t => t.Name).ToArray());
            // Feldseite zeigt nach außen: Nordmauer (z = −50) → −z, Ostmauer → +x; Eckturm Nordost zeigt nach Nordost
            var nord = greybox.Tuerme.First(t => t.Z == -50);
            Pruefe.Wahr(nord.FeldZ < -0.99, $"Nordmauer: Feldseite nach Norden ({nord.FeldX} | {nord.FeldZ})");
            var ost = greybox.Tore.Single(t => t.Name == "Osttor").Lage;
            Pruefe.Wahr(ost.FeldX > 0.99 && ost.X == 50 && ost.Z == 0, "Osttor auf der Ostmauer, Feldseite nach Osten");
            var nordost = greybox.Ecktuerme.Single(t => t.X == 50 && t.Z == -50);
            Pruefe.Wahr(nordost.FeldX > 0.7 && nordost.FeldZ < -0.7, "Eckturm Nordost schaut nach Nordost");
            // Entwurf und Stadtkarte rechnen dasselbe
            var entwurf = Oeffnen("greybox").Mauern[0].Plan;
            Pruefe.Gleich(greybox.AlleTuerme.Count(), entwurf.AlleTuerme.Count());
        }),

        ("Mauer: anlegen, Ecken verschieben, einfügen, löschen, ganz verschieben, öffnen – Tore wandern mit; alles mit Rückgängig", () =>
        {
            var e = Kartenentwurf.Neu("probe", "Probe", 300, 300, Katalog.Value);
            var leer = e.ZuJson();
            Pruefe.Wirft(() => e.MauerAnlegen(new[] { O(0, 0), O(10, 0) }, geschlossen: true));
            Pruefe.Wirft(() => e.MauerAnlegen(new[] { O(0, 0), O(10, 0) }, geschlossen: false, katalog: "markt"));
            var m = e.MauerAnlegen(new[] { O(-40, -40), O(40.02, -40), O(40, 40), O(-40, 40), O(-40, -40) }, geschlossen: true);
            Pruefe.Gleich("Stadtmauer", m.Name);
            Pruefe.Gleich(4, m.Ecken.Count, "der doppelte Schlusspunkt fällt weg");
            Pruefe.Gleich(O(40, -40), m.Ecken[1], "auf 5 cm");
            Pruefe.Wahr(e.Befunde.Any(b => b.Regel == "mauer-tor" && b.Schwere == Schwere.Hinweis && b.Andere.Contains(m)), "Hinweis: Mauer ohne Tor");

            var t = e.TorSetzen(m, 43, 10.02);
            Pruefe.Gleich(0, t);
            Pruefe.Gleich(new EntwurfTor("Tor", 40, 10), m.Tore[0], "das Tor rastet auf die Linie");
            Pruefe.Wahr(!e.Befunde.Any(b => b.Regel == "mauer-tor"), "mit Tor kein Hinweis");
            e.TorVerschieben(m, 0, 30, 47);
            Pruefe.Gleich(O(30, 40), m.Tore[0].Punkt, "entlang der Mauer auf die Südseite");
            e.TorUmbenennen(m, 0, "Südtor");
            e.TorSchliesstNachtsSetzen(m, 0, false);
            Pruefe.Gleich(new EntwurfTor("Südtor", 30, 40, false), m.Tore[0]);
            Pruefe.Gleich("Südtor bleibt nachts offen", e.Geschichte.RueckgaengigText);

            // Ecke Südost nach (50, 50): das Tor bleibt bei einem Achtel der Südstrecke (von (50, 50) nach (−40, 40))
            e.MauerEckeVerschieben(m, 2, 50, 50);
            Pruefe.Gleich(O(50, 50), m.Ecken[2]);
            var (a, b) = m.Strecke(2);
            var anteil = (40 - 30) / 80.0;
            Pruefe.Wahr(m.Tore[0].Punkt.Abstand(new Kartenort(a.X + (b.X - a.X) * anteil, a.Z + (b.Z - a.Z) * anteil)) < 0.05, $"Tor folgt der Strecke: {m.Tore[0].Punkt}");
            Pruefe.Wahr(!e.Befunde.Any(x => x.Regel == "tor"), "das Tor liegt auf der Linie:\n  " + Liste(e.Befunde));

            Pruefe.Gleich(4, e.MauerEckeEinfuegenBei(m, -40, 0.3), "auf der letzten Strecke (zurück zur ersten Ecke)");
            Pruefe.Gleich(O(-40, 0.3), m.Ecken[4]);
            Pruefe.Wahr(e.MauerEckeLoeschen(m, 4), "Ecke weg");
            Pruefe.Gleich(4, m.Ecken.Count);
            e.MauerVerschieben(m, 5, -5);
            Pruefe.Gleich(O(-35, -45), m.Ecken[0]);
            Pruefe.Wahr(!e.Befunde.Any(x => x.Regel == "tor"), "das Tor wandert mit");
            Pruefe.Wahr(e.MauerGeschlossenSetzen(m, false), "öffnen");
            Pruefe.Gleich(3, m.Strecken);
            Pruefe.Wahr(!e.Befunde.Any(x => x.Regel == "tor"), "Tor auf der übrigen Linie:\n  " + Liste(e.Befunde));
            Pruefe.Wahr(e.ZuJson().Contains("\"geschlossen\": false"), "offen geschrieben");

            // Rückgängig führt alles zurück, Wiederholen wieder hin
            var ende = e.ZuJson();
            while (e.Rueckgaengig()) { }
            Pruefe.Gleich(leer, e.ZuJson(), "alles zurückgenommen");
            while (e.Wiederholen()) { }
            Pruefe.Gleich(ende, e.ZuJson(), "alles wiederholt");
            Pruefe.Gleich(ende, Kartenentwurf.AusJson(ende, Katalog.Value).ZuJson(), "geschrieben und wieder gelesen");
            var karte = Stadtkarte.AusJson(ende, Katalog.Value);
            Pruefe.Gleich("Südtor", karte.Mauern[0].Tore[0].Name);
            Pruefe.Wahr(!karte.Mauern[0].Tore[0].SchliesstNachts, "schließt nachts nicht");

            e.TorLoeschen(m, 0);
            e.MauerLoeschen(m);
            Pruefe.Gleich(0, e.Mauern.Count);
            e.Rueckgaengig();
            e.Rueckgaengig();
            Pruefe.Gleich(ende, e.ZuJson(), "Mauer und Tor wieder da");
            Pruefe.Wahr(ReferenceEquals(m, e.Mauern[0]), "die Mauer bleibt dasselbe Objekt");
        }),

        ("Mauer: Greybox und Köln – Mauern bearbeiten und zurücknehmen ergibt Byte für Byte die Datei; das Ereignis nennt die Mauer", () =>
        {
            foreach (var kennung in new[] { "greybox", "koeln" })
            {
                var e = Oeffnen(kennung);
                var meldungen = new List<Kartenaenderung>();
                e.Geaendert += meldungen.Add;
                var m = e.Mauern[0];
                e.TorSetzen(m, m.Ecken[1].X, m.Ecken[1].Z + 3);
                e.MauerEckeVerschieben(m, 1, m.Ecken[1].X + 2, m.Ecken[1].Z);
                e.MauerUmbenennen(m, "Neue Mauer");
                // seit Schritt 4 wandern Wege durch ein Tor mit, wenn die Ecke das Tor verschiebt – dann auch ihr Ereignis (geändert)
                Pruefe.Wahr(meldungen.Where(x => x.Teil != Kartenteil.Weg).All(x => x.Teil == Kartenteil.Mauer && ReferenceEquals(x.Mauer, m))
                            && meldungen.Any(x => ReferenceEquals(x.Mauer, m))
                            && meldungen.Where(x => x.Teil == Kartenteil.Weg).All(x => x.Art == Aenderungsart.Geaendert), "Ereignis für die Mauer (und Wege im Tor)");
                while (e.Rueckgaengig()) { }
                Pruefe.Gleich(Text(kennung), e.ZuJson(), kennung);
            }
        }),

        ("Mauer: Prüfung – Tor neben der Linie, Weg durch die Mauer ohne Tor, Gebäude auf der Mauer (Fehler), Mauer ohne Tor (Hinweis)", () =>
        {
            var e = GreyboxMit(k => k["mauern"]![0]!["tore"]![0]!["punkt"] = new JsonArray(55, 0));
            Pruefe.Wahr(Fehler(e).Any(b => b.Regel == "tor" && b.Text.Contains("Osttor") && b.Text.Contains("neben der Linie")), "Tor neben der Linie:\n  " + Liste(Fehler(e)));
            Pruefe.Wahr(Fehler(e).Any(b => b.Regel == "tor" && b.Text.Contains("kreuzt die Mauer")), "die Ostgasse kreuzt nun ohne Tor");

            var g = Oeffnen("greybox");
            var weg = g.WegAnlegen(Wegklasse.Gasse, new[] { O(40, -30), O(60, -30) }, glatt: false);
            Pruefe.Wahr(Fehler(g).Any(b => b.Regel == "tor" && b.Andere.Contains(weg) && b.Andere.Contains(g.Mauern[0])), "Weg ohne Tor:\n  " + Liste(Fehler(g)));

            var werkstatt = g.Gebaeude.First(x => x.Art == Bausteinart.Werkstattplatz);
            g.Verschieben(werkstatt.Kennung, 50, 20);
            Pruefe.Wahr(Fehler(g).Any(b => b.Regel == "mauer" && b.Gebaeude.Contains(werkstatt.Kennung) && b.Text.Contains("auf der Mauer")), "Werkstatt auf der Mauer");
            var wohnhaus = g.Gebaeude.First(x => x.Art == Bausteinart.Wohnhausplatz);
            g.Verschieben(wohnhaus.Kennung, -50, -30);
            Pruefe.Wahr(Fehler(g).Any(b => b.Regel == "mauer" && b.Gebaeude.Contains(wohnhaus.Kennung)), "jedes Gebäude, nicht nur Werkstätten");

            // Köln: zwei Gebäude auf der Mauer bleiben Hinweise (dort gelten die Stadtregeln nicht), die fehlende Startbelegung auch
            var koeln = Oeffnen("koeln");
            var hinweise = koeln.Befunde.Where(b => b.Schwere == Schwere.Hinweis).ToList();
            Pruefe.Wahr(hinweise.Count(b => b.Regel == "mauer") == 2 && hinweise.Any(b => b.Regel == "start-fehlt"), "Köln:\n  " + Liste(hinweise));
            Pruefe.Wahr(!koeln.HatFehler, "Köln ohne Fehler");
        }),

        ("Mauer: Ein Wegende rastet in die Tormitte ein, ein Punkt dazwischen auch; läuft schon ein Weg durchs Tor, rastet es an ihm ein", () =>
        {
            var e = Kartenentwurf.Neu("probe", "Probe", 300, 300, Katalog.Value);
            var m = e.MauerAnlegen(new[] { O(-40, -40), O(40, -40), O(40, 40), O(-40, 40) }, geschlossen: true);
            e.TorSetzen(m, 40, 0, "Osttor");
            var fang = e.Einrasten(42.5, 1.5);
            Pruefe.Wahr(fang is { Ziel: Fangziel.Tor, Tor: 0, Weg: null } f && f.Ort == O(40, 0) && ReferenceEquals(f.Mauer, m), $"Vorschau: Tor ({fang})");
            Pruefe.Wahr(e.Einrasten(45, 0) is null, "außerhalb des Torfangs (4 m)");
            var weg = e.WegAnlegen(Wegklasse.Land, new[] { O(0, 0), O(38.5, 2) }, glatt: false);
            Pruefe.Gleich(O(40, 0), weg.Punkte[^1], "Ende in der Tormitte");
            var durch = e.WegAnlegen(Wegklasse.Land, new[] { O(0, 20), O(41, -2.5), O(80, 0) }, glatt: false);
            Pruefe.Gleich(O(40, 0), durch.Punkte[1], "Punkt dazwischen in der Tormitte");
            Pruefe.Wahr(!e.Befunde.Any(b => b.Regel == "tor"), "kein Weg kreuzt ohne Tor:\n  " + Liste(e.Befunde));
            e.StuetzpunktVerschieben(durch, 1, 37.5, 2);
            Pruefe.Gleich(O(40, 0), durch.Punkte[1], "auch beim Verschieben");

            // Greybox: Die Ostgasse hat ihren Stützpunkt in der Tormitte – ein neuer Weg rastet dort an ihr ein
            var g = Oeffnen("greybox");
            var gf = g.Einrasten(48.5, 2);
            Pruefe.Wahr(gf is { Ziel: Fangziel.Stuetzpunkt, Tor: 0, AmWeg: true } x && x.Ort == O(50, 0), $"Greybox: am Weg im Tor ({gf})");
        }),

        // ── Höfe und Halteplätze ─────────────────────────────────────────────

        ("Einrasten: Höfe der Werkstattplätze sind Fangpunkte (vor Wegen); eine Gasse, die am Hof endet, wird umgedreht; Halteplätze der Darstellung", () =>
        {
            var e = Kartenentwurf.Neu("probe", "Probe", 300, 300, Katalog.Value);
            var w = e.Setzen("werkstattplatz", 0, 0);
            var hof = w.Hof!.Value; // (0, 6.5)
            e.WegAnlegen(Wegklasse.Land, new[] { O(-30, 8), O(30, 8) }, glatt: false); // 1,5 m neben dem Hof
            var fang = e.Einrasten(0.5, 7.5);
            Pruefe.Wahr(fang is { Ziel: Fangziel.Hof, Weg: null } f && f.Ort == hof && ReferenceEquals(f.Gebaeude, w), $"Hof vor Weg ({fang})");
            var gasse = e.WegAnlegen(Wegklasse.Gasse, new[] { O(20, 20), O(0.4, 7) }, glatt: false);
            Pruefe.Gleich(hof, gasse.Punkte[0], "die Gasse beginnt am Hof (umgedreht)");
            Pruefe.Gleich(O(20, 20), gasse.Punkte[^1]);
            Pruefe.Gleich(2, e.Wege[0].Punkte.Count, "die Landstraße bekommt keinen Stützpunkt");
            // Ziehen eines Endes an den Hof: bleibt am Hof
            var feld = e.WegAnlegen(Wegklasse.Feldweg, new[] { O(-20, 30), O(-20, 20) }, glatt: false);
            e.StuetzpunktVerschieben(feld, 1, -0.6, 6);
            Pruefe.Gleich(hof, feld.Punkte[1]);

            // Halteplätze (Karrenplätze, Gatter) nennt die Darstellung
            var grube = e.Setzen("grube", 80, 0);
            e.Fangpunkte = g => g.Katalog == "grube" ? new[] { new Kartenort(g.X + 3, g.Z + 9) } : Array.Empty<Kartenort>();
            Pruefe.Wahr(e.Einrasten(84, 9.5) is { Ziel: Fangziel.Halteplatz } h && h.Ort == O(83, 9) && ReferenceEquals(h.Gebaeude, grube), "Halteplatz");
            var zur = e.WegAnlegen(Wegklasse.Feldweg, new[] { O(30, 8), O(82, 10) }, glatt: false);
            Pruefe.Gleich(O(83, 9), zur.Punkte[^1]);
        }),

        // ── Startbelegung ────────────────────────────────────────────────────

        ("Start: Greybox – Belegung, freie Plätze, Betriebsarten je Platz; gelesen wie der Kern liest", () =>
        {
            var e = Oeffnen("greybox");
            e.Daten = Daten.Value;
            var s = e.Start;
            Folge(new[] { "Haus Eisenbart", "Haus Eichwald", "Haus Kieselbach" }, s.Haeuser.Select(h => h.Name).ToArray());
            Pruefe.Gleich("Wohnhaus1", s.SpielerWohnhaus);
            Pruefe.Gleich(new EntwurfStartplatz("Weinmarkt", "{art} am Weinmarkt"), s.SpielerWerkstatt);
            Pruefe.Gleich(Wappen.Eichwald, s.Haeuser[1].Wappen);
            Pruefe.Gleich("Rapier", s.Haeuser[0].Betriebe[0].Produkt);
            var bel = e.BelegungVon("Giesserei_Eisenbart");
            Pruefe.Wahr(bel is [{ Haus: 0, Art: Belegungsart.Betrieb, Betrieb: 0 }], "Gießerei Eisenbart gehört Haus Eisenbart");
            Pruefe.Wahr(e.BelegungVon("Wohnhaus1") is [{ IstSpieler: true, Art: Belegungsart.Wohnhaus }], "Wohnhaus1 ist das des Spielers");
            Pruefe.Wahr(e.BelegungVon("Weinmarkt") is [{ IstSpieler: true, Art: Belegungsart.Startplatz }], "Weinmarkt ist der Startplatz");
            Folge(new[] { "Wohnhaus5", "Wohnhaus6", "Wohnhaus7", "Wohnhaus8" }, e.FreieWohnhausplaetze().Select(g => g.Kennung).ToArray());
            Pruefe.Wahr(e.FreieBetriebsplaetze().Any(g => g.Kennung == "Grube_am_Silberberg") && !e.FreieBetriebsplaetze().Any(g => g.Kennung == "Grube_Eisenbart"), "freie Plätze");
            var arten = e.BetriebsartenFuer(e.GebaeudeMit("Weinmarkt")!);
            Pruefe.Wahr(arten.Contains(Betriebsart.Giesserei) && arten.Contains(Betriebsart.Tischlerei) && !arten.Contains(Betriebsart.Schmiede) && !arten.Contains(Betriebsart.Mine),
                "Werkstattplatz: Werkstätten der Stufe 1: " + string.Join(", ", arten));
            Folge(new[] { Betriebsart.Mine }, e.BetriebsartenFuer(e.GebaeudeMit("Grube_am_Silberberg")!).ToArray());
            // Dieselbe Belegung wie der Leser der Stadtkarte
            var kern = Stadtkarte.AusDatei(Datenpfad.Karte("greybox")).Start!;
            var entwurf = s.AlsStartbelegung();
            Folge(kern.Rivalennamen.ToArray(), entwurf.Rivalennamen.ToArray());
            Folge(kern.Rivalenwappen.ToArray(), entwurf.Rivalenwappen.ToArray());
            Pruefe.Wahr(!e.HatFehler, "Greybox ohne Fehler (mit Daten):\n  " + Liste(Fehler(e)));
        }),

        ("Start: Häuser hinzufügen (bis 8), bearbeiten, Betriebe, Startplatz je Beruf – alles mit Rückgängig, Greybox wieder Byte für Byte", () =>
        {
            var e = Oeffnen("greybox");
            e.Daten = Daten.Value;
            var meldungen = new List<Kartenaenderung>();
            e.Geaendert += meldungen.Add;
            var i = e.HausHinzufuegen();
            Pruefe.Gleich(3, i);
            var h = e.Start.Haeuser[3];
            Pruefe.Gleich("Haus Ahlbeck", h.Name);
            Pruefe.Gleich("Wohnhaus5", h.Wohnhaus, "der erste freie Wohnhausplatz");
            Pruefe.Wahr(h.Farbe is not null && h.Farbe != "#b33329" && h.Farbe != "#336bb8", $"eine freie Farbe ({h.Farbe})");
            Pruefe.Wahr(!e.Start.Haeuser.Take(3).Any(x => x.Wappen == h.Wappen), "ein freies Wappen");
            Pruefe.Wahr(meldungen[^1] is { Teil: Kartenteil.Start } m && ReferenceEquals(m.Start, e.Start), "Ereignis für die Startbelegung");
            Pruefe.Wahr(Fehler(e).Any(b => b.Text.Contains("Haus Ahlbeck: Ein Haus braucht mindestens einen Betrieb")), "noch ohne Betrieb");

            var b = e.BetriebHinzufuegen(i, "Grube_am_Silberberg", Betriebsart.Mine);
            Pruefe.Gleich(0, b);
            Pruefe.Gleich("Grube am Silberberg", e.GebaeudeMit("Grube_am_Silberberg")!.Betrieb, "ein Platz mit Namen behält ihn");
            Pruefe.Wahr(!e.HatFehler, "vollständiges Haus ohne Fehler:\n  " + Liste(Fehler(e)));
            // Ein Werkstattplatz ohne Betriebsnamen bekommt im selben Schritt einen (die Greybox hat keinen freien: einer vor der Stadt)
            var w = e.Setzen("werkstattplatz", 0, 75);
            e.BetriebHinzufuegen(i, w.Kennung, Betriebsart.Tischlerei, "Truhe");
            Pruefe.Gleich("Tischlerei Ahlbeck", w.Betrieb, "der Platz bekommt einen Betriebsnamen");
            Pruefe.Gleich(new EntwurfStartbetrieb(w.Kennung, Betriebsart.Tischlerei, "Truhe"), e.Start.Haeuser[3].Betriebe[1]);
            Pruefe.Gleich("Haus Ahlbeck: Betrieb hinzugefügt", e.Geschichte.RueckgaengigText);
            e.BetriebProduktSetzen(i, 1, null);
            Pruefe.Gleich(null, e.Start.Haeuser[3].Betriebe[1].Produkt);
            e.Rueckgaengig();
            e.Rueckgaengig();
            Pruefe.Wahr(string.IsNullOrEmpty(w.Betrieb) && e.Start.Haeuser[3].Betriebe.Count == 1, "ein Rückgängig nimmt Betrieb und Namen zurück");
            e.Rueckgaengig();
            Pruefe.Wahr(e.GebaeudeMit(w.Kennung) is null, "Werkstattplatz wieder weg");

            e.HausUmbenennen(i, "Haus Wendelstein");
            e.HausVolkSetzen(i, Volk.Zwerg);
            e.HausGeschlechtSetzen(i, Geschlecht.Frau);
            e.HausFarbeSetzen(i, "#123456");
            e.HausWappenSetzen(i, new Wappen(Teilung.Geviert, Tinktur.Rot, Tinktur.Gold, Figur.Krone));
            h = e.Start.Haeuser[3];
            Pruefe.Gleich(new EntwurfHaus("Haus Wendelstein", Volk.Zwerg, Geschlecht.Frau, new Wappen(Teilung.Geviert, Tinktur.Rot, Tinktur.Gold, Figur.Krone), "#123456", "Wohnhaus5"),
                h with { Betriebe = Array.Empty<EntwurfStartbetrieb>() });
            Pruefe.Gleich(new EntwurfStartbetrieb("Grube_am_Silberberg", Betriebsart.Mine), h.Betriebe.Single());
            e.HausWohnhausSetzen(i, "Wohnhaus8");
            e.SpielerStartplatzSetzen("Weinmarkt", "Tischler", "Tischlerei am Weinmarkt");
            Pruefe.Gleich("Tischlerei am Weinmarkt", e.Start.StartplatzFuer("Tischler")!.Name);
            Pruefe.Gleich("{art} am Weinmarkt", e.Start.StartplatzFuer("Schmied")!.Name);
            Pruefe.Wahr(!e.HatFehler, "ohne Fehler:\n  " + Liste(Fehler(e)));
            var karte = e.ZuStadtkarte();
            Pruefe.Gleich(4, karte.Start!.Haeuser.Count);
            Pruefe.Gleich("Weinmarkt", karte.Start.Spieler.WerkstattFuer("Tischler").Platz);
            Pruefe.Gleich("Tischlerei am Weinmarkt", karte.Start.Spieler.WerkstattFuer("Tischler").Name);
            e.SpielerStartplatzEntfernen("Tischler");
            Pruefe.Gleich(0, e.Start.JeBeruf.Count);

            // bis acht Häuser, dann nicht mehr
            while (e.Start.Haeuser.Count < Kartenentwurf.HoechstensHaeuser) Pruefe.Wahr(e.HausHinzufuegen() >= 0, "Platz für ein Haus");
            var schritte = e.Geschichte.Schritte;
            Pruefe.Gleich(-1, e.HausHinzufuegen());
            Pruefe.Gleich(schritte, e.Geschichte.Schritte, "kein Schritt");
            Pruefe.Gleich(8, e.Start.Haeuser.Select(x => x.Name).Distinct().Count());
            Pruefe.Gleich(8, e.Start.Haeuser.Select(x => x.Wappen).Distinct().Count());
            e.HausEntfernen(7);
            Pruefe.Gleich(7, e.Start.Haeuser.Count);
            e.BetriebEntfernen(0, 1);
            Pruefe.Gleich(1, e.Start.Haeuser[0].Betriebe.Count);

            while (e.Rueckgaengig()) { }
            Pruefe.Gleich(Text("greybox"), e.ZuJson(), "alles zurückgenommen: die Greybox Byte für Byte");
        }),

        ("Start: Köln ohne Startbelegung – die erste landet hinter „uebergaenge“ (wie das Werkzeug schreibt); zurückgenommen fällt sie wieder weg", () =>
        {
            var e = Oeffnen("koeln");
            Pruefe.Wahr(e.Start.IstLeer, "Köln hat keine Startbelegung");
            e.HausHinzufuegen();
            var json = e.ZuJson();
            var (ue, st, vo) = (json.IndexOf("\n \"uebergaenge\""), json.IndexOf("\n \"start\""), json.IndexOf("\n \"vorkommen\""));
            Pruefe.Wahr(ue > 0 && ue < st && st < vo, $"Reihenfolge uebergaenge {ue} < start {st} < vorkommen {vo}");
            e.Rueckgaengig();
            Pruefe.Gleich(Text("koeln"), e.ZuJson());
        }),

        ("Start: Prüfung – Haus ohne Wohnhaus, Platz doppelt, Betriebsart passt nicht, Name doppelt, Startplatz fehlt, Ware, kein Haus", () =>
        {
            Kartenentwurf Neu()
            {
                var e = Oeffnen("greybox");
                e.Daten = Daten.Value;
                return e;
            }
            void Erwarte(Kartenentwurf e, string text)
            {
                var f = Fehler(e);
                Pruefe.Wahr(f.Any(b => b.Regel == "start" && b.Text.Contains(text) && b.Andere.Contains(e.Start)), $"erwartet „{text}“:\n  " + Liste(f));
            }

            var e = Neu();
            e.HausWohnhausSetzen(1, null);
            Erwarte(e, "Haus Eichwald: Es fehlt das Wohnhaus");
            e = Neu();
            e.HausWohnhausSetzen(1, "Wohnhaus2");
            Erwarte(e, "Der Wohnhausplatz „Wohnhaus2“ ist mehrfach vergeben");
            e = Neu();
            e.BetriebHinzufuegen(2, "Giesserei_Eisenbart", Betriebsart.Giesserei);
            Erwarte(e, "Der Platz „Giesserei_Eisenbart“ ist mehrfach vergeben");
            e = Neu();
            e.BetriebHinzufuegen(2, "Weinmarkt", Betriebsart.Maurerei);
            Erwarte(e, "Der Platz „Weinmarkt“ ist mehrfach vergeben");
            e = Neu();
            var frei = e.Setzen("werkstattplatz", 0, 75); // die Greybox hat keinen freien Werkstattplatz
            e.BetriebHinzufuegen(0, frei.Kennung, Betriebsart.Schmiede);
            Erwarte(e, "Schmiede ist keine Werkstatt der Stufe 1");
            e = Neu();
            frei = e.Setzen("werkstattplatz", 0, 75);
            e.BetriebHinzufuegen(0, frei.Kennung, Betriebsart.Mine);
            Erwarte(e, "Mine ist keine Werkstatt der Stufe 1");
            e = Neu();
            e.BetriebHinzufuegen(0, "Grube_am_Silberberg", Betriebsart.Steinbruch);
            Erwarte(e, "kein Platz für Steinbruch");
            e = Neu();
            e.BetriebProduktSetzen(0, 0, "Truhe");
            Erwarte(e, "Giesserei stellt „Truhe“ nicht her");
            e = Neu();
            e.HausUmbenennen(2, "Haus Eisenbart");
            Erwarte(e, "Das Haus „Haus Eisenbart“ steht doppelt");
            e = Neu();
            e.HausWappenSetzen(2, Wappen.Eisenbart);
            Erwarte(e, "dasselbe Wappen");
            e = Neu();
            e.HausFarbeSetzen(0, "rot");
            Erwarte(e, "Die Farbe „rot“ ist nicht #rrggbb");
            e = Neu();
            e.SpielerStartplatzEntfernen();
            Erwarte(e, "Der Startplatz der Werkstatt fehlt");
            e = Neu();
            e.SpielerWohnhausSetzen(null);
            Erwarte(e, "Spieler: Es fehlt das Wohnhaus");
            e = Neu();
            e.SpielerStartplatzSetzen("Wohnhaus6", "Wirt");
            Erwarte(e, "Spieler (Wirt): „Wohnhaus6“ ist kein Werkstattplatz");
            e = Neu();
            e.SpielerStartplatzSetzen("Weinmarkt", "Gaukler");
            Erwarte(e, "Den Beruf „Gaukler“ gibt es nicht");
            e = Neu();
            for (var i = 2; i >= 0; i--) e.HausEntfernen(i);
            Erwarte(e, "mindestens ein KI-Haus");
            // Ohne Daten prüft die Werkstatt Betriebsart und Ware am Werkstattplatz nicht
            e = Oeffnen("greybox");
            frei = e.Setzen("werkstattplatz", 0, 75);
            e.BetriebHinzufuegen(0, frei.Kennung, Betriebsart.Schmiede);
            e.BetriebProduktSetzen(0, 0, "Truhe");
            Pruefe.Wahr(!Fehler(e).Any(b => b.Regel == "start"), "ohne Daten:\n  " + Liste(Fehler(e)));
        }),

        // ── Karte ausprobieren ───────────────────────────────────────────────

        ("Ausprobieren: Greybox-Entwurf → Partie mit denselben Häusern, Betrieben und Wohnhäusern wie aus der Datei", () =>
        {
            var e = Oeffnen("greybox");
            var (sim, spieler) = e.Ausprobieren(Daten.Value, "Schmied");
            var (simDatei, spielerDatei) = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie(Rivalen: 3), karte: Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));
            var (w, wd) = (sim.Wirtschaft!, simDatei.Wirtschaft!);
            Folge(wd.Konten.Select(k => k.Inhaber).ToArray(), w.Konten.Select(k => k.Inhaber).ToArray());
            Folge(wd.Konten.Select(k => k.Wappen).ToArray(), w.Konten.Select(k => k.Wappen).ToArray());
            Folge(wd.Betriebe.Select(b => (b.Name, b.Art, b.Konto.Inhaber, b.Produkt)).ToArray(), w.Betriebe.Select(b => (b.Name, b.Art, b.Konto.Inhaber, b.Produkt)).ToArray());
            Folge(wd.Konten.Select(k => wd.Bauen.Wohnhaus(k)?.Kennung).ToArray(), w.Konten.Select(k => w.Bauen.Wohnhaus(k)?.Kennung).ToArray());
            Pruefe.Gleich("Gießerei am Weinmarkt", w.Betriebe.First(b => b.Konto == spieler).Name);
            Pruefe.Wahr(w.Karte is not null && w.Bauen.IstAktiv, "die Partie hat die Karte und baut");

            // mit einem anderen Beruf und einem vierten Haus aus der Werkstatt
            e.Daten = Daten.Value;
            var i = e.HausHinzufuegen();
            e.BetriebHinzufuegen(i, "Grube_am_Silberberg", Betriebsart.Mine);
            var (sim4, spieler4) = e.Ausprobieren(Daten.Value, "Tischler");
            Pruefe.Gleich(5, sim4.Wirtschaft!.Konten.Count, "Spieler und vier KI-Häuser");
            Pruefe.Gleich("Tischlerei am Weinmarkt", sim4.Wirtschaft.Betriebe.First(b => b.Konto == spieler4).Name);
            Pruefe.Gleich("Wohnhaus5", sim4.Wirtschaft.Bauen.Wohnhaus(sim4.Wirtschaft.Konten[4])!.Kennung);
            Pruefe.Wahr(sim4.Wirtschaft.Betriebe.Any(b => b.Name == "Grube am Silberberg" && b.Konto.Inhaber == "Haus Ahlbeck"), "die Grube gehört dem neuen Haus");
            // die Partie läuft
            for (var t = 0; t < 200; t++) sim4.Tick();

            // mit Fehler nicht
            e.HausWohnhausSetzen(i, null);
            Pruefe.WirftMitMeldung(() => e.Ausprobieren(Daten.Value), "Fehler");
        }),

        ("Ausprobieren: eine neue kleine Stadt aus der Werkstatt (Mauer, Tor, Straße, Plätze, Start) hat keine Fehler und lässt sich spielen", () =>
        {
            var e = Kleinstadt();
            Pruefe.Wahr(!e.HatFehler, "Kleinstadt ohne Fehler:\n  " + Liste(Fehler(e)));
            Pruefe.Gleich(O(60, 10), e.Mauern[0].Tore[0].Punkt, "das Tor rastet auf die Mauer");
            var probe = e.Probepartie("Steinmetz");
            Pruefe.Gleich(1, probe.Rivalen);
            var (sim, spieler) = e.Ausprobieren(Daten.Value, "Steinmetz");
            var w = sim.Wirtschaft!;
            Pruefe.Gleich(2, w.Konten.Count);
            Pruefe.Gleich("Haus Ahlbeck", w.Konten[1].Inhaber);
            Pruefe.Gleich("Maurerei am Werkstattplatz 2", w.Betriebe.First(b => b.Konto == spieler).Name);
            Pruefe.Gleich("Tischlerei Ahlbeck", w.Betriebe.Single(b => b.Konto == w.Konten[1]).Name);
            for (var t = 0; t < 200; t++) sim.Tick();
            // Wieder schreiben und lesen: dieselbe Karte
            var json = e.ZuJson();
            Pruefe.Gleich(json, Kartenentwurf.AusJson(json, Katalog.Value).ZuJson());
            Pruefe.Wahr(json.Contains("\"start\": {") && json.Contains("\"tore\": [{\"name\": \"Osttor\", \"punkt\": [60, 10], \"schliesstNachts\": true}]"), json);
        }),

        // ── Natur: Gruppen einzeln, dichtere Wälder ──────────────────────────

        ("Natur: Gruppen einer Streufläche einzeln bearbeiten (Gewicht, Radius, Teile) – die übrigen bleiben; Rückgängig gibt die Datei zurück", () =>
        {
            var e = Oeffnen("greybox");
            var streu = e.Naturflaechen.Single(f => f.Name == "Streu im Umland");
            var vorher = streu.Gruppen.ToList();
            e.GruppeSetzen(streu, 1, 0.5, 3);
            Pruefe.Gleich(0.5, streu.Gruppen[1].Gewicht);
            Pruefe.Gleich(3.0, streu.Gruppen[1].Radius);
            Pruefe.Wahr(ReferenceEquals(vorher[1].Teile, streu.Gruppen[1].Teile), "die Teile bleiben");
            Pruefe.Wahr(ReferenceEquals(vorher[0], streu.Gruppen[0]) && ReferenceEquals(vorher[2], streu.Gruppen[2]), "die anderen Gruppen bleiben");
            e.GruppenteilSetzen(streu, 0, 1, new Dictionary<string, double> { ["farn"] = 2, ["gras"] = 1, ["pilze"] = 0 }, 3, 5);
            Folge(new[] { "farn", "gras" }, streu.Gruppen[0].Teile[1].Arten.Keys.ToArray(), "Gewicht 0 fällt weg");
            Folge(new[] { 3, 5 }, streu.Gruppen[0].Teile[1].Anzahl);
            Pruefe.Wahr(ReferenceEquals(vorher[0].Teile[0], streu.Gruppen[0].Teile[0]), "der andere Teil bleibt");
            var t = e.GruppenteilHinzufuegen(streu, 0, new Dictionary<string, double> { ["wildblumen"] = 1 }, 1, 2);
            Pruefe.Gleich(2, t);
            Pruefe.Wahr(e.GruppenteilEntfernen(streu, 0, 2), "Teil entfernt");
            var g = e.GruppeHinzufuegen(streu, art: "farn");
            Pruefe.Gleich(vorher.Count, g);
            Pruefe.Wahr(!e.HatFehler, "ohne Fehler:\n  " + Liste(Fehler(e)));
            Pruefe.Wahr(e.GruppeEntfernen(streu, 0), "Gruppe entfernt");
            Pruefe.Gleich(vorher.Count, streu.Gruppen.Count);
            var frei = e.Freiraum();
            Pruefe.Wahr(e.NaturVon(streu, frei).Count > 0, "die Fläche streut weiter");

            // eine Gruppe mit einem Teil: der letzte Teil bleibt; die letzte Gruppe auch
            var bach = e.Naturflaechen.Single(f => f.Name == "Am Bach beim Kräutergarten");
            Pruefe.Wahr(!e.GruppeEntfernen(bach, 0), "die letzte Gruppe bleibt");
            var wald = e.Naturflaechen.Single(f => f.Name == "Tannenwald um die Stadt");
            Pruefe.Wahr(!e.GruppenteilEntfernen(wald, 0, 0), "der letzte Teil bleibt");

            while (e.Rueckgaengig()) { }
            Pruefe.Gleich(Text("greybox"), e.ZuJson(), "Greybox Byte für Byte");
        }),

        ("Natur: Eine neue Waldfläche ist dichter (10 Bäume je 1.000 m²), die Greybox bleibt, wie sie ist", () =>
        {
            var e = Kartenentwurf.Neu("probe", "Probe", 400, 400, Katalog.Value);
            var f = e.NaturflaecheAnlegen("wald", new[] { O(100, 100), O(136, 100), O(136, 136), O(100, 136) });
            Pruefe.Gleich(Kartenentwurf.WaldDichte, f.Dichte);
            Pruefe.Gleich(10.0, f.Dichte);
            var baeume = e.NaturVon(f, e.Freiraum()).Count;
            Pruefe.Wahr(baeume is >= 10 and <= 15, $"1.296 m² tragen {baeume} Bäume (vorher 3)");
            Pruefe.Gleich(Kartenentwurf.StreuDichte, e.NaturflaecheAnlegen("streu", new[] { O(-100, -100), O(-60, -100), O(-60, -60) }).Dichte);
            // die Wälder der Greybox selbst (seit Schritt 5a hat sie im Randstreifen dazu Waldstücke des Rand-Vorschlags mit der neuen Dichte)
            var greybox = Oeffnen("greybox");
            // (seit PR #199 reicht das Spielfeld nach Süden bis in ein Waldstück des Randes – das zählt nicht dazu)
            Pruefe.Wahr(greybox.Naturflaechen.Where(n => n.Art == "wald" && !n.Name.StartsWith("Wald im Rand") && Kartenrechteck.Um(n.Ecken)!.Value.Ueberdeckt(greybox.Kartengrenzen.Spielfeld))
                .Select(n => n.Dichte).SequenceEqual(new[] { 2.4, 2.4 }), "Greybox-Wälder unverändert");
        }),
    };
}
