using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Der Texturpinsel der Kartenwerkstatt (Schritt 5e): malt eine Bodentextur, streut ihre Deko – und der Radierer (zurück zur Wiese)
//
//  Wie die Geländepinsel: Ein Strich setzt selbst alle Radius/4 Meter (mindestens 0,5 m) einen Tupfer, jeder Tupfer wirkt in einem Kreis,
//  innen voll, zum Rand weich (Kante 0 = harter Rand, 1 = von der Mitte an weich). Ein Punkt zählt in einem Strich nur einmal, mit dem
//  stärksten Tupfer, der ihn traf – so deckt ein Strich gleichmäßig, egal wie oft er über dieselbe Stelle fährt; ein zweiter Strich deckt weiter.
//    • Textur: Anteil a = Stärke · Gewicht; alle Anteile werden um a kleiner, die gemalte Textur bekommt a dazu (Stärke 1 deckt ganz).
//    • Deko: Mit „Deko streuen“ geht die Dekodichte um das Gewicht des Tupfers zur Dichte des Pinsels (0 … 2, 1 = normal), ohne auf 0
//      (dort streut nichts). Gilt unabhängig von der Stärke: Eine leicht gemalte Textur streut, aber nur nach ihrem Anteil.
//    • Radierer: Wiese mit Stärke 1, ohne Deko.
//    • Filter (Pinsel-Paket 1, Pinselfilter): nur bei einer Steigung (aus dem Höhenraster, das ein Texturstrich nicht ändert) oder nur auf einer
//      Bodentextur – gemessen am Boden vor dem Strich, sonst wüchse die gemalte Fläche mit jedem Tupfer über ihren Filter hinaus.
//  Alles bleibt in der Karte (Spielfeld und Rand). Ohne Zufall: gleicher Strich, gleicher Boden.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Ein Texturpinsel (siehe Kopf der Datei): Textur, Radius (m), Stärke (0 … 1), Kante (0 … 1), Deko streuen, Dichte der Deko (0 … 2) und der
/// Filter (Steigung, Boden; null: überall).
/// </summary>
public sealed record Bodenpinsel(Bodentextur Textur, double Radius = 5, double Staerke = 1, double Kante = 0.5, bool Deko = true, double Dichte = 1,
    Pinselfilter? Filter = null)
{
    /// <summary>Kleinster und größter Radius (Meter).</summary>
    public const double MinRadius = 0.5, MaxRadius = 100;

    /// <summary>Der Radierer: zurück zur Wiese, ohne Deko.</summary>
    public static Bodenpinsel Radierer(double radius = 5, double kante = 0.5, Pinselfilter? filter = null) => new(Bodentextur.Wiese, radius, 1, kante, Deko: false, Filter: filter);

    /// <summary>Ist das der Radierer (Wiese ohne Deko)?</summary>
    public bool IstRadierer => Textur == Bodentextur.Wiese && !Deko;

    /// <summary>Der Pinsel mit Werten in ihren Grenzen.</summary>
    public Bodenpinsel Begrenzt() => this with
    {
        Radius = Math.Clamp(Radius, MinRadius, MaxRadius),
        Staerke = Math.Clamp(Staerke, 0, 1),
        Kante = Math.Clamp(Kante, 0, 1),
        Dichte = Math.Clamp(Dichte, 0, 2),
        Filter = Filter is { } f && !f.Begrenzt().IstLeer ? f.Begrenzt() : null,
    };

    /// <summary>Abstand der Tupfer entlang des Strichs (Meter).</summary>
    public double Tupfabstand => Math.Max(0.5, Radius / 4);

    /// <summary>Wie stark ein Tupfer in diesem Abstand von seiner Mitte wirkt (1 innen, 0 am Rand).</summary>
    public double Gewicht(double abstand)
    {
        if (abstand >= Radius) return 0;
        var innen = Radius * (1 - Kante);
        return abstand <= innen ? 1 : Gelaendeform.Weich((Radius - abstand) / (Radius - innen));
    }
}

/// <summary>
/// Sammelt die Änderungen eines Texturstrichs am Bodenraster: merkt sich jede Kachel, bevor sie zum ersten Mal geändert wird (für Rückgängig
/// und als Ausgangsstand des Strichs), und das Rechteck, das sich geändert hat.
/// </summary>
public sealed class Bodenaenderung
{
    private readonly Bodenraster _raster;
    private readonly (int I0, int J0, int I1, int J1) _erlaubt;
    private readonly Dictionary<(int I, int J), byte[]?[]?> _vorher = new();
    private (int I0, int J0, int I1, int J1)? _geaendert;

