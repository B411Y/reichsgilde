using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Ratskammer mit vier Reitern (PR „Ratskammer mit Reitern“):
/// <list type="bullet">
/// <item><b>Ämter</b>: der Ratsbaum (<see cref="RatBaumAnzeige"/>) mit der Tafel zum gewählten Amt – Bewerben mit den Voraussetzungen, Bewerberreihe und Stimme,
/// „Euer Amt“, Geschenke an die Ratsherren (RatAnzeige.Baum.cs) – und der Domprediger (RatAnzeige.Dom.cs).</item>
/// <item><b>Anträge</b>: Tagesordnung, Antrag zum Fron- und Sklavenstatut, Wegbau, die letzte Sitzung (RatAnzeige.Antraege.cs).</item>
/// <item><b>Stadtglaube</b>: der geltende Glaube und der Antrag dazu (nur mit Glaubenssystem).</item>
/// <item><b>Kasse</b>: Stand und Haushalt der Stadt, Bauten, Fest und die Anträge zu Steuer, Bau und Fest (RatAnzeige.Kasse.cs, nur mit Stadtkasse).</item>
/// </list>
/// Wer im Rat sitzt, legt hier auch seine Stimme bei den Wahlen und den Anträgen vorab fest (bis zur Sitzung am Jahresende änderbar).
///
/// <b>Aussehen und Logik sind getrennt:</b> Farben, Schriften, Ränder und Abstände stammen aus dem Thema <c>res://scenes/rat_thema.tres</c>
/// (<see cref="RatStil"/>). Der Reiter „Ämter“ und die Kurzübersicht sind schon ganz auf das Thema umgestellt; die Reiter „Anträge“, „Stadtglaube“ und „Kasse“
/// rufen noch <c>Hauptmenue.Text</c> mit Größe und Farbe auf und wandern mit dem Fable-Neudesign ins Thema.
///
/// Wie alle Bedienfelder ändert sie nichts selbst, sondern schickt Befehle an die Stadtwirtschaft.
/// </summary>
public partial class RatAnzeige : PanelContainer
{
    /// <summary>Die Reiter in ihrer Reihenfolge (auch für <c>--rat=&lt;reiter&gt;</c> und die Knöpfe der Kurzübersicht).</summary>
    public enum Reiter { Aemter, Antraege, Stadtglaube, Kasse }

    // Farben nur für Textstellen, die sich nach dem Zustand richten – sie kommen aus dem Thema, nicht aus dem Code.
    private static Color Gold => RatStil.Farbe("gold");
    private static Color Grau => RatStil.Farbe("grau");
    private static Color Gruen => RatStil.Farbe("gruen");
    private static Color Rot => RatStil.Farbe("rot");

    private Simulation? _simulation;
    private Konto? _spieler;
    /// <summary>Die Seite des Reiters, die gerade aufgebaut wird – alle Abschnitte hängen ihre Zeilen hier ein.</summary>
    private VBoxContainer _inhalt = null!;
    private TabContainer _reiter = null!;
    private VBoxContainer _kopf = null!;
    private readonly Dictionary<Reiter, VBoxContainer> _seiten = new();
    /// <summary>Nur der Betrag wird gemerkt – das Zahlenfeld entsteht bei jedem Aufbau neu (siehe FamilienAnzeige).</summary>
    private double _geschenkBetrag = 200;
    private Label _meldung = null!;

    /// <summary>Ergebnis eines Befehls – für das Protokoll.</summary>
    public event Action<string>? Gemeldet;

    private static readonly Dictionary<Reiter, string> Reitername = new()
    {
        [Reiter.Aemter] = "Ämter", [Reiter.Antraege] = "Anträge", [Reiter.Stadtglaube] = "Stadtglaube", [Reiter.Kasse] = "Kasse",
    };

