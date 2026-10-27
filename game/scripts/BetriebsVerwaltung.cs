using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das Bedienfeld für einen eigenen Betrieb: Produkt, Förderung, Personal, Karren und Eskorten.
///
/// Wichtig: Dieses Feld ändert NICHTS direkt. Jeder Knopf erzeugt einen Befehl und schickt ihn an
/// <see cref="Stadtwirtschaft.Ausfuehren"/>. Die Antwort (ausgeführt oder abgelehnt mit Grund)
/// geht über <see cref="Gemeldet"/> ins Protokoll. Genau so wird es später im Multiplayer laufen.
///
/// Die Zahlen neben den Auswahlen (Gulden je Arbeitsminute, Jahresertrag, Fahrtkosten) kommen
/// aus <see cref="Kalkulation"/> – dieselben Rechnungen, nach denen die KI-Rivalen entscheiden.
///
/// Aufbau: Die Bedienelemente werden im Code erzeugt. Zeilen, deren Zahl sich ändern kann
/// (Personal, Karren), werden neu gebaut, sobald sich ihr „Bauplan" ändert – sonst nur beschriftet.
/// </summary>
public partial class BetriebsVerwaltung : VBoxContainer
{
    private static readonly Color Grau = new(0.72f, 0.72f, 0.72f);

    private Simulation? _simulation;
    private Konto? _spieler;

    /// <summary>Nur der Abschnitt „Karren“ (Kaufen, Zustand, Handel): so steht er als Tafel am Hoftor im Innenraum (<see cref="KarrenAnzeige"/>).</summary>
    public bool NurKarren { get; init; }

    /// <summary>Nur der Abschnitt „Geldleihe“ (Kasse, Rahmen, Zins für Häuser, offene Kredite): so steht er als Tafel am Hauptbuchpult im Innenraum (<see cref="BankAnzeige"/>).</summary>
    public bool NurBank { get; init; }

    /// <summary>
    /// Kurzform unter „…“ in der Kurzübersicht (<see cref="BetriebsUebersicht"/>, PR #197): ohne Überschrift und ohne Ware (die steht schon darüber).
    /// </summary>
    public bool Kurzform { get; init; }

    /// <summary>
    /// In der Kurzform: Hat der Betrieb eine Innenansicht, stehen Personal, Karren und Bankgeschäft nur noch drinnen (Tafeln am Personalbuch,
    /// Hoftor, Hauptbuchpult) und hier nicht. Ohne Innenansicht (Umland) bleiben sie hier.
    /// </summary>
    public bool MitInnenansicht { get; set; }

    /// <summary>Wessen Betriebe dieses Feld bedienen darf.</summary>
    public Konto? Spieler => _spieler;
    private Betrieb? _betrieb;
    private string _bauplan = "";

    // Feste Elemente
    private Label _ueberschrift = null!;
    private HBoxContainer _produktZeile = null!;
    private OptionButton _produktWahl = null!;
    private readonly List<string> _produkte = new();
    private HBoxContainer _foerderZeile = null!;
    private HSlider _silberRegler = null!;
    private Label _silberText = null!;
    private Label _personalKopf = null!;
    private VBoxContainer _gaststube = null!;
    private Label _gaesteText = null!;
    private Button _bardeKnopf = null!;
    private CheckBox _wuerfeltisch = null!;
    private Label _diebesText = null!;
    private Label _wacheText = null!;
    private VBoxContainer _bankstube = null!;
    private Label _bankText = null!;
    private Zahlfeld _kassenBetrag = null!;
    private Button _kasseFuellen = null!;
    private Zahlfeld _zinsWahl = null!;
    private Button _zinsKnopf = null!;
    private Betrieb? _zinsFuer;
    private VBoxContainer _kirchenstube = null!;
    private Label _kirchenText = null!;
    private VBoxContainer _beichtKnoepfe = null!;
    private Button _bekehrenKnopf = null!;
    private string _beichtPlan = "";
    private HBoxContainer _ausbauZeile = null!;
    private Label _ausbauText = null!;
    private Button _ausbauKnopf = null!;
    private Label _foerderName = null!;
    private VBoxContainer _personal = null!;
    private VBoxContainer _karren = null!;
    private Label _karrenKopf = null!;
    private HBoxContainer _karrenKauf = null!;
    private OptionButton _karrenTyp = null!;
    private readonly List<string> _karrenTypen = new();
    private Button _kaufenKnopf = null!;

    // Zeilen, die beim Neubau ersetzt werden
    private readonly List<(string Kraft, Label Anzahl, Label Info, Button Minus, Button Plus)> _personalZeilen = new();
    private readonly List<(OptionButton? Eskorte, CheckBox? Nachts, Label Info, Button Verkaufen)> _karrenZeilen = new();

    /// <summary>Ergebnis eines Befehls (Erfolg oder Ablehnung) – für das Protokoll.</summary>
    public event Action<string>? Gemeldet;

    /// <summary>Der Spieler will mit diesem Betrieb am Markt handeln (öffnet das Marktfenster).</summary>
    public event Action<Betrieb>? HandelGewuenscht;
    private Button _handelKnopf = null!;

