using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Die Tafel „Karte“ der Kartenwerkstatt und die Tafel der Feldfläche – ein Teil der <c>partial class Kartenwerkstatt</c> (Schritt 5a).
///
/// <b>Reiter „Karte“</b>: die Karte als Ganzes. Größe des Spielfelds (Breite und Tiefe in 50-m-Schritten, 400 × 300 bis 2.000 × 2.000 m) und
/// Breite des Randstreifens (100 bis 600 m); „Rand vorschlagen“ füllt den Randstreifen mit Feldern und Waldstücken (fragt vorher, wenn es Flächen
/// ersetzt); „Kamera-Start übernehmen“ nimmt den aktuellen Blick als Start des Spiels; der Kartenrand lässt sich als Linie zeigen. Jede Änderung
/// ist ein Befehl des Entwurfs (<see cref="Kartenentwurf.GroesseSetzen"/>, <see cref="Kartenentwurf.RandbreiteSetzen"/>,
/// <see cref="Kartenentwurf.RandVorschlagen"/>, <see cref="Kartenentwurf.KameraStartSetzen"/>) mit Rückgängig; was nach einer Änderung nicht mehr in der
/// Karte liegt, meldet die Prüfung rechts.
///
/// <b>Feldfläche</b> (Reiter „Eigenschaften“): Name, Richtung der Streifen, Streifenbreite und Stücklänge, Anteile der Früchte, Startwert, Aussparungen.
/// </summary>
public partial class Kartenwerkstatt
{
    private VBoxContainer _karteTafel = null!;

    /// <summary>Zeigt die Tafel rechts den Reiter „Karte“?</summary>
    private bool _karteOffen;

    private Zahlfeld? _breiteFeld, _tiefeFeld, _randFeld;
    private Label? _masseText, _kameraText;

    // ═════════════════════════════════════════════════════════════════════════
    //  Reiter „Karte“
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Öffnet den Reiter „Karte“ (die anderen bleiben, nur einer ist zu sehen).</summary>
    private void KarteReiterWaehlen()
    {
        _karteOffen = true;
        _startOffen = false;
        _reiterKarte.SetPressedNoSignal(true);
        _eigenschaften.Visible = false;
        _startTafel.Visible = false;
        _karteTafel.Visible = true;
        KarteZeigen();
        _inhaltRolle.ScrollVertical = 0;
        GruppenMarkieren(); // Kartenwerkstatt.Palette.cs
    }

