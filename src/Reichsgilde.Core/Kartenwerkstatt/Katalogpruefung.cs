using Reichsgilde.Core.Bauen;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Den Gebäudekatalog (game/daten/katalog.json) gegen die Modelle prüfen (game/assets/modelle/) – bis Schritt 4 tools/karten/katalog_pruefen.py
//
//  Fehler:
//    • die Modelldatei aus `modell` fehlt,
//    • ein `knoten` fehlt in der Datei, bei Natur auch eine Variante (`Tanne1` … `Tanne3` bei `varianten: 3`).
//  Hinweise: Die Grundfläche passt nicht zur Größe des Modells –
//    • das Modell ragt mehr als 0,5 m über die Fläche (gemessen von der Mitte der Fläche, also mit `versatz`),
//    • oder es füllt sie in beiden Richtungen nicht einmal zur Hälfte.
//  Die Ausdehnung zählt alle Netze der Datei (bzw. des Knotens samt Kindern) mit ihrer Lage, auch Zustände wie Leerstand oder Gerüst.
//
//  Bekannt und gewollt (Entscheidung des Kerns, PR #163): Die Umlandbetriebe sind als Modell größer als ihre Fläche im Katalog – die Fläche
//  bleibt, sie ist die belegte Fläche für das Bauen. Ihre Hinweise sind deshalb erwartet.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Die Katalogprüfung (siehe Kopf der Datei). Hängt an keiner Karte; das Kartenskript ruft sie bei <c>pruefen</c> mit auf.</summary>
public static class Katalogpruefung
{
    /// <summary>So viele Meter darf ein Modell über seine Fläche ragen, ohne dass ein Hinweis kommt.</summary>
    public const double Ueberstand = 0.5;

    /// <summary>Diesen Anteil der Fläche soll ein Modell in wenigstens einer Richtung füllen.</summary>
    public const double Mindestens = 0.5;

    /// <summary>
    /// Prüft jeden Baustein mit Modell gegen seine Datei unter <paramref name="modellordner"/> (<c>&lt;modell&gt;.glb</c>). Die Befunde nennen den
    /// Baustein in <see cref="Befund.Gebaeude"/> (seine Kennung im Katalog).
    /// </summary>
    public static List<Befund> Pruefen(Gebaeudekatalog katalog, string modellordner)
    {
        var befunde = new List<Befund>();
        var dateien = new Dictionary<string, Modelldatei?>();
        Modelldatei? Datei(string modell)
        {
            if (dateien.TryGetValue(modell, out var d)) return d;
            var pfad = Path.Combine(modellordner, modell + ".glb");
            return dateien[modell] = File.Exists(pfad) ? Modelldatei.Lesen(pfad) : null;
        }
        void Melde(Schwere s, Baustein b, string text) => befunde.Add(new Befund(s, "katalog-modell", text, new[] { b.Kennung }, null));

        foreach (var b in katalog.Bausteine)
        {
            if (b.Modell is not { } modell) continue;
            if (Datei(modell) is not { } datei)
            {
                Melde(Schwere.Fehler, b, $"{b.Kennung}: Die Modelldatei {modell}.glb fehlt.");
                continue;
            }
            var namen = new List<string>();
            if (b.Knoten is { } knoten)
            {
                // Natur und Bausteine mit mehreren Varianten (die Furt) heißen „Knoten1“ … „KnotenN“
                namen = b.Art == Bausteinart.Natur || b.Varianten > 1 ? Enumerable.Range(1, b.Varianten).Select(v => knoten + v).ToList() : new List<string> { knoten };
                foreach (var n in namen.Where(n => !datei.HatKnoten(n)))
                    Melde(Schwere.Fehler, b, $"{b.Kennung}: Der Knoten „{n}“ fehlt in {modell}.glb.");
            }
            if (b.Groesse is not { Length: 2 } g) continue;
            var aus = (namen.Count > 0 ? namen.Select(n => datei.Ausdehnung(n)) : new[] { datei.Ausdehnung() }).Where(a => a is not null).Select(a => a!.Value).ToList();
            if (aus.Count == 0) continue;
            var (x0, z0, x1, z1) = (aus.Min(a => a.X0), aus.Min(a => a.Z0), aus.Max(a => a.X1), aus.Max(a => a.Z1));
            var (w, t) = (g[0], g[1]);
            // Mitte der Fläche in Koordinaten des Modells (der Ursprung liegt `versatz` neben ihr)
            var (mx, mz) = b.Versatz is { Length: 2 } v ? (-v[0], -v[1]) : (0.0, 0.0);
            var ueber = new[] { x1 - (mx + w / 2), (mx - w / 2) - x0, z1 - (mz + t / 2), (mz - t / 2) - z0 }.Max();
            var text = $"{b.Kennung}: Fläche {M(w)} × {M(t)} m, Modell {modell}{(b.Knoten is null ? "" : $" ({b.Knoten})")} " +
                       $"x {M(x0)} … {M(x1)}, z {M(z0)} … {M(z1)} ({M(x1 - x0)} × {M(z1 - z0)} m)";
            if (ueber > Ueberstand)
                Melde(Schwere.Hinweis, b, $"{text} – ragt bis {M(ueber)} m über die Fläche" + (b.Art == Bausteinart.Umlandbetrieb ? " (Umland: bekannt, Fläche bleibt)." : "."));
            else if (x1 - x0 < Mindestens * w && z1 - z0 < Mindestens * t)
                Melde(Schwere.Hinweis, b, $"{text} – füllt die Fläche nicht einmal zur Hälfte.");
        }
        return befunde;
    }

    private static string M(double x) => Kartenschreiber.Zahl(Math.Round(x, 1)).Replace('.', ',');
}
