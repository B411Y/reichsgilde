using Reichsgilde.Core;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests für das Abstimmen des Spielers im Rat (PR #111): Stimmen bei Wahlen, zum Statut und zum Stadtglauben
/// vorab festlegen, Vorgabe ohne Stimme, ungültige Stimmen, Stimmrecht, Prognosen und Spielstand.
/// </summary>
static class RatStimmeTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));

    static Simulation NeueSimulation() =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);

    /// <summary>Ein Haus im gewünschten Stand mit dem gewünschten Vermögen.</summary>
    static Konto Haus(Stadtwirtschaft w, string name, int stand, double vermoegen)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: 1_000_000);
        for (var i = 0; i < stand; i++)
            Pruefe.Wahr(w.Ausfuehren(Wohnhaushilfe.MitWohnhaus(w, k), new StandErwerben()).Erfolg, "Aufstieg fehlgeschlagen");
        k.Schaden(k.Vermoegen - vermoegen);
        return k;
    }

    /// <summary>Setzt das Haus auf diesen Sitz (statt des Ratsherrn).</summary>
    static void Sitz(Stadtwirtschaft w, string amt, Konto k)
    {
        var sitz = w.Rat.AmtNamens(amt)!;
        sitz.Ratsherr = null;
        sitz.Konto = k;
    }

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    static void Ok(Stadtwirtschaft w, Konto k, Befehl befehl)
    {
        var e = w.Ausfuehren(k, befehl);
        Pruefe.Wahr(e.Erfolg, e.Meldung);
    }

    static void Abgelehnt(Stadtwirtschaft w, Konto k, Befehl befehl, string grund)
    {
        var e = w.Ausfuehren(k, befehl);
        Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains(grund), $"erwartet abgelehnt mit „{grund}“, war: {e.Erfolg} – {e.Meldung}");
    }

    /// <summary>
    /// Wahl zum Zöllner: Der Spieler sitzt als Marktmeister unter den 5 Wählern. Ein Bewerber hat zwei Ratsherren beschenkt
    /// (Wendelstein, Silberhain), die beiden anderen (Erzhammer, Eisenfaust) halten zum Ratsherrn im Amt (Tannenkamp): 2 zu 2.
    /// </summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto Spieler, Konto Bewerber, Amt Zoellner) Wahlaufbau(bool geschenke = true)
    {
        var sim = NeueSimulation();
        var w = sim.Wirtschaft!;
        var spieler = Haus(w, "Spieler", 1, 20_000);
        Sitz(w, "Marktmeister", spieler);
        var bewerber = Haus(w, "Bewerber", 1, 20_000);
        Ok(w, bewerber, new AmtBewerben("Zöllner"));
        if (geschenke)
            foreach (var name in new[] { "Ulrich Wendelstein", "Aelindra vom Silberhain" })
                Ok(w, bewerber, new Schenken(name, 1000));
        return (sim, w, spieler, bewerber, w.Rat.AmtNamens("Zöllner")!);
    }

    /// <summary>
    /// Antrag im Rat: Ein Reformer (Zöllner) beantragt etwas, der Spieler sitzt als Marktmeister (statt Bertram Kesselflick).
    /// Bei beiden Anträgen unten steht es ohne den Spieler 3 zu 3, und der Bürgermeister ist dafür.
    /// </summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto Spieler, Konto Reformer) Antragsaufbau()
    {
        var sim = NeueSimulation();
        var w = sim.Wirtschaft!;
        var reformer = Haus(w, "Reformer", 1, 50_000);
        Sitz(w, "Zöllner", reformer);
        var spieler = Haus(w, "Spieler", 1, 50_000);
        Sitz(w, "Marktmeister", spieler);
        return (sim, w, spieler, reformer);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Rat abstimmen: Die Stimme des Spielers entscheidet eine Wahl gegen den Ratsherrn im Amt – die Prognose zählt sie mit", () =>
        {
            var (sim, w, spieler, bewerber, zoellner) = Wahlaufbau();
            Pruefe.Gleich("Bewerber|Hinnerk Tannenkamp", string.Join("|", w.Rat.Kandidaten(zoellner).Select(k => k.Name)));
            Pruefe.Gleich(true, w.Rat.Kandidaten(zoellner)[1].IstAmtsinhaber);
            Pruefe.Gleich<string?>(null, w.Rat.WahlStimmeVerboten(spieler, zoellner));
            Pruefe.Gleich((2, 0, 5), w.Rat.WahlPrognose(zoellner, bewerber));

            Ok(w, spieler, new WahlStimme("Zöllner", "Bewerber"));
            Pruefe.Gleich(bewerber, w.Rat.WahlStimmeVon(spieler, zoellner)!.Haus);
            Pruefe.Gleich((3, 0, 5), w.Rat.WahlPrognose(zoellner, bewerber));
            JahrBeenden(sim);
            Pruefe.Gleich(bewerber, zoellner.Konto);
            Pruefe.Wahr(w.Rat.LetzteSitzung.Any(s => s.Contains("Bewerber löst Hinnerk Tannenkamp ab") && s.Contains("Bewerber 3")),
                        string.Join(" | ", w.Rat.LetzteSitzung));
        }),

        ("Rat abstimmen: Ohne Stimme (oder zurückgenommen) gilt die Vorgabe – für den Amtsinhaber", () =>
        {
            var (sim, w, spieler, bewerber, zoellner) = Wahlaufbau();
            Pruefe.Gleich<Wahlkandidat?>(null, w.Rat.WahlStimmeVon(spieler, zoellner));
            Pruefe.Gleich("Hinnerk Tannenkamp", w.Rat.VorgabeStimme(zoellner)!.Name);
            Ok(w, spieler, new WahlStimme("Zöllner", "Bewerber"));
            var e = w.Ausfuehren(spieler, new WahlStimme("Zöllner", null));
            Pruefe.Wahr(e.Erfolg && e.Meldung.Contains("Vorgabe: für Hinnerk Tannenkamp"), e.Meldung);
            Pruefe.Gleich<Wahlkandidat?>(null, w.Rat.WahlStimmeVon(spieler, zoellner));
            Pruefe.Gleich((2, 0, 5), w.Rat.WahlPrognose(zoellner, bewerber));
            JahrBeenden(sim);
            Pruefe.Gleich("Hinnerk Tannenkamp", zoellner.Inhaber);
            Pruefe.Wahr(w.Rat.LetzteSitzung.Any(s => s.Contains("bleibt im Amt") && s.Contains("Hinnerk Tannenkamp 3")),
                        string.Join(" | ", w.Rat.LetzteSitzung));
        }),

        ("Rat abstimmen: Stimme für den Ratsherrn im Amt (mit seinem Namen) – sie hält ihn gegen einen beschenkten Rat", () =>
        {
            var (sim, w, spieler, bewerber, zoellner) = Wahlaufbau();
            Ok(w, spieler, new WahlStimme("Zöllner", "Hinnerk Tannenkamp"));
            var stimme = w.Rat.WahlStimmeVon(spieler, zoellner)!;
            Pruefe.Wahr(stimme.Haus is null && stimme.IstAmtsinhaber && stimme.Name == "Hinnerk Tannenkamp", stimme.ToString());
            Abgelehnt(w, spieler, new WahlStimme("Zöllner", "Graf Dracula"), "Zur Wahl stehen: Bewerber, Hinnerk Tannenkamp");
            Pruefe.Gleich("Hinnerk Tannenkamp", w.Rat.WahlStimmeVon(spieler, zoellner)!.Name); // die abgelehnte Stimme ändert nichts
            JahrBeenden(sim);
            Pruefe.Gleich("Hinnerk Tannenkamp", zoellner.Inhaber);
        }),

        ("Rat abstimmen: Antrag auf ein Statut – Enthaltung lässt ihn durch (Bürgermeister), ein Nein kippt ihn", () =>
        {
            foreach (var nein in new[] { false, true })
            {
                var (sim, w, spieler, reformer) = Antragsaufbau();
                Abgelehnt(w, spieler, new StatutStimme(true), "kein Antrag zum Statut");
                Ok(w, reformer, new StatutBeantragen(Fronstatut.FreieStadt));
                Abgelehnt(w, reformer, new StatutStimme(false), "stimmt ohnehin dafür");
                Pruefe.Gleich((3, 3, 1, (bool?)true), w.Rat.AbstimmungsPrognose(reformer, Fronstatut.FreieStadt));
                if (nein)
                {
                    Ok(w, spieler, new StatutStimme(false));
                    Pruefe.Gleich((bool?)false, w.Rat.StatutStimmeVon(spieler));
                    Pruefe.Gleich((3, 4, 0, (bool?)true), w.Rat.AbstimmungsPrognose(reformer, Fronstatut.FreieStadt));
                    // Eine gedachte Abstimmung über ein anderes Statut zählt die Stimme nicht: Sie gilt nur dem vorliegenden Antrag.
                    Pruefe.Gleich(1, w.Rat.AbstimmungsPrognose(reformer, Fronstatut.Leibeigenschaft).Enthaltung);
                }
                JahrBeenden(sim);
                Pruefe.Gleich(nein ? Fronstatut.Bestienfron : Fronstatut.FreieStadt, w.Statut);
                Pruefe.Wahr(w.Rat.LetzteSitzung.Any(s => s.Contains(nein ? "abgelehnt (3 dafür, 4 dagegen)" : "ANGENOMMEN (3 dafür, 3 dagegen, der Bürgermeister")),
                            string.Join(" | ", w.Rat.LetzteSitzung));
            }
        }),

        ("Rat abstimmen: Antrag auf einen Stadtglauben – Enthaltung lässt ihn durch, ein Nein kippt ihn", () =>
        {
            foreach (var nein in new[] { false, true })
            {
                var (sim, w, spieler, reformer) = Antragsaufbau();
                Abgelehnt(w, spieler, new StadtglaubeStimme(true), "kein Antrag zum Stadtglauben");
                Ok(w, reformer, new StadtglaubeBeantragen(Konfession.Katholisch));
                Abgelehnt(w, reformer, new StadtglaubeStimme(false), "stimmt ohnehin dafür");
                // Katholische Ratsherren dafür (Wendelstein, Silberhain), evangelische dagegen (Erzhammer, Eisenfaust, Kupferbart).
                Pruefe.Gleich((3, 3, 1, (bool?)true), w.Glaube.AbstimmungsPrognose(reformer, Konfession.Katholisch));
                if (nein)
                {
                    Ok(w, spieler, new StadtglaubeStimme(false));
                    Pruefe.Gleich((bool?)false, w.Glaube.StimmeVon(spieler));
                    Pruefe.Gleich((3, 4, 0, (bool?)true), w.Glaube.AbstimmungsPrognose(reformer, Konfession.Katholisch));
                    Pruefe.Gleich(1, w.Glaube.AbstimmungsPrognose(reformer, Konfession.Evangelisch).Enthaltung);
                }
                JahrBeenden(sim);
                Pruefe.Gleich(nein ? null : (Konfession?)Konfession.Katholisch, w.Glaube.Stadtglaube);
                Pruefe.Gleich<bool?>(null, w.Glaube.StimmeVon(spieler)); // nach der Sitzung: kein Antrag, keine Stimme
            }
        }),

        ("Rat abstimmen: Ungültige Stimmen verfallen – Kandidat scheidet aus, Antrag fällt weg, Haus verliert sein Amt", () =>
        {
            // Der Kandidat scheidet aus (Bankrott): Die Stimme verfällt und lebt auch nach einer neuen Bewerbung nicht wieder auf.
            var (_, w, spieler, bewerber, zoellner) = Wahlaufbau();
            Ok(w, spieler, new WahlStimme("Zöllner", "Bewerber"));
            w.Rat.Ausscheiden(bewerber);
            Pruefe.Wahr(w.Rat.WahlStimmeVerboten(spieler, zoellner)!.Contains("bewirbt sich dieses Jahr niemand"), "Wahl ohne Bewerber");
            Ok(w, bewerber, new AmtBewerben("Zöllner"));
            Pruefe.Gleich<Wahlkandidat?>(null, w.Rat.WahlStimmeVon(spieler, zoellner));

            // Das Haus des Spielers verliert sein Amt: Seine Stimmen verfallen, auch wenn es wieder in den Rat kommt.
            Ok(w, spieler, new WahlStimme("Zöllner", "Bewerber"));
            w.Rat.Ausscheiden(spieler);
            Abgelehnt(w, spieler, new WahlStimme("Zöllner", "Bewerber"), "nur Mitglieder des Rates");
            Sitz(w, "Marktmeister", spieler);
            Pruefe.Gleich<Wahlkandidat?>(null, w.Rat.WahlStimmeVon(spieler, zoellner));

            // Der Antrag fällt weg: Die Stimme gilt nicht für einen neuen Antrag desselben Jahres.
            var (_, w2, spieler2, reformer) = Antragsaufbau();
            var dritter = Haus(w2, "Dritter", 1, 50_000);
            Sitz(w2, "Gefängniswärter", dritter);
            Ok(w2, reformer, new StatutBeantragen(Fronstatut.FreieStadt));
            Ok(w2, reformer, new StadtglaubeBeantragen(Konfession.Katholisch));
            Ok(w2, spieler2, new StatutStimme(false));
            Ok(w2, spieler2, new StadtglaubeStimme(false));
            w2.Rat.Ausscheiden(reformer);
            Pruefe.Gleich<bool?>(null, w2.Rat.StatutStimmeVon(spieler2));
            Pruefe.Gleich<bool?>(null, w2.Glaube.StimmeVon(spieler2));
            Ok(w2, dritter, new StatutBeantragen(Fronstatut.FreieStadt));
            Ok(w2, dritter, new StadtglaubeBeantragen(Konfession.Katholisch));
            Pruefe.Gleich<bool?>(null, w2.Rat.StatutStimmeVon(spieler2));
            Pruefe.Gleich<bool?>(null, w2.Glaube.StimmeVon(spieler2));
            Pruefe.Gleich(1, w2.Rat.AbstimmungsPrognose(dritter, Fronstatut.FreieStadt).Enthaltung);
        }),

        ("Rat abstimmen: Wer nicht stimmberechtigt ist, wird mit Grund abgewiesen", () =>
        {
            var (_, w, spieler, bewerber, zoellner) = Wahlaufbau();
            var draussen = Haus(w, "Draußen", 1, 20_000);
            Abgelehnt(w, draussen, new WahlStimme("Zöllner", "Bewerber"), "nur Mitglieder des Rates");
            Abgelehnt(w, draussen, new StatutStimme(true), "nur Mitglieder des Rates");
            Abgelehnt(w, draussen, new StadtglaubeStimme(true), "nur Mitglieder des Rates");
            Abgelehnt(w, spieler, new WahlStimme("Kaiser", null), "Ein Amt „Kaiser“ gibt es nicht");
            Abgelehnt(w, spieler, new WahlStimme("Gefängniswärter", "Dorla Kupferbart"), "bewirbt sich dieses Jahr niemand");

            // Nur höhere Ämter wählen: Der Gefängniswärter bewirbt sich um den Marktmeister – der Zöllner stimmt dort nicht mit.
            var waerter = Haus(w, "Wärter", 1, 20_000);
            Sitz(w, "Gefängniswärter", waerter);
            var unten = Haus(w, "Unten", 1, 20_000);
            Sitz(w, "Zöllner", unten);
            Ok(w, waerter, new AmtBewerben("Marktmeister"));
            Abgelehnt(w, unten, new WahlStimme("Marktmeister", "Wärter"), "nur die Inhaber höherer Ämter");
            // Kandidaten stimmen nicht mit: Der Spieler hält das Amt, um das sich der Wärter bewirbt.
            Abgelehnt(w, spieler, new WahlStimme("Marktmeister", "Wärter"), "selbst zur Wahl");
            Pruefe.Gleich("Wärter|Spieler", string.Join("|", w.Rat.Kandidaten(w.Rat.AmtNamens("Marktmeister")!).Select(k => k.Name)));

            // Eine KI stimmt nach ihrem Charakter – Befehle zum Abstimmen nimmt sie nicht.
            var ki = Haus(w, "KI-Haus", 1, 20_000);
            w.KiAnmelden(ki);
            Sitz(w, "Kämmerer", ki);
            Pruefe.Wahr(w.Rat.WahlStimmeVerboten(ki, zoellner)!.Contains("nach eigenem Ermessen"), "KI darf nicht festlegen");
        }),

        ("Rat abstimmen: Nach der Sitzung sind alle Stimmen gelöscht – auch bei einer neuen Wahl im nächsten Jahr", () =>
        {
            var (sim, w, spieler, bewerber, zoellner) = Wahlaufbau(geschenke: false);
            Ok(w, spieler, new WahlStimme("Zöllner", "Bewerber"));
            Pruefe.Gleich((1, 0, 5), w.Rat.WahlPrognose(zoellner, bewerber));
            JahrBeenden(sim); // 1 zu 4 – der Bewerber verliert
            Pruefe.Gleich("Hinnerk Tannenkamp", zoellner.Inhaber);
            Ok(w, bewerber, new AmtBewerben("Zöllner"));
            Pruefe.Gleich<Wahlkandidat?>(null, w.Rat.WahlStimmeVon(spieler, zoellner));
            Pruefe.Gleich((0, 0, 5), w.Rat.WahlPrognose(zoellner, bewerber));
        }),

        ("Rat abstimmen: Stimmen überstehen Speichern und Laden; alte Spielstände laden ohne Stimmen", () =>
        {
            var (sim, w, spieler, reformer) = Antragsaufbau();
            Ok(w, reformer, new StatutBeantragen(Fronstatut.FreieStadt));
            Ok(w, reformer, new StadtglaubeBeantragen(Konfession.Katholisch));
            var zoellnerKandidat = Haus(w, "Zollbewerber", 1, 20_000);
            var waerterKandidat = Haus(w, "Wärterbewerber", 1, 20_000);
            Ok(w, zoellnerKandidat, new AmtBewerben("Zöllner"));
            Ok(w, waerterKandidat, new AmtBewerben("Gefängniswärter"));
            Ok(w, spieler, new WahlStimme("Zöllner", "Zollbewerber"));
            Ok(w, spieler, new WahlStimme("Gefängniswärter", "Dorla Kupferbart"));
            Ok(w, spieler, new StatutStimme(false));
            Ok(w, spieler, new StadtglaubeStimme(true));

            var json = Speicherung.Speichern(sim, spieler);
            var (geladen, sp) = Speicherung.Laden(json);
            var g = geladen.Wirtschaft!;
            Pruefe.Gleich("Zollbewerber", g.Rat.WahlStimmeVon(sp!, g.Rat.AmtNamens("Zöllner")!)!.Name);
            var waerter = g.Rat.WahlStimmeVon(sp!, g.Rat.AmtNamens("Gefängniswärter")!)!;
            Pruefe.Wahr(waerter.Haus is null && waerter.Name == "Dorla Kupferbart", waerter.ToString());
            Pruefe.Gleich((bool?)false, g.Rat.StatutStimmeVon(sp!));
            Pruefe.Gleich((bool?)true, g.Glaube.StimmeVon(sp!));
            Pruefe.Gleich(Speicherung.Speichern(sim, spieler, DateTime.UnixEpoch), Speicherung.Speichern(geladen, sp, DateTime.UnixEpoch));

            // Ein Spielstand von vor dem Abstimmen: ohne die drei Felder.
            var knoten = System.Text.Json.Nodes.JsonNode.Parse(json)!;
            var wirtschaft = knoten["Wirtschaft"]!.AsObject();
            wirtschaft["Rat"]!.AsObject().Remove("WahlStimmen");
            wirtschaft["Rat"]!.AsObject().Remove("StatutStimmen");
            wirtschaft["Glaube"]!.AsObject().Remove("Stimmen");
            var (alt, altSpieler) = Speicherung.Laden(knoten.ToJsonString());
            var a = alt.Wirtschaft!;
            Pruefe.Gleich<Wahlkandidat?>(null, a.Rat.WahlStimmeVon(altSpieler!, a.Rat.AmtNamens("Zöllner")!));
            Pruefe.Gleich<bool?>(null, a.Rat.StatutStimmeVon(altSpieler!));
            Pruefe.Gleich<bool?>(null, a.Glaube.StimmeVon(altSpieler!));
            Pruefe.Gleich(Fronstatut.FreieStadt, a.Rat.Antrag!.Value.Statut);
        }),
    };
}
