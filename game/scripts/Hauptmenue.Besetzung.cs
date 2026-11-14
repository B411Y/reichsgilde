using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// „Neue Partie“, zweiter Teil (Entscheidung 11.11.2026): der gewürfelte Vorschlag für den Spieler, seine drei Persönlichkeitszüge, die KI-Gegner
/// und der aufklappbare Bereich „Erweitert“ (jede KI einzeln, KI-Häuser mit Umland und die übrigen Einstellungen). Gewürfelt wird nur vom Kern
/// (<see cref="Partiebesetzung"/>); die Oberfläche zeigt und reicht weiter.
/// </summary>
public partial class Hauptmenue
{
    private HFlowContainer _zuegeReihe = null!;
    private Button _zuegeNeu = null!;
    private Control _zuegeZeile = null!;
    private Label _kiStaerkeHinweis = null!;
    private Button _erweitertKnopf = null!;
    private VBoxContainer _erweitert = null!;
    private VBoxContainer _kiListe = null!;
    private Label _kiListeKopf = null!;
    private readonly List<KiZeile> _kiZeilen = new();

    /// <summary>Welcher Wurf der Persönlichkeitszüge gerade gezeigt wird (0, 1, 2, … – „Neu würfeln“ zählt hoch).</summary>
    private int _zugWurf;

    private sealed class KiZeile
    {
        public required VBoxContainer Wurzel { get; init; }
        public required LineEdit Name { get; init; }
        public required OptionButton Wappen { get; init; }
        public required OptionButton Volk { get; init; }
        public required OptionButton Glaube { get; init; }
        public required OptionButton Beruf { get; init; }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Öffnen und Vorschlag
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Richtet die Seite auf die gewählte Karte ein: höchstens so viele KI-Gegner wie Höfe minus eins, die Hofstellen, die Liste für „Erweitert“, und
    /// einen frischen gewürfelten Vorschlag (neuer Zufallsstartwert, Züge ab Wurf 0).
    /// </summary>
    private void NeuePartieOeffnen()
    {
        _fehler.Text = "";
        _zugWurf = 0;
        _startwert.Value = GD.RandRange(0, 999_999);
        var hoechstens = Partiebesetzung.HoechstensKi(Stadtkarten.Startkarte);
        _kiAnzahl.MaxValue = hoechstens;
        _kiAnzahl.Value = Math.Min(3, hoechstens);
        HofstellenWahlFuellen();
        BerufFuellen(_beruf, false);
        KiZeilenBauen(hoechstens);
        VorschlagAnwenden();
    }

    /// <summary>Der gewürfelte Vorschlag des Kerns für Name, Wappen, Volk, Glaube und Beruf des Spielers – steht danach in den Feldern und ist frei änderbar.</summary>
    private void VorschlagAnwenden()
    {
        if (DatenLesekopie() is { } daten)
        {
            try
            {
                var b = Partiebesetzung.SpielerWuerfeln(daten, Stadtkarten.Startkarte, (int)_startwert.Value);
                _familie.Text = b.Familienname;
                _wappen = b.Wappen;
                for (var i = 0; i < _vorlagenKnoepfe.Count; i++)
                    _vorlagenKnoepfe[i].SetPressedNoSignal(Wappen.Vorlagen[i] == _wappen);
                WappenZeigen();
                _volk.Select(Math.Max(0, _volk.GetItemIndex((int)b.Volk)));
                GlaubeFuellen(b.Glaube);
                BerufWaehlen(_beruf, b.Beruf);
            }
            catch (ArgumentException fehler)
            {
                GD.PushWarning($"[neue partie] Kein Vorschlag: {fehler.Message}");
            }
        }
        VolkHinweisZeigen();
        PaletteFuellen();
        AussehenZeigen();
        ZuegeZeigen();
    }

    private static Wirtschaftsdaten? DatenLesekopie() => Datenvorrat.Lesekopie();

    /// <summary>Glaubensnamen wie in der Oberfläche überall (klein geschrieben).</summary>
    private static string GlaubeName(Konfession k) => k switch
    {
        Konfession.Katholisch => "katholisch",
        Konfession.Evangelisch => "evangelisch",
        Konfession.Naturglaube => "Naturglaube",
        _ => k.ToString(),
    };

