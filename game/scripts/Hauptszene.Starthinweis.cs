using Godot;

namespace Reichsgilde;

/// <summary>
/// <b>Start-Hinweis zum Personalbuch</b> – ein Teil der <c>partial class Hauptszene</c>. Seit PR #318 beginnt jeder Betrieb mit einem Lehrling
/// (<c>personal.startJeBetrieb</c>); die KI stellt sofort nach, ein neuer Spieler übersieht es leicht. Deshalb steht bei einer <b>neuen Partie</b>
/// einmalig ein Brief (Benachrichtigungen, Stufe 1; bleibt, bis man ihn weglegt) mit einem Knopf, der in die Werkstatt zum Personalbuch führt.
/// Nach dem Laden eines Spielstands erscheint sie nicht; gemerkt wird nichts. Ohne Startoptionen von selbst, sonst mit <c>--starthinweis=1</c>
/// (Klicktests sollen gleiche Bilder liefern).
/// </summary>
public partial class Hauptszene
{
    private void StartHinweisZeigen(Dictionary<string, string> optionen)
    {
        var gewollt = optionen.GetValueOrDefault("starthinweis") == "1" || OS.GetCmdlineUserArgs().Length == 0;
        if (!gewollt || _probepartie || _simulation.Zeit.VergangeneMinuten != 0) return; // nur neue Partie
        if (_eigeneGiesserei is not { } betrieb || betrieb.Belegt > 1) return;

        // Als Brief (Benachrichtigungen Stufe 1): Absender der Haushofmeister, der Knopf öffnet das Personalbuch
        var thema = Ordnung.Thema("handel");
        BriefEinreihen(new Brief
        {
            Schluessel = "starthinweis", Titel = "Ein Lehrling genügt nicht", Absender = "Euer Haushofmeister", Farbe = Color.FromHtml(thema.Farbe), Thema = thema.Id,
            Ziel = "personal", Betrieb = betrieb.Name,
            Zeilen = { ("", "In Eurer Werkstatt arbeitet nur ein Lehrling. Im Personalbuch könnt Ihr weitere einstellen.", false) },
        });
    }
}
