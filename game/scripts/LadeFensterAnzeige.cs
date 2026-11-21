using Godot;

namespace Reichsgilde;

/// <summary>Eine Zeile Kurzwert im Lade-Fenster: links der Name, rechts der Wert; <paramref name="Warnung"/> setzt beide in Warnfarbe mit Zeichen.</summary>
public sealed record LadeZeile(string Name, string Wert, bool Warnung = false);

/// <summary>
/// Ein Befehl im Lade-Fenster: Titel, rechts die Kosten (Gulden, AP), darunter die Wirkung. Mit <paramref name="Sperre"/> ist er gesperrt und nennt
/// Grund und nächsten Schritt; ohne <paramref name="Aktion"/> ist er nur Auskunft.
/// </summary>
public sealed record LadeBefehl(string Titel, string Kosten = "", string Folge = "", string? Sperre = null, Action? Aktion = null);

/// <summary>Was ein Lade-Fenster zeigt: Titel im Kopf, eine Zeile Einleitung, Kurzwerte, Befehle.</summary>
public sealed record LadeInhalt(string Titel, string Vor, IReadOnlyList<LadeZeile> Zeilen, IReadOnlyList<LadeBefehl> Befehle);

/// <summary>
/// Das <b>Lade-Fenster</b> eines Fachs (UI-Umbau „Zunftlade“, Teil 3; Entwurf 3): ein Fenster fester Größe über der Lade mit Kurzwerten und Befehlen –
/// Kosten in Gulden und AP, Wirkung, gesperrte Befehle mit Grund und nächstem Schritt. Es gibt nur <b>eines</b>; jedes Fach füllt es mit seinem
/// Inhalt (<see cref="Zeigen"/>), ein zweiter Klick auf dasselbe Fach schließt es.
///
/// Es rechnet nichts selbst: Der Inhalt kommt als <see cref="LadeInhalt"/> aus <c>Hauptszene.Faecher.cs</c> (dort wird der Kern gelesen), viermal in
/// der Sekunde neu; gebaut wird nur, wenn sich etwas geändert hat (ein Knopf unter der Maus bleibt stehen). Aussehen und Maße stehen im Thema der
/// Fenster (<c>fenster_thema.tres</c>: „LadeFensterMasse“, „FensterVor“, „FensterZeile…“, „FensterBefehl…“).
/// </summary>
public partial class LadeFensterAnzeige : PanelContainer
{
    private readonly VBoxContainer _spalte;
    private Fensterkleid _kleid = null!;
    private Func<LadeInhalt?>? _quelle;
    private string _bau = "";   // Gerüst des Inhalts (Titel, Zeilennamen, Befehle): ändert es sich nicht, wird nichts neu gebaut
    private double _seit;
    private Label? _vor;
    private readonly List<Label> _werte = new();
    private readonly List<LadeBefehlKnopf> _knoepfe = new();

    /// <summary>Welches Fach gerade offen ist (null: keines) – die Lade drückt danach ihr Fach ein.</summary>
    public string? Fach { get; private set; }

