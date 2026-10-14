using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das Marktfenster: alle Waren als Kacheln mit Icon und Preis, nach Warengruppen geordnet – wie der Markt im
/// Original. Der Preis ist grün, wenn er über dem Grundpreis liegt (gut zum Verkaufen), rot darunter (gut zum Einkaufen).
/// Einzelheiten (Nachfrage, Zutaten, Gewinn je Minute, eigener Bestand) stehen im Tooltip.
///
/// Reine Anzeige: Gehandelt wird (noch) über die Karren der Betriebe.
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
        Hide();
    }

    public void Verbinden(Simulation simulation, Konto spieler)
    {
        _simulation = simulation;
        _spieler = spieler;
    }

    public void Oeffnen()
    {
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
                $"Von den Häusern verkauft im letzten Jahr: {Zahlenformat.Ganz(stand.SpielerAngebotImJahr)}",
            };
            if (daten.Zutaten.Count > 0)
                zeilen.Add($"Zutaten: {string.Join(", ", daten.Zutaten.Select(z => $"{z.Value} {z.Key}"))} · " +
                           $"Gewinn je Arbeitsminute {Zahlenformat.Kommazahl(Kalkulation.GewinnJeArbeitsminute(w, ware))} {waehrung}");
            if (w.Gastwirtschaft.IstAktiv && w.Gastwirtschaft.Daten.Ausschank.Contains(ware))
                zeilen.Add($"Wird in Schänken ausgeschenkt: {Zahlenformat.Kommazahl(w.Gastwirtschaft.Ausschankpreis(ware))} {waehrung} je Gast");
            if (bestand > 0)
                zeilen.Add($"In euren Lagern: {bestand}");
            var tipp = string.Join("\n", zeilen);
            if (kachel.TooltipText != tipp) kachel.TooltipText = tipp;
        }
    }
}
