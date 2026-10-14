using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Partie;

/// <summary>
/// Der Aktionspunkte-Vorrat eines Charakters (wie im Original „Die Gilde").
///
///   • Zu Beginn jeder Runde kommen neue AP dazu (Standard: 4).
///   • Nicht verbrauchte AP bleiben erhalten und werden in die nächste Runde übertragen.
///   • Maßnahmen wie Training, Intrigen oder Privilegien kosten AP.
/// </summary>
public sealed class Aktionspunkte
{
    public int Vorrat { get; private set; }

    public Aktionspunkte(int startVorrat = 0)
    {
        if (startVorrat < 0)
            throw new ArgumentOutOfRangeException(nameof(startVorrat));
        Vorrat = startVorrat;
    }

    /// <summary>Neue Runde: Die AP pro Runde kommen zum Rest aus der Vorrunde dazu.</summary>
    /// <param name="proRunde">Grundwert aus den Partie-Einstellungen.</param>
    /// <param name="bonus">Zusätzliche AP, z. B. durch den 6. Meistergrad oder Gegenstände.</param>
    /// <param name="hoechstens">Obergrenze des Vorrats – was darüber hinausgeht, verfällt.</param>
    public void NeueRunde(int proRunde, int bonus = 0, int hoechstens = int.MaxValue) =>
        Vorrat = Math.Max(Vorrat, Math.Min(hoechstens, Vorrat + Math.Max(0, proRunde + bonus)));

    /// <summary>Nur zum Laden eines Spielstands.</summary>
    internal void Setzen(int vorrat) => Vorrat = Math.Max(0, vorrat);

    public bool Reicht(int kosten) => kosten >= 0 && Vorrat >= kosten;

    /// <summary>Gibt AP aus. Liefert false (und zieht nichts ab), wenn der Vorrat nicht reicht.</summary>
    public bool Ausgeben(int kosten)
    {
        if (!Reicht(kosten))
            return false;
        Vorrat -= kosten;
        return true;
    }
}

/// <summary>
/// Was persönliche Handlungen kosten (Abschnitt „aktionspunkte" in wirtschaft.json, ab Datenversion 10).
/// Die Wirtschaft (Betriebe, Personal, Karren, Wächter, Stand, Liegenschaften) bleibt frei.
/// Wie viele AP je Runde dazukommen, steht in den Partie-Einstellungen.
/// </summary>
public sealed class AktionspunkteDaten
{
    /// <summary>Höchstens so viele AP darf ein Haus ansparen (Standard 12 = drei Jahre).</summary>
    public int Hoechstens { get; init; } = 12;

    /// <summary>Um so viele Sterne steigt ein Talent beim Üben (höchstens einmal je Talent und Jahr).</summary>
    public double TrainingSchritt { get; init; } = 0.5;

    /// <summary>Kosten je Befehl, Schlüssel = Name des Befehls (z. B. „Werben"). Fehlt ein Befehl, ist er frei.</summary>
    public Dictionary<string, int> Kosten { get; init; } = new();

    public int KostenFuer(Befehl befehl) => Kosten.GetValueOrDefault(befehl.GetType().Name);

    public void Pruefen(List<string> f)
    {
        if (Hoechstens < 1) f.Add("aktionspunkte.hoechstens muss mindestens 1 sein.");
        if (TrainingSchritt <= 0) f.Add("aktionspunkte.trainingSchritt muss größer als 0 sein.");
        // Tippfehler fallen sofort auf: Jeder Schlüssel muss ein echter Befehl sein.
        var befehle = typeof(Befehl).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Befehl)) && !t.IsAbstract).Select(t => t.Name).ToHashSet();
        foreach (var (name, kosten) in Kosten)
        {
            if (!befehle.Contains(name)) f.Add($"aktionspunkte.kosten: Einen Befehl „{name}“ gibt es nicht.");
            if (kosten < 0) f.Add($"aktionspunkte.kosten.{name} darf nicht negativ sein.");
            if (kosten > Hoechstens) f.Add($"aktionspunkte.kosten.{name} ist teurer als der höchste Vorrat ({Hoechstens}).");
        }
    }
}

