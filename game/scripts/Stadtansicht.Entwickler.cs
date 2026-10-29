using Godot;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die Hilfen des Entwicklerfensters (<see cref="EntwicklerAnzeige"/>) in der Stadtansicht – ein Teil der <c>partial class Stadtansicht</c>:
/// Wetter und Jahreszeit von Hand vorgeben, einen Blitz auslösen, Gassennetz und Bauzonen als Hilfsansicht zeigen, die Lichter im Bild zählen.
/// Alles ist reine Darstellung, nichts davon wird gespeichert und die Simulation merkt nichts. Wetter und Jahreszeit nutzen denselben Weg wie die
/// Startoptionen <c>--wetter</c> und <c>--jahreszeit</c> (Stadtansicht.Wetter.cs).
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Die vorgegebene Wetterlage (null: das Wetter läuft von selbst).</summary>
    public Wetterlage? WetterErzwungen => _wetterVorgabe;

    /// <summary>Die vorgegebene Jahreszeit der Ansicht (null: die der Uhr).</summary>
    public Jahreszeit? JahreszeitErzwungen => _jahreszeitVorgabe;

    /// <summary>Gibt eine Wetterlage vor (null: wieder automatisch). Die Werte stehen sofort, ohne Überblenden.</summary>
    public void WetterErzwingen(Wetterlage? lage)
    {
        _wetterVorgabe = lage;
        _wetterSofort = true;
    }

    /// <summary>Gibt die Jahreszeit der Ansicht vor (null: wieder die der Uhr); Wiese, Laub, Äcker und Schnee folgen im nächsten Bild.</summary>
    public void JahreszeitErzwingen(Jahreszeit? jahreszeit) => _jahreszeitVorgabe = jahreszeit;

    /// <summary>Löst einen Blitz aus, auch bei schönem Wetter (Licht gibt es nur in den Wettereffekten Mittel und Hoch und nicht in den Innenräumen).</summary>
    public void BlitzAusloesen()
    {
        _blitzZeit = 0;
        _blitzPulse = 2 + _blitzZufall.Next(3);
        _blitzAbstand = 0.3f / _blitzPulse * (0.8f + 0.4f * (float)_blitzZufall.NextDouble());
        BlitzRichtung();
    }

    private Node3D? _wegenetzKnoten;

    /// <summary>Das Gassennetz der Passanten zeigen (pink, Knoten gelb) – wie <c>--wegenetz=1</c>; beim Einschalten neu gezeichnet.</summary>
    public bool WegenetzSichtbar
    {
        get => _wegenetzKnoten is { Visible: true };
        set
        {
            if (_wegenetzKnoten is not null)
            {
                RemoveChild(_wegenetzKnoten);
                _wegenetzKnoten.QueueFree();
                _wegenetzKnoten = null;
            }
            if (!value) return;
            _wegenetzKnoten = new Node3D { Name = "Wegenetz" };
            AddChild(_wegenetzKnoten);
            NetzZeichnen(_netz, _wegenetzKnoten);
        }
    }

    /// <summary>Die Bauzonen der Karte zeigen, auch ohne Baumodus (der Baumodus selbst bleibt unberührt).</summary>
    public bool BauzonenHilfe
    {
        get => _bauzonenHilfe;
        set
        {
            _bauzonenHilfe = value;
            if (value && _bauzonen is null) BauzonenBauen();
            if (_bauzonen is not null) _bauzonen.Visible = value || _baumodus;
        }
    }

    private bool _bauzonenHilfe;

    /// <summary>Wie viele Lichter gerade im Bild sind: sichtbare Sonne, Lampen und Strahler der Stadt (zählt den ganzen Baum, darum nicht jedes Bild).</summary>
    public int LichterImBild()
    {
        var zahl = 0;
        foreach (var knoten in FindChildren("*", "Light3D", true, false))
            if (knoten is Light3D { Visible: true } licht && licht.IsVisibleInTree() && licht.LightEnergy > 0.001f) zahl++;
        return zahl;
    }
}
