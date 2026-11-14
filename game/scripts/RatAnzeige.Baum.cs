using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Ratskammer, Reiter „Ämter“ (PR „Ratsbaum“): der <see cref="RatBaumAnzeige"/> ersetzt das Raster; darunter die Tafel zum gewählten Amt:
/// <list type="bullet">
/// <item><b>Euer Amt</b> (F7): am eigenen Knoten die Befugnis mit ihren Knöpfen (<see cref="AmtBefugnisAnzeige"/>).</item>
/// <item><b>Bewerben</b>: die Voraussetzungen aus <see cref="Stadtrat.BewerbungPruefen"/>, Erfülltes grün, Fehlendes rot; der Knopf ist gesperrt, solange etwas fehlt.
/// „Bewerbung zurückziehen“ (F8) ohne Erstattung der Aktionspunkte.</item>
/// <item><b>Bewerberreihe</b>: Porträts der Bewerber (<see cref="Stadtrat.Kandidaten"/>); ein Klick legt die Stimme fest (<see cref="WahlStimme"/>), die gesetzte Stimme trägt ein Siegel.</item>
/// <item><b>Ratsherren</b>: Haltung, Meinung, Geschenkbetrag, Beschenken und Moschus wie bisher.</item>
/// </list>
/// Aussehen nur aus dem Thema (<see cref="RatStil"/>); die Tafel liest den Kern und schickt Befehle.
/// </summary>
public partial class RatAnzeige
{
    /// <summary>Knöpfe am eigenen Knoten, die eine andere Tafel öffnen (Hauptszene verdrahtet sie).</summary>
    public event Action? GerichtGewuenscht, MarktGewuenscht;

    private RatBaumAnzeige _baum = null!;
    private VBoxContainer _detail = null!;
    /// <summary>Name des gewählten Amtes – bleibt über den Neuaufbau erhalten (null: noch keines gewählt).</summary>
    private string? _gewaehlt;
    private readonly List<Action> _gunstVorschau = new();

    /// <summary>Wählt ein Amt im Baum vor (Startoption <c>--ratamt=</c>); wirkt beim nächsten Aufbau.</summary>
    public void AmtWaehlen(string? amtName) => _gewaehlt = amtName;

    /// <summary>Die Verweise von „Euer Amt“ am eigenen Knoten: Reiter der Kammer wechseln, Gericht und Markt öffnen.</summary>
    private AmtBefugnisAnzeige Befugnis() => new(_spieler!)
    {
        Befehlen = Befehlen,
        RatGewuenscht = reiter => _reiter.CurrentTab = _reiter.GetTabIdxFromControl(_seiten[reiter]),
        GerichtGewuenscht = () => GerichtGewuenscht?.Invoke(),
        MarktGewuenscht = () => MarktGewuenscht?.Invoke(),
    };

