using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
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
///   --blick=0,0,120    Kamera auf einen Punkt der Stadt (x,z in Metern) und optional auf eine Zoomstufe stellen
///   --ui=0             Tafeln und Fenster ausblenden (freie Sicht für Bildschirmfotos)
///   --aufsteigen=1     das Wohnhaus zum Bürgerhaus ausbauen und zum Bürger aufsteigen (nötig, um Werkstätten zu bauen; mit --vermoegen=30000)
///   --spulen=1020      so viele Spielminuten auf einen Schlag ablaufen lassen (1020 = ein Spieljahr; nach einem --bauklick)
///   --speichern=x.json die Partie nach --bauklick und --spulen in den Spielstand-Ordner speichern (für Tests mit --laden)
///   --bauen=1          den Baumodus öffnen (=wohnhaus:2 oder =werkstatt:Tischlerei wählt gleich das Gebäude)
///   --bauort=-96,-3,0  mit --bauen=<Gebäude>: der Umriss sitzt an dieser Stelle (x,z in Metern, Drehung 0–3), bis die Maus bewegt wird
///   --bauklick=1       mit --bauort: die Rückfrage öffnen (=2: gleich bauen; auch bei --leerstand: gleich kaufen bzw. abreißen)
///   --leerstand=kaufen mit --auswahl=wohnhaus:<Inhaber>: die Rückfrage zum Kauf des leeren Hauses öffnen (=abreissen: zum Abriss; Leerzeichen als _)
///   --innen=1          direkt in der Innenansicht der eigenen Werkstatt starten (--innen=Rathaus, --innen=Dom bzw. --innen=wohnhaus:<Inhaber>: dort)
///   --raum=2           mit --innen: gleich im zweiten Raum der Raumleiste (Werkstatt: 1 Werkraum, 2 Lager; Wohnhaus: 1 Wohnstube, 2 Kontor)
///   --innenblick=L1    mit --innen: gleich auf diesem Blickpunkt des Raums stehen (0 = Mitte, L1, L2, R1, R2, O1, U1 – was das Modell hat)
///   --tafel=personal   mit --innen: gleich eine Tafel der Bedienung öffnen (personal, produktion, lager, karren, einrichten)
///   --arbeiter=1,2     mit --innen: diese Arbeiter im Raum auswählen (Nummern wie im Personalbuch)
///   --einrichtung=Werkzeug1,Regale  der eigenen Werkstatt gleich diese Einrichtung kaufen (mit --vermoegen=5000)
///   --einstellen=Lehrling,Geselle   der eigenen Werkstatt gleich diese Arbeiter einstellen
///   --ausbauen=2       die eigene Werkstatt gleich zweimal ausbauen und den Umbau abwarten (Stufe 3; mit --vermoegen=30000)
///   --verwalten=1      das Bedienfeld der eigenen Gießerei gleich öffnen
///   --karren=Ochsenkarren,Kriegskarren  der eigenen Gießerei gleich diese Karren dazukaufen (mit --vermoegen=5000; Handkarren, Ochsenkarren, Kriegskarren)
///   --vermoegen=12000  Startvermögen des Spielers setzen (zum Ausprobieren des Aufstiegs)
///   --beruf=Steinmetz  Startberuf der Testpartie (Schmied, Tischler, Steinmetz, Alchimist, Wirt, Prediger, Dieb, Gardist)
///   --bericht=1        gleich zu Beginn einen Jahresbericht zeigen (zum Testen der Anzeige)
///   --rat=1            gleich zu Beginn die Ratskammer öffnen
///   --familie=1        gleich zu Beginn die Familientafel öffnen
///   --gericht=1        gleich zu Beginn den Gerichtssaal öffnen
///   --hinterzimmer=1   gleich zu Beginn das Hinterzimmer öffnen
///   --markt=1          gleich zu Beginn das Marktfenster öffnen
///   --verkaufen=1      gleich zu Beginn die Rückfrage zum Verkauf der eigenen Werkstatt öffnen (=2: vorher eine freie
///                      Liegenschaft dazukaufen und bis Mittag laufen lassen, damit der Verkauf erlaubt ist und Ware im Lager liegt)
///   --einstellungen=1  gleich zu Beginn das Fenster „Einstellungen“ öffnen
///   --kanten=<Art>     Kantenglättung nur für diesen Start (Aus, Fxaa, Smaa, Taa, Msaa2, Msaa4, Msaa8)
///   --schatten=<Stufe> Schattenqualität nur für diesen Start (Niedrig, Mittel, Hoch)
///   --holzschnitt=<An|Aus> Holzschnitt-Stil nur für diesen Start
///   --umriss=<0–100>   Stärke des Holzschnitt-Umrisses in Prozent, nur für diesen Start
///   --bloom=<An|Aus>   Bloom nur für diesen Start
///   --bloomstaerke=<0–100> Stärke des Bloom in Prozent, nur für diesen Start
///   --ssao=<Stufe>     Umgebungsverdeckung nur für diesen Start (Aus, Niedrig, Mittel, Hoch)
///   --ssil=<An|Aus>    indirektes Licht (SSIL) nur für diesen Start
///   --fsr=<An|Aus>     FSR 2.2 (Hochskalierung) nur für diesen Start
///   --renderaufloesung=<25–100> Renderauflösung in Prozent bei FSR, nur für diesen Start
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
    private WappenBild _auswahlWappen = null!;
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
    private MarktAnzeige _markt = null!;
    private PersonalAnzeige _personalTafel = null!;
    private ProduktionAnzeige _produktionTafel = null!;
    private LagerAnzeige _lagerTafel = null!;
    private KarrenAnzeige _karrenTafel = null!;
    private EinrichtenAnzeige _einrichtenTafel = null!;
    /// <summary>Die Tafeln der Bedienung im Innenraum (rechts im Bild, damit der Raum frei bleibt).</summary>
    private InnenTafel[] _innenTafeln = null!;
    private VerkaufAnzeige _verkauf = null!;
    private BauAnzeige _bau = null!;
    private BauRueckfrage _bauFrage = null!;
    private Button _leerstandKaufen = null!;
    private Button _leerstandAbreissen = null!;
    /// <summary>Das leere Wohnhaus, zu dem gerade die Rückfrage offen ist – nach dem Kauf zeigt die Auswahl seine Baustelle.</summary>
    private Grundstueck? _leerstandZiel;
    /// <summary>Startoption --bauklick: 1 = Rückfrage öffnen, 2 = gleich bauen – sobald der Umriss steht.</summary>
    private int _bauAutoKlick;
    /// <summary>Startoption --spulen: So viele Spielminuten (Ticks) auf einen Schlag ablaufen lassen, sobald nichts mehr zu klicken bleibt.</summary>
    private int _spulen;
    /// <summary>Startoption --innen=betrieb:<Name> für eine Werkstatt, die erst durch --bauklick und --spulen entsteht.</summary>
    private string? _innenWartet;
    /// <summary>Startoption --speichern=x.json: Nach --bauklick und --spulen wird die Partie unter diesem Namen gespeichert.</summary>
    private string? _speichernAls;
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
    private PanelContainer _raumleiste = null!;
    private HBoxContainer _raumknoepfe = null!;

    // Spielmenü (Esc): Weiter, Speichern, Laden, Hauptmenü, Beenden
    private PanelContainer _spielmenue = null!;
    private EinstellungenAnzeige _einstellungen = null!;
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
        AuswahlWappenEinbauen();
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
        _markt = new MarktAnzeige();
        ratMitte.AddChild(_markt);
        _verkauf = new VerkaufAnzeige();
        ratMitte.AddChild(_verkauf);
        InnenTafelnBauen();
        _bauFrage = new BauRueckfrage();
        ratMitte.AddChild(_bauFrage);
        _bau = new BauAnzeige();
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_bau); // rechts, anstelle des Kontors
        _gerichtKnopf = GetNode<Button>("%GerichtKnopf");
        // Der Bericht sitzt in einem CenterContainer: Der hält ihn mittig, egal wie hoch er wird.
        var berichtMitte = Hauptmenue.Mitte();
        GetNode<CanvasLayer>("Oberflaeche").AddChild(berichtMitte);
        _jahresbericht.Reparent(berichtMitte);
        _blick = GetNode<Label>("%Blick");
        _datum = GetNode<Label>("%Datum");
        _uhrzeit = GetNode<Label>("%Uhrzeit");
        _tagesfortschritt = GetNode<ProgressBar>("%Tagesfortschritt");
        _tagesfortschritt.MaxValue = Spielzeit.MinutenProTag; // 05–22 Uhr
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
        RaumleisteBauen();
        LeerstandKnoepfeBauen();

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
        _kontor.MarktGewuenscht += MarktOeffnen;
        _kontor.BauenGewuenscht += BauenOeffnen;
        _bau.Geschlossen += () => { _bauFrage.Hide(); AuswahlAnzeigen(); };
        _stadt.BauPlatzGewaehlt += befehl =>
        {
            _stadt.BauFest = true; // der Umriss bleibt stehen, bis die Rückfrage beantwortet ist
            _bauFrage.OeffnenNeubau(befehl);
        };
        _stadt.BauAbgebrochen += _bau.Zurueck;
        _bauFrage.Gemeldet += Protokollieren;
        _bauFrage.Abgebrochen += () => _stadt.BauFest = false;
        _bauFrage.Beauftragt += neubau =>
        {
            if (neubau) _bau.Schliessen();
            if (_simulation.Wirtschaft is not { } wirtschaft || _spieler is null) return;
            // Die Auswahl zeigt jetzt die Baustelle (Neubau) bzw. die des gekauften Leerstands
            var baustelle = neubau
                ? wirtschaft.Bauen.Grundstuecke.LastOrDefault(g => g.Besitzer == _spieler && g.Arbeit == Bauarbeit.Neubau)
                : _leerstandZiel;
            if (baustelle is not null) _auswahlKennung = Stadtansicht.BaustellenKennung(baustelle);
        };

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
                      + "Leertaste Pause · 1–4 Tempo (5–6 Test) · Mausrad Zoom\nRechte Maustaste/WASD verschieben · Q/E drehen · Alt: alle Namen\n"
                      + "Doppelklick auf Werkstatt, Wohnhaus, Rathaus oder Dom = hineingehen · Esc zurück bzw. Spielmenü";

        _stadt.GebaeudeAngeklickt += kennung =>
        {
            if (RaumAngeklickt(kennung)) return;
            _auswahlKennung = kennung;
            AuswahlAnzeigen();
            if (kennung == "Marktplatz") MarktOeffnen(); // wie im Original: ein Klick auf den Markt öffnet die Preistafel
        };
        _stadt.InnenansichtGewechselt += kennung =>
        {
            if (kennung is not null) _auswahlKennung = kennung;
            Protokollieren(kennung is null ? "Zurück in den Gassen der Stadt."
                : _stadt.HatBedienung ? $"Ihr betretet {_stadt.InnenName}. Gegenstände anklicken (Buch, Werkbank, Lager, Hoftor, Werkzeugwand) · Maus an den Bildrand: Blick wechseln · Rechtsklick: hinaus."
                : _stadt.RaumKennung is not null ? $"Ihr betretet {(kennung is "Rathaus" ? "das Rathaus" : _stadt.InnenName)}. Räume wechseln: Leiste unten oder Q/E · ein Klick in den Raum öffnet seine Tafel."
                : $"Ihr betretet {(kennung is "Dom" ? "den" : "die")} {_stadt.InnenName}.");
            RaumleisteAufbauen();
        };
        _stadt.RaumGewechselt += _ =>
        {
            RaumleisteAufbauen();
            foreach (var tafel in _innenTafeln) tafel.Hide(); // ein anderer Raum, eine andere Bedienung
        };
        _stadt.InnenRaeumeGeaendert += RaumleisteAufbauen; // z. B. die Sägehalle nach dem Ausbau zum Sägewerk
        _innenKnopf.Pressed += InnenKnopfGedrueckt;
        foreach (var tafel in _innenTafeln)
        {
            tafel.Gemeldet += Protokollieren;
            tafel.Auswahl = () => _stadt.GewaehlteArbeiter;
        }
        _karrenTafel.HandelGewuenscht += b => { TafelnSchliessen(); _markt.Oeffnen(b); };
        _stadt.BedienungAngeklickt += (tafel, _) => InnenTafelOeffnen(tafel);
        _verwaltung.Gemeldet += Protokollieren;
        _verwaltung.HandelGewuenscht += b => { TafelnSchliessen(); _markt.Oeffnen(b); };
        _markt.Gemeldet += Protokollieren;
        _verwaltung.VerkaufGewuenscht += b => { TafelnSchliessen(); _verkauf.Oeffnen(b); };
        _verkauf.Gemeldet += Protokollieren;
        _kontor.Gemeldet += Protokollieren;
        _kontor.VerwaltenGewuenscht += betrieb => { _auswahlKennung = Stadtansicht.BetriebsKennung(betrieb); AuswahlAnzeigen(); };
        if (optionen.TryGetValue("auswahl", out var vorauswahl))
            _auswahlKennung = vorauswahl.Replace('_', ' '); // Leerzeichen in Namen als _ (--auswahl=wohnhaus:Haus_Eisenbart)
        if (optionen.TryGetValue("drehung", out var d) && int.TryParse(d, out var drehung))
            _stadt.Drehen(drehung, sofort: true);
        if (optionen.TryGetValue("blick", out var blick)) // --blick=x,z[,zoom]: Kamera auf einen Punkt der Stadt (Meter) stellen
        {
            var teile = blick.Split(',').Select(t => float.TryParse(t, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : float.NaN).ToArray();
            if (teile.Length >= 2 && !float.IsNaN(teile[0]) && !float.IsNaN(teile[1]))
                _stadt.KameraSetzen(teile[0], teile[1], teile.Length >= 3 && !float.IsNaN(teile[2]) ? teile[2] : null);
        }
        if (optionen.TryGetValue("ui", out var ui) && ui == "0")
            GetNode<CanvasLayer>("Oberflaeche").Visible = false; // freie Sicht auf die Stadt für Bildschirmfotos
        if (optionen.TryGetValue("verwalten", out var verwalten) && verwalten == "1" && _eigeneGiesserei is not null)
            _auswahlKennung = Stadtansicht.BetriebsKennung(_eigeneGiesserei);
        if (optionen.TryGetValue("karren", out var karrenTypen) && _eigeneGiesserei is { } kaeufer && _simulation.Wirtschaft is { } wKarren && _spieler is not null)
            foreach (var typ in karrenTypen.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var ergebnis = wKarren.Ausfuehren(_spieler, new KarrenKaufen(kaeufer.Name, typ));
                if (!ergebnis.Erfolg) Protokollieren("--karren=" + typ + ": " + ergebnis.Meldung);
            }
        if (optionen.TryGetValue("rat", out var rat) && rat == "1")
            _ratskammer.Oeffnen();
        if (optionen.TryGetValue("familie", out var familie) && familie == "1")
            _familientafel.Oeffnen();
        if (optionen.TryGetValue("gericht", out var gericht) && gericht == "1")
            _gerichtssaal.Oeffnen();
        if (optionen.TryGetValue("hinterzimmer", out var hinterzimmer) && hinterzimmer == "1")
            _hinterzimmer.Oeffnen();
        if (optionen.TryGetValue("markt", out var markt) && markt == "1")
            _markt.Oeffnen();
        if (optionen.TryGetValue("verkaufen", out var verkaufen) && verkaufen is "1" or "2" && _eigeneGiesserei is { } werkstatt)
        {
            if (verkaufen == "2") VerkaufVorbereiten();
            _auswahlKennung = Stadtansicht.BetriebsKennung(werkstatt);
            _verkauf.Oeffnen(werkstatt);
        }
        if (optionen.TryGetValue("aufsteigen", out var aufsteigen) && aufsteigen == "1") AufsteigenVorbereiten();
        if (optionen.TryGetValue("ausbauen", out var ausbauen) && int.TryParse(ausbauen, out var stufen)) AusbauenVorbereiten(stufen);
        if (optionen.TryGetValue("einstellen", out var einstellen) && _eigeneGiesserei is { } einsteller && _simulation.Wirtschaft is { } wEin && _spieler is not null)
            foreach (var kraft in einstellen.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var ergebnis = wEin.Ausfuehren(_spieler, new ArbeiterEinstellen(einsteller.Name, kraft));
                if (!ergebnis.Erfolg) Protokollieren("--einstellen=" + kraft + ": " + ergebnis.Meldung);
            }
        if (optionen.TryGetValue("einrichtung", out var einrichtung) && _eigeneGiesserei is { } kaeufer2 && _simulation.Wirtschaft is { } wEinr && _spieler is not null)
            foreach (var kennung in einrichtung.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var ergebnis = wEinr.Ausfuehren(_spieler, new EinrichtungKaufen(kaeufer2.Name, kennung));
                if (!ergebnis.Erfolg) Protokollieren("--einrichtung=" + kennung + ": " + ergebnis.Meldung);
            }
        if (optionen.TryGetValue("bauen", out var bauen) && bauen != "0" && _spieler is not null)
            BauenStarten(bauen, optionen);
        if (optionen.TryGetValue("leerstand", out var leerstand) && leerstand is "kaufen" or "abreissen"
            && _simulation.Wirtschaft is { } wLeer && _spieler is not null && LeerstandKonto(wLeer) is { } leerKonto)
        {
            _leerstandZiel = wLeer.Bauen.Leerstand(leerKonto);
            if (optionen.GetValueOrDefault("bauklick") == "2") // gleich kaufen bzw. abreißen lassen, ohne Rückfrage
            {
                Befehl befehl = leerstand == "abreissen" ? new LeerstandAbreissen(leerKonto.Inhaber) : new LeerstandKaufen(leerKonto.Inhaber);
                var ergebnis = wLeer.Ausfuehren(_spieler, befehl);
                Protokollieren(ergebnis.Erfolg ? ergebnis.Meldung : "✗ " + ergebnis.Meldung);
                if (ergebnis.Erfolg && _leerstandZiel is not null) _auswahlKennung = Stadtansicht.BaustellenKennung(_leerstandZiel);
            }
            else _bauFrage.OeffnenLeerstand(leerKonto, leerstand == "abreissen"); // mit --auswahl=wohnhaus:<Inhaber>, Leerzeichen als _
        }
        if (optionen.TryGetValue("spulen", out var spulen) && int.TryParse(spulen, out var ticks)) _spulen = ticks;
        if (optionen.TryGetValue("speichern", out var speichern)) _speichernAls = speichern;
        if (optionen.TryGetValue("kanten", out var kanten) && Enum.TryParse<Kantenglaettung>(kanten, true, out var art))
            Fenster.KantenglaettungSetzen(art, speichern: false); // für Vergleichsbilder, ändert die gespeicherte Wahl nicht
        if (optionen.TryGetValue("schatten", out var schatten) && Enum.TryParse<Schattenqualitaet>(schatten, true, out var stufe))
            Fenster.SchattenqualitaetSetzen(stufe, speichern: false); // ebenso
        if (optionen.ContainsKey("holzschnitt") || optionen.ContainsKey("umriss")) // ebenso; was fehlt, bleibt wie eingestellt
        {
            var holzschnittAn = optionen.TryGetValue("holzschnitt", out var holzschnitt) ? !holzschnitt.Equals("Aus", StringComparison.OrdinalIgnoreCase) : Fenster.HolzschnittAn;
            var umrissProzent = optionen.TryGetValue("umriss", out var umriss) && int.TryParse(umriss, out var staerke) ? staerke : Fenster.HolzschnittUmriss;
            Fenster.HolzschnittSetzen(holzschnittAn, umrissProzent, speichern: false);
        }
        if (optionen.ContainsKey("bloom") || optionen.ContainsKey("bloomstaerke")) // ebenso; was fehlt, bleibt wie eingestellt
        {
            var bloomAn = optionen.TryGetValue("bloom", out var bloom) ? !bloom.Equals("Aus", StringComparison.OrdinalIgnoreCase) : Fenster.BloomAn;
            var bloomProzent = optionen.TryGetValue("bloomstaerke", out var bloomStaerke) && int.TryParse(bloomStaerke, out var prozent) ? prozent : Fenster.BloomStaerke;
            Fenster.BloomSetzen(bloomAn, bloomProzent, speichern: false);
        }
        if (optionen.TryGetValue("ssao", out var ssao) && Enum.TryParse<Umgebungsverdeckung>(ssao, true, out var verdeckung))
            Fenster.UmgebungsverdeckungSetzen(verdeckung, speichern: false); // ebenso
        if (optionen.TryGetValue("ssil", out var ssil))
            Fenster.IndirektesLichtSetzen(!ssil.Equals("Aus", StringComparison.OrdinalIgnoreCase), speichern: false);
        if (optionen.ContainsKey("fsr") || optionen.ContainsKey("renderaufloesung")) // ebenso; was fehlt, bleibt wie eingestellt
        {
            var fsrAn = optionen.TryGetValue("fsr", out var fsr) ? !fsr.Equals("Aus", StringComparison.OrdinalIgnoreCase) : Fenster.FsrAn;
            var fsrProzent = optionen.TryGetValue("renderaufloesung", out var aufloesung) && int.TryParse(aufloesung, out var anteil) ? anteil : Fenster.Renderaufloesung;
            Fenster.FsrSetzen(fsrAn, fsrProzent, speichern: false);
        }
        // Erst nach den Grafikoptionen: Das Fenster zeigt beim Öffnen die dann gültigen Werte.
        if (optionen.TryGetValue("einstellungen", out var einstellungen) && einstellungen == "1")
        {
            SpielmenueOeffnen();
            EinstellungenOeffnen();
        }
        if (optionen.TryGetValue("bericht", out var bericht) && bericht == "1" && _spieler is not null)
            _jahresbericht.Zeigen(_simulation, _spieler);
        if (optionen.TryGetValue("innen", out var innen))
        {
            if (innen == "1" && _eigeneGiesserei is not null) _stadt.InnenansichtBetreten(_eigeneGiesserei);
            else if (innen != "1" && !_stadt.InnenansichtBetreten(innen)) _innenWartet = innen; // Rathaus oder Dom; ein Betrieb, den es noch nicht gibt (--bauklick), kommt später
            if (optionen.TryGetValue("raum", out var raum) && int.TryParse(raum, out var raumNummer))
                _stadt.RaumWaehlen(raumNummer - 1); // --raum=2: zweiter Raum der Leiste
            if (optionen.TryGetValue("innenblick", out var blickName) && !_stadt.InnenBlickWaehlen(blickName))
                Protokollieren($"--innenblick={blickName}: Diesen Blickpunkt gibt es in diesem Raum nicht.");
            if (optionen.TryGetValue("arbeiter", out var arbeiter))
                _stadt.ArbeiterWaehlen(arbeiter.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse));
            if (optionen.TryGetValue("tafel", out var tafelName)) InnenTafelOeffnen(tafelName);
        }

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
        _markt.Hide();
        _verkauf.Hide();
        foreach (var tafel in _innenTafeln) tafel.Hide();
        if (_bauFrage.Visible) _bauFrage.Zurueck(); // der Umriss der Platzwahl wird wieder frei
    }

    /// <summary>
    /// Baut die fünf Tafeln der Bedienung im Innenraum und hängt sie rechts ins Bild, mittig in der Höhe und mit Luft über der Raumleiste.
    /// </summary>
    private void InnenTafelnBauen()
    {
        _personalTafel = new PersonalAnzeige();
        _produktionTafel = new ProduktionAnzeige();
        _lagerTafel = new LagerAnzeige();
        _karrenTafel = new KarrenAnzeige();
        _einrichtenTafel = new EinrichtenAnzeige();
        _innenTafeln = new InnenTafel[] { _personalTafel, _produktionTafel, _lagerTafel, _karrenTafel, _einrichtenTafel };
        var rand = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        rand.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        rand.AddThemeConstantOverride("margin_left", 16);
        rand.AddThemeConstantOverride("margin_right", 16);
        rand.AddThemeConstantOverride("margin_top", 16);
        rand.AddThemeConstantOverride("margin_bottom", 84);
        var reihe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End, MouseFilter = Control.MouseFilterEnum.Ignore };
        rand.AddChild(reihe);
        foreach (var tafel in _innenTafeln)
        {
            tafel.Tafel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            reihe.AddChild(tafel.Tafel);
        }
        GetNode<CanvasLayer>("Oberflaeche").AddChild(rand);
    }

    /// <summary>Öffnet die Tafel zu einem Gegenstand im Raum (Personal, Produktion, Lager, Karren, Einrichten) – nur für die eigene Werkstatt.</summary>
    private void InnenTafelOeffnen(string tafel)
    {
        if (_stadt.InnenBetrieb is not { } betrieb) return;
        if (betrieb.Konto != _spieler)
        {
            Protokollieren($"{betrieb.Name} gehört {betrieb.Konto.Inhaber} – die Bücher und Werkzeuge dort sind nicht für Euch.");
            return;
        }
        InnenTafel? ziel = tafel.ToLowerInvariant() switch
        {
            "personal" => _personalTafel,
            "produktion" => _produktionTafel,
            "lager" => _lagerTafel,
            "karren" => _karrenTafel,
            "einrichten" => _einrichtenTafel,
            _ => null,
        };
        if (ziel is null) return;
        TafelnSchliessen();
        ziel.Oeffnen(betrieb);
    }

    /// <summary>
    /// Nur für den Klicktest (--ausbauen=N): Die eigene Werkstatt wird N-mal ausgebaut (Gießerei → Schmiede → Goldschmiede), der Umbau läuft
    /// jeweils durch. Alles mit den gewöhnlichen Befehlen; es braucht Geld und Meistergrad (--vermoegen=30000, eventuell --aufsteigen=1).
    /// </summary>
    private void AusbauenVorbereiten(int stufen)
    {
        if (_simulation.Wirtschaft is not { } w || _spieler is null || _eigeneGiesserei is not { } betrieb) return;
        for (var i = 0; i < stufen; i++)
        {
            var ergebnis = w.Ausfuehren(_spieler, new BetriebAusbauen(betrieb.Name));
            if (!ergebnis.Erfolg) { Protokollieren("--ausbauen: " + ergebnis.Meldung); GD.Print("--ausbauen: " + ergebnis.Meldung); return; }
            for (var t = 0; t < 2000 && betrieb.UmbauMinuten > 0 && !_simulation.IstBeendet; t++) _simulation.Tick();
        }
    }

    /// <summary>
    /// Nur für den Klicktest (--verkaufen=2): Der Spieler kauft die günstigste freie Liegenschaft, die er darf, dazu –
    /// sonst wäre die Werkstatt sein letzter Betrieb –, und die Stadt läuft bis Mittag, damit Ware im Lager liegt.
    /// Alles mit den gewöhnlichen Befehlen; reicht der Stand nicht, wird vorher das Wohnhaus ausgebaut und aufgestiegen
    /// (dafür braucht es Geld: --vermoegen=20000).
    /// </summary>
    private void VerkaufVorbereiten()
    {
        if (_simulation.Wirtschaft is not { } w || _spieler is null) return;
        bool Kaufen()
        {
            var frei = w.FreieLiegenschaften.Where(l => w.LiegenschaftErwerbVerboten(_spieler, l.Name) is null)
                .OrderBy(l => l.Kaufpreis).FirstOrDefault();
            return frei is not null && w.Ausfuehren(_spieler, new LiegenschaftErwerben(frei.Name)).Erfolg;
        }
        var gekauft = Kaufen();
        if (!gekauft) w.Ausfuehren(_spieler, new WohnhausAusbauen()); // der Umbau dauert – aufsteigen erst danach
        while (_simulation.Zeit.MinuteImTag < 12 * 60 - Spielzeit.TagesbeginnMinute && !_simulation.IstBeendet)
            _simulation.Tick();
        if (gekauft) return;
        w.Ausfuehren(_spieler, new StandErwerben());
        if (!Kaufen()) Protokollieren("--verkaufen=2: Keine freie Liegenschaft zu haben – mehr Vermögen mit --vermoegen=20000.");
    }

    /// <summary>
    /// Nur für den Klicktest (--aufsteigen=1): Der Spieler baut sein Wohnhaus zum Bürgerhaus aus und steigt zum Bürger auf –
    /// erst dann darf er Werkstätten bauen. Alles mit den gewöhnlichen Befehlen (dafür braucht es Geld: --vermoegen=30000).
    /// </summary>
    private void AufsteigenVorbereiten()
    {
        if (_simulation.Wirtschaft is not { } w || _spieler is null) return;
        w.Ausfuehren(_spieler, new WohnhausAusbauen());
        for (var i = 0; i < 245 && !_simulation.IstBeendet; i++) _simulation.Tick(); // der Umbau dauert 4 Stunden
        var ergebnis = w.Ausfuehren(_spieler, new StandErwerben());
        if (!ergebnis.Erfolg) Protokollieren("--aufsteigen=1: " + ergebnis.Meldung + " (mehr Vermögen mit --vermoegen=30000)");
    }

    private void MarktOeffnen()
    {
        TafelnSchliessen();
        _markt.Oeffnen();
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
            start = new Spielzeit(Math.Clamp(stunde * 60 - Spielzeit.TagesbeginnMinute, 0, Spielzeit.MinutenProTag - 1));
        double? startvermoegen = optionen.TryGetValue("vermoegen", out var v) && double.TryParse(v, out var betrag) ? betrag : null;

        var (simulation, spieler) = Partieaufbau.Erstellen(einstellungen ?? Partieeinstellungen.Standard, daten,
            neu ?? new NeuePartie(Startvermoegen: startvermoegen, Beruf: optionen.GetValueOrDefault("beruf", "Schmied"),
                Glaube: optionen.TryGetValue("glaube", out var g) && Enum.TryParse<Reichsgilde.Core.Glaube.Konfession>(g, true, out var konfession) ? konfession : null),
            start, Stadtkarten.Greybox);
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
        Hauptmenue.Knopf(_menueKnoepfe, "Einstellungen", EinstellungenOeffnen);
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
        _einstellungen = new EinstellungenAnzeige(EinstellungenSchliessen);
        mitte.AddChild(_einstellungen.Tafel);
        _einstellungen.Tafel.Hide();

        _menueMeldung = Hauptmenue.Text(spalte, "Ungespeicherter Fortschritt geht beim Laden oder im Hauptmenü verloren.", 12, new Color(0.7f, 0.7f, 0.7f), umbruchBreite: 360);
        tafel.Hide();
    }

    // Die Einstellungen liegen an der Stelle des Spielmenüs; das Spiel bleibt dabei pausiert.
    private void EinstellungenOeffnen()
    {
        _einstellungen.Auffrischen();
        _spielmenue.Hide();
        _einstellungen.Tafel.Show();
    }

    private void EinstellungenSchliessen()
    {
        _einstellungen.Tafel.Hide();
        _spielmenue.Show();
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
        _eigeneGiesserei = w.Betriebe.FirstOrDefault(b => b.Konto == spieler && b.InDerStadt); // erste eigene Werkstatt (oder Schmugglerloch)

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
        _markt.Verbinden(simulation, spieler);
        _verkauf.Verbinden(simulation, spieler);
        foreach (var tafel in _innenTafeln) tafel.Verbinden(simulation, spieler);
        // Um 20 Uhr schlagen die Banden zu – ein offenes Hinterzimmer zeigt sofort, was geschah.
        w.Gauner.NachtVorbei += () => { if (_hinterzimmer.Visible) _hinterzimmer.Aufbauen(); };
        _verwaltung.Verbinden(simulation, spieler);
        _bauFrage.Verbinden(simulation, spieler);
        _bau.Verbinden(simulation, spieler, _stadt);
        // Die Stadtansicht stellt jeden Betrieb an seinen Platz im Stadtplan und zeigt die freien Plätze.
        _stadt.Verbinden(simulation, spieler);
        w.BetriebErworben += betrieb =>
        {
            _stadt.BetriebHinzufuegen(betrieb);
            // Hat der Spieler gerade den ausgewählten Platz erworben, wechselt die Auswahl auf den neuen Betrieb.
            if (_auswahlKennung == Stadtansicht.LiegenschaftKennung(betrieb.Name))
                _auswahlKennung = Stadtansicht.BetriebsKennung(betrieb);
        };
        // Ein Betrieb wurde verkauft: Nichts darf mehr auf ihn zeigen. Das Haus in der Stadt baut die Stadtansicht selbst um.
        w.BetriebAbgegeben += (betrieb, frei) =>
        {
            var kennung = Stadtansicht.BetriebsKennung(betrieb);
            if (_stadt.InnenKennung == kennung) _stadt.InnenansichtVerlassen();
            // War der Betrieb ausgewählt, zeigt die Auswahl jetzt das freie Haus mit seinem Kaufangebot (Gegenstück zu BetriebErworben).
            if (_auswahlKennung == kennung) _auswahlKennung = Stadtansicht.LiegenschaftKennung(frei.Name);
            if (_verkauf.Betrieb == betrieb) _verkauf.Hide();
            if (betrieb.Konto != spieler) return;
            if (_markt.Visible) _markt.Oeffnen(); // Auswahl der Handelsbetriebe neu aufbauen
            if (_eigeneGiesserei == betrieb)
                _eigeneGiesserei = w.Betriebe.FirstOrDefault(b => b.Konto == spieler && b.InDerStadt);
        };
    }

    /// <summary>Schließt das Gebäudefenster unten links samt Bedienfeld.</summary>
    private void AuswahlSchliessen()
    {
        _auswahlKennung = null;
        _auswahl.Hide();
        _verwaltung.Zeigen(null);
    }

    public override void _Process(double delta)
    {
        _simulation.Aktualisieren(delta);
        if (_spulen > 0 && _bauAutoKlick == 0) // Klicktest: Zeit überspringen (z. B. eine Bauzeit)
        {
            var ticks = _spulen;
            _spulen = 0;
            for (var i = 0; i < ticks && !_simulation.IstBeendet; i++) _simulation.Tick();
        }
        if (_speichernAls is not null && _bauAutoKlick == 0 && _spulen == 0 && _spieler is not null)
        {
            Protokollieren(Spielstaende.Speichern(_simulation, _spieler, _speichernAls).Meldung); // Spielstand für Tests ablegen
            _speichernAls = null;
        }
        if (_innenWartet is not null && _bauAutoKlick == 0 && _spulen == 0 && _stadt.InnenansichtBetreten(_innenWartet)) _innenWartet = null;
        Anzeigen();
        _stadt.Auswahl = _auswahl.Visible ? _auswahlKennung : null; // Name des ausgewählten Gebäudes bleibt sichtbar
        _stadt.Aktualisieren(delta);
        foreach (var tafel in _innenTafeln) tafel.Aktualisieren(delta);
        var mausPos = GetViewport().GetMousePosition();
        _stadt.UnterTafel = _innenTafeln.Any(t => t.Visible && t.Tafel.GetGlobalRect().HasPoint(mausPos));
        BauAutoKlickPruefen();
        if (_auswahlKennung is not null) AuswahlAnzeigen();
    }

    /// <summary>Tastatur: Leertaste = Pause, 1–4 = Tempostufen, 5–6 = Testtempo 16×/32×. (Kamera: siehe Stadtansicht)</summary>
    public override void _UnhandledInput(InputEvent eingabe)
    {
        if (eingabe is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            // Esc geht einen Schritt zurück: Innenansicht verlassen → Auswahl schließen → Spielmenü auf/zu.
            if (_jahresbericht.Visible) { /* der Bericht wird mit „Weiter" geschlossen */ }
            else if (_bauFrage.Visible)
                _bauFrage.Zurueck();
            else if (_bau.Aktiv)
                _bau.Zurueck();
            else if (_ratskammer.Visible)
                _ratskammer.Hide();
            else if (_familientafel.Visible)
                _familientafel.Hide();
            else if (_gerichtssaal.Visible)
                _gerichtssaal.Hide();
            else if (_hinterzimmer.Visible)
                _hinterzimmer.Hide();
            else if (_markt.Visible)
                _markt.Hide();
            else if (_verkauf.Visible)
                _verkauf.Hide();
            else if (_innenTafeln.Any(t => t.Visible))
                foreach (var tafel in _innenTafeln) tafel.Hide();
            else if (_einstellungen.Tafel.Visible)
                EinstellungenSchliessen();
            else if (_spielmenue.Visible)
                SpielmenueSchliessen();
            else if (_stadt.InnenKennung is not null)
                _stadt.InnenansichtVerlassen();
            else if (_auswahl.Visible)
                AuswahlSchliessen();
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
        _blick.Text = _stadt.RaumName is { } raum ? $"{_stadt.InnenName} · {raum}"
            : _stadt.InnenName is { } innen
            ? $"Blick nach {_stadt.Blickrichtung} · Innenansicht {innen}"
            : $"Blick nach {_stadt.Blickrichtung}";
    }

    private void InnenKnopfGedrueckt()
    {
        if (_stadt.InnenKennung is not null)
            _stadt.InnenansichtVerlassen();
        else if (_auswahlKennung is { } kennung)
            _stadt.InnenansichtBetreten(kennung);
        AuswahlAnzeigen();
    }

    /// <summary>
    /// Ein Raum wurde angeklickt (nur in der Raumansicht möglich) und öffnet seine Tafel: Ratssaal → Stadtrat,
    /// Gerichtssaal und Kerker → Stadtgericht, Wohnstube → Familie. Amtsstube und Kontor zeigen ihr Fenster ohnehin
    /// (siehe <see cref="AuswahlImRaum"/>). Gibt false zurück, wenn die Kennung kein Raum ist.
    /// </summary>
    private bool RaumAngeklickt(string kennung)
    {
        var w = _simulation.Wirtschaft;
        switch (kennung)
        {
            case Stadtansicht.RaumRat:
                if (w?.Rat.IstAktiv == true) { TafelnSchliessen(); _ratskammer.Oeffnen(); }
                return true;
            case Stadtansicht.RaumGericht or Stadtansicht.RaumKerker:
                if (w?.Justiz.IstAktiv == true) { TafelnSchliessen(); _gerichtssaal.Oeffnen(); }
                return true;
            case Stadtansicht.RaumWohnstube:
                TafelnSchliessen();
                _familientafel.Oeffnen();
                return true;
            case Stadtansicht.RaumAmt or Stadtansicht.RaumKontor or Stadtansicht.RaumWerk or Stadtansicht.RaumLager or Stadtansicht.RaumSaege:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// In der Raumansicht bleibt das Bild frei: Das Gebäudefenster erscheint nur in Kontor und Amtsstube,
    /// das Kontor des Hauses (rechts oben) nur im Kontor. Draußen und im Dom (noch Diorama) wie bisher.
    /// </summary>
    private bool AuswahlImRaum => _stadt.RaumKennung is null or Stadtansicht.RaumKontor or Stadtansicht.RaumAmt;

    /// <summary>Leiste unten rechts (dort ist in jedem Raum Platz): ein Knopf je Raum, der gezeigte ist gedrückt, dazu „Zur Stadt".</summary>
    private void RaumleisteBauen()
    {
        _raumleiste = new PanelContainer { Visible = false };
        _raumleiste.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.15f, 0.13f, 0.97f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 8, ContentMarginBottom = 8,
        });
        _raumknoepfe = new HBoxContainer();
        _raumknoepfe.AddThemeConstantOverride("separation", 8);
        _raumleiste.AddChild(_raumknoepfe);
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_raumleiste);
        _raumleiste.GrowHorizontal = Control.GrowDirection.Begin;
        _raumleiste.GrowVertical = Control.GrowDirection.Begin;
    }

    private void RaumleisteAufbauen()
    {
        foreach (var knopf in _raumknoepfe.GetChildren())
        {
            _raumknoepfe.RemoveChild(knopf); // sofort heraus, sonst zählt die alte Breite beim Messen noch mit
            knopf.QueueFree();
        }
        _raumleiste.Visible = _stadt.RaumKennung is not null;
        if (!_raumleiste.Visible) return;
        foreach (var (kennung, name) in _stadt.InnenRaeume)
        {
            var knopf = new Button { Text = name, ToggleMode = true, ButtonPressed = kennung == _stadt.RaumKennung, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(90, 0) };
            knopf.Pressed += () => _stadt.RaumWaehlen(kennung);
            _raumknoepfe.AddChild(knopf);
        }
        var zurueck = new Button { Text = "Zur Stadt (Esc)", FocusMode = Control.FocusModeEnum.None };
        zurueck.Pressed += _stadt.InnenansichtVerlassen;
        _raumknoepfe.AddChild(zurueck);
        // Breite neu messen, dann wieder in die Ecke setzen
        _raumleiste.ResetSize();
        _raumleiste.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight, Control.LayoutPresetMode.Minsize, 16);
    }

    private Betrieb? AusgewaehlterBetrieb() =>
        _simulation.Wirtschaft?.Betriebe.FirstOrDefault(b => Stadtansicht.BetriebsKennung(b) == _auswahlKennung);

    /// <summary>
    /// Setzt rechts neben den Text des Gebäudefensters das Wappen des Besitzers. Der Text steht in der Szene;
    /// hier kommt er samt Wappen in eine Zeile (HBox) an dieselbe Stelle.
    /// </summary>
    private void AuswahlWappenEinbauen()
    {
        var spalte = _auswahlText.GetParent();
        var stelle = _auswahlText.GetIndex();
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 10);
        spalte.RemoveChild(_auswahlText);
        zeile.AddChild(_auswahlText);
        _auswahlText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _auswahlWappen = WappenBild.Neu(null, 56);
        _auswahlWappen.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        _auswahlWappen.Visible = false;
        zeile.AddChild(_auswahlWappen);
        spalte.AddChild(zeile);
        spalte.MoveChild(zeile, stelle);
    }

    /// <summary>Zeigt Informationen zum angeklickten Gebäude.</summary>
    private void AuswahlAnzeigen()
    {
        if (_auswahlKennung is null || _simulation.Wirtschaft is not { } w)
            return;

        // Eine Baustelle, die fertig wird, wird zum Haus bzw. zur Werkstatt: Die Auswahl folgt ihr (ein verschwundener Bauplatz schließt sie).
        if (_auswahlKennung.StartsWith("baustelle:") || _auswahlKennung.StartsWith("bauplatz:"))
        {
            var folgekennung = _stadt.KennungNachBau(_auswahlKennung);
            if (folgekennung is null) { AuswahlSchliessen(); return; }
            _auswahlKennung = folgekennung;
        }

        var betrieb = AusgewaehlterBetrieb();
        _verwaltung.Zeigen(betrieb); // nur bei eigenen Betrieben sichtbar
        _innenKnopf.Visible = _stadt.HatInnenansicht(_auswahlKennung);
        _innenKnopf.Text = _stadt.InnenKennung == _auswahlKennung ? "Zurück zur Stadt (Esc)" : "Innenansicht betreten";
        var frei = _auswahlKennung.StartsWith("liegenschaft:")
            ? w.FreieLiegenschaften.FirstOrDefault(l => Stadtansicht.LiegenschaftKennung(l.Name) == _auswahlKennung)
            : null;
        _auswahlWappen.Wappen = betrieb?.Konto.Wappen;
        if (_auswahlWappen.Visible != betrieb is not null) _auswahlWappen.Visible = betrieb is not null;
        _auswahlText.Text = betrieb is not null ? BetriebsText(betrieb, w)
            : frei is not null ? LiegenschaftText(frei, w)
            : _auswahlKennung switch
        {
            "Marktplatz" => $"Marktplatz\n\n{w.Markt.Staende.Count()} Waren – Preise und Einzelheiten im Marktfenster.",
            "Rathaus" => RathausText(w),
            "Dom" => "Dom\n\nKatholische und evangelische Gläubige aller vier Kulturvölker. "
                     + "Für die Kirche sind Orks und Oger Heiden.\n\nPredigten, Ablass und Kirchenbann folgen später.",
            "Osttor" => "Osttor\n\nHier endet der Schutz der Mauern. Auf dem Weg ins Umland lauern Banditen aller Völker, "
                        + "wilde Ork-Rotten und Ogerhorden – nachts doppelt so häufig.",
            _ when _stadt.EinwohnerText(_auswahlKennung) is { } einwohner => einwohner, // Passanten (Darstellung)
            _ when _stadt.WohnhausText(_auswahlKennung) is { } wohnhaus => wohnhaus + LeerstandZusatz(w), // Wohnhaus eines Spielhauses (Darstellung)
            _ when _stadt.GrundstueckText(_auswahlKennung) is { } grundstueck => grundstueck, // Baustelle oder Bauplatz (Darstellung)
            _ => _auswahlKennung,
        };
        AktionKnopfAnzeigen(w, frei);
        LeerstandKnoepfeAnzeigen(w);
        var ratSichtbar = _auswahlKennung == "Rathaus" && w.Rat.IstAktiv;
        if (_ratKnopf.Visible != ratSichtbar) _ratKnopf.Visible = ratSichtbar; // nur bei Änderung setzen (siehe AktionKnopfAnzeigen)
        var gerichtSichtbar = _auswahlKennung == "Rathaus" && w.Justiz.IstAktiv;
        if (_gerichtKnopf.Visible != gerichtSichtbar) _gerichtKnopf.Visible = gerichtSichtbar;
        if (_auswahl.Visible != AuswahlImRaum) _auswahl.Visible = AuswahlImRaum;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Bauen und Leerstand
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Der Baumodus: Bauzonen zeigen, Gebäude wählen, Platz bestimmen (<see cref="BauAnzeige"/>).</summary>
    private void BauenOeffnen()
    {
        if (_simulation.Wirtschaft?.Bauen.IstAktiv != true)
        {
            Protokollieren("✗ In dieser Stadt wird nicht gebaut (keine Karte geladen).");
            return;
        }
        TafelnSchliessen();
        _bau.Oeffnen();
    }

    /// <summary>
    /// Startoption <c>--bauen</c>: =1 öffnet den Baumodus, =wohnhaus:2 bzw. =werkstatt:Tischlerei wählt gleich das Gebäude.
    /// Mit <c>--bauort=x,z,drehung</c> sitzt der Umriss an dieser Weltstelle (bis die Maus bewegt wird), mit <c>--bauklick=1</c> öffnet sich
    /// dort die Rückfrage, mit <c>--bauklick=2</c> wird gleich gebaut.
    /// </summary>
    private void BauenStarten(string wahl, Dictionary<string, string> optionen)
    {
        BauenOeffnen();
        if (wahl == "1" || !_bau.Aktiv) return;
        var teile = wahl.Split(':', 2);
        Func<double, double, int, BauBefehl>? erzeuger = null;
        if (teile.Length == 2 && teile[0] == "wohnhaus" && int.TryParse(teile[1], out var stufe))
            erzeuger = (x, z, d) => new WohnhausBauen(stufe, x, z, d);
        else if (teile.Length == 2 && teile[0] == "werkstatt" && Enum.TryParse<Betriebsart>(teile[1], true, out var art))
            erzeuger = (x, z, d) => new WerkstattBauen(art, x, z, d);
        if (erzeuger is null)
        {
            Protokollieren($"--bauen={wahl}: gemeint ist 1, wohnhaus:<Stufe 0–4> oder werkstatt:<Betriebsart>.");
            return;
        }
        _bau.Waehlen(wahl, erzeuger);
        if (optionen.TryGetValue("bauort", out var ort))
        {
            var zahlen = ort.Split(',').Select(t => double.TryParse(t, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : double.NaN).ToArray();
            if (zahlen.Length >= 2 && !double.IsNaN(zahlen[0]) && !double.IsNaN(zahlen[1]))
            {
                _stadt.BauOrtSetzen(zahlen[0], zahlen[1]);
                if (zahlen.Length >= 3 && !double.IsNaN(zahlen[2])) _stadt.BauDrehung = ((int)zahlen[2] % 4 + 4) % 4;
            }
        }
        if (optionen.TryGetValue("bauklick", out var klick) && int.TryParse(klick, out var art2)) _bauAutoKlick = art2;
    }

    /// <summary>Nur für den Klicktest (--bauklick): Sobald der Umriss steht und grün ist, die Rückfrage öffnen oder gleich bauen.</summary>
    private void BauAutoKlickPruefen()
    {
        if (_bauAutoKlick == 0 || _stadt.BauAuftrag is not { } befehl || _simulation.Wirtschaft is not { } w || _spieler is null) return;
        var modus = _bauAutoKlick;
        _bauAutoKlick = 0;
        if (_stadt.BauGrund is { } grund) { Protokollieren($"--bauklick: Hier kann nicht gebaut werden – {grund}"); return; }
        if (modus == 1)
        {
            _stadt.BauFest = true;
            _bauFrage.OeffnenNeubau(befehl);
            return;
        }
        var ergebnis = w.Ausfuehren(_spieler, befehl);
        Protokollieren(ergebnis.Erfolg ? ergebnis.Meldung : "✗ " + ergebnis.Meldung);
        if (!ergebnis.Erfolg) return;
        _bau.Schliessen();
        if (w.Bauen.Grundstuecke.LastOrDefault(g => g.Besitzer == _spieler && g.Arbeit == Bauarbeit.Neubau) is { } baustelle)
            _auswahlKennung = Stadtansicht.BaustellenKennung(baustelle);
    }

    /// <summary>Zwei Knöpfe unter dem Gebäudefenster, wenn ein leeres Wohnhaus gewählt ist: „Kaufen" und „Abreißen" (je mit Rückfrage).</summary>
    private void LeerstandKnoepfeBauen()
    {
        var spalte = _aktionKnopf.GetParent();
        var stelle = _aktionKnopf.GetIndex() + 1;
        Button Neu(bool abreissen)
        {
            var knopf = new Button { FocusMode = Control.FocusModeEnum.None, Visible = false };
            knopf.Pressed += () =>
            {
                if (_simulation.Wirtschaft is not { } w || LeerstandKonto(w) is not { } leer) return;
                _leerstandZiel = w.Bauen.Leerstand(leer);
                TafelnSchliessen();
                _bauFrage.OeffnenLeerstand(leer, abreissen);
            };
            spalte.AddChild(knopf);
            spalte.MoveChild(knopf, stelle + (abreissen ? 1 : 0));
            return knopf;
        }
        _leerstandKaufen = Neu(false);
        _leerstandAbreissen = Neu(true);
    }

    /// <summary>Das leere Wohnhaus, das gerade ausgewählt ist (null: die Auswahl ist keines).</summary>
    private Konto? LeerstandKonto(Stadtwirtschaft w) =>
        _auswahlKennung is { } kennung && kennung.StartsWith("wohnhaus:")
            ? w.Konten.FirstOrDefault(k => Stadtansicht.WohnhausKennung(k) == kennung && w.Bauen.Leerstand(k) is not null)
            : null;

    /// <summary>Preise für Kauf und Abriss unter dem Text eines leeren Hauses – dieselbe Rechnung wie in der Rückfrage.</summary>
    private string LeerstandZusatz(Stadtwirtschaft w)
    {
        if (_spieler is null || LeerstandKonto(w) is not { } leer) return "";
        var v = w.Bauen.LeerstandVorschau(_spieler, leer);
        var waehrung = w.Daten.Allgemein.Waehrung;
        var zeilen = new List<string>
        {
            "",
            $"Kaufen: {Zahlenformat.Ganz(v.KaufSumme)} {waehrung} (Kauf {Zahlenformat.Ganz(v.Kaufpreis)} + Sanierung {Zahlenformat.Ganz(v.Sanierung)}{(v.Anrechnung > 0.5 ? $" − euer Haus {Zahlenformat.Ganz(v.Anrechnung)}" : "")}), "
                + $"{BauAnzeige.BauzeitText(v.SanierungMinuten)}, dann zieht ihr ein.",
            $"Abreißen: {Zahlenformat.Ganz(v.AbrissSumme)} {waehrung} (Kauf {Zahlenformat.Ganz(v.Kaufpreis)} + Abriss {Zahlenformat.Ganz(v.Abriss)}), "
                + $"{BauAnzeige.BauzeitText(v.AbrissMinuten)}, danach freier Bauplatz.",
        };
        if (v.KaufVerboten is { } kauf) zeilen.Add($"⚠ Kaufen: {kauf}");
        if (v.AbrissVerboten is { } abriss) zeilen.Add($"⚠ Abreißen: {abriss}");
        return "\n" + string.Join("\n", zeilen);
    }

    /// <summary>Zeigt die Knöpfe „Kaufen" und „Abreißen" nur bei einem leeren Wohnhaus; gesperrt mit Grund im Tooltip, wenn es nicht geht.</summary>
    private void LeerstandKnoepfeAnzeigen(Stadtwirtschaft w)
    {
        // Nur bei Änderung zuweisen (wie beim Aktionsknopf: sonst geht ein Klick zwischen zwei Bildern verloren)
        var leer = _spieler is null ? null : LeerstandKonto(w);
        var sichtbar = leer is not null && _stadt.RaumKennung is null;
        if (_leerstandKaufen.Visible != sichtbar) _leerstandKaufen.Visible = sichtbar;
        if (_leerstandAbreissen.Visible != sichtbar) _leerstandAbreissen.Visible = sichtbar;
        if (!sichtbar || leer is null) return;
        var v = w.Bauen.LeerstandVorschau(_spieler!, leer);
        var waehrung = w.Daten.Allgemein.Waehrung;
        void Setzen(Button knopf, string text, string? grund)
        {
            var gesperrt = grund is not null || !_spieler!.IstAktiv;
            if (knopf.Disabled != gesperrt) knopf.Disabled = gesperrt;
            if (knopf.Text != text) knopf.Text = text;
            if (knopf.TooltipText != (grund ?? "")) knopf.TooltipText = grund ?? "";
        }
        Setzen(_leerstandKaufen, $"Kaufen und sanieren ({Zahlenformat.Ganz(v.KaufSumme)} {waehrung})", v.KaufVerboten);
        Setzen(_leerstandAbreissen, $"Abreißen lassen ({Zahlenformat.Ganz(v.AbrissSumme)} {waehrung})", v.AbrissVerboten);
    }

    /// <summary>Ein freier Platz: was er kostet, ab welchem Stand und was er voraussichtlich einbringt.</summary>
    private string LiegenschaftText(LiegenschaftDaten l, Stadtwirtschaft w)
    {
        var waehrung = w.Daten.Allgemein.Waehrung;
        var d = w.Daten.Art(l.Art);
        var art = $"{d.Name} ({(d.OhneProduktion ? UiHilfe.Taetigkeit(d.Beruf) : string.Join(", ", d.IstWerkstatt ? d.Produkte : d.Rohstoffe))})";
        var zeilen = new List<string>
        {
            $"{l.Name} – {(l.IstPacht ? "zu verpachten" : "zu verkaufen")}",
            "",
            l.Bebaut ? $"{art}, Stufe {d.Stufe}, mit {w.Daten.Plaetze(l.Art)} Arbeitsplätzen – das Haus steht schon" : $"{art} mit {w.Daten.Plaetze(l.Art)} Arbeitsplätzen",
            l.IstPacht
                ? $"Pacht: {Zahlenformat.Ganz(w.Daten.Fixkosten(l.Art))} {waehrung} im Jahr"
                : $"Kaufpreis: {Zahlenformat.Ganz(l.Kaufpreis)} {waehrung}, Unterhalt {Zahlenformat.Ganz(w.Daten.Fixkosten(l.Art))} im Jahr",
            $"Zu haben ab dem Stand: {l.MindestStand}" + (l.MindestGrad > 1 ? $" und dem {l.MindestGrad}. Meistergrad ({d.Beruf})" : ""),
            _spieler is { Berufe.Count: > 0 } sp && w.Daten.Berufe.Count > 0 && !sp.Beherrscht(d.Beruf)
                ? $"Handwerk: {d.Beruf} – beherrscht ihr nicht; erst im Kontor erlernen"
                : $"Handwerk: {d.Beruf}",
            $"Erwarteter Gewinn bei voller Belegung: ≈ {Zahlenformat.MitVorzeichen(Kalkulation.ErwarteterJahresgewinn(w, l.Art))} {waehrung} im Jahr (zu heutigen Preisen)",
        };
        if (_spieler is { IstAktiv: true } && w.LiegenschaftErwerbVerboten(_spieler, l.Name) is { } grund)
            zeilen.Add($"⚠ {grund}");
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
        var (sichtbar, gesperrt, text, tipp, aktion) = AktionBestimmen(w, frei);
        _aktion = aktion;
        _aktionKnopf.Visible = sichtbar;
        if (!sichtbar) return;
        _aktionKnopf.Disabled = gesperrt;
        if (_aktionKnopf.Text != text) _aktionKnopf.Text = text;
        if (_aktionKnopf.TooltipText != tipp) _aktionKnopf.TooltipText = tipp;
    }

    private (bool Sichtbar, bool Gesperrt, string Text, string Tipp, Func<Befehl>? Aktion) AktionBestimmen(Stadtwirtschaft w, LiegenschaftDaten? frei)
    {
        if (_spieler is null) return (false, true, "", "", null);
        var waehrung = w.Daten.Allgemein.Waehrung;

        if (frei is not null)
        {
            // Stand, Handwerk, Meistergrad und Geld prüft der Kern; den Grund zeigt der Text darüber (⚠) und der Tooltip.
            var grund = w.LiegenschaftErwerbVerboten(_spieler, frei.Name);
            var kosten = frei.Kaufpreis + w.Daten.Fixkosten(frei.Art);
            var text = _spieler.Vermoegen < kosten ? $"Es fehlen {Zahlenformat.Ganz(kosten - _spieler.Vermoegen)} {waehrung}"
                : frei.IstPacht ? $"Pachten ({Zahlenformat.Ganz(w.Daten.Fixkosten(frei.Art))} {waehrung} im Jahr)"
                : $"Kaufen ({Zahlenformat.Ganz(frei.Kaufpreis)} {waehrung})";
            return (true, grund is not null || !_spieler.IstAktiv, text, grund ?? "", () => new LiegenschaftErwerben(frei.Name));
        }

        if (_auswahlKennung != "Rathaus" || w.NaechsterStand(_spieler) is not { } naechster)
            return (false, true, "", "", null);
        var reicht = _spieler.Reinvermoegen >= naechster.Schwelle;
        var ohneHaus = w.Wohnen.StandVerboten(_spieler, _spieler.Stand + 1); // Wohnhaus fehlt (PR #75)
        var standText = !reicht
            ? $"{naechster.Name} ab {Zahlenformat.Ganz(naechster.Schwelle)} {waehrung} – es fehlen {Zahlenformat.Ganz(naechster.Schwelle - _spieler.Reinvermoegen)}"
            : ohneHaus ?? $"Aufsteigen zum {naechster.Name} ({Zahlenformat.Ganz(naechster.Gebuehr)} {waehrung})";
        return (true, !reicht || ohneHaus is not null || !_spieler.IstAktiv, standText, "", () => new StandErwerben());
    }

    /// <summary>Die Tafel oben links macht Platz, solange unten links eine Auswahl offen ist.</summary>
    private void PlatzMachen()
    {
        _hinweis.Visible = !_auswahl.Visible && _stadt.RaumKennung is null;
        var kontorSichtbar = _stadt.RaumKennung is null or Stadtansicht.RaumKontor && !_bau.Aktiv; // im Baumodus steht rechts die Bautafel
        if (_kontor.Visible != kontorSichtbar) _kontor.Visible = kontorSichtbar;
    }

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
            $"Besitzer: {b.Konto.Inhaber}" + (b.InDerStadt && b.Konto.Meistergrad(b.Artdaten.Beruf) > 0
                ? $" · {b.Artdaten.Beruf} im {b.Konto.Meistergrad(b.Artdaten.Beruf)}. Grad · Stufe {b.Stufe}: {b.Artdaten.Name}" : ""),
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
        // Ein Befehl meldet sein Ergebnis, die Stadt dieselbe Nachricht für alle – nur einmal zeigen.
        if (_protokoll.Count > 0 && _protokoll[0] == meldung) return;
        _protokoll.Insert(0, meldung);
        if (_protokoll.Count > MaxProtokollZeilen)
            _protokoll.RemoveAt(_protokoll.Count - 1);
        _protokollLabel.Text = string.Join("\n", _protokoll);
    }
}
