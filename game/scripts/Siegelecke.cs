using Godot;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>Eine Meldung, wie sie im Verlauf steht (neueste zuerst) und in Siegeln und Briefen erscheint.</summary>
public sealed class Meldungsposten
{
    public string Zeit = "";
    public int Jahr;
    public string Text = "";
    /// <summary>Der Text, wie der Kern ihn meldete (zum Erkennen von Doppelungen).</summary>
    public string Original = "";
    public Meldeart Art = null!;
    public Betroffenheit Bezug;
    public Meldestufe Stufe;
    public bool Warnung;
    public bool RivalenZug;
    /// <summary>Name eines eigenen Betriebs, den der Text nennt (für den Knopf „Zum Betrieb“).</summary>
    public string? Betrieb;
    /// <summary>Die Meldung des Kerns mit Art, Haus, Person und Ort (Stufe 2) – null bei Meldungen der Oberfläche selbst.</summary>
    public Meldung? Kern;
}

/// <summary>Eine Zeile eines Briefes: Kopf (Zeit, Art), Text, Warnung (rot) und die Meldung des Kerns, zu der ein Knopf „Hinsehen“ springt.</summary>
public sealed record Briefzeile(string Kopf, string Text, bool Warnung, Meldung? Kern = null)
{
    public static implicit operator Briefzeile((string Kopf, string Text, bool Warnung) z) => new(z.Kopf, z.Text, z.Warnung);
}

/// <summary>Ein Brief: Absender, Titel und Zeilen; <see cref="Ziel"/> ist, wohin der Knopf unten führt (siehe <see cref="Meldeart.Ziel"/>).</summary>
public sealed class Brief
{
    /// <summary>Kennzeichnet den Brief; ein zweiter mit demselben Schlüssel ersetzt den ersten.</summary>
    public string Schluessel = "";
    public string Titel = "", Absender = "";
    public Color Farbe = new(0.7f, 0.6f, 0.3f);
    /// <summary>Das Thema (Farbe und Zeichen des Siegels am Fuß).</summary>
    public string Thema = "stadt";
    public List<Briefzeile> Zeilen = new();
    public string Ziel = "";
    public string? Betrieb;
    public string Fuss = "";
    /// <summary>Wen der Brief zeigt (Porträt neben dem Titel; ein Klick öffnet das Charakterfenster) – null: kein Porträt.</summary>
    public Person? Person;
    public Familienbuch? Buch;
    /// <summary>Die Meldung, zu der der Knopf „Hinsehen“ unten springt (bei einem Brief mit einer Meldung).</summary>
    public Meldung? Kern;
}

/// <summary>
/// Zeichnet ein Wachssiegel im Stil des kolorierten Holzschnitts: schwarzer Umriss, die Farbe leicht daneben, Schraffur im Schatten, ein Zeichen je Thema
/// (Ausrufezeichen, Waage, Herz, Krone, Turm, Kreuz). Für die Siegel der Ecke und das Siegel am Fuß jedes Briefes.
/// </summary>
public static class Siegelzeichner
{
    private static readonly Color Tinte = new(0.09f, 0.07f, 0.05f);

