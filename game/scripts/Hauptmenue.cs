using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Das Hauptmenü: Neue Partie (mit Einstellungen), Partie laden, Beenden.
/// Im Hintergrund steht die Stadt, die Kamera dreht sich langsam um sie herum.
///
/// Wird das Spiel mit Startoptionen aufgerufen (z. B. „-- --tempo=8"), geht es ohne Menü direkt
/// in die Stadt – für Tests und die automatische Prüfung.
/// </summary>
public partial class Hauptmenue : Node
{
    private const string Spielszene = "res://scenes/Hauptszene.tscn";
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);

    private PanelContainer _start = null!;
    private PanelContainer _neu = null!;
    private PanelContainer _laden = null!;
    private SpielstandListe _liste = null!;
    private Label _ladeFehler = null!;

    // Felder des Dialogs „Neue Partie"
    private LineEdit _familie = null!;
    private OptionButton _volk = null!;
    private Label _volkHinweis = null!;
    private OptionButton _geschlecht = null!;
    private LineEdit _vorname = null!;
    private OptionButton _sieg = null!;
    private SpinBox _endjahr = null!;
    private SpinBox _zielvermoegen = null!;
    private SpinBox _dauer = null!;
    private OptionButton _statut = null!;
    private SpinBox _startvermoegen = null!;
    private SpinBox _aktionspunkte = null!;
    private OptionButton _beruf = null!;
    private OptionButton _rivalen = null!;
    private OptionButton _rivale = null!;
    private SpinBox _startwert = null!;
    private Label _fehler = null!;
    private Control _endjahrZeile = null!;
    private Control _zielZeile = null!;

    public override void _Ready()
    {
        if (OS.GetCmdlineUserArgs().Length > 0)
        {
            // Startoptionen → Test- oder Prüflauf: Menü überspringen.
            CallDeferred(MethodName.SpielStarten);
            return;
        }

        var stadt = GetNode<Stadtansicht>("%Stadt");
        var drehen = new Godot.Timer { WaitTime = 9, Autostart = true };
        drehen.Timeout += () => stadt.Drehen(1);
        AddChild(drehen);

        var oberflaeche = new CanvasLayer();
        AddChild(oberflaeche);
        var mitte = Mitte();
        oberflaeche.AddChild(mitte);
        _start = StartTafel();
        _neu = NeuTafel();
        _laden = LadeTafel();
        foreach (var tafel in new[] { _start, _neu, _laden })
            mitte.AddChild(tafel);
        Zeigen(_start);
    }

    private void SpielStarten() => GetTree().ChangeSceneToFile(Spielszene);

    private void Zeigen(PanelContainer tafel)
    {
        foreach (var t in new[] { _start, _neu, _laden })
            t.Visible = t == tafel;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Starttafel
    // ═════════════════════════════════════════════════════════════════════════

    private PanelContainer StartTafel()
    {
        var (tafel, spalte) = Tafel(420);
        Text(spalte, "Reichsgilde", 40, Gold, zentriert: true);
        Text(spalte, "Handel, Handwerk und Ränke im Heiligen Römischen Reich", 15, null, zentriert: true);
        spalte.AddChild(new HSeparator());

        Knopf(spalte, "Neue Partie", () => Zeigen(_neu));
        Knopf(spalte, "Partie laden", () => { _liste.Auffrischen(); _ladeFehler.Text = ""; Zeigen(_laden); });
        Knopf(spalte, "Vollbild / Fenster (F11)", Fenster.Umschalten);
        Knopf(spalte, "Beenden", () => GetTree().Quit());

        Text(spalte, "Hobby- und Lernprojekt · Open Source (MIT) · Greybox-Grafik", 11, new Color(0.6f, 0.6f, 0.6f), zentriert: true);
        return tafel;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Neue Partie
    // ═════════════════════════════════════════════════════════════════════════

    private PanelContainer NeuTafel()
    {
        var (tafel, spalte) = Tafel(560);
        Text(spalte, "Neue Partie", 26, Gold, zentriert: true);

        var raster = new GridContainer { Columns = 2 };
        raster.AddThemeConstantOverride("h_separation", 16);
        raster.AddThemeConstantOverride("v_separation", 8);
        spalte.AddChild(raster);

        _familie = new LineEdit { Text = "Familie Hammerschmid", CustomMinimumSize = new Vector2(280, 0) };
        Zeile(raster, "Name eures Hauses", _familie);

        _volk = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (var volk in Spielbare)
            _volk.AddItem(volk.Mehrzahl(), (int)volk);
        _volk.ItemSelected += _ => VolkHinweisZeigen();
        Zeile(raster, "Volk", _volk);
        _volkHinweis = new Label { CustomMinimumSize = new Vector2(280, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _volkHinweis.AddThemeFontSizeOverride("font_size", 12);
        _volkHinweis.AddThemeColorOverride("font_color", new Color(0.72f, 0.72f, 0.72f));
        Zeile(raster, "", _volkHinweis);

        var oberhaupt = new HBoxContainer();
        oberhaupt.AddThemeConstantOverride("separation", 8);
        _geschlecht = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        _geschlecht.AddItem("Patriarch", (int)Geschlecht.Mann);
        _geschlecht.AddItem("Matriarchin", (int)Geschlecht.Frau);
        oberhaupt.AddChild(_geschlecht);
        _vorname = new LineEdit { PlaceholderText = "Vorname (zufällig)", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        oberhaupt.AddChild(_vorname);
        Zeile(raster, "Oberhaupt", oberhaupt);

        _beruf = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        _beruf.AddItem("Schmied (Gießerei: Beschlag, Rapier, Silberring)");
        _beruf.AddItem("Tischler (Tischlerei: Holzzapfen, Schemel, Truhe)");
        _beruf.AddItem("Steinmetz (Maurerei: Schleifziegel, Grabstein, Mühlstein)");
        _beruf.AddItem("Alchimist (Tinkturei: Farbe, Kräuterschnaps, Heilsalbe)");
        _beruf.AddItem("Wirt (Schänke: Gäste bewirten, Dünnbier, Körnermahlzeit)");
        _beruf.AddItem("Prediger (Kirche: Messe und Ablass, Kerzen und Schriften)");
        _beruf.AddItem("Dieb (Schmugglerloch: Taschendiebe, Einbruch, Schutzgeld)");
        _beruf.AddItem("Gardist (Wachhaus: Sold, Streife, Razzia, Kopfgeld)");
        _beruf.AddItem("Geldleiher (Wechselstube: Kasse, Wechsel, Kredite an Häuser)");
        Zeile(raster, "Startberuf", _beruf);
        VolkHinweisZeigen();

        _sieg = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (var (text, wert) in new[] { ("Partie endet in einem Jahr", Siegbedingung.Endjahr), ("Zielvermögen erreichen", Siegbedingung.Zielvermoegen),
                                             ("Letzte Dynastie gewinnt", Siegbedingung.LetzteDynastie), ("Endlos spielen", Siegbedingung.Endlos) })
            _sieg.AddItem(text, (int)wert);
        _sieg.ItemSelected += _ => SiegFelderZeigen();
        Zeile(raster, "Siegbedingung", _sieg);

        _endjahr = Zahl(Spielzeit.StartJahr + 1, 2000, 1, 1600);
        _endjahrZeile = Zeile(raster, "Endjahr", _endjahr);
        _zielvermoegen = Zahl(10_000, 10_000_000, 10_000, 200_000, " Gulden");
        _zielZeile = Zeile(raster, "Zielvermögen", _zielvermoegen);

        _dauer = Zahl(1, 120, 1, 15, " Min.");
        Zeile(raster, "Dauer eines Spieljahres", _dauer);

        _statut = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (var statut in Enum.GetValues<Fronstatut>())
            _statut.AddItem($"{statut.Kuerzel()} – {statut.Name()}", (int)statut);
        _statut.Select(_statut.GetItemIndex((int)Fronstatut.Bestienfron));
        Zeile(raster, "Fron- und Sklavenstatut", _statut);

        _startvermoegen = Zahl(1000, 50_000, 500, 4000, " Gulden");
        Zeile(raster, "Startvermögen", _startvermoegen);

        _aktionspunkte = Zahl(Partieeinstellungen.MinAktionspunkte, Partieeinstellungen.MaxAktionspunkte, 1, Partieeinstellungen.Standard.AktionspunkteProRunde, " AP");
        _aktionspunkte.TooltipText = "So viele Aktionspunkte bekommt jedes Haus zum Jahreswechsel – für Werben, Rat, Gericht, Hinterzimmer und Üben.";
        Zeile(raster, "Aktionspunkte je Jahr", _aktionspunkte);

        _rivale = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        _rivale.AddItem("Vorsichtig (leichter)", (int)KiCharakter.Vorsichtig);
        _rivale.AddItem("Ausgewogen", (int)KiCharakter.Ausgewogen);
        _rivale.AddItem("Wagemutig (schwerer)", (int)KiCharakter.Wagemutig);
        _rivale.Select(1);
        Zeile(raster, "Die KI-Häuser spielen", _rivale);

        _rivalen = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        _rivalen.AddItem("Haus Eisenbart (Schmiede)", 1);
        _rivalen.AddItem("Eisenbart und Eichwald (Tischler)", 2);
        _rivalen.AddItem("Eisenbart, Eichwald und Kieselbach (Steinmetz)", 3);
        _rivalen.Select(2);
        Zeile(raster, "Rivalen", _rivalen);

        var startwertZeile = new HBoxContainer();
        _startwert = Zahl(0, 999_999, 1, 1400);
        startwertZeile.AddChild(_startwert);
        var wuerfeln = new Button { Text = "Würfeln", FocusMode = Control.FocusModeEnum.None };
        wuerfeln.Pressed += () => _startwert.Value = GD.RandRange(0, 999_999);
        startwertZeile.AddChild(wuerfeln);
        Zeile(raster, "Zufallsstartwert", startwertZeile);

        Text(spalte, "Gleicher Startwert und gleiche Entscheidungen ergeben dieselbe Partie.", 12, new Color(0.65f, 0.65f, 0.65f), umbruchBreite: 500);
        _fehler = Text(spalte, "", 14, Rot, umbruchBreite: 500);

        var knoepfe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        knoepfe.AddThemeConstantOverride("separation", 12);
        spalte.AddChild(knoepfe);
        Knopf(knoepfe, "Zurück", () => Zeigen(_start));
        Knopf(knoepfe, "Partie beginnen", NeuePartieBeginnen);

        SiegFelderZeigen();
        return tafel;
    }

    private static readonly Volk[] Spielbare = { Volk.Mensch, Volk.Zwerg, Volk.Elb, Volk.Halbling };

    /// <summary>Was die Wahl des Volkes für die Dynastie bedeutet (die Zahlen stehen in wirtschaft.json).</summary>
    private void VolkHinweisZeigen() => _volkHinweis.Text = (Volk)_volk.GetSelectedId() switch
    {
        Volk.Zwerg => "Leben lange und bekommen selten Kinder, die erst mit 30 mündig sind. Hochzeiten mit Zwergen sind teuer. Haus Eisenbart sind Zwerge.",
        Volk.Elb => "Leben sehr lange, bekommen aber nur selten Kinder, die erst mit 45 mündig sind. Ihr Haushalt ist teuer, doch sie werben geschickt.",
        Volk.Halbling => "Bekommen schnell Kinder, leben aber etwas kürzer. Ihr Haushalt ist günstig.",
        _ => "Bekommen schnell Kinder und leben durchschnittlich lang. Sie stellen die Mehrheit in Stadt und Rat.",
    };

    /// <summary>Endjahr bzw. Zielvermögen nur zeigen, wenn die gewählte Siegbedingung sie braucht.</summary>
    private void SiegFelderZeigen()
    {
        var sieg = (Siegbedingung)_sieg.GetSelectedId();
        ZeileZeigen(_endjahrZeile, sieg == Siegbedingung.Endjahr);
        ZeileZeigen(_zielZeile, sieg == Siegbedingung.Zielvermoegen);
    }

    private static void ZeileZeigen(Control feld, bool sichtbar)
    {
        feld.Visible = sichtbar;
        feld.GetMeta("label").As<Label>().Visible = sichtbar;
    }

    private void NeuePartieBeginnen()
    {
        var einstellungen = Partieeinstellungen.Standard with
        {
            Siegbedingung = (Siegbedingung)_sieg.GetSelectedId(),
            Endjahr = (int)_endjahr.Value,
            Zielvermoegen = (long)_zielvermoegen.Value,
            RundenDauerMinuten = _dauer.Value,
            AktionspunkteProRunde = (int)_aktionspunkte.Value,
            StartStatut = (Fronstatut)_statut.GetSelectedId(),
            Zufallsstartwert = (int)_startwert.Value,
        };
        var fehler = einstellungen.Pruefen().ToList();
        if (Partieaufbau.Rivalennamen.Contains(_familie.Text.Trim()))
            fehler.Add($"„{_familie.Text.Trim()}“ ist der Name eines Rivalen – wählt bitte einen anderen.");
        if (fehler.Count > 0)
        {
            _fehler.Text = string.Join("\n", fehler);
            return;
        }

        Partiewahl.NeuePartieWaehlen(einstellungen,
            new NeuePartie(_familie.Text, _startvermoegen.Value, (KiCharakter)_rivale.GetSelectedId(),
                (Volk)_volk.GetSelectedId(), (Geschlecht)_geschlecht.GetSelectedId(),
                string.IsNullOrWhiteSpace(_vorname.Text) ? null : _vorname.Text.Trim(),
                Beruf: _beruf.Selected switch { 1 => "Tischler", 2 => "Steinmetz", 3 => "Alchimist", 4 => "Wirt", 5 => "Prediger", 6 => "Dieb", 7 => "Gardist", 8 => "Geldleiher", _ => "Schmied" },
                Rivalen: _rivalen.GetSelectedId()));
        SpielStarten();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Laden
    // ═════════════════════════════════════════════════════════════════════════

    private PanelContainer LadeTafel()
    {
        var (tafel, spalte) = Tafel(520);
        Text(spalte, "Partie laden", 26, Gold, zentriert: true);
        _liste = new SpielstandListe();
        spalte.AddChild(_liste);
        _liste.Gewaehlt += pfad =>
        {
            // Vorab prüfen, damit ein kaputter Spielstand hier gemeldet wird und nicht erst in der Stadt.
            try
            {
                Spielstaende.Laden(pfad);
                Partiewahl.LadenWaehlen(pfad);
                SpielStarten();
            }
            catch (SpielstandFehler fehler)
            {
                _ladeFehler.Text = fehler.Message;
            }
        };
        _ladeFehler = Text(spalte, "", 14, Rot, umbruchBreite: 460);
        Knopf(spalte, "Zurück", () => Zeigen(_start));
        return tafel;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Bausteine
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ein Behälter über den ganzen Bildschirm, der seinen Inhalt immer mittig hält – auch wenn der
    /// Inhalt wächst. Er fängt keine Mausklicks ab (MouseFilter Ignore), die Stadt darunter bleibt bedienbar.
    /// </summary>
    internal static CenterContainer Mitte()
    {
        var mitte = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        mitte.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return mitte;
    }

    /// <summary>Eine deckende Tafel mit Rand und einer Spalte für den Inhalt.</summary>
    internal static (PanelContainer Tafel, VBoxContainer Spalte) Tafel(int breite)
    {
        var tafel = new PanelContainer { CustomMinimumSize = new Vector2(breite, 0) };
        tafel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.11f, 0.10f, 0.96f),
            BorderColor = Gold * new Color(1, 1, 1, 0.8f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ShadowColor = new Color(0, 0, 0, 0.5f), ShadowSize = 12,
        });
        var rand = new MarginContainer();
        foreach (var seite in new[] { "left", "right", "top", "bottom" })
            rand.AddThemeConstantOverride($"margin_{seite}", 26);
        tafel.AddChild(rand);
        var spalte = new VBoxContainer();
        spalte.AddThemeConstantOverride("separation", 12);
        rand.AddChild(spalte);
        Rollbereich.Einhuellen(tafel);
        return (tafel, spalte);
    }

    internal static Button Knopf(Container eltern, string text, Action aktion)
    {
        var knopf = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(200, 40) };
        knopf.AddThemeFontSizeOverride("font_size", 17);
        knopf.Pressed += aktion;
        eltern.AddChild(knopf);
        return knopf;
    }

    /// <param name="umbruchBreite">Breite in Pixeln, ab der umgebrochen wird. Ohne feste Breite kennt Godot beim
    /// ersten Aufbau die Breite noch nicht und bräche nach jedem Buchstaben um.</param>
    internal static Label Text(Container eltern, string text, int groesse, Color? farbe = null, bool zentriert = false, int umbruchBreite = 0)
    {
        var label = new Label { Text = text };
        if (umbruchBreite > 0)
        {
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(umbruchBreite, 0);
        }
        label.AddThemeFontSizeOverride("font_size", groesse);
        if (farbe is { } f) label.AddThemeColorOverride("font_color", f);
        if (zentriert) label.HorizontalAlignment = HorizontalAlignment.Center;
        eltern.AddChild(label);
        return label;
    }

    private static Control Zeile(GridContainer raster, string beschriftung, Control feld)
    {
        var label = new Label { Text = beschriftung };
        label.AddThemeFontSizeOverride("font_size", 15);
        raster.AddChild(label);
        feld.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        raster.AddChild(feld);
        feld.SetMeta("label", label); // damit ein ausgeblendetes Feld seine Beschriftung mitnimmt
        return feld;
    }

    private static SpinBox Zahl(double min, double max, double schritt, double wert, string einheit = "") =>
        new Zahlfeld { MinValue = min, MaxValue = max, Step = schritt, Value = wert, Suffix = einheit.Trim(), Rounded = true };
}