    /// <summary>Der Spieler will den Betrieb verkaufen bzw. die Pacht kündigen (öffnet die Rückfrage).</summary>
    public event Action<Betrieb>? VerkaufGewuenscht;
    private Button _verkaufKnopf = null!;
    private Label _verkaufText = null!;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 6);
        if (!Kurzform) AddChild(new HSeparator());
        _ueberschrift = NeuesLabel(this, 15);
        _ueberschrift.Text = "⚒ Verwalten";
        _ueberschrift.Visible = !Kurzform;

        // Produkt (nur Gießerei)
        _produktZeile = Zeile(this);
        NeuesLabel(_produktZeile, 14).Text = "Produkt:";
        _produktWahl = new OptionButton { FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _produktWahl.ItemSelected += index => Befehlen(new ProduktWaehlen(_betrieb!.Name, _produkte[(int)index]));
        _produktZeile.AddChild(_produktWahl);

        // Gebäudestufe und Ausbau (nur Werkstätten, wenn es Meistergrade gibt)
        _ausbauZeile = Zeile(this);
        _ausbauText = NeuesLabel(_ausbauZeile, 13, Grau);
        _ausbauText.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _ausbauText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _ausbauText.CustomMinimumSize = new Vector2(220, 0);
        _ausbauText.MouseFilter = MouseFilterEnum.Stop; // für den Tooltip
        _ausbauKnopf = new Button { FocusMode = FocusModeEnum.None };
        _ausbauKnopf.AddThemeFontSizeOverride("font_size", 13);
        _ausbauKnopf.Pressed += () => Befehlen(new BetriebAusbauen(_betrieb!.Name));
        _ausbauZeile.AddChild(_ausbauKnopf);

        // Gaststube (nur Schänken des Wirts)
        _gaststube = new VBoxContainer();
        _gaststube.AddThemeConstantOverride("separation", 4);
        AddChild(_gaststube);
        _gaesteText = NeuesLabel(_gaststube, 13, Grau);
        _gaesteText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _gaesteText.CustomMinimumSize = new Vector2(380, 0);
        var gastZeile = Zeile(_gaststube);

        // Versteck des Diebes: Taschendiebe, Fänge, was die Stufe erlaubt
        _diebesText = NeuesLabel(this, 13, Grau);
        _diebesText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _diebesText.CustomMinimumSize = new Vector2(380, 0);

        // Wache des Gardisten: Sold, Patrouille, Kopfgeld, Geleit, Razzien
        _wacheText = NeuesLabel(this, 13, Grau);
        _wacheText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _wacheText.CustomMinimumSize = new Vector2(380, 0);

        // Geldleihe: Kasse, Rahmen, Stadtzinsen, Wechsel, Zins für Häuser, offene Kredite
        _bankstube = new VBoxContainer();
        _bankstube.AddThemeConstantOverride("separation", 4);
        AddChild(_bankstube);
        _bankText = NeuesLabel(_bankstube, 13, Grau);
        _bankText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _bankText.CustomMinimumSize = new Vector2(380, 0);
        var kassenZeile = Zeile(_bankstube);
        NeuesLabel(kassenZeile, 13).Text = "Kasse:";
        _kassenBetrag = new Zahlfeld { MinValue = 100, MaxValue = 1_000_000, Step = 100, Value = 1_000, CustomMinimumSize = new Vector2(120, 0) };
        kassenZeile.AddChild(_kassenBetrag);
        var einlegen = new Button { Text = "einlegen", FocusMode = FocusModeEnum.None };
        einlegen.AddThemeFontSizeOverride("font_size", 13);
        einlegen.Pressed += () => Befehlen(new KasseEinlegen(_betrieb!.Name, _kassenBetrag.Value));
        kassenZeile.AddChild(einlegen);
        var abheben = new Button { Text = "abheben", FocusMode = FocusModeEnum.None };
        abheben.AddThemeFontSizeOverride("font_size", 13);
        abheben.Pressed += () => Befehlen(new KasseAbheben(_betrieb!.Name, _kassenBetrag.Value));
        kassenZeile.AddChild(abheben);
        _kasseFuellen = new Button { FocusMode = FocusModeEnum.None };
        _kasseFuellen.AddThemeFontSizeOverride("font_size", 13);
        _kasseFuellen.Pressed += () =>
        {
            if (_simulation?.Wirtschaft is not { } w || _betrieb is null) return;
            var fehlt = Math.Floor(Math.Min(w.Bank.RahmenVoll(_betrieb) - w.Bank.Kasse(_betrieb), _betrieb.Konto.Vermoegen));
            if (fehlt > 0) Befehlen(new KasseEinlegen(_betrieb.Name, fehlt));
        };
        kassenZeile.AddChild(_kasseFuellen);
        var zinsZeile = Zeile(_bankstube);
        NeuesLabel(zinsZeile, 13).Text = "Zins für Häuser:";
        _zinsWahl = new Zahlfeld { Step = 1, Suffix = "%", CustomMinimumSize = new Vector2(90, 0) };
        zinsZeile.AddChild(_zinsWahl);
        _zinsKnopf = new Button { Text = "festsetzen", FocusMode = FocusModeEnum.None };
        _zinsKnopf.AddThemeFontSizeOverride("font_size", 13);
        _zinsKnopf.Pressed += () => Befehlen(new ZinsSetzen(_betrieb!.Name, _zinsWahl.Value / 100));
        zinsZeile.AddChild(_zinsKnopf);

        // Kirche (nur Kirchen des Predigers): Messe, Kollekte, Ablass, Beichtwissen
        _kirchenstube = new VBoxContainer();
        _kirchenstube.AddThemeConstantOverride("separation", 4);
        AddChild(_kirchenstube);
        _kirchenText = NeuesLabel(_kirchenstube, 13, Grau);
        _kirchenText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _kirchenText.CustomMinimumSize = new Vector2(380, 0);
        _bekehrenKnopf = new Button { FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        _bekehrenKnopf.AddThemeFontSizeOverride("font_size", 13);
        _bekehrenKnopf.Pressed += () => Befehlen(new Bekehrungspredigt());
        _kirchenstube.AddChild(_bekehrenKnopf);
        _beichtKnoepfe = new VBoxContainer();
        _kirchenstube.AddChild(_beichtKnoepfe);
        _bardeKnopf = new Button { FocusMode = FocusModeEnum.None };
        _bardeKnopf.AddThemeFontSizeOverride("font_size", 13);
        _bardeKnopf.Pressed += () => Befehlen(new BardeEinladen(_betrieb!.Name));
        gastZeile.AddChild(_bardeKnopf);
        _wuerfeltisch = new CheckBox { Text = "Würfeltisch", FocusMode = FocusModeEnum.None };
        _wuerfeltisch.AddThemeFontSizeOverride("font_size", 13);
        _wuerfeltisch.Toggled += an => Befehlen(new WuerfeltischAufstellen(_betrieb!.Name, an));
        gastZeile.AddChild(_wuerfeltisch);

        // Förderung (nur Mine)
        _foerderZeile = Zeile(this);
        _foerderName = NeuesLabel(_foerderZeile, 14);
        _foerderName.Text = "Silber:";
        _silberRegler = new HSlider { MinValue = 0, MaxValue = 100, Step = 5, FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _silberRegler.DragEnded += geaendert => { if (geaendert) Befehlen(new FoerderungAufteilen(_betrieb!.Name, _silberRegler.Value / 100)); };
        _foerderZeile.AddChild(_silberRegler);
        _silberText = NeuesLabel(_foerderZeile, 13, Grau);
        _silberText.CustomMinimumSize = new Vector2(120, 0);

        // Personal
        _personalKopf = NeuesLabel(this, 14);
        _personal = new VBoxContainer();
        AddChild(_personal);

        // Karren
        _karrenKopf = NeuesLabel(this, 14);
        _karrenKopf.Text = "Karren:";
        _karrenKopf.TooltipText = "Die Karren des Betriebs bringen Waren zum Markt und Zutaten zurück. Eskorte, Nachtfahrt und Zustand stehen je Karren.";
        _karren = new VBoxContainer();
        AddChild(_karren);
        var kauf = Zeile(this);
        _karrenKauf = kauf;
        _karrenTyp = new OptionButton { FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        kauf.AddChild(_karrenTyp);
        _kaufenKnopf = new Button { Text = "Kaufen", FocusMode = FocusModeEnum.None };
        _kaufenKnopf.Pressed += () => Befehlen(new KarrenKaufen(_betrieb!.Name, _karrenTypen[_karrenTyp.Selected]));
        kauf.AddChild(_kaufenKnopf);
        _handelKnopf = new Button { Text = "Handel am Markt …", FocusMode = FocusModeEnum.None,
                                    TooltipText = "Einen Karren mit einem Auftrag zum Markt schicken: verkaufen, einkaufen, Waren zurückhalten." };
        _handelKnopf.Pressed += () => { if (_betrieb is not null) HandelGewuenscht?.Invoke(_betrieb); };
        kauf.AddChild(_handelKnopf);

        // Verkaufen bzw. Pacht kündigen – erst nach einer Rückfrage (VerkaufAnzeige). Gesperrt mit Grund aus dem Kern.
        var verkauf = Zeile(this);
        _verkaufKnopf = new Button { FocusMode = FocusModeEnum.None };
        _verkaufKnopf.AddThemeFontSizeOverride("font_size", 13);
        _verkaufKnopf.Pressed += () => { if (_betrieb is not null) VerkaufGewuenscht?.Invoke(_betrieb); };
        verkauf.AddChild(_verkaufKnopf);
        _verkaufText = NeuesLabel(verkauf, 12, Grau);
        _verkaufText.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _verkaufText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _verkaufText.CustomMinimumSize = new Vector2(220, 0);

        if (NurKarren)
            foreach (var kind in GetChildren().OfType<Control>().Where(k => k != _karrenKopf && k != _karren && k != _karrenKauf))
                kind.Visible = false;
        if (NurBank)
            foreach (var kind in GetChildren().OfType<Control>().Where(k => k != _bankstube))
                kind.Visible = false;
        Visible = false;
    }

    public void Verbinden(Simulation simulation, Konto spieler)
    {
        _simulation = simulation;
        _spieler = spieler;
        var w = simulation.Wirtschaft!;
        foreach (var (typ, daten) in w.Daten.Logistik.Karren.OrderBy(k => k.Value.Preis))
        {
            _karrenTypen.Add(typ);
            _karrenTyp.AddItem($"{typ}: {daten.Sorten} × {daten.KapazitaetJeSorte} Stück, {Zahlenformat.Ganz(daten.Preis)} {w.Daten.Allgemein.Waehrung}");
        }
    }

    /// <summary>Die Auswahlliste zeigt genau die Produkte dieser Werkstatt (Gießerei und Tischlerei haben verschiedene).</summary>
    private void ProdukteAngleichen(Betrieb b)
    {
        if (_produkte.SequenceEqual(b.Artdaten.Produkte)) return;
        _produkte.Clear();
        _produktWahl.Clear();
        foreach (var ware in b.Artdaten.Produkte)
        {
            _produkte.Add(ware);
            _produktWahl.AddItem(ware);
        }
    }

    /// <summary>Zeigt das Feld für diesen Betrieb – aber nur, wenn er dem Spieler gehört.</summary>
    public void Zeigen(Betrieb? betrieb)
    {
        var neu = betrieb is not null && betrieb.Konto == _spieler ? betrieb : null;
        if (neu == _betrieb && Visible == (neu is not null)) return; // nichts geändert
        _betrieb = neu;
        Visible = _betrieb is not null;
        _bauplan = ""; // erzwingt Neubau der Zeilen
    }

    private void Befehlen(Befehl befehl)
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null) return;
        var ergebnis = w.Ausfuehren(_spieler, befehl);
        Gemeldet?.Invoke(ergebnis.Erfolg ? ergebnis.Meldung : "✗ " + ergebnis.Meldung);
        Aktualisieren();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Anzeige
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Jedes Bild: Zahlen und Auswahlen an den Zustand der Simulation angleichen.</summary>
    public void Aktualisieren()
    {
        if (!Visible || _betrieb is not { } b || _simulation?.Wirtschaft is not { } w) return;
        var waehrung = w.Daten.Allgemein.Waehrung;

        var bauplan = $"{w.Statut}|{string.Join(",", b.Karren.Select(k => k.Name))}|{string.Join(",", b.Belegung.Keys.OrderBy(k => k))}";
        if (bauplan != _bauplan)
        {
            ZeilenNeuBauen(b, w);
            _bauplan = bauplan;
        }

        if (NurKarren)
        {
            KarrenAktualisieren(b, w);
            return;
        }
        if (NurBank)
        {
            BankstubeAktualisieren(b, w);
            return;
        }

        // Produkt: Auswahl angleichen (Select löst KEIN ItemSelected aus) und Gewinn je Minute zeigen
        _produktZeile.Visible = b.IstWerkstatt;
        if (_produktZeile.Visible)
        {
            ProdukteAngleichen(b);
            for (var i = 0; i < _produkte.Count; i++)
                _produktWahl.SetItemText(i, $"{_produkte[i]} · {Zahlenformat.Kommazahl(Kalkulation.GewinnJeArbeitsminute(w, _produkte[i]))} {waehrung}/Min.");
            _produktWahl.Select(_produkte.IndexOf(b.Produkt));
        }

        AusbauAktualisieren(b, w);
        GaststubeAktualisieren(b, w);
        KirchenstubeAktualisieren(b, w);
        DiebesstubeAktualisieren(b, w);
        WachstubeAktualisieren(b, w);
        BankstubeAktualisieren(b, w);

        _foerderZeile.Visible = b.Rohstoffe.Count >= 2; // nur, wer zwei Rohstoffe fördert, teilt auf (Grube: Eisen/Silber)
        if (_foerderZeile.Visible)
        {
            if (!_silberRegler.HasFocus()) _silberRegler.SetValueNoSignal(b.Silberanteil * 100);
            _foerderName.Text = $"{b.Rohstoffe[1]}:";
            _silberText.Text = $"{Zahlenformat.Ganz(b.Silberanteil * 100)} % {b.Rohstoffe[1]}";
            _silberText.TooltipText = $"So viel der Arbeit geht in die Förderung von {b.Rohstoffe[1]}, der Rest in {b.Rohstoffe[0]}. Der Regler daneben verschiebt den Anteil.";
        }

        // Personal
        _personalKopf.Text = $"Personal ({b.Belegt}/{b.Plaetze} Plätze, Statut {w.Statut.Kuerzel()}):";
        _personalKopf.TooltipText = $"Der Betrieb hat {b.Plaetze} Arbeitsplätze, {b.Belegt} sind besetzt. Das Statut der Stadt ({w.Statut.Kuerzel()}) bestimmt, welche Arbeitskräfte erlaubt sind. Lohnkosten: {Zahlenformat.Ganz(b.Lohnkosten)} {w.Daten.Allgemein.Waehrung} im Jahr.";
        foreach (var (kraft, anzahl, info, minus, plus) in _personalZeilen)
        {
            var daten = w.Daten.Arbeitskraefte[kraft];
            var jetzt = b.Belegung.GetValueOrDefault(kraft);
            anzahl.Text = jetzt.ToString();
            minus.Disabled = jetzt == 0;
            plus.Disabled = b.Belegt >= b.Plaetze || !daten.IstErlaubt(w.Statut, b.Ort);
            info.Text = $"Lohn {Zahlenformat.Ganz(w.Daten.Lohn(kraft, b.Art))} · Ertrag ≈ {Zahlenformat.MitVorzeichen(Kalkulation.Jahresertrag(w, b, kraft))}/Jahr"
                        + (daten.Risiko > 0 ? $" · Risiko {Zahlenformat.Ganz(daten.Risiko * 100)} %" : "");
        }

        KarrenAktualisieren(b, w);
        VerkaufAktualisieren(b, w);
        if (!Kurzform) return;
        // Kurzform: die Ware steht darüber, was die Innenansicht hat, steht nur drinnen
        _produktZeile.Visible = false;
        _personalKopf.Visible = _personal.Visible = !MitInnenansicht; // Karren und Geldleihe stellen sich oben jedes Bild selbst
        if (!MitInnenansicht) return;
        _karrenKopf.Visible = _karren.Visible = _karrenKauf.Visible = false;
        _bankstube.Visible = false;
    }

    /// <summary>Der Abschnitt „Karren“: Zeilen beschriften, Eskorte und Nachtfahrt angleichen, Kaufen und Handel sperren.</summary>
    private void KarrenAktualisieren(Betrieb b, Stadtwirtschaft w)
    {
        var waehrung = w.Daten.Allgemein.Waehrung;
        // Karren – Wache und Geldleihe haben nichts zu fahren (der Dieb schon: Beute aus Einbrüchen).
        var ohneKarren = w.Wache.IstWachbetrieb(b) || w.Bank.IstBankbetrieb(b);
        _karrenKopf.Visible = _karren.Visible = _karrenKauf.Visible = !ohneKarren || b.Karren.Count > 0;
        for (var i = 0; i < _karrenZeilen.Count && i < b.Karren.Count; i++)
        {
            var (eskorte, nachts, info, verkaufen) = _karrenZeilen[i];
            var karren = b.Karren[i];
            if (eskorte is not null && nachts is not null)
            {
                var namen = w.Daten.Logistik.Eskorten.Keys.ToList();
                var wert = Kalkulation.Ladungswert(w, karren);
                for (var e = 0; e < namen.Count; e++)
                {
                    // Die Stadtwache gibt Geleit nur, solange ein Haus ein Wachhaus mit Leuten führt.
                    var frei = w.Wache.EskorteVerfuegbar(namen[e]);
                    eskorte.SetItemDisabled(e, !frei);
                    if (frei)
                    {
                        var preis = w.Daten.Logistik.Eskorten[namen[e]].KostenJeFahrt;
                        var gesamt = Kalkulation.ErwarteteFahrtkosten(w, namen[e], wert);
                        var verlust = gesamt > preis ? gesamt - preis : 0;
                        eskorte.SetItemText(e, $"{namen[e]} · ≈ {Zahlenformat.Ganz(gesamt)} {waehrung}/Fahrt");
                        eskorte.SetItemTooltip(e, $"Preis der Eskorte {Zahlenformat.Ganz(preis)} + erwarteter Verlust durch Überfälle {Zahlenformat.Ganz(verlust)} = ≈ {Zahlenformat.Ganz(gesamt)} {waehrung} je beladener Fahrt. Die kleinste Zahl ist die günstigste Wahl.");
                    }
                    else
                    {
                        eskorte.SetItemText(e, $"{namen[e]} · nur mit einem Wachhaus in der Stadt");
                        eskorte.SetItemTooltip(e, "Die Stadtwache gibt Geleit nur, solange ein Haus ein Wachhaus mit Leuten führt.");
                    }
                }
                eskorte.Select(namen.IndexOf(karren.Eskorte));
                var gewaehlt = namen.IndexOf(karren.Eskorte);
                if (gewaehlt >= 0) eskorte.TooltipText = eskorte.GetItemTooltip(gewaehlt);
                nachts.SetPressedNoSignal(karren.NachtsFahren);
            }
            info.Text = karren.ZustandText(_simulation.Zeit);
            VerkaufenAngleichen(verkaufen, b, i, w);
        }
        _kaufenKnopf.Disabled = b.Konto.Vermoegen < w.Daten.Logistik.Karren[_karrenTypen[Math.Max(0, _karrenTyp.Selected)]].Preis;
        _handelKnopf.Visible = w.Daten.Handel is not null && b.Karren.Count > 0;
    }

    /// <summary>Der Knopf „Verkaufen (… Gulden)“ eines Karrens: gesperrt, solange der Kern einen Grund nennt (unterwegs).</summary>
    private void VerkaufenAngleichen(Button knopf, Betrieb b, int nummer, Stadtwirtschaft w)
    {
        var karren = b.Karren[nummer];
        var waehrung = w.Daten.Allgemein.Waehrung;
        var erloes = Zahlenformat.Ganz(w.KarrenErloes(karren));
        var text = $"Verkaufen ({erloes} {waehrung})";
        if (knopf.Text != text) knopf.Text = text;
        var grund = w.KarrenVerkaufVerboten(_spieler!, b, nummer);
        knopf.Disabled = grund is not null;
        var tipp = grund ?? $"{karren.Name} verkaufen: {Zahlenformat.Ganz(w.Daten.Logistik.KarrenVerkaufAnteil * 100)} % des Kaufpreises " +
                            $"({Zahlenformat.Ganz(karren.Daten.Preis)} {waehrung}), also {erloes} {waehrung}. Geht nur, solange er am Betrieb steht." +
                            (karren.Vormerkung is not null ? " Der vorgemerkte Handelsauftrag verfällt." : "");
        if (knopf.TooltipText != tipp) knopf.TooltipText = tipp;
    }

    /// <summary>„Verkaufen …“ bzw. „Pacht kündigen …“ – gesperrt, solange der Kern einen Grund nennt (letzter Betrieb, Umbau, Karren unterwegs).</summary>
    private void VerkaufAktualisieren(Betrieb b, Stadtwirtschaft w)
    {
        var pacht = w.Betriebswert(b) <= 0;
        var knopf = pacht ? "Pacht kündigen …" : "Verkaufen …";
        if (_verkaufKnopf.Text != knopf) _verkaufKnopf.Text = knopf;
        var grund = w.VerkaufVerboten(_spieler!, b);
        _verkaufKnopf.Disabled = grund is not null;
        var tipp = grund ?? (pacht
            ? "Die Pacht kündigen: Personal und Karren gehen mit, die Lagerware wird mitverkauft. Vorher kommt eine Rückfrage."
            : $"Das Haus bringt {Zahlenformat.Ganz(w.Daten.Verkauf.Anteil * 100)} % seines Werts, Personal, Wächter und Karren gehen mit, " +
              "die Lagerware wird mitverkauft. Vorher kommt eine Rückfrage mit allen Zahlen.");
        if (_verkaufKnopf.TooltipText != tipp) _verkaufKnopf.TooltipText = tipp;
        var text = grund ?? "";
        if (_verkaufText.Text != text) _verkaufText.Text = text;
    }

    /// <summary>Gästebuch, Ausschank-Vorrat, Barde, Würfeltisch, Stammtisch und dunkle Ecke einer Schänke.</summary>
    private void GaststubeAktualisieren(Betrieb b, Stadtwirtschaft w)
    {
        var g = w.Gastwirtschaft;
        _gaststube.Visible = b.IstSchaenke && g.IstAktiv;
        if (!_gaststube.Visible) return;
        var d = g.Daten;
        var waehrung = w.Daten.Allgemein.Waehrung;
        var jetzt = g.DiesesJahr(b);
        string Buch(Gaestebuch x) => $"{x.Bewirtet} bewirtet" + (x.Abgewiesen > 0 ? $", {x.Abgewiesen} gingen leer aus" : "")
                                     + $" · {Zahlenformat.Ganz(x.Einnahmen)} {waehrung}" + (x.Wuerfelgeld > 0 ? $" + {Zahlenformat.Ganz(x.Wuerfelgeld)} am Würfeltisch" : "");
        var ausschank = d.Ausschank.Where(b.Artdaten.Produkte.Contains)
            .Select(x => $"{x} {b.ImLager(x)} ({Zahlenformat.Ganz(g.Ausschankpreis(x))} {waehrung})");
        var zeilen = new List<string>
        {
            $"Gaststube – Gäste ab {d.OeffnetUm} Uhr, abends mehr. Dieses Jahr: {Buch(jetzt)}",
        };
        if (g.Vorjahr(b) is { } vorjahr) zeilen.Add($"Vorjahr: {Buch(vorjahr)}");
        zeilen.Add($"Ausschank (je {d.Vorrat} bleiben für die Gäste im Lager): {string.Join(" · ", ausschank)}");
        zeilen.Add($"Erwartet ≈ {Zahlenformat.Ganz(g.ErwarteteGaeste(b))} Gäste im Jahr bei der heutigen Konkurrenz.");
        zeilen.Add(g.HatStammtisch(b)
            ? $"Stammtisch: Am Jahresende +{Zahlenformat.Ganz(d.Stammtisch.Gunst)} Gunst bei jedem Ratsherrn (mit Barde ×{Zahlenformat.Ganz(d.Barde.Stammtisch)}) – ohne Bestechung."
            : $"Stammtisch ab Stufe {d.Stammtisch.AbStufe} (Gasthaus).");
        zeilen.Add($"Dunkle Ecke: Am Gerichtstag hört ihr Gerüchte über andere Häuser ({Zahlenformat.Ganz(d.DunkleEcke.Chance * 100)} % je Tat).");
        var text = string.Join("\n", zeilen);
        if (_gaesteText.Text != text) _gaesteText.Text = text;
        _gaesteText.TooltipText = "Die Gastwirtschaft: Gäste kommen nach Lage und Konkurrenz und trinken, was der Ausschank bietet; Buchführung des laufenden Jahres und des Vorjahrs.";

        var gebucht = g.BardeGebucht(b);
        var knopf = gebucht ? "Barde spielt heute Abend ♪" : $"Barde einladen ({Zahlenformat.Ganz(d.Barde.Kosten)} {waehrung})";
        if (_bardeKnopf.Text != knopf) _bardeKnopf.Text = knopf;
        _bardeKnopf.Disabled = gebucht || b.Konto.Vermoegen < d.Barde.Kosten;
        var tipp = $"Ab {d.AbendAb} Uhr {Zahlenformat.Ganz(d.Barde.Gaeste * 100)} % mehr Gäste, jeder zahlt {Zahlenformat.Ganz(d.Barde.Zeche * 100)} % mehr; " +
                   $"der Stammtisch bringt ×{Zahlenformat.Ganz(d.Barde.Stammtisch)} Gunst. Einmal je Jahr.\n" +
                   $"Erwarteter Mehrerlös ≈ {Zahlenformat.Ganz(g.BardeNutzen(b))} {waehrung} (wenn genug im Lager ist).";
        if (_bardeKnopf.TooltipText != tipp) _bardeKnopf.TooltipText = tipp;

        _wuerfeltisch.SetPressedNoSignal(g.HatWuerfeltisch(b));
        _wuerfeltisch.Disabled = b.Stufe < d.Wuerfeltisch.AbStufe;
        _wuerfeltisch.TooltipText = b.Stufe < d.Wuerfeltisch.AbStufe
            ? $"Einen Würfeltisch gibt es erst ab Stufe {d.Wuerfeltisch.AbStufe} (Wirtshaus)."
            : $"{Zahlenformat.Ganz(d.Wuerfeltisch.EinsatzJeGast)} {waehrung} je Gast fürs Haus – aber {Zahlenformat.Ganz(d.Wuerfeltisch.Ansehen)} Ansehen weniger, solange er steht.";
    }

    /// <summary>Sold, Patrouille (Stadt) bzw. Schutz der Landstraße (Wachturm), Kopfgeld, Geleitgeld und Razzien.</summary>
    private void WachstubeAktualisieren(Betrieb b, Stadtwirtschaft w)
    {
        _wacheText.Visible = w.Wache.IstWachbetrieb(b);
        if (!_wacheText.Visible) return;
        var text = string.Join("\n", WacheZeilen(b, w));
        if (_wacheText.Text != text) _wacheText.Text = text;
    }

    /// <summary>Die Zeilen zur Wache eines Hauses (Sold, Streife, Razzien, Geleit, Ansehen) – im Bedienfeld und auf der Tafel des Wachplans (<see cref="WachplanAnzeige"/>).</summary>
    public static List<string> WacheZeilen(Betrieb b, Stadtwirtschaft w)
    {
        var wache = w.Wache;
        var d = wache.Daten;
        var waehrung = w.Daten.Allgemein.Waehrung;
        string Buch(Reichsgilde.Core.Ordnung.Wachbuch x) =>
            $"Sold {Zahlenformat.Ganz(x.Sold)}, Kopfgeld {Zahlenformat.Ganz(x.Kopfgeld)}, Geleit {Zahlenformat.Ganz(x.Geleitgeld)} {waehrung}, {x.Razzien} {(x.Razzien == 1 ? "Razzia" : "Razzien")}";
        var jahr = Zahlenformat.Ganz(wache.WertJeMinute(b.Stufe) * w.Daten.Allgemein.Arbeitsminuten);
        var zeilen = new List<string>
        {
            $"Wache – die Stadt zahlt Sold: ≈ {jahr} {waehrung} je Geselle und Jahr (nach Steuer). Dieses Jahr (ganzes Haus): {Buch(wache.DiesesJahr(b.Konto))}",
        };
        if (wache.Vorjahr(b.Konto) is { } vorjahr) zeilen.Add($"Vorjahr: {Buch(vorjahr)}");
        if (b.InDerStadt)
        {
            zeilen.Add($"Streife: Nachts gelingen Diebstahl, Sabotage, Einbruch und Brand in der Stadt {Zahlenformat.Ganz(wache.Patrouilleschutz * 100)} % seltener " +
                       $"(alle Wachen zusammen, höchstens {Zahlenformat.Ganz(d.Patrouille.Hoechstens * 100)} %), Zeugen sehen mehr.");
            zeilen.Add($"Razzien (im Gerichtssaal): noch {wache.FreieRazzien(b.Konto)} dieses Jahr, {Zahlenformat.Ganz(wache.Razziachance(b.Konto) * 100)} % je Tat. " +
                       $"Kopfgeld: {Zahlenformat.Ganz(d.Kopfgeld.AnteilBusse * 100)} % der Buße, wenn ihr eine Anklage gewinnt; {Zahlenformat.Ganz(d.Kopfgeld.Taschendieb)} {waehrung} je gefasstem Taschendieb.");
            zeilen.Add(b.Stufe >= d.Geleit.AbStufe
                ? $"Geleitschutz: Karren aller Häuser können mit der „{d.Geleit.Eskorte}“ fahren – der Lohn geht an das Haus mit der größten Wache."
                : $"Geleitschutz ab Stufe {d.Geleit.AbStufe}.");
        }
        else
            zeilen.Add($"Landstraße: Banden überfallen Karren auf allen Umlandwegen {Zahlenformat.Ganz((1 - wache.Landstrassenfaktor) * 100)} % seltener.");
        zeilen.Add($"Ein Haus der Wache: {Zahlenformat.Ganz(d.Ansehen)} Ansehen mehr.");
        return zeilen;
    }

    /// <summary>Kasse und Rahmen, Stadtzinsen und Wechsel, der Zins für Häuser und die offenen Kredite des Hauses.</summary>
    private void BankstubeAktualisieren(Betrieb b, Stadtwirtschaft w)
    {
        var bank = w.Bank;
        _bankstube.Visible = bank.IstBankbetrieb(b);
        if (!_bankstube.Visible) return;
        var d = bank.Daten;
        var waehrung = w.Daten.Allgemein.Waehrung;
        string Buch(Reichsgilde.Core.Bank.Bankbuch x) =>
            $"Stadtzinsen {Zahlenformat.Ganz(x.Stadtzinsen)}, Wechsel {Zahlenformat.Ganz(x.Wechsel)}, Kreditzinsen {Zahlenformat.Ganz(x.Hauszinsen)} {waehrung}"
            + (x.Ausfall > 0 ? $" · Ausfälle {Zahlenformat.Ganz(x.Ausfall)}" : "");
        var kasse = bank.Kasse(b);
        var voll = bank.RahmenVoll(b);
        var jahr = Zahlenformat.Ganz(bank.WertJeMinute(b) * w.Daten.Allgemein.Arbeitsminuten);
        var zeilen = new List<string>
        {
            $"Geldleihe – die Schreiber verleihen das Geld der Kasse an Bürger ({Zahlenformat.Ganz(d.Stadtzins * 100)} % im Jahr) und wechseln Münzen am Markt: " +
            $"≈ {jahr} {waehrung} je Geselle und Jahr bei voller Kasse (nach Steuer und Ausfällen).",
            $"Kasse {Zahlenformat.Ganz(kasse)} {waehrung} · die Schreiber betreuen jetzt {Zahlenformat.Ganz(bank.Rahmen(b))}, voll besetzt {Zahlenformat.Ganz(voll)}" +
            (kasse > voll + 0.5 ? " – was darüber liegt, bringt nichts." : kasse < voll - 0.5 ? " – füllt die Kasse, sonst bleiben Schreiber ohne Arbeit." : "."),
            $"Dieses Jahr (ganzes Haus): {Buch(bank.DiesesJahr(b.Konto))}",
        };
        if (bank.Vorjahr(b.Konto) is { } vorjahr) zeilen.Add($"Vorjahr: {Buch(vorjahr)}");
        zeilen.Add($"Jedes Jahr fallen mit {Zahlenformat.Ganz(d.Ausfall.Chance * 100)} % Kunden aus – dann sind {Zahlenformat.Ganz(d.Ausfall.Anteil * 100)} % der Kasse verloren.");
        var kredite = bank.KrediteVon(b.Konto).ToList();
        zeilen.Add(kredite.Count == 0
            ? $"Kredite an Häuser: keine. Andere Häuser leihen im Kontor – höchstens {Zahlenformat.Ganz(bank.FreierKreditrahmen(b.Konto))} {waehrung} sind noch frei (Obergrenze {Zahlenformat.Ganz(bank.Kredithoechstbetrag(b.Konto))})."
            : "Kredite an Häuser: " + string.Join(" · ", kredite.Select(k =>
                $"{k.Nehmer.Inhaber} {Zahlenformat.Ganz(k.Rest)} zu {Zahlenformat.Ganz(k.Satz * 100)} %, noch {k.JahreOffen} {(k.JahreOffen == 1 ? "Jahr" : "Jahre")}" + (k.IstWucher ? " (Wucher!)" : ""))));
        zeilen.Add($"Über {Zahlenformat.Ganz(d.Wucher.Ab * 100)} % ist Wucher: eine Spur fürs Gericht, der Schuldner hat den Beweis, {Zahlenformat.Ganz(d.Wucher.Ansehen)} Ansehen weniger. " +
                   $"Geldleihe +{d.ApJeStufe[1]} AP, Bankhaus +{d.ApJeStufe[2]} AP je Jahr.");
        var text = string.Join("\n", zeilen);
        if (_bankText.Text != text) _bankText.Text = text;

        var fehlt = Math.Floor(Math.Min(voll - kasse, b.Konto.Vermoegen));
        var fuellen = $"auffüllen (+{Zahlenformat.Ganz(Math.Max(0, fehlt))})";
        if (_kasseFuellen.Text != fuellen) _kasseFuellen.Text = fuellen;
        _kasseFuellen.Disabled = fehlt < 1;
        if (_zinsFuer != b)
        {
            // Grenzen und Wert nur beim Wechsel des Betriebs setzen – sonst überschriebe jedes Bild die Eingabe.
            _zinsWahl.MinValue = d.Kredit.ZinsMindestens * 100;
            _zinsWahl.MaxValue = d.Kredit.ZinsHoechstens * 100;
            _zinsWahl.SetValueNoSignal(Math.Round(bank.Zinssatz(b.Konto) * 100));
            _zinsFuer = b;
        }
        var tipp = $"Gilt für neue Kredite. Jetzt: {Zahlenformat.Ganz(bank.Zinssatz(b.Konto) * 100)} %. Über {Zahlenformat.Ganz(d.Wucher.Ab * 100)} % ist Wucher.";
        if (_zinsKnopf.TooltipText != tipp) _zinsKnopf.TooltipText = tipp;
    }

    /// <summary>Taschendiebe (Beute, Fänge, Fangchance) und was diese Stufe an nächtlichen Aufträgen erlaubt.</summary>
    private void DiebesstubeAktualisieren(Betrieb b, Stadtwirtschaft w)
    {
        var diebe = w.Diebe;
        _diebesText.Visible = diebe.IstDiebesbetrieb(b);
        if (!_diebesText.Visible) return;
        var d = diebe.Daten;
        var waehrung = w.Daten.Allgemein.Waehrung;
        string Buch(Reichsgilde.Core.Gauner.Diebesbuch x) => $"{Zahlenformat.Ganz(x.Beute)} {waehrung} erbeutet, {x.Faenge} erwischt";
        var jahr = Zahlenformat.Ganz(diebe.WertJeMinute(b.Stufe) * w.Daten.Allgemein.Arbeitsminuten);
        var zeilen = new List<string>
        {
            $"Taschendiebe – tagsüber am Markt, steuerfrei: ≈ {jahr} {waehrung} je Geselle und Jahr. Dieses Jahr: {Buch(diebe.DiesesJahr(b.Konto))}",
        };
        if (diebe.Vorjahr(b.Konto) is { } vorjahr) zeilen.Add($"Vorjahr: {Buch(vorjahr)}");
        zeilen.Add($"Fangchance ≈ {Zahlenformat.Kommazahl(diebe.Fangchance(b) * 100)} % je Dieb und Stunde – wer erwischt wird, kommt in den Turm, " +
                   "und ein anderes Haus erfährt, wem er diente.");
        zeilen.Add($"Nachts {d.AuftraegeJeStufe[b.Stufe - 1]} {(d.AuftraegeJeStufe[b.Stufe - 1] == 1 ? "Auftrag" : "Aufträge")} (im Hinterzimmer): Einbruch, Auskundschaften" +
                   (b.Stufe >= d.Schutzgeld.AbStufe ? ", Schutzgeld" : "") + (b.Stufe >= d.Entfuehrung.AbStufe ? ", Entführung" : "") + ".");
        zeilen.Add($"Ein Haus von Dieben: {Zahlenformat.Ganz(d.Ansehen)} Ansehen weniger.");
        var text = string.Join("\n", zeilen);
        if (_diebesText.Text != text) _diebesText.Text = text;
    }

    /// <summary>Messe und Kollekte, Ablässe, Anziehung und – je Haus – das Beichtwissen mit dem Knopf, es preiszugeben.</summary>
    private void KirchenstubeAktualisieren(Betrieb b, Stadtwirtschaft w)
    {
        var kw = w.Kirchenwesen;
        _kirchenstube.Visible = kw.IstKirche(b);
        if (!_kirchenstube.Visible) return;
        var d = kw.Daten;
        var waehrung = w.Daten.Allgemein.Waehrung;
        string Buch(Reichsgilde.Core.Kirche.Kirchenbuch x) =>
            $"{Zahlenformat.Ganz(x.Glaeubige)} Gläubige, Kollekte {Zahlenformat.Ganz(x.Kollekte)} {waehrung}"
            + (x.Ablaesse > 0 ? $" · {x.Ablaesse} {(x.Ablaesse == 1 ? "Ablass" : "Ablässe")} für {Zahlenformat.Ganz(x.Ablassgeld)} {waehrung}" : "");
        var zeilen = new List<string>
        {
            $"Kirche – Messe um {d.MesseUm} Uhr, die Gläubigen spenden (steuerfrei). Dieses Jahr: {Buch(kw.DiesesJahr(b))}",
        };
        if (kw.Vorjahr(b) is { } vorjahr) zeilen.Add($"Vorjahr: {Buch(vorjahr)}");
        var gw = w.Glaube;
        if (gw.IstAktiv && gw.KonfessionVon(b) is { } konfession)
        {
            var anteile = string.Join(", ", gw.Anteile.OrderBy(x => x.Key).Select(x => $"{x.Key.Gemeinde()} {Zahlenformat.Ganz(x.Value * 100)} %"));
            zeilen.Add($"Eure Kirche ist {konfession.Name()}. In der Stadt: {anteile} – jede Gemeinde geht in ihre Kirche; " +
                       $"ohne eigene Kirche kommen Andersgläubige auch hierher, spenden aber nur {Zahlenformat.Ganz(gw.Daten.FremdeKirche * 100)} %.");
            if (gw.IstKetzerkirche(b))
                zeilen.Add($"Stadtglaube ist {gw.Stadtglaube!.Value.Name()} – eure Messe ist Ketzerei: Am Jahresende eine Spur, die {gw.Stadtglaube.Value.Gemeinde()} haben sie gesehen.");
        }
        zeilen.Add($"Erwartet ≈ {Zahlenformat.Ganz(kw.ErwarteteGlaeubige(b))} Gläubige und {Zahlenformat.Ganz(kw.ErwarteteKollekte(b))} {waehrung} Kollekte im Jahr (Stufe, Ansehen und Rhetorik ziehen an).");
        zeilen.Add($"Ablass: Andere Häuser kaufen hier ihre Taten frei – ab {Zahlenformat.Ganz(d.Ablass.Grundpreis)} {waehrung}, das Geld geht an euch.");
        var wissen = w.Konten.Where(k => k != b.Konto && k.IstAktiv)
            .Select(k => (Haus: k, Taten: kw.Beichtwissen(b.Konto, k).Count())).Where(x => x.Taten > 0).ToList();
        zeilen.Add(wissen.Count == 0
            ? $"Beichte: Am Jahresende hört ihr von fremden Taten ({Zahlenformat.Ganz(d.Beichte.Chance * 100)} % je Tat) – noch wisst ihr nichts."
            : "Beichte – was ihr wisst (preisgeben macht Beweise daraus, ist aber selbst ein Vergehen):");
        var text = string.Join("\n", zeilen);
        if (_kirchenText.Text != text) _kirchenText.Text = text;

        _bekehrenKnopf.Visible = gw.IstAktiv && b.Konto == _spieler && w.HatAktionspunkte;
        if (_bekehrenKnopf.Visible)
        {
            var grund = gw.BekehrenVerboten(_spieler!);
            _bekehrenKnopf.Text = "Bekehrungspredigt";
            _bekehrenKnopf.Disabled = grund is not null;
            _bekehrenKnopf.TooltipText = grund ?? $"+{Zahlenformat.Kommazahl(gw.Bekehrungskraft(_spieler!) * 100)} Prozentpunkte für eure Gemeinde (Rhetorik hilft), " +
                                                  $"+{Zahlenformat.Ganz(gw.Daten.Bekehrung.Gunst)} Gunst bei jedem Ratsherrn eures Glaubens. Einmal im Jahr." +
                                                  (gw.Stadtglaube is { } sg && sg != gw.GlaubeVon(_spieler!) ? " Gegen den Stadtglauben ist das Ketzerei!" : "");
            UiHilfe.ApKosten(_bekehrenKnopf, w, _spieler!, new Bekehrungspredigt());
        }

        var plan = string.Join("|", wissen.Select(x => $"{x.Haus.Inhaber}:{x.Taten}"));
        if (plan == _beichtPlan) return;
        _beichtPlan = plan;
        foreach (var kind in _beichtKnoepfe.GetChildren()) kind.QueueFree();
        var ap = w.ApKosten(_spieler!, new BeichtgeheimnisBrechen(""));
        foreach (var (haus, taten) in wissen)
        {
            var knopf = new Button
            {
                Text = $"{haus.Inhaber}: {taten} {(taten == 1 ? "Tat" : "Taten")} – Beichtgeheimnis brechen ({ap} AP)",
                FocusMode = FocusModeEnum.None,
                TooltipText = "Wird daraus ein Beweis gegen das Haus – und gegen euch die Tat „Bruch des Beichtgeheimnisses“.",
            };
            knopf.AddThemeFontSizeOverride("font_size", 13);
            var name = haus.Inhaber;
            knopf.Pressed += () => Befehlen(new BeichtgeheimnisBrechen(name));
            _beichtKnoepfe.AddChild(knopf);
        }
    }

    /// <summary>„Stufe 1: Gießerei" und der Knopf zum Ausbau – gesperrt mit Begründung, solange Grad oder Geld fehlen.</summary>
    private void AusbauAktualisieren(Betrieb b, Stadtwirtschaft w)
    {
        _ausbauZeile.Visible = b.InDerStadt && w.Daten.Meistergrade is not null;
        if (!_ausbauZeile.Visible) return;
        var waehrung = w.Daten.Allgemein.Waehrung;
        var beruf = b.Artdaten.Beruf;
        var text = $"Stufe {b.Stufe}: {b.Artdaten.Name} · {beruf} im {_spieler!.Meistergrad(beruf)}. Grad";
        if (UiHilfe.VolksZusatz(w, _spieler, beruf) is { Length: > 0 } volk)
            text += $" · Volk{volk}";
        if (b.UmbauMinuten > 0) text += $" · Umbau noch {Zahlenformat.Ganz(b.UmbauMinuten / 60.0)} Std.";
        _ausbauKnopf.Visible = b.Artdaten.Ausbau is not null;
        if (b.Artdaten.Ausbau is { } ziel)
        {
            var neu = w.Daten.Art(ziel);
            var knopf = $"Ausbau: {neu.Name} ({Zahlenformat.Ganz(b.Artdaten.AusbauKosten)} {waehrung})";
            if (_ausbauKnopf.Text != knopf) _ausbauKnopf.Text = knopf;
            var grund = w.AusbauVerboten(_spieler, b);
            _ausbauKnopf.Disabled = grund is not null;
            var neueWaren = neu.Produkte.Where(p => !b.Artdaten.Produkte.Contains(p));
            var tipp = (grund is not null ? grund + "\n\n" : "")
                       + $"Stufe {neu.Stufe}, ab {w.Daten.Meistergrade!.GradFuerStufe(neu.Stufe)}. Meistergrad: {neu.Plaetze} Arbeitsplätze, "
                       + $"neue Waren {string.Join(", ", neueWaren)}; Unterhalt {Zahlenformat.Ganz(neu.Fixkosten)} {waehrung} im Jahr.\n"
                       + $"Erwarteter Mehrgewinn bei voller Belegung ≈ {Zahlenformat.MitVorzeichen(Kalkulation.AusbauMehrgewinn(w, b))} {waehrung} im Jahr. "
                       + "Der Umbau dauert einen halben Arbeitstag.";
            if (_ausbauKnopf.TooltipText != tipp) { _ausbauKnopf.TooltipText = tipp; _ausbauText.TooltipText = tipp; }
        }
        else text += " · höchste Stufe";
        _ausbauText.Text = text;
    }

    private void ZeilenNeuBauen(Betrieb b, Stadtwirtschaft w)
    {
        UiHilfe.KinderEntfernen(_personal);
        UiHilfe.KinderEntfernen(_karren);
        _personalZeilen.Clear();
        _karrenZeilen.Clear();

        // Personal: alle Arbeitskräfte, die hier erlaubt sind – plus die, die (noch) beschäftigt sind.
        var kraefte = w.Daten.Arbeitskraefte
            .Where(k => k.Value.IstErlaubt(w.Statut, b.Ort) || b.Belegung.ContainsKey(k.Key))
            .Select(k => k.Key);
        foreach (var kraft in kraefte)
        {
            var zeile = Zeile(_personal);
            var name = NeuesLabel(zeile, 14);
            name.Text = kraft;
            name.CustomMinimumSize = new Vector2(92, 0);
            var minus = KleinerKnopf(zeile, "−");
            var anzahl = NeuesLabel(zeile, 14);
            anzahl.CustomMinimumSize = new Vector2(18, 0);
            anzahl.HorizontalAlignment = HorizontalAlignment.Center;
            var plus = KleinerKnopf(zeile, "+");
            var info = NeuesLabel(zeile, 12, Grau);
            var kraftTipp = $"{kraft}: Grundlohn {Zahlenformat.Ganz(w.Daten.Lohn(kraft, b.Art))} {w.Daten.Allgemein.Waehrung}. Mit − und + stellt ihr Leute ein oder entlasst sie.";
            name.TooltipText = anzahl.TooltipText = info.TooltipText = kraftTipp;
            minus.Pressed += () => Befehlen(new PersonalSetzen(b.Name, kraft, b.Belegung.GetValueOrDefault(kraft) - 1));
            plus.Pressed += () => Befehlen(new PersonalSetzen(b.Name, kraft, b.Belegung.GetValueOrDefault(kraft) + 1));
            _personalZeilen.Add((kraft, anzahl, info, minus, plus));
        }

        // Karren: im Umland mit Eskorte und Nachtfahrt, in der Stadt nur der Zustand.
        for (var i = 0; i < b.Karren.Count; i++)
        {
            var nummer = i;
            var spalte = new VBoxContainer();
            _karren.AddChild(spalte);
            var zeile = Zeile(spalte);
            var name = NeuesLabel(zeile, 14);
            name.Text = b.Karren[i].Name;
            name.CustomMinimumSize = new Vector2(110, 0);

            // Knoten nur anlegen, wenn sie auch in den Baum kommen: Godot gibt Knoten außerhalb
            // des Baums nie von selbst frei (Speicherleck beim Beenden).
            OptionButton? eskorte = null;
            CheckBox? nachts = null;
            if (b.Ort == Arbeitsort.Umland)
            {
                eskorte = new OptionButton { FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill };
                nachts = new CheckBox { Text = "nachts", FocusMode = FocusModeEnum.None };
                foreach (var e in w.Daten.Logistik.Eskorten.Keys) eskorte.AddItem(e);
                eskorte.ItemSelected += index => Befehlen(new EskorteWaehlen(b.Name, nummer, w.Daten.Logistik.Eskorten.Keys.ElementAt((int)index)));
                nachts.Toggled += an => Befehlen(new NachtfahrtErlauben(b.Name, nummer, an));
                nachts.TooltipText = "Auch in der Dunklen Nacht ins Umland fahren (doppeltes Überfallrisiko)" + (w.Daten.Logistik.Tore is { } tore
                    ? $". Nach Torschluss ({tore.SchliessenUm} Uhr) zahlt der Karren {Zahlenformat.Ganz(tore.Torgeld)} {w.Daten.Allgemein.Waehrung} Torgeld je Durchfahrt – ohne Erlaubnis wartet er bis zum Morgen vor dem Tor."
                    : "");
                zeile.AddChild(eskorte);
                zeile.AddChild(nachts);
            }
            else
            {
                NeuesLabel(zeile, 12, Grau).Text = "Stadtweg – keine Überfälle, keine Eskorte nötig";
            }
            // Verkaufen (PR #205): ohne Rückfrage, gesperrt mit Grund aus dem Kern (unterwegs)
            var verkaufen = new Button { FocusMode = FocusModeEnum.None };
            verkaufen.AddThemeFontSizeOverride("font_size", 13);
            verkaufen.Pressed += () => Befehlen(new KarrenVerkaufen(b.Name, nummer));
            zeile.AddChild(verkaufen);
            var info = NeuesLabel(spalte, 12, Grau);
            info.TooltipText = "Zustand des Karrens: wartet, ist unterwegs oder auf dem Rückweg, mit Ladung und Eskorte.";
            name.TooltipText = $"{b.Karren[i].Name}: einer der Karren dieses Betriebs.";
            _karrenZeilen.Add((eskorte, nachts, info, verkaufen));
        }
    }

    // ── Bausteine ───────────────────────────────────────────────────────────

    private static HBoxContainer Zeile(Container eltern)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 6);
        eltern.AddChild(zeile);
        return zeile;
    }

    private static Button KleinerKnopf(Container eltern, string text)
    {
        // FocusMode None: Sonst würde die Leertaste (Pause) den zuletzt geklickten Knopf erneut drücken.
        var knopf = new Button { Text = text, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(28, 0) };
        eltern.AddChild(knopf);
        return knopf;
    }

    private static Label NeuesLabel(Container eltern, int groesse, Color? farbe = null)
    {
        var label = new Label();
        label.AddThemeFontSizeOverride("font_size", groesse);
        if (farbe is { } f) label.AddThemeColorOverride("font_color", f);
        label.MouseFilter = MouseFilterEnum.Pass; // damit ein Tooltip (Tipp) die Maus findet
        eltern.AddChild(label);
        return label;
    }
}
