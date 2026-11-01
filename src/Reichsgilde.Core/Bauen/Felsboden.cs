using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Der Boden um den Fels (Felspinsel F6)
//
//  Ein Felsstück soll nicht hart auf der Wiese stehen. Wie am echten Hang (Konzeptbild 6): Fels → Moos → Schutt/Kies → Wiese. Darum malt der
//  Felspinsel beim Streuen und Stempeln den Boden mit – in denselben Befehl wie die Stücke (ein Schritt bei Rückgängig), in die vorhandenen
//  Kanäle des Bodenrasters (Fels, Kies, Moos; kein neuer Kanal):
//    • Fels unter der Grundfläche (Ellipse aus Breite × Tiefe, gedreht wie das Stück) und bis <see cref="Felsboden.FelsRand"/> darüber hinaus.
//    • Schuttkegel hangabwärts: Kies, so lang wie das Stück hoch ist (mal <see cref="Felsboden.KegelJeHoehe"/>, am steilen Hang länger), unten
//      breiter, mit ausgefranstem Ende. Auf ebenem Boden (unter Felspinsel.HangAb) statt des Kegels ein schmaler Kiesring um den Fuß.
//    • Moossaum um den Fuß (bis <see cref="Felsboden.MoosBreite"/>), am stärksten am oberen Rand (hangaufwärts) und auf der Nordseite (−z),
//      wo im Mittel des Tages Schatten ist – dieselbe Regel wie das Moos im Fels-Material.
//  Je Punkt zählt in einem Strich das stärkste Gewicht jeder der drei Texturen (über alle Stücke des Strichs); der neue Wert wird jedes Mal aus
//  dem Stand vor dem Strich gerechnet: erst Moos, dann Kies, zuletzt Fels darüber. So ist das Ergebnis unabhängig von der Reihenfolge der Stücke
//  und vom mehrfachen Überfahren, und gleich auf jedem Rechner (nur +, −, ·, /, √ und DetMathe; Rauschen aus ganzen Zahlen).
//  Die Dekodichte bleibt, wie sie war. Schuttbrocken selbst malen nichts (sie sind der Schutt). Der Radierer der Felsen lässt den Boden (dafür
//  ist der Radierer des Texturpinsels da); Verschieben und Drehen eines Stücks auch.
//
//  Dazu die Schuttsteine: kleine Steine der Form „schuttbrocken“ im Kegel, nicht gespeichert, sondern aus Lage und Form des Stücks gerechnet
//  (wie die Deko der Bodentexturen) – die Darstellung zeichnet sie mit dem Fels-Material des Stücks.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Was der Boden von einem Felsstück braucht: Form, Mitte, Drehung (Grad) und Maßstab.</summary>
public readonly record struct Felsfuss(string Form, double X, double Z, double Drehung, double Massstab)
{
    public static Felsfuss Von(Felslage l) => new(l.Form, l.X, l.Z, l.Drehung, l.Massstab);
    public static Felsfuss Von(EntwurfFels f) => new(f.Form, f.X, f.Z, f.Drehung, f.Massstab);
    public static Felsfuss Von(Felsstueck f) => new(f.Form, f.Punkt is { Length: 2 } p ? p[0] : 0, f.Punkt is { Length: 2 } q ? q[1] : 0, f.Drehung, f.Massstab);
}

/// <summary>Ein Schuttstein vor einem Felsstück (Form „schuttbrocken“): Ort, Drehung (Grad), Maßstab.</summary>
public readonly record struct Schuttstein(double X, double Z, double Drehung, double Massstab);

/// <summary>Die Rechnungen für den Boden um den Fels (siehe Kopf der Datei).</summary>
public static class Felsboden
{
    /// <summary>So weit reicht der Fels über die Grundfläche hinaus (Meter, weich auslaufend).</summary>
    public const double FelsRand = 0.6;

    /// <summary>Breite des Moossaums außerhalb der Grundfläche (Meter, Plan: 0,5–1 m); er beginnt <see cref="MoosInnen"/> innerhalb.</summary>
    public const double MoosBreite = 0.9, MoosInnen = 0.4;

    /// <summary>Wie stark Moos, Kies und Fels höchstens decken (Anteil; Fels ganz).</summary>
    public const double MoosStaerke = 0.85, KiesStaerke = 0.9;

    /// <summary>Länge des Schuttkegels je Meter Höhe des Stücks; kürzester und längster Kegel (Meter).</summary>
    public const double KegelJeHoehe = 1.3, KegelMin = 1.2, KegelMax = 14;

