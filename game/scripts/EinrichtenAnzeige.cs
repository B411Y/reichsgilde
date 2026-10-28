using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Einrichtung (Werkzeugwand, PR #152; seit PR #224 als Talentbaum, siehe <see cref="TalentbaumAnsicht"/>): 3 Spalten × 3 Reihen (Gebäudestufen) mit den Stücken,
/// die eine Werkstatt dieses Berufs haben kann. Gekaufte Stücke stehen sofort im Raum; wo man nicht kaufen kann, nennt der Hinweis den Grund aus dem Kern
/// (<see cref="Werkstattwesen.EinrichtungVerboten"/>). Öffnet innen an der Werkzeugwand und außen über „Einrichtung“ der Kurzübersicht.
/// </summary>
public sealed class EinrichtenAnzeige : InnenTafel
{
    public EinrichtenAnzeige() : base(960) { }

    protected override string Stand(Stadtwirtschaft w, Betrieb b) =>
        $"{b.Stufe}|{(int)b.Konto.Vermoegen / 10}|" + string.Join(";", w.Werkstaetten.Baum(b).Stuecke.Select(e => $"{e.Kennung}:{e.Zustand}:{(int)e.MehrgewinnJeJahr}"));

    protected override void Aufbauen(Stadtwirtschaft w, Betrieb b)
    {
        var baum = w.Werkstaetten.Baum(b);
        var waehrung = w.Daten.Allgemein.Waehrung;
        Kopf("Einrichtung", b, $"In der Kasse: {Zahlenformat.Ganz(b.Konto.Vermoegen)} {waehrung}");
        if (baum.Stuecke.Count == 0) Text(Inhalt, "Für diese Werkstatt gibt es nichts zu kaufen.", 14, Grau);
        else
        {
            Inhalt.AddChild(TalentbaumAnsicht.Bauen(baum, waehrung, r => $"Stufe {r}", s => Befehlen(new EinrichtungKaufen(b.Name, s.Kennung))));
            Text(Inhalt, "Ein Klick auf ein Stück kauft es. Bunte Stücke kann man kaufen (beim Darüberfahren leuchten sie golden), ein grüner Haken heißt gekauft, dunkle sind gesperrt. "
                + "Der Rahmen im Bild zeigt die Stufe (Stein, Silber, Gold); Linien zeigen, was vorher da sein muss.", 12, Grau, 840);
        }
    }
}
