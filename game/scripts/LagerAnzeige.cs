using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das Lager (Regal und Kisten, PR #152): Bestand und Kapazität je Ware und der Schalter „Zurückhalten“ – eine zurückgehaltene Ware verkauft
/// der Karren nicht von selbst (Befehl <see cref="WareZurueckhalten"/>).
/// </summary>
public sealed class LagerAnzeige : InnenTafel
{
    public LagerAnzeige() : base(700) { }

    private static List<string> Waren(Betrieb b) =>
        b.Zutaten.Keys.Concat(b.Artdaten.Produkte).Append(b.Produkt).Concat(b.Lager.Keys).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();

    protected override string Stand(Stadtwirtschaft w, Betrieb b) =>
        b.LagerKapazitaetJeWare + "|" + b.HatEinrichtung("Regale") + "|" + string.Join(";", Waren(b).Select(x => $"{x}:{b.ImLager(x)}:{b.HaeltZurueck(x)}"));

    protected override void Aufbauen(Stadtwirtschaft w, Betrieb b)
    {
        if (w.Wache.IstWachbetrieb(b))
        {
            WaffenkammerAufbauen(w, b);
            return;
        }
        var zutaten = b.Zutaten.Keys.ToHashSet();
        Kopf("Lager", b, $"Je Ware passen {b.LagerKapazitaetJeWare} Stück ins Lager" + (b.HatEinrichtung("Regale") ? " (mit Regalen)." : ". Regale an der Werkzeugwand schaffen mehr Platz."));
        var raster = Raster(6);
        foreach (var kopf in new[] { "", "Ware", "Art", "Bestand", "", "Zurückhalten" })
            Zelle(raster, kopf, 12, Grau);
        foreach (var ware in Waren(b))
        {
            var menge = b.ImLager(ware);
            var voll = menge >= b.LagerKapazitaetJeWare;
            raster.AddChild(WarenIcons.Bild(ware, 32));
            Zelle(raster, ware, 15, Hell, 120);
            Zelle(raster, zutaten.Contains(ware) ? "Zutat" : b.Artdaten.Produkte.Contains(ware) ? (ware == b.Produkt ? "Hauptware" : "Ware") : "Lagerware", 12, Grau, 80);
            Zelle(raster, $"{menge} / {b.LagerKapazitaetJeWare}", 14, voll ? Rot : Hell, 80, rechts: true);
            Balken(raster, menge / (double)Math.Max(1, b.LagerKapazitaetJeWare), 120, voll ? Rot : Gold);
            var halten = new CheckBox { Text = "zurückhalten", FocusMode = Control.FocusModeEnum.None, ButtonPressed = b.HaeltZurueck(ware),
                                        TooltipText = "Der Karren verkauft diese Ware nicht von selbst." };
            halten.Toggled += an => Befehlen(new WareZurueckhalten(b.Name, ware, an));
            raster.AddChild(halten);
        }
        if (Waren(b).Count == 0) Text(Inhalt, "Das Lager ist leer.", 14, Grau);
        Text(Inhalt, "Ist das Lager einer Ware voll, ruht die Arbeit an ihr, bis der Karren etwas verkauft hat.", 12, Grau, 660);
    }

    /// <summary>Die Waffenkammer der Wache: keine Waren, nur die Einrichtung „Waffenkammer“ (Regale) mit ihrer Wirkung auf den Lohn der Wachleute.</summary>
    private void WaffenkammerAufbauen(Stadtwirtschaft w, Betrieb b)
    {
        Kopf("Waffenkammer", b, "Hellebarden, Rüstungen und Schilde stellt die Stadt – hier lagert keine Ware.");
        var regale = w.Werkstaetten.Einrichtungsliste(b).FirstOrDefault(e => e.Kennung == "Regale");
        if (regale is null) return;
        Text(Inhalt, regale.Gekauft ? $"{regale.Name} (gekauft): {regale.Wirkung}" : $"{regale.Name} ({Zahlenformat.Ganz(regale.Preis)} {w.Daten.Allgemein.Waehrung}, an der Waffenwand unter „Einrichtung“): {regale.Wirkung}",
            14, regale.Gekauft ? Gruen : Hell, 660);
    }
}
