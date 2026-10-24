using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Felder und Kartenrand in der Kartenwerkstatt – ein Teil der <c>partial class Stadtansicht</c> (Überblick in Stadtansicht.Kartenwerkstatt.cs,
/// Boden, Dunst und Äcker selbst in Stadtansicht.Rand.cs).
///
/// <b>Felder, live.</b> Wie die Natur der Flächen: Wo welcher Acker liegt, rechnet der Kern wie im Spiel (<see cref="Kartenentwurf.FelderVon"/> mit
/// einem <see cref="Naturfreiraum"/>); hier wird nur gezeichnet, je Feldfläche ein Knoten mit einem Netz je Frucht. Neu gerechnet wird, was sich
/// geändert hat: die Fläche selbst und jede Feldfläche in der Nähe eines geänderten Gebäudes, Weges oder einer Bauzone (die Vormerkung teilt
/// sich mit der Natur, <see cref="NaturNachziehen"/>).
///
/// <b>Kopf der Karte.</b> Ändert sich die Größe, die Randbreite oder der Kamera-Start (Teil <see cref="EntwurfKopf"/>), baut <see cref="RandNeuBauen"/>
/// Boden, Dunst und die Linien des Kartenrands neu.
/// </summary>
public partial class Stadtansicht
{
    private Node3D? _wsFelderKnoten;
    private readonly Dictionary<EntwurfFeld, Node3D> _wsFelder = new();
    private readonly Dictionary<EntwurfFeld, int> _wsFeldAnzahl = new();
    private readonly HashSet<EntwurfFeld> _wsFelderNeu = new();
    private bool _wsRandNeu;

    /// <summary>Ein Acker ragt höchstens so weit über seine Feldfläche hinaus, dass eine Änderung ihn nicht betrifft (Meter, etwas mehr als die Stücklänge der Vorgabe).</summary>
    private const float FeldUeberstand = 8;

    /// <summary>Wie viele Acker-Stücke eine Feldfläche gerade hat (für die Tafel).</summary>
    public int WerkstattFeldAnzahl(EntwurfFeld f) => _wsFeldAnzahl.GetValueOrDefault(f);

    /// <summary>Die Äcker einer Feldfläche neu: die Stücke des Kerns, je Frucht ein Netz.</summary>
    private void FeldStreuen(EntwurfFeld f, Naturfreiraum frei)
    {
        FeldEntfernen(f);
        if (_entwurf is null) return;
        if (_wsFelderKnoten is null)
        {
            _wsFelderKnoten = new Node3D { Name = "Felder" };
            AddChild(_wsFelderKnoten);
        }
        var parzellen = _entwurf.FelderVon(f, frei);
        var knoten = new Node3D { Name = "Acker_" + f.Bezeichnung };
        _wsFelderKnoten.AddChild(knoten);
        _wsFelder[f] = knoten;
        _wsFeldAnzahl[f] = parzellen.Count;
        FeldNetzeBauen(parzellen, knoten);
    }

    private void FeldEntfernen(EntwurfFeld f)
    {
        if (_wsFelder.Remove(f, out var alt))
        {
            GrasAeckerWeg(alt); // dort wächst wieder Gras (Stadtansicht.Gras.cs)
            alt.QueueFree();
        }
        _wsFeldAnzahl.Remove(f);
    }

    /// <summary>
    /// Die Richtung der Streifen einer neuen Feldfläche: parallel zu ihrer längsten Kante (Grad wie <c>Rotation.Y</c>, 0 bis unter 180) – so
    /// laufen die Furchen längs des Feldes, wie ein Bauer sie pflügt.
    /// </summary>
    internal static double FeldRichtungVon(IReadOnlyList<Kartenort> ecken)
    {
        var (laengste, richtung) = (0.0, 0.0);
        for (var i = 0; i < ecken.Count; i++)
        {
            var (a, b) = (ecken[i], ecken[(i + 1) % ecken.Count]);
            var laenge = a.Abstand(b);
            if (laenge <= laengste) continue;
            laengste = laenge;
            richtung = Math.Atan2(b.X - a.X, b.Z - a.Z) * 180 / Math.PI; // die lokale z-Achse zeigt bei Drehung t nach (sin t, cos t)
        }
        return Math.Round(((richtung % 180) + 180) % 180);
    }

    /// <summary>Boden, Dunst und Kartenrand nach einer Änderung am Kopf der Karte neu (aus <see cref="Nachziehen"/>).</summary>
    private void RandNeuBauen()
    {
        _wsRandNeu = false;
        if (_entwurf is null) return;
        BodenBauen(_entwurf.Kartengrenzen);
    }

    /// <summary>
    /// Der aktuelle Blick als Kamera-Start der Karte (Knopf „Kamera-Start übernehmen“): Punkt (ins Spielfeld geholt, <paramref name="verschoben"/>
    /// sagt es), Zoom (sichtbare Bildhöhe) und Drehung (0 = die gewohnte Blickrichtung nach Nordosten).
    /// </summary>
    public (Kartenort Punkt, double Zoom, double Drehung) WerkstattKameraStart(out bool verschoben)
    {
        var p = _kameraDrehung.Position;
        var punkt = new Kartenort(p.X, p.Z);
        verschoben = false;
        if (_grenzen is { } g && !g.ImSpielfeld(punkt.X, punkt.Z))
        {
            punkt = g.BlickpunktKlemmen(punkt.X, punkt.Z);
            verschoben = true;
        }
        return (punkt, _kamera.Size, Mathf.PosMod(_zielDrehung + 45f, 360f));
    }
}
