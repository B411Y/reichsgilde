using Reichsgilde.Core.Geheimnisse;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Die Art einer Meldung (Benachrichtigungen Stufe 2). Zuerst die Ereignisse, die der Haushofmeister ansagt; alles andere ist
/// <see cref="Sonstiges"/> und wird von der Oberfläche weiter am Text eingeordnet. Die Werte werden im Spielstand als Name gespeichert –
/// neue Arten kommen hinten dazu, umbenannt wird keine.
/// </summary>
public enum Meldungsart
{
    Sonstiges,
    /// <summary>Das Oberhaupt eines Hauses ist gestorben (mit Erbe oder ohne – dann erlischt das Haus).</summary>
    TodOberhaupt,
    /// <summary>Ein anderes Mitglied der Familie ist gestorben.</summary>
    TodFamilie,
    Geburt,
    Heirat,
    /// <summary>Ein Mitglied der Familie ist erkrankt (auch an der Pest).</summary>
    Krankheit,
    /// <summary>Die Pest ist in der Stadt (betrifft alle).</summary>
    Pest,
    /// <summary>Feuer: Brandanschlag auf Werkstatt oder Wohnhaus, Blitzschlag, Funkenflug.</summary>
    Feuer,
    /// <summary>Einbruch in Werkstatt oder Wohnhaus (misslungen: <see cref="Meldungswichtigkeit.Normal"/>).</summary>
    Diebstahl,
    /// <summary>Überfall auf Karren oder Schiff (abgewehrt: <see cref="Meldungswichtigkeit.Normal"/>).</summary>
    Ueberfall,
    /// <summary>Ein Haus wird vor Gericht angeklagt (betroffen ist der Beklagte).</summary>
    Anklage,
    /// <summary>Der Kämmerer mahnt, oder ein Haus ist bankrott.</summary>
    Schulden,
    /// <summary>Der Verwalter eines Betriebs kauft nur das Nötigste.</summary>
    KasseKnapp,
    /// <summary>Es fehlt eine Zutat – die Arbeit ruht.</summary>
    LagerLeer,
    /// <summary>Das Lager ist voll – die Arbeit ruht.</summary>
    LagerVoll,
    Meisterpruefung,
    /// <summary>Ein Haus steigt in einen höheren Stand auf.</summary>
    StandAufstieg,
    /// <summary>Ergebnis einer Wahl in der Ratssitzung.</summary>
    Wahl,
    /// <summary>Ergebnis eines Antrags in der Ratssitzung.</summary>
    Ratssitzung,
    /// <summary>Eine Entscheidung (Stufe 3) ist gefallen – gewählt, mit der Voreinstellung nach Fristablauf oder hinfällig. Nur für das betroffene Haus.</summary>
    Entscheidung,
}

/// <summary>Wie wichtig eine Meldung aus Sicht des Kerns ist. Was die Oberfläche daraus macht (Verlauf, Siegel, Brief), entscheidet sie selbst.</summary>
public enum Meldungswichtigkeit { Gering, Normal, Hoch, Dringend }

/// <summary>Wo etwas geschah: in einem Betrieb (<see cref="Meldung.Ortsname"/> = Name), im Wohnhaus des Hauses, an einem Gebäude der Karte (Kennung und Lage).</summary>
public enum Meldungsort { Keiner, Betrieb, Wohnhaus, Gebaeude }

/// <summary>
/// Eine Meldung des Kerns mit Art, Betroffenem und Ort (Benachrichtigungen Stufe 2). <see cref="Text"/> ist genau der Satz, den
/// <see cref="Stadtwirtschaft.Meldung"/> weiterhin meldet. Verweise stehen als Nummern (Haus = Nummer in <see cref="Stadtwirtschaft.Konten"/>,
/// Person = <see cref="Dynastie.Person.Nummer"/>), damit die Meldung ohne Umweg in den Spielstand passt und auf jedem Rechner gleich ist.
/// </summary>
/// <param name="Art">Die Art (<see cref="Meldungsart.Sonstiges"/>, wenn die Stelle noch nur Text meldet).</param>
/// <param name="Text">Der Satz für die Anzeige.</param>
public sealed record Meldung(Meldungsart Art, string Text)
{
    public Meldungswichtigkeit Wichtigkeit { get; init; } = Meldungswichtigkeit.Gering;

    /// <summary>Das betroffene Haus (Nummer in <see cref="Stadtwirtschaft.Konten"/>) oder null (die Stadt, oder die Stelle sagt es noch nicht).</summary>
    public int? Haus { get; init; }

    /// <summary>Die betroffene Person (<see cref="Dynastie.Person.Nummer"/>) oder null.</summary>
    public int? Person { get; init; }

    public Meldungsort Ort { get; init; }

    /// <summary>Bei <see cref="Meldungsort.Betrieb"/> der Name des Betriebs, bei <see cref="Meldungsort.Gebaeude"/> die Kennung des Gebäudes in der Karte.</summary>
    public string? Ortsname { get; init; }

    /// <summary>Lage auf der Karte in Metern, wenn der Kern sie kennt (Blitzschlag).</summary>
    public double? X { get; init; }
    public double? Z { get; init; }

    /// <summary>Spielminute der Meldung (<see cref="Spielzeit.VergangeneMinuten"/>).</summary>
    public long Minute { get; init; }

    /// <summary>Nur für das betroffene Haus bestimmt (Nachrichten der Unterwelt an ihren Auftraggeber) – andere sollen sie nicht lesen.</summary>
    public bool Vertraulich { get; init; }

    /// <summary>
    /// Worum es geht, wenn die Meldung von einem Geheimnis handelt (Teil 3c: entstanden, entdeckt, erpresst, angezeigt, bloßgestellt, verurteilt) –
    /// für das Siegel der Art in Meldung und Brief. Nennt eine Meldung mehrere Geheimnisse verschiedener Art, bleibt es null; ebenso, wo nur
    /// bekannt wird, dass es ein Geheimnis gibt (der öffentliche Satz zum Erpresserbrief).
    /// </summary>
    public Geheimnisart? GeheimnisArt { get; init; }

    /// <summary>Das Spieljahr der Meldung.</summary>
    public int Jahr => new Spielzeit(Minute).Jahr;

    /// <summary>
    /// Die vorgegebene Wichtigkeit einer Art (eine Stelle darf sie senken, etwa beim abgewehrten Überfall). Achtung: Der Stress
    /// (<see cref="Dynastie.Familienbuch.StressNachMeldung"/>) zählt Diebstahl, Feuer und Überfall nur ab <see cref="Meldungswichtigkeit.Hoch"/> –
    /// eine vereitelte Tat muss deshalb „Normal“ melden.
    /// </summary>
    public static Meldungswichtigkeit Vorgabe(Meldungsart art) => art switch
    {
        Meldungsart.TodOberhaupt or Meldungsart.Feuer or Meldungsart.Pest => Meldungswichtigkeit.Dringend,
        Meldungsart.TodFamilie or Meldungsart.Geburt or Meldungsart.Heirat or Meldungsart.Diebstahl or Meldungsart.Ueberfall or Meldungsart.Anklage
            or Meldungsart.Schulden or Meldungsart.Meisterpruefung or Meldungsart.StandAufstieg or Meldungsart.Wahl => Meldungswichtigkeit.Hoch,
        Meldungsart.Krankheit or Meldungsart.KasseKnapp or Meldungsart.LagerLeer or Meldungsart.LagerVoll or Meldungsart.Ratssitzung
            or Meldungsart.Entscheidung => Meldungswichtigkeit.Normal,
        _ => Meldungswichtigkeit.Gering,
    };
}