    /// <summary>Die Glauben, mit denen das gewählte Volk beginnen kann; die bisherige (oder die übergebene) Wahl bleibt, wenn sie dazu passt.</summary>
    private void GlaubeFuellen(Konfession? wahl = null)
    {
        var bisher = wahl is { } w ? (int)w : _glaube.ItemCount > 0 ? _glaube.GetSelectedId() : -1;
        _glaube.Clear();
        foreach (var g in Startaufstellung.Startglauben((Volk)_volk.GetSelectedId()))
            _glaube.AddItem(GlaubeName(g), (int)g);
        _glaube.Select(Math.Max(0, _glaube.GetItemIndex(bisher)));
    }

    /// <summary>Alle Berufe, mit denen ein Haus beginnen kann, aus den Daten (<see cref="Startaufstellung.FreieBerufe"/>), mit der Werkstatt in Klammern; „zufällig“ davor für die KI.</summary>
    private static void BerufFuellen(OptionButton wahl, bool mitZufall)
    {
        var bisher = GewaehlterBeruf(wahl);
        wahl.Clear();
        if (mitZufall)
        {
            wahl.AddItem("zufällig");
            wahl.SetItemMetadata(0, "");
        }
        if (DatenLesekopie() is { } daten)
            foreach (var b in Startaufstellung.FreieBerufe(daten, Array.Empty<string>()))
            {
                wahl.AddItem($"{b} ({daten.Art(daten.Berufe[b].Werkstatt).Name})");
                wahl.SetItemMetadata(wahl.ItemCount - 1, b);
            }
        BerufWaehlen(wahl, bisher);
    }

    private static string? GewaehlterBeruf(OptionButton wahl)
    {
        if (wahl.ItemCount == 0 || wahl.Selected < 0) return null;
        var name = wahl.GetItemMetadata(wahl.Selected).AsString();
        return name == "" ? null : name;
    }

