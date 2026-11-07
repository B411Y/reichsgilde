using System.Text.Json;
using Godot;

namespace Reichsgilde;

/// <summary>Ein Abschnitt des Aufbaus: wie viel davor schon fertig ist (0–1) und was der Balken währenddessen sagt.</summary>
public readonly record struct Ladeschritt(float Anteil, string Text);

/// <summary>
/// Der Ladebildschirm vor einer neuen Partie, einem geladenen Spielstand und der Kartenwerkstatt: ein großer Holzschnitt füllt das Fenster
/// (Stadtansicht von Köln am Rhein mit Dom-Baustelle und Kran, aus <c>assets/oberflaeche/ladebilder/</c>; fehlt er, die Kartenvorschau der Stadt aus
/// <c>assets/oberflaeche/karten/</c>) in einem Holzrahmen; unten liegt der Tipp aus <c>res://daten/ladetipps.json</c> auf einer Pergamenttafel,
/// darunter der Fortschritt als Pergamentstreifen (<see cref="Ladebalken"/>) mit dem Abschnitt, der gerade läuft.
/// Er hängt an der Wurzel des Baums, nicht an einer Szene: Das Hauptmenü öffnet ihn, die Hauptszene bzw. Kartenwerkstatt baut sich dahinter
/// Abschnitt für Abschnitt auf (<see cref="Durchlaufen"/>, ein Abschnitt je Bild, damit er nicht einfriert) und schließt ihn erst, wenn die Stadt
/// vollständig steht. Während der Abschnitte zeichnet das Fenster keine 3D-Welt (<c>Viewport.Disable3D</c>), erst in den letzten Bildern
/// dahinter die fertige Stadt – so übersetzt der Grafiktreiber ihre Shader, solange das Bild noch steht.
/// </summary>
public partial class Ladebildschirm : CanvasLayer
{
    /// <summary>Der offene Ladebildschirm, oder null.</summary>
    public static Ladebildschirm? Aktuell { get; private set; }

    private const string TippDatei = "res://daten/ladetipps.json";
    private const double TippSekunden = 7;
    private const string BilderOrdner = "res://assets/oberflaeche/ladebilder/";

    /// <summary>Die Ladebilder (Dateinamen ohne Endung, 16 : 9); Quelle und Prompts: <c>docs/assets/prompts/ladebilder.md</c>.</summary>
    private static readonly string[] Bilder = { "koeln" };

    private static int _letztesBild = -1;

    /// <summary><c>--ladebildfotos=&lt;ordner&gt;</c>: speichert den Ladebildschirm bei 10, 50 und 90 % des Balkens als PNG (für Bilder im PR).</summary>
    private static readonly string? FotoOrdner = OS.GetCmdlineUserArgs().LastOrDefault(a => a.StartsWith("--ladebildfotos="))?["--ladebildfotos=".Length..];
    private static readonly float[] FotoStufen = { 0.1f, 0.5f, 0.9f };
    private int _foto;
    private bool _schliesst;

    private static readonly Color HolzRahmen = new(0.34f, 0.22f, 0.12f);

    private TextureRect _bild = null!;
    private Label _tipp = null!;
    private Ladebalken _balken = null!;
    private Control _wurzel = null!;
    private string[] _tipps = Array.Empty<string>();
    private int _tippNummer;
    private double _tippStart = -1;

    /// <summary>
    /// Öffnet den Ladebildschirm (oder gibt den offenen zurück) mit der Vorschau der Karte <paramref name="karte"/> als Bild.
    /// Er erscheint mit dem nächsten Bild; wer gleich danach lange rechnet, wartet vorher <see cref="Bild"/> ab.
    /// </summary>
    public static Ladebildschirm Zeigen(SceneTree baum, string? karte)
    {
        if (Aktuell is { } offen && IsInstanceValid(offen)) return offen;
        var lade = new Ladebildschirm { Layer = 120, Name = "Ladebildschirm" };
        lade.BildSetzen(karte);
        baum.Root.CallDeferred(Node.MethodName.AddChild, lade); // die Wurzel kann gerade eine Szene einhängen
        Aktuell = lade;
        return lade;
    }

    /// <summary>Wartet ein Bild ab: Was davor gesetzt wurde, ist danach zu sehen.</summary>
    public static async Task Bild(SceneTree baum) => await baum.ToSignal(baum, SceneTree.SignalName.ProcessFrame);

