using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Ratskammer: alle Ämter mit Inhabern, Haltung zum Statut und eurer Gunst bei jedem Ratsherrn –
/// dazu Bewerbung, Geschenke und Anträge zum Statut, jeweils mit einer Prognose nach heutigem Stand.
///
/// Wie alle Bedienfelder ändert sie nichts selbst, sondern schickt Befehle an die Stadtwirtschaft.
/// </summary>
public partial class RatAnzeige : PanelContainer
{
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Grau = new(0.72f, 0.72f, 0.72f);
    private static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Rot = new(0.95f, 0.45f, 0.4f);

    private Simulation? _simulation;
    private Konto? _spieler;
    private VBoxContainer _inhalt = null!;
    private SpinBox _geschenk = null!;
    private Label _meldung = null!;

    /// <summary>Ergebnis eines Befehls – für das Protokoll.</summary>
    public event Action<string>? Gemeldet;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(720, 0);
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
        _inhalt.AddThemeConstantOverride("separation", 8);
        spalte.AddChild(_inhalt);

        _meldung = Hauptmenue.Text(spalte, "", 13, Grau, umbruchBreite: 670);
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
        Aufbauen(); // Gunst, Prognosen und Knöpfe haben sich geändert
    }

    /// <summary>Baut den Inhalt neu auf – beim Öffnen und nach jedem Befehl.</summary>
    public void Aufbauen()
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null) return;
        var rat = w.Rat;
        var waehrung = w.Daten.Allgemein.Waehrung;
        UiHilfe.KinderEntfernen(_inhalt);

        Hauptmenue.Text(_inhalt, rat.Daten.Kammer, 26, Gold, zentriert: true);
        Hauptmenue.Text(_inhalt, $"Geltendes Fron- und Sklavenstatut: {w.Statut.Kuerzel()} – {w.Statut.Name()}", 15, null, zentriert: true);

        // ── Die Ämter ──
        var geschenkZeile = new HBoxContainer();
        _inhalt.AddChild(geschenkZeile);
        Hauptmenue.Text(geschenkZeile, "Geschenk je Klick:", 14);
        _geschenk ??= new SpinBox { MinValue = 10, MaxValue = 5000, Step = 10, Value = 200, Suffix = waehrung, CustomMinimumSize = new Vector2(150, 0) };
        if (_geschenk.GetParent() is { } altesEltern) altesEltern.RemoveChild(_geschenk);
        geschenkZeile.AddChild(_geschenk);
        Hauptmenue.Text(geschenkZeile, $"   (+{Zahlenformat.Ganz(_geschenk.Value * rat.Daten.Gunst.JeGulden)} Gunst, verblasst um {Zahlenformat.Ganz(rat.Daten.Gunst.VerfallJeJahr * 100)} % im Jahr)", 12, Grau);

        var raster = new GridContainer { Columns = 6 };
        raster.AddThemeConstantOverride("h_separation", 14);
        raster.AddThemeConstantOverride("v_separation", 4);
        _inhalt.AddChild(raster);
        foreach (var kopf in new[] { "Stufe", "Amt", "Inhaber", "Haltung", "Eure Gunst", "" })
            Hauptmenue.Text(raster, kopf, 12, Grau);
        foreach (var amt in rat.Aemter)
        {
            var eigenes = amt.Konto == _spieler;
            var farbe = eigenes ? Gold : (Color?)null;
            Hauptmenue.Text(raster, amt.Stufe.ToString(), 14, farbe);
            Hauptmenue.Text(raster, $"{amt.Name} ({Zahlenformat.Ganz(amt.Daten.Einkommen)} {waehrung}/Jahr)", 14, farbe);
            Hauptmenue.Text(raster, amt.Ratsherr is { } r ? $"{r.Name}, {r.Daten.Volk.Einzahl()}" : amt.Inhaber, 14, farbe);
            if (amt.Ratsherr is { } ratsherr)
            {
                Hauptmenue.Text(raster, $"{ratsherr.Haltung.Kuerzel()} – {ratsherr.Haltung.Name()}", 13);
                Hauptmenue.Text(raster, Zahlenformat.Ganz(rat.Gunst(ratsherr, _spieler)), 14);
                var knopf = new Button { Text = "Beschenken", FocusMode = FocusModeEnum.None };
                knopf.Pressed += () => Befehlen(new Schenken(ratsherr.Name, _geschenk.Value));
                raster.AddChild(knopf);
            }
            else
            {
                Hauptmenue.Text(raster, amt.Konto is null ? "" : "Haus", 13, Grau);
                Hauptmenue.Text(raster, "", 14);
                Hauptmenue.Text(raster, "", 14);
            }
        }

        // ── Laufbahn ──
        _inhalt.AddChild(new HSeparator());
        var eigenesAmt = rat.AmtVon(_spieler);
        Hauptmenue.Text(_inhalt, $"Eure Laufbahn: {w.StandVon(_spieler).Name}, {(eigenesAmt is null ? "noch ohne Amt" : $"im Amt als {eigenesAmt.Name}")}", 16);
        if (rat.Bewerbungen.TryGetValue(_spieler, out var beworben) && rat.AmtNamens(beworben) is { } ziel)
        {
            Hauptmenue.Text(_inhalt, $"Ihr bewerbt euch als {ziel.Name}. Gewählt wird am Jahresende.", 14);
            PrognoseZeigen(rat, ziel);
        }
        else
        {
            var waehlbar = rat.WaehlbareAemter(_spieler).ToList();
            if (waehlbar.Count == 0)
            {
                var naechste = (eigenesAmt?.Stufe ?? 0) + 1;
                var noetig = rat.Aemter.Where(a => a.Stufe == naechste).Select(a => a.Daten.MindestStand).FirstOrDefault();
                Hauptmenue.Text(_inhalt, noetig is null ? "Höher geht es nicht – ihr habt das höchste Amt der Stadt."
                    : $"Für die nächste Stufe braucht ihr mindestens den Stand {noetig}.", 14, Grau);
            }
            else
            {
                var zeile = new HBoxContainer();
                zeile.AddThemeConstantOverride("separation", 10);
                _inhalt.AddChild(zeile);
                var auswahl = new OptionButton { FocusMode = FocusModeEnum.None };
                foreach (var a in waehlbar) auswahl.AddItem($"{a.Name} ({Zahlenformat.Ganz(a.Daten.Einkommen)} {waehrung}/Jahr)");
                zeile.AddChild(auswahl);
                var bewerben = new Button { Text = "Bewerben", FocusMode = FocusModeEnum.None };
                bewerben.Pressed += () => Befehlen(new AmtBewerben(waehlbar[auswahl.Selected].Name));
                zeile.AddChild(bewerben);
                foreach (var a in waehlbar) PrognoseZeigen(rat, a, $"{a.Name}: ");
            }
        }

        // ── Antrag zum Statut ──
        _inhalt.AddChild(new HSeparator());
        Hauptmenue.Text(_inhalt, "Antrag zum Fron- und Sklavenstatut", 16);
        if (rat.Antrag is { } antrag)
        {
            var (ja, nein, enthaltung, bm) = rat.AbstimmungsPrognose(antrag.Antragsteller, antrag.Statut);
            var durch = Stadtrat.IstAngenommen(ja, nein, bm);
            Hauptmenue.Text(_inhalt, $"{antrag.Antragsteller.Inhaber} beantragt Statut {antrag.Statut.Kuerzel()} – {antrag.Statut.Name()}. " +
                                     $"Stand heute: {ja} dafür, {nein} dagegen{(enthaltung > 0 ? $", {enthaltung} Enthaltung" : "")} → {(durch ? "würde angenommen" : "würde abgelehnt")}.",
                14, durch ? Gruen : Rot, umbruchBreite: 670);
        }
        else if (eigenesAmt is null)
            Hauptmenue.Text(_inhalt, "Anträge stellen nur Mitglieder des Rates.", 14, Grau);
        else
        {
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 10);
            _inhalt.AddChild(zeile);
            var statuten = Enum.GetValues<Fronstatut>().Where(s => s != w.Statut).ToList();
            var auswahl = new OptionButton { FocusMode = FocusModeEnum.None };
            foreach (var s in statuten)
            {
                var (ja, nein, _, bm) = rat.AbstimmungsPrognose(_spieler, s);
                auswahl.AddItem($"{s.Kuerzel()} – {s.Name()} (heute {ja}:{nein}{(Stadtrat.IstAngenommen(ja, nein, bm) ? ", angenommen" : "")})");
            }
            zeile.AddChild(auswahl);
            var stellen = new Button { Text = "Antrag stellen", FocusMode = FocusModeEnum.None };
            stellen.Pressed += () => Befehlen(new StatutBeantragen(statuten[auswahl.Selected]));
            zeile.AddChild(stellen);
        }
        Hauptmenue.Text(_inhalt, "Ratsherren stimmen ihrer Haltung zu – einem Nachbarstatut nur, wenn eure Gunst bei ihnen " +
                                 $"mindestens {Zahlenformat.Ganz(rat.Daten.Gunst.SchwelleNachbarstatut)} beträgt.", 12, Grau, umbruchBreite: 670);

        // ── Letzte Sitzung ──
        if (rat.LetzteSitzung.Count > 0)
        {
            _inhalt.AddChild(new HSeparator());
            Hauptmenue.Text(_inhalt, "Letzte Ratssitzung", 16);
            foreach (var eintrag in rat.LetzteSitzung)
                Hauptmenue.Text(_inhalt, "• " + eintrag, 13, null, umbruchBreite: 670);
        }
    }

    private void PrognoseZeigen(Stadtrat rat, Amt amt, string vorsatz = "")
    {
        var (sicher, unsicher, waehler) = rat.WahlPrognose(amt, _spieler!);
        var mehrheit = waehler / 2 + 1;
        var text = $"{vorsatz}Stand heute {sicher} sichere Stimme{(sicher == 1 ? "" : "n")}{(unsicher > 0 ? $" und {unsicher} unentschieden" : "")} " +
                   $"von {waehler} – für die Mehrheit braucht ihr {mehrheit}.";
        Hauptmenue.Text(_inhalt, text, 13, sicher >= mehrheit ? Gruen : Grau, umbruchBreite: 670);
    }
}