    private static void BerufWaehlen(OptionButton wahl, string? beruf)
    {
        for (var i = 0; i < wahl.ItemCount; i++)
            if (wahl.GetItemMetadata(i).AsString() == (beruf ?? ""))
            {
                wahl.Select(i);
                return;
            }
        if (wahl.ItemCount > 0) wahl.Select(0);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Persönlichkeitszüge
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Zeigt die drei Persönlichkeitszüge des Oberhaupts (Kern: Startwert, Volk und Wurf bestimmen sie); ohne Charakterzüge in den Daten verschwindet die Zeile.</summary>
    private void ZuegeZeigen()
    {
        UiHilfe.KinderEntfernen(_zuegeReihe);
        var zuege = DatenLesekopie() is { } daten
            ? Partiebesetzung.SpielerZuege(daten, (int)_startwert.Value, (Volk)_volk.GetSelectedId(), _zugWurf)
            : Array.Empty<Charakterzug>();
        ZeileZeigen(_zuegeZeile, zuege.Count > 0);
        foreach (var z in zuege)
        {
            var tipp = $"{z.Name} ({z.Artname})";
            if (z.Beschreibung != "") tipp += "\n" + z.Beschreibung;
            if (z.Wirkungstext != "") tipp += "\nWirkung: " + z.Wirkungstext;
            _zuegeReihe.AddChild(new CharakterAnzeige.ZugIcon(z, tipp));
        }
    }

    /// <summary>„Neu würfeln“: der Kern zählt den Wurf hoch (<see cref="ZuegeNeuWuerfeln"/>), die Oberfläche zeigt die neuen Züge.</summary>
    private void ZuegeWuerfelnKlick()
    {
        var neu = Partiebesetzung.Ausfuehren(new NeuePartie(ZugWurf: _zugWurf), new ZuegeNeuWuerfeln());
        _zugWurf = neu.ZugWurf ?? 0;
        ZuegeZeigen();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  KI-Gegner
    // ═════════════════════════════════════════════════════════════════════════

    private void KiStaerkeHinweisZeigen() =>
        _kiStaerkeHinweis.Text = (KiStaerke)_kiStaerke.GetSelectedId() switch
        {
            KiStaerke.Leicht => "Die KI-Häuser starten mit weniger Gulden und wirtschaften vorsichtiger.",
            KiStaerke.Schwer => "Die KI-Häuser starten mit mehr Gulden und wirtschaften schneller.",
            _ => "Die KI-Häuser starten und wirtschaften wie bisher.",
        };

    /// <summary>Eine Zeile je möglichem KI-Haus für „Erweitert“; gezeigt werden so viele, wie KI-Gegner gewählt sind.</summary>
    private void KiZeilenBauen(int anzahl)
    {
        UiHilfe.KinderEntfernen(_kiListe);
        _kiZeilen.Clear();
        for (var i = 0; i < anzahl; i++)
        {
            var wurzel = new VBoxContainer();
            wurzel.AddThemeConstantOverride("separation", 4);
            Text(wurzel, $"KI-Haus {i + 1}", 14, Gold);

            var oben = new HBoxContainer();
            oben.AddThemeConstantOverride("separation", 6);
            oben.AddChild(new Label { Text = "Haus" });
            var name = new LineEdit { PlaceholderText = "zufällig", CustomMinimumSize = new Vector2(150, 0) };
            oben.AddChild(name);
            oben.AddChild(new Label { Text = "Wappen" });
            // FitToLongestItem aus: sonst bestimmt die längste Wappenbeschreibung die Mindestbreite (trotz ClipText) und „Erweitert“ machte die Tafel breiter.
            var wappen = new OptionButton { FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true, FitToLongestItem = false, CustomMinimumSize = new Vector2(120, 0) };
            wappen.AddItem("zufällig", -1);
            for (var w = 0; w < Wappen.Vorlagen.Count; w++)
                wappen.AddItem(Wappen.Vorlagen[w].Beschreibung(), w);
            oben.AddChild(wappen);
            wurzel.AddChild(oben);

            var unten = new HBoxContainer();
            unten.AddThemeConstantOverride("separation", 6);
            var volk = new OptionButton { FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true, CustomMinimumSize = new Vector2(120, 0) };
            volk.AddItem("Volk: zufällig", -1);
            foreach (var v in Startaufstellung.KiVoelker) volk.AddItem(v.Mehrzahl(), (int)v);
            var glaube = new OptionButton { FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true, CustomMinimumSize = new Vector2(120, 0) };
            glaube.AddItem("Glaube: zufällig", -1);
            foreach (var g in new[] { Konfession.Katholisch, Konfession.Evangelisch }) glaube.AddItem(GlaubeName(g), (int)g);
            var beruf = new OptionButton { FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true, CustomMinimumSize = new Vector2(120, 0) };
            BerufFuellen(beruf, true);
            unten.AddChild(volk);
            unten.AddChild(glaube);
            unten.AddChild(beruf);
            wurzel.AddChild(unten);

            _kiListe.AddChild(wurzel);
            _kiZeilen.Add(new KiZeile { Wurzel = wurzel, Name = name, Wappen = wappen, Volk = volk, Glaube = glaube, Beruf = beruf });
        }
        KiZeilenZeigen();
    }

    /// <summary>Nur so viele Zeilen zeigen, wie KI-Gegner gewählt sind.</summary>
    private void KiZeilenZeigen()
    {
        var n = (int)_kiAnzahl.Value;
        for (var i = 0; i < _kiZeilen.Count; i++) _kiZeilen[i].Wurzel.Visible = i < n;
        _kiListeKopf.Visible = n > 0;
    }

    /// <summary>Die einzeln festgelegten KI-Häuser (je Feld null = gewürfelt); null, wenn nichts festgelegt ist.</summary>
    private IReadOnlyList<Hausvorgabe?>? KiVorgabenLesen()
    {
        var liste = new List<Hausvorgabe?>();
        for (var i = 0; i < (int)_kiAnzahl.Value && i < _kiZeilen.Count; i++)
        {
            var z = _kiZeilen[i];
            var name = z.Name.Text.Trim();
            var v = new Hausvorgabe(
                Name: name == "" ? null : name,
                Wappen: z.Wappen.GetSelectedId() < 0 ? null : Wappen.Vorlagen[z.Wappen.GetSelectedId()],
                Volk: z.Volk.GetSelectedId() < 0 ? null : (Volk)z.Volk.GetSelectedId(),
                Glaube: z.Glaube.GetSelectedId() < 0 ? null : (Konfession)z.Glaube.GetSelectedId(),
                Beruf: GewaehlterBeruf(z.Beruf));
            liste.Add(v == new Hausvorgabe(null, null, null, null, null) ? null : v);
        }
        return liste.All(v => v is null) ? null : liste;
    }

    /// <summary>
    /// Vorab-Prüfung der Besetzung, damit der Fehler hier steht und nicht erst beim Aufbau der Stadt: Namen, Wappen und Berufe dürfen nicht doppelt
    /// vorkommen (der Spieler zählt mit), und was der Spieler festlegt, muss der Kern würfeln können (Glaube zum Volk, Beruf frei).
    /// </summary>
    private string? BesetzungProblem(int startwert, IReadOnlyList<Hausvorgabe?>? vorgaben)
    {
        var fehler = new List<string>();
        var kis = (vorgaben ?? Array.Empty<Hausvorgabe?>()).OfType<Hausvorgabe>().ToList();
        var namen = kis.Select(v => v.Name).OfType<string>().Select(Partieaufbau.Hausname).Prepend(Partieaufbau.Hausname(_familie.Text)).ToList();
        if (namen.GroupBy(n => n).FirstOrDefault(g => g.Count() > 1) is { } doppelt)
            fehler.Add($"„{doppelt.Key}“ heißen zwei Häuser.");
        var wappen = kis.Select(v => v.Wappen).OfType<Wappen>().Prepend(_wappen).ToList();
        if (wappen.Count != wappen.Distinct().Count())
            fehler.Add("Zwei Häuser führen dasselbe Wappen.");
        var berufe = kis.Select(v => v.Beruf).OfType<string>().Prepend(GewaehlterBeruf(_beruf) ?? "").ToList();
        if (berufe.GroupBy(b => b).FirstOrDefault(g => g.Count() > 1) is { } doppelterBeruf)
            fehler.Add($"Den Beruf „{doppelterBeruf.Key}“ hat nur ein Haus – jeder Beruf kommt einmal vor.");
        if (fehler.Count == 0 && DatenLesekopie() is { } daten)
        {
            try
            {
                Partiebesetzung.SpielerWuerfeln(daten, Stadtkarten.Startkarte, startwert,
                    new Hausvorgabe(Partieaufbau.Hausname(_familie.Text), _wappen, (Volk)_volk.GetSelectedId(), (Konfession)_glaube.GetSelectedId(), GewaehlterBeruf(_beruf)), vorgaben);
            }
            catch (ArgumentException e)
            {
                fehler.Add(e.Message);
            }
        }
        return fehler.Count == 0 ? null : string.Join("\n", fehler);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Erweitert
    // ═════════════════════════════════════════════════════════════════════════

    private void ErweitertUmschalten()
    {
        _erweitert.Visible = !_erweitert.Visible;
        _erweitertKnopf.Text = _erweitert.Visible ? "Erweitert verbergen" : "Erweitert anzeigen";
    }

    /// <summary>
    /// Der aufklappbare Bereich: jede KI einzeln (Name, Wappen, Volk, Glaube, Beruf; leer = zufällig), „KI-Häuser mit Umland“ (die bisherige
    /// Schwierigkeit) und die übrigen Einstellungen der Partie – Siegbedingung, Dauer, Statut, Startvermögen, Aktionspunkte, Hofstelle, Zufallsstartwert.
    /// </summary>
    private VBoxContainer ErweitertBauen()
    {
        var wurzel = new VBoxContainer();
        wurzel.AddThemeConstantOverride("separation", 10);
        wurzel.AddChild(new HSeparator());

        _kiListeKopf = Text(wurzel, "KI-Häuser einzeln festlegen – leer oder „zufällig“ lässt würfeln", 14, Gold);
        _kiListe = new VBoxContainer();
        _kiListe.AddThemeConstantOverride("separation", 8);
        wurzel.AddChild(_kiListe);
        wurzel.AddChild(new HSeparator());

        var raster = new GridContainer { Columns = 2 };
        raster.AddThemeConstantOverride("h_separation", 16);
        raster.AddThemeConstantOverride("v_separation", 8);
        wurzel.AddChild(raster);

        _schwierigkeit = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        _schwierigkeit.AddItem("Nein", (int)Schwierigkeit.Normal);
        _schwierigkeit.AddItem("Ja", (int)Schwierigkeit.Schwer);
        _schwierigkeit.ItemSelected += _ => SchwierigkeitHinweisZeigen();
        Zeile(raster, "KI-Häuser mit Umland", _schwierigkeit);
        _schwierigkeitHinweis = new Label { CustomMinimumSize = new Vector2(330, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _schwierigkeitHinweis.AddThemeFontSizeOverride("font_size", 12);
        _schwierigkeitHinweis.AddThemeColorOverride("font_color", new Color(0.72f, 0.72f, 0.72f));
        Zeile(raster, "", _schwierigkeitHinweis);
        SchwierigkeitHinweisZeigen();

        _rivale = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        _rivale.AddItem("Vorsichtig (leichter)", (int)KiCharakter.Vorsichtig);
        _rivale.AddItem("Ausgewogen", (int)KiCharakter.Ausgewogen);
        _rivale.AddItem("Wagemutig (schwerer)", (int)KiCharakter.Wagemutig);
        _rivale.Select(1);
        Zeile(raster, "Die KI-Häuser spielen", _rivale);

        _hofstelle = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        _hofstelle.TooltipText = "Eine Hofstelle ist Wohnhaus und Werkstatt nebeneinander. Zufall lost eine unter den Startplätzen der Karte; die KI-Häuser bekommen immer geloste Hofstellen.";
        Zeile(raster, "Hofstelle", _hofstelle);
        HofstellenWahlFuellen();

        _sieg = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (var (text, wert) in new[] { ("Partie endet in einem Jahr", Siegbedingung.Endjahr), ("Zielvermögen erreichen", Siegbedingung.Zielvermoegen),
                                             ("Letzte Dynastie gewinnt", Siegbedingung.LetzteDynastie), ("Endlos spielen", Siegbedingung.Endlos) })
            _sieg.AddItem(text, (int)wert);
        _sieg.ItemSelected += _ => SiegFelderZeigen();
        Zeile(raster, "Siegbedingung", _sieg);

        _endjahr = Zahl(Spielzeit.StartJahr + 1, 2000, 1, 1600);
        _endjahrZeile = Zeile(raster, "Endjahr", _endjahr);
        _zielvermoegen = Zahl(10_000, 10_000_000, 10_000, 200_000, " Gulden");
        _zielZeile = Zeile(raster, "Zielvermögen", _zielvermoegen);

        _dauer = Zahl(1, 120, 1, 15, " Min.");
        Zeile(raster, "Dauer eines Spieljahres", _dauer);

        _statut = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (var statut in Enum.GetValues<Fronstatut>())
            _statut.AddItem($"{statut.Kuerzel()} – {statut.Name()}", (int)statut);
        _statut.Select(_statut.GetItemIndex((int)Fronstatut.Bestienfron));
        Zeile(raster, "Fron- und Sklavenstatut", _statut);

        _startvermoegen = Zahl(1000, 50_000, 500, 4000, " Gulden");
        Zeile(raster, "Startvermögen", _startvermoegen);

        _aktionspunkte = Zahl(Partieeinstellungen.MinAktionspunkte, Partieeinstellungen.MaxAktionspunkte, 1, Partieeinstellungen.Standard.AktionspunkteProRunde, " AP");
        _aktionspunkte.TooltipText = "So viele Aktionspunkte bekommt jedes Haus zum Jahreswechsel – für Werben, Rat, Gericht, Hinterzimmer und Üben.";
        Zeile(raster, "Aktionspunkte je Jahr", _aktionspunkte);

        var startwertZeile = new HBoxContainer();
        _startwert = Zahl(0, 999_999, 1, 1400);
        _startwert.ValueChanged += _ => ZuegeZeigen(); // die Züge hängen am Startwert
        startwertZeile.AddChild(_startwert);
        var wuerfeln = new Button { Text = "Würfeln", FocusMode = Control.FocusModeEnum.None };
        wuerfeln.Pressed += () => _startwert.Value = GD.RandRange(0, 999_999);
        startwertZeile.AddChild(wuerfeln);
        Zeile(raster, "Zufallsstartwert", startwertZeile);

        Text(wurzel, "Gleicher Startwert und gleiche Entscheidungen ergeben dieselbe Partie. Der Vorschlag oben und die Persönlichkeitszüge hängen am Startwert.", 12,
            new Color(0.65f, 0.65f, 0.65f), umbruchBreite: 600);
        SiegFelderZeigen();
        return wurzel;
    }
}
