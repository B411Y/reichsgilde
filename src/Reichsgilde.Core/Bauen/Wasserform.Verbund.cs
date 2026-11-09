using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Wasserverbund: ein Fluss (oder Bach) und ein See (oder Teich), die sich berühren, sind eine Wasserfläche (Befund 1, Nutzer 09.11.2026;
//  Vorgabe Teamleiter „C1“, Entscheidung zum Spiegel Teamleiter 09.11.2026)
//
//  Bisher rechnete jedes Gewässer sein eigenes Ufer: Lag ein See an einem Fluss, stand zwischen beiden ein Grasstreifen (die Uferrampe des einen lag
//  im anderen), und ein See ohne Wasserstand lag 0,4 m unter seinem tiefsten Ufer, ein Fluss aber um seinen Freibord (0,4 bis 1 m) unter dem Gelände –
//  auf ebenem Land stand der See einen halben Meter über dem Fluss daneben.
//
//  • Berührung: Ein Punkt des Laufs berührt den See, wenn sein Abstand zum Umriss (innen negativ) minus der halben Breite höchstens VerbundSpalt
//    beträgt (Lücke bis 4 m oder schon im See).
//  • Spiegel: Ein See ohne Wasserstand (StandAutomatisch) nimmt den tiefsten Spiegel aller Läufe an seinen Berührstellen an – nie höher als sein
//    eigener (er würde sonst über sein tiefstes Ufer gehoben). Ein See mit festem Wasserstand behält ihn.
//  • Verbund: die Berührstellen, an denen sich der Spiegel des Laufs und der Wasserstand höchstens um VerbundToleranz unterscheiden, als Stücke
//    (Intervalle der Stelle). Nach Nummer des Sees, dann des Laufs, dann Stelle sortiert – keine Reihenfolge aus Dictionary oder HashSet.
//  • Abstandsfeld: sL = Abstand zur Mitte − halbe Breite, sS = Randabstand (innen negativ); gemeinsam das glatte Minimum
//    smin(sL, sS, k = 2 · VerbundSpalt) – so schließt sich eine Lücke bis 4 m. Längs blendet das Gewicht gV (1 in den Stücken, über VerbundAuslauf
//    weich auf 0) zum alten Einzelabstand min(sL, sS) über; quer geht der Anteil hS (0 nahe am Lauf, 1 nahe am See) zwischen Spiegel, Uferbreite und
//    Böschung der beiden über.
//  • Boden: gV · Verbundboden + (1 − gV) · alter Boden (die Höhen, nicht die Abstände, werden überblendet: Ohne Verbund – und am Ende jedes
//    Auslaufs – ist jeder Ort rechnerisch genau wie vorher, ohne Stufe).
//  • An, ImUfer, Uferabstand, Ueberlappt: Wasser ist, wo das Abstandsfeld unter 0 liegt; Spiegel, Strömung und Uferabstand gehen stetig über.
//  Nur Grundrechenarten und Math.Sqrt (Mehrspieler-Gleichheit).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Lauf und ein See, die sich berühren, als eine Wasserfläche (siehe Kopf der Datei Wasserform.Verbund.cs).</summary>
public sealed class Wasserverbund
{
    public Wasserlauf Lauf { get; }

    public Wassersee See { get; }

    /// <summary>Die Stücke des Laufs (Stelle von–bis in Metern), an denen er mit dem See eine Fläche bildet; aufsteigend.</summary>
    public IReadOnlyList<(double Von, double Bis)> Stuecke { get; }

    /// <summary>Das Rechteck, in dem der Verbund wirkt (Stücke mit Auslauf, Lücke und Ufer).</summary>
    public Kartenrechteck Bereich { get; }

    /// <summary>Der größte Unterschied zwischen Spiegel des Laufs und Wasserstand in den Stücken (Meter, für das Protokoll).</summary>
    public double SpiegelUnterschied { get; }

    private readonly int _von, _bis; // Punkte des Laufs, deren Stücke zählen (Stücke samt Auslauf und Reichweite)
    private readonly Dictionary<(int, int), List<int>> _raster = new();
    private const double Feld = 16;

    internal Wasserverbund(Wasserlauf lauf, Wassersee see, List<(double Von, double Bis)> stuecke, double unterschied)
    {
        (Lauf, See, Stuecke, SpiegelUnterschied) = (lauf, see, stuecke, unterschied);
        var p = lauf.Punkte;
        var weit = Wasserform.VerbundAuslauf + Wasserform.VerbundReichweite + p.Max(w => w.Breite);
        var (von, bis) = (stuecke[0].Von - weit, stuecke[^1].Bis + weit);
        _von = 0;
        while (_von + 1 < p.Count && p[_von + 1].Stelle < von) _von++;
        _bis = p.Count - 1;
        while (_bis - 1 > _von && p[_bis - 1].Stelle > bis) _bis--;
        var r = Wasserform.VerbundReichweite + p.Skip(_von).Take(_bis - _von + 1).Max(w => w.Breite) / 2;
        Bereich = (Kartenrechteck.Um(p.Skip(_von).Take(_bis - _von + 1).Select(w => w.Ort)) ?? default).Vergroessert(r);
        for (var k = _von; k < _bis; k++)
        {
            var (a, b) = (p[k].Ort, p[k + 1].Ort);
            for (var i = (int)Math.Floor((Math.Min(a.X, b.X) - r) / Feld); i <= (int)Math.Floor((Math.Max(a.X, b.X) + r) / Feld); i++)
                for (var j = (int)Math.Floor((Math.Min(a.Z, b.Z) - r) / Feld); j <= (int)Math.Floor((Math.Max(a.Z, b.Z) + r) / Feld); j++)
                {
                    if (!_raster.TryGetValue((i, j), out var liste)) _raster[(i, j)] = liste = new List<int>();
                    liste.Add(k);
                }
        }
        // Das geschlossene Ufer des Sees: sein Umriss in Stücken zu höchstens 1 m, ohne die Stücke, an denen der Lauf anliegt (Lücke bis VerbundSpalt) –
        // dort ist im Verbund kein Ufer mehr, und der Grund fällt nicht von dort ab (Befund NAS 09.11.2026: ein flacher Schleier am alten Ufer)
        foreach (var (a, b) in Kartengeometrie.Strecken(see.Umriss))
        {
            var teile = Math.Max(1, (int)Math.Ceiling(a.Abstand(b)));
            for (var t = 0; t < teile; t++)
            {
                var (pa, pb) = (Zwischen(a, b, (double)t / teile), Zwischen(a, b, (double)(t + 1) / teile));
                if (Lage(Zwischen(pa, pb, 0.5)) is { SL: <= Wasserform.VerbundSpalt }) continue;
                _geschlossen.Add((pa, pb));
            }
        }
    }

    private static Kartenort Zwischen(Kartenort a, Kartenort b, double t) => new(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);

    private readonly List<(Kartenort A, Kartenort B)> _geschlossen = new();

    /// <summary>
    /// Abstand zum geschlossenen Ufer des Sees (Meter): zum Umriss ohne die Stücke, an denen der Lauf anliegt; <see cref="double.MaxValue"/>, wenn der Lauf
    /// den ganzen Umriss berührt.
    /// </summary>
    public double SeeuferAbstand(double x, double z)
    {
        var p = new Kartenort(x, z);
        var best = double.MaxValue;
        foreach (var (a, b) in _geschlossen) best = Math.Min(best, Kartengeometrie.AbstandStrecke(p, a, b));
        return best;
    }

    /// <summary>Der gemeinsame Abstand zum Ufer am Ort (Meter, negativ = Wasser) – null, wo dieser Verbund nicht wirkt.</summary>
    public double? AbstandAm(double x, double z) => Lage(new Kartenort(x, z))?.S;

    /// <summary>
    /// Der Anteil des Sees am Ort (0 = Lauf, 1 = See oder Lücke, stetig; ohne das Gewicht längs) und das Gewicht längs (1 im Stück, am Ende des Auslaufs
    /// 0) – null, wo dieser Verbund nicht wirkt. Die Darstellung blendet damit zwischen Verbund und den Regeln der Einzelgewässer über.
    /// </summary>
    public (double Anteil, double Gewicht)? AnteilAm(double x, double z) =>
        Lage(new Kartenort(x, z)) is { } lage ? (Wasserform.AnteilQuer(lage), lage.G) : null;

    /// <summary>Das Gewicht längs (1 in einem Stück, über <see cref="Wasserform.VerbundAuslauf"/> Meter weich auf 0) an dieser Stelle des Laufs.</summary>
    public double Gewicht(double stelle)
    {
        var abstand = double.MaxValue;
        foreach (var (von, bis) in Stuecke)
        {
            if (stelle >= von && stelle <= bis) return 1;
            abstand = Math.Min(abstand, stelle < von ? von - stelle : stelle - bis);
        }
        return 1 - Gelaendeform.Weich(abstand / Wasserform.VerbundAuslauf);
    }

    /// <summary>
    /// Wo der Ort im Verbund liegt – null, wenn der Verbund dort nicht wirkt (außerhalb seines Bereichs, weiter als die Reichweite vom Lauf oder längs
    /// außerhalb des Auslaufs).
    /// </summary>
    internal Verbundlage? Lage(Kartenort p)
    {
        if (!Bereich.Enthaelt(p) || !_raster.TryGetValue(((int)Math.Floor(p.X / Feld), (int)Math.Floor(p.Z / Feld)), out var liste)) return null;
        var best = (K: -1, T: 0.0, D: double.MaxValue);
        foreach (var k in liste)
        {
            var (a, b) = (Lauf.Punkte[k].Ort, Lauf.Punkte[k + 1].Ort);
            var (dx, dz) = (b.X - a.X, b.Z - a.Z);
            var l2 = dx * dx + dz * dz;
            var t = l2 == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Z - a.Z) * dz) / l2, 0, 1);
            var d = p.Abstand(new Kartenort(a.X + t * dx, a.Z + t * dz));
            if (d < best.D) best = (k, t, d);
        }
        if (best.K < 0) return null;
        var w = Lauf.Bei(best.K, best.T);
        var g = Gewicht(w.Stelle);
        if (g <= 0) return null;
        var halb = w.Breite / 2;
        var sL = best.D - halb;
        var rand = See.Randabstand(p);
        var sS = See.Enthaelt(p) ? -rand : rand;
        var k2 = Wasserform.VerbundGlaettung;
        var h = Math.Clamp(0.5 + 0.5 * (sS - sL) / k2, 0, 1); // Anteil von sL am glatten Minimum
        var glatt = sS + (sL - sS) * h - k2 * h * (1 - h);
        var hS = Math.Clamp(0.5 + 0.5 * (sL - sS) / k2, 0, 1); // Anteil des Sees: 1 nahe am See, 0 nahe am Lauf
        return new Verbundlage(w, best.D, sL, sS, glatt, g, hS);
    }
}

