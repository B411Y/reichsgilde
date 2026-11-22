using Godot;

namespace Reichsgilde;

/// <summary>
/// <b>Abstand der Passanten zu Ständen, Bänken, Brunnen, Trog und Kreuz auf dem Marktpflaster</b> – ein Teil der <c>partial class Stadtansicht</c> (Nutzer, 22.11.2026).
/// Die Marktfläche (<see cref="Marktflaeche"/>) kennt die Umrisse genau und hält Wege und die seitliche Lage frei; Anfang und Ende eines Weges, Stehplätze (Gruppe,
/// Brunnen, Schauen) und rohe Linien über den Platz (wenn im Bild keine Zeit mehr für die Wegsuche war) lagen aber noch in den Hindernissen. Jede Figur auf dem
/// Pflaster wird darum nach der Bewegung aus den Hindernissen geschoben, bis ihr Körper (Radius plus 2 cm) frei ist. Ausnahmen mit gewollter Berührung: wer auf
/// der Bank sitzt oder sich am Brunnenrand anlehnt (und noch auf dem Weg dorthin ist). Nur Darstellung; <c>--kollisionsfix=0</c> schaltet es ab.
/// </summary>
public partial class Stadtansicht
{
    private const float MarktKoerperLuft = 0.02f;
    /// <summary>Startoption <c>--marktabstand=0</c> schaltet nur diese Prüfung ab (für Vorher-Nachher-Messungen).</summary>
    private static readonly bool MarktAbstandAn = Wasserarten.Startoption("marktabstand") != "0";
    private long _marktAbstandTicks, _marktAbstandAufrufe, _marktAbstandGeschoben;

    /// <summary>Der Ort der Figur, aus den Hindernissen eines Marktpflasters geschoben (unverändert, wenn sie frei steht oder nicht auf einem Markt ist).</summary>
    private Vector3 MarktAbstandHalten(Passant p, Vector3 ort, int stelle = int.MinValue)
    {
        if (stelle == int.MinValue) stelle = p.Marktstelle >= 0 ? p.Marktstelle : p.AlteMarktstelle;
        if (!KollisionsFix || !MarktAbstandAn || MarktV2 || _maerkte.Count == 0) return ort;
        var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        try { return MarktAbstandRechnen(p, ort, stelle); }
        finally { _marktAbstandTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0; _marktAbstandAufrufe++; }
    }

    private Vector3 MarktAbstandRechnen(Passant p, Vector3 ort, int stelle)
    {
        if (stelle >= 0 && stelle < _marktstellen.Count)
        {
            var s = _marktstellen[stelle];
            if ((s.Bewegung == "Sitzen" || s.Art is Marktbummel.Art.Bank or Marktbummel.Art.Rand) && (p.Stand is not null || p.AmRand < 1.5f)) return ort;
        }
        foreach (var platz in _maerkte)
        {
            if (platz.Flaeche is not { } f || !f.Auf(ort.X, ort.Z, -1f)) continue;
            if (MarktAufstehen(p, f)) return ort;
            var frei = p.Radius + MarktKoerperLuft;
            if (f.Abstand(ort.X, ort.Z) >= frei) return ort;
            _marktAbstandGeschoben++;
            var (x, z) = f.HerausSchieben(ort.X, ort.Z, frei);
            return new Vector3(x, ort.Y, z);
        }
        return ort;
    }

    /// <summary>Steht die Figur gerade von der Bank (Sitzplatz) auf, auf den ersten 0,6 m ihres Weges? Der Sitzplatz liegt im Rechteck der Bank (höchstens 0,6 m tief), die Mitte eines Brunnens nicht.</summary>
    private static bool MarktAufstehen(Passant p, Marktflaeche f) =>
        p.Stand is null && p.AmRand < 0.6f && p.Pfad.Length > 0 && f.Abstand(p.Pfad[0].X, p.Pfad[0].Z) is var a && a < 0 && a > -0.6f && p.Pfad[0].DistanceTo(p.Knoten.Position) < 1.2f;
}
