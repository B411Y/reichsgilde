using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Produktion (Amboss und Werkbank, PR #152): alle Waren der Werkstatt mit Zutaten und Zeit je Stück, die zugeteilten Arbeiter, der
/// Fortschritt des angefangenen Stücks und der Grund eines Stillstands. Arbeiter lassen sich einer Ware zuteilen – die im Raum
/// ausgewählten (Klick oder Rahmen) mit einem Knopf, jeder einzelne über die Liste unten.
/// </summary>
public sealed class ProduktionAnzeige : InnenTafel
{
    public ProduktionAnzeige() : base(900) { }

    protected override string Stand(Stadtwirtschaft w, Betrieb b)
    {
        var s = w.Werkstaetten;
        return $"{b.Produkt}|{string.Join(",", Auswahl())}|" + string.Join(";", s.WarenInArbeit(b).Select(x => $"{x.Ware}:{x.Arbeiter}:{(int)(x.Fortschritt * 20)}:{x.Stillstand}"))
             + "|" + string.Join(";", s.Arbeiterliste(b).Select(z => $"{z.Nummer}:{z.Arbeitskraft}:{z.Ware}:{z.Zugeteilt}"));
    }

    protected override void Aufbauen(Stadtwirtschaft w, Betrieb b)
    {
        var s = w.Werkstaetten;
        var gewaehlt = Auswahl().OrderBy(n => n).ToList();
        var arbeiter = s.Arbeiterliste(b);
        var inArbeit = s.WarenInArbeit(b).ToDictionary(x => x.Ware);
        Kopf("Produktion", b, "Die Hauptware stellt die Werkstatt her, solange kein Arbeiter etwas anderes zugeteilt bekommt.");

        Text(Inhalt, gewaehlt.Count == 0
            ? "Im Raum ist kein Arbeiter ausgewählt (anklicken oder mit gedrückter linker Taste einen Rahmen aufziehen)."
            : "Im Raum ausgewählt: " + string.Join(", ", gewaehlt.Select(n => arbeiter.FirstOrDefault(z => z.Nummer == n) is { } z ? $"{z.Arbeitskraft} Nr. {n}" : $"Nr. {n}")),
            13, gewaehlt.Count == 0 ? Grau : Gold, 860);

        var raster = Raster(8);
        var kopfTipps = new[] { "", "Ware, die diese Werkstatt herstellt (★ = Hauptware).", "Was für ein Stück aus dem Lager gebraucht wird.", "Arbeitsminuten für ein Stück bei Grundtempo; Meistergrad, Einrichtung und Laune ändern das.", "Arbeiter, die dieser Ware zugeteilt sind.", "Fortschritt des angefangenen Stücks; rot, wenn etwas fehlt.", "", "" };
        var kopfNr = 0;
        foreach (var kopf in new[] { "", "Ware", "Zutaten je Stück", "Zeit je Stück", "Arbeiter", "Stück in Arbeit", "", "" })
            { var z = Zelle(raster, kopf, 12, Grau); if (kopfTipps[kopfNr] != "") Tipp(z, kopfTipps[kopfNr]); kopfNr++; }
        foreach (var ware in b.Artdaten.Produkte)
        {
            var daten = w.Daten.Waren[ware];
            var hauptware = ware == b.Produkt;
            var ab = raster.GetChildCount();
            raster.AddChild(WarenIcons.Bild(ware, 32));
            Zelle(raster, ware + (hauptware ? "  ★" : ""), 15, hauptware ? Gold : Hell, 110);
            Zelle(raster, string.Join(", ", daten.Zutaten.Select(z => $"{z.Value} {z.Key}")), 13, Hell, 190);
            Zelle(raster, $"{Zahlenformat.Kommazahl(b.MinutenJeStueck(ware))} Min.", 13, Hell, 66, rechts: true);
            var daran = arbeiter.Where(z => z.Ware == ware).Select(z => $"Nr. {z.Nummer}").ToList();
            Zelle(raster, daran.Count == 0 ? "–" : string.Join(", ", daran), 13, daran.Count == 0 ? Grau : Hell, 80);
            var spalte = new VBoxContainer();
            spalte.AddThemeConstantOverride("separation", 1);
            raster.AddChild(spalte);
            if (inArbeit.TryGetValue(ware, out var arbeit))
            {
                Balken(spalte, arbeit.Fortschritt, 140, arbeit.Stillstand is null ? Gold : Rot);
                if (arbeit.Stillstand is { } still) Text(spalte, still, 11, Rot, 160);
            }
            else Text(spalte, "ruht", 12, Grau);
            ZeileTipp(raster, ab, WarenTipp(w, b, ware) + (hauptware ? "\nHauptware: Die Werkstatt stellt sie her, solange kein Arbeiter etwas anderes zugeteilt bekommt." : ""));
            Knopf(raster, gewaehlt.Count > 0 ? $"Zuteilen ({gewaehlt.Count})" : "Zuteilen", () =>
            {
                foreach (var nummer in gewaehlt) Befehlen(new ArbeiterZuteilen(b.Name, nummer, ware));
            }, gewaehlt.Count == 0 ? "Wählt erst im Raum Arbeiter aus." : null);
            Knopf(raster, "Hauptware", () => Befehlen(new ProduktWaehlen(b.Name, ware)), hauptware ? $"{ware} ist schon die Hauptware." : null);
        }
        if (gewaehlt.Count > 0)
            Knopf(Inhalt, "Ausgewählte zurück an die Hauptware", () =>
            {
                foreach (var nummer in gewaehlt) Befehlen(new ArbeiterZuteilen(b.Name, nummer, null));
            });

        Inhalt.AddChild(new HSeparator());
        Text(Inhalt, "Arbeiter einzeln", 16, Gold);
        if (arbeiter.Count == 0) Text(Inhalt, "Es arbeitet niemand hier.", 13, Grau);
        var liste = Raster(2, streifen: false);
        foreach (var z in arbeiter)
        {
            Tipp(Zelle(liste, (gewaehlt.Contains(z.Nummer) ? "» " : "") + $"{z.Arbeitskraft} Nr. {z.Nummer}", 14, gewaehlt.Contains(z.Nummer) ? Gold : Hell, 150),
                $"{z.Arbeitskraft} Nr. {z.Nummer}\nArbeitet an: {z.Ware}{(z.Zugeteilt ? "" : " (Hauptware des Betriebs)")}\nLaune {Zahlenformat.Ganz(z.Laune)} · Tempo {Zahlenformat.Ganz(z.TempoFaktor * 100)} %");
            
            var wahl = new OptionButton { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(260, 0) };
            wahl.AddThemeFontSizeOverride("font_size", 13);
            wahl.AddItem($"Hauptware ({b.Produkt})");
            foreach (var ware in b.Artdaten.Produkte) wahl.AddItem(ware);
            wahl.Select(z.Zugeteilt ? 1 + b.Artdaten.Produkte.ToList().IndexOf(z.Ware) : 0);
            Tipp(wahl, "Wählt, welche Ware dieser Arbeiter herstellt.");
            wahl.ItemSelected += index => Befehlen(new ArbeiterZuteilen(b.Name, z.Nummer, index == 0 ? null : b.Artdaten.Produkte[(int)index - 1]));
            liste.AddChild(wahl);
        }
    }
}
