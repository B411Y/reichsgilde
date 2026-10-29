using Godot;

namespace Reichsgilde;

/// <summary>
/// Klicktest ohne echte Maus (Autoload): Das Spiel führt die Aktionen des Klicktests selbst aus, für Windows, wo es keinen unsichtbaren
/// Bildschirm wie Xvfb gibt (<c>tools/klicktest/lauf.ps1</c>). Nur aktiv mit <c>--klickskript=&lt;aktionen&gt;</c> und
/// <c>--klickausgabe=&lt;ordner&gt;</c>, sonst tut er nichts. <c>--klickwarten=&lt;sek&gt;</c> wartet vorher (ab Programmstart).
/// Aktionen wie in <c>lauf.sh</c>, durch <c>;</c> getrennt: <c>m x y</c> (Maus bewegen), <c>w sek</c> (warten), <c>k taste</c>
/// (Taste, Namen wie bei xdotool, auch <c>ctrl+s</c>), <c>b knopf</c> (1 links, 2 Mitte, 3 rechts, 4/5 Rad), <c>d x1 y1 x2 y2</c>
/// (mit links ziehen), <c>s name</c> (Bildschirmfoto <c>&lt;ordner&gt;/&lt;name&gt;.png</c> aus der Viewport-Textur).
/// Maus und Tasten gehen als InputEvents über <see cref="Input.ParseInputEvent"/> ins Spiel, die echte Maus bleibt, wo sie ist.
/// Grenze: <see cref="Viewport.GetMousePosition"/> liest im Hauptfenster die echte Maus, Hover und Tooltips folgen also ihr.
/// Am Ende beendet er das Spiel; jede Aktion steht im Log (Zeilen mit „Klicktest:“).
/// </summary>
public partial class KlicktestTreiber : Node
{
    private const string OptionSkript = "--klickskript=";
    private const string OptionAusgabe = "--klickausgabe=";
    private const string OptionWarten = "--klickwarten=";

    /// <summary>Die Startoptionen ohne die des Klicktests: Ein Lauf nur mit ihnen verhält sich wie ein Start ohne Optionen (Hauptmenü).</summary>
    public static string[] SpielOptionen() =>
        OS.GetCmdlineUserArgs().Where(a => !a.StartsWith("--klick")).ToArray();