    /// <summary>Baut die Tafel „Karte“ neu nach dem Stand des Entwurfs.</summary>
    private void KarteZeigen()
    {
        UiHilfe.KinderEntfernen(_karteTafel);
        Hauptmenue.Text(_karteTafel, "Spielfeld: Gebäude, Bauzonen, Mauern. Der Randstreifen darum ist nicht bespielbar, aber gestaltbar (Felder, Wald, Wege) und verblasst außen im Dunst.",
            12, Grau, umbruchBreite: Rechtsbreite - 44);
        var raster = new GridContainer { Columns = 2 };
        raster.AddThemeConstantOverride("h_separation", 10);
        raster.AddThemeConstantOverride("v_separation", 6);
        _karteTafel.AddChild(raster);

        _breiteFeld = Groessenfeld(Kartengrenzen.MinBreite, Kartengrenzen.MaxSeite, "Von West nach Ost (x). 400 bis 2.000 m in 50-m-Schritten – was danach außerhalb läge, meldet die Prüfung als Fehler.",
            wert => _entwurf.GroesseSetzen(wert, _entwurf.Kartengrenzen.Tiefe));
        Zeile(raster, "Breite", _breiteFeld);
        _tiefeFeld = Groessenfeld(Kartengrenzen.MinTiefe, Kartengrenzen.MaxSeite, "Von Nord nach Süd (z). 300 bis 2.000 m in 50-m-Schritten.",
            wert => _entwurf.GroesseSetzen(_entwurf.Kartengrenzen.Breite, wert));
        Zeile(raster, "Tiefe", _tiefeFeld);
        _randFeld = Groessenfeld(Kartengrenzen.RandMin, Kartengrenzen.RandMax, "Breite des Randstreifens um das Spielfeld: 100 bis 600 m (Vorgabe 300). Je breiter, desto weiter lässt sich im Spiel herauszoomen.",
            wert => _entwurf.RandbreiteSetzen(wert));
        Zeile(raster, "Randstreifen", _randFeld);
        _masseText = Wert("");
        Zeile(raster, "Ganze Karte", _masseText);

        var kante = new CheckBox
        {
            Text = "Kartenrand als Linie zeigen", ButtonPressed = _stadt.WerkstattKartenrandSichtbar, FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Gold: die Grenze des Spielfelds. Blau: die Außenkante des Randstreifens. Nur in der Werkstatt, im Spiel gibt es die Linien nicht.",
        };
        kante.AddThemeFontSizeOverride("font_size", 14);
        kante.Toggled += an => _stadt.WerkstattKartenrandSichtbar = an;
        _karteTafel.AddChild(kante);

        Hauptmenue.Text(_karteTafel, "Randlandschaft", 16, Gold);
        Hauptmenue.Text(_karteTafel, "Felder, Wald und etwas Streu – gewöhnliche Flächen, die man ändern und übermalen kann.", 12, Grau, umbruchBreite: Rechtsbreite - 44);
        Knopf(_karteTafel, "Rand vorschlagen", () => RandVorschlagenFragen(),
            "Felder, Wald und Streu im Randstreifen neu vorschlagen (jedes Mal anders; Flächen, die ganz im Rand liegen, werden ersetzt – mit Rückfrage)");

        Hauptmenue.Text(_karteTafel, "Kamera-Start", 16, Gold);
        _kameraText = Hauptmenue.Text(_karteTafel, "", 13, Hell, umbruchBreite: Rechtsbreite - 44);
        Knopf(_karteTafel, "Kamera-Start übernehmen", KameraStartUebernehmen,
            "Der aktuelle Blick (Punkt, Zoom und Drehung) wird zum Blick beim Start der Partie – stellt ihn vorher mit Maus, WASD, Mausrad und Strg+Q/E ein");
        KarteWerteSetzen();
    }

    /// <summary>Ein Zahlenfeld für Größe oder Rand: ein Befehl je Änderung, danach zeigt es den Wert, den der Kern daraus gemacht hat (auf 50 m gerundet).</summary>
    private Zahlfeld Groessenfeld(double min, double max, string tipp, Action<double> setzen)
    {
        var feld = new Zahlfeld { MinValue = min, MaxValue = max, Step = Kartengrenzen.Schritt, Suffix = "m", Rounded = true, TooltipText = tipp };
        feld.ValueChanged += wert =>
        {
            setzen(wert);
            KarteWerteSetzen(); // eine Eingabe wie 620 rundet der Kern auf 600 – das Feld zeigt das
        };
        return feld;
    }

    /// <summary>Die Werte der Tafel „Karte“ aus dem Entwurf (nach jeder Änderung, auch durch Rückgängig), ohne Befehle auszulösen.</summary>
    private void KarteWerteSetzen()
    {
        if (_breiteFeld is null || !IsInstanceValid(_breiteFeld) || !_karteOffen) return;
        var g = _entwurf.Kartengrenzen;
        _breiteFeld.SetValueNoSignal(g.Breite);
        _tiefeFeld!.SetValueNoSignal(g.Tiefe);
        _randFeld!.SetValueNoSignal(g.Randbreite);
        var a = g.Aussen;
        _masseText!.Text = $"mit Rand {Kartenschreiber.Zahl(a.Breite)} × {Kartenschreiber.Zahl(a.Tiefe)} m";
        _kameraText!.Text = _entwurf.Kamera is { } k
            ? $"Punkt x {Kartenschreiber.Zahl(k.Punkt[0])} · z {Kartenschreiber.Zahl(k.Punkt[1])} m, Zoom {Komma(k.Zoom, "0.#")} m Bildhöhe, Drehung {Komma(k.Drehung, "0.#")}°"
            : "Keiner: Das Spiel schaut auf die Mitte der Gebäude.";
    }

