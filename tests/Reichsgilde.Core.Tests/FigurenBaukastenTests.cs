using Reichsgilde;
using Reichsgilde.Core;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Der Figuren-Baukasten (Darstellung, <c>game/scripts/FigurenBaukasten.cs</c>, hier mitübersetzt): Aus den Personendaten des Kerns entsteht ein festes Rezept,
/// die Tracht kommt aus einem Katalog. Gleiche Person → gleiches Rezept (draußen wie drinnen), verschiedene Personen → verschiedene Rezepte,
/// Volk, Geschlecht, Alter, Haar und Rolle werden abgebildet, und alle Wege holen die Tracht aus derselben Stelle.
/// </summary>
static class FigurenBaukastenTests
{
    /// <summary>Alles, was man der Figur ansieht, als Text (das Erbgut über die Gene, die die Darstellung liest).</summary>
    static string Schluessel(FigurRezept r) =>
        $"{r.Kennung}|{r.Nummer}|{r.Volk}|{r.Frau}|{r.Kind}|{r.Grau}|{r.Haar}|{r.Rolle}|{r.Tracht}|" +
        string.Join(",", new[] { "bartwuchs", "bartform", "haar_rot", "haar_helligkeit", "groesse" }.Select(g => r.Erbgut.SichtbarerWert(g)));

    static Einwohner Einwohner(int nummer, Volk volk, Geschlecht geschlecht, int geburtsjahr, string rolle) =>
        new(nummer, Herkunft.Stadt, "", "Anna", "Weber", volk, geschlecht, geburtsjahr, Konfession.Katholisch, rolle);

    static Person Person(int nummer, Volk volk, Geschlecht geschlecht, int geburtsjahr, Haarfarbe haar) =>
        new(nummer, "Grete", "Fugger", volk, geschlecht, geburtsjahr) { Aussehen = new Aussehen(1, haar, default) };

