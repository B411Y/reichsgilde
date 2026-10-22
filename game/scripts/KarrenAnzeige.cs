using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Der Karrenhof (Hoftor, PR #152): die Karren des Hauses kaufen und ihren Zustand sehen, dazu „Handel am Markt …“. Es ist derselbe
/// Abschnitt wie im Bedienfeld der Betriebsverwaltung – <see cref="BetriebsVerwaltung"/> mit <see cref="BetriebsVerwaltung.NurKarren"/>.
/// </summary>
public sealed class KarrenAnzeige : InnenTafel
{
    private readonly BetriebsVerwaltung _karren = new() { NurKarren = true };
    private readonly Label _name;
    private readonly Label _keine;
    private const string KeineKarren = "Dieses Haus hat nichts zu fahren. Die Wache begleitet gegen Lohn die Karren anderer Häuser (Wachplan).";

    public KarrenAnzeige() : base(700)
    {
        Hauptmenue.Text(Inhalt, "Karrenhof", 24, Gold, zentriert: true);
        _name = Hauptmenue.Text(Inhalt, "", 14, Hell, zentriert: true);
        Inhalt.AddChild(new HSeparator());
        _keine = Hauptmenue.Text(Inhalt, KeineKarren, 13, Grau, umbruchBreite: 620);
        _keine.Visible = false;
        Inhalt.AddChild(_karren);
        _karren.HandelGewuenscht += HandelMelden;
        _karren.Gemeldet += Melden;
    }

    public override void Verbinden(Simulation simulation, Konto spieler)
    {
        base.Verbinden(simulation, spieler);
        _karren.Verbinden(simulation, spieler);
    }

    // Das Bedienfeld friert sich nicht ein und baut sich selbst um: Die Tafel baut nur beim Öffnen und stellt ihm den Betrieb hin.
    protected override string Stand(Stadtwirtschaft w, Betrieb b) => b.Name;

    protected override void Leeren() { }

    protected override void Aufbauen(Stadtwirtschaft w, Betrieb b)
    {
        _name.Text = $"{b.Name} · {b.Artdaten.Name}";
        var bank = w.Bank.IstBankbetrieb(b);
        _keine.Text = bank ? "Dieses Haus hat nichts zu fahren: Die Schreiber wechseln und verleihen nur Geld." : KeineKarren;
        _keine.Visible = w.Wache.IstWachbetrieb(b) || bank; // wie im Bedienfeld: Wache und Geldleihe haben keine Karren
        _karren.Zeigen(b);
    }

    public override void Aktualisieren(double delta)
    {
        base.Aktualisieren(delta);
        if (Visible) _karren.Aktualisieren();
    }
}
