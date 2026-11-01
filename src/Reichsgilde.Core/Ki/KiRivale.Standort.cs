using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Ki;

/// <summary>
/// Bauort einer neuen Werkstatt nach dem Karrenweg (phase5/gehtempo-ki; offener Punkt aus „Mehrere Marktplätze“). Seit der Kern die Fahrzeit nach
/// Weglänge rechnet (Datenversion 65), entscheidet der Weg des Karrens über eine Werkstatt, nicht die Luftlinie zum Platz: In der Greybox liegt
/// der Luftlinien-erste Bauort (0 | 25, Vorderseite nach Süden) 25 m vom Marktplatz, sein Hof aber an der Gasse auf der falschen Seite – der
/// Handkarren braucht 131 Minuten. Drei Meter daneben, nach Osten gedreht, sind es 24. Die KI baute dort Schänken und Tinktureien, die fast die
/// Hälfte der Zeit stillstanden.
///
/// Deshalb nimmt die KI unter den <see cref="StandortKandidaten"/> nächsten Bauorten nach Luftlinie den mit der kürzesten Fahrt des Startkarrens
/// hin und zurück (Gleichstand: der nähere). Die Fahrzeit fragt sie beim Kern ab (<see cref="Stadtwirtschaft.FahrzeitAmBauort"/>): dieselbe
/// Rechnung, die der Kern nach dem Bau für den Betrieb macht, also auch das, was der Spieler beim Bauen sehen könnte.
/// </summary>
public sealed partial class KiRivale
{
    /// <summary>Unter so vielen Bauorten – den nächsten nach Luftlinie zum nächsten Marktplatz – sucht die KI den mit dem kürzesten Karrenweg.</summary>
    public const int StandortKandidaten = 20;

    /// <summary>
    /// Wie lange ein Karren der Art <paramref name="karrenTyp"/> von einer an diesem Ort gebauten Werkstatt zum Markt und zurück führe (Spielminuten,
    /// aus <see cref="Stadtwirtschaft.FahrzeitAmBauort"/>). Null ohne Fahrzeit nach Weglänge (Abschnitt <c>logistik.weglaenge</c> fehlt, keine Karte,
    /// kein Netz) – dann zählt die Luftlinie wie bisher.
    /// </summary>
    internal (int Hin, int Zurueck)? FahrzeitAmBauort(WerkstattBauen ort, string karrenTyp) =>
        _w.Daten.Logistik.Weglaenge is not null && _w.FahrzeitAmBauort(ort, karrenTyp) is { } hin && _w.FahrzeitAmBauort(ort, karrenTyp, zumMarkt: false) is { } zurueck
            ? (hin, zurueck)
            : null;

    /// <summary>
    /// Der Bauort für eine neue Werkstatt der Art <paramref name="art"/>: aus den Bauorten, geordnet nach Luftlinie zum nächsten Marktplatz
    /// (<paramref name="nachLuftlinie"/>), unter den ersten <see cref="StandortKandidaten"/> der mit der kürzesten Fahrt des Startkarrens hin und
    /// zurück; bei Gleichstand der nähere. Ohne Fahrzeit nach Weglänge oder ohne Startkarren der erste. Dazu die Fahrzeit zum Markt (null: nicht gerechnet).
    /// </summary>
    private (BauBefehl? Ort, int? Minuten) Bauort(IReadOnlyList<BauBefehl> nachLuftlinie, Betriebsart art)
    {
        var erster = nachLuftlinie.FirstOrDefault();
        var typ = _w.Daten.Art(art).Startkarren;
        if (erster is not WerkstattBauen w0 || typ == "" || FahrzeitAmBauort(w0, typ) is null) return (erster, null);
        var best = nachLuftlinie.Take(StandortKandidaten)
            .Select((o, i) => (Ort: o, Rang: i, Zeit: o is WerkstattBauen wb ? FahrzeitAmBauort(wb, typ) : null))
            .Where(x => x.Zeit is not null)
            .OrderBy(x => x.Zeit!.Value.Hin + x.Zeit.Value.Zurueck)
            .ThenBy(x => x.Rang)
            .First();
        return (best.Ort, best.Zeit!.Value.Hin);
    }
}
