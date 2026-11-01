using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Kartenwerkstatt (Level-Editor), eine eigene Szene, erreichbar über das Hauptmenü. Sie zeigt eine Karte so wie das Spiel – aber ohne
/// Simulation – und bearbeitet sie über den <see cref="Kartenentwurf"/> des Kerns: Gebäude setzen, verschieben, drehen, umbenennen, löschen,
/// jeweils mit Rückgängig und Wiederholen, dazu die Prüfung nach den Kartenregeln (live) und das Speichern im Kartenformat 2.
///
/// Aufgaben verteilt:
///   • diese Datei: Öffnen und Speichern, „Karte ausprobieren“ und die Rückkehr, Tasten, Startoptionen;
///   • Kartenwerkstatt.Tafeln.cs: Kopfleiste, Eigenschaften der Gebäude und Prüfliste rechts, Rückfragen;
///   • Kartenwerkstatt.Palette.cs: Gruppenleiste links (Tasten 1–0), Paletten mit Icons und Vorschaubildern, Kopfleiste, klappbare Abschnitte;
///   • Kartenwerkstatt.Eigenschaften.cs: die Tafel rechts für Wege, Flächen (mit Streu-Gruppen), Pflanzen, Mauern und den Pinsel;
///   • Kartenwerkstatt.Start.cs: die Tafel „Start“ (Startbelegung: Spieler und KI-Häuser);
///   • Kartenwerkstatt.Felsen.cs: die Tafel des Felswerkzeugs (Vorlage, Gestein, Grundgestein, Regler, Filter);
///   • Kartenwerkstatt.Karte.cs: der Reiter „Karte“ (Größe, Randstreifen, Rand vorschlagen, Kamera-Start) und die Tafel der Feldfläche;
///   • Kartenwerkstatt.Ebenen.cs: der Knopf „Ebenen“ (Bauzonen, Natur, Requisiten, Linien, Beschriftungen ein- und ausblenden, Taste H);
///   • Kartenwerkstatt.Katalogbilder.cs: die vorab gerenderten Vorschaubilder der Palette;
///   • Stadtansicht.Kartenwerkstatt*.cs: alles in der 3D-Welt (Gebäude, Wege, Flächen, Natur, Mauern, Belegung, Auswahl, Ziehen).
///
/// Speicherort (Entscheidung vom 22.10.2026, Schritt 4): Die Karten des Spiels in <c>game/karten/</c> (Greybox, Köln, Probestadt …) sind die
/// Quelle. In der Entwicklung (<see cref="Entwicklung"/>: das Spiel läuft mit einem Editor-Build von Godot aus dem Projektordner) speichert die
/// Werkstatt eine solche „Spielkarte“ direkt dorthin zurück – beim ersten Speichern je Karte und Sitzung mit Rückfrage; „Speichern unter“ bietet
/// <c>game/karten/</c> und den Benutzerordner <c>user://karten/</c>. Im exportierten Spiel sind die Spielkarten nur zu lesen, gespeichert wird
/// nur im Benutzerordner (wie die Spielstände). Eine andere Karte zu öffnen (oder eine neue) lädt die Szene neu – so beginnt die Stadtansicht
/// immer leer.
///
/// „Karte ausprobieren“ (nur ohne Fehler): Der Kern stellt auf dem Entwurf, wie er gerade ist, eine neue Partie auf
/// (<see cref="Kartenentwurf.Ausprobieren"/>); die Hauptszene holt sie ab (<see cref="ProbeAbholen"/>), spielt sie ohne Auto-Stand und führt
/// über das Spielmenü zurück (<see cref="ZurueckAusDerProbe"/>). Der Entwurf wartet so lange im „Briefkasten“ samt Rückgängig-Geschichte,
/// Kamera und Auswahl.
/// </summary>
public partial class Kartenwerkstatt : Node
{
    public const string Szene = "res://scenes/Kartenwerkstatt.tscn";
    private const string Menueszene = "res://scenes/Hauptmenue.tscn";

    /// <summary>Hier speichert die Werkstatt (Linux: ~/.local/share/godot/app_userdata/Reichsgilde/karten).</summary>
    public const string Ordner = "user://karten";

    /// <summary>Die Karten des Spiels (<c>game/karten/</c>) – in der Entwicklung schreibt die Werkstatt direkt hierher, sonst nur lesen.</summary>
    private const string Mitgeliefert = "res://karten";

