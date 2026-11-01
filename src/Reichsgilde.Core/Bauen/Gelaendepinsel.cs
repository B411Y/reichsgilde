using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Die Geländepinsel der Kartenwerkstatt (Schritt 5b, Pinsel-Paket 1): Heben, Senken, Glätten, Ebnen, Rauschen, Terrassen – und die Rampe
//
//  Ein Pinselstrich ist eine Folge von Punkten (die Maus beim Ziehen). Der Strich setzt selbst alle Radius/4 Meter (mindestens 1 m) einen
//  Tupfer – nicht je Bild der Darstellung –, damit derselbe Strich immer dasselbe Gelände ergibt, egal wie schnell der Rechner ist.
//  Jeder Tupfer wirkt in einem Kreis: innen voll, zum Rand hin weich auslaufend (Kante 0 = harter Rand, 1 = von der Mitte an weich).
//    • Heben/Senken: je Tupfer bis zu „Stärke“ Meter (in der Mitte) höher bzw. tiefer – mehrmals drüberstreichen hebt weiter.
//    • Glätten: je Tupfer zieht jeden Punkt um „Stärke“ (0 … 1) zum Mittel seiner Nachbarn (5 × 5 Punkte, 8 × 8 m).
//    • Ebnen: zieht alles auf die Höhe unter dem ersten Punkt des Strichs – oder auf eine eingegebene Zielhöhe –, um „Stärke“ (0 … 1; 1 = ganz eben).
//    • Rauschen: natürliche Unebenheit – ein Rauschen mit Buckeln von etwa „Maßstab“ Metern und „Stärke“ Metern Höhe (nach oben und unten),
//      aus dem Startwert.
//    • Terrassen: treppt den Boden in Stufen von „Stufenhöhe“ Metern (bei der Bezugshöhe, Vorgabe 0 m, und ihren Vielfachen darüber und
//      darunter): Jeder Punkt geht auf die nächste Stufe; die „Böschung“ (0 … 1) ist der Anteil jeder Stufe, über den es weich hinaufgeht
//      (0 = senkrechte Kante, 1 = sanfte Wellen). „Stärke“ (0 … 1) wie beim Ebnen.
//    Ebnen, Rauschen und Terrassen wirken in einem Strich nur einmal je Punkt (mit dem stärksten Tupfer, der ihn traf), vom Stand vor dem Strich.
//    • „Nur Mulden“ beim Heben: hebt jeden Punkt um den Hub, aber höchstens bis zur Höhe der Tupfermitte plus Hub – was darüber liegt (Kuppen),
//      bleibt; Hangfuß und Senken füllen sich. Ist alles eben, hebt er wie gewohnt. „Nur Spitzen“ beim Senken umgekehrt: höchstens bis Mitte
//      minus Hub, was darunter liegt (Mulden), bleibt – ein Grat wird zum Bauplatz. Nach Terrain3D (MIT), src/terrain_3d_editor.cpp:244-262
//      („Lift troughs“, „Flatten peaks“), Commit cdcd382c.
//    • Filter (Pinselfilter): nur bei einer Steigung oder auf einer Bodentextur – gemessen am Stand vor dem Strich.
//  Die Rampe legt zwischen zwei Punkten ein Band mit gleichmäßigem Gefälle von der Höhe am ersten zur Höhe am zweiten Punkt, mit weichen
//  Seiten. Alles bleibt in −20 … +60 m und in der Karte (Spielfeld und Rand); Höhen werden auf ganze Zentimeter gerundet.
//  Ohne Zufall und ohne Winkelfunktionen: gleiche Punkte, gleiches Gelände – auf jedem Rechner.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Was ein Geländepinsel tut (siehe Kopf der Datei).</summary>
public enum Pinselart
{
    Heben,
    Senken,
    Glaetten,
    Ebnen,
    Rauschen,
    Terrassen,
}