    /// <summary>Wie viele Flächen ein neuer Vorschlag ersetzen würde: Natur- und Feldflächen, die ganz im Randstreifen liegen (wie im Kern, <see cref="Kartenentwurf.RandVorschlagen"/>).</summary>
    private int RandFlaechen()
    {
        var spielfeld = _entwurf.Kartengrenzen.Spielfeld;
        bool GanzImRand(EntwurfFlaeche f) => Kartenrechteck.Um(f.Ecken) is { } r && !r.Ueberdeckt(spielfeld);
        return _entwurf.Naturflaechen.Count(GanzImRand) + _entwurf.Felder.Count(GanzImRand);
    }

    /// <summary>„Rand vorschlagen“: gibt es Flächen im Rand, die der Vorschlag ersetzt, erst fragen.</summary>
    private void RandVorschlagenFragen(int? startwert = null)
    {
        var ersetzt = RandFlaechen();
        if (ersetzt == 0)
        {
            RandVorschlagen(startwert);
            return;
        }
        Rueckfrage("Randlandschaft ersetzen?",
            $"Im Randstreifen liegen {ersetzt} {(ersetzt == 1 ? "Fläche" : "Flächen")} (Wald, Streu, Felder), die ganz im Rand liegen. Ein neuer Vorschlag ersetzt "
            + $"{(ersetzt == 1 ? "sie" : "sie alle")}. Flächen, die ins Spielfeld reichen, bleiben. Mit Rückgängig (Strg+Z) kommt alles zurück.",
            "Ersetzen", () => RandVorschlagen(startwert));
    }

    private void RandVorschlagen(int? startwert)
    {
        // jedes Mal ein anderer Vorschlag (der erste Vorschlag einer neuen Karte ist fest, siehe NeueKarte)
        var neu = _entwurf.RandVorschlagen(startwert ?? (int)(GD.Randi() % 100000) + 1);
        Melden(neu.Count == 0 ? "Der Randstreifen ist schon voll – es gab nichts vorzuschlagen."
            : $"Randlandschaft vorgeschlagen: {neu.Count} neue Flächen (Felder, Wald, Streu) – Rückgängig (Strg+Z) nimmt sie wieder weg.");
    }

