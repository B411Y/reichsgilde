using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;
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

    /// <summary>Seine Konfession (null = der Startglaube seines Volkes, siehe Abschnitt „glaube").</summary>
    public Konfession? Glaube { get; init; }

    /// <summary>Welches Fron- und Sklavenstatut er für richtig hält.</summary>
    public Fronstatut Haltung { get; init; }

    /// <summary>Welches Amt er zu Spielbeginn hält (null = Nachrücker, springt bei freien Sitzen ein).</summary>
    public string? Amt { get; init; }

    /// <summary>Mann oder Frau (fürs Porträt und die Bürger-KI; null = aus dem Vornamen abgeleitet, alte Daten).</summary>
    public Geschlecht? Geschlecht { get; init; }

    /// <summary>Gesicht, Haar und Augen im Porträt (null = fest aus dem Namen abgeleitet, alte Daten). Wirkt auf nichts in der Simulation.</summary>
    public AussehenDaten? Aussehen { get; init; }

    /// <summary>Feste Charakterzüge (Kennungen aus "charakterzuege"); null = aus dem Namen gewürfelt, in jeder Partie gleich.</summary>
    public List<string>? Zuege { get; init; }
}

/// <summary>Ein Aussehen in den Daten (<c>{ "gesicht": 2, "haar": "Braun", "augen": "Blau" }</c>), siehe <see cref="Dynastie.Aussehen"/>.</summary>
public sealed class AussehenDaten
{
    public int Gesicht { get; init; }
    public Haarfarbe Haar { get; init; }
    public Augenfarbe Augen { get; init; }

    public Aussehen Wert => new(Gesicht, Haar, Augen);
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

    /// <summary>Stadtkasse, Steuersatz und Stimmen der Ratsherren zu Geldfragen (PR #200). Fehlt der Abschnitt, gibt es keine Stadtkasse.</summary>
    public KasseDaten? Kasse { get; init; }

    /// <summary>Was der Rat bauen lassen kann (Antrag „Bauvorhaben“, aus der Stadtkasse).</summary>
    public List<BauvorhabenDaten> Bauvorhaben { get; init; } = new();

    /// <summary>Antrag „Fest ausrichten“ (aus der Stadtkasse).</summary>
    public FestDaten Fest { get; init; } = new();

