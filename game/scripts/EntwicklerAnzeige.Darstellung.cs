using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Entwickler;
using Reichsgilde.Core.Mehrspieler;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die Reiter <b>Wetter und Darstellung</b> und <b>Messwerte</b> des Entwicklerfensters. Alles hier ist reine Darstellung bzw. Lesen: Wetter und Jahreszeit
/// gibt die Stadtansicht vor (Stadtansicht.Entwickler.cs, derselbe Weg wie <c>--wetter</c> und <c>--jahreszeit</c>), Dunst und Unschärfe stellt
/// <see cref="Fenster"/> ohne Speichern um (die Einstellungen der Datei bleiben unberührt).
/// </summary>
public sealed partial class EntwicklerAnzeige
{
    private readonly List<(Wetterlage? Lage, Button Knopf)> _wetterKnoepfe = new();
    private readonly List<(Jahreszeit? Zeit, Button Knopf)> _zeitKnoepfe = new();
    private OptionButton _dunst = null!, _unschaerfe = null!, _nachtnebel = null!, _morgennebel = null!;
    private Label _nebelHeute = null!;

    /// <summary>Die Stufen der Auswahl „Morgennebel“: Automatisch (gewürfelt) und vier feste Stärken.</summary>
    private static readonly (string Name, float? Staerke)[] Nebelstufen = { ("Automatisch", null), ("Keiner", 0f), ("Leicht", 0.35f), ("Mittel", 0.65f), ("Stark", 1f) };
    private CheckBox _wegenetz = null!, _bauzonen = null!;

    private Label _werte = null!, _pruefsumme = null!;
    private RichTextLabel _ki = null!;
    private double _seitLichter = 10;
    private int _lichter;
    private int _messwerteNummer = -1;

    // ═════════════════════════════════════════════════════════════════════════
    //  Reiter: Wetter und Darstellung
    // ═════════════════════════════════════════════════════════════════════════

    private void DarstellungReiter()
    {
        var s = NeuerReiter("Wetter und Darstellung");
        Hauptmenue.Text(s, "Nur Darstellung, nichts davon wird gespeichert oder von der Simulation gelesen.", 13, Grau);

        var z = Zeile(s, "Wetter", "Gibt eine Wetterlage fest vor (wie --wetter=…); „Automatisch“ lässt das Wetter wieder von selbst laufen.");
        z.AddThemeConstantOverride("separation", 4);
        var reihe1 = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reihe1.AddThemeConstantOverride("h_separation", 4);
        z.AddChild(reihe1);
        WetterKnopf(reihe1, null, "Automatisch");
        foreach (var lage in Enum.GetValues<Wetterlage>()) WetterKnopf(reihe1, lage, Wetterablauf.Name(lage));

        z = Zeile(s, "Blitz", "Löst einen Blitz aus, auch bei schönem Wetter. Das Licht des Blitzes gibt es in den Wettereffekten Mittel und Hoch, nicht im Innenraum.");
        Knopf(z, "Blitz auslösen", () => { _stadt.BlitzAusloesen(); Melden("Blitz."); });

        z = Zeile(s, "Jahreszeit", "Die Jahreszeit der Ansicht: Wiese, Laub, Äcker, Schnee. Der Kalender und die Simulation bleiben, wie sie sind.");
        var reihe2 = new HBoxContainer();
        reihe2.AddThemeConstantOverride("separation", 4);
        z.AddChild(reihe2);
        ZeitKnopf(reihe2, null, "Automatisch");
        foreach (var jahreszeit in Enum.GetValues<Jahreszeit>()) ZeitKnopf(reihe2, jahreszeit, KalenderTexte.Name(jahreszeit));

        s.AddChild(new HSeparator());
        z = Zeile(s, "Dunst und Nebel", "Morgendunst und Bodennebel der Stadt (Hoch mit Lichtbahnen). Der Nebel des Wetters (Wetter „Nebel“) bleibt davon unberührt.");
        _dunst = Auswahl(z, new[] { "Aus", "Niedrig", "Hoch" }, (int)Fenster.AktuellerDunst, i => Fenster.DunstSetzen((Morgendunst)i, speichern: false));
        z = Zeile(s, "Nachtnebel", "Nebel auch bei Nacht (wie --nachtnebel=…). An (neu): Nach Einbruch der Dunkelheit wächst der Nebel einer ruhigen Nacht weiter, so dunkel wie eine unbeleuchtete Wand, mit Lichthof um Laternen und Feuer. Aus (alt, Lösung A vom 29.10.2026): nachts klare Luft, der Nebel kommt erst mit der Morgendämmerung. Zum Vergleich des hellen Saums am Fuß von Häusern und Mauern.");
        _nachtnebel = Auswahl(z, new[] { "Aus (alt, Lösung A)", "An (neu)" }, _stadt.Nachtnebel ? 1 : 0, i => { _stadt.Nachtnebel = i == 1; Melden(i == 1 ? "Nachtnebel an." : "Nachtnebel aus (Lösung A)."); });
        z = Zeile(s, "Morgennebel", "Wie dicht der Nebel dieses Spieltags wird (wie --morgennebel=…). Automatisch: gewürfelt nach Jahreszeit (Herbst am häufigsten) und nur nach einer klaren, ruhigen Nacht; Wind und Regen lösen ihn auf. Am dichtesten um den Sonnenaufgang, danach in 1¾ bis 3 Stunden aufgelöst.");
        _morgennebel = Auswahl(z, Nebelstufen.Select(n => n.Name).ToArray(), 0, i => { _stadt.MorgennebelVorgabe = Nebelstufen[i].Staerke; Melden($"Morgennebel: {Nebelstufen[i].Name}."); });
        _nebelHeute = Hauptmenue.Text(z, "", 13, Grau);
        _nebelHeute.VerticalAlignment = VerticalAlignment.Center;
        z = Zeile(s, "Unschärfe", "Miniatur-Unschärfe (Tilt-Shift) oben und unten im Bild.");
        _unschaerfe = Auswahl(z, new[] { "Aus", "Leicht", "Stark" }, (int)Fenster.AktuelleUnschaerfe, i => Fenster.UnschaerfeSetzen((Miniaturunschaerfe)i, speichern: false));

        s.AddChild(new HSeparator());
        z = Zeile(s, "Hilfsanzeigen");
        _wegenetz = new CheckBox { Text = "Wegenetz der Passanten", FocusMode = FocusModeEnum.None, TooltipText = "Gassennetz: Kanten pink, Knoten gelb (wie --wegenetz=1)." };
        _wegenetz.Toggled += an => _stadt.WegenetzSichtbar = an;
        z.AddChild(_wegenetz);
        _bauzonen = new CheckBox { Text = "Bauzonen", FocusMode = FocusModeEnum.None, TooltipText = "Die Bauzonen der Karte als farbige Flächen mit Raster, auch ohne Baumodus." };
        _bauzonen.Toggled += an => _stadt.BauzonenHilfe = an;
        z.AddChild(_bauzonen);
    }

