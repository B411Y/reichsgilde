using System.Diagnostics;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Schwarzblende beim Betreten und Verlassen eines Innenraums: eine schwarze Fläche auf einer <see cref="CanvasLayer"/> über allen Tafeln (Ebene 127,
/// unter dem Handschuh auf 128), gesteuert vom Zustandsautomaten <see cref="InnenraumUebergang"/> (Ablauf, Zeiten und Gründe dort).
/// Der Aufbau des Raums kommt als Liste kleiner Schritte (<see cref="Starten"/>): höchstens einer je Bild, damit das Schwarz nicht ruckelt;
/// ist die Liste leer, meldet die Blende den Raum als fertig und wartet noch <see cref="InnenraumUebergang.MindestBilder"/> gezeichnete Bilder ab.
/// Die Fläche fängt Mausklicks ab, solange ein Übergang läuft. Gilt in beiden Zeigerarten (2D und 3D); nur die Klinke ist 3D (<see cref="KlinkeStarter"/>).
/// </summary>
public partial class Uebergangsblende : CanvasLayer
{
    /// <summary>Haken für die Klinken-Animation des 3D-Handschuhs (Teil 3a): beginnt die Klinke-Folge der Hand und gibt zurück, nach wie vielen Sekunden die Blende beginnen soll.
    /// Solange nichts eingetragen ist (oder null zurückkommt), wird die Klinke übersprungen; sonst die Dauer in Sekunden bis zur Blende.</summary>
    public static Func<double?>? KlinkeStarter { get; set; }

    /// <summary>Haken: die Hand lässt die Klinke los (der Raum ist offen oder das Betreten wurde abgebrochen).</summary>
    public static Action? KlinkeLoslasser { get; set; }
    private bool _klinkeGespielt;

    /// <summary>Aus mit <c>--blende=0</c> (Klicktests ohne Wartezeit).</summary>
    public static bool Aktiv { get; } = !OS.GetCmdlineUserArgs().Contains("--blende=0");

    /// <summary>Log der Schritte mit Zeiten (<c>--innenmessung</c>).</summary>
    public static bool Messen { get; } = OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--innenmessung="));

    public InnenraumUebergang Ablauf { get; } = new();
    private ColorRect _flaeche = null!;
    private readonly Queue<(string Name, Action Tat)> _schritte = new();

    /// <summary>Ein Übergang läuft (Eingaben in die Stadt sind gesperrt).</summary>
    public bool Laufend => Ablauf.Laufend;

    /// <summary>Irgendeine Blende läuft gerade (für den Handschuh: kein Winken).</summary>
    public static bool Laeuft { get; private set; }

    public Uebergangsblende()
    {
        Layer = 127;
        Name = "Uebergangsblende";
        ProcessMode = ProcessModeEnum.Always;
        ProcessPriority = -100; // vor den Anzeigen: dieselbe Reihenfolge jedes Bild
    }

    public override void _Ready()
    {
        _flaeche = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false, FocusMode = Control.FocusModeEnum.None };
        _flaeche.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_flaeche);
    }

    /// <summary>Beginnt den Übergang; <paramref name="schritte"/> sind die Aufbauschritte im Schwarzen (je Bild einer).</summary>
    public bool Starten(UebergangRichtung richtung, string? ziel, IEnumerable<(string, Action)> schritte, bool klinke = false)
    {
        var klinkeDauer = klinke ? KlinkeStarter?.Invoke() : null;
        var klinkeDa = klinkeDauer is not null;
        if (!Ablauf.Starten(richtung, ziel, klinkeDa, klinkeDauer ?? InnenraumUebergang.KlinkeSekunden)) { if (klinkeDa) KlinkeLoslasser?.Invoke(); return false; }
        _klinkeGespielt = klinkeDa;
        _schritte.Clear();
        foreach (var s in schritte) _schritte.Enqueue(s);
        _flaeche.MouseFilter = Control.MouseFilterEnum.Stop;
        _flaeche.Visible = true;
        return true;
    }

    /// <summary>Gezeichnete Bilder; ohne Fenster (headless) zeichnet nichts, dann zählen die Bilder der Spielschleife.</summary>
    public static long Gezeichnet() => DisplayServer.GetName() == "headless" ? (long)Engine.GetProcessFrames() : (long)Engine.GetFramesDrawn();

    public override void _Process(double delta)
    {
        var gezeichnet = Gezeichnet();
        if (Ablauf.Phase == UebergangPhase.Laden)
        {
            if (_schritte.Count > 0)
            {
                var (name, tat) = _schritte.Dequeue();
                var uhr = Stopwatch.StartNew();
                tat();
                if (Messen) GD.Print($"[innenmessung] Schritt {name}: {uhr.Elapsed.TotalMilliseconds:0.0} ms");
            }
            if (_schritte.Count == 0) Ablauf.LadenFertig(gezeichnet);
        }
        Ablauf.Schritt(delta, gezeichnet);
        Laeuft = Ablauf.Laufend;
        if (_klinkeGespielt && Ablauf.Phase is UebergangPhase.BlendeEin or UebergangPhase.Ruhe or UebergangPhase.Fertig) { _klinkeGespielt = false; KlinkeLoslasser?.Invoke(); } // der Raum ist offen: die Hand lässt die Klinke los
        _flaeche.Color = new Color(0, 0, 0, (float)Ablauf.Alpha);
        var sichtbar = Ablauf.Laufend || Ablauf.Alpha > 0.001;
        _flaeche.Visible = sichtbar;
        _flaeche.MouseFilter = Ablauf.Laufend ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
    }
}
