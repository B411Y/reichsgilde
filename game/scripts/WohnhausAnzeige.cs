using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Tafel des eigenen Wohnhauses (PR #197, früher das Kontor rechts oben): Ein Klick auf das Wohnhaus öffnet sie rechts im Bild, mit fünf Reitern –
/// <b>Haus</b> (Stand, Wohnhaus und Ausbau, Hauswächter, Aktionspunkte, Statut), <b>Familie</b> (kurz, dazu der Knopf zum Familienbuch),
/// <b>Handwerk</b> (Berufe, Meistergrade und Prüfungen, Lernen), <b>Geld</b> (Bargeld und Reinvermögen, Kredit beim Geldleiher) und
/// <b>Hinterzimmer</b> (der Knopf zu Banden, Erpresserbriefen und Wächtern). Die großen Fenster (Familie, Hinterzimmer) bleiben in der Bildmitte.
///
/// Reine Anzeige mit Befehlsknöpfen – alle Zahlen kommen aus dem Simulationskern, jeder Knopf schickt einen Befehl an <see cref="Stadtwirtschaft.Ausfuehren"/>.
/// Die Bedienelemente werden einmal im Code erzeugt und jedes Bild nur beschriftet (Eingaben wie der Kreditbetrag bleiben so stehen).
/// </summary>
public partial class WohnhausAnzeige : PanelContainer
{
    /// <summary>Die Reiter in ihrer Reihenfolge (auch für die Startoption <c>--wohnhaus=&lt;reiter&gt;</c>).</summary>
    public enum Reiter { Haus, Familie, Handwerk, Geld, Hinterzimmer }

    private const int Breite = 400;
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.75f, 0.75f, 0.75f);

    private Simulation? _simulation;
    private Konto? _spieler;

    private TabContainer _reiter = null!;
    private Label _titel = null!;
    private WappenBild _titelWappen = null!;
    private Label _standZeile = null!;
    private Label _statutZeile = null!;
    private Label _wohnhausText = null!;
    private Button _ausbauKnopf = null!;
    private Button _hauswaechterKnopf = null!;
    private Label _apZeile = null!;
    private Label _familienZeile = null!;
    private Label _vermoegen = null!;
    private Label _vermoegenTeile = null!;
    private Label _hinterzimmerText = null!;

    /// <summary>Der Spieler möchte das Familienbuch (Brautschau, Testament) in der Bildmitte öffnen.</summary>
    public event Action? FamilieGewuenscht;

    /// <summary>Der Spieler möchte ins Hinterzimmer (Fenster in der Bildmitte).</summary>
    public event Action? HinterzimmerGewuenscht;

    /// <summary>Der Spieler hat die Tafel mit „×“ geschlossen.</summary>
    public event Action? Geschlossen;

    /// <summary>Ergebnis eines Befehls (Ausbau, Beruf, Prüfung, Kredit) – für die Meldungen.</summary>
    public event Action<string>? Gemeldet;

    private VBoxContainer _berufZeile = null!;
    private Label _berufText = null!;
    private Button _lernenKnopf = null!;
    private OptionButton _berufWahl = null!;
    private readonly List<string> _lernbar = new();
    private string? _lernbarerBeruf => _berufWahl.Selected >= 0 && _berufWahl.Selected < _lernbar.Count ? _lernbar[_berufWahl.Selected] : null;
    private VBoxContainer _meisterListe = null!;
    private readonly List<(string Beruf, Label Text, Button Pruefung)> _meisterZeilen = new();
    private VBoxContainer _kreditZeile = null!;
    private Label _kreditText = null!;
    private OptionButton _geberWahl = null!;
    private readonly List<Konto> _geber = new();
    private HBoxContainer _kreditEingabe = null!;
    private Zahlfeld _kreditBetrag = null!;
    private OptionButton _kreditJahre = null!;
    private Button _leihenKnopf = null!;
    private Button _abloesenKnopf = null!;

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.15f, 0.13f, 0.97f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
        });
        var rand = new MarginContainer();
        foreach (var seite in new[] { "left", "right", "top", "bottom" })
            rand.AddThemeConstantOverride($"margin_{seite}", 16);
        AddChild(rand);
        var spalte = new VBoxContainer { CustomMinimumSize = new Vector2(Breite, 0) };
        spalte.AddThemeConstantOverride("separation", 8);
        rand.AddChild(spalte);

        // Kopf: Wappen, Name des Hauses und Stand, rechts „×“
        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 10);
        spalte.AddChild(kopf);
        _titelWappen = WappenBild.Neu(null, 40);
        kopf.AddChild(_titelWappen);
        _titel = NeuesLabel(kopf, 18, Gold, umbruch: 300);
        _titel.VerticalAlignment = VerticalAlignment.Center;
        _titel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var zu = new Button { Text = "×", FocusMode = FocusModeEnum.None, TooltipText = "Schließen (Esc)", SizeFlagsVertical = SizeFlags.ShrinkBegin };
        zu.Pressed += () => { Hide(); Geschlossen?.Invoke(); };
        kopf.AddChild(zu);

        _reiter = new TabContainer { FocusMode = FocusModeEnum.None, ClipTabs = false }; // alle Reiter sichtbar, ohne Pfeile
        _reiter.AddThemeFontSizeOverride("font_size", 14);
        spalte.AddChild(_reiter);

        // ── Haus ──
        var haus = Seite(Reiter.Haus);
        _standZeile = NeuesLabel(haus, 14, Gold, umbruch: Breite);
        _wohnhausText = NeuesLabel(haus, 14, umbruch: Breite);
        _wohnhausText.MouseFilter = MouseFilterEnum.Stop; // für den Tooltip
        var hausKnoepfe = new HFlowContainer();
        hausKnoepfe.AddThemeConstantOverride("h_separation", 8);
        haus.AddChild(hausKnoepfe);
        _ausbauKnopf = KleinerKnopf(hausKnoepfe, () => Senden(new WohnhausAusbauen()));
        _hauswaechterKnopf = KleinerKnopf(hausKnoepfe, () => { if (_spieler is not null) Senden(new HauswaechterSetzen(!_spieler.Hauswaechter)); });
        _apZeile = NeuesLabel(haus, 14, Gold, umbruch: Breite);
        _apZeile.MouseFilter = MouseFilterEnum.Stop; // für den Tooltip
        _statutZeile = NeuesLabel(haus, 13, Grau, umbruch: Breite);

        // ── Familie ──
        var familie = Seite(Reiter.Familie);
        _familienZeile = NeuesLabel(familie, 14, umbruch: Breite);
        var familienKnopf = KleinerKnopf(familie, () => FamilieGewuenscht?.Invoke());
        familienKnopf.Text = "Familienbuch: Brautschau, Testament … (F)";
        familienKnopf.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;

        // ── Handwerk ──
        var handwerk = Seite(Reiter.Handwerk);
        _berufZeile = new VBoxContainer();
        handwerk.AddChild(_berufZeile);
        _berufText = NeuesLabel(_berufZeile, 14, umbruch: Breite);
        var lernen = new HBoxContainer();
        lernen.AddThemeConstantOverride("separation", 8);
        _berufZeile.AddChild(lernen);
        _berufWahl = new OptionButton { FocusMode = FocusModeEnum.None, TooltipText = "Welches Handwerk soll das Haus als Nächstes lernen?" };
        _berufWahl.AddThemeFontSizeOverride("font_size", 13);
        lernen.AddChild(_berufWahl);
        _lernenKnopf = KleinerKnopf(lernen, BerufLernen);
        _meisterListe = new VBoxContainer();
        _meisterListe.AddThemeConstantOverride("separation", 4);
        handwerk.AddChild(_meisterListe);

        // ── Geld ──
        var geld = Seite(Reiter.Geld);
        _vermoegen = NeuesLabel(geld, 24);
        _vermoegenTeile = NeuesLabel(geld, 13, Grau, umbruch: Breite);
        geld.AddChild(new HSeparator());
        // Kredit beim Geldleiher: leihen oder den laufenden Kredit ablösen
        _kreditZeile = new VBoxContainer();
        _kreditZeile.AddThemeConstantOverride("separation", 6);
        geld.AddChild(_kreditZeile);
        _kreditText = NeuesLabel(_kreditZeile, 13, new Color(0.85f, 0.85f, 0.85f), umbruch: Breite);
        _kreditText.MouseFilter = MouseFilterEnum.Stop; // für den Tooltip
        _geberWahl = new OptionButton { FocusMode = FocusModeEnum.None, TooltipText = "Bei welchem Geldleiher?" };
        _geberWahl.AddThemeFontSizeOverride("font_size", 13);
        _kreditZeile.AddChild(_geberWahl);
        _kreditEingabe = new HBoxContainer();
        _kreditEingabe.AddThemeConstantOverride("separation", 8);
        _kreditZeile.AddChild(_kreditEingabe);
        _kreditBetrag = new Zahlfeld { MinValue = 100, MaxValue = 1_000_000, Step = 100, Value = 1_000, CustomMinimumSize = new Vector2(120, 0) };
        _kreditEingabe.AddChild(_kreditBetrag);
        _kreditJahre = new OptionButton { FocusMode = FocusModeEnum.None, TooltipText = "Laufzeit: Jedes Jahresende sind Zinsen und eine gleiche Rate fällig." };
        _kreditJahre.AddThemeFontSizeOverride("font_size", 13);
        _kreditEingabe.AddChild(_kreditJahre);
        _leihenKnopf = KleinerKnopf(_kreditEingabe, Leihen);
        _abloesenKnopf = KleinerKnopf(_kreditZeile, () => Senden(new KreditTilgen()));
        _abloesenKnopf.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;

        // ── Hinterzimmer ──
        var hinterzimmer = Seite(Reiter.Hinterzimmer);
        _hinterzimmerText = NeuesLabel(hinterzimmer, 14, umbruch: Breite);
        var hinterKnopf = KleinerKnopf(hinterzimmer, () => HinterzimmerGewuenscht?.Invoke());
        hinterKnopf.Text = "Ins Hinterzimmer (H)";
        hinterKnopf.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;

        Rollbereich.Einhuellen(this);
        Hide();
    }

    /// <summary>Eine Seite des Reiters – der Name des Knotens ist die Beschriftung des Reiters.</summary>
    private VBoxContainer Seite(Reiter reiter)
    {
        var seite = new VBoxContainer { Name = reiter.ToString() };
        seite.AddThemeConstantOverride("separation", 6);
        _reiter.AddChild(seite);
        return seite;
    }

    private static Label NeuesLabel(Container eltern, int groesse, Color? farbe = null, int umbruch = 0) =>
        Hauptmenue.Text(eltern, "", groesse, farbe, umbruchBreite: umbruch);

    private static Button KleinerKnopf(Container eltern, Action aktion)
    {
        var knopf = new Button { FocusMode = FocusModeEnum.None };
        knopf.AddThemeFontSizeOverride("font_size", 13);
        knopf.Pressed += aktion;
        eltern.AddChild(knopf);
        return knopf;
    }

    /// <summary>Verbindet die Anzeige mit der Simulation. Wird von der Hauptszene aufgerufen.</summary>
    public void Verbinden(Simulation simulation, Konto spieler)
    {
        _simulation = simulation;
        _spieler = spieler;
    }

    /// <summary>Öffnet die Tafel auf diesem Reiter (rechts im Bild, unter dem Zeitstrahl).</summary>
    public void Zeigen(Reiter reiter)
    {
        _reiter.CurrentTab = (int)reiter;
        Show();
        Rollbereich.RechtsAnheften(this);
        Aktualisieren();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Texte, die auch die Kopfzeile zeigt (Geld, Stand, Aktionspunkte)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Hat das Haus Geld verliehen, Schulden oder ein ausgebautes Wohnhaus? Dann ist das Reinvermögen nicht das Bargeld.</summary>
    public static bool MitReinvermoegen(Konto k) => Math.Abs(k.Forderungen) > 0.5 || Math.Abs(k.Schulden) > 0.5 || k.Hauswert > 0.5;

    /// <summary>Woraus sich das Reinvermögen zusammensetzt (Tooltip des Geldes in der Kopfzeile und Text im Reiter Geld).</summary>
    public static string VermoegenTeile(Konto k, string waehrung) => MitReinvermoegen(k)
        ? $"Bargeld {Zahlenformat.Ganz(k.Vermoegen)} + verliehen {Zahlenformat.Ganz(k.Forderungen)} − Schulden {Zahlenformat.Ganz(k.Schulden)}" +
          $" + Wohnhaus {Zahlenformat.Ganz(k.Hauswert)} = Reinvermögen {Zahlenformat.Ganz(k.Reinvermoegen)} {waehrung}.\n" +
          "Stand, Rangliste und Ansehen richten sich nach dem Reinvermögen; bezahlt wird mit Bargeld."
        : $"Bargeld {Zahlenformat.Ganz(k.Vermoegen)} {waehrung} – damit wird bezahlt. Stand, Rangliste und Ansehen richten sich nach dem Reinvermögen " +
          "(Bargeld, Verliehenes und Wohnhaus, weniger Schulden).";

    /// <summary>Der Weg zum nächsten Stand in einem Satz (oder Bankrott, Kerker, höchster Stand).</summary>
    public static string StandText(Stadtwirtschaft w, Konto k)
    {
        var waehrung = w.Daten.Allgemein.Waehrung;
        return k.IstBankrott ? "Bankrott" : k.IstErloschen ? "Das Haus ist erloschen"
            : w.Justiz.IstAktiv && w.Justiz.KerkerBis(k) is int kerker ? $"Das Oberhaupt sitzt bis Ende {kerker} im Kerker"
            : w.NaechsterStand(k) is not { } neu ? "Höchster Stand erreicht"
            : k.Reinvermoegen >= neu.Schwelle && w.Wohnen.StandVerboten(k, k.Stand + 1) is { } ohneHaus ? ohneHaus
            : k.Reinvermoegen >= neu.Schwelle ? $"Aufstieg zum {neu.Name} im Rathaus möglich!"
            : $"Nächster Stand: {neu.Name} ab {Zahlenformat.Ganz(neu.Schwelle)} {waehrung} (noch {Zahlenformat.Ganz(neu.Schwelle - Math.Max(0, k.Reinvermoegen))})";
    }

    /// <summary>Was Aktionspunkte kosten – Tooltip der AP in Kopfzeile und Reiter Haus.</summary>
    public const string ApTipp =
        "Persönliche Handlungen kosten Aktionspunkte: Werben und Heiraten, Ratsherren beschenken, sich bewerben (je 1), " +
        "Statut-Antrag (2), Spitzel (1), Anklage (2), Bande anheuern (1), Erpresserbrief (2), Talent üben (2–6), Beruf erlernen (4), Meisterprüfung (2–4).\n" +
        "Betriebe, Personal, Karren, Wächter, Stand und Liegenschaften sind frei. Nicht verbrauchte AP bleiben – bis zur Obergrenze.";

    /// <summary>„Jakob (31, rüstig) · verheiratet mit Agnes · 2 Kinder · Erbe: Konrad“.</summary>
    public static string FamilienText(Stadtwirtschaft w, Konto k)
    {
        var buch = w.Familien;
        if (buch.Von(k) is not { } f) return "Das Haus hat (noch) kein Familienbuch.";
        var o = f.Oberhaupt;
        var kinder = f.Kinder.Count(x => x.Lebt);
        var erbe = buch.Erbe(f);
        var kranke = f.Lebende.Count(p => p.Krankheit is not null);
        return $"{o.Vorname} ({o.Alter(buch.Jahr)}, {(o.Krankheit is { } krank ? $"krank: {krank.Name}" : buch.Verfassung(o))})"
               + (f.Ehepartner is { } e ? $" · verheiratet mit {e.Vorname}" : " · ledig")
               + (kinder > 0 ? $" · {kinder} {(kinder == 1 ? "Kind" : "Kinder")}" : "")
               + (erbe is null ? " · ohne Erben!" : erbe == f.Ehepartner ? "" : $" · Erbe: {erbe.Vorname}")
               + (kranke > 0 && o.Krankheit is null ? $" · {kranke} krank" : "");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Anzeige
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Überträgt die aktuellen Zahlen auf die Tafel (jedes Bild, solange sie offen ist).</summary>
    public void Aktualisieren()
    {
        if (!Visible || _simulation?.Wirtschaft is not { } w || _spieler is null) return;
        var waehrung = w.Daten.Allgemein.Waehrung;

        _titelWappen.Wappen = _spieler.Wappen;
        Setzen(_titel, $"Wohnhaus · {_spieler.Inhaber}\n{w.StandVon(_spieler).Name}" + (w.Rat.AmtVon(_spieler) is { } amt ? $" · {amt.Name}" : ""));

        // Haus
        Setzen(_standZeile, StandText(w, _spieler));
        WohnhausAktualisieren(w, waehrung);
        _apZeile.Visible = w.HatAktionspunkte;
        if (w.Daten.Aktionspunkte is { } ap)
        {
            var bonus = w.ApBonus(_spieler);
            Setzen(_apZeile, $"Aktionspunkte: {_spieler.Aktionspunkte.Vorrat} von höchstens {ap.Hoechstens} · +{w.AktionspunkteJeRunde + bonus} zum Jahreswechsel"
                             + (bonus > 0 ? $" (davon {bonus} als Meister oder Geldleiher)" : ""));
            _apZeile.TooltipText = ApTipp;
        }
        Setzen(_statutZeile, $"Statut {w.Statut.Kuerzel()} – {w.Statut.Name()}"
                             + (w.IstArbeitszeit(_simulation.Zeit) ? " · es wird gearbeitet" : " · Feierabend")
                             + (w.Gesundheit.IstPestjahr ? $" · PEST: {Zahlenformat.Ganz(w.Gesundheit.Krankenstand * 100)} % der Arbeitskräfte fehlen" : ""));

        // Familie, Handwerk, Geld, Hinterzimmer
        Setzen(_familienZeile, FamilienText(w, _spieler));
        BerufZeileAktualisieren(w);
        MeisterAktualisieren(w);
        Setzen(_vermoegen, $"{Zahlenformat.Ganz(_spieler.Vermoegen)} {waehrung}" + (MitReinvermoegen(_spieler) ? " bar" : ""));
        _vermoegen.AddThemeColorOverride("font_color", _spieler.Reinvermoegen < 0 ? new Color(0.95f, 0.45f, 0.4f) : new Color(1, 1, 1));
        Setzen(_vermoegenTeile, VermoegenTeile(_spieler, waehrung));
        KreditAktualisieren(w);
        Setzen(_hinterzimmerText, "Im Hinterzimmer heuert ihr Banden an, schreibt Erpresserbriefe und stellt Wächter vor eure Häuser. "
                                  + "Die Banden ziehen ab 20 Uhr los.");
    }

    /// <summary>Setzt einen Text nur, wenn er sich ändert (spart das Neuvermessen der Tafel).</summary>
    private static void Setzen(Label label, string text)
    {
        if (label.Text != text) label.Text = text;
    }

    /// <summary>„Handwerk: Schmied“, eine Auswahl der noch fremden Berufe und ein Knopf, um den gewählten zu erlernen.</summary>
    private void BerufZeileAktualisieren(Stadtwirtschaft w)
    {
        Setzen(_berufText, $"Handwerk: {string.Join(", ", _spieler!.Berufe.Select(b => b + UiHilfe.VolksZusatz(w, _spieler, b)))}");
        var lernbarSichtbar = w.Daten.Berufe.Count > 1 && !_spieler.IstErloschen;
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
        _berufWahl.Visible = _lernenKnopf.Visible = lernbarSichtbar && _lernbarerBeruf is not null;
        if (!_lernenKnopf.Visible || _lernbarerBeruf is not { } beruf) return;
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
                var text = NeuesLabel(_meisterListe, 13, new Color(0.85f, 0.85f, 0.85f), umbruch: Breite);
                var knopf = KleinerKnopf(_meisterListe, () => Senden(new MeisterpruefungAblegen(beruf)));
                knopf.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
                _meisterZeilen.Add((beruf, text, knopf));
            }
        }
        var waehrung = w.Daten.Allgemein.Waehrung;
        foreach (var (beruf, text, knopf) in _meisterZeilen)
        {
            var grad = _spieler.Meistergrad(beruf);
            var erfahrung = Zahlenformat.Ganz(Math.Floor(_spieler.Erfahrung(beruf)));
            var p = w.NaechstePruefung(_spieler, beruf);
            Setzen(text, $"{beruf}: {grad}. Meistergrad · Erfahrung {erfahrung}" + (p is not null ? $" / {Zahlenformat.Ganz(p.Erfahrung)} Std." : " Std. · höchster Grad"));
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

    /// <summary>Zeile „Wohnhaus: Bürgerhaus“ mit Knopf zum Ausbauen (gesperrt mit Grund aus dem Kern) und dem Hauswächter.</summary>
    private void WohnhausAktualisieren(Stadtwirtschaft w, string waehrung)
    {
        _wohnhausText.Visible = w.Wohnen.IstAktiv;
        if (!w.Wohnen.IstAktiv || _spieler is null)
        {
            _ausbauKnopf.Visible = _hauswaechterKnopf.Visible = false;
            return;
        }
        var stufe = w.Wohnen.Stufe(_spieler)!;
        var instand = _spieler.WohnhausInstandBis is { } bis && _simulation is { } s
            ? Math.Max(0, bis - s.Zeit.VergangeneMinuten) : (long?)null;
        Setzen(_wohnhausText, $"Wohnhaus: {stufe.Name}"
                   + (instand is { } rest0 ? $" – nach dem Brand in Instandsetzung (noch {rest0 / 60}:{rest0 % 60:00} h, so lange fehlt das Ansehen)"
                      : stufe.Ansehen > 0 ? $" (+{Zahlenformat.Ganz(stufe.Ansehen)} Ansehen, Unterhalt {Zahlenformat.Ganz(stufe.Unterhalt)} {waehrung} im Jahr)" : ""));
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
        // Stadtpalais und Stadthof gibt es nur als Neubau (Knopf „Bauen“) – dann kein Ausbau-Knopf, der Hinweis steht schon oben.
        var nurNeubau = w.Bauen.NurNeubau(_spieler.WohnhausStufe + 1) && w.Wohnen.FertigAb(_spieler) is null;
        _ausbauKnopf.Visible = naechste is not null && _spieler.IstAktiv && !nurNeubau;
        if (naechste is null || nurNeubau) return;
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
            _geberWahl.Visible = _kreditEingabe.Visible = false;
            _abloesenKnopf.Visible = true;
            Setzen(_kreditText, $"Kredit bei {k.Geber.Inhaber}: noch {Zahlenformat.Ganz(k.Rest)} {waehrung} zu {Zahlenformat.Ganz(k.Satz * 100)} % · " +
                                $"Jahresende: Rate {Zahlenformat.Ganz(k.Tilgungsrate)} + Zinsen ≈ {Zahlenformat.Ganz(k.Aufgelaufen)}");
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
        _geberWahl.Visible = _kreditEingabe.Visible = geber.Count > 0;
        if (geber.Count == 0)
        {
            Setzen(_kreditText, "Kredit: In der Stadt verleiht noch niemand Geld.");
            return;
        }
        for (var i = 0; i < geber.Count; i++)
            _geberWahl.SetItemText(i, $"{geber[i].Inhaber} · {Zahlenformat.Ganz(bank.Zinssatz(geber[i]) * 100)} % · bis {Zahlenformat.Ganz(bank.Hoechstkredit(geber[i], _spieler!))}");
        Setzen(_kreditText, "Kredit beim Geldleiher:");
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
        if (_lernbarerBeruf is { } beruf) Senden(new BerufErlernen(beruf));
    }
}
