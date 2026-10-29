using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Der Belag der Wege (Kartenwerkstatt, Schritt 5d) – Stufen, Startbelag der Karte, Daten
//
//  Vier Stufen: Trampelpfad, Fahrweg (mit Spurrillen), Kies, Pflaster. In der Karte steht je Weg, wo er einen anderen Belag hat als die
//  Vorgabe seiner Klasse (Feld „belag“: Abschnitte in Metern entlang des Verlaufs). Im Spiel wird jeder Weg in Stücke zu 5 m geteilt
//  (Wegebau): Jedes Stück hat seine Stufe, seinen Austritt (Trampelpfad und Fahrweg) bzw. Zustand (Kies und Pflaster).
//  Die Werte (Tempo, Austritt, Verfall, Preise, Stadtrat) stehen in wirtschaft.json, Abschnitt „wegebau“ (ab Datenversion 52).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Der Belag eines Wegstücks, vom schlechtesten zum besten. In der Datei kleingeschrieben („pflaster“).</summary>
public enum Belagstufe
{
    /// <summary>Unbefestigt und schmal ausgetreten – Karren fahren hier am langsamsten.</summary>
    Trampelpfad,

    /// <summary>Unbefestigt, aber von Rädern und Hufen zu einer Fahrbahn mit Spurrillen ausgefahren.</summary>
    Fahrweg,

    /// <summary>Mit Kies geschottert (Bauauftrag).</summary>
    Kies,

    /// <summary>Mit Steinen gepflastert (Bauauftrag).</summary>
    Pflaster,
}

/// <summary>
/// Ein Abschnitt eines Weges in der Karte mit eigenem Belag: von <see cref="Von"/> bis <see cref="Bis"/> Meter entlang des Verlaufs (ohne
/// <see cref="Bis"/>: bis zum Ende). <see cref="Zustand"/> nur bei Kies und Pflaster (1 = neu; ohne Angabe 1). Spätere Abschnitte
/// gehen früheren vor.
/// </summary>
public sealed class Belagstelle
{
    public double Von { get; init; }
    public double? Bis { get; init; }
    public Belagstufe Stufe { get; init; }
    public double? Zustand { get; init; }
}

/// <summary>Ein Abschnitt eines Weges mit gleichem Belag (für Darstellung und Kartenwerkstatt).</summary>
/// <param name="Von">Meter entlang des Verlaufs.</param>
/// <param name="Bis">Meter entlang des Verlaufs.</param>
/// <param name="Austritt">Trampelpfad und Fahrweg: 0 = frisch zugewachsen … 1 = tief ausgefahren (Anteil der Spurrillen); Kies und Pflaster 1.</param>
/// <param name="Zustand">Kies und Pflaster: 1 = neu … 0 = verfallen; Trampelpfad und Fahrweg 1.</param>
/// <param name="Verkehr">Fahrten im Jahr (Karren im Vorjahr, dazu Fußgänger und Fernverkehr umgerechnet; im Mittel des Abschnitts).</param>
/// <param name="Tempo">So schnell fahren Karren hier (1 = Pflaster neu; aus <c>wegebau.tempo</c>), ohne Daten 1.</param>
/// <param name="Baustelle">Wird hier gerade gebaut?</param>
public readonly record struct Belagabschnitt(double Von, double Bis, Belagstufe Stufe, double Austritt = 1, double Zustand = 1, double Verkehr = 0,
    double Tempo = 1, bool Baustelle = false)
{
    public double Laenge => Bis - Von;
}

/// <summary>Hilfen für den Belag, die Karte, Kartenwerkstatt und Spiel gemeinsam nutzen.</summary>
public static class Wegbelag
{
    /// <summary>Der Belag eines Weges ohne Angabe in der Karte: Karrenstraßen sind Fahrwege, Gassen und Fußwege Trampelpfade.</summary>
    public static Belagstufe Vorgabe(Wegklasse k) => Kartenweg.TraegtKarren(k) ? Belagstufe.Fahrweg : Belagstufe.Trampelpfad;

    /// <summary>Anzeigename („Fahrweg mit Spurrillen“ heißt hier kurz „Fahrweg“).</summary>
    public static string Anzeigename(Belagstufe s) => s switch
    {
        Belagstufe.Trampelpfad => "Trampelpfad",
        Belagstufe.Fahrweg => "Fahrweg",
        Belagstufe.Kies => "Kies",
        _ => "Pflaster",
    };

    /// <summary>Kies und Pflaster entstehen nur als Bauauftrag (Trampelpfad und Fahrweg tritt der Verkehr aus).</summary>
    public static bool IstBefestigt(Belagstufe s) => s >= Belagstufe.Kies;

