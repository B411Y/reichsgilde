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
///
/// Hafen (phase5/ki-hafen, offener Punkt „Hafenziel“ aus kern.md): Seit Datenversion 73 fährt ein Karren zum Handeln an den Hafen, wenn der offen
/// ist und Hin- plus Rückfahrt dorthin kürzer dauern als zum Marktplatz (<see cref="Stadtwirtschaft.FaehrtZumHafen"/>); gehandelt wird dort mit
/// demselben Markt. Die KI suchte aber nur unter den Bauorten nahe am Marktplatz und rechnete nur die Fahrt dorthin, also fuhr nie ein Karren der
/// KI zum Hafen. Jetzt wertet sie jeden Bauort nach der Fahrt, die sein Karren im Mittel über die Jahre macht (<see cref="ErwarteteRundfahrt"/>):
/// zum Hafen, solange der offen ist und näher liegt, im Winter zum Marktplatz. Dazu nimmt sie die <see cref="StandortKandidaten"/> nächsten
/// Bauorte am Hafen hinzu – nur die, an denen der Hafen wirklich der nähere Handelsort ist (sonst brächte die Nähe zum Hafen nichts). Nach den Waren
/// des Betriebs gewichtet sie nicht: Am Hafen gilt derselbe Markt, die kürzere Fahrt hilft jeder Ware gleich (Designfrage beim Teamleiter).
/// </summary>
public sealed partial class KiRivale
{
    /// <summary>
    /// Unter so vielen Bauorten – den nächsten nach Luftlinie zum nächsten Marktplatz – sucht die KI den mit dem kürzesten Karrenweg; fahren Karren
    /// zum Hafen, dazu unter ebenso vielen nächsten am Hafen.
    /// </summary>
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
    /// Wie lange derselbe Karren zum nächsten Hafen und zurück führe (<see cref="Stadtwirtschaft.FahrzeitZumHafenAmBauort"/>, dieselbe Rechnung wie
    /// nach dem Bau). Null, wenn die Karren nicht zum Hafen fahren (kein Hafen auf der Karte, Schalter <c>schiffshandel.karrenZumHafen</c> aus,
    /// alte Daten) oder kein Weg dorthin führt. Ob der Hafen gerade offen ist, zählt hier nicht (siehe <see cref="HafenOffenAnteil"/>).
    /// </summary>
    internal (int Hin, int Zurueck)? FahrzeitZumHafenAmBauort(WerkstattBauen ort, string karrenTyp) =>
        _w.FahrzeitZumHafenAmBauort(ort, karrenTyp) is { } hin && _w.FahrzeitZumHafenAmBauort(ort, karrenTyp, hin: false) is { } zurueck
            ? (hin, zurueck)
            : null;

    /// <summary>
    /// Welcher Anteil der Spieljahre den Hafen offen sieht: Mit Jahreszeiten ist jedes vierte Spieljahr Winter, dann ist der Hafen zu (Eisschollen,
    /// im strengen Winter zugefroren; <see cref="Schiffahrt.HafenOffen"/>) und jeder Karren fährt zum Marktplatz – also 3/4. Ohne Jahreszeiten 1.
    /// Ob der Schiffshandel ruht, spielt keine Rolle: Die Karren fahren auch dann an den Hafen.
    /// </summary>
    internal double HafenOffenAnteil => _w.Jahreszeiten.IstAktiv ? 0.75 : 1;

    /// <summary>
    /// Wie lange die Fahrt eines Karrens (hin und zurück) über die Jahre im Mittel dauert, wenn er <paramref name="markt"/> Minuten zum Marktplatz
    /// und <paramref name="hafen"/> zum Hafen bräuchte (null: kein Weg zum Hafen): Ist der Hafen näher, fährt er dorthin, solange er offen ist
    /// (<see cref="HafenOffenAnteil"/>), sonst zum Marktplatz – wie der Kern beim Aufbruch wählt (<see cref="Stadtwirtschaft.FaehrtZumHafen"/>,
    /// Gleichstand: Marktplatz). Die Minuten sind die der Jahreszeit, in der die KI entscheidet (Schlamm im Frühling, im strengen Winter die
    /// Fähre im Eis); für die anderen Jahreszeiten nimmt sie dieselben – eine Abfrage je Jahreszeit hat der Kern nicht (Befund der Prüfung, Offen).
    /// </summary>
    internal double ErwarteteRundfahrt(int markt, int? hafen) =>
        hafen is { } h && h < markt ? markt - HafenOffenAnteil * (markt - h) : markt;

