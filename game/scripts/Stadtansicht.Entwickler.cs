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

    /// <summary>Löst einen Blitz aus (mit dem Abstand in Kilometern oder gewürfelt), auch bei schönem Wetter (Licht gibt es nur in den Wettereffekten Mittel und Hoch und nicht in den Innenräumen).</summary>
    public void BlitzAusloesen(float? km = null)
    {
        _blitzZeit = 0;
        _einschlag = null;
        if (km is { } k) (_blitzAbstandKm, _blitzStaerke) = (k, KlangUmgebungModell.BlitzHelligkeit(k)); // Abstand vorgegeben (Hörprobe, Entwicklerbefehl)
        else BlitzAbstandWuerfeln(KlangGewitterFortschritt);
        _blitzPulse = 2 + _blitzZufall.Next(3);
        _blitzAbstand = 0.3f / _blitzPulse * (0.8f + 0.4f * (float)_blitzZufall.NextDouble());
        BlitzRichtung();
    }

    /// <summary>
    /// <b>Direkter Blitzeinschlag</b> an einem Ort der Stadt (Andockstelle für die spätere Spielmechanik „Einschlag setzt ein Haus in Brand“, die nicht hier liegt): Der
    /// Blitz ist am hellsten (Stärke 1, mehr Pulse), kommt aus der Richtung des Orts, und der Ton spielt die brachiale Einschlag-Aufnahme fast ohne Verzögerung
    /// (Abstand aus dem Ort zum Blickpunkt, ÷ 343 m/s). Normale Blitze schlagen nie näher als 1 km ein und nie so laut. Der Ort in Metern der Stadt (y wird ignoriert).
    /// </summary>
    public void BlitzEinschlag(Vector3 ort)
    {
        var fokus = KlangFokus;
        float dx = ort.X - fokus.X, dz = ort.Z - fokus.Z;
        var breite = Math.Max(GetViewport().GetVisibleRect().Size.X, 1f);
        var seite = _raum is not null ? 0f : StadtKamera.UnprojectPosition(ort).X / breite * 2f - 1f;
        _blitzZeit = 0;
        _blitzPulse = 4;
        _blitzAbstand = 0.3f / _blitzPulse;
        _blitzAbstandKm = MathF.Sqrt(dx * dx + dz * dz) / 1000f;
        _blitzStaerke = 1f;
        _einschlag = (_blitzAbstandKm, Mathf.Clamp(seite, -1f, 1f));
        // das Licht kommt von der Seite des Einschlags: Himmelsrichtung vom Blickpunkt zum Ort, steil von oben
        _blitzLicht.RotationDegrees = new Vector3(-62f, Mathf.RadToDeg(Mathf.Atan2(dx, dz)) + 180f, 0);
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
