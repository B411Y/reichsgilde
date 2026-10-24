using System.Threading.Tasks;
using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Die Wegeschicht (Kartenwerkstatt, Schritt 5g) – ein Teil der <c>partial class Stadtansicht</c>. Wege liegen nicht mehr als Bänder über dem Boden,
/// sie sind wie im Warcraft-3-Editor in den Boden eingebacken:
/// <list type="bullet">
/// <item><b>Schicht je Geländekachel</b> (<see cref="SchichtKachelRechnen"/>): ein Bild zu 0,25 m je Bildpunkt (514 × 514 mit Rand) mit dem Abstand zur
/// nächsten Wegmitte (R), dem Abstand zum Wegrand (G, innen negativ) und der Belagstufe (B: 0 Trampelpfad, 1 Fahrweg, 2 Kies, 3 Pflaster, 4 Baustelle;
/// an Abschnittsgrenzen fließend, Pflaster neben Erde hart; A frei). Ein zweites, grobes Bild (1 m) trägt Austritt, Zustand, den Stadtgrund (B: in der
/// Mauer Lehm, freie Plätze ausgespart; Wege darauf sind gepflasterte Gassen) und den Abstand zur Erde (A, 0–4 m: dort Erde auf dem Pflaster).
/// Ein drittes Bild (0,25 m) trägt die Spurlinien der Karren (Schritt 5j, Stadtansicht.Spurrillen.cs).</item>
/// <item><b>Vereinigung:</b> Alle Wege und die Stücke zu Höfen, Karrenplätzen und Gattern (<see cref="SchichtAnschlussMerken"/>) gehen in ein
/// Abstandsfeld ein. Innerhalb eines Weges gilt der kleinste Abstand, zwischen Wegen ein weiches Minimum (<see cref="SchichtRundung"/>):
/// Kreuzungen verschmelzen von selbst, die Ecken runden sich. Wo zwei Wege sich decken, gewinnt der stärkere Belag (Pflaster vor Kies vor Fahrweg vor Pfad).
/// Unter Brücken (<c>WegLuecken</c>) bleibt der Weg aus.</item>
/// <item><b>Zeichnen:</b> <c>boden.gdshader</c> mischt die Schicht in den Boden, die Ränder je Belag. Hat die Karte Wege, trägt jede Kachel den
/// Boden-Shader (auch eine flache Karte wie die Greybox), sonst bleibt alles wie bisher.</item>
/// <item><b>Neu rechnen</b> (<see cref="WegeschichtBild"/>): nur die Kacheln, die eine Änderung berührt (Weg in der Werkstatt, <c>BelagGeaendert</c>
/// im Spiel), parallel; einmal je Bild.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Größe eines Bildpunkts der Schicht (Meter) und Bildgröße je Kachel (128 m plus je ein Punkt Rand).</summary>
    private const float SchichtTexel = 0.25f;
    private const int SchichtBild = (int)(KachelZellen * 2 / SchichtTexel) + 2;

    /// <summary>Das grobe Bild (Austritt, Zustand): 1 m je Punkt, mit Rand.</summary>
    private const int SchichtInfoBild = KachelZellen * 2 + 2;

    /// <summary>So weit neben dem Rand rechnet die Schicht (Ränder, niedergetretenes Gras, Rundung).</summary>
    private const float SchichtRand = 2f;

    /// <summary>Wertebereiche im Bild: Abstand zur Mitte 0 … 10 m, Abstand zum Rand −6 … +2 m.</summary>
    private const float SchichtMitteMax = 10f, SchichtRandMin = -6f;

    /// <summary>Weiches Minimum zwischen zwei Wegen (Meter): so stark runden sich die Ecken an Kreuzungen.</summary>
    private const float SchichtRundung = 1.6f;

    /// <summary>Abtastung des Belags entlang eines Weges (Meter).</summary>
    private const float SchichtBelagSchritt = 0.5f;

    /// <summary>Die Bilder der Schicht je Kachel, die einen Weg hat.</summary>
    private readonly Dictionary<(int I, int J), (ImageTexture Bild, ImageTexture Info, ImageTexture Spur)> _schichtKacheln = new();

    /// <summary>Kacheln, die neu gerechnet werden müssen (oder alle).</summary>
    private readonly HashSet<(int I, int J)> _schichtVeraltet = new();
    private bool _schichtAlles;

    /// <summary>Die Stücke zu Höfen, Karrenplätzen und Gattern (sonst als Quader gezeichnet).</summary>
    private readonly List<(Vector2 Von, Vector2 Bis, float Breite)> _schichtAnschluesse = new();

    /// <summary>Der Rahmen jedes Weges bzw. jeder Mauer der Werkstatt beim letzten Rechnen (so wird auch die alte Stelle neu gerechnet).</summary>
    private readonly Dictionary<object, Rect2> _schichtRahmen = new();

    /// <summary>Kann die Ansicht Wege in den Boden einbacken? (Der Boden-Shader fehlt nur in einem kaputten Export – dann Bänder wie früher.)</summary>
    private bool WegeschichtMoeglich => BodenShaderLaden();

    /// <summary>Hat die Karte etwas für die Schicht (Wege oder Pflaster in einer Mauer)? Dann trägt jede Kachel den Boden-Shader.</summary>
    private bool WegeschichtAn => WegeschichtMoeglich && (_entwurf is { } e
        ? e.Wege.Count > 0 || e.Mauern.Any(m => m.Ecken.Count >= 3)
        : _karte is { } k && (k.Wege.Count > 0 || _mauerFlaechen.Any(f => f.Length >= 3)));

    /// <summary>Ein Teil der Schicht: ein Weg (Linie mit Belag je halbem Meter) oder eine Pflasterfläche (Vieleck).</summary>
    private sealed class Schichtteil
    {
        public Vector2[] Linie = Array.Empty<Vector2>();
        public float[] Stelle = Array.Empty<float>();
        public float Halb;
        public bool Flaeche;
        /// <summary>Eine Fläche, die aus dem Stadtgrund ausgespart ist (ein freier Platz: Wiese statt Lehm).</summary>
        public bool Loch;
        public Rect2 Rahmen;

        /// <summary>Belag je <see cref="SchichtBelagSchritt"/> Meter: Stufe (NaN = Lücke, etwa eine Brücke), Austritt, Zustand.</summary>
        public float[] Stufe = Array.Empty<float>(), Austritt = Array.Empty<float>(), Zustand = Array.Empty<float>();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Melden
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Kacheln in diesem Rahmen (Meter) beim nächsten Bild neu rechnen.</summary>
    private void SchichtVeraltet(Rect2 rahmen)
    {
        const float kachel = KachelZellen * 2f;
        var r = rahmen.Grow(SchichtRand + SchichtTexel);
        for (var j = Mathf.FloorToInt(r.Position.Y / kachel); j <= Mathf.FloorToInt(r.End.Y / kachel); j++)
            for (var i = Mathf.FloorToInt(r.Position.X / kachel); i <= Mathf.FloorToInt(r.End.X / kachel); i++)
                _schichtVeraltet.Add((i, j));
    }

    /// <summary>Ein Teil (Weg oder Mauer der Werkstatt) hat sich geändert: alte und neue Stelle neu rechnen. Ohne <paramref name="punkte"/> ist er gelöscht.</summary>
    private void SchichtTeilGeaendert(object teil, IEnumerable<Vector2>? punkte, float halb)
    {
        if (_schichtRahmen.Remove(teil, out var alt)) SchichtVeraltet(alt);
        if (punkte is null || Rahmen(punkte, halb) is not { } neu) return;
        _schichtRahmen[teil] = neu;
        SchichtVeraltet(neu);
    }

    private static Rect2? Rahmen(IEnumerable<Vector2> punkte, float zugabe)
    {
        Rect2? r = null;
        foreach (var p in punkte) r = r is { } x ? x.Expand(p) : new Rect2(p, Vector2.Zero);
        return r?.Grow(zugabe);
    }

    /// <summary>Ein Stück zu einem Hof, Karrenplatz oder Gatter geht in die Schicht (statt eines Quaders, <see cref="WegZiehen"/>).</summary>
    private void SchichtAnschlussMerken(Vector3 von, Vector3 bis, float breite)
    {
        _schichtAnschluesse.Add((new Vector2(von.X, von.Z), new Vector2(bis.X, bis.Z), breite));
        SchichtVeraltet(new Rect2(new Vector2(von.X, von.Z), Vector2.Zero).Expand(new Vector2(bis.X, bis.Z)).Grow(breite / 2));
    }

    /// <summary>Die Werkstatt baut ihre Anschlüsse neu: die alten vergessen (ihre Stellen werden neu gerechnet).</summary>
    private void SchichtAnschluesseVergessen()
    {
        foreach (var (von, bis, breite) in _schichtAnschluesse) SchichtVeraltet(new Rect2(von, Vector2.Zero).Expand(bis).Grow(breite / 2));
        _schichtAnschluesse.Clear();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Rechnen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Jedes Bild: veraltete Kacheln neu rechnen (parallel), ihre Bilder ersetzen und den Kacheln den Stoff geben.</summary>
    private void WegeschichtBild()
    {
        if (!_schichtAlles && _schichtVeraltet.Count == 0) return;
        if (_grenzen is null || !WegeschichtMoeglich)
        {
            _schichtAlles = false;
            _schichtVeraltet.Clear();
            return;
        }
        if (_entwurf is not null && _randKnoten is not null && BodenKacheln != _bodenGebaut)
        {
            RandNeuBauen(); // der erste Weg auf einer Karte ohne Kacheln (oder der letzte ist weg): der Boden wird in Kacheln gezeichnet
            _schichtAlles = true;
        }
        var (a0, b0, a1, b1) = KachelBereich(_grenzen.Aussen);
        var liste = new List<(int I, int J)>();
        if (_schichtAlles)
            for (var j = b0; j <= b1; j++)
                for (var i = a0; i <= a1; i++) liste.Add((i, j));
        else liste.AddRange(_schichtVeraltet.Where(k => k.I >= a0 && k.I <= a1 && k.J >= b0 && k.J <= b1));
        var alles = _schichtAlles;
        _schichtAlles = false;
        _schichtVeraltet.Clear();

        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var (teile, spuren) = SchichtTeile();
        var ergebnis = new (byte[] Bild, byte[] Info, byte[] Spur)?[liste.Count];
        var bueschel = new Vector4[]?[liste.Count];
        var raster = BodenRaster;
        Parallel.For(0, liste.Count, k =>
        {
            ergebnis[k] = SchichtKachelRechnen(liste[k].I, liste[k].J, teile, spuren);
            // Grasbüschel am Wegrand (Stadtansicht.Bueschel.cs), solange das Bild noch da ist
            if (ergebnis[k] is { } d) bueschel[k] = BueschelAusWegen(liste[k].I, liste[k].J, d.Bild, d.Info, raster);
        });
        for (var k = 0; k < liste.Count; k++)
        {
            var kachel = liste[k];
            if (ergebnis[k] is not { } daten)
            {
                _schichtKacheln.Remove(kachel);
                _grasStadt.Remove(kachel);
            }
            else
            {
                var bild = Image.CreateFromData(SchichtBild, SchichtBild, false, Image.Format.Rgba8, daten.Bild);
                bild.GenerateMipmaps(); // weit weg mittelt sich der Abstand: kein Flimmern am Rand
                var info = Image.CreateFromData(SchichtInfoBild, SchichtInfoBild, false, Image.Format.Rgba8, daten.Info);
                var spur = Image.CreateFromData(SchichtBild, SchichtBild, false, Image.Format.Rgba8, daten.Spur);
                spur.GenerateMipmaps();
                if (_schichtKacheln.TryGetValue(kachel, out var alt))
                {
                    alt.Bild.Update(bild);
                    alt.Info.Update(info);
                    alt.Spur.Update(spur);
                }
                else _schichtKacheln[kachel] = (ImageTexture.CreateFromImage(bild), ImageTexture.CreateFromImage(info), ImageTexture.CreateFromImage(spur));
                _grasStadt[kachel] = daten.Info; // Pflaster als Grund: dort kein Grasteppich (Stadtansicht.Gras.cs)
            }
            if (_gelaendeKacheln.TryGetValue(kachel, out var netz)) netz.MaterialOverride = KachelStoff(kachel.I, kachel.J);
            BueschelWegeGesetzt(kachel, bueschel[k]);
        }
        if (alles) GD.Print($"Wegeschicht: {teile.Count} Teile, {_schichtKacheln.Count} von {liste.Count} Kacheln mit Wegen, {uhr.ElapsedMilliseconds} ms");
    }

    /// <summary>Gibt dem Stoff einer Kachel die Bilder der Schicht (oder schaltet sie aus).</summary>
    private void SchichtSetzen(int ti, int tj, ShaderMaterial stoff)
    {
        var da = _schichtKacheln.TryGetValue((ti, tj), out var schicht);
        stoff.SetShaderParameter("wege_an", da);
        if (!da) return;
        stoff.SetShaderParameter("wege", schicht.Bild);
        stoff.SetShaderParameter("wege_info", schicht.Info);
        stoff.SetShaderParameter("wege_spur", schicht.Spur);
        stoff.SetShaderParameter("wege_ursprung", new Vector2(ti * KachelZellen * 2f, tj * KachelZellen * 2f));
    }

    /// <summary>
    /// Alle Teile der Schicht und die Spurlinien der Karren (auf dem Hauptfaden gesammelt; das Rechnen danach liest nur noch sie). Dazu kommen die
    /// ausgefahrenen Ecken der Kreuzungen (Stadtansicht.Spurrillen.cs).
    /// </summary>
    private (List<Schichtteil> Teile, List<Spurlinie> Spuren) SchichtTeile()
    {
        var teile = new List<Schichtteil>();
        if (_entwurf is { } entwurf)
        {
            for (var i = 0; i < entwurf.Wege.Count; i++)
            {
                var w = entwurf.Wege[i];
                if (SchichtWeg(w.Verlauf.Select(V2).ToArray(), (float)w.BreiteWirksam, w.BelagAbschnitte, WegLuecken(i)) is { } teil) teile.Add(teil);
            }
            foreach (var m in entwurf.Mauern.Where(m => m.Ecken.Count >= 3)) teile.Add(SchichtFlaeche(m.Ecken.Select(V2).ToArray()));
        }
        else if (_karte is { } karte)
        {
            for (var i = 0; i < karte.Wege.Count; i++)
            {
                var w = karte.Wege[i];
                var linie = w.Verlauf.Where(p => p is { Length: 2 }).Select(Punkt2).ToArray();
                if (SchichtWeg(linie, (float)w.BreiteWirksam, BelagStuecke(i), WegLuecken(i)) is { } teil) teile.Add(teil);
            }
            foreach (var f in _mauerFlaechen.Where(f => f.Length >= 3)) teile.Add(SchichtFlaeche(f));
        }
        foreach (var (von, bis, breite) in _schichtAnschluesse)
        {
            // Hof- und Karrenwege sind ausgefahren, Stücke zu Gattern Trampelpfade
            var stufe = breite >= 2.5f ? Belagstufe.Fahrweg : Belagstufe.Trampelpfad;
            var stueck = new[] { new Belagabschnitt(0, von.DistanceTo(bis) + 1, stufe, Austritt: 0.7) };
            if (SchichtWeg(new[] { von, bis }, breite, stueck, null) is { } teil) teile.Add(teil);
        }
        var wege = teile.Where(t => !t.Flaeche).ToList();
        var (ecken, spuren) = SchichtKreuzungen(wege, teile.Where(t => t.Flaeche).Select(t => t.Linie).ToList());
        teile.AddRange(ecken);
        teile.AddRange(SchichtLoecher().Select(l => SchichtFlaeche(l, loch: true)));
        return (teile, spuren);
    }

    private static Schichtteil? SchichtWeg(Vector2[] roh, float breite, IReadOnlyList<Belagabschnitt> stuecke, List<(float Von, float Bis)>? luecken)
    {
        var linie = roh.Where((p, i) => i == 0 || p.DistanceTo(roh[i - 1]) > 0.01f).ToArray();
        if (linie.Length < 2) return null;
        var stelle = new float[linie.Length];
        for (var i = 1; i < linie.Length; i++) stelle[i] = stelle[i - 1] + linie[i].DistanceTo(linie[i - 1]);
        var teil = new Schichtteil { Linie = linie, Stelle = stelle, Halb = breite / 2, Rahmen = Rahmen(linie, breite / 2 + SchichtRand)!.Value };
        var n = Mathf.CeilToInt(stelle[^1] / SchichtBelagSchritt) + 2;
        (teil.Stufe, teil.Austritt, teil.Zustand) = (new float[n], new float[n], new float[n]);
        for (var k = 0; k < n; k++)
        {
            var s = k * SchichtBelagSchritt;
            var (stufe, austritt, zustand) = SchichtBelag(stuecke, s);
            if (luecken is not null && luecken.Any(l => s > l.Von && s < l.Bis)) stufe = float.NaN; // Brücke: kein Weg auf dem Boden
            (teil.Stufe[k], teil.Austritt[k], teil.Zustand[k]) = (stufe, austritt, zustand);
        }
        return teil;
    }

    private static Schichtteil SchichtFlaeche(Vector2[] ecken, bool loch = false) =>
        new() { Linie = ecken, Flaeche = true, Loch = loch, Rahmen = Rahmen(ecken, SchichtRand)!.Value };

    /// <summary>
    /// Freie Plätze, die in der Stadt Wiese bleiben statt Lehm (Schritt 5j; der Grasteppich wächst dort): die Bauzonen und im Spiel die freien
    /// Werkstattplätze, die Einwohner- und Wohnhausplätze ohne Haus und die Bauplätze des Kerns. In der Werkstatt stehen auf allen Plätzen Modelle.
    /// </summary>
    private List<Vector2[]> SchichtLoecher()
    {
        var liste = new List<Vector2[]>();
        if (_entwurf is { } e)
        {
            liste.AddRange(e.Bauzonen.Select(z => z.Ecken.Select(V2).ToArray()));
            return liste;
        }
        if (_karte is not { } karte) return liste;
        liste.AddRange(karte.Bauzonen.Select(z => z.Flaeche.Select(Punkt2).ToArray()));
        if (_wirtschaft is not { } w) return liste;
        var stehen = StehendeWerkstattplaetze();
        foreach (var g in karte.GebaeudeDerArt(Bausteinart.Werkstattplatz).Where(g => !stehen.Contains(g)))
            if (Naturhindernis.Von(g.Baustein, g.Punkt[0], g.Punkt[1], g.Drehung, g.Massstab) is { } h)
                liste.Add(Rechteck(new Vector2((float)h.X, (float)h.Z), Mathf.DegToRad((float)h.Drehung), new Vector2((float)h.HalbX, (float)h.HalbZ)));
        if (_wohnhaeuserGebaut)
        {
            var (stadt, draussen) = Einwohnerplaetze();
            foreach (var p in stadt.Concat(draussen).Where(p => !_gebautePlaetze.Contains(p) && !_hausplaetze.ContainsValue(p)))
                liste.Add(Rechteck(new Vector2(p.Mitte.X, p.Mitte.Z), p.Drehung, p.Groesse / 2));
        }
        if (w.Bauen.IstAktiv)
            foreach (var g in w.Bauen.Grundstuecke.Where(g => g.Zustand == Bauzustand.Bauplatz))
                liste.Add(Rechteck(new Vector2((float)g.Grundriss.X, (float)g.Grundriss.Z), DrehungY(g.Grundriss),
                    new Vector2((float)g.Grundriss.Breite / 2, (float)g.Grundriss.Tiefe / 2)));
        return liste.Where(l => l.Length >= 3).ToList();
    }

    /// <summary>Die Ecken eines gedrehten Rechtecks (Drehung wie <c>Rotation.Y</c>).</summary>
    private static Vector2[] Rechteck(Vector2 mitte, float drehung, Vector2 halb)
    {
        var (c, s) = (Mathf.Cos(drehung), Mathf.Sin(drehung));
        return new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) }
            .Select(k => mitte + new Vector2(c * k.X * halb.X + s * k.Y * halb.Y, -s * k.X * halb.X + c * k.Y * halb.Y)).ToArray();
    }

    /// <summary>Stufe (0 Trampelpfad … 3 Pflaster, 4 Baustelle), Austritt und Zustand an einer Stelle; an Abschnittsgrenzen auf 3 m fließend (wie früher das Band).</summary>
    private static (float Stufe, float Austritt, float Zustand) SchichtBelag(IReadOnlyList<Belagabschnitt> stuecke, float s)
    {
        if (stuecke.Count == 0) return (1, 0.6f, 1);
        static float Stufe(Belagabschnitt b) => b.Baustelle ? 4 : b.Stufe switch
        {
            Belagstufe.Trampelpfad => 0, Belagstufe.Fahrweg => 1, Belagstufe.Kies => 2, _ => 3,
        };
        var i = stuecke.Count - 1;
        for (var k = 0; k < stuecke.Count; k++)
            if (s < stuecke[k].Bis) { i = k; break; }
        var (hier, nachbar, anteil) = (stuecke[i], -1, 0f);
        if (s - (float)hier.Von < BelagUebergang && i > 0) (nachbar, anteil) = (i - 1, 0.5f * (1 - (s - (float)hier.Von) / BelagUebergang));
        else if ((float)hier.Bis - s < BelagUebergang && i + 1 < stuecke.Count) (nachbar, anteil) = (i + 1, 0.5f * (1 - ((float)hier.Bis - s) / BelagUebergang));
        if (nachbar < 0) return (Stufe(hier), (float)hier.Austritt, (float)hier.Zustand);
        var b = stuecke[nachbar];
        // Pflaster neben Erde (Pfad, Fahrweg): eine klare Grenze, über die der Shader Erde schmiert (sonst läge dazwischen ein Streifen „Kies“)
        if (!hier.Baustelle && !b.Baustelle && Mathf.Abs(Stufe(hier) - Stufe(b)) >= 1.5f) return (Stufe(hier), (float)hier.Austritt, (float)hier.Zustand);
        return (Mathf.Lerp(Stufe(hier), Stufe(b), anteil), Mathf.Lerp((float)hier.Austritt, (float)b.Austritt, anteil), Mathf.Lerp((float)hier.Zustand, (float)b.Zustand, anteil));
    }

    /// <summary>Weiches Minimum (polynomial): rundet die Ecken, wo zwei Wege zusammenkommen.</summary>
    private static float WeichMin(float a, float b, float k)
    {
        var h = Mathf.Max(k - Mathf.Abs(a - b), 0) / k;
        return Mathf.Min(a, b) - h * h * k * 0.25f;
    }

    /// <summary>
    /// Rechnet die Bilder einer Kachel (läuft parallel, liest nur <paramref name="teile"/> und <paramref name="spuren"/>). Null, wenn kein Teil sie
    /// berührt. Bildpunkt k deckt [Kachel + (k − 1) · 0,25 m, Kachel + k · 0,25 m]. Gerechnet wird 4 m über die Kachel hinaus (<see cref="SchichtErdRand"/>),
    /// damit der Abstand zur Erde (Schritt 5j, grobes Bild A) auch an Kachelgrenzen stimmt; in die Bilder kommt nur die Kachel.
    /// </summary>
    private static (byte[] Bild, byte[] Info, byte[] Spur)? SchichtKachelRechnen(int ti, int tj, List<Schichtteil> teile, List<Spurlinie> spuren)
    {
        const int n = SchichtBild, r0 = SchichtErdRand, gn = n + 2 * r0;
        const float t = SchichtTexel, kachel = KachelZellen * 2f;
        var (x0, z0) = (ti * kachel - t, tj * kachel - t);
        if (!teile.Any(teil => teil.Rahmen.Intersects(new Rect2(x0, z0, n * t, n * t)))) return null;
        // der Ausschnitt mit Rand
        var (gx0, gz0) = (x0 - r0 * t, z0 - r0 * t);
        var flaeche = new Rect2(gx0, gz0, gn * t, gn * t);
        float[]? rand = null, mitte = null, stufe = null, austritt = null, zustand = null;
        var pflaster = new List<Schichtteil>();
        var loecher = new List<Schichtteil>();
        foreach (var teil in teile)
        {
            if (!teil.Rahmen.Intersects(flaeche)) continue;
            if (teil.Flaeche)
            {
                (teil.Loch ? loecher : pflaster).Add(teil); // der Grund in der Mauer ist kein Weg: er kommt ins grobe Bild
                continue;
            }
            if (rand is null)
            {
                rand = new float[gn * gn];
                Array.Fill(rand, SchichtRand);
                mitte = new float[gn * gn];
                Array.Fill(mitte, SchichtMitteMax);
                stufe = new float[gn * gn];
                Array.Fill(stufe, 1f);
                austritt = new float[gn * gn];
                zustand = new float[gn * gn];
                Array.Fill(zustand, 1f);
            }
            var r = teil.Rahmen.Intersection(flaeche);
            var a0 = Math.Clamp((int)Mathf.Floor((r.Position.X - gx0) / t), 0, gn - 1);
            var b0 = Math.Clamp((int)Mathf.Floor((r.Position.Y - gz0) / t), 0, gn - 1);
            var a1 = Math.Clamp((int)Mathf.Ceil((r.End.X - gx0) / t), 0, gn - 1);
            var b1 = Math.Clamp((int)Mathf.Ceil((r.End.Y - gz0) / t), 0, gn - 1);
            var (w, h) = (a1 - a0 + 1, b1 - b0 + 1);
            var abstand = new float[w * h];
            var ort = new float[w * h];
            Array.Fill(abstand, float.MaxValue);
            LinieAbstand(teil, gx0, gz0, a0, b0, w, h, abstand, ort);

            for (var b = 0; b < h; b++)
                for (var a = 0; a < w; a++)
                {
                    var d = abstand[b * w + a];
                    var e = d - teil.Halb;
                    if (e >= SchichtRand) continue;
                    var k = Math.Clamp((int)MathF.Round(ort[b * w + a] / SchichtBelagSchritt), 0, teil.Stufe.Length - 1);
                    var st = teil.Stufe[k];
                    if (float.IsNaN(st)) continue;
                    var (au, zu) = (teil.Austritt[k], teil.Zustand[k]);
                    var i = (b + b0) * gn + a + a0;
                    var alt = rand![i];
                    if (alt >= SchichtRand)
                    {
                        (rand[i], mitte![i], stufe![i], austritt![i], zustand![i]) = (e, d, st, au, zu);
                        continue;
                    }
                    var innen = e < 0.3f && alt < 0.3f;
                    if (innen) stufe![i] = Mathf.Max(stufe[i], st); // der stärkere Belag gewinnt
                    if (e < alt)
                    {
                        if (!innen) stufe![i] = st;
                        (mitte![i], austritt![i], zustand![i]) = (d, au, zu);
                    }
                    rand[i] = WeichMin(alt, e, SchichtRundung);
                }
        }
        if (rand is null && pflaster.Count == 0) return null;
        if (rand is null)
        {
            rand = new float[gn * gn];
            Array.Fill(rand, SchichtRand);
            mitte = new float[gn * gn];
            Array.Fill(mitte, SchichtMitteMax);
            stufe = new float[gn * gn];
            austritt = new float[gn * gn];
            zustand = new float[gn * gn];
        }

        // Spurrillen: die beiden nächsten Spurlinien je Bildpunkt (Stadtansicht.Spurrillen.cs)
        var d1 = new float[n * n];
        var d2 = new float[n * n];
        var s1 = new float[n * n];
        var s2 = new float[n * n];
        Array.Fill(d1, SpurReichweite);
        Array.Fill(d2, SpurReichweite);
        SpurenRechnen(spuren, x0, z0, n, d1, d2, s1, s2);

        var bild = new byte[n * n * 4];
        var spur = new byte[n * n * 4];
        const float randFaktor = 255f / (SchichtRand - SchichtRandMin), mitteFaktor = 255f / SchichtMitteMax;
        for (var b = 0; b < n; b++)
            for (var a = 0; a < n; a++)
            {
                var (i, o) = ((b + r0) * gn + a + r0, (b * n + a) * 4);
                bild[o] = Byte(mitte![i] * mitteFaktor);
                bild[o + 1] = Byte((rand[i] - SchichtRandMin) * randFaktor);
                bild[o + 2] = Byte(stufe![i] * 63.75f);
                bild[o + 3] = 255; // frei (bis 5i die Stärke der Spurrillen, jetzt im Bild der Spurlinien)
                spur[o] = Byte(d1[b * n + a] * 100f);
                spur[o + 1] = Byte(d2[b * n + a] * 100f);
                spur[o + 2] = Byte(s1[b * n + a] * 255f);
                spur[o + 3] = Byte(s2[b * n + a] * 255f);
            }

        // grob: Punkt k (1 m) liegt mitten zwischen den feinen Punkten 4k − 2 und 4k − 1 der Kachel; mit Rand (4 Punkte je Seite) ist das der
        // Punkt 4K − 2 des Ausschnitts für K = k + 4
        const int m = SchichtInfoBild, rg = SchichtErdRand / 4, gm = m + 2 * rg;
        var info = new byte[m * m * 4];
        // Pflaster als Grund (in der Mauer): Deckung je Meter, am Rand auf einem Meter weich
        var grund = new float[gm * gm];
        foreach (var f in pflaster) FlaecheDecken(f.Linie, ti * kachel - 1 - rg, tj * kachel - 1 - rg, gm, grund);
        if (pflaster.Count > 0 && loecher.Count > 0)
        {
            // freie Plätze: Wiese statt Lehm (am Rand auf einem Meter weich)
            var loch = new float[gm * gm];
            foreach (var f in loecher) FlaecheDecken(f.Linie, ti * kachel - 1 - rg, tj * kachel - 1 - rg, gm, loch);
            for (var i = 0; i < grund.Length; i++) grund[i] *= 1f - loch[i];
        }
        // Erde, die auf Pflaster getragen wird: Punkte auf Pfad oder Fahrweg außerhalb der Stadt
        var erde = new bool[gm * gm];
        var erdeDa = false;
        for (var b = 0; b < gm; b++)
            for (var a = 0; a < gm; a++)
            {
                var i = Math.Clamp(4 * b - 2, 0, gn - 1) * gn + Math.Clamp(4 * a - 2, 0, gn - 1);
                erdeDa |= erde[b * gm + a] = rand[i] < -0.05f && stufe![i] < 1.5f && grund[b * gm + a] < 0.5f;
            }
        for (var b = 0; b < m; b++)
            for (var a = 0; a < m; a++)
            {
                var (bb, aa) = (b + rg, a + rg);
                var i = Math.Clamp(4 * bb - 2, 0, gn - 1) * gn + Math.Clamp(4 * aa - 2, 0, gn - 1);
                var o = (b * m + a) * 4;
                info[o] = Byte(austritt![i] * 255f);
                info[o + 1] = Byte(zustand![i] * 255f);
                info[o + 2] = Byte(grund[bb * gm + aa] * 255f);
                // Abstand zur nächsten Erde (0 … 4 m): Dort verschmiert der Shader Erde über das Pflaster – gesucht nur auf Pflaster (ein Pflasterweg
                // oder eine Gasse der Stadt)
                info[o + 3] = 255;
                if (!erdeDa || rand[i] > 0.6f || (stufe![i] < 2.5f && grund[bb * gm + aa] < 0.3f)) continue;
                var naechste = ErdeReichweite;
                foreach (var (da, db, dist) in ErdeNachbarn)
                {
                    if (dist >= naechste) break;
                    var (x, z) = (aa + da, bb + db);
                    if (x >= 0 && z >= 0 && x < gm && z < gm && erde[z * gm + x]) naechste = dist;
                }
                info[o + 3] = Byte(Mathf.Max(naechste - 0.5f, 0f) / ErdeReichweite * 255f);
            }
        return (bild, info, spur);
    }

    /// <summary>Der Rand, um den eine Kachel weiter gerechnet wird (Punkte zu 0,25 m: 4 m), und wie weit der Abstand zur Erde reicht (Meter).</summary>
    private const int SchichtErdRand = 16;
    private const float ErdeReichweite = 4f;

    /// <summary>Die Nachbarn im groben Bild bis <see cref="ErdeReichweite"/>, nach Abstand geordnet.</summary>
    private static readonly (int A, int B, float Abstand)[] ErdeNachbarn =
        (from b in Enumerable.Range(-4, 9) from a in Enumerable.Range(-4, 9) let d = MathF.Sqrt(a * a + b * b) where d <= ErdeReichweite orderby d select (a, b, d)).ToArray();

    /// <summary>Kleinster Abstand jedes Bildpunkts im Ausschnitt zur Linie des Weges und die Stelle (Meter entlang) des nächsten Punkts.</summary>
    private static void LinieAbstand(Schichtteil teil, float x0, float z0, int a0, int b0, int w, int h, float[] abstand, float[] ort)
    {
        const float t = SchichtTexel;
        var reichweite = teil.Halb + SchichtRand;
        for (var s = 0; s + 1 < teil.Linie.Length; s++)
        {
            var (p, q) = (teil.Linie[s], teil.Linie[s + 1]);
            var d = q - p;
            var laenge2 = Mathf.Max(d.LengthSquared(), 1e-6f);
            var laenge = Mathf.Sqrt(laenge2);
            var ib0 = Math.Max(0, (int)Mathf.Floor((Mathf.Min(p.Y, q.Y) - reichweite - z0) / t) - b0);
            var ib1 = Math.Min(h - 1, (int)Mathf.Ceil((Mathf.Max(p.Y, q.Y) + reichweite - z0) / t) - b0);
            var quer = new Vector2(-d.Y, d.X) / laenge * reichweite;
            for (var b = ib0; b <= ib1; b++)
            {
                var z = z0 + (b + b0 + 0.5f) * t;
                // nur die Punkte der Zeile, die die Kapsel um die Strecke schneidet (eine schräge, lange Strecke hätte sonst ein riesiges Rechteck)
                var (von, bis) = (float.MaxValue, float.MinValue);
                Scheibe(p, z, reichweite, ref von, ref bis);
                Scheibe(q, z, reichweite, ref von, ref bis);
                Kante(p + quer, q + quer, z, ref von, ref bis);
                Kante(p - quer, q - quer, z, ref von, ref bis);
                if (von > bis) continue;
                var ia0 = Math.Max(0, (int)MathF.Floor((von - x0) / t) - a0);
                var ia1 = Math.Min(w - 1, (int)MathF.Ceiling((bis - x0) / t) - a0);
                for (var a = ia0; a <= ia1; a++)
                {
                    var x = x0 + (a + a0 + 0.5f) * t;
                    var (vx, vz) = (x - p.X, z - p.Y);
                    var u = Math.Clamp((vx * d.X + vz * d.Y) / laenge2, 0f, 1f);
                    var (ex, ez) = (vx - d.X * u, vz - d.Y * u);
                    var dist = MathF.Sqrt(ex * ex + ez * ez);
                    var i = b * w + a;
                    if (dist < abstand[i])
                    {
                        abstand[i] = dist;
                        ort[i] = teil.Stelle[s] + u * laenge;
                    }
                }
            }
        }
    }

    /// <summary>Ein Wert 0 … 255 (gerundet, begrenzt) als Byte.</summary>
    private static byte Byte(float v) => v <= 0f ? (byte)0 : v >= 255f ? (byte)255 : (byte)(v + 0.5f);

    /// <summary>Wo schneidet die Zeile <paramref name="z"/> die Scheibe um <paramref name="c"/>? Erweitert [von, bis].</summary>
    private static void Scheibe(Vector2 c, float z, float r, ref float von, ref float bis)
    {
        var dz = z - c.Y;
        if (MathF.Abs(dz) > r) return;
        var halbe = MathF.Sqrt(r * r - dz * dz);
        (von, bis) = (MathF.Min(von, c.X - halbe), MathF.Max(bis, c.X + halbe));
    }

    /// <summary>Wo schneidet die Zeile <paramref name="z"/> die Strecke von <paramref name="s0"/> nach <paramref name="s1"/>? Erweitert [von, bis].</summary>
    private static void Kante(Vector2 s0, Vector2 s1, float z, ref float von, ref float bis)
    {
        if (MathF.Abs(s1.Y - s0.Y) < 1e-6f || (z - s0.Y) * (z - s1.Y) > 0) return;
        var x = s0.X + (z - s0.Y) / (s1.Y - s0.Y) * (s1.X - s0.X);
        (von, bis) = (MathF.Min(von, x), MathF.Max(bis, x));
    }

    /// <summary>
    /// Deckung eines Vielecks auf einem Raster (1 m, <paramref name="groesse"/>², Zellmitten ab <paramref name="x0"/>, <paramref name="z0"/>): Zeile für Zeile
    /// die Schnittpunkte mit den Kanten; innen 1, am Rand auf einem Meter (waagrecht gemessen) weich. Erhöht <paramref name="deckung"/> auf das Maximum.
    /// </summary>
    private static void FlaecheDecken(Vector2[] ecken, float x0, float z0, int groesse, float[] deckung)
    {
        var schnitte = new List<float>();
        for (var b = 0; b < groesse; b++)
        {
            var z = z0 + b + 0.5f;
            schnitte.Clear();
            for (int k = 0, l = ecken.Length - 1; k < ecken.Length; l = k++)
            {
                var (p, q) = (ecken[l], ecken[k]);
                if ((q.Y > z) != (p.Y > z)) schnitte.Add(q.X + (z - q.Y) / (p.Y - q.Y) * (p.X - q.X));
            }
            if (schnitte.Count < 2) continue;
            schnitte.Sort();
            for (var k = 0; k + 1 < schnitte.Count; k += 2)
            {
                var (von, bis) = (schnitte[k], schnitte[k + 1]);
                var a0 = Math.Max(0, (int)MathF.Floor(von - x0 - 1));
                var a1 = Math.Min(groesse - 1, (int)MathF.Ceiling(bis - x0));
                for (var a = a0; a <= a1; a++)
                {
                    var x = x0 + a + 0.5f;
                    var wert = Math.Clamp(MathF.Min(x - von, bis - x) + 0.5f, 0f, 1f);
                    var i = b * groesse + a;
                    if (wert > deckung[i]) deckung[i] = wert;
                }
            }
        }
    }
}
