using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Dynastie;

public enum Geschlecht
{
    Mann,
    Frau,
}

/// <summary>
/// Ein Mensch, Zwerg, Elb oder Halbling der Stadt – Familienmitglied oder Heiratskandidat.
/// Jede Person hat eine eindeutige Nummer. Befehle und Spielstände verweisen über diese Nummer auf sie.
/// </summary>
public sealed class Person
{
    public int Nummer { get; }
    public string Vorname { get; }

    /// <summary>Der Name des Hauses, zu dem die Person gehört (ändert sich bei der Heirat).</summary>
    public string Familienname { get; internal set; }

    /// <summary>Der Familienname bei der Geburt („geb. …").</summary>
    public string Geburtsname { get; }

    public Volk Volk { get; }
    public Geschlecht Geschlecht { get; }
    public int Geburtsjahr { get; }
    public int? Todesjahr { get; internal set; }

    /// <summary>Nummern der Eltern (null bei Personen, die zu Spielbeginn schon leben oder von außen kommen).</summary>
    public int? Vater { get; }
    public int? Mutter { get; }

    public bool Lebt => Todesjahr is null;

    private readonly double[] _talente = new double[TalentTexte.Alle.Length];

    /// <summary>
    /// Sterne, die gerade wirken (0 bis 6, halbe Sterne möglich) – eine Krankheit zieht etwas ab.
    /// Für Anlage, Üben, Vererben und Speichern zählt <see cref="Anlage"/>.
    /// </summary>
    public double Talent(Talent t) => Math.Max(0, _talente[(int)t] - (Krankheit?.Abzug(t) ?? 0));

    /// <summary>Sterne ohne Krankheit – das, was die Person eigentlich kann.</summary>
    public double Anlage(Talent t) => _talente[(int)t];
    internal void TalentSetzen(Talent t, double wert) => _talente[(int)t] = wert;

    /// <summary>Die Krankheit, an der die Person gerade leidet (oder null).</summary>
    public Gesundheit.Erkrankung? Krankheit { get; internal set; }

    /// <summary>Woran die Person gestorben ist („an der Pest") – nur, wenn es eine Krankheit war.</summary>
    public string? Todesursache { get; internal set; }

    private readonly int[] _geuebt = new int[TalentTexte.Alle.Length];

    /// <summary>Jahr, in dem das Talent zuletzt geübt wurde (0 = nie).</summary>
    public int GeuebtIm(Talent t) => _geuebt[(int)t];
    internal void GeuebtSetzen(Talent t, int jahr) => _geuebt[(int)t] = jahr;
    public double TalentSumme => _talente.Sum();

    /// <summary>Die beste Begabung, z. B. für die Brautschau.</summary>
    public IEnumerable<(Talent Talent, double Wert)> Staerken =>
        TalentTexte.Alle.Select(t => (t, Anlage(t))).OrderByDescending(x => x.Item2).ThenBy(x => x.t);

    /// <summary>Laufende Ausbildung eines Kindes (Name aus "ausbildungen") oder null.</summary>
    public string? Ausbildung { get; internal set; }

    /// <summary>Abgeschlossene Ausbildung (nach der Volljährigkeit).</summary>
    public string? Abschluss { get; internal set; }
    public string Name => $"{Vorname} {Familienname}";

    /// <summary>„Agnes Hammerschmid, geb. Weber" – der Geburtsname nur, wenn er anders ist.</summary>
    public string NameMitHerkunft => Geburtsname == Familienname ? Name : $"{Name}, geb. {Geburtsname}";

    internal Person(int nummer, string vorname, string familienname, Volk volk, Geschlecht geschlecht, int geburtsjahr,
        int? vater = null, int? mutter = null, string? geburtsname = null, int? todesjahr = null)
    {
        Nummer = nummer;
        Vorname = vorname;
        Familienname = familienname;
        Geburtsname = geburtsname ?? familienname;
        Volk = volk;
        Geschlecht = geschlecht;
        Geburtsjahr = geburtsjahr;
        Vater = vater;
        Mutter = mutter;
        Todesjahr = todesjahr;
    }