    public Ladebildschirm()
    {
        _wurzel = new Control { MouseFilter = Control.MouseFilterEnum.Stop }; // nichts dahinter ist klickbar
        _wurzel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_wurzel);
        var grund = new ColorRect { Color = new Color(0.09f, 0.075f, 0.06f), MouseFilter = Control.MouseFilterEnum.Ignore };
        grund.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _wurzel.AddChild(grund);
        // Das Bild füllt das Fenster (zugeschnitten, nie verzerrt); Tipp und Balken liegen als Pergament darüber
        _bild = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _bild.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _wurzel.AddChild(_bild);
        // Holzrahmen ums ganze Bild: außen Holz mit schwarzem Umriss, innen eine dünne Tintenlinie (Blatt im Rahmen)
        var rahmen = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        rahmen.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        rahmen.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Colors.Transparent, BorderColor = HolzRahmen, DrawCenter = false,
            BorderWidthLeft = RahmenBreite, BorderWidthTop = RahmenBreite, BorderWidthRight = RahmenBreite, BorderWidthBottom = RahmenBreite,
        });
        _wurzel.AddChild(rahmen);
        foreach (var (rand, farbe, breite) in new[] { (0, Ladebalken.Tinte, 3), (RahmenBreite - 3, Ladebalken.Tinte, 3) })
        {
            var linie = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
            linie.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            linie.OffsetLeft = linie.OffsetTop = rand;
            linie.OffsetRight = linie.OffsetBottom = -rand;
            linie.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = Colors.Transparent, BorderColor = farbe, DrawCenter = false,
                BorderWidthLeft = breite, BorderWidthTop = breite, BorderWidthRight = breite, BorderWidthBottom = breite,
            });
            _wurzel.AddChild(linie);
        }

        // Unten, mittig: der Rat als Pergamenttafel, darunter der Fortschritt
        var mitte = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        mitte.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        mitte.AddThemeConstantOverride("margin_bottom", RahmenBreite + 14);
        _wurzel.AddChild(mitte);
        var unten = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkEnd, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };
        unten.AddThemeConstantOverride("separation", 10);
        mitte.AddChild(unten);
        var tafel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        tafel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Ladebalken.Pergament, BorderColor = Ladebalken.Tinte,
            BorderWidthLeft = 3, BorderWidthTop = 3, BorderWidthRight = 3, BorderWidthBottom = 3,
            ContentMarginLeft = 22, ContentMarginTop = 12, ContentMarginRight = 22, ContentMarginBottom = 14,
        });
        unten.AddChild(tafel);
        var text = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        text.AddThemeConstantOverride("separation", 4);
        tafel.AddChild(text);
        var kopf = new Label { Text = "Ein Rat für unterwegs", HorizontalAlignment = HorizontalAlignment.Center };
        kopf.AddThemeFontOverride("font", Schriften.Ueberschrift);
        kopf.AddThemeFontSizeOverride("font_size", 22);
        kopf.AddThemeColorOverride("font_color", Ladebalken.Rost);
        text.AddChild(kopf);
        _tipp = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 58), VerticalAlignment = VerticalAlignment.Top,
        };
        _tipp.AddThemeFontOverride("font", Schriften.Text);
        _tipp.AddThemeFontSizeOverride("font_size", 20);
        _tipp.AddThemeColorOverride("font_color", Ladebalken.Tinte);
        text.AddChild(_tipp);
        _balken = new Ladebalken();
        unten.AddChild(_balken);
        _unten = unten;
    }

    private const int RahmenBreite = 14;
    private readonly VBoxContainer _unten;

    public override void _Ready()
    {
        _tipps = TippsLesen();
        _tippNummer = _tipps.Length > 0 ? (int)(GD.Randi() % (uint)_tipps.Length) : 0;
        TippZeigen();
        GetViewport().SizeChanged += Anpassen;
        Anpassen();
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= Anpassen;
        if (Aktuell == this) Aktuell = null;
    }

    /// <summary>Tafel und Balken folgen der Fenstergröße: bis 72 % der Breite, höchstens 900; das Bild füllt das Fenster.</summary>
    private void Anpassen()
    {
        var flaeche = GetViewport().GetVisibleRect().Size;
        var breite = Mathf.Clamp(flaeche.X * 0.72f, 420, 900);
        _balken.CustomMinimumSize = new Vector2(breite, 54);
        _tipp.CustomMinimumSize = new Vector2(breite - 44, 58);
        _unten.ResetSize();
    }

    /// <summary>Wählt ein Ladebild zufällig, nie dasselbe wie beim vorigen Ladevorgang; fehlen sie, die Kartenvorschau.</summary>
    private void BildSetzen(string? karte)
    {
        var nummer = (int)(GD.Randi() % (uint)Bilder.Length);
        if (Bilder.Length > 1 && nummer == _letztesBild) nummer = (nummer + 1 + (int)(GD.Randi() % (uint)(Bilder.Length - 1))) % Bilder.Length;
        var pfad = $"{BilderOrdner}{Bilder[nummer]}.webp";
        if (ResourceLoader.Exists(pfad))
        {
            _letztesBild = nummer;
            _bild.Texture = ResourceLoader.Load<Texture2D>(pfad);
            return;
        }
        pfad = $"res://assets/oberflaeche/karten/{karte}.png";
        if (karte is null || !ResourceLoader.Exists(pfad)) pfad = "res://assets/oberflaeche/karten/koeln.png";
        _bild.Texture = ResourceLoader.Exists(pfad) ? ResourceLoader.Load<Texture2D>(pfad) : null;
    }

    private static string[] TippsLesen()
    {
        try
        {
            var json = Godot.FileAccess.GetFileAsString(TippDatei);
            using var dokument = JsonDocument.Parse(json);
            return dokument.RootElement.GetProperty("tipps").EnumerateArray().Select(t => t.GetString() ?? "").Where(t => t != "").ToArray();
        }
        catch (Exception fehler) when (fehler is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            GD.PushError($"{TippDatei}: {fehler.Message}");
            return Array.Empty<string>();
        }
    }

    private void TippZeigen() => _tipp.Text = _tipps.Length > 0 ? _tipps[_tippNummer % _tipps.Length] : "";

    public override void _Process(double delta)
    {
        if (FotoOrdner is not null && !_schliesst && _foto < FotoStufen.Length && _balken.Gezeigt >= FotoStufen[_foto]) FotoMachen(FotoStufen[_foto++]);
        // Echte Zeit statt delta: während der Abschnitte (ein Bild dauert bis zu 5 s) blieb delta klein, der Tipp wechselte nie
        var jetzt = Time.GetTicksMsec() / 1000.0;
        if (_tippStart < 0) _tippStart = jetzt;
        if (jetzt - _tippStart < TippSekunden || _tipps.Length < 2) return;
        _tippStart = jetzt;
        _tippNummer++;
        TippZeigen();
    }

    private void FotoMachen(float stufe)
    {
        var pfad = $"{FotoOrdner}/ladebild_{(int)Math.Round(stufe * 100)}.png";
        RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePostDraw,
            Callable.From(() => GetViewport().GetTexture().GetImage().SavePng(pfad)), (uint)GodotObject.ConnectFlags.OneShot);
    }

    /// <summary>Setzt den Balken auf <paramref name="anteil"/> (0–1) und den Text des Abschnitts, der jetzt läuft.</summary>
    public void Fortschritt(float anteil, string text)
    {
        _balken.Ziel = Mathf.Clamp(anteil, 0, 1);
        _balken.Text = text;
        if (Ladezeit.An) GD.Print($"Ladezeit: Balken {anteil:0.000} „{text}“ bei {Time.GetTicksMsec()} ms"); // Eichung der Anteile: die Zeit bis zur nächsten Zeile ist der Abschnitt
    }

    /// <summary>
    /// Läuft die Abschnitte durch – je Abschnitt ein Bild, damit Balken und Text zu sehen sind, während er rechnet –, der Balken von
    /// <paramref name="von"/> bis <paramref name="bis"/>. Der Abschnitt rechnet beim nächsten <c>MoveNext</c>, sein Text steht schon da.
    /// </summary>
    public async Task Durchlaufen(IEnumerable<Ladeschritt> schritte, float von, float bis)
    {
        foreach (var schritt in schritte)
        {
            Fortschritt(von + (bis - von) * schritt.Anteil, schritt.Text);
            var uhr = Ladezeit.Uhr();
            await Bild(GetTree());
            Ladezeit.Dazu("Bild zwischen den Abschnitten", uhr);
        }
        Fortschritt(bis, _balken.Text);
    }

    /// <summary>Blendet den Ladebildschirm kurz aus und entfernt ihn.</summary>
    public void Schliessen()
    {
        if (Ladezeit.An) GD.Print($"Ladezeit: Balken 1.000 „Ladebild schließt“ bei {Time.GetTicksMsec()} ms");
        if (Aktuell == this) Aktuell = null;
        if (FotoOrdner is not null && _foto < FotoStufen.Length) FotoMachen(FotoStufen[_foto++]); // der Balken kommt oft nicht bis 90 %, bevor das Bild schließt
        _schliesst = true;
        _balken.Ziel = 1;
        _wurzel.MouseFilter = Control.MouseFilterEnum.Ignore;
        var blende = CreateTween();
        blende.TweenProperty(_wurzel, "modulate:a", 0f, 0.25f);
        blende.TweenCallback(Callable.From(QueueFree));
    }
}

