using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Kartenbau.Staedte;

// Köln: Startgrundstücke, Einwohnerplätze und Umlandbetriebe an den Straßen (mit Abstandsprüfung), Kreuzungen der Wege.

public sealed partial class Koeln
{
    // ── Geometrie-Hilfen ─────────────────────────────────────────────────────

    /// <summary>Das Rechteck <paramref name="breite"/> × <paramref name="tiefe"/> um <paramref name="mitte"/>, gedreht wie ein Gebäude.</summary>
    private static List<Kartenort> Rechteck(Kartenort mitte, double breite, double tiefe, double drehung)
    {
        var ecken = new List<Kartenort>();
        foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
        {
            var d = Kartengeometrie.Dreh(sx * breite / 2, sz * tiefe / 2, drehung);
            ecken.Add(P(mitte.X + d.X, mitte.Z + d.Z));
        }
        return ecken;
    }

    /// <summary>Der Punkt der Linie bei Länge <paramref name="s"/> und die Richtung dort (Einheitsvektor); null außerhalb der Linie.</summary>
    private static (Kartenort Punkt, double Tx, double Tz)? Auf(IReadOnlyList<Kartenort> linie, double s)
    {
        if (s < 0) return null;
        for (var i = 0; i + 1 < linie.Count; i++)
        {
            var l = linie[i].Abstand(linie[i + 1]);
            if (l < 1e-9) continue;
            if (s <= l)
            {
                var (tx, tz) = ((linie[i + 1].X - linie[i].X) / l, (linie[i + 1].Z - linie[i].Z) / l);
                return (P(linie[i].X + tx * s, linie[i].Z + tz * s), tx, tz);
            }
            s -= l;
        }
        return null;
    }

    private static double Laenge(IReadOnlyList<Kartenort> linie)
    {
        var l = 0.0;
        for (var i = 0; i + 1 < linie.Count; i++) l += linie[i].Abstand(linie[i + 1]);
        return l;
    }

    /// <summary>
    /// Steht an dieser Stelle nichts im Weg? Abstand zu Gebäuden (<paramref name="abstand"/>), zu Wegen (halbe Breite + 0,3 m), zu Mauern (4 m)
    /// und zum Wasser (3 m).
    /// </summary>
    private bool Frei(IReadOnlyList<Kartenort> umriss, double abstand = 1.5, double wasserAbstand = 3)
    {
        foreach (var g in E.Gebaeude)
        {
            var e = g.Ecken();
            if (e.Count > 0 && Kartengeometrie.AbstandVielecke(umriss, e) < abstand) return false;
        }
        foreach (var w in E.Wege)
            if (Kartengeometrie.AbstandVielecke(umriss, w.Verlauf, false) < w.BreiteWirksam / 2 + 0.3) return false;
        foreach (var m in E.Mauern)
            if (Kartengeometrie.AbstandVielecke(umriss, m.Ecken, m.Geschlossen) < 4) return false;
        if (E.Wasserform.Ueberlappt(umriss) is not null) return false;
        foreach (var p in umriss.Append(Kartengeometrie.Mitte(umriss)))
            if (E.Wasserform.Uferabstand(p.X, p.Z, 8) < wasserAbstand) return false;
        return true;
    }

    // ── Startgrundstücke ─────────────────────────────────────────────────────

    private readonly List<Kartenort> _startMitten = new();

