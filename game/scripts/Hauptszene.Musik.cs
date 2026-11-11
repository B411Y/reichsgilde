using Reichsgilde.Core;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Hängt die <b>dynamische Musik</b> (<see cref="Musikspieler"/>, <see cref="MusikModell"/>) an die Partie – ein Teil der <c>partial class Hauptszene</c>.
/// Sie liest nur: Uhr, Jahreszeit und Volk des Oberhaupts, die Meldungen des Kerns (über <see cref="KlangAnsagenErkennung"/>, also nur das eigene Haus)
/// und die Zustände Unfreie bei der Arbeit (Stadtansicht.Musik.cs) und Not in der Stadt, dazu seit Runde 2 die Berufe und den Glauben des Hauses, den Ort der Kamera
/// (eigene Werkstatt, Kirchenraum) und die Messe. Nicht in der Probepartie der Kartenwerkstatt.
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
        w.Gauner.Vorfaelle.Neu += v =>
        {
            var anlass = MusikModell.AnlassAusVorfall(v, spieler);
            if (anlass != MusikModell.Anlass.Keiner) Musikspieler.Melden(anlass);
        };
        w.Aufgestiegen += (konto, _) =>
        {
            if (konto == spieler) Musikspieler.Melden(MusikModell.Anlass.Aufstieg);
        };
        Musikspieler.PartieAnschliessen(() => _umgebungBereit ? MusikEingabe(w, spieler) : null);
        TreeExiting += () => Musikspieler.PartieAnschliessen(null);
    }

    /// <summary>Alles, was die Musik je Bild von der Partie liest (nur lesend): Volk, Berufe und Glaube des Hauses, Ort der Kamera, Messe.</summary>
    private Musikspieler.PartieEingabe MusikEingabe(Stadtwirtschaft w, Konto spieler)
    {
        var oberhaupt = w.Familien.Von(spieler)?.Oberhaupt;
        var uhr = _stadt.KlangUhrMinute;
        var glaube = MusikModell.GlaubeName(oberhaupt?.Glaube);
        var (ort, kirche) = _stadt.KlangMusikOrt(w, spieler);
        return new Musikspieler.PartieEingabe(uhr, _stadt.KlangJahreszeit, oberhaupt?.Volk, MusikZustand(w), MusikModell.GruppeVon(spieler.Berufe), glaube, ort, kirche,
            w.Kirchenwesen.IstAktiv && MusikModell.IstMesse(uhr, w.Kirchenwesen.Daten.MesseUm));
    }

    /// <summary>Eine gewonnene Wahl des eigenen Hauses (Amt gewonnen) meldet der Ratssitzung (<c>SitzungErgebnisAnsagen</c>) auch der Musik.</summary>
    private void MusikWahlErgebnis(Sitzungspunkt punkt, Konto spieler)
    {
        if (_probepartie || punkt.Thema != Sitzungsthema.Wahl || punkt.Ergebnis?.Gewaehlt is not { } gewaehlt) return;
        if (punkt.Kandidaten.Any(k => k.Haus == spieler) && gewaehlt == spieler.Inhaber) Musikspieler.Melden(MusikModell.Anlass.Aufstieg);
    }

    /// <summary>
    /// <b>Auslöser des Aufstands</b> (umhängbar): Es gibt noch keinen Aufstand im Kern. Bis dahin steht „große Not in der Stadt“ dafür: ein Pestjahr
    /// (<c>Gesundheitswesen.IstPestjahr</c>). Später hier den Aufstand abfragen.
    /// </summary>
    private static bool MusikAufstand(Stadtwirtschaft w) => w.Gesundheit.IstPestjahr;

    private MusikModell.Anlass MusikZustand(Stadtwirtschaft w) =>
        MusikAufstand(w) ? MusikModell.Anlass.Aufstand : _stadt.KlangUnfreieNah() ? MusikModell.Anlass.Sklaven : MusikModell.Anlass.Keiner;
}
