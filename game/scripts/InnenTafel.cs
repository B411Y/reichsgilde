using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Gemeinsame Grundlage der Tafeln, die der Spieler im Innenraum seiner Werkstatt öffnet (PR #152): Personal, Produktion, Lager,
/// Karren und Einrichten. Jede Tafel gehört zu einem Gegenstand im Raum (Personalbuch, Amboss, Regal, Hoftor, Werkzeugwand) und bedient
/// den Betrieb, in dem man steht.
///
/// Wichtig wie bei <see cref="BetriebsVerwaltung"/>: Die Tafel ändert NICHTS direkt. Jeder Knopf erzeugt einen Befehl und schickt ihn an
/// <see cref="Stadtwirtschaft.Ausfuehren"/>; die Antwort geht über <see cref="Gemeldet"/> ins Protokoll. Gesperrte Knöpfe nennen den Grund
/// aus den Verbots-Abfragen des Kerns.
///
/// Aufbau: Eine Tafel baut ihre Zeilen neu, sobald sich ihr „Stand" ändert (eine Zeichenkette aus allem, was sie zeigt) – höchstens fünfmal
/// in der Sekunde, damit ein Knopf unter der Maus nicht mitten im Klick verschwindet.
/// </summary>
public abstract class InnenTafel
{
    protected static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    protected static readonly Color Grau = new(0.72f, 0.72f, 0.72f);
    protected static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    protected static readonly Color Rot = new(0.95f, 0.45f, 0.4f);
    protected static readonly Color Hell = new(0.92f, 0.88f, 0.80f);

    /// <summary>Die Tafel selbst – die Hauptszene hängt sie in die Oberfläche.</summary>
    public PanelContainer Tafel { get; }
    protected VBoxContainer Inhalt { get; }
    protected Simulation? Sim { get; private set; }
    protected Konto? Spieler { get; private set; }
    protected Betrieb? Betrieb { get; private set; }

    /// <summary>Nummern der im Raum ausgewählten Arbeiter (setzt die Hauptszene aus der Stadtansicht).</summary>
    public Func<IReadOnlyCollection<int>> Auswahl { get; set; } = () => Array.Empty<int>();

    /// <summary>Ergebnis eines Befehls (Erfolg oder Ablehnung) – für das Protokoll.</summary>
    public event Action<string>? Gemeldet;

    /// <summary>Der Spieler will am Markt handeln (nur die Karrentafel).</summary>
    public event Action<Betrieb>? HandelGewuenscht;

    private string _stand = "";
    private double _seitAufbau;

    protected InnenTafel(int breite)
    {
        var (tafel, spalte) = Hauptmenue.Tafel(breite);
        Tafel = tafel;
        Inhalt = new VBoxContainer();
        Inhalt.AddThemeConstantOverride("separation", 6);
        spalte.AddChild(Inhalt);
        var schliessen = Hauptmenue.Knopf(spalte, "Schließen", Hide);
        schliessen.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        spalte.AddThemeConstantOverride("separation", 10);
        Fensterrahmen.Anmelden(Tafel, GetType().Name);
        Tafel.Hide();
    }

    public bool Visible => Tafel.Visible;
    public void Hide() => Tafel.Hide();

    public virtual void Verbinden(Simulation simulation, Konto spieler)
    {
        Sim = simulation;
        Spieler = spieler;
    }

    /// <summary>Öffnet die Tafel für diesen Betrieb.</summary>
    public void Oeffnen(Betrieb betrieb)
    {
        Betrieb = betrieb;
        _stand = "";
        _seitAufbau = 1;
        Tafel.Show();
        Aktualisieren(0);
    }

    /// <summary>Jedes Bild: Gibt es den Betrieb noch? Hat sich etwas geändert, wird neu aufgebaut.</summary>
    public virtual void Aktualisieren(double delta)
    {
        if (!Tafel.Visible || Betrieb is not { } b || Sim?.Wirtschaft is not { } w) return;
        if (!b.InDerStadt || !w.Betriebe.Contains(b))
        {
            Hide(); // verkauft
            return;
        }
        _seitAufbau += delta;
        var stand = Stand(w, b);
        if (stand == _stand || _seitAufbau < 0.2) return;
        _stand = stand;
        _seitAufbau = 0;
        Leeren();
        Aufbauen(w, b);
    }

    /// <summary>Räumt den Inhalt vor dem Neuaufbau weg (die Karrentafel behält ihr Bedienfeld).</summary>
    protected virtual void Leeren() => UiHilfe.KinderEntfernen(Inhalt);

    protected void Melden(string text) => Gemeldet?.Invoke(text);

    /// <summary>Alles, was die Tafel zeigt, als Zeichenkette – ändert sie sich, wird neu aufgebaut. Zahlen gerundet, damit es nicht jede Minute geschieht.</summary>
    protected abstract string Stand(Stadtwirtschaft w, Betrieb b);

    protected abstract void Aufbauen(Stadtwirtschaft w, Betrieb b);

    /// <summary>Schickt den Befehl an den Kern, meldet das Ergebnis und baut die Tafel gleich neu.</summary>
    protected void Befehlen(Befehl befehl)
    {
        UiHilfe.Befehlen(Sim, Spieler, befehl, Tafel, ergebnis =>
        {
            Gemeldet?.Invoke(UiHilfe.MeldungText(ergebnis));
            _stand = "";
            _seitAufbau = 1;
        });
    }

    protected void HandelMelden(Betrieb b) => HandelGewuenscht?.Invoke(b);

    // ── Bausteine ───────────────────────────────────────────────────────────

    protected static Label Text(Container eltern, string text, int groesse, Color? farbe = null, int umbruch = 0) =>
        Hauptmenue.Text(eltern, text, groesse, farbe, umbruchBreite: umbruch);

    /// <summary>Eine Überschrift mit goldener Schrift und, rechts, dem Namen des Betriebs.</summary>
    protected void Kopf(string titel, Betrieb b, string unter)
    {
        Hauptmenue.Text(Inhalt, titel, 24, Gold, zentriert: true);
        Hauptmenue.Text(Inhalt, $"{b.Name} · {b.Artdaten.Name}", 14, Hell, zentriert: true);
        if (unter != "") Hauptmenue.Text(Inhalt, unter, 12, Grau, zentriert: true);
        Inhalt.AddChild(new HSeparator());
    }

    /// <summary>Ein Raster mit Spalten, in dem Zeilen als Folge von Steuerelementen stehen.</summary>
    protected GridContainer Raster(int spalten, int abstandX = 14)
    {
        var raster = new GridContainer { Columns = spalten };
        raster.AddThemeConstantOverride("h_separation", abstandX);
        raster.AddThemeConstantOverride("v_separation", 6);
        Inhalt.AddChild(raster);
        return raster;
    }

    protected static Label Zelle(Container raster, string text, int groesse = 14, Color? farbe = null, float breite = 0, bool rechts = false)
    {
        var label = Hauptmenue.Text(raster, text, groesse, farbe ?? Hell);
        label.VerticalAlignment = VerticalAlignment.Center;
        if (breite > 0) label.CustomMinimumSize = new Vector2(breite, 0);
        if (rechts) label.HorizontalAlignment = HorizontalAlignment.Right;
        return label;
    }

    /// <summary>Hängt einen Tooltip an ein Steuerelement; Beschriftungen und Bilder fangen sonst keine Maus und zeigten nie einen (PR Interface-Feinschliff).</summary>
    protected static T Tipp<T>(T steuer, string text) where T : Control
    {
        steuer.TooltipText = text;
        if (steuer.MouseFilter == Control.MouseFilterEnum.Ignore) steuer.MouseFilter = Control.MouseFilterEnum.Pass;
        return steuer;
    }

    /// <summary>Gibt allen Zellen eines Rasters ab dem Kind <paramref name="ab"/> denselben Tooltip (eine Zeile der Liste); Knöpfe mit eigenem Hinweis behalten ihn.</summary>
    protected static void ZeileTipp(Container raster, int ab, string text)
    {
        for (var i = ab; i < raster.GetChildCount(); i++)
            if (raster.GetChild(i) is Control c && (c is not Button || c.TooltipText == "")) Tipp(c, text);
    }

    /// <summary>Tooltip einer Ware im Betrieb: Marktpreis, Lager, Zeit, Zutaten samt Kosten und Rohertrag je Stück (nur, was die Abfragen hergeben).</summary>
    protected static string WarenTipp(Stadtwirtschaft w, Betrieb b, string ware)
    {
        var waehrung = w.Daten.Allgemein.Waehrung;
        var zeilen = new List<string> { ware };
        double? preis = w.Daten.Waren.ContainsKey(ware) ? w.Markt[ware].Preis : null;
        if (preis is { } p) zeilen.Add($"Preis am Markt: {Zahlenformat.Kommazahl(p)} {waehrung}");
        zeilen.Add($"Im Lager: {b.ImLager(ware)} von {b.LagerKapazitaetJeWare}" + (b.HaeltZurueck(ware) ? " (zurückgehalten)" : ""));
        if (b.Artdaten.Produkte.Contains(ware))
            zeilen.Add($"Zeit je Stück: {Zahlenformat.Kommazahl(b.MinutenJeStueck(ware))} Min.");
        if (w.Daten.Waren.TryGetValue(ware, out var daten) && daten.Zutaten.Count > 0)
        {
            zeilen.Add("Zutaten je Stück: " + string.Join(", ", daten.Zutaten.Select(z => $"{z.Value} {z.Key}")));
            var kosten = daten.Zutaten.Sum(z => w.Markt[z.Key].Preis * z.Value);
            zeilen.Add($"Zutaten kosten am Markt {Zahlenformat.Kommazahl(kosten)} {waehrung}");
            if (preis is { } pr && !b.Artdaten.Produkte.Contains(ware)) zeilen.Add($"Ertrag je Stück (Preis minus Zutaten): {Zahlenformat.Kommazahl(pr - kosten)} {waehrung}");
        }
        if (b.Artdaten.Produkte.Contains(ware) && b.IstWerkstatt)
        {
            var r = Kalkulation.GewinnJeStueck(w, b, ware);
            string G(double x) => Zahlenformat.Kommazahl(x);
            zeilen.Add($"Gewinn je Stück: {G(r.Gewinn)} {waehrung}");
            zeilen.Add($"  Preis {G(r.Preis)} − Steuer {G(r.Steuer)}");
            zeilen.Add($"  − Zutaten {G(r.Zutaten)} − Lohn {G(r.Lohn)}");
            zeilen.Add($"  − Unterhalt {G(r.Unterhalt)}");
            if (b.Belegt == 0) zeilen.Add("  (ohne Personal: gerechnet mit einem Gesellen)");
        }
        return string.Join("\n", zeilen);
    }

    protected static Button Knopf(Container eltern, string text, Action aktion, string? verboten = null, int groesse = 13)
    {
        var knopf = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, Disabled = verboten is not null, TooltipText = verboten ?? "" };
        knopf.AddThemeFontSizeOverride("font_size", groesse);
        knopf.Pressed += aktion;
        eltern.AddChild(knopf);
        return knopf;
    }

    protected static HBoxContainer Reihe(Container eltern)
    {
        var reihe = new HBoxContainer();
        reihe.AddThemeConstantOverride("separation", 8);
        eltern.AddChild(reihe);
        return reihe;
    }

    protected static ProgressBar Balken(Container eltern, double wert, float breite, Color? farbe = null)
    {
        var balken = new ProgressBar
        {
            MaxValue = 1, Value = wert, ShowPercentage = false, CustomMinimumSize = new Vector2(breite, 12),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        balken.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = farbe ?? Gold });
        balken.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.2f, 0.17f, 0.13f) });
        eltern.AddChild(balken);
        return balken;
    }
}
