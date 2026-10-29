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

    /// <summary>Gewünschtes Schutzgeld und Lösegeld je Haus, gewählte Geisel je Haus (Nummer der Person).</summary>
    private readonly Dictionary<string, double> _schutzgeld = new();
    private readonly Dictionary<string, double> _loesegeld = new();
    private readonly Dictionary<string, int> _geisel = new();

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
        Rollbereich.Einhuellen(this);
        Fensterrahmen.Anmelden(this, "Hinterzimmer", festhaltbar: true);
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

    private Label Text(Container eltern, string text, int groesse, Color? farbe = null, bool umbrechen = false)
    {
        var label = Hauptmenue.Text(eltern, text, groesse, farbe, umbruchBreite: umbrechen ? Breite - 50 : 0);
        if (label.TooltipText == "") label.TooltipText = Erklaerung(text);
        return label;
    }

    /// <summary>Kurze Erklärung zu einer Zeile des Hinterzimmers, nach ihrem Anfang gewählt (Tooltip).</summary>
    private static string Erklaerung(string t) =>
        t.StartsWith("Erpresserbrief") ? "Jemand droht mit einer Anklage. Zahlen, bestreiten oder dem Gericht stellen sind möglich; Beweise machen die Drohung ernst."
        : t.StartsWith("Lösegeld") || t.StartsWith("Zahlt ihr nicht bis") ? "Eine Angehörige wurde entführt. Wer nicht zahlt, verliert sie bis zum Jahresende an Sklavenhändler."
        : t.StartsWith("Schutzgeld") || t.StartsWith("Zahlt ihr nicht, legen") ? "Eine Bande verlangt Schutzgeld. Wer nicht zahlt, riskiert Brandstiftung an einem Betrieb."
        : t.StartsWith("Eure Leute ·") ? "Das Versteck eurer Gauner: Stufe und Besetzung bestimmen, wie viele Aufträge je Nacht möglich sind."
        : t.StartsWith("Noch ") ? "Aufträge, die in dieser Nacht noch ohne Zusatzkosten möglich sind."
        : t.StartsWith("• heute Nacht") ? "Dieser Auftrag wird heute um 20 Uhr ausgeführt."
        : t.StartsWith("   Diebstahl") || t.StartsWith("   Einbruch") || t.StartsWith("   Chance") ? "Erfolgschance und erwartete Beute des Auftrags; Wächter und Einrichtung des Ziels senken sie."
        : t.StartsWith("   Wegelagerer") ? "Wegelagerer lauern der nächsten beladenen Fahrt im Umland auf."
        : t.StartsWith("·") ? "Für diese Nacht bestellt."
        : t.Contains(" · ") && t.Contains("Gulden") ? "Haus mit Stand und Reinvermögen: ein mögliches Ziel eurer Gauner."
        : t.StartsWith("Wohnhaus:") ? "Das Wohnhaus als Ziel: Einbruch und Brandstiftung sind möglich, ein Hauswächter halbiert die Chance."
        : t.StartsWith("Karren im Umland") ? "Karren im Umland können Wegelagerer überfallen."
        : t.StartsWith("Dolch") ? "Ein Dolch gegen ein Oberhaupt: schwerste Tat, hohe Strafe, wenn sie auffliegt."
        : t.StartsWith("Entführung") ? "Eine Angehörige des Zielhauses entführen und Lösegeld fordern."
        : "";

    private void Knopf(Container eltern, string text, Action aktion, bool gesperrt = false, string? hinweis = null, Befehl? ap = null)
    {
        var knopf = new Button { Text = text, FocusMode = FocusModeEnum.None, Disabled = gesperrt, TooltipText = hinweis ?? "" };
        knopf.Pressed += aktion;
        if (ap is not null && _simulation?.Wirtschaft is { } w && _spieler is not null) UiHilfe.ApKosten(knopf, w, _spieler, ap);
        eltern.AddChild(knopf);
    }

    private static string Prozent(double anteil) => $"{Zahlenformat.Ganz(anteil * 100)} %";

    private static string StufenName(Stadtwirtschaft w, int stufe) =>
        w.Daten.Betriebsarten.Values.FirstOrDefault(a => a.Beruf == w.Diebe.Daten.Beruf && a.Stufe == stufe)?.Name ?? $"Stufe {stufe}";

    /// <summary>Was die eigenen Diebe gegen dieses Haus tun können: ausspähen, Schutzgeld verlangen, jemanden entführen.</summary>
    private void EigeneLeute(Stadtwirtschaft w, Konto haus, bool imKerker)
    {
        var diebe = w.Diebe;
        var waehrung = w.Daten.Allgemein.Waehrung;
        var schluessel = haus.Inhaber;

        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 10);
        _inhalt.AddChild(zeile);
        Text(zeile, "Eure Leute", 14).TooltipText = "Die Gauner, die für euer Haus arbeiten: Sie spähen aus, stehlen und erpressen, immer nachts ab 20 Uhr.";
        if (diebe.Auftraege.Any(a => a.Dieb == _spieler && a.Ziel == haus && a.Art == Diebesstueck.Auskundschaften))
            Text(zeile, "· spähen heute Nacht aus", 13, Gold);
        else
        {
            var grund = diebe.Verboten(_spieler!, Diebesstueck.Auskundschaften);
            Knopf(zeile, "Auskundschaften", () => Befehlen(new Auskundschaften(haus.Inhaber)), gesperrt: imKerker || grund is not null,
                hinweis: grund ?? $"Je unbekannte Tat {Prozent(diebe.Daten.Auskundschaften.Chance)} Chance auf einen Beweis – Grundlage für Erpressung und Anklage. Keine Spur.",
                ap: new Auskundschaften(""));
        }

        // Schutzgeld
        var versteck = diebe.Versteck(_spieler!)!;
        if (versteck.Stufe >= diebe.Daten.Schutzgeld.AbStufe)
        {
            var schutz = new HBoxContainer();
            schutz.AddThemeConstantOverride("separation", 10);
            _inhalt.AddChild(schutz);
            Text(schutz, "Schutzgeld", 14);
            if (diebe.BriefAn(haus, _spieler!, Diebesbriefart.Schutzgeld) is { } offen)
                Text(schutz, $"· {Zahlenformat.Ganz(offen.Betrag)} verlangt – wartet auf Antwort", 13, Gold);
            else
            {
                var vorschlag = Math.Max(100, Math.Round(diebe.DrohenderBrand(haus, _spieler!) * 0.8 / 50) * 50);
                var feld = new Zahlfeld { MinValue = 50, MaxValue = 20_000, Step = 50, Value = _schutzgeld.GetValueOrDefault(schluessel, vorschlag), Suffix = waehrung, CustomMinimumSize = new Vector2(150, 0) };
                feld.ValueChanged += wert => _schutzgeld[schluessel] = wert;
                schutz.AddChild(feld);
                Knopf(schutz, "Fordern", () => Befehlen(new SchutzgeldFordern(haus.Inhaber, _schutzgeld.GetValueOrDefault(schluessel, vorschlag))), gesperrt: imKerker,
                    hinweis: $"Zahlt das Haus nicht, brennt nachts sein wertvollster Betrieb (erwarteter Schaden ≈ {Zahlenformat.Ganz(diebe.DrohenderBrand(haus, _spieler!))}). " +
                             "Die Forderung ist ein Beweis gegen euch.", ap: new SchutzgeldFordern("", 0));
            }
        }

        // Entführung
        if (versteck.Stufe >= diebe.Daten.Entfuehrung.AbStufe && diebe.Entfuehrbar(haus).ToList() is { Count: > 0 } personen)
        {
            var entf = new HBoxContainer();
            entf.AddThemeConstantOverride("separation", 10);
            _inhalt.AddChild(entf);
            Text(entf, "Entführung", 14);
            var wahl = new OptionButton { FocusMode = FocusModeEnum.None };
            foreach (var p in personen) wahl.AddItem($"{p.Vorname} ({p.Alter(w.Familien.Jahr)})");
            var gewaehlt = Math.Max(0, personen.FindIndex(p => p.Nummer == _geisel.GetValueOrDefault(schluessel)));
            wahl.Select(gewaehlt);
            wahl.ItemSelected += i => _geisel[schluessel] = personen[(int)i].Nummer;
            entf.AddChild(wahl);
            var vorschlag = Math.Clamp(Math.Round(haus.Vermoegen * 0.1 / 100) * 100, 500, 5_000);
            var feld = new Zahlfeld { MinValue = 100, MaxValue = 50_000, Step = 100, Value = _loesegeld.GetValueOrDefault(schluessel, vorschlag), Suffix = waehrung, CustomMinimumSize = new Vector2(150, 0) };
            feld.ValueChanged += wert => _loesegeld[schluessel] = wert;
            entf.AddChild(feld);
            var grund = diebe.Verboten(_spieler!, Diebesstueck.Entfuehrung);
            Knopf(entf, "Verschleppen", () => Befehlen(new Entfuehren(haus.Inhaber, personen[Math.Max(0, personen.FindIndex(p => p.Nummer == _geisel.GetValueOrDefault(schluessel)))].Nummer,
                    _loesegeld.GetValueOrDefault(schluessel, vorschlag))), gesperrt: imKerker || grund is not null,
                hinweis: grund ?? $"Chance {Prozent(diebe.Entfuehrungschance(_spieler!))}. Zahlt das Haus nicht bis Ende nächsten Jahres, wird die Geisel in die Fremde verkauft. " +
                                  "Schweres Vergehen – vor Gericht droht der Kerker.", ap: new Entfuehren("", 0, 0));
        }
    }

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
        Text(_inhalt, "Bei Nacht und Nebel: Bestellt wird jetzt, ausgeführt heute um 20 Uhr. Bezahlt wird im Voraus. " +
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

        // ── Forderungen von Dieben an euch ──
        var diebe = w.Diebe;
        if (diebe.IstAktiv)
            foreach (var brief in diebe.Briefe.Where(b => b.Opfer == _spieler))
            {
                _inhalt.AddChild(new HSeparator());
                var dieb = brief.Dieb.Inhaber;
                if (brief.Art == Diebesbriefart.Loesegeld)
                {
                    var geisel = w.Familien.Von(_spieler)?.Lebende.FirstOrDefault(p => p.Nummer == brief.Geisel);
                    Text(_inhalt, $"Lösegeldbrief von {dieb}: {geisel?.Vorname ?? "Eine Angehörige"} ist in ihrer Gewalt – {Zahlenformat.Ganz(brief.Betrag)} {waehrung}.", 15, Rot, umbrechen: true);
                    Text(_inhalt, $"Zahlt ihr nicht bis Ende {brief.Jahr + 1}, wird {geisel?.Vorname ?? "die Geisel"} in die Fremde verkauft. " +
                                  "Solange erbt die Geisel nicht. Der Brief liegt als Beweis gegen den Entführer in eurem Beweisbuch – Entführung bringt Kerker.", 13, null, umbrechen: true);
                }
                else
                {
                    Text(_inhalt, $"Schutzgeld für {dieb}: {Zahlenformat.Ganz(brief.Betrag)} {waehrung}, sonst brennt es.", 15, Rot, umbrechen: true);
                    var ziel = diebe.Brandziel(_spieler);
                    Text(_inhalt, $"Zahlt ihr nicht, legen seine Leute Feuer in {ziel?.Name ?? "einem eurer Betriebe"} " +
                                  $"(Chance {Prozent(ziel is null ? 0 : diebe.Brandchance(ziel, brief.Dieb))}, Schaden ≈ {Zahlenformat.Ganz(ziel is null ? 0 : w.Gauner.Sabotageschaden(ziel))}). " +
                                  "Ein Wächter halbiert die Chance. Die Forderung liegt als Beweis in eurem Beweisbuch.", 13, null, umbrechen: true);
                }
                var zeile = new HBoxContainer();
                zeile.AddThemeConstantOverride("separation", 10);
                _inhalt.AddChild(zeile);
                var art = brief.Art;
                Knopf(zeile, $"Zahlen ({Zahlenformat.Ganz(brief.Betrag)} {waehrung})", () => Befehlen(new DiebesforderungBeantworten(dieb, art, true)),
                    gesperrt: _spieler.Vermoegen < brief.Betrag);
                Knopf(zeile, "Ablehnen", () => Befehlen(new DiebesforderungBeantworten(dieb, art, false)));
            }

        // ── Eure Leute (Dieb) ──
        var versteck = diebe.IstAktiv ? diebe.Versteck(_spieler) : null;
        if (versteck is not null)
        {
            _inhalt.AddChild(new HSeparator());
            var dd = diebe.Daten;
            Text(_inhalt, $"Eure Leute · {versteck.Name} ({versteck.Artdaten.Name}, {versteck.Belegt} Diebe)", 16, Gold);
            var buch = diebe.DiesesJahr(_spieler);
            Text(_inhalt, $"Noch {diebe.FreieAuftraege(_spieler)} von {dd.AuftraegeJeStufe[versteck.Stufe - 1]} Aufträgen heute Nacht frei – sie kosten kein Geld, nur Aktionspunkte. " +
                          $"Einbruch: bis zu {diebe.EinbruchStueck(_spieler)} Stück ins eigene Lager · Auskundschaften: {Prozent(dd.Auskundschaften.Chance)} je Tat · " +
                          $"Schutzgeld ab {StufenName(w, dd.Schutzgeld.AbStufe)} · Entführung ab {StufenName(w, dd.Entfuehrung.AbStufe)}. " +
                          $"Taschendiebe dieses Jahr: {Zahlenformat.Ganz(buch.Beute)} {waehrung}, {buch.Faenge} erwischt.", 12, Grau, umbrechen: true);
            foreach (var a in diebe.Auftraege.Where(a => a.Dieb == _spieler))
                Text(_inhalt, $"• heute Nacht: {a.Art.Name()} bei {a.Ziel.Inhaber}{(a.Betrieb is { } bn ? $" ({bn})" : "")}", 13, Gold);
            foreach (var b in diebe.Briefe.Where(b => b.Dieb == _spieler))
                Text(_inhalt, $"• {b.Art.Name()}forderung an {b.Opfer.Inhaber}: {Zahlenformat.Ganz(b.Betrag)} {waehrung} – wartet auf Antwort (bis Ende {b.Jahr + 1})", 13, Gold);
        }

        // ── Gegen andere Häuser ──
        foreach (var haus in w.Konten.Where(k => k != _spieler && k.IstAktiv))
        {
            _inhalt.AddChild(new HSeparator());
            Text(_inhalt, $"{haus.Inhaber} · {w.StandVon(haus).Name} · {Zahlenformat.Ganz(haus.Reinvermoegen)} {waehrung}", 16);
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
                    gesperrt: imKerker, hinweis: "Die Bande holt die wertvollste Ware aus dem Lager.", ap: new Beauftragen("", Gaunerstueck.Diebstahl));
                if (sabotage) Text(zeile, "· Sabotage bestellt", 13, Gold);
                else Knopf(zeile, $"Sabotage ({Zahlenformat.Ganz(g.Sabotage.Kosten)})", () => Befehlen(new Beauftragen(haus.Inhaber, Gaunerstueck.Sabotage, name)),
                    gesperrt: imKerker, hinweis: "Feuer in der Werkstatt: Stillstand und Reparaturkosten.", ap: new Beauftragen("", Gaunerstueck.Sabotage));
                if (versteck is not null)
                {
                    if (diebe.Auftraege.Any(a => a.Dieb == _spieler && a.Art == Diebesstueck.Einbruch && a.Betrieb == name))
                        Text(zeile, "· eure Leute steigen ein", 13, Gold);
                    else
                    {
                        var grund = diebe.Verboten(_spieler, Diebesstueck.Einbruch);
                        Knopf(zeile, "Einbruch (eigene Leute)", () => Befehlen(new Einbrechen(haus.Inhaber, name)), gesperrt: imKerker || grund is not null,
                            hinweis: grund ?? $"Chance {Prozent(diebe.Einbruchchance(b, _spieler))}, Beute ≈ {Zahlenformat.Ganz(diebe.MoeglicheBeute(b, _spieler).Sum(x => x.Wert))} ins eigene Lager – kein Hehler, keine Bande.",
                            ap: new Einbrechen("", ""));
                    }
                }
                Text(_inhalt, $"   Diebstahl: Chance {Prozent(unterwelt.Erfolgschance(Gaunerstueck.Diebstahl, b, _spieler))}, im Lager liegt " +
                              (beute.Count == 0 ? "nichts zu holen" : $"{string.Join(", ", beute.Select(x => $"{x.Stueck} {x.Ware}"))} (Hehler ≈ {Zahlenformat.Ganz(beute.Sum(x => x.Wert) * g.HehlerAnteil)})") +
                              $" · Sabotage: Chance {Prozent(unterwelt.Erfolgschance(Gaunerstueck.Sabotage, b, _spieler))}, Schaden beim Gegner ≈ {Zahlenformat.Ganz(unterwelt.Sabotageschaden(b))}",
                    12, Grau, umbrechen: true);
            }

            // Wohnhaus – Einbruch (Bargeld) und Brand (Reparatur, Ansehen fehlt bis zur Instandsetzung)
            if (unterwelt.GegenWohnhaus && w.Wohnen.Stufe(haus) is { } hausStufe)
            {
                var zeile = new HBoxContainer();
                zeile.AddThemeConstantOverride("separation", 10);
                _inhalt.AddChild(zeile);
                var beschaedigt = w.Wohnen.IstBeschaedigt(haus);
                Text(zeile, $"Wohnhaus: {hausStufe.Name}{(haus.Hauswaechter ? " (Hauswächter)" : "")}{(beschaedigt ? " – abgebrannt, in Instandsetzung" : "")}", 14);
                var wh = g.Wohnhaus!;
                if (bestellt.Any(a => a.Art == Gaunerstueck.Hauseinbruch)) Text(zeile, "· Einbruch bestellt", 13, Gold);
                else Knopf(zeile, $"Einbruch ({Zahlenformat.Ganz(wh.Einbruch.Kosten)})", () => Befehlen(new Beauftragen(haus.Inhaber, Gaunerstueck.Hauseinbruch)),
                    gesperrt: imKerker, hinweis: "Die Bande holt Bargeld aus der Truhe – kein Hehler nötig.", ap: new Beauftragen("", Gaunerstueck.Hauseinbruch));
                if (bestellt.Any(a => a.Art == Gaunerstueck.Brandstiftung)) Text(zeile, "· Brand bestellt", 13, Gold);
                else Knopf(zeile, $"Brand ({Zahlenformat.Ganz(wh.Brand.Kosten)})", () => Befehlen(new Beauftragen(haus.Inhaber, Gaunerstueck.Brandstiftung)),
                    gesperrt: imKerker || beschaedigt, hinweis: beschaedigt ? "Das Haus ist vom letzten Brand noch nicht instand gesetzt."
                        : "Feuer am Wohnhaus: Reparatur, und bis zur Instandsetzung fehlt dem Haus das Ansehen. Die Stufe bleibt.",
                    ap: new Beauftragen("", Gaunerstueck.Brandstiftung));
                Text(_inhalt, $"   Einbruch: Chance {Prozent(unterwelt.ErfolgschanceWohnhaus(Gaunerstueck.Hauseinbruch, haus, _spieler))}, Beute ≈ {Zahlenformat.Ganz(unterwelt.Hausbeute(haus))}" +
                              $" · Brand: Chance {Prozent(unterwelt.ErfolgschanceWohnhaus(Gaunerstueck.Brandstiftung, haus, _spieler))}, Reparatur ≈ {Zahlenformat.Ganz(unterwelt.Brandschaden(haus))}" +
                              (hausStufe.Ansehen > 0 ? $", −{Zahlenformat.Ganz(hausStufe.Ansehen)} Ansehen für einen Tag" : ""),
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
                    Knopf(zeile, $"Überfall ({Zahlenformat.Ganz(g.Ueberfall.Kosten)})", () => Befehlen(new Beauftragen(haus.Inhaber, Gaunerstueck.Ueberfall)), gesperrt: imKerker,
                        ap: new Beauftragen("", Gaunerstueck.Ueberfall));
                Text(_inhalt, $"   Wegelagerer (Stärke {Zahlenformat.Kommazahl(unterwelt.Staerke(_spieler))}) lauern bis zur nächsten Nacht der nächsten beladenen Fahrt auf. " +
                              "Erfolg = Stärke × (1 − Schutz der Eskorte). Ein Überfall ist ein schweres Vergehen: Vor Gericht droht Kerker.", 12, Grau, umbrechen: true);
            }

            // Krötenschleim – gegen das Oberhaupt
            if (w.Gesundheit.IstAktiv && w.Familien.Von(haus) is { } fremd)
            {
                var zeile = new HBoxContainer();
                zeile.AddThemeConstantOverride("separation", 10);
                _inhalt.AddChild(zeile);
                var o = fremd.Oberhaupt;
                Text(zeile, $"{o.OberhauptTitel} {o.Vorname}{(o.Krankheit is { } k ? $" (krank: {k.Name})" : "")}", 14);
                if (bestellt.Any(a => a.Art == Gaunerstueck.Kroetenschleim))
                    Text(zeile, "· Krötenschleim bestellt (heute Nacht)", 13, Gold);
                else
                    Knopf(zeile, $"Krötenschleim ({Zahlenformat.Ganz(unterwelt.Kosten(Gaunerstueck.Kroetenschleim, _spieler))}{(unterwelt.MischtSelbst(_spieler) ? ", selbst gemischt" : "")})", () => Befehlen(new Beauftragen(haus.Inhaber, Gaunerstueck.Kroetenschleim)),
                        gesperrt: imKerker, hinweis: "Ins Essen gemischt: Das Oberhaupt erkrankt (Ruhr, Lungenentzündung, Haarausfall …). Vergehen: Giftmischerei.",
                        ap: new Beauftragen("", Gaunerstueck.Kroetenschleim));
                Text(_inhalt, $"   Chance {Prozent(unterwelt.Erfolgschance(Gaunerstueck.Kroetenschleim, null, _spieler))} – Wächter helfen nicht. " +
                              "Wer schon krank ist, merkt nichts davon.", 12, Grau, umbrechen: true);

                // Giftdolch des Parfumeurs – nur Gegengift heilt
                if (w.Duft.IstAktiv && w.Duft.Daten.Gift.Dolche.Count > 0)
                {
                    var duft = w.Duft;
                    var giftZeile = new HBoxContainer();
                    giftZeile.AddThemeConstantOverride("separation", 10);
                    _inhalt.AddChild(giftZeile);
                    Text(giftZeile, "Dolch:", 14);
                    if (duft.Auftraege.Any(a => a.Auftraggeber == _spieler && a.Ziel == haus))
                        Text(giftZeile, "· Giftanschlag geplant (heute Nacht)", 13, Gold);
                    else
                        foreach (var (dolch, daten) in duft.Daten.Gift.Dolche.OrderBy(x => x.Value.Chance))
                        {
                            var preis = duft.Preis(_spieler, dolch);
                            var grund = duft.GiftVerboten(_spieler, haus, dolch);
                            var krankheit = w.Gesundheit.Daten.Krankheiten.GetValueOrDefault(daten.Krankheit);
                            Knopf(giftZeile, $"{dolch} ({(preis > 0 ? Zahlenformat.Ganz(preis) : "aus dem Lager")}, {Prozent(duft.Giftchance(_spieler, dolch))})",
                                () => Befehlen(new Giftanschlag(haus.Inhaber, dolch)), gesperrt: imKerker || grund is not null,
                                hinweis: grund ?? $"Heute Nacht: {daten.Krankheit} ({Prozent(krankheit?.Sterben ?? 0)} tödlich am Jahresende), nur Gegengift heilt. " +
                                                  "Vergehen: Giftanschlag – vor Gericht droht Kerker.",
                                ap: new Giftanschlag("", ""));
                        }
                }
            }

            // Hetzschrift – gegen das Ansehen des Hauses
            if (w.Kirchenwesen is { IstAktiv: true } kw)
            {
                var zeile = new HBoxContainer();
                zeile.AddThemeConstantOverride("separation", 10);
                _inhalt.AddChild(zeile);
                var hetze = kw.Daten.Hetzschrift;
                Text(zeile, $"Ansehen {Zahlenformat.Ganz(w.Rat.Ansehen(haus))}", 14);
                if (kw.GehetztBis(haus) is int bis)
                    Text(zeile, $"· eine Hetzschrift geht um (bis Ende {bis})", 13, Gold);
                else
                {
                    var preis = kw.Schriftpreis(_spieler, hetze.Ware);
                    Knopf(zeile, $"Hetzschrift ({(preis > 0 ? Zahlenformat.Ganz(preis) : "aus dem Lager")})", () => Befehlen(new HetzschriftVerbreiten(haus.Inhaber)),
                        gesperrt: imKerker || _spieler.Vermoegen < preis,
                        hinweis: $"{Zahlenformat.Ganz(hetze.Ansehen)} Ansehen weniger bis Ende nächsten Jahres (Wahl, Brautschau, Kirche). Vergehen: Verleumdung.",
                        ap: new HetzschriftVerbreiten(""));
                }
            }

            // Blume der Zwietracht – ein Ratsherr denkt schlechter über dieses Haus (Meinung, PR #231)
            if (w.Duft.IstAktiv && w.Rat.IstAktiv && w.Rat.Ratsherren.Count > 0)
            {
                var duft = w.Duft;
                var zeile = new HBoxContainer();
                zeile.AddThemeConstantOverride("separation", 10);
                _inhalt.AddChild(zeile);
                var ware = duft.Daten.Zwietracht.Ware;
                var preis = duft.Preis(_spieler, ware);
                Text(zeile, $"{ware} ({(preis > 0 ? Zahlenformat.Ganz(preis) : "aus dem Lager")}) bei", 14);
                var wahl = new OptionButton { FocusMode = FocusModeEnum.None };
                foreach (var r in w.Rat.Ratsherren) wahl.AddItem($"{r.Name} (Meinung {Zahlenformat.Ganz(w.Rat.Meinung(r, haus))})");
                zeile.AddChild(wahl);
                Knopf(zeile, "säen", () => Befehlen(new ZwietrachtSaeen(w.Rat.Ratsherren[Math.Max(0, wahl.Selected)].Name, haus.Inhaber)),
                    gesperrt: imKerker || duft.ZwietrachtVerboten(_spieler, w.Rat.Ratsherren[0].Name, haus) is not null,
                    hinweis: $"Die Meinung des Ratsherrn über {haus.Inhaber} sinkt um {Zahlenformat.Ganz(duft.Daten.Zwietracht.Gunst)} (Wahl, Anträge, Gericht; verblasst wie alle Taten). Vergehen: Intrige.",
                    ap: new ZwietrachtSaeen("", ""));
            }

            // Eigene Diebe: Auskundschaften, Schutzgeld, Entführung
            if (versteck is not null)
                EigeneLeute(w, haus, imKerker);

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
                    var feld = new Zahlfeld { MinValue = 50, MaxValue = 50_000, Step = 50, Value = _schweigegeld.GetValueOrDefault(schluessel, vorschlag), Suffix = waehrung, CustomMinimumSize = new Vector2(150, 0) };
                    feld.ValueChanged += wert => _schweigegeld[schluessel] = wert;
                    zeile.AddChild(feld);
                    Knopf(zeile, "Brief schicken", () => Befehlen(new Erpressen(haus.Inhaber, _schweigegeld.GetValueOrDefault(schluessel, vorschlag))),
                        gesperrt: imKerker, hinweis: "Der Brief selbst ist ein Beweis gegen euch.", ap: new Erpressen("", 0));
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
