using Reichsgilde.Core.Partie;

/// <summary>Tests der Messhilfe „messen meldungen“ (Meldungsmessung.cs): Einordnung der Texte nach Art und die vorgeschlagene Stufe.</summary>
static class MeldungsmessungTests
{
    public static IEnumerable<(string, Action)> Alle()
    {
        yield return ("Meldungsmessung: typische Texte des Kerns landen in ihrer Art", () =>
        {
            (string Text, string Art)[] faelle =
            {
                ("Trauer im Haus Eisenbart: Peter, Ehemann von Caerwyn, ist mit 61 Jahren gestorben.", "Tod"),
                ("Freude im Haus Eisenbart: Anna und Hans bekommen einen Sohn, Jakob.", "Geburt"),
                ("Hochzeit im Haus Eisenbart: Hans Eisenbart heiratet Anna, geb. Färber (Mensch, 22 Jahre).", "Heirat"),
                ("Hennig Eisenbart ist krank: Husten (Rhetorik −1) – bis zum Jahresende.", "Krankheit"),
                ("Die Pest ist in der Stadt! In diesem Jahr fehlen 20 % der Arbeitskräfte.", "Pest"),
                ("Neuer Charakterzug für Hans Eisenbart: Narbe – Eine Narbe erzählt, was er überlebt hat.", "Familie"),
                ("Haus Eisenbart ist nun Bürger! (Gebühr 500 Gulden)", "Stand"),
                ("Haus Eisenbart legt die Meisterprüfung ab: Schmied im 2. Grad.", "Stand"),
                ("Wahl 1405 – Bürgermeister: Haus Brendel bleibt im Amt (3:2).", "Rat"),
                ("Ratssitzung 1405: Antrag von Haus Brendel auf Steuer 8 % – angenommen.", "Steuer"),
                ("Gerichtstag 1405: Das Verfahren Haus Brendel gegen Haus Eisenbart wird eingestellt.", "Gericht"),
                ("Der Spitzel von Haus Brendel berichtet über Haus Eisenbart: nichts Neues (insgesamt 2).", "Spitzel"),
                ("In der dunklen Ecke von Schänke Brendel hört Haus Brendel Gerüchte: 2 neue Beweise.", "Spitzel"),
                ("Einbruch in Schmiede Eisenbart: 3 Eisen (Wert 30) gestohlen.", "Diebstahl"),
                ("Eure Diebe haben in Schmiede Brendel Ware für 40 erbeutet – der Hehler zahlt 20.", "Diebstahl"),
                ("Blitzschlag! Die Schmiede (Haus Eisenbart) brennt.", "Blitz"),
                ("Schmiede Eisenbart: Nach dem Brand wird repariert – die Arbeit ruht.", "Blitz"),
                ("Schmiede Eisenbart: Es fehlt Eisen – die Arbeit ruht.", "Stillstand"),
                ("Der Kämmerer mahnt Haus Eisenbart: 300 Gulden Schulden. Zahlt bis zum Jahresende.", "Schulden"),
                ("Haus Eisenbart leiht 1.000 Gulden bei Haus Brendel – 8 % Zins, 3 Jahre.", "Bank"),
                ("Unfall in Schmiede Eisenbart: Lehrling Nr. 2 hat sich verletzt und fällt für den Rest des Tages aus.", "Personal"),
                ("Schmiede Eisenbart: Lehrling Nr. 2 ist nun Geselle (Brief 50 Gulden, Lohn ab jetzt 300 Gulden im Jahr).", "Personal"),
                ("Handkarren (Schmiede Eisenbart) am Markt: 4 Dolch verkauft.", "Karren"),
                ("Möwe (Haus Eisenbart) ist zurück: 300 Gulden.", "Schiff"),
                ("Frühling 1404: die Erdwege sind aufgeweicht.", "Jahreszeit"),
                ("Festmarkt: Herbstmarkt ist vorbei.", "Jahreszeit"),
                ("Haus Eisenbart baut eine Schmiede.", "Bau"),
                ("Haus Eisenbart kauft Schmiede am Tor für 3.000 Gulden.", "Besitz"),
                ("Haus Eisenbart kauft in St. Martin einen Ablass.", "Kirche"),
                ("Etwas ganz Neues.", "Sonstiges"),
            };
            foreach (var (text, art) in faelle)
                Pruefe.Gleich(art, Meldungsmessung.Einordnen(text), text);
        });

        yield return ("Meldungsmessung: Spitzelberichte gehören dem Auftraggeber, nicht dem Belauschten", () =>
        {
            Pruefe.Gleich("Haus Brendel", Meldungsmessung.Auftraggeber("Der Spitzel von Haus Brendel berichtet über Haus Eisenbart: nichts Neues (insgesamt 2)."));
            Pruefe.Gleich("Haus Brendel", Meldungsmessung.Auftraggeber("In der dunklen Ecke von Schänke Eisenbart hört Haus Brendel Gerüchte: 1 neuer Beweis."));
            Pruefe.Gleich<string?>(null, Meldungsmessung.Auftraggeber("Gerichtstag 1405: nichts."));
        });

        yield return ("Meldungsmessung: Stufe nach Art und Bezug – fremde Häuser nur im Verlauf, Pest und Jahreszeit für alle", () =>
        {
            Pruefe.Gleich(Meldungsstufe.Brief, Meldungsmessung.Stufe("Tod", Meldungsbezug.Eigen));
            Pruefe.Gleich(Meldungsstufe.Verlauf, Meldungsmessung.Stufe("Tod", Meldungsbezug.Fremd));
            Pruefe.Gleich(Meldungsstufe.Brief, Meldungsmessung.Stufe("Pest", Meldungsbezug.Stadt));
            Pruefe.Gleich(Meldungsstufe.Siegel, Meldungsmessung.Stufe("Jahreszeit", Meldungsbezug.Stadt));
            Pruefe.Gleich(Meldungsstufe.Siegel, Meldungsmessung.Stufe("Stillstand", Meldungsbezug.Eigen));
            Pruefe.Gleich(Meldungsstufe.Verlauf, Meldungsmessung.Stufe("Bau", Meldungsbezug.Eigen));
            Pruefe.Gleich(Meldungsstufe.Verlauf, Meldungsmessung.Stufe("KI-Zug", Meldungsbezug.Fremd));
            // Jede Art hat eine Stufe, und „Sonstiges“ ist die letzte (Ausgabe in dieser Reihenfolge)
            Pruefe.Gleich("Sonstiges", Meldungsmessung.Arten[^1]);
        });

        yield return ("Meldungsmessung: die Rollen Wirt, Schmied und Ausbau", () =>
        {
            Pruefe.Gleich("Wirt", Meldungsmessung.Aufbau("Wirt", "greybox", 1, 1).Beruf);
            Pruefe.Gleich(Bezugsart.EinBetrieb, Meldungsmessung.Aufbau("Schmied", "greybox", 1, 1).Bezug);
            Pruefe.Gleich(Bezugsart.WieKi, Meldungsmessung.Aufbau("Ausbau", "greybox", 1, 1).Bezug);
            Pruefe.Wirft(() => Meldungsmessung.Aufbau("Bader", "greybox", 1, 1));
        });
    }
}