    /// <summary>Ein geprüfter Bauort: Rang in der Liste der Kandidaten, Fahrzeit hin und zurück zum Markt und zum Hafen (null: kein Weg dorthin).</summary>
    internal readonly record struct Standortwahl(BauBefehl Ort, int Rang, (int Hin, int Zurueck) Markt, (int Hin, int Zurueck)? Hafen, double Rundfahrt)
    {
        /// <summary>Fährt der Karren von hier zum Hafen, solange der offen ist?</summary>
        public bool ZumHafen => Hafen is { } h && h.Hin + h.Zurueck < Markt.Hin + Markt.Zurueck;
    }

    /// <summary>
    /// Die Bauorte, unter denen die KI wählt, mit ihren Fahrzeiten: die ersten <see cref="StandortKandidaten"/> aus <paramref name="nachLuftlinie"/>
    /// (Luftlinie zum nächsten Marktplatz), dahinter – nur wenn Karren zum Hafen fahren – die <see cref="StandortKandidaten"/> nächsten nach Luftlinie
    /// zum nächsten Hafen, die noch nicht dabei sind und an denen der Hafen der nähere Handelsort ist. Ohne Fahrzeit nach Weglänge leer.
    /// </summary>
    internal List<Standortwahl> Standortkandidaten(IReadOnlyList<BauBefehl> nachLuftlinie, string karrenTyp)
    {
        var liste = new List<Standortwahl>();
        void Pruefen(BauBefehl o, bool nurZumHafen)
        {
            if (o is not WerkstattBauen wb) return;
            var hafen = FahrzeitZumHafenAmBauort(wb, karrenTyp);
            if (nurZumHafen && hafen is null) return; // (zuerst der Hafen: ohne Weg dorthin spart das die Suche zum Markt)
            if (FahrzeitAmBauort(wb, karrenTyp) is not { } markt) return;
            var wahl = new Standortwahl(o, liste.Count, markt, hafen, ErwarteteRundfahrt(markt.Hin + markt.Zurueck, hafen is { } h ? h.Hin + h.Zurueck : null));
            if (!nurZumHafen || wahl.ZumHafen) liste.Add(wahl);
        }
        var amMarkt = nachLuftlinie.Take(StandortKandidaten).ToList();
        foreach (var o in amMarkt) Pruefen(o, nurZumHafen: false);
        if (!_w.Schiffahrt.KarrenZumHafen || _w.Schiffahrt.Hafengebaeude is not { Count: > 0 } haefen) return liste;
        // Luftlinie zum nächsten Hafen (Mitte des Gebäudes, wie MarktAbstandQuadrat); OrderBy ist stabil – bei Gleichstand die Reihenfolge nach dem Markt.
        double AbstandQuadrat(BauBefehl o) => haefen.Min(h => (o.X - h.Punkt[0]) * (o.X - h.Punkt[0]) + (o.Z - h.Punkt[1]) * (o.Z - h.Punkt[1]));
        foreach (var o in nachLuftlinie.Skip(StandortKandidaten).OrderBy(AbstandQuadrat).Take(StandortKandidaten))
            Pruefen(o, nurZumHafen: true);
        return liste;
    }

    /// <summary>
    /// Der Bauort für eine neue Werkstatt der Art <paramref name="art"/>: unter den <see cref="Standortkandidaten"/> der mit der kürzesten
    /// erwarteten Fahrt des Startkarrens hin und zurück (<see cref="ErwarteteRundfahrt"/>); bei Gleichstand der frühere (nähere am Markt).
    /// Ohne Fahrzeit nach Weglänge oder ohne Startkarren der erste nach Luftlinie (<paramref name="nachLuftlinie"/>). Dazu die Wahl mit ihren
    /// Fahrzeiten (null: nicht gerechnet).
    /// </summary>
    internal (BauBefehl? Ort, Standortwahl? Wahl) Bauort(IReadOnlyList<BauBefehl> nachLuftlinie, Betriebsart art)
    {
        var erster = nachLuftlinie.FirstOrDefault();
        var typ = _w.Daten.Art(art).Startkarren;
        if (erster is not WerkstattBauen w0 || typ == "" || FahrzeitAmBauort(w0, typ) is null) return (erster, null);
        var best = Standortkandidaten(nachLuftlinie, typ)
            .OrderBy(x => x.Rundfahrt)
            .ThenBy(x => x.Rang)
            .First();
        return (best.Ort, best);
    }
}