    internal Bodenaenderung(Bodenraster raster, Kartenrechteck karte) => (_raster, _erlaubt) = (raster, Bodenraster.PunkteIn(karte));

    /// <summary>Das Rechteck (Meter), in dem sich seit dem Anfang etwas geändert hat – null: noch nichts.</summary>
    public Kartenrechteck? Bereich => _geaendert is { } g
        ? new Kartenrechteck(g.I0 * Bodenraster.Abstand, g.J0 * Bodenraster.Abstand, g.I1 * Bodenraster.Abstand, g.J1 * Bodenraster.Abstand) : null;

    /// <summary>Die Bodenpunkte in einem Rechteck (Meter), die in der Karte liegen – leer, wenn keiner.</summary>
    internal (int I0, int J0, int I1, int J1) PunkteIn(Kartenrechteck r)
    {
        var (i0, j0, i1, j1) = Bodenraster.PunkteIn(r);
        return (Math.Max(i0, _erlaubt.I0), Math.Max(j0, _erlaubt.J0), Math.Min(i1, _erlaubt.I1), Math.Min(j1, _erlaubt.J1));
    }

    /// <summary>Die elf Werte eines Punkts vor dem Strich.</summary>
    internal void Vorher(int i, int j, Span<byte> aus)
    {
        if (!_vorher.TryGetValue(Bodenraster.KachelVon(i, j), out var alt))
        {
            _raster.WerteLesen(i, j, aus);
            return;
        }
        aus.Clear();
        if (alt is null) return;
        var n = ((j & (Bodenraster.Kachel - 1)) * Bodenraster.Kachel) | (i & (Bodenraster.Kachel - 1));
        for (var c = 0; c < Bodenraster.Kanaele; c++)
            if (alt[c] is { } w) aus[c] = w[n];
    }

    /// <summary>Der Anteil einer Textur an einem Punkt vor dem Strich (0 … 255; die Wiese bekommt, was übrig bleibt).</summary>
    internal byte GewichtVorher(int i, int j, Bodentextur t)
    {
        Span<byte> w = stackalloc byte[Bodenraster.Kanaele];
        Vorher(i, j, w);
        if (t != Bodentextur.Wiese) return w[(int)t - 1];
        var summe = 0;
        for (var c = 0; c < Bodenraster.DekoKanal; c++) summe += w[c];
        return (byte)(255 - summe);
    }

    /// <summary>Setzt die elf Werte eines Punkts; merkt sich vorher seine Kachel.</summary>
    internal void Setzen(int i, int j, ReadOnlySpan<byte> werte)
    {
        Span<byte> jetzt = stackalloc byte[Bodenraster.Kanaele];
        _raster.WerteLesen(i, j, jetzt);
        if (jetzt.SequenceEqual(werte)) return;
        var k = Bodenraster.KachelVon(i, j);
        if (!_vorher.ContainsKey(k)) _vorher[k] = _raster.KachelKopie(k);
        _raster.WerteSetzen(i, j, werte);
        _geaendert = _geaendert is { } g ? (Math.Min(g.I0, i), Math.Min(g.J0, j), Math.Max(g.I1, i), Math.Max(g.J1, j)) : (i, j, i, j);
    }

    /// <summary>Die geänderten Kacheln vorher und nachher, gepackt (für den Befehl mit Rückgängig); Kacheln, die gleich blieben, fallen weg.</summary>
    internal List<((int I, int J) Kachel, string?[]? Vorher, string?[]? Nachher)> Kacheln()
    {
        var liste = new List<((int I, int J), string?[]?, string?[]?)>();
        foreach (var (k, alt) in _vorher.OrderBy(k => k.Key.J).ThenBy(k => k.Key.I))
        {
            _raster.Aufraeumen(k);
            var (vorher, nachher) = (Bodenraster.KachelPacken(alt), _raster.KachelGepackt(k));
            if (!Gleich(vorher, nachher)) liste.Add((k, vorher, nachher));
        }
        return liste;

        static bool Gleich(string?[]? a, string?[]? b) => a is null ? b is null : b is not null && a.SequenceEqual(b);
    }
}

