using Godot;

namespace Reichsgilde;

/// <summary>
/// Der eigene Mauszeiger: ein Lederhandschuh, dessen Stufe dem Stand des Hauses folgt (1 Gemeiner bis 6 Reichsgraf), dazu zwei Handschuhe
/// nur zur freien Wahl in den Einstellungen (7 Wache = Plattenhandschuh, 8 Dieb = schwarzes Leder). Die Bilder sind feine Pixel-Art aus gezeichneten
/// Vorlagen (<c>tools/oberflaeche/zeiger_aus_vorlage.py</c>) unter <c>assets/oberflaeche/zeiger/</c>. Drei Bilder:
/// <b>normal</b> (offene Hand, ohne hellen Rand), <b>klick</b> (über Anklickbarem: Gebäude, Figur, Bauplatz im Baumodus, Knöpfe der Oberfläche; derselbe Handschuh,
/// unverändert in den Lederfarben, mit weichem weißem Schein außen herum – beide Bilder gleich groß mit durchsichtigem Rand, daher gleicher Zeigerpunkt)
/// und das <b>Türschloss</b> (Bronze mit S-Griff, ebenfalls ohne hellen Rand und mit Außenschein, denn es erscheint nur über Anklickbarem) über einem Gebäude mit Innenraum, das ein Doppelklick öffnet.
/// Das Steuerknoten-Objekt hängt in der Hauptszene (liest die Stadt) und im Hauptmenü (nur Stufe 1, normal). Die Größe (48, 64 oder 80 Pixel)
/// folgt der Fensterhöhe und der Größe der Oberfläche. Dazu der <b>Hilfe-Zeiger</b> (Dreieck mit Fragezeichen aus Messing, <c>hilfe_&lt;px&gt;.png</c>, nach einer Vorlage des Nutzers) für <c>CursorShape.Help</c>. Ein-/Ausschalter: <see cref="Fenster.EigenerZeigerAn"/>. Ab 48 Pixel zeichnet statt der 2D-Bilder der 3D-Handschuh (<see cref="HandschuhZeiger"/>, Einstellung <see cref="Fenster.Handschuh3dAn"/>); der System-Zeiger wird dann unsichtbar, bei fehlender Datei oder Abschalten gilt wieder das Bild.
/// </summary>
public partial class Mauszeiger : Node
{
    /// <summary>Handstufen nach Stand des Hauses (1 Gemeiner bis 6 Reichsgraf).</summary>
    public const int Stufen = 6;

    /// <summary>Alle wählbaren Handschuhe: die sechs Stände, dazu 7 Wache und 8 Dieb (nur freie Wahl, nie nach Stand).</summary>
    public const int Haende = 8;

    public enum Art { Normal, Klick, Schloss }

    /// <summary>Handstufe fest nur für diesen Start (Startoption <c>--zeigerstufe</c> 1..8, schlägt die Wahl in den Einstellungen); 0 = die Wahl der Einstellungen
    /// (<see cref="Fenster.ZeigerStufe"/>), und deren 0 heißt: nach dem Stand des Hauses.</summary>
    public static int StufeFest { get; set; }

    private static readonly int[] Groessen = { 48, 64, 80 };

    /// <summary>Namen der acht Handschuhe (Index 0 = Stufe 1; 7 und 8 nur freie Wahl), für die Wahl in den Einstellungen.</summary>
    public static readonly string[] StufenNamen =
    {
        "Gemeiner – Arbeitsleder", "Bürger – helles Leder", "Patrizier – Braunleder mit Waldgrün",
        "Edler – schwarzes Leder", "Freiherr – Purpursamt", "Reichsgraf – Königsblau mit Hermelin",
        "Wache – Plattenhandschuh", "Dieb – schwarzes Leder",
    };

    /// <summary>Zeigerpunkt der Hand (Fingerspitze) je Größe 48, 64, 80 (Ausgabe von <c>zeiger_aus_vorlage.py</c>, <c>hotspots.txt</c>; für alle acht Hände gleich).</summary>
    private static readonly Vector2I[] HandPunkte = { new(8, 7), new(11, 9), new(13, 11) };

