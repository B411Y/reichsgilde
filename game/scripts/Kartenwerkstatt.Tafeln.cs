using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Die Tafeln der Kartenwerkstatt – ein Teil der <c>partial class Kartenwerkstatt</c>: oben die Leiste (Neu, Öffnen, Speichern, Prüfen,
/// Karte ausprobieren, Rückgängig/Wiederholen, Gassennetz, Zurück), links der Katalog (Wege, Flächen, Mauer, Gebäude nach Art, Natur), rechts
/// zwei Reiter: „Eigenschaften“ des gewählten Teils (Kartenwerkstatt.Eigenschaften.cs für Wege, Flächen, Pflanzen, Mauern und den Pinsel) und
/// „Start“ (Kartenwerkstatt.Start.cs), darunter die Fehlerliste; unten Meldung und Tastenhilfe. Dazu die Rückfragen (Neu, Öffnen, Speichern
/// unter, ungespeicherte Änderungen, Karte ausprobieren) in der Mitte.
/// Stil wie die Tafeln im Spiel (dunkles Holz, goldene Überschriften); jede Änderung an einem Teil ist ein Befehl des Entwurfs.
/// </summary>
public partial class Kartenwerkstatt
{
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.72f, 0.72f, 0.72f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);
    private static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Hell = new(0.92f, 0.88f, 0.80f);

    private const int Katalogbreite = 270, Rechtsbreite = 400, Rand = 16, ObenAb = 92, UntenBis = 76;

    /// <summary>Die Gruppen der Gebäude im Katalog links: Überschrift und die Arten, die dazugehören (Reihenfolge wie im Fahrplan).</summary>
    private static readonly (string Titel, string[] Arten)[] Katalogteile =
    {
        ("Werkstattplätze", new[] { Bausteinart.Werkstattplatz }),
        ("Wohnhausplätze", new[] { Bausteinart.Wohnhausplatz }),
        ("Einwohnerplätze", new[] { Bausteinart.Einwohnerplatz }),
        ("Sonderbauten", new[] { Bausteinart.Sonderbau }),
        ("Kulissen", new[] { Bausteinart.Kulisse }),
        ("Umland", new[] { Bausteinart.Umlandbetrieb, Bausteinart.Umlandstelle }),
        ("Gelände", new[] { Bausteinart.Berg }),
    };

    /// <summary>Die Wegarten im Katalog (Reihenfolge: von der Landstraße zum Fußweg).</summary>
    private static readonly Wegklasse[] Wegarten =
        { Wegklasse.Land, Wegklasse.Haupt, Wegklasse.Ring, Wegklasse.Wall, Wegklasse.Gasse, Wegklasse.Feldweg, Wegklasse.Fussweg, Wegklasse.Leinpfad };

    private Label _titel = null!, _quelle = null!, _status = null!, _pruefKopf = null!, _hilfe = null!;
    private Button _rueckgaengig = null!, _wiederholen = null!, _probeKnopf = null!, _speichernKnopf = null!;
    private CheckButton _netzSchalter = null!;
    private VBoxContainer _eigenschaften = null!, _fehlerListe = null!;

    /// <summary>
    /// Die Knöpfe des Katalogs nach Schlüssel: Gebäude und Pflanzen nach ihrer Kennung, dazu „weg:&lt;klasse&gt;“, „bauzone:&lt;lage&gt;“, „wald“,
    /// „streu“, „feldflaeche“ (ein Baustein „feld“ gibt es schon: die Arbeitsstelle im Umland), „aussparung“, „mauer“, „tor“, „pinsel“, „radierer“
    /// (auch die Werte der Startoption <c>--werkstatt-werkzeug</c>).
    /// </summary>
    private readonly Dictionary<string, (Button Knopf, Action Waehlen)> _katalogKnoepfe = new();

    /// <summary>Der Rollbereich des Katalogs (ein Werkzeug, das nicht von Hand gewählt wird, rollt dorthin).</summary>
    private ScrollContainer _katalogRolle = null!;

    // Der gezeigte Teil (die Felder bleiben stehen, solange derselbe gewählt ist – sonst spränge der Fokus weg); Pinsel: PinselTafel
    private object? _gezeigt;
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
        Knopf(zeile, "Öffnen", OeffnenZeigen, Entwicklung ? "Eine Karte des Spiels (game/karten) oder eine eigene öffnen"
            : "Eine eigene Karte öffnen – oder eine Karte des Spiels (nur lesen)");
        _speichernKnopf = Knopf(zeile, "Speichern", Speichern); // Tooltip nach der Quelle (Auffrischen)
        Knopf(zeile, "Speichern unter", SpeichernUnterZeigen, Entwicklung ? "Unter einem neuen Namen speichern – in game/karten oder im Benutzerordner"
            : "Unter einem neuen Namen im Benutzerordner speichern");
        Knopf(zeile, "Prüfen", Pruefen, "Die ganze Karte nach den Kartenregeln prüfen (läuft nach jeder Änderung von selbst)");
        _probeKnopf = Knopf(zeile, "Karte ausprobieren", AusprobierenZeigen);
        zeile.AddChild(new VSeparator());
        _rueckgaengig = Knopf(zeile, "Rückgängig", () => _entwurf.Rueckgaengig());
        _wiederholen = Knopf(zeile, "Wiederholen", () => _entwurf.Wiederholen());
        zeile.AddChild(new VSeparator());
        _netzSchalter = new CheckButton
        {
            Text = "Gassennetz", FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Zeigt das Gassennetz, auf dem Passanten und Karren laufen (pink, Knoten gelb) – mit den Stücken von Höfen, Karrenplätzen und Toren zum nächsten Weg",
        };
        _netzSchalter.AddThemeFontSizeOverride("font_size", 15);
        _netzSchalter.Toggled += an =>
        {
            _stadt.WerkstattNetzSichtbar = an;
            if (an) Melden(_stadt.WerkstattNetzTeile <= 1 ? "Gassennetz: alles ist verbunden." : $"Gassennetz: {_stadt.WerkstattNetzTeile} getrennte Teile – nicht jedes Ziel ist erreichbar.",
                _stadt.WerkstattNetzTeile > 1);
        };
        zeile.AddChild(_netzSchalter);
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
        Hauptmenue.Text(spalte, "Werkzeug wählen, dann in die Stadt klicken. Esc oder Rechtsklick: fertig.", 12, Grau, umbruchBreite: Katalogbreite - 30);
        var rolle = _katalogRolle = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        spalte.AddChild(rolle);
        var liste = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        liste.AddThemeConstantOverride("separation", 3);
        rolle.AddChild(liste);
        var gruppe = new ButtonGroup { AllowUnpress = true };

        void Kopf(string titel)
        {
            var kopf = Hauptmenue.Text(liste, titel, 15, Gold);
            kopf.CustomMinimumSize = new Vector2(0, 26);
            kopf.VerticalAlignment = VerticalAlignment.Bottom;
        }
        void Eintrag(string schluessel, string text, string tipp, Werkstattwerkzeug werkzeug, Action? vorher = null, string? baustein = null)
        {
            var knopf = new Button
            {
                Text = text, ToggleMode = true, ButtonGroup = gruppe, FocusMode = Control.FocusModeEnum.None,
                Alignment = HorizontalAlignment.Left, ClipText = true, TooltipText = tipp,
            };
            knopf.AddThemeFontSizeOverride("font_size", 14);
            void Waehlen()
            {
                vorher?.Invoke();
                if (!_stadt.WerkstattWerkzeugWaehlen(werkzeug, baustein)) knopf.SetPressedNoSignal(false);
            }
            knopf.Pressed += () =>
            {
                if (knopf.ButtonPressed) Waehlen();
                else _stadt.WerkstattWerkzeugWaehlen(Werkstattwerkzeug.Auswahl);
            };
            liste.AddChild(knopf);
            _katalogKnoepfe[schluessel] = (knopf, Waehlen);
        }

        // Wege: je Art ein Werkzeug, dazu der Schalter gerade/glatt
        Kopf("Wege");
        var glatt = new CheckButton { Text = "Glatte Kurve", ButtonPressed = _stadt.WerkstattGlatt, FocusMode = Control.FocusModeEnum.None,
            TooltipText = "An: eine weiche Kurve durch die Punkte. Aus: gerade Strecken von Punkt zu Punkt." };
        glatt.AddThemeFontSizeOverride("font_size", 14);
        glatt.Toggled += an => _stadt.WerkstattGlatt = an;
        liste.AddChild(glatt);
        foreach (var klasse in Wegarten)
            Eintrag("weg:" + klasse.ToString().ToLowerInvariant(), $"{Kartenweg.Anzeigename(klasse)} · {Breite(Kartenweg.Standardbreite(klasse))}",
                $"Breite {Breite(Kartenweg.Standardbreite(klasse))}, mindestens {Breite(Kartenweg.Mindestbreite(klasse))}"
                + (Kartenweg.TraegtKarren(klasse) ? " · Karren fahren hier" : " · nur zu Fuß") + " – Punkte klicken, Doppelklick oder Enter beendet",
                Werkstattwerkzeug.Weg, () => _stadt.WerkstattWegklasse = klasse);

        // Flächen
        Kopf("Flächen");
        foreach (var lage in new[] { Lage.Kern, Lage.Mauerring, Lage.Vorstadt, Lage.Umland })
            Eintrag("bauzone:" + lage.ToString().ToLowerInvariant(), $"Bauzone {Stadtansicht.LageName(lage)}",
                "Hier dürfen Spieler und KI bauen – Ecken klicken (auf dem 2-m-Raster), Doppelklick schließt", Werkstattwerkzeug.Bauzone,
                () => _stadt.WerkstattZonenlage = lage);
        Eintrag("wald", "Waldfläche", "Mischwald (Tanne, Eiche, Buche) – Ecken klicken, Doppelklick schließt; rechts Dichte und Mischung", Werkstattwerkzeug.Wald);
        Eintrag("streu", "Streufläche", "Büsche, Gras, Steine und Holz locker verstreut – Ecken klicken, Doppelklick schließt", Werkstattwerkzeug.Streu);
        Eintrag("feldflaeche", "Feldfläche", "Äcker in Streifen mit Furchen, jedes Stück mit einer Frucht – vor allem für den Randstreifen. Ecken klicken, Doppelklick schließt; rechts Richtung, Streifen und Früchte",
            Werkstattwerkzeug.Feld);
        Eintrag("aussparung", "Aussparung", "Ein Loch in der gewählten Wald-, Streu- oder Feldfläche – dort wächst nichts", Werkstattwerkzeug.Aussparung);

        // Mauer und Tore
        Kopf("Mauer");
        Eintrag("mauer", "Mauer", "Ecken klicken (gerade Stücke) – Klick auf die erste Ecke schließt den Ring, Doppelklick oder Enter lässt sie offen. Türme setzt das Spiel selbst.",
            Werkstattwerkzeug.Mauer);
        Eintrag("tor", "Tor", "Klick auf eine Mauer setzt dort ein Tor (es rastet auf die Linie); ein Weg durch das Tor rastet in seiner Mitte ein", Werkstattwerkzeug.Tor);

        // Wasser (Schritt 5c): Flüsse und Bäche (Linie, in Fließrichtung), Seen und Teiche (Fläche), Fährstellen, Häfen
        Kopf("Wasser");
        Eintrag("wasser:fluss", $"Fluss · {Breite(Wasserform.Vorgabebreite("fluss"))}", "Punkte klicken, in Fließrichtung (die Pfeile zeigen sie) – Doppelklick oder Enter legt den Fluss an. Brücken entstehen, wo ein Weg ihn quert.",
            Werkstattwerkzeug.Fluss, () => (_stadt.WerkstattFlussart, _stadt.WerkstattFlussbreite) = ("fluss", 0));
        Eintrag("wasser:bach", $"Bach · {Breite(Wasserform.Vorgabebreite("bach"))}", "Wie der Fluss, aber schmal und flach: Wege führen durch eine Furt (unter 0,5 m Tiefe) oder über eine kleine Brücke",
            Werkstattwerkzeug.Fluss, () => (_stadt.WerkstattFlussart, _stadt.WerkstattFlussbreite) = ("bach", 0));
        Eintrag("wasser:see", "See", "Ecken klicken, Doppelklick schließt – eine stille Wasserfläche mit Wasserstand und Tiefe", Werkstattwerkzeug.See, () => _stadt.WerkstattSeeart = "see");
        Eintrag("wasser:teich", "Teich", "Wie der See, nur klein", Werkstattwerkzeug.See, () => _stadt.WerkstattSeeart = "teich");
        Eintrag("wasser:faehre", "Fährstelle", "Zwei Klicks, die Anleger an beiden Ufern – sie rasten an Wegen ein. Karren setzen mit der Fähre über (und warten dort länger).", Werkstattwerkzeug.Faehre);
        if (_entwurf.Katalog.Finde(Kartenentwurf.HafenKatalog) is { } hafen)
            Eintrag("wasser:hafen", hafen.Anzeigename, "Ein Hafen am Ufer: die Vorderseite (Pfeil) zum Wasser, höchstens 3 m davon entfernt – Q/E dreht", Werkstattwerkzeug.Baustein, baustein: hafen.Kennung);

        // Gelände (Schritt 5b): Pinsel und Rampe
        Kopf("Gelände");
        foreach (var (art, name, tipp) in Gelaendepinsel)
            Eintrag("gelaende:" + art.ToString().ToLowerInvariant(), name, tipp + " – mit gedrückter Maus malen (Radius, Stärke und Kante rechts)",
                Werkstattwerkzeug.Gelaende, () => _stadt.GelaendeArtWaehlen(art));
        Eintrag("rampe", "Rampe", "Zwei Klicks: Anfang und Ende – dazwischen ein Band mit gleichmäßigem Gefälle (Breite rechts)", Werkstattwerkzeug.Rampe);

        // Gebäude
        foreach (var (titel, arten) in Katalogteile)
        {
            var bausteine = _entwurf.Katalog.Bausteine.Where(b => arten.Contains(b.Art)).ToList();
            if (bausteine.Count == 0) continue;
            Kopf(titel);
            foreach (var b in bausteine)
            {
                var masse = b.Groesse is { Length: 2 } g ? $"{Kartenschreiber.Zahl(g[0])} × {Kartenschreiber.Zahl(g[1])} m" : "ohne Grundfläche";
                Eintrag(b.Kennung, b.Anzeigename,
                    $"{b.Kennung} · Grundfläche {masse} (mit 2 m Vorplatz vorn)" + (b.Modell is { } m ? $" · Modell {m}" : " · noch ohne Modell"),
                    Werkstattwerkzeug.Baustein, baustein: b.Kennung);
            }
        }

        // Natur: Pinsel, Radierer und jede Art einzeln
        Kopf("Natur");
        Eintrag("pinsel", "Pinsel", "Mit gedrückter Maus malen – beim Loslassen wachsen dort Pflanzen (Radius, Dichte und Arten rechts)", Werkstattwerkzeug.Pinsel);
        Eintrag("radierer", "Radierer", "Mit gedrückter Maus über einzelne Pflanzen fahren – beim Loslassen sind sie weg", Werkstattwerkzeug.Radierer);
        foreach (var b in _entwurf.Katalog.Bausteine.Where(b => b.Art == Bausteinart.Natur))
            Eintrag(b.Kennung, ArtName(b), $"{b.Kennung} · {b.Varianten} Variante(n) · Modell {b.Modell} – Klick setzt eine Pflanze, Q/E dreht",
                Werkstattwerkzeug.Pflanze, baustein: b.Kennung);
    }

    /// <summary>Die Geländepinsel im Katalog: Art, Name und was sie tun.</summary>
    private static readonly (Pinselart Art, string Name, string Tipp)[] Gelaendepinsel =
    {
        (Pinselart.Heben, "Heben", "Hebt den Boden an (ein Hügel)"),
        (Pinselart.Senken, "Senken", "Senkt den Boden ab (eine Mulde, ein Graben)"),
        (Pinselart.Glaetten, "Glätten", "Gleicht Buckel aus, ohne die Höhe zu ändern"),
        (Pinselart.Ebnen, "Ebnen", "Zieht alles auf die Höhe unter dem ersten Klick (eine Terrasse)"),
        (Pinselart.Rauschen, "Rauschen", "Macht den Boden natürlich uneben (Buckel nach Startwert)"),
    };

    private static string Breite(double meter) => $"{Kartenschreiber.Zahl(Math.Round(meter, 2)).Replace('.', ',')} m";

    /// <summary>Anzeigename einer Natur-Art: Name aus dem Katalog, sonst der Knoten des Modells („Tanne“), sonst die Kennung.</summary>
    private static string ArtName(Baustein b) => !string.IsNullOrWhiteSpace(b.Name) ? b.Name! : b.Knoten ?? b.Kennung;

    private string ArtName(string kennung) => _entwurf.Katalog.Finde(kennung) is { } b ? ArtName(b) : kennung;

    /// <summary>
    /// Wählt ein Werkzeug wie ein Klick im Katalog und rollt den Katalog dorthin (Startoptionen <c>--werkstatt-umriss</c>, <c>--werkstatt-werkzeug</c>,
    /// Klick auf einen Befund, Knöpfe der Tafel rechts): Schlüssel wie in
    /// <see cref="_katalogKnoepfe"/>; „weg“ allein ist die Gasse, „bauzone“ allein die Vorstadt.
    /// </summary>
    private void KatalogWaehlen(string schluessel)
    {
        schluessel = schluessel switch { "weg" => "weg:gasse", "bauzone" => "bauzone:vorstadt", "flaeche" => "wald", _ => schluessel };
        if (!_katalogKnoepfe.TryGetValue(schluessel, out var eintrag)) throw new ArgumentException($"Das Werkzeug „{schluessel}“ gibt es im Katalog nicht.");
        KatalogWahlAufheben();
        eintrag.Knopf.SetPressedNoSignal(true);
        eintrag.Waehlen();
        Callable.From(() => _katalogRolle.EnsureControlVisible(eintrag.Knopf)).CallDeferred(); // nach dem Aufbau der Tafel (Startoptionen)
    }

    private void KatalogWahlAufheben()
    {
        foreach (var (knopf, _) in _katalogKnoepfe.Values) knopf.SetPressedNoSignal(false);
    }

    private void RechtsBauen()
    {
        var (tafel, spalte) = Tafel();
        (tafel.AnchorLeft, tafel.AnchorTop, tafel.AnchorRight, tafel.AnchorBottom) = (1, 0, 1, 1);
        (tafel.OffsetLeft, tafel.OffsetTop, tafel.OffsetRight, tafel.OffsetBottom) = (-Rand - Rechtsbreite, ObenAb, -Rand, -UntenBis);
        _oberflaeche.AddChild(tafel);
        ReiterBauen(spalte); // Kartenwerkstatt.Start.cs
        // Eigenschaften und Start teilen sich einen Rollbereich (Streu-Gruppen und acht Häuser werden lang); die Fehlerliste bleibt darunter
        _inhaltRolle = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsStretchRatio = 3.5f, HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, // nie breiter als die Tafel
        };
        spalte.AddChild(_inhaltRolle);
        var inhalt = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _inhaltRolle.AddChild(inhalt);
        _eigenschaften = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _eigenschaften.AddThemeConstantOverride("separation", 6);
        inhalt.AddChild(_eigenschaften);
        _startTafel = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false };
        _startTafel.AddThemeConstantOverride("separation", 6);
        inhalt.AddChild(_startTafel);
        _karteTafel = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false };
        _karteTafel.AddThemeConstantOverride("separation", 6);
        inhalt.AddChild(_karteTafel);
        spalte.AddChild(new HSeparator());
        Hauptmenue.Text(spalte, "Prüfung", 20, Gold);
        _pruefKopf = Hauptmenue.Text(spalte, "", 14, Grau, umbruchBreite: Rechtsbreite - 30);
        _pruefKopf.MouseFilter = Control.MouseFilterEnum.Stop; // der Tooltip nennt die Regeln der Karte
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
        _bodenInfo = Hauptmenue.Text(spalte, " ", 13, Gold, zentriert: true); // „Höhe 12,4 m · Gefälle 6 %“ unter der Maus
        _hilfe = Hauptmenue.Text(spalte, "", 12, Grau, zentriert: true);
        HilfeZeigen();
    }

    /// <summary>Die Tastenhilfe unten nach dem Werkzeug; die zweite Zeile (Kamera) bleibt.</summary>
    private void HilfeZeigen()
    {
        var zeile = _stadt.Kartenwerkzeug switch
        {
            Werkstattwerkzeug.Baustein => "Klick setzt · Q/E dreht (15°, mit Alt 1°) · Alt: frei verschieben · Esc oder Rechtsklick: fertig",
            Werkstattwerkzeug.Pflanze => "Klick setzt die Pflanze · Q/E dreht · Esc oder Rechtsklick: fertig",
            Werkstattwerkzeug.Weg => "Punkte klicken · Doppelklick oder Enter: fertig · Rücktaste: letzter Punkt weg · Esc bricht ab · Ring: rastet ein (weiß Weg, orange Hof, blau Tor, grün Halteplatz)",
            Werkstattwerkzeug.Bauzone or Werkstattwerkzeug.Wald or Werkstattwerkzeug.Streu or Werkstattwerkzeug.Feld or Werkstattwerkzeug.Aussparung =>
                "Ecken klicken · Doppelklick, Enter oder Klick auf die erste Ecke schließt · Rücktaste: letzte Ecke weg · Esc bricht ab",
            Werkstattwerkzeug.Pinsel => "Mit gedrückter Maus malen – Loslassen streut Pflanzen · Radius, Dichte und Arten rechts · Esc bricht ab",
            Werkstattwerkzeug.Radierer => "Mit gedrückter Maus über einzelne Pflanzen fahren – Loslassen löscht sie (die Natur der Flächen bleibt) · Esc bricht ab",
            Werkstattwerkzeug.Gelaende => "Mit gedrückter Maus malen – Radius, Stärke und Kante rechts · Loslassen beendet den Strich (ein Schritt für Strg+Z) · Esc nimmt ihn zurück · Gelb: Hangsperre ab 8 %, rot ab 15 %",
            Werkstattwerkzeug.Rampe => "Zwei Klicks: Anfang und Ende der Rampe · Breite und Kante rechts · Esc: erster Klick zurück · Rechtsklick: fertig",
            Werkstattwerkzeug.Mauer => "Ecken klicken (1-m-Raster, Alt: frei) · Klick auf die erste Ecke schließt den Ring · Doppelklick oder Enter: offen fertig · Rücktaste: letzte Ecke weg · Esc bricht ab",
            Werkstattwerkzeug.Fluss => "Punkte klicken, in Fließrichtung (Pfeile) · Doppelklick oder Enter: fertig · Rücktaste: letzter Punkt weg · Esc bricht ab",
            Werkstattwerkzeug.See => "Ecken klicken · Doppelklick, Enter oder Klick auf die erste Ecke schließt · Rücktaste: letzte Ecke weg · Esc bricht ab",
            Werkstattwerkzeug.Faehre => "Zwei Klicks: die Anleger an beiden Ufern (rasten an Wegen ein, der Ring zeigt wo) · Esc: erster Klick zurück · Rechtsklick: fertig",
            Werkstattwerkzeug.Tor => "Klick auf eine Mauer setzt ein Tor · Esc oder Rechtsklick: fertig",
            Werkstattwerkzeug.Platz => (_stadt.WerkstattPlatzHinweis ?? "Einen Platz anklicken") + " · grün: passt · Esc oder Rechtsklick bricht ab",
            _ => "Klick wählt · Ziehen verschiebt (Mauer: mit Umschalt) · Griffe ziehen, Klick auf Linie oder Rand fügt ein · Q/E dreht · Entf löscht Griff oder Teil · Esc hebt auf · Strg+Z/Y · Strg+S",
        };
        _hilfe.Text = zeile + "\nMausrad Zoom · rechte Maustaste oder WASD verschieben die Kamera · Strg+Q/E oder Bild auf/ab drehen sie · Alt zeigt alle Namen";
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Auffrischen nach jeder Änderung
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Kopf, Rückgängig/Wiederholen und Fehlerliste nach dem Stand des Entwurfs.</summary>
    private void Auffrischen()
    {
        var name = _entwurf.Stadt.Length > 0 ? _entwurf.Stadt : "Ohne Namen";
        _titel.Text = $"{name} · {_entwurf.Kennung} · {_entwurf.Gebaeude.Count} Gebäude, {_entwurf.Wege.Count} Wege, "
                      + $"{_entwurf.Bauzonen.Count + _entwurf.Naturflaechen.Count + _entwurf.Felder.Count} Flächen, {_entwurf.Pflanzen.Count} Pflanzen"
                      + (_entwurf.Gewaesser.Count + _entwurf.Faehren.Count > 0 ? $", {_entwurf.Gewaesser.Count} Gewässer" + (_entwurf.Faehren.Count > 0 ? $", {_entwurf.Faehren.Count} Fähren" : "") : "");
        var quelle = _pfad is not { } pfad ? "neue Karte, noch nicht gespeichert"
            : _nurLesen ? $"Spielkarte {Anzeigepfad(pfad)} – nur lesen (Speichern legt eine eigene Datei an)"
            : IstSpielkarte(pfad) ? $"Spielkarte – wird direkt gespeichert · {Anzeigepfad(pfad)}"
            : $"Eigene Karte · {Anzeigepfad(pfad)}";
        _quelle.Text = quelle + (_entwurf.Geschichte.IstGeaendert ? " · ungespeicherte Änderungen" : "");
        _speichernKnopf.TooltipText = _pfad is null || _nurLesen ? "Die Karte hat noch keine eigene Datei – fragt nach dem Namen (Strg+S)"
            : IstSpielkarte(_pfad) ? $"Direkt in {Anzeigepfad(_pfad)} speichern – das Spiel startet mit dieser Karte (Strg+S; beim ersten Mal mit Rückfrage)"
            : $"In {Anzeigepfad(_pfad)} speichern (Strg+S)";
        var g = _entwurf.Geschichte;
        _rueckgaengig.Disabled = !g.KannRueckgaengig;
        _rueckgaengig.TooltipText = g.RueckgaengigText is { } r ? $"Rückgängig: {r} (Strg+Z)" : "Nichts rückgängig zu machen (Strg+Z)";
        _wiederholen.Disabled = !g.KannWiederholen;
        _wiederholen.TooltipText = g.WiederholenText is { } w ? $"Wiederholen: {w} (Strg+Y)" : "Nichts zu wiederholen (Strg+Y)";
        var fehler = _entwurf.Befunde.Count(b => b.Schwere == Schwere.Fehler);
        _probeKnopf.Disabled = fehler > 0 || _daten is null;
        _probeKnopf.TooltipText = _daten is null ? "Ohne Wirtschaftsdaten lässt sich keine Partie aufstellen."
            : fehler > 0 ? $"Erst die {(fehler == 1 ? "Fehler" : $"{fehler} Fehler")} der Prüfung beheben (Liste rechts) – dann lässt sich die Karte ausprobieren."
            : "Eine neue Partie auf dieser Karte starten, wie sie gerade ist – Esc im Spiel führt zurück, hier bleibt alles erhalten.";
        FehlerlisteZeigen();
    }

    private void Melden(string text, bool fehler = false)
    {
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", fehler ? Rot : Gruen);
        _status.Visible = text.Length > 0;
    }

    /// <summary>
    /// „Prüfen“: die ganze Karte nach den Regeln (läuft sonst nach jeder Änderung von selbst) und die Kapazität frisch gerechnet – die Prüfung nach
    /// jeder Änderung nimmt sie aus dem Zwischenspeicher des Entwurfs (neu nur, wenn sich ein Gebäude, ein Weg oder eine Bauzone ändert).
    /// </summary>
    private void Pruefen()
    {
        Auffrischen();
        var fehler = _entwurf.Befunde.Count(b => b.Schwere == Schwere.Fehler);
        var hinweise = _entwurf.Befunde.Count - fehler;
        var text = fehler + hinweise == 0 ? "Prüfung: Die Karte hält alle Regeln ein." : $"Prüfung: {fehler} Fehler, {hinweise} Hinweise – rechts in der Liste.";
        if (_entwurf.Regeln.Kapazitaet is { } schwere)
        {
            var uhr = System.Diagnostics.Stopwatch.StartNew();
            var frisch = Kapazitaetspruefung.Pruefen(_entwurf, schwere);
            text += $" Kapazität neu gerechnet ({uhr.ElapsedMilliseconds} ms): "
                    + (frisch.Befunde.Count == 0 ? "alles passt." : $"{frisch.Befunde.Count} {(frisch.Befunde.Count == 1 ? "Gebäudeart passt" : "Gebäudearten passen")} nicht ganz.");
        }
        Melden(text, fehler > 0);
    }

    /// <summary>Das Symbol der Kapazität in der Fehlerliste: 3 × 3 Rasterfelder (wie eine Bauzone) in der Farbe der Schwere – je Farbe einmal gebaut.</summary>
    private static Texture2D KapazitaetsSymbol(Color farbe)
    {
        if (_symbole.TryGetValue(farbe, out var symbol)) return symbol;
        var bild = Image.CreateEmpty(18, 18, false, Image.Format.Rgba8);
        for (var i = 0; i < 3; i++)
            for (var j = 0; j < 3; j++)
                bild.FillRect(new Rect2I(1 + i * 6, 1 + j * 6, 5, 5), i == 1 && j == 1 ? farbe.Darkened(0.45f) : farbe);
        return _symbole[farbe] = ImageTexture.CreateFromImage(bild);
    }

    private static readonly Dictionary<Color, Texture2D> _symbole = new();

    /// <summary>Was die Kapazitätsprüfung prüft und ihr Bericht (Zeilen wie <c>tools/Kartenbau pruefen</c>) – für Tooltips.</summary>
    private string KapazitaetsBericht(Schwere schwere) =>
        "Kapazität: Passen die Gebäude einer Partie mit 8 Häusern in die Bauzonen der Stadt?\n"
        + "Die Prüfung setzt sie probeweise auf das Raster – Werkstätten an Karrenstraßen, Wohnhäuser auch an Gassen:\n"
        + string.Join("\n", _entwurf.Kapazitaetsrechnung(schwere).Bericht.Select(z => "  " + z))
        + "\nKlick zeigt in der Karte, wo die Prüfung die Gebäude unterbringt\n(noch ein Klick oder Esc blendet es aus).";

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
        var regeln = _entwurf.Regeln;
        _pruefKopf.TooltipText = $"Regeln dieser Karte ({_entwurf.Kennung}): allgemeine Kartenregeln"
            + (regeln.Stadtregeln ? ", Stadtregeln" : "") + (regeln.Werkstattnorm ? ", Werkstattnorm der Greybox" : "")
            + (regeln.Startgrundstuecke > 0 ? $", {regeln.Startgrundstuecke} Startgrundstücke" : "")
            + (regeln.Kapazitaet is { } k ? $", Kapazität als {(k == Schwere.Fehler ? "Fehler" : "Hinweis")}" : ", Kapazität nicht (der Probeplan prüft)");
        foreach (var b in befunde.Take(ListenGrenze))
        {
            var istFehler = b.Schwere == Schwere.Fehler;
            var knopf = new Button
            {
                Text = (istFehler ? "Fehler: " : "Hinweis: ") + b.Text, Flat = true, Alignment = HorizontalAlignment.Left,
                AutowrapMode = TextServer.AutowrapMode.WordSmart, FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(Rechtsbreite - 46, 0),
                TooltipText = b.Gebaeude.Count + b.Andere.Count > 0
                    ? $"Regel „{b.Regel}“ · {string.Join(", ", b.Gebaeude.Concat(b.Andere.Select(t => t.Bezeichnung)))} – Klick wählt und zeigt"
                    : $"Regel „{b.Regel}“",
            };
            knopf.AddThemeFontSizeOverride("font_size", 13);
            knopf.AddThemeColorOverride("font_color", istFehler ? Rot : Grau);
            knopf.AddThemeColorOverride("font_hover_color", Hell);
            knopf.Pressed += () => BefundZeigen(b);
            _fehlerListe.AddChild(knopf);
            if (b.Regel == "kapazitaet")
            {
                knopf.Icon = KapazitaetsSymbol(istFehler ? Rot : Grau);
                knopf.TooltipText = KapazitaetsBericht(b.Schwere);
            }
            else if (b.Regel == "einwohnerplaetze")
                knopf.TooltipText = "Einwohnerhäuser (die Häuser der Bürger der Stadt) stehen im Spiel auf Einwohnerplätzen\n"
                    + "und auf Wohnhausplätzen, die am Start niemand bewohnt. Fehlen beide, stehen alle auf einem Platz.\n"
                    + "Es fehlen Einwohnerplätze an den Gassen: im Katalog links unter „Einwohnerplätze“\n"
                    + "(am Ring 8,5 × 8 m, in der Vorstadt 7,5 × 6,2 m), die Vorderseite zur Gasse. Klick wählt das Werkzeug.";
        }
        // Gilt die Kapazität und passt alles, steht sie trotzdem in der Liste (grün) – mit ihrem Bericht
        if (regeln.Kapazitaet is { } schwere && befunde.All(b => b.Regel != "kapazitaet"))
        {
            var knopf = new Button
            {
                Text = "Kapazität: Die Gebäude einer Partie mit 8 Häusern passen in die Bauzonen der Stadt.", Flat = true, Alignment = HorizontalAlignment.Left,
                AutowrapMode = TextServer.AutowrapMode.WordSmart, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(Rechtsbreite - 46, 0),
                Icon = KapazitaetsSymbol(Gruen), TooltipText = KapazitaetsBericht(schwere),
            };
            knopf.AddThemeFontSizeOverride("font_size", 13);
            knopf.AddThemeColorOverride("font_color", Gruen);
            knopf.AddThemeColorOverride("font_hover_color", Hell);
            knopf.Pressed += () => KapazitaetUmschalten(schwere);
            _fehlerListe.AddChild(knopf);
        }
    }

    /// <summary>Blendet die Belegung der Kapazitätsprüfung in der Karte ein oder aus (mit der Legende unten).</summary>
    private void KapazitaetUmschalten(Schwere schwere)
    {
        if (_stadt.WerkstattKapazitaetSichtbar)
        {
            _stadt.WerkstattKapazitaetZeigen(null);
            Melden("");
            return;
        }
        _stadt.WerkstattKapazitaetZeigen(_entwurf.Kapazitaetsrechnung(schwere));
        Melden("Kapazität: dunkelgrau steht schon, rot Werkstätten, gelb Ratsherrenhäuser, weiß Bürgerhäuser, grün Wohnhäuser der Einwohner, "
               + "violett Stadthöfe, rosa Stadtpalais (Reserve) – freie Felder in der Farbe der Bauzone. Esc oder eine Änderung blendet es aus.");
    }

    /// <summary>
    /// Klick auf einen Befund: wählt das erste betroffene Gebäude (sonst den ersten Weg, die erste Fläche, Mauer oder Pflanze) und fährt hin; ein
    /// Befund der Startbelegung öffnet die Tafel „Start“.
    /// </summary>
    private void BefundZeigen(Befund b)
    {
        if (b.Regel == "kapazitaet")
        {
            KapazitaetUmschalten(b.Schwere);
            return;
        }
        if (b.Regel == "einwohnerplaetze" && _entwurf.Katalog.Bausteine.FirstOrDefault(x => x.Art == Bausteinart.Einwohnerplatz) is { } platz)
        {
            KatalogWaehlen(platz.Kennung);
            Melden($"Werkzeug „{platz.Anzeigename}“: an eine Gasse klicken, die Vorderseite (Pfeil) zur Gasse – Q/E dreht.");
            return;
        }
        if (b.Regel is "groesse" or "kamera" || b.Andere.Any(t => t is EntwurfKopf))
        {
            KarteReiterWaehlen(); // die Größe und der Kamera-Start stehen im Reiter „Karte“
            if (b.Ort is { } kopfOrt) _stadt.WerkstattHinfahren(kopfOrt);
            return;
        }
        var start = b.Andere.Any(t => t is EntwurfStart);
        if (start) ReiterWaehlen(true);
        EntwurfTeil? teil = _entwurf.GebaeudeMit(b.Gebaeude.FirstOrDefault()) ?? b.Andere.FirstOrDefault(t => t is not EntwurfStart);
        if (teil is not null) _stadt.WerkstattWaehlen(teil, hinfahren: b.Ort is null);
        if (b.Ort is { } ort) _stadt.WerkstattHinfahren(ort);
        if (start && !_startOffen) ReiterWaehlen(true);
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
        if (_stadt.Kartenwerkzeug is Werkstattwerkzeug.Pinsel or Werkstattwerkzeug.Radierer)
        {
            if (neu || _gezeigt != PinselMarke) PinselTafel(); // Kartenwerkstatt.Eigenschaften.cs
            return;
        }
        if (_stadt.Kartenwerkzeug is Werkstattwerkzeug.Gelaende or Werkstattwerkzeug.Rampe)
        {
            if (neu || _gezeigt != GelaendeMarke) GelaendeTafel(); // Kartenwerkstatt.Eigenschaften.cs
            return;
        }
        if (_stadt.Kartenwerkzeug is Werkstattwerkzeug.Fluss or Werkstattwerkzeug.See or Werkstattwerkzeug.Faehre)
        {
            if (neu || _gezeigt != WasserMarke) WasserWerkzeugTafel(); // Kartenwerkstatt.Wasser.cs
            return;
        }
        var teil = _stadt.WerkstattGewaehlt;
        if (!neu && teil is not null && ReferenceEquals(teil, _gezeigt))
        {
            if (teil is EntwurfGebaeude gezeigt) WerteSetzen(gezeigt);
            else if (!EingabeOffen()) TeilTafel(teil); // Kartenwerkstatt.Eigenschaften.cs
            return;
        }
        UiHilfe.KinderEntfernen(_eigenschaften);
        (_gezeigt, _nameFeld, _betriebFeld, _punktText, _drehFeld, _massstabFeld, _lageWahl, _konfessionWahl) = (teil, null, null, null, null, null, null, null);
        if (teil is null)
        {
            Hauptmenue.Text(_eigenschaften, "Nichts gewählt. Klickt ein Gebäude, einen Weg, eine Fläche oder eine Pflanze an – oder wählt links ein Werkzeug und klickt in die Stadt.",
                14, Grau, umbruchBreite: Rechtsbreite - 30);
            return;
        }
        if (teil is not EntwurfGebaeude g)
        {
            TeilTafel(teil);
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
            _lageWahl.AddItem("nach Bauzone", NachBauzone);
            foreach (var lage in Enum.GetValues<Lage>()) _lageWahl.AddItem(Stadtansicht.LageName(lage), (int)lage);
            _lageWahl.ItemSelected += i => _entwurf.LageSetzen(kennung, _lageWahl!.GetItemId((int)i) is var id and not NachBauzone ? (Lage)id : null);
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

    /// <summary>Kennung des Eintrags „nach Bauzone“ in der Wahl der Lage (−1 hieße für Godot „Kennung = Platz in der Liste“, also 0 wie Kern).</summary>
    private const int NachBauzone = 100;

    /// <summary>Die Werte in die Felder, ohne Befehle auszulösen; ein Textfeld, in dem gerade geschrieben wird, bleibt, wie es ist.</summary>
    private void WerteSetzen(EntwurfGebaeude g)
    {
        if (_nameFeld is { } name && !name.HasFocus()) name.Text = g.Name;
        if (_betriebFeld is { } betrieb && !betrieb.HasFocus()) betrieb.Text = g.Betrieb ?? "";
        if (_punktText is not null) _punktText.Text = $"x {Kartenschreiber.Zahl(g.X)} · z {Kartenschreiber.Zahl(g.Z)} m";
        _drehFeld?.SetValueNoSignal(g.Drehung);
        _massstabFeld?.SetValueNoSignal(g.Massstab);
        _lageWahl?.Select(_lageWahl.GetItemIndex(g.Lage is { } l ? (int)l : NachBauzone));
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
        var breite = new Zahlfeld { MinValue = Kartengrenzen.MinBreite, MaxValue = Kartengrenzen.MaxSeite, Step = Kartengrenzen.Schritt, Value = Kartengrenzen.NeueBreite, Suffix = "m", Rounded = true,
            TooltipText = "Das Spielfeld von West nach Ost (x), um den Marktplatz – 400 bis 2.000 m in 50-m-Schritten. Darum liegt der Randstreifen." };
        Zeile(raster, "Breite", breite);
        var tiefe = new Zahlfeld { MinValue = Kartengrenzen.MinTiefe, MaxValue = Kartengrenzen.MaxSeite, Step = Kartengrenzen.Schritt, Value = Kartengrenzen.NeueTiefe, Suffix = "m", Rounded = true,
            TooltipText = "Das Spielfeld von Nord nach Süd (z), um den Marktplatz – 300 bis 2.000 m in 50-m-Schritten" };
        Zeile(raster, "Tiefe", tiefe);
        Hauptmenue.Text(spalte, "Um das Spielfeld liegt ein Randstreifen von 300 m (im Reiter „Karte“ einstellbar); die neue Karte bekommt dort gleich einen Vorschlag aus Feldern und Wäldern, den man übermalen kann.",
            13, Grau, umbruchBreite: 420);
        var fehler = Hauptmenue.Text(spalte, "", 14, Rot, umbruchBreite: 420);
        var knoepfe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        knoepfe.AddThemeConstantOverride("separation", 12);
        spalte.AddChild(knoepfe);
        Hauptmenue.Knopf(knoepfe, "Anlegen", () =>
        {
            var k = Kartenentwurf.KennungVon(kennung.Text.Trim()).ToLowerInvariant();
            if (k.Length == 0) { fehler.Text = "Die Kennung braucht mindestens einen Buchstaben."; return; }
            if (stadt.Text.Trim().Length == 0) { fehler.Text = "Die Stadt braucht einen Namen."; return; }
            var neu = NeueKarte(k, stadt.Text.Trim(), breite.Value, tiefe.Value);
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
        if (Entwicklung) Liste("Karten des Spiels (game/karten – Speichern schreibt direkt dorthin)", Mitgeliefert, false, "Keine gefunden.");
        Liste("Eigene Karten", Ordner, false, $"Noch keine – gespeichert wird im Ordner {ProjectSettings.GlobalizePath(Ordner)}.");
        if (!Entwicklung) Liste("Karten des Spiels (nur lesen)", Mitgeliefert, true, "Keine gefunden.");
        spalte.MoveChild(fehler, spalte.GetChildCount() - 1);
        Hauptmenue.Knopf(spalte, "Abbrechen", DialogSchliessen);
        DialogZeigen(tafel);
    }

    /// <summary>„Karte ausprobieren“: den Beruf wählen, dann startet eine Partie auf dem Entwurf (nur ohne Fehler, siehe <see cref="Ausprobieren"/>).</summary>
    private void AusprobierenZeigen()
    {
        if (_probeKnopf.Disabled)
        {
            Melden(_probeKnopf.TooltipText, fehler: true);
            return;
        }
        var (tafel, spalte) = Hauptmenue.Tafel(480);
        Hauptmenue.Text(spalte, "Karte ausprobieren", 26, Gold, zentriert: true);
        Hauptmenue.Text(spalte, $"Eine neue Partie auf „{_entwurf.Stadt}“, so wie die Karte gerade ist – mit "
            + (_entwurf.Start.Haeuser.Count == 0 ? "den drei Häusern des Spiels" : $"{_entwurf.Start.Haeuser.Count} KI-Häusern")
            + ". Nichts wird gespeichert. Zurück geht es im Spiel mit Esc → „Zurück zur Kartenwerkstatt“; hier ist dann alles wie vorher, auch Rückgängig.",
            15, Hell, umbruchBreite: 420);
        var raster = new GridContainer { Columns = 2 };
        raster.AddThemeConstantOverride("h_separation", 14);
        spalte.AddChild(raster);
        var berufe = Berufe(); // Kartenwerkstatt.Start.cs
        var beruf = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (var b in berufe)
            beruf.AddItem(b + (_entwurf.Start.StartplatzFuer(b) is { } platz ? $" – {_entwurf.GebaeudeMit(platz.Platz)?.Name ?? platz.Platz}" : ""));
        beruf.Select(Math.Max(0, berufe.ToList().IndexOf("Schmied")));
        Zeile(raster, "Als", beruf);
        var knoepfe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        knoepfe.AddThemeConstantOverride("separation", 12);
        spalte.AddChild(knoepfe);
        Hauptmenue.Knopf(knoepfe, "Ausprobieren", () =>
        {
            DialogSchliessen();
            Ausprobieren(berufe[Math.Max(0, beruf.Selected)]);
        });
        Hauptmenue.Knopf(knoepfe, "Abbrechen", DialogSchliessen);
        DialogZeigen(tafel);
    }

    /// <summary>
    /// „Speichern unter“: Name und – in der Entwicklung – der Ordner: <c>game/karten/</c> (eine Karte des Spiels, <c>--karte=&lt;name&gt;</c>) oder
    /// der Benutzerordner. Vorgewählt ist der Ordner der offenen Datei. Eine andere vorhandene Datei überschreibt erst nach einer Rückfrage.
    /// </summary>
    private void SpeichernUnterZeigen()
    {
        var (tafel, spalte) = Hauptmenue.Tafel(480);
        Hauptmenue.Text(spalte, "Speichern unter", 26, Gold, zentriert: true);
        var ordner = Entwicklung && IstSpielkarte(_pfad) ? Mitgeliefert : Ordner;
        var wahl = new OptionButton { FocusMode = Control.FocusModeEnum.None, Visible = Entwicklung };
        wahl.AddItem("Karten des Spiels (game/karten)");
        wahl.AddItem("Eigene Karten (Benutzerordner)");
        wahl.Selected = ordner == Mitgeliefert ? 0 : 1;
        spalte.AddChild(wahl);
        var ort = Hauptmenue.Text(spalte, "", 13, Grau, umbruchBreite: 420);
        void OrtZeigen() => ort.Text = ordner == Mitgeliefert
            ? $"Im Ordner {ProjectSettings.GlobalizePath(Mitgeliefert)} bei den Karten des Spiels – Git zeigt die Änderung."
            : $"Im Ordner {ProjectSettings.GlobalizePath(Ordner)}";
        OrtZeigen();
        wahl.ItemSelected += i =>
        {
            ordner = i == 0 ? Mitgeliefert : Ordner;
            OrtZeigen();
        };
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
            var pfad = $"{ordner}/{datei}";
            if (Godot.FileAccess.FileExists(pfad) && pfad != _pfad)
                Rueckfrage("Datei überschreiben?", $"Die Karte {Anzeigepfad(pfad)} gibt es schon. Überschreiben?", "Überschreiben", () =>
                {
                    if (IstSpielkarte(pfad)) _ueberschreibenBestaetigt.Add(pfad);
                    SpeichernNach(pfad);
                });
            else if (pfad == _pfad && IstSpielkarte(pfad) && !_ueberschreibenBestaetigt.Contains(pfad)) SpielkarteUeberschreiben(pfad, () => { });
            else if (SpeichernNach(pfad))
            {
                if (IstSpielkarte(pfad)) _ueberschreibenBestaetigt.Add(pfad); // neu angelegt: weiter ohne Rückfrage
                DialogSchliessen();
            }
        }
        name.TextSubmitted += _ => Los();
        Hauptmenue.Knopf(knoepfe, "Speichern", Los);
        Hauptmenue.Knopf(knoepfe, "Abbrechen", DialogSchliessen);
        DialogZeigen(tafel);
        name.CallDeferred(Control.MethodName.GrabFocus);
    }
}