/// <summary>
/// Ein Geländepinsel: Art, Radius (Meter), Stärke (Heben, Senken, Rauschen: Meter; Glätten, Ebnen, Terrassen: Anteil 0 … 1), weiche Kante (0 … 1),
/// dazu für das Rauschen Startwert und Maßstab (Größe der Buckel in Metern). Pinsel-Paket 1: <paramref name="Zielhoehe"/> (Ebnen: diese Höhe in
/// Metern statt der unter dem ersten Punkt; Terrassen: die Bezugshöhe, Vorgabe 0 m), <paramref name="Stufenhoehe"/> und <paramref name="Boeschung"/>
/// der Terrassen, <paramref name="NurAusgleich"/> (Heben: nur Mulden, Senken: nur Spitzen) und der <paramref name="Filter"/> (Steigung, Boden).
/// </summary>
public sealed record Pinsel(Pinselart Art, double Radius = 10, double Staerke = 0.25, double Kante = 0.5, int Startwert = 1, double Massstab = 20,
    double? Zielhoehe = null, double Stufenhoehe = 2, double Boeschung = 0.3, bool NurAusgleich = false, Pinselfilter? Filter = null)
{
    /// <summary>Kleinster und größter Radius (Meter).</summary>
    public const double MinRadius = 1, MaxRadius = 200;

    /// <summary>Kleinste und größte Stufenhöhe der Terrassen (Meter).</summary>
    public const double MinStufe = 0.25, MaxStufe = 20;

    /// <summary>Ist die Stärke ein Anteil 0 … 1 (Glätten, Ebnen, Terrassen) und nicht Meter?</summary>
    public bool StaerkeAlsAnteil => Art is Pinselart.Glaetten or Pinselart.Ebnen or Pinselart.Terrassen;

    /// <summary>Der Pinsel mit Werten in ihren Grenzen (Zielhöhe auf ganze Zentimeter in −20 … +60 m).</summary>
    public Pinsel Begrenzt() => this with
    {
        Radius = Math.Clamp(Radius, MinRadius, MaxRadius),
        Staerke = StaerkeAlsAnteil ? Math.Clamp(Staerke, 0, 1) : Math.Clamp(Staerke, 0, Hoehenraster.Hoechste - Hoehenraster.Tiefste),
        Kante = Math.Clamp(Kante, 0, 1),
        Massstab = Math.Clamp(Massstab, 2, 1000),
        Zielhoehe = Zielhoehe is { } z ? Math.Clamp(Math.Round(z * 100, MidpointRounding.AwayFromZero), Hoehenraster.TiefsteCm, Hoehenraster.HoechsteCm) / 100 : null,
        Stufenhoehe = Math.Clamp(Stufenhoehe, MinStufe, MaxStufe),
        Boeschung = Math.Clamp(Boeschung, 0, 1),
        Filter = Filter is { } f && !f.Begrenzt().IstLeer ? f.Begrenzt() : null,
    };

    /// <summary>
    /// Terrassen: die Höhe der Stufe (cm), auf die ein Punkt mit dieser Höhe (cm) geht – die nächste Stufe bei Bezugshöhe + k · Stufenhöhe, über
    /// die Böschung weich zur nächsten (siehe Kopf der Datei). Ein eigener Gedanke, Terrain3D hat keine Terrassen.
    /// </summary>
    public double Stufe(double cm)
    {
        var (s, bezug) = (Stufenhoehe * 100, (Zielhoehe ?? 0) * 100);
        var t = (cm - bezug) / s;
        var k = Math.Floor(t);
        var g = t - k;
        var hinauf = Boeschung <= 0 ? (g < 0.5 ? 0 : 1) : Gelaendeform.Weich((g - (0.5 - Boeschung / 2)) / Boeschung);
        return bezug + (k + hinauf) * s;
    }

    /// <summary>Abstand der Tupfer entlang des Strichs (Meter).</summary>
    public double Tupfabstand => Math.Max(1, Radius / 4);

    /// <summary>Wie stark ein Tupfer in diesem Abstand von seiner Mitte wirkt (1 innen, 0 am Rand).</summary>
    public double Gewicht(double abstand)
    {
        if (abstand >= Radius) return 0;
        var innen = Radius * (1 - Kante);
        return abstand <= innen ? 1 : Gelaendeform.Weich((Radius - abstand) / (Radius - innen));
    }
}

/// <summary>
/// Sammelt die Änderungen eines Pinselstrichs oder einer Rampe am Raster: merkt sich jede Kachel, bevor sie zum ersten Mal geändert wird
/// (für Rückgängig), und das Rechteck, das sich geändert hat.
/// </summary>
public sealed class Gelaendeaenderung
{
    private readonly Hoehenraster _raster;
    private readonly (int I0, int J0, int I1, int J1) _erlaubt;
    private readonly Dictionary<(int I, int J), short[]?> _vorher = new();
    private (int I0, int J0, int I1, int J1)? _geaendert;

