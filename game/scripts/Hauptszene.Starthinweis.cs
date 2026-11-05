using Godot;

namespace Reichsgilde;

/// <summary>
/// <b>Start-Hinweis zum Personalbuch</b> – ein Teil der <c>partial class Hauptszene</c>. Seit PR #318 beginnt jeder Betrieb mit einem Lehrling
/// (<c>personal.startJeBetrieb</c>); die KI stellt sofort nach, ein neuer Spieler übersieht es leicht. Deshalb steht bei einer <b>neuen Partie</b>
/// einmalig eine Tafel in den Meldungen unten links (bleibt bis zum Klick stehen) mit einem Knopf, der in die Werkstatt zum Personalbuch führt.
/// Nach dem Laden eines Spielstands erscheint sie nicht; gemerkt wird nichts. Ohne Startoptionen von selbst, sonst mit <c>--starthinweis=1</c>
/// (Klicktests sollen gleiche Bilder liefern). Zieht später mit den Meldungen ins Benachrichtigungssystem um.
/// </summary>
public partial class Hauptszene
{
    private void StartHinweisZeigen(Dictionary<string, string> optionen)
    {
        var gewollt = optionen.GetValueOrDefault("starthinweis") == "1" || OS.GetCmdlineUserArgs().Length == 0;
        if (!gewollt || _probepartie || _simulation.Zeit.VergangeneMinuten != 0) return; // nur neue Partie
        if (_eigeneGiesserei is not { } betrieb || betrieb.Belegt > 1) return;

        var tafel = Leistentafel(10, 6);
        tafel.TooltipText = "Bleibt stehen, bis ihr sie schließt.";
        var spalte = new VBoxContainer();
        spalte.AddThemeConstantOverride("separation", 6);
        tafel.AddChild(spalte);
        Hauptmenue.Text(spalte, "Hinweis: In Eurer Werkstatt arbeitet nur ein Lehrling. Im Personalbuch könnt Ihr weitere einstellen.",
            13, new Color(0.95f, 0.82f, 0.45f), umbruchBreite: 380);
        var knoepfe = new HBoxContainer();
        knoepfe.AddThemeConstantOverride("separation", 8);
        spalte.AddChild(knoepfe);
        Hauptmenue.Knopf(knoepfe, "Zum Personalbuch", () =>
        {
            MeldungEntfernen(tafel);
            if (_stadt.InnenansichtBetreten(betrieb)) InnenTafelOeffnen("personal");
        });
        Hauptmenue.Knopf(knoepfe, "Später", () => MeldungEntfernen(tafel));
        _meldungen.AddChild(tafel);
        _meldungsZeilen.Add((tafel, true, 0)); // bleibt stehen wie eine Warnung
    }
}
