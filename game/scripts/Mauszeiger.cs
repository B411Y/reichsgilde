using Godot;

namespace Reichsgilde;

/// <summary>
/// Der eigene Mauszeiger: ein Lederhandschuh, dessen Stufe dem Stand des Hauses folgt (1 Gemeiner bis 6 Reichsgraf; PNGs aus
/// <c>tools/modelle/handschuh.py</c>, Blender, unter <c>assets/oberflaeche/zeiger/</c>). Drei Bilder:
/// <b>normal</b> (offene Hand), <b>klick</b> (wärmer, über Anklickbarem: Gebäude, Figur, Bauplatz im Baumodus, Knöpfe der Oberfläche)
/// und das <b>Türschloss</b> (Bronze mit S-Griff) über einem Gebäude mit Innenraum, das ein Doppelklick öffnet.
/// Das Steuerknoten-Objekt hängt in der Hauptszene (liest die Stadt) und im Hauptmenü (nur Stufe 1, normal). Die Größe (32, 48 oder 64 Pixel)
/// folgt der Fensterhöhe und der Größe der Oberfläche. Ein-/Ausschalter: <see cref="Fenster.EigenerZeigerAn"/>.
/// </summary>
public partial class Mauszeiger : Node
{
    public const int Stufen = 6;

    public enum Art { Normal, Klick, Schloss }

    /// <summary>Handstufe fest (Startoption <c>--zeigerstufe</c>); 0 = nach dem Stand des Hauses.</summary>
    public static int StufeFest { get; set; }

    private static readonly int[] Groessen = { 32, 48, 64 };

    /// <summary>Zeigerpunkt (Fingerspitze) bei 32 Pixeln; bei 48 und 64 mal 1,5 bzw. 2 (Ausgabe von <c>handschuh.py hotspots</c>).</summary>
    private static readonly Vector2I HandPunkt32 = new(6, 10), SchlossPunkt32 = new(11, 16);

    private static Mauszeiger? _aktiv;
    private static (int Stufe, Art Art, int Groesse, bool An)? _gesetzt;

    /// <summary>Stand des Hauses (0 = Gemeiner) oder null (kein Spiel, zum Beispiel im Hauptmenü).</summary>
    public Func<int?>? StandAbfrage { get; set; }

    /// <summary>Die Stadt, wenn eine da ist (Hauptszene); ohne sie nur Stufe und „klick“ auf Knöpfen.</summary>
    public Stadtansicht? Stadt { get; set; }

    public override void _Ready()
    {
        _aktiv = this;
        ProcessMode = ProcessModeEnum.Always;
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
        var stufe = StufeFest > 0 ? StufeFest : Math.Clamp((StandAbfrage?.Invoke() ?? 0) + 1, 1, Stufen);
        var art = an ? ArtErmitteln() : Art.Normal;
        var groesse = an ? GroesseWaehlen() : 0;
        if (_gesetzt == (stufe, art, groesse, an)) return;
        _gesetzt = (stufe, art, groesse, an);
        if (!an)
        {
            ZeigerEntfernen();
            return;
        }
        var bild = Bild(stufe, art, groesse);
        if (bild is null) return;
        var punkt = art == Art.Schloss ? SchlossPunkt32 : HandPunkt32;
        var hotspot = new Vector2(punkt.X, punkt.Y) * (groesse / 32f);
        // Pfeil = der gewählte Zustand; „Zeigefinger“ (Knöpfe mit Handform) immer klick.
        Input.SetCustomMouseCursor(bild, Input.CursorShape.Arrow, hotspot);
        var klick = art == Art.Normal ? Bild(stufe, Art.Klick, groesse) : bild;
        if (klick is not null) Input.SetCustomMouseCursor(klick, Input.CursorShape.PointingHand, hotspot);
        GD.Print($"[zeiger] Stufe {stufe} {art.ToString().ToLowerInvariant()} {groesse}px bei {GetViewport().GetMousePosition()} über {Beschreibung()}");
    }

    /// <summary>Wofür der Zustand gilt, fürs Log (Klicktest): Knopf/Tafel, Kennung des Gebäudes oder „Boden“.</summary>
    private string Beschreibung() =>
        GetViewport().GuiGetHoveredControl() is { } steuerung ? $"Steuerelement {steuerung.GetType().Name}" : Stadt?.KennungUnterDerMaus is { } kennung ? $"Kennung {kennung}" : "freiem Boden";

    private static void ZeigerEntfernen()
    {
        Input.SetCustomMouseCursor(null, Input.CursorShape.Arrow);
        Input.SetCustomMouseCursor(null, Input.CursorShape.PointingHand);
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

    /// <summary>32, 48 oder 64 Pixel: Fensterhöhe (1080 = 1) mal Größe der Oberfläche; Standard 48 bei 1080p.</summary>
    private static int GroesseWaehlen()
    {
        var mass = DisplayServer.WindowGetSize().Y / 1080f * Fenster.Oberflaechengroesse / 100f;
        return mass < 0.75f ? Groessen[0] : mass < 1.35f ? Groessen[1] : Groessen[2];
    }

    private static readonly Dictionary<string, Texture2D?> Vorrat = new();

    private static Texture2D? Bild(int stufe, Art art, int groesse)
    {
        var name = art == Art.Schloss ? $"tuer_schloss_{groesse}" : $"hand_{stufe}_{(art == Art.Klick ? "klick" : "normal")}_{groesse}";
        if (Vorrat.TryGetValue(name, out var bild)) return bild;
        var pfad = $"res://assets/oberflaeche/zeiger/{name}.png";
        bild = ResourceLoader.Exists(pfad) ? GD.Load<Texture2D>(pfad) : null;
        if (bild is null) GD.PushWarning($"[zeiger] Bild fehlt: {pfad}");
        Vorrat[name] = bild;
        return bild;
    }
}
