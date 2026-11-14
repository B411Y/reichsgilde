using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Oberfläche der sichtbaren Ratssitzung (PR #202, Szene Teil 1 14.11.2026): Sie führt Punkt für Punkt durch die Sitzung und meldet jeden Schritt, damit
/// die Stadtansicht ihn im Ratssaal zeigt (<see cref="PunktGezeigt"/>, <see cref="StimmeGezeigt"/> …). Was früher eine große Tafel mit der Auszählung war,
/// sieht man jetzt im Saal; die Oberfläche trägt nur noch ein schmales <b>Spruchband</b> unten (was gerade gesagt wird, die Knöpfe) und das <b>Ratsbuch</b>
/// rechts (die Striche des Schreibers). Aussehen steht nur im Ratskammer-Thema (<see cref="RatStil"/>, Varianten „RatSpruchband“, „RatsbuchTafel“ …).
///
/// Ablauf je Punkt (Dauer bei Tempo 1):
/// <list type="number">
/// <item><b>Aufruf</b> (2 s): Der Bürgermeister ruft den Punkt auf – Titel und worum es geht.</item>
/// <item><b>Rede</b> (3 s): der Antragsteller mit seiner Begründung; bei Wahlen, wer antritt.</item>
/// <item><b>Eigene Stimme</b>: Darf der Spieler an seinem Sitz noch stimmen (<see cref="Sitzungsstimme.Frei"/>), wartet die Sitzung: Ja, Nein, Enthaltung bzw.
/// ein Kandidat; vorausgewählt ist die Stimme, die er vorab gesetzt hat. „Weiter“ gibt sie ab.</item>
/// <item><b>Hände</b> (2,2 s): „Wer ist dafür?“ – alle Ja-Stimmen heben <i>gleichzeitig</i> die Hand; bei einer Wahl zeigen alle ihren Kandidaten.</item>
/// <item><b>Dagegen</b> (2,2 s, nur Anträge): die Neinstimmen schütteln den Kopf, Enthaltungen bleiben still.</item>
/// <item><b>Zählung</b>: Der Stadtschreiber zählt die Hände einzeln; über jeder Hand erscheint eine Zahl, im Ratsbuch wachsen die Striche (<see cref="Gezaehlt"/>).</item>
/// <item><b>Ergebnis</b> (3,5 s): <see cref="Simulation.RatssitzungWeiter"/> hält den Punkt ab; Zählung, Stichentscheid des Bürgermeisters bzw. Los, Beschluss oder Gewählter.</item>
/// </list>
/// „Weiter“ springt zum nächsten Schritt, „Schneller“ schaltet das Tempo 1×, 2×, 4× durch (Vorgabe: Einstellung „Tempo der Sitzung“, <see cref="Fenster.SitzungsTempo"/>),
/// „Überspringen“ hält alle übrigen Punkte ohne Bild ab (<see cref="Simulation.RatssitzungAbschliessen"/>), „Gründe“ listet im Ratsbuch, warum jeder so stimmte.
///
/// Wiederholung (<see cref="WiederholungZeigen"/>): dieselbe Oberfläche zeigt die letzte Sitzung aus ihren abgehaltenen Punkten, ohne Stimmrecht und ohne den Kern zu fragen.
/// Seit der Stimmrunde (Ratssitzung PR 8) ist das auch der Weg am Jahresende: Der Kern hat schon abgestimmt, die Szene ist eine Wiedergabe.
/// </summary>
public partial class RatssitzungAnzeige : Control
{
    private enum Schritt { Aufruf, Rede, Spieler, Haende, Dagegen, Zaehlung, Ergebnis }

    /// <summary>Sekunden (bei Tempo 1) zwischen zwei gezählten Händen.</summary>
    private const double ZaehlTakt = 0.45;

    private double Dauer(Schritt schritt) => schritt switch
    {
        Schritt.Aufruf => 2.0,
        Schritt.Rede => 3.0,
        Schritt.Haende => 2.2,
        Schritt.Dagegen => 2.2,
        Schritt.Zaehlung => _zaehlfolge.Count * ZaehlTakt + 1.2,
        Schritt.Ergebnis => 3.5,
        _ => double.PositiveInfinity, // der Spieler stimmt: warten
    };

    private Simulation? _simulation;
    private Konto? _spieler;
    /// <summary>Wiederholung der letzten Sitzung: die Punkte mit Ergebnis (null: die laufende Sitzung).</summary>
    private List<Sitzungspunkt>? _wiederholung;
    private int _wiederholungNummer;
    /// <summary>Die Wiedergabe zeigt die eben abgehaltene Sitzung (Kopfzeile ohne „Wiederholung“).</summary>
    private bool _gerade;
    private int _jahr;
    private int _anzahl;

    private Sitzungspunkt? _punkt;
    private Schritt _schritt;
    private double _zeit;
    private int _tempo = 1;

    /// <summary>Die Stimmen des Punktes, die in der Hände-Runde (dafür bzw. ein Kandidat), der Dagegen-Runde (Nein, Enthaltung) gezeigt werden.</summary>
    private List<Sitzungsstimme> _dafuer = new(), _dagegen = new(), _still = new();
    /// <summary>Die Hände in der Reihenfolge, in der der Schreiber sie zählt (zuerst dafür, dann dagegen), und wie viele schon gezählt sind.</summary>
    private List<Sitzungsstimme> _zaehlfolge = new();
    private int _gezaehlt;
    /// <summary>Das Ratsbuch: je Zeile der Name, die Zahl der Striche und das Label dazu.</summary>
    private readonly List<(string Name, Label Striche)> _buchZeilen = new();
    private int[] _buchZahlen = Array.Empty<int>();

    private Label _kopf = null!, _titel = null!, _text = null!, _zeile = null!, _ergebnis = null!, _hinweis = null!, _wahlFrage = null!;
    private HFlowContainer _wahl = null!;
    private HBoxContainer _wahlKnoepfe = null!;
    private Button _weiter = null!, _schneller = null!, _ueberspringen = null!, _gruendeKnopf = null!;
    private PanelContainer _buch = null!;
    private GridContainer _buchGitter = null!;
    private VBoxContainer _gruende = null!;

    /// <summary>Läuft eine Sitzung oder Wiederholung auf der Oberfläche?</summary>
    public bool Laeuft => Visible;

    /// <summary>Die Sitzung wartet (Spielmenü, Einstellungen oder Hilfe sind offen) – die Hauptszene setzt es jedes Bild.</summary>
    public bool Angehalten { get; set; }

    /// <summary>Ein neuer Punkt wird aufgerufen (die Stimmen sind noch nicht gezeigt).</summary>
    public event Action<Sitzungspunkt>? PunktGezeigt;
    /// <summary>Der Antragsteller ist fertig, die Abstimmung beginnt.</summary>
    public event Action? RedeBeendet;
    /// <summary>Dieser Sitz ist am Zug (null: keiner) – nur, solange der Spieler an seinem Sitz stimmt.</summary>
    public event Action<Amt?>? AmZug;
    /// <summary>Eine Stimme wird gezeigt (Hände-Runde: alle gleichzeitig; auch erneut, wenn das Los entschieden hat).</summary>
    public event Action<Sitzungsstimme>? StimmeGezeigt;
    /// <summary>Der Schreiber hat diese Hand gezählt: laufende Nummer in der Zeile des Ratsbuchs und der Stand aller Zeilen als Text.</summary>
    public event Action<Sitzungsstimme, int, string>? Gezaehlt;
    /// <summary>Der Punkt ist abgehalten (mit <see cref="Sitzungspunkt.Ergebnis"/>).</summary>
    public event Action<Sitzungspunkt>? ErgebnisGezeigt;
    /// <summary>Die Sitzung (oder Wiederholung) ist zu Ende; true: es war eine Wiederholung.</summary>
    public event Action<bool>? Beendet;
    /// <summary>Ergebnis eines Befehls – für die Meldungen.</summary>
    public event Action<string>? Gemeldet;

    public override void _Ready()
    {
        RatStil.Anwenden(this);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        var rand = new MarginContainer { ThemeTypeVariation = "RatSitzungRand", MouseFilter = MouseFilterEnum.Ignore };
        rand.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(rand);
        var gesamt = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        rand.AddChild(gesamt);

        // Oben rechts der Mitte: das Ratsbuch (erst ab der Zählung sichtbar)
        var oben = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        gesamt.AddChild(oben);
        oben.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        _buch = new PanelContainer { ThemeTypeVariation = "RatsbuchTafel", Visible = false, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Stop };
        oben.AddChild(_buch);
        var buchSpalte = RatStil.Container<VBoxContainer>(_buch, "RatSpalte");
        var buchTitel = RatStil.Text(buchSpalte, "Ratsbuch", "RatSpruchbandTitel");
        buchTitel.TooltipText = "Der Stadtschreiber zählt die erhobenen Hände und macht für jede einen Strich.";
        _buchGitter = RatStil.Container<GridContainer>(buchSpalte, "RatRaster");
        _buchGitter.Columns = 1; // Name, darunter die Striche
        _gruende = RatStil.Container<VBoxContainer>(buchSpalte, "RatSpalte");
        _gruende.Visible = false;

        // Unten in der Mitte: das Spruchband
        var unten = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        gesamt.AddChild(unten);
        var band = new PanelContainer { ThemeTypeVariation = "RatSpruchband", MouseFilter = MouseFilterEnum.Stop };
        unten.AddChild(band);
        var spalte = RatStil.Container<VBoxContainer>(band, "RatSpalte");

        _kopf = RatStil.Text(spalte, "", "RatHinweis", umbrechen: true, breite: "spruchband_breite");
        _kopf.TooltipText = "Die Ratssitzung am Jahresende: Der Rat entscheidet über Anträge und Wahlen, die Mehrheit zählt, der Bürgermeister gibt bei Gleichstand den Ausschlag.";
        _titel = RatStil.Text(spalte, "", "RatSpruchbandTitel", umbrechen: true, breite: "spruchband_breite");
        _titel.TooltipText = "Der Punkt der Tagesordnung, über den gerade verhandelt wird.";
        _text = RatStil.Text(spalte, "", "RatSpruchbandText", umbrechen: true, breite: "spruchband_breite");
        _zeile = RatStil.Text(spalte, "", "RatText", umbrechen: true, breite: "spruchband_breite");
        _zeile.TooltipText = "Was gerade gesagt wird: der Aufruf, die Rede, die Abstimmung oder die Zählung.";
        // Die eigene Stimme: eine Zeile mit Frage und Knöpfen (das Spruchband soll den Ratstisch nicht verdecken)
        _wahl = new HFlowContainer { Visible = false };
        spalte.AddChild(_wahl);
        _wahlFrage = RatStil.Text(_wahl, "", "RatGold");
        _wahlFrage.TooltipText = "Eure Stimme: Ja, Nein oder Enthaltung. Eine Enthaltung zählt nicht.";
        _wahlFrage.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _wahlKnoepfe = RatStil.Container<HBoxContainer>(_wahl, "RatZeile");
        _ergebnis = RatStil.Text(spalte, "", "RatText", umbrechen: true, breite: "spruchband_breite");
        _ergebnis.TooltipText = "Das Ergebnis der Abstimmung: Ja gegen Nein; bei Gleichstand entscheidet der Bürgermeister.";
        _hinweis = RatStil.Text(spalte, "", "RatHinweis", umbrechen: true, breite: "spruchband_breite");

        var knoepfe = RatStil.Container<HBoxContainer>(spalte, "RatZeile");
        knoepfe.Alignment = BoxContainer.AlignmentMode.Center;
        _weiter = RatStil.Amtsknopf(knoepfe, "Weiter", WeiterGedrueckt, "Zum nächsten Schritt (Leertaste)");
        _schneller = RatStil.Amtsknopf(knoepfe, "Schneller (1×)", () =>
        {
            _tempo = _tempo >= 4 ? 1 : _tempo * 2;
            TempoZeigen();
        }, "Tempo der Sitzung: 1×, 2×, 4× (die Vorgabe stellt ihr in den Einstellungen ein)");
        _ueberspringen = RatStil.Amtsknopf(knoepfe, "Überspringen", Ueberspringen, "Alle übrigen Punkte ohne Bild abhalten – die Ergebnisse stehen im Protokoll");
        _gruendeKnopf = RatStil.Amtsknopf(knoepfe, "Gründe", () => { }, "Im Ratsbuch auflisten, warum jeder so stimmt");
        _gruendeKnopf.ToggleMode = true;
        _gruendeKnopf.Toggled += an =>
        {
            _gruende.Visible = an;
            if (an) GruendeAufbauen();
        };
        Visible = false;
    }

    /// <summary>Setzt den Text einer Zeile; eine leere Zeile nimmt keinen Platz weg (das Spruchband soll den Ratstisch nicht verdecken).</summary>
    private static void Setzen(Label zeile, string text)
    {
        zeile.Text = text;
        zeile.Visible = text.Length > 0;
    }

    private void TempoZeigen() => _schneller.Text = $"Schneller ({_tempo}×)";

    public void Verbinden(Simulation simulation, Konto spieler)
    {
        _simulation = simulation;
        _spieler = spieler;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Beginn und Ende
    // ═════════════════════════════════════════════════════════════════════════

    private void Beginnen()
    {
        _tempo = Fenster.SitzungsTempo;
        TempoZeigen();
        _gruendeKnopf.SetPressedNoSignal(false);
        _gruende.Visible = false;
        Show();
    }

    /// <summary>Beginnt die Oberfläche zur laufenden Sitzung des Kerns (<see cref="Simulation.Ratssitzung"/>).</summary>
    public void SitzungZeigen()
    {
        if (_simulation?.Ratssitzung is not { } sitzung) return;
        _wiederholung = null;
        _jahr = sitzung.Jahr;
        _anzahl = sitzung.Anzahl;
        _ueberspringen.Text = "Überspringen";
        _ueberspringen.TooltipText = "Alle übrigen Punkte ohne Bild abhalten – die Ergebnisse stehen im Protokoll";
        Beginnen();
        PunktBeginnen(sitzung.AktuellerPunkt);
    }

    /// <summary>Zeigt eine abgehaltene Sitzung noch einmal (Knopf „Letzte Sitzung“ im Rathaus).</summary>
    /// <param name="gerade">Die eben am Jahresende abgehaltene Sitzung (Szene als Wiedergabe, PR 8) statt „Letzte Ratssitzung ansehen“.</param>
    public void WiederholungZeigen(Ratssitzung sitzung, bool gerade = false)
    {
        if (sitzung.Abgehalten.Count == 0) return;
        _wiederholung = sitzung.Abgehalten.ToList();
        _wiederholungNummer = 0;
        _jahr = sitzung.Jahr;
        _anzahl = _wiederholung.Count;
        _gerade = gerade;
        _ueberspringen.Text = gerade ? "Überspringen" : "Schließen";
        _ueberspringen.TooltipText = gerade ? "Die übrigen Punkte nicht mehr zeigen – das Ergebnis steht schon fest" : "Die Wiederholung beenden";
        Beginnen();
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
        if (_schritt == Schritt.Zaehlung)
            while (_gezaehlt < _zaehlfolge.Count && _zeit >= (_gezaehlt + 1) * ZaehlTakt) EineZaehlen();
        if (_zeit >= Dauer(_schritt)) Weiter();
    }

    /// <summary>Knopf „Weiter“ und Leertaste: den Schritt sofort beenden (am eigenen Sitz: die gewählte Stimme abgeben).</summary>
    public void WeiterGedrueckt()
    {
        if (Visible && _punkt is not null && !Angehalten) Weiter();
    }

    private static bool IstWahl(Sitzungspunkt punkt) => punkt.Thema is Sitzungsthema.Wahl or Sitzungsthema.Domprediger;

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
        _schritt = Schritt.Aufruf;
        _zeit = 0;
        _kopf.Text = _wiederholung is null
            ? $"Ratssitzung {_jahr} · Punkt {punkt.Nummer} von {_anzahl}"
            : _gerade ? $"Ratssitzung {_jahr} · Punkt {punkt.Nummer} von {_anzahl}"
            : $"Letzte Ratssitzung {_jahr} (Wiederholung) · Punkt {punkt.Nummer} von {_anzahl}";
        _titel.Text = punkt.Titel;
        _text.Text = punkt.Text;
        Setzen(_zeile, "Der Bürgermeister ruft den Punkt auf.");
        Setzen(_ergebnis, "");
        Setzen(_hinweis, "");
        _wahl.Visible = false;
        _buch.Visible = false;
        _gruendeKnopf.Disabled = true;
        _gruende.Visible = false;
        _gruendeKnopf.SetPressedNoSignal(false);
        StimmenSortieren(punkt);
        PunktGezeigt?.Invoke(punkt);
    }

    /// <summary>Teilt die Stimmen des Punktes auf die Runden: Hände (dafür, Kandidat), Dagegen, Enthaltung (still).</summary>
    private void StimmenSortieren(Sitzungspunkt punkt)
    {
        var wahl = IstWahl(punkt);
        _dafuer = punkt.Stimmen.Where(s => wahl ? s.Kandidat is not null : s.Ja == true).ToList();
        _dagegen = wahl ? new List<Sitzungsstimme>() : punkt.Stimmen.Where(s => s.Ja == false).ToList();
        _still = punkt.Stimmen.Where(s => s.Kandidat is null && s.Ja is null).ToList();
    }

    private void Weiter()
    {
        if (_punkt is not { } punkt) return;
        _zeit = 0;
        switch (_schritt)
        {
            case Schritt.Aufruf:
                _schritt = Schritt.Rede;
                Setzen(_zeile, punkt.Antragsteller is { } a ? $"{a.Inhaber}: „{punkt.Begruendung}“" : punkt.Begruendung);
                break;
            case Schritt.Rede:
                RedeBeendet?.Invoke();
                if (EigeneStimme(punkt) is { } eigene)
                {
                    _schritt = Schritt.Spieler;
                    AmZug?.Invoke(eigene.Sitz);
                    EigeneStimmeAufbauen();
                }
                else HaendeBeginnen();
                break;
            case Schritt.Spieler:
                _wahl.Visible = false;
                AmZug?.Invoke(null);
                HaendeBeginnen();
                break;
            case Schritt.Haende:
                if (_dagegen.Count > 0 || _still.Count > 0) DagegenBeginnen();
                else ZaehlungBeginnen();
                break;
            case Schritt.Dagegen:
                ZaehlungBeginnen();
                break;
            case Schritt.Zaehlung:
                ZaehlenBis(int.MaxValue);
                Abhalten();
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

    /// <summary>Die Stimme des Spielers an seinem Sitz, wenn er sie noch setzen darf (nicht in der Wiederholung).</summary>
    private Sitzungsstimme? EigeneStimme(Sitzungspunkt punkt)
    {
        if (_wiederholung is not null || _simulation?.Ratssitzung is not { } sitzung || _spieler is null) return null;
        return punkt.Stimmen.FirstOrDefault(s => s.Frei && s.Haus == _spieler) is { } stimme && sitzung.StimmeVerboten(punkt, _spieler) is null ? stimme : null;
    }

    private void HaendeBeginnen()
    {
        var punkt = _punkt!;
        StimmenSortieren(punkt); // der Spieler kann seine Stimme gerade geändert haben
        _schritt = Schritt.Haende;
        _gruendeKnopf.Disabled = false;
        Setzen(_zeile, IstWahl(punkt) ? "Wer wählt wen? Die Hände zeigen, für wen." : "Wer ist dafür? Die Hände gehen hoch.");
        foreach (var stimme in _dafuer) StimmeGezeigt?.Invoke(stimme); // alle zugleich
    }

    private void DagegenBeginnen()
    {
        _schritt = Schritt.Dagegen;
        Setzen(_zeile, "Wer ist dagegen? Enthaltungen bleiben still.");
        foreach (var stimme in _dagegen) StimmeGezeigt?.Invoke(stimme);
        foreach (var stimme in _still) StimmeGezeigt?.Invoke(stimme);
    }

    /// <summary>Der Schreiber beginnt zu zählen: Erst dafür, dann dagegen, jede Hand einzeln; das Ratsbuch öffnet sich.</summary>
    private void ZaehlungBeginnen()
    {
        var punkt = _punkt!;
        _schritt = Schritt.Zaehlung;
        _zaehlfolge = _dafuer.Concat(_dagegen).ToList();
        _gezaehlt = 0;
        BuchAufbauen(punkt);
        Setzen(_zeile, "Der Stadtschreiber zählt die Hände.");
        _buch.Visible = true;
        if (_gruendeKnopf.ButtonPressed) GruendeAufbauen();
    }

    /// <summary>Die Zeilen des Ratsbuchs: bei Anträgen „Dafür“ und „Dagegen“, bei Wahlen je Kandidat eine.</summary>
    private void BuchAufbauen(Sitzungspunkt punkt)
    {
        UiHilfe.KinderEntfernen(_buchGitter);
        _buchZeilen.Clear();
        var namen = IstWahl(punkt) ? punkt.Kandidaten.Select(k => k.Name + (k.IstAmtsinhaber ? " (bisher)" : "")).ToList() : new List<string> { "Dafür", "Dagegen" };
        _buchZahlen = new int[namen.Count];
        foreach (var name in namen)
        {
            var etikett = RatStil.Text(_buchGitter, name, "RatsbuchName", umbrechen: true, breite: "ratsbuch_breite");
            var striche = RatStil.Text(_buchGitter, "", "RatsbuchStrich");
            _buchZeilen.Add((name, striche));
        }
    }

    /// <summary>Zählt eine Hand: ein Strich im Ratsbuch und die Zahl über der Hand im Saal.</summary>
    private void EineZaehlen()
    {
        var punkt = _punkt!;
        var stimme = _zaehlfolge[_gezaehlt++];
        var zeile = IstWahl(punkt) ? punkt.Kandidaten.ToList().FindIndex(k => k.Name == stimme.Kandidat) : stimme.Ja == true ? 0 : 1;
        if (zeile < 0 || zeile >= _buchZahlen.Length) return;
        _buchZahlen[zeile]++;
        _buchZeilen[zeile].Striche.Text = Striche(_buchZahlen[zeile]);
        Gezaehlt?.Invoke(stimme, _buchZahlen[zeile], string.Join(" · ", _buchZeilen.Select((z, i) => $"{Kurz(z.Name)} {_buchZahlen[i]}")));
    }

    private void ZaehlenBis(int anzahl)
    {
        while (_gezaehlt < _zaehlfolge.Count && _gezaehlt < anzahl) EineZaehlen();
    }

    /// <summary>Striche in Fünfergruppen: „||||/ ||“.</summary>
    private static string Striche(int zahl)
    {
        var gruppen = Enumerable.Repeat("||||/", zahl / 5).ToList();
        if (zahl % 5 > 0) gruppen.Add(new string('|', zahl % 5));
        return string.Join(" ", gruppen);
    }

    /// <summary>Der Name einer Zeile kurz für die Zahl im Saal („Haus Hammerschmid (bisher)“ → „Hammerschmid“).</summary>
    private static string Kurz(string name)
    {
        var ohne = name.Replace(" (bisher)", "");
        return ohne.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? ohne;
    }

    /// <summary>Alle Hände sind gezählt: den Punkt im Kern abhalten (das Los entscheidet erst jetzt) und das Ergebnis zeigen.</summary>
    private void Abhalten()
    {
        AmZug?.Invoke(null);
        var punkt = _punkt!;
        if (_wiederholung is null && _simulation?.Ratssitzung is not null)
        {
            if (_simulation.RatssitzungWeiter() is { } abgehalten) punkt = abgehalten;
            _punkt = punkt;
            foreach (var stimme in punkt.Stimmen)
                if (stimme.Los) StimmeGezeigt?.Invoke(stimme); // gezogener Kandidat
        }
        _schritt = Schritt.Ergebnis;
        ErgebnisZeigen(punkt);
        ErgebnisGezeigt?.Invoke(punkt);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Anzeige
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Im Ratsbuch: je Sitz eine Zeile mit der Stimme und ihrer Begründung (erst, wenn die Hände gezeigt sind).</summary>
    private void GruendeAufbauen()
    {
        UiHilfe.KinderEntfernen(_gruende);
        if (_punkt is not { } punkt || _schritt < Schritt.Haende) return;
        foreach (var stimme in punkt.Stimmen)
        {
            var text = stimme.Kandidat is { } k ? k + (stimme.Los ? " (Los)" : "") : stimme.Ja switch { true => "Ja", false => "Nein", _ => "Enthaltung" };
            var zeile = RatStil.Text(_gruende, $"{stimme.Sitz.Name} {stimme.Name}: {text} – {stimme.Begruendung}", stimme.Haus is not null && stimme.Haus == _spieler ? "RatGold" : "RatHinweis",
                umbrechen: true, breite: "ratsbuch_breite");
            zeile.TooltipText = stimme.Begruendung;
        }
    }

    /// <summary>Am eigenen Sitz: die Knöpfe zum Abstimmen, die gesetzte Stimme gedrückt.</summary>
    private void EigeneStimmeAufbauen()
    {
        var punkt = _punkt!;
        if (EigeneStimme(punkt) is not { } stimme) return;
        UiHilfe.KinderEntfernen(_wahlKnoepfe);
        _wahl.Visible = true;
        _wahlFrage.Text = $"Eure Stimme als {stimme.Sitz.Name}:";
        if (IstWahl(punkt)) // der Domprediger wird gewählt wie ein Amt (ohne Amt im Rat)
        {
            foreach (var kandidat in punkt.Kandidaten)
            {
                var knopf = WahlKnopf(kandidat.Name + (kandidat.IstAmtsinhaber ? " (bisher)" : ""), stimme.Kandidat == kandidat.Name,
                    () => Stimmen(punkt.Amt is { } amt ? new WahlStimme(amt.Name, kandidat.Name) : new DomStimme(kandidat.Name)));
                if (punkt.Thema == Sitzungsthema.Domprediger && _simulation?.Wirtschaft is { } w && RatAnzeige.PunkteImSchnitt(w, kandidat) is { } p)
                    knopf.TooltipText = $"Im Schnitt {Zahlenformat.Kommazahl(p)} Punkte bei den Ratsherren (Ansehen, Meinung, Rhetorik" + (kandidat.IstAmtsinhaber ? ", Amtsbonus" : "") + ").";
            }
        }
        else
        {
            foreach (var (text, ja) in new[] { ("Ja", (bool?)true), ("Nein", false), ("Enthaltung", null) })
                WahlKnopf(text, stimme.Ja == ja, () => Stimmen(AntragsStimme(punkt, ja)));
        }
        Setzen(_hinweis, "„Weiter“ gibt die Stimme ab (vorausgewählt: eure Stimme von vorab).");
    }

    private Button WahlKnopf(string text, bool gedrueckt, Action aktion)
    {
        var knopf = RatStil.Amtsknopf(_wahlKnoepfe, text, aktion);
        knopf.ToggleMode = true;
        knopf.SetPressedNoSignal(gedrueckt);
        return knopf;
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
            EigeneStimmeAufbauen();
        });
    }

    private void ErgebnisZeigen(Sitzungspunkt punkt)
    {
        _wahl.Visible = false;
        Setzen(_zeile, "");
        Setzen(_hinweis, "");
        if (punkt.Ergebnis is not { } e)
        {
            Setzen(_ergebnis, "");
            return;
        }
        if (IstWahl(punkt))
        {
            var zaehlung = string.Join(", ", e.Zaehlung.Select(z => $"{z.Kandidat} {z.Stimmen}"));
            Setzen(_ergebnis, (e.Gewaehlt is { } g ? $"Gewählt: {g}" : "Niemand gewählt") + (e.Stichentscheid ? " – Gleichstand, das Los entschied" : "")
                + (zaehlung.Length > 0 ? $"\nStimmen: {zaehlung}" : ""));
            _ergebnis.ThemeTypeVariation = "RatGold";
        }
        else
        {
            var zaehlung = $"{e.Ja} Ja, {e.Nein} Nein, {e.Enthaltung} Enthaltung";
            var stich = e.Stichentscheid && e.Buergermeister is { } bm ? $" – Gleichstand, der Bürgermeister stimmt {(bm ? "dafür" : "dagegen")}" : "";
            Setzen(_ergebnis, $"{(e.Angenommen ? "Angenommen" : "Abgelehnt")}: {zaehlung}{stich}");
            _ergebnis.ThemeTypeVariation = e.Angenommen ? "RatGut" : "RatSchlecht";
        }
        if (e.Protokoll.Count > 0) Setzen(_hinweis, string.Join("\n", e.Protokoll));
        if (_gruendeKnopf.ButtonPressed) GruendeAufbauen(); // das Los kann eine Stimme geändert haben
    }
}
