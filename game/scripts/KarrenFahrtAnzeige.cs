using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das Transportmenü („Karren selbst fahren“, Nutzer 23.11.2026): einen oder mehrere Karren des Hauses wählen, das Ziel aus der Liste wählen
/// (Markt oder eigener Betrieb in der Stadt) oder – später, Paket K3 – im Bild auf das Gebäude klicken, die Eskorte dieser Fahrt wählen und
/// losschicken oder heimschicken. Am Ziel steht der Karren, bis ihm ein Befehl gegeben wird; von hier führen Knöpfe zum Ladefenster
/// (<see cref="KarrenLadeAnzeige"/>) und zum Handelsfenster (<see cref="KarrenHandelAnzeige"/>).
///
/// <b>Schnittstelle zur Stadtansicht (K3):</b> Drückt der Spieler „Ziel im Bild wählen“, meldet <see cref="ZielWahlImBild"/>(true); die Stadtansicht
/// nimmt dann den Klick auf ein Gebäude an und ruft <see cref="ZielSetzen"/> mit dem Betriebsnamen (null: Markt). <see cref="ZielWahlImBild"/>(false)
/// heißt: die Wahl im Bild ist zu Ende (Abbruch, Tafel geschlossen oder Ziel gesetzt).
/// </summary>
public sealed class KarrenFahrtAnzeige : KarrenTafel
{
    private readonly HashSet<string> _gewaehlt = new();
    private string? _ziel; // Betriebsname; null = Markt
    private string? _eskorte; // null: die Stufe des ersten gewählten Karrens
    private bool _imBild;

    /// <summary>Die Wahl des Ziels im Bild beginnt (true) oder endet (false) – für die Stadtansicht.</summary>
    public event Action<bool>? ZielWahlImBild;

    /// <summary>Das Ziel der Fahrt kam aus dem Bild (Klick aufs Gebäude) oder der Liste: Betriebsname, null = Markt.</summary>
    public event Action<string?>? ZielGewaehlt;

    /// <summary>Die Stadtansicht hat ein Gebäude angeklickt: Ziel setzen (Betriebsname eines eigenen Betriebs in der Stadt; null = Markt).</summary>
    public void ZielSetzen(string? betrieb)
    {
        _ziel = betrieb;
        ImBildBeenden();
        ZielGewaehlt?.Invoke(betrieb);
    }

    public override void Hide()
    {
        ImBildBeenden();
        base.Hide();
    }

    private void ImBildBeenden()
    {
        if (!_imBild) return;
        _imBild = false;
        ZielWahlImBild?.Invoke(false);
    }

    private List<Eintrag> Karren(Stadtwirtschaft w, Betrieb b) =>
        MeineKarren(w).OrderBy(e => e.Karren.StandortVon(w) == b ? 0 : e.Heimat == b ? 1 : 2).ThenBy(e => e.Heimat.Name, StringComparer.Ordinal).ThenBy(e => e.Nr).ToList();

    protected override string Stand(Stadtwirtschaft w, Betrieb b)
    {
        var karren = Karren(w, b);
        var ziele = string.Join(",", w.Betriebe.Where(x => x.Konto == Spieler && x.InDerStadt).Select(x => x.Name));
        return $"{string.Join(";", karren.Select(e => $"{e.Schluessel}:{e.Karren.ZustandText(Sim!.Zeit)}:{LadungStand(e.Karren)}:{e.Karren.Eskorte}"))}|{ziele}|{string.Join(",", _gewaehlt.OrderBy(x => x))}|{_ziel}|{_eskorte}|{_imBild}|{w.HausFaehrtAutomatisch(Spieler!)}|{string.Join(",", w.Daten.Logistik.Eskorten.Keys.Where(w.Wache.EskorteVerfuegbar))}";
    }

