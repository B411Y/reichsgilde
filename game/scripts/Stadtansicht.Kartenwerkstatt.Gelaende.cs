using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Das Gelände in der Kartenwerkstatt – ein Teil der <c>partial class Stadtansicht</c> (Überblick in Stadtansicht.Kartenwerkstatt.cs, Boden und
/// Höhen in Stadtansicht.Gelaende.cs). Der Kern rechnet das Gelände (<see cref="Kartenentwurf.Gelaendeform"/>) und führt die Pinsel aus; hier steht, was
/// die Werkstatt in der 3D-Welt daraus macht:
/// <list type="bullet">
/// <item><b>Pinsel</b> (<see cref="Werkstattwerkzeug.Gelaende"/>): Heben, Senken, Glätten, Ebnen, Rauschen, Terrassen. Mit gedrückter Maus malt der Strich; jede Mausbewegung
/// gibt der Kern weiter (<see cref="Kartenentwurf.GelaendeStrichWeiter"/>), das Raster ändert sich sofort, und nur die Kacheln unter dem Pinsel werden neu
/// gezeichnet. Loslassen beendet den Strich (ein Rückgängig-Schritt). Der Kreis an der Maus zeigt Radius und weiche Kante auf dem Gelände.</item>
/// <item><b>Rampe</b> (<see cref="Werkstattwerkzeug.Rampe"/>): zwei Klicks – Anfang und Ende; dazwischen ein Band mit gleichmäßigem Gefälle.</item>
/// <item><b>Folgen</b>: Was auf dem Gelände steht, folgt ihm – nach einem Strich werden die Gebäude, Wege, Mauern, Flächen, Pflanzen und die Natur im geänderten
/// Rechteck neu gebaut (erst nach dem Strich, nicht bei jeder Mausbewegung); Wege und Mauern ändern ihr Bett im Gelände und damit die Kacheln unter ihnen.</item>
/// <item><b>Höhenanzeige</b> (<see cref="WerkstattBodenInfo"/>): Höhe und Gefälle unter der Maus für die Leiste unten.</item>
/// <item><b>Pinsel-Paket 1</b>: Filter nach Steigung und Boden (<see cref="GelaendeFilter"/>), nur Mulden / nur Spitzen, Zielhöhe mit <b>Pipette</b> (Strg+Klick oder
/// Knopf „Pipette“, <see cref="PipetteBereit"/>), Terrassen und die <b>Anzeige unter dem Pinsel</b> (<see cref="WerkstattPinselInfo"/>).</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    // ═════════════════════════════════════════════════════════════════════════
    //  Einstellungen der Werkzeuge (die Tafel rechts ändert sie)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Der Geländepinsel, der gerade gewählt ist.</summary>
    public Pinselart GelaendeArt { get; private set; } = Pinselart.Heben;

    /// <summary>Radius (Meter), Stärke (Heben, Senken, Rauschen: Meter je Tupfer; Glätten, Ebnen: 0 … 1), weiche Kante (0 hart … 1 weich).</summary>
    public double GelaendeRadius { get; set; } = 12;
    public double GelaendeStaerke { get; set; } = 0.25;
    public double GelaendeKante { get; set; } = 0.5;

    /// <summary>Rauschen: Startwert (gleicher Startwert, gleiche Buckel) und Maßstab (Größe der Buckel in Metern).</summary>
    public int GelaendeStartwert { get; set; } = 1;
    public double GelaendeMassstab { get; set; } = 20;

    /// <summary>Ebnen: auf <see cref="GelaendeZielhoehe"/> statt auf die Höhe unter dem ersten Klick? Terrassen nehmen die Zielhöhe immer als Bezugshöhe.</summary>
    public bool GelaendeZielFest { get; set; }

    /// <summary>Zielhöhe des Ebnens bzw. Bezugshöhe der Terrassen (Meter; die Pipette setzt sie).</summary>
    public double GelaendeZielhoehe { get; set; }

    /// <summary>Terrassen: Höhe einer Stufe (Meter) und Anteil der weichen Böschung (0 … 1).</summary>
    public double GelaendeStufenhoehe { get; set; } = 2;
    public double GelaendeBoeschung { get; set; } = 0.3;

    /// <summary>Heben nur Mulden, Senken nur Spitzen (<see cref="Pinsel.NurAusgleich"/>) – je Pinsel ein eigener Haken.</summary>
    public bool GelaendeNurMulden { get; set; }
    public bool GelaendeNurSpitzen { get; set; }

    /// <summary>Wo der Geländepinsel wirkt (Steigung, Boden); <see cref="Pinselfilter.Keiner"/>: überall.</summary>
    public Pinselfilter GelaendeFilter { get; set; } = Pinselfilter.Keiner;

    /// <summary>Der nächste Linksklick nimmt mit der Pipette auf, statt zu malen (Knopf „Pipette“ in der Tafel; Strg+Klick geht immer).</summary>
    public bool PipetteBereit { get; set; }

    /// <summary>Die Pipette hat etwas aufgenommen (Zielhöhe oder Bodentextur) – die Tafel zeigt den neuen Wert.</summary>
    public event Action? WerkstattPipetteGenommen;

    /// <summary>Rampe: Breite des Bandes und Länge der weichen Seiten (Meter).</summary>
    public double RampeBreite { get; set; } = 6;
    public double RampeKante { get; set; } = 4;

    /// <summary>Die Vorgabe der Stärke je Pinsel: was beim Wechsel der Art eingestellt wird (die Einheit ist verschieden).</summary>
    public static double StaerkeVorgabe(Pinselart art) => art switch
    {
        Pinselart.Heben or Pinselart.Senken => 0.25,
        Pinselart.Rauschen => 0.6,
        Pinselart.Glaetten => 0.5,
        _ => 1,
    };

    /// <summary>Wählt die Art des Pinsels (und stellt die Stärke auf ihre Vorgabe, wenn sich die Einheit ändert).</summary>
    public void GelaendeArtWaehlen(Pinselart art)
    {
        var alteEinheitMeter = !new Pinsel(GelaendeArt).StaerkeAlsAnteil;
        var neueEinheitMeter = !new Pinsel(art).StaerkeAlsAnteil;
        if (art != GelaendeArt) PipetteBereit = false;
        GelaendeArt = art;
        if (alteEinheitMeter != neueEinheitMeter || art == Pinselart.Rauschen) GelaendeStaerke = StaerkeVorgabe(art);
    }

    /// <summary>Braucht der gewählte Pinsel eine Zielhöhe (Ebnen mit fester Höhe, Terrassen) – dann nimmt die Pipette sie auf?</summary>
    public bool GelaendeMitZielhoehe => GelaendeArt is Pinselart.Ebnen or Pinselart.Terrassen;

    /// <summary>Der Pinsel mit den gewählten Werten.</summary>
    public Pinsel GelaendePinsel => new(GelaendeArt, GelaendeRadius, GelaendeStaerke, GelaendeKante, GelaendeStartwert, GelaendeMassstab,
        Zielhoehe: GelaendeArt == Pinselart.Terrassen || (GelaendeArt == Pinselart.Ebnen && GelaendeZielFest) ? GelaendeZielhoehe : null,
        Stufenhoehe: GelaendeStufenhoehe, Boeschung: GelaendeBoeschung,
        NurAusgleich: GelaendeArt == Pinselart.Heben ? GelaendeNurMulden : GelaendeArt == Pinselart.Senken && GelaendeNurSpitzen, Filter: GelaendeFilter);

    // ═════════════════════════════════════════════════════════════════════════
    //  Pipette
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ein Linksklick mit Strg (oder nach dem Knopf „Pipette“): Ebnen und Terrassen nehmen die Höhe des Rasters unter der Maus als Zielhöhe auf,
    /// der Bodenpinsel die stärkste Textur dort. True: aufgenommen (der Klick malt nicht).
    /// </summary>
    private bool PipetteKlick(Vector2? boden, bool strg)
    {
        if (_entwurf is null || !(strg || PipetteBereit)) return false;
        var bereit = PipetteBereit;
        PipetteBereit = false;
        if (Kartenwerkzeug == Werkstattwerkzeug.Gelaende && GelaendeMitZielhoehe && boden is { } p)
        {
            GelaendeZielhoehe = Gelaendepinsel.Pipette(_entwurf.Hoehen, new Kartenort(p.X, p.Y));
            GelaendeZielFest = true;
            WerkstattMeldung?.Invoke($"Pipette: Zielhöhe {Meter(GelaendeZielhoehe)} aufgenommen.", false);
        }
        else if (Kartenwerkzeug == Werkstattwerkzeug.Boden && boden is { } q)
        {
            (BodenArt, BodenRadierer) = (_entwurf.Boden.Anteile(q.X, q.Y)[0].Textur, false);
            WerkstattMeldung?.Invoke($"Pipette: {BodenName(BodenArt)} aufgenommen.", false);
        }
        else return bereit; // der Knopf war gedrückt, aber hier gibt es nichts aufzunehmen (neben der Karte): der Klick malt trotzdem nicht
        WerkstattPipetteGenommen?.Invoke();
        return true;
    }

    private static string Meter(double m) => $"{m.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("de-DE"))} m";

    // ═════════════════════════════════════════════════════════════════════════
    //  Strich und Rampe
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Läuft ein Pinselstrich auf dem Gelände (Maustaste gedrückt)?</summary>
    private bool _wsGelaendeStrich;

    /// <summary>Der erste Klick der Rampe (null: noch keiner).</summary>
    private Vector2? _wsRampeAnfang;

    private void GelaendeStrichBeginnen(Vector2 maus)
    {
        if (_entwurf is null || BodenpunktBei(maus) is not { } boden) return;
        Befehl(() => _entwurf.GelaendeStrichBeginnen(GelaendePinsel));
        _wsGelaendeStrich = true;
        GelaendeStrichPunkt(boden);
    }

    private void GelaendeStrichWeiter(Vector2 maus)
    {
        if (!_wsGelaendeStrich || BodenpunktBei(maus) is not { } boden) return;
        GelaendeStrichPunkt(boden);
    }

    private void GelaendeStrichPunkt(Vector2 boden) => Befehl(() => _entwurf!.GelaendeStrichWeiter(new Kartenort(boden.X, boden.Y)));

    /// <summary>Loslassen: Der Strich wird ein Schritt der Geschichte.</summary>
    private void GelaendeStrichBeenden()
    {
        if (!_wsGelaendeStrich || _entwurf is null) return;
        _wsGelaendeStrich = false;
        _entwurf.GelaendeStrichBeenden();
    }

    /// <summary>Esc beim Malen: Der Strich wird beendet und gleich zurückgenommen (es bleibt kein Schritt zurück).</summary>
    private void GelaendeStrichAbbrechen()
    {
        if (!_wsGelaendeStrich || _entwurf is null) return;
        _wsGelaendeStrich = false;
        if (_entwurf.GelaendeStrichBeenden()) _entwurf.Geschichte.Rueckgaengig();
    }

    /// <summary>Klick mit der Rampe: der erste setzt den Anfang, der zweite legt die Rampe an (ein Schritt) und beginnt eine neue.</summary>
    private void RampeKlick(Vector2? boden)
    {
        if (_entwurf is null || boden is not { } p) return;
        if (_wsRampeAnfang is not { } von)
        {
            _wsRampeAnfang = p;
            return;
        }
        _wsRampeAnfang = null;
        var ok = Befehl(() => _entwurf.Rampe(new Kartenort(von.X, von.Y), new Kartenort(p.X, p.Y), RampeBreite, RampeKante));
        if (!ok) WerkstattMeldung?.Invoke("Die Rampe ändert nichts (Anfang und Ende liegen zu nah beieinander oder das Gelände ist dort schon so).", true);
    }

    /// <summary>Beendet alles Halbfertige (anderes Werkzeug, Esc).</summary>
    private void GelaendeWerkzeugAus()
    {
        GelaendeStrichAbbrechen();
        _wsRampeAnfang = null;
        PipetteBereit = false; // jede Wahl eines Werkzeugs (auch Boden) hebt eine bereitgestellte Pipette auf
        PinselAus();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Vorschau: Pinselkreis und Rampenband
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Farbe des Pinsels je Art – für den Kreis an der Maus.</summary>
    private static Color GelaendeFarbe(Pinselart art) => art switch
    {
        Pinselart.Heben => new Color(0.98f, 0.72f, 0.28f),
        Pinselart.Senken => new Color(0.45f, 0.72f, 1f),
        Pinselart.Glaetten => new Color(0.92f, 0.92f, 0.92f),
        Pinselart.Ebnen => new Color(0.65f, 0.95f, 0.45f),
        Pinselart.Terrassen => new Color(0.95f, 0.85f, 0.45f),
        _ => new Color(0.85f, 0.55f, 0.95f),
    };

    /// <summary>Jedes Bild mit dem Geländepinsel: ein Kreis auf dem Gelände (außen der Radius, innen, wo die Kante weich wird).</summary>
    private void GelaendePinselZeigen(Vector2? maus)
    {
        if (maus is not { } m) return;
        var farbe = GelaendeFarbe(GelaendeArt);
        var radius = (float)Math.Clamp(GelaendeRadius, Pinsel.MinRadius, Pinsel.MaxRadius);
        VorschauDreiecke(BandDreiecke(Kreis(m, radius), Mathf.Max(0.12f, MeterJePixel * 1.2f), 0.2f), new Color(farbe, 0.95f));
        var innen = radius * (1 - (float)Math.Clamp(GelaendeKante, 0, 1));
        if (innen > 1.5f) VorschauDreiecke(BandDreiecke(Kreis(m, innen), Mathf.Max(0.08f, MeterJePixel * 0.8f), 0.2f), new Color(farbe, 0.5f));
        VorschauPunkte(new[] { m }, new Color(farbe, 0.95f));
    }

    /// <summary>Ein geschlossener Kreis aus Punkten (so viele, dass die Stücke etwa 3 m lang sind, mindestens 32).</summary>
    private static List<Vector2> Kreis(Vector2 mitte, float radius)
    {
        var n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Tau * radius / 3f), 32, 160);
        var punkte = new List<Vector2>(n + 1);
        for (var i = 0; i <= n; i++)
        {
            var w = Mathf.Tau * i / n;
            punkte.Add(mitte + new Vector2(Mathf.Cos(w), Mathf.Sin(w)) * radius);
        }
        return punkte;
    }

    /// <summary>Jedes Bild mit der Rampe: der Anfang (nach dem ersten Klick) und ein Band bis zur Maus, sonst nur die Maus.</summary>
    private void RampeZeigen(Vector2? maus)
    {
        var farbe = new Color(0.98f, 0.8f, 0.3f);
        var halb = (float)RampeBreite / 2;
        if (_wsRampeAnfang is { } a)
        {
            VorschauPunkte(new[] { a }, new Color(1f, 1f, 1f, 0.95f));
            if (maus is { } b && b.DistanceTo(a) > 0.5f)
            {
                VorschauDreiecke(BandDreiecke(new List<Vector2> { a, b }, halb, 0.2f), new Color(farbe, 0.45f));
                VorschauDreiecke(BandDreiecke(new List<Vector2> { a, b }, halb + (float)RampeKante, 0.2f), new Color(farbe, 0.15f));
                VorschauPunkte(new[] { b }, new Color(farbe, 0.95f));
            }
        }
        else if (maus is { } m) VorschauPunkte(new[] { m }, new Color(farbe, 0.95f));
    }

    /// <summary>
    /// Höhe und Gefälle des Bodens unter der Maus für die Leiste unten („Höhe 12,4 m · Gefälle 6 %“); null, wenn die Maus nicht über dem Boden steht
    /// (über einer Tafel, gesperrte Werkstatt).
    /// </summary>
    public string? WerkstattBodenInfo()
    {
        if (_entwurf is null || WerkstattGesperrt || GetViewport().GuiGetHoveredControl() is not null) return null;
        if (_wsOrt is null && !GetViewport().GetVisibleRect().HasPoint(GetViewport().GetMousePosition())) return null;
        var ort = _wsOrt ?? BodenpunktBei(GetViewport().GetMousePosition());
        if (ort is not { } p) return null;
        var hoehe = BodenHoehe(p.X, p.Y);
        var (sx, sz) = BodenBewegt ? _gelaende!.Steigung(p.X, p.Y) : (0.0, 0.0);
        var gefaelle = Math.Sqrt(sx * sx + sz * sz) * 100;
        var kultur = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
        return $"Höhe {hoehe.ToString("0.0", kultur)} m · Gefälle {gefaelle.ToString("0", kultur)} %" + (BodenTexturInfo(p) is { } boden ? " · " + boden : "");
    }

    /// <summary>
    /// Die Anzeige unter dem Pinsel (Pinsel-Paket 1), nur mit Gelände- und Bodenpinsel: Höhe des Rasters (wie die Pipette sie nimmt) und Steigung
    /// (wie der Filter sie misst), die Bodenarten mit Anteil und ob der Filter hier greift. Null: kein Pinsel, Maus über einer Tafel oder neben dem Boden.
    /// </summary>
    public string? WerkstattPinselInfo()
    {
        if (_entwurf is null || Kartenwerkzeug is not (Werkstattwerkzeug.Gelaende or Werkstattwerkzeug.Boden) || WerkstattGesperrt) return null;
        if (GetViewport().GuiGetHoveredControl() is not null) return null;
        if (_wsOrt is null && !GetViewport().GetVisibleRect().HasPoint(GetViewport().GetMousePosition())) return null;
        if ((_wsOrt ?? BodenpunktBei(GetViewport().GetMousePosition())) is not { } p) return null;
        var kultur = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
        var roh = Gelaendepinsel.Pipette(_entwurf.Hoehen, new Kartenort(p.X, p.Y));
        var steigung = Pinselfilter.Steigung(_entwurf.Hoehen, p.X, p.Y);
        var zeilen = new List<string>();
        var bett = BodenHoehe(p.X, p.Y);
        zeilen.Add($"Höhe {Meter(roh)} · Steigung {steigung.ToString("0", kultur)} %" + (Math.Abs(bett - roh) > 0.05 ? $" (Wegbett {bett.ToString("0.00", kultur)} m)" : ""));
        var anteile = _entwurf.Boden.Anteile(p.X, p.Y);
        zeilen.Add(string.Join(" · ", anteile.Take(3).Select(a => $"{BodenName(a.Textur)} {Math.Round(a.Anteil * 100.0 / 255, MidpointRounding.AwayFromZero)} %")));
        if (Kartenwerkzeug == Werkstattwerkzeug.Gelaende && GelaendeArt == Pinselart.Terrassen)
            zeilen.Add($"Stufe hier: {Meter(new Pinsel(Pinselart.Terrassen, Zielhoehe: GelaendeZielhoehe, Stufenhoehe: GelaendeStufenhoehe, Boeschung: GelaendeBoeschung).Begrenzt().Stufe(roh * 100) / 100)}");
        var filter = Kartenwerkzeug == Werkstattwerkzeug.Gelaende ? GelaendeFilter : BodenFilter;
        if (!filter.IstLeer)
        {
            var f = filter.Begrenzt();
            var anteil = f.Boden is { } t ? anteile.FirstOrDefault(a => a.Textur == t).Anteil : 0;
            var wirkt = f.BodenPasst(anteil) ? f.SteigungFaktor(steigung) : 0;
            zeilen.Add(wirkt <= 0 ? "Filter: wirkt hier nicht" : $"Filter: wirkt hier {Math.Round(wirkt * 100, MidpointRounding.AwayFromZero)} %");
        }
        if (PipetteBereit) zeilen.Add(Kartenwerkzeug == Werkstattwerkzeug.Boden ? "Pipette: Klick nimmt die Bodenart" : "Pipette: Klick nimmt die Höhe");
        return string.Join("\n", zeilen);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Folgen: alles auf dem Gelände nachziehen
    // ═════════════════════════════════════════════════════════════════════════

    private readonly List<Kartenrechteck> _wsGelaendeKacheln = new(), _wsGelaendeObjekte = new();

    /// <summary>Der Boden hat sich im Rechteck geändert (Pinsel, Rampe, Rückgängig) oder ein Weg bzw. eine Mauer, die ihr Bett im Gelände hat.</summary>
    private void GelaendeVormerken(Kartenrechteck bereich)
    {
        _wsGelaendeKacheln.Add(bereich);
        _wsGelaendeObjekte.Add(bereich);
        _wsGeprueft = null; // das Gefälle unter dem Umriss kann sich geändert haben
    }

    private static Kartenrechteck Vereinigt(List<Kartenrechteck> liste) =>
        liste.Aggregate((a, b) => new Kartenrechteck(Math.Min(a.X0, b.X0), Math.Min(a.Z0, b.Z0), Math.Max(a.X1, b.X1), Math.Max(a.Z1, b.Z1)));

    private static Kartenrechteck RechteckVon(Rect2 r) => new(r.Position.X, r.Position.Y, r.End.X, r.End.Y);

    /// <summary>Am Ende des Bildes (aus <see cref="Nachziehen"/>): die Kacheln im geänderten Rechteck neu; ist der Strich zu Ende, auch alles, was darauf steht.</summary>
    private void GelaendeNachziehen()
    {
        if (_entwurf is null) return;
        if (_wsGelaendeKacheln.Count > 0)
        {
            var r = Vereinigt(_wsGelaendeKacheln);
            _wsGelaendeKacheln.Clear();
            GelaendeKachelnNeu(r);
        }
        if (_wsGelaendeObjekte.Count == 0 || _entwurf.StrichLaeuft) return;
        var bereich = Vereinigt(_wsGelaendeObjekte);
        _wsGelaendeObjekte.Clear();
        var rect = new Rect2((float)bereich.X0, (float)bereich.Z0, (float)bereich.Breite, (float)bereich.Tiefe).Grow(2);
        if (!GewaesserLeer) _wsWasserNeu = true; // der Spiegel der Flüsse folgt dem Gelände (Brücken, Fähren mit ihm)

        foreach (var g in _entwurf.Gebaeude.ToList())
            if (BereichVon(g) is { } b && b.Intersects(rect) && _stuecke.ContainsKey(g))
            {
                StueckEntfernen(g);
                StueckBauen(g);
            }
        foreach (var w in _entwurf.Wege.ToList())
            if (BereichVon(w) is { } b && b.Intersects(rect)) WegBauen(w, false);
        foreach (var m in _entwurf.Mauern.ToList())
            if (BereichVon(m) is { } b && b.Intersects(rect)) MauerBauen(m, false);
        foreach (var f in _entwurf.Bauzonen.Cast<EntwurfFlaeche>().Concat(_entwurf.Naturflaechen).Concat(_entwurf.Felder).ToList())
            if (BereichVon(f) is { } b && b.Intersects(rect)) FlaecheBauen(f, false);
        foreach (var p in _entwurf.Pflanzen.ToList())
            if (rect.HasPoint(V2(p.Punkt))) PflanzeBauen(p, false);
        NaturBereichVormerken(rect.Grow(NaturUeberstand)); // die Natur der Flächen und die Äcker folgen dem Boden (Stadtansicht.Kartenwerkstatt.Natur.cs)
        _wsFehlerNeu = true; // die Hangsperre hat sich vielleicht geändert
        _wsGriffeNeu = true;
        _wsNetzNeu = true;
    }
}
