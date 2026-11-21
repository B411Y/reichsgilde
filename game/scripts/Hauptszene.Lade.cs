using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die <b>Zunftlade</b> unten in der Mitte (UI-Umbau „Zunftlade“, Teil 1, nach Entwurf 3; ersetzt Kopfzeile und Betriebsleiste aus PR #197) – ein
/// Teil der <c>partial class Hauptszene</c>. Ein Kasten aus Holz mit Steinrand, drei Spalten: links und rechts je eine <b>Seite</b>, in der Mitte
/// das <b>Porträt-Fach</b> im Steinbogen mit dem Namensschild auf Pergament. Jede Seite hat oben eine <b>Schiene</b> und darunter drei
/// <b>Fächer</b> (Knöpfe mit Griff, Symbol, Name und einer Zeile Zustand):
/// <list type="bullet">
/// <item>links die Schiene mit den Werten Gulden, Ansehen, Stress; Fächer in der Stadt: Familie, Betriebe, Bauen – in der Innenansicht: Personal,
/// Produktion, Lager;</item>
/// <item>rechts die Schiene mit den Aktionspunkten und den kleinen Knöpfen Markt, Hinterzimmer, Menü; Fächer in der Stadt: Rat, Bericht, Lexikon –
/// in der Innenansicht: Karren, Talentbaum, Zur Stadt.</item>
/// </list>
/// Das Fach „Betriebe“ klappt über der Lade die Liste der eigenen Betriebe auf (Klick: hinfahren und Kurzübersicht, Doppelklick: hinein). Über der
/// Lade docken rechts die Meldungen an (<see cref="Siegelecke"/>), links in der Innenansicht das Schild der Räume. Die Lade ist höchstens so breit
/// wie im Thema steht und wird mit dem Fenster schmaler. Aussehen und Maße stehen nur im Thema (<see cref="LadeStil"/>).
/// </summary>
public partial class Hauptszene
{
    private Control _lade = null!;
    private LadeFlaeche _ladeKorpus = null!;
    private Control _ladePortraetfach = null!;
    private LadeFlaeche _ladeNamensschild = null!;
    private PortraetAnzeige _ladePortraet = null!;
    private Label _ladeName = null!;
    private (PanelContainer Feld, Label Name, Label Wert, Label Zusatz) _wertGeld, _wertAnsehen, _wertStress, _wertAp;
    private Control[] _faecherStadt = null!, _faecherInnen = null!;
    private (LadeFach Knopf, Label Name, Label Zustand) _fachFamilie, _fachBetriebe, _fachBauen, _fachPersonal, _fachProduktion, _fachLager,
        _fachRat, _fachBericht, _fachLexikon, _fachKarren, _fachTalentbaum, _fachStadt;
    private double _leisteSeit;
    private int _lebenswegSumme;
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
        // Zwei gleich breite Seiten links und rechts vom Porträt-Fach
        var halb = LadeStil.Mass("portraet_breite") / 2f + LadeStil.Mass("portraet_luft");
        var links = LadeStil.Behaelter<VBoxContainer>(innen, "LadeSeite");
        links.ClipContents = true;
        links.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        links.AnchorRight = 0.5f;
        links.OffsetRight = -halb;
        var rechts = LadeStil.Behaelter<VBoxContainer>(innen, "LadeSeite");
        rechts.ClipContents = true;
        rechts.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        rechts.AnchorLeft = 0.5f;
        rechts.OffsetLeft = halb;

        // ── Links: Schiene mit Gulden, Ansehen, Stress ──
        var schieneLinks = Schiene(links);
        _wertGeld = Wertfeld(schieneLinks, "gulden", "", wertZuerst: true);
        _wertAnsehen = Wertfeld(schieneLinks, "ansehen", "Ansehen", wertZuerst: true);
        _wertStress = Wertfeld(schieneLinks, null, "Stress", wertZuerst: false);

        // ── Links: Fächer – in der Stadt Familie, Betriebe, Bauen; drinnen Personal, Produktion, Lager ──
        var stadtLinks = Faecher(links);
        _fachFamilie = Fach(stadtLinks, "familie", "Familie", "Eure Familie: Oberhaupt, Ehepartner, Kinder, Stammbaum", () => FachOeffnen("familie", FamilieInhalt));
        _fachBetriebe = Fach(stadtLinks, "betriebe", "Betriebe", "Eure Betriebe: Zustand, hineingehen, hinfahren", () => FachOeffnen("betriebe", BetriebeInhalt));
        _fachBauen = Fach(stadtLinks, "bauen", "Bauen", "Wohnhaus ausbauen, Wohnhaus oder Werkstatt bauen, Wege ausbauen", () => FachOeffnen("bauen", BauenInhalt));
        var innenLinks = Faecher(links);
        _fachPersonal = Fach(innenLinks, "personal", "Personal", "Das Personal dieser Werkstatt", () => FachOeffnen("personal", PersonalInhalt));
        _fachProduktion = Fach(innenLinks, "produktion", "Produktion", "Was diese Werkstatt herstellt", () => FachOeffnen("produktion", ProduktionInhalt));
        _fachLager = Fach(innenLinks, "lager", "Lager", "Das Lager dieser Werkstatt", () => FachOeffnen("lager", LagerInhalt));

        // ── Rechts: Schiene mit den Aktionspunkten und den kleinen Knöpfen Markt, Hinterzimmer, Menü ──
        var schieneRechts = Schiene(rechts);
        _wertAp = Wertfeld(schieneRechts, null, "AP", wertZuerst: false);
        var luft = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        schieneRechts.AddChild(luft);
        var klein = LadeStil.Behaelter<HBoxContainer>(schieneRechts, "LadeReihe");
        KleinerKnopf(klein, "markt", "Markt", "Der Markt der Stadt: Preise eurer Waren, Markt öffnen (M)", () => FachOeffnen("markt", MarktInhalt));
        KleinerKnopf(klein, "hinterzimmer", "Hinterzimmer", "Das Hinterzimmer: was das Licht scheut (H)", HinterzimmerOeffnen);
        KleinerKnopf(klein, "menue", "Menü", "Spielmenü: Speichern, Laden, Einstellungen, Hilfe (Esc)", SpielmenueOeffnen);

        // ── Rechts: Fächer – in der Stadt Rat, Bericht, Lexikon; drinnen Karren, Talentbaum, Zur Stadt ──
        var stadtRechts = Faecher(rechts);
        _fachRat = Fach(stadtRechts, "rat", "Rat", "Der Rat der Stadt: nächste Sitzung, euer Amt, Ämter, Anträge, Kasse (R öffnet die Ratskammer)", () => FachOeffnen("rat", RatInhalt));
        _fachBericht = Fach(stadtRechts, "bericht", "Bericht", "Das Jahr bisher, Jahresrechnung, Rivalen, Umsätze und alle Meldungen zum Nachlesen",
            () => FachOeffnen("bericht", BerichtInhalt));
        _fachLexikon = Fach(stadtRechts, "lexikon", "Lexikon", "Begriffe und Regeln des Spiels nachschlagen (L)", () => FachOeffnen("lexikon", LexikonInhalt));
        _fachLexikon.Zustand.Text = "Nachschlagen";
        var innenRechts = Faecher(rechts);
        _fachKarren = Fach(innenRechts, "karren", "Karren", "Die Karren dieser Werkstatt", () => FachOeffnen("karren", KarrenInhalt));
        _fachTalentbaum = Fach(innenRechts, "talentbaum", "Talentbaum", "Der Talentbaum der Einrichtung dieser Werkstatt", () => FachOeffnen("talentbaum", TalentbaumInhalt));
        _fachStadt = Fach(innenRechts, "stadt", "Zur Stadt", "Zurück in die Stadt (Esc)", () => _stadt.InnenansichtVerlassenMitBlende());
        _faecherStadt = new Control[] { stadtLinks, stadtRechts };
        _faecherInnen = new Control[] { innenLinks, innenRechts };
        foreach (var f in _faecherInnen) f.Visible = false;

        // ── Mitte: das Porträt-Fach im Steinbogen (ragt über die Lade), darunter das Namensschild ──
        _ladePortraetfach = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Stop, MouseDefaultCursorShape = Control.CursorShape.PointingHand, Visible = false,
            TooltipText = "Euer Oberhaupt – ein Klick öffnet die Charakterseite: Stand, Gulden, AP, Stress, Amt, Talente, Charakterzüge, Familie",
        };
        _ladePortraetfach.GuiInput += eingabe =>
        {
            if (eingabe is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) CharakterseiteOeffnen();
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
        _ladeName = LadeStil.Text(_ladeNamensschild, "", "LadeName");
        _ladeName.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _ladeName.HorizontalAlignment = HorizontalAlignment.Center;
    }

    /// <summary>Klick aufs Porträt: die Charakterseite des eigenen Oberhaupts (ein zweiter Klick schließt sie).</summary>
    private void CharakterseiteOeffnen()
    {
        if (_simulation.Wirtschaft is not { } w || _spieler is null || w.Familien.Von(_spieler)?.Oberhaupt is not { } oberhaupt) return;
        var offen = _charakter.Visible;
        TafelnSchliessen();
        if (!offen) _charakter.Oeffnen(oberhaupt);
    }

    /// <summary>Die Schiene einer Seite: eine Reihe fester Höhe über den Fächern.</summary>
    private static HBoxContainer Schiene(Node seite)
    {
        var schiene = LadeStil.Behaelter<HBoxContainer>(seite, "LadeSchiene");
        schiene.CustomMinimumSize = new Vector2(0, LadeStil.Mass("schiene_hoehe"));
        return schiene;
    }

    /// <summary>Eine Reihe aus drei gleich breiten Fächern, die den Rest der Seite füllt.</summary>
    private static HBoxContainer Faecher(Node seite)
    {
        var reihe = LadeStil.Behaelter<HBoxContainer>(seite, "LadeFaecher");
        reihe.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        return reihe;
    }

    /// <summary>
    /// Der Hinweis am Feld Gulden (Entwurf: „Gulden: 4.000 · dieses Jahr +312“ mit den Posten darunter): Bargeld, der Gewinn des laufenden Jahres aus
    /// der Jahresrechnung des Kerns (<see cref="Jahresbilanz.Gewinn"/>) mit jedem Posten, der schon etwas enthält, darunter das Reinvermögen.
    /// Die zweite Ebene liefert das Lexikon: Begriffe wie die Umsatzsteuer sind unterstrichen und erklären sich im eigenen Hinweis.
    /// </summary>
    private static string GuldenTipp(Konto konto, string waehrung)
    {
        var l = konto.Laufend;
        var zeilen = new List<string> { $"{waehrung}: {Zahlenformat.Ganz(konto.Vermoegen)} · dieses Jahr bisher {{{{{Zahlenformat.MitVorzeichen(l.Gewinn)}}}}}" };
        void Posten(string name, double wert)
        {
            if (Math.Abs(wert) >= 0.5) zeilen.Add($"  {name} {{{{{Zahlenformat.MitVorzeichen(wert)}}}}}");
        }
        Posten("Umsatz", l.Umsatz);
        Posten("Zutaten", -l.Zutaten);
        Posten("Löhne", -l.Personal);
        Posten("Handgeld", -l.Handgeld);
        Posten("Unterhalt", -l.Fixkosten);
        Posten("[[umsatzsteuer|Umsatzsteuer]]", -l.Steuer);
        Posten("Fahrten", -l.Transport);
        Posten("Schaden", -l.Schaden);
        zeilen.Add("Löhne und Unterhalt werden am Jahresende fällig.");
        zeilen.Add(WohnhausAnzeige.VermoegenTeile(konto, waehrung));
        return string.Join("\n", zeilen);
    }

    /// <summary>
    /// Ein Werte-Feld der Schiene: Symbol (wenn es eines gibt), Wert und Beschriftung – <paramref name="wertZuerst"/>: „4.000 Gulden“, sonst
    /// „AP 4/12“; dahinter ein Zusatz (z. B. „ruhig“). Es fängt die Maus nur für seinen Hinweis.
    /// </summary>
    private static (PanelContainer Feld, Label Name, Label Wert, Label Zusatz) Wertfeld(Node eltern, string? symbol, string name, bool wertZuerst)
    {
        var feld = new PanelContainer
        {
            ThemeTypeVariation = "LadeWertfach", MouseFilter = Control.MouseFilterEnum.Stop, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(0, LadeStil.Mass("wert_hoehe")),
        };
        eltern.AddChild(feld);
        var reihe = LadeStil.Behaelter<HBoxContainer>(feld, "LadeWertReihe");
        if (symbol is not null) LadeStil.Symbol(reihe, symbol, "symbol_wert");
        Label nameLabel, wert;
        if (wertZuerst)
        {
            wert = LadeStil.Text(reihe, "", "LadeWert");
            nameLabel = LadeStil.Text(reihe, name, "LadeWertname");
        }
        else
        {
            nameLabel = LadeStil.Text(reihe, name, "LadeWertname");
            wert = LadeStil.Text(reihe, "", "LadeWert");
        }
        var zusatz = LadeStil.Text(reihe, "", "LadeWertname");
        zusatz.Visible = false;
        return (feld, nameLabel, wert, zusatz);
    }

    /// <summary>Ein Fach: Knopf mit Griff, darin Symbol, Name und eine Zeile Zustand. Die Kinder fangen keine Maus.</summary>
    private static (LadeFach Knopf, Label Name, Label Zustand) Fach(Node eltern, string symbol, string name, string tipp, Action aktion)
    {
        var knopf = new LadeFach { TooltipText = tipp, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill, ClipContents = true };
        knopf.ToggleMode = true; // das Fach, dessen Lade-Fenster offen ist, bleibt eingedrückt (LadeAnzeigen gleicht es jedes Bild ab)
        knopf.Pressed += aktion;
        eltern.AddChild(knopf);
        var rand = LadeStil.Behaelter<MarginContainer>(knopf, "LadeFachInhalt");
        rand.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var spalte = LadeStil.Behaelter<VBoxContainer>(rand, "LadeSpalteEng");
        spalte.Alignment = BoxContainer.AlignmentMode.Center;
        LadeStil.Symbol(spalte, symbol, "symbol_fach");
        var nameLabel = LadeStil.Text(spalte, name, "LadeFachtitel");
        nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        var zustand = LadeStil.Text(spalte, "", "LadeTextLeise");
        zustand.HorizontalAlignment = HorizontalAlignment.Center;
        zustand.ClipText = true;
        return (knopf, nameLabel, zustand);
    }

    /// <summary>Ein kleiner Knopf der Schiene: Symbol und Wort.</summary>
    private static Button KleinerKnopf(Node eltern, string symbol, string text, string tipp, Action aktion)
    {
        var knopf = LadeStil.Knopf(eltern, text, tipp, aktion, "LadeKnopfKlein");
        knopf.Icon = LadeStil.Bild(symbol, "LadeSymbole");
        knopf.CustomMinimumSize = new Vector2(0, LadeStil.Mass("wert_hoehe"));
        knopf.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        return knopf;
    }

    /// <summary>Sperrt oder gibt ein Fach frei; der Name wird dabei blass.</summary>
    private static void FachFreigeben((LadeFach Knopf, Label Name, Label Zustand) fach, bool frei)
    {
        if (fach.Knopf.Disabled == !frei) return;
        fach.Knopf.Disabled = !frei;
        LadeStil.Variante(fach.Name, frei ? "LadeFachtitel" : "LadeFachGesperrt");
        fach.Knopf.QueueRedraw();
    }

    /// <summary>
    /// Stellt die Lade unten mittig ins Bild (höchstens so breit wie im Thema, sonst so breit wie das Fenster) und dockt an, was an ihr hängt:
    /// die Meldungen rechts über der Lade und das Schild der Räume links. Rechnet nur, wenn sich die Bildgröße ändert.
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

    /// <summary>Jedes Bild: Lage der Lade, Porträt, Namensschild, Werte und die Fächer von Stadt bzw. Innenansicht an die Simulation angleichen.</summary>
    private void LadeWerteAnzeigen()
    {
        LadeEinpassen();
        var drinnen = _stadt.InnenKennung is not null;
        foreach (var f in _faecherStadt)
            if (f.Visible == drinnen) f.Visible = !drinnen;
        foreach (var f in _faecherInnen)
            if (f.Visible != drinnen) f.Visible = drinnen;

        var mitSpieler = _simulation.Wirtschaft is not null && _spieler is not null;
        foreach (var feld in new[] { _wertGeld.Feld, _wertAnsehen.Feld, _wertStress.Feld, _wertAp.Feld })
            if (!mitSpieler && feld.Visible) feld.Visible = false;
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
        var stand = w.StandVon(_spieler).Name;
        Setzen(_ladeName, oberhaupt is not null ? $"{oberhaupt.Vorname} · {stand}" : stand);
        var standTipp = $"{_spieler.Inhaber} · {stand}" + (w.Rat.AmtVon(_spieler) is { } amt ? $" · {amt.Name}" : "") + $"\n{WohnhausAnzeige.StandText(w, _spieler)}";
        if (_ladeNamensschild.TooltipText != standTipp) _ladeNamensschild.TooltipText = standTipp;

        LebenswegPunkteAnzeigen(w);
        _wertGeld.Feld.Visible = _wertAnsehen.Feld.Visible = true;
        Setzen(_wertGeld.Name, waehrung);
        Setzen(_wertGeld.Wert, Zahlenformat.Ganz(_spieler.Vermoegen));
        LadeStil.Variante(_wertGeld.Wert, _spieler.Reinvermoegen < 0 ? "LadeWertSchlecht" : "LadeWert");
        var geldTipp = GuldenTipp(_spieler, waehrung);
        if (_wertGeld.Feld.TooltipText != geldTipp) _wertGeld.Feld.TooltipText = geldTipp;
        Setzen(_wertAnsehen.Wert, Zahlenformat.Ganz(w.Rat.Ansehen(_spieler)));
        _wertAnsehen.Feld.TooltipText = $"Ansehen eures Hauses, aus Stand und Vermögen.\n{WohnhausAnzeige.StandText(w, _spieler)}";
        var stress = oberhaupt is not null && w.Familien.StressDaten is not null && w.Familien.HatStress(oberhaupt);
        if (_wertStress.Feld.Visible != stress) _wertStress.Feld.Visible = stress;
        if (stress && oberhaupt is not null && w.Familien.StressDaten is { } stressDaten)
        {
            var stufe = w.Familien.StressStufe(oberhaupt);
            Setzen(_wertStress.Wert, Zahlenformat.Ganz(w.Familien.Stress(oberhaupt)));
            LadeStil.Variante(_wertStress.Wert, stufe > 1 ? "LadeWertSchlecht" : "LadeWert");
            Setzen(_wertStress.Zusatz, w.Familien.StressStufenname(stufe));
            _wertStress.Zusatz.Visible = true;
            _wertStress.Feld.TooltipText = CharakterAnzeige.StressTipp(w.Familien, stressDaten, oberhaupt);
        }
        var mitAp = w.HatAktionspunkte && w.Daten.Aktionspunkte is not null;
        if (_wertAp.Feld.Visible != mitAp) _wertAp.Feld.Visible = mitAp;
        if (w.Daten.Aktionspunkte is { } ap)
        {
            Setzen(_wertAp.Wert, $"{_spieler.Aktionspunkte.Vorrat}/{ap.Hoechstens}");
            _wertAp.Feld.TooltipText = $"Aktionspunkte: {_spieler.Aktionspunkte.Vorrat} von höchstens {ap.Hoechstens}, +{w.AktionspunkteJeRunde + w.ApBonus(_spieler)} zum Jahreswechsel.\n"
                                       + WohnhausAnzeige.ApTipp;
        }
    }

    /// <summary>Fächer freigeben, das offene Fach eindrücken; die Zustandszeilen viermal je Sekunde (<c>Hauptszene.Faecher.cs</c>).</summary>
    private void LadeAnzeigen(double delta)
    {
        if (_simulation.Wirtschaft is not { } w) return;
        var mitSpieler = _spieler is not null;
        FachFreigeben(_fachFamilie, mitSpieler);
        FachFreigeben(_fachRat, w.Rat.IstAktiv && mitSpieler);
        FachFreigeben(_fachBauen, w.Bauen.IstAktiv && mitSpieler);
        // Drinnen: Die Fächer der Werkstatt gelten nur im eigenen Betrieb (Rathaus, Dom, Wohnhaus und fremde Werkstätten haben sie nicht)
        var eigener = _stadt.InnenBetrieb is { } innen && innen.Konto == _spieler ? innen : null;
        foreach (var fach in new[] { _fachPersonal, _fachProduktion, _fachLager, _fachKarren, _fachTalentbaum })
            FachFreigeben(fach, eigener is not null);
        Setzen(_fachProduktion.Zustand, eigener is { IstWerkstatt: true } ? eigener.Produkt : "");

        var eigene = w.Betriebe.Where(b => b.Konto == _spieler).ToList();
        FachFreigeben(_fachBetriebe, eigene.Count > 0);
        // Das Fach, dessen Lade-Fenster offen ist, bleibt eingedrückt
        foreach (var (fach, name) in new[] { (_fachFamilie, "familie"), (_fachBetriebe, "betriebe"), (_fachBauen, "bauen"), (_fachRat, "rat"), (_fachBericht, "bericht"),
                     (_fachLexikon, "lexikon"), (_fachPersonal, "personal"), (_fachProduktion, "produktion"), (_fachLager, "lager"), (_fachKarren, "karren"),
                     (_fachTalentbaum, "talentbaum"), (_fachStadt, "") })
        {
            var offen = name.Length > 0 && _ladeFenster.Visible && _ladeFenster.Fach == name;
            if (fach.Knopf.ButtonPressed == offen) continue;
            fach.Knopf.SetPressedNoSignal(offen);
            fach.Knopf.QueueRedraw();
        }
        _leisteSeit += delta;
        if (_leisteSeit < 0.25) return;
        _leisteSeit = 0;
        // Zustand des Fachs „Betriebe“ wie im Entwurf: „1 läuft · 1 steht“
        var stehen = eigene.Count(b => BetriebsUebersicht.Zustand(b).Warnung);
        var laufen = eigene.Count - stehen;
        var teile = new List<string>();
        if (laufen > 0) teile.Add(laufen == 1 ? "1 läuft" : $"{laufen} laufen");
        if (stehen > 0) teile.Add(stehen == 1 ? "1 steht" : $"{stehen} stehen");
        FachZustand(_fachBetriebe, string.Join(" · ", teile), stehen > 0);
        if (_spieler is not null) FachZustaendeAnzeigen(w, _spieler);
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
