using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das Kontor: zeigt Vermögen, Stand, alle eigenen Betriebe, Jahresrechnung und Marktpreise an.
/// Reine Anzeige – alle Zahlen kommen aus dem Simulationskern.
/// Die Bedienelemente werden hier im Code erzeugt, damit die Szene übersichtlich bleibt.
/// </summary>
public partial class KontorAnzeige : PanelContainer
{
    private Simulation? _simulation;
    private Konto? _spieler;
    private Konto? _rivale;

    private Label _titel = null!;
    private Label _vermoegen = null!;
    private Label _standZeile = null!;
    private Label _statutZeile = null!;
    private VBoxContainer _betriebsliste = null!;
    private readonly List<(Betrieb Betrieb, Label Kopf, Label Zustand)> _betriebsZeilen = new();
    private Label _diesesJahr = null!;
    private Label _vorjahr = null!;
    private Label _rivaleZeile = null!;

    /// <summary>Der Spieler möchte einen seiner Betriebe verwalten (⚒-Knopf im Kontor).</summary>
    public event Action<Betrieb>? VerwaltenGewuenscht;
    private GridContainer _preise = null!;
    private readonly Dictionary<string, Label> _preisLabels = new();

    public override void _Ready()
    {
        var rand = new MarginContainer();
        foreach (var seite in new[] { "left", "right", "top", "bottom" })
            rand.AddThemeConstantOverride($"margin_{seite}", 22);
        AddChild(rand);

        var spalte = new VBoxContainer();
        spalte.AddThemeConstantOverride("separation", 6);
        rand.AddChild(spalte);

        _titel = NeuesLabel(spalte, 22);
        _vermoegen = NeuesLabel(spalte, 26);
        _standZeile = NeuesLabel(spalte, 14, new Color(0.95f, 0.82f, 0.45f));
        _statutZeile = NeuesLabel(spalte, 13, new Color(0.75f, 0.75f, 0.75f));
        // Je Betrieb zwei Zeilen und ein ⚒-Knopf – neu gebaut, sobald ein Betrieb dazukommt.
        _betriebsliste = new VBoxContainer();
        _betriebsliste.AddThemeConstantOverride("separation", 2);
        spalte.AddChild(_betriebsliste);
        _diesesJahr = NeuesLabel(spalte, 15);
        _vorjahr = NeuesLabel(spalte, 15);
        _rivaleZeile = NeuesLabel(spalte, 14, new Color(0.75f, 0.75f, 0.75f));
        _rivaleZeile.AutowrapMode = TextServer.AutowrapMode.WordSmart; // lange KI-Züge umbrechen statt das Kontor zu verbreitern
        _rivaleZeile.CustomMinimumSize = new Vector2(360, 0);

        spalte.AddChild(new HSeparator());
        NeuesLabel(spalte, 15).Text = "Marktplatz";

        _preise = new GridContainer { Columns = 3 };
        _preise.AddThemeConstantOverride("h_separation", 18);
        spalte.AddChild(_preise);
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
        foreach (var kind in _betriebsliste.GetChildren()) kind.QueueFree();
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

        // Kopfzeile der Preistabelle und eine Zeile je Ware.
        foreach (var kopf in new[] { "Ware", "Preis", "Grundpreis" })
            NeuesLabel(_preise, 13, new Color(0.7f, 0.7f, 0.7f)).Text = kopf;
        foreach (var stand in simulation.Wirtschaft!.Markt.Staende)
        {
            NeuesLabel(_preise, 14).Text = stand.Ware;
            _preisLabels[stand.Ware] = NeuesLabel(_preise, 14);
            NeuesLabel(_preise, 14, new Color(0.7f, 0.7f, 0.7f)).Text = Zahlenformat.Ganz(stand.Grundpreis);
        }
    }

    /// <summary>Überträgt die aktuellen Zahlen auf die Anzeige.</summary>
    public void Aktualisieren()
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null)
            return;

        var waehrung = w.Daten.Allgemein.Waehrung;
        _titel.Text = $"Kontor · {_spieler.Inhaber} · {w.StandVon(_spieler).Name}";
        _vermoegen.Text = $"{Zahlenformat.Ganz(_spieler.Vermoegen)} {waehrung}";
        _vermoegen.AddThemeColorOverride("font_color", _spieler.Vermoegen < 0 ? new Color(0.95f, 0.45f, 0.4f) : new Color(1, 1, 1));
        _standZeile.Text = _spieler.IstBankrott ? "Bankrott"
            : w.NaechsterStand(_spieler) is not { } neu ? "Höchster Stand erreicht"
            : _spieler.Vermoegen >= neu.Schwelle ? $"Aufstieg zum {neu.Name} im Rathaus möglich!"
            : $"Nächster Stand: {neu.Name} ab {Zahlenformat.Ganz(neu.Schwelle)} {waehrung} (noch {Zahlenformat.Ganz(neu.Schwelle - Math.Max(0, _spieler.Vermoegen))})";

        _statutZeile.Text = $"Statut {w.Statut.Kuerzel()} – {w.Statut.Name()}"
                          + (w.IstArbeitszeit(_simulation.Zeit) ? " · es wird gearbeitet" : " · Feierabend");

        var eigene = w.Betriebe.Where(x => x.Konto == _spieler).ToList();
        if (eigene.Count != _betriebsZeilen.Count)
            BetriebsZeilenBauen(eigene);
        foreach (var (betrieb, kopf, zustand) in _betriebsZeilen)
        {
            var belegung = betrieb.Belegt == 0 ? "kein Personal" : string.Join(", ", betrieb.Belegung.Select(x => $"{x.Value}× {x.Key}"));
            var was = betrieb.Art == Betriebsart.Giesserei ? betrieb.Produkt : $"{Zahlenformat.Ganz(betrieb.Silberanteil * 100)} % Silber";
            kopf.Text = $"{betrieb.Name}: {was} · {belegung}";
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

        _rivaleZeile.Text = _rivale is null ? "" : $"Rivale {_rivale.Inhaber}: {Zahlenformat.Ganz(_rivale.Vermoegen)} {waehrung}"
            + RivalenZug(_rivale);

        foreach (var (ware, label) in _preisLabels)
            label.Text = Zahlenformat.Kommazahl(w.Markt[ware].Preis);
    }
}
