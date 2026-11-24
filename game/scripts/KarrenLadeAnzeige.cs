using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das Ladefenster an einem eigenen Betrieb („Karren selbst fahren“): Steht ein Karren hier, lädt der Spieler Waren aus dem Lager auf den Karren,
/// lädt Ladung ins Lager ab oder lädt „alles ab“. Je Ladefläche gehört eine Ware auf den Karren (<see cref="KarrenBeladen"/>, <see cref="KarrenAbladen"/>).
/// Von hier geht es zurück zum Transportmenü (<see cref="KarrenFahrtAnzeige"/>), um den Karren loszuschicken.
/// </summary>
public sealed class KarrenLadeAnzeige : KarrenTafel
{
    private string? _karren;
    private int _menge = 10;

    private List<Eintrag> Hier(Stadtwirtschaft w, Betrieb b) =>
        MeineKarren(w).Where(e => e.Karren.Zustand == KarrenZustand.AmBetrieb && e.Karren.StandortVon(w) == b).ToList();

    private static List<string> Waren(Betrieb b, Karren k) =>
        b.Zutaten.Keys.Concat(b.Artdaten.Produkte).Append(b.Produkt).Concat(b.Lager.Keys).Concat(k.Ladung.Keys)
            .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();

    protected override string Stand(Stadtwirtschaft w, Betrieb b)
    {
        var hier = Hier(w, b);
        var gewaehlt = Gewaehlt(hier);
        return $"{string.Join(";", hier.Select(e => $"{e.Schluessel}:{LadungStand(e.Karren)}"))}|{gewaehlt?.Schluessel}|" +
               (gewaehlt is { } g ? string.Join(";", Waren(b, g.Karren).Select(x => $"{x}:{b.ImLager(x)}")) : "") + $"|{w.HausFaehrtAutomatisch(Spieler!)}";
    }

    private Eintrag? Gewaehlt(List<Eintrag> hier) => hier.FirstOrDefault(e => e.Schluessel == (Vorwahl ?? _karren)) ?? hier.FirstOrDefault();

    protected override void Aufbauen(Stadtwirtschaft w, Betrieb b)
    {
        Titel("Karren beladen", b, "Waren aus dem Lager auf den Karren laden oder von ihm abladen. Je Ladefläche gehört eine Ware auf den Karren.");
        if (w.HausFaehrtAutomatisch(Spieler!))
        {
            Hauptmenue.Text(Inhalt, "Die Karren dieses Hauses fahren von selbst und laden selbst.", Masz("schrift"), Leise);
            return;
        }
        var hier = Hier(w, b);
        if (hier.Count == 0)
        {
            Hauptmenue.Text(Inhalt, "Hier steht kein Karren. Schickt einen Karren hierher (Karrenfahrt, Ziel dieser Betrieb).", Masz("schrift"), Leise, umbruchBreite: Masz("breite") - 60);
            Knopf(Inhalt, "Karrenfahrt …", "Das Transportmenü öffnen.", () => Weiter("fahrt", b, null));
            return;
        }
        var e = Gewaehlt(hier)!;
        _karren = e.Schluessel;
        Vorwahl = null;
        var k = e.Karren;

        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 10);
        Inhalt.AddChild(kopf);
        Zeile(kopf, "Karren:", Schrift);
        var liste = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Welcher der hier stehenden Karren geladen wird." };
        foreach (var x in hier) liste.AddItem(x.Heimat == b ? x.Karren.Name : $"{x.Karren.Name} ({x.Heimat.Name})");
        liste.Select(hier.IndexOf(e));
        liste.ItemSelected += i => _karren = hier[(int)i].Schluessel;
        kopf.AddChild(liste);
        var flaeche = Zeile(kopf, $"Ladeflächen: {Zahlenformat.Ganz(k.BelegteFlaechen)} von {Zahlenformat.Ganz(k.Daten.Sorten)} (je {Zahlenformat.Ganz(k.Daten.KapazitaetJeSorte)} Stück)", k.BelegteFlaechen >= k.Daten.Sorten ? Warnung : Leise);
        flaeche.TooltipText = "Jede Ladefläche trägt eine Ware, bis zu der Stückzahl je Fläche.";

        var mengenzeile = new HBoxContainer();
        mengenzeile.AddThemeConstantOverride("separation", 10);
        Inhalt.AddChild(mengenzeile);
        Zeile(mengenzeile, "Menge:", Schrift);
        Zahl(mengenzeile, _menge, 9999, v => _menge = v, "So viele Stück werden mit „laden“ und „abladen“ bewegt (höchstens so viele, wie da sind).");

        var raster = Raster(6, 12, true);
        foreach (var kopfzeile in new[] { "", "Ware", "Lager", "Karren", "", "" }) Zeile(raster, kopfzeile, Leise);
        foreach (var ware in Waren(b, k))
        {
            var imLager = b.ImLager(ware);
            var aufKarren = k.Ladung.GetValueOrDefault(ware);
            var ab = raster.GetChildCount();
            raster.AddChild(WarenIcons.Bild(ware, 32));
            Zeile(raster, ware, Schrift, Masz("spalte_ware"));
            Zeile(raster, Zahlenformat.Ganz(imLager), Schrift, Masz("spalte_zahl"), rechts: true);
            Zeile(raster, Zahlenformat.Ganz(aufKarren), aufKarren > 0 ? Akzent : Leise, Masz("spalte_zahl"), rechts: true);
            var laden = new Dictionary<string, int> { [ware] = Math.Min(_menge, imLager) };
            Knopf(raster, "laden", "Aus dem Lager auf den Karren laden.", () => Befehlen(new KarrenBeladen(e.Heimat.Name, e.Nr, laden)),
                imLager == 0 ? $"Im Lager liegt keine {ware}." : w.KarrenBeladenVerboten(Spieler!, e.Heimat, e.Nr, laden));
            var ablad = new Dictionary<string, int> { [ware] = Math.Min(_menge, aufKarren) };
            Knopf(raster, "abladen", "Vom Karren ins Lager abladen.", () => Befehlen(new KarrenAbladen(e.Heimat.Name, e.Nr, ablad)),
                aufKarren == 0 ? $"Auf dem Karren liegt keine {ware}." : w.KarrenAbladenVerboten(Spieler!, e.Heimat, e.Nr, ablad));
            ZeileTipp(raster, ab, WarenTipp(w, b, ware));
        }

        Inhalt.AddChild(new HSeparator());
        var fuss = new HBoxContainer();
        fuss.AddThemeConstantOverride("separation", 10);
        Inhalt.AddChild(fuss);
        Knopf(fuss, "Alles abladen", "Die ganze Ladung des Karrens ins Lager legen.", () => Befehlen(new KarrenAbladen(e.Heimat.Name, e.Nr, null)),
            w.KarrenAbladenVerboten(Spieler!, e.Heimat, e.Nr));
        Knopf(fuss, "Karrenfahrt …", "Den Karren losschicken (Transportmenü).", () => Weiter("fahrt", b, e.Schluessel));
    }
}
