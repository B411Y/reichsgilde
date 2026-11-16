using Godot;

namespace Reichsgilde;

/// <summary>
/// <b>Platzwahl am Start</b> – ein Teil der <c>partial class Hauptszene</c> („nur noch Bauzonen“, Entscheidung vom 15.11.2026).
/// Eine neue Partie aus dem Menü beginnt ohne Wohnhaus und Werkstatt des Spielers (<c>NeuePartie.Startwahl</c>): Er setzt beides selbst in
/// eine Bauzone – erst das Wohnhaus, dann die Werkstatt seines Berufs. Der Kern sagt, ob die Wahl offen ist
/// (<c>w.Bauen.StartwahlVon</c>); diese Datei sorgt nur für den Ablauf:
///
///   • Im Einzelspiel ruht die Zeit, bis beides steht (Haltegrund <see cref="HaltegrundPlatzwahl"/>, abgeglichen in ZeitHalteAbgleichen) –
///     gesetzt wird er schon vor dem ersten Tick. Im Mehrspieler läuft die Zeit weiter (eine gemeinsame Regel fehlt noch, siehe kern.md).
///   • Die Bautafel (<see cref="BauAnzeige"/>) zeigt statt der Liste die Platzwahl und bleibt offen: Schließt sie etwas (anderes Fenster,
///     Taste B, der gesetzte erste Schritt), öffnet sie sich im nächsten Bild wieder – außer über Spielmenü, Einstellungen und Hilfe.
///   • Steht die Werkstatt, ist sie „der eigene Betrieb“ der Hauptszene, und der Start-Hinweis zum Personalbuch folgt.
///
/// Ein geladener Spielstand mit offener Wahl läuft genauso weiter (die Wahl steht im Spielstand).
/// </summary>
public partial class Hauptszene
{
    /// <summary>Der Haltegrund des Takts während der Platzwahl am Start (nur Einzelspiel).</summary>
    private const string HaltegrundPlatzwahl = "die Platzwahl";

    /// <summary>War die Platzwahl im letzten Bild offen? (Damit ihr Ende genau einmal behandelt wird.)</summary>
    private bool _startwahlLief;

    /// <summary>Die Startoptionen, für den Start-Hinweis nach der Platzwahl.</summary>
    private Dictionary<string, string>? _startOptionen;

    /// <summary>Wählt der Spieler noch den Platz von Wohnhaus oder Werkstatt?</summary>
    private bool StartwahlOffen => _spieler is not null && _simulation.Wirtschaft?.Bauen.StartwahlVon(_spieler) is not null;

    /// <summary>Hält die Platzwahl die Zeit an? Nur im Einzelspiel und solange die Partie läuft.</summary>
    private bool StartwahlHaelt => DarfAnhalten && !_simulation.IstBeendet && StartwahlOffen;

    /// <summary>Jedes Bild, vor dem Tick: Zeit anhalten, Bautafel offen halten, das Ende der Wahl einmal behandeln.</summary>
    private void StartwahlPflegen()
    {
        if (!StartwahlOffen)
        {
            if (!_startwahlLief) return;
            _startwahlLief = false;
            // Die Werkstatt steht: Sie ist jetzt der eigene Betrieb (Kopfzeile, Startoptionen, Start-Hinweis)
            if (_simulation.Wirtschaft is { } w && _spieler is not null)
                _eigeneGiesserei ??= w.Betriebe.FirstOrDefault(b => b.Konto == _spieler && b.InDerStadt);
            if (_startOptionen is not null) StartHinweisZeigen(_startOptionen);
            return;
        }
        _startwahlLief = true;
        if (StartwahlHaelt) _simulation.Takt.Anhalten(HaltegrundPlatzwahl); // schon vor dem ersten Tick; freigegeben wird in ZeitHalteAbgleichen
        if (_bau.Aktiv || _bauFrage.Visible || _spielmenue.Visible || _einstellungen.Tafel.Visible || _hilfe.Visible || _simulation.IstBeendet) return;
        BauenOeffnen(); // die Tafel wählt das Gebäude selbst (BauAnzeige.StartwahlWaehlen)
        // Klicktest: --bauort2=x,z,drehung setzt den Umriss des zweiten Schritts (Werkstatt) – wie --bauort den des ersten
        if (_startOptionen?.GetValueOrDefault("bauort2") is { } ort && _simulation.Wirtschaft!.Bauen.StartwahlVon(_spieler!) is { WohnhausOffen: false })
            BauOrtSetzen(ort);
    }
}
