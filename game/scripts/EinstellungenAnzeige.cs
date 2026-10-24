using Godot;

namespace Reichsgilde;

/// <summary>
/// Fenster „Einstellungen“ mit fünf Reitern: <b>Voreinstellung</b> (Niedrig bis Ultra, automatische Wahl, Vollbild), <b>Leistung</b>
/// (Bildrate, VSync, dynamische Auflösung, FSR 2.2, Detailstufe), <b>Bild</b> (Oberflächendetails, Kantenglättung, Nachschärfen,
/// Schatten, Umgebungsverdeckung, indirektes Licht), <b>Stimmung</b> (Bloom, Miniatur-Unschärfe, Wolkenschatten, Morgendunst,
/// Farbstimmung, Fahnen) und <b>Barrierefreiheit</b> (Größe der Oberfläche, Farbfilter). Erreichbar aus dem Hauptmenü und aus dem
/// Spielmenü (Esc). Jede Änderung wirkt sofort und wird gespeichert (siehe <see cref="Fenster"/>).
/// </summary>
public sealed class EinstellungenAnzeige
{
    /// <summary>Anzeigename und kurze Erklärung je Verfahren – in der Reihenfolge der Auswahlliste.</summary>
    private static readonly (Kantenglaettung Art, string Name, string Erklaerung)[] Verfahren =
    {
        (Kantenglaettung.Aus, "Aus", "Keine Glättung. Am schnellsten, aber Kanten sind gezackt."),
        (Kantenglaettung.Fxaa, "FXAA", "Sehr schnell. Glättet das fertige Bild, macht es dabei leicht weich."),
        (Kantenglaettung.Smaa, "SMAA (empfohlen)", "Schnell. Sucht Kanten im fertigen Bild und glättet sie, das Bild bleibt scharf."),
        (Kantenglaettung.Taa, "TAA", "Schnell und sehr glatt. Nutzt die letzten Bilder mit, bewegte Dinge ziehen leichte Schlieren."),
        (Kantenglaettung.Msaa2, "MSAA 2×", "Berechnet jede Kante mehrfach: beste Kanten ohne Unschärfe. Kostet mehr Grafikleistung."),
        (Kantenglaettung.Msaa4, "MSAA 4×", "Wie MSAA 2×, noch glatter. Kostet deutlich mehr Grafikleistung."),
        (Kantenglaettung.Msaa8, "MSAA 8×", "Höchste Stufe. Nur für starke Grafikkarten."),
    };

    /// <summary>Anzeigename und Erklärung je Stufe der Schattenqualität – in der Reihenfolge der Auswahlliste (Werte: Stadtansicht.Licht.cs).</summary>
    private static readonly (Schattenqualitaet Stufe, string Name, string Erklaerung)[] Schattenstufen =
    {
        (Schattenqualitaet.Niedrig, "Niedrig", "Kleine Schattenkarte, wenig Proben. Für schwache Grafikkarten; Schattenkanten sind weicher und gröber."),
        (Schattenqualitaet.Mittel, "Mittel", "Halbe Auflösung der Schattenkarte. Saubere Schatten, spart Grafikspeicher und Leistung."),
        (Schattenqualitaet.Hoch, "Hoch (empfohlen)", "Große Schattenkarte, feiner Filter: scharfe, glatte Schatten bis an den Fuß jeder Figur."),
    };

    /// <summary>Anzeigename und Erklärung je Stufe der Umgebungsverdeckung – in der Reihenfolge der Auswahlliste (Werte: Stadtansicht.Licht.cs).</summary>
    private static readonly (Umgebungsverdeckung Stufe, string Name, string Erklaerung)[] Verdeckungsstufen =
    {
        (Umgebungsverdeckung.Aus, "Aus", "Keine Verdunkelung in Ecken und an Fußpunkten. Am schnellsten."),
        (Umgebungsverdeckung.Niedrig, "Niedrig", "Ecken, Fugen und Fußpunkte etwas dunkler, die Häuser stehen fester. Wenig Proben, kostet wenig."),
        (Umgebungsverdeckung.Mittel, "Mittel (empfohlen)", "Wie Niedrig, mit mehr Proben: gleichmäßiger, weniger Körnung."),
        (Umgebungsverdeckung.Hoch, "Hoch", "Volle Auflösung und viele Proben: feinste Fugen. Kostet deutlich mehr Grafikleistung."),
    };

