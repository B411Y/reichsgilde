using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Die Tafel rechts für das Felswerkzeug (Felspinsel F4, alle Werte seit P3) – ein Teil der <c>partial class Kartenwerkstatt</c>, im Stil der übrigen
/// Werkstatt-Tafeln. Oben Vorlage (Gruppe oder Einzelstücke mit ihren Formen), Gestein der neuen Stücke und das Grundgestein der Karte (ein Befehl mit
/// Rückgängig) – beim Radierer und bei „Neu verteilen“ stattdessen die Formen mit „nur diese Formen“. Darunter die Regler des Pinsels (Radius, Dichte,
/// Abstand, Größe mit Streuung, Einsinken mit Streuung, Neigung zum Hang; beim Radierer „Ausdünnen bis“, bei „Neu verteilen“ die Häkchen, welche Werte
/// neu gewürfelt werden), zugeklappt „Erweitert“ mit den Zufallsbereichen (Drehung, Kippen, Rollen, Achsen, Versatz, Neigung bis) und der Filter wie bei
/// Gelände und Boden mit dem Höhenfilter. Die Regler ändern nur die Einstellungen der Stadtansicht (<see cref="Stadtansicht.FelsPinsel"/>), erst Klick
/// oder Strich ändern die Karte.
/// </summary>
public partial class Kartenwerkstatt
{
    /// <summary>Steht in <see cref="_gezeigt"/>, solange die Tafel das Felswerkzeug zeigt.</summary>
    private static readonly object FelsMarke = new();

    private static string GesteinName(string kennung) => Gesteinsarten.Nach(kennung).Name;

    private void FelsTafel()
    {
        EigenschaftenLeeren();
        _gezeigt = FelsMarke;
        var modus = _stadt.FelsModus;
        var gruppe = _stadt.FelsKatalog.Finde(_stadt.FelsGruppe); // P5: feste und eigene Gruppen
        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 8);
        _eigenschaften.AddChild(kopf);
        var farbe = modus switch
        {
            Stadtansicht.FelsArt.Radieren => new Color(0.33f, 0.40f, 0.24f),
            Stadtansicht.FelsArt.Verteilen => new Color(0.78f, 0.64f, 0.30f),
            _ => Stadtansicht.FelsFarbe(_stadt.FelsGestein ?? _entwurf.Grundgestein.Wirksam),
        };
        kopf.AddChild(new TextureRect { Texture = Farbfeld(farbe), StretchMode = TextureRect.StretchModeEnum.KeepCentered, MouseFilter = Control.MouseFilterEnum.Ignore });
        var titel = modus switch
        {
            Stadtansicht.FelsArt.Radieren => "Fels-Radierer",
            Stadtansicht.FelsArt.Verteilen => "Felsen neu verteilen",
            Stadtansicht.FelsArt.Streuen => gruppe is null ? "Felsen streuen" : $"Streuen: {gruppe.Name}",
            _ => gruppe is null ? "Stempel: Felsstück" : $"Stempel: {gruppe.Name}",
        };
        var name = Hauptmenue.Text(kopf, titel, 17, Gold);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        if (modus == Stadtansicht.FelsArt.Stempeln)
        {
            var wuerfeln = new Button { Text = "Neu würfeln", FocusMode = Control.FocusModeEnum.None,
                TooltipText = "Neue Drehungen, Größen und Kippwinkel für den nächsten Stempel – die Vorschau zeigt sie gleich" };
            wuerfeln.AddThemeFontSizeOverride("font_size", 13);
            wuerfeln.Pressed += () => _stadt.FelsStempelStartwert++;
            kopf.AddChild(wuerfeln);
        }

        if (modus is Stadtansicht.FelsArt.Streuen or Stadtansicht.FelsArt.Stempeln) FelsVorlage(gruppe);
        else FelsFormenWahl(modus);