    /// <summary>
    /// Ein Startgrundstück (24 × 14 m: Werkstattplatz 16 × 14 und Wohnhausplatz 8 × 10, vorn bündig) an einer Straße, mit der Vorderseite zur
    /// Straße. Gesucht wird um <paramref name="sHint"/> (Meter entlang der Straße) die freie Stelle, deren Mitte dem Abstand
    /// <see cref="ZielAbstand"/> zum Markt am nächsten liegt.
    /// </summary>
    private void Startgrundstueck(int n, string strasse, bool links, double sHint)
    {
        var weg = _strassen[strasse];
        var linie = weg.Verlauf;
        var halb = weg.BreiteWirksam / 2;
        (double Wert, double S, Kartenort W, Kartenort H, double D, double Abstand)? beste = null;
        for (var s = sHint - 45; s <= sHint + 45; s += 0.5)
        {
            if (Auf(linie, s) is not var (p, tx, tz)) continue;
            var (nx, nz) = links ? (tz, -tx) : (-tz, tx);
            var vorn = P(p.X + nx * (halb + 1), p.Z + nz * (halb + 1));
            var d = Math.Round(Math.Atan2(-nx, -nz) * 180 / Math.PI * 10) / 10;
            var (w, h) = (Kartengeometrie.Dreh(-4, -7, d), Kartengeometrie.Dreh(8.1, -5, d));
            var (wm, hm) = (P(vorn.X + w.X, vorn.Z + w.Z), P(vorn.X + h.X, vorn.Z + h.Z));
            var mitte = P((wm.X + hm.X) / 2, (wm.Z + hm.Z) / 2);
            var abstand = mitte.Abstand(P(0, 0));
            var wert = Math.Abs(abstand - ZielAbstand) + 0.04 * Math.Abs(s - sHint);
            if (beste is { } b && wert >= b.Wert) continue;
            if (!Frei(Rechteck(wm, 16, 14, d)) || !Frei(Rechteck(hm, 8, 10, d))) continue;
            beste = (wert, s, wm, hm, d, abstand);
        }
        if (beste is not { } t) throw new InvalidOperationException($"Startgrundstück {n} an der {strasse}: keine freie Stelle bei {sHint} m.");
        var werkstatt = E.Setzen("werkstattplatz", t.W.X, t.W.Z, t.D);
        var wohnhaus = E.Setzen("wohnhausplatz", t.H.X, t.H.Z, t.D);
        E.KennungSetzen(werkstatt.Kennung, $"S{n}_Werkstatt");
        E.KennungSetzen(wohnhaus.Kennung, $"S{n}_Wohnhaus");
        E.Umbenennen($"S{n}_Werkstatt", $"Werkstatt {strasse}");
        E.Umbenennen($"S{n}_Wohnhaus", $"Wohnhaus {strasse}");
        E.LageSetzen($"S{n}_Wohnhaus", Lage.Mauerring);
        _startMitten.Add(P((t.W.X + t.H.X) / 2, (t.W.Z + t.H.Z) / 2));
        Console.WriteLine($"   S{n}: {strasse} bei {t.S:0.#} m, {t.Abstand:0.#} m vom Markt");
    }

    private void Startgrundstuecke()
    {
        Startgrundstueck(1, "Hohe Straße", links: true, sHint: 36);        // Nord, östlich der Hohen Straße
        Startgrundstueck(2, "Rheingasse", links: false, sHint: 62);        // Nordost, westlich der Rheingasse
        Startgrundstueck(3, "Rheingasse", links: false, sHint: 242);       // Südost
        Startgrundstueck(4, "Severinstraße", links: true, sHint: 95);      // Süd
        Startgrundstueck(5, "Hohe Straße", links: false, sHint: 215);      // Südsüdwest, westlich der Hohen Straße
        Startgrundstueck(6, "Weyerstraße", links: true, sHint: 40);        // Südwest
        Startgrundstueck(7, "Hahnenstraße", links: false, sHint: 48);      // West
        Startgrundstueck(8, "Ehrenstraße", links: true, sHint: 50);        // Nordwest
    }

    // ── Reihen von Häusern an einer Straße ───────────────────────────────────

    /// <summary>
    /// Plätze nebeneinander an einer Straße, mit der Vorderseite zur Straße, soweit Platz ist (<see cref="Frei"/>): von <paramref name="von"/> bis
    /// <paramref name="bis"/> Meter entlang des Weges, <paramref name="luecke"/> Meter dazwischen, höchstens <paramref name="hoechstens"/>.
    /// Gibt die gesetzten Gebäude zurück.
    /// </summary>
    private List<EntwurfGebaeude> Reihe(string katalog, string strasse, bool links, double von, double bis, double luecke = 1, int hoechstens = int.MaxValue,
        double abstandZurStrasse = 0.8)
    {
        var weg = _strassen[strasse];
        var linie = weg.Verlauf;
        var halb = weg.BreiteWirksam / 2;
        var b = E.Katalog.Finde(katalog)?.Groesse ?? throw new ArgumentException($"Der Baustein „{katalog}“ hat keine Größe.", nameof(katalog));
        var liste = new List<EntwurfGebaeude>();
        for (var s = von + b[0] / 2; s <= Math.Min(bis, Laenge(linie)) - b[0] / 2 && liste.Count < hoechstens; s += 0.5)
        {
            if (Auf(linie, s) is not var (p, tx, tz)) break;
            var (nx, nz) = links ? (tz, -tx) : (-tz, tx);
            var d = Math.Round(Math.Atan2(-nx, -nz) * 180 / Math.PI * 10) / 10;
            var abstand = halb + abstandZurStrasse + b[1] / 2;
            var mitte = P(p.X + nx * abstand, p.Z + nz * abstand);
            if (!Frei(Rechteck(mitte, b[0], b[1], d))) continue;
            liste.Add(E.Setzen(katalog, mitte.X, mitte.Z, d));
            s += b[0] / 2 + luecke - 0.5;
        }
        return liste;
    }

