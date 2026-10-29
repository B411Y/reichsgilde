using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Tafel der sichtbaren Ratssitzung (PR #202) unten in der Bildmitte: Sie führt Punkt für Punkt durch die Sitzung und meldet jeden Schritt, damit
/// die Stadtansicht ihn im Ratssaal zeigt (<see cref="PunktGezeigt"/>, <see cref="StimmeGezeigt"/> …).
///
/// Ablauf je Punkt (Dauer bei Tempo 1):
/// <list type="number">
/// <item><b>Aufruf</b> (2 s): Der Bürgermeister ruft den Punkt auf – Titel und worum es geht.</item>
/// <item><b>Rede</b> (3 s): der Antragsteller mit seiner Begründung in einer Zeile; bei Wahlen, wer antritt.</item>
/// <item><b>Stimmen</b> (je 1,2 s): Sitz für Sitz, höchstes Amt zuerst, mit einer Zeile Begründung. Darf der Spieler an seinem Sitz noch stimmen
/// (<see cref="Sitzungsstimme.Frei"/>), wartet die Tafel dort: Ja, Nein, Enthaltung bzw. ein Kandidat; vorausgewählt ist die Stimme, die er vorab gesetzt hat.
/// Ein Klick schickt den Befehl, die Tafel liest den Punkt neu; „Weiter“ gibt die Stimme ab.</item>
/// <item><b>Ergebnis</b> (3,5 s): <see cref="Simulation.RatssitzungWeiter"/> hält den Punkt ab; Zählung, Stichentscheid des Bürgermeisters bzw. Los, Beschluss oder Gewählter.</item>
/// </list>
/// „Weiter“ springt zum nächsten Schritt, „Schneller“ schaltet das Tempo 1×, 2×, 4× durch, „Überspringen“ hält alle übrigen Punkte ohne Bild ab
/// (<see cref="Simulation.RatssitzungAbschliessen"/>). Mit drei Punkten und sieben Sitzen dauert eine Sitzung etwa eine Minute.
///
/// Wiederholung (<see cref="WiederholungZeigen"/>): dieselbe Tafel zeigt die letzte Sitzung aus ihren abgehaltenen Punkten, ohne Stimmrecht und ohne den Kern zu fragen.
/// </summary>
public partial class RatssitzungAnzeige : Control
{
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.72f, 0.72f, 0.72f);
    private static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);
    private const int Breite = 1040;

    private enum Schritt { Aufruf, Rede, Stimme, Spieler, Ergebnis }

    private static double Dauer(Schritt schritt) => schritt switch
    {
        Schritt.Aufruf => 2.0,
        Schritt.Rede => 3.0,
        Schritt.Stimme => 1.2,
        Schritt.Ergebnis => 3.5,
        _ => double.PositiveInfinity, // der Spieler stimmt: warten
    };

    private Simulation? _simulation;
    private Konto? _spieler;
    /// <summary>Wiederholung der letzten Sitzung: die Punkte mit Ergebnis (null: die laufende Sitzung).</summary>
    private List<Sitzungspunkt>? _wiederholung;
    private int _wiederholungNummer;
    private int _jahr;
    private int _anzahl;

    private Sitzungspunkt? _punkt;
    private Schritt _schritt;
    /// <summary>Wie viele Stimmen des Punktes schon gezeigt sind.</summary>
    private int _gezeigt;
    private double _zeit;
    private int _tempo = 1;

    /// <summary>Wer gerade spricht oder stimmt (PR #211): der Bürgermeister beim Aufruf, der Antragsteller bei der Rede, dann Sitz für Sitz; nickt bei Zustimmung.</summary>
    private PortraetAnzeige _redner = null!;
    private Label _rednerName = null!;
    private Label _kopf = null!, _titel = null!, _text = null!, _rede = null!, _aktuell = null!, _ergebnis = null!, _hinweis = null!;
    private GridContainer _stimmen = null!;
    private HFlowContainer _wahl = null!;
    private HBoxContainer _wahlKnoepfe = null!;
    private Label _wahlFrage = null!;
    private Button _weiter = null!, _schneller = null!, _ueberspringen = null!;

    /// <summary>Läuft eine Sitzung oder Wiederholung auf der Tafel?</summary>
    public bool Laeuft => Visible;

    /// <summary>Die Sitzung wartet (Spielmenü, Einstellungen oder Hilfe sind offen) – die Hauptszene setzt es jedes Bild.</summary>
    public bool Angehalten { get; set; }

    /// <summary>Ein neuer Punkt wird aufgerufen (die Stimmen sind noch nicht gezeigt).</summary>
    public event Action<Sitzungspunkt>? PunktGezeigt;
    /// <summary>Der Antragsteller ist fertig, die Abstimmung beginnt.</summary>
    public event Action? RedeBeendet;
    /// <summary>Dieser Sitz ist am Zug (null: keiner).</summary>
    public event Action<Amt?>? AmZug;
    /// <summary>Eine Stimme wird gezeigt (auch erneut, wenn der Spieler seine ändert oder das Los entschieden hat).</summary>
    public event Action<Sitzungsstimme>? StimmeGezeigt;
    /// <summary>Der Punkt ist abgehalten (mit <see cref="Sitzungspunkt.Ergebnis"/>).</summary>
    public event Action<Sitzungspunkt>? ErgebnisGezeigt;
    /// <summary>Die Sitzung (oder Wiederholung) ist zu Ende; true: es war eine Wiederholung.</summary>
    public event Action<bool>? Beendet;
    /// <summary>Ergebnis eines Befehls – für die Meldungen.</summary>
    public event Action<string>? Gemeldet;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        var unten = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End, MouseFilter = MouseFilterEnum.Ignore };
        unten.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        unten.OffsetBottom = -14;
        AddChild(unten);
        var mitte = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        unten.AddChild(mitte);
        var (tafel, spalte) = Hauptmenue.Tafel(Breite);
        tafel.MouseFilter = MouseFilterEnum.Stop;
        mitte.AddChild(tafel);
        spalte.AddThemeConstantOverride("separation", 6);

        _kopf = Hauptmenue.Text(spalte, "", 13, Grau);
        _kopf.TooltipText = "Die Ratssitzung am Jahresende: Der Rat entscheidet über Anträge und Wahlen, die Mehrheit zählt, der Bürgermeister gibt bei Gleichstand den Ausschlag.";
        var reihe = new HBoxContainer();
        reihe.AddThemeConstantOverride("separation", 24);
        spalte.AddChild(reihe);

        // Ganz links: wer spricht
        var rednerSpalte = new VBoxContainer();
        rednerSpalte.AddThemeConstantOverride("separation", 4);
        reihe.AddChild(rednerSpalte);
        _redner = PortraetAnzeige.Neu(PortraetAnzeige.Mittel);
        _redner.Visible = false;
        rednerSpalte.AddChild(_redner);
        _rednerName = Hauptmenue.Text(rednerSpalte, "", 12, Grau, zentriert: true, umbruchBreite: PortraetAnzeige.Mittel);
        _rednerName.TooltipText = "Wer gerade spricht: ein Ratsherr oder der Vertreter eines Hauses.";

        // Links: Punkt, Rede, Ergebnis und die eigene Stimme
        var links = new VBoxContainer { CustomMinimumSize = new Vector2(400, 0) };
        links.AddThemeConstantOverride("separation", 6);
        reihe.AddChild(links);
        _titel = Hauptmenue.Text(links, "", 21, Gold, umbruchBreite: 400);
        _titel.TooltipText = "Der Punkt der Tagesordnung, über den gerade verhandelt wird.";
        _text = Hauptmenue.Text(links, "", 15, null, umbruchBreite: 400);
        _rede = Hauptmenue.Text(links, "", 14, new Color(0.9f, 0.86f, 0.76f), umbruchBreite: 400);
        _rede.TooltipText = "Die Rede für den Antrag oder dagegen; sie zeigt, wie der Redner abwägt.";
        _aktuell = Hauptmenue.Text(links, "", 14, null, umbruchBreite: 400);
        _aktuell.TooltipText = "Die Stimme, die gerade fällt, mit ihrer Begründung."; // die Stimme, die gerade fällt, mit ganzer Begründung
        // Die eigene Stimme: eine Zeile mit Frage und Knöpfen (die Tafel soll den Ratstisch nicht verdecken)
        _wahl = new HFlowContainer { Visible = false, CustomMinimumSize = new Vector2(400, 0) };
        _wahl.AddThemeConstantOverride("h_separation", 6);
        _wahl.AddThemeConstantOverride("v_separation", 4);
        links.AddChild(_wahl);
        _wahlFrage = Hauptmenue.Text(_wahl, "", 15, Gold);
        _wahlFrage.TooltipText = "Eure Stimme: Ja, Nein oder Enthaltung. Eine Enthaltung zählt nicht.";
        _wahlFrage.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _wahlKnoepfe = new HBoxContainer();
        _wahlKnoepfe.AddThemeConstantOverride("separation", 6);
        _wahl.AddChild(_wahlKnoepfe);
        _ergebnis = Hauptmenue.Text(links, "", 16, null, umbruchBreite: 400);
        _ergebnis.TooltipText = "Das Ergebnis der Abstimmung: Ja gegen Nein; bei Gleichstand entscheidet der Bürgermeister.";
        _hinweis = Hauptmenue.Text(links, "", 12, Grau, umbruchBreite: 400);

        // Rechts: die Stimmen der Sitze
        _stimmen = new GridContainer { Columns = 3, CustomMinimumSize = new Vector2(430, 0) };
        _stimmen.AddThemeConstantOverride("h_separation", 12);
        _stimmen.AddThemeConstantOverride("v_separation", 3);
        reihe.AddChild(_stimmen);

        spalte.AddChild(new HSeparator());
        var knoepfe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        knoepfe.AddThemeConstantOverride("separation", 12);
        spalte.AddChild(knoepfe);
        _weiter = Knopf(knoepfe, "Weiter", WeiterGedrueckt, "Zum nächsten Schritt (Leertaste)");
        _schneller = Knopf(knoepfe, "Schneller (1×)", () =>
        {
            _tempo = _tempo >= 4 ? 1 : _tempo * 2;
            _schneller!.Text = $"Schneller ({_tempo}×)";
        }, "Tempo der Sitzung: 1×, 2×, 4×");
        _ueberspringen = Knopf(knoepfe, "Überspringen", Ueberspringen, "Alle übrigen Punkte ohne Bild abhalten – die Ergebnisse stehen im Protokoll");
        Visible = false;
    }

    /// <summary>Setzt den Text einer Zeile; eine leere Zeile nimmt keinen Platz weg (die Tafel soll den Ratstisch nicht verdecken).</summary>
    private static void Setzen(Label zeile, string text)
    {
        zeile.Text = text;
        zeile.Visible = text.Length > 0;
    }

    private static Button Knopf(Container eltern, string text, Action aktion, string tipp)
    {
        var knopf = new Button { Text = text, TooltipText = tipp, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(150, 36) };
        knopf.AddThemeFontSizeOverride("font_size", 15);
        knopf.Pressed += aktion;
        eltern.AddChild(knopf);
        return knopf;
    }

    public void Verbinden(Simulation simulation, Konto spieler)
    {
        _simulation = simulation;
        _spieler = spieler;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Beginn und Ende
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Beginnt die Tafel zur laufenden Sitzung des Kerns (<see cref="Simulation.Ratssitzung"/>).</summary>
    public void SitzungZeigen()
    {
        if (_simulation?.Ratssitzung is not { } sitzung) return;
        _wiederholung = null;
        _jahr = sitzung.Jahr;
        _anzahl = sitzung.Anzahl;
        _ueberspringen.Text = "Überspringen";
        _ueberspringen.TooltipText = "Alle übrigen Punkte ohne Bild abhalten – die Ergebnisse stehen im Protokoll";
        Show();
        PunktBeginnen(sitzung.AktuellerPunkt);
    }

    /// <summary>Zeigt eine abgehaltene Sitzung noch einmal (Knopf „Letzte Sitzung“ im Rathaus).</summary>
    public void WiederholungZeigen(Ratssitzung sitzung)
    {
        if (sitzung.Abgehalten.Count == 0) return;
        _wiederholung = sitzung.Abgehalten.ToList();
        _wiederholungNummer = 0;
        _jahr = sitzung.Jahr;
        _anzahl = _wiederholung.Count;
        _ueberspringen.Text = "Schließen";
        _ueberspringen.TooltipText = "Die Wiederholung beenden";
        Show();
        PunktBeginnen(_wiederholung[0]);
    }

    private void Ueberspringen()
    {
        if (_wiederholung is null && _simulation?.Ratssitzung is not null)
        {
            _simulation.RatssitzungAbschliessen();
            Gemeldet?.Invoke("Die übrigen Punkte der Ratssitzung sind abgehalten – die Ergebnisse stehen im Protokoll.");
        }
        Beenden();
    }

    private void Beenden()
    {
        var wiederholung = _wiederholung is not null;
        Hide();
        _punkt = null;
        _wiederholung = null;
        AmZug?.Invoke(null);
        Beendet?.Invoke(wiederholung);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Ablauf
    // ═════════════════════════════════════════════════════════════════════════

    public override void _Process(double delta)
    {
        if (!Visible || _punkt is null || Angehalten) return;
        if (_wiederholung is null && _simulation?.Ratssitzung is null && _schritt != Schritt.Ergebnis)
        {
            Beenden(); // die Sitzung ist anders zu Ende gegangen (z. B. Partie beendet)
            return;
        }
        _zeit += delta * _tempo;
        if (_zeit >= Dauer(_schritt)) Weiter();
    }

    /// <summary>Knopf „Weiter“ und Leertaste: den Schritt sofort beenden (am eigenen Sitz: die gewählte Stimme abgeben).</summary>
    public void WeiterGedrueckt()
    {
        if (Visible && _punkt is not null && !Angehalten) Weiter();
    }

    private void PunktBeginnen(Sitzungspunkt? punkt)
    {
        if (punkt is null)
        {
            // Kein Punkt mehr, die Sitzung steht aber noch: den Rest ohne Bild abhalten
            if (_wiederholung is null && _simulation?.Ratssitzung is not null) _simulation.RatssitzungAbschliessen();
            Beenden();
            return;
        }
        _punkt = punkt;
        _gezeigt = 0;
        _schritt = Schritt.Aufruf;
        _zeit = 0;
        _kopf.Text = _wiederholung is null
            ? $"Ratssitzung {_jahr} · Punkt {punkt.Nummer} von {_anzahl}"
            : $"Letzte Ratssitzung {_jahr} (Wiederholung) · Punkt {punkt.Nummer} von {_anzahl}";
        _titel.Text = punkt.Titel;
        _text.Text = punkt.Text;
        Setzen(_rede, "Der Bürgermeister ruft den Punkt auf.");
        RednerZeigen(punkt.Stimmen.FirstOrDefault(x => x.Sitz.Name == "Bürgermeister")?.Sitz, null);
        Setzen(_aktuell, "");
        Setzen(_ergebnis, "");
        Setzen(_hinweis, "");
        _wahl.Visible = false;
        StimmenAufbauen();
        PunktGezeigt?.Invoke(punkt);
    }

    private void Weiter()
    {
        if (_punkt is not { } punkt) return;
        _zeit = 0;
        switch (_schritt)
        {
            case Schritt.Aufruf:
                _schritt = Schritt.Rede;
                if (punkt.Antragsteller is { } redner) RednerZeigen(null, redner);
                Setzen(_rede, punkt.Antragsteller is { } a
                    ? $"{a.Inhaber}: „{punkt.Begruendung}“"
                    : punkt.Begruendung);
                break;
            case Schritt.Rede:
                RedeBeendet?.Invoke();
                _schritt = Schritt.Stimme;
                NaechsteStimme();
                break;
            case Schritt.Stimme:
                NaechsteStimme();
                break;
            case Schritt.Spieler:
                _wahl.Visible = false;
                StimmeZeigen(_gezeigt - 1);
                _schritt = Schritt.Stimme;
                NaechsteStimme();
                break;
            case Schritt.Ergebnis:
                if (_wiederholung is { } liste)
                    PunktBeginnen(++_wiederholungNummer < liste.Count ? liste[_wiederholungNummer] : null);
                else if (_simulation?.Ratssitzung is { } sitzung)
                    PunktBeginnen(sitzung.AktuellerPunkt);
                else
                    Beenden();
                break;
        }
    }

    /// <summary>Die nächste Stimme zeigen – oder am eigenen Sitz warten; nach der letzten den Punkt abhalten.</summary>
    private void NaechsteStimme()
    {
        var punkt = _punkt!;
        if (_gezeigt >= punkt.Stimmen.Count)
        {
            Abhalten();
            return;
        }
        var stimme = punkt.Stimmen[_gezeigt++];
        AmZug?.Invoke(stimme.Sitz);
        RednerZeigen(stimme.Sitz, null, stimme.Ja != false && !(stimme.Ja is null && stimme.Kandidat is null));
        if (_wiederholung is null && stimme.Frei && stimme.Haus == _spieler && _simulation?.Ratssitzung is { } sitzung
            && sitzung.StimmeVerboten(punkt, _spieler!) is null)
        {
            _schritt = Schritt.Spieler;
            EigeneStimmeAufbauen();
            StimmeZeigen(_gezeigt - 1);
            return;
        }
        StimmeZeigen(_gezeigt - 1);
    }

    /// <summary>Alle Stimmen sind gezeigt: den Punkt im Kern abhalten (das Los entscheidet erst jetzt) und das Ergebnis zeigen.</summary>
    private void Abhalten()
    {
        AmZug?.Invoke(null);
        var punkt = _punkt!;
        if (_wiederholung is null && _simulation?.Ratssitzung is not null)
        {
            if (_simulation.RatssitzungWeiter() is { } abgehalten) punkt = abgehalten;
            _punkt = punkt;
            for (var i = 0; i < punkt.Stimmen.Count; i++)
                if (punkt.Stimmen[i].Los) StimmeZeigen(i); // gezogener Kandidat
        }
        _schritt = Schritt.Ergebnis;
        ErgebnisZeigen(punkt);
        ErgebnisGezeigt?.Invoke(punkt);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Anzeige
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Je Sitz eine Zeile: Amt (und Name), Stimme, Begründung – die Stimme erst, wenn sie gezeigt ist.</summary>
    private void StimmenAufbauen()
    {
        UiHilfe.KinderEntfernen(_stimmen);
        foreach (var stimme in _punkt!.Stimmen)
        {
            var eigen = stimme.Haus is not null && stimme.Haus == _spieler;
            var sitzLabel = Hauptmenue.Text(_stimmen, $"{stimme.Sitz.Name}: {stimme.Name}", 13, eigen ? Gold : null);
            sitzLabel.CustomMinimumSize = new Vector2(190, 0);
            sitzLabel.TooltipText = $"{stimme.Sitz.Name}: {stimme.Name}" + (eigen ? " – euer Haus." : ".");
            var ja = Hauptmenue.Text(_stimmen, "…", 13, Grau);
            ja.CustomMinimumSize = new Vector2(80, 0);
            ja.TooltipText = "Die Stimme, sobald sie gefallen ist.";
            var grund = Hauptmenue.Text(_stimmen, "", 12, Grau);
            grund.CustomMinimumSize = new Vector2(140, 0);
            grund.ClipText = true;
            grund.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        }
    }

    /// <summary>Zeigt, wer spricht: den Inhaber eines Sitzes oder das Oberhaupt eines Hauses; ohne Familie (alter Spielstand) bleibt das Bild leer.</summary>
    private void RednerZeigen(Amt? sitz, Konto? haus, bool nicken = false)
    {
        if (_simulation?.Wirtschaft is not { } w) return;
        var da = sitz is not null ? _redner.ZeigenAmt(w, sitz) : haus is not null && _redner.ZeigenHaus(w, haus);
        _redner.Visible = da;
        _rednerName.Text = da ? (sitz is not null ? $"{sitz.Name}\n{sitz.Inhaber}" : haus!.Inhaber) : "";
        _rednerName.Visible = da;
        if (da && nicken) _redner.Nicken();
    }

    private void StimmeZeigen(int nummer)
    {
        var stimme = _punkt!.Stimmen[nummer];
        if (_stimmen.GetChildCount() <= nummer * 3 + 2) return;
        var feld = (Label)_stimmen.GetChild(nummer * 3 + 1);
        var grund = (Label)_stimmen.GetChild(nummer * 3 + 2);
        var (text, farbe) = stimme.Kandidat is { } k ? (k + (stimme.Los ? " (Los)" : ""), Gold)
            : stimme.Ja switch { true => ("Ja", Gruen), false => ("Nein", Rot), _ => ("Enthaltung", Grau) };
        feld.Text = text;
        feld.AddThemeColorOverride("font_color", farbe);
        grund.Text = stimme.Begruendung;
        grund.TooltipText = stimme.Begruendung;
        grund.MouseFilter = MouseFilterEnum.Pass;
        if (nummer == _gezeigt - 1 && _schritt == Schritt.Stimme)
        {
            Setzen(_aktuell, $"{stimme.Sitz.Name} {stimme.Name}: {text} – {stimme.Begruendung}");
            _aktuell.AddThemeColorOverride("font_color", farbe);
        }
        StimmeGezeigt?.Invoke(stimme);
    }

    /// <summary>Am eigenen Sitz: die Knöpfe zum Abstimmen, die gesetzte Stimme gedrückt.</summary>
    private void EigeneStimmeAufbauen()
    {
        var punkt = _punkt!;
        var stimme = punkt.Stimmen[_gezeigt - 1];
        UiHilfe.KinderEntfernen(_wahlKnoepfe);
        _wahl.Visible = true;
        _wahlFrage.Text = $"Eure Stimme als {stimme.Sitz.Name}:";
        Setzen(_aktuell, ""); // die Knöpfe zeigen die eigene Stimme
        if (punkt.Thema == Sitzungsthema.Wahl)
        {
            foreach (var kandidat in punkt.Kandidaten)
                WahlKnopf(kandidat.Name + (kandidat.IstAmtsinhaber ? " (bisher)" : ""), stimme.Kandidat == kandidat.Name,
                    () => Stimmen(new WahlStimme(punkt.Amt!.Name, kandidat.Name)));
        }
        else
        {
            foreach (var (text, ja) in new[] { ("Ja", (bool?)true), ("Nein", false), ("Enthaltung", null) })
                WahlKnopf(text, stimme.Ja == ja, () => Stimmen(AntragsStimme(punkt, ja)));
        }
        Setzen(_hinweis, "„Weiter“ gibt die Stimme ab (vorausgewählt: eure Stimme von vorab).");
    }

    private void WahlKnopf(string text, bool gedrueckt, Action aktion)
    {
        var knopf = new Button { Text = text, ToggleMode = true, ButtonPressed = gedrueckt, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 32) };
        knopf.AddThemeFontSizeOverride("font_size", 14);
        if (gedrueckt) knopf.AddThemeColorOverride("font_pressed_color", Gold);
        knopf.Pressed += aktion;
        _wahlKnoepfe.AddChild(knopf);
    }

    private static Befehl AntragsStimme(Sitzungspunkt punkt, bool? ja) => punkt.Thema switch
    {
        Sitzungsthema.Statut => new StatutStimme(ja),
        Sitzungsthema.Stadtglaube => new StadtglaubeStimme(ja),
        Sitzungsthema.Pflaster => new PflasterStimme(ja),
        _ => new RatsantragStimme(punkt.Antragsart!.Value, ja),
    };

    /// <summary>Schickt die Stimme an den Kern und liest den Punkt neu (die Stimmen davor können sich mitändern, z. B. die Zählung).</summary>
    private void Stimmen(Befehl befehl)
    {
        if (_simulation?.Ratssitzung is not { } sitzung) return;
        UiHilfe.Befehlen(_simulation, _spieler, befehl, this, ergebnis =>
        {
            if (!ergebnis.Erfolg) Gemeldet?.Invoke("✗ " + ergebnis.Meldung);
            if (_punkt is null) return;
            if (sitzung.AktuellerPunkt is { } neu && neu.Nummer == _punkt.Nummer) _punkt = neu;
            for (var i = 0; i < _gezeigt; i++) StimmeZeigen(i);
            EigeneStimmeAufbauen();
        });
    }

    private void ErgebnisZeigen(Sitzungspunkt punkt)
    {
        _wahl.Visible = false;
        Setzen(_aktuell, "");
        Setzen(_hinweis, "");
        if (punkt.Ergebnis is not { } e)
        {
            Setzen(_ergebnis, "");
            return;
        }
        if (punkt.Thema == Sitzungsthema.Wahl)
        {
            var zaehlung = string.Join(", ", e.Zaehlung.Select(z => $"{z.Kandidat} {z.Stimmen}"));
            Setzen(_ergebnis, (e.Gewaehlt is { } g ? $"Gewählt: {g}" : "Niemand gewählt") + (e.Stichentscheid ? " – Gleichstand, das Los entschied" : "")
                + (zaehlung.Length > 0 ? $"\nStimmen: {zaehlung}" : ""));
            _ergebnis.AddThemeColorOverride("font_color", Gold);
        }
        else
        {
            var zaehlung = $"{e.Ja} Ja, {e.Nein} Nein, {e.Enthaltung} Enthaltung";
            var stich = e.Stichentscheid && e.Buergermeister is { } bm ? $" – Gleichstand, der Bürgermeister stimmt {(bm ? "dafür" : "dagegen")}" : "";
            Setzen(_ergebnis, $"{(e.Angenommen ? "Angenommen" : "Abgelehnt")}: {zaehlung}{stich}");
            _ergebnis.AddThemeColorOverride("font_color", e.Angenommen ? Gruen : Rot);
        }
        if (e.Protokoll.Count > 0) Setzen(_hinweis, string.Join("\n", e.Protokoll));
    }
}
