using Reichsgilde.Core.Glaube;
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
public sealed class Person : ICharakter
{
    public int Nummer { get; }

    /// <summary>„P12“ – für Befehle und Meinungen (<see cref="ICharakter"/>).</summary>
    public string Kennung => $"P{Nummer}";
    public string Vorname { get; }

    /// <summary>Der Name des Hauses, zu dem die Person gehört (ändert sich bei der Heirat).</summary>
    public string Familienname { get; internal set; }

    /// <summary>Der Familienname bei der Geburt („geb. …").</summary>
    public string Geburtsname { get; }

    public Volk Volk { get; }
    public Geschlecht Geschlecht { get; }
    public int Geburtsjahr { get; }
    public int? Todesjahr { get; internal set; }

    /// <summary>Der Glaube heute (ein Haus folgt dem Glauben seines Oberhaupts).</summary>
    public Konfession Glaube { get; internal set; }

    /// <summary>Der Glaube, mit dem die Person aufwuchs – wer übertritt, behält ihn hier (Mischehe).</summary>
    public Konfession Herkunftsglaube { get; internal set; }

    internal void GlaubeSetzen(Konfession glaube) { Glaube = glaube; Herkunftsglaube = glaube; }

    /// <summary>Nummern der Eltern (null bei Personen, die zu Spielbeginn schon leben oder von außen kommen).</summary>
    public int? Vater { get; }
    public int? Mutter { get; }

    public bool Lebt => Todesjahr is null;

    /// <summary>
    /// Wie die Person im Porträt aussieht (Gesicht, Haar, Augen). Bis das Familienbuch würfelt oder vererbt, ein festes
    /// Aussehen aus Nummer und Volk – so bekommen auch Personen aus alten Spielständen eines.
    /// </summary>
    public Aussehen Aussehen { get; internal set; }

    private Erbgut? _erbgut;

    /// <summary>
    /// Das Erbgut für den 3D-Kopf: zwei Allele je Gen, eines sichtbar (<see cref="Dynastie.Erbgut.SichtbarerWert(string)"/>). Das Familienbuch
    /// würfelt oder vererbt es, sobald die Person ins Spiel kommt. Bis dahin (nur Personen, die ein Test selbst baut) gilt ein festes Erbgut
    /// aus Nummer und Volk. Wirkt auf nichts in der Simulation; das 2D-Porträt nimmt weiter <see cref="Aussehen"/>.
    /// </summary>
    public Erbgut Erbgut
    {
        get => _erbgut ??= Erbgut.Wuerfeln(Erbgutvorlage.Standard, Zugwuerfel.Mischen(Nummer, (int)Volk, 7919), Volk);
        internal set => _erbgut = value;
    }

    private readonly double[] _talente = new double[TalentTexte.Alle.Length];

    /// <summary>
    /// Sterne, die gerade wirken (0 bis 6, halbe Sterne möglich): Anlage, dazu die Charakterzüge, abzüglich einer Krankheit.
    /// Für Üben, Vererben und Speichern zählt <see cref="Anlage"/>.
    /// </summary>
    public double Talent(Talent t)
    {
        var anlage = _talente[(int)t];
        var mitZuegen = anlage + Zugwirkung(t);
        // Züge heben nicht über den Höchstwert (wer schon mehr kann, behält es); sie ziehen höchstens bis 0 ab.
        if (mitZuegen > anlage) mitZuegen = Math.Min(mitZuegen, Math.Max(anlage, TalentHoechstwert));
        return Math.Max(0, mitZuegen - (Krankheit?.Abzug(t) ?? 0));
    }

    /// <summary>Sterne ohne Krankheit und ohne Charakterzüge – das, was die Person gelernt und geerbt hat.</summary>
    public double Anlage(Talent t) => _talente[(int)t];

    /// <summary>Sterne ohne Charakterzüge, aber mit Krankheit (zum Vergleich im Charakterfenster).</summary>
    public double TalentOhneZuege(Talent t) => Math.Max(0, _talente[(int)t] - (Krankheit?.Abzug(t) ?? 0));

    /// <summary>Was die Charakterzüge zusammen an diesem Talent ändern (positiv oder negativ).</summary>
    public double Zugwirkung(Talent t) => _zuege.Sum(z => z.Talent(t));

    /// <summary>Bis hierhin heben Charakterzüge ein Talent (der Höchstwert aus "dynastie.talente").</summary>
    internal double TalentHoechstwert { get; set; } = 6;

    private readonly List<Charakterzug> _zuege = new();

    /// <summary>Die Charakterzüge (PR #227): Persönlichkeit ab der Volljährigkeit, angeborene, erworbene – in dieser Reihenfolge.</summary>
    public IReadOnlyList<Charakterzug> Zuege => _zuege;

    public bool HatZug(string kennung) => _zuege.Any(z => z.Kennung == kennung);

    /// <summary>Für Tests, die Talente ohne Züge prüfen.</summary>
    internal void ZuegeLeeren() => _zuege.Clear();

    internal void ZugHinzufuegen(Charakterzug zug)
    {
        if (_zuege.Contains(zug)) return;
        // Nach Art geordnet (Persönlichkeit, angeboren, erworben), innerhalb der Art in der Reihenfolge des Erhalts.
        var stelle = _zuege.FindLastIndex(z => z.Art <= zug.Art) + 1;
        _zuege.Insert(stelle, zug);
    }

    /// <summary>Nimmt einen Zug wieder weg (Entwicklerfenster).</summary>
    internal void ZugEntfernen(Charakterzug zug) => _zuege.Remove(zug);

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
        Aussehen = Aussehenspalette.Abgeleitet(nummer, volk);
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
