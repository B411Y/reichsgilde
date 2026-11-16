using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Tafel „Start“ der Kartenwerkstatt – ein Teil der <c>partial class Kartenwerkstatt</c>: wer am Start mitspielt
/// (<see cref="EntwurfStart"/>). Oben der Spieler (Farbe), darunter die KI-Häuser (bis acht) als Liste; das gewählte Haus klappt auf: Name,
/// Volk, Oberhaupt, Farbe, Wappen und Startbetriebe mit Ware. Wo die Häuser wohnen und arbeiten, steht nicht in der Karte – das lost jede
/// Partie in den Bauzonen innerhalb der Mauer (seit 16.11.2026 ohne Werkstatt- und Wohnhausplätze und ohne Höfe).
///
/// Betriebe: „Werkstatt“ fragt nur die Betriebsart (<see cref="Kartenentwurf.Stadtwerkstaetten"/>); „Umlandbetrieb“ fragt die Stadtansicht nach
/// einem Platz (<see cref="Stadtansicht.WerkstattPlatzWaehlen"/>; passende grün), ein Klick übergibt ihn. Ein Klick auf einen belegten
/// Umlandbetrieb in der Karte klappt das Haus auf, dem er gehört. Jede Änderung ist ein Befehl des Entwurfs (mit Rückgängig).
/// </summary>
public partial class Kartenwerkstatt
{
    private VBoxContainer _startTafel = null!;
    private Button _reiterEigenschaften = null!, _reiterStart = null!, _reiterKarte = null!;
    private ScrollContainer _inhaltRolle = null!;

    /// <summary>Zeigt die Tafel rechts die Startbelegung (statt der Eigenschaften)?</summary>
    private bool _startOffen;

    /// <summary>Das aufgeklappte KI-Haus (Index in <see cref="EntwurfStart.Haeuser"/>; −1: keins).</summary>
    private int _startHaus = -1;

    /// <summary>Ein Startbetrieb, dessen Platz gewählt ist und dessen Art noch fehlt (Haus, Kennung des Platzes) – null: keiner.</summary>
    private (int Haus, string Platz)? _neuerBetrieb;

    private bool _startVorgemerkt;

    /// <summary>Das aufgeklappte Haus beim nächsten Aufbau in den sichtbaren Teil rollen (nach „Haus hinzufügen“).</summary>
    private bool _hausZeigen;