    public static void Zeichnen(CanvasItem ziel, Vector2 mitte, float r, Color farbe, string thema, int zufall, bool gedaempft = false)
    {
        if (gedaempft) farbe = farbe.Lerp(new Color(0.55f, 0.52f, 0.47f), 0.55f);
        // Unregelmäßiger Wachsrand: der Radius schwankt leicht (fest je Siegel)
        const int n = 30;
        var rand = new Vector2[n + 1];
        for (var i = 0; i < n; i++)
        {
            var w = i * Mathf.Tau / n;
            var schwank = 1f + 0.055f * Mathf.Sin(w * 3 + zufall) + 0.035f * Mathf.Sin(w * 5 + zufall * 2.3f);
            rand[i] = mitte + new Vector2(Mathf.Cos(w), Mathf.Sin(w)) * r * schwank;
        }
        rand[n] = rand[0];
        // Schlagschatten, dann die Farbfläche leicht versetzt (Handfarbe), dann der schwarze Umriss
        ziel.DrawColoredPolygon(rand.Select(p => p + new Vector2(2.5f, 3f)).Take(n).ToArray(), new Color(0, 0, 0, 0.32f));
        ziel.DrawColoredPolygon(rand.Select(p => p + new Vector2(1.6f, 1.2f)).Take(n).ToArray(), farbe);
        // Schraffur im Schatten (unten rechts): parallele Schrägstriche, je weiter außen, desto kürzer
        var normale = new Vector2(1, 1).Normalized();
        var richtung = new Vector2(-1, 1).Normalized();
        for (var k = 0; k < 5; k++)
        {
            var d = r * (0.38f + 0.11f * k);
            var h = Mathf.Sqrt(Mathf.Max(0, r * r - d * d)) * 0.82f;
            ziel.DrawLine(mitte + normale * d - richtung * h, mitte + normale * d + richtung * h, new Color(0, 0, 0, 0.36f), 1.2f, true);
        }
        ziel.DrawPolyline(rand, Tinte, 2f, true);
        // Prägering
        ziel.DrawArc(mitte + new Vector2(0.8f, 0.6f), r * 0.66f, 0, Mathf.Tau, 28, farbe.Darkened(0.35f), 1.6f, true);
        ziel.DrawArc(mitte, r * 0.66f, Mathf.Pi * 0.9f, Mathf.Pi * 1.7f, 12, farbe.Lightened(0.35f), 1.4f, true); // Glanz oben links
        Zeichen(ziel, mitte, r * 0.40f, thema);
    }

    private static void Linie(CanvasItem c, Vector2 mitte, float s, float breite, params (float X, float Y)[] punkte) => LinieFarbe(c, mitte, s, breite, Tinte, punkte);

    private static void LinieFarbe(CanvasItem c, Vector2 mitte, float s, float breite, Color farbe, params (float X, float Y)[] punkte)
    {
        var pts = punkte.Select(p => mitte + new Vector2(p.X, p.Y) * s).ToArray();
        c.DrawPolyline(pts, farbe, breite, true);
    }

    /// <summary>Eine Schreibfeder (Kiel unten links, Fahne oben rechts) in Tinte – Zeichen der Entscheidungen (Siegel, Verlauf, Brief).</summary>
    public static void Feder(CanvasItem c, Vector2 m, float s, float breite, Color? farbe = null)
    {
        var f = farbe ?? Tinte;
        LinieFarbe(c, m, s, breite * 0.9f, f, (-0.95f, 0.95f), (0.55f, -0.55f)); // Kiel
        LinieFarbe(c, m, s, breite * 0.8f, f, (0.95f, -0.95f), (0.35f, -0.9f), (-0.05f, -0.6f), (-0.4f, -0.1f), (-0.55f, 0.35f), (-0.45f, 0.45f)); // Fahne links oben
        LinieFarbe(c, m, s, breite * 0.8f, f, (0.95f, -0.95f), (0.9f, -0.35f), (0.6f, 0.05f), (0.2f, 0.3f), (-0.2f, 0.45f), (-0.45f, 0.45f)); // Fahne rechts unten
        LinieFarbe(c, m, s, breite * 0.5f, f, (0.1f, -0.45f), (0.55f, -0.1f)); // zwei Äste
        LinieFarbe(c, m, s, breite * 0.5f, f, (-0.15f, -0.05f), (0.25f, 0.2f));
    }

    /// <summary>
    /// Das Zeichen einer Meldungsart als Marke mit farbigem Rand – das Bild auf der Symbolfläche der 3D-Schriftrollen (<see cref="SchriftrollenBuehne"/>):
    /// Rand in der Farbe der Art, heller Grund leicht versetzt (Handfarbe), schwarzer Umriss, Schraffur im Schatten unten rechts, das Zeichen in Tinte.
    /// </summary>
    public static void Marke(CanvasItem ziel, Rect2 feld, Color farbe, string thema, Color grund, float randAnteil)
    {
        var s = Mathf.Min(feld.Size.X, feld.Size.Y);
        var strich = Mathf.Max(1.5f, s * 0.04f);
        ziel.DrawRect(feld, farbe);
        var innen = feld.Grow(-s * randAnteil);
        ziel.DrawRect(new Rect2(innen.Position + new Vector2(strich * 0.5f, strich * 0.4f), innen.Size - new Vector2(strich * 0.5f, strich * 0.4f)), grund);
        for (var k = 0; k < 3; k++)
        {
            var d = innen.Size.X * (0.16f + 0.10f * k);
            ziel.DrawLine(new Vector2(innen.End.X - d, innen.End.Y), new Vector2(innen.End.X, innen.End.Y - d), Tinte with { A = 0.34f }, Mathf.Max(1f, strich * 0.5f), true);
        }
        ziel.DrawRect(innen, Tinte, false, strich);
        ziel.DrawRect(feld.Grow(-strich / 2), Tinte, false, strich);
        Zeichen(ziel, feld.GetCenter(), innen.Size.X * 0.30f, thema);
    }

