using System.Reflection;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Gleichlauf in der Nacht (Mehrspieler M1, nach der Prüfung): Unterwelt und Diebeswesen merken sich während eines Nachtversuchs Faktor,
/// „letzter Versuch“ und „schon Erfolg“. Diese Felder stehen nicht im Spielstand – das geht nur, wenn sie zwischen zwei Spielminuten immer auf
/// dem Ruhewert stehen (1, true, false). Bis zur Prüfung blieb „schon Erfolg“ nach einem Versuch stehen; ein späterer Überfall, eine Entführung
/// oder ein Brand sah dann den Erfolg eines anderen Auftrags, und nach dem Laden (false) lief die Partie anders.
/// Der Test liest die privaten Felder per Reflection: Er prüft genau die Annahme, mit der sie aus dem Spielstand bleiben dürfen.
/// </summary>
static class GaunerNachtzustandTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static T Feld<T>(object o, string name) =>
        (T)(o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(o)
            ?? throw new Exception($"Feld {o.GetType().Name}.{name} gibt es nicht mehr – Test anpassen."));

    static string? Abweichung(object wesen)
    {
        var (faktor, letzter, bisher) = (Feld<double>(wesen, "_chanceFaktor"), Feld<bool>(wesen, "_letzterVersuch"), Feld<bool>(wesen, "_bisherErfolg"));
        return faktor == 1 && letzter && !bisher ? null : $"{wesen.GetType().Name}: Faktor {faktor}, letzter Versuch {letzter}, schon Erfolg {bisher}";
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Gaunernacht: Zwischen zwei Spielminuten steht der Versuchszustand von Unterwelt und Dieben immer auf Ruhe (nicht im Spielstand)", () =>
        {
            // Wie im Gleichheitstest: Startwert 4711, KI wagemutig und schwer – viele Banden und Einbrüche
            var (sim, _) = Partieaufbau.Erstellen(
                Partieeinstellungen.Standard with { Zufallsstartwert = 4711, Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = Schwierigkeit.Schwer },
                Daten.Value, new NeuePartie(Rivale: KiCharakter.Wagemutig), karte: Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));
            var w = sim.Wirtschaft!;
            var naechte = 0; // Minuten, in denen ein Auftrag dieser Nacht schon Erfolg hatte – sonst prüft der Test nichts
            for (var t = 0; t < 4 * Spielzeit.MinutenProTag; t++)
            {
                sim.Tick();
                foreach (var wesen in new object[] { w.Gauner, w.Diebe })
                {
                    if (Abweichung(wesen) is { } fehler) throw new Exception($"Nach Minute {t + 1}: {fehler}");
                    if (Feld<List<bool>>(wesen, "_erfolgreich").Contains(true)) naechte++;
                }
            }
            Pruefe.Wahr(naechte > 0, "In vier Jahren hatte kein Auftrag einen Erfolg mitten in der Nacht – Test prüft nichts (Daten geändert?).");
        }),
    };
}