/// <summary>Ein Texturstrich auf dem Bodenraster (siehe Kopf der Datei). Entsteht mit <see cref="Bodenpinsel"/> über den Kartenentwurf.</summary>
public sealed class Bodenstrich
{
    private readonly Bodenaenderung _aenderung;

    /// <summary>Je Punkt das stärkste Gewicht, mit dem ihn ein Tupfer dieses Strichs traf.</summary>
    private readonly Dictionary<(int, int), double> _staerkster = new();

    /// <summary>Die Steigung für den Filter (null: kein Filter nach der Steigung).</summary>
    private readonly Filtermessung? _filter;

    private Kartenort? _letzter;
    private double _rest;

    /// <param name="hoehen">Das Höhenraster für einen Filter nach der Steigung (null: flach).</param>
    internal Bodenstrich(Bodenraster raster, Bodenpinsel pinsel, Kartenrechteck karte, Hoehenraster? hoehen = null)
    {
        Pinsel = pinsel.Begrenzt();
        _aenderung = new Bodenaenderung(raster, karte);
        if (Pinsel.Filter is { MitSteigung: true } f) _filter = new Filtermessung(f, hoehen is null ? (_, _) => 0 : (i, j) => hoehen.Wert(i, j));
    }

    public Bodenpinsel Pinsel { get; }

    /// <summary>Die Änderungen bisher (Rückgängig-Daten, geändertes Rechteck).</summary>
    public Bodenaenderung Aenderung => _aenderung;

    /// <summary>Führt den Strich bis zu diesem Punkt weiter (wie <see cref="Gelaendestrich.Weiter"/>). Gibt das geänderte Rechteck zurück (null: nichts).</summary>
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
        Span<byte> vorher = stackalloc byte[Bodenraster.Kanaele];
        Span<byte> neu = stackalloc byte[Bodenraster.Kanaele];
        Span<double> genau = stackalloc double[Bodenraster.DekoKanal];
        var zielDeko = p.Deko ? p.Dichte * Bodenraster.DekoNormal : 0;
        var gemalt = p.Textur == Bodentextur.Wiese ? -1 : (int)p.Textur - 1;
        for (var j = j0; j <= j1; j++)
            for (var i = i0; i <= i1; i++)
            {
                var (dx, dz) = (i * Bodenraster.Abstand - m.X, j * Bodenraster.Abstand - m.Z);
                var w = p.Gewicht(Math.Sqrt(dx * dx + dz * dz));
                if (w <= 0) continue;
                if (p.Filter is { } filter)
                {
                    _aenderung.Vorher(i, j, vorher);
                    if (filter.Boden is { } t && !filter.BodenPasst(t == Bodentextur.Wiese ? 255 - Summe(vorher) : vorher[(int)t - 1])) continue;
                    if (_filter is not null) w *= _filter.SteigungFaktor(i * Bodenraster.Abstand, j * Bodenraster.Abstand);
                    if (w <= 0) continue;
                }
                if (w <= _staerkster.GetValueOrDefault((i, j))) continue;
                _staerkster[(i, j)] = w;
                _aenderung.Vorher(i, j, vorher);
                var a = p.Staerke * w;
                var summe = 0;
                for (var c = 0; c < Bodenraster.DekoKanal; c++)
                {
                    genau[c] = vorher[c] * (1 - a) + (c == gemalt ? 255 * a : 0);
                    neu[c] = (byte)Math.Clamp((int)Math.Round(genau[c], MidpointRounding.AwayFromZero), 0, 255);
                    summe += neu[c];
                }
                // Rundung: nie mehr als 255 zusammen – zuerst bei der gemalten Textur abziehen, sonst beim größten Anteil
                while (summe > 255)
                {
                    var c = gemalt >= 0 && neu[gemalt] > 0 ? gemalt : Groesster(neu);
                    neu[c]--;
                    summe--;
                }
                var d = vorher[Bodenraster.DekoKanal] * (1 - w) + zielDeko * w;
                neu[Bodenraster.DekoKanal] = (byte)Math.Clamp((int)Math.Round(d, MidpointRounding.AwayFromZero), 0, 255);
                _aenderung.Setzen(i, j, neu);
            }
        return new Kartenrechteck(i0 * Bodenraster.Abstand, j0 * Bodenraster.Abstand, i1 * Bodenraster.Abstand, j1 * Bodenraster.Abstand);

        static int Summe(Span<byte> werte)
        {
            var summe = 0;
            for (var c = 0; c < Bodenraster.DekoKanal; c++) summe += werte[c];
            return summe;
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
