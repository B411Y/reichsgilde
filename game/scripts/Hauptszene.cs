using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die Hauptszene: hält die Simulation und verbindet sie mit der Stadtansicht (3D)
/// und der Oberfläche (Zeittafel, Kontor, Auswahl).
///
/// Aufgabenteilung:
///   • Die SIMULATION (Reichsgilde.Core) entscheidet, was passiert.
///   • Stadtansicht und Oberfläche ZEIGEN es nur an und leiten Eingaben weiter.
/// Diese Trennung ist die wichtigste Architekturregel des Projekts.
///
/// Startoptionen zum Testen (nach "--" an Godot anhängen):
///   --startstunde=11   Partie beginnt um 11 Uhr statt um 06 Uhr
///   --tempo=8          Anfangstempo
///   --auswahl=Dom      Gebäude vorauswählen (Kennung wie beim Anklicken)
/// </summary>
public partial class Hauptszene : Node
{
    private const int MaxProtokollZeilen = 5;
    private const string WirtschaftsdatenPfad = "res://data/wirtschaft.json";

    private Simulation _simulation = null!;
    private readonly List<string> _protokoll = new();
    private readonly List<Button> _tempoKnoepfe = new();
    private string? _auswahlKennung;

    private Stadtansicht _stadt = null!;
    private KontorAnzeige _kontor = null!;
    private PanelContainer _tafel = null!;
    private PanelContainer _auswahl = null!;
    private Label _auswahlText = null!;
    private Label _datum = null!;
    private Label _uhrzeit = null!;
    private ProgressBar _tagesfortschritt = null!;
    private Button _pauseKnopf = null!;
    private HBoxContainer _tempoleiste = null!;
    private Label _hinweis = null!;
    private Label _protokollLabel = null!;

