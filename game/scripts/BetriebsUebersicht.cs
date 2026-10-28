using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Kurzübersicht eines eigenen Betriebs in der Tafel rechts (PR #197): Zustand mit Warnzeichen und „Beheben“, die Ware (draußen noch wechselbar),
/// Arbeiter, Lager und Karren, der Gewinn dieses Jahres, „Hineingehen“ und „…“. Alle übrigen Einstellungen – Produktion, Personal, Lager, Karren,
/// Einrichtung – gibt es nur noch in der Innenansicht (<see cref="InnenTafel"/>); „Beheben“ geht hinein und öffnet dort gleich die passende Tafel.
///
/// Hinter „…“ steht das Bedienfeld <see cref="BetriebsVerwaltung"/> in seiner Kurzform: Ausbau, was nur dieser Beruf hat (Gaststube, Kirche, Wache,
/// Diebe) und Verkaufen bzw. Pacht kündigen. Ein Betrieb ohne Innenansicht (Umland: Grube, Hof) zeigt dort auch Personal, Förderung und Karren mit
/// Eskorte und Nachtfahrt – sonst käme man an sie nicht heran.
/// </summary>
public partial class BetriebsUebersicht : VBoxContainer
{
    private static readonly Color Grau = new(0.75f, 0.75f, 0.75f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);
    private static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    private const int Breite = 300;

    private Simulation? _simulation;
    private Konto? _spieler;
    private Betrieb? _betrieb;
    private Func<Betrieb, bool> _hatInnenansicht = _ => false;

    private Label _zustand = null!;
    private Button _beheben = null!;
    private HBoxContainer _wareZeile = null!;
    private OptionButton _wareWahl = null!;
    private readonly List<string> _waren = new();
    private Label _angaben = null!;
    private Button _hinein = null!;
    private Button _mehrKnopf = null!;
    private BetriebsVerwaltung _verwaltung = null!;
    private string? _behebenTafel;

    /// <summary>Ergebnis eines Befehls (Ware gewechselt, Ausbau …) – für die Meldungen.</summary>
    public event Action<string>? Gemeldet;

    /// <summary>Hineingehen und gleich diese Tafel öffnen (null: nur hineingehen).</summary>
    public event Action<Betrieb, string?>? HineinGewuenscht;

    /// <summary>Handel am Markt (aus dem Bedienfeld eines Betriebs ohne Innenansicht).</summary>
    public event Action<Betrieb>? HandelGewuenscht;

    /// <summary>Verkaufen bzw. Pacht kündigen – öffnet die Rückfrage.</summary>
    public event Action<Betrieb>? VerkaufGewuenscht;

    public Betrieb? Betrieb => _betrieb;

    /// <summary>Ist „…“ aufgeklappt?</summary>
    public bool MehrOffen
    {
        get => _mehrKnopf.ButtonPressed;
        set => _mehrKnopf.ButtonPressed = value;
    }

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 6);

        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 8);
        AddChild(zeile);
        _zustand = Hauptmenue.Text(zeile, "", 14, umbruchBreite: 200);
        _zustand.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _beheben = new Button { Text = "Beheben", FocusMode = FocusModeEnum.None, SizeFlagsVertical = SizeFlags.ShrinkBegin };
        _beheben.Pressed += Beheben;
        zeile.AddChild(_beheben);

        _wareZeile = new HBoxContainer();
        _wareZeile.AddThemeConstantOverride("separation", 8);
        AddChild(_wareZeile);
        Hauptmenue.Text(_wareZeile, "Ware:", 14);
        _wareWahl = new OptionButton { FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill,
                                       TooltipText = "Die Hauptware: Daran arbeitet, wem drinnen keine eigene Ware zugeteilt ist." };
        _wareWahl.ItemSelected += index =>
        {
            if (_betrieb is not null && index >= 0 && index < _waren.Count) Befehlen(new ProduktWaehlen(_betrieb.Name, _waren[(int)index]));
        };
        _wareZeile.AddChild(_wareWahl);

        _angaben = Hauptmenue.Text(this, "", 13, umbruchBreite: Breite);

        var knoepfe = new HBoxContainer();
        knoepfe.AddThemeConstantOverride("separation", 8);
        AddChild(knoepfe);
        _hinein = new Button { Text = "Hineingehen", FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill,
                               TooltipText = "Drinnen: Produktion, Personal, Lager, Karren und Einrichtung (auch Doppelklick aufs Haus)" };
        _hinein.Pressed += () => { if (_betrieb is not null) HineinGewuenscht?.Invoke(_betrieb, null); };
        knoepfe.AddChild(_hinein);
        _mehrKnopf = new Button { Text = "…", ToggleMode = true, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(44, 0),
                                  TooltipText = "Mehr: Ausbau, Verkaufen bzw. Pacht kündigen" };
        _mehrKnopf.Toggled += an => _verwaltung.Visible = an && _betrieb is not null;
        knoepfe.AddChild(_mehrKnopf);

        _verwaltung = new BetriebsVerwaltung { Kurzform = true };
        AddChild(_verwaltung);
        _verwaltung.Gemeldet += t => Gemeldet?.Invoke(t);
        _verwaltung.HandelGewuenscht += b => HandelGewuenscht?.Invoke(b);
        _verwaltung.VerkaufGewuenscht += b => VerkaufGewuenscht?.Invoke(b);
        Visible = false;
    }

    /// <param name="hatInnenansicht">Ob ein Betrieb eine Innenansicht hat (fragt die Stadtansicht).</param>
    public void Verbinden(Simulation simulation, Konto spieler, Func<Betrieb, bool> hatInnenansicht)
    {
        _simulation = simulation;
        _spieler = spieler;
        _hatInnenansicht = hatInnenansicht;
        _verwaltung.Verbinden(simulation, spieler);
    }

    /// <summary>Zeigt die Übersicht für diesen Betrieb – nur, wenn er dem Spieler gehört (sonst verschwindet sie).</summary>
    public void Zeigen(Betrieb? betrieb)
    {
        var neu = betrieb is not null && betrieb.Konto == _spieler ? betrieb : null;
        if (neu != _betrieb)
        {
            _betrieb = neu;
            _mehrKnopf.SetPressedNoSignal(false);
            _verwaltung.Zeigen(neu);
            _verwaltung.Visible = false;
        }
        if (Visible != (neu is not null)) Visible = neu is not null;
    }

    private void Befehlen(Befehl befehl)
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null) return;
        var ergebnis = w.Ausfuehren(_spieler, befehl);
        Gemeldet?.Invoke(ergebnis.Erfolg ? ergebnis.Meldung : "✗ " + ergebnis.Meldung);
    }

    private void Beheben()
    {
        if (_betrieb is null) return;
        if (_hatInnenansicht(_betrieb)) HineinGewuenscht?.Invoke(_betrieb, _behebenTafel);
        else MehrOffen = true; // Umland: Personal und Karren stehen unter „…“
    }

    /// <summary>
    /// Der Zustand eines Betriebs für Betriebsleiste und Kurzübersicht: kurz (Knopf der Leiste), lang (Tafel), ob ein Warnzeichen dazugehört und welche
    /// Tafel der Innenansicht ihn behebt (null: keine). Gelesen wird nur aus dem Kern (Belegung, Stillstand, Lager, Karren, Umbau).
    /// </summary>
    public static (string Kurz, string Lang, bool Warnung, string? Tafel) Zustand(Betrieb b)
    {
        if (b.UmbauMinuten > 0)
            return ("Umbau", b.Stillstand ?? $"Umbau zur Stufe {b.Stufe} – noch {Zahlenformat.Ganz(b.UmbauMinuten / 60.0)} Std.", false, null);
        if (b.Belegt == 0)
            // Werkstätten haben ein Personalbuch im Innenraum, Umlandbetriebe nicht – dort stellt man unter „…“ in der Betriebsleiste ein.
            return ("kein Personal",
                b.IstWerkstatt
                    ? "Kein Personal – stellt drinnen am Personalbuch Arbeitskräfte ein."
                    : "Kein Personal – stellt unter „…“ Arbeitskräfte ein.",
                true, "personal");
        if (b.SabotageMinuten > 0)
            return ("Reparatur", b.Stillstand ?? "Nach dem Anschlag wird repariert.", true, null);
        if (b.Stillstand is { } still)
        {
            // Lager voll: Eine der Waren, an denen gearbeitet wird, hat keinen Platz mehr. Sonst fehlt eine Zutat – die holen die Karren vom Markt.
            IEnumerable<string> waren = b.IstWerkstatt ? b.Arbeiter.Select(b.WareVon).Distinct() : b.Rohstoffe;
            return waren.Any(x => b.ImLager(x) >= b.LagerKapazitaetJeWare)
                ? ("Lager voll", still, true, "lager")
                : ("steht", still, true, "karren");
        }
        var unterwegs = b.Karren.Count(k => k.Zustand != KarrenZustand.AmBetrieb);
        if (unterwegs > 0) return ("Karren unterwegs", $"Die Arbeit läuft · {unterwegs} von {b.Karren.Count} Karren unterwegs.", false, null);
        return ("läuft", "Die Arbeit läuft.", false, null);
    }

    /// <summary>Jedes Bild: Texte und Auswahl an den Zustand der Simulation angleichen.</summary>
    public void Aktualisieren()
    {
        if (!Visible || _betrieb is not { } b || _simulation?.Wirtschaft is not { } w) return;
        var waehrung = w.Daten.Allgemein.Waehrung;
        var innen = _hatInnenansicht(b);

        var (_, lang, warnung, tafel) = Zustand(b);
        var zustand = (warnung ? "⚠ " : "✔ ") + lang;
        if (_zustand.Text != zustand) _zustand.Text = zustand;
        _zustand.AddThemeColorOverride("font_color", warnung ? Rot : Gruen);
        _zustand.MouseFilter = MouseFilterEnum.Pass;
        _zustand.TooltipText = warnung ? "Der Betrieb steht oder stockt. „Beheben“ öffnet die Tafel, an der sich das ändern lässt." : "Der Betrieb läuft.";
        _behebenTafel = tafel;
        var beheben = warnung && (innen ? tafel is not null : true);
        if (_beheben.Visible != beheben) _beheben.Visible = beheben;
        _beheben.TooltipText = innen && tafel is not null ? $"Hineingehen und die Tafel „{TafelName(tafel)}“ öffnen" : "Personal und Karren unter „…“ zeigen";

        // Ware: Auswahl angleichen (Select löst KEIN ItemSelected aus)
        _wareZeile.Visible = b.IstWerkstatt;
        if (b.IstWerkstatt)
        {
            if (!_waren.SequenceEqual(b.Artdaten.Produkte))
            {
                _waren.Clear();
                _wareWahl.Clear();
                foreach (var ware in b.Artdaten.Produkte)
                {
                    _waren.Add(ware);
                    _wareWahl.AddItem(ware);
                }
            }
            for (var i = 0; i < _waren.Count; i++)
                _wareWahl.SetItemText(i, $"{_waren[i]} · {Zahlenformat.Kommazahl(Kalkulation.GewinnJeArbeitsminute(w, _waren[i]))} {waehrung}/Min.");
            if (_wareWahl.Selected != _waren.IndexOf(b.Produkt)) _wareWahl.Select(_waren.IndexOf(b.Produkt));
        }

        var arbeiter = b.Belegt == 0 ? "niemand" : string.Join(", ", b.Belegung.Select(x => $"{x.Value}× {x.Key}"));
        var lager = string.Join(" · ", b.Lager.Where(l => l.Value > 0).OrderBy(l => l.Key).Select(l => $"{l.Value} {l.Key}"));
        var unterwegs = b.Karren.Count(k => k.Zustand != KarrenZustand.AmBetrieb);
        var zeilen = new List<string>
        {
            $"Arbeiter: {arbeiter} ({b.Belegt}/{b.Plaetze})",
            $"Lager: {(lager == "" ? "leer" : lager)} (je Ware bis {b.LagerKapazitaetJeWare})",
            b.Karren.Count == 0 ? "Karren: keine" : $"Karren: {b.Karren.Count}, davon {unterwegs} unterwegs",
            $"Gewinn dieses Jahr bisher: {Zahlenformat.MitVorzeichen(b.Laufend.Gewinn)} {waehrung}" + (b.Vorjahr is { } v ? $" (Vorjahr {Zahlenformat.MitVorzeichen(v.Gewinn)})" : ""),
        };
        var angaben = string.Join("\n", zeilen);
        if (_angaben.Text != angaben) _angaben.Text = angaben;
        _angaben.MouseFilter = MouseFilterEnum.Pass;
        _angaben.TooltipText = "Löhne und Unterhalt werden erst am Jahresende abgerechnet – bis dahin sieht der Gewinn besser aus, als er wird.";
        _angaben.MouseFilter = MouseFilterEnum.Stop;

        if (_hinein.Visible != innen) _hinein.Visible = innen;
        var mehrTipp = innen ? "Mehr: Ausbau, Verkaufen bzw. Pacht kündigen" : "Mehr: Personal, Förderung, Karren, Ausbau, Verkaufen bzw. Pacht kündigen";
        if (_mehrKnopf.TooltipText != mehrTipp) _mehrKnopf.TooltipText = mehrTipp;
        _verwaltung.MitInnenansicht = innen;
        _verwaltung.Aktualisieren();
    }

    private static string TafelName(string tafel) => tafel switch
    {
        "personal" => "Personal",
        "lager" => "Lager",
        "karren" => "Karrenhof",
        "produktion" => "Produktion",
        _ => tafel,
    };
}
