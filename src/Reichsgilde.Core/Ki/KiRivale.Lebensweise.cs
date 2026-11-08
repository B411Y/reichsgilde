using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Ki;

/// <summary>
/// Die Lebensweise der KI-Häuser (Charaktersystem Teil 3a): Für Oberhaupt, Erbe und Ehepartner wählt die KI einen Pfad und nimmt jeden
/// freien Punkt sofort – beides über Befehle (0 AP), wie ein Spieler.
///
///   • Pfad: der mit dem höchsten Wert aus Talent (Sterne) + 1 je passendem Zug − 1 je hemmendem Zug (bei Gleichstand das erste Talent).
///     Gewählt wird einmal; ist der Baum des Pfads voll, wechselt die KI zum nächstbesten Pfad mit freien Knoten.
///   • Knoten: der mögliche mit dem höchsten Gewicht <c>ki</c> aus den Daten, dann die niedrigere Reihe, dann die Spalte von links.
/// </summary>
public sealed partial class KiRivale
{
    private void Lebensweise()
    {
        var fb = _w.Familien;
        if (!fb.HatLebensweise || fb.Von(Konto) is not { } f) return;
        var d = fb.LebensweiseDaten!;
        var personen = new List<Person> { f.Oberhaupt };
        if (fb.Erbe(f) is { } erbe && !personen.Contains(erbe)) personen.Add(erbe);
        if (f.Ehepartner is { } e && !personen.Contains(e)) personen.Add(e);
        foreach (var p in personen.Where(fb.HatLebensweg))
        {
            // Knoten zuerst: freie Punkte liegen auch in früheren Pfaden.
            foreach (var t in TalentTexte.Alle.Where(d.Pfade.ContainsKey))
                while (fb.FreiePunkte(p, t) > 0 && BesterKnoten(p, t) is { } k)
                    if (!Befehlen(new KnotenWaehlen(p.Nummer, k.Kennung), $"{p.Vorname} lernt „{k.Daten.Name}“ ({d.Pfade[t].Name}).")) break;
            if (KiPfad(p) is { } ziel && ziel != p.Lebensweg.Pfad)
                Befehlen(new LebenswegWaehlen(p.Nummer, ziel), $"{p.Vorname} folgt dem {d.Pfade[ziel].Name}.");
        }
    }

    /// <summary>Der Pfad, den die KI für diese Person will: der gewählte, solange sein Baum nicht voll ist, sonst der beste mit freien Knoten.</summary>
    private Talent? KiPfad(Person p)
    {
        var fb = _w.Familien;
        var d = fb.LebensweiseDaten!;
        bool Voll(Talent t) => fb.Pfade(p).FirstOrDefault(z => z.Pfad == t) is { } z && z.Knoten >= d.Pfade[t].Knoten.Count;
        if (p.Lebensweg.Pfad is { } jetzt && !Voll(jetzt)) return jetzt;
        double Wert(Talent t) => p.Talent(t) + p.Zuege.Count(z => d.Pfade[t].Passend.Contains(z.Kennung)) - p.Zuege.Count(z => d.Pfade[t].Hemmend.Contains(z.Kennung));
        var kandidaten = TalentTexte.Alle.Where(t => d.Pfade.ContainsKey(t) && !Voll(t)).ToList();
        return kandidaten.Count == 0 ? p.Lebensweg.Pfad : kandidaten.OrderByDescending(Wert).ThenBy(t => t).First();
    }

    private (string Kennung, KnotenDaten Daten)? BesterKnoten(Person p, Talent pfad)
    {
        var fb = _w.Familien;
        var d = fb.LebensweiseDaten!;
        var spalten = d.Pfade[pfad].Spalten;
        return d.AlleKnoten.Where(x => x.Pfad == pfad && fb.KnotenVerboten(Konto, p, x.Kennung) is null)
            .OrderByDescending(x => x.Daten.Ki).ThenBy(x => x.Daten.Reihe).ThenBy(x => spalten.IndexOf(x.Daten.Spalte))
            .Select(x => ((string, KnotenDaten)?)(x.Kennung, x.Daten)).FirstOrDefault();
    }
}
