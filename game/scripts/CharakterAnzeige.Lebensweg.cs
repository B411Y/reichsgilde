using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Der Reiter „Lebensweg“ im Charakterfenster (Charaktersystem Teil 3a, Lebensweise): die fünf Pfade mit Erfahrungsbalken, Punkten und „bis zum nächsten Punkt“,
/// der gewählte Pfad hervorgehoben, darunter der Baum 3 × 3 in der <see cref="TalentbaumAnsicht"/> (ein Knoten kostet einen Punkt, nicht Gulden).
/// Bedienbar nur für Personen des eigenen Hauses (Oberhaupt, Erbe, Ehepartner); bei den Oberhäuptern anderer Häuser nur zum Ansehen.
/// Das Fenster liest den Kern nur: Pfad und Knoten gehen als <see cref="LebenswegWaehlen"/> und <see cref="KnotenWaehlen"/> über den Boten.
///
/// Zeichen der Pfade und Icons der 45 Knoten: assets/oberflaeche/lebensweise (<see cref="PfadZeichen"/>, <see cref="KnotenDatei"/>).
/// </summary>
public partial class CharakterAnzeige
{
    private bool _lebensweg;        // der Reiter „Lebensweg“ ist gewählt (statt Züge und Familie)
    private Talent? _pfadAnsicht;   // der Pfad, dessen Baum gezeigt wird (null: der aktive)

    /// <summary>Dateien der fünf Pfad-Zeichen (ohne Endung) in assets/oberflaeche/lebensweise.</summary>
    private static readonly Dictionary<Talent, string> PfadZeichen = new()
    {
        [Talent.Verhandeln] = "pfad_kaufmann", [Talent.Handwerk] = "pfad_meister", [Talent.NachtUndNebel] = "pfad_schatten",
        [Talent.Kampf] = "pfad_schwert", [Talent.Rhetorik] = "pfad_redner",
    };

    private static readonly Dictionary<string, Texture2D?> BilderSpeicher = new();

    /// <summary>
    /// Bild aus assets/oberflaeche/lebensweise (128 px, rund; mit Mipmaps eingelesen, damit es auch bei 38 oder 64 px scharf bleibt) oder null, wenn es die Datei nicht gibt.
    /// </summary>
    private static Texture2D? LebensweiseBild(string datei)
    {
        if (BilderSpeicher.TryGetValue(datei, out var bild)) return bild;
        var pfad = "res://assets/oberflaeche/lebensweise/" + datei + ".png";
        bild = ResourceLoader.Exists(pfad) ? GD.Load<Texture2D>(pfad) : null;
        BilderSpeicher[datei] = bild;
        return bild;
    }