/// <summary>
/// Ein Ort im Verbund: der nächste Punkt des Laufs <see cref="W"/> im Abstand <see cref="D"/>, die Einzelabstände <see cref="SL"/> (Lauf) und
/// <see cref="SS"/> (See, innen negativ), das glatte Minimum <see cref="Glatt"/>, das Gewicht längs <see cref="G"/> und der Anteil des Sees quer
/// <see cref="HS"/>.
/// </summary>
internal readonly record struct Verbundlage(Wasserpunkt W, double D, double SL, double SS, double Glatt, double G, double HS)
{
    /// <summary>Der gemeinsame Abstand (negativ = Wasser): längs zum Einzelabstand überblendet.</summary>
    public double S => G * Glatt + (1 - G) * Math.Min(SL, SS);

    /// <summary>Spiegel, Uferbreite und Böschungsanteil des Sees quer überblendet.</summary>
    public double Spiegel(Wassersee see) => HS * see.Wasserstand + (1 - HS) * W.Spiegel;

    public double Uferbreite => HS * Wasserform.SeeUfer + (1 - HS) * Wasserform.Uferbreite(W.Breite);
}

public sealed partial class Wasserform
{
    /// <summary>Bis zu dieser Lücke (Meter, zwischen Ufer des Laufs und Umriss des Sees) bilden Lauf und See eine Fläche.</summary>
    public const double VerbundSpalt = 4.0;

