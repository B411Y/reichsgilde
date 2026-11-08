using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Ki;

/// <summary>
/// Die Lebensweise der KI-Häuser (Charaktersystem Teil 3a): Für Oberhaupt, Erbe und Ehepartner wählt die KI einen Pfad und nimmt jeden
/// freien Punkt sofort – beides über Befehle (0 AP), wie ein Spieler.
///
///   • Pfad: der mit dem höchsten Wert aus Talent (Sterne) + 1 je passendem Zug − 1 je hemmendem Zug (bei Gleichstand das erste Talent),
///     dazu <see cref="PfadZumHaus"/>, wenn der Pfad zur Arbeit des Hauses passt: der Pfad des Meisters für ein Haus mit einer Werkstatt mit Ware,
///     der Pfad der Schatten für ein Haus mit einem Betrieb des Diebes. Den Pfad der Schatten nimmt die KI nur, wenn das Haus in der Unterwelt
///     arbeitet (sonst brächte er ihm fast nichts; Nachsteuerung der Lebensweise, 8.11.2026). Ein Spieler wählt frei.
///     Hat die Person im Pfad einen Knoten, bleibt er; ist sein Baum voll, wechselt die KI zum nächstbesten Pfad mit freien Knoten.
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
            PfadWaehlen(p); // zuerst: Ein Pfad ohne eigenen Knoten (z. B. beim ersten Buchen festgehalten) bekommt so keinen Knoten, bevor die KI wählt
            // Freie Punkte liegen auch in früheren Pfaden.
            foreach (var t in TalentTexte.Alle.Where(d.Pfade.ContainsKey))
                while (fb.FreiePunkte(p, t) > 0 && BesterKnoten(p, t) is { } k)
                    if (!Befehlen(new KnotenWaehlen(p.Nummer, k.Kennung), $"{p.Vorname} lernt „{k.Daten.Name}“ ({d.Pfade[t].Name}).")) break;
            PfadWaehlen(p); // ist der Baum jetzt voll, gleich weiter zum nächsten
        }

        void PfadWaehlen(Person p)
        {
            if (KiPfad(p) is { } ziel && ziel != p.Lebensweg.Pfad)
                Befehlen(new LebenswegWaehlen(p.Nummer, ziel), $"{p.Vorname} folgt dem {d.Pfade[ziel].Name}.");
        }
    }

    /// <summary>So viele Sterne zählt ein Pfad mehr, der zur Arbeit des Hauses passt (Meister: Werkstatt mit Ware; Schatten: Betrieb des Diebes).</summary>
    internal const double PfadZumHaus = 2;

    /// <summary>Der Pfad, den die KI für diese Person will: der gewählte, solange sein Baum nicht voll ist, sonst der beste mit freien Knoten.</summary>
    private Talent? KiPfad(Person p)
    {
        var fb = _w.Familien;
        var d = fb.LebensweiseDaten!;
        bool Voll(Talent t) => fb.Pfade(p).FirstOrDefault(z => z.Pfad == t) is { } z && z.Knoten >= d.Pfade[t].Knoten.Count;
        var eigene = _w.Betriebe.Where(b => b.Konto == Konto).ToList();
        var werkstatt = eigene.Any(b => !b.OhneProduktion);
        var unterwelt = eigene.Any(_w.Diebe.IstDiebesbetrieb);
        // Der Pfad bleibt, sobald die Person in ihm einen Knoten hat – außer es ist der Pfad der Schatten ohne Betrieb des Diebes.
        // Ohne Knoten wählt die KI neu: Der Kern hält beim ersten Buchen den Pfad des stärksten Talents fest (neuer Erbe, neuer Ehepartner).
        bool HatKnoten(Talent t) => fb.Pfade(p).FirstOrDefault(z => z.Pfad == t) is { Knoten: > 0 };
        if (p.Lebensweg.Pfad is { } jetzt && !Voll(jetzt) && HatKnoten(jetzt) && (jetzt != Talent.NachtUndNebel || unterwelt)) return jetzt;
        double Wert(Talent t) => p.Talent(t) + p.Zuege.Count(z => d.Pfade[t].Passend.Contains(z.Kennung)) - p.Zuege.Count(z => d.Pfade[t].Hemmend.Contains(z.Kennung))
                                 + (t == Talent.Handwerk && werkstatt || t == Talent.NachtUndNebel && unterwelt ? PfadZumHaus : 0);
        var kandidaten = TalentTexte.Alle.Where(t => d.Pfade.ContainsKey(t) && !Voll(t)).ToList();
        // Den Pfad der Schatten nur in der Unterwelt – außer alle anderen Bäume sind voll.
        if (!unterwelt && kandidaten.Any(t => t != Talent.NachtUndNebel)) kandidaten.Remove(Talent.NachtUndNebel);
        if (kandidaten.Count == 0) return p.Lebensweg.Pfad;
        var bester = kandidaten.OrderByDescending(Wert).ThenBy(t => t).First();
        // Bei Gleichstand bleibt der jetzige Pfad (kein Hin und Her).
        return p.Lebensweg.Pfad is { } bisher && kandidaten.Contains(bisher) && Wert(bisher) >= Wert(bester) ? bisher : bester;
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
