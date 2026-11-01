using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
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
/// hin und zurück (Gleichstand: der nähere). Die Fahrzeit rechnet sie aus den öffentlichen Bausteinen des Kerns genau so, wie der Kern die
/// Strecke eines gebauten Betriebs rechnet (<c>Stadtwirtschaft.StreckeWieImBild</c>, <see cref="Karrenweg"/>): Halteplatz am Modell, Hof,
/// Zubringer, Netz aller Wege, Halt am Pflaster. Der Test <c>KiStandortTests</c> baut an solchen Orten und vergleicht mit dem Kern – ändert der
/// Kern seine Strecke, fällt er auf. Besser wäre eine Abfrage des Kerns für einen geplanten Bauort (Bitte an den Kern, siehe docs/bereiche/kern.md).
/// </summary>
public sealed partial class KiRivale
{
    /// <summary>Unter so vielen Bauorten – den nächsten nach Luftlinie zum nächsten Marktplatz – sucht die KI den mit dem kürzesten Karrenweg.</summary>
    public const int StandortKandidaten = 20;

    /// <summary>
    /// Wie lange ein Karren der Art <paramref name="karrenTyp"/> von einer an diesem Ort gebauten Werkstatt zum Markt und zurück führe (Spielminuten,
    /// wie <see cref="Stadtwirtschaft.Fahrzeit(Betrieb, string, bool)"/> nach dem Bau: auf ganze Minuten, zwischen Mindest- und Höchstdauer; ohne
    /// Weg die feste Fahrzeit). Null ohne Fahrzeit nach Weglänge (alte Daten, keine Karte, kein Netz) – dann zählt die Luftlinie wie bisher.
    /// </summary>
    internal (int Hin, int Zurueck)? FahrzeitAmBauort(WerkstattBauen ort, string karrenTyp)
    {
        var l = _w.Daten.Logistik;
        if (l.Weglaenge is not { } grenzen || _w.Karte is not { } karte || _w.Karrennetz is not { } netz
            || !l.Karren.TryGetValue(karrenTyp, out var art) || art.Tempo <= 0) return null;
        // Halteplatz und Hof wie Karrenweg.Start für eine Werkstatt auf einem Grundstück: Mitte der Fläche, gedreht wie sie, Hof in der Mitte des Vorplatzes.
        var g = _w.Bauen.Grundriss(ort);
        var drehung = g.Drehung * 90.0;
        Kartenort Bei(double lx, double lz)
        {
            var d = Kartengeometrie.Dreh(lx, lz, drehung);
            return new Kartenort(g.X + d.X, g.Z + d.Z);
        }
        var hof = Bei(0, g.Tiefe / 2 - Karrenweg.HofVorKante);
        var anker = karte.Katalog?.Werkstatt(_w.Daten.Art(ort.Art).Beruf)?.KarrenAnker is { Length: 2 } a ? Bei(a[0], a[1]) : hof;
        // Die Strecke wie Stadtwirtschaft.StreckeWieImBild: über das Pflaster, wenn der Hof näher am Platz als am Weg liegt; sonst zum Platz mit den
        // kleinsten Kosten der Wegwahl (bei Gleichstand der erste der Karte); ohne Marktplatz zum Ursprung.
        var gelaende = karte.Gelaendeform;
        var maerkte = karte.Marktplaetze;
        IReadOnlyList<Fahrtstueck> hin;
        if (Karrenweg.UeberDasPflaster(anker, hof, maerkte, netz.AbstandZumWeg(hof), gelaende) is var (gerade, _))
            hin = gerade.Stuecke;
        else if (maerkte.Count == 0)
            hin = Karrenweg.MitZubringer(netz.FahrtMitVerlauf(hof, default), anker, hof, gelaende).Stuecke;
        else
        {
            (IReadOnlyList<Fahrtstueck> Stuecke, double Kosten) best = (Array.Empty<Fahrtstueck>(), double.MaxValue);
            foreach (var m in maerkte)
            {
                var fahrt = Karrenweg.BisZumHalt(
                    Karrenweg.MitZubringer(netz.FahrtMitVerlauf(hof, new Kartenort(m.Punkt[0], m.Punkt[1])), anker, hof, gelaende), Karrenweg.Halt(m));
                if (fahrt.Stuecke.Count > 0 && Karrennetz.Kosten(fahrt.Stuecke) is var kosten && kosten < best.Kosten - 1e-9) best = (fahrt.Stuecke, kosten);
            }
            hin = best.Stuecke;
        }
        var fest = l.FahrzeitAb(Arbeitsort.Werkstatt);
        if (hin.Count == 0) return (fest, fest);
        int Zeit(IReadOnlyList<Fahrtstueck> fahrt) =>
            Math.Clamp(Math.Max(1, (int)Math.Round(_w.Fahrminuten(fahrt, art.MeterJeMinute), MidpointRounding.AwayFromZero)), grenzen.MindestMinuten, grenzen.HoechstMinuten);
        return (Zeit(hin), Zeit(Karrenweg.Umgekehrt(hin)));
    }

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
