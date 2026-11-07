using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;

/// <summary>
/// Fehler vom 07.11.2026: Karten ohne Restflächen-Liste (probestadt, alte Spielstände) haben <c>Restflaechen == null</c>; die Darstellung
/// (Bodenstempel) lief darüber in eine NullReferenceException. <see cref="Stadtkarte.RestflaechenOderLeer"/> liefert dann eine leere Liste.
/// </summary>
static class KarteOhneRestflaechenTests
{
    public static IEnumerable<(string, Action)> Alle()
    {
        yield return ("Karte ohne Restflächen: RestflaechenOderLeer ist leer, nicht null", () =>
        {
            var karte = Stadtkarte.AusDatei(Datenpfad.Karte("probestadt"));
            var leer = karte.RestflaechenOderLeer();
            if (leer is null || leer.Count != 0) throw new Exception("probestadt: erwartet leere Liste");
            // Wie die Darstellung: darüber iterieren darf nicht werfen
            foreach (var _ in karte.RestflaechenOderLeer()) throw new Exception("keine Restfläche erwartet");
        });

        yield return ("Jede Karte unter game/karten lädt, RestflaechenOderLeer ist nie null", () =>
        {
            var ordner = Path.GetDirectoryName(Datenpfad.Karte("probestadt"))!;
            var dateien = Directory.GetFiles(ordner, "*.json");
            if (dateien.Length < 3) throw new Exception("zu wenige Karten gefunden: " + dateien.Length);
            foreach (var datei in dateien)
            {
                var karte = Stadtkarte.AusDatei(datei);
                if (karte.RestflaechenOderLeer() is null) throw new Exception(datei + ": null");
            }
        });

        yield return ("Karte mit leerem Feld und ohne Feld laden beide ohne Ausnahme", () =>
        {
            var pfad = Datenpfad.Karte("probestadt");
            var ohne = Stadtkarte.AusDatei(pfad);
            var json = JsonNode.Parse(File.ReadAllText(pfad))!.AsObject();
            json["restflaechen"] = new JsonArray();
            var katalog = Gebaeudekatalog.AusDatei(Stadtkarte.KatalogZu(pfad));
            var mit = Stadtkarte.AusJson(json.ToJsonString(), katalog);
            if (mit.RestflaechenOderLeer().Count != 0 || ohne.RestflaechenOderLeer().Count != 0) throw new Exception("erwartet leer");
        });
    }
}
