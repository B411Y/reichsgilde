using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Die Bodentexturen in der Kartenwerkstatt – ein Teil der <c>partial class Stadtansicht</c> (Zeichnen: Stadtansicht.Boden.cs; Überblick in
/// Stadtansicht.Kartenwerkstatt.cs). Der Kern führt das Bodenraster und die Pinsel (<see cref="Kartenentwurf.BodenStrichBeginnen"/>); hier steht, was die
/// Werkstatt in der 3D-Welt daraus macht:
/// <list type="bullet">
/// <item><b>Pinsel</b> (<see cref="Werkstattwerkzeug.Boden"/>): eine der elf Texturen oder der Radierer (zurück zur Wiese). Mit gedrückter Maus malt der
/// Strich; jede Mausbewegung gibt der Kern weiter, und nur die Bilder der Kacheln unter dem Pinsel werden ersetzt. Loslassen beendet den Strich (ein
/// Rückgängig-Schritt), Esc nimmt ihn zurück. Der Kreis an der Maus zeigt Radius und weiche Kante in der Farbe der Textur.</item>
/// <item><b>Deko</b> (<see cref="Bodendeko"/>): je Bodenkachel (64 m) ein Knoten mit je einem MultiMesh je Art. Neu gestreut wird nach dem Strich und wo sich
/// ein Hindernis (Gebäude, Weg, Bauzone, Mauer, Wasser) oder das Gelände geändert hat – nur die Kacheln dort.</item>
/// <item><b>Boden unter der Maus</b> (<see cref="WerkstattBodenInfo"/>): Name der Textur für die Leiste unten.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    // ═════════════════════════════════════════════════════════════════════════
    //  Einstellungen des Pinsels (die Tafel rechts ändert sie)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Textur des Pinsels, und ob stattdessen der Radierer gewählt ist (zurück zur Wiese, ohne Deko).</summary>
    public Bodentextur BodenArt { get; set; } = Bodentextur.Erde;
    public bool BodenRadierer { get; set; }

    /// <summary>Radius (Meter), Stärke (0 … 1), weiche Kante (0 hart … 1 weich), Deko streuen und Dichte der Deko (0 … 2).</summary>
    public double BodenRadius { get; set; } = 8;
    public double BodenStaerke { get; set; } = 1;
    public double BodenKante { get; set; } = 0.5;
    public bool BodenDeko { get; set; } = true;
    public double BodenDichte { get; set; } = 1;

    /// <summary>Der Pinsel mit den gewählten Werten.</summary>
    public Bodenpinsel BodenPinsel => BodenRadierer
        ? Bodenpinsel.Radierer(BodenRadius, BodenKante)
        : new Bodenpinsel(BodenArt, BodenRadius, BodenStaerke, BodenKante, BodenDeko, BodenDichte);

    /// <summary>Die Farbe einer Textur aus dem Katalog (Vorschau im Katalog und Kreis an der Maus).</summary>
    public Color BodenFarbe(Bodentextur t) =>
        _entwurf?.Katalog.TexturDaten(t) is { Farbe: { Length: 7 } hex } && Color.FromHtml(hex) is var f ? f : new Color(0.5f, 0.5f, 0.5f);

    /// <summary>Der Name einer Textur aus dem Katalog.</summary>
    public string BodenName(Bodentextur t) => _entwurf?.Katalog.TexturDaten(t)?.Name ?? t.ToString();

    // ═════════════════════════════════════════════════════════════════════════
    //  Strich
    // ═════════════════════════════════════════════════════════════════════════

    private bool _wsBodenStrich;

    private void BodenStrichBeginnen(Vector2 maus)
    {
        if (_entwurf is null || BodenpunktBei(maus) is not { } boden) return;
        Befehl(() => _entwurf.BodenStrichBeginnen(BodenPinsel.Begrenzt()));
        _wsBodenStrich = true;
        BodenStrichPunkt(boden);
    }

    private void BodenStrichWeiter(Vector2 maus)
    {
        if (!_wsBodenStrich || BodenpunktBei(maus) is not { } boden) return;
        BodenStrichPunkt(boden);
    }

    private void BodenStrichPunkt(Vector2 boden) => Befehl(() => _entwurf!.BodenStrichWeiter(new Kartenort(boden.X, boden.Y)));

    /// <summary>Loslassen: Der Strich wird ein Schritt der Geschichte; danach streut die Deko neu.</summary>
    private void BodenStrichBeenden()
    {
        if (!_wsBodenStrich || _entwurf is null) return;
        _wsBodenStrich = false;
        _entwurf.BodenStrichBeenden();
    }

    /// <summary>Esc beim Malen: Der Strich wird beendet und gleich zurückgenommen (es bleibt kein Schritt zurück).</summary>
    private void BodenStrichAbbrechen()
    {
        if (!_wsBodenStrich || _entwurf is null) return;
        _wsBodenStrich = false;
        if (_entwurf.BodenStrichBeenden()) _entwurf.Geschichte.Rueckgaengig();
    }

    private void BodenWerkzeugAus() => BodenStrichAbbrechen();

    // ═════════════════════════════════════════════════════════════════════════
    //  Vorschau: der Kreis an der Maus
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Jedes Bild mit dem Bodenpinsel: ein Kreis auf dem Gelände (außen der Radius, innen, wo die Kante weich wird), in der Farbe der Textur.</summary>
    private void BodenPinselZeigen(Vector2? maus)
    {
        if (maus is not { } m) return;
        var farbe = BodenRadierer ? new Color(0.95f, 0.35f, 0.3f) : BodenFarbe(BodenArt).Lightened(0.35f);
        var radius = (float)Math.Clamp(BodenRadius, Bodenpinsel.MinRadius, Bodenpinsel.MaxRadius);
        VorschauDreiecke(BandDreiecke(Kreis(m, radius), Mathf.Max(0.12f, MeterJePixel * 1.2f), 0.2f), new Color(farbe, 0.95f));
        var innen = radius * (1 - (float)Math.Clamp(BodenKante, 0, 1));
        if (innen > 1.5f) VorschauDreiecke(BandDreiecke(Kreis(m, innen), Mathf.Max(0.08f, MeterJePixel * 0.8f), 0.2f), new Color(farbe, 0.5f));
        VorschauPunkte(new[] { m }, new Color(farbe, 0.95f));
    }

    /// <summary>Für die Leiste unten: die Textur unter der Maus („Boden: Waldboden“), wenn irgendwo gemalt ist oder der Bodenpinsel gewählt ist; sonst null.</summary>
    private string? BodenTexturInfo(Vector2 p)
    {
        if (_entwurf is null || (!BodenGemalt && Kartenwerkzeug != Werkstattwerkzeug.Boden)) return null;
        return "Boden: " + BodenName(_entwurf.Boden.Textur(p.X, p.Y));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Folgen: Bilder und Deko nachziehen
    // ═════════════════════════════════════════════════════════════════════════

    private readonly List<Kartenrechteck> _wsBodenBereiche = new();
    private readonly List<Rect2> _wsDekoBereiche = new();

    /// <summary>Der Boden hat sich im Rechteck geändert (Strich, Rückgängig, Wiederholen).</summary>
    private void BodenVormerken(Kartenrechteck bereich)
    {
        _wsBodenBereiche.Add(bereich);
        var r = new Rect2((float)bereich.X0, (float)bereich.Z0, (float)bereich.Breite, (float)bereich.Tiefe);
        _wsDekoBereiche.Add(r);
    }

    /// <summary>Deko der Bodenkacheln, je Kachel ein Knoten.</summary>
    private readonly Dictionary<(int I, int J), Node3D> _wsBodenDeko = new();
    private Node3D? _wsBodenDekoKnoten;

    /// <summary>Beim Aufbau der Werkstatt: die Deko aller Kacheln.</summary>
    private void BodendekoAufbauen()
    {
        if (_entwurf is null) return;
        _wsBodenDekoKnoten = new Node3D { Name = "Bodendeko" };
        AddChild(_wsBodenDekoKnoten);
        var deko = _entwurf.BodendekoVon(_entwurf.Freiraum(Modellmass));
        foreach (var k in deko.Kacheln) BodendekoKachel(deko, k);
    }

    private void BodendekoKachel(Bodendeko deko, (int I, int J) kachel)
    {
        if (_wsBodenDeko.Remove(kachel, out var alt)) alt.QueueFree();
        if (_wsBodenDekoKnoten is null) return;
        var lagen = deko.Kachel(kachel);
        if (lagen.Count == 0) return;
        var knoten = new Node3D { Name = $"Deko_{kachel.I}_{kachel.J}" };
        _wsBodenDekoKnoten.AddChild(knoten);
        _wsBodenDeko[kachel] = knoten;
        LagenZeichnen(knoten, lagen);
    }

    /// <summary>
    /// Am Ende des Bildes (aus <see cref="Nachziehen"/>, vor der Natur): die Bilder der Kacheln im geänderten Rechteck ersetzen und, wenn kein Strich läuft,
    /// die Deko der Kacheln neu streuen, die ein geändertes Rechteck oder ein verändertes Hindernis (<see cref="_wsNaturBereiche"/>) berührt.
    /// </summary>
    private void BodenNachziehen()
    {
        if (_entwurf is null) return;
        if (_wsBodenBereiche.Count > 0)
        {
            var r = Vereinigt(_wsBodenBereiche);
            _wsBodenBereiche.Clear();
            BodenStoffeNeu(r);
        }
        if (_wsNaturBereiche.Count > 0) _wsDekoBereiche.AddRange(_wsNaturBereiche);
        if (_wsDekoBereiche.Count == 0 || _entwurf.BodenStrichLaeuft || _wsBodenStrich) return;
        var bereiche = _wsDekoBereiche.Select(b => b.Grow((float)Bodendeko.Rand + 1)).ToList();
        _wsDekoBereiche.Clear();
        var kacheln = _entwurf.Boden.Kacheln.Concat(_wsBodenDeko.Keys).Distinct()
            .Where(k =>
            {
                var b = Bodenraster.KachelBereich(k);
                var rahmen = new Rect2((float)b.X0, (float)b.Z0, (float)b.Breite, (float)b.Tiefe);
                return bereiche.Any(x => x.Intersects(rahmen));
            }).ToList();
        if (kacheln.Count == 0) return;
        var deko = _entwurf.BodendekoVon(_entwurf.Freiraum(Modellmass));
        foreach (var k in kacheln) BodendekoKachel(deko, k);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Startoption
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Für Startoptionen: ein Pinselstrich (ein Schritt) mit Textur (oder „radierer“), Radius, Stärke und den Punkten; die Kante, Deko und Dichte
    /// stehen so, wie sie im Pinsel eingestellt sind. Gibt false zurück, wenn nichts geändert wurde.
    /// </summary>
    public bool WerkstattBodenMalen(Bodenpinsel pinsel, IReadOnlyList<Kartenort> strich) =>
        _entwurf is not null && Befehl(() => _entwurf.BodenMalen(pinsel.Begrenzt(), strich));
}
