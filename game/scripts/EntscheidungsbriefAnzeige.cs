using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Entscheidungen;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Der <b>Entscheidungsbrief</b> (Benachrichtigungen Stufe 3): ein Bogen Pergament wie <see cref="BriefAnzeige"/>, aber mit Frage statt Meldung – Porträt der Person
/// (bzw. des Absenders), Titel auf dem Farbband, zwei bis vier Knöpfe, ein <b>Fristbalken</b> mit Zahl in Spielzeit („Noch 1:20 Std. bis zur Voreinstellung“),
/// unten der Knopf „Später“ und das Wachssiegel mit Feder. Jeder Knopf trägt als Tooltip die „Folgen“ aus dem Kern (<see cref="Entscheidungswesen.Folgen"/>, reiner Text);
/// wo dort steht, dass sich etwas nicht zurücknehmen lässt, ist die Schrift rot.
/// Alle Wahlen gehen als <see cref="EntscheidungTreffen"/> über <see cref="UiHilfe.Befehlen"/>. Der Brief schließt nie selbst: Er verschwindet erst, wenn der Kern
/// die Frage geschlossen hat (die Hauptszene gleicht jedes Bild ab); schlägt der Befehl fehl („✗“, z. B. zu wenig Geld), bleibt er stehen und nennt den Grund.
/// Mehrere offene Fragen bilden einen Stapel („Brief 1 von 3“, die dringendste zuerst). Der Kern wird nur gelesen.
/// </summary>
public sealed partial class EntscheidungsbriefAnzeige : PanelContainer
{
    /// <summary>Farbe des Themas „Entscheidung“ (Farbband, Fristbalken, Siegel).</summary>
    public static readonly Color Farbe = new(0.48f, 0.23f, 0.37f);

    private Simulation? _simulation;
    private Konto? _spieler;
    private readonly List<Entscheidung> _stapel = new();
    private int _nr;
    private VBoxContainer _inhalt = null!;
    private Fensterkleid? _kleid;
    private Label? _fristText;
    private Fristbalken? _balken;
    private Label? _fehlerText;
    private string _fehler = "";
    private bool _wartet;
    private double _alter = 1;
    private string _stand = "";

    /// <summary>Eine Meldung der Oberfläche (abgelehnter Befehl „✗ …“) – die Hauptszene legt sie ins Protokoll.</summary>
    public event Action<string>? Gemeldet;

    /// <summary>Der Befehl wirkte: Die Hauptszene gleicht sofort ab (die Frage ist zu, der nächste Brief kommt nach vorn).</summary>
    public event Action? Beantwortet;

    /// <summary>„Später“ (oder ✕): Die Fragen im Stapel liegen beiseite, ihre Frist läuft weiter.</summary>
    public event Action? Spaeter;

    /// <summary>Die Mahnung führt weiter: „bank“ (Kredit) oder „betriebe“ (Betrieb verkaufen).</summary>
    public event Action<string>? Wegweiser;

    /// <summary>Die Frage, die gerade vorn liegt (null, wenn der Brief zu ist).</summary>
    public Entscheidung? Aktuell => Visible && _nr < _stapel.Count ? _stapel[_nr] : null;

    /// <summary>Wie viele Fragen im Stapel liegen.</summary>
    public int Anzahl => _stapel.Count;

    public void Verbinden(Simulation simulation, Konto spieler)
    {
        _simulation = simulation;
        _spieler = spieler;
    }

    public override void _Ready()
    {
        // Im neuen Stoff (Teil 4): ein Fenster in der Größe der Lade-Fenster, Rand aus Stein, der Titel der Frage im Kopf aus Holz, der Brief auf Pergament
        var rand = new MarginContainer { ThemeTypeVariation = "LadeFensterLeib" };
        AddChild(rand);
        _inhalt = new VBoxContainer { ThemeTypeVariation = "LadeFensterSpalte" };
        rand.AddChild(_inhalt);
        Rollbereich.Einhuellen(this);
        var rahmen = Fensterrahmen.Anmelden(this, "Entscheidung", () => Spaeter?.Invoke(), titel: "");
        _kleid = rahmen.Kleid;
        GroesseSetzen();
        GetViewport().SizeChanged += GroesseSetzen;
        Hide();
    }