    // ═════════════════════════════════════════════════════════════════════════
    //  Reiter
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Reiter oben in der Tafel rechts: „Eigenschaften“ (des gewählten Teils), „Start“ (Startbelegung) und „Karte“ (Größe, Rand, Kamera-Start).</summary>
    private void ReiterBauen(VBoxContainer spalte)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 6);
        spalte.AddChild(zeile);
        var gruppe = new ButtonGroup();
        Button Reiter(string text, Action wahl, string tipp)
        {
            var knopf = new Button { Text = text, ToggleMode = true, ButtonGroup = gruppe, FocusMode = Control.FocusModeEnum.None, TooltipText = tipp,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 34) };
            knopf.AddThemeFontSizeOverride("font_size", 16);
            knopf.Pressed += wahl;
            zeile.AddChild(knopf);
            return knopf;
        }
        _reiterEigenschaften = Reiter("Eigenschaften", () => ReiterWaehlen(false), "Was das gewählte Stück ist: Name, Lage, Werte");
        _reiterStart = Reiter("Start", () => ReiterWaehlen(true), "Startbelegung: wo der Spieler und die KI-Häuser am Start wohnen und arbeiten");
        _reiterKarte = Reiter("Karte", KarteReiterWaehlen, "Die Karte als Ganzes: Größe, Randstreifen, Randlandschaft, Kamera-Start");
        _reiterEigenschaften.SetPressedNoSignal(true);
    }

    /// <summary>Wechselt den Reiter rechts (die Inhalte bleiben, nur einer ist zu sehen).</summary>
    private void ReiterWaehlen(bool start)
    {
        _startOffen = start;
        _karteOffen = false;
        (start ? _reiterStart : _reiterEigenschaften).SetPressedNoSignal(true);
        _eigenschaften.Visible = !start;
        _startTafel.Visible = start;
        _karteTafel.Visible = false;
        if (start) StartZeigen();
        _inhaltRolle.ScrollVertical = 0;
        GruppenMarkieren(); // Kartenwerkstatt.Palette.cs
    }

    /// <summary>Die Auswahl in der Karte hat sich geändert: Bei offener Tafel „Start“ klappt ein belegter Platz sein Haus auf, sonst die Eigenschaften.</summary>
    private void AuswahlGeaendert(EntwurfTeil? teil)
    {
        if (_startOffen && (teil is null || teil is EntwurfGebaeude g && _entwurf.BelegungVon(g.Kennung).Count > 0))
        {
            if (teil is EntwurfGebaeude gebaeude && _entwurf.BelegungVon(gebaeude.Kennung) is [var b, ..])
            {
                _startHaus = b.Haus;
                StartZeigen();
            }
            EigenschaftenZeigen(neu: true);
            return;
        }
        if ((_startOffen && _stadt.Kartenwerkzeug != Werkstattwerkzeug.Platz) || _karteOffen) ReiterWaehlen(false);
        EigenschaftenZeigen(neu: true);
        HilfeZeigen(); // ein gewähltes Felsstück hat eigene Tasten (Gizmo)
    }

    /// <summary>Die Startbelegung hat sich geändert: die Tafel am Ende des Bildes neu (ein Befehl kann mehrere Meldungen schicken).</summary>
    private void StartVormerken()
    {
        if (_startVorgemerkt || !_startOffen) return;
        _startVorgemerkt = true;
        Callable.From(() =>
        {
            _startVorgemerkt = false;
            if (_startOffen && !(GetViewport().GuiGetFocusOwner() is { } fokus && _startTafel.IsAncestorOf(fokus))) StartZeigen();
        }).CallDeferred();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Die Tafel
    // ═════════════════════════════════════════════════════════════════════════

    private EntwurfStart Start => _entwurf.Start;

    /// <summary>Baut die Tafel „Start“ neu nach dem Stand des Entwurfs.</summary>
    private void StartZeigen()
    {
        UiHilfe.KinderEntfernen(_startTafel);
        if (_startHaus >= Start.Haeuser.Count) _startHaus = Start.Haeuser.Count - 1;
        if (_neuerBetrieb is { } nb && (nb.Haus >= Start.Haeuser.Count || nb.Platz != "" && _entwurf.GebaeudeMit(nb.Platz) is null)) _neuerBetrieb = null;
        // Seit „nur noch Bauzonen“ (16.11.2026) gibt es keine Höfe und Plätze mehr: Die Tafel nennt nur, wer mitspielt.
        Hauptmenue.Text(_startTafel, "Wer am Start mitspielt. Wo die Häuser wohnen und arbeiten, lost jede Partie in den Bauzonen innerhalb der Mauer – "
            + "der Spieler wählt seinen Platz beim Start selbst. Nur die Umlandbetriebe haben ihren Platz in der Karte; dort tragen sie die Hausfarbe und eine hohe Fahne.",
            12, Grau, umbruchBreite: Rechtsbreite - 44);
        SpielerTeil();
        _startTafel.AddChild(new HSeparator());
        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 8);
        _startTafel.AddChild(kopf);
        var titel = Hauptmenue.Text(kopf, $"KI-Häuser ({Start.Haeuser.Count} von {Kartenentwurf.HoechstensHaeuser})", 16, Gold);
        titel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var dazu = Knopf(kopf, "Haus hinzufügen", () =>
        {
            var neu = _entwurf.HausHinzufuegen();
            if (neu >= 0) (_startHaus, _hausZeigen) = (neu, true);
            StartZeigen();
        }, "Ein neues KI-Haus mit freiem Namen, freier Farbe und freiem Wappen");
        dazu.Disabled = Start.Haeuser.Count >= Kartenentwurf.HoechstensHaeuser;
        if (Start.Haeuser.Count == 0)
            Hauptmenue.Text(_startTafel, "Noch keins – dann würfelt jede Partie ihre KI-Häuser selbst (Name, Wappen, Volk und Beruf).",
                12, Grau, umbruchBreite: Rechtsbreite - 44);
        for (var i = 0; i < Start.Haeuser.Count; i++) HausZeile(i);
    }

    // ── Spieler ──────────────────────────────────────────────────────────────

    private void SpielerTeil()
    {
        Hauptmenue.Text(_startTafel, "Spieler", 16, Gold);
        var raster = Raster();
        _startTafel.AddChild(raster);
        Zeile(raster, "Farbe", Farbwahl(Start.SpielerFarbe, f => _entwurf.SpielerFarbeSetzen(f), "Die Farbe des Spielerhauses in der Stadt (Dächer, Fahnen)"));
    }

    /// <summary>Die Berufe, die eine Werkstatt in der Stadt haben (aus den Wirtschaftsdaten; ohne Daten die zehn des Hauptmenüs).</summary>
    private IReadOnlyList<string> Berufe() => _daten is { } d
        ? d.Betriebsarten.Values.Where(a => a.InDerStadt && a.Stufe == 1).Select(a => a.Beruf).Distinct().ToList()
        : new[] { "Schmied", "Tischler", "Steinmetz", "Alchimist", "Wirt", "Prediger", "Dieb", "Gardist", "Geldleiher", "Parfumeur" };

    private void HausZeile(int i)
    {
        var h = Start.Haeuser[i];
        var auf = i == _startHaus;
        var knopf = new Button
        {
            ToggleMode = true, ButtonPressed = auf, FocusMode = Control.FocusModeEnum.None, Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(0, 40), TooltipText = auf ? "Zuklappen" : "Aufklappen: Name, Volk, Farbe, Wappen, Betriebe",
        };
        var inhalt = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        inhalt.AddThemeConstantOverride("separation", 8);
        knopf.AddChild(inhalt);
        inhalt.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, 4);
        inhalt.AddChild(new ColorRect { Color = Farbe(h.Farbe), CustomMinimumSize = new Vector2(10, 30), MouseFilter = Control.MouseFilterEnum.Ignore });
        var wappen = WappenBild.Neu(h.Wappen, 30);
        wappen.MouseFilter = Control.MouseFilterEnum.Ignore;
        inhalt.AddChild(wappen);
        var text = Hauptmenue.Text(inhalt, $"{h.Name}\n{Zusammenfassung(h)}", 13, Hell);
        text.MouseFilter = Control.MouseFilterEnum.Ignore;
        text.AddThemeConstantOverride("line_spacing", -3);
        knopf.Pressed += () =>
        {
            _startHaus = auf ? -1 : i;
            _neuerBetrieb = null;
            StartZeigen();
            if (!auf && h.Betriebe.FirstOrDefault(b => b.Platz != "") is { } umland) _stadt.WerkstattWaehlen(umland.Platz, hinfahren: true);
        };
        _startTafel.AddChild(knopf);
        if (!auf) return;
        HausEinzelheiten(i, h);
        if (_hausZeigen) SichtbarMachen(knopf);
        _hausZeigen = false;
    }

    private string Zusammenfassung(EntwurfHaus h)
    {
        var teile = new List<string> { h.Volk.Einzahl() };
        teile.AddRange(h.Betriebe.Select(b => ArtName(b.Art)));
        return string.Join(" · ", teile);
    }

    private void HausEinzelheiten(int i, EntwurfHaus h)
    {
        var raster = Raster();
        _startTafel.AddChild(raster);
        Zeile(raster, "Name", Textfeld(h.Name, text => _entwurf.HausUmbenennen(i, text), "So heißt das Haus im Spiel („Haus …“)"));
        var volk = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (var v in Enum.GetValues<Volk>()) volk.AddItem(v.Einzahl(), (int)v);
        volk.Select(volk.GetItemIndex((int)h.Volk));
        volk.ItemSelected += x => _entwurf.HausVolkSetzen(i, (Volk)volk.GetItemId((int)x));
        Zeile(raster, "Volk", volk);
        var geschlecht = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        geschlecht.AddItem("Patriarch", (int)Geschlecht.Mann);
        geschlecht.AddItem("Matriarchin", (int)Geschlecht.Frau);
        geschlecht.Select(geschlecht.GetItemIndex((int)h.Geschlecht));
        geschlecht.ItemSelected += x => _entwurf.HausGeschlechtSetzen(i, (Geschlecht)geschlecht.GetItemId((int)x));
        Zeile(raster, "Oberhaupt", geschlecht);
        Zeile(raster, "Farbe", Farbwahl(h.Farbe, f => _entwurf.HausFarbeSetzen(i, f), "Farbe des Hauses in der Stadt (Dächer, Fahnen, Rahmen in der Werkstatt)"));
        Zeile(raster, "Wappen", WappenWahl(h.Wappen, w => _entwurf.HausWappenSetzen(i, w)));

        Hauptmenue.Text(_startTafel, h.Betriebe.Count == 0 ? "Betriebe: noch keiner (ein Haus ohne Betrieb hat am Start nichts zu tun)" : "Betriebe", 14, Gold,
            umbruchBreite: Rechtsbreite - 44);
        for (var j = 0; j < h.Betriebe.Count; j++) BetriebZeile(i, j, h.Betriebe[j]);
        if (_neuerBetrieb is { } neu && neu.Haus == i) ArtWahl(i, neu.Platz);
        var knoepfe = new HBoxContainer();
        knoepfe.AddThemeConstantOverride("separation", 8);
        _startTafel.AddChild(knoepfe);
        // Die Werkstatt in der Stadt hat keinen Platz in der Karte (die Partie lost sie in eine Bauzone); ein Umlandbetrieb steht auf seinem Platz.
        Knopf(knoepfe, "Werkstatt", () =>
        {
            (_startHaus, _neuerBetrieb) = (i, (i, ""));
            StartZeigen();
        }, "Die Werkstatt des Hauses in der Stadt: nur die Betriebsart wählen – wo sie steht, lost jede Partie in den Bauzonen innerhalb der Mauer");
        Knopf(knoepfe, "Umlandbetrieb", () => PlatzFragen($"Umlandbetrieb für {h.Name}: einen freien Umlandbetrieb in der Karte anklicken",
            g => BetriebGrund(g), g => BetriebPlatzGewaehlt(i, g)), "Einen Umlandbetrieb der Karte anklicken (Grube, Holzschlag, Steinbruch …)");
        Knopf(knoepfe, "Haus entfernen", () =>
        {
            _entwurf.HausEntfernen(i);
            _startHaus = -1;
        }, "Das Haus aus der Startbelegung nehmen (seine Umlandbetriebe werden frei)");
        _startTafel.AddChild(new HSeparator());
    }

    /// <summary>Ein Startbetrieb: Art und Platz (die Werkstatt in der Stadt hat keinen), die erste Ware zur Wahl, Hinfahren und Entfernen.</summary>
    private void BetriebZeile(int haus, int nummer, EntwurfStartbetrieb b)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 6);
        _startTafel.AddChild(zeile);
        var platz = b.Platz == "" ? null : _entwurf.GebaeudeMit(b.Platz);
        var text = b.Platz == ""
            ? Wert($"{ArtName(b.Art)} · in der Stadt", "Werkstatt in der Stadt: Wo sie steht, lost jede Partie in den Bauzonen innerhalb der Mauer")
            : Wert($"{ArtName(b.Art)} · {platz?.Name ?? b.Platz}", $"Platz {b.Platz}" + (platz?.Betrieb is { } name ? $" („{name}“)" : ""));
        text.CustomMinimumSize = new Vector2(150, 0);
        text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        zeile.AddChild(text);
        var waren = Waren(b.Art);
        if (waren.Count > 0)
        {
            var ware = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Was der Betrieb am Start herstellt bzw. fördert" };
            ware.AddItem("Vorgabe", -1);
            foreach (var w in waren) ware.AddItem(w);
            ware.Select(b.Produkt is { } p && waren.IndexOf(p) is var k and >= 0 ? k + 1 : 0);
            ware.ItemSelected += x => _entwurf.BetriebProduktSetzen(haus, nummer, x <= 0 ? null : waren[(int)x - 1]);
            zeile.AddChild(ware);
        }
        if (b.Platz != "") Knopf(zeile, "→", () => _stadt.WerkstattWaehlen(b.Platz, hinfahren: true), "Hinfahren");
        Knopf(zeile, "×", () => _entwurf.BetriebEntfernen(haus, nummer), "Den Betrieb entfernen (der Name an einem Umlandbetrieb bleibt)");
    }

    /// <summary>Nach der Wahl des Platzes: die Betriebsart (eine Werkstatt der Stufe 1); ein Umlandbetrieb hat nur seine – dann gleich hinzufügen.</summary>
    private void BetriebPlatzGewaehlt(int haus, EntwurfGebaeude platz)
    {
        var arten = _entwurf.BetriebsartenFuer(platz);
        _startHaus = haus;
        if (arten.Count == 1)
        {
            _neuerBetrieb = null;
            _entwurf.BetriebHinzufuegen(haus, platz.Kennung, arten[0]);
            return;
        }
        _neuerBetrieb = (haus, platz.Kennung);
        StartZeigen();
    }

    /// <summary>Die Wahl der Betriebsart für die Werkstatt in der Stadt (Platz „“): Liste, „Hinzufügen“, „Abbrechen“.</summary>
    private void ArtWahl(int haus, string platz)
    {
        var g = platz == "" ? null : _entwurf.GebaeudeMit(platz);
        var arten = platz == "" ? _entwurf.Stadtwerkstaetten().ToList() : g is null ? new List<Betriebsart>() : _entwurf.BetriebsartenFuer(g).ToList();
        Hauptmenue.Text(_startTafel, platz == "" ? (arten.Count == 0 ? "Werkstatt in der Stadt: ohne Wirtschaftsdaten unbekannt" : "Werkstatt in der Stadt:") : $"Neuer Betrieb auf {g?.Name ?? platz}:", 13, Hell);
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 6);
        _startTafel.AddChild(zeile);
        var wahl = new OptionButton { FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var art in arten) wahl.AddItem(ArtName(art));
        zeile.AddChild(wahl);
        Knopf(zeile, "Hinzufügen", () =>
        {
            _neuerBetrieb = null;
            if (wahl.Selected >= 0 && wahl.Selected < arten.Count) _entwurf.BetriebHinzufuegen(haus, platz, arten[wahl.Selected]);
            else StartZeigen();
        });
        Knopf(zeile, "Abbrechen", () =>
        {
            _neuerBetrieb = null;
            StartZeigen();
        });
        SichtbarMachen(zeile);
    }

    /// <summary>Rollt die Tafel rechts so, dass das Feld zu sehen ist (erst nach dem Aufbau – vorher kennt es seine Lage nicht).</summary>
    private void SichtbarMachen(Control feld) => Callable.From(() =>
    {
        if (IsInstanceValid(feld) && feld.IsInsideTree()) _inhaltRolle.EnsureControlVisible(feld);
    }).CallDeferred();

    // ── Plätze ───────────────────────────────────────────────────────────────

    /// <summary>Fragt die Stadtansicht nach einem Platz (Werkzeug „Platz“); die Katalogwahl links verschwindet.</summary>
    private void PlatzFragen(string hinweis, Func<EntwurfGebaeude, string?> pruefen, Action<EntwurfGebaeude> gewaehlt)
    {
        KatalogWahlAufheben();
        _stadt.WerkstattPlatzWaehlen(hinweis, pruefen, g =>
        {
            gewaehlt(g);
            Melden($"{g.Name} gewählt.");
        });
    }

    /// <summary>Warum dieser Platz kein Wohnhaus für das Haus (−1: Spieler) sein kann – null: er kann.</summary>
    private string? BetriebGrund(EntwurfGebaeude g)
    {
        if (g.Art != Bausteinart.Umlandbetrieb) return "kein Umlandbetrieb";
        if (_entwurf.BelegungVon(g.Kennung) is [var x, ..]) return $"schon belegt: {Belegter(x)}";
        if (_entwurf.BetriebsartenFuer(g).Count == 0) return _daten is null ? "ohne Wirtschaftsdaten unbekannt" : "hier passt keine Betriebsart";
        return null;
    }

    private string Belegter(Platzbelegung b) => Start.Haeuser[b.Haus].Name;

    // ── Farben und Wappen ────────────────────────────────────────────────────

    private static Color Farbe(string? text) => text is { Length: > 0 } && Color.HtmlIsValid(text) ? Color.FromHtml(text) : new Color(0.5f, 0.5f, 0.5f);

    /// <summary>Die acht Hausfarben als Felder zum Anklicken, dazu „frei“ mit dem Farbwähler.</summary>
    private static Control Farbwahl(string? farbe, Action<string?> setzen, string tipp)
    {
        var zeile = new HBoxContainer { TooltipText = tipp };
        zeile.AddThemeConstantOverride("separation", 3);
        foreach (var f in Kartenentwurf.Hausfarben)
        {
            var gewaehlt = string.Equals(f, farbe, StringComparison.OrdinalIgnoreCase);
            var feld = new Button { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(22, 26), TooltipText = f };
            var stil = new StyleBoxFlat
            {
                BgColor = Color.FromHtml(f), BorderColor = gewaehlt ? Colors.White : new Color(0, 0, 0, 0.6f),
                BorderWidthLeft = gewaehlt ? 3 : 1, BorderWidthRight = gewaehlt ? 3 : 1, BorderWidthTop = gewaehlt ? 3 : 1, BorderWidthBottom = gewaehlt ? 3 : 1,
            };
            foreach (var art in new[] { "normal", "hover", "pressed" }) feld.AddThemeStyleboxOverride(art, stil);
            feld.Pressed += () => setzen(f);
            zeile.AddChild(feld);
        }
        var frei = new ColorPickerButton
        {
            Color = Farbe(farbe), EditAlpha = false, CustomMinimumSize = new Vector2(30, 26), FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Eine eigene Farbe wählen",
        };
        frei.PopupClosed += () =>
        {
            var neu = "#" + frei.Color.ToHtml(false);
            if (!string.Equals(neu, farbe, StringComparison.OrdinalIgnoreCase)) setzen(neu);
        };
        zeile.AddChild(frei);
        return zeile;
    }

    private static readonly string[] Teilungsnamen = { "ungeteilt", "geteilt", "gespalten", "schräg geteilt", "geviert", "Schildhaupt", "Balken", "Pfahl", "Sparren", "Bord" };

    /// <summary>Das Wappen: Vorschau, Teilung und Figur, Grund- und Figurfarbe, „Würfeln“ – jede Änderung ein Befehl.</summary>
    private static Control WappenWahl(Wappen wappen, Action<Wappen> setzen)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 8);
        zeile.AddChild(WappenBild.Neu(wappen, 64));
        var spalte = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        spalte.AddThemeConstantOverride("h_separation", 4);
        spalte.AddThemeConstantOverride("v_separation", 4);
        zeile.AddChild(spalte);
        OptionButton Wahl(IEnumerable<(string Text, int Wert)> eintraege, int gewaehlt, Action<int> neu, string tipp)
        {
            var wahl = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = tipp, ClipText = true, FitToLongestItem = false,
                CustomMinimumSize = new Vector2(76, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            foreach (var (text, wert) in eintraege) wahl.AddItem(text, wert);
            wahl.Select(wahl.GetItemIndex(gewaehlt));
            wahl.ItemSelected += i => neu(wahl.GetItemId((int)i));
            spalte.AddChild(wahl);
            return wahl;
        }
        Wahl(Enum.GetValues<Teilung>().Select(t => (Teilungsnamen[(int)t], (int)t)), (int)wappen.Teilung, t => setzen(wappen with { Teilung = (Teilung)t }), "Teilung des Schilds");
        Wahl(Enum.GetValues<Figur>().Select(f => (Wappen.Figurname(f), (int)f)), (int)wappen.Figur, f => setzen(wappen with { Figur = (Figur)f }), "Figur");
        var farben = Enum.GetValues<Tinktur>().Select(t => (Wappen.Name(t), (int)t)).ToList();
        var grund = Wahl(farben, (int)wappen.Farbe1, t => setzen(wappen with { Farbe1 = (Tinktur)t }), "Farbe des Grundes");
        var figur = Wahl(farben, (int)wappen.Farbe2, t => setzen(wappen with { Farbe2 = (Tinktur)t }), "Farbe der Figur");
        foreach (var wahl in new[] { grund, figur })
            for (var i = 0; i < wahl.ItemCount; i++)
            {
                var bild = Image.CreateEmpty(14, 14, false, Image.Format.Rgb8);
                bild.Fill(WappenBild.Farbe((Tinktur)wahl.GetItemId(i)));
                wahl.SetItemIcon(i, ImageTexture.CreateFromImage(bild));
            }
        var wuerfeln = new Button { Text = "Würfeln", FocusMode = Control.FocusModeEnum.None, TooltipText = "Ein zufälliges Wappen" };
        wuerfeln.Pressed += () => setzen(Wappen.Zufall(new Random()));
        spalte.AddChild(wuerfeln);
        if (wappen.Fehler() is { } fehler)
        {
            var text = new Label { Text = fehler, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(120, 0) };
            text.AddThemeFontSizeOverride("font_size", 11);
            text.AddThemeColorOverride("font_color", Rot);
            spalte.AddChild(text);
        }
        return zeile;
    }

    // ── Hilfen ───────────────────────────────────────────────────────────────

    private static GridContainer Raster()
    {
        var raster = new GridContainer { Columns = 2 };
        raster.AddThemeConstantOverride("h_separation", 10);
        raster.AddThemeConstantOverride("v_separation", 6);
        return raster;
    }

    /// <summary>Der Name einer Betriebsart aus den Wirtschaftsdaten („Gießerei“), ohne Daten ihr Bezeichner.</summary>
    private string ArtName(Betriebsart art) => _daten is { } d && d.KenntArt(art) ? d.Art(art).Name : art.ToString();

    /// <summary>Was ein Betrieb dieser Art herstellen (Werkstatt) bzw. fördern kann (Umland).</summary>
    private List<string> Waren(Betriebsart art) =>
        _daten is { } d && d.KenntArt(art) ? d.Art(art).Produkte.Concat(d.Art(art).Rohstoffe).ToList() : new List<string>();
}
