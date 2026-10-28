using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Die Seite „Stadt wählen“ des Hauptmenüs – der erste Schritt einer neuen Partie, vor „Euer Oberhaupt“. Je Karte eine Kachel mit Vorschaubild
/// (<c>game/assets/oberflaeche/karten/&lt;kennung&gt;.png</c>, gerendert mit <c>tools/kartenbilder/kartenbilder.sh</c>; selbst gebaute Karten und
/// Karten ohne Bild zeigen einen Platzhalter), Name, Kurztext und Kennzahlen. Welche Karten erscheinen, sagt <see cref="Stadtkarten.Angebote"/>;
/// die letzte Wahl ist vorgewählt (<c>user://einstellungen.cfg</c>, Abschnitt „spiel“, Schlüssel „karte“). „Weiter“ setzt <see cref="Stadtkarten.Gewaehlt"/>,
/// die Hauptszene startet die Partie auf dieser Karte (derselbe Weg wie <c>--karte=</c>). Unten rechts im Menü schaltet ein Haken den
/// <see cref="Entwickler"/>-Modus um, der auch die Test- und Messkarten zeigt.
/// </summary>
public partial class Hauptmenue
{
    private const string BilderOrdner = "res://assets/oberflaeche/karten/";
    private static readonly Color KartenGrau = new(0.72f, 0.72f, 0.72f);

    private PanelContainer _kartenwahl = null!;
    private VBoxContainer _kartenKacheln = null!;
    private Button _kartenWeiter = null!;
    private Label _kartenLeer = null!;
    private CheckBox _entwicklerHaken = null!;
    private string? _kartenAuswahl;

    private PanelContainer KartenwahlTafel()
    {
        var (tafel, spalte) = Tafel(760);
        Text(spalte, "Stadt wählen", 26, Gold, zentriert: true);
        Text(spalte, "In welcher Stadt wollt Ihr Euer Handelshaus gründen?", 14, KartenGrau, zentriert: true);
        _kartenKacheln = new VBoxContainer();
        _kartenKacheln.AddThemeConstantOverride("separation", 10);
        spalte.AddChild(_kartenKacheln);
        _kartenLeer = Text(spalte, "Keine spielbare Karte gefunden.", 14, Rot, zentriert: true);

        var knoepfe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        knoepfe.AddThemeConstantOverride("separation", 12);
        spalte.AddChild(knoepfe);
        Knopf(knoepfe, "Zurück", () => Zeigen(_start));
        _kartenWeiter = Knopf(knoepfe, "Weiter", KarteUebernehmen);
        return tafel;
    }