        Hauptmenue.Text(_eigenschaften, "Pinsel", 15, Gold);
        var raster = ZweiSpalten();
        if (modus != Stadtansicht.FelsArt.Stempeln)
            Zeile(raster, "Radius", Schieber(Felspinsel.MinRadius, 60, 1, _stadt.FelsRadius, Breite, r => _stadt.FelsRadius = r, "Halbmesser des Kreises an der Maus"));
        if (modus == Stadtansicht.FelsArt.Radieren)
        {
            Zeile(raster, "Ausdünnen bis", Schieber(0, 0.8, 0.02, _stadt.FelsAusduennen, d => d <= 0 ? "aus: alles weg" : $"{d * 100:0} %", d => _stadt.FelsAusduennen = d,
                "0: nimmt alle Stücke mit der Mitte im Kreis weg. Darüber dünnt der Pinsel nur aus: Er nimmt zufällig gewählte Stücke weg, bis ihre Grundflächen " +
                "nur noch so viel des Kreises decken (wie „Erase Density“ in Unreal)"));
            Hauptmenue.Text(_eigenschaften, "Nimmt Felsstücke weg, deren Mitte im Kreis liegt – mit „Ausdünnen bis“ nur so viele, bis die Dichte erreicht ist. " +
                                            "Der Boden bleibt, wie er ist. Beim Streuen radiert gehaltenes Umschalt ebenso (ohne Ausdünnen).", 12, Grau, umbruchBreite: Rechtsbreite - 30);
            FelsFilter();
            return;
        }
        if (modus == Stadtansicht.FelsArt.Verteilen)
        {
            Hauptmenue.Text(_eigenschaften, "Neu würfeln:", 13, Hell);
            FelsWurfHaken(_eigenschaften);
            raster = ZweiSpalten();
        }
        if (modus == Stadtansicht.FelsArt.Streuen)
        {
            Zeile(raster, "Dichte", Schieber(0.02, 0.8, 0.02, _stadt.FelsDichte, d => $"{d * 100:0} %", d => _stadt.FelsDichte = d,
                "Wie viel des Kreises die Felsen bedecken sollen: Der Strich setzt Stücke, bis die Grundflächen diesen Anteil erreichen"));
            Zeile(raster, "Abstand", Schieber(0, 10, 0.25, _stadt.FelsAbstand, a => a <= 0 ? "dicht an dicht" : $"+ {Komma(a, "0.00")} m", a => _stadt.FelsAbstand = a,
                "So viele Meter bleiben zwischen den Grundflächen zweier Stücke zusätzlich frei (0: sie dürfen sich wie bisher ein Stück überdecken)"));
        }
        Label? spanne = null;
        string Spanne() => $"Größe {Komma(Math.Max(Felsformen.MassstabMin, _stadt.FelsGroesse * (1 - _stadt.FelsStreuung)), "0.00")} bis {Komma(_stadt.FelsGroesse * (1 + _stadt.FelsStreuung), "0.00")} ×";
        Zeile(raster, "Größe", Schieber(Felsformen.MassstabMin, 2.5, 0.05, _stadt.FelsGroesse, g => $"{Komma(g, "0.00")} ×", g =>
            {
                _stadt.FelsGroesse = g;
                if (spanne is not null) spanne.Text = Spanne();
            },
            "Maßstab der Stücke (1 = wie das Modell: eine Felswand ist 9 m breit und 6,6 m hoch, die große Wand 20 m breit und 16 m hoch)"));
        Zeile(raster, "Streuung", Schieber(0, 0.75, 0.05, _stadt.FelsStreuung, s => $"± {s * 100:0} %", s =>
            {
                _stadt.FelsStreuung = s;
                if (spanne is not null) spanne.Text = Spanne();
            },
            "So weit weicht die Größe je Stück (bzw. Gruppe) zufällig ab – Größe und Streuung zusammen sind der Bereich von–bis"));
        Zeile(raster, "", spanne = Wert(Spanne(), "Der Bereich, aus dem jedes Stück seine Größe zieht"));
        Zeile(raster, "Einsinken", Schieber(0, 0.6, 0.05, _stadt.FelsEinsinken, e => $"{e * 100:0} %", e => _stadt.FelsEinsinken = e,
            "Anteil der Höhe unter dem Boden – 20 bis 40 % lassen keine Unterkante schweben"));
        Zeile(raster, "Einsinken ±", Schieber(0, Felspinsel.EinsinkenStreuungMax, 0.01, _stadt.FelsEinsinkenStreuung, e => $"± {e * 100:0} %", e => _stadt.FelsEinsinkenStreuung = e,
            "So weit weicht das Einsinken je Stück zufällig ab (Anteil der Höhe; Vorgabe ± 5 %)"));
        Zeile(raster, "Neigung", Schieber(0, 1, 0.05, _stadt.FelsNeigung, n => $"{n * 100:0} %", n => _stadt.FelsNeigung = n,
            "Wie weit sich ein Stück zum Hang legt: 0 % steht senkrecht, 100 % liegt ganz am Hang an"));
        var hang = new CheckBox { Text = "Schauseite hangabwärts", ButtonPressed = _stadt.FelsZumHang, FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Wände, Rippen, Überhänge, Felsnasen und Gruppen drehen ihre Vorderseite hangabwärts (± 20° oder der Bereich unter „Erweitert“), sobald der Hang 3 % steigt; sonst ganz zufällig" };
        hang.AddThemeFontSizeOverride("font_size", 14);
        hang.Toggled += an => _stadt.FelsZumHang = an;
        _eigenschaften.AddChild(hang);
        if (modus == Stadtansicht.FelsArt.Streuen)
        {
            var aus = new CheckBox { Text = "Gebäude und Wege aussparen", ButtonPressed = _stadt.FelsAussparen, FocusMode = Control.FocusModeEnum.None,
                TooltipText = "Das Streuen setzt kein Stück, dessen Grundfläche ein Gebäude (mit Vorplatz) oder einen Weg berührt. Der Stempel und das Gizmo bleiben frei, " +
                              "und Bauen auf Fels bleibt erlaubt. Gilt für den Strich, wie die Karte beim Beginn des Strichs steht." };
            aus.AddThemeFontSizeOverride("font_size", 14);
            CheckBox? zonen = null;
            aus.Toggled += an =>
            {
                _stadt.FelsAussparen = an;
                if (zonen is not null) zonen.Disabled = !an;
            };
            _eigenschaften.AddChild(aus);
            zonen = new CheckBox { Text = "auch Bauzonen aussparen", ButtonPressed = _stadt.FelsAussparenBauzonen, Disabled = !_stadt.FelsAussparen, FocusMode = Control.FocusModeEnum.None,
                TooltipText = "Dazu bleiben auch die Bauzonen frei (die Flächen, auf denen Häuser entstehen dürfen). Aus: Felsen dürfen in Bauzonen stehen." };
            zonen.AddThemeFontSizeOverride("font_size", 14);
            zonen.Toggled += an => _stadt.FelsAussparenBauzonen = an;
            _eigenschaften.AddChild(zonen);
        }
        if (modus != Stadtansicht.FelsArt.Verteilen)
        {
            var boden = new CheckBox { Text = "Boden mitmalen", ButtonPressed = _stadt.FelsBoden, FocusMode = Control.FocusModeEnum.None,
                TooltipText = "Unter jedem Stück wird der Boden zu Fels, hangabwärts liegt ein Schuttkegel aus Kies, am oberen Rand und im Norden ein Moossaum – " +
                              "alles ein Schritt mit den Stücken. Der Radierer der Felsen lässt den Boden (dafür der Radierer der Bodentexturen)." };
            boden.AddThemeFontSizeOverride("font_size", 14);
            boden.Toggled += an => _stadt.FelsBoden = an;
            _eigenschaften.AddChild(boden);
        }
        if (modus == Stadtansicht.FelsArt.Stempeln)
        {
            var hangFormen = new CheckBox { Text = "Hang mitformen", ButtonPressed = _stadt.FelsHangFormen, FocusMode = Control.FocusModeEnum.None,
                TooltipText = "Der Stempel hebt das Gelände hinter und neben den großen Wänden an (sie sind hinten offen), damit sie im Hang stecken und nicht auf der Wiese stehen. " +
                              "Der Hang beginnt etwa 2,5 m hinter der Fußlinie. Ein Schritt mit den Stücken; gilt nur für die Wände des Wand-Satzes (Große Wand, Mittlere Wand, Pfeilerbündel)." };
            hangFormen.AddThemeFontSizeOverride("font_size", 14);
            CheckBox? schicht = null;
            hangFormen.Toggled += an =>
            {
                _stadt.FelsHangFormen = an;
                if (schicht is not null) schicht.Disabled = !an;
            };
            _eigenschaften.AddChild(hangFormen);
            // P4b: der Hang als eigene Schicht (Vorgabe an) – folgt dem Stück beim Verschieben, Drehen und Löschen
            schicht = new CheckBox { Text = "als eigene Schicht (folgt dem Stück)", ButtonPressed = _stadt.FelsHangSchicht, Disabled = !_stadt.FelsHangFormen, FocusMode = Control.FocusModeEnum.None,
                TooltipText = "An: Der Hang gehört zu den Wänden. Er wandert mit, wenn man eine Wand verschiebt oder dreht, und verschwindet mit ihr – am alten Platz bleibt kein Wall. " +
                              "Das Gelände darunter (die Grundhöhe) bleibt, wie es gemalt wurde. Aus: Der Hang wird fest ins Gelände geschrieben (wie früher). " +
                              "Beim Gizmo schaltet „Eigener Hang“ ihn für gewählte Stücke an oder aus." };
            schicht.AddThemeFontSizeOverride("font_size", 13);
            schicht.Toggled += an => _stadt.FelsHangSchicht = an;
            _eigenschaften.AddChild(schicht);
        }
        if (modus == Stadtansicht.FelsArt.Streuen)
        {
            var streuenHang = new CheckBox { Text = "Hang mitformen (eigene Schicht)", ButtonPressed = _stadt.FelsStreuenHang, FocusMode = Control.FocusModeEnum.None,
                TooltipText = "Die gestreuten Wände (Große Wand, Mittlere Wand, Pfeilerbündel, Gruppe Große Wand) heben das Gelände dahinter an – als eigene Schicht, sie folgt jeder Wand " +
                              "beim Verschieben und Löschen. Ein Strich bleibt ein Schritt für Strg+Z. Aus: Der Strich lässt das Gelände, wie es ist." };
            streuenHang.AddThemeFontSizeOverride("font_size", 14);
            streuenHang.Toggled += an => _stadt.FelsStreuenHang = an;
            _eigenschaften.AddChild(streuenHang);
        }
        Hauptmenue.Text(_eigenschaften, modus switch
            {
                Stadtansicht.FelsArt.Stempeln => "Die Vorschau an der Maus zeigt genau die Stücke, die der Klick setzt. Felsen sperren nichts: Gebäude, Wege und Bauzonen dürfen darauf stehen.",
                Stadtansicht.FelsArt.Verteilen => "Der Strich würfelt die angehakten Werte jedes Stücks unter dem Pinsel einmal neu – mit den Werten oben und unter „Erweitert“, " +
                                                  "wie ein Stück, das der Pinsel dort setzte (wie „Reapply“ in Unreal). Ort, Form und Gestein bleiben. Ein Strich ist ein Schritt.",
                _ => "Ein Strich setzt alle halben Radius einen Tupfer; jedes Stück hält Abstand zu den anderen. Mit Umschalt radiert der Strich. Felsen sperren nichts.",
            },
            12, Grau, umbruchBreite: Rechtsbreite - 30);
        if (modus == Stadtansicht.FelsArt.Verteilen && _stadt.FelsAuswahl.Count > 0)
            Knopf(_eigenschaften, $"Auf die Auswahl anwenden ({_stadt.FelsAuswahl.Count})", () => _stadt.FelsAuswahlVerteilen(),
                "Würfelt die angehakten Werte der gewählten Stücke neu (ein Schritt für Strg+Z)");
        FelsErweitert();
        FelsFilter();
        FelsSchwebendeZeile(_eigenschaften);
    }