    /// <summary>Knoten-Kennung („HueterDesHauses“) → Dateiname („hueter_des_hauses“).</summary>
    internal static string KnotenDatei(string kennung)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < kennung.Length; i++)
        {
            if (i > 0 && char.IsUpper(kennung[i])) sb.Append('_');
            sb.Append(char.ToLowerInvariant(kennung[i]));
        }
        return sb.ToString();
    }

    /// <summary>Die Reihen des Baums („Anfang“, „Mitte“, „Gipfel“): Eine Gebäudestufe ist es hier nicht.</summary>
    private static readonly string[] ReihenNamen = { "Anfang", "Mitte", "Gipfel" };

    /// <summary>Lesbare Namen der Taten, die Erfahrung bringen (Schlüssel wie in <c>lebensweise.pfade.*.taten</c>).</summary>
    private static readonly Dictionary<string, string> TatNamen = new()
    {
        ["KreditAufnehmen"] = "Kredit aufnehmen", ["Schenken"] = "Gulden schenken", ["Aktivitaet:Geschenk"] = "Geschenk machen", ["BerufErlernen"] = "Beruf erlernen",
        ["ArbeiterBefoerdern"] = "Arbeiter befördern", ["MeisterpruefungAblegen"] = "Meisterprüfung ablegen",
        ["Beauftragen"] = "Hinterzimmer-Auftrag", ["SpitzelAnsetzen"] = "Spitzel ansetzen", ["Erpressen"] = "Erpressen", ["Einbrechen"] = "Einbrechen",
        ["Auskundschaften"] = "Auskundschaften",
        ["Aktivitaet:Jagd"] = "Jagd", ["Razzia"] = "Razzia", ["Beauftragen:Ueberfall"] = "Überfall", ["Entfuehren"] = "Entführen",
        ["AmtBewerben"] = "Um ein Amt bewerben", ["StatutBeantragen"] = "Statut beantragen", ["SteuersatzBeantragen"] = "Steuer beantragen",
        ["BauvorhabenBeantragen"] = "Bauvorhaben beantragen", ["FestBeantragen"] = "Fest beantragen", ["StadtglaubeBeantragen"] = "Stadtglauben beantragen",
        ["WegPflasterBeantragen"] = "Wege pflastern beantragen", ["Anklagen"] = "Anklagen", ["Aktivitaet:Gastmahl"] = "Gastmahl", ["Aktivitaet:Zechen"] = "Zechen",
        ["Aktivitaet:Messe"] = "Messe", ["Aktivitaet:Brettspiel"] = "Brettspiel", ["Bekehrungspredigt"] = "Bekehrungspredigt", ["Werben"] = "Werben",
    };

    private static Texture2D? PfadBild(Talent pfad) => PfadZeichen.TryGetValue(pfad, out var name) ? LebensweiseBild(name) : null;

    /// <summary>Wie viele Punkte der Lebensweise dieser Person noch auf einen Knoten warten (0 ohne Lebensweg).</summary>
    private static int FreiePunkte(Familienbuch buch, Person p) =>
        buch.HatLebensweg(p) ? TalentTexte.Alle.Sum(t => buch.FreiePunkte(p, t)) : 0;

    /// <summary>Öffnet das Fenster dieser Person gleich im Reiter „Lebensweg“ (vom Zeichen in der Kopfzeile).</summary>
    public void LebensweiseOeffnen(Person person)
    {
        Zeigen(person);
        _lebensweg = true;
        _pfadAnsicht = null;
        Aufbauen();
    }

    /// <summary>Der schmale Kopf im Reiter „Lebensweg“: kleines Porträt, Name, Volk, Alter und Rolle.</summary>
    private void KompakterKopf(Familienbuch buch, Person p, Familie? familie)
    {
        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 14);
        _inhalt.AddChild(kopf);
        var bild = PortraetAnzeige.Neu(PortraetAnzeige.Klein);
        bild.Zeigen(buch, p);
        bild.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        kopf.AddChild(bild);
        var daten = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        daten.AddThemeConstantOverride("separation", 2);
        kopf.AddChild(daten);
        Hauptmenue.Text(daten, p.NameMitHerkunft, 22, Gold, umbruchBreite: Breite - PortraetAnzeige.Klein - 70);
        var rolle = familie is null ? "" : familie.Oberhaupt == p ? p.OberhauptTitel : familie.Ehepartner == p ? p.Ehepartnertitel : p.SohnOderTochter;
        if (familie is not null) rolle += $" im Haus {familie.Nachname}";
        Hauptmenue.Text(daten, $"{p.Volk.Einzahl()} · {p.Alter(buch.Jahr)} Jahre · {rolle}", 14, Grau, umbruchBreite: Breite - PortraetAnzeige.Klein - 70);
    }

    /// <summary>Die Reiterleiste „Person“ und „Lebensweg“ (der zweite zeigt freie Punkte in Gold).</summary>
    private void ReiterBauen(Familienbuch buch, Person p)
    {
        var leiste = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        leiste.AddThemeConstantOverride("separation", 6);
        _inhalt.AddChild(leiste);
        var gruppe = new ButtonGroup();
        var frei = FreiePunkte(buch, p);
        foreach (var (name, lebensweg) in new[] { ("Züge und Familie", false), ("Lebensweg", true) })
        {
            var text = lebensweg && frei > 0 ? $"{name} ({frei} frei)" : name;
            var knopf = new Button { Text = text, ToggleMode = true, ButtonGroup = gruppe, FocusMode = FocusModeEnum.None, ButtonPressed = _lebensweg == lebensweg, CustomMinimumSize = new Vector2(170, 0) };
            knopf.AddThemeFontSizeOverride("font_size", 17);
            knopf.AddThemeColorOverride("font_pressed_color", Gold);
            knopf.AddThemeColorOverride("font_hover_pressed_color", Gold);
            if (lebensweg && frei > 0) knopf.AddThemeColorOverride("font_color", Gold);
            if (lebensweg)
                knopf.TooltipText = "Die Lebensweise: Erfahrung in einem von fünf Pfaden gibt Punkte, mit denen Ihr Knoten wählt. Mehr zur [[lebensweise|Lebensweise]].";
            knopf.Pressed += () => { if (_lebensweg == lebensweg) return; _lebensweg = lebensweg; Aufbauen(); };
            leiste.AddChild(knopf);
        }
    }

    // ── Der Reiter ──────────────────────────────────────────────────────────

    private void LebenswegBauen(Stadtwirtschaft w, Person p)
    {
        var buch = w.Familien;
        if (buch.LebensweiseDaten is not { } daten) return;
        var familie = buch.FamilieVon(p);
        var eigen = familie is not null && _spieler is not null && familie.Konto == _spieler;
        var pfade = buch.Pfade(p);
        var aktiv = buch.AktiverPfad(p);
        var ansicht = _pfadAnsicht is { } gewaehlt && daten.Pfade.ContainsKey(gewaehlt) ? gewaehlt : aktiv;
        var breite = Breite - 50;
        var ich = p.Geschlecht == Geschlecht.Frau ? "sie" : "er";

        Hauptmenue.Text(_inhalt, $"Erfahrung fließt in den Pfad, dem {p.Vorname} gerade folgt; je 100 Erfahrung gibt einen Punkt für einen Knoten. Den Pfad zu wechseln kostet nichts.",
            13, Grau, umbruchBreite: breite);
        RollenHinweise(buch, familie, p, daten, pfade, breite);
        if (_meldung != "") Hauptmenue.Text(_inhalt, _meldung, 14, _meldungGut ? Gruen : Rot, umbruchBreite: breite);

        foreach (var z in pfade) PfadKarte(buch, p, z, ansicht, buch.ErfahrungJeJahr(p));

        // Der gewählte Pfad: Beschreibung, was ihn nährt, Wechsel und der Baum
        var pd = daten.Pfade[ansicht];
        var zeile = pfade.First(z => z.Pfad == ansicht);
        _inhalt.AddChild(new HSeparator());
        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 12);
        _inhalt.AddChild(kopf);
        kopf.AddChild(PfadBildFeld(ansicht, 48));
        var kopfText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        kopfText.AddThemeConstantOverride("separation", 2);
        kopf.AddChild(kopfText);
        Hauptmenue.Text(kopfText, pd.Name, 20, Gold);
        Hauptmenue.Text(kopfText, pd.Beschreibung, 13, Grau, umbruchBreite: breite - 70);

        string Zuege(IEnumerable<string> kennungen) => string.Join(", ", kennungen.Select(k => buch.Zug(k)?.Name ?? k));
        var nahrung = $"Passende Züge lernen schneller (× {Zahlenformat.Kommazahl(daten.ZugPassend)}): {Zuege(pd.Passend)}. " +
                      $"Hemmend (× {Zahlenformat.Kommazahl(daten.ZugHemmend)}): {Zuege(pd.Hemmend)}.";
        var taten = string.Join(", ", pd.Taten.Select(t => TatNamen.GetValueOrDefault(t, t)));
        var info = Hauptmenue.Text(_inhalt, nahrung, 13, null, umbruchBreite: breite);
        info.MouseFilter = MouseFilterEnum.Stop;
        info.TooltipText = $"Was Erfahrung bringt: am Jahresende {Zahlenformat.Ganz(daten.Grund)} und {Zahlenformat.Ganz(daten.JeStern)} je Stern im Talent {ansicht.Name()}; " +
                           $"dazu sofort {Zahlenformat.Ganz(daten.JeAp)} je Aktionspunkt für eine Tat dieses Pfads (nur beim Oberhaupt): {taten}. Üben des Talents {ansicht.Name()} zählt auch.";

        if (eigen)
        {
            var knopfZeile = new HBoxContainer();
            knopfZeile.AddThemeConstantOverride("separation", 12);
            _inhalt.AddChild(knopfZeile);
            if (ansicht == aktiv)
                Hauptmenue.Text(knopfZeile, $"{p.Vorname} folgt diesem Pfad.", 14, Gruen);
            else
            {
                var folgen = Hauptmenue.Knopf(knopfZeile, "Diesem Pfad folgen", () => PfadWaehlen(p, ansicht));
                folgen.TooltipText = $"Ab jetzt fließt neue Erfahrung in den {pd.Name}. Was {p.Vorname} in den anderen Pfaden gesammelt hat und die gelernten Knoten bleiben; " +
                                     "der Wechsel kostet nichts und keine Aktionspunkte.";
                Hauptmenue.Text(knopfZeile, "Gesammelte Erfahrung und gelernte Knoten bleiben.", 12, Grau);
            }
        }
        else Hauptmenue.Text(_inhalt, $"{p.Vorname} gehört nicht zu Eurem Haus – Ihr seht nur, was {ich} gelernt hat.", 13, Grau, umbruchBreite: breite);

        var baum = buch.Lebensbaum(p, ansicht, _spieler);
        if (baum is null) return;
        var texte = new TalentbaumAnsicht.BaumTexte(
            s => s.Zustand == Baumzustand.Gekauft ? "gelernt" : "1 Punkt",
            s => KnotenTipp(baum, s, zeile),
            s => LebensweiseBild(KnotenDatei(s.Kennung)) ?? PfadBild(ansicht)); // fehlt ein Knoten-Bild, steht das Zeichen des Pfads
        _inhalt.AddChild(TalentbaumAnsicht.Bauen(baum, "Punkt", r => ReihenNamen[Math.Clamp(r - 1, 0, ReihenNamen.Length - 1)],
            s => KnotenLernen(p, s), texte));
        if (zeile.Frei > 0 && eigen)
            Hauptmenue.Text(_inhalt, $"{zeile.Frei} freie{(zeile.Frei == 1 ? "r" : "")} Punkt{(zeile.Frei == 1 ? "" : "e")} – ein Klick auf einen goldenen Knoten lernt ihn, für immer.", 13, Gold, umbruchBreite: breite);
    }

    /// <summary>Wer sammelt wie viel, Vermächtnis und Wirkung für das Haus – je nach Rolle der Person.</summary>
    private void RollenHinweise(Familienbuch buch, Familie? familie, Person p, LebensweiseDaten daten, IReadOnlyList<PfadZeile> pfade, int breite)
    {
        if (familie is null) return;
        var anteil = buch.LebenswegAnteil(p);
        var ich = p.Geschlecht == Geschlecht.Frau ? "sie" : "er";
        if (familie.Oberhaupt != p)
        {
            var rolle = familie.Ehepartner == p ? p.Ehepartnertitel : p.Geschlecht == Geschlecht.Frau ? "Erbin" : "Erbe";
            Hauptmenue.Text(_inhalt, $"{p.Vorname} ist {rolle} und sammelt {Zahlenformat.Ganz(anteil * 100)} % der Erfahrung des Oberhaupts. " +
                                     $"Die Knoten wirken für das ganze Haus erst, wenn {ich} das Haus führt.",
                13, Grau, umbruchBreite: breite);
        }
        if (buch.Erbe(familie) == p && familie.Oberhaupt != p && daten.Vermaechtnis > 0)
        {
            var o = familie.Oberhaupt;
            var opfad = buch.AktiverPfad(o);
            var wert = o.Lebensweg.Erfahrung(opfad) * daten.Vermaechtnis;
            var hinweis = Hauptmenue.Text(_inhalt,
                $"Vermächtnis: Wird {p.Vorname} Oberhaupt, bekommt {ich} {Zahlenformat.Ganz(daten.Vermaechtnis * 100)} % der Erfahrung von {o.Vorname} im {daten.Pfade[opfad].Name} dazu " +
                $"(jetzt etwa {Zahlenformat.Ganz(wert)}). Die Knoten von {o.Vorname} gehen nicht über.", 13, Gold, umbruchBreite: breite);
            hinweis.MouseFilter = MouseFilterEnum.Stop;
            hinweis.TooltipText = "Beim Erbfall behält der Erbe seine eigenen Knoten und erbt einen Teil der Erfahrung des Verstorbenen in dessen Pfad. Mehr zur [[lebensweise|Lebensweise]].";
        }
        else if (familie.Oberhaupt == p && daten.Vermaechtnis > 0 && buch.Erbe(familie) is { } erbe)
            Hauptmenue.Text(_inhalt, $"Stirbt {p.Vorname}, erbt {erbe.Vorname} {Zahlenformat.Ganz(daten.Vermaechtnis * 100)} % der Erfahrung im aktiven Pfad (Vermächtnis); die Knoten gehen nicht über.", 13, Grau, umbruchBreite: breite);
    }

    /// <summary>Eine Karte je Pfad: Zeichen, Name, Balken bis zum nächsten Punkt, Punkte. Aktiv: goldener Rahmen; angesehen: heller Grund. Ein Klick zeigt den Baum dieses Pfads.</summary>
    private void PfadKarte(Familienbuch buch, Person p, PfadZeile z, Talent ansicht, double jeJahr)
    {
        var angesehen = z.Pfad == ansicht;
        var karte = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand };
        karte.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = angesehen ? new Color(0.22f, 0.19f, 0.13f) : new Color(0.16f, 0.14f, 0.11f),
            BorderColor = z.Aktiv ? Gold : angesehen ? Gold * new Color(1, 1, 1, 0.4f) : new Color(0.35f, 0.30f, 0.22f),
            BorderWidthLeft = z.Aktiv ? 3 : 1, BorderWidthTop = z.Aktiv ? 3 : 1, BorderWidthRight = z.Aktiv ? 3 : 1, BorderWidthBottom = z.Aktiv ? 3 : 1,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 8, ContentMarginRight = 10, ContentMarginTop = 4, ContentMarginBottom = 4,
        });
        karte.GuiInput += eingabe =>
        {
            if (eingabe is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && _pfadAnsicht != z.Pfad)
            {
                _pfadAnsicht = z.Pfad;
                karte.AcceptEvent(); // sonst reicht der Klick an die Welt durch, sobald Aufbauen() die Karte freigibt (öffnete dahinter liegende Gebäude)
                Aufbauen();
            }
        };
        _inhalt.AddChild(karte);
        var reihe = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        reihe.AddThemeConstantOverride("separation", 12);
        karte.AddChild(reihe);
        reihe.AddChild(PfadBildFeld(z.Pfad, 38));

        var mitte = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        mitte.AddThemeConstantOverride("separation", 2);
        reihe.AddChild(mitte);
        var kopf = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        kopf.AddThemeConstantOverride("separation", 10);
        mitte.AddChild(kopf);
        Hauptmenue.Text(kopf, z.Name, 16, Gold).MouseFilter = MouseFilterEnum.Ignore;
        if (z.Aktiv) Hauptmenue.Text(kopf, $"{p.Vorname} folgt diesem Pfad", 12, Gruen).MouseFilter = MouseFilterEnum.Ignore;

        var balken = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        balken.AddThemeConstantOverride("separation", 10);
        mitte.AddChild(balken);
        var anteil = z.ErfahrungNaechsterPunkt is { } kosten && z.BisNaechsterPunkt is { } rest ? Math.Clamp((kosten - rest) / kosten, 0, 1) : 1;
        balken.AddChild(new ErfahrungsBalken(anteil, z.Frei > 0));
        string bis;
        if (z.BisNaechsterPunkt is not { } fehlt) bis = "Der Baum ist voll.";
        else
        {
            bis = $"noch {Zahlenformat.Ganz(Math.Ceiling(fehlt))} Erfahrung";
            if (z.Aktiv && jeJahr > 0) bis += $", etwa {Zahlenformat.Ganz(Math.Max(1, Math.Ceiling(fehlt / jeJahr - 1e-9)))} Jahr{(Math.Ceiling(fehlt / jeJahr - 1e-9) <= 1 ? "" : "e")}";
            bis += " bis zum nächsten Punkt";
        }
        Hauptmenue.Text(balken, bis, 12, Grau).MouseFilter = MouseFilterEnum.Ignore;

        var rechts = new VBoxContainer { CustomMinimumSize = new Vector2(120, 0), MouseFilter = MouseFilterEnum.Ignore };
        reihe.AddChild(rechts);
        var punkte = Hauptmenue.Text(rechts, $"{z.Knoten} Knoten gelernt", 13, null);
        punkte.HorizontalAlignment = HorizontalAlignment.Right;
        punkte.MouseFilter = MouseFilterEnum.Ignore;
        var freiText = z.Frei > 0 ? $"{z.Frei} frei" : "";
        var frei = Hauptmenue.Text(rechts, freiText, 14, Gold);
        frei.HorizontalAlignment = HorizontalAlignment.Right;
        frei.MouseFilter = MouseFilterEnum.Ignore;

        karte.TooltipText = $"{z.Name}: {Zahlenformat.Ganz(z.Erfahrung)} Erfahrung gesammelt, {z.Punkte} Punkte, davon {z.Knoten} als Knoten gelernt" +
                            (z.Frei > 0 ? $" und {z.Frei} frei" : "") + ".\n" +
                            (z.Aktiv ? "Hier fließt neue Erfahrung hin.\n" : "Hier fließt gerade keine neue Erfahrung hin; Gesammeltes bleibt.\n") +
                            "Ein Klick zeigt den Baum dieses Pfads.";
    }

    private static Control PfadBildFeld(Talent pfad, int seite)
    {
        var rahmen = new PanelContainer { CustomMinimumSize = new Vector2(seite, seite), MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        rahmen.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.09f, 0.07f), BorderColor = new Color(0.45f, 0.38f, 0.25f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
        });
        if (PfadBild(pfad) is { } bild)
            rahmen.AddChild(new TextureRect
            {
                Texture = bild, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps, MouseFilter = MouseFilterEnum.Ignore,
            });
        return rahmen;
    }

    private static string KnotenTipp(Einrichtungsbaum baum, BaumStueck s, PfadZeile pfad)
    {
        var zeilen = new List<string> { s.Name, s.Wirkung, $"Kostet: 1 Punkt ({pfad.Name})", $"Reihe {s.Reihe}: {ReihenNamen[Math.Clamp(s.Reihe - 1, 0, ReihenNamen.Length - 1)]}" };
        if (s.Voraussetzung is { } v) zeilen.Add("Braucht: " + (baum.Stuecke.FirstOrDefault(x => x.Kennung == v)?.Name ?? v));
        zeilen.Add(s.Zustand == Baumzustand.Gekauft ? "Schon gelernt."
            : s.Verboten is { } grund ? (s.Zustand == Baumzustand.Gesperrt ? "Noch gesperrt: " : "Gerade nicht möglich: ") + grund
            : "Ein Klick lernt es, für immer.");
        zeilen.Add("Mehr zur [[lebensweise|Lebensweise]].");
        return string.Join("\n", zeilen);
    }

    // ── Befehle ─────────────────────────────────────────────────────────────

    private void PfadWaehlen(Person p, Talent pfad) =>
        UiHilfe.Befehlen(_simulation, _spieler, new LebenswegWaehlen(p.Nummer, pfad), this, Ergebnis);

    private void KnotenLernen(Person p, BaumStueck s)
    {
        if (s.Zustand == Baumzustand.Gekauft) return;
        UiHilfe.Befehlen(_simulation, _spieler, new Reichsgilde.Core.Wirtschaft.KnotenWaehlen(p.Nummer, s.Kennung), this, Ergebnis);
    }

    private void Ergebnis(Befehlsergebnis e)
    {
        _meldung = UiHilfe.MeldungText(e);
        _meldungGut = e.Erfolg;
        Gemeldet?.Invoke(_meldung);
        Aufbauen();
    }

    /// <summary>Der Balken bis zum nächsten Punkt: golden gefüllt; voll und grün schimmernd, wenn schon ein Punkt wartet.</summary>
    private sealed partial class ErfahrungsBalken : Control
    {
        private const int B = 200, H = 12;
        private readonly double _anteil;
        private readonly bool _frei;

        public ErfahrungsBalken(double anteil, bool frei)
        {
            _anteil = anteil;
            _frei = frei;
            CustomMinimumSize = new Vector2(B, H);
            SizeFlagsVertical = SizeFlags.ShrinkCenter;
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Draw()
        {
            DrawRect(new Rect2(0, 0, B, H), new Color(0.10f, 0.09f, 0.07f));
            var laenge = (float)(_anteil * (B - 2));
            if (laenge > 0.5f) DrawRect(new Rect2(1, 1, laenge, H - 2), _frei ? new Color(0.95f, 0.78f, 0.30f) : new Color(0.72f, 0.58f, 0.24f));
            // Schraffur im Balken wie im Holzschnitt: feine schräge Striche über dem gefüllten Teil
            for (var x = 4f; x < laenge; x += 5)
                DrawLine(new Vector2(x, H - 1), new Vector2(Math.Min(x + 4, laenge + 1), 1), new Color(0.2f, 0.14f, 0.05f, 0.35f), 1f);
            DrawRect(new Rect2(0, 0, B, H), new Color(0.05f, 0.04f, 0.03f), false, 1.5f);
        }
    }
}