    /// <summary>Der Haken „Entwickler-Optionen“ unten rechts im Bild (in einem Rand, der die Mausklicks durchlässt); nur auf der Starttafel zu sehen (<see cref="Zeigen"/>).</summary>
    private Control EntwicklerHaken()
    {
        _entwicklerHaken = new CheckBox
        {
            Text = "Entwickler-Optionen", ButtonPressed = Entwickler.Aktiv, FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Für die Arbeit am Spiel: zeigt in der Kartenwahl auch Test- und Messkarten (später weitere Hilfen).",
        };
        _entwicklerHaken.AddThemeFontSizeOverride("font_size", 14);
        // unauffällig, aber lesbar: 60 % Deckkraft, beim Darüberfahren voll
        _entwicklerHaken.Modulate = new Color(1, 1, 1, 0.6f);
        _entwicklerHaken.MouseEntered += () => _entwicklerHaken.Modulate = Colors.White;
        _entwicklerHaken.MouseExited += () => _entwicklerHaken.Modulate = new Color(1, 1, 1, 0.6f);
        _entwicklerHaken.Toggled += Entwickler.Setzen;
        _entwicklerHaken.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        _entwicklerHaken.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        var rand = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        rand.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var seite in new[] { "left", "right", "top", "bottom" })
            rand.AddThemeConstantOverride($"margin_{seite}", 12);
        rand.AddChild(_entwicklerHaken);
        return rand;
    }

    /// <summary>Öffnet die Seite: Kacheln neu aufbauen (der Entwickler-Modus oder eine Werkstatt-Karte kann sich geändert haben), die letzte Wahl vorwählen.</summary>
    private void KartenwahlZeigen()
    {
        UiHilfe.KinderEntfernen(_kartenKacheln);
        var angebote = Stadtkarten.Angebote();
        var letzte = LetzteKarte();
        _kartenAuswahl = angebote.Any(a => a.Kennung == letzte) ? letzte : angebote.FirstOrDefault()?.Kennung;
        var gruppe = new ButtonGroup();
        var raster = new GridContainer { Columns = 2 };
        raster.AddThemeConstantOverride("h_separation", 12);
        raster.AddThemeConstantOverride("v_separation", 12);
        _kartenKacheln.AddChild(raster);
        foreach (var angebot in angebote)
            raster.AddChild(Kachel(angebot, gruppe));
        _kartenLeer.Visible = angebote.Count == 0;
        _kartenWeiter.Disabled = angebote.Count == 0;
        Zeigen(_kartenwahl);
    }

    private Button Kachel(Stadtkarten.Angebot angebot, ButtonGroup gruppe)
    {
        var karte = angebot.Karte;
        var knopf = new Button
        {
            ToggleMode = true, ButtonGroup = gruppe, FocusMode = Control.FocusModeEnum.None, ButtonPressed = angebot.Kennung == _kartenAuswahl,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        knopf.Pressed += () => _kartenAuswahl = angebot.Kennung;
        var rand = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        rand.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var seite in new[] { "left", "right", "top", "bottom" })
            rand.AddThemeConstantOverride($"margin_{seite}", 8);
        knopf.AddChild(rand);
        var inhalt = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        inhalt.AddThemeConstantOverride("separation", 4);
        rand.AddChild(inhalt);

        inhalt.AddChild(Vorschau(angebot));
        Text(inhalt, karte.Stadt + (angebot.Testkarte ? "  (Testkarte)" : angebot.Eigene ? "  (selbst gebaut)" : ""), 19, Gold).MouseFilter = Control.MouseFilterEnum.Ignore;
        var kurz = Text(inhalt, karte.Beschreibung ?? "", 12, KartenGrau, umbruchBreite: 320);
        kurz.MouseFilter = Control.MouseFilterEnum.Ignore;
        kurz.Visible = !string.IsNullOrWhiteSpace(karte.Beschreibung);
        Text(inhalt, Kennzahlen(karte), 12, new Color(0.85f, 0.82f, 0.72f), umbruchBreite: 320).MouseFilter = Control.MouseFilterEnum.Ignore;
        // ein Knopf misst seine Kinder nicht selbst: feste Höhe für Bild, Name, drei Zeilen Kurztext und Kennzahlen
        knopf.CustomMinimumSize = new Vector2(350, 270);
        return knopf;
    }

    /// <summary>Das Vorschaubild der Karte – oder, wo es keins gibt, ein Platzhalter mit dem Namen.</summary>
    private static Control Vorschau(Stadtkarten.Angebot angebot)
    {
        var pfad = $"{BilderOrdner}{angebot.Kennung}.png";
        if (!angebot.Eigene && ResourceLoader.Exists(pfad))
            return new TextureRect
            {
                Texture = ResourceLoader.Load<Texture2D>(pfad), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                CustomMinimumSize = new Vector2(0, 150), MouseFilter = Control.MouseFilterEnum.Ignore,
            };
        var platz = new PanelContainer { CustomMinimumSize = new Vector2(0, 150), MouseFilter = Control.MouseFilterEnum.Ignore };
        platz.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.2f, 0.18f, 0.14f), BorderColor = Gold * new Color(1, 1, 1, 0.4f), BorderWidthBottom = 1, BorderWidthTop = 1, BorderWidthLeft = 1, BorderWidthRight = 1 });
        var name = Text(platz, angebot.Karte.Stadt, 22, new Color(0.5f, 0.46f, 0.38f), zentriert: true);
        name.VerticalAlignment = VerticalAlignment.Center;
        name.MouseFilter = Control.MouseFilterEnum.Ignore;
        return platz;
    }

    /// <summary>„2 Rivalen · 18 Werkstattplätze · Fluss · Hafen · 400 × 300 m“ – was man vor der Wahl wissen will.</summary>
    internal static string Kennzahlen(Stadtkarte karte)
    {
        var teile = new List<string>();
        var rivalen = karte.Startbelegung.Haeuser.Count;
        teile.Add(rivalen == 1 ? "1 Rivale" : $"{rivalen} Rivalen");
        var plaetze = karte.Gebaeude.Count(g => g.Art == Bausteinart.Werkstattplatz);
        teile.Add(plaetze == 1 ? "1 Werkstattplatz" : $"{plaetze} Werkstattplätze");
        if (karte.Wasser.Any(w => w.Art is "fluss" or "bach")) teile.Add("Fluss");
        if (karte.Haefen.Count > 0) teile.Add("Hafen");
        var g = karte.Kartengrenzen;
        teile.Add($"{g.Breite:0} × {g.Tiefe:0} m");
        return string.Join(" · ", teile);
    }

    /// <summary>„Weiter“: merkt die Wahl, richtet die Rivalen der Karte ein und geht zu „Euer Oberhaupt“.</summary>
    private void KarteUebernehmen()
    {
        if (_kartenAuswahl is null) return;
        Stadtkarten.Gewaehlt = _kartenAuswahl;
        var einstellungen = new ConfigFile();
        einstellungen.Load("user://einstellungen.cfg");
        einstellungen.SetValue("spiel", "karte", _kartenAuswahl);
        einstellungen.Save("user://einstellungen.cfg");
        _rivalen.Clear(); // die Rivalen gehören zur Karte
        RivalenWahlFuellen();
        Zeigen(_oberhaupt);
    }

    private static string LetzteKarte()
    {
        var einstellungen = new ConfigFile();
        return einstellungen.Load("user://einstellungen.cfg") == Error.Ok ? einstellungen.GetValue("spiel", "karte", "").AsString() : "";
    }
}
