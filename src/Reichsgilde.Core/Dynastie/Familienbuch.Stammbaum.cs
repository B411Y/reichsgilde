using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Dynastie;

/// <summary>
/// Ein Knoten im Stammbaum eines Hauses: eine Person aus dem Blut des Hauses, ihre Ehepartner (Eingeheiratete) und die Kinder darunter.
/// Der Baum ist nur abgeleitet (aus den Personen, ihren Eltern und dem Familienstand) – er hat keinen eigenen Spielstand.
/// </summary>
/// <param name="Person">Die Person aus dem Haus (Gründer oder Kind).</param>
/// <param name="Ehepartner">Wer in die Ehe kam: andere Elternteile der Kinder und der heutige Ehepartner, die früheste Ehe zuerst.</param>
/// <param name="Kinder">Die Kinder aus diesen Ehen, das älteste zuerst.</param>
public sealed record StammbaumKnoten(Person Person, IReadOnlyList<Person> Ehepartner, IReadOnlyList<StammbaumKnoten> Kinder);

/// <summary>Was ein Eintrag der Hauschronik erzählt (bestimmt Farbe und Zeichen in der Oberfläche). Steht im Spielstand als Name – neue Werte kommen hinten dazu.</summary>
public enum Chronikart { Gruendung, Geburt, Heirat, Tod, Erbe, Amt, Geheimnis, Entscheidung, Sonstiges, Brand }

/// <summary>Ein Eintrag der Hauschronik. <paramref name="Person"/> ist die Personennummer, wenn eine Person im Mittelpunkt steht (Porträt, Klick).</summary>
public sealed record Chronikeintrag(int Jahr, Chronikart Art, string Text, int? Person);

public sealed partial class Familienbuch
{
    // ═════════════════════════════════════════════════════════════════════════
    //  Stammbaum und Hauschronik (nur lesend; gespeichert wird nur Familie.Hauschronik)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Der Stammbaum des Hauses: die Wurzeln (Gründer und alle, die keinem Elternteil im Haus zuzuordnen sind) mit ihren Nachkommen.
    /// Ehepartner stehen am Knoten der Person, mit der sie Kinder haben (oder die sie heute geheiratet haben); wer im Haus ohne Kinder und ohne
    /// Ehe verwitwet ist, erscheint als eigene Wurzel. Jede Person kommt genau einmal vor.
    /// </summary>
    public IReadOnlyList<StammbaumKnoten> Stammbaum(Familie f)
    {
        var mitglieder = f.Mitglieder;
        var nummern = new Dictionary<int, Person>(mitglieder.Count);
        foreach (var p in mitglieder) nummern[p.Nummer] = p;

        bool HatElternImHaus(Person p) => (p.Vater is int v && nummern.ContainsKey(v)) || (p.Mutter is int m && nummern.ContainsKey(m));

        // Kinder je Elternteil, einmal gesammelt (statt je Person über alle Mitglieder zu laufen).
        var kinderVon = new Dictionary<int, List<Person>>();
        foreach (var k in mitglieder.Where(HatElternImHaus).OrderBy(k => k.Geburtsjahr).ThenBy(k => k.Nummer))
        {
            foreach (var eltern in new[] { k.Vater, k.Mutter })
                if (eltern is int e && nummern.ContainsKey(e))
                {
                    if (!kinderVon.TryGetValue(e, out var liste)) kinderVon[e] = liste = new List<Person>();
                    liste.Add(k);
                }
        }

        var vergeben = new HashSet<int>(); // schon im Baum (als Knoten oder Ehepartner)

        StammbaumKnoten Knoten(Person p)
        {
            vergeben.Add(p.Nummer);
            var ehepartner = new List<Person>();
            var kinder = new List<Person>();
            var block = new List<Person> { p };
            void Anhaengen(Person x)
            {
                if (!HatElternImHaus(x) && !block.Contains(x) && vergeben.Add(x.Nummer)) { block.Add(x); ehepartner.Add(x); }
            }
            // Der Block wächst, bis keine Ehe mehr dazukommt: Jeder im Block kann Ehepartner haben
            //  - durch gemeinsame Kinder (der andere Elternteil),
            //  - als Nachfolger nach dem Witwenrecht (folgt auf ein früheres Oberhaupt im Block, auch ohne Kinder, auch über mehrere Ehen),
            //  - als heutiger Ehepartner des Oberhaupts.
            for (var i = 0; i < block.Count; i++)
            {
                var q = block[i];
                var ahnIndex = f.Ahnen.ToList().IndexOf(q);
                if (ahnIndex >= 0) Anhaengen(ahnIndex + 1 < f.Ahnen.Count ? f.Ahnen[ahnIndex + 1] : f.Oberhaupt);
                if (q == f.Oberhaupt && f.Ehepartner is { } heute) Anhaengen(heute);
                foreach (var k in kinderVon.GetValueOrDefault(q.Nummer) ?? new List<Person>())
                {
                    if (!kinder.Contains(k)) kinder.Add(k);
                    foreach (var eltern in new[] { k.Vater, k.Mutter })
                        if (eltern is int e && nummern.TryGetValue(e, out var anderer)) Anhaengen(anderer);
                }
            }
            kinder = kinder.OrderBy(k => k.Geburtsjahr).ThenBy(k => k.Nummer).ToList();
            var unten = new List<StammbaumKnoten>();
            foreach (var k in kinder)
                if (!vergeben.Contains(k.Nummer)) unten.Add(Knoten(k));
            return new StammbaumKnoten(p, ehepartner, unten);
        }

        var wurzeln = new List<StammbaumKnoten>();
        // Zuerst die, die vor allen anderen ins Haus kamen (Reihenfolge der Mitglieder); sie nehmen ihre Ehepartner mit.
        foreach (var p in mitglieder)
            if (!HatElternImHaus(p) && !vergeben.Contains(p.Nummer))
                wurzeln.Add(Knoten(p));
        // Wer durch kaputte Eltern-Verweise (Kreise, Verweis auf sich selbst) nirgends hängt, wird nicht verschluckt, sondern eine eigene Wurzel.
        foreach (var p in mitglieder)
            if (!vergeben.Contains(p.Nummer))
                wurzeln.Add(Knoten(p));
        return wurzeln;
    }

