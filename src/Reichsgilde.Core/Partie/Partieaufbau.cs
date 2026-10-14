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
/// <param name="Beruf">Startberuf: bestimmt die erste Werkstatt (Schmied → Gießerei, Tischler → Tischlerei).</param>
/// <param name="Rivalen">Wie viele KI-Häuser mitspielen: 1 = Haus Eisenbart (Schmied), 2 = dazu Haus Eichwald (Tischler).</param>
public sealed record NeuePartie(
    string Familienname = "Familie Hammerschmid",
    double? Startvermoegen = null,
    KiCharakter Rivale = KiCharakter.Ausgewogen,
    Volk Volk = Volk.Mensch,
    Geschlecht Geschlecht = Geschlecht.Mann,
    string? Vorname = null,
    string Beruf = "Schmied",
    int Rivalen = 2);

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
/// Stellt eine neue Partie auf: das Haus des Spielers mit der Werkstatt seines Startberufs, Haus Eisenbart
/// (Zwerge, Schmiede: Gießerei und Grube) und – bei zwei Rivalen – Haus Eichwald (Elben, Tischler: Tischlerei
/// und Holzschlag), beide von der KI geführt. Alle Oberhäupter beginnen ledig.
/// </summary>
public static class Partieaufbau
{
    public const string Rivalenname = "Haus Eisenbart";

    /// <summary>Der zweite KI-Rivale: ein Elbenhaus von Tischlern.</summary>
    public const string ZweiterRivale = "Haus Eichwald";
    public const Volk ZweiterRivaleVolk = Volk.Elb;
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
        if (name is Rivalenname or ZweiterRivale)
            throw new ArgumentException($"„{name}“ ist schon der Name eines Rivalen.");
        if (neu.Rivalen is < 1 or > 2)
            throw new ArgumentException("Es spielen ein oder zwei KI-Häuser mit.");

        var simulation = new Simulation(einstellungen, startzeit, daten);
        var w = simulation.Wirtschaft!;

        if (neu.Volk is Volk.Ork or Volk.Oger)
            throw new ArgumentException("Orks und Oger sind nicht spielbar.");
        var startArt = daten.Berufe.TryGetValue(neu.Beruf, out var startberuf) ? startberuf.Werkstatt
            : neu.Beruf == "Schmied" ? Betriebsart.Giesserei // ältere Daten ohne „berufe"
            : throw new ArgumentException($"Den Beruf „{neu.Beruf}“ gibt es nicht.");
        var spieler = w.KontoEroeffnen(name, startvermoegen: neu.Startvermoegen);
        var werkstatt = w.BetriebEroeffnen($"{daten.Art(startArt).Name} am Weinmarkt", startArt, spieler);
        w.Einstellen(werkstatt, "Geselle", 2);

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

        // Haus Eichwald: Elben, Tischler – mit Tischlerei und Holzschlag, ebenfalls von der KI geführt.
        if (neu.Rivalen >= 2 && daten.Berufe.TryGetValue("Tischler", out var tischler))
        {
            var eichwald = w.KontoEroeffnen(ZweiterRivale);
            if (w.Familien.IstAktiv)
                w.Familien.Gruenden(eichwald, ZweiterRivaleVolk, Geschlecht.Frau);
            var tischlerei = w.BetriebEroeffnen("Tischlerei Eichwald", tischler.Werkstatt, eichwald);
            w.Einstellen(tischlerei, "Geselle", 2);
            var holzschlag = w.BetriebEroeffnen("Holzschlag Eichwald", tischler.Umland, eichwald);
            w.Einstellen(holzschlag, bergleute, 3);
            w.KiAnmelden(eichwald, ProfilFuer(neu.Rivale));
        }
        return (simulation, spieler);
    }
}
