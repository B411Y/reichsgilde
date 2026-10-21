using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Die Kartenwerkstatt (Level-Editor) in der Stadtansicht – ein Teil der <c>partial class Stadtansicht</c>. Die Szene „Kartenwerkstatt“
/// (<see cref="Kartenwerkstatt"/>) hat Tafeln, Dateien und Startoptionen; hier liegt alles, was in der 3D-Welt passiert.
///
/// <b>Zeichnen.</b> Boden, Mauern, Wege und Natur kommen aus denselben Methoden wie im Spiel (Stadtansicht.Karte.cs), einmal beim Öffnen.
/// Jedes Gebäude des Entwurfs dagegen ist ein eigenes „Stück“: ein Knoten an Punkt und Drehung der Karte mit Modell, Klickfläche, Namen
/// und Markierung. Ändert ein Befehl ein Gebäude (Ereignis <see cref="Kartenentwurf.Geaendert"/>), wird nur sein Stück neu gebaut.
/// Werkstattplätze zeigen das Werkstattmodell des Schmieds (Stufe 1, ohne Hausfarbe): Alle Werkstätten haben dieselbe Normfläche
/// 16 × 14 m, und ein Haus zeigt Abstände und Höhe besser als ein flacher Bauplatz-Zaun. Wohnhausplätze zeigen Fachwerkhäuser (ein
/// Doppelplatz zwei), Einwohnerplätze ein Einwohnerhaus, alle anderen das Modell ihres Bausteins (ohne Modell ein grauer Block).
///
/// <b>Bedienen.</b> Ein Baustein des Katalogs gewählt (<see cref="WerkstattBausteinWaehlen"/>): Sein Umriss folgt der Maus auf dem
/// 1-m-Raster, Q/E dreht in 15°-Schritten, mit gehaltener Alt-Taste frei; grün = an dieser Stelle kein Fehler
/// (<see cref="Kartenpruefung.PruefeStelle"/>), rot = mit dem ersten Fehler als Text. Klick setzt. Sonst wählt ein Klick ein Stück
/// (goldener Rahmen), Ziehen verschiebt es (ein Rückgängig-Schritt je Ziehen), Q/E dreht das gewählte, Entf löscht es. Stücke mit Fehler
/// (<see cref="Kartenentwurf.FehlerhafteGebaeude"/>) leuchten rot. Die Kamera dreht hier mit Strg+Q/E oder Bild↑/Bild↓.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Der Entwurf, den die Kartenwerkstatt zeigt (null: im Spiel und im Hauptmenü).</summary>
    private Kartenentwurf? _entwurf;

    private Node3D? _stueckeKnoten;
    private readonly Dictionary<EntwurfGebaeude, Stueck> _stuecke = new();

    /// <summary>Ein Gebäude des Entwurfs in der Welt: Wurzel an Punkt und Drehung, darunter Modell, Klickfläche, Name und Markierung.</summary>
    private sealed class Stueck
    {
        public required Node3D Wurzel { get; init; }

        /// <summary>Alles Sichtbare des Modells (bekommt den roten Überzug, wenn das Stück einen Fehler hat).</summary>
        public required Node3D Modell { get; init; }

        /// <summary>Rahmen um die Grundfläche: gold, wenn gewählt, rot bei Fehlern.</summary>
        public required Node3D Markierung { get; init; }

        public required MeshInstance3D[] Rahmen { get; init; }
        public required MeshInstance3D Fuellung { get; init; }
        public bool Fehlerhaft { get; set; }
    }

    /// <summary>Kennung der Klickfläche eines Stücks (Präfix „stueck:“, damit sie sich nicht mit dem Spiel mischt).</summary>
    private static string StueckKennung(EntwurfGebaeude g) => "stueck:" + g.Kennung;

    /// <summary>Gesperrt, solange die Oberfläche eine Rückfrage zeigt: Klicks und Tasten gehen dann nicht an die Karte.</summary>
    public bool WerkstattGesperrt { get; set; }

    /// <summary>Kennung des gewählten Gebäudes (null: keins).</summary>
    public string? WerkstattAuswahl { get; private set; }

    /// <summary>Die Auswahl hat sich geändert (Klick, Setzen, Löschen) – die Tafel rechts zeigt das neue Stück.</summary>
    public event Action<string?>? WerkstattAuswahlGeaendert;

    /// <summary>Der Baustein, der gerade gesetzt wird (null: keiner).</summary>
    public string? WerkstattBaustein { get; private set; }

    /// <summary>Das Setzen wurde beendet (Esc, Rechtsklick) – der Katalog hebt die Wahl auf.</summary>
    public event Action? WerkstattSetzenBeendet;

    /// <summary>Die Drehung des Umrisses beim Setzen (Grad).</summary>
    public double WerkstattDrehung { get; set; }

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet einen Entwurf: Boden, Mauern, Wege, Natur und Bauzonen (halbtransparent) wie im Spiel, dazu jedes Gebäude als Stück.
    /// Einmal je Szene – ein anderer Entwurf lädt die Szene neu. Licht fest auf hellen Mittag, ohne Passanten, Karren und Vögel.
    /// </summary>
    public void EntwurfZeigen(Kartenentwurf entwurf)
    {
        if (_karteGebaut) return;
        _karteGebaut = true;
        _entwurf = entwurf;
        _karte = HintergrundVon(entwurf);
        if (_karte is { } karte)
        {
            LagenLesen(karte);
            var (x0, z0, x1, z1) = entwurf.Grenzen();
            _kartenRahmen = _kartenRahmen.Merge(new Rect2((float)x0, (float)z0, (float)(x1 - x0), (float)(z1 - z0))).Grow(30);
            BodenLegen(karte);
            foreach (var mauer in karte.Mauern) MauerAusKarte(mauer);
            WegeAnlegen(karte);
            NaturAusKarte(karte);
            KameraAusKarte(karte);
            Baumodus = true; // die Bauzonen der Karte als farbige Flächen mit Raster (Stadtansicht.Bauen.cs)
        }
        else Flaeche(Vector3.Zero, new Vector2(520, 480), Wiese, -0.05f);
        if (entwurf.Groesse is not null) KartenrandZeichnen(entwurf.Grenzen());

        _stueckeKnoten = new Node3D { Name = "Stuecke" };
        AddChild(_stueckeKnoten);
        foreach (var g in entwurf.Gebaeude) StueckBauen(g);
        FehlerZeigen();
        entwurf.Geaendert += EntwurfGeaendert;
        LichtNachUhrzeit(12 * 60, 100); // heller Mittag, sofort ohne Überblenden
    }

    /// <summary>
    /// Die Karte für Boden, Mauern, Wege und Natur: der Entwurf, wie der Kern ihn zum Spielen liest. Hat er Fehler, die der Leser nicht
    /// annimmt (z. B. einen unbekannten Baustein), ohne die Gebäude – dann wächst die Natur auch dort, wo Gebäude stehen.
    /// </summary>
    private static Stadtkarte? HintergrundVon(Kartenentwurf entwurf)
    {
        var json = entwurf.ZuJson();
        try
        {
            return Stadtkarte.AusJson(json, entwurf.Katalog);
        }
        catch (KartenFehler) { }
        try
        {
            var ohne = JsonNode.Parse(json)!.AsObject();
            ohne["gebaeude"] = new JsonArray();
            return Stadtkarte.AusJson(ohne.ToJsonString(), entwurf.Katalog);
        }
        catch (Exception ex) when (ex is KartenFehler or JsonException)
        {
            GD.PushWarning($"Kartenwerkstatt: Die Karte „{entwurf.Kennung}“ lässt sich nicht zeichnen ({ex.Message}) – nur die Gebäude erscheinen.");
            return null;
        }
    }

    /// <summary>Der Rand einer Karte mit Größe (neue Karten): ein heller Strich am Boden.</summary>
    private void KartenrandZeichnen((double X0, double Z0, double X1, double Z1) g)
    {
        var stoff = Transparent(new Color(0.95f, 0.88f, 0.6f), 0.7f);
        var (x0, z0, x1, z1) = ((float)g.X0, (float)g.Z0, (float)g.X1, (float)g.Z1);
        var rand = new Node3D { Name = "Kartenrand" };
        AddChild(rand);
        Quader(new Vector3((x0 + x1) / 2, 0.1f, z0), new Vector3(x1 - x0, 0.1f, 0.6f), stoff, rand);
        Quader(new Vector3((x0 + x1) / 2, 0.1f, z1), new Vector3(x1 - x0, 0.1f, 0.6f), stoff, rand);
        Quader(new Vector3(x0, 0.1f, (z0 + z1) / 2), new Vector3(0.6f, 0.1f, z1 - z0), stoff, rand);
        Quader(new Vector3(x1, 0.1f, (z0 + z1) / 2), new Vector3(0.6f, 0.1f, z1 - z0), stoff, rand);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Stücke
    // ═════════════════════════════════════════════════════════════════════════

    private void EntwurfGeaendert(Kartenaenderung a)
    {
        switch (a.Art)
        {
            case Aenderungsart.Gesetzt:
                StueckBauen(a.Gebaeude);
                break;
            case Aenderungsart.Geloescht:
                StueckEntfernen(a.Gebaeude);
                if (WerkstattAuswahl == a.Gebaeude.Kennung) WerkstattWaehlen(null);
                break;
            case Aenderungsart.Geaendert:
                // Neu bauen statt nur verschieben: Maßstab, Name und Baustein können sich mitgeändert haben
                StueckEntfernen(a.Gebaeude);
                StueckBauen(a.Gebaeude);
                break;
        }
        FehlerZeigen();
    }

    private void StueckEntfernen(EntwurfGebaeude g)
    {
        if (_stuecke.Remove(g, out var s)) KnotenEntfernen(s.Wurzel);
    }

    /// <summary>Baut das Stück eines Gebäudes: Modell nach Art (siehe Kopf der Datei), Klickfläche über der Grundfläche, Name, Markierung.</summary>
    private void StueckBauen(EntwurfGebaeude g)
    {
        if (_stueckeKnoten is null) return;
        var wurzel = new Node3D
        {
            Name = "Stueck_" + g.Kennung,
            Position = new Vector3((float)g.X, 0, (float)g.Z),
            Rotation = new Vector3(0, Mathf.DegToRad((float)g.Drehung), 0),
        };
        _stueckeKnoten.AddChild(wurzel);
        var modell = new Node3D { Name = "Modell" };
        wurzel.AddChild(modell);
        var groesse = g.Groesse is var (b, t) ? new Vector2((float)b, (float)t) : new Vector2(4, 4);
        StueckModell(g, modell, groesse);

        // Klickfläche und Name: so hoch wie das Modell (flache wie der Markt mindestens 2 m; Häuser höchstens 12 m, sonst fängt ihre Klickfläche
        // im Schrägblick die Klicks auf die Nachbarn dahinter; Sonderbauten und Berge bis 30 m)
        var hoehe = Mathf.Clamp(MasseImModell(modell).End.Y, 2, g.Art is Bausteinart.Sonderbau or Bausteinart.Berg ? 30 : 12);
        var kennung = StueckKennung(g);
        Klickflaeche(kennung, new Vector3(0, hoehe / 2, 0), new Vector3(groesse.X, hoehe, groesse.Y), wurzel);
        Beschriftung(g.Name, new Vector3(0, hoehe + 4, 0), 44, wurzel, kennung);

        var (markierung, rahmen, fuellung) = MarkierungBauen(groesse, wurzel);
        var stueck = new Stueck { Wurzel = wurzel, Modell = modell, Markierung = markierung, Rahmen = rahmen, Fuellung = fuellung };
        _stuecke[g] = stueck;
        MarkierungSetzen(g, stueck);
    }

    /// <summary>Das Modell eines Stücks nach der Art seines Bausteins, lokal: Mitte der Grundfläche im Ursprung, Vorderseite nach +z.</summary>
    private void StueckModell(EntwurfGebaeude g, Node3D modell, Vector2 groesse)
    {
        var m = (float)g.Massstab;
        var versatz = g.Baustein?.Versatz is { Length: 2 } v ? new Vector3((float)v[0], 0, (float)v[1]) * m : Vector3.Zero;
        var stabil = g.Kennung.Sum(c => c); // eine feste Zahl je Kennung für Variante und Hausfarbe (string.GetHashCode ändert sich je Start)
        switch (g.Art)
        {
            case Bausteinart.Werkstattplatz:
                Einsetzen(WerkstattModellDatei("Schmied"), new ModellZustand(Stufe: 1));
                break;
            case Bausteinart.Wohnhausplatz when Katalog?.Wohnhaus(0) is { Modell: { } datei } fachwerk:
            {
                // Fachwerkhäuser mit der Front an der Vorderkante; ein Doppelplatz (17 m) bekommt zwei
                var anzahl = Math.Max(1, (int)(groesse.X / 8));
                var breite = groesse.X / anzahl;
                for (var i = 0; i < anzahl; i++)
                    ModellEinsetzen(datei, modell, new Vector3(-groesse.X / 2 + breite * (i + 0.5f), 0, groesse.Y / 2 - ModellFront(fachwerk)),
                        new ModellZustand(Variante: (stabil + i) % 3 + 1, Besitzer: Hausfarben[(stabil + i) % Hausfarben.Length]));
                break;
            }
            case Bausteinart.Einwohnerplatz:
            {
                var art = groesse.X >= 8 ? Hausart.Buergerhaus : Hausart.Wohnhaus;
                ModellEinsetzen(EinwohnerDatei(art), modell, new Vector3(0, 0, groesse.Y / 2 - EinwohnerFront(art)),
                    new ModellZustand(Variante: stabil % 3 + 1, Besitzer: Hausfarben[stabil % Hausfarben.Length]));
                break;
            }
            case Bausteinart.Umlandstelle when g.Baustein is { Modell: { } datei } b:
                StelleEinsetzen(datei, b.Knoten ?? g.Name);
                break;
            case Bausteinart.Kulisse when g.Baustein?.Modell is { } datei:
                Einsetzen(datei, new ModellZustand(Variante: stabil % 3 + 1, Besitzer: Hausfarben[stabil % Hausfarben.Length]));
                break;
            default:
                if (g.Baustein?.Modell is { } eigenes) Einsetzen(eigenes, new ModellZustand());
                else Block();
                break;
        }

        // Ein Modell an der Lage der Karte (versatz und Maßstab wie ModellLage)
        void Einsetzen(string datei, ModellZustand zustand)
        {
            if (ModellEinsetzen(datei, modell, Vector3.Zero, zustand) is { } instanz)
                instanz.Wurzel.Transform = new Transform3D(Basis.Identity.Scaled(Vector3.One * m), versatz);
        }

        // Eine Arbeitsstelle aus dem Baukasten (Weide, Feld, Wald) – ihr Knoten samt Kindern (Laubkrone)
        void StelleEinsetzen(string datei, string knoten)
        {
            if (Modell(datei)?.Instantiate<Node3D>() is not { } kasten) return;
            if (kasten.FindChild(knoten, true, false) is MeshInstance3D { Mesh: { } netz } teil)
            {
                LaubMelden(netz);
                var stelle = new MeshInstance3D { Name = knoten, Mesh = netz, Transform = new Transform3D(Basis.Identity.Scaled(Vector3.One * m), versatz) };
                foreach (var kind in teil.GetChildren().OfType<MeshInstance3D>())
                    stelle.AddChild(new MeshInstance3D { Name = kind.Name, Mesh = kind.Mesh, Transform = kind.Transform });
                modell.AddChild(stelle);
                KronenMelden(stelle);
            }
            else GD.PushError($"Modell unvollständig: res://assets/modelle/{datei}.glb hat keinen Knoten „{knoten}“.");
            kasten.Free();
        }

        // Ohne Modell (Kirche, Hafen): ein grauer Block mit Dach auf der Fläche ohne Vorplatz, bis es eins gibt
        void Block()
        {
            const float hoehe = 6;
            var haus = new Vector2(Mathf.Max(1, groesse.X - 1), Mathf.Max(1, groesse.Y - (float)Kartenpruefung.Vorplatz - 1));
            var mitte = new Vector3(0, 0, -(float)Kartenpruefung.Vorplatz / 2);
            Quader(mitte + new Vector3(0, hoehe / 2, 0), new Vector3(haus.X, hoehe, haus.Y), Material(Bruchstein), modell);
            Dach(mitte + new Vector3(0, hoehe, 0), new Vector3(haus.X + 0.4f, 3, haus.Y + 0.4f), Material(Schiefer), quer: true, modell);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Markierung: gewählt (gold) und Fehler (rot)
    // ═════════════════════════════════════════════════════════════════════════

    private static readonly Color Auswahlgold = new(1f, 0.82f, 0.3f);
    private static readonly Color Fehlerrot = new(1f, 0.22f, 0.18f);
    private StandardMaterial3D? _rahmenGold, _rahmenRot, _flaecheGold, _flaecheRot, _fehlerUeberzug;

    /// <summary>Ein Rahmen um die Grundfläche und eine Linie an der Kante des Vorplatzes (2 m hinter der Vorderkante), dazu eine leichte Füllung.</summary>
    private (Node3D Knoten, MeshInstance3D[] Rahmen, MeshInstance3D Fuellung) MarkierungBauen(Vector2 groesse, Node3D wurzel)
    {
        // Der Rahmen scheint durch die Häuser (ohne Tiefentest) – sonst verdecken sie im Schrägblick die hintere Hälfte und Nachbarn den Rest
        _rahmenGold ??= ImmerSichtbar(Transparent(Auswahlgold, 0.95f));
        _rahmenRot ??= ImmerSichtbar(Transparent(Fehlerrot, 0.95f));
        _flaecheGold ??= Transparent(Auswahlgold, 0.18f);
        _flaecheRot ??= Transparent(Fehlerrot, 0.28f);
        var knoten = new Node3D { Name = "Markierung", Visible = false };
        wurzel.AddChild(knoten);
        var (hb, ht) = (groesse.X / 2, groesse.Y / 2);
        const float dicke = 0.45f, hoehe = 0.25f;
        var vorplatz = ht - (float)Kartenpruefung.Vorplatz;
        var rahmen = new[]
        {
            Quader(new Vector3(0, hoehe / 2, -ht), new Vector3(groesse.X + dicke, hoehe, dicke), _rahmenGold, knoten),
            Quader(new Vector3(0, hoehe / 2, ht), new Vector3(groesse.X + dicke, hoehe, dicke), _rahmenGold, knoten),
            Quader(new Vector3(-hb, hoehe / 2, 0), new Vector3(dicke, hoehe, groesse.Y), _rahmenGold, knoten),
            Quader(new Vector3(hb, hoehe / 2, 0), new Vector3(dicke, hoehe, groesse.Y), _rahmenGold, knoten),
            Quader(new Vector3(0, hoehe / 2, vorplatz), new Vector3(groesse.X, hoehe * 0.6f, dicke * 0.4f), _rahmenGold, knoten),
        };
        var fuellung = Quader(new Vector3(0, 0.12f, 0), new Vector3(groesse.X, 0.08f, groesse.Y), _flaecheGold, knoten);
        foreach (var teil in rahmen.Append(fuellung)) teil.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return (knoten, rahmen, fuellung);
    }

    private static StandardMaterial3D ImmerSichtbar(StandardMaterial3D stoff)
    {
        stoff.NoDepthTest = true;
        stoff.RenderPriority = 1;
        return stoff;
    }

    /// <summary>Zeigt Rahmen und roten Überzug eines Stücks nach Auswahl und Fehlern.</summary>
    private void MarkierungSetzen(EntwurfGebaeude g, Stueck s)
    {
        var gewaehlt = WerkstattAuswahl == g.Kennung;
        s.Markierung.Visible = gewaehlt || s.Fehlerhaft;
        foreach (var teil in s.Rahmen) teil.MaterialOverride = s.Fehlerhaft ? _rahmenRot : _rahmenGold;
        s.Fuellung.MaterialOverride = s.Fehlerhaft ? _flaecheRot : _flaecheGold;
        // gewählt UND fehlerhaft: der Rahmen rot, die Füllung gold – beides ist zu sehen
        if (gewaehlt && s.Fehlerhaft) s.Fuellung.MaterialOverride = _flaecheGold;
    }

    /// <summary>Nach jeder Änderung: Welche Stücke haben einen Fehler? Nur wo sich das ändert, wird der Überzug umgeschaltet.</summary>
    private void FehlerZeigen()
    {
        if (_entwurf is null) return;
        _fehlerUeberzug ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(Fehlerrot, 0.42f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        var fehlerhaft = _entwurf.FehlerhafteGebaeude;
        foreach (var (g, s) in _stuecke)
        {
            var neu = fehlerhaft.Contains(g.Kennung);
            if (neu == s.Fehlerhaft) continue;
            s.Fehlerhaft = neu;
            foreach (var teil in s.Modell.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
                teil.MaterialOverlay = neu ? _fehlerUeberzug : null;
            MarkierungSetzen(g, s);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Auswahl und Kamera
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Wählt ein Gebäude (null: keins) – mit <paramref name="hinfahren"/> fährt die Kamera hin.</summary>
    public void WerkstattWaehlen(string? kennung, bool hinfahren = false)
    {
        if (_entwurf?.GebaeudeMit(kennung) is null) kennung = null;
        var vorher = WerkstattAuswahl;
        WerkstattAuswahl = kennung;
        Auswahl = kennung is null ? null : "stueck:" + kennung; // der Name bleibt sichtbar
        foreach (var (g, s) in _stuecke)
            if (g.Kennung == vorher || g.Kennung == kennung) MarkierungSetzen(g, s);
        if (hinfahren && _entwurf?.GebaeudeMit(kennung) is { } gebaeude) WerkstattHinfahren(gebaeude.Punkt);
        if (vorher != kennung) WerkstattAuswahlGeaendert?.Invoke(kennung);
    }

    /// <summary>Fährt die Kamera zu einem Ort der Karte (Zoom bleibt).</summary>
    public void WerkstattHinfahren(Kartenort ort) => Kamerafahrt(new Vector3((float)ort.X, 0, (float)ort.Z), _kamera.Size);

    // ═════════════════════════════════════════════════════════════════════════
    //  Setzen: der Umriss an der Maus
    // ═════════════════════════════════════════════════════════════════════════

    private Node3D? _wsUmriss;
    private MeshInstance3D? _wsFlaeche, _wsVorplatz, _wsKoerper, _wsPfeil;
    private StandardMaterial3D? _wsFarbe, _wsVorplatzFarbe, _wsKoerperFarbe, _wsPfeilFarbe;
    private Label3D? _wsText;

    /// <summary>Nur für Startoptionen: Der Umriss steht hier, bis die Maus bewegt wird.</summary>
    private Vector2? _wsOrt;

    /// <summary>Was zuletzt geprüft wurde (Baustein, Punkt, Drehung, Kennung) und mit welchem Ergebnis – geprüft wird nur, wenn sich das ändert.</summary>
    private (string Katalog, double X, double Z, double Drehung, double Massstab, string? Kennung)? _wsGeprueft;

    private string? _wsGrund;

    /// <summary>Wählt einen Baustein zum Setzen (null: Setzen beenden). Die Auswahl eines Stücks bleibt.</summary>
    public void WerkstattBausteinWaehlen(string? katalog)
    {
        ZiehenAbbrechen();
        WerkstattBaustein = _entwurf?.Katalog.Finde(katalog) is null ? null : katalog;
        _wsGeprueft = null;
        if (WerkstattBaustein is null && _wsUmriss is not null) _wsUmriss.Visible = false;
    }

    /// <summary>Für Startoptionen: Der Umriss des gewählten Bausteins steht an dieser Stelle (Meter), bis die Maus bewegt wird.</summary>
    public void WerkstattUmrissSetzen(double x, double z) => _wsOrt = new Vector2((float)x, (float)z);

    /// <summary>Jedes Bild (aus der Szene): Umriss beim Setzen und beim Ziehen nachführen.</summary>
    public void WerkstattAktualisieren()
    {
        if (_entwurf is null) return;
        if (_wsZiehen is { Aktiv: true } z)
        {
            ZiehenNachfuehren(z);
            return;
        }
        if (WerkstattBaustein is not { } katalog) return;
        var ort = _wsOrt;
        if (ort is null && !WerkstattGesperrt && GetViewport().GuiGetHoveredControl() is null)
            ort = BodenpunktBei(GetViewport().GetMousePosition());
        if (ort is null)
        {
            if (_wsUmriss is not null) _wsUmriss.Visible = false;
            return;
        }
        var (x, zz) = Einrasten(ort.Value.X, ort.Value.Y);
        UmrissZeigen(katalog, x, zz, WerkstattDrehung, 1, null);
    }

    /// <summary>Auf das 1-m-Raster – mit gehaltener Alt-Taste frei (der Kern rundet dann auf 5 cm).</summary>
    private static (double X, double Z) Einrasten(double x, double z) =>
        Input.IsKeyPressed(Key.Alt) ? (x, z) : (Kartenraster.Runden(x), Kartenraster.Runden(z));

    /// <summary>Zeigt den Umriss eines Bausteins an einer Stelle: grün ohne Fehler, rot mit dem ersten Fehler als Text.</summary>
    private void UmrissZeigen(string katalog, double x, double z, double drehung, double massstab, string? kennung)
    {
        if (_entwurf is null || _entwurf.Katalog.Finde(katalog) is not { } baustein) return;
        var stand = (katalog, x, z, drehung, massstab, kennung);
        if (_wsGeprueft != stand)
        {
            _wsGeprueft = stand;
            _wsGrund = Kartenpruefung.PruefeStelle(_entwurf, katalog, x, z, drehung, massstab, kennung)
                .FirstOrDefault(b => b.Schwere == Schwere.Fehler)?.Text;
        }
        var groesse = baustein.Groesse is { Length: 2 } gr ? new Vector2((float)gr[0], (float)gr[1]) * (float)massstab : new Vector2(4, 4);
        if (_wsUmriss is null)
        {
            _wsUmriss = new Node3D { Name = "Werkstattumriss" };
            AddChild(_wsUmriss);
            _wsFarbe = Transparent(Colors.White, 0.45f);
            _wsVorplatzFarbe = Transparent(Colors.White, 0.3f);
            _wsKoerperFarbe = Transparent(Colors.White, 0.16f);
            _wsPfeilFarbe = Transparent(Colors.White, 0.85f);
            _wsFlaeche = new MeshInstance3D { Mesh = new BoxMesh(), MaterialOverride = _wsFarbe };
            _wsVorplatz = new MeshInstance3D { Mesh = new BoxMesh(), MaterialOverride = _wsVorplatzFarbe };
            _wsKoerper = new MeshInstance3D { Mesh = new BoxMesh(), MaterialOverride = _wsKoerperFarbe };
            _wsPfeil = new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(2.4f, 1.2f, 0.3f) }, MaterialOverride = _wsPfeilFarbe, RotationDegrees = new Vector3(90, 0, 0) };
            foreach (var teil in new[] { _wsFlaeche, _wsVorplatz, _wsKoerper, _wsPfeil })
            {
                teil.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                _wsUmriss.AddChild(teil);
            }
            _wsText = Beschriftung("", Vector3.Zero, 40, _wsUmriss);
            _wsText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _wsText.Width = 620;
        }
        const float hoehe = 6;
        var vorplatz = (float)Kartenpruefung.Vorplatz;
        _wsUmriss.Visible = true;
        _wsUmriss.Position = new Vector3((float)x, 0, (float)z);
        _wsUmriss.Rotation = new Vector3(0, Mathf.DegToRad((float)drehung), 0);
        var farbe = _wsGrund is null ? new Color(0.35f, 0.9f, 0.35f) : new Color(0.95f, 0.28f, 0.25f);
        _wsFarbe!.AlbedoColor = new Color(farbe, 0.45f);
        _wsVorplatzFarbe!.AlbedoColor = new Color(farbe.Lightened(0.35f), 0.3f);
        _wsKoerperFarbe!.AlbedoColor = new Color(farbe, 0.16f);
        _wsPfeilFarbe!.AlbedoColor = new Color(farbe.Lightened(0.5f), 0.85f);
        ((BoxMesh)_wsFlaeche!.Mesh).Size = new Vector3(groesse.X, 0.12f, groesse.Y);
        _wsFlaeche.Position = new Vector3(0, 0.14f, 0);
        ((BoxMesh)_wsVorplatz!.Mesh).Size = new Vector3(groesse.X, 0.16f, vorplatz);
        _wsVorplatz.Position = new Vector3(0, 0.16f, groesse.Y / 2 - vorplatz / 2);
        ((BoxMesh)_wsKoerper!.Mesh).Size = new Vector3(Mathf.Max(0.4f, groesse.X - 0.4f), hoehe, Mathf.Max(0.4f, groesse.Y - vorplatz - 0.4f));
        _wsKoerper.Position = new Vector3(0, hoehe / 2, -vorplatz / 2);
        _wsPfeil!.Position = new Vector3(0, 0.3f, groesse.Y / 2 - vorplatz / 2); // die Spitze zeigt zur Vorderseite (+z)
        _wsText!.Position = new Vector3(0, hoehe + 4, 0);
        _wsText.Text = _wsGrund ?? (kennung is null ? $"{baustein.Anzeigename} – passt" : "passt");
        _wsText.Modulate = farbe.Lightened(0.35f);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Ziehen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Ein Stück, das gerade gezogen wird: wo die Maus anfasste, der Abstand zur Mitte und die vorläufige Lage.</summary>
    private sealed class Ziehvorgang
    {
        public required EntwurfGebaeude Gebaeude { get; init; }
        public required Vector2 Start { get; init; }
        public required Vector2 Versatz { get; init; }
        public bool Aktiv { get; set; }
        public double X { get; set; }
        public double Z { get; set; }
        public double Drehung { get; set; }
    }

    private Ziehvorgang? _wsZiehen;

    /// <summary>So viele Pixel muss die Maus mit gedrückter Taste wandern, bis aus dem Klick ein Ziehen wird.</summary>
    private const float ZiehSchwelle = 5;

    private void ZiehenNachfuehren(Ziehvorgang z)
    {
        if (BodenpunktBei(GetViewport().GetMousePosition()) is { } boden)
            (z.X, z.Z) = Einrasten(boden.X + z.Versatz.X, boden.Y + z.Versatz.Y);
        if (_stuecke.TryGetValue(z.Gebaeude, out var s))
        {
            s.Wurzel.Position = new Vector3((float)z.X, 0, (float)z.Z);
            s.Wurzel.Rotation = new Vector3(0, Mathf.DegToRad((float)z.Drehung), 0);
        }
        UmrissZeigen(z.Gebaeude.Katalog, z.X, z.Z, z.Drehung, z.Gebaeude.Massstab, z.Gebaeude.Kennung);
    }

    /// <summary>Loslassen: Hat sich Lage oder Drehung geändert, wird daraus ein Befehl (ein Rückgängig-Schritt).</summary>
    private void ZiehenBeenden()
    {
        if (_wsZiehen is not { } z || _entwurf is null) return;
        _wsZiehen = null;
        if (_wsUmriss is not null) _wsUmriss.Visible = false;
        if (!z.Aktiv) return;
        var g = z.Gebaeude;
        if (Kartenraster.Punkt(z.X) != g.X || Kartenraster.Punkt(z.Z) != g.Z || Kartenraster.Drehung(z.Drehung) != g.Drehung)
            _entwurf.Versetzen(g.Kennung, z.X, z.Z, z.Drehung); // baut das Stück neu (EntwurfGeaendert)
        else if (_stuecke.TryGetValue(g, out var s))
        {
            s.Wurzel.Position = new Vector3((float)g.X, 0, (float)g.Z);
            s.Wurzel.Rotation = new Vector3(0, Mathf.DegToRad((float)g.Drehung), 0);
        }
    }

    /// <summary>Esc beim Ziehen: Das Stück springt an seinen Platz zurück, es entsteht kein Befehl.</summary>
    private void ZiehenAbbrechen()
    {
        if (_wsZiehen is not { } z) return;
        _wsZiehen = null;
        if (_wsUmriss is not null) _wsUmriss.Visible = false;
        if (!_stuecke.TryGetValue(z.Gebaeude, out var s)) return;
        s.Wurzel.Position = new Vector3((float)z.Gebaeude.X, 0, (float)z.Gebaeude.Z);
        s.Wurzel.Rotation = new Vector3(0, Mathf.DegToRad((float)z.Gebaeude.Drehung), 0);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Eingabe
    // ═════════════════════════════════════════════════════════════════════════

    private float _wsRechtsGezogen;

    /// <summary>
    /// Die Eingabe der Kartenwerkstatt – vor der Kamera (wie <see cref="BauEingabe"/>). Gibt true zurück, wenn verbraucht. Mausrad, rechte
    /// Maustaste und Strg+Q/E bleiben bei der Kamera.
    /// </summary>
    private bool WerkstattEingabe(InputEvent eingabe)
    {
        if (_entwurf is null) return false;
        if (WerkstattGesperrt) return eingabe is InputEventMouseButton { ButtonIndex: MouseButton.Left };
        switch (eingabe)
        {
            case InputEventKey { Pressed: true, Keycode: Key.Q or Key.E, CtrlPressed: false } taste:
                StueckDrehen(taste.Keycode == Key.Q ? 1 : -1, taste.AltPressed, taste.Echo);
                return true;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Pageup or Key.Pagedown } taste:
                _zielDrehung += taste.Keycode == Key.Pageup ? -90 : 90; // die Kamera (Q/E drehen hier das Stück)
                return true;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Delete } when _wsZiehen is null && WerkstattAuswahl is { } weg:
                _entwurf.Loeschen(weg);
                return true;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }:
                if (_wsZiehen is not null) ZiehenAbbrechen();
                else if (WerkstattBaustein is not null) { WerkstattBausteinWaehlen(null); WerkstattSetzenBeendet?.Invoke(); }
                else if (WerkstattAuswahl is not null) WerkstattWaehlen(null);
                else return false;
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } klick:
                LinksDruecken(klick.Position);
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
                ZiehenBeenden();
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right } rechts:
                if (rechts.Pressed) _wsRechtsGezogen = 0;
                else if (_wsRechtsGezogen < 6 && WerkstattBaustein is not null) { WerkstattBausteinWaehlen(null); WerkstattSetzenBeendet?.Invoke(); }
                return false; // das Ziehen der Kamera läuft weiter
            case InputEventMouseMotion bewegung:
                _wsOrt = null;
                if (Input.IsMouseButtonPressed(MouseButton.Right)) _wsRechtsGezogen += bewegung.Relative.Length();
                if (_wsZiehen is { Aktiv: false } z && bewegung.Position.DistanceTo(z.Start) > ZiehSchwelle) z.Aktiv = true;
                return false;
        }
        return false;
    }

    /// <summary>Linke Maustaste: beim Setzen das Stück setzen, sonst ein Stück wählen (und vielleicht ziehen) oder die Auswahl aufheben.</summary>
    private void LinksDruecken(Vector2 maus)
    {
        if (_entwurf is null) return;
        if (WerkstattBaustein is { } katalog)
        {
            if (_wsUmriss is not { Visible: true }) return;
            var g = _entwurf.Setzen(katalog, _wsUmriss.Position.X, _wsUmriss.Position.Z, WerkstattDrehung);
            WerkstattWaehlen(g.Kennung);
            return;
        }
        var kennung = KennungBei(maus) is { } k && k.StartsWith("stueck:") ? k["stueck:".Length..] : null;
        WerkstattWaehlen(kennung);
        if (_entwurf.GebaeudeMit(kennung) is not { } gebaeude || BodenpunktBei(maus) is not { } boden) return;
        _wsZiehen = new Ziehvorgang
        {
            Gebaeude = gebaeude, Start = maus, Versatz = new Vector2((float)gebaeude.X - boden.X, (float)gebaeude.Z - boden.Y),
            X = gebaeude.X, Z = gebaeude.Z, Drehung = gebaeude.Drehung,
        };
    }

    /// <summary>
    /// Q/E: dreht, was gerade bewegt wird – den Umriss beim Setzen, das gezogene Stück oder (als Befehl) das gewählte. In 15°-Schritten auf das
    /// Raster, mit Alt um 1°. <paramref name="richtung"/> +1 = gegen den Uhrzeigersinn (von oben), −1 = mit ihm.
    /// </summary>
    private void StueckDrehen(int richtung, bool frei, bool wiederholt)
    {
        double Weiter(double grad) => frei ? Kartenraster.Drehung(grad + richtung) : Kartenraster.DrehungRunden(grad + richtung * Kartenraster.Drehschritt);
        if (_wsZiehen is { } z) z.Drehung = Weiter(z.Drehung);
        else if (WerkstattBaustein is not null) WerkstattDrehung = Weiter(WerkstattDrehung);
        else if (!wiederholt && _entwurf?.GebaeudeMit(WerkstattAuswahl) is { } g) _entwurf.Drehen(g.Kennung, Weiter(g.Drehung)); // gehaltene Taste: kein Befehl je Wiederholung
    }
}