    /// <summary>Alter in diesem Jahr (bei Verstorbenen: das Alter beim Tod).</summary>
    public int Alter(int jahr) => (Todesjahr ?? jahr) - Geburtsjahr;

    public bool IstKindVon(Person elternteil) => Vater == elternteil.Nummer || Mutter == elternteil.Nummer;

    /// <summary>„Patriarch" oder „Matriarchin" – je nach Geschlecht.</summary>
    public string OberhauptTitel => Geschlecht == Geschlecht.Frau ? "Matriarchin" : "Patriarch";

    public string SohnOderTochter => Geschlecht == Geschlecht.Frau ? "Tochter" : "Sohn";
    public string Ehepartnertitel => Geschlecht == Geschlecht.Frau ? "Ehefrau" : "Ehemann";
}

/// <summary>Ein lediger Bürger der Stadt, um den man werben kann.</summary>
public sealed class Kandidat
{
    public Person Person { get; }

    /// <summary>Stand der Herkunftsfamilie (Stufe in der Ständeordnung).</summary>
    public int Stand { get; }

    internal Kandidat(Person person, int stand)
    {
        Person = person;
        Stand = stand;
    }
}

/// <summary>
/// Die Familie eines Hauses: das Oberhaupt, sein Ehepartner und alle Angehörigen – lebende und verstorbene.
/// Das Konto (Vermögen, Betriebe, Stand) gehört dem Haus und geht beim Erbfall an das neue Oberhaupt.
/// </summary>
public sealed class Familie
{
    internal readonly List<Person> MitgliederListe = new();
    internal readonly List<Person> AhnenListe = new();

    public Konto Konto { get; }

    /// <summary>Volk des Hauses. Kinder gehören immer diesem Volk an – auch aus einer Ehe zwischen Völkern.</summary>
    public Volk Volk { get; }

    /// <summary>Familienname, z. B. „Hammerschmid" für „Familie Hammerschmid".</summary>
    public string Nachname { get; }

    public Person Oberhaupt { get; internal set; }

    /// <summary>Der lebende Ehepartner des Oberhaupts (null = ledig oder verwitwet).</summary>
    public Person? Ehepartner { get; internal set; }

    /// <summary>Der im Testament bestimmte Erbe (null = das älteste volljährige Kind erbt).</summary>
    public Person? Testament { get; internal set; }

    /// <summary>Alle Angehörigen, auch Verstorbene, in der Reihenfolge, in der sie zum Haus kamen.</summary>
    public IReadOnlyList<Person> Mitglieder => MitgliederListe;

    /// <summary>Die früheren Oberhäupter, das erste zuerst.</summary>
    public IReadOnlyList<Person> Ahnen => AhnenListe;

    public IEnumerable<Person> Lebende => MitgliederListe.Where(p => p.Lebt);

    /// <summary>Die Kinder des Oberhaupts (auch verstorbene), das älteste zuerst.</summary>
    public IEnumerable<Person> Kinder => KinderVon(Oberhaupt);

    public IEnumerable<Person> KinderVon(Person elternteil) => MitgliederListe
        .Where(k => k.IstKindVon(elternteil))
        .OrderBy(k => k.Geburtsjahr)
        .ThenBy(k => k.Nummer);

    internal Familie(Konto konto, Volk volk, string nachname, Person oberhaupt)
    {
        Konto = konto;
        Volk = volk;
        Nachname = nachname;
        Oberhaupt = oberhaupt;
        MitgliederListe.Add(oberhaupt);
    }

    /// <summary>„Familie Hammerschmid" → „Hammerschmid", „Haus Eisenbart" → „Eisenbart".</summary>
    public static string NachnameAus(string hausname)
    {
        foreach (var vorsatz in new[] { "Familie ", "Haus ", "Sippe " })
            if (hausname.StartsWith(vorsatz, StringComparison.Ordinal) && hausname.Length > vorsatz.Length)
                return hausname[vorsatz.Length..].Trim();
        return hausname.Trim();
    }
}
