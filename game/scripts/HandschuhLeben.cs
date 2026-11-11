namespace Reichsgilde;

/// <summary>Was die Winken-Logik dem Handschuh sagt.</summary>
public enum WinkenEreignis { Nichts, Start, Abbruch }

/// <summary>
/// Das Easter Egg „Handschuh winkt“, reine Zustandslogik ohne Godot (<c>HandschuhLebenTests.cs</c>): Nach <see cref="Wartezeit"/> Sekunden ohne Eingabe (Maus, Tastatur, Mausrad) dreht sich die Hand mit der Handfläche zur Kamera und winkt
/// (Clip <c>Winken</c>, 2 bis 3 Mal), danach geht sie in die vorige Haltung zurück; solange weiter keine Eingabe kommt, wiederholt es sich nach 2 bis 3 Minuten. Jede Eingabe bricht ab und setzt die Zeit zurück.
/// Nie über oder in Textfeldern (auch bei der Texteingabe), nie während der Schwarzblende oder der Klinke. Nicht abschaltbar (Nutzer). Der Zufall (Wartezeit der Wiederholung) kommt vom Aufrufer
/// (in der Darstellung ein eigener Zufallsgenerator, nicht die Zufallsquelle des Kerns).
/// </summary>
public sealed class WinkenLogik
{
    public const double Wartezeit = 60.0, WiederholungMin = 120.0, WiederholungMax = 180.0;

    private readonly Func<double> _zufall;
    private readonly double _wartezeit, _wiederMin, _wiederMax;
    private double _leer;
    private double _naechste;

    /// <summary>Normal: 60 s, danach alle 120 bis 180 s. Für den Test (<c>--winktest=1</c>) kürzere Zeiten.</summary>
    public WinkenLogik(Func<double> zufall, double wartezeit = Wartezeit, double wiederMin = WiederholungMin, double wiederMax = WiederholungMax)
    {
        (_zufall, _wartezeit, _wiederMin, _wiederMax) = (zufall, wartezeit, wiederMin, wiederMax);
        _naechste = wartezeit;
    }

    /// <summary>Die Hand winkt gerade (bis <see cref="Beendet"/> oder ein Abbruch).</summary>
    public bool Winkt { get; private set; }

    /// <summary>Sekunden ohne Eingabe seit dem Beginn oder dem letzten Winken.</summary>
    public double Leerlauf => _leer;

    /// <summary>Ein Bild weiter. <paramref name="eingabe"/>: in diesem Bild kam Maus, Taste oder Rad; <paramref name="gesperrt"/>: Textfeld unter der Maus oder in Eingabe, Blende, Klinke.</summary>
    public WinkenEreignis Schritt(double dt, bool eingabe, bool gesperrt)
    {
        if (eingabe || gesperrt)
        {
            var warWinken = Winkt;
            Winkt = false;
            _leer = 0;
            _naechste = _wartezeit;
            return warWinken ? WinkenEreignis.Abbruch : WinkenEreignis.Nichts;
        }
        if (Winkt) return WinkenEreignis.Nichts;
        _leer += Math.Max(dt, 0);
        if (_leer < _naechste) return WinkenEreignis.Nichts;
        Winkt = true;
        return WinkenEreignis.Start;
    }

    /// <summary>Der Clip ist durch und die Hand zurück in der vorigen Haltung: die Wiederholung beginnt (2 bis 3 Minuten ohne Eingabe).</summary>
    public void Beendet()
    {
        Winkt = false;
        _leer = 0;
        _naechste = _wiederMin + (_wiederMax - _wiederMin) * Math.Clamp(_zufall(), 0.0, 1.0);
    }
}

/// <summary>
/// Das ungeduldige Zucken der offenen Hand an der Tür (Haltung Bereit), reine Zustandslogik ohne Godot (<c>HandschuhLebenTests.cs</c>): alle 3 bis 6 Sekunden zufällig ein kurzer Clip (<c>Bereit_zucken</c>, etwa 0,6 s, nur Finger),
/// nur in Bereit und ohne Klinke; jeder Klick und jede andere Haltung unterbricht und setzt neu an. Der Zufall kommt vom Aufrufer (Darstellung, nicht der Kern).
/// </summary>
public sealed class BereitZucken
{
    public const double Min = 3.0, Max = 6.0;

    private readonly Func<double> _zufall;
    private double _rest;

    public BereitZucken(Func<double> zufall)
    {
        _zufall = zufall;
        Neu();
    }

    /// <summary>Der Zuck-Clip läuft gerade.</summary>
    public bool Zuckt { get; private set; }

    private void Neu() => _rest = Min + (Max - Min) * Math.Clamp(_zufall(), 0.0, 1.0);

    /// <summary>Ein Bild weiter. <paramref name="inBereit"/>: die Hand steht in Bereit (kein Übergang, keine Klinke); <paramref name="klick"/>: Taste gedrückt oder Klinke läuft. Gibt true zurück, wenn der Zuck-Clip jetzt beginnen soll.</summary>
    public bool Schritt(double dt, bool inBereit, bool klick)
    {
        if (!inBereit || klick)
        {
            if (Zuckt || klick) Neu();
            Zuckt = false;
            return false;
        }
        if (Zuckt) return false;
        _rest -= Math.Max(dt, 0);
        if (_rest > 0) return false;
        Zuckt = true;
        return true;
    }

    /// <summary>Der Zuck-Clip ist zu Ende (die Schleife läuft weiter): neues Intervall.</summary>
    public void Beendet()
    {
        Zuckt = false;
        Neu();
    }
}
