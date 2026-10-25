using Godot;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die <b>Betriebsleiste</b> unten (PR #197) – ein Teil der <c>partial class Hauptszene</c>: je eigener Betrieb ein Knopf mit dem Symbol seiner Ware,
/// dem Namen und dem Zustand (läuft, steht, Lager voll, Karren unterwegs; ein Warnzeichen, wenn er steht). Ein Klick fährt die Kamera hin und öffnet die
/// Kurzübersicht rechts, ein Doppelklick geht hinein. Daneben „Hafen“ (nur mit Schiffshandel, PR #199) und „+ Bauen“, rechts Rat, Bericht und Menü. Bei vielen Betrieben rollt die Leiste.
/// In der Innenansicht ist sie ausgeblendet (unten rechts steht dort die Raumleiste).
/// </summary>
public partial class Hauptszene
{
    private PanelContainer _leiste = null!;
    private HBoxContainer _leistenBetriebe = null!;
    private readonly List<(Betrieb Betrieb, Button Knopf, Label Zustand)> _leistenKnoepfe = new();
    private Button _leisteRat = null!;
    private Button _leisteBauen = null!;
    private double _leisteSeit;

    private void LeisteBauen()
    {
        _leiste = Leistentafel(8, 6);
        _leiste.MouseFilter = Control.MouseFilterEnum.Stop;
        _leiste.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide, Control.LayoutPresetMode.KeepSize, 16);
        _leiste.OffsetLeft = 16;
        _leiste.OffsetRight = -16;
        _leiste.OffsetBottom = -12;
        _leiste.GrowVertical = Control.GrowDirection.Begin;
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_leiste);
        var reihe = new HBoxContainer();
        reihe.AddThemeConstantOverride("separation", 8);
        _leiste.AddChild(reihe);

        var rolle = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, 52),
        };
        reihe.AddChild(rolle);
        _leistenBetriebe = new HBoxContainer();
        _leistenBetriebe.AddThemeConstantOverride("separation", 6);
        rolle.AddChild(_leistenBetriebe);

        Button Knopf(string text, string tipp, Action aktion)
        {
            var knopf = new Button { Text = text, TooltipText = tipp, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 44),
                                     SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            knopf.Pressed += aktion;
            reihe.AddChild(knopf);
            return knopf;
        }
        HafenKnopfBauen(reihe); // „Hafen“: Schiffe und ferne Städte, nur mit Schiffshandel (Hauptszene.Hafen.cs)
        _leisteBauen = Knopf("+ Bauen", "Wohnhaus oder Werkstatt bauen – auf Bauplätzen und in den Bauzonen (B)", BauenOeffnen);
        reihe.AddChild(new VSeparator());
        _leisteRat = Knopf("Rat", "Die Ratskammer: Ämter, Wahlen, Anträge (R). Das Rathaus selbst zeigt dazu Statut und Gericht.", RatOeffnen);
        Knopf("Bericht", "Jahresrechnung, Rivalen, Umsätze und das Protokoll des Jahres", () => BerichtOeffnen(BerichtAnzeige.Reiter.Jahresrechnung));
        Knopf("Menü", "Spielmenü: Speichern, Laden, Einstellungen, Hilfe (Esc)", SpielmenueOeffnen);
    }

    /// <summary>Baut die Knöpfe der Betriebe neu, wenn einer dazukommt oder wegfällt; sonst nur die Zustände (viermal je Sekunde).</summary>
    private void LeisteAnzeigen(double delta)
    {
        var sichtbar = _stadt.InnenKennung is null && _spieler is not null;
        if (_leiste.Visible != sichtbar) _leiste.Visible = sichtbar;
        if (!sichtbar || _simulation.Wirtschaft is not { } w) return;
        var ratAn = w.Rat.IstAktiv;
        if (_leisteRat.Disabled == ratAn) _leisteRat.Disabled = !ratAn;
        if (_leisteBauen.Disabled == w.Bauen.IstAktiv) _leisteBauen.Disabled = !w.Bauen.IstAktiv;
        HafenKnopfAnzeigen(w);

        var eigene = w.Betriebe.Where(b => b.Konto == _spieler).ToList();
        if (!eigene.SequenceEqual(_leistenKnoepfe.Select(k => k.Betrieb)))
        {
            UiHilfe.KinderEntfernen(_leistenBetriebe);
            _leistenKnoepfe.Clear();
            foreach (var b in eigene) _leistenKnoepfe.Add(BetriebsKnopf(b));
            _leisteSeit = 1;
        }
        _leisteSeit += delta;
        var auswahl = (_auswahl.Visible ? AusgewaehlterBetrieb() : null);
        foreach (var (b, knopf, zustand) in _leistenKnoepfe)
            if (knopf.ButtonPressed != (b == auswahl)) knopf.SetPressedNoSignal(b == auswahl);
        if (_leisteSeit < 0.25) return;
        _leisteSeit = 0;
        foreach (var (b, knopf, zustand) in _leistenKnoepfe)
        {
            var (kurz, lang, warnung, _) = BetriebsUebersicht.Zustand(b);
            Setzen(zustand, (warnung ? "⚠ " : "") + kurz);
            zustand.AddThemeColorOverride("font_color", warnung ? new Color(0.95f, 0.45f, 0.4f) : new Color(0.75f, 0.75f, 0.75f));
            var tipp = $"{b.Name} ({b.Artdaten.Name}): {lang}\nKlick: hinfahren und Kurzübersicht · Doppelklick: hineingehen";
            if (knopf.TooltipText != tipp) knopf.TooltipText = tipp;
        }
    }

    /// <summary>Ein Knopf der Leiste: Symbol der Ware, Name und Zustand. Die Kinder fangen keine Maus, der Knopf selbst nimmt Klick und Doppelklick.</summary>
    private (Betrieb, Button, Label) BetriebsKnopf(Betrieb b)
    {
        var knopf = new Button { ToggleMode = true, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(200, 48), ClipContents = true };
        _leistenBetriebe.AddChild(knopf);
        var inhalt = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        inhalt.AddThemeConstantOverride("separation", 6);
        inhalt.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, 6);
        knopf.AddChild(inhalt);
        var ware = b.IstWerkstatt ? b.Produkt : b.Rohstoffe.Count > 0 ? b.Rohstoffe[0] : b.Artdaten.Name;
        var symbol = WarenIcons.Bild(ware, 32);
        symbol.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        inhalt.AddChild(symbol);
        var spalte = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        spalte.AddThemeConstantOverride("separation", 0);
        inhalt.AddChild(spalte);
        var name = Hauptmenue.Text(spalte, b.Name, 13);
        name.MouseFilter = Control.MouseFilterEnum.Ignore;
        name.ClipText = true;
        name.CustomMinimumSize = new Vector2(146, 0);
        var zustand = Hauptmenue.Text(spalte, "", 12);
        zustand.MouseFilter = Control.MouseFilterEnum.Ignore;
        // Doppelklick: hineingehen. Der erste Klick des Doppelklicks hat die Kurzübersicht schon geöffnet.
        knopf.GuiInput += eingabe =>
        {
            if (eingabe is InputEventMouseButton { Pressed: true, DoubleClick: true, ButtonIndex: MouseButton.Left })
                BetriebBetreten(b, null);
        };
        knopf.Pressed += () => BetriebAuswaehlen(b, hinfahren: true);
        return (b, knopf, zustand);
    }

    /// <summary>Zeigt die Kurzübersicht eines eigenen Betriebs rechts (und fährt auf Wunsch die Kamera hin).</summary>
    private void BetriebAuswaehlen(Betrieb b, bool hinfahren)
    {
        var kennung = Stadtansicht.BetriebsKennung(b);
        RechtsSchliessen();
        _auswahlKennung = kennung;
        AuswahlAnzeigen();
        if (hinfahren) _stadt.Hinfahren(kennung);
    }

    /// <summary>Geht in den Betrieb hinein und öffnet auf Wunsch gleich eine Tafel der Bedienung (Kurzübersicht „Beheben“, Startoption).</summary>
    private void BetriebBetreten(Betrieb b, string? tafel)
    {
        _auswahlKennung = Stadtansicht.BetriebsKennung(b);
        if (_stadt.InnenKennung != _auswahlKennung && !_stadt.InnenansichtBetreten(b)) return;
        if (tafel is not null) InnenTafelOeffnen(tafel);
    }
}
