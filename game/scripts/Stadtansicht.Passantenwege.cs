using Godot;
using Reichsgilde.Core.Buerger;

namespace Reichsgilde;

/// <summary>
/// Wege der Passanten (Teil der <c>partial class Stadtansicht</c>, zu Stadtansicht.Passanten.cs; Nutzer, 1.11.2026):
///   • <b>Wegbreite</b>: Wie breit der Weg an jedem Stück eines Pfades ist, aus den Wegen der Karte (<c>breite</c> in <c>game/karten/*.json</c>), den
///     Anschlüssen an Türen und Höfe und dem Pflaster der Marktplätze. Die Spur (<see cref="Passantenspur"/>) nutzt sie, und Ausweichen bleibt in ihr.
///   • <b>Wegeinstieg</b>: Wer unterwegs einen neuen Weg bekommt, steigt vom Wegstück aus ein, auf dem er ist, gleich in Zielrichtung
///     (<see cref="Wegeinstieg"/>, <see cref="Gassennetz.PfadZwischen"/>), statt erst zum nächsten Knoten zurückzugehen.
///   • <b>Ohne Umkehr</b>: Kein Pfad geht bis zu einem Punkt und auf derselben Linie wieder zurück.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Auf dem Pflaster eines Marktplatzes gehen die Passanten in dieser halben Breite (Stände, Brunnen und Bänke umgehen sie mit nur 0,3 m Abstand).</summary>
    private const float PflasterHalb = 1.0f;

    /// <summary>Gemerkte halbe Breiten je Stück (Schlüssel aus den gerundeten Enden): Die meisten Stücke sind Kanten des Netzes und kommen oft vor.</summary>
    private readonly Dictionary<(int, int, int, int), float> _stueckBreiten = new();
    private int _stueckBreitenStand = -1;

    /// <summary>Die Mittellinie eines Pfades mit der Breite jedes Stücks (für <see cref="Passantenspur.IMittellinie.Halbbreite"/>).</summary>
    private PfadMitte PfadMitteBauen(Vector3[] pfad) => new(pfad, PfadBreiten(pfad)) { OhneRundung = SpurV1 };

    /// <summary>Die halbe Breite jedes Stücks eines Pfades. Mit <c>--passantenspur=v1</c> überall das Grundmaß.</summary>
    private float[] PfadBreiten(Vector3[] pfad)
    {
        var halbe = new float[Math.Max(pfad.Length - 1, 1)];
        for (var i = 0; i < halbe.Length; i++)
            halbe[i] = SpurV1 || pfad.Length < 2 ? Passantenspur.Gassenbreite / 2 : StueckHalbbreite(pfad[i], pfad[i + 1]);
        return halbe;
    }

    /// <summary>
    /// Die halbe Breite des Weges, auf dem das Stück a–b liegt: auf dem Pflaster <see cref="PflasterHalb"/>; sonst der Weg der Karte (oder das
    /// Anschlussstück), der in der Mitte des Stücks am nächsten liegt (höchstens 0,6 m daneben) und in dieselbe Richtung läuft; ohne einen solchen ein
    /// Türweg. Gemerkt je Stück; neu, sobald Wege dazukommen (Kartenwerkstatt, Bauen).
    /// </summary>
    private float StueckHalbbreite(Vector3 a, Vector3 b)
    {
        // auf dem Pflaster gleich (nicht gemerkt: die Wege über die freie Fläche sind je Person andere Stücke)
        if (AufDemPflaster(new Vector3((a.X + b.X) / 2, 0, (a.Z + b.Z) / 2))) return PflasterHalb;
        var stand = _wegStrecken.Count * 7919 + _anschlussStrecken.Count;
        if (stand != _stueckBreitenStand) { _stueckBreiten.Clear(); _stueckBreitenStand = stand; }
        var (ka, kb) = ((Mathf.RoundToInt(a.X * 10), Mathf.RoundToInt(a.Z * 10)), (Mathf.RoundToInt(b.X * 10), Mathf.RoundToInt(b.Z * 10)));
        if (ka.CompareTo(kb) > 0) (ka, kb) = (kb, ka);
        var schluessel = (ka.Item1, ka.Item2, kb.Item1, kb.Item2);
        if (_stueckBreiten.TryGetValue(schluessel, out var gemerkt)) return gemerkt;

        var mitte = new Vector3((a.X + b.X) / 2, 0, (a.Z + b.Z) / 2);
        var richtung = new Vector3(b.X - a.X, 0, b.Z - a.Z);
        var lang = richtung.Length() > 0.3f;
        if (lang) richtung /= richtung.Length();
        var halb = TuerwegBreite / 2;
        if (AufDemPflaster(mitte)) halb = PflasterHalb;
        else
        {
            var abstand = 0.6f;
            foreach (var liste in new[] { _wegStrecken, _anschlussStrecken })
                foreach (var (von, bis, breite) in liste)
                {
                    var (v, w) = (von with { Y = 0 }, bis with { Y = 0 });
                    var d = Geometry3D.GetClosestPointToSegment(mitte, v, w).DistanceTo(mitte);
                    if (d >= abstand) continue;
                    var strecke = w - v;
                    if (lang && strecke.LengthSquared() > 1e-4f && Mathf.Abs(strecke.Normalized().Dot(richtung)) < 0.8f) continue; // quer dazu: ein anderer Weg
                    (abstand, halb) = (d, breite / 2);
                }
        }
        _stueckBreiten[schluessel] = halb;
        return halb;
    }

    /// <summary>
    /// Der neue Weg beginnt für diese Figur dort, wo sie gerade geht (<paramref name="strecke"/> Meter auf dem <paramref name="altPfad"/>): Sie steigt
    /// vom Wegstück aus ein, vorwärts oder rückwärts am nächsten Knoten, und verlässt das Netz am Knoten des neuen Pfades, von dem aus der Rest am
    /// kürzesten ist – alles zusammen in einem Durchgang (<see cref="Gassennetz.PfadZwischen"/>). So geht sie gleich in Zielrichtung, ohne erst zum Knoten
    /// hinter ihr und danach denselben Weg zurück. Sie geht sofort weiter, ohne Wartezeit, und holt auf (<see cref="Gehtempo.Weitergehen"/>).
    /// </summary>
    private void Weitergehen(Passant p, Weg weg, Vector3[] altPfad, float strecke, double minuteGenau)
    {
        var altXZ = altPfad.Select(v => (v.X, v.Z)).ToArray();
        // wo die Figur gezeigt wurde (auf der gerundeten Mittellinie, ohne ihren Versatz – der geht über VersatzStart weich weiter), mit der Höhe des Pfades
        var altLinie = new Passantenspur.Polylinie(altXZ);
        var stelle = p.Knoten.Position - p.VersatzJetzt;
        stelle.Y = Gassennetz.PunktAuf(altPfad, altLinie.Laenge > 0 ? strecke / altLinie.Laenge : 0).Y;
        var neu = p.Pfad;
        List<Vector3> pfad;
        if (SpurV1) pfad = WeitergehenV1(stelle, neu);
        else
        {
            pfad = new List<Vector3> { stelle };
            var neuXZ = neu.Select(v => (v.X, v.Z)).ToArray();
            var einstiege = Wegeinstieg.Einstiege(altXZ, strecke, i => _netz.KnotenGenau(altPfad[i]));
            if (einstiege.Count == 0 && _netz.KanteBei(stelle) is { } kante) // der alte Pfad hatte keinen Knoten (ein Stück in derselben Gasse): die Kante, auf der sie ist
                einstiege = new() { new(kante.A, stelle.DistanceTo(_netz.Knoten[kante.A]), Array.Empty<int>()), new(kante.B, stelle.DistanceTo(_netz.Knoten[kante.B]), Array.Empty<int>()) };
            var ausstiege = Wegeinstieg.Ausstiege(neuXZ, i => _netz.KnotenGenau(neu[i]));
            if (_netz.PfadZwischen(einstiege.Select(x => (x.Knoten, x.Laenge)).ToList(), ausstiege.Select(x => (x.Knoten, x.Laenge)).ToList()) is { } verbindung)
            {
                pfad.AddRange(einstiege[verbindung.Anfang].Folge.Select(i => altPfad[i]));
                pfad.AddRange(verbindung.Pfad);
                pfad.AddRange(ausstiege[verbindung.Ende].Folge.Select(i => neu[i]));
            }
            else pfad = WeitergehenV1(stelle, neu); // keine Verbindung über die Einstiege: wie früher über den nächsten Knoten (nicht geradewegs durch Häuser)
            pfad = OhneUmkehr(pfad);
            // Markt v3: Was davon auf einem Marktplatz liegt, geht über dessen freie Fläche (nicht über Knoten am Brunnen oder durch Stände)
            if (!MarktV2) pfad = UeberDenPlatz(pfad, p.Nummer);
        }
        p.Pfad = pfad.ToArray();
        p.Laenge = Gassennetz.Laenge(p.Pfad);
        var sekundenJeMinute = _simulation!.Takt.RundenDauerSekunden / Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag;
        p.Verzoegerung = 0;
        (p.LaufStart, p.LaufEnde) = Gehtempo.Weitergehen(p.Laenge, p.Gehtempo * sekundenJeMinute, minuteGenau, weg.Ankunft);
    }

    /// <summary>Der Anschluss wie vor dem 1.11.2026 (<c>--passantenspur=v1</c>, zum Vergleichen): zum nächsten Knoten, über das Netz zum Anfang, dann der Weg.</summary>
    private List<Vector3> WeitergehenV1(Vector3 stelle, Vector3[] neu)
    {
        var pfad = new List<Vector3> { stelle };
        if (_netz.Knoten.Count > 0)
        {
            var (a, b) = (NaechsterKnoten(stelle), NaechsterKnoten(neu[0]));
            if (a != b)
                foreach (var punkt in _netz.Pfad(a, b))
                    if (pfad[^1].DistanceTo(punkt) > 0.05f) pfad.Add(punkt);
        }
        foreach (var punkt in neu)
            if (pfad[^1].DistanceTo(punkt) > 0.05f) pfad.Add(punkt);
        return pfad;
    }

    /// <summary>Der Knoten des Gassennetzes, der einem Punkt am nächsten liegt (ohne einen neuen anzulegen).</summary>
    private int NaechsterKnoten(Vector3 punkt)
    {
        var (beste, abstand) = (0, float.MaxValue);
        for (var i = 0; i < _netz.Knoten.Count; i++)
        {
            var d = _netz.Knoten[i].DistanceSquaredTo(new Vector3(punkt.X, _netz.Knoten[i].Y, punkt.Z));
            if (d < abstand) (beste, abstand) = (i, d);
        }
        return beste;
    }

    /// <summary>Der Pfad ohne Umkehr auf derselben Linie und ohne doppelte Punkte (<see cref="Wegeinstieg.OhneUmkehr"/>).</summary>
    private static List<Vector3> OhneUmkehr(List<Vector3> pfad) =>
        Wegeinstieg.OhneUmkehr(pfad.Select(v => (v.X, v.Z)).ToList()).Select(i => pfad[i]).ToList();
}