    protected override void Aufbauen(Stadtwirtschaft w, Betrieb b)
    {
        var karren = Karren(w, b);
        Titel("Karrenfahrt", b, "Karren wählen, Ziel wählen, losschicken. Am Ziel steht der Karren, bis Ihr ihm einen Befehl gebt.");
        if (w.HausFaehrtAutomatisch(Spieler!))
        {
            Hauptmenue.Text(Inhalt, "Die Karren dieses Hauses fahren von selbst zum Markt und zurück (Entwicklerhaken „Karren fahren automatisch“).", Masz("schrift"), Leise, umbruchBreite: Masz("breite") - 60);
            return;
        }
        if (karren.Count == 0)
        {
            Hauptmenue.Text(Inhalt, "Das Haus hat keinen Karren. Kaufen kann man ihn im Karrenhof.", Masz("schrift"), Leise);
            return;
        }
        _gewaehlt.RemoveWhere(k => karren.All(e => e.Schluessel != k || e.Karren.Unterwegs));
        if (Vorwahl is { } vor && karren.Any(e => e.Schluessel == vor && !e.Karren.Unterwegs)) { _gewaehlt.Add(vor); }
        Vorwahl = null;

        var raster = Raster(5, 12, true);
        foreach (var kopf in new[] { "", "Karren", "Zustand", "Ladung", "" }) Zeile(raster, kopf, Leise);
        foreach (var e in karren)
        {
            var k = e.Karren;
            var grund = w.KarrenBefehlVerboten(Spieler!, e.Heimat, e.Nr);
            var an = new CheckBox { FocusMode = Control.FocusModeEnum.None, ButtonPressed = _gewaehlt.Contains(e.Schluessel), Disabled = grund is not null, TooltipText = grund ?? $"{k.Name} für die nächste Fahrt wählen." };
            an.Toggled += gedrueckt => { if (gedrueckt) _gewaehlt.Add(e.Schluessel); else _gewaehlt.Remove(e.Schluessel); };
            raster.AddChild(an);
            var ab = raster.GetChildCount();
            Zeile(raster, e.Heimat == b ? k.Name : $"{k.Name} ({e.Heimat.Name})", Schrift, Masz("spalte_name"));
            Zeile(raster, k.ZustandText(Sim!.Zeit), k.Unterwegs ? Akzent : Schrift, Masz("spalte_zustand")).AutowrapMode = TextServer.AutowrapMode.WordSmart;
            Zeile(raster, LadungText(k), Leise, Masz("spalte_ladung")).AutowrapMode = TextServer.AutowrapMode.WordSmart;
            if (k.Zustand == KarrenZustand.AmMarkt)
                Knopf(raster, "Handeln …", "Am Markt kaufen und verkaufen.", () => Weiter("handel", b, e.Schluessel), grund);
            else if (k.Zustand == KarrenZustand.AmBetrieb)
                Knopf(raster, "Beladen …", "Am Betrieb laden und abladen.", () => Weiter("laden", k.StandortVon(w) ?? e.Heimat, e.Schluessel), grund);
            else raster.AddChild(new Control());
            ZeileTipp(raster, ab, $"{k.Name}: {k.Typ}, {Zahlenformat.Ganz(k.Daten.Sorten)} Ladeflächen zu je {Zahlenformat.Ganz(k.Daten.KapazitaetJeSorte)} Stück.");
        }

        Inhalt.AddChild(new HSeparator());
        var wahl = _gewaehlt.Select(s => karren.First(e => e.Schluessel == s)).ToList();
        ZielZeile(w, b);
        EskorteZeile(w, wahl);
        Knoepfe(w, wahl);
    }

