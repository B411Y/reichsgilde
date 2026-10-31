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

    /// <summary>Dasselbe für die Regler des Geländepinsels und der Rampe.</summary>
    private static readonly object GelaendeMarke = new();

    /// <summary>Dasselbe für die Regler des Bodenpinsels.</summary>
    private static readonly object BodenMarke = new();

    /// <summary>Wird in der Tafel gerade geschrieben (ein Text- oder Zahlenfeld hat den Fokus)?</summary>
    private bool EingabeOffen() => GetViewport().GuiGetFocusOwner() is { } fokus && _eigenschaften.IsAncestorOf(fokus);

    /// <summary>Baut die Tafel für einen Weg, eine Fläche, eine Pflanze oder eine Mauer.</summary>
    private void TeilTafel(EntwurfTeil teil)
    {
        EigenschaftenLeeren();
        _gezeigt = teil;
        _stadt.WegStreckeMarkieren(null, 0, 0, 0); // die Markierung des Belag-Abschnitts gehört zum gewählten Weg (WegTafel)
        var raster = new GridContainer { Columns = 2 };
        raster.AddThemeConstantOverride("h_separation", 10);
        raster.AddThemeConstantOverride("v_separation", 6);
        _eigenschaften.AddChild(raster);
        switch (teil)
        {
            case EntwurfWeg w: WegTafel(w, raster); break;
            case EntwurfFluss fl: FlussTafel(fl, raster); break; // Kartenwerkstatt.Wasser.cs
            case EntwurfSee se: SeeTafel(se, raster); break;
            case EntwurfFaehre fa: FaehreTafel(fa, raster); break;
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
            case EntwurfFluss or EntwurfSee: _entwurf.GewaesserLoeschen(teil); break;
            case EntwurfFaehre fa: _entwurf.FaehreLoeschen(fa); break;
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
        BelagZeilen(w, raster);
        Hauptmenue.Text(_eigenschaften, "Griffe ziehen verschiebt die Stützpunkte, ein Klick auf die Linie fügt einen ein, Entf löscht den gewählten.",
            12, Grau, umbruchBreite: Rechtsbreite - 30);
    }

    // ── Belag (Schritt 5d) ──

    /// <summary>Welcher Weg, welche Stufe und welcher Abschnitt (Meter entlang) gerade für den Belag gewählt sind.</summary>
    private EntwurfWeg? _belagWeg;
    private Belagstufe _belagStufe = Belagstufe.Kies;
    private double _belagVon, _belagBis;

    /// <summary>
    /// Der Startbelag des Weges: die Abschnitte als Liste, darunter Stufe, Abschnitt (zwei Regler, goldenes Band im Bild) und die Knöpfe
    /// „Abschnitt setzen“, „ganzer Weg“ und „Vorgabe“ (Belag nach der Klasse). Jede Änderung ist ein Schritt der Geschichte (Strg+Z).
    /// </summary>
    private void BelagZeilen(EntwurfWeg w, GridContainer raster)
    {
        var laenge = w.Laenge;
        if (!ReferenceEquals(_belagWeg, w))
        {
            _belagWeg = w;
            _belagVon = 0;
            _belagBis = Math.Round(laenge);
        }
        _belagBis = Math.Min(_belagBis, Math.Round(laenge));
        var abschnitte = w.BelagAbschnitte;
        var liste = new VBoxContainer();
        foreach (var a in abschnitte)
            liste.AddChild(Wert($"{Math.Round(a.Von)}–{Math.Round(a.Bis)} m: {Wegbelag.Anzeigename(a.Stufe)}"));
        Zeile(raster, "Belag", liste);
        if (w.Belag.Count == 0) Hauptmenue.Text(_eigenschaften, $"Ohne Angabe: {Wegbelag.Anzeigename(Wegbelag.Vorgabe(w.Klasse))} (Vorgabe der Art).", 12, Grau, umbruchBreite: Rechtsbreite - 30);

        var stufe = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Der Belag, mit dem das Spiel auf diesem Abschnitt beginnt" };
        foreach (var s in Enum.GetValues<Belagstufe>()) stufe.AddItem(Wegbelag.Anzeigename(s), (int)s);
        stufe.Select(stufe.GetItemIndex((int)_belagStufe));
        stufe.ItemSelected += i => _belagStufe = (Belagstufe)stufe.GetItemId((int)i);
        Zeile(raster, "Stufe", stufe);
        void Markieren() => _stadt.WegStreckeMarkieren(w.Verlauf.Select(p => new Vector2((float)p.X, (float)p.Z)).ToList(), (float)w.BreiteWirksam, _belagVon, _belagBis);
        Zeile(raster, "von", Schieber(0, Math.Max(1, Math.Round(laenge)), 1, _belagVon, Meter, wert => { _belagVon = wert; Markieren(); }, "Anfang des Abschnitts, in Metern entlang des Weges"));
        Zeile(raster, "bis", Schieber(0, Math.Max(1, Math.Round(laenge)), 1, _belagBis, Meter, wert => { _belagBis = wert; Markieren(); }, "Ende des Abschnitts, in Metern entlang des Weges"));
        Markieren();
        var knoepfe = new HBoxContainer();
        knoepfe.AddThemeConstantOverride("separation", 6);
        _eigenschaften.AddChild(knoepfe);
        Knopf(knoepfe, "Abschnitt setzen", () => _entwurf.BelagSetzen(w, Math.Min(_belagVon, _belagBis), Math.Max(_belagVon, _belagBis), _belagStufe),
            "Den gewählten Abschnitt mit dieser Stufe belegen");
        Knopf(knoepfe, "Ganzer Weg", () => _entwurf.BelagSetzen(w, _belagStufe), "Den ganzen Weg mit dieser Stufe belegen");
        Knopf(knoepfe, "Vorgabe", () => _entwurf.BelagSetzen(w, (Belagstufe?)null), "Alle Abschnitte entfernen: der Belag richtet sich wieder nach der Art des Weges");
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
        ("natur/natur_kit_baeume", "Bäume (Kit, teuer)"), ("natur/natur_kit_boden", "Boden (Kit)"), ("natur/natur_kit_steine", "Steine (Kit)"),
        ("props/props_gegenstaende", "Gegenstände"), ("props/props_dorf", "Zierteile"),
    };

    private void PinselTafel()
    {
        EigenschaftenLeeren();
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
    //  Gelände: Pinsel und Rampe
    // ═════════════════════════════════════════════════════════════════════════

    private void GelaendeTafel()
    {
        EigenschaftenLeeren();
        _gezeigt = GelaendeMarke;
        var rampe = _stadt.Kartenwerkzeug == Werkstattwerkzeug.Rampe;
        var art = _stadt.GelaendeArt;
        Hauptmenue.Text(_eigenschaften, rampe ? "Rampe" : Gelaendepinsel.First(p => p.Art == art).Name, 17, Gold);
        var raster = new GridContainer { Columns = 2 };
        raster.AddThemeConstantOverride("h_separation", 10);
        raster.AddThemeConstantOverride("v_separation", 6);
        _eigenschaften.AddChild(raster);
        if (rampe)
        {
            Zeile(raster, "Breite", Schieber(1, 40, 0.5, _stadt.RampeBreite, Breite, b => _stadt.RampeBreite = b, "Breite des Bandes mit gleichmäßigem Gefälle"));
            Zeile(raster, "Kante", Schieber(0, 20, 0.5, _stadt.RampeKante, Breite, k => _stadt.RampeKante = k, "So weit läuft das Gelände seitlich weich in die Rampe aus"));
            Hauptmenue.Text(_eigenschaften, "Das Band verbindet die Höhe unter dem ersten Klick mit der unter dem zweiten – mit gleichmäßigem Gefälle. Gut für Straßen am Hang. Gefälle über 15 % sperrt Bauplätze.",
                12, Grau, umbruchBreite: Rechtsbreite - 30);
            return;
        }
        // Pinsel-Paket 1: Überschriften „Pinsel“ und „Filter“ (Größe 15) werden klappbare Abschnitte (Kartenwerkstatt.Palette.cs)
        _eigenschaften.RemoveChild(raster);
        Hauptmenue.Text(_eigenschaften, "Pinsel", 15, Gold);
        _eigenschaften.AddChild(raster);
        Zeile(raster, "Radius", Schieber(1, 100, 1, _stadt.GelaendeRadius, Breite, r => _stadt.GelaendeRadius = r, "Halbmesser des Kreises an der Maus"));
        if (art is Pinselart.Glaetten or Pinselart.Ebnen or Pinselart.Terrassen)
            Zeile(raster, "Stärke", Schieber(0.05, 1, 0.05, _stadt.GelaendeStaerke, s => $"{s * 100:0} %", s => _stadt.GelaendeStaerke = s,
                art switch
                {
                    Pinselart.Ebnen => "100 % ebnet ganz, weniger zieht den Boden nur ein Stück zur Zielhöhe",
                    Pinselart.Terrassen => "100 % setzt jeden Punkt ganz auf seine Stufe, weniger nur ein Stück dorthin",
                    _ => "Wie stark Buckel je Tupfer ausgeglichen werden",
                }));
        else
            Zeile(raster, "Stärke", Schieber(0.05, art == Pinselart.Rauschen ? 4 : 2, 0.05, _stadt.GelaendeStaerke, s => $"{Komma(s, "0.00")} m", s => _stadt.GelaendeStaerke = s,
                art == Pinselart.Rauschen ? "Höhe der Buckel in Metern" : "Meter je Tupfer (ein Tupfer alle Radius/4 Meter entlang des Strichs)"));
        Zeile(raster, "Kante", Schieber(0, 1, 0.05, _stadt.GelaendeKante, k => $"{k * 100:0} %", k => _stadt.GelaendeKante = k, "0 % hart bis zum Rand, 100 % weich vom Mittelpunkt an"));
        if (art == Pinselart.Rauschen)
        {
            var start = new Zahlfeld { MinValue = 0, MaxValue = 99999, Step = 1, Value = _stadt.GelaendeStartwert, TooltipText = "Gleicher Startwert, gleiche Buckel" };
            start.ValueChanged += w => _stadt.GelaendeStartwert = (int)w;
            Zeile(raster, "Startwert", start);
            Zeile(raster, "Maßstab", Schieber(4, 100, 1, _stadt.GelaendeMassstab, Breite, m => _stadt.GelaendeMassstab = m, "Größe der Buckel in Metern"));
        }
        if (art is Pinselart.Heben or Pinselart.Senken)
            _eigenschaften.AddChild(Haken(art == Pinselart.Heben ? "Nur Mulden heben" : "Nur Spitzen kappen", _stadt.GelaendeNurAusgleich, an => _stadt.GelaendeNurAusgleich = an,
                art == Pinselart.Heben
                    ? "Hebt nur, was tiefer liegt als die Mitte des Pinsels (höchstens bis dorthin) – füllt Senken und Hangfuß, Kuppen bleiben"
                    : "Senkt nur, was höher liegt als die Mitte des Pinsels (höchstens bis dorthin) – kappt Grate für einen Bauplatz, Mulden bleiben"));
        if (art == Pinselart.Ebnen)
        {
            _eigenschaften.AddChild(Haken("Feste Zielhöhe", _stadt.GelaendeZielFest, an => _stadt.GelaendeZielFest = an,
                "Aus: Zielhöhe ist die Höhe unter dem ersten Klick. An: die Höhe darunter (eintippen oder mit der Pipette aufnehmen)"));
            ZielhoeheZeile("Zielhöhe", "Auf diese Höhe zieht der Pinsel den Boden (−20 bis +60 m, auf Zentimeter)", ein: () => _stadt.GelaendeZielFest = true);
        }
        if (art == Pinselart.Terrassen)
        {
            var stufen = ZweiSpalten();
            Zeile(stufen, "Stufenhöhe", Schieber(Pinsel.MinStufe, 10, 0.25, _stadt.GelaendeStufenhoehe, Breite, h => _stadt.GelaendeStufenhoehe = h, "Höhe einer Stufe in Metern"));
            Zeile(stufen, "Böschung", Schieber(0, 1, 0.05, _stadt.GelaendeBoeschung, b => $"{b * 100:0} %",
                b => _stadt.GelaendeBoeschung = b, "Anteil jeder Stufe, über den es weich hinaufgeht: 0 % senkrechte Kante, 100 % sanfte Wellen"));
            ZielhoeheZeile("Bezugshöhe", "Hier liegt eine Stufe, die übrigen im Abstand der Stufenhöhe darüber und darunter (Vorgabe 0 m)");
        }
        var text = art switch
        {
            Pinselart.Heben => "Mit gedrückter Maus über den Boden fahren: Er steigt unter dem Pinsel.",
            Pinselart.Senken => "Mit gedrückter Maus über den Boden fahren: Er sinkt unter dem Pinsel.",
            Pinselart.Glaetten => "Gleicht kleine Unebenheiten aus; die großen Formen bleiben.",
            Pinselart.Ebnen => "Ohne feste Zielhöhe bestimmt der erste Klick sie; alles im Strich wird dorthin gezogen – so entsteht eine Terrasse für Gebäude. Strg+Klick nimmt die Höhe auf.",
            Pinselart.Terrassen => "Treppt den Boden in gleich hohe Stufen: jeder Punkt geht auf die nächste Stufe – für Weinberge, Plateaus und Hochufer. Strg+Klick nimmt die Bezugshöhe auf.",
            _ => "Lässt kleine Hügel und Mulden wachsen. Ohne Zufall: gleicher Startwert, gleiche Buckel.",
        };
        Hauptmenue.Text(_eigenschaften, text + "\nWege und Mauern ebnen den Boden unter sich quer ein; Gebäude stehen auf dem höchsten Punkt unter ihrer Fläche, darunter wächst ein Sockel aus Bruchstein.",
            12, Grau, umbruchBreite: Rechtsbreite - 30);
        FilterAbschnitt(() => _stadt.GelaendeFilter, f => _stadt.GelaendeFilter = f);
    }

    /// <summary>Ein Raster mit zwei Spalten (Beschriftung, Feld) unten in der Tafel.</summary>
    private GridContainer ZweiSpalten()
    {
        var raster = new GridContainer { Columns = 2 };
        raster.AddThemeConstantOverride("h_separation", 10);
        raster.AddThemeConstantOverride("v_separation", 6);
        _eigenschaften.AddChild(raster);
        return raster;
    }

    /// <summary>Ein Haken mit Tooltip (Schriftgröße wie „Deko streuen“).</summary>
    private static CheckBox Haken(string text, bool an, Action<bool> umschalten, string tipp)
    {
        var haken = new CheckBox { Text = text, ButtonPressed = an, FocusMode = Control.FocusModeEnum.None, TooltipText = tipp };
        haken.AddThemeFontSizeOverride("font_size", 14);
        haken.Toggled += an => umschalten(an);
        return haken;
    }

    /// <summary>Die Zeile der Ziel- bzw. Bezugshöhe: ein Zahlenfeld (Meter, auf cm) und der Knopf „Pipette“ (der nächste Klick nimmt die Höhe auf).</summary>
    private void ZielhoeheZeile(string beschriftung, string tipp, Action? ein = null)
    {
        var raster = ZweiSpalten();
        var zeile = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        zeile.AddThemeConstantOverride("separation", 8);
        var feld = new Zahlfeld
        {
            MinValue = Hoehenraster.Tiefste, MaxValue = Hoehenraster.Hoechste, Step = 0.01, Value = _stadt.GelaendeZielhoehe, Suffix = "m",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, TooltipText = tipp, CustomArrowStep = 0.5,
        };
        feld.ValueChanged += w =>
        {
            _stadt.GelaendeZielhoehe = w;
            ein?.Invoke();
        };
        zeile.AddChild(feld);
        var pipette = new Button
        {
            Text = "Pipette", ToggleMode = true, ButtonPressed = _stadt.PipetteBereit, FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Der nächste Klick in die Karte nimmt die Höhe des Bodens dort auf, statt zu malen (oder gleich Strg+Klick)",
        };
        pipette.AddThemeFontSizeOverride("font_size", 13);
        pipette.Toggled += an => _stadt.PipetteBereit = an;
        zeile.AddChild(pipette);
        Zeile(raster, beschriftung, zeile);
    }

    /// <summary>
    /// Der Abschnitt „Filter“ (Pinsel-Paket 1) für Gelände- und Bodenpinsel: Steigung ab/bis mit drei Vorlagen, Bodentextur mit Rand. Die Vorlagen
    /// zeichnen die Tafel neu, damit die Regler den neuen Stand zeigen.
    /// </summary>
    private void FilterAbschnitt(Func<Pinselfilter> holen, Action<Pinselfilter> setzen)
    {
        var filter = holen();
        Hauptmenue.Text(_eigenschaften, "Filter", 15, Gold);
        var vorlagen = new HBoxContainer();
        vorlagen.AddThemeConstantOverride("separation", 6);
        _eigenschaften.AddChild(vorlagen);
        void Vorlage(string text, string tipp, Func<Pinselfilter, Pinselfilter> neu)
        {
            var knopf = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, TooltipText = tipp, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            knopf.AddThemeFontSizeOverride("font_size", 13);
            knopf.Pressed += () =>
            {
                setzen(neu(holen()));
                EigenschaftenZeigen(neu: true);
            };
            vorlagen.AddChild(knopf);
        }
        Vorlage("Überall", "Kein Filter: Der Pinsel wirkt im ganzen Kreis", _ => Pinselfilter.Keiner);
        Vorlage($"Ebene ≤ {Kartenpruefung.HangHinweis * 100:0} %", "Nur wo es flacher ist als der Hinweis der Hangsperre (8 %) – Wiese, Bauplätze",
            f => f with { SteigungAb = 0, SteigungBis = Kartenpruefung.HangHinweis * 100 });
        Vorlage($"Hang ≥ {Kartenpruefung.HangFehler * 100:0} %", "Nur wo es steiler ist als die Grenze der Hangsperre (15 %) – Fels an Böschungen",
            f => f with { SteigungAb = Kartenpruefung.HangFehler * 100, SteigungBis = null });
        var raster = ZweiSpalten();
        const double ohneGrenze = 100;
        Zeile(raster, "Steigung ab", Schieber(0, ohneGrenze, 1, filter.SteigungAb, a => $"{a:0} %", a => setzen(holen() with { SteigungAb = a }),
            "Unter dieser Steigung wirkt der Pinsel nicht (100 % = 45°); innen an der Grenze läuft er über 2 Prozentpunkte weich aus"));
        Zeile(raster, "Steigung bis", Schieber(0, ohneGrenze, 1, filter.SteigungBis ?? ohneGrenze, b => b >= ohneGrenze ? "ohne Grenze" : $"{b:0} %",
            b => setzen(holen() with { SteigungBis = b >= ohneGrenze ? null : b }), "Über dieser Steigung wirkt der Pinsel nicht; ganz rechts: ohne Grenze"));
        var wahl = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Nur auf dieser Bodentextur malen (gemessen am Boden vor dem Strich)" };
        wahl.AddItem("jeder Boden");
        var texturen = Enum.GetValues<Bodentextur>();
        foreach (var t in texturen) wahl.AddItem(_stadt.BodenName(t));
        wahl.Selected = filter.Boden is { } gewaehlt ? Array.IndexOf(texturen, gewaehlt) + 1 : 0;
        wahl.ItemSelected += i =>
        {
            setzen(holen() with { Boden = i == 0 ? null : texturen[i - 1] });
            EigenschaftenZeigen(neu: true);
        };
        Zeile(raster, "Nur auf", wahl);
        if (filter.Boden is not null)
            Zeile(raster, "Rand", Schieber(0, 1, 0.05, filter.BodenRand, r => $"{r * 100:0} %", r => setzen(holen() with { BodenRand = r }),
                "0 %: nur wo die Textur mindestens die Hälfte deckt; 100 %: auch wo nur ein Hauch davon liegt"));
        Hauptmenue.Text(_eigenschaften, filter.IstLeer ? "Der Pinsel wirkt überall in seinem Kreis." : "Der Pinsel wirkt nur, wo der Filter passt – das Schild an der Maus zeigt es.",
            12, Grau, umbruchBreite: Rechtsbreite - 30);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Boden: Texturpinsel und Radierer
    // ═════════════════════════════════════════════════════════════════════════

    private void BodenTafel()
    {
        EigenschaftenLeeren();
        _gezeigt = BodenMarke;
        var radierer = _stadt.BodenRadierer;
        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 8);
        _eigenschaften.AddChild(kopf);
        kopf.AddChild(new TextureRect { Texture = Farbfeld(radierer ? new Color(0.33f, 0.40f, 0.24f) : _stadt.BodenFarbe(_stadt.BodenArt)), StretchMode = TextureRect.StretchModeEnum.KeepCentered, MouseFilter = Control.MouseFilterEnum.Ignore });
        Hauptmenue.Text(kopf, radierer ? "Boden-Radierer" : _stadt.BodenName(_stadt.BodenArt), 17, Gold);
        Hauptmenue.Text(_eigenschaften, "Pinsel", 15, Gold);
        var raster = new GridContainer { Columns = 2 };
        raster.AddThemeConstantOverride("h_separation", 10);
        raster.AddThemeConstantOverride("v_separation", 6);
        _eigenschaften.AddChild(raster);
        Zeile(raster, "Radius", Schieber(1, 100, 1, _stadt.BodenRadius, Breite, r => _stadt.BodenRadius = r, "Halbmesser des Kreises an der Maus"));
        if (!radierer)
            Zeile(raster, "Stärke", Schieber(0.05, 1, 0.05, _stadt.BodenStaerke, s => $"{s * 100:0} %", s => _stadt.BodenStaerke = s,
                "100 % deckt ganz, weniger mischt die Textur nur leicht unter (mehrere Striche decken weiter)"));
        Zeile(raster, "Kante", Schieber(0, 1, 0.05, _stadt.BodenKante, k => $"{k * 100:0} %", k => _stadt.BodenKante = k, "0 % hart bis zum Rand, 100 % weich vom Mittelpunkt an"));
        if (radierer)
        {
            Hauptmenue.Text(_eigenschaften, "Malt die Wiese zurück und nimmt die Deko mit.", 12, Grau, umbruchBreite: Rechtsbreite - 30);
            FilterAbschnitt(() => _stadt.BodenFilter, f => _stadt.BodenFilter = f);
            return;
        }
        var deko = new CheckBox { Text = "Deko streuen", ButtonPressed = _stadt.BodenDeko, FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Streut passende kleine Teile (Gras, Steine, Farn, Schilf …) auf die gemalte Fläche – aus dem Boden gerechnet, nicht einzeln gespeichert" };
        deko.AddThemeFontSizeOverride("font_size", 14);
        deko.Toggled += an => _stadt.BodenDeko = an;
        _eigenschaften.AddChild(deko);
        var dichte = new GridContainer { Columns = 2 };
        dichte.AddThemeConstantOverride("h_separation", 10);
        _eigenschaften.AddChild(dichte);
        Zeile(dichte, "Dichte", Schieber(0, 2, 0.1, _stadt.BodenDichte, d => $"{Komma(d, "0.0")} ×", d => _stadt.BodenDichte = d, "Wie dicht die Deko steht: 1 normal, 2 doppelt, 0 gar nicht"));
        var daten = _entwurf.Katalog.TexturDaten(_stadt.BodenArt);
        var streut = daten is { Dichte: > 0 } && daten.Deko.Count > 0
            ? "Streut: " + string.Join(", ", daten.Deko.Where(e => e.Value > 0).Select(e => ArtName(e.Key))) + "."
            : "Diese Textur streut nichts.";
        Hauptmenue.Text(_eigenschaften, streut + "\nWege, Gebäude, Bauzonen und Wasser bleiben frei. Die Deko ist nicht gespeichert, sondern wird aus dem Boden gerechnet. "
            + "Strg+Klick nimmt die Bodenart unter der Maus auf.", 12, Grau, umbruchBreite: Rechtsbreite - 30);
        FilterAbschnitt(() => _stadt.BodenFilter, f => _stadt.BodenFilter = f);
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
