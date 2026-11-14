using Godot;

namespace Reichsgilde;

/// <summary>
/// Die zentrale Einblendung neuer Meldungen: Jede Meldung, die als <b>Siegel</b> oder <b>Brief</b> kommt (die Stufe aus den Einstellungen „Meldungen“),
/// erscheint zuerst groß mittig am unteren Bildrand über der Leiste – Siegel ihres Themas, Art und Text auf Pergament –, steht eine Weile und wandert dann
/// verkleinert in die Siegel-Ecke; erst dort erscheint das Siegel bzw. der Brief (<c>ankunft</c>). Meldungen der Stufe „Verlauf“ bekommen keine Einblendung.
/// Größe und Dauer richten sich nach der Stufe (Brief größer und länger als Siegel). „Siegel und Briefe bewegen“ aus: kein Einblenden und kein Wandern,
/// die Tafel steht und verschwindet.
///
/// Aussehen und Zeiten stehen nur im Thema der Meldungen (<c>res://scenes/meldung_thema.tres</c>, Varianten <c>MeldungEinblendungSiegel…</c>/<c>…Brief…</c>,
/// Maße <c>einblendung_…</c>); dieser Code kennt nur ihre Namen. Mehrere Meldungen kommen nacheinander; stehen schon zu viele an
/// (<c>einblendung_hoechstens_wartend</c>), geht die nächste ohne Einblendung gleich in die Ecke. Solange große Fenster oder ein Brief offen sind (<see cref="Ruht"/>),
/// ist die Einblendung unsichtbar, eine laufende hält an und geht danach weiter (Logik ohne Godot: <see cref="Einblendungsreihe{T}"/>).
/// </summary>
public sealed partial class EinblendungAnzeige : Control
{
    private sealed record Eintrag(string Art, string Text, Meldethema Thema, Meldestufe Stufe, Func<Vector2?> Ziel, Action Ankunft);

    private readonly Einblendungsreihe<Eintrag> _reihe = new();
    /// <summary>Der Tween der laufenden Einblendung (Einblenden/Stehen bzw. Wandern) – angehalten, solange etwas ruht.</summary>
    private Tween? _tween;

    /// <summary>Abstand über der Betriebsleiste (z. B. 40 über der Raumleiste), setzt die Hauptszene jedes Bild.</summary>
    public float AbstandUnten { get; set; }