    static readonly Farbwert Hausfarbe = new(0.2f, 0.4f, 0.6f);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Figuren-Baukasten: dieselbe Person ergibt draußen und drinnen dasselbe Rezept (Einwohner als Passant und im Dom, Oberhaupt im Kontor, in der Wohnstube und im Rat)", () =>
        {
            var e = Einwohner(42, Volk.Zwerg, Geschlecht.Frau, 1370, "Krämer");
            Pruefe.Gleich(Schluessel(FigurenBaukasten.Von(e, 1400)), Schluessel(FigurenBaukasten.Von(e, 1400)), "Einwohner");
            var p = Person(7, Volk.Elb, Geschlecht.Frau, 1360, Haarfarbe.Silberblond);
            var kontor = FigurenBaukasten.Von(p, 1400, true, true, Hausfarbe);
            var stube = FigurenBaukasten.Von(p, 1400, true, true, Hausfarbe);
            Pruefe.Gleich(Schluessel(kontor), Schluessel(stube), "Oberhaupt");
            Pruefe.Wahr(ReferenceEquals(p.Erbgut, kontor.Erbgut), "Familienpersonen tragen ihr echtes Erbgut");
            // Ein Arbeiter sieht in der Werkstatt (Zeile der Arbeiterliste) aus wie in Bank, Wache und Diebesnest (Arbeiter des Kerns)
            var a = new Arbeiter(3, "Geselle", 0, 50) { Volk = Volk.Mensch, Geschlecht = Geschlecht.Frau, Aussehen = new Aussehen(2, Haarfarbe.Rot, default), AlterBeimEinstellen = 30 };
            var zeile = new ArbeiterZeile(3, "Geselle", "Eisen", true, 50, 1, null, 0, null, "Ida", Volk.Mensch, Geschlecht.Frau, a.Aussehen, 30, Stufe: 2);
            Pruefe.Gleich(Schluessel(FigurenBaukasten.Von(zeile, "Schmiede")), Schluessel(FigurenBaukasten.Von(a, "Schmiede", null, 0)), "Arbeiter");
        }),

        ("Figuren-Baukasten: verschiedene Personen bekommen verschiedene Kennungen und Figurnummern", () =>
        {
            var kennungen = new List<string>();
            var nummern = new HashSet<int>();
            for (var i = 1; i <= 50; i++)
            {
                foreach (var r in new[]
                {
                    FigurenBaukasten.Von(Einwohner(i, Volk.Mensch, Geschlecht.Mann, 1370, "Knecht"), 1400),
                    FigurenBaukasten.Von(Person(i, Volk.Mensch, Geschlecht.Mann, 1370, Haarfarbe.Braun), 1400, true, false, Hausfarbe),
                    FigurenBaukasten.Von(new ArbeiterZeile(i, "Lehrling", "", true, 50, 1, null, 0, null, Stufe: 1), "Schmiede"),
                })
                {
                    kennungen.Add(r.Kennung);
                    nummern.Add(r.Nummer);
                }
            }
            Pruefe.Gleich(kennungen.Count, kennungen.Distinct().Count(), "Kennungen eindeutig");
            Pruefe.Gleich(kennungen.Count, nummern.Count, "Figurnummern eindeutig");
            var x = FigurenBaukasten.Von(Einwohner(1, Volk.Mensch, Geschlecht.Mann, 1370, "Knecht"), 1400);
            var y = FigurenBaukasten.Von(Einwohner(2, Volk.Mensch, Geschlecht.Mann, 1370, "Knecht"), 1400);
            Pruefe.Wahr(Schluessel(x) != Schluessel(y), "zwei Einwohner sehen verschieden aus");
        }),

        ("Figuren-Baukasten: alle Wege holen die Tracht aus dem einen Katalog – eine Änderung dort wirkt bei Passanten, Familie, Arbeitern, Fuhrleuten und Statisten", () =>
        {
            foreach (var rolle in new[] { "Ratsherr", "Bürgersohn", "Mönch", "Bettler", "Witwer", "Hirte", "Feldarbeiter", "Holzsammler", "Krämer", "Bote", "Torwächter", "Knecht", "Tagelöhner" })
                foreach (var g in new[] { Geschlecht.Mann, Geschlecht.Frau })
                {
                    Pruefe.Gleich(FigurenBaukasten.Tracht(rolle, g == Geschlecht.Frau), FigurenBaukasten.Von(Einwohner(5, Volk.Mensch, g, 1370, rolle), 1400).Tracht, rolle);
                    Pruefe.Gleich(FigurenBaukasten.Tracht(rolle, g == Geschlecht.Frau), FigurenBaukasten.Statist("x", Volk.Mensch, g == Geschlecht.Frau, rolle).Tracht, rolle);
                }
            var p = Person(9, Volk.Mensch, Geschlecht.Mann, 1370, Haarfarbe.Braun);
            Pruefe.Gleich(FigurenBaukasten.Tracht("Oberhaupt", false, Hausfarbe), FigurenBaukasten.Von(p, 1400, true, true, Hausfarbe).Tracht);
            Pruefe.Gleich(Hausfarbe, FigurenBaukasten.Von(p, 1400, true, true, Hausfarbe).Tracht.Gewand, "das Oberhaupt trägt die Farbe seines Hauses");
            Pruefe.Gleich(FigurenBaukasten.Tracht("Familie", false), FigurenBaukasten.Von(p, 1400, true, false, Hausfarbe).Tracht);
            var zeile = new ArbeiterZeile(1, "Vorarbeiter", "", true, 50, 1, null, 0, null, Stufe: 3);
            Pruefe.Gleich(FigurenBaukasten.Tracht("Arbeiter.Vorarbeiter", false), FigurenBaukasten.Von(zeile, "Schmiede").Tracht);
            Pruefe.Wahr(FigurenBaukasten.Von(zeile, "Schmiede").Tracht.Schuerze, "Arbeiter mit Lederschürze");
            var f = new Fuhrmann(Volk.Halbling, Geschlecht.Mann, new Aussehen(0, Haarfarbe.Braun, default), 1234);
            Pruefe.Gleich(FigurenBaukasten.Tracht("Fuhrmann", false), FigurenBaukasten.Von(f).Tracht);
            Pruefe.Gleich(Kopfbedeckung.Strohhut, FigurenBaukasten.Von(f).Tracht.Kopf);
        }),

        ("Figuren-Baukasten: Volk, Geschlecht, Alter und Rolle werden abgebildet (grau ab 55, Kinder kleiner, Orks und Oger nur als Mann)", () =>
        {
            var jung = FigurenBaukasten.Von(Einwohner(1, Volk.Halbling, Geschlecht.Frau, 1380, "Bettler"), 1400);
            Pruefe.Gleich(Volk.Halbling, jung.Volk);
            Pruefe.Wahr(jung.Frau && !jung.Grau, "junge Halblingsfrau");
            Pruefe.Gleich("Bettler", jung.Rolle);
            Pruefe.Wahr(jung.Tracht.Gebeugt, "Bettler gehen gebeugt");
            Pruefe.Wahr(FigurenBaukasten.Von(Einwohner(1, Volk.Mensch, Geschlecht.Mann, 1340, "Knecht"), 1400).Grau, "mit 60 grau");
            Pruefe.Wahr(!FigurenBaukasten.Von(Einwohner(1, Volk.Mensch, Geschlecht.Mann, 1346, "Knecht"), 1400).Grau, "mit 54 nicht grau");
            Pruefe.Wahr(FigurenBaukasten.Von(Person(3, Volk.Mensch, Geschlecht.Mann, 1395, Haarfarbe.Blond), 1400, false, false, Hausfarbe).Kind, "minderjährig: Kind");
            Pruefe.Wahr(!FigurenBaukasten.Statist("o", Volk.Ork, true).Frau, "Orks haben nur das Männermodell");
            Pruefe.Gleich(Kopfbedeckung.Haube, FigurenBaukasten.Statist("w", Volk.Mensch, true).Tracht.Kopf, "Frauen mit Haube");
            var a = new Arbeiter(4, "Lehrling", 0, 50) { Volk = Volk.Mensch, Geschlecht = Geschlecht.Mann, AlterBeimEinstellen = 54 };
            Pruefe.Wahr(FigurenBaukasten.Von(a, "Schmiede", null, Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag * 2).Grau, "der Arbeiter altert mit der Dienstzeit");
        }),

        ("Figuren-Baukasten: Haar aus dem Aussehen der Person, wo es eines gibt, sonst aus den Farb-Genen (nur Farben des Volks)", () =>
        {
            Pruefe.Gleich(Haarfarbe.Rot, FigurenBaukasten.Von(Person(5, Volk.Zwerg, Geschlecht.Mann, 1370, Haarfarbe.Rot), 1400, true, false, Hausfarbe).Haar);
            Pruefe.Gleich(Haarfarbe.Hellblond, FigurenBaukasten.Von(new Fuhrmann(Volk.Mensch, Geschlecht.Frau, new Aussehen(0, Haarfarbe.Hellblond, default), 77)).Haar);
            foreach (var volk in new[] { Volk.Mensch, Volk.Zwerg, Volk.Elb, Volk.Halbling, Volk.Ork, Volk.Oger })
                for (var i = 0; i < 40; i++)
                {
                    var r = FigurenBaukasten.Von(Einwohner(i, volk, Geschlecht.Mann, 1370, "Knecht"), 1400);
                    Pruefe.Wahr(Aussehenspalette.Haarfarben(volk).Contains(r.Haar), $"{volk}: {r.Haar} gehört zur Palette");
                    Pruefe.Gleich(FigurenBaukasten.HaarAusGenen(r.Erbgut, volk), r.Haar, "aus den Genen");
                }
        }),

        ("Figuren-Baukasten: der Bürgermeister trägt die Amtskette und keine Kopfbedeckung – sonst seine eigene Tracht (Hausfarbe bzw. Ratsrot)", () =>
        {
            var p = Person(11, Volk.Mensch, Geschlecht.Mann, 1360, Haarfarbe.Braun);
            var eigene = FigurenBaukasten.Von(p, 1400, true, true, Hausfarbe).Tracht;
            var amt = FigurenBaukasten.Amtstracht(eigene);
            Pruefe.Wahr(amt.Amtskette, "Amtskette");
            Pruefe.Gleich(Kopfbedeckung.Keine, amt.Kopf, "kein Barett");
            Pruefe.Gleich(Hausfarbe, amt.Gewand, "Gewand in der Hausfarbe");
            var stadt = FigurenBaukasten.Amtstracht(FigurenBaukasten.Ratsherr("Ulrich Ratzinger", Volk.Mensch, Geschlecht.Mann, null).Tracht);
            Pruefe.Wahr(stadt.Amtskette && stadt.Kopf == Kopfbedeckung.Keine, "Ratsherr der Stadt als Bürgermeister");
            // Gegenprobe: ohne Amt keine Kette
            Pruefe.Wahr(!eigene.Amtskette && !FigurenBaukasten.Tracht("Ratsherr", false).Amtskette, "ohne Amt keine Kette");
        }),

        ("Figuren-Baukasten: Blick – Plätze blicken bei Drehung 0 nach −Z, das Modell (+Z) bekommt eine halbe Drehung mehr; DrehungZu zeigt zum Ziel", () =>
        {
            const float nord = 0, west = MathF.PI / 2, sued = MathF.PI, ost = -MathF.PI / 2;
            void Richtung(float d, float x, float z)
            {
                var (vx, vz) = FigurenBaukasten.Vorwaerts(d);
                Pruefe.Wahr(MathF.Abs(x - vx) < 1e-5f && MathF.Abs(z - vz) < 1e-5f, $"Platz blickt nach ({x}, {z})");
                // das Modell blickt nach +Z: nach der Modelldrehung zeigt +Z dorthin, wohin der Platz blickt
                var m = FigurenBaukasten.Modelldrehung(d);
                Pruefe.Wahr(MathF.Abs(x - MathF.Sin(m)) < 1e-5f && MathF.Abs(z - MathF.Cos(m)) < 1e-5f, $"Modell blickt nach ({x}, {z})");
            }
            Richtung(nord, 0, -1);
            Richtung(sued, 0, 1);
            Richtung(west, -1, 0);
            Richtung(ost, 1, 0);
            // Wohnstube: das Oberhaupt sitzt nördlich am Tisch und blickt nach Süden auf den Tisch; die Kinder gegenüber nach Norden
            Pruefe.Wahr(FigurenBaukasten.Blickfehler(sued, 0.4f, -0.95f, 0.4f, 0) < 1, "Oberhaupt zum Tisch");
            Pruefe.Wahr(FigurenBaukasten.Blickfehler(nord, -0.3f, 0.95f, -0.3f, 0) < 1, "Kind zum Tisch");
            // Ehefrau am Herd: zum Kessel gedreht
            var herd = FigurenBaukasten.DrehungZu(-2.3f, -1.5f, -3.1f, -2.4f);
            Pruefe.Wahr(FigurenBaukasten.Blickfehler(herd, -2.3f, -1.5f, -3.1f, -2.4f) < 0.01f, "zum Kessel");
            // Gegenprobe: verkehrt herum schlägt die Prüfung an (180°), und „West“ allein war am Herd über 45° daneben
            Pruefe.Wahr(FigurenBaukasten.Blickfehler(nord, 0.4f, -0.95f, 0.4f, 0) > 179, "rückwärts erkannt");
            Pruefe.Wahr(FigurenBaukasten.Blickfehler(west, -2.3f, -1.5f, -3.1f, -2.4f) > 45, "alte Herd-Drehung daneben");
        }),

        ("Figuren-Baukasten: Formschlüssel „Sitzen“ – der Clip Sitzen setzt ihn auf 1, Stehen, Gehen und die übrigen Passanten-Clips (auch SitzenBock) auf 0", () =>
        {
            Pruefe.Gleich(1f, FigurenBaukasten.SitzSchluessel("Sitzen"));
            foreach (var klip in new[] { "Stehen", "Gehen", "Tragen", "Reden", "Hacken", "Buecken", "Kauern", "Betteln", "Ziehen", "Nebenhergehen", "SitzenBock", "" })
                Pruefe.Gleich(0f, FigurenBaukasten.SitzSchluessel(klip), klip);
        }),

        ("Figuren-Baukasten: Vorrücken beim Sitzen – kleine Völker weiter vorn, Menschen, Elben und Oger nicht; Pfad oder Name der Datei", () =>
        {
            foreach (var datei in new[] { "passant_probe", "passant_probe_frau", "passant_probe_elb", "passant_probe_elbin", "passant_probe_oger" })
                Pruefe.Gleich(0f, FigurenBaukasten.VorrueckenFuer(datei, 0.30f), datei);
            foreach (var datei in new[] { "passant_probe_zwerg", "passant_probe_zwergin", "passant_probe_halbling", "passant_probe_halblingin" })
            {
                Pruefe.Wahr(FigurenBaukasten.VorrueckenFuer(datei, 0.30f) > 0.05f, datei + ": tiefer Stuhl");
                // Gegenprobe: je tiefer der Sitz, desto weiter vor; an einer ganz schmalen Kante gar nicht
                Pruefe.Wahr(FigurenBaukasten.VorrueckenFuer(datei, 0.30f) > FigurenBaukasten.VorrueckenFuer(datei, 0.16f), datei + ": flache Bank");
                Pruefe.Gleich(0f, FigurenBaukasten.VorrueckenFuer(datei, 0.05f), datei + ": schmale Kante");
            }
            Pruefe.Gleich(FigurenBaukasten.VorrueckenFuer("passant_probe_zwerg", 0.2f),
                FigurenBaukasten.VorrueckenFuer("res://assets/modelle/figuren/probe/passant_probe_zwerg.glb", 0.2f), "Pfad wie im Spiel");
        }),

        ("Figuren-Baukasten: fest – Figurnummer und Erbgut hängen nur an der Kennung, in jedem Lauf gleich", () =>
        {
            Pruefe.Gleich(FigurenBaukasten.Figurnummer("P12"), FigurenBaukasten.Figurnummer("P12"));
            var n = FigurenBaukasten.Figurnummer("A:Schmiede Ulm:3");
            Pruefe.Wahr(n >= 2_000_000 && n < 52_000_000, "Bereich der Figurnummern");
            // FNV-1a über „P1“, in 50 000 000 gefaltet: ein fester Wert, unabhängig von Laufzeit und System
            Pruefe.Gleich(2_000_000 + (int)(unchecked((((2166136261u ^ 'P') * 16777619u) ^ '1') * 16777619u) % 50_000_000u), FigurenBaukasten.Figurnummer("P1"));
            var e1 = FigurenBaukasten.ErbgutFuer(Volk.Zwerg, 123);
            var e2 = FigurenBaukasten.ErbgutFuer(Volk.Zwerg, 123);
            foreach (var gen in new[] { "bartwuchs", "bartform", "haar_rot", "haar_helligkeit" })
                Pruefe.Gleich(e1.SichtbarerWert(gen), e2.SichtbarerWert(gen), gen);
        }),
    };
}