    /// <param name="raster">Das Raster, das geändert wird.</param>
    /// <param name="karte">Die ganze Karte (Spielfeld und Rand): Punkte außerhalb bleiben, wie sie sind.</param>
    internal Gelaendeaenderung(Hoehenraster raster, Kartenrechteck karte) => (_raster, _erlaubt) = (raster, Hoehenraster.Punkte(karte));

    /// <summary>Das Rechteck (Meter), in dem sich seit dem Anfang etwas geändert hat – null: noch nichts.</summary>
    public Kartenrechteck? Bereich => _geaendert is { } g
        ? new Kartenrechteck(g.I0 * Hoehenraster.Abstand, g.J0 * Hoehenraster.Abstand, g.I1 * Hoehenraster.Abstand, g.J1 * Hoehenraster.Abstand) : null;

    /// <summary>Die Rasterpunkte in einem Rechteck (Meter), die in der Karte liegen – leer, wenn keiner.</summary>
    internal (int I0, int J0, int I1, int J1) PunkteIn(Kartenrechteck r)
    {
        var (i0, j0, i1, j1) = Hoehenraster.Punkte(r);
        return (Math.Max(i0, _erlaubt.I0), Math.Max(j0, _erlaubt.J0), Math.Min(i1, _erlaubt.I1), Math.Min(j1, _erlaubt.J1));
    }

    /// <summary>Setzt einen Rasterpunkt (cm, wird begrenzt); merkt sich vorher seine Kachel.</summary>
    internal void Setzen(int i, int j, double cm)
    {
        var neu = Math.Clamp((int)Math.Round(cm, MidpointRounding.AwayFromZero), Hoehenraster.TiefsteCm, Hoehenraster.HoechsteCm);
        if (neu == _raster.Wert(i, j)) return;
        var k = Hoehenraster.KachelVon(i, j);
        if (!_vorher.ContainsKey(k)) _vorher[k] = _raster.KachelKopie(k);
        _raster.WertSetzen(i, j, neu);
        _geaendert = _geaendert is { } g ? (Math.Min(g.I0, i), Math.Min(g.J0, j), Math.Max(g.I1, i), Math.Max(g.J1, j)) : (i, j, i, j);
    }

    /// <summary>Der Wert einer Kachel vor der Änderung (für Ebnen und Rauschen, die vom Stand vor dem Strich ausgehen).</summary>
    internal short WertVorher(int i, int j)
    {
        if (!_vorher.TryGetValue(Hoehenraster.KachelVon(i, j), out var alt)) return _raster.Wert(i, j);
        return alt is null ? (short)0 : alt[((j & (Hoehenraster.Kachel - 1)) * Hoehenraster.Kachel) | (i & (Hoehenraster.Kachel - 1))];
    }

    /// <summary>Die geänderten Kacheln vorher und nachher (für den Befehl mit Rückgängig); leer, wenn sich nichts geändert hat.</summary>
    internal List<((int I, int J) Kachel, short[]? Vorher, short[]? Nachher)> Kacheln() =>
        _vorher.OrderBy(k => k.Key.J).ThenBy(k => k.Key.I).Select(k => (k.Key, k.Value, LeerAlsNull(_raster.KachelKopie(k.Key)))).ToList();

    private static short[]? LeerAlsNull(short[]? w) => w is null || Array.TrueForAll(w, v => v == 0) ? null : w;
}

/// <summary>Ein Pinselstrich auf dem Raster (siehe Kopf der Datei). Entsteht mit <see cref="Gelaendepinsel.Beginnen"/>.</summary>
public sealed class Gelaendestrich
{
    private readonly Hoehenraster _raster;
    private readonly Gelaendeaenderung _aenderung;

    /// <summary>Genaue Höhen (cm) der berührten Punkte für Heben, Senken, Glätten – sonst summierten sich die Rundungen.</summary>
    private readonly Dictionary<(int, int), double> _genau = new();

    /// <summary>Ebnen, Rauschen und Terrassen: das stärkste Gewicht, mit dem ein Punkt in diesem Strich getroffen wurde.</summary>
    private readonly Dictionary<(int, int), double> _staerkster = new();

    /// <summary>Der Filter (Steigung vom Stand vor dem Strich) – null: wirkt überall.</summary>
    private readonly Filtermessung? _filter;

    /// <summary>Das Bodenraster für den Bodenfilter (null: keins, dann passt kein Bodenfilter außer auf Wiese).</summary>
    private readonly Bodenraster? _boden;

    private Kartenort? _letzter;
    private double _rest;
    private double _ziel;

