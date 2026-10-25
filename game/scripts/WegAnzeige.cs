using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Wegtafel (Kartenwerkstatt, Schritt 5d): Ein Klick auf einen Weg zeigt seinen Belag Abschnitt für Abschnitt (Stufe, Zustand bzw. Austritt, Verkehr, Tempo
/// der Karren) und die laufenden Bauaufträge. Mit zwei Reglern wählt man einen Abschnitt (in Stücken zu 5 m; er erscheint als goldenes Band im Bild) und lässt ihn mit
/// Kies oder Pflaster ausbauen – Preis, Bauzeit und der Grund, warum es nicht geht, kommen aus <see cref="Wegebau.Vorschau"/> – oder beantragt im Rat, dass die
/// Stadt ihn pflastert (<see cref="Wegebau.AntragPrognose"/>). Die Tafel rechnet nichts selbst.
/// </summary>
public partial class WegAnzeige : PanelContainer
{
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.72f, 0.72f, 0.72f);
    private static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);
    private static readonly Color Hell = new(0.92f, 0.88f, 0.80f);
    private const int Breite = 380;

    private Simulation? _simulation;
    private Konto? _spieler;
    private Stadtansicht? _stadt;
    private VBoxContainer _inhalt = null!;
    private VBoxContainer _unten = null!;
    private Label _auswahlText = null!;
    private int _weg = -1;
    private double _von, _bis;
    private bool _veraltet;
    private string _meldung = "";
    private bool _meldungErfolg;

    /// <summary>Ein Befehl oder Antrag wurde gemeldet (für das Protokoll der Hauptszene).</summary>
    public event Action<string>? Gemeldet;

    public event Action? Geschlossen;

    public bool Aktiv => Visible;

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.15f, 0.13f, 0.97f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
        });
        var rand = new MarginContainer();
        foreach (var seite in new[] { "left", "right", "top", "bottom" })
            rand.AddThemeConstantOverride($"margin_{seite}", 16);
        AddChild(rand);
        _inhalt = new VBoxContainer { CustomMinimumSize = new Vector2(Breite, 0) };
        _inhalt.AddThemeConstantOverride("separation", 6);
        rand.AddChild(_inhalt);
        Rollbereich.Einhuellen(this);
        Hide();
    }

    public void Verbinden(Simulation simulation, Konto spieler, Stadtansicht stadt)
    {
        _simulation = simulation;
        _spieler = spieler;
        _stadt = stadt;
        if (simulation.Wirtschaft is { } w) w.Wegebau.BelagGeaendert += () => _veraltet = true;
    }

    /// <summary>Öffnet die Tafel für einen Weg; der Abschnitt von etwa 50 m liegt um <paramref name="stelle"/> (Meter entlang des Verlaufs).</summary>
    public void Oeffnen(int weg, double stelle)
    {
        if (_simulation?.Wirtschaft is not { } w || _stadt is null || !w.Wegebau.IstAktiv || w.Karte is not { } karte || weg < 0 || weg >= karte.Wege.Count) return;
        var daten = w.Wegebau.Daten!;
        var laenge = karte.Wege[weg].Laenge;
        var stueck = daten.Stueck;
        _weg = weg;
        var von = Math.Floor((stelle - 25) / stueck) * stueck;
        von = Math.Clamp(von, 0, Math.Max(0, Math.Floor(laenge / stueck) * stueck - stueck));
        _von = von;
        _bis = Math.Min(laenge, von + Math.Min(50, daten.HoechstensMeter));
        _meldung = "";
        Aufbauen();
        Show();
        Rollbereich.RechtsAnheften(this); // rechts unter dem Zeitstrahl, über der Betriebsleiste
    }

    public void Schliessen()
    {
        if (!Visible) return;
        _stadt?.WegAuswahlMarkieren(-1, 0, 0);
        _weg = -1;
        Hide();
        Geschlossen?.Invoke();
    }

    private double _seitPruefung;
    private string _stand = "";

    public override void _Process(double delta)
    {
        if (!Visible) return;
        if (_veraltet)
        {
            _veraltet = false;
            Aufbauen();
            return;
        }
        // Vermögen, Ratsantrag und Bauaufträge ändern sich von selbst: dann nur den unteren Teil neu aufbauen (die Regler bleiben unter der Maus)
        _seitPruefung += delta;
        if (_seitPruefung < 0.5 || _simulation?.Wirtschaft is not { } w || _spieler is null) return;
        _seitPruefung = 0;
        if (Stand(w) != _stand && _unten is not null && IsInstanceValid(_unten)) Auswahl(w);
    }

    /// <summary>Woran die Tafel merkt, dass sich etwas von selbst geändert hat.</summary>
    private string Stand(Stadtwirtschaft w) =>
        $"{w.Wegebau.Antrag?.Antragsteller.Inhaber}|{Zahlenformat.Ganz(_spieler!.Vermoegen)}|{w.Wegebau.Baustellen.Count}|{_spieler.Aktionspunkte.Vorrat}";

    private void Befehlen(Befehl befehl)
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null) return;
        var ergebnis = w.Ausfuehren(_spieler, befehl);
        _meldung = ergebnis.Erfolg ? ergebnis.Meldung : "✗ " + ergebnis.Meldung;
        _meldungErfolg = ergebnis.Erfolg;
        Gemeldet?.Invoke(_meldung);
        Aufbauen();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau
    // ═════════════════════════════════════════════════════════════════════════

    private void Aufbauen()
    {
        if (_simulation?.Wirtschaft is not { } w || _stadt is null || _weg < 0 || w.Karte is not { } karte || _weg >= karte.Wege.Count) return;
        var bau = w.Wegebau;
        var weg = karte.Wege[_weg];
        var laenge = weg.Laenge;
        var stueck = bau.Daten!.Stueck;
        UiHilfe.KinderEntfernen(_inhalt);

        Hauptmenue.Text(_inhalt, _stadt.WegName(_weg), 22, Gold, zentriert: true);
        Hauptmenue.Text(_inhalt, $"{Kartenweg.Anzeigename(weg.Klasse)} · {Zahlenformat.Ganz(laenge)} m lang · Wegekasse der Stadt: {Zahlenformat.Ganz(bau.Wegekasse)} {w.Daten.Allgemein.Waehrung}",
            13, Grau, zentriert: true, umbruchBreite: Breite);

        // ── der Belag Abschnitt für Abschnitt ──
        Hauptmenue.Text(_inhalt, "Belag", 16, Gold);
        var abschnitte = bau.Abschnitte(_weg);
        foreach (var a in abschnitte.Take(10))
        {
            var zustand = Wegbelag.IstBefestigt(a.Stufe) ? $"Zustand {Math.Round(a.Zustand * 100)} %" : $"ausgetreten {Math.Round(a.Austritt * 100)} %";
            var bauText = a.Baustelle ? " · wird gebaut" : "";
            Hauptmenue.Text(_inhalt, $"{Zahlenformat.Ganz(a.Von)}–{Zahlenformat.Ganz(a.Bis)} m: {Wegbelag.Anzeigename(a.Stufe)}, {zustand} · Verkehr {Zahlenformat.Ganz(a.Verkehr)}/Jahr · Karren {Math.Round(a.Tempo * 100)} %{bauText}",
                13, a.Baustelle ? Gold : Hell, umbruchBreite: Breite);
        }
        if (abschnitte.Count > 10) Hauptmenue.Text(_inhalt, $"… und {abschnitte.Count - 10} weitere Abschnitte", 12, Grau);
        foreach (var b in bau.Baustellen.Where(b => b.Weg == _weg))
            Hauptmenue.Text(_inhalt, $"Baustelle {Zahlenformat.Ganz(b.Von)}–{Zahlenformat.Ganz(b.Bis)} m: {Wegbelag.Anzeigename(b.Stufe)} für {b.BauherrName}", 12, Gold, umbruchBreite: Breite);
        Hauptmenue.Text(_inhalt, "Viel Verkehr tritt Trampelpfade zu Fahrwegen aus, ohne Verkehr wächst alles zu. Kies und Pflaster halten, verfallen aber langsam.", 12, Grau, umbruchBreite: Breite);

        // ── Abschnitt wählen ──
        _inhalt.AddChild(new HSeparator());
        Hauptmenue.Text(_inhalt, "Abschnitt wählen", 16, Gold);
        _auswahlText = Hauptmenue.Text(_inhalt, "", 14, Hell, umbruchBreite: Breite);
        Regler("von", _von, laenge, stueck, wert => { _von = wert; if (_bis <= _von) _bis = Math.Min(laenge, _von + stueck); Auswahl(w); });
        Regler("bis", _bis, laenge, stueck, wert => { _bis = wert; if (_von >= _bis) _von = Math.Max(0, _bis - stueck); Auswahl(w); });
        _unten = new VBoxContainer();
        _unten.AddThemeConstantOverride("separation", 6);
        _inhalt.AddChild(_unten);
        Auswahl(w);

        Hauptmenue.Knopf(_inhalt, "Schließen (Esc)", Schliessen).SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    private void Regler(string name, double wert, double laenge, double stueck, Action<double> geaendert)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 8);
        _inhalt.AddChild(zeile);
        Hauptmenue.Text(zeile, name, 14, Grau).CustomMinimumSize = new Vector2(34, 0);
        var regler = new HSlider { MinValue = 0, MaxValue = laenge, Step = stueck, Value = wert, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(250, 22), FocusMode = FocusModeEnum.None };
        regler.ValueChanged += v => geaendert(v);
        zeile.AddChild(regler);
    }

    /// <summary>Der gewählte Abschnitt hat sich geändert: Markierung im Bild, Text und die Knöpfe darunter.</summary>
    private void Auswahl(Stadtwirtschaft w)
    {
        var bau = w.Wegebau;
        _stadt!.WegAuswahlMarkieren(_weg, _von, _bis);
        UiHilfe.KinderEntfernen(_unten);
        var waehrung = w.Daten.Allgemein.Waehrung;
        var verkehr = bau.MittlererVerkehr(_weg, _von, _bis);
        var stufen = bau.Abschnitte(_weg).Where(a => a.Bis > _von && a.Von < _bis).Select(a => Wegbelag.Anzeigename(a.Stufe)).Distinct();
        _auswahlText.Text = $"{Zahlenformat.Ganz(_von)}–{Zahlenformat.Ganz(_bis)} m ({Zahlenformat.Ganz(_bis - _von)} m) · jetzt {string.Join(" und ", stufen)} · Verkehr im Mittel {Zahlenformat.Ganz(verkehr)}/Jahr";

        // ── Ausbau auf eigene Kosten ──
        foreach (var stufe in new[] { Belagstufe.Kies, Belagstufe.Pflaster })
        {
            var v = bau.Vorschau(_spieler, _weg, _von, _bis, stufe);
            var text = v.Verboten is not null && v.Kosten <= 0
                ? $"{Wegbelag.Anzeigename(stufe)}"
                : $"{Wegbelag.Anzeigename(stufe)} bauen lassen\n{Zahlenformat.Ganz(v.Kosten)} {waehrung} · {BauAnzeige.BauzeitText(v.BauzeitMinuten)} · {Zahlenformat.Ganz(v.Meter)} m";
            var knopf = new Button { Text = text, FocusMode = FocusModeEnum.None, Disabled = v.Verboten is not null, Alignment = HorizontalAlignment.Left, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            knopf.AddThemeFontSizeOverride("font_size", 14);
            var s = stufe;
            knopf.Pressed += () => Befehlen(new WegAusbauen(_weg, _von, _bis, s));
            _unten.AddChild(knopf);
            if (v.Verboten is not null) Hauptmenue.Text(_unten, "✗ " + v.Verboten, 12, Rot, umbruchBreite: Breite);
        }

        // ── Ratsantrag ──
        _unten.AddChild(new HSeparator());
        Hauptmenue.Text(_unten, "Pflaster beim Rat beantragen", 16, Gold);
        if (bau.Antrag is { } antrag)
        {
            var (ja, nein, enthaltung, bm) = bau.AntragPrognose(antrag.Antragsteller, antrag.Weg, antrag.Von, antrag.Bis);
            var durch = Stadtrat.IstAngenommen(ja, nein, bm);
            Hauptmenue.Text(_unten, $"Dieses Jahr liegt schon ein Antrag vor: {antrag.Antragsteller.Inhaber} will {w.Karte!.Wege[antrag.Weg].Name switch { "" => "einen Weg", var n => n }} " +
                                    $"({Zahlenformat.Ganz(antrag.Von)}–{Zahlenformat.Ganz(antrag.Bis)} m) pflastern lassen. Stand heute {ja}:{nein}{(enthaltung > 0 ? $", {enthaltung} Enthaltung" : "")} → {(durch ? "würde angenommen" : "würde abgelehnt")}.",
                13, durch ? Gruen : Rot, umbruchBreite: Breite);
        }
        else if (w.Rat.IstAktiv && w.Rat.AmtVon(_spieler!) is not null)
        {
            var (ja, nein, enthaltung, bm) = bau.AntragPrognose(_spieler!, _weg, _von, _bis);
            var durch = Stadtrat.IstAngenommen(ja, nein, bm);
            var v = bau.Vorschau(null, _weg, _von, _bis, Belagstufe.Pflaster);
            Hauptmenue.Text(_unten, $"Die Stadt zahlt aus der Wegekasse ({Zahlenformat.Ganz(v.Kosten)} {waehrung}). Abgestimmt wird am Jahresende; bei Erfolg wächst euer Ansehen. " +
                                    $"Stand heute: {ja} dafür, {nein} dagegen{(enthaltung > 0 ? $", {enthaltung} Enthaltung" : "")} → {(durch ? "würde angenommen" : "würde abgelehnt")}.",
                13, durch ? Gruen : Rot, umbruchBreite: Breite);
            var antragKnopf = new Button { Text = "Antrag stellen", FocusMode = FocusModeEnum.None, Disabled = v.Kosten <= 0 };
            antragKnopf.Pressed += () => Befehlen(new WegPflasterBeantragen(_weg, _von, _bis));
            UiHilfe.ApKosten(antragKnopf, w, _spieler!, new WegPflasterBeantragen(_weg, _von, _bis));
            _unten.AddChild(antragKnopf);
        }
        else
            Hauptmenue.Text(_unten, "Anträge stellen nur Mitglieder des Rates.", 13, Grau);

        if (_meldung != "") Hauptmenue.Text(_unten, _meldung, 13, _meldungErfolg ? Gruen : Rot, umbruchBreite: Breite);
        _stand = Stand(w);
    }
}
