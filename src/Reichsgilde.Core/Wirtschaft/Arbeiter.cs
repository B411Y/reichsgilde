using Reichsgilde.Core.Speichern;
namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Ein einzelner Arbeiter eines Betriebs – wie in Gilde 1 mit eigener Ware, eigener Laune und eigener Dienstzeit.
/// Ändern lässt er sich nur über Befehle (Zuteilen, Befördern, Prämie …); die Oberfläche liest ihn.
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

    internal Arbeiter(int nummer, string arbeitskraft, long eingestelltUm, double laune)
    {
        Nummer = nummer;
        Arbeitskraft = arbeitskraft;
        EingestelltUm = eingestelltUm;
        Laune = laune;
    }

    /// <summary>Fehlt er gerade (Unfall)?</summary>
    public bool FaelltAus(long jetzt) => AusfallBis is { } bis && bis > jetzt;

    internal ArbeiterStand Sichern() => new()
    {
        Nummer = Nummer, Arbeitskraft = Arbeitskraft, Ware = Ware, Laune = Laune, AusfallBis = AusfallBis,
        EingestelltUm = EingestelltUm, PraemieBis = PraemieBis, UnzufriedenStunden = UnzufriedenStunden,
    };

    internal static Arbeiter Laden(ArbeiterStand s) => new(s.Nummer, s.Arbeitskraft, s.EingestelltUm, s.Laune)
    {
        Ware = s.Ware, AusfallBis = s.AusfallBis, PraemieBis = s.PraemieBis, UnzufriedenStunden = s.UnzufriedenStunden,
    };
}
