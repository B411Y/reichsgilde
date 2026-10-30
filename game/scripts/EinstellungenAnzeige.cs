using System.Text;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Fenster „Einstellungen“ mit vier Reitern (Umbau 29.10.2026, vorher sieben):
/// <b>Grafik</b> – oben die Voreinstellung (Niedrig bis Ultra, automatische Wahl, Vollbild), darunter in den Gruppen <i>Leistung</i> (Bildrate, VSync, FSR 2.2),
/// <i>Bild</i> (Kantenglättung, Schatten) und <i>Stimmung</i> (Bloom) die sechs Hauptregler; der Knopf <i>Erweitert</i> klappt den Rest auf (Dynamische Auflösung,
/// Detailstufe, Boden, Gras, Umgebungsverdeckung, indirektes Licht, globale Beleuchtung (SDFGI), Unschärfe, Wolken, Dunst, Wetter, Farbstimmung und unter Windows die Grafikschnittstelle,
/// siehe <see cref="Grafikschnittstelle"/>); der Zustand wird in <c>user://einstellungen.cfg</c> (Abschnitt „oberflaeche“) gemerkt.
/// <b>Ton</b> (Lautstärke je Bus, Stumm, Ansagen, Ton im Hintergrund stumm; siehe <see cref="KlangDienst"/>), <b>Spiel</b> (Ratssitzung zeigen) und
/// <b>Barrierefreiheit</b> (Größe der Oberfläche, Farbfilter, Fahnen bewegen, Tooltips festheften).
/// Erklärtexte stehen nicht im Fenster, sondern in den Tooltips der Zeilen (sie nennen bei Auswahllisten alle Stufen und heben die gewählte hervor);
/// ein Dropdown-Eintrag oder Schalter, der viel Grafikleistung kostet, trägt das Flammenzeichen (<see cref="Oberflaechenstil.LeistungsSymbol"/>).
/// Erreichbar aus dem Hauptmenü und aus dem Spielmenü (Esc). Jede Änderung wirkt sofort und wird gespeichert (siehe <see cref="Fenster"/>).
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

    /// <summary>
    /// Anzeigename und Erklärung je Stufe der globalen Beleuchtung (SDFGI) – in der Reihenfolge der Auswahlliste. Ehrlich zu den Kosten und zu dem,
    /// was es in der Stadt bringt (Werte und Messung: Stadtansicht.GlobaleBeleuchtung.cs, docs/bereiche/darstellung.md).
    /// </summary>
    private static readonly (GlobaleBeleuchtung Stufe, string Name, string Erklaerung)[] GiStufen =
    {
        (GlobaleBeleuchtung.Aus, "Aus (empfohlen)", "Kein SDFGI: gleichmäßiges Umgebungslicht, klare Schatten. Kostet nichts."),
        (GlobaleBeleuchtung.Niedrig, "Niedrig", "Wenige Strahlen auf halber Auflösung, das Licht wird langsam nachgeführt. In der Stadt hellt es die Schatten auf, in den Räumen grobe Zellen."),
        (GlobaleBeleuchtung.Mittel, "Mittel", "Doppelt so viele Strahlen, das Licht folgt schneller; in den Räumen feinere Zellen."),
        (GlobaleBeleuchtung.Hoch, "Hoch", "Volle Auflösung und viele Strahlen; in den Räumen die feinsten Zellen und Godots Verfahren gegen Lichtlecks. Nur für starke Grafikkarten."),
    };

    /// <summary>Vorwort des Tooltips der globalen Beleuchtung (dazu, wenn es fehlt, der Grund).</summary>
    private const string GiVorwort =
        "Versuch: Licht kommt von Wänden, Boden und Himmel zurück, auch von Dingen außerhalb des Bildes – Schatten werden heller, enge Gassen dunkler, " +
        "Räume bekommen Licht nur noch von Fenstern, Glut und Lampen. Kostet viel Grafikleistung. In der Stadt wirkt es nur grob, weil die Kamera weit über der Stadt steht; " +
        "was gebaut oder abgerissen wird und Fensterlicht kommen erst nach, wenn man die Kamera verschiebt. Kurz nach dem Umschalten flackert das Licht, bis es sich gesetzt hat.";

    /// <summary>Die Voreinstellungen mit Erklärung (Werte: <see cref="Fenster.StufeVon"/>).</summary>
    private static readonly (Voreinstellung Stufe, string Erklaerung)[] Voreinstellungen =
    {
        (Voreinstellung.Niedrig, "Für schwache Grafikkarten: FXAA, kleine Schatten, keine Umgebungsverdeckung, kein Bloom, einfacher Boden, halb so viel Streu und Passanten."),
        (Voreinstellung.Mittel, "SMAA, mittlere Schatten, wenig Umgebungsverdeckung, Bloom, Boden ohne Unebenheit im Licht, Wolkenschatten; drei Viertel der Streu und Passanten."),
        (Voreinstellung.Hoch, "Das empfohlene Bild: SMAA, große Schatten, Umgebungsverdeckung, Bloom, leichte Miniatur-Unschärfe, Wolkenschatten und einfacher Morgendunst."),
        (Voreinstellung.Ultra, "Für starke Grafikkarten: wie Hoch, dazu MSAA 4×, feinste Umgebungsverdeckung, indirektes Licht und Morgendunst mit Lichtstrahlen."),
        (Voreinstellung.Eigene, "Eigene Wahl: Mindestens ein Regler weicht von den Stufen ab. Eine Stufe wählen stellt alle Regler wieder ein."),
    };

    private static readonly (VSync Art, string Name, string Erklaerung)[] VsyncArten =
    {
        (VSync.Aus, "Aus", "Zeichnet so schnell es geht. Am wenigsten Verzögerung, aber bei schnellen Kamerafahrten können Risse im Bild entstehen."),
        (VSync.An, "An (empfohlen)", "Wartet mit jedem Bild auf den Bildschirm: kein Riss. Schafft das Spiel die Bildrate nicht, fällt es auf die Hälfte."),
        (VSync.Adaptiv, "Adaptiv", "Wartet nur, solange das Spiel schnell genug ist; wird es langsamer, zeichnet es sofort (kurze Risse statt Ruckeln)."),
    };

    /// <summary>Die Wahl „Ratssitzung zeigen“ mit Erklärung – in der Reihenfolge der Auswahlliste.</summary>
    private static readonly (RatssitzungZeigen Wert, string Name, string Erklaerung)[] Sitzungswahl =
    {
        (RatssitzungZeigen.Immer, "Immer", "Jede Ratssitzung mit Anträgen oder Wahlen wird im Ratssaal gezeigt."),
        (RatssitzungZeigen.Betroffen, "Wenn betroffen (empfohlen)",
            "Nur, wenn ihr im Rat sitzt und mitstimmt, selbst etwas beantragt habt oder euch um ein Amt bewerbt. Sonst steht das Ergebnis im Protokoll."),
        (RatssitzungZeigen.Nie, "Nie", "Die Sitzung läuft ohne Halt im Hintergrund; das Ergebnis steht im Protokoll und in den Meldungen."),
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

    private static readonly (Wettereffekte Stufe, string Name, string Erklaerung)[] Wetterstufen =
    {
        (Wettereffekte.Niedrig, "Niedrig", "Wolken, Nebel und Blitze nur am Himmel und im Licht, dazu wenig Regen und Schnee. Kostet fast nichts."),
        (Wettereffekte.Mittel, "Mittel", "Dazu nasser, glänzender Boden mit Pfützen, nasse Dächer und das Licht der Blitze über der Stadt."),
        (Wettereffekte.Hoch, "Hoch (empfohlen)", "Dichter Regen und Schnee, Flocken wirbeln im Wind, Regentropfen ziehen Ringe in den Pfützen (mit Boden Hoch)."),
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
    private static readonly string[] Reiternamen = { "Grafik", "Ton", "Spiel", "Barrierefreiheit" };

    /// <summary>Namen der früheren Reiter, die <c>--reiter</c> weiter versteht: alle öffnen „Grafik“, die drei letzten dazu „Erweitert“.</summary>
    private static readonly string[] FrueherGrafik = { "Voreinstellung", "Leistung", "Bild", "Stimmung" };

    /// <summary>Kurze Erklärung je Regler im Reiter „Ton“ (Tooltip) – in der Reihenfolge von <see cref="KlangDienst.Regelbusse"/>.</summary>
    private static readonly string[] TonErklaerungen =
    {
        "Die Gesamtlautstärke; sie wirkt auf alles andere.",
        "Die Musik: Stadtthemen und Stimmungen.",
        "Der Grundklang der Stadt und der Natur: Wind, Vögel, Menschenmenge, Regen.",
        "Geräusche aus der Welt: Hämmern, Karren, Glocken, Tiere, Schritte.",
        "Klicks, Papier und Siegel der Tafeln und Knöpfe.",
        "Sprache und Rufe der Menschen.",
    };

    private const string Datei = "user://einstellungen.cfg";
    private const string KostetViel = "Kostet viel Grafikleistung. Auf einem schwächeren Rechner zuerst hier sparen.";
    private const string ErklaerungKantenFsr = "FSR 2.2 glättet selbst. Diese Wahl gilt wieder, wenn FSR aus ist.";
    private static readonly Color Grau = new(0.75f, 0.75f, 0.75f);
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private const int Umbruch = 520;
    private const int Breite = 620;

    private readonly List<(HSlider Regler, Label Wert, CheckButton Stumm)> _tonRegler = new();
    private readonly CheckButton _tonHintergrund;
    private readonly CheckButton _tonAnsagen;

    public PanelContainer Tafel { get; }
    private readonly List<Button> _reiter = new();
    private readonly List<VBoxContainer> _seiten = new();
    /// <summary>Alle Zeilen mit Tooltip: Der Text wird bei jedem <see cref="Auffrischen"/> neu geholt (bei Listen hebt er die gewählte Stufe hervor).</summary>
    private readonly List<(Container Zeile, Func<string> Text)> _tipps = new();

    // Grafik: Voreinstellung
    private readonly OptionButton _voreinstellung;
    private readonly Button _automatisch;
    private readonly Label _automatischErgebnis;
    private readonly CheckButton _vollbild;
    // Grafik: Leistung
    private readonly OptionButton _bildrate;
    private readonly OptionButton _vsync;
    private readonly CheckButton _fsr;
    private readonly OptionButton _dynamisch;
    private readonly Label _dynamischStand;
    private readonly HBoxContainer _fsrZeile;
    private readonly HSlider _fsrRegler;
    private readonly Label _fsrWert;
    private readonly HSlider _natur;
    private readonly Label _naturWert;
    private readonly HSlider _passanten;
    private readonly Label _passantenWert;
    private readonly OptionButton _feinheiten;
    // Grafik: Bild
    private readonly OptionButton _boden;
    private readonly OptionButton _gras;
    private readonly OptionButton _kanten;
    private readonly Label _kantenHinweis;
    private readonly HSlider _schaerfe;
    private readonly Label _schaerfeWert;
    private readonly OptionButton _schatten;
    private readonly OptionButton _verdeckung;
    private readonly CheckButton _indirekt;
    private readonly OptionButton _gi;
    // Grafik: Stimmung
    private readonly CheckButton _bloom;
    private readonly HSlider _bloomRegler;
    private readonly Label _bloomWert;
    private readonly OptionButton _unschaerfe;
    private readonly CheckButton _wolken;
    private readonly OptionButton _dunst;
    private readonly OptionButton _wetter;
    private readonly OptionButton _tonemapping;
    private readonly HSlider _helligkeit;
    private readonly Label _helligkeitWert;
    private readonly HSlider _kontrast;
    private readonly Label _kontrastWert;
    private readonly HSlider _saettigung;
    private readonly Label _saettigungWert;
    // Grafik: Erweitert
    private readonly VBoxContainer _erweitert;
    private readonly Button _erweitertKnopf;
    private bool _erweitertAn;
    private readonly OptionButton? _schnittstelle;
    private readonly Label? _schnittstelleStand;
    // Barrierefreiheit
    private readonly CheckButton _fahnen;
    private readonly HSlider _groesse;
    private readonly Label _groesseWert;
    private bool _groesseZiehen;
    private readonly OptionButton _farbfilter;
    private readonly HSlider _tooltipZeit;
    private readonly Label _tooltipZeitWert;
    private readonly CheckButton _tooltipTaste;
    // Spiel
    private readonly OptionButton _sitzung;

    public EinstellungenAnzeige(Action zurueck)
    {
        var (tafel, spalte) = Hauptmenue.Tafel(Breite);
        Tafel = tafel;
        Hauptmenue.Text(spalte, "Einstellungen", 26, Gold, zentriert: true);

        // Reiterleiste: vier Knöpfe, der gewählte in Gold
        var leiste = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        leiste.AddThemeConstantOverride("separation", 6);
        spalte.AddChild(leiste);
        var gruppe = new ButtonGroup();
        for (var i = 0; i < Reiternamen.Length; i++)
        {
            var nummer = i;
            var knopf = new Button { Text = Reiternamen[i], ToggleMode = true, ButtonGroup = gruppe, FocusMode = Control.FocusModeEnum.None };
            knopf.AddThemeFontSizeOverride("font_size", 17);
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
            seite.AddThemeConstantOverride("separation", 8);
            spalte.AddChild(seite);
            _seiten.Add(seite);
        }

        // ── Grafik ───────────────────────────────────────────────────────────
        var s = _seiten[0];
        Abschnitt(s, "Voreinstellung");
        var zeile = Zeile(s, "Voreinstellung", () =>
            Liste("Eine Stufe stellt alle Regler für Leistung, Bild und die teuren Stimmungen (Unschärfe, Wolken, Dunst) ein. " +
                  "Farbstimmung, Bildrate, VSync, Fahnen und Barrierefreiheit bleiben, wie sie sind.",
                  Voreinstellungen.Select(v => (Fenster.Name(v.Stufe), v.Erklaerung)), IndexVon(Voreinstellungen, v => v.Stufe == Fenster.AktuelleVoreinstellung)));
        _voreinstellung = Auswahl(zeile, Voreinstellungen.Select(v => Fenster.Name(v.Stufe)), nummer =>
        {
            Fenster.VoreinstellungSetzen(Voreinstellungen[nummer].Stufe);
            Auffrischen();
        });
        _voreinstellung.SetItemDisabled(Voreinstellungen.Length - 1, true); // „Eigene“ ergibt sich, wählen lässt es sich nicht
        _automatisch = Hauptmenue.Knopf(s, "Automatisch wählen", () =>
        {
            Fenster.AutomatischWaehlen();
            Auffrischen();
        });
        _automatisch.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        _automatisch.TooltipText = "Misst ein paar Sekunden lang, wie schnell dieser Rechner die Stadt zeichnet, und wählt die höchste Stufe, die " +
                                   "flüssig bleibt (60 Bilder je Sekunde mit Reserve). Beim ersten Start geschieht das von selbst.";
        _automatischErgebnis = Hinweis(s, "");
        _vollbild = Schalter(s, "Vollbild (F11)", () => "Das Spiel füllt den ganzen Bildschirm. Die Taste F11 wechselt jederzeit zwischen Vollbild und Fenster.",
                             an => { if (an != Fenster.IstVollbild) Fenster.VollbildSetzen(an); });

        Abschnitt(s, "Leistung");
        zeile = Zeile(s, "Bildrate begrenzen", () => "Höchstens so viele Bilder je Sekunde: spart Strom, hält Lüfter und Grafikkarte kühler.");
        _bildrate = Auswahl(zeile, Bildraten.Select(b => b == 0 ? "Unbegrenzt" : $"{b} Bilder je Sekunde"), nummer =>
        {
            Fenster.BildrateSetzen(Bildraten[nummer]);
            Auffrischen();
        });
        zeile = Zeile(s, "VSync", () => Liste("Gleicht die Bilder mit dem Bildschirm ab.", VsyncArten.Select(v => (v.Name, v.Erklaerung)),
                                              IndexVon(VsyncArten, v => v.Art == Fenster.AktuellesVsync)));
        _vsync = Auswahl(zeile, VsyncArten.Select(v => v.Name), nummer =>
        {
            Fenster.VsyncSetzen(VsyncArten[nummer].Art);
            Auffrischen();
        });
        _fsr = Schalter(s, "FSR 2.2 (Hochskalierung)", () => "Rechnet das Bild kleiner und holt es aus den letzten Bildern auf volle Größe: spart Grafikleistung, glättet selbst. " +
                                                              "67 % Renderauflösung = AMD-Stufe „Qualität“. Bewegtes kann leicht schlieren. Die Renderauflösung stellt ihr unter Erweitert ein.",
                        an =>
                        {
                            Fenster.FsrSetzen(an, Fenster.Renderaufloesung);
                            Auffrischen();
                        });

        Abschnitt(s, "Bild");
        zeile = Zeile(s, "Kantenglättung", () => (Fenster.FsrAn ? ErklaerungKantenFsr + "\n\n" : "") +
                                                 Liste("Macht gezackte Kanten glatt.", Verfahren.Select(v => (v.Name, v.Erklaerung)),
                                                       IndexVon(Verfahren, v => v.Art == Fenster.AktuelleKantenglaettung)));
        _kanten = Auswahl(zeile, Verfahren.Select(v => v.Name), nummer =>
        {
            Fenster.KantenglaettungSetzen(Verfahren[nummer].Art);
            Auffrischen();
        }, nummer => Verfahren[nummer].Art is Kantenglaettung.Msaa2 or Kantenglaettung.Msaa4 or Kantenglaettung.Msaa8);
        _kantenHinweis = Hinweis(s, "");
        zeile = Zeile(s, "Schattenqualität", () => Liste("Wie fein die Schatten der Häuser und Figuren sind.", Schattenstufen.Select(v => (v.Name, v.Erklaerung)),
                                                         IndexVon(Schattenstufen, x => x.Stufe == Fenster.AktuelleSchattenqualitaet)));
        _schatten = Auswahl(zeile, Schattenstufen.Select(v => v.Name), nummer =>
        {
            Fenster.SchattenqualitaetSetzen(Schattenstufen[nummer].Stufe);
            Auffrischen();
        });

        Abschnitt(s, "Stimmung");
        _bloom = Schalter(s, "Lichtschein (Bloom)", () => "Heller Schein um Fensterlicht, Glut und Domfenster. Sonnenbeschienene Flächen bleiben klar. " +
                                                          "50 % Stärke ist das empfohlene Bild; kostet etwas Grafikleistung. Die Stärke stellt ihr unter Erweitert ein.",
                          an =>
                          {
                              Fenster.BloomSetzen(an, Fenster.BloomStaerke);
                              Auffrischen();
                          });

        // ── Grafik: Erweitert ────────────────────────────────────────────────
        _erweitertKnopf = Hauptmenue.Knopf(s, "Erweitert aufklappen", () => ErweitertSetzen(!_erweitertAn, merken: true));
        _erweitertKnopf.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        _erweitertKnopf.TooltipText = "Zeigt alle übrigen Grafikregler. Das Fenster merkt sich, ob sie offen oder zu sind.";
        _erweitert = new VBoxContainer();
        _erweitert.AddThemeConstantOverride("separation", 8);
        s.AddChild(_erweitert);
        var e = _erweitert;

        Abschnitt(e, "Leistung (erweitert)");
        zeile = Zeile(e, "Dynamische Auflösung", () => "Wird es eng, rechnet das Spiel das 3D-Bild kleiner (bis 50 %) und holt es auf volle Größe – mit FSR 2.2, wenn es an ist, " +
                                                      "sonst einfach vergrößert (weicher). Geregelt wird ruhig, in kleinen Schritten alle paar Sekunden. Tafeln und Schrift bleiben scharf.");
        _dynamisch = Auswahl(zeile, Zielraten.Select(z => z == 0 ? "Aus" : $"Ziel {z} Bilder je Sekunde"), nummer =>
        {
            Fenster.DynamischeAufloesungSetzen(Zielraten[nummer]);
            Auffrischen();
        });
        _dynamischStand = Hinweis(e, "");
        (_fsrRegler, _fsrWert, _fsrZeile) = Regler(e, "Renderauflösung (FSR)", 25, 100, 1, " %", wert =>
        {
            Fenster.FsrSetzen(Fenster.FsrAn, wert);
            Auffrischen();
        }, () => "In welcher Größe das 3D-Bild gerechnet wird, bevor FSR 2.2 es auf volle Größe holt. 67 % = AMD-Stufe „Qualität“. Nur sichtbar, wenn FSR an ist " +
                 "und die dynamische Auflösung aus.");
        (_natur, _naturWert, _) = Regler(e, "Natur-Dichte", 25, 100, 5, " %", wert =>
        {
            Fenster.NaturDichteSetzen(wert);
            Auffrischen();
        }, () => "Anteil von Gras, Kräutern, Büschen, Steinen und Holz vor der Stadt; Bäume dünnen nur halb so stark aus.");
        (_passanten, _passantenWert, _) = Regler(e, "Passanten", 25, 100, 5, " %", wert =>
        {
            Fenster.PassantenMengeSetzen(wert);
            Auffrischen();
        }, () => "Wie viele Einwohner als Figur auf Gassen und Markt zu sehen sind. Die Stadt rechnet weiter mit allen – nur weniger Figuren werden gezeichnet.");
        zeile = Zeile(e, "Sichtweite der Feinheiten", () => "Bis zu welchem Zoom Bodenpflanzen, Kiesel und Holz gezeichnet werden. Nah: nur von nahem, und weit Entferntes wird vereinfacht " +
                                                           "(dann werfen Spitzdächer und Zinnen keine feinen Schatten mehr).");
        _feinheiten = Auswahl(zeile, Sichtweiten.Select(f => f.Name), nummer =>
        {
            Fenster.FeinheitenSetzen(Sichtweiten[nummer].Stufe);
            Auffrischen();
        });

        Abschnitt(e, "Bild (erweitert)");
        zeile = Zeile(e, "Boden", () => Liste("Wie die Bodentexturen gezeichnet werden.", Bodenstufen.Select(b => (b.Name, b.Erklaerung)),
                                              IndexVon(Bodenstufen, b => b.Stufe == Fenster.AktuelleBodenqualitaet)));
        _boden = Auswahl(zeile, Bodenstufen.Select(b => b.Name), nummer =>
        {
            Fenster.BodenqualitaetSetzen(Bodenstufen[nummer].Stufe);
            Auffrischen();
        });
        zeile = Zeile(e, "Gras", () => Liste("Ein Teppich kleiner Halme auf den Wiesen.", Grasstufen.Select(g => (g.Name, g.Erklaerung)),
                                             IndexVon(Grasstufen, g => g.Stufe == Fenster.AktuellerGrasteppich)));
        _gras = Auswahl(zeile, Grasstufen.Select(g => g.Name), nummer =>
        {
            Fenster.GrasteppichSetzen(Grasstufen[nummer].Stufe);
            Auffrischen();
        }, nummer => Grasstufen[nummer].Stufe == Grasteppich.SehrDicht);
        (_schaerfe, _schaerfeWert, _) = Regler(e, "Nachschärfen", 0, 100, 5, "", wert =>
        {
            Fenster.SchaerfeSetzen(wert);
            Auffrischen();
        }, () => "Holt feine Kanten zurück, die Glättung oder Hochskalierung weich gemacht haben. Mit FSR 2.2 ist es dessen eigene Schärfe. 0 = aus.");
        zeile = Zeile(e, "Umgebungsverdeckung", () => Liste("Verdunkelt Ecken, Fugen und Fußpunkte, damit die Häuser fest stehen.", Verdeckungsstufen.Select(v => (v.Name, v.Erklaerung)),
                                                            IndexVon(Verdeckungsstufen, v => v.Stufe == Fenster.AktuelleUmgebungsverdeckung)));
        _verdeckung = Auswahl(zeile, Verdeckungsstufen.Select(v => v.Name), nummer =>
        {
            Fenster.UmgebungsverdeckungSetzen(Verdeckungsstufen[nummer].Stufe);
            Auffrischen();
        }, nummer => Verdeckungsstufen[nummer].Stufe == Umgebungsverdeckung.Hoch);
        _indirekt = Schalter(e, "Indirektes Licht (SSIL)", () => "Flächen strahlen etwas Farbe auf ihre Nachbarn zurück. Zurückhaltend; kostet Grafikleistung.",
                             an =>
                             {
                                 Fenster.IndirektesLichtSetzen(an);
                                 Auffrischen();
                             }, teuer: true);
        zeile = Zeile(e, "Globale Beleuchtung (SDFGI)", () => Liste(Fenster.GlobaleBeleuchtungMoeglich ? GiVorwort
                                                                        : GiVorwort + "\n\nMit dieser Grafikschnittstelle nicht verfügbar (nur Vulkan und DirectX 12).",
                                                                    GiStufen.Select(g => (g.Name, g.Erklaerung)),
                                                                    IndexVon(GiStufen, g => g.Stufe == Fenster.AktuelleGlobaleBeleuchtung)));
        _gi = Auswahl(zeile, GiStufen.Select(g => g.Name), nummer =>
        {
            Fenster.GlobaleBeleuchtungSetzen(GiStufen[nummer].Stufe);
            Auffrischen();
        }, nummer => GiStufen[nummer].Stufe != GlobaleBeleuchtung.Aus);

        Abschnitt(e, "Stimmung (erweitert)");
        (_bloomRegler, _bloomWert, _) = Regler(e, "Bloom: Stärke", 0, 100, 5, " %", wert => Fenster.BloomSetzen(Fenster.BloomAn, wert),
                                               () => "Wie stark der Lichtschein ist. 50 % ist das empfohlene Bild; kostet etwas Grafikleistung.");
        zeile = Zeile(e, "Miniatur-Unschärfe", () => "Oben und unten im Bild wird die Stadt weich, die Mitte bleibt scharf – wie ein Foto eines Modells (Tilt-Shift). Nah stärker als weit. " +
                                                     "Tafeln und Räume bleiben scharf. Kostet etwas Grafikleistung.");
        _unschaerfe = Auswahl(zeile, Unschaerfen.Select(u => u.Name), nummer =>
        {
            Fenster.UnschaerfeSetzen(Unschaerfen[nummer].Stufe);
            Auffrischen();
        });
        _wolken = Schalter(e, "Wolkenschatten", () => "Schatten einzelner Wolken ziehen langsam mit dem Wind über Felder, Gassen und Dächer – nur bei Sonne.",
                           an =>
                           {
                               Fenster.WolkenschattenSetzen(an);
                               Auffrischen();
                           });
        zeile = Zeile(e, "Morgendunst, Lichtstrahlen", () => Liste("Dunst am Morgen und Lichtbahnen der tiefen Sonne.", Dunststufen.Select(d => (d.Name, d.Erklaerung)),
                                                                   IndexVon(Dunststufen, d => d.Stufe == Fenster.AktuellerDunst)));
        _dunst = Auswahl(zeile, Dunststufen.Select(d => d.Name), nummer =>
        {
            Fenster.DunstSetzen(Dunststufen[nummer].Stufe);
            Auffrischen();
        }, nummer => Dunststufen[nummer].Stufe == Morgendunst.Hoch);
        zeile = Zeile(e, "Wettereffekte", () => Liste("Regen, Schnee, Nebel und Blitze.", Wetterstufen.Select(w => (w.Name, w.Erklaerung)),
                                                      IndexVon(Wetterstufen, w => w.Stufe == Fenster.AktuelleWettereffekte)));
        _wetter = Auswahl(zeile, Wetterstufen.Select(w => w.Name), nummer =>
        {
            Fenster.WettereffekteSetzen(Wetterstufen[nummer].Stufe);
            Auffrischen();
        });
        zeile = Zeile(e, "Farbstimmung", () => Liste("Wie helle Stellen und Farben ins Bild kommen. Wirkt auf Stadt und Räume, nicht auf die Tafeln.", Tonemappings.Select(t => (t.Name, t.Erklaerung)),
                                                     IndexVon(Tonemappings, t => t.Art == Fenster.AktuellesTonemapping)));
        _tonemapping = Auswahl(zeile, Tonemappings.Select(t => t.Name), nummer =>
        {
            Fenster.FarbstimmungSetzen(Tonemappings[nummer].Art, Fenster.Helligkeit, Fenster.Kontrast, Fenster.Saettigung);
            Auffrischen();
        });
        (_helligkeit, _helligkeitWert, _) = Regler(e, "Helligkeit", 50, 150, 5, " %", wert =>
            Fenster.FarbstimmungSetzen(Fenster.AktuellesTonemapping, wert, Fenster.Kontrast, Fenster.Saettigung),
            () => "Wirkt auf Stadt und Räume, nicht auf die Tafeln. 100 % lässt das Bild, wie es ist.");
        (_kontrast, _kontrastWert, _) = Regler(e, "Kontrast", 50, 150, 5, " %", wert =>
            Fenster.FarbstimmungSetzen(Fenster.AktuellesTonemapping, Fenster.Helligkeit, wert, Fenster.Saettigung),
            () => "Wirkt auf Stadt und Räume, nicht auf die Tafeln. 100 % lässt das Bild, wie es ist.");
        (_saettigung, _saettigungWert, _) = Regler(e, "Sättigung", 0, 150, 5, " %", wert =>
            Fenster.FarbstimmungSetzen(Fenster.AktuellesTonemapping, Fenster.Helligkeit, Fenster.Kontrast, wert),
            () => "Wirkt auf Stadt und Räume, nicht auf die Tafeln. 100 % lässt das Bild, wie es ist; 0 % macht es schwarzweiß.");
        var zuruecksetzen = Hauptmenue.Knopf(e, "Farbstimmung zurücksetzen", () =>
        {
            Fenster.FarbstimmungSetzen(Tonemapping.Bisher, 100, 100, 100);
            Auffrischen();
        });
        zuruecksetzen.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        zuruecksetzen.TooltipText = "Linear, Helligkeit, Kontrast und Sättigung zurück auf 100 %.";

        if (Grafikschnittstelle.Sichtbar)
        {
            Abschnitt(e, "Grafikschnittstelle");
            zeile = Zeile(e, "Grafikschnittstelle", () =>
                "Über welche Schnittstelle das Spiel die Grafikkarte anspricht. Vulkan ist der Standard und am besten getestet. DirectX 12 ist neu und kann auf einem " +
                "Windows-Rechner anders laufen (besser oder schlechter). Die Wahl gilt erst nach einem Neustart des Spiels. Startet DirectX 12 nicht oder stürzt das Spiel damit ab, " +
                "nimmt es beim nächsten Start selbst wieder Vulkan.");
            _schnittstelle = Auswahl(zeile, Grafikschnittstelle.Arten.Select(a => a.Name), nummer =>
            {
                Grafikschnittstelle.Setzen(Grafikschnittstelle.Arten[nummer].Kennung);
                Auffrischen();
            });
            _schnittstelleStand = Hinweis(e, "");
        }

        // ── Spiel ────────────────────────────────────────────────────────────
        s = _seiten[2];
        Abschnitt(s, "Ratssitzung");
        zeile = Zeile(s, "Ratssitzung zeigen", () => Liste("Am Jahresende hält die Partie an, und ihr seht die Sitzung im Ratssaal: Antrag, Begründung, die Stimmen der Ratsherren und das Ergebnis. " +
                                                           "Sitzt ihr im Rat, stimmt ihr an eurer Stelle selbst ab.", Sitzungswahl.Select(w => (w.Name, w.Erklaerung)),
                                                           IndexVon(Sitzungswahl, w => w.Wert == Fenster.Ratssitzung)));
        _sitzung = Auswahl(zeile, Sitzungswahl.Select(w => w.Name), nummer =>
        {
            Fenster.RatssitzungSetzen(Sitzungswahl[nummer].Wert);
            Auffrischen();
        });

        // ── Ton ──────────────────────────────────────────────────────────────
        s = _seiten[1];
        Abschnitt(s, "Lautstärke");
        for (var i = 0; i < KlangDienst.Regelbusse.Length; i++)
        {
            var bus = i;
            var (_, name, _) = KlangDienst.Regelbusse[i];
            var (regler, wert, reglerZeile) = Regler(s, name, 0, 100, 5, " %", prozent => KlangDienst.ProzentSetzen(bus, prozent), () => TonErklaerungen[bus], breite: 120);
            var stumm = new CheckButton { Text = "Stumm", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(130, 0), TooltipText = "Schaltet nur diesen Bus stumm: " + TonErklaerungen[bus] };
            stumm.AddThemeFontSizeOverride("font_size", 15);
            stumm.Toggled += an => KlangDienst.StummSetzen(bus, an);
            reglerZeile.AddChild(stumm);
            _tonRegler.Add((regler, wert, stumm));
        }
        Abschnitt(s, "Ansagen und Hintergrund");
        _tonAnsagen = Schalter(s, "Ansagen des Haushofmeisters", () => "Bei wichtigen Ereignissen (Tod, Krankheit, Geburt, leeres Lager …) spricht der Haushofmeister einen kurzen Satz; die Meldung steht auch als Text da. " +
                                                                       "Immer nur eine Ansage zur Zeit, bei dichter Folge entfallen manche.", an => KlangDienst.AnsagenSetzen(an));
        _tonHintergrund = Schalter(s, "Ton im Hintergrund stumm", () => "Verliert das Spiel den Fokus (ein anderes Fenster ist vorn), wird alles stumm; beim Zurückkehren ist der Ton wieder da.",
                                   an => KlangDienst.HintergrundStummSetzen(an));

        // ── Barrierefreiheit ─────────────────────────────────────────────────
        s = _seiten[3];
        Abschnitt(s, "Darstellung");
        (_groesse, _groesseWert, _) = Regler(s, "Größe der Oberfläche", 75, 200, 5, " %", wert =>
        {
            // Erst beim Loslassen anwenden: Sonst wächst die Tafel unter der Maus, während man zieht
            if (!_groesseZiehen) OberflaecheAnwenden(wert);
        }, () => "Alle Tafeln, Schriften und Knöpfe größer oder kleiner; die Stadt bleibt scharf. Passt eine Tafel nicht mehr ins Fenster, lässt sie sich rollen.");
        _groesse.DragStarted += () => _groesseZiehen = true;
        _groesse.DragEnded += _ =>
        {
            _groesseZiehen = false;
            OberflaecheAnwenden((int)_groesse.Value);
        };
        zeile = Zeile(s, "Farbfilter", () => Liste("Gilt für das ganze Bild mit Tafeln, Wappen und Warenzeichen: Farben, die sich für das Auge kaum unterscheiden, werden in Helligkeit und andere Farbtöne verschoben.",
                                                   Farbfilterarten.Select(f => (f.Name, f.Erklaerung)), IndexVon(Farbfilterarten, f => f.Art == Fenster.AktuellerFarbfilter)));
        _farbfilter = Auswahl(zeile, Farbfilterarten.Select(f => f.Name), nummer =>
        {
            Fenster.FarbfilterSetzen(Farbfilterarten[nummer].Art);
            Auffrischen();
        });
        _fahnen = Schalter(s, "Fahnen bewegen", () => "Aus: Alle Fahnen hängen still – für alle, denen die Bewegung im Bild unangenehm ist.", an =>
        {
            Fenster.FahnenSetzen(an);
            Auffrischen();
        });
        Abschnitt(s, "Tooltips festheften");
        var tooltipText = () => "Ein Tooltip zeigt oben rechts einen Kreis, der sich füllt, solange die Maus auf ihm oder seiner Stelle ruht. Ist er voll, bleibt der Tooltip als kleines Fenster stehen " +
                                "(das Kreuz oben rechts schließt es, Esc das oberste). T oder die mittlere Maustaste heftet sofort fest. Verweise im Text (unterstrichen) öffnen weitere Tooltips.";
        var tooltipZeile = Zeile(s, "Verweilen", tooltipText);
        _tooltipZeit = new HSlider
        {
            MinValue = (int)(Fenster.TooltipFestMin * 2), MaxValue = (int)(Fenster.TooltipFestMax * 2), Step = 1, FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(180, 0),
        };
        tooltipZeile.AddChild(_tooltipZeit);
        _tooltipZeitWert = Hauptmenue.Text(tooltipZeile, "", 15);
        _tooltipZeitWert.CustomMinimumSize = new Vector2(55, 0);
        _tooltipZeit.ValueChanged += halbe =>
        {
            Fenster.TooltipFestSetzen(halbe / 2, _tooltipTaste!.ButtonPressed);
            Auffrischen();
        };
        _tooltipTaste = Schalter(s, "Nur mit Taste (T oder mittlere Maustaste)", tooltipText, an =>
        {
            Fenster.TooltipFestSetzen(Fenster.TooltipFestZeit, an);
            Auffrischen();
        });

        spalte.AddChild(new HSeparator());
        Hauptmenue.Knopf(spalte, "Zurück", zurueck);

        // Während die Tafel offen ist: Stand der dynamischen Auflösung und der automatischen Wahl nachführen
        var uhr = new Godot.Timer { WaitTime = 0.5, Autostart = true };
        uhr.Timeout += () => { if (Tafel.IsVisibleInTree()) StandAuffrischen(); };
        Tafel.AddChild(uhr);
        ReiterZeigen(0);
        ErweitertSetzen(ErweitertGemerkt(), merken: false);
        Auffrischen();
    }

    /// <summary>Zeigt den Reiter mit dieser Nummer (0 = Grafik).</summary>
    public void ReiterZeigen(int nummer)
    {
        for (var i = 0; i < _seiten.Count; i++)
        {
            _seiten[i].Visible = i == nummer;
            _reiter[i].SetPressedNoSignal(i == nummer);
        }
    }

    /// <summary>
    /// Zeigt den Reiter mit diesem Namen (Startoption <c>--reiter</c>); unbekannte Namen lassen den Reiter, wie er ist. Die Namen der früheren Reiter gehen weiter:
    /// „Voreinstellung“ öffnet Grafik, „Leistung“, „Bild“ und „Stimmung“ öffnen Grafik mit aufgeklapptem „Erweitert“ (nur für diesen Start). Neu: „GrafikErweitert“.
    /// </summary>
    public void ReiterZeigen(string name)
    {
        if (name.Equals("GrafikErweitert", StringComparison.OrdinalIgnoreCase) || Array.FindIndex(FrueherGrafik, r => r.Equals(name, StringComparison.OrdinalIgnoreCase)) > 0)
        {
            ReiterZeigen(0);
            ErweitertSetzen(true, merken: false);
            return;
        }
        if (name.Equals("Voreinstellung", StringComparison.OrdinalIgnoreCase))
        {
            ReiterZeigen(0);
            return;
        }
        var nummer = Array.FindIndex(Reiternamen, r => r.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (nummer >= 0) ReiterZeigen(nummer);
    }

    /// <summary>Klappt den Teil „Erweitert“ der Grafik auf oder zu; <paramref name="merken"/> schreibt den Zustand nach <c>user://einstellungen.cfg</c>.</summary>
    private void ErweitertSetzen(bool an, bool merken)
    {
        _erweitertAn = an;
        _erweitert.Visible = an;
        _erweitertKnopf.Text = an ? "Erweitert zuklappen" : "Erweitert aufklappen"; // Pfeilzeichen (▸ ▾) fehlen der Knopfschrift
        if (!merken) return;
        var datei = new ConfigFile();
        datei.Load(Datei); // andere Einstellungen behalten (fehlt die Datei, bleibt sie leer)
        datei.SetValue("oberflaeche", "grafik_erweitert", an);
        datei.Save(Datei);
    }

    private static bool ErweitertGemerkt()
    {
        var datei = new ConfigFile();
        return datei.Load(Datei) == Error.Ok && datei.GetValue("oberflaeche", "grafik_erweitert", false).AsBool();
    }

    private void OberflaecheAnwenden(int prozent)
    {
        Fenster.OberflaechengroesseSetzen(prozent);
        Auffrischen();
    }

    /// <summary>Zeigt die aktuellen Werte – Vollbild kann sich inzwischen per F11 geändert haben, die automatische Wahl im Hintergrund laufen.</summary>
    public void Auffrischen()
    {
        _voreinstellung.Select(Array.FindIndex(Voreinstellungen, v => v.Stufe == Fenster.AktuelleVoreinstellung));
        _vollbild.SetPressedNoSignal(Fenster.IstVollbild);

        _bildrate.Select(Math.Max(0, Array.IndexOf(Bildraten, Fenster.Bildrate)));
        _vsync.Select(Array.FindIndex(VsyncArten, v => v.Art == Fenster.AktuellesVsync));
        _dynamisch.Select(Math.Max(0, Array.IndexOf(Zielraten, Fenster.DynamischeAufloesung)));
        _fsr.SetPressedNoSignal(Fenster.FsrAn);
        _fsrZeile.Visible = Fenster.FsrAn && Fenster.DynamischeAufloesung == 0; // die dynamische Auflösung wählt selbst
        WertSetzen(_fsrRegler, _fsrWert, Fenster.Renderaufloesung, " %");
        WertSetzen(_natur, _naturWert, Fenster.NaturDichte, " %");
        WertSetzen(_passanten, _passantenWert, Fenster.PassantenMenge, " %");
        _feinheiten.Select(Array.FindIndex(Sichtweiten, f => f.Stufe == Fenster.AktuelleFeinheiten));

        _boden.Select(Array.FindIndex(Bodenstufen, b => b.Stufe == Fenster.AktuelleBodenqualitaet));
        _gras.Select(Array.FindIndex(Grasstufen, g => g.Stufe == Fenster.AktuellerGrasteppich));
        _kanten.Select(Array.FindIndex(Verfahren, v => v.Art == Fenster.AktuelleKantenglaettung));
        _kanten.Disabled = Fenster.FsrAn; // FSR 2.2 glättet selbst; die Wahl bleibt gespeichert
        _kantenHinweis.Text = Fenster.FsrAn ? "FSR 2.2 glättet selbst; die Wahl gilt wieder, wenn FSR aus ist." : "";
        _kantenHinweis.Visible = _kantenHinweis.Text != "";
        WertSetzen(_schaerfe, _schaerfeWert, Fenster.Schaerfe, "");
        _schatten.Select(Array.FindIndex(Schattenstufen, x => x.Stufe == Fenster.AktuelleSchattenqualitaet));
        _verdeckung.Select(Array.FindIndex(Verdeckungsstufen, v => v.Stufe == Fenster.AktuelleUmgebungsverdeckung));
        _indirekt.SetPressedNoSignal(Fenster.IndirektesLichtAn);
        _gi.Select(Array.FindIndex(GiStufen, g => g.Stufe == Fenster.AktuelleGlobaleBeleuchtung));
        _gi.Disabled = !Fenster.GlobaleBeleuchtungMoeglich; // unter opengl3 (Kompatibilität) gibt es kein SDFGI; die Wahl bleibt gespeichert

        _bloom.SetPressedNoSignal(Fenster.BloomAn);
        WertSetzen(_bloomRegler, _bloomWert, Fenster.BloomStaerke, " %");
        _unschaerfe.Select(Array.FindIndex(Unschaerfen, u => u.Stufe == Fenster.AktuelleUnschaerfe));
        _wolken.SetPressedNoSignal(Fenster.WolkenschattenAn);
        _dunst.Select(Array.FindIndex(Dunststufen, d => d.Stufe == Fenster.AktuellerDunst));
        _wetter.Select(Array.FindIndex(Wetterstufen, w => w.Stufe == Fenster.AktuelleWettereffekte));
        _tonemapping.Select(Array.FindIndex(Tonemappings, t => t.Art == Fenster.AktuellesTonemapping));
        WertSetzen(_helligkeit, _helligkeitWert, Fenster.Helligkeit, " %");
        WertSetzen(_kontrast, _kontrastWert, Fenster.Kontrast, " %");
        WertSetzen(_saettigung, _saettigungWert, Fenster.Saettigung, " %");
        if (_schnittstelle is not null)
        {
            var gewaehlt = Grafikschnittstelle.Gewaehlt;
            _schnittstelle.Select(Math.Max(0, Array.FindIndex(Grafikschnittstelle.Arten, a => a.Kennung == gewaehlt)));
            var zeilen = new List<string>();
            if (Grafikschnittstelle.Hinweis != "") zeilen.Add(Grafikschnittstelle.Hinweis);
            zeilen.Add(gewaehlt == Grafikschnittstelle.Aktiv
                ? $"Läuft mit {Grafikschnittstelle.Name(Grafikschnittstelle.Aktiv)}."
                : $"Gilt erst nach einem Neustart des Spiels. Jetzt läuft {Grafikschnittstelle.Name(Grafikschnittstelle.Aktiv)}, danach {Grafikschnittstelle.Name(gewaehlt)}.");
            _schnittstelleStand!.Text = string.Join("\n", zeilen);
        }

        _fahnen.SetPressedNoSignal(Fenster.FahnenBewegen);
        if (!_groesseZiehen) WertSetzen(_groesse, _groesseWert, Fenster.Oberflaechengroesse, " %");
        _farbfilter.Select(Array.FindIndex(Farbfilterarten, f => f.Art == Fenster.AktuellerFarbfilter));
        _tooltipZeit.SetValueNoSignal(Fenster.TooltipFestZeit * 2);
        _tooltipZeitWert.Text = Fenster.TooltipFestZeit.ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("de-DE")) + " s";
        _tooltipTaste.SetPressedNoSignal(Fenster.TooltipNurMitTaste);
        _tooltipZeit.Editable = !Fenster.TooltipNurMitTaste;
        _sitzung.Select(Array.FindIndex(Sitzungswahl, w => w.Wert == Fenster.Ratssitzung));
        for (var i = 0; i < _tonRegler.Count; i++)
        {
            WertSetzen(_tonRegler[i].Regler, _tonRegler[i].Wert, KlangDienst.Prozent(i), " %");
            _tonRegler[i].Stumm.SetPressedNoSignal(KlangDienst.Stumm(i));
        }
        _tonAnsagen.SetPressedNoSignal(KlangDienst.AnsagenAn);
        _tonHintergrund.SetPressedNoSignal(KlangDienst.HintergrundStumm);
        StandAuffrischen();
        TippsSetzen();
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

    /// <summary>Setzt den Tooltip jeder Zeile neu (Beschriftung, Feld und Wert); das Flammenzeichen behält seinen eigenen Text.</summary>
    private void TippsSetzen()
    {
        foreach (var (zeile, text) in _tipps)
        {
            var t = text();
            foreach (var kind in zeile.GetChildren())
                if (kind is Control c && !c.HasMeta("symbol")) c.TooltipText = t;
        }
    }

    // ── Bausteine der Tafel ─────────────────────────────────────────────────

    /// <summary>Überschrift einer Gruppe, darunter eine feine Linie.</summary>
    private static void Abschnitt(Container eltern, string titel)
    {
        if (eltern.GetChildCount() > 0) eltern.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) }); // Luft über jeder Gruppe außer der ersten
        var kopf = Hauptmenue.Text(eltern, titel, 19, Gold);
        kopf.AddThemeConstantOverride("line_spacing", 0);
        eltern.AddChild(new HSeparator());
    }

    /// <summary>Ein Hinweis unter einer Zeile, der sich mit dem Stand ändert (zum Beispiel „Misst gerade …“); Erklärungen gehören in Tooltips.</summary>
    private static Label Hinweis(Container eltern, string text) => Hauptmenue.Text(eltern, text, 13, Grau, umbruchBreite: Umbruch);

    /// <summary>
    /// Ein Schalter mit Beschriftung links; ein Klick auf die Beschriftung schaltet auch. <paramref name="teuer"/> hängt das Flammenzeichen an.
    /// </summary>
    private CheckButton Schalter(Container eltern, string beschriftung, Func<string> tipp, Action<bool> geaendert, bool teuer = false)
    {
        var zeile = Zeile(eltern, beschriftung, tipp);
        var schalter = new CheckButton { FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        schalter.Toggled += an => geaendert(an);
        zeile.AddChild(schalter);
        if (zeile.GetChild(0) is Label name)
            name.GuiInput += ereignis =>
            {
                if (ereignis is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) schalter.ButtonPressed = !schalter.ButtonPressed;
            };
        if (teuer) zeile.AddChild(Symbol());
        return schalter;
    }

    /// <summary>Das Flammenzeichen „kostet viel Leistung“ mit eigenem Tooltip.</summary>
    private static TextureRect Symbol()
    {
        var symbol = new TextureRect
        {
            Texture = Oberflaechenstil.LeistungsSymbol(), StretchMode = TextureRect.StretchModeEnum.KeepCentered, CustomMinimumSize = new Vector2(22, 22),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Stop, TooltipText = KostetViel,
        };
        symbol.SetMeta("symbol", true);
        return symbol;
    }

    /// <summary>
    /// Eine Auswahlliste im Feld der Zeile. Wer <paramref name="teuer"/> kennt, bekommt bei den Einträgen, die viel Leistung kosten, das Flammenzeichen
    /// (in der Liste und im geschlossenen Feld).
    /// </summary>
    private static OptionButton Auswahl(Container zeile, IEnumerable<string> namen, Action<int> gewaehlt, Func<int, bool>? teuer = null)
    {
        var auswahl = new OptionButton { FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true };
        auswahl.AddThemeConstantOverride("icon_max_width", 20);
        var nummer = 0;
        foreach (var name in namen)
        {
            if (teuer is not null && teuer(nummer)) auswahl.AddIconItem(Oberflaechenstil.LeistungsSymbol(), name);
            else auswahl.AddItem(name);
            nummer++;
        }
        auswahl.ItemSelected += n => gewaehlt((int)n);
        zeile.AddChild(auswahl);
        return auswahl;
    }

    /// <summary>Eine Zeile mit Beschriftung links; das Feld kommt danach dazu. Der Tooltip hängt an allen Teilen der Zeile.</summary>
    private HBoxContainer Zeile(Container eltern, string beschriftung, Func<string> tipp, int breite = 200)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 10);
        eltern.AddChild(zeile);
        var name = Hauptmenue.Text(zeile, beschriftung, 15);
        name.CustomMinimumSize = new Vector2(breite, 0);
        _tipps.Add((zeile, tipp));
        return zeile;
    }

    /// <summary>Ein Regler mit Beschriftung und Wert in einer Zeile.</summary>
    private (HSlider Regler, Label Wert, HBoxContainer Zeile) Regler(Container eltern, string beschriftung, int min, int max, int schritt,
                                                                     string einheit, Action<int> geaendert, Func<string> tipp, int breite = 200)
    {
        var zeile = Zeile(eltern, beschriftung, tipp, breite);
        var regler = new HSlider
        {
            MinValue = min, MaxValue = max, Step = schritt, FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(160, 0),
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

    private static int IndexVon<T>(T[] liste, Func<T, bool> passt) => Math.Max(0, Array.FindIndex(liste, e => passt(e)));

    /// <summary>
    /// Der Text eines Tooltips mit Auswahlliste: ein Vorwort, dann jede Stufe mit ihrer Erklärung; vor der gewählten steht „»“ und hinter ihrem Namen „(gewählt)“.
    /// Reiner Text: Der Tooltip zeigt Klammern wie [b] nicht als Formatierung, sondern wörtlich.
    /// </summary>
    private static string Liste(string vorwort, IEnumerable<(string Name, string Text)> stufen, int aktuell)
    {
        var text = new StringBuilder(vorwort);
        var nummer = 0;
        foreach (var (name, erklaerung) in stufen)
        {
            text.Append("\n\n");
            text.Append(nummer == aktuell ? $"» {name} (gewählt): {erklaerung}" : $"{name}: {erklaerung}");
            nummer++;
        }
        return text.ToString();
    }
}