    /// <summary>Schuttsteine je Meter Höhe des Stücks und höchstens je Stück.</summary>
    public const double SteineJeHoehe = 1.4;
    public const int SteineMax = 9;

    /// <summary>Form, die selbst Schutt ist und nichts malt.</summary>
    public const string Schuttform = "schuttbrocken";

    /// <summary>Malt dieses Stück Boden (alle Formen außer dem Schuttbrocken, und nur bekannte)?</summary>
    public static bool Malt(string form) => form != Schuttform && Felsformen.Finde(form) is not null;

    /// <summary>Die Lage eines Stücks für den Boden: Halbachsen der Grundfläche, Drehung, Hang, Kegel.</summary>
    private readonly struct Lage
    {
        public readonly double X, Z, Rb, Rt, C, S, Hoehe, Kegel, Gx, Gz, Rdir, Rquer, Reichweite;
        public readonly bool Hang;

        public Lage(Felsfuss f, Felsform form, Hoehenraster hoehen)
        {
            (X, Z) = (f.X, f.Z);
            (Rb, Rt) = (form.Breite * f.Massstab / 2, form.Tiefe * f.Massstab / 2);
            var t = f.Drehung * Math.PI / 180;
            (C, S) = (DetMathe.Cos(t), DetMathe.Sin(t));
            Hoehe = form.Hoehe * f.Massstab;
            var (hx, hz) = Felsstreuung.Hang(hoehen, f.X, f.Z);
            var steigung = Pinselfilter.Betrag(hx, hz);
            Hang = steigung >= Felspinsel.HangAb;
            var laenge = Math.Sqrt(hx * hx + hz * hz);
            (Gx, Gz) = Hang ? (-hx / laenge, -hz / laenge) : (0, 0); // hangabwärts, Länge 1
            Kegel = Math.Clamp(KegelJeHoehe * Hoehe * Math.Clamp(0.6 + steigung / 50, 0.6, 1.4), KegelMin, KegelMax);
            Rdir = Hang ? RadiusIn(Gx, Gz) : 0;
            Rquer = Hang ? RadiusIn(-Gz, Gx) : 0;
            Reichweite = Math.Max(Rb, Rt) + Math.Max(Hang ? Kegel + 1 : 0.35 * Hoehe + 0.6, MoosBreite) + 1;
        }

        /// <summary>Weltversatz → Achsen des Stücks (Umkehrung von Kartengeometrie.Dreh).</summary>
        public (double U, double V) Lokal(double dx, double dz) => (dx * C - dz * S, dx * S + dz * C);

        /// <summary>Der Radius der Grundfläche in einer Weltrichtung (Länge 1).</summary>
        public double RadiusIn(double ux, double uz)
        {
            var (u, v) = Lokal(ux, uz);
            var q = u / Rb * (u / Rb) + v / Rt * (v / Rt);
            return q <= 0 ? 0 : 1 / Math.Sqrt(q);
        }
    }

    /// <summary>Rauschen 0 … 1 aus einem Bodenpunkt (ganze Zahlen, auf jedem Rechner gleich).</summary>
    private static double Rauschen(int i, int j, int salz) => NaturVerteilung.Streuung(i * 31 + salz, (long)j * 92821 + salz * 7) % 1024 / 1023.0;

