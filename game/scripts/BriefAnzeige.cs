using Godot;
using Reichsgilde.Core.Geheimnisse;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das <b>Brief-Fenster</b> (Benachrichtigungen, Stufe 1): ein Bogen Pergament mit Absender, Titel und den Zeilen, unten das Siegel in der Farbe des Themas.
/// Es steht rechts unten über der Siegel-Ecke, ohne die Stadt zu verdecken, und hält nie die Zeit an. Kommen mehrere Briefe, bilden sie einen Stapel
/// („Brief 2 von 3“, Pfeile zum Blättern); Schließen legt den ganzen Stapel weg – alles steht weiter im Verlauf. Ein Knopf unten führt, soweit der Text es hergibt,
/// zum Betrieb, ins Familienbuch, in die Ratskammer oder zur Jahresrechnung.
/// Gestaltet wie die übrigen Bilder: schwarzer Umriss, Farbband leicht neben der Linie, Schraffur am Rand im Schatten. Handelt der Brief von einem Geheimnis
/// (<see cref="Meldung.GeheimnisArt"/>), steht unten statt des Wachssiegels das Siegel seiner Art; in Briefen mit mehreren Zeilen steht es klein vor der Zeile.
/// </summary>
public sealed partial class BriefAnzeige : PanelContainer
{
    private static readonly Color Papier = new(0.90f, 0.84f, 0.68f);
    private static readonly Color Tinte = new(0.14f, 0.10f, 0.07f);
    private static readonly Color Blass = new(0.38f, 0.30f, 0.20f);
    private static readonly Color Rot = new(0.62f, 0.15f, 0.10f);
    private const int Breite = 440;

    private readonly List<Brief> _stapel = new();
    private int _nr;
    private VBoxContainer _inhalt = null!;
    private double _alter = 1;

    /// <summary>Der Spieler drückt den Knopf unten; die Hauptszene kennt das Ziel (Betrieb, Familienbuch …).</summary>
    public event Action<Brief>? ZielGewaehlt;

    /// <summary>Der Spieler drückt „Hinsehen“ zu einer Meldung (Stufe 2): die Hauptszene fährt hin und öffnet Auswahl, Tafel oder Charakterfenster.</summary>
    public event Action<Meldung>? Hinsehen;

    /// <summary>Wohin „Hinsehen“ zu einer Meldung führt, als kurzer Text für den Tooltip (null: nirgends hin, kein Knopf). Setzt die Hauptszene.</summary>
    public Func<Meldung, string?> HinsehenZiel { get; set; } = _ => null;

    /// <summary>Der Tooltip zum Siegel einer Geheimnisart (Name und Beschreibung aus den Daten). Setzt die Hauptszene.</summary>
    public Func<Geheimnisart, string> GeheimnisTipp { get; set; } = art => art.ToString();

    /// <summary>Wurde der Stapel weggelegt (Schließen)?</summary>
    public event Action? Weggelegt;

