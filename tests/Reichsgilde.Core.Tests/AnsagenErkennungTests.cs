using Reichsgilde;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Entwickler;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests der Ansagen des Haushofmeisters (Ton Schritt 3, Spielordner <c>KlangAnsagenErkennung.cs</c>, hier mitübersetzt): Die Erkennung hängt
/// an den Wortlauten der Kernmeldungen. Diese Tests lassen <b>echte</b> Meldungen aus einer Partie erkennen (Entwicklerbefehle lösen Tod, Geburt,
/// Krankheit, Heirat, Einbruch, Feuer, Hausbrand aus) – ändert jemand einen Satz im Kern, fällt es hier auf. Außerdem: fremde Häuser lösen nichts
/// aus (Mehrspieler), und die Satzliste <c>game/daten/ansagen.json</c> ist vollständig (jedes Ereignis, Namen der Dateien, keine Namen im Satz).
/// </summary>
static class AnsagenErkennungTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static (Simulation Sim, Konto Spieler, Konto Rivale, List<string> Meldungen) Partie()
    {
        var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos },
            Daten.Value, new NeuePartie());
        for (var i = 0; i < 300; i++) sim.Tick();
        sim.EntwicklerErlaubt = true;
        var meldungen = new List<string>();
        sim.Wirtschaft!.Meldung += meldungen.Add;
        var rivale = sim.Wirtschaft.Konten.First(k => k != spieler && k.IstAktiv);
        return (sim, spieler, rivale, meldungen);
    }

    /// <summary>Führt den Befehl aus und gibt zurück, was der Haushofmeister des Spielers zu den entstandenen Meldungen ansagen würde.</summary>
    static List<string> Angesagt(Simulation sim, Konto spieler, List<string> meldungen, Befehl befehl)
    {
        meldungen.Clear();
        var e = sim.Bote.SofortAusfuehren(spieler, befehl);
        Pruefe.Wahr(e.Erfolg, $"{befehl.GetType().Name} abgelehnt: {e.Meldung}");
        return meldungen.Select(m => KlangAnsagenErkennung.Erkennen(m, sim.Wirtschaft!, spieler)).OfType<string>().ToList();
    }

    static void Genau(string erwartet, List<string> ist) =>
        Pruefe.Wahr(ist.Count == 1 && ist[0] == erwartet, $"Erwartet nur „{erwartet}“, erkannt: [{string.Join(", ", ist)}]");

    static string DateiText() => File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
        "game", "daten", "ansagen.json"));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Ansagen: Echte Familienmeldungen des Kerns – Geburt, Heirat, Krankheit, Pest, Tod, Erbfall", () =>
        {
            var (sim, spieler, _, meldungen) = Partie();
            var f = sim.Wirtschaft!.Familien.Von(spieler)!;
            Genau(KlangAnsagenErkennung.Id.Heirat, Angesagt(sim, spieler, meldungen, new EntwicklerHeirat()));
            Genau(KlangAnsagenErkennung.Id.Geburt, Angesagt(sim, spieler, meldungen, new EntwicklerGeburt(Geschlecht.Mann)));
            var kind = f.Mitglieder[^1];
            var name = sim.Wirtschaft.Gesundheit.Daten.Krankheiten.Keys.First(k => k != "Pest");
            Genau(KlangAnsagenErkennung.Id.Krankheit, Angesagt(sim, spieler, meldungen, new EntwicklerKrankheit(f.Oberhaupt.Nummer, name)));
            Genau(KlangAnsagenErkennung.Id.Krankheit, Angesagt(sim, spieler, meldungen, new EntwicklerKrankheit(kind.Nummer, "Pest")));
            Genau(KlangAnsagenErkennung.Id.TodFamilie, Angesagt(sim, spieler, meldungen, new EntwicklerTod(kind.Nummer)));
            // Erbfall: mit volljährigem Kind als Erbe (sonst erlischt das Haus ohne Ansage)
            sim.Wirtschaft.Familien.KindAufnehmen(spieler, sim.Wirtschaft.Familien.VolkDaten(f.Volk).Volljaehrig + 1, Geschlecht.Frau);
            Genau(KlangAnsagenErkennung.Id.TodOberhaupt, Angesagt(sim, spieler, meldungen, new EntwicklerTod(f.Oberhaupt.Nummer)));
            Pruefe.Gleich(true, KlangAnsagenErkennung.Herrin(sim.Wirtschaft, spieler)); // die Erbin ist jetzt das Oberhaupt: „Meine Herrin“
        }),

        ("Ansagen: Echte Meldungen von Einbruch, Feuer in der Werkstatt und Hausbrand – nur für das eigene Haus", () =>
        {
            var (sim, spieler, rivale, meldungen) = Partie();
            var b = sim.Wirtschaft!.Betriebe.First(x => x.Konto == spieler && x.IstWerkstatt && x.Zutaten.Count > 0);
            var fremd = sim.Wirtschaft.Betriebe.First(x => x.Konto == rivale && x.IstWerkstatt && x.Zutaten.Count > 0);
            sim.Bote.SofortAusfuehren(spieler, new EntwicklerZutaten(b.Name));
            sim.Bote.SofortAusfuehren(spieler, new EntwicklerZutaten(fremd.Name));
            Genau(KlangAnsagenErkennung.Id.Diebstahl, Angesagt(sim, spieler, meldungen, new EntwicklerDiebstahl(b.Name)));
            Genau(KlangAnsagenErkennung.Id.Feuer, Angesagt(sim, spieler, meldungen, new EntwicklerFeuer(b.Name)));
            Genau(KlangAnsagenErkennung.Id.Feuer, Angesagt(sim, spieler, meldungen, new EntwicklerHausbrand()));
            // fremdes Haus: nichts
            Pruefe.Gleich(0, Angesagt(sim, spieler, meldungen, new EntwicklerDiebstahl(fremd.Name)).Count);
            Pruefe.Gleich(0, Angesagt(sim, spieler, meldungen, new EntwicklerFeuer(fremd.Name)).Count);
            Pruefe.Gleich(0, Angesagt(sim, spieler, meldungen, new EntwicklerHausbrand(rivale.Inhaber)).Count);
            Pruefe.Gleich(0, Angesagt(sim, spieler, meldungen, new EntwicklerGeburt(Geschlecht.Frau, rivale.Inhaber)).Count);
            Pruefe.Gleich(0, Angesagt(sim, spieler, meldungen, new EntwicklerHeirat(null, rivale.Inhaber)).Count);
            // und umgekehrt hört der Rivale seine eigenen Ereignisse (gleicher Text, anderer Spieler)
            meldungen.Clear();
            sim.Bote.SofortAusfuehren(spieler, new EntwicklerFeuer(fremd.Name));
            Pruefe.Gleich(KlangAnsagenErkennung.Id.Feuer, KlangAnsagenErkennung.Erkennen(meldungen[0], sim.Wirtschaft, rivale));
        }),

        ("Ansagen: Wortlaute der übrigen Kernmeldungen (Überfall, Kasse, Lager, Schulden, Anklage, Meisterprüfung, Pest) mit eigenen und fremden Namen", () =>
        {
            var (sim, spieler, rivale, _) = Partie();
            var w = sim.Wirtschaft!;
            var b = w.Betriebe.First(x => x.Konto == spieler);
            var fremd = w.Betriebe.First(x => x.Konto == rivale);
            string? E(string text) => KlangAnsagenErkennung.Erkennen(text, w, spieler);
            // eigen
            Pruefe.Gleich(KlangAnsagenErkennung.Id.KasseKnapp, E($"{b.Name}: Verwalter kauft nur das Nötigste – die Kasse ist knapp."));
            Pruefe.Gleich(KlangAnsagenErkennung.Id.LagerLeer, E($"{b.Name}: Es fehlt Holz – die Arbeit ruht."));
            Pruefe.Gleich(KlangAnsagenErkennung.Id.LagerVoll, E($"{b.Name}: Das Lager für Holz ist voll – die Arbeit ruht."));
            Pruefe.Gleich(KlangAnsagenErkennung.Id.Ueberfall, E($"Überfall auf dem Weg von {b.Name} zum Markt: Eine wilde Rotte (3 Räuber) erbeutet 4 Holz (Wert 40 Gulden)."));
            Pruefe.Gleich(KlangAnsagenErkennung.Id.Ueberfall, E($"Überfall auf dem Rückweg nach {b.Name}: Eine Bande „Wölfe“ (3 Räuber) erbeutet 4 Holz (Wert 40 Gulden)."));
            Pruefe.Gleich(KlangAnsagenErkennung.Id.Ueberfall, E($"Hinterhalt auf Karren 1 von {b.Name}: Wegelagerer erbeuten 4 Holz (Wert 40)."));
            Pruefe.Gleich(KlangAnsagenErkennung.Id.Ueberfall, E($"Seeräuber überfallen Kogge (Elbe) ({spieler.Inhaber}) auf dem Weg nach Lübeck und erbeuten 4 Holz (Wert 40 Gulden)."));
            Pruefe.Gleich(KlangAnsagenErkennung.Id.Schulden, E($"Der Kämmerer mahnt {spieler.Inhaber}: 500 Gulden Schulden."));
            Pruefe.Gleich(KlangAnsagenErkennung.Id.Anklage, E($"{rivale.Inhaber} klagt {spieler.Inhaber} wegen Diebstahl an (2 Beweise). Es richten A, B."));
            Pruefe.Gleich(KlangAnsagenErkennung.Id.Meisterpruefung, E($"{spieler.Inhaber} legt die Meisterprüfung ab: Schmied im 2. Grad."));
            Pruefe.Gleich(KlangAnsagenErkennung.Id.PestStadt, E("Die Pest ist in der Stadt! In diesem Jahr fehlen 20 % der Arbeitskräfte."));
            // fremd: nichts (außer der Pest, die die ganze Stadt trifft)
            Pruefe.Wahr(E($"{fremd.Name}: Verwalter kauft nur das Nötigste – die Kasse ist knapp.") is null, "Fremde Kasse gemeldet.");
            Pruefe.Wahr(E($"{fremd.Name}: Es fehlt Holz – die Arbeit ruht.") is null, "Fremdes Lager gemeldet.");
            Pruefe.Wahr(E($"Überfall auf dem Weg von {fremd.Name} zum Markt: Eine wilde Rotte (3 Räuber) erbeutet 4 Holz (Wert 40 Gulden).") is null, "Fremder Überfall gemeldet.");
            Pruefe.Wahr(E($"Der Kämmerer mahnt {rivale.Inhaber}: 500 Gulden Schulden.") is null, "Fremde Schulden gemeldet.");
            Pruefe.Wahr(E($"{spieler.Inhaber} klagt {rivale.Inhaber} wegen Diebstahl an (2 Beweise). Es richten A, B.") is null, "Eigene Klage gegen andere gemeldet.");
            Pruefe.Wahr(E($"{rivale.Inhaber} legt die Meisterprüfung ab: Schmied im 2. Grad.") is null, "Fremde Meisterprüfung gemeldet.");
            // misslungene Taten und Gewöhnliches bleiben still
            Pruefe.Wahr(E($"Einbruch in {b.Name} – doch das Lager war leer.") is null, "Leerer Einbruch gemeldet.");
            Pruefe.Wahr(E($"Überfall auf dem Weg von {b.Name} zum Markt: Die Eskorte schlägt eine wilde Rotte (3 Räuber) in die Flucht.") is null, "Abgewehrter Überfall gemeldet.");
            Pruefe.Wahr(E($"{b.Name}: Umbau (Stufe 2: Esse) – die Arbeit ruht.") is null, "Umbau gemeldet.");
            Pruefe.Wahr(E("Es ist Mittag.") is null, "Gewöhnliche Meldung erkannt.");
        }),

        ("Ansagen: Satzliste – 20 Ereignisse, je 3 Sätze in beiden Fassungen, Dateinamen nach Schema, keine Namen, nur bekannte Kennungen", () =>
        {
            using var doc = System.Text.Json.JsonDocument.Parse(DateiText());
            var ereignisse = doc.RootElement.GetProperty("ereignisse").EnumerateArray().ToList();
            Pruefe.Gleich(20, ereignisse.Count);
            var bekannt = typeof(KlangAnsagenErkennung.Id).GetFields().Select(x => (string)x.GetRawConstantValue()!).ToHashSet();
            var kennungen = ereignisse.Select(e => e.GetProperty("id").GetString()!).ToList();
            Pruefe.Gleich(kennungen.Count, kennungen.Distinct().Count());
            Pruefe.Wahr(bekannt.SetEquals(kennungen), $"Kennungen weichen von KlangAnsagenErkennung.Id ab: {string.Join(", ", bekannt.Except(kennungen).Concat(kennungen.Except(bekannt)))}");
            foreach (var e in ereignisse)
            {
                var id = e.GetProperty("id").GetString()!;
                Pruefe.Wahr(e.GetProperty("prioritaet").GetInt32() is >= 1 and <= 9, $"{id}: Rang außerhalb 1–9.");
                var varianten = e.GetProperty("varianten").EnumerateArray().ToList();
                Pruefe.Wahr(varianten.Count is 2 or 3, $"{id}: {varianten.Count} Varianten.");
                for (var i = 0; i < varianten.Count; i++)
                {
                    var v = varianten[i];
                    Pruefe.Gleich(i + 1, v.GetProperty("nr").GetInt32());
                    Pruefe.Gleich($"{id}_{i + 1}.ogg", v.GetProperty("datei_herr").GetString());
                    Pruefe.Gleich($"{id}_{i + 1}_herrin.ogg", v.GetProperty("datei_herrin").GetString());
                    var herr = v.GetProperty("herr").GetString()!;
                    var herrin = v.GetProperty("herrin").GetString()!;
                    Pruefe.Wahr(herr.StartsWith("Mein Herr, "), $"{id}_{i + 1}: Anrede „Mein Herr“ fehlt.");
                    Pruefe.Gleich(herr["Mein Herr".Length..], herrin["Meine Herrin".Length..]);
                    Pruefe.Wahr(herrin.StartsWith("Meine Herrin, "), $"{id}_{i + 1}: Anrede „Meine Herrin“ fehlt.");
                    Pruefe.Wahr(herr.Split(' ').Length <= 14, $"{id}_{i + 1}: Satz zu lang für etwa 4 Sekunden.");
                    Pruefe.Wahr(!herr.Contains('{') && !herr.Contains("Nr."), $"{id}_{i + 1}: Platzhalter im Satz.");
                }
            }
        }),
    };
}
