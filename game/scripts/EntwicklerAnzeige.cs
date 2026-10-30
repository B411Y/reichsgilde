using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Entwickler;
using Reichsgilde.Core.Gesundheit;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das <b>Entwicklerfenster</b> („Trainer“, Taste <b>F12</b> und Knopf „Entwickler“ in der Betriebsleiste – beides nur mit dem Haken
/// „Entwickler-Optionen“ im Hauptmenü, <see cref="Entwickler.Aktiv"/>): in der Bildmitte sechs Reiter. <b>Haus und Zeit</b> (Gulden, Aktionspunkte, Stand, Ansehen,
/// Meinung, Vorspulen), <b>Familie und Ereignisse</b> (Geburt, Heirat, Krankheit, Tod, Charakterzüge, Diebstahl, Feuer), <b>Wirtschaft</b> (Ware, Zutaten),
/// <b>Wetter und Darstellung</b>, <b>Messwerte</b> (EntwicklerAnzeige.Darstellung.cs) und <b>Klänge</b> (alle Klänge zum Anhören mit Bus und Pegel, EntwicklerAnzeige.Klaenge.cs).
///
/// Alles, was die Partie verändert, geht als <see cref="EntwicklerBefehl"/> über <see cref="UiHilfe.Befehlen"/> an den Kern (der prüft die Sperre
/// <see cref="Simulation.EntwicklerErlaubt"/>, setzt das Kennzeichen „mit Entwicklerhilfe“ und liefert die Meldung, die unten im Fenster steht).
/// Vorspulen ist kein Befehl (<see cref="Simulation.Vorspulen"/>). Wetter, Jahreszeit, Dunst, Unschärfe und Hilfsanzeigen sind reine Darstellung und
/// wirken nur bis zum Ende dieser Sitzung (sie werden nicht in die Einstellungen geschrieben). Die Auswahllisten füllt das Fenster aus dem Zustand der Partie.
/// </summary>
public sealed partial class EntwicklerAnzeige : CenterContainer
{
    private const int Breite = 860;
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.75f, 0.75f, 0.75f);
    private static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);

    private Simulation _sim = null!;
    private Konto _spieler = null!;
    private Stadtansicht _stadt = null!;
    private Action<double> _tempo = null!;

    private TabContainer _reiter = null!;
    private Label _ergebnis = null!;
    private Label _stand = null!;
    private double _seitAuffrischen = 1;

    private Waehler _haus = null!, _meinungVon = null!, _standWahl = null!;
    private Waehler _person = null!, _krankheit = null!, _zug = null!, _kandidat = null!, _betriebEreignis = null!;
    private Waehler _betriebWare = null!, _ware = null!;
    private SpinBox _gulden = null!, _ap = null!, _ansehen = null!, _meinung = null!, _menge = null!;
    private Button? _todKnopf;

    /// <summary>Die Tafel selbst (für die Prüfung, ob sie offen ist und die Maus darüber liegt).</summary>
    public PanelContainer Tafel { get; private set; } = null!;

    /// <summary>Ist das Fenster zu sehen? (Die Tafel kann nach dem Ziehen außerhalb dieses Containers hängen und allein versteckt werden.)</summary>
    public bool Offen => Visible && Tafel.Visible;

    /// <summary>
    /// Verbindet das Fenster mit der Partie. <paramref name="tempo"/> stellt die Spielgeschwindigkeit (wie die Zifferntasten).
    /// Vor dem Einhängen in den Baum aufrufen (Hauptszene.Entwickler.cs): <see cref="_Ready"/> baut die Reiter schon während <c>AddChild</c>.
    /// </summary>
    public void Verbinden(Simulation simulation, Konto spieler, Stadtansicht stadt, Action<double> tempo)
    {
        _sim = simulation;
        _spieler = spieler;
        _stadt = stadt;
        _tempo = tempo;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var (tafel, spalte) = Hauptmenue.Tafel(Breite);
        Tafel = tafel;
        AddChild(tafel);

        var kopf = new HBoxContainer();
        spalte.AddChild(kopf);
        var titel = Hauptmenue.Text(kopf, "Entwickler", 24, Gold);
        titel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titel.TooltipText = "Werte und Ereignisse von Hand setzen, die Zeit vorspulen, Wetter und Hilfsanzeigen schalten, Messwerte lesen. Nur mit den Entwickler-Optionen; "
                            + "nach dem ersten wirksamen Eingriff trägt die Partie für immer das Kennzeichen „mit Entwicklerhilfe“.";
        var hausZeile = new HBoxContainer();
        hausZeile.AddThemeConstantOverride("separation", 6);
        kopf.AddChild(hausZeile);
        Hauptmenue.Text(hausZeile, "Haus:", 14, Grau);
        _haus = new Waehler(hausZeile, 170, "Das Haus, für das die Eingriffe dieses Reiters „Haus und Zeit“ und das Hausfeuer gelten.");
        kopf.AddChild(new Control { CustomMinimumSize = new Vector2(52, 0) }); // Platz für Festhalten und ✕ des Fensterrahmens

        _stand = Hauptmenue.Text(spalte, "", 13, Grau, umbruchBreite: Breite - 20);
        _reiter = new TabContainer { FocusMode = FocusModeEnum.None, ClipTabs = false, CustomMinimumSize = new Vector2(Breite - 40, 470) };
        spalte.AddChild(_reiter);
        HausReiter();
        FamilieReiter();
        WirtschaftReiter();
        DarstellungReiter();
        MesswerteReiter();
        KlaengeReiter();

        spalte.AddChild(new HSeparator());
        _ergebnis = Hauptmenue.Text(spalte, "Noch nichts getan.", 14, Grau, umbruchBreite: Breite - 20);
        _ergebnis.CustomMinimumSize = new Vector2(Breite - 20, 40);

        Fensterrahmen.Anmelden(tafel, "Entwickler", Schliessen, festhaltbar: true);
        Hide();
    }

    /// <summary>Öffnet das Fenster in der Mitte (auf einem Reiter, wenn angegeben).</summary>
    public void Oeffnen(int reiter = -1)
    {
        Show();
        Tafel.Show(); // nach dem Ziehen hängt die Tafel in der Fensterebene, nicht mehr in diesem Container
        if (reiter >= 0 && reiter < _reiter.GetTabCount()) _reiter.CurrentTab = reiter;
        Auffrischen(true);
        DarstellungAuffrischen(); // die Schalter gleich auf dem Stand der Stadt, nicht erst nach einer halben Sekunde
    }

    public void Schliessen()
    {
        Hide();
        Tafel.Hide();
    }

    public void Umschalten()
    {
        if (Offen) Schliessen();
        else Oeffnen();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Bausteine
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Ein Reiter: eine Spalte mit dem Titel als Reitername.</summary>
    private VBoxContainer NeuerReiter(string name)
    {
        var rolle = new ScrollContainer { Name = name, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _reiter.AddChild(rolle);
        var spalte = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        spalte.AddThemeConstantOverride("separation", 10);
        rolle.AddChild(spalte);
        return spalte;
    }

    /// <summary>Eine Zeile mit Beschriftung links; die Bedienelemente kommen in die zurückgegebene Reihe.</summary>
    private static HBoxContainer Zeile(Container eltern, string beschriftung, string? tipp = null)
    {
        var reihe = new HBoxContainer();
        reihe.AddThemeConstantOverride("separation", 8);
        eltern.AddChild(reihe);
        var name = Hauptmenue.Text(reihe, beschriftung, 15, Gold);
        name.CustomMinimumSize = new Vector2(150, 0);
        name.VerticalAlignment = VerticalAlignment.Center;
        if (tipp is not null) name.TooltipText = tipp;
        return reihe;
    }

    private static Button Knopf(Container eltern, string text, Action aktion, string? tipp = null)
    {
        var knopf = new Button { Text = text, FocusMode = FocusModeEnum.None, TooltipText = tipp ?? "" };
        knopf.Pressed += aktion;
        eltern.AddChild(knopf);
        return knopf;
    }

    private static SpinBox Zahl(Container eltern, double min, double max, double schritt, double wert, int breite = 120, string? tipp = null)
    {
        var feld = new SpinBox { MinValue = min, MaxValue = max, Step = schritt, Value = wert, CustomMinimumSize = new Vector2(breite, 0), SelectAllOnFocus = true, TooltipText = tipp ?? "" };
        eltern.AddChild(feld);
        return feld;
    }

    /// <summary>Der eingetippte Wert, auch wenn Enter nicht gedrückt wurde.</summary>
    private static double Wert(SpinBox feld)
    {
        feld.Apply();
        return feld.Value;
    }

    private void Zeigen(Befehlsergebnis e)
    {
        _ergebnis.Text = (e.Erfolg ? "" : "✗ ") + e.Meldung;
        _ergebnis.AddThemeColorOverride("font_color", e.Erfolg ? Gruen : Rot);
        Auffrischen(true);
    }

    private void Melden(string text, bool gut = true)
    {
        _ergebnis.Text = text;
        _ergebnis.AddThemeColorOverride("font_color", gut ? Gruen : Rot);
    }

    private void Befehlen(Befehl befehl) => UiHilfe.Befehlen(_sim, _spieler, befehl, this, Zeigen);

    /// <summary>Der gewählte Hausname für Befehle (das eigene Haus ebenso mit Namen).</summary>
    private string? HausName => _haus.Schluessel;

    // ═════════════════════════════════════════════════════════════════════════
    //  Reiter: Haus und Zeit
    // ═════════════════════════════════════════════════════════════════════════

    private void HausReiter()
    {
        var s = NeuerReiter("Haus und Zeit");
        var z = Zeile(s, "Gulden", "Gulden geben oder nehmen, ohne Buchung in der Jahresrechnung.");
        _gulden = Zahl(z, 0, 100_000_000, 100, 1000, tipp: "Betrag in Gulden (eintippen oder mit den Pfeilen).");
        Knopf(z, "Geben", () => Befehlen(new EntwicklerGulden(Wert(_gulden), HausName)));
        Knopf(z, "Nehmen", () => Befehlen(new EntwicklerGulden(-Wert(_gulden), HausName)));
        foreach (var betrag in new[] { 1_000, 10_000, 100_000 })
            Knopf(z, $"+{Zahlenformat.Ganz(betrag)}", () => Befehlen(new EntwicklerGulden(betrag, HausName)));

        z = Zeile(s, "Aktionspunkte", "Den Vorrat setzen; „Auffüllen“ bis zum Höchstwert. Kostet selbst keine Punkte.");
        _ap = Zahl(z, 0, 999, 1, 10, 90);
        Knopf(z, "Setzen", () => Befehlen(new EntwicklerAktionspunkte((int)Wert(_ap), HausName)));
        Knopf(z, "Auffüllen", () => Befehlen(new EntwicklerAktionspunkte(null, HausName)));

        z = Zeile(s, "Stand", "Den Stand (Titel) setzen, ohne Vermögen, Gebühr und Wohnhaus.");
        _standWahl = new Waehler(z, 240);
        Knopf(z, "Setzen", () => { if (_standWahl.Zahl is int st) Befehlen(new EntwicklerStand(st, HausName)); });

        z = Zeile(s, "Ansehen", "Das Ansehen jetzt auf diesen Wert setzen; es wandert danach mit Vermögen, Stand und Schande weiter.");
        _ansehen = Zahl(z, -1000, 1000, 1, 50, 100);
        Knopf(z, "Setzen", () => Befehlen(new EntwicklerAnsehen(Wert(_ansehen), HausName)));

        z = Zeile(s, "Meinung über das Haus", "Was diese Person über das Oberhaupt des Hauses denkt (−100 bis +100); sie verblasst am Jahresende wie jede Tat.");
        _meinungVon = new Waehler(z, 300);
        _meinung = Zahl(z, -100, 100, 1, 50, 90);
        Knopf(z, "Setzen", () => { if (_meinungVon.Schluessel is { } von) Befehlen(new EntwicklerMeinung(von, Wert(_meinung), HausName)); });

        s.AddChild(new HSeparator());
        ZeitAbschnitt(s);
    }

    private SpinBox _spulZahl = null!, _uhrStunde = null!, _uhrMinute = null!;
    private OptionButton _spulEinheit = null!;

    private void ZeitAbschnitt(VBoxContainer s)
    {
        Hauptmenue.Text(s, "Zeit", 18, Gold);
        var z = Zeile(s, "Tempo", "Spielgeschwindigkeit wie die Zifferntasten, dazu 64×: Der Taktgeber rechnet dabei etwa 80 Spielminuten je Sekunde, höchstens 240 je Bild, das hält er aus.");
        foreach (var t in new[] { 1.0, 4.0, 8.0, 16.0, 32.0, 64.0 })
            Knopf(z, $"{t}×", () => { _tempo(t); Melden($"Tempo {t}×."); });
        Knopf(z, "Pause", () => { _sim.Takt.PauseUmschalten(); Melden(_sim.Takt.IstPausiert ? "Pause." : "Weiter."); });

        z = Zeile(s, "Vorspulen um", "Rechnet die Zeit sofort durch, mit allen Ereignissen (Jahresabrechnung, Ratssitzung …). Höchstens 20 Spieljahre. Das Fenster hängt dabei kurz.");
        _spulZahl = Zahl(z, 1, 100_000, 1, 30, 100);
        _spulEinheit = new OptionButton { FocusMode = FocusModeEnum.None };
        foreach (var e in new[] { "Minuten", "Stunden", "Jahre" }) _spulEinheit.AddItem(e);
        z.AddChild(_spulEinheit);
        Knopf(z, "Vorspulen", () =>
        {
            var malMinuten = _spulEinheit.Selected switch { 1 => 60L, 2 => Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag, _ => 1L };
            Zeigen(_sim.Vorspulen((long)Wert(_spulZahl) * malMinuten));
        });

        z = Zeile(s, "Bis zur Uhrzeit", "Bis zu dieser Uhrzeit (4 bis 22 Uhr); ist sie heute vorbei, bis zu ihr im nächsten Jahr.");
        _uhrStunde = Zahl(z, 4, 22, 1, 12, 70);
        Hauptmenue.Text(z, ":", 16);
        _uhrMinute = Zahl(z, 0, 59, 5, 0, 70);
        Knopf(z, "Vorspulen", () => Zeigen(_sim.VorspulenBisUhrzeit((int)Wert(_uhrStunde), (int)Wert(_uhrMinute))));

        z = Zeile(s, "Jahreswechsel", "Jahresende: bis 22:59, der nächste Tick bringt die Jahresabrechnung. Ratssitzung: über den Jahreswechsel, die Sitzung beginnt (Wahlen und Anträge gibt es nur dort).");
        Knopf(z, "Bis Jahresende", () => Zeigen(_sim.VorspulenBisJahresende()));
        Knopf(z, "Bis Ratssitzung", () => Zeigen(_sim.VorspulenBisRatssitzung()));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Reiter: Familie und Ereignisse
    // ═════════════════════════════════════════════════════════════════════════

    private void FamilieReiter()
    {
        var s = NeuerReiter("Familie und Ereignisse");
        var z = Zeile(s, "Person", "Die Person für Krankheit, Tod und Charakterzug: alle lebenden Angehörigen der Häuser und die Heiratskandidaten der Brautschau.");
        _person = new Waehler(z, 420);

        z = Zeile(s, "Krankheit", "Die gewählte Person erkrankt (eine andere Krankheit weicht der neuen); „geheilt“ macht sie gesund.");
        _krankheit = new Waehler(z, 220);
        Knopf(z, "Anwenden", () => { if (_person.Zahl is int p) Befehlen(new EntwicklerKrankheit(p, _krankheit.Schluessel is { Length: > 0 } k ? k : null)); });

        z = Zeile(s, "Charakterzug", "Gibt der gewählten Person den Zug oder nimmt ihn weg; beim Geben fällt sein Gegenteil weg.");
        _zug = new Waehler(z, 220);
        Knopf(z, "Geben", () => { if (_person.Zahl is int p && _zug.Schluessel is { } k) Befehlen(new EntwicklerZug(p, k)); });
        Knopf(z, "Entfernen", () => { if (_person.Zahl is int p && _zug.Schluessel is { } k) Befehlen(new EntwicklerZug(p, k, false)); });

        z = Zeile(s, "Tod", "Die gewählte Person stirbt sofort, mit Erbfolge (ohne Erben erlischt das Haus). Zweimal klicken.");
        _todKnopf = Knopf(z, "Sterben lassen", TodGedrueckt);

        s.AddChild(new HSeparator());
        z = Zeile(s, "Geburt", "Ein Kind kommt sofort zur Welt, vom Oberhaupt und seinem Ehepartner (ohne Ehepartner vom Oberhaupt allein).");
        Knopf(z, "Sohn", () => Befehlen(new EntwicklerGeburt(Geschlecht.Mann, HausName)));
        Knopf(z, "Tochter", () => Befehlen(new EntwicklerGeburt(Geschlecht.Frau, HausName)));

        z = Zeile(s, "Heirat", "Das Oberhaupt des Hauses heiratet sofort, ohne Zuneigung, Ansehen, Stand und Kosten; nur das Geschlecht muss passen.");
        _kandidat = new Waehler(z, 330);
        Knopf(z, "Heiraten", () => Befehlen(new EntwicklerHeirat(_kandidat.Zahl, HausName)));

        s.AddChild(new HSeparator());
        z = Zeile(s, "Betrieb", "Der Betrieb für Diebstahl und Feuer.");
        _betriebEreignis = new Waehler(z, 420);
        z = Zeile(s, "Ereignis", "Gelingt sofort, wie ein Gaunerstück, aber ohne Täter, Spur und Hehler. Das Hausfeuer trifft das Wohnhaus des gewählten Hauses.");
        Knopf(z, "Diebstahl", () => { if (_betriebEreignis.Schluessel is { } b) Befehlen(new EntwicklerDiebstahl(b)); });
        Knopf(z, "Feuer im Betrieb", () => { if (_betriebEreignis.Schluessel is { } b) Befehlen(new EntwicklerFeuer(b)); });
        Knopf(z, "Hausbrand", () => Befehlen(new EntwicklerHausbrand(HausName)));
    }

    private void TodGedrueckt()
    {
        if (_todKnopf is null || _person.Zahl is not int p) return;
        if (_todKnopf.Text == "Sterben lassen")
        {
            _todKnopf.Text = "Wirklich?";
            GetTree().CreateTimer(3).Timeout += () => { if (IsInstanceValid(_todKnopf)) _todKnopf.Text = "Sterben lassen"; };
            return;
        }
        _todKnopf.Text = "Sterben lassen";
        Befehlen(new EntwicklerTod(p));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Reiter: Wirtschaft
    // ═════════════════════════════════════════════════════════════════════════

    private void WirtschaftReiter()
    {
        var s = NeuerReiter("Wirtschaft");
        var z = Zeile(s, "Betrieb");
        _betriebWare = new Waehler(z, 420);
        z = Zeile(s, "Ware", "Die Ware, die ins Lager gelegt oder aus ihm genommen wird.");
        _ware = new Waehler(z, 240);
        _menge = Zahl(z, 1, EntwicklerHilfeGrenze, 1, 100, 100, "Menge in Stück; höchstens " + Zahlenformat.Ganz(EntwicklerHilfeGrenze) + " einer Ware im Lager.");
        z = Zeile(s, "Lager", "Legt die Ware ins Lager des Betriebs, ohne Kosten und auch über die Lagergrenze, oder nimmt sie heraus (höchstens, was da ist).");
        Knopf(z, "Einlagern", () => { if (_betriebWare.Schluessel is { } b && _ware.Schluessel is { } w) Befehlen(new EntwicklerWare(b, w, (int)Wert(_menge))); });
        Knopf(z, "Entnehmen", () => { if (_betriebWare.Schluessel is { } b && _ware.Schluessel is { } w) Befehlen(new EntwicklerWare(b, w, -(int)Wert(_menge))); });
        z = Zeile(s, "Zutaten", "Füllt alle Zutaten der Waren in Arbeit bis zur Lagergrenze je Ware auf, ohne Kosten (nur Werkstätten mit Zutaten).");
        Knopf(z, "Zutaten auffüllen", () => { if (_betriebWare.Schluessel is { } b) Befehlen(new EntwicklerZutaten(b)); });
    }

    private const double EntwicklerHilfeGrenze = Entwicklerhilfe.WareHoechstens;

    // ═════════════════════════════════════════════════════════════════════════
    //  Auswahllisten aus dem Zustand füllen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Füllt die Listen neu (nur wenn sich ihr Inhalt ändert) und die Stand-Zeile; <paramref name="sofort"/>: auch außerhalb des Takts.</summary>
    private void Auffrischen(bool sofort)
    {
        if (_sim?.Wirtschaft is not { } w) return;
        var aktiv = w.Konten.Where(k => k.IstAktiv).ToList();
        _haus.Fuellen(aktiv.Select(k => (k.Inhaber, k == _spieler ? k.Inhaber + " (ihr)" : k.Inhaber)).ToList(), _spieler.Inhaber);
        _standWahl.Fuellen(w.Daten.Staende.Select((st, i) => (i.ToString(), $"{i} · {st.Name}")).ToList(), w.StandVon(_spieler) is { } aktuell ? w.Daten.Staende.IndexOf(aktuell).ToString() : null);
        var charaktere = w.Meinungen.WichtigePersonen();
        _meinungVon.Fuellen(charaktere.Select(c => (c.Charakter.Kennung, $"{c.Charakter.Name} – {c.Rolle}")).ToList(), null);

        var lebende = w.Familien.IstAktiv
            ? aktiv.Select(k => (Konto: k, Familie: w.Familien.Von(k))).Where(x => x.Familie is not null)
                .SelectMany(x => x.Familie!.Lebende.Select(p => (p.Nummer.ToString(), $"{p.Name} ({x.Konto.Inhaber}{(p == x.Familie.Oberhaupt ? ", Oberhaupt" : "")})"))).ToList()
            : new List<(string, string)>();
        lebende.AddRange(w.Familien.Kandidaten.Select(k => (k.Person.Nummer.ToString(), $"{k.Person.Name} (Brautschau)")));
        _person.Fuellen(lebende, null);
        var krankheiten = new List<(string, string)> { ("", "geheilt (gesund machen)") };
        krankheiten.AddRange(w.Gesundheit.Daten.Krankheiten.Keys.Select(k => (k, k)));
        krankheiten.Add((Erkrankung.PestName, Erkrankung.PestName));
        _krankheit.Fuellen(krankheiten, null);
        _zug.Fuellen(w.Familien.Zugkatalog.Select(z => (z.Kennung, z.Name)).ToList(), null);
        var kandidaten = new List<(string, string)> { ("", "der erste Passende") };
        kandidaten.AddRange(w.Familien.Kandidaten.Select(k => (k.Person.Nummer.ToString(), $"{k.Person.Name} ({k.Person.Geschlecht}, Stand {k.Stand})")));
        _kandidat.Fuellen(kandidaten, null);
        var betriebe = w.Betriebe.OrderBy(b => b.Konto == _spieler ? 0 : 1).Select(b => (b.Name, $"{b.Name} ({b.Konto.Inhaber})")).ToList();
        _betriebEreignis.Fuellen(betriebe, null);
        _betriebWare.Fuellen(betriebe, null);
        _ware.Fuellen(w.Daten.Waren.Keys.OrderBy(x => x, StringComparer.CurrentCulture).Select(x => (x, x)).ToList(), null);

        var ap = w.Daten.Aktionspunkte is { } a ? $" · AP {_spieler.Aktionspunkte.Vorrat}/{a.Hoechstens}" : "";
        _stand.Text = $"{_spieler.Inhaber}: {Zahlenformat.Ganz(_spieler.Vermoegen)} {w.Daten.Allgemein.Waehrung} · {w.StandVon(_spieler).Name}{ap} · Ansehen {Zahlenformat.Ganz(w.Rat.Ansehen(_spieler))}"
                      + $" · {_sim.Zeit.DatumText}, {_sim.Zeit.UhrzeitText}"
                      + (_sim.EntwicklerErlaubt ? "" : " · GESPERRT (Entwickler-Optionen aus)")
                      + (_sim.MitEntwicklerhilfe ? " · Partie trägt das Kennzeichen „mit Entwicklerhilfe“" : "");
    }

    public override void _Process(double delta)
    {
        if (!Offen || _sim is null) return;
        KlaengeAuffrischen(); // der Pegel läuft jedes Bild mit
        _seitAuffrischen += delta;
        if (_seitAuffrischen < 0.5) return;
        _seitAuffrischen = 0;
        Auffrischen(false);
        DarstellungAuffrischen();
    }

    /// <summary>Eine Auswahlliste, die ihre Einträge aus dem Zustand der Partie bekommt und die Wahl behält, solange es den Eintrag noch gibt.</summary>
    private sealed class Waehler
    {
        private readonly OptionButton _knopf;
        private readonly List<string> _schluessel = new();
        private string _inhalt = "\0";

        public Waehler(Container eltern, int breite, string? tipp = null)
        {
            _knopf = new OptionButton { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(breite, 0), TooltipText = tipp ?? "", ClipText = true };
            eltern.AddChild(_knopf);
        }

        /// <summary>Der Schlüssel des gewählten Eintrags (null bei leerer Liste).</summary>
        public string? Schluessel => _knopf.Selected >= 0 && _knopf.Selected < _schluessel.Count ? _schluessel[_knopf.Selected] : null;

        /// <summary>Der gewählte Schlüssel als Zahl (null, wenn leer oder keine Zahl).</summary>
        public int? Zahl => int.TryParse(Schluessel, out var n) ? n : null;

        public void Fuellen(IReadOnlyList<(string Schluessel, string Text)> eintraege, string? vorwahl)
        {
            var inhalt = string.Join("\n", eintraege.Select(e => e.Schluessel + "\t" + e.Text));
            if (inhalt == _inhalt) return;
            var alt = Schluessel ?? vorwahl;
            _inhalt = inhalt;
            _knopf.Clear();
            _schluessel.Clear();
            foreach (var (schluessel, text) in eintraege)
            {
                _knopf.AddItem(text);
                _schluessel.Add(schluessel);
            }
            var wahl = alt is null ? -1 : _schluessel.IndexOf(alt);
            if (wahl < 0 && vorwahl is not null) wahl = _schluessel.IndexOf(vorwahl);
            if (_knopf.ItemCount > 0) _knopf.Select(Math.Max(0, wahl));
        }
    }
}
