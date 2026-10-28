using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Kartenbau.Umbauten;

// ═════════════════════════════════════════════════════════════════════════════
//  Probestadt in die Kartenwahl (Entscheidung des Nutzers 28.10.2026)
//
//  Das neue Hauptmenü bietet vor dem Oberhaupt eine Stadt zur Wahl an. Dort erscheinen Karten mit dem Kennzeichen „spielbar“ – heute nur
//  die Probestadt. Sie bekommt das Kennzeichen und einen Kurztext für ihre Kachel. Sonst ändert sich an der Karte nichts.
//  Aufruf: dotnet run --project tools/Kartenbau -- umbauen kartenwahl
// ═════════════════════════════════════════════════════════════════════════════

public sealed class Kartenwahl : Umbau
{
    public override string Kennung => "kartenwahl";
    public override string Karte => "probestadt";
    public override string Beschreibung => "Probestadt ist spielbar (Kartenwahl) und bekommt einen Kurztext";

    public const string Kurztext = "Eine kleine, ummauerte Stadt mit Marktplatz und Kirche, von einem Fluss durchzogen – überschaubar und gut zum Einstieg.";

    public override void Anwenden(Kartenentwurf e, TextWriter aus)
    {
        e.SpielbarSetzen(true);
        e.BeschreibungSetzen(Kurztext);
        aus.WriteLine("   spielbar: true, Kurztext gesetzt");
    }
}
