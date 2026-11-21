namespace Reichsgilde;

/// <summary>Wo der große 3D-Brief gerade steht.</summary>
public enum BriefZustand { Weg, Auffalten, Offen, Zufalten }

/// <summary>
/// Die Zeiten des großen Briefs: Längen der Clips <c>auffalten</c> und <c>zufalten</c> aus der .glb, die Sichtbarkeit der Inhaltsfläche (Zeiten des
/// Modells, <c>docs/bereiche/darstellung.md</c>) und das kurze Ein- und Ausblenden aus dem Thema (<c>meldung_thema.tres</c>). Alles in Sekunden.
/// </summary>
public readonly record struct BriefMasse(double AufSekunden, double ZuSekunden, double InhaltAb, double InhaltBis, double EinblendSekunden, double AusblendSekunden, double Tempo = 1);

/// <summary>
/// Die reine Ablauflogik des großen 3D-Briefs (<see cref="BriefBuehne"/>, <see cref="BriefAnzeige"/>) ohne Godot, damit sie sich testen lässt
/// (<c>BriefAblaufTests.cs</c>): Das Siegel bricht und der Brief faltet sich auf (Clip <c>auffalten</c>); kurz vor dem Ende liegt der Inhalt als Bild auf
/// der Inhaltsfläche; sobald er offen ist, liegt die echte 2D-Tafel darüber. Erledigt: Der Inhalt liegt noch einen Augenblick als Bild auf dem Blatt,
/// dann faltet sich der Brief zu (Clip <c>zufalten</c>, ohne Hüpfen) und ist weg. Ohne Bewegung (Einstellung „Siegel und Briefe bewegen“ aus)
/// steht der Brief sofort offen und ist sofort weg. Dazu die Streckregel des Modells als reine Rechnung.
/// </summary>
public sealed class BriefAblauf
{
    /// <summary>Maße des Modells <c>brief_gross.glb</c> in Metern: Blattbreite, kleinste und größte Blatthöhe, Rand um die Inhaltsfläche, feste Falzzonen, Mitte.</summary>
    public const double BlattBreite = 0.30, HoeheMin = 0.42, HoeheMax = 0.60, Rand = 0.02, Falze = 0.24, Mitte = 0.18;

    /// <summary>Breite der Inhaltsfläche (Blatt ohne die beiden Ränder).</summary>
    public const double InhaltBreite = BlattBreite - 2 * Rand;

    private readonly BriefMasse _m;
    private double _clipzeit;

    public BriefAblauf(BriefMasse masse) => _m = masse;

    public BriefZustand Zustand { get; private set; } = BriefZustand.Weg;

    /// <summary>Der Clip der .glb, der gerade gilt, und die Stelle darin (Sekunden).</summary>
    public string Clip => Zustand is BriefZustand.Zufalten or BriefZustand.Weg ? "zufalten" : "auffalten";
    public double Clipzeit => _clipzeit;

    /// <summary>Der Brief ist zu sehen (faltet sich auf, liegt offen oder faltet sich zu).</summary>
    public bool Sichtbar => Zustand != BriefZustand.Weg;

    /// <summary>Die echte 2D-Tafel liegt auf dem Blatt (Text scharf, Knöpfe klickbar): nur, solange der Brief offen ist.</summary>
    public bool TafelSichtbar => Zustand == BriefZustand.Offen;

    /// <summary>Der Inhalt liegt als Bild auf der Inhaltsfläche: am Ende des Auffaltens und am Anfang des Zufaltens (solange das Blatt flach ist).</summary>
    public bool BildSichtbar => Zustand switch
    {
        BriefZustand.Auffalten => _clipzeit >= _m.InhaltAb,
        BriefZustand.Zufalten => _clipzeit <= _m.InhaltBis,
        _ => false,
    };