    internal Gelaendestrich(Hoehenraster raster, Pinsel pinsel, Kartenrechteck karte, Bodenraster? boden = null)
    {
        (_raster, Pinsel, _boden) = (raster, pinsel.Begrenzt(), boden);
        _aenderung = new Gelaendeaenderung(raster, karte);
        if (Pinsel.Filter is { } f) _filter = new Filtermessung(f, (i, j) => _aenderung.WertVorher(i, j));
    }

    public Pinsel Pinsel { get; }

    /// <summary>Die Änderungen bisher (Rückgängig-Daten, geändertes Rechteck).</summary>
    public Gelaendeaenderung Aenderung => _aenderung;

    /// <summary>Ebnen: die Zielhöhe in Metern (die eingegebene oder die Höhe unter dem ersten Punkt); vorher 0.</summary>
    public double Zielhoehe => _ziel / 100;

    /// <summary>
    /// Führt den Strich bis zu diesem Punkt weiter: der erste Punkt setzt einen Tupfer, danach alle <see cref="Pinsel.Tupfabstand"/> Meter
    /// entlang der Strecke einer. Gibt das Rechteck zurück, das sich dabei geändert hat (null: nichts).
    /// </summary>
    public Kartenrechteck? Weiter(Kartenort p)
    {
        Kartenrechteck? bereich = null;
        void Merken(Kartenrechteck? r)
        {
            if (r is not { } x) return;
            bereich = bereich is { } b ? new Kartenrechteck(Math.Min(b.X0, x.X0), Math.Min(b.Z0, x.Z0), Math.Max(b.X1, x.X1), Math.Max(b.Z1, x.Z1)) : x;
        }
        if (_letzter is not { } von)
        {
            _ziel = Pinsel.Zielhoehe is { } z ? z * 100 : _raster.Hoehe(p.X, p.Z) * 100;
            Merken(Tupfen(p));
            _letzter = p;
            return bereich;
        }
        var laenge = von.Abstand(p);
        var schritt = Pinsel.Tupfabstand;
        var weg = schritt - _rest;
        while (weg <= laenge + 1e-9)
        {
            var t = laenge == 0 ? 1 : weg / laenge;
            Merken(Tupfen(new Kartenort(von.X + (p.X - von.X) * t, von.Z + (p.Z - von.Z) * t)));
            weg += schritt;
        }
        _rest = laenge - (weg - schritt);
        _letzter = p;
        return bereich;
    }

    /// <summary>Ein Tupfer an diesem Ort.</summary>
    private Kartenrechteck? Tupfen(Kartenort m)
    {
        var p = Pinsel;
        var (i0, j0, i1, j1) = _aenderung.PunkteIn(new Kartenrechteck(m.X - p.Radius, m.Z - p.Radius, m.X + p.Radius, m.Z + p.Radius));
        if (i0 > i1 || j0 > j1) return null;
        var neu = new List<(int I, int J, double Cm)>();
        // Nur Mulden / nur Spitzen: die Höhe unter der Mitte des Tupfers (Stand vor diesem Tupfer) ist die Grenze
        var mitte = p.NurAusgleich && p.Art is Pinselart.Heben or Pinselart.Senken ? GenauBei(m) : 0;
        for (var j = j0; j <= j1; j++)
            for (var i = i0; i <= i1; i++)
            {
                var (dx, dz) = (i * Hoehenraster.Abstand - m.X, j * Hoehenraster.Abstand - m.Z);
                var w = p.Gewicht(Math.Sqrt(dx * dx + dz * dz));
                if (w > 0 && p.Filter is not null) w *= FilterFaktor(i, j);
                if (w <= 0) continue;
                switch (p.Art)
                {
                    case Pinselart.Heben:
                    {
                        var (jetzt, hub) = (Genau(i, j), p.Staerke * 100 * w);
                        // nach Terrain3D (MIT), src/terrain_3d_editor.cpp:244-262, Commit cdcd382c: clamp(Mitte + Hub, jetzt, jetzt + Hub)
                        var ziel = p.NurAusgleich ? Math.Min(jetzt + hub, Math.Max(jetzt, mitte + hub)) : jetzt + hub;
                        if (ziel != jetzt) neu.Add((i, j, ziel));
                        break;
                    }
                    case Pinselart.Senken:
                    {
                        var (jetzt, hub) = (Genau(i, j), p.Staerke * 100 * w);
                        // nach Terrain3D (MIT), src/terrain_3d_editor.cpp:244-262, Commit cdcd382c: clamp(Mitte − Hub, jetzt − Hub, jetzt)
                        var ziel = p.NurAusgleich ? Math.Max(jetzt - hub, Math.Min(jetzt, mitte - hub)) : jetzt - hub;
                        if (ziel != jetzt) neu.Add((i, j, ziel));
                        break;
                    }
                    case Pinselart.Glaetten:
                        // aus dem Stand vor diesem Tupfer – alle Punkte des Tupfers sehen dieselben Nachbarn
                        double summe = 0;
                        for (var b = -2; b <= 2; b++)
                            for (var a = -2; a <= 2; a++)
                                summe += Genau(i + a, j + b);
                        var h = Genau(i, j);
                        neu.Add((i, j, h + (summe / 25 - h) * p.Staerke * w));
                        break;
                    case Pinselart.Ebnen:
                    case Pinselart.Rauschen:
                    case Pinselart.Terrassen:
                        var vorher = _staerkster.GetValueOrDefault((i, j));
                        if (w <= vorher) continue;
                        _staerkster[(i, j)] = w;
                        double start = _aenderung.WertVorher(i, j);
                        neu.Add((i, j, p.Art switch
                        {
                            Pinselart.Ebnen => start + (_ziel - start) * p.Staerke * w,
                            Pinselart.Terrassen => start + (p.Stufe(start) - start) * p.Staerke * w,
                            _ => start + Gelaendepinsel.Rauschwert(p.Startwert, i * Hoehenraster.Abstand, j * Hoehenraster.Abstand, p.Massstab) * p.Staerke * 100 * w,
                        }));
                        break;
                }
            }
        foreach (var (i, j, cm) in neu)
        {
            var begrenzt = Math.Clamp(cm, Hoehenraster.TiefsteCm, Hoehenraster.HoechsteCm);
            if (p.Art is Pinselart.Heben or Pinselart.Senken or Pinselart.Glaetten) _genau[(i, j)] = begrenzt;
            _aenderung.Setzen(i, j, begrenzt);
        }
        return new Kartenrechteck(i0 * Hoehenraster.Abstand, j0 * Hoehenraster.Abstand, i1 * Hoehenraster.Abstand, j1 * Hoehenraster.Abstand);
    }

