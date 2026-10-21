using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Die Tafeln der Kartenwerkstatt – ein Teil der <c>partial class Kartenwerkstatt</c>: oben die Leiste (Neu, Öffnen, Speichern, Prüfen,
/// Rückgängig/Wiederholen, Zurück), links der Katalog nach Art, rechts die Eigenschaften des gewählten Gebäudes und darunter die Fehlerliste,
/// unten Meldung und Tastenhilfe. Dazu die Rückfragen (Neu, Öffnen, Speichern unter, ungespeicherte Änderungen) in der Mitte.
/// Stil wie die Tafeln im Spiel (dunkles Holz, goldene Überschriften); jede Änderung an einem Gebäude ist ein Befehl des Entwurfs.
/// </summary>
public partial class Kartenwerkstatt
{
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.72f, 0.72f, 0.72f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);
    private static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Hell = new(0.92f, 0.88f, 0.80f);

    private const int Katalogbreite = 270, Rechtsbreite = 370, Rand = 16, ObenAb = 92, UntenBis = 76;

    /// <summary>Die Gruppen des Katalogs links: Überschrift und die Arten, die dazugehören (Reihenfolge wie im Fahrplan).</summary>
    private static readonly (string Titel, string[] Arten)[] Katalogteile =
    {
        ("Werkstattplätze", new[] { Bausteinart.Werkstattplatz }),
        ("Wohnhausplätze", new[] { Bausteinart.Wohnhausplatz }),
        ("Einwohnerplätze", new[] { Bausteinart.Einwohnerplatz }),
        ("Sonderbauten", new[] { Bausteinart.Sonderbau }),
        ("Kulissen", new[] { Bausteinart.Kulisse }),
        ("Umland", new[] { Bausteinart.Umlandbetrieb, Bausteinart.Umlandstelle }),
        ("Natur und Gelände", new[] { Bausteinart.Berg }),
    };

    private Label _titel = null!, _quelle = null!, _status = null!, _pruefKopf = null!;
    private Button _rueckgaengig = null!, _wiederholen = null!;
    private VBoxContainer _eigenschaften = null!, _fehlerListe = null!;
    private readonly Dictionary<string, Button> _katalogKnoepfe = new();

    // Die Felder des gezeigten Gebäudes (bleiben stehen, solange dasselbe gewählt ist – sonst spränge der Fokus weg)
    private string? _gezeigt;
    private LineEdit? _nameFeld, _betriebFeld;
    private Label? _punktText;
    private SpinBox? _drehFeld, _massstabFeld;
    private OptionButton? _lageWahl, _konfessionWahl;

    private ColorRect _schleier = null!;
    private CenterContainer _dialogMitte = null!;
    private PanelContainer? _dialog;

    private bool DialogOffen => _dialog is not null;

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau
    // ═════════════════════════════════════════════════════════════════════════

    private void OberflaecheBauen()
    {
        _oberflaeche = new CanvasLayer();
        AddChild(_oberflaeche);
        LeisteBauen();
        KatalogBauen();
        RechtsBauen();
        HinweisBauen();
        _schleier = new ColorRect { Color = new Color(0, 0, 0, 0.45f), Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _schleier.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _oberflaeche.AddChild(_schleier);
        _dialogMitte = Hauptmenue.Mitte();
        _schleier.AddChild(_dialogMitte);
    }

    /// <summary>Eine Tafel im Stil der Spieloberfläche (wie Bau- und Auswahltafel) mit Rand und einer Spalte.</summary>
    private static (PanelContainer Tafel, VBoxContainer Spalte) Tafel(int rand = 14, int abstand = 6)
    {
        var tafel = new PanelContainer();
        tafel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.15f, 0.13f, 0.97f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
        });
        var raender = new MarginContainer();
        foreach (var seite in new[] { "left", "right", "top", "bottom" })
            raender.AddThemeConstantOverride($"margin_{seite}", rand);
        tafel.AddChild(raender);
        var spalte = new VBoxContainer();
        spalte.AddThemeConstantOverride("separation", abstand);
        raender.AddChild(spalte);
        return (tafel, spalte);
    }

    private static Button Knopf(Container eltern, string text, Action aktion, string? tipp = null)
    {
        var knopf = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 34), TooltipText = tipp ?? "" };
        knopf.AddThemeFontSizeOverride("font_size", 15);
        knopf.Pressed += aktion;
        eltern.AddChild(knopf);
        return knopf;
    }

    private void LeisteBauen()
    {
        var (tafel, spalte) = Tafel(rand: 10);
        tafel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        (tafel.OffsetLeft, tafel.OffsetTop, tafel.OffsetRight) = (Rand, Rand, -Rand);
        _oberflaeche.AddChild(tafel);
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 8);
        spalte.AddChild(zeile);
        Knopf(zeile, "Neu", NeuZeigen, "Eine neue, leere Karte anlegen");
        Knopf(zeile, "Öffnen", OeffnenZeigen, "Eine eigene Karte öffnen – oder Greybox und Köln (nur lesen)");
        Knopf(zeile, "Speichern", Speichern, "In die eigene Datei unter user://karten speichern (Strg+S)");
        Knopf(zeile, "Speichern unter", SpeichernUnterZeigen, "Unter einem neuen Namen in user://karten speichern");
        Knopf(zeile, "Prüfen", Pruefen, "Die ganze Karte nach den Kartenregeln prüfen (läuft nach jeder Änderung von selbst)");
        zeile.AddChild(new VSeparator());
        _rueckgaengig = Knopf(zeile, "Rückgängig", () => _entwurf.Rueckgaengig());
        _wiederholen = Knopf(zeile, "Wiederholen", () => _entwurf.Wiederholen());
        zeile.AddChild(new VSeparator());

        var mitte = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        mitte.AddThemeConstantOverride("separation", 0);
        zeile.AddChild(mitte);
        _titel = Hauptmenue.Text(mitte, "", 18, Gold);
        _titel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _quelle = Hauptmenue.Text(mitte, "", 12, Grau);
        _quelle.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        Knopf(zeile, "Zurück zum Hauptmenü", ZurueckZumMenue);
    }

    private void KatalogBauen()
    {
        var (tafel, spalte) = Tafel();
        (tafel.AnchorLeft, tafel.AnchorTop, tafel.AnchorRight, tafel.AnchorBottom) = (0, 0, 0, 1);
        (tafel.OffsetLeft, tafel.OffsetTop, tafel.OffsetRight, tafel.OffsetBottom) = (Rand, ObenAb, Rand + Katalogbreite, -UntenBis);
        _oberflaeche.AddChild(tafel);
        Hauptmenue.Text(spalte, "Katalog", 20, Gold);
        Hauptmenue.Text(spalte, "Baustein wählen, dann in die Stadt klicken. Esc oder Rechtsklick: fertig.", 12, Grau, umbruchBreite: Katalogbreite - 30);
        var rolle = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        spalte.AddChild(rolle);
        var liste = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        liste.AddThemeConstantOverride("separation", 3);
        rolle.AddChild(liste);
        var gruppe = new ButtonGroup { AllowUnpress = true };
        foreach (var (titel, arten) in Katalogteile)
        {
            var bausteine = _entwurf.Katalog.Bausteine.Where(b => arten.Contains(b.Art)).ToList();
            if (bausteine.Count == 0) continue;
            var kopf = Hauptmenue.Text(liste, titel, 15, Gold);
            kopf.CustomMinimumSize = new Vector2(0, 26);
            kopf.VerticalAlignment = VerticalAlignment.Bottom;
            foreach (var b in bausteine)
            {
                var masse = b.Groesse is { Length: 2 } g ? $"{Kartenschreiber.Zahl(g[0])} × {Kartenschreiber.Zahl(g[1])} m" : "ohne Grundfläche";
                var knopf = new Button
                {
                    Text = b.Anzeigename, ToggleMode = true, ButtonGroup = gruppe, FocusMode = Control.FocusModeEnum.None,
                    Alignment = HorizontalAlignment.Left, ClipText = true,
                    TooltipText = $"{b.Kennung} · Grundfläche {masse} (mit 2 m Vorplatz vorn)" + (b.Modell is { } m ? $" · Modell {m}" : " · noch ohne Modell"),
                };
                knopf.AddThemeFontSizeOverride("font_size", 14);
                var kennung = b.Kennung;
                knopf.Pressed += () => _stadt.WerkstattBausteinWaehlen(knopf.ButtonPressed ? kennung : null);
                liste.AddChild(knopf);
                _katalogKnoepfe[kennung] = knopf;
            }
        }
    }

    /// <summary>Wählt einen Baustein wie ein Klick im Katalog (für die Startoption <c>--werkstatt-umriss</c>).</summary>
    private void KatalogWaehlen(string kennung)
    {
        if (!_katalogKnoepfe.TryGetValue(kennung, out var knopf)) throw new ArgumentException($"Den Baustein „{kennung}“ gibt es im Katalog nicht.");
        knopf.SetPressedNoSignal(true);
        _stadt.WerkstattBausteinWaehlen(kennung);
    }

    private void KatalogWahlAufheben()
    {
        foreach (var knopf in _katalogKnoepfe.Values) knopf.SetPressedNoSignal(false);
    }

    private void RechtsBauen()
    {
        var (tafel, spalte) = Tafel();
        (tafel.AnchorLeft, tafel.AnchorTop, tafel.AnchorRight, tafel.AnchorBottom) = (1, 0, 1, 1);
        (tafel.OffsetLeft, tafel.OffsetTop, tafel.OffsetRight, tafel.OffsetBottom) = (-Rand - Rechtsbreite, ObenAb, -Rand, -UntenBis);
        _oberflaeche.AddChild(tafel);
        Hauptmenue.Text(spalte, "Eigenschaften", 20, Gold);
        _eigenschaften = new VBoxContainer();
        _eigenschaften.AddThemeConstantOverride("separation", 6);
        spalte.AddChild(_eigenschaften);
        spalte.AddChild(new HSeparator());
        Hauptmenue.Text(spalte, "Prüfung", 20, Gold);
        _pruefKopf = Hauptmenue.Text(spalte, "", 14, Grau, umbruchBreite: Rechtsbreite - 30);
        var rolle = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        spalte.AddChild(rolle);
        _fehlerListe = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _fehlerListe.AddThemeConstantOverride("separation", 2);
        rolle.AddChild(_fehlerListe);
    }

    private void HinweisBauen()
    {
        var (tafel, spalte) = Tafel(rand: 8, abstand: 2);
        (tafel.AnchorLeft, tafel.AnchorTop, tafel.AnchorRight, tafel.AnchorBottom) = (0, 1, 1, 1);
        (tafel.OffsetLeft, tafel.OffsetTop, tafel.OffsetRight, tafel.OffsetBottom) = (Rand, -UntenBis + 8, -Rand, -Rand);
        tafel.GrowVertical = Control.GrowDirection.Begin;
        _oberflaeche.AddChild(tafel);
        _status = Hauptmenue.Text(spalte, "", 13, Gruen, zentriert: true);
        _status.Visible = false;
        Hauptmenue.Text(spalte,
            "Klick wählt · Ziehen verschiebt · Q/E dreht (15°, mit Alt 1°) · Alt: frei verschieben · Entf löscht · Esc bricht ab · Strg+Z/Y rückgängig/wiederholen · Strg+S speichert\n" +
            "Mausrad Zoom · rechte Maustaste oder WASD verschieben die Kamera · Strg+Q/E oder Bild auf/ab drehen sie · Alt zeigt alle Namen",
            12, Grau, zentriert: true);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Auffrischen nach jeder Änderung
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Kopf, Rückgängig/Wiederholen und Fehlerliste nach dem Stand des Entwurfs.</summary>
    private void Auffrischen()
    {
        var name = _entwurf.Stadt.Length > 0 ? _entwurf.Stadt : "Ohne Namen";
        _titel.Text = $"{name} · {_entwurf.Kennung} · {_entwurf.Gebaeude.Count} Gebäude";
        var quelle = _nurLesen ? $"{_pfad} – mitgeliefert, nur lesen (Speichern legt eine eigene Datei an)"
            : _pfad ?? "neue Karte, noch nicht gespeichert";
        _quelle.Text = quelle + (_entwurf.Geschichte.IstGeaendert ? " · ungespeicherte Änderungen" : "");
        var g = _entwurf.Geschichte;
        _rueckgaengig.Disabled = !g.KannRueckgaengig;
        _rueckgaengig.TooltipText = g.RueckgaengigText is { } r ? $"Rückgängig: {r} (Strg+Z)" : "Nichts rückgängig zu machen (Strg+Z)";
        _wiederholen.Disabled = !g.KannWiederholen;
        _wiederholen.TooltipText = g.WiederholenText is { } w ? $"Wiederholen: {w} (Strg+Y)" : "Nichts zu wiederholen (Strg+Y)";
        FehlerlisteZeigen();
    }

    private void Melden(string text, bool fehler = false)
    {
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", fehler ? Rot : Gruen);
        _status.Visible = text.Length > 0;
    }

    private void Pruefen()
    {
        Auffrischen();
        var fehler = _entwurf.Befunde.Count(b => b.Schwere == Schwere.Fehler);
        var hinweise = _entwurf.Befunde.Count - fehler;
        Melden(fehler + hinweise == 0 ? "Prüfung: Die Karte hält alle Regeln ein." : $"Prüfung: {fehler} Fehler, {hinweise} Hinweise – rechts in der Liste.", fehler > 0);
    }

    /// <summary>So viele Befunde zeigt die Liste höchstens (eine kaputte Karte kann Hunderte haben).</summary>
    private const int ListenGrenze = 150;

    /// <summary>Die Befunde der Prüfung, Fehler zuerst; ein Klick wählt das betroffene Gebäude und fährt die Kamera an den Ort.</summary>
    private void FehlerlisteZeigen()
    {
        UiHilfe.KinderEntfernen(_fehlerListe);
        var befunde = _entwurf.Befunde.OrderBy(b => b.Schwere).ToList(); // Fehler vor Hinweisen, sonst in der Reihenfolge der Prüfung
        var fehler = befunde.Count(b => b.Schwere == Schwere.Fehler);
        var hinweise = befunde.Count - fehler;
        _pruefKopf.Text = befunde.Count == 0 ? "Keine Fehler, keine Hinweise." : $"{fehler} Fehler, {hinweise} Hinweise" + (befunde.Count > ListenGrenze ? $" (die ersten {ListenGrenze})" : "");
        _pruefKopf.AddThemeColorOverride("font_color", fehler > 0 ? Rot : befunde.Count > 0 ? Grau : Gruen);
        foreach (var b in befunde.Take(ListenGrenze))
        {
            var istFehler = b.Schwere == Schwere.Fehler;
            var knopf = new Button
            {
                Text = (istFehler ? "Fehler: " : "Hinweis: ") + b.Text, Flat = true, Alignment = HorizontalAlignment.Left,
                AutowrapMode = TextServer.AutowrapMode.WordSmart, FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(Rechtsbreite - 46, 0),
                TooltipText = b.Gebaeude.Count > 0 ? $"Regel „{b.Regel}“ · {string.Join(", ", b.Gebaeude)} – Klick wählt und zeigt" : $"Regel „{b.Regel}“",
            };
            knopf.AddThemeFontSizeOverride("font_size", 13);
            knopf.AddThemeColorOverride("font_color", istFehler ? Rot : Grau);
            knopf.AddThemeColorOverride("font_hover_color", Hell);
            knopf.Pressed += () => BefundZeigen(b);
            _fehlerListe.AddChild(knopf);
        }
    }

    private void BefundZeigen(Befund b)
    {
        var kennung = b.Gebaeude.FirstOrDefault();
        if (kennung is not null) _stadt.WerkstattWaehlen(kennung, hinfahren: b.Ort is null);
        if (b.Ort is { } ort) _stadt.WerkstattHinfahren(ort);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Eigenschaften des gewählten Gebäudes
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Baut die Felder für das gewählte Gebäude (<paramref name="neu"/>: eine andere Auswahl) oder setzt nur ihre Werte (dasselbe Gebäude hat
    /// sich geändert – die Felder bleiben, damit der Fokus nicht wegspringt). Jede Änderung in einem Feld ist ein Befehl.
    /// </summary>
    private void EigenschaftenZeigen(bool neu)
    {
        var g = _entwurf.GebaeudeMit(_stadt.WerkstattAuswahl);
        if (!neu && g is not null && g.Kennung == _gezeigt)
        {
            WerteSetzen(g);
            return;
        }
        UiHilfe.KinderEntfernen(_eigenschaften);
        (_gezeigt, _nameFeld, _betriebFeld, _punktText, _drehFeld, _massstabFeld, _lageWahl, _konfessionWahl) = (g?.Kennung, null, null, null, null, null, null, null);
        if (g is null)
        {
            Hauptmenue.Text(_eigenschaften, "Kein Gebäude gewählt. Klickt ein Gebäude an – oder wählt links einen Baustein und klickt in die Stadt.",
                14, Grau, umbruchBreite: Rechtsbreite - 30);
            return;
        }
        var kennung = g.Kennung;
        var raster = new GridContainer { Columns = 2 };
        raster.AddThemeConstantOverride("h_separation", 10);
        raster.AddThemeConstantOverride("v_separation", 6);
        _eigenschaften.AddChild(raster);

        _nameFeld = new LineEdit { Text = g.Name };
        _nameFeld.TextSubmitted += _ => _nameFeld.ReleaseFocus();
        _nameFeld.FocusExited += () =>
        {
            if (_entwurf.GebaeudeMit(kennung) is { } x && _nameFeld?.Text is { Length: > 0 } text && text != x.Name) _entwurf.Umbenennen(kennung, text);
        };
        Zeile(raster, "Name", _nameFeld);
        Zeile(raster, "Baustein", Wert($"{g.Baustein?.Anzeigename ?? "unbekannt"} ({g.Katalog})"));
        Zeile(raster, "Kennung", Wert(g.Kennung, "Spielstände und die Startbelegung verweisen auf die Kennung – die Werkstatt ändert sie nicht."));
        _punktText = Wert("");
        Zeile(raster, "Punkt", _punktText);

        _drehFeld = new Zahlfeld { MinValue = 0, MaxValue = 359.9, Step = 0.1, Suffix = "°", Value = g.Drehung, TooltipText = "0° = Vorderseite nach Süden, 90° = nach Osten (Q/E: 15°-Schritte)" };
        _drehFeld.ValueChanged += wert => _entwurf.Drehen(kennung, wert);
        Zeile(raster, "Drehung", _drehFeld);
        _massstabFeld = new Zahlfeld { MinValue = 0.25, MaxValue = 4, Step = 0.05, Value = g.Massstab, TooltipText = "Vergrößert Modell und Grundfläche" };
        _massstabFeld.ValueChanged += wert => _entwurf.MassstabSetzen(kennung, wert);
        Zeile(raster, "Maßstab", _massstabFeld);

        if (g.Art is Bausteinart.Werkstattplatz or Bausteinart.Umlandbetrieb)
        {
            _betriebFeld = new LineEdit { Text = g.Betrieb ?? "", PlaceholderText = "(keiner)", TooltipText = "Der Betrieb, der hier steht (Werkstatt eines KI-Hauses, Umlandbetrieb)" };
            _betriebFeld.TextSubmitted += _ => _betriebFeld.ReleaseFocus();
            _betriebFeld.FocusExited += () =>
            {
                if (_entwurf.GebaeudeMit(kennung) is { } x && (_betriebFeld?.Text ?? "") != (x.Betrieb ?? "")) _entwurf.BetriebSetzen(kennung, _betriebFeld?.Text);
            };
            Zeile(raster, "Betrieb", _betriebFeld);
        }
        if (g.Art is Bausteinart.Wohnhausplatz or Bausteinart.Einwohnerplatz)
        {
            _lageWahl = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Ohne Angabe gilt die nächste Bauzone" };
            _lageWahl.AddItem("nach Bauzone", -1);
            foreach (var lage in Enum.GetValues<Lage>()) _lageWahl.AddItem(Stadtansicht.LageName(lage), (int)lage);
            _lageWahl.ItemSelected += i => _entwurf.LageSetzen(kennung, _lageWahl!.GetItemId((int)i) is var id and >= 0 ? (Lage)id : null);
            Zeile(raster, "Lage", _lageWahl);
        }
        if (g.Art == Bausteinart.Sonderbau)
        {
            _konfessionWahl = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Für Kirchen: Welche Gemeinde dort predigt" };
            foreach (var text in Konfessionen) _konfessionWahl.AddItem(text);
            _konfessionWahl.ItemSelected += i => _entwurf.KonfessionSetzen(kennung, i == 0 ? null : Konfessionen[i]);
            Zeile(raster, "Konfession", _konfessionWahl);
        }

        var knoepfe = new HBoxContainer();
        knoepfe.AddThemeConstantOverride("separation", 8);
        _eigenschaften.AddChild(knoepfe);
        Knopf(knoepfe, "Hinfahren", () => _stadt.WerkstattWaehlen(kennung, hinfahren: true));
        Knopf(knoepfe, "Löschen", () => _entwurf.Loeschen(kennung), "Das Gebäude aus der Karte nehmen (Entf)");
        WerteSetzen(g);
    }

    private static readonly string[] Konfessionen = { "keine", "katholisch", "evangelisch" };

    /// <summary>Die Werte in die Felder, ohne Befehle auszulösen; ein Textfeld, in dem gerade geschrieben wird, bleibt, wie es ist.</summary>
    private void WerteSetzen(EntwurfGebaeude g)
    {
        if (_nameFeld is { } name && !name.HasFocus()) name.Text = g.Name;
        if (_betriebFeld is { } betrieb && !betrieb.HasFocus()) betrieb.Text = g.Betrieb ?? "";
        if (_punktText is not null) _punktText.Text = $"x {Kartenschreiber.Zahl(g.X)} · z {Kartenschreiber.Zahl(g.Z)} m";
        _drehFeld?.SetValueNoSignal(g.Drehung);
        _massstabFeld?.SetValueNoSignal(g.Massstab);
        _lageWahl?.Select(_lageWahl.GetItemIndex(g.Lage is { } l ? (int)l : -1));
        _konfessionWahl?.Select(Math.Max(0, Array.IndexOf(Konfessionen, g.Konfession ?? "keine")));
    }

    private static Label Wert(string text, string? tipp = null)
    {
        var label = new Label
        {
            Text = text, TooltipText = tipp ?? "", MouseFilter = tipp is null ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(Rechtsbreite - 130, 0), // lange Namen brechen um, statt die Tafel zu verbreitern
        };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", Hell);
        return label;
    }

    private static void Zeile(GridContainer raster, string beschriftung, Control feld)
    {
        var label = new Label { Text = beschriftung };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", Grau);
        raster.AddChild(label);
        feld.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        raster.AddChild(feld);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Rückfragen in der Mitte
    // ═════════════════════════════════════════════════════════════════════════

    private void DialogZeigen(PanelContainer tafel)
    {
        if (_dialog is not null) _dialogMitte.RemoveChild(_dialog);
        _dialog?.QueueFree();
        _dialog = tafel;
        _dialogMitte.AddChild(tafel);
        _schleier.Visible = true;
        _stadt.WerkstattGesperrt = true;
    }

    private void DialogSchliessen()
    {
        if (_dialog is null) return;
        _dialogMitte.RemoveChild(_dialog);
        _dialog.QueueFree();
        _dialog = null;
        _schleier.Visible = false;
        _stadt.WerkstattGesperrt = false;
    }

    /// <summary>Eine Rückfrage mit Text und zwei Knöpfen: <paramref name="jaText"/> führt <paramref name="ja"/> aus, „Abbrechen“ schließt.</summary>
    private void Rueckfrage(string titel, string text, string jaText, Action ja)
    {
        var (tafel, spalte) = Hauptmenue.Tafel(480);
        Hauptmenue.Text(spalte, titel, 24, Gold, zentriert: true);
        Hauptmenue.Text(spalte, text, 15, Hell, umbruchBreite: 420);
        var knoepfe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        knoepfe.AddThemeConstantOverride("separation", 12);
        spalte.AddChild(knoepfe);
        Hauptmenue.Knopf(knoepfe, jaText, () => { DialogSchliessen(); ja(); });
        Hauptmenue.Knopf(knoepfe, "Abbrechen", DialogSchliessen);
        DialogZeigen(tafel);
    }

    /// <summary>Hat die Karte ungespeicherte Änderungen, erst fragen, ob sie verworfen werden dürfen.</summary>
    private void MitRueckfrage(Action weiter)
    {
        if (!_entwurf.Geschichte.IstGeaendert)
        {
            DialogSchliessen();
            weiter();
            return;
        }
        Rueckfrage("Ungespeicherte Änderungen",
            $"Die Karte „{_entwurf.Stadt}“ hat Änderungen, die noch nicht gespeichert sind. Verwerfen und weiter?", "Verwerfen", weiter);
    }

    private void ZurueckZumMenue() => MitRueckfrage(() => GetTree().ChangeSceneToFile(Menueszene));

    private void NeuZeigen()
    {
        var (tafel, spalte) = Hauptmenue.Tafel(480);
        Hauptmenue.Text(spalte, "Neue Karte", 26, Gold, zentriert: true);
        var raster = new GridContainer { Columns = 2 };
        raster.AddThemeConstantOverride("h_separation", 14);
        raster.AddThemeConstantOverride("v_separation", 8);
        spalte.AddChild(raster);
        var kennung = new LineEdit { Text = "neustadt", TooltipText = "Kurz, ohne Leerzeichen – so heißt später die Datei" };
        Zeile(raster, "Kennung", kennung);
        var stadt = new LineEdit { Text = "Neustadt" };
        Zeile(raster, "Name der Stadt", stadt);
        var breite = new Zahlfeld { MinValue = 60, MaxValue = 2000, Step = 10, Value = 300, Suffix = "m", Rounded = true, TooltipText = "Von West nach Ost (x), um den Marktplatz" };
        Zeile(raster, "Breite", breite);
        var tiefe = new Zahlfeld { MinValue = 60, MaxValue = 2000, Step = 10, Value = 300, Suffix = "m", Rounded = true, TooltipText = "Von Nord nach Süd (z), um den Marktplatz" };
        Zeile(raster, "Tiefe", tiefe);
        var fehler = Hauptmenue.Text(spalte, "", 14, Rot, umbruchBreite: 420);
        var knoepfe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        knoepfe.AddThemeConstantOverride("separation", 12);
        spalte.AddChild(knoepfe);
        Hauptmenue.Knopf(knoepfe, "Anlegen", () =>
        {
            var k = Kartenentwurf.KennungVon(kennung.Text.Trim()).ToLowerInvariant();
            if (k.Length == 0) { fehler.Text = "Die Kennung braucht mindestens einen Buchstaben."; return; }
            if (stadt.Text.Trim().Length == 0) { fehler.Text = "Die Stadt braucht einen Namen."; return; }
            var neu = Kartenentwurf.Neu(k, stadt.Text.Trim(), breite.Value, tiefe.Value, KatalogLaden());
            MitRueckfrage(() => Wechseln(neu, null, false));
        });
        Hauptmenue.Knopf(knoepfe, "Abbrechen", DialogSchliessen);
        DialogZeigen(tafel);
    }

    private void OeffnenZeigen()
    {
        var (tafel, spalte) = Hauptmenue.Tafel(520);
        Hauptmenue.Text(spalte, "Karte öffnen", 26, Gold, zentriert: true);
        var fehler = Hauptmenue.Text(spalte, "", 14, Rot, umbruchBreite: 460);
        void Liste(string titel, string ordner, bool nurLesen, string leer)
        {
            Hauptmenue.Text(spalte, titel, 17, Gold);
            var karten = Karten(ordner);
            if (karten.Count == 0) Hauptmenue.Text(spalte, leer, 13, Grau, umbruchBreite: 460);
            foreach (var (pfad, text) in karten)
            {
                var knopf = Knopf(spalte, text, () =>
                {
                    var (entwurf, grund) = Lesen(pfad);
                    if (entwurf is null) fehler.Text = grund ?? "";
                    else MitRueckfrage(() => Wechseln(entwurf, pfad, nurLesen));
                }, pfad);
                knopf.Alignment = HorizontalAlignment.Left;
            }
        }
        Liste("Eigene Karten", Ordner, false, $"Noch keine – gespeichert wird im Ordner {ProjectSettings.GlobalizePath(Ordner)}.");
        Liste("Mitgeliefert (nur lesen)", Mitgeliefert, true, "Keine gefunden.");
        spalte.MoveChild(fehler, spalte.GetChildCount() - 1);
        Hauptmenue.Knopf(spalte, "Abbrechen", DialogSchliessen);
        DialogZeigen(tafel);
    }

    private void SpeichernUnterZeigen()
    {
        var (tafel, spalte) = Hauptmenue.Tafel(480);
        Hauptmenue.Text(spalte, "Speichern unter", 26, Gold, zentriert: true);
        Hauptmenue.Text(spalte, $"Im Ordner {ProjectSettings.GlobalizePath(Ordner)}", 13, Grau, umbruchBreite: 420);
        var vorschlag = !_nurLesen && _pfad is { } p ? p.GetFile() : _entwurf.Kennung + ".json";
        var name = new LineEdit { Text = vorschlag, SelectAllOnFocus = true };
        spalte.AddChild(name);
        var fehler = Hauptmenue.Text(spalte, "", 14, Rot, umbruchBreite: 420);
        var knoepfe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        knoepfe.AddThemeConstantOverride("separation", 12);
        spalte.AddChild(knoepfe);
        void Los()
        {
            if (Dateiname(name.Text) is not { } datei) { fehler.Text = "Der Name braucht mindestens einen Buchstaben."; return; }
            var pfad = $"{Ordner}/{datei}";
            if (Godot.FileAccess.FileExists(pfad) && pfad != _pfad)
                Rueckfrage("Datei überschreiben?", $"Die Karte {datei} gibt es schon. Überschreiben?", "Überschreiben", () => SpeichernNach(pfad));
            else if (SpeichernNach(pfad)) DialogSchliessen();
        }
        name.TextSubmitted += _ => Los();
        Hauptmenue.Knopf(knoepfe, "Speichern", Los);
        Hauptmenue.Knopf(knoepfe, "Abbrechen", DialogSchliessen);
        DialogZeigen(tafel);
        name.CallDeferred(Control.MethodName.GrabFocus);
    }
}
