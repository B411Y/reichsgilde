using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Familientafel: Oberhaupt, Ehepartner, Kinder und Ahnen, die Erbfolge mit Testament –
/// und für ein lediges Oberhaupt die Brautschau mit Geschenken und Heirat.
///
/// Wie alle Bedienfelder ändert sie nichts selbst, sondern schickt Befehle an die Stadtwirtschaft.
/// </summary>
public partial class FamilienAnzeige : PanelContainer
{
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.72f, 0.72f, 0.72f);
    private static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);
    private const int Breite = 900;

    private Simulation? _simulation;
    private Konto? _spieler;
    private VBoxContainer _inhalt = null!;
    /// <summary>
    /// Nur der Betrag wird gemerkt – das Zahlenfeld entsteht bei jedem Aufbau neu.
    /// Früher wurde EIN Feld umgehängt. War der Spieler verheiratet, blieb es in einer alten Zeile stecken,
    /// wurde mit ihr gelöscht, und nach dem Tod des Ehepartners griff die Brautschau ins Leere (Absturz).
    /// </summary>
    private double _geschenkBetrag = 200;
    private Label _meldung = null!;

    /// <summary>Ergebnis eines Befehls – für das Protokoll.</summary>
    public event Action<string>? Gemeldet;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(Breite, 0);
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
        _inhalt.AddThemeConstantOverride("separation", 6);
        spalte.AddChild(_inhalt);

        _meldung = Hauptmenue.Text(spalte, "", 13, Grau, umbruchBreite: Breite - 50);
        var schliessen = Hauptmenue.Knopf(spalte, "Schließen", Hide);
        schliessen.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        Rollbereich.Einhuellen(this);
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
        Aufbauen();
    }

    private Label Text(Container eltern, string text, int groesse, Color? farbe = null, bool umbrechen = false) =>
        Hauptmenue.Text(eltern, text, groesse, farbe, umbruchBreite: umbrechen ? Breite - 50 : 0);

    /// <summary>Baut den Inhalt neu auf – beim Öffnen, nach jedem Befehl und zum Jahreswechsel.</summary>
    public void Aufbauen()
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null) return;
        var buch = w.Familien;
        UiHilfe.KinderEntfernen(_inhalt);
        if (buch.Von(_spieler) is not { } f)
        {
            Text(_inhalt, "Euer Haus hat keine Familie (Spielstand von vor der Dynastie).", 15, Grau);
            return;
        }
        var waehrung = w.Daten.Allgemein.Waehrung;
        var jahr = buch.Jahr;
        var o = f.Oberhaupt;

        Hauptmenue.Text(_inhalt, $"Haus {f.Nachname}", 26, Gold, zentriert: true);
        Hauptmenue.Text(_inhalt, $"{f.Volk.Mehrzahl()} · {w.StandVon(_spieler).Name} · im Jahr {jahr}", 14, Grau, zentriert: true);
        if (_spieler.IstErloschen)
            Hauptmenue.Text(_inhalt, "Das Haus ist erloschen.", 18, Rot, zentriert: true);

        // ── Oberhaupt und Ehe ──
        Text(_inhalt, $"{o.OberhauptTitel}: {o.Name}, {o.Volk.Einzahl()}, {o.Alter(jahr)} Jahre – {buch.Verfassung(o)}" +
                      (o.Krankheit is { } krank ? $" · krank ({krank.Name})" : ""), 16);
        if (buch.HatTalente)
        {
            var talente = Text(_inhalt, "Talente: " + TalentText(o) + "   ⓘ", 14, Gold);
            talente.TooltipText = Talentwirkung(w, buch);
            talente.MouseFilter = MouseFilterEnum.Stop;
            if (w.HatAktionspunkte && !_spieler!.IstErloschen)
            {
                UebenZeile(w, buch);
                LesenZeile(w);
                HeiligenduftZeile(w);
            }
        }
        if (f.Ehepartner is { } e)
        {
            Text(_inhalt, $"{e.Ehepartnertitel}: {e.NameMitHerkunft}, {e.Volk.Einzahl()}, {e.Alter(jahr)} Jahre – {buch.Verfassung(e)}", 15);
            if (w.Diebe.IstGeisel(e))
                Text(_inhalt, $"{e.Vorname} ist als Geisel verschleppt – Lösegeld im Hinterzimmer.", 14, new Color(0.95f, 0.45f, 0.4f));
            if (buch.HatTalente)
                Text(_inhalt, $"Talente: {TalentText(e)} – zählen erst, wenn {(e.Geschlecht == Geschlecht.Frau ? "sie" : "er")} das Haus führt; die Kinder erben davon.",
                    12, Grau, umbrechen: true);
            var chance = buch.Geburtschance(f);
            Text(_inhalt, chance > 0 ? $"Chance auf ein Kind in diesem Jahr: {Zahlenformat.Ganz(chance * 100)} %"
                : "In diesem Alter sind keine Kinder mehr zu erwarten.", 13, Grau);
        }
        else
            Text(_inhalt, f.Ahnen.Count > 0 || f.Mitglieder.Any(p => !p.Lebt && p != o) ? "Ledig oder verwitwet – unten in der Brautschau findet sich eine Partie." : "Noch ledig – unten in der Brautschau findet sich eine Partie.", 14, Grau);
        var ausbildung = buch.Ausbildungskosten(f);
        Text(_inhalt, $"Haushalt und Unterhalt: {Zahlenformat.Ganz(buch.Haushaltskosten(f))} {waehrung} im Jahr" +
                      (ausbildung > 0 ? $" · Ausbildung: {Zahlenformat.Ganz(ausbildung)} {waehrung}" : ""), 13, Grau);
        GesundheitAbschnitt(w, f, waehrung);

        // ── Kinder ──
        var kinder = f.Kinder.ToList();
        if (kinder.Count > 0)
        {
            _inhalt.AddChild(new HSeparator());
            Text(_inhalt, "Kinder", 16);
            var raster = new GridContainer { Columns = buch.HatTalente ? 6 : 4 };
            raster.AddThemeConstantOverride("h_separation", 14);
            raster.AddThemeConstantOverride("v_separation", 2);
            _inhalt.AddChild(raster);
            foreach (var k in kinder)
            {
                var istErbe = f.Testament == k;
                var farbe = !k.Lebt ? Grau : istErbe ? Gold : (Color?)null;
                Text(raster, $"{k.Vorname} ({k.SohnOderTochter})", 14, farbe);
                Text(raster, k.Lebt ? $"{k.Alter(jahr)} Jahre" : $"† {k.Todesjahr} mit {k.Alter(jahr)}", 14, farbe);
                Text(raster, !k.Lebt ? (k.Todesursache == "in die Fremde verschleppt" ? "verschleppt" : "")
                    : w.Diebe.IstGeisel(k) ? "Geisel!" : buch.IstVolljaehrig(k) ? "volljährig" : $"mündig mit {buch.VolkDaten(k.Volk).Volljaehrig}", 13,
                    k.Lebt && w.Diebe.IstGeisel(k) ? new Color(0.95f, 0.45f, 0.4f) : farbe ?? Grau);
                if (buch.HatTalente)
                {
                    var staerken = Text(raster, k.Lebt ? StaerkenText(k) : "", 13, farbe ?? Grau);
                    staerken.TooltipText = k.Lebt ? TalentText(k) : "";
                    staerken.MouseFilter = MouseFilterEnum.Stop;
                    AusbildungZelle(raster, w, buch, k, farbe);
                }
                if (k.Lebt && !istErbe && !_spieler.IstErloschen)
                {
                    var knopf = new Button { Text = "Als Erben einsetzen", FocusMode = FocusModeEnum.None };
                    knopf.Pressed += () => Befehlen(new ErbenBestimmen(k.Nummer));
                    raster.AddChild(knopf);
                }
                else
                    Text(raster, istErbe ? "Erbe laut Testament" : "", 13, Gold);
            }
        }

        // ── Erbfolge ──
        if (!_spieler.IstErloschen)
        {
            var erbe = buch.Erbe(f);
            Text(_inhalt, erbe is null
                    ? $"Stürbe {o.Vorname} heute, erlösche das Haus – es gibt keinen volljährigen Erben{(buch.Daten.Witwenrecht ? " und keinen Ehepartner" : "")}!"
                    : erbe == f.Ehepartner ? $"Stürbe {o.Vorname} heute, führte {erbe.Vorname} das Haus nach dem Witwenrecht weiter."
                    : $"Stürbe {o.Vorname} heute, erbte {erbe.Vorname}.",
                14, erbe is null ? Rot : Grau, umbrechen: true);
        }
        if (f.Ahnen.Count > 0)
            Text(_inhalt, "Ahnen: " + string.Join(" → ", f.Ahnen.Select(a => $"{a.Vorname} († {a.Todesjahr}, {a.Alter(jahr)} J.)")), 13, Grau, umbrechen: true);

        if (f.Ehepartner is null && !_spieler.IstErloschen)
            BrautschauAufbauen(w, buch, f);

        // ── Chronik ──
        var chronik = buch.DiesesJahr.Concat(buch.LetztesJahr).Where(x => x.Konto == _spieler).Select(x => x.Text).ToList();
        if (chronik.Count > 0)
        {
            _inhalt.AddChild(new HSeparator());
            Text(_inhalt, "Familienchronik", 16);
            foreach (var eintrag in chronik.Take(6))
                Text(_inhalt, "• " + eintrag, 13, null, umbrechen: true);
        }
    }

    /// <summary>Hausapotheke und alle Kranken im Haus – mit Medicus und Kräuterschnaps.</summary>
    private void GesundheitAbschnitt(Stadtwirtschaft w, Familie f, string waehrung)
    {
        var g = w.Gesundheit;
        if (!g.IstAktiv || _spieler!.IstErloschen) return;
        _inhalt.AddChild(new HSeparator());
        Text(_inhalt, g.IstPestjahr ? "Gesundheit – die Pest ist in der Stadt!" : "Gesundheit", 16, g.IstPestjahr ? Rot : null);

        var schnaps = g.Daten.Schnaps;
        var apotheke = new HBoxContainer();
        apotheke.AddThemeConstantOverride("separation", 8);
        _inhalt.AddChild(apotheke);
        Text(apotheke, $"Hausapotheke: {g.Apotheke(_spieler)} × {schnaps}", 14);
        foreach (var stueck in new[] { 1, 3 })
        {
            var anzahl = stueck;
            var kaufen = new Button { Text = $"{anzahl} kaufen ({Zahlenformat.Ganz(g.SchnapsPreis * anzahl)} {waehrung})", FocusMode = FocusModeEnum.None };
            kaufen.TooltipText = $"{schnaps} vom Markt: heilt leichte Krankheiten (Husten, Angst im Dunkeln, Lachanfälle) und senkt bei der Pest das Sterberisiko.";
            kaufen.Pressed += () => Befehlen(new SchnapsKaufen(anzahl));
            apotheke.AddChild(kaufen);
        }

        var kranke = f.Lebende.Where(p => p.Krankheit is not null).ToList();
        if (kranke.Count == 0)
        {
            Text(_inhalt, "Alle im Haus sind gesund.", 13, Grau);
            return;
        }
        foreach (var p in kranke)
        {
            var k = p.Krankheit!;
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 8);
            _inhalt.AddChild(zeile);
            var risiko = g.Sterberisiko(k);
            var bis = k.Bis <= w.Familien.Jahr ? "bis Jahresende" : $"bis Ende {k.Bis}";
            Text(zeile, $"{p.Vorname}: {k.Beschreibung} – {bis}{(risiko > 0 ? $" · {Zahlenformat.Ganz(risiko * 100)} % tödlich" : "")}", 14,
                risiko > 0 ? Rot : null);

            var person = p.Nummer;
            if (k.NurGegengift)
            {
                // Gift: Weder Medicus noch Kräuterschnaps helfen – nur Gegengift.
                var preis = g.GegengiftPreis(_spieler!);
                var gegengift = new Button
                {
                    Text = $"{g.Daten.Gegengift} ({(preis > 0 ? $"{Zahlenformat.Ganz(preis)} {waehrung}" : "aus dem Lager")})",
                    FocusMode = FocusModeEnum.None, Disabled = _spieler!.Vermoegen < preis,
                    TooltipText = "Heilt die Vergiftung sofort. Medicus und Kräuterschnaps helfen gegen Gift nicht.",
                };
                gegengift.Pressed += () => Befehlen(new GegengiftGeben(person));
                zeile.AddChild(gegengift);
                continue;
            }
            var medicus = new Button { Text = $"Medicus ({Zahlenformat.Ganz(g.MedicusKosten(k))} {waehrung})", FocusMode = FocusModeEnum.None };
            medicus.Disabled = k.IstPest && k.Medicus;
            medicus.TooltipText = medicus.Disabled ? "Der Medicus pflegt schon." :
                k.IstPest ? "Halbiert das Sterberisiko an der Pest." : "Heilt die Krankheit sofort.";
            medicus.Pressed += () => Befehlen(new MedicusRufen(person));
            zeile.AddChild(medicus);

            var geben = new Button { Text = schnaps, FocusMode = FocusModeEnum.None };
            string? gesperrt = g.Apotheke(_spieler) <= 0 ? "Die Hausapotheke ist leer."
                : k.IstPest ? (k.Schnaps ? "Hat schon Kräuterschnaps bekommen." : null)
                : !k.Schnapsheilbar ? $"Gegen {k.Name} hilft nur der Medicus." : null;
            geben.Disabled = gesperrt is not null;
            geben.TooltipText = gesperrt ?? (k.IstPest ? "Senkt das Sterberisiko an der Pest etwas." : "Heilt die Krankheit sofort.");
            geben.Pressed += () => Befehlen(new SchnapsGeben(person));
            zeile.AddChild(geben);
        }
    }

    /// <summary>Je Talent ein Knopf „üben" – ½ Stern für Aktionspunkte, einmal je Talent und Jahr.</summary>
    private void UebenZeile(Stadtwirtschaft w, Familienbuch buch)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 6);
        _inhalt.AddChild(zeile);
        var schritt = TalentTexte.Sterne(w.Daten.Aktionspunkte?.TrainingSchritt ?? 0.5);
        Text(zeile, $"Üben (+{schritt} Stern):", 13, Grau);
        foreach (var t in TalentTexte.Alle)
        {
            var talent = t;
            var knopf = new Button { Text = t.Kurz(), FocusMode = FocusModeEnum.None };
            knopf.AddThemeFontSizeOverride("font_size", 13);
            if (buch.TrainingVerboten(_spieler!, t) is { } grund)
            {
                knopf.Disabled = true;
                knopf.TooltipText = grund;
            }
            else
                knopf.TooltipText = $"{t.Name()} üben: +{schritt} Stern (einmal im Jahr je Talent). Je höher das Talent, desto mehr Aktionspunkte kostet es.";
            knopf.Pressed += () => Befehlen(new TalentTrainieren(talent));
            UiHilfe.ApKosten(knopf, w, _spieler!, new TalentTrainieren(t));
            zeile.AddChild(knopf);
        }
    }

    /// <summary>Je Talent ein Knopf „Lehrschrift lesen" – ½ Stern bis 3 Sterne, einmal im Jahr (aus dem Lager oder vom Markt).</summary>
    private void LesenZeile(Stadtwirtschaft w)
    {
        var kw = w.Kirchenwesen;
        if (!kw.IstAktiv) return;
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 6);
        _inhalt.AddChild(zeile);
        var preis = kw.Schriftpreis(_spieler!, kw.Daten.Lehrschrift);
        var herkunft = preis > 0 ? $"{Zahlenformat.Ganz(preis)} {w.Daten.Allgemein.Waehrung} am Markt" : "aus dem Lager";
        Text(zeile, $"Lehrschrift ({herkunft}):", 13, Grau);
        foreach (var t in TalentTexte.Alle)
        {
            var talent = t;
            var knopf = new Button { Text = t.Kurz(), FocusMode = FocusModeEnum.None };
            knopf.AddThemeFontSizeOverride("font_size", 13);
            if (kw.LesenVerboten(_spieler!, t) is { } grund)
            {
                knopf.Disabled = true;
                knopf.TooltipText = grund;
            }
            else
                knopf.TooltipText = $"Eine Lehrschrift über {t.Name()} lesen: +½ Stern, aber nur bis {TalentTexte.Sterne(kw.Daten.LehrschriftBis)} Sterne. Einmal im Jahr.";
            knopf.Pressed += () => Befehlen(new LehrschriftLesen(talent));
            UiHilfe.ApKosten(knopf, w, _spieler!, new LehrschriftLesen(t));
            zeile.AddChild(knopf);
        }
    }

    /// <summary>Heiligenduft in der Kirche stiften – mehr Ansehen bis Ende des nächsten Jahres.</summary>
    private void HeiligenduftZeile(Stadtwirtschaft w)
    {
        var duft = w.Duft;
        if (!duft.IstAktiv) return;
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 6);
        _inhalt.AddChild(zeile);
        var d = duft.Daten.Heiligenduft;
        var preis = duft.Preis(_spieler!, d.Ware);
        Text(zeile, $"{d.Ware} ({(preis > 0 ? $"{Zahlenformat.Ganz(preis)} {w.Daten.Allgemein.Waehrung} am Markt" : "aus dem Lager")}):", 13, Grau);
        var knopf = new Button { Text = "in der Kirche stiften", FocusMode = FocusModeEnum.None };
        knopf.AddThemeFontSizeOverride("font_size", 13);
        var grund = duft.HeiligenduftVerboten(_spieler!);
        knopf.Disabled = grund is not null;
        knopf.TooltipText = grund ?? $"+{Zahlenformat.Ganz(d.Ansehen)} Ansehen bis Ende des nächsten Jahres (Wahl, Brautschau, Kirche).";
        knopf.Pressed += () => Befehlen(new HeiligenduftStiften());
        UiHilfe.ApKosten(knopf, w, _spieler!, new HeiligenduftStiften());
        zeile.AddChild(knopf);
        if (duft.HeiligBis(_spieler!) is int bis)
            Text(zeile, $"· wirkt bis Ende {bis} (+{Zahlenformat.Ganz(d.Ansehen)} Ansehen)", 13, Gold);
    }

    /// <summary>„Verhandeln 2 · Handwerk 1½ · Nacht ½ · Kampf 1½ · Rhetorik 2"</summary>
    private static string TalentText(Person p) =>
        string.Join(" · ", TalentTexte.Alle.Select(t => $"{t.Kurz()} {TalentTexte.Sterne(p.Talent(t))}"));

    /// <summary>Die zwei besten Talente – für enge Spalten.</summary>
    private static string StaerkenText(Person p) =>
        string.Join(", ", p.Staerken.Take(2).Select(x => $"{x.Talent.Kurz()} {TalentTexte.Sterne(x.Wert)}"));

    private static string Talentwirkung(Stadtwirtschaft w, Familienbuch buch)
    {
        var m = buch.TalentDaten.Wirkung;
        string P(double x) => Zahlenformat.Kommazahl(x * 100);
        return "Es zählen die Talente des Oberhaupts – je Stern:\n" +
               $"• Verhandeln: +{P(m.Verhandeln)} % Erlös auf dem Markt\n" +
               $"• Handwerkskunst: +{P(m.Handwerk)} % Leistung aller Arbeitskräfte\n" +
               $"• Bei Nacht und Nebel: +{P(m.NachtErfolg)} Prozentpunkte Erfolg im Hinterzimmer, −{P(m.NachtZeugen)} % Zeugen\n" +
               $"• Kampf: +{P(m.KampfUeberfall)} Prozentpunkte Stärke angeheuerter Wegelagerer, +{P(m.KampfSchutz)} Prozentpunkte Schutz der eigenen Karren\n" +
               $"• Rhetorik: +{Zahlenformat.Kommazahl(m.RhetorikRat)} Punkte bei Ratswahlen, {Zahlenformat.Kommazahl(m.RhetorikGericht)} Punkt Plädoyer vor Gericht, +{P(m.RhetorikWerben)} % Wirkung der Brautgeschenke\n" +
               $"Höchstens {Zahlenformat.Kommazahl(buch.TalentDaten.Hoechstwert)} Sterne. Kinder erben die Hälfte vom Mittel der Eltern, den Rest vom Volk – und lernen in der Ausbildung dazu.";
    }

    /// <summary>
    /// Ausbildung eines Kindes: Auswahl für Unmündige, Abschluss für Volljährige.
    /// Die Liste zeigt alle Wege; verschlossene (Stand) sind ausgegraut.
    /// </summary>
    private void AusbildungZelle(GridContainer raster, Stadtwirtschaft w, Familienbuch buch, Person k, Color? farbe)
    {
        if (!k.Lebt || buch.IstVolljaehrig(k) || _spieler!.IstErloschen)
        {
            Text(raster, k.Lebt && k.Abschluss is { } abschluss ? abschluss : "", 13, farbe ?? Grau);
            return;
        }
        var waehrung = w.Daten.Allgemein.Waehrung;
        var auswahl = new OptionButton { FocusMode = FocusModeEnum.None };
        auswahl.AddItem("keine Ausbildung", 0);
        var wege = buch.TalentDaten.Ausbildungen.ToList();
        for (var i = 0; i < wege.Count; i++)
        {
            var (name, a) = wege[i];
            auswahl.AddItem($"{name} ({Zahlenformat.Ganz(a.Kosten)} {waehrung}/Jahr)", i + 1);
            var index = auswahl.ItemCount - 1;
            var zuwachs = string.Join(", ", a.Talente.Select(x => $"{x.Key.Kurz()} +{Zahlenformat.Kommazahl(x.Value * buch.Ausbildungsfaktor(k.Volk))}"));
            auswahl.SetItemTooltip(index, $"{a.Beschreibung}\nJe Jahr: {zuwachs}");
            if (buch.AusbildungVerboten(_spieler!, name) is { } grund)
            {
                auswahl.SetItemDisabled(index, true);
                auswahl.SetItemTooltip(index, grund);
            }
            if (k.Ausbildung == name) auswahl.Select(index);
        }
        if (k.Ausbildung is null) auswahl.Select(0);
        var ab = buch.AusbildungAb(k.Volk);
        auswahl.TooltipText = k.Alter(buch.Jahr) < ab ? $"Die Ausbildung beginnt mit {ab} Jahren – vorher kostet sie nichts." : "Läuft bis zur Volljährigkeit.";
        auswahl.ItemSelected += index =>
        {
            var id = auswahl.GetItemId((int)index);
            Befehlen(new AusbildungWaehlen(k.Nummer, id == 0 ? null : wege[id - 1].Key));
        };
        raster.AddChild(auswahl);
    }

    private void BrautschauAufbauen(Stadtwirtschaft w, Familienbuch buch, Familie f)
    {
        var waehrung = w.Daten.Allgemein.Waehrung;
        var d = buch.Daten;
        _inhalt.AddChild(new HSeparator());
        Text(_inhalt, "Brautschau", 16);

        var geschenkZeile = new HBoxContainer();
        _inhalt.AddChild(geschenkZeile);
        Text(geschenkZeile, "Geschenk je Klick:", 14);
        var geschenk = new Zahlfeld { MinValue = 10, MaxValue = 5000, Step = 10, Value = _geschenkBetrag, Suffix = waehrung, CustomMinimumSize = new Vector2(150, 0) };
        geschenkZeile.AddChild(geschenk);
        var jeZuneigung = buch.GuldenJeZuneigung(_spieler!);
        string Hinweis() => $"   (+{Zahlenformat.Ganz(_geschenkBetrag / jeZuneigung)} Zuneigung, höchstens {Zahlenformat.Ganz(d.ZuneigungJeJahr)} im Jahr)";
        var betragHinweis = Text(geschenkZeile, Hinweis(), 12, Grau);
        geschenk.ValueChanged += wert => { _geschenkBetrag = wert; betragHinweis.Text = Hinweis(); };

        var mitTalenten = buch.HatTalente;
        var raster = new GridContainer { Columns = mitTalenten ? 8 : 7 };
        raster.AddThemeConstantOverride("h_separation", 12);
        raster.AddThemeConstantOverride("v_separation", 3);
        _inhalt.AddChild(raster);
        foreach (var kopf in mitTalenten
                     ? new[] { "Name", "Volk, Alter", "Stand", "Stärken", "Kinder ≈", "Hochzeit", "Bereit", "" }
                     : new[] { "Name", "Volk, Alter", "Stand", "Kinder ≈", "Hochzeit", "Bereit", "" })
            Text(raster, kopf, 12, Grau);

        var passende = buch.Kandidaten.Where(k => k.Person.Geschlecht != f.Oberhaupt.Geschlecht)
            .OrderByDescending(k => buch.Zuneigung(_spieler!, k)).ThenBy(k => k.Stand).ThenBy(k => k.Person.Alter(buch.Jahr)).ToList();
        foreach (var k in passende)
        {
            var p = k.Person;
            var verboten = buch.WerbenVerboten(_spieler!, k);
            var bereit = buch.Bereitschaft(_spieler!, k);
            var schwelle = buch.Heiratsschwelle(k);
            var farbe = verboten is not null ? Grau : bereit >= schwelle ? Gruen : (Color?)null;
            var rivalen = buch.Mitbewerber(_spieler!, k).Select(x => x.Inhaber).ToList();
            var name = Text(raster, p.Name + (rivalen.Count > 0 ? " ⚔" : ""), 14, farbe);
            if (rivalen.Count > 0) { name.TooltipText = $"Auch {string.Join(", ", rivalen)} wirbt um {p.Vorname}."; name.MouseFilter = MouseFilterEnum.Stop; }
            Text(raster, $"{p.Volk.Einzahl()}, {p.Alter(buch.Jahr)}", 14, farbe);
            Text(raster, w.Daten.Staende[k.Stand].Name, 14, farbe);
            if (mitTalenten)
            {
                var staerken = Text(raster, StaerkenText(p), 13, farbe ?? Grau);
                staerken.TooltipText = TalentText(p);
                staerken.MouseFilter = MouseFilterEnum.Stop;
            }
            Text(raster, Zahlenformat.Kommazahl(buch.ErwarteteKinder(f, p)), 14, farbe);
            Text(raster, $"{Zahlenformat.Ganz(buch.Hochzeitskosten(k))}", 14, farbe);
            // Abrunden: 29,6 darf nicht als „30 / 30" erscheinen, solange die Heirat noch nicht geht.
            Text(raster, $"{Math.Floor(bereit + 1e-9)} / {Zahlenformat.Ganz(schwelle)}", 14, farbe);

            var knoepfe = new HBoxContainer();
            knoepfe.AddThemeConstantOverride("separation", 4);
            raster.AddChild(knoepfe);
            if (verboten is not null)
            {
                var hinweis = Text(knoepfe, "nicht standesgemäß", 12, Grau);
                hinweis.TooltipText = verboten;
                hinweis.MouseFilter = MouseFilterEnum.Stop;
                continue;
            }
            if (bereit >= schwelle)
            {
                var heiraten = new Button { Text = "Heiraten", FocusMode = FocusModeEnum.None };
                heiraten.Pressed += () => Befehlen(new Heiraten(p.Nummer));
                UiHilfe.ApKosten(heiraten, w, _spieler!, new Heiraten(0));
                knoepfe.AddChild(heiraten);
            }
            else
            {
                var werben = new Button { Text = "Werben", FocusMode = FocusModeEnum.None, Disabled = buch.ZuneigungRest(_spieler!, k) <= 0.001 };
                werben.TooltipText = werben.Disabled ? $"{p.Vorname} braucht Zeit – im nächsten Jahr geht es weiter." : "";
                werben.Pressed += () => Befehlen(new Werben(p.Nummer, _geschenkBetrag));
                UiHilfe.ApKosten(werben, w, _spieler!, new Werben(0, 0));
                knoepfe.AddChild(werben);
                if (w.Duft.IstAktiv)
                {
                    var duft = w.Duft;
                    var moschus = new Button { Text = "Moschus", FocusMode = FocusModeEnum.None };
                    var grund = duft.MoschusVerboten(_spieler!, p.Nummer);
                    var preis = duft.Preis(_spieler!, duft.Daten.Moschus.Ware);
                    moschus.Disabled = grund is not null;
                    moschus.TooltipText = grund ?? $"{duft.Daten.Moschus.Ware} schenken ({(preis > 0 ? $"{Zahlenformat.Ganz(preis)} {w.Daten.Allgemein.Waehrung} am Markt" : "aus dem Lager")}): " +
                                                   $"+{Zahlenformat.Ganz(duft.Daten.Moschus.Zuneigung)} Zuneigung – zusätzlich zu den Geschenken des Jahres. Einmal im Jahr.";
                    moschus.Pressed += () => Befehlen(new MoschusSchenken(p.Nummer));
                    UiHilfe.ApKosten(moschus, w, _spieler!, new MoschusSchenken(0));
                    knoepfe.AddChild(moschus);
                }
            }
        }
        Text(_inhalt, $"„Bereit“ = Zuneigung + Ansehen eures Hauses ({Zahlenformat.Kommazahl(w.Rat.Ansehen(_spieler!))}, aus Stand und Vermögen). " +
                      $"Ab der Schwelle willigt die Person ein. Die Zuneigung verblasst um {Zahlenformat.Ganz(d.ZuneigungVerfall * 100)} % im Jahr, " +
                      $"und jedes Jahr heiraten manche anderweitig. Ehen zwischen Völkern sind erlaubt, bringen aber weniger Kinder; " +
                      "die Kinder gehören immer dem Volk eures Hauses an. ⚔ = ein anderes Haus wirbt mit.", 12, Grau, umbrechen: true);
    }
}
