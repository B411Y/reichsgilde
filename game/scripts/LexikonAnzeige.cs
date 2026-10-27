using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Tafel des <b>Lexikons</b> (Taste <b>L</b>, Knopf „Lexikon“ in der Betriebsleiste): in der Bildmitte links ein Suchfeld mit der Liste der Begriffe nach
/// Gruppen, rechts der Eintrag mit anklickbaren Verweisen und Live-Zahlen, oben „Zurück“ (Verlauf der angeklickten Begriffe). Die Texte kommen aus
/// <see cref="Lexikon"/>; die Tafel liest nur. Die Live-Zahlen werden zweimal je Sekunde aufgefrischt, solange sie offen ist.
/// </summary>
public partial class LexikonAnzeige : CenterContainer
{
    private const int Breite = 920, Hoehe = 520, ListeBreite = 270;
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.75f, 0.75f, 0.75f);

    private LineEdit _suche = null!;
    private Tree _baum = null!;
    private Label _titel = null!;
    private Label _gruppe = null!;
    private RichTextLabel _text = null!;
    private Button _zurueck = null!;
    private readonly Dictionary<string, TreeItem> _eintraege = new();
    private readonly List<string> _verlauf = new();
    private string? _aktuell;
    private string _gezeigt = "";
    private string _baumStand = "\0";
    private bool _baumMeldet = true;
    private double _seitAufbau;

    /// <summary>Die Tafel selbst (für die Prüfung, ob sie offen ist und die Maus darüber liegt).</summary>
    public PanelContainer Tafel { get; private set; } = null!;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var (tafel, spalte) = Hauptmenue.Tafel(Breite);
        Tafel = tafel;
        AddChild(tafel);
        Lexikon.AnzeigeOeffnen = Oeffnen;

        var kopf = new HBoxContainer();
        spalte.AddChild(kopf);
        var titel = Hauptmenue.Text(kopf, "Lexikon", 24, Gold);
        titel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _zurueck = new Button { Text = "◀ Zurück", FocusMode = FocusModeEnum.None, TooltipText = "Zum vorigen Eintrag" };
        _zurueck.Pressed += Zurueck;
        kopf.AddChild(_zurueck);
        var zu = new Button { Text = "×", FocusMode = FocusModeEnum.None, TooltipText = "Schließen (Esc)" };
        zu.Pressed += Schliessen;
        kopf.AddChild(zu);

        var reihe = new HBoxContainer();
        reihe.AddThemeConstantOverride("separation", 16);
        spalte.AddChild(reihe);

        var links = new VBoxContainer { CustomMinimumSize = new Vector2(ListeBreite, 0) };
        links.AddThemeConstantOverride("separation", 6);
        reihe.AddChild(links);
        _suche = new LineEdit { PlaceholderText = "Suchen …", ClearButtonEnabled = true };
        _suche.AddThemeFontSizeOverride("font_size", 15);
        _suche.TextChanged += _ => BaumBauen();
        _suche.GuiInput += eingabe =>
        {
            if (eingabe is InputEventKey { Pressed: true, Keycode: Key.Escape })
            {
                Schliessen();
                AcceptEvent();
            }
        };
        links.AddChild(_suche);
        _baum = new Tree
        {
            HideRoot = true, SelectMode = Tree.SelectModeEnum.Single, FocusMode = FocusModeEnum.Click,
            CustomMinimumSize = new Vector2(ListeBreite, Hoehe - 40), SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _baum.AddThemeFontSizeOverride("font_size", 14);
        _baum.ItemSelected += BaumGewaehlt;
        links.AddChild(_baum);

        var rechts = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rechts.AddThemeConstantOverride("separation", 4);
        reihe.AddChild(rechts);
        _titel = Hauptmenue.Text(rechts, "", 22, Gold, umbruchBreite: Breite - ListeBreite - 100);
        _gruppe = Hauptmenue.Text(rechts, "", 13, Grau);
        _text = new RichTextLabel
        {
            BbcodeEnabled = true, FitContent = false, ScrollActive = true, SelectionEnabled = true,
            CustomMinimumSize = new Vector2(Breite - ListeBreite - 100, Hoehe - 70), SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _text.AddThemeFontSizeOverride("normal_font_size", 16);
        _text.AddThemeFontSizeOverride("bold_font_size", 16);
        _text.MetaClicked += meta => Zeigen(meta.AsString());
        rechts.AddChild(_text);

        Hide();
    }

    public override void _ExitTree()
    {
        if (Lexikon.AnzeigeOeffnen == Oeffnen) Lexikon.AnzeigeOeffnen = null;
    }

    /// <summary>Öffnet das Lexikon (beim Begriff dieser Kennung; leer oder unbekannt: bei der Startseite bzw. dem zuletzt gezeigten Begriff).</summary>
    public void Oeffnen(string kennung = "")
    {
        Show();
        _baumStand = "\0";
        BaumBauen();
        if (Lexikon.FindeBegriff(kennung) is not null) Zeigen(kennung);
        else if (_aktuell is not null) Zeigen(_aktuell, merken: false);
        else Startseite();
        _suche.CallDeferred(Control.MethodName.GrabFocus);
    }

    public void Schliessen() => Hide();

    /// <summary>Trägt einen Suchtext ein (Startoption <c>--lexikon=suche:&lt;text&gt;</c>).</summary>
    public void Suchen(string text)
    {
        _suche.Text = text;
        BaumBauen();
    }

    /// <summary>Zeigt die Startseite (noch kein Begriff gewählt).</summary>
    private void Startseite()
    {
        _aktuell = null;
        _titel.Text = "Lexikon";
        _gruppe.Text = Lexikon.Fehler ?? $"{Lexikon.Alle.Count} Begriffe";
        _gezeigt = "";
        _text.Text = "Hier steht, wie das Spiel funktioniert. Links nach Gruppen stöbern oder oben suchen; goldene Wörter im Text führen zu weiteren Einträgen, " +
                     "und die Zahlen darin sind die, die gerade im Spiel gelten.\n\nTipp: [b]L[/b] öffnet und schließt das Lexikon, [b]Esc[/b] schließt es.";
        _zurueck.Disabled = _verlauf.Count == 0;
    }

    /// <summary>Zeigt einen Begriff. <paramref name="merken"/>: den vorigen in den Verlauf legen (für „Zurück“).</summary>
    private void Zeigen(string kennung, bool merken = true)
    {
        if (Lexikon.FindeBegriff(kennung) is not { } b) return;
        if (merken && _aktuell is not null && _aktuell != b.Kennung) _verlauf.Add(_aktuell);
        _aktuell = b.Kennung;
        _titel.Text = b.Titel;
        _gruppe.Text = b.Gruppe;
        _gezeigt = "";
        _zurueck.Disabled = _verlauf.Count == 0;
        if (_eintraege.TryGetValue(b.Kennung, out var zeile))
        {
            _baumMeldet = false;
            zeile.GetParent()?.SetCollapsed(false);
            zeile.Select(0);
            _baum.ScrollToItem(zeile);
            _baumMeldet = true;
        }
        TextAktualisieren();
        _text.ScrollToLine(0);
    }

    private void Zurueck()
    {
        if (_verlauf.Count == 0) return;
        var vorige = _verlauf[^1];
        _verlauf.RemoveAt(_verlauf.Count - 1);
        Zeigen(vorige, merken: false);
    }

    private void BaumGewaehlt()
    {
        if (!_baumMeldet || _baum.GetSelected() is not { } zeile) return;
        if (zeile.GetMetadata(0).AsString() is { Length: > 0 } kennung) Zeigen(kennung);
        else zeile.Collapsed = !zeile.Collapsed; // eine Gruppe: auf- und zuklappen
    }

    /// <summary>Baut die Liste neu: ohne Suchtext nach Gruppen, mit Suchtext die Treffer.</summary>
    private void BaumBauen()
    {
        var suchtext = _suche.Text.Trim();
        if (suchtext == _baumStand) return;
        _baumStand = suchtext;
        _baumMeldet = false;
        _baum.Clear();
        _eintraege.Clear();
        var wurzel = _baum.CreateItem();
        if (suchtext == "")
        {
            foreach (var gruppe in Lexikon.Gruppen)
            {
                var kopf = _baum.CreateItem(wurzel);
                kopf.SetText(0, gruppe);
                kopf.SetCustomColor(0, Gold);
                kopf.SetMetadata(0, "");
                kopf.Collapsed = true;
                foreach (var b in Lexikon.Alle.Where(b => b.Gruppe == gruppe).OrderBy(b => b.Titel, StringComparer.CurrentCultureIgnoreCase))
                    EintragZeile(kopf, b);
            }
        }
        else
        {
            foreach (var b in Lexikon.Suche(suchtext)) EintragZeile(wurzel, b);
        }
        _baumMeldet = true;
        if (_aktuell is not null && _eintraege.TryGetValue(_aktuell, out var zeile))
        {
            _baumMeldet = false;
            zeile.GetParent()?.SetCollapsed(false);
            zeile.Select(0);
            _baumMeldet = true;
        }
    }

    private void EintragZeile(TreeItem eltern, Begriff b)
    {
        var zeile = _baum.CreateItem(eltern);
        zeile.SetText(0, b.Titel);
        zeile.SetMetadata(0, b.Kennung);
        _eintraege[b.Kennung] = zeile;
    }

    /// <summary>Setzt den Text des gezeigten Begriffs neu zusammen (Live-Zahlen) und lässt ihn nur ändern, wenn er anders ist.</summary>
    private void TextAktualisieren()
    {
        if (_aktuell is null || Lexikon.FindeBegriff(_aktuell) is not { } b) return;
        var neu = Lexikon.Aufloesen(b.Text, mitVerweisen: true);
        var erwaehnt = Lexikon.Alle.Where(o => o.Kennung != b.Kennung && ErwaehntBegriff(o.Text, b.Kennung))
            .OrderBy(o => o.Titel, StringComparer.CurrentCultureIgnoreCase).ToList();
        if (erwaehnt.Count > 0)
            neu += "\n\n[color=#bfbfbf]Erwähnt in: [/color]" + string.Join(" · ", erwaehnt.Select(o => $"[url={o.Kennung}][color=#e8c872]{o.Titel}[/color][/url]"));
        if (neu == _gezeigt) return;
        _gezeigt = neu;
        var stelle = _text.GetVScrollBar().Value;
        _text.Text = neu;
        _text.GetVScrollBar().Value = stelle;
    }

    private static bool ErwaehntBegriff(string text, string kennung)
        => text.Contains($"[[{kennung}]]") || text.Contains($"[[{kennung}|");

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _seitAufbau += delta;
        if (_seitAufbau < 0.5) return;
        _seitAufbau = 0;
        TextAktualisieren();
    }
}
