using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Rückfrage vor dem Verkauf eines Betriebs (bzw. vor der Kündigung einer Umlandpacht).
///
/// Das Fenster rechnet selbst nichts: Alle Zahlen kommen aus <see cref="Stadtwirtschaft.VerkaufVorschau"/> –
/// dieselbe Rechnung, nach der der Kern beim Verkauf bucht. Erst „Verkaufen“ schickt den Befehl
/// <see cref="BetriebVerkaufen"/>. Liegt noch Ware im Lager, steht oben eine Warnung: Sie geht mit Abschlag weg.
///
/// Solange das Fenster offen ist, läuft die Zeit weiter (Löhne bis heute, Marktpreise) – darum wird die
/// Vorschau zweimal je Sekunde neu geholt. Nur wenn sich eine angezeigte Zahl ändert, wird der Inhalt neu gebaut.
/// </summary>
public partial class VerkaufAnzeige : PanelContainer
{
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.72f, 0.72f, 0.72f);
    private static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);
    private static readonly Color Orange = new(1f, 0.66f, 0.3f);
    private static readonly Color Hell = new(0.92f, 0.88f, 0.80f);
    private const int Breite = 480;

    private Simulation? _simulation;
    private Konto? _spieler;
    private Betrieb? _betrieb;
    private VBoxContainer _inhalt = null!;
    private Button _verkaufKnopf = null!;
    private string _bauplan = "";
    private double _seitAktualisierung;

    /// <summary>Ergebnis des Verkaufs (Erfolg oder Ablehnung) – für das Protokoll.</summary>
    public event Action<string>? Gemeldet;

    /// <summary>Für welchen Betrieb das Fenster gerade offen ist (sonst null).</summary>
    public Betrieb? Betrieb => Visible ? _betrieb : null;

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.10f, 0.08f, 1f),
            BorderColor = new Color(0.62f, 0.50f, 0.32f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ShadowColor = new Color(0, 0, 0, 0.5f), ShadowSize = 12,
        });
        var rand = new MarginContainer();
        foreach (var seite in new[] { "left", "right", "top", "bottom" })
            rand.AddThemeConstantOverride($"margin_{seite}", 22);
        AddChild(rand);
        var spalte = new VBoxContainer { CustomMinimumSize = new Vector2(Breite, 0) };
        spalte.AddThemeConstantOverride("separation", 12);
        rand.AddChild(spalte);
        _inhalt = new VBoxContainer();
        _inhalt.AddThemeConstantOverride("separation", 8);
        spalte.AddChild(_inhalt);

        // Die Knöpfe stehen außerhalb des Inhalts: Der wird neu gebaut, die Knöpfe bleiben unter der Maus.
        var knoepfe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        knoepfe.AddThemeConstantOverride("separation", 16);
        spalte.AddChild(knoepfe);
        _verkaufKnopf = Hauptmenue.Knopf(knoepfe, "Verkaufen", Bestaetigen);
        Hauptmenue.Knopf(knoepfe, "Abbrechen", Hide);
        Rollbereich.Einhuellen(this);
        Hide();
    }

    public void Verbinden(Simulation simulation, Konto spieler)
    {
        _simulation = simulation;
        _spieler = spieler;
    }

    /// <summary>Öffnet die Rückfrage für diesen eigenen Betrieb.</summary>
    public void Oeffnen(Betrieb betrieb)
    {
        _betrieb = betrieb;
        _bauplan = ""; // erzwingt den Neubau
        Aufbauen();
        Show();
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _seitAktualisierung += delta;
        if (_seitAktualisierung < 0.5) return;
        _seitAktualisierung = 0;
        Aufbauen();
    }

    private void Bestaetigen()
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null || _betrieb is null) return;
        var ergebnis = w.Ausfuehren(_spieler, new BetriebVerkaufen(_betrieb.Name));
        Gemeldet?.Invoke(ergebnis.Erfolg ? ergebnis.Meldung : "✗ " + ergebnis.Meldung);
        if (ergebnis.Erfolg) Hide();
        else Aufbauen();
    }

    /// <summary>Holt die Vorschau aus dem Kern und baut den Inhalt neu – aber nur, wenn sich etwas Sichtbares geändert hat.</summary>
    private void Aufbauen()
    {
        if (_simulation?.Wirtschaft is not { } w || _betrieb is not { } b) return;
        if (!w.Betriebe.Contains(b)) { Hide(); return; } // inzwischen verkauft
        var v = w.VerkaufVorschau(b);
        var bauplan = string.Join("|", Zahlenformat.Ganz(v.Summe), Zahlenformat.Ganz(v.Abrechnung), Zahlenformat.Ganz(v.Warenerloes),
            Zahlenformat.Ganz(v.Kasse), v.Verboten, string.Join(",", v.Lagerware.Select(x => $"{x.Ware}:{x.Stueck}")),
            string.Join(",", v.Personal.Select(x => $"{x.Key}:{x.Value}")), v.Karren.Count, v.Waechter);
        if (bauplan == _bauplan) return;
        _bauplan = bauplan;

        UiHilfe.KinderEntfernen(_inhalt);
        var d = w.Daten.Verkauf;
        var waehrung = w.Daten.Allgemein.Waehrung;
        var abschlag = $"Marktwert −{Zahlenformat.Ganz(d.WarenAbschlag * 100)} %";

        Hauptmenue.Text(_inhalt, v.IstPacht ? $"Pacht kündigen: {v.Betrieb}" : $"{v.Betrieb} verkaufen", 22, Gold, zentriert: true);
        Hauptmenue.Text(_inhalt, v.IstPacht
            ? $"{b.Artdaten.Name} im Umland. Die Pacht wird gekündigt – für den Betrieb selbst gibt es nichts. " +
              "Danach kann jedes Haus ihn wieder pachten."
            : $"Stufe {v.Stufe}: {b.Artdaten.Name}. Voller Wert {Zahlenformat.Ganz(v.Wert)} {waehrung} (Kaufpreis der Stufe 1 und alle Ausbauten) – " +
              $"ihr bekommt {Zahlenformat.Ganz(d.Anteil * 100)} %. Danach steht das Haus mit seiner Stufe zum vollen Wert zum Verkauf.",
            13, Grau, umbruchBreite: Breite);

        // Warnung: Ware im Lager geht mit Abschlag weg – gut sichtbar, denn das lässt sich oft besser am Markt verkaufen.
        if (v.Lagerware.Count > 0)
        {
            var (kasten, spalte) = Kasten(Orange);
            _inhalt.AddChild(kasten);
            Hauptmenue.Text(spalte, $"⚠ Im Lager liegen noch Waren – sie werden zum {abschlag} mitverkauft:", 14, Orange, umbruchBreite: Breite - 24);
            var raster = Raster(spalte);
            foreach (var posten in v.Lagerware.OrderByDescending(x => x.Wert))
                Zeile(raster, $"{posten.Stueck} {posten.Ware}", $"{Zahlenformat.Ganz(posten.Wert)} {waehrung}", Hell, 13);
            Hauptmenue.Text(spalte, "Wer mehr will, verkauft die Ware vorher selbst am Markt.", 12, Grau, umbruchBreite: Breite - 24);
        }

        // Die Rechnung
        var rechnung = Raster(_inhalt);
        if (!v.IstPacht)
            Zeile(rechnung, $"Erlös für das Haus ({Zahlenformat.Ganz(d.Anteil * 100)} % des Werts)", "+" + Zahlenformat.Ganz(v.Gebaeudeerloes));
        Zeile(rechnung, $"Lagerware ({abschlag})", v.Warenerloes > 0 ? "+" + Zahlenformat.Ganz(v.Warenerloes) : "0");
        Zeile(rechnung, v.IstPacht ? "Löhne und Pacht bis heute" : "Löhne und Unterhalt bis heute", v.Abrechnung > 0.5 ? "−" + Zahlenformat.Ganz(v.Abrechnung) : "0");
        Zeile(rechnung, "= Erlös", $"{Zahlenformat.MitVorzeichen(v.Summe)} {waehrung}", v.Summe >= 0 ? Gruen : Rot, 17);
        if (v.Kasse > 0.5)
            Zeile(rechnung, "Kasse der Geldleihe zurück ins Bargeld", $"+{Zahlenformat.Ganz(v.Kasse)} {waehrung}", Hell, 13);
        Hauptmenue.Text(_inhalt, "Der Erlös zählt nicht zum Gewinn – im Jahresbericht steht er unter „Außerdem“ als Verkäufe.", 12, Grau, umbruchBreite: Breite);

        // Was mit dem Betrieb weggeht
        var mit = new List<string>();
        if (v.Personal.Count > 0) mit.Add(string.Join(", ", v.Personal.Select(x => $"{x.Value}× {x.Key}")));
        if (v.Waechter) mit.Add("der Wächter");
        if (v.Karren.Count > 0) mit.Add(string.Join(", ", v.Karren));
        Hauptmenue.Text(_inhalt, mit.Count == 0 ? "Personal, Wächter oder Karren hat der Betrieb nicht."
            : $"Mit dem Betrieb gehen: {string.Join(" · ", mit)}.", 13, Hell, umbruchBreite: Breite);

        if (v.Verboten is { } grund)
            Hauptmenue.Text(_inhalt, $"✗ {grund}", 14, Rot, umbruchBreite: Breite);

        var knopf = v.IstPacht ? "Pacht kündigen" : "Verkaufen";
        if (_verkaufKnopf.Text != knopf) _verkaufKnopf.Text = knopf;
        _verkaufKnopf.Disabled = v.Verboten is not null || _spieler?.IstAktiv != true;
        _verkaufKnopf.TooltipText = v.Verboten ?? "";
    }

    // ── Bausteine ───────────────────────────────────────────────────────────

    /// <summary>Ein Kasten mit farbigem Rand – für die Warnung zur Lagerware.</summary>
    private static (PanelContainer Kasten, VBoxContainer Spalte) Kasten(Color rand)
    {
        var kasten = new PanelContainer();
        kasten.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.22f, 0.14f, 0.08f), BorderColor = rand,
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 8, ContentMarginBottom = 8,
        });
        var spalte = new VBoxContainer();
        spalte.AddThemeConstantOverride("separation", 4);
        kasten.AddChild(spalte);
        return (kasten, spalte);
    }

    private static GridContainer Raster(Container eltern)
    {
        var raster = new GridContainer { Columns = 2 };
        raster.AddThemeConstantOverride("h_separation", 24);
        raster.AddThemeConstantOverride("v_separation", 4);
        eltern.AddChild(raster);
        return raster;
    }

    /// <summary>Eine Zeile der Rechnung: Text links, Betrag rechtsbündig.</summary>
    private static void Zeile(GridContainer raster, string text, string betrag, Color? farbe = null, int groesse = 14)
    {
        var links = Hauptmenue.Text(raster, text, groesse, Hell);
        links.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var rechts = Hauptmenue.Text(raster, betrag, groesse, farbe ?? Hell);
        rechts.HorizontalAlignment = HorizontalAlignment.Right;
    }
}