    /// <summary>„Kamera-Start übernehmen“: der Blick der Werkstatt wird zum Blick beim Start; liegt der Blickpunkt im Rand, kommt er an den Rand des Spielfelds.</summary>
    private void KameraStartUebernehmen()
    {
        var (punkt, zoom, drehung) = _stadt.WerkstattKameraStart(out var verschoben);
        _entwurf.KameraStartSetzen(punkt, zoom, drehung);
        Melden("Kamera-Start übernommen" + (verschoben ? " – der Blickpunkt lag im Randstreifen und ist an den Rand des Spielfelds gerückt." : "."));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Feldfläche
    // ═════════════════════════════════════════════════════════════════════════

    private void FeldTafel(EntwurfFeld f, GridContainer raster)
    {
        Zeile(raster, "Name", Textfeld(f.Name, text => _entwurf.FeldUmbenennen(f, text)));
        var richtung = new Zahlfeld
        {
            MinValue = 0, MaxValue = 359, Step = 1, Suffix = "°", Value = Math.Round(f.Richtung), Rounded = true,
            TooltipText = "In welche Richtung die Furchen und Streifen laufen: 0° = Nord–Süd, 90° = Ost–West",
        };
        richtung.ValueChanged += wert => _entwurf.FeldRichtungSetzen(f, wert);
        Zeile(raster, "Richtung", richtung);
        var streifen = Zahl(FeldVerteilung.StreifenMin, FeldVerteilung.StreifenMax, 0.5, f.Streifen, "m", s => _entwurf.FeldStreifenSetzen(f, s, f.Laenge),
            "Breite eines Ackerstreifens (3 bis 30 m)");
        Zeile(raster, "Streifen", streifen);
        var laenge = Zahl(FeldVerteilung.LaengeMin, FeldVerteilung.LaengeMax, 5, f.Laenge, "m", l => _entwurf.FeldStreifenSetzen(f, f.Streifen, l),
            "Mittlere Länge eines Ackerstücks (10 bis 200 m); jedes Stück ist 60 bis 140 % davon lang und hat eine Frucht");
        Zeile(raster, "Stücke", laenge);
        var wuerfeln = new HBoxContainer();
        wuerfeln.AddThemeConstantOverride("separation", 8);
        var startwert = Wert(f.Startwert.ToString());
        startwert.CustomMinimumSize = new Vector2(70, 0);
        wuerfeln.AddChild(startwert);
        Knopf(wuerfeln, "Neu würfeln", () => _entwurf.FeldStartwertSetzen(f, (int)(GD.Randi() % 100000) + 1), "Dieselben Früchte, andere Stücke");
        Zeile(raster, "Startwert", wuerfeln);
        Zeile(raster, "Äcker", Wert($"{_stadt.WerkstattFeldAnzahl(f)} Stücke – unter Wegen, Gebäuden und Bauzonen bleibt es frei"));

        Hauptmenue.Text(_eigenschaften, "Früchte", 15, Gold);
        var regler = new Dictionary<string, HSlider>();
        void Uebernehmen()
        {
            var gewichte = regler.Where(r => r.Value.Value > 0).ToDictionary(r => r.Key, r => r.Value.Value / 100);
            if (gewichte.Count == 0) Melden("Mindestens eine Frucht braucht einen Anteil über 0.", fehler: true);
            else _entwurf.FeldFruechteSetzen(f, gewichte);
        }
        var obergrenze = Math.Max(100, f.Fruechte.Values.Select(w => Math.Round(w * 100)).DefaultIfEmpty(100).Max());
        var fruechte = new GridContainer { Columns = 2 };
        fruechte.AddThemeConstantOverride("h_separation", 10);
        _eigenschaften.AddChild(fruechte);
        foreach (var (kennung, name) in FeldVerteilung.Fruechte)
        {
            var schieber = Schieber(0, obergrenze, 5, Math.Round(f.Fruechte.GetValueOrDefault(kennung) * 100), a => $"{a:0} %", _ => Uebernehmen(),
                "Anteil dieser Frucht im Verhältnis zu den anderen (0 nimmt sie heraus)");
            regler[kennung] = schieber.GetChild<HSlider>(0);
            Zeile(fruechte, name, schieber);
        }

        Hauptmenue.Text(_eigenschaften, $"Aussparungen: {f.Aussparungen.Count}", 15, Gold);
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 8);
        _eigenschaften.AddChild(zeile);
        Knopf(zeile, "Aussparung zeichnen", () => KatalogWaehlen("aussparung"), "Ecken klicken, Doppelklick schließt – dort liegt kein Acker");
        for (var i = 0; i < f.Aussparungen.Count; i++)
        {
            var nummer = i;
            Knopf(zeile, $"{i + 1} löschen", () => _entwurf.AussparungLoeschen(f, nummer), $"Aussparung {i + 1} entfernen");
        }
        Hauptmenue.Text(_eigenschaften, "Die Äcker folgen dem Raster der Streifen, nicht den Ecken: Wer eine Ecke schiebt, verschiebt keinen Acker. Wo Wege, Gebäude oder Bauzonen liegen, bleibt es frei.",
            12, Grau, umbruchBreite: Rechtsbreite - 44);
    }
}
