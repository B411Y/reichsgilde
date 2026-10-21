using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Die Kartenwerkstatt (Level-Editor), eine eigene Szene, erreichbar über das Hauptmenü. Sie zeigt eine Karte so wie das Spiel – aber ohne
/// Simulation – und bearbeitet sie über den <see cref="Kartenentwurf"/> des Kerns: Gebäude setzen, verschieben, drehen, umbenennen, löschen,
/// jeweils mit Rückgängig und Wiederholen, dazu die Prüfung nach den Kartenregeln (live) und das Speichern im Kartenformat 2.
///
/// Aufgaben verteilt:
///   • diese Datei: Öffnen und Speichern, Tasten, Rückfragen, Startoptionen;
///   • Kartenwerkstatt.Tafeln.cs: Leiste oben, Katalog links, Eigenschaften und Fehlerliste rechts;
///   • Stadtansicht.Kartenwerkstatt.cs: alles in der 3D-Welt (Gebäude zeichnen, Umriss, Auswahl, Ziehen).
///
/// Speicherort (Entscheidung vom 21.10.2026): Die mitgelieferten Karten (<c>game/karten/</c>, also Greybox und Köln) öffnet die Werkstatt
/// nur lesend; gespeichert wird nur im Benutzerordner <c>user://karten/</c> (wie die Spielstände). So überschreibt sie nie, was das
/// Python-Werkzeug erzeugt. Eine andere Karte zu öffnen (oder eine neue) lädt die Szene neu – so beginnt die Stadtansicht immer leer.
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

    private Kartenentwurf _entwurf = null!;

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
        if (_auftrag is not { } auftrag)
            (auftrag, meldung) = Startkarte(optionen.GetValueOrDefault("kartenwerkstatt"));
        _auftrag = null;
        (_entwurf, _pfad, _nurLesen) = auftrag;

        _stadt.EntwurfZeigen(_entwurf);
        OberflaecheBauen(); // Kartenwerkstatt.Tafeln.cs
        _entwurf.Geaendert += a =>
        {
            AuffrischenVormerken();
            if (a.Gebaeude.Kennung == _stadt.WerkstattAuswahl) EigenschaftenZeigen(neu: false);
        };
        _stadt.WerkstattAuswahlGeaendert += _ => EigenschaftenZeigen(neu: true);
        _stadt.WerkstattSetzenBeendet += KatalogWahlAufheben;
        Auffrischen();
        EigenschaftenZeigen(neu: true);
        if (meldung is not null) Melden(meldung);
        StartoptionenAusfuehren(optionen);
    }

    public override void _Process(double delta) => _stadt.WerkstattAktualisieren();

    private bool _auffrischenVorgemerkt;

    /// <summary>
    /// Kopf und Fehlerliste erst am Ende des Bildes auffrischen: Der Entwurf meldet eine Änderung, bevor seine Geschichte den Schritt kennt
    /// (Rückgängig wäre sonst noch gesperrt), und mehrere Änderungen in einem Bild (Startoptionen) bauen die Liste nur einmal.
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

    /// <summary>
    /// Die Schritte der Startoptionen in fester Reihenfolge: setzen, versetzen, rückgängig, auswahl, speichern, umriss, dann Kamera und Tafeln
    /// (Liste in <c>CLAUDE.md</c>, „Bauen, testen, ausprobieren“).
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
            if (o.TryGetValue("werkstatt-versetzen", out var versetzen))
            {
                var t = versetzen.Split(',');
                _entwurf.Versetzen(t[0], Zahl(t[1]), Zahl(t[2]), t.Length > 3 ? Zahl(t[3]) : _entwurf.GebaeudeMit(t[0])?.Drehung ?? 0);
                _stadt.WerkstattWaehlen(t[0]);
            }
            if (o.TryGetValue("werkstatt-rueckgaengig", out var zurueck))
                for (var i = 0; i < int.Parse(zurueck); i++) _entwurf.Rueckgaengig();
            if (o.TryGetValue("werkstatt-auswahl", out var auswahl)) _stadt.WerkstattWaehlen(auswahl, hinfahren: true);
            if (o.TryGetValue("werkstatt-speichern", out var datei) && Dateiname(datei) is { } name) SpeichernNach($"{Ordner}/{name}");
            if (o.TryGetValue("werkstatt-umriss", out var umriss))
            {
                var t = umriss.Split(',');
                KatalogWaehlen(t[0]);
                _stadt.WerkstattDrehung = t.Length > 3 ? Zahl(t[3]) : 0;
                _stadt.WerkstattUmrissSetzen(Zahl(t[1]), Zahl(t[2]));
            }
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
        if (o.GetValueOrDefault("werkstatt-dialog") is { } dialog)
        {
            if (dialog == "neu") NeuZeigen();
            else if (dialog == "oeffnen") OeffnenZeigen();
            else if (dialog == "speichern") SpeichernUnterZeigen();
            else if (dialog == "zurueck") ZurueckZumMenue();
        }
        if (o.GetValueOrDefault("ui") == "0") _oberflaeche.Visible = false;
    }
}
