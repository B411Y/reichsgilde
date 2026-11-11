using Godot;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Entscheidungen;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Wahl des Spielers für die Entscheidungen (Benachrichtigungen Stufe 3, Oberfläche): je Art <b>Entscheidung</b> (der Brief fragt) oder <b>Automatisch</b>
/// (die Oberfläche schickt <c>EntscheidungTreffen(nr, −1)</c>, es gilt die Voreinstellung; es bleibt nur ein Brief mit dem Ergebnis), dazu der Haken
/// „Bei Entscheidungen anhalten“. Reine Oberflächen-Einstellung im Abschnitt <c>[entscheidungen]</c> von <c>user://einstellungen.cfg</c>.
/// Die Krankheit eines Kindes ist eine eigene Wahl (<see cref="KrankheitKind"/>, Standard Automatisch), weil der Kern Kind und Erwachsenen nicht trennt:
/// die Oberfläche unterscheidet über die Person (<see cref="Schluessel"/>, <see cref="Familienbuch.IstVolljaehrig"/>).
/// </summary>
public static class Entscheidungseinstellungen
{
    private const string Datei = "user://einstellungen.cfg", Abschnitt = "entscheidungen";

    /// <summary>Schlüssel der Krankheit eines Kindes (eine Wahl neben „Krankheit“ der Erwachsenen).</summary>
    public const string KrankheitKind = "KrankheitKind";

    /// <summary>Alle Wahlen, in der Reihenfolge der Einstellungen: Name der Art, Beschriftung und Standard „Automatisch“?</summary>
    public static readonly (string Schluessel, string Name, string Erklaerung, bool Automatisch)[] Arten =
    {
        ("Erpresserbrief", "Erpresserbrief", "Jemand verlangt Schweigegeld.", false),
        ("Schutzgeld", "Schutzgeld", "Die Diebe verlangen Schutzgeld, sonst brennt es.", false),
        ("Loesegeld", "Lösegeld", "Eine Geisel wird gegen Lösegeld angeboten.", false),
        ("Krankheit", "Krankheit (Oberhaupt, Gatte, Gesinde)", "Jemand im Haus ist krank: Medicus, Kräuterschnaps oder nichts.", false),
        (KrankheitKind, "Krankheit eines Kindes", "Ein Kind ist krank. Automatisch: Medicus, wenn bezahlbar, sonst Kräuterschnaps, sonst nichts.", true),
        ("Erbe", "Erbe bestimmen", "Das Oberhaupt ist alt oder schwer krank: einen Erben einsetzen.", false),
        ("Geheimnis", "Geheimnis entdeckt", "Euer Haus kennt ein fremdes Geheimnis: anzeigen, erpressen, bloßstellen oder merken.", false),
        ("Mahnung", "Mahnung des Kämmerers", "Das Haus hat Schulden: Kredit, Betrieb verkaufen oder abwarten.", false),
    };

    private static Dictionary<string, bool>? _wahl;
    private static bool _anhalten = true;

    /// <summary>Hält die Zeit an, solange ein Entscheidungsbrief offen ist (nur im Einzelspiel mit Verzögerung 0; Standard an)?</summary>
    public static bool Anhalten
    {
        get { Laden(); return _anhalten; }
        set
        {
            Laden();
            if (_anhalten == value) return;
            _anhalten = value;
            Speichern();
        }
    }

    /// <summary>Der Schlüssel der Wahl zu dieser Frage: der Name der Art, bei einer Krankheit eines Kindes <see cref="KrankheitKind"/>.</summary>
    public static string Schluessel(Entscheidung e, Familienbuch? buch)
    {
        if (e.Art == Entscheidungsart.Krankheit && e.Person is int n && buch?.PersonNummer(n) is { } p && !buch.IstVolljaehrig(p)) return KrankheitKind;
        return e.Art.ToString();
    }

    /// <summary>Soll diese Art automatisch beantwortet werden? Ohne eigene Wahl gilt der Standard.</summary>
    public static bool Automatisch(string schluessel)
    {
        Laden();
        if (_wahl!.TryGetValue(schluessel, out var a)) return a;
        return Arten.FirstOrDefault(x => x.Schluessel == schluessel).Automatisch;
    }

    public static void Setzen(string schluessel, bool automatisch)
    {
        Laden();
        _wahl![schluessel] = automatisch;
        Speichern();
    }

    /// <summary>Alle Wahlen zurück auf den Standard (der Haken „anhalten“ auch).</summary>
    public static void Zuruecksetzen()
    {
        Laden();
        _wahl!.Clear();
        _anhalten = true;
        Speichern();
    }

    private static void Laden()
    {
        if (_wahl is not null) return;
        _wahl = new Dictionary<string, bool>();
        var datei = new ConfigFile();
        if (datei.Load(Datei) != Error.Ok) return;
        _anhalten = datei.GetValue(Abschnitt, "anhalten", true).AsBool();
        foreach (var (schluessel, _, _, _) in Arten)
            if (datei.HasSectionKey(Abschnitt, schluessel)) _wahl[schluessel] = datei.GetValue(Abschnitt, schluessel).AsBool();
    }

    private static void Speichern()
    {
        var datei = new ConfigFile();
        datei.Load(Datei); // andere Einstellungen behalten
        datei.EraseSection(Abschnitt);
        datei.SetValue(Abschnitt, "anhalten", _anhalten);
        foreach (var (schluessel, automatisch) in _wahl!) datei.SetValue(Abschnitt, schluessel, automatisch);
        datei.Save(Datei);
    }
}
