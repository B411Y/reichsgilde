using System.Globalization;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Kartenbau;

/// <summary>
/// <c>bruecke &lt;karte&gt;</c>: Prüfskript für die Brücke einer Karte (Klappbrücke Köln, Karte). Es rechnet nur, es ändert nichts:
/// Maße der Brücke aus dem Wasserplan (Weite, Pfeiler, Deckhöhe über dem Spiegel, Straßenbreite), die freie Durchfahrt im Wasser (größter Abstand
/// zwischen Ufer und Pfeilern, gemessen gegen 13 m), die Wartezonen (gerade Straße beiderseits der Mitte), die Erreichbarkeit für Karren über die
/// Brücke (Karrennetz) und die Zahl der Hindernisse im Wasserband für das Strömungsfeld der Darstellung. Maße des Modells: <see cref="Klappbrueckenmasse"/>.
/// </summary>
public static class Brueckenpruefung
{
    /// <summary>Maße des abgenommenen Modells (Austausch klappbruecke/bericht.md, masse_ausgabe.txt); keine Messung im Spiel.</summary>
    public static class Klappbrueckenmasse
    {
        public const double Durchfahrt = 13.0;        // lichte Weite offen
        public const double FahrbahnLichtMin = 6.4;   // lichte Breite der Fahrbahn
        public const double FahrbahnKlappe = 7.0;     // Breite der Klappe
        public const double DeckUeberWasser = 2.4;    // geschätzt (Modellbericht); die Karte rechnet Deck − Spiegel
        public const double WartezoneVon = 11.5;      // Schranke
        public const double WartezoneBis = 30.2;      // Ende der geraden Straße (18,7 m)
    }

    public static int Pruefen(Projektordner o, string karte, TextWriter aus)
    {
        var (katalog, daten) = (Gebaeudekatalog.AusDatei(o.Katalog), Reichsgilde.Core.Wirtschaft.Wirtschaftsdaten.AusDatei(o.Wirtschaft));
        var e = Befehle.Oeffnen(o, karte, katalog, daten);
        var plan = e.Wasserplan;
        var stadt = e.ZuStadtkarte();
        var fehler = 0;
        var tauglich = new List<string>();
        var gewaesser = daten.Logistik?.Klappbruecke?.Gewaesser; // nur die Brücke über dieses Gewässer ist die Klappbrücke (Daten, seit Version 90)
        string Z(double x) => x.ToString("0.0", CultureInfo.InvariantCulture);
        var bruecken = plan.Bruecken.Where(b => gewaesser is null || b.GewaesserName == gewaesser).ToList();
        aus.WriteLine($"Brücken in „{e.Kennung}“ über „{gewaesser ?? "alle Gewässer"}“: {bruecken.Count}");
        foreach (var q in bruecken)
        {
            var weg = stadt.Wege[q.Weg];
            aus.WriteLine($"— „{q.WegName}“ über „{q.GewaesserName}“: {q.Art}, {Z(q.Spannweite)} m Wasser, {Z(q.UferBStelle - q.UferAStelle)} m von Ufer zu Ufer, Karren {(q.Karren ? "ja" : "nein")}");
            aus.WriteLine($"   Straßenbreite {Z(weg.BreiteWirksam)} m (Klasse {weg.Klasse}); Modell braucht lichte {Z(Klappbrueckenmasse.FahrbahnLichtMin)} m (Klappe {Z(Klappbrueckenmasse.FahrbahnKlappe)} m): {(weg.BreiteWirksam >= Klappbrueckenmasse.FahrbahnLichtMin ? "ja" : "NEIN, zu schmal")}");
            if (weg.BreiteWirksam < Klappbrueckenmasse.FahrbahnLichtMin) tauglich.Add("Straße zu schmal");
            aus.WriteLine($"   Spiegel {Z(q.Spiegel)} m, Deck {Z(q.Deckhoehe)} m, Deck über Spiegel {Z(q.Deckhoehe - q.Spiegel)} m (Modell geschätzt {Z(Klappbrueckenmasse.DeckUeberWasser)} m), Tiefe {Z(q.Tiefe)} m");

            // Durchfahrt: Lücken zwischen Ufer und Pfeilern entlang der Brückenachse (Pfeiler 2 m breit)
            var stellen = new List<(double Von, double Bis)>();
            var kanten = new List<double> { q.UferAStelle };
            var achse = q.Pfeiler.Select(p => Stelle(q, p.Ort)).OrderBy(s => s).ToList();
            foreach (var s in achse) { kanten.Add(s - Wasserplan.SteinPfeiler / 2); kanten.Add(s + Wasserplan.SteinPfeiler / 2); }
            kanten.Add(q.UferBStelle);
            for (var i = 0; i + 1 < kanten.Count; i += 2) stellen.Add((kanten[i], kanten[i + 1]));
            var mitte = (q.UferAStelle + q.UferBStelle) / 2;
            var beste = stellen.OrderByDescending(s => Math.Round(s.Bis - s.Von, 2)).ThenBy(s => Math.Abs((s.Von + s.Bis) / 2 - mitte)).First();
            aus.WriteLine($"   Pfeiler: {q.Pfeiler.Count} bei Stelle {string.Join(", ", achse.Select(s => Z(s - mitte)))} (Abstand zur Flussmitte, m)");
            aus.WriteLine($"   Lichte Weiten zwischen den Pfeilern: {string.Join(", ", stellen.Select(s => Z(s.Bis - s.Von)))} m; größte {Z(beste.Bis - beste.Von)} m, Mitte {Z((beste.Von + beste.Bis) / 2 - mitte)} m neben der Flussmitte");
            var durchfahrt = beste.Bis - beste.Von >= Klappbrueckenmasse.Durchfahrt && Math.Abs((beste.Von + beste.Bis) / 2 - mitte) <= 2;
            if (!durchfahrt) tauglich.Add("keine Durchfahrt");
            aus.WriteLine($"   Durchfahrt {Z(Klappbrueckenmasse.Durchfahrt)} m in der Flussmitte: {(durchfahrt ? "ja" : "NEIN (die Karte kennt keine Durchfahrt; die Pfeiler berechnet der Wasserplan aus der Weite)")}");

            // Wartezonen: gerade Straße 11,5 … 30,2 m beiderseits der Mitte (Stellen auf dem Weg: UferA/B-Mitte entlang der Linie)
            var verlauf = weg.Verlauf.Select(Kartenort.Aus).ToList();
            foreach (var vorzeichen in new[] { -1, 1 })
            {
                var (a, b) = (mitte + vorzeichen * Klappbrueckenmasse.WartezoneVon, mitte + vorzeichen * Klappbrueckenmasse.WartezoneBis);
                var gerade = Gerade(verlauf, Math.Min(a, b), Math.Max(a, b), out var abweichung);
                aus.WriteLine($"   Wartezone {(vorzeichen < 0 ? "West" : "Ost ")} (Stelle {Z(Math.Min(a, b))} … {Z(Math.Max(a, b))} auf dem Weg, Weg {Z(LaengeVon(verlauf))} m lang): {(gerade ? "gerade" : "NICHT gerade")} (größte Abweichung {Z(abweichung)} m)");
                if (!gerade) fehler++;
            }
        }

        // Erreichbarkeit: Karren über die Brücke von Ufer zu Ufer (Karrennetz)
        var netz = Karrennetz.Von(stadt);
        foreach (var q in bruecken.Where(b => b.Karren))
        {
            var (west, ost) = (q.Anfang, q.Ende);
            var fahrt = netz.Fahrt(west, ost);
            var ueber = fahrt.Where(s => s.Art == Fahrbahn.Bruecke).Sum(s => s.Laenge);
            var hin = fahrt.Count > 0;
            aus.WriteLine($"   Karren {q.Anfang} → {q.Ende}: {(hin ? "erreichbar" : "NICHT erreichbar")}, {Z(fahrt.Sum(s => s.Laenge))} m, davon {Z(ueber)} m Fahrbahn.Bruecke");
            if (!hin || ueber <= 0) fehler++;
        }
        aus.WriteLine(fehler == 0 ? "Brücke: Wartezonen und Karrenweg in Ordnung." : $"Brücke: {fehler} Fehler.");
        aus.WriteLine(tauglich.Count == 0 ? "Maße für die Klappbrücke: erfüllt." : $"Maße für die Klappbrücke: nicht erfüllt ({string.Join(", ", tauglich)}).");
        return fehler == 0 ? 0 : 1;
    }

