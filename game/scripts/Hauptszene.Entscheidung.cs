using Godot;

namespace Reichsgilde;

/// <summary>Startoption <c>--entscheidung=</c> der Hauptszene (Benachrichtigungen Stufe 3), ein Teil der <c>partial class Hauptszene</c>.</summary>
public partial class Hauptszene
{
    /// <summary>Startoption <c>--entscheidung=</c>, wartet wie <c>--erpresserbrief=</c> bis nach dem Vorspulen.</summary>
    private string? _entscheidungWartet;

    /// <summary>
    /// <c>--entscheidung=&lt;Erpresserbrief|Schutzgeld|Loesegeld|Krankheit|Erbe|Geheimnis|Mahnung&gt;[:Betrag]</c>: legt den Anlass einer Entscheidung
    /// über Entwicklerbefehle an (darum nur mit <c>--entwickler=1</c>); die Frage öffnet der Kern in der nächsten Spielminute. Unterwelt: eine Forderung
    /// des ersten anderen Hauses (<see cref="Core.Entwickler.EntwicklerForderung"/>, ohne Betrag 400). Krankheit: Ruhr beim ersten Kind (sonst beim
    /// Oberhaupt). Erbe: das Oberhaupt bekommt die Pest, fehlende Kinder werden geboren (zwei zur Wahl). Geheimnis: Schulden des Oberhaupts des ersten
    /// anderen Hauses, euer Haus weiß davon. Mahnung: das Vermögen fällt unter null – die Mahnung kommt mit dem nächsten Jahresabschluss
    /// (z. B. mit <c>--spulen=</c> bis über das Jahresende).
    /// </summary>
    private void EntscheidungStartoption(string wert)
    {
        foreach (var teil in wert.Split(',')) EntscheidungStartoptionEine(teil.Trim()); // mehrere mit Komma: ein Stapel im Klicktest
    }

    private void EntscheidungStartoptionEine(string wert)
    {
        if (_simulation.Wirtschaft is not { } w || _spieler is null) return;
        var teile = wert.Split(':');
        var betrag = 400.0;
        if (!Enum.TryParse<Core.Entscheidungen.Entscheidungsart>(teile[0], out var art) || !Enum.IsDefined(art)
            || teile.Length > 1 && !double.TryParse(teile[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out betrag))
        {
            Protokollieren($"--entscheidung={wert}: erwartet <Erpresserbrief|Schutzgeld|Loesegeld|Krankheit|Erbe|Geheimnis|Mahnung>[:Betrag].");
            return;
        }
        void Befehl(Core.Wirtschaft.Befehl befehl)
        {
            var text = "--entscheidung: " + UiHilfe.MeldungText(_simulation.Bote.SofortAusfuehren(_spieler, befehl));
            Protokollieren(text);
            GD.Print(text); // auch ins Log (Klicktest, --headless)
        }
        var familie = w.Familien.Von(_spieler);
        var rivale = w.Konten.FirstOrDefault(k => k != _spieler && k.IstAktiv && w.Familien.Von(k) is not null);
        switch (art)
        {
            case Core.Entscheidungen.Entscheidungsart.Erpresserbrief or Core.Entscheidungen.Entscheidungsart.Schutzgeld or Core.Entscheidungen.Entscheidungsart.Loesegeld:
                if (rivale is null) { Protokollieren("--entscheidung: kein anderes Haus."); return; }
                Befehl(new Core.Entwickler.EntwicklerForderung(art, rivale.Inhaber, betrag));
                break;
            case Core.Entscheidungen.Entscheidungsart.Krankheit:
                if (familie is null) { Protokollieren("--entscheidung: euer Haus hat keine Familie."); return; }
                Befehl(new Core.Entwickler.EntwicklerKrankheit((familie.Kinder.FirstOrDefault(k => k.Lebt) ?? familie.Oberhaupt).Nummer, "Ruhr"));
                break;
            case Core.Entscheidungen.Entscheidungsart.Erbe:
                if (familie is null) { Protokollieren("--entscheidung: euer Haus hat keine Familie."); return; }
                for (var i = familie.Kinder.Count(k => k.Lebt); i < 2; i++)
                    Befehl(new Core.Entwickler.EntwicklerGeburt(i == 0 ? Core.Dynastie.Geschlecht.Frau : Core.Dynastie.Geschlecht.Mann));
                Befehl(new Core.Entwickler.EntwicklerKrankheit(familie.Oberhaupt.Nummer, "Pest"));
                break;
            case Core.Entscheidungen.Entscheidungsart.Geheimnis:
                if (rivale is null || w.Familien.Von(rivale) is not { } f) { Protokollieren("--entscheidung: kein anderes Haus mit Familie."); return; }
                Befehl(new Core.Entwickler.EntwicklerGeheimnis(f.Oberhaupt.Kennung, Core.Geheimnisse.Geheimnisart.Schulden));
                break;
            default: // Mahnung
                Befehl(new Core.Entwickler.EntwicklerGulden(-(_spieler.Vermoegen + betrag)));
                Protokollieren("--entscheidung=Mahnung: Die Mahnung kommt mit dem nächsten Jahresabschluss.");
                break;
        }
    }
}