/// <summary>
/// Der Fortschritt als Pergamentstreifen im Holzschnitt-Stil: heller Streifen mit eingerollten Enden und schwarzem Umriss, Schraffur im Schatten
/// unten, der gefüllte Teil in rötlicher Handfarbe, leicht neben seinem Umriss (wie aufgemalt), darauf der Text in Tinte mit einem Hof in Pergamentfarbe.
/// Der Balken läuft dem Ziel weich nach, aber nie darüber hinaus.
/// </summary>
public partial class Ladebalken : Control
{
    public static readonly Color Pergament = new(0.87f, 0.79f, 0.60f);
    public static readonly Color Tinte = new(0.10f, 0.07f, 0.05f);
    public static readonly Color Rost = new(0.55f, 0.20f, 0.10f);
    private static readonly Color Rolle = new(0.76f, 0.66f, 0.46f);
    private static readonly Color Handfarbe = new(0.70f, 0.30f, 0.17f, 0.62f);

    /// <summary>Wohin der Balken läuft (0–1).</summary>
    public float Ziel { get; set; }

    /// <summary>Der Text auf dem Streifen.</summary>
    public string Text
    {
        get => _text;
        set { if (_text != value) { _text = value; QueueRedraw(); } }
    }

    private string _text = "";
    private float _gezeigt;