    /// <summary>
    /// Läuft das Spiel in der Entwicklung, also mit einem Editor-Build von Godot aus dem Projektordner (<c>godot --path game</c> oder F5 im
    /// Editor)? Dann ist <c>res://</c> der Ordner <c>game/</c> auf der Platte und beschreibbar. Die Prüfung: Godot setzt das Merkmal „editor“ in
    /// jedem Editor-Build – auch ohne geöffneten Editor –, nie in einer Exportvorlage (dort „template“); dazu muss <c>game/karten/</c> als Ordner
    /// auf der Platte liegen (im Export steckt <c>res://</c> in der .pck-Datei).
    /// </summary>
    public static bool Entwicklung { get; } = OS.HasFeature("editor") && DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(Mitgeliefert));

    /// <summary>Ist der Pfad eine Karte des Spiels (<c>res://karten/…</c>)?</summary>
    private static bool IstSpielkarte(string? pfad) => pfad?.StartsWith(Mitgeliefert + "/") == true;

    /// <summary>Wie ein Pfad in Meldungen erscheint: Spielkarten als <c>game/karten/x.json</c>, sonst der ganze Pfad auf der Platte.</summary>
    private static string Anzeigepfad(string pfad) => IstSpielkarte(pfad) ? "game/" + pfad["res://".Length..] : ProjectSettings.GlobalizePath(pfad);

    /// <summary>Die Spielkarten, deren Überschreiben in dieser Sitzung schon bestätigt ist (statisch: überlebt Öffnen und „Karte ausprobieren“).</summary>
    private static readonly HashSet<string> _ueberschreibenBestaetigt = new();

    /// <summary>
    /// Was die Szene beim nächsten Laden öffnet (Öffnen und Neu laden die Szene neu). Statisch, damit es den Szenenwechsel überlebt –
    /// derselbe „Briefkasten“ wie <see cref="Partiewahl"/>.
    /// </summary>
    private static (Kartenentwurf Entwurf, string? Pfad, bool NurLesen)? _auftrag;

    /// <summary>Die Startoptionen gelten nur für den ersten Start der Szene, nicht nach Öffnen oder Neu.</summary>
    private static bool _startoptionenErledigt;

    /// <summary>
    /// Was die Werkstatt während einer Probepartie aufhebt: Entwurf, Datei, Kamera, Auswahl und die Tafel rechts (Start offen, aufgeklapptes
    /// Haus) – null: keine Probepartie.
    /// </summary>
    private static (Kartenentwurf Entwurf, string? Pfad, bool NurLesen, (Vector3, float, float) Kamera, EntwurfTeil? Auswahl, bool Start, int Haus)? _probe;

    /// <summary>Die aufgestellte Probepartie, bis die Hauptszene sie abholt.</summary>
    private static (Reichsgilde.Core.Simulation Simulation, Konto Spieler)? _probepartie;

    private const string Spielszene = "res://scenes/Hauptszene.tscn";

    private Kartenentwurf _entwurf = null!;

    /// <summary>Die Wirtschaftsdaten (<c>wirtschaft.json</c>) – für Betriebsarten und Waren der Startbelegung und zum Ausprobieren (null: nicht lesbar).</summary>
    private Wirtschaftsdaten? _daten;

    /// <summary>Woher die Karte kommt (null: neu, noch nie gespeichert) und ob die Datei nur gelesen werden darf (Spielkarte im exportierten Spiel).</summary>
    private string? _pfad;
    private bool _nurLesen;

    private Stadtansicht _stadt = null!;

    /// <summary>Die Wurzel aller Tafeln (für <c>--ui=0</c>).</summary>
    private CanvasLayer _oberflaeche = null!;

    /// <summary>
    /// Baut die Werkstatt hinter dem Ladebildschirm auf, ein Abschnitt je Bild (async: Godot ruft <c>_Ready</c> und läuft nach jedem
    /// <c>await</c> weiter). Bis der Ladebildschirm geht, ruhen <c>_Process</c> und Eingabe der Werkstatt und ihrer Stadt.
    /// </summary>
    public override async void _Ready()
    {
        _stadt = GetNode<Stadtansicht>("%Stadt");
        _stadt.OhneHintergrund = true;
        Ruhen(true);
        _stadt.Ruhen(true);
        var optionen = _startoptionenErledigt ? new Dictionary<string, string>() : Startoptionen();
        _startoptionenErledigt = true;
        var lade = Ladebildschirm.Zeigen(GetTree(), optionen.GetValueOrDefault("kartenwerkstatt"));
        lade.Fortschritt(0.03f, "Der Entwurf wird ausgerollt …");
        GetViewport().Disable3D = true; // erst die fertige Werkstatt zeichnen (ErsteBilderAbwarten)
        await Ladebildschirm.Bild(GetTree());
        string? meldung = null;
        var zurueck = _auftrag is null ? _probe : null; // zurück aus der Probepartie (oder über das Hauptmenü wieder hier): weiter wie vorher
        _probe = null;
        if (zurueck is { } r) _auftrag = (r.Entwurf, r.Pfad, r.NurLesen);
        if (_auftrag is not { } auftrag)
            (auftrag, meldung) = Startkarte(optionen.GetValueOrDefault("kartenwerkstatt"));
        _auftrag = null;
        (_entwurf, _pfad, _nurLesen) = auftrag;
        (_daten, var datenFehler) = DatenLaden();
        _entwurf.Daten ??= _daten; // Betriebsarten je Platz und die Prüfung der Startbelegung
        Ladezeit.Marke("Kern: Entwurf und wirtschaft.json lesen");

        await lade.Durchlaufen(_stadt.EntwurfZeigenSchritte(_entwurf), 0.09f, 0.60f);
        lade.Fortschritt(0.60f, "Werkzeug wird bereitgelegt …");
        await Ladebildschirm.Bild(GetTree());
        OberflaecheBauen(); // Kartenwerkstatt.Tafeln.cs
        Ladezeit.Marke("Werkstatt: Tafeln bauen");
        _entwurf.Geaendert += EntwurfGeaendert;
        _stadt.WerkstattAuswahlGeaendert += AuswahlGeaendert;
        _stadt.WerkstattWerkzeugBeendet += KatalogWahlAufheben;
        _stadt.WerkstattWerkzeugGeaendert += () =>
        {
            // ein Werkzeug des Katalogs zeigt seine Tafel unter „Eigenschaften“ (Pinsel); die Wahl eines Platzes gehört zur Tafel „Start“
            if ((_startOffen && _stadt.Kartenwerkzeug is not (Werkstattwerkzeug.Auswahl or Werkstattwerkzeug.Platz)) || (_karteOffen && _stadt.Kartenwerkzeug != Werkstattwerkzeug.Auswahl)) ReiterWaehlen(false);
            EigenschaftenZeigen(neu: true);
            HilfeZeigen();
        };
        _stadt.WerkstattMeldung += (text, fehler) => Melden(text, fehler);
        _stadt.WerkstattGriffGeaendert += () =>
        {
            if (_stadt.WerkstattGewaehlt is EntwurfFluss fluss && ReferenceEquals(_gezeigt, fluss) && !EingabeOffen()) TeilTafel(fluss); // Breite und Tiefe am gewählten Punkt
        };
        _stadt.WerkstattNaturGestreut += () => EigenschaftenZeigen(neu: false);
        _stadt.WerkstattPipetteGeaendert += genommen =>
        {
            // die Bodenart-Pipette wählt die Textur wie ein Klick in der Palette (Knopf, Name in der Leiste); die Höhen-Pipette zeigt nur den
            // Wert; ohne Treffer springt der Knopf „Pipette“ heraus
            var textur = "boden:" + Bodenraster.Kennung(_stadt.BodenArt);
            if (genommen && _stadt.Kartenwerkzeug == Werkstattwerkzeug.Boden && _katalogKnoepfe.ContainsKey(textur)) KatalogWaehlen(textur);
            else EigenschaftenZeigen(neu: true);
        };
        GetTree().AutoAcceptQuit = false; // das Schließen des Fensters fragt nach ungespeicherten Änderungen (_Notification)
        if (zurueck is { } wieder)
        {
            _stadt.WerkstattKamera = wieder.Kamera;
            _stadt.WerkstattWaehlen(wieder.Auswahl);
            _startHaus = wieder.Haus;
            if (wieder.Start) ReiterWaehlen(true);
            meldung = "Zurück aus der Probepartie – die Karte ist, wie sie war, mit Rückgängig.";
        }
        Auffrischen();
        EigenschaftenZeigen(neu: true);
        if (meldung is not null) Melden(meldung);
        else if (datenFehler is not null) Melden(datenFehler, fehler: true);
        StartoptionenAusfuehren(optionen);
        await ErsteBilderAbwarten(lade, 0.66f);
        Ruhen(false);
        _stadt.Ruhen(false);
        Ladezeit.Marke("Ladebild weg");
        lade.Schliessen();
        Ladezeit.BilderMessen(GetTree(), "Kartenwerkstatt"); // --ladezeit=1: die ersten sichtbaren Bilder (Ladezeit.cs)
    }

    /// <summary>Während des Aufbaus: kein <c>_Process</c> (die Tafeln fehlen noch), keine Eingabe.</summary>
    private void Ruhen(bool ruhen)
    {
        SetProcess(!ruhen);
        SetProcessInput(!ruhen);
        SetProcessUnhandledInput(!ruhen);
    }

    /// <summary>
    /// Die ersten Bilder noch hinter dem Ladebildschirm: Werkstatt und Stadt rechnen (Natur, Wegeschicht, Gras), der Grafiktreiber übersetzt die
    /// Shader; Eingaben ruhen noch. Fertig, wenn drei Bilder in Folge kurz sind (höchstens 5 s).
    /// </summary>
    private async Task ErsteBilderAbwarten(Ladebildschirm lade, float von)
    {
        GetViewport().Disable3D = false;
        SetProcess(true);
        _stadt.SetProcess(true); // rechnen ja, Eingabe erst ohne Ladebildschirm
        var anfang = Time.GetTicksMsec();
        var vorher = anfang;
        var ruhig = 0;
        for (var bild = 0; bild < 300; bild++)
        {
            lade.Fortschritt(von + (1 - von) * Math.Min(1f, bild / 6f), "Letzte Handgriffe …");
            await Ladebildschirm.Bild(GetTree());
            var jetzt = Time.GetTicksMsec();
            ruhig = bild >= 1 && jetzt - vorher < 50 ? ruhig + 1 : 0;
            vorher = jetzt;
            if (ruhig >= 3 || jetzt - anfang > 5000) break;
        }
    }

    /// <summary>Die Zeile in der Leiste unten: Höhe und Gefälle des Bodens unter der Maus.</summary>
    private Label _bodenInfo = null!;

    public override void _Process(double delta)
    {
        var uhr = Ladezeit.Uhr();
        _stadt.WerkstattAktualisieren();
        Ladezeit.Dazu("im Bild: WerkstattAktualisieren", uhr);
        PinselInfoZeigen(); // Kartenwerkstatt.Tafeln.cs: das Schild unter dem Pinsel
        // Höhe, Gefälle und Boden unter der Maus – nicht doppelt, solange das Schild sie zeigt (dort die Steigung, wie der Filter sie misst)
        var boden = _pinselInfo.Visible ? null : _stadt.WerkstattBodenInfo();
        var info = $"Werkzeug: {_werkzeugName}" + (boden is null ? "" : " · " + boden);
        if (_bodenInfo.Text != info) _bodenInfo.Text = info;
    }

    /// <summary>Die Szene geht (Hauptmenü, Öffnen, Ausprobieren): Der Entwurf kann sie überleben – also alle Ereignisse an ihn lösen.</summary>
    public override void _ExitTree()
    {
        GetTree().AutoAcceptQuit = true;
        _entwurf.Geaendert -= EntwurfGeaendert;
        _stadt.WerkstattLoesen();
    }

    private void EntwurfGeaendert(Kartenaenderung a)
    {
        AuffrischenVormerken();
        if (ReferenceEquals(a.Objekt, _stadt.WerkstattGewaehlt)) EigenschaftenZeigen(neu: false);
        if (a.Objekt is EntwurfStart) StartVormerken(); // Kartenwerkstatt.Start.cs
        if (a.Objekt is EntwurfKopf) KarteWerteSetzen(); // Kartenwerkstatt.Karte.cs
    }

    /// <summary>Liest <c>wirtschaft.json</c> (wie die Hauptszene) – oder nennt den Grund, warum es nicht geht.</summary>
    private static (Wirtschaftsdaten? Daten, string? Fehler) DatenLaden()
    {
        var (daten, fehler, leer) = Datenvorrat.Holen(); // meist schon im Hintergrund gelesen (Datenvorrat.cs)
        return leer ? (null, $"{Hauptszene.WirtschaftsdatenPfad} fehlt – ohne sie kennt die Startbelegung keine Betriebsarten.")
            : (daten, fehler?.Message);
    }

    /// <summary>Das Fenster soll schließen (X): bei ungespeicherten Änderungen erst fragen.</summary>
    public override void _Notification(int was)
    {
        if (was == NotificationWMCloseRequest) MitRueckfrage(() => GetTree().Quit());
    }

    private bool _auffrischenVorgemerkt;

    /// <summary>
    /// Kopf und Fehlerliste einmal am Ende des Bildes auffrischen: Ein Befehl kann viele Teile ändern (der Pinsel setzt Dutzende Pflanzen, jede
    /// meldet sich einzeln), und mehrere Befehle in einem Bild (Startoptionen) bauen die Liste so nur einmal.
    /// </summary>
    private void AuffrischenVormerken()
    {
        if (_auffrischenVorgemerkt) return;
        _auffrischenVorgemerkt = true;
        Callable.From(() =>
        {
            _auffrischenVorgemerkt = false;
            Auffrischen();
        }).CallDeferred();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Öffnen, Neu, Speichern
    // ═════════════════════════════════════════════════════════════════════════

    private static Gebaeudekatalog KatalogLaden() => Gebaeudekatalog.AusJson(Godot.FileAccess.GetFileAsString(Stadtkarten.KatalogPfad));

    /// <summary>
    /// Die Karte beim ersten Start: aus der Startoption (eine Spielkarte wie <c>greybox</c>, <c>koeln</c>, <c>probestadt</c>, dazu <c>neu</c> oder
    /// eine Datei <c>x.json</c> aus <c>user://karten/</c>), sonst die Greybox. Lässt sie sich nicht öffnen, eine leere Karte mit der Meldung warum.
    /// </summary>
    private static ((Kartenentwurf, string?, bool) Auftrag, string? Meldung) Startkarte(string? wahl)
    {
        wahl ??= Stadtkarten.Vorgabe;
        if (wahl == "neu") return ((NeueKarte("neustadt", "Neustadt", Kartengrenzen.NeueBreite, Kartengrenzen.NeueTiefe), null, false), null);
        var (pfad, nurLesen) = wahl.EndsWith(".json") ? ($"{Ordner}/{wahl}", false) : ($"{Mitgeliefert}/{wahl}.json", !Entwicklung);
        var (entwurf, fehler) = Lesen(pfad);
        if (entwurf is not null) return ((entwurf, pfad, nurLesen), null);
        return ((NeueKarte("neustadt", "Neustadt", Kartengrenzen.NeueBreite, Kartengrenzen.NeueTiefe), null, false), fehler);
    }

    /// <summary>
    /// Eine neue, leere Karte mit Randlandschaft: Der Kern schlägt Felder und Waldstücke für den Randstreifen vor (<see cref="Kartenentwurf.RandVorschlagen"/>);
    /// der Vorschlag gilt als Ausgangsstand (nicht als Änderung), lässt sich aber mit Rückgängig wegnehmen.
    /// </summary>
    private static Kartenentwurf NeueKarte(string kennung, string stadt, double breite, double tiefe)
    {
        var entwurf = Kartenentwurf.Neu(kennung, stadt, breite, tiefe, KatalogLaden());
        entwurf.RandVorschlagen();
        entwurf.SpielbarSetzen(true); // eine selbst gebaute Karte erscheint in der Kartenwahl, bis man den Haken im Reiter „Karte“ wegnimmt
        entwurf.Geschichte.Gespeichert();
        return entwurf;
    }

    /// <summary>Liest eine Kartendatei als Entwurf – oder nennt den Grund, warum es nicht geht.</summary>
    private static (Kartenentwurf? Entwurf, string? Fehler) Lesen(string pfad)
    {
        if (!Godot.FileAccess.FileExists(pfad)) return (null, $"Die Datei {pfad} gibt es nicht.");
        try
        {
            return (Kartenentwurf.AusJson(Godot.FileAccess.GetFileAsString(pfad), KatalogLaden()), null);
        }
        catch (KartenFehler fehler)
        {
            return (null, $"{pfad} lässt sich nicht öffnen: {string.Join(" ", fehler.Fehler)}");
        }
    }

    /// <summary>Öffnet eine andere Karte: Die Szene lädt neu und holt sie aus dem Briefkasten.</summary>
    private void Wechseln(Kartenentwurf entwurf, string? pfad, bool nurLesen)
    {
        _auftrag = (entwurf, pfad, nurLesen);
        GetTree().ReloadCurrentScene();
    }

    /// <summary>
    /// Speichern (Strg+S): in die eigene Datei; eine Spielkarte in der Entwicklung direkt nach <c>game/karten/</c> (beim ersten Mal je Sitzung mit
    /// Rückfrage), eine neue oder nur lesbare Karte fragt nach dem Namen.
    /// </summary>
    private void Speichern()
    {
        if (_pfad is null || _nurLesen) SpeichernUnterZeigen();
        else if (IstSpielkarte(_pfad) && !_ueberschreibenBestaetigt.Contains(_pfad)) SpielkarteUeberschreiben(_pfad, () => { });
        else SpeichernNach(_pfad);
    }

    /// <summary>Die Rückfrage vor dem ersten Speichern einer Spielkarte in dieser Sitzung; „Überschreiben“ speichert und merkt die Zusage.</summary>
    private void SpielkarteUeberschreiben(string pfad, Action danach)
    {
        Rueckfrage("Spielkarte überschreiben?",
            $"„{_entwurf.Stadt}“ wird direkt in {Anzeigepfad(pfad)} gespeichert – eine Karte des Spiels; Git zeigt die Änderung. "
            + "In dieser Sitzung fragt die Werkstatt für diese Karte nicht noch einmal.",
            "Überschreiben", () =>
            {
                _ueberschreibenBestaetigt.Add(pfad);
                if (SpeichernNach(pfad)) danach();
            });
    }

    /// <summary>Schreibt die Karte nach <paramref name="pfad"/> (<c>user://karten/</c> oder eine Spielkarte) und merkt den Stand als gespeichert.</summary>
    private bool SpeichernNach(string pfad)
    {
        DirAccess.MakeDirRecursiveAbsolute(pfad.GetBaseDir());
        using var datei = Godot.FileAccess.Open(pfad, Godot.FileAccess.ModeFlags.Write);
        if (datei is null)
        {
            Melden($"Speichern fehlgeschlagen ({Anzeigepfad(pfad)}): {Godot.FileAccess.GetOpenError()}", fehler: true);
            return false;
        }
        datei.StoreString(_entwurf.ZuJson());
        _entwurf.Geschichte.Gespeichert();
        (_pfad, _nurLesen) = (pfad, false);
        Melden(IstSpielkarte(pfad) ? $"Gespeichert: {Anzeigepfad(pfad)} (Spielkarte – „git diff“ zeigt, was sich geändert hat)" : $"Gespeichert: {Anzeigepfad(pfad)}");
        Auffrischen();
        return true;
    }

    /// <summary>Ein Dateiname aus der Eingabe: nur Buchstaben, Ziffern, _ und -, mit „.json“ (null, wenn nichts übrig bleibt).</summary>
    private static string? Dateiname(string eingabe)
    {
        var name = eingabe.Trim();
        if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) name = name[..^5];
        name = new string(name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray()).Trim('_');
        return name.Length == 0 ? null : name + ".json";
    }

    /// <summary>Die Karten eines Ordners (Benutzerordner oder Spielkarten): Pfad und Anzeige („Köln – koeln.json“).</summary>
    private static List<(string Pfad, string Text)> Karten(string ordner)
    {
        var liste = new List<(string, string)>();
        if (!DirAccess.DirExistsAbsolute(ordner)) return liste;
        foreach (var datei in DirAccess.GetFilesAt(ordner).Where(d => d.EndsWith(".json")).Order())
        {
            var pfad = $"{ordner}/{datei}";
            var stadt = "";
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(Godot.FileAccess.GetFileAsString(pfad));
                if (doc.RootElement.TryGetProperty("stadt", out var s)) stadt = s.GetString() ?? "";
            }
            catch (System.Text.Json.JsonException) { stadt = "(nicht lesbar)"; }
            liste.Add((pfad, stadt.Length > 0 ? $"{stadt} – {datei}" : datei));
        }
        return liste;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Karte ausprobieren
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Stellt auf dem Entwurf eine neue Partie mit diesem Beruf auf (<see cref="Kartenentwurf.Ausprobieren"/>) und wechselt in die Hauptszene; der
    /// Entwurf wartet samt Rückgängig, Kamera und Auswahl auf die Rückkehr. Geht es nicht (Fehler in der Karte), steht der Grund unten.
    /// </summary>
    private void Ausprobieren(string beruf)
    {
        if (_daten is null)
        {
            Melden("Ohne Wirtschaftsdaten lässt sich keine Partie aufstellen.", fehler: true);
            return;
        }
        try
        {
            _probepartie = _entwurf.Ausprobieren(_daten, beruf);
        }
        catch (Exception ex) when (ex is InvalidOperationException or KartenFehler or ArgumentException)
        {
            Melden($"Ausprobieren geht nicht: {ex.Message}", fehler: true);
            return;
        }
        _probe = (_entwurf, _pfad, _nurLesen, _stadt.WerkstattKamera, _stadt.WerkstattGewaehlt, _startOffen, _startHaus);
        GetTree().ChangeSceneToFile(Spielszene);
    }

    /// <summary>Für die Hauptszene: die aufgestellte Probepartie (einmal; null, wenn keine wartet).</summary>
    public static (Reichsgilde.Core.Simulation Simulation, Konto Spieler)? ProbeAbholen()
    {
        var partie = _probepartie;
        _probepartie = null;
        return partie;
    }

    /// <summary>Für die Hauptszene (Spielmenü einer Probepartie): zurück in die Kartenwerkstatt zum Entwurf, wie er war.</summary>
    public static void ZurueckAusDerProbe(SceneTree baum) => baum.ChangeSceneToFile(Szene);

    // ═════════════════════════════════════════════════════════════════════════
    //  Tasten
    // ═════════════════════════════════════════════════════════════════════════

    public override void _Input(InputEvent eingabe)
    {
        // Ein Klick neben ein Textfeld (in die Stadt, auf eine andere Tafel) beendet die Eingabe – der Name wird dann übernommen
        if (eingabe is InputEventMouseButton { Pressed: true } && GetViewport().GuiGetFocusOwner() is LineEdit feld
            && !feld.GetGlobalRect().HasPoint(feld.GetGlobalMousePosition()))
            feld.ReleaseFocus();
    }

    /// <summary>Was die Stadt nicht braucht: Strg+Z/Y/S, Esc schließt eine Rückfrage.</summary>
    public override void _UnhandledInput(InputEvent eingabe)
    {
        if (eingabe is not InputEventKey { Pressed: true } taste) return;
        if (DialogOffen)
        {
            if (taste.Keycode == Key.Escape) DialogSchliessen();
            GetViewport().SetInputAsHandled();
            return;
        }
        switch (taste)
        {
            case { CtrlPressed: true, Keycode: Key.Z, ShiftPressed: false }:
                _entwurf.Rueckgaengig();
                break;
            case { CtrlPressed: true, Keycode: Key.Y } or { CtrlPressed: true, Keycode: Key.Z, ShiftPressed: true }:
                _entwurf.Wiederholen();
                break;
            case { CtrlPressed: true, Keycode: Key.S, Echo: false }:
                Speichern();
                break;
            case { CtrlPressed: false, AltPressed: false, ShiftPressed: false, MetaPressed: false, Echo: false } when GruppeFuerTaste(taste.Keycode) is { } gruppe:
                GruppeWaehlen(gruppe.Kennung); // 1–0: die Gruppen der Leiste links
                break;
            case { CtrlPressed: false, AltPressed: false, ShiftPressed: false, MetaPressed: false, Echo: false, Keycode: Key.H } when GetViewport().GuiGetFocusOwner() is not LineEdit:
                EbenenTaste(); // H: alle Ebenen aus oder an (Kartenwerkstatt.Ebenen.cs)
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Startoptionen (für Klicktests)
    // ═════════════════════════════════════════════════════════════════════════

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

    /// <summary>Eine Zahl der Startoptionen (Punkt als Dezimaltrenner; das Minuszeichen „−“ gilt wie „-“).</summary>
    private static double Zahl(string text) => double.Parse(text.Replace('−', '-'), System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Liest eine Höhenkarte aus der Startoption <c>--werkstatt-hoehenkarte</c> ein und meldet den Bericht (oder den Fehler) unten.</summary>
    private void HoehenkarteAusStartoption(string eintrag)
    {
        var teile = eintrag.Split(',');
        var angaben = teile.Skip(1).Select(t => t.Split('=', 2)).Where(kv => kv.Length == 2).ToDictionary(kv => kv[0], kv => kv[1]);
        int Cm(string name, double vorgabe = double.NaN)
        {
            var meter = angaben.TryGetValue(name, out var text) ? Zahl(text) : !double.IsNaN(vorgabe) ? vorgabe : throw new ArgumentException($"--werkstatt-hoehenkarte: „{name}=“ fehlt.");
            return (int)Math.Round(meter * 100, MidpointRounding.AwayFromZero);
        }
        var bytes = Godot.FileAccess.GetFileAsBytes(teile[0]);
        if (bytes.Length == 0)
        {
            Melden($"Höhenkarte {teile[0]} nicht lesbar ({Godot.FileAccess.GetOpenError()}).", fehler: true);
            return;
        }
        var ursprung = (angaben.GetValueOrDefault("ursprung") ?? throw new ArgumentException("--werkstatt-hoehenkarte: „ursprung=x:z“ fehlt.")).Split(':');
        var (werte, breite, tiefe) = Hoehenkarte.Lesen(bytes, angaben.TryGetValue("breite", out var b) ? int.Parse(b) : 0, angaben.GetValueOrDefault("bytefolge") == "be");
        var lage = new Hoehenkartenlage(breite, tiefe, Cm("pixel"), (int)Math.Round(Zahl(ursprung[0]) * 100, MidpointRounding.AwayFromZero),
            (int)Math.Round(Zahl(ursprung[1]) * 100, MidpointRounding.AwayFromZero), Cm("unten"), Cm("oben"), Cm("versatz", 0),
            angaben.TryGetValue("stufe", out var stufe) ? int.Parse(stufe) : 5);
        var bericht = _entwurf.HoehenkarteEinlesen(werte, lage);
        Melden($"Höhenkarte eingelesen: {breite} × {tiefe} Pixel, {bericht.Abgedeckt} Rasterpunkte abgedeckt, {bericht.Geaendert} geändert"
            + (bericht.Warnung is { } warnung ? " – " + warnung : "")
            + " · nur zum Ausprobieren: ohne Herkunft – für eine Spielkarte über das Kartenskript einlesen (hoehenkarte-einlesen, Quelle, Lizenz, Abruf)",
            fehler: bericht.Abgeschnitten);
    }

    /// <summary>
    /// Die Angaben des Pinsel-Pakets 1 in den Startoptionen („ziel=3.5“, „stufe=2“, „boeschung=0.3“, „ausgleich=1“, „steigung=15-40“ oder „steigung=-8“,
    /// „bodenfilter=sand:0.5“); andere Angaben (kante, deko, dichte) übergeht sie.
    /// </summary>
    private static (double? Ziel, double? Stufe, double? Boeschung, bool Ausgleich, Pinselfilter? Filter) PinselAngaben(IEnumerable<string> angaben)
    {
        (double? ziel, double? stufe, double? boeschung, bool ausgleich) = (null, null, null, false);
        var filter = Pinselfilter.Keiner;
        foreach (var angabe in angaben)
        {
            var kv = angabe.Split('=', 2);
            if (kv.Length < 2) continue;
            switch (kv[0])
            {
                case "ziel": ziel = Zahl(kv[1]); break;
                case "stufe": stufe = Zahl(kv[1]); break;
                case "boeschung": boeschung = Zahl(kv[1]); break;
                case "ausgleich": ausgleich = kv[1] is "1" or "an"; break;
                case "steigung":
                    var grenzen = kv[1].Split('-', 2);
                    filter = filter with
                    {
                        SteigungAb = grenzen[0].Length > 0 ? Zahl(grenzen[0]) : 0,
                        SteigungBis = grenzen.Length > 1 && grenzen[1].Length > 0 ? Zahl(grenzen[1]) : null,
                    };
                    break;
                case "bodenfilter":
                    var b = kv[1].Split(':');
                    filter = filter with
                    {
                        Boden = Bodenraster.TexturVon(b[0]) ?? throw new ArgumentException($"Unbekannte Bodentextur „{b[0]}“ im Filter."),
                        BodenRand = b.Length > 1 ? Zahl(b[1]) : 0,
                    };
                    break;
            }
        }
        return (ziel, stufe, boeschung, ausgleich, filter.IstLeer ? null : filter);
    }

    /// <summary>„art,x1,z1;x2,z2;…[,wort]“ → Art, Punkte und das Wort am Ende (oder null).</summary>
    private static (string Art, List<Kartenort> Punkte, string? Wort) Liste(string eintrag)
    {
        var t = eintrag.Replace(';', ',').Split(',', StringSplitOptions.RemoveEmptyEntries);
        var rest = t.Length > 1 && !double.TryParse(t[^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) ? t[^1] : null;
        var zahlen = t.Skip(1).Take(t.Length - 1 - (rest is null ? 0 : 1)).Select(Zahl).ToList();
        var punkte = Enumerable.Range(0, zahlen.Count / 2).Select(i => new Kartenort(zahlen[2 * i], zahlen[2 * i + 1])).ToList();
        return (t[0], punkte, rest);
    }

    private EntwurfTeil AussparungUndFlaeche(EntwurfFlaeche f, List<Kartenort> punkte)
    {
        _entwurf.AussparungAnlegen(f, punkte);
        return f;
    }

    /// <summary>
    /// Ein Teil aus der Startoption <c>--werkstatt-auswahl</c>: „weg:&lt;Name&gt;“, „zone:&lt;Kennung&gt;“, „natur:&lt;Name&gt;“, „feld:&lt;Name&gt;“, „mauer:&lt;Name&gt;“,
    /// sonst die Kennung eines Gebäudes.
    /// </summary>
    private EntwurfTeil? TeilVon(string text)
    {
        var (art, name) = text.Contains(':') ? (text[..text.IndexOf(':')], text[(text.IndexOf(':') + 1)..].Replace('_', ' ')) : ("", text);
        return art switch
        {
            "weg" => _entwurf.Wege.FirstOrDefault(w => w.Name == name),
            "zone" => _entwurf.Bauzonen.FirstOrDefault(z => z.Kennung == name),
            "natur" => _entwurf.Naturflaechen.FirstOrDefault(n => n.Name == name),
            "feld" => _entwurf.Felder.FirstOrDefault(n => n.Name == name),
            "mauer" => _entwurf.Mauern.FirstOrDefault(m => m.Name == name),
            "fluss" => _entwurf.Fluesse.FirstOrDefault(f => f.Bezeichnung == name),
            "see" => _entwurf.Seen.FirstOrDefault(s => s.Bezeichnung == name),
            "faehre" => _entwurf.Faehren.FirstOrDefault(f => f.Bezeichnung == name),
            _ => _entwurf.GebaeudeMit(text),
        };
    }

    /// <summary>
    /// Die Schritte der Startoptionen in fester Reihenfolge: groesse, randvorschlag, setzen, mauer, tor, fluss, see, weg, faehre, belag, feld, flaeche, pinsel, radierer, start,
    /// versetzen, rückgängig, auswahl, speichern, umriss, werkzeug, netz, dann Kamera (blick, drehung, kamerastart), Tafeln und zuletzt ausprobieren (Liste in <c>CLAUDE.md</c>, „Bauen, testen,
    /// ausprobieren“). Mehrere Wege, Flächen, Mauern, Tore, Striche und Startbefehle trennt „|“, Punkte „;“.
    /// </summary>
    private void StartoptionenAusfuehren(Dictionary<string, string> o)
    {
        try
        {
            // --werkstatt-groesse=<breite>,<tiefe>[,<rand>] (400 × 300 bis 2.000 × 2.000, Rand 100 bis 600 m), --werkstatt-randvorschlag=1 (oder eine Zahl als Startwert)
            if (o.TryGetValue("werkstatt-groesse", out var groesse))
            {
                var t = groesse.Split(',');
                _entwurf.GroesseSetzen(Zahl(t[0]), Zahl(t[1]));
                if (t.Length > 2) _entwurf.RandbreiteSetzen(Zahl(t[2]));
            }
            if (o.TryGetValue("werkstatt-randvorschlag", out var vorschlag))
                _entwurf.RandVorschlagen(int.TryParse(vorschlag, out var startwert) && startwert > 1 ? startwert : null);
            if (o.TryGetValue("werkstatt-setzen", out var setzen))
                foreach (var eintrag in setzen.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var t = eintrag.Split(',');
                    var g = _entwurf.Setzen(t[0], Zahl(t[1]), Zahl(t[2]), t.Length > 3 ? Zahl(t[3]) : 0);
                    _stadt.WerkstattWaehlen(g.Kennung);
                }
            // --werkstatt-mauer=x1,z1;x2,z2;…[,offen] (ohne „offen“ ab drei Ecken geschlossen)
            if (o.TryGetValue("werkstatt-mauer", out var mauern))
                foreach (var eintrag in mauern.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var (_, punkte, wort) = Liste("-," + eintrag);
                    _stadt.WerkstattWaehlen(_entwurf.MauerAnlegen(punkte, geschlossen: wort != "offen" && punkte.Count >= 3));
                }
            // --werkstatt-tor=x,z|x,z (auf die nächste Mauer bis 10 m)
            if (o.TryGetValue("werkstatt-tor", out var tore))
                foreach (var eintrag in tore.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var t = eintrag.Split(',');
                    var (x, z) = (Zahl(t[0]), Zahl(t[1]));
                    var fang = _entwurf.MauerBei(x, z, 10) ?? throw new ArgumentException($"Bei {eintrag} liegt keine Mauer (bis 10 m).");
                    _entwurf.TorSetzen(fang.Mauer, x, z);
                    _stadt.WerkstattWaehlen(fang.Mauer);
                }
            // --werkstatt-fluss=<fluss|bach>,<breite>,x1,z1;x2,z2;…[|…] (Punkte in Fließrichtung; Breite 0 = nach der Art); --werkstatt-see=[teich:]x1,z1;x2,z2;…[,<wasserstand>][|…]
            if (o.TryGetValue("werkstatt-fluss", out var fluesse))
                foreach (var eintrag in fluesse.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var t = eintrag.Split(',', 3);
                    if (_stadt.WerkstattFlussAnlegen(t[0], Zahl(t[1]), Liste("-," + t[2]).Punkte) is { } fluss) _stadt.WerkstattWaehlen(fluss);
                }
            if (o.TryGetValue("werkstatt-see", out var seen))
                foreach (var eintrag in seen.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var art = eintrag.StartsWith("teich:") ? "teich" : "see";
                    var zahlen = (art == "teich" ? eintrag["teich:".Length..] : eintrag).Replace(';', ',').Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Zahl).ToList();
                    double? wasserstand = zahlen.Count % 2 == 1 ? zahlen[^1] : null; // eine Zahl mehr als Paare: der Wasserstand
                    if (wasserstand is not null) zahlen.RemoveAt(zahlen.Count - 1);
                    var punkte = Enumerable.Range(0, zahlen.Count / 2).Select(i => new Kartenort(zahlen[2 * i], zahlen[2 * i + 1])).ToList();
                    if (_stadt.WerkstattSeeAnlegen(art, punkte, wasserstand) is { } see) _stadt.WerkstattWaehlen(see);
                }
            // --werkstatt-weg=gasse,x1,z1;x2,z2;…[,gerade]
            if (o.TryGetValue("werkstatt-weg", out var wege))
                foreach (var eintrag in wege.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var (art, punkte, wort) = Liste(eintrag);
                    var klasse = Enum.Parse<Wegklasse>(art, ignoreCase: true);
                    if (_stadt.WerkstattWegAnlegen(klasse, punkte, wort != "gerade") is { } w) _stadt.WerkstattWaehlen(w);
                }
            // --werkstatt-faehre=x1,z1;x2,z2[|…]: eine Fährstelle (die Anleger rasten an Wegen ein; nach den Wegen)
            if (o.TryGetValue("werkstatt-faehre", out var faehren))
                foreach (var eintrag in faehren.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var punkte = Liste("-," + eintrag).Punkte;
                    if (punkte.Count >= 2 && _stadt.WerkstattFaehreSetzen(punkte[0], punkte[1]) is { } faehre) _stadt.WerkstattWaehlen(faehre);
                }
            // --werkstatt-belag=<weg>,<stufe>[,<von>,<bis>][|…]: Startbelag (Schritt 5d). <weg> ist die Nummer (ab 0, in der Reihenfolge der Wege) oder der Name (Leerzeichen als _),
            // <stufe> trampelpfad, fahrweg, kies, pflaster oder vorgabe (Belag nach der Art); mit von und bis (Meter) nur dieser Abschnitt, sonst der ganze Weg; ein Schritt je Eintrag
            if (o.TryGetValue("werkstatt-belag", out var belaege))
                foreach (var eintrag in belaege.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var t = eintrag.Split(',');
                    var weg = int.TryParse(t[0], out var nr) && nr >= 0 && nr < _entwurf.Wege.Count ? _entwurf.Wege[nr]
                        : _entwurf.Wege.FirstOrDefault(w => w.Name == t[0].Replace('_', ' ')) ?? throw new ArgumentException($"Den Weg „{t[0]}“ gibt es nicht.");
                    if (t[1].Equals("vorgabe", StringComparison.OrdinalIgnoreCase)) _entwurf.BelagSetzen(weg, (Belagstufe?)null);
                    else if (!Enum.TryParse<Belagstufe>(t[1], ignoreCase: true, out var stufe)) throw new ArgumentException($"Unbekannter Belag „{t[1]}“ (trampelpfad, fahrweg, kies, pflaster, vorgabe).");
                    else if (t.Length >= 4) _entwurf.BelagSetzen(weg, Zahl(t[2]), Zahl(t[3]), stufe);
                    else _entwurf.BelagSetzen(weg, stufe);
                    _stadt.WerkstattWaehlen(weg);
                }
            // --werkstatt-feld=x1,z1;x2,z2;…[|…] (Feldflächen, die Streifen laufen längs der längsten Kante)
            if (o.TryGetValue("werkstatt-feld", out var felder))
                foreach (var eintrag in felder.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var (_, punkte, _) = Liste("-," + eintrag);
                    _stadt.WerkstattWaehlen(_entwurf.FeldAnlegen(punkte, null, Stadtansicht.FeldRichtungVon(punkte)));
                }
            // --werkstatt-flaeche=wald|streu|feld|bauzone:<lage>|aussparung,x1,z1;x2,z2;…
            if (o.TryGetValue("werkstatt-flaeche", out var flaechen))
                foreach (var eintrag in flaechen.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var (art, punkte, _) = Liste(eintrag);
                    EntwurfTeil? neu = art switch
                    {
                        "wald" or "streu" => _entwurf.NaturflaecheAnlegen(art, punkte),
                        "feld" => _entwurf.FeldAnlegen(punkte, null, Stadtansicht.FeldRichtungVon(punkte)),
                        "aussparung" when _stadt.WerkstattGewaehlt is EntwurfFlaeche { KannAussparungen: true } f => AussparungUndFlaeche(f, punkte),
                        _ when art.StartsWith("bauzone") => _entwurf.BauzoneAnlegen(art.Contains(':') ? Enum.Parse<Lage>(art.Split(':')[1], true) : Lage.Vorstadt, punkte),
                        _ => throw new ArgumentException($"Unbekannte Fläche „{art}“ (wald, streu, feld, bauzone:<lage>, aussparung nach einer Wald-, Streu- oder Feldfläche)."),
                    };
                    _stadt.WerkstattWaehlen(neu);
                }
            // --werkstatt-pinsel=eiche+hasel,radius,dichte,x1,z1;x2,z2;…
            if (o.TryGetValue("werkstatt-pinsel", out var pinsel))
                foreach (var eintrag in pinsel.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var t = eintrag.Split(',', 4);
                    _stadt.PinselArten.Clear();
                    foreach (var art in t[0].Split('+')) _stadt.PinselArten[art] = 1;
                    (_stadt.PinselRadius, _stadt.PinselDichte) = (Zahl(t[1]), Zahl(t[2]));
                    _stadt.WerkstattStreuen(Liste("-," + t[3]).Punkte);
                }
            // --werkstatt-radierer=radius,x1,z1;x2,z2;…
            if (o.TryGetValue("werkstatt-radierer", out var radierer))
            {
                var t = radierer.Split(',', 2);
                _stadt.PinselRadius = Zahl(t[0]);
                _stadt.WerkstattRadieren(Liste("-," + t[1]).Punkte);
            }
            // --werkstatt-hoehenkarte=<pfad>,pixel=<m>,ursprung=<x>:<z>,unten=<m>,oben=<m>[,breite=<px>][,versatz=<m>][,stufe=<cm>][,bytefolge=be]:
            // eine Höhenkarte (r16/raw) einlesen – ein Schritt mit Rückgängig, der Bericht steht unten (Kartenskript: hoehenkarte-einlesen)
            if (o.TryGetValue("werkstatt-hoehenkarte", out var hoehenkarte)) HoehenkarteAusStartoption(hoehenkarte);
            // --werkstatt-filter=<gelaende|boden>:<angaben>: der Filter in der Tafel (wie unten steigung=…,bodenfilter=…; leer: aus)
            if (o.TryGetValue("werkstatt-filter", out var filterOption))
                foreach (var eintrag in filterOption.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var (wem, angaben) = (eintrag.Split(':', 2)[0], eintrag.Contains(':') ? eintrag.Split(':', 2)[1] : "");
                    var filter = PinselAngaben(angaben.Split(',', StringSplitOptions.RemoveEmptyEntries)).Filter ?? Pinselfilter.Keiner;
                    if (wem == "boden") _stadt.BodenFilter = filter;
                    else _stadt.GelaendeFilter = filter;
                }
            // --werkstatt-gelaende=<pinsel>,<radius>,<staerke>,x1,z1;x2,z2;…[,<angabe>=<wert>…][|…]: ein Pinselstrich (ein Schritt); <pinsel> ist heben, senken,
            // glaetten, ebnen, rauschen oder terrassen, hinten mit :kante[:startwert[:massstab]] (Kante 0 … 1); Angaben (Pinsel-Paket 1): ziel=<m>, stufe=<m>,
            // boeschung=<0–1>, ausgleich=1 (nur Mulden / nur Spitzen), steigung=<ab>-<bis> (Prozent, eine Seite darf fehlen), bodenfilter=<textur>[:<rand 0–1>]
            if (o.TryGetValue("werkstatt-gelaende", out var gelaende))
                foreach (var eintrag in gelaende.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var t = eintrag.Split(',', 4);
                    var p = t[0].Split(':');
                    if (!Enum.TryParse<Pinselart>(p[0], ignoreCase: true, out var art)) throw new ArgumentException($"Unbekannter Geländepinsel „{p[0]}“ (heben, senken, glaetten, ebnen, rauschen, terrassen).");
                    var tail = System.Text.RegularExpressions.Regex.Match(t[3], ",[a-z]+=");
                    var angaben = PinselAngaben(tail.Success ? t[3][(tail.Index + 1)..].Split(',') : Array.Empty<string>());
                    var gelaendepinsel = new Pinsel(art, Zahl(t[1]), Zahl(t[2]), p.Length > 1 ? Zahl(p[1]) : 0.5, p.Length > 2 ? (int)Zahl(p[2]) : 1, p.Length > 3 ? Zahl(p[3]) : 20)
                    {
                        Zielhoehe = angaben.Ziel, Stufenhoehe = angaben.Stufe ?? 2, Boeschung = angaben.Boeschung ?? 0.3, NurAusgleich = angaben.Ausgleich, Filter = angaben.Filter,
                    };
                    _entwurf.GelaendePinseln(gelaendepinsel, Liste("-," + (tail.Success ? t[3][..tail.Index] : t[3])).Punkte.Select(q => new Kartenort(q.X, q.Z)).ToList());
                }
            // --werkstatt-boden=<textur|radierer>,<radius>,<staerke>,x1,z1;x2,z2;…[,kante=<0–1>][,deko=aus][,dichte=<0–2>][,steigung=…][,bodenfilter=…][|…]: ein
            // Texturstrich (ein Schritt); <textur> ist eine Kennung wie waldboden oder sand; steigung und bodenfilter wie bei --werkstatt-gelaende
            if (o.TryGetValue("werkstatt-boden", out var boeden))
                foreach (var eintrag in boeden.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var t = eintrag.Split(',', 4);
                    var tail = System.Text.RegularExpressions.Regex.Match(t[3], ",(kante|deko|dichte|steigung|bodenfilter)=");
                    var punkte = Liste("-," + (tail.Success ? t[3][..tail.Index] : t[3])).Punkte.Select(q => new Kartenort(q.X, q.Z)).ToList();
                    var (kante, deko, dichte) = (0.5, true, 1.0);
                    if (tail.Success)
                        foreach (var opt in t[3][(tail.Index + 1)..].Split(','))
                        {
                            var kv = opt.Split('=');
                            if (kv[0] == "kante") kante = Zahl(kv[1]);
                            else if (kv[0] == "deko") deko = kv[1] != "aus";
                            else if (kv[0] == "dichte") dichte = Zahl(kv[1]);
                        }
                    var bodenfilter = tail.Success ? PinselAngaben(t[3][(tail.Index + 1)..].Split(',')).Filter : null;
                    var bodenpinsel = t[0] == "radierer"
                        ? Bodenpinsel.Radierer(Zahl(t[1]), kante, bodenfilter)
                        : new Bodenpinsel(Bodenraster.TexturVon(t[0]) ?? throw new ArgumentException($"Unbekannte Bodentextur „{t[0]}“."), Zahl(t[1]), Zahl(t[2]), kante, deko, dichte, bodenfilter);
                    _stadt.WerkstattBodenMalen(bodenpinsel, punkte);
                }
            // --werkstatt-fels=<schritt>[|…] (Felspinsel F4), je Schritt ein Befehl: grundgestein:<gestein> · stempel:<vorlage>,x,z[,drehung] ·
            // streuen:<vorlage>,<radius>,<dichte>,x1,z1;x2,z2;… · radierer:<radius>,x1,z1;… – <vorlage> ist eine Gruppe (wand, kuppe, rippe) oder Formen mit +
            // (block+platte), hinten wahlweise @<gestein>; Größe, Einsinken, Neigung und Filter wie in der Tafel eingestellt
            if (o.TryGetValue("werkstatt-fels", out var felsen))
                foreach (var eintrag in felsen.Split('|', StringSplitOptions.RemoveEmptyEntries)) FelsBefehl(eintrag);
            // --werkstatt-rampe=x1,z1;x2,z2,breite[|…]: eine Rampe (ein Schritt)
            if (o.TryGetValue("werkstatt-rampe", out var rampen))
                foreach (var eintrag in rampen.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var teile = eintrag.Split(';');
                    var von = teile[0].Split(',');
                    var nach = teile[1].Split(',');
                    _entwurf.Rampe(new Kartenort(Zahl(von[0]), Zahl(von[1])), new Kartenort(Zahl(nach[0]), Zahl(nach[1])), Zahl(nach[2]));
                }
            if (o.TryGetValue("werkstatt-start", out var start))
                foreach (var befehl in start.Split('|', StringSplitOptions.RemoveEmptyEntries)) StartBefehl(befehl);
            if (o.TryGetValue("werkstatt-versetzen", out var versetzen))
            {
                var t = versetzen.Split(',');
                _entwurf.Versetzen(t[0], Zahl(t[1]), Zahl(t[2]), t.Length > 3 ? Zahl(t[3]) : _entwurf.GebaeudeMit(t[0])?.Drehung ?? 0);
                _stadt.WerkstattWaehlen(t[0]);
            }
            if (o.TryGetValue("werkstatt-rueckgaengig", out var zurueck))
                for (var i = 0; i < int.Parse(zurueck); i++) _entwurf.Rueckgaengig();
            if (o.TryGetValue("werkstatt-auswahl", out var auswahl)) _stadt.WerkstattWaehlen(TeilVon(auswahl), hinfahren: true);
            // --werkstatt-speichern=<datei>: unter diesem Namen im Benutzerordner; =1 wie Strg+S (Spielkarte mit Rückfrage), =direkt ohne sie
            if (o.TryGetValue("werkstatt-speichern", out var datei))
            {
                if (datei is "1" or "direkt" && _pfad is { } eigene && !_nurLesen)
                {
                    if (datei == "direkt") _ueberschreibenBestaetigt.Add(eigene);
                    Speichern();
                }
                else if (Dateiname(datei) is { } name) SpeichernNach($"{Ordner}/{name}");
            }
            if (o.TryGetValue("werkstatt-umriss", out var umriss))
            {
                var t = umriss.Split(',');
                KatalogWaehlen(t[0]);
                _stadt.WerkstattDrehung = t.Length > 3 ? Zahl(t[3]) : 0;
                _stadt.WerkstattUmrissSetzen(Zahl(t[1]), Zahl(t[2]));
            }
            if (o.TryGetValue("werkstatt-werkzeug", out var werkzeug)) KatalogWaehlen(werkzeug);
            if (o.TryGetValue("werkstatt-gruppe", out var gruppe)) GruppeOeffnen(gruppe);
            if (o.TryGetValue("werkstatt-ebenen", out var aus))
                foreach (var name in aus.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    if (name == "alle") EbenenAlle(false);
                    else _stadt.WerkstattEbeneSetzen(Enum.Parse<Werkstattebene>(name, ignoreCase: true), false);
            if (o.GetValueOrDefault("werkstatt-ebenenliste") == "1" && _ebenenKnopf is not null) _ebenenKnopf.ButtonPressed = true; // Liste offen (Haken zeigen den Stand)
            if (o.TryGetValue("katalogbilder", out var katalogbilder)) KatalogbilderSchreiben(katalogbilder);
            if (o.GetValueOrDefault("werkstatt-netz") == "1") _netzSchalter.ButtonPressed = true;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or IndexOutOfRangeException)
        {
            GD.PushError($"Kartenwerkstatt: Startoption nicht ausführbar – {ex.Message}");
        }
        if (o.TryGetValue("blick", out var blick))
        {
            var t = blick.Split(',');
            _stadt.KameraSetzen((float)Zahl(t[0]), (float)Zahl(t[1]), t.Length > 2 ? (float)Zahl(t[2]) : null);
        }
        if (o.TryGetValue("drehung", out var drehung) && int.TryParse(drehung, out var viertel)) _stadt.Drehen(viertel, sofort: true);
        // --werkstatt-kamerastart=1: den Blick (nach --blick und --drehung) als Kamera-Start übernehmen
        if (o.GetValueOrDefault("werkstatt-kamerastart") == "1") KameraStartUebernehmen();
        if (o.GetValueOrDefault("werkstatt-tafel") == "start") ReiterWaehlen(true);
        else if (o.GetValueOrDefault("werkstatt-tafel") == "karte") KarteReiterWaehlen();
        if (o.TryGetValue("werkstatt-haus", out var haus) && int.TryParse(haus, out var hausNummer))
        {
            _startHaus = hausNummer - 1;
            if (_startOffen) StartZeigen();
        }
        if (o.GetValueOrDefault("werkstatt-dialog") is { } dialog)
        {
            if (dialog == "ausprobieren") AusprobierenZeigen();
            else if (dialog == "neu") NeuZeigen();
            else if (dialog == "oeffnen") OeffnenZeigen();
            else if (dialog == "speichern") SpeichernUnterZeigen();
            else if (dialog == "zurueck") ZurueckZumMenue();
            else if (dialog == "schliessen") _Notification((int)NotificationWMCloseRequest);
        }
        if (o.GetValueOrDefault("ui") == "0") _oberflaeche.Visible = false;
        // --werkstatt-ausprobieren=<Beruf>: gleich eine Probepartie starten (nach allen anderen Schritten)
        if (o.TryGetValue("werkstatt-ausprobieren", out var probeBeruf)) Callable.From(() => Ausprobieren(probeBeruf)).CallDeferred();
        // --werkstatt-bildpunkte=x1,z1;x2,z2;…: schreibt nach 1,5 s, wo diese Orte auf dem Bildschirm liegen (für Klicks mit xdotool)
        if (o.TryGetValue("werkstatt-bildpunkte", out var orte))
            GetTree().CreateTimer(1.5).Timeout += () =>
            {
                foreach (var ort in Liste("-," + orte).Punkte)
                    GD.Print($"Bildpunkt {Kartenschreiber.Zahl(ort.X)},{Kartenschreiber.Zahl(ort.Z)} -> {_stadt.WerkstattBildpunkt(ort.X, ort.Z)}");
            };
    }

    /// <summary>Ein Schritt der Startoption <c>--werkstatt-fels</c> (siehe dort).</summary>
    private void FelsBefehl(string eintrag)
    {
        var (art, rest) = (eintrag.Split(':', 2)[0], eintrag.Contains(':') ? eintrag.Split(':', 2)[1] : "");
        Felspinsel Vorlage(string text)
        {
            var (vorlage, gestein) = (text.Split('@')[0], text.Contains('@') ? text.Split('@')[1] : null);
            if (gestein is not null && !Felsformen.IstGestein(gestein)) throw new ArgumentException($"Unbekannte Gesteinsart „{gestein}“.");
            var basis = _stadt.FelsPinsel with { Radierer = false, Gestein = gestein ?? _stadt.FelsGestein };
            if (Felsformen.Gruppe(vorlage) is { } g) return basis with { Gruppe = g.Kennung };
            var formen = vorlage.Split('+');
            if (formen.Any(f => Felsformen.Finde(f) is null)) throw new ArgumentException($"Unbekannte Felsvorlage „{vorlage}“ (wand, kuppe, rippe oder Formen mit +).");
            return basis with { Gruppe = null, Formen = formen };
        }
        switch (art)
        {
            case "grundgestein":
                _entwurf.GrundgesteinSetzen(rest);
                break;
            case "stempel":
            {
                var t = rest.Split(',');
                _stadt.WerkstattFelsStempeln(Vorlage(t[0]), new Kartenort(Zahl(t[1]), Zahl(t[2])), t.Length > 3 ? Zahl(t[3]) : null);
                break;
            }
            case "streuen":
            {
                var t = rest.Split(',', 4);
                var pinsel = Vorlage(t[0]) with { Radius = Zahl(t[1]), Dichte = Zahl(t[2]) };
                _stadt.WerkstattFelsStreuen(pinsel, Liste("-," + t[3]).Punkte.Select(q => new Kartenort(q.X, q.Z)).ToList());
                break;
            }
            case "radierer":
            {
                var t = rest.Split(',', 2);
                _stadt.WerkstattFelsStreuen(Felspinsel.Radiergummi(Zahl(t[0]), _stadt.FelsFilter), Liste("-," + t[1]).Punkte.Select(q => new Kartenort(q.X, q.Z)).ToList());
                break;
            }
            default:
                throw new ArgumentException($"Unbekannter Felsbefehl „{art}“ (grundgestein, stempel, streuen, radierer).");
        }
    }

    /// <summary>
    /// Ein Schritt der Startoption <c>--werkstatt-start</c>: „haus[:Name]“, „wohnhaus:&lt;Haus&gt;:&lt;Kennung&gt;“, „betrieb:&lt;Haus&gt;:&lt;Platz&gt;:&lt;Art&gt;[:&lt;Ware&gt;]“,
    /// „farbe:&lt;Haus&gt;:#rrggbb“, „spieler:&lt;Platz&gt;[:&lt;Beruf&gt;]“, „spielerwohnhaus:&lt;Kennung&gt;“, „spielerfarbe:#rrggbb“, „entfernen:&lt;Haus&gt;“ – Häuser
    /// mit ihrer Nummer ab 1.
    /// </summary>
    private void StartBefehl(string befehl)
    {
        var t = befehl.Split(':');
        int Haus(int i) => int.Parse(t[i]) - 1;
        switch (t[0])
        {
            case "haus": _startHaus = _entwurf.HausHinzufuegen(t.Length > 1 ? t[1].Replace('_', ' ') : null); break;
            case "wohnhaus": _entwurf.HausWohnhausSetzen(Haus(1), t[2]); break;
            case "betrieb": _entwurf.BetriebHinzufuegen(Haus(1), t[2], Enum.Parse<Betriebsart>(t[3], ignoreCase: true), t.Length > 4 ? t[4] : null); break;
            case "farbe": _entwurf.HausFarbeSetzen(Haus(1), t[2]); break;
            case "spieler": _entwurf.SpielerStartplatzSetzen(t[1], t.Length > 2 ? t[2] : null); break;
            case "spielerwohnhaus": _entwurf.SpielerWohnhausSetzen(t[1]); break;
            case "spielerfarbe": _entwurf.SpielerFarbeSetzen(t[1]); break;
            case "entfernen": _entwurf.HausEntfernen(Haus(1)); break;
            default: throw new ArgumentException($"Unbekannter Startbefehl „{t[0]}“ (haus, wohnhaus, betrieb, farbe, spieler, spielerwohnhaus, spielerfarbe, entfernen).");
        }
    }
}
