using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die Hafentafel (PR #199): rechts im Stil der Kurzübersicht, geöffnet mit einem Klick auf den Hafen (auch Kran und Lagerschuppen), auf ein eigenes Schiff
/// oder den Knopf „Hafen“ der Betriebsleiste. Zwei Reiter:
/// <list type="bullet">
/// <item><b>Schiffe</b>: die eigenen Schiffe dieses Hafens mit Zustand („zurück gegen …“), Ladung und Ertrag; „Schiff kaufen“ (gesperrt mit dem Grund aus
/// <see cref="Schiffahrt.KaufVerboten"/>). Das gewählte Schiff klappt auf: am Kai der Auftrag – Ziel, Quelle (Markt oder Lager eines eigenen Betriebs),
/// Hin- und Rückfracht, Geleit, Wiederholen, „Vorschlag“ (<see cref="Kalkulation.BesteSchiffsfahrt"/> für das Ziel), der erwartete Gewinn
/// (<see cref="Kalkulation.Schiffsfahrt"/>, aufgeschlüsselt im Tooltip), „Auftrag erteilen“ (gesperrt mit dem Grund aus <see cref="Schiffahrt.AuftragVerboten"/>)
/// und „Verkaufen“; unterwegs der laufende Auftrag, Geleit und „Nicht wiederholen“.</item>
/// <item><b>Ferne Städte</b>: je Stadt, was sie kauft und verkauft, zu welchem Preis (neben dem Preis am Markt daheim) und wie sehr der Markt dort schon
/// gedrückt ist.</item>
/// </list>
/// Gelesen wird nur aus dem Kern; die Befehle gehen über <see cref="Stadtwirtschaft.Ausfuehren"/>. Die Tafel baut sich nur neu, wenn sich der Aufbau ändert
/// (Schiff gekauft, abgelegt, angekommen, anderes Ziel …), sonst werden zweimal je Sekunde die Texte nachgezogen – so bleibt eine Eingabe in den Mengen stehen.
/// </summary>
public partial class HafenAnzeige : PanelContainer
{
    /// <summary>Die Reiter in ihrer Reihenfolge.</summary>
    public enum Reiter { Schiffe, Staedte }

    private const int Breite = 420;
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.75f, 0.75f, 0.75f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);
    private static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);

    private Simulation? _simulation;
    private Konto? _spieler;
    private string _hafen = "";
    private string? _gewaehlt;

    private Label _titel = null!;
    private Label _fluss = null!;
    private TabContainer _reiter = null!;
    private VBoxContainer _schiffsSeite = null!;
    private VBoxContainer _staedteSeite = null!;
    private string _aufbau = "";
    private double _seitAufbau;

    /// <summary>Der Auftrag, den der Spieler für ein Schiff am Kai zusammenstellt (je Schiff gemerkt, solange das Spiel läuft).</summary>
    private sealed class Entwurf
    {
        public string Ziel = "";
        public readonly Dictionary<string, int> Hin = new();
        public readonly Dictionary<string, int> Rueck = new();
        public string? Betrieb;
        public bool Wiederholen;
    }

    private readonly Dictionary<string, Entwurf> _entwuerfe = new();

    /// <summary>Was beim Nachziehen der Texte des gewählten Schiffs gebraucht wird (null: keins gewählt oder nicht aufgebaut).</summary>
    private sealed class Bedienung
    {
        public required Schiff Schiff { get; init; }
        public Label? Laderaum { get; init; }
        public Label? Gewinn { get; init; }
        public Button? Erteilen { get; init; }
        public OptionButton? Geleit { get; init; }
        public Button? Verkaufen { get; init; }
        public OptionButton? Ziel { get; init; }
        public List<string> Ziele { get; } = new();
        public List<(string Ware, Label Text, bool Hin)> Zeilen { get; } = new();
    }

    private Bedienung? _bedienung;
    /// <summary>Beim nächsten Aufbau ein Schiff von selbst aufklappen (gleich nach dem Öffnen).</summary>
    private bool _selbstWaehlen;
    /// <summary>So viele eigene Schiffe hatte der letzte Aufbau – kommt eins dazu (Kauf), klappt es auf.</summary>
    private int _letzteAnzahl;
    private readonly List<(Schiff Schiff, Label Text)> _schiffsTexte = new();
    private Button? _kaufen;
    private string _kaufBeschreibung = "";
    private Label? _fremde;

    /// <summary>Ergebnis eines Befehls (Kauf, Auftrag, Verkauf …) – für die Meldungen.</summary>
    public event Action<string>? Gemeldet;

    /// <summary>Kennung des Hafens (Gebäude der Karte), den die Tafel zeigt.</summary>
    public string Hafen => _hafen;

    /// <summary>Das gewählte Schiff (sein Name bleibt in der Stadt sichtbar), sonst null.</summary>
    public string? GewaehltesSchiff => _gewaehlt;

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
        _titel = Hauptmenue.Text(kopf, "Hafen", 20, Gold);
        _titel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var zu = new Button { Text = "×", FocusMode = FocusModeEnum.None, TooltipText = "Schließen (Esc)" };
        zu.Pressed += Hide;
        kopf.AddChild(zu);
        _fluss = Hauptmenue.Text(spalte, "", 13, Grau, umbruchBreite: Breite);

        _reiter = new TabContainer { FocusMode = FocusModeEnum.None, ClipTabs = false };
        _reiter.AddThemeFontSizeOverride("font_size", 14);
        _reiter.TabChanged += _ => _aufbau = "";
        spalte.AddChild(_reiter);
        _schiffsSeite = Seite("Schiffe");
        _staedteSeite = Seite("Ferne Städte");

        Rollbereich.Einhuellen(this);
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

    /// <summary>Öffnet die Tafel dieses Hafens (rechts im Bild), auf Wunsch mit diesem Schiff aufgeklappt.</summary>
    public void Zeigen(string hafen, string? schiff = null, Reiter reiter = Reiter.Schiffe)
    {
        _hafen = hafen;
        _gewaehlt = schiff;
        _selbstWaehlen = schiff is null;
        _reiter.CurrentTab = (int)reiter;
        _aufbau = "";
        _seitAufbau = 1;
        Show();
        Rollbereich.RechtsAnheften(this);
        Aktualisieren(0);
    }

    /// <summary>Jedes Bild: neu aufbauen, wenn sich der Aufbau geändert hat; sonst höchstens zweimal je Sekunde die Texte nachziehen.</summary>
    public void Aktualisieren(double delta)
    {
        if (!Visible || _simulation?.Wirtschaft is not { } w || _spieler is null) return;
        if (w.Schiffahrt.Hafen(_hafen) is not { } hafen)
        {
            Hide();
            return;
        }
        var titel = hafen.Name;
        if (_titel.Text != titel) _titel.Text = titel;
        var fluss = $"am {hafen.Fluss} · Schiffe fahren flussauf und flussab zu den fernen Städten am Kartenrand";
        if (_fluss.Text != fluss) _fluss.Text = fluss;

        var aufbau = Aufbauschluessel(w);
        if (aufbau != _aufbau)
        {
            _aufbau = aufbau;
            if ((Reiter)_reiter.CurrentTab == Reiter.Schiffe) SchiffeAufbauen(w);
            else StaedteAufbauen(w);
            _seitAufbau = 1;
        }
        _seitAufbau += delta;
        if (_seitAufbau < 0.5) return;
        _seitAufbau = 0;
        if ((Reiter)_reiter.CurrentTab == Reiter.Schiffe) SchiffeNachziehen(w);
        else StaedteNachziehen(w);
    }

    /// <summary>Was den Aufbau bestimmt: die eigenen Schiffe (am Kai oder nicht, mit welchem Auftrag), das gewählte und sein Entwurf (Ziel, Quelle).</summary>
    private string Aufbauschluessel(Stadtwirtschaft w)
    {
        if ((Reiter)_reiter.CurrentTab == Reiter.Staedte) return "staedte";
        var teile = EigeneSchiffe(w).Select(s => $"{s.Name}:{s.Zustand == SchiffZustand.AmKai}:{s.Auftrag?.Wiederholen}");
        var entwurf = _gewaehlt is not null && _entwuerfe.TryGetValue(_gewaehlt, out var e) ? $"{e.Ziel}/{e.Betrieb}" : "";
        return $"{string.Join("|", teile)}#{_gewaehlt}#{entwurf}#{w.Betriebe.Count(b => b.Konto == _spieler)}";
    }

    private List<Schiff> EigeneSchiffe(Stadtwirtschaft w) => w.Schiffahrt.SchiffeVon(_spieler!).Where(s => s.Hafen == _hafen).ToList();

    private void Befehlen(Befehl befehl)
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null) return;
        var ergebnis = w.Ausfuehren(_spieler, befehl);
        Gemeldet?.Invoke(ergebnis.Erfolg ? ergebnis.Meldung : "✗ " + ergebnis.Meldung);
        _aufbau = ""; // gleich neu: Zustand, Liste, Knöpfe
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Reiter „Schiffe“
    // ═════════════════════════════════════════════════════════════════════════

    private void SchiffeAufbauen(Stadtwirtschaft w)
    {
        UiHilfe.KinderEntfernen(_schiffsSeite);
        _schiffsTexte.Clear();
        _bedienung = null;
        var waehrung = w.Daten.Allgemein.Waehrung;
        var eigene = EigeneSchiffe(w);
        if (_gewaehlt is not null && eigene.All(s => s.Name != _gewaehlt)) _gewaehlt = null; // verkauft
        // Beim Öffnen und nach einem Kauf klappt ein Schiff von selbst auf (das erste am Kai bzw. das neue); danach entscheidet der Spieler
        if (_selbstWaehlen || eigene.Count > _letzteAnzahl)
            _gewaehlt ??= (eigene.Count > _letzteAnzahl && !_selbstWaehlen ? eigene.LastOrDefault() : eigene.FirstOrDefault(s => s.Zustand == SchiffZustand.AmKai) ?? eigene.FirstOrDefault())?.Name;
        _selbstWaehlen = false;
        _letzteAnzahl = eigene.Count;

        // Oben „Schiff kaufen“ (mit Preis); was ein Schiff kann, steht im Tooltip und – solange keins da ist – darunter
        var typ = w.Schiffahrt.Daten?.Schiffe.Values.FirstOrDefault();
        _kaufen = null;
        if (typ is not null)
        {
            var beschreibung = $"Ein Flussschiff fasst {typ.Sorten} Waren zu je {typ.KapazitaetJeSorte} Stück; Unterhalt {Zahlenformat.Ganz(typ.Unterhalt)} {waehrung} " +
                               $"im Jahr. Flussab {typ.MinutenFlussab}, flussauf {typ.MinutenFlussauf} Minuten je Strecke (mal Entfernung der Stadt).";
            _kaufen = new Button { FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
                                   Text = $"Schiff kaufen ({Zahlenformat.Ganz(typ.Preis)} {waehrung})" };
            _kaufen.Pressed += () => Befehlen(new SchiffKaufen(_hafen));
            _schiffsSeite.AddChild(_kaufen);
            _kaufBeschreibung = beschreibung;
            if (eigene.Count == 0) Hauptmenue.Text(_schiffsSeite, beschreibung, 12, Grau, umbruchBreite: Breite);
        }

        if (eigene.Count == 0)
            Hauptmenue.Text(_schiffsSeite, "Ihr habt hier noch kein Schiff. Ein Flussschiff bringt Waren zu den fernen Städten am Fluss und kauft dort ein, " +
                                            "was es daheim nicht oder nur teuer gibt.", 13, Grau, umbruchBreite: Breite);
        foreach (var schiff in eigene)
        {
            var gewaehlt = schiff.Name == _gewaehlt;
            var knopf = new Button
            {
                Text = schiff.Name, ToggleMode = true, ButtonPressed = gewaehlt, FocusMode = FocusModeEnum.None, Alignment = HorizontalAlignment.Left,
                TooltipText = gewaehlt ? "Zuklappen" : "Aufklappen: Auftrag, Geleit, Verkaufen",
            };
            knopf.AddThemeFontSizeOverride("font_size", 15);
            var name = schiff.Name;
            knopf.Pressed += () =>
            {
                _gewaehlt = _gewaehlt == name ? null : name;
                _aufbau = "";
            };
            _schiffsSeite.AddChild(knopf);
            _schiffsTexte.Add((schiff, Hauptmenue.Text(_schiffsSeite, "", 13, umbruchBreite: Breite)));
            if (gewaehlt)
            {
                if (schiff.Zustand == SchiffZustand.AmKai) AuftragAufbauen(w, schiff);
                else UnterwegsAufbauen(w, schiff);
            }
            _schiffsSeite.AddChild(new HSeparator());
        }

        _fremde = Hauptmenue.Text(_schiffsSeite, "", 12, Grau, umbruchBreite: Breite);
        SchiffeNachziehen(w);
    }

    /// <summary>Das gewählte Schiff am Kai: der Auftrag zum Zusammenstellen.</summary>
    private void AuftragAufbauen(Stadtwirtschaft w, Schiff schiff)
    {
        var waehrung = w.Daten.Allgemein.Waehrung;
        var e = EntwurfVon(w, schiff);
        var stadt = w.Schiffahrt.Stadt(e.Ziel);
        var kasten = Kasten();

        // Ziel: je Stadt mit dem besten Gewinn, den der Vorschlag für sie findet
        var zielZeile = Zeile(kasten, "Ziel:");
        var ziel = new OptionButton { FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true,
                                      TooltipText = "Die ferne Stadt. Dahinter steht, was der Vorschlag dort zu heutigen Preisen gewinnen würde." };
        zielZeile.AddChild(ziel);
        var bedienung = new Bedienung { Schiff = schiff, Ziel = ziel };
        foreach (var s in w.Schiffahrt.Staedte)
        {
            bedienung.Ziele.Add(s.Name);
            ziel.AddItem(s.Name);
        }
        ziel.Select(bedienung.Ziele.IndexOf(e.Ziel));
        ziel.ItemSelected += index =>
        {
            e.Ziel = bedienung.Ziele[(int)index];
            e.Hin.Clear();
            e.Rueck.Clear();
            _aufbau = "";
        };

        // Quelle: Markt oder Lager eines eigenen Betriebs
        var quellen = new List<string?> { null };
        quellen.AddRange(w.Betriebe.Where(b => b.Konto == _spieler).Select(b => (string?)b.Name));
        var quelleZeile = Zeile(kasten, "Quelle:");
        var quelle = new OptionButton { FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true,
                                        TooltipText = "Markt: Die Hinfracht wird am Markt gekauft, die Rückfracht dort verkauft.\n" +
                                                      "Lager eines Betriebs: Die Hinfracht kommt aus seinem Lager, die Rückfracht geht hinein (was nicht passt, auf den Markt)." };
        quelleZeile.AddChild(quelle);
        foreach (var q in quellen) quelle.AddItem(q is null ? "Markt (kaufen und verkaufen)" : $"Lager: {q}");
        quelle.Select(Math.Max(0, quellen.IndexOf(e.Betrieb)));
        quelle.ItemSelected += index =>
        {
            e.Betrieb = quellen[(int)index];
            _aufbau = "";
        };

        // Oben das Ergebnis: erwarteter Gewinn, Vorschlag und Auftrag; darunter Geleit, Wiederholen und die Fracht im Einzelnen
        var gewinn = Hauptmenue.Text(kasten, "", 14, umbruchBreite: Breite - 20);
        gewinn.MouseFilter = MouseFilterEnum.Stop;

        var knoepfe = new HBoxContainer();
        knoepfe.AddThemeConstantOverride("separation", 8);
        kasten.AddChild(knoepfe);
        var vorschlag = new Button { Text = "Vorschlag", FocusMode = FocusModeEnum.None,
                                     TooltipText = "Füllt die Fracht mit dem einträglichsten Auftrag für dieses Ziel (Ware vom Markt, zu heutigen Preisen, so weit das Geld reicht)." };
        vorschlag.Pressed += () => VorschlagUebernehmen(w, schiff, e);
        knoepfe.AddChild(vorschlag);
        var erteilen = new Button { Text = "Auftrag erteilen", FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        erteilen.Pressed += () => Befehlen(new SchiffsauftragErteilen(schiff.Name, e.Ziel, new Dictionary<string, int>(e.Hin), new Dictionary<string, int>(e.Rueck),
                                                                      e.Betrieb, e.Wiederholen));
        knoepfe.AddChild(erteilen);

        var geleit = GeleitWahl(w, schiff, kasten);
        var wiederholen = new CheckBox { Text = "Wiederholen (nach der Rückkehr mit demselben Auftrag wieder los)", ButtonPressed = e.Wiederholen, FocusMode = FocusModeEnum.None };
        wiederholen.AddThemeFontSizeOverride("font_size", 13);
        wiederholen.Toggled += an => e.Wiederholen = an;
        kasten.AddChild(wiederholen);

        var laderaum = Hauptmenue.Text(kasten, "", 12, Grau, umbruchBreite: Breite - 20);
        // Fracht: je Ware, die die Stadt kauft (hin) bzw. verkauft (zurück), eine Menge
        if (stadt is not null)
        {
            var hoechstens = schiff.Daten.Gesamtkapazitaet;
            void Fracht(string ueberschrift, IEnumerable<string> waren, Dictionary<string, int> mengen, bool hin)
            {
                Hauptmenue.Text(kasten, ueberschrift, 13, Gold);
                var gitter = new GridContainer { Columns = 2 };
                gitter.AddThemeConstantOverride("h_separation", 8);
                kasten.AddChild(gitter);
                foreach (var ware in waren.OrderBy(x => x, StringComparer.Ordinal))
                {
                    var zeile = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                    zeile.AddThemeConstantOverride("separation", 6);
                    gitter.AddChild(zeile);
                    var bild = WarenIcons.Bild(ware, 20);
                    bild.SizeFlagsVertical = SizeFlags.ShrinkCenter;
                    zeile.AddChild(bild);
                    var text = Hauptmenue.Text(zeile, ware, 12);
                    text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    text.CustomMinimumSize = new Vector2(270, 0);
                    text.MouseFilter = MouseFilterEnum.Stop;
                    bedienung.Zeilen.Add((ware, text, hin));
                    var menge = new SpinBox { MinValue = 0, MaxValue = hoechstens, Step = 1, CustomArrowStep = schiff.Daten.KapazitaetJeSorte / 3.0,
                                              Value = mengen.GetValueOrDefault(ware), CustomMinimumSize = new Vector2(84, 0),
                                              TooltipText = $"Stück (je Laderaum eine Ware, bis {schiff.Daten.KapazitaetJeSorte})" };
                    menge.ValueChanged += wert =>
                    {
                        if (wert <= 0) mengen.Remove(ware);
                        else mengen[ware] = (int)wert;
                        _seitAufbau = 1; // Gewinn und Laderaum gleich nachziehen
                    };
                    gitter.AddChild(menge);
                }
            }
            Fracht($"Hinfracht – in {stadt.Name} verkaufen", stadt.Daten.Kauft.Keys, e.Hin, true);
            Fracht($"Rückfracht – in {stadt.Name} kaufen", stadt.Daten.Verkauft.Keys, e.Rueck, false);
        }

        var verkaufen = VerkaufKnopf(w, schiff, kasten);
        _bedienung = new Bedienung
        {
            Schiff = schiff, Laderaum = laderaum, Gewinn = gewinn, Erteilen = erteilen, Geleit = geleit, Verkaufen = verkaufen, Ziel = ziel,
        };
        _bedienung.Ziele.AddRange(bedienung.Ziele);
        _bedienung.Zeilen.AddRange(bedienung.Zeilen);
    }

    /// <summary>Das gewählte Schiff beim Beladen oder unterwegs: der laufende Auftrag, Geleit für die nächste Strecke, „Nicht wiederholen“.</summary>
    private void UnterwegsAufbauen(Stadtwirtschaft w, Schiff schiff)
    {
        var kasten = Kasten();
        if (schiff.Auftrag is { } a)
        {
            string Liste(IReadOnlyDictionary<string, int> fracht) =>
                fracht.Any(x => x.Value > 0) ? string.Join(", ", fracht.Where(x => x.Value > 0).Select(x => $"{x.Value} {x.Key}")) : "leer";
            Hauptmenue.Text(kasten, $"Auftrag: nach {a.Ziel} · hin {Liste(a.Hinfracht)} · zurück {Liste(a.Rueckfracht)}" +
                                    (a.Betrieb is { } b ? $" · Lager {b}" : " · Markt") + (a.Wiederholen ? " · wiederholt sich" : ""), 13, umbruchBreite: Breite - 20);
            if (a.Wiederholen)
            {
                var beenden = new Button { Text = "Nicht wiederholen", FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
                                           TooltipText = "Das Schiff fährt diese Fahrt zu Ende und bleibt dann am Kai." };
                beenden.Pressed += () => Befehlen(new SchiffsauftragBeenden(schiff.Name));
                kasten.AddChild(beenden);
            }
        }
        var geleit = GeleitWahl(w, schiff, kasten);
        var verkaufen = VerkaufKnopf(w, schiff, kasten);
        _bedienung = new Bedienung { Schiff = schiff, Geleit = geleit, Verkaufen = verkaufen };
    }

    /// <summary>Ein eingerückter Kasten unter dem Knopf des Schiffs.</summary>
    private VBoxContainer Kasten()
    {
        var rand = new MarginContainer();
        rand.AddThemeConstantOverride("margin_left", 12);
        _schiffsSeite.AddChild(rand);
        var kasten = new VBoxContainer();
        kasten.AddThemeConstantOverride("separation", 6);
        rand.AddChild(kasten);
        return kasten;
    }

    private static HBoxContainer Zeile(Container eltern, string beschriftung)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 8);
        eltern.AddChild(zeile);
        Hauptmenue.Text(zeile, beschriftung, 13).CustomMinimumSize = new Vector2(64, 0);
        return zeile;
    }

    /// <summary>Das Geleit (Stufen der Eskorten wie bei den Karren); die Wahl gilt sofort, auch unterwegs für die nächste Strecke.</summary>
    private OptionButton GeleitWahl(Stadtwirtschaft w, Schiff schiff, Container kasten)
    {
        var zeile = Zeile(kasten, "Geleit:");
        var wahl = new OptionButton { FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
        var namen = w.Daten.Logistik.Eskorten.Keys.ToList();
        foreach (var n in namen) wahl.AddItem(n);
        wahl.Select(namen.IndexOf(schiff.Geleit));
        wahl.ItemSelected += index => Befehlen(new SchiffGeleitWaehlen(schiff.Name, namen[(int)index]));
        zeile.AddChild(wahl);
        return wahl;
    }

    private Button VerkaufKnopf(Stadtwirtschaft w, Schiff schiff, Container kasten)
    {
        var anteil = w.Schiffahrt.Daten?.VerkaufAnteil ?? 0;
        var knopf = new Button { FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
                                 Text = $"Verkaufen ({Zahlenformat.Ganz(schiff.Daten.Preis * anteil)} {w.Daten.Allgemein.Waehrung})" };
        knopf.Pressed += () => Befehlen(new SchiffVerkaufen(schiff.Name));
        kasten.AddChild(knopf);
        return knopf;
    }

    /// <summary>Der Entwurf eines Schiffs; neu mit dem letzten Auftrag oder dem Vorschlag (sonst der ersten Stadt).</summary>
    private Entwurf EntwurfVon(Stadtwirtschaft w, Schiff schiff)
    {
        if (_entwuerfe.TryGetValue(schiff.Name, out var e) && w.Schiffahrt.Stadt(e.Ziel) is not null) return e;
        e = new Entwurf();
        _entwuerfe[schiff.Name] = e;
        var vorschlag = Kalkulation.BesteSchiffsfahrt(w, _spieler!, schiff.Daten, _spieler!.Vermoegen, schiff.Geleit);
        if (vorschlag is not null) Uebernehmen(e, vorschlag);
        else e.Ziel = w.Schiffahrt.Staedte.FirstOrDefault()?.Name ?? "";
        return e;
    }

    private static void Uebernehmen(Entwurf e, Fahrtvorschlag v)
    {
        e.Ziel = v.Ziel;
        e.Betrieb = null;
        e.Hin.Clear();
        e.Rueck.Clear();
        foreach (var (ware, stueck) in v.Hinfracht) e.Hin[ware] = stueck;
        foreach (var (ware, stueck) in v.Rueckfracht) e.Rueck[ware] = stueck;
    }

    private void VorschlagUebernehmen(Stadtwirtschaft w, Schiff schiff, Entwurf e)
    {
        if (Kalkulation.BesteSchiffsfahrt(w, _spieler!, schiff.Daten, _spieler!.Vermoegen, schiff.Geleit, ziel: e.Ziel) is { } v)
        {
            Uebernehmen(e, v);
            _entwuerfe[schiff.Name] = e;
            _aufbau = "";
        }
        else Gemeldet?.Invoke($"Eine Fahrt nach {e.Ziel} bringt zu heutigen Preisen keinen Gewinn.");
        _aufbau = ""; // die Mengen neu setzen
    }

    /// <summary>Zweimal je Sekunde: Zustände der Schiffe, Preise der Fracht, Laderaum, Gewinn, Geleit und Knöpfe.</summary>
    private void SchiffeNachziehen(Stadtwirtschaft w)
    {
        var waehrung = w.Daten.Allgemein.Waehrung;
        foreach (var (schiff, text) in _schiffsTexte)
            Setzen(text, SchiffText(w, schiff));

        if (_kaufen is not null && IsInstanceValid(_kaufen))
        {
            var grund = w.Schiffahrt.KaufVerboten(_spieler!, _hafen) ?? (_spieler!.IstAktiv ? null : "Euer Haus ist nicht mehr im Spiel.");
            if (_kaufen.Disabled != (grund is not null)) _kaufen.Disabled = grund is not null;
            var tipp = (grund is null ? "" : grund + "\n\n") + _kaufBeschreibung;
            if (_kaufen.TooltipText != tipp) _kaufen.TooltipText = tipp;
        }
        if (_fremde is not null && IsInstanceValid(_fremde))
        {
            var fremde = w.Schiffahrt.Schiffe.Where(s => s.Hafen == _hafen && s.Besitzer != _spieler).ToList();
            Setzen(_fremde, fremde.Count == 0 ? "" : "Andere Häuser: " + string.Join(" · ", fremde.Select(s => $"{s.Name} ({s.Besitzer.Inhaber}) {w.Schiffahrt.ZustandText(s)}")));
        }

        if (_bedienung is not { } b || !IsInstanceValid(b.Verkaufen)) return;
        var schiffB = b.Schiff;
        // Verkaufen nur am Kai
        if (b.Verkaufen is { } verkaufen)
        {
            var amKai = schiffB.Zustand == SchiffZustand.AmKai;
            if (verkaufen.Disabled == amKai) verkaufen.Disabled = !amKai;
            Setzen(verkaufen, amKai ? $"Verkauf zum halben Preis ({Zahlenformat.Ganz((w.Schiffahrt.Daten?.VerkaufAnteil ?? 0) * 100)} %)." : "Verkaufen geht nur am Kai.");
        }

        Entwurf? e = schiffB.Zustand == SchiffZustand.AmKai ? EntwurfVon(w, schiffB) : null;
        var stadt = e is null ? null : w.Schiffahrt.Stadt(e.Ziel);
        var ausLager = e?.Betrieb is not null;
        var betrieb = e?.Betrieb is { } bn ? w.Betriebe.FirstOrDefault(x => x.Name == bn) : null;

        // Ziele: der beste Gewinn je Stadt
        if (b.Ziel is not null)
            for (var i = 0; i < b.Ziele.Count; i++)
            {
                var v = Kalkulation.BesteSchiffsfahrt(w, _spieler!, schiffB.Daten, _spieler!.Vermoegen, schiffB.Geleit, ziel: b.Ziele[i]);
                var s = w.Schiffahrt.Stadt(b.Ziele[i])!;
                var richtung = s.Daten.IstFlussauf ? "flussauf" : "flussab";
                var text = s.Name + (v is null ? " · lohnt nicht" : $" · Vorschlag ≈ {Zahlenformat.MitVorzeichen(v.Rechnung.Gewinn)} {waehrung}");
                if (b.Ziel.GetItemText(i) != text) b.Ziel.SetItemText(i, text);
                b.Ziel.SetItemTooltip(i, $"{s.Name} liegt {richtung}; die ganze Fahrt dauert {Schiffahrt.Rundreise(schiffB.Daten, s)} Minuten (beladen, hin, liegen, zurück).");
                if (i == b.Ziel.Selected) Setzen(b.Ziel, b.Ziel.GetItemTooltip(i));
            }

        // Fracht: Preise dort und daheim
        if (stadt is not null)
            foreach (var (ware, label, hin) in b.Zeilen)
            {
                var hier = w.Markt[ware].Preis;
                var lager = betrieb is not null && hin ? $" · im Lager {betrieb.ImLager(ware)}" : "";
                var text = hin
                    ? $"{ware} · dort {Zahlenformat.Kommazahl(stadt.Ankaufpreis(ware))} · hier {Zahlenformat.Kommazahl(hier)}{lager}"
                    : $"{ware} · dort {Zahlenformat.Kommazahl(stadt.Verkaufspreis(ware))} · hier {Zahlenformat.Kommazahl(hier)}";
                Setzen(label, text);
                var druck = stadt.Druck(ware);
                var tipp = (hin ? $"{stadt.Name} zahlt jetzt {Zahlenformat.Kommazahl(stadt.Ankaufpreis(ware))} {waehrung} je Stück" : $"In {stadt.Name} kostet ein Stück jetzt {Zahlenformat.Kommazahl(stadt.Verkaufspreis(ware))} {waehrung}")
                           + $"; am Markt daheim {Zahlenformat.Kommazahl(hier)}." + (druck >= 0.5 ? $"\nDer Markt dort ist gedrückt: zuletzt {Zahlenformat.Ganz(druck)} Stück gehandelt, er erholt sich Stunde um Stunde." : "")
                           + "\nJedes Stück drückt den Preis für das nächste.";
                if (label.TooltipText != tipp) label.TooltipText = tipp;
            }

        // Laderaum
        if (e is not null && b.Laderaum is not null)
        {
            var kap = schiffB.Daten.KapazitaetJeSorte;
            int Raeume(Dictionary<string, int> f) => f.Where(x => x.Value > 0).Sum(x => (x.Value + kap - 1) / kap);
            var (hinR, rueckR) = (Raeume(e.Hin), Raeume(e.Rueck));
            Setzen(b.Laderaum, $"Laderäume: hin {hinR} von {schiffB.Daten.Sorten}, zurück {rueckR} von {schiffB.Daten.Sorten} (je Raum eine Ware, bis {kap} Stück)");
            var voll = hinR > schiffB.Daten.Sorten || rueckR > schiffB.Daten.Sorten;
            b.Laderaum.AddThemeColorOverride("font_color", voll ? Rot : Grau);
        }

        // Gewinn und Erteilen
        if (e is not null && stadt is not null && b.Gewinn is not null && b.Erteilen is not null)
        {
            var r = Kalkulation.Schiffsfahrt(w, _spieler!, schiffB.Daten, stadt, e.Hin, e.Rueck, ausLager, schiffB.Geleit);
            var leer = e.Hin.Count == 0 && e.Rueck.Count == 0;
            Setzen(b.Gewinn, leer ? "Erwarteter Gewinn: – (noch keine Fracht)"
                : $"Erwarteter Gewinn der Fahrt: {Zahlenformat.MitVorzeichen(r.Gewinn)} {waehrung} in {r.Minuten} Min. (≈ {Zahlenformat.MitVorzeichen(r.GewinnJeJahr)} im Jahr bei steter Fahrt)");
            b.Gewinn.AddThemeColorOverride("font_color", leer ? Grau : r.Gewinn >= 0 ? Gruen : Rot);
            var tipp = RechnungText(r, stadt, ausLager, waehrung);
            if (b.Gewinn.TooltipText != tipp) b.Gewinn.TooltipText = tipp;
            var auftrag = new Schiffsauftrag(e.Ziel, e.Hin, e.Rueck, e.Betrieb, e.Wiederholen);
            var grund = w.Schiffahrt.AuftragVerboten(_spieler!, schiffB.Name, auftrag) ?? (_spieler!.IstAktiv ? null : "Euer Haus ist nicht mehr im Spiel.");
            if (b.Erteilen.Disabled != (grund is not null)) b.Erteilen.Disabled = grund is not null;
            Setzen(b.Erteilen, grund ?? $"{schiffB.Name} wird beladen und fährt nach {stadt.Name}.");
        }

        // Geleit: was jede Stufe für diese Fahrt kostet (Preis und erwarteter Verlust durch Flusspiraten) – wie bei den Karren
        if (b.Geleit is { } geleit)
        {
            var namen = w.Daten.Logistik.Eskorten.Keys.ToList();
            var (hinF, rueckF) = e is not null ? ((IReadOnlyDictionary<string, int>)e.Hin, (IReadOnlyDictionary<string, int>)e.Rueck)
                : (schiffB.Ladung, schiffB.Auftrag?.Rueckfracht ?? new Dictionary<string, int>());
            var fahrten = (hinF.Any(x => x.Value > 0) ? 1 : 0) + (rueckF.Any(x => x.Value > 0) ? 1 : 0);
            var wert = hinF.Concat(rueckF).Where(x => x.Value > 0).Sum(x => w.Markt[x.Key].Preis * x.Value);
            for (var i = 0; i < namen.Count; i++)
            {
                var frei = w.Wache.EskorteVerfuegbar(namen[i]);
                geleit.SetItemDisabled(i, !frei);
                if (!frei)
                {
                    geleit.SetItemText(i, $"{namen[i]} · nur mit einem Wachhaus in der Stadt");
                    geleit.SetItemTooltip(i, "Die Stadtwache gibt Geleit nur, solange ein Haus ein Wachhaus mit Leuten führt.");
                    continue;
                }
                var preis = w.Schiffahrt.GeleitKosten(namen[i]) * fahrten;
                var verlust = Kalkulation.SchiffsRisiko(w, _spieler!, namen[i]) * wert;
                geleit.SetItemText(i, $"{namen[i]} · ≈ {Zahlenformat.Ganz(preis + verlust)} {waehrung}/Fahrt");
                geleit.SetItemTooltip(i, $"Preis des Geleits {Zahlenformat.Ganz(preis)} ({Zahlenformat.Ganz(w.Schiffahrt.GeleitKosten(namen[i]))} je beladener Strecke) + erwarteter Verlust durch " +
                                         $"Flusspiraten {Zahlenformat.Ganz(verlust)} = ≈ {Zahlenformat.Ganz(preis + verlust)} {waehrung} für diese Fahrt. Die kleinste Zahl ist die günstigste Wahl.");
            }
            var gewaehlt = namen.IndexOf(schiffB.Geleit);
            if (gewaehlt >= 0 && geleit.Selected != gewaehlt) geleit.Select(gewaehlt);
            if (gewaehlt >= 0) geleit.TooltipText = geleit.GetItemTooltip(gewaehlt);
        }
    }

    /// <summary>Die Aufschlüsselung des erwarteten Gewinns (Tooltip).</summary>
    private static string RechnungText(Fahrtrechnung r, FerneStadt stadt, bool ausLager, string waehrung)
    {
        string Plus(double wert) => Zahlenformat.MitVorzeichen(wert);
        string Minus(double wert) => Zahlenformat.MitVorzeichen(-wert);
        var zeilen = new List<string>
        {
            ausLager ? $"Hinfracht aus dem Lager (am Markt wert): {Minus(r.Einkauf)}" : $"Einkauf der Hinfracht am Markt: {Minus(r.Einkauf)}",
            $"Erlös in {stadt.Name}: {Plus(r.ErloesFern)}",
            $"Einkauf der Rückfracht in {stadt.Name}: {Minus(r.EinkaufFern)}",
            $"Erlös der Rückfracht daheim (nach Steuer): {Plus(r.ErloesHeim)}",
            $"Zoll ({stadt.Daten.Zollstellen} Zollstellen): {Minus(r.Zoll)}",
            $"Geleit: {Minus(r.Geleit)}",
            $"Risiko Flusspiraten (erwarteter Verlust): {Minus(r.Risiko)}",
            $"= Gewinn {Zahlenformat.MitVorzeichen(r.Gewinn)} {waehrung} in {r.Minuten} Minuten (beladen, hin, liegen, zurück)",
            "",
            "Zu heutigen Preisen – jedes Stück drückt den Preis für das nächste. Der Unterhalt des Schiffs kommt zum Jahresende dazu.",
        };
        return string.Join("\n", zeilen);
    }

    /// <summary>Eine Zeile je eigenes Schiff: Zustand, wann es zurück ist, Ladung, Fahrten und Ertrag.</summary>
    private string SchiffText(Stadtwirtschaft w, Schiff s)
    {
        var waehrung = w.Daten.Allgemein.Waehrung;
        var zeilen = new List<string> { w.Schiffahrt.ZustandText(s) + (s.IstUnterwegs || s.Zustand == SchiffZustand.Beladen ? $" · zurück {ZurueckText(w, s)}" : "") };
        if (s.Geladen > 0) zeilen.Add("Ladung: " + string.Join(", ", s.Ladung.Where(x => x.Value > 0).Select(x => $"{x.Value} {x.Key}")));
        zeilen.Add($"Geleit: {s.Geleit} · Fahrten: {s.Fahrten} · Ertrag dieses Jahr {Zahlenformat.MitVorzeichen(s.ErtragLaufend)} {waehrung}" +
                   (s.ErtragVorjahr is { } v ? $" (Vorjahr {Zahlenformat.MitVorzeichen(v)})" : ""));
        return string.Join("\n", zeilen);
    }

    /// <summary>„gegen 14:30“ bzw. „im nächsten Jahr“ (ein Spieltag ist ein Jahr).</summary>
    private string ZurueckText(Stadtwirtschaft w, Schiff s)
    {
        var zeit = _simulation!.Zeit;
        var um = zeit.UhrzeitInMinuten + (int)(w.Schiffahrt.ZurueckUm(s) - zeit.VergangeneMinuten);
        return um >= Spielzeit.TagesendeMinute ? "im nächsten Jahr" : $"gegen {um / 60:00}:{um % 60:00}";
    }

    private static void Setzen(Label label, string text)
    {
        if (label.Text != text) label.Text = text;
    }

    private static void Setzen(Control knopf, string tipp)
    {
        if (knopf.TooltipText != tipp) knopf.TooltipText = tipp;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Reiter „Ferne Städte“
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Zahlen des Reiters „Ferne Städte“, die zweimal je Sekunde nachgezogen werden: Preis dort, daheim und Druck je Ware.</summary>
    private readonly List<(FerneStadt Stadt, string Ware, bool Kauft, Label Dort, Label Daheim, Label Druck)> _preise = new();

    /// <summary>Je Stadt: Richtung, Fahrzeit, Zoll und die Preise (dort, daheim, Druck). Einmal aufgebaut, die Zahlen zieht <see cref="StaedteNachziehen"/> nach.</summary>
    private void StaedteAufbauen(Stadtwirtschaft w)
    {
        UiHilfe.KinderEntfernen(_staedteSeite);
        _preise.Clear();
        var typ = w.Schiffahrt.Daten?.Schiffe.Values.FirstOrDefault();
        foreach (var stadt in w.Schiffahrt.Staedte)
        {
            Hauptmenue.Text(_staedteSeite, stadt.Name, 16, Gold);
            var fahrt = typ is null ? "" : $" · hin {Schiffahrt.Fahrzeit(typ, stadt, true)}, zurück {Schiffahrt.Fahrzeit(typ, stadt, false)} Min.";
            Hauptmenue.Text(_staedteSeite, $"{(stadt.Daten.IstFlussauf ? "Flussauf" : "Flussab")}{fahrt} · {stadt.Daten.Zollstellen} Zollstellen " +
                                            $"({Zahlenformat.Ganz((w.Schiffahrt.Daten?.ZollJeStelle ?? 0) * 100)} % vom Wert je Stelle)", 12, Grau, umbruchBreite: Breite);
            Tabelle("Kauft – dort verkaufen", stadt.Daten.Kauft.Keys, stadt, kauft: true);
            Tabelle("Verkauft – dort kaufen", stadt.Daten.Verkauft.Keys, stadt, kauft: false);
            _staedteSeite.AddChild(new HSeparator());
        }
        StaedteNachziehen(w);
    }

    private void Tabelle(string ueberschrift, IEnumerable<string> waren, FerneStadt stadt, bool kauft)
    {
        Hauptmenue.Text(_staedteSeite, ueberschrift, 13);
        var gitter = new GridContainer { Columns = 4 };
        gitter.AddThemeConstantOverride("h_separation", 14);
        _staedteSeite.AddChild(gitter);
        foreach (var kopf in new[] { "Ware", "dort", "daheim", "Druck" }) Hauptmenue.Text(gitter, kopf, 12, Grau);
        foreach (var ware in waren.OrderBy(x => x, StringComparer.Ordinal))
        {
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 6);
            gitter.AddChild(zeile);
            var bild = WarenIcons.Bild(ware, 18);
            bild.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            zeile.AddChild(bild);
            Hauptmenue.Text(zeile, ware, 12).CustomMinimumSize = new Vector2(150, 0);
            var dort = Hauptmenue.Text(gitter, "", 12);
            var daheim = Hauptmenue.Text(gitter, "", 12);
            var druck = Hauptmenue.Text(gitter, "", 12);
            druck.TooltipText = "Zuletzt gehandelte Stück, die der Markt dort noch nicht verdaut hat – sie drücken den Preis; er erholt sich Stunde um Stunde.";
            druck.MouseFilter = MouseFilterEnum.Stop;
            _preise.Add((stadt, ware, kauft, dort, daheim, druck));
        }
    }

    private void StaedteNachziehen(Stadtwirtschaft w)
    {
        foreach (var (stadt, ware, kauft, dort, daheim, druck) in _preise)
        {
            Setzen(dort, Zahlenformat.Kommazahl(kauft ? stadt.Ankaufpreis(ware) : stadt.Verkaufspreis(ware)));
            Setzen(daheim, Zahlenformat.Kommazahl(w.Markt[ware].Preis));
            var d = stadt.Druck(ware);
            Setzen(druck, d < 0.5 ? "–" : Zahlenformat.Ganz(d));
            druck.AddThemeColorOverride("font_color", d >= 0.5 ? Rot : Grau);
        }
    }
}
