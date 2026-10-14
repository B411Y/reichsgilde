using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Der Jahresbericht in der Mitte des Bildschirms – nach jeder Jahresabrechnung und am Ende der Partie.
///
/// Zeigt: die Jahresrechnung des Spielers (Posten für Posten), sein Vermögen und etwaige Schulden,
/// den Fortschritt zum nächsten Stand und die Rangliste aller Häuser.
/// Reine Anzeige: Alle Zahlen stammen aus dem Simulationskern.
/// </summary>
public partial class JahresberichtAnzeige : PanelContainer
{
    private static readonly Color Grau = new(0.72f, 0.72f, 0.72f);
    private static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);

    private VBoxContainer _inhalt = null!;
    private Button _weiter = null!;

    /// <summary>Der Spieler hat den Bericht geschlossen.</summary>
    public event Action? Geschlossen;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(560, 0);
        // Deckender Hintergrund: Das Standard-Panel ist halb durchsichtig, dann schimmern die Stadt
        // und ihre Beschriftungen durch den Bericht.
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.11f, 0.10f, 1f),
            BorderColor = Gold * new Color(1, 1, 1, 0.8f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ShadowColor = new Color(0, 0, 0, 0.5f), ShadowSize = 12,
        });
        var rand = new MarginContainer();
        foreach (var seite in new[] { "left", "right", "top", "bottom" })
            rand.AddThemeConstantOverride($"margin_{seite}", 24);
        AddChild(rand);

        var spalte = new VBoxContainer();
        spalte.AddThemeConstantOverride("separation", 10);
        rand.AddChild(spalte);

        _inhalt = new VBoxContainer();
        _inhalt.AddThemeConstantOverride("separation", 8);
        spalte.AddChild(_inhalt);

        _weiter = new Button { Text = "Weiter", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(160, 0), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        _weiter.Pressed += () => { Hide(); Geschlossen?.Invoke(); };
        spalte.AddChild(_weiter);
        Hide();
    }

    /// <summary>Baut den Bericht für das abgeschlossene Jahr auf und zeigt ihn.</summary>
    /// <param name="endgrund">Gesetzt, wenn die Partie vorbei ist – dann wird es der Abschlussbericht.</param>
    public void Zeigen(Simulation simulation, Konto spieler, string? endgrund = null)
    {
        if (simulation.Wirtschaft is not { } w) return;
        UiHilfe.KinderEntfernen(_inhalt);
        var waehrung = w.Daten.Allgemein.Waehrung;
        var bilanz = spieler.Vorjahr;

        // ── Überschrift ──
        var titel = endgrund is null
            ? bilanz is null ? "Jahresrechnung" : $"Jahresrechnung {bilanz.Jahr}"
            : spieler.IstBankrott ? "Bankrott – die Partie ist verloren"
            : spieler.IstErloschen ? "Das Haus ist erloschen – die Partie ist verloren"
            : "Die Partie ist zu Ende";
        Text(titel, 24, endgrund is not null && !spieler.IstAktiv ? Rot : Gold, zentriert: true);
        if (endgrund is not null)
            Text(endgrund, 15, Grau, zentriert: true);
        Text($"{spieler.Inhaber} · {w.StandVon(spieler).Name}", 16, null, zentriert: true);

        // ── Jahresrechnung ──
        if (bilanz is not null)
        {
            _inhalt.AddChild(new HSeparator());
            var raster = Raster(2);
            Zeile(raster, "Umsatz", Zahlenformat.Ganz(bilanz.Umsatz));
            Zeile(raster, "− Zutaten", Zahlenformat.Ganz(bilanz.Zutaten));
            Zeile(raster, "− Löhne", Zahlenformat.Ganz(bilanz.Personal));
            Zeile(raster, "− Unterhalt und Pacht", Zahlenformat.Ganz(bilanz.Fixkosten));
            Zeile(raster, "− Steuern", Zahlenformat.Ganz(bilanz.Steuer));
            if (bilanz.Transport > 0) Zeile(raster, "− Eskorten", Zahlenformat.Ganz(bilanz.Transport));
            if (bilanz.Schaden > 0) Zeile(raster, "− Schäden", Zahlenformat.Ganz(bilanz.Schaden));
            Zeile(raster, "= Gewinn", $"{Zahlenformat.MitVorzeichen(bilanz.Gewinn)} {waehrung}", bilanz.Gewinn >= 0 ? Gruen : Rot, 17);

            var nebenbei = new List<string>();
            if (bilanz.Investitionen > 0) nebenbei.Add($"Anschaffungen {Zahlenformat.Ganz(bilanz.Investitionen)}");
            if (bilanz.Standesgebuehren > 0) nebenbei.Add($"Standesgebühren {Zahlenformat.Ganz(bilanz.Standesgebuehren)}");
            if (bilanz.Amtseinkommen > 0) nebenbei.Add($"Amtseinkommen +{Zahlenformat.Ganz(bilanz.Amtseinkommen)}");
            if (bilanz.Geschenke > 0) nebenbei.Add($"Geschenke an Ratsherren {Zahlenformat.Ganz(bilanz.Geschenke)}");
            if (bilanz.Familie > 0) nebenbei.Add($"Haushalt, Brautwerbung und Hochzeit {Zahlenformat.Ganz(bilanz.Familie)}");
            if (bilanz.Raubwert > 0) nebenbei.Add($"von Banditen geraubt: Waren für {Zahlenformat.Ganz(bilanz.Raubwert)}");
            if (nebenbei.Count > 0) Text("Außerdem: " + string.Join(" · ", nebenbei), 13, Grau);
        }

        // ── Vermögen, Schulden, Stand ──
        _inhalt.AddChild(new HSeparator());
        Text($"Vermögen: {Zahlenformat.Ganz(spieler.Vermoegen)} {waehrung}", 18, spieler.Vermoegen >= 0 ? null : Rot);
        if (spieler.Schuldenjahre > 0 && !spieler.IstBankrott)
            {
            var rest = w.Daten.Schulden.JahreBisBankrott - spieler.Schuldenjahre;
            Text($"⚠ Ihr steht in der Kreide! Noch {rest} {(rest == 1 ? "Jahresabschluss" : "Jahresabschlüsse")} im Minus, dann droht der Bankrott.", 14, Rot);
        }

        if (w.NaechsterStand(spieler) is { } naechster)
        {
            var balken = new ProgressBar
            {
                MinValue = 0, MaxValue = naechster.Schwelle, Value = Math.Max(0, spieler.Vermoegen),
                ShowPercentage = false, CustomMinimumSize = new Vector2(0, 12),
            };
            Text($"Nächster Stand: {naechster.Name} ab {Zahlenformat.Ganz(naechster.Schwelle)} {waehrung}", 14);
            _inhalt.AddChild(balken);
            if (spieler.Vermoegen >= naechster.Schwelle)
                Text($"Ihr könnt im Rathaus aufsteigen (Gebühr {Zahlenformat.Ganz(naechster.Gebuehr)} {waehrung}).", 14, Gold);
        }

        // ── Familie: zuerst das eigene Haus, dann was man in der Stadt über die anderen hört ──
        var familie = w.Familien.LetztesJahr.OrderBy(e => e.Konto == spieler ? 0 : 1).Select(e => (e.Konto == spieler, e.Text)).ToList();
        if (familie.Count > 0)
        {
            _inhalt.AddChild(new HSeparator());
            Text("Familie", 16);
            foreach (var (eigenes, eintrag) in familie.Take(6))
                Text("• " + eintrag, 13, eigenes ? null : Grau);
        }

        // ── Ratssitzung ──
        if (w.Rat.LetzteSitzung.Count > 0 && endgrund is null)
        {
            _inhalt.AddChild(new HSeparator());
            Text("Ratssitzung", 16);
            foreach (var eintrag in w.Rat.LetzteSitzung)
                Text("• " + eintrag, 13);
        }

        // ── Rangliste ──
        _inhalt.AddChild(new HSeparator());
        Text("Rangliste der Häuser", 16);
        var rang = Raster(4);
        foreach (var kopf in new[] { "Rang", "Haus", "Stand", "Vermögen" })
            Label(rang, kopf, 13, Grau);
        var platz = 1;
        foreach (var konto in w.Rangliste)
        {
            var farbe = konto == spieler ? Gold : (Color?)null;
            Label(rang, !konto.IstAktiv ? "–" : $"{platz++}.", 14, farbe);
            Label(rang, konto.Inhaber, 14, farbe);
            Label(rang, konto.IstBankrott ? "bankrott" : konto.IstErloschen ? "erloschen" : w.StandVon(konto).Name, 14, !konto.IstAktiv ? Rot : farbe);
            Label(rang, Zahlenformat.Ganz(konto.Vermoegen), 14, farbe);
        }

        _weiter.Text = endgrund is null ? "Weiter" : "Schließen";
        Show();

    }

    // ── Bausteine ───────────────────────────────────────────────────────────

    private GridContainer Raster(int spalten)
    {
        var raster = new GridContainer { Columns = spalten };
        raster.AddThemeConstantOverride("h_separation", 28);
        raster.AddThemeConstantOverride("v_separation", 3);
        _inhalt.AddChild(raster);
        return raster;
    }

    private static void Zeile(GridContainer raster, string posten, string betrag, Color? farbe = null, int groesse = 15)
    {
        Label(raster, posten, groesse, farbe);
        var rechts = Label(raster, betrag, groesse, farbe);
        rechts.HorizontalAlignment = HorizontalAlignment.Right;
        rechts.SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    private void Text(string text, int groesse, Color? farbe = null, bool zentriert = false)
    {
        var label = Label(_inhalt, text, groesse, farbe);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        if (zentriert) label.HorizontalAlignment = HorizontalAlignment.Center;
    }

    private static Label Label(Container eltern, string text, int groesse, Color? farbe = null)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", groesse);
        if (farbe is { } f) label.AddThemeColorOverride("font_color", f);
        eltern.AddChild(label);
        return label;
    }
}
