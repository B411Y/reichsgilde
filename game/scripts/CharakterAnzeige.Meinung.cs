using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Meinungen und Aktivitäten im Charakterfenster (Charaktersystem Teil 2, PR #231): die Zeile „Meinung über Euch“ mit Balken und
/// Gründe-Tooltip bei jeder wichtigen Person (außer dem eigenen Oberhaupt) und die Liste der Aktivitäten mit ihr.
/// </summary>
public partial class CharakterAnzeige
{
    /// <summary>Eine Meldung (Ergebnis einer Aktivität) für das Protokoll der Hauptszene.</summary>
    public event Action<string>? Gemeldet;

    private Konto? _spieler;
    private ICharakter? _aktivMit;   // gesetzt = statt der Talente und Züge die Aktivitätenliste zeigen
    private double _geschenk = 100;
    private string _meldung = "";
    private bool _meldungGut;

    /// <summary>Ist das eine wichtige Person und nicht das eigene Oberhaupt? Nur dann gibt es Meinung und Aktivitäten.</summary>
    private bool MeinungMoeglich(Stadtwirtschaft w, ICharakter c) =>
        _spieler is not null && w.Meinungen.Oberhaupt(_spieler)?.Kennung != c.Kennung
        && w.Meinungen.WichtigePersonen().Any(p => p.Charakter.Kennung == c.Kennung);

    private void MeinungBauen(Container daten, Stadtwirtschaft w, ICharakter c)
    {
        if (_spieler is null || !MeinungMoeglich(w, c)) return;
        var wert = w.Meinungen.Meinung(c, _spieler);
        var zeile = new HBoxContainer { MouseFilter = MouseFilterEnum.Stop, TooltipText = MeinungsHilfe.Tipp(w, c, _spieler) };
        zeile.AddThemeConstantOverride("separation", 10);
        daten.AddChild(zeile);
        Hauptmenue.Text(zeile, "Meinung über Euch:", 14).MouseFilter = MouseFilterEnum.Ignore;
        zeile.AddChild(new MeinungsHilfe.Balken(wert, w.Meinungen.Daten?.Hoechstwert ?? 100));
        Hauptmenue.Text(zeile, Zahlenformat.MitVorzeichen(wert), 15, MeinungsHilfe.Farbe(wert)).MouseFilter = MouseFilterEnum.Ignore;
    }

    private void AktivitaetenKnopf(Container knoepfe, Stadtwirtschaft w, ICharakter c)
    {
        if (!w.Meinungen.IstAktiv || !MeinungMoeglich(w, c)) return;
        Hauptmenue.Knopf(knoepfe, "Aktivitäten", () => { _aktivMit = c; _meldung = ""; Aufbauen(); })
            .TooltipText = $"Was Ihr mit {c.Name} unternehmen könnt: Geschenk, Gastmahl, Zechen, Messe, Brettspiel, Jagd. Mehr zu [[aktivitaeten|Aktivitäten]].";
    }

    // ── Liste der Aktivitäten ───────────────────────────────────────────────

    private void AktivitaetenBauen(Stadtwirtschaft w, ICharakter c)
    {
        if (_spieler is null) return;
        var waehrung = w.Daten.Allgemein.Waehrung;
        Hauptmenue.Text(_inhalt, $"Aktivitäten mit {c.Name}", 24, Gold);
        var wert = w.Meinungen.Meinung(c, _spieler);
        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 10);
        _inhalt.AddChild(kopf);
        kopf.MouseFilter = MouseFilterEnum.Stop;
        kopf.TooltipText = MeinungsHilfe.Tipp(w, c, _spieler);
        Hauptmenue.Text(kopf, "Meinung über Euch:", 14).MouseFilter = MouseFilterEnum.Ignore;
        kopf.AddChild(new MeinungsHilfe.Balken(wert, w.Meinungen.Daten?.Hoechstwert ?? 100));
        Hauptmenue.Text(kopf, Zahlenformat.MitVorzeichen(wert), 15, MeinungsHilfe.Farbe(wert)).MouseFilter = MouseFilterEnum.Ignore;
        Hauptmenue.Text(_inhalt, "Jede Aktivität kostet Aktionspunkte; geht es gut, steigt die Meinung, geht es schief, sinkt sie. " +
                                 "Gefahr und Wirkung hängen von den Charakterzügen der Person ab.", 13, Grau, umbruchBreite: Breite - 50);
        if (_meldung != "")
            Hauptmenue.Text(_inhalt, _meldung, 14, _meldungGut ? Gruen : Rot, umbruchBreite: Breite - 50);
        _inhalt.AddChild(new HSeparator());

        foreach (var a in w.Meinungen.Aktivitaeten(_spieler, c, _geschenk))
        {
            var kasten = new VBoxContainer();
            kasten.AddThemeConstantOverride("separation", 2);
            _inhalt.AddChild(kasten);
            var reihe = new HBoxContainer();
            reihe.AddThemeConstantOverride("separation", 12);
            kasten.AddChild(reihe);
            var name = Hauptmenue.Text(reihe, a.Name, 17, Gold, umbruchBreite: 165);
            name.MouseFilter = MouseFilterEnum.Stop;
            name.TooltipText = a.Beschreibung;
            var art = a.Art;
            var geschenkArt = art == w.Meinungen.GeschenkArt;
            Zahlfeld? betrag = null;
            if (geschenkArt)
            {
                betrag = new Zahlfeld { MinValue = 10, MaxValue = 5000, Step = 10, Value = _geschenk, Suffix = waehrung, CustomMinimumSize = new Vector2(150, 0) };
                reihe.AddChild(betrag);
            }
            else reihe.AddChild(new Control { CustomMinimumSize = new Vector2(150, 0) }); // gleiche Spalten wie beim Geschenk
            var info = Hauptmenue.Text(reihe, "", 13, null, umbruchBreite: 300);
            reihe.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
            var knopf = new Button { Text = "Unternehmen", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(110, 0) };
            reihe.AddChild(knopf);
            var hinweis = Hauptmenue.Text(kasten, "", 12, Grau, umbruchBreite: Breite - 50);

            void Zeigen(Aktivitaetsangebot angebot)
            {
                var kosten = $"{angebot.Ap} AP" + (angebot.Gulden > 0 ? $" · {Zahlenformat.Ganz(angebot.Gulden)} {waehrung}" : "");
                var gefahr = angebot.Gefahr > 0 && angebot.UnglueckText != ""
                    ? $"Gefahr {Zahlenformat.Ganz(angebot.Gefahr * 100)} % – sonst {angebot.UnglueckText} ({Zahlenformat.MitVorzeichen(angebot.Unglueck)})" : "Kein Risiko";
                info.Text = $"{kosten}\n{gefahr}" +
                            $"\nGelingt: {Zahlenformat.MitVorzeichen(angebot.Wirkung)} · im Mittel {Zahlenformat.MitVorzeichen(angebot.Erwartet)}";
                hinweis.Text = angebot.Sperrgrund ?? angebot.Hinweis;
                hinweis.AddThemeColorOverride("font_color", angebot.Sperrgrund is null ? Grau : Rot);
                hinweis.Visible = hinweis.Text != "";
                knopf.Disabled = angebot.Sperrgrund is not null;
                info.Modulate = knopf.Disabled ? new Color(1, 1, 1, 0.55f) : Colors.White;
                knopf.TooltipText = angebot.Sperrgrund ?? angebot.Beschreibung;
            }
            Zeigen(a);
            if (betrag is not null)
                betrag.ValueChanged += neu =>
                {
                    _geschenk = neu;
                    if (w.Meinungen.Aktivitaet(_spieler, c, art, neu) is { } neuesAngebot) Zeigen(neuesAngebot);
                };
            knopf.Pressed += () =>
            {
                if (_simulation?.Wirtschaft is not { } wirtschaft || _spieler is null) return;
                var ergebnis = wirtschaft.Ausfuehren(_spieler, new Aktivitaet(art, c.Kennung, betrag is null ? 0 : _geschenk));
                _meldung = ergebnis.Erfolg ? ergebnis.Meldung : "✗ " + ergebnis.Meldung;
                _meldungGut = ergebnis.Erfolg;
                Gemeldet?.Invoke(_meldung);
                Aufbauen();
            };
            _inhalt.AddChild(new HSeparator());
        }
    }
}
