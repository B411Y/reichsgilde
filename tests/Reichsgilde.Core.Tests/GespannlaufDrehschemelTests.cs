using Reichsgilde;

/// <summary>
/// Drehschemel und Tiere an der Deichsel (Darstellung, 23.11.2026; <c>game/scripts/Gespannlauf.cs</c>): Der Winkel zwischen Vorderwagen und Kasten bleibt in engen Kurven
/// unter der Grenze (die Vorderräder laufen sonst in den Kasten), und die Tiere, die in Richtung der Deichsel gehen, rutschen nicht über den halben Weg seitwärts.
/// Gemessen wird eine 90°-Kurve mit Halbmesser 3, 4 und 6 m zwischen zwei Geraden (Großer Pferdekarren: Deichsel 4,27 m, Radstand 2,20 m).
/// </summary>
static class GespannlaufDrehschemelTests
{
    static void Fehler(string s) => throw new Exception(s);

    /// <summary>Wegpunkte: 20 m geradeaus, 90°-Bogen (Halbmesser r), 20 m geradeaus; alle 5 cm ein Punkt.</summary>
    static List<(double X, double Z)> Kurve(double r, bool links = true)
    {
        var p = new List<(double X, double Z)>();
        for (var z = 0.0; z < 20; z += 0.05) p.Add((0, z));
        var n = (int)(Math.PI / 2 * r / 0.05);
        for (var i = 0; i <= n; i++)
        {
            var w = i * 0.05 / r;
            p.Add((r * (1 - Math.Cos(w)) * (links ? 1 : -1), 20 + r * Math.Sin(w)));
        }
        var (x1, z1) = p[^1];
        for (var s = 0.05; s <= 20; s += 0.05) p.Add((x1 + s * (links ? 1 : -1), z1));
        return p;
    }

    /// <summary>Fährt den Weg mit 1,3 m/s und 60 Bildern; liefert die größten Winkel und den Anteil des Weges, auf dem das Tier mehr als <paramref name="rutschGrad"/> Grad gegen die Deichsel steht.</summary>
    static (double MaxKnick, double MaxWagen, double RutschAnteil, double RutschAnteilKurve) Fahren(Gespannlauf g, List<(double X, double Z)> weg, double rutschGrad)
    {
        var (p0, p1) = (weg[0], weg[1]);
        g.Setzen(p0.X, p0.Z, p1.X - p0.X, p1.Z - p0.Z);
        var (maxK, maxW, rutsch, kurve, kurveRutsch) = (0.0, 0.0, 0.0, 0.0, 0.0);
        var dt = 1.0 / 60; var tempo = 1.3;
        for (var s = 0.0; s < (weg.Count - 21) * 0.05; s += tempo * dt)
        {
            var i = (int)(s / 0.05);
            var j = Math.Min(i + 20, weg.Count - 1);
            g.Weiter(weg[i].X, weg[i].Z, weg[j].X - weg[i].X, weg[j].Z - weg[i].Z, tempo * dt * 2);
            maxK = Math.Max(maxK, Math.Abs(g.Knick));
            maxW = Math.Max(maxW, Math.Abs(g.WagenKnick));
            var gedreht = Math.Abs(g.Knick) > rutschGrad * Math.PI / 180;
            if (gedreht) rutsch += tempo * dt;
            if (weg[i].Z > 19 && (weg[i].Z < 21 || Math.Abs(weg[i].X) > 0.01))
            {
                kurve += tempo * dt;
                if (gedreht) kurveRutsch += tempo * dt;
            }
        }
        var gesamt = (weg.Count - 21) * 0.05;
        return (maxK * 180 / Math.PI, maxW * 180 / Math.PI, rutsch / gesamt, kurve > 0 ? kurveRutsch / kurve : 0);
    }

    public static IEnumerable<(string, Action)> Alle()
    {
        yield return ("Gespannlauf Drehschemel: Winkel Vorderwagen gegen Kasten bleibt in engen Kurven unter 40 Grad", () =>
        {
            foreach (var r in new[] { 3.0, 4.0, 6.0 })
                foreach (var links in new[] { true, false })
                {
                    var g = new Gespannlauf(4.27, 2.2, 30, 3.0, 40);
                    var erg = Fahren(g, Kurve(r, links), 15);
                    if (erg.MaxWagen > 40 + 1e-6) Fehler($"Halbmesser {r}: Vorderwagen gegen Kasten {erg.MaxWagen:0.0} Grad (Grenze 40)");
                    if (Math.Abs(Math.Sqrt(Math.Pow(g.VorneX - g.HintenX, 2) + Math.Pow(g.VorneZ - g.HintenZ, 2)) - 2.2) > 1e-6) Fehler("Radstand schwankt");
                }
        });
        yield return ("Gespannlauf Drehschemel: ohne Grenze knickt der Vorderwagen in der engen Kurve über 45 Grad (Anlass der Grenze)", () =>
        {
            var erg = Fahren(new Gespannlauf(4.27, 2.2, 30, 3.0), Kurve(3.0), 15);
            if (erg.MaxWagen < 45) Fehler($"Ohne Grenze nur {erg.MaxWagen:0.0} Grad: Test der Grenze zeigt nichts mehr");
        });
        yield return ("Gespannlauf Drehschemel: Tiere an der Deichsel rutschen höchstens auf dem halben Kurvenweg über 15 Grad seitwärts", () =>
        {
            foreach (var r in new[] { 3.0, 4.0, 6.0 })
            {
                var erg = Fahren(new Gespannlauf(4.27, 2.2, 30, 3.0, 40), Kurve(r), 15);
                var ein = Fahren(new Gespannlauf(4.19, 0, 30, 3.0), Kurve(r), 15);
                Console.WriteLine($"[drehschemel] Halbmesser {r}: Gross Knick max {erg.MaxKnick:0.0}, Wagen max {erg.MaxWagen:0.0}, Rutsch>15 Grad {erg.RutschAnteilKurve * 100:0} % der Kurve; Ein-Pferd Knick max {ein.MaxKnick:0.0}, Rutsch {ein.RutschAnteilKurve * 100:0} %");
                if (erg.RutschAnteilKurve > 0.5) Fehler($"Gross, Halbmesser {r}: {erg.RutschAnteilKurve * 100:0} % der Kurve über 15 Grad");
                if (ein.RutschAnteilKurve > 0.5) Fehler($"Ein-Pferd, Halbmesser {r}: {ein.RutschAnteilKurve * 100:0} % der Kurve über 15 Grad");
            }
        });
    }
}
