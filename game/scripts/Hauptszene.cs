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
/// und der Oberfläche (PR #197): oben Kopfzeile und Zeitstrahl (Hauptszene.Kopf.cs), unten die Betriebsleiste (Hauptszene.Leiste.cs),
/// unten links die Meldungen, die Hilfe (Hauptszene.Meldungen.cs), rechts eine Tafel nur, wenn man etwas anklickt (Auswahl, Wohnhaus, Bericht).
///
/// Aufgabenteilung:
///   • Die SIMULATION (Reichsgilde.Core) entscheidet, was passiert.
///   • Stadtansicht und Oberfläche ZEIGEN es nur an und leiten Eingaben weiter.
/// Diese Trennung ist die wichtigste Architekturregel des Projekts.
///
/// Startoptionen zum Testen (nach "--" an Godot anhängen):
///   --startstunde=11   Partie beginnt um 11 Uhr statt um 04 Uhr (Zunftglocke)
///   --tempo=8          Anfangstempo
///   --auswahl=Dom      Gebäude vorauswählen (Kennung wie beim Anklicken)
///   --drehung=1        Kamera um 1–3 Vierteldrehungen gedreht starten (0 = Blick nach Nordosten)
///   --blick=0,0,120    Kamera auf einen Punkt der Stadt (x,z in Metern) und optional auf eine Zoomstufe stellen
///   --ui=0             Tafeln und Fenster ausblenden (freie Sicht für Bildschirmfotos)
///   --aufsteigen=1     das Wohnhaus zum Bürgerhaus ausbauen und zum Bürger aufsteigen (nötig, um Werkstätten zu bauen; mit --vermoegen=30000)
///   --spulen=1140      so viele Spielminuten auf einen Schlag ablaufen lassen (1140 = ein Spieljahr; nach einem --bauklick)
///   --speichern=x.json die Partie nach --bauklick und --spulen in den Spielstand-Ordner speichern (für Tests mit --laden)
///   --bauen=1          den Baumodus öffnen (=wohnhaus:2 oder =werkstatt:Tischlerei wählt gleich das Gebäude)
///   --bauort=-96,-3,0  mit --bauen=<Gebäude>: der Umriss sitzt an dieser Stelle (x,z in Metern, Drehung 0–3), bis die Maus bewegt wird
///   --bauklick=1       mit --bauort: die Rückfrage öffnen (=2: gleich bauen; auch bei --leerstand: gleich kaufen bzw. abreißen)
///   --leerstand=kaufen mit --auswahl=wohnhaus:<Inhaber>: die Rückfrage zum Kauf des leeren Hauses öffnen (=abreissen: zum Abriss; Leerzeichen als _)
///   --kischmied=alle   Passanten mit dem KI-Schmied aus TRELLIS: aus, wenige (Standard: Knechte und Tagelöhner, jeder zweite) oder alle Männer
///   --innen=1          direkt in der Innenansicht der eigenen Werkstatt starten (--innen=Rathaus, --innen=Dom bzw. --innen=wohnhaus:<Inhaber>: dort)
///   --raum=2           mit --innen: gleich im zweiten Raum der Raumleiste (Werkstatt: 1 Werkraum, 2 Lager; Wohnhaus: 1 Wohnstube, 2 Kontor)
///   --innenblick=L1    mit --innen: gleich auf diesem Blickpunkt des Raums stehen (0 = Mitte, L1, L2, R1, R2, O1, U1 – was das Modell hat)
///   --tafel=personal   mit --innen: gleich eine Tafel der Bedienung öffnen (personal, produktion, lager, karren, einrichten, wache, bank)
///   --baum=wohnhaus    den Talentbaum der Einrichtung gleich öffnen, von außen (=wohnhaus oder =betrieb für die eigene Werkstatt; mit --vermoegen=5000)
///   --arbeiter=1,2     mit --innen: diese Arbeiter im Raum auswählen (Nummern wie im Personalbuch)
///   --einrichtung=Werkzeug1,Regale  der eigenen Werkstatt gleich diese Einrichtung kaufen (mit --vermoegen=5000)
///   --einstellen=Lehrling,Geselle   der eigenen Werkstatt gleich diese Arbeiter einstellen
///   --ausbauen=2       die eigene Werkstatt gleich zweimal ausbauen und den Umbau abwarten (Stufe 3; mit --vermoegen=30000)
///   --verwalten=1      die Kurzübersicht der eigenen Werkstatt gleich öffnen (=mehr: mit „…“ aufgeklappt)
///   --wohnhaus=haus    die Tafel des Wohnhauses gleich öffnen (Reiter haus, familie, handwerk, geld, hinterzimmer)
///   --hilfe=1          gleich zu Beginn die Hilfe zeigen
///   --lexikon=1        das Lexikon öffnen (=<kennung>: gleich bei diesem Begriff, =suche:<text>: mit diesem Suchtext); --lexikon-pruefen=1 prüft die Datei und beendet das Spiel
///   --meldung=⚠_Text   eine Meldung unten links einstellen (zum Testen; Leerzeichen als _, mit ✗ oder ⚠ eine Warnung)
///   --karren=Ochsenkarren,Kriegskarren  der eigenen Gießerei gleich diese Karren dazukaufen (mit --vermoegen=5000; Handkarren, Ochsenkarren, Kriegskarren)
///   --schiff=1         am ersten Hafen gleich so viele Flussschiffe kaufen (mit --vermoegen=10000)
///   --schiffsauftrag=vorschlag  jedem eigenen Schiff am Kai den Vorschlag geben (=<Ziel>: den Vorschlag für diese ferne Stadt, z. B. Eisenfurt)
///   --hafen=1          die Hafentafel des ersten Hafens öffnen (=<Kennung> eines Hafens der Karte, =staedte: auf dem Reiter „Ferne Städte“)
///   --vermoegen=12000  Startvermögen des Spielers setzen (zum Ausprobieren des Aufstiegs)
///   --beruf=Steinmetz  Startberuf der Testpartie (Schmied, Tischler, Steinmetz, Alchimist, Wirt, Prediger, Dieb, Gardist, Geldleiher)
///   --bericht=1        gleich zu Beginn einen Jahresbericht zeigen (zum Testen der Anzeige); =jahresrechnung, rivalen, umsaetze, protokoll: die Tafel „Bericht“ auf diesem Reiter
///   --rat=1            gleich zu Beginn die Ratskammer öffnen
///   --amt=Kämmerer     dem Spieler ein Amt geben; --kasse=9000 Stand der Stadtkasse; --antrag=steuer:4|bau:brunnen|fest Antrag stellen;
///   --rathaus=amt      das Rathaus mit diesem Reiter auswählen (stadtkasse, rat, amt) – alle vier in Hauptszene.Rat.cs
///   --sitzung=1        eine Ratssitzung mit dem Spieler im Rat vorbereiten und bis zum Jahresende vorspulen (=letzte: ohne Halt, danach das Rathaus mit dem Knopf
///                      „Letzte Ratssitzung ansehen“); --sitzungzeigen=<immer|betroffen|nie> nur für diesen Start – beide in Hauptszene.Ratssitzung.cs
///   --familie=1        gleich zu Beginn die Familientafel öffnen
///   --gericht=1        gleich zu Beginn den Gerichtssaal öffnen
///   --hinterzimmer=1   gleich zu Beginn das Hinterzimmer öffnen
///   --markt=1          gleich zu Beginn das Marktfenster öffnen
///   --verkaufen=1      gleich zu Beginn die Rückfrage zum Verkauf der eigenen Werkstatt öffnen (=2: vorher eine freie
///                      Liegenschaft dazukaufen und bis Mittag laufen lassen, damit der Verkauf erlaubt ist und Ware im Lager liegt)
///   --schwierigkeit=<Normal|Schwer>  Schwierigkeit der neuen Partie nur für diesen Start (Normal: KI-Häuser starten wie der Spieler, Schwer: mit Vorsprung)
///   --einstellungen=1  gleich zu Beginn das Fenster „Einstellungen“ öffnen
///   --kanten=<Art>     Kantenglättung nur für diesen Start (Aus, Fxaa, Smaa, Taa, Msaa2, Msaa4, Msaa8)
///   --schatten=<Stufe> Schattenqualität nur für diesen Start (Niedrig, Mittel, Hoch)
///   --bloom=<An|Aus>   Bloom nur für diesen Start
///   --bloomstaerke=<0–100> Stärke des Bloom in Prozent, nur für diesen Start
///   --ssao=<Stufe>     Umgebungsverdeckung nur für diesen Start (Aus, Niedrig, Mittel, Hoch)
///   --ssil=<An|Aus>    indirektes Licht (SSIL) nur für diesen Start
///   --fsr=<An|Aus>     FSR 2.2 (Hochskalierung) nur für diesen Start
///   --renderaufloesung=<25–100> Renderauflösung in Prozent bei FSR, nur für diesen Start
///   --voreinstellung=<Stufe> alle Grafikregler einer Voreinstellung (Niedrig, Mittel, Hoch, Ultra), nur für diesen Start; einzelne Optionen danach überschreiben sie
///   --bildrate=<Zahl>  Bildrate begrenzen (0 = unbegrenzt), --vsync=<Aus|An|Adaptiv>, --dynamisch=<Ziel-Bildrate|0> (dynamische Auflösung)
///   --natur=<25–100>   Natur-Dichte, --passanten=<25–100> Menge der Passanten, --feinheiten=<Nah|Mittel|Weit> Sichtweite der Feinheiten
///   --boden=<Niedrig|Mittel|Hoch> Bodenqualität (Mischung der Bodentexturen, Normalen, Grasbüschel)
///   --tiltshift=<Aus|Leicht|Stark> Miniatur-Unschärfe, --wolken=<An|Aus> Wolkenschatten, --dunst=<Aus|Niedrig|Hoch> Morgendunst
///   --tonemapping=<Bisher|Agx>, --helligkeit=, --kontrast=, --saettigung=<Prozent> Farbstimmung, --schaerfe=<0–100> Nachschärfen, --fahnen=<An|Aus>
///   --uigroesse=<75–200> Größe der Oberfläche in Prozent, --farbfilter=<Aus|Protanopie|Deuteranopie|Tritanopie>
///   (alle Grafikoptionen gelten nur für diesen Start und ändern die gespeicherte Wahl nicht)
///   --reiter=<Name>    mit --einstellungen=1: diesen Reiter zeigen (Voreinstellung, Leistung, Bild, Stimmung, Barrierefreiheit, Spiel)
///   --automatisch=1    die Voreinstellung wie beim ersten Start automatisch wählen (misst ein paar Sekunden, speichert das Ergebnis)
///   --messung=<Sekunden> nach 8 Sekunden Vorlauf so lange Bildzeiten messen, je Sekunde und am Ende ins Protokoll schreiben, dann beenden
///   --laden=datei.json einen Spielstand aus dem Spielstand-Ordner laden
/// </summary>
public partial class Hauptszene : Node
{
    public const string WirtschaftsdatenPfad = "res://daten/wirtschaft.json";