    public override void _Ready()
    {
        RatStil.Anwenden(this);
        ThemeTypeVariation = "RatTafel";
        CustomMinimumSize = new Vector2(RatStil.Mass("kammer_breite"), 0);
        var rand = RatStil.Container<MarginContainer>(this, "RatRand");
        var spalte = RatStil.Container<VBoxContainer>(rand, "RatSpalte");

        _kopf = RatStil.Container<VBoxContainer>(spalte, "RatSpalte");
        _reiter = new TabContainer { ThemeTypeVariation = "RatReiter", FocusMode = FocusModeEnum.None, ClipTabs = false };
        spalte.AddChild(_reiter);
        foreach (var r in Enum.GetValues<Reiter>())
        {
            var seite = new VBoxContainer { Name = Reitername[r], ThemeTypeVariation = "RatSpalte" };
            _reiter.AddChild(seite);
            _seiten[r] = seite;
        }
        _inhalt = _seiten[Reiter.Aemter];

        _meldung = RatStil.Text(spalte, "", "RatHinweis", umbrechen: true);
        var schliessen = Hauptmenue.Knopf(spalte, "Schließen", Hide);
        schliessen.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        Rollbereich.Einhuellen(this);
        Fensterrahmen.Anmelden(this, "Rat", festhaltbar: true);
        Hide();
    }

    public void Verbinden(Simulation simulation, Konto spieler)
    {
        _simulation = simulation;
        _spieler = spieler;
    }

    /// <summary>Öffnet die Kammer auf einem Reiter (ohne Angabe: der Reiter, der zuletzt offen war, beim ersten Mal „Ämter“).</summary>
    public void Oeffnen(Reiter? reiter = null)
    {
        _meldung.Text = "";
        if (reiter is { } r) _reiter.CurrentTab = _reiter.GetTabIdxFromControl(_seiten[r]);
        Aufbauen();
        Show();
    }

    private void Befehlen(Befehl befehl)
    {
        UiHilfe.Befehlen(_simulation, _spieler, befehl, this, ergebnis =>
        {
            _meldung.Text = UiHilfe.MeldungText(ergebnis);
            _meldung.ThemeTypeVariation = ergebnis.Erfolg ? "RatGut" : "RatSchlecht";
            if (ergebnis.Erfolg) KlangOberflaeche.Spielen(KlangOberflaeche.Ereignis.Siegel); // Antrag oder Stimme ist besiegelt
            Gemeldet?.Invoke(_meldung.Text);
            Aufbauen(); // Gunst, Prognosen und Knöpfe haben sich geändert
        });
    }

