using Reichsgilde;

/// <summary>Welche Knöpfe die Kurzübersicht zeigt (Fehlerpaket 1, Punkt 8): dieselben Regeln wie vorher im Untermenü „…“.</summary>
static class BetriebsKnoepfeTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Betriebsknöpfe: Werkstatt mit Ausbau zeigt Ausbau und Verkaufen; auf höchster Stufe nur Verkaufen", () =>
        {
            Pruefe.Wahr(BetriebsKnoepfe.Ausbau(true, true, true), "Werkstatt mit Ausbau");
            Pruefe.Wahr(!BetriebsKnoepfe.Ausbau(true, true, false), "höchste Stufe: kein Ausbau");
            Pruefe.Wahr(BetriebsKnoepfe.Verkauf(), "Verkaufen immer");
        }),

        ("Betriebsknöpfe: Ausbau-Knopf zeigt nur den Betrag aus den Daten, der Stufenname steht im Tooltip (Gegenprobe)", () =>
        {
            var d = Reichsgilde.Core.Wirtschaft.Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
            var waehrung = d.Allgemein.Waehrung;
            var ausbauten = d.Betriebsarten.Values.Where(a => a.Ausbau is not null).ToList();
            Pruefe.Wahr(ausbauten.Count > 0, "es gibt Betriebsarten mit Ausbau");
            foreach (var art in ausbauten)
            {
                var ziel = d.Art(art.Ausbau!.Value).Name;
                var text = BetriebsKnoepfe.AusbauText(Reichsgilde.Core.Zahlenformat.Ganz(art.AusbauKosten), waehrung);
                Pruefe.Gleich($"Ausbau ({Reichsgilde.Core.Zahlenformat.Ganz(art.AusbauKosten)} {waehrung})", text);
                Pruefe.Wahr(!text.Contains(ziel), $"Stufenname „{ziel}“ nicht im Knopftext: {text}");
                Pruefe.Wahr(BetriebsKnoepfe.AusbauTipp(ziel).Contains(ziel), "Stufenname im Tooltip");
            }
        }),

        ("Betriebsknöpfe: Gegenprobe – Umlandbetrieb (nicht in der Stadt) und Daten ohne Meistergrade bekommen keinen Ausbau, wie vorher im Untermenü", () =>
        {
            Pruefe.Wahr(!BetriebsKnoepfe.Ausbau(false, true, true), "Umland: kein Ausbau");
            Pruefe.Wahr(!BetriebsKnoepfe.Ausbau(true, false, true), "ohne Meistergrade: kein Ausbau");
            Pruefe.Wahr(BetriebsKnoepfe.Verkauf(), "Umland: Pacht kündigen bleibt");
        }),
    };
}
