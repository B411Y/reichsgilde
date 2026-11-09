using Godot;

namespace Reichsgilde;

/// <summary>
/// Prüfhilfe <c>--passantnah=1</c> (Teil der <c>partial class Stadtansicht</c>, NAS 09.11.): Die Kamera folgt einem Passanten in der nahen Zoomstufe
/// (25, Skelettstufe der Probe), damit man Füße, Hände und Kleidung aus der Nähe sieht; sonst landet die freie Kamera auf Zoom 69.
/// <c>--passantnah=&lt;zoom&gt;</c> (größer als 1) nimmt diesen Zoom. Gewählt wird nach drei Sekunden der sichtbare Passant, der dem Blickpunkt am
/// nächsten ist; verschwindet er (ins Haus), der nächste. Alle 5 s eine Zeile <c>[passantnah]</c>: Bewegung, Lage über dem Boden und die Höhe des
/// tiefsten Fußknochens über dem Knoten – so lassen sich alte und neue Figuren auch ohne Bild vergleichen (Gegenprobe Marktbelag).
/// </summary>
public partial class Stadtansicht
{
    private static readonly string? PassantNahOption = Wasserarten.Startoption("passantnah");
    private Passant? _nahPassant;
    private double _nahZeit, _nahMelde;

    /// <summary>Einmal je Bild nach den Passanten (nur mit <c>--passantnah</c>).</summary>
    private void PassantNahFolgen(double delta)
    {
        if (PassantNahOption is null or "0" or "aus") return;
        _nahZeit += delta;
        if (_nahZeit < 3) return;
        if (_nahPassant is null || !_nahPassant.Knoten.Visible || !IsInstanceValid(_nahPassant.Knoten) || !_passanten.ContainsValue(_nahPassant))
        {
            _nahPassant = null;
            var mitte = _kameraDrehung.Position;
            var bester = float.MaxValue;
            foreach (var p in _passanten.Values)
            {
                if (!p.Knoten.Visible || p.Figur is null) continue;
                var d = new Vector2(p.Knoten.Position.X - mitte.X, p.Knoten.Position.Z - mitte.Z).LengthSquared();
                if (d < bester) (bester, _nahPassant) = (d, p);
            }
            if (_nahPassant is null) return;
            GD.Print($"[passantnah] folge Einwohner {_nahPassant.Nummer} bei ({_nahPassant.Knoten.Position.X:0.0}, {_nahPassant.Knoten.Position.Z:0.0})");
        }
        var lage = _nahPassant.Knoten.Position;
        var zoom = float.TryParse(PassantNahOption, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z) && z > 1 ? z : KameraMinZoom;
        KameraSetzen(lage.X, lage.Z, zoom);
        _nahMelde += delta;
        if (_nahMelde < 5) return;
        _nahMelde = 0;
        var f = _nahPassant.Figur!;
        GD.Print($"[passantnah] Einwohner {_nahPassant.Nummer}: {(f.Probe ? "Probe" : "alt")}, Bewegung {f.Bewegung} (Clip {(f.KiKlip.Length > 0 ? f.KiKlip : f.Bewegung)}, Takt {f.Takt:0.00}, " +
                 $"Phase {f.Phase:0.00}), Knoten y {lage.Y:0.000}, Boden {BodenHoehe(lage.X, lage.Z):0.000}, tiefster Fußknochen über dem Knoten {FussKnochenHoehe(f):0.000} m, " +
                 $"Netzunterkante in Ruhe {NetzUnterkante(f):0.000} m");
    }

    /// <summary>Höhe (Meter) des tiefsten Fuß- oder Zehenknochens über dem Ursprung des Modells, in der aktuellen Pose.</summary>
    private static float FussKnochenHoehe(Figur f)
    {
        var tiefst = float.MaxValue;
        foreach (var s in f.Modell.FindChildren("*", nameof(Skeleton3D), true, false))
        {
            if (s is not Skeleton3D skelett) continue;
            for (var i = 0; i < skelett.GetBoneCount(); i++)
            {
                var name = skelett.GetBoneName(i).ToLowerInvariant();
                if (!name.Contains("foot") && !name.Contains("toe") && !name.Contains("fuss") && !name.Contains("fuß") && !name.Contains("zeh")) continue;
                var y = (skelett.GlobalTransform * skelett.GetBoneGlobalPose(i)).Origin.Y - f.Modell.GlobalPosition.Y;
                if (y < tiefst) tiefst = y;
            }
        }
        return tiefst == float.MaxValue ? float.NaN : tiefst;
    }

    /// <summary>Unterkante (Meter) der Netze der Figur in der Ruhelage über dem Ursprung des Modells (Kasten der Netze, mit Maßstab).</summary>
    private static float NetzUnterkante(Figur f)
    {
        var tiefst = float.MaxValue;
        foreach (var teil in f.Teile)
        {
            if (!IsInstanceValid(teil) || !teil.Visible) continue;
            var kasten = teil.GlobalTransform * teil.GetAabb();
            tiefst = Mathf.Min(tiefst, kasten.Position.Y - f.Modell.GlobalPosition.Y);
        }
        return tiefst == float.MaxValue ? float.NaN : tiefst;
    }
}