    /// <summary>Stelle (Meter vom Anfang des Weges) des Punkts auf der Brückenachse: Abstand zum Anfang der Brücke plus <c>VonStelle</c>.</summary>
    private static double Stelle(Wasserquerung q, Kartenort p) => q.VonStelle + q.Anfang.Abstand(p);

    private static double LaengeVon(IReadOnlyList<Kartenort> v)
    {
        var l = 0.0;
        for (var i = 0; i + 1 < v.Count; i++) l += v[i].Abstand(v[i + 1]);
        return l;
    }

    /// <summary>Liegt der Weg zwischen den Stellen auf einer Geraden? Die Abweichung ist der größte Abstand der Stützpunkte von der Sehne.</summary>
    private static bool Gerade(IReadOnlyList<Kartenort> v, double von, double bis, out double abweichung)
    {
        abweichung = 0;
        var punkte = new List<Kartenort>();
        var s = 0.0;
        for (var i = 0; i + 1 < v.Count; i++)
        {
            var l = v[i].Abstand(v[i + 1]);
            if (s + l >= von && s <= bis)
            {
                // Anfang und Ende des Abschnitts in diesem Segment
                var t0 = Math.Max(0, (von - s) / l);
                var t1 = Math.Min(1, (bis - s) / l);
                punkte.Add(new Kartenort(v[i].X + (v[i + 1].X - v[i].X) * t0, v[i].Z + (v[i + 1].Z - v[i].Z) * t0));
                punkte.Add(new Kartenort(v[i].X + (v[i + 1].X - v[i].X) * t1, v[i].Z + (v[i + 1].Z - v[i].Z) * t1));
            }
            s += l;
        }
        if (punkte.Count < 2) return false;
        var (a, b) = (punkte[0], punkte[^1]);
        var len = a.Abstand(b);
        if (len < 1e-9) return false;
        foreach (var p in punkte)
            abweichung = Math.Max(abweichung, Math.Abs((b.X - a.X) * (a.Z - p.Z) - (a.X - p.X) * (b.Z - a.Z)) / len);
        return abweichung <= 0.05 && len >= bis - von - 0.05;
    }
}
