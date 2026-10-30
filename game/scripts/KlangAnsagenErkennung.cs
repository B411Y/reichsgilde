using System.Text.RegularExpressions;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Erkennt in den Textmeldungen des Kerns die Ereignisse, die der Haushofmeister ansagt (<see cref="KlangAnsagen"/>), und prüft, ob sie das
/// <b>eigene Haus</b> betreffen (Mehrspieler: fremde Häuser sagt er nie an). Der Kern meldet nur Text (<c>Stadtwirtschaft.Meldung</c>), darum
/// hängt die Erkennung an den Wortlauten; ändert jemand einen Satz im Kern, schlägt der Test <c>AnsagenErkennungTests</c> an (er lässt echte
/// Meldungen aus einer Partie erkennen). Ohne Godot, damit der Test die Datei mitübersetzen kann.
///
/// Wer betroffen ist, steht je Muster in der Gruppe <c>wer</c>: der Name eines Betriebs oder der Inhaber des Hauses. Familienereignisse
/// (Tod, Krankheit, Geburt, Hochzeit) tragen das Konto im Familienbuch (<c>Familien.DiesesJahr</c>), dort wird der Text wiedergefunden.
/// Die Ereignisse, die nicht als Text kommen (Aufstieg im Stand, Wahl, Ratssitzung, Jahresende), löst <c>Hauptszene</c> direkt aus.
/// </summary>
public static class KlangAnsagenErkennung
{
    /// <summary>Die Kennungen der Ereignisse – wie in <c>game/daten/ansagen.json</c>.</summary>
    public static class Id
    {
        public const string TodOberhaupt = "tod_oberhaupt", TodFamilie = "tod_familie", Feuer = "feuer", PestStadt = "pest_stadt",
            Krankheit = "krankheit", Ueberfall = "ueberfall", Diebstahl = "diebstahl", Anklage = "anklage", Schulden = "schulden",
            WahlVerloren = "wahl_verloren", WahlGewonnen = "wahl_gewonnen", StandAufstieg = "stand_aufstieg",
            Meisterpruefung = "meisterpruefung", Geburt = "geburt", Heirat = "heirat", Ratssitzung = "ratssitzung",
            KasseKnapp = "kasse_knapp", LagerLeer = "lager_leer", LagerVoll = "lager_voll", Jahresende = "jahresende";
    }

    /// <summary>Wen eine Meldung trifft: ein Betrieb (Name), ein Haus (Inhaber), die Familie (Familienbuch) oder die ganze Stadt.</summary>
    private enum Betroffen { Betrieb, Haus, Familie, Stadt }

    private sealed record Muster(string Ereignis, Regex Text, Betroffen Art);

    private static Muster M(string ereignis, string muster, Betroffen art) =>
        new(ereignis, new Regex(muster, RegexOptions.CultureInvariant | RegexOptions.Singleline), art);

