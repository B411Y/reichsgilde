using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Einrichtung (Werkzeugwand, PR #152): alle Stücke, die eine Werkstatt dieses Berufs haben kann, mit Name, Preis, Wirkung, Stufe und
/// Voraussetzung. Gekaufte Stücke stehen sofort im Raum; wo man nicht kaufen kann, nennt die Tafel den Grund aus dem Kern
/// (<see cref="Werkstattwesen.EinrichtungVerboten"/>).
/// </summary>
public sealed class EinrichtenAnzeige : InnenTafel
{
    public EinrichtenAnzeige() : base(820) { }

    protected override string Stand(Stadtwirtschaft w, Betrieb b) =>
        string.Join(";", w.Werkstaetten.Einrichtungsliste(b).Select(e => $"{e.Kennung}:{e.Gekauft}:{e.Verboten}"));

    protected override void Aufbauen(Stadtwirtschaft w, Betrieb b)
    {
        var liste = w.Werkstaetten.Einrichtungsliste(b);
        var waehrung = w.Daten.Allgemein.Waehrung;
        Kopf("Einrichtung", b, $"In der Kasse: {Zahlenformat.Ganz(b.Konto.Vermoegen)} {waehrung}");
        if (liste.Count == 0) Text(Inhalt, "Für diese Werkstatt gibt es nichts zu kaufen.", 14, Grau);
        var raster = Raster(5);
        foreach (var kopf in new[] { "Stück", "Wirkung", "Ab Stufe", "Preis", "" })
            Zelle(raster, kopf, 12, Grau);
        foreach (var e in liste)
        {
            var ab = raster.GetChildCount();
            Zelle(raster, e.Name, 15, e.Gekauft ? Gruen : Gold, 150);
            var wirkung = new VBoxContainer();
            wirkung.AddThemeConstantOverride("separation", 0);
            raster.AddChild(wirkung);
            Text(wirkung, e.Wirkung, 13, Hell, 270);
            if (e.Voraussetzung is { } v)
                Text(wirkung, "Braucht: " + (liste.FirstOrDefault(x => x.Kennung == v)?.Name ?? v), 11, Grau);
            if (!e.Gekauft && e.Verboten is { } grund) Text(wirkung, grund, 11, Rot, 270);
            Zelle(raster, e.AbStufe.ToString(), 14, b.Stufe >= e.AbStufe ? Hell : Rot, 60, rechts: true);
            Zelle(raster, $"{Zahlenformat.Ganz(e.Preis)} {waehrung}", 14, Hell, 90, rechts: true);
            if (e.Gekauft) Zelle(raster, "gekauft", 14, Gruen, 90);
            else Knopf(raster, "Kaufen", () => Befehlen(new EinrichtungKaufen(b.Name, e.Kennung)), e.Verboten);
            ZeileTipp(raster, ab, $"{e.Name}\n{e.Wirkung}\nPreis {Zahlenformat.Ganz(e.Preis)} {waehrung} · ab Stufe {e.AbStufe}"
                + (e.Voraussetzung is { } vor ? "\nBraucht: " + (liste.FirstOrDefault(x => x.Kennung == vor)?.Name ?? vor) : "")
                + (e.Gekauft ? "\nSchon gekauft." : e.Verboten is { } g ? "\nGesperrt: " + g : ""));
        }
        Text(Inhalt, "Gekaufte Stücke stehen sofort im Raum und wirken auf Tempo, Lager, Unfälle, Brand oder Einbruch.", 12, Grau, 780);
    }
}
