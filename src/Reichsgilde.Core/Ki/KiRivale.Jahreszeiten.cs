using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Ki;

/// <summary>
/// Die KI-Häuser und die Jahreszeit (Datenversion 67): kein Schiffshandel bei Eis und Vorrat vor dem Winter. Beides liest nur, was auch der Spieler
/// weiß – die Jahreszeit, den angekündigten strengen Winter (die Meldung zum Jahresbeginn nennt die Stunde, zu der der Fluss zufriert), die Daten.
/// </summary>
public sealed partial class KiRivale
{
    /// <summary>
    /// Läuft kein Schiff aus? Solange der Fluss zu ist, nimmt der Hafen ohnehin nichts an; im strengen Winter lässt die KI die Schiffe schon vorher am
    /// Kai, sonst säßen sie im Eis fest. Zur ersten Stunde des Jahres sagt sie es einmal.
    /// </summary>
    private bool SchiffeBleibenImHafen()
    {
        var j = _w.Jahreszeiten;
        if (!j.FlussZugefroren && !j.EisDroht) return false;
        if (_stundenImJahr == 1 && _w.Schiffahrt.SchiffeVon(Konto).Any(s => s.Zustand == SchiffZustand.AmKai))
            Entscheiden($"lässt die Schiffe am Kai – ein strenger Winter, der {j.Flussname} friert zu.");
        return true;
    }

    /// <summary>
    /// Bis zu welchem Bestand der Verwalter eines KI-Betriebs diese Zutat jetzt auffüllt, wenn er Vorrat für den Winter anlegt – null, wenn nicht:
    /// Im Herbst ab <see cref="KiJahreszeitenDaten.VorratAbStunde"/> bis zur Lagergrenze für jede Zutat, die im Winter mehr gefragt ist oder jetzt mit
    /// der Ernte billiger kommt. Eine reine Rechnung aus Uhr und Daten: nichts zu speichern, nach dem Laden gleich.
    /// </summary>
    internal int? WinterVorrat(Betrieb betrieb, string zutat)
    {
        var j = _w.Jahreszeiten;
        if (j.Daten is not { } d || _w.Daten.Ki?.Jahreszeiten is not { } k || !Konto.IstAktiv) return null;
        if (j.Jahreszeit != Jahreszeit.Herbst || _w.Uhr.UhrzeitInMinuten < k.VorratAbStunde * 60) return null;
        if (d.Winter.NachfrageVon(zutat) <= 1 && d.Herbst.AngebotVon(zutat) <= 1) return null;
        return betrieb.LagerKapazitaetJeWare;
    }
}