    /// <summary>Wie weit der Balken gerade gefüllt gezeichnet wird (0–1).</summary>
    public float Gezeigt => _gezeigt;

    public Ladebalken() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Process(double delta)
    {
        // Lange Bilder während des Aufbaus: lieber ein Sprung als ein Balken, der hinterherhinkt
        var neu = Mathf.MoveToward(_gezeigt, Ziel, (float)Math.Max(delta, 1 / 60.0) * 1.2f);
        if (Math.Abs(neu - _gezeigt) < 0.0001f) return;
        _gezeigt = neu;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var b = Size.X;
        var h = Size.Y;
        const float rolle = 16;
        var (oben, unten) = (6f, h - 6);
        // Der Streifen: oben und unten leicht gewellt wie geschöpftes Papier
        var umriss = new List<Vector2>();
        for (var x = rolle; x <= b - rolle; x += 12) umriss.Add(new Vector2(x, oben + Welle(x, 1)));
        umriss.Add(new Vector2(b - rolle, oben));
        umriss.Add(new Vector2(b - rolle, unten));
        for (var x = b - rolle; x >= rolle; x -= 12) umriss.Add(new Vector2(x, unten + Welle(x, 2)));
        umriss.Add(new Vector2(rolle, unten));
        var punkte = umriss.ToArray();
        DrawColoredPolygon(punkte, Pergament);

        // Schraffur im Schatten: unteres Drittel, schräg
        var schattenOben = unten - (unten - oben) * 0.34f;
        var schraeg = unten - 1 - schattenOben;
        for (var x = rolle + 2 + schraeg; x < b - rolle - 2; x += 6)
            DrawLine(new Vector2(x - schraeg, unten - 1), new Vector2(x, schattenOben), new Color(Tinte, 0.28f), 1f);

        // Der gefüllte Teil: Handfarbe etwas versetzt, darüber der Umriss in Tinte
        var innenLinks = rolle + 6;
        var innenBreite = b - 2 * rolle - 12;
        var voll = innenBreite * _gezeigt;
        if (voll > 1)
        {
            var feld = new Rect2(innenLinks, oben + 6, voll, unten - oben - 12);
            DrawRect(new Rect2(feld.Position + new Vector2(3, 2), feld.Size), Handfarbe);
            DrawRect(feld, Tinte, false, 1.5f);
        }
        DrawPolyline(punkte.Append(punkte[0]).ToArray(), Tinte, 2.5f);

        // Eingerollte Enden
        foreach (var x in new[] { 0f, b - rolle * 2 + 4 })
        {
            var kasten = new Rect2(x + 2, 1, rolle * 2 - 8, h - 2);
            DrawRect(kasten, Rolle);
            DrawRect(kasten, Tinte, false, 2f);
            DrawLine(new Vector2(kasten.Position.X + kasten.Size.X * 0.5f, 4), new Vector2(kasten.Position.X + kasten.Size.X * 0.5f, h - 4), new Color(Tinte, 0.55f), 1f);
        }

        // Text in Tinte, mittig. Darunter ein Hof in Pergamentfarbe (Umriss der Schrift): Über der roten Füllung und der Schraffur
        // stünde die dunkle Tinte sonst schlecht lesbar – so liegt jeder Buchstabe auf „freiem Papier“, wie ausgespart beim Kolorieren.
        if (_text == "") return;
        var schrift = Schriften.Text;
        const int groesse = 20;
        var mass = schrift.GetStringSize(_text, HorizontalAlignment.Left, -1, groesse);
        var ort = new Vector2((b - mass.X) / 2, (h - mass.Y) / 2 + schrift.GetAscent(groesse));
        DrawStringOutline(schrift, ort, _text, HorizontalAlignment.Left, -1, groesse, 7, Pergament);
        DrawString(schrift, ort, _text, HorizontalAlignment.Left, -1, groesse, Tinte);
    }

    private static float Welle(float x, int saat) => 1.6f * Mathf.Sin(x * 0.043f + saat) + 0.9f * Mathf.Sin(x * 0.11f + saat * 2.3f);
}
