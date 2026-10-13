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
///   --drehung=1        Kamera um 1–3 Vierteldrehungen gedreht starten (0 = Blick nach Nordosten)
///   --innen=1          direkt in der Innenansicht der eigenen Gießerei starten
///   --verwalten=1      das Bedienfeld der eigenen Gießerei gleich öffnen
///   --vermoegen=12000  Startvermögen des Spielers setzen (zum Ausprobieren des Aufstiegs)
///   --bericht=1        gleich zu Beginn einen Jahresbericht zeigen (zum Testen der Anzeige)
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
    private Button _standKnopf = null!;
    private JahresberichtAnzeige _jahresbericht = null!;
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

    public override void _Ready()
    {
        _stadt = GetNode<Stadtansicht>("%Stadt");
        _kontor = GetNode<KontorAnzeige>("%Kontor");
        _tafel = GetNode<PanelContainer>("%Tafel");
        _auswahl = GetNode<PanelContainer>("%Auswahl");
        _auswahlText = GetNode<Label>("%AuswahlText");
        _innenKnopf = GetNode<Button>("%InnenKnopf");
        _verwaltung = GetNode<BetriebsVerwaltung>("%Verwaltung");
        _standKnopf = GetNode<Button>("%StandKnopf");
        _jahresbericht = GetNode<JahresberichtAnzeige>("%Jahresbericht");
        _blick = GetNode<Label>("%Blick");
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
        {
            Protokollieren($"Jahresabrechnung {jahr} abgeschlossen. Es ist nun {_simulation.Zeit.DatumText}.");
            if (_spieler is null || _simulation.IstBeendet) return;
            _jahresbericht.Zeigen(_simulation, _spieler);
            if (BeiJahresberichtPausieren) _simulation.Takt.IstPausiert = true;
        };
        _simulation.PartieBeendet += () =>
        {
            Protokollieren($"Die Partie ist zu Ende: {_simulation.Endgrund}");
            if (_spieler is not null) _jahresbericht.Zeigen(_simulation, _spieler, _simulation.Endgrund);
        };
        _jahresbericht.Geschlossen += () => { if (!_simulation.IstBeendet) _simulation.Takt.IstPausiert = false; };
        _standKnopf.Pressed += () =>
        {
            if (_simulation.Wirtschaft is not { } wirtschaft || _spieler is null) return;
            var ergebnis = wirtschaft.Ausfuehren(_spieler, new StandErwerben());
            if (!ergebnis.Erfolg) Protokollieren("✗ " + ergebnis.Meldung); // Erfolg meldet die Stadt selbst
        };

        var minuten = _simulation.Takt.RundenDauerSekunden / 60;
        _hinweis.Text = $"1 Spieljahr = {minuten:0} Min. · {_simulation.Einstellungen.SiegbedingungText}\n"
                      + "Leertaste Pause · 1–4 Tempo · Mausrad Zoom\nRechte Maustaste/WASD verschieben · Q/E drehen\n"
                      + "Doppelklick auf eine Gießerei = Innenansicht · Esc zurück";

        _stadt.GebaeudeAngeklickt += kennung => { _auswahlKennung = kennung; AuswahlAnzeigen(); };
        _stadt.InnenansichtGewechselt += betrieb =>
        {
            if (betrieb is not null) _auswahlKennung = Stadtansicht.BetriebsKennung(betrieb);
            Protokollieren(betrieb is null ? "Zurück in den Gassen der Stadt." : $"Ihr betretet die {betrieb.Name}.");
        };
        _innenKnopf.Pressed += InnenKnopfGedrueckt;
        _verwaltung.Gemeldet += Protokollieren;
        _kontor.VerwaltenGewuenscht += betrieb => { _auswahlKennung = Stadtansicht.BetriebsKennung(betrieb); AuswahlAnzeigen(); };
        if (optionen.TryGetValue("auswahl", out var vorauswahl))
            _auswahlKennung = vorauswahl;
        if (optionen.TryGetValue("drehung", out var d) && int.TryParse(d, out var drehung))
            _stadt.Drehen(drehung, sofort: true);
        if (optionen.TryGetValue("verwalten", out var verwalten) && verwalten == "1" && _eigeneGiesserei is not null)
            _auswahlKennung = Stadtansicht.BetriebsKennung(_eigeneGiesserei);
        if (optionen.TryGetValue("bericht", out var bericht) && bericht == "1" && _spieler is not null)
            _jahresbericht.Zeigen(_simulation, _spieler);
        if (optionen.TryGetValue("innen", out var innen) && innen == "1" && _eigeneGiesserei is not null)
            _stadt.InnenansichtBetreten(_eigeneGiesserei);

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
    /// der Spieler mit einer Gießerei, ein KI-Rivale mit Gießerei und Mine.
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

        double? startvermoegen = optionen.TryGetValue("vermoegen", out var v) && double.TryParse(v, out var betrag) ? betrag : null;
        var spieler = w.KontoEroeffnen("Familie Hammerschmid", startvermoegen: startvermoegen);
        _spieler = spieler;
        // Bankrott des Spielers beendet die Partie; ein bankrotter Rivale scheidet nur aus.
        w.Bankrott += konto =>
        {
            if (konto == spieler)
                simulation.Beenden($"{spieler.Inhaber} konnte die Schulden nicht begleichen.");
        };
        var giesserei = w.BetriebEroeffnen("Gießerei am Weinmarkt", Betriebsart.Giesserei, spieler);
        giesserei.Produkt = "Silberring";
        w.Einstellen(giesserei, "Geselle", 2);

        var rivale = w.KontoEroeffnen("Haus Eisenbart");
        var rivalenGiesserei = w.BetriebEroeffnen("Gießerei Eisenbart", Betriebsart.Giesserei, rivale);
        rivalenGiesserei.Produkt = "Rapier";
        w.Einstellen(rivalenGiesserei, "Geselle", 2);
        var grube = w.BetriebEroeffnen("Grube Eisenbart", Betriebsart.Mine, rivale);
        w.Einstellen(grube, "Ork", 3);

        w.Meldung += Protokollieren;
        _eigeneGiesserei = giesserei;

        // Ab jetzt führt die KI Haus Eisenbart: Produkt, Förderung, Personal, Eskorten und Karren.
        // Ihre Entscheidungen erscheinen im Protokoll und im Kontor.
        w.KiAnmelden(rivale);
        _kontor.Verbinden(simulation, spieler, giesserei, rivale);
        _verwaltung.Verbinden(simulation, spieler);
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
            // Esc geht einen Schritt zurück: erst aus der Innenansicht, dann Auswahl schließen.
            if (_stadt.InnenBetrieb is not null)
                _stadt.InnenansichtVerlassen();
            else
            {
                _auswahlKennung = null;
                _auswahl.Hide();
                _verwaltung.Zeigen(null);
            }
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
        _auswahlText.Text = betrieb is not null ? BetriebsText(betrieb, w) : _auswahlKennung switch
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
        StandKnopfAnzeigen(w);
        _auswahl.Show();
    }

    /// <summary>Im Rathaus: Ständeordnung, Rangliste und das geltende Statut.</summary>
    private string RathausText(Stadtwirtschaft w)
    {
        var waehrung = w.Daten.Allgemein.Waehrung;
        var zeilen = new List<string> { "Rathaus", "" };
        if (_spieler is not null)
        {
            zeilen.Add($"Euer Stand: {w.StandVon(_spieler).Name}");
            if (w.NaechsterStand(_spieler) is { } neu)
                zeilen.Add($"Nächster Stand: {neu.Name} ab {Zahlenformat.Ganz(neu.Schwelle)} {waehrung} Vermögen, Gebühr {Zahlenformat.Ganz(neu.Gebuehr)}\n  ({neu.Privilegien})");
            zeilen.Add("");
        }
        zeilen.Add("Rangliste:");
        var platz = 1;
        foreach (var konto in w.Rangliste)
            zeilen.Add(konto.IstBankrott
                ? $"  –  {konto.Inhaber} (bankrott)"
                : $"  {platz++}. {konto.Inhaber} · {w.StandVon(konto).Name} · {Zahlenformat.Ganz(konto.Vermoegen)} {waehrung}");
        zeilen.Add("");
        zeilen.Add($"Geltendes Fron- und Sklavenstatut: {w.Statut.Kuerzel()} – {w.Statut.Name()}. Ämter und Wahlen folgen später.");
        return string.Join("\n", zeilen);
    }

    /// <summary>Der Aufstiegsknopf erscheint nur im Rathaus und ist gesperrt, solange das Vermögen nicht reicht.</summary>
    private void StandKnopfAnzeigen(Stadtwirtschaft w)
    {
        var naechster = _spieler is null ? null : w.NaechsterStand(_spieler);
        _standKnopf.Visible = _auswahlKennung == "Rathaus" && naechster is not null;
        if (naechster is null || _spieler is null) return;
        var reicht = _spieler.Vermoegen >= naechster.Schwelle;
        _standKnopf.Disabled = !reicht || _spieler.IstBankrott;
        _standKnopf.Text = reicht
            ? $"Aufsteigen zum {naechster.Name} ({Zahlenformat.Ganz(naechster.Gebuehr)} {w.Daten.Allgemein.Waehrung})"
            : $"{naechster.Name} ab {Zahlenformat.Ganz(naechster.Schwelle)} {w.Daten.Allgemein.Waehrung} – es fehlen {Zahlenformat.Ganz(naechster.Schwelle - _spieler.Vermoegen)}";
    }

    /// <summary>Die Tafel oben links macht Platz, solange unten links eine Auswahl offen ist.</summary>
    private void PlatzMachen() => _hinweis.Visible = !_auswahl.Visible;

    private string BetriebsText(Betrieb b, Stadtwirtschaft w)
    {
        // Eigener Betrieb: kurz halten – Lager steht im Kontor, Personal und Karren im Bedienfeld darunter.
        if (b.Konto == _verwaltung.Spieler)
            // Immer zwei Zeilen: Sonst wächst und schrumpft das Feld mit der Warnung, und die Knöpfe
            // darunter springen dem Spieler unter der Maus weg.
            return $"{b.Name} (Euer Betrieb)\n" + (b.Stillstand is { } eigenerStillstand ? $"⚠ {eigenerStillstand}" : "✔ Die Arbeit läuft.");

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