    /// <summary>
    /// Ein großes Fenster oder ein Brief ist offen (setzt die Hauptszene jedes Bild): Die Einblendung ist unsichtbar, eine laufende hält an und geht
    /// danach weiter, keine neue beginnt (<see cref="Einblendungsreihe{T}"/>).
    /// </summary>
    public bool Ruht
    {
        get => _reihe.Ruht;
        set
        {
            if (!_reihe.RuhtSetzen(value)) return;
            Visible = _reihe.Sichtbar;
            if (value) _tween?.Pause();
            else _tween?.Play();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Theme = MeldungStil.Thema;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    /// <summary>
    /// Reiht eine Meldung ein. <paramref name="ziel"/> liefert beim Wandern den Punkt in der Ecke (null: am Platz verschwinden),
    /// <paramref name="ankunft"/> läuft, wenn sie dort ist (Siegel zeigen, Brief einreihen).
    /// </summary>
    public void Einblenden(string art, string text, Meldethema thema, Meldestufe stufe, Func<Vector2?> ziel, Action ankunft)
    {
        if (stufe < Meldestufe.Siegel || !_reihe.Einreihen(new Eintrag(art, text, thema, stufe, ziel, ankunft), MeldungStil.Mass("einblendung_hoechstens_wartend")))
        {
            ankunft(); // Verlauf oder Flut: ohne Einblendung
            return;
        }
        Naechste();
    }

    public override void _Process(double delta) => Naechste();

    private static double Sekunden(string mass) => MeldungStil.Mass(mass) / 1000.0;

    /// <summary>Beginnt die nächste Meldung, wenn keine läuft und nichts ruht.</summary>
    private async void Naechste()
    {
        if (_reihe.Beginnen() is not { } e) return;
        var stufe = e.Stufe == Meldestufe.Brief ? "Brief" : "Siegel";
        var klein = stufe.ToLowerInvariant();
        var tafel = new PanelContainer { ThemeTypeVariation = "MeldungEinblendung" + stufe, MouseFilter = MouseFilterEnum.Ignore, Modulate = Colors.Transparent };
        var reihe = new HBoxContainer { ThemeTypeVariation = "MeldungEinblendungReihe", MouseFilter = MouseFilterEnum.Ignore };
        tafel.AddChild(reihe);
        var siegelGroesse = MeldungStil.Mass("einblendung_siegel_" + klein);
        reihe.AddChild(new Siegelbild
        {
            Farbe = Color.FromHtml(e.Thema.Farbe), ThemaId = e.Thema.Id, CustomMinimumSize = new Vector2(siegelGroesse, siegelGroesse),
            SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore,
        });
        var spalte = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        reihe.AddChild(spalte);
        spalte.AddChild(new Label { Text = e.Art, ThemeTypeVariation = "MeldungEinblendungArt" + stufe, MouseFilter = MouseFilterEnum.Ignore });
        spalte.AddChild(new Label
        {
            Text = e.Text, ThemeTypeVariation = "MeldungEinblendungText" + stufe, MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, MaxLinesVisible = MeldungStil.Mass("einblendung_zeilen"),
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            CustomMinimumSize = new Vector2(MeldungStil.Mass("einblendung_breite_" + klein), 0),
        });
        AddChild(tafel);

        // Erst nach einem Bild steht die Größe fest (Umbruch); dann mittig unten über der Leiste
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!IsInstanceValid(tafel)) return;
        tafel.ResetSize();
        tafel.PivotOffset = tafel.Size / 2;
        tafel.Position = new Vector2((Size.X - tafel.Size.X) / 2,
                                     Size.Y - Rollbereich.LeistenHoehe - AbstandUnten - MeldungStil.Mass("einblendung_abstand_unten") - tafel.Size.Y);

        var bewegt = Meldungseinstellungen.Bewegung;
        var tw = Merken(tafel.CreateTween());
        if (bewegt) tw.TweenProperty(tafel, "modulate:a", 1.0, Sekunden("einblendung_einblenden_ms"));
        else tafel.Modulate = Colors.White;
        tw.TweenInterval(Sekunden("einblendung_stehen_" + klein + "_ms"));
        tw.TweenCallback(Callable.From(() => Wandern(tafel, e, bewegt)));
    }

    /// <summary>Merkt sich den Tween der laufenden Einblendung; ruht gerade etwas, beginnt er angehalten.</summary>
    private Tween Merken(Tween tw)
    {
        _tween = tw;
        if (Ruht) tw.Pause();
        return tw;
    }

    /// <summary>Verkleinert in die Ecke (bzw. ohne Bewegung: weg), dann Ankunft und die nächste Meldung.</summary>
    private void Wandern(PanelContainer tafel, Eintrag e, bool bewegt)
    {
        if (!bewegt || e.Ziel() is not { } ziel)
        {
            Fertig(tafel, e);
            return;
        }
        var dauer = Sekunden("einblendung_wandern_ms");
        var massstab = MeldungStil.Mass("einblendung_endgroesse_prozent") / 100f;
        var tw = Merken(tafel.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In));
        tw.TweenProperty(tafel, "position", ziel - tafel.Size / 2, dauer);
        tw.TweenProperty(tafel, "scale", new Vector2(massstab, massstab), dauer);
        tw.TweenProperty(tafel, "modulate:a", 0.0, dauer);
        tw.Chain().TweenCallback(Callable.From(() => Fertig(tafel, e)));
    }

    private void Fertig(PanelContainer tafel, Eintrag e)
    {
        tafel.QueueFree();
        _tween = null;
        e.Ankunft();
        _reihe.Fertig(e);
        Naechste();
    }

    /// <summary>Das Siegel des Themas, wie es in der Ecke steht (gezeichnet wie dort).</summary>
    private sealed partial class Siegelbild : Control
    {
        public Color Farbe;
        public string ThemaId = "stadt";

        public override void _Draw() => Siegelzeichner.Zeichnen(this, Size / 2, Mathf.Min(Size.X, Size.Y) * 0.42f, Farbe, ThemaId, 7);
    }
}