    /// <summary>Der Brief wird geöffnet: Siegel bricht, auffalten; ohne Bewegung sofort offen. Mitten im Zufalten geht es an der passenden Stelle zurück.</summary>
    public void Oeffnen(bool bewegt)
    {
        if (Zustand is BriefZustand.Auffalten or BriefZustand.Offen) return;
        if (!bewegt)
        {
            Zustand = BriefZustand.Offen;
            _clipzeit = _m.AufSekunden;
            return;
        }
        _clipzeit = Zustand == BriefZustand.Zufalten ? Gespiegelt(_clipzeit, _m.ZuSekunden, _m.AufSekunden) : 0;
        Zustand = BriefZustand.Auffalten;
    }

    /// <summary>Die Meldung ist erledigt: zufalten und weg (kein Hüpfen); ohne Bewegung sofort weg. Mitten im Auffalten geht es an der passenden Stelle zurück.</summary>
    public void Schliessen(bool bewegt)
    {
        if (Zustand is BriefZustand.Zufalten or BriefZustand.Weg) return;
        if (!bewegt)
        {
            Zustand = BriefZustand.Weg;
            return;
        }
        _clipzeit = Zustand == BriefZustand.Auffalten ? Gespiegelt(_clipzeit, _m.AufSekunden, _m.ZuSekunden) : 0;
        Zustand = BriefZustand.Zufalten;
    }

    private static double Gespiegelt(double zeit, double von, double nach) => von <= 0 ? 0 : Math.Clamp((1 - zeit / von) * nach, 0, nach);

    /// <summary>Rechnet die Zeit weiter. Ohne Bewegung springt der Brief an das Ende des laufenden Schritts.</summary>
    public void Weiter(double dt, bool bewegt)
    {
        dt = bewegt ? Math.Max(0, dt) * Math.Max(_m.Tempo, 0.01) : double.MaxValue;
        switch (Zustand)
        {
            case BriefZustand.Auffalten:
                _clipzeit = Math.Min(_m.AufSekunden, _clipzeit + dt);
                if (_clipzeit >= _m.AufSekunden) Zustand = BriefZustand.Offen;
                break;
            case BriefZustand.Zufalten:
                _clipzeit = Math.Min(_m.ZuSekunden, _clipzeit + dt);
                if (_clipzeit >= _m.ZuSekunden) Zustand = BriefZustand.Weg;
                break;
        }
    }

    /// <summary>Deckkraft von 0 bis 1: blendet am Anfang des Auffaltens kurz ein und am Ende des Zufaltens kurz aus.</summary>
    public double Deckkraft => Zustand switch
    {
        BriefZustand.Weg => 0,
        BriefZustand.Auffalten when _m.EinblendSekunden > 0 => Math.Clamp(_clipzeit / _m.EinblendSekunden, 0, 1),
        BriefZustand.Zufalten when _m.AusblendSekunden > 0 => Math.Clamp((_m.ZuSekunden - _clipzeit) / _m.AusblendSekunden, 0, 1),
        _ => 1,
    };

    /// <summary>Die Blatthöhe (Meter) zu einer Höhe des Inhalts: Inhalt plus die beiden Ränder, begrenzt auf den Bereich des Modells (0,42 bis 0,60 m).</summary>
    public static double Blatthoehe(double inhaltMeter) => Math.Clamp(inhaltMeter + 2 * Rand, HoeheMin, HoeheMax);

    /// <summary>
    /// Die Streckregel des Modells zu einer Blatthöhe: Skala Y des Knochens <c>Streck</c> (1 bis 2; nur die Mitte dehnt sich), Versatz der Knochen
    /// <c>Anker_oben</c> (+) und <c>Anker_unten</c> (−) in Metern (die Falzzonen wandern nur) und die Skala Y der <c>Inhaltsflaeche</c>.
    /// </summary>
    public static (double Skala, double Versatz, double InhaltSkala) Streckung(double hoehe)
    {
        hoehe = Math.Clamp(hoehe, HoeheMin, HoeheMax);
        var s = (hoehe - Falze) / Mitte;
        return (s, (s - 1) * Mitte / 2, (hoehe - 2 * Rand) / (HoeheMin - 2 * Rand));
    }
}