    /// <summary>So viel (Meter) dürfen Spiegel des Laufs und Wasserstand des Sees auseinanderliegen (ein See ohne Wasserstand nimmt den des Laufs an).</summary>
    public const double VerbundToleranz = 0.25;

    /// <summary>So lang (Meter Stelle) geht der Verbund an den Enden seiner Stücke weich in die Einzelgewässer über.</summary>
    public const double VerbundAuslauf = 6.0;

    /// <summary>Breite des glatten Minimums (k = 2 · Spalt): Lücken bis <see cref="VerbundSpalt"/> schließen sich.</summary>
    public const double VerbundGlaettung = 2 * VerbundSpalt;

    /// <summary>So weit (Meter) neben der halben Breite des Laufs wirkt der Verbund höchstens (Spalt, Glättung und Ufer).</summary>
    public const double VerbundReichweite = VerbundSpalt + VerbundGlaettung + 4;

    /// <summary>Die Wasserverbünde (Lauf und See als eine Fläche), nach Nummer des Sees, dann des Laufs.</summary>
    public IReadOnlyList<Wasserverbund> Verbuende => Stand.Verbuende;

    /// <summary>
    /// Die Verbünde und die Seen mit angepasstem Wasserstand: ein See ohne Wasserstand nimmt den tiefsten Spiegel der Läufe an seinen Berührstellen an
    /// (nie höher als seinen eigenen); dann je Paar die Stücke mit Berührung und höchstens <see cref="VerbundToleranz"/> Unterschied im Spiegel.
    /// </summary>
    private static (List<Wassersee>, List<Wasserverbund>) VerbuendeRechnen(List<Wasserlauf> laeufe, List<Wassersee> seen)
    {
        var verbuende = new List<Wasserverbund>();
        if (laeufe.Count == 0 || seen.Count == 0) return (seen, verbuende);
        var neu = new List<Wassersee>(seen.Count);
        foreach (var roh in seen)
        {
            // je Lauf die Berührung jedes Punktes (Lücke bis VerbundSpalt), in der Reihenfolge der Läufe
            var beruehrt = new List<(Wasserlauf Lauf, bool[] Punkte)>();
            var tiefster = double.MaxValue;
            foreach (var l in laeufe)
            {
                if (l.Punkte.Count < 2 || !l.Bereich.Vergroessert(VerbundSpalt).Ueberdeckt(roh.Bereich)) continue;
                var punkte = new bool[l.Punkte.Count];
                var irgendeiner = false;
                for (var i = 0; i < l.Punkte.Count; i++)
                {
                    var w = l.Punkte[i];
                    if (!roh.Bereich.Vergroessert(VerbundSpalt + w.Breite / 2).Enthaelt(w.Ort)) continue;
                    var rand = roh.Randabstand(w.Ort);
                    var sS = roh.Enthaelt(w.Ort) ? -rand : rand;
                    if (sS - w.Breite / 2 > VerbundSpalt) continue;
                    punkte[i] = irgendeiner = true;
                    tiefster = Math.Min(tiefster, w.Spiegel);
                }
                if (irgendeiner) beruehrt.Add((l, punkte));
            }
            // Nach unten bewusst nicht begrenzt (Prüfer 09.11.2026): Schneidet sich der Lauf tief ein (Durchstich), sinkt ein See ohne Wasserstand mit –
            // er liefe sonst in den Lauf aus. Wer das nicht will, gibt dem See einen festen Wasserstand (dann gilt die Toleranz, kein Verbund).
            var see = roh.StandAutomatisch && tiefster < roh.Wasserstand ? roh.MitWasserstand(tiefster) : roh;
            neu.Add(see);
            foreach (var (l, punkte) in beruehrt)
            {
                var stuecke = new List<(double Von, double Bis)>();
                var unterschied = 0.0;
                int? anfang = null;
                for (var i = 0; i <= punkte.Length; i++)
                {
                    var drin = i < punkte.Length && punkte[i] && Math.Abs(l.Punkte[i].Spiegel - see.Wasserstand) <= VerbundToleranz;
                    if (drin)
                    {
                        anfang ??= i;
                        unterschied = Math.Max(unterschied, Math.Abs(l.Punkte[i].Spiegel - see.Wasserstand));
                    }
                    else if (anfang is { } a)
                    {
                        stuecke.Add((l.Punkte[a].Stelle, l.Punkte[i - 1].Stelle));
                        anfang = null;
                    }
                }
                if (stuecke.Count > 0) verbuende.Add(new Wasserverbund(l, see, stuecke, unterschied));
            }
        }
        return (neu, verbuende);
    }