    /// <summary>
    /// Die Gewichte (Fels, Kies, Moos; je 0 … 1) eines Stücks an jedem Bodenpunkt in seiner Reichweite – <paramref name="aus"/> wird je Punkt
    /// aufgerufen, nur wo etwas wirkt. Grenzen: nur Punkte zwischen <paramref name="i0"/> … <paramref name="j1"/>.
    /// </summary>
    internal static void Gewichte(Felsfuss f, Hoehenraster hoehen, (int I0, int J0, int I1, int J1) erlaubt, Action<int, int, double, double, double> aus)
    {
        if (!Malt(f.Form)) return;
        var l = new Lage(f, Felsformen.Finde(f.Form)!, hoehen);
        var (i0, j0) = (Math.Max(erlaubt.I0, (int)Math.Floor(l.X - l.Reichweite)), Math.Max(erlaubt.J0, (int)Math.Floor(l.Z - l.Reichweite)));
        var (i1, j1) = (Math.Min(erlaubt.I1, (int)Math.Ceiling(l.X + l.Reichweite)), Math.Min(erlaubt.J1, (int)Math.Ceiling(l.Z + l.Reichweite)));
        for (var j = j0; j <= j1; j++)
            for (var i = i0; i <= i1; i++)
            {
                var (dx, dz) = (i * Bodenraster.Abstand - l.X, j * Bodenraster.Abstand - l.Z);
                var rho = Math.Sqrt(dx * dx + dz * dz);
                var (u, v) = l.Lokal(dx, dz);
                var e = Math.Sqrt(u / l.Rb * (u / l.Rb) + v / l.Rt * (v / l.Rt));
                // Abstand zum Rand der Grundfläche (Meter, entlang des Strahls von der Mitte; innen negativ)
                var d = e < 1e-9 ? -Math.Min(l.Rb, l.Rt) : rho * (1 - 1 / e);
                var rand = (Rauschen(i, j, 1) - 0.5) * 0.5; // ausgefranste Ränder (± 25 cm)

                var fels = d + rand <= 0 ? 1 : Gelaendeform.Weich(1 - (d + rand) / FelsRand);

                var (ux, uz) = rho < 1e-9 ? (0.0, 0.0) : (dx / rho, dz / rho);
                var oben = l.Hang ? Math.Max(0, -(ux * l.Gx + uz * l.Gz)) : 0;
                var nord = Math.Max(0, -uz);
                var seite = Math.Clamp(0.25 + 0.75 * Math.Max(oben, 0.8 * nord), 0, 1);
                var moos = Gelaendeform.Weich((d + rand + MoosInnen) / MoosInnen) * Gelaendeform.Weich(1 - (d + rand) / MoosBreite) * seite;

                double kies;
                if (l.Hang)
                {
                    // Kegel von der Mitte hangabwärts: Länge Rdir + Kegel, unten breiter, das Ende ausgefranst
                    var a = dx * l.Gx + dz * l.Gz;
                    var q = Math.Abs(-dx * l.Gz + dz * l.Gx);
                    var t = a / (l.Rdir + l.Kegel) + (Rauschen(i, j, 2) - 0.5) * 0.18;
                    var halb = l.Rquer * (0.8 + 0.6 * Math.Clamp(t, 0, 1)) + 0.4;
                    kies = a <= 0 ? 0 : Gelaendeform.Weich((1 - t) / 0.45) * Gelaendeform.Weich((halb - q) / (0.4 * halb));
                }
                else kies = 0.6 * Gelaendeform.Weich(1 - (d + rand) / (0.35 * l.Hoehe + 0.6));

                if (fels <= 0 && kies <= 0 && moos <= 0) continue;
                aus(i, j, fels, kies, moos);
            }
    }

    /// <summary>
    /// Die Schuttsteine vor einem Stück (siehe Kopf der Datei): je Meter Höhe <see cref="SteineJeHoehe"/>, höchstens <see cref="SteineMax"/>, im
    /// Kegel hangabwärts (eben: rund um den Fuß). Aus Lage und Form gerechnet, ohne Zufall des Strichs – ein Stück hat immer dieselben Steine.
    /// </summary>
    public static IReadOnlyList<Schuttstein> Schuttsteine(Felsfuss f, Hoehenraster hoehen)
    {
        if (!Malt(f.Form)) return Array.Empty<Schuttstein>();
        var l = new Lage(f, Felsformen.Finde(f.Form)!, hoehen);
        var n = Math.Min(SteineMax, (int)Math.Round(SteineJeHoehe * l.Hoehe, MidpointRounding.AwayFromZero));
        if (n <= 0) return Array.Empty<Schuttstein>();
        var zufall = new Zufallsquelle(unchecked((int)NaturVerteilung.Streuung((int)Math.Round(f.X * 100), (long)Math.Round(f.Z * 100) * 17 + 3)));
        var aus = new List<Schuttstein>(n);
        for (var k = 0; k < n; k++)
        {
            double x, z;
            if (l.Hang)
            {
                // längs: vom Fuß bis ans Ende des Kegels, dichter am Fuß; quer: in der Breite des Kegels
                var t = zufall.NextDouble();
                var laengs = l.Rdir * 0.85 + l.Kegel * 0.8 * t * t;
                var quer = (2 * zufall.NextDouble() - 1) * (l.Rquer * (0.7 + 0.5 * t));
                (x, z) = (l.X + l.Gx * laengs - l.Gz * quer, l.Z + l.Gz * laengs + l.Gx * quer);
            }
            else
            {
                var w = zufall.NextDouble() * 2 * Math.PI;
                var (ux, uz) = (DetMathe.Cos(w), DetMathe.Sin(w));
                var r = l.RadiusIn(ux, uz) * 0.95 + zufall.NextDouble() * (0.25 * l.Hoehe + 0.3);
                (x, z) = (l.X + ux * r, l.Z + uz * r);
            }
            var drehung = zufall.NextDouble() * 360;
            var massstab = Math.Clamp(Math.Min(1.6, 0.35 + 0.18 * l.Hoehe) * (0.55 + 0.9 * zufall.NextDouble()), Felsformen.MassstabMin, 2);
            aus.Add(new Schuttstein(Kartenraster.Punkt(x), Kartenraster.Punkt(z), Kartenraster.Drehung(drehung), Math.Round(massstab, 2)));
        }
        return aus;
    }
}