    public Brief? Aktuell => Visible && _nr < _stapel.Count ? _stapel[_nr] : null;

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
        var rahmen = Fensterrahmen.Anmelden(this, "Brief", Weglegen);
        rahmen.PlatzSetzen((groesse, sichtbar) => new Vector2(sichtbar.X - groesse.X - 16,
            Mathf.Max(Rollbereich.KopfHoehe, sichtbar.Y - Rollbereich.LeistenHoehe - Rollbereich.EckeHoehe - groesse.Y - 10 - AbstandUnten)));
        Hide();
    }

    /// <summary>Hinter dem ✕ des Fensterrahmens (hell, für dunkle Tafeln) eine dunkle Platte, damit es auf dem Pergament zu sehen ist.</summary>
    public override void _Draw()
    {
        var platte = new Rect2(Size.X - 40, 5, 34, 26);
        DrawRect(platte, new Color(0.22f, 0.17f, 0.12f));
        DrawRect(platte, new Color(0.09f, 0.07f, 0.05f), false, 2f);
    }

    /// <summary>Zusätzlicher Abstand nach unten (z. B. über der Raumleiste im Innenraum).</summary>
    public float AbstandUnten { get; set; }

    /// <summary>Ein Brief kommt an: öffnet das Fenster, wenn es zu ist; sonst kommt er hinten an den Stapel, ohne die Seite zu wechseln.</summary>
    public void Einreihen(Brief brief)
    {
        var vorher = _stapel.FindIndex(b => b.Schluessel == brief.Schluessel && brief.Schluessel != "");
        if (vorher >= 0) _stapel[vorher] = brief; else _stapel.Add(brief);
        if (!Visible)
        {
            _nr = vorher >= 0 ? vorher : _stapel.Count - 1;
            Oeffnen();
        }
        else Aufbauen();
    }

    /// <summary>Der Spieler will diesen Brief lesen (Klick auf ein Siegel): er kommt nach vorn.</summary>
    public void Zeigen(Brief brief)
    {
        var vorher = _stapel.FindIndex(b => b.Schluessel == brief.Schluessel && brief.Schluessel != "");
        if (vorher >= 0) _stapel[vorher] = brief; else _stapel.Add(brief);
        _nr = vorher >= 0 ? vorher : _stapel.Count - 1;
        if (!Visible) Oeffnen(); else Aufbauen();
    }

    private void Oeffnen()
    {
        Aufbauen();
        _alter = 0;
        Modulate = new Color(1, 1, 1, Meldungseinstellungen.Bewegung ? 0 : 1);
        Show();
    }

    public override void _Process(double delta)
    {
        if (_alter >= 1 || !Visible) return;
        _alter += delta / 0.22;
        Modulate = new Color(1, 1, 1, Meldungseinstellungen.Bewegung ? (float)Math.Min(1, _alter) : 1f);
    }

    public void Weglegen()
    {
        _stapel.Clear();
        _nr = 0;
        Hide();
        Weggelegt?.Invoke();
    }

    private void Aufbauen()
    {
        UiHilfe.KinderEntfernen(_inhalt);
        if (_stapel.Count == 0) return;
        _nr = Math.Clamp(_nr, 0, _stapel.Count - 1);
        var b = _stapel[_nr];

        // Kopf: Absender klein, rechts Platz für das ✕ des Fensterrahmens
        var kopf = new HBoxContainer();
        _inhalt.AddChild(kopf);
        var von = Hauptmenue.Text(kopf, "Absender: " + b.Absender, 13, Blass);
        von.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        kopf.AddChild(new Control { CustomMinimumSize = new Vector2(34, 0) });

        // Titel auf einem Farbband (Handfarbe leicht neben dem Umriss)
        var band = new Farbband { Farbe = b.Farbe, CustomMinimumSize = new Vector2(0, 40) };
        _inhalt.AddChild(band);
        var titel = new Label { Text = b.Titel, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        titel.AddThemeFontOverride("font", Schriften.Ueberschrift);
        titel.AddThemeFontSizeOverride("font_size", 24);
        titel.AddThemeColorOverride("font_color", Tinte);
        titel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        titel.OffsetLeft = 12;
        band.AddChild(titel);

        // Porträt des Betroffenen (Stufe 2): links neben Name und Haus; ein Klick öffnet das Charakterfenster
        if (b.Person is { } person && b.Buch is { } buch)
        {
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
            bild.TooltipText = "Klick: Charakterfenster";
            rahmen.AddChild(bild);
            reihe.AddChild(rahmen);
            var namen = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
            namen.AddThemeConstantOverride("separation", 0);
            reihe.AddChild(namen);
            Hauptmenue.Text(namen, person.Name, 17, Tinte);
            Hauptmenue.Text(namen, (person.Lebt ? "" : "† ") + "Haus " + person.Familienname, 13, Blass);
        }

        foreach (var (zeilenKopf, text, warnung, kern) in b.Zeilen.Take(40))
        {
            var zeile = new VBoxContainer();
            zeile.AddThemeConstantOverride("separation", 0);
            _inhalt.AddChild(zeile);
            // Kopf mit kleinem Knopf „Hinsehen“, wenn die Zeile zu einer Meldung mit Ziel gehört und der Brief mehrere Zeilen hat
            var ziel = kern is not null && b.Zeilen.Count > 1 ? HinsehenZiel(kern) : null;
            if (zeilenKopf != "" || ziel is not null)
            {
                var kopfzeile = new HBoxContainer();
                kopfzeile.AddThemeConstantOverride("separation", 6);
                zeile.AddChild(kopfzeile);
                Hauptmenue.Text(kopfzeile, zeilenKopf, 12, Blass).SizeFlagsHorizontal = SizeFlags.ExpandFill;
                if (ziel is not null)
                {
                    var hin = new Button { Text = "Hinsehen", FocusMode = FocusModeEnum.None, TooltipText = ziel, Flat = true };
                    hin.AddThemeFontSizeOverride("font_size", 12);
                    hin.AddThemeColorOverride("font_color", Blass);
                    hin.Pressed += () => Hinsehen?.Invoke(kern!);
                    kopfzeile.AddChild(hin);
                }
            }
            // Bei mehreren Zeilen: eine Meldung über ein Geheimnis bekommt das kleine Siegel seiner Art vor den Text (ein Brief mit einer Meldung trägt es unten)
            var geheim = b.Zeilen.Count > 1 && kern?.GeheimnisArt is { } art ? CharakterAnzeige.GeheimnisSiegelBild(art, 30, GeheimnisTipp(art)) : null;
            if (geheim is null)
            {
                Hauptmenue.Text(zeile, text, 16, warnung ? Rot : Tinte, umbruchBreite: Breite - 6);
                continue;
            }
            var mitSiegel = new HBoxContainer();
            mitSiegel.AddThemeConstantOverride("separation", 8);
            zeile.AddChild(mitSiegel);
            mitSiegel.AddChild(geheim);
            Hauptmenue.Text(mitSiegel, text, 16, warnung ? Rot : Tinte, umbruchBreite: Breite - 6 - 38);
        }
        if (b.Zeilen.Count > 40) Hauptmenue.Text(_inhalt, $"… und {b.Zeilen.Count - 40} weitere im Verlauf.", 13, Blass);
        if (b.Fuss != "") Hauptmenue.Text(_inhalt, b.Fuss, 13, Blass, umbruchBreite: Breite - 6);

        // Fuß: Knöpfe links, Siegel rechts
        var fuss = new HBoxContainer();
        fuss.AddThemeConstantOverride("separation", 8);
        _inhalt.AddChild(fuss);
        var knoepfe = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        knoepfe.AddThemeConstantOverride("separation", 8);
        fuss.AddChild(knoepfe);
        // „Hinsehen“: zur Meldung des Briefes (Haus, Betrieb, Ort oder Person); er ersetzt den Knopf „Zu <Betrieb>“
        var hinsehen = b.Kern is { } k ? HinsehenZiel(k) : null;
        if (hinsehen is not null)
        {
            var hin = new Button { Text = "Hinsehen", FocusMode = FocusModeEnum.None, TooltipText = hinsehen };
            hin.AddThemeFontSizeOverride("font_size", 14);
            hin.Pressed += () => Hinsehen?.Invoke(b.Kern!);
            knoepfe.AddChild(hin);
        }
        var zielText = hinsehen is not null && b.Ziel == "betrieb" ? "" : ZielText(b);
        if (zielText != "")
        {
            var ziel = new Button { Text = zielText, FocusMode = FocusModeEnum.None };
            ziel.AddThemeFontSizeOverride("font_size", 14);
            ziel.Pressed += () => ZielGewaehlt?.Invoke(b);
            knoepfe.AddChild(ziel);
        }
        if (_stapel.Count > 1)
        {
            var zurueck = new Button { Text = "‹", FocusMode = FocusModeEnum.None, TooltipText = "Voriger Brief" };
            zurueck.Pressed += () => { _nr = (_nr + _stapel.Count - 1) % _stapel.Count; Aufbauen(); KlangOberflaeche.Spielen(KlangOberflaeche.Ereignis.MeldungBrief); };
            var weiter = new Button { Text = "›", FocusMode = FocusModeEnum.None, TooltipText = "Nächster Brief" };
            weiter.Pressed += () => { _nr = (_nr + 1) % _stapel.Count; Aufbauen(); KlangOberflaeche.Spielen(KlangOberflaeche.Ereignis.MeldungBrief); };
            knoepfe.AddChild(zurueck);
            Hauptmenue.Text(knoepfe, $"Brief {_nr + 1} von {_stapel.Count}", 14, Blass).SizeFlagsVertical = SizeFlags.ShrinkCenter;
            knoepfe.AddChild(weiter);
        }
        var weg = new Button { Text = _stapel.Count > 1 ? "Alle weglegen" : "Weglegen", FocusMode = FocusModeEnum.None, TooltipText = "Legt den Brief weg. Er steht weiter im Verlauf (Bericht)." };
        weg.AddThemeFontSizeOverride("font_size", 14);
        weg.Pressed += Weglegen;
        knoepfe.AddChild(weg);
        // Unten das Siegel: bei einem Brief über ein Geheimnis das Siegel seiner Art (Holzschnitt-Medaille), sonst das gezeichnete Wachssiegel des Themas
        if (b.Kern?.GeheimnisArt is { } briefArt && CharakterAnzeige.GeheimnisSiegelBild(briefArt, 56, GeheimnisTipp(briefArt)) is { } medaille)
        {
            medaille.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            fuss.AddChild(medaille);
        }
        else fuss.AddChild(new SiegelBild { Farbe = b.Farbe, Thema = b.Thema, CustomMinimumSize = new Vector2(52, 52) });
    }

    /// <summary>Die Beschriftung des Knopfs unten im Brief („Zu …“, „Familienbuch“ …); leer: kein Knopf. Auch der Verlauf nutzt sie.</summary>
    public static string ZielText(Brief b) => b.Ziel switch
    {
        "betrieb" when b.Betrieb is not null => "Zu " + b.Betrieb,
        "personal" => "Zum Personalbuch",
        "familie" => "Familienbuch",
        "rat" => "Ratskammer",
        "bericht" => "Jahresrechnung",
        _ => "",
    };

    /// <summary>Ein Farbband: Fläche in der Farbe des Themas, leicht versetzt zum schwarzen Umriss, mit Schraffur am rechten Ende.</summary>
    internal sealed partial class Farbband : Control
    {
        public Color Farbe;

        public override void _Draw()
        {
            var r = new Rect2(Vector2.Zero, Size);
            DrawRect(new Rect2(r.Position + new Vector2(3, 2.5f), r.Size - new Vector2(3, 2.5f)), Farbe.Lightened(0.15f) with { A = 0.85f });
            for (var x = r.Size.X * 0.72f; x < r.Size.X; x += 6)
                DrawLine(new Vector2(x, r.Size.Y), new Vector2(x + 10, 3), new Color(0, 0, 0, 0.18f), 1f, true);
            DrawRect(new Rect2(1, 1, r.Size.X - 2, r.Size.Y - 2), new Color(0.09f, 0.07f, 0.05f), false, 2f);
            DrawLine(new Vector2(0, r.Size.Y + 3), new Vector2(r.Size.X, r.Size.Y + 3), new Color(0.09f, 0.07f, 0.05f, 0.6f), 1f);
        }
    }

    /// <summary>Das Siegel am Fuß des Briefes.</summary>
    internal sealed partial class SiegelBild : Control
    {
        public Color Farbe;
        public string Thema = "stadt";
        public override void _Draw() => Siegelzeichner.Zeichnen(this, Size / 2, 22, Farbe, Thema, 7);
    }
}
