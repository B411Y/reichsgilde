using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Der Gerichtssaal: Beweisbuch gegen jedes andere Haus, Spitzel, Anklage mit Prognose,
/// laufende Verfahren (mit Richtern und ihrer heutigen Überzeugung), eure eigenen Spuren
/// und was am letzten Gerichtstag geschah.
///
/// Wie alle Bedienfelder ändert er nichts selbst, sondern schickt Befehle an die Stadtwirtschaft.
/// </summary>
public partial class GerichtAnzeige : PanelContainer
{
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.72f, 0.72f, 0.72f);
    private static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);
    private const int Breite = 760;

    private Simulation? _simulation;
    private Konto? _spieler;
    private VBoxContainer _inhalt = null!;
    private Label _meldung = null!;

    /// <summary>Nur der Betrag wird gemerkt – das Zahlenfeld entsteht bei jedem Aufbau neu (siehe FamilienAnzeige).</summary>
    private double _geschenkBetrag = 200;

    /// <summary>Ergebnis eines Befehls – für das Protokoll.</summary>
    public event Action<string>? Gemeldet;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(Breite, 0);
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.11f, 0.10f, 1f),
            BorderColor = Gold * new Color(1, 1, 1, 0.8f),
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
        _inhalt.AddThemeConstantOverride("separation", 6);
        spalte.AddChild(_inhalt);

        _meldung = Hauptmenue.Text(spalte, "", 13, Grau, umbruchBreite: Breite - 50);
        var schliessen = Hauptmenue.Knopf(spalte, "Schließen", Hide);
        schliessen.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        Rollbereich.Einhuellen(this);
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

    private Button Knopf(Container eltern, string text, Action aktion, bool gesperrt = false, string? hinweis = null, Befehl? ap = null)
    {
        var knopf = new Button { Text = text, FocusMode = FocusModeEnum.None, Disabled = gesperrt, TooltipText = hinweis ?? "" };
        knopf.Pressed += aktion;
        if (ap is not null && _simulation?.Wirtschaft is { } w && _spieler is not null) UiHilfe.ApKosten(knopf, w, _spieler, ap);
        eltern.AddChild(knopf);
        return knopf;
    }

    /// <summary>Baut den Inhalt neu auf – beim Öffnen, nach jedem Befehl und zum Jahreswechsel.</summary>
    public void Aufbauen()
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null) return;
        UiHilfe.KinderEntfernen(_inhalt);
        var gericht = w.Justiz;
        if (!gericht.IstAktiv)
        {
            Text(_inhalt, "In dieser Stadt gibt es kein Gericht (Spielstand von vor dem Gericht).", 15, Grau);
            return;
        }
        var d = gericht.Daten;
        var waehrung = w.Daten.Allgemein.Waehrung;

        Hauptmenue.Text(_inhalt, "Stadtgericht", 26, Gold, zentriert: true);
        var richter = w.Rat.AmtNamens("Stadtrichter");
        Hauptmenue.Text(_inhalt, $"Stadtrichter: {richter?.Inhaber ?? "—"} · Gerichtstag am Jahresende, vor der Ratssitzung", 14, Grau, zentriert: true);
        Text(_inhalt, $"Jeder Beweis wiegt {Zahlenformat.Ganz(d.BeweiskraftJeBeweis)}, ein Richter spricht ab {Zahlenformat.Ganz(d.SchuldSchwelle)} schuldig. " +
                      $"Gunst (aus Geschenken) verschiebt sein Urteil um höchstens ±{Zahlenformat.Ganz(d.GunstHoechstwirkung)}. " +
                      $"Ab einer Beweiskraft von {Zahlenformat.Ganz(d.SichereBeweiskraft)} hilft dem Beklagten keine Bestechung mehr. " +
                      "Achtung: Jedes Geschenk an einen Ratsherrn ist Bestechung und hinterlässt eine Spur.", 12, Grau, umbrechen: true);

        // ── Beweisbuch: je anderes Haus ──
        _inhalt.AddChild(new HSeparator());
        Text(_inhalt, "Beweisbuch", 16);
        foreach (var haus in w.Konten.Where(k => k != _spieler && k.IstAktiv))
        {
            var beweise = gericht.Beweise(_spieler, haus).ToList();
            var kraft = gericht.Beweiskraft(_spieler, haus);
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 10);
            _inhalt.AddChild(zeile);
            Text(zeile, $"{haus.Inhaber}: {beweise.Count} {(beweise.Count == 1 ? "Beweis" : "Beweise")} (Beweiskraft {Zahlenformat.Ganz(kraft)})", 14,
                kraft >= d.SichereBeweiskraft ? Gruen : null);
            if (gericht.HatSpitzel(_spieler, haus))
                Text(zeile, "· Spitzel ist angesetzt", 13, Grau);
            else
                Knopf(zeile, $"Spitzel ansetzen ({Zahlenformat.Ganz(d.SpitzelKosten)} {waehrung})", () => Befehlen(new SpitzelAnsetzen(haus.Inhaber)),
                    ap: new SpitzelAnsetzen(""));
            if (gericht.VerfahrenGegen(_spieler, haus) is null)
                Knopf(zeile, $"Anklage erheben ({Zahlenformat.Ganz(d.Gerichtsgebuehr)} {waehrung})", () => Befehlen(new Anklagen(haus.Inhaber)),
                    gesperrt: beweise.Count == 0, hinweis: beweise.Count == 0 ? "Ohne Beweise nimmt das Gericht keine Anklage an." : null,
                    ap: new Anklagen(""));

            foreach (var b in beweise.TakeLast(5))
                Text(_inhalt, "   • " + b.Beschreibung, 12, Grau);
            if (beweise.Count > 5)
                Text(_inhalt, $"   … und {beweise.Count - 5} ältere", 12, Grau);
            if (beweise.Count > 0 && gericht.VerfahrenGegen(_spieler, haus) is null)
            {
                var (vorsitz, neutral) = gericht.PrognoseVorAnklage(_spieler, haus);
                var text = kraft >= d.SichereBeweiskraft
                    ? "   Prognose: sicherer Schuldspruch – selbst bestochene Richter kämen nicht dagegen an."
                    : $"   Prognose: Vorsitz ({vorsitz?.Richter.Name}) {(vorsitz?.Schuldig == true ? "schuldig" : "nicht schuldig")}, " +
                      $"unbeeinflusste Schöffen {(neutral ? "schuldig" : "nicht schuldig")}. " +
                      $"Bestechung durch {haus.Inhaber} könnte das Urteil noch kippen – sicher wird es ab {Zahlenformat.Ganz(Math.Ceiling(d.SichereBeweiskraft / d.BeweiskraftJeBeweis))} Beweisen.";
                Text(_inhalt, text, 12, kraft >= d.SichereBeweiskraft ? Gruen : neutral ? null : Grau, umbrechen: true);
            }
        }

        // ── Laufende Verfahren ──
        if (gericht.Verfahren.Count > 0)
        {
            _inhalt.AddChild(new HSeparator());
            Text(_inhalt, "Laufende Verfahren", 16);
            var geschenkZeile = new HBoxContainer();
            _inhalt.AddChild(geschenkZeile);
            Text(geschenkZeile, "Geschenk je Klick:", 13);
            var geschenk = new Zahlfeld { MinValue = 10, MaxValue = 5000, Step = 10, Value = _geschenkBetrag, Suffix = waehrung, CustomMinimumSize = new Vector2(150, 0) };
            geschenk.ValueChanged += wert => _geschenkBetrag = wert;
            geschenkZeile.AddChild(geschenk);
            Text(geschenkZeile, "   (Geschenke an Richter sind Bestechung – mit neuer Spur)", 12, Grau);

            foreach (var v in gericht.Verfahren)
            {
                var beteiligt = v.Anklaeger == _spieler || v.Beklagter == _spieler;
                var stimmen = gericht.Prognose(v);
                var schuldig = Stadtgericht.IstSchuldig(stimmen);
                Text(_inhalt, $"{v.Anklaeger.Inhaber} gegen {v.Beklagter.Inhaber} wegen {VergehenTexte.Aufzaehlung(gericht.Beweise(v.Anklaeger, v.Beklagter).Select(b => b.Art))} – " +
                              $"Stand heute: {(schuldig ? "SCHULDIG" : "Freispruch")} ({stimmen.Count(s => s.Schuldig)} von {stimmen.Count})", 14,
                    !beteiligt ? Grau : (schuldig == (v.Anklaeger == _spieler)) ? Gruen : Rot, umbrechen: true);
                var raster = new GridContainer { Columns = 4 };
                raster.AddThemeConstantOverride("h_separation", 14);
                _inhalt.AddChild(raster);
                foreach (var s in stimmen)
                {
                    Text(raster, $"   {s.Richter.Name}", 13);
                    Text(raster, s.Richter.Rolle, 12, Grau);
                    Text(raster, $"{Zahlenformat.Kommazahl(s.Ueberzeugung)} → {(s.Schuldig ? "schuldig" : "nicht schuldig")}", 13);
                    if (beteiligt && s.Richter.Ratsherr is { } r)
                        Knopf(raster, "Beschenken", () => Befehlen(new Schenken(r.Name, _geschenkBetrag)), ap: new Schenken("", 0));
                    else
                        Text(raster, "", 12);
                }
            }
        }

        // ── Eure Spuren ──
        _inhalt.AddChild(new HSeparator());
        Text(_inhalt, "Euer Ruf vor Gericht", 16);
        var spuren = gericht.SpurenVon(_spieler).ToList();
        Text(_inhalt, spuren.Count == 0
                ? "Ihr habt keine Spuren hinterlassen."
                : $"Ihr habt {spuren.Count} {(spuren.Count == 1 ? "Tat" : "Taten")} begangen ({VergehenTexte.Aufzaehlung(spuren.Select(s => s.Art))}), die noch nicht verjährt " +
                  $"{(spuren.Count == 1 ? "ist" : "sind")} (zusammen {Zahlenformat.Ganz(spuren.Sum(s => s.Betrag))} {waehrung}). " +
                  $"Verjährung nach {d.VerjaehrungJahre} Jahren.", 13, spuren.Count == 0 ? Grau : null, umbrechen: true);
        if (gericht.WurdeBeobachtet(_spieler))
            Text(_inhalt, "Am letzten Gerichtstag hat ein Spitzel über euch berichtet. Man hat ein Auge auf euch!", 13, Rot, umbrechen: true);
        if (gericht.PrangerBis(_spieler) is int bis)
            Text(_inhalt, $"Ihr steht bis {bis} am Pranger: {Zahlenformat.Ganz(d.PrangerAnsehen)} Ansehen weniger bei Wahl und Brautschau.", 13, Rot, umbrechen: true);

        // ── Letzter Gerichtstag ──
        if (gericht.LetzterGerichtstag.Count > 0)
        {
            _inhalt.AddChild(new HSeparator());
            Text(_inhalt, "Letzter Gerichtstag", 16);
            foreach (var eintrag in gericht.LetzterGerichtstag)
                Text(_inhalt, "• " + eintrag, 13, null, umbrechen: true);
        }
    }
}
