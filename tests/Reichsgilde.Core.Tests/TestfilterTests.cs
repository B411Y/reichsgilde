// Selbsttest des Test-Filters (Testauswahl.cs, LangsameTests.cs):
// Muster, Schalter, Auswahl, Zusammenfassung – und dass jeder Eintrag der Liste
// langsamer Tests noch auf einen echten Test passt.
static class TestfilterTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Testfilter: Muster ohne Groß- und Kleinschreibung, * für beliebigen Text, irgendwo im Namen", () =>
        {
            Pruefe.Gleich(true, Mustervergleich.Enthaelt("taschendieb", "Fang: Ein TASCHENDIEB auf dem Markt"));
            Pruefe.Gleich(true, Mustervergleich.Enthaelt("Taschen*Markt", "Fang: Ein Taschendieb auf dem Markt"));
            Pruefe.Gleich(false, Mustervergleich.Enthaelt("Markt*Taschen", "Fang: Ein Taschendieb auf dem Markt"));
            Pruefe.Gleich(true, Mustervergleich.Enthaelt("übersicht", "Die Übersicht der Häuser"));
            Pruefe.Gleich(true, Mustervergleich.Enthaelt("a.b", "x a.b y"));   // Punkt ist kein Platzhalter
            Pruefe.Gleich(false, Mustervergleich.Enthaelt("a.b", "x aXb y"));
            Pruefe.Gleich(true, Mustervergleich.Enthaelt("(2)", "Lauf (2) von 3")); // Klammern wörtlich
        }),

        ("Testfilter: Klassenname muss ganz passen, * erlaubt", () =>
        {
            Pruefe.Gleich(true, Mustervergleich.Ganz("diebtests", "DiebTests"));
            Pruefe.Gleich(false, Mustervergleich.Ganz("Dieb", "DiebTests"));
            Pruefe.Gleich(true, Mustervergleich.Ganz("Dieb*", "DiebTests"));
            Pruefe.Gleich(true, Mustervergleich.Ganz("*Tests", "DiebTests"));
            Pruefe.Gleich(false, Mustervergleich.Ganz("*Dieb", "DiebTests"));
        }),

        ("Testfilter: Schalter lesen – Komma trennt Muster, unbekannte und leere Schalter sind ein Fehler", () =>
        {
            var auswahl = Testauswahl.Lesen(new[] { "--filter=Taschendieb, Nachtgang", "--klasse=DiebTests", "--ohne-langsam", "--zeiten=5", "--liste" });
            Pruefe.Gleich(null, auswahl.Fehler);
            Pruefe.Gleich("Taschendieb|Nachtgang", string.Join("|", auswahl.Filter));
            Pruefe.Gleich("DiebTests", string.Join("|", auswahl.Klassen));
            Pruefe.Gleich(true, auswahl.OhneLangsam);
            Pruefe.Gleich(true, auswahl.NurListe);
            Pruefe.Gleich(5, auswahl.Zeiten);
            Pruefe.Gleich(20, Testauswahl.Lesen(new[] { "--zeiten" }).Zeiten);
            Pruefe.Gleich(0, Testauswahl.Lesen(Array.Empty<string>()).Zeiten);
            Pruefe.Wahr(Testauswahl.Lesen(new[] { "--filtr=Dieb" }).Fehler is not null, "Tippfehler im Schalter muss auffallen");
            Pruefe.Wahr(Testauswahl.Lesen(new[] { "--filter=" }).Fehler is not null, "leeres Muster muss auffallen");
            Pruefe.Wahr(Testauswahl.Lesen(new[] { "--filter" }).Fehler is not null, "Muster fehlt");
            Pruefe.Wahr(Testauswahl.Lesen(new[] { "--zeiten=0" }).Fehler is not null, "--zeiten=0 ist keine Anzahl");
            Pruefe.Wahr(Testauswahl.Lesen(new[] { "--liste=ja" }).Fehler is not null, "--liste nimmt keinen Wert");
        }),

        ("Testfilter: Filter passt auf Klasse oder Testname, zusammen mit --klasse gilt beides", () =>
        {
            var dieb = new Testfall("DiebTests", "Fang am Markt", () => { });
            var wache = new Testfall("WacheTests", "Streife fängt den Dieb", () => { });
            var markt = new Testfall("MarktTests", "Preise", () => { });
            var nurFilter = Testauswahl.Lesen(new[] { "--filter=dieb" });
            Pruefe.Gleich(true, nurFilter.Passt(dieb));   // über den Klassennamen
            Pruefe.Gleich(true, nurFilter.Passt(wache));  // über den Testnamen
            Pruefe.Gleich(false, nurFilter.Passt(markt));
            var beides = Testauswahl.Lesen(new[] { "--filter=dieb", "--klasse=WacheTests,Markt*" });
            Pruefe.Gleich(false, beides.Passt(dieb));
            Pruefe.Gleich(true, beides.Passt(wache));
            Pruefe.Gleich(false, beides.Passt(markt));    // Klasse passt, Filter nicht
            Pruefe.Gleich(true, Testauswahl.Lesen(Array.Empty<string>()).Passt(markt));
        }),

        ("Testfilter: Lauf zählt gelaufen, übersprungen und Fehler; --liste führt nichts aus", () =>
        {
            var gelaufen = new List<string>();
            var tests = new List<Testfall>
            {
                new("ATests", "eins", () => gelaufen.Add("eins")),
                new("ATests", "zwei kaputt", () => { gelaufen.Add("zwei"); throw new Exception("absichtlich"); }),
                new("BTests", "drei", () => gelaufen.Add("drei")),
            };

            var text = new StringWriter();
            Pruefe.Gleich(0, Testlauf.Ausfuehren(tests, Testauswahl.Lesen(new[] { "--klasse=BTests" }), text));
            Pruefe.Gleich("drei", string.Join("|", gelaufen));
            Pruefe.Wahr(text.ToString().Contains("Gelaufen: 1, übersprungen: 2"), text.ToString());

            gelaufen.Clear();
            text = new StringWriter();
            Pruefe.Gleich(1, Testlauf.Ausfuehren(tests, Testauswahl.Lesen(new[] { "--filter=zwei", "--zeiten" }), text));
            Pruefe.Wahr(text.ToString().Contains("FEHLER  zwei kaputt") && text.ToString().Contains("1 von 1 Tests fehlgeschlagen"), text.ToString());
            Pruefe.Wahr(text.ToString().Contains("langsamsten Tests"), "--zeiten zeigt die Zeitliste");

            gelaufen.Clear();
            text = new StringWriter();
            Pruefe.Gleich(0, Testlauf.Ausfuehren(tests, Testauswahl.Lesen(new[] { "--liste" }), text));
            Pruefe.Gleich(1, Testlauf.Ausfuehren(tests, Testauswahl.Lesen(new[] { "--liste", "--filter=gibtsnicht" }), new StringWriter()));
            Pruefe.Gleich(0, gelaufen.Count);
            Pruefe.Wahr(text.ToString().Contains("ATests: zwei kaputt"), text.ToString());

            Pruefe.Gleich(1, Testlauf.Ausfuehren(tests, Testauswahl.Lesen(new[] { "--filter=gibtsnicht" }), new StringWriter()));
            Pruefe.Gleich(2, Testlauf.Ausfuehren(tests, Testauswahl.Lesen(new[] { "--unbekannt" }), new StringWriter()));
            Pruefe.Gleich(0, gelaufen.Count);
        }),

        ("Testfilter: --ohne-langsam überspringt die Tests aus LangsameTests, ohne Schalter laufen sie mit", () =>
        {
            Pruefe.Wahr(LangsameTests.Liste.Length > 0, "Die Liste der langsamen Tests ist leer");
            var (klasse, muster, _) = LangsameTests.Liste[0];
            var langsamerName = muster is null ? "irgendein Test" : muster.Replace("*", " ");
            var gelaufen = new List<string>();
            Action schnell = () => gelaufen.Add("schnell");
            var tests = new List<Testfall>
            {
                new(klasse, langsamerName, () => gelaufen.Add("langsam")),
                new("SchnelleBeispielTests", "schnell", schnell),
                new("SchnelleBeispielTests", "schnell", schnell), // zweimal derselbe Testfall läuft auch zweimal
            };
            var text = new StringWriter();
            Pruefe.Gleich(0, Testlauf.Ausfuehren(tests, Testauswahl.Lesen(new[] { "--ohne-langsam" }), text));
            Pruefe.Gleich("schnell|schnell", string.Join("|", gelaufen));
            Pruefe.Wahr(text.ToString().Contains("übersprungen: 1 (davon 1 langsam)"), text.ToString());

            gelaufen.Clear();
            Pruefe.Gleich(0, Testlauf.Ausfuehren(tests, Testauswahl.Lesen(Array.Empty<string>()), new StringWriter()));
            Pruefe.Gleich("langsam|schnell|schnell", string.Join("|", gelaufen));
        }),

        ("Testfilter: jeder Eintrag in LangsameTests trifft genau so viele Tests wie angegeben", () =>
        {
            var alle = Testlauf.KlassenTests(typeof(TestfilterTests).Assembly);
            foreach (var (klasse, muster, anzahl) in LangsameTests.Liste)
            {
                var treffer = alle.Count(t => string.Equals(t.Klasse, klasse, StringComparison.OrdinalIgnoreCase)
                                              && (muster is null || Mustervergleich.Enthaelt(muster, t.Name)));
                Pruefe.Gleich(anzahl, treffer, $"LangsameTests: „{klasse}{(muster is null ? "" : ": " + muster)}“ (umbenannt oder Muster zu weit?)");
            }
        }),
    };
}
