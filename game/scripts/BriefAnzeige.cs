using Godot;
using Reichsgilde.Core.Geheimnisse;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das <b>Brief-Fenster</b> (Benachrichtigungen, Stufe 1): ein gefalteter Brief aus Hadernpapier mit Wachssiegel, wie man ihn um 1400 schrieb – als
/// 3D-Modell (<see cref="BriefBuehne"/>, Ablauf in <see cref="BriefAblauf"/>). Beim Öffnen bricht das Siegel und der Brief faltet sich groß in der Bildmitte
/// auf; kurz vor dem Ende liegt der Inhalt als Bild auf dem Blatt, und sobald der Brief offen ist, liegt genau darüber diese Tafel mit Absender, Titel,
/// den Zeilen und den Knöpfen (scharfer Text, klickbar) – ohne eigenen Grund, das Papier ist das Blatt des Modells. Weggelegt faltet er sich ohne Hüpfen
/// zusammen und verschwindet. Lange Briefe strecken das Blatt in der Höhe (Streckregel des Modells), noch längere rollen. Ohne Bewegung
/// (<see cref="Meldungseinstellungen.Bewegung"/>) liegt der Brief sofort offen da und ist sofort weg. Er hält nie die Zeit an. Kommen mehrere Briefe, bilden sie einen Stapel
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
    /// <summary>Breite der Tafel = Breite der Inhaltsfläche des Blatts, Innenabstand und Breite der Textzeilen (Oberflächen-Pixel, aus dem Thema).</summary>
    private static int TafelBreite => MeldungStil.Mass("brief_gross_breite");
    private static int Innen => MeldungStil.Mass("brief_gross_innen");
    private static int Breite => TafelBreite - 2 * Innen;

    private readonly List<Brief> _stapel = new();
    private int _nr;
    private VBoxContainer _inhalt = null!;
    private VBoxContainer? _abbildInhalt;
    private MarginContainer _rand = null!;
    private Rollbereich _bereich = null!;
    private Control _sperre = null!;
    private Fensterrahmen _rahmen = null!;
    private readonly BriefBuehne _buehne = new() { Name = "BriefBuehne" };
    private BriefAblauf _ablauf = null!;
    private Rect2 _bildRechteck;
    /// <summary>So viele Bilder bleibt der Brief nach dem Öffnen noch unsichtbar: Die Tafel kennt ihre Größe (und damit die Blatthöhe) erst nach der Anordnung.</summary>
    private int _verdeckt;

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
        // Erst die Tafel (das erste Kind hüllt der Rollbereich ein), dann die Bühne
        _rand = new MarginContainer();
        foreach (var seite in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" }) _rand.AddThemeConstantOverride(seite, Innen);
        AddChild(_rand);
        _inhalt = new VBoxContainer { CustomMinimumSize = new Vector2(Breite, 0) };
        _inhalt.AddThemeConstantOverride("separation", 8);
        _rand.AddChild(_inhalt);
        AddChild(_buehne);
        _ablauf = new BriefAblauf(_buehne.Masse);
        if (_buehne.Bereit)
        {
            // Das Papier ist das Blatt des 3D-Modells: die Tafel selbst hat keinen Grund. Ihre Breite ist die der Inhaltsfläche, ihre kleinste Höhe die des ungestreckten Blatts
            AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
            _rand.CustomMinimumSize = new Vector2(TafelBreite, BriefBuehne.Tafelhoehen(TafelBreite).Kleinste);
            if (_buehne.Abbild is { } abbild)
            {
                foreach (var seite in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" }) abbild.AddThemeConstantOverride(seite, Innen);
                _abbildInhalt = new VBoxContainer { CustomMinimumSize = new Vector2(Breite, 0) };
                _abbildInhalt.AddThemeConstantOverride("separation", 8);
                abbild.AddChild(_abbildInhalt);
            }
        }
        else
        {
            // Ohne Modell: ein Bogen Papier in 2D, ohne Falten
            var blatt = new StyleBoxFlat
            {
                BgColor = Papier, BorderColor = new Color(0.09f, 0.07f, 0.05f),
                BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
                CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2, CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2,
                ShadowColor = new Color(0, 0, 0, 0.45f), ShadowSize = 2, ShadowOffset = new Vector2(5, 6),
            };
            blatt.SetContentMarginAll(14);
            AddThemeStyleboxOverride("panel", blatt);
        }
        _bereich = Rollbereich.Einhuellen(this);
        // Während des Faltens liegt eine durchlässige Sperre über der (unsichtbaren) Tafel, damit kein Knopf einen Klick bekommt
        _sperre = new Control { MouseFilter = MouseFilterEnum.Stop, Visible = false };
        AddChild(_sperre);
        _rahmen = Fensterrahmen.Anmelden(this, "Brief", Weglegen);
        // Groß in der Bildmitte (über der Raumleiste des Innenraums etwas höher)
        _rahmen.PlatzSetzen((groesse, sichtbar) => new Vector2((sichtbar.X - groesse.X) / 2, Mathf.Max(0, (sichtbar.Y - AbstandUnten - groesse.Y) / 2)));
        Hide();
    }

    /// <summary>
    /// Zeichnet das Bild des 3D-Briefs hinter die Tafel (es ragt um den Rand des Blatts über sie hinaus) und, wenn der Brief offen ist, hinter dem ✕ des
    /// Fensterrahmens (hell, für dunkle Tafeln) eine dunkle Platte, damit es auf dem Papier zu sehen ist.
    /// </summary>
    public override void _Draw()
    {
        if (_buehne.Bereit)
        {
            if (_verdeckt == 0 && _ablauf.Sichtbar && _buehne.Bild is { } bild) DrawTextureRect(bild, _bildRechteck, false, new Color(1, 1, 1, (float)_ablauf.Deckkraft));
            if (!_ablauf.TafelSichtbar || _verdeckt > 0) return;
        }
        var platte = new Rect2(Size.X - 40, 5, 34, 26);
        DrawRect(platte, new Color(0.22f, 0.17f, 0.12f));
        DrawRect(platte, new Color(0.09f, 0.07f, 0.05f), false, 2f);
    }

    /// <summary>Der Brief ist da oder faltet sich gerade auf (nicht: faltet sich zu).</summary>
    private bool Offen => Visible && (!_buehne.Bereit || _ablauf.Zustand is BriefZustand.Auffalten or BriefZustand.Offen);

    /// <summary>Zusätzlicher Abstand nach unten (z. B. über der Raumleiste im Innenraum).</summary>
    public float AbstandUnten { get; set; }

    /// <summary>Ein Brief kommt an: öffnet das Fenster, wenn es zu ist; sonst kommt er hinten an den Stapel, ohne die Seite zu wechseln.</summary>
    public void Einreihen(Brief brief)
    {
        if (!_freigabe && _aufschub.Zurueckstellen(brief, SitzungOffen())) return;   // wartet bis nach Sitzung/Stimmrunde (Briefaufschub.cs)
        var vorher = _stapel.FindIndex(b => b.Schluessel == brief.Schluessel && brief.Schluessel != "");
        if (vorher >= 0) _stapel[vorher] = brief; else _stapel.Add(brief);
        if (!Offen)
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
        if (!Offen) Oeffnen(); else Aufbauen();
    }

    private void Oeffnen()
    {
        Aufbauen();
        if (_buehne.Bereit)
        {
            if (!Visible)
            {
                _ablauf = new BriefAblauf(_buehne.Masse); // von vorn (auch wenn das Fenster von außen einfach versteckt wurde)
                _verdeckt = 3;
            }
            _ablauf.Oeffnen(Meldungseinstellungen.Bewegung); // Siegel bricht, auffalten; mitten im Zufalten geht es an der passenden Stelle zurück
            TafelZeigen(false);
        }
        Show();
    }

    /// <summary>Die echte Tafel (Text, Knöpfe, ✕) an oder aus – aus heißt unsichtbar und ohne Klicks, die Größe bleibt (sie gibt die Blatthöhe vor).</summary>
    private void TafelZeigen(bool an)
    {
        var ton = new Color(1, 1, 1, an ? 1 : 0);
        if (_bereich.Modulate != ton) _bereich.Modulate = ton;
        if (_sperre.Visible == an) _sperre.Visible = !an;
        _rahmen.LeisteSichtbar = an;
    }

    // Ratssitzung und Stimmrunde (Nutzer, Rathaus Stufe 10): Briefe liegen sonst über deren Tafel. Sie warten, solange eine der beiden Tafeln
    // offen ist (beide hängen wie der Brief in der Ebene „Oberflaeche“), und kommen direkt danach in ihrer Reihenfolge (Briefaufschub.cs).
    private readonly Briefaufschub<Brief> _aufschub = new();
    private bool _freigabe;
    private bool SitzungOffen() => Ruht?.Invoke() == true
        || GetParent() is { } ebene && ebene.GetChildren().Any(k => k is RatssitzungAnzeige { Visible: true } or StimmrundeAnzeige { Visible: true });

    /// <summary>Setzt die Hauptszene: die ganze Folge Stimmrunde → Sitzung → Jahresbericht (<see cref="Briefphase"/>), auch zwischen den Tafeln.</summary>
    public Func<bool>? Ruht { get; set; }

    public override void _Process(double delta)
    {
        if (_aufschub.Wartend > 0 && _aufschub.Freigeben(SitzungOffen()) is { Count: > 0 } frei)
        {
            _freigabe = true;
            foreach (var b in frei) Einreihen(b);
            _freigabe = false;
        }
        if (!_buehne.Bereit) return;
        if (!Visible)
        {
            _buehne.Ruhen(); // auch wenn das Fenster von außen einfach versteckt wurde
            return;
        }
        // Die Tafel darf so hoch werden, wie sich das Blatt strecken lässt (und wie das Fenster Platz hat); was länger ist, rollt
        var fenster = GetViewportRect().Size;
        var platz = fenster.Y - AbstandUnten - 2 * (BriefBuehne.Blattrand(TafelBreite) + MeldungStil.Mass("brief_gross_fensterrand"));
        _bereich.Abstand = 0;
        _bereich.UntenFrei = 0;
        _bereich.ObenFrei = fenster.Y - Mathf.Min(BriefBuehne.Tafelhoehen(TafelBreite).Groesste, platz);
        // Bildzeit, nicht Spielzeit: Der Brief gehört zur Oberfläche und läuft unabhängig vom Spieltempo
        if (_verdeckt > 0) _verdeckt--;
        else _ablauf.Weiter(Math.Min(delta, 0.1), Meldungseinstellungen.Bewegung);
        if (!_ablauf.Sichtbar)
        {
            _buehne.Ruhen();
            Hide();
            return;
        }
        _bildRechteck = _buehne.Einstellen(Size, Math.Max(GetWindow().GetFinalTransform().Scale.X, 0.1f));
        _buehne.Weiter(_ablauf);
        TafelZeigen(_ablauf.TafelSichtbar && _verdeckt == 0);
        QueueRedraw();
    }

    /// <summary>Legt den Stapel weg: Der Brief faltet sich zu (ohne Hüpfen) und verschwindet; ohne Bewegung oder ohne Modell sofort.</summary>
    public void Weglegen()
    {
        _stapel.Clear();
        _nr = 0;
        if (_buehne.Bereit && Visible && _ablauf.Sichtbar)
        {
            _ablauf.Schliessen(Meldungseinstellungen.Bewegung);
            TafelZeigen(false);
            if (!_ablauf.Sichtbar)
            {
                _buehne.Ruhen();
                Hide();
            }
        }
        else Hide();
        Weggelegt?.Invoke();
    }

    /// <summary>
    /// Baut den Inhalt des aktuellen Briefs: einmal in die echte Tafel und, wenn die Bühne steht, mit denselben Steuerelementen noch einmal in ihr Abbild
    /// (das Bild auf der Inhaltsfläche während des Faltens; dort nimmt nichts Eingaben an).
    /// </summary>
    private void Aufbauen()
    {
        if (_stapel.Count == 0) return;
        _nr = Math.Clamp(_nr, 0, _stapel.Count - 1);
        var b = _stapel[_nr];
        Bauen(_inhalt, b);
        if (_abbildInhalt is null) return;
        Bauen(_abbildInhalt, b);
        _buehne.SiegelSetzen(b.Thema, b.Farbe);
        _buehne.AbbildGeaendert();
    }

    private void Bauen(VBoxContainer blatt, Brief b)
    {
        UiHilfe.KinderEntfernen(blatt);

        // Kopf: Absender klein, rechts Platz für das ✕ des Fensterrahmens
        var kopf = new HBoxContainer();
        blatt.AddChild(kopf);
        var von = Hauptmenue.Text(kopf, "Absender: " + b.Absender, 13, Blass);
        von.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        kopf.AddChild(new Control { CustomMinimumSize = new Vector2(34, 0) });

        // Titel auf einem Farbband (Handfarbe leicht neben dem Umriss)
        var band = new Farbband { Farbe = b.Farbe, CustomMinimumSize = new Vector2(0, 40) };
        blatt.AddChild(band);
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
            blatt.AddChild(reihe);
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
            blatt.AddChild(zeile);
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
        if (b.Zeilen.Count > 40) Hauptmenue.Text(blatt, $"… und {b.Zeilen.Count - 40} weitere im Verlauf.", 13, Blass);
        if (b.Fuss != "") Hauptmenue.Text(blatt, b.Fuss, 13, Blass, umbruchBreite: Breite - 6);

        // Fuß: Knöpfe links, Siegel rechts – unten am Blatt (ein kurzer Brief füllt das Blatt nicht)
        blatt.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        var fuss = new HBoxContainer();
        fuss.AddThemeConstantOverride("separation", 8);
        blatt.AddChild(fuss);
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
