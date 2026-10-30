using Reichsgilde.Core;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Hängt die <b>Ansagen des Haushofmeisters</b> (<see cref="KlangAnsagen"/>) an die Ereignisse der Partie – ein Teil der <c>partial class Hauptszene</c>.
/// Textmeldungen des Kerns erkennt <see cref="KlangAnsagenErkennung"/>; Aufstieg im Stand und Jahresende sind eigene Ereignisse, Ratssitzung und
/// Wahl kommen aus <c>Hauptszene.Ratssitzung.cs</c>. Immer nur für das eigene Haus (<paramref name="spieler"/>), nicht in einer Probepartie.
/// </summary>
public partial class Hauptszene
{
    private void AnsagenVerbinden(Simulation simulation, Stadtwirtschaft w, Konto spieler)
    {
        KlangAnsagen.Entwicklermodus = () => simulation.EntwicklerErlaubt;
        w.Meldung += meldung =>
        {
            if (!_probepartie) KlangAnsagen.Meldung(meldung, w, spieler);
        };
        w.Aufgestiegen += (konto, _) =>
        {
            if (konto == spieler && !_probepartie) KlangAnsagen.Ereignis(KlangAnsagenErkennung.Id.StandAufstieg, w, spieler);
        };
        simulation.JahrBeendet += _ =>
        {
            if (!_probepartie) KlangAnsagen.Ereignis(KlangAnsagenErkennung.Id.Jahresende, w, spieler);
        };
    }

    /// <summary>Die Ratssitzung beginnt auf dem Bildschirm: Ansage, wenn sie das eigene Haus betrifft (Sitz, Antrag oder Kandidatur).</summary>
    private void SitzungAnsagen(Ratssitzung sitzung)
    {
        if (_spieler is not null && _simulation.Wirtschaft is { } w && SitzungBetrifft(sitzung, _spieler))
            KlangAnsagen.Ereignis(KlangAnsagenErkennung.Id.Ratssitzung, w, _spieler);
    }

    /// <summary>Ein Punkt der Sitzung ist abgehalten: bei einer Wahl mit dem eigenen Haus Ansage von Sieg oder Niederlage (nicht in der Wiederholung).</summary>
    private void SitzungErgebnisAnsagen(Sitzungspunkt punkt)
    {
        if (_spieler is not null && _simulation.Ratssitzung is not null && _simulation.Wirtschaft is { } w)
            KlangAnsagen.Wahlergebnis(punkt, w, _spieler);
    }
}