    /// <summary>Das Zeichen in der Mitte, gezeichnet in Tinte (Einheitskoordinaten von −1 bis 1, skaliert mit <paramref name="s"/>).</summary>
    private static void Zeichen(CanvasItem c, Vector2 m, float s, string thema)
    {
        var breite = Mathf.Max(1.8f, s * 0.2f);
        switch (thema)
        {
            case "gefahr": // Ausrufezeichen
                Linie(c, m, s, breite * 1.2f, (0, -0.95f), (0, 0.18f));
                c.DrawCircle(m + new Vector2(0, 0.72f) * s, breite * 0.85f, Tinte);
                break;
            case "handel": // Waage
                Linie(c, m, s, breite, (-0.95f, -0.45f), (0.95f, -0.45f));
                Linie(c, m, s, breite, (0, -0.9f), (0, 0.85f));
                Linie(c, m, s, breite, (-0.5f, 0.85f), (0.5f, 0.85f));
                Linie(c, m, s, breite * 0.8f, (-0.95f, -0.45f), (-1.15f, 0.2f), (-0.7f, 0.2f), (-0.95f, -0.45f));
                Linie(c, m, s, breite * 0.8f, (0.95f, -0.45f), (0.7f, 0.2f), (1.15f, 0.2f), (0.95f, -0.45f));
                break;
            case "familie": // Herz
                var herz = new List<(float, float)>();
                for (var i = 0; i <= 24; i++)
                {
                    var t = i / 24f * Mathf.Tau;
                    herz.Add((16f * Mathf.Pow(Mathf.Sin(t), 3) / 17f, -(13f * Mathf.Cos(t) - 5f * Mathf.Cos(2 * t) - 2f * Mathf.Cos(3 * t) - Mathf.Cos(4 * t)) / 17f));
                }
                Linie(c, m + new Vector2(0, 0.05f * s), s * 1.1f, breite, herz.ToArray());
                break;
            case "rat": // Krone
                Linie(c, m, s, breite, (-0.95f, 0.65f), (-0.95f, -0.45f), (-0.45f, 0.1f), (0, -0.8f), (0.45f, 0.1f), (0.95f, -0.45f), (0.95f, 0.65f), (-0.95f, 0.65f));
                break;
            case "entscheidung": // Feder
                Feder(c, m, s * 1.05f, breite);
                break;
            case "befehl": // Kreuz
                Linie(c, m, s, breite * 1.2f, (-0.7f, -0.7f), (0.7f, 0.7f));
                Linie(c, m, s, breite * 1.2f, (0.7f, -0.7f), (-0.7f, 0.7f));
                break;
            default: // Turm mit Zinnen
                Linie(c, m, s, breite, (-0.7f, 0.9f), (-0.7f, -0.2f), (-0.95f, -0.2f), (-0.95f, -0.9f), (-0.5f, -0.9f), (-0.5f, -0.55f), (-0.15f, -0.55f), (-0.15f, -0.9f),
                      (0.15f, -0.9f), (0.15f, -0.55f), (0.5f, -0.55f), (0.5f, -0.9f), (0.95f, -0.9f), (0.95f, -0.2f), (0.7f, -0.2f), (0.7f, 0.9f), (-0.7f, 0.9f));
                break;
        }
    }
}

