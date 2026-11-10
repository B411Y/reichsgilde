using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Hängt die <b>dynamische Musik</b> (<see cref="Musikspieler"/>, <see cref="MusikModell"/>) an die Partie – ein Teil der <c>partial class Hauptszene</c>.
/// Sie liest nur: Uhr, Jahreszeit und Volk des Oberhaupts, die Meldungen des Kerns (über <see cref="KlangAnsagenErkennung"/>, also nur das eigene Haus)
/// und die Zustände Unfreie bei der Arbeit (Stadtansicht.Musik.cs) und Not in der Stadt. Nicht in der Probepartie der Kartenwerkstatt.
/// </summary>
public partial class Hauptszene
{
    private void MusikVerbinden(Simulation simulation, Stadtwirtschaft w, Konto spieler)
    {
        if (_probepartie) return;
        w.Gemeldet += meldung =>
        {
            var anlass = MusikModell.AnlassAusAnsage(KlangAnsagenErkennung.Erkennen(meldung, w, spieler));
            if (anlass != MusikModell.Anlass.Keiner) Musikspieler.Melden(anlass);
        };
        Musikspieler.PartieAnschliessen(() => _umgebungBereit
            ? new Musikspieler.PartieEingabe(_stadt.KlangUhrMinute, _stadt.KlangJahreszeit, w.Familien.Von(spieler)?.Oberhaupt.Volk, MusikZustand(w))
            : null);
        TreeExiting += () => Musikspieler.PartieAnschliessen(null);
    }

    /// <summary>
    /// <b>Auslöser des Aufstands</b> (umhängbar): Es gibt noch keinen Aufstand im Kern. Bis dahin steht „große Not in der Stadt“ dafür: ein Pestjahr
    /// (<c>Gesundheitswesen.IstPestjahr</c>). Später hier den Aufstand abfragen.
    /// </summary>
    private static bool MusikAufstand(Stadtwirtschaft w) => w.Gesundheit.IstPestjahr;

    private MusikModell.Anlass MusikZustand(Stadtwirtschaft w) =>
        MusikAufstand(w) ? MusikModell.Anlass.Aufstand : _stadt.KlangUnfreieNah() ? MusikModell.Anlass.Sklaven : MusikModell.Anlass.Keiner;
}
