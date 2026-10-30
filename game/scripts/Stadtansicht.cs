using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die 2,5D-Stadtansicht als „Greybox": Alle Gebäude bestehen aus einfachen Grundformen
/// (Quader, Prismen, Zylinder). So lässt sich Aufbau, Kamera und Zusammenspiel mit der
/// Simulation erproben, bevor echte Grafiken entstehen. Später werden die Grundformen
/// einfach durch Modelle ersetzt – die Logik bleibt.
///
/// Was diese Ansicht zeigt (alles kommt aus dem Simulationskern):
///   • Karren fahren entlang der Wege zum Markt und zurück
///   • Sonne, Himmel und Licht folgen der Spieluhr
///   • Das Material „Glut“ der Werkstatt-Modelle (Esse, Ofen, Athanor) glüht, solange dort gearbeitet wird; bei Nacht leuchten die Fenster
///   • Gebäude lassen sich anklicken (Ereignis <see cref="GebaeudeAngeklickt"/>)
///   • Werkstätten und Rathaus lassen sich betreten: Eine eigene Kamera blickt fest in je einen Raum
///     (Raumansicht, Stadtansicht.Raeume.cs; Einrichtung des Werkraums: Stadtansicht.Innenraeume.cs).
///     Der Dom ist noch ein „Diorama": Dach und Vorderwände werden in der Stadt ausgeblendet.
///
/// Blickrichtungen: Die Kamera schaut schräg von oben und diagonal zu den Himmelsrichtungen –
/// nach Nordosten, Nordwesten, Südwesten oder Südosten (Entscheidung vom 13.10.2026).
/// Norden ist −Z, Osten ist +X. Dadurch zeigen immer zwei Gebäudeseiten zur Kamera.
///
/// Steuerung: Mausrad = Zoom, rechte Maustaste ziehen oder WASD/Pfeiltasten = verschieben,
/// Q/E = um 90° drehen, Linksklick = Gebäude auswählen, Doppelklick = hineingehen. Im Gebäude: Q/E = Raum wechseln.
/// </summary>
public partial class Stadtansicht : Node3D
{
    // ── Farben der Greybox (gedämpft, bodenständig) ─────────────────────────
    private static readonly Color Wiese = new(0.33f, 0.40f, 0.24f);
    private static readonly Color Pflaster = new(0.47f, 0.44f, 0.40f);
    private static readonly Color Weg = new(0.45f, 0.36f, 0.26f);
    private static readonly Color Mauerstein = new(0.52f, 0.49f, 0.45f);
    private static readonly Color Putz = new(0.80f, 0.75f, 0.64f);
    private static readonly Color Fachwerk = new(0.62f, 0.55f, 0.45f);
    private static readonly Color Ziegel = new(0.55f, 0.25f, 0.18f);
    private static readonly Color Schiefer = new(0.28f, 0.30f, 0.34f);
    private static readonly Color Holz = new(0.36f, 0.25f, 0.16f);
    private static readonly Color Spieler = new(0.20f, 0.42f, 0.72f); // nur ohne Farbe in der Karte (Besitzerfarbe) und für die Ständetafel im Rathaus
    private static readonly Color Rivale = new(0.70f, 0.20f, 0.16f);
    private static readonly Color Bruchstein = new(0.68f, 0.65f, 0.60f);
    private static readonly Color Kraut = new(0.30f, 0.52f, 0.25f);
    private static readonly Color Wasser = new(0.25f, 0.42f, 0.55f);
    private static readonly Color Morsch = new(0.33f, 0.29f, 0.25f);

    private const float KameraMinZoom = 25f;
    /// <summary>So weit (Meter) darf die Kamera über das Rechteck aller Gebäude, Wege und Mauern der Karte hinaus verschoben werden.</summary>
    private const float KameraRand = 12f;
    private const float KameraMaxZoom = 140f;
    private const float InnenZoom = 15f;
    private const float InnenMinZoom = 10f;
    private const float InnenMaxZoom = 40f;
    private const int MaxKistenJeWare = 8;   // 8 Kisten × 5 Stück = Lager voll (40)
    private const int StueckJeKiste = 5;
    private const int MaxWarenplaetze = 6;

    private Simulation? _simulation;
    private Node3D _kameraDrehung = null!;
    private Camera3D _kamera = null!;
    private DirectionalLight3D _sonne = null!;
    private WorldEnvironment _umgebung = null!;
    private float _zielDrehung = -45f; // −45° = Blick nach Nordosten; jede Vierteldrehung ±90°
    private bool _ziehen;
    private double _animationszeit;

    // Innenansicht: welcher Betrieb gerade „aufgeschnitten" ist, und wohin die Kamera danach zurückkehrt.
    private InnenAnsicht? _innen;
    private Vector3 _vorherPosition;
    private float _vorherZoom;
    private Tween? _kamerafahrt;
    private readonly List<Node3D> _kulisseAusgeblendet = new();

    // Raumansicht: eigene Kamera mit Perspektive, die fest in den gerade gezeigten Raum blickt (Stadtansicht.Raeume.cs).
    private Camera3D _raumKamera = null!;
    private Raum? _raum;

    private readonly Dictionary<Betrieb, BetriebsAnsicht> _betriebe = new();
    private readonly Dictionary<Karren, KarrenAnsicht> _karren = new();
    private readonly List<Label3D> _beschriftungen = new();
    private const float BezugsZoom = 95f;

    /// <summary>Wird ausgelöst, wenn ein Gebäude angeklickt wird. Übergeben wird seine Kennung.</summary>
    public event Action<string>? GebaeudeAngeklickt;

    /// <summary>Wird ausgelöst, wenn eine Innenansicht betreten (Kennung des Gebäudes) oder verlassen (null) wird.</summary>
    public event Action<string?>? InnenansichtGewechselt;

    /// <summary>Wird ausgelöst, wenn in der Raumansicht ein anderer Raum gezeigt wird (Kennung des Raums, null beim Verlassen).</summary>
    public event Action<string?>? RaumGewechselt;

    /// <summary>Wird ausgelöst, wenn sich ändert, welche Räume des betretenen Gebäudes man betreten darf (z. B. die Sägehalle nach dem Ausbau): Die Raumleiste muss neu aufgebaut werden.</summary>
    public event Action? InnenRaeumeGeaendert;

    /// <summary>Kennung des gezeigten Raums („raum:werk", „rathaus:rat" …), sonst null – auch im Dom, der noch ein Diorama ist.</summary>
    public string? RaumKennung => _raum?.Kennung;

    /// <summary>Name des gezeigten Raums, sonst null.</summary>
    public string? RaumName => _raum?.Name;

    /// <summary>Die Räume des betretenen Gebäudes, die man gerade betreten darf (für die Raumleiste).</summary>
    public IReadOnlyList<(string Kennung, string Name)> InnenRaeume =>
        _innen is null ? Array.Empty<(string, string)>() : Zugaengliche(_innen).Select(r => (r.Kennung, r.Name)).ToList();

    /// <summary>Kennung des Gebäudes, dessen Innenansicht gerade gezeigt wird („betrieb:…", „Rathaus", „Dom"), sonst null.</summary>
    public string? InnenKennung => _innen?.Kennung;

    /// <summary>Name des betretenen Gebäudes, sonst null.</summary>
    public string? InnenName => _innen?.Name;

    /// <summary>Der Betrieb, dessen Innenansicht gerade gezeigt wird, sonst null (auch im Rathaus und im Dom).</summary>
    public Betrieb? InnenBetrieb => (_innen as BetriebsAnsicht)?.Betrieb;

    /// <summary>Ob dieser Betrieb eine Innenansicht hat (alle Werkstätten in der Stadt).</summary>
    public bool HatInnenansicht(Betrieb betrieb) => _betriebe.TryGetValue(betrieb, out var a) && a.HatInnenraum;

    /// <summary>Ob das Gebäude mit dieser Kennung eine Innenansicht hat (Werkstätten, Rathaus, Dom).</summary>
    public bool HatInnenansicht(string? kennung) => kennung is not null && AnsichtZu(kennung) is { HatInnenraum: true };

    /// <summary>Rathaus und Dom: Gebäude mit Innenraum, die kein Betrieb sind.</summary>
    private readonly Dictionary<string, SonderbauAnsicht> _sonderbauten = new();

    private InnenAnsicht? AnsichtZu(string kennung) =>
        _sonderbauten.TryGetValue(kennung, out var s) ? s : _betriebe.Values.FirstOrDefault(b => b.Kennung == kennung);

    /// <summary>In welche Himmelsrichtung die Kamera gerade blickt.</summary>
    public string Blickrichtung
    {
        get
        {
            // Drehung −45° → Kamera steht im Südwesten und blickt nach Nordosten;
            // jede +90° wandert die Kamera eine Ecke weiter (gegen den Uhrzeigersinn, von oben gesehen).
            var schritt = ((int)Mathf.Round((_zielDrehung + 45f) / 90f) % 4 + 4) % 4;
            return schritt switch { 0 => "Nordosten", 1 => "Nordwesten", 2 => "Südwesten", _ => "Südosten" };
        }
    }

    /// <summary>Richtung von einem Gebäude zur Kamera (waagerecht), für das Ausblenden der Vorderwand.</summary>
    private Vector3 RichtungZurKamera
    {
        get
        {
            var winkel = Mathf.DegToRad(_kameraDrehung.RotationDegrees.Y);
            return new Vector3(Mathf.Sin(winkel), 0, Mathf.Cos(winkel));
        }
    }

    /// <summary>
    /// Alles, was ein Gebäude zum Aufschneiden braucht (Diorama-Innenansicht): einzelne Wände, ein Dach als
    /// eigene Gruppe und der Knoten „Innen" mit der Einrichtung. Werkstätten (<see cref="BetriebsAnsicht"/>) und
    /// die Sonderbauten Rathaus und Dom (<see cref="SonderbauAnsicht"/>) teilen sich dieses Gerüst.
    /// </summary>
    private abstract class InnenAnsicht
    {
        /// <summary>Mitte des Gebäudes (Ziel der Kamera beim Betreten). Das Wohnhaus des Spielers wandert beim Umzug.</summary>
        public required Vector3 Mitte { get; set; }
        /// <summary>Kennung wie beim Anklicken („betrieb:…", „Rathaus", „Dom").</summary>
        public abstract string Kennung { get; }
        public abstract string Name { get; }
        public Label3D? Schild { get; init; }

        /// <summary>Jede Wand mit ihrer Außenrichtung (zeigt vom Gebäude weg).</summary>
        public List<(Node3D Wand, Vector3 Aussen)> Waende { get; } = new();
        public Node3D? Dach { get; set; }
        /// <summary>Das ganze Gebäude von außen, wo es ein Modell ist (Rathaus, Dom): Es steht beim Blick in ein anderes Haus nur als Ganzes davor und wird dann ausgeblendet.</summary>
        public Node3D? Huelle { get; set; }
        public Node3D? Innen { get; set; }
        public bool HatInnenraum => Innen is not null;
        /// <summary>Zoom beim Betreten und wie weit man die Kamera im Innern verschieben darf.</summary>
        public float Zoom { get; init; } = InnenZoom;
        public float Reichweite { get; init; } = 8;
        /// <summary>Klickfläche des ganzen Gebäudes – in der Innenansicht aus, damit die Räume erreichbar sind.</summary>
        public CollisionShape3D? Aussenklick { get; set; }
        /// <summary>Weitere Klickflächen des Gebäudes von außen (Turm), die in der Innenansicht ebenfalls aus sind.</summary>
        public List<CollisionShape3D> ZusatzKlicks { get; } = new();
        /// <summary>Klickflächen der Räume (Rathaus) – nur in der Innenansicht an.</summary>
        public List<CollisionShape3D> Raumklicks { get; } = new();