    /// <summary>Die Befugnisse der Ämter.</summary>
    public BefugnisDaten Befugnisse { get; init; } = new();

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
            else if (r.Aussehen is { } a && Aussehenspalette.Verboten(r.Volk, a.Wert) is { } grund) f.Add($"Ratsherr {r.Name}: aussehen – {grund}");
        }
        foreach (var amt in Aemter.Where(a => Ratsherren.Count(r => r.Amt == a.Name) > 1))
            f.Add($"Amt {amt.Name}: mehr als ein Ratsherr hält es zu Spielbeginn.");
        if (Gunst.JeGulden < 0 || Gunst.VerfallJeJahr is < 0 or > 1) f.Add("politik.gunst: jeGulden ≥ 0 und verfallJeJahr zwischen 0 und 1.");
        if (Kasse is { } k)
        {
            if (k.SteuerMin < 0 || k.SteuerMax > 50 || k.SteuerMin > k.SteuerMax) f.Add("politik.kasse: 0 ≤ steuerMin ≤ steuerMax ≤ 50 (Prozent).");
            if (k.Startbestand < 0 || k.StartJeKarte.Values.Any(x => x < 0) || k.Reserve < 0 || k.Sparsamkeit.Values.Any(x => x <= 0))
                f.Add("politik.kasse: Startbestand und Reserve ≥ 0, Sparsamkeit > 0.");
        }
        foreach (var b in Bauvorhaben)
        {
            if (string.IsNullOrWhiteSpace(b.Kennung) || string.IsNullOrWhiteSpace(b.Name)) f.Add("politik.bauvorhaben: kennung und name dürfen nicht fehlen.");
            if (b.Kosten <= 0 || b.BauzeitStunden < 0 || b.Ansehen < 0 || b.AnsehenJahre < 0) f.Add($"Bauvorhaben {b.Kennung}: kosten > 0, bauzeitStunden, ansehen und ansehenJahre ≥ 0.");
            if (b.Wert is < 0 or > 1) f.Add($"Bauvorhaben {b.Kennung}: wert muss zwischen 0 und 1 liegen.");
        }
        foreach (var doppelt in Bauvorhaben.GroupBy(b => b.Kennung).Where(g => g.Count() > 1))
            f.Add($"Bauvorhaben „{doppelt.Key}“ ist doppelt eingetragen.");
        if (Fest.Kosten <= 0 || Fest.Ansehen < 0 || Fest.Nachfrage < 0 || Fest.Gaeste < 0) f.Add("politik.fest: kosten > 0, ansehen, nachfrage und gaeste ≥ 0.");
        var bf = Befugnisse;
        if (bf.RichterSpanne is < 0 or > 1 || bf.StreifeFaktor < 1 || bf.StreifeMindestens is < 0 or > 1 || bf.StreifeHoechstens is < 0 or > 1
            || bf.MarktmeisterSteuer is < 0 or > 1 || bf.KautionJeJahr < 0)
            f.Add("politik.befugnisse: richterSpanne, streifeMindestens, streifeHoechstens und marktmeisterSteuer zwischen 0 und 1, streifeFaktor ≥ 1, kautionJeJahr ≥ 0.");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Stadtkasse, Anträge und Befugnisse (PR #200, ab Datenversion 54)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Die Stadtkasse (Abschnitt „politik.kasse“): Einnahmen aus Umsatzsteuer, Glaubenszins, Bußen, Gebühren und dem Zoll am eigenen Hafen;
/// Ausgaben für Amtsgehälter, Bauvorhaben und Feste. Die Ratsherren stimmen über Geldfragen nach dem Stand der Kasse ab:
///   • Steuersatz: höher, solange die Kasse unter ihrer Reserve liegt; niedriger, sobald sie mehr als das Doppelte hat.
///   • Bauvorhaben und Fest: dafür, wenn danach noch die Reserve in der Kasse bleibt.
///   • Wer ihre Gunst hat (ab <see cref="AntragGunst"/>), bekommt ihre Stimme in jedem Fall.
/// Reserve eines Ratsherrn = <see cref="Reserve"/> × Sparsamkeit seines Volkes (Zwerge rechnen strenger, Halblinge feiern gern).
/// </summary>
public sealed class KasseDaten
{
    /// <summary>Geld in der Kasse zu Spielbeginn.</summary>
    public double Startbestand { get; init; } = 3000;

    /// <summary>
    /// Kleine Städte zahlen kleinere Amtsgehälter: volle Gehälter ab so vielen Häusern, darunter nach den Einwohnern (Datenversion 57: 6;
    /// ohne Angabe 0 = immer volle Gehälter).
    /// </summary>
    public int GehaltVolleHaeuser { get; init; }

    /// <summary>Startbestand je Karte (Kennung der Karte → Gulden); fehlt die Karte, gilt <see cref="Startbestand"/>.</summary>
    public Dictionary<string, double> StartJeKarte { get; init; } = new();

    /// <summary>Kleinster und größter Steuersatz, den der Rat beschließen kann (Prozent).</summary>
    public int SteuerMin { get; init; } = 3;
    public int SteuerMax { get; init; } = 8;

    /// <summary>So viel will ein Ratsherr mindestens in der Kasse wissen (mal Sparsamkeit seines Volkes).</summary>
    public double Reserve { get; init; } = 2000;

    /// <summary>Faktor auf die Reserve je Volk (fehlt ein Volk: 1).</summary>
    public Dictionary<Volk, double> Sparsamkeit { get; init; } = new();

    /// <summary>Ab dieser Gunst stimmt ein Ratsherr jedem Antrag zu Steuer, Bau und Fest des Hauses zu.</summary>
    public double AntragGunst { get; init; } = 20;

    public double ReserveVon(Volk volk) => Reserve * Sparsamkeit.GetValueOrDefault(volk, 1);
}

/// <summary>Was ein fertiges Bauvorhaben bewirkt.</summary>
public enum Bauwirkung
{
    /// <summary>Weniger Krankheiten und Pest: Chance zu erkranken × (1 − Wert).</summary>
    Gesundheit,

    /// <summary>Nächtliche Taten in der Stadt gelingen seltener: Erfolgschance × (1 − Wert).</summary>
    Nachtschutz,

    /// <summary>Schiffe laden und löschen schneller: Lade- und Liegezeit × (1 − Wert).</summary>
    Hafen,

    /// <summary>Mehr Käufer am Markt: Nachfrage aller Waren × (1 + Wert).</summary>
    Nachfrage,
}

/// <summary>Ein Bauvorhaben der Stadt (Abschnitt „politik.bauvorhaben“): jedes wird einmal gebaut und wirkt danach für immer.</summary>
public sealed class BauvorhabenDaten
{
    public string Kennung { get; init; } = "";
    public string Name { get; init; } = "";
    public string Beschreibung { get; init; } = "";
    public double Kosten { get; init; }

    /// <summary>Bauzeit in Spielstunden (19 = ein Spieljahr); so lange steht die Baustelle.</summary>
    public int BauzeitStunden { get; init; } = Zeit.Spielzeit.StundenProTag;

    public Bauwirkung Wirkung { get; init; }
    public double Wert { get; init; }

    /// <summary>Ansehen für den Antragsteller, wenn der Rat zustimmt – für so viele Jahre.</summary>
    public double Ansehen { get; init; } = 3;
    public int AnsehenJahre { get; init; } = 3;
}

/// <summary>Antrag „Fest ausrichten“ (Abschnitt „politik.fest“): Das Fest gilt das ganze nächste Jahr.</summary>
public sealed class FestDaten
{
    public double Kosten { get; init; } = 1500;

    /// <summary>Ansehen für den Antragsteller (bis Ende des Festjahres) – zählt schon bei den Wahlen derselben Sitzung.</summary>
    public double Ansehen { get; init; } = 5;

    /// <summary>Mehr Marktbesucher: Nachfrage aller Waren × (1 + Nachfrage) im Festjahr.</summary>
    public double Nachfrage { get; init; } = 0.05;

    /// <summary>Mehr Wirtshausgäste: Gäste × (1 + Gaeste) im Festjahr.</summary>
    public double Gaeste { get; init; } = 0.2;
}

/// <summary>Die Befugnisse der Ämter (Abschnitt „politik.befugnisse“).</summary>
public sealed class BefugnisDaten
{
    /// <summary>Stadtrichter: Die Buße darf er um höchstens so viel ändern (0,25 = ±25 %).</summary>
    public double RichterSpanne { get; init; } = 0.25;

    /// <summary>Stadthauptmann: Im Viertel der Streife wirkt der Schutz der Wache so viel mal …</summary>
    public double StreifeFaktor { get; init; } = 2;

    /// <summary>… mindestens so viel (die Stadtbüttel gehen mit, auch ohne Wachhaus) …</summary>
    public double StreifeMindestens { get; init; } = 0.1;

    /// <summary>… und höchstens so viel.</summary>
    public double StreifeHoechstens { get; init; } = 0.6;

    /// <summary>Marktmeister: Auf eigene Verkäufe am Markt zahlt er nur diesen Teil der Umsatzsteuer.</summary>
    public double MarktmeisterSteuer { get; init; } = 0.5;

    /// <summary>Gefängniswärter: Kaution für die vorzeitige Freilassung je angefangenem Jahr, das noch bleibt (geht an den Wärter).</summary>
    public double KautionJeJahr { get; init; } = 1000;
}
