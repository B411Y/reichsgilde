namespace Reichsgilde.Core.Bauen;

/// <summary>
/// Die Felder der Klappbrücke aus den Pfeilern des Wasserplans (<see cref="Wasserquerung.Pfeiler"/> bei <see cref="Wasserquerung.Durchfahrt"/> &gt; 0): Zwischen
/// Uferkante und Torhauspfeiler und zwischen den Zwischenpfeilern liegt je ein Bogenfeld, in der Mitte das Mittelteil mit den Torhäusern. Alle Stellen in Metern
/// ab der Uferkante A entlang der Brücke. Die Darstellung stellt Bogenfelder und Zwischenpfeiler nur dorthin (kein Eigenrechnen der Pfeiler).
/// </summary>
public sealed record Klappbrueckenlage(IReadOnlyList<(double Mitte, double Laenge)> Felder, IReadOnlyList<double> Zwischenpfeiler, double Mitte)
{
    /// <summary>
    /// Wie weit die Klappen zur Spielminute <paramref name="minute"/> offen sind (0 zu, 1 ganz auf) bei einer Öffnung des Kerns von <paramref name="von"/> bis <paramref name="bis"/>:
    /// Sie fahren auf, sobald die Öffnung beginnt, und zu, sodass sie bei <paramref name="bis"/> geschlossen sind; eine Fahrt dauert <paramref name="fahrtMinuten"/> Spielminuten.
    /// Eine Öffnung, die kürzer als zwei Fahrten ist, öffnet nur so weit, wie die Zeit reicht. Außerhalb der Öffnung 0.
    /// </summary>
    public static float Offenheit(double minute, double von, double bis, double fahrtMinuten) =>
        fahrtMinuten <= 0 ? (minute >= von && minute < bis ? 1f : 0f)
        : minute < von || minute >= bis ? 0f : (float)Math.Min(Math.Clamp((minute - von) / fahrtMinuten, 0, 1), Math.Clamp((bis - minute) / fahrtMinuten, 0, 1));

    /// <summary>
    /// Wie weit die Schrankenbäume zur Spielminute <paramref name="minute"/> geschlossen sind (0 oben und offen, 1 quer über der Fahrbahn) bei einer Öffnung des Kerns von
    /// <paramref name="von"/> bis <paramref name="bis"/>: Sie schließen sich in der Fahrt (<paramref name="fahrtMinuten"/> Spielminuten) vor <paramref name="von"/>, bleiben zu, solange die Brücke
    /// offen ist, und heben sich in der Fahrt nach <paramref name="bis"/>. Rein für die Anzeige, ohne Wirkung auf den Kern.
    /// </summary>
    public static float Schrankenstand(double minute, double von, double bis, double fahrtMinuten) =>
        fahrtMinuten <= 0 ? (minute >= von && minute < bis ? 1f : 0f)
        : (float)Math.Min(Math.Clamp((minute - (von - fahrtMinuten)) / fahrtMinuten, 0, 1), Math.Clamp((bis + fahrtMinuten - minute) / fahrtMinuten, 0, 1));

    /// <summary>
    /// <paramref name="pfeilerStellen"/>: die Pfeiler von Ufer A nach B (aufsteigend); die beiden mittleren sind die Torhauspfeiler und stehen
    /// <c>durchfahrt + torhausBreite</c> auseinander (Mittelpunkt zu Mittelpunkt). Null, wenn die Pfeiler dazu nicht passen (zu wenige, ungerade, falscher Abstand,
    /// ein Feld ohne Länge).
    /// </summary>
    public static Klappbrueckenlage? Aus(double weite, IReadOnlyList<double> pfeilerStellen, double durchfahrt, double torhausBreite, double zwischenpfeilerBreite)
    {
        var n = pfeilerStellen.Count;
        if (n < 2 || n % 2 != 0 || durchfahrt <= 0) return null;
        var (torA, torB) = (pfeilerStellen[n / 2 - 1], pfeilerStellen[n / 2]);
        if (Math.Abs(torB - torA - (durchfahrt + torhausBreite)) > 0.01) return null;
        var felder = new List<(double, double)>();
        var zwischen = new List<double>();
        void Seite(IEnumerable<double> pfeiler, double von, double bis)
        {
            var start = von;
            foreach (var c in pfeiler)
            {
                felder.Add(((start + c - zwischenpfeilerBreite / 2) / 2, c - zwischenpfeilerBreite / 2 - start));
                zwischen.Add(c);
                start = c + zwischenpfeilerBreite / 2;
            }
            felder.Add(((start + bis) / 2, bis - start));
        }
        Seite(pfeilerStellen.Take(n / 2 - 1), 0, torA - torhausBreite / 2);
        Seite(pfeilerStellen.Skip(n / 2 + 1), torB + torhausBreite / 2, weite);
        if (felder.Any(f => f.Item2 < 0.5)) return null;
        return new Klappbrueckenlage(felder, zwischen, (torA + torB) / 2);
    }
}
