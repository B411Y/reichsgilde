using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Oben im Bild (PR #197) – ein Teil der <c>partial class Hauptszene</c>: links die <b>Kopfzeile</b> (Wappen, Geld, Stand, Aktionspunkte; ein Klick
/// öffnet das Familienfenster), in der Mitte der <b>Zeitstrahl</b> (Jahreszeit, Jahr, Uhrzeit und Wetter mit Aussicht, darunter das Band des Tages mit
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
    private Label _kopfPunkte = null!;                       // Lebensweise: freie Punkte (Teil 3a)
    private Dictionary<int, int>? _lebenswegFrei;            // Person → freie Punkte beim letzten Blick (null: noch nicht gelesen)
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
        _kopfzeile.TooltipText = "Euer Haus – ein Klick öffnet die Familie (Oberhaupt, Ehepartner, Kinder, Stammbaum)";
        _kopfzeile.GuiInput += eingabe =>
        {
            if (eingabe is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) FamilieOeffnen();
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
        _kopfPunkte = KopfText(14, new Color(0.55f, 0.85f, 0.5f)); // „2 Lebensweg-Punkte frei“: ein Klick öffnet den Reiter
        _kopfPunkte.MouseFilter = Control.MouseFilterEnum.Stop;
        _kopfPunkte.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        _kopfPunkte.Visible = false;
        _kopfPunkte.GuiInput += eingabe =>
        {
            if (eingabe is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } || _simulation.Wirtschaft is not { } w || _spieler is null) return;
            if (LebenswegPersonMitPunkten(w) is { } person) _charakter.LebensweiseOeffnen(person);
            GetViewport().SetInputAsHandled();
        };
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
        _zeitText.MouseFilter = Control.MouseFilterEnum.Pass;
        _zeitText.TooltipText = "Jahreszeit, Jahr, Uhrzeit und Tagesabschnitt. Ein Spieltag dauert von 4 bis 23 Uhr, gearbeitet wird von 8 bis 20 Uhr; 1140 Ticks sind ein Jahr.";
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
        _zeitstrahl.Jahreszeit = _stadt.JahreszeitErzwungen ?? zeit.Jahreszeit; // Nacht und Mond nach den Sonnenzeiten der Jahreszeit, wie das Licht der Stadt
        _zeitstrahl.Minute = _stadt.LichtUhrErzwungen is { } lichtUhr ? (int)(lichtUhr * 60) - Spielzeit.TagesbeginnMinute : zeit.MinuteImTag;
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
            _kopfGeld.Visible = _kopfStand.Visible = _kopfAp.Visible = _kopfPortraet.Visible = _kopfPunkte.Visible = false;
            return;
        }
        var waehrung = w.Daten.Allgemein.Waehrung;
        _kopfWappen.Wappen = _spieler.Wappen;
        var oberhaupt = w.Familien.Von(_spieler)?.Oberhaupt;
        _kopfPortraet.Visible = oberhaupt is not null;
        if (oberhaupt is not null) _kopfPortraet.Zeigen(w.Familien, oberhaupt);
        LebenswegPunkteAnzeigen(w);
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

    /// <summary>Die Person des eigenen Hauses mit den meisten freien Punkten der Lebensweise (Oberhaupt, Ehepartner, Erbe), sonst null.</summary>
    private Reichsgilde.Core.Dynastie.Person? LebenswegPersonMitPunkten(Reichsgilde.Core.Wirtschaft.Stadtwirtschaft w)
    {
        if (_spieler is null || w.Familien.Von(_spieler) is not { } haus) return null;
        var buch = w.Familien;
        return new[] { haus.Oberhaupt, haus.Ehepartner, buch.Erbe(haus) }.OfType<Reichsgilde.Core.Dynastie.Person>().Distinct()
            .Where(buch.HatLebensweg).Select(p => (Person: p, Frei: Reichsgilde.Core.Dynastie.TalentTexte.Alle.Sum(t => buch.FreiePunkte(p, t))))
            .Where(x => x.Frei > 0).OrderByDescending(x => x.Frei).Select(x => x.Person).FirstOrDefault();
    }

    /// <summary>
    /// Lebensweise (Teil 3a): Das Zeichen in der Kopfzeile zeigt freie Punkte der eigenen Leute; wird einer neu frei, kommt eine Meldung
    /// („Neuigkeiten aus dem Haus“). Der Kern sagt es nicht von selbst – darum liest die Oberfläche die Punkte jedes Bild (ein paar Rechnungen).
    /// </summary>
    private void LebenswegPunkteAnzeigen(Reichsgilde.Core.Wirtschaft.Stadtwirtschaft w)
    {
        if (_spieler is null || !w.Familien.HatLebensweise || w.Familien.Von(_spieler) is not { } haus)
        {
            _kopfPunkte.Visible = false;
            _lebenswegFrei = null;
            return;
        }
        var buch = w.Familien;
        var jetzt = new Dictionary<int, int>();
        var personen = new[] { haus.Oberhaupt, haus.Ehepartner, buch.Erbe(haus) }.OfType<Reichsgilde.Core.Dynastie.Person>().Distinct().Where(buch.HatLebensweg).ToList();
        foreach (var p in personen)
            jetzt[p.Nummer] = Reichsgilde.Core.Dynastie.TalentTexte.Alle.Sum(t => buch.FreiePunkte(p, t));
        if (_lebenswegFrei is not null)
            foreach (var p in personen)
                if (jetzt[p.Nummer] > _lebenswegFrei.GetValueOrDefault(p.Nummer, 0))
                {
                    var pfad = Reichsgilde.Core.Dynastie.TalentTexte.Alle.First(t => buch.FreiePunkte(p, t) > 0);
                    var name = buch.LebensweiseDaten?.Pfade.GetValueOrDefault(pfad)?.Name ?? "der Lebensweise";
                    Protokollieren(new Reichsgilde.Core.Wirtschaft.Meldung(Reichsgilde.Core.Wirtschaft.Meldungsart.Sonstiges,
                        $"Neuer Punkt der Lebensweise: {p.Vorname} kann einen Knoten im {name} lernen.")
                        { Haus = w.Konten.ToList().IndexOf(_spieler), Person = p.Nummer, Wichtigkeit = Reichsgilde.Core.Wirtschaft.Meldungswichtigkeit.Normal, Minute = _simulation.Zeit.VergangeneMinuten });
                }
        _lebenswegFrei = jetzt;
        var summe = jetzt.Values.Sum();
        _kopfPunkte.Visible = summe > 0;
        if (summe <= 0) return;
        Setzen(_kopfPunkte, summe == 1 ? "1 Punkt frei" : $"{summe} Punkte frei");
        _kopfPunkte.TooltipText = "Freie Punkte der Lebensweise – ein Klick öffnet den Lebensweg:\n" + string.Join("\n", personen.Where(p => jetzt[p.Nummer] > 0).Select(p => $"{p.Vorname}: {jetzt[p.Nummer]}"));
    }

    /// <summary>Setzt einen Text nur bei einer Änderung (sonst misst Godot die Tafel jedes Bild neu).</summary>
    private static void Setzen(Label label, string text)
    {
        if (label.Text != text) label.Text = text;
    }
}
