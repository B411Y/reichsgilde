using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Speichern;
namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Ein einzelner Arbeiter eines Betriebs – wie in Gilde 1 mit eigener Ware, eigener Laune und eigener Dienstzeit.
/// Ändern lässt er sich nur über Befehle (Zuteilen, Befördern, Prämie …); die Oberfläche liest ihn.
/// Seit PR #224 hat jeder einen Namen, ein Aussehen fürs Porträt, Erfahrung und – als Vorarbeiter – eine Eigenschaft.
/// </summary>
public sealed class Arbeiter
{
    /// <summary>Feste Nummer im Betrieb (ab 1) – bleibt, auch wenn andere gehen. Befehle nennen den Arbeiter bei dieser Nummer.</summary>
    public int Nummer { get; }

    /// <summary>Art der Arbeitskraft („Lehrling", „Geselle", „Ork" …) – ändert sich nur durch Beförderung.</summary>
    public string Arbeitskraft { get; internal set; }

    /// <summary>Die Ware, die ihm zugeteilt ist (null: er arbeitet an der Ware des Betriebs, <see cref="Betrieb.Produkt"/>).</summary>
    public string? Ware { get; internal set; }

    /// <summary>Laune 0–100 (neutral 50 laut Daten).</summary>
    public double Laune { get; internal set; }

    /// <summary>Nach einem Unfall: Er fehlt bis zu dieser Spielminute (null = er ist da).</summary>
    public long? AusfallBis { get; internal set; }

    /// <summary>Seit dieser Spielminute arbeitet er hier (für die Dienstzeit bis zur Beförderung).</summary>
    public long EingestelltUm { get; internal set; }

    /// <summary>Bis zu dieser Spielminute wirkt eine Prämie auf seine Laune (null = keine).</summary>
    public long? PraemieBis { get; internal set; }

    /// <summary>So viele Arbeitsstunden am Stück ist seine Laune schon unter der Kündigungsgrenze.</summary>
    public int UnzufriedenStunden { get; internal set; }

    /// <summary>Vor- und Familienname (gewürfelt beim Einstellen, fest je Betrieb und Nummer).</summary>
    public string Name { get; internal set; } = "";

    public Volk Volk { get; internal set; } = Volk.Mensch;
    public Geschlecht Geschlecht { get; internal set; } = Geschlecht.Mann;

    /// <summary>Wie er im Porträt aussieht (Gesicht, Haar, Augen – passend zum Volk).</summary>
    public Aussehen Aussehen { get; internal set; }

    /// <summary>Alter in Menschenjahren beim Einstellen (das Porträt altert mit der Dienstzeit, <see cref="Menschenjahre"/>).</summary>
    public double AlterBeimEinstellen { get; internal set; }

    /// <summary>
    /// Erfahrung in Stunden (PR #224): Jede Arbeitsstunde bringt so viel, wie er in ihr schafft (Tempo × Laune), als Anfänger mit
    /// Lehrlingsstube und Lehrmeister mehr. Reicht sie für die nächste Stufe, kann er befördert werden.
    /// </summary>
    public double Erfahrung { get; internal set; }

    /// <summary>Als Vorarbeiter: seine Eigenschaft (Kennung aus „personal.laufbahnen“; null = keine oder noch nicht gewählt).</summary>
    public string? Eigenschaft { get; internal set; }

    /// <summary>Nach der Beförderung zum Vorarbeiter: die drei gezogenen Eigenschaften, aus denen gewählt wird (null = nichts zu wählen).</summary>
    public IReadOnlyList<string>? EigenschaftWahl { get; internal set; }

    internal Arbeiter(int nummer, string arbeitskraft, long eingestelltUm, double laune)
    {
        Nummer = nummer;
        Arbeitskraft = arbeitskraft;
        EingestelltUm = eingestelltUm;
        Laune = laune;
    }

    /// <summary>Fehlt er gerade (Unfall)?</summary>
    public bool FaelltAus(long jetzt) => AusfallBis is { } bis && bis > jetzt;

    /// <summary>Wie alt er jetzt ist, in Menschenjahren: Alter beim Einstellen plus Dienstjahre (ein Spieljahr ist ein Spieltag).</summary>
    public double Menschenjahre(long jetzt) => AlterBeimEinstellen + Math.Max(0, jetzt - EingestelltUm) / (double)Zeit.Spielzeit.MinutenProTag;

    internal ArbeiterStand Sichern() => new()
    {
        Nummer = Nummer, Arbeitskraft = Arbeitskraft, Ware = Ware, Laune = Laune, AusfallBis = AusfallBis,
        EingestelltUm = EingestelltUm, PraemieBis = PraemieBis, UnzufriedenStunden = UnzufriedenStunden,
        Name = Name, Volk = Volk, Geschlecht = Geschlecht, Gesicht = Aussehen.Gesicht, Haar = Aussehen.Haar, Augen = Aussehen.Augen,
        AlterBeimEinstellen = AlterBeimEinstellen,
        Erfahrung = Erfahrung, Eigenschaft = Eigenschaft, EigenschaftWahl = EigenschaftWahl?.ToList(),
    };

    internal static Arbeiter Laden(ArbeiterStand s) => new(s.Nummer, s.Arbeitskraft, s.EingestelltUm, s.Laune)
    {
        Ware = s.Ware, AusfallBis = s.AusfallBis, PraemieBis = s.PraemieBis, UnzufriedenStunden = s.UnzufriedenStunden,
        Name = s.Name ?? "", Volk = s.Volk ?? Volk.Mensch, Geschlecht = s.Geschlecht ?? Geschlecht.Mann,
        Aussehen = new Aussehen(s.Gesicht ?? 0, s.Haar ?? default, s.Augen ?? default),
        AlterBeimEinstellen = s.AlterBeimEinstellen ?? 0, Erfahrung = s.Erfahrung ?? 0, Eigenschaft = s.Eigenschaft,
        EigenschaftWahl = s.EigenschaftWahl,
    };
}