    /// <summary>Durchsichtiger Rand rundum je Größe 48, 64, 80 für den Außenschein (<c>SCHEINRAND</c> in <c>zeiger_aus_vorlage.py</c>); der Zeigerpunkt der Hand liegt um diesen Rand nach innen versetzt.</summary>
    private static readonly int[] Scheinrand = { 6, 8, 10 };

    /// <summary>Zeigerpunkt (Mitte des Schlüssellochs, samt Scheinrand 6/8/10) des Türschlosses je Größe 48, 64, 80.</summary>
    private static readonly Vector2[] SchlossPunkte = { new(21, 31), new(27, 41), new(34, 52) };

    /// <summary>Das Bild eines Handschuhs (normal, 48 Pixel, ohne den durchsichtigen Rand für den Schein) für die Vorschau in den Einstellungen.</summary>
    public static Texture2D? Vorschau(int stufe) =>
        Bild(stufe, Art.Normal, 48) is { } bild ? new AtlasTexture { Atlas = bild, Region = new Rect2(Scheinrand[0], Scheinrand[0], 48, 48) } : null;

    private static Mauszeiger? _aktiv;
    private static (int Stufe, Art Art, int Groesse, bool An, bool Hilfe, bool Drei)? _gesetzt;
    private HandschuhZeiger? _handschuh;

    /// <summary>Stand des Hauses (0 = Gemeiner) oder null (kein Spiel, zum Beispiel im Hauptmenü).</summary>
    public Func<int?>? StandAbfrage { get; set; }

    /// <summary>Die Stadt, wenn eine da ist (Hauptszene); ohne sie nur Stufe und „klick“ auf Knöpfen.</summary>
    public Stadtansicht? Stadt { get; set; }

    public override void _Ready()
    {
        _aktiv = this;
        ProcessMode = ProcessModeEnum.Always;
        AddChild(_handschuh = new HandschuhZeiger()); // 3D-Handschuh obenauf (HandschuhZeiger.cs), zeichnet nur, wenn er gilt
        if (OS.GetCmdlineUserArgs().Contains("--klinketest=1")) _handschuh.KlinkeTestStarten(); // Hover über einer Tür und Klinke-Folge vortäuschen, Log [klinketest]
    }

    public override void _ExitTree()
    {
        if (_aktiv == this) _aktiv = null;
    }

    public override void _Process(double delta) => Anwenden();

    /// <summary>Beim Ein- und Ausschalten sofort neu setzen (Einstellungen).</summary>
    public static void Auffrischen()
    {
        _gesetzt = null;
        _aktiv?.Anwenden();
        if (_aktiv is null && !Fenster.EigenerZeigerAn) ZeigerEntfernen();
    }

