using Godot;
using Reichsgilde.Core.Entwickler;

namespace Reichsgilde;

/// <summary>
/// Startoption <c>--diebtest=1|festnahme|klick|klickfremd|klickfest|bildfolge</c> (Teil der <c>partial class Hauptszene</c>; nur für Bilder und Tests, braucht <c>--entwickler=1</c>): Das eigene Haus
/// und das erste Haus der KI bekommen per Entwicklerbefehl <see cref="EntwicklerDiebeshaus"/> ein Versteck voller Diebe – so sind in Köln, wo die KI kaum
/// Diebe einsetzt, Taschendiebe am Markt zu sehen (Stadtansicht.Taschendiebe.cs). <c>festnahme</c>: zusätzlich zeigt die Stadt, sobald Passanten am Markt
/// sind, eine Festnahme (nur Bild, der Kern merkt nichts). <c>klick</c>, <c>klickfremd</c>, <c>klickfest</c>: dazu wird die erste sichtbare eigene, fremde bzw. gefasste Gestalt angeklickt (Auswahltafel, für Bilder). Dazu <c>--startstunde=9</c> (Taschendiebe arbeiten 8 bis 20 Uhr).
/// Dazu Einbruch und Banden bei Nacht (<c>nacht|einbruch|bande|fremd|nachtfolge|bandefolge|klicknacht|klickbande|klickfremdnacht|echt|echtfolge</c>, Stadtansicht.Nachtdiebe.cs): siehe <c>docs/STARTOPTIONEN.md</c>.
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
        // Nacht (Stadtansicht.Nachtdiebe.cs): die Stadt trägt kurz nach 20 Uhr einen Vorfall ins Bild ein (nur Bild); „echt“ bestellt dagegen wirklich Einbruch und Bande beim Kern
        if (wahl is "nacht" or "einbruch" or "bande" or "fremd" or "nachtfolge" or "bandefolge" or "klicknacht" or "klickbande" or "klickfremdnacht")
            _stadt.NachtProbe = wahl switch { "nachtfolge" or "klicknacht" => "einbruch", "bandefolge" or "klickbande" => "bande", "klickfremdnacht" => "fremd", _ => wahl };
        _stadt.NachtFolge = wahl is "nachtfolge" or "bandefolge" or "echtfolge";
        if (wahl is "klicknacht" or "klickbande" or "klickfremdnacht") _stadt.NachtKlick = wahl == "klickfremdnacht" ? "klickfremd" : wahl;
        if (wahl is "echt" or "echtfolge") DiebtestEchteAuftraege(w);
    }

    /// <summary>
    /// <c>--diebtest=echt</c>: bestellt beim Kern wirklich einen Einbruch der eigenen Diebe und eine Bande gegen zwei Betriebe des ersten Hauses der KI (mit Ware im Lager und
    /// Gulden), damit die Vorfälle um 20 Uhr aus dem Kern kommen. Mit <c>--startstunde=19</c>; im Protokoll Zeilen <c>[nachtdieb]</c>.
    /// </summary>
    private void DiebtestEchteAuftraege(Reichsgilde.Core.Wirtschaft.Stadtwirtschaft w)
    {
        if (w.Konten.FirstOrDefault(k => k != _spieler && k.IstAktiv) is not { } rivale) return;
        _simulation.Bote.Einreihen(_spieler, new EntwicklerGulden(20000), _ => { });
        var ziele = w.Betriebe.Where(b => b.Konto == rivale && b.InDerStadt && b.OhneProduktion == false).Take(2).ToList();
        foreach (var b in ziele)
            foreach (var ware in b.Artdaten.Produkte.Take(1))
                _simulation.Bote.Einreihen(_spieler, new EntwicklerWare(b.Name, ware, 8), _ => { });
        if (ziele.Count > 0)
            _simulation.Bote.Einreihen(_spieler, new Reichsgilde.Core.Wirtschaft.Einbrechen(rivale.Inhaber, ziele[0].Name), e => Protokollieren("--diebtest=echt Einbrechen: " + e.Meldung));
        if (ziele.Count > 1)
            _simulation.Bote.Einreihen(_spieler, new Reichsgilde.Core.Wirtschaft.Beauftragen(rivale.Inhaber, Reichsgilde.Core.Gauner.Gaunerstueck.Diebstahl, ziele[1].Name), e => Protokollieren("--diebtest=echt Bande: " + e.Meldung));
    }
}