    private void WetterKnopf(Container eltern, Wetterlage? lage, string text)
    {
        var knopf = new Button { Text = text, ToggleMode = true, FocusMode = FocusModeEnum.None };
        knopf.Pressed += () => { _stadt.WetterErzwingen(lage); Melden($"Wetter: {text}."); DarstellungAuffrischen(); };
        eltern.AddChild(knopf);
        _wetterKnoepfe.Add((lage, knopf));
    }

    private void ZeitKnopf(Container eltern, Jahreszeit? jahreszeit, string text)
    {
        var knopf = new Button { Text = text, ToggleMode = true, FocusMode = FocusModeEnum.None };
        knopf.Pressed += () => { _stadt.JahreszeitErzwingen(jahreszeit); Melden($"Jahreszeit der Ansicht: {text}."); DarstellungAuffrischen(); };
        eltern.AddChild(knopf);
        _zeitKnoepfe.Add((jahreszeit, knopf));
    }

    private static OptionButton Auswahl(Container eltern, string[] eintraege, int gewaehlt, Action<int> geaendert)
    {
        var knopf = new OptionButton { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(140, 0) };
        foreach (var e in eintraege) knopf.AddItem(e);
        knopf.Select(gewaehlt);
        knopf.ItemSelected += i => geaendert((int)i);
        eltern.AddChild(knopf);
        return knopf;
    }

