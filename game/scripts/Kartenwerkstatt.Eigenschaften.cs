using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Die Tafel rechts für Wege, Flächen (Streuflächen mit ihren Gruppen, Felder in Kartenwerkstatt.Karte.cs), Pflanzen und Mauern und die Regler von Pinsel und Radierer – ein Teil der <c>partial class Kartenwerkstatt</c>
/// (die Gebäude stehen in Kartenwerkstatt.Tafeln.cs). Jede Änderung in einem Feld ist ein Befehl des Entwurfs; ändert sich der gezeigte Teil
/// (auch durch Rückgängig), wird die Tafel neu gebaut – außer, in einem Textfeld wird gerade geschrieben.
/// </summary>
public partial class Kartenwerkstatt
{
    /// <summary>Steht in <see cref="_gezeigt"/>, solange die Tafel die Regler von Pinsel oder Radierer zeigt.</summary>
    private static readonly object PinselMarke = new();

    /// <summary>Wird in der Tafel gerade geschrieben (ein Text- oder Zahlenfeld hat den Fokus)?</summary>
    private bool EingabeOffen() => GetViewport().GuiGetFocusOwner() is { } fokus && _eigenschaften.IsAncestorOf(fokus);

    /// <summary>Baut die Tafel für einen Weg, eine Fläche, eine Pflanze oder eine Mauer.</summary>
    private void TeilTafel(EntwurfTeil teil)
    {
        UiHilfe.KinderEntfernen(_eigenschaften);
        _gezeigt = teil;
        var raster = new GridContainer { Columns = 2 };
        raster.AddThemeConstantOverride("h_separation", 10);
        raster.AddThemeConstantOverride("v_separation", 6);
        _eigenschaften.AddChild(raster);
        switch (teil)
        {
            case EntwurfWeg w: WegTafel(w, raster); break;
            case EntwurfBauzone z: BauzonenTafel(z, raster); break;
            case EntwurfNaturflaeche f: NaturTafel(f, raster); break;
            case EntwurfFeld fe: FeldTafel(fe, raster); break; // Kartenwerkstatt.Karte.cs
            case EntwurfPflanze p: PflanzenTafel(p, raster); break;
            case EntwurfMauer m: MauerTafel(m, raster); break;
        }
        var knoepfe = new HBoxContainer();
        knoepfe.AddThemeConstantOverride("separation", 8);
        _eigenschaften.AddChild(knoepfe);
        Knopf(knoepfe, "Hinfahren", () => _stadt.WerkstattWaehlen(teil, hinfahren: true));
        Knopf(knoepfe, "Löschen", () => Loeschen(teil), "Aus der Karte nehmen (Entf, wenn kein Griff gewählt ist)");
    }

