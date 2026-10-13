using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Die Wirtschaft der Reichsstadt: Markt, Betriebe, Konten und das geltende Fronstatut.
///
/// Ablauf:
///   • jede Spielminute während der Arbeitszeit (Standard 08:00–20:00) arbeiten alle Betriebe,
///   • jede Spielminute fahren die Karren (auch nachts – ins Umland aber nur, wenn erlaubt),
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
    private readonly Random _reihenfolge;
    private readonly List<Karren> _alleKarren = new();
    private readonly List<KiRivale> _rivalen = new();

    /// <summary>Der eine Zufallsgenerator der Wirtschaft (nur intern, damit niemand die Reihenfolge stört).</summary>
    internal Random Zufall => _zufall;

    internal void Melden(string meldung) => Meldung?.Invoke(meldung);

    public Wirtschaftsdaten Daten { get; }
    public Markt Markt { get; }
    public Fronstatut Statut { get; private set; }
    public IReadOnlyList<Betrieb> Betriebe => _betriebe;
    public IReadOnlyList<Konto> Konten => _konten;

    /// <summary>Konten, die von der KI geführt werden.</summary>
    public IReadOnlyList<KiRivale> Rivalen => _rivalen;

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
        // Eigener Generator nur für die Reihenfolge – so bleiben die Ereignis-Würfel unabhängig
        // davon, wie viele Karren es gibt.
        _reihenfolge = new Random(unchecked(zufallsstartwert * 31 + 7));
    }

    /// <summary>Legt ein Konto mit dem Startvermögen aus den Daten an.</summary>
    public Konto KontoEroeffnen(string inhaber, int startjahr = Spielzeit.StartJahr)
    {
        var konto = new Konto(inhaber, Daten.Allgemein.Startvermoegen, startjahr);
        _konten.Add(konto);
        return konto;
    }

    /// <summary>
    /// Eröffnet einen Betrieb mit Startausstattung: Eine Gießerei bekommt einen Handkarren,
    /// eine Mine einen Ochsenkarren (mit einem Handkarren käme sie mit dem Abtransport nicht nach).
    /// </summary>
    /// <param name="startkarren">Anderer Karrentyp als Startausstattung oder "" für keinen.</param>
    public Betrieb BetriebEroeffnen(string name, Betriebsart art, Konto konto, string? startkarren = null)
    {
        if (!_konten.Contains(konto))
            throw new ArgumentException("Das Konto gehört nicht zu dieser Stadtwirtschaft.", nameof(konto));
        var betrieb = new Betrieb(name, art, konto, Daten);
        _betriebe.Add(betrieb);

        startkarren ??= art == Betriebsart.Mine ? "Ochsenkarren" : "Handkarren";
        if (startkarren != "")
            betrieb.KarrenHinzufuegen(startkarren);
        return betrieb;
    }

    /// <summary>
    /// Übergibt ein Konto samt seinen Betrieben an die KI. Sie trifft sofort erste Entscheidungen
    /// und danach jede volle Stunde sowie zum Jahresende.
    /// </summary>
    public KiRivale KiAnmelden(Konto konto, KiProfil? profil = null)
    {
        if (!_konten.Contains(konto))
            throw new ArgumentException("Das Konto gehört nicht zu dieser Stadtwirtschaft.", nameof(konto));
        if (_rivalen.Any(r => r.Konto == konto))
            throw new InvalidOperationException($"{konto.Inhaber} wird bereits von der KI geführt.");
        var rivale = new KiRivale(konto, profil ?? KiProfil.Standard, this);
        _rivalen.Add(rivale);
        rivale.Stunde();
        return rivale;
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

        // Die KI überlegt jede volle Stunde – mit den Marktpreisen dieser Stunde.
        if (zeit.MinuteImTag % 60 == 0 && zeit.VergangeneMinuten > 0)
            foreach (var rivale in _rivalen)
                rivale.Stunde();

        if (IstArbeitszeit(zeit))
            foreach (var betrieb in _betriebe)
                betrieb.Arbeitsminute(Melden);

        // Fairness: Wer in derselben Minute am Markt ankommt, ist in zufälliger Reihenfolge dran.
        // Sonst hätte immer der zuerst eröffnete Betrieb den besseren Preis (im Multiplayer: wer zuerst beitritt).
        _alleKarren.Clear();
        foreach (var betrieb in _betriebe)
            _alleKarren.AddRange(betrieb.Karren);
        for (var i = _alleKarren.Count - 1; i > 0; i--)
        {
            var j = _reihenfolge.Next(i + 1);
            (_alleKarren[i], _alleKarren[j]) = (_alleKarren[j], _alleKarren[i]);
        }
        foreach (var karren in _alleKarren)
            karren.Tick(zeit, this);
    }

    /// <summary>Jahresende für alle Betriebe und Konten.</summary>
    internal void Jahresabrechnung(int neuesJahr)
    {
        foreach (var betrieb in _betriebe)
            betrieb.Jahresabrechnung(_zufall, m => Meldung?.Invoke(m));
        foreach (var konto in _konten)
            konto.JahrAbschliessen(neuesJahr);
        foreach (var rivale in _rivalen)
            rivale.Jahresende();
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
