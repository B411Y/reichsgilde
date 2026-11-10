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
    private static readonly Color Papier = new(0.90f, 0.84f, 0.68f);
    private static readonly Color Tinte = new(0.14f, 0.10f, 0.07f);
    private static readonly Color Blass = new(0.38f, 0.30f, 0.20f);
    private static readonly Color Rot = new(0.62f, 0.15f, 0.10f);
    private const int Breite = 440;

    /// <summary>Farbe des Themas „Entscheidung“ (Farbband, Fristbalken, Siegel).</summary>
    public static readonly Color Farbe = new(0.48f, 0.23f, 0.37f);

    private Simulation? _simulation;
    private Konto? _spieler;
    private readonly List<Entscheidung> _stapel = new();
    private int _nr;
    private VBoxContainer _inhalt = null!;
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

    /// <summary>Zusätzlicher Abstand nach unten (z. B. über der Raumleiste im Innenraum).</summary>
    public float AbstandUnten { get; set; }

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
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Papier, BorderColor = new Color(0.09f, 0.07f, 0.05f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2, CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2,
            ShadowColor = new Color(0, 0, 0, 0.45f), ShadowSize = 2, ShadowOffset = new Vector2(5, 6),
        });
        var rand = new MarginContainer();
        rand.AddThemeConstantOverride("margin_left", 22);
        rand.AddThemeConstantOverride("margin_right", 22);
        rand.AddThemeConstantOverride("margin_top", 16);
        rand.AddThemeConstantOverride("margin_bottom", 18);
        AddChild(rand);
        _inhalt = new VBoxContainer { CustomMinimumSize = new Vector2(Breite, 0) };
        _inhalt.AddThemeConstantOverride("separation", 8);
        rand.AddChild(_inhalt);
        Rollbereich.Einhuellen(this);
        // Mittig über der Betriebsleiste (der gewöhnliche Brief sitzt rechts darüber)
        var rahmen = Fensterrahmen.Anmelden(this, "Entscheidung", () => Spaeter?.Invoke());
        rahmen.PlatzSetzen((groesse, sichtbar) => new Vector2((sichtbar.X - groesse.X) / 2,
            Mathf.Max(Rollbereich.KopfHoehe, sichtbar.Y - Rollbereich.LeistenHoehe - groesse.Y - 14 - AbstandUnten)));
        Hide();
    }

    /// <summary>Hinter dem ✕ des Fensterrahmens (hell, für dunkle Tafeln) eine dunkle Platte, damit es auf dem Pergament zu sehen ist.</summary>
    public override void _Draw()
    {
        var platte = new Rect2(Size.X - 40, 5, 34, 26);
        DrawRect(platte, new Color(0.22f, 0.17f, 0.12f));
        DrawRect(platte, new Color(0.09f, 0.07f, 0.05f), false, 2f);
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

    private void Aufbauen()
    {
        UiHilfe.KinderEntfernen(_inhalt);
        _fristText = null;
        _balken = null;
        _fehlerText = null;
        if (_stapel.Count == 0 || Wirtschaft is not { } w || w.Entscheidungen is not { } wesen) return;
        _nr = Math.Clamp(_nr, 0, _stapel.Count - 1);
        var e = _stapel[_nr];

        // Kopf: Absender klein, rechts Platz für das ✕ des Fensterrahmens
        var kopf = new HBoxContainer();
        _inhalt.AddChild(kopf);
        Hauptmenue.Text(kopf, "Absender: " + (e.Absender is { } a ? a.Inhaber : "Euer Schreiber") + "  ·  Eine Entscheidung", 13, Blass).SizeFlagsHorizontal = SizeFlags.ExpandFill;
        kopf.AddChild(new Control { CustomMinimumSize = new Vector2(34, 0) });

        // Titel auf dem Farbband
        var band = new BriefAnzeige.Farbband { Farbe = Farbe, CustomMinimumSize = new Vector2(0, 40) };
        _inhalt.AddChild(band);
        var titel = new Label { Text = wesen.Titel(e), VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore, ClipText = true };
        titel.AddThemeFontOverride("font", Schriften.Ueberschrift);
        titel.AddThemeFontSizeOverride("font_size", 22);
        titel.AddThemeColorOverride("font_color", Tinte);
        titel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        titel.OffsetLeft = 12;
        band.AddChild(titel);

        PortraetZeile(w.Familien, e);

        Hauptmenue.Text(_inhalt, wesen.Beschreibung(e), 16, Tinte, umbruchBreite: Breite - 6);

        // Frist: Balken mit Zahl
        _fristText = Hauptmenue.Text(_inhalt, "", 13, Blass, umbruchBreite: Breite - 6);
        _balken = new Fristbalken { CustomMinimumSize = new Vector2(0, 14), MouseFilter = MouseFilterEnum.Ignore };
        _inhalt.AddChild(_balken);
        FristAuffrischen();

        // Die Wahlen: ein Knopf je Wahl, Folgen im Tooltip, Unumkehrbares rot
        var wahlen = wesen.Wahlen(e);
        var vorgabe = wesen.Voreinstellung(e);
        for (var i = 0; i < wahlen.Count; i++)
        {
            var wahl = i;
            var folgen = wesen.Folgen(e, i);
            var knopf = new Button
            {
                Text = wahlen[i], FocusMode = FocusModeEnum.None, TooltipText = folgen, Disabled = _wartet, Alignment = HorizontalAlignment.Left,
                ClipText = true, CustomMinimumSize = new Vector2(0, 32),
            };
            knopf.AddThemeFontSizeOverride("font_size", 15);
            if (folgen.Contains("nicht zurücknehmen", StringComparison.Ordinal))
            {
                knopf.AddThemeColorOverride("font_color", new Color(0.95f, 0.45f, 0.38f));
                knopf.AddThemeColorOverride("font_hover_color", new Color(1f, 0.6f, 0.5f));
                knopf.TooltipText = "Nicht rückgängig zu machen!\n" + folgen;
            }
            knopf.Pressed += () => Waehlen(e, wahl);
            _inhalt.AddChild(knopf);
        }
        _fehlerText = _fehler != "" ? Hauptmenue.Text(_inhalt, _fehler, 14, Rot, umbruchBreite: Breite - 6) : null;

        // Fuß: Stapel und „Später“ links, Siegel mit Feder rechts
        var fuss = new HBoxContainer();
        fuss.AddThemeConstantOverride("separation", 8);
        _inhalt.AddChild(fuss);
        var leiste = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        leiste.AddThemeConstantOverride("separation", 8);
        fuss.AddChild(leiste);
        var spaeter = new Button { Text = "Später", FocusMode = FocusModeEnum.None, TooltipText = "Legt den Brief beiseite. Die Frist läuft weiter; das Feder-Siegel unten rechts holt ihn zurück." };
        spaeter.AddThemeFontSizeOverride("font_size", 14);
        spaeter.Pressed += () => Spaeter?.Invoke();
        leiste.AddChild(spaeter);
        if (_stapel.Count > 1)
        {
            var zurueck = new Button { Text = "‹", FocusMode = FocusModeEnum.None, TooltipText = "Voriger Brief" };
            zurueck.Pressed += () => { _nr = (_nr + _stapel.Count - 1) % _stapel.Count; _fehler = ""; Aufbauen(); KlangOberflaeche.Spielen(KlangOberflaeche.Ereignis.MeldungBrief); };
            var weiter = new Button { Text = "›", FocusMode = FocusModeEnum.None, TooltipText = "Nächster Brief" };
            weiter.Pressed += () => { _nr = (_nr + 1) % _stapel.Count; _fehler = ""; Aufbauen(); KlangOberflaeche.Spielen(KlangOberflaeche.Ereignis.MeldungBrief); };
            leiste.AddChild(zurueck);
            Hauptmenue.Text(leiste, $"Brief {_nr + 1} von {_stapel.Count}", 14, Blass).SizeFlagsVertical = SizeFlags.ShrinkCenter;
            leiste.AddChild(weiter);
        }
        fuss.AddChild(new BriefAnzeige.SiegelBild { Farbe = Farbe, Thema = "entscheidung", CustomMinimumSize = new Vector2(52, 52) });
        if (vorgabe < 0) { /* keine Voreinstellung bekannt: nichts weiter */ }
    }

    /// <summary>Porträt der Person der Frage – oder des Absenders (Oberhaupt seines Hauses); ein Klick fehlt hier absichtlich, der Brief soll nicht ablenken.</summary>
    private void PortraetZeile(Familienbuch buch, Entscheidung e)
    {
        Person? person = e.Person is int n ? buch.PersonNummer(n) : null;
        var vonAbsender = false;
        if (person is null && e.Absender is { } absender && buch.IstAktiv && buch.Von(absender) is { } fam) { person = fam.Oberhaupt; vonAbsender = true; }
        if (person is null) return;
        var reihe = new HBoxContainer();
        reihe.AddThemeConstantOverride("separation", 10);
        _inhalt.AddChild(reihe);
        var rahmen = new PanelContainer();
        rahmen.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Tinte, BorderColor = Tinte, BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
        });
        var bild = PortraetAnzeige.Neu(PortraetAnzeige.Klein);
        bild.Zeigen(buch, person);
        rahmen.AddChild(bild);
        reihe.AddChild(rahmen);
        var namen = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        namen.AddThemeConstantOverride("separation", 0);
        reihe.AddChild(namen);
        Hauptmenue.Text(namen, person.Name, 17, Tinte);
        Hauptmenue.Text(namen, (vonAbsender ? "Absender, " : person.Lebt ? "" : "† ") + "Haus " + person.Familienname, 13, Blass);
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
            var tinte = new Color(0.09f, 0.07f, 0.05f);
            DrawRect(new Rect2(0, 0, Size.X, Size.Y), new Color(0.78f, 0.70f, 0.52f));
            var farbe = Anteil < 0.25f ? new Color(0.70f, 0.20f, 0.14f) : Farbe.Lightened(0.1f);
            var voll = Mathf.Max(0, (Size.X - 4) * Anteil);
            DrawRect(new Rect2(3.5f, 3f, voll, Size.Y - 5), farbe);
            for (var x = 4f; x < voll; x += 5) DrawLine(new Vector2(x, Size.Y - 2), new Vector2(Mathf.Min(x + 6, 3.5f + voll), 4), new Color(0, 0, 0, 0.2f), 1f, true);
            DrawRect(new Rect2(1, 1, Size.X - 2, Size.Y - 2), tinte, false, 2f);
        }
    }
}
