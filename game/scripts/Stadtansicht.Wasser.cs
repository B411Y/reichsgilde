using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Das Wasser der Karte – ein Teil der <c>partial class Stadtansicht</c> (Kartenwerkstatt, Schritt 5c; realistisches Wasser, Phase 3:
/// <c>docs/wasser/phase3.md</c>). Der Kern rechnet Flüsse, Bäche, Seen und Teiche samt Spiegel und Bett (<see cref="Wasserform"/>) und die
/// Übergänge darüber (<see cref="Wasserplan"/>); das Bett steckt schon im Boden (Stadtansicht.Gelaende.cs). Gezeichnet wird alles mit
/// <c>wasser_realistisch.gdshaderinc</c>; welche Gewässerart (<see cref="Wasserart"/>) ein Gewässer ist, entscheiden Breite bzw. Fläche aus der Karte
/// (<see cref="Wasserarten.AusKarte"/>), welche Stufe des Shaders und wie fein das Netz, die Grafik-Voreinstellung (<see cref="Wasserarten.StufeAusGrafik"/>).
/// <list type="bullet">
/// <item><b>Flüsse und Bäche</b> (<see cref="FlussNetze"/>): ein Band entlang der Mittellinie auf der Höhe des Spiegels (er fällt nur, steigt nie), etwas breiter
/// als der Fluss – die Ränder verschwinden im Ufer –, quer und längs in Ecken zu 2 m (Ultra 1 m) geteilt und in Stücke zu 64 m (Godot zeichnet nur die
/// Stücke im Bild). Wo der gezeichnete Boden das Bett nicht zeigt (ein schmaler Bach, eine Furt), liegt das Band knapp darüber (<see cref="Wasserdeckel"/>).
/// Die Ecken tragen Laufachsen und Strömung (die Wellen folgen den Kurven) und in <c>COLOR</c> Uferabstand, „offen“ (friert nie: Furt, Fähre, Hafen,
/// Mündung), Schaumanteil (nur ab 30 m Breite; an einem Hafen nur ein schmaler Saum) und Deckkraft (in der Mündung übernimmt das größere Gewässer).</item>
/// <item><b>Seen und Teiche</b> (<see cref="SeeNetz"/>): eine Fläche genau über den Zellen des Bodens (<see cref="RasterDreiecke"/>) auf der Höhe des
/// Wasserstands, der Uferabstand jeder Ecke aus dem Umriss.</item>
/// <item><b>Hafenbecken</b> (<see cref="BeckenNetz"/>): vor der Kaimauer eines Hafens hebt der Kern das Ufer aus; darin liegt ruhiges Wasser ohne Schaum auf dem
/// Spiegel des Flusses davor und blendet dort, wo der Fluss beginnt, in ihn über.</item>
/// <item><b>Eis</b> (<see cref="WasserWinterNachfuehren"/>): Winterfortschritt aus Jahreszeit und Tageszeit des Kerns (ein Spieltag ist ein Jahr), Regeln je
/// Art (<see cref="Wasserarten"/>). <b>Wetter</b>: Wind, Regen, Schnee und Himmelsfarbe liest der Shader aus den globalen Werten (Stadtansicht.Wetter.cs).</item>
/// <item><b>Übergänge, Fähren, Hafen</b>: Stadtansicht.Wasser.Uebergaenge.cs.</item>
/// </list>
/// Im Spiel entsteht das Wasser einmal mit der Karte (<see cref="GewaesserBauen"/>), in der Kartenwerkstatt neu, sobald ein Befehl Gewässer, Wege, Fähren oder
/// Gelände geändert hat (Stadtansicht.Kartenwerkstatt.Wasser.cs); wechselt die Grafikstufe, nur die Wasserflächen (<see cref="WasserStufeNachfuehren"/>).
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Das Band eines Flusses ist um diesen Faktor breiter als der Fluss: Die Ränder liegen unter dem Ufer.</summary>
    private const float FlussRandFaktor = 1.15f;

    /// <summary>In diesem Abstand (Meter) tastet <see cref="Wasserdeckel"/> den Boden längs ab (der Kern gibt einen Punkt je Meter, das Raster hat 2 m).</summary>
    private const int FlussSchritt = 2;

    /// <summary>So lang (Meter) ist ein Stück des Flussbandes: Godot lässt Stücke außerhalb des Bildes weg.</summary>
    private const float FlussStueck = 64f;

    /// <summary>So hoch (Meter) liegt das Wasser mindestens über dem gezeichneten Boden (und dem Weg einer Furt) in der Mitte des Bandes.</summary>
    private const float WasserUeberBoden = 0.03f;

    /// <summary>Um Furt (ab der halben Wegbreite), Fähre und Hafenbecken bleibt das Wasser in diesem Umkreis (m) offen und geht bis zur doppelten Weite ins Eis über.</summary>
    private const float OffenUmkreis = 3f;

    /// <summary>
    /// Grundspiegelung des Himmels (<c>himmel_grund</c>): Die Stadt hat keinen Himmel, den Godot im Wasser spiegeln könnte (nur eine Hintergrundfarbe), und die
    /// Kamera schaut steil von oben, wo die echte Spiegelung schwach ist. Ohne diesen Anteil wäre tiefes Wasser fast schwarz.
    /// </summary>
    private const float WasserHimmelGrund = 0.2f;

    /// <summary>Wiederholung der Normalkarten je Meter im Spiel (Testszene 0,25): Die Kamera ist weiter weg, kleinere Kräusel wirkten dort wie Rauschen.</summary>
    private const float WasserBildMassstab = 0.15f;

    private Node3D? _gewaesserKnoten;
    private Node3D? _wasserFlaechen;
    private readonly List<(ShaderMaterial Stoff, Wasserart Art)> _wasserStoffe = new();
    private Wasserstufe _wasserStufe;
    private float _wasserWinter = -1;

    /// <summary>Startoption <c>--winter=0..1</c>: Eis so weit gewachsen, unabhängig von der Uhr (für Bildschirmfotos).</summary>
    private static readonly float? WinterVorgabe =
        float.TryParse(Wasserarten.Startoption("winter"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w)
            ? Mathf.Clamp(w, 0f, 1f) : null;

    /// <summary>Das Wasser, das die Ansicht zeichnet: im Spiel das der Karte, in der Werkstatt das des Entwurfs (folgt jeder Änderung von selbst).</summary>
    private Wasserform? GewaesserForm => _gelaende?.Wasser;

    /// <summary>Brücken, Furten und Fähren der Karte bzw. des Entwurfs (null ohne Karte).</summary>
    private Wasserplan? Uebergangsplan => _entwurf?.Wasserplan ?? _karte?.Wasserplan;

    /// <summary>
    /// Baut Wasserflächen, Brücken, Furten, Fähren neu auf (oder zum ersten Mal). Eine Karte ohne Wasser bekommt nichts und kostet nichts.
    /// </summary>
    private void GewaesserBauen()
    {
        if (_gewaesserKnoten is not null) KnotenEntfernen(_gewaesserKnoten);
        _gewaesserKnoten = null;
        _wasserFlaechen = null;
        _wasserStoffe.Clear();
        UebergaengeVergessen();
        if (GewaesserForm is not { IstLeer: false } form) return;

        _gewaesserKnoten = new Node3D { Name = "Gewaesser" };
        AddChild(_gewaesserKnoten);
        WasserflaechenBauen(form);
        if (Uebergangsplan is { } plan) UebergaengeBauen(_gewaesserKnoten, plan);
    }

    /// <summary>Die Wasserflächen (ohne Übergänge) in der Stufe der Grafik-Voreinstellung, mit einem Stoff je Gewässer.</summary>
    private void WasserflaechenBauen(Wasserform form)
    {
        if (_gewaesserKnoten is null) return;
        if (_wasserFlaechen is not null) KnotenEntfernen(_wasserFlaechen);
        _wasserStoffe.Clear();
        _wasserFlaechen = new Node3D { Name = "Wasserflaechen" };
        _gewaesserKnoten.AddChild(_wasserFlaechen);
        _wasserStufe = _wasserStufeMessung ?? Wasserarten.StufeAusGrafik();
        var shader = Wasserarten.ShaderFuer(_wasserStufe);
        var abstand = Wasserarten.NetzAbstand(_wasserStufe);
        _wasserWinter = WinterJetzt();

        foreach (var lauf in form.Laeufe)
        {
            if (lauf.Punkte.Count < 2) continue;
            var breite = lauf.Punkte.Max(p => p.Breite);
            var art = Wasserarten.AusKarte(lauf.Art, breite);
            var (anfang, ende) = (lauf.Punkte[0].Ort, lauf.Punkte[^1].Ort);
            var richtung = new Vector2((float)(ende.X - anfang.X), (float)(ende.Z - anfang.Z));
            // Reihenfolge der Durchsichtigen: Flüsse zuerst, Bäche darüber (sie blenden in der Mündung in den Fluss über)
            var stoff = WasserStoff(shader, art, richtung, lauf: true, (float)lauf.Stroemung, abstand, breite > Wasserarten.BachBisBreite ? -8 : -7);
            var nr = 0;
            foreach (var netz in FlussNetze(lauf, form, abstand))
                _wasserFlaechen.AddChild(WasserInstanz($"Fluss_{lauf.Name}_{nr++}", netz, stoff));
        }
        foreach (var see in form.Seen)
        {
            if (SeeNetz(see, form, abstand) is not { } netz) continue;
            var art = Wasserarten.AusKarte(see.Art, 0, Flaecheninhalt(see.Umriss));
            _wasserFlaechen.AddChild(WasserInstanz("See_" + see.Name, netz, WasserStoff(shader, art, new Vector2(1, 0.4f), lauf: false, null, abstand, -8)));
        }
        if (form.Becken.Count > 0)
        {
            var stoff = WasserStoff(shader, Wasserarten.Hafenbecken, new Vector2(1, 0.4f), lauf: false, null, abstand, -6); // nach dem Fluss: blendet in ihn über
            foreach (var becken in form.Becken)
                if (BeckenNetz(becken, form, abstand) is { } netz)
                    _wasserFlaechen.AddChild(WasserInstanz("Becken_" + becken.Name, netz, stoff));
        }
        // Für Klicktests: was gebaut ist und wo die Furten liegen (dort friert nichts)
        var furten = Uebergangsplan?.Querungen.Where(q => q.Art == Uebergangsart.Furt).Select(q => $"{q.GewaesserName} bei {q.Mitte.X:0},{q.Mitte.Z:0}") ?? Enumerable.Empty<string>();
        GD.Print($"[wasser] Stufe {_wasserStufe}, Netz {abstand:0} m, {_wasserFlaechen.GetChildCount()} Flächen, " +
                 $"{string.Join(", ", _wasserStoffe.Select(s => s.Art.Name).Distinct())}; Furten: {string.Join("; ", furten)}");
    }

    private ShaderMaterial WasserStoff(Shader shader, Wasserart art, Vector2 richtung, bool lauf, float? tempo, float abstand, int reihenfolge)
    {
        var stoff = new ShaderMaterial { Shader = shader, ResourceName = "Wasser_" + art.Name, RenderPriority = reihenfolge };
        Wasserarten.BilderSetzen(stoff);
        Wasserarten.Anwenden(stoff, art, richtung, _wasserWinter, uferImNetz: true, lauf, tempo, abstand);
        stoff.SetShaderParameter("himmel_grund", WasserHimmelGrund);
        stoff.SetShaderParameter("sampler_scale", new Vector2(WasserBildMassstab, WasserBildMassstab));
        _wasserStoffe.Add((stoff, art));
        return stoff;
    }

    private static MeshInstance3D WasserInstanz(string name, ArrayMesh netz, ShaderMaterial stoff) => new()
    {
        Name = name, Mesh = netz, MaterialOverride = stoff, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
    };

    /// <summary>Bei jeder Änderung der Grafik-Einstellungen (Stadtansicht.Grafik.cs): Hat sich die Stufe des Wassers geändert, die Flächen neu bauen.</summary>
    private void WasserStufeNachfuehren()
    {
        if (_gewaesserKnoten is null || GewaesserForm is not { IstLeer: false } form || _wasserStufeMessung is not null
            || Wasserarten.StufeAusGrafik() == _wasserStufe) return;
        WasserflaechenBauen(form);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Eis nach Jahreszeit
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Der Winterfortschritt jetzt: aus Jahreszeit und Fortschritt im Spieljahr (Kern: <see cref="Spielzeit.Jahreszeit"/>, <see cref="Spielzeit.MinuteImTag"/>
    /// mit der angebrochenen Minute), siehe <see cref="Wasserarten.Winterfortschritt"/>. Ist die Jahreszeit vorgegeben (<c>--jahreszeit</c>, Entwicklerfenster)
    /// und weicht von der Uhr ab, steht das Eis im Winter voll, sonst gar nicht; <c>--winter</c> setzt den Wert fest.
    /// </summary>
    private float WinterJetzt()
    {
        if (WinterVorgabe is { } vorgabe) return vorgabe;
        if (_simulation is null) return _jahreszeitVorgabe == Jahreszeit.Winter ? 1f : 0f;
        var zeit = _simulation.Zeit;
        var gezeigt = GezeigteJahreszeit(zeit.Jahreszeit);
        if (gezeigt != zeit.Jahreszeit) return gezeigt == Jahreszeit.Winter ? 1f : 0f;
        var anteil = (zeit.MinuteImTag + _simulation.Takt.AngebrocheneMinute) / Spielzeit.MinutenProTag;
        return Wasserarten.Winterfortschritt(zeit.Jahreszeit, anteil);
    }

    /// <summary>Jedes Bild (aus <see cref="WasserBild"/>): das Eis aller Gewässer, die zufrieren können, auf den Winterfortschritt (nur bei Änderung).</summary>
    private void WasserWinterNachfuehren()
    {
        if (_wasserStoffe.Count == 0) return;
        var winter = WinterJetzt();
        if (Mathf.Abs(winter - _wasserWinter) < 0.002f) return;
        _wasserWinter = winter;
        foreach (var (stoff, art) in _wasserStoffe)
            if (art.Eis != Eisregel.Nie) stoff.SetShaderParameter("eis", winter);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Flüsse und Bäche
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Das Band eines Flusses in Stücken: Reihen alle <paramref name="abstand"/> Meter entlang der Mittellinie, in jeder Reihe gleich viele Ecken quer
    /// (Abstand höchstens <paramref name="abstand"/>, mindestens vier Felder), alle auf der Höhe des Spiegels. UV = (quer, Stelle) in Metern, UV2 = Strömung
    /// nach x und z, COLOR = (Uferabstand / 32, offen, Schaumanteil, Deckkraft) – wie <c>wasser_realistisch.gdshaderinc</c> sie liest.
    /// </summary>
    private List<ArrayMesh> FlussNetze(Wasserlauf lauf, Wasserform form, float abstand)
    {
        var netze = new List<ArrayMesh>();
        var p = lauf.Punkte;
        var schritt = Math.Max(1, Mathf.RoundToInt(abstand));
        var reihen = new List<int>();
        for (var i = 0; i < p.Count; i += schritt) reihen.Add(i);
        if (reihen[^1] != p.Count - 1) reihen.Add(p.Count - 1);
        var groessteHalbe = (float)p.Max(w => w.Breite) / 2 * FlussRandFaktor;
        var felder = Math.Max(4, Mathf.CeilToInt(2 * groessteHalbe / abstand));
        if (felder % 2 == 1) felder++; // eine Ecke genau in der Mitte (dort ist das Ufer am weitesten)
        var andere = AndereGewaesser(form, lauf, null);
        var offenStellen = OffeneStellen(form, lauf.Nummer, lauf.Name);
        var tempo = (float)lauf.Stroemung;

        var ecken = new List<Vector3>();
        var uv = new List<Vector2>();
        var uv2 = new List<Vector2>();
        var farben = new List<Color>();
        var stueckAnfang = 0; // erste Reihe des Stücks
        for (var r = 0; r < reihen.Count; r++)
        {
            var i = reihen[r];
            var pt = p[i];
            var (vor, nach) = (p[Math.Max(0, i - 1)].Ort, p[Math.Min(p.Count - 1, i + 1)].Ort);
            var richtung = new Vector2((float)(nach.X - vor.X), (float)(nach.Z - vor.Z)).Normalized();
            if (richtung == Vector2.Zero) richtung = new Vector2((float)pt.RichtungX, (float)pt.RichtungZ);
            var quer = new Vector2(-richtung.Y, richtung.X);
            var halb = (float)pt.Breite / 2;
            var mitte = new Vector2((float)pt.Ort.X, (float)pt.Ort.Z);
            var hoehe = Mathf.Max((float)pt.Spiegel, Wasserdeckel(mitte, richtung, halb));
            var schaum = Mathf.SmoothStep(Wasserarten.SchaumAbBreite - 4f, Wasserarten.SchaumAbBreite, (float)pt.Breite);
            for (var j = 0; j <= felder; j++)
            {
                var o = halb * FlussRandFaktor * (2f * j / felder - 1f);
                var ort = mitte + quer * o;
                ecken.Add(new Vector3(ort.X, hoehe, ort.Y));
                uv.Add(new Vector2(o, (float)pt.Stelle));
                uv2.Add(richtung * tempo);
                var (offen, kai) = Offenheit(offenStellen, ort);
                var fremd = andere.Count > 0 && ImAnderenWasser(form, andere, ort, lauf, null);
                farben.Add(new Color(Mathf.Clamp((halb - Mathf.Abs(o)) / 32f, 0f, 1f), fremd ? 1f : offen,
                    schaum * Mathf.Lerp(1f, Wasserarten.KaiSchaumAnteil, kai), fremd ? 0f : 1f));
            }
            // Stück abschließen: nach FlussStueck Metern oder am Ende (die letzte Reihe ist zugleich die erste des nächsten Stücks)
            var laenge = (float)(pt.Stelle - p[reihen[stueckAnfang]].Stelle);
            if (r > stueckAnfang && (laenge >= FlussStueck || r == reihen.Count - 1))
            {
                netze.Add(BandNetz(ecken, uv, uv2, farben, felder));
                if (r == reihen.Count - 1) break;
                var letzte = felder + 1;
                ecken = ecken.GetRange(ecken.Count - letzte, letzte);
                uv = uv.GetRange(uv.Count - letzte, letzte);
                uv2 = uv2.GetRange(uv2.Count - letzte, letzte);
                farben = farben.GetRange(farben.Count - letzte, letzte);
                stueckAnfang = r;
            }
        }
        return netze;
    }

    /// <summary>Ein Stück Band aus Reihen zu je <paramref name="felder"/> + 1 Ecken: zwei Dreiecke je Feld, alle mit der Vorderseite nach oben.</summary>
    private static ArrayMesh BandNetz(List<Vector3> ecken, List<Vector2> uv, List<Vector2> uv2, List<Color> farben, int felder)
    {
        var breite = felder + 1;
        var indizes = new List<int>();
        for (var r = 0; r + 1 < ecken.Count / breite; r++)
            for (var j = 0; j < felder; j++)
            {
                int a = r * breite + j, b = a + 1, c = a + breite, d = c + 1;
                DreieckNachOben(indizes, ecken, a, b, c);
                DreieckNachOben(indizes, ecken, b, d, c);
            }
        return NetzAusFeldern(ecken, uv, uv2, farben, indizes);
    }

    /// <summary>
    /// Der Shader zeichnet nur Vorderseiten (<c>cull_back</c>), und Godot nimmt als Vorderseite die im Uhrzeigersinn: Die Ecken so ordnen, dass sie von oben
    /// gesehen im Uhrzeigersinn laufen (das Kreuzprodukt (b − a) × (c − a) zeigt dann nach unten).
    /// </summary>
    private static void DreieckNachOben(List<int> indizes, List<Vector3> ecken, int a, int b, int c)
    {
        var n = (ecken[b] - ecken[a]).Cross(ecken[c] - ecken[a]);
        if (n.Y <= 0) indizes.AddRange(new[] { a, b, c });
        else indizes.AddRange(new[] { a, c, b });
    }

    /// <summary>
    /// Wie hoch das Band an dieser Stelle mindestens liegen muss, damit der gezeichnete Boden nicht hindurchsticht: <see cref="WasserUeberBoden"/> über dem höchsten
    /// Punkt des Bodens (in einer Furt: des Weges, <see cref="FurtBoden"/>) quer bis drei Viertel der halben Breite – so weit ist das Wasser deckend, weiter außen läuft es
    /// aus – und längs bis zur nächsten Reihe. Der Boden ist nur alle 2 m bekannt: Das Bett eines schmalen oder schräg zum Raster laufenden Baches ist
    /// im Netz eine Kette einzelner Gruben, und ein Spiegel darin zerfiele in Flecken. Wo das Raster das Bett zeigt (ein breiter Fluss), liegt der Boden tiefer als der
    /// Spiegel, und es bleibt beim Spiegel.
    /// </summary>
    private float Wasserdeckel(Vector2 mitte, Vector2 richtung, float halb)
    {
        if (!BodenBewegt) return float.MinValue;
        var quer = new Vector2(-richtung.Y, richtung.X);
        var hoechst = float.MinValue;
        foreach (var laengs in new[] { -1f, -0.5f, 0f, 0.5f, 1f })
            foreach (var seite in new[] { -0.75f, -0.4f, 0f, 0.4f, 0.75f })
            {
                var p = mitte + richtung * (laengs * FlussSchritt / 2f) + quer * (seite * halb);
                var boden = FurtBoden(p.X, p.Y) is { } weg ? weg + WegHoehe : BodenHoehe(p.X, p.Y);
                hoechst = Mathf.Max(hoechst, boden);
            }
        return hoechst + WasserUeberBoden;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Offene Stellen, Mündungen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wo ein Gewässer nie zufriert („bewegtes Wasser“, Nutzer 29.10.2026): Furten über dieses Gewässer (Strecke und halbe Wegbreite), Fährlinien und
    /// Hafenbecken; die Becken zählen zugleich als Kaimauer (dort bleibt vom Uferschaum nur ein schmaler Saum).
    /// </summary>
    private List<(Vector2 A, Vector2 B, float Halb, bool Kai)> OffeneStellen(Wasserform form, int nummer, string name)
    {
        var stellen = new List<(Vector2, Vector2, float, bool)>();
        static Vector2 V(Kartenort o) => new((float)o.X, (float)o.Z);
        if (Uebergangsplan is { } plan)
        {
            foreach (var q in plan.Querungen)
                if (q.Art == Uebergangsart.Furt && q.Gewaesser == nummer) stellen.Add((V(q.Anfang), V(q.Ende), (float)q.Breite / 2, false));
            foreach (var f in plan.Faehren)
                if (f.Gewaesser == name) stellen.Add((V(f.Von), V(f.Bis), 2f, false));
        }
        foreach (var b in form.Becken)
            for (var k = 0; k < b.Umriss.Count; k++) // die Seiten des Beckens
                stellen.Add((V(b.Umriss[k]), V(b.Umriss[(k + 1) % b.Umriss.Count]), 0f, true));
        return stellen;
    }

    /// <summary>Wie offen das Wasser am Ort ist (1 = friert nie, weich ins Eis übergehend) und wie nah an einer Kaimauer (1 = dort).</summary>
    private static (float Offen, float Kai) Offenheit(List<(Vector2 A, Vector2 B, float Halb, bool Kai)> stellen, Vector2 p)
    {
        float offen = 0, kai = 0;
        foreach (var (a, b, halb, istKai) in stellen)
        {
            var d = Mathf.Max(0, AbstandStrecke(p, a, b) - halb);
            offen = Mathf.Max(offen, 1f - Mathf.SmoothStep(OffenUmkreis, 2 * OffenUmkreis, d));
            if (istKai) kai = Mathf.Max(kai, 1f - Mathf.SmoothStep(2f, 8f, d));
        }
        return (offen, kai);
    }

    private static float AbstandStrecke(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var l2 = ab.LengthSquared();
        var t = l2 < 1e-6f ? 0 : Mathf.Clamp((p - a).Dot(ab) / l2, 0, 1);
        return p.DistanceTo(a + ab * t);
    }

    /// <summary>Die Rechtecke der anderen Gewässer (ohne <paramref name="lauf"/> bzw. <paramref name="see"/>): nur dort lohnt die Frage nach einer Mündung.</summary>
    private static List<Kartenrechteck> AndereGewaesser(Wasserform form, Wasserlauf? lauf, Wassersee? see) =>
        form.Laeufe.Where(l => l != lauf).Select(l => l.Bereich).Concat(form.Seen.Where(s => s != see).Select(s => s.Bereich)).ToList();

    /// <summary>
    /// Liegt der Ort in einem anderen Gewässer, das dort gilt (Mündung: der Kern nimmt das mit dem tieferen Spiegel)? Dort blendet dieses Wasser aus und
    /// friert nicht (ein Bach friert nicht bis in den offenen Rhein hinein).
    /// </summary>
    private static bool ImAnderenWasser(Wasserform form, List<Kartenrechteck> andere, Vector2 ort, Wasserlauf? lauf, Wassersee? see)
    {
        var p = new Kartenort(ort.X, ort.Y);
        if (!andere.Any(r => r.Enthaelt(p)) || form.An(ort.X, ort.Y) is not { } w) return false;
        object? dort = (object?)w.Lauf ?? w.See;
        object? hier = (object?)lauf ?? see;
        return dort is not null && !ReferenceEquals(dort, hier);
    }

    private static ArrayMesh NetzAusFeldern(List<Vector3> ecken, List<Vector2> uv, List<Vector2> uv2, List<Color> farben, List<int>? indizes)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = ecken.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = ecken.Select(_ => Vector3.Up).ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uv.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV2] = uv2.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = farben.ToArray();
        if (indizes is not null) arrays[(int)Mesh.ArrayType.Index] = indizes.ToArray();
        var netz = new ArrayMesh();
        netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        netz.CustomAabb = netz.GetAabb().Grow(1f); // die Wellen heben und schieben die Fläche ein Stück über das Netz hinaus
        return netz;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Seen, Teiche und Hafenbecken
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die Fläche eines Sees: Dreiecke genau über den Zellen des Bodens (so klafft am Ufer kein Spalt), alle auf der Höhe des Wasserstands, in Ultra
    /// geviertelt. COLOR = (Abstand zum Umriss / 32, offen, 1, Deckkraft).
    /// </summary>
    private ArrayMesh? SeeNetz(Wassersee see, Wasserform form, float abstand)
    {
        if (see.Umriss.Count < 3 || _gelaende is null) return null;
        var umriss = see.Umriss.Select(o => new Vector2((float)o.X, (float)o.Z)).ToArray();
        var ecken = FeinerTeilen(RasterDreiecke(umriss, 0, (float)see.Wasserstand), abstand);
        if (ecken.Count == 0) return null;
        var andere = AndereGewaesser(form, null, see);
        var offenStellen = OffeneStellen(form, see.Nummer, see.Name);
        var werte = new Dictionary<(float, float), Color>();
        var farben = ecken.Select(e =>
        {
            if (werte.TryGetValue((e.X, e.Z), out var c)) return c;
            var ort = new Vector2(e.X, e.Z);
            var rand = (float)see.Randabstand(new Kartenort(e.X, e.Z));
            var fremd = andere.Count > 0 && ImAnderenWasser(form, andere, ort, null, see);
            return werte[(e.X, e.Z)] = new Color(Mathf.Clamp(rand / 32f, 0f, 1f), fremd ? 1f : Offenheit(offenStellen, ort).Offen, 1f, fremd ? 0f : 1f);
        }).ToList();
        return DreieckNetz(ecken, farben);
    }

    /// <summary>
    /// Das Wasser eines Hafenbeckens: wie ein See genau über den Zellen des Bodens, auf dem Spiegel des Gewässers vor der Kaimauer. Wo der Fluss beginnt, ist
    /// die Deckkraft der Ecke 0 – dort blendet das Becken in das Band des Flusses über (das zuerst gezeichnet wird). COLOR = (Tiefe · 2 / 32, 1, 1, Deckkraft):
    /// Ohne Tiefentextur (Stufe Niedrig) liest der Shader den halben Uferabstand als Tiefe.
    /// </summary>
    private ArrayMesh? BeckenNetz(Hafenbecken becken, Wasserform form, float abstand)
    {
        if (_gelaende is null) return null;
        var spiegel = (float)becken.Spiegel;
        var ecken = FeinerTeilen(RasterDreiecke(becken.Umriss.Select(o => new Vector2((float)o.X, (float)o.Z)).ToArray(), 0, spiegel), abstand);
        if (ecken.Count == 0) return null;
        var farben = ecken.Select(e => new Color(Mathf.Clamp((spiegel - BodenHoehe(e.X, e.Z)) * 2f / 32f, 0f, 1f), 1f, 1f,
            form.IstWasser(e.X, e.Z) ? 0f : 1f)).ToList();
        return DreieckNetz(ecken, farben);
    }

    /// <summary>Ultra (Netz 1 m): jedes Dreieck des 2-m-Rasters in vier teilen; sonst unverändert.</summary>
    private static List<Vector3> FeinerTeilen(List<Vector3> dreiecke, float abstand)
    {
        if (abstand >= (float)Hoehenraster.Abstand * 0.75f) return dreiecke;
        var fein = new List<Vector3>(dreiecke.Count * 4);
        for (var i = 0; i + 2 < dreiecke.Count; i += 3)
        {
            var (a, b, c) = (dreiecke[i], dreiecke[i + 1], dreiecke[i + 2]);
            var (ab, bc, ca) = ((a + b) / 2, (b + c) / 2, (c + a) / 2);
            fein.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
        }
        return fein;
    }

    /// <summary>Eine Dreiecksliste (je drei Ecken) mit Vorderseite nach oben als Netz für den Wasser-Shader (ohne Laufachsen).</summary>
    private static ArrayMesh DreieckNetz(List<Vector3> ecken, List<Color> farben)
    {
        var indizes = new List<int>(ecken.Count);
        for (var i = 0; i + 2 < ecken.Count; i += 3) DreieckNachOben(indizes, ecken, i, i + 1, i + 2);
        var leer = ecken.Select(_ => Vector2.Zero).ToList();
        return NetzAusFeldern(ecken, leer, leer, farben, indizes);
    }

    /// <summary>Fläche eines Vielecks (m², Gaußsche Trapezformel).</summary>
    private static double Flaecheninhalt(IReadOnlyList<Kartenort> umriss)
    {
        var summe = 0.0;
        for (var i = 0; i < umriss.Count; i++)
        {
            var (a, b) = (umriss[i], umriss[(i + 1) % umriss.Count]);
            summe += a.X * b.Z - b.X * a.Z;
        }
        return Math.Abs(summe) / 2;
    }
}
