using System.Text;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Tooltips des Spiels (PR #214), Vorbild ist die Bedienung in Crusader Kings 3 (nur die Mechanik):
///   • <b>Festheften:</b> Ein Tooltip zeigt oben rechts einen kleinen Füllkreis. Ist er nach <see cref="Fenster.TooltipFestZeit"/> voll – oder drückt man die
///     mittlere Maustaste bzw. T –, wird der Tooltip ein kleines Fenster mit ✕, das stehen bleibt und sich verschieben lässt.
///   • <b>Verweise:</b> Im Text steht <c>[[kennung]]</c> oder <c>[[kennung|Anzeigetext]]</c>. Das wird unterstrichen; verweilt die Maus darauf, öffnet sich ein
///     Tooltip zum Begriff (<see cref="BegriffSuchen"/>), der selbst wieder Verweise haben und festgeheftet werden kann – beliebig tief. Ein Klick heftet gleich fest.
///
/// Wie es zu den rund 170 vorhandenen <c>TooltipText</c>-Stellen kommt, ohne sie umzubauen: Der eingebaute Tooltip ist in <c>project.godot</c> ausgeschaltet
/// (<c>gui/timers/tooltip_delay_sec</c> riesig). Dieser Autoload fragt jedes Bild, über welchem Steuerelement die Maus steht
/// (<see cref="Viewport.GuiGetHoveredControl"/>), liest dessen <see cref="Control.GetTooltip"/> – wie der eingebaute Tooltip, auch von den Eltern, bis ein Steuerelement
/// die Maus für sich beansprucht – und zeigt den Text im Stil der Tafeln.
///
/// Kein eigener Text für Begriffe: Den liefert das Lexikon über <see cref="BegriffSuchen"/>. Ohne Lexikon (oder ohne Treffer) bleibt ein Verweis normaler Text.
/// </summary>
public partial class Tooltips : Node
{
    /// <summary>Vom Lexikon gesetzt: Titel und Text zu einer Kennung, null bei keinem Treffer (der Verweis bleibt dann einfacher Text).</summary>
    public static Func<string, (string Titel, string Text)?>? BegriffSuchen;

    /// <summary>Vom Lexikon gesetzt: öffnet das Lexikon bei dieser Kennung; der feste Tooltip eines Begriffs bekommt dafür einen Knopf „Im Lexikon“.</summary>
    public static Action<string>? LexikonOeffnen;

    /// <summary>Sekunden, die die Maus auf einem Steuerelement ruhen muss, bis sein Tooltip erscheint (wie der eingebaute Tooltip).</summary>
    private const double Verzoegerung = 0.5;
    /// <summary>Sekunden, die die Maus auf einem Verweis ruhen muss, bis sein Begriff erscheint.</summary>
    private const double VerweisVerzoegerung = 0.35;
    /// <summary>Sekunden, die ein Tooltip nach dem Weggehen der Maus bleibt – so lange braucht die Maus von der Quelle in den Tooltip.</summary>
    private const double Nachlauf = 0.25;
    private const int MaxBreite = 340;
    private const int Schriftgroesse = 16;

    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Hell = new(0.92f, 0.88f, 0.80f);

    private sealed class Tipp
    {
        public PanelContainer Tafel = null!;
        public RichTextLabel Text = null!;
        public Control Kreis = null!;
        public Control Rechts = null!;
        public VBoxContainer Spalte = null!;
        public Control? Quelle;
        public Tipp? Eltern;
        public List<Tipp> Kinder = new();
        public string? Kennung;
        public bool Fest, Gezogen;
        public double Fuellung, Inaktiv;
        public string? VerweisUnterMaus;
        public double VerweisSeit;
        public Vector2 Anker, Griff;
        public int Bilder;
    }

    private CanvasLayer _ebene = null!;
    private readonly List<Tipp> _tipps = new();
    private Control? _quelle;
    private string _quelleText = "";
    private double _quelleSeit;
    private bool _unterdrueckt;

    /// <summary>Für Klicktests (<c>--tiptest=1</c>): Begriffe, wenn das Lexikon keine liefert, und ein Knopf mit einem Tooltip voller Verweise.</summary>
    private static readonly Dictionary<string, (string Titel, string Text)> Testbegriffe = new()
    {
        ["zunft"] = ("Zunft", "Der Zusammenschluss der Handwerker einer Stadt. Sie wacht über [[lehrling|Ausbildung]] und [[meister|Meistergrade]]."),
        ["lehrling"] = ("Lehrling", "Wer ein Handwerk lernt. Nach den Lehrjahren wird er Geselle, später vielleicht [[meister|Meister]]."),
        ["meister"] = ("Meister", "Wer die Meisterprüfung der [[zunft|Zunft]] besteht, darf eine eigene [[werkstatt]] führen."),
        ["werkstatt"] = ("Werkstatt", "Hier arbeiten Meister, Gesellen und [[lehrling|Lehrlinge]]. Der Ertrag hängt vom Meistergrad ab."),
    };

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _ebene = new CanvasLayer { Layer = 110, Name = "Tooltips" };
        AddChild(_ebene);
        if (OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--tiptest")))
        {
            var knopf = new Button
            {
                Text = "Tooltip-Test", FocusMode = Control.FocusModeEnum.None,
                TooltipText = "Die [[zunft|Zunft]] ordnet das Handwerk. Mehr zu [[meister]], [[werkstatt]] und [[unbekannt|einem Unbekannten]].\n(Verweilen: festheften)",
            };
            knopf.Position = new Vector2(620, 300);
            knopf.CustomMinimumSize = new Vector2(160, 40);
            var testEbene = new CanvasLayer { Layer = 90 };
            testEbene.AddChild(knopf);
            AddChild(testEbene);
        }
        // --tooltipfest=<Sekunden|taste> für Klicktests (gilt nur für diesen Start)
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (!arg.StartsWith("--tooltipfest=")) continue;
            var wert = arg["--tooltipfest=".Length..];
            if (wert == "taste") Fenster.TooltipFestSetzen(Fenster.TooltipFestZeit, true, speichern: false);
            else if (double.TryParse(wert, System.Globalization.CultureInfo.InvariantCulture, out var sekunden))
                Fenster.TooltipFestSetzen(sekunden, false, speichern: false);
        }
    }

    // ── Eingabe ─────────────────────────────────────────────────────────────

    public override void _Input(InputEvent eingabe)
    {
        switch (eingabe)
        {
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.T, CtrlPressed: false, AltPressed: false, ShiftPressed: false }:
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Middle }:
                if (Ziel() is { } ziel)
                {
                    Festmachen(ziel);
                    GetViewport().SetInputAsHandled();
                }
                break;
            case InputEventMouseButton { Pressed: true }:
                // Ein Klick außerhalb der Tooltips beendet die losen (wie beim eingebauten Tooltip) und bis zum nächsten Steuerelement kommt keiner wieder
                var maus = GetViewport().GetMousePosition();
                if (!_tipps.Any(t => t.Tafel.GetGlobalRect().HasPoint(maus)))
                {
                    foreach (var wurzel in _tipps.Where(t => !t.Fest && t.Eltern is null).ToArray()) Entfernen(wurzel);
                    _unterdrueckt = true;
                }
                break;
        }
    }

    /// <summary>Der Tooltip, den T bzw. die mittlere Maustaste festheftet: der jüngste lose unter der Maus oder an der gehaltenen Quelle.</summary>
    private Tipp? Ziel()
    {
        var maus = GetViewport().GetMousePosition();
        return _tipps.LastOrDefault(t => !t.Fest && (t.Tafel.GetGlobalRect().HasPoint(maus) || QuelleGehalten(t)));
    }

    /// <summary>Esc: schließt den obersten festen Tooltip. false, wenn keiner offen ist.</summary>
    public static bool ObersteFesteSchliessen() => _instanz?.ObersteFeste() ?? false;

    /// <summary>Schließt alle Tooltips, auch die festen (Taste X).</summary>
    public static void AlleSchliessen()
    {
        if (_instanz is null) return;
        foreach (var t in _instanz._tipps.ToArray()) _instanz.Entfernen(t, auchFeste: true);
    }

    /// <summary>Ist ein fester Tooltip offen?</summary>
    public static bool FesteOffen => _instanz?._tipps.Any(t => t.Fest) ?? false;

    private static Tooltips? _instanz;

    public override void _EnterTree() => _instanz = this;

    public override void _ExitTree()
    {
        if (_instanz == this) _instanz = null;
        BegriffSuchen = null;
        LexikonOeffnen = null;
    }

    private bool ObersteFeste()
    {
        var t = _tipps.LastOrDefault(x => x.Fest);
        if (t is null) return false;
        Entfernen(t, auchFeste: true);
        return true;
    }

    // ── Jedes Bild ──────────────────────────────────────────────────────────

    public override void _Process(double delta)
    {
        var viewport = GetViewport();
        var maus = viewport.GetMousePosition();
        QuelleAktualisieren(viewport.GuiGetHoveredControl(), delta);

        if (_quelle is not null && _quelleSeit >= Verzoegerung && !_unterdrueckt && _quelleText != ""
            && !_tipps.Any(t => !t.Fest && t.Quelle == _quelle))
        {
            foreach (var alt in _tipps.Where(t => !t.Fest && t.Eltern is null).ToArray()) Entfernen(alt);
            Neu(null, _quelleText, null, null, _quelle, maus);
        }

        var sichtbar = viewport.GetVisibleRect().Size;
        foreach (var t in _tipps.ToArray())
        {
            if (!IsInstanceValid(t.Tafel)) continue;
            t.Bilder++;
            if (t.Bilder == 2) t.Tafel.Modulate = Colors.White; // erst zeigen, wenn die Größe feststeht

            if (t.Gezogen)
            {
                if (Input.IsMouseButtonPressed(MouseButton.Left))
                    t.Tafel.GlobalPosition = Klemmen(maus - t.Griff, t.Tafel.Size, sichtbar);
                else
                    t.Gezogen = false;
            }
            else if (!t.Fest)
                t.Tafel.GlobalPosition = Lage(t.Anker, t.Tafel.Size, sichtbar);

            if (t.Fest) continue;

            var imTipp = t.Tafel.GetGlobalRect().HasPoint(maus);
            var gehalten = QuelleGehalten(t);
            if (imTipp || gehalten || t.Kinder.Any(k => Aktiv(k, maus))) t.Inaktiv = 0;
            else
            {
                t.Inaktiv += delta;
                if (t.Inaktiv > Nachlauf) { Entfernen(t); continue; }
            }

            if ((imTipp || gehalten) && !Fenster.TooltipNurMitTaste)
            {
                t.Fuellung += delta;
                t.Kreis.QueueRedraw();
                if (t.Fuellung >= Fenster.TooltipFestZeit) { Festmachen(t); continue; }
            }

            // Verweilt die Maus auf einem Verweis, öffnet sich dessen Begriff
            if (t.VerweisUnterMaus is { } kennung)
            {
                t.VerweisSeit += delta;
                if (t.VerweisSeit >= VerweisVerzoegerung && !t.Kinder.Any(k => k.Kennung == kennung))
                    BegriffOeffnen(t, kennung, maus, fest: false);
            }
        }
    }

    private bool Aktiv(Tipp t, Vector2 maus) =>
        t.Tafel.GetGlobalRect().HasPoint(maus) || QuelleGehalten(t) || t.Kinder.Any(k => Aktiv(k, maus));

    private bool QuelleGehalten(Tipp t) =>
        t.Eltern is null
            ? t.Quelle is not null && t.Quelle == _quelle
            : t.Kennung is not null && t.Eltern.VerweisUnterMaus == t.Kennung;

    /// <summary>Findet das Steuerelement unter der Maus, das einen Tooltip hat (wie der eingebaute: nach oben, bis eins die Maus beansprucht).</summary>
    private void QuelleAktualisieren(Control? unter, double delta)
    {
        if (unter is not null && _ebene.IsAncestorOf(unter)) return; // über einem eigenen Tooltip: Quelle bleibt
        Control? quelle = null;
        var text = "";
        for (var c = unter; c is not null; c = c.GetParentOrNull<Control>())
        {
            var t = c.GetTooltip(c.GetLocalMousePosition());
            if (!string.IsNullOrEmpty(t))
            {
                quelle = c;
                text = t;
                break;
            }
            if (c.MouseFilter == Control.MouseFilterEnum.Stop || c.TopLevel) break;
        }
        if (quelle != _quelle)
        {
            _quelle = quelle;
            _quelleSeit = 0;
            _unterdrueckt = false;
        }
        else _quelleSeit += delta;
        _quelleText = text;
    }

    // ── Aufbau ──────────────────────────────────────────────────────────────

    private void BegriffOeffnen(Tipp eltern, string kennung, Vector2 maus, bool fest)
    {
        var bestehend = eltern.Kinder.FirstOrDefault(k => k.Kennung == kennung);
        if (bestehend is not null)
        {
            if (fest && !bestehend.Fest) Festmachen(bestehend);
            return;
        }
        if (Suchen(kennung) is not { } begriff) return;
        var neu = Neu(begriff.Titel, begriff.Text, eltern, kennung, null, maus);
        if (fest) Festmachen(neu);
    }

    private static (string Titel, string Text)? Suchen(string kennung)
    {
        if (BegriffSuchen?.Invoke(kennung) is { } treffer) return treffer;
        return Testbegriffe.TryGetValue(kennung, out var test) && OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--tiptest")) ? test : null;
    }

    private Tipp Neu(string? titel, string roh, Tipp? eltern, string? kennung, Control? quelle, Vector2 maus)
    {
        var t = new Tipp { Eltern = eltern, Kennung = kennung, Quelle = quelle, Anker = maus };
        eltern?.Kinder.Add(t);

        t.Tafel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop, Modulate = new Color(1, 1, 1, 0) };
        t.Tafel.AddThemeStyleboxOverride("panel", Stil(false));
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 8);
        t.Tafel.AddChild(zeile);
        t.Spalte = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        t.Spalte.AddThemeConstantOverride("separation", 4);
        zeile.AddChild(t.Spalte);
        if (!string.IsNullOrEmpty(titel))
        {
            var kopf = new Label { Text = titel };
            kopf.AddThemeFontSizeOverride("font_size", 20); // ab 20 nimmt Schriften.cs die Überschriftschrift
            kopf.AddThemeColorOverride("font_color", Gold);
            t.Spalte.AddChild(kopf);
        }

        var bbcode = Aufbereiten(roh, out var schlicht);
        t.Text = new RichTextLabel
        {
            BbcodeEnabled = true, FitContent = true, ScrollActive = false, SelectionEnabled = false, ContextMenuEnabled = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Pass,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        t.Text.AddThemeFontOverride("normal_font", Schriften.Text);
        t.Text.AddThemeFontSizeOverride("normal_font_size", Schriftgroesse);
        t.Text.AddThemeColorOverride("default_color", Hell);
        var breite = Schriften.Text.GetMultilineStringSize(schlicht, HorizontalAlignment.Left, MaxBreite, Schriftgroesse).X;
        t.Text.CustomMinimumSize = new Vector2(Math.Min(MaxBreite, MathF.Ceiling(breite) + 6), 0);
        t.Text.Text = bbcode;
        t.Text.MetaHoverStarted += meta => { t.VerweisUnterMaus = meta.AsString(); t.VerweisSeit = 0; };
        t.Text.MetaHoverEnded += _ => t.VerweisUnterMaus = null;
        t.Text.MetaClicked += meta => BegriffOeffnen(t, meta.AsString(), GetViewport().GetMousePosition(), fest: true);
        t.Spalte.AddChild(t.Text);

        // Rechts oben: der Füllkreis, nach dem Festheften das ✕
        t.Rechts = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        zeile.AddChild(t.Rechts);
        t.Kreis = new Control { CustomMinimumSize = new Vector2(18, 18), MouseFilter = Control.MouseFilterEnum.Ignore };
        t.Kreis.Draw += () => KreisZeichnen(t);
        t.Rechts.AddChild(t.Kreis);
        t.Kreis.Visible = !Fenster.TooltipNurMitTaste;
        if (Fenster.TooltipNurMitTaste) t.Rechts.CustomMinimumSize = new Vector2(0, 0);

        t.Tafel.GuiInput += e => ZiehenBeginnen(t, e);
        t.Text.GuiInput += e => ZiehenBeginnen(t, e);
        _ebene.AddChild(t.Tafel);
        _tipps.Add(t);
        return t;
    }

    private void KreisZeichnen(Tipp t)
    {
        var mitte = t.Kreis.Size / 2;
        var radius = Math.Min(mitte.X, mitte.Y) - 2;
        var anteil = Math.Clamp(t.Fuellung / Fenster.TooltipFestZeit, 0, 1);
        if (anteil > 0)
        {
            var punkte = new List<Vector2> { mitte };
            var schritte = Math.Max(2, (int)(32 * anteil));
            for (var i = 0; i <= schritte; i++)
            {
                var winkel = -Mathf.Pi / 2 + (float)anteil * Mathf.Tau * i / schritte;
                punkte.Add(mitte + new Vector2(Mathf.Cos(winkel), Mathf.Sin(winkel)) * radius);
            }
            t.Kreis.DrawColoredPolygon(punkte.ToArray(), new Color(Gold, 0.85f));
        }
        t.Kreis.DrawArc(mitte, radius, 0, Mathf.Tau, 28, new Color(Gold, 0.9f), 1.5f, true);
    }

    private static StyleBoxFlat Stil(bool fest) => new()
    {
        BgColor = new Color(0.12f, 0.11f, 0.10f, 0.97f),
        BorderColor = fest ? Gold : Gold * new Color(1, 1, 1, 0.7f),
        BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
        CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
        ShadowColor = new Color(0, 0, 0, 0.5f), ShadowSize = fest ? 10 : 6,
        ContentMarginLeft = 12, ContentMarginRight = 10, ContentMarginTop = 9, ContentMarginBottom = 10,
    };

    private void Festmachen(Tipp t)
    {
        if (t.Fest) return;
        t.Fest = true;
        t.Tafel.AddThemeStyleboxOverride("panel", Stil(true));
        t.Kreis.QueueFree();
        t.Rechts.CustomMinimumSize = Vector2.Zero;
        var zu = Kreuz.Knopf(Kreuz.Art.Kreuz, "Schließen (Esc)");
        zu.Pressed += () => Entfernen(t, auchFeste: true);
        t.Rechts.AddChild(zu);
        if (t.Eltern is null) _unterdrueckt = true; // sonst käme für dieselbe Quelle gleich der nächste lose Tooltip
        if (t.Kennung is { } kennung && LexikonOeffnen is { } oeffnen)
        {
            var lexikon = new Button { Text = "Im Lexikon", FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
            lexikon.AddThemeFontSizeOverride("font_size", 14);
            lexikon.Pressed += () => oeffnen(kennung);
            t.Spalte.AddChild(lexikon);
        }
        _ebene.MoveChild(t.Tafel, -1);
    }

    private void ZiehenBeginnen(Tipp t, InputEvent e)
    {
        if (e is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return;
        _ebene.MoveChild(t.Tafel, -1);
        if (!t.Fest) return;
        t.Gezogen = true;
        t.Griff = GetViewport().GetMousePosition() - t.Tafel.GlobalPosition;
    }

    private void Entfernen(Tipp t, bool auchFeste = false)
    {
        if (!_tipps.Remove(t)) return;
        foreach (var kind in t.Kinder.ToArray())
        {
            if (kind.Fest && !auchFeste) kind.Eltern = null; // ein festes Fenster bleibt, auch wenn sein Ursprung verschwindet
            else Entfernen(kind, auchFeste);
        }
        t.Eltern?.Kinder.Remove(t);
        if (IsInstanceValid(t.Tafel)) t.Tafel.QueueFree();
    }

    // ── Hilfen ──────────────────────────────────────────────────────────────

    /// <summary>Neben der Maus; passt es nicht ins Bild, nach links bzw. über die Maus.</summary>
    private static Vector2 Lage(Vector2 anker, Vector2 groesse, Vector2 sichtbar)
    {
        var p = anker + new Vector2(14, 22);
        if (p.X + groesse.X > sichtbar.X - 4) p.X = Math.Max(4, sichtbar.X - groesse.X - 4);
        if (p.Y + groesse.Y > sichtbar.Y - 4) p.Y = Math.Max(4, anker.Y - groesse.Y - 12);
        return p;
    }

    private static Vector2 Klemmen(Vector2 p, Vector2 groesse, Vector2 sichtbar) =>
        new(Math.Clamp(p.X, 0, Math.Max(0, sichtbar.X - groesse.X)), Math.Clamp(p.Y, 0, Math.Max(0, sichtbar.Y - groesse.Y)));

    /// <summary>
    /// Wandelt den Text in BBCode: <c>[[kennung|Text]]</c> wird ein unterstrichener Verweis (nur wenn das Lexikon die Kennung kennt), alle anderen
    /// eckigen Klammern werden entschärft. <paramref name="schlicht"/> ist der Text ohne Auszeichnung (zum Messen der Breite).
    /// </summary>
    internal static string Aufbereiten(string roh, out string schlicht)
    {
        var bb = new StringBuilder();
        var pl = new StringBuilder();
        var i = 0;
        while (i < roh.Length)
        {
            var anfang = roh.IndexOf("[[", i, StringComparison.Ordinal);
            var ende = anfang < 0 ? -1 : roh.IndexOf("]]", anfang + 2, StringComparison.Ordinal);
            if (anfang < 0 || ende < 0)
            {
                Anhaengen(bb, pl, roh[i..]);
                break;
            }
            Anhaengen(bb, pl, roh[i..anfang]);
            var inhalt = roh[(anfang + 2)..ende];
            var trenner = inhalt.IndexOf('|');
            var kennung = (trenner < 0 ? inhalt : inhalt[..trenner]).Trim();
            var anzeige = trenner < 0 ? kennung : inhalt[(trenner + 1)..];
            if (kennung != "" && !kennung.Contains('[') && !kennung.Contains(']') && Suchen(kennung) is not null)
            {
                bb.Append("[color=#f0d070][url=").Append(kennung).Append(']').Append(Entschaerfen(anzeige)).Append("[/url][/color]");
                pl.Append(anzeige);
            }
            else Anhaengen(bb, pl, anzeige);
            i = ende + 2;
        }
        schlicht = pl.ToString();
        return bb.ToString();
    }

    private static void Anhaengen(StringBuilder bb, StringBuilder pl, string text)
    {
        bb.Append(Entschaerfen(text));
        pl.Append(text);
    }

    private static string Entschaerfen(string text) => text.Replace("[", "[lb]");
}
