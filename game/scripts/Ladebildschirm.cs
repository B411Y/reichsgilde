using System.Text.Json;
using Godot;

namespace Reichsgilde;

/// <summary>Ein Abschnitt des Aufbaus: wie viel davor schon fertig ist (0–1) und was der Balken währenddessen sagt.</summary>
public readonly record struct Ladeschritt(float Anteil, string Text);

/// <summary>
/// Der Ladebildschirm vor einer neuen Partie, einem geladenen Spielstand und der Kartenwerkstatt: oben ein wechselnder Tipp aus
/// <c>res://daten/ladetipps.json</c>, in der Mitte ein quadratisches Holzschnitt-Bild in einer Tafel (Platzhalter: eines von acht Bildern aus
/// <c>assets/oberflaeche/ladebilder/</c>, je Ladevorgang zufällig, nie zweimal dasselbe hintereinander; fehlen sie, die Kartenvorschau der Stadt aus
/// <c>assets/oberflaeche/karten/</c>), darunter der Fortschritt als Pergamentstreifen (<see cref="Ladebalken"/>) mit dem Abschnitt, der gerade läuft.
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

    /// <summary>Die Holzschnitt-Platzhalter (Dateinamen ohne Endung); Quelle und Prompts: <c>docs/assets/prompts/ladebilder.md</c>.</summary>
    private static readonly string[] Bilder = { "schmied", "markt", "zimmerleute", "mahlzeit", "kaufmann", "stadtansicht", "ochsenkarren", "prediger" };

    private static int _letztesBild = -1;

    private static readonly Color HolzRahmen = new(0.34f, 0.22f, 0.12f);

    private TextureRect _bild = null!;
    private Label _tipp = null!;
    private Ladebalken _balken = null!;
    private Control _wurzel = null!;
    private string[] _tipps = Array.Empty<string>();
    private int _tippNummer;
    private double _tippZeit;

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
        var mitte = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        mitte.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _wurzel.AddChild(mitte);
        var spalte = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        spalte.AddThemeConstantOverride("separation", 18);
        mitte.AddChild(spalte);

        var kopf = new Label { Text = "Ein Rat für unterwegs", HorizontalAlignment = HorizontalAlignment.Center };
        kopf.AddThemeFontOverride("font", Schriften.Ueberschrift);
        kopf.AddThemeFontSizeOverride("font_size", 22);
        kopf.AddThemeColorOverride("font_color", Oberflaechenstil.Gold);
        spalte.AddChild(kopf);
        _tipp = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 58), VerticalAlignment = VerticalAlignment.Top,
        };
        _tipp.AddThemeFontSizeOverride("font_size", 20);
        _tipp.AddThemeColorOverride("font_color", Oberflaechenstil.Schrift);
        spalte.AddChild(_tipp);

        // Das Bild als Tafel: außen ein Holzrahmen mit schwarzem Umriss, darin ein Pergamentrand um den Holzschnitt (Blatt im Rahmen)
        var holz = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        holz.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = HolzRahmen, BorderColor = Ladebalken.Tinte,
            BorderWidthLeft = 3, BorderWidthTop = 3, BorderWidthRight = 3, BorderWidthBottom = 3,
            ContentMarginLeft = 12, ContentMarginTop = 12, ContentMarginRight = 12, ContentMarginBottom = 12,
        });
        spalte.AddChild(holz);
        var blatt = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        blatt.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Ladebalken.Pergament, BorderColor = Ladebalken.Tinte,
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            ContentMarginLeft = 6, ContentMarginTop = 6, ContentMarginRight = 6, ContentMarginBottom = 6,
        });
        holz.AddChild(blatt);
        _bild = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        blatt.AddChild(_bild);
        _balken = new Ladebalken();
        spalte.AddChild(_balken);
        _spalte = spalte;
    }

    private readonly VBoxContainer _spalte;

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

    /// <summary>
    /// Bild und Balken folgen der Fenstergröße: das Bild quadratisch (1 : 1, nie verzerrt), so groß es über Tipp und Balken passt;
    /// Tipp und Balken sind breiter als das Bild (bis 72 % des Fensters, höchstens 760), damit sie nicht zusammengedrückt wirken.
    /// </summary>
    private void Anpassen()
    {
        var flaeche = GetViewport().GetVisibleRect().Size;
        var seite = Mathf.Clamp(Mathf.Min(flaeche.X * 0.72f, flaeche.Y - 300), 240, 760);
        var breite = Mathf.Max(seite + 54, Mathf.Min(flaeche.X * 0.72f, 760));
        _bild.CustomMinimumSize = new Vector2(seite, seite);
        _balken.CustomMinimumSize = new Vector2(breite, 54);
        _tipp.CustomMinimumSize = new Vector2(breite - 20, 58);
        _spalte.ResetSize();
    }

    /// <summary>Wählt ein Holzschnitt-Bild zufällig, nie dasselbe wie beim vorigen Ladevorgang; fehlt es, die Kartenvorschau.</summary>
    private void BildSetzen(string? karte)
    {
        var nummer = (int)(GD.Randi() % (uint)Bilder.Length);
        if (nummer == _letztesBild) nummer = (nummer + 1 + (int)(GD.Randi() % (uint)(Bilder.Length - 1))) % Bilder.Length;
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
        _tippZeit += delta;
        if (_tippZeit < TippSekunden || _tipps.Length < 2) return;
        _tippZeit = 0;
        _tippNummer++;
        TippZeigen();
    }

    /// <summary>Setzt den Balken auf <paramref name="anteil"/> (0–1) und den Text des Abschnitts, der jetzt läuft.</summary>
    public void Fortschritt(float anteil, string text)
    {
        _balken.Ziel = Mathf.Clamp(anteil, 0, 1);
        _balken.Text = text;
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
        if (Aktuell == this) Aktuell = null;
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
