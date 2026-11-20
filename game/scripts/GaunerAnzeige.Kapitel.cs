using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Geheimnisse;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die übrigen Kapitel des Schwarzen Buchs: „Heute Nacht“ (Bestelltes, Nachtgeschehen), „Eigene Leute“ (nur mit Diebesbetrieb: Versteck und
/// die Aufträge an die eigenen Diebe gegen das Haus des Registers) und „Wachen, Briefe“ (Wächter der eigenen Betriebe, Briefe an euch mit
/// Antwort, eure eigenen Forderungen).
/// </summary>
public partial class GaunerAnzeige
{
    // ── Heute Nacht ─────────────────────────────────────────────────────────

    private void NachtSeiten(VBoxContainer links, VBoxContainer rechts, List<(Konto Ziel, string Text)> bestellt, int post)
    {
        HinterzimmerStil.Seitentitel(links, "Heute Nacht");
        HinterzimmerStil.Text(links, "Bestellt", "HzGruppe", tipp: "Diese Aufträge werden heute um 20 Uhr ausgeführt.");
        if (bestellt.Count == 0) HinterzimmerStil.Text(links, "Nichts bestellt. Taten bestellt ihr im Kapitel „Die Häuser“.", "HzKlein", "text_breite");
        foreach (var (_, text) in bestellt) HinterzimmerStil.Text(links, "• " + text, "HzText", "text_breite");
        HinterzimmerStil.Container<HSeparator>(links, "HzTrenner");
        HinterzimmerStil.Text(links, "Briefe an euch", "HzGruppe");
        if (post == 0) HinterzimmerStil.Text(links, "Keine Briefe.", "HzKlein");
        else
        {
            var zeile = HinterzimmerStil.Container<HBoxContainer>(links, "HzZeile");
            HinterzimmerStil.Text(zeile, post == 1 ? "Ein Brief wartet auf Antwort." : $"{post} Briefe warten auf Antwort.", "HzText");
            HinterzimmerStil.Knopf(zeile, "Zu den Briefen", () => Aufschlagen(Kapitel.Schutz));
        }

        HinterzimmerStil.Seitentitel(rechts, "Nachtgeschehen");
        var unterwelt = _w.Gauner;
        var nachrichten = unterwelt.NachrichtenFuer(_spieler!, false).Reverse().Concat(unterwelt.NachrichtenFuer(_spieler!, true).Reverse()).Take(6).ToList();
        if (nachrichten.Count == 0) HinterzimmerStil.Text(rechts, "Noch ist nichts geschehen.", "HzKlein");
        foreach (var n in nachrichten) HinterzimmerStil.Text(rechts, "• " + n, "HzText", "text_breite");
        rechts.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        var g = unterwelt.Daten;
        HinterzimmerStil.Text(rechts, $"Bestellt wird jetzt, ausgeführt heute um 20 Uhr, bezahlt im Voraus. Zeuge: {Prozent(g.ZeugenChance)}, bei einem Wächter {Prozent(g.ZeugenChanceWaechter)}. " +
                                      $"Der Hehler zahlt {Prozent(g.HehlerAnteil)} des Marktwerts.", "HzKlein", "text_breite",
            "Bei Nacht und Nebel: Jedes Gaunerstück hinterlässt eine Spur fürs Gericht, und mit dieser Chance erkennt euch ein Zeuge.");
    }

    // ── Eigene Leute ────────────────────────────────────────────────────────

    /// <summary>Links das Versteck und was die eigenen Diebe gerade tun, rechts die drei Aufträge gegen das Haus des Registers.</summary>
    private void LeuteSeiten(VBoxContainer links, VBoxContainer rechts, Betrieb versteck, Konto? haus)
    {
        var w = _w;
        var diebe = w.Diebe;
        var dd = diebe.Daten;
        HinterzimmerStil.Seitentitel(links, "Eigene Leute",
            "Die Gauner, die für euer Haus arbeiten: Sie spähen aus, stehlen und erpressen, immer nachts ab 20 Uhr.");
        HinterzimmerStil.Text(links, $"{versteck.Name} ({versteck.Artdaten.Name}, {versteck.Belegt} Diebe)", "HzText", "text_breite",
            "Das Versteck eurer Gauner: Stufe und Besetzung bestimmen, wie viele Aufträge je Nacht möglich sind.");
        HinterzimmerStil.Text(links, $"Noch {diebe.FreieAuftraege(_spieler!)} von {dd.AuftraegeJeStufe[versteck.Stufe - 1]} Aufträgen heute Nacht frei – sie kosten kein Geld, nur Aktionspunkte.",
            "HzGold", "text_breite");
        var buch = diebe.DiesesJahr(_spieler!);
        HinterzimmerStil.Text(links, $"Einbruch: bis zu {diebe.EinbruchStueck(_spieler!)} Stück ins eigene Lager · Auskundschaften: {Prozent(dd.Auskundschaften.Chance)} je Tat · " +
                                     $"Schutzgeld ab {StufenName(w, dd.Schutzgeld.AbStufe)} · Entführung ab {StufenName(w, dd.Entfuehrung.AbStufe)}.", "HzKlein", "text_breite");
        HinterzimmerStil.Text(links, $"Taschendiebe dieses Jahr: {Gulden(buch.Beute)}, {buch.Faenge} erwischt.", "HzKlein", "text_breite");
        HinterzimmerStil.Container<HSeparator>(links, "HzTrenner");
        HinterzimmerStil.Text(links, "Heute Nacht", "HzGruppe");
        var auftraege = diebe.Auftraege.Where(a => a.Dieb == _spieler).ToList();
        if (auftraege.Count == 0) HinterzimmerStil.Text(links, "Kein Auftrag. Einbrüche bestellt ihr im Kapitel „Die Häuser“ beim Betrieb.", "HzKlein", "text_breite");
        foreach (var a in auftraege)
            HinterzimmerStil.Text(links, $"• {a.Art.Name()} bei {a.Ziel.Inhaber}{(a.Betrieb is { } bn ? $" ({bn})" : "")}", "HzText", "text_breite");
        var forderungen = diebe.Briefe.Where(b => b.Dieb == _spieler).ToList();
        if (forderungen.Count > 0)
        {
            HinterzimmerStil.Container<HSeparator>(links, "HzTrenner");
            HinterzimmerStil.Text(links, "Eure Forderungen", "HzGruppe");
            foreach (var b in forderungen)
                HinterzimmerStil.Text(links, $"• {b.Art.Name()}forderung an {b.Opfer.Inhaber}: {Gulden(b.Betrag)} – wartet auf Antwort (bis Ende {b.Jahr + 1})", "HzText", "text_breite");
        }

        if (haus is null)
        {
            HinterzimmerStil.Text(rechts, "Kein anderes Haus ist mehr im Spiel.", "HzKlein", "text_breite");
            return;
        }
        var inhaber = haus.Inhaber;
        HinterzimmerStil.Seitentitel(rechts, $"Gegen {inhaber}", "Das Haus wählt ihr im Register über dem Buch.");
        var raster = Gruppe(rechts, "Aufträge an eure Leute");
        Tat(raster, "auge", "Auskundschaften", "Je unbekannte Tat ein Beweis möglich. Keine Spur.",
            new Auskundschaften(""), () => Befehlen(new Auskundschaften(inhaber)), chance: dd.Auskundschaften.Chance, preis: "frei",
            bestellt: diebe.Auftraege.Any(a => a.Dieb == _spieler && a.Ziel == haus && a.Art == Diebesstueck.Auskundschaften) ? "Eure Leute spähen heute Nacht aus." : null,
            grund: diebe.Verboten(_spieler!, Diebesstueck.Auskundschaften),
            tipp: "Beweise sind die Grundlage für Erpressung und Anklage.");

        var brand = diebe.DrohenderBrand(haus, _spieler!);
        var schutzVorschlag = Math.Max(100, Math.Round(brand * 0.8 / 50) * 50);
        var schutzOffen = diebe.BriefAn(haus, _spieler!, Diebesbriefart.Schutzgeld);
        Tat(raster, "muenze", "Schutzgeld", $"Sonst brennt sein Betrieb (Schaden ≈ {Zahlenformat.Ganz(brand)}).",
            new SchutzgeldFordern("", 0), () => Befehlen(new SchutzgeldFordern(inhaber, _schutzgeld.GetValueOrDefault(inhaber, schutzVorschlag))),
            preis: "frei", feld: Betragsfeld(_schutzgeld, inhaber, schutzVorschlag, 50, 20_000, 50), knopf: "Fordern",
            bestellt: schutzOffen is not null ? $"{Gulden(schutzOffen.Betrag)} verlangt – wartet auf Antwort." : null, bestelltKurz: "verlangt",
            grund: versteck.Stufe < dd.Schutzgeld.AbStufe ? $"Erst ab {StufenName(w, dd.Schutzgeld.AbStufe)}." : null,
            tipp: "Zahlt das Haus nicht, brennt nachts sein wertvollster Betrieb. Die Forderung ist ein Beweis gegen euch.");

        var personen = diebe.Entfuehrbar(haus).ToList();
        var loeseVorschlag = Math.Clamp(Math.Round(haus.Vermoegen * 0.1 / 100) * 100, 500, 5_000);
        var entfuehrbar = versteck.Stufe >= dd.Entfuehrung.AbStufe && personen.Count > 0;
        HBoxContainer? eingabe = null;
        if (entfuehrbar)
        {
            eingabe = new HBoxContainer { ThemeTypeVariation = "HzEng" };
            var wahl = new OptionButton { FocusMode = FocusModeEnum.None };
            foreach (var p in personen) wahl.AddItem($"{p.Vorname} ({p.Alter(w.Familien.Jahr)})");
            wahl.Select(Math.Max(0, personen.FindIndex(p => p.Nummer == _geisel.GetValueOrDefault(inhaber))));
            wahl.ItemSelected += i => _geisel[inhaber] = personen[(int)i].Nummer;
            wahl.TooltipText = "Wen eure Leute verschleppen sollen.";
            eingabe.AddChild(wahl);
            eingabe.AddChild(Betragsfeld(_loesegeld, inhaber, loeseVorschlag, 100, 50_000, 100));
        }
        Tat(raster, "kette", "Entführung", "Lösegeld bis Ende nächsten Jahres.",
            new Entfuehren("", 0, 0), () => Befehlen(new Entfuehren(inhaber, personen[Math.Max(0, personen.FindIndex(p => p.Nummer == _geisel.GetValueOrDefault(inhaber)))].Nummer,
                _loesegeld.GetValueOrDefault(inhaber, loeseVorschlag))),
            chance: diebe.Entfuehrungschance(_spieler!), preis: "frei", schwer: true, feld: eingabe, knopf: "Verschleppen",
            grund: versteck.Stufe < dd.Entfuehrung.AbStufe ? $"Erst ab {StufenName(w, dd.Entfuehrung.AbStufe)}."
                : personen.Count == 0 ? "In diesem Haus lässt sich niemand entführen."
                : diebe.Verboten(_spieler!, Diebesstueck.Entfuehrung),
            tipp: "Eine Angehörige des Hauses entführen und Lösegeld fordern. Zahlt das Haus nicht bis Ende nächsten Jahres, wird die Geisel in die Fremde verkauft. Schweres Vergehen – vor Gericht droht der Kerker.");
    }

    // ── Wachen, Briefe ──────────────────────────────────────────────────────

    private void SchutzSeiten(VBoxContainer links, VBoxContainer rechts)
    {
        var w = _w;
        var unterwelt = w.Gauner;
        var g = unterwelt.Daten;
        var gericht = w.Justiz;
        var diebe = w.Diebe;

        // ── Links: Wächter der eigenen Betriebe ──
        HinterzimmerStil.Seitentitel(links, "Wachen");
        var eigene = w.Betriebe.Where(b => b.Konto == _spieler).ToList();
        if (eigene.Count == 0) HinterzimmerStil.Text(links, "Ihr habt keinen Betrieb, den ein Wächter schützen könnte.", "HzKlein", "text_breite");
        foreach (var b in eigene)
        {
            var zeile = HinterzimmerStil.Container<HBoxContainer>(links, "HzZeile");
            var angriff = unterwelt.LetzterAngriff(b.Name);
            var spalte = HinterzimmerStil.Container<VBoxContainer>(zeile, "HzDicht");
            spalte.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            HinterzimmerStil.Text(spalte, b.Name, "HzText");
            HinterzimmerStil.Text(spalte, angriff is int j ? $"zuletzt angegriffen {j}" : b.Waechter ? "Ein Wächter steht hier." : "ohne Wächter",
                angriff is not null ? "HzGrund" : b.Waechter ? "HzGold" : "HzKlein");
            var name = b.Name;
            var waechter = b.Waechter;
            HinterzimmerStil.Knopf(zeile, waechter ? "Wächter entlassen" : $"Wächter anstellen ({Zahlenformat.Ganz(g.WaechterKosten)}/Jahr)",
                () => Befehlen(new WaechterSetzen(name, !waechter)), tipp: $"Ein Wächter kostet {Gulden(g.WaechterKosten)} im Jahr.").SizeFlagsVertical = SizeFlags.ShrinkCenter;
        }
        links.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        HinterzimmerStil.Text(links, $"Ein Wächter senkt die Chance jeder Bande und erkennt sie zu {Prozent(g.ZeugenChanceWaechter)} statt {Prozent(g.ZeugenChance)}.", "HzKlein", "text_breite");

        // ── Rechts: Briefe an euch – Erpresserbriefe, dann Forderungen von Dieben ──
        HinterzimmerStil.Seitentitel(rechts, "Briefe");
        HinterzimmerStil.Text(rechts, "An euch", "HzGruppe");
        var keine = true;
        foreach (var f in unterwelt.Forderungen.Where(f => f.Opfer == _spieler))
        {
            keine = false;
            if (f.Geheimnis is not null)
                GeheimnisBrief(rechts, f, w.Geheimnisse.ArtDesBriefes(f)); // Teil 3c: mit dem Siegel der Art (wenn sie bekannt ist)
            else
            {
                var busse = gericht.DrohendeBusse(f.Erpresser, _spieler!);
                var beweise = gericht.Beweise(f.Erpresser, _spieler!).Count();
                HinterzimmerStil.Text(rechts, $"Erpresserbrief von {f.Erpresser.Inhaber}: {Gulden(f.Betrag)} Schweigegeld, sonst Anklage.", "HzSchlecht", "text_breite",
                    "Jemand droht mit einer Anklage. Zahlen, bestreiten oder dem Gericht stellen sind möglich; Beweise machen die Drohung ernst.");
                HinterzimmerStil.Text(rechts, $"Er hat {beweise} Beweise gegen euch. Ein Schuldspruch kostete {Gulden(busse)} Buße, " +
                                              $"{gericht.Daten.PrangerJahre} Jahre Pranger{(gericht.DrohtKerker(f.Erpresser, _spieler!) ? " und Kerker" : "")}" +
                                              $"{(w.Rat.AmtVon(_spieler!) is { } amt ? $" und euer Amt als {amt.Name}" : "")}. " +
                                              "Sein Brief liegt als Beweis gegen ihn in eurem Beweisbuch.", "HzText", "text_breite");
            }
            var erpresser = f.Erpresser.Inhaber;
            Antwort(rechts, f.Betrag, () => Befehlen(new ErpressungBeantworten(erpresser, true)), () => Befehlen(new ErpressungBeantworten(erpresser, false)));
        }
        if (diebe.IstAktiv)
            foreach (var brief in diebe.Briefe.Where(b => b.Opfer == _spieler))
            {
                keine = false;
                var dieb = brief.Dieb.Inhaber;
                if (brief.Art == Diebesbriefart.Loesegeld)
                {
                    var geisel = w.Familien.Von(_spieler!)?.Lebende.FirstOrDefault(p => p.Nummer == brief.Geisel);
                    HinterzimmerStil.Text(rechts, $"Lösegeldbrief von {dieb}: {geisel?.Vorname ?? "Eine Angehörige"} ist in ihrer Gewalt – {Gulden(brief.Betrag)}.", "HzSchlecht", "text_breite",
                        "Eine Angehörige wurde entführt. Wer nicht zahlt, verliert sie bis zum Jahresende an Sklavenhändler.");
                    HinterzimmerStil.Text(rechts, $"Zahlt ihr nicht bis Ende {brief.Jahr + 1}, wird {geisel?.Vorname ?? "die Geisel"} in die Fremde verkauft. " +
                                                  "Solange erbt die Geisel nicht. Der Brief liegt als Beweis gegen den Entführer in eurem Beweisbuch – Entführung bringt Kerker.", "HzText", "text_breite");
                }
                else
                {
                    HinterzimmerStil.Text(rechts, $"Schutzgeld für {dieb}: {Gulden(brief.Betrag)}, sonst brennt es.", "HzSchlecht", "text_breite",
                        "Eine Bande verlangt Schutzgeld. Wer nicht zahlt, riskiert Brandstiftung an einem Betrieb.");
                    var ziel = diebe.Brandziel(_spieler!);
                    HinterzimmerStil.Text(rechts, $"Zahlt ihr nicht, legen seine Leute Feuer in {ziel?.Name ?? "einem eurer Betriebe"} " +
                                                  $"(Chance {Prozent(ziel is null ? 0 : diebe.Brandchance(ziel, brief.Dieb))}, Schaden ≈ {Zahlenformat.Ganz(ziel is null ? 0 : unterwelt.Sabotageschaden(ziel))}). " +
                                                  "Ein Wächter halbiert die Chance. Die Forderung liegt als Beweis in eurem Beweisbuch.", "HzText", "text_breite");
                }
                var art = brief.Art;
                Antwort(rechts, brief.Betrag, () => Befehlen(new DiebesforderungBeantworten(dieb, art, true)), () => Befehlen(new DiebesforderungBeantworten(dieb, art, false)));
            }
        if (keine) HinterzimmerStil.Text(rechts, "Keine Briefe.", "HzKlein");

        // ── Rechts: eure eigenen Forderungen ──
        HinterzimmerStil.Container<HSeparator>(rechts, "HzTrenner");
        HinterzimmerStil.Text(rechts, "Eure Forderungen", "HzGruppe");
        var eigeneForderungen = unterwelt.Forderungen.Where(f => f.Erpresser == _spieler)
            .Select(f => $"• Erpresserbrief an {f.Opfer.Inhaber}: {Gulden(f.Betrag)} – wartet auf Antwort")
            .Concat(diebe.IstAktiv
                ? diebe.Briefe.Where(b => b.Dieb == _spieler).Select(b => $"• {b.Art.Name()}forderung an {b.Opfer.Inhaber}: {Gulden(b.Betrag)} – wartet auf Antwort (bis Ende {b.Jahr + 1})")
                : Enumerable.Empty<string>()).ToList();
        if (eigeneForderungen.Count == 0) HinterzimmerStil.Text(rechts, "Keine offene Forderung.", "HzKlein");
        foreach (var text in eigeneForderungen) HinterzimmerStil.Text(rechts, text, "HzText", "text_breite");
    }

    /// <summary>Die zwei Knöpfe unter einem Brief an euch: zahlen (gesperrt, wenn das Geld fehlt) oder ablehnen. Das geht auch im Kerker und ohne Raum.</summary>
    private void Antwort(Container seite, double betrag, Action zahlen, Action ablehnen)
    {
        var zeile = HinterzimmerStil.Container<HBoxContainer>(seite, "HzZeile");
        var zuWenig = _spieler!.Vermoegen < betrag;
        HinterzimmerStil.Knopf(zeile, $"Zahlen ({Gulden(betrag)})", zahlen, tipp: zuWenig ? $"Dafür fehlt das Geld: {Gulden(betrag)} nötig." : "", gesperrt: zuWenig);
        HinterzimmerStil.Knopf(zeile, "Ablehnen", ablehnen);
    }

    /// <summary>
    /// Ein Erpresserbrief mit einem Geheimnis statt Beweisen (Teil 3c): links das Siegel seiner Art, rechts wer droht und womit. Zahlen heißt,
    /// er vergisst es; wer nicht zahlt, dem kann er es anzeigen oder bloßstellen. Ohne Art (älterer Spielstand, Geheimnis inzwischen vorbei) ohne Siegel.
    /// </summary>
    private void GeheimnisBrief(Container seite, Forderung f, Geheimnisart? art)
    {
        var gw = _w.Geheimnisse;
        var daten = gw.IstAktiv && art is { } a ? gw.Daten.Art(a) : null;
        var tipp = daten is null ? "Ein Geheimnis" : $"Ein Geheimnis – {daten.Name}: {daten.Beschreibung}";
        var reihe = HinterzimmerStil.Container<HBoxContainer>(seite, "HzZeile");
        var siegel = CharakterAnzeige.GeheimnisSiegelBild(art, HinterzimmerStil.Mass("siegel"), tipp); // ohne Datei bleibt nur der Text
        if (siegel is not null) reihe.AddChild(siegel);
        var spalte = HinterzimmerStil.Container<VBoxContainer>(reihe, "HzSeite");
        spalte.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var breite = siegel is null ? "text_breite" : "text_breite_siegel";
        HinterzimmerStil.Text(spalte, $"Erpresserbrief von {f.Erpresser.Inhaber}: {Gulden(f.Betrag)} Schweigegeld, sonst kommt ein Geheimnis ans Licht.", "HzSchlecht", breite,
            "Jemand weiß ein Geheimnis eures Hauses und verlangt Schweigegeld. Zahlt ihr, vergisst er es; zahlt ihr nicht, " +
            "kann er es anzeigen (wo es ein Vergehen ist) oder bloßstellen.");
        var worum = gw.WorumDerBrief(f) ?? daten?.Name ?? "ein Geheimnis eures Hauses";
        HinterzimmerStil.Text(spalte, $"Er weiß: {worum}. Zahlt ihr, schweigt er und vergisst es. Zahlt ihr nicht, kann er es anzeigen " +
                                      "oder in der ganzen Stadt bloßstellen.", "HzText", breite, tipp);
    }
}
