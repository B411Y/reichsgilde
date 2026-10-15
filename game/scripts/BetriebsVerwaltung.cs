using Godot;
using Reichsgilde.Core;
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
    private VBoxContainer _kirchenstube = null!;
    private Label _kirchenText = null!;
    private VBoxContainer _beichtKnoepfe = null!;
    private string _beichtPlan = "";
    private HBoxContainer _ausbauZeile = null!;
    private Label _ausbauText = null!;
    private Button _ausbauKnopf = null!;
    private Label _foerderName = null!;
    private VBoxContainer _personal = null!;
    private VBoxContainer _karren = null!;
    private OptionButton _karrenTyp = null!;
    private readonly List<string> _karrenTypen = new();
    private Button _kaufenKnopf = null!;

    // Zeilen, die beim Neubau ersetzt werden
    private readonly List<(string Kraft, Label Anzahl, Label Info, Button Minus, Button Plus)> _personalZeilen = new();
    private readonly List<(OptionButton? Eskorte, CheckBox? Nachts, Label Info)> _karrenZeilen = new();

    /// <summary>Ergebnis eines Befehls (Erfolg oder Ablehnung) – für das Protokoll.</summary>
    public event Action<string>? Gemeldet;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 6);
        AddChild(new HSeparator());
        _ueberschrift = NeuesLabel(this, 15);
        _ueberschrift.Text = "⚒ Verwalten";

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

        // Kirche (nur Kirchen des Predigers): Messe, Kollekte, Ablass, Beichtwissen
        _kirchenstube = new VBoxContainer();
        _kirchenstube.AddThemeConstantOverride("separation", 4);
        AddChild(_kirchenstube);
        _kirchenText = NeuesLabel(_kirchenstube, 13, Grau);
        _kirchenText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _kirchenText.CustomMinimumSize = new Vector2(380, 0);
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
        NeuesLabel(this, 14).Text = "Karren:";
        _karren = new VBoxContainer();
        AddChild(_karren);
        var kauf = Zeile(this);
        _karrenTyp = new OptionButton { FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        kauf.AddChild(_karrenTyp);
        _kaufenKnopf = new Button { Text = "Kaufen", FocusMode = FocusModeEnum.None };
        _kaufenKnopf.Pressed += () => Befehlen(new KarrenKaufen(_betrieb!.Name, _karrenTypen[_karrenTyp.Selected]));
        kauf.AddChild(_kaufenKnopf);

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

        var bauplan = $"{w.Statut}|{b.Karren.Count}|{string.Join(",", b.Belegung.Keys.OrderBy(k => k))}";
        if (bauplan != _bauplan)
        {
            ZeilenNeuBauen(b, w);
            _bauplan = bauplan;
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

        _foerderZeile.Visible = b.Rohstoffe.Count >= 2; // nur, wer zwei Rohstoffe fördert, teilt auf (Grube: Eisen/Silber)
        if (_foerderZeile.Visible)
        {
            if (!_silberRegler.HasFocus()) _silberRegler.SetValueNoSignal(b.Silberanteil * 100);
            _foerderName.Text = $"{b.Rohstoffe[1]}:";
            _silberText.Text = $"{Zahlenformat.Ganz(b.Silberanteil * 100)} % {b.Rohstoffe[1]}";
        }

        // Personal
        _personalKopf.Text = $"Personal ({b.Belegt}/{b.Plaetze} Plätze, Statut {w.Statut.Kuerzel()}):";
        foreach (var (kraft, anzahl, info, minus, plus) in _personalZeilen)
        {
            var daten = w.Daten.Arbeitskraefte[kraft];
            var jetzt = b.Belegung.GetValueOrDefault(kraft);
            anzahl.Text = jetzt.ToString();
            minus.Disabled = jetzt == 0;
            plus.Disabled = b.Belegt >= b.Plaetze || !daten.IstErlaubt(w.Statut, b.Ort);
            info.Text = $"Lohn {Zahlenformat.Ganz(daten.Kosten)} · Ertrag ≈ {Zahlenformat.MitVorzeichen(Kalkulation.Jahresertrag(w, b, kraft))}/Jahr"
                        + (daten.Risiko > 0 ? $" · Risiko {Zahlenformat.Ganz(daten.Risiko * 100)} %" : "");
        }

        // Karren
        for (var i = 0; i < _karrenZeilen.Count && i < b.Karren.Count; i++)
        {
            var (eskorte, nachts, info) = _karrenZeilen[i];
            var karren = b.Karren[i];
            if (eskorte is not null && nachts is not null)
            {
                var namen = w.Daten.Logistik.Eskorten.Keys.ToList();
                var wert = Kalkulation.Ladungswert(w, karren);
                for (var e = 0; e < namen.Count; e++)
                    eskorte.SetItemText(e, $"{namen[e]} · ≈ {Zahlenformat.Ganz(Kalkulation.ErwarteteFahrtkosten(w, namen[e], wert))} {waehrung}/Fahrt");
                eskorte.Select(namen.IndexOf(karren.Eskorte));
                nachts.SetPressedNoSignal(karren.NachtsFahren);
            }
            info.Text = karren.ZustandText(_simulation.Zeit);
        }
        _kaufenKnopf.Disabled = b.Konto.Vermoegen < w.Daten.Logistik.Karren[_karrenTypen[Math.Max(0, _karrenTyp.Selected)]].Preis;
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
        zeilen.Add($"Fangchance ≈ {Zahlenformat.Kommazahl(diebe.Fangchance(b.Konto, b.Stufe) * 100)} % je Dieb und Stunde – wer erwischt wird, kommt in den Turm, " +
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
        zeilen.Add($"Erwartet ≈ {Zahlenformat.Ganz(kw.ErwarteteGlaeubige(b))} Gläubige im Jahr (Stufe, Ansehen und Rhetorik ziehen an).");
        zeilen.Add($"Ablass: Andere Häuser kaufen hier ihre Taten frei – ab {Zahlenformat.Ganz(d.Ablass.Grundpreis)} {waehrung}, das Geld geht an euch.");
        var wissen = w.Konten.Where(k => k != b.Konto && k.IstAktiv)
            .Select(k => (Haus: k, Taten: kw.Beichtwissen(b.Konto, k).Count())).Where(x => x.Taten > 0).ToList();
        zeilen.Add(wissen.Count == 0
            ? $"Beichte: Am Jahresende hört ihr von fremden Taten ({Zahlenformat.Ganz(d.Beichte.Chance * 100)} % je Tat) – noch wisst ihr nichts."
            : "Beichte – was ihr wisst (preisgeben macht Beweise daraus, ist aber selbst ein Vergehen):");
        var text = string.Join("\n", zeilen);
        if (_kirchenText.Text != text) _kirchenText.Text = text;

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
                nachts.TooltipText = "Auch in der Dunklen Nacht ins Umland fahren (doppeltes Überfallrisiko)";
                zeile.AddChild(eskorte);
                zeile.AddChild(nachts);
            }
            else
            {
                NeuesLabel(zeile, 12, Grau).Text = "Stadtweg – keine Überfälle, keine Eskorte nötig";
            }
            var info = NeuesLabel(spalte, 12, Grau);
            _karrenZeilen.Add((eskorte, nachts, info));
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
        eltern.AddChild(label);
        return label;
    }
}
