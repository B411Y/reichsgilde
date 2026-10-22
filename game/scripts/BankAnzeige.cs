using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das Bankgeschäft (Hauptbuchpult in der Wechselstube, PR #175): Kasse einlegen und abheben, der Zins für Häuser, Stadtzinsen und Wechsel, die offenen Kredite. Es ist derselbe
/// Abschnitt wie im Bedienfeld der Betriebsverwaltung – <see cref="BetriebsVerwaltung"/> mit <see cref="BetriebsVerwaltung.NurBank"/>.
/// </summary>
public sealed class BankAnzeige : InnenTafel
{
    private readonly BetriebsVerwaltung _bank = new() { NurBank = true };
    private readonly Label _name;

    public BankAnzeige() : base(700)
    {
        Hauptmenue.Text(Inhalt, "Bankgeschäft", 24, Gold, zentriert: true);
        _name = Hauptmenue.Text(Inhalt, "", 14, Hell, zentriert: true);
        Inhalt.AddChild(new HSeparator());
        Inhalt.AddChild(_bank);
        _bank.Gemeldet += Melden;
    }

    public override void Verbinden(Simulation simulation, Konto spieler)
    {
        base.Verbinden(simulation, spieler);
        _bank.Verbinden(simulation, spieler);
    }

    // Das Bedienfeld friert sich nicht ein und baut sich selbst um: Die Tafel baut nur beim Öffnen und stellt ihm den Betrieb hin.
    protected override string Stand(Stadtwirtschaft w, Betrieb b) => b.Name;

    protected override void Leeren() { }

    protected override void Aufbauen(Stadtwirtschaft w, Betrieb b)
    {
        _name.Text = $"{b.Name} · {b.Artdaten.Name}";
        _bank.Zeigen(b);
    }

    public override void Aktualisieren(double delta)
    {
        base.Aktualisieren(delta);
        if (Visible) _bank.Aktualisieren();
    }
}