    /// <summary>Die Voreinstellungen mit Erklärung (Werte: <see cref="Fenster.StufeVon"/>).</summary>
    private static readonly (Voreinstellung Stufe, string Erklaerung)[] Voreinstellungen =
    {
        (Voreinstellung.Niedrig, "Für schwache Grafikkarten: FXAA, kleine Schatten, keine Umgebungsverdeckung, kein Bloom, Oberflächen ohne Struktur, einfacher Boden, halb so viel Streu und Passanten."),
        (Voreinstellung.Mittel, "SMAA, mittlere Schatten, wenig Umgebungsverdeckung, Bloom, Oberflächendetails, Boden ohne Unebenheit im Licht, Wolkenschatten; drei Viertel der Streu und Passanten."),
        (Voreinstellung.Hoch, "Das empfohlene Bild: SMAA, große Schatten, Umgebungsverdeckung, Bloom, Oberflächendetails, leichte Miniatur-Unschärfe, Wolkenschatten und einfacher Morgendunst."),
        (Voreinstellung.Ultra, "Für starke Grafikkarten: wie Hoch, dazu MSAA 4×, feinste Umgebungsverdeckung, indirektes Licht und Morgendunst mit Lichtstrahlen."),
        (Voreinstellung.Eigene, "Eigene Wahl: Mindestens ein Regler weicht von den Stufen ab. Eine Stufe wählen stellt alle Regler wieder ein."),
    };

    private static readonly (VSync Art, string Name, string Erklaerung)[] VsyncArten =
    {
        (VSync.Aus, "Aus", "Zeichnet so schnell es geht. Am wenigsten Verzögerung, aber bei schnellen Kamerafahrten können Risse im Bild entstehen."),
        (VSync.An, "An (empfohlen)", "Wartet mit jedem Bild auf den Bildschirm: kein Riss. Schafft das Spiel die Bildrate nicht, fällt es auf die Hälfte."),
        (VSync.Adaptiv, "Adaptiv", "Wartet nur, solange das Spiel schnell genug ist; wird es langsamer, zeichnet es sofort (kurze Risse statt Ruckeln)."),
    };

    private static readonly int[] Bildraten = { 30, 60, 90, 120, 144, 0 };
    private static readonly int[] Zielraten = { 0, 30, 45, 60, 90, 120, 144 };

    private static readonly (Feinheiten Stufe, string Name)[] Sichtweiten =
    {
        (Feinheiten.Nah, "Nah"), (Feinheiten.Mittel, "Mittel"), (Feinheiten.Weit, "Weit (empfohlen)"),
    };

    private static readonly (Bodenqualitaet Stufe, string Name, string Erklaerung)[] Bodenstufen =
    {
        (Bodenqualitaet.Niedrig, "Niedrig", "Die Bodentexturen gehen weich ineinander über. Kostet am wenigsten."),
        (Bodenqualitaet.Mittel, "Mittel", "Scharfe, unregelmäßige Grenzen (Gras wächst über die Erde, Steine ragen aus dem Kies) und keine sichtbaren Wiederholungen."),
        (Bodenqualitaet.Hoch, "Hoch (empfohlen)", "Dazu Unebenheit und Glanz im Licht, Fels am Hang ohne Verzerrung und kleine Grasbüschel an den Grenzen der Wiese."),
    };

    private static readonly (Grasteppich Stufe, string Name, string Erklaerung)[] Grasstufen =
    {
        (Grasteppich.Aus, "Aus", "Die Wiese ist nur die Bodentextur. Kostet nichts."),
        (Grasteppich.Dicht, "Dicht (empfohlen)", "Ein Teppich kleiner Grashalme auf der Wiese, im Wind wiegend, an Wegen niedergetreten."),
        (Grasteppich.SehrDicht, "Sehr dicht", "Doppelt so viele Halme. Kostet mehr Grafikleistung."),
    };

    private static readonly (Miniaturunschaerfe Stufe, string Name)[] Unschaerfen =
    {
        (Miniaturunschaerfe.Aus, "Aus"), (Miniaturunschaerfe.Leicht, "Leicht (empfohlen)"), (Miniaturunschaerfe.Stark, "Stark"),
    };

    private static readonly (Morgendunst Stufe, string Name, string Erklaerung)[] Dunststufen =
    {
        (Morgendunst.Aus, "Aus", "Klare Luft zu jeder Stunde."),
        (Morgendunst.Niedrig, "Niedrig", "Ein Schleier am Boden: morgens dicht, mittags fast weg, abends leicht. Kostet fast nichts."),
        (Morgendunst.Hoch, "Hoch", "Echter Nebel in der Luft: Die tief stehende Sonne zieht Lichtbahnen hindurch, Häuser und Mauern werfen Schatten hinein; in den Räumen tanzt Staub im Licht der Fenster. Kostet deutlich Grafikleistung."),
    };

    private static readonly (Tonemapping Art, string Name, string Erklaerung)[] Tonemappings =
    {
        (Tonemapping.Bisher, "Linear (bisher)", "Kräftige Farben; sehr helle Stellen (Glut, Fensterlicht) werden einfach abgeschnitten."),
        (Tonemapping.Agx, "AgX", "Wie ein Film: helle Stellen laufen weich aus und werden heller statt bunter, die Farben wirken ruhiger und natürlicher."),
    };

