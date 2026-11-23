using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das Handelsfenster am Markt („Karren selbst fahren“): Steht ein Karren des Hauses am Markt, verkauft der Spieler seine Ladung und kauft
/// Waren auf den Karren (<see cref="KarrenHandeln"/>; verkauft wird wie vom Verwalter, gekauft zum Marktpreis mit Händlerspanne). Gehandelt
/// wird nur zu den Marktzeiten. Von hier geht es zurück zum Transportmenü (<see cref="KarrenFahrtAnzeige"/>), um den Karren heimzuschicken.
/// </summary>
public sealed class KarrenHandelAnzeige : KarrenTafel
{
    private string? _karren;
    private string? _kaufWare;
    private int _verkaufMenge = 10;
    private int _kaufMenge = 10;

    private List<Eintrag> AmMarkt(Stadtwirtschaft w) => MeineKarren(w).Where(e => e.Karren.Zustand == KarrenZustand.AmMarkt).ToList();

    private Eintrag? Gewaehlt(List<Eintrag> am) => am.FirstOrDefault(e => e.Schluessel == (Vorwahl ?? _karren)) ?? am.FirstOrDefault();

    protected override string Stand(Stadtwirtschaft w, Betrieb b)
    {
        var am = AmMarkt(w);
        var g = Gewaehlt(am);
        var preise = g is { } e ? string.Join(",", e.Karren.Ladung.Keys.Concat(_kaufWare is null ? Array.Empty<string>() : new[] { _kaufWare }).Distinct().Where(x => w.Daten.Waren.ContainsKey(x)).Select(x => (int)Math.Round(w.Markt[x].Preis * 10))) : "";
        return $"{string.Join(";", am.Select(x => $"{x.Schluessel}:{LadungStand(x.Karren)}"))}|{g?.Schluessel}|{preise}|{_kaufWare}|{w.Uhr.UhrzeitInMinuten / 30}|{Math.Round(Spieler!.Vermoegen)}|{w.HausFaehrtAutomatisch(Spieler!)}";
    }

    protected override void Aufbauen(Stadtwirtschaft w, Betrieb b)
    {
        Titel("Handel am Markt", b, "Die Ladung des Karrens verkaufen, Waren auf ihn kaufen. Gehandelt wird nur zu den Marktzeiten.");
        if (w.HausFaehrtAutomatisch(Spieler!))
        {
            Hauptmenue.Text(Inhalt, "Die Karren dieses Hauses fahren von selbst und handeln selbst.", Masz("schrift"), Leise);
            return;
        }
        var am = AmMarkt(w);
        if (am.Count == 0)
        {
            Hauptmenue.Text(Inhalt, "Kein Karren des Hauses steht am Markt. Schickt einen hin (Karrenfahrt, Ziel Markt).", Masz("schrift"), Leise, umbruchBreite: Masz("breite") - 60);
            Knopf(Inhalt, "Karrenfahrt …", "Das Transportmenü öffnen.", () => Weiter("fahrt", b, null));
            return;
        }
        var e = Gewaehlt(am)!;
        _karren = e.Schluessel;
        Vorwahl = null;
        var k = e.Karren;
        var waehrung = Waehrung(w);

        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 10);
        Inhalt.AddChild(kopf);
        Zeile(kopf, "Karren:", Schrift);
        var liste = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Welcher der am Markt stehenden Karren handelt." };
        foreach (var x in am) liste.AddItem(x.Heimat == b ? x.Karren.Name : $"{x.Karren.Name} ({x.Heimat.Name})");
        liste.Select(am.IndexOf(e));
        liste.ItemSelected += i => _karren = am[(int)i].Schluessel;
        kopf.AddChild(liste);
        Zeile(kopf, $"Ladeflächen: {Zahlenformat.Ganz(k.BelegteFlaechen)} von {Zahlenformat.Ganz(k.Daten.Sorten)} · Kasse: {Zahlenformat.Ganz(Spieler!.Vermoegen)} {waehrung}", Leise);
        if (w.Daten.Handel is { } h)
            Hauptmenue.Text(Inhalt, $"Der Markt hat von {h.MarktVon} bis {h.MarktBis} Uhr offen.", Masz("klein"), Leise);