    /// <summary>
    /// Der Boden mit allen Verbünden, die hier wirken: je Verbund gV · Verbundboden + (1 − gV) · alter Boden von Lauf und See. <see cref="Boden"/> nimmt
    /// davon und vom alten Boden aller Gewässer das Minimum: Der Verbund macht nur zusätzlich Wasser – so behält auch eine Schleife desselben Laufs, die
    /// räumlich nah, entlang des Laufs aber weit weg liegt, ihr Bett (Prüfer 09.11.2026; der Verbund kennt nur die Punkte um seine Stücke).
    /// </summary>
    private double VerbundBoden(Kartenort p, double boden, bool wegkern)
    {
        var ziel = boden;
        foreach (var v in Verbuende)
        {
            if (v.Lage(p) is not { } lage) continue;
            var alt = Math.Min(LaufBoden(v.Lauf, p, boden, wegkern), SeeBoden(v.See, p, boden, wegkern));
            var spiegel = lage.Spiegel(v.See);
            var s = lage.Glatt;
            var verbund = boden;
            if (s < 0)
            {
                // im Wasser: die Böschung des Sees (30 %) vom gemeinsamen Ufer, wo der See näher als 4 m (bis 8 m weich) liegt – auch in der Lücke und am
                // Ufer des Laufs zum See hin (sonst bliebe dort eine flache Schwelle, im Bild ein Streifen); am anderen Ufer des Laufs nur sein Querprofil
                verbund = Math.Min(boden, spiegel - Nahe(lage) * Verbundtiefe(v, p, s, lage));
                var halb = lage.W.Breite / 2;
                if (lage.D <= halb) verbund = Math.Min(verbund, lage.W.Spiegel - lage.W.Tiefe * (1 - (lage.D / halb) * (lage.D / halb)));
            }
            else if (!wegkern && boden > spiegel && s < lage.Uferbreite)
                verbund = spiegel + (boden - spiegel) * Gelaendeform.Weich(s / lage.Uferbreite);
            ziel = Math.Min(ziel, lage.G * verbund + (1 - lage.G) * alt);
        }
        return ziel;
    }

