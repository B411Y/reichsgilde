namespace Reichsgilde;

/// <summary>Die drei Bücher der Innenansichten: Personalbuch (Lesepult), Familienbuch (Stubentisch), Kontorbuch (Schreibpult).</summary>
public enum BuchArt { Personal, Familie, Kontor }

/// <summary>Wo die Buchschau gerade steht: Ruhe → Hin (Schwenk, das Buch klappt auf) → Offen ⇄ Blaettern → Zurueck (zuklappen, Schwenk zurück) → Ruhe.</summary>
public enum BuchZustand { Ruhe, Hin, Offen, Blaettern, Zurueck }

/// <summary>
/// Die Maße eines Buchs: Cliplängen und Zeiten der Inhaltsflächen aus dem Modell (<c>docs/bereiche/darstellung.md</c>, „3D-Bücher für Innenansichten“),
/// Breite und Höhe des aufgeschlagenen Buchs in Metern. Zeiten in Sekunden.
/// </summary>
public readonly record struct BuchMasse(
    double Aufklappen, double Zuklappen, double Umblaettern, double InhaltAb, double InhaltBis, double BlattInhaltAb, double BreiteOffen, double HoeheOffen)
{
    /// <summary>Die Werte der drei Modelle (<c>buch_personal.glb</c>, <c>buch_familie.glb</c>, <c>buch_kontor.glb</c>).</summary>
    public static BuchMasse Von(BuchArt art) => art switch
    {
        BuchArt.Personal => new BuchMasse(1.033, 0.867, 0.733, 0.533, 0.100, 0.700, 0.526, 0.310),
        BuchArt.Familie => new BuchMasse(0.933, 0.800, 0.733, 0.433, 0.100, 0.700, 0.471, 0.284),
        _ => new BuchMasse(0.733, 0.667, 0.600, 0.367, 0.067, 0.567, 0.506, 0.372),
    };
}

/// <summary>
/// Die reine Ablauflogik der Buchschau ohne Godot, damit sie sich testen lässt (<c>BuchAblaufTests.cs</c>): Die Kamera schwenkt in
/// <see cref="SchwenkSekunden"/> abgebremst zum Buch, das Buch klappt dabei auf, die 2D-Tafel liegt darüber, sobald die Seiten flach liegen;
/// beim Reiterwechsel blättert es um (die Tafel ist so lange weg), beim Schließen klappt es zu und die Kamera fährt zurück.
/// Während des Schwenks (<see cref="Gesperrt"/>) ist nichts anklickbar. Ohne Bewegung (Einstellung „Siegel und Briefe bewegen“ aus)
/// steht das Buch sofort offen und ist sofort wieder zu.
/// </summary>
public sealed class BuchAblauf
{
    /// <summary>Dauer des Kameraschwenks hin und zurück.</summary>
    public const double SchwenkSekunden = 0.8;

    /// <summary>Das Buch beginnt aufzuklappen, wenn der Schwenk so weit ist; beim Schließen fährt die Kamera los, wenn das Zuklappen so weit ist.</summary>
    public const double AufklappenAb = 0.45, ZurueckAb = 0.25;

    private readonly BuchMasse _m;
    private double _zeit;
    private bool _vorwaerts = true;

    public BuchAblauf(BuchMasse masse) => _m = masse;

    public BuchZustand Zustand { get; private set; } = BuchZustand.Ruhe;

    /// <summary>Läuft die Schau (alles außer Ruhe)?</summary>
    public bool Aktiv => Zustand != BuchZustand.Ruhe;

    /// <summary>Während des Schwenks hin und zurück ist nichts anklickbar.</summary>
    public bool Gesperrt => Zustand is BuchZustand.Hin or BuchZustand.Zurueck;

    /// <summary>Der Clip des Modells, der gerade gilt.</summary>
    public string Clip => Zustand switch
    {
        BuchZustand.Hin => "aufklappen",
        BuchZustand.Offen => "offen",
        BuchZustand.Blaettern => _vorwaerts ? "umblaettern" : "umblaettern_zurueck",
        BuchZustand.Zurueck => "zuklappen",
        _ => "zu",
    };

