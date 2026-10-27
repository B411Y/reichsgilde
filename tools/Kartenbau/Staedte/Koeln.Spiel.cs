using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Kartenbau.Staedte;

// Köln: Umland (Betriebe, Vorkommen), Natur, Boden und die Startbelegung.

public sealed partial class Koeln
{
    /// <summary>Die acht Häuser: der Spieler auf dem ersten Startgrundstück, die KI-Häuser auf den übrigen (Volk, Werkstatt, Umlandbetrieb).</summary>
    private static readonly (string Name, Volk Volk, Geschlecht Geschlecht, Betriebsart Werkstatt, Betriebsart Umland)[] KiHaeuser =
    {
        ("Haus Overstolz", Volk.Zwerg, Geschlecht.Mann, Betriebsart.Giesserei, Betriebsart.Mine),
        ("Haus Hardevust", Volk.Elb, Geschlecht.Frau, Betriebsart.Tischlerei, Betriebsart.Holzfaellerei),
        ("Haus Lyskirchen", Volk.Halbling, Geschlecht.Mann, Betriebsart.Maurerei, Betriebsart.Steinbruch),
        ("Haus Gryn", Volk.Mensch, Geschlecht.Frau, Betriebsart.Tinkturei, Betriebsart.Kraeutergarten),
        ("Haus Scherfgin", Volk.Mensch, Geschlecht.Mann, Betriebsart.Giesserei, Betriebsart.Mine),
        ("Haus Mommersloch", Volk.Halbling, Geschlecht.Frau, Betriebsart.Schaenke, Betriebsart.Bauernhof),
        ("Haus Kleingedank", Volk.Mensch, Geschlecht.Mann, Betriebsart.Tischlerei, Betriebsart.Holzfaellerei),
    };

    /// <summary>
    /// Die Umlandbetriebe: Baustein, Straße, Meter entlang der Straße, Seite (links in Laufrichtung) und – für eine Liegenschaft aus
    /// <c>wirtschaft.json</c> – ihr Name; ohne Namen gehört der Betrieb dem nächsten KI-Haus der passenden Art.
    /// </summary>
    private static readonly (string Baustein, string Strasse, double S, bool Links, string? Liegenschaft)[] UmlandBetriebe =
    {
        ("kraeutergarten", "Aachener Straße", 95, true, "Kräutergarten am Bach"),
        ("bauernhof", "Aachener Straße", 150, false, null),
        ("kraeutergarten", "Aachener Straße", 190, true, null),
        ("holzschlag", "Venloer Straße", 260, true, "Holzschlag im Eichenforst"),
        ("holzschlag", "Venloer Straße", 150, false, null),
        ("bauernhof", "Neusser Straße", 38, false, "Bauernhof an der Mühle"),
        ("steinbruch", "Luxemburger Straße", 215, true, "Steinbruch am Felsenhang"),
        ("steinbruch", "Luxemburger Straße", 250, false, null),
        ("duftgarten", "Bonner Straße", 75, true, "Duftgarten am Lavendelhang"),
        ("wachturm", "Bonner Straße", 95, false, "Wachturm an der Landstraße"),
        ("grube", "Bensberger Straße", 45, false, "Grube am Silberberg"),
        ("grube", "Bensberger Straße", 70, true, null),
        ("grube", "Mülheimer Weg", 85, false, "Grube im Tannenwald"),
        ("grube", "Mülheimer Weg", 125, false, null),
        ("klosterhof", "Siegburger Straße", 75, true, "Klosterhof am Osthang"),
        ("holzschlag", "Siegburger Straße", 120, false, null),
    };

    private readonly List<EntwurfGebaeude> _umland = new();

    /// <summary>Die Arbeitsstellen der Einwohner draußen: Baustein, Kennung und Name wie in der Greybox, Straße, Seite und Meter, ab denen gesucht wird.</summary>
    private static readonly (string Baustein, string Kennung, string Name, string Strasse, bool Links, double Ab)[] Stellen =
    {
        ("weide", "StelleWeide", "Weide", "Venloer Straße", true, 112),
        ("feld", "StelleFeld", "Feld", "Venloer Straße", false, 112),
        ("sammelwald", "StelleWald", "Wald", "Aachener Straße", false, 112),
    };

