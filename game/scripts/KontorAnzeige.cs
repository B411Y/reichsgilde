using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das Kontor: zeigt Vermögen, Stand, alle eigenen Betriebe und die Jahresrechnung an (die Preise stehen im Marktfenster).
/// Reine Anzeige – alle Zahlen kommen aus dem Simulationskern.
/// Die Bedienelemente werden hier im Code erzeugt, damit die Szene übersichtlich bleibt.
/// </summary>
public partial class KontorAnzeige : PanelContainer
{
    private Simulation? _simulation;
    private Konto? _spieler;
    private Konto? _rivale;

    private Label _titel = null!;
    private WappenBild _titelWappen = null!;
    private Label _vermoegen = null!;
    private Label _standZeile = null!;
    private Label _statutZeile = null!;
    private HBoxContainer _wohnhausZeile = null!;
    private Label _wohnhausText = null!;
    private Button _ausbauKnopf = null!;
    private Button _hauswaechterKnopf = null!;
    private Label _apZeile = null!;
    private Label _familienZeile = null!;
    private VBoxContainer _betriebsliste = null!;
    private readonly List<(Betrieb Betrieb, Label Kopf, Label Zustand)> _betriebsZeilen = new();
    private Label _diesesJahr = null!;
    private Label _vorjahr = null!;
    private Label _rivaleZeile = null!;

    /// <summary>Der Spieler möchte einen seiner Betriebe verwalten (⚒-Knopf im Kontor).</summary>
    public event Action<Betrieb>? VerwaltenGewuenscht;

    /// <summary>Der Spieler möchte die Familientafel öffnen.</summary>
    public event Action? FamilieGewuenscht;

    /// <summary>Der Spieler möchte ins Hinterzimmer.</summary>
    public event Action? HinterzimmerGewuenscht;

    /// <summary>Der Spieler möchte das Marktfenster sehen.</summary>
    public event Action? MarktGewuenscht;

    /// <summary>Ergebnis eines Befehls aus dem Kontor (Beruf erlernen) – für das Protokoll.</summary>
    public event Action<string>? Gemeldet;

    private HBoxContainer _berufZeile = null!;
    private Label _berufText = null!;
    private Button _lernenKnopf = null!;
    private OptionButton _berufWahl = null!;
    private readonly List<string> _lernbar = new();
    private string? _lernbarerBeruf => _berufWahl.Selected >= 0 && _berufWahl.Selected < _lernbar.Count ? _lernbar[_berufWahl.Selected] : null;
    private VBoxContainer _meisterListe = null!;
    private readonly List<(string Beruf, Label Text, Button Pruefung)> _meisterZeilen = new();
    private HBoxContainer _kreditZeile = null!;
    private Label _kreditText = null!;
    private OptionButton _geberWahl = null!;
    private readonly List<Konto> _geber = new();
    private Zahlfeld _kreditBetrag = null!;
    private OptionButton _kreditJahre = null!;
    private Button _leihenKnopf = null!;
    private Button _abloesenKnopf = null!;

    public override void _Ready()
    {
        var rand = new MarginContainer();
        foreach (var seite in new[] { "left", "right", "top", "bottom" })
            rand.AddThemeConstantOverride($"margin_{seite}", 22);
        AddChild(rand);

        var spalte = new VBoxContainer();
        spalte.AddThemeConstantOverride("separation", 6);
        rand.AddChild(spalte);

        // Kopf: Wappen des Hauses und Titel
        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 10);
        spalte.AddChild(kopf);
        _titelWappen = WappenBild.Neu(null, 40);
        kopf.AddChild(_titelWappen);
        _titel = NeuesLabel(kopf, 22);
        _titel.VerticalAlignment = VerticalAlignment.Center;
        _vermoegen = NeuesLabel(spalte, 26);
        _vermoegen.MouseFilter = MouseFilterEnum.Stop; // für den Tooltip (Bargeld, Kasse, Schulden)
        _standZeile = NeuesLabel(spalte, 14, new Color(0.95f, 0.82f, 0.45f));
        _wohnhausZeile = new HBoxContainer();
        _wohnhausZeile.AddThemeConstantOverride("separation", 8);
        spalte.AddChild(_wohnhausZeile);
        _wohnhausText = NeuesLabel(_wohnhausZeile, 14);
        _wohnhausText.MouseFilter = MouseFilterEnum.Stop; // für den Tooltip
        _ausbauKnopf = new Button { FocusMode = FocusModeEnum.None };
        _ausbauKnopf.AddThemeFontSizeOverride("font_size", 13);
        _ausbauKnopf.Pressed += () => Senden(new WohnhausAusbauen());
        _wohnhausZeile.AddChild(_ausbauKnopf);
        _hauswaechterKnopf = new Button { FocusMode = FocusModeEnum.None };
        _hauswaechterKnopf.AddThemeFontSizeOverride("font_size", 13);
        _hauswaechterKnopf.Pressed += () => { if (_spieler is not null) Senden(new HauswaechterSetzen(!_spieler.Hauswaechter)); };
        _wohnhausZeile.AddChild(_hauswaechterKnopf);
        _statutZeile = NeuesLabel(spalte, 13, new Color(0.75f, 0.75f, 0.75f));
        _apZeile = NeuesLabel(spalte, 14, new Color(0.95f, 0.82f, 0.45f));
        _apZeile.MouseFilter = MouseFilterEnum.Stop; // für den Tooltip
        _berufZeile = new HBoxContainer();
        _berufZeile.AddThemeConstantOverride("separation", 8);
        spalte.AddChild(_berufZeile);
        _berufText = NeuesLabel(_berufZeile, 14);
        _berufWahl = new OptionButton { FocusMode = FocusModeEnum.None, TooltipText = "Welches Handwerk soll das Haus als Nächstes lernen?" };
        _berufWahl.AddThemeFontSizeOverride("font_size", 13);
        _berufZeile.AddChild(_berufWahl);
        _lernenKnopf = new Button { FocusMode = FocusModeEnum.None };
        _lernenKnopf.AddThemeFontSizeOverride("font_size", 13);
        _lernenKnopf.Pressed += BerufLernen;
        _berufZeile.AddChild(_lernenKnopf);
        _meisterListe = new VBoxContainer();
        _meisterListe.AddThemeConstantOverride("separation", 2);
        spalte.AddChild(_meisterListe);