    /// <summary>So steil (Meter je Meter) fällt der Grund im Verbund höchstens vom gemeinsamen Ufer ab: in Lücke und Kehle, wo das geschlossene Ufer weit ist.</summary>
    public const double VerbundBoeschung = 2.0;

    /// <summary>
    /// Die Tiefe des Sees im Verbund an einem Ort im Wasser (s &lt; 0): die Böschung des Sees (30 %) vom geschlossenen Ufer, also nicht von den Stücken des
    /// Umrisses, an denen der Lauf anliegt – Lücke und altes Ufer liegen so auf Seetiefe (Befund NAS 09.11.2026: dort war der Grund flach, ein olivgrüner
    /// Schleier im Wasser); zum gemeinsamen Ufer hin höchstens <see cref="VerbundBoeschung"/> steil (am Rand der Lücke 0, ohne Kante zur Uferrampe).
    /// Vom Lauf weg (8 bis 16 m hinter seinem Ufer) geht die Tiefe in die des Sees allein über: Am Rand des Bereichs, in dem der Verbund wirkt, ist sie
    /// genau die alte (Prüfer 09.11.2026: bei einem tiefen See sprang der Grund dort sonst um mehrere Meter).
    /// </summary>
    private static double Verbundtiefe(Wasserverbund v, Kartenort p, double s, Verbundlage lage)
    {
        var verbund = Math.Min(v.See.Tiefe, Math.Min(v.SeeuferAbstand(p.X, p.Z) * Seeboeschung, -s * VerbundBoeschung));
        var allein = v.See.Enthaelt(p) ? Math.Min(v.See.Tiefe, v.See.Randabstand(p) * Seeboeschung) : 0;
        return allein + (verbund - allein) * (1 - Gelaendeform.Weich((lage.SL - VerbundSpalt * 2) / (VerbundSpalt * 2)));
    }

    /// <summary>Wie nah der See liegt: 1 bis <see cref="VerbundSpalt"/> (und im See), weich auf 0 beim Doppelten.</summary>
    private static double Nahe(Verbundlage lage) => 1 - Gelaendeform.Weich((lage.SS - VerbundSpalt) / VerbundSpalt);

