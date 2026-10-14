using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das Hinterzimmer: Banden anheuern (Diebstahl, Sabotage, Überfall), Erpresserbriefe schicken und beantworten,
/// Wächter für die eigenen Betriebe – und was nachts geschah.
///
/// Wie alle Bedienfelder ändert es nichts selbst, sondern schickt Befehle an die Stadtwirtschaft.
/// </summary>
public partial class GaunerAnzeige : PanelContainer
{
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.72f, 0.72f, 0.72f);
    private static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);
    private const int Breite = 780;

    private Simulation? _simulation;
    private Konto? _spieler;
    private VBoxContainer _inhalt = null!;
    private Label _meldung = null!;

    /// <summary>Gewünschtes Schweigegeld je Haus – nur der Wert wird gemerkt, das Feld entsteht bei jedem Aufbau neu.</summary>
    private readonly Dictionary<string, double> _schweigegeld = new();

    /// <summary>Ergebnis eines Befehls – für das Protokoll.</summary>
    public event Action<string>? Gemeldet;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(Breite, 0);
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.09f, 0.09f, 1f),
            BorderColor = new Color(0.55f, 0.45f, 0.35f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
        });
        var rand = new MarginContainer();
        foreach (var seite in new[] { "left", "right", "top", "bottom" })
            rand.AddThemeConstantOverride($"margin_{seite}", 22);
        AddChild(rand);
        var spalte = new VBoxContainer();
        spalte.AddThemeConstantOverride("separation", 8);
        rand.AddChild(spalte);

        _inhalt = new VBoxContainer();
        _inhalt.AddThemeConstantOverride("separation", 5);
        spalte.AddChild(_inhalt);

        _meldung = Hauptmenue.Text(spalte, "", 13, Grau, umbruchBreite: Breite - 50);
        var schliessen = Hauptmenue.Knopf(spalte, "Schließen", Hide);
        schliessen.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        Hide();
    }

    public void Verbinden(Simulation simulation, Konto spieler)
    {
        _simulation = simulation;
        _spieler = spieler;
    }

    public void Oeffnen()
    {
        _meldung.Text = "";
        Aufbauen();
        Show();
    }

    private void Befehlen(Befehl befehl)
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null) return;
        var ergebnis = w.Ausfuehren(_spieler, befehl);
        _meldung.Text = ergebnis.Erfolg ? ergebnis.Meldung : "✗ " + ergebnis.Meldung;
        _meldung.AddThemeColorOverride("font_color", ergebnis.Erfolg ? Gruen : Rot);
        Gemeldet?.Invoke(_meldung.Text);
        Aufbauen();
    }

    private Label Text(Container eltern, string text, int groesse, Color? farbe = null, bool umbrechen = false) =>
        Hauptmenue.Text(eltern, text, groesse, farbe, umbruchBreite: umbrechen ? Breite - 50 : 0);

    private void Knopf(Container eltern, string text, Action aktion, bool gesperrt = false, string? hinweis = null)
    {
        var knopf = new Button { Text = text, FocusMode = FocusModeEnum.None, Disabled = gesperrt, TooltipText = hinweis ?? "" };
        knopf.Pressed += aktion;
        eltern.AddChild(knopf);
    }

    private static string Prozent(double anteil) => $"{Zahlenformat.Ganz(anteil * 100)} %";

    /// <summary>Baut den Inhalt neu auf – beim Öffnen, nach jedem Befehl und zum Jahreswechsel.</summary>
    public void Aufbauen()
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null) return;
        UiHilfe.KinderEntfernen(_inhalt);
        var unterwelt = w.Gauner;
        if (!unterwelt.IstAktiv)
        {
            Text(_inhalt, "In dieser Stadt gibt es keine Unterwelt (Spielstand von vor den Gaunern).", 15, Grau);
            return;
        }
        var g = unterwelt.Daten;
        var gericht = w.Justiz;
        var waehrung = w.Daten.Allgemein.Waehrung;
        var imKerker = gericht.IstAktiv && gericht.ImKerker(_spieler);

        Hauptmenue.Text(_inhalt, "Hinterzimmer", 26, Gold, zentriert: true);
        Text(_inhalt, "Bei Nacht und Nebel: Bestellt wird jetzt, ausgeführt heute um 21 Uhr. Bezahlt wird im Voraus. " +
                      $"Jedes Gaunerstück hinterlässt eine Spur fürs Gericht, und mit {Prozent(g.ZeugenChance)} Chance " +
                      $"(bei einem Wächter {Prozent(g.ZeugenChanceWaechter)}) erkennt euch ein Zeuge. Der Hehler zahlt {Prozent(g.HehlerAnteil)} des Marktwerts.",
            12, Grau, umbrechen: true);
        if (imKerker)
            Text(_inhalt, $"Euer Oberhaupt sitzt bis Ende {gericht.KerkerBis(_spieler)} im Kerker – aus der Zelle heraus heuert niemand eine Bande an.", 14, Rot, umbrechen: true);

        // ── Erpresserbriefe an euch ──
        foreach (var f in unterwelt.Forderungen.Where(f => f.Opfer == _spieler))
        {
            _inhalt.AddChild(new HSeparator());
            var busse = gericht.DrohendeBusse(f.Erpresser, _spieler);
            var beweise = gericht.Beweise(f.Erpresser, _spieler).Count();
            Text(_inhalt, $"Erpresserbrief von {f.Erpresser.Inhaber}: {Zahlenformat.Ganz(f.Betrag)} {waehrung} Schweigegeld, sonst Anklage.", 15, Rot, umbrechen: true);
            Text(_inhalt, $"Er hat {beweise} Beweise gegen euch. Ein Schuldspruch kostete {Zahlenformat.Ganz(busse)} {waehrung} Buße, " +
                          $"{gericht.Daten.PrangerJahre} Jahre Pranger{(gericht.DrohtKerker(f.Erpresser, _spieler) ? " und Kerker" : "")}" +
                          $"{(w.Rat.AmtVon(_spieler) is { } amt ? $" und euer Amt als {amt.Name}" : "")}. " +
                          "Sein Brief liegt als Beweis gegen ihn in eurem Beweisbuch.", 13, null, umbrechen: true);
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 10);
            _inhalt.AddChild(zeile);
            var erpresser = f.Erpresser.Inhaber;
            Knopf(zeile, $"Zahlen ({Zahlenformat.Ganz(f.Betrag)} {waehrung})", () => Befehlen(new ErpressungBeantworten(erpresser, true)),
                gesperrt: _spieler.Vermoegen < f.Betrag);
            Knopf(zeile, "Ablehnen", () => Befehlen(new ErpressungBeantworten(erpresser, false)));
        }

        // ── Gegen andere Häuser ──
        foreach (var haus in w.Konten.Where(k => k != _spieler && k.IstAktiv))
        {
            _inhalt.AddChild(new HSeparator());
            Text(_inhalt, $"{haus.Inhaber} · {w.StandVon(haus).Name} · {Zahlenformat.Ganz(haus.Vermoegen)} {waehrung}", 16);
            var bestellt = unterwelt.Auftraege.Where(a => a.Auftraggeber == _spieler && a.Ziel == haus).ToList();
            foreach (var b in w.Betriebe.Where(b => b.Konto == haus))
            {
                var zeile = new HBoxContainer();
                zeile.AddThemeConstantOverride("separation", 10);
                _inhalt.AddChild(zeile);
                Text(zeile, $"{b.Name}{(b.Waechter ? " (Wächter)" : "")}", 14);
                var beute = unterwelt.MoeglicheBeute(b);
                var diebstahl = bestellt.Any(a => a.Art == Gaunerstueck.Diebstahl && a.Betrieb == b.Name);
                var sabotage = bestellt.Any(a => a.Art == Gaunerstueck.Sabotage && a.Betrieb == b.Name);
                var name = b.Name;
                if (diebstahl) Text(zeile, "· Diebstahl bestellt", 13, Gold);
                else Knopf(zeile, $"Diebstahl ({Zahlenformat.Ganz(g.Diebstahl.Kosten)})", () => Befehlen(new Beauftragen(haus.Inhaber, Gaunerstueck.Diebstahl, name)),
                    gesperrt: imKerker, hinweis: "Die Bande holt die wertvollste Ware aus dem Lager.");
                if (sabotage) Text(zeile, "· Sabotage bestellt", 13, Gold);
                else Knopf(zeile, $"Sabotage ({Zahlenformat.Ganz(g.Sabotage.Kosten)})", () => Befehlen(new Beauftragen(haus.Inhaber, Gaunerstueck.Sabotage, name)),
                    gesperrt: imKerker, hinweis: "Feuer in der Werkstatt: Stillstand und Reparaturkosten.");
                Text(_inhalt, $"   Diebstahl: Chance {Prozent(unterwelt.Erfolgschance(Gaunerstueck.Diebstahl, b, _spieler))}, im Lager liegt " +
                              (beute.Count == 0 ? "nichts zu holen" : $"{string.Join(", ", beute.Select(x => $"{x.Stueck} {x.Ware}"))} (Hehler ≈ {Zahlenformat.Ganz(beute.Sum(x => x.Wert) * g.HehlerAnteil)})") +
                              $" · Sabotage: Chance {Prozent(unterwelt.Erfolgschance(Gaunerstueck.Sabotage, b, _spieler))}, Schaden beim Gegner ≈ {Zahlenformat.Ganz(unterwelt.Sabotageschaden(b))}",
                    12, Grau, umbrechen: true);
            }

            if (unterwelt.HatUmlandfahrten(haus))
            {
                var zeile = new HBoxContainer();
                zeile.AddThemeConstantOverride("separation", 10);
                _inhalt.AddChild(zeile);
                Text(zeile, "Karren im Umland", 14);
                if (unterwelt.Hinterhalte.Any(h => h.Auftraggeber == _spieler && h.Ziel == haus))
                    Text(zeile, "· Eure Wegelagerer liegen auf der Lauer", 13, Gold);
                else if (bestellt.Any(a => a.Art == Gaunerstueck.Ueberfall))
                    Text(zeile, "· Überfall bestellt (heute Nacht)", 13, Gold);
                else
                    Knopf(zeile, $"Überfall ({Zahlenformat.Ganz(g.Ueberfall.Kosten)})", () => Befehlen(new Beauftragen(haus.Inhaber, Gaunerstueck.Ueberfall)), gesperrt: imKerker);
                Text(_inhalt, $"   Wegelagerer (Stärke {Zahlenformat.Kommazahl(unterwelt.Staerke(_spieler))}) lauern bis zur nächsten Nacht der nächsten beladenen Fahrt auf. " +
                              "Erfolg = Stärke × (1 − Schutz der Eskorte). Ein Überfall ist ein schweres Vergehen: Vor Gericht droht Kerker.", 12, Grau, umbrechen: true);
            }

            // Erpressung
            if (gericht.IstAktiv)
            {
                var beweise = gericht.Beweise(_spieler, haus).Count();
                var zeile = new HBoxContainer();
                zeile.AddThemeConstantOverride("separation", 10);
                _inhalt.AddChild(zeile);
                Text(zeile, $"Erpressung ({beweise}/{g.ErpressungMindestBeweise} Beweise)", 14, beweise >= g.ErpressungMindestBeweise ? null : Grau);
                if (unterwelt.ForderungAn(haus, _spieler) is { } offen)
                    Text(zeile, $"· Brief über {Zahlenformat.Ganz(offen.Betrag)} zugestellt – wartet auf Antwort", 13, Gold);
                else if (beweise >= g.ErpressungMindestBeweise)
                {
                    var vorschlag = Math.Round(gericht.DrohendeBusse(_spieler, haus) * 0.6 / 50) * 50;
                    var schluessel = haus.Inhaber;
                    var feld = new SpinBox { MinValue = 50, MaxValue = 50_000, Step = 50, Value = _schweigegeld.GetValueOrDefault(schluessel, vorschlag), Suffix = waehrung, CustomMinimumSize = new Vector2(150, 0) };
                    feld.ValueChanged += wert => _schweigegeld[schluessel] = wert;
                    zeile.AddChild(feld);
                    Knopf(zeile, "Brief schicken", () => Befehlen(new Erpressen(haus.Inhaber, _schweigegeld.GetValueOrDefault(schluessel, vorschlag))),
                        gesperrt: imKerker, hinweis: "Der Brief selbst ist ein Beweis gegen euch.");
                    Text(_inhalt, $"   Ein Schuldspruch kostete {haus.Inhaber} heute {Zahlenformat.Ganz(gericht.DrohendeBusse(_spieler, haus))} {waehrung} Buße – " +
                                  "wer weniger verlangt, wird eher bezahlt. Zahlt das Haus, verbrennen eure Beweise; zahlt es nicht, könnt ihr klagen.",
                        12, Grau, umbrechen: true);
                }
            }
        }

        // ── Eure Betriebe ──
        _inhalt.AddChild(new HSeparator());
        Text(_inhalt, "Eure Betriebe", 16);
        foreach (var b in w.Betriebe.Where(b => b.Konto == _spieler))
        {
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 10);
            _inhalt.AddChild(zeile);
            var angriff = unterwelt.LetzterAngriff(b.Name);
            Text(zeile, $"{b.Name}{(angriff is int j ? $" – zuletzt angegriffen {j}" : "")}", 14, angriff is not null ? Rot : null);
            var name = b.Name;
            Knopf(zeile, b.Waechter ? "Wächter entlassen" : $"Wächter anstellen ({Zahlenformat.Ganz(g.WaechterKosten)} {waehrung}/Jahr)",
                () => Befehlen(new WaechterSetzen(name, !b.Waechter)));
        }

        // ── Nachtgeschehen ──
        var nachrichten = unterwelt.NachrichtenFuer(_spieler, false).Reverse().Concat(unterwelt.NachrichtenFuer(_spieler, true).Reverse()).Take(6).ToList();
        if (nachrichten.Count > 0)
        {
            _inhalt.AddChild(new HSeparator());
            Text(_inhalt, "Nachtgeschehen", 16);
            foreach (var n in nachrichten)
                Text(_inhalt, "• " + n, 13, null, umbrechen: true);
        }
    }
}
