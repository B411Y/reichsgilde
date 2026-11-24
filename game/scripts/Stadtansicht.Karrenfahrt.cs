using Godot;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Karren selbst fahren, Sonderfahrten (Darstellung, Paket K3; Teil der <c>partial class Stadtansicht</c>). Die gewohnte Fahrt Heimatbetrieb ↔ Markt fährt
/// <see cref="Karrenzug"/> (Stadtansicht.Karren.cs). Fährt der Spieler seine Karren selbst, gibt es drei Fälle, die der Zug nicht kennt:
/// <list type="bullet">
/// <item>der Karren <b>steht an einem anderen eigenen Betrieb</b> (<see cref="Karren.StandortBetrieb"/>): am Halteplatz jenes Betriebs, quer versetzt nach der Nummer;</item>
/// <item>er fährt <b>von einem anderen Betrieb zum Markt</b>, <b>vom Markt zu einem anderen Betrieb</b> oder <b>von Betrieb zu Betrieb</b>;</item>
/// <item>Betrieb → Betrieb fährt die Strecke des Kerns (<see cref="Stadtwirtschaft.KarrenverlaufZwischen"/>, dieselben Punkte wie die Fahrzeit); ohne sie (alte Daten,
/// ohne Karte) über den Markt: Weg des ersten bis zum Halt, Weg des zweiten rückwärts.</item>
/// </list>
/// Diese Fahrten laufen <b>nach der Zeit des Kerns</b>, ohne eigenen Zustand: Der Anteil der Strecke ist die vergangene Fahrzeit durch die Fahrzeit
/// (<see cref="Karren.Ankunft"/>). Der Karren ist so immer dort, wo ihn der Kern hat; nur Wartezeiten an der Klappbrücke strecken die Fahrt gleichmäßig.
/// Am Markt (<see cref="KarrenZustand.AmMarkt"/>) und bei der Rückfahrt Markt → Heimatbetrieb zeigt weiter der Zug.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Gehört dieser Karren gerade nicht auf den gewohnten Weg Heimatbetrieb ↔ Markt?</summary>
    private static bool KarrenIstSonder(Karren karren, Stadtwirtschaft w) => karren.Zustand switch
    {
        KarrenZustand.AmBetrieb => karren.StandortBetrieb is not null,
        KarrenZustand.ZumMarkt => karren.Herkunft(w) != karren.Heimat,
        KarrenZustand.ZumBetrieb => !(karren.Herkunft(w) is null && karren.Ziel(w) == karren.Heimat),
        _ => false,
    };

    /// <summary>Gebaute Sonderwege (Herkunft, Ziel, Heimat; Betriebsnamen, leer = Markt). Fällt weg, wenn sich die Betriebe ändern.</summary>
    private readonly Dictionary<(string Von, string Nach, string Heimat, bool Hafen), Vector3[]?> _karrenSonderwege = new();
    private int _karrenSonderwegeBetriebe = -1;

    /// <summary>Wo der Karren eines Betriebs am Markt hält (Ende seines Weges); null ohne Weg.</summary>
    private Vector3? KarrenMarktHalt(Betrieb betrieb) => KarrenHandelsortHalt(betrieb, false);

    /// <summary>Wo der Karren eines Betriebs am Marktplatz (<paramref name="hafen"/> false) oder am Hafen (true) hält (Ende seines Weges dorthin); null ohne Weg.</summary>
    private Vector3? KarrenHandelsortHalt(Betrieb betrieb, bool hafen) =>
        _betriebe.TryGetValue(betrieb, out var a) && KarrenWeg(a, hafen) is { Length: >= 2 } weg ? weg[^1] : null;

    /// <summary>
    /// Der Weg einer Sonderfahrt von <paramref name="von"/> nach <paramref name="nach"/> (null: der Markt): Betrieb → Markt der Weg des Betriebs bis zum Halt und gerade weiter
    /// zum Halt des Heimatbetriebs (dort steht der Karren dann); Markt → Betrieb vom Halt des Heimatbetriebs den Weg des Betriebs rückwärts; Betrieb → Betrieb die Strecke des Kerns.
    /// Null, wenn ein Betrieb im Bild fehlt.
    /// </summary>
    private Vector3[]? KarrenSonderWeg(Karren karren, Betrieb? von, Betrieb? nach, bool hafen = false)
    {
        if (_wirtschaft is not { } w) return null;
        if (_karrenSonderwegeBetriebe != _betriebe.Count) { _karrenSonderwege.Clear(); _karrenSonderwegeBetriebe = _betriebe.Count; }
        var schluessel = (von?.Name ?? "", nach?.Name ?? "", karren.Heimat.Name, hafen);
        if (_karrenSonderwege.TryGetValue(schluessel, out var bekannt)) return bekannt;
        Vector3[]? weg = null;
        var heimatHalt = KarrenHandelsortHalt(karren.Heimat, hafen);
        if (von is null && nach is null)
        {
            // K4: von einem Handelsort zum anderen (Markt ↔ Hafen): vom Halt des einen über die Strecke des Kerns zum Halt des anderen (Ziel ist der Hafen, wenn hafen)
            var anfang = KarrenHandelsortHalt(karren.Heimat, !hafen);
            var liste = new List<Vector3>();
            if (anfang is { } a0) liste.Add(a0);
            foreach (var p in w.StreckeZwischenHandelsorten(karren.Heimat, hafen)?.Punkte ?? [])
            {
                var punkt = new Vector3((float)p.X, 0, (float)p.Z);
                if (liste.Count == 0 || liste[^1].DistanceTo(punkt) > 0.5f) liste.Add(punkt);
            }
            if (heimatHalt is { } e0 && (liste.Count == 0 || liste[^1].DistanceTo(e0) > 0.5f)) liste.Add(e0);
            weg = liste.ToArray();
        }
        else if (von is not null && nach is not null)
        {
            if (w.KarrenverlaufZwischen(von, nach) is { Count: >= 2 } punkte)
                weg = punkte.Select(p => new Vector3((float)p.X, 0, (float)p.Z)).ToArray();
            else if (_betriebe.TryGetValue(von, out var a) && _betriebe.TryGetValue(nach, out var b))
                weg = KarrenWeg(a).Concat(KarrenWeg(b).Reverse()).ToArray();
        }
        else if (von is not null && _betriebe.TryGetValue(von, out var vonAnsicht))
        {
            var liste = KarrenWeg(vonAnsicht, hafen).ToList();
            if (heimatHalt is { } h && liste[^1].DistanceTo(h) > 0.5f) liste.Add(h);
            weg = liste.ToArray();
        }
        else if (nach is not null && _betriebe.TryGetValue(nach, out var nachAnsicht))
        {
            var liste = KarrenWeg(nachAnsicht, hafen).Reverse().ToList();
            if (heimatHalt is { } h && liste[0].DistanceTo(h) > 0.5f) liste.Insert(0, h);
            weg = liste.ToArray();
        }
        if (weg is { Length: < 2 }) weg = null;
        return _karrenSonderwege[schluessel] = weg;
    }

    /// <summary>
    /// Zeigt den Karren, wenn er auf einer Sonderfahrt ist oder an einem anderen Betrieb steht (siehe oben): true, dann ist er gesetzt und der Aufrufer ist fertig; false:
    /// die gewohnte Fahrt (<see cref="Karrenzug"/>).
    /// </summary>
    private bool KarrenSonderBewegen(Karren karren, int nummer, KarrenAnsicht gezeigt, double jetztGenau, double delta)
    {
        if (_wirtschaft is not { } w || _simulation is not { } sim || !KarrenIstSonder(karren, w)) return false;
        var (von, nach) = (karren.Herkunft(w), karren.Ziel(w));
        var steht = karren.Zustand == KarrenZustand.AmBetrieb;
        // Am anderen Betrieb: der Weg dieses Betriebs, der Karren steht an seinem Anfang (dem Halteplatz)
        var weg = steht ? (von is not null && _betriebe.TryGetValue(von, out var standort) ? KarrenWeg(standort) : null) : KarrenSonderWeg(karren, von, nach, karren.ZielHafen);
        if (weg is not { Length: >= 2 }) return false; // kein Weg im Bild: die gewohnte Fahrt zeigt ihn (am Heimatbetrieb)
        var laenge = WegLaenge(weg);

        // Fahrzeit des Kerns nur neu holen, wenn eine neue Fahrt begonnen hat
        var schluessel = (karren.Zustand, karren.Ankunft, von?.Name ?? "", nach?.Name ?? "");
        if (!steht && gezeigt.SonderSchluessel != schluessel)
        {
            gezeigt.SonderSchluessel = schluessel;
            gezeigt.SonderDauer = Math.Max(1, w.KarrenFahrt(karren, von, nach).Minuten);
        }
        var anteil = steht ? 0f : (float)(1 - Math.Clamp((karren.Ankunft - jetztGenau) / gezeigt.SonderDauer, 0, 1));

        // Zugpunkt (Tier bzw. Zieher) liegt um die Deichsellänge voraus, das Gespann hängt nach (wie auf dem gewohnten Weg)
        var deichsel = gezeigt.Lauf is { } lauf ? (float)lauf.Deichsel : 0f;
        var vor = deichsel / Math.Max(laenge, 1f);
        var ziel = PunktAufWegVerlaengert(weg, anteil + vor, laenge);
        var blick = PunktAufWegVerlaengert(weg, anteil + vor + 1f / Math.Max(laenge, 1f), laenge);
        var fahrt = blick - ziel;
        fahrt.Y = 0;
        if (fahrt.LengthSquared() > 1e-6f)
        {
            var rechts = new Vector3(-fahrt.Z, 0, fahrt.X).Normalized();
            if (steht) // mehrere Karren am selben Betrieb nebeneinander, die Nummer bestimmt den Platz (Gespannbreite plus Luft)
            {
                var seite = rechts * (2.2f + 3.2f * nummer);
                ziel += seite;
                blick += seite;
            }
            else
            {
                ziel += rechts * KarrenSpur; // Rechtsverkehr wie auf dem gewohnten Weg
                blick += rechts * KarrenSpur;
            }
        }

        var neu = !gezeigt.Sonder; // das erste Bild der Sonderfahrt: gleich dort, ohne Schleppkurve
        gezeigt.Sonder = true;
        gezeigt.AufWeg = false; // die Folgeregel der Karren am Heimatbetrieb kennt ihn nicht
        gezeigt.WegRef = null;
        var imBild = KarrenImBild(gezeigt.Wurzel.GlobalPosition);
        var geschwindigkeit = steht || anteil >= 1f || sim.Takt.IstPausiert ? 0f : (float)(laenge / gezeigt.SonderDauer * sim.Takt.TicksProSekunde);
        gezeigt.Geschwindigkeit = geschwindigkeit;
        if (geschwindigkeit > 0.01f) gezeigt.Tempo = geschwindigkeit;
        KarrenPoseSetzen(gezeigt, ziel, blick, neu || steht, imBild, delta);
        KarrenLadungZeigen(karren, gezeigt);
        KarrenAnimieren(karren, gezeigt, geschwindigkeit, delta);
        return true;
    }
}
