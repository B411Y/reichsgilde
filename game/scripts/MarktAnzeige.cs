using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das Marktfenster: alle Waren als Kacheln mit Icon und Preis, nach Warengruppen geordnet – wie der Markt im
/// Original. Der Preis ist grün, wenn er über dem Grundpreis liegt (gut zum Verkaufen), rot darunter (gut zum Einkaufen).
/// Einzelheiten (Nachfrage, Zutaten, Gewinn je Minute, eigener Bestand) stehen im Tooltip.
///
/// Handel von Hand: Oben wählt man Betrieb und Karren, klickt eine Ware an und setzt sie mit Menge auf den
/// Auftrag (verkaufen aus dem Lager / kaufen ins Lager). Der Karren fährt los, handelt bei Ankunft und bringt die
/// Einkäufe heim – die Regeln prüft der Kern (<see cref="Stadtwirtschaft.HandelsauftragVerboten"/>).
/// </summary>
public partial class MarktAnzeige : PanelContainer
{
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.72f, 0.72f, 0.72f);
    private static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);
    private static readonly Color Hell = new(0.92f, 0.88f, 0.80f);
    private const int Spalten = 12;
    private const int Icon = 64;
    private const int Kachelbreite = 86;

    /// <summary>Ab dieser Abweichung vom Grundpreis wird der Preis eingefärbt.</summary>
    private const double Spanne = 0.03;

    private Simulation? _simulation;
    private Konto? _spieler;
    private VBoxContainer _inhalt = null!;
    private readonly List<(string Ware, Label Preis, Label Bestand, Control Kachel)> _kacheln = new();
    private double _seitAktualisierung;

    // Handel von Hand
    private readonly List<Betrieb> _handelsbetriebe = new();
    private OptionButton? _betriebWahl;
    private OptionButton? _karrenWahl;
    private Label? _handelInfo, _wareText, _auftragText, _meldung;
    private Zahlfeld? _menge;
    private Button? _verkaufKnopf, _kaufKnopf, _losKnopf, _leerenKnopf, _verwerfenKnopf;
    private CheckBox? _halten;
    private string? _ware;
    private readonly Dictionary<string, int> _verkauf = new();
    private readonly Dictionary<string, int> _kauf = new();
    private Betrieb? _vorwahl;

    /// <summary>Ein Handelsauftrag wurde abgeschickt (für das Protokoll).</summary>
    public event Action<string>? Gemeldet;

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.10f, 0.08f, 1f),
            BorderColor = new Color(0.62f, 0.50f, 0.32f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
        });
        var rand = new MarginContainer();
        foreach (var seite in new[] { "left", "right", "top", "bottom" })
            rand.AddThemeConstantOverride($"margin_{seite}", 20);
        AddChild(rand);
        var spalte = new VBoxContainer();
        spalte.AddThemeConstantOverride("separation", 8);
        rand.AddChild(spalte);
        _inhalt = new VBoxContainer();
        _inhalt.AddThemeConstantOverride("separation", 6);
        spalte.AddChild(_inhalt);
        var schliessen = Hauptmenue.Knopf(spalte, "Schließen", Hide);
        schliessen.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        Rollbereich.Einhuellen(this);
        Fensterrahmen.Anmelden(this, "Markt", festhaltbar: true);
        Hide();
    }

    public void Verbinden(Simulation simulation, Konto spieler)
    {
        _simulation = simulation;
        _spieler = spieler;
    }

    public void Oeffnen() => Oeffnen(null);

    /// <summary>Öffnet den Markt – mit diesem Betrieb für den Handelsauftrag vorgewählt.</summary>
    public void Oeffnen(Betrieb? betrieb)
    {
        _vorwahl = betrieb;
        Aufbauen();
        Show();
    }

    /// <summary>Warengruppe für die Anzeige (aus „typ" in den Daten).</summary>
    private static string Gruppe(WareDaten d) => d.Typ switch
    {
        "Rohstoff" => "Rohstoffe",
        "Zwischenprodukt" => "Zwischenwaren",
        "Fertigprodukt" or "Heilmittel" => "Fertigwaren",
        _ => "Sonstiges",
    };

    /// <summary>Wer stellt die Ware her? Beruf und niedrigste Stufe (Rohstoffe: der Umlandbetrieb) – auch zum Sortieren.</summary>
    private static (int Beruf, int Stufe, string Text) Herkunft(Wirtschaftsdaten d, string ware)
    {
        var berufe = d.Berufe.Keys.ToList();
        var art = d.Betriebsarten.Values
            .Where(a => a.Produkte.Contains(ware) || a.Rohstoffe.Contains(ware))
            .OrderBy(a => a.Stufe)
            .FirstOrDefault();
        if (art is null) return (berufe.Count, 0, "nur über Händler zu haben");
        var index = berufe.IndexOf(art.Beruf);
        return (index < 0 ? berufe.Count : index, art.Stufe,
            art.IstWerkstatt ? $"{art.Beruf}, ab Stufe {art.Stufe} ({art.Name})" : $"{art.Beruf}: {art.Name}");
    }

    /// <summary>Baut die Kacheln neu auf (beim Öffnen) – Preise ändert danach <see cref="_Process"/>.</summary>
    private void Aufbauen()
    {
        if (_simulation?.Wirtschaft is not { } w) return;
        UiHilfe.KinderEntfernen(_inhalt);
        _kacheln.Clear();

        Hauptmenue.Text(_inhalt, "Marktplatz", 26, Gold, zentriert: true);
        Hauptmenue.Text(_inhalt, "Die Preise bilden sich aus Nachfrage und Angebot des letzten Jahres. " +
                                 "Grün ▲ über dem Grundpreis – gut zum Verkaufen; rot ▼ darunter – gut zum Einkaufen. " +
                                 "Die gelbe Zahl im Bild: so viel liegt in euren Lagern. Einzelheiten zeigt der Mauszeiger.", 12, Grau, umbruchBreite: Spalten * (Kachelbreite + 6));

        if (w.Daten.Handel is not null && _spieler is not null)
            HandelAufbauen(w);
        VorschauAufbauen(w);

        var d = w.Daten;
        var gruppen = d.Waren
            .GroupBy(x => Gruppe(x.Value))
            .OrderBy(g => Array.IndexOf(new[] { "Rohstoffe", "Zwischenwaren", "Fertigwaren", "Sonstiges" }, g.Key));
        foreach (var gruppe in gruppen)
        {
            Hauptmenue.Text(_inhalt, gruppe.Key, 16, Gold);
            var raster = new GridContainer { Columns = Spalten };
            raster.AddThemeConstantOverride("h_separation", 6);
            raster.AddThemeConstantOverride("v_separation", 6);
            _inhalt.AddChild(raster);
            foreach (var (ware, _) in gruppe.OrderBy(x => Herkunft(d, x.Key).Beruf).ThenBy(x => Herkunft(d, x.Key).Stufe).ThenBy(x => x.Value.Grundpreis))
                raster.AddChild(Kachel(ware));
        }
        Aktualisieren();
    }

    private Control Kachel(string ware)
    {
        var kachel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(Kachelbreite, 0),
            MouseFilter = MouseFilterEnum.Stop, // für den Tooltip
        };
        kachel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.22f, 0.18f, 0.14f),
            BorderColor = new Color(0.40f, 0.32f, 0.22f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 3, ContentMarginRight = 3, ContentMarginTop = 4, ContentMarginBottom = 3,
        });
        var spalte = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        spalte.AddThemeConstantOverride("separation", 0);
        kachel.AddChild(spalte);
        // Icon mit dem eigenen Lagerbestand als kleine Zahl in der Ecke (wie im Original)
        var rahmen = new Control { CustomMinimumSize = new Vector2(Icon, Icon), SizeFlagsHorizontal = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        spalte.AddChild(rahmen);
        var bild = WarenIcons.Bild(ware, Icon);
        bild.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        rahmen.AddChild(bild);
        var bestand = new Label { MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Right };
        bestand.AddThemeFontSizeOverride("font_size", 12);
        bestand.AddThemeColorOverride("font_color", Gold);
        bestand.AddThemeColorOverride("font_outline_color", new Color(0.08f, 0.06f, 0.05f));
        bestand.AddThemeConstantOverride("outline_size", 4);
        bestand.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
        bestand.GrowHorizontal = GrowDirection.Begin;
        bestand.GrowVertical = GrowDirection.Begin;
        rahmen.AddChild(bestand);
        var name = Zeile(spalte, ware, 11, Hell);
        name.ClipText = true;
        var preis = Zeile(spalte, "", 14, Hell);
        _kacheln.Add((ware, preis, bestand, kachel));
        kachel.GuiInput += ereignis =>
        {
            if (ereignis is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && _betriebWahl is not null)
                WareWaehlen(ware);
        };
        return kachel;
    }

    private static Label Zeile(Container eltern, string text, int groesse, Color farbe)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", groesse);
        label.AddThemeColorOverride("font_color", farbe);
        eltern.AddChild(label);
        return label;
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _seitAktualisierung += delta;
        if (_seitAktualisierung < 0.5) return; // Preise zweimal je Sekunde reichen
        _seitAktualisierung = 0;
        Aktualisieren();
    }

    /// <summary>Preise, Farben, eigener Bestand und Tooltips angleichen.</summary>
    private void Aktualisieren()
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null) return;
        HandelAktualisieren(w);
        VorschauAktualisieren(w);
        var vorschau = w.Befugnisse.Marktvorschau(_spieler).ToDictionary(v => v.Ware); // leer, wenn der Spieler nicht Marktmeister ist
        var d = w.Daten;
        var waehrung = d.Allgemein.Waehrung;
        var eigene = w.Betriebe.Where(b => b.Konto == _spieler).ToList();
        foreach (var (ware, preisLabel, bestandLabel, kachel) in _kacheln)
        {
            var stand = w.Markt[ware];
            var abweichung = stand.Preis / stand.Grundpreis - 1;
            var (zeichen, farbe) = abweichung > Spanne ? ("▲ ", Gruen) : abweichung < -Spanne ? ("▼ ", Rot) : ("", Hell);
            var preisText = zeichen + Zahlenformat.Kommazahl(stand.Preis);
            if (preisLabel.Text != preisText)
            {
                preisLabel.Text = preisText;
                preisLabel.AddThemeColorOverride("font_color", farbe);
            }
            var bestand = eigene.Sum(b => b.ImLager(ware));
            var bestandText = bestand > 0 ? bestand.ToString() : "";
            if (bestandLabel.Text != bestandText) bestandLabel.Text = bestandText;

            var daten = d.Waren[ware];
            var zeilen = new List<string>
            {
                $"{ware} ({Gruppe(daten) switch { "Rohstoffe" => "Rohstoff", "Zwischenwaren" => "Zwischenware", "Fertigwaren" => "Fertigware", _ => daten.Typ }})",
                $"Hergestellt: {Herkunft(d, ware).Text}",
                $"Preis {Zahlenformat.Kommazahl(stand.Preis)} {waehrung} (Grundpreis {Zahlenformat.Ganz(stand.Grundpreis)}, " +
                $"{(abweichung >= 0 ? "+" : "−")}{Zahlenformat.Ganz(Math.Abs(abweichung) * 100)} %)",
                $"Nachfrage der Stadt: {Zahlenformat.Ganz(daten.Nachfrage)} im Jahr ({Zahlenformat.Ganz(daten.NpcAnteil * 100)} % decken Händler)",
            };
            if (Warenkorb(w, ware) is { } korb) zeilen.Add(korb);
            zeilen.Add($"Von den Häusern verkauft im letzten Jahr: {Zahlenformat.Ganz(stand.SpielerAngebotImJahr)}");
            if (daten.Zutaten.Count > 0)
                zeilen.Add($"Zutaten: {string.Join(", ", daten.Zutaten.Select(z => $"{z.Value} {z.Key}"))} · " +
                           $"Gewinn je Arbeitsminute {Zahlenformat.Kommazahl(Kalkulation.GewinnJeArbeitsminute(w, ware))} {waehrung}");
            if (w.Gastwirtschaft.IstAktiv && w.Gastwirtschaft.Daten.Ausschank.Contains(ware))
                zeilen.Add($"Wird in Schänken ausgeschenkt: {Zahlenformat.Kommazahl(w.Gastwirtschaft.Ausschankpreis(ware))} {waehrung} je Gast");
            if (bestand > 0)
                zeilen.Add($"In euren Lagern: {bestand}");
            if (vorschau.TryGetValue(ware, out var naechste))
                zeilen.Add($"Marktmeister: in einer Stunde {Zahlenformat.Kommazahl(naechste.PreisNaechsteStunde)} {waehrung}, wenn bis dahin niemand verkauft");
            var tipp = string.Join("\n", zeilen);
            if (kachel.TooltipText != tipp) kachel.TooltipText = tipp;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Marktmeister (PR #200)
    // ═════════════════════════════════════════════════════════════════════════

    private Label? _vorschau;

    /// <summary>Nur für den Marktmeister: die Preise der nächsten Stunde – die Waren, deren Preis sich am meisten ändert.</summary>
    private void VorschauAufbauen(Stadtwirtschaft w)
    {
        _vorschau = null;
        if (_spieler is null || w.Befugnisse.MarktvorschauVerboten(_spieler) is not null) return;
        Hauptmenue.Text(_inhalt, $"Als Marktmeister seht ihr die Preise der nächsten Stunde (auch im Tooltip jeder Ware) und zahlt auf eigene Verkäufe am Markt nur " +
                                 $"{Zahlenformat.Ganz(w.SteuersatzMarkt(_spieler) * 100)} % Steuer.", 13, Gold, umbruchBreite: Spalten * (Kachelbreite + 6));
        _vorschau = Hauptmenue.Text(_inhalt, "", 12, Hell, umbruchBreite: Spalten * (Kachelbreite + 6));
    }

    private void VorschauAktualisieren(Stadtwirtschaft w)
    {
        if (_vorschau is null || _spieler is null) return;
        var waehrung = w.Daten.Allgemein.Waehrung;
        var groesste = w.Befugnisse.Marktvorschau(_spieler)
            .Where(v => v.PreisJetzt > 0 && Math.Abs(v.PreisNaechsteStunde / v.PreisJetzt - 1) >= 0.001)
            .OrderByDescending(v => Math.Abs(v.PreisNaechsteStunde / v.PreisJetzt - 1)).Take(8)
            .Select(v => $"{v.Ware} {Zahlenformat.Kommazahl(v.PreisJetzt)} → {Zahlenformat.Kommazahl(v.PreisNaechsteStunde)} " +
                         $"({(v.PreisNaechsteStunde >= v.PreisJetzt ? "▲" : "▼")} {Zahlenformat.Kommazahl(Math.Abs(v.PreisNaechsteStunde / v.PreisJetzt - 1) * 100)} %)")
            .ToList();
        var text = groesste.Count == 0 ? "In der nächsten Stunde bleiben die Preise gleich."
            : $"Nächste Stunde ({waehrung}): " + string.Join(" · ", groesste);
        if (_vorschau.Text != text) _vorschau.Text = text;
    }

    /// <summary>
    /// Zeile zum Warenkorb der Einwohner, z. B. „Schmiedewaren: Die Einwohner fragen 7 % mehr nach“ –
    /// null, wenn die Ware in keinem Warenkorb liegt. Gezeigt wird die Wirkung auf die Nachfrage (Faktor × Kopplung).
    /// </summary>
    private static string? Warenkorb(Stadtwirtschaft w, string ware)
    {
        var e = w.Einwohnerschaft;
        if (e.Warengruppe(ware) is not { } gruppe) return null;
        var wirkung = e.Daten.Kopplung.Markt * (e.Warenkorbfaktor(ware) - 1);
        var prozent = Zahlenformat.Ganz(Math.Abs(wirkung) * 100);
        var text = prozent == "0" ? "wie üblich" : $"{prozent} % {(wirkung > 0 ? "mehr" : "weniger")}";
        return $"{gruppe}: Die Einwohner fragen {text} nach";
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Handel von Hand
    // ═════════════════════════════════════════════════════════════════════════

    private Betrieb? GewaehlterBetrieb => _betriebWahl is { } o && o.Selected >= 0 && o.Selected < _handelsbetriebe.Count ? _handelsbetriebe[o.Selected] : null;
    private int GewaehlterKarren => Math.Max(0, _karrenWahl?.Selected ?? 0);

    private void HandelAufbauen(Stadtwirtschaft w)
    {
        _handelsbetriebe.Clear();
        _handelsbetriebe.AddRange(w.Betriebe.Where(b => b.Konto == _spieler && b.Karren.Count > 0));
        var rahmen = new PanelContainer();
        rahmen.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.18f, 0.15f, 0.11f), BorderColor = new Color(0.50f, 0.40f, 0.26f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 8, ContentMarginBottom = 8,
        });
        _inhalt.AddChild(rahmen);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        rahmen.AddChild(box);
        Hauptmenue.Text(box, "Handelsauftrag", 16, Gold);
        if (_handelsbetriebe.Count == 0)
        {
            Hauptmenue.Text(box, "Ihr habt keinen Betrieb mit Karren.", 13, Grau);
            _betriebWahl = null;
            return;
        }

        var zeile1 = new HBoxContainer();
        zeile1.AddThemeConstantOverride("separation", 8);
        box.AddChild(zeile1);
        Hauptmenue.Text(zeile1, "Betrieb:", 13, Hell);
        _betriebWahl = new OptionButton { FocusMode = FocusModeEnum.None };
        foreach (var b in _handelsbetriebe) _betriebWahl.AddItem(b.Name);
        _betriebWahl.Select(_vorwahl is not null && _handelsbetriebe.Contains(_vorwahl) ? _handelsbetriebe.IndexOf(_vorwahl) : 0);
        _betriebWahl.ItemSelected += _ => { KarrenListeFuellen(); AuftragLeeren(); };
        zeile1.AddChild(_betriebWahl);
        Hauptmenue.Text(zeile1, "Karren:", 13, Hell);
        _karrenWahl = new OptionButton { FocusMode = FocusModeEnum.None };
        zeile1.AddChild(_karrenWahl);
        _handelInfo = Hauptmenue.Text(zeile1, "", 12, Grau);
        _handelInfo.AutowrapMode = TextServer.AutowrapMode.WordSmart; // „Zuletzt: …“ kann lang werden – das Fenster soll nicht wachsen
        _handelInfo.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _handelInfo.CustomMinimumSize = new Vector2(420, 0);
        KarrenListeFuellen();

        var zeile2 = new HBoxContainer();
        zeile2.AddThemeConstantOverride("separation", 8);
        box.AddChild(zeile2);
        _wareText = Hauptmenue.Text(zeile2, "Ware anklicken, um sie auf den Auftrag zu setzen.", 13, Hell);
        _menge = new Zahlfeld { MinValue = 1, MaxValue = 75, Step = 1, Value = 10, CustomMinimumSize = new Vector2(90, 0) };
        zeile2.AddChild(_menge);
        _verkaufKnopf = Knopf(zeile2, "verkaufen", () => Setzen(_verkauf));
        _kaufKnopf = Knopf(zeile2, "kaufen", () => Setzen(_kauf));
        _halten = new CheckBox { Text = "zurückhalten", FocusMode = FocusModeEnum.None, TooltipText = "Der Karren verkauft diese Ware nicht von selbst (zum Horten und späteren Verkauf von Hand)." };
        _halten.AddThemeFontSizeOverride("font_size", 13);
        _halten.Toggled += an =>
        {
            if (GewaehlterBetrieb is { } b && _ware is { } ware) Befehlen(new WareZurueckhalten(b.Name, ware, an));
        };
        zeile2.AddChild(_halten);

        var zeile3 = new HBoxContainer();
        zeile3.AddThemeConstantOverride("separation", 8);
        box.AddChild(zeile3);
        _auftragText = Hauptmenue.Text(zeile3, "", 13, Hell);
        _auftragText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _auftragText.CustomMinimumSize = new Vector2(620, 0);
        _leerenKnopf = Knopf(zeile3, "leeren", AuftragLeeren);
        _losKnopf = Knopf(zeile3, "Karren losschicken", Losschicken);
        _verwerfenKnopf = Knopf(zeile3, "Vormerkung verwerfen", () =>
        {
            if (GewaehlterBetrieb is { } b) Befehlen(new HandelsauftragVerwerfen(b.Name, GewaehlterKarren));
        });
        _meldung = Hauptmenue.Text(box, "", 13, Grau);
        _meldung.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _meldung.CustomMinimumSize = new Vector2(Spalten * (Kachelbreite + 6) - 40, 0);
        if (_ware is not null && !w.Daten.Waren.ContainsKey(_ware)) _ware = null;
    }

    private static Button Knopf(Container eltern, string text, Action klick)
    {
        var knopf = new Button { Text = text, FocusMode = FocusModeEnum.None };
        knopf.AddThemeFontSizeOverride("font_size", 13);
        knopf.Pressed += klick;
        eltern.AddChild(knopf);
        return knopf;
    }

    private void KarrenListeFuellen()
    {
        if (_karrenWahl is null || GewaehlterBetrieb is not { } b) return;
        _karrenWahl.Clear();
        foreach (var k in b.Karren) _karrenWahl.AddItem($"{k.Name} ({k.Daten.Sorten} × {k.Daten.KapazitaetJeSorte})");
        _karrenWahl.Select(0);
    }

    private void WareWaehlen(string ware)
    {
        _ware = ware;
        foreach (var (w2, _, _, kachel) in _kacheln)
            kachel.Modulate = w2 == ware ? new Color(1.15f, 1.1f, 0.9f) : Colors.White;
        if (_simulation?.Wirtschaft is { } w) HandelAktualisieren(w);
    }

    private void Setzen(Dictionary<string, int> liste)
    {
        if (_ware is null || _menge is null) return;
        _menge.Apply();
        var menge = (int)_menge.Value;
        liste[_ware] = liste.GetValueOrDefault(_ware) + menge;
        (liste == _verkauf ? _kauf : _verkauf).Remove(_ware); // eine Ware entweder verkaufen oder kaufen
        if (_simulation?.Wirtschaft is { } w) HandelAktualisieren(w);
    }

    private void AuftragLeeren()
    {
        _verkauf.Clear();
        _kauf.Clear();
        if (_simulation?.Wirtschaft is { } w) HandelAktualisieren(w);
    }

    private void Losschicken()
    {
        if (GewaehlterBetrieb is not { } b) return;
        Befehlen(new Handelsauftrag(b.Name, GewaehlterKarren, new Dictionary<string, int>(_verkauf), new Dictionary<string, int>(_kauf)), AuftragLeeren);
    }

    /// <summary>Schickt den Befehl über den Boten; <paramref name="beiErfolg"/> läuft nach der Meldung, nur wenn er gelang.</summary>
    private void Befehlen(Befehl befehl, Action? beiErfolg = null)
    {
        if (_meldung is null) return;
        UiHilfe.Befehlen(_simulation, _spieler, befehl, this, e =>
        {
            if (_meldung is null || _simulation?.Wirtschaft is not { } w) return;
            _meldung.Text = UiHilfe.MeldungText(e);
            _meldung.AddThemeColorOverride("font_color", e.Erfolg ? Gruen : Rot);
            Gemeldet?.Invoke(_meldung.Text);
            HandelAktualisieren(w);
            if (e.Erfolg) beiErfolg?.Invoke();
        });
    }

    /// <summary>Texte, Schätzungen und Knöpfe des Handelsauftrags angleichen.</summary>
    private void HandelAktualisieren(Stadtwirtschaft w)
    {
        if (_betriebWahl is null || GewaehlterBetrieb is not { } b || _spieler is null) return;
        var waehrung = w.Daten.Allgemein.Waehrung;
        var h = w.Daten.Handel!;
        var karren = b.Karren[Math.Min(GewaehlterKarren, b.Karren.Count - 1)];
        var ankunft = w.AnkunftAmMarkt(karren);
        string Kurz(IReadOnlyDictionary<string, int> l) => string.Join(", ", l.Where(x => x.Value > 0).Select(x => $"{x.Value} {x.Key}"));
        var unterwegs = karren.Zustand != KarrenZustand.AmBetrieb;
        var info = $"{karren.ZustandText(_simulation!.Zeit)} · Markt {h.MarktVon}–{h.MarktBis} Uhr" +
                   (unterwegs ? "" : $", Ankunft {ankunft / 60:00}:{ankunft % 60:00}") +
                   (karren.Vormerkung is { } v
                       ? $"\nVorgemerkt: {(v.Verkauf.Any(x => x.Value > 0) ? "verkaufen " + Kurz(v.Verkauf) : "")}{(v.Verkauf.Any(x => x.Value > 0) && v.Kauf.Any(x => x.Value > 0) ? "; " : "")}{(v.Kauf.Any(x => x.Value > 0) ? "kaufen " + Kurz(v.Kauf) : "")}"
                       : "") +
                   (karren.LetzterHandel is { } letzt ? $"\nZuletzt: {letzt}" : "");
        if (_handelInfo!.Text != info) _handelInfo.Text = info;

        if (_ware is { } ware)
        {
            var stand = w.Markt[ware];
            var text = $"{ware}: im Lager {b.ImLager(ware)} · verkaufen ≈ {Zahlenformat.Kommazahl(stand.Preis * w.Familien.VerkaufsFaktor(_spieler))} · " +
                       $"kaufen ≈ {Zahlenformat.Kommazahl(w.HandKaufpreis(_spieler, ware, 1))} {waehrung} je Stück";
            if (_wareText!.Text != text) _wareText.Text = text;
            _halten!.SetPressedNoSignal(b.HaeltZurueck(ware));
        }
        _verkaufKnopf!.Disabled = _kaufKnopf!.Disabled = _halten!.Disabled = _ware is null;

        string Liste(Dictionary<string, int> l, Func<string, int, double> wert) =>
            string.Join(", ", l.Select(x => $"{x.Value} {x.Key} (≈ {Zahlenformat.Ganz(wert(x.Key, x.Value))})"));
        var teile = new List<string>();
        if (_verkauf.Count > 0)
            teile.Add("Verkaufen: " + Liste(_verkauf, (ware, n) => w.Markt[ware].Preis * n * w.Familien.VerkaufsFaktor(_spieler)));
        if (_kauf.Count > 0)
            teile.Add("Kaufen: " + Liste(_kauf, (ware, n) => w.HandKaufpreis(_spieler, ware, n)));
        var grund = w.HandelsauftragVerboten(b, GewaehlterKarren, _verkauf, _kauf, vormerken: unterwegs);
        var auftrag = teile.Count == 0 ? "Auftrag: noch leer." : "Auftrag – " + string.Join(" · ", teile) + $" {waehrung}" + (grund is null ? "" : $"\n✗ {grund}");
        if (_auftragText!.Text != auftrag) _auftragText.Text = auftrag;
        _losKnopf!.Text = unterwegs ? "vormerken" : "Karren losschicken";
        _losKnopf.Disabled = grund is not null;
        _losKnopf.TooltipText = grund ?? (unterwegs
            ? "Der Karren ist unterwegs – der Auftrag startet, sobald er zurück ist (vor dem Verwalter)."
            : "Der Karren lädt die Verkaufsware, fährt zum Markt, handelt und bringt die Einkäufe heim.");
        _verwerfenKnopf!.Visible = karren.Vormerkung is not null;
        _leerenKnopf!.Disabled = teile.Count == 0;
    }
}
