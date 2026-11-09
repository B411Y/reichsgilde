using Godot;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Für die Pinselmessung (<c>--pinselmessung</c>, Kartenwerkstatt.Pinselmessung.cs) – ein Teil der <c>partial class Stadtansicht</c>: ein Strich des
/// gewählten Pinsels wie mit gedrückter Maus, aber mit Orten in Metern statt Bildpunkten (headless gibt es keine Maus). Der Kreis steht am Ort.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Wie das Drücken der linken Maustaste über <paramref name="boden"/>.</summary>
    public void MessStrichBeginnen(Vector2 boden)
    {
        if (_entwurf is null) return;
        _wsOrt = boden;
        switch (Kartenwerkzeug)
        {
            case Werkstattwerkzeug.Gelaende:
                Befehl(() => _entwurf.GelaendeStrichBeginnen(GelaendePinsel));
                _wsGelaendeStrich = true;
                GelaendeStrichPunkt(boden);
                break;
            case Werkstattwerkzeug.Boden:
                Befehl(() => _entwurf.BodenStrichBeginnen(BodenPinsel.Begrenzt()));
                _wsBodenStrich = true;
                BodenStrichPunkt(boden);
                break;
            case Werkstattwerkzeug.Fels when FelsModus != FelsArt.Stempeln:
                Befehl(() => _entwurf.FelsStrichBeginnen(FelsPinsel));
                _wsFelsStrich = true;
                FelsStrichPunkt(boden);
                break;
            case Werkstattwerkzeug.Pinsel or Werkstattwerkzeug.Radierer:
                _wsStrich = new List<Vector2> { boden };
                _wsRadiert.Clear();
                if (Kartenwerkzeug == Werkstattwerkzeug.Radierer) Radieren(boden);
                break;
        }
    }

    /// <summary>Wie eine Mausbewegung mit gedrückter Taste nach <paramref name="boden"/>.</summary>
    public void MessStrichWeiter(Vector2 boden)
    {
        if (_entwurf is null) return;
        _wsOrt = boden;
        if (_wsGelaendeStrich) GelaendeStrichPunkt(boden);
        if (_wsBodenStrich) BodenStrichPunkt(boden);
        if (_wsFelsStrich && _entwurf.FelsStrichLaeuft) FelsStrichPunkt(boden);
        if (_wsStrich is { } strich)
        {
            if (Kartenwerkzeug == Werkstattwerkzeug.Radierer) Radieren(boden);
            if (boden.DistanceTo(strich[^1]) >= Mathf.Max(0.5f, (float)PinselRadius / 3)) strich.Add(boden);
        }
    }

    /// <summary>Wie das Loslassen der linken Maustaste.</summary>
    public void MessStrichEnde()
    {
        StrichBeenden();
        GelaendeStrichBeenden();
        BodenStrichBeenden();
        FelsStrichBeenden();
    }
}