    private Vector2 _maus = new(800, 450);

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        var skript = args.LastOrDefault(a => a.StartsWith(OptionSkript))?[OptionSkript.Length..];
        var ausgabe = args.LastOrDefault(a => a.StartsWith(OptionAusgabe))?[OptionAusgabe.Length..];
        if (string.IsNullOrWhiteSpace(skript) || string.IsNullOrWhiteSpace(ausgabe)) return;
        var warten = double.TryParse(args.LastOrDefault(a => a.StartsWith(OptionWarten))?[OptionWarten.Length..],
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w) ? w : 0;
        ProcessMode = ProcessModeEnum.Always; // auch bei angehaltenem Spiel
        DirAccess.MakeDirRecursiveAbsolute(ausgabe);
        _ = Ablaufen(skript, ausgabe, warten);
    }

    private async Task Ablaufen(string skript, string ausgabe, double warten)
    {
        var schonVergangen = Time.GetTicksMsec() / 1000.0;
        Melden($"Start, warte {warten:0.#} s (seit Programmstart {schonVergangen:0.#} s vergangen)");
        if (warten > schonVergangen) await Warten(warten - schonVergangen);
        foreach (var roh in skript.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var teile = roh.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            try
            {
                switch (teile[0])
                {
                    case "m": Bewegen(new Vector2(Zahl(teile[1]), Zahl(teile[2])), 0); break;
                    case "w": await Warten(Zahl(teile[1])); break;
                    case "k": await Taste(teile[1]); break;
                    case "b": await Klicken(int.Parse(teile[1])); break;
                    case "d": await Ziehen(new Vector2(Zahl(teile[1]), Zahl(teile[2])), new Vector2(Zahl(teile[3]), Zahl(teile[4]))); break;
                    case "s": await Foto(ausgabe, teile[1]); break;
                    default: Melden($"unbekannte Aktion „{roh}“, übersprungen"); continue;
                }
                Melden(roh);
            }
            catch (Exception fehler) when (fehler is IndexOutOfRangeException or FormatException)
            {
                Melden($"Aktion „{roh}“ unvollständig, übersprungen");
            }
        }
        Melden("fertig, beende das Spiel");
        GetTree().Quit();
    }

    private static float Zahl(string text) =>
        float.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

    private static void Melden(string text) => GD.Print($"Klicktest: {text}");

    /// <summary>Wartet in echter Zeit, unabhängig von Spieltempo und Pause.</summary>
    private async Task Warten(double sekunden) =>
        await ToSignal(GetTree().CreateTimer(sekunden, processAlways: true, processInPhysics: false, ignoreTimeScale: true), SceneTreeTimer.SignalName.Timeout);

    private async Task Bild() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    private void Bewegen(Vector2 ziel, MouseButtonMask knoepfe)
    {
        Input.ParseInputEvent(new InputEventMouseMotion
        {
            Position = ziel, GlobalPosition = ziel, Relative = ziel - _maus, ButtonMask = knoepfe,
        });
        _maus = ziel;
    }

    private void Knopf(MouseButton knopf, bool gedrueckt)
    {
        Input.ParseInputEvent(new InputEventMouseButton
        {
            Position = _maus, GlobalPosition = _maus, ButtonIndex = knopf, Pressed = gedrueckt,
            ButtonMask = gedrueckt && knopf <= MouseButton.Middle ? (MouseButtonMask)(1 << ((int)knopf - 1)) : 0,
            Factor = 1,
        });
    }

    /// <summary>Knöpfe wie bei xdotool: 1 links, 2 Mitte, 3 rechts, 4 Rad hoch, 5 Rad runter.</summary>
    private async Task Klicken(int nummer)
    {
        var knopf = nummer switch
        {
            1 => MouseButton.Left, 2 => MouseButton.Middle, 3 => MouseButton.Right,
            4 => MouseButton.WheelUp, 5 => MouseButton.WheelDown,
            _ => throw new FormatException(),
        };
        Knopf(knopf, true);
        await Bild();
        Knopf(knopf, false);
        await Bild();
    }

    /// <summary>Wie in lauf.sh: drücken, über die Mitte zum Ziel, je 0,3 s Pause, loslassen.</summary>
    private async Task Ziehen(Vector2 von, Vector2 nach)
    {
        Bewegen(von, 0);
        Knopf(MouseButton.Left, true);
        await Warten(0.3);
        Bewegen((von + nach) / 2, MouseButtonMask.Left);
        await Warten(0.3);
        Bewegen(nach, MouseButtonMask.Left);
        await Warten(0.3);
        Knopf(MouseButton.Left, false);
        await Bild();
    }

    /// <summary>xdotool-Namen, die Godot anders nennt; der Rest (F5, Tab, Buchstaben, Ziffern) geht über <see cref="OS.FindKeycodeFromString"/>.</summary>
    private static readonly Dictionary<string, Key> Tastennamen = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Return"] = Key.Enter, ["KP_Enter"] = Key.KpEnter, ["space"] = Key.Space, ["BackSpace"] = Key.Backspace,
        ["Prior"] = Key.Pageup, ["Next"] = Key.Pagedown, ["Page_Up"] = Key.Pageup, ["Page_Down"] = Key.Pagedown,
        ["plus"] = Key.Plus, ["minus"] = Key.Minus, ["comma"] = Key.Comma, ["period"] = Key.Period,
        ["ctrl"] = Key.Ctrl, ["Control_L"] = Key.Ctrl, ["shift"] = Key.Shift, ["Shift_L"] = Key.Shift, ["alt"] = Key.Alt, ["Alt_L"] = Key.Alt,
    };

    private static Key TasteZuKey(string name) =>
        Tastennamen.TryGetValue(name, out var key) ? key
        : OS.FindKeycodeFromString(name) is var gefunden and not Key.None ? gefunden
        : throw new FormatException();

    /// <summary>Eine Taste drücken und loslassen; mit <c>+</c> gehaltene Umschalter davor, z. B. <c>ctrl+s</c>.</summary>
    private async Task Taste(string kombination)
    {
        var tasten = kombination.Split('+').Select(TasteZuKey).ToArray();
        var haupt = tasten[^1];
        var umschalter = tasten[..^1];
        foreach (var u in umschalter) TastenEreignis(u, true, umschalter);
        TastenEreignis(haupt, true, umschalter);
        await Bild();
        TastenEreignis(haupt, false, umschalter);
        foreach (var u in Enumerable.Reverse(umschalter)) TastenEreignis(u, false, umschalter);
        await Bild();
    }

    private static void TastenEreignis(Key key, bool gedrueckt, Key[] umschalter)
    {
        var ereignis = new InputEventKey
        {
            Keycode = key, PhysicalKeycode = key, Pressed = gedrueckt,
            CtrlPressed = umschalter.Contains(Key.Ctrl), ShiftPressed = umschalter.Contains(Key.Shift), AltPressed = umschalter.Contains(Key.Alt),
        };
        // Druckbare Zeichen auch als Text, damit Eingabefelder sie annehmen.
        if (gedrueckt && key is >= Key.Space and <= Key.Asciitilde && !ereignis.CtrlPressed && !ereignis.AltPressed)
            ereignis.Unicode = ereignis.ShiftPressed ? (char)key : char.ToLowerInvariant((char)key);
        Input.ParseInputEvent(ereignis);
    }

    private async Task Foto(string ausgabe, string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var bild = GetViewport().GetTexture().GetImage();
        var pfad = System.IO.Path.Combine(ausgabe, name + ".png");
        var fehler = bild.SavePng(pfad);
        Melden(fehler == Error.Ok
            ? $"Foto {pfad} ({bild.GetWidth()}×{bild.GetHeight()}, {Engine.GetFramesPerSecond():0} Bilder/s)"
            : $"Foto {pfad} nicht gespeichert: {fehler}");
    }
}