    private void Loeschen(EntwurfTeil teil)
    {
        if (!_entwurf.Enthaelt(teil)) return;
        switch (teil)
        {
            case EntwurfWeg w: _entwurf.WegLoeschen(w); break;
            case EntwurfFlaeche f: _entwurf.FlaecheLoeschen(f); break;
            case EntwurfPflanze p: _entwurf.PflanzeLoeschen(p); break;
            case EntwurfMauer m: _entwurf.MauerLoeschen(m); break;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Weg
    // ═════════════════════════════════════════════════════════════════════════

    private void WegTafel(EntwurfWeg w, GridContainer raster)
    {
        Zeile(raster, "Name", Textfeld(w.Name, text => _entwurf.WegUmbenennen(w, text)));
        var art = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Die Art bestimmt Mindestbreite und ob Karren fahren" };
        foreach (var klasse in Wegarten) art.AddItem(Kartenweg.Anzeigename(klasse), (int)klasse);
        art.Select(art.GetItemIndex((int)w.Klasse));
        art.ItemSelected += i => _entwurf.WegKlasseSetzen(w, (Wegklasse)art.GetItemId((int)i));
        Zeile(raster, "Art", art);
        Zeile(raster, "Breite", Schieber(w.Mindestbreite, Math.Max(12, w.BreiteWirksam), 0.25, w.BreiteWirksam, Breite,
            wert => _entwurf.WegBreiteSetzen(w, wert), $"Nicht schmaler als {Breite(w.Mindestbreite)} ({Kartenweg.Anzeigename(w.Klasse)})"));
        var glatt = new CheckBox { Text = "glatte Kurve", ButtonPressed = w.IstGlatt, FocusMode = Control.FocusModeEnum.None,
            TooltipText = "An: eine weiche Kurve durch die Stützpunkte. Aus: gerade Strecken." };
        glatt.Toggled += an => _entwurf.WegKurveSetzen(w, an);
        Zeile(raster, "Verlauf", glatt);
        Zeile(raster, "Länge", Wert($"{Math.Round(w.Laenge)} m · {w.Punkte.Count} Stützpunkte"));
        Zeile(raster, "Verkehr", Wert(w.IstKarrenstrasse ? "Karren und Fußgänger" : "nur Fußgänger"));
        Hauptmenue.Text(_eigenschaften, "Griffe ziehen verschiebt die Stützpunkte, ein Klick auf die Linie fügt einen ein, Entf löscht den gewählten.",
            12, Grau, umbruchBreite: Rechtsbreite - 30);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Bauzone
    // ═════════════════════════════════════════════════════════════════════════

    private void BauzonenTafel(EntwurfBauzone z, GridContainer raster)
    {
        Zeile(raster, "Kennung", Textfeld(z.Kennung, text => _entwurf.BauzoneKennungSetzen(z, text), "Kurz und eindeutig (K1, V3 …) – die Prüfung meldet doppelte"));
        var lage = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Was hier gebaut werden darf und wie viel ein Platz kostet" };
        foreach (var l in Enum.GetValues<Lage>()) lage.AddItem(Stadtansicht.LageName(l), (int)l);
        lage.Select(lage.GetItemIndex((int)z.Art));
        lage.ItemSelected += i => _entwurf.BauzoneArtSetzen(z, (Lage)lage.GetItemId((int)i));
        Zeile(raster, "Lage", lage);
        Zeile(raster, "Fläche", Wert($"{Math.Round(Inhalt(z.Ecken))} m² · {z.Ecken.Count} Ecken"));
        Hauptmenue.Text(_eigenschaften, "Ecken rasten auf das 2-m-Raster. Griffe ziehen, Klick auf den Rand fügt eine Ecke ein, Ziehen innen verschiebt die Zone.",
            12, Grau, umbruchBreite: Rechtsbreite - 30);
    }

    /// <summary>Eine Zahl mit deutschem Komma.</summary>
    private static string Komma(double wert, string format) => wert.ToString(format, System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

    private static double Inhalt(IReadOnlyList<Kartenort> ecken)
    {
        var summe = 0.0;
        for (var i = 0; i < ecken.Count; i++)
        {
            var (a, b) = (ecken[i], ecken[(i + 1) % ecken.Count]);
            summe += a.X * b.Z - b.X * a.Z;
        }
        return Math.Abs(summe / 2);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Naturfläche
    // ═════════════════════════════════════════════════════════════════════════

    private void NaturTafel(EntwurfNaturflaeche f, GridContainer raster)
    {
        Zeile(raster, "Name", Textfeld(f.Name, text => _entwurf.NaturflaecheUmbenennen(f, text)));
        var art = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Nur die Beschreibung – was wächst, sagt die Mischung" };
        art.AddItem("Waldfläche");
        art.AddItem("Streufläche");
        art.Select(f.Art == "streu" ? 1 : 0);
        art.ItemSelected += i => _entwurf.NaturArtSetzen(f, i == 1 ? "streu" : "wald");
        Zeile(raster, "Art", art);
        Zeile(raster, "Dichte", Schieber(0, Math.Max(30, f.Dichte), 0.1, f.Dichte, d => $"{Komma(d, "0.0")} /1000 m²", d => _entwurf.DichteSetzen(f, d),
            "Streupunkte je 1.000 m² (ein Wald: je Punkt ein Baum; Streu: je Punkt ein Grüppchen)"));
        var wuerfeln = new HBoxContainer();
        wuerfeln.AddThemeConstantOverride("separation", 8);
        var startwert = Wert(f.Startwert.ToString());
        startwert.CustomMinimumSize = new Vector2(70, 0);
        wuerfeln.AddChild(startwert);
        Knopf(wuerfeln, "Neu würfeln", () => _entwurf.StartwertSetzen(f, (int)(GD.Randi() % 100000) + 1), "Gleiche Mischung, andere Lagen");
        Zeile(raster, "Startwert", wuerfeln);
        Zeile(raster, "Pflanzen", Wert($"{_stadt.WerkstattNaturAnzahl(f)} – unter Wegen, Gebäuden und Bauzonen bleibt es frei"));

        if (f.Art == "wald") MischungTafel(f);
        else GruppenTafel(f);

        Hauptmenue.Text(_eigenschaften, $"Aussparungen: {f.Aussparungen.Count}", 15, Gold);
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 8);
        _eigenschaften.AddChild(zeile);
        Knopf(zeile, "Aussparung zeichnen", () => KatalogWaehlen("aussparung"), "Ecken klicken, Doppelklick schließt – dort wächst nichts");
        for (var i = 0; i < f.Aussparungen.Count; i++)
        {
            var nummer = i;
            Knopf(zeile, $"{i + 1} löschen", () => _entwurf.AussparungLoeschen(f, nummer), $"Aussparung {i + 1} entfernen");
        }
    }

    /// <summary>Die Mischung eines Waldes: je Art ein Regler (Anteil), „Art hinzufügen“.</summary>
    private void MischungTafel(EntwurfNaturflaeche f)
    {
        Hauptmenue.Text(_eigenschaften, "Mischung", 15, Gold);
        var mischung = f.Mischung.OrderByDescending(e => e.Value).ToList();
        var regler = new Dictionary<string, HSlider>();
        void MischungUebernehmen()
        {
            var arten = regler.Where(r => r.Value.Value > 0).ToDictionary(r => r.Key, r => r.Value.Value);
            if (arten.Count == 0) Melden("Mindestens eine Art braucht einen Anteil über 0.", fehler: true);
            else _entwurf.MischungSetzen(f, arten);
        }
        var mischRaster = new GridContainer { Columns = 2 };
        mischRaster.AddThemeConstantOverride("h_separation", 10);
        _eigenschaften.AddChild(mischRaster);
        foreach (var (kennung, anteil) in mischung)
        {
            var schieber = Schieber(0, 100, 1, Math.Round(anteil * 100), a => $"{a:0} %", _ => MischungUebernehmen(),
                "Anteil dieser Art (die Anteile werden auf 100 % gerechnet; 0 nimmt die Art heraus)");
            regler[kennung] = schieber.GetChild<HSlider>(0);
            Zeile(mischRaster, ArtName(kennung), schieber);
        }
        var dazu = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Eine weitere Art mit 20 % in die Mischung nehmen" };
        dazu.AddItem("Art hinzufügen …", -1);
        var andere = _entwurf.Katalog.Bausteine.Where(b => b.Art == Bausteinart.Natur && !regler.ContainsKey(b.Kennung)).ToList();
        foreach (var b in andere) dazu.AddItem(ArtName(b));
        dazu.ItemSelected += i =>
        {
            if (i <= 0) return;
            var arten = regler.ToDictionary(r => r.Key, r => r.Value.Value);
            arten[andere[(int)i - 1].Kennung] = 20;
            _entwurf.MischungSetzen(f, arten);
        };
        _eigenschaften.AddChild(dazu);
        if (f.Gruppen.Count > 1 || f.Gruppen.Any(g => g.Teile.Count > 1))
            Hauptmenue.Text(_eigenschaften, "Dieser Wald mischt in Gruppen. Wer die Mischung hier ändert, macht daraus eine einfache: je Streupunkt ein Baum. Gruppen bearbeitet ihr, wenn ihr die Art auf „Streufläche“ stellt.",
                12, Grau, umbruchBreite: Rechtsbreite - 44);
    }

    /// <summary>
    /// Die Gruppen einer Streufläche einzeln: Je Streupunkt wird eine Gruppe nach ihrem Gewicht gewählt, ihre Teile liegen in einer Scheibe mit dem
    /// Radius; jeder Teil hat Arten (mit Gewicht, 0 nimmt die Art heraus) und eine Anzahl von–bis. Jede Änderung ist ein Befehl.
    /// </summary>
    private void GruppenTafel(EntwurfNaturflaeche f)
    {
        Hauptmenue.Text(_eigenschaften, "Gruppen", 15, Gold);
        Hauptmenue.Text(_eigenschaften, "An jedem Streupunkt wächst eine Gruppe (nach Gewicht gewählt): ihre Teile verteilt in einer Scheibe mit dem Radius.",
            12, Grau, umbruchBreite: Rechtsbreite - 44);
        for (var gi = 0; gi < f.Gruppen.Count; gi++)
        {
            var g = gi;
            var gruppe = f.Gruppen[g];
            var kopf = new HBoxContainer();
            kopf.AddThemeConstantOverride("separation", 6);
            _eigenschaften.AddChild(kopf);
            var titel = Hauptmenue.Text(kopf, $"Gruppe {g + 1}", 14, Hell);
            titel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            Knopf(kopf, "×", () =>
            {
                if (!_entwurf.GruppeEntfernen(f, g)) Melden("Eine Fläche braucht mindestens eine Gruppe – sonst wächst dort nichts.", fehler: true);
            }, "Die Gruppe entfernen");
            var werte = new HBoxContainer();
            werte.AddThemeConstantOverride("separation", 6);
            _eigenschaften.AddChild(werte);
            werte.AddChild(Klein("Gewicht"));
            werte.AddChild(Zahl(0, 100, 0.1, gruppe.Gewicht, "", w => _entwurf.GruppeSetzen(f, g, w, f.Gruppen[g].Radius), "Wie oft diese Gruppe gewählt wird (im Verhältnis zu den anderen)"));
            werte.AddChild(Klein("Radius"));
            werte.AddChild(Zahl(0, 30, 0.5, gruppe.Radius, "m", r => _entwurf.GruppeSetzen(f, g, f.Gruppen[g].Gewicht, r), "Halbmesser der Scheibe um den Streupunkt (0: alles genau darauf)"));
            for (var ti = 0; ti < gruppe.Teile.Count; ti++) GruppenteilZeile(f, g, ti);
            Knopf(_eigenschaften, "+ Teil", () => _entwurf.GruppenteilHinzufuegen(f, g, new Dictionary<string, double> { ["gras"] = 1 }, 1, 2),
                "Ein weiterer Teil der Gruppe (Gras, 1–2 Stück – danach anpassen)");
            _eigenschaften.AddChild(new HSeparator());
        }
        Knopf(_eigenschaften, "+ Gruppe", () => _entwurf.GruppeHinzufuegen(f), "Eine neue Gruppe: Gras, 1–3 Stück im Radius 2 m");
    }

    /// <summary>Ein Teil einer Gruppe: Anzahl von–bis, je Art ein Gewicht (0 nimmt sie heraus), „+ Art“, Entfernen.</summary>
    private void GruppenteilZeile(EntwurfNaturflaeche f, int g, int t)
    {
        var teil = f.Gruppen[g].Teile[t];
        Dictionary<string, double> Arten() => new(f.Gruppen[g].Teile[t].Arten);
        (int, int) Anzahl() => (f.Gruppen[g].Teile[t].Anzahl[0], f.Gruppen[g].Teile[t].Anzahl.Length > 1 ? f.Gruppen[g].Teile[t].Anzahl[1] : f.Gruppen[g].Teile[t].Anzahl[0]);
        var (von, bis) = Anzahl();
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 6);
        _eigenschaften.AddChild(zeile);
        zeile.AddChild(Klein($"Teil {t + 1} · Anzahl"));
        zeile.AddChild(Zahl(0, 50, 1, von, "", v => _entwurf.GruppenteilSetzen(f, g, t, Arten(), (int)v, Math.Max((int)v, Anzahl().Item2)), "So viele mindestens"));
        zeile.AddChild(Klein("–"));
        zeile.AddChild(Zahl(0, 50, 1, bis, "", b => _entwurf.GruppenteilSetzen(f, g, t, Arten(), Math.Min(Anzahl().Item1, (int)b), (int)b), "So viele höchstens"));
        Knopf(zeile, "×", () =>
        {
            if (!_entwurf.GruppenteilEntfernen(f, g, t)) Melden("Der letzte Teil einer Gruppe bleibt – entfernt sonst die ganze Gruppe.", fehler: true);
        }, "Den Teil entfernen");
        var arten = new GridContainer { Columns = 4 };
        arten.AddThemeConstantOverride("h_separation", 4);
        _eigenschaften.AddChild(arten);
        foreach (var (art, gewicht) in teil.Arten)
        {
            var name = Klein(ArtName(art));
            arten.AddChild(name);
            arten.AddChild(Zahl(0, 100, 0.1, gewicht, "", w =>
            {
                var neu = Arten();
                neu[art] = w;
                if (neu.Values.All(x => x <= 0)) Melden("Ein Teil braucht mindestens eine Art mit Gewicht über 0.", fehler: true);
                else _entwurf.GruppenteilSetzen(f, g, t, neu, Anzahl().Item1, Anzahl().Item2);
            }, "Gewicht der Art in diesem Teil (0 nimmt sie heraus)"));
        }
        var dazu = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Eine weitere Art in diesen Teil (Gewicht 1)" };
        dazu.AddItem("+ Art …", -1);
        var andere = _entwurf.Katalog.Bausteine.Where(b => b.Art == Bausteinart.Natur && !teil.Arten.ContainsKey(b.Kennung)).ToList();
        foreach (var b in andere) dazu.AddItem(ArtName(b));
        dazu.ItemSelected += i =>
        {
            if (i <= 0) return;
            var neu = Arten();
            neu[andere[(int)i - 1].Kennung] = 1;
            _entwurf.GruppenteilSetzen(f, g, t, neu, Anzahl().Item1, Anzahl().Item2);
        };
        _eigenschaften.AddChild(dazu);
    }

    private static Label Klein(string text)
    {
        var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", Grau);
        return label;
    }

    /// <summary>Ein kleines Zahlenfeld, das bei jeder Änderung (Pfeile, Enter, Verlassen) übernimmt.</summary>
    private static Zahlfeld Zahl(double min, double max, double schritt, double wert, string einheit, Action<double> uebernehmen, string tipp)
    {
        var feld = new Zahlfeld { MinValue = min, MaxValue = max, Step = schritt, Value = wert, Suffix = einheit, TooltipText = tipp, CustomMinimumSize = new Vector2(60, 0) };
        feld.GetLineEdit().AddThemeConstantOverride("minimum_character_width", 3);
        feld.ValueChanged += wert => uebernehmen(wert);
        return feld;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Mauer und Tore
    // ═════════════════════════════════════════════════════════════════════════

    private void MauerTafel(EntwurfMauer m, GridContainer raster)
    {
        Zeile(raster, "Name", Textfeld(m.Name, text => _entwurf.MauerUmbenennen(m, text)));
        var geschlossen = new CheckBox { Text = "geschlossen (Ring)", ButtonPressed = m.Geschlossen, FocusMode = Control.FocusModeEnum.None,
            TooltipText = "An: die letzte Ecke läuft zurück zur ersten. Aus: die Mauer endet offen (wie Köln am Rhein)." };
        geschlossen.Toggled += an =>
        {
            if (!_entwurf.MauerGeschlossenSetzen(m, an))
            {
                Melden("Ein Ring braucht mindestens drei Ecken.", fehler: true);
                geschlossen.SetPressedNoSignal(m.Geschlossen);
            }
        };
        Zeile(raster, "Verlauf", geschlossen);
        var laenge = 0.0;
        for (var i = 0; i < m.Strecken; i++) laenge += m.Strecke(i).A.Abstand(m.Strecke(i).B);
        Zeile(raster, "Länge", Wert($"{Math.Round(laenge)} m · {m.Ecken.Count} Ecken"));
        Zeile(raster, "Türme", Wert($"{m.Plan.Ecktuerme.Count} Ecktürme, {m.Plan.Tuerme.Count} Mauertürme – setzt das Spiel (an Knicken ab {Mauerplan.EckturmAbWinkel:0}°, etwa alle {Mauerplan.TurmAbstand:0} m)"));

        Hauptmenue.Text(_eigenschaften, m.Tore.Count == 0 ? "Tore: noch keins" : $"Tore: {m.Tore.Count}", 15, Gold);
        for (var i = 0; i < m.Tore.Count; i++)
        {
            var nummer = i;
            var tor = m.Tore[i];
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 6);
            _eigenschaften.AddChild(zeile);
            var name = Textfeld(tor.Name, text => _entwurf.TorUmbenennen(m, nummer, text), "Name des Tors (Passanten, Wege und die Prüfung nennen es so)");
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            zeile.AddChild(name);
            var nachts = new CheckBox { Text = "schließt nachts", ButtonPressed = tor.SchliesstNachts, FocusMode = Control.FocusModeEnum.None,
                TooltipText = "Nachts zu: Gauner kommen dann nicht hindurch" };
            nachts.AddThemeFontSizeOverride("font_size", 13);
            nachts.Toggled += an => _entwurf.TorSchliesstNachtsSetzen(m, nummer, an);
            zeile.AddChild(nachts);
            Knopf(zeile, "→", () => _stadt.WerkstattHinfahren(tor.Punkt), "Hinfahren");
            Knopf(zeile, "×", () => _entwurf.TorLoeschen(m, nummer), "Das Tor löschen");
        }
        Knopf(_eigenschaften, "Tor setzen", () => KatalogWaehlen("tor"), "Werkzeug „Tor“: Klick auf die Mauer setzt ein Tor");
        Hauptmenue.Text(_eigenschaften, "Griffe: Ecken gold, Tore blau. Ziehen verschiebt (Tore gleiten an der Mauer entlang), Klick auf die Linie fügt eine Ecke ein, Entf löscht den gewählten Griff. "
            + "Umschalt + Ziehen an der Linie verschiebt die ganze Mauer. Ein Weg durch ein Tor folgt ihm.",
            12, Grau, umbruchBreite: Rechtsbreite - 44);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Einzelne Pflanze
    // ═════════════════════════════════════════════════════════════════════════

    private void PflanzenTafel(EntwurfPflanze p, GridContainer raster)
    {
        Zeile(raster, "Art", Wert($"{ArtName(p.Katalog)} ({p.Katalog})"));
        Zeile(raster, "Punkt", Wert($"x {Kartenschreiber.Zahl(p.X)} · z {Kartenschreiber.Zahl(p.Z)} m"));
        var dreh = new Zahlfeld { MinValue = 0, MaxValue = 359.9, Step = 0.1, Suffix = "°", Value = p.Drehung, TooltipText = "Q/E: 15°-Schritte" };
        dreh.ValueChanged += wert => _entwurf.PflanzeDrehen(p, wert);
        Zeile(raster, "Drehung", dreh);
        var massstab = new Zahlfeld { MinValue = 0.3, MaxValue = 3, Step = 0.05, Value = p.Massstab };
        massstab.ValueChanged += wert => _entwurf.PflanzeMassstabSetzen(p, wert);
        Zeile(raster, "Maßstab", massstab);
        Zeile(raster, "Variante", Wert(p.Variante == 0 ? "aus dem Ort gewürfelt" : $"{p.Variante} von {p.Baustein?.Varianten ?? 1}"));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Pinsel und Radierer
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Gruppen der Arten in der Tafel des Pinsels: Baukasten des Modells → Überschrift.</summary>
    private static readonly (string Datei, string Titel)[] Artgruppen =
    {
        ("natur/natur_baeume", "Bäume"), ("natur/natur_buesche", "Büsche"), ("natur/natur_boden", "Boden"),
        ("natur/natur_steine", "Steine"), ("natur/natur_holz", "Holz"),
    };

    private void PinselTafel()
    {
        UiHilfe.KinderEntfernen(_eigenschaften);
        _gezeigt = PinselMarke;
        var radierer = _stadt.Kartenwerkzeug == Werkstattwerkzeug.Radierer;
        Hauptmenue.Text(_eigenschaften, radierer ? "Radierer" : "Pinsel", 17, Gold);
        var raster = new GridContainer { Columns = 2 };
        raster.AddThemeConstantOverride("h_separation", 10);
        raster.AddThemeConstantOverride("v_separation", 6);
        _eigenschaften.AddChild(raster);
        Zeile(raster, "Radius", Schieber(1, 30, 0.5, _stadt.PinselRadius, Breite, r => _stadt.PinselRadius = r, "Halbmesser des Kreises an der Maus"));
        if (radierer)
        {
            Hauptmenue.Text(_eigenschaften, "Löscht einzeln gesetzte Pflanzen. Die Natur einer Wald- oder Streufläche ändert man über die Fläche (Dichte, Mischung, Aussparung).",
                12, Grau, umbruchBreite: Rechtsbreite - 30);
            return;
        }
        Zeile(raster, "Dichte", Schieber(5, 200, 5, _stadt.PinselDichte, d => $"{d:0} /1000 m²", d => _stadt.PinselDichte = d,
            "Pflanzen je 1.000 m² des Strichs (ein Strich von 6 m Radius und 20 m Länge sind gut 350 m²)"));
        Hauptmenue.Text(_eigenschaften, "Arten", 15, Gold);
        var arten = new GridContainer { Columns = 2 };
        arten.AddThemeConstantOverride("h_separation", 6);
        _eigenschaften.AddChild(arten);
        foreach (var (datei, titel) in Artgruppen)
            foreach (var b in _entwurf.Katalog.Bausteine.Where(b => b.Art == Bausteinart.Natur && b.Modell == datei))
            {
                var kennung = b.Kennung;
                var kreuz = new CheckBox { Text = ArtName(b), ButtonPressed = _stadt.PinselArten.ContainsKey(kennung), FocusMode = Control.FocusModeEnum.None, TooltipText = titel };
                kreuz.AddThemeFontSizeOverride("font_size", 13);
                kreuz.Toggled += an =>
                {
                    if (an) _stadt.PinselArten[kennung] = 1;
                    else _stadt.PinselArten.Remove(kennung);
                };
                arten.AddChild(kreuz);
            }
        Hauptmenue.Text(_eigenschaften, "Wege, Gebäude, Bauzonen und die Stadt bleiben frei; neue Pflanzen halten Abstand zu schon gesetzten.",
            12, Grau, umbruchBreite: Rechtsbreite - 30);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Felder
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Ein Textfeld, das beim Verlassen (oder Enter) übernimmt, wenn der Text nicht leer ist und sich geändert hat.</summary>
    private static LineEdit Textfeld(string text, Action<string> uebernehmen, string? tipp = null)
    {
        var feld = new LineEdit { Text = text, TooltipText = tipp ?? "" };
        feld.TextSubmitted += _ => feld.ReleaseFocus();
        feld.FocusExited += () =>
        {
            if (feld.Text.Trim() is { Length: > 0 } neu && neu != text) uebernehmen(neu);
        };
        return feld;
    }

    /// <summary>
    /// Ein Schieberegler mit dem Wert daneben. Übernommen (<paramref name="fertig"/>) wird beim Loslassen – ein Schritt je Ziehen, nicht je
    /// Bildpunkt –, ein Klick auf die Leiste oder die Pfeiltasten gleich. Das Loslassen fragt jedes Bild die Maustaste ab: Das Signal
    /// <c>DragEnded</c> kam im Klicktest (Xvfb) nicht an, und ein verlorenes Loslassen hieße eine verlorene Änderung.
    /// </summary>
    private static HBoxContainer Schieber(double min, double max, double schritt, double wert, Func<double, string> text, Action<double> fertig, string? tipp = null)
    {
        var zeile = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        zeile.AddThemeConstantOverride("separation", 8);
        var regler = new HSlider
        {
            MinValue = min, MaxValue = max, Step = schritt, Value = wert, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(100, 0), TooltipText = tipp ?? "",
            FocusMode = Control.FocusModeEnum.None,
        };
        var anzeige = Wert(text(wert));
        anzeige.CustomMinimumSize = new Vector2(96, 0);
        double? vorher = null; // der Wert beim Anfassen, solange gezogen wird
        void Loslassen()
        {
            if (vorher is not { } alt) return;
            vorher = null;
            if (regler.Value != alt) fertig(regler.Value);
        }
        regler.DragStarted += () => vorher ??= regler.Value;
        regler.DragEnded += _ => Loslassen();
        regler.ValueChanged += v =>
        {
            anzeige.Text = text(v);
            if (vorher is null) fertig(v);
        };
        // Beim Anfassen springt der Wert schon (Klick neben den Griff) – der Wert vor dem Anfassen ist dann der vorige
        regler.GuiInput += e =>
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) vorher ??= regler.Value;
        };
        regler.Ready += () => regler.GetTree().ProcessFrame += Abfragen;
        regler.TreeExiting += () => regler.GetTree().ProcessFrame -= Abfragen;
        void Abfragen()
        {
            if (vorher is not null && !Input.IsMouseButtonPressed(MouseButton.Left)) Loslassen();
        }
        zeile.AddChild(regler);
        zeile.AddChild(anzeige);
        return zeile;
    }
}