    private void Anwenden()
    {
        var an = Fenster.EigenerZeigerAn;
        var stufe = StufeFest > 0 ? Math.Clamp(StufeFest, 1, Haende) : Fenster.ZeigerStufe > 0 ? Math.Clamp(Fenster.ZeigerStufe, 1, Haende) : Math.Clamp((StandAbfrage?.Invoke() ?? 0) + 1, 1, Stufen);
        var art = an ? ArtErmitteln() : Art.Normal;
        var groesse = an ? GroesseWaehlen() : 0;
        // Fragezeichen nur genau dort, wo das Steuerelement unter der Maus wirklich einen Tooltip hat; sonst trägt der Hilfe-Platz das Bild des Pfeils.
        var hilfeAn = an && !string.IsNullOrEmpty(Tooltips.QuelleFinden(GetViewport().GuiGetHoveredControl()).Text);
        // 3D-Handschuh (ab 48 Pixel, Einstellung, Datei da): die Bühne zeichnet ihn, der System-Zeiger wird unsichtbar; sonst wie bisher der 2D-Zeiger.
        var drei = an && HandschuhZeiger.Wirksam(groesse);
        Stadt?.InnenSchweben(an && art == Art.Schloss ? Stadt.KennungUnterDerMaus : null); // Vorladen des Raums unter der Maus (Stadtansicht.Uebergang.cs)
        _handschuh?.Setze(drei, groesse, art != Art.Normal, hilfeAn, an && TextUnterMaus(), art == Art.Schloss, Stadt?.RahmenZieht ?? false, GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit);
        if (_gesetzt == (stufe, art, groesse, an, hilfeAn, drei)) return;
        _gesetzt = (stufe, art, groesse, an, hilfeAn, drei);
        if (!an)
        {
            ZeigerEntfernen();
            return;
        }
        if (drei)
        {
            UnsichtbarSetzen();
            GD.Print($"[zeiger] 3D-Handschuh {groesse}px über {Beschreibung()}");
            return;
        }
        var bild = Bild(stufe, art, groesse);
        if (bild is null) return;
        var nr = Array.IndexOf(Groessen, groesse);
        var hotspot = art == Art.Schloss ? SchlossPunkte[nr] : new Vector2(HandPunkte[nr].X, HandPunkte[nr].Y);
        // Godot macht beim Setzen eines Bildes die zuletzt bzw. mittig gesetzte Form (früher: Hilfe) zur aktuellen, bis sich die Maus bewegt – das Fragezeichen blitzte so auf Knopfrändern auf.
        // Darum die Form von vorher danach wieder herstellen.
        var vorher = Input.GetCurrentCursorShape();
        var hilfe = hilfeAn ? HilfeBild(groesse) : null;
        if (hilfe is not null) Input.SetCustomMouseCursor(hilfe, Input.CursorShape.Help, HilfePunkte[nr]);
        else Input.SetCustomMouseCursor(bild, Input.CursorShape.Help, hotspot);
        // „Zeigefinger“ (Knöpfe mit Handform) immer klick.
        var klick = art == Art.Normal ? Bild(stufe, Art.Klick, groesse) : bild;
        if (klick is not null) Input.SetCustomMouseCursor(klick, Input.CursorShape.PointingHand, hotspot);
        // Pfeil = der gewählte Zustand.
        Input.SetCustomMouseCursor(bild, Input.CursorShape.Arrow, hotspot);
        if (Input.GetCurrentCursorShape() != vorher) DisplayServer.CursorSetShape((DisplayServer.CursorShape)(int)vorher);
        GD.Print($"[zeiger] Stufe {stufe} {art.ToString().ToLowerInvariant()} {groesse}px bei {GetViewport().GetMousePosition()} über {Beschreibung()}");
    }

    /// <summary>Steht die Maus über einem Textfeld (der Zeiger dort wäre der I-Strich)? Dann nimmt der 3D-Handschuh die Haltung Text.</summary>
    private bool TextUnterMaus() =>
        GetViewport().GuiGetHoveredControl() is { } steuerung && steuerung.GetCursorShape(steuerung.GetLocalMousePosition()) == Control.CursorShape.Ibeam;

    /// <summary>Wofür der Zustand gilt, fürs Log (Klicktest): Knopf/Tafel, Kennung des Gebäudes oder „Boden“.</summary>
    private string Beschreibung() =>
        GetViewport().GuiGetHoveredControl() is { } steuerung ? $"Steuerelement {steuerung.GetType().Name}" : Stadt?.KennungUnterDerMaus is { } kennung ? $"Kennung {kennung}" : "freiem Boden";

    /// <summary>Für den 3D-Handschuh: Pfeil, Hand und Hilfe des Systems werden ein durchsichtiges Pünktchen (Zeiger bleibt für Godot „da“, die Form wechselt weiter).</summary>
    private static void UnsichtbarSetzen()
    {
        _leer ??= ImageTexture.CreateFromImage(Image.CreateEmpty(2, 2, false, Image.Format.Rgba8));
        var vorher = Input.GetCurrentCursorShape();
        Input.SetCustomMouseCursor(_leer, Input.CursorShape.Help);
        Input.SetCustomMouseCursor(_leer, Input.CursorShape.Ibeam); // der I-Strich des Systems fällt weg: über Textfeldern zeichnet der Handschuh die Haltung Text
        Input.SetCustomMouseCursor(_leer, Input.CursorShape.PointingHand);
        Input.SetCustomMouseCursor(_leer, Input.CursorShape.Arrow);
        if (Input.GetCurrentCursorShape() != vorher) DisplayServer.CursorSetShape((DisplayServer.CursorShape)(int)vorher);
    }