    // ── Einwohnerplätze ──────────────────────────────────────────────────────

    private void Einwohnerplaetze()
    {
        // Das Spiel ordnet die Plätze nach ihrer Lage: Plätze innerhalb einer Mauer (die Altstadt und Deutz zählen beide) bekommen zuerst die
        // Ratsherren- und Bürgerhäuser, nach dem Abstand zum Markt; die übrigen Häuser stehen auf den Plätzen vor den Mauern (Vorstadt).
        var n = 0;
        // Altstadt: an den Straßen um die Märkte und an den Wallstraßen (Häuser mit dem Rücken zur Mauer)
        foreach (var (strasse, links, von, bis, hoechstens) in new (string, bool, double, double, int)[]
        {
            ("Trankgasse", true, 0, 200, 3), ("Trankgasse", false, 0, 200, 3),
            ("Cäcilienstraße", true, 0, 200, 2), ("Cäcilienstraße", false, 0, 200, 2), ("Wollgasse", true, 0, 200, 3), ("Wollgasse", false, 0, 200, 3),
            ("Kunibertswall", true, 0, 1000, 5), ("Mauritiuswall", true, 0, 1000, 6), ("Kartäuserwall", true, 0, 1000, 5),
        })
            n += Reihe("einwohnerplatz_ring", strasse, links, von, bis, luecke: 1, hoechstens: hoechstens).Count;
        Console.WriteLine($"   Einwohnerplätze in der Altstadt: {n}");

        // Kulissenhäuser (feste Modelle, kein Bauland): ein paar Häuser an den Märkten
        var k = 0;
        foreach (var (strasse, links, von, bis, hoechstens) in new (string, bool, double, double, int)[]
        {
            ("Bayenstraße", true, 0, 200, 3), ("Rheingasse", true, 100, 200, 2), ("Domkloster", true, 0, 200, 1),
        })
            k += Reihe("kulisse_buergerhaus", strasse, links, von, bis, luecke: 1, hoechstens: hoechstens).Count;
        Console.WriteLine($"   Kulissenhäuser: {k}");

        // Deutz (innerhalb der Mauer von Deutz) und die Vorstädte vor den Toren: die kleinen Plätze
        n = 0;
        foreach (var (strasse, links) in new[] { ("Deutzer Freiheit", true), ("Deutzer Freiheit", false), ("Deutzer Kirchstraße", true), ("Deutzer Kirchstraße", false) })
            n += Reihe("einwohnerplatz_vorstadt", strasse, links, 0, 1000, luecke: 1.5, hoechstens: 3).Count;
        Console.WriteLine($"   Einwohnerplätze in Deutz: {n}");
        n = 0;
        foreach (var strasse in new[] { "Aachener Straße", "Venloer Straße", "Neusser Straße", "Luxemburger Straße", "Bonner Straße" })
            foreach (var links in strasse == "Neusser Straße" ? new[] { true } : new[] { true, false })   // an der Neusser Straße endet das Spielfeld bald
                n += Reihe("einwohnerplatz_vorstadt", strasse, links, 12, 80, luecke: 1.5, hoechstens: 3).Count;
        Console.WriteLine($"   Einwohnerplätze vor den Toren: {n}");
    }

    // ── Kreuzungen ───────────────────────────────────────────────────────────

    /// <summary>
    /// Wo sich zwei Wege kreuzen oder einer am anderen endet, bekommt jeder einen gemeinsamen Stützpunkt (Wegenetz, Karren, Gassennetz: so
    /// ist jede Kreuzung eindeutig). Nur Karren- und Fußwege untereinander, keine Brücke über Wasser.
    /// </summary>
    private void KreuzungenVerbinden()
    {
        var wege = E.Wege.ToList();
        var treffer = new List<(EntwurfWeg A, EntwurfWeg B, Kartenort Ort)>();
        for (var i = 0; i < wege.Count; i++)
            for (var j = i + 1; j < wege.Count; j++)
            {
                var (a, b) = (wege[i].Verlauf, wege[j].Verlauf);
                for (var s = 0; s + 1 < a.Count; s++)
                    for (var t = 0; t + 1 < b.Count; t++)
                        if (Kartengeometrie.Schnitt(a[s], a[s + 1], b[t], b[t + 1]) is { } ort
                            && !treffer.Any(x => x.A == wege[i] && x.B == wege[j] && x.Ort.Abstand(ort) < 1))
                            treffer.Add((wege[i], wege[j], ort));
            }
        foreach (var (a, b, ort) in treffer)
            foreach (var w in new[] { a, b })
                if (w.Punkte.All(p => p.Abstand(ort) > 0.6))
                    E.StuetzpunktEinfuegenBei(w, ort.X, ort.Z);
    }
}