    public LadeFensterAnzeige()
    {
        Name = "LadeFenster";
        Visible = false;
        var rand = new MarginContainer { ThemeTypeVariation = "LadeFensterLeib" };
        AddChild(rand);
        _spalte = new VBoxContainer { ThemeTypeVariation = "LadeFensterSpalte", SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        rand.AddChild(_spalte);
    }

    public override void _Ready()
    {
        Rollbereich.Einhuellen(this);
        var rahmen = Fensterrahmen.Anmelden(this, "LadeFenster", Schliessen);
        _kleid = rahmen.Kleid!;
        _kleid.Titel = "";
        GroesseSetzen();
        GetViewport().SizeChanged += GroesseSetzen;
        VisibilityChanged += () =>
        {
            if (!Visible) Fach = null;
        };
    }

    /// <summary>Feste Größe wie im Entwurf (1280 × 530), schmaler bei schmalem Bild; der Inhalt rollt, wenn er höher ist.</summary>
    private void GroesseSetzen()
    {
        if (!IsInsideTree()) return;
        var bild = GetViewportRect().Size;
        var stil = FensterStil.Thema.GetStylebox("panel", "FensterTafelKopf").GetMinimumSize();
        var breite = Mathf.Min(FensterStil.Mass("breite", "LadeFensterMasse"), bild.X) - stil.X;
        var hoehe = FensterStil.Mass("hoehe", "LadeFensterMasse") - stil.Y;
        GetNode<Rollbereich>("Rollbereich").GetChild<Control>(0).CustomMinimumSize = new Vector2(breite, hoehe);
        _spalte.CustomMinimumSize = new Vector2(Mathf.Min(FensterStil.Mass("inhalt_breite", "LadeFensterMasse"), breite), 0);
    }

    /// <summary>Öffnet das Fenster für ein Fach; derselbe Aufruf noch einmal schließt es.</summary>
    public void Zeigen(string fach, Func<LadeInhalt?> quelle)
    {
        if (Visible && Fach == fach)
        {
            Schliessen();
            return;
        }
        _quelle = quelle;
        _bau = "";
        _seit = 1;
        Fach = fach;
        if (Visible) Aufbauen();
        else Show();
        Fach = fach;
    }

    public void Schliessen()
    {
        Fach = null;
        Hide();
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _seit += delta;
        if (_seit < 0.25) return;
        _seit = 0;
        Aufbauen();
    }

    private void Aufbauen()
    {
        if (_quelle?.Invoke() is not { } inhalt)
        {
            Schliessen();
            return;
        }
        // Neu gebaut wird nur, wenn sich das Gerüst ändert. Sonst nur die Texte tauschen: Ein Neubau je Zahlenwechsel (der Rat zählt die Zeit bis zur
        // Sitzung mit) ließ das Fenster flackern – Texte mit Zeilenumbruch messen im neuen Knoten kurz mit Breite 0 und das Fenster sprang auf die
        // dreifache Höhe –, und ein offener Knopf oder eine Auswahl darin wäre jedes Mal weg gewesen.
        var bau = inhalt.Titel + "\n" + (inhalt.Vor.Length > 0) + "\n" + string.Join("\n", inhalt.Zeilen.Select(z => $"{z.Name}|{z.Warnung}"))
                  + "\n" + string.Join("\n", inhalt.Befehle.Select(b => $"{b.Titel}|{b.Kosten.Length > 0}|{b.Sperre is not null}|{b.Folge.Length > 0}|{b.Aktion is null}"));
        if (bau == _bau)
        {
            if (_vor is not null) _vor.Text = inhalt.Vor;
            for (var i = 0; i < _werte.Count; i++) if (_werte[i].Text != inhalt.Zeilen[i].Wert) _werte[i].Text = inhalt.Zeilen[i].Wert;
            for (var i = 0; i < _knoepfe.Count; i++) _knoepfe[i].Auffrischen(inhalt.Befehle[i]);
            return;
        }
        _bau = bau;
        _kleid.Titel = inhalt.Titel;
        UiHilfe.KinderEntfernen(_spalte);
        _vor = null;
        _werte.Clear();
        _knoepfe.Clear();
        if (inhalt.Vor.Length > 0)
        {
            _vor = new Label { Text = inhalt.Vor, ThemeTypeVariation = "FensterVor", AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _spalte.AddChild(_vor);
        }
        if (inhalt.Zeilen.Count > 0)
        {
            var zeilen = new VBoxContainer { ThemeTypeVariation = "FensterZeilen" };
            _spalte.AddChild(zeilen);
            foreach (var z in inhalt.Zeilen)
            {
                var kasten = Zeile(z);
                zeilen.AddChild(kasten);
                _werte.Add((Label)kasten.GetChild(0).GetChild(kasten.GetChild(0).GetChildCount() - 1));
            }
        }
        if (inhalt.Befehle.Count > 0)
        {
            var befehle = new VBoxContainer { ThemeTypeVariation = "FensterBefehle" };
            _spalte.AddChild(befehle);
            foreach (var b in inhalt.Befehle)
            {
                var knopf = new LadeBefehlKnopf(b);
                befehle.AddChild(knopf);
                _knoepfe.Add(knopf);
            }
        }
    }

    private static Control Zeile(LadeZeile z)
    {
        var kasten = new PanelContainer { ThemeTypeVariation = "FensterZeile" };
        kasten.SetMeta(FensterStil.Stoff, FensterStil.Pergament); // der Strich darunter kommt aus dem Thema, nicht aus der Umrechnung
        var reihe = new HBoxContainer { ThemeTypeVariation = "FensterZeileReihe" };
        kasten.AddChild(reihe);
        if (z.Warnung) reihe.AddChild(LadeBefehlKnopf.Zeichen("gefahr", "FensterZeichenWarnung"));
        reihe.AddChild(new Label { Text = z.Name, ThemeTypeVariation = z.Warnung ? "FensterZeileWarnung" : "FensterZeileName", SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true });
        reihe.AddChild(new Label { Text = z.Wert, ThemeTypeVariation = z.Warnung ? "FensterZeileWarnung" : "FensterZeileWert" });
        return kasten;
    }
}

/// <summary>
/// Ein Befehl als Knopf aus Holz: oben Titel und Kosten, darunter Wirkung oder – gesperrt – das Schloss mit Grund und nächstem Schritt.
/// Die Kinder fangen keine Maus; die Höhe folgt dem Inhalt.
/// </summary>
public partial class LadeBefehlKnopf : Button
{
    private readonly MarginContainer _rand;
    private readonly Label _titel;
    private Label? _kosten, _text; // Kosten rechts; Grund (gesperrt) oder Wirkung
    private Action? _aktion;       // die jeweils neueste Aktion: der Knopf bleibt, die Aktion wird nur getauscht

    public LadeBefehlKnopf(LadeBefehl b)
    {
        var gesperrt = b.Sperre is not null;
        ThemeTypeVariation = gesperrt ? "FensterBefehlGesperrt" : "FensterBefehl";
        FocusMode = FocusModeEnum.None;
        Disabled = gesperrt || b.Aktion is null; // ohne Aktion: Auskunft – bleibt Holz (das Thema setzt „disabled“ wie „normal“)
        _aktion = b.Aktion;
        if (b.Aktion is not null && !gesperrt)
        {
            Pressed += () => _aktion?.Invoke();
            MouseDefaultCursorShape = CursorShape.PointingHand;
        }
        _rand = new MarginContainer { ThemeTypeVariation = "FensterBefehlInhalt", MouseFilter = MouseFilterEnum.Ignore };
        _rand.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_rand);
        var spalte = new VBoxContainer { ThemeTypeVariation = "FensterBefehlSpalte", MouseFilter = MouseFilterEnum.Ignore };
        _rand.AddChild(spalte);
        var kopf = new HBoxContainer { ThemeTypeVariation = "FensterZeileReihe", MouseFilter = MouseFilterEnum.Ignore };
        spalte.AddChild(kopf);
        _titel = new Label { Text = b.Titel, ThemeTypeVariation = "FensterBefehlTitel", SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        kopf.AddChild(_titel);
        if (b.Kosten.Length > 0)
        {
            _kosten = new Label { Text = b.Kosten, ThemeTypeVariation = "FensterBefehlKosten", MouseFilter = MouseFilterEnum.Ignore };
            kopf.AddChild(_kosten);
        }
        if (gesperrt)
        {
            var sperre = new HBoxContainer { ThemeTypeVariation = "FensterZeileReihe", MouseFilter = MouseFilterEnum.Ignore };
            spalte.AddChild(sperre);
            var schloss = Zeichen("schloss", "FensterZeichenSperre");
            schloss.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            sperre.AddChild(schloss);
            _text = new Label
            {
                Text = b.Sperre, ThemeTypeVariation = "FensterBefehlSperre", AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore,
            };
            sperre.AddChild(_text);
        }
        else if (b.Folge.Length > 0)
        {
            _text = new Label
            {
                Text = b.Folge, ThemeTypeVariation = "FensterBefehlFolge", AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore,
            };
            spalte.AddChild(_text);
        }
        _rand.MinimumSizeChanged += HoeheAnpassen;
        Resized += HoeheAnpassen; // umbrochener Text braucht je nach Breite mehr oder weniger Zeilen
    }

    public override void _Ready() => HoeheAnpassen();

    /// <summary>Gleiches Gerüst, neue Werte: Kosten, Wirkung oder Grund und die Aktion tauschen, ohne den Knopf neu zu bauen.</summary>
    public void Auffrischen(LadeBefehl b)
    {
        _aktion = b.Aktion;
        if (_titel.Text != b.Titel) _titel.Text = b.Titel;
        if (_kosten is not null && _kosten.Text != b.Kosten) _kosten.Text = b.Kosten;
        var text = b.Sperre ?? b.Folge;
        if (_text is not null && _text.Text != text) _text.Text = text;
    }

    /// <summary>Die Höhe folgt dem Inhalt (ein Knopf von Godot misst nur seinen eigenen Text, eine eigene Mindestgröße per Skript beachtet er nicht).</summary>
    private void HoeheAnpassen()
    {
        var hoehe = _rand.GetCombinedMinimumSize().Y;
        if (!Mathf.IsEqualApprox(CustomMinimumSize.Y, hoehe)) CustomMinimumSize = new Vector2(0, hoehe);
    }

    /// <summary>Ein Zeichen des Themas der Fenster (Familie „FensterZeichen“) in der Größe und Farbe der Variante.</summary>
    public static TextureRect Zeichen(string name, string variante)
    {
        var groesse = FensterStil.Mass("groesse", variante);
        return new TextureRect
        {
            Texture = FensterStil.Thema.GetIcon(name, "FensterZeichen"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(groesse, groesse),
            Modulate = FensterStil.Farbe("farbe", variante), MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
    }
}
