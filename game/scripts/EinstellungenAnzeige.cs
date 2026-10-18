using Godot;

namespace Reichsgilde;

/// <summary>
/// Fenster „Einstellungen“: Vollbild, Kantenglättung, Schattenqualität und Bloom. Erreichbar aus dem Hauptmenü und aus dem Spielmenü (Esc).
/// Jede Änderung wirkt sofort und wird gespeichert (siehe <see cref="Fenster"/>).
/// </summary>
public sealed class EinstellungenAnzeige
{
    /// <summary>Anzeigename und kurze Erklärung je Verfahren – in der Reihenfolge der Auswahlliste.</summary>
    private static readonly (Kantenglaettung Art, string Name, string Erklaerung)[] Verfahren =
    {
        (Kantenglaettung.Aus, "Aus", "Keine Glättung. Am schnellsten, aber Kanten sind gezackt."),
        (Kantenglaettung.Fxaa, "FXAA", "Sehr schnell. Glättet das fertige Bild, macht es dabei leicht weich."),
        (Kantenglaettung.Smaa, "SMAA (empfohlen)", "Schnell. Sucht Kanten im fertigen Bild und glättet sie, das Bild bleibt scharf."),
        (Kantenglaettung.Taa, "TAA", "Schnell und sehr glatt. Nutzt die letzten Bilder mit, bewegte Dinge ziehen leichte Schlieren."),
        (Kantenglaettung.Msaa2, "MSAA 2×", "Berechnet jede Kante mehrfach: beste Kanten ohne Unschärfe. Kostet mehr Grafikleistung."),
        (Kantenglaettung.Msaa4, "MSAA 4×", "Wie MSAA 2×, noch glatter. Kostet deutlich mehr Grafikleistung."),
        (Kantenglaettung.Msaa8, "MSAA 8×", "Höchste Stufe. Nur für starke Grafikkarten."),
    };

    /// <summary>Anzeigename und Erklärung je Stufe der Schattenqualität – in der Reihenfolge der Auswahlliste (Werte: Stadtansicht.Licht.cs).</summary>
    private static readonly (Schattenqualitaet Stufe, string Name, string Erklaerung)[] Schattenstufen =
    {
        (Schattenqualitaet.Niedrig, "Niedrig", "Kleine Schattenkarte, wenig Proben. Für schwache Grafikkarten; Schattenkanten sind weicher und gröber."),
        (Schattenqualitaet.Mittel, "Mittel", "Halbe Auflösung der Schattenkarte. Saubere Schatten, spart Grafikspeicher und Leistung."),
        (Schattenqualitaet.Hoch, "Hoch (empfohlen)", "Große Schattenkarte, feiner Filter: scharfe, glatte Schatten bis an den Fuß jeder Figur."),
    };

    public PanelContainer Tafel { get; }
    private readonly CheckButton _vollbild;
    private readonly OptionButton _kanten;
    private readonly Label _erklaerung;
    private readonly OptionButton _schatten;
    private readonly Label _schattenErklaerung;
    private readonly CheckButton _bloom;
    private readonly HSlider _bloomRegler;
    private readonly Label _bloomWert;