        Hauptmenue.Text(Inhalt, "Verkaufen", Masz("schrift") + 2, Akzent);
        if (k.Ladung.Count == 0) Hauptmenue.Text(Inhalt, "Der Karren hat nichts geladen.", Masz("schrift"), Leise);
        else
        {
            var menge = new HBoxContainer();
            menge.AddThemeConstantOverride("separation", 10);
            Inhalt.AddChild(menge);
            Zeile(menge, "Menge:", Schrift);
            Zahl(menge, _verkaufMenge, 9999, v => _verkaufMenge = v, "So viele Stück werden mit „verkaufen“ abgegeben (höchstens so viele, wie geladen sind).");
            var raster = Raster(6, 12, true);
            foreach (var kopfzeile in new[] { "", "Ware", "Karren", "Preis", "", "" }) Zeile(raster, kopfzeile, Leise);
            foreach (var (ware, anzahl) in k.Ladung.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var ab = raster.GetChildCount();
                raster.AddChild(WarenIcons.Bild(ware, 32));
                Zeile(raster, ware, Schrift, Masz("spalte_ware"));
                Zeile(raster, Zahlenformat.Ganz(anzahl), Schrift, Masz("spalte_zahl"), rechts: true);
                Zeile(raster, w.Daten.Waren.ContainsKey(ware) ? $"{Zahlenformat.Kommazahl(w.Markt[ware].Preis)} {waehrung}" : "–", Schrift, Masz("spalte_zahl") + 30, rechts: true);
                var einzeln = new Dictionary<string, int> { [ware] = Math.Min(_verkaufMenge, anzahl) };
                Knopf(raster, "verkaufen", "Diese Menge zum Marktpreis verkaufen (mit Verhandeln, Steuer und Hafenaufschlag wie beim Verwalter).",
                    () => Befehlen(new KarrenHandeln(e.Heimat.Name, e.Nr, einzeln, new Dictionary<string, int>())),
                    w.KarrenHandelnVerboten(Spieler!, e.Heimat, e.Nr, einzeln, new Dictionary<string, int>()));
                var alles = new Dictionary<string, int> { [ware] = anzahl };
                Knopf(raster, "alles", "Die ganze Menge dieser Ware verkaufen.",
                    () => Befehlen(new KarrenHandeln(e.Heimat.Name, e.Nr, alles, new Dictionary<string, int>())),
                    w.KarrenHandelnVerboten(Spieler!, e.Heimat, e.Nr, alles, new Dictionary<string, int>()));
                ZeileTipp(raster, ab, $"{ware}: Marktpreis {(w.Daten.Waren.ContainsKey(ware) ? Zahlenformat.Kommazahl(w.Markt[ware].Preis) : "–")} {waehrung}");
            }
        }

        Inhalt.AddChild(new HSeparator());
        Hauptmenue.Text(Inhalt, "Kaufen", Masz("schrift") + 2, Akzent);
        var kauf = new HBoxContainer();
        kauf.AddThemeConstantOverride("separation", 10);
        Inhalt.AddChild(kauf);
        var waren = w.Markt.Staende.Select(s => s.Ware).Where(x => w.Daten.Waren.ContainsKey(x)).OrderBy(x => x, StringComparer.Ordinal).ToList();
        _kaufWare = waren.Contains(_kaufWare ?? "") ? _kaufWare : waren.FirstOrDefault();
        var wahl = new OptionButton { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(Masz("auswahl") / 2, 0), TooltipText = "Welche Ware auf den Karren gekauft wird." };
        foreach (var x in waren) wahl.AddItem(x);
        wahl.Select(Math.Max(0, waren.IndexOf(_kaufWare ?? "")));
        wahl.ItemSelected += i => _kaufWare = waren[(int)i];
        kauf.AddChild(wahl);
        Zahl(kauf, _kaufMenge, 9999, v => _kaufMenge = v, "So viele Stück werden gekauft (je Ladefläche eine Ware, höchstens so viele wie auf die Fläche passen).");
        if (_kaufWare is { } kw)
        {
            var kaufen = new Dictionary<string, int> { [kw] = _kaufMenge };
            var kosten = w.HandKaufpreis(Spieler!, kw, _kaufMenge);
            Knopf(kauf, "kaufen", $"{Zahlenformat.Ganz(_kaufMenge)} {kw} auf den Karren kaufen: zum Marktpreis mit Händlerspanne, jedes Stück hebt den Preis.",
                () => Befehlen(new KarrenHandeln(e.Heimat.Name, e.Nr, new Dictionary<string, int>(), kaufen)),
                w.KarrenHandelnVerboten(Spieler!, e.Heimat, e.Nr, new Dictionary<string, int>(), kaufen));
            Zeile(kauf, $"etwa {Zahlenformat.Ganz(kosten)} {waehrung}", Leise);
        }
        Inhalt.AddChild(new HSeparator());
        Knopf(Inhalt, "Karrenfahrt …", "Den Karren heimschicken (Transportmenü).", () => Weiter("fahrt", b, e.Schluessel));
    }
}
