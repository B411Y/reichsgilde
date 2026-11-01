using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Ein möglicher Weg der Karren von einem Bauort zu einem Marktplatz (<see cref="Stadtwirtschaft.FahrwegeAmBauort"/>).
/// </summary>
/// <param name="Markt">Der Marktplatz am Ende des Weges.</param>
/// <param name="Hin">Die Stücke vom Halteplatz bis zum Halt am Markt (wie <see cref="Stadtwirtschaft.Fahrten"/> nach dem Bau).</param>
/// <param name="Zurueck">Dieselben rückwärts.</param>
/// <param name="Punkte">Der Verlauf: Stück i führt von Punkte[i] nach Punkte[i + 1].</param>
/// <param name="Bester">Der Weg, den die Karren der Werkstatt nach dem Bau fahren würden (zum nächsten Platz, wie <see cref="Stadtwirtschaft.MarktplatzVon"/>).</param>
public sealed record Fahrweg(Kartengebaeude Markt, IReadOnlyList<Fahrtstueck> Hin, IReadOnlyList<Fahrtstueck> Zurueck, IReadOnlyList<Kartenort> Punkte, bool Bester);

/// <summary>
/// Die Wege von einem Bauort zu allen Marktplätzen (Anzeige des Fahrwegs im Baumodus, Wunsch des Nutzers vom 01.11.2026): Die Darstellung zeichnet
/// sie auf die Wege der Karte und färbt sie nach der aufgelaufenen Fahrzeit. Der beste Weg ist genau die Strecke, die der Kern nach dem Bau rechnet
/// (<see cref="StreckeWieImBild"/>); die übrigen führen auf dieselbe Weise (Zubringer, Netz aller Wege, Halt vor dem Pflaster) zu den anderen
/// Plätzen. Ändert nichts, steht nicht im Spielstand.
/// </summary>
public sealed partial class Stadtwirtschaft
{
    /// <summary>
    /// Die Wege der Karren von diesem Bauort zu jedem erreichbaren Marktplatz der Karte (in der Reihenfolge der Datei, der beste zuerst) – leer
    /// ohne Karte, ohne Bauwesen, für Umland, mit alten Daten (vor Datenversion 65 hat die Strecke keinen Verlauf) oder ohne Weg zu einem Markt.
    /// Der beste ist dieselbe Strecke wie <see cref="FahrzeitAmBauort"/>; Plätze ohne Weg fehlen.
    /// </summary>
    public IReadOnlyList<Fahrweg> FahrwegeAmBauort(WerkstattBauen ort)
    {
        if (Karte is not { } karte || Karrennetz is not { } netz || !Bauen.IstAktiv || !NachWeglaengeGerechnet
            || !Daten.Betriebsarten.TryGetValue(ort.Art, out var artDaten) || !artDaten.InDerStadt)
            return Array.Empty<Fahrweg>();
        var platz = new Betriebsplatz("", null, new Grundstueck(-1, Bauen.Grundriss(ort), Grundstuecksart.Werkstatt, null, null));
        var start = Karrenweg.Start(platz, ort.Art, artDaten, karte.Katalog);
        var (hin, zurueck, bester, punkte) = StreckeWieImBild(start, karte, netz);
        if (bester is null || punkte is not { Count: >= 2 }) return Array.Empty<Fahrweg>();
        var wege = new List<Fahrweg> { new(bester, hin, zurueck, punkte, true) };
        var (anker, hof) = start;
        foreach (var m in karte.Marktplaetze)
        {
            if (ReferenceEquals(m, bester)) continue;
            var fahrt = Karrenweg.BisZumHalt(Karrenweg.MitZubringer(netz.FahrtMitVerlauf(hof, Mitte(m)), anker, hof, karte.Gelaendeform), Karrenweg.Halt(m));
            if (fahrt.Stuecke.Count == 0 || fahrt.Punkte.Count < 2) continue;
            wege.Add(new Fahrweg(m, fahrt.Stuecke, Karrenweg.Umgekehrt(fahrt.Stuecke), fahrt.Punkte, false));
        }
        return wege;
    }

    /// <summary>
    /// Wie lange ein Karren dieser Art auf dem Weg fährt (Spielminuten hin und zurück): dieselbe Rechnung wie <see cref="Fahrzeit(Betrieb, string, bool)"/>
    /// (ganze Minuten, Mindest- und Höchstdauer). Für den besten Weg dasselbe wie <see cref="FahrzeitAmBauort"/>.
    /// </summary>
    public (int Hin, int Zurueck) FahrzeitenAuf(Fahrweg weg, Betriebsart art, string karrenTyp)
    {
        var basis = Daten.Logistik.FahrzeitAb(Daten.Art(art).Ort);
        return NachStrecke(karrenTyp) ? Zeiten(weg.Hin, weg.Zurueck, karrenTyp, basis) : (basis, basis);
    }

    /// <summary>
    /// Wie viele Spielminuten ein Karren dieser Art bis zu jedem Punkt des Weges unterwegs ist (ungerundet, ohne Mindest- und Höchstdauer; Punkt 0:
    /// 0) – für den Farbverlauf der Anzeige. Jedes Stück wie in <see cref="Fahrminuten"/> (Länge × Zeitfaktor ÷ Tempo), eine Furt oder Fähre zählt
    /// ihre Minuten ab dem Punkt, an dem sie beginnt. Ohne Tempo der Art (alte Daten): null.
    /// </summary>
    public double[]? Zwischenzeiten(Fahrweg weg, string karrenTyp)
    {
        if (NachWeglaenge(karrenTyp) is not { } art) return null;
        var wasser = Wasserhalt;
        var minuten = new double[weg.Punkte.Count];
        var vorher = Fahrbahn.Strasse;
        for (var i = 0; i < weg.Hin.Count && i + 1 < minuten.Length; i++)
        {
            var s = weg.Hin[i];
            var aufschlag = wasser is not null && s.Art != vorher ? wasser.Aufschlag(new[] { s }) : 0;
            vorher = s.Art;
            minuten[i + 1] = minuten[i] + aufschlag + s.Laenge * Zeitfaktor(s) / Math.Max(art.MeterJeMinute, 1e-9);
        }
        return minuten;
    }
}