    private void ZielZeile(Stadtwirtschaft w, Betrieb b)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 10);
        Inhalt.AddChild(zeile);
        Zeile(zeile, "Ziel:", Schrift);
        var liste = new OptionButton { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(Masz("auswahl"), 0), TooltipText = "Wohin die gewählten Karren fahren: zum Markt oder zu einem eigenen Betrieb in der Stadt." };
        var namen = new List<string?> { null };
        liste.AddItem("Markt");
        foreach (var x in w.Betriebe.Where(x => x.Konto == Spieler && x.InDerStadt).OrderBy(x => x == b ? 0 : 1).ThenBy(x => x.Name, StringComparer.Ordinal))
        {
            namen.Add(x.Name);
            liste.AddItem(x.Name + (x == b ? " (hier)" : ""));
        }
        liste.Select(Math.Max(0, namen.IndexOf(_ziel)));
        liste.ItemSelected += i => { _ziel = namen[(int)i]; ZielGewaehlt?.Invoke(_ziel); };
        zeile.AddChild(liste);
        var bild = Hauptmenue.Knopf(zeile, _imBild ? "Gebäude anklicken … (Abbrechen)" : "Ziel im Bild wählen", () =>
        {
            _imBild = !_imBild;
            ZielWahlImBild?.Invoke(_imBild);
        });
        bild.TooltipText = "Auf das Gebäude in der Stadt klicken, zu dem die Karren fahren sollen (der Markt zählt auch).";
    }

    private void EskorteZeile(Stadtwirtschaft w, List<Eintrag> wahl)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 10);
        Inhalt.AddChild(zeile);
        Zeile(zeile, "Eskorte:", Schrift);
        var namen = w.Daten.Logistik.Eskorten.Keys.ToList();
        var liste = new OptionButton { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(Masz("auswahl"), 0),
                                       TooltipText = "Geleitschutz für diese Fahrt und alle folgenden, vorbelegt mit der Stufe des Karrens. Eine Stufe gibt es erst, wenn ein Haus Wächter dafür führt." };
        for (var i = 0; i < namen.Count; i++)
        {
            var preis = w.Daten.Logistik.Eskorten[namen[i]].KostenJeFahrt;
            liste.AddItem(preis > 0 ? $"{namen[i]} ({Zahlenformat.Ganz(preis)} {Waehrung(w)} je Fahrt)" : namen[i]);
            if (!w.Wache.EskorteVerfuegbar(namen[i])) { liste.SetItemDisabled(i, true); liste.SetItemTooltip(i, $"Die {namen[i]} gibt es nur mit einem Wachhaus mit Leuten."); }
        }
        var vorbelegt = _eskorte ?? wahl.FirstOrDefault()?.Karren.Eskorte ?? namen.FirstOrDefault();
        liste.Select(Math.Max(0, namen.IndexOf(vorbelegt!)));
        liste.ItemSelected += i => _eskorte = namen[(int)i];
        zeile.AddChild(liste);
    }

    private string? EskorteWahl(Stadtwirtschaft w, List<Eintrag> wahl) => _eskorte ?? wahl.FirstOrDefault()?.Karren.Eskorte;

    private void Knoepfe(Stadtwirtschaft w, List<Eintrag> wahl)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 10);
        Inhalt.AddChild(zeile);
        var gruppen = wahl.GroupBy(e => e.Heimat).ToList();
        var eskorte = EskorteWahl(w, wahl);
        string? Grund(Func<IGrouping<Betrieb, Eintrag>, string?> pruefung) =>
            wahl.Count == 0 ? "Erst einen oder mehrere Karren ankreuzen." : gruppen.Select(pruefung).FirstOrDefault(g => g is not null);

        var losGrund = Grund(g => w.KarrenLosschickenVerboten(Spieler!, g.Key, g.Select(e => e.Nr).ToList(), _ziel, eskorte));
        var ziel = _ziel ?? "den Markt";
        Knopf(zeile, "Losschicken", $"Die gewählten Karren fahren zu {ziel} (mit der gewählten Eskorte).", () =>
        {
            foreach (var g in gruppen) Befehlen(new KarrenLosschicken(g.Key.Name, g.Select(e => e.Nr).ToList(), _ziel, eskorte));
            _gewaehlt.Clear();
        }, losGrund);
        var zurueckGrund = Grund(g => w.KarrenLosschickenVerboten(Spieler!, g.Key, g.Select(e => e.Nr).ToList(), g.Key.Name, eskorte));
        Knopf(zeile, "Zurück zum Heimatbetrieb", "Die gewählten Karren fahren zu dem Betrieb, zu dem sie gehören.", () =>
        {
            foreach (var g in gruppen) Befehlen(new KarrenZurueck(g.Key.Name, g.Select(e => e.Nr).ToList(), eskorte));
            _gewaehlt.Clear();
        }, zurueckGrund);
    }
}
