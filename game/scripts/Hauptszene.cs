using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
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
///   --drehung=1        Kamera um 1–3 Vierteldrehungen gedreht starten (0 = Blick nach Nordosten)
///   --innen=1          direkt in der Innenansicht der eigenen Gießerei starten
///   --verwalten=1      das Bedienfeld der eigenen Gießerei gleich öffnen
///   --vermoegen=12000  Startvermögen des Spielers setzen (zum Ausprobieren des Aufstiegs)
///   --bericht=1        gleich zu Beginn einen Jahresbericht zeigen (zum Testen der Anzeige)
///   --rat=1            gleich zu Beginn die Ratskammer öffnen
///   --familie=1        gleich zu Beginn die Familientafel öffnen
///   --gericht=1        gleich zu Beginn den Gerichtssaal öffnen
///   --hinterzimmer=1   gleich zu Beginn das Hinterzimmer öffnen
///   --laden=datei.json einen Spielstand aus dem Spielstand-Ordner laden
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
    private Button _innenKnopf = null!;
    private BetriebsVerwaltung _verwaltung = null!;
    /// <summary>Ein Knopf für die Hauptaktion des ausgewählten Ortes (Aufsteigen, Kaufen, Pachten).</summary>
    private Button _aktionKnopf = null!;
    private Func<Befehl>? _aktion;
    private JahresberichtAnzeige _jahresbericht = null!;
    private RatAnzeige _ratskammer = null!;
    private FamilienAnzeige _familientafel = null!;
    private GerichtAnzeige _gerichtssaal = null!;
    private GaunerAnzeige _hinterzimmer = null!;
    private Button _gerichtKnopf = null!;
    private Button _ratKnopf = null!;
    private Konto? _spieler;

    /// <summary>
    /// Nach jeder Jahresabrechnung hält das Spiel an, bis der Bericht geschlossen wird (wie im Original).
    /// Im Multiplayer wird das später eine Einstellung der Partie.
    /// </summary>
    private const bool BeiJahresberichtPausieren = true;
    private Label _blick = null!;
    private Betrieb? _eigeneGiesserei;
    private Label _datum = null!;
    private Label _uhrzeit = null!;
    private ProgressBar _tagesfortschritt = null!;
    private Button _pauseKnopf = null!;
    private HBoxContainer _tempoleiste = null!;
    private Label _hinweis = null!;
    private Label _protokollLabel = null!;

    // Spielmenü (Esc): Weiter, Speichern, Laden, Hauptmenü, Beenden
    private PanelContainer _spielmenue = null!;
    private VBoxContainer _menueKnoepfe = null!;
    private SpielstandListe _menueListe = null!;
    private Label _menueMeldung = null!;
    private bool _warPausiertVorMenue;

    public override void _Ready()
    {
        _stadt = GetNode<Stadtansicht>("%Stadt");
        _kontor = GetNode<KontorAnzeige>("%Kontor");
        _tafel = GetNode<PanelContainer>("%Tafel");
        _auswahl = GetNode<PanelContainer>("%Auswahl");
        _auswahlText = GetNode<Label>("%AuswahlText");
        _innenKnopf = GetNode<Button>("%InnenKnopf");
        _verwaltung = GetNode<BetriebsVerwaltung>("%Verwaltung");
        _aktionKnopf = GetNode<Button>("%AktionKnopf");
        _jahresbericht = GetNode<JahresberichtAnzeige>("%Jahresbericht");
        _ratKnopf = GetNode<Button>("%RatKnopf");
        _ratskammer = new RatAnzeige();
        var ratMitte = Hauptmenue.Mitte();
        GetNode<CanvasLayer>("Oberflaeche").AddChild(ratMitte);
        ratMitte.AddChild(_ratskammer);
        _familientafel = new FamilienAnzeige();
        ratMitte.AddChild(_familientafel); // gleiche Mitte: es ist immer nur eine Tafel offen
        _gerichtssaal = new GerichtAnzeige();
        ratMitte.AddChild(_gerichtssaal);
        _hinterzimmer = new GaunerAnzeige();
        ratMitte.AddChild(_hinterzimmer);
        _gerichtKnopf = GetNode<Button>("%GerichtKnopf");
        // Der Bericht sitzt in einem CenterContainer: Der hält ihn mittig, egal wie hoch er wird.
        var berichtMitte = Hauptmenue.Mitte();
        GetNode<CanvasLayer>("Oberflaeche").AddChild(berichtMitte);
        _jahresbericht.Reparent(berichtMitte);
        _blick = GetNode<Label>("%Blick");
        _datum = GetNode<Label>("%Datum");
        _uhrzeit = GetNode<Label>("%Uhrzeit");
        _tagesfortschritt = GetNode<ProgressBar>("%Tagesfortschritt");
        _pauseKnopf = GetNode<Button>("%PauseKnopf");
        _tempoleiste = GetNode<HBoxContainer>("%Tempoleiste");
        _hinweis = GetNode<Label>("%Hinweis");
        _protokollLabel = GetNode<Label>("%Protokoll");

        // Tafeln an die Bildschirmecken heften (passt sich jeder Fenstergröße an).
        // Deckende Tafeln: Beschriftungen der Stadt sollen nicht durch die Oberfläche scheinen.
        foreach (var tafel in new[] { _tafel, _kontor, _auswahl })
            tafel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.16f, 0.15f, 0.13f, 0.97f),
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            });
        _tafel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft, Control.LayoutPresetMode.Minsize, 16);
        _kontor.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize, 16);
        _kontor.GrowHorizontal = Control.GrowDirection.Begin; // wächst nach links ins Bild
        _auswahl.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft, Control.LayoutPresetMode.Minsize, 16);
        _auswahl.GrowVertical = Control.GrowDirection.Begin; // wächst nach oben
        // Passt eine Tafel nicht ins Fenster, wird sie gerollt (Fenstergröße ist frei wählbar).
        foreach (var tafel in new[] { _tafel, _kontor, _auswahl })
            Rollbereich.Einhuellen(tafel);

        var optionen = Startoptionen();
        _simulation = SimulationAufbauen(optionen);
        SpielmenueBauen();
        _ratKnopf.Pressed += () => { TafelnSchliessen(); _ratskammer.Oeffnen(); };
        _ratskammer.Gemeldet += Protokollieren;
        _kontor.FamilieGewuenscht += () => { TafelnSchliessen(); _familientafel.Oeffnen(); };
        _familientafel.Gemeldet += Protokollieren;
        _gerichtKnopf.Pressed += () => { TafelnSchliessen(); _gerichtssaal.Oeffnen(); };
        _gerichtssaal.Gemeldet += Protokollieren;
        _kontor.HinterzimmerGewuenscht += () => { TafelnSchliessen(); _hinterzimmer.Oeffnen(); };
        _hinterzimmer.Gemeldet += Protokollieren;

        _pauseKnopf.Pressed += PauseUmschalten;
        foreach (var tempo in Taktgeber.AlleTempostufen /* TESTTEMPO */)
        {
            var knopf = new Button
            {
                Text = $"{tempo}×",
                ToggleMode = true,
                FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(46, 0),
            };
            knopf.Pressed += () => TempoSetzen(tempo);
            if (!Taktgeber.Tempostufen.Contains(tempo)) // TESTTEMPO: Teststufen orange und mit Hinweis
            {
                knopf.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.25f));
                knopf.TooltipText = "Nur zum Testen – wird vor der Veröffentlichung entfernt";
            }
            _tempoleiste.AddChild(knopf);
            _tempoKnoepfe.Add(knopf);
        }
        if (optionen.TryGetValue("tempo", out var t) && double.TryParse(t, System.Globalization.CultureInfo.InvariantCulture, out var tempoWert))
            TempoSetzen(tempoWert);

        _simulation.PhaseBegonnen += phase =>
            Protokollieren($"{_simulation.Zeit.UhrzeitText} – {KalenderTexte.Name(phase)} beginnt.");
        _simulation.JahrBeendet += jahr =>
        {
            Protokollieren($"Jahresabrechnung {jahr} abgeschlossen. Es ist nun {_simulation.Zeit.DatumText}.");
            if (_ratskammer.Visible) _ratskammer.Aufbauen(); // neue Ämter, verblasste Gunst
            if (_familientafel.Visible) _familientafel.Aufbauen(); // ein Jahr älter, Geburten, Todesfälle
            if (_gerichtssaal.Visible) _gerichtssaal.Aufbauen(); // Urteile und Spitzelberichte
            if (_hinterzimmer.Visible) _hinterzimmer.Aufbauen();
            if (_spieler is null || _simulation.IstBeendet) return;
            // Automatisch speichern – überschreibt jedes Jahr denselben Spielstand.
            var (pfad, meldung) = Spielstaende.Speichern(_simulation, _spieler, Spielstaende.AutoDatei);
            if (pfad is null) Protokollieren(meldung);
            _jahresbericht.Zeigen(_simulation, _spieler);
            if (BeiJahresberichtPausieren) _simulation.Takt.IstPausiert = true;
        };
        _simulation.PartieBeendet += () =>
        {
            Protokollieren($"Die Partie ist zu Ende: {_simulation.Endgrund}");
            if (_spieler is not null) _jahresbericht.Zeigen(_simulation, _spieler, _simulation.Endgrund);
        };
        _jahresbericht.Geschlossen += () =>
        {
            if (_simulation.IstBeendet) SpielmenueOeffnen(); // Partie vorbei: Laden, Hauptmenü oder Beenden
            else _simulation.Takt.IstPausiert = false;
        };
        _aktionKnopf.Pressed += () =>
        {
            if (_simulation.Wirtschaft is not { } wirtschaft || _spieler is null || _aktion is null) return;
            var ergebnis = wirtschaft.Ausfuehren(_spieler, _aktion());
            if (!ergebnis.Erfolg) Protokollieren("✗ " + ergebnis.Meldung); // Erfolge meldet die Stadt selbst
            AuswahlAnzeigen();
        };

        var minuten = _simulation.Takt.RundenDauerSekunden / 60;
        _hinweis.Text = $"1 Spieljahr = {minuten:0} Min. · {_simulation.Einstellungen.SiegbedingungText}\n"
                      + "Leertaste Pause · 1–4 Tempo (5–6 Test) · Mausrad Zoom\nRechte Maustaste/WASD verschieben · Q/E drehen\n"
                      + "Doppelklick auf eine Gießerei = Innenansicht · Esc zurück bzw. Spielmenü";

        _stadt.GebaeudeAngeklickt += kennung => { _auswahlKennung = kennung; AuswahlAnzeigen(); };
        _stadt.InnenansichtGewechselt += betrieb =>
        {
            if (betrieb is not null) _auswahlKennung = Stadtansicht.BetriebsKennung(betrieb);
            Protokollieren(betrieb is null ? "Zurück in den Gassen der Stadt." : $"Ihr betretet die {betrieb.Name}.");
        };
        _innenKnopf.Pressed += InnenKnopfGedrueckt;
        _verwaltung.Gemeldet += Protokollieren;
        _kontor.Gemeldet += Protokollieren;
        _kontor.VerwaltenGewuenscht += betrieb => { _auswahlKennung = Stadtansicht.BetriebsKennung(betrieb); AuswahlAnzeigen(); };
        if (optionen.TryGetValue("auswahl", out var vorauswahl))
            _auswahlKennung = vorauswahl;
        if (optionen.TryGetValue("drehung", out var d) && int.TryParse(d, out var drehung))
            _stadt.Drehen(drehung, sofort: true);
        if (optionen.TryGetValue("verwalten", out var verwalten) && verwalten == "1" && _eigeneGiesserei is not null)
            _auswahlKennung = Stadtansicht.BetriebsKennung(_eigeneGiesserei);
        if (optionen.TryGetValue("rat", out var rat) && rat == "1")
            _ratskammer.Oeffnen();
        if (optionen.TryGetValue("familie", out var familie) && familie == "1")
            _familientafel.Oeffnen();
        if (optionen.TryGetValue("gericht", out var gericht) && gericht == "1")
            _gerichtssaal.Oeffnen();
        if (optionen.TryGetValue("hinterzimmer", out var hinterzimmer) && hinterzimmer == "1")
            _hinterzimmer.Oeffnen();
        if (optionen.TryGetValue("bericht", out var bericht) && bericht == "1" && _spieler is not null)
            _jahresbericht.Zeigen(_simulation, _spieler);
        if (optionen.TryGetValue("innen", out var innen) && innen == "1" && _eigeneGiesserei is not null)
            _stadt.InnenansichtBetreten(_eigeneGiesserei);

        if (_simulation.Zeit.VergangeneMinuten == 0) // nur bei einer neuen Partie, nicht nach dem Laden
            Protokollieren($"Die Zunftglocke läutet. {_simulation.Zeit.DatumText} beginnt.");
        Anzeigen();
    }

    /// <summary>Es ist immer nur eine Tafel in der Bildmitte offen.</summary>
    private void TafelnSchliessen()
    {
        _ratskammer.Hide();
        _familientafel.Hide();
        _gerichtssaal.Hide();
        _hinterzimmer.Hide();
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
    /// der Spieler mit einer Gießerei, ein KI-Rivale mit Gießerei und Mine.
    /// </summary>
    private Simulation SimulationAufbauen(Dictionary<string, string> optionen)
    {
        // 1. Kommt die Szene aus dem Hauptmenü? Dann liegt dort ein Auftrag: laden oder neue Partie.
        var (einstellungen, neu, ladePfad) = Partiewahl.Abholen();
        if (optionen.TryGetValue("laden", out var datei)) // Startoption für Tests: Spielstand direkt laden
            ladePfad = $"{Spielstaende.Ordner}/{datei}";
        if (ladePfad is not null)
        {
            try
            {
                var (geladen, spielerKonto) = Spielstaende.Laden(ladePfad);
                if (spielerKonto is not null && geladen.Wirtschaft is not null)
                {
                    SimulationVerbinden(geladen, spielerKonto);
                    Protokollieren($"Spielstand geladen: {geladen.Zeit.DatumText}, {geladen.Zeit.UhrzeitText}. Die Zeit steht – Leertaste zum Weiterspielen.");
                    return geladen;
                }
                Protokollieren("Der Spielstand enthält keinen Spieler.");
            }
            catch (SpielstandFehler fehler)
            {
                Protokollieren(fehler.Message);
                GD.PushError(fehler.Message);
            }
        }

        // 2. Neue Partie – mit den Einstellungen aus dem Menü oder (Startoptionen/Test) den Standardwerten.
        var daten = WirtschaftsdatenLaden();
        if (daten is null)
        {
            _kontor.Hide();
            return new Simulation(); // ohne Daten läuft nur die Uhr
        }

        Spielzeit? start = null;
        if (optionen.TryGetValue("startstunde", out var s) && int.TryParse(s, out var stunde))
            start = new Spielzeit(((stunde - 6 + 24) % 24) * 60L);
        double? startvermoegen = optionen.TryGetValue("vermoegen", out var v) && double.TryParse(v, out var betrag) ? betrag : null;

        var (simulation, spieler) = Partieaufbau.Erstellen(einstellungen ?? Partieeinstellungen.Standard, daten,
            neu ?? new NeuePartie(Startvermoegen: startvermoegen), start);
        SimulationVerbinden(simulation, spieler);
        return simulation;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Spielmenü (Esc)
    // ═════════════════════════════════════════════════════════════════════════

    private void SpielmenueBauen()
    {
        var (tafel, spalte) = Hauptmenue.Tafel(420);
        _spielmenue = tafel;
        var mitte = Hauptmenue.Mitte();
        GetNode<CanvasLayer>("Oberflaeche").AddChild(mitte);
        mitte.AddChild(tafel);
        Hauptmenue.Text(spalte, "Spielmenü", 26, new Color(0.95f, 0.82f, 0.45f), zentriert: true);

        _menueKnoepfe = new VBoxContainer();
        _menueKnoepfe.AddThemeConstantOverride("separation", 10);
        spalte.AddChild(_menueKnoepfe);
        Hauptmenue.Knopf(_menueKnoepfe, "Weiterspielen", SpielmenueSchliessen);
        Hauptmenue.Knopf(_menueKnoepfe, "Speichern", () =>
        {
            var (_, meldung) = Spielstaende.Speichern(_simulation, _spieler);
            _menueMeldung.Text = meldung;
            Protokollieren(meldung);
        });
        Hauptmenue.Knopf(_menueKnoepfe, "Laden", () =>
        {
            _menueListe.Auffrischen();
            _menueListe.Visible = !_menueListe.Visible;
        });
        Hauptmenue.Knopf(_menueKnoepfe, "Vollbild / Fenster (F11)", Fenster.Umschalten);
        Hauptmenue.Knopf(_menueKnoepfe, "Zum Hauptmenü", () => GetTree().ChangeSceneToFile("res://scenes/Hauptmenue.tscn"));
        Hauptmenue.Knopf(_menueKnoepfe, "Spiel beenden", () => GetTree().Quit());

        _menueListe = new SpielstandListe { Visible = false };
        spalte.AddChild(_menueListe);
        _menueListe.Gewaehlt += pfad =>
        {
            try
            {
                Spielstaende.Laden(pfad); // vorab prüfen
                Partiewahl.LadenWaehlen(pfad);
                GetTree().ChangeSceneToFile("res://scenes/Hauptszene.tscn"); // Szene neu aufbauen
            }
            catch (SpielstandFehler fehler)
            {
                _menueMeldung.Text = fehler.Message;
            }
        };
        _menueMeldung = Hauptmenue.Text(spalte, "Ungespeicherter Fortschritt geht beim Laden oder im Hauptmenü verloren.", 12, new Color(0.7f, 0.7f, 0.7f), umbruchBreite: 360);
        tafel.Hide();
    }

    private void SpielmenueOeffnen()
    {
        _warPausiertVorMenue = _simulation.Takt.IstPausiert;
        _simulation.Takt.IstPausiert = true;
        _menueListe.Visible = false;
        _spielmenue.Show();
    }

    private void SpielmenueSchliessen()
    {
        _spielmenue.Hide();
        if (!_simulation.IstBeendet) _simulation.Takt.IstPausiert = _warPausiertVorMenue;
    }

    /// <summary>Liest game/data/wirtschaft.json. Bei Fehlern: Meldung ins Protokoll und null.</summary>
    private Wirtschaftsdaten? WirtschaftsdatenLaden()
    {
        try
        {
            // Godot.FileAccess liest auch aus dem exportierten Spiel (res:// = Projektordner).
            var json = Godot.FileAccess.GetFileAsString(WirtschaftsdatenPfad);
            if (!string.IsNullOrEmpty(json))
                return Wirtschaftsdaten.AusJson(json);
            Protokollieren($"Fehler: {WirtschaftsdatenPfad} wurde nicht gefunden.");
        }
        catch (WirtschaftsdatenFehler fehler)
        {
            Protokollieren(fehler.Message);
            GD.PushError(fehler.Message);
        }
        return null;
    }

    /// <summary>
    /// Verbindet eine (neue oder geladene) Partie mit Stadtansicht, Kontor und Bedienfeld
    /// und abonniert ihre Ereignisse.
    /// </summary>
    private void SimulationVerbinden(Simulation simulation, Konto spieler)
    {
        var w = simulation.Wirtschaft!;
        _spieler = spieler;
        _eigeneGiesserei = w.Betriebe.FirstOrDefault(b => b.Konto == spieler && b.IstWerkstatt); // erste eigene Werkstatt

        // Bankrott des Spielers beendet die Partie; ein bankrotter Rivale scheidet nur aus.
        w.Bankrott += konto =>
        {
            if (konto == spieler)
                simulation.Beenden($"{spieler.Inhaber} konnte die Schulden nicht begleichen.");
        };
        // Stirbt das Haus des Spielers aus, ist die Partie vorbei (später: Zuschauer-Modus).
        w.Familien.Erloschen += konto =>
        {
            if (konto == spieler)
                simulation.Beenden($"Das Haus {w.Familien.Von(spieler)?.Nachname ?? spieler.Inhaber} ist ohne Erben erloschen.");
        };
        w.Meldung += Protokollieren;
        // Die ersten Züge der KI fielen schon beim Aufstellen – ins Protokoll nachtragen (älteste zuerst).
        foreach (var rivale in w.Rivalen)
            foreach (var zug in rivale.Entscheidungen.Reverse())
                Protokollieren(zug);

        var rivalenKonto = w.Konten.FirstOrDefault(k => k != spieler);
        _kontor.Verbinden(simulation, spieler, rivalenKonto);
        _ratskammer.Verbinden(simulation, spieler);
        _familientafel.Verbinden(simulation, spieler);
        _gerichtssaal.Verbinden(simulation, spieler);
        _hinterzimmer.Verbinden(simulation, spieler);
        // Um 21 Uhr schlagen die Banden zu – ein offenes Hinterzimmer zeigt sofort, was geschah.
        w.Gauner.NachtVorbei += () => { if (_hinterzimmer.Visible) _hinterzimmer.Aufbauen(); };
        _verwaltung.Verbinden(simulation, spieler);
        // Die Stadtansicht stellt jeden Betrieb an seinen Platz im Stadtplan und zeigt die freien Plätze.
        _stadt.Verbinden(simulation, spieler);
        w.BetriebErworben += betrieb =>
        {
            _stadt.BetriebHinzufuegen(betrieb);
            // Hat der Spieler gerade den ausgewählten Platz erworben, wechselt die Auswahl auf den neuen Betrieb.
            if (_auswahlKennung == Stadtansicht.LiegenschaftKennung(betrieb.Name))
                _auswahlKennung = Stadtansicht.BetriebsKennung(betrieb);
        };
    }

    public override void _Process(double delta)
    {
        _simulation.Aktualisieren(delta);
        Anzeigen();
        _stadt.Aktualisieren(delta);
        if (_auswahlKennung is not null) AuswahlAnzeigen();
    }

    /// <summary>Tastatur: Leertaste = Pause, 1–4 = Tempostufen, 5–6 = Testtempo 16×/32×. (Kamera: siehe Stadtansicht)</summary>
    public override void _UnhandledInput(InputEvent eingabe)
    {
        if (eingabe is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            // Esc geht einen Schritt zurück: Innenansicht verlassen → Auswahl schließen → Spielmenü auf/zu.
            if (_jahresbericht.Visible) { /* der Bericht wird mit „Weiter" geschlossen */ }
            else if (_ratskammer.Visible)
                _ratskammer.Hide();
            else if (_familientafel.Visible)
                _familientafel.Hide();
            else if (_gerichtssaal.Visible)
                _gerichtssaal.Hide();
            else if (_hinterzimmer.Visible)
                _hinterzimmer.Hide();
            else if (_spielmenue.Visible)
                SpielmenueSchliessen();
            else if (_stadt.InnenBetrieb is not null)
                _stadt.InnenansichtVerlassen();
            else if (_auswahl.Visible)
            {
                _auswahlKennung = null;
                _auswahl.Hide();
                _verwaltung.Zeigen(null);
            }
            else
                SpielmenueOeffnen();
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
                if (stufe < Taktgeber.AlleTempostufen /* TESTTEMPO */.Count)
                    TempoSetzen(Taktgeber.AlleTempostufen /* TESTTEMPO */[stufe]);
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
            _tempoKnoepfe[i].SetPressedNoSignal(!takt.IstPausiert && Taktgeber.AlleTempostufen /* TESTTEMPO */[i] == takt.Tempo);

        _kontor.Aktualisieren();
        _verwaltung.Aktualisieren();
        PlatzMachen();
        _blick.Text = _stadt.InnenBetrieb is { } innen
            ? $"Blick nach {_stadt.Blickrichtung} · Innenansicht {innen.Name}"
            : $"Blick nach {_stadt.Blickrichtung}";
    }

    private void InnenKnopfGedrueckt()
    {
        if (_stadt.InnenBetrieb is not null)
            _stadt.InnenansichtVerlassen();
        else if (AusgewaehlterBetrieb() is { } betrieb)
            _stadt.InnenansichtBetreten(betrieb);
        AuswahlAnzeigen();
    }

    private Betrieb? AusgewaehlterBetrieb() =>
        _simulation.Wirtschaft?.Betriebe.FirstOrDefault(b => Stadtansicht.BetriebsKennung(b) == _auswahlKennung);

    /// <summary>Zeigt Informationen zum angeklickten Gebäude.</summary>
    private void AuswahlAnzeigen()
    {
        if (_auswahlKennung is null || _simulation.Wirtschaft is not { } w)
            return;

        var betrieb = AusgewaehlterBetrieb();
        _verwaltung.Zeigen(betrieb); // nur bei eigenen Betrieben sichtbar
        _innenKnopf.Visible = betrieb is not null && _stadt.HatInnenansicht(betrieb);
        _innenKnopf.Text = _stadt.InnenBetrieb == betrieb && betrieb is not null ? "Zurück zur Stadt (Esc)" : "Innenansicht betreten";
        var frei = _auswahlKennung.StartsWith("liegenschaft:")
            ? w.FreieLiegenschaften.FirstOrDefault(l => Stadtansicht.LiegenschaftKennung(l.Name) == _auswahlKennung)
            : null;
        _auswahlText.Text = betrieb is not null ? BetriebsText(betrieb, w)
            : frei is not null ? LiegenschaftText(frei, w)
            : _auswahlKennung switch
        {
            "Marktplatz" => "Marktplatz\n\n" + string.Join("\n", w.Markt.Staende.Select(s =>
                $"{s.Ware}: {Zahlenformat.Kommazahl(s.Preis)} {w.Daten.Allgemein.Waehrung} (Grundpreis {Zahlenformat.Ganz(s.Grundpreis)})")),
            "Rathaus" => RathausText(w),
            "Dom" => "Dom\n\nKatholische und evangelische Gläubige aller vier Kulturvölker. "
                     + "Für die Kirche sind Orks und Oger Heiden.\n\nPredigten, Ablass und Kirchenbann folgen später.",
            "Osttor" => "Osttor\n\nHier endet der Schutz der Mauern. Auf dem Weg ins Umland lauern Banditen aller Völker, "
                        + "wilde Ork-Rotten und Ogerhorden – nachts doppelt so häufig.",
            _ => _auswahlKennung,
        };
        AktionKnopfAnzeigen(w, frei);
        var ratSichtbar = _auswahlKennung == "Rathaus" && w.Rat.IstAktiv;
        if (_ratKnopf.Visible != ratSichtbar) _ratKnopf.Visible = ratSichtbar; // nur bei Änderung setzen (siehe AktionKnopfAnzeigen)
        var gerichtSichtbar = _auswahlKennung == "Rathaus" && w.Justiz.IstAktiv;
        if (_gerichtKnopf.Visible != gerichtSichtbar) _gerichtKnopf.Visible = gerichtSichtbar;
        _auswahl.Show();
    }

    /// <summary>Ein freier Platz: was er kostet, ab welchem Stand und was er voraussichtlich einbringt.</summary>
    private string LiegenschaftText(LiegenschaftDaten l, Stadtwirtschaft w)
    {
        var waehrung = w.Daten.Allgemein.Waehrung;
        var d = w.Daten.Art(l.Art);
        var art = $"{d.Name} ({string.Join(", ", d.IstWerkstatt ? d.Produkte : d.Rohstoffe)})";
        var zeilen = new List<string>
        {
            $"{l.Name} – {(l.IstPacht ? "zu verpachten" : "zu verkaufen")}",
            "",
            $"{art} mit {w.Daten.Plaetze(l.Art)} Arbeitsplätzen",
            l.IstPacht
                ? $"Pacht: {Zahlenformat.Ganz(w.Daten.Fixkosten(l.Art))} {waehrung} im Jahr"
                : $"Kaufpreis: {Zahlenformat.Ganz(l.Kaufpreis)} {waehrung}, Unterhalt {Zahlenformat.Ganz(w.Daten.Fixkosten(l.Art))} im Jahr",
            $"Zu haben ab dem Stand: {l.MindestStand}",
            _spieler is { Berufe.Count: > 0 } sp && w.Daten.Berufe.Count > 0 && !sp.Beherrscht(d.Beruf)
                ? $"Handwerk: {d.Beruf} – beherrscht ihr nicht; erst im Kontor erlernen"
                : $"Handwerk: {d.Beruf}",
            $"Erwarteter Gewinn bei voller Belegung: ≈ {Zahlenformat.MitVorzeichen(Kalkulation.ErwarteterJahresgewinn(w, l.Art))} {waehrung} im Jahr (zu heutigen Preisen)",
        };
        return string.Join("\n", zeilen);
    }

    /// <summary>Im Rathaus: Ständeordnung, Rangliste und das geltende Statut.</summary>
    private string RathausText(Stadtwirtschaft w)
    {
        var waehrung = w.Daten.Allgemein.Waehrung;
        var zeilen = new List<string> { "Rathaus", "" };
        if (_spieler is not null)
        {
            zeilen.Add($"Euer Stand: {w.StandVon(_spieler).Name}" + (w.Rat.AmtVon(_spieler) is { } amt ? $" · Amt: {amt.Name}" : ""));
            if (w.NaechsterStand(_spieler) is { } neu)
                zeilen.Add($"Nächster Stand: {neu.Name}\n  ab {Zahlenformat.Ganz(neu.Schwelle)} {waehrung} Vermögen, Gebühr {Zahlenformat.Ganz(neu.Gebuehr)}\n  {neu.Privilegien}");
        }
        zeilen.Add($"Geltendes Fron- und Sklavenstatut: {w.Statut.Kuerzel()} – {w.Statut.Name()}");
        if (_spieler is not null && w.Justiz.IstAktiv && w.Justiz.KerkerBis(_spieler) is int kerker)
            zeilen.Add($"Euer Oberhaupt sitzt bis Ende {kerker} im Kerker.");
        if (_spieler is not null && w.Justiz.IstAktiv && w.Justiz.PrangerBis(_spieler) is int pranger)
            zeilen.Add($"Ihr steht bis {pranger} am Pranger (−{Zahlenformat.Ganz(w.Justiz.Daten.PrangerAnsehen)} Ansehen).");
        if (w.Rat.IstAktiv)
            zeilen.Add($"Bürgermeister: {w.Rat.Aemter.First().Inhaber}" + (w.Rat.Antrag is { } antrag
                ? $" · Antrag liegt vor: Statut {antrag.Statut.Kuerzel()} ({antrag.Antragsteller.Inhaber})" : ""));
        return string.Join("\n", zeilen);
    }

    /// <summary>
    /// Der Aktionsknopf: im Rathaus „Aufsteigen", an einem freien Platz „Kaufen" bzw. „Pachten".
    /// Ist die Aktion (noch) nicht möglich, ist er gesperrt und nennt den Grund.
    /// </summary>
    private void AktionKnopfAnzeigen(Stadtwirtschaft w, LiegenschaftDaten? frei)
    {
        // Erst ausrechnen, dann EINMAL zuweisen. Wichtig: Würde der Knopf in jedem Bild kurz unsichtbar
        // und wieder sichtbar geschaltet, vergäße Godot, dass er gerade gedrückt wird – ein Klick, dessen
        // Drücken und Loslassen in verschiedene Bilder fallen, ginge verloren.
        var (sichtbar, gesperrt, text, aktion) = AktionBestimmen(w, frei);
        _aktion = aktion;
        _aktionKnopf.Visible = sichtbar;
        if (!sichtbar) return;
        _aktionKnopf.Disabled = gesperrt;
        if (_aktionKnopf.Text != text) _aktionKnopf.Text = text;
    }

    private (bool Sichtbar, bool Gesperrt, string Text, Func<Befehl>? Aktion) AktionBestimmen(Stadtwirtschaft w, LiegenschaftDaten? frei)
    {
        if (_spieler is null) return (false, true, "", null);
        var waehrung = w.Daten.Allgemein.Waehrung;

        if (frei is not null)
        {
            var kosten = frei.Kaufpreis + w.Daten.Fixkosten(frei.Art);
            var standOk = _spieler.Stand >= w.Daten.StandStufe(frei.MindestStand);
            var geldOk = _spieler.Vermoegen >= kosten;
            var text = !standOk ? $"Erst ab dem Stand {frei.MindestStand}"
                : !geldOk ? $"Es fehlen {Zahlenformat.Ganz(kosten - _spieler.Vermoegen)} {waehrung}"
                : frei.IstPacht ? $"Pachten ({Zahlenformat.Ganz(w.Daten.Fixkosten(frei.Art))} {waehrung} im Jahr)"
                : $"Kaufen ({Zahlenformat.Ganz(frei.Kaufpreis)} {waehrung})";
            return (true, !standOk || !geldOk || !_spieler.IstAktiv, text, () => new LiegenschaftErwerben(frei.Name));
        }

        if (_auswahlKennung != "Rathaus" || w.NaechsterStand(_spieler) is not { } naechster)
            return (false, true, "", null);
        var reicht = _spieler.Vermoegen >= naechster.Schwelle;
        var standText = reicht
            ? $"Aufsteigen zum {naechster.Name} ({Zahlenformat.Ganz(naechster.Gebuehr)} {waehrung})"
            : $"{naechster.Name} ab {Zahlenformat.Ganz(naechster.Schwelle)} {waehrung} – es fehlen {Zahlenformat.Ganz(naechster.Schwelle - _spieler.Vermoegen)}";
        return (true, !reicht || !_spieler.IstAktiv, standText, () => new StandErwerben());
    }

    /// <summary>Die Tafel oben links macht Platz, solange unten links eine Auswahl offen ist.</summary>
    private void PlatzMachen() => _hinweis.Visible = !_auswahl.Visible;

    private string BetriebsText(Betrieb b, Stadtwirtschaft w)
    {
        // Eigener Betrieb: kurz halten – Lager steht im Kontor, Personal und Karren im Bedienfeld darunter.
        if (b.Konto == _verwaltung.Spieler)
            // Immer zwei Zeilen: Sonst wächst und schrumpft das Feld mit der Warnung, und die Knöpfe
            // darunter springen dem Spieler unter der Maus weg.
            return $"{b.Name} (Euer Betrieb)\n" + (b.Belegt == 0 ? "⚠ Kein Personal – stellt unten Arbeitskräfte ein."
                : b.Stillstand is { } eigenerStillstand ? $"⚠ {eigenerStillstand}" : "✔ Die Arbeit läuft.");

        var zeilen = new List<string>
        {
            b.Name,
            $"Besitzer: {b.Konto.Inhaber}",
            $"Belegung: {(b.Belegt == 0 ? "niemand" : string.Join(", ", b.Belegung.Select(x => $"{x.Value}× {x.Key}")))} ({b.Belegt}/{b.Plaetze} Plätze)",
        };
        if (b.IstWerkstatt) zeilen.Add($"Produkt: {b.Produkt}");
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
