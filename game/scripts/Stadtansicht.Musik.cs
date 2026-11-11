using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Was die dynamische Musik (<see cref="Musikspieler"/>) von der Stadtansicht liest – ein Teil der <c>partial class Stadtansicht</c>, nur lesend
/// (die Darstellung bleibt unberührt; Datei kam mit der Musik, Bereich Sound).
/// </summary>
public partial class Stadtansicht
{
    /// <summary>
    /// Wo die Kamera für die Musik ist: im Kirchenraum (Innenansicht einer Kirche des Kerns oder des Doms; dann der Glaube der Kirche: ihr Haus, ersatzweise der
    /// Stadtglaube, sonst null = der des Spielers), in einer eigenen Werkstatt (Innenansicht eines Stadtbetriebs des Spielers) oder draußen. Nur lesend.
    /// </summary>
    public (MusikModell.Ort Ort, string? KirchenGlaube) KlangMusikOrt(Stadtwirtschaft w, Konto spieler)
    {
        if (_innen is null) return (MusikModell.Ort.Draussen, null);
        if (_innen is BetriebsAnsicht { Betrieb: var b })
        {
            if (b.Art is Betriebsart.Kirche or Betriebsart.Dom or Betriebsart.Kathedrale)
                return (MusikModell.Ort.Kirche, MusikModell.GlaubeName(w.Glaube.KonfessionVon(b) ?? w.Glaube.Stadtglaube));
            return b.Konto == spieler && b.InDerStadt ? (MusikModell.Ort.EigeneWerkstatt, null) : (MusikModell.Ort.Draussen, null);
        }
        return _innen.Kennung == "Dom" ? (MusikModell.Ort.Kirche, MusikModell.GlaubeName(w.Glaube.Stadtglaube)) : (MusikModell.Ort.Draussen, null);
    }

    /// <summary>Wer gilt als Arbeitskraft ohne Freiheit: Leibeigene sowie Orks und Oger (Volk des Arbeiters).</summary>
    public static bool IstUnfrei(Arbeiter a) => a.Arbeitskraft == "Leibeigener" || a.Volk is Volk.Ork or Volk.Oger;

    /// <summary>
    /// Arbeiten gerade Unfreie, Orks oder Oger (Arbeitszeit, kein Umbau, kein Stillstand), und ist die Kamera bei ihnen? „Bei ihnen“: der Blickpunkt am Boden
    /// liegt höchstens 12 + 0,3 · Zoom Meter von ihrem Betrieb (Zoom 40: 24 m), oder die Innenansicht zeigt genau diesen Betrieb.
    /// </summary>
    public bool KlangUnfreieNah()
    {
        if (_simulation?.Wirtschaft is not { } w) return false;
        var uhr = _simulation.Zeit.UhrzeitInMinuten;
        var (fx, fz) = KlangFokus;
        var reichweite = 12f + 0.3f * KlangZoom;
        foreach (var (betrieb, ansicht) in _betriebe)
        {
            if (!betrieb.InDerStadt || betrieb.UmbauMinuten != 0 || betrieb.Stillstand is not null || !betrieb.IstArbeitszeit(uhr)) continue;
            if (!betrieb.Arbeiter.Any(IstUnfrei)) continue;
            if (_raum is not null && ReferenceEquals(_innen, ansicht)) return true;
            if (_raum is null && Mathf.Sqrt((ansicht.Mitte.X - fx) * (ansicht.Mitte.X - fx) + (ansicht.Mitte.Z - fz) * (ansicht.Mitte.Z - fz)) <= reichweite) return true;
        }
        return false;
    }
}
