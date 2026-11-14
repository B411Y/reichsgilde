using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Dynastie;

/// <summary>Wie eine Wirkung zu lesen ist: relativer Zuschlag (+3 %), Prozentpunkte (+4 Prozentpunkte) oder einfache Punkte (+2 Punkte).</summary>
public enum WirkungsEinheit { Prozent, Prozentpunkte, Punkte }

/// <summary>
/// Eine Wirkung eines Talents je Stern, gelesen aus denselben Daten, die die Simulation benutzt.
/// <paramref name="JeStern"/> ist der Wert je Stern als Bruchteil (0,03 = 3 %) bzw. als Punktzahl; negativ senkt.
/// </summary>
public sealed record TalentWirkungsZeile(string Text, double JeStern, WirkungsEinheit Einheit)
{
    /// <summary>Der Wert bei dieser Sternzahl, in der Einheit der Anzeige (Prozent und Prozentpunkte ×100).</summary>
    public double Bei(double sterne) => JeStern * sterne * (Einheit == WirkungsEinheit.Punkte ? 1 : 100);

    /// <summary>„+3 %“, „−8 %“, „+4 Prozentpunkte“, „+2 Punkte“ (Vorzeichen und Einheit, Zahl deutsch).</summary>
    public static string Formatieren(double wert, WirkungsEinheit einheit) =>
        (wert > 0 ? "+" : wert < 0 ? "−" : "") + Zahlenformat.Knapp(Math.Abs(wert)) + einheit switch
        {
            WirkungsEinheit.Prozent => " %",
            WirkungsEinheit.Prozentpunkte => " Prozentpunkte",
            _ => Math.Abs(wert) == 1 ? " Punkt" : " Punkte",
        };

    public string JeSternText => Formatieren(Bei(1), Einheit);
    public string JetztText(double sterne) => Formatieren(Bei(sterne), Einheit);
}

/// <summary>
/// Was ein Talent des Oberhaupts je Stern bewirkt – nur lesend, für die Tooltips der Oberfläche. Es sind dieselben Werte, mit denen
/// <see cref="Familienbuch"/> (Erlös, Leistung, Gauner, Karren, Rat, Gericht, Brautgeschenke), das Geheimniswesen (Spitzel),
/// das Glaubenswesen (Bekehrung) und das Kirchenwesen (Anziehung) rechnen; ein Test hält sie gleich.
/// Wirken tut immer das Talent des <i>Oberhaupts</i> (<see cref="Familienbuch.Talent"/>).
/// </summary>
public static class Talentwirkungen
{
    public static IReadOnlyList<TalentWirkungsZeile> Zeilen(Stadtwirtschaft w, Talent t)
    {
        var m = w.Familien.TalentDaten.Wirkung;
        var z = new List<TalentWirkungsZeile>();
        void Neu(string text, double jeStern, WirkungsEinheit e) => z.Add(new TalentWirkungsZeile(text, jeStern, e));
        switch (t)
        {
            case Talent.Verhandeln:
                Neu("Erlös beim Verkaufen", m.Verhandeln, WirkungsEinheit.Prozent);
                Neu("Einkaufspreis beim Handel von Hand und mit Karren", -m.Verhandeln, WirkungsEinheit.Prozent);
                break;
            case Talent.Handwerk:
                Neu("Leistung aller Arbeitskräfte", m.Handwerk, WirkungsEinheit.Prozent);
                break;
            case Talent.NachtUndNebel:
                Neu("Erfolgschance im Hinterzimmer", m.NachtErfolg, WirkungsEinheit.Prozentpunkte);
                Neu("Chance, dass Zeugen die Tat sehen", -m.NachtZeugen, WirkungsEinheit.Prozent);
                if (w.Geheimnisse.IstAktiv)
                    Neu("Spitzel: Geheimnisse erfahren (je Stern Vorsprung vor dem Ausgespähten)", w.Geheimnisse.Daten.Entdecken.SpitzelJeStern, WirkungsEinheit.Prozentpunkte);
                break;
            case Talent.Kampf:
                Neu("Stärke angeheuerter Wegelagerer", m.KampfUeberfall, WirkungsEinheit.Prozentpunkte);
                Neu("Schutz der eigenen Karren und Schiffe vor Überfall", m.KampfSchutz, WirkungsEinheit.Prozentpunkte);
                break;
            case Talent.Rhetorik:
                Neu("Punkte bei Ratswahlen", m.RhetorikRat, WirkungsEinheit.Punkte);
                Neu("Plädoyer vor Gericht (je Stern Unterschied zum Gegner)", m.RhetorikGericht, WirkungsEinheit.Punkte);
                Neu("Wirkung der Brautgeschenke", m.RhetorikWerben, WirkungsEinheit.Prozent);
                if (w.Glaube.IstAktiv) Neu("Kraft der Bekehrungspredigt", w.Glaube.Daten.Bekehrung.JeRhetorik, WirkungsEinheit.Prozent);
                if (w.Kirchenwesen.IstAktiv) Neu("Anziehung der eigenen Kirchen", w.Kirchenwesen.Daten.AnziehungJeRhetorik, WirkungsEinheit.Prozent);
                break;
        }
        return z;
    }
}
