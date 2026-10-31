using Reichsgilde;
using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests der Umgebungsklänge (Ton Schritt 4; Spielordner <c>KlangUmgebungModell.cs</c>, <c>KlangStadtlebenModell.cs</c>, <c>KlangStadtlebenKern.cs</c>,
/// hier mitübersetzt – sie kennen kein Godot). Die große Selbstprüfung des Spiels (<c>--umgebungtest=1</c>) läuft hier als ein Test mit; dazu
/// Nachbesserung 31.10.2026: <b>Gemurmel, Schenken und Hämmern hängen an echten Menschen</b>. Leere Straßen sind still, volle laut, der Umkreis hängt
/// am Zoom, die Schänke klingt nur bei Gästen und nur zwischen 8 und 22 Uhr, die Werkstatt hämmert nur, wenn gearbeitet wird (8–20 Uhr), und die
/// Kernabfragen stimmen in einer echten Partie über einen ganzen Spieltag.
/// </summary>
static class KlangUmgebungTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Umgebungsklänge: die Selbstprüfung des Spiels (Kurven, Pegel, Tempo 32, Rufe, Menschen) meldet keinen Fehler", () =>
        {
            var log = new List<string>();
            var gut = KlangUmgebungModell.Selbsttest(log.Add);
            Pruefe.Wahr(gut, string.Join(" | ", log.Where(l => l.Contains("FEHLER"))));
        }),

        ("Umgebungsklänge: die Selbstprüfung bemerkt eine absichtlich falsche Kurve", () =>
        {
            var log = new List<string>();
            Pruefe.Gleich(false, KlangUmgebungModell.Selbsttest(log.Add, kaputt: true));
        }),

        ("Menschen: leere Straße = Stille (Gemurmel 0, Schenke 0, Stadtleben nur der Grund)", () =>
        {
            var z = new KlangStadtlebenModell.Zustand();
            var l = z.Schritt(new KlangStadtlebenModell.Momentbild { MarktOffen = true }, 40, 1);
            Pruefe.Gleich(0f, l.Menge);
            Pruefe.Gleich(0f, l.Markt);
            Pruefe.Gleich(0f, l.Schenke);
            Pruefe.Gleich(0f, l.Werkstatt);
            var tag = KlangUmgebungModell.Schichten.ToList().FindIndex(s => s.Name == "stadt_tag");
            var gem = KlangUmgebungModell.Schichten.ToList().FindIndex(s => s.Name == "stadt_gemurmel");
            Pruefe.Wahr(KlangUmgebungModell.Gewicht(gem, 12, 12, 1, 0, l) == 0f, "Gemurmel auf leerer Straße");
            Pruefe.Wahr(KlangUmgebungModell.Gewicht(tag, 12, 12, 1, 0, l) <= KlangUmgebungModell.StadtGrund + 1e-6f, "Stadtleben auf leerer Straße über dem Grund");
        }),

        ("Menschen: Gemurmel wächst mit der Zahl, weich und gesättigt", () =>
        {
            float Menge(int n)
            {
                var m = new KlangStadtlebenModell.Momentbild();
                for (var i = 0; i < n; i++) m.Leute.Add(new KlangStadtlebenModell.Quelle(5 + i % 10, 5 + i / 10));
                return new KlangStadtlebenModell.Zustand().Schritt(m, 40, 1).Menge;
            }
            float vorher = -1;
            foreach (var n in new[] { 0, 1, 2, 4, 8, 16, 32, 64, 200 })
            {
                var v = Menge(n);
                Pruefe.Wahr(v >= vorher, $"Menge bei {n} Leuten sinkt: {v}");
                Pruefe.Wahr(v <= 1f, $"Menge bei {n} Leuten über 1: {v}");
                vorher = v;
            }
            Pruefe.Gleich(0f, Menge(1));
            Pruefe.Wahr(Menge(200) > 0.99f, "Sättigung: 200 Leute nicht voll");
        }),

        ("Menschen: Anstieg 3 s, Abfall 6 s, nie ein Sprung", () =>
        {
            var z = new KlangStadtlebenModell.Zustand();
            var leer = new KlangStadtlebenModell.Momentbild();
            var voll = new KlangStadtlebenModell.Momentbild();
            for (var i = 0; i < 40; i++) voll.Leute.Add(new KlangStadtlebenModell.Quelle(5 + i % 10, 5 + i / 10));
            z.Schritt(leer, 40, 1);
            var vorher = 0f;
            float groesst = 0, t63 = -1;
            for (var t = 0.0; t < 20; t += 1.0 / 60)
            {
                var v = z.Schritt(voll, 40, 1.0 / 60).Menge;
                groesst = Math.Max(groesst, Math.Abs(v - vorher));
                vorher = v;
                if (t63 < 0 && v >= 0.632f * 0.999f * KlangStadtlebenModell.Saettigung(37, 1, 6)) t63 = (float)t;
            }
            Pruefe.Wahr(groesst < 0.01f, $"Sprung im Anstieg: {groesst}");
            Pruefe.Wahr(t63 is > 2f and < 4f, $"Anstieg auf 63 % nach {t63:0.0} s (erwartet etwa 3 s)");
            var vollMenge = vorher;
            float t37 = -1;
            for (var t = 0.0; t < 30; t += 1.0 / 60)
            {
                var v = z.Schritt(leer, 40, 1.0 / 60).Menge;
                if (t37 < 0 && v <= vollMenge * 0.368f) t37 = (float)t;
            }
            Pruefe.Wahr(t37 is > 5f and < 7f, $"Abfall auf 37 % nach {t37:0.0} s (erwartet etwa 6 s)");
        }),

        ("Menschen: der Umkreis wächst mit dem Zoom und kennt keine Sichtbarkeit", () =>
        {
            Pruefe.Wahr(KlangStadtlebenModell.Umkreis(25) < KlangStadtlebenModell.Umkreis(95) && KlangStadtlebenModell.Umkreis(95) < KlangStadtlebenModell.Umkreis(140), "Umkreis wächst nicht");
            var m = new KlangStadtlebenModell.Momentbild();
            for (var i = 0; i < 20; i++) m.Leute.Add(new KlangStadtlebenModell.Quelle(80 + i % 5, i / 5, Seite: 5f)); // weit außerhalb des Bildes
            Pruefe.Gleich(0f, new KlangStadtlebenModell.Zustand().Schritt(m, 25, 1).Menge);
            Pruefe.Wahr(new KlangStadtlebenModell.Zustand().Schritt(m, 140, 1).Menge > 0.3f, "Zoom 140 hört 80 m nicht");
        }),

        ("Menschen: Markt nur bei offenem Markt; Schänke nur mit Gästen und in der Nähe, aus ihrer Richtung", () =>
        {
            var m = new KlangStadtlebenModell.Momentbild { MarktOffen = false };
            for (var i = 0; i < 10; i++) m.Marktleute.Add(new KlangStadtlebenModell.Quelle(5 + i, 5));
            Pruefe.Gleich(0f, new KlangStadtlebenModell.Zustand().Schritt(m, 40, 1).Markt);
            m.MarktOffen = true;
            Pruefe.Wahr(new KlangStadtlebenModell.Zustand().Schritt(m, 40, 1).Markt > 0.7f, "Offener Markt mit 10 Leuten zu leise");
            var s = new KlangStadtlebenModell.Momentbild();
            s.Schenken.Add(new KlangStadtlebenModell.Quelle(-8, 3, -0.7f, 6));
            var l = new KlangStadtlebenModell.Zustand().Schritt(s, 40, 1);
            Pruefe.Wahr(l.Schenke > 0.7f && l.SchenkeSeite < -0.6f, $"Schänke mit Gästen: {l.Schenke} / {l.SchenkeSeite}");
            s.Schenken.Clear();
            s.Schenken.Add(new KlangStadtlebenModell.Quelle(200, 5, 1f, 8));
            Pruefe.Gleich(0f, new KlangStadtlebenModell.Zustand().Schritt(s, 40, 1).Schenke);
        }),

        ("Menschen: Schritte und Hämmern entstehen nur an Gängern und arbeitenden Werkstätten, am Ort der Figur", () =>
        {
            var passant = Array.FindIndex(KlangUmgebungModell.Rufarten, r => r.Name == "Passant");
            var handwerk = Array.FindIndex(KlangUmgebungModell.Rufarten, r => r.Name == "Handwerk");
            List<KlangUmgebungModell.Ruf> Lauf(KlangStadtlebenModell.Momentbild m)
            {
                var z = new KlangUmgebungModell.Zustand();
                var lz = new KlangStadtlebenModell.Zustand();
                var p = new KlangUmgebungModell.RufPlaner(5);
                var alle = new List<KlangUmgebungModell.Ruf>();
                var neu = new List<KlangUmgebungModell.Ruf>();
                var e0 = new KlangUmgebungModell.Eingaben(12 * 60, 12, Reichsgilde.Core.Zeit.Jahreszeit.Fruehling, 40, 140, KlangUmgebungModell.Innenart.Draussen, false, 0);
                for (var t = 0.0; t < 1800; t += 1.0 / 30)
                {
                    var e = e0 with { Leben = lz.Schritt(m, 40, 1.0 / 30) };
                    var a = z.Schritt(e, 1.0 / 30);
                    neu.Clear();
                    p.Schritt(e, a, 1.0 / 30, neu);
                    alle.AddRange(neu);
                }
                return alle;
            }
            var leer = Lauf(new KlangStadtlebenModell.Momentbild { MarktOffen = true });
            Pruefe.Gleich(0, leer.Count(r => r.Art == passant));
            Pruefe.Gleich(0, leer.Count(r => r.Art == handwerk));
            var m = new KlangStadtlebenModell.Momentbild();
            for (var i = 0; i < 3; i++) m.Leute.Add(new KlangStadtlebenModell.Quelle(10 + i, 5, -0.9f, 1, true));
            m.Werkstaetten.Add(new KlangStadtlebenModell.Quelle(60, -20, 0.8f, 2));
            var mit = Lauf(m);
            Pruefe.Wahr(mit.Count(r => r.Art == passant) >= 20, "zu wenige Schritte mit drei Gängern");
            Pruefe.Wahr(mit.Count(r => r.Art == handwerk) >= 8, "zu wenig Hämmern mit einer Werkstatt");
            foreach (var r in mit.Where(r => r.Art == passant)) Pruefe.Wahr(Math.Abs(r.Seite + 0.72f) < 1e-4f, $"Schritte auf Seite {r.Seite}, erwartet −0,72 (Figur links im Bild)");
            foreach (var r in mit.Where(r => r.Art == handwerk)) Pruefe.Wahr(Math.Abs(r.Seite - 0.4f) < 1e-4f && r.Fern, $"Hämmern auf Seite {r.Seite}, erwartet 0,40 fern");
            // Stehende machen keine Schritte
            var steher = new KlangStadtlebenModell.Momentbild();
            for (var i = 0; i < 6; i++) steher.Leute.Add(new KlangStadtlebenModell.Quelle(10 + i, 5, 0, 1, false));
            Pruefe.Gleich(0, Lauf(steher).Count(r => r.Art == passant));
        }),

        ("Menschen: Kernabfragen über einen Spieltag – Schänke nur 8–22 Uhr, hämmernde Werkstatt nur 8–20 Uhr, nur mit Arbeitern", () =>
        {
            var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos },
                Daten.Value, new NeuePartie(Beruf: "Wirt")); // der Spieler als Wirt: es gibt eine Schänke
            var w = sim.Wirtschaft!;
            int werkMinuten = 0, schaenkeMinuten = 0, gaesteMax = 0;
            for (var t = 0; t < 1140 * 2; t++)
            {
                sim.Tick();
                var uhr = sim.Zeit.UhrzeitInMinuten;
                var schaenken = KlangStadtlebenKern.OffeneSchaenken(w, uhr);
                var werk = KlangStadtlebenKern.ArbeitendeWerkstaetten(w, uhr);
                if (uhr < 8 * 60 || uhr >= 22 * 60) Pruefe.Gleich(0, schaenken.Count);
                if (uhr < 8 * 60 || uhr >= 20 * 60) Pruefe.Gleich(0, werk.Count);
                foreach (var (b, n) in schaenken) { Pruefe.Wahr(b.IstSchaenke && n > 0, "Schänke ohne Gast"); gaesteMax = Math.Max(gaesteMax, n); }
                foreach (var (b, n) in werk) Pruefe.Wahr(n > 0 && b.Stillstand is null && KlangStadtlebenKern.LaermBerufe.Contains(b.Artdaten.Beruf), $"{b.Name} hämmert ohne Arbeit");
                if (werk.Count > 0) werkMinuten++;
                if (schaenken.Count > 0) schaenkeMinuten++;
            }
            Console.WriteLine($"          Kern: hämmernde Werkstatt in {werkMinuten} von 2280 Minuten, Schänke mit Gästen in {schaenkeMinuten}, höchstens {gaesteMax} Gäste");
            Pruefe.Wahr(werkMinuten > 0, "In zwei Spieltagen hämmerte nie eine Werkstatt");
            Pruefe.Wahr(schaenkeMinuten > 0, "In zwei Spieltagen saß nie ein Gast in der offenen Schänke");
        }),
    };
}