/// <summary>
/// Eine Meldung in der Ecke: eine 3D-Schriftrolle (<see cref="SchriftrollenBuehne"/>), die geschlossen erscheint, mit einem kurzen Sprung aufrollt und
/// dann das Zeichen ihres Themas mit farbigem Rand zeigt; dazu eine Zahl, wenn sich mehrere Meldungen stapeln. Linksklick liest den Brief, Rechtsklick
/// legt sie ungelesen beiseite (verwirft sie). Die Rolle bleibt offen und pendelt ganz leicht, bis die Meldung erledigt ist; dann rollt sie zu, springt
/// kurz und verschwindet (<see cref="Schliessen"/>). Ohne Bewegung (<see cref="Meldungseinstellungen.Bewegung"/>) steht sie sofort offen und still.
/// Steht die Bühne nicht (Modell fehlt, alle Plätze belegt), zeichnet sich die Meldung wie früher als Wachssiegel in 2D.
/// </summary>
public sealed partial class Siegel : Control
{
    public const float Groesse = 56;

    public string Schluessel = "";
    public string ThemaId = "stadt";
    public Color Farbe = Colors.Gray;
    public int Anzahl = 1;
    public bool Gelesen;
    /// <summary>Bleibt, bis die Hauptszene es selbst wegnimmt (Entscheidungen): Rechtsklick und Frist verwerfen es nicht.</summary>
    public bool Dauerhaft;
    /// <summary>Echtzeit in Sekunden, nach der das Siegel von selbst verschwindet (0: bleibt, bis es gelesen ist).</summary>
    public double Frist;
    public List<Meldungsposten> Posten = new();
    private Control? _bild;
    /// <summary>Baut den Brief, wenn das Siegel angeklickt wird (so zeigt ein Stillstand immer den neuesten Stand).</summary>
    public Func<Siegel, Brief>? BriefBauer;
    public event Action<Siegel>? Angeklickt, Verworfen;

    /// <summary>Die gemeinsame Bühne der Schriftrollen (setzt die <see cref="Siegelecke"/> vor dem Einhängen).</summary>
    public SchriftrollenBuehne? Buehne;
    private SchriftrollenBuehne.Rolle? _rolle;
    private Action? _geschlossen;
    /// <summary>So viele Bilder zeichnet sich die Rolle nach dem Erscheinen noch nicht: Das Skelett übernimmt die neue Haltung erst mit dem nächsten Bild der Bühne.</summary>
    private int _verdeckt;