    /// <summary>Feste Größe wie die Lade-Fenster (1280 × 530, schmaler bei schmalem Bild).</summary>
    private void GroesseSetzen()
    {
        if (!IsInsideTree()) return;
        var bild = GetViewportRect().Size;
        var stil = FensterStil.Thema.GetStylebox("panel", "FensterTafelKopf").GetMinimumSize();
        GetNode<Rollbereich>("Rollbereich").GetChild<Control>(0).CustomMinimumSize = new Vector2(
            Mathf.Min(FensterStil.Mass("breite", "LadeFensterMasse"), bild.X) - stil.X, FensterStil.Mass("hoehe", "LadeFensterMasse") - stil.Y);
    }

    /// <summary>
    /// Setzt den Stapel (dringendste zuerst; leer: Brief zu). Baut nur neu, wenn sich die Fragen ändern; die Frage, die vorn lag, bleibt vorn.
    /// </summary>
    public void Setzen(IReadOnlyList<Entscheidung> fragen)
    {
        if (fragen.Count == 0)
        {
            if (Visible) Hide();
            _stapel.Clear();
            _stand = "";
            _fehler = "";
            return;
        }
        var vorn = _nr < _stapel.Count ? _stapel[_nr].Nummer : -1;
        var stand = string.Join(",", fragen.Select(f => f.Nummer));
        if (stand == _stand && Visible) return;
        var neuVorn = Math.Max(0, fragen.ToList().FindIndex(f => f.Nummer == vorn));
        if (vorn != -1 && fragen.All(f => f.Nummer != vorn)) _fehler = ""; // die vordere ist weg: der Fehler gehörte zu ihr
        _stapel.Clear();
        _stapel.AddRange(fragen);
        _stand = stand;
        _nr = neuVorn;
        Aufbauen();
        if (!Visible)
        {
            _alter = 0;
            Modulate = new Color(1, 1, 1, Meldungseinstellungen.Bewegung ? 0 : 1);
            Show();
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        if (_alter < 1)
        {
            _alter += delta / 0.22;
            Modulate = new Color(1, 1, 1, Meldungseinstellungen.Bewegung ? (float)Math.Min(1, _alter) : 1f);
        }
        FristAuffrischen();
    }

    private Stadtwirtschaft? Wirtschaft => _simulation?.Wirtschaft;

    /// <summary>Spielminuten als „1:20“ (Spielstunden und -minuten).</summary>
    public static string Dauer(long minuten) => $"{minuten / 60}:{minuten % 60:00}";

    private void FristAuffrischen()
    {
        if (_fristText is null || _balken is null || Aktuell is not { } e || Wirtschaft?.Entscheidungen is not { } wesen) return;
        var rest = wesen.RestMinuten(e);
        var gesamt = Math.Max(1, e.Frist - e.Beginn);
        _balken.Anteil = Math.Clamp((float)rest / gesamt, 0f, 1f);
        _balken.QueueRedraw();
        var vor = wesen.Wahlen(e) is { } w && wesen.Voreinstellung(e) is var v && v >= 0 && v < w.Count ? w[v] : "";
        var text = (rest > 0 ? $"Noch {Dauer(rest)} Std. bis zur Voreinstellung" : "Die Voreinstellung gilt gleich") + (vor != "" ? $": {vor}" : "")
                   + (_simulation!.Takt.IstPausiert ? "  (die Zeit steht)" : "");
        if (_fristText.Text != text) _fristText.Text = text;
    }

    private static Label Schrift(Node eltern, string text, string variante, bool umbrechen = false)
    {
        var label = new Label { Text = text, ThemeTypeVariation = variante, MouseFilter = MouseFilterEnum.Ignore };
        if (umbrechen) label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        eltern.AddChild(label);
        return label;
    }

    private void Aufbauen()
    {
        UiHilfe.KinderEntfernen(_inhalt);
        _fristText = null;
        _balken = null;
        _fehlerText = null;
        if (_stapel.Count == 0 || Wirtschaft is not { } w || w.Entscheidungen is not { } wesen) return;
        _nr = Math.Clamp(_nr, 0, _stapel.Count - 1);
        var e = _stapel[_nr];
        if (_kleid is not null) _kleid.Titel = wesen.Titel(e);

        // Zwei Spalten wie im Entwurf: links der Brief (Absender, Text, Frist), rechts die Wahlen
        var spalten = new HBoxContainer { ThemeTypeVariation = "BriefSpalten", SizeFlagsVertical = SizeFlags.ExpandFill };
        _inhalt.AddChild(spalten);
        var links = new VBoxContainer { ThemeTypeVariation = "LadeFensterSpalte", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1f };
        spalten.AddChild(links);
        var rechts = new VBoxContainer { ThemeTypeVariation = "FensterBefehle", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.15f };
        spalten.AddChild(rechts);

        Absenderzeile(links, w.Familien, e);
        var text = Schrift(links, wesen.Beschreibung(e), "BriefText", umbrechen: true);
        text.CustomMinimumSize = new Vector2(FensterStil.Mass("text_breite", "BriefMasse"), 0);

        // Frist: Balken, darunter die Zahl
        _balken = new Fristbalken { CustomMinimumSize = new Vector2(0, FensterStil.Mass("frist_hoehe", "BriefMasse")), MouseFilter = MouseFilterEnum.Ignore };
        links.AddChild(_balken);
        _fristText = Schrift(links, "", "CharakterZusatz", umbrechen: true);
        FristAuffrischen();
        _fehlerText = _fehler != "" ? Schrift(links, _fehler, "FensterZeileWarnung", umbrechen: true) : null;

        // Die Wahlen: ein Befehl je Wahl mit seinen Folgen; Unumkehrbares ist als solches benannt
        var wahlen = wesen.Wahlen(e);
        for (var i = 0; i < wahlen.Count; i++)
        {
            var wahl = i;
            var folgen = wesen.Folgen(e, i);
            var endgueltig = folgen.Contains("nicht zurücknehmen", StringComparison.Ordinal);
            var knopf = new LadeBefehlKnopf(new LadeBefehl(wahlen[i], endgueltig ? "endgültig" : "", folgen, null, _wartet ? null : () => Waehlen(e, wahl)));
            if (endgueltig) knopf.TooltipText = "Nicht rückgängig zu machen!";
            rechts.AddChild(knopf);
        }

        // Fuß: „Später“ und der Stapel
        var fuss = new HBoxContainer { ThemeTypeVariation = "FensterZeileReihe" };
        rechts.AddChild(fuss);
        var spaeter = new Button { Text = "Später", FocusMode = FocusModeEnum.None, ThemeTypeVariation = "CharakterUeben",
            TooltipText = "Legt den Brief beiseite. Die Frist läuft weiter; der Brief mit der Feder rechts über der Lade oder das Fach „Familie“ holt ihn zurück." };
        spaeter.Pressed += () => Spaeter?.Invoke();
        fuss.AddChild(spaeter);
        if (_stapel.Count > 1)
        {
            var zurueck = new Button { Text = "‹", FocusMode = FocusModeEnum.None, TooltipText = "Voriger Brief", ThemeTypeVariation = "CharakterUeben" };
            zurueck.Pressed += () => { _nr = (_nr + _stapel.Count - 1) % _stapel.Count; _fehler = ""; Aufbauen(); KlangOberflaeche.Spielen(KlangOberflaeche.Ereignis.MeldungBrief); };
            var weiter = new Button { Text = "›", FocusMode = FocusModeEnum.None, TooltipText = "Nächster Brief", ThemeTypeVariation = "CharakterUeben" };
            weiter.Pressed += () => { _nr = (_nr + 1) % _stapel.Count; _fehler = ""; Aufbauen(); KlangOberflaeche.Spielen(KlangOberflaeche.Ereignis.MeldungBrief); };
            fuss.AddChild(zurueck);
            Schrift(fuss, $"Brief {_nr + 1} von {_stapel.Count}", "CharakterZusatz");
            fuss.AddChild(weiter);
        }
        Schrift(fuss, "Später: der Brief bleibt als Siegel mit Feder liegen.", "CharakterZusatz").SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    /// <summary>
    /// Kopf des Briefs: Porträt der Person der Frage – oder des Absenders (Oberhaupt seines Hauses) –, Name und Herkunft, rechts die Marke des Themas.
    /// Ein Klick aufs Porträt fehlt absichtlich, der Brief soll nicht ablenken.
    /// </summary>
    private void Absenderzeile(Container eltern, Familienbuch buch, Entscheidung e)
    {
        Person? person = e.Person is int n ? buch.PersonNummer(n) : null;
        var vonAbsender = false;
        if (person is null && e.Absender is { } absender && buch.IstAktiv && buch.Von(absender) is { } fam) { person = fam.Oberhaupt; vonAbsender = true; }
        var reihe = new HBoxContainer { ThemeTypeVariation = "CharakterWertReihe" };
        eltern.AddChild(reihe);
        if (person is not null)
        {
            var bild = PortraetAnzeige.Neu(FensterStil.Mass("bild", "BriefMasse"));
            bild.Zeigen(buch, person);
            reihe.AddChild(bild);
        }
        var namen = new VBoxContainer { ThemeTypeVariation = "FensterBefehlSpalte", SizeFlagsVertical = SizeFlags.ShrinkCenter, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reihe.AddChild(namen);
        Schrift(namen, person?.Name ?? (e.Absender is { } a ? a.Inhaber : "Euer Schreiber"), "FensterZeileWert");
        var herkunft = person is null ? "Absender" : (vonAbsender ? "Absender · " : person.Lebt ? "" : "† ") + "Haus " + person.Familienname;
        if (person is not null && !vonAbsender) herkunft += " · Absender: " + (e.Absender is { } b ? b.Inhaber : "Euer Schreiber");
        Schrift(namen, herkunft, "CharakterZusatz");
        var marke = new PanelContainer { ThemeTypeVariation = "BriefMarke", SizeFlagsVertical = SizeFlags.ShrinkCenter };
        marke.SetMeta(FensterStil.Stoff, FensterStil.Dunkel);
        reihe.AddChild(marke);
        Schrift(marke, "Entscheidung", "BriefMarkeText");
    }

    private void Waehlen(Entscheidung e, int wahl)
    {
        if (_wartet) return;
        _wartet = true;
        _fehler = "";
        Aufbauen();
        UiHilfe.Befehlen(_simulation, _spieler, new EntscheidungTreffen(e.Nummer, wahl), this, ergebnis =>
        {
            _wartet = false;
            if (ergebnis.Erfolg)
            {
                if (e.Art == Entscheidungsart.Mahnung && wahl == Entscheidungswesen.ZumKredit) Wegweiser?.Invoke("bank");
                else if (e.Art == Entscheidungsart.Mahnung && wahl == Entscheidungswesen.ZumVerkauf) Wegweiser?.Invoke("betriebe");
                Beantwortet?.Invoke();
                return;
            }
            // „✗“: Die Frage bleibt offen, der Brief bleibt stehen und nennt den Grund
            _fehler = "Das geht nicht: " + ergebnis.Meldung; // ohne „✗“ – die Schrift hat das Zeichen nicht (das Protokoll zeigt es als Siegel)
            Gemeldet?.Invoke(UiHilfe.MeldungText(ergebnis));
            Aufbauen();
        });
    }

    /// <summary>Der Fristbalken: Fläche in der Farbe des Themas leicht neben dem Umriss, Schraffur; rot, wenn weniger als ein Viertel bleibt.</summary>
    private sealed partial class Fristbalken : Control
    {
        public float Anteil = 1f;

        public override void _Draw()
        {
            // schlicht wie im Entwurf: Grund, Füllung in der Farbe des Themas (rot, wenn weniger als ein Viertel bleibt), feiner Rand – Farben aus dem Thema der Fenster
            DrawRect(new Rect2(0, 0, Size.X, Size.Y), FensterStil.Farbe("grund", "BriefFrist"));
            var farbe = FensterStil.Farbe(Anteil < 0.25f ? "knapp" : "fuellung", "BriefFrist");
            DrawRect(new Rect2(1, 1, Mathf.Max(0, (Size.X - 2) * Anteil), Size.Y - 2), farbe);
            DrawRect(new Rect2(0.5f, 0.5f, Size.X - 1, Size.Y - 1), FensterStil.Farbe("rand", "BriefFrist"), false, 1f);
        }
    }
}
