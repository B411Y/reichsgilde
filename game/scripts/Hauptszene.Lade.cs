using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die <b>Zunftlade</b> unten in der Mitte (UI-Umbau „Zunftlade“, Teil 1; ersetzt Kopfzeile und Betriebsleiste aus PR #197) – ein Teil der
/// <c>partial class Hauptszene</c>. Ein Kasten aus Holz mit Steinrand: links die <b>Werte-Fächer</b> des Hauses (Bargeld, Aktionspunkte, Ansehen,
/// Stress, freie Punkte der Lebensweise) und das Fach der eigenen <b>Betriebe</b> (je Betrieb Symbol der Ware, Name, Zustand; Klick: hinfahren und
/// Kurzübersicht, Doppelklick: hineingehen), in der Mitte das <b>Porträt-Fach</b> im Steinbogen mit dem Namensschild auf Pergament (Klick: Familie),
/// rechts das Fach der <b>Befehle</b> (Hafen, Bauen, Rat, Bericht, Lexikon, Menü). Über der Lade docken rechts die Meldungen an
/// (<see cref="Siegelecke"/>), links in der Innenansicht das Schild der Räume. Die Lade ist höchstens so breit wie im Thema steht und wird mit dem
/// Fenster schmaler. Aussehen und Maße stehen nur im Thema (<see cref="LadeStil"/>).
/// </summary>
public partial class Hauptszene
{
    private Control _lade = null!;
    private LadeFlaeche _ladeKorpus = null!;
    private Control _ladePortraetfach = null!;
    private LadeFlaeche _ladeNamensschild = null!;
    private PortraetAnzeige _ladePortraet = null!;
    private WappenBild _ladeWappen = null!;
    private Label _ladeName = null!;
    private Control _ladeWerte = null!;
    private (PanelContainer Fach, Label Name, Label Wert) _wertGeld, _wertAp, _wertAnsehen, _wertStress;
    private Button _ladePunkte = null!;                      // Lebensweise: freie Punkte (Teil 3a)
    private Label _blick = null!;
    private VBoxContainer _leistenBetriebe = null!;
    private readonly List<(Betrieb Betrieb, Button Knopf, Label Zustand)> _leistenKnoepfe = new();
    private Button _leisteRat = null!;
    private Button _leisteBauen = null!;
    private double _leisteSeit;
    private Vector2 _ladeBild = new(-1, -1);

    /// <summary>So weit ragt der Porträtbogen über die Oberkante der Lade.</summary>
    private static float LadeUeberstand =>
        Mathf.Max(0, LadeStil.Mass("portraet_hoehe") + LadeStil.Mass("portraet_unten") - LadeStil.Mass("lade_hoehe"));

    private void LadeBauen()
    {
        _lade = new Control { Name = "Lade", MouseFilter = Control.MouseFilterEnum.Ignore };
        LadeStil.Anwenden(_lade);
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_lade);

        // ── Der Kasten: Holz, Steinrand oben und an den Seiten (unten steht er auf dem Bildrand) ──
        _ladeKorpus = LadeStil.Flaeche(_lade, "LadeKorpus", LadeFlaeche.Seiten.Oben | LadeFlaeche.Seiten.Links | LadeFlaeche.Seiten.Rechts);
        _ladeKorpus.MouseFilter = Control.MouseFilterEnum.Stop;
        var innen = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _ladeKorpus.AddChild(innen);
        // Zwei gleich breite Flügel links und rechts vom Porträt-Fach
        var halb = LadeStil.Mass("portraet_breite") / 2f + LadeStil.Mass("portraet_luft");
        var links = LadeStil.Behaelter<HBoxContainer>(innen, "LadeFluegel");
        links.ClipContents = true;
        links.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        links.AnchorRight = 0.5f;
        links.OffsetRight = -halb;
        var rechts = LadeStil.Behaelter<HBoxContainer>(innen, "LadeFluegel");
        rechts.ClipContents = true;
        rechts.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        rechts.AnchorLeft = 0.5f;
        rechts.OffsetLeft = halb;

        // ── Links: Werte-Fächer ──
        var werte = LadeStil.Behaelter<VBoxContainer>(links, "LadeSpalte");
        _ladeWerte = werte;
        var raster = LadeStil.Behaelter<GridContainer>(werte, "LadeRaster");
        raster.Columns = LadeStil.Mass("werte_spalten");
        _wertGeld = Wertfach(raster, "Bargeld");
        _wertAp = Wertfach(raster, "Aktionspunkte");
        _wertAnsehen = Wertfach(raster, "Ansehen");
        _wertStress = Wertfach(raster, "Stress");
        _ladePunkte = LadeStil.Knopf(werte, "", "", () => // „2 Punkte frei“: ein Klick öffnet den Lebensweg
        {
            if (_simulation.Wirtschaft is not { } w || _spieler is null) return;
            if (LebenswegPersonMitPunkten(w) is { } person) _charakter.LebensweiseOeffnen(person);
        });
        _ladePunkte.Visible = false;

        // ── Links: das Fach der eigenen Betriebe (rollt bei vielen) ──
        var betriebe = LadeStil.Flaeche(links, "LadeFach");
        betriebe.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var betriebeSpalte = LadeStil.Behaelter<VBoxContainer>(betriebe, "LadeSpalte");
        LadeStil.Text(betriebeSpalte, "Betriebe", "LadeFachtitel");
        var rolle = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        betriebeSpalte.AddChild(rolle);
        _leistenBetriebe = LadeStil.Behaelter<VBoxContainer>(rolle, "LadeSpalte");
        _leistenBetriebe.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        // ── Rechts: das Fach der Befehle, darüber klein, wohin man blickt ──
        var befehle = LadeStil.Flaeche(rechts, "LadeFach");
        befehle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var befehleSpalte = LadeStil.Behaelter<VBoxContainer>(befehle, "LadeSpalte");
        _blick = LadeStil.Text(befehleSpalte, "", "LadeTextLeise");
        _blick.ClipText = true;
        var knoepfe = LadeStil.Behaelter<GridContainer>(befehleSpalte, "LadeRaster");
        knoepfe.Columns = LadeStil.Mass("befehl_spalten");
        knoepfe.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        Button Befehl(string text, string tipp, Action aktion)
        {
            var knopf = LadeStil.Knopf(knoepfe, text, tipp, aktion);
            knopf.SizeFlagsHorizontal = knopf.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            return knopf;
        }
        HafenKnopfBauen(knoepfe); // „Hafen“: Schiffe und ferne Städte, nur mit Schiffshandel (Hauptszene.Hafen.cs)
        _leisteBauen = Befehl("Bauen", "Wohnhaus oder Werkstatt bauen – auf Bauplätzen und in den Bauzonen (B)", BauenOeffnen);
        _leisteRat = Befehl("Rat", "Die Ratskammer: Ämter, Wahlen, Anträge (R). Das Rathaus selbst zeigt dazu Statut und Gericht.", RatOeffnen);
        Befehl("Bericht", "Jahresrechnung, Rivalen, Umsätze und alle Meldungen zum Nachlesen", () => BerichtOeffnen(BerichtAnzeige.Reiter.Jahresrechnung));
        Befehl("Lexikon", "Begriffe und Regeln des Spiels nachschlagen (L)", () => _lexikon.Oeffnen());
        if (Entwickler.Aktiv) // nur mit dem Haken „Entwickler-Optionen“ (Hauptszene.Entwickler.cs)
            Befehl("Entwickler", "Entwicklerfenster: Werte setzen, Zeit vorspulen, Wetter, Messwerte (F12)", () => _entwickler?.Umschalten());
        Befehl("Menü", "Spielmenü: Speichern, Laden, Einstellungen, Hilfe (Esc)", SpielmenueOeffnen);

        // ── Mitte: das Porträt-Fach im Steinbogen (ragt über die Lade), darunter das Namensschild ──
        _ladePortraetfach = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Stop, MouseDefaultCursorShape = Control.CursorShape.PointingHand, Visible = false,
            TooltipText = "Euer Haus – ein Klick öffnet die Familie (Oberhaupt, Ehepartner, Kinder, Stammbaum)",
        };
        _ladePortraetfach.GuiInput += eingabe =>
        {
            if (eingabe is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) FamilieOeffnen();
        };
        _lade.AddChild(_ladePortraetfach);
        var rand = LadeStil.Mass("portraet_rand");
        var maske = new LadeBogen { Rand = rand };
        maske.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _ladePortraetfach.AddChild(maske);
        var grund = new ColorRect { Color = LadeStil.Farbe("portraet_grund"), MouseFilter = Control.MouseFilterEnum.Ignore };
        grund.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        maske.AddChild(grund);
        var seite = LadeStil.Mass("portraet_breite") - 2 * rand;
        _ladePortraet = PortraetAnzeige.Neu(seite); // das Oberhaupt (PR #211), blinzelt ab und zu
        _ladePortraet.MouseFilter = Control.MouseFilterEnum.Ignore;
        _ladePortraet.Position = new Vector2(rand, LadeStil.Mass("portraet_hoehe") - rand - seite); // steht auf der Schwelle, der Bogen schneidet oben ab
        _ladePortraet.Size = new Vector2(seite, seite);
        maske.AddChild(_ladePortraet);
        var bogen = new TextureRect
        {
            Texture = LadeStil.Bild("bogen", "LadePortraet"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        bogen.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _ladePortraetfach.AddChild(bogen);

        _ladeNamensschild = LadeStil.Flaeche(_lade, "LadeNamensschild");
        _ladeNamensschild.MouseFilter = Control.MouseFilterEnum.Stop; // für den Hinweis zum Stand
        _ladeNamensschild.Visible = false;
        var schild = LadeStil.Behaelter<HBoxContainer>(_ladeNamensschild, "LadeReihe");
        schild.Alignment = BoxContainer.AlignmentMode.Center;
        _ladeWappen = WappenBild.Neu(null, LadeStil.Mass("wappen"));
        _ladeWappen.MouseFilter = Control.MouseFilterEnum.Ignore;
        _ladeWappen.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        schild.AddChild(_ladeWappen);
        _ladeName = LadeStil.Text(schild, "", "LadeName");
        _ladeName.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _ladeName.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _ladeName.HorizontalAlignment = HorizontalAlignment.Center;
    }

    /// <summary>Ein Werte-Fach: oben klein, was es ist, darunter der Wert. Es fängt die Maus nur für seinen Hinweis.</summary>
    private static (PanelContainer Fach, Label Name, Label Wert) Wertfach(Node eltern, string name)
    {
        var fach = new PanelContainer
        {
            ThemeTypeVariation = "LadeWertfach", MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(LadeStil.Mass("wertfach_breite"), 0),
        };
        eltern.AddChild(fach);
        var spalte = LadeStil.Behaelter<VBoxContainer>(fach, "LadeSpalteEng");
        return (fach, LadeStil.Text(spalte, name, "LadeWertname"), LadeStil.Text(spalte, "", "LadeWert"));
    }

    /// <summary>
    /// Stellt die Lade unten mittig ins Bild (höchstens so breit wie im Thema, sonst so breit wie das Fenster) und dockt an, was an ihr hängt:
    /// die Meldungen rechts über der Lade, das Schild der Räume links. Rechnet nur, wenn sich die Bildgröße ändert.
    /// </summary>
    private void LadeEinpassen()
    {
        var bild = GetViewport().GetVisibleRect().Size;
        if (bild == _ladeBild) return;
        _ladeBild = bild;
        float breite = Mathf.Min(LadeStil.Mass("lade_breite"), bild.X), hoehe = LadeStil.Mass("lade_hoehe"), ueber = LadeUeberstand;
        _lade.Position = new Vector2((bild.X - breite) / 2, bild.Y - hoehe - ueber);
        _lade.Size = new Vector2(breite, hoehe + ueber);
        _ladeKorpus.Position = new Vector2(0, ueber);
        _ladeKorpus.Size = new Vector2(breite, hoehe);
        var fach = new Vector2(LadeStil.Mass("portraet_breite"), LadeStil.Mass("portraet_hoehe"));
        _ladePortraetfach.Position = new Vector2((breite - fach.X) / 2, hoehe + ueber - LadeStil.Mass("portraet_unten") - fach.Y);
        _ladePortraetfach.Size = fach;
        var schild = new Vector2(fach.X, LadeStil.Mass("namensschild_hoehe"));
        _ladeNamensschild.Position = new Vector2((breite - schild.X) / 2, hoehe + ueber - LadeStil.Mass("namensschild_unten") - schild.Y);
        _ladeNamensschild.Size = schild;
        // Was an der Lade hängt
        _ecke.Andocken((bild.X - breite) / 2 + LadeStil.Mass("meldungen_einzug"), LadeStil.Mass("meldungen_unten"));
        RaumleisteEinpassen();
    }

    /// <summary>Das Schild der Räume steht links über der Lade (die Breite wechselt mit den Räumen).</summary>
    private void RaumleisteEinpassen()
    {
        if (_raumleiste is null || _ladeBild.X < 0) return;
        var kante = (_ladeBild.X - Mathf.Min(LadeStil.Mass("lade_breite"), _ladeBild.X)) / 2;
        _raumleiste.Position = new Vector2(kante + LadeStil.Mass("schild_einzug"), _ladeBild.Y - LadeStil.Mass("schild_unten") - _raumleiste.Size.Y);
    }

    /// <summary>Jedes Bild: Lage der Lade, Porträt, Namensschild und Werte an die Simulation angleichen.</summary>
    private void LadeWerteAnzeigen()
    {
        LadeEinpassen();
        Setzen(_blick, _stadt.RaumName is { } raum ? $"{_stadt.InnenName} · {raum}"
            : _stadt.InnenName is { } innen ? $"Innenansicht {innen} · Blick nach {_stadt.Blickrichtung}"
            : $"Blick nach {_stadt.Blickrichtung}");

        var mitSpieler = _simulation.Wirtschaft is not null && _spieler is not null;
        if (_ladeWerte.Visible != mitSpieler) _ladeWerte.Visible = mitSpieler;
        if (_simulation.Wirtschaft is not { } w || _spieler is null)
        {
            _ladePortraetfach.Visible = _ladeNamensschild.Visible = false;
            return;
        }
        var waehrung = w.Daten.Allgemein.Waehrung;
        var oberhaupt = w.Familien.Von(_spieler)?.Oberhaupt;
        _ladePortraetfach.Visible = _ladeNamensschild.Visible = true;
        _ladePortraet.Visible = oberhaupt is not null;
        if (oberhaupt is not null) _ladePortraet.Zeigen(w.Familien, oberhaupt);
        _ladeWappen.Wappen = _spieler.Wappen;
        var stand = w.StandVon(_spieler).Name + (w.Rat.AmtVon(_spieler) is { } amt ? $" · {amt.Name}" : "");
        Setzen(_ladeName, oberhaupt is not null ? $"{oberhaupt.Vorname} · {stand}" : stand);
        var standTipp = $"{_spieler.Inhaber} · {stand}\n{WohnhausAnzeige.StandText(w, _spieler)}";
        if (_ladeNamensschild.TooltipText != standTipp) _ladeNamensschild.TooltipText = standTipp;

        LebenswegPunkteAnzeigen(w);
        Setzen(_wertGeld.Name, waehrung);
        Setzen(_wertGeld.Wert, Zahlenformat.Ganz(_spieler.Vermoegen));
        LadeStil.Variante(_wertGeld.Wert, _spieler.Reinvermoegen < 0 ? "LadeWertSchlecht" : "LadeWert");
        _wertGeld.Fach.TooltipText = WohnhausAnzeige.VermoegenTeile(_spieler, waehrung);
        _wertAp.Fach.Visible = w.HatAktionspunkte && w.Daten.Aktionspunkte is not null;
        if (w.Daten.Aktionspunkte is { } ap)
        {
            Setzen(_wertAp.Wert, $"{_spieler.Aktionspunkte.Vorrat}/{ap.Hoechstens}");
            _wertAp.Fach.TooltipText = $"Aktionspunkte: {_spieler.Aktionspunkte.Vorrat} von höchstens {ap.Hoechstens}, +{w.AktionspunkteJeRunde + w.ApBonus(_spieler)} zum Jahreswechsel.\n"
                                       + WohnhausAnzeige.ApTipp;
        }
        Setzen(_wertAnsehen.Wert, Zahlenformat.Ganz(w.Rat.Ansehen(_spieler)));
        _wertAnsehen.Fach.TooltipText = "Ansehen eures Hauses, aus Stand und Vermögen.";
        var stress = oberhaupt is not null && w.Familien.StressDaten is not null && w.Familien.HatStress(oberhaupt);
        if (_wertStress.Fach.Visible != stress) _wertStress.Fach.Visible = stress;
        if (stress && oberhaupt is not null && w.Familien.StressDaten is { } stressDaten)
        {
            var stufe = w.Familien.StressStufe(oberhaupt);
            Setzen(_wertStress.Wert, w.Familien.StressStufenname(stufe));
            LadeStil.Variante(_wertStress.Wert, stufe > 1 ? "LadeWertSchlecht" : "LadeWert");
            _wertStress.Fach.TooltipText = CharakterAnzeige.StressTipp(w.Familien, stressDaten, oberhaupt);
        }
    }

    /// <summary>Baut die Knöpfe der Betriebe neu, wenn einer dazukommt oder wegfällt; sonst nur die Zustände (viermal je Sekunde).</summary>
    private void LadeAnzeigen(double delta)
    {
        if (_simulation.Wirtschaft is not { } w) return;
        var ratAn = w.Rat.IstAktiv && _spieler is not null;
        if (_leisteRat.Disabled == ratAn) _leisteRat.Disabled = !ratAn;
        var bauenAn = w.Bauen.IstAktiv && _spieler is not null;
        if (_leisteBauen.Disabled == bauenAn) _leisteBauen.Disabled = !bauenAn;
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
            LadeStil.Variante(zustand, warnung ? "LadeTextWarnung" : "LadeTextLeise");
            var tipp = $"{b.Name} ({b.Artdaten.Name}): {lang}\nKlick: hinfahren und Kurzübersicht · Doppelklick: hineingehen";
            if (knopf.TooltipText != tipp) knopf.TooltipText = tipp;
        }
    }

    /// <summary>Ein Betrieb im Fach: Symbol der Ware, Name und Zustand. Die Kinder fangen keine Maus, der Knopf selbst nimmt Klick und Doppelklick.</summary>
    private (Betrieb, Button, Label) BetriebsKnopf(Betrieb b)
    {
        var knopf = LadeStil.Knopf(_leistenBetriebe, "", "", null);
        knopf.ToggleMode = true;
        knopf.ClipContents = true;
        knopf.CustomMinimumSize = new Vector2(0, LadeStil.Mass("betrieb_hoehe"));
        knopf.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var rand = LadeStil.Behaelter<MarginContainer>(knopf, "LadeKnopfInhalt");
        rand.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var inhalt = LadeStil.Behaelter<HBoxContainer>(rand, "LadeReihe");
        var ware = b.IstWerkstatt ? b.Produkt : b.Rohstoffe.Count > 0 ? b.Rohstoffe[0] : b.Artdaten.Name;
        var symbol = WarenIcons.Bild(ware, LadeStil.Mass("betrieb_symbol"));
        symbol.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        inhalt.AddChild(symbol);
        var spalte = LadeStil.Behaelter<VBoxContainer>(inhalt, "LadeSpalteEng");
        spalte.Alignment = BoxContainer.AlignmentMode.Center;
        spalte.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var name = LadeStil.Text(spalte, b.Name);
        name.ClipText = true;
        var zustand = LadeStil.Text(spalte, "", "LadeTextLeise");
        zustand.ClipText = true;
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
