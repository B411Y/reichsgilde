using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Die Bedienung der Kartenwerkstatt (Schritt 5f) – ein Teil der <c>partial class Kartenwerkstatt</c>: oben die Kopfleiste (Datei, Rückgängig,
/// Gassennetz, Ausprobieren), links die schmale Gruppenleiste (ein Icon je Gruppe, Taste 1–0) und daneben die Palette der gewählten Gruppe
/// (Icons der Werkzeuge, Gebäude als kleine Vorschaubilder aus dem Modell). Rechts bleiben nur die Eigenschaften (Kartenwerkstatt.Tafeln.cs).
/// Die Icons im Holzschnitt-Stil erzeugt <c>tools/icons/kartenwerkstatt_icons.py</c>.
/// </summary>
public partial class Kartenwerkstatt
{
    /// <summary>Eine Gruppe der Leiste links: Kennung, Name, Icon, Taste, Erklärung und ob sie eine Palette aufklappt.</summary>
    private sealed record Gruppe(string Kennung, string Name, string Icon, Key Taste, string Tipp, bool Palette);

    private static readonly Gruppe[] Gruppen =
    {
        new("auswaehlen", "Auswählen", "auswaehlen", Key.Key1, "Gebäude, Wege, Flächen, Pflanzen und Mauern anklicken, ziehen, drehen und löschen – ihre Eigenschaften stehen rechts.", false),
        new("gelaende", "Gelände", "gelaende", Key.Key2, "Hügel und Mulden malen, Rampen ziehen, Berge setzen.", true),
        new("boden", "Boden", "boden", Key.Key3, "Bodentexturen malen: Erde, Kies, Pflaster, Sand … (mit Deko wie Steinen und Gras).", true),
        new("wasser", "Wasser", "wasser", Key.Key4, "Flüsse, Bäche, Seen, Teiche, Fähren und Häfen. Brücken und Furten entstehen von selbst.", true),
        new("wege", "Wege", "wege", Key.Key5, "Straßen, Gassen und Pfade zeichnen – von der Landstraße bis zum Fußweg.", true),
        new("flaechen", "Flächen und Natur", "flaechen", Key.Key6, "Bauzonen, Wald, Streu, Felder und Aussparungen; Pinsel, Radierer und einzelne Pflanzen.", true),
        new("gebaeude", "Gebäude", "gebaeude", Key.Key7, "Werkstatt-, Wohnhaus- und Einwohnerplätze, Sonderbauten, Kulissen und Umland.", true),
        new("mauer", "Mauer und Tore", "mauer", Key.Key8, "Stadtmauern ziehen und Tore hineinsetzen.", true),
        new("start", "Start", "start", Key.Key9, "Startbelegung: wo der Spieler und die KI-Häuser am Start wohnen und arbeiten.", false),
        new("pruefen", "Prüfen", "pruefen", Key.Key0, "Die Prüfliste ein- oder ausblenden. Der Zähler zeigt Fehler und Hinweise der Karte nach den Kartenregeln (läuft nach jeder Änderung von selbst).", false),
    };

    private static string TastenName(Key taste) => taste == Key.Key0 ? "0" : ((int)taste - (int)Key.Key0).ToString();

    private readonly Dictionary<string, Button> _gruppenKnoepfe = new();
    private readonly Dictionary<string, VBoxContainer> _paletten = new();

    /// <summary>Zu jedem Werkzeug des Katalogs die Gruppe, in der es liegt (damit <see cref="KatalogWaehlen"/> die richtige Palette aufklappt).</summary>
    private readonly Dictionary<string, string> _eintragGruppe = new();

    private PanelContainer _paletteTafel = null!;
    private Label _paletteTitel = null!, _paletteHinweis = null!, _pruefZaehler = null!;
    private VBoxContainer _pruefBereich = null!;
    private string? _paletteGruppe;

    /// <summary>Das gewählte Werkzeug für die Statusleiste („Gelände › Heben“).</summary>
    private string _werkzeugName = "Auswählen";

    // ═════════════════════════════════════════════════════════════════════════
    //  Icons
    // ═════════════════════════════════════════════════════════════════════════

    private const string IconOrdner = "res://assets/oberflaeche/icons/kartenwerkstatt/";

    private static readonly Dictionary<string, Texture2D?> _ikonen = new();

    /// <summary>Das Icon <c>icons/kartenwerkstatt/&lt;name&gt;.png</c> (null: gibt es nicht).</summary>
    private static Texture2D? Symbol(string name)
    {
        if (name.Length == 0) return null;
        if (_ikonen.TryGetValue(name, out var bild) && bild is not null) return bild;
        var pfad = $"{IconOrdner}{name}.png";
        bild = ResourceLoader.Exists(pfad) ? ResourceLoader.Load<Texture2D>(pfad) : null;
        if (bild is null) GD.PushError($"Icon fehlt: {pfad}");
        return _ikonen[name] = bild;
    }

    /// <summary>Ein Farbfeld mit Tintenrand und Schraffur unten rechts als Symbol einer Bodentextur.</summary>
    private static ImageTexture Farbfeld(Color farbe)
    {
        const int n = 40;
        var tinte = new Color(0.13f, 0.09f, 0.07f);
        var bild = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        bild.Fill(farbe);
        for (var x = 0; x < n; x++)
            for (var y = 0; y < n; y++)
                if (x < 2 || y < 2 || x >= n - 2 || y >= n - 2 || (x + y > n + 6 && (x + y) % 4 == 0)) bild.SetPixel(x, y, tinte);
        return ImageTexture.CreateFromImage(bild);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Kopfleiste
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Ein Knopf der Kopfleiste: Icon (und Text), Tooltip.</summary>
    private static Button KopfKnopf(Container eltern, string icon, string text, Action aktion, string tipp, bool umschalter = false)
    {
        var knopf = new Button
        {
            Icon = Symbol(icon), Text = text, TooltipText = tipp, FocusMode = Control.FocusModeEnum.None, ToggleMode = umschalter,
            CustomMinimumSize = new Vector2(text.Length == 0 ? 42 : 0, 42),
        };
        knopf.AddThemeConstantOverride("icon_max_width", 30);
        knopf.AddThemeFontSizeOverride("font_size", 15);
        if (umschalter) knopf.Toggled += _ => aktion();
        else knopf.Pressed += aktion;
        eltern.AddChild(knopf);
        return knopf;
    }

    private void LeisteBauen()
    {
        var (tafel, spalte) = Tafel(rand: 6);
        tafel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        (tafel.OffsetLeft, tafel.OffsetTop, tafel.OffsetRight) = (Rand, Rand, -Rand);
        _oberflaeche.AddChild(tafel);
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 6);
        spalte.AddChild(zeile);
        KopfKnopf(zeile, "neu", "", NeuZeigen, "Neu\nEine neue, leere Karte anlegen.");
        KopfKnopf(zeile, "oeffnen", "", OeffnenZeigen, Entwicklung ? "Öffnen\nEine Karte des Spiels (game/karten) oder eine eigene öffnen."
            : "Öffnen\nEine eigene Karte öffnen – oder eine Karte des Spiels (nur lesen).");
        _speichernKnopf = KopfKnopf(zeile, "speichern", "", Speichern, "Speichern"); // Tooltip nach der Quelle (Auffrischen)
        KopfKnopf(zeile, "speichern_unter", "", SpeichernUnterZeigen, Entwicklung ? "Speichern unter\nUnter einem neuen Namen speichern – in game/karten oder im Benutzerordner."
            : "Speichern unter\nUnter einem neuen Namen im Benutzerordner speichern.");
        zeile.AddChild(new VSeparator());
        _rueckgaengig = KopfKnopf(zeile, "rueckgaengig", "", () => _entwurf.Rueckgaengig(), "Rückgängig");
        _wiederholen = KopfKnopf(zeile, "wiederholen", "", () => _entwurf.Wiederholen(), "Wiederholen");
        zeile.AddChild(new VSeparator());
        _netzSchalter = KopfKnopf(zeile, "netz", "", () =>
        {
            var an = _netzSchalter.ButtonPressed;
            _stadt.WerkstattNetzSichtbar = an;
            if (an) Melden(_stadt.WerkstattNetzTeile <= 1 ? "Gassennetz: alles ist verbunden." : $"Gassennetz: {_stadt.WerkstattNetzTeile} getrennte Teile – nicht jedes Ziel ist erreichbar.",
                _stadt.WerkstattNetzTeile > 1);
        }, "Gassennetz\nZeigt das Gassennetz, auf dem Passanten und Karren laufen (pink, Knoten gelb) – mit den Stücken von Höfen, Karrenplätzen und Toren zum nächsten Weg.", umschalter: true);
        EbenenKnopfBauen(zeile); // Kartenwerkstatt.Ebenen.cs
        zeile.AddChild(new VSeparator());

        var mitte = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        mitte.AddThemeConstantOverride("separation", 0);
        zeile.AddChild(mitte);
        _titel = Hauptmenue.Text(mitte, "", 18, Gold);
        _titel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _quelle = Hauptmenue.Text(mitte, "", 12, Grau);
        _quelle.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _probeKnopf = KopfKnopf(zeile, "ausprobieren", "Karte ausprobieren", AusprobierenZeigen, "");
        KopfKnopf(zeile, "", "Hauptmenü", ZurueckZumMenue, "Zurück zum Hauptmenü");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Gruppenleiste links
    // ═════════════════════════════════════════════════════════════════════════

    private void GruppenBauen()
    {
        var (tafel, spalte) = Tafel(rand: 6, abstand: 4);
        (tafel.AnchorLeft, tafel.AnchorTop, tafel.AnchorRight, tafel.AnchorBottom) = (0, 0, 0, 0);
        (tafel.OffsetLeft, tafel.OffsetTop, tafel.OffsetRight) = (Rand, ObenAb, Rand + Leistenbreite);
        _oberflaeche.AddChild(tafel);
        foreach (var g in Gruppen)
        {
            var knopf = new Button
            {
                Icon = Symbol(g.Icon), ToggleMode = true, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(Leistenbreite - 12, 46),
                TooltipText = $"{g.Name} (Taste {TastenName(g.Taste)})\n{g.Tipp}",
            };
            knopf.AddThemeConstantOverride("icon_max_width", 32);
            var kennung = g.Kennung;
            knopf.Pressed += () => GruppeWaehlen(kennung);
            spalte.AddChild(knopf);
            _gruppenKnoepfe[g.Kennung] = knopf;
            var taste = new Label { Text = TastenName(g.Taste), MouseFilter = Control.MouseFilterEnum.Ignore };
            taste.AddThemeFontSizeOverride("font_size", 11);
            taste.AddThemeColorOverride("font_color", Gold);
            taste.Position = new Vector2(4, 0);
            knopf.AddChild(taste);
            if (g.Kennung != "pruefen") continue;
            _pruefZaehler = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Right };
            _pruefZaehler.AddThemeFontSizeOverride("font_size", 11);
            _pruefZaehler.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
            (_pruefZaehler.OffsetLeft, _pruefZaehler.OffsetTop, _pruefZaehler.OffsetRight, _pruefZaehler.OffsetBottom) = (-36, -18, -3, 0);
            knopf.AddChild(_pruefZaehler);
        }
        GruppenMarkieren();
    }

    private static Gruppe? GruppeFuerTaste(Key taste) => Gruppen.FirstOrDefault(g => g.Taste == taste);

    /// <summary>Ein Klick auf einen Gruppenknopf (oder seine Taste).</summary>
    private void GruppeWaehlen(string kennung)
    {
        switch (kennung)
        {
            case "auswaehlen":
                PaletteSchliessen();
                _stadt.WerkstattWerkzeugWaehlen(Werkstattwerkzeug.Auswahl);
                if (_startOffen || _karteOffen) ReiterWaehlen(false);
                break;
            case "start":
                PaletteSchliessen();
                ReiterWaehlen(true);
                break;
            case "pruefen":
                _pruefBereich.Visible = !_pruefBereich.Visible;
                if (_pruefBereich.Visible) Pruefen();
                break;
            default:
                if (_paletteGruppe == kennung) PaletteSchliessen();
                else PaletteOeffnen(kennung);
                break;
        }
        GruppenMarkieren();
    }

    /// <summary>Hebt den Knopf der offenen Palette (oder des Reiters „Start“) hervor, dazu „Prüfen“, solange die Liste offen ist.</summary>
    private void GruppenMarkieren()
    {
        var aktiv = _startOffen ? "start" : _karteOffen ? "" : _paletteGruppe ?? "auswaehlen";
        foreach (var (kennung, knopf) in _gruppenKnoepfe) knopf.SetPressedNoSignal(kennung == aktiv || (kennung == "pruefen" && _pruefBereich?.Visible == true));
    }

    private void PaletteOeffnen(string kennung)
    {
        if (_startOffen || _karteOffen) ReiterWaehlen(false);
        _paletteGruppe = kennung;
        foreach (var (name, palette) in _paletten) palette.Visible = name == kennung;
        var gruppe = Gruppen.First(g => g.Kennung == kennung);
        _paletteTitel.Text = $"{gruppe.Name}  ({TastenName(gruppe.Taste)})";
        _paletteHinweis.Text = gruppe.Tipp;
        _paletteTafel.Visible = true;
        _katalogRolle.ScrollVertical = 0;
        VorschauenRendern();
        GruppenMarkieren();
    }

    private void PaletteSchliessen()
    {
        _paletteGruppe = null;
        _paletteTafel.Visible = false;
        GruppenMarkieren();
    }

    /// <summary>Der Zähler am Knopf „Prüfen“: Fehler (rot) oder Hinweise (gold), sonst eine grüne Null.</summary>
    private void ZaehlerAuffrischen()
    {
        var fehler = _entwurf.Befunde.Count(b => b.Schwere == Schwere.Fehler);
        var hinweise = _entwurf.Befunde.Count - fehler;
        _pruefZaehler.Text = fehler > 0 ? $"{fehler} F" : hinweise > 0 ? $"{hinweise} H" : "0";
        _pruefZaehler.AddThemeColorOverride("font_color", fehler > 0 ? Rot : hinweise > 0 ? Gold : Gruen);
        _gruppenKnoepfe["pruefen"].TooltipText = $"Prüfen (Taste 0)\n{Gruppen[^1].Tipp}\nJetzt: {fehler} Fehler (F), {hinweise} Hinweise (H).";
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Paletten
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Gebäude, die noch auf ihr Vorschaubild warten (der Knopf bekommt es, sobald es gerendert ist).</summary>
    private readonly List<(Button Knopf, Baustein Baustein)> _vorschauWarte = new();

    private void KatalogBauen()
    {
        var (tafel, spalte) = Tafel();
        _paletteTafel = tafel;
        tafel.Visible = false;
        (tafel.AnchorLeft, tafel.AnchorTop, tafel.AnchorRight, tafel.AnchorBottom) = (0, 0, 0, 1);
        (tafel.OffsetLeft, tafel.OffsetTop, tafel.OffsetRight, tafel.OffsetBottom) = (Rand + Leistenbreite + 6, ObenAb, Rand + Leistenbreite + 6 + Palettenbreite, -UntenBis);
        _oberflaeche.AddChild(tafel);
        _paletteTitel = Hauptmenue.Text(spalte, "", 20, Gold);
        _paletteHinweis = Hauptmenue.Text(spalte, "", 12, Grau, umbruchBreite: Palettenbreite - 30);
        Hauptmenue.Text(spalte, "Werkzeug wählen, dann in die Stadt klicken. Esc oder Rechtsklick: fertig.", 12, Grau, umbruchBreite: Palettenbreite - 30);
        var rolle = _katalogRolle = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        spalte.AddChild(rolle);
        var alle = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        rolle.AddChild(alle);
        foreach (var g in Gruppen.Where(g => g.Palette))
        {
            var palette = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false };
            palette.AddThemeConstantOverride("separation", 4);
            alle.AddChild(palette);
            _paletten[g.Kennung] = palette;
        }

        var gruppe = "";
        VBoxContainer liste = null!;
        GridContainer raster = null!;
        void Gruppe(string kennung) { gruppe = kennung; liste = _paletten[kennung]; raster = null!; }
        void Kopf(string titel)
        {
            var kopf = Hauptmenue.Text(liste, titel, 15, Gold);
            kopf.CustomMinimumSize = new Vector2(0, 26);
            kopf.VerticalAlignment = VerticalAlignment.Bottom;
            raster = new GridContainer { Columns = 3 };
            raster.AddThemeConstantOverride("h_separation", 4);
            raster.AddThemeConstantOverride("v_separation", 4);
            liste.AddChild(raster);
        }
        Button Eintrag(string schluessel, string text, string tipp, Werkstattwerkzeug werkzeug, string? icon = null, Action? vorher = null, string? baustein = null, Texture2D? bild = null)
        {
            var knopf = new Button
            {
                Text = text, ToggleMode = true, FocusMode = Control.FocusModeEnum.None, ClipText = false, Icon = bild ?? (icon is null ? null : Symbol(icon)),
                IconAlignment = HorizontalAlignment.Center, VerticalIconAlignment = VerticalAlignment.Top, AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(94, 92), TooltipText = $"{text}\n{tipp}",
            };
            knopf.AddThemeFontSizeOverride("font_size", text.Length > 14 ? 11 : 12);
            knopf.AddThemeConstantOverride("icon_max_width", 60);
            var meineGruppe = gruppe;
            void Waehlen()
            {
                vorher?.Invoke();
                if (_stadt.WerkstattWerkzeugWaehlen(werkzeug, baustein)) _werkzeugName = $"{Gruppen.First(g => g.Kennung == meineGruppe).Name} › {text}";
                else knopf.SetPressedNoSignal(false);
            }
            knopf.Pressed += () =>
            {
                if (knopf.ButtonPressed) Waehlen();
                else _stadt.WerkstattWerkzeugWaehlen(Werkstattwerkzeug.Auswahl);
            };
            raster.AddChild(knopf);
            _katalogKnoepfe[schluessel] = (knopf, Waehlen);
            _eintragGruppe[schluessel] = meineGruppe;
            return knopf;
        }
        void Gebaeude(Baustein b, string text, string tipp, Werkstattwerkzeug werkzeug, string ersatz)
        {
            var knopf = Eintrag(b.Kennung, text, tipp, werkzeug, ersatz, baustein: b.Kennung);
            if (KatalogBild(b.Kennung) is { } vorab) knopf.Icon = vorab;
            else if (_vorschauen.TryGetValue(b.Kennung, out var bild)) knopf.Icon = bild;
            else _vorschauWarte.Add((knopf, b));
        }

        // Gelände (Schritt 5b): Pinsel und Rampe, dazu die Berge
        Gruppe("gelaende");
        Kopf("Pinsel");
        foreach (var (art, name, tipp) in Gelaendepinsel)
            Eintrag("gelaende:" + art.ToString().ToLowerInvariant(), name, tipp + " – mit gedrückter Maus malen (Radius, Stärke und Kante rechts)",
                Werkstattwerkzeug.Gelaende, art.ToString().ToLowerInvariant(), () => _stadt.GelaendeArtWaehlen(art));
        Eintrag("rampe", "Rampe", "Zwei Klicks: Anfang und Ende – dazwischen ein Band mit gleichmäßigem Gefälle (Breite rechts)", Werkstattwerkzeug.Rampe, "rampe");
        var berge = _entwurf.Katalog.Bausteine.Where(b => b.Art == Bausteinart.Berg).ToList();
        if (berge.Count > 0) Kopf("Berge");
        foreach (var b in berge) Gebaeude(b, b.Anzeigename, BausteinTipp(b), Werkstattwerkzeug.Baustein, "gelaende");

        // Boden (Schritt 5e): die elf Texturen mit Farbfeld, dazu der Radierer
        Gruppe("boden");
        Kopf("Texturen");
        foreach (var d in _entwurf.Katalog.Bodentexturen.Where(d => d.Textur is not null))
        {
            var textur = d.Textur!.Value;
            var streut = d.Dichte > 0 && d.Deko.Count > 0 ? "streut " + string.Join(", ", d.Deko.Where(e => e.Value > 0).Select(e => ArtName(e.Key))) : "streut nichts";
            Eintrag("boden:" + d.Kennung, d.Name, $"{d.Name} malen – mit gedrückter Maus (Radius, Stärke, Kante und Deko rechts) · {streut}",
                Werkstattwerkzeug.Boden, vorher: () => (_stadt.BodenArt, _stadt.BodenRadierer) = (textur, false), bild: Farbfeld(_stadt.BodenFarbe(textur)));
        }
        Eintrag("boden:radierer", "Radierer", "Mit gedrückter Maus malen: zurück zur Wiese, ohne Deko", Werkstattwerkzeug.Boden, "radierer", () => _stadt.BodenRadierer = true);

        // Wasser (Schritt 5c): Flüsse und Bäche (Linie, in Fließrichtung), Seen und Teiche (Fläche), Fährstellen, Häfen
        Gruppe("wasser");
        Kopf("Gewässer");
        Eintrag("wasser:fluss", $"Fluss · {Breite(Wasserform.Vorgabebreite("fluss"))}", "Punkte klicken, in Fließrichtung (die Pfeile zeigen sie) – Doppelklick oder Enter legt den Fluss an. Brücken entstehen, wo ein Weg ihn quert.",
            Werkstattwerkzeug.Fluss, "fluss", () => (_stadt.WerkstattFlussart, _stadt.WerkstattFlussbreite) = ("fluss", 0));
        Eintrag("wasser:bach", $"Bach · {Breite(Wasserform.Vorgabebreite("bach"))}", "Wie der Fluss, aber schmal und flach: Wege führen durch eine Furt (unter 0,5 m Tiefe) oder über eine kleine Brücke",
            Werkstattwerkzeug.Fluss, "bach", () => (_stadt.WerkstattFlussart, _stadt.WerkstattFlussbreite) = ("bach", 0));
        Eintrag("wasser:see", "See", "Ecken klicken, Doppelklick schließt – eine stille Wasserfläche mit Wasserstand und Tiefe", Werkstattwerkzeug.See, "see", () => _stadt.WerkstattSeeart = "see");
        Eintrag("wasser:teich", "Teich", "Wie der See, nur klein", Werkstattwerkzeug.See, "teich", () => _stadt.WerkstattSeeart = "teich");
        Eintrag("wasser:faehre", "Fährstelle", "Zwei Klicks, die Anleger an beiden Ufern – sie rasten an Wegen ein. Karren setzen mit der Fähre über (und warten dort länger).", Werkstattwerkzeug.Faehre, "faehre");
        if (_entwurf.Katalog.Finde(Kartenentwurf.HafenKatalog) is { } hafen)
            Gebaeude(hafen, hafen.Anzeigename, "Ein Hafen am Ufer: die Rückseite (Kai) zum Wasser, höchstens 3 m davon entfernt, die Vorderseite (Pfeil) zum Weg – Q/E dreht", Werkstattwerkzeug.Baustein, "hafen");

        // Wege: je Art ein Werkzeug, dazu der Schalter gerade/glatt
        Gruppe("wege");
        var glatt = new CheckButton { Text = "Glatte Kurve", ButtonPressed = _stadt.WerkstattGlatt, FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Glatte Kurve\nAn: eine weiche Kurve durch die Punkte. Aus: gerade Strecken von Punkt zu Punkt." };
        glatt.AddThemeFontSizeOverride("font_size", 14);
        glatt.Toggled += an => _stadt.WerkstattGlatt = an;
        liste.AddChild(glatt);
        Kopf("Wegarten");
        foreach (var klasse in Wegarten)
            Eintrag("weg:" + klasse.ToString().ToLowerInvariant(), $"{Kartenweg.Anzeigename(klasse)} · {Breite(Kartenweg.Standardbreite(klasse))}",
                $"Breite {Breite(Kartenweg.Standardbreite(klasse))}, mindestens {Breite(Kartenweg.Mindestbreite(klasse))}"
                + (Kartenweg.TraegtKarren(klasse) ? " · Karren fahren hier" : " · nur zu Fuß") + " – Punkte klicken, Doppelklick oder Enter beendet",
                Werkstattwerkzeug.Weg, WegIcon(klasse), () => _stadt.WerkstattWegklasse = klasse);

        // Flächen und Natur
        Gruppe("flaechen");
        Kopf("Flächen");
        foreach (var lage in new[] { Lage.Kern, Lage.Mauerring, Lage.Vorstadt, Lage.Umland })
            Eintrag("bauzone:" + lage.ToString().ToLowerInvariant(), $"Bauzone {Stadtansicht.LageName(lage)}",
                "Hier dürfen Spieler und KI bauen – Ecken klicken (auf dem 2-m-Raster), Doppelklick schließt", Werkstattwerkzeug.Bauzone, "bauzone_" + lage.ToString().ToLowerInvariant(),
                () => _stadt.WerkstattZonenlage = lage);
        Eintrag("wald", "Waldfläche", "Mischwald (Tanne, Eiche, Buche) – Ecken klicken, Doppelklick schließt; rechts Dichte und Mischung", Werkstattwerkzeug.Wald, "wald");
        Eintrag("streu", "Streufläche", "Büsche, Gras, Steine und Holz locker verstreut – Ecken klicken, Doppelklick schließt", Werkstattwerkzeug.Streu, "streu");
        Eintrag("feldflaeche", "Feldfläche", "Äcker in Streifen mit Furchen, jedes Stück mit einer Frucht – vor allem für den Randstreifen. Ecken klicken, Doppelklick schließt; rechts Richtung, Streifen und Früchte",
            Werkstattwerkzeug.Feld, "feld");
        Eintrag("aussparung", "Aussparung", "Ein Loch in der gewählten Wald-, Streu- oder Feldfläche – dort wächst nichts", Werkstattwerkzeug.Aussparung, "aussparung");
        Kopf("Natur");
        Eintrag("pinsel", "Pinsel", "Mit gedrückter Maus malen – beim Loslassen wachsen dort Pflanzen (Radius, Dichte und Arten rechts)", Werkstattwerkzeug.Pinsel, "pinsel");
        Eintrag("radierer", "Radierer", "Mit gedrückter Maus über einzelne Pflanzen fahren – beim Loslassen sind sie weg", Werkstattwerkzeug.Radierer, "radierer");
        foreach (var b in _entwurf.Katalog.Bausteine.Where(b => b.Art == Bausteinart.Natur && b.Modell?.StartsWith("props/") != true))
            Eintrag(b.Kennung, ArtName(b), $"{b.Kennung} · {b.Varianten} Variante(n) · Modell {b.Modell} – Klick setzt eine Pflanze, Q/E dreht",
                Werkstattwerkzeug.Pflanze, "pflanze", baustein: b.Kennung, bild: KatalogBild(b.Kennung));
        Kopf("Requisiten (Asset-Kits)");
        foreach (var b in _entwurf.Katalog.Bausteine.Where(b => b.Art == Bausteinart.Natur && b.Modell?.StartsWith("props/") == true))
            Eintrag(b.Kennung, ArtName(b), $"{b.Kennung} · {b.Varianten} Variante(n) · Modell {b.Modell} – Klick stellt es auf, Q/E dreht, Maßstab rechts",
                Werkstattwerkzeug.Pflanze, "gebaeude", baustein: b.Kennung, bild: KatalogBild(b.Kennung));

        // Gebäude
        Gruppe("gebaeude");
        foreach (var (titel, arten) in Katalogteile.Where(t => t.Arten[0] != Bausteinart.Berg))
        {
            var bausteine = _entwurf.Katalog.Bausteine.Where(b => arten.Contains(b.Art)).ToList();
            if (bausteine.Count == 0) continue;
            Kopf(titel);
            foreach (var b in bausteine)
                Gebaeude(b, b.Anzeigename, BausteinTipp(b), Werkstattwerkzeug.Baustein, arten[0] == Bausteinart.Umlandbetrieb ? "umland" : "gebaeude");
        }

        // Mauer und Tore
        Gruppe("mauer");
        Kopf("Mauer");
        Eintrag("mauer", "Mauer", "Ecken klicken (gerade Stücke) – Klick auf die erste Ecke schließt den Ring, Doppelklick oder Enter lässt sie offen. Türme setzt das Spiel selbst.",
            Werkstattwerkzeug.Mauer, "mauer");
        Eintrag("tor", "Tor", "Klick auf eine Mauer setzt dort ein Tor (es rastet auf die Linie); ein Weg durch das Tor rastet in seiner Mitte ein", Werkstattwerkzeug.Tor, "tor");
    }

    private static string BausteinTipp(Baustein b)
    {
        var masse = b.Groesse is { Length: 2 } g ? $"{Kartenschreiber.Zahl(g[0])} × {Kartenschreiber.Zahl(g[1])} m" : "ohne Grundfläche";
        return $"{b.Kennung} · Grundfläche {masse} (mit 2 m Vorplatz vorn)" + (b.Modell is { } m ? $" · Modell {m}" : " · noch ohne Modell");
    }

    private static string WegIcon(Wegklasse klasse) => klasse switch
    {
        Wegklasse.Land => "weg_land",
        Wegklasse.Haupt or Wegklasse.Ring => "weg_haupt",
        Wegklasse.Feldweg => "weg_feld",
        Wegklasse.Fussweg or Wegklasse.Leinpfad => "weg_fuss",
        _ => "weg_gasse",
    };

    /// <summary>
    /// Wählt ein Werkzeug wie ein Klick in der Palette und klappt dafür die Gruppe auf (Startoptionen <c>--werkstatt-umriss</c>, <c>--werkstatt-werkzeug</c>,
    /// Klick auf einen Befund, Knöpfe der Tafel rechts): Schlüssel wie in <see cref="_katalogKnoepfe"/>; „weg“ allein ist die Gasse, „bauzone“ allein die Vorstadt.
    /// </summary>
    private void KatalogWaehlen(string schluessel)
    {
        schluessel = schluessel switch { "weg" => "weg:gasse", "bauzone" => "bauzone:vorstadt", "flaeche" => "wald", _ => schluessel };
        if (!_katalogKnoepfe.TryGetValue(schluessel, out var eintrag)) throw new ArgumentException($"Das Werkzeug „{schluessel}“ gibt es in der Palette nicht.");
        KatalogWahlAufheben();
        PaletteOeffnen(_eintragGruppe[schluessel]);
        eintrag.Knopf.SetPressedNoSignal(true);
        eintrag.Waehlen();
        Callable.From(() => _katalogRolle.EnsureControlVisible(eintrag.Knopf)).CallDeferred(); // nach dem Aufbau der Tafel (Startoptionen)
    }

    private void KatalogWahlAufheben()
    {
        _werkzeugName = "Auswählen";
        foreach (var (knopf, _) in _katalogKnoepfe.Values) knopf.SetPressedNoSignal(false);
    }

    /// <summary>
    /// <c>--werkstatt-gruppe=&lt;kennung&gt;</c> (für Klicktests): klappt die Palette einer Gruppe auf (auswaehlen, gelaende, boden, wasser, wege, flaechen,
    /// gebaeude, mauer, start, pruefen).
    /// </summary>
    private void GruppeOeffnen(string kennung)
    {
        if (Gruppen.All(g => g.Kennung != kennung)) throw new ArgumentException($"Die Gruppe „{kennung}“ gibt es nicht ({string.Join(", ", Gruppen.Select(g => g.Kennung))}).");
        if (kennung is "start" or "pruefen" or "auswaehlen" || _paletteGruppe != kennung) GruppeWaehlen(kennung);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Vorschaubilder der Gebäude
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die Vorschaubilder nach Baustein – statisch, damit sie Öffnen, Neu und Ausprobieren überleben. Jedes Gebäude wird einmal in einen eigenen
    /// <see cref="SubViewport"/> gerendert (eigene Welt, schräge Sicht von vorn, schlichtes Licht) – beim ersten Aufklappen von Gebäude oder Gelände,
    /// nicht beim Start: Die Modelle laden sonst alle auf einmal.
    /// </summary>
    private static readonly Dictionary<string, Texture2D> _vorschauen = new();

    private bool _vorschauLaeuft;

    private async void VorschauenRendern()
    {
        if (_vorschauLaeuft || _vorschauWarte.Count == 0) return;
        _vorschauLaeuft = true;
        var auftraege = _vorschauWarte.ToList();
        _vorschauWarte.Clear();
        var halter = new Node { Name = "Vorschauen" };
        AddChild(halter);
        var bilder = new List<(Button Knopf, string Kennung, SubViewport Fenster)>();
        foreach (var (knopf, baustein) in auftraege)
        {
            if (_stadt.WerkstattVorschaumodell(baustein) is not { } modell) continue;
            var fenster = VorschauFenster(halter, modell);
            bilder.Add((knopf, baustein.Kennung, fenster));
        }
        // zwei gezeichnete Bilder abwarten, dann sind die Fenster fertig
        for (var i = 0; i < 3; i++) await ToSignal(RenderingServer.Singleton, RenderingServerInstance.SignalName.FramePostDraw);
        if (!IsInstanceValid(this) || !IsInsideTree()) return;
        foreach (var (knopf, kennung, fenster) in bilder)
        {
            var textur = ImageTexture.CreateFromImage(fenster.GetTexture().GetImage());
            _vorschauen[kennung] = textur;
            foreach (var (k, b) in auftraege.Where(a => a.Baustein.Kennung == kennung))
                if (IsInstanceValid(k)) k.Icon = textur;
        }
        halter.QueueFree();
        _vorschauLaeuft = false;
    }

    /// <summary>Ein Fenster mit eigener Welt für ein Vorschaubild: neutrales Licht, durchsichtiger Hintergrund, Dreiviertelansicht von vorn (rechts oben) auf das Modell.</summary>
    private static SubViewport VorschauFenster(Node halter, Node3D modell, int groesse = 128)
    {
        var fenster = new SubViewport
        {
            Size = new Vector2I(groesse, groesse), TransparentBg = true, OwnWorld3D = true, Msaa3D = Viewport.Msaa.Msaa4X,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
        };
        halter.AddChild(fenster);
        fenster.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment { AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.85f, 0.82f, 0.76f), AmbientLightEnergy = 0.8f },
        });
        fenster.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-48, 32, 0), LightEnergy = 1.1f });
        fenster.AddChild(modell);
        var masse = ModellMasse(modell);
        var mitte = masse.GetCenter();
        var kamera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = Mathf.Max(masse.Size.Length() * 0.62f, 0.4f), Near = 0.1f, Far = 600f };
        fenster.AddChild(kamera);
        kamera.Position = mitte + new Vector3(0.55f, 0.6f, 1f).Normalized() * 150f;
        kamera.LookAt(mitte, Vector3.Up);
        return fenster;
    }

    /// <summary>Alles Sichtbare eines Modells in Weltmaßen (die Fenster der Vorschau haben ihre eigene Welt, der Knoten steht schon darin).</summary>
    private static Aabb ModellMasse(Node3D wurzel)
    {
        var alle = wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().Where(m => m.IsVisibleInTree() && m.Mesh is not null).ToList();
        if (alle.Count == 0) return new Aabb(Vector3.Zero, Vector3.One * 4);
        var masse = alle[0].GlobalTransform * alle[0].GetAabb();
        foreach (var m in alle.Skip(1)) masse = masse.Merge(m.GlobalTransform * m.GetAabb());
        return masse;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Eigenschaften in klappbaren Abschnitten
    // ═════════════════════════════════════════════════════════════════════════

    private bool _abschnitteVorgemerkt;

    /// <summary>Leert die Tafel „Eigenschaften“ für einen neuen Aufbau; danach (am Ende des Bildes) klappen die Überschriften zu Abschnitten.</summary>
    private void EigenschaftenLeeren()
    {
        UiHilfe.KinderEntfernen(_eigenschaften);
        if (_abschnitteVorgemerkt) return;
        _abschnitteVorgemerkt = true;
        Callable.From(AbschnitteBilden).CallDeferred();
    }

    /// <summary>
    /// Macht aus jeder Zwischenüberschrift (gold, Größe 15) der Tafel einen Knopf, der alles bis zur nächsten Überschrift ein- und ausklappt –
    /// bei mindestens zwei Überschriften. Die Felder selbst bleiben dieselben (nur in einen Behälter verschoben), ihre Werte setzen sich weiter nach.
    /// </summary>
    private void AbschnitteBilden()
    {
        _abschnitteVorgemerkt = false;
        if (!IsInstanceValid(_eigenschaften)) return;
        // die wirkliche Größe einer Überschrift der Größe 15 (Hauptmenue.Text rechnet sie um) von einem Probe-Label ablesen
        var probe = Hauptmenue.Text(_eigenschaften, "", 15, Gold);
        var kopfGroesse = probe.GetThemeFontSize("font_size");
        _eigenschaften.RemoveChild(probe);
        probe.Free();
        bool Kopf(Node n) => n is Label l && l.GetThemeFontSize("font_size") == kopfGroesse && l.GetThemeColor("font_color") == Gold;
        var kinder = _eigenschaften.GetChildren().ToList();
        if (kinder.Count(Kopf) < 2) return;
        foreach (var kind in kinder) _eigenschaften.RemoveChild(kind);
        VBoxContainer? inhalt = null;
        foreach (var kind in kinder)
        {
            if (kind is Label kopf && Kopf(kind))
            {
                var titel = kopf.Text;
                var knopf = new Button
                {
                    Text = "– " + titel, Flat = true, ToggleMode = true, ButtonPressed = true, Alignment = HorizontalAlignment.Left, FocusMode = Control.FocusModeEnum.None,
                    TooltipText = "Abschnitt ein- oder ausklappen",
                };
                knopf.AddThemeFontSizeOverride("font_size", 15);
                knopf.AddThemeColorOverride("font_color", Gold);
                knopf.AddThemeColorOverride("font_pressed_color", Gold);
                knopf.AddThemeColorOverride("font_hover_color", Hell);
                var behaelter = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                behaelter.AddThemeConstantOverride("separation", 6);
                knopf.Toggled += an =>
                {
                    behaelter.Visible = an;
                    knopf.Text = (an ? "– " : "+ ") + titel;
                };
                _eigenschaften.AddChild(knopf);
                _eigenschaften.AddChild(behaelter);
                inhalt = behaelter;
                kopf.QueueFree();
            }
            else (inhalt ?? _eigenschaften).AddChild(kind);
        }
    }
}