    // Reihenfolge zählt: das erste passende Muster gewinnt.
    private static readonly Muster[] Erkennungen =
    {
        // Familie (Familienbuch.Ereignis, Gesundheitswesen)
        M(Id.TodOberhaupt, @"gestorben\. .+ \(\d+ Jahre\)\. Ämter und Gunst im Rat beginnen von vorn\.$", Betroffen.Familie),
        M(Id.TodFamilie, @"^Trauer im Haus ", Betroffen.Familie),
        M(Id.Krankheit, @" ist krank: | hat die Pest! Ohne Hilfe", Betroffen.Familie),
        M(Id.Geburt, @"^Freude im Haus ", Betroffen.Familie),
        M(Id.Heirat, @"^Hochzeit im Haus ", Betroffen.Familie),
        // Stadt
        M(Id.PestStadt, @"^Die Pest ist in der Stadt!", Betroffen.Stadt),
        // Feuer: Wohnhaus (Unterwelt, Entwickler), Werkstatt (Anschlag, Entwickler)
        M(Id.Feuer, @"^Feuer am (?<haus>.+?) von (?<wer>[^!]+)!", Betroffen.Haus),
        M(Id.Feuer, @"^Anschlag auf (?<wer>.+?): Feuer in der Werkstatt!", Betroffen.Betrieb),
        M(Id.Feuer, @"^Feuer in (?<wer>.+?)! ", Betroffen.Betrieb),
        // Diebstahl: Wohnhaus, Betrieb (nur gelungene Einbrüche)
        M(Id.Diebstahl, @"^Einbruch ins (?<haus>.+?) von (?<wer>[^:]+): .+ gestohlen\.", Betroffen.Haus),
        M(Id.Diebstahl, @"^Einbruch in (?<wer>.+?): .+ gestohlen\.", Betroffen.Betrieb),
        // Überfall mit Beute: Karren (Zufall, Hinterhalt), Schiff (Piraten)
        M(Id.Ueberfall, @"^Überfall auf dem Weg von (?<wer>.+?) zum Markt: .+ erbeutet ", Betroffen.Betrieb),
        M(Id.Ueberfall, @"^Überfall auf dem Rückweg nach (?<wer>.+?): .+ erbeutet ", Betroffen.Betrieb),
        M(Id.Ueberfall, @"^Hinterhalt auf .+? von (?<wer>.+?): Wegelagerer erbeuten ", Betroffen.Betrieb),
        M(Id.Ueberfall, @"^.+ überfallen .+ \((?<wer>[^)]+)\) .+ und erbeuten ", Betroffen.Haus),
        // Gericht, Geld, Rang
        M(Id.Anklage, @"^.+? klagt (?<wer>.+?) wegen .+ an \(", Betroffen.Haus),
        M(Id.Schulden, @"^Der Kämmerer mahnt (?<wer>.+?): ", Betroffen.Haus),
        M(Id.Meisterpruefung, @"^(?<wer>.+) legt die Meisterprüfung ab: ", Betroffen.Haus),
        // Betriebe
        M(Id.KasseKnapp, @"^(?<wer>.+?): Verwalter kauft nur das Nötigste – die Kasse ist knapp\.$", Betroffen.Betrieb),
        M(Id.LagerLeer, @"^(?<wer>.+?): Es fehlt .+ – die Arbeit ruht\.$", Betroffen.Betrieb),
        M(Id.LagerVoll, @"^(?<wer>.+?): Das Lager für .+ ist voll – die Arbeit ruht\.$", Betroffen.Betrieb),
    };

    /// <summary>
    /// Welches Ereignis ist das, und betrifft es das Haus <paramref name="spieler"/>? <c>null</c>, wenn die Meldung nichts Ansagbares ist
    /// oder ein fremdes Haus trifft.
    /// </summary>
    public static string? Erkennen(string text, Stadtwirtschaft w, Konto spieler)
    {
        foreach (var m in Erkennungen)
        {
            var treffer = m.Text.Match(text);
            if (!treffer.Success) continue;
            var wer = treffer.Groups["wer"].Value;
            var eigen = m.Art switch
            {
                Betroffen.Stadt => true,
                Betroffen.Haus => wer == spieler.Inhaber,
                Betroffen.Betrieb => w.Betriebe.Any(b => b.Konto == spieler && b.Name == wer),
                _ => FamilieEigen(text, w, spieler),
            };
            return eigen ? m.Ereignis : null;
        }
        return null;
    }

    /// <summary>Hat das Familienbuch diesen Text für das Konto des Spielers eingetragen? (Der Eintrag entsteht kurz vor der Meldung.)</summary>
    private static bool FamilieEigen(string text, Stadtwirtschaft w, Konto spieler)
    {
        var eintrag = w.Familien.DiesesJahr.LastOrDefault(e => e.Text == text) ?? w.Familien.LetztesJahr.LastOrDefault(e => e.Text == text);
        return eintrag is not null && eintrag.Konto == spieler;
    }

    /// <summary>Ist das Oberhaupt des Hauses eine Frau? Dann heißt die Anrede „Meine Herrin“ (sonst „Mein Herr“).</summary>
    public static bool Herrin(Stadtwirtschaft w, Konto spieler) =>
        w.Familien.Von(spieler)?.Oberhaupt.Geschlecht == Geschlecht.Frau;
}