    /// <summary>Reiter „Ämter“: Laufbahn, der Baum, die Tafel zum gewählten Amt, der Domprediger.</summary>
    private void AemterSeite(Stadtwirtschaft w)
    {
        var rat = w.Rat;
        var eigenesAmt = rat.AmtVon(_spieler!);
        
        // ── Laufbahn ──
        RatStil.Text(_inhalt, $"Eure Laufbahn: {w.StandVon(_spieler!).Name}, {(eigenesAmt is null ? "noch ohne Amt" : $"im Amt als {eigenesAmt.Name}")}", "RatAbschnitt");
        if (rat.Bewerbungen.TryGetValue(_spieler!, out var beworben) && rat.AmtNamens(beworben) is { } ziel)
        {
            RatStil.Text(_inhalt, $"Ihr bewerbt euch als {ziel.Name}. Gewählt wird am Jahresende.", "RatText", umbrechen: true);
            ZurueckziehenKnopf(_inhalt, ziel);
        }

        // ── Der Baum ──
        _baum = new RatBaumAnzeige { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        _inhalt.AddChild(_baum);
        _baum.Aufbauen(w, _spieler!, _gewaehlt);
        _baum.AmtGewaehlt += amt =>
        {
            _gewaehlt = amt.Name;
            _baum.Waehlen(_gewaehlt);
            DetailAufbauen(_simulation!.Wirtschaft!);
        };

        // ── Tafel zum gewählten Amt ──
        var tafel = RatStil.Container<PanelContainer>(_inhalt, "RatKurzTafel");
        _detail = RatStil.Container<VBoxContainer>(tafel, "RatSpalte");
        DetailAufbauen(w);

        DomAbschnitt(w); // RatAnzeige.Dom.cs
    }

    private void ZurueckziehenKnopf(Container eltern, Amt ziel)
    {
        var zurueck = RatStil.Amtsknopf(eltern, "Bewerbung zurückziehen", () => Befehlen(new BewerbungZurueckziehen(ziel.Name)),
            "Ihr nehmt die Bewerbung zurück und könnt euch um ein anderes Amt bewerben. Die Aktionspunkte der Bewerbung sind verbraucht; eine neue Bewerbung kostet wieder.");
        zurueck.Name = "BewerbungZurueckziehen";
        zurueck.FocusMode = FocusModeEnum.All;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Die Tafel zum gewählten Amt
    // ═════════════════════════════════════════════════════════════════════════

    private void DetailAufbauen(Stadtwirtschaft w)
    {
        UiHilfe.KinderEntfernen(_detail);
        _gunstVorschau.Clear();
        var rat = w.Rat;
        var waehrung = w.Daten.Allgemein.Waehrung;
        if (_gewaehlt is null || rat.AmtNamens(_gewaehlt) is not { } amt)
        {
            RatStil.Text(_detail, "Wählt ein Amt im Baum: Die Tafel zeigt, was das Amt darf, wer sich bewirbt, was für eine Bewerbung fehlt – und bei Ratsherren ihre Meinung über euch.", "RatHinweis", umbrechen: true);
            return;
        }
        RatStil.Text(_detail, $"{amt.Name} · Stufe {amt.Stufe} · {Zahlenformat.Ganz(amt.Daten.Einkommen)} {waehrung} im Jahr", "RatAbschnitt");
        RatStil.Text(_detail, amt.Daten.Befugnis, "RatHinweis", umbrechen: true);
        InhaberZeile(w, amt);

        if (amt.Ratsherr is { } ratsherr) RatsherrAbschnitt(w, amt, ratsherr);

        if (amt.Konto == _spieler)
        {
            // Euer Amt (F7): die Befugnis am eigenen Knoten
            _detail.AddChild(new HSeparator());
            Befugnis().Aufbauen(w, _detail);
        }
        else BewerbenAbschnitt(w, amt);

        WahlAbschnitt(w, amt);
    }

    /// <summary>Wer das Amt hat: Haus (mit Wappen), Stand, Volk, Glaube – oder der Ratsherr.</summary>
    private void InhaberZeile(Stadtwirtschaft w, Amt amt)
    {
        var zeile = RatStil.Container<HBoxContainer>(_detail, "RatZeile");
        if (!amt.IstBesetzt)
        {
            RatStil.Text(zeile, $"Unbesetzt – bewerben darf, wer mindestens den Stand {amt.Daten.MindestStand} hat.", "RatText");
            return;
        }
        if (amt.Konto is { } haus)
        {
            zeile.AddChild(WappenBild.Neu(haus.Wappen, RatStil.Mass("knoten_wappen")));
            var volk = w.Familien.Von(haus)?.Volk;
            var glaube = w.Glaube.IstAktiv ? w.Glaube.GlaubeVon(haus) : null;
            RatStil.Text(zeile, $"{haus.Inhaber} · {w.StandVon(haus).Name}" + (volk is { } v ? $" · {v.Einzahl()}" : "") + (glaube is { } g ? $" · {g.Name()}" : ""), "RatText");
        }
        else if (amt.Ratsherr is { } r)
            RatStil.Text(zeile, $"{r.Name}, Ratsherr · {r.Daten.Volk.Einzahl()}" + (w.Glaube.IstAktiv ? $" · {w.Glaube.GlaubeVon(r).Name()}" : ""), "RatText");
    }

    /// <summary>Haltung, Meinung über euch, Geschenk und Moschus eines Ratsherrn (wie im bisherigen Raster).</summary>
    private void RatsherrAbschnitt(Stadtwirtschaft w, Amt amt, Ratsherr ratsherr)
    {
        var rat = w.Rat;
        var waehrung = w.Daten.Allgemein.Waehrung;
        var meinung = rat.Meinung(ratsherr, _spieler!);
        RatStil.Text(_detail, $"Haltung: {ratsherr.Haltung.Kuerzel()} – {ratsherr.Haltung.Name()}", "RatText").TooltipText = $"{ratsherr.Name} stimmt nach der Haltung {ratsherr.Haltung.Name()}.";
        RatStil.Text(_detail, $"Meinung über euch: {Zahlenformat.MitVorzeichen(meinung)}", meinung >= 5 ? "RatGut" : meinung <= -5 ? "RatSchlecht" : "RatText").TooltipText = MeinungsHilfe.Tipp(w, ratsherr, _spieler);
        if (w.Glaube.IstAktiv)
            RatStil.Text(_detail, "Der Glaube zählt in seiner Meinung über euch.", "RatHinweis");

        var geschenkZeile = RatStil.Container<HBoxContainer>(_detail, "RatZeile");
        RatStil.Text(geschenkZeile, "Geschenk je Klick:");
        var geschenk = new Zahlfeld { MinValue = 10, MaxValue = 5000, Step = 10, Value = _geschenkBetrag, Suffix = waehrung, CustomMinimumSize = new Vector2(RatStil.Mass("zahlfeld_breite"), 0) };
        geschenkZeile.AddChild(geschenk);
        // Wie viel Gunst ein Geschenk bringt, hängt von den Charakterzügen des Ratsherrn ab (Rat.GunstJeGulden) – die Zahl steht am Knopf.
        RatStil.Text(geschenkZeile, $"(verblasst um {Zahlenformat.Ganz(rat.Daten.Gunst.VerfallJeJahr * 100)} % im Jahr)", "RatHinweis");
        geschenk.ValueChanged += wert => { _geschenkBetrag = wert; foreach (var a in _gunstVorschau) a(); };

        var knoepfe = RatStil.Container<HBoxContainer>(_detail, "RatZeile");
        var knopf = RatStil.Amtsknopf(knoepfe, "Beschenken", () => Befehlen(new Schenken(ratsherr.Name, _geschenkBetrag)));
        knopf.FocusMode = FocusModeEnum.All;
        void Vorschau() { if (!knopf.Disabled) knopf.TooltipText = $"{Zahlenformat.MitVorzeichen(_geschenkBetrag * rat.GunstJeGulden(ratsherr, _spieler!))} Meinung bei {ratsherr.Name} (weitere Geschenke in diesem Jahr wirken weniger)"; } // nach dem AP-Hinweis nicht überschreiben
        Vorschau();
        _gunstVorschau.Add(Vorschau);
        UiHilfe.ApKosten(knopf, w, _spieler!, new Schenken("", 0));
        if (!w.Duft.IstAktiv) return;
        var duft = w.Duft;
        var preis = duft.Preis(_spieler!, duft.Daten.Moschus.Ware);
        var moschus = RatStil.Amtsknopf(knoepfe, "Moschus", () => Befehlen(new MoschusRatsherr(ratsherr.Name)));
        moschus.FocusMode = FocusModeEnum.All;
        var grund = duft.MoschusRatsherrVerboten(_spieler!, ratsherr.Name);
        moschus.Disabled = grund is not null;
        moschus.TooltipText = grund ?? $"{duft.Daten.Moschus.Ware} schenken ({(preis > 0 ? $"{Zahlenformat.Ganz(preis)} {waehrung} am Markt" : "aus dem Lager")}): " +
                                       $"+{Zahlenformat.Ganz(duft.Daten.Moschus.Gunst)} Meinung – ein Duft ist keine Bestechung. Einmal im Jahr je Ratsherr.";
        UiHilfe.ApKosten(moschus, w, _spieler!, new MoschusRatsherr(""));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Bewerben: Voraussetzungen, Fehlendes in Rot
    // ═════════════════════════════════════════════════════════════════════════

    private void BewerbenAbschnitt(Stadtwirtschaft w, Amt amt)
    {
        var rat = w.Rat;
        _detail.AddChild(new HSeparator());
        RatStil.Text(_detail, "Bewerben", "RatAbschnitt");
        var voraussetzungen = rat.BewerbungPruefen(_spieler!, amt.Name);
        foreach (var v in voraussetzungen)
            RatStil.Text(_detail, (v.Erfuellt ? "+ " : "– ") + v.Text, v.Erfuellt ? "RatGut" : "RatSchlecht", umbrechen: true);

        var schonHier = rat.Bewerbungen.TryGetValue(_spieler!, out var beworben) && beworben == amt.Name;
        if (schonHier)
        {
            RatStil.Text(_detail, "Ihr bewerbt euch um dieses Amt. Gewählt wird am Jahresende.", "RatText", umbrechen: true);
            ZurueckziehenKnopf(_detail, amt);
            return;
        }
        var knopf = RatStil.Amtsknopf(_detail, "Bewerben", () => Befehlen(new AmtBewerben(amt.Name)),
            $"Ihr bewerbt euch als {amt.Name}. Gewählt wird am Jahresende; bis dahin könnt ihr die Bewerbung zurückziehen.");
        knopf.Name = "Bewerben";
        knopf.FocusMode = FocusModeEnum.All;
        var fehlt = voraussetzungen.FirstOrDefault(v => !v.Erfuellt);
        if (fehlt is not null) { knopf.Disabled = true; knopf.TooltipText = fehlt.Text; }
        else UiHilfe.ApKosten(knopf, w, _spieler!, new AmtBewerben(""));
        if (fehlt is null) PrognoseZeigen(rat, amt);
    }

    private void PrognoseZeigen(Stadtrat rat, Amt amt)
    {
        var (sicher, unsicher, waehler) = rat.WahlPrognose(amt, _spieler!);
        var mehrheit = waehler / 2 + 1;
        var text = $"Stand heute {sicher} sichere Stimme{(sicher == 1 ? "" : "n")}{(unsicher > 0 ? $" und {unsicher} unentschieden" : "")} " +
                   $"von {waehler} – für die Mehrheit braucht ihr {mehrheit}.";
        RatStil.Text(_detail, text, sicher >= mehrheit ? "RatGut" : "RatHinweis", umbrechen: true);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Bewerberreihe und eure Stimme
    // ═════════════════════════════════════════════════════════════════════════

    private void WahlAbschnitt(Stadtwirtschaft w, Amt amt)
    {
        var rat = w.Rat;
        var kandidaten = rat.Kandidaten(amt);
        _detail.AddChild(new HSeparator());
        RatStil.Text(_detail, "Bewerber", "RatAbschnitt");
        if (kandidaten.Count == 0)
        {
            RatStil.Text(_detail, amt.IstBesetzt ? "Niemand bewirbt sich um dieses Amt – der Inhaber bleibt." : "Niemand bewirbt sich um dieses Amt – es bleibt unbesetzt.", "RatHinweis", umbrechen: true);
            return;
        }
        var grund = rat.WahlStimmeVerboten(_spieler!, amt);
        var gewaehlt = rat.WahlStimmeVon(_spieler!, amt);
        var vorgabe = rat.VorgabeStimme(amt);

        var reihe = RatStil.Container<HBoxContainer>(_detail, "RatZeile");
        foreach (var k in kandidaten)
        {
            var knopf = new Button { ThemeTypeVariation = "RatBewerber", ToggleMode = true, FocusMode = FocusModeEnum.All, Disabled = grund is not null,
                CustomMinimumSize = new Vector2(RatStil.Mass("bewerber_breite"), RatStil.Mass("bewerber_hoehe")), Name = "Bewerber_" + k.Name };
            var istGewaehlt = gewaehlt is not null && gewaehlt.Name == k.Name;
            knopf.SetPressedNoSignal(istGewaehlt);
            var kandidat = k;
            knopf.Pressed += () => Befehlen(new WahlStimme(amt.Name, istGewaehlt ? null : kandidat.Name)); // zweiter Klick nimmt die Stimme zurück: es gilt wieder die Vorgabe
            knopf.TooltipText = grund ?? (istGewaehlt ? "Eure Stimme (besiegelt). Ein weiterer Klick nimmt sie zurück: Dann gilt die Vorgabe, der bisherige Inhaber."
                : $"Ein Klick legt eure Stimme für {k.Name} fest. Sie zählt in der Prognose und in der Sitzung am Jahresende und lässt sich bis dahin ändern.");
            var rand = RatStil.Container<MarginContainer>(knopf, "RatKnotenRand");
            rand.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            rand.MouseFilter = Control.MouseFilterEnum.Ignore;
            var spalte = RatStil.Container<VBoxContainer>(rand, "RatBewerberSpalte");
            spalte.MouseFilter = Control.MouseFilterEnum.Ignore;
            var bild = PortraetAnzeige.Neu(RatStil.Mass("bewerber_portraet"));
            bild.MouseFilter = Control.MouseFilterEnum.Ignore; // der Klick ist die Stimme, nicht das Charakterfenster
            bild.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            if (k.Haus is not null) bild.ZeigenHaus(w, k.Haus);
            else if (amt.Ratsherr is { } rh) bild.ZeigenRatsherr(rh);
            spalte.AddChild(bild);
            var namensZeile = RatStil.Container<HBoxContainer>(spalte, "RatKnotenNamenszeile");
            namensZeile.MouseFilter = Control.MouseFilterEnum.Ignore;
            if (k.Haus is not null)
            {
                var wappen = WappenBild.Neu(k.Haus.Wappen, RatStil.Mass("knoten_wappen"));
                wappen.MouseFilter = Control.MouseFilterEnum.Ignore;
                namensZeile.AddChild(wappen);
            }
            Beschriften(namensZeile, k.Name, "RatKnotenName");
            Beschriften(spalte, k.Haus is null ? "Ratsherr im Amt" : k.IstAmtsinhaber ? "bisheriger Inhaber" : "Bewerber", "RatKnotenInfo");
            if (k.Haus is not null)
            {
                var (sicher, unsicher, waehler) = rat.WahlPrognose(amt, k.Haus);
                Beschriften(spalte, $"{sicher}{(unsicher > 0 ? $" (+{unsicher} offen)" : "")} von {waehler}", "RatKnotenInfo");
            }
            if (istGewaehlt) Beschriften(spalte, "Eure Stimme (Siegel)", "RatKnotenGut");
            reihe.AddChild(knopf);
        }
        if (grund is not null)
            RatStil.Text(_detail, "Eure Stimme: " + grund, "RatHinweis", umbrechen: true);
        else
            RatStil.Text(_detail, gewaehlt is null
                ? $"Ihr habt keine Stimme festgelegt – es gilt die Vorgabe: {vorgabe?.Name}. Ein Klick auf ein Porträt legt sie fest, bis zur Sitzung am Jahresende änderbar."
                : $"Eure Stimme für {gewaehlt.Name} ist festgelegt (bis zur Sitzung am Jahresende änderbar).", "RatHinweis", umbrechen: true);
    }

    private static void Beschriften(Container eltern, string text, string variante)
    {
        var l = RatStil.Text(eltern, text, variante);
        l.ClipText = true;
        l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        l.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        l.MouseFilter = Control.MouseFilterEnum.Ignore;
    }
}
