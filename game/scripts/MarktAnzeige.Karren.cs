using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Karren selbst fahren: Gehandelt wird nur hier im Marktmenü und nur mit einem eigenen Karren, der am Markt steht
/// (<see cref="KarrenZustand.AmMarkt"/>). Der Spieler verkauft Waren aus der Ladung und kauft Waren in die Ladung
/// (<see cref="KarrenHandeln"/>; die Regeln und Gründe liefert <see cref="Stadtwirtschaft.KarrenHandelnVerboten"/>). Nichts wird vorher
/// festgelegt – Handelsauftrag, Vormerkung und „zurückhalten“ gibt es im Selbstfahren nicht (<see cref="Stadtwirtschaft.VerwalterhandelVerboten"/>).
/// </summary>
public partial class MarktAnzeige
{
    private sealed record AmMarktKarren(Betrieb Heimat, int Nr, Karren Karren)
    {
        public string Schluessel => $"{Heimat.Name}#{Nr}";
    }

    private VBoxContainer? _karrenInhalt;
    private Label? _karrenMeldung;
    private string? _selbstKarren;
    private string? _selbstKaufWare;
    private int _selbstVerkaufMenge = 10;
    private int _selbstKaufMenge = 10;
    private string _karrenStand = "";

    /// <summary>Öffnet den Markt mit diesem Karren (Schlüssel „Betrieb#Nummer“) vorgewählt – für den Knopf „Handeln …“ der Karrentafel.</summary>
    public void Oeffnen(Betrieb? betrieb, string? karrenSchluessel)
    {
        _selbstKarren = karrenSchluessel;
        Oeffnen(betrieb);
    }

    private List<AmMarktKarren> KarrenAmMarkt(Stadtwirtschaft w) =>
        w.Betriebe.Where(b => b.Konto == _spieler)
            .SelectMany(b => b.Karren.Select((k, i) => new AmMarktKarren(b, i, k)))
            .Where(e => e.Karren.Zustand == KarrenZustand.AmMarkt).ToList();