    private Simulation _simulation = null!;
    private readonly List<Button> _tempoKnoepfe = new();
    private string? _auswahlKennung;

    private Stadtansicht _stadt = null!;
    private WohnhausAnzeige _wohnhaus = null!;
    private BerichtAnzeige _berichtTafel = null!;
    private LexikonAnzeige _lexikon = null!;
    private HafenAnzeige _hafenTafel = null!;
    /// <summary>Den Jahresbericht hat der Spieler selbst geöffnet (Tafel „Bericht“): Beim Schließen bleibt die Pause, wie sie vorher war.</summary>
    private bool? _pauseVorBericht;
    private PanelContainer _auswahl = null!;
    private Fensterrahmen _auswahlRahmen = null!;
    private ulong _auswahlKlickBild = ulong.MaxValue; // Bild, in dem ein Klick in die Welt die Auswahl an die Maus gesetzt hat
    private Label _auswahlText = null!;
    private WappenBild _auswahlWappen = null!;
    private Button _innenKnopf = null!;
    private BetriebsUebersicht _uebersicht = null!;
    /// <summary>Ein Knopf für die Hauptaktion des ausgewählten Ortes (Aufsteigen, Kaufen, Pachten).</summary>
    private Button _aktionKnopf = null!;
    private Func<Befehl>? _aktion;
    private JahresberichtAnzeige _jahresbericht = null!;
    private RatAnzeige _ratskammer = null!;
    private RathausTafel _rathausTafel = null!;
    private FamilienAnzeige _familientafel = null!;
    private CharakterAnzeige _charakter = null!;
    private GerichtAnzeige _gerichtssaal = null!;
    private GaunerAnzeige _hinterzimmer = null!;
    private MarktAnzeige _markt = null!;
    private PersonalAnzeige _personalTafel = null!;
    private ProduktionAnzeige _produktionTafel = null!;
    private LagerAnzeige _lagerTafel = null!;
    private KarrenAnzeige _karrenTafel = null!;
    private EinrichtenAnzeige _einrichtenTafel = null!;
    private WohnhausBaumAnzeige _wohnhausBaum = null!;
    private WachplanAnzeige _wachplanTafel = null!;
    private BankAnzeige _bankTafel = null!;
    /// <summary>Die Tafeln der Bedienung im Innenraum (rechts im Bild, damit der Raum frei bleibt).</summary>
    private InnenTafel[] _innenTafeln = null!;
    private VerkaufAnzeige _verkauf = null!;
    private BauAnzeige _bau = null!;
    private WegAnzeige _wegTafel = null!;
    private BauRueckfrage _bauFrage = null!;
    private Button _leerstandKaufen = null!;
    private Button _leerstandAbreissen = null!;
    /// <summary>Das leere Wohnhaus, zu dem gerade die Rückfrage offen ist – nach dem Kauf zeigt die Auswahl seine Baustelle.</summary>
    private Grundstueck? _leerstandZiel;
    /// <summary>Startoption --bauklick: 1 = Rückfrage öffnen, 2 = gleich bauen – sobald der Umriss steht.</summary>
    private int _bauAutoKlick;
    /// <summary>Startoption --spulen: So viele Spielminuten (Ticks) auf einen Schlag ablaufen lassen, sobald nichts mehr zu klicken bleibt.</summary>
    private int _spulen;
    private string? _charakterWartet;
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
    private Betrieb? _eigeneGiesserei;
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
        // Oben, unten links und die Leiste zuerst: Schon beim Aufbau der Partie kommen Meldungen.
        KopfBauen();
        MeldungenBauen();
        LeisteBauen();
        _auswahl = GetNode<PanelContainer>("%Auswahl");
        _auswahlText = GetNode<Label>("%AuswahlText");
        AuswahlWappenEinbauen();
        _innenKnopf = GetNode<Button>("%InnenKnopf");
        _uebersicht = new BetriebsUebersicht();
        _auswahlText.GetParent().GetParent().AddChild(_uebersicht); // gleich unter Name und Wappen, vor „Hineingehen“
        _auswahlText.GetParent().GetParent().MoveChild(_uebersicht, _auswahlText.GetParent().GetIndex() + 1);
        _rathausTafel = new RathausTafel(); // Stadtkasse, Tagesordnung, Befugnis des Amtes – unter dem Text des Rathauses (PR #200)
        _auswahlText.GetParent().GetParent().AddChild(_rathausTafel);
        _auswahlText.GetParent().GetParent().MoveChild(_rathausTafel, _uebersicht.GetIndex() + 1);
        _aktionKnopf = GetNode<Button>("%AktionKnopf");
        _jahresbericht = GetNode<JahresberichtAnzeige>("%Jahresbericht");
        _ratKnopf = GetNode<Button>("%RatKnopf");
        _ratskammer = new RatAnzeige();
        var ratMitte = Hauptmenue.Mitte();
        GetNode<CanvasLayer>("Oberflaeche").AddChild(ratMitte);
        ratMitte.AddChild(_ratskammer);
        _familientafel = new FamilienAnzeige();
        ratMitte.AddChild(_familientafel); // gleiche Mitte: es ist immer nur eine Tafel offen
        _charakter = new CharakterAnzeige();
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_charakter); // frei verschiebbar, über den Tafeln: Klick auf ein Porträt (PR #227)
        PortraetAnzeige.PersonOeffnen = _charakter.Oeffnen;
        PortraetAnzeige.RatsherrOeffnen = _charakter.Oeffnen;
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
        _wegTafel = new WegAnzeige();
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_wegTafel); // ebenfalls rechts: Belag und Ausbau eines angeklickten Weges
        _wohnhaus = new WohnhausAnzeige();
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_wohnhaus); // rechts: das eigene Wohnhaus mit seinen Reitern
        _berichtTafel = new BerichtAnzeige { Meldungen = () => _jahresProtokoll };
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_berichtTafel); // rechts: Jahresrechnung, Rivalen, Umsätze, Protokoll
        _hafenTafel = new HafenAnzeige();
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_hafenTafel); // rechts: Schiffe und ferne Städte eines Hafens (PR #199)
        GetNode<CanvasLayer>("Oberflaeche").MoveChild(ratMitte, -1); // die Fenster der Mitte (Rat, Markt …) liegen über den Tafeln rechts
        _gerichtKnopf = GetNode<Button>("%GerichtKnopf");
        // Der Bericht sitzt in einem CenterContainer: Der hält ihn mittig, egal wie hoch er wird.
        var berichtMitte = Hauptmenue.Mitte();
        GetNode<CanvasLayer>("Oberflaeche").AddChild(berichtMitte);
        _jahresbericht.Reparent(berichtMitte);

        // Die Auswahl steht rechts, unter dem Zeitstrahl (deckend: Beschriftungen der Stadt sollen nicht durchscheinen).
        // Passt sie nicht ins Fenster, wird sie gerollt (Fenstergröße ist frei wählbar).
        _auswahl.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.15f, 0.13f, 0.97f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
        });
        Rollbereich.Einhuellen(_auswahl);
        Rollbereich.RechtsAnheften(_auswahl);
        _auswahlRahmen = Fensterrahmen.Anmelden(_auswahl, "Auswahl", AuswahlSchliessen);
        _auswahlRahmen.Mittig = false; // Ausnahme: Sie erscheint an der Maus (oder rechts im Raum), nicht in der Mitte
        _auswahlRahmen.FuerEsc = false; // Esc schließt sie erst nach der Innenansicht (siehe _UnhandledInput)
        RaumleisteBauen();
        HilfeBauen();
        _lexikon = new LexikonAnzeige(); // das Lexikon in der Bildmitte (Taste L, Knopf in der Betriebsleiste)
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_lexikon);
        Tooltips.BegriffSuchen = Lexikon.FindeMitVerweisen; // Verweise in Tooltips zeigen die Begriffe des Lexikons, beliebig tief
        Tooltips.LexikonOeffnen = Lexikon.Oeffnen;
        Tooltips.VerweiseErgaenzen = (text, eigene) => Lexikon.VerweiseEinsetzen(text, eigene); // auch normale Tooltips verlinken Lexikon-Begriffe
        LeerstandKnoepfeBauen();

        Ladezeit.Marke("Oberfläche: Tafeln bauen");
        RatssitzungBauen(); // die Tafel der sichtbaren Ratssitzung (Hauptszene.Ratssitzung.cs) – vor der Partie, die sich mit ihr verbindet
        var optionen = Startoptionen();
        if (optionen.GetValueOrDefault("lexikon-pruefen") == "1")
        {
            GetTree().Quit(Lexikon.PruefenUndMelden() ? 0 : 1); // nur die Datei prüfen (Test), kein Spiel
            return;
        }
        _simulation = SimulationAufbauen(optionen);
        EntwicklerSperreSetzen(); // Hauptszene.Entwickler.cs
        SpielmenueBauen();
        _ratKnopf.Pressed += RatOeffnen;
        _ratskammer.Gemeldet += Protokollieren;
        _charakter.Gemeldet += Protokollieren;
        _rathausTafel.Gemeldet += Protokollieren;
        _rathausTafel.RatGewuenscht += RatOeffnen;
        _rathausTafel.GerichtGewuenscht += () => { TafelnSchliessen(); _gerichtssaal.Oeffnen(); };
        _rathausTafel.MarktGewuenscht += MarktOeffnen;
        _wohnhaus.FamilieGewuenscht += FamilieOeffnen;
        _familientafel.Gemeldet += Protokollieren;
        _gerichtKnopf.Pressed += () => { TafelnSchliessen(); _gerichtssaal.Oeffnen(); };
        _gerichtssaal.Gemeldet += Protokollieren;
        _wohnhaus.HinterzimmerGewuenscht += HinterzimmerOeffnen;
        _wohnhaus.Geschlossen += () => { if (_auswahlKennung == EigenesWohnhaus) _auswahlKennung = null; };
        _hinterzimmer.Gemeldet += Protokollieren;
        _berichtTafel.JahresberichtGewuenscht += () =>
        {
            if (_spieler is null || _spieler.Vorjahr is null) return;
            _pauseVorBericht ??= _simulation.Takt.IstPausiert;
            _simulation.Takt.IstPausiert = true;
            _jahresbericht.Zeigen(_simulation, _spieler);
        };
        _bau.Geschlossen += () => { _bauFrage.Hide(); _wegTafel.Schliessen(); AuswahlAnzeigen(); }; // mit dem Baumenü endet auch der Wege-Modus
        _wegTafel.Gemeldet += Protokollieren;
        _stadt.WegAngeklickt += (weg, stelle) =>
        {
            // kommt nur im Wege-Modus des Baumenüs (Stadtansicht.Wegemodus.cs); die Bautafel bleibt rechts stehen, die Wegtafel links daneben
            if (_stadt.RaumKennung is not null) return;
            RechtsSchliessen();
            _wegTipp?.Hide();
            _wegTafel.Oeffnen(weg, stelle, _bau.Aktiv ? _bau.Size.X + 16 : 0);
        };
        WegeModusVerbinden();
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
            KlangOberflaeche.Spielen(KlangOberflaeche.Ereignis.Bauen);
            if (_simulation.Wirtschaft is not { } wirtschaft || _spieler is null) return;
            // Die Auswahl zeigt jetzt die Baustelle (Neubau) bzw. die des gekauften Leerstands
            var baustelle = neubau
                ? wirtschaft.Bauen.Grundstuecke.LastOrDefault(g => g.Besitzer == _spieler && g.Arbeit == Bauarbeit.Neubau)
                : _leerstandZiel;
            if (baustelle is not null) _auswahlKennung = Stadtansicht.BaustellenKennung(baustelle);
        };

        _pauseKnopf.SetMeta("klang", "still");
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
            knopf.SetMeta("klang", "still");
            knopf.Pressed += () => TempoSetzen(tempo);
            if (!Taktgeber.Tempostufen.Contains(tempo)) // TESTTEMPO: Teststufen orange und mit Hinweis
            {
                knopf.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.25f));
                knopf.TooltipText = "Nur zum Testen – wird vor der Veröffentlichung entfernt";
            }
            else knopf.TooltipText = $"Spielgeschwindigkeit {tempo}-fach (Zifferntasten wählen die Stufen, Leertaste pausiert)";
            _tempoleiste.AddChild(knopf);
            _tempoKnoepfe.Add(knopf);
        }
        if (optionen.TryGetValue("tempo", out var t) && double.TryParse(t, System.Globalization.CultureInfo.InvariantCulture, out var tempoWert))
            TempoSetzen(tempoWert);

        _simulation.PhaseBegonnen += phase =>
            Protokollieren($"{KalenderTexte.Name(phase)} beginnt."); // die Uhrzeit steht schon vor jeder Meldung
        _simulation.JahrBeendet += jahr =>
        {
            _jahresProtokoll.Clear(); // das Protokoll gilt für ein Jahr
            Protokollieren($"Jahresabrechnung {jahr} abgeschlossen. Es ist nun {_simulation.Zeit.DatumText}.");
            if (_ratskammer.Visible) _ratskammer.Aufbauen(); // neue Ämter, verblasste Gunst
            if (_familientafel.Visible) _familientafel.Aufbauen(); // ein Jahr älter, Geburten, Todesfälle
            if (_charakter.Visible) _charakter.Aufbauen();
            if (_gerichtssaal.Visible) _gerichtssaal.Aufbauen(); // Urteile und Spitzelberichte
            if (_hinterzimmer.Visible) _hinterzimmer.Aufbauen();
            if (_spieler is null || _simulation.IstBeendet) return;
            // Automatisch speichern – überschreibt jedes Jahr denselben Spielstand (nicht in einer Probepartie der Kartenwerkstatt).
            if (!_probepartie && Spielstaende.Speichern(_simulation, _spieler, Spielstaende.AutoDatei) is (null, var meldung)) Protokollieren(meldung);
            if (_sitzungsTafel.Laeuft)
            {
                _jahresberichtWartet = true; // erst nach dem Ergebnis des letzten Punktes (Hauptszene.Ratssitzung.cs)
                return;
            }
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
            else _simulation.Takt.IstPausiert = _pauseVorBericht ?? false; // selbst geöffnet: Pause wie vorher, nach dem Jahreswechsel: weiter
            _pauseVorBericht = null;
        };
        _aktionKnopf.Pressed += () =>
        {
            if (_spieler is null || _aktion is null) return;
            UiHilfe.Befehlen(_simulation, _spieler, _aktion(), this, ergebnis =>
            {
                if (!ergebnis.Erfolg) Protokollieren("✗ " + ergebnis.Meldung); // Erfolge meldet die Stadt selbst
                AuswahlAnzeigen();
            });
        };

        _stadt.GebaeudeAngeklickt += kennung =>
        {
            if (RaumAngeklickt(kennung)) return;
            if (HafenAngeklickt(kennung)) return; // Hafen, Kran, Schuppen oder eigenes Schiff: die Hafentafel (Hauptszene.Hafen.cs)
            RechtsSchliessen();
            _auswahlKennung = kennung;
            // Wie ein Tooltip: unten rechts neben dem Zeiger (am Rand nach links bzw. oben), bis der Spieler sie zieht
            var klickort = GetViewport().GetMousePosition();
            _auswahlRahmen.PlatzSetzen((groesse, sichtbar) => Tooltips.Lage(klickort, groesse, sichtbar));
            _auswahlKlickBild = Engine.GetProcessFrames();
            AuswahlAnzeigen();
            if (kennung == "Marktplatz") MarktOeffnen(); // wie im Original: ein Klick auf den Markt öffnet die Preistafel
        };
        _stadt.InnenansichtGewechselt += kennung =>
        {
            if (kennung is not null) _auswahlKennung = kennung;
            Protokollieren(kennung is null ? "Zurück in den Gassen der Stadt."
                : _stadt.HatBedienung ? $"Ihr betretet {_stadt.InnenName}. Gegenstände anklicken ({_stadt.InnenBedienungen}) · Maus an den Bildrand: Blick wechseln · Rechtsklick: hinaus."
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
        _uebersicht.Gemeldet += Protokollieren;
        _uebersicht.HandelGewuenscht += b => { TafelnSchliessen(); _markt.Oeffnen(b); };
        _uebersicht.HineinGewuenscht += BetriebBetreten;
        _uebersicht.EinrichtungGewuenscht += b => { TafelnSchliessen(); _einrichtenTafel.Oeffnen(b); };
        _wohnhaus.EinrichtungGewuenscht += () => { TafelnSchliessen(); _wohnhausBaum.Oeffnen(); };
        _wohnhausBaum.Gemeldet += Protokollieren;
        _markt.Gemeldet += Protokollieren;
        _uebersicht.VerkaufGewuenscht += b => { TafelnSchliessen(); _verkauf.Oeffnen(b); };
        _verkauf.Gemeldet += Protokollieren;
        _wohnhaus.Gemeldet += Protokollieren;
        _hafenTafel.Gemeldet += Protokollieren;
        if (optionen.TryGetValue("auswahl", out var vorauswahl) && vorauswahl.StartsWith("weg:"))
        {
            // --auswahl=weg:<nr>[,<stelle>]: die Wegtafel (Belag, Ausbau, Antrag) für einen Weg der Karte öffnen
            var teile = vorauswahl[4..].Split(',');
            if (int.TryParse(teile[0], out var wegNr) && _stadt.WegMitte(wegNr) is var (nr, mitte))
            {
                var stelle = teile.Length > 1 && double.TryParse(teile[1], System.Globalization.CultureInfo.InvariantCulture, out var st) ? st : mitte;
                _wegTafel.Oeffnen(nr, stelle);
                if (_stadt.WegOrt(nr, stelle) is { } ort)
                {
                    GD.Print($"Weg {nr} ({_stadt.WegName(nr)}), Stelle {stelle:0} m: x {ort.X:0.#}, z {ort.Y:0.#}");
                    if (!optionen.ContainsKey("blick")) _stadt.KameraSetzen(ort.X, ort.Y, 40f);
                }
            }
            else Protokollieren("--auswahl=" + vorauswahl + ": diesen Weg gibt es nicht.");
        }
        else if (optionen.TryGetValue("auswahl", out vorauswahl))
            _auswahlKennung = vorauswahl.Replace('_', ' '); // Leerzeichen in Namen als _ (--auswahl=wohnhaus:Haus_Eisenbart)
        if (optionen.TryGetValue("drehung", out var d) && int.TryParse(d, out var drehung))
            _stadt.Drehen(drehung, sofort: true);
        if (optionen.TryGetValue("blick", out var blick)) // --blick=x,z[,zoom]: Kamera auf einen Punkt der Stadt (Meter) stellen
        {
            var teile = blick.Split(',').Select(t => float.TryParse(t, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : float.NaN).ToArray();
            if (teile.Length >= 2 && !float.IsNaN(teile[0]) && !float.IsNaN(teile[1]))
                _stadt.KameraSetzen(teile[0], teile[1], teile.Length >= 3 && !float.IsNaN(teile[2]) ? teile[2] : null);
        }
        // --wetter=<klar|…|nebel>, --jahreszeit=<fruehling|…|winter>, --blitz=1: nur Darstellung, nur für diesen Start (Stadtansicht.Wetter.cs)
        if (optionen.ContainsKey("wetter") || optionen.ContainsKey("jahreszeit") || optionen.ContainsKey("blitz"))
            _stadt.WetterVorgeben(optionen.TryGetValue("wetter", out var wetterName) && Enum.TryParse<Wetterlage>(wetterName, true, out var lage) ? lage : null,
                                  optionen.TryGetValue("jahreszeit", out var jahreszeitName) && Enum.TryParse<Jahreszeit>(jahreszeitName, true, out var jz) ? jz : null,
                                  optionen.GetValueOrDefault("blitz") == "1");
        if (optionen.TryGetValue("wetterspaeter", out var spaeter) && spaeter.Split('@') is [var spaeterLage, var spaeterSekunden]
            && Enum.TryParse<Wetterlage>(spaeterLage, true, out var spaeterWetter) && double.TryParse(spaeterSekunden, System.Globalization.CultureInfo.InvariantCulture, out var sek))
            _stadt.WetterVorgebenNach(spaeterWetter, sek); // --wetterspaeter=regen@20: Wetter schlägt nach 20 s um (Stadtansicht.Wetter.cs)
        if (optionen.TryGetValue("pflasterrelief", out var relief)) _stadt.PflasterReliefVorgeben(relief != "0"); // Parallax im Pflaster (Stadtansicht.Pflasterrelief.cs)
        if (optionen.TryGetValue("kischmied", out var kiSchmied)) _stadt.KiSchmiedVorgeben(kiSchmied); // aus|wenige|alle (Stadtansicht.KiFiguren.cs)
        if (optionen.GetValueOrDefault("messe") == "1") _stadt.MesseVorgeben(); // Kirchenfenster wie zur Messe, nur Darstellung (Stadtansicht.Modelle.cs)
        if (optionen.TryGetValue("ui", out var ui) && ui == "0")
            GetNode<CanvasLayer>("Oberflaeche").Visible = false; // freie Sicht auf die Stadt für Bildschirmfotos
        if (optionen.TryGetValue("kartenbild", out var kartenbild)) KartenbildAufnehmen(kartenbild);
        if (optionen.TryGetValue("verwalten", out var verwalten) && verwalten is "1" or "mehr" && _eigeneGiesserei is not null)
        {
            BetriebAuswaehlen(_eigeneGiesserei, hinfahren: false);
            if (verwalten == "mehr") _uebersicht.MehrOffen = true;
        }
        if (optionen.TryGetValue("wohnhaus", out var wohnhausReiter))
        {
            if (!int.TryParse(wohnhausReiter, out _) && Enum.TryParse<WohnhausAnzeige.Reiter>(wohnhausReiter, true, out var reiter)) WohnhausOeffnen(reiter);
            else Protokollieren($"--wohnhaus={wohnhausReiter}: gemeint ist haus, familie, handwerk, geld oder hinterzimmer.");
        }
        if (optionen.TryGetValue("karren", out var karrenTypen) && _eigeneGiesserei is { } kaeufer && _simulation.Wirtschaft is { } wKarren && _spieler is not null)
            foreach (var typ in karrenTypen.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                _simulation.Bote.Einreihen(_spieler, new KarrenKaufen(kaeufer.Name, typ), ergebnis =>
                {
                    if (!ergebnis.Erfolg) Protokollieren("--karren=" + typ + ": " + ergebnis.Meldung);
                });
            }
        if (optionen.GetValueOrDefault("umland") == "1") UmlandVorbereiten();
        RatStartoptionen(optionen); // --amt, --kasse, --antrag, --rathaus (Hauptszene.Rat.cs); vor den Tafeln, die ein Amt zeigen
        HafenStartoptionen(optionen); // --schiff, --schiffsauftrag, --hafen (Hauptszene.Hafen.cs)
        if (optionen.TryGetValue("rat", out var rat) && rat == "1")
            _ratskammer.Oeffnen();
        if (optionen.TryGetValue("familie", out var familie) && familie == "1")
            _familientafel.Oeffnen();
        if (optionen.GetValueOrDefault("charakter-ehe") == "1") CharakterVerheiraten(); // sofort, damit die Kinder über --spulen heranwachsen
        if (optionen.TryGetValue("charakter", out var charakter)) _charakterWartet = charakter; // erst nach dem Vorspulen (--spulen)
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
        RatssitzungStartoptionen(optionen); // --sitzung, --sitzungzeigen (Hauptszene.Ratssitzung.cs); nach --amt und --aufsteigen (Bewerbung braucht den Stand Bürger)
        if (optionen.TryGetValue("einstellen", out var einstellen) && _eigeneGiesserei is { } einsteller && _simulation.Wirtschaft is { } wEin && _spieler is not null)
            foreach (var kraft in einstellen.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                _simulation.Bote.Einreihen(_spieler, new ArbeiterEinstellen(einsteller.Name, kraft), ergebnis =>
                {
                    if (!ergebnis.Erfolg) Protokollieren("--einstellen=" + kraft + ": " + ergebnis.Meldung);
                });
            }
        if (optionen.TryGetValue("einrichtung", out var einrichtung) && _eigeneGiesserei is { } kaeufer2 && _simulation.Wirtschaft is { } wEinr && _spieler is not null)
            foreach (var kennung in einrichtung.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                _simulation.Bote.Einreihen(_spieler, new EinrichtungKaufen(kaeufer2.Name, kennung), ergebnis =>
                {
                    if (!ergebnis.Erfolg) Protokollieren("--einrichtung=" + kennung + ": " + ergebnis.Meldung);
                });
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
                var ergebnis = _simulation.Bote.SofortAusfuehren(_spieler, befehl);
                Protokollieren(ergebnis.Erfolg ? ergebnis.Meldung : "✗ " + ergebnis.Meldung);
                if (ergebnis.Erfolg && _leerstandZiel is not null) _auswahlKennung = Stadtansicht.BaustellenKennung(_leerstandZiel);
            }
            else _bauFrage.OeffnenLeerstand(leerKonto, leerstand == "abreissen"); // mit --auswahl=wohnhaus:<Inhaber>, Leerzeichen als _
        }
        WegBefehleStarten(optionen);
        if (optionen.TryGetValue("spulen", out var spulen) && int.TryParse(spulen, out var ticks)) _spulen = ticks;
        if (optionen.TryGetValue("speichern", out var speichern)) _speichernAls = speichern;
        if (optionen.TryGetValue("klangtest", out var klangtest) && klangtest == "1") KlangOberflaeche.Testen(this); // spielt alle Oberflächenklänge, Ergebnis im Log
        Fenster.StartoptionenAnwenden(optionen); // Grafik nur für diesen Start: --kanten, --voreinstellung, --dunst … (Fenster.Grafik.cs)
        // Erst nach den Grafikoptionen: Das Fenster zeigt beim Öffnen die dann gültigen Werte.
        if (optionen.TryGetValue("einstellungen", out var einstellungen) && einstellungen == "1")
        {
            SpielmenueOeffnen();
            EinstellungenOeffnen();
            if (optionen.TryGetValue("reiter", out var reiter)) _einstellungen.ReiterZeigen(reiter); // --reiter=Stimmung
        }
        if (optionen.TryGetValue("bericht", out var bericht) && bericht == "1" && _spieler is not null)
            _jahresbericht.Zeigen(_simulation, _spieler);
        else if (bericht is not null && !int.TryParse(bericht, out _) && Enum.TryParse<BerichtAnzeige.Reiter>(bericht, true, out var berichtReiter))
            BerichtOeffnen(berichtReiter);
        if (optionen.TryGetValue("lexikon", out var lexikonWahl))
        {
            _lexikon.Oeffnen(lexikonWahl == "1" || lexikonWahl.StartsWith("suche:") ? "" : lexikonWahl);
            if (lexikonWahl.StartsWith("suche:")) _lexikon.Suchen(lexikonWahl[6..].Replace('_', ' '));
        }
        EntwicklerBauen(optionen); // Fenster nur mit dem Haken „Entwickler-Optionen“ (Hauptszene.Entwickler.cs)
        if (optionen.TryGetValue("meldung", out var testMeldung)) Protokollieren(testMeldung.Replace('_', ' '));
        // Die Hilfe: auf Wunsch (--hilfe=1) und beim allerersten Spiel einmal von selbst – nicht bei Startoptionen (Klicktests sollen gleiche Bilder liefern).
        if (optionen.GetValueOrDefault("hilfe") == "1" || (!Fenster.HilfeGesehen && OS.GetCmdlineUserArgs().Length == 0))
            HilfeOeffnen();
        if (optionen.TryGetValue("baum", out var baumWahl)) // Talentbaum der Einrichtung von außen: wohnhaus oder betrieb (eigene Werkstatt)
        {
            if (baumWahl == "wohnhaus") _wohnhausBaum.Oeffnen();
            else if (_eigeneGiesserei is { } baumBetrieb) _einrichtenTafel.Oeffnen(baumBetrieb);
        }
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
        Ladezeit.BilderMessen(GetTree(), "Partie"); // --ladezeit=1: Rest von _Ready, dann die ersten Bilder (Ladezeit.cs)
    }

    /// <summary>Es ist immer nur eine Tafel in der Bildmitte offen.</summary>
    private void TafelnSchliessen(bool auchFeste = false)
    {
        // Festgehaltene Fenster (Fensterrahmen) bleiben offen – so lassen sich mehrere Tafeln nebeneinander benutzen
        foreach (var tafel in new Control[] { _ratskammer, _familientafel, _charakter, _gerichtssaal, _hinterzimmer, _markt, _verkauf })
            Fensterrahmen.SchliessenFallsLose(tafel, auchFeste);
        _wegTafel.Schliessen();
        foreach (var tafel in _innenTafeln) tafel.Hide();
        _wohnhausBaum.Hide();
        if (_bauFrage.Visible) _bauFrage.Zurueck(); // der Umriss der Platzwahl wird wieder frei
    }

    /// <summary>Rechts steht immer nur eine Tafel: Auswahl, Wohnhaus, Bericht, Hafen, Bautafel oder Wegtafel. Schließt alle (der Baumodus endet dabei nicht).</summary>
    private void RechtsSchliessen(bool auchFeste = false)
    {
        if (_auswahl.Visible || _auswahlKennung is not null) AuswahlSchliessen();
        Fensterrahmen.SchliessenFallsLose(_wohnhaus, auchFeste);
        Fensterrahmen.SchliessenFallsLose(_berichtTafel, auchFeste);
        Fensterrahmen.SchliessenFallsLose(_hafenTafel, auchFeste);
        _wegTafel.Schliessen();
    }

    /// <summary>Steht rechts gerade die Bau- oder die Wegtafel? Dann wartet die Auswahl (sie bleibt gewählt und erscheint danach wieder).</summary>
    private bool RechtsFrei => !_bau.Aktiv && !_wegTafel.Aktiv;

    /// <summary>Kennung des eigenen Wohnhauses („wohnhaus:&lt;Inhaber&gt;“), sonst null.</summary>
    private string? EigenesWohnhaus => _spieler is null ? null : Stadtansicht.WohnhausKennung(_spieler);

    /// <summary>Die Tafel des eigenen Wohnhauses rechts, auf diesem Reiter (Klick auf das Wohnhaus oder die Kopfzeile, Startoption <c>--wohnhaus</c>).</summary>
    private void WohnhausOeffnen(WohnhausAnzeige.Reiter reiter)
    {
        if (_spieler is null || _simulation.Wirtschaft is null) return;
        if (_bau.Aktiv) _bau.Schliessen();
        RechtsSchliessen();
        _auswahlKennung = EigenesWohnhaus;
        _wohnhaus.Zeigen(reiter);
    }

    /// <summary>Der Bericht rechts (Knopf „Bericht“ der Leiste, Klick auf eine Meldung, Startoption <c>--bericht=&lt;reiter&gt;</c>).</summary>
    private void BerichtOeffnen(BerichtAnzeige.Reiter reiter)
    {
        if (_spieler is null) return;
        if (_bau.Aktiv) _bau.Schliessen();
        RechtsSchliessen();
        _berichtTafel.Zeigen(reiter);
    }

    private void RatOeffnen()
    {
        if (_simulation.Wirtschaft?.Rat.IstAktiv != true) return;
        TafelnSchliessen();
        _ratskammer.Oeffnen();
    }

    private void FamilieOeffnen()
    {
        TafelnSchliessen();
        _familientafel.Oeffnen();
    }

    private void HinterzimmerOeffnen()
    {
        TafelnSchliessen();
        _hinterzimmer.Oeffnen();
    }

    /// <summary>
    /// Baut die sieben Tafeln der Bedienung im Innenraum und hängt sie rechts ins Bild, mittig in der Höhe und mit Luft über der Raumleiste.
    /// </summary>
    private void InnenTafelnBauen()
    {
        _personalTafel = new PersonalAnzeige();
        _produktionTafel = new ProduktionAnzeige();
        _lagerTafel = new LagerAnzeige();
        _karrenTafel = new KarrenAnzeige();
        _einrichtenTafel = new EinrichtenAnzeige();
        _wohnhausBaum = new WohnhausBaumAnzeige();
        _wachplanTafel = new WachplanAnzeige();
        _bankTafel = new BankAnzeige();
        _innenTafeln = new InnenTafel[] { _personalTafel, _produktionTafel, _lagerTafel, _karrenTafel, _einrichtenTafel, _wachplanTafel, _bankTafel };
        var rand = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        rand.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        rand.AddThemeConstantOverride("margin_left", 16);
        rand.AddThemeConstantOverride("margin_right", 16);
        rand.AddThemeConstantOverride("margin_top", (int)Rollbereich.KopfHoehe); // unter Kopfzeile und Zeitstrahl
        rand.AddThemeConstantOverride("margin_bottom", 84);
        var reihe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End, MouseFilter = Control.MouseFilterEnum.Ignore };
        rand.AddChild(reihe);
        var alleTafeln = _innenTafeln.Select(t => t.Tafel).Append(_wohnhausBaum.Tafel);
        foreach (var tafel in alleTafeln)
        {
            tafel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            reihe.AddChild(tafel);
            if (tafel.GetNodeOrNull<Rollbereich>("Rollbereich") is { } rolle)
            {
                rolle.ObenFrei = Rollbereich.KopfHoehe - rolle.Abstand; // die Tafel rollt, bevor sie an den Rand stößt
                rolle.UntenFrei = 84 - rolle.Abstand;
            }
        }
        GetNode<CanvasLayer>("Oberflaeche").AddChild(rand);
    }

    /// <summary>Öffnet die Tafel zu einem Gegenstand im Raum (Personal, Produktion, Lager, Karren, Einrichten, Wachplan, Bankgeschäft) – nur für die eigene Werkstatt.</summary>
    private void InnenTafelOeffnen(string tafel)
    {
        if (_stadt.InnenBetrieb is not { } betrieb) return;
        if (betrieb.Konto != _spieler)
        {
            Protokollieren($"{betrieb.Name} gehört {betrieb.Konto.Inhaber} – die Bücher und Werkzeuge dort sind nicht für Euch.");
            return;
        }
        if (tafel.Equals("Auftraege", StringComparison.OrdinalIgnoreCase)) // Kartentisch des Diebs: Aufträge und Banden im Hinterzimmer (Gaunerei)
        {
            TafelnSchliessen();
            _hinterzimmer.Oeffnen();
            return;
        }
        InnenTafel? ziel = tafel.ToLowerInvariant() switch
        {
            "personal" => _personalTafel,
            "produktion" => _produktionTafel,
            "lager" => _lagerTafel,
            "karren" => _karrenTafel,
            "einrichten" or "talentbaum" => _einrichtenTafel,
            "wache" => _wachplanTafel,
            "bank" => _bankTafel,
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
            var ergebnis = _simulation.Bote.SofortAusfuehren(_spieler, new BetriebAusbauen(betrieb.Name));
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
    /// <summary>
    /// Nur für den Klicktest (--umland=1, PR #205): den günstigsten freien Umlandbetrieb erwerben (vorher zum Bürger aufsteigen, wenn nötig), ihm einen Ochsenkarren dazukaufen und
    /// seine Kurzübersicht zeigen – ohne Innenansicht stehen Karren, Eskorte und „Verkaufen“ dort unter „…“.
    /// </summary>
    private void UmlandVorbereiten()
    {
        if (_simulation.Wirtschaft is not { } w || _spieler is null) return;
        Betrieb? Erwerben()
        {
            var frei = w.FreieLiegenschaften
                .Where(l => w.Daten.Art(l.Art).Ort == Arbeitsort.Umland && w.LiegenschaftErwerbVerboten(_spieler, l.Name) is null)
                .OrderBy(l => l.Kaufpreis).FirstOrDefault();
            return frei is not null && _simulation.Bote.SofortAusfuehren(_spieler, new LiegenschaftErwerben(frei.Name)).Erfolg
                ? w.Betriebe.FirstOrDefault(b => b.Konto == _spieler && b.Name == frei.Name) : null;
        }
        var umland = Erwerben();
        if (umland is null)
        {
            AufsteigenVorbereiten(); // Umlandbetriebe erst ab Bürger
            umland = Erwerben();
        }
        if (umland is null)
        {
            Protokollieren("--umland=1: Kein freier Umlandbetrieb zu haben (mehr Vermögen mit --vermoegen=30000).");
            return;
        }
        _simulation.Bote.SofortAusfuehren(_spieler, new KarrenKaufen(umland.Name, "Ochsenkarren"));
        BetriebAuswaehlen(umland, hinfahren: true);
        _uebersicht.MehrOffen = true;
    }

    private void VerkaufVorbereiten()
    {
        if (_simulation.Wirtschaft is not { } w || _spieler is null) return;
        bool Kaufen()
        {
            var frei = w.FreieLiegenschaften.Where(l => w.LiegenschaftErwerbVerboten(_spieler, l.Name) is null)
                .OrderBy(l => l.Kaufpreis).FirstOrDefault();
            return frei is not null && _simulation.Bote.SofortAusfuehren(_spieler, new LiegenschaftErwerben(frei.Name)).Erfolg;
        }
        var gekauft = Kaufen();
        if (!gekauft) _simulation.Bote.SofortAusfuehren(_spieler, new WohnhausAusbauen()); // der Umbau dauert – aufsteigen erst danach
        while (_simulation.Zeit.MinuteImTag < 12 * 60 - Spielzeit.TagesbeginnMinute && !_simulation.IstBeendet)
            _simulation.Tick();
        if (gekauft) return;
        _simulation.Bote.SofortAusfuehren(_spieler, new StandErwerben());
        if (!Kaufen()) Protokollieren("--verkaufen=2: Keine freie Liegenschaft zu haben – mehr Vermögen mit --vermoegen=20000.");
    }

    /// <summary>
    /// Nur für den Klicktest (--aufsteigen=1): Der Spieler baut sein Wohnhaus zum Bürgerhaus aus und steigt zum Bürger auf –
    /// erst dann darf er Werkstätten bauen. Alles mit den gewöhnlichen Befehlen (dafür braucht es Geld: --vermoegen=30000).
    /// </summary>
    private void AufsteigenVorbereiten()
    {
        if (_simulation.Wirtschaft is not { } w || _spieler is null) return;
        _simulation.Bote.SofortAusfuehren(_spieler, new WohnhausAusbauen());
        for (var i = 0; i < 245 && !_simulation.IstBeendet; i++) _simulation.Tick(); // der Umbau dauert 4 Stunden
        var ergebnis = _simulation.Bote.SofortAusfuehren(_spieler, new StandErwerben());
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
        if (ProbepartieAbholen() is { } probe) return probe; // „Karte ausprobieren“ aus der Kartenwerkstatt (Hauptszene.Probepartie.cs)
        if (optionen.TryGetValue("laden", out var datei)) // Startoption für Tests: Spielstand direkt laden
            ladePfad = $"{Spielstaende.Ordner}/{datei}";
        if (ladePfad is not null)
        {
            try
            {
                var (geladen, spielerKonto) = Spielstaende.Laden(ladePfad);
                Ladezeit.Marke("Kern: Spielstand lesen");
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
        Ladezeit.Marke("Kern: wirtschaft.json lesen");
        if (daten is null)
            return new Simulation(); // ohne Daten läuft nur die Uhr

        Spielzeit? start = null;
        if (optionen.TryGetValue("startstunde", out var s) && int.TryParse(s, out var stunde))
            start = new Spielzeit(Math.Clamp(stunde * 60 - Spielzeit.TagesbeginnMinute, 0, Spielzeit.MinutenProTag - 1));
        double? startvermoegen = optionen.TryGetValue("vermoegen", out var v) && double.TryParse(v, out var betrag) ? betrag : null;

        var karte = Stadtkarten.Startkarte; // --karte=koeln: eine andere Karte, nur für diesen Start (das Hauptmenü nimmt dieselbe)
        // Rivalen: alle KI-Häuser der Karte, wie im Menü vorgewählt (Probestadt zwei, ohne Startbelegung die drei des Ersatzes); mehr als die Karte
        // hat, geht nicht – auch eine Wahl aus dem Menü wird darauf begrenzt
        var haeuser = (karte?.Startbelegung ?? Startbelegung.Ersatz).Haeuser.Count;
        neu ??= new NeuePartie(Startvermoegen: startvermoegen, Beruf: optionen.GetValueOrDefault("beruf", "Schmied"),
            Glaube: optionen.TryGetValue("glaube", out var g) && Enum.TryParse<Reichsgilde.Core.Glaube.Konfession>(g, true, out var konfession) ? konfession : null,
            Rivalen: haeuser);
        if (neu.Rivalen > haeuser) neu = neu with { Rivalen = haeuser };
        einstellungen ??= Partieeinstellungen.Standard;
        if (optionen.TryGetValue("schwierigkeit", out var stufe) && Enum.TryParse<Schwierigkeit>(stufe, true, out var schwierigkeit))
            einstellungen = einstellungen with { Schwierigkeit = schwierigkeit }; // --schwierigkeit=Schwer: nur für diesen Start
        var (simulation, spieler) = Partieaufbau.Erstellen(einstellungen, daten, neu, start, karte);
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
        Hauptmenue.Knopf(_menueKnoepfe, "Hilfe (F1)", HilfeOeffnen);
        ProbeKnopfBauen(); // „Zurück zur Kartenwerkstatt“ in einer Probepartie (Hauptszene.Probepartie.cs)
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

    /// <summary>Liest game/daten/wirtschaft.json. Bei Fehlern: Meldung ins Protokoll und null.</summary>
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
        w.Meldung += meldung => Protokollieren(SchiffsMeldung(meldung)); // Überfälle auf eigene Schiffe als Warnung (Hauptszene.Hafen.cs)
        // Die ersten Züge der KI fielen schon beim Aufstellen – ins Protokoll nachtragen (älteste zuerst).
        foreach (var rivale in w.Rivalen)
            foreach (var zug in rivale.Entscheidungen.Reverse())
                Protokollieren(zug);

        _wohnhaus.Verbinden(simulation, spieler);
        _berichtTafel.Verbinden(simulation, spieler);
        Lexikon.Verbinden(w); // die Live-Zahlen des Lexikons kommen aus dieser Partie
        _hafenTafel.Verbinden(simulation, spieler);
        _ratskammer.Verbinden(simulation, spieler);
        _rathausTafel.Verbinden(simulation, spieler);
        RatssitzungVerbinden(simulation, w, spieler);
        _familientafel.Verbinden(simulation, spieler);
        _charakter.Verbinden(simulation, spieler);
        _gerichtssaal.Verbinden(simulation, spieler);
        _hinterzimmer.Verbinden(simulation, spieler);
        _markt.Verbinden(simulation, spieler);
        _verkauf.Verbinden(simulation, spieler);
        foreach (var tafel in _innenTafeln) tafel.Verbinden(simulation, spieler);
        _wohnhausBaum.Verbinden(simulation, spieler);
        // Um 20 Uhr schlagen die Banden zu – ein offenes Hinterzimmer zeigt sofort, was geschah.
        w.Gauner.NachtVorbei += () => { if (_hinterzimmer.Visible) _hinterzimmer.Aufbauen(); };
        _uebersicht.Verbinden(simulation, spieler, _stadt.HatInnenansicht);
        _bauFrage.Verbinden(simulation, spieler);
        _bau.Verbinden(simulation, spieler, _stadt);
        _wegTafel.Verbinden(simulation, spieler, _stadt);
        Ladezeit.Marke("Kern: Partie aufstellen, Oberfläche verbinden");
        // Die Stadtansicht zeichnet die Karte der Partie, stellt jeden Betrieb an seinen Platz und zeigt die freien Plätze.
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

    /// <summary>Schließt die Auswahl rechts samt Kurzübersicht (und die Wohnhaus-Tafel, die für das eigene Wohnhaus an ihrer Stelle steht).</summary>
    private void AuswahlSchliessen()
    {
        if (_auswahlKennung is not null && _auswahlKennung == EigenesWohnhaus) _wohnhaus.Hide();
        _auswahlKennung = null;
        _auswahl.Hide();
        _uebersicht.Zeigen(null);
        _rathausTafel.Zeigen(false);
    }

    public override void _Process(double delta)
    {
        _simulation.Aktualisieren(delta);
        WegTippNachfuehren();
        if (_spulen > 0 && _bauAutoKlick == 0) // Klicktest: Zeit überspringen (z. B. eine Bauzeit)
        {
            var ticks = _spulen;
            _spulen = 0;
            for (var i = 0; i < ticks && !_simulation.IstBeendet; i++) _simulation.Tick();
        }
        if (_charakterWartet is { } wunsch && _bauAutoKlick == 0 && _spulen == 0)
        {
            _charakterWartet = null;
            CharakterStartoption(wunsch);
        }
        if (_speichernAls is not null && _bauAutoKlick == 0 && _spulen == 0 && _spieler is not null)
        {
            Protokollieren(Spielstaende.Speichern(_simulation, _spieler, _speichernAls).Meldung); // Spielstand für Tests ablegen
            _speichernAls = null;
        }
        if (_innenWartet is not null && _bauAutoKlick == 0 && _spulen == 0 && _stadt.InnenansichtBetreten(_innenWartet)) _innenWartet = null;
        RatssitzungPruefen(); // steht die Partie vor der Ratssitzung: in den Ratssaal

        Anzeigen();
        LeisteAnzeigen(delta);
        MeldungenAltern(delta);
        _berichtTafel.Aktualisieren(delta);
        _hafenTafel.Aktualisieren(delta);
        _stadt.Auswahl = _auswahl.Visible || _wohnhaus.Visible ? _auswahlKennung // Name des ausgewählten Gebäudes bleibt sichtbar
            : _hafenTafel.Visible ? (_hafenTafel.GewaehltesSchiff is { } schiff ? "schiff:" + schiff : Stadtansicht.HafenKennung(_hafenTafel.Hafen)) : null;
        var uhr = Ladezeit.Uhr();
        _stadt.Aktualisieren(delta);
        Ladezeit.Dazu("im Bild: Stadtansicht.Aktualisieren gesamt", uhr);
        foreach (var tafel in _innenTafeln) tafel.Aktualisieren(delta);
        _wohnhausBaum.Aktualisieren(delta);
        var innenTafelOffen = _innenTafeln.Any(t => t.Visible) || _wohnhausBaum.Visible || _sitzungsTafel.Laeuft; // die Meldungen schienen sonst durch die große Tafel
        if (_meldungen.Visible == innenTafelOffen) _meldungen.Visible = !innenTafelOffen;
        var mausPos = GetViewport().GetMousePosition();
        _stadt.UnterTafel = _innenTafeln.Any(t => t.Visible && t.Tafel.GetGlobalRect().HasPoint(mausPos))
            || (_wohnhausBaum.Visible && _wohnhausBaum.Tafel.GetGlobalRect().HasPoint(mausPos));
        BauAutoKlickPruefen();
        if (_auswahlKennung is not null) AuswahlAnzeigen();
    }

    /// <summary>Tastatur: Leertaste = Pause, 1–4 = Tempostufen, 5–6 = Testtempo 16×/32×. (Kamera: siehe Stadtansicht)</summary>
    public override void _UnhandledInput(InputEvent eingabe)
    {
        if (RatssitzungEingabe(eingabe)) return; // während der Ratssitzung: nur Spielmenü, Weiter, Hilfe
        if (eingabe is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            // Esc geht einen Schritt zurück: feste Tooltips → Fenster (das oberste) → Innenansicht verlassen → Auswahl schließen → Spielmenü auf/zu.
            if (Tooltips.ObersteFesteSchliessen()) { /* ein festgeheftetes Tooltip-Fenster ist zu */ }
            else if (_jahresbericht.Visible) { /* der Bericht wird mit „Weiter" geschlossen */ }
            else if (_hilfe.Visible)
                HilfeSchliessen();
            else if (_lexikon.Offen)
                _lexikon.Schliessen();
            else if (_bauFrage.Visible)
                _bauFrage.Zurueck();
            else if (_wegTafel.Aktiv)
                _wegTafel.Schliessen();
            else if (_bau.Aktiv)
                _bau.Zurueck();
            else if (Fensterrahmen.ObersteSchliessen())
            { /* das zuletzt geöffnete oder angeklickte Fenster ist zu (Mitte, Innentafeln, Wohnhaus, Bericht, Hafen) */ }
            else if (_einstellungen.Tafel.Visible)
                EinstellungenSchliessen();
            else if (_spielmenue.Visible)
                SpielmenueSchliessen();
            else if (_stadt.InnenKennung is not null)
                _stadt.InnenansichtVerlassen();
            else if (_berichtTafel.Visible)
                _berichtTafel.Hide();
            else if (_hafenTafel.Visible)
                _hafenTafel.Hide();
            else if (_auswahl.Visible || _wohnhaus.Visible)
                AuswahlSchliessen();
            else
                SpielmenueOeffnen();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (eingabe is not InputEventKey { Pressed: true, Echo: false } taste)
            return;

        // Tastenkürzel springen direkt hin (PR #197). Mit Strg oder Alt nicht (Alt hält die Namen der Stadt sichtbar).
        var ohneZusatz = !taste.CtrlPressed && !taste.AltPressed && !taste.MetaPressed;
        var imSpiel = _spieler is not null && !_spielmenue.Visible && !_einstellungen.Tafel.Visible && !_jahresbericht.Visible;
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
            case Key.F1:
                if (_hilfe.Visible) HilfeSchliessen();
                else HilfeOeffnen();
                break;
            case Key.X when ohneZusatz && imSpiel && (Fensterrahmen.IrgendeinesOffen || Tooltips.FesteOffen): // alle Fenster zu (auch festgehaltene)
                Tooltips.AlleSchliessen();
                Fensterrahmen.AlleSchliessen();
                break;
            case Key.F when ohneZusatz && imSpiel:
                if (_familientafel.Visible) _familientafel.Hide();
                else FamilieOeffnen();
                break;
            case Key.H when ohneZusatz && imSpiel:
                if (_hinterzimmer.Visible) _hinterzimmer.Hide();
                else HinterzimmerOeffnen();
                break;
            case Key.R when ohneZusatz && imSpiel && !_bau.Aktiv: // im Baumodus dreht R den Umriss (Stadtansicht.Bauen.cs)
                if (_ratskammer.Visible) _ratskammer.Hide();
                else RatOeffnen();
                break;
            case Key.M when ohneZusatz && imSpiel:
                if (_markt.Visible) _markt.Hide();
                else MarktOeffnen();
                break;
            case Key.L when ohneZusatz && imSpiel:
                if (_lexikon.Offen) _lexikon.Schliessen();
                else _lexikon.Oeffnen();
                break;
            case Key.F12 when imSpiel && _entwickler is not null: // Entwicklerfenster, nur mit dem Haken „Entwickler-Optionen“
                _entwickler.Umschalten();
                break;
            case Key.B when ohneZusatz && imSpiel:
                if (_bau.Aktiv) _bau.Schliessen();
                else BauenOeffnen();
                break;
            case Key.G when ohneZusatz && imSpiel: // Wege-Modus (W ist die Kamera): öffnet das Baumenü auf „Wege“ bzw. schaltet um
                if (!_bau.Aktiv) BauenOeffnen();
                if (_bau.Aktiv) _bau.WegeModus = !_bau.WegeModus;
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    private void PauseUmschalten()
    {
        _simulation.Takt.PauseUmschalten();
        KlangOberflaeche.Spielen(KlangOberflaeche.Ereignis.Tempo); // Knopf wie Leertaste
        Protokollieren(_simulation.Takt.IstPausiert ? "Pause." : "Weiter geht's.");
    }

    private void TempoSetzen(double tempo)
    {
        if (tempo <= 0) return;
        _simulation.Takt.Tempo = tempo;
        _simulation.Takt.IstPausiert = false;
        KlangOberflaeche.Spielen(KlangOberflaeche.Ereignis.Tempo); // Knopf wie Zifferntaste
    }

    /// <summary>Überträgt den Zustand der Simulation auf Kopfzeile, Zeitstrahl und die offenen Tafeln rechts.</summary>
    private void Anzeigen()
    {
        KopfAnzeigen();
        _wohnhaus.Aktualisieren();
        _uebersicht.Aktualisieren();
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
            case Stadtansicht.RaumAmt or Stadtansicht.RaumKontor or Stadtansicht.RaumWerk or Stadtansicht.RaumLager or Stadtansicht.RaumSaege or Stadtansicht.RaumHuette or Stadtansicht.RaumLaden or Stadtansicht.RaumTurm or Stadtansicht.RaumPavillon or Stadtansicht.RaumKueche or Stadtansicht.RaumSaal or Stadtansicht.RaumKirche or Stadtansicht.RaumSkriptorium or Stadtansicht.RaumSchatzkammer or Stadtansicht.RaumSchlafboden or Stadtansicht.RaumGildenhalle or Stadtansicht.RaumZelle or Stadtansicht.RaumUebung or Stadtansicht.RaumHauptmann or Stadtansicht.RaumPfand or Stadtansicht.RaumBankkontor or Stadtansicht.RaumEmpfang or Stadtansicht.RaumTresor:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// In der Raumansicht bleibt das Bild frei: Die Tafel rechts (Auswahl, beim eigenen Wohnhaus dessen Tafel) erscheint nur in Kontor
    /// und Amtsstube. Draußen und im Dom (noch Diorama) wie bisher.
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
        _raumleiste.Visible = _stadt.RaumKennung is not null && !_sitzungsTafel.Laeuft; // in der Ratssitzung bleibt man im Saal
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
        var wappenRand = new MarginContainer(); // das Wappen sitzt links vom ✕ des Fensterrahmens und überdeckt es nicht
        wappenRand.AddThemeConstantOverride("margin_right", 30);
        spalte.RemoveChild(_auswahlText);
        zeile.AddChild(_auswahlText);
        _auswahlText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _auswahlWappen = WappenBild.Neu(null, 56);
        _auswahlWappen.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        _auswahlWappen.Visible = false;
        wappenRand.AddChild(_auswahlWappen);
        zeile.AddChild(wappenRand);
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

        // Das eigene Wohnhaus hat seine eigene Tafel mit Reitern (an derselben Stelle rechts)
        if (_auswahlKennung == EigenesWohnhaus)
        {
            if (_auswahl.Visible) _auswahl.Hide();
            _uebersicht.Zeigen(null);
            // Im eigenen Wohnhaus nur im Kontor (wie früher das Kontor), aus einem anderen Gebäude heraus (Kopfzeile, F) überall
            var zeigen = RechtsFrei && (AuswahlImRaum || _stadt.InnenKennung != EigenesWohnhaus);
            if (_wohnhaus.Visible != zeigen)
            {
                if (zeigen) _wohnhaus.Zeigen(WohnhausAnzeige.Reiter.Haus);
                else _wohnhaus.Hide();
            }
            return;
        }
        if (_wohnhaus.Visible) _wohnhaus.Hide();

        var betrieb = AusgewaehlterBetrieb();
        _uebersicht.Zeigen(betrieb); // nur bei eigenen Betrieben sichtbar
        var eigener = _uebersicht.Visible; // die Kurzübersicht hat ihr eigenes „Hineingehen“
        var innenKnopf = !eigener && _stadt.HatInnenansicht(_auswahlKennung);
        if (_innenKnopf.Visible != innenKnopf) _innenKnopf.Visible = innenKnopf;
        var innenText = _stadt.InnenKennung == _auswahlKennung ? "Zurück zur Stadt (Esc)" : "Hineingehen";
        if (_innenKnopf.Text != innenText) _innenKnopf.Text = innenText;
        var frei = _auswahlKennung.StartsWith("liegenschaft:")
            ? w.FreieLiegenschaften.FirstOrDefault(l => Stadtansicht.LiegenschaftKennung(l.Name) == _auswahlKennung)
            : null;
        var schiff = Stadtansicht.SchiffName(_auswahlKennung) is { } schiffName ? w.Schiffahrt.Schiff(schiffName) : null;
        var besitzer = betrieb?.Konto ?? schiff?.Besitzer;
        _auswahlWappen.Wappen = besitzer?.Wappen;
        if (_auswahlWappen.Visible != besitzer is not null) _auswahlWappen.Visible = besitzer is not null;
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
            _ when schiff is not null => FremdesSchiffText(schiff, w), // Schiff eines anderen Hauses (Hauptszene.Hafen.cs)
            _ when _stadt.WohnhausText(_auswahlKennung) is { } wohnhaus => wohnhaus + LeerstandZusatz(w), // Wohnhaus eines Spielhauses (Darstellung)
            _ when _stadt.GrundstueckText(_auswahlKennung) is { } grundstueck => grundstueck, // Baustelle oder Bauplatz (Darstellung)
            _ when _stadt.StadtbauText(_auswahlKennung) is { } stadtbau => stadtbau, // Bauvorhaben der Stadt (PR #200)
            _ => _auswahlKennung,
        };
        AktionKnopfAnzeigen(w, frei);
        LeerstandKnoepfeAnzeigen(w);
        var ratSichtbar = _auswahlKennung == "Rathaus" && w.Rat.IstAktiv;
        if (_ratKnopf.Visible != ratSichtbar) _ratKnopf.Visible = ratSichtbar; // nur bei Änderung setzen (siehe AktionKnopfAnzeigen)
        var gerichtSichtbar = _auswahlKennung == "Rathaus" && w.Justiz.IstAktiv;
        _rathausTafel.Zeigen(_auswahlKennung == "Rathaus"); // nur mit Stadtkasse (RathausTafel.cs)
        if (_gerichtKnopf.Visible != gerichtSichtbar) _gerichtKnopf.Visible = gerichtSichtbar;
        if (_auswahl.Visible != (AuswahlImRaum && RechtsFrei))
        {
            _auswahl.Visible = AuswahlImRaum && RechtsFrei;
            if (_auswahl.Visible)
            {
                Rollbereich.RechtsAnheften(_auswahl);
                // Ohne Klick in die Welt (Taste, Leiste, Kopfzeile) steht sie wie früher rechts am Rand
                if (_auswahlKlickBild != Engine.GetProcessFrames())
                    _auswahlRahmen.PlatzSetzen((groesse, sichtbar) => new Vector2(sichtbar.X - groesse.X - 16, Rollbereich.KopfHoehe));
            }
        }
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
        _berichtTafel.Hide(); // rechts steht jetzt die Bautafel; die Auswahl wartet (RechtsFrei)
        _hafenTafel.Hide();
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
        if (wahl == "wege" && _bau.Aktiv) _bau.WegeModus = true; // der Wege-Modus des Baumenüs (Klicks mit KLICKS=…)
        if (wahl is "1" or "wege" || !_bau.Aktiv) return;
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

    /// <summary>
    /// Startoptionen zum Wegbelag: <c>--wegausbau=&lt;weg&gt;,&lt;von&gt;,&lt;bis&gt;,&lt;Kies|Pflaster&gt;[|…]</c> lässt einen Abschnitt auf Kosten des Spielers ausbauen,
    /// <c>--wegantrag=&lt;weg&gt;,&lt;von&gt;,&lt;bis&gt;</c> stellt den Ratsantrag „Weg pflastern“ (Meter entlang des Verlaufs). Die Meldung steht im Protokoll.
    /// </summary>
    private void WegBefehleStarten(Dictionary<string, string> optionen)
    {
        if (_simulation.Wirtschaft is not { } w || _spieler is null) return;
        double[]? Zahlen(string text, int anzahl) =>
            text.Split(',').Take(anzahl).Select(t => double.TryParse(t, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : double.NaN).ToArray() is { } z && z.Length == anzahl && z.All(d => !double.IsNaN(d)) ? z : null;
        if (optionen.TryGetValue("wegausbau", out var ausbauListe))
            foreach (var ausbau in ausbauListe.Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                var teile = ausbau.Split(',');
                if (teile.Length == 4 && Zahlen(string.Join(',', teile.Take(3)), 3) is { } z && Enum.TryParse<Belagstufe>(teile[3], true, out var stufe))
                {
                    _simulation.Bote.Einreihen(_spieler, new WegAusbauen((int)z[0], z[1], z[2], stufe), ergebnis =>
                        Protokollieren(UiHilfe.MeldungText(ergebnis)));
                }
                else Protokollieren("--wegausbau=" + ausbau + ": gemeint ist <Weg>,<von>,<bis>,<Kies|Pflaster>.");
            }
        if (optionen.TryGetValue("wegantrag", out var antrag))
        {
            if (Zahlen(antrag, 3) is { } z)
            {
                _simulation.Bote.Einreihen(_spieler, new WegPflasterBeantragen((int)z[0], z[1], z[2]), ergebnis =>
                    Protokollieren(UiHilfe.MeldungText(ergebnis)));
            }
            else Protokollieren("--wegantrag=" + antrag + ": gemeint ist <Weg>,<von>,<bis>.");
        }
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
        var ergebnis = _simulation.Bote.SofortAusfuehren(_spieler, befehl);
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
        {
            zeilen.Add("Ämter:\n" + string.Join("\n", w.Rat.Aemter.Select(a => $"  {a.Name}: {a.Inhaber}")));
            if (w.Rat.Antrag is { } antrag && !w.Ratsantraege.IstAktiv) // mit Stadtkasse steht er in der Tagesordnung (Reiter „Rat“)
                zeilen.Add($"Antrag liegt vor: Statut {antrag.Statut.Kuerzel()} ({antrag.Antragsteller.Inhaber})");
        }
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

    private string BetriebsText(Betrieb b, Stadtwirtschaft w)
    {
        // Eigener Betrieb: nur der Name – Zustand, Ware, Arbeiter, Lager und Gewinn zeigt die Kurzübersicht darunter.
        if (b.Konto == _spieler)
            return $"{b.Name}\n{b.Artdaten.Name}, Stufe {b.Stufe} · Euer Betrieb";

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

    /// <summary>
    /// <c>--kartenbild=&lt;ziel.png&gt;</c>: stellt die Kamera auf die ganze Karte (Mitte des Spielfelds, Zoom = Tiefe des Spielfelds), blendet die Oberfläche
    /// aus, speichert nach 6 s ein Bild (480 × 270) als Vorschau für die Kartenwahl und beendet das Spiel. Gemacht für
    /// <c>tools/kartenbilder/kartenbilder.sh</c>; braucht ein Fenster (Xvfb).
    /// </summary>
    private void KartenbildAufnehmen(string ziel)
    {
        GetNode<CanvasLayer>("Oberflaeche").Visible = false;
        if (Stadtkarten.Startkarte?.Kartengrenzen is { } grenzen)
            _stadt.KameraSetzen((float)grenzen.Mitte.X, (float)grenzen.Mitte.Z, (float)grenzen.Tiefe * 1.1f);
        var uhr = new Godot.Timer { WaitTime = 6, OneShot = true, Autostart = true };
        uhr.Timeout += () =>
        {
            var bild = GetViewport().GetTexture().GetImage();
            bild.Resize(480, 270, Image.Interpolation.Lanczos);
            var fehler = bild.SavePng(ziel);
            GD.Print(fehler == Error.Ok ? $"Kartenbild geschrieben: {ziel}" : $"Kartenbild {ziel}: {fehler}");
            GetTree().Quit();
        };
        AddChild(uhr);
    }
}
