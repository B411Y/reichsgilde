using Reichsgilde.Kartenbau;

/// <summary>
/// Prüfskript <c>bruecke</c> des Kartenbaus (Klappbrücke Köln, Karte): Köln wird gelesen, Wartezonen sind gerade, Karren kommen über die Brücke,
/// das Deck liegt 2,4 m über dem Spiegel, die Straße ist 6,4 m breit; Karten ohne Brücke über das Gewässer der Klappbrücke melden keine. Die Durchfahrt ist absichtlich nicht
/// festgenagelt: sie ist die Aufgabe des Kerns (Pfeiler im Wasserplan).
/// </summary>
static class BrueckenpruefungTests
{
    static (int Ergebnis, string Text) Lauf(string karte)
    {
        var aus = new StringWriter();
        var ergebnis = Brueckenpruefung.Pruefen(Projektordner.Finden()!, karte, aus);
        return (ergebnis, aus.ToString());
    }

    public static IEnumerable<(string, Action)> Alle()
    {
        yield return ("Brückenprüfung: Köln – Rheinbrücke mit geraden Wartezonen, Karren kommen hinüber, Deck 2,4 m über dem Spiegel", () =>
        {
            var (ergebnis, text) = Lauf("koeln");
            Pruefe.Wahr(ergebnis == 0, "Köln: Ergebnis 0 erwartet.\n" + text);
            Pruefe.Wahr(text.Contains("Wartezone West") && text.Contains("Wartezone Ost") && !text.Contains("NICHT gerade"), "Wartezonen gerade.\n" + text);
            Pruefe.Wahr(text.Contains("erreichbar") && !text.Contains("NICHT erreichbar"), "Karren erreichbar.\n" + text);
            Pruefe.Wahr(text.Contains("Deck über Spiegel 2.4 m"), "Deck 2,4 m über dem Spiegel.\n" + text);
            // Wasserplan wie im Spiel (Klappbrücke): 13 m frei zwischen den Torhauspfeilern, kein Fehlalarm „Durchfahrt NEIN“
            Pruefe.Wahr(text.Contains("größte 13.0 m") && text.Contains("in der Flussmitte: ja") && text.Contains("Klappbrücke: erfüllt"), "Durchfahrt 13 m.\n" + text);
        });
        yield return ("Brückenprüfung: Karten ohne Rhein melden keine Brücke und keinen Fehler", () =>
        {
            foreach (var karte in new[] { "greybox", "probestadt" })
            {
                var (ergebnis, text) = Lauf(karte);
                Pruefe.Wahr(ergebnis == 0 && text.Contains(": 0"), karte + ": keine Brücke über den Rhein.\n" + text);
            }
        });
    }
}
