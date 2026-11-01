using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Kartenbau.Staedte;

// Köln: die Südhälfte von Deutz (Wunsch des Nutzers vom 1.11.2026). Seit die Werkstatt-Bauplätze WB_Neumarkt1–4 südlich der Freiheit liegen
// (Koeln.Bauplaetze.cs), waren die Ecken zwischen Bauplätzen, Rhein und Mauer leer. Hier stehen jetzt Häuser an zwei Gassen, dahinter Gärten
// und Höfe – wie in einer kleinen Stadt um 1400, in der hinter den Häusern an der Gasse Kohl- und Krautgärten bis an die Mauer reichen.
//
//    • Urbanusgasse (nach St. Urban, der Pfarrkirche von Deutz; die Gasse ist erfunden): von der Freiheit am Brückenkopf zwischen dem
//      Einwohnerplatz am Rhein und WB_Neumarkt1 nach Süden, dann hinter dem Bauplatz nach Osten bis vor WB_Neumarkt2. Häuser an der
//      Westseite (zum Rhein) und an der Südseite (zur Mauer).
//    • Abteigasse (nach der Abtei Deutz, erfunden): von der Freiheit vor dem Osttor zwischen WB_Neumarkt4 und der Ostmauer nach Süden bis vor
//      die Südmauer, eine Sackgasse. Häuser an der Westseite, an der Mauer ein Gartenstreifen.
//
// Die Gassen sind Wege der Klasse Gasse (Karren nehmen sie nur, wenn nichts anderes geht – sie zählen im Karrennetz doppelt); die Fahrzeiten
// der Bauplätze bleiben, wie sie waren (Tabelle in docs/LEVELDESIGN.md). Die Häuser sind Kulissenhäuser (feste Modelle, stehen auch mit wenigen
// Rivalen); von den acht Einwohnerplätzen von Deutz stehen zwei an der Urbanusgasse in der Mauer, sechs vor dem Südtor. Abstände: Gassenmitte
// mindestens 2 m zu den Bauplätzen, Häuser 2 m zu den Bauplätzen und 3 m zur Mauerlinie (Regel 1 m).

public sealed partial class Koeln
{
    /// <summary>Die beiden Gassen der Deutzer Südhälfte; jede beginnt knapp jenseits der Freiheit, damit die Kreuzung im Netz hängt.</summary>
    private void DeutzerGassen()
    {
        Weg(Wegklasse.Gasse, "Urbanusgasse", P(134, 7), P(134, 28), P(149, 28));
        Weg(Wegklasse.Gasse, "Abteigasse", P(206, -2.2), P(206, 36));
    }

    /// <summary>Kulissenhäuser an den beiden Gassen (vor den Einwohnerplätzen gesetzt, die Plätze an Freiheit und Kirchstraße bleiben frei).</summary>
    private int DeutzerHaeuser()
    {
        var n = 0;
        // Westseite und Südseite der Urbanusgasse (in Laufrichtung rechts), Westseite der Abteigasse
        foreach (var strasse in new[] { "Urbanusgasse", "Abteigasse" })
            n += Reihe("kulisse_buergerhaus", strasse, links: false, 0, 1000, luecke: 1, abstandZurStrasse: 0.4, mauerAbstand: 3).Count;
        return n;
    }

    /// <summary>Gärten und Höfe hinter den Häusern der Deutzer Südhälfte (Bodentexturen; Natur und Äcker wachsen in der Stadt nicht).</summary>
    private void DeutzerGaerten()
    {
        // Kraut- und Kohlgärten (umgegrabene Erde; Wiese zeigt die Stadtansicht in der Mauer als Lehm): an der Ostmauer neben der Abteigasse,
        // zwischen WB_Neumarkt3 und dem Haus an der Abteigasse, hinter ihm bis an die Südmauer
        TexturFlaeche(Bodentextur.Erde, 208, 4, 210.5, 36, radius: 2);
        TexturFlaeche(Bodentextur.Erde, 192.5, 23, 194.5, 37, radius: 2);
        TexturFlaeche(Bodentextur.Erde, 197, 32, 204, 37, radius: 2);
        // Höfe: am Ende der Abteigasse, zwischen den Häusern an der Urbanusgasse und WB_Neumarkt2
        TexturFlaeche(Bodentextur.Mist, 205, 33, 207, 36, radius: 1.5, staerke: 0.7);
        TexturFlaeche(Bodentextur.Mist, 149, 30, 150.5, 37, radius: 1.5, staerke: 0.7);
    }
}
