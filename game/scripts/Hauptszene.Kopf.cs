using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Oben im Bild (PR #197) – ein Teil der <c>partial class Hauptszene</c>: links die <b>Kopfzeile</b> (Wappen, Geld, Stand, Aktionspunkte; ein Klick
/// öffnet das Wohnhaus), in der Mitte der <b>Zeitstrahl</b> (Jahreszeit, Jahr, Uhrzeit und Wetter mit Aussicht, darunter das Band des Tages mit
/// Sonne bzw. Mond, darunter Pause und Tempo). Beide sitzen in einer Reihe über die ganze Breite: Ist das Fenster schmal, rückt der Zeitstrahl aus
/// der Mitte nach rechts, statt die Kopfzeile zu überdecken.
/// </summary>
public partial class Hauptszene
{
    private HBoxContainer _obenReihe = null!;
    private Control _obenAusgleich = null!;
    private PanelContainer _kopfzeile = null!;
    private WappenBild _kopfWappen = null!;
    private PortraetAnzeige _kopfPortraet = null!;
    private Label _kopfGeld = null!;
    private Label _kopfStand = null!;
    private Label _kopfAp = null!;
    private Label _blick = null!;
    private PanelContainer _zeitTafel = null!;
    private Label _zeitText = null!;
    private WetterAnzeige _wetter = null!;
    private Zeitstrahl _zeitstrahl = null!;
    private Button _pauseKnopf = null!;
    private HBoxContainer _tempoleiste = null!;

    /// <summary>Eine kleine, deckende Tafel (dunkler Grund, Rand innen) – für Kopfzeile, Zeitstrahl, Betriebsleiste und Meldungen.</summary>
    private static PanelContainer Leistentafel(int randLinks = 12, int randOben = 8)
    {
        var tafel = new PanelContainer();
        tafel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.15f, 0.13f, 0.97f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = randLinks, ContentMarginRight = randLinks, ContentMarginTop = randOben, ContentMarginBottom = randOben,
        });
        return tafel;
    }

    private void KopfBauen()
    {
        var oberflaeche = GetNode<CanvasLayer>("Oberflaeche");
        _obenReihe = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _obenReihe.AddThemeConstantOverride("separation", 16);
        _obenReihe.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide, Control.LayoutPresetMode.KeepSize, 16);
        _obenReihe.OffsetLeft = 16;
        _obenReihe.OffsetRight = -16;
        _obenReihe.OffsetTop = 16;
        oberflaeche.AddChild(_obenReihe);

        // ── Kopfzeile: Wappen · Geld · Stand · AP, darunter klein, wohin man blickt ──
        _kopfzeile = Leistentafel();
        _kopfzeile.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        _kopfzeile.MouseFilter = Control.MouseFilterEnum.Stop;
        _kopfzeile.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        _kopfzeile.TooltipText = "Euer Haus – ein Klick öffnet das Wohnhaus (Stand, Familie, Handwerk, Geld, Hinterzimmer)";
        _kopfzeile.GuiInput += eingabe =>
        {
            if (eingabe is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) WohnhausOeffnen(WohnhausAnzeige.Reiter.Haus);
        };
        _obenReihe.AddChild(_kopfzeile);
        var kopfSpalte = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        kopfSpalte.AddThemeConstantOverride("separation", 2);
        _kopfzeile.AddChild(kopfSpalte);
        var kopfReihe = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        kopfReihe.AddThemeConstantOverride("separation", 12);
        kopfSpalte.AddChild(kopfReihe);
        _kopfPortraet = PortraetAnzeige.Neu(PortraetAnzeige.Klein); // das Oberhaupt (PR #211), blinzelt ab und zu
        _kopfPortraet.MouseFilter = Control.MouseFilterEnum.Ignore;
        _kopfPortraet.Visible = false;
        kopfReihe.AddChild(_kopfPortraet);
        _kopfWappen = WappenBild.Neu(null, 34);
        _kopfWappen.MouseFilter = Control.MouseFilterEnum.Ignore;
        kopfReihe.AddChild(_kopfWappen);
        Label KopfText(int groesse, Color? farbe = null)
        {
            var label = Hauptmenue.Text(kopfReihe, "", groesse, farbe);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.MouseFilter = Control.MouseFilterEnum.Pass; // Tooltip, der Klick geht an die Kopfzeile
            return label;
        }
        _kopfGeld = KopfText(20);
        _kopfStand = KopfText(15, new Color(0.95f, 0.82f, 0.45f));
        _kopfAp = KopfText(15, new Color(0.95f, 0.82f, 0.45f));
        _blick = Hauptmenue.Text(kopfSpalte, "", 12, new Color(0.75f, 0.75f, 0.75f));
        _blick.MouseFilter = Control.MouseFilterEnum.Ignore;

        var links = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _obenReihe.AddChild(links);

        // ── Zeitstrahl: Datum, Uhrzeit, Wetter · Band · Pause und Tempo ──
        _zeitTafel = Leistentafel(14, 6);
        _zeitTafel.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        _zeitTafel.MouseFilter = Control.MouseFilterEnum.Stop;
        _obenReihe.AddChild(_zeitTafel);
        var zeitSpalte = new VBoxContainer();
        zeitSpalte.AddThemeConstantOverride("separation", 2);
        _zeitTafel.AddChild(zeitSpalte);
        var zeitReihe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        zeitReihe.AddThemeConstantOverride("separation", 12);
        zeitSpalte.AddChild(zeitReihe);
        _zeitText = Hauptmenue.Text(zeitReihe, "", 16);
        _zeitText.VerticalAlignment = VerticalAlignment.Center;
        _wetter = new WetterAnzeige(); // Wetter jetzt und Aussicht (PR #195)
        zeitReihe.AddChild(_wetter);
        _zeitstrahl = new Zeitstrahl();
        zeitSpalte.AddChild(_zeitstrahl);
        var knoepfe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        knoepfe.AddThemeConstantOverride("separation", 6);
        zeitSpalte.AddChild(knoepfe);
        _pauseKnopf = new Button { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(96, 0), TooltipText = "Pause (Leertaste)" };
        knoepfe.AddChild(_pauseKnopf);
        _tempoleiste = new HBoxContainer();
        _tempoleiste.AddThemeConstantOverride("separation", 4);
        knoepfe.AddChild(_tempoleiste);

        var rechts = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _obenReihe.AddChild(rechts);
        // So breit wie die Kopfzeile, damit der Zeitstrahl in der Mitte steht – solange Platz ist (siehe KopfAnzeigen)
        _obenAusgleich = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _obenReihe.AddChild(_obenAusgleich);
    }

    /// <summary>Jedes Bild: Kopfzeile und Zeitstrahl an die Simulation angleichen.</summary>
    private void KopfAnzeigen()
    {
        var zeit = _simulation.Zeit;
        var takt = _simulation.Takt;
        Setzen(_zeitText, $"{KalenderTexte.Name(zeit.Jahreszeit)} · {zeit.Jahr} · {zeit.UhrzeitText} · {KalenderTexte.Name(zeit.Tagesphase)}");
        _wetter.Zeigen(_stadt.WetterJetzt, _stadt.WetterAussicht(3));
        _zeitstrahl.Minute = zeit.MinuteImTag;
        var pause = _simulation.IstBeendet ? "Partie beendet" : takt.IstPausiert ? "▶ Weiter" : "❚❚ Pause";
        if (_pauseKnopf.Text != pause) _pauseKnopf.Text = pause;
        _pauseKnopf.Disabled = _simulation.IstBeendet;
        for (var i = 0; i < _tempoKnoepfe.Count; i++)
            _tempoKnoepfe[i].SetPressedNoSignal(!takt.IstPausiert && Taktgeber.AlleTempostufen /* TESTTEMPO */[i] == takt.Tempo);

        // Der Ausgleich rechts ist so breit wie die Kopfzeile, solange daneben noch Platz bleibt – sonst schmaler (bis 0).
        var frei = _obenReihe.Size.X - _kopfzeile.Size.X - _zeitTafel.Size.X - 4 * 16;
        var ausgleich = Mathf.Clamp(frei, 0, _kopfzeile.Size.X);
        if (!Mathf.IsEqualApprox(_obenAusgleich.CustomMinimumSize.X, ausgleich)) _obenAusgleich.CustomMinimumSize = new Vector2(ausgleich, 0);

        Setzen(_blick, _stadt.RaumName is { } raum ? $"{_stadt.InnenName} · {raum}"
            : _stadt.InnenName is { } innen ? $"Innenansicht {innen} · Blick nach {_stadt.Blickrichtung}"
            : $"Blick nach {_stadt.Blickrichtung}");

        if (_simulation.Wirtschaft is not { } w || _spieler is null)
        {
            _kopfzeile.Visible = _blick.Text != "";
            _kopfGeld.Visible = _kopfStand.Visible = _kopfAp.Visible = _kopfPortraet.Visible = false;
            return;
        }
        var waehrung = w.Daten.Allgemein.Waehrung;
        _kopfWappen.Wappen = _spieler.Wappen;
        var oberhaupt = w.Familien.Von(_spieler)?.Oberhaupt;
        _kopfPortraet.Visible = oberhaupt is not null;
        if (oberhaupt is not null) _kopfPortraet.Zeigen(w.Familien, oberhaupt);
        Setzen(_kopfGeld, $"{Zahlenformat.Ganz(_spieler.Vermoegen)} {waehrung}");
        _kopfGeld.AddThemeColorOverride("font_color", _spieler.Reinvermoegen < 0 ? new Color(0.95f, 0.45f, 0.4f) : new Color(1, 1, 1));
        _kopfGeld.TooltipText = WohnhausAnzeige.VermoegenTeile(_spieler, waehrung);
        Setzen(_kopfStand, w.StandVon(_spieler).Name + (w.Rat.AmtVon(_spieler) is { } amt ? $" · {amt.Name}" : ""));
        _kopfStand.TooltipText = WohnhausAnzeige.StandText(w, _spieler);
        _kopfAp.Visible = w.HatAktionspunkte && w.Daten.Aktionspunkte is not null;
        if (w.Daten.Aktionspunkte is { } ap)
        {
            Setzen(_kopfAp, $"AP {_spieler.Aktionspunkte.Vorrat}/{ap.Hoechstens}");
            _kopfAp.TooltipText = $"Aktionspunkte: {_spieler.Aktionspunkte.Vorrat} von höchstens {ap.Hoechstens}, +{w.AktionspunkteJeRunde + w.ApBonus(_spieler)} zum Jahreswechsel.\n"
                                  + WohnhausAnzeige.ApTipp;
        }
    }

    /// <summary>Setzt einen Text nur bei einer Änderung (sonst misst Godot die Tafel jedes Bild neu).</summary>
    private static void Setzen(Label label, string text)
    {
        if (label.Text != text) label.Text = text;
    }
}