    /// <summary>Die Stelle im Clip in Sekunden.</summary>
    public double Clipzeit => Zustand switch
    {
        BuchZustand.Hin => Math.Clamp(_zeit - AufklappenAb, 0, _m.Aufklappen),
        BuchZustand.Blaettern => Math.Min(_zeit, _m.Umblaettern),
        BuchZustand.Zurueck => Math.Min(_zeit, _m.Zuklappen),
        _ => 0,
    };

    /// <summary>Wie weit die Kamera beim Buch ist: 0 = alter Blick, 1 = vor dem Buch. Hin abgebremst, zurück weich an- und abgefahren.</summary>
    public double Kamera
    {
        get
        {
            switch (Zustand)
            {
                case BuchZustand.Ruhe: return 0;
                case BuchZustand.Hin:
                {
                    var t = Math.Clamp(_zeit / SchwenkSekunden, 0, 1);
                    return 1 - (1 - t) * (1 - t) * (1 - t);
                }
                case BuchZustand.Zurueck:
                {
                    var t = Math.Clamp((_zeit - ZurueckAb) / SchwenkSekunden, 0, 1);
                    return 1 - t * t * (3 - 2 * t);
                }
                default: return 1;
            }
        }
    }

    /// <summary>Liegt die 2D-Tafel über dem Buch? Erst wenn die Seiten flach liegen, beim Umblättern nicht.</summary>
    public bool TafelSichtbar => Zustand switch
    {
        BuchZustand.Hin => _zeit - AufklappenAb >= _m.InhaltAb,
        BuchZustand.Offen => true,
        BuchZustand.Blaettern => _zeit >= _m.BlattInhaltAb,
        _ => false,
    };

    private double HinDauer => Math.Max(SchwenkSekunden, AufklappenAb + _m.Aufklappen);
    private double ZurueckDauer => Math.Max(_m.Zuklappen, ZurueckAb + SchwenkSekunden);

    /// <summary>Das Buch wurde angeklickt. Gibt false zurück, wenn schon eine Schau läuft.</summary>
    public bool Oeffnen(bool bewegt)
    {
        if (Aktiv) return false;
        _zeit = 0;
        Zustand = bewegt ? BuchZustand.Hin : BuchZustand.Offen;
        return true;
    }

    /// <summary>Reiterwechsel: Das Blatt wendet sich (vorwärts oder zurück). Ohne Bewegung und während des Schwenks geschieht nichts.</summary>
    public bool Blaettern(bool vorwaerts, bool bewegt)
    {
        if (!bewegt || Zustand is not (BuchZustand.Offen or BuchZustand.Blaettern)) return false;
        _vorwaerts = vorwaerts;
        _zeit = 0;
        Zustand = BuchZustand.Blaettern;
        return true;
    }

    /// <summary>Esc oder Klick neben das Buch: zuklappen, Kamera zurück. Während des Schwenks geschieht nichts.</summary>
    public bool Schliessen(bool bewegt)
    {
        if (Zustand is not (BuchZustand.Offen or BuchZustand.Blaettern)) return false;
        _zeit = 0;
        Zustand = bewegt ? BuchZustand.Zurueck : BuchZustand.Ruhe;
        return true;
    }

    /// <summary>Sofort zurück in die Ruhe (Raumwechsel, Haus verlassen): kein Zuklappen, kein Schwenk.</summary>
    public void Abbrechen()
    {
        _zeit = 0;
        Zustand = BuchZustand.Ruhe;
    }

    public void Weiter(double delta)
    {
        if (Zustand is BuchZustand.Ruhe or BuchZustand.Offen) return;
        _zeit += Math.Max(0, delta);
        switch (Zustand)
        {
            case BuchZustand.Hin when _zeit >= HinDauer:
            case BuchZustand.Blaettern when _zeit >= _m.Umblaettern:
                Zustand = BuchZustand.Offen;
                _zeit = 0;
                break;
            case BuchZustand.Zurueck when _zeit >= ZurueckDauer:
                Zustand = BuchZustand.Ruhe;
                _zeit = 0;
                break;
        }
    }
}
