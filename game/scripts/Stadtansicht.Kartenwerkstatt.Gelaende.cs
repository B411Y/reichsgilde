using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Das Gelände in der Kartenwerkstatt – ein Teil der <c>partial class Stadtansicht</c> (Überblick in Stadtansicht.Kartenwerkstatt.cs, Boden und
/// Höhen in Stadtansicht.Gelaende.cs). Der Kern rechnet das Gelände (<see cref="Kartenentwurf.Gelaendeform"/>) und führt die Pinsel aus; hier steht, was
/// die Werkstatt in der 3D-Welt daraus macht:
/// <list type="bullet">
/// <item><b>Pinsel</b> (<see cref="Werkstattwerkzeug.Gelaende"/>): Heben, Senken, Glätten, Ebnen, Rauschen. Mit gedrückter Maus malt der Strich; jede Mausbewegung
/// gibt der Kern weiter (<see cref="Kartenentwurf.GelaendeStrichWeiter"/>), das Raster ändert sich sofort, und nur die Kacheln unter dem Pinsel werden neu
/// gezeichnet. Loslassen beendet den Strich (ein Rückgängig-Schritt). Der Kreis an der Maus zeigt Radius und weiche Kante auf dem Gelände.</item>
/// <item><b>Rampe</b> (<see cref="Werkstattwerkzeug.Rampe"/>): zwei Klicks – Anfang und Ende; dazwischen ein Band mit gleichmäßigem Gefälle.</item>
/// <item><b>Folgen</b>: Was auf dem Gelände steht, folgt ihm – nach einem Strich werden die Gebäude, Wege, Mauern, Flächen, Pflanzen und die Natur im geänderten
/// Rechteck neu gebaut (erst nach dem Strich, nicht bei jeder Mausbewegung); Wege und Mauern ändern ihr Bett im Gelände und damit die Kacheln unter ihnen.</item>
/// <item><b>Höhenanzeige</b> (<see cref="WerkstattBodenInfo"/>): Höhe und Gefälle unter der Maus für die Leiste unten.</item>
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
        var alteEinheitMeter = GelaendeArt is Pinselart.Heben or Pinselart.Senken or Pinselart.Rauschen;
        var neueEinheitMeter = art is Pinselart.Heben or Pinselart.Senken or Pinselart.Rauschen;
        GelaendeArt = art;
        if (alteEinheitMeter != neueEinheitMeter || art == Pinselart.Rauschen) GelaendeStaerke = StaerkeVorgabe(art);
    }

    /// <summary>Der Pinsel mit den gewählten Werten.</summary>
    public Pinsel GelaendePinsel => new(GelaendeArt, GelaendeRadius, GelaendeStaerke, GelaendeKante, GelaendeStartwert, GelaendeMassstab);

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
        return $"Höhe {hoehe.ToString("0.0", kultur)} m · Gefälle {gefaelle.ToString("0", kultur)} %";
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
