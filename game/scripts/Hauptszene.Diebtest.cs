using Godot;
using Reichsgilde.Core.Entwickler;

namespace Reichsgilde;

/// <summary>
/// Startoption <c>--diebtest=1|festnahme|klick|klickfremd|klickfest|bildfolge</c> (Teil der <c>partial class Hauptszene</c>; nur für Bilder und Tests, braucht <c>--entwickler=1</c>): Das eigene Haus
/// und das erste Haus der KI bekommen per Entwicklerbefehl <see cref="EntwicklerDiebeshaus"/> ein Versteck voller Diebe – so sind in Köln, wo die KI kaum
/// Diebe einsetzt, Taschendiebe am Markt zu sehen (Stadtansicht.Taschendiebe.cs). <c>festnahme</c>: zusätzlich zeigt die Stadt, sobald Passanten am Markt
/// sind, eine Festnahme (nur Bild, der Kern merkt nichts). <c>klick</c>, <c>klickfremd</c>, <c>klickfest</c>: dazu wird die erste sichtbare eigene, fremde bzw. gefasste Gestalt angeklickt (Auswahltafel, für Bilder). Dazu <c>--startstunde=9</c> (Taschendiebe arbeiten 8 bis 20 Uhr).
/// </summary>
public partial class Hauptszene
{
    private void DiebtestStarten(Dictionary<string, string> optionen)
    {
        if (!optionen.TryGetValue("diebtest", out var wahl) || wahl is "0" or "" || _spieler is null || _simulation.Wirtschaft is not { } w) return;
        _simulation.Bote.Einreihen(_spieler, new EntwicklerDiebeshaus(), e => { if (!e.Erfolg) Protokollieren("--diebtest: " + e.Meldung + " (mit --entwickler=1?)"); });
        if (w.Konten.FirstOrDefault(k => k != _spieler && k.IstAktiv) is { } rivale)
            _simulation.Bote.Einreihen(_spieler, new EntwicklerDiebeshaus(rivale.Inhaber), e => { if (!e.Erfolg) Protokollieren("--diebtest: " + e.Meldung); });
        _stadt.Bildfolge = wahl == "bildfolge";
        _stadt.FestnahmeProbe = wahl is "festnahme" or "klickfest";
        if (wahl is "klick" or "klickfremd" or "klickfest") _stadt.KlickProbe = wahl;
    }
}
