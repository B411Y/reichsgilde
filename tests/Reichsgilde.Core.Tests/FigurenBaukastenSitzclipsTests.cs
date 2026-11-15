using Reichsgilde;

/// <summary>Formschlüssel „Sitzen“ in den sitzenden Arbeitsclips (Rock fällt wie im Clip „Sitzen“, Entscheidung Teamleiter 14.11.2026).</summary>
public static class FigurenBaukastenSitzclipsTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Figuren-Baukasten: Formschlüssel „Sitzen“ auch in den sitzenden Arbeitsclips, nicht in den stehenden", () =>
        {
            foreach (var klip in new[] { "Feinarbeit", "Trinken", "Schreiben", "Beten", "Zaehlen", "Putzen", "Rechnen", "Kutschieren" })
                Pruefe.Gleich(1f, FigurenBaukasten.SitzSchluessel(klip), klip);
            foreach (var klip in new[] { "Haemmern", "Blasebalg", "Schleifen", "Hobeln", "Saegen", "Drechseln", "Gatter", "Behauen", "Bildhauen",
                                         "Moersern", "Ruehren", "Destillieren", "Mischen", "Pressen", "Zapfen", "Laute", "Kerzenziehen", "Schoepfen",
                                         "Predigen", "Knien", "Knacken", "Schlafen", "Fechten", "Wiegen", "Hueten", "Eilen", "Gebeugt" })
                Pruefe.Gleich(0f, FigurenBaukasten.SitzSchluessel(klip), klip);
        }),
    };
}
