using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Der Bericht (Knopf „Bericht“ in der Betriebsleiste, PR #197): rechts eine Tafel mit vier Reitern – <b>Jahresrechnung</b> (dieses Jahr bisher und das
/// Vorjahr, dazu der Knopf zum ganzen Jahresbericht), <b>Rivalen</b> (alle anderen Häuser mit Vermögen, Stand und letztem Zug), <b>Umsätze</b> (jeder
/// eigene Betrieb mit Umsatz und Gewinn) und <b>Protokoll</b> (alle Meldungen des Jahres, die neueste oben – ein Klick auf eine Meldung unten links
/// öffnet es). Reine Anzeige; zweimal je Sekunde neu beschriftet.
/// </summary>
public partial class BerichtAnzeige : PanelContainer
{
    /// <summary>Die Reiter in ihrer Reihenfolge (auch für die Startoption <c>--bericht=&lt;reiter&gt;</c>).</summary>
    public enum Reiter { Jahresrechnung, Rivalen, Umsaetze, Protokoll }

    private const int Breite = 400;
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.75f, 0.75f, 0.75f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);

    private Simulation? _simulation;
    private Konto? _spieler;
    private TabContainer _reiter = null!;
    private Label _jahr = null!;
    private Button _berichtKnopf = null!;
    private VBoxContainer _rivalen = null!;
    private VBoxContainer _umsaetze = null!;
    private VBoxContainer _protokoll = null!;
    private string _stand = "";
    private double _seitAufbau;

    /// <summary>Liefert die Meldungen des laufenden Jahres (Uhrzeit, Text, Warnung), die neueste zuerst – die Hauptszene sammelt sie.</summary>
    public Func<IReadOnlyList<(string Zeit, string Text, bool Warnung)>> Meldungen { get; set; } = () => Array.Empty<(string, string, bool)>();

    /// <summary>Der Spieler will den ganzen Jahresbericht des Vorjahres sehen (Fenster in der Bildmitte).</summary>
    public event Action? JahresberichtGewuenscht;

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
        var spalte = new VBoxContainer { CustomMinimumSize = new Vector2(Breite, 0) };
        spalte.AddThemeConstantOverride("separation", 8);
        rand.AddChild(spalte);

        var kopf = new HBoxContainer();
        spalte.AddChild(kopf);
        var titel = Hauptmenue.Text(kopf, "Bericht", 20, Gold);
        titel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        kopf.AddChild(new Control { CustomMinimumSize = new Vector2(52, 0) }); // Platz für Festhalten und ✕ des Fensterrahmens

        _reiter = new TabContainer { FocusMode = FocusModeEnum.None, ClipTabs = false }; // alle Reiter sichtbar, ohne Pfeile
        _reiter.AddThemeFontSizeOverride("font_size", 14);
        _reiter.TabChanged += _ => _stand = "";
        spalte.AddChild(_reiter);

        var jahr = Seite("Jahresrechnung");
        _jahr = Hauptmenue.Text(jahr, "", 14, umbruchBreite: Breite);
        _berichtKnopf = new Button { FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        _berichtKnopf.AddThemeFontSizeOverride("font_size", 13);
        _berichtKnopf.Pressed += () => JahresberichtGewuenscht?.Invoke();
        jahr.AddChild(_berichtKnopf);
        _rivalen = Seite("Rivalen");
        _umsaetze = Seite("Umsätze");
        _protokoll = Seite("Protokoll");

        Rollbereich.Einhuellen(this);
        Fensterrahmen.Anmelden(this, "Bericht", festhaltbar: true);
        Hide();
    }

    private VBoxContainer Seite(string name)
    {
        var seite = new VBoxContainer { Name = name };
        seite.AddThemeConstantOverride("separation", 6);
        _reiter.AddChild(seite);
        return seite;
    }

    public void Verbinden(Simulation simulation, Konto spieler)
    {
        _simulation = simulation;
        _spieler = spieler;
    }

    /// <summary>Öffnet den Bericht auf diesem Reiter (rechts im Bild).</summary>
    public void Zeigen(Reiter reiter)
    {
        _reiter.CurrentTab = (int)reiter;
        _stand = "";
        _seitAufbau = 1;
        Show();
        Rollbereich.RechtsAnheften(this);
        Aktualisieren(0);
    }

    /// <summary>Jedes Bild: höchstens zweimal je Sekunde neu beschriften, und nur, wenn sich etwas geändert hat.</summary>
    public void Aktualisieren(double delta)
    {
        if (!Visible || _simulation?.Wirtschaft is not { } w || _spieler is null) return;
        _seitAufbau += delta;
        if (_seitAufbau < 0.5) return;
        _seitAufbau = 0;
        var waehrung = w.Daten.Allgemein.Waehrung;
        switch ((Reiter)_reiter.CurrentTab)
        {
            case Reiter.Jahresrechnung:
                JahresrechnungZeigen(w, waehrung);
                break;
            case Reiter.Rivalen:
                Neu(_rivalen, string.Join("|", w.Konten.Where(k => k != _spieler).Select(k => $"{k.Inhaber}:{(int)k.Reinvermoegen}:{k.IstAktiv}"))
                              + "|" + string.Join("|", w.Rivalen.Select(r => r.Entscheidungen.Count > 0 ? r.Entscheidungen[0] : "")), () => RivalenZeigen(w, waehrung));
                break;
            case Reiter.Umsaetze:
                Neu(_umsaetze, string.Join("|", w.Betriebe.Where(b => b.Konto == _spieler).Select(b => $"{b.Name}:{(int)b.Laufend.Umsatz}:{(int)b.Laufend.Gewinn}")),
                    () => UmsaetzeZeigen(w, waehrung));
                break;
            case Reiter.Protokoll:
                var meldungen = Meldungen();
                Neu(_protokoll, $"{meldungen.Count}:{(meldungen.Count > 0 ? meldungen[0].Text : "")}", () => ProtokollZeigen(meldungen));
                break;
        }
    }

    /// <summary>Baut eine Seite neu, wenn sich ihr Stand geändert hat.</summary>
    private void Neu(VBoxContainer seite, string stand, Action aufbauen)
    {
        if (stand == _stand) return;
        _stand = stand;
        UiHilfe.KinderEntfernen(seite);
        aufbauen();
    }

    private void JahresrechnungZeigen(Stadtwirtschaft w, string waehrung)
    {
        var l = _spieler!.Laufend;
        var hergestellt = string.Join(", ", l.Hergestellt.OrderBy(x => x.Key).Select(x => $"{x.Value} {x.Key}"));
        var zeilen = new List<string>
        {
            $"Dieses Jahr ({_simulation!.Zeit.DatumText}) bisher:",
            $"  Umsatz {Zahlenformat.Ganz(l.Umsatz)} · Zutaten {Zahlenformat.Ganz(l.Zutaten)} · Fahrten {Zahlenformat.Ganz(l.Transport)} · Steuer {Zahlenformat.Ganz(l.Steuer)}",
            $"  Löhne und Unterhalt werden am Jahresende fällig.",
            hergestellt == "" ? "  Noch nichts hergestellt." : $"  Hergestellt: {hergestellt}",
            "",
        };
        if (_spieler.Vorjahr is { } v)
        {
            zeilen.Add($"Jahresrechnung {v.Jahr}: Gewinn {Zahlenformat.MitVorzeichen(v.Gewinn)} {waehrung}");
            zeilen.Add($"  Umsatz {Zahlenformat.Ganz(v.Umsatz)} − Zutaten {Zahlenformat.Ganz(v.Zutaten)} − Löhne {Zahlenformat.Ganz(v.Personal)} − Unterhalt {Zahlenformat.Ganz(v.Fixkosten)}"
                      + $" − Steuer {Zahlenformat.Ganz(v.Steuer)} − Fahrten {Zahlenformat.Ganz(v.Transport)} − Schaden {Zahlenformat.Ganz(v.Schaden)}");
            if (v.Raubwert > 0) zeilen.Add($"  Geraubt: {Zahlenformat.Ganz(v.Raubwert)} {waehrung}");
        }
        else zeilen.Add("Jahresrechnung: erst nach dem ersten Jahr.");
        var text = string.Join("\n", zeilen);
        if (_jahr.Text != text) _jahr.Text = text;
        _berichtKnopf.Visible = _spieler.Vorjahr is not null;
        if (_spieler.Vorjahr is { } vj) _berichtKnopf.Text = $"Ganzer Jahresbericht {vj.Jahr} …";
    }

    private void RivalenZeigen(Stadtwirtschaft w, string waehrung)
    {
        var andere = w.Konten.Where(k => k != _spieler).OrderByDescending(k => k.Reinvermoegen).ToList();
        if (andere.Count == 0) Hauptmenue.Text(_rivalen, "In dieser Partie gibt es keine anderen Häuser.", 13, Grau);
        foreach (var k in andere)
        {
            // Links das Oberhaupt (PR #211), rechts Name, Zustand und letzter Zug
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 12);
            _rivalen.AddChild(zeile);
            var bild = PortraetAnzeige.Neu(PortraetAnzeige.Klein);
            if (bild.ZeigenHaus(w, k)) zeile.AddChild(bild);
            var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            zeile.AddChild(text);
            var name = UiHilfe.MitWappen(text, k.Wappen, k.Inhaber, 15, k.IstAktiv ? null : Grau);
            name.TooltipText = k.Inhaber;
            var zustand = k.IstBankrott ? "bankrott" : k.IstErloschen ? "erloschen"
                : $"{Zahlenformat.Ganz(k.Reinvermoegen)} {waehrung} · {w.StandVon(k).Name} · {w.Betriebe.Count(b => b.Konto == k)} Betriebe";
            Hauptmenue.Text(text, zustand, 13, k.IstAktiv ? null : Grau, umbruchBreite: Breite - 80).TooltipText =
                $"{k.Inhaber}: Reinvermögen {Zahlenformat.Ganz(k.Reinvermoegen)} {waehrung}, Stand {w.StandVon(k).Name}, {w.Betriebe.Count(b => b.Konto == k)} Betriebe. Ein Rivale spielt nach denselben Regeln wie ihr.";
            if (w.Rivalen.FirstOrDefault(r => r.Konto == k) is { Entscheidungen.Count: > 0 } ki)
                Hauptmenue.Text(text, "Letzter Zug: " + ki.Entscheidungen[0].Replace($"{k.Inhaber}: ", ""), 12, Grau, umbruchBreite: Breite - 80);
        }
    }

    private void UmsaetzeZeigen(Stadtwirtschaft w, string waehrung)
    {
        var eigene = w.Betriebe.Where(b => b.Konto == _spieler).ToList();
        if (eigene.Count == 0) Hauptmenue.Text(_umsaetze, "Ihr habt keinen Betrieb.", 13, Grau);
        Hauptmenue.Text(_umsaetze, "Dieses Jahr bisher (Löhne und Unterhalt erst am Jahresende) · Vorjahr", 12, Grau, umbruchBreite: Breite);
        foreach (var b in eigene)
        {
            Hauptmenue.Text(_umsaetze, $"{b.Name} ({b.Artdaten.Name})", 15, Gold).TooltipText = $"{b.Name}: {b.Belegt}/{b.Plaetze} Arbeitsplätze besetzt, Hauptware {b.Produkt}.";
            var vorjahr = b.Vorjahr is { } v ? $" · Vorjahr Gewinn {Zahlenformat.MitVorzeichen(v.Gewinn)}" : "";
            Hauptmenue.Text(_umsaetze, $"Umsatz {Zahlenformat.Ganz(b.Laufend.Umsatz)} · Gewinn bisher {Zahlenformat.MitVorzeichen(b.Laufend.Gewinn)} {waehrung}{vorjahr}",
                            13, b.Laufend.Gewinn < 0 ? Rot : null, umbruchBreite: Breite).TooltipText =
                "Umsatz und Gewinn des laufenden Jahres. Löhne und Unterhalt werden erst am Jahresende abgerechnet, bis dahin sieht der Gewinn besser aus, als er wird.";
        }
    }

    private void ProtokollZeigen(IReadOnlyList<(string Zeit, string Text, bool Warnung)> meldungen)
    {
        if (meldungen.Count == 0) Hauptmenue.Text(_protokoll, "Noch keine Meldungen in diesem Jahr.", 13, Grau);
        foreach (var (zeit, text, warnung) in meldungen.Take(200))
            Hauptmenue.Text(_protokoll, $"{zeit}  {text}", 13, warnung ? Rot : null, umbruchBreite: Breite).TooltipText = warnung ? "Warnung: Hier braucht ein Betrieb oder das Haus eure Aufmerksamkeit." : "Meldung des Jahres mit der Uhrzeit, zu der sie geschah.";
    }
}
