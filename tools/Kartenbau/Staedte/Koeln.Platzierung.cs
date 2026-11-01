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

    /// <summary>Wie weit ein Punkt von der Mitte des nächsten Marktplatzes liegt (<see cref="Maerkte"/>; wie die Kartenprüfung).</summary>
    private double ZumMarkt(Kartenort p) =>
        E.Gebaeude.Where(g => g.Katalog == Stadtkarte.MarktKatalog).Select(g => p.Abstand(g.Punkt)).DefaultIfEmpty(p.Abstand(P(0, 0))).Min();

    /// <summary>
    /// Ein Startgrundstück (24 × 14 m: Werkstattplatz 16 × 14 und Wohnhausplatz 8 × 10, vorn bündig) an einer Straße, mit der Vorderseite zur
    /// Straße. Gesucht wird um <paramref name="sHint"/> (Meter entlang der Straße) die freie Stelle, deren Mitte dem Abstand
    /// <paramref name="ziel"/> (ohne Angabe <see cref="ZielAbstand"/>) zum nächsten Marktplatz am nächsten liegt (höchstens <paramref name="fenster"/> Meter von dort). Die Werkstatt liegt auf der Seite des Grundstücks, die näher am
    /// Marktplatz ist (seit den drei Märkten, 31.10.2026: Die Karren fahren vom Werkstatthof los, ihr Weg soll kurz sein).
    /// </summary>
    private void Startgrundstueck(int n, string strasse, bool links, double sHint, double fenster = 12, double? ziel = null)
    {
        var zielAbstand = ziel ?? ZielAbstand;
        var weg = _strassen[strasse];
        var linie = weg.Verlauf;
        var halb = weg.BreiteWirksam / 2;
        (double Wert, double S, Kartenort W, Kartenort H, double D, double Abstand)? beste = null;
        for (var s = sHint - fenster; s <= sHint + fenster; s += 0.5)
        {
            if (Auf(linie, s) is not var (p, tx, tz)) continue;
            var (nx, nz) = links ? (tz, -tx) : (-tz, tx);
            var vorn = P(p.X + nx * (halb + 1), p.Z + nz * (halb + 1));
            var d = Math.Round(Math.Atan2(-nx, -nz) * 180 / Math.PI * 10) / 10;
            // die Werkstatt links oder rechts vom Wohnhaus: die Lage, in der sie dem Marktplatz näher ist
            var (wm, hm) = new[] { 1, -1 }.Select(seite => (Kartengeometrie.Dreh(-4 * seite, -7, d), Kartengeometrie.Dreh(8.1 * seite, -5, d)))
                .Select(x => (W: P(vorn.X + x.Item1.X, vorn.Z + x.Item1.Z), H: P(vorn.X + x.Item2.X, vorn.Z + x.Item2.Z)))
                .OrderBy(x => ZumMarkt(x.W)).First();
            var mitte = P((wm.X + hm.X) / 2, (wm.Z + hm.Z) / 2);
            var abstand = ZumMarkt(mitte);
            var wert = Math.Abs(abstand - zielAbstand) + 0.04 * Math.Abs(s - sHint);
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
        Console.WriteLine($"   S{n}: {strasse} bei {t.S:0.#} m, {t.Abstand:0.#} m vom Markt, Werkstatt bei {t.W}");
    }

    private void Startgrundstuecke()
    {
        // Je Marktplatz Grundstücke an den Straßen, die auf ihn führen (Werkstatt zum Platz hin), je Straßenseite höchstens eins. Die Rheingasse
        // beginnt am Kunibertstor (z = −150), Meter entlang = z + 150; der Malzbüchel an der Severinstraße, die Kirchstraße in Deutz am Nordtor,
        // die Deutzer Freiheit am Brückenkopf. Seit die Plätze am Rhein eine Hausreihe vom Tor abrücken (1.11.2026), liegen die Grundstücke an der
        // Rheingasse 14–15 m weiter zur Stadtmitte (im Süden bis 3 m vor die Bayenstraße; im Norden rücken dafür Trankgasse 9 m und Hafen 6 m nach
        // Süden), und das dritte am Südplatz fährt über den Malzbüchel statt über den Kartäuserwall (der den Platz nicht mehr berührt).
        // Der Spieler 2 m weiter vom Platz (innerhalb der ±10 % der Prüfung): sein Werkstattweg 24 m, 30 Minuten wie die alte feste Fahrzeit (die
        // Häuser der KI 20–24 m wie seit den drei Marktplätzen)
        Startgrundstueck(1, "Rheingasse", links: false, sHint: 224, ziel: ZielAbstand + 2);   // Südplatz, nördlich davon, zur Stadt hin (der Spieler)
        Startgrundstueck(2, "Rheingasse", links: true, sHint: 226);           // Südplatz, nördlich davon, zum Rhein
        Startgrundstueck(3, "Malzbüchel", links: false, sHint: 18);           // Südplatz, westlich davon, zwischen Malzbüchel und Kartäuserwall
        Startgrundstueck(4, "Rheingasse", links: false, sHint: 74);           // Nordplatz, südlich davon, zur Stadt hin
        Startgrundstueck(5, "Rheingasse", links: true, sHint: 74);            // Nordplatz, südlich davon, zum Rhein
        Startgrundstueck(6, "Deutzer Kirchstraße", links: false, sHint: 19);  // Deutz, nördlich des Platzes, westlich der Kirchstraße
        Startgrundstueck(7, "Deutzer Kirchstraße", links: true, sHint: 19);   // Deutz, nördlich des Platzes, östlich der Kirchstraße
        Startgrundstueck(8, "Deutzer Freiheit", links: true, sHint: 12);      // Deutz, westlich des Platzes, nördlich der Freiheit (zum Rhein)
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
        // Seit 1.11.2026 stehen zwischen Mauer und Marktplatz an der Rheingasse (Meter entlang = z + 150) Häuser mit der Front zur Rheingasse: zur
        // Stadt hin eins zwischen Wallstraße und Platz, am Rhein zwei hintereinander – vor den Wallstraßen gesetzt, damit sie an der Rheingasse
        // stehen. Am Heumarkt Einwohnerplätze (das Bild vergibt die Einwohnerhäuser nach dem Abstand zum Platz des Spielers, dort stehen sie immer),
        // am Alter Markt Kulissenhäuser (feste Modelle): Mit weniger Rivalen blieben Einwohnerplätze dort als letzte leer.
        var kulissenAmMarkt = 0;
        foreach (var (links, hoechstens) in new[] { (false, 1), (true, 2) })
            kulissenAmMarkt += Reihe("kulisse_buergerhaus", "Rheingasse", links, 0, 150 - (R - MarktVonMauer), luecke: 1, hoechstens: hoechstens).Count;
        // Altstadt: an den Straßen um die Märkte und an den Wallstraßen (Häuser mit dem Rücken zur Mauer)
        foreach (var (strasse, links, von, bis, hoechstens) in new (string, bool, double, double, int)[]
        {
            ("Rheingasse", false, 150 + R - MarktVonMauer, 300, 1), ("Rheingasse", true, 150 + R - MarktVonMauer, 300, 2),
            ("Trankgasse", true, 0, 200, 3), ("Trankgasse", false, 0, 200, 3),
            ("Hammergasse", true, 0, 200, 2), ("Hammergasse", false, 0, 200, 2), ("Wollgasse", true, 0, 200, 3), ("Wollgasse", false, 0, 200, 3),
            ("Kunibertswall", true, 0, 1000, 5), ("Mauritiuswall", true, 0, 1000, 6), ("Kartäuserwall", true, 0, 1000, 5),
        })
            n += Reihe("einwohnerplatz_ring", strasse, links, von, bis, luecke: 1, hoechstens: hoechstens).Count;
        Console.WriteLine($"   Einwohnerplätze in der Altstadt: {n}");

        // Kulissenhäuser (feste Modelle, kein Bauland): ein paar Häuser in der Altstadt (bis 31.10.2026 an den Märkten der Bayenstraße)
        var k = 0;
        foreach (var (strasse, links, von, bis, hoechstens) in new (string, bool, double, double, int)[]
        {
            ("Bayenstraße", true, 0, 200, 3), ("Rheingasse", true, 100, 200, 2), ("Engelspforte", true, 0, 200, 1),
        })
            k += Reihe("kulisse_buergerhaus", strasse, links, von, bis, luecke: 1, hoechstens: hoechstens).Count;
        Console.WriteLine($"   Kulissenhäuser: {k} und {kulissenAmMarkt} am Alter Markt");

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