        /// <summary>Glut und Flammen, die bei der Arbeit (oder zu ihrer eigenen Zeit) hell leuchten.</summary>
        public List<Feuerstelle> Feuer { get; } = new();
        /// <summary>Teile, die sich bei der Arbeit drehen oder auf und ab gehen.</summary>
        public List<Bewegtteil> Bewegt { get; } = new();
        /// <summary>Einrichtung, die erst ab einer Gebäudestufe dasteht.</summary>
        public List<(Node3D Teil, int AbStufe)> Stufenteile { get; } = new();
        /// <summary>Figuren, die keine Arbeiter sind: Gäste, Gläubige, Kunden, Gefangene.</summary>
        public List<Arbeiterfigur> Besucher { get; } = new();
        /// <summary>Kleine Tafel im Raum für Zahlen aus der Simulation (Gäste, Kasse, Aufträge …).</summary>
        public Label3D? Infotafel { get; set; }
        /// <summary>Zeigt Zustände aus der Simulation (Uhrzeit, Arbeitszeit, wird gearbeitet).</summary>
        public Action<Spielzeit, bool, bool>? Zustand { get; set; }

        /// <summary>Räume der Raumansicht in ihrer Reihenfolge. Leer = Diorama (Gebäude wird aufgeschnitten).</summary>
        public List<Raum> Raeume { get; } = new();
        public bool Raumansicht => Raeume.Count > 0;
        /// <summary>Weitere Zustände einzelner Räume (Kontor, Wohnstube), jedes Bild nach <see cref="Zustand"/>.</summary>
        public List<Action<Spielzeit, bool>> Raumzustaende { get; } = new();
    }

    /// <summary>Rathaus oder Dom: kein Betrieb, aber mit Innenraum.</summary>
    private sealed class SonderbauAnsicht : InnenAnsicht
    {
        public required string Schluessel { get; init; }
        public required string Titel { get; init; }
        public override string Kennung => Schluessel;
        public override string Name => Titel;
    }

    /// <summary>
    /// Darstellung eines Betriebs: Grundstück, Wegstrecke zum Markt und Esse.
    /// Bei Werkstätten sind Wände, Dach und Einrichtung einzelne Teile – so lässt sich das
    /// Gebäude wie ein Puppenhaus aufschneiden (Diorama-Innenansicht).
    /// </summary>
    private sealed class BetriebsAnsicht : InnenAnsicht
    {
        public required Betrieb Betrieb { get; init; }
        /// <summary>Wo Karren losfahren und halten: bei Werkstätten mit Modell dessen „Anker_Karren“ (<see cref="KarrenAnker"/>), sonst der Hof vor dem Tor.</summary>
        public required Vector3 Hof { get; set; }
        public required Vector3[] WegZumMarkt { get; set; }
        /// <summary>Alles, was zum Betrieb gehört (Gebäude, Weg, Innenraum) – verschwindet beim Verkauf in einem Zug.</summary>
        public Node3D? Gruppe { get; set; }
        public bool Spieler { get; set; }
        public override string Kennung => BetriebsKennung(Betrieb);
        public override string Name => Betrieb.Name;

        /// <summary>Farbe des Besitzers (Fahne, Karren).</summary>
        public Color Farbe { get; set; }
        /// <summary>Das Gebäude von außen (nur Werkstätten): das Modell aus Blender, hängt am <see cref="InnenAnsicht.Dach"/>-Knoten.</summary>
        public ModellInstanz? Modell { get; init; }
        /// <summary>Glühen die Essen und Öfen des Modells (Material „Glut“) gerade? Geschaltet wird nur beim Wechsel.</summary>
        public bool Glueht { get; set; }
        /// <summary>Abstand zweier Karren nebeneinander am Halteplatz (die Karren stehen mittig um den Anker verteilt, je eine Reihe weit auseinander), im Umland entlang der Vorderkante des gedrehten Modells; null bei Werkstätten (dort warten die Karren hintereinander auf dem Weg, siehe <c>KarrenBewegen</c>).</summary>
        public Vector3? KarrenReihe { get; init; }

        // ── nur bei Werkstätten mit Innenraum ──
        public Werkstil Stil { get; set; }
        public Dictionary<string, (MeshInstance3D[] Kisten, Label3D Schild)> Kisten { get; } = new();
        public List<Arbeiterfigur> Arbeiter { get; } = new();
        public string Belegungsstand { get; set; } = "";

        /// <summary>Arbeitsplätze der Reihe nach: die ersten für Stufe 1, dahinter die an den Stücken späterer Stufen.</summary>
        public List<Arbeitsplatz> Plaetze { get; } = new();
        /// <summary>Werkstück auf der Werkbank – sichtbar, solange gearbeitet wird, gefärbt nach dem Produkt.</summary>
        public MeshInstance3D? Werkstueck { get; set; }
        /// <summary>Materialien, die die Farbe des Produkts annehmen (Sud im Kessel, Duft in der Vorlage).</summary>
        public List<StandardMaterial3D> Produktfarben { get; } = new();

        // ── Gebäudestufe (nur Werkstätten) ──
        public int GezeigteStufe { get; set; } = 1;
        /// <summary>Innenraum aus einem Blender-Modell (Stadtansicht.Innenmodell.cs); null bei den alten Räumen aus Grundkörpern.</summary>
        public ModellRaumDaten? Modellraum { get; set; }
        public bool GezeigterUmbau { get; set; }
    }

    /// <summary>Eine Figur: Körper, der bewegliche Arm (mit Werkzeug), ihr Takt, was sie tut und wo der Arm ruht.</summary>
    private sealed record Arbeiterfigur(Node3D Koerper, Node3D Arm, float Takt, Bewegung Art, Vector3 ArmRuhe);

    /// <summary>
    /// Wo ein Betrieb steht: Mitte seiner Fläche, Drehung (Bogenmaß wie <c>Rotation.Y</c>), Breite und Tiefe der Fläche und – auf einem festen Platz –
    /// das Gebäude der Karte. Neu gebaute Werkstätten stehen auf ihrem Grundstück (dann ohne Gebäude).
    /// </summary>
    private sealed record Platz(Vector3 Mitte, float Drehung, Vector2 Groesse, Reichsgilde.Core.Bauen.Kartengebaeude? Gebaeude)
    {
        public Basis Basis => new(Vector3.Up, Drehung);

        /// <summary>Ein Punkt der Fläche in Koordinaten der Stadt (x nach rechts, z zur Vorderseite, von der Mitte).</summary>
        public Vector3 Lokal(Vector3 lokal) => Mitte + Basis * lokal;

        /// <summary>Der Hof im Vorplatz, wo die Gasse beginnt, Karren losfahren und Gäste ankommen.</summary>
        public Vector3 Hof => Lokal(new Vector3(0, 0, Groesse.Y / 2 - HofVorKante));
    }

    /// <summary>
    /// Der Platz eines Betriebs (oder einer freien Liegenschaft): der feste Platz der Karte oder das Grundstück einer neu gebauten Werkstatt
    /// (Abfrage des Kerns <c>Bauwesen.PlatzVon</c>). Null, wenn er nirgends steht.
    /// </summary>
    private Platz? PlatzVon(string betrieb)
    {
        var platz = _wirtschaft?.Bauen.PlatzVon(betrieb)
                    ?? (_karte?.PlatzVon(betrieb) is { } fest ? new Reichsgilde.Core.Bauen.Betriebsplatz(betrieb, fest, null) : null);
        if (platz is null) return null;
        var (x, z) = platz.Punkt;
        var groesse = platz.Gebaeude is { } g ? GroesseVon(g)
            : new Vector2((float)platz.Grundstueck!.Grundriss.Breite, (float)platz.Grundstueck.Grundriss.Tiefe);
        var drehung = Mathf.DegToRad((float)platz.DrehungGrad);
        return new Platz(AufFundament(new Vector3((float)x, 0, (float)z), drehung, groesse), drehung, groesse, platz.Gebaeude); // Mitte auf der Höhe des Fußbodens
    }

    /// <summary>Der Hof vor der Tür einer Werkstatt: die Stelle, wo Karren losfahren und Gäste ankommen (null, wenn der Betrieb nirgends steht).</summary>
    private Vector3? HofVon(string betrieb) => PlatzVon(betrieb)?.Hof;

    /// <summary>
    /// Der Weg vom <paramref name="start"/> (Hof, Karrenplatz) über das Gassennetz bis zur Mitte des Marktplatzes – für die Karren. Hängt der Start
    /// noch nicht am Netz (eine neu gebaute Werkstatt), bekommt er sein Stück zum nächsten Weg (gezeichnet in <paramref name="breite"/>).
    /// </summary>
    private Vector3[] MarktWeg(Vector3 start, float breite)
    {
        var von = AnWegAnschliessen(start, breite);
        var nach = MarktKnoten();
        var pfad = nach >= 0 ? _netz.Pfad(von, nach, faehren: true) : Array.Empty<Vector3>(); // Karren setzen mit Fähren über (Stadtansicht.Wasser.cs)
        return pfad.Length >= 2 ? pfad : new[] { start, _markt };
    }

    /// <summary>Freie Bauplätze und Gruben, die noch niemandem gehören: Name → ihre Darstellung.</summary>
    private readonly Dictionary<string, Node3D> _freiePlaetze = new();

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau
    // ═════════════════════════════════════════════════════════════════════════

    public override void _Ready()
    {
        Ladezeit.Marke("Szene laden (Datei und Knoten)");
        OberflaecheEinrichten(); // vor dem Aufbau: Jeder neue Knoten bekommt sein Material mit Struktur (Stadtansicht.Oberflaechendetails.cs)
        UmgebungUndLicht();
        KameraAufbauen();
        SchattenEinrichten(); // braucht Sonne und Kamera (Stadtansicht.Licht.cs)
        BloomNachfuehren();   // beide Umgebungen stehen jetzt (Stadtansicht.Licht.cs)
        Fenster.BloomGeaendert += BloomNachfuehren;
        VerdeckungNachfuehren(); // Umgebungsverdeckung, indirektes Licht und SDFGI (Stadtansicht.Licht.cs)
        GlobaleBeleuchtungEinrichten(); // SDFGI: neues Feld, Partikel und Figuren tragen nicht bei (Stadtansicht.GlobaleBeleuchtung.cs)
        Fenster.VerdeckungGeaendert += VerdeckungNachfuehren;
        GrafikEinrichten(); // Unschärfe, Wolken, Dunst, Farbstimmung, Detailstufe (Stadtansicht.Grafik.cs)
        Ladezeit.Marke("Stadtansicht: Licht, Kamera, Grafik");

        // Die Stadt selbst kommt aus der Karte (Stadtansicht.Karte.cs): mit Partie aus Verbinden (die Karte der Partie), ohne Partie (Hauptmenü)
        // die Greybox-Karte, und eine Mustermischung füllt die Wohnplätze. Erst nach dem Aufbau der Szene prüfen, denn die Hauptszene verbindet
        // die Simulation erst nach diesem _Ready.
        Callable.From(() =>
        {
            if (_simulation is not null || _entwurf is not null || OhneHintergrund) return; // Partie oder Kartenwerkstatt (Stadtansicht.Kartenwerkstatt.cs)
            KarteAufbauen(Stadtkarten.Greybox);
            WohnhaeuserPruefen();
            Ladezeit.Marke("Greybox des Hauptmenüs");
        }).CallDeferred();
    }

