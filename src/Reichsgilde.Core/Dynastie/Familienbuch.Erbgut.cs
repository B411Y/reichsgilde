using Reichsgilde.Core.Speichern;

namespace Reichsgilde.Core.Dynastie;

/// <summary>
/// Das Erbgut der Personen (Charakter-Editor, Phase 1): Wer ins Spiel kommt, bekommt es hier – gewürfelt (Oberhaupt bei Spielbeginn,
/// Heiratskandidat) oder vererbt (Kind). Die Regeln stehen in <see cref="Erbgut"/>, die Werte in <c>game/daten/erbgut.json</c>.
///
/// Würfel: ein eigener Startwert je Person aus dem Startwert des Familienbuchs und der Personennummer (<see cref="ErbgutStartwert"/>).
/// Kein anderer Generator würfelt dafür – Familien, Talente, Aussehen und Wirtschaft laufen genau wie vorher, und ein alter Spielstand
/// bekommt beim Laden dasselbe Erbgut wie eine neue Partie mit demselben Startwert.
/// </summary>
public sealed partial class Familienbuch
{
    private const int ZweckErbgut = 7, ZweckErbgutEntwickler = 8;

    /// <summary>Die Gen-Vorlage, nach der alle Personen dieser Partie ihr Erbgut tragen.</summary>
    public Erbgutvorlage Erbgutvorlage => Erbgutvorlage.Standard;

    /// <summary>Der Startwert für das Erbgut einer Person – fest aus dem Startwert der Partie und der Nummer.</summary>
    internal int ErbgutStartwert(int nummer) => Zugwuerfel.Mischen(_aussehenZufall.Startwert, nummer, ZweckErbgut);

    /// <summary>Wer erwachsen von außen kommt (Oberhaupt bei Spielbeginn, Heiratskandidat): Erbgut aus der Glockenkurve des Volkes.</summary>
    private void ErbgutWuerfeln(Person p) => p.Erbgut = Erbgut.Wuerfeln(Erbgutvorlage, ErbgutStartwert(p.Nummer), p.Volk);

    /// <summary>Ein Kind: je Gen ein Allel von Vater und Mutter (fehlt einer, eines aus dem Volk des Kindes).</summary>
    private void ErbgutVererben(Person kind, Person? vater, Person? mutter) =>
        kind.Erbgut = Erbgut.Vererben(Erbgutvorlage, ErbgutStartwert(kind.Nummer), kind.Volk, vater?.Erbgut, mutter?.Erbgut);

    /// <summary>
    /// Entwicklerfenster: ein neues Erbgut, gewürfelt wie für jemanden von außen. Der Würfel hängt am bisherigen Erbgut – jeder Aufruf gibt
    /// ein anderes, und auf jedem Rechner dasselbe.
    /// </summary>
    internal void EntwicklerErbgutWuerfeln(Person p)
    {
        var startwert = Zugwuerfel.Mischen(ErbgutStartwert(p.Nummer), Erbgutvorlage.Streuwert(p.Erbgut.AlsText()), ZweckErbgutEntwickler);
        p.Erbgut = Erbgut.Wuerfeln(Erbgutvorlage, startwert, p.Volk);
    }

    /// <summary>Die Genliste für den Spielstand (in dieser Reihenfolge steht <see cref="PersonStand.Erbgut"/>).</summary>
    private List<string> ErbgutGene() => Erbgutvorlage.Gene.Select(g => g.Kennung).ToList();

    /// <summary>Das Erbgut einer Person als Text für den Spielstand.</summary>
    private string ErbgutText(Person p) =>
        p.Erbgut.Vorlage == Erbgutvorlage ? p.Erbgut.AlsText()
            : throw new InvalidOperationException($"{p.Name} trägt ein Erbgut aus einer anderen Gen-Vorlage.");

    /// <summary>
    /// Beim Laden: das Erbgut jeder Person aus dem Spielstand, Fehlendes wie bei der Geburt. In der Reihenfolge der Nummern – Eltern sind
    /// immer älter als ihre Kinder, ihr Erbgut steht also schon, wenn ein Kind aus einem alten Spielstand es erben soll.
    /// </summary>
    private void ErbgutWiederherstellen(FamilienbuchStand s, IReadOnlyDictionary<int, Person> personen)
    {
        foreach (var ps in s.Personen.OrderBy(p => p.Nummer))
        {
            var p = personen[ps.Nummer];
            var vater = p.Vater is int v ? personen.GetValueOrDefault(v) : null;
            var mutter = p.Mutter is int m ? personen.GetValueOrDefault(m) : null;
            p.Erbgut = Erbgut.AusSpielstand(Erbgutvorlage, ErbgutStartwert(p.Nummer), p.Volk, s.ErbgutGene, ps.Erbgut,
                vater?.Erbgut, mutter?.Erbgut);
        }
    }
}
