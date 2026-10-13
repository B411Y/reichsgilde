using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Die Wirtschaft der Reichsstadt: Markt, Betriebe, Konten und das geltende Fronstatut.
///
/// Ablauf:
///   • jede Spielminute während der Arbeitszeit (Standard 08:00–20:00) arbeiten alle Betriebe,
///   • jede volle Spielstunde rückt das Markt-Fenster weiter,
///   • am Jahresende werden Löhne, Unterhalt und Pacht fällig, und Unfreie können ausbrechen.
///
/// Alle Zufallsereignisse kommen aus EINEM Zufallsgenerator mit festem Startwert.
/// Gleicher Startwert + gleiche Eingaben = exakt gleicher Spielverlauf.
/// Das ist die Voraussetzung für Multiplayer und für reproduzierbare Tests.
/// </summary>
public sealed class Stadtwirtschaft
{
    private readonly List<Betrieb> _betriebe = new();
    private readonly List<Konto> _konten = new();
    private readonly Random _zufall;

    public Wirtschaftsdaten Daten { get; }
    public Markt Markt { get; }
    public Fronstatut Statut { get; private set; }
    public IReadOnlyList<Betrieb> Betriebe => _betriebe;
    public IReadOnlyList<Konto> Konten => _konten;

    /// <summary>Meldungen für den Spieler (Ausbrüche, Entlassungen …).</summary>
    public event Action<string>? Meldung;

    /// <summary>Arbeitsbeginn in Minuten nach Mitternacht (08:00).</summary>
    public const int Arbeitsbeginn = 8 * 60;

    public Stadtwirtschaft(Wirtschaftsdaten daten, Fronstatut statut, int zufallsstartwert)
    {
        Daten = daten;
        Statut = statut;
        Markt = new Markt(daten);
        _zufall = new Random(zufallsstartwert);
    }

    /// <summary>Legt ein Konto mit dem Startvermögen aus den Daten an.</summary>
    public Konto KontoEroeffnen(string inhaber, int startjahr = Spielzeit.StartJahr)
    {
        var konto = new Konto(inhaber, Daten.Allgemein.Startvermoegen, startjahr);
        _konten.Add(konto);
        return konto;
    }

    public Betrieb BetriebEroeffnen(string name, Betriebsart art, Konto konto)
    {
        if (!_konten.Contains(konto))
            throw new ArgumentException("Das Konto gehört nicht zu dieser Stadtwirtschaft.", nameof(konto));
        var betrieb = new Betrieb(name, art, konto, Daten);
        _betriebe.Add(betrieb);
        return betrieb;
    }

    /// <summary>Ist zu dieser Uhrzeit Arbeitszeit?</summary>
    public bool IstArbeitszeit(Spielzeit zeit)
    {
        var uhr = zeit.UhrzeitInMinuten;
        return uhr >= Arbeitsbeginn && uhr < Arbeitsbeginn + Daten.Allgemein.Arbeitsminuten;
    }

    /// <summary>Eine Spielminute. Wird von der Simulation VOR dem Weiterstellen der Uhr aufgerufen.</summary>
    internal void Tick(Spielzeit zeit)
    {
        if (zeit.MinuteImTag % 60 == 0 && zeit.VergangeneMinuten > 0)
            Markt.NeueStunde();
        else if (zeit.VergangeneMinuten > 0)
            Markt.NeueMinute();

        if (IstArbeitszeit(zeit))
            foreach (var betrieb in _betriebe)
                betrieb.Arbeitsminute(Markt);
    }

    /// <summary>Jahresende für alle Betriebe und Konten.</summary>
    internal void Jahresabrechnung(int neuesJahr)
    {
        foreach (var betrieb in _betriebe)
            betrieb.Jahresabrechnung(_zufall, m => Meldung?.Invoke(m));
        foreach (var konto in _konten)
            konto.JahrAbschliessen(neuesJahr);
    }

    /// <summary>
    /// Der Stadtrat ändert das Fron- und Sklavenstatut. Arbeitskräfte, die nun verboten sind,
    /// werden sofort entlassen (später: Verstoß → Anklage vor Gericht).
    /// </summary>
    public void StatutAendern(Fronstatut neu)
    {
        if (neu == Statut) return;
        Statut = neu;
        Meldung?.Invoke($"Der Rat hat ein neues Statut beschlossen: {neu.Kuerzel()} – {neu.Name()}.");
        foreach (var betrieb in _betriebe)
            foreach (var (kraft, anzahl) in betrieb.UnerlaubteEntlassen(neu))
                Meldung?.Invoke($"{betrieb.Name}: {anzahl}× {kraft} musste unter dem neuen Statut entlassen werden.");
    }

    /// <summary>Hilfsfunktion: Einstellen unter dem aktuell geltenden Statut.</summary>
    public void Einstellen(Betrieb betrieb, string arbeitskraft, int anzahl) =>
        betrieb.Einstellen(arbeitskraft, anzahl, Statut);

    public double HoechstesVermoegen => _konten.Count == 0 ? 0 : _konten.Max(k => k.Vermoegen);
}