    private double _alter;
    private bool _ueber;
    private readonly int _zufall = (int)(GD.Randi() % 100);

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(Groesse, Groesse);
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        MouseEntered += () => { _ueber = true; QueueRedraw(); };
        MouseExited += () => { _ueber = false; QueueRedraw(); };
        _rolle = Buehne?.Nehmen(ThemaId, Farbe);
        _rolle?.Ablauf.Erscheinen(Meldungseinstellungen.Bewegung);
        Buehne?.Uebernehmen(_rolle);
        _verdeckt = 2;
    }

    public override void _ExitTree()
    {
        Buehne?.Geben(_rolle);
        _rolle = null;
    }

    /// <summary>Die Rolle erscheint (noch einmal) geschlossen und rollt auf – wenn die Einblendung in der Ecke ankommt.</summary>
    public void Erscheinen()
    {
        _alter = 0;
        _rolle?.Ablauf.Erscheinen(Meldungseinstellungen.Bewegung);
        Buehne?.Uebernehmen(_rolle);
        _verdeckt = 2;
        QueueRedraw();
    }

    /// <summary>
    /// Die Meldung ist erledigt: Die Rolle rollt zu, springt kurz und verschwindet; danach ruft sie <paramref name="fertig"/>. Ohne Rolle, ohne Bewegung
    /// oder unsichtbar (hinter einer großen Tafel, noch vor der Einblendung) sofort.
    /// </summary>
    public void Schliessen(Action fertig)
    {
        if (_geschlossen is not null) return;
        MouseFilter = MouseFilterEnum.Ignore;
        _bild?.QueueFree();
        _bild = null;
        Anzahl = 1;
        if (_rolle is null || !Meldungseinstellungen.Bewegung || !IsVisibleInTree() || Modulate.A <= 0)
        {
            fertig();
            return;
        }
        _geschlossen = fertig;
        _rolle.Ablauf.Schliessen(true);
        QueueRedraw();
    }

    /// <summary>
    /// Ein kleines Bild unten links am Siegel (das Porträt des Betroffenen, Stufe 2) – ersetzt das vorige; null nimmt es weg. Fängt keine Klicks ab:
    /// ein Klick liest weiter den Brief.
    /// </summary>
    public void BildSetzen(Control? bild)
    {
        _bild?.QueueFree();
        _bild = bild;
        if (bild is null) return;
        bild.MouseFilter = MouseFilterEnum.Ignore;
        foreach (var kind in bild.FindChildren("*", owned: false).OfType<Control>()) kind.MouseFilter = MouseFilterEnum.Ignore;
        bild.Position = new Vector2(-10, Groesse - 22); // unten links neben dem Zeichen der Rolle
        AddChild(bild);
    }

    /// <summary>Ein neuer Posten kam dazu: das Siegel springt kurz auf.</summary>
    public void Aufleben()
    {
        Gelesen = false;
        _alter = 0.12;
        _rolle?.Ablauf.Huepfen();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _alter += delta;
        if (_verdeckt > 0 && --_verdeckt == 0) QueueRedraw();
        if (_geschlossen is not null)
        {
            // Zurollen läuft: weg, sobald die Rolle fertig ist (oder die Bewegung inzwischen abgeschaltet wurde)
            if (_rolle is null || _rolle.Ablauf.Zustand == RollenZustand.Weg || !Meldungseinstellungen.Bewegung)
            {
                var fertig = _geschlossen;
                _geschlossen = null;
                SetProcess(false);
                fertig();
                return;
            }
            QueueRedraw(); // Ausblenden am Ende
            return;
        }
        if (Frist > 0 && !Dauerhaft && _alter >= Frist) Verworfen?.Invoke(this);
        if (_rolle is null && Meldungseinstellungen.Bewegung && (_alter < 0.4 || (!Gelesen && !_ueber))) QueueRedraw();
    }

    public override void _GuiInput(InputEvent ereignis)
    {
        if (ereignis is not InputEventMouseButton { Pressed: true } klick) return;
        if (klick.ButtonIndex == MouseButton.Left) Angeklickt?.Invoke(this);
        else if (klick.ButtonIndex == MouseButton.Right && !Dauerhaft) Verworfen?.Invoke(this);
        AcceptEvent();
    }

    public override void _Draw()
    {
        if (_rolle is not null && Buehne?.Bild is { } bild)
        {
            if (_verdeckt > 0) return;
            // Der Ausschnitt dieser Rolle aus dem einen Bild der Bühne, mittig über dem Feld (größer als das Feld: Platz für Sprung und Pendeln)
            var seite = Buehne.Zelle * (_ueber && _geschlossen is null ? MeldungStil.Mass("rolle_ueber_prozent") / 100f : 1f);
            var ton = Gelesen ? MeldungStil.RollenFarbe("gelesen") : Colors.White;
            DrawTextureRectRegion(bild, new Rect2((new Vector2(Groesse, Groesse) - new Vector2(seite, seite)) / 2, new Vector2(seite, seite)), Buehne.Ausschnitt(_rolle),
                                  ton with { A = (float)_rolle.Ablauf.Deckkraft });
            Zaehler();
            return;
        }
        var bewegt = Meldungseinstellungen.Bewegung;
        var t = bewegt ? Mathf.Min(1f, (float)(_alter / 0.28)) : 1f;
        var groesse = 1f + (bewegt ? 0.55f * (1 - t) * (1 - t) : 0f);
        if (bewegt && !Gelesen && !_ueber && t >= 1f) groesse *= 1f + 0.028f * Mathf.Sin((float)_alter * 3.2f);
        if (_ueber) groesse *= 1.06f;
        var mitte = new Vector2(Groesse, Groesse) / 2;
        DrawSetTransform(mitte, 0, new Vector2(groesse, groesse));
        Siegelzeichner.Zeichnen(this, Vector2.Zero, Groesse * 0.40f, Farbe with { A = bewegt ? t : 1f }, ThemaId, _zufall, Gelesen);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        Zaehler();
    }

    /// <summary>Die Zahl oben rechts, wenn sich mehrere Meldungen stapeln.</summary>
    private void Zaehler()
    {
        if (Anzahl > 1)
        {
            var ort = new Vector2(Groesse - 12, 12);
            DrawCircle(ort + new Vector2(1.2f, 1.5f), 10, new Color(0, 0, 0, 0.35f));
            DrawCircle(ort, 10, new Color(0.92f, 0.86f, 0.70f));
            DrawArc(ort, 10, 0, Mathf.Tau, 20, new Color(0.09f, 0.07f, 0.05f), 1.8f, true);
            var text = Anzahl > 99 ? "99+" : Anzahl.ToString();
            var schrift = ThemeDB.FallbackFont;
            DrawString(schrift, ort + new Vector2(-10, 4.5f), text, HorizontalAlignment.Center, 20, 13, new Color(0.09f, 0.07f, 0.05f));
        }
    }
}