    /// <summary>Keine Greybox ohne Partie bauen: Das Hauptmenü wird gleich übersprungen (Startoptionen), oder eine Partie bzw. die Werkstatt
    /// baut sich gleich hinter dem Ladebildschirm auf (ihre Karte kommt erst nach dem ersten Bild).</summary>
    public bool OhneHintergrund { get; set; }

    private void UmgebungUndLicht()
    {
        var umgebung = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.47f, 0.60f, 0.72f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.75f, 0.75f, 0.80f),
            AmbientLightEnergy = 0.5f,
        };
        _umgebung = new WorldEnvironment { Environment = umgebung };
        AddChild(_umgebung);

        // Schatten: Bias, Weichzeichner und Größe der Schattenkarte stellt SchattenNachfuehren ein (Stadtansicht.Licht.cs).
        _sonne = new DirectionalLight3D { ShadowEnabled = true, LightEnergy = 1.1f };
        _sonne.RotationDegrees = new Vector3(-50, 30, 0);
        AddChild(_sonne);
    }

    private void KameraAufbauen()
    {
        // Drehung (um die Hochachse) → Neigung (40° schräg von oben) → Kamera mit Abstand.
        _kameraDrehung = new Node3D { Name = "Kamerafuehrung", RotationDegrees = new Vector3(0, _zielDrehung, 0) };
        AddChild(_kameraDrehung);
        var neigung = new Node3D { Name = "Kameraneigung", RotationDegrees = new Vector3(-KameraNeigung, 0, 0) };
        _kameraDrehung.AddChild(neigung);
        _kameraNeigung = neigung; // ihre Höhe folgt dem Gelände (Stadtansicht.Gelaende.cs)
        _kamera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal, // keine Fluchtpunkte = klassischer 2,5D-Look
            Size = 95,
            Position = new Vector3(0, 0, KameraAbstand), // Near und Far folgen dem Zoom (SchattenNachfuehren)
        };
        neigung.AddChild(_kamera);
        Feuer.AusblendenZuschlag = KameraAbstand; // die Fernausblendung der Feuerlichter misst von der Kamera, die 150 m vor dem Blickpunkt steht (Feuer.cs)
        RaumKameraAufbauen(); // wohin die Kamera beim Start blickt, sagt die Karte (KameraAusKarte)
        FreieKameraEinrichten(); // --freiekamera=1: frei fliegen statt der Spielkamera (Stadtansicht.FreieKamera.cs)
    }

    private static float AbstandZurStrecke(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        var t = Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
        return p.DistanceTo(a + ab * t);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Verbindung zur Simulation
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Verbindet die Stadt mit der Partie. Gezeichnet wird sie danach in <see cref="Aufbauen"/> (abschnittsweise hinter dem Ladebildschirm):
    /// ihre Karte (Stadtansicht.Karte.cs), die Betriebe der Simulation – jeder an seinem Platz (<see cref="PlatzVon"/>) –, freie Liegenschaften als
    /// Bauplatz bzw. leere Grube mit Schild und die Wohnhäuser. Bis dahin ruht die Stadt (kein <c>_Process</c>, keine Eingabe).
    /// </summary>
    public void Verbinden(Simulation simulation, Konto spieler)
    {
        _simulation = simulation;
        _spielerKonto = spieler;
        Ruhen(true);
        if (simulation.Wirtschaft is not { } w) return;
        _wirtschaft = w;
        // Verkauf und Kündigung zeigt die Stadt selbst; den Erwerb meldet die Hauptszene (BetriebHinzufuegen).
        w.BetriebAbgegeben += BetriebEntfernen;
        w.Wegebau.BelagGeaendert += BelagVeraltetMelden; // geänderte Wege neu zeichnen (Stadtansicht.Wegbelag.cs)
    }

    /// <summary>
    /// Zeichnet die Stadt der verbundenen Partie in Abschnitten (<see cref="Ladeschritt"/>, Anteil 0–1). Am Ende stehen auch die Wohnhäuser
    /// (früher erst im ersten <see cref="Aktualisieren"/>), damit das erste Bild vollständig ist.
    /// </summary>
    public IEnumerable<Ladeschritt> Aufbauen()
    {
        var karte = _wirtschaft?.Karte ?? Stadtkarten.Greybox;
        // die Karte bis 0,89, ihre Anteile darauf umgerechnet; die Betriebe hängen mit ihren Wegen am Gassennetz der Karte
        foreach (var schritt in KarteAufbauenSchritte(karte))
            yield return schritt with { Anteil = schritt.Anteil * 0.89f };
        if (_wirtschaft is not { } w) yield break;
        yield return new Ladeschritt(0.89f, "Werkstätten öffnen ihre Läden …");
        foreach (var betrieb in w.Betriebe)
            BetriebHinzufuegen(betrieb);
        Ladezeit.Marke("Szene: Betriebe (Werkstätten, Umland)");
        yield return new Ladeschritt(0.985f, "Die Bürger ziehen ein …");
        foreach (var frei in w.FreieLiegenschaften)
            FreienPlatzZeigen(frei);
        Ladezeit.Marke("Szene: freie Plätze");
        WohnhausRaeumeAnlegen(); // Wohnstube und Kontor im eigenen Wohnhaus (Stadtansicht.Wohnhaus.cs)
        Ladezeit.Marke("Szene: Räume des Wohnhauses");
        WohnhaeuserPruefen(); // die Wohnhäuser der Karte, wie sie der Kern vergeben hat (sonst erst im ersten Aktualisieren)
        GrundstueckeAktualisieren(); // Baustellen und Bauplätze (Stadtansicht.Bauen.cs)
        Ladezeit.Marke("Szene: Wohnhäuser und Grundstücke");
    }

    /// <summary>
    /// Während des Aufbaus hinter dem Ladebildschirm ruht die Stadt: kein <c>_Process</c> (Gras, Wegeschicht, Kamera), keine Eingabe.
    /// </summary>
    public void Ruhen(bool ruhen)
    {
        SetProcess(!ruhen);
        SetProcessInput(!ruhen);
        SetProcessUnhandledInput(!ruhen);
    }

    private Stadtwirtschaft? _wirtschaft;

    public override void _ExitTree()
    {
        Fenster.BloomGeaendert -= BloomNachfuehren;
        Fenster.VerdeckungGeaendert -= VerdeckungNachfuehren;
        GrafikAufgeben();
        WetterAufgeben(); // die globalen Shader-Werte zurück auf Sommer und trocken (Stadtansicht.Wetter.cs)
        OberflaecheAufgeben();
        GlobaleBeleuchtungAufgeben();
        if (_wirtschaft is not null)
        {
            _wirtschaft.BetriebAbgegeben -= BetriebEntfernen;
            _wirtschaft.Wegebau.BelagGeaendert -= BelagVeraltetMelden;
        }
    }

    private Konto? _spielerKonto;

    /// <summary>Baut einen Betrieb an seinem Platz – auch mitten im Spiel, wenn er gerade erworben wurde.</summary>
    public void BetriebHinzufuegen(Betrieb betrieb)
    {
        if (_betriebe.ContainsKey(betrieb)) return;
        if (PlatzVon(betrieb.Name) is not { } platz)
        {
            GD.PushWarning($"Für „{betrieb.Name}“ gibt es keinen Platz in der Karte – er wird nicht gezeigt.");
            return;
        }
        // Stand hier ein freier Platz (Bauplatz, leere Grube oder leeres Haus), weicht er dem neuen Betrieb.
        if (_freiePlaetze.Remove(betrieb.Name, out var alt))
            KnotenEntfernen(alt);
        _leereHaeuser.Remove(betrieb.Name);
        GrasPlatz(platz.Mitte, platz.Groesse); // der Grasteppich weicht dem Gebäude (Stadtansicht.Gras.cs)

        var spieler = betrieb.Konto == _spielerKonto;
        var farbe = Besitzerfarbe(betrieb.Konto);
        var vorher = GetChildCount(); // alles ab hier gehört zum Betrieb und kommt in seine Gruppe
        // Werkstätten nach Beruf (eine ausgebaute Drechslerei bleibt eine Tischlerwerkstatt), Umland nach dem Baustein seines Platzes
        var ansicht = betrieb.InDerStadt
            ? WerkstattBauen(betrieb, platz, farbe, StilVon(betrieb.Artdaten.Beruf))
            : UmlandBauen(betrieb, platz, farbe);
        ansicht.Spieler = spieler;
        ansicht.Farbe = farbe;
        ansicht.Gruppe = InGruppe("Betrieb_" + betrieb.Name.Replace(" ", "_"), vorher);
        if (betrieb.InDerStadt && platz.Drehung != 0)
        {
            // Eine gedrehte Werkstatt: Gebäude samt Innenraum um die Mitte drehen, die Wände schauen mit
            var basis = UmMitteDrehen(ansicht.Gruppe, platz.Mitte, platz.Drehung, ansicht.Innen);
            for (var i = 0; i < ansicht.Waende.Count; i++)
                ansicht.Waende[i] = (ansicht.Waende[i].Wand, basis * ansicht.Waende[i].Aussen);
        }
        if (KarrenAnker(betrieb.Name) is { } halt)
        {
            // Karren starten und halten am „Anker_Karren“ des Modells (erst jetzt, nach dem Drehen, liegt er an seinem Platz) und fahren von dort zum Hof und weiter
            ansicht.Hof = halt;
            if (ansicht.WegZumMarkt[0].DistanceTo(halt) > 0.1f) ansicht.WegZumMarkt = ansicht.WegZumMarkt.Prepend(halt).ToArray();
        }
        _betriebe[betrieb] = ansicht;
        GlobaleBeleuchtungNeuAnlegen(); // SDFGI sieht das neue Gebäude sonst erst, wenn die Kamera weiterwandert
    }

    /// <summary>Werkstil nach Beruf – eine ausgebaute Drechslerei bleibt eine Tischlerwerkstatt.</summary>
    private static Werkstil StilVon(string beruf) => beruf switch
    {
        "Tischler" => Werkstil.Tischlerei, "Steinmetz" => Werkstil.Maurerei, "Alchimist" => Werkstil.Tinkturei, "Wirt" => Werkstil.Schaenke,
        "Prediger" => Werkstil.Kirche, "Dieb" => Werkstil.Diebesnest, "Gardist" => Werkstil.Wache, "Geldleiher" => Werkstil.Bank, "Parfumeur" => Werkstil.Siederei, _ => Werkstil.Giesserei,
    };

    /// <summary>
    /// Hängt alles, was seit <paramref name="abKind"/> direkt an der Stadt dazukam, an eine neue Gruppe. Die Bausteine eines
    /// Gebäudes hängen einzeln an der Stadt (wie Rathaus und Wohnhäuser); so lässt es sich trotzdem in einem Zug entfernen.
    /// Die Gruppe liegt im Ursprung – alle Lagen bleiben, wie sie sind.
    /// </summary>
    private Node3D InGruppe(string name, int abKind)
    {
        var teile = GetChildren().Skip(abKind).ToList();
        var gruppe = new Node3D { Name = name };
        AddChild(gruppe);
        foreach (var teil in teile)
        {
            RemoveChild(teil);
            gruppe.AddChild(teil);
        }
        return gruppe;
    }

    /// <summary>
    /// Ein Betrieb wurde verkauft oder seine Pacht gekündigt (Ereignis des Kerns): Gebäude, Innenraum und Karren verschwinden,
    /// an ihre Stelle tritt der freie Platz – in der Stadt das leere Haus mit dem Schild „zu verkaufen“, vor der Stadt die leere Grube.
    /// </summary>
    private void BetriebEntfernen(Betrieb betrieb, LiegenschaftDaten frei)
    {
        if (_betriebe.Remove(betrieb, out var ansicht))
        {
            if (_innen == ansicht) InnenansichtVerlassen(); // wer gerade drin ist, steht wieder in der Stadt
            if (ansicht.Gruppe is not null) KnotenEntfernen(ansicht.Gruppe);
        }
        KarrenOhneBetriebEntfernen(); // sie gehen mit dem Betrieb an den Käufer
        FreienPlatzZeigen(frei);
        GlobaleBeleuchtungNeuAnlegen();
    }

    /// <summary>Karren, die zu keinem Betrieb mehr gehören: mit dem Betrieb verkauft oder einzeln verkauft (PR #205).</summary>
    private void KarrenOhneBetriebEntfernen()
    {
        foreach (var karren in _karren.Keys.Where(k => !_betriebe.Keys.Any(b => b.Karren.Contains(k))).ToList())
        {
            _karren[karren].Wurzel.QueueFree();
            _karren.Remove(karren);
        }
    }

    /// <summary>
    /// Farbe der Fahnen, Dächer und Karren eines Hauses: aus der Startbelegung der Karte (Spieler blau, Haus Eisenbart rot, Haus Eichwald grün,
    /// Haus Kieselbach ocker in der Greybox); ein Haus ohne Farbe in der Karte ist rot, der Spieler blau.
    /// </summary>
    private Color Besitzerfarbe(Konto konto)
    {
        var istSpieler = konto == _spielerKonto;
        var text = (_karte?.Startbelegung ?? Startbelegung.Ersatz).Farbe(konto.Inhaber, istSpieler);
        return text is not null && Color.HtmlIsValid(text) ? Color.FromHtml(text) : istSpieler ? Spieler : Rivale;
    }

    /// <summary>Kennung eines freien Platzes für Mausklicks.</summary>
    public static string LiegenschaftKennung(string name) => "liegenschaft:" + name;

    /// <summary>
    /// Ein freier Platz: in der Stadt ein eingezäuntes Grundstück, vor der Stadt das Modell des Betriebs im Leerstand (<see cref="UmlandPlatzZeigen"/>).
    /// Alles hängt an einem Knoten, damit es beim Erwerb in einem Zug verschwindet.
    /// </summary>
    private void FreienPlatzZeigen(LiegenschaftDaten frei)
    {
        if (PlatzVon(frei.Name) is not { } p) return;
        GrasPlatz(p.Mitte, p.Groesse); // auf einem freien Werkstattplatz wächst wieder Gras (Stadtansicht.Gras.cs)
        var art = _simulation?.Wirtschaft?.Daten.Art(frei.Art);
        if (frei.Bebaut && art is { InDerStadt: true })
        {
            LeeresHausZeigen(frei, p, art); // verkaufte Werkstatt: Das Haus steht noch
            return;
        }
        var gruppe = new Node3D { Name = "Frei_" + frei.Name.Replace(" ", "_") };
        AddChild(gruppe);
        if (art is { InDerStadt: false })
        {
            UmlandPlatzZeigen(frei, p, gruppe); // Modell mit „Leerstand“, Schild, Klickfläche und Beschriftung
            _freiePlaetze[frei.Name] = gruppe;
            return;
        }
        // Zaun um das Grundstück (die ganze Fläche mit Vorplatz) und ein Haufen Bauholz
        var holz = Material(Holz);
        var (hb, ht) = (p.Groesse.X / 2, p.Groesse.Y / 2);
        var m = p.Mitte;
        foreach (var (mitte, groesse) in new[]
        {
            (new Vector3(0, 0.5f, -ht), new Vector3(2 * hb, 1, 0.15f)), (new Vector3(0, 0.5f, ht), new Vector3(2 * hb, 1, 0.15f)),
            (new Vector3(-hb, 0.5f, 0), new Vector3(0.15f, 1, 2 * ht)), (new Vector3(hb, 0.5f, 0), new Vector3(0.15f, 1, 2 * ht)),
        })
            Quader(m + mitte, groesse, holz, gruppe);
        Quader(m + new Vector3(-2, 0.3f, 2), new Vector3(3, 0.6f, 1.2f), holz, gruppe);

        Verkaufsschild(m + new Vector3(-6, 0, ht + 0.6f), frei.IstPacht, gruppe);
        Klickflaeche(LiegenschaftKennung(frei.Name), m + new Vector3(0, 2, 0), new Vector3(2 * hb, 4, 2 * ht), gruppe);
        Beschriftung($"{frei.Name}\n{(frei.IstPacht ? "zu verpachten" : "zu verkaufen")}",
            m + new Vector3(0, 6, 1), 36, gruppe, LiegenschaftKennung(frei.Name));
        UmMitteDrehen(gruppe, m, p.Drehung);
        _freiePlaetze[frei.Name] = gruppe;
    }

    /// <summary>
    /// Schild am Pfosten – ohne Text, die Farbe sagt es: Ocker = zu verkaufen, Grün = zu verpachten.
    /// Name und Preis zeigt die Beschriftung, sobald die Maus darüber steht.
    /// </summary>
    private void Verkaufsschild(Vector3 fuss, bool pacht, Node3D? eltern = null)
    {
        var holz = Material(Holz);
        var tafel = Material(pacht ? new Color(0.35f, 0.6f, 0.32f) : new Color(0.86f, 0.66f, 0.28f));
        tafel.EmissionEnabled = true; // leuchtet leicht, damit es auch nachts zu sehen ist
        tafel.Emission = tafel.AlbedoColor * 0.35f;
        Quader(fuss + new Vector3(0, 1.8f, 0), new Vector3(0.22f, 3.6f, 0.22f), holz, eltern);
        Quader(fuss + new Vector3(0, 3.2f, 0.15f), new Vector3(2.6f, 1.5f, 0.12f), tafel, eltern);
        Quader(fuss + new Vector3(0, 3.2f, 0), new Vector3(2.6f, 1.5f, 0.12f), tafel, eltern); // dieselbe Tafel von hinten: Draußen schaut die Vorderseite oft von der Kamera weg
        Quader(fuss + new Vector3(0, 3.2f, 0.08f), new Vector3(2.9f, 1.8f, 0.06f), holz, eltern); // Rahmen
    }

    /// <summary>Verkaufte Werkstätten, die leer zum Verkauf stehen: Name → Mitte, Gruppe und Dach (für den Diorama-Schnitt des Doms).</summary>
    private readonly Dictionary<string, (Vector3 Mitte, Node3D Gruppe, Node3D Dach)> _leereHaeuser = new();

    /// <summary>
    /// Die Datei des Werkstatt-Modells eines Berufs aus dem Gebäudekatalog (<c>schmied</c>, <c>geldleiher</c> …). Die Modelle sind 16 × 14 m groß
    /// (mit Vorplatz) und haben ihren Ursprung in der Mitte der Fläche, die Front 2 m hinter der Vorderkante (siehe <see cref="ModellEinsetzen"/>).
    /// </summary>
    private string WerkstattModellDatei(string beruf) => Katalog?.Werkstatt(beruf)?.Modell ?? "gebaeude/" + beruf.ToLowerInvariant();

    /// <summary>Die Betriebsstufe der Modelle (Knoten „Stufe1“ bis „Stufe3“).</summary>
    private static int Modellstufe(int stufe) => Math.Clamp(stufe, 1, 3);

    /// <summary>
    /// Eine verkaufte Werkstatt steht leer zum Verkauf: das Modell in seiner Stufe, aber mit „Leerstand“ (Tor und Fenster vernagelt, dunkles Glas),
    /// ohne Wappen, Fahne und Besitzerfarbe, davor das ockerfarbene Schild. Kein Innenraum – betreten
    /// lässt es sich erst wieder, wenn es jemand gekauft hat (dann weicht es dem neuen Betrieb, <see cref="BetriebHinzufuegen"/>).
    /// </summary>
    private void LeeresHausZeigen(LiegenschaftDaten frei, Platz p, BetriebsartDaten art)
    {
        var vorher = GetChildCount(); // alles ab hier kommt in die Gruppe des leeren Hauses
        var modell = ModellEinsetzen(WerkstattModellDatei(art.Beruf), this, p.Mitte,
                                     new ModellZustand(Stufe: Modellstufe(art.Stufe), Leerstand: true));
        Verkaufsschild(p.Mitte + new Vector3(3.5f, 0, p.Groesse.Y / 2 - 0.4f), frei.IstPacht); // rechts vom Tor: links stehen in den Modellen Vordach und Karrenplatz

        var kennung = LiegenschaftKennung(frei.Name);
        Klickflaeche(kennung, p.Mitte + new Vector3(0, 5, 0), new Vector3(14, 10, 14)); // das Gebäude des Modells mit dem Schild vor der Tür
        Beschriftung($"{frei.Name}\n{(frei.IstPacht ? "zu verpachten" : "zu verkaufen")}",
            p.Mitte + new Vector3(0, 13, 1), 36, kennung: kennung);
        var gruppe = InGruppe("Leer_" + frei.Name.Replace(" ", "_"), vorher);
        UmMitteDrehen(gruppe, p.Mitte, p.Drehung); // eine gedrehte Werkstatt
        _freiePlaetze[frei.Name] = gruppe;
        _leereHaeuser[frei.Name] = (p.Mitte, gruppe, modell?.Wurzel ?? gruppe);
    }

    /// <summary>Entfernt einen Knoten samt Kindern – und vergisst seine Beschriftungen, die sonst ins Leere zeigen.</summary>
    private void KnotenEntfernen(Node3D knoten)
    {
        _beschriftungen.RemoveAll(label => label == knoten || knoten.IsAncestorOf(label));
        _kulisseAusgeblendet.RemoveAll(teil => teil == knoten || knoten.IsAncestorOf(teil));
        knoten.QueueFree();
    }

    /// <summary>
    /// Werkstattarten nach Beruf (<see cref="Werkstil"/>): bestimmt die Einrichtung der Räume (Raumansicht, siehe Stadtansicht.Raeume.cs).
    /// Von außen steht das Modell des Berufs (Stufe, Gerüst, Besitzerfarbe, Glut bei der Arbeit).
    /// </summary>
    private enum Werkstil { Giesserei, Tischlerei, Maurerei, Tinkturei, Schaenke, Kirche, Diebesnest, Wache, Bank, Siederei }

    /// <summary>
    /// Eine Werkstatt auf ihrem Platz: das Modell des Berufs in der Mitte der Fläche (ungedreht; <see cref="BetriebHinzufuegen"/> dreht die ganze
    /// Gruppe), Klickfläche, Schild und Innenraum. Die Karren fahren vom Hof über das Gassennetz zum Markt (<see cref="MarktWeg"/>).
    /// </summary>
    private BetriebsAnsicht WerkstattBauen(Betrieb betrieb, Platz p, Color besitzer, Werkstil stil = Werkstil.Giesserei)
    {
        const float breite = 10, tiefe = 8, dicke = 0.4f;
        if (p.Gebaeude is null) PlatzSockel(p.Mitte, p.Drehung, p.Groesse); // ein Neubau auf einem Grundstück; feste Plätze haben ihren Sockel aus der Karte
        var modell = ModellEinsetzen(WerkstattModellDatei(betrieb.Artdaten.Beruf), this, p.Mitte,
                                     new ModellZustand(Stufe: Modellstufe(betrieb.Stufe), Geruest: betrieb.UmbauMinuten > 0, Besitzer: besitzer), betrieb.Name);

        // Klickfläche: das Gebäude des Modells (Halle und Anbauten), nicht der Hof ringsum
        Klickflaeche(BetriebsKennung(betrieb), p.Mitte + new Vector3(0, 5, 0), new Vector3(14, 10, 12));
        var schild = Beschriftung(betrieb.Name, p.Mitte + new Vector3(0, 13, 1), 44, kennung: BetriebsKennung(betrieb));

        var hof = p.Hof;
        var ansicht = new BetriebsAnsicht
        {
            Betrieb = betrieb,
            Mitte = p.Mitte,
            Hof = hof,
            WegZumMarkt = MarktWeg(hof, HofwegBreite), // eine neu gebaute Werkstatt bekommt hier ihr Stück zur nächsten Gasse
            Modell = modell,
            Schild = schild,
            Dach = modell?.Wurzel,
            GezeigteStufe = betrieb.Stufe,
            GezeigterUmbau = betrieb.UmbauMinuten > 0,
        };
        Innenraum(ansicht, stil, breite, tiefe, dicke);
        if (betrieb.UmbauMinuten > 0) StufeZeigen(ansicht); // die Beschriftung „(Umbau)“
        return ansicht;
    }

    /// <summary>
    /// Die Gebäudestufe von außen: Das Modell zeigt den Knoten „Stufe1“ bis „Stufe3“ der Betriebsstufe, während eines Umbaus steht das Gerüst davor.
    /// Die Grundfläche bleibt gleich – die Nachbarn rücken nicht zusammen.
    /// </summary>
    private void StufeZeigen(BetriebsAnsicht a)
    {
        var b = a.Betrieb;
        a.GezeigteStufe = b.Stufe;
        a.GezeigterUmbau = b.UmbauMinuten > 0;
        if (a.Modell is { } modell) ModellStufeSetzen(modell, Modellstufe(b.Stufe), b.UmbauMinuten > 0);
        if (a.Schild is not null)
            a.Schild.Text = b.Name + (b.UmbauMinuten > 0 ? " (Umbau)" : ""); // die Stufe zeigt das Modell – eine zweite Zeile überdeckte die Nachbarn
    }


    public static string BetriebsKennung(Betrieb b) => "betrieb:" + b.Name;

    /// <summary>Jedes Bild: Licht nach Uhrzeit, Esse, Karren.</summary>
    public void Aktualisieren(double delta)
    {
        Feuer.UhrStellen(delta); // die gemeinsame Uhr aller Flammen und des Rauchs (Feuer.cs), auch wenn kein Feuer ein eigenes Licht hat
        if (_simulation is null) return;
        var uhr = Ladezeit.Uhr(); // --ladezeit=1: was die ersten Bilder kosten (Ladezeit.cs)
        WohnhaeuserPruefen(); // einmal, sobald der Kern die Wohnhäuser vergeben hat
        Ladezeit.Dazu("im Bild: Wohnhäuser setzen", uhr);
        uhr = Ladezeit.Uhr();
        GrundstueckeAktualisieren(); // Wohnhäuser, Baustellen und Bauplätze der Karte (Stadtansicht.Bauen.cs)
        Ladezeit.Dazu("im Bild: Grundstücke", uhr);
        BauUmrissAktualisieren();    // Baumodus: der Umriss an der Maus
        var zeit = _simulation.Zeit;
        // Die Simulation zählt nur ganze Minuten (bei Tempo 1 etwa eine pro Sekunde). Für die Darstellung
        // nehmen wir den Bruchteil der angebrochenen Minute dazu – sonst springen Sonne, Schatten und Karren
        // einmal pro Sekunde, statt sich jedes Bild ein Stückchen weiterzubewegen.
        var bruchteil = _simulation.Takt.AngebrocheneMinute;
        FensterNachtSetzen(zeit.VergangeneMinuten); // jede Nacht andere Fenster hell (Stadtansicht.Modelle.cs)
        WetterAktualisieren(zeit.VergangeneMinuten + bruchteil, zeit.Jahreszeit, delta); // vor dem Licht: Wolken dämpfen es (Stadtansicht.Wetter.cs)
        LichtNachUhrzeit(_lichtUhrVorgabe is { } lichtUhr ? lichtUhr * 60f : zeit.UhrzeitInMinuten + (float)bruchteil, delta, GezeigteJahreszeit(zeit.Jahreszeit)); // Stadtansicht.Licht.cs
        NachtlichterAktualisieren(delta); // Stadtansicht.Nachtlichter.cs
        PassantenAktualisieren(zeit.VergangeneMinuten + bruchteil, delta); // Stadtansicht.Passanten.cs
        JahreszeitSetzen(GezeigteJahreszeit(zeit.Jahreszeit)); // Laub je Jahreszeit, im Winter kahl (Stadtansicht.Natur.cs)
        VoegelAktualisieren(zeit.UhrzeitInMinuten + (float)bruchteil, delta, _simulation.Takt.IstPausiert); // Stadtansicht.Voegel.cs

        if (_simulation.Wirtschaft is not { } w) return;
        var arbeitszeit = w.IstArbeitszeit(zeit);
        if (!_simulation.Takt.IstPausiert) _animationszeit += delta;

        foreach (var (betrieb, ansicht) in _betriebe)
        {
            if (betrieb.InDerStadt && (betrieb.Stufe != ansicht.GezeigteStufe || (betrieb.UmbauMinuten > 0) != ansicht.GezeigterUmbau))
                StufeZeigen(ansicht);
            var hierArbeitszeit = w.IstArbeitszeit(betrieb, zeit); // im Wirtshaus bis 22 Uhr
            var arbeitet = hierArbeitszeit && betrieb.Belegt > 0 && betrieb.Stillstand is null;
            // Der Feuerkorb des Wachturms brennt auch nachts, solange ein Gardist Wache hält
            var glueht = arbeitet || (betrieb.Art == Betriebsart.Wachturm && betrieb.Belegt > 0 && betrieb.Stillstand is null && _fensterLicht > 0);
            if (ansicht.Modell is { Glut.Length: > 0 } modell && ansicht.Glueht != glueht)
            {
                ansicht.Glueht = glueht; // das Material „Glut“ (Esse, Ofen, Athanor, Feuerkorb) nur beim Wechsel schalten
                GlutSetzen(modell, glueht);
            }
            if (ansicht.Modell is { } haus) FenstergruppeSetzen(haus, FenstergruppeVon(betrieb, w)); // Stadtansicht.Modelle.cs, nur beim Wechsel
            WerkstattBrandPruefen(betrieb, ansicht, zeit.VergangeneMinuten); // ein Feuer im Betrieb (Stadtansicht.Feuer.cs)
            if (ansicht == _innen)
                InnenAktualisieren(ansicht, zeit, hierArbeitszeit, arbeitet, delta);

            for (var i = 0; i < betrieb.Karren.Count; i++)
            {
                // Hin- und Rückfahrt dauern auf Gelände verschieden lang (der Kern rechnet die Steigung ein, Stadtwirtschaft.Fahrzeit); in der Ebene gleich lang wie immer
                var fahrzeit = w.Fahrzeit(betrieb, zumMarkt: betrieb.Karren[i].Zustand != KarrenZustand.ZumBetrieb);
                KarrenBewegen(betrieb.Karren[i], i, ansicht, fahrzeit, zeit.VergangeneMinuten + bruchteil, delta, ansicht.Spieler);
            }
        }
        if (_karren.Keys.Any(k => !k.Heimat.Karren.Contains(k))) KarrenOhneBetriebEntfernen(); // ein Karren ist verkauft
        if (_domFenster is { } dom) // der Dom: schwacher Schein, zur Messe heller
            FenstergruppeSetzen(dom, _messeVorgegeben || (w.Kirchenwesen.IstAktiv && zeit.UhrzeitInMinuten / 60 == w.Kirchenwesen.Daten.MesseUm)
                ? Fenstergruppe.Messe : Fenstergruppe.Kirche);
        SchiffeAktualisieren(w, zeit.VergangeneMinuten + bruchteil, delta); // Stadtansicht.Schiffe.cs
        NachtwaechterAktualisieren(w, zeit, bruchteil, delta); // Wachleute auf Streife (Stadtansicht.Nachtlichter.cs)
        FeuerAktualisieren(w, zeit, bruchteil, delta); // Kaminrauch und brennende Häuser (Stadtansicht.Feuer.cs)
        StadtbauAktualisieren(w); // Bauvorhaben und Fest der Stadt (Stadtansicht.Stadtbau.cs)
        if (_innen is SonderbauAnsicht sonderbau)
            SonderbauAktualisieren(sonderbau, zeit, arbeitszeit, delta);
    }

    // ── Innenansicht betreten und verlassen ─────────────────────────────────

    /// <summary>Fährt mit der Kamera in den Betrieb und schneidet ihn auf. Gibt false zurück, wenn er keinen Innenraum hat.</summary>
    public bool InnenansichtBetreten(Betrieb betrieb) =>
        _betriebe.TryGetValue(betrieb, out var ansicht) && Betreten(ansicht);

    /// <summary>Betritt das Gebäude mit dieser Kennung („Rathaus", „Dom" oder „betrieb:…").</summary>
    public bool InnenansichtBetreten(string kennung) => AnsichtZu(kennung) is { } ansicht && Betreten(ansicht);

    private bool Betreten(InnenAnsicht ansicht)
    {
        if (!ansicht.HatInnenraum) return false;
        if (_innen == ansicht) return true;
        if (_innen is null)
        {
            _vorherPosition = _kameraDrehung.Position;
            _vorherZoom = _kamera.Size;
        }
        else InnenraumZeigen(_innen, false);

        _innen = ansicht;
        InnenraumZeigen(ansicht, true);
        if (ansicht.Raumansicht) RaumZeigen(ansicht.Raeume[0], melden: false);
        else Kamerafahrt(ansicht.Mitte, ansicht.Zoom);
        InnenansichtGewechselt?.Invoke(ansicht.Kennung);
        if (_raum is not null) RaumGewechselt?.Invoke(_raum.Kennung);
        return true;
    }

    /// <summary>Zurück in die Stadt: Gebäude wieder schließen, Kamera an die alte Stelle.</summary>
    public void InnenansichtVerlassen()
    {
        if (_innen is null) return;
        var raumansicht = _innen.Raumansicht;
        InnenraumZeigen(_innen, false);
        _innen = null;
        if (!raumansicht) Kamerafahrt(_vorherPosition, _vorherZoom);
        InnenansichtGewechselt?.Invoke(null);
        if (raumansicht) RaumGewechselt?.Invoke(null);
    }

    private void InnenraumZeigen(InnenAnsicht ansicht, bool innen)
    {
        if (ansicht.Raumansicht)
        {
            RaumansichtUmschalten(ansicht, innen);
            if (innen) InnenAuffuellen(ansicht);
            return;
        }
        ansicht.Innen!.Visible = innen;
        if (ansicht.Dach is not null) ansicht.Dach.Visible = !innen;
        if (ansicht.Schild is not null) ansicht.Schild.Visible = !innen;
        foreach (var (wand, _) in ansicht.Waende) wand.Visible = true;
        // Drinnen treffen Klicks die Räume, draußen das ganze Gebäude.
        if (ansicht.Aussenklick is not null) ansicht.Aussenklick.Disabled = innen;
        foreach (var klick in ansicht.ZusatzKlicks) klick.Disabled = innen;
        foreach (var raum in ansicht.Raumklicks) raum.Disabled = !innen;
        KulisseZeigen();
        if (!innen) return;
        InnenAuffuellen(ansicht);
        Aufschneiden(ansicht);
    }

    /// <summary>Beim Betreten sofort einmal auffüllen, damit nichts „nachploppt".</summary>
    private void InnenAuffuellen(InnenAnsicht ansicht)
    {
        if (_simulation?.Wirtschaft is { } w)
        {
            var zeit = _simulation.Zeit;
            var arbeitszeit = w.IstArbeitszeit(zeit);
            if (ansicht is BetriebsAnsicht b)
            {
                var hierArbeitszeit = w.IstArbeitszeit(b.Betrieb, zeit); // im Wirtshaus bis 22 Uhr
                InnenAktualisieren(b, zeit, hierArbeitszeit, hierArbeitszeit && b.Betrieb.Belegt > 0 && b.Betrieb.Stillstand is null, 0);
            }
            else if (ansicht is SonderbauAnsicht s)
                SonderbauAktualisieren(s, zeit, arbeitszeit, 0);
        }
    }

    /// <summary>
    /// Der Diorama-Schnitt: Jede Wand, deren Außenseite zur Kamera zeigt, wird ausgeblendet.
    /// Beim diagonalen Blick sind das genau zwei Wände – man schaut über die offene Ecke hinein.
    /// Das Skalarprodukt (Dot) misst, wie sehr zwei Richtungen übereinstimmen: 1 = gleich, 0 = quer, −1 = entgegen.
    /// </summary>
    private void Aufschneiden(InnenAnsicht ansicht)
    {
        var zurKamera = RichtungZurKamera;
        foreach (var (wand, aussen) in ansicht.Waende)
            wand.Visible = aussen.Dot(zurKamera) <= 0.5f;
        KulisseAusblenden(ansicht, zurKamera);
    }

    /// <summary>
    /// Blendet Nachbarhäuser, Bäume und Mauerstücke aus, die zwischen Kamera und Werkstatt stehen.
    /// „Davor" heißt: ein Stück in Richtung Kamera (vorne) und nicht zu weit seitlich daneben.
    /// Flaches (Boden, Wege) und bewegliche Dinge (Karren) bleiben immer sichtbar.
    /// </summary>
    private void KulisseAusblenden(InnenAnsicht ansicht, Vector3 zurKamera)
    {
        KulisseZeigen();
        var eigene = new HashSet<Node>(ansicht.Waende.Select(x => (Node)x.Wand));
        // Betriebe und leere Häuser hängen in eigenen Gruppen (im Ursprung, die Lagen gelten also wie an der Stadt).
        var gruppen = _betriebe.Values.Select(b => b.Gruppe).Concat(_leereHaeuser.Values.Select(l => l.Gruppe)).OfType<Node3D>();
        foreach (var kind in GetChildren().Concat(gruppen.SelectMany(g => g.GetChildren())))
        {
            if (kind is not MeshInstance3D teil || eigene.Contains(teil) || teil.Mesh is PlaneMesh || teil.Position.Y < 0.2f)
                continue;
            var abstand = teil.Position - ansicht.Mitte;
            abstand.Y = 0;
            var vorne = abstand.Dot(zurKamera);
            var seitlich = (abstand - zurKamera * vorne).Length();
            if (vorne is > 4.5f and < 28f && seitlich < 11f && teil.Visible)
            {
                teil.Visible = false;
                _kulisseAusgeblendet.Add(teil);
            }
        }
        // Dächer anderer Werkstätten, des Rathauses und des Doms sind eigene Gruppen – die gleichen Regeln gelten.
        var daecher = _betriebe.Values.Cast<InnenAnsicht>().Concat(_sonderbauten.Values).Where(x => x != ansicht && (x.Huelle ?? x.Dach) is not null)
            .Select(x => (x.Mitte, Dach: (x.Huelle ?? x.Dach)!)).Concat(_leereHaeuser.Values.Select(l => (l.Mitte, l.Dach)));
        foreach (var (mitte, dach) in daecher)
        {
            var abstand = mitte - ansicht.Mitte;
            var vorne = abstand.Dot(zurKamera);
            if (vorne is > 4.5f and < 28f && (abstand - zurKamera * vorne).Length() < 11f && dach.Visible)
            {
                dach.Visible = false;
                _kulisseAusgeblendet.Add(dach);
            }
        }
    }

    private void KulisseZeigen()
    {
        foreach (var teil in _kulisseAusgeblendet) teil.Visible = true;
        _kulisseAusgeblendet.Clear();
    }

    private void Kamerafahrt(Vector3 ziel, float zoom)
    {
        if (_innen is null)
        {
            // draußen bleibt das Ziel im Spielfeld und der Zoom unter dem größten (Stadtansicht.Rand.cs)
            if (KameraRechteck() is { } r) ziel = new Vector3(Mathf.Clamp(ziel.X, (float)r.X0, (float)r.X1), 0, Mathf.Clamp(ziel.Z, (float)r.Z0, (float)r.Z1));
            zoom = Mathf.Min(zoom, KameraGroessterZoom());
        }
        ziel.Y = 0; // die Höhe trägt die Neigung (KameraHoeheNachfuehren)
        _kamerafahrt?.Kill();
        _kamerafahrt = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _kamerafahrt.TweenProperty(_kameraDrehung, "position", ziel, 0.6);
        _kamerafahrt.TweenProperty(_kamera, "size", zoom, 0.6);
    }

    /// <summary>Stellt die Kamera auf einen Punkt der Stadt (Meter) und optional auf eine Zoomstufe – für Startoption und Klicktest.</summary>
    public void KameraSetzen(float x, float z, float? zoom = null)
    {
        _kameraDrehung.Position = new Vector3(x, 0, z);
        KameraHoeheSofort();
        if (zoom is { } z2) _kamera.Size = Mathf.Clamp(z2, KameraMinZoom, KameraGroessterZoom());
    }

    /// <summary>Fährt die Kamera sanft zu einem Gebäude (Betriebsleiste der Oberfläche, PR #197), einem Hafen oder Schiff (PR #199), der Zoom bleibt. False, wenn es die Kennung nicht gibt oder man drinnen ist.</summary>
    public bool Hinfahren(string kennung)
    {
        if (_innen is not null || (AnsichtZu(kennung)?.Mitte ?? HafenOderSchiffMitte(kennung)) is not { } mitte) return false;
        Kamerafahrt(mitte, _kamera.Size);
        return true;
    }

    /// <summary>Dreht die Kamera um ganze Vierteldrehungen (+1 = 90° gegen den Uhrzeigersinn von oben).</summary>
    public void Drehen(int vierteldrehungen, bool sofort = false)
    {
        _zielDrehung += 90f * vierteldrehungen;
        if (sofort) _kameraDrehung.RotationDegrees = new Vector3(0, _zielDrehung, 0);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Kamera und Auswahl
    // ═════════════════════════════════════════════════════════════════════════

    public override void _Process(double delta)
    {
        var frei = FreieKameraBild(); // --freiekamera=1: die Spielkamera folgt der freien (Stadtansicht.FreieKamera.cs)
        SchattenNachfuehren(); // Zoom, Fenstergröße oder Schattenqualität geändert? (Stadtansicht.Licht.cs)
        OberflaecheBild();     // umgefärbte Materialien der Oberflächendetails nachziehen (Stadtansicht.Oberflaechendetails.cs)
        GrafikBild(delta);     // Unschärfe nach Zoom, Wolken, Dunst nach Uhrzeit (Stadtansicht.Grafik.cs)
        DunstFarbeNachfuehren(); // der Dunst am Kartenrand in der Farbe des Himmels (Stadtansicht.Rand.cs)
        WasserBild(delta);       // Eis auf dem Wasser, Fähren, Tretrad der Häfen (Stadtansicht.Wasser.Uebergaenge.cs)
        BelagBild();             // geänderte Wege neu zeichnen (Stadtansicht.Wegbelag.cs)
        WegeModusBild();         // Wege-Modus: Leuchten nachziehen, Weg unter der Maus (Stadtansicht.Wegemodus.cs)
        var uhr = Ladezeit.Uhr(); // --ladezeit=1: was die ersten Bilder kosten (Ladezeit.cs)
        WegeschichtBild();       // geänderte Kacheln der Wegeschicht neu rechnen (Stadtansicht.Wegeschicht.cs)
        Ladezeit.Dazu("im Bild: Wegeschicht", uhr);
        uhr = Ladezeit.Uhr();
        BueschelBild();          // Grasbüschel an den Grenzen der Wiese nachziehen (Stadtansicht.Bueschel.cs)
        Ladezeit.Dazu("im Bild: Grasbüschel", uhr);
        uhr = Ladezeit.Uhr();
        GrasBild();              // Grasteppich: geänderte Kacheln, Felder im Bild (Stadtansicht.Gras.cs)
        Ladezeit.Dazu("im Bild: Grasteppich", uhr);

        // Beschriftungen mitskalieren, damit sie bei jedem Zoom gleich groß wirken.
        var massstab = _raum is not null ? RaumSchildMassstab : _kamera.Size / BezugsZoom;
        foreach (var label in _beschriftungen)
            label.Scale = Vector3.One * massstab;
        BeschriftungenZeigen();

        // Sanft auf die Zieldrehung zudrehen (Q/E)
        var aktuell = _kameraDrehung.RotationDegrees.Y;
        _kameraDrehung.RotationDegrees = new Vector3(0, Mathf.LerpAngle(Mathf.DegToRad(aktuell), Mathf.DegToRad(_zielDrehung), (float)Math.Min(1, delta * 8)) * 180 / Mathf.Pi, 0);
        if (!frei) KameraGrenzenAnwenden(delta); // zurückfedern, Zoom begrenzen (Stadtansicht.Rand.cs)
        KameraHoeheNachfuehren(delta); // mit dem Gelände steigen und fallen (Stadtansicht.Gelaende.cs)
        if (_innen is { Raumansicht: false }) Aufschneiden(_innen);
        if (_raum is not null)
        {
            RaumBildAktualisieren(delta); // Blickpunkte, Rand, Namen unter der Maus (Stadtansicht.Innenkamera.cs)
            return; // feste Ansicht: im Raum wird nicht verschoben
        }

        // Verschieben mit Tastatur – nicht beim Schreiben in ein Textfeld, nicht mit Strg (Strg+S speichert in der Kartenwerkstatt) und nicht mit freier Kamera
        if (frei || GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit || Input.IsKeyPressed(Key.Ctrl)) return;
        var richtung = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) richtung.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) richtung.Y += 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) richtung.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) richtung.X += 1;
        if (richtung != Vector2.Zero)
            Verschieben(richtung.Normalized() * (float)delta * _kamera.Size * 0.8f);
    }

    private void Verschieben(Vector2 bildschirm)
    {
        var basis = _kameraDrehung.GlobalTransform.Basis;
        var rechts = new Vector3(basis.X.X, 0, basis.X.Z).Normalized();
        var vor = new Vector3(basis.Z.X, 0, basis.Z.Z).Normalized();
        var neu = _kameraDrehung.Position + rechts * bildschirm.X + vor * bildschirm.Y;
        if (_innen is not null)
        {
            // In der Innenansicht nur ein Stück um das Gebäude herum verschieben.
            var m = _innen.Mitte;
            var r = _innen.Reichweite;
            _kameraDrehung.Position = new Vector3(Mathf.Clamp(neu.X, m.X - r, m.X + r), 0, Mathf.Clamp(neu.Z, m.Z - r, m.Z + r));
            return;
        }
        if (KameraRechteck() is not null)
        {
            _kameraDrehung.Position = KameraGebremst(_kameraDrehung.Position, neu); // das Spielfeld der Karte, am Rand weich gebremst (Stadtansicht.Rand.cs)
            return;
        }
        var rahmen = _kartenRahmen.Grow(KameraRand); // ohne Karte: bei den Gebäuden bleiben
        _kameraDrehung.Position = new Vector3(Mathf.Clamp(neu.X, rahmen.Position.X, rahmen.End.X), 0, Mathf.Clamp(neu.Z, rahmen.Position.Y, rahmen.End.Y));
    }

    public override void _UnhandledInput(InputEvent eingabe)
    {
        if (_raum is not null)
        {
            RaumEingabe(eingabe);
            return;
        }
        // Mausrad, rechte Maustaste und Q/E gehören der freien Kamera, wenn sie aktiv ist (Stadtansicht.FreieKamera.cs)
        if (FreiAktiv && eingabe is InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown or MouseButton.Right }
                or InputEventKey { Keycode: Key.Q or Key.E })
            return;
        if (BauEingabe(eingabe) || WerkstattEingabe(eingabe)) // Baumodus: R dreht, Klick setzt (Stadtansicht.Bauen.cs); Kartenwerkstatt (Stadtansicht.Kartenwerkstatt.cs)
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        switch (eingabe)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                Zoomen(0.9f);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                Zoomen(1.1f);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right } rechts:
                _ziehen = rechts.Pressed;
                break;
            case InputEventMouseMotion bewegung when _ziehen:
                // Pixel → Meter: die sichtbare Höhe entspricht _kamera.Size
                var meterJePixel = _kamera.Size / GetViewport().GetVisibleRect().Size.Y;
                Verschieben(-bewegung.Relative * meterJePixel);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } klick:
                Auswaehlen(klick.Position, klick.DoubleClick);
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Q }:
                _zielDrehung -= 90;
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.E }:
                _zielDrehung += 90;
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    private void Zoomen(float faktor)
    {
        _kamerafahrt?.Kill(); // Mausrad unterbricht eine laufende Kamerafahrt
        var (min, max) = _innen is null ? (KameraMinZoom, KameraGroessterZoom()) : (InnenMinZoom, InnenMaxZoom);
        _kamera.Size = Mathf.Clamp(_kamera.Size * faktor, min, max);
    }

    /// <summary>Das ausgewählte Gebäude (setzt die Hauptszene) – sein Name bleibt sichtbar.</summary>
    public string? Auswahl { get; set; }

    /// <summary>Das Gebäude unter der Maus (null über Tafeln oder freiem Gelände).</summary>
    private string? _unterDerMaus;

    /// <summary>
    /// Namen in der Stadt nur, wo man sie braucht: unter der Maus, beim ausgewählten Gebäude und – solange Alt
    /// gedrückt ist – überall. Im Innenraum keine (dort zeigen Schilder und Infotafel, was los ist).
    /// </summary>
    private void BeschriftungenZeigen()
    {
        // Über einer Tafel zählt die Stadt dahinter nicht.
        _unterDerMaus = GetViewport().GuiGetHoveredControl() is null ? KennungBei(GetViewport().GetMousePosition()) : null;
        var alle = Input.IsKeyPressed(Key.Alt);
        foreach (var label in _beschriftungen)
        {
            if (!label.HasMeta("kennung")) continue;
            var kennung = label.GetMeta("kennung").AsString();
            var sichtbar = _innen is null && _wsEbenen[Werkstattebene.Beschriftungen] && (alle || kennung == _unterDerMaus || kennung == Auswahl);
            if (label.Visible != sichtbar) label.Visible = sichtbar;
        }
    }

    /// <summary>Welches Gebäude liegt an dieser Bildschirmstelle? (Strahl aus der Kamera, trifft die Klickflächen)</summary>
    private string? KennungBei(Vector2 bildschirm)
    {
        if (PassantBei(bildschirm) is { } einwohner) return einwohner; // Figuren vor Gebäuden (Stadtansicht.Passanten.cs)
        var kamera = _raum is not null ? _raumKamera : StadtKamera;
        var von = kamera.ProjectRayOrigin(bildschirm);
        var bis = von + kamera.ProjectRayNormal(bildschirm) * 500;
        var treffer = GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(von, bis));
        return treffer.Count > 0 && treffer["collider"].As<Node>() is { } koerper && koerper.HasMeta("kennung")
            ? koerper.GetMeta("kennung").AsString() : null;
    }

    private void Auswaehlen(Vector2 mausposition, bool doppelklick)
    {
        if (_wegeModus)
        {
            // Wege-Modus des Baumenüs: nur Wege sind anklickbar, Gebäude und Boden nicht (Stadtansicht.Wegemodus.cs)
            if (_innen is null && GetViewport().GuiGetHoveredControl() is null && BodenPunktBei(mausposition) is { } wegBoden && WegBeiPunkt(wegBoden) is var (wegNr, wegStelle))
                WegAngeklickt?.Invoke(wegNr, wegStelle);
            return;
        }
        if (VogelAufscheuchen(mausposition)) return; // Klick auf einen Vogel (Stadtansicht.Voegel.cs)
        if (KennungBei(mausposition) is not { } kennung) return;
        GebaeudeAngeklickt?.Invoke(kennung);
        if (doppelklick && AnsichtZu(kennung) is { } ansicht)
            Betreten(ansicht);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Bausteine der Greybox
    // ═════════════════════════════════════════════════════════════════════════

    private static StandardMaterial3D Material(Color farbe) => new() { AlbedoColor = farbe, Roughness = 0.95f, ResourceName = StoffNamen.GetValueOrDefault(farbe, "") };

    /// <summary>
    /// Namen für die Farben der Greybox (Wiese, Pflaster, Mauerstein …): Die Oberflächendetails suchen über den Namen die Materialart
    /// (Gras, Pflaster, Holz …, <c>materialarten.json</c>), die Farben selbst kennen sie nicht. Das Spiel liest diese Namen sonst nicht.
    /// </summary>
    private static readonly Dictionary<Color, string> StoffNamen = new()
    {
        [Wiese] = "Gras", [Pflaster] = "Pflaster", [Weg] = "Erde", [Mauerstein] = "Mauerstein", [Putz] = "Putz", [Fachwerk] = "Fachwerk",
        [Ziegel] = "Dachziegel", [Schiefer] = "Schiefer", [Holz] = "Holz", [Bruchstein] = "Bruchstein", [Kraut] = "Kraut", [Morsch] = "Holz alt",
        [Wasser] = "Wasser",
    };

    /// <summary>Ein Quader. Ohne <paramref name="eltern"/> hängt er direkt an der Stadt,
    /// sonst an einem anderen Knoten (dann ist <paramref name="mitte"/> relativ zu diesem).</summary>
    private MeshInstance3D Quader(Vector3 mitte, Vector3 groesse, StandardMaterial3D material, Node3D? eltern = null)
    {
        var quader = new MeshInstance3D { Mesh = new BoxMesh { Size = groesse }, MaterialOverride = material, Position = mitte };
        (eltern ?? this).AddChild(quader);
        return quader;
    }

    private void Flaeche(Vector3 mitte, Vector2 groesse, Color farbe, float hoehe)
    {
        AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = groesse }, MaterialOverride = Material(farbe), Position = mitte + new Vector3(0, hoehe, 0) });
    }

    /// <summary>Satteldach: ein Prisma, dessen First entlang Z (quer = entlang X) läuft.</summary>
    private void Dach(Vector3 unterkante, Vector3 groesse, StandardMaterial3D material, bool quer, Node3D? eltern = null)
    {
        var dach = new MeshInstance3D
        {
            Mesh = new PrismMesh { Size = quer ? new Vector3(groesse.Z, groesse.Y, groesse.X) : groesse },
            MaterialOverride = material,
            Position = unterkante + new Vector3(0, groesse.Y / 2, 0),
        };
        if (quer) dach.RotationDegrees = new Vector3(0, 90, 0);
        (eltern ?? this).AddChild(dach);
    }

    /// <summary>Ein Turm mit spitzem Dach (rund oder eckig).</summary>
    private void Turm(Vector3 grund, float radius, float hoehe, Color dach, bool eckig = false, Node3D? eltern = null)
    {
        var segmente = eckig ? 4 : 10;
        var schaft = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = hoehe, RadialSegments = segmente },
            MaterialOverride = Material(Mauerstein),
            Position = grund + new Vector3(0, hoehe / 2, 0),
        };
        var spitze = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = radius * 1.25f, Height = radius * 2.6f, RadialSegments = segmente },
            MaterialOverride = Material(dach),
            Position = grund + new Vector3(0, hoehe + radius * 1.3f, 0),
        };
        if (eckig) { schaft.RotationDegrees = new Vector3(0, 45, 0); spitze.RotationDegrees = new Vector3(0, 45, 0); }
        (eltern ?? this).AddChild(schaft);
        (eltern ?? this).AddChild(spitze);
    }

    /// <summary>Ein flacher Weg zwischen zwei Punkten. Über den Marktplatz führt keiner – dort liegt das Pflaster des Modells (Stadtansicht.Markt.cs).</summary>
    private void WegZiehen(Vector3 von, Vector3 bis, float breite, Node3D? eltern = null)
    {
        if (WegeschichtMoeglich && _grenzen is not null)
        {
            SchichtAnschlussMerken(von, bis, breite); // in den Boden eingebacken (Stadtansicht.Wegeschicht.cs)
            return;
        }
        // Gruppen (Betrieb, freier Platz) liegen beim Ziehen noch im Ursprung: Ihre Wege sind in Koordinaten der Stadt.
        if ((eltern is null || eltern.Transform.IsEqualApprox(Transform3D.Identity)) && OhneMarktplatz(von, bis, breite) is { } stuecke)
        {
            foreach (var (a, b) in stuecke) WegStueck(a, b, breite, eltern ?? this);
            return;
        }
        WegStueck(von, bis, breite, eltern ?? this);
    }

    private void WegStueck(Vector3 von, Vector3 bis, float breite, Node3D eltern)
    {
        var mitte = (von + bis) / 2;
        var laenge = von.DistanceTo(bis);
        var gier = Mathf.Atan2(bis.X - von.X, bis.Z - von.Z);
        var weg = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(breite, 0.06f, laenge + breite * 0.5f) },
            MaterialOverride = Material(Weg),
            Position = new Vector3(mitte.X, 0.04f, mitte.Z),
            Rotation = new Vector3(0, gier, 0),
        };
        if (BodenBewegt)
        {
            // am Hang: auf der Höhe des Bodens in der Mitte, entlang des Gefälles geneigt (gerechnet in der Ebene, nicht im Raum: von und bis sind unten)
            var (ha, hb) = (BodenHoehe(von.X, von.Z), BodenHoehe(bis.X, bis.Z));
            var waagerecht = new Vector2(bis.X - von.X, bis.Z - von.Z).Length();
            weg.Position = new Vector3(mitte.X, (ha + hb) / 2 + 0.04f, mitte.Z);
            weg.Basis = new Basis(Vector3.Up, gier) * new Basis(Vector3.Right, -Mathf.Atan2(hb - ha, Mathf.Max(0.001f, waagerecht)));
        }
        eltern.AddChild(weg);
    }

    /// <summary>
    /// Ein Weg als ein flaches Band entlang einer Linie (glatte Kurven): oben auf derselben Höhe wie die Quader von <see cref="WegStueck"/>,
    /// an den Enden um ein Viertel der Breite länger wie sie. Weniger als zwei Punkte: nichts.
    /// </summary>
    private void WegBand(IReadOnlyList<Vector3> punkte, float breite, Node3D eltern, Func<float, float, float>? boden = null)
    {
        if (punkte.Count < 2) return;
        var linie = punkte.Select(p => new Vector2(p.X, p.Z)).ToList();
        // an beiden Enden verlängern (wie die Quader)
        linie[0] -= (linie[1] - linie[0]).Normalized() * breite * 0.25f;
        linie[^1] += (linie[^1] - linie[^2]).Normalized() * breite * 0.25f;
        var ecken = BandDreiecke(linie, breite / 2, WegHoehe, boden);
        if (ecken.Length == 0) return;
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = ecken;
        arrays[(int)Mesh.ArrayType.Normal] = ecken.Select(e => BodenNormale(e.X, e.Z)).ToArray();
        var netz = new ArrayMesh();
        netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        eltern.AddChild(new MeshInstance3D { Name = "Wegband", Mesh = netz, MaterialOverride = Material(Weg) });
    }

    /// <summary>Höhe der Oberseite eines Weges (die Quader von <see cref="WegStueck"/>: Mitte 0,04 m, 6 cm dick).</summary>
    private const float WegHoehe = 0.07f;

    /// <summary>
    /// Die Dreiecke eines flachen Bandes um eine Linie (x, z) in der Höhe <paramref name="hoehe"/>, je drei Ecken, mit der Vorderseite nach
    /// oben. An den Knicken liegen die Ränder auf der Winkelhalbierenden (so überlappen sich die Stücke nicht – wichtig für Durchsichtiges;
    /// in sehr spitzen Knicken höchstens dreifach so weit). <paramref name="boden"/> ersetzt die Höhe des Bodens (<see cref="BodenHoehe(float, float)"/>), etwa in einer Furt.
    /// </summary>
    private Vector3[] BandDreiecke(IReadOnlyList<Vector2> linie, float halb, float hoehe, Func<float, float, float>? boden = null)
    {
        // doppelte Punkte (ein Knick ohne Länge) weglassen – ihre Richtung wäre Zufall
        linie = linie.Where((p, i) => i == 0 || p.DistanceTo(linie[i - 1]) > 0.01f).ToList();
        if (linie.Count < 2) return Array.Empty<Vector3>();
        if (BodenBewegt) linie = Verfeinert(linie, BandSchritt); // auf Gelände folgt das Band dem Boden: Punkte höchstens alle BandSchritt Meter
        var links = new Vector2[linie.Count];
        var rechts = new Vector2[linie.Count];
        for (var i = 0; i < linie.Count; i++)
        {
            var vor = i > 0 ? (linie[i] - linie[i - 1]).Normalized() : Vector2.Zero;
            var nach = i + 1 < linie.Count ? (linie[i + 1] - linie[i]).Normalized() : Vector2.Zero;
            var richtung = (vor + nach).Normalized();
            if (richtung == Vector2.Zero) richtung = nach != Vector2.Zero ? nach : vor != Vector2.Zero ? vor : Vector2.Right;
            var quer = new Vector2(-richtung.Y, richtung.X);
            var bezug = nach != Vector2.Zero ? nach : vor;
            var gehrung = Mathf.Min(3, 1 / Mathf.Max(0.33f, Mathf.Abs(quer.Dot(new Vector2(-bezug.Y, bezug.X)))));
            links[i] = linie[i] + quer * halb * gehrung;
            rechts[i] = linie[i] - quer * halb * gehrung;
        }
        var ecken = new List<Vector3>();
        for (var i = 0; i + 1 < linie.Count; i++)
            foreach (var (a, b, c) in new[] { (links[i], rechts[i], links[i + 1]), (rechts[i], rechts[i + 1], links[i + 1]) })
                Dreieck(ecken, a, b, c, hoehe, boden: boden);
        return ecken.ToArray();
    }

    /// <summary>Wie weit (Meter) auf Gelände die Punkte eines Bandes und die Kanten eines Dreiecks höchstens auseinander liegen.</summary>
    private const float BandSchritt = 2f, DreieckSchritt = 4f;

    /// <summary>
    /// Das Pflaster der Stadt bedeckt den Boden ganz, aber unter den Wegen (7 cm): 3 cm über dem Boden, genau auf seinen Dreiecken (<see cref="RasterDreiecke"/>).
    /// Bauzonen (durchscheinend, über allem) liegen ebenso 2 cm über ihrer Fläche.
    /// </summary>
    private const float PflasterAmHang = 0.03f, ZoneAmHang = 0.02f;

    /// <summary>Die Linie mit zusätzlichen Punkten, so dass keine Strecke länger als <paramref name="schritt"/> ist (die Punkte der Linie bleiben).</summary>
    private static List<Vector2> Verfeinert(IReadOnlyList<Vector2> linie, float schritt)
    {
        var aus = new List<Vector2> { linie[0] };
        for (var i = 1; i < linie.Count; i++)
        {
            var (a, b) = (linie[i - 1], linie[i]);
            var teile = Mathf.CeilToInt(a.DistanceTo(b) / schritt);
            for (var k = 1; k < teile; k++) aus.Add(a.Lerp(b, (float)k / teile));
            aus.Add(b);
        }
        return aus;
    }

    /// <summary>
    /// Ein Dreieck, Vorderseite nach oben: in (x, z) gerechnet gegen den Uhrzeigersinn (wie bei <see cref="Vieleck"/>), <paramref name="hoehe"/> Meter
    /// über dem Boden. Auf Gelände wird es in kleine Dreiecke geteilt (Kanten bis <see cref="DreieckSchritt"/> Meter), jede Ecke liegt auf dem Boden.
    /// </summary>
    private void Dreieck(List<Vector3> ecken, Vector2 a, Vector2 b, Vector2 c, float hoehe, float schritt = DreieckSchritt, Func<float, float, float>? boden = null)
    {
        if ((b - a).Cross(c - a) < 0) (b, c) = (c, b);
        if (!BodenBewegt)
        {
            DreieckFlach(ecken, a, b, c, hoehe);
            return;
        }
        DreieckAmBoden(ecken, a, b, c, hoehe, schritt, 0, boden ?? (Func<float, float, float>)BodenHoehe);
    }

    private void DreieckAmBoden(List<Vector3> ecken, Vector2 a, Vector2 b, Vector2 c, float hoehe, float schritt, int tiefe, Func<float, float, float> boden)
    {
        var (ab, bc, ca) = (a.DistanceTo(b), b.DistanceTo(c), c.DistanceTo(a));
        var laengste = Mathf.Max(ab, Mathf.Max(bc, ca));
        if (laengste <= schritt || tiefe >= 24)
        {
            foreach (var p in new[] { a, b, c }) ecken.Add(new Vector3(p.X, hoehe + boden(p.X, p.Y), p.Y));
            return;
        }
        // an der längsten Kante in der Mitte teilen
        if (laengste == ab) { var m = (a + b) / 2; DreieckAmBoden(ecken, a, m, c, hoehe, schritt, tiefe + 1, boden); DreieckAmBoden(ecken, m, b, c, hoehe, schritt, tiefe + 1, boden); }
        else if (laengste == bc) { var m = (b + c) / 2; DreieckAmBoden(ecken, a, b, m, hoehe, schritt, tiefe + 1, boden); DreieckAmBoden(ecken, a, m, c, hoehe, schritt, tiefe + 1, boden); }
        else { var m = (c + a) / 2; DreieckAmBoden(ecken, a, b, m, hoehe, schritt, tiefe + 1, boden); DreieckAmBoden(ecken, m, b, c, hoehe, schritt, tiefe + 1, boden); }
    }

    /// <summary>Ein flaches Dreieck in der Höhe <paramref name="hoehe"/> (über y = 0, Vorderseite nach oben wie <see cref="Dreieck"/>): für Dunst und alles, was nicht dem Boden folgt.</summary>
    private static void DreieckFlach(List<Vector3> ecken, Vector2 a, Vector2 b, Vector2 c, float hoehe)
    {
        if ((b - a).Cross(c - a) < 0) (b, c) = (c, b);
        ecken.Add(new Vector3(a.X, hoehe, a.Y));
        ecken.Add(new Vector3(b.X, hoehe, b.Y));
        ecken.Add(new Vector3(c.X, hoehe, c.Y));
    }

    /// <summary>Unsichtbarer Körper, damit Mausklicks das Gebäude treffen (um die Hochachse gedreht wie das Gebäude). Die Form lässt sich abschalten (Innenansicht).</summary>
    private CollisionShape3D Klickflaeche(string kennung, Vector3 mitte, Vector3 groesse, Node3D? eltern = null, float drehung = 0)
    {
        var koerper = new StaticBody3D { Position = mitte, Rotation = new Vector3(0, drehung, 0) };
        koerper.SetMeta("kennung", kennung);
        var form = new CollisionShape3D { Shape = new BoxShape3D { Size = groesse } };
        koerper.AddChild(form);
        (eltern ?? this).AddChild(koerper);
        return form;
    }

    /// <param name="kennung">Kennung des Gebäudes (wie bei der Klickfläche). Mit Kennung ist es ein Name in der Stadt:
    /// Er erscheint nur unter der Maus, beim ausgewählten Gebäude oder mit gedrückter Alt-Taste (<see cref="BeschriftungenZeigen"/>).
    /// Ohne Kennung (Schilder im Innenraum) ist die Beschriftung immer sichtbar.</param>
    private Label3D Beschriftung(string text, Vector3 position, int groesse, Node3D? eltern = null, string? kennung = null)
    {
        var label = new Label3D
        {
            Text = text,
            Position = position,
            FontSize = groesse,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            OutlineSize = 14,
            OutlineModulate = new Color(0.08f, 0.07f, 0.06f),
            Modulate = new Color(0.97f, 0.94f, 0.86f),
            PixelSize = 0.04f,
        };
        if (kennung is not null)
        {
            label.SetMeta("kennung", kennung);
            label.Font = Schriften.Ueberschrift;
            label.Visible = false;
        }
        else
        {
            // Schilder im Raum liegen auf hellem Boden und Mauerwerk: ein dicker, deckender dunkler Rand ist ihr Grund (F8, Dom innen) – und die Zeilen halten Abstand
            label.OutlineSize = 30;
            label.OutlineModulate = new Color(0.07f, 0.06f, 0.05f);
            label.LineSpacing = 6;
        }
        _beschriftungen.Add(label);
        (eltern ?? this).AddChild(label);
        return label;
    }
}
