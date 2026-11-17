namespace Reichsgilde;

/// <summary>Wo eine Schriftrolle der Meldungsleiste gerade steht.</summary>
public enum RollenZustand { Zu, Aufrollen, Offen, Zurollen, Weg }

/// <summary>
/// Die Maße einer Schriftrolle – alle aus dem Thema der Meldungen (<c>meldung_thema.tres</c>, <see cref="SchriftrollenBuehne"/> liest sie dort),
/// die Cliplänge aus der .glb. Zeiten in Sekunden, Winkel in Grad, Höhe in Metern der kleinen 3D-Bühne.
/// </summary>
public readonly record struct RollenMasse(
    double ClipSekunden, double SymbolAb, double NeigungGrad, double PendelGrad, double HuepfHoehe, double HuepfSekunden, double AusblendSekunden);

/// <summary>
/// Die reine Ablauflogik einer 3D-Schriftrolle (<see cref="SchriftrollenBuehne"/>) ohne Godot, damit sie sich testen lässt
/// (<c>SchriftrolleAblaufTests.cs</c>): erscheint geschlossen, rollt mit einem kurzen Sprung auf, dreht sich offen ganz leicht nach links und rechts
/// (nur um die Hochachse Y, jede Rolle mit eigener Phase und eigenem Tempo), rollt zu und ist weg – ohne Sprung. Die Neigung schaut immer schräg
/// nach oben zum Spieler und bleibt fest. Ohne Bewegung (Einstellung „Siegel und Briefe bewegen“ aus) steht die Rolle sofort offen und still.
/// </summary>
public sealed class SchriftrolleAblauf
{
    /// <summary>Grunddauer einer Schwingung nach links und rechts in Sekunden (das Tempo je Rolle streckt oder staucht sie).</summary>
    private const double Periode = 6.7;
    private const double PendelAn = 0.7, PendelAus = 0.2;

    private readonly RollenMasse _m;
    private readonly double _phase;
    private readonly double _tempo;
    private double _zeit, _clipzeit, _huepfen = -1, _pendel;

    /// <summary>Zwei Zahlen von 0 bis 1 für Phase und Tempo – je Rolle andere, damit nicht alle gleich pendeln.</summary>
    public SchriftrolleAblauf(RollenMasse masse, double zufallPhase, double zufallTempo)
    {
        _m = masse;
        _phase = zufallPhase * Math.Tau;
        _tempo = 0.75 + 0.5 * Math.Clamp(zufallTempo, 0, 1);
    }

    public RollenZustand Zustand { get; private set; } = RollenZustand.Zu;

    /// <summary>Der Clip der .glb, der gerade gilt, und die Stelle darin (Sekunden).</summary>
    public string Clip => Zustand is RollenZustand.Zurollen or RollenZustand.Weg ? "zurollen" : "aufrollen";
    public double Clipzeit => _clipzeit;

    /// <summary>Das Symbol steht auf der Rolle, sobald sie weit genug offen ist (Zeiten des Modells).</summary>
    public bool SymbolSichtbar => Zustand switch
    {
        RollenZustand.Aufrollen => _clipzeit >= _m.SymbolAb,
        RollenZustand.Offen => true,
        RollenZustand.Zurollen => _clipzeit <= _m.SymbolAb,
        _ => false,
    };

    /// <summary>Die Rolle erscheint geschlossen und rollt mit einem kurzen Sprung auf; ohne Bewegung steht sie sofort offen.</summary>
    public void Erscheinen(bool bewegt)
    {
        _pendel = 0;
        if (!bewegt)
        {
            Zustand = RollenZustand.Offen;
            _clipzeit = _m.ClipSekunden;
            _huepfen = -1;
            return;
        }
        Zustand = RollenZustand.Aufrollen;
        _clipzeit = 0;
        _huepfen = 0;
    }

