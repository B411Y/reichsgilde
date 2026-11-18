using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die <b>Zeitleiste</b> oben in der Mitte (UI-Umbau „Zunftlade“, Teil 1; vorher Kopfzeile und Zeitstrahl aus PR #197) – ein Teil der
/// <c>partial class Hauptszene</c>: ganz oben das Band des Tages mit Sonne bzw. Mond (<see cref="Zeitstrahl"/>), darunter links Pause und
/// Tempo, rechts Jahreszeit, Jahr, Uhrzeit, Tagesabschnitt und Wetter mit Aussicht. Holz mit Steinrand; das Aussehen steht im Thema der Lade
/// (<see cref="LadeStil"/>). Die Werte des Hauses stehen unten in der Lade (<c>Hauptszene.Lade.cs</c>).
/// </summary>
public partial class Hauptszene
{
    private LadeFlaeche _zeitTafel = null!;
    private Label _zeitDatum = null!;
    private Label _zeitUhr = null!;
    private Label _zeitPhase = null!;
    private WetterAnzeige _wetter = null!;
    private Zeitstrahl _zeitstrahl = null!;
    private Button _pauseKnopf = null!;
    private HBoxContainer _tempoleiste = null!;
    private Dictionary<int, int>? _lebenswegFrei;            // Person → freie Punkte beim letzten Blick (null: noch nicht gelesen)

    private void ZeitleisteBauen()
    {
        _zeitTafel = LadeStil.Flaeche(null, "LadeZeit");
        _zeitTafel.Name = "Zeitleiste";
        LadeStil.Anwenden(_zeitTafel);
        _zeitTafel.MouseFilter = Control.MouseFilterEnum.Stop;
        _zeitTafel.CustomMinimumSize = new Vector2(LadeStil.Mass("zeit_breite"), 0);
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_zeitTafel);
        _zeitTafel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop, Control.LayoutPresetMode.Minsize);
        _zeitTafel.GrowHorizontal = Control.GrowDirection.Both;
        _zeitTafel.OffsetTop = LadeStil.Mass("zeit_oben");

        var spalte = LadeStil.Behaelter<VBoxContainer>(_zeitTafel, "LadeZeitSpalte");
        // Ganz oben: das Band des Tages
        _zeitstrahl = new Zeitstrahl { Himmel = LadeStil.Farbe("zeit_himmel") };
        spalte.AddChild(_zeitstrahl);
        // Darunter links Pause und Tempo, rechts Jahreszeit, Jahr, Uhrzeit, Tagesabschnitt und Wetter
        var reihe = LadeStil.Behaelter<HBoxContainer>(spalte, "LadeZeitReihe");
        var knoepfe = LadeStil.Behaelter<HBoxContainer>(reihe, "LadeReihe");
        _pauseKnopf = LadeStil.Knopf(knoepfe, "", "Pause (Leertaste)", PauseUmschalten);
        _pauseKnopf.CustomMinimumSize = new Vector2(LadeStil.Mass("pause_breite"), 0);
        _pauseKnopf.SetMeta("klang", "still");
        _tempoleiste = LadeStil.Behaelter<HBoxContainer>(knoepfe, "LadeReihe");
        foreach (var tempo in Taktgeber.AlleTempostufen /* TESTTEMPO */)
        {
            // TESTTEMPO: Die Teststufen stehen nur mit den Entwickler-Optionen in der Leiste (orange, mit Hinweis); die Zifferntasten wählen sie weiter.
            var test = !Taktgeber.Tempostufen.Contains(tempo);
            var knopf = LadeStil.Knopf(_tempoleiste, $"{tempo}×",
                test ? "Nur zum Testen – wird vor der Veröffentlichung entfernt" : $"Spielgeschwindigkeit {tempo}-fach (Zifferntasten wählen die Stufen, Leertaste pausiert)",
                () => TempoSetzen(tempo), test ? "LadeKnopfTest" : "LadeKnopf");
            knopf.ToggleMode = true;
            knopf.CustomMinimumSize = new Vector2(LadeStil.Mass("tempo_breite"), 0);
            knopf.Visible = !test || Entwickler.Aktiv;
            knopf.SetMeta("klang", "still");
            _tempoKnoepfe.Add(knopf);
        }
        var luft = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        reihe.AddChild(luft);
        var text = LadeStil.Behaelter<HBoxContainer>(reihe, "LadeReihe");
        text.MouseFilter = Control.MouseFilterEnum.Stop; // für den Hinweis
        text.TooltipText = "Jahreszeit, Jahr, Uhrzeit und Tagesabschnitt. Ein Spieltag dauert von 4 bis 23 Uhr, gearbeitet wird von 8 bis 20 Uhr; 1140 Ticks sind ein Jahr.";
        _zeitDatum = LadeStil.Text(text, "", "LadeZeitText");
        _zeitUhr = LadeStil.Text(text, "", "LadeZeitUhr");
        _zeitPhase = LadeStil.Text(text, "", "LadeZeitText");
        _wetter = new WetterAnzeige(); // Wetter jetzt und Aussicht (PR #195)
        reihe.AddChild(_wetter);
    }

    /// <summary>Jedes Bild: die Zeitleiste an die Simulation angleichen.</summary>
    private void ZeitleisteAnzeigen()
    {
        var zeit = _simulation.Zeit;
        var takt = _simulation.Takt;
        Setzen(_zeitDatum, $"{KalenderTexte.Name(zeit.Jahreszeit)} · {zeit.Jahr} ·");
        Setzen(_zeitUhr, zeit.UhrzeitText);
        Setzen(_zeitPhase, $"· {KalenderTexte.Name(zeit.Tagesphase)}");
        _wetter.Zeigen(_stadt.WetterJetzt, _stadt.WetterAussicht(3));
        _zeitstrahl.Jahreszeit = _stadt.JahreszeitErzwungen ?? zeit.Jahreszeit; // Nacht und Mond nach den Sonnenzeiten der Jahreszeit, wie das Licht der Stadt
        _zeitstrahl.Minute = _stadt.LichtUhrErzwungen is { } lichtUhr ? (int)(lichtUhr * 60) - Spielzeit.TagesbeginnMinute : zeit.MinuteImTag;
        var pause = _simulation.IstBeendet ? "Partie beendet" : takt.IstPausiert ? "▶ Weiter" : "❚❚ Pause";
        if (_pauseKnopf.Text != pause) _pauseKnopf.Text = pause;
        // Ein Haltegrund (Ratssitzung, Jahresbericht im Einzelspiel) sperrt Pause und Tempo, bis das Fenster zu ist
        var gesperrt = takt.IstAngehalten;
        _pauseKnopf.Disabled = _simulation.IstBeendet || gesperrt;
        var tipp = gesperrt && takt.Haltegruende.All(g => g == HaltegrundPlatzwahl) ? "Die Zeit ruht, bis Wohnhaus und Werkstatt ihren Platz haben."
            : gesperrt ? $"Die Zeit ruht, bis {string.Join(" und ", takt.Haltegruende)} {(takt.Haltegruende.Count > 1 ? "geschlossen sind" : "geschlossen ist")}." : "Pause (Leertaste)";
        if (_pauseKnopf.TooltipText != tipp) _pauseKnopf.TooltipText = tipp;
        for (var i = 0; i < _tempoKnoepfe.Count; i++)
        {
            _tempoKnoepfe[i].SetPressedNoSignal(!takt.IstPausiert && Taktgeber.AlleTempostufen /* TESTTEMPO */[i] == takt.Tempo);
            _tempoKnoepfe[i].Disabled = gesperrt;
        }
    }

    /// <summary>Die Person des eigenen Hauses mit den meisten freien Punkten der Lebensweise (Oberhaupt, Ehepartner, Erbe), sonst null.</summary>
    private Reichsgilde.Core.Dynastie.Person? LebenswegPersonMitPunkten(Reichsgilde.Core.Wirtschaft.Stadtwirtschaft w)
    {
        if (_spieler is null || w.Familien.Von(_spieler) is not { } haus) return null;
        var buch = w.Familien;
        return new[] { haus.Oberhaupt, haus.Ehepartner, buch.Erbe(haus) }.OfType<Reichsgilde.Core.Dynastie.Person>().Distinct()
            .Where(buch.HatLebensweg).Select(p => (Person: p, Frei: Reichsgilde.Core.Dynastie.TalentTexte.Alle.Sum(t => buch.FreiePunkte(p, t))))
            .Where(x => x.Frei > 0).OrderByDescending(x => x.Frei).Select(x => x.Person).FirstOrDefault();
    }

    /// <summary>
    /// Lebensweise (Teil 3a): Der Knopf bei den Werten der Lade zeigt freie Punkte der eigenen Leute; wird einer neu frei, kommt eine Meldung
    /// („Neuigkeiten aus dem Haus“). Der Kern sagt es nicht von selbst – darum liest die Oberfläche die Punkte jedes Bild (ein paar Rechnungen).
    /// </summary>
    private void LebenswegPunkteAnzeigen(Reichsgilde.Core.Wirtschaft.Stadtwirtschaft w)
    {
        if (_spieler is null || !w.Familien.HatLebensweise || w.Familien.Von(_spieler) is not { } haus)
        {
            _ladePunkte.Visible = false;
            _lebenswegFrei = null;
            return;
        }
        var buch = w.Familien;
        var jetzt = new Dictionary<int, int>();
        var personen = new[] { haus.Oberhaupt, haus.Ehepartner, buch.Erbe(haus) }.OfType<Reichsgilde.Core.Dynastie.Person>().Distinct().Where(buch.HatLebensweg).ToList();
        foreach (var p in personen)
            jetzt[p.Nummer] = Reichsgilde.Core.Dynastie.TalentTexte.Alle.Sum(t => buch.FreiePunkte(p, t));
        if (_lebenswegFrei is not null)
            foreach (var p in personen)
                if (jetzt[p.Nummer] > _lebenswegFrei.GetValueOrDefault(p.Nummer, 0))
                {
                    var pfad = Reichsgilde.Core.Dynastie.TalentTexte.Alle.First(t => buch.FreiePunkte(p, t) > 0);
                    var name = buch.LebensweiseDaten?.Pfade.GetValueOrDefault(pfad)?.Name ?? "der Lebensweise";
                    Protokollieren(new Reichsgilde.Core.Wirtschaft.Meldung(Reichsgilde.Core.Wirtschaft.Meldungsart.Sonstiges,
                        $"Neuer Punkt der Lebensweise: {p.Vorname} kann einen Knoten im {name} lernen.")
                        { Haus = w.Konten.ToList().IndexOf(_spieler), Person = p.Nummer, Wichtigkeit = Reichsgilde.Core.Wirtschaft.Meldungswichtigkeit.Normal, Minute = _simulation.Zeit.VergangeneMinuten });
                }
        _lebenswegFrei = jetzt;
        var summe = jetzt.Values.Sum();
        _ladePunkte.Visible = summe > 0;
        if (summe <= 0) return;
        Setzen(_ladePunkte, summe == 1 ? "1 Punkt frei" : $"{summe} Punkte frei");
        _ladePunkte.TooltipText = "Freie Punkte der Lebensweise – ein Klick öffnet den Lebensweg:\n" + string.Join("\n", personen.Where(p => jetzt[p.Nummer] > 0).Select(p => $"{p.Vorname}: {jetzt[p.Nummer]}"));
    }

    /// <summary>Setzt einen Text nur bei einer Änderung (sonst misst Godot die Tafel jedes Bild neu).</summary>
    private static void Setzen(Label label, string text)
    {
        if (label.Text != text) label.Text = text;
    }

    /// <summary>Wie <see cref="Setzen(Label, string)"/>, für die Beschriftung eines Knopfs.</summary>
    private static void Setzen(Button knopf, string text)
    {
        if (knopf.Text != text) knopf.Text = text;
    }
}