    /// <summary>Wie viele Personen ein Stammbaum zeigt (zum Prüfen und für die Anzeige).</summary>
    public static int Personenzahl(IEnumerable<StammbaumKnoten> wurzeln) =>
        wurzeln.Sum(k => 1 + k.Ehepartner.Count + Personenzahl(k.Kinder));

    /// <summary>
    /// Die Hauschronik, das neueste Jahr zuerst. Sie entsteht aus zwei Quellen, beide dauerhaft:
    /// <list type="bullet">
    /// <item>den Personen selbst – Geburt, Tod, Erbfolge (Todesjahr eines früheren Oberhaupts, Nachfolger) und der Anfang des Hauses;</item>
    /// <item>der gespeicherten Chronik des Hauses (<see cref="Familie.Hauschronik"/>): Heirat, Amt und Stand, Geheimnis, Brand, Entscheidungen mit Folgen.</item>
    /// </list>
    /// Vertrauliche Einträge (<see cref="Hauschronikeintrag.Vertraulich"/>) stehen nur für <paramref name="betrachter"/> = das Haus selbst darin.
    /// </summary>
    public IReadOnlyList<Chronikeintrag> Chronik(Familie f, Konto betrachter)
    {
        var liste = new List<Chronikeintrag>();
        var nummern = f.Mitglieder.ToDictionary(p => p.Nummer);
        var erster = f.Mitglieder.Count > 0 ? f.Mitglieder[0] : f.Oberhaupt;
        liste.Add(new Chronikeintrag(Spielzeit.StartJahr, Chronikart.Gruendung,
            $"Das Haus {f.Nachname} beginnt mit {erster.Name} ({erster.Volk.Einzahl()}, {erster.Alter(Spielzeit.StartJahr)} Jahre).", erster.Nummer));

        foreach (var p in f.Mitglieder)
        {
            if (p.Vater is int v && nummern.TryGetValue(v, out var vater))
            {
                var mutter = p.Mutter is int m && nummern.TryGetValue(m, out var mu) ? mu : null;
                liste.Add(new Chronikeintrag(p.Geburtsjahr, Chronikart.Geburt,
                    $"{p.Vorname} wird geboren, {(p.Geschlecht == Geschlecht.Frau ? "Tochter" : "Sohn")} von {vater.Vorname}{(mutter is null ? "" : $" und {mutter.Vorname}")}.", p.Nummer));
            }
            else if (p.Mutter is int m2 && nummern.TryGetValue(m2, out var mutter2))
                liste.Add(new Chronikeintrag(p.Geburtsjahr, Chronikart.Geburt,
                    $"{p.Vorname} wird geboren, {(p.Geschlecht == Geschlecht.Frau ? "Tochter" : "Sohn")} von {mutter2.Vorname}.", p.Nummer));
            if (p.Todesjahr is int tod)
                liste.Add(new Chronikeintrag(tod, Chronikart.Tod,
                    $"{p.Vorname} {p.Familienname} stirbt mit {p.Alter(Jahr)} Jahren{(p.Todesursache is { } u ? " " + u : "")}.", p.Nummer));
        }

        // Erbfolge: Der Nachfolger eines früheren Oberhaupts ist das nächste in der Ahnenreihe, zuletzt das heutige Oberhaupt.
        for (var i = 0; i < f.Ahnen.Count; i++)
        {
            var tot = f.Ahnen[i];
            var erbe = i + 1 < f.Ahnen.Count ? f.Ahnen[i + 1] : f.Oberhaupt;
            if (tot.Todesjahr is not int jahr) continue;
            if (erbe == tot)
                liste.Add(new Chronikeintrag(jahr, Chronikart.Erbe, $"Mit {tot.Vorname} erlischt das Haus {f.Nachname}.", tot.Nummer));
            else
                liste.Add(new Chronikeintrag(jahr, Chronikart.Erbe, $"{erbe.Vorname} {erbe.Familienname} wird Oberhaupt, nach {tot.Vorname}.", erbe.Nummer));
        }

        // Die gespeicherte Chronik: nur, was sich aus den Personen nicht ablesen lässt.
        foreach (var e in f.Hauschronik)
            if (!e.Vertraulich || betrachter == f.Konto)
                liste.Add(new Chronikeintrag(e.Jahr, e.Art, e.Text, e.Person));

        // Neueste zuerst; im selben Jahr: in der Reihenfolge, in der die Einträge entstanden (stabil).
        return liste.Select((e, i) => (e, i)).OrderByDescending(x => x.e.Jahr).ThenBy(x => x.i).Select(x => x.e).ToList();
    }
}