    /// <summary>Baut den Inhalt neu auf – beim Öffnen und nach jedem Befehl. Der gewählte Reiter bleibt.</summary>
    public void Aufbauen()
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null) return;
        var rat = w.Rat;

        var fokusAmt = _baum?.IsInsideTree() == true ? _baum.FokusAmt() : null; // die Tastaturauswahl bleibt am Knoten
        UiHilfe.KinderEntfernen(_kopf);
        RatStil.Text(_kopf, rat.Daten.Kammer, "RatTitel", zentriert: true);
        RatStil.Text(_kopf, $"Geltendes Fron- und Sklavenstatut: {w.Statut.Kuerzel()} – {w.Statut.Name()}", "RatText", zentriert: true);

        // Reiter ohne Grundlage verschwinden (kein Glaubenssystem, keine Stadtkasse)
        _reiter.SetTabHidden(_reiter.GetTabIdxFromControl(_seiten[Reiter.Stadtglaube]), !w.Glaube.IstAktiv);
        _reiter.SetTabHidden(_reiter.GetTabIdxFromControl(_seiten[Reiter.Kasse]), !w.Kasse.IstAktiv);
        if (_reiter.IsTabHidden(_reiter.CurrentTab)) _reiter.CurrentTab = 0;

        foreach (var (reiter, seite) in _seiten)
        {
            UiHilfe.KinderEntfernen(seite);
            _inhalt = seite;
            switch (reiter)
            {
                case Reiter.Aemter: AemterSeite(w); break;
                case Reiter.Antraege: AntraegeSeite(w); break;
                case Reiter.Stadtglaube: if (w.Glaube.IstAktiv) StadtglaubeSeite(w); break;
                case Reiter.Kasse: if (w.Kasse.IstAktiv) KasseSeite(w); break;
            }
        }
        if (fokusAmt is not null) _baum.CallDeferred(RatBaumAnzeige.MethodName.FokusAuf, fokusAmt);
    }

    /// <summary>Reiter „Anträge“: Tagesordnung, Antrag zum Statut, Wegbau, letzte Sitzung.</summary>
    private void AntraegeSeite(Stadtwirtschaft w)
    {
        var rat = w.Rat;
        var eigenesAmt = rat.AmtVon(_spieler!);
        TagesordnungAbschnitt(w); // RatAnzeige.Antraege.cs

        // ── Antrag zum Statut ──
        _inhalt.AddChild(new HSeparator());
        Hauptmenue.Text(_inhalt, "Antrag zum Fron- und Sklavenstatut", 16);
        if (rat.Antrag is { } antrag)
        {
            var (ja, nein, enthaltung, bm) = rat.AbstimmungsPrognose(antrag.Antragsteller, antrag.Statut);
            var durch = Stadtrat.IstAngenommen(ja, nein, bm);
            Hauptmenue.Text(_inhalt, $"{antrag.Antragsteller.Inhaber} beantragt Statut {antrag.Statut.Kuerzel()} – {antrag.Statut.Name()}. " +
                                     $"Stand heute: {ja} dafür, {nein} dagegen{(enthaltung > 0 ? $", {enthaltung} Enthaltung" : "")} → {(durch ? "würde angenommen" : "würde abgelehnt")}.",
                14, durch ? Gruen : Rot, umbruchBreite: 670);
            AntragStimme(rat.StatutStimmeVerboten(_spieler), rat.StatutStimmeVon(_spieler), ja => Befehlen(new StatutStimme(ja)));
        }
        else if (eigenesAmt is null)
            Hauptmenue.Text(_inhalt, "Anträge stellen nur Mitglieder des Rates.", 14, Grau);
        else
        {
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 10);
            _inhalt.AddChild(zeile);
            var statuten = Enum.GetValues<Fronstatut>().Where(s => s != w.Statut).ToList();
            var auswahl = new OptionButton { FocusMode = FocusModeEnum.None };
            foreach (var s in statuten)
            {
                var (ja, nein, _, bm) = rat.AbstimmungsPrognose(_spieler, s);
                auswahl.AddItem($"{s.Kuerzel()} – {s.Name()} (heute {ja}:{nein}{(Stadtrat.IstAngenommen(ja, nein, bm) ? ", angenommen" : "")})");
            }
            zeile.AddChild(auswahl);
            var stellen = new Button { Text = "Antrag stellen", FocusMode = FocusModeEnum.None };
            stellen.Pressed += () => Befehlen(new StatutBeantragen(statuten[auswahl.Selected]));
            UiHilfe.ApKosten(stellen, w, _spieler, new StatutBeantragen(default));
            zeile.AddChild(stellen);
        }
        Hauptmenue.Text(_inhalt, "Ratsherren stimmen ihrer Haltung zu – einem Nachbarstatut nur, wenn ihre Meinung über euch " +
                                 $"mindestens {Zahlenformat.Ganz(rat.Daten.Gunst.SchwelleNachbarstatut)} beträgt.", 12, Grau, umbruchBreite: 670);

        if (w.Wegebau.IstAktiv)
            WegbauAbschnitt(w, rat, eigenesAmt);

        // ── Letzte Sitzung ──
        if (rat.LetzteSitzung.Count > 0)
        {
            _inhalt.AddChild(new HSeparator());
            Hauptmenue.Text(_inhalt, "Letzte Ratssitzung", 16);
            foreach (var eintrag in rat.LetzteSitzung)
                Hauptmenue.Text(_inhalt, "• " + eintrag, 13, null, umbruchBreite: 670);
        }
    }

    /// <summary>Reiter „Stadtglaube“: der geltende Glaube mit Glaubenszins und der Antrag dazu.</summary>
    private void StadtglaubeSeite(Stadtwirtschaft w)
    {
        var waehrung = w.Daten.Allgemein.Waehrung;
        RatStil.Text(_inhalt, $"Stadtglaube: {KonfessionTexte.StadtglaubeText(w.Glaube.Stadtglaube)}" +
                              (w.Glaube.Stadtglaube is null ? "" : $" – Andersgläubige zahlen {Zahlenformat.Ganz(w.Glaube.Daten.Stadtglaube.Abgabe)} {waehrung} Glaubenszins im Jahr"),
            "RatAbschnitt", umbrechen: true);
        StadtglaubeAbschnitt(w, w.Rat.AmtVon(_spieler!));
    }

    /// <summary>Wegbau der Stadt (Schritt 5d): die Wegekasse und der Antrag „Weg pflastern“ dieses Jahres (gestellt wird er an der Wegtafel: Weg anklicken).</summary>
    private void WegbauAbschnitt(Stadtwirtschaft w, Stadtrat rat, Amt? eigenesAmt)
    {
        var bau = w.Wegebau;
        var waehrung = w.Daten.Allgemein.Waehrung;
        _inhalt.AddChild(new HSeparator());
        Hauptmenue.Text(_inhalt, "Wegbau der Stadt", 16);
        Hauptmenue.Text(_inhalt, $"Wegekasse: {Zahlenformat.Ganz(bau.Wegekasse)} {waehrung}. Die Stadt bessert ihr Pflaster aus und pflastert Hauptstraßen und Markt, wo am meisten gefahren wird.",
            14, null, umbruchBreite: 670);
        if (bau.Antrag is { } antrag && w.Karte is { } karte)
        {
            var (ja, nein, enthaltung, bm) = bau.AntragPrognose(antrag.Antragsteller, antrag.Weg, antrag.Von, antrag.Bis);
            var durch = Stadtrat.IstAngenommen(ja, nein, bm);
            var name = karte.Wege[antrag.Weg].Name is { Length: > 0 } n ? n : Kartenweg.Anzeigename(karte.Wege[antrag.Weg].Klasse);
            Hauptmenue.Text(_inhalt, $"{antrag.Antragsteller.Inhaber} beantragt, {name} von {Zahlenformat.Ganz(antrag.Von)} bis {Zahlenformat.Ganz(antrag.Bis)} m zu pflastern. " +
                                     $"Stand heute: {ja} dafür, {nein} dagegen{(enthaltung > 0 ? $", {enthaltung} Enthaltung" : "")} → {(durch ? "würde angenommen" : "würde abgelehnt")}.",
                14, durch ? Gruen : Rot, umbruchBreite: 670);
        }
        else
            Hauptmenue.Text(_inhalt, eigenesAmt is null ? "Es liegt kein Antrag vor. Anträge stellen nur Mitglieder des Rates."
                : "Es liegt kein Antrag vor. Klickt in der Stadt einen Weg an: Dort stellt ihr den Antrag „Pflaster“ (Ansehen +" + Zahlenformat.Ganz(bau.Daten!.Rat.AntragAnsehen) + ", wenn er angenommen wird).",
                13, Grau, umbruchBreite: 670);
    }

    /// <summary>Antrag zum Stadtglauben – wie das Statut, aber Ratsherren stimmen nach ihrem Glauben.</summary>
    private void StadtglaubeAbschnitt(Stadtwirtschaft w, Amt? eigenesAmt)
    {
        var gw = w.Glaube;
        _inhalt.AddChild(new HSeparator());
        Hauptmenue.Text(_inhalt, "Antrag zum Stadtglauben", 16);
        if (gw.Antrag is { } antrag)
        {
            var (ja, nein, enthaltung, bm) = gw.AbstimmungsPrognose(antrag.Antragsteller, antrag.Glaube);
            var durch = Stadtrat.IstAngenommen(ja, nein, bm);
            Hauptmenue.Text(_inhalt, $"{antrag.Antragsteller.Inhaber} beantragt: {KonfessionTexte.StadtglaubeText(antrag.Glaube)}. " +
                                     $"Stand heute: {ja} dafür, {nein} dagegen{(enthaltung > 0 ? $", {enthaltung} Enthaltung" : "")} → {(durch ? "würde angenommen" : "würde abgelehnt")}.",
                14, durch ? Gruen : Rot, umbruchBreite: 670);
            AntragStimme(gw.StimmeVerboten(_spieler!), gw.StimmeVon(_spieler!), ja => Befehlen(new StadtglaubeStimme(ja)));
        }
        else if (eigenesAmt is null)
            Hauptmenue.Text(_inhalt, "Anträge stellen nur Mitglieder des Rates.", 14, Grau);
        else
        {
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 10);
            _inhalt.AddChild(zeile);
            var ziele = new Konfession?[] { Konfession.Katholisch, Konfession.Evangelisch, null }.Where(k => k != gw.Stadtglaube).ToList();
            var auswahl = new OptionButton { FocusMode = FocusModeEnum.None };
            foreach (var z in ziele)
            {
                var (ja, nein, _, bm) = gw.AbstimmungsPrognose(_spieler!, z);
                auswahl.AddItem($"{KonfessionTexte.StadtglaubeText(z)} (heute {ja}:{nein}{(Stadtrat.IstAngenommen(ja, nein, bm) ? ", angenommen" : "")})");
            }
            zeile.AddChild(auswahl);
            var stellen = new Button { Text = "Antrag stellen", FocusMode = FocusModeEnum.None };
            stellen.Pressed += () => Befehlen(new StadtglaubeBeantragen(ziele[auswahl.Selected]));
            UiHilfe.ApKosten(stellen, w, _spieler!, new StadtglaubeBeantragen(null));
            zeile.AddChild(stellen);
        }
        var d = gw.Daten.Stadtglaube;
        Hauptmenue.Text(_inhalt, "Ratsherren stimmen für ihren eigenen Glauben; für Glaubensfreiheit, wenn ein fremder gilt (sonst ab " +
                                 $"{Zahlenformat.Ganz(d.SchwelleFrei)} Meinung); für einen fremden Glauben erst ab {Zahlenformat.Ganz(d.SchwelleFremd)} Meinung. " +
                                 "Gilt ein Stadtglaube, ist jede Messe und Bekehrungspredigt anderen Glaubens Ketzerei.", 12, Grau, umbruchBreite: 670);
    }

    /// <summary>Die Stimme zu einem Antrag (Statut oder Stadtglaube): Dafür / Dagegen / Enthaltung, oder der Grund, warum nicht.</summary>
    private void AntragStimme(string? verboten, bool? aktuell, Action<bool?> setzen)
    {
        if (verboten is not null)
        {
            Hauptmenue.Text(_inhalt, "Eure Stimme: " + verboten, 13, Grau, umbruchBreite: 670);
            return;
        }
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 6);
        _inhalt.AddChild(zeile);
        Hauptmenue.Text(zeile, "Eure Stimme:", 14);
        foreach (var (text, wert) in new (string, bool?)[] { ("Dafür", true), ("Dagegen", false), ("Enthaltung", null) })
        {
            var knopf = new Button { Text = text, ToggleMode = true, ButtonPressed = aktuell == wert, FocusMode = FocusModeEnum.None };
            knopf.TooltipText = wert is null ? "Ohne Stimme gilt die Enthaltung – sie ändert die Prognose nicht." : "Gilt bis zur Ratssitzung am Jahresende; bis dahin änderbar.";
            knopf.Pressed += () => setzen(wert);
            zeile.AddChild(knopf);
        }
        Hauptmenue.Text(zeile, "  (bis zur Sitzung änderbar, Vorgabe: Enthaltung)", 12, Grau);
    }
}
