using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Bauen auf freien Hofstellen (09.11.2026): Der Werkstattteil nimmt genau eine eingepasste Werkstatt auf, der Wohnhausteil ein Wohnhaus. Die
/// Werkstatt steht danach wie auf dem Platz der Karte (Platz, Drehung, Fahrzeit), auch nach dem Laden; die KI findet die Stelle unter ihren
/// Bauorten. Ohne Hofstellen bleibt der Platz fest wie bisher.
/// </summary>
static class HofstellenBauenTests
{
    /// <summary>Wirtschaftsdaten mit sofort fertigen Werkstätten (Bauzeit 0), sonst wie wirtschaft.json.</summary>
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() =>
    {
        var text = File.ReadAllText(Datenpfad.Wirtschaft());
        var alt = "\"werkstatt\": { \"breite\": 16, \"tiefe\": 14, \"bauzeitJahre\": 1,";
        Pruefe.Wahr(text.Contains(alt), "Zeile der Werkstatt in bauen gefunden");
        return Wirtschaftsdaten.AusJson(text.Replace(alt, "\"werkstatt\": { \"breite\": 16, \"tiefe\": 14, \"bauzeitJahre\": 0,"));
    });

    /// <summary>Greybox mit einem KI-Haus und ohne Los: H3 (Tischlerei_Eichwald, Wohnhaus3) und H4 sind frei. Der Spieler ist reich und Patrizier.</summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto Spieler) Partie(Stadtkarte? karte = null)
    {
        var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, Daten.Value,
            new NeuePartie(Startvermoegen: 200_000, Rivalen: 1, Startplaetze: Startplatzvergabe.Vorgabe),
            karte: karte ?? Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));
        spieler.Stand = 5;
        return (sim, sim.Wirtschaft!, spieler);
    }

    /// <summary>Der freie Werkstattplatz von H3 in der Greybox: Mitte (24 | −13, seit den fairen Hofstellen; vorher 22 | −13), Tür nach Süden.</summary>
    static readonly WerkstattBauen AufH3 = new(Betriebsart.Giesserei, 24, -13, 0);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Hofstelle bebauen: Auf den freien Werkstattplatz passt genau eine Werkstatt (eingerastet); daneben, halb darauf oder ein Wohnhaus nicht", () =>
        {
            var (_, w, spieler) = Partie();
            var platz = w.Bauen.Grundstuecke.Single(g => g.Kennung == "Tischlerei_Eichwald");
            Pruefe.Wahr(Bauwesen.IstWerkstattplatz(platz) && !Bauwesen.IstWohnhausplatz(platz), "Werkstattteil als Bauplatz");
            Pruefe.Gleich(null, w.Bauen.BauVerboten(spieler, AufH3));
            Pruefe.Gleich(AufH3, w.Bauen.Einrasten(AufH3 with { X = 19.3, Z = -10.8 }));
            Pruefe.Gleich("Eine Werkstatt kommt genau auf den Werkstattplatz der Hofstelle – nicht daneben oder halb darauf.",
                w.Bauen.PlatzVerboten(AufH3 with { X = 26 }));
            Pruefe.Gleich("Auf dem Werkstattplatz einer Hofstelle steht nur eine Werkstatt.", w.Bauen.PlatzVerboten(new WohnhausBauen(1, 22, -13, 0)));
            // Der Wohnhausteil: nur ein Wohnhaus, eingepasst; eine Werkstatt dort nicht
            var wohn = w.Bauen.Grundstuecke.Single(g => g.Kennung == "Wohnhaus3");
            Pruefe.Wahr(Bauwesen.IstWohnhausplatz(wohn), "Wohnhausteil als Bauplatz");
            var haus = new WohnhausBauen(1, wohn.Grundriss.X, wohn.Grundriss.Z, wohn.Grundriss.Drehung);
            Pruefe.Gleich(null, w.Bauen.PlatzVerboten(haus));
            Pruefe.Gleich("Auf einem Wohnhausplatz stehen nur Wohnhäuser.", w.Bauen.PlatzVerboten(new WerkstattBauen(Betriebsart.Giesserei, wohn.Grundriss.X, wohn.Grundriss.Z, 0)));
            // Die KI findet die Stelle unter ihren Bauorten (in der Drehung des Platzes)
            Pruefe.Wahr(w.Bauen.Bauorte(spieler, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0)).Contains(AufH3), "Bauort H3");
        }),

        ("Hofstelle bebauen: Die neue Werkstatt steht wie auf dem Platz der Karte – Platz, Fahrzeit wie vorher am Bauort; auch nach dem Laden", () =>
        {
            var (sim, w, spieler) = Partie();
            var typ = w.Daten.Art(Betriebsart.Giesserei).Startkarren;
            var vorher = w.FahrzeitAmBauort(AufH3, typ);
            Pruefe.Wahr(vorher is > 0, "Fahrzeit am Bauort");
            var e = w.Ausfuehren(spieler, AufH3);
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            var neu = w.Betriebe.Single(b => b.Konto == spieler && b.Name != w.Betriebe.First(x => x.Konto == spieler).Name);
            var platz = w.Bauen.PlatzVon(neu.Name)!;
            Pruefe.Gleich("Tischlerei_Eichwald", platz.Gebaeude?.Kennung);
            Pruefe.Wahr(platz.Grundstueck is null, "wie ein Platz der Karte");
            Pruefe.Wahr(w.Bauen.Grundstuecke.Single(g => g.Betrieb == neu.Name) is { AufWerkstattplatz: true, Kennung: "Tischlerei_Eichwald" }, "Grundstück mit Kennung");
            Pruefe.Wahr(w.Bauen.Grundstuecke.All(g => g.Art != Grundstuecksart.Bauplatz || g.Kennung != "Tischlerei_Eichwald"), "Bauplatz vergeben");
            Pruefe.Gleich(vorher, w.Fahrzeit(neu, typ, true));
            // Ein zweites Mal geht es dort nicht mehr
            Pruefe.Wahr(w.Bauen.PlatzVerboten(AufH3) is { } besetzt && besetzt.Contains(neu.Name), w.Bauen.PlatzVerboten(AufH3) ?? "frei?");
            var (geladen, _) = Speicherung.Laden(Speicherung.Speichern(sim, spieler, new DateTime(2026, 1, 1)));
            var gw = geladen.Wirtschaft!;
            Pruefe.Gleich("Tischlerei_Eichwald", gw.Bauen.PlatzVon(neu.Name)!.Gebaeude?.Kennung);
            Pruefe.Gleich(vorher, gw.Fahrzeit(gw.Betriebe.Single(b => b.Name == neu.Name), typ, true));
        }),

        ("Hofstelle bebauen: Gegenprobe ohne Hofstellen – der Platz eines nicht mitspielenden Hauses bleibt fest, kein Bauplatz", () =>
        {
            var (_, w, spieler) = Partie(HofstellenTests.AlteKarte("greybox"));
            Pruefe.Wahr(w.Bauen.Grundstuecke.All(g => g.Kennung != "Tischlerei_Eichwald"), "kein Grundstück");
            Pruefe.Gleich("Hier steht Tischlerei Eichwald.", w.Bauen.PlatzVerboten(AufH3));
            Pruefe.Wahr(!w.Bauen.Bauorte(spieler, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0)).Contains(AufH3), "kein Bauort");
        }),
    };
}
