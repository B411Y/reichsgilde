using System.Text;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Der Tooltip eines Talents: ein Satz, was es bewirkt, und die echte Wirkung in Zahlen – je Stern und beim jetzigen Stand.
/// Die Zahlen liest <see cref="Talentwirkungen"/> aus denselben Daten, mit denen die Simulation rechnet; hier steht keine Zahl abgeschrieben.
/// Aussehen: Der Tooltip läuft über den Tooltip-Autoload (<see cref="Tooltips"/>), der die Tafel-Optik schon festlegt; hier nur Text in klaren Zeilen.
/// </summary>
public static class TalentTipp
{
    /// <summary>
    /// <paramref name="p"/> ist die Person, deren Talent gezeigt wird. Wirken tut nur das Talent des Oberhaupts;
    /// bei allen anderen steht dabei, was es als Oberhaupt bewirken würde.
    /// </summary>
    public static string Text(Stadtwirtschaft w, Person p, Talent t)
    {
        var jetzt = p.Talent(t);
        var sb = new StringBuilder();
        sb.Append(t.Name()).Append(" – ").Append(t.Satz());
        var oberhaupt = w.Familien.FamilieVon(p) is { } f && f.Oberhaupt == p;
        var sterne = $"{Zahlenformat.Knapp(jetzt)} von {Zahlenformat.Knapp(w.Familien.TalentDaten.Hoechstwert)} Sternen";
        sb.Append(oberhaupt ? $"\nJetzt {sterne}:" : $"\nHat {sterne}; die Wirkung zählt nur beim Oberhaupt. Als Oberhaupt:");
        foreach (var z in Talentwirkungen.Zeilen(w, t))
            sb.Append($"\n• {z.Text}: {{{{{z.JeSternText}}}}} je Stern (jetzt {{{{{z.JetztText(jetzt)}}}}})"); // {{…}}: Wertfarbe im Tooltip-Thema
        return sb.ToString();
    }
}
