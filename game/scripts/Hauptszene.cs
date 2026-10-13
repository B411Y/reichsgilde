using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die erste Szene des Spiels: zeigt die laufende Spielzeit und das Kontor an und erlaubt Pause und Tempo.
///
/// Aufgabenteilung:
///   • Die SIMULATION (Reichsgilde.Core) entscheidet, was passiert.
///   • Diese Szene ZEIGT es nur an und leitet Eingaben weiter.
/// Diese Trennung ist die wichtigste Architekturregel des Projekts.
/// </summary>
public partial class Hauptszene : Control
{
    private const int MaxProtokollZeilen = 5;

    private const string WirtschaftsdatenPfad = "res://data/wirtschaft.json";

    private Simulation _simulation = null!;
    private KontorAnzeige _kontor = null!;
    private readonly List<string> _protokoll = new();
    private readonly List<Button> _tempoKnoepfe = new();

    // Verweise auf die Knoten aus Hauptszene.tscn (das "%" sucht nach eindeutigen Namen).
    private ColorRect _himmel = null!;
    private Label _datum = null!;
    private Label _uhrzeit = null!;
    private ProgressBar _tagesfortschritt = null!;
    private Button _pauseKnopf = null!;
    private HBoxContainer _tempoleiste = null!;
    private Label _hinweis = null!;
    private Label _protokollLabel = null!;

    /// <summary>Wird von Godot einmal aufgerufen, sobald die Szene geladen ist.</summary>
    public override void _Ready()
    {
        _himmel = GetNode<ColorRect>("%Himmel");
        _datum = GetNode<Label>("%Datum");
        _uhrzeit = GetNode<Label>("%Uhrzeit");
        _tagesfortschritt = GetNode<ProgressBar>("%Tagesfortschritt");
        _pauseKnopf = GetNode<Button>("%PauseKnopf");
        _tempoleiste = GetNode<HBoxContainer>("%Tempoleiste");
        _hinweis = GetNode<Label>("%Hinweis");
        _protokollLabel = GetNode<Label>("%Protokoll");
        _kontor = GetNode<KontorAnzeige>("%Kontor");

        _simulation = SimulationAufbauen();

        _pauseKnopf.Pressed += PauseUmschalten;

        // Für jede Tempostufe aus dem Simulationskern einen Knopf erzeugen.
        foreach (var tempo in Taktgeber.Tempostufen)
        {
            var knopf = new Button
            {
                Text = $"{tempo}×",
                ToggleMode = true,
                FocusMode = FocusModeEnum.None,
                CustomMinimumSize = new Vector2(52, 0),
            };
            knopf.Pressed += () => TempoSetzen(tempo);
            _tempoleiste.AddChild(knopf);
            _tempoKnoepfe.Add(knopf);
        }

        // Auf Ereignisse der Simulation reagieren.
        _simulation.PhaseBegonnen += phase =>
            Protokollieren($"{_simulation.Zeit.UhrzeitText} – {KalenderTexte.Name(phase)} beginnt.");
        _simulation.JahrBeendet += jahr =>
            Protokollieren($"Jahresabrechnung {jahr} abgeschlossen. Es ist nun {_simulation.Zeit.DatumText}.");
        _simulation.PartieBeendet += () =>
            Protokollieren($"Die Partie ist zu Ende ({_simulation.Einstellungen.SiegbedingungText}).");

        var minuten = _simulation.Takt.RundenDauerSekunden / 60;
        _hinweis.Text = $"1 Spieljahr = {minuten:0} Min. · {_simulation.Einstellungen.SiegbedingungText}\n"
                      + "Leertaste = Pause · Tasten 1–4 = Tempo";

        Protokollieren($"Die Zunftglocke läutet. {_simulation.Zeit.DatumText} beginnt.");
        Anzeigen();
    }

    /// <summary>
    /// Lädt die Wirtschaftsdaten und stellt die Startaufstellung des Vertical Slice auf:
    /// der Spieler mit einer Gießerei, ein Rivale mit Gießerei und Mine.
    /// Ohne gültige Datendatei läuft nur die Zeit (mit Fehlermeldung im Protokoll).
    /// </summary>
    private Simulation SimulationAufbauen()
    {
        Wirtschaftsdaten? daten = null;
        try
        {
            // Godot.FileAccess liest auch aus dem exportierten Spiel (res:// = Projektordner).
            var json = Godot.FileAccess.GetFileAsString(WirtschaftsdatenPfad);
            if (string.IsNullOrEmpty(json))
                Protokollieren($"Fehler: {WirtschaftsdatenPfad} wurde nicht gefunden.");
            else
                daten = Wirtschaftsdaten.AusJson(json);
        }
        catch (WirtschaftsdatenFehler fehler)
        {
            Protokollieren(fehler.Message);
            GD.PushError(fehler.Message);
        }

        var simulation = new Simulation(wirtschaftsdaten: daten);
        if (simulation.Wirtschaft is not { } w)
        {
            _kontor.Hide();
            return simulation;
        }

        var spieler = w.KontoEroeffnen("Familie Hammerschmid");
        var giesserei = w.BetriebEroeffnen("Gießerei am Weinmarkt", Betriebsart.Giesserei, spieler);
        giesserei.Produkt = "Silberring";
        w.Einstellen(giesserei, "Geselle", 2);

        var rivale = w.KontoEroeffnen("Haus Eisenbart");
        var rivalenGiesserei = w.BetriebEroeffnen("Gießerei Eisenbart", Betriebsart.Giesserei, rivale);
        rivalenGiesserei.Produkt = "Rapier";
        w.Einstellen(rivalenGiesserei, "Geselle", 2);
        w.Einstellen(w.BetriebEroeffnen("Grube Eisenbart", Betriebsart.Mine, rivale), "Ork", 3);

        w.Meldung += Protokollieren;
        _kontor.Verbinden(simulation, spieler, giesserei, rivale);
        return simulation;
    }

