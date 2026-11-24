using Reichsgilde;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Karren selbst fahren, Darstellung (K3): Die Punkte der Fahrt von Betrieb zu Betrieb (<see cref="Stadtwirtschaft.KarrenverlaufZwischen"/>) passen
/// zu den Stücken des Kerns (<see cref="Stadtwirtschaft.StreckeZwischen"/>): so fährt das Bild genau die Strecke, nach der der Kern die Zeit rechnet.
/// </summary>
static class KarrenverlaufZwischenTests
{
    public static IEnumerable<(string, Action)> Alle()
    {
        yield return ("Karrenverlauf zwischen Betrieben: Punkte = Stücke + 1, Längen gleich, Anfang und Ende an den Halteplätzen (Greybox, Köln)", () =>
        {
            foreach (var kennung in new[] { "greybox", "koeln" })
            {
                var daten = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
                var haeuser = Kartenhaeuser.Anzahl(File.ReadAllText(Altkarte.Pfad(kennung)));
                var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                    daten, new NeuePartie(Rivalen: haeuser, Aufstellung: Aufstellungsart.Vorgabe), karte: Stadtkarte.AusDatei(Altkarte.Pfad(kennung)));
                var w = sim.Wirtschaft!;
                var mitWeg = w.Betriebe.Where(b => w.Karrenverlauf(b) is not null).Take(4).ToList();
                var geprueft = 0;
                foreach (var von in mitWeg)
                    foreach (var nach in mitWeg.Where(n => n != von))
                    {
                        var stuecke = w.StreckeZwischen(von, nach);
                        var punkte = w.KarrenverlaufZwischen(von, nach);
                        if (stuecke is null) { Pruefe.Wahr(punkte is null, "ohne Stücke auch keine Punkte"); continue; }
                        Pruefe.Gleich(stuecke.Count + 1, punkte!.Count, $"{kennung}: {von.Name} -> {nach.Name}");
                        for (var i = 0; i < stuecke.Count; i++)
                            Pruefe.Ungefaehr(stuecke[i].Laenge, punkte[i].Abstand(punkte[i + 1]), 1e-6);
                        var (ankerVon, _) = Karrenweg.Start(w.Bauen.PlatzVon(von.Name)!, von, w.Karte!.Katalog);
                        var (ankerNach, _) = Karrenweg.Start(w.Bauen.PlatzVon(nach.Name)!, nach, w.Karte!.Katalog);
                        Pruefe.Ungefaehr(0, punkte[0].Abstand(ankerVon), 1e-9);
                        Pruefe.Ungefaehr(0, punkte[^1].Abstand(ankerNach), 1e-9);
                        geprueft++;
                    }
                Pruefe.Wahr(geprueft > 0, $"{kennung}: keine Fahrt zwischen Betrieben geprüft");
            }
        });
    }
}
