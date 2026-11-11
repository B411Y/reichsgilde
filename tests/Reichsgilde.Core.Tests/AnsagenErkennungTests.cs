using Reichsgilde;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Entwickler;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests der Ansagen des Haushofmeisters (Ton Schritt 3, Spielordner <c>KlangAnsagenErkennung.cs</c>, hier mitübersetzt): Die Erkennung liest seit den
/// Benachrichtigungen Stufe 2 Art, Wichtigkeit und Haus der Meldung des Kerns (<c>Stadtwirtschaft.Gemeldet</c>), keinen Wortlaut mehr. Diese Tests lassen
/// <b>echte</b> Meldungen aus einer Partie erkennen (Entwicklerbefehle lösen Tod, Geburt, Krankheit, Heirat, Einbruch, Feuer, Hausbrand aus). Außerdem:
/// fremde Häuser lösen nichts aus (Mehrspieler), vereitelte Taten bleiben still, und die Satzliste <c>game/daten/ansagen.json</c> ist vollständig
/// (jedes Ereignis, Namen der Dateien, keine Namen im Satz).
/// </summary>
static class AnsagenErkennungTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static (Simulation Sim, Konto Spieler, Konto Rivale, List<Meldung> Meldungen) Partie()
    {
        var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos },
            Daten.Value, new NeuePartie(Aufstellung: Aufstellungsart.Vorgabe));
        for (var i = 0; i < 300; i++) sim.Tick();
        sim.EntwicklerErlaubt = true;
        var meldungen = new List<Meldung>();
        sim.Wirtschaft!.Gemeldet += meldungen.Add;
        var rivale = sim.Wirtschaft.Konten.First(k => k != spieler && k.IstAktiv);
        return (sim, spieler, rivale, meldungen);
    }

    /// <summary>Führt den Befehl aus und gibt zurück, was der Haushofmeister des Spielers zu den entstandenen Meldungen ansagen würde.</summary>
    static List<string> Angesagt(Simulation sim, Konto spieler, List<Meldung> meldungen, Befehl befehl)
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

        ("Ansagen: Art, Wichtigkeit und Haus der Meldung entscheiden (Überfall, Kasse, Lager, Schulden, Anklage, Meisterprüfung, Pest) – eigen, fremd, vereitelt", () =>
        {
            var (sim, spieler, rivale, _) = Partie();
            var w = sim.Wirtschaft!;
            int Nr(Konto k) => w.Konten.ToList().IndexOf(k);
            string? E(Meldungsart art, Konto? haus, Meldungswichtigkeit? wichtig = null) =>
                KlangAnsagenErkennung.Erkennen(new Meldung(art, "beliebiger Text") { Haus = haus is null ? null : Nr(haus), Wichtigkeit = wichtig ?? Meldung.Vorgabe(art) }, w, spieler);
            // eigen
            Pruefe.Gleich(KlangAnsagenErkennung.Id.KasseKnapp, E(Meldungsart.KasseKnapp, spieler));
            Pruefe.Gleich(KlangAnsagenErkennung.Id.LagerLeer, E(Meldungsart.LagerLeer, spieler));
            Pruefe.Gleich(KlangAnsagenErkennung.Id.LagerVoll, E(Meldungsart.LagerVoll, spieler));
            Pruefe.Gleich(KlangAnsagenErkennung.Id.Ueberfall, E(Meldungsart.Ueberfall, spieler));
            Pruefe.Gleich(KlangAnsagenErkennung.Id.Schulden, E(Meldungsart.Schulden, spieler));
            Pruefe.Gleich(KlangAnsagenErkennung.Id.Anklage, E(Meldungsart.Anklage, spieler));
            Pruefe.Gleich(KlangAnsagenErkennung.Id.Meisterpruefung, E(Meldungsart.Meisterpruefung, spieler));
            Pruefe.Gleich(KlangAnsagenErkennung.Id.PestStadt, E(Meldungsart.Pest, null));
            // fremd: nichts (außer der Pest, die die ganze Stadt trifft)
            foreach (var art in new[] { Meldungsart.KasseKnapp, Meldungsart.LagerLeer, Meldungsart.Ueberfall, Meldungsart.Schulden, Meldungsart.Anklage, Meldungsart.Meisterpruefung,
                         Meldungsart.Geburt, Meldungsart.TodOberhaupt, Meldungsart.Feuer })
            {
                Pruefe.Wahr(E(art, rivale) is null, $"Fremdes Haus angesagt: {art}");
                Pruefe.Wahr(E(art, null) is null, $"Ohne Haus angesagt: {art}");
            }
            // vereitelt, abgewehrt, leer: still
            foreach (var art in new[] { Meldungsart.Feuer, Meldungsart.Diebstahl, Meldungsart.Ueberfall })
                Pruefe.Wahr(E(art, spieler, Meldungswichtigkeit.Normal) is null, $"Misslungene Tat angesagt: {art}");
            // eigene Ansage-Auslöser (Aufstieg, Wahl, Ratssitzung) und Sonstiges: nicht von hier
            foreach (var art in new[] { Meldungsart.StandAufstieg, Meldungsart.Wahl, Meldungsart.Ratssitzung, Meldungsart.Sonstiges })
                Pruefe.Wahr(E(art, spieler) is null, $"Doppelt angesagt: {art}");
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