    private void KarrenAufbauen(Stadtwirtschaft w)
    {
        var rahmen = new PanelContainer();
        rahmen.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.18f, 0.15f, 0.11f), BorderColor = new Color(0.50f, 0.40f, 0.26f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 8, ContentMarginBottom = 8,
        });
        _inhalt.AddChild(rahmen);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        rahmen.AddChild(box);
        Hauptmenue.Text(box, "Handel mit dem Karren", 16, Gold);
        _karrenInhalt = new VBoxContainer();
        _karrenInhalt.AddThemeConstantOverride("separation", 6);
        box.AddChild(_karrenInhalt);
        _karrenMeldung = Hauptmenue.Text(box, "", 13, Grau);
        _karrenMeldung.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _karrenMeldung.CustomMinimumSize = new Vector2(Spalten * (Kachelbreite + 6) - 40, 0);
        _karrenStand = "";
        KarrenAktualisieren(w);
    }

    private void KarrenAktualisieren(Stadtwirtschaft w)
    {
        if (_karrenInhalt is null || _spieler is null) return;
        var am = KarrenAmMarkt(w);
        var gewaehlt = am.FirstOrDefault(e => e.Schluessel == _selbstKarren) ?? am.FirstOrDefault();
        var waren = w.Markt.Staende.Select(s => s.Ware).Where(x => w.Daten.Waren.ContainsKey(x)).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var preise = gewaehlt is null ? "" : string.Join(",", gewaehlt.Karren.Ladung.Keys.Append(_selbstKaufWare ?? "").Distinct().Where(x => w.Daten.Waren.ContainsKey(x)).Select(x => (int)Math.Round(w.Markt[x].Preis * 10)));
        var stand = $"{string.Join(";", am.Select(x => $"{x.Schluessel}:{string.Join(",", x.Karren.Ladung.OrderBy(l => l.Key, StringComparer.Ordinal).Select(l => $"{l.Key}:{l.Value}"))}"))}|{gewaehlt?.Schluessel}|{preise}|{_selbstKaufWare}|{w.Uhr.UhrzeitInMinuten / 30}|{Math.Round(_spieler.Vermoegen)}";
        if (stand == _karrenStand) return;
        // Wer gerade in ein Zahlfeld tippt, wird nicht unterbrochen.
        if (_karrenInhalt.GetViewport()?.GuiGetFocusOwner() is LineEdit fokus && _karrenInhalt.IsAncestorOf(fokus)) return;
        _karrenStand = stand;
        UiHilfe.KinderEntfernen(_karrenInhalt);

        var waehrung = w.Daten.Allgemein.Waehrung;
        if (gewaehlt is null)
        {
            Hauptmenue.Text(_karrenInhalt, "Kein Karren am Markt – schicke einen Karren her, um zu handeln.", 13, Grau,
                umbruchBreite: Spalten * (Kachelbreite + 6) - 40);
            Hauptmenue.Text(_karrenInhalt, "Im Transportmenü des Betriebs (Karrenfahrt, Ziel Markt) schickst du ihn los; gehandelt wird erst, wenn er hier steht.", 12, Grau,
                umbruchBreite: Spalten * (Kachelbreite + 6) - 40);
            return;
        }
        _selbstKarren = gewaehlt.Schluessel;
        var k = gewaehlt.Karren;

        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 10);
        _karrenInhalt.AddChild(kopf);
        Hauptmenue.Text(kopf, "Karren:", 13, Hell);
        var liste = new OptionButton { FocusMode = FocusModeEnum.None, TooltipText = "Welcher der am Markt stehenden Karren handelt." };
        foreach (var x in am) liste.AddItem($"{x.Karren.Name} ({x.Heimat.Name})");
        liste.Select(am.IndexOf(gewaehlt));
        liste.ItemSelected += i => { _selbstKarren = am[(int)i].Schluessel; _karrenStand = ""; KarrenAktualisieren(w); };
        kopf.AddChild(liste);
        Hauptmenue.Text(kopf, $"Ladeflächen: {Zahlenformat.Ganz(k.BelegteFlaechen)} von {Zahlenformat.Ganz(k.Daten.Sorten)} " +
                              $"({Zahlenformat.Ganz(k.Daten.KapazitaetJeSorte)} Stück je Fläche) · Kasse: {Zahlenformat.Ganz(_spieler.Vermoegen)} {waehrung}", 13, Grau);
        if (w.Daten.Handel is { } h)
            Hauptmenue.Text(_karrenInhalt, $"Der Markt hat von {h.MarktVon} bis {h.MarktBis} Uhr offen. Vom Erlös geht die Marktsteuer ab ({Zahlenformat.Kommazahl(w.SteuersatzMarkt(_spieler) * 100)} %).", 12, Grau);

        // Verkaufen aus der Ladung
        Hauptmenue.Text(_karrenInhalt, "Verkaufen (aus der Ladung)", 14, Gold);
        if (k.Ladung.Count == 0) Hauptmenue.Text(_karrenInhalt, "Der Karren hat nichts geladen.", 13, Grau);
        else
        {
            var menge = new HBoxContainer();
            menge.AddThemeConstantOverride("separation", 10);
            _karrenInhalt.AddChild(menge);
            Hauptmenue.Text(menge, "Menge:", 13, Hell);
            ZahlAnlegen(menge, _selbstVerkaufMenge, v => _selbstVerkaufMenge = v, "So viele Stück werden mit „verkaufen“ abgegeben (höchstens so viele, wie geladen sind).");
            var raster = new GridContainer { Columns = 6 };
            raster.AddThemeConstantOverride("h_separation", 12);
            _karrenInhalt.AddChild(raster);
            foreach (var kopfzeile in new[] { "", "Ware", "Karren", "Preis", "", "" }) Hauptmenue.Text(raster, kopfzeile, 12, Grau);
            foreach (var (ware, anzahl) in k.Ladung.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                raster.AddChild(WarenIcons.Bild(ware, 32));
                Hauptmenue.Text(raster, ware, 13, Hell);
                Hauptmenue.Text(raster, Zahlenformat.Ganz(anzahl), 13, Hell);
                Hauptmenue.Text(raster, w.Daten.Waren.ContainsKey(ware) ? $"{Zahlenformat.Kommazahl(w.Markt[ware].Preis)} {waehrung}" : "–", 13, Hell);
                var einzeln = new Dictionary<string, int> { [ware] = Math.Min(_selbstVerkaufMenge, anzahl) };
                var alles = new Dictionary<string, int> { [ware] = anzahl };
                var leer = new Dictionary<string, int>();
                KarrenKnopf(raster, "verkaufen", $"Diese Menge zum Marktpreis verkaufen (mit Verhandeln, Steuer und Hafenaufschlag wie beim Verwalter): etwa {Zahlenformat.Ganz(w.Daten.Waren.ContainsKey(ware) ? w.KarrenErloes(k, ware, einzeln[ware]) : 0)} {waehrung} vor Steuer.",
                    () => new KarrenHandeln(gewaehlt.Heimat.Name, gewaehlt.Nr, new Dictionary<string, int> { [ware] = Math.Min(_selbstVerkaufMenge, anzahl) }, leer), w.KarrenHandelnVerboten(_spieler, gewaehlt.Heimat, gewaehlt.Nr, einzeln, leer));
                KarrenKnopf(raster, "alles", "Die ganze Menge dieser Ware verkaufen.",
                    () => new KarrenHandeln(gewaehlt.Heimat.Name, gewaehlt.Nr, alles, leer), w.KarrenHandelnVerboten(_spieler, gewaehlt.Heimat, gewaehlt.Nr, alles, leer));
            }
        }

        // Kaufen in die Ladung
        _karrenInhalt.AddChild(new HSeparator());
        Hauptmenue.Text(_karrenInhalt, "Kaufen (in die Ladung)", 14, Gold);
        var kauf = new HBoxContainer();
        kauf.AddThemeConstantOverride("separation", 10);
        _karrenInhalt.AddChild(kauf);
        _selbstKaufWare = waren.Contains(_selbstKaufWare ?? "") ? _selbstKaufWare : waren.FirstOrDefault();
        var wahl = new OptionButton { FocusMode = FocusModeEnum.None, TooltipText = "Welche Ware auf den Karren gekauft wird." };
        foreach (var x in waren) wahl.AddItem(x);
        wahl.Select(Math.Max(0, waren.IndexOf(_selbstKaufWare ?? "")));
        wahl.ItemSelected += i => { _selbstKaufWare = waren[(int)i]; _karrenStand = ""; KarrenAktualisieren(w); };
        kauf.AddChild(wahl);
        ZahlAnlegen(kauf, _selbstKaufMenge, v => _selbstKaufMenge = v, "So viele Stück werden gekauft (je Ladefläche eine Ware, höchstens so viele wie auf die Fläche passen).");
        if (_selbstKaufWare is { } kw)
        {
            var kaufen = new Dictionary<string, int> { [kw] = _selbstKaufMenge };
            var leer = new Dictionary<string, int>();
            KarrenKnopf(kauf, "kaufen", $"{Zahlenformat.Ganz(_selbstKaufMenge)} {kw} in die Ladung kaufen: Marktpreis mit Händlerspanne, jedes Stück hebt den Preis.",
                () => new KarrenHandeln(gewaehlt.Heimat.Name, gewaehlt.Nr, leer, new Dictionary<string, int> { [kw] = _selbstKaufMenge }), w.KarrenHandelnVerboten(_spieler, gewaehlt.Heimat, gewaehlt.Nr, leer, kaufen));
            Hauptmenue.Text(kauf, $"etwa {Zahlenformat.Ganz(w.HandKaufpreis(_spieler, kw, _selbstKaufMenge))} {waehrung}", 13, Grau);
        }
    }

    private void ZahlAnlegen(Container eltern, int wert, Action<int> geaendert, string tipp)
    {
        var feld = new Zahlfeld { MinValue = 1, MaxValue = 9999, Step = 1, Value = Math.Clamp(wert, 1, 9999), CustomMinimumSize = new Vector2(90, 0), TooltipText = tipp };
        feld.ValueChanged += v => { geaendert((int)v); _karrenStand = ""; }; // beim nächsten Auffrischen neu aufbauen (Knopf-Sperren und Schätzungen)
        eltern.AddChild(feld);
    }

    private void KarrenKnopf(Container eltern, string text, string tipp, Func<Befehl> befehl, string? grund)
    {
        var knopf = Hauptmenue.Knopf(eltern, text, () => KarrenBefehl(befehl()));
        knopf.TooltipText = grund ?? tipp;
        knopf.Disabled = grund is not null;
    }

    private void KarrenBefehl(Befehl befehl)
    {
        UiHilfe.Befehlen(_simulation, _spieler, befehl, this, e =>
        {
            if (_karrenMeldung is null || _simulation?.Wirtschaft is not { } w) return;
            _karrenMeldung.Text = UiHilfe.MeldungText(e);
            _karrenMeldung.AddThemeColorOverride("font_color", e.Erfolg ? Gruen : Rot);
            Gemeldet?.Invoke(_karrenMeldung.Text);
            _karrenStand = "";
            KarrenAktualisieren(w);
        });
    }
}