    /// <summary>
    /// <see cref="An"/> mit den Verbünden: Wo das gemeinsame Abstandsfeld unter 0 liegt, ist Wasser – Gewässer das mit dem kleineren eigenen Abstand
    /// (Gleichstand: kleinere Nummer), Spiegel quer überblendet, Strömung des Laufs mal (1 − Anteil des Sees), Uferabstand −s. Ein anderes Gewässer
    /// mit tieferem Spiegel behält Vorrang (wie bei einer Mündung).
    /// </summary>
    private Wasserstelle? VerbundAn(Kartenort p, Wasserstelle? best)
    {
        foreach (var v in Verbuende)
        {
            if (v.Lage(p) is not { } lage || lage.S >= 0) continue;
            var laufGilt = lage.SL < lage.SS || (lage.SL == lage.SS && v.Lauf.Nummer < v.See.Nummer);
            var halb = lage.W.Breite / 2;
            var profil = lage.D < halb ? 1 - 0.5 * (lage.D / halb) * (lage.D / halb) : 0.5;
            var tempo = v.Lauf.Stroemung * profil * (1 - AnteilQuer(lage));
            var (spiegel, sx, sz) = (lage.Spiegel(v.See), lage.W.RichtungX * tempo, lage.W.RichtungZ * tempo);
            // längs mit dem Gewicht in das, was Lauf und See allein ergäben (Prüfer 09.11.2026: sonst sprangen Spiegel um bis zu 8 cm und Strömung am Ende
            // des Auslaufs); wo allein kein Wasser war (Lücke), bleibt der Verbund
            var eigenes = best is { } b && (ReferenceEquals(b.Lauf, v.Lauf) || ReferenceEquals(b.See, v.See));
            if (eigenes)
            {
                var alt = best!.Value;
                (spiegel, sx, sz) = (lage.G * spiegel + (1 - lage.G) * alt.Spiegel, lage.G * sx + (1 - lage.G) * alt.StroemungX, lage.G * sz + (1 - lage.G) * alt.StroemungZ);
            }
            var stelle = new Wasserstelle(laufGilt ? v.Lauf : null, laufGilt ? null : v.See, spiegel, sx, sz, -lage.S);
            if (best is null || eigenes || spiegel < best.Value.Spiegel) best = stelle;
        }
        return best;
    }

    /// <summary>
    /// Anteil des Sees an einem Ort im Verbund (längs mal gV): 1 in Lücke und See außerhalb des Laufs, weich über den letzten Meter seines Betts – nur,
    /// wo der See näher als <see cref="VerbundSpalt"/> (bis zum Doppelten weich) liegt, am anderen Ufer bleibt alles wie ohne Verbund; dazu 1, wo der
    /// Lauf tief im See liegt (der See 6 m tiefer innen als der Lauf: dort geht der Lauf im See auf, etwa an seinem Ende).
    /// </summary>
    internal static double Anteil(Verbundlage lage) => lage.G * AnteilQuer(lage);

    /// <summary>Der Anteil des Sees quer, ohne das Gewicht längs (siehe <see cref="Anteil"/>).</summary>
    internal static double AnteilQuer(Verbundlage lage) =>
        Math.Max(Gelaendeform.Weich((lage.SL + 1) / 1.5) * (1 - Gelaendeform.Weich((lage.SS - VerbundSpalt) / VerbundSpalt)),
            Gelaendeform.Weich((lage.SL - lage.SS - 2) / 4));

    /// <summary>
    /// Der Anteil des Sees an einem Ort (0 = Lauf, 1 = See oder Lücke, stetig; 0 außerhalb jedes Verbunds) – für die Darstellung: Dort übernimmt die
    /// Fläche des Verbunds, und Eis wächst wie im See.
    /// </summary>
    public double Verbundanteil(double x, double z)
    {
        var p = new Kartenort(x, z);
        var anteil = 0.0;
        foreach (var v in Verbuende)
            if (v.Lage(p) is { } lage) anteil = Math.Max(anteil, Anteil(lage));
        return anteil;
    }

    /// <summary>Der gemeinsame Abstand zum Ufer (Meter, negativ = im Wasser) des ersten Verbunds, der hier wirkt – null außerhalb jedes Verbunds.</summary>
    public double? Verbundabstand(double x, double z)
    {
        var p = new Kartenort(x, z);
        foreach (var v in Verbuende)
            if (v.Lage(p) is { } lage) return lage.S;
        return null;
    }

    private bool VerbundImUfer(Kartenort p)
    {
        foreach (var v in Verbuende)
            if (v.Lage(p) is { } lage && lage.S <= lage.Uferbreite) return true;
        return false;
    }

    private double VerbundUferabstand(Kartenort p)
    {
        var best = double.MaxValue;
        foreach (var v in Verbuende)
            if (v.Lage(p) is { } lage) best = Math.Min(best, Math.Max(0, lage.S));
        return best;
    }

    /// <summary>Überdeckt ein Umriss Wasser eines Verbunds (Ecken und Mitte geprüft, etwa ein Haus in der geschlossenen Lücke)?</summary>
    private (string Name, Kartenort Ort)? VerbundUeberlappt(IReadOnlyList<Kartenort> umriss, Kartenrechteck rahmen)
    {
        foreach (var v in Verbuende)
        {
            if (!v.Bereich.Ueberdeckt(rahmen.Vergroessert(0.01))) continue;
            foreach (var e in umriss.Append(rahmen.Mitte))
                if (v.Lage(e) is { S: < 0 }) return (v.Lauf.Name, e);
        }
        return null;
    }
}