    /// <summary>Die Meldung ist erledigt: nur zurollen, dann verschwinden (kein Sprung); ohne Bewegung sofort weg.</summary>
    public void Schliessen(bool bewegt)
    {
        if (Zustand is RollenZustand.Zurollen or RollenZustand.Weg) return;
        if (!bewegt)
        {
            Zustand = RollenZustand.Weg;
            return;
        }
        // Mitten im Aufrollen geschlossen: an der passenden Stelle des Zurollens weitermachen, statt erst ganz aufzuspringen
        _clipzeit = Zustand == RollenZustand.Aufrollen ? Math.Clamp(_m.ClipSekunden - _clipzeit, 0, _m.ClipSekunden) : 0;
        Zustand = RollenZustand.Zurollen;
        _huepfen = -1;
    }

    /// <summary>Ein kurzer Sprung auf der Stelle (eine weitere Meldung kam auf denselben Stapel).</summary>
    public void Huepfen()
    {
        if (Zustand is RollenZustand.Offen && _huepfen < 0) _huepfen = 0;
    }

    /// <summary>Rechnet die Zeit weiter. Ohne Bewegung springt die Rolle an das Ende des laufenden Schritts und steht still.</summary>
    public void Weiter(double dt, bool bewegt)
    {
        if (Zustand == RollenZustand.Weg) return;
        if (!bewegt)
        {
            _huepfen = -1;
            _pendel = 0;
            if (Zustand == RollenZustand.Zurollen) Zustand = RollenZustand.Weg;
            else
            {
                Zustand = RollenZustand.Offen;
                _clipzeit = _m.ClipSekunden;
            }
            return;
        }
        dt = Math.Max(0, dt);
        _zeit += dt;
        if (_huepfen >= 0)
        {
            _huepfen += dt;
            if (_huepfen >= _m.HuepfSekunden) _huepfen = -1;
        }
        switch (Zustand)
        {
            case RollenZustand.Zu:
                break;
            case RollenZustand.Aufrollen:
                _clipzeit += dt;
                if (_clipzeit >= _m.ClipSekunden)
                {
                    _clipzeit = _m.ClipSekunden;
                    Zustand = RollenZustand.Offen;
                }
                break;
            case RollenZustand.Offen:
                _pendel = Math.Min(1, _pendel + dt / PendelAn);
                break;
            case RollenZustand.Zurollen:
                _pendel = Math.Max(0, _pendel - dt / PendelAus);
                _clipzeit += dt;
                if (_clipzeit >= _m.ClipSekunden)
                {
                    _clipzeit = _m.ClipSekunden;
                    Zustand = RollenZustand.Weg;
                }
                break;
        }
    }

    /// <summary>Höhe des Sprungs über der Ruhelage (Meter): einmal hoch und zurück wie ein geworfener Stein, ohne Nachschwingen.</summary>
    public double Hoehe
    {
        get
        {
            if (_huepfen < 0 || _m.HuepfSekunden <= 0) return 0;
            var u = Math.Clamp(_huepfen / _m.HuepfSekunden, 0, 1);
            return _m.HuepfHoehe * 4 * u * (1 - u);
        }
    }

    /// <summary>
    /// Die Drehung in Grad um X, Y und Z: X ist die feste Neigung nach oben zum Spieler (positiv heißt hier „Oberkante nach hinten“, nie negativ),
    /// Y das leise Drehen nach links und rechts im Leerlauf (höchstens <see cref="RollenMasse.PendelGrad"/>), Z bleibt 0.
    /// </summary>
    public (double X, double Y, double Z) Drehung =>
        (Math.Max(0, _m.NeigungGrad), _pendel * _m.PendelGrad * Math.Sin(_zeit * _tempo * Math.Tau / Periode + _phase), 0);

    /// <summary>Deckkraft von 0 bis 1: voll, nur am Ende des Zurollens blendet die Rolle kurz aus.</summary>
    public double Deckkraft
    {
        get
        {
            if (Zustand == RollenZustand.Weg) return 0;
            if (Zustand != RollenZustand.Zurollen || _m.AusblendSekunden <= 0) return 1;
            return Math.Clamp((_m.ClipSekunden - _clipzeit) / _m.AusblendSekunden, 0, 1);
        }
    }
}