        // Kredit beim Geldleiher: leihen oder den laufenden Kredit ablösen
        _kreditZeile = new HBoxContainer();
        _kreditZeile.AddThemeConstantOverride("separation", 8);
        spalte.AddChild(_kreditZeile);
        _kreditText = NeuesLabel(_kreditZeile, 13, new Color(0.85f, 0.85f, 0.85f));
        _kreditText.MouseFilter = MouseFilterEnum.Stop; // für den Tooltip
        _geberWahl = new OptionButton { FocusMode = FocusModeEnum.None, TooltipText = "Bei welchem Geldleiher?" };
        _geberWahl.AddThemeFontSizeOverride("font_size", 13);
        _kreditZeile.AddChild(_geberWahl);
        _kreditBetrag = new Zahlfeld { MinValue = 100, MaxValue = 1_000_000, Step = 100, Value = 1_000, CustomMinimumSize = new Vector2(120, 0) };
        _kreditZeile.AddChild(_kreditBetrag);
        _kreditJahre = new OptionButton { FocusMode = FocusModeEnum.None, TooltipText = "Laufzeit: Jedes Jahresende sind Zinsen und eine gleiche Rate fällig." };
        _kreditJahre.AddThemeFontSizeOverride("font_size", 13);
        _kreditZeile.AddChild(_kreditJahre);
        _leihenKnopf = new Button { FocusMode = FocusModeEnum.None };
        _leihenKnopf.AddThemeFontSizeOverride("font_size", 13);
        _leihenKnopf.Pressed += Leihen;
        _kreditZeile.AddChild(_leihenKnopf);
        _abloesenKnopf = new Button { FocusMode = FocusModeEnum.None };
        _abloesenKnopf.AddThemeFontSizeOverride("font_size", 13);
        _abloesenKnopf.Pressed += () => Senden(new KreditTilgen());
        _kreditZeile.AddChild(_abloesenKnopf);
        var familie = new HBoxContainer();
        spalte.AddChild(familie);
        var familienKnopf = new Button { Text = "Familie", FocusMode = FocusModeEnum.None, TooltipText = "Familie, Brautschau und Testament" };
        familienKnopf.Pressed += () => FamilieGewuenscht?.Invoke();
        familie.AddChild(familienKnopf);
        var hinterzimmer = new Button { Text = "Hinterzimmer", FocusMode = FocusModeEnum.None, TooltipText = "Banden, Erpresserbriefe und Wächter" };
        hinterzimmer.Pressed += () => HinterzimmerGewuenscht?.Invoke();
        familie.AddChild(hinterzimmer);
        familie.MoveChild(hinterzimmer, 1);
        var markt = new Button { Text = "Marktplatz", FocusMode = FocusModeEnum.None, TooltipText = "Alle Waren mit Preisen" };
        markt.Pressed += () => MarktGewuenscht?.Invoke();
        familie.AddChild(markt);
        familie.MoveChild(markt, 2);
        _familienZeile = NeuesLabel(familie, 14);
        // Je Betrieb zwei Zeilen und ein ⚒-Knopf – neu gebaut, sobald ein Betrieb dazukommt.
        _betriebsliste = new VBoxContainer();
        _betriebsliste.AddThemeConstantOverride("separation", 2);
        spalte.AddChild(_betriebsliste);
        _diesesJahr = NeuesLabel(spalte, 15);
        _vorjahr = NeuesLabel(spalte, 15);
        _rivaleZeile = NeuesLabel(spalte, 14, new Color(0.75f, 0.75f, 0.75f));
        _rivaleZeile.AutowrapMode = TextServer.AutowrapMode.WordSmart; // lange KI-Züge umbrechen statt das Kontor zu verbreitern
        _rivaleZeile.CustomMinimumSize = new Vector2(360, 0);

    }

    private static Label NeuesLabel(Container eltern, int groesse, Color? farbe = null)
    {
        var label = new Label();
        label.AddThemeFontSizeOverride("font_size", groesse);
        if (farbe is { } f) label.AddThemeColorOverride("font_color", f);
        eltern.AddChild(label);
        return label;
    }

    private void BetriebsZeilenBauen(List<Betrieb> eigene)
    {
        UiHilfe.KinderEntfernen(_betriebsliste);
        _betriebsZeilen.Clear();
        foreach (var betrieb in eigene)
        {
            var zeile = new HBoxContainer();
            _betriebsliste.AddChild(zeile);
            var knopf = new Button { Text = "⚒", FocusMode = FocusModeEnum.None, TooltipText = $"{betrieb.Name} verwalten" };
            knopf.Pressed += () => VerwaltenGewuenscht?.Invoke(betrieb);
            zeile.AddChild(knopf);
            var kopf = NeuesLabel(zeile, 15);
            var zustand = NeuesLabel(_betriebsliste, 13, new Color(0.8f, 0.8f, 0.8f));
            _betriebsZeilen.Add((betrieb, kopf, zustand));
        }
    }

    /// <summary>„Jakob (31, rüstig) · verheiratet · 2 Kinder · Erbe: Agnes".</summary>
    /// <summary>„Handwerk: Schmied", eine Auswahl der noch fremden Berufe und ein Knopf, um den gewählten zu erlernen.</summary>
    private void BerufZeileAktualisieren(Stadtwirtschaft w)
    {
        _berufZeile.Visible = w.Daten.Berufe.Count > 1 && !_spieler!.IstErloschen;
        if (!_berufZeile.Visible) return;
        var handwerk = $"Handwerk: {string.Join(", ", _spieler!.Berufe.Select(b => b + UiHilfe.VolksZusatz(w, _spieler, b)))}";
        if (_berufText.Text != handwerk) _berufText.Text = handwerk;
        var lernbar = w.Daten.Berufe.Keys.Where(b => !_spieler.Beherrscht(b)).OrderBy(b => b, StringComparer.Ordinal).ToList();
        if (!lernbar.SequenceEqual(_lernbar))
        {
            var vorher = _lernbarerBeruf;
            _lernbar.Clear();
            _lernbar.AddRange(lernbar);
            _berufWahl.Clear();
            foreach (var b in lernbar) _berufWahl.AddItem(b + UiHilfe.VolksZusatz(w, _spieler, b));
            if (lernbar.Count > 0) _berufWahl.Select(Math.Max(0, vorher is null ? 0 : lernbar.IndexOf(vorher)));
        }
        _berufWahl.Visible = _lernenKnopf.Visible = _lernbarerBeruf is not null;
        if (_lernbarerBeruf is not { } beruf) return;
        var befehl = new BerufErlernen(beruf);
        var daten = w.Daten.Berufe[beruf];
        var ap = w.ApKosten(_spieler, befehl);
        var text = $"erlernen ({Zahlenformat.Ganz(daten.Lehrgeld)} {w.Daten.Allgemein.Waehrung}{(ap > 0 ? $" · {ap} AP" : "")})";
        if (_lernenKnopf.Text != text) _lernenKnopf.Text = text;
        var fehlt = _spieler.Vermoegen < daten.Lehrgeld ? "Für das Lehrgeld fehlt Geld."
            : !w.ApReichen(_spieler, befehl) ? $"Nicht genug Aktionspunkte ({_spieler.Aktionspunkte.Vorrat} von {ap})." : null;
        _lernenKnopf.Disabled = fehlt is not null;
        var volk = UiHilfe.VolksZusatz(w, _spieler, beruf);
        var tipp = fehlt ?? $"{daten.Beschreibung}\nDanach dürft ihr Werkstätten und Umlandbetriebe dieses Handwerks erwerben." +
                   (volk == "" ? "" : $"\nEuer Volk{volk}: So viel leisten eure Betriebe in diesem Handwerk mehr oder weniger.");
        if (_lernenKnopf.TooltipText != tipp) _lernenKnopf.TooltipText = tipp;
    }

    /// <summary>Je beherrschtem Beruf: Meistergrad, Erfahrung bis zur nächsten Prüfung und der Knopf dafür.</summary>
    private void MeisterAktualisieren(Stadtwirtschaft w)
    {
        _meisterListe.Visible = w.Daten.Meistergrade is not null && !_spieler!.IstErloschen;
        if (!_meisterListe.Visible) return;
        var berufe = _spieler!.Berufe.ToList();
        if (!berufe.SequenceEqual(_meisterZeilen.Select(z => z.Beruf)))
        {
            UiHilfe.KinderEntfernen(_meisterListe);
            _meisterZeilen.Clear();
            foreach (var beruf in berufe)
            {
                var zeile = new HBoxContainer();
                zeile.AddThemeConstantOverride("separation", 8);
                _meisterListe.AddChild(zeile);
                var text = NeuesLabel(zeile, 13, new Color(0.85f, 0.85f, 0.85f));
                var knopf = new Button { FocusMode = FocusModeEnum.None };
                knopf.AddThemeFontSizeOverride("font_size", 13);
                knopf.Pressed += () => Pruefen(beruf);
                zeile.AddChild(knopf);
                _meisterZeilen.Add((beruf, text, knopf));
            }
        }
        var waehrung = w.Daten.Allgemein.Waehrung;
        foreach (var (beruf, text, knopf) in _meisterZeilen)
        {
            var grad = _spieler.Meistergrad(beruf);
            var erfahrung = Zahlenformat.Ganz(Math.Floor(_spieler.Erfahrung(beruf)));
            var p = w.NaechstePruefung(_spieler, beruf);
            var zeile = $"{beruf}: {grad}. Meistergrad · Erfahrung {erfahrung}" + (p is not null ? $" / {Zahlenformat.Ganz(p.Erfahrung)} Std." : " Std. · höchster Grad");
            if (text.Text != zeile) text.Text = zeile;
            knopf.Visible = p is not null;
            if (p is null) continue;
            var befehl = new MeisterpruefungAblegen(beruf);
            var ap = w.ApKosten(_spieler, befehl);
            var beschriftung = $"Prüfung zum {grad + 1}. Grad ({Zahlenformat.Ganz(p.Gebuehr)} {waehrung}{(p.Ap > 0 && w.HatAktionspunkte ? $" · {p.Ap} AP" : "")})";
            if (knopf.Text != beschriftung) knopf.Text = beschriftung;
            var grund = w.MeisterpruefungVerboten(_spieler, beruf)
                        ?? (!w.ApReichen(_spieler, befehl) ? $"Nicht genug Aktionspunkte ({_spieler.Aktionspunkte.Vorrat} von {ap})." : null);
            knopf.Disabled = grund is not null;
            var m = w.Daten.Meistergrade!;
            var nutzen = Enumerable.Range(1, 3).FirstOrDefault(st => m.GradFuerStufe(st) == grad + 1) is var stufe and > 1
                ? $"Mit dem {grad + 1}. Grad dürft ihr eure Werkstätten auf Stufe {stufe} ausbauen."
                : grad + 1 == m.ApBonusAbGrad ? $"Der {grad + 1}. Grad bringt {m.ApBonus} Aktionspunkt mehr je Jahr."
                : "Ein Schritt auf dem Weg zur nächsten Gebäudestufe.";
            var tipp = (grund is not null ? grund + "\n\n" : "") + "Erfahrung sammelt das Haus mit jedem Stück, das seine Betriebe herstellen oder fördern " +
                       "(Arbeitsstunden eines Gesellen). " + nutzen;
            if (knopf.TooltipText != tipp) knopf.TooltipText = tipp;
        }
    }

    private void Pruefen(string beruf)
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null) return;
        var e = w.Ausfuehren(_spieler, new MeisterpruefungAblegen(beruf));
        Gemeldet?.Invoke(e.Erfolg ? e.Meldung : "✗ " + e.Meldung);
    }

    /// <summary>Zeile „Wohnhaus: Bürgerhaus“ mit Knopf zum Ausbauen (gesperrt mit Grund aus dem Kern).</summary>
    private void WohnhausAktualisieren(Stadtwirtschaft w, string waehrung)
    {
        _wohnhausZeile.Visible = w.Wohnen.IstAktiv;
        if (!w.Wohnen.IstAktiv || _spieler is null) return;
        var stufe = w.Wohnen.Stufe(_spieler)!;
        var instand = _spieler.WohnhausInstandBis is { } bis && _simulation is { } s
            ? Math.Max(0, bis - s.Zeit.VergangeneMinuten) : (long?)null;
        var text = $"Wohnhaus: {stufe.Name}"
                   + (instand is { } rest0 ? $" – nach dem Brand in Instandsetzung (noch {rest0 / 60}:{rest0 % 60:00} h, so lange fehlt das Ansehen)"
                      : stufe.Ansehen > 0 ? $" (+{Zahlenformat.Ganz(stufe.Ansehen)} Ansehen, Unterhalt {Zahlenformat.Ganz(stufe.Unterhalt)} {waehrung} im Jahr)" : "");
        if (_wohnhausText.Text != text) _wohnhausText.Text = text;
        _wohnhausText.TooltipText = "Das Wohnhaus gehört dem Haus, nicht einem Betrieb. Es bringt Ansehen und ist Pflicht für den Stand: " +
                                    "Wer aufsteigen will, braucht vorher das passende Haus. Ausbauen darf man höchstens eine Stufe über dem eigenen Stand.\n" +
                                    $"Was in den Ausbau geflossen ist, zählt als Hauswert zum Reinvermögen (jetzt {Zahlenformat.Ganz(_spieler.Hauswert)} {waehrung}) – " +
                                    "wer baut, fällt also nicht unter die Schwelle seines Standes.";

        _hauswaechterKnopf.Visible = w.Gauner.GegenWohnhaus && _spieler.IstAktiv;
        if (_hauswaechterKnopf.Visible)
        {
            var wtext = _spieler.Hauswaechter ? "Hauswächter entlassen" : $"Hauswächter ({Zahlenformat.Ganz(w.Gauner.Daten.WaechterKosten)} {waehrung}/Jahr)";
            if (_hauswaechterKnopf.Text != wtext) _hauswaechterKnopf.Text = wtext;
            _hauswaechterKnopf.TooltipText = "Ein Hauswächter halbiert die Chance auf Einbruch und Brand am Wohnhaus und sieht öfter, wer es war.";
        }

        var naechste = w.Wohnen.NaechsteStufe(_spieler);
        _ausbauKnopf.Visible = naechste is not null && _spieler.IstAktiv;
        if (naechste is null) return;
        string knopf;
        if (w.Wohnen.FertigAb(_spieler) is { } ab && _simulation is { } sim)
        {
            var rest = Math.Max(0, ab - sim.Zeit.VergangeneMinuten);
            knopf = $"Ausbau zum {naechste.Name} läuft (noch {rest / 60}:{rest % 60:00} h)";
        }
        else knopf = $"Ausbauen zum {naechste.Name} ({Zahlenformat.Ganz(naechste.Ausbau)} {waehrung})";
        if (_ausbauKnopf.Text != knopf) _ausbauKnopf.Text = knopf;
        var grund = w.Wohnen.AusbauVerboten(_spieler);
        _ausbauKnopf.Disabled = grund is not null;
        var tipp = grund ?? $"Ausbau zum {naechste.Name}: +{Zahlenformat.Ganz(naechste.Ansehen)} Ansehen, Unterhalt {Zahlenformat.Ganz(naechste.Unterhalt)} {waehrung} im Jahr. " +
                   $"Bauzeit {w.Wohnen.Daten!.BauzeitMinuten / 60.0:0.#} Stunden.";
        if (_ausbauKnopf.TooltipText != tipp) _ausbauKnopf.TooltipText = tipp;
    }

    private void Senden(Befehl befehl)
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null) return;
        var e = w.Ausfuehren(_spieler, befehl);
        Gemeldet?.Invoke(e.Erfolg ? e.Meldung : "✗ " + e.Meldung);
    }

    private void Leihen()
    {
        if (_geberWahl.Selected < 0 || _geberWahl.Selected >= _geber.Count) return;
        Senden(new KreditAufnehmen(_geber[_geberWahl.Selected].Inhaber, _kreditBetrag.Value, _kreditJahre.Selected + 1));
    }

    /// <summary>
    /// Läuft ein Kredit: Rest, Zins, nächste Rate und der Knopf zum Ablösen.
    /// Sonst: die Geldleiher der Stadt mit Zins und Höchstbetrag, Betrag, Laufzeit und „leihen".
    /// </summary>
    private void KreditAktualisieren(Stadtwirtschaft w)
    {
        var bank = w.Bank;
        _kreditZeile.Visible = bank.IstAktiv && _spieler!.IstAktiv;
        if (!_kreditZeile.Visible) return;
        var waehrung = w.Daten.Allgemein.Waehrung;
        if (bank.KreditVon(_spieler!) is { } k)
        {
            _geberWahl.Visible = _kreditBetrag.Visible = _kreditJahre.Visible = _leihenKnopf.Visible = false;
            _abloesenKnopf.Visible = true;
            var text = $"Kredit bei {k.Geber.Inhaber}: noch {Zahlenformat.Ganz(k.Rest)} {waehrung} zu {Zahlenformat.Ganz(k.Satz * 100)} % · " +
                       $"Jahresende: Rate {Zahlenformat.Ganz(k.Tilgungsrate)} + Zinsen ≈ {Zahlenformat.Ganz(k.Aufgelaufen)}";
            if (_kreditText.Text != text) _kreditText.Text = text;
            var abloesung = bank.Abloesung(k);
            var knopf = $"ablösen ({Zahlenformat.Ganz(abloesung)})";
            if (_abloesenKnopf.Text != knopf) _abloesenKnopf.Text = knopf;
            _abloesenKnopf.Disabled = _spieler!.Vermoegen < abloesung;
            _kreditText.TooltipText = "Fehlt am Jahresende das Bargeld, pfändet der Büttel Lagerware zu " +
                                      $"{Zahlenformat.Ganz(bank.Daten.Kredit.PfandAnteil * 100)} % des Marktpreises.";
            return;
        }
        _abloesenKnopf.Visible = false;
        var geber = bank.Geldleiher.Where(g => g != _spieler).OrderBy(g => g.Inhaber, StringComparer.Ordinal).ToList();
        if (!geber.SequenceEqual(_geber))
        {
            var vorher = _geberWahl.Selected >= 0 && _geberWahl.Selected < _geber.Count ? _geber[_geberWahl.Selected] : null;
            _geber.Clear();
            _geber.AddRange(geber);
            _geberWahl.Clear();
            foreach (var g in geber) _geberWahl.AddItem(g.Inhaber);
            if (geber.Count > 0) _geberWahl.Select(Math.Max(0, vorher is null ? 0 : geber.IndexOf(vorher)));
        }
        if (_kreditJahre.ItemCount != bank.Daten.Kredit.LaufzeitHoechstens)
        {
            _kreditJahre.Clear();
            for (var j = 1; j <= bank.Daten.Kredit.LaufzeitHoechstens; j++) _kreditJahre.AddItem(j == 1 ? "1 Jahr" : $"{j} Jahre");
            _kreditJahre.Select(Math.Min(1, _kreditJahre.ItemCount - 1));
        }
        _geberWahl.Visible = _kreditBetrag.Visible = _kreditJahre.Visible = _leihenKnopf.Visible = geber.Count > 0;
        if (geber.Count == 0)
        {
            const string keiner = "Kredit: In der Stadt verleiht noch niemand Geld.";
            if (_kreditText.Text != keiner) _kreditText.Text = keiner;
            return;
        }
        for (var i = 0; i < geber.Count; i++)
            _geberWahl.SetItemText(i, $"{geber[i].Inhaber} · {Zahlenformat.Ganz(bank.Zinssatz(geber[i]) * 100)} % · bis {Zahlenformat.Ganz(bank.Hoechstkredit(geber[i], _spieler!))}");
        const string kopf = "Kredit:";
        if (_kreditText.Text != kopf) _kreditText.Text = kopf;
        var befehl = new KreditAufnehmen("", 0, 1);
        var ap = w.ApKosten(_spieler!, befehl);
        var beschriftung = $"leihen{(ap > 0 ? $" ({ap} AP)" : "")}";
        if (_leihenKnopf.Text != beschriftung) _leihenKnopf.Text = beschriftung;
        var g0 = _geber[Math.Clamp(_geberWahl.Selected, 0, _geber.Count - 1)];
        var grund = bank.KreditVerboten(g0, _spieler!, _kreditBetrag.Value, _kreditJahre.Selected + 1)
                    ?? (!w.ApReichen(_spieler!, befehl) ? $"Nicht genug Aktionspunkte ({_spieler!.Aktionspunkte.Vorrat} von {ap})." : null);
        _leihenKnopf.Disabled = grund is not null;
        var satz = bank.Zinssatz(g0);
        var tipp = (grund is not null ? grund + "\n\n" : "") +
                   $"Zinsen laufen stündlich auf ({Zahlenformat.Ganz(satz * 100)} % im Jahr); am Jahresende sind Zinsen und eine gleiche Rate fällig. " +
                   $"Höchstens {Zahlenformat.Ganz(bank.Daten.Kredit.KreditwuerdigAnteil * 100)} % eures Reinvermögens. Geliehenes Geld zählt nicht zum Vermögen." +
                   (bank.IstWucher(satz) ? "\nDieser Zins ist Wucher – euer Schuldschein wäre ein Beweis gegen den Geldleiher." : "");
        if (_leihenKnopf.TooltipText != tipp) _leihenKnopf.TooltipText = tipp;
    }

    private void BerufLernen()
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null || _lernbarerBeruf is null) return;
        var e = w.Ausfuehren(_spieler, new BerufErlernen(_lernbarerBeruf));
        Gemeldet?.Invoke(e.Erfolg ? e.Meldung : "✗ " + e.Meldung);
    }

    private string FamilienText(Stadtwirtschaft w)
    {
        var buch = w.Familien;
        if (_spieler is null || buch.Von(_spieler) is not { } f) return "";
        var o = f.Oberhaupt;
        var kinder = f.Kinder.Count(k => k.Lebt);
        var erbe = buch.Erbe(f);
        var kranke = f.Lebende.Count(p => p.Krankheit is not null);
        return $"{o.Vorname} ({o.Alter(buch.Jahr)}, {(o.Krankheit is { } k ? $"krank: {k.Name}" : buch.Verfassung(o))})"
               + (f.Ehepartner is { } e ? $" · verheiratet mit {e.Vorname}" : " · ledig")
               + (kinder > 0 ? $" · {kinder} {(kinder == 1 ? "Kind" : "Kinder")}" : "")
               + (erbe is null ? " · ohne Erben!" : erbe == f.Ehepartner ? "" : $" · Erbe: {erbe.Vorname}")
               + (kranke > 0 && o.Krankheit is null ? $" · {kranke} krank" : "");
    }

    /// <summary>Der letzte Zug der KI, ohne den Namen davor (der steht schon in der Zeile).</summary>
    private string RivalenZug(Konto rivale)
    {
        var ki = _simulation?.Wirtschaft?.Rivalen.FirstOrDefault(r => r.Konto == rivale);
        if (ki is null || ki.Entscheidungen.Count == 0) return "";
        return "\nLetzter Zug: " + ki.Entscheidungen[0].Replace($"{rivale.Inhaber}: ", "");
    }

    /// <summary>Verbindet die Anzeige mit der Simulation. Wird von der Hauptszene aufgerufen.</summary>
    public void Verbinden(Simulation simulation, Konto spieler, Konto? rivale)
    {
        _simulation = simulation;
        _spieler = spieler;
        _rivale = rivale;
    }

    /// <summary>Überträgt die aktuellen Zahlen auf die Anzeige.</summary>
    public void Aktualisieren()
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null)
            return;

        var waehrung = w.Daten.Allgemein.Waehrung;
        _titelWappen.Wappen = _spieler.Wappen;
        _titel.Text = $"Kontor · {_spieler.Inhaber} · {w.StandVon(_spieler).Name}"
                      + (w.Rat.AmtVon(_spieler) is { } amt ? $" · {amt.Name}" : "");
        // Mit Kasse oder Kredit: Bargeld groß, darunter klein, was dem Haus wirklich gehört.
        var verliehen = Math.Abs(_spieler.Forderungen) > 0.5 || Math.Abs(_spieler.Schulden) > 0.5 || _spieler.Hauswert > 0.5;
        _vermoegen.Text = $"{Zahlenformat.Ganz(_spieler.Vermoegen)} {waehrung}"
                          + (verliehen ? $" bar · Reinvermögen {Zahlenformat.Ganz(_spieler.Reinvermoegen)}" : "");
        _vermoegen.AddThemeColorOverride("font_color", _spieler.Reinvermoegen < 0 ? new Color(0.95f, 0.45f, 0.4f) : new Color(1, 1, 1));
        _vermoegen.TooltipText = verliehen
            ? $"Bargeld {Zahlenformat.Ganz(_spieler.Vermoegen)} + verliehen {Zahlenformat.Ganz(_spieler.Forderungen)} − Schulden {Zahlenformat.Ganz(_spieler.Schulden)}" +
              $" + Wohnhaus {Zahlenformat.Ganz(_spieler.Hauswert)} {waehrung}.\n" +
              "Stand, Rangliste und Ansehen richten sich nach dem Reinvermögen; bezahlt wird mit Bargeld."
            : "";
        _standZeile.Text = _spieler.IstBankrott ? "Bankrott" : _spieler.IstErloschen ? "Das Haus ist erloschen"
            : w.Justiz.IstAktiv && w.Justiz.KerkerBis(_spieler) is int kerker ? $"Das Oberhaupt sitzt bis Ende {kerker} im Kerker"
            : w.NaechsterStand(_spieler) is not { } neu ? "Höchster Stand erreicht"
            : _spieler.Reinvermoegen >= neu.Schwelle && w.Wohnen.StandVerboten(_spieler, _spieler.Stand + 1) is { } ohneHaus ? ohneHaus
            : _spieler.Reinvermoegen >= neu.Schwelle ? $"Aufstieg zum {neu.Name} im Rathaus möglich!"
            : $"Nächster Stand: {neu.Name} ab {Zahlenformat.Ganz(neu.Schwelle)} {waehrung} (noch {Zahlenformat.Ganz(neu.Schwelle - Math.Max(0, _spieler.Reinvermoegen))})";

        _familienZeile.Text = FamilienText(w);
        WohnhausAktualisieren(w, waehrung);
        BerufZeileAktualisieren(w);
        MeisterAktualisieren(w);
        KreditAktualisieren(w);

        _apZeile.Visible = w.HatAktionspunkte;
        if (w.Daten.Aktionspunkte is { } ap)
        {
            var bonus = w.ApBonus(_spieler);
            _apZeile.Text = $"Aktionspunkte: {_spieler.Aktionspunkte.Vorrat} von höchstens {ap.Hoechstens} · +{w.AktionspunkteJeRunde + bonus} zum Jahreswechsel"
                            + (bonus > 0 ? $" (davon {bonus} als Meister oder Geldleiher)" : "");
            _apZeile.TooltipText = "Persönliche Handlungen kosten Aktionspunkte: Werben und Heiraten, Ratsherren beschenken, sich bewerben (je 1), " +
                                   "Statut-Antrag (2), Spitzel (1), Anklage (2), Bande anheuern (1), Erpresserbrief (2), Talent üben (2–6), Beruf erlernen (4), Meisterprüfung (2–4).\n" +
                                   "Betriebe, Personal, Karren, Wächter, Stand und Liegenschaften sind frei. Nicht verbrauchte AP bleiben – bis zur Obergrenze.";
        }

        _statutZeile.Text = $"Statut {w.Statut.Kuerzel()} – {w.Statut.Name()}"
                          + (w.IstArbeitszeit(_simulation.Zeit) ? " · es wird gearbeitet" : " · Feierabend")
                          + (w.Gesundheit.IstPestjahr ? $" · PEST: {Zahlenformat.Ganz(w.Gesundheit.Krankenstand * 100)} % der Arbeitskräfte fehlen" : "");

        var eigene = w.Betriebe.Where(x => x.Konto == _spieler).ToList();
        if (eigene.Count != _betriebsZeilen.Count)
            BetriebsZeilenBauen(eigene);
        foreach (var (betrieb, kopf, zustand) in _betriebsZeilen)
        {
            var belegung = betrieb.Belegt == 0 ? "kein Personal" : string.Join(", ", betrieb.Belegung.Select(x => $"{x.Value}× {x.Key}"));
            var was = betrieb.OhneProduktion ? UiHilfe.Taetigkeit(betrieb.Artdaten.Beruf)
                : betrieb.IstWerkstatt ? betrieb.Produkt
                : betrieb.Rohstoffe.Count == 1 ? betrieb.Rohstoffe[0]
                : $"{Zahlenformat.Ganz(betrieb.Silberanteil * 100)} % {betrieb.Rohstoffe[1]}";
            var stufe = betrieb.InDerStadt && w.Daten.Meistergrade is not null && betrieb.Stufe > 1 ? $" ({betrieb.Artdaten.Name})" : "";
            kopf.Text = $"{betrieb.Name}{stufe}: {was} · {belegung}";
            var lager = string.Join(" · ", betrieb.Lager.OrderBy(l => l.Key).Select(l => $"{l.Value} {l.Key}"));
            var unterwegs = betrieb.Karren.Count(k => k.Zustand != KarrenZustand.AmBetrieb);
            zustand.Text = $"Lager: {(lager == "" ? "leer" : lager)} · Karren {unterwegs}/{betrieb.Karren.Count} unterwegs"
                           + (betrieb.Stillstand is { } still ? $"\n⚠ {still}" : "");
        }

        var laufend = _spieler.Laufend;
        var hergestellt = string.Join(", ", laufend.Hergestellt.OrderBy(x => x.Key).Select(x => $"{x.Value} {x.Key}"));
        _diesesJahr.Text = $"Dieses Jahr: Umsatz {Zahlenformat.Ganz(laufend.Umsatz)}" + (hergestellt == "" ? "" : $" · {hergestellt}");

        _vorjahr.Text = _spieler.Vorjahr is { } v
            ? $"Jahresrechnung {v.Jahr}: Gewinn {Zahlenformat.MitVorzeichen(v.Gewinn)} {waehrung}"
              + (v.Raubwert > 0 ? $" · geraubt: {Zahlenformat.Ganz(v.Raubwert)}" : "")
            : "Jahresrechnung: erst nach dem ersten Jahr";

        var rivalen = w.Rivalen.Select(r => r.Konto).ToList();
        _rivaleZeile.Text = rivalen.Count switch
        {
            0 => "",
            1 => $"Rivale {rivalen[0].Inhaber}: {Zahlenformat.Ganz(rivalen[0].Reinvermoegen)} {waehrung}" + RivalenZug(rivalen[0]),
            _ => "Rivalen: " + string.Join(" · ", rivalen.Select(r => $"{r.Inhaber} {(r.IstAktiv ? Zahlenformat.Ganz(r.Reinvermoegen) : r.IstBankrott ? "bankrott" : "erloschen")}"))
                 + (_rivale is not null ? RivalenZug(_rivale) : ""),
        };

    }
}
