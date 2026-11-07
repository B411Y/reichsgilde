using System.Globalization;
using Godot;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Diebe weichen der Streife aus (Teil der <c>partial class Stadtansicht</c>; Wachen und Diebe, Nutzer 07.11.2026; nur Bild – der Kern merkt nichts, die Tat
/// und ihr Ergebnis bleiben, wie sie sind). Die Szenen der Diebe sind reine Funktionen der Spielzeit entlang fester Wege (Taschendiebe, Nachtdiebe); die Streife
/// ist es auch: Ihr Ort folgt allein aus Runde, Platz in der Reihe, Wachwechsel und Uhrzeit (<see cref="StreifeOrtBei"/>, dieselbe Rechnung wie bei den
/// Wachleuten selbst). Deshalb wird <b>beim Aufbau einer Szene einmal</b> vorausgerechnet (<see cref="AusweichenPlanen"/>): Kommt eine sichtbare Streife dem Dieb
/// zwischen zwei Taten näher als <see cref="Ausweichgang.Radius"/> Meter, bekommt die Szene eine zweite, kurze <see cref="Ausweichbahn"/>: Der Dieb tritt in eine
/// Seitengasse (ein Knoten des Gassennetzes, der nicht auf seinem Weg liegt) bzw. am Markt auf einen freien Platz abseits, steht dort im Schatten, bis die Streife
/// vorbei ist, und ist am Ende wieder genau dort auf seinem Weg, wo er ohne Ausweichen wäre. Je Bild gibt es keinen Abgleich der Figuren: nur die Stelle auf der Bahn.
/// <list type="bullet">
/// <item>Nur die Wege zwischen zwei Taten: das Anschleichen (nicht in der Schlussphase, zur Tat steht er immer am Ziel), der Abgang, die Flucht bei Nacht.</item>
/// <item>Nur Wachleute, die zum Zeitpunkt des Aufbaus zu sehen sind und auf ihrer Runde gehen; höchstens ein Ausweichen je Weg.</item>
/// <item>Die Zeit der Streife in der Zukunft gilt nur bis zum Wachwechsel und bis 23 Uhr (danach kennt der Kern die Schicht nicht).</item>
/// </list>
/// Testhilfe: <c>--diebtest=ausweichen|ausweichnacht</c> (Hauptszene.Diebtest.cs) und <c>--streifenversatz=&lt;Spielminuten&gt;</c> (die Streife geht um so viel früher bzw. später
/// los – nur Bild, damit ein Bild die Begegnung trifft), siehe <c>docs/STARTOPTIONEN.md</c>.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Startoption <c>--streifenversatz=&lt;Spielminuten&gt;</c> (nur Bild): Wo die Streife auf ihrer Runde ist, rechnet sich um so viele Minuten später aus (Test).</summary>
    private static readonly double StreifenVersatz =
        double.TryParse(Wasserarten.Startoption("streifenversatz"), NumberStyles.Float, CultureInfo.InvariantCulture, out var versatz) ? versatz : 0;

    /// <summary>Wie weit ein Dieb seitlich ausweicht, wenn sich keine Seitengasse findet (Meter): in den Schatten der Hauswand am Rand der Gasse.</summary>
    private const float SchattenAbstand = 1.6f;

    /// <summary>Test (<c>--diebtest=ausweichen|ausweichnacht</c>): Die Zeit hält dreimal 6 s an – der Dieb tritt zur Seite, steht im Schatten, kehrt zurück – für Bilder.</summary>
    public bool AusweichFolge { get; set; }
    private int _ausweichHaltStufe;

    /// <summary>Spielminute und Uhrzeit (Minuten im Tag) des laufenden Bildes: damit rechnet die Streife zu einer anderen Zeit aus (gesetzt von den Aktualisierungen der Diebe).</summary>
    private double _ausweichJetzt, _ausweichUhr;

    /// <summary>Die zweite Bahn eines Diebes: vom Weg zu einem Seitenpunkt, dort stehen, zurück auf den Weg. Die Zeiten stehen im Takt der Szene (<see cref="Ausweichgang.Fenster"/>).</summary>
    private sealed class Ausweichbahn
    {
        public required Ausweichgang.Fenster Fenster { get; init; }
        public required Vector3[] Aus { get; init; }
        public required Vector3[] Zurueck { get; init; }
        public required float AusLaenge { get; init; }
        public required float ZurLaenge { get; init; }
        /// <summary>Der Seitenpunkt und die Blickrichtung dort (weg von der Streife).</summary>
        public required Vector3 Seite { get; init; }
        public required Vector3 Blick { get; init; }
        public double Hinweg => AusLaenge / Math.Max(0.01, AusLaenge + ZurLaenge);
    }

    /// <summary>Wo ein Wachmann zur Uhrzeit <paramref name="uhr"/> (Minuten im Tag) ist; null, wenn er dann nicht (mehr) auf seiner Runde geht (anderer Wachwechsel, Tagesende).</summary>
    private static Vector3? StreifeOrtBei(Wachgang g, double uhr, double sekundenJeMinute)
    {
        if (g.Runde is not { } runde || g.Laenge < 1) return null;
        if (uhr < Spielzeit.TagesbeginnMinute || uhr >= Spielzeit.TagesendeMinute || (int)(uhr / 120) != g.Wechsel) return null;
        var meter = StreifeMeter(uhr - g.Wechsel * 120, g.Reihe, sekundenJeMinute);
        if (meter < 0) return null;
        return Gassennetz.PunktAuf(runde, meter % g.Laenge / g.Laenge, g.Laenge);
    }

    private static float Abstand2D(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    /// <summary>
    /// Plant das Ausweichen auf einem Weg, einmal beim Aufbau der Szene. <paramref name="route"/> ist der Weg des Diebes, <paramref name="meterBei"/> wie weit er zur Zeit
    /// <c>t</c> (im Takt der Szene) darauf ist (null, wo nicht ausgewichen werden darf: Schlussphase, Tat), <paramref name="von"/> bis <paramref name="bis"/> der erlaubte
    /// Zeitraum, <paramref name="versatz"/> der Unterschied zur Spielminute (Spielminute = t + versatz). <paramref name="platz"/>: auf dem Pflaster des Marktes wählt es einen
    /// freien Punkt abseits, sonst (Gassen) eine Seitengasse oder den Schatten am Rand. Null, wenn keine sichtbare Streife nah genug kommt.
    /// </summary>
    private Ausweichbahn? AusweichenPlanen(Vector3[] route, Func<double, double?> meterBei, double von, double bis, double versatz, MarktplatzAnsicht? platz, string was)
    {
        if (_simulation is null || route.Length < 2 || bis - von < Ausweichgang.MinDauer) return null;
        var streifen = _wachgaenge.Values.Where(g => g.Sichtbar && g.Runde is not null).ToList();
        if (streifen.Count == 0) return null;
        var laenge = Gassennetz.Laenge(route);
        if (laenge < 1) return null;
        var sekundenJeMinute = _simulation.Takt.RundenDauerSekunden / Spielzeit.MinutenProTag;
        Vector3? Streife(Wachgang g, double t) => StreifeOrtBei(g, _ausweichUhr + (t + versatz - _ausweichJetzt), sekundenJeMinute);
        Vector3? Dieb(double t) => meterBei(t) is { } m ? Gassennetz.PunktAuf(route, (float)(m / laenge), laenge) : null;
        var naechsterAbstand = float.MaxValue;
        float Abstand(double t)
        {
            if (Dieb(t) is not { } p) return float.MaxValue;
            var best = float.MaxValue;
            foreach (var g in streifen)
                if (Streife(g, t) is { } q) best = Math.Min(best, Abstand2D(p, q));
            naechsterAbstand = Math.Min(naechsterAbstand, best);
            return best;
        }
        if (Ausweichgang.Finden(von, bis, Abstand) is not { } fenster)
        {
            if (DiebLog) GD.Print($"[ausweichen] {was}: kein Ausweichen, die Streife kommt dem Dieb höchstens auf {(naechsterAbstand > 9000 ? "– (keine in Sicht)" : naechsterAbstand.ToString("0") + " m")} nah ({streifen.Count} Wachleute zu sehen)");
            return null;
        }
        if (meterBei(fenster.Von) is not { } mA || meterBei(fenster.Naechste) is not { } mM || meterBei(fenster.Bis) is not { } mB) return null;
        var am = Gassennetz.PunktAuf(route, (float)(mM / laenge), laenge);
        Vector3? streife = null;
        var streifeAbstand = float.MaxValue;
        foreach (var g in streifen)
            if (Streife(g, fenster.Naechste) is { } q && Abstand2D(am, q) < streifeAbstand) { streife = q; streifeAbstand = Abstand2D(am, q); }
        var s = streife ?? am + Vector3.Back * 8;

        // Seitenpunkt und der kurze Weg dorthin (ab dem Punkt der Route, an dem er abzweigt)
        float mQ = (float)mM;
        Vector3[] zumPunkt;
        Vector3 seite;
        if (platz is not null && MarktSeite(platz, am, s) is { } frei) { seite = frei; zumPunkt = WegAuf(platz, am, frei, am.Y); }
        else if (platz is null && GassenSeite(route, (float)mA, (float)mB, (float)mM, s) is { } gasse) { (seite, zumPunkt, mQ) = gasse; }
        else
        {
            // keine Seitengasse und kein freier Punkt: in den Schatten am Rand der Gasse, rechtwinklig zum Weg, weg von der Streife
            var vorn = Gassennetz.PunktAuf(route, Mathf.Min(1, (float)(mM / laenge) + 0.5f / laenge), laenge) - am;
            vorn.Y = 0;
            var quer = vorn.LengthSquared() > 1e-6f ? new Vector3(-vorn.Z, 0, vorn.X).Normalized() : Vector3.Right;
            if (quer.Dot(am - s) < 0) quer = -quer;
            seite = am + quer * SchattenAbstand;
            zumPunkt = new[] { am, seite };
        }
        var aus = Verbinden(RouteTeil(route, (float)mA, mQ), zumPunkt);
        var zurueck = Verbinden(zumPunkt.Reverse().ToArray(), RouteTeil(route, mQ, (float)mB));
        var weg = seite - s;
        weg.Y = 0;
        weg = weg.LengthSquared() > 1e-6f ? weg.Normalized() : Vector3.Forward;
        if (DiebLog) GD.Print($"[ausweichen] {was}: weicht aus, Spielminute {fenster.Von:0.0} bis {fenster.Bis:0.0} (Nähe um {fenster.Naechste:0.0}, Streife {streifeAbstand:0} m), Seitenpunkt {seite.X:0}|{seite.Z:0}, hin {Gassennetz.Laenge(aus):0} m, zurück {Gassennetz.Laenge(zurueck):0} m");
        return new Ausweichbahn
        {
            Fenster = fenster, Aus = aus, Zurueck = zurueck, AusLaenge = Gassennetz.Laenge(aus), ZurLaenge = Gassennetz.Laenge(zurueck), Seite = seite, Blick = weg,
        };
    }

    /// <summary>Hängt zwei Wege aneinander (der Anfang des zweiten ist das Ende des ersten).</summary>
    private static Vector3[] Verbinden(Vector3[] a, Vector3[] b)
    {
        var liste = new List<Vector3>(a);
        foreach (var p in b)
            if (liste.Count == 0 || liste[^1].DistanceTo(p) > 0.01f) liste.Add(p);
        if (liste.Count == 1) liste.Add(liste[0]);
        return liste.ToArray();
    }

    /// <summary>Das Stück einer Route von <paramref name="m1"/> bis <paramref name="m2"/> Metern (m1 kleiner als m2; sonst ein Punkt).</summary>
    private static Vector3[] RouteTeil(Vector3[] route, float m1, float m2)
    {
        var laenge = Gassennetz.Laenge(route);
        if (m2 - m1 < 0.05f) { var p = Gassennetz.PunktAuf(route, m1 / laenge, laenge); return new[] { p, p }; }
        return WegAb(WegBis(route, m2), m1);
    }

    /// <summary>Ein freier Punkt auf dem Pflaster 3,5 bis 5,5 m neben <paramref name="am"/>, möglichst weit von der Streife (Schatten eines Standes, Rand); null, wenn keiner frei ist.</summary>
    private static Vector3? MarktSeite(MarktplatzAnsicht platz, Vector3 am, Vector3 streife)
    {
        if (platz.Flaeche is not { } f) return null;
        Vector3? beste = null;
        var weit = float.MinValue;
        foreach (var radius in new[] { 3.5f, 5.5f })
            for (var i = 0; i < 8; i++)
            {
                var w = i * Mathf.Pi / 4;
                var p = am + new Vector3(Mathf.Cos(w), 0, Mathf.Sin(w)) * radius;
                if (!f.Auf(p.X, p.Z, 0.5f) || f.Frei(p.X, p.Z) < 0.8f) continue;
                var d = Abstand2D(p, streife);
                if (d > weit) { weit = d; beste = new Vector3(p.X, platz.Hoehe, p.Z); }
            }
        return beste != null && weit > Abstand2D(am, streife) ? beste : null;
    }

    /// <summary>
    /// Eine Seitengasse: ein Knoten der Route zwischen den Metern <paramref name="mA"/> und <paramref name="mB"/>, von dem ein Nachbarknoten abzweigt, der nicht auf der Route liegt,
    /// 2,5 bis 14 m weit ist und weiter von der Streife weg als der Knoten selbst. Gewählt wird der mit dem größten Abstand zur Streife, bei Gleichstand der näher an der Stelle der
    /// größten Nähe (<paramref name="mM"/>). Ergebnis: der Seitenpunkt, der Weg vom Knoten dorthin und der Meter des Knotens auf der Route; null, wenn keine Seitengasse da ist.
    /// </summary>
    private (Vector3 Punkt, Vector3[] Weg, float Meter)? GassenSeite(Vector3[] route, float mA, float mB, float mM, Vector3 streife)
    {
        (Vector3, Vector3[], float)? beste = null;
        var wert = float.MinValue;
        var meter = 0f;
        for (var i = 0; i < route.Length; i++)
        {
            if (i > 0) meter += route[i - 1].DistanceTo(route[i]);
            if (meter < mA + 0.3f || meter > mB - 0.3f) continue;
            var k = _netz.KnotenGenau(route[i]);
            if (k < 0) continue;
            foreach (var nb in _netz.NachbarnVon(k))
            {
                var q = _netz.Knoten[nb];
                var len = Abstand2D(q, route[i]);
                if (len < 2.5f || len > 14f || route.Any(r => Abstand2D(r, q) < 1f)) continue;
                var abstand = Abstand2D(q, streife);
                if (abstand < Abstand2D(route[i], streife)) continue;
                var w = abstand - 0.3f * Math.Abs(meter - mM);
                if (w > wert) { wert = w; beste = (q, new[] { route[i], q }, meter); }
            }
        }
        return beste;
    }

    /// <summary>
    /// Wo der Dieb zur Zeit <paramref name="t"/> auf seiner Ausweichbahn ist; false, wenn er nicht ausweicht (er geht seinen Weg). <paramref name="kniend"/>: nachts duckt er sich
    /// am Seitenpunkt. <paramref name="tempo"/>: Meter je echter Sekunde bei Tempo 1, schon auf <c>GehenTempo</c> umgerechnet, wenn die Bewegung „Eilen“ ist.
    /// </summary>
    private bool AusweichenStellen(Ausweichbahn b, double t, double sekundenJeMinute, bool kniend, bool eigen, out Vector3 ort, out Vector3 blick, out string bewegung, out float tempo)
    {
        (ort, blick, bewegung, tempo) = (default, default, "Gehen", 0f);
        var phase = Ausweichgang.Bei(b.Fenster, t, b.Hinweg, out var anteil);
        if (phase is Ausweichgang.Phase.Vorher or Ausweichgang.Phase.Nachher) return false;
        if (phase == Ausweichgang.Phase.Halt)
        {
            (ort, blick, bewegung) = (b.Seite, b.Blick, kniend ? "Knien" : "Stehen");
        }
        else
        {
            var weg = phase == Ausweichgang.Phase.Hinaus ? b.Aus : b.Zurueck;
            var laenge = phase == Ausweichgang.Phase.Hinaus ? b.AusLaenge : b.ZurLaenge;
            ort = Gassennetz.PunktAuf(weg, (float)anteil, laenge);
            blick = Gassennetz.PunktAuf(weg, Mathf.Min(1, (float)anteil + 0.5f / Mathf.Max(1, laenge)), laenge) - ort;
            var anteilZeit = (1 - Ausweichgang.HaltAnteil) * (phase == Ausweichgang.Phase.Hinaus ? Math.Clamp(b.Hinweg, 0.1, 0.9) : 1 - Math.Clamp(b.Hinweg, 0.1, 0.9));
            var minuten = Math.Max(0.1, (b.Fenster.Bis - b.Fenster.Von) * anteilZeit);
            var mps = (float)(laenge / (minuten * sekundenJeMinute));
            bewegung = mps > Gehtempo.Gehen * 1.7 ? "Eilen" : "Gehen";
            tempo = bewegung == "Eilen" ? mps * GehenTempo / EilenTempo : mps; // DiebStellen teilt durch GehenTempo
        }
        if (AusweichFolge && eigen && _haltBis == 0 && _ausweichHaltStufe < 3)
        {
            var da = _ausweichHaltStufe switch
            {
                0 => phase == Ausweichgang.Phase.Hinaus && anteil >= 0.6,
                1 => phase == Ausweichgang.Phase.Halt && anteil >= 0.5,
                _ => phase == Ausweichgang.Phase.Zurueck && anteil >= 0.4,
            };
            if (da)
            {
                _haltBis = Time.GetTicksMsec() + 6000;
                _simulation!.Takt.IstPausiert = true;
                GD.Print($"[ausweichen] Halt {_ausweichHaltStufe} {Time.GetUnixTimeFromSystem():0.0} {phase} {anteil:0.00}");
                DiebBildAblegen($"aus{_ausweichHaltStufe}", () => _diebfiguren.Values.Where(d => d.Sichtbar && d.Eigen && d.Zustand.StartsWith("weicht", StringComparison.Ordinal)).Take(1).Select(d => d.Knoten)
                    .Concat(_wachgaenge.Values.Where(g => g.Sichtbar).Select(g => g.Knoten)));
                _ausweichHaltStufe++;
            }
        }
        return true;
    }
}