    // ── Umland ───────────────────────────────────────────────────────────────

    private void Umland()
    {
        var mine = 0; var holz = 0; var stein = 0;
        foreach (var (baustein, strasse, s, links, liegenschaft) in UmlandBetriebe)
        {
            var weg = _strassen[strasse];
            var tiefe = E.Katalog.Finde(baustein)!.Groesse![1];
            if (Auf(weg.Verlauf, s) is not var (p, tx, tz)) throw new InvalidOperationException($"{baustein}: {strasse} ist keine {s} m lang.");
            var (nx, nz) = links ? (tz, -tx) : (-tz, tx);
            var abstand = weg.BreiteWirksam / 2 + 5 + tiefe / 2;
            var mitte = P(p.X + nx * abstand, p.Z + nz * abstand);
            var d = Math.Round(Math.Atan2(-nx, -nz) * 180 / Math.PI * 10) / 10;
            var b = E.Katalog.Finde(baustein)!.Groesse!;
            if (!Frei(Rechteck(mitte, b[0] + 4, b[1] + 4, d), 2, 5))
            {
                Console.WriteLine($"   !!! {baustein} an der {strasse} bei {s} m: keine freie Stelle ({mitte}).");
                continue;
            }
            var g = E.Setzen(baustein, mitte.X, mitte.Z, d);
            if (liegenschaft is not null) { E.Umbenennen(g.Kennung, liegenschaft); E.BetriebSetzen(g.Kennung, liegenschaft); }
            else E.Umbenennen(g.Kennung, E.Katalog.Finde(baustein)!.Anzeigename);
            _umland.Add(g);
            switch (baustein)
            {
                case "grube": E.VorkommenHinzufuegen($"M{++mine}", "Mine", mitte); break;
                case "holzschlag": E.VorkommenHinzufuegen($"Hz{++holz}", "Holzschlag", mitte); break;
                case "steinbruch": E.VorkommenHinzufuegen($"St{++stein}", "Steinbruch", mitte); break;
            }
        }
    }

    /// <summary>Die Arbeitsstellen der Einwohner (Weide, Feld, Wald): die erste freie Stelle ab einer Straßenlänge, die Vorderseite (das Gatter) zur Straße.</summary>
    private void Arbeitsstellen()
    {
        foreach (var (baustein, kennung, name, strasse, links, ab) in Stellen)
        {
            var weg = _strassen[strasse];
            var b = E.Katalog.Finde(baustein)!.Groesse!;
            for (var s = ab; s < Laenge(weg.Verlauf) - 20; s += 2)
            {
                if (Auf(weg.Verlauf, s) is not var (p, tx, tz)) break;
                var (nx, nz) = links ? (tz, -tx) : (-tz, tx);
                var mitte = P(p.X + nx * (weg.BreiteWirksam / 2 + 6 + b[1] / 2), p.Z + nz * (weg.BreiteWirksam / 2 + 6 + b[1] / 2));
                var d = Math.Round(Math.Atan2(-nx, -nz) * 180 / Math.PI * 10) / 10;
                if (!Frei(Rechteck(mitte, b[0] + 4, b[1] + 4, d), 2, 5)) continue;
                var g = E.Setzen(baustein, mitte.X, mitte.Z, d);
                E.Umbenennen(g.Kennung, name);
                E.KennungSetzen(g.Kennung, kennung);
                break;
            }
        }
    }

    // ── Natur ────────────────────────────────────────────────────────────────