/// <summary>
/// Die Ecke unten rechts, über der Betriebsleiste: eine Reihe von Meldungen als 3D-Schriftrollen (im Code weiter „Siegel“), die neueste links, damit sich
/// die älteren nie verschieben. Eine Rolle je Thema (mit Zähler); Stillstände haben je Betrieb eine eigene. Fängt selbst keine Mausklicks ab. Die Höhe, die
/// sie braucht, steht in <see cref="Rollbereich.EckeHoehe"/> – die Tafeln rechts lassen den Platz frei, statt verdeckt zu werden. Alle Rollen teilen sich
/// eine kleine 3D-Bühne (<see cref="SchriftrollenBuehne"/>), die nur rechnet, solange die Ecke zu sehen ist.
/// </summary>
public sealed partial class Siegelecke : Control
{
    public const int Hoechstens = 7;
    private readonly HBoxContainer _reihe = new() { Alignment = BoxContainer.AlignmentMode.End, MouseFilter = MouseFilterEnum.Ignore };
    private readonly List<Siegel> _siegel = new();
    private readonly SchriftrollenBuehne _buehne = new() { Name = "SchriftrollenBuehne" };
    private int _schliessend;

    /// <summary>Das Siegel wurde angeklickt (gelesen werden soll).</summary>
    public event Action<Siegel>? Angeklickt;

    public IReadOnlyList<Siegel> Alle => _siegel;

