using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Politik;

/// <summary>Ein Amt (Abschnitt "politik.aemter").</summary>
public sealed class AmtDaten
{
    public string Name { get; init; } = "";

    /// <summary>Stufe in der Pyramide: 1 = unten (Zöllner), 4 = oben (Bürgermeister).</summary>
    public int Stufe { get; init; } = 1;

    /// <summary>Das übergeordnete Amt (null beim höchsten Amt). Für die Pyramide und spätere Befugnisse.</summary>
    public string? Vorgesetzt { get; init; }

    /// <summary>Amtseinkommen in Gulden am Ende jedes Jahres.</summary>
    public double Einkommen { get; init; }

    /// <summary>Ab welchem Stand man sich bewerben darf.</summary>
    public string MindestStand { get; init; } = "Bürger";

    /// <summary>Was das Amt darf – bisher nur Beschreibung, die Befugnisse folgen mit Gericht, Wache und Steuern.</summary>
    public string Befugnis { get; init; } = "";
}

/// <summary>Ein Ratsherr aus der Stadt, der kein Spieler ist (Abschnitt "politik.ratsherren").</summary>
public sealed class RatsherrDaten
{
    public string Name { get; init; } = "";
    public Volk Volk { get; init; } = Volk.Mensch;

    /// <summary>Welches Fron- und Sklavenstatut er für richtig hält.</summary>
    public Fronstatut Haltung { get; init; }

    /// <summary>Welches Amt er zu Spielbeginn hält (null = Nachrücker, springt bei freien Sitzen ein).</summary>
    public string? Amt { get; init; }
}

/// <summary>
/// Regeln für Gunst und Ansehen (Abschnitt "politik.gunst").
///
/// Wie ein Ratsherr wählt: Er gibt jedem Kandidaten Punkte und stimmt für den mit den meisten.
///   Punkte = Ansehen des Kandidaten + Gunst des Ratsherrn für ihn (+ Amtsbonus für den Amtsinhaber)
///   Ansehen eines Hauses = Stand × AnsehenJeStand + Vermögen/1000 × AnsehenJe1000Gulden (gedeckelt)
///   Ansehen eines Ratsherrn = AnsehenRatsherr + Stufe seines Amtes × AnsehenRatsherrJeStufe
///   (ein Bürgermeister ist schwerer zu stürzen als ein Zöllner)
/// Gunst gewinnt man mit Geschenken (Gulden × JeGulden) und verliert sie jedes Jahr zum Teil wieder.
/// </summary>
public sealed class GunstDaten
{
    public double JeGulden { get; init; } = 0.05;
    public double VerfallJeJahr { get; init; } = 0.3;
    public double Hoechstwert { get; init; } = 100;

    /// <summary>Ab dieser Gunst stimmt ein Ratsherr einem Statut zu, das eine Stufe neben seiner Haltung liegt.</summary>
    public double SchwelleNachbarstatut { get; init; } = 25;

    public double AnsehenJeStand { get; init; } = 8;
    public double AnsehenJe1000Gulden { get; init; } = 0.4;
    public double AnsehenHoechstwert { get; init; } = 40;
    public double Amtsbonus { get; init; } = 5;
    public double AnsehenRatsherr { get; init; } = 10;
    public double AnsehenRatsherrJeStufe { get; init; } = 6;

    /// <summary>Ein Ratsherr gibt einem Haus mit einem Oberhaupt seines eigenen Volkes so viele Punkte mehr.</summary>
    public double Volksnaehe { get; init; } = 5;
}

/// <summary>Alle Zahlen zur Stadtpolitik (Abschnitt "politik" in wirtschaft.json).</summary>
public sealed class PolitikDaten
{
    /// <summary>Name der Kammer. Später kommen weitere Kammern dazu (Kirche, Land …).</summary>
    public string Kammer { get; init; } = "Stadtrat";
    public List<AmtDaten> Aemter { get; init; } = new();
    public List<RatsherrDaten> Ratsherren { get; init; } = new();
    public GunstDaten Gunst { get; init; } = new();

    public int HoechsteStufe => Aemter.Count == 0 ? 0 : Aemter.Max(a => a.Stufe);

    internal void Pruefen(List<string> f, Func<string, int> standStufe)
    {
        foreach (var a in Aemter)
        {
            if (string.IsNullOrWhiteSpace(a.Name)) f.Add("politik.aemter: name fehlt.");
            if (a.Stufe < 1) f.Add($"Amt {a.Name}: stufe muss mindestens 1 sein.");
            if (a.Einkommen < 0) f.Add($"Amt {a.Name}: einkommen darf nicht negativ sein.");
            if (standStufe(a.MindestStand) < 0) f.Add($"Amt {a.Name}: mindestStand „{a.MindestStand}“ gibt es nicht.");
            if (a.Vorgesetzt is { } v && Aemter.All(x => x.Name != v)) f.Add($"Amt {a.Name}: vorgesetzt „{v}“ gibt es nicht.");
        }
        foreach (var doppelt in Aemter.GroupBy(a => a.Name).Where(g => g.Count() > 1))
            f.Add($"Amt „{doppelt.Key}“ ist doppelt eingetragen.");
        foreach (var r in Ratsherren)
        {
            if (r.Amt is { } amt && Aemter.All(a => a.Name != amt)) f.Add($"Ratsherr {r.Name}: amt „{amt}“ gibt es nicht.");
            if (r.Volk is Volk.Ork or Volk.Oger) f.Add($"Ratsherr {r.Name}: Orks und Oger haben kein Bürgerrecht.");
        }
        foreach (var amt in Aemter.Where(a => Ratsherren.Count(r => r.Amt == a.Name) > 1))
            f.Add($"Amt {amt.Name}: mehr als ein Ratsherr hält es zu Spielbeginn.");
        if (Gunst.JeGulden < 0 || Gunst.VerfallJeJahr is < 0 or > 1) f.Add("politik.gunst: jeGulden ≥ 0 und verfallJeJahr zwischen 0 und 1.");
    }
}