    private double Genau(int i, int j) => _genau.TryGetValue((i, j), out var h) ? h : _raster.Wert(i, j);

    /// <summary>Die genaue Höhe (cm) an einem Ort, bilinear zwischen den vier Punkten der Zelle.</summary>
    private double GenauBei(Kartenort m)
    {
        var (fx, fz) = (m.X / Hoehenraster.Abstand, m.Z / Hoehenraster.Abstand);
        var (i, j) = ((int)Math.Floor(fx), (int)Math.Floor(fz));
        var (tx, tz) = (fx - i, fz - j);
        double a = Genau(i, j), b = Genau(i + 1, j), c = Genau(i, j + 1), d = Genau(i + 1, j + 1);
        return (a + (b - a) * tx) * (1 - tz) + (c + (d - c) * tx) * tz;
    }

    /// <summary>Wie stark der Filter an einem Höhenpunkt wirkt (0 … 1): Steigung vom Stand vor dem Strich, Boden am Bodenpunkt darunter.</summary>
    private double FilterFaktor(int i, int j)
    {
        var f = Pinsel.Filter!;
        if (f.Boden is { } t)
        {
            var (bi, bj) = (Bodenraster.Punktindex(i * Hoehenraster.Abstand), Bodenraster.Punktindex(j * Hoehenraster.Abstand));
            var anteil = _boden?.Gewicht(bi, bj, t) ?? (t == Bodentextur.Wiese ? 255 : 0);
            if (!f.BodenPasst(anteil)) return 0;
        }
        return _filter!.SteigungFaktorAmPunkt(i, j);
    }
}

/// <summary>Die Rechnungen der Geländepinsel (siehe Kopf der Datei).</summary>
public static class Gelaendepinsel
{
    /// <summary>
    /// Beginnt einen Pinselstrich auf dem Raster; Punkte außerhalb von <paramref name="karte"/> (Spielfeld und Rand) bleiben. Das Bodenraster
    /// braucht nur ein Pinsel mit Bodenfilter.
    /// </summary>
    public static Gelaendestrich Beginnen(Hoehenraster raster, Pinsel pinsel, Kartenrechteck karte, Bodenraster? boden = null) => new(raster, pinsel, karte, boden);

