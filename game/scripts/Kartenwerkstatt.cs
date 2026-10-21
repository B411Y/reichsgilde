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
///   • Kartenwerkstatt.Tafeln.cs: Leiste oben, Katalog links, Eigenschaften der Gebäude und Fehlerliste rechts, Rückfragen;
///   • Kartenwerkstatt.Eigenschaften.cs: die Tafel rechts für Wege, Flächen (mit Streu-Gruppen), Pflanzen, Mauern und den Pinsel;
///   • Kartenwerkstatt.Start.cs: die Tafel „Start“ (Startbelegung: Spieler und KI-Häuser);
///   • Stadtansicht.Kartenwerkstatt*.cs: alles in der 3D-Welt (Gebäude, Wege, Flächen, Natur, Mauern, Belegung, Auswahl, Ziehen).
///
/// Speicherort (Entscheidung vom 21.10.2026): Die mitgelieferten Karten (<c>game/karten/</c>, also Greybox und Köln) öffnet die Werkstatt
/// nur lesend; gespeichert wird nur im Benutzerordner <c>user://karten/</c> (wie die Spielstände). So überschreibt sie nie, was das
/// Python-Werkzeug erzeugt. Eine andere Karte zu öffnen (oder eine neue) lädt die Szene neu – so beginnt die Stadtansicht immer leer.
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

    /// <summary>Die Karten, die mit dem Spiel kommen – nur lesen.</summary>
    private const string Mitgeliefert = "res://karten";

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

    /// <summary>Woher die Karte kommt (null: neu, noch nie gespeichert) und ob die Datei nur gelesen werden darf (mitgeliefert).</summary>
    private string? _pfad;
    private bool _nurLesen;

    private Stadtansicht _stadt = null!;

    /// <summary>Die Wurzel aller Tafeln (für <c>--ui=0</c>).</summary>
    private CanvasLayer _oberflaeche = null!;

    public override void _Ready()
    {
        _stadt = GetNode<Stadtansicht>("%Stadt");
        var optionen = _startoptionenErledigt ? new Dictionary<string, string>() : Startoptionen();
        _startoptionenErledigt = true;
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

        _stadt.EntwurfZeigen(_entwurf);
        OberflaecheBauen(); // Kartenwerkstatt.Tafeln.cs
        _entwurf.Geaendert += EntwurfGeaendert;
        _stadt.WerkstattAuswahlGeaendert += AuswahlGeaendert;
        _stadt.WerkstattWerkzeugBeendet += KatalogWahlAufheben;
        _stadt.WerkstattWerkzeugGeaendert += () =>
        {
            // ein Werkzeug des Katalogs zeigt seine Tafel unter „Eigenschaften“ (Pinsel); die Wahl eines Platzes gehört zur Tafel „Start“
            if (_startOffen && _stadt.Kartenwerkzeug is not (Werkstattwerkzeug.Auswahl or Werkstattwerkzeug.Platz)) ReiterWaehlen(false);
            EigenschaftenZeigen(neu: true);
            HilfeZeigen();
        };
        _stadt.WerkstattMeldung += (text, fehler) => Melden(text, fehler);
        _stadt.WerkstattNaturGestreut += () => EigenschaftenZeigen(neu: false);
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
    }

    public override void _Process(double delta) => _stadt.WerkstattAktualisieren();

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
    }

    /// <summary>Liest <c>wirtschaft.json</c> (wie die Hauptszene) – oder nennt den Grund, warum es nicht geht.</summary>
    private static (Wirtschaftsdaten? Daten, string? Fehler) DatenLaden()
    {
        try
        {
            var json = Godot.FileAccess.GetFileAsString(Hauptszene.WirtschaftsdatenPfad);
            return string.IsNullOrEmpty(json) ? (null, $"{Hauptszene.WirtschaftsdatenPfad} fehlt – ohne sie kennt die Startbelegung keine Betriebsarten.")
                : (Wirtschaftsdaten.AusJson(json), null);
        }
        catch (WirtschaftsdatenFehler fehler)
        {
            return (null, fehler.Message);
        }
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
    /// Die Karte beim ersten Start: aus der Startoption (<c>greybox</c>, <c>koeln</c>, <c>neu</c> oder eine Datei <c>x.json</c> aus
    /// <c>user://karten/</c>), sonst die Greybox. Lässt sie sich nicht öffnen, eine leere Karte mit der Meldung warum.
    /// </summary>
    private static ((Kartenentwurf, string?, bool) Auftrag, string? Meldung) Startkarte(string? wahl)
    {
        wahl ??= Stadtkarten.Vorgabe;
        if (wahl == "neu") return ((Kartenentwurf.Neu("neustadt", "Neustadt", 300, 300, KatalogLaden()), null, false), null);
        var (pfad, nurLesen) = wahl.EndsWith(".json") ? ($"{Ordner}/{wahl}", false) : ($"{Mitgeliefert}/{wahl}.json", true);
        var (entwurf, fehler) = Lesen(pfad);
        if (entwurf is not null) return ((entwurf, pfad, nurLesen), null);
        return ((Kartenentwurf.Neu("neustadt", "Neustadt", 300, 300, KatalogLaden()), null, false), fehler);
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

    /// <summary>Speichern (Strg+S): in die eigene Datei; eine mitgelieferte oder neue Karte fragt nach dem Namen.</summary>
    private void Speichern()
    {
        if (_pfad is null || _nurLesen) SpeichernUnterZeigen();
        else SpeichernNach(_pfad);
    }

    /// <summary>Schreibt die Karte nach <paramref name="pfad"/> (unter <c>user://karten/</c>) und merkt den Stand als gespeichert.</summary>
    private bool SpeichernNach(string pfad)
    {
        DirAccess.MakeDirRecursiveAbsolute(Ordner);
        using var datei = Godot.FileAccess.Open(pfad, Godot.FileAccess.ModeFlags.Write);
        if (datei is null)
        {
            Melden($"Speichern fehlgeschlagen: {Godot.FileAccess.GetOpenError()}", fehler: true);
            return false;
        }
        datei.StoreString(_entwurf.ZuJson());
        _entwurf.Geschichte.Gespeichert();
        (_pfad, _nurLesen) = (pfad, false);
        Melden($"Gespeichert: {ProjectSettings.GlobalizePath(pfad)}");
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

    /// <summary>Die eigenen Karten im Benutzerordner und die mitgelieferten: Pfad und Anzeige („Köln – koeln.json“).</summary>
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

    private static double Zahl(string text) => double.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>„art,x1,z1;x2,z2;…[,wort]“ → Art, Punkte und das Wort am Ende (oder null).</summary>
    private static (string Art, List<Kartenort> Punkte, string? Wort) Liste(string eintrag)
    {
        var t = eintrag.Replace(';', ',').Split(',', StringSplitOptions.RemoveEmptyEntries);
        var rest = t.Length > 1 && !double.TryParse(t[^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) ? t[^1] : null;
        var zahlen = t.Skip(1).Take(t.Length - 1 - (rest is null ? 0 : 1)).Select(Zahl).ToList();
        var punkte = Enumerable.Range(0, zahlen.Count / 2).Select(i => new Kartenort(zahlen[2 * i], zahlen[2 * i + 1])).ToList();
        return (t[0], punkte, rest);
    }

    private EntwurfTeil AussparungUndFlaeche(EntwurfNaturflaeche f, List<Kartenort> punkte)
    {
        _entwurf.AussparungAnlegen(f, punkte);
        return f;
    }

    /// <summary>
    /// Ein Teil aus der Startoption <c>--werkstatt-auswahl</c>: „weg:&lt;Name&gt;“, „zone:&lt;Kennung&gt;“, „natur:&lt;Name&gt;“, „mauer:&lt;Name&gt;“,
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
            "mauer" => _entwurf.Mauern.FirstOrDefault(m => m.Name == name),
            _ => _entwurf.GebaeudeMit(text),
        };
    }

    /// <summary>
    /// Die Schritte der Startoptionen in fester Reihenfolge: setzen, mauer, tor, weg, flaeche, pinsel, radierer, start, versetzen, rückgängig,
    /// auswahl, speichern, umriss, werkzeug, netz, dann Kamera, Tafeln und zuletzt ausprobieren (Liste in <c>CLAUDE.md</c>, „Bauen, testen,
    /// ausprobieren“). Mehrere Wege, Flächen, Mauern, Tore, Striche und Startbefehle trennt „|“, Punkte „;“.
    /// </summary>
    private void StartoptionenAusfuehren(Dictionary<string, string> o)
    {
        try
        {
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
            // --werkstatt-weg=gasse,x1,z1;x2,z2;…[,gerade]
            if (o.TryGetValue("werkstatt-weg", out var wege))
                foreach (var eintrag in wege.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var (art, punkte, wort) = Liste(eintrag);
                    var klasse = Enum.Parse<Wegklasse>(art, ignoreCase: true);
                    if (_stadt.WerkstattWegAnlegen(klasse, punkte, wort != "gerade") is { } w) _stadt.WerkstattWaehlen(w);
                }
            // --werkstatt-flaeche=wald|streu|bauzone:<lage>|aussparung,x1,z1;x2,z2;…
            if (o.TryGetValue("werkstatt-flaeche", out var flaechen))
                foreach (var eintrag in flaechen.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var (art, punkte, _) = Liste(eintrag);
                    EntwurfTeil? neu = art switch
                    {
                        "wald" or "streu" => _entwurf.NaturflaecheAnlegen(art, punkte),
                        "aussparung" when _stadt.WerkstattGewaehlt is EntwurfNaturflaeche f => AussparungUndFlaeche(f, punkte),
                        _ when art.StartsWith("bauzone") => _entwurf.BauzoneAnlegen(art.Contains(':') ? Enum.Parse<Lage>(art.Split(':')[1], true) : Lage.Vorstadt, punkte),
                        _ => throw new ArgumentException($"Unbekannte Fläche „{art}“ (wald, streu, bauzone:<lage>, aussparung nach einer Naturfläche)."),
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
            if (o.TryGetValue("werkstatt-speichern", out var datei) && Dateiname(datei) is { } name) SpeichernNach($"{Ordner}/{name}");
            if (o.TryGetValue("werkstatt-umriss", out var umriss))
            {
                var t = umriss.Split(',');
                KatalogWaehlen(t[0]);
                _stadt.WerkstattDrehung = t.Length > 3 ? Zahl(t[3]) : 0;
                _stadt.WerkstattUmrissSetzen(Zahl(t[1]), Zahl(t[2]));
            }
            if (o.TryGetValue("werkstatt-werkzeug", out var werkzeug)) KatalogWaehlen(werkzeug);
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
        if (o.GetValueOrDefault("werkstatt-tafel") == "start") ReiterWaehlen(true);
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