    private static readonly (Farbfilter Art, string Name, string Erklaerung)[] Farbfilterarten =
    {
        (Farbfilter.Aus, "Aus", "Keine Änderung der Farben."),
        (Farbfilter.Protanopie, "Protanopie (Rot schwach)", "Rot ist schwer von Grün zu unterscheiden, Rot wirkt dunkel. Der Filter schiebt Rot-Grün-Unterschiede ins Blaue und in die Helligkeit."),
        (Farbfilter.Deuteranopie, "Deuteranopie (Grün schwach)", "Die häufigste Form: Grün und Rot sind schwer zu trennen. Der Filter schiebt die Unterschiede ins Blaue und in die Helligkeit."),
        (Farbfilter.Tritanopie, "Tritanopie (Blau schwach)", "Blau und Gelb sind schwer zu trennen. Der Filter schiebt die Unterschiede ins Rote und Grüne."),
    };

    /// <summary>Die Reiter in ihrer Reihenfolge (Name auch für die Startoption <c>--reiter</c>).</summary>
    private static readonly string[] Reiternamen = { "Voreinstellung", "Leistung", "Bild", "Stimmung", "Barrierefreiheit" };

    private const string ErklaerungKantenFsr = "FSR 2.2 glättet selbst. Diese Wahl gilt wieder, wenn FSR aus ist.";
    private static readonly Color Grau = new(0.75f, 0.75f, 0.75f);
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private const int Umbruch = 500;

    public PanelContainer Tafel { get; }
    private readonly List<Button> _reiter = new();
    private readonly List<VBoxContainer> _seiten = new();

    // Voreinstellung
    private readonly OptionButton _voreinstellung;
    private readonly Label _voreinstellungErklaerung;
    private readonly Button _automatisch;
    private readonly Label _automatischErgebnis;
    private readonly CheckButton _vollbild;
    // Leistung
    private readonly OptionButton _bildrate;
    private readonly OptionButton _vsync;
    private readonly Label _vsyncErklaerung;
    private readonly OptionButton _dynamisch;
    private readonly Label _dynamischStand;
    private readonly CheckButton _fsr;
    private readonly HBoxContainer _fsrZeile;
    private readonly HSlider _fsrRegler;
    private readonly Label _fsrWert;
    private readonly HSlider _natur;
    private readonly Label _naturWert;
    private readonly HSlider _passanten;
    private readonly Label _passantenWert;
    private readonly OptionButton _feinheiten;
    // Bild
    private readonly CheckButton _oberflaeche;
    private readonly OptionButton _boden;
    private readonly Label _bodenErklaerung;
    private readonly OptionButton _gras;
    private readonly Label _grasErklaerung;
    private readonly OptionButton _kanten;
    private readonly Label _erklaerung;
    private readonly HSlider _schaerfe;
    private readonly Label _schaerfeWert;
    private readonly OptionButton _schatten;
    private readonly Label _schattenErklaerung;
    private readonly OptionButton _verdeckung;
    private readonly Label _verdeckungErklaerung;
    private readonly CheckButton _indirekt;
    // Stimmung
    private readonly CheckButton _bloom;
    private readonly HSlider _bloomRegler;
    private readonly Label _bloomWert;
    private readonly OptionButton _unschaerfe;
    private readonly CheckButton _wolken;
    private readonly OptionButton _dunst;
    private readonly Label _dunstErklaerung;
    private readonly OptionButton _tonemapping;
    private readonly Label _tonemappingErklaerung;
    private readonly HSlider _helligkeit;
    private readonly Label _helligkeitWert;
    private readonly HSlider _kontrast;
    private readonly Label _kontrastWert;
    private readonly HSlider _saettigung;
    private readonly Label _saettigungWert;
    private readonly CheckButton _fahnen;
    // Barrierefreiheit
    private readonly HSlider _groesse;
    private readonly Label _groesseWert;
    private bool _groesseZiehen;
    private readonly OptionButton _farbfilter;
    private readonly Label _farbfilterErklaerung;