    public EinstellungenAnzeige(Action zurueck)
    {
        var (tafel, spalte) = Hauptmenue.Tafel(460);
        Tafel = tafel;
        Hauptmenue.Text(spalte, "Einstellungen", 26, new Color(0.95f, 0.82f, 0.45f), zentriert: true);
        spalte.AddChild(new HSeparator());

        Hauptmenue.Text(spalte, "Anzeige", 18);
        _vollbild = new CheckButton { Text = "Vollbild (F11)", FocusMode = Control.FocusModeEnum.None };
        _vollbild.AddThemeFontSizeOverride("font_size", 15);
        _vollbild.Toggled += an => { if (an != Fenster.IstVollbild) Fenster.VollbildSetzen(an); };
        spalte.AddChild(_vollbild);

        Hauptmenue.Text(spalte, "Kantenglättung", 18);
        _kanten = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (var (_, name, _) in Verfahren) _kanten.AddItem(name);
        _kanten.ItemSelected += nummer =>
        {
            Fenster.KantenglaettungSetzen(Verfahren[nummer].Art);
            _erklaerung!.Text = Verfahren[nummer].Erklaerung;
        };
        spalte.AddChild(_kanten);
        _erklaerung = Hauptmenue.Text(spalte, "", 13, new Color(0.75f, 0.75f, 0.75f), umbruchBreite: 400);

        Hauptmenue.Text(spalte, "Schattenqualität", 18);
        _schatten = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (var (_, name, _) in Schattenstufen) _schatten.AddItem(name);
        _schatten.ItemSelected += nummer =>
        {
            Fenster.SchattenqualitaetSetzen(Schattenstufen[nummer].Stufe);
            _schattenErklaerung!.Text = Schattenstufen[nummer].Erklaerung;
        };
        spalte.AddChild(_schatten);
        _schattenErklaerung = Hauptmenue.Text(spalte, "", 13, new Color(0.75f, 0.75f, 0.75f), umbruchBreite: 400);

        Hauptmenue.Text(spalte, "Bloom", 18);
        _bloom = new CheckButton { Text = "Lichtschein (Bloom)", FocusMode = Control.FocusModeEnum.None };
        _bloom.AddThemeFontSizeOverride("font_size", 15);
        _bloom.Toggled += an => Fenster.BloomSetzen(an, Fenster.BloomStaerke);
        spalte.AddChild(_bloom);
        var reglerZeile = new HBoxContainer();
        reglerZeile.AddThemeConstantOverride("separation", 10);
        spalte.AddChild(reglerZeile);
        Hauptmenue.Text(reglerZeile, "Stärke", 15);
        _bloomRegler = new HSlider
        {
            MinValue = 0, MaxValue = 100, Step = 5, FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(200, 0),
        };
        _bloomRegler.ValueChanged += wert =>
        {
            _bloomWert!.Text = $"{(int)wert} %";
            Fenster.BloomSetzen(Fenster.BloomAn, (int)wert);
        };
        reglerZeile.AddChild(_bloomRegler);
        _bloomWert = Hauptmenue.Text(reglerZeile, "", 15);
        _bloomWert.CustomMinimumSize = new Vector2(50, 0);
        Hauptmenue.Text(spalte, "Heller Schein um Fensterlicht, Glut und Domfenster. Sonnenbeschienene Flächen bleiben klar. 50 % ist das empfohlene Bild; kostet etwas Grafikleistung.",
            13, new Color(0.75f, 0.75f, 0.75f), umbruchBreite: 400);

        spalte.AddChild(new HSeparator());
        Hauptmenue.Knopf(spalte, "Zurück", zurueck);
        Auffrischen();
    }

    /// <summary>Zeigt die aktuellen Werte – Vollbild kann sich inzwischen per F11 geändert haben.</summary>
    public void Auffrischen()
    {
        _vollbild.SetPressedNoSignal(Fenster.IstVollbild);
        var nummer = Array.FindIndex(Verfahren, v => v.Art == Fenster.AktuelleKantenglaettung);
        _kanten.Select(nummer);
        _erklaerung.Text = Verfahren[nummer].Erklaerung;
        var stufe = Array.FindIndex(Schattenstufen, s => s.Stufe == Fenster.AktuelleSchattenqualitaet);
        _schatten.Select(stufe);
        _schattenErklaerung.Text = Schattenstufen[stufe].Erklaerung;
        _bloom.SetPressedNoSignal(Fenster.BloomAn);
        _bloomRegler.SetValueNoSignal(Fenster.BloomStaerke);
        _bloomWert.Text = $"{Fenster.BloomStaerke} %";
    }
}
