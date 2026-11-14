namespace Reichsgilde.Core.Zeit;

/// <summary>
/// Der Taktgeber übersetzt ECHTZEIT (Sekunden auf deiner Uhr) in SPIELMINUTEN (Ticks).
///
/// Beispiel mit den Standardwerten:
///   Rundendauer 15 Minuten = 900 Sekunden für 1140 Spielminuten (04–23 Uhr)
///   → 1,27 Spielminuten pro echter Sekunde bei Tempo 1.
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

    /// <summary>
    /// So viele echte Sekunden dauert eine Spielminute bei Tempo 1 und der Standard-Rundendauer: 900 ÷ 1140 ≈ 0,79. Damit rechnet der Kern ein
    /// Tempo, wie man es im Bild sieht (Meter je Sekunde), in Meter je Spielminute um (Karren: <c>KarrenDaten.MeterJeMinute</c>). Fest mit der
    /// Standard-Rundendauer, nicht mit der eingestellten: Die Simulation darf nicht davon abhängen, wie lang jemand eine Runde einstellt.
    /// </summary>
    public const double SekundenJeSpielminute = StandardRundenDauerSekunden / Spielzeit.MinutenProTag;

    /// <summary>Erlaubte Tempostufen (Faktor auf die normale Geschwindigkeit).</summary>
    public static readonly IReadOnlyList<double> Tempostufen = new[] { 1.0, 2.0, 4.0, 8.0 };

    // TESTTEMPO – nur für die Entwicklung, damit man schneller testen kann.
    // Vor der Veröffentlichung entfernen: diese Zeile löschen und in der Hauptszene
    // „AlleTempostufen" wieder durch „Tempostufen" ersetzen (Suche nach TESTTEMPO).
    public static readonly IReadOnlyList<double> TestTempostufen = new[] { 16.0, 32.0 };

    /// <summary>Normale Stufen und – während der Entwicklung – die Teststufen.</summary>
    public static IReadOnlyList<double> AlleTempostufen => Tempostufen.Concat(TestTempostufen).ToList(); // TESTTEMPO

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

    private readonly List<string> _haltegruende = new();

    /// <summary>
    /// Steht die Zeit? Lesen: die Pause des Spielers (<see cref="Spielerpause"/>) <b>oder</b> ein Haltegrund (<see cref="IstAngehalten"/>).
    /// Schreiben setzt nur die Pause des Spielers – einen Haltegrund hebt weder die Leertaste noch eine Tempotaste auf.
    /// Pause ist immer erlaubt – im Einzel- und Mehrspielermodus.
    /// </summary>
    public bool IstPausiert
    {
        get => Spielerpause || IstAngehalten;
        set => Spielerpause = value;
    }

    /// <summary>Die Pause, die der Spieler (Leertaste, Knopf, Spielmenü, Entscheidungsbrief) gesetzt hat – ohne die Haltegründe.</summary>
    public bool Spielerpause { get; private set; }

    public void PauseUmschalten() => Spielerpause = !Spielerpause;

    /// <summary>
    /// Haltepunkt: Die Zeit steht, solange mindestens ein Grund gesetzt ist (z. B. „Ratssitzung“, „Jahresbericht“ im Einzelspiel), unabhängig von der
    /// Pause des Spielers. Ist der letzte Grund frei, läuft die Zeit so weiter wie vorher (Spielerpause und Tempo bleiben unberührt).
    /// Gehört zur Echtzeit wie die Pause, nicht zur Simulation: kein Befehl, kein Spielstand, ändert keinen Tick.
    /// </summary>
    public void Anhalten(string grund)
    {
        if (!_haltegruende.Contains(grund)) _haltegruende.Add(grund);
    }

    /// <summary>Nimmt einen Haltegrund zurück (ohne ihn: nichts).</summary>
    public void Freigeben(string grund) => _haltegruende.Remove(grund);

    /// <summary>Ist mindestens ein Haltegrund gesetzt?</summary>
    public bool IstAngehalten => _haltegruende.Count > 0;

    /// <summary>Die gesetzten Haltegründe in der Reihenfolge, in der sie kamen.</summary>
    public IReadOnlyList<string> Haltegruende => _haltegruende;

    /// <summary>Spielminuten pro echter Sekunde bei den aktuellen Einstellungen.</summary>
    public double TicksProSekunde => Spielzeit.MinutenProTag / RundenDauerSekunden * Tempo;

    /// <summary>
    /// Wie weit die nächste Spielminute schon angebrochen ist (0 bis knapp unter 1).
    /// Nur für die Darstellung: Sonne, Schatten und Karren rechnen mit „Minute + Bruchteil"
    /// und bewegen sich so jedes Bild ein Stückchen statt einmal pro Minute zu springen.
    /// Die Simulation selbst rechnet weiter nur in ganzen Minuten.
    /// </summary>
    public double AngebrocheneMinute => Math.Clamp(_angesammelteTicks, 0, 0.999);

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