    public EinstellungenAnzeige(Action zurueck)
    {
        var (tafel, spalte) = Hauptmenue.Tafel(560);
        Tafel = tafel;
        Hauptmenue.Text(spalte, "Einstellungen", 26, Gold, zentriert: true);

        // Reiterleiste: fünf Knöpfe, der gewählte in Gold
        var leiste = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        leiste.AddThemeConstantOverride("separation", 4);
        spalte.AddChild(leiste);
        var gruppe = new ButtonGroup();
        for (var i = 0; i < Reiternamen.Length; i++)
        {
            var nummer = i;
            var knopf = new Button { Text = Reiternamen[i], ToggleMode = true, ButtonGroup = gruppe, FocusMode = Control.FocusModeEnum.None };
            knopf.AddThemeFontSizeOverride("font_size", 15);
            knopf.AddThemeColorOverride("font_pressed_color", Gold);
            knopf.AddThemeColorOverride("font_hover_pressed_color", Gold);
            knopf.Pressed += () => ReiterZeigen(nummer);
            leiste.AddChild(knopf);
            _reiter.Add(knopf);
        }
        spalte.AddChild(new HSeparator());
        foreach (var _ in Reiternamen)
        {
            var seite = new VBoxContainer();
            seite.AddThemeConstantOverride("separation", 10);
            spalte.AddChild(seite);
            _seiten.Add(seite);
        }

        // ── Voreinstellung ───────────────────────────────────────────────────
        var s = _seiten[0];
        Abschnitt(s, "Voreinstellung");
        _voreinstellung = Auswahl(s, Voreinstellungen.Select(v => Fenster.Name(v.Stufe)), nummer =>
        {
            Fenster.VoreinstellungSetzen(Voreinstellungen[nummer].Stufe);
            Auffrischen();
        });
        _voreinstellung.SetItemDisabled(Voreinstellungen.Length - 1, true); // „Eigene“ ergibt sich, wählen lässt es sich nicht
        _voreinstellungErklaerung = Hinweis(s, "");
        Hinweis(s, "Eine Stufe stellt alle Regler für Leistung, Bild und die teuren Stimmungen (Unschärfe, Wolken, Dunst) ein. " +
                   "Farbstimmung, Bildrate, VSync, Fahnen und Barrierefreiheit bleiben, wie sie sind.");
        _automatisch = Hauptmenue.Knopf(s, "Automatisch wählen", () =>
        {
            Fenster.AutomatischWaehlen();
            Auffrischen();
        });
        _automatisch.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        _automatischErgebnis = Hinweis(s, "");
        Hinweis(s, "Misst ein paar Sekunden lang, wie schnell dieser Rechner die Stadt zeichnet, und wählt die höchste Stufe, die " +
                   "flüssig bleibt (60 Bilder je Sekunde mit Reserve). Beim ersten Start geschieht das von selbst.");
        Abschnitt(s, "Anzeige");
        _vollbild = Schalter(s, "Vollbild (F11)", an => { if (an != Fenster.IstVollbild) Fenster.VollbildSetzen(an); });

        // ── Leistung ─────────────────────────────────────────────────────────
        s = _seiten[1];
        Abschnitt(s, "Bildrate begrenzen");
        _bildrate = Auswahl(s, Bildraten.Select(b => b == 0 ? "Unbegrenzt" : $"{b} Bilder je Sekunde"), nummer =>
        {
            Fenster.BildrateSetzen(Bildraten[nummer]);
            Auffrischen();
        });
        Hinweis(s, "Höchstens so viele Bilder je Sekunde: spart Strom, hält Lüfter und Grafikkarte kühler.");
        Abschnitt(s, "VSync");
        _vsync = Auswahl(s, VsyncArten.Select(v => v.Name), nummer =>
        {
            Fenster.VsyncSetzen(VsyncArten[nummer].Art);
            Auffrischen();
        });
        _vsyncErklaerung = Hinweis(s, "");
        Abschnitt(s, "Dynamische Auflösung");
        _dynamisch = Auswahl(s, Zielraten.Select(z => z == 0 ? "Aus" : $"Ziel {z} Bilder je Sekunde"), nummer =>
        {
            Fenster.DynamischeAufloesungSetzen(Zielraten[nummer]);
            Auffrischen();
        });
        _dynamischStand = Hinweis(s, "");
        Hinweis(s, "Wird es eng, rechnet das Spiel das 3D-Bild kleiner (bis 50 %) und holt es auf volle Größe – mit FSR 2.2, wenn es an ist, " +
                   "sonst einfach vergrößert (weicher). Geregelt wird ruhig, in kleinen Schritten alle paar Sekunden. Tafeln und Schrift bleiben scharf.");
        Abschnitt(s, "Hochskalierung");
        _fsr = Schalter(s, "FSR 2.2 (Hochskalierung)", an =>
        {
            Fenster.FsrSetzen(an, Fenster.Renderaufloesung);
            Auffrischen();
        });
        (_fsrRegler, _fsrWert, _fsrZeile) = Regler(s, "Renderauflösung", 25, 100, 1, " %", wert =>
        {
            Fenster.FsrSetzen(Fenster.FsrAn, wert);
            Auffrischen();
        });
        Hinweis(s, "Rechnet das Bild kleiner und holt es aus den letzten Bildern auf volle Größe: spart Grafikleistung, glättet selbst. 67 % = AMD-Stufe „Qualität“. Bewegtes kann leicht schlieren.");
        Abschnitt(s, "Detailstufe");
        (_natur, _naturWert, _) = Regler(s, "Natur-Dichte", 25, 100, 5, " %", wert =>
        {
            Fenster.NaturDichteSetzen(wert);
            Auffrischen();
        });
        Hinweis(s, "Anteil von Gras, Kräutern, Büschen, Steinen und Holz vor der Stadt; Bäume dünnen nur halb so stark aus.");
        (_passanten, _passantenWert, _) = Regler(s, "Passanten", 25, 100, 5, " %", wert =>
        {
            Fenster.PassantenMengeSetzen(wert);
            Auffrischen();
        });
        Hinweis(s, "Wie viele Einwohner als Figur auf Gassen und Markt zu sehen sind. Die Stadt rechnet weiter mit allen – nur weniger Figuren werden gezeichnet.");
        var feinZeile = Zeile(s, "Sichtweite der Feinheiten");
        _feinheiten = Auswahl(feinZeile, Sichtweiten.Select(f => f.Name), nummer =>
        {
            Fenster.FeinheitenSetzen(Sichtweiten[nummer].Stufe);
            Auffrischen();
        });
        Hinweis(s, "Bis zu welchem Zoom Bodenpflanzen, Kiesel und Holz gezeichnet werden. Nah: nur von nahem, und weit Entferntes wird vereinfacht " +
                   "(dann werfen Spitzdächer und Zinnen keine feinen Schatten mehr).");

        // ── Bild ─────────────────────────────────────────────────────────────
        s = _seiten[2];
        Abschnitt(s, "Oberflächen");
        _oberflaeche = Schalter(s, "Oberflächendetails", an =>
        {
            Fenster.OberflaechendetailsSetzen(an);
            Auffrischen();
        });
        Hinweis(s, "Mauerwerk, Ziegel, Stroh, Schiefer, Holzmaserung, Laub und Boden mit feiner Struktur und leichter Unebenheit im Licht. Aus: nur die Farben der Modelle – kostet weniger Grafikleistung.");
        var bodenZeile = Zeile(s, "Boden");
        _boden = Auswahl(bodenZeile, Bodenstufen.Select(b => b.Name), nummer =>
        {
            Fenster.BodenqualitaetSetzen(Bodenstufen[nummer].Stufe);
            Auffrischen();
        });
        _bodenErklaerung = Hinweis(s, "");
        var grasZeile = Zeile(s, "Gras");
        _gras = Auswahl(grasZeile, Grasstufen.Select(g => g.Name), nummer =>
        {
            Fenster.GrasteppichSetzen(Grasstufen[nummer].Stufe);
            Auffrischen();
        });
        _grasErklaerung = Hinweis(s, "");
        Abschnitt(s, "Kantenglättung");
        _kanten = Auswahl(s, Verfahren.Select(v => v.Name), nummer =>
        {
            Fenster.KantenglaettungSetzen(Verfahren[nummer].Art);
            Auffrischen();
        });
        _erklaerung = Hinweis(s, "");
        (_schaerfe, _schaerfeWert, _) = Regler(s, "Nachschärfen", 0, 100, 5, "", wert =>
        {
            Fenster.SchaerfeSetzen(wert);
            Auffrischen();
        });
        Hinweis(s, "Holt feine Kanten zurück, die Glättung oder Hochskalierung weich gemacht haben. Mit FSR 2.2 ist es dessen eigene Schärfe. 0 = aus.");
        Abschnitt(s, "Schattenqualität");
        _schatten = Auswahl(s, Schattenstufen.Select(v => v.Name), nummer =>
        {
            Fenster.SchattenqualitaetSetzen(Schattenstufen[nummer].Stufe);
            Auffrischen();
        });
        _schattenErklaerung = Hinweis(s, "");
        Abschnitt(s, "Umgebungsverdeckung");
        _verdeckung = Auswahl(s, Verdeckungsstufen.Select(v => v.Name), nummer =>
        {
            Fenster.UmgebungsverdeckungSetzen(Verdeckungsstufen[nummer].Stufe);
            Auffrischen();
        });
        _verdeckungErklaerung = Hinweis(s, "");
        _indirekt = Schalter(s, "Indirektes Licht (SSIL)", an =>
        {
            Fenster.IndirektesLichtSetzen(an);
            Auffrischen();
        });
        Hinweis(s, "Flächen strahlen etwas Farbe auf ihre Nachbarn zurück. Zurückhaltend; kostet Grafikleistung.");

        // ── Stimmung ─────────────────────────────────────────────────────────
        s = _seiten[3];
        Abschnitt(s, "Bloom");
        _bloom = Schalter(s, "Lichtschein (Bloom)", an =>
        {
            Fenster.BloomSetzen(an, Fenster.BloomStaerke);
            Auffrischen();
        });
        (_bloomRegler, _bloomWert, _) = Regler(s, "Stärke", 0, 100, 5, " %", wert => Fenster.BloomSetzen(Fenster.BloomAn, wert));
        Hinweis(s, "Heller Schein um Fensterlicht, Glut und Domfenster. Sonnenbeschienene Flächen bleiben klar. 50 % ist das empfohlene Bild; kostet etwas Grafikleistung.");
        Abschnitt(s, "Miniatur-Unschärfe");
        _unschaerfe = Auswahl(s, Unschaerfen.Select(u => u.Name), nummer =>
        {
            Fenster.UnschaerfeSetzen(Unschaerfen[nummer].Stufe);
            Auffrischen();
        });
        Hinweis(s, "Oben und unten im Bild wird die Stadt weich, die Mitte bleibt scharf – wie ein Foto eines Modells (Tilt-Shift). Nah stärker als weit. " +
                   "Tafeln und Räume bleiben scharf. Kostet etwas Grafikleistung.");
        Abschnitt(s, "Wolkenschatten");
        _wolken = Schalter(s, "Wolkenschatten", an =>
        {
            Fenster.WolkenschattenSetzen(an);
            Auffrischen();
        });
        Hinweis(s, "Schatten einzelner Wolken ziehen langsam mit dem Wind über Felder, Gassen und Dächer – nur bei Sonne.");
        Abschnitt(s, "Morgendunst und Lichtstrahlen");
        _dunst = Auswahl(s, Dunststufen.Select(d => d.Name), nummer =>
        {
            Fenster.DunstSetzen(Dunststufen[nummer].Stufe);
            Auffrischen();
        });
        _dunstErklaerung = Hinweis(s, "");
        Abschnitt(s, "Farbstimmung");
        _tonemapping = Auswahl(s, Tonemappings.Select(t => t.Name), nummer =>
        {
            Fenster.FarbstimmungSetzen(Tonemappings[nummer].Art, Fenster.Helligkeit, Fenster.Kontrast, Fenster.Saettigung);
            Auffrischen();
        });
        _tonemappingErklaerung = Hinweis(s, "");
        (_helligkeit, _helligkeitWert, _) = Regler(s, "Helligkeit", 50, 150, 5, " %", wert =>
            Fenster.FarbstimmungSetzen(Fenster.AktuellesTonemapping, wert, Fenster.Kontrast, Fenster.Saettigung));
        (_kontrast, _kontrastWert, _) = Regler(s, "Kontrast", 50, 150, 5, " %", wert =>
            Fenster.FarbstimmungSetzen(Fenster.AktuellesTonemapping, Fenster.Helligkeit, wert, Fenster.Saettigung));
        (_saettigung, _saettigungWert, _) = Regler(s, "Sättigung", 0, 150, 5, " %", wert =>
            Fenster.FarbstimmungSetzen(Fenster.AktuellesTonemapping, Fenster.Helligkeit, Fenster.Kontrast, wert));
        var zuruecksetzen = Hauptmenue.Knopf(s, "Farbstimmung zurücksetzen", () =>
        {
            Fenster.FarbstimmungSetzen(Tonemapping.Bisher, 100, 100, 100);
            Auffrischen();
        });
        zuruecksetzen.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        Hinweis(s, "Wirkt auf Stadt und Räume, nicht auf die Tafeln. 100 % lässt das Bild, wie es ist.");
        Abschnitt(s, "Fahnen");
        _fahnen = Schalter(s, "Fahnen bewegen", an =>
        {
            Fenster.FahnenSetzen(an);
            Auffrischen();
        });
        Hinweis(s, "Aus: Alle Fahnen hängen still – für alle, denen die Bewegung im Bild unangenehm ist.");

        // ── Barrierefreiheit ─────────────────────────────────────────────────
        s = _seiten[4];
        Abschnitt(s, "Größe der Oberfläche");
        (_groesse, _groesseWert, _) = Regler(s, "Größe", 75, 200, 5, " %", wert =>
        {
            // Erst beim Loslassen anwenden: Sonst wächst die Tafel unter der Maus, während man zieht
            if (!_groesseZiehen) OberflaecheAnwenden(wert);
        });
        _groesse.DragStarted += () => _groesseZiehen = true;
        _groesse.DragEnded += _ =>
        {
            _groesseZiehen = false;
            OberflaecheAnwenden((int)_groesse.Value);
        };
        Hinweis(s, "Alle Tafeln, Schriften und Knöpfe größer oder kleiner; die Stadt bleibt scharf. Passt eine Tafel nicht mehr ins Fenster, lässt sie sich rollen.");
        Abschnitt(s, "Farbfilter");
        _farbfilter = Auswahl(s, Farbfilterarten.Select(f => f.Name), nummer =>
        {
            Fenster.FarbfilterSetzen(Farbfilterarten[nummer].Art);
            Auffrischen();
        });
        _farbfilterErklaerung = Hinweis(s, "");
        Hinweis(s, "Gilt für das ganze Bild mit Tafeln, Wappen und Warenzeichen: Farben, die sich für das Auge kaum unterscheiden, werden in Helligkeit und andere Farbtöne verschoben.");

        spalte.AddChild(new HSeparator());
        Hauptmenue.Knopf(spalte, "Zurück", zurueck);

        // Während die Tafel offen ist: Stand der dynamischen Auflösung und der automatischen Wahl nachführen
        var uhr = new Godot.Timer { WaitTime = 0.5, Autostart = true };
        uhr.Timeout += () => { if (Tafel.IsVisibleInTree()) StandAuffrischen(); };
        Tafel.AddChild(uhr);
        ReiterZeigen(0);
        Auffrischen();
    }