    /// <summary>
    /// P4b: „Schwebende Stücke“ (wie „Invalid“ im Foliage-Werkzeug von Unreal): die Zahl der Stücke, deren Unterkante über dem Boden liegt (etwa nach dem
    /// Senken des Geländes darunter), und der Knopf, der sie wählt – dann setzt „Auf den Boden“ (End) sie wieder auf. Die Zahl steht beim Aufbau der Tafel;
    /// der Knopf zählt neu.
    /// </summary>
    private void FelsSchwebendeZeile(Container eltern)
    {
        var anzahl = _stadt.FelsSchwebendeAnzahl();
        Hauptmenue.Text(eltern, "Schwebende Stücke", 15, Gold);
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 8);
        eltern.AddChild(zeile);
        var text = Hauptmenue.Text(zeile, anzahl == 0 ? "keine" : $"{anzahl} schweben", 13, anzahl == 0 ? Grau : Hell);
        text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        Knopf(zeile, "Schwebende wählen", () =>
            {
                var n = _stadt.FelsSchwebendeWaehlen();
                if (n == 0) text.Text = "keine";
                Melden(n == 0 ? "Kein Felsstück schwebt." : $"{n} schwebende Felsstücke gewählt – „Auf den Boden“ (End) setzt sie auf.");
            },
            "Wählt alle Stücke, deren Unterkante mehr als 10 cm über dem Boden liegt – zum Beispiel nach dem Senken des Geländes unter ihnen. Danach „Auf den Boden“ (End) oder Löschen.");
    }

    /// <summary>Die Häkchen, welche Werte „Neu verteilen“ neu würfelt (Strich und Knopf an der Auswahl teilen sie).</summary>
    private void FelsWurfHaken(Container eltern)
    {
        var flaeche = new HFlowContainer();
        flaeche.AddThemeConstantOverride("h_separation", 8);
        eltern.AddChild(flaeche);
        foreach (var (wurf, text, tipp) in new[]
                 {
                     (Felswurf.Groesse, "Größe", "Maßstab aus Größe und Streuung"),
                     (Felswurf.Achsen, "Achsen", "Breite, Höhe und Tiefe aus ihren Bereichen unter „Erweitert“ (ohne Bereich: 1)"),
                     (Felswurf.Drehung, "Drehung", "Drehung um die Senkrechte (hangabwärts bzw. der Bereich unter „Erweitert“)"),
                     (Felswurf.Kippen, "Kippen", "Kippen um die eigene x-Achse (± 8° oder der Bereich unter „Erweitert“)"),
                     (Felswurf.Rollen, "Rollen", "Rollen um die eigene z-Achse (± 8° oder der Bereich unter „Erweitert“)"),
                     (Felswurf.Versatz, "Höhe", "Höhenversatz aus dem Bereich unter „Erweitert“ (ohne Bereich: 0)"),
                     (Felswurf.Einsinken, "Einsinken", "Einsinken und seine Streuung"),
                     (Felswurf.Neigung, "Neigung", "Neigung zum Hang (mit „Neigung bis“)"),
                 })
        {
            var w = wurf;
            var haken = new CheckBox { Text = text, ButtonPressed = (_stadt.FelsVerteilenWurf & w) != 0, FocusMode = Control.FocusModeEnum.None, TooltipText = tipp };
            haken.AddThemeFontSizeOverride("font_size", 13);
            haken.Toggled += an => _stadt.FelsVerteilenWurf = an ? _stadt.FelsVerteilenWurf | w : _stadt.FelsVerteilenWurf & ~w;
            flaeche.AddChild(haken);
        }
    }

    /// <summary>Der zugeklappte Abschnitt „Erweitert“: die Zufallsbereiche des Pinsels (R2) und „Neigung bis“. Ohne Häkchen gilt das Verhalten von vorher.</summary>
    private void FelsErweitert()
    {
        Hauptmenue.Text(_eigenschaften, "Erweitert", 15, Gold);
        var raster = ZweiSpalten();
        BereichZeile(raster, "Drehung", _stadt.FelsDrehbereich, -360, 360, 1, "°", 0, 360,
            "Drehung um die Senkrechte von–bis in Grad: ab Norden, bei „Schauseite hangabwärts“ am Hang um hangabwärts herum. Ohne Häkchen: ganz zufällig bzw. hangabwärts ± 20°",
            b => _stadt.FelsDrehbereich = b);
        BereichZeile(raster, "Kippen (X)", _stadt.FelsKippbereich, -Felsformen.KippenGrenze, Felsformen.KippenGrenze, 0.5, "°", -8, 8,
            "Kippen um die eigene x-Achse von–bis (nach vorn oder hinten). Ohne Häkchen ± 8°", b => _stadt.FelsKippbereich = b);
        BereichZeile(raster, "Rollen (Z)", _stadt.FelsRollbereich, -Felsformen.KippenGrenze, Felsformen.KippenGrenze, 0.5, "°", -8, 8,
            "Rollen um die eigene z-Achse von–bis (zur Seite). Ohne Häkchen ± 8°", b => _stadt.FelsRollbereich = b);
        BereichZeile(raster, "Breite (X)", _stadt.FelsAchseX, Felsformen.AchseMin, Felsformen.AchseMax, 0.05, "", 0.8, 1.25,
            "Faktor auf den Maßstab in der Breite, von–bis. Ohne Häkchen 1", b => _stadt.FelsAchseX = b);
        BereichZeile(raster, "Höhe (Y)", _stadt.FelsAchseY, Felsformen.AchseMin, Felsformen.AchseMax, 0.05, "", 0.8, 1.25,
            "Faktor auf den Maßstab in der Höhe, von–bis. Ohne Häkchen 1", b => _stadt.FelsAchseY = b);
        BereichZeile(raster, "Tiefe (Z)", _stadt.FelsAchseZ, Felsformen.AchseMin, Felsformen.AchseMax, 0.05, "", 0.8, 1.25,
            "Faktor auf den Maßstab in der Tiefe, von–bis. Ohne Häkchen 1", b => _stadt.FelsAchseZ = b);
        BereichZeile(raster, "Höhenversatz", _stadt.FelsVersatzbereich, -Felsformen.VersatzMax, Felsformen.VersatzMax, 0.05, "m", 0, 0.5,
            "Meter über (oder unter) der Lage aus Boden und Einsinken, von–bis. Ohne Häkchen 0", b => _stadt.FelsVersatzbereich = b);
        var zeile = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        zeile.AddThemeConstantOverride("separation", 4);
        var an = new CheckBox { ButtonPressed = _stadt.FelsNeigungBis is not null, FocusMode = Control.FocusModeEnum.None, TooltipText = "Grenze an oder aus" };
        var grad = new Zahlfeld { MinValue = 0, MaxValue = 90, Step = 1, Suffix = "°", Value = _stadt.FelsNeigungBis ?? 30, Editable = an.ButtonPressed,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = "Am steilen Hang neigt sich ein Stück höchstens so viele Grad aus der Senkrechten (wie „Align Max Angle“ in Unreal)" };
        an.Toggled += ja =>
        {
            grad.Editable = ja;
            _stadt.FelsNeigungBis = ja ? grad.Value : null;
        };
        grad.ValueChanged += v =>
        {
            if (an.ButtonPressed) _stadt.FelsNeigungBis = v;
        };
        zeile.AddChild(an);
        zeile.AddChild(grad);
        Zeile(raster, "Neigung bis", zeile);
        Hauptmenue.Text(_eigenschaften, "Ein Bereich von–bis gilt mit Häkchen: Jedes Stück zieht seinen Wert gleich verteilt daraus. Ohne Häkchen bleibt das Verhalten von vorher.",
            12, Grau, umbruchBreite: Rechtsbreite - 30);
    }

    /// <summary>
    /// Eine Zeile für einen Zufallsbereich: Häkchen (an/aus), von, bis. Aus: null (es gilt das Verhalten ohne Bereich). Die Zahlen gelten erst mit dem
    /// Häkchen; beim Anhaken stehen dort die Vorgaben (<paramref name="vonVorgabe"/>, <paramref name="bisVorgabe"/>) bzw. der zuletzt eingestellte Bereich.
    /// </summary>
    private static void BereichZeile(GridContainer raster, string name, Felsbereich? wert, double min, double max, double schritt, string einheit,
        double vonVorgabe, double bisVorgabe, string tipp, Action<Felsbereich?> setzen)
    {
        var zeile = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        zeile.AddThemeConstantOverride("separation", 4);
        var an = new CheckBox { ButtonPressed = wert is not null, FocusMode = Control.FocusModeEnum.None, TooltipText = "Bereich an oder aus" };
        Zahlfeld Feld(double v, string was) => new()
        {
            MinValue = min, MaxValue = max, Step = schritt, Suffix = einheit, Value = v, Editable = wert is not null,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(64, 0), TooltipText = $"{tipp} ({was})",
        };
        var von = Feld(wert?.Von ?? vonVorgabe, "von");
        var bis = Feld(wert?.Bis ?? bisVorgabe, "bis");
        void Uebernehmen() => setzen(an.ButtonPressed ? new Felsbereich(Math.Min(von.Value, bis.Value), Math.Max(von.Value, bis.Value)) : null);
        an.Toggled += ja =>
        {
            von.Editable = bis.Editable = ja;
            Uebernehmen();
        };
        von.ValueChanged += _ => Uebernehmen();
        bis.ValueChanged += _ => Uebernehmen();
        zeile.AddChild(an);
        zeile.AddChild(von);
        zeile.AddChild(bis);
        Zeile(raster, name, zeile);
    }

    /// <summary>Der Filter wie bei Gelände und Boden, darunter der Höhenfilter (P3).</summary>
    private void FelsFilter()
    {
        FilterAbschnitt(() => _stadt.FelsFilter, f => _stadt.FelsFilter = f);
        var raster = ZweiSpalten();
        BereichZeile(raster, "Höhe", _stadt.FelsHoehenbereich, Hoehenraster.Tiefste, Hoehenraster.Hoechste, 0.5, "m", 0, 10,
            "Nur wo das Gelände an der Mitte des Stücks so hoch liegt (Meter, von–bis) – gilt für Streuen, Stempel, Radierer und Neu verteilen",
            b => _stadt.FelsHoehenbereich = b);
    }

    /// <summary>Beim Radierer und bei „Neu verteilen“: die Formen und das Häkchen „nur diese Formen“.</summary>
    private void FelsFormenWahl(Stadtansicht.FelsArt modus)
    {
        Hauptmenue.Text(_eigenschaften, "Formen", 15, Gold);
        var nur = new CheckBox { Text = "Nur diese Formen", ButtonPressed = _stadt.FelsNurFormen, FocusMode = Control.FocusModeEnum.None,
            TooltipText = modus == Stadtansicht.FelsArt.Radieren
                ? "Der Radierer nimmt nur Stücke der angehakten Formen weg, alle anderen bleiben"
                : "Neu verteilt werden nur Stücke der angehakten Formen" };
        nur.AddThemeFontSizeOverride("font_size", 14);
        nur.Toggled += an => _stadt.FelsNurFormen = an;
        _eigenschaften.AddChild(nur);
        FelsFormenGitter();
    }

    /// <summary>Der Abschnitt „Vorlage“: Gruppe oder Einzelstücke (mit Formen), Gestein der neuen Stücke, Grundgestein der Karte.</summary>
    private void FelsVorlage(Felsgruppe? gruppe)
    {
        Hauptmenue.Text(_eigenschaften, "Vorlage", 15, Gold);
        var raster = ZweiSpalten();
        var gruppen = _stadt.FelsKatalog.Alle; // P5: erst die festen, dann die eigenen aus dem gemeinsamen Katalog
        var vorlage = new OptionButton { FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Eine feste Gruppe (Wand, Kuppe, Rippe, Große Wand aus 5 bis 8 Stücken), eine eigene Gruppe (gewählte Stücke › „Als Gruppe ablegen“) " +
                          "oder einzelne Stücke aus den Formen darunter" };
        vorlage.AddItem("Einzelstücke");
        foreach (var g in gruppen) vorlage.AddItem(g.Eigen ? $"Eigene: {g.Name} ({g.Stuecke.Count} Stücke)" : $"Gruppe {g.Name} ({g.Stuecke.Count} Stücke)");
        vorlage.Selected = gruppe is null ? 0 : gruppen.ToList().FindIndex(g => g.Kennung == gruppe.Kennung) + 1;
        vorlage.ItemSelected += i =>
        {
            _stadt.FelsGruppe = i == 0 ? null : gruppen[(int)i - 1].Kennung;
            EigenschaftenZeigen(neu: true);
        };
        Zeile(raster, "Vorlage", vorlage);

        var gestein = new OptionButton { FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Das Gestein der neuen Stücke – „wie die Karte“ folgt dem Grundgestein, auch wenn es sich später ändert" };
        gestein.AddItem($"wie die Karte ({GesteinName(_entwurf.Grundgestein.Wirksam)})");
        foreach (var g in Felsformen.Gesteine) gestein.AddItem(GesteinName(g));
        gestein.Selected = _stadt.FelsGestein is { } eigen ? Felsformen.Gesteine.ToList().IndexOf(eigen) + 1 : 0;
        gestein.ItemSelected += i =>
        {
            _stadt.FelsGestein = i == 0 ? null : Felsformen.Gesteine[(int)i - 1];
            EigenschaftenZeigen(neu: true);
        };
        Zeile(raster, "Gestein", gestein);

        var grund = new OptionButton { FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Die Grundgesteinsart der Karte: Hang-Fels im Boden und alle Stücke ohne eigenes Gestein (ein Schritt für Strg+Z)" };
        foreach (var g in Felsformen.Gesteine) grund.AddItem(GesteinName(g));
        grund.Selected = Felsformen.Gesteine.ToList().IndexOf(_entwurf.Grundgestein.Wirksam);
        grund.ItemSelected += i =>
        {
            var neu = Felsformen.Gesteine[(int)i];
            _entwurf.GrundgesteinSetzen(neu == _entwurf.Grundgestein.Wirksam && _entwurf.Grundgestein.Gestein is null ? null : neu);
            EigenschaftenZeigen(neu: true);
        };
        Zeile(raster, "Karte", grund);

        if (gruppe is null)
        {
            FelsFormenGitter();
            return;
        }
        if (gruppe.Eigen) FelsVorlageVerwalten(gruppe); // P5: Umbenennen und Löschen (Kartenwerkstatt.Felskatalog.cs)
        // P5: Spiegeln und Verbinden – nur für Gruppen
        var spiegeln = new CheckBox { Text = "Zufällig spiegeln", ButtonPressed = _stadt.FelsSpiegeln, FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Jede gesetzte Gruppe wird mit halber Wahrscheinlichkeit an ihrer Längsachse gespiegelt (die Lage der Stücke, nicht die Modelle) – " +
                          "zusammen mit der Drehung fällt die Wiederholung derselben Gruppe weniger auf" };
        spiegeln.AddThemeFontSizeOverride("font_size", 14);
        spiegeln.Toggled += an => _stadt.FelsSpiegeln = an;
        _eigenschaften.AddChild(spiegeln);
        var verbinden = new CheckBox { Text = "Stücke verbinden (gemeinsam wählen)", ButtonPressed = _stadt.FelsVerbinden, FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Die Stücke jeder gesetzten Gruppe bekommen eine gemeinsame Nummer: Mit dem Werkzeug Auswahl wählt „Ganze Gruppe wählen“ sie zusammen, " +
                          "„Gruppe auflösen“ trennt sie wieder. Die gesetzten Stücke sind danach eigenständig (keine Verknüpfung zur Vorlage)." };
        verbinden.AddThemeFontSizeOverride("font_size", 14);
        verbinden.Toggled += an => _stadt.FelsVerbinden = an;
        _eigenschaften.AddChild(verbinden);
    }

    /// <summary>Je Form ein Häkchen (zwei Spalten); nie leer – ohne Haken gilt der Block.</summary>
    private void FelsFormenGitter()
    {
        var formen = new GridContainer { Columns = 2 };
        formen.AddThemeConstantOverride("h_separation", 12);
        _eigenschaften.AddChild(formen);
        foreach (var f in Felsformen.Alle)
        {
            var form = f.Kennung;
            var haken = new CheckBox { Text = f.Name, ButtonPressed = _stadt.FelsFormen.Contains(form), FocusMode = Control.FocusModeEnum.None,
                TooltipText = $"{f.Name}: {Komma(f.Breite, "0.0")} × {Komma(f.Tiefe, "0.0")} m, {Komma(f.Hoehe, "0.0")} m hoch" + (f.Gerichtet ? " – mit Schauseite" : "") };
            haken.AddThemeFontSizeOverride("font_size", 13);
            haken.Toggled += an =>
            {
                if (an && !_stadt.FelsFormen.Contains(form)) _stadt.FelsFormen.Add(form);
                if (!an) _stadt.FelsFormen.Remove(form);
                if (_stadt.FelsFormen.Count == 0) _stadt.FelsFormen.Add(Felspinsel.FormVorgabe); // nie leer (sonst gälte die Vorgabe unsichtbar)
            };
            formen.AddChild(haken);
        }
    }
}