/// <summary>
/// Der Boden eines Felsstrichs oder Stempels (siehe Kopf der Datei): sammelt je Punkt das stärkste Gewicht von Fels, Kies und Moos und schreibt
/// den Punkt jedes Mal neu aus dem Stand vor dem Strich. Die Änderungen liegen in einer <see cref="Bodenaenderung"/> (für Rückgängig).
/// </summary>
internal sealed class Felsbodenstrich
{
    private readonly Hoehenraster _hoehen;
    private readonly (int I0, int J0, int I1, int J1) _erlaubt;
    private readonly Dictionary<(int, int), (double Fels, double Kies, double Moos)> _staerkster = new();

    public Felsbodenstrich(Bodenraster raster, Hoehenraster hoehen, Kartenrechteck karte)
    {
        Aenderung = new Bodenaenderung(raster, karte);
        _hoehen = hoehen;
        _erlaubt = Aenderung.PunkteIn(karte);
    }

    public Bodenaenderung Aenderung { get; }

    /// <summary>Malt den Boden um ein Stück. Gibt das Rechteck zurück, in dem sich etwas geändert haben kann (null: nichts).</summary>
    public Kartenrechteck? Malen(Felsfuss f)
    {
        var (i0, j0, i1, j1) = (int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
        Span<byte> vorher = stackalloc byte[Bodenraster.Kanaele];
        Span<byte> neu = stackalloc byte[Bodenraster.Kanaele];
        Span<double> g = stackalloc double[Bodenraster.DekoKanal];
        var (fels, kies, moos) = ((int)Bodentextur.Fels - 1, (int)Bodentextur.Kies - 1, (int)Bodentextur.Moos - 1);
        var punkte = new List<(int I, int J)>();
        Felsboden.Gewichte(f, _hoehen, _erlaubt, (i, j, wf, wk, wm) =>
        {
            var alt = _staerkster.GetValueOrDefault((i, j));
            var jetzt = (Math.Max(alt.Fels, wf), Math.Max(alt.Kies, wk), Math.Max(alt.Moos, wm));
            if (jetzt == alt) return;
            _staerkster[(i, j)] = jetzt;
            punkte.Add((i, j));
        });
        foreach (var (i, j) in punkte)
        {
            var w = _staerkster[(i, j)];
            Aenderung.Vorher(i, j, vorher);
            for (var c = 0; c < Bodenraster.DekoKanal; c++) g[c] = vorher[c];
            Auftragen(g, moos, Felsboden.MoosStaerke * w.Moos);
            Auftragen(g, kies, Felsboden.KiesStaerke * w.Kies);
            Auftragen(g, fels, w.Fels);
            var summe = 0;
            for (var c = 0; c < Bodenraster.DekoKanal; c++)
            {
                neu[c] = (byte)Math.Clamp((int)Math.Round(g[c], MidpointRounding.AwayFromZero), 0, 255);
                summe += neu[c];
            }
            // Rundung: nie mehr als 255 zusammen – zuerst beim Fels abziehen, sonst beim größten Anteil (wie der Texturpinsel)
            while (summe > 255)
            {
                var c = neu[fels] > 0 ? fels : Groesster(neu);
                neu[c]--;
                summe--;
            }
            neu[Bodenraster.DekoKanal] = vorher[Bodenraster.DekoKanal];
            Aenderung.Setzen(i, j, neu);
            (i0, j0, i1, j1) = (Math.Min(i0, i), Math.Min(j0, j), Math.Max(i1, i), Math.Max(j1, j));
        }
        if (i0 > i1) return null;
        return new Kartenrechteck(i0 * Bodenraster.Abstand, j0 * Bodenraster.Abstand, i1 * Bodenraster.Abstand, j1 * Bodenraster.Abstand);

        static void Auftragen(Span<double> g, int kanal, double a)
        {
            if (a <= 0) return;
            for (var c = 0; c < g.Length; c++) g[c] *= 1 - a;
            g[kanal] += 255 * a;
        }

        static int Groesster(Span<byte> werte)
        {
            var beste = 0;
            for (var c = 1; c < Bodenraster.DekoKanal; c++)
                if (werte[c] > werte[beste]) beste = c;
            return beste;
        }
    }
}
