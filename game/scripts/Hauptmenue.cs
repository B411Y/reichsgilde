using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Das Hauptmenü: Neue Partie (Stadt wählen, Oberhaupt, Einstellungen), Partie laden, Einstellungen, Kartenwerkstatt (Level-Editor), Beenden.
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
    private PanelContainer _oberhaupt = null!;
    private PanelContainer _laden = null!;
    private EinstellungenAnzeige _einstellungen = null!;
    private DanksagungAnzeige _danksagung = null!;
    private SpielstandListe _liste = null!;
    private Label _ladeFehler = null!;

    // Felder des Dialogs „Neue Partie"
    private LineEdit _familie = null!;
    private Wappen _wappen = Wappen.Vorlagen[0];
    private WappenBild _wappenVorschau = null!;
    private Label _wappenText = null!;
    private OptionButton _farbe1 = null!;
    private OptionButton _farbe2 = null!;
    private readonly List<Button> _vorlagenKnoepfe = new();
    private OptionButton _volk = null!;
    // Seite „Euer Oberhaupt" (PR #211): Volk, Geschlecht, Gesicht, Haar und Augen mit großem Porträt
    private PortraetAnzeige _portraet = null!;
    private readonly List<(Button Knopf, PortraetAnzeige Bild)> _gesichter = new();
    private HBoxContainer _haarWahl = null!;
    private HBoxContainer _augenWahl = null!;
    private Label _aussehenText = null!;
    private int _gesicht;
    private Haarfarbe _haar = Aussehenspalette.Haarfarben(Volk.Mensch)[0];
    private Augenfarbe _augen = Aussehenspalette.Augenfarben(Volk.Mensch)[0];
    private Label _volkHinweis = null!;
    private OptionButton _glaube = null!;
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
    private OptionButton _hofstelle = null!;
    private SpinBox _kiAnzahl = null!;
    private OptionButton _kiStaerke = null!;
    private OptionButton _rivale = null!;
    private OptionButton _schwierigkeit = null!;
    private Label _schwierigkeitHinweis = null!;
    private SpinBox _startwert = null!;
    private Label _fehler = null!;
    private Control _endjahrZeile = null!;
    private Control _zielZeile = null!;

    public override void _Ready()
    {
        Ladezeit.Marke("Programmstart bis Hauptmenü");
        Datenvorrat.Vorladen(); // wirtschaft.json im Hintergrund, solange das Menü steht (Datenvorrat.cs)
        var portraetOption = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--portraets="));
        var stadtwahl = OS.GetCmdlineUserArgs().Any(a => a == "--stadtwahl=1");
        var neuePartie = OS.GetCmdlineUserArgs().Any(a => a == "--neuepartie=1");
        var danksagung = OS.GetCmdlineUserArgs().Any(a => a is "--danksagung=1" or "--danksagung=lizenzen");
        var feuertest = Feuertest.Angefordert; // auch ohne „--“ davor erkannt (Feuertest.Startargumente)
        if ((feuertest || KlicktestTreiber.SpielOptionen().Length > 0) && !_startoptionenGenutzt && portraetOption is null && !stadtwahl && !neuePartie && !danksagung)
        {
            // Startoptionen → Test- oder Prüflauf: Menü überspringen (nur beim ersten Mal – „Zurück zum Hauptmenü“ zeigt dann das Menü).
            _startoptionenGenutzt = true;
            // Die Greybox hinter dem Menü sähe niemand (spart rund 2,5 s). --menuhintergrund=1 baut sie trotzdem: wie der Weg über das Menü,
            // das dabei Modelle und Shader schon lädt – für Vergleiche der Ladezeit (docs/STARTOPTIONEN.md).
            GetNode<Stadtansicht>("%Stadt").OhneHintergrund = !OS.GetCmdlineUserArgs().Contains("--menuhintergrund=1");
            if (feuertest)
                Callable.From(FeuertestOeffnen).CallDeferred();
            else if (OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--kartenwerkstatt")))
                CallDeferred(MethodName.WerkstattOeffnen);
            else if (OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--wassertest")))
                Callable.From(() => GetTree().ChangeSceneToFile(Wassertest.Szene)).CallDeferred();
            else if (Felstest.Angefordert)
                Callable.From(() => GetTree().ChangeSceneToFile(Felstest.Szene)).CallDeferred();
            else if (Rauchtest.Angefordert)
                Callable.From(() => GetTree().ChangeSceneToFile(Rauchtest.Szene)).CallDeferred();
            else
                CallDeferred(MethodName.SpielStarten);
            return;
        }

        Musikspieler.MenueSetzen(true); // ein ruhiges Stück im Hauptmenü (Musikspieler.cs); nur im echten Menü, nicht bei Startoptionen; beim Verlassen blendet es aus
        TreeExiting += () => Musikspieler.MenueSetzen(false);
        var stadt = GetNode<Stadtansicht>("%Stadt");
        var drehen = new Godot.Timer { WaitTime = 9, Autostart = true };
        drehen.Timeout += () => stadt.Drehen(1);
        AddChild(drehen);
        AddChild(new Mauszeiger()); // Lederhandschuh, Stufe 1 (Mauszeiger.cs)

        var oberflaeche = new CanvasLayer();
        AddChild(oberflaeche);
        var mitte = Mitte();
        oberflaeche.AddChild(mitte);
        _start = StartTafel();
        _kartenwahl = KartenwahlTafel();
        _neu = NeuTafel();       // zuerst: Volk, Glaube und Beruf liegen hier, das Oberhaupt-Porträt liest sie
        _oberhaupt = OberhauptTafel();
        _laden = LadeTafel();
        _einstellungen = new EinstellungenAnzeige(() => Zeigen(_start));
        _danksagung = new DanksagungAnzeige(() => Zeigen(_start));
        foreach (var tafel in new[] { _start, _kartenwahl, _oberhaupt, _neu, _laden, _einstellungen.Tafel, _danksagung.Tafel })
            mitte.AddChild(tafel);
        oberflaeche.AddChild(EntwicklerHaken());
        Zeigen(_start);
        if (stadtwahl) // --stadtwahl=1: gleich die Seite „Stadt wählen“ (für Bildschirmfotos; mit --entwickler=1 auch mit Testkarten)
        {
            _startoptionenGenutzt = true;
            KartenwahlZeigen();
        }
        if (neuePartie) // --neuepartie=1: gleich die Seite „Neue Partie“ (für Bildschirmfotos)
        {
            _startoptionenGenutzt = true;
            NeuePartieOeffnen();
            Zeigen(_neu);
        }
        if (danksagung) // für Bildschirmfotos
        {
            _startoptionenGenutzt = true;
            DanksagungZeigen();
            if (OS.GetCmdlineUserArgs().Contains("--danksagung=lizenzen")) _danksagung.LizenzenZeigen();
        }
        if (portraetOption is not null)
        {
            _startoptionenGenutzt = true;
            PortraetOptionAnwenden(portraetOption["--portraets=".Length..]);
            // --blinzeln=zu|offen|<0..1>: die Augen für das Bildschirmfoto festhalten (sonst blinzelt das Bild zufällig)
            var blinzeln = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--blinzeln="));
            if (blinzeln is not null) _portraet.BlinzelnFesthalten(blinzeln.EndsWith("zu") ? 1f : float.TryParse(blinzeln["--blinzeln=".Length..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var teil) ? Math.Clamp(teil, 0f, 1f) : 0f);
            Zeigen(_oberhaupt);
        }
    }

    /// <summary>Zur Partie: erst der Ladebildschirm (zwei Bilder, damit er steht), dann die Hauptszene, die sich dahinter aufbaut.</summary>
    private async void SpielStarten()
    {
        Ladezeit.Marke("Hauptmenü bis Spielstart");
        await SzeneHinterLadebild(Spielszene, Stadtkarten.StartKennung);
    }

    /// <summary>Zur Kartenwerkstatt, ebenfalls hinter dem Ladebildschirm.</summary>
    private async void WerkstattOeffnen()
    {
        Ladezeit.Marke("Hauptmenü bis Kartenwerkstatt");
        var wahl = OS.GetCmdlineUserArgs().LastOrDefault(a => a.StartsWith("--kartenwerkstatt="))?["--kartenwerkstatt=".Length..];
        await SzeneHinterLadebild(Kartenwerkstatt.Szene, wahl);
    }

    /// <summary>
    /// Zur Testszene „Feuer und Flammen“ (<c>--feuertest=1</c>), ohne Ladebildschirm: Er hängt an der Wurzel und schaltet die 3D-Welt ab, bis
    /// Hauptszene oder Kartenwerkstatt ihn schließen – über der Testszene bliebe er stehen.
    /// Lädt sie nicht, steht das im Protokoll (Ausgabefenster des Editors), statt dass nur ein leeres Fenster bleibt.
    /// </summary>
    private void FeuertestOeffnen()
    {
        var fehler = GetTree().ChangeSceneToFile(Feuertest.Szene);
        if (fehler != Error.Ok)
            GD.PushError($"[feuer] Die Testszene {Feuertest.Szene} ließ sich nicht laden ({fehler}). Ist das Projekt gebaut (Hammer oben rechts)?");
    }

    private async Task SzeneHinterLadebild(string szene, string? karte)
    {
        var baum = GetTree();
        Ladebildschirm.Zeigen(baum, karte).Fortschritt(0.02f, "Die Reise beginnt …");
        await Ladebildschirm.Bild(baum);
        await Ladebildschirm.Bild(baum);
        baum.ChangeSceneToFile(szene);
    }

    /// <summary>Die Startoptionen haben das Menü schon einmal übersprungen.</summary>
    private static bool _startoptionenGenutzt;

    private void Zeigen(PanelContainer tafel)
    {
        foreach (var t in new[] { _start, _kartenwahl, _oberhaupt, _neu, _laden, _einstellungen.Tafel, _danksagung.Tafel })
            t.Visible = t == tafel;
        _entwicklerHaken.Visible = tafel == _start;
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

        Knopf(spalte, "Neue Partie", KartenwahlZeigen);
        Knopf(spalte, "Partie laden", () => { _liste.Auffrischen(); _ladeFehler.Text = ""; Zeigen(_laden); });
        Knopf(spalte, "Einstellungen", () => { _einstellungen.Auffrischen(); Zeigen(_einstellungen.Tafel); });
        Knopf(spalte, "Kartenwerkstatt", WerkstattOeffnen).TooltipText =
            "Karten bauen und prüfen: Gebäude setzen, verschieben, drehen – Greybox und Köln öffnen (nur lesen), eigene Karten speichern";
        Knopf(spalte, "Danksagung", DanksagungZeigen).TooltipText = "Wer am Spiel mitgewirkt hat und welches fremde Material in ihm steckt, mit Lizenzen";
        Knopf(spalte, "Beenden", () => GetTree().Quit());

        Text(spalte, "Hobby- und Lernprojekt · Open Source (MIT) · Greybox-Grafik", 11, new Color(0.6f, 0.6f, 0.6f), zentriert: true);
        return tafel;
    }

    private void DanksagungZeigen()
    {
        _danksagung.Oeffnen();
        Zeigen(_danksagung.Tafel);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Euer Oberhaupt (Porträt, PR #211)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Seite „Euer Oberhaupt“ (von „Neue Partie“ aus): Geschlecht und Vorname, Gesicht (5 zur Wahl), Haar- und Augenfarbe, mit großem Porträt; das Volk wählt „Neue Partie“.</summary>
    private PanelContainer OberhauptTafel()
    {
        var (tafel, spalte) = Tafel(700);
        Text(spalte, "Euer Oberhaupt", 26, Gold, zentriert: true);

        var reihe = new HBoxContainer();
        reihe.AddThemeConstantOverride("separation", 24);
        spalte.AddChild(reihe);

        var links = new VBoxContainer();
        links.AddThemeConstantOverride("separation", 6);
        reihe.AddChild(links);
        _portraet = PortraetAnzeige.Neu(PortraetAnzeige.Gross);
        links.AddChild(_portraet);
        _aussehenText = Text(links, "", 12, new Color(0.72f, 0.72f, 0.72f), zentriert: true, umbruchBreite: PortraetAnzeige.Gross);

        var rechts = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        rechts.AddThemeConstantOverride("separation", 8);
        reihe.AddChild(rechts);

        Text(rechts, "Oberhaupt", 14, Gold);
        var oberhaupt = new HBoxContainer();
        oberhaupt.AddThemeConstantOverride("separation", 8);
        rechts.AddChild(oberhaupt);
        _geschlecht = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        _geschlecht.AddItem("Patriarch", (int)Geschlecht.Mann);
        _geschlecht.AddItem("Matriarchin", (int)Geschlecht.Frau);
        _geschlecht.ItemSelected += _ => AussehenZeigen();
        oberhaupt.AddChild(_geschlecht);
        _vorname = new LineEdit { PlaceholderText = "Vorname (zufällig)", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        oberhaupt.AddChild(_vorname);

        Text(rechts, "Gesicht", 14, Gold);
        var gesichter = new HBoxContainer();
        gesichter.AddThemeConstantOverride("separation", 6);
        rechts.AddChild(gesichter);
        var gruppe = new ButtonGroup();
        for (var i = 0; i < Aussehenspalette.Gesichter; i++)
        {
            var nummer = i;
            var knopf = new Button
            {
                ToggleMode = true, ButtonGroup = gruppe, FocusMode = Control.FocusModeEnum.None, ButtonPressed = i == _gesicht,
                CustomMinimumSize = new Vector2(PortraetAnzeige.Klein + 8, PortraetAnzeige.Klein + 8), TooltipText = $"Gesicht {i + 1}",
            };
            var bild = PortraetAnzeige.Neu(PortraetAnzeige.Klein, lebendig: false);
            bild.MouseFilter = Control.MouseFilterEnum.Ignore;
            bild.CustomMinimumSize = Vector2.Zero;
            knopf.AddChild(bild);
            bild.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, 4);
            knopf.Pressed += () => { _gesicht = nummer; AussehenZeigen(); };
            gesichter.AddChild(knopf);
            _gesichter.Add((knopf, bild));
        }

        Text(rechts, "Haar", 14, Gold);
        _haarWahl = new HBoxContainer();
        _haarWahl.AddThemeConstantOverride("separation", 5);
        rechts.AddChild(_haarWahl);
        Text(rechts, "Augen", 14, Gold);
        _augenWahl = new HBoxContainer();
        _augenWahl.AddThemeConstantOverride("separation", 5);
        rechts.AddChild(_augenWahl);

        var knoepfe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        knoepfe.AddThemeConstantOverride("separation", 12);
        spalte.AddChild(knoepfe);
        Knopf(knoepfe, "Zurück", () => Zeigen(_neu));
        Knopf(knoepfe, "Fertig", () => Zeigen(_neu));

        PaletteFuellen();
        AussehenZeigen();
        return tafel;
    }

    /// <summary>Die Farbfelder für Haar und Augen des gewählten Volkes (nur seine Palette); gewählt bleibt die Farbe, wenn sie auch dort vorkommt.</summary>
    private void PaletteFuellen()
    {
        var volk = (Volk)_volk.GetSelectedId();
        var haare = Aussehenspalette.Haarfarben(volk);
        var augen = Aussehenspalette.Augenfarben(volk);
        if (!haare.Contains(_haar)) _haar = haare[0];
        if (!augen.Contains(_augen)) _augen = augen[0];
        UiHilfe.KinderEntfernen(_haarWahl);
        UiHilfe.KinderEntfernen(_augenWahl);
        var haarGruppe = new ButtonGroup();
        foreach (var h in haare)
        {
            var farbe = h;
            FarbFeld(_haarWahl, haarGruppe, PortraetAnzeige.Farbe(h), h == Haarfarbe.Kahl ? "kahl" : PortraetAnzeige.FarbName(h), h == _haar,
                () => { _haar = farbe; AussehenZeigen(); });
        }
        var augenGruppe = new ButtonGroup();
        foreach (var a in augen)
        {
            var farbe = a;
            FarbFeld(_augenWahl, augenGruppe, PortraetAnzeige.Farbe(a), PortraetAnzeige.FarbName(a), a == _augen,
                () => { _augen = farbe; AussehenZeigen(); });
        }
    }

    private static void FarbFeld(Container eltern, ButtonGroup gruppe, Color farbe, string name, bool gewaehlt, Action gewaehltAktion)
    {
        StyleBoxFlat Kasten(Color rand, int breite) => new()
        {
            BgColor = farbe, BorderColor = rand,
            BorderWidthLeft = breite, BorderWidthTop = breite, BorderWidthRight = breite, BorderWidthBottom = breite,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
        };
        var knopf = new Button
        {
            ToggleMode = true, ButtonGroup = gruppe, FocusMode = Control.FocusModeEnum.None, ButtonPressed = gewaehlt,
            CustomMinimumSize = new Vector2(34, 34), TooltipText = name,
        };
        var dunkel = new Color(0.05f, 0.04f, 0.04f);
        knopf.AddThemeStyleboxOverride("normal", Kasten(dunkel, 1));
        knopf.AddThemeStyleboxOverride("hover", Kasten(new Color(0.8f, 0.75f, 0.6f), 2));
        knopf.AddThemeStyleboxOverride("pressed", Kasten(Gold, 3));
        knopf.AddThemeStyleboxOverride("hover_pressed", Kasten(Gold, 3));
        knopf.Pressed += gewaehltAktion;
        eltern.AddChild(knopf);
    }

    private Aussehen GewaehltesAussehen => new(_gesicht, _haar, _augen);

    /// <summary>Zeigt die Wahl im großen Porträt und in den fünf Gesichtern (jedes mit der gewählten Haar- und Augenfarbe).</summary>
    private void AussehenZeigen()
    {
        var volk = (Volk)_volk.GetSelectedId();
        var geschlecht = (Geschlecht)_geschlecht.GetSelectedId();
        _portraet.Zeigen(volk, geschlecht, GewaehltesAussehen, Altersstufe.Mittel, Kleidung.Schlicht, Standgruppe.Gemeiner);
        for (var i = 0; i < _gesichter.Count; i++)
        {
            _gesichter[i].Bild.Zeigen(volk, geschlecht, new Aussehen(i, _haar, _augen), Altersstufe.Mittel, Kleidung.Schlicht, Standgruppe.Gemeiner);
            _gesichter[i].Knopf.SetPressedNoSignal(i == _gesicht);
        }
        _aussehenText.Text = $"{volk.Einzahl()} · Haar {(_haar == Haarfarbe.Kahl ? "kahl" : PortraetAnzeige.FarbName(_haar))} · Augen {PortraetAnzeige.FarbName(_augen)}";
    }

    /// <summary>Startoption <c>--portraets=&lt;Volk&gt;[,&lt;mann|frau&gt;[,&lt;Gesicht&gt;[,&lt;Haar&gt;[,&lt;Augen&gt;]]]]</c> – wählt vor (<c>1</c> = Mensch, Mann, Gesicht 1); für Bildschirmfotos.</summary>
    private void PortraetOptionAnwenden(string wert)
    {
        var teile = wert.Split(',');
        if (!char.IsDigit(teile[0].FirstOrDefault()) && Enum.TryParse<Volk>(teile[0], true, out var volk) && Spielbare.Contains(volk))
            _volk.Select(_volk.GetItemIndex((int)volk));
        if (teile.Length > 1) _geschlecht.Select(teile[1].StartsWith("f", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
        if (teile.Length > 2 && int.TryParse(teile[2], out var gesicht)) _gesicht = Math.Clamp(gesicht, 0, Aussehenspalette.Gesichter - 1);
        var gewaehltesVolk = (Volk)_volk.GetSelectedId();
        if (teile.Length > 3 && Enum.TryParse<Haarfarbe>(teile[3], true, out var haar) && Aussehenspalette.Haarfarben(gewaehltesVolk).Contains(haar)) _haar = haar;
        if (teile.Length > 4 && Enum.TryParse<Augenfarbe>(teile[4], true, out var augen) && Aussehenspalette.Augenfarben(gewaehltesVolk).Contains(augen)) _augen = augen;
        VolkHinweisZeigen();
        GlaubeFuellen();
        PaletteFuellen();
        AussehenZeigen();
        ZuegeZeigen();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Neue Partie
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// „Neue Partie“ (Entscheidung 11.11.2026): oben, was der Spieler einstellt – Wappen, Name, Volk, Glaube, Beruf (vorbelegt mit einem gewürfelten
    /// Vorschlag des Kerns), seine drei Persönlichkeitszüge (immer gewürfelt, „Neu würfeln“) – darunter die KI-Gegner (Anzahl, Stärke). „Erweitert“
    /// klappt auf: jede KI einzeln, KI-Häuser mit Umland und alle übrigen Einstellungen (Hauptmenue.Besetzung.cs).
    /// </summary>
    private PanelContainer NeuTafel()
    {
        var (tafel, spalte) = Tafel(720);
        Text(spalte, "Neue Partie", 26, Gold, zentriert: true);
        spalte.AddChild(WappenWahl());
        spalte.AddChild(new HSeparator());

        var raster = new GridContainer { Columns = 2 };
        raster.AddThemeConstantOverride("h_separation", 16);
        raster.AddThemeConstantOverride("v_separation", 8);
        spalte.AddChild(raster);

        // Nur der Name – das Spiel schreibt überall „Haus …“ davor, wie bei den KI-Häusern.
        var hausname = new HBoxContainer();
        hausname.AddThemeConstantOverride("separation", 6);
        hausname.AddChild(new Label { Text = "Haus" });
        _familie = new LineEdit { Text = "Hammerschmid", PlaceholderText = "Hammerschmid", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        hausname.AddChild(_familie);
        hausname.CustomMinimumSize = new Vector2(280, 0);
        Zeile(raster, "Name eures Hauses", hausname);

        _volk = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (var volk in Spielbare)
            _volk.AddItem(volk.Mehrzahl(), (int)volk);
        _volk.ItemSelected += _ => { VolkHinweisZeigen(); GlaubeFuellen(); PaletteFuellen(); AussehenZeigen(); ZuegeZeigen(); };
        Zeile(raster, "Volk", _volk);
        _volkHinweis = new Label { CustomMinimumSize = new Vector2(330, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _volkHinweis.AddThemeFontSizeOverride("font_size", 12);
        _volkHinweis.AddThemeColorOverride("font_color", new Color(0.72f, 0.72f, 0.72f));
        Zeile(raster, "", _volkHinweis);

        _glaube = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        Zeile(raster, "Glaube", _glaube);

        _beruf = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        Zeile(raster, "Startberuf", _beruf);

        // Die drei Persönlichkeitszüge des Oberhaupts: immer gewürfelt, nie gewählt.
        _zuegeReihe = new HFlowContainer();
        _zuegeReihe.AddThemeConstantOverride("h_separation", 10);
        var zuegeBox = new HBoxContainer();
        zuegeBox.AddThemeConstantOverride("separation", 12);
        zuegeBox.AddChild(_zuegeReihe);
        _zuegeReihe.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _zuegeNeu = new Button { Text = "Neu würfeln", FocusMode = Control.FocusModeEnum.None, TooltipText = "Würfelt drei andere Persönlichkeitszüge. Gewählt werden sie nicht." };
        _zuegeNeu.Pressed += ZuegeWuerfelnKlick;
        zuegeBox.AddChild(_zuegeNeu);
        _zuegeZeile = Zeile(raster, "Persönlichkeit", zuegeBox);

        Knopf(spalte, "Oberhaupt gestalten …", () => Zeigen(_oberhaupt)).TooltipText = "Geschlecht, Vorname, Gesicht, Haar und Augen des Oberhaupts";

        spalte.AddChild(new HSeparator());
        var ki = new GridContainer { Columns = 2 };
        ki.AddThemeConstantOverride("h_separation", 16);
        ki.AddThemeConstantOverride("v_separation", 8);
        spalte.AddChild(ki);
        _kiAnzahl = Zahl(0, 1, 1, 0);
        _kiAnzahl.TooltipText = "Wie viele KI-Häuser mitspielen – null bis zur Zahl der Höfe der Karte minus eins. Beruf, Volk, Glaube, Name und Wappen werden gewürfelt (jeder Beruf kommt nur einmal vor), es sei denn, ihr legt sie unter „Erweitert“ fest.";
        _kiAnzahl.ValueChanged += _ => KiZeilenZeigen();
        Zeile(ki, "KI-Gegner", _kiAnzahl);
        _kiStaerke = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        _kiStaerke.AddItem("Leicht", (int)KiStaerke.Leicht);
        _kiStaerke.AddItem("Normal", (int)KiStaerke.Normal);
        _kiStaerke.AddItem("Schwer", (int)KiStaerke.Schwer);
        _kiStaerke.Select(1);
        _kiStaerke.ItemSelected += _ => KiStaerkeHinweisZeigen();
        Zeile(ki, "Stärke der KI", _kiStaerke);
        _kiStaerkeHinweis = new Label { CustomMinimumSize = new Vector2(330, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _kiStaerkeHinweis.AddThemeFontSizeOverride("font_size", 12);
        _kiStaerkeHinweis.AddThemeColorOverride("font_color", new Color(0.72f, 0.72f, 0.72f));
        Zeile(ki, "", _kiStaerkeHinweis);
        KiStaerkeHinweisZeigen();

        _erweitertKnopf = Knopf(spalte, "Erweitert anzeigen", ErweitertUmschalten);
        _erweitertKnopf.TooltipText = "Jede KI einzeln festlegen, KI-Häuser mit Umland, Siegbedingung, Dauer, Startvermögen und mehr";
        _erweitert = ErweitertBauen();
        _erweitert.Visible = false;
        spalte.AddChild(_erweitert);

        _fehler = Text(spalte, "", 14, Rot, umbruchBreite: 640);

        var knoepfe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        knoepfe.AddThemeConstantOverride("separation", 12);
        spalte.AddChild(knoepfe);
        Knopf(knoepfe, "Zurück", () => Zeigen(_kartenwahl));
        Knopf(knoepfe, "Partie beginnen", NeuePartieBeginnen);

        GlaubeFuellen();
        SiegFelderZeigen();
        return tafel;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Wappen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Vorschau links, rechts die Vorlagen, darunter die beiden Farben und „Würfeln“.</summary>
    private Control WappenWahl()
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 14);
        _wappenVorschau = WappenBild.Neu(_wappen, 110);
        zeile.AddChild(_wappenVorschau);

        var rechts = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        rechts.AddThemeConstantOverride("separation", 6);
        zeile.AddChild(rechts);

        var raster = new GridContainer { Columns = 6 };
        raster.AddThemeConstantOverride("h_separation", 4);
        raster.AddThemeConstantOverride("v_separation", 4);
        rechts.AddChild(raster);
        var gruppe = new ButtonGroup();
        foreach (var vorlage in Wappen.Vorlagen)
        {
            var knopf = new Button
            {
                ToggleMode = true, ButtonGroup = gruppe, FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(46, 50), TooltipText = vorlage.Beschreibung(),
                ButtonPressed = vorlage == _wappen,
            };
            var bild = WappenBild.Neu(vorlage, 40);
            bild.MouseFilter = Control.MouseFilterEnum.Ignore;
            bild.CustomMinimumSize = Vector2.Zero;
            knopf.AddChild(bild);
            bild.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, 3);
            knopf.Pressed += () => { _wappen = vorlage; WappenZeigen(); };
            raster.AddChild(knopf);
            _vorlagenKnoepfe.Add(knopf);
        }

        var farben = new HBoxContainer();
        farben.AddThemeConstantOverride("separation", 6);
        rechts.AddChild(farben);
        farben.AddChild(new Label { Text = "Grund" });
        _farbe1 = Farbwahl(t => _wappen = _wappen with { Farbe1 = t });
        farben.AddChild(_farbe1);
        farben.AddChild(new Label { Text = "Figur" });
        _farbe2 = Farbwahl(t => _wappen = _wappen with { Farbe2 = t });
        farben.AddChild(_farbe2);
        var wuerfeln = new Button { Text = "Würfeln", FocusMode = Control.FocusModeEnum.None, TooltipText = "Ein zufälliges Wappen" };
        wuerfeln.Pressed += () =>
        {
            _wappen = Wappen.Zufall(new Random());
            for (var i = 0; i < _vorlagenKnoepfe.Count; i++)
                _vorlagenKnoepfe[i].SetPressedNoSignal(Wappen.Vorlagen[i] == _wappen);
            WappenZeigen();
        };
        farben.AddChild(wuerfeln);

        _wappenText = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(300, 0) };
        _wappenText.AddThemeFontSizeOverride("font_size", 12);
        rechts.AddChild(_wappenText);
        WappenZeigen();
        return zeile;
    }

    /// <summary>Auswahl einer Tinktur mit Farbfeld davor.</summary>
    private OptionButton Farbwahl(Action<Tinktur> gewaehlt)
    {
        var wahl = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (var t in Enum.GetValues<Tinktur>())
        {
            var bild = Image.CreateEmpty(14, 14, false, Image.Format.Rgb8);
            bild.Fill(WappenBild.Farbe(t));
            wahl.AddIconItem(ImageTexture.CreateFromImage(bild), Wappen.Name(t), (int)t);
        }
        wahl.ItemSelected += index => { gewaehlt((Tinktur)wahl.GetItemId((int)index)); WappenZeigen(); };
        return wahl;
    }

    private void WappenZeigen()
    {
        _wappenVorschau.Wappen = _wappen;
        KiWappenSperren(); // das Wappen des Spielers ist für die KI-Zeilen vergeben
        _farbe1.Select(_farbe1.GetItemIndex((int)_wappen.Farbe1));
        _farbe2.Select(_farbe2.GetItemIndex((int)_wappen.Farbe2));
        var problem = WappenProblem();
        var tipp = problem is null && Wappen.IstMetall(_wappen.Farbe1) == Wappen.IstMetall(_wappen.Farbe2)
            ? " Tipp der Herolde: Metall (Gold, Silber) auf Farbe ist von weitem besser zu erkennen." : "";
        _wappenText.Text = problem ?? _wappen.Beschreibung() + tipp;
        _wappenText.AddThemeColorOverride("font_color", problem is null ? new Color(0.75f, 0.72f, 0.65f) : Rot);
    }

    /// <summary>Wer am Start in der Stadt lebt: die Belegung der Karte, mit der eine neue Partie beginnt (ohne Karte die eingebaute).</summary>
    private static Startbelegung Startbelegung => Stadtkarten.Startkarte?.Startbelegung ?? Startbelegung.Ersatz;

    /// <summary>
    /// Die Wahl der Hofstelle: „Zufall“ (Vorgabe) und je Start-Hofstelle der gewählten Karte ein Eintrag „Heumarkt – 120 m zum Heumarkt“.
    /// Karten ohne Hofstellen: die Zeile verschwindet. Der Weg wird beim Wechsel der Karte einmal gerechnet (Köln einige 10 ms).
    /// </summary>
    private void HofstellenWahlFuellen()
    {
        _hofstelle.Clear();
        _hofstelle.AddItem("Zufall", 0);
        _hofstelle.SetItemMetadata(0, "");
        var karte = Stadtkarten.Startkarte;
        if (karte is { HatHofstellen: true })
        {
            var start = karte.Hofstellen.Where(h => h.Start).Select(h => h.Kennung).ToHashSet();
            foreach (var a in Hofstellenvergabe.Angebot(karte).Where(a => start.Contains(a.Kennung)))
            {
                var text = a.Meter is { } m && a.Markt is { } markt ? $"{a.Name} – {Math.Round(m):0} m zum {markt}" : a.Name;
                _hofstelle.AddItem(text);
                _hofstelle.SetItemMetadata(_hofstelle.ItemCount - 1, a.Kennung);
            }
        }
        _hofstelle.Select(0);
        ZeileZeigen(_hofstelle, _hofstelle.ItemCount > 1);
    }

    /// <summary>Warum dieses Wappen nicht geht – oder null.</summary>
    private string? WappenProblem()
    {
        if (_wappen.Fehler() is { } fehler) return fehler;
        if (Startbelegung.Rivalenwappen.Contains(_wappen))
            return "Dieses Wappen führt schon ein KI-Haus – ändert eine Farbe.";
        return null;
    }

    private static readonly Volk[] Spielbare = { Volk.Mensch, Volk.Zwerg, Volk.Elb, Volk.Halbling };

    /// <summary>Was die Wahl des Volkes für die Dynastie bedeutet (die Zahlen stehen in wirtschaft.json).</summary>
    private void VolkHinweisZeigen()
    {
        var volk = (Volk)_volk.GetSelectedId();
        var text = volk switch
        {
            Volk.Zwerg => "Leben lange und bekommen selten Kinder, die erst mit 30 mündig sind. Hochzeiten mit Zwergen sind teuer. Haus Eisenbart sind Zwerge.",
            Volk.Elb => "Leben sehr lange, bekommen aber nur selten Kinder, die erst mit 45 mündig sind. Ihr Haushalt ist teuer, doch sie werben geschickt.",
            Volk.Halbling => "Bekommen schnell Kinder, leben aber etwas kürzer. Ihr Haushalt ist günstig.",
            _ => "Bekommen schnell Kinder und leben durchschnittlich lang. Sie stellen die Mehrheit in Stadt und Rat.",
        };
        // Völkerboni und Startglaube stehen in wirtschaft.json („berufe…voelker", „glaube") – hier nur als Hinweis.
        text += " " + volk switch
        {
            Volk.Zwerg => "Berufe: Schmied, Steinmetz +3 %, Geldleiher +2 %; Parfumeur −3 %, Dieb −2 %.",
            Volk.Elb => "Berufe: Alchimist, Prediger, Parfumeur +3 %; Steinmetz −3 %, Gardist −2 %.",
            Volk.Halbling => "Berufe: Wirt +3 %, Dieb +2 %; Schmied −3 %, Gardist −2 %.",
            _ => "Berufe: Tischler +3 %, Gardist +2 %, keine Schwächen.",
        };
        var glaube = volk is Volk.Zwerg or Volk.Halbling ? "meist evangelisch" : "meist katholisch";
        _volkHinweis.Text = $"{text} Glaube: {glaube} – im Rat schätzen Ratsherren Glaubensbrüder (7 katholisch, 6 evangelisch).";
    }

    /// <summary>Was die gewählte Schwierigkeit bedeutet – eine Zeile unter der Auswahl.</summary>
    private void SchwierigkeitHinweisZeigen() =>
        _schwierigkeitHinweis.Text = (Schwierigkeit)_schwierigkeit.GetSelectedId() switch
        {
            Schwierigkeit.Schwer => "Die KI-Häuser starten mit Vorsprung: Werkstatt und Umlandbetrieb (Grube, Holzschlag, Steinbruch).",
            _ => "Alle Häuser starten gleich: eine Werkstatt mit einem Lehrling. Die Umlandbetriebe der KI stehen frei zur Pacht.",
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
            Schwierigkeit = (Schwierigkeit)_schwierigkeit.GetSelectedId(),
            Zufallsstartwert = (int)_startwert.Value,
        };
        var fehler = einstellungen.Pruefen().ToList();
        var hausname = Partieaufbau.Hausname(_familie.Text);
        if (Startbelegung.Rivalennamen.Contains(hausname))
            fehler.Add($"„{hausname}“ ist der Name eines Rivalen – wählt bitte einen anderen.");
        if (WappenProblem() is { } wappenFehler)
            fehler.Add(wappenFehler);
        if (Aussehenspalette.Verboten((Volk)_volk.GetSelectedId(), GewaehltesAussehen) is { } aussehenFehler)
            fehler.Add(aussehenFehler);
        var vorgaben = KiVorgabenLesen();
        if (BesetzungProblem(einstellungen.Zufallsstartwert, vorgaben) is { } besetzungFehler)
            fehler.Add(besetzungFehler);
        if (fehler.Count > 0)
        {
            _fehler.Text = string.Join("\n", fehler);
            return;
        }

        Partiewahl.NeuePartieWaehlen(einstellungen,
            new NeuePartie(_familie.Text, _startvermoegen.Value, (KiCharakter)_rivale.GetSelectedId(),
                (Volk)_volk.GetSelectedId(), (Geschlecht)_geschlecht.GetSelectedId(),
                string.IsNullOrWhiteSpace(_vorname.Text) ? null : _vorname.Text.Trim(),
                Beruf: GewaehlterBeruf(_beruf)!,
                Rivalen: (int)_kiAnzahl.Value,
                Hofstelle: _hofstelle.Selected > 0 ? _hofstelle.GetItemMetadata(_hofstelle.Selected).AsString() : null,
                Glaube: (Konfession)_glaube.GetSelectedId(),
                Wappen: _wappen, Aussehen: GewaehltesAussehen,
                KiStaerke: (KiStaerke)_kiStaerke.GetSelectedId(), KiVorgaben: vorgaben, ZugWurf: _zugWurf));
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
        label.MouseFilter = Control.MouseFilterEnum.Pass; // sonst zeigte kein Tooltip, der an einer Beschriftung hängt (Pass: Klicks gehen weiter an die Tafel)
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