    private void Natur()
    {
        // Wälder: Ville im Westen, Königsforst im Osten, Forst bei Deutz, Tannenwald an der Mülheimer Straße
        E.NaturflaecheAnlegen("wald", new[] { P(-699, -300), P(-360, -230), P(-352, 130), P(-380, 330), P(-699, 520) }, "Ville");
        E.NaturflaecheAnlegen("wald", new[] { P(300, -150), P(450, -130), P(470, 250), P(300, 260) }, "Königsforst");
        E.NaturflaecheAnlegen("wald", new[] { P(190, 60), P(300, 40), P(300, 200), P(225, 195) }, "Forst bei Deutz");
        E.NaturflaecheAnlegen("wald", new[] { P(168, -199), P(300, -199), P(300, -100), P(192, -112) }, "Tannenwald");
        // Wiesen und Hecken um die Stadt (nicht innerhalb der Mauer)
        E.NaturflaecheAnlegen("streu", new[] { P(-399, -199), P(299, -199), P(299, 249), P(-399, 249) }, "Wiesen der Kölner Bucht");
        // Äcker der Kölner Bucht
        foreach (var (name, ecken, richtung) in new[]
        {
            ("Feld Aachen", new[] { P(-340, 30), P(-210, 28), P(-205, 100), P(-335, 110) }, 85.0),
            ("Feld Venlo", new[] { P(-330, -70), P(-200, -72), P(-195, -10), P(-325, -12) }, 80.0),
            ("Feld Neuss", new[] { P(-130, -195), P(-30, -195), P(-35, -155), P(-120, -158) }, 10.0),
            ("Feld Bonn", new[] { P(-90, 160), P(-20, 165), P(-25, 240), P(-95, 235) }, 5.0),
            ("Deutzer Feld", new[] { P(125, 60), P(210, 60), P(215, 150), P(130, 160) }, 0.0),
            ("Feld Mülheim", new[] { P(75, -195), P(140, -195), P(140, -120), P(80, -125) }, 0.0),
        })
            E.FeldAnlegen(ecken, name, richtung);
    }

    // ── Boden ────────────────────────────────────────────────────────────────

    private void Boden()
    {
        // Sand und Schlamm am Rheinufer (links durchgehend, rechts mit der Breite des Flusses)
        Textur(Bodentextur.Sand, new[] { P(56.5, -499), P(56.5, 549) }, radius: 4, staerke: 0.9, kante: 0.5, deko: false);
        Textur(Bodentextur.Schlamm, new[] { P(58.5, -499), P(58.5, 549) }, radius: 2.5, staerke: 0.8, kante: 0.5, deko: false);
        // Kies am Hafen
        TexturFlaeche(Bodentextur.Kies, 42, -100, 56, -52, radius: 4);
        // Fels an den Gruben
        foreach (var g in _umland.Where(g => g.Katalog == "grube"))
            Textur(Bodentextur.Fels, new[] { P(g.X, g.Z) }, radius: 12, staerke: 0.8, kante: 0.7);
    }

    // ── Start ────────────────────────────────────────────────────────────────

    private void Start()
    {
        E.SpielerWohnhausSetzen("S1_Wohnhaus");
        E.SpielerFarbeSetzen("#336bb8");
        E.SpielerStartplatzSetzen("S1_Werkstatt", null, "{art} an der Hohen Straße");

        var frei = _umland.Where(g => g.Betrieb is null).ToList();
        for (var i = 0; i < KiHaeuser.Length; i++)
        {
            var h = KiHaeuser[i];
            var haus = E.HausHinzufuegen(h.Name);
            E.HausVolkSetzen(haus, h.Volk);
            E.HausGeschlechtSetzen(haus, h.Geschlecht);
            E.HausWohnhausSetzen(haus, $"S{i + 2}_Wohnhaus");
            E.BetriebHinzufuegen(haus, $"S{i + 2}_Werkstatt", h.Werkstatt);
            var umland = frei.FirstOrDefault(g => E.Katalog.Finde(g.Katalog)?.BetriebsartWert == h.Umland);
            if (umland is null) { Console.WriteLine($"   !!! {h.Name}: kein Umlandbetrieb {h.Umland}"); continue; }
            frei.Remove(umland);
            E.BetriebHinzufuegen(haus, umland.Kennung, h.Umland);
        }
    }
}
