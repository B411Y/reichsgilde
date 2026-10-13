using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das Kontor: zeigt Vermögen, Betrieb, Jahresrechnung und Marktpreise an.
/// Reine Anzeige – alle Zahlen kommen aus dem Simulationskern.
/// Die Bedienelemente werden hier im Code erzeugt, damit die Szene übersichtlich bleibt.
/// </summary>
public partial class KontorAnzeige : PanelContainer
{
    private Simulation? _simulation;
    private Konto? _spieler;
    private Betrieb? _betrieb;
    private Konto? _rivale;

    private Label _titel = null!;
    private Label _vermoegen = null!;
    private Label _betriebZeile = null!;
    private Label _diesesJahr = null!;
    private Label _vorjahr = null!;
    private Label _rivaleZeile = null!;
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
        _betriebZeile = NeuesLabel(spalte, 15);
        _diesesJahr = NeuesLabel(spalte, 15);
        _vorjahr = NeuesLabel(spalte, 15);
        _rivaleZeile = NeuesLabel(spalte, 14, new Color(0.75f, 0.75f, 0.75f));

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

    /// <summary>Verbindet die Anzeige mit der Simulation. Wird von der Hauptszene aufgerufen.</summary>
    public void Verbinden(Simulation simulation, Konto spieler, Betrieb betrieb, Konto? rivale)
    {
        _simulation = simulation;
        _spieler = spieler;
        _betrieb = betrieb;
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
        if (_simulation?.Wirtschaft is not { } w || _spieler is null || _betrieb is null)
            return;

        var waehrung = w.Daten.Allgemein.Waehrung;
        _titel.Text = $"Kontor · {_spieler.Inhaber}";
        _vermoegen.Text = $"{Zahlenformat.Ganz(_spieler.Vermoegen)} {waehrung}";

        var belegung = string.Join(", ", _betrieb.Belegung.Select(b => $"{b.Value}× {b.Key}"));
        _betriebZeile.Text = $"{_betrieb.Name}: {_betrieb.Produkt} · {belegung}\nStatut {w.Statut.Kuerzel()} – {w.Statut.Name()}"
                           + (w.IstArbeitszeit(_simulation.Zeit) ? " · es wird gearbeitet" : " · Feierabend");

        var laufend = _spieler.Laufend;
        var stueck = laufend.Hergestellt.GetValueOrDefault(_betrieb.Produkt);
        _diesesJahr.Text = $"Dieses Jahr: {stueck} {_betrieb.Produkt} · Umsatz {Zahlenformat.Ganz(laufend.Umsatz)}";

        _vorjahr.Text = _spieler.Vorjahr is { } v
            ? $"Jahresrechnung {v.Jahr}: Gewinn {Zahlenformat.MitVorzeichen(v.Gewinn)} {waehrung}"
            : "Jahresrechnung: erst nach dem ersten Jahr";

        _rivaleZeile.Text = _rivale is null ? "" : $"Rivale {_rivale.Inhaber}: {Zahlenformat.Ganz(_rivale.Vermoegen)} {waehrung}";

        foreach (var (ware, label) in _preisLabels)
            label.Text = Zahlenformat.Kommazahl(w.Markt[ware].Preis);
    }
}