    public override void _Ready()
    {
        _stadt = GetNode<Stadtansicht>("%Stadt");
        _kontor = GetNode<KontorAnzeige>("%Kontor");
        _tafel = GetNode<PanelContainer>("%Tafel");
        _auswahl = GetNode<PanelContainer>("%Auswahl");
        _auswahlText = GetNode<Label>("%AuswahlText");
        _datum = GetNode<Label>("%Datum");
        _uhrzeit = GetNode<Label>("%Uhrzeit");
        _tagesfortschritt = GetNode<ProgressBar>("%Tagesfortschritt");
        _pauseKnopf = GetNode<Button>("%PauseKnopf");
        _tempoleiste = GetNode<HBoxContainer>("%Tempoleiste");
        _hinweis = GetNode<Label>("%Hinweis");
        _protokollLabel = GetNode<Label>("%Protokoll");

        // Tafeln an die Bildschirmecken heften (passt sich jeder Fenstergröße an).
        _tafel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft, Control.LayoutPresetMode.Minsize, 16);
        _kontor.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize, 16);
        _kontor.GrowHorizontal = Control.GrowDirection.Begin; // wächst nach links ins Bild
        _auswahl.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft, Control.LayoutPresetMode.Minsize, 16);
        _auswahl.GrowVertical = Control.GrowDirection.Begin; // wächst nach oben

        var optionen = Startoptionen();
        _simulation = SimulationAufbauen(optionen);

        _pauseKnopf.Pressed += PauseUmschalten;
        foreach (var tempo in Taktgeber.Tempostufen)
        {
            var knopf = new Button
            {
                Text = $"{tempo}×",
                ToggleMode = true,
                FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(46, 0),
            };
            knopf.Pressed += () => TempoSetzen(tempo);
            _tempoleiste.AddChild(knopf);
            _tempoKnoepfe.Add(knopf);
        }
        if (optionen.TryGetValue("tempo", out var t) && double.TryParse(t, System.Globalization.CultureInfo.InvariantCulture, out var tempoWert))
            TempoSetzen(tempoWert);

        _simulation.PhaseBegonnen += phase =>
            Protokollieren($"{_simulation.Zeit.UhrzeitText} – {KalenderTexte.Name(phase)} beginnt.");
        _simulation.JahrBeendet += jahr =>
            Protokollieren($"Jahresabrechnung {jahr} abgeschlossen. Es ist nun {_simulation.Zeit.DatumText}.");
        _simulation.PartieBeendet += () =>
            Protokollieren($"Die Partie ist zu Ende ({_simulation.Einstellungen.SiegbedingungText}).");

        var minuten = _simulation.Takt.RundenDauerSekunden / 60;
        _hinweis.Text = $"1 Spieljahr = {minuten:0} Min. · {_simulation.Einstellungen.SiegbedingungText}\n"
                      + "Leertaste Pause · 1–4 Tempo · Mausrad Zoom\nRechte Maustaste/WASD verschieben · Q/E drehen";

        _stadt.GebaeudeAngeklickt += kennung => { _auswahlKennung = kennung; AuswahlAnzeigen(); };
        if (optionen.TryGetValue("auswahl", out var vorauswahl))
            _auswahlKennung = vorauswahl;

        Protokollieren($"Die Zunftglocke läutet. {_simulation.Zeit.DatumText} beginnt.");
        Anzeigen();
    }

    /// <summary>Liest Startoptionen wie "--startstunde=11" (nach "--" auf der Kommandozeile).</summary>
    private static Dictionary<string, string> Startoptionen()
    {
        var optionen = new Dictionary<string, string>();
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            var teile = arg.TrimStart('-').Split('=', 2);
            if (teile.Length == 2) optionen[teile[0]] = teile[1];
        }
        return optionen;
    }

    /// <summary>
    /// Lädt die Wirtschaftsdaten und stellt die Startaufstellung des Vertical Slice auf:
    /// der Spieler mit einer Gießerei, ein Rivale mit Gießerei und Mine.
    /// </summary>
    private Simulation SimulationAufbauen(Dictionary<string, string> optionen)
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

        Spielzeit? start = null;
        if (optionen.TryGetValue("startstunde", out var s) && int.TryParse(s, out var stunde))
            start = new Spielzeit(((stunde - 6 + 24) % 24) * 60L);

        var simulation = new Simulation(zeit: start, wirtschaftsdaten: daten);
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
        var grube = w.BetriebEroeffnen("Grube Eisenbart", Betriebsart.Mine, rivale);
        w.Einstellen(grube, "Ork", 3);
        grube.Karren[0].Eskorte = "Normal"; // Der Rivale lässt seine Erzfuhren begleiten.

        w.Meldung += Protokollieren;
        _kontor.Verbinden(simulation, spieler, giesserei, rivale);
        _stadt.Verbinden(simulation, new[]
        {
            (giesserei, new Vector3(-20, 0, 14), true),
            (rivalenGiesserei, new Vector3(18, 0, 16), false),
            (grube, new Vector3(68, 0, -12), false),
        });
        return simulation;
    }

    public override void _Process(double delta)
    {
        _simulation.Aktualisieren(delta);
        Anzeigen();
        _stadt.Aktualisieren(delta);
        if (_auswahlKennung is not null) AuswahlAnzeigen();
    }

    /// <summary>Tastatur: Leertaste = Pause, 1–4 = Tempostufen. (Kamera: siehe Stadtansicht)</summary>
    public override void _UnhandledInput(InputEvent eingabe)
    {
        if (eingabe is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            _auswahlKennung = null;
            _auswahl.Hide();
            GetViewport().SetInputAsHandled();
            return;
        }
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
        if (tempo <= 0) return;
        _simulation.Takt.Tempo = tempo;
        _simulation.Takt.IstPausiert = false;
    }

    /// <summary>Überträgt den Zustand der Simulation auf die Zeittafel und das Kontor.</summary>
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

        _kontor.Aktualisieren();
    }

    /// <summary>Zeigt Informationen zum angeklickten Gebäude.</summary>
    private void AuswahlAnzeigen()
    {
        if (_auswahlKennung is null || _simulation.Wirtschaft is not { } w)
            return;

        var betrieb = w.Betriebe.FirstOrDefault(b => Stadtansicht.BetriebsKennung(b) == _auswahlKennung);
        _auswahlText.Text = betrieb is not null ? BetriebsText(betrieb, w) : _auswahlKennung switch
        {
            "Marktplatz" => "Marktplatz\n\n" + string.Join("\n", w.Markt.Staende.Select(s =>
                $"{s.Ware}: {Zahlenformat.Kommazahl(s.Preis)} {w.Daten.Allgemein.Waehrung} (Grundpreis {Zahlenformat.Ganz(s.Grundpreis)})")),
            "Rathaus" => $"Rathaus\n\nHier tagt der Rat der Reichsstadt. Geltendes Fron- und Sklavenstatut: "
                         + $"{w.Statut.Kuerzel()} – {w.Statut.Name()}.\n\nÄmter, Wahlen und Gesetze folgen in einer späteren Phase.",
            "Dom" => "Dom\n\nKatholische und evangelische Gläubige aller vier Kulturvölker. "
                     + "Für die Kirche sind Orks und Oger Heiden.\n\nPredigten, Ablass und Kirchenbann folgen später.",
            "Osttor" => "Osttor\n\nHier endet der Schutz der Mauern. Auf dem Weg ins Umland lauern Banditen aller Völker, "
                        + "wilde Ork-Rotten und Ogerhorden – nachts doppelt so häufig.",
            _ => _auswahlKennung,
        };
        _auswahl.Show();
    }

    private string BetriebsText(Betrieb b, Stadtwirtschaft w)
    {
        var zeilen = new List<string>
        {
            b.Name,
            $"Besitzer: {b.Konto.Inhaber}",
            $"Belegung: {(b.Belegt == 0 ? "niemand" : string.Join(", ", b.Belegung.Select(x => $"{x.Value}× {x.Key}")))} ({b.Belegt}/{b.Plaetze} Plätze)",
        };
        if (b.Art == Betriebsart.Giesserei) zeilen.Add($"Produkt: {b.Produkt}");
        zeilen.Add("Lager: " + (b.Lager.Count == 0 ? "leer" : string.Join(" · ", b.Lager.OrderBy(l => l.Key).Select(l => $"{l.Value} {l.Key}"))));
        if (b.Stillstand is { } still) zeilen.Add($"⚠ {still}");
        foreach (var k in b.Karren)
        {
            var ladung = string.Join(" + ", k.Ladung.Select(l => $"{l.Value} {l.Key}"));
            zeilen.Add($"{k.Name} ({k.Eskorte} Eskorte): {k.ZustandText(_simulation.Zeit)}{(ladung == "" ? "" : $" · {ladung}")}");
        }
        return string.Join("\n", zeilen);
    }

    private void Protokollieren(string meldung)
    {
        _protokoll.Insert(0, meldung);
        if (_protokoll.Count > MaxProtokollZeilen)
            _protokoll.RemoveAt(_protokoll.Count - 1);
        _protokollLabel.Text = string.Join("\n", _protokoll);
    }
}