    /// <summary>Gleicht die Schalter dieses Reiters mit dem tatsächlichen Stand ab (Wetter und Jahreszeit, Dunst, Unschärfe; auch wenn sie anderswo geändert wurden).</summary>
    private void DarstellungAuffrischen()
    {
        foreach (var (lage, knopf) in _wetterKnoepfe) knopf.SetPressedNoSignal(lage == _stadt.WetterErzwungen);
        foreach (var (zeit, knopf) in _zeitKnoepfe) knopf.SetPressedNoSignal(zeit == _stadt.JahreszeitErzwungen);
        if (_dunst.Selected != (int)Fenster.AktuellerDunst) _dunst.Select((int)Fenster.AktuellerDunst);
        if (_unschaerfe.Selected != (int)Fenster.AktuelleUnschaerfe) _unschaerfe.Select((int)Fenster.AktuelleUnschaerfe);
        if (_nachtnebel.Selected != (_stadt.Nachtnebel ? 1 : 0)) _nachtnebel.Select(_stadt.Nachtnebel ? 1 : 0);
        var nebelStufe = Array.FindIndex(Nebelstufen, n => n.Staerke == _stadt.MorgennebelVorgabe);
        if (nebelStufe >= 0 && _morgennebel.Selected != nebelStufe) _morgennebel.Select(nebelStufe);
        var (heute, grund) = _stadt.MorgennebelHeute;
        _nebelHeute.Text = $"heute {heute:0.00} ({grund})";
        if (_wegenetz.ButtonPressed != _stadt.WegenetzSichtbar) _wegenetz.SetPressedNoSignal(_stadt.WegenetzSichtbar);
        if (_bauzonen.ButtonPressed != _stadt.BauzonenHilfe) _bauzonen.SetPressedNoSignal(_stadt.BauzonenHilfe);
        if (_reiter.CurrentTab == _messwerteNummer) MesswerteAuffrischen();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Reiter: Messwerte
    // ═════════════════════════════════════════════════════════════════════════

    private void MesswerteReiter()
    {
        var s = NeuerReiter("Messwerte");
        _messwerteNummer = _reiter.GetTabCount() - 1;
        _werte = Hauptmenue.Text(s, "", 14, umbruchBreite: Breite - 60);
        var z = Zeile(s, "Prüfsumme", "Eine Kurzform (8 Zeichen) des SHA-256 über den Spielstand, ohne Speicherzeit, Titel, Tempo, Spielerkonto und Warteschlange. Zwei Rechner im Gleichschritt müssen sie gleich haben. Nur auf Knopfdruck, sie rechnet den ganzen Spielstand durch.");
        Knopf(z, "Prüfsumme berechnen", () =>
        {
            var summe = Zustandspruefsumme.Kurz(_sim);
            _pruefsumme.Text = summe is null ? "– (während einer Ratssitzung gibt es keine)" : $"{summe}  ({_sim.Zeit.DatumText}, {_sim.Zeit.UhrzeitText})";
        });
        _pruefsumme = Hauptmenue.Text(z, "–", 15, Gold);
        _pruefsumme.VerticalAlignment = VerticalAlignment.Center;

        z = Zeile(s, "KI-Einblick", "Was die KI-Häuser rechnen und zuletzt entschieden haben (nur lesen, ändert nichts).");
        Knopf(z, "Auffrischen", KiAuffrischen);
        _ki = new RichTextLabel
        {
            FitContent = false, ScrollActive = true, SelectionEnabled = true, CustomMinimumSize = new Vector2(Breite - 60, 190),
            TooltipText = "Pro KI-Haus: Vermögen, Hochrechnung, Profil, Betriebe mit Wert je Arbeitsminute und die letzten Entscheidungen mit Begründung.",
        };
        _ki.AddThemeFontSizeOverride("normal_font_size", 13);
        s.AddChild(_ki);
        _reiter.TabChanged += tab =>
        {
            if (tab == _messwerteNummer) { _seitLichter = 10; MesswerteAuffrischen(); KiAuffrischen(); }
        };
    }

    private void KiAuffrischen()
    {
        if (_sim?.Wirtschaft is not { } w) return;
        var einblicke = KiEinblick.Alle(w);
        _ki.Text = einblicke.Count == 0 ? "In dieser Partie gibt es keine KI-Häuser." : string.Join("\n\n", einblicke.Select(e => string.Join("\n", e.Zeilen)));
    }

    private void MesswerteAuffrischen()
    {
        if (_sim is null) return;
        _seitLichter += 0.5;
        if (_seitLichter >= 2) // der ganze Szenenbaum: nur alle zwei Sekunden
        {
            _seitLichter = 0;
            _lichter = _stadt.LichterImBild();
        }
        static string Mb(double byte_) => $"{byte_ / 1048576.0:0} MB";
        var aufrufe = RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame);
        var objekte = RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalObjectsInFrame);
        var flaechen = RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalPrimitivesInFrame);
        var takt = _sim.Takt;
        _werte.Text =
            $"Bildrate {Engine.GetFramesPerSecond():0} /s · Bildzeit {Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000:0.0} ms (Zeichnen {RenderingServer.GetFrameSetupTimeCpu():0.0} ms)\n"
            + $"Zeichenaufrufe {Zahlenformat.Ganz(aufrufe)} · Objekte {Zahlenformat.Ganz(objekte)} · Dreiecke {Zahlenformat.Ganz(flaechen)}\n"
            + $"Lichter im Bild {_lichter} · Knoten {Zahlenformat.Ganz(Performance.GetMonitor(Performance.Monitor.ObjectNodeCount))}\n"
            + $"Speicher: Godot {Mb(OS.GetStaticMemoryUsage())} · .NET {Mb(GC.GetTotalMemory(false))} · Grafik {Mb(RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.VideoMemUsed))}"
            + $" (Texturen {Mb(RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TextureMemUsed))})\n"
            + $"Spielzeit {_sim.Zeit.DatumText}, {_sim.Zeit.UhrzeitText} · Minute {Zahlenformat.Ganz(_sim.Zeit.VergangeneMinuten)} · Tempo {takt.Tempo}×{(takt.IstPausiert ? " (Pause)" : "")}"
            + $" · {Math.Round(takt.TicksProSekunde, 1)} Ticks/s";
    }
}