    /// <summary>Zeigt den Reiter mit dieser Nummer (0 = Voreinstellung).</summary>
    public void ReiterZeigen(int nummer)
    {
        for (var i = 0; i < _seiten.Count; i++)
        {
            _seiten[i].Visible = i == nummer;
            _reiter[i].SetPressedNoSignal(i == nummer);
        }
    }

    /// <summary>Zeigt den Reiter mit diesem Namen (Startoption <c>--reiter</c>); unbekannte Namen lassen den Reiter, wie er ist.</summary>
    public void ReiterZeigen(string name)
    {
        var nummer = Array.FindIndex(Reiternamen, r => r.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (nummer >= 0) ReiterZeigen(nummer);
    }

    private void OberflaecheAnwenden(int prozent)
    {
        Fenster.OberflaechengroesseSetzen(prozent);
        Auffrischen();
    }

    /// <summary>Zeigt die aktuellen Werte – Vollbild kann sich inzwischen per F11 geändert haben, die automatische Wahl im Hintergrund laufen.</summary>
    public void Auffrischen()
    {
        var voreinstellung = Array.FindIndex(Voreinstellungen, v => v.Stufe == Fenster.AktuelleVoreinstellung);
        _voreinstellung.Select(voreinstellung);
        _voreinstellungErklaerung.Text = Voreinstellungen[voreinstellung].Erklaerung;
        _vollbild.SetPressedNoSignal(Fenster.IstVollbild);

        _bildrate.Select(Math.Max(0, Array.IndexOf(Bildraten, Fenster.Bildrate)));
        var vsync = Array.FindIndex(VsyncArten, v => v.Art == Fenster.AktuellesVsync);
        _vsync.Select(vsync);
        _vsyncErklaerung.Text = VsyncArten[vsync].Erklaerung;
        _dynamisch.Select(Math.Max(0, Array.IndexOf(Zielraten, Fenster.DynamischeAufloesung)));
        _fsr.SetPressedNoSignal(Fenster.FsrAn);
        _fsrZeile.Visible = Fenster.FsrAn && Fenster.DynamischeAufloesung == 0; // die dynamische Auflösung wählt selbst
        WertSetzen(_fsrRegler, _fsrWert, Fenster.Renderaufloesung, " %");
        WertSetzen(_natur, _naturWert, Fenster.NaturDichte, " %");
        WertSetzen(_passanten, _passantenWert, Fenster.PassantenMenge, " %");
        _feinheiten.Select(Array.FindIndex(Sichtweiten, f => f.Stufe == Fenster.AktuelleFeinheiten));

        _oberflaeche.SetPressedNoSignal(Fenster.OberflaechendetailsAn);
        var boden = Array.FindIndex(Bodenstufen, b => b.Stufe == Fenster.AktuelleBodenqualitaet);
        _boden.Select(boden);
        _bodenErklaerung.Text = Bodenstufen[boden].Erklaerung;
        var gras = Array.FindIndex(Grasstufen, g => g.Stufe == Fenster.AktuellerGrasteppich);
        _gras.Select(gras);
        _grasErklaerung.Text = Grasstufen[gras].Erklaerung;
        var nummer = Array.FindIndex(Verfahren, v => v.Art == Fenster.AktuelleKantenglaettung);
        _kanten.Select(nummer);
        _kanten.Disabled = Fenster.FsrAn; // FSR 2.2 glättet selbst; die Wahl bleibt gespeichert
        _erklaerung.Text = Fenster.FsrAn ? ErklaerungKantenFsr : Verfahren[nummer].Erklaerung;
        WertSetzen(_schaerfe, _schaerfeWert, Fenster.Schaerfe, "");
        var stufe = Array.FindIndex(Schattenstufen, x => x.Stufe == Fenster.AktuelleSchattenqualitaet);
        _schatten.Select(stufe);
        _schattenErklaerung.Text = Schattenstufen[stufe].Erklaerung;
        var verdeckung = Array.FindIndex(Verdeckungsstufen, v => v.Stufe == Fenster.AktuelleUmgebungsverdeckung);
        _verdeckung.Select(verdeckung);
        _verdeckungErklaerung.Text = Verdeckungsstufen[verdeckung].Erklaerung;
        _indirekt.SetPressedNoSignal(Fenster.IndirektesLichtAn);

        _bloom.SetPressedNoSignal(Fenster.BloomAn);
        WertSetzen(_bloomRegler, _bloomWert, Fenster.BloomStaerke, " %");
        _unschaerfe.Select(Array.FindIndex(Unschaerfen, u => u.Stufe == Fenster.AktuelleUnschaerfe));
        _wolken.SetPressedNoSignal(Fenster.WolkenschattenAn);
        var dunst = Array.FindIndex(Dunststufen, d => d.Stufe == Fenster.AktuellerDunst);
        _dunst.Select(dunst);
        _dunstErklaerung.Text = Dunststufen[dunst].Erklaerung;
        var tonemapping = Array.FindIndex(Tonemappings, t => t.Art == Fenster.AktuellesTonemapping);
        _tonemapping.Select(tonemapping);
        _tonemappingErklaerung.Text = Tonemappings[tonemapping].Erklaerung;
        WertSetzen(_helligkeit, _helligkeitWert, Fenster.Helligkeit, " %");
        WertSetzen(_kontrast, _kontrastWert, Fenster.Kontrast, " %");
        WertSetzen(_saettigung, _saettigungWert, Fenster.Saettigung, " %");
        _fahnen.SetPressedNoSignal(Fenster.FahnenBewegen);

        if (!_groesseZiehen) WertSetzen(_groesse, _groesseWert, Fenster.Oberflaechengroesse, " %");
        var filter = Array.FindIndex(Farbfilterarten, f => f.Art == Fenster.AktuellerFarbfilter);
        _farbfilter.Select(filter);
        _farbfilterErklaerung.Text = Farbfilterarten[filter].Erklaerung;
        StandAuffrischen();
    }

    /// <summary>Was sich ohne Zutun ändert: die gewählte Renderauflösung der dynamischen Auflösung und die automatische Wahl.</summary>
    private void StandAuffrischen()
    {
        _dynamischStand.Text = Fenster.DynamischeAufloesung > 0
            ? $"Gerade: {Fenster.DynamischerAnteil * 100:0} % der Auflösung" + (Fenster.FsrAn ? " (FSR 2.2)" : " (vergrößert)")
            : "";
        _dynamischStand.Visible = _dynamischStand.Text != "";
        _automatisch.Disabled = Fenster.WaehltAutomatisch;
        _automatischErgebnis.Text = Fenster.WaehltAutomatisch ? "Misst gerade …" : Fenster.AutomatischErgebnis;
        _automatischErgebnis.Visible = _automatischErgebnis.Text != "";
        // Ist die automatische Wahl inzwischen fertig, zeigen alle Regler ihre neue Stufe
        if (!Fenster.WaehltAutomatisch && _voreinstellung.Selected != Array.FindIndex(Voreinstellungen, v => v.Stufe == Fenster.AktuelleVoreinstellung))
            Auffrischen();
    }

    // ── Bausteine der Tafel ─────────────────────────────────────────────────

    private static void Abschnitt(Container eltern, string titel) => Hauptmenue.Text(eltern, titel, 18);

    private static Label Hinweis(Container eltern, string text) => Hauptmenue.Text(eltern, text, 13, Grau, umbruchBreite: Umbruch);

    private static CheckButton Schalter(Container eltern, string text, Action<bool> geaendert)
    {
        var schalter = new CheckButton { Text = text, FocusMode = Control.FocusModeEnum.None };
        schalter.AddThemeFontSizeOverride("font_size", 15);
        schalter.Toggled += an => geaendert(an);
        eltern.AddChild(schalter);
        return schalter;
    }

    private static OptionButton Auswahl(Container eltern, IEnumerable<string> namen, Action<int> gewaehlt)
    {
        var auswahl = new OptionButton { FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var name in namen) auswahl.AddItem(name);
        auswahl.ItemSelected += nummer => gewaehlt((int)nummer);
        eltern.AddChild(auswahl);
        return auswahl;
    }

    /// <summary>Eine Zeile mit Beschriftung links; das Feld kommt danach dazu.</summary>
    private static HBoxContainer Zeile(Container eltern, string beschriftung)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 10);
        eltern.AddChild(zeile);
        var name = Hauptmenue.Text(zeile, beschriftung, 15);
        name.CustomMinimumSize = new Vector2(150, 0);
        return zeile;
    }

    /// <summary>Ein Regler mit Beschriftung und Wert in einer Zeile.</summary>
    private static (HSlider Regler, Label Wert, HBoxContainer Zeile) Regler(Container eltern, string beschriftung, int min, int max, int schritt,
                                                                          string einheit, Action<int> geaendert)
    {
        var zeile = Zeile(eltern, beschriftung);
        var regler = new HSlider
        {
            MinValue = min, MaxValue = max, Step = schritt, FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(180, 0),
        };
        zeile.AddChild(regler);
        var wert = Hauptmenue.Text(zeile, "", 15);
        wert.CustomMinimumSize = new Vector2(55, 0);
        regler.ValueChanged += zahl =>
        {
            wert.Text = $"{(int)zahl}{einheit}";
            geaendert((int)zahl);
        };
        return (regler, wert, zeile);
    }

    private static void WertSetzen(HSlider regler, Label wert, int zahl, string einheit)
    {
        regler.SetValueNoSignal(zahl);
        wert.Text = $"{zahl}{einheit}";
    }
}
