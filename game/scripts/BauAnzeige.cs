using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Der Baumodus: Tafel rechts mit der Auswahl des Gebäudes (Wohnhaus-Stufen, Werkstatt-Berufe) und – sobald eines gewählt ist –
/// dem Stand der Platzwahl. Die Bauzonen der Karte erscheinen dabei in der Stadt (<see cref="Stadtansicht.Baumodus"/>),
/// und ein Umriss folgt der Maus (<see cref="Stadtansicht.BauPlatzieren"/>): grün = hier erlaubt, rot = nicht (mit Grund),
/// R dreht, Klick setzt. Danach fragt <see cref="BauRueckfrage"/> mit Preis und Bauzeit nach.
///
/// Die Tafel rechnet nichts selbst: Preise und Bauzeit kommen aus <see cref="Bauwesen.Vorschau"/>, was ein Haus nicht darf
/// (Stand, Handwerk, Geld) aus <see cref="Bauwesen.BauherrVerboten"/> – solche Gebäude sind ausgegraut und nennen den Grund.
/// </summary>
public partial class BauAnzeige : PanelContainer
{
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.72f, 0.72f, 0.72f);
    private static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);
    private static readonly Color Hell = new(0.92f, 0.88f, 0.80f);
    private const int Breite = 380;

    private Simulation? _simulation;
    private Konto? _spieler;
    private Stadtansicht? _stadt;
    private VBoxContainer _inhalt = null!;
    private string _bauplan = "";
    private double _seitAktualisierung;

    /// <summary>Das Gebäude, dessen Platz gerade gewählt wird (null: die Liste ist offen).</summary>
    private (string Name, Func<double, double, int, BauBefehl> Erzeuger, BauBefehl Beispiel)? _gewaehlt;

    /// <summary>Der Baumodus wurde geschlossen (Knopf, Esc) – die Hauptszene stellt die Stadt wieder normal.</summary>
    public event Action? Geschlossen;

    public bool Aktiv => Visible;

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.15f, 0.13f, 0.97f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
        });
        var rand = new MarginContainer();
        foreach (var seite in new[] { "left", "right", "top", "bottom" })
            rand.AddThemeConstantOverride($"margin_{seite}", 16);
        AddChild(rand);
        _inhalt = new VBoxContainer { CustomMinimumSize = new Vector2(Breite, 0) };
        _inhalt.AddThemeConstantOverride("separation", 6);
        rand.AddChild(_inhalt);
        Rollbereich.Einhuellen(this);
        Hide();
    }

    public void Verbinden(Simulation simulation, Konto spieler, Stadtansicht stadt)
    {
        _simulation = simulation;
        _spieler = spieler;
        _stadt = stadt;
    }

    /// <summary>Öffnet den Baumodus mit der Liste der Gebäude.</summary>
    public void Oeffnen()
    {
        if (_stadt is null) return;
        _stadt.Baumodus = true;
        _gewaehlt = null;
        Aufbauen(erzwingen: true);
        Show();
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight, LayoutPresetMode.Minsize, 16);
        GrowHorizontal = GrowDirection.Begin; // wächst nach links ins Bild
    }

    /// <summary>Schließt den Baumodus ganz: Bauzonen und Umriss verschwinden.</summary>
    public void Schliessen()
    {
        if (_stadt is not null) _stadt.Baumodus = false;
        _gewaehlt = null;
        Hide();
        Geschlossen?.Invoke();
    }

    /// <summary>Ein Schritt zurück (Esc, Rechtsklick): von der Platzwahl zur Liste, von der Liste ganz hinaus.</summary>
    public void Zurueck()
    {
        if (_gewaehlt is null) { Schliessen(); return; }
        _gewaehlt = null;
        _stadt?.BauPlatzieren(null);
        Aufbauen(erzwingen: true);
    }

    /// <summary>Wählt ein Gebäude (auch für die Startoption <c>--bauen=wohnhaus:1</c>) – der Umriss folgt ab jetzt der Maus.</summary>
    public void Waehlen(string name, Func<double, double, int, BauBefehl> erzeuger)
    {
        if (_stadt is null) return;
        _gewaehlt = (name, erzeuger, erzeuger(0, 0, 0));
        _stadt.BauPlatzieren(erzeuger);
        Aufbauen(erzwingen: true);
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _seitAktualisierung += delta;
        if (_seitAktualisierung < 0.25) return;
        _seitAktualisierung = 0;
        Aufbauen(erzwingen: false);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Baut die Tafel neu – aber nur, wenn sich etwas Sichtbares geändert hat (sonst springen die Knöpfe unter der Maus weg).</summary>
    private void Aufbauen(bool erzwingen)
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null || _stadt is null || !w.Bauen.IstAktiv) return;
        var plan = _gewaehlt is { } g ? PlatzwahlPlan(w, g) : ListenPlan(w);
        if (!erzwingen && plan == _bauplan) return;
        _bauplan = plan;
        UiHilfe.KinderEntfernen(_inhalt);
        if (_gewaehlt is { } gewaehlt) PlatzwahlZeigen(w, gewaehlt);
        else ListeZeigen(w);
    }

    private string ListenPlan(Stadtwirtschaft w) =>
        "Liste|" + Zahlenformat.Ganz(_spieler!.Vermoegen) + "|" + string.Join("|", Gebaeude(w).Select(x => x.Verboten + Zahlenformat.Ganz(x.Vorschau.Kosten)));

    private string PlatzwahlPlan(Stadtwirtschaft w, (string Name, Func<double, double, int, BauBefehl> Erzeuger, BauBefehl Beispiel) g) =>
        $"Platz|{g.Name}|{_stadt!.BauGrund}|{_stadt.BauAuftrag is not null}|{_stadt.BauDrehung}|{Zahlenformat.Ganz(_spieler!.Vermoegen)}";

    /// <summary>Was gebaut werden kann: Wohnhaus-Stufen und Werkstätten der Stadt (Stufe 1), mit Vorschau und dem Grund, warum es nicht geht.</summary>
    private List<(string Gruppe, string Name, string Beruf, Func<double, double, int, BauBefehl> Erzeuger, Bauvorschau Vorschau, string? Verboten)> Gebaeude(Stadtwirtschaft w)
    {
        var liste = new List<(string, string, string, Func<double, double, int, BauBefehl>, Bauvorschau, string?)>();
        var stufen = w.Wohnen.Daten?.Stufen ?? new();
        for (var i = 0; i < stufen.Count; i++)
        {
            var stufe = i;
            BauBefehl Erzeuger(double x, double z, int d) => new WohnhausBauen(stufe, x, z, d);
            liste.Add(("Wohnhaus", stufen[i].Name, "", Erzeuger, w.Bauen.Vorschau(_spieler!, Erzeuger(0, 0, 0)), w.Bauen.BauherrVerboten(_spieler!, Erzeuger(0, 0, 0))));
        }
        foreach (var art in Enum.GetValues<Betriebsart>())
        {
            if (!w.Daten.KenntArt(art) || w.Daten.Art(art) is not { InDerStadt: true, Stufe: 1 } daten) continue;
            var a = art;
            BauBefehl Erzeuger(double x, double z, int d) => new WerkstattBauen(a, x, z, d);
            liste.Add(("Werkstatt", daten.Name, daten.Beruf, Erzeuger, w.Bauen.Vorschau(_spieler!, Erzeuger(0, 0, 0)), w.Bauen.BauherrVerboten(_spieler!, Erzeuger(0, 0, 0))));
        }
        return liste;
    }

    private void ListeZeigen(Stadtwirtschaft w)
    {
        var waehrung = w.Daten.Allgemein.Waehrung;
        Hauptmenue.Text(_inhalt, "Bauen", 22, Gold, zentriert: true);
        Hauptmenue.Text(_inhalt, $"Vermögen: {Zahlenformat.Ganz(_spieler!.Vermoegen)} {waehrung}", 15, Hell);
        Hauptmenue.Text(_inhalt, "Wählt ein Gebäude, dann zeigt ein Umriss an der Maus, wo es steht. Gebaut wird nur in den farbigen Bauzonen und auf Bauplätzen – am Raster von 2 m, mit der Vorderseite an einer Straße.",
            13, Grau, umbruchBreite: Breite);

        // Legende: die Farben der Bauzonen in der Stadt
        var legende = new HBoxContainer();
        legende.AddThemeConstantOverride("separation", 14);
        _inhalt.AddChild(legende);
        foreach (var lage in new[] { Lage.Kern, Lage.Mauerring, Lage.Vorstadt })
        {
            var punkt = new ColorRect { Color = Stadtansicht.LageFarbe(lage), CustomMinimumSize = new Vector2(14, 14), SizeFlagsVertical = SizeFlags.ShrinkCenter };
            legende.AddChild(punkt);
            Hauptmenue.Text(legende, Stadtansicht.LageName(lage), 13, Hell);
        }

        string? gruppe = null;
        foreach (var (g, name, beruf, erzeuger, vorschau, verboten) in Gebaeude(w))
        {
            if (g != gruppe)
            {
                gruppe = g;
                Hauptmenue.Text(_inhalt, g == "Wohnhaus" ? "Wohnhaus (das alte wird angerechnet)" : "Werkstatt (Stufe 1)", 16, Gold);
            }
            var kosten = $"{Zahlenformat.Ganz(vorschau.Kosten)} {waehrung}";
            var mass = $"{Zahlenformat.Ganz(vorschau.Grundriss.Breite)} × {Zahlenformat.Ganz(vorschau.Grundriss.Tiefe)} m";
            var knopf = new Button
            {
                Text = $"{name}{(beruf != "" ? $" ({beruf})" : "")}\n{kosten} · {mass} · {BauzeitText(vorschau.BauzeitMinuten)}",
                FocusMode = FocusModeEnum.None,
                Disabled = verboten is not null,
                TooltipText = verboten ?? "",
                Alignment = HorizontalAlignment.Left,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            knopf.AddThemeFontSizeOverride("font_size", 14);
            knopf.Pressed += () => Waehlen($"{name}{(beruf != "" ? $" ({beruf})" : "")}", erzeuger);
            _inhalt.AddChild(knopf);
            if (verboten is not null)
                Hauptmenue.Text(_inhalt, "✗ " + verboten, 12, Rot, umbruchBreite: Breite);
        }
        Hauptmenue.Knopf(_inhalt, "Schließen (Esc)", Schliessen).SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    private void PlatzwahlZeigen(Stadtwirtschaft w, (string Name, Func<double, double, int, BauBefehl> Erzeuger, BauBefehl Beispiel) g)
    {
        var vorschau = w.Bauen.Vorschau(_spieler!, g.Beispiel);
        var waehrung = w.Daten.Allgemein.Waehrung;
        Hauptmenue.Text(_inhalt, "Platz wählen", 22, Gold, zentriert: true);
        Hauptmenue.Text(_inhalt, g.Name, 17, Hell, zentriert: true);
        Hauptmenue.Text(_inhalt,
            $"Fläche {Zahlenformat.Ganz(vorschau.Grundriss.Breite)} × {Zahlenformat.Ganz(vorschau.Grundriss.Tiefe)} m (mit 2 m Vorplatz) · Bauzeit {BauzeitText(vorschau.BauzeitMinuten)} · " +
            $"Kosten {Zahlenformat.Ganz(vorschau.Kosten)} {waehrung}", 14, Grau, umbruchBreite: Breite);

        // Zustand des Umrisses: grün oder rot mit Grund
        if (_stadt!.BauAuftrag is null)
            Hauptmenue.Text(_inhalt, "Bewegt die Maus über die Stadt.", 15, Grau, umbruchBreite: Breite);
        else if (_stadt.BauGrund is { } grund)
            Hauptmenue.Text(_inhalt, "✗ " + grund, 15, Rot, umbruchBreite: Breite);
        else
            Hauptmenue.Text(_inhalt, "✔ Hier kann gebaut werden – Klick setzt das Gebäude.", 15, Gruen, umbruchBreite: Breite);
        Hauptmenue.Text(_inhalt, "R dreht das Gebäude (Eingang zur Straße) · Q/E drehen die Kamera · Rechtsklick oder Esc: zurück zur Auswahl",
            13, Grau, umbruchBreite: Breite);

        var knoepfe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        knoepfe.AddThemeConstantOverride("separation", 10);
        _inhalt.AddChild(knoepfe);
        var drehen = Hauptmenue.Knopf(knoepfe, "Drehen (R)", () => _stadt.BauDrehung = (_stadt.BauDrehung + 1) % 4);
        drehen.CustomMinimumSize = new Vector2(120, 36);
        var zurueck = Hauptmenue.Knopf(knoepfe, "Zurück", Zurueck);
        zurueck.CustomMinimumSize = new Vector2(120, 36);
    }

    /// <summary>„4 Stunden", „1 Spieljahr", „2 Spieljahre" – ein Spieljahr hat 1.020 Spielminuten.</summary>
    public static string BauzeitText(int minuten)
    {
        if (minuten >= Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag)
        {
            var jahre = minuten / (double)Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag;
            return jahre == 1 ? "1 Spieljahr" : $"{jahre:0.#} Spieljahre";
        }
        return minuten % 60 == 0 ? $"{minuten / 60} Stunden" : $"{minuten} Minuten";
    }
}