    /// <summary>Die Höhen-Pipette: die Höhe des gespeicherten Rasters an einem Ort, auf ganze Zentimeter (Meter) – als Zielhöhe für Ebnen und Terrassen.</summary>
    public static double Pipette(Hoehenraster raster, Kartenort ort) => Math.Round(raster.Hoehe(ort.X, ort.Z) * 100, MidpointRounding.AwayFromZero) / 100;

    /// <summary>
    /// Die Rampe von <paramref name="a"/> nach <paramref name="b"/>: ein Band von <paramref name="breite"/> Metern mit gleichmäßigem Gefälle
    /// von der Höhe unter a zur Höhe unter b; daneben geht es über <paramref name="kante"/> Meter weich ins Gelände über. Vor a und hinter b
    /// bleibt alles, wie es ist.
    /// </summary>
    public static Gelaendeaenderung Rampe(Hoehenraster raster, Kartenort a, Kartenort b, double breite, double kante, Kartenrechteck karte)
    {
        var aenderung = new Gelaendeaenderung(raster, karte);
        var (ha, hb) = (raster.Hoehe(a.X, a.Z) * 100, raster.Hoehe(b.X, b.Z) * 100);
        var (halb, rand) = (Math.Max(0.5, breite / 2), Math.Max(0, kante));
        var (dx, dz) = (b.X - a.X, b.Z - a.Z);
        var l2 = dx * dx + dz * dz;
        if (l2 < 1e-6) return aenderung;
        var weite = halb + rand;
        var r = new Kartenrechteck(Math.Min(a.X, b.X), Math.Min(a.Z, b.Z), Math.Max(a.X, b.X), Math.Max(a.Z, b.Z)).Vergroessert(weite);
        var (i0, j0, i1, j1) = aenderung.PunkteIn(r);
        var neu = new List<(int, int, double)>();
        for (var j = j0; j <= j1; j++)
            for (var i = i0; i <= i1; i++)
            {
                var (px, pz) = (i * Hoehenraster.Abstand - a.X, j * Hoehenraster.Abstand - a.Z);
                var t = (px * dx + pz * dz) / l2;
                if (t < 0 || t > 1) continue;
                var (qx, qz) = (px - t * dx, pz - t * dz);
                var d = Math.Sqrt(qx * qx + qz * qz);
                if (d >= weite) continue;
                var w = d <= halb ? 1 : Gelaendeform.Weich((weite - d) / rand);
                double h = raster.Wert(i, j);
                neu.Add((i, j, h + (ha + (hb - ha) * t - h) * w));
            }
        foreach (var (i, j, cm) in neu) aenderung.Setzen(i, j, cm);
        return aenderung;
    }

    /// <summary>
    /// Rauschen an einem Ort, −1 … 1: drei Lagen Wertrauschen (Buckel von etwa <paramref name="massstab"/> Metern, dazu halb und viertel so
    /// große mit halber und viertel Höhe), aus dem Startwert. Ganzzahlige Streuung, kein Zufallsgenerator: auf jedem Rechner gleich.
    /// </summary>
    public static double Rauschwert(int startwert, double x, double z, double massstab)
    {
        double summe = 0, hoehe = 1, gewichte = 0, f = 1 / Math.Max(2, massstab);
        for (var lage = 0; lage < 3; lage++)
        {
            summe += hoehe * Wertrauschen(unchecked(startwert + lage * 1013), x * f, z * f);
            gewichte += hoehe;
            (hoehe, f) = (hoehe / 2, f * 2);
        }
        return summe / gewichte;
    }

    /// <summary>Ein Wert −1 … 1 je Gitterpunkt, dazwischen weich (fünfter Ordnung) verblendet.</summary>
    private static double Wertrauschen(int startwert, double x, double z)
    {
        var (i, j) = ((int)Math.Floor(x), (int)Math.Floor(z));
        var (u, v) = (Glatt(x - i), Glatt(z - j));
        double a = Gitter(startwert, i, j), b = Gitter(startwert, i + 1, j), c = Gitter(startwert, i, j + 1), d = Gitter(startwert, i + 1, j + 1);
        return (a + (b - a) * u) * (1 - v) + (c + (d - c) * u) * v;

        static double Glatt(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    }

    /// <summary>Ein fester Wert −1 … 1 je Gitterpunkt und Startwert (ganzzahlige Streuung).</summary>
    private static double Gitter(int startwert, int i, int j)
    {
        unchecked
        {
            var h = (uint)startwert * 0x9E3779B1u ^ (uint)i * 0x85EBCA77u ^ (uint)j * 0xC2B2AE3Du;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return h / (double)uint.MaxValue * 2 - 1;
        }
    }
}