    /// <summary>
    /// Der Startbelag eines Weges der Länge <paramref name="laenge"/> als lückenlose Abschnitte von 0 bis zur Länge: die Vorgabe der Klasse,
    /// darüber die Abschnitte der Karte (spätere vor früheren, auf die Länge beschnitten). Gleiche Nachbarn werden zusammengelegt.
    /// </summary>
    public static IReadOnlyList<Belagabschnitt> Startabschnitte(Wegklasse klasse, double laenge, IReadOnlyList<Belagstelle>? stellen)
    {
        // Grenzen sammeln, je Teilstück den obersten Abschnitt nehmen
        var grenzen = new List<double> { 0, laenge };
        foreach (var s in stellen ?? Array.Empty<Belagstelle>())
        {
            grenzen.Add(Math.Clamp(s.Von, 0, laenge));
            grenzen.Add(Math.Clamp(s.Bis ?? laenge, 0, laenge));
        }
        var punkte = grenzen.Distinct().OrderBy(x => x).ToList();
        var liste = new List<Belagabschnitt>();
        for (var i = 0; i + 1 < punkte.Count; i++)
        {
            var (von, bis) = (punkte[i], punkte[i + 1]);
            if (bis - von <= 1e-9) continue;
            var (stufe, zustand) = StufeBei(klasse, laenge, stellen, (von + bis) / 2);
            if (liste.Count > 0 && liste[^1].Stufe == stufe && liste[^1].Zustand == zustand)
                liste[^1] = liste[^1] with { Bis = bis };
            else
                liste.Add(new Belagabschnitt(von, bis, stufe, Austritt: stufe == Belagstufe.Trampelpfad ? 0 : 1, Zustand: zustand));
        }
        if (liste.Count == 0) liste.Add(new Belagabschnitt(0, Math.Max(0, laenge), Vorgabe(klasse), Austritt: Vorgabe(klasse) == Belagstufe.Trampelpfad ? 0 : 1));
        return liste;
    }

    /// <summary>Stufe und Zustand des Startbelags an einer Stelle (Meter entlang des Verlaufs).</summary>
    public static (Belagstufe Stufe, double Zustand) StufeBei(Wegklasse klasse, double laenge, IReadOnlyList<Belagstelle>? stellen, double stelle)
    {
        if (stellen is not null)
            for (var i = stellen.Count - 1; i >= 0; i--)
            {
                var s = stellen[i];
                if (stelle >= s.Von && stelle <= (s.Bis ?? laenge))
                    return (s.Stufe, IstBefestigt(s.Stufe) ? Math.Clamp(s.Zustand ?? 1, 0, 1) : 1);
            }
        return (Vorgabe(klasse), 1);
    }

    /// <summary>
    /// Wo ein Punkt auf einer Linie liegt: die Stelle (Meter vom Anfang entlang der Linie) des nächsten Punktes der Linie und der Abstand dorthin.
    /// </summary>
    public static (double Stelle, double Abstand) Projektion(IReadOnlyList<double[]> linie, double x, double z)
    {
        if (linie.Count == 0) return (0, double.MaxValue);
        if (linie.Count == 1)
        {
            var (ex, ez) = (x - linie[0][0], z - linie[0][1]);
            return (0, Math.Sqrt(ex * ex + ez * ez));
        }
        var (best, stelle, weg) = (double.MaxValue, 0.0, 0.0);
        for (var i = 0; i + 1 < linie.Count; i++)
        {
            var (ax, az, bx, bz) = (linie[i][0], linie[i][1], linie[i + 1][0], linie[i + 1][1]);
            var (dx, dz) = (bx - ax, bz - az);
            var l2 = dx * dx + dz * dz;
            var l = Math.Sqrt(l2);
            var t = l2 == 0 ? 0 : Math.Clamp(((x - ax) * dx + (z - az) * dz) / l2, 0, 1);
            var (rx, rz) = (x - (ax + t * dx), z - (az + t * dz));
            var d = Math.Sqrt(rx * rx + rz * rz);
            if (d < best) (best, stelle) = (d, weg + t * l);
            weg += l;
        }
        return (stelle, best);
    }

    /// <summary>Der Punkt einer Linie an einer Stelle (Meter vom Anfang; außerhalb: das Ende).</summary>
    public static Kartenort Punkt(IReadOnlyList<double[]> linie, double stelle)
    {
        if (linie.Count == 0) return default;
        var weg = 0.0;
        for (var i = 0; i + 1 < linie.Count; i++)
        {
            var (a, b) = (linie[i], linie[i + 1]);
            var (ex, ez) = (b[0] - a[0], b[1] - a[1]);
            var l = Math.Sqrt(ex * ex + ez * ez);
            if (weg + l >= stelle && l > 0)
            {
                var t = Math.Clamp((stelle - weg) / l, 0, 1);
                return new Kartenort(a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1]));
            }
            weg += l;
        }
        return stelle <= 0 ? Kartenort.Aus(linie[0]) : Kartenort.Aus(linie[^1]);
    }
}