    /// <summary>Der Knopf „Verlauf“ rechts in der Ecke: alle Meldungen, auch die gelesenen und beiseitegelegten, im Bericht nachlesen.</summary>
    public event Action? VerlaufGewuenscht;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Theme = MeldungStil.Thema;
        _buehne.Sichtbar = IsVisibleInTree;
        AddChild(_buehne);
        _reihe.AddThemeConstantOverride("separation", 6);
        _reihe.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_reihe);
        // Der Knopf „Verlauf“ steht immer ganz rechts (Siegel kommen links dazu) – auch wenn keine Siegel mehr da sind
        var verlauf = new Button
        {
            Text = "Verlauf", ThemeTypeVariation = "MeldungVerlaufKnopf", FocusMode = FocusModeEnum.None, SizeFlagsVertical = SizeFlags.ShrinkEnd,
            TooltipText = "Alle Meldungen nachlesen, auch gelesene und beiseitegelegte (Bericht, Reiter „Verlauf“) – mit Datum und Art; ein Klick auf eine Zeile führt hin.",
        };
        verlauf.Pressed += () => VerlaufGewuenscht?.Invoke();
        _reihe.AddChild(verlauf);
        // Unten rechts, über der Betriebsleiste; wächst nach links
        SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight, LayoutPresetMode.KeepSize, 16);
        CustomMinimumSize = new Vector2(Siegel.Groesse * (Hoechstens + 1) + 6 * (Hoechstens + 1) + MeldungStil.Mass("verlauf_breite"), Siegel.Groesse + 6); // Platz für die dauerhafte Feder und den Knopf
        OffsetLeft = -16 - CustomMinimumSize.X;
        OffsetRight = -16;
        Hoehe(Rollbereich.LeistenHoehe);
    }

    /// <summary>Setzt den Abstand zum unteren Rand (über der Leiste bzw. der Raumleiste).</summary>
    public void Hoehe(float vonUnten)
    {
        OffsetBottom = -vonUnten;
        OffsetTop = -vonUnten - Siegel.Groesse - 6;
    }

    public Siegel? Finden(string schluessel) => _siegel.FirstOrDefault(s => s.Schluessel == schluessel);

    /// <summary>Legt ein Siegel an oder fügt dem vorhandenen mit diesem Schlüssel den Posten hinzu. Gibt zurück, ob es neu war.</summary>
    public bool Hinzufuegen(string schluessel, Meldethema thema, Meldungsposten posten, Func<Siegel, Brief> briefBauer, double frist = 0, bool stapeln = true, bool dauerhaft = false)
    {
        var vorhanden = Finden(schluessel);
        if (vorhanden is not null)
        {
            if (stapeln)
            {
                vorhanden.Posten.Insert(0, posten);
                if (vorhanden.Posten.Count > 60) vorhanden.Posten.RemoveAt(vorhanden.Posten.Count - 1);
                vorhanden.Anzahl = vorhanden.Gelesen ? 1 : vorhanden.Anzahl + 1;
            }
            vorhanden.Aufleben();
            return false;
        }
        var s = new Siegel
        {
            Schluessel = schluessel, ThemaId = thema.Id, Farbe = Color.FromHtml(thema.Farbe), Frist = frist, BriefBauer = briefBauer, Dauerhaft = dauerhaft,
            Posten = new List<Meldungsposten> { posten }, Buehne = _buehne,
        };
        s.Angeklickt += k => Angeklickt?.Invoke(k);
        s.Verworfen += Entfernen;
        s.TooltipText = "";
        _siegel.Insert(0, s);
        _reihe.AddChild(s);
        _reihe.MoveChild(s, 0);
        // Das älteste gewöhnliche Siegel weicht (es steht weiter im Verlauf); ein dauerhaftes (Feder der Entscheidungen) zählt nicht mit und weicht nie
        while (_siegel.Count(x => !x.Dauerhaft) > Hoechstens && _siegel.LastOrDefault(x => !x.Dauerhaft) is { } aelteste) Entfernen(aelteste);
        Rollbereich.EckeHoehe = Siegel.Groesse + 14;
        return true;
    }

    /// <summary>
    /// Die Meldung ist erledigt (gelesen, beiseitegelegt, abgelaufen, verdrängt): Sie zählt sofort nicht mehr mit, ihre Rolle rollt aber erst zu und
    /// verschwindet dann aus der Reihe (<see cref="Siegel.Schliessen"/>). Der Platz der Ecke bleibt so lange frei.
    /// </summary>
    public void Entfernen(Siegel s) => Entfernen(s, false);

    private void Entfernen(Siegel s, bool sofort)
    {
        if (!_siegel.Remove(s)) return;
        if (sofort)
        {
            Wegnehmen(s);
            return;
        }
        _schliessend++;
        s.Schliessen(() =>
        {
            _schliessend--;
            Wegnehmen(s);
        });
    }

    private void Wegnehmen(Siegel s)
    {
        if (IsInstanceValid(s))
        {
            if (s.GetParent() == _reihe) _reihe.RemoveChild(s);
            s.QueueFree();
        }
        if (_siegel.Count == 0 && _schliessend <= 0) Rollbereich.EckeHoehe = 0;
    }

    /// <summary>Alle Meldungen sofort weg, ohne Zurollen (neue Partie, Spielstand geladen).</summary>
    public void Leeren()
    {
        foreach (var s in _siegel.ToList()) Entfernen(s, true);
    }

    /// <summary>Tooltips der Siegel auffrischen (erste Zeile des neuesten Postens).</summary>
    public void TippsAuffrischen()
    {
        foreach (var s in _siegel)
        {
            var neu = s.Posten.Count > 0 ? s.Posten[0].Text : "";
            if (neu.Length > 140) neu = neu[..140] + "…";
            var tipp = s.Dauerhaft ? neu + "\nKlick: Entscheidungsbrief öffnen"
                : (s.Anzahl > 1 ? $"{s.Anzahl} Meldungen · " : "") + neu + "\nKlick: Brief lesen · Rechtsklick: beiseitelegen";
            if (s.TooltipText != tipp) s.TooltipText = tipp;
        }
    }
}
