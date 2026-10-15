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
///   • Die Esse einer Gießerei glüht, solange dort gearbeitet wird
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
    private static readonly Color Marktpflaster = new(0.55f, 0.51f, 0.46f);
    private static readonly Color Weg = new(0.45f, 0.36f, 0.26f);
    private static readonly Color Mauerstein = new(0.52f, 0.49f, 0.45f);
    private static readonly Color Putz = new(0.80f, 0.75f, 0.64f);
    private static readonly Color Fachwerk = new(0.62f, 0.55f, 0.45f);
    private static readonly Color Ziegel = new(0.55f, 0.25f, 0.18f);
    private static readonly Color Schiefer = new(0.28f, 0.30f, 0.34f);
    private static readonly Color Fels = new(0.40f, 0.37f, 0.34f);
    private static readonly Color Tanne = new(0.16f, 0.27f, 0.17f);
    private static readonly Color Holz = new(0.36f, 0.25f, 0.16f);
    private static readonly Color Spieler = new(0.20f, 0.42f, 0.72f);
    private static readonly Color Rivale = new(0.70f, 0.20f, 0.16f);
    private static readonly Color ZweiterRivale = new(0.22f, 0.55f, 0.28f);
    private static readonly Color DritterRivale = new(0.85f, 0.68f, 0.18f);
    private static readonly Color Bruchstein = new(0.68f, 0.65f, 0.60f);
    private static readonly Color Kraut = new(0.30f, 0.52f, 0.25f);
    private static readonly Color Wasser = new(0.25f, 0.42f, 0.55f);
    private static readonly Color Korn = new(0.80f, 0.68f, 0.30f);
    private static readonly Color Morsch = new(0.33f, 0.29f, 0.25f);

    private const float KameraMinZoom = 25f;
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
    private readonly Dictionary<Karren, Node3D> _karren = new();
    private readonly List<Label3D> _beschriftungen = new();
    private const float BezugsZoom = 95f;

    /// <summary>Wird ausgelöst, wenn ein Gebäude angeklickt wird. Übergeben wird seine Kennung.</summary>
    public event Action<string>? GebaeudeAngeklickt;

    /// <summary>Wird ausgelöst, wenn eine Innenansicht betreten (Kennung des Gebäudes) oder verlassen (null) wird.</summary>
    public event Action<string?>? InnenansichtGewechselt;

    /// <summary>Wird ausgelöst, wenn in der Raumansicht ein anderer Raum gezeigt wird (Kennung des Raums, null beim Verlassen).</summary>
    public event Action<string?>? RaumGewechselt;

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
        public required Vector3 Mitte { get; init; }
        /// <summary>Kennung wie beim Anklicken („betrieb:…", „Rathaus", „Dom").</summary>
        public abstract string Kennung { get; }
        public abstract string Name { get; }
        public Label3D? Schild { get; init; }

        /// <summary>Jede Wand mit ihrer Außenrichtung (zeigt vom Gebäude weg).</summary>
        public List<(Node3D Wand, Vector3 Aussen)> Waende { get; } = new();
        public Node3D? Dach { get; set; }
        public Node3D? Innen { get; set; }
        public bool HatInnenraum => Innen is not null;
        /// <summary>Zoom beim Betreten und wie weit man die Kamera im Innern verschieben darf.</summary>
        public float Zoom { get; init; } = InnenZoom;
        public float Reichweite { get; init; } = 8;
        /// <summary>Klickfläche des ganzen Gebäudes – in der Innenansicht aus, damit die Räume erreichbar sind.</summary>
        public CollisionShape3D? Aussenklick { get; set; }
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
        public required Vector3 Hof { get; init; }
        public required Vector3[] WegZumMarkt { get; init; }
        public bool Spieler { get; set; }
        public override string Kennung => BetriebsKennung(Betrieb);
        public override string Name => Betrieb.Name;

        /// <summary>Farbe des Besitzers (Fahne, Karren).</summary>
        public Color Farbe { get; set; }
        public OmniLight3D? Glut { get; init; }
        public StandardMaterial3D? Esse { get; init; }

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
        /// <summary>Dachgaube, Dachreiter und Gerüst – hängt am Dach, verschwindet also in der Innenansicht mit.</summary>
        public Node3D? Ausbau { get; set; }
        public int GezeigteStufe { get; set; } = 1;
        public bool GezeigterUmbau { get; set; }
    }

    /// <summary>Eine Figur: Körper, der bewegliche Arm (mit Werkzeug), ihr Takt, was sie tut und wo der Arm ruht.</summary>
    private sealed record Arbeiterfigur(Node3D Koerper, Node3D Arm, float Takt, Bewegung Art, Vector3 ArmRuhe);

    // Feste Punkte der Stadt (Meter). Der Marktplatz liegt im Ursprung.
    public static readonly Vector3 Marktplatz = Vector3.Zero;
    public static readonly Vector3 Osttor = new(36, 0, 0);

    /// <summary>Ende der Landstraße vor dem Osttor.</summary>
    private static readonly Vector3 Landstrasse = new(56, 0, -2);

    /// <summary>
    /// Der Stadtplan: Wo steht welcher Betrieb (Mitte des Grundstücks)? Die Namen sind dieselben wie in
    /// der Simulation und in wirtschaft.json („liegenschaften"). Ein neuer Bauplatz braucht hier einen Eintrag.
    /// </summary>
    public static readonly Dictionary<string, Vector3> Stadtplan = new()
    {
        ["Gießerei am Weinmarkt"] = new(-20, 0, 14),
        ["Tischlerei am Weinmarkt"] = new(-20, 0, 14), // derselbe Platz – je nach Startberuf
        ["Gießerei Eisenbart"] = new(18, 0, 16),
        ["Gießerei an der Südgasse"] = new(2, 0, 20),
        ["Tischlerei Eichwald"] = new(-14, 0, 27),
        ["Tischlerei am Holzmarkt"] = new(18, 0, -7),
        ["Maurerei am Weinmarkt"] = new(-20, 0, 14),    // derselbe Platz – je nach Startberuf
        ["Maurerei Kieselbach"] = new(25, 0, -29),
        ["Maurerei an der Stadtmauer"] = new(21, 0, 29),
        ["Steinbruch Kieselbach"] = new(40, 0, 52),
        ["Steinbruch am Felsenhang"] = new(-6, 0, -66),
        ["Tinkturei am Weinmarkt"] = new(-20, 0, 14),   // derselbe Platz – je nach Startberuf
        ["Tinkturei am Domplatz"] = new(-11, 0, -31),
        ["Kräutergarten am Bach"] = new(72, 0, 56),
        ["Schänke am Weinmarkt"] = new(-20, 0, 14),     // derselbe Platz – je nach Startberuf
        ["Schänke am Fischmarkt"] = new(-27, 0, 28),
        ["Bauernhof an der Mühle"] = new(90, 0, -48),
        ["Kirche am Weinmarkt"] = new(-20, 0, 14),      // derselbe Platz – je nach Startberuf
        ["Schmugglerloch am Weinmarkt"] = new(-20, 0, 14), // derselbe Platz – je nach Startberuf
        ["Wachhaus am Weinmarkt"] = new(-20, 0, 14),    // derselbe Platz – je nach Startberuf
        ["Wachhaus an der Torstraße"] = new(29, 0, 10),
        ["Wechselstube am Weinmarkt"] = new(-20, 0, 14), // derselbe Platz – je nach Startberuf
        ["Wechselstube am Markt"] = new(14, 0, -18),
        ["Siederei am Weinmarkt"] = new(-20, 0, 14),     // derselbe Platz – je nach Startberuf
        ["Siederei an der Seifengasse"] = new(-24.5f, 0, -29),
        ["Duftgarten am Lavendelhang"] = new(90, 0, 8),
        ["Wachturm an der Landstraße"] = new(46, 0, -10),
        ["Schmugglerloch am Osttor"] = new(29, 0, -12),
        ["Kirche an der Westmauer"] = new(-30, 0, -4),
        ["Klosterhof am Osthang"] = new(100, 0, 66),     // südöstlich hinter dem Kräutergarten – vor keinem Berg versteckt
        ["Grube Eisenbart"] = new(68, 0, -12),
        ["Grube am Silberberg"] = new(66, 0, 32),
        ["Grube im Tannenwald"] = new(48, 0, -52),
        ["Holzschlag Eichwald"] = new(20, 0, -62),
        ["Holzschlag im Eichenforst"] = new(14, 0, 66),
    };

    /// <summary>
    /// Werkstätten erreichen den Markt über eine Gasse – meist auf knapp halbem Weg zur Mitte. Wo dieser gerade Weg
    /// durch ein Nachbarhaus führte, stehen hier eigene Knicke (der Reihe nach, vom Betrieb zum Markt).
    /// </summary>
    private static readonly Dictionary<string, Vector3[]> Gassen = new()
    {
        ["Maurerei an der Stadtmauer"] = new Vector3[] { new(11, 0, 24) },                    // zwischen Südgasse und Gießerei Eisenbart
        ["Tinkturei am Domplatz"] = new Vector3[] { new(-11, 0, -4) },                        // zwischen Dom und Rathaus nach Süden
        ["Schänke am Fischmarkt"] = new Vector3[] { new(-20.5f, 0, 22), new(-8, 0, 21.5f) },  // an Weinmarkt und Eichwald vorbei
        ["Kirche an der Westmauer"] = new Vector3[] { new(-22, 0, 5), new(-12, 0, 6) },      // nördlich am Domturm vorbei
        ["Schmugglerloch am Osttor"] = new Vector3[] { new(26, 0, -1) },                     // auf die Torstraße, an der Tischlerei am Holzmarkt vorbei
    };

    private static Vector3[] Gasse(string name, Vector3 p) => Gassen.GetValueOrDefault(name) ?? new[] { new Vector3(p.X * 0.45f, 0, p.Z * 0.45f) };

    /// <summary>Weg vom Betrieb über die Gasse(n) zum Markt ziehen.</summary>
    private void GasseZiehen(Vector3 p, Vector3[] gasse, Node3D? eltern = null)
    {
        var punkte = gasse.Prepend(p).Append(Marktplatz).ToList();
        for (var i = 0; i + 1 < punkte.Count; i++)
            WegZiehen(punkte[i], punkte[i + 1], 3.5f, eltern);
    }

    /// <summary>Gruben liegen vor der Stadt: Hier zweigt ihr Weg von der Straße ab, die zum Osttor führt.</summary>
    private static readonly Dictionary<string, Vector3> Abzweige = new()
    {
        ["Grube Eisenbart"] = new(56, 0, -2),
        ["Grube am Silberberg"] = new(52, 0, 18),
        ["Grube im Tannenwald"] = new(46, 0, -26),
        ["Holzschlag Eichwald"] = new(42, 0, -20),
        ["Holzschlag im Eichenforst"] = new(46, 0, 26),
        ["Steinbruch Kieselbach"] = new(46, 0, 26),
        ["Steinbruch am Felsenhang"] = new(42, 0, -20),
        ["Kräutergarten am Bach"] = new(52, 0, 18),
        ["Bauernhof an der Mühle"] = new(46, 0, -26),
        ["Klosterhof am Osthang"] = new(64, 0, 43),      // auf dem Weg zum Kräutergarten
        ["Wachturm an der Landstraße"] = new(46, 0, -1), // direkt an der Landstraße
        ["Duftgarten am Lavendelhang"] = Landstrasse,      // am Ende der Landstraße weiter nach Osten
    };

    /// <summary>Höhe der Beschriftung über einem freien Platz, wo die übliche (6 m) eine Nachbarbeschriftung verdeckte.</summary>
    private static readonly Dictionary<string, float> SchildHoehe = new()
    {
        ["Wachhaus an der Torstraße"] = 9, // sonst über „Gießerei Eisenbart“
        ["Wechselstube am Markt"] = 8,     // sonst über „Tischlerei am Holzmarkt“
    };

    /// <summary>Höhe der Beschriftung über einem gebauten Werkstattplatz, wo die übliche (13 m) eine Nachbarbeschriftung verdeckte.</summary>
    private static readonly Dictionary<string, float> GebaeudeSchildHoehe = new()
    {
        ["Wechselstube am Markt"] = 7,  // sonst über „Schmugglerloch am Osttor“ oder „Rathaus“
    };

    /// <summary>Freie Bauplätze und Gruben, die noch niemandem gehören: Name → ihre Darstellung.</summary>
    private readonly Dictionary<string, Node3D> _freiePlaetze = new();

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau
    // ═════════════════════════════════════════════════════════════════════════

    public override void _Ready()
    {
        UmgebungUndLicht();
        KameraAufbauen();

        Flaeche(new Vector3(20, 0, 0), new Vector2(240, 200), Wiese, -0.05f);
        Flaeche(Vector3.Zero, new Vector2(72, 72), Pflaster, 0f);
        Flaeche(Vector3.Zero, new Vector2(24, 24), Marktpflaster, 0.02f);

        Stadtmauer();
        Marktstaende();
        RathausBauen(new Vector3(0, 0, -20)); // Rathaus und Dom mit Innenraum: Stadtansicht.Sonderbauten.cs
        DomBauen(new Vector3(-20, 0, -14));
        Wohnhaeuser();
        Wald();

        // Wege: Ostgasse zum Tor und hinaus zur Grube
        WegZiehen(Marktplatz, Osttor, 5);
        WegZiehen(Osttor, Landstrasse, 4);
    }

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

        _sonne = new DirectionalLight3D { ShadowEnabled = true, LightEnergy = 1.1f };
        _sonne.RotationDegrees = new Vector3(-50, 30, 0);
        AddChild(_sonne);
    }

    private void KameraAufbauen()
    {
        // Drehung (um die Hochachse) → Neigung (40° schräg von oben) → Kamera mit Abstand.
        _kameraDrehung = new Node3D { Name = "Kamerafuehrung", RotationDegrees = new Vector3(0, _zielDrehung, 0) };
        AddChild(_kameraDrehung);
        var neigung = new Node3D { RotationDegrees = new Vector3(-40, 0, 0) };
        _kameraDrehung.AddChild(neigung);
        _kamera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal, // keine Fluchtpunkte = klassischer 2,5D-Look
            Size = 95,
            Position = new Vector3(0, 0, 150),
            Far = 400,
        };
        neigung.AddChild(_kamera);
        _kameraDrehung.Position = new Vector3(24, 0, -6); // Stadt und Grube im Blick
        RaumKameraAufbauen();
    }

    private void Stadtmauer()
    {
        const float rand = 36, hoehe = 6, dicke = 2;
        var stein = Material(Mauerstein);
        // Nord, Süd, West als ganze Mauern; Ost mit Tor in der Mitte.
        Quader(new Vector3(0, hoehe / 2, -rand), new Vector3(2 * rand, hoehe, dicke), stein);
        Quader(new Vector3(0, hoehe / 2, rand), new Vector3(2 * rand, hoehe, dicke), stein);
        Quader(new Vector3(-rand, hoehe / 2, 0), new Vector3(dicke, hoehe, 2 * rand), stein);
        Quader(new Vector3(rand, hoehe / 2, -20), new Vector3(dicke, hoehe, 32), stein);
        Quader(new Vector3(rand, hoehe / 2, 20), new Vector3(dicke, hoehe, 32), stein);

        foreach (var ecke in new[] { new Vector3(-rand, 0, -rand), new Vector3(rand, 0, -rand), new Vector3(-rand, 0, rand), new Vector3(rand, 0, rand) })
            Turm(ecke, 3.5f, 10, Schiefer);

        // Torhaus über dem Osttor
        Quader(new Vector3(rand, 8, 0), new Vector3(5, 4, 9), stein);
        Turm(new Vector3(rand, 0, -5.5f), 2.6f, 11, Schiefer);
        Turm(new Vector3(rand, 0, 5.5f), 2.6f, 11, Schiefer);
        Klickflaeche("Osttor", new Vector3(rand, 5, 0), new Vector3(6, 10, 12));
        Beschriftung("Osttor", new Vector3(rand, 15, 0), 48, kennung: "Osttor");
    }

    private void Marktstaende()
    {
        var planen = new[] { new Color(0.72f, 0.62f, 0.38f), new Color(0.55f, 0.20f, 0.18f), new Color(0.25f, 0.38f, 0.30f), new Color(0.62f, 0.55f, 0.44f) };
        var i = 0;
        foreach (var (x, z) in new[] { (-8f, -8f), (0f, -9f), (8f, -8f), (-9f, 2f), (9f, 2f), (-4f, 8f), (4f, 8f) })
        {
            var p = new Vector3(x, 0, z);
            Quader(p + new Vector3(0, 0.6f, 0), new Vector3(3, 1.2f, 2), Material(Holz));
            Dach(p + new Vector3(0, 2.4f, 0), new Vector3(3.6f, 1f, 2.6f), Material(planen[i++ % planen.Length]), quer: true);
        }
        // Brunnen
        var brunnen = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1.6f, BottomRadius = 1.8f, Height = 1f }, MaterialOverride = Material(Mauerstein) };
        brunnen.Position = new Vector3(0, 0.5f, 0);
        AddChild(brunnen);
        Klickflaeche("Marktplatz", new Vector3(0, 1, 0), new Vector3(22, 2, 22));
        Beschriftung("Marktplatz", new Vector3(0, 6, 0), 56, kennung: "Marktplatz");
    }


    private void Wohnhaeuser()
    {
        // Feste Liste – jede Partie sieht gleich aus. Später erzeugt der Stadtplan die Häuser.
        var haeuser = new (float X, float Z, bool Quer)[]
        {
            (10, -29, true), // (−11|−29): Tinkturei am Domplatz; (−24,5|−29): Siederei an der Seifengasse; (19|−29), (28|−29): Maurerei Kieselbach
            (-4, 29, true), (5, 29, true), // (−29|29): Schänke am Fischmarkt; (29|29): Maurerei an der Stadtmauer
            // (−29|±6): Kirche an der Westmauer; (29|−12): Schmugglerloch am Osttor; (29|10): Wachhaus an der Torstraße;
            // (14|−18): Wechselstube am Markt
            (-29, 18, false),
        };
        var i = 0;
        foreach (var (x, z, quer) in haeuser)
        {
            var wand = i % 3 == 0 ? Fachwerk : Putz;
            var dach = i % 4 == 0 ? Schiefer : Ziegel;
            Haus(new Vector3(x, 0, z), new Vector3(7, 6 + i % 3, 6), wand, dach, quer);
            i++;
        }
    }

    private void Wald()
    {
        // Deterministischer „Zufall" – gleiche Bäume bei jedem Start.
        var zufall = new Random(1400);
        for (var i = 0; i < 70; i++)
        {
            var winkel = zufall.NextDouble() * Math.Tau;
            var abstand = 48 + zufall.NextDouble() * 50;
            var p = new Vector3((float)(Math.Cos(winkel) * abstand) + 10, 0, (float)(Math.Sin(winkel) * abstand));
            if (ImWegFuerWald(p)) continue; // Gruben und Straßen frei lassen
            var hoehe = 6 + (float)zufall.NextDouble() * 5;
            var baum = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = 2.2f, Height = hoehe, RadialSegments = 7 },
                MaterialOverride = Material(Tanne),
                Position = p + new Vector3(0, hoehe / 2, 0),
            };
            AddChild(baum);
        }
    }

    /// <summary>Liegt dieser Punkt auf einer Grube oder einem Weg? Dort wachsen keine Bäume.</summary>
    private static bool ImWegFuerWald(Vector3 p)
    {
        if (Mathf.Abs(p.Z) < 6 && p.X > 30) return true; // Straße vom Osttor
        foreach (var (name, abzweig) in Abzweige)
        {
            var grube = Stadtplan[name];
            if (p.DistanceTo(grube + new Vector3(10, 0, -4)) < 22) return true;
            if (AbstandZurStrecke(p, Osttor, abzweig) < 5 || AbstandZurStrecke(p, abzweig, grube) < 5) return true;
        }
        return false;
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
    /// Stellt die Betriebe der Simulation in die Stadt – jeden an seinen Platz laut <see cref="Stadtplan"/>.
    /// Freie Liegenschaften werden als Bauplatz bzw. leere Grube mit Schild gezeigt.
    /// </summary>
    public void Verbinden(Simulation simulation, Konto spieler)
    {
        _simulation = simulation;
        _spielerKonto = spieler;
        if (simulation.Wirtschaft is not { } w) return;
        foreach (var betrieb in w.Betriebe)
            BetriebHinzufuegen(betrieb);
        foreach (var frei in w.FreieLiegenschaften)
            FreienPlatzZeigen(frei);
    }

    private Konto? _spielerKonto;

    /// <summary>Baut einen Betrieb an seinem Platz – auch mitten im Spiel, wenn er gerade erworben wurde.</summary>
    public void BetriebHinzufuegen(Betrieb betrieb)
    {
        if (_betriebe.ContainsKey(betrieb)) return;
        if (!Stadtplan.TryGetValue(betrieb.Name, out var platz))
        {
            GD.PushWarning($"Für „{betrieb.Name}“ gibt es keinen Platz im Stadtplan – er wird nicht gezeigt.");
            return;
        }
        // Stand hier ein freier Platz (Bauplatz oder leere Grube), weicht er dem neuen Betrieb.
        if (_freiePlaetze.Remove(betrieb.Name, out var alt))
            KnotenEntfernen(alt);

        var spieler = betrieb.Konto == _spielerKonto;
        var farbe = Besitzerfarbe(betrieb.Konto);
        var abzweig = Abzweige.GetValueOrDefault(betrieb.Name, Osttor + new Vector3(20, 0, 0));
        // Werkstätten nach Beruf (eine ausgebaute Drechslerei bleibt eine Tischlerwerkstatt), Umland nach Art
        var ansicht = betrieb.InDerStadt
            ? GiessereiBauen(betrieb, platz, farbe, betrieb.Artdaten.Beruf switch
            {
                "Tischler" => Werkstil.Tischlerei, "Steinmetz" => Werkstil.Maurerei, "Alchimist" => Werkstil.Tinkturei, "Wirt" => Werkstil.Schaenke,
                "Prediger" => Werkstil.Kirche, "Dieb" => Werkstil.Diebesnest, "Gardist" => Werkstil.Wache, "Geldleiher" => Werkstil.Bank, "Parfumeur" => Werkstil.Siederei, _ => Werkstil.Giesserei,
            })
            : betrieb.Art switch
            {
                Betriebsart.Holzfaellerei => HolzschlagBauen(betrieb, platz, farbe, abzweig),
                Betriebsart.Steinbruch => SteinbruchBauen(betrieb, platz, farbe, abzweig),
                Betriebsart.Kraeutergarten => KraeutergartenBauen(betrieb, platz, farbe, abzweig),
                Betriebsart.Bauernhof => BauernhofBauen(betrieb, platz, farbe, abzweig),
                Betriebsart.Klosterhof => KlosterhofBauen(betrieb, platz, farbe, abzweig),
                Betriebsart.Wachturm => WachturmBauen(betrieb, platz, farbe, abzweig),
                Betriebsart.Duftgarten => DuftgartenBauen(betrieb, platz, farbe, abzweig),
                _ => MineBauen(betrieb, platz, farbe, abzweig),
            };
        ansicht.Spieler = spieler;
        ansicht.Farbe = farbe;
        _betriebe[betrieb] = ansicht;
    }

    /// <summary>Farbe der Fahnen und Karren: Spieler blau, Haus Eisenbart rot, Haus Eichwald grün, Haus Kieselbach ocker.</summary>
    private Color Besitzerfarbe(Konto konto) =>
        konto == _spielerKonto ? Spieler : konto.Inhaber == Partieaufbau.ZweiterRivale ? ZweiterRivale
        : konto.Inhaber == Partieaufbau.DritterRivale ? DritterRivale : Rivale;

    /// <summary>Kennung eines freien Platzes für Mausklicks.</summary>
    public static string LiegenschaftKennung(string name) => "liegenschaft:" + name;

    /// <summary>
    /// Ein freier Platz: in der Stadt ein eingezäuntes Grundstück, vor der Stadt eine Grube ohne Schuppen.
    /// Alles hängt an einem Knoten, damit es beim Erwerb in einem Zug verschwindet.
    /// </summary>
    private void FreienPlatzZeigen(LiegenschaftDaten frei)
    {
        if (!Stadtplan.TryGetValue(frei.Name, out var p)) return;
        var gruppe = new Node3D { Name = "Frei_" + frei.Name.Replace(" ", "_") };
        AddChild(gruppe);
        var holz = Material(Holz);

        var umland = _simulation?.Wirtschaft?.Daten.Art(frei.Art).Ort == Arbeitsort.Umland;
        if (frei.Art == Betriebsart.Holzfaellerei)
        {
            // Ein Kahlschlag mit Baumstümpfen
            foreach (var (dx, dz) in new[] { (-4f, -3f), (3f, -4f), (5f, 3f), (-2f, 4f), (0f, 0f) })
                Quader(p + new Vector3(dx, 0.4f, dz), new Vector3(1.2f, 0.8f, 1.2f), holz, gruppe);
            WegZiehen(Abzweige[frei.Name], p + new Vector3(-2, 0, 0), 4, gruppe);
            WegZiehen(Osttor, Abzweige[frei.Name], 4, gruppe);
        }
        else if (frei.Art == Betriebsart.Bauernhof)
        {
            Felder(p, gruppe);
            WegZiehen(Abzweige[frei.Name], p + new Vector3(-2, 0, 0), 4, gruppe);
            WegZiehen(Osttor, Abzweige[frei.Name], 4, gruppe);
        }
        else if (frei.Art == Betriebsart.Wachturm)
        {
            Quader(p + new Vector3(0, 0.3f, 0), new Vector3(4, 0.6f, 4), Material(Mauerstein), gruppe); // Fundament
            WegZiehen(Abzweige[frei.Name], p + new Vector3(-2, 0, 0), 4, gruppe);
        }
        else if (frei.Art == Betriebsart.Klosterhof)
        {
            Klostergarten(p, gruppe);
            WegZiehen(Abzweige[frei.Name], p + new Vector3(-2, 0, 0), 4, gruppe);
            WegZiehen(Osttor, Abzweige[frei.Name], 4, gruppe);
        }
        else if (frei.Art == Betriebsart.Duftgarten)
        {
            Duftbeete(p, gruppe);
            WegZiehen(Abzweige[frei.Name], p + new Vector3(-2, 0, 0), 4, gruppe);
        }
        else if (frei.Art == Betriebsart.Kraeutergarten)
        {
            Beete(p, gruppe);
            WegZiehen(Abzweige[frei.Name], p + new Vector3(-2, 0, 0), 4, gruppe);
            WegZiehen(Osttor, Abzweige[frei.Name], 4, gruppe);
        }
        else if (frei.Art == Betriebsart.Steinbruch)
        {
            FelsenBauen(p, gruppe);
            WegZiehen(Abzweige[frei.Name], p + new Vector3(-2, 0, 0), 4, gruppe);
            WegZiehen(Osttor, Abzweige[frei.Name], 4, gruppe);
        }
        else if (frei.Art == Betriebsart.Mine)
        {
            BergBauen(p, gruppe);
            Quader(p + new Vector3(2, 2, 0), new Vector3(3, 4, 4), Material(new Color(0.08f, 0.07f, 0.07f)), gruppe); // verlassener Stollen
            WegZiehen(Abzweige[frei.Name], p + new Vector3(-2, 0, 0), 4, gruppe);
            WegZiehen(Osttor, Abzweige[frei.Name], 4, gruppe);
        }
        else
        {
            // Zaun um das Grundstück (10 × 8 m) und ein Haufen Bauholz
            foreach (var (mitte, groesse) in new[]
            {
                (new Vector3(0, 0.5f, -4), new Vector3(10, 1, 0.15f)), (new Vector3(0, 0.5f, 4), new Vector3(10, 1, 0.15f)),
                (new Vector3(-5, 0.5f, 0), new Vector3(0.15f, 1, 8)), (new Vector3(5, 0.5f, 0), new Vector3(0.15f, 1, 8)),
            })
                Quader(p + mitte, groesse, holz, gruppe);
            Quader(p + new Vector3(-2, 0.3f, 1), new Vector3(3, 0.6f, 1.2f), holz, gruppe);
            GasseZiehen(p, Gasse(frei.Name, p), gruppe);
        }

        // Schild am Pfosten – ohne Text, die Farbe sagt es: Ocker = zu verkaufen, Grün = zu verpachten.
        // Name und Preis zeigt die Beschriftung, sobald die Maus darüber steht.
        var tafel = Material(frei.IstPacht ? new Color(0.35f, 0.6f, 0.32f) : new Color(0.86f, 0.66f, 0.28f));
        tafel.EmissionEnabled = true; // leuchtet leicht, damit es auch nachts zu sehen ist
        tafel.Emission = tafel.AlbedoColor * 0.35f;
        Quader(p + new Vector3(-4, 1.8f, 4.6f), new Vector3(0.22f, 3.6f, 0.22f), holz, gruppe);
        Quader(p + new Vector3(-4, 3.2f, 4.75f), new Vector3(2.6f, 1.5f, 0.12f), tafel, gruppe);
        Quader(p + new Vector3(-4, 3.2f, 4.68f), new Vector3(2.9f, 1.8f, 0.06f), holz, gruppe); // Rahmen

        Klickflaeche(LiegenschaftKennung(frei.Name), p + new Vector3(0, 2, 0), umland ? new Vector3(16, 8, 14) : new Vector3(10, 4, 8), gruppe);
        Beschriftung($"{frei.Name}\n{(frei.IstPacht ? "zu verpachten" : "zu verkaufen")}",
            p + new Vector3(0, SchildHoehe.GetValueOrDefault(frei.Name, frei.Art == Betriebsart.Mine ? 9 : 6), 0), 36, gruppe, LiegenschaftKennung(frei.Name));
        _freiePlaetze[frei.Name] = gruppe;
    }

    /// <summary>Entfernt einen Knoten samt Kindern – und vergisst seine Beschriftungen, die sonst ins Leere zeigen.</summary>
    private void KnotenEntfernen(Node3D knoten)
    {
        _beschriftungen.RemoveAll(label => label == knoten || knoten.IsAncestorOf(label));
        knoten.QueueFree();
    }

    /// <summary>
    /// Werkstatt in Fachwerk, Stein, Putz oder morschem Holz – je nach Beruf (<see cref="Werkstil"/>).
    /// Die Gießerei hat eine glühende Esse; jede Werkstatt hat einen Innenraum (Diorama).
    /// </summary>
    private enum Werkstil { Giesserei, Tischlerei, Maurerei, Tinkturei, Schaenke, Kirche, Diebesnest, Wache, Bank, Siederei }

    private BetriebsAnsicht GiessereiBauen(Betrieb betrieb, Vector3 p, Color besitzer, Werkstil stil = Werkstil.Giesserei)
    {
        var tischlerei = stil != Werkstil.Giesserei; // ohne glühende Esse auf dem Dach
        // Maße: 10 m (West–Ost) × 8 m (Nord–Süd), Wände 6 m hoch und 0,4 m dick.
        const float breite = 10, tiefe = 8, hoehe = 6, dicke = 0.4f;

        // ── Außenhülle: vier einzelne Wände, damit jede für sich ausgeblendet werden kann ──
        var wandFarbe = Material(stil switch
        {
            Werkstil.Maurerei or Werkstil.Kirche or Werkstil.Wache => Mauerstein, // Steinmetz, Kirche und Wache bauen massiv
            Werkstil.Tinkturei or Werkstil.Bank => Putz,
            Werkstil.Diebesnest => Morsch,                      // verwittertes Holz, schief geflickt
            _ => Fachwerk,
        });
        var waende = new (string Name, Vector3 Mitte, Vector3 Groesse, Vector3 Aussen)[]
        {
            ("Nordwand", new(0, hoehe / 2, -tiefe / 2 + dicke / 2), new(breite, hoehe, dicke), Vector3.Forward),
            ("Suedwand", new(0, hoehe / 2, tiefe / 2 - dicke / 2), new(breite, hoehe, dicke), Vector3.Back),
            ("Westwand", new(-breite / 2 + dicke / 2, hoehe / 2, 0), new(dicke, hoehe, tiefe - 2 * dicke), Vector3.Left),
            ("Ostwand", new(breite / 2 - dicke / 2, hoehe / 2, 0), new(dicke, hoehe, tiefe - 2 * dicke), Vector3.Right),
        };
        var gebaeude = new List<(Node3D, Vector3)>();
        foreach (var (name, mitte, groesse, aussen) in waende)
        {
            var wand = Quader(p + mitte, groesse, wandFarbe);
            wand.Name = name;
            // Balken des Fachwerks: dunkle Leisten oben und unten auf der Außenseite
            var leiste = Material(Holz);
            var laenge = Mathf.Max(groesse.X, groesse.Z) + 0.1f;
            var leistenGroesse = aussen.X != 0 ? new Vector3(0.12f, 0.3f, laenge) : new Vector3(laenge, 0.3f, 0.12f);
            foreach (var y in new[] { -hoehe / 2 + 0.15f, 0.2f, hoehe / 2 - 0.15f })
                Quader(aussen * (dicke / 2 + 0.06f) + new Vector3(0, y, 0), leistenGroesse, leiste, wand);
            gebaeude.Add((wand, aussen));
        }
        var suedwand = gebaeude[1].Item1;
        Quader(new Vector3(-2, -hoehe / 2 + 1.5f, dicke / 2 + 0.07f), new Vector3(1.8f, 3, 0.1f), Material(new Color(0.24f, 0.16f, 0.10f)), suedwand); // Tor
        var westwand = gebaeude[2].Item1;
        Quader(new Vector3(-0.35f, 2, 0), new Vector3(0.2f, 3, 2), Material(besitzer), westwand); // Wappenfahne in der Besitzerfarbe
        var vorDemSchmuck = GetChildCount(); // ab hier: Schmuck des Berufs (siehe WandschmuckAnDieWand)

        // ── Dach mit Schornstein: eine Gruppe, die in der Innenansicht verschwindet ──
        var dach = new Node3D { Name = "Dach" };
        AddChild(dach);
        Dach(p + new Vector3(0, hoehe, 0), new Vector3(breite + 0.6f, 4.8f, tiefe + 0.6f), Material(stil switch
        {
            Werkstil.Tischlerei or Werkstil.Tinkturei or Werkstil.Kirche => Schiefer,
            Werkstil.Diebesnest => Morsch,
            _ => Ziegel,
        }), quer: true, eltern: dach);
        if (stil == Werkstil.Bank)
        {
            // Wechseltisch mit grünem Tuch vor dem Tor, Münzstapel und Waage darauf; Schild mit goldener Münze; vergitterte Fenster
            var holz = Material(Holz);
            var tuch = Material(new Color(0.16f, 0.38f, 0.22f));
            var gold = Material(new Color(0.92f, 0.75f, 0.30f));
            gold.Metallic = 0.7f;
            gold.Roughness = 0.35f;
            var eisen = Material(new Color(0.30f, 0.31f, 0.34f));
            var tisch = p + new Vector3(1.8f, 0, tiefe / 2 + 1.3f);
            foreach (var dx in new[] { -1.1f, 1.1f })
                Quader(tisch + new Vector3(dx, 0.45f, 0), new Vector3(0.12f, 0.9f, 0.9f), holz);
            Quader(tisch + new Vector3(0, 0.95f, 0), new Vector3(2.6f, 0.1f, 1.1f), tuch);
            foreach (var (dx, hoch) in new[] { (-0.8f, 0.30f), (-0.5f, 0.18f), (-0.2f, 0.24f) })
                AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = hoch, RadialSegments = 10 },
                    MaterialOverride = gold,
                    Position = tisch + new Vector3(dx, 1.0f + hoch / 2, 0.1f),
                });
            Quader(tisch + new Vector3(0.7f, 1.35f, 0), new Vector3(0.06f, 0.7f, 0.06f), eisen);         // Waage: Ständer
            Quader(tisch + new Vector3(0.7f, 1.7f, 0), new Vector3(0.8f, 0.04f, 0.04f), eisen);          // Balken
            foreach (var dx in new[] { 0.32f, 1.08f })
                Quader(tisch + new Vector3(dx, 1.45f, 0), new Vector3(0.24f, 0.03f, 0.24f), gold);       // Schalen
            Quader(tisch + new Vector3(0, 0.45f, -0.75f), new Vector3(1.0f, 0.9f, 0.5f), holz);          // Truhe dahinter
            Quader(p + new Vector3(-2, 4.5f, tiefe / 2 + 0.3f), new Vector3(0.1f, 0.1f, 0.8f), eisen);   // Schildarm
            AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 0.08f, RadialSegments = 20 },
                MaterialOverride = gold,
                Position = p + new Vector3(-2, 3.9f, tiefe / 2 + 0.6f),
                RotationDegrees = new Vector3(90, 0, 0),
            });
            foreach (var x in new[] { 1.5f, 3.4f })
                foreach (var dx in new[] { -0.45f, -0.15f, 0.15f, 0.45f })
                    Quader(p + new Vector3(x + dx, 3.2f, tiefe / 2 + 0.08f), new Vector3(0.06f, 1.2f, 0.06f), eisen); // Gitter
        }
        else if (stil == Werkstil.Wache)
        {
            // Wappenschild der Stadt über dem Tor, Speergestell, ein Schilderhäuschen mit Fahne
            var rot = Material(new Color(0.72f, 0.12f, 0.12f));
            var weiss = Material(new Color(0.92f, 0.90f, 0.85f));
            Quader(p + new Vector3(-2, 4.4f, tiefe / 2 + 0.08f), new Vector3(1.0f, 1.2f, 0.06f), weiss);
            Quader(p + new Vector3(-2, 4.4f, tiefe / 2 + 0.12f), new Vector3(1.0f, 0.4f, 0.06f), rot);
            var holz = Material(Holz);
            var eisen = Material(new Color(0.55f, 0.57f, 0.62f));
            Quader(p + new Vector3(1.2f, 0.9f, tiefe / 2 + 0.6f), new Vector3(2.2f, 0.12f, 0.12f), holz);
            foreach (var dx in new[] { 0.4f, 0.9f, 1.4f, 1.9f })
            {
                Quader(p + new Vector3(dx, 1.4f, tiefe / 2 + 0.6f), new Vector3(0.07f, 2.8f, 0.07f), holz);
                Quader(p + new Vector3(dx, 2.9f, tiefe / 2 + 0.6f), new Vector3(0.14f, 0.3f, 0.07f), eisen);
            }
            var haus = p + new Vector3(breite / 2 + 1.2f, 0, tiefe / 2 - 0.5f);
            Quader(haus + new Vector3(0, 1.3f, 0), new Vector3(1.3f, 2.6f, 1.3f), rot);
            Dach(haus + new Vector3(0, 2.6f, 0), new Vector3(1.6f, 0.8f, 1.6f), Material(Schiefer), quer: false);
            Quader(haus + new Vector3(0, 3.8f, 0), new Vector3(0.08f, 2.4f, 0.08f), holz);
            Quader(haus + new Vector3(0.45f, 4.6f, 0), new Vector3(0.8f, 0.5f, 0.05f), rot);
        }
        else if (stil == Werkstil.Diebesnest)
        {
            // Vernagelte Fenster, eine trübe Laterne über dem Tor, Kisten und ein Fass mit Diebesgut, eine Falltür im Hof
            var bretter = Material(Holz);
            foreach (var x in new[] { 1.5f, 3.4f })
            {
                Quader(p + new Vector3(x, 3.4f, tiefe / 2 + 0.08f), new Vector3(1.4f, 0.18f, 0.06f), bretter);
                Quader(p + new Vector3(x, 3.0f, tiefe / 2 + 0.08f), new Vector3(1.4f, 0.18f, 0.06f), bretter);
            }
            var laterne = Material(new Color(0.95f, 0.75f, 0.35f));
            laterne.EmissionEnabled = true;
            laterne.Emission = new Color(1f, 0.65f, 0.25f);
            laterne.EmissionEnergyMultiplier = 0.8f;
            Quader(p + new Vector3(-2, 4.1f, tiefe / 2 + 0.5f), new Vector3(0.35f, 0.45f, 0.35f), laterne);
            Quader(p + new Vector3(-2, 4.5f, tiefe / 2 + 0.3f), new Vector3(0.1f, 0.1f, 0.6f), bretter);
            foreach (var (dx, dz, h) in new[] { (breite / 2 + 1.0f, -2.4f, 1.0f), (breite / 2 + 1.1f, -1.1f, 0.8f), (breite / 2 + 1.0f, -1.8f, 1.8f) })
                Quader(p + new Vector3(dx, h / 2 + (h > 1.5f ? 0.5f : 0), dz), new Vector3(1.0f, h > 1.5f ? 0.8f : h, 1.0f), bretter);
            Quader(p + new Vector3(2.6f, 0.03f, tiefe / 2 + 1.6f), new Vector3(1.4f, 0.06f, 1.4f), Material(new Color(0.22f, 0.15f, 0.10f))); // Falltür
        }
        else if (stil == Werkstil.Kirche)
        {
            // Glockenturm an der Ostseite mit Schallöffnungen und spitzem Helm, ein Kreuz darauf; Opferstock vor dem Tor
            var stein = Material(Mauerstein);
            var turm = p + new Vector3(breite / 2 + 1.4f, 0, -1.5f);
            Quader(turm + new Vector3(0, 6, 0), new Vector3(2.8f, 12, 2.8f), stein, dach);
            var dunkel = Material(new Color(0.10f, 0.09f, 0.08f));
            Quader(turm + new Vector3(0, 10.2f, 1.42f), new Vector3(1.0f, 1.6f, 0.06f), dunkel, dach);
            Quader(turm + new Vector3(1.42f, 10.2f, 0), new Vector3(0.06f, 1.6f, 1.0f), dunkel, dach);
            dach.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = 2.1f, Height = 5, RadialSegments = 4 },
                MaterialOverride = Material(Schiefer),
                Position = turm + new Vector3(0, 14.5f, 0),
                RotationDegrees = new Vector3(0, 45, 0),
            });
            var gold = Material(new Color(0.85f, 0.70f, 0.30f));
            Quader(turm + new Vector3(0, 17.6f, 0), new Vector3(0.15f, 1.4f, 0.15f), gold, dach);
            Quader(turm + new Vector3(0, 17.8f, 0), new Vector3(0.8f, 0.15f, 0.15f), gold, dach);
            Quader(p + new Vector3(2.2f, 0.5f, tiefe / 2 + 1.2f), new Vector3(0.7f, 1.0f, 0.7f), Material(Holz)); // Opferstock
        }
        else if (stil == Werkstil.Schaenke)
        {
            // Wirtshausschild am Ausleger über dem Tor, Fässer und ein Tisch mit Bänken vor der Tür
            var holz = Material(Holz);
            Quader(p + new Vector3(-2, 4.6f, tiefe / 2 + 0.9f), new Vector3(0.15f, 0.15f, 1.6f), holz);
            Quader(p + new Vector3(-2, 3.9f, tiefe / 2 + 1.5f), new Vector3(1.4f, 1.1f, 0.12f), Material(new Color(0.85f, 0.72f, 0.35f)));
            foreach (var dz in new[] { -2.2f, -0.6f })
                Quader(p + new Vector3(breite / 2 + 1.0f, 0.6f, dz), new Vector3(1.2f, 1.2f, 1.2f), holz);
            Quader(p + new Vector3(2.5f, 0.8f, tiefe / 2 + 2.2f), new Vector3(2.6f, 0.12f, 1.0f), holz);   // Tisch
            Quader(p + new Vector3(2.5f, 0.45f, tiefe / 2 + 1.3f), new Vector3(2.6f, 0.1f, 0.35f), holz);  // Bank
            Quader(p + new Vector3(2.5f, 0.45f, tiefe / 2 + 3.1f), new Vector3(2.6f, 0.1f, 0.35f), holz);  // Bank
        }
        else if (stil == Werkstil.Siederei)
        {
            // Kupferkessel mit Destillierhelm an der Ostseite, Blumenkästen mit Lavendel und Jasmin, lila Fensterläden
            var kupfer = Material(new Color(0.78f, 0.45f, 0.25f));
            kupfer.Metallic = 0.6f;
            kupfer.Roughness = 0.35f;
            var kessel = p + new Vector3(breite / 2 + 1.4f, 0, -1);
            Quader(kessel + new Vector3(0, 0.4f, 0), new Vector3(1.8f, 0.8f, 1.8f), Material(Mauerstein)); // Feuerstelle
            AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.9f, Height = 1.6f }, MaterialOverride = kupfer, Position = kessel + new Vector3(0, 1.5f, 0) });
            AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.15f, BottomRadius = 0.4f, Height = 1.0f }, MaterialOverride = kupfer, Position = kessel + new Vector3(0, 2.6f, 0) });
            Quader(kessel + new Vector3(0, 3.0f, 0.9f), new Vector3(0.12f, 0.12f, 1.8f), kupfer);                          // Kühlrohr
            Quader(kessel + new Vector3(0, 1.7f, 1.8f), new Vector3(0.12f, 2.6f, 0.12f), kupfer);
            var lavendel = Material(new Color(0.55f, 0.43f, 0.78f));
            var jasmin = Material(new Color(0.96f, 0.95f, 0.88f));
            var laden = Material(new Color(0.42f, 0.30f, 0.58f));
            foreach (var x in new[] { 1.5f, 3.4f })
            {
                Quader(p + new Vector3(x, 2.4f, tiefe / 2 + 0.35f), new Vector3(1.4f, 0.35f, 0.5f), Material(Holz));          // Blumenkasten
                for (var i = 0; i < 3; i++)
                    Quader(p + new Vector3(x - 0.45f + i * 0.45f, 2.75f, tiefe / 2 + 0.35f), new Vector3(0.35f, 0.4f, 0.35f), i % 2 == 0 ? lavendel : jasmin);
                foreach (var dx in new[] { -0.85f, 0.85f })
                    Quader(p + new Vector3(x + dx, 3.4f, tiefe / 2 + 0.08f), new Vector3(0.3f, 1.2f, 0.06f), laden);           // Fensterläden
            }
        }
        else if (stil == Werkstil.Tinkturei)
        {
            // Fässer mit Kräuterauszügen an der Ostseite, ein Trockengestell mit Kräuterbündeln vor dem Tor
            var fass = Material(Holz);
            foreach (var dz in new[] { -2.5f, -0.8f, 0.9f })
                Quader(p + new Vector3(breite / 2 + 1.0f, 0.6f, dz), new Vector3(1.1f, 1.2f, 1.1f), fass);
            Quader(p + new Vector3(2, 1.1f, tiefe / 2 + 1.4f), new Vector3(2.4f, 0.12f, 0.12f), fass);
            foreach (var dx in new[] { 1.2f, 1.9f, 2.6f })
                Quader(p + new Vector3(dx, 0.75f, tiefe / 2 + 1.4f), new Vector3(0.35f, 0.6f, 0.35f), Material(Kraut));
        }
        else if (stil == Werkstil.Maurerei)
        {
            // Quader und Bruchsteine an der Ostseite, ein Werkblock vor dem Tor
            var stein = Material(Bruchstein);
            for (var i = 0; i < 3; i++)
                Quader(p + new Vector3(breite / 2 + 1.3f, 0.5f + i * 1.0f, -2 + i * 1.2f), new Vector3(1.8f, 1f, 2.6f - i * 0.6f), stein);
            Quader(p + new Vector3(2, 0.45f, tiefe / 2 + 1.4f), new Vector3(1.4f, 0.9f, 1.4f), stein);
        }
        else if (tischlerei)
        {
            // Holzstapel an der Ostseite, ein Sägebock vor dem Tor
            var stamm = Material(Holz);
            for (var lage = 0; lage < 3; lage++)
                Quader(p + new Vector3(breite / 2 + 1.2f, 0.4f + lage * 0.8f, 0), new Vector3(1.8f, 0.7f, tiefe - 1 - lage), stamm);
            Quader(p + new Vector3(2, 0.5f, tiefe / 2 + 1.4f), new Vector3(2.2f, 1f, 0.6f), stamm);
        }
        WandschmuckAnDieWand(p, tiefe, suedwand, vorDemSchmuck);
        var esse = Material(new Color(0.3f, 0.28f, 0.27f));
        esse.EmissionEnabled = true;
        esse.Emission = new Color(1f, 0.45f, 0.1f);
        esse.EmissionEnergyMultiplier = 0;
        Quader(p + new Vector3(3.6f, 10.5f, 0.2f), new Vector3(1.4f, 5, 1.4f), Material(Mauerstein), dach);
        Quader(p + new Vector3(3.6f, 13.3f, 0.2f), new Vector3(1.6f, 0.6f, 1.6f), esse, dach);
        var glut = new OmniLight3D { LightColor = new Color(1f, 0.55f, 0.2f), OmniRange = 12, LightEnergy = 0, Position = p + new Vector3(3.6f, 14.5f, 0.2f) };
        dach.AddChild(glut);

        Klickflaeche(BetriebsKennung(betrieb), p + new Vector3(0, 4, 0), new Vector3(breite, 8, tiefe));
        var schild = Beschriftung(betrieb.Name, p + new Vector3(0, GebaeudeSchildHoehe.GetValueOrDefault(betrieb.Name, 13), 0), 44, kennung: BetriebsKennung(betrieb));

        // Weg zur nächsten Gasse und dann zum Markt
        var hof = p + new Vector3(0, 0, 5.5f);
        var gasse = Gasse(betrieb.Name, p);
        GasseZiehen(p, gasse);

        var ansicht = new BetriebsAnsicht
        {
            Betrieb = betrieb,
            Mitte = p,
            Hof = hof,
            WegZumMarkt = gasse.Prepend(hof).Append(Marktplatz).ToArray(),
            Glut = tischlerei ? null : glut,
            Esse = tischlerei ? null : esse,
            Schild = schild,
            Dach = dach,
        };
        ansicht.Waende.AddRange(gebaeude);
        Innenraum(ansicht, stil, breite, tiefe, dicke);
        if (betrieb.Stufe > 1 || betrieb.UmbauMinuten > 0) StufeZeigen(ansicht);
        return ansicht;
    }

    /// <summary>
    /// Was an der Südwand hängt (Wappenschild, Fenstergitter, Blumenkästen, Fensterläden, Laterne, Münzschild), wird ein
    /// Kind der Wand. So verschwindet es mit ihr, wenn die Werkstatt in der Innenansicht aufgeschnitten wird – sonst
    /// schwebte es mitten im Raum. Freistehendes vor der Tür (Tische, Speergestell, Fässer) bleibt stehen.
    /// Erkannt wird Wandschmuck an seiner Lage: dicht vor der Wand (bis 0,4 m) und höher als 1,5 m,
    /// oder bis 0,65 m davor und höher als 3,5 m (Schilder an einem kurzen Arm).
    /// </summary>
    private void WandschmuckAnDieWand(Vector3 p, float tiefe, Node3D suedwand, int abKind)
    {
        foreach (var kind in GetChildren().Skip(abKind).OfType<MeshInstance3D>().ToList())
        {
            var vorDerWand = kind.Position.Z - (p.Z + tiefe / 2);
            var hoehe = kind.Position.Y - p.Y;
            if (vorDerWand is < -0.1f or > 0.65f || !(vorDerWand <= 0.4f && hoehe > 1.5f || hoehe > 3.5f)) continue;
            var lage = kind.GlobalTransform;
            RemoveChild(kind);
            suedwand.AddChild(kind);
            kind.GlobalTransform = lage;
        }
    }

    /// <summary>
    /// Die Gebäudestufe von außen: ab Stufe 2 eine Dachgaube über dem Tor, ab Stufe 3 ein Dachreiter auf dem First
    /// (golden beim Schmied, kupfergrün beim Tischler, schiefergrau beim Steinmetz). Während des Umbaus steht ein Gerüst vor der Südwand.
    /// Die Grundfläche bleibt gleich – die Nachbarn rücken nicht zusammen.
    /// </summary>
    private void StufeZeigen(BetriebsAnsicht a)
    {
        const float hoehe = 6, tiefe = 8;
        var b = a.Betrieb;
        a.GezeigteStufe = b.Stufe;
        a.GezeigterUmbau = b.UmbauMinuten > 0;
        if (a.Ausbau is not null) KnotenEntfernen(a.Ausbau);
        var gruppe = new Node3D { Name = "Ausbau" };
        (a.Dach ?? (Node3D)this).AddChild(gruppe);
        a.Ausbau = gruppe;
        var p = a.Mitte;
        var holz = Material(Holz);

        if (b.Stufe >= 2)
        {
            // Dachgaube auf der Südseite des Dachs, mit Fenster und eigenem Satteldach
            var gaube = p + new Vector3(-2.5f, 0, 2.6f);
            Quader(gaube + new Vector3(0, hoehe + 1.6f, 0), new Vector3(2.4f, 2.0f, 2.0f), Material(b.Artdaten.Beruf switch { "Steinmetz" or "Prediger" or "Gardist" => Mauerstein, "Alchimist" or "Geldleiher" => Putz, "Dieb" => Morsch, _ => Fachwerk }), gruppe);
            Quader(gaube + new Vector3(0, hoehe + 1.7f, 1.02f), new Vector3(1.1f, 1.0f, 0.06f), Material(new Color(0.12f, 0.1f, 0.08f)), gruppe);
            Dach(gaube + new Vector3(0, hoehe + 2.6f, 0), new Vector3(2.9f, 1.2f, 2.4f), Material(b.Artdaten.Beruf is "Tischler" or "Alchimist" or "Prediger" ? Schiefer : Ziegel), quer: false, eltern: gruppe);
        }
        if (b.Stufe >= 3)
        {
            // Dachreiter: kleiner Turm auf dem First mit vierseitiger Spitze
            var first = p + new Vector3(-1, hoehe + 4.8f, 0);
            Quader(first + new Vector3(0, 0.9f, 0), new Vector3(1.6f, 2.2f, 1.6f), Material(b.Artdaten.Beruf switch { "Steinmetz" or "Prediger" or "Gardist" => Mauerstein, "Alchimist" or "Geldleiher" => Putz, "Dieb" => Morsch, _ => Fachwerk }), gruppe);
            var spitze = Material(b.Artdaten.Beruf switch
            {
                "Tischler" => new Color(0.35f, 0.55f, 0.45f),   // kupfergrün
                "Steinmetz" => new Color(0.30f, 0.33f, 0.40f),  // Schiefer
                "Alchimist" => new Color(0.45f, 0.30f, 0.55f),  // violett
                "Wirt" => new Color(0.60f, 0.25f, 0.20f),       // weinrot
                "Prediger" => new Color(0.82f, 0.82f, 0.86f),   // silbern
                "Dieb" => new Color(0.16f, 0.18f, 0.30f),       // nachtblau
                "Gardist" => new Color(0.72f, 0.12f, 0.12f),    // stadtrot
                "Geldleiher" => new Color(0.15f, 0.28f, 0.55f), // königsblau
                "Parfumeur" => new Color(0.62f, 0.45f, 0.78f),  // lavendel
                _ => new Color(0.85f, 0.68f, 0.25f),            // golden
            });
            spitze.Metallic = 0.5f;
            spitze.Roughness = 0.4f;
            gruppe.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = 1.3f, Height = 3, RadialSegments = 4 },
                MaterialOverride = spitze,
                Position = first + new Vector3(0, 3.5f, 0),
                RotationDegrees = new Vector3(0, 45, 0),
            });
        }
        if (b.UmbauMinuten > 0)
        {
            // Gerüst vor der Südwand: Stangen und zwei Laufbretter
            var sued = p + new Vector3(0, 0, tiefe / 2 + 0.7f);
            foreach (var x in new[] { -4.5f, -1.5f, 1.5f, 4.5f })
                Quader(sued + new Vector3(x, 3.6f, 0), new Vector3(0.15f, 7.2f, 0.15f), holz, gruppe);
            foreach (var y in new[] { 2.6f, 5.2f })
                Quader(sued + new Vector3(0, y, 0), new Vector3(9.6f, 0.12f, 0.9f), holz, gruppe);
        }
        if (a.Schild is not null)
            a.Schild.Text = b.Name + (b.UmbauMinuten > 0 ? " (Umbau)" : ""); // die Stufe zeigen Gaube und Dachreiter – eine zweite Zeile überdeckte die Nachbarn
    }

    /// <summary>Berg aus zwei abgeflachten Kugeln.</summary>
    private void BergBauen(Vector3 p, Node3D? eltern = null)
    {
        foreach (var (versatz, radius) in new[] { (new Vector3(10, 0, -8), 18f), (new Vector3(20, 0, 4), 13f) })
        {
            var berg = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = radius, Height = radius * 1.3f, RadialSegments = 12, Rings = 6 },
                MaterialOverride = Material(Fels),
                Position = p + versatz,
            };
            (eltern ?? this).AddChild(berg);
        }
    }

    private BetriebsAnsicht MineBauen(Betrieb betrieb, Vector3 p, Color besitzer, Vector3 abzweig)
    {
        BergBauen(p);
        Quader(p + new Vector3(2, 2, 0), new Vector3(3, 4, 4), Material(new Color(0.08f, 0.07f, 0.07f))); // Stolleneingang
        Haus(p + new Vector3(-6, 0, 6), new Vector3(6, 4, 5), Holz, Schiefer, quer: true);                // Schuppen
        Quader(p + new Vector3(-9.2f, 3.5f, 6), new Vector3(0.2f, 3, 2), Material(besitzer));

        Klickflaeche(BetriebsKennung(betrieb), p + new Vector3(2, 4, 0), new Vector3(16, 8, 14));
        var schild = Beschriftung(betrieb.Name, p + new Vector3(2, 9, 0), 44, kennung: BetriebsKennung(betrieb));

        var hof = p + new Vector3(-2, 0, 0);
        WegZiehen(abzweig, hof, 4);
        if (AbstandZurStrecke(abzweig, Osttor, Landstrasse) > 1) // nicht an der Landstraße? Dann eigene Verbindung zum Tor
            WegZiehen(Osttor, abzweig, 4);
        return new BetriebsAnsicht
        {
            Betrieb = betrieb,
            Mitte = p,
            Hof = hof,
            WegZumMarkt = new[] { hof, abzweig, Osttor, Marktplatz },
            Schild = schild,
        };
    }

    /// <summary>Ein Felsen mit abgetreppter Bruchkante – kleiner als der Berg einer Grube.</summary>
    private void FelsenBauen(Vector3 p, Node3D? eltern = null)
    {
        foreach (var (versatz, radius) in new[] { (new Vector3(8, 0, -7), 9f), (new Vector3(14, 0, 1), 6f) })
            (eltern ?? this).AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = radius, Height = radius * 1.4f, RadialSegments = 10, Rings = 5 },
                MaterialOverride = Material(Fels),
                Position = p + versatz,
            });
        // Abgetreppte, hell angeschnittene Bruchkante vor dem Felsen
        var schnitt = Material(Bruchstein);
        for (var stufe = 0; stufe < 3; stufe++)
            Quader(p + new Vector3(3 + stufe * 1.6f, 1 + stufe * 2, -2 - stufe * 1.2f), new Vector3(6, 2, 4), schnitt, eltern);
    }

    /// <summary>Steinbruch: Felsen mit Bruchkante, Steinquader zum Abtransport und ein Schuppen.</summary>
    private BetriebsAnsicht SteinbruchBauen(Betrieb betrieb, Vector3 p, Color besitzer, Vector3 abzweig)
    {
        FelsenBauen(p);
        var stein = Material(Bruchstein);
        foreach (var (dx, dz, h) in new[] { (0f, 4f, 1.2f), (1.6f, 4.2f, 0.8f), (0.8f, 5.6f, 1f), (3.5f, 6f, 1.2f) })
            Quader(p + new Vector3(dx, h / 2, dz), new Vector3(1.4f, h, 1.4f), stein);
        Haus(p + new Vector3(-6, 0, 6), new Vector3(6, 4, 5), Holz, Schiefer, quer: true);
        Quader(p + new Vector3(-9.2f, 3.5f, 6), new Vector3(0.2f, 3, 2), Material(besitzer));

        Klickflaeche(BetriebsKennung(betrieb), p + new Vector3(2, 4, 0), new Vector3(16, 8, 14));
        var schild = Beschriftung(betrieb.Name, p + new Vector3(2, 9, 0), 44, kennung: BetriebsKennung(betrieb));
        var hof = p + new Vector3(-2, 0, 0);
        WegZiehen(abzweig, hof, 4);
        if (AbstandZurStrecke(abzweig, Osttor, Landstrasse) > 1)
            WegZiehen(Osttor, abzweig, 4);
        return new BetriebsAnsicht
        {
            Betrieb = betrieb,
            Mitte = p,
            Hof = hof,
            WegZumMarkt = new[] { hof, abzweig, Osttor, Marktplatz },
            Schild = schild,
        };
    }

    /// <summary>Gersten- und Roggenfelder in Streifen und eine kleine Windmühle.</summary>
    private void Felder(Vector3 p, Node3D? eltern = null)
    {
        var gerste = Material(Korn);
        var roggen = Material(new Color(0.66f, 0.55f, 0.28f));
        for (var streifen = 0; streifen < 6; streifen++)
            Quader(p + new Vector3(1 + streifen * 2.2f, 0.25f, -3), new Vector3(1.9f, 0.5f, 12), streifen % 2 == 0 ? gerste : roggen, eltern);
        // Windmühle: Turm mit Kappe und vier Flügeln
        var m = p + new Vector3(16, 0, 6);
        Quader(m + new Vector3(0, 3, 0), new Vector3(2.4f, 6, 2.4f), Material(Putz), eltern);
        Dach(m + new Vector3(0, 6, 0), new Vector3(3, 1.6f, 3), Material(Holz), quer: false, eltern: eltern);
        foreach (var winkel in new[] { 30f, 120f })
            (eltern ?? this).AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.4f, 7, 0.1f) },
                MaterialOverride = Material(Holz),
                Position = m + new Vector3(0, 5, 1.35f),
                RotationDegrees = new Vector3(0, 0, winkel),
            });
    }

    /// <summary>Bauernhof: Felder, Mühle und eine Scheune – vor der Stadt.</summary>
    private BetriebsAnsicht BauernhofBauen(Betrieb betrieb, Vector3 p, Color besitzer, Vector3 abzweig)
    {
        Felder(p);
        Haus(p + new Vector3(-6, 0, 6), new Vector3(7, 4.5f, 5), Holz, Ziegel, quer: true);   // Scheune
        Quader(p + new Vector3(-9.7f, 3.5f, 6), new Vector3(0.2f, 3, 2), Material(besitzer));

        Klickflaeche(BetriebsKennung(betrieb), p + new Vector3(4, 3, 0), new Vector3(20, 6, 16));
        var schild = Beschriftung(betrieb.Name, p + new Vector3(4, 9, 0), 44, kennung: BetriebsKennung(betrieb));
        var hof = p + new Vector3(-2, 0, 0);
        WegZiehen(abzweig, hof, 4);
        if (AbstandZurStrecke(abzweig, Osttor, Landstrasse) > 1)
            WegZiehen(Osttor, abzweig, 4);
        return new BetriebsAnsicht
        {
            Betrieb = betrieb,
            Mitte = p,
            Hof = hof,
            WegZumMarkt = new[] { hof, abzweig, Osttor, Marktplatz },
            Schild = schild,
        };
    }

    /// <summary>Klosterhof: Gemüsebeete, Bienenstöcke (Wachs), eine Lumpenkammer und eine Kapelle – vor der Stadt am Osthang.</summary>
    private BetriebsAnsicht KlosterhofBauen(Betrieb betrieb, Vector3 p, Color besitzer, Vector3 abzweig)
    {
        Klostergarten(p);
        Haus(p + new Vector3(-6, 0, 6), new Vector3(7, 4.5f, 5), Mauerstein, Schiefer, quer: true);   // Lumpenkammer und Zellen
        Quader(p + new Vector3(-9.7f, 3.5f, 6), new Vector3(0.2f, 3, 2), Material(besitzer));

        Klickflaeche(BetriebsKennung(betrieb), p + new Vector3(2, 3, 0), new Vector3(18, 6, 16));
        var schild = Beschriftung(betrieb.Name, p + new Vector3(2, 9, 0), 44, kennung: BetriebsKennung(betrieb));
        var hof = p + new Vector3(-2, 0, 0);
        WegZiehen(abzweig, hof, 4);
        if (AbstandZurStrecke(abzweig, Osttor, Landstrasse) > 1)
            WegZiehen(Osttor, abzweig, 4);
        return new BetriebsAnsicht
        {
            Betrieb = betrieb,
            Mitte = p,
            Hof = hof,
            WegZumMarkt = new[] { hof, abzweig, Osttor, Marktplatz },
            Schild = schild,
        };
    }

    /// <summary>Wachturm: steinerner Turm mit Zinnen, Feuerkorb und Fahne an der Landstraße, eine Palisade um den Hof.</summary>
    private BetriebsAnsicht WachturmBauen(Betrieb betrieb, Vector3 p, Color besitzer, Vector3 abzweig)
    {
        var stein = Material(Mauerstein);
        Quader(p + new Vector3(0, 6, 0), new Vector3(4, 12, 4), stein);
        foreach (var (dx, dz) in new[] { (-1.6f, -1.6f), (1.6f, -1.6f), (-1.6f, 1.6f), (1.6f, 1.6f), (0f, -1.6f), (0f, 1.6f), (-1.6f, 0f), (1.6f, 0f) })
            Quader(p + new Vector3(dx, 12.5f, dz), new Vector3(0.7f, 1.0f, 0.7f), stein); // Zinnen
        var glut = Material(new Color(1f, 0.55f, 0.2f));
        glut.EmissionEnabled = true;
        glut.Emission = new Color(1f, 0.5f, 0.15f);
        glut.EmissionEnergyMultiplier = 1.2f;
        Quader(p + new Vector3(0, 12.4f, 0), new Vector3(0.8f, 0.6f, 0.8f), glut); // Feuerkorb
        Quader(p + new Vector3(0, 15, 0), new Vector3(0.12f, 4, 0.12f), Material(Holz));
        Quader(p + new Vector3(0.7f, 16.3f, 0), new Vector3(1.3f, 0.8f, 0.06f), Material(besitzer));
        Quader(p + new Vector3(0, 1.3f, 2.03f), new Vector3(1.2f, 2.6f, 0.08f), Material(new Color(0.24f, 0.16f, 0.10f))); // Tür
        var holz = Material(Holz);
        foreach (var (mitte, groesse) in new[]
        {
            (new Vector3(0, 0.8f, -5), new Vector3(10, 1.6f, 0.3f)), (new Vector3(-5, 0.8f, 0), new Vector3(0.3f, 1.6f, 10)), (new Vector3(5, 0.8f, 0), new Vector3(0.3f, 1.6f, 10)),
        })
            Quader(p + mitte, groesse, holz);

        Klickflaeche(BetriebsKennung(betrieb), p + new Vector3(0, 6, 0), new Vector3(10, 12, 10));
        var schild = Beschriftung(betrieb.Name, p + new Vector3(0, 15, 0), 44, kennung: BetriebsKennung(betrieb));
        var hof = p + new Vector3(-2, 0, 0);
        WegZiehen(abzweig, hof, 4);
        if (AbstandZurStrecke(abzweig, Osttor, Landstrasse) > 1)
            WegZiehen(Osttor, abzweig, 4);
        return new BetriebsAnsicht
        {
            Betrieb = betrieb,
            Mitte = p,
            Hof = hof,
            WegZumMarkt = new[] { hof, abzweig, Osttor, Marktplatz },
            Schild = schild,
        };
    }

    /// <summary>Beete in Reihen, sechs strohgedeckte Bienenkörbe und ein Wegkreuz.</summary>
    private void Klostergarten(Vector3 p, Node3D? eltern = null)
    {
        var erde = Material(new Color(0.36f, 0.27f, 0.18f));
        var kohl = Material(new Color(0.42f, 0.55f, 0.32f));
        for (var reihe = 0; reihe < 3; reihe++)
        {
            Quader(p + new Vector3(2 + reihe * 2.4f, 0.15f, -4), new Vector3(1.6f, 0.3f, 6), erde, eltern);
            for (var i = 0; i < 3; i++)
                Quader(p + new Vector3(2 + reihe * 2.4f, 0.45f, -6 + i * 2), new Vector3(0.8f, 0.4f, 0.8f), kohl, eltern);
        }
        var stroh = Material(new Color(0.78f, 0.64f, 0.34f));
        var bank = Material(Holz);
        Quader(p + new Vector3(4.5f, 0.35f, 3.2f), new Vector3(7.2f, 0.15f, 1.0f), bank, eltern); // Bienenbank
        for (var i = 0; i < 6; i++)
            (eltern ?? this).AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.55f, Height = 1.3f, RadialSegments = 10, Rings = 5 },
                MaterialOverride = stroh,
                Position = p + new Vector3(1.6f + i * 1.15f, 1.0f, 3.2f),
            });
        var kreuz = Material(Holz);
        Quader(p + new Vector3(10, 1.4f, -1), new Vector3(0.2f, 2.8f, 0.2f), kreuz, eltern);
        Quader(p + new Vector3(10, 2.2f, -1), new Vector3(1.2f, 0.18f, 0.18f), kreuz, eltern);
    }

    /// <summary>Kräuterbeete in Reihen und ein Krötenteich.</summary>
    private void Beete(Vector3 p, Node3D? eltern = null)
    {
        var erde = Material(new Color(0.36f, 0.27f, 0.18f));
        var kraut = Material(Kraut);
        for (var reihe = 0; reihe < 4; reihe++)
        {
            Quader(p + new Vector3(2 + reihe * 2.4f, 0.15f, -3), new Vector3(1.6f, 0.3f, 8), erde, eltern);
            for (var i = 0; i < 4; i++)
                Quader(p + new Vector3(2 + reihe * 2.4f, 0.55f, -6 + i * 2), new Vector3(0.9f, 0.6f, 0.9f), kraut, eltern);
        }
        (eltern ?? this).AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 3.2f, BottomRadius = 3.2f, Height = 0.1f, RadialSegments = 16 },
            MaterialOverride = Material(Wasser),
            Position = p + new Vector3(6, 0.05f, 6),
        });
    }

    /// <summary>Lavendel- und Jasminbeete im Wechsel, dazu ein Trockengestell.</summary>
    private void Duftbeete(Vector3 p, Node3D? eltern = null)
    {
        var erde = Material(new Color(0.40f, 0.30f, 0.20f));
        var lavendel = Material(new Color(0.55f, 0.43f, 0.78f));
        var jasmin = Material(new Color(0.96f, 0.95f, 0.88f));
        var blatt = Material(Kraut);
        for (var reihe = 0; reihe < 5; reihe++)
        {
            var x = 0 + reihe * 2.2f;
            Quader(p + new Vector3(x, 0.15f, -1), new Vector3(1.4f, 0.3f, 10), erde, eltern);
            for (var i = 0; i < 5; i++)
            {
                var z = -5 + i * 2;
                if (reihe % 2 == 0)
                    Quader(p + new Vector3(x, 0.6f, z), new Vector3(0.9f, 0.7f, 1.2f), lavendel, eltern);
                else
                {
                    Quader(p + new Vector3(x, 0.55f, z), new Vector3(1.0f, 0.6f, 1.0f), blatt, eltern);
                    Quader(p + new Vector3(x, 0.95f, z), new Vector3(0.6f, 0.25f, 0.6f), jasmin, eltern);
                }
            }
        }
        var holz = Material(Holz);
        Quader(p + new Vector3(4.4f, 1.6f, 6), new Vector3(6, 0.12f, 0.12f), holz, eltern);                  // Trockengestell
        foreach (var dx in new[] { 1.6f, 7.2f })
            Quader(p + new Vector3(dx, 0.8f, 6), new Vector3(0.15f, 1.6f, 0.15f), holz, eltern);
        foreach (var dx in new[] { 2.6f, 3.6f, 4.6f, 5.6f, 6.6f })
            Quader(p + new Vector3(dx, 1.25f, 6), new Vector3(0.3f, 0.6f, 0.3f), lavendel, eltern);
    }

    /// <summary>Duftgarten: Lavendel- und Jasminbeete, Trockengestell, Gärtnerhütte – östlich am Ende der Landstraße.</summary>
    private BetriebsAnsicht DuftgartenBauen(Betrieb betrieb, Vector3 p, Color besitzer, Vector3 abzweig)
    {
        Duftbeete(p);
        Haus(p + new Vector3(-6, 0, 6), new Vector3(6, 4, 5), Holz, Ziegel, quer: true);
        Quader(p + new Vector3(-9.2f, 3.5f, 6), new Vector3(0.2f, 3, 2), Material(besitzer));

        Klickflaeche(BetriebsKennung(betrieb), p + new Vector3(2, 3, 0), new Vector3(16, 6, 14));
        var schild = Beschriftung(betrieb.Name, p + new Vector3(2, 7, 0), 44, kennung: BetriebsKennung(betrieb));
        var hof = p + new Vector3(-2, 0, 0);
        WegZiehen(abzweig, hof, 4);
        if (AbstandZurStrecke(abzweig, Osttor, Landstrasse) > 1)
            WegZiehen(Osttor, abzweig, 4);
        return new BetriebsAnsicht
        {
            Betrieb = betrieb,
            Mitte = p,
            Hof = hof,
            WegZumMarkt = new[] { hof, abzweig, Osttor, Marktplatz },
            Schild = schild,
        };
    }

    /// <summary>Kräutergarten: Beete, Teich, Gärtnerhütte – vor der Stadt am Bach.</summary>
    private BetriebsAnsicht KraeutergartenBauen(Betrieb betrieb, Vector3 p, Color besitzer, Vector3 abzweig)
    {
        Beete(p);
        Haus(p + new Vector3(-6, 0, 6), new Vector3(6, 4, 5), Holz, Schiefer, quer: true);
        Quader(p + new Vector3(-9.2f, 3.5f, 6), new Vector3(0.2f, 3, 2), Material(besitzer));

        Klickflaeche(BetriebsKennung(betrieb), p + new Vector3(2, 3, 0), new Vector3(16, 6, 14));
        var schild = Beschriftung(betrieb.Name, p + new Vector3(2, 7, 0), 44, kennung: BetriebsKennung(betrieb));
        var hof = p + new Vector3(-2, 0, 0);
        WegZiehen(abzweig, hof, 4);
        if (AbstandZurStrecke(abzweig, Osttor, Landstrasse) > 1)
            WegZiehen(Osttor, abzweig, 4);
        return new BetriebsAnsicht
        {
            Betrieb = betrieb,
            Mitte = p,
            Hof = hof,
            WegZumMarkt = new[] { hof, abzweig, Osttor, Marktplatz },
            Schild = schild,
        };
    }

    /// <summary>Holzschlag: Blockhütte, gestapelte Stämme und Stümpfe – im Wald vor der Stadt.</summary>
    private BetriebsAnsicht HolzschlagBauen(Betrieb betrieb, Vector3 p, Color besitzer, Vector3 abzweig)
    {
        var stamm = Material(Holz);
        Haus(p + new Vector3(-6, 0, 6), new Vector3(6, 4, 5), Holz, Schiefer, quer: true);           // Hütte
        Quader(p + new Vector3(-9.2f, 3.5f, 6), new Vector3(0.2f, 3, 2), Material(besitzer));        // Fahne
        for (var reihe = 0; reihe < 3; reihe++)                                                       // Stämme
            for (var lage = 0; lage < 3 - reihe; lage++)
                Quader(p + new Vector3(2 + reihe * 2.5f, 0.45f + lage * 0.9f, -2), new Vector3(0.8f, 0.8f, 7), stamm);
        foreach (var (dx, dz) in new[] { (6f, 5f), (9f, 1f), (3f, 7f), (10f, -6f) })                  // Stümpfe
            Quader(p + new Vector3(dx, 0.4f, dz), new Vector3(1.1f, 0.8f, 1.1f), stamm);

        Klickflaeche(BetriebsKennung(betrieb), p + new Vector3(2, 4, 0), new Vector3(16, 8, 14));
        var schild = Beschriftung(betrieb.Name, p + new Vector3(2, 8, 0), 44, kennung: BetriebsKennung(betrieb));
        var hof = p + new Vector3(-2, 0, 0);
        WegZiehen(abzweig, hof, 4);
        if (AbstandZurStrecke(abzweig, Osttor, Landstrasse) > 1)
            WegZiehen(Osttor, abzweig, 4);
        return new BetriebsAnsicht
        {
            Betrieb = betrieb,
            Mitte = p,
            Hof = hof,
            WegZumMarkt = new[] { hof, abzweig, Osttor, Marktplatz },
            Schild = schild,
        };
    }

    public static string BetriebsKennung(Betrieb b) => "betrieb:" + b.Name;

    /// <summary>Jedes Bild: Licht nach Uhrzeit, Esse, Karren.</summary>
    public void Aktualisieren(double delta)
    {
        if (_simulation is null) return;
        var zeit = _simulation.Zeit;
        // Die Simulation zählt nur ganze Minuten (bei Tempo 1 etwa eine pro Sekunde). Für die Darstellung
        // nehmen wir den Bruchteil der angebrochenen Minute dazu – sonst springen Sonne, Schatten und Karren
        // einmal pro Sekunde, statt sich jedes Bild ein Stückchen weiterzubewegen.
        var bruchteil = _simulation.Takt.AngebrocheneMinute;
        LichtNachUhrzeit(zeit.UhrzeitInMinuten + (float)bruchteil);

        if (_simulation.Wirtschaft is not { } w) return;
        var arbeitszeit = w.IstArbeitszeit(zeit);
        if (!_simulation.Takt.IstPausiert) _animationszeit += delta;

        foreach (var (betrieb, ansicht) in _betriebe)
        {
            if (betrieb.InDerStadt && (betrieb.Stufe != ansicht.GezeigteStufe || (betrieb.UmbauMinuten > 0) != ansicht.GezeigterUmbau))
                StufeZeigen(ansicht);
            var arbeitet = arbeitszeit && betrieb.Belegt > 0 && betrieb.Stillstand is null;
            if (ansicht.Glut is not null) ansicht.Glut.LightEnergy = arbeitet ? 3f : 0f;
            if (ansicht.Esse is not null) ansicht.Esse.EmissionEnergyMultiplier = arbeitet ? 2.5f : 0f;
            if (ansicht == _innen)
                InnenAktualisieren(ansicht, zeit, arbeitszeit, arbeitet, delta);

            var fahrzeit = w.Daten.Logistik.FahrzeitAb(betrieb.Ort);
            for (var i = 0; i < betrieb.Karren.Count; i++)
                KarrenBewegen(betrieb.Karren[i], i, ansicht, fahrzeit, zeit.VergangeneMinuten + bruchteil, delta, ansicht.Spieler);
        }
        if (_innen is SonderbauAnsicht sonderbau)
            SonderbauAktualisieren(sonderbau, zeit, arbeitszeit, delta);
    }

    private void KarrenBewegen(Karren karren, int nummer, BetriebsAnsicht ansicht, int fahrzeit, double jetztGenau, double delta, bool spieler)
    {
        if (!_karren.TryGetValue(karren, out var knoten))
        {
            knoten = KarrenModell(ansicht.Farbe);
            knoten.Position = ansicht.Hof;
            _karren[karren] = knoten;
        }

        Vector3 ziel;
        Vector3 blick;
        if (karren.Zustand == KarrenZustand.AmBetrieb)
        {
            ziel = ansicht.Hof + new Vector3(nummer * 3f, 0, 0);
            blick = ansicht.WegZumMarkt[1];
        }
        else
        {
            var rest = Math.Clamp((karren.Ankunft - jetztGenau) / fahrzeit, 0, 1);
            var anteil = karren.Zustand == KarrenZustand.ZumMarkt ? 1 - rest : rest;
            ziel = PunktAufWeg(ansicht.WegZumMarkt, (float)anteil);
            var weiter = PunktAufWeg(ansicht.WegZumMarkt, (float)Math.Clamp(anteil + (karren.Zustand == KarrenZustand.ZumMarkt ? 0.02 : -0.02), 0, 1));
            blick = weiter;
        }

        // Das Ziel wandert dank des Minuten-Bruchteils schon stetig. Das kurze Hinterhergleiten
        // fängt nur noch echte Sprünge ab (Abfahrt vom Hof, Ankunft, Tempowechsel).
        knoten.Position = knoten.Position.Lerp(ziel, (float)Math.Min(1, delta * 10));
        var richtung = blick - knoten.Position;
        richtung.Y = 0;
        if (richtung.LengthSquared() > 0.01f)
            knoten.Rotation = new Vector3(0, Mathf.Atan2(richtung.X, richtung.Z), 0);

        // Ladung sichtbar machen, Eskorte als Begleiter
        knoten.GetNode<Node3D>("Ladung").Visible = karren.Ladung.Count > 0;
        knoten.GetNode<Node3D>("Eskorte").Visible = karren.Eskorte != "Keine" && karren.Ladung.Count > 0;
    }

    private static Vector3 PunktAufWeg(Vector3[] weg, float anteil)
    {
        var laengen = new float[weg.Length - 1];
        float gesamt = 0;
        for (var i = 0; i < laengen.Length; i++) { laengen[i] = weg[i].DistanceTo(weg[i + 1]); gesamt += laengen[i]; }
        var strecke = anteil * gesamt;
        for (var i = 0; i < laengen.Length; i++)
        {
            if (strecke <= laengen[i]) return weg[i].Lerp(weg[i + 1], laengen[i] > 0 ? strecke / laengen[i] : 0);
            strecke -= laengen[i];
        }
        return weg[^1];
    }

    private Node3D KarrenModell(Color besitzer)
    {
        var karren = new Node3D();
        AddChild(karren);
        karren.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.8f, 2.6f) }, MaterialOverride = Material(Holz), Position = new Vector3(0, 1.1f, 0) });
        foreach (var x in new[] { -0.95f, 0.95f })
        {
            var rad = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.6f, BottomRadius = 0.6f, Height = 0.2f }, MaterialOverride = Material(new Color(0.2f, 0.15f, 0.1f)) };
            rad.Position = new Vector3(x, 0.6f, 0);
            rad.RotationDegrees = new Vector3(0, 0, 90);
            karren.AddChild(rad);
        }
        var ladung = new MeshInstance3D { Name = "Ladung", Mesh = new BoxMesh { Size = new Vector3(1.3f, 0.7f, 2f) }, MaterialOverride = Material(besitzer), Position = new Vector3(0, 1.85f, 0) };
        karren.AddChild(ladung);
        var eskorte = new MeshInstance3D { Name = "Eskorte", Mesh = new CapsuleMesh { Radius = 0.35f, Height = 1.8f }, MaterialOverride = Material(new Color(0.35f, 0.35f, 0.38f)), Position = new Vector3(1.8f, 0.9f, 0.6f) };
        karren.AddChild(eskorte);
        return karren;
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
                InnenAktualisieren(b, zeit, arbeitszeit, arbeitszeit && b.Betrieb.Belegt > 0 && b.Betrieb.Stillstand is null, 0);
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
        foreach (var kind in GetChildren())
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
        foreach (var andere in _betriebe.Values.Cast<InnenAnsicht>().Concat(_sonderbauten.Values).Where(x => x != ansicht && x.Dach is not null))
        {
            var abstand = andere.Mitte - ansicht.Mitte;
            var vorne = abstand.Dot(zurKamera);
            if (vorne is > 4.5f and < 28f && (abstand - zurKamera * vorne).Length() < 11f && andere.Dach!.Visible)
            {
                andere.Dach.Visible = false;
                _kulisseAusgeblendet.Add(andere.Dach);
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
        _kamerafahrt?.Kill();
        _kamerafahrt = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _kamerafahrt.TweenProperty(_kameraDrehung, "position", ziel, 0.6);
        _kamerafahrt.TweenProperty(_kamera, "size", zoom, 0.6);
    }

    /// <summary>Dreht die Kamera um ganze Vierteldrehungen (+1 = 90° gegen den Uhrzeigersinn von oben).</summary>
    public void Drehen(int vierteldrehungen, bool sofort = false)
    {
        _zielDrehung += 90f * vierteldrehungen;
        if (sofort) _kameraDrehung.RotationDegrees = new Vector3(0, _zielDrehung, 0);
    }

    // ── Tag und Nacht ───────────────────────────────────────────────────────

    /// <summary>
    /// Sonne bei Tag, Mond bei Nacht – beide werfen Schatten.
    ///
    /// Wann ist ein Schatten sichtbar? Ein Schatten ist die Stelle, an der nur noch das
    /// Umgebungslicht ankommt. Sichtbar wird er erst, wenn das gerichtete Licht (Sonne oder Mond)
    /// deutlich heller ist als das Umgebungslicht. Deshalb ist die Nacht hier nicht einfach „alles
    /// dunkler", sondern: wenig blaues Umgebungslicht + ein klarer, kühler Mond.
    ///
    /// Beide Gestirne wandern in einem Bogen von Osten (+X) über Süden nach Westen. Ihre Stärke
    /// hängt an der Höhe über dem Horizont – so blenden sie in der Dämmerung weich über.
    /// </summary>
    private void LichtNachUhrzeit(float uhrzeitInMinuten)
    {
        var stunde = uhrzeitInMinuten / 60f;
        // Gespielt wird von 05 bis 22 Uhr: Die Sonne steht von 05 bis 20 Uhr am Himmel, danach zwei Stunden Mondnacht.
        var sonnenhoehe = Mathf.Sin((stunde - 5f) / 15f * Mathf.Pi);          // > 0 zwischen 05 und 20 Uhr
        var mondhoehe = Mathf.Sin(((stunde + 4f) % 24f) / 9f * Mathf.Pi);       // > 0 zwischen 20 und 05 Uhr
        var tag = Mathf.Clamp(sonnenhoehe, 0f, 1f);

        if (sonnenhoehe >= mondhoehe)
        {
            var fortschritt = (stunde - 5f) / 15f; // 0 = Aufgang im Osten, 1 = Untergang im Westen
            _sonne.RotationDegrees = new Vector3(-(8 + 52 * tag), 90 - fortschritt * 180, 0);
            _sonne.LightColor = new Color(1f, 0.78f, 0.55f).Lerp(new Color(1f, 0.97f, 0.9f), tag);
            _sonne.LightEnergy = 0.2f + 1.0f * Mathf.Sqrt(tag); // etwas Dämmerlicht schon bei Sonnenaufgang
            _sonne.ShadowOpacity = 1f;
        }
        else
        {
            var nacht = Mathf.Clamp(mondhoehe, 0f, 1f);
            var fortschritt = ((stunde + 4f) % 24f) / 9f; // 0 = Aufgang (20 Uhr), 1 = Untergang (05 Uhr)
            _sonne.RotationDegrees = new Vector3(-(10 + 45 * nacht), 90 - fortschritt * 180, 0);
            _sonne.LightColor = new Color(0.62f, 0.72f, 1f);
            _sonne.LightEnergy = 0.15f + 0.4f * Mathf.Sqrt(nacht);
            _sonne.ShadowOpacity = 0.85f; // Mondschatten sind etwas weicher und weniger tief
        }

        var umgebung = _umgebung.Environment;
        umgebung.BackgroundColor = Himmelsfarbe(uhrzeitInMinuten);
        // Nachts wenig, dafür bläuliches Umgebungslicht – so heben sich Mondschatten und die warme Glut der Essen ab.
        // Dämmerung: Das Umgebungslicht steigt schon ab 04:00 und klingt erst gegen 21:00 ab –
        // so beginnt der Spieltag um 05:00 im Morgengrauen statt in tiefer Nacht.
        var hell = Mathf.Clamp(Mathf.Sin((stunde - 4f) / 17f * Mathf.Pi), 0f, 1f);
        umgebung.AmbientLightColor = new Color(0.30f, 0.36f, 0.58f).Lerp(new Color(0.85f, 0.83f, 0.78f), hell);
        umgebung.AmbientLightEnergy = 0.18f + 0.52f * hell;
        RaumlichtNachUhrzeit(uhrzeitInMinuten, hell);
    }

    // Stützpunkte für den Himmel: (Uhrzeit in Minuten, Farbe).
    private static readonly (int Minute, Color Farbe)[] HimmelsVerlauf =
    {
        (0 * 60,  new Color(0.04f, 0.05f, 0.10f)),
        (4 * 60,  new Color(0.10f, 0.10f, 0.18f)),
        (5 * 60,  new Color(0.65f, 0.48f, 0.38f)), // Morgenrot zur Zunftglocke
        (8 * 60,  new Color(0.52f, 0.66f, 0.78f)),
        (14 * 60, new Color(0.55f, 0.69f, 0.82f)),
        (19 * 60, new Color(0.70f, 0.45f, 0.30f)), // Abendrot
        (20 * 60 + 30, new Color(0.14f, 0.12f, 0.20f)),
        (24 * 60, new Color(0.04f, 0.05f, 0.10f)),
    };

    public static Color Himmelsfarbe(float uhrzeitInMinuten)
    {
        for (var i = 0; i < HimmelsVerlauf.Length - 1; i++)
        {
            var (von, farbeVon) = HimmelsVerlauf[i];
            var (bis, farbeBis) = HimmelsVerlauf[i + 1];
            if (uhrzeitInMinuten < bis)
                return farbeVon.Lerp(farbeBis, (uhrzeitInMinuten - von) / (bis - von));
        }
        return HimmelsVerlauf[0].Farbe;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Kamera und Auswahl
    // ═════════════════════════════════════════════════════════════════════════

    public override void _Process(double delta)
    {
        // Beschriftungen mitskalieren, damit sie bei jedem Zoom gleich groß wirken.
        var massstab = _raum is not null ? RaumSchildMassstab : _kamera.Size / BezugsZoom;
        foreach (var label in _beschriftungen)
            label.Scale = Vector3.One * massstab;
        BeschriftungenZeigen();

        // Sanft auf die Zieldrehung zudrehen (Q/E)
        var aktuell = _kameraDrehung.RotationDegrees.Y;
        _kameraDrehung.RotationDegrees = new Vector3(0, Mathf.LerpAngle(Mathf.DegToRad(aktuell), Mathf.DegToRad(_zielDrehung), (float)Math.Min(1, delta * 8)) * 180 / Mathf.Pi, 0);
        if (_innen is { Raumansicht: false }) Aufschneiden(_innen);
        if (_raum is not null) return; // feste Ansicht: im Raum wird nicht verschoben

        // Verschieben mit Tastatur
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
        _kameraDrehung.Position = new Vector3(Mathf.Clamp(neu.X, -70, 110), 0, Mathf.Clamp(neu.Z, -80, 80));
    }

    public override void _UnhandledInput(InputEvent eingabe)
    {
        if (_raum is not null)
        {
            RaumEingabe(eingabe);
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
        var (min, max) = _innen is null ? (KameraMinZoom, KameraMaxZoom) : (InnenMinZoom, InnenMaxZoom);
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
            var sichtbar = _innen is null && (alle || kennung == _unterDerMaus || kennung == Auswahl);
            if (label.Visible != sichtbar) label.Visible = sichtbar;
        }
    }

    /// <summary>Welches Gebäude liegt an dieser Bildschirmstelle? (Strahl aus der Kamera, trifft die Klickflächen)</summary>
    private string? KennungBei(Vector2 bildschirm)
    {
        var kamera = _raum is not null ? _raumKamera : _kamera;
        var von = kamera.ProjectRayOrigin(bildschirm);
        var bis = von + kamera.ProjectRayNormal(bildschirm) * 500;
        var treffer = GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(von, bis));
        return treffer.Count > 0 && treffer["collider"].As<Node>() is { } koerper && koerper.HasMeta("kennung")
            ? koerper.GetMeta("kennung").AsString() : null;
    }

    private void Auswaehlen(Vector2 mausposition, bool doppelklick)
    {
        if (KennungBei(mausposition) is not { } kennung)
            return;
        GebaeudeAngeklickt?.Invoke(kennung);
        if (doppelklick && AnsichtZu(kennung) is { } ansicht)
            Betreten(ansicht);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Bausteine der Greybox
    // ═════════════════════════════════════════════════════════════════════════

    private static StandardMaterial3D Material(Color farbe) => new() { AlbedoColor = farbe, Roughness = 0.95f };

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

    /// <summary>Ein Haus: Wandquader plus Satteldach.</summary>
    private void Haus(Vector3 grund, Vector3 groesse, Color wand, Color dach, bool quer)
    {
        Quader(grund + new Vector3(0, groesse.Y / 2, 0), groesse, Material(wand));
        var dachhoehe = Mathf.Min(groesse.X, groesse.Z) * 0.6f;
        Dach(grund + new Vector3(0, groesse.Y, 0), new Vector3(groesse.X + 0.6f, dachhoehe, groesse.Z + 0.6f), Material(dach), quer);
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

    /// <summary>Ein flacher Weg zwischen zwei Punkten.</summary>
    private void WegZiehen(Vector3 von, Vector3 bis, float breite, Node3D? eltern = null)
    {
        var mitte = (von + bis) / 2;
        var laenge = von.DistanceTo(bis);
        var weg = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(breite, 0.06f, laenge + breite * 0.5f) },
            MaterialOverride = Material(Weg),
            Position = new Vector3(mitte.X, 0.04f, mitte.Z),
            Rotation = new Vector3(0, Mathf.Atan2(bis.X - von.X, bis.Z - von.Z), 0),
        };
        (eltern ?? this).AddChild(weg);
    }

    /// <summary>Unsichtbarer Körper, damit Mausklicks das Gebäude treffen. Die Form lässt sich abschalten (Innenansicht).</summary>
    private CollisionShape3D Klickflaeche(string kennung, Vector3 mitte, Vector3 groesse, Node3D? eltern = null)
    {
        var koerper = new StaticBody3D { Position = mitte };
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
        _beschriftungen.Add(label);
        (eltern ?? this).AddChild(label);
        return label;
    }
}
