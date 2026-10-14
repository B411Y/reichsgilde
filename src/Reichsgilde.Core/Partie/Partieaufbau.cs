using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Partie;

/// <summary>Was der Spieler im Menü „Neue Partie" einstellt – zusätzlich zu den <see cref="Partieeinstellungen"/>.</summary>
/// <param name="Familienname">Name des eigenen Hauses.</param>
/// <param name="Startvermoegen">Gulden zu Beginn; null = Wert aus wirtschaft.json.</param>
/// <param name="Rivale">Charakter des KI-Rivalen.</param>
/// <param name="Volk">Volk des eigenen Hauses.</param>
/// <param name="Geschlecht">Patriarch oder Matriarchin.</param>
/// <param name="Vorname">Vorname des Oberhaupts; null = ein zufälliger Name des Volkes.</param>
public sealed record NeuePartie(
    string Familienname = "Familie Hammerschmid",
    double? Startvermoegen = null,
    KiCharakter Rivale = KiCharakter.Ausgewogen,
    Volk Volk = Volk.Mensch,
    Geschlecht Geschlecht = Geschlecht.Mann,
    string? Vorname = null);

/// <summary>Wie der KI-Rivale spielt – im Menü als Schwierigkeit wählbar.</summary>
public enum KiCharakter
{
    /// <summary>Viel Reserve, fürchtet Raub, kauft nur, was sich schnell bezahlt macht.</summary>
    Vorsichtig,
    Ausgewogen,
    /// <summary>Wenig Reserve, nimmt Risiken, expandiert früh.</summary>
    Wagemutig,
}

/// <summary>
/// Stellt eine neue Partie auf: das Haus des Spielers mit einer Gießerei und Haus Eisenbart, ein Zwergenhaus
/// mit Gießerei und Grube, geführt von der KI. Beide Oberhäupter beginnen ledig.
/// (Später: mehrere Städte, Berufe und Rivalen.)
/// </summary>
public static class Partieaufbau
{
    public const string Rivalenname = "Haus Eisenbart";
    public const Volk RivalenVolk = Volk.Zwerg;

    public static KiProfil ProfilFuer(KiCharakter charakter) => charakter switch
    {
        KiCharakter.Vorsichtig => KiProfil.Standard with { Reserve = 2500, Vorsicht = 2, MaxAmortisationJahre = 4, Gaunerei = 0 },
        KiCharakter.Wagemutig => KiProfil.Standard with { Reserve = 800, Vorsicht = 0.7, MaxAmortisationJahre = 8, Gaunerei = 2 },
        _ => KiProfil.Standard,
    };

    /// <returns>Die Simulation und das Konto des Spielers.</returns>
    /// <param name="startzeit">Abweichender Startzeitpunkt (zum Testen); ohne Angabe Frühling 1400, 06:00.</param>
    public static (Simulation Simulation, Konto Spieler) Erstellen(Partieeinstellungen einstellungen, Wirtschaftsdaten daten, NeuePartie neu,
        Spielzeit? startzeit = null)
    {
        var name = string.IsNullOrWhiteSpace(neu.Familienname) ? "Familie Hammerschmid" : neu.Familienname.Trim();
        if (name == Rivalenname)
            throw new ArgumentException($"„{Rivalenname}“ ist schon der Name des Rivalen.");

        var simulation = new Simulation(einstellungen, startzeit, daten);
        var w = simulation.Wirtschaft!;

        if (neu.Volk is Volk.Ork or Volk.Oger)
            throw new ArgumentException("Orks und Oger sind nicht spielbar.");
        var spieler = w.KontoEroeffnen(name, startvermoegen: neu.Startvermoegen);
        var giesserei = w.BetriebEroeffnen("Gießerei am Weinmarkt", Betriebsart.Giesserei, spieler);
        giesserei.Produkt = "Silberring";
        w.Einstellen(giesserei, "Geselle", 2);

        var rivale = w.KontoEroeffnen(Rivalenname);
        if (w.Familien.IstAktiv)
        {
            w.Familien.Gruenden(spieler, neu.Volk, neu.Geschlecht, neu.Vorname);
            w.Familien.Gruenden(rivale, RivalenVolk, Geschlecht.Mann);
        }
        var rivalenGiesserei = w.BetriebEroeffnen("Gießerei Eisenbart", Betriebsart.Giesserei, rivale);
        rivalenGiesserei.Produkt = "Rapier";
        w.Einstellen(rivalenGiesserei, "Geselle", 2);
        var grube = w.BetriebEroeffnen("Grube Eisenbart", Betriebsart.Mine, rivale);
        // Orks nur, wenn das Startstatut sie im Umland erlaubt (Statut B oder C) – sonst Bergleute.
        var bergleute = daten.Arbeitskraefte.TryGetValue("Ork", out var ork) && ork.IstErlaubt(w.Statut, Arbeitsort.Umland) ? "Ork" : "Bergmann";
        w.Einstellen(grube, bergleute, 3);

        // Ab jetzt führt die KI Haus Eisenbart: Produkt, Förderung, Personal, Eskorten, Karren, Stand, Expansion.
        w.KiAnmelden(rivale, ProfilFuer(neu.Rivale));
        return (simulation, spieler);
    }
}
