using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Ratskammer: alle Ämter mit Inhabern, Haltung zum Statut und eurer Gunst bei jedem Ratsherrn –
/// dazu Bewerbung, Geschenke und Anträge zum Statut, jeweils mit einer Prognose nach heutigem Stand.
/// Wer im Rat sitzt, legt hier auch seine Stimme bei den Wahlen und den Anträgen vorab fest (bis zur Sitzung am Jahresende änderbar).
/// Mit Stadtkasse (PR #200) dazu die Tagesordnung und die Anträge zu Steuer, Bau und Fest (RatAnzeige.Antraege.cs).
///
/// Wie alle Bedienfelder ändert sie nichts selbst, sondern schickt Befehle an die Stadtwirtschaft.
/// </summary>
public partial class RatAnzeige : PanelContainer
{
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.72f, 0.72f, 0.72f);
    private static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);

    private Simulation? _simulation;
    private Konto? _spieler;
    private VBoxContainer _inhalt = null!;
    /// <summary>Nur der Betrag wird gemerkt – das Zahlenfeld entsteht bei jedem Aufbau neu (siehe FamilienAnzeige).</summary>
    private double _geschenkBetrag = 200;
    private Label _meldung = null!;

    /// <summary>Ergebnis eines Befehls – für das Protokoll.</summary>
    public event Action<string>? Gemeldet;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(720, 0);
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.11f, 0.10f, 1f),
            BorderColor = Gold * new Color(1, 1, 1, 0.8f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
        });
        var rand = new MarginContainer();
        foreach (var seite in new[] { "left", "right", "top", "bottom" })
            rand.AddThemeConstantOverride($"margin_{seite}", 22);
        AddChild(rand);
        var spalte = new VBoxContainer();
        spalte.AddThemeConstantOverride("separation", 8);
        rand.AddChild(spalte);

        _inhalt = new VBoxContainer();
        _inhalt.AddThemeConstantOverride("separation", 8);
        spalte.AddChild(_inhalt);

        _meldung = Hauptmenue.Text(spalte, "", 13, Grau, umbruchBreite: 670);
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

    public void Oeffnen()
    {
        _meldung.Text = "";
        Aufbauen();
        Show();
    }

    private void Befehlen(Befehl befehl)
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null) return;
        var ergebnis = w.Ausfuehren(_spieler, befehl);
        _meldung.Text = ergebnis.Erfolg ? ergebnis.Meldung : "✗ " + ergebnis.Meldung;
        _meldung.AddThemeColorOverride("font_color", ergebnis.Erfolg ? Gruen : Rot);
        Gemeldet?.Invoke(_meldung.Text);
        Aufbauen(); // Gunst, Prognosen und Knöpfe haben sich geändert
    }

    /// <summary>Baut den Inhalt neu auf – beim Öffnen und nach jedem Befehl.</summary>
    public void Aufbauen()
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null) return;
        var rat = w.Rat;
        var waehrung = w.Daten.Allgemein.Waehrung;
        UiHilfe.KinderEntfernen(_inhalt);

        Hauptmenue.Text(_inhalt, rat.Daten.Kammer, 26, Gold, zentriert: true);
        Hauptmenue.Text(_inhalt, $"Geltendes Fron- und Sklavenstatut: {w.Statut.Kuerzel()} – {w.Statut.Name()}", 15, null, zentriert: true);
        if (w.Glaube.IstAktiv)
            Hauptmenue.Text(_inhalt, $"Stadtglaube: {KonfessionTexte.StadtglaubeText(w.Glaube.Stadtglaube)}" +
                                     (w.Glaube.Stadtglaube is null ? "" : $" – Andersgläubige zahlen {Zahlenformat.Ganz(w.Glaube.Daten.Stadtglaube.Abgabe)} {waehrung} Glaubenszins im Jahr"),
                15, null, zentriert: true);

        // ── Die Ämter ──
        var geschenkZeile = new HBoxContainer();
        _inhalt.AddChild(geschenkZeile);
        Hauptmenue.Text(geschenkZeile, "Geschenk je Klick:", 14);
        var geschenk = new Zahlfeld { MinValue = 10, MaxValue = 5000, Step = 10, Value = _geschenkBetrag, Suffix = waehrung, CustomMinimumSize = new Vector2(150, 0) };
        geschenkZeile.AddChild(geschenk);
        string Hinweis() => $"   (+{Zahlenformat.Ganz(_geschenkBetrag * rat.Daten.Gunst.JeGulden)} Gunst, verblasst um {Zahlenformat.Ganz(rat.Daten.Gunst.VerfallJeJahr * 100)} % im Jahr)";
        var hinweis = Hauptmenue.Text(geschenkZeile, Hinweis(), 12, Grau);
        geschenk.ValueChanged += wert => { _geschenkBetrag = wert; hinweis.Text = Hinweis(); };

        var raster = new GridContainer { Columns = 7 };
        raster.AddThemeConstantOverride("h_separation", 14);
        raster.AddThemeConstantOverride("v_separation", 4);
        _inhalt.AddChild(raster);
        var kopfTipps = new[] { "", "Rang des Amtes: höhere Ämter entscheiden mehr.", "Das Amt mit seinem Einkommen im Jahr.", "Wer das Amt innehat: ein Haus (Wappen) oder ein Ratsherr.", "Wofür ein Ratsherr eintritt; danach stimmt er bei Anträgen.", "Wie gewogen euch der Ratsherr ist. Geschenke erhöhen die Gunst, sie zählt bei Wahlen und Anträgen.", "" };
        var kopfNr = 0;
        foreach (var kopf in new[] { "", "Stufe", "Amt", "Inhaber", "Haltung", "Eure Gunst", "" })
            Hauptmenue.Text(raster, kopf, 12, Grau).TooltipText = kopfTipps[kopfNr++];
        foreach (var amt in rat.Aemter)
        {
            var eigenes = amt.Konto == _spieler;
            var farbe = eigenes ? Gold : (Color?)null;
            var bild = PortraetAnzeige.Neu(PortraetAnzeige.Klein); // der Inhaber (PR #211)
            if (bild.ZeigenAmt(w, amt)) raster.AddChild(bild);
            else raster.AddChild(new Control { CustomMinimumSize = new Vector2(PortraetAnzeige.Klein, PortraetAnzeige.Klein) }); // freier Sitz
            Hauptmenue.Text(raster, amt.Stufe.ToString(), 14, farbe).TooltipText = $"Stufe {amt.Stufe} der Ämter.";
            Hauptmenue.Text(raster, $"{amt.Name} ({Zahlenformat.Ganz(amt.Daten.Einkommen)} {waehrung}/Jahr)", 14, farbe).TooltipText = $"{amt.Name}: Einkommen {Zahlenformat.Ganz(amt.Daten.Einkommen)} {waehrung} im Jahr.";
            var glaube = !w.Glaube.IstAktiv ? null : amt.Ratsherr is { } rg ? w.Glaube.GlaubeVon(rg) : amt.Konto is { } hk ? w.Glaube.GlaubeVon(hk) : null;
            var kurz = glaube switch { Konfession.Katholisch => ", kath.", Konfession.Evangelisch => ", ev.", Konfession.Naturglaube => ", Heide", _ => "" };
            var inhaber = UiHilfe.MitWappen(raster, amt.Ratsherr is null ? amt.Konto?.Wappen : null, (amt.Ratsherr is { } r ? $"{r.Name}, {r.Daten.Volk.Einzahl()}" : amt.Inhaber) + kurz, 14, farbe);
            if (amt.Ratsherr is { } rn && w.Glaube.IstAktiv)
            {
                var naehe = w.Glaube.Naehe(rn, _spieler);
                inhaber.TooltipText = $"{rn.Name} ist {w.Glaube.GlaubeVon(rn).Name()} – euer Glaube bringt bei ihm {Zahlenformat.MitVorzeichen(naehe)} Punkte in der Wahl.";
                inhaber.MouseFilter = MouseFilterEnum.Stop;
            }
            if (amt.Ratsherr is { } ratsherr)
            {
                Hauptmenue.Text(raster, $"{ratsherr.Haltung.Kuerzel()} – {ratsherr.Haltung.Name()}", 13).TooltipText = $"{ratsherr.Name} stimmt nach der Haltung {ratsherr.Haltung.Name()}.";
                Hauptmenue.Text(raster, Zahlenformat.Ganz(rat.Gunst(ratsherr, _spieler)), 14).TooltipText = $"Eure Gunst bei {ratsherr.Name}: {Zahlenformat.Ganz(rat.Gunst(ratsherr, _spieler))}.";
                var knoepfe = new HBoxContainer();
                knoepfe.AddThemeConstantOverride("separation", 4);
                raster.AddChild(knoepfe);
                var knopf = new Button { Text = "Beschenken", FocusMode = FocusModeEnum.None };
                knopf.Pressed += () => Befehlen(new Schenken(ratsherr.Name, _geschenkBetrag));
                UiHilfe.ApKosten(knopf, w, _spieler, new Schenken("", 0));
                knoepfe.AddChild(knopf);
                if (w.Duft.IstAktiv)
                {
                    var duft = w.Duft;
                    var preis = duft.Preis(_spieler, duft.Daten.Moschus.Ware);
                    var moschus = new Button { Text = "Moschus", FocusMode = FocusModeEnum.None };
                    var grund = duft.MoschusRatsherrVerboten(_spieler, ratsherr.Name);
                    moschus.Disabled = grund is not null;
                    moschus.TooltipText = grund ?? $"{duft.Daten.Moschus.Ware} schenken ({(preis > 0 ? $"{Zahlenformat.Ganz(preis)} {waehrung} am Markt" : "aus dem Lager")}): " +
                                                   $"+{Zahlenformat.Ganz(duft.Daten.Moschus.Gunst)} Gunst – ein Duft ist keine Bestechung. Einmal im Jahr je Ratsherr.";
                    moschus.Pressed += () => Befehlen(new MoschusRatsherr(ratsherr.Name));
                    UiHilfe.ApKosten(moschus, w, _spieler, new MoschusRatsherr(""));
                    knoepfe.AddChild(moschus);
                }
            }
            else
            {
                Hauptmenue.Text(raster, amt.Konto is null ? "" : "Haus", 13, Grau);
                Hauptmenue.Text(raster, "", 14);
                Hauptmenue.Text(raster, "", 14);
            }
        }

        // ── Laufbahn ──
        _inhalt.AddChild(new HSeparator());
        var eigenesAmt = rat.AmtVon(_spieler);
        Hauptmenue.Text(_inhalt, $"Eure Laufbahn: {w.StandVon(_spieler).Name}, {(eigenesAmt is null ? "noch ohne Amt" : $"im Amt als {eigenesAmt.Name}")}", 16);
        if (rat.Bewerbungen.TryGetValue(_spieler, out var beworben) && rat.AmtNamens(beworben) is { } ziel)
        {
            Hauptmenue.Text(_inhalt, $"Ihr bewerbt euch als {ziel.Name}. Gewählt wird am Jahresende.", 14);
            PrognoseZeigen(rat, ziel);
        }
        else
        {
            var waehlbar = rat.WaehlbareAemter(_spieler).ToList();
            if (waehlbar.Count == 0)
            {
                var naechste = (eigenesAmt?.Stufe ?? 0) + 1;
                var noetig = rat.Aemter.Where(a => a.Stufe == naechste).Select(a => a.Daten.MindestStand).FirstOrDefault();
                Hauptmenue.Text(_inhalt, noetig is null ? "Höher geht es nicht – ihr habt das höchste Amt der Stadt."
                    : $"Für die nächste Stufe braucht ihr mindestens den Stand {noetig}.", 14, Grau);
            }
            else
            {
                var zeile = new HBoxContainer();
                zeile.AddThemeConstantOverride("separation", 10);
                _inhalt.AddChild(zeile);
                var auswahl = new OptionButton { FocusMode = FocusModeEnum.None };
                foreach (var a in waehlbar) auswahl.AddItem($"{a.Name} ({Zahlenformat.Ganz(a.Daten.Einkommen)} {waehrung}/Jahr)");
                zeile.AddChild(auswahl);
                var bewerben = new Button { Text = "Bewerben", FocusMode = FocusModeEnum.None };
                bewerben.Pressed += () => Befehlen(new AmtBewerben(waehlbar[auswahl.Selected].Name));
                UiHilfe.ApKosten(bewerben, w, _spieler, new AmtBewerben(""));
                zeile.AddChild(bewerben);
                foreach (var a in waehlbar) PrognoseZeigen(rat, a, $"{a.Name}: ");
            }
        }

        WahlStimmenAbschnitt(rat, waehrung);
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
        Hauptmenue.Text(_inhalt, "Ratsherren stimmen ihrer Haltung zu – einem Nachbarstatut nur, wenn eure Gunst bei ihnen " +
                                 $"mindestens {Zahlenformat.Ganz(rat.Daten.Gunst.SchwelleNachbarstatut)} beträgt.", 12, Grau, umbruchBreite: 670);

        if (w.Wegebau.IstAktiv)
            WegbauAbschnitt(w, rat, eigenesAmt);

        if (w.Glaube.IstAktiv)
            StadtglaubeAbschnitt(w, eigenesAmt);

        KassenAntraegeAbschnitt(w, eigenesAmt); // Steuer, Bau, Fest (RatAnzeige.Antraege.cs)

        // ── Letzte Sitzung ──
        if (rat.LetzteSitzung.Count > 0)
        {
            _inhalt.AddChild(new HSeparator());
            Hauptmenue.Text(_inhalt, "Letzte Ratssitzung", 16);
            foreach (var eintrag in rat.LetzteSitzung)
                Hauptmenue.Text(_inhalt, "• " + eintrag, 13, null, umbruchBreite: 670);
        }
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
                                 $"{Zahlenformat.Ganz(d.SchwelleFrei)} Gunst); für einen fremden Glauben erst ab {Zahlenformat.Ganz(d.SchwelleFremd)} Gunst. " +
                                 "Gilt ein Stadtglaube, ist jede Messe und Bekehrungspredigt anderen Glaubens Ketzerei.", 12, Grau, umbruchBreite: 670);
    }

    /// <summary>
    /// Die Stimme des Spielers bei den Ämterwahlen: je Wahl eine Auswahl der Kandidaten, die Vorgabe zuerst.
    /// Wer nicht abstimmen darf, sieht den Grund des Kerns in grau; ohne Wahl in der Stadt entfällt der Abschnitt.
    /// </summary>
    private void WahlStimmenAbschnitt(Stadtrat rat, string waehrung)
    {
        var wahlen = rat.Aemter.Where(a => rat.Kandidaten(a).Count > 0).Select(a => (Amt: a, Grund: rat.WahlStimmeVerboten(_spieler!, a))).ToList();
        if (wahlen.Count == 0) return;
        _inhalt.AddChild(new HSeparator());
        Hauptmenue.Text(_inhalt, "Eure Stimme bei den Wahlen", 16);
        if (wahlen.All(x => x.Grund is not null && x.Grund == wahlen[0].Grund))
        {
            // Derselbe Grund für alle Wahlen (z. B. kein Mitglied des Rates): einmal sagen genügt.
            Hauptmenue.Text(_inhalt, $"Dieses Jahr wird gewählt: {string.Join(", ", wahlen.Select(x => x.Amt.Name))}. {wahlen[0].Grund}", 13, Grau, umbruchBreite: 670);
            return;
        }
        Hauptmenue.Text(_inhalt, "Ihr legt die Stimme vorab fest und ändert sie bis zur Ratssitzung am Jahresende, wann ihr wollt. " +
                                 "Ohne Stimme gilt die Vorgabe: der bisherige Inhaber.", 12, Grau, umbruchBreite: 670);
        foreach (var (amt, grund) in wahlen)
        {
            var kandidaten = rat.Kandidaten(amt);
            if (grund is not null)
            {
                Hauptmenue.Text(_inhalt, $"{amt.Name}: {grund}", 13, Grau, umbruchBreite: 670);
                continue;
            }
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 10);
            _inhalt.AddChild(zeile);
            var name = Hauptmenue.Text(zeile, $"{amt.Name}:", 14);
            name.CustomMinimumSize = new Vector2(120, 0);
            var auswahl = new OptionButton { FocusMode = FocusModeEnum.None };
            var vorgabe = rat.VorgabeStimme(amt);
            auswahl.AddItem($"Vorgabe: {vorgabe?.Name}");
            foreach (var k in kandidaten)
                auswahl.AddItem(k.Haus is null ? $"{k.Name} (Ratsherr im Amt)" : k.IstAmtsinhaber ? $"{k.Name} (bisheriger Inhaber)" : k.Name);
            var gewaehlt = rat.WahlStimmeVon(_spieler!, amt);
            auswahl.Selected = gewaehlt is null ? 0 : kandidaten.ToList().FindIndex(k => k.Name == gewaehlt.Name) + 1;
            auswahl.TooltipText = $"Zur Wahl für {amt.Name} ({Zahlenformat.Ganz(amt.Daten.Einkommen)} {waehrung}/Jahr) stehen {kandidaten.Count}. Eure Stimme zählt in der Prognose und in der Sitzung.";
            auswahl.ItemSelected += index => Befehlen(new WahlStimme(amt.Name, index == 0 ? null : kandidaten[(int)index - 1].Name));
            zeile.AddChild(auswahl);
            // Stand der Bewerber nach heutigem Stand – zählt die festgelegte Stimme schon mit.
            var stand = kandidaten.Where(k => k.Haus is not null).Select(k =>
            {
                var (sicher, unsicher, waehler) = rat.WahlPrognose(amt, k.Haus!);
                return $"{k.Name} {sicher}{(unsicher > 0 ? $" (+{unsicher} offen)" : "")} von {waehler}";
            });
            Hauptmenue.Text(_inhalt, "Stimmen heute: " + string.Join(" · ", stand), 12, Grau, umbruchBreite: 670);
        }
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

    private void PrognoseZeigen(Stadtrat rat, Amt amt, string vorsatz = "")
    {
        var (sicher, unsicher, waehler) = rat.WahlPrognose(amt, _spieler!);
        var mehrheit = waehler / 2 + 1;
        var text = $"{vorsatz}Stand heute {sicher} sichere Stimme{(sicher == 1 ? "" : "n")}{(unsicher > 0 ? $" und {unsicher} unentschieden" : "")} " +
                   $"von {waehler} – für die Mehrheit braucht ihr {mehrheit}.";
        Hauptmenue.Text(_inhalt, text, 13, sicher >= mehrheit ? Gruen : Grau, umbruchBreite: 670);
    }
}
