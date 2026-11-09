using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

/// <summary>
/// Die Gruppe „Große Wand“ trägt seit 09.11.2026 (Nutzer; seit Felsen R7 haben die Wände eine geschlossene Rückseite) zwei mittlere Wände als Stufe
/// vor den großen. Sie stehen so weit davor, dass „Hang mitformen“ zwischen ihnen und den großen keinen Wall aufschüttet (Felshang.Vorgelagert).
/// </summary>
static class FelsgruppeWandTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenentwurf Leer() => Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);

    /// <summary>Ein Ort zwischen einer mittleren Wand und den großen dahinter: 1 m vor ihrer Rückseite, in ihren eigenen Achsen.</summary>
    static Kartenort HinterDerMittleren(EntwurfFels f)
    {
        var form = Felsformen.Finde(f.Form)!;
        var o = Kartengeometrie.Dreh(0, form.Rueckwand * f.Massstab * f.AchseZ + 1.0, f.Drehung);
        return new Kartenort(f.X + o.X, f.Z + o.Z);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Fels: Die Große Wand trägt zwei mittlere Wände vor den großen; „Hang mitformen“ schüttet zwischen ihnen keinen Wall auf", () =>
        {
            var g = Felsformen.Gruppe("wandgross")!;
            var formen = g.Stuecke.Select(s => s.Form).ToList();
            Pruefe.Gleich(3, formen.Count(f => f.StartsWith("wand_gross_", StringComparison.Ordinal)), "drei große Wände");
            Pruefe.Gleich(2, formen.Count(f => f.StartsWith("wand_mittel_", StringComparison.Ordinal)), "zwei mittlere Wände");
            Pruefe.Gleich("wand_gross_1", formen[0], "die Mitte der Gruppe bleibt die große Wand");
            // Jede mittlere Wand steht vor (größeres Z: hangabwärts) einer großen, die sie in der Breite deckt
            foreach (var m in g.Stuecke.Where(s => s.Form.StartsWith("wand_mittel_", StringComparison.Ordinal)))
                Pruefe.Wahr(g.Stuecke.Any(s => s.Form.StartsWith("wand_gross_", StringComparison.Ordinal) && m.Z - s.Z > 2.5
                    && Math.Abs(m.X - s.X) < Felsformen.Finde(s.Form)!.Breite * s.Massstab / 2), $"{m.Form} steht vor einer großen Wand");

            foreach (var startwert in new[] { 1, 5, 9, 13 })
            {
                var e = Leer();
                var neue = e.FelsStempeln(new Felspinsel(Gruppe: "wandgross", Streuung: 0, Boden: false, HangFormen: true), new Kartenort(0, 0),
                    drehung: 0, startwert: startwert);
                Pruefe.Gleich(g.Stuecke.Count, neue.Count, "alle Stücke gesetzt");
                foreach (var m in neue.Where(f => f.Form.StartsWith("wand_mittel_", StringComparison.Ordinal)))
                {
                    var p = HinterDerMittleren(m);
                    var h = e.Hoehen.Hoehe(p.X, p.Z);
                    Pruefe.Wahr(h < 0.3, $"Startwert {startwert}: hinter {m.Form} kein Wall ({h:0.00} m bei {p.X:0.0}, {p.Z:0.0})");
                    // Gegenprobe: allein gestempelt hebt dieselbe Wand dort den Hang an
                    var allein = Leer();
                    var f = allein.FelsStempeln(new Felspinsel(Formen: new[] { m.Form }, Streuung: 0, Boden: false, HangFormen: true),
                        new Kartenort(m.X, m.Z), drehung: m.Drehung, startwert: startwert)[0];
                    var q = HinterDerMittleren(f);
                    Pruefe.Wahr(allein.Hoehen.Hoehe(q.X, q.Z) > 1.5, $"{m.Form} allein hebt den Hang ({allein.Hoehen.Hoehe(q.X, q.Z):0.00} m)");
                }
                // hinter den großen Wänden steigt der Hang wie bisher
                var mitte = neue[0];
                Pruefe.Wahr(e.Hoehen.Hoehe(mitte.X, mitte.Z - 5.2 * mitte.Massstab) > 8, "hinter der großen Wand steigt der Hang");
            }
        }),
    };
}
