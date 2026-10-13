namespace Reichsgilde.Core.Zeit;

/// <summary>
/// Der Taktgeber übersetzt ECHTZEIT (Sekunden auf deiner Uhr) in SPIELMINUTEN (Ticks).
///
/// Beispiel mit den Standardwerten:
///   Rundendauer 15 Minuten = 900 Sekunden für 1440 Spielminuten
///   → 1,6 Spielminuten pro echter Sekunde bei Tempo 1.
///
/// Godot ruft jedes Bild (ca. 60× pro Sekunde) <see cref="Voranschreiten"/> mit der
/// vergangenen Echtzeit auf. Der Taktgeber sammelt die Bruchteile und meldet zurück,
/// wie viele GANZE Ticks die Simulation jetzt ausführen soll.
/// </summary>
public sealed class Taktgeber
{
    /// <summary>Standard aus dem Original „The Guild Gold": 15 Minuten pro Spieljahr.</summary>
    public const double StandardRundenDauerSekunden = 15 * 60;

    public const double MinRundenDauerSekunden = 60;
    public const double MaxRundenDauerSekunden = 2 * 60 * 60;

    /// <summary>Erlaubte Tempostufen (Faktor auf die normale Geschwindigkeit).</summary>
    public static readonly IReadOnlyList<double> Tempostufen = new[] { 1.0, 2.0, 4.0, 8.0 };

    /// <summary>
    /// Schutz gegen Ruckler: Pro Aufruf werden höchstens so viele Ticks ausgegeben.
    /// (Hängt das Spiel kurz, holt die Simulation nicht schlagartig eine ganze Stunde nach.)
    /// </summary>
    public const int MaxTicksProAufruf = 240;

    private double _rundenDauerSekunden = StandardRundenDauerSekunden;
    private double _tempo = 1.0;
    private double _angesammelteTicks;

    /// <summary>Wie lange ein Spieljahr bei Tempo 1 in Echtzeit dauert. Einstellbar als Spieloption.</summary>
    public double RundenDauerSekunden
    {
        get => _rundenDauerSekunden;
        set => _rundenDauerSekunden = Math.Clamp(value, MinRundenDauerSekunden, MaxRundenDauerSekunden);
    }

    /// <summary>Tempo-Faktor: 1 = normal, 2 = doppelt so schnell usw.</summary>
    public double Tempo
    {
        get => _tempo;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Zum Anhalten bitte die Pause verwenden.");
            _tempo = value;
        }
    }

    /// <summary>Ist das Spiel pausiert? Pause ist immer erlaubt – im Einzel- und Mehrspielermodus.</summary>
    public bool IstPausiert { get; set; }

    public void PauseUmschalten() => IstPausiert = !IstPausiert;

    /// <summary>Spielminuten pro echter Sekunde bei den aktuellen Einstellungen.</summary>
    public double TicksProSekunde => Spielzeit.MinutenProTag / RundenDauerSekunden * Tempo;

    /// <summary>
    /// Meldet, wie viele Ticks für die vergangene Echtzeit fällig sind.
    /// </summary>
    /// <param name="echtzeitSekunden">Vergangene Echtzeit seit dem letzten Aufruf (in Godot: „delta").</param>
    public int Voranschreiten(double echtzeitSekunden)
    {
        if (IstPausiert || echtzeitSekunden <= 0)
            return 0;

        _angesammelteTicks += echtzeitSekunden * TicksProSekunde;

        // Kleine Toleranz gegen Rundungsfehler von Kommazahlen (0,1 + 0,2 ist im Computer nicht exakt 0,3).
        var faellig = (int)Math.Floor(_angesammelteTicks + 1e-9);
        if (faellig > MaxTicksProAufruf)
        {
            faellig = MaxTicksProAufruf;
            _angesammelteTicks = 0; // Rest verwerfen statt ewig hinterherzulaufen
        }
        else
        {
            _angesammelteTicks -= faellig;
        }

        return faellig;
    }
}