    /// <summary>Wird von Godot jedes Bild aufgerufen. "delta" = Sekunden seit dem letzten Bild.</summary>
    public override void _Process(double delta)
    {
        _simulation.Aktualisieren(delta);
        Anzeigen();
    }

    /// <summary>Tastatur: Leertaste = Pause, 1–4 = Tempostufen.</summary>
    public override void _UnhandledInput(InputEvent eingabe)
    {
        if (eingabe is not InputEventKey { Pressed: true, Echo: false } taste)
            return;

        switch (taste.Keycode)
        {
            case Key.Space:
                PauseUmschalten();
                break;
            case >= Key.Key1 and <= Key.Key9:
                var stufe = (int)(taste.Keycode - Key.Key1);
                if (stufe < Taktgeber.Tempostufen.Count)
                    TempoSetzen(Taktgeber.Tempostufen[stufe]);
                break;
            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    private void PauseUmschalten()
    {
        _simulation.Takt.PauseUmschalten();
        Protokollieren(_simulation.Takt.IstPausiert ? "Pause." : "Weiter geht's.");
    }

    private void TempoSetzen(double tempo)
    {
        _simulation.Takt.Tempo = tempo;
        _simulation.Takt.IstPausiert = false;
    }

    /// <summary>Überträgt den Zustand der Simulation auf die Anzeige.</summary>
    private void Anzeigen()
    {
        var zeit = _simulation.Zeit;
        var takt = _simulation.Takt;

        _datum.Text = $"{zeit.DatumText}  ·  Runde {zeit.Runde}";
        _uhrzeit.Text = $"{zeit.UhrzeitText}  ·  {KalenderTexte.Name(zeit.Tagesphase)}";
        _tagesfortschritt.Value = zeit.MinuteImTag;
        _pauseKnopf.Text = _simulation.IstBeendet ? "Partie beendet"
                         : takt.IstPausiert ? "▶ Weiter" : "❚❚ Pause";
        _pauseKnopf.Disabled = _simulation.IstBeendet;

        for (var i = 0; i < _tempoKnoepfe.Count; i++)
            _tempoKnoepfe[i].SetPressedNoSignal(!takt.IstPausiert && Taktgeber.Tempostufen[i] == takt.Tempo);

        _himmel.Color = Himmelsfarbe(zeit.UhrzeitInMinuten);
        _kontor.Aktualisieren();
    }

    private void Protokollieren(string meldung)
    {
        _protokoll.Insert(0, meldung);
        if (_protokoll.Count > MaxProtokollZeilen)
            _protokoll.RemoveAt(_protokoll.Count - 1);
        _protokollLabel.Text = string.Join("\n", _protokoll);
    }

    // Stützpunkte für den Tag-Nacht-Farbverlauf: (Uhrzeit in Minuten, Farbe).
    private static readonly (int Minute, Color Farbe)[] HimmelsVerlauf =
    {
        (0 * 60,  new Color(0.04f, 0.05f, 0.10f)), // Mitternacht
        (5 * 60,  new Color(0.10f, 0.10f, 0.18f)), // vor dem Morgengrauen
        (6 * 60,  new Color(0.55f, 0.42f, 0.35f)), // Morgengrauen – Zunftglocke
        (9 * 60,  new Color(0.47f, 0.60f, 0.72f)), // Vormittag
        (14 * 60, new Color(0.50f, 0.64f, 0.78f)), // Nachmittag
        (18 * 60, new Color(0.62f, 0.40f, 0.26f)), // Abendrot – Vesper
        (21 * 60, new Color(0.14f, 0.12f, 0.20f)), // Zwielicht
        (24 * 60, new Color(0.04f, 0.05f, 0.10f)), // wieder Mitternacht
    };

    /// <summary>Berechnet die Himmelsfarbe, indem zwischen zwei Stützpunkten überblendet wird.</summary>
    private static Color Himmelsfarbe(int uhrzeitInMinuten)
    {
        for (var i = 0; i < HimmelsVerlauf.Length - 1; i++)
        {
            var (von, farbeVon) = HimmelsVerlauf[i];
            var (bis, farbeBis) = HimmelsVerlauf[i + 1];
            if (uhrzeitInMinuten < bis)
                return farbeVon.Lerp(farbeBis, (float)(uhrzeitInMinuten - von) / (bis - von));
        }
        return HimmelsVerlauf[0].Farbe;
    }
}