    private static ImageTexture? _leer;

    private static void ZeigerEntfernen()
    {
        Input.SetCustomMouseCursor(null, Input.CursorShape.Arrow);
        Input.SetCustomMouseCursor(null, Input.CursorShape.PointingHand);
        Input.SetCustomMouseCursor(null, Input.CursorShape.Help);
        Input.SetCustomMouseCursor(null, Input.CursorShape.Ibeam);
    }

    private static Texture2D? HilfeBild(int groesse)
    {
        var name = $"hilfe_{groesse}";
        if (Vorrat.TryGetValue(name, out var bild)) return bild;
        var pfad = $"res://assets/oberflaeche/zeiger/{name}.png";
        bild = ResourceLoader.Exists(pfad) ? GD.Load<Texture2D>(pfad) : null;
        if (bild is null) GD.PushWarning($"[zeiger] Bild fehlt: {pfad}");
        Vorrat[name] = bild;
        return bild;
    }

    /// <summary>Was liegt unter der Maus? Tafeln und Knöpfe zuerst, dann die Stadt.</summary>
    private Art ArtErmitteln()
    {
        var steuerung = GetViewport().GuiGetHoveredControl();
        if (steuerung is not null)
        {
            var anklickbar = steuerung is BaseButton { Disabled: false } || steuerung.MouseDefaultCursorShape == Control.CursorShape.PointingHand;
            return anklickbar ? Art.Klick : Art.Normal;
        }
        if (Stadt is null) return Art.Normal;
        if (Stadt.KennungUnterDerMaus is { } kennung)
            return Stadt.RaumKennung is null && Stadt.HatInnenansicht(kennung) ? Art.Schloss : Art.Klick;
        return Stadt.Baumodus ? Art.Klick : Art.Normal;
    }

    /// <summary>48, 64 oder 80 Pixel: Fensterhöhe (1080 = 1) mal Größe der Oberfläche; Standard 64 bei 1080p (Nutzerwunsch 11.11.2026; 48 nur noch bei kleinem Fenster oder kleiner Oberfläche).</summary>
    private static int GroesseWaehlen()
    {
        var mass = DisplayServer.WindowGetSize().Y / 1080f * Fenster.Oberflaechengroesse / 100f;
        return mass < 0.75f ? Groessen[0] : mass < 1.35f ? Groessen[1] : Groessen[2];
    }

    /// <summary>Zeigerpunkt des Hilfe-Zeigers (Spitze des Dreiecks) je Größe 48, 64, 80 (Ausgabe von <c>zeiger_hilfe.py</c>, <c>hotspots.txt</c>).</summary>
    private static readonly Vector2[] HilfePunkte = { new(0, 0), new(0, 0), new(0, 0) };

    private static readonly Dictionary<string, Texture2D?> Vorrat = new();

    private static Texture2D? Bild(int stufe, Art art, int groesse)
    {
        var hand = stufe switch { 7 => "wache", 8 => "dieb", _ => stufe.ToString() };
        var name = art == Art.Schloss ? $"tuer_schloss_{groesse}" : $"hand_{hand}_{(art == Art.Klick ? "klick" : "normal")}_{groesse}";
        if (Vorrat.TryGetValue(name, out var bild)) return bild;
        var pfad = $"res://assets/oberflaeche/zeiger/{name}.png";
        bild = ResourceLoader.Exists(pfad) ? GD.Load<Texture2D>(pfad) : null;
        if (bild is null) GD.PushWarning($"[zeiger] Bild fehlt: {pfad}");
        Vorrat[name] = bild;
        return bild;
    }
}
