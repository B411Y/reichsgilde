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
