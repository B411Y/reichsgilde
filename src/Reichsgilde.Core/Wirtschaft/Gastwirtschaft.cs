using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Wirtschaft;

// ─────────────────────────────────────────────────────────────────────────────
//  Daten (Abschnitt „gastwirtschaft" in wirtschaft.json, ab Datenversion 16)
// ─────────────────────────────────────────────────────────────────────────────

public sealed class DunkleEckeDaten
{
    /// <summary>Chance je unbekannter Spur eines anderen Hauses, sie am Gerichtstag als Gerücht zu hören.</summary>
    public double Chance { get; init; } = 0.2;
}

public sealed class WuerfeltischDaten
{
    public int AbStufe { get; init; } = 2;

    /// <summary>Was das Haus je bewirtetem Gast am Würfeltisch einnimmt.</summary>
    public double EinsatzJeGast { get; init; } = 4;

    /// <summary>So viel Ansehen kostet ein Würfeltisch, solange er steht.</summary>
    public double Ansehen { get; init; } = 2;
}

public sealed class StammtischDaten
{
    public int AbStufe { get; init; } = 3;

    /// <summary>Gunst bei jedem Ratsherrn am Jahresende – ohne Geschenk, also ohne Bestechungsspur.</summary>
    public double Gunst { get; init; } = 4;
}

public sealed class BardeDaten
{
    public double Kosten { get; init; } = 120;

    /// <summary>So viel mehr Gäste kommen am Abend (0,5 = +50 %).</summary>
    public double Gaeste { get; init; } = 0.5;

    /// <summary>So viel mehr zahlt jeder Gast am Abend (gute Laune).</summary>
    public double Zeche { get; init; } = 0.1;

    /// <summary>Faktor auf die Stammtisch-Gunst in dem Jahr.</summary>
    public double Stammtisch { get; init; } = 2;
}

/// <summary>
/// Gäste, Ausschank und die Extras der Wirtshäuser. Gäste kehren von <see cref="OeffnetUm"/> bis zum Feierabend des Wirtshauses (22 Uhr) ein,
/// abends doppelt so viele; sie verteilen sich nach der Anziehung der Schänken (Stufe, Ansehen, Barde).
/// </summary>
public sealed class GastwirtschaftDaten
{
    /// <summary>Wessen Werkstätten Gäste bewirten.</summary>
    public string Beruf { get; init; } = "Wirt";

    /// <summary>Was ausgeschenkt wird (Waren der Schänke, die Gäste bestellen).</summary>
    public List<string> Ausschank { get; init; } = new();

    /// <summary>Ausschankpreis = Grundpreis × Aufschlag.</summary>
    public double Aufschlag { get; init; } = 1.25;

    /// <summary>Gäste in der ganzen Stadt je Jahr (ohne Barden).</summary>
    public double GaesteJeJahr { get; init; } = 120;

    public int OeffnetUm { get; init; } = 12;
    public int AbendAb { get; init; } = 18;
    public double AbendGewicht { get; init; } = 2;

    /// <summary>So viel je Ausschankware bleibt für die Gäste im Lager; nur der Rest fährt zum Markt.</summary>
    public int Vorrat { get; init; } = 15;

    public List<double> AnziehungJeStufe { get; init; } = new() { 1, 1.5, 2 };

    /// <summary>Anziehung × (1 + Ansehen × dieser Wert): Bei 40 Ansehen kommen 50 % mehr.</summary>
    public double AnziehungJeAnsehen { get; init; } = 0.0125;

    public DunkleEckeDaten DunkleEcke { get; init; } = new();
    public WuerfeltischDaten Wuerfeltisch { get; init; } = new();
    public StammtischDaten Stammtisch { get; init; } = new();
    public BardeDaten Barde { get; init; } = new();

    public void Pruefen(List<string> f, IReadOnlyDictionary<string, WareDaten> waren, IReadOnlyDictionary<string, BerufDaten> berufe)
    {
        if (berufe.Count > 0 && !berufe.ContainsKey(Beruf)) f.Add($"gastwirtschaft.beruf: Den Beruf „{Beruf}“ gibt es nicht.");
        foreach (var w in Ausschank.Where(w => !waren.ContainsKey(w)))
            f.Add($"gastwirtschaft.ausschank: Die Ware „{w}“ gibt es nicht.");
        if (Aufschlag <= 0 || GaesteJeJahr < 0 || Vorrat < 0) f.Add("gastwirtschaft: aufschlag > 0, gaesteJeJahr und vorrat ≥ 0.");
        var erste = Spielzeit.TagesbeginnMinute / 60;
        var letzte = Spielzeit.TagesendeMinute / 60 - 1;
        if (OeffnetUm < erste || OeffnetUm > letzte || AbendAb < OeffnetUm || AbendAb > letzte)
            f.Add($"gastwirtschaft: oeffnetUm zwischen {erste} und {letzte} (gespielte Stunden), abendAb danach.");
        if (AnziehungJeStufe.Count == 0 || AnziehungJeStufe.Any(a => a <= 0)) f.Add("gastwirtschaft.anziehungJeStufe: Werte größer als 0.");
        if (DunkleEcke.Chance is < 0 or > 1) f.Add("gastwirtschaft.dunkleEcke.chance muss zwischen 0 und 1 liegen.");
        if (Barde.Kosten < 0 || Barde.Gaeste < 0 || Barde.Zeche < 0 || Barde.Stammtisch < 0) f.Add("gastwirtschaft.barde: Werte dürfen nicht negativ sein.");
    }
}

/// <summary>Was eine Schänke in einem Jahr bewirtet hat.</summary>
public sealed record Gaestebuch(int Bewirtet, int Abgewiesen, double Einnahmen, double Wuerfelgeld);

// ─────────────────────────────────────────────────────────────────────────────
//  Teilsystem
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Die Gastwirtschaft der Stadt: Jede volle Stunde kehren Gäste in die Schänken ein und bestellen, was im Lager ist –
/// zum Ausschankpreis, ohne Karren und Markt. Dazu:
///   • Barde (Befehl, einmal je Jahr): abends mehr Gäste, höhere Zeche, doppelte Stammtisch-Gunst,
///   • Würfeltisch (ab Stufe 2): Einnahmen je Gast, kostet Ansehen,
///   • Stammtisch (ab Stufe 3): am Jahresende Gunst bei allen Ratsherren – ohne Bestechung,
///   • dunkle Ecke: am Gerichtstag Gerüchte über andere Häuser (siehe <see cref="Justiz.Stadtgericht"/>).
/// Die Gäste kommen ohne Zufall: Bruchteile werden je Schänke gesammelt, bis ein ganzer Gast beisammen ist.
/// </summary>
public sealed class Gastwirtschaft
{
    private readonly Stadtwirtschaft _w;
    private readonly GastwirtschaftDaten? _d;
    private readonly Dictionary<string, double> _offen = new();          // angefangene Gäste je Schänke
    private readonly Dictionary<string, Gaestebuch> _diesesJahr = new();
    private readonly Dictionary<string, Gaestebuch> _vorjahr = new();
    private readonly HashSet<string> _barde = new();                     // Barde gebucht (dieses Jahr)
    private readonly HashSet<string> _wuerfeltisch = new();

    public Gastwirtschaft(Stadtwirtschaft w, GastwirtschaftDaten? daten)
    {
        _w = w;
        _d = daten;
    }

    public bool IstAktiv => _d is not null;
    public GastwirtschaftDaten Daten => _d ?? throw new InvalidOperationException("Es gibt keine Gastwirtschaft (gastwirtschaft fehlt in den Daten).");

    /// <summary>Alle Schänken, in die heute Gäste einkehren können: Haus im Spiel, jemand schenkt aus.</summary>
    public IEnumerable<Betrieb> Schaenken => _w.Betriebe.Where(b => b.IstSchaenke && b.Konto.IstAktiv && b.Belegt > 0);

    public Gaestebuch DiesesJahr(Betrieb b) => _diesesJahr.GetValueOrDefault(b.Name, new Gaestebuch(0, 0, 0, 0));
    public Gaestebuch? Vorjahr(Betrieb b) => _vorjahr.GetValueOrDefault(b.Name);
    public bool BardeGebucht(Betrieb b) => _barde.Contains(b.Name);
    public bool HatWuerfeltisch(Betrieb b) => _wuerfeltisch.Contains(b.Name);
    public bool HatStammtisch(Betrieb b) => IstAktiv && b.IstSchaenke && b.Stufe >= Daten.Stammtisch.AbStufe;

    /// <summary>Was ein Gast für diese Ware zahlt (ohne Barden-Aufschlag).</summary>
    public double Ausschankpreis(string ware) => _w.Daten.Waren[ware].Grundpreis * Daten.Aufschlag;

    /// <summary>Wie gern Gäste hierher kommen: Stufe × Ansehen des Hauses (ohne Barde).</summary>
    public double Anziehung(Betrieb b)
    {
        var d = Daten;
        var stufe = d.AnziehungJeStufe[Math.Clamp(b.Stufe - 1, 0, d.AnziehungJeStufe.Count - 1)];
        return stufe * (1 + Math.Max(0, _w.Rat.Ansehen(b.Konto)) * d.AnziehungJeAnsehen);
    }

    /// <summary>Gewicht einer Stunde (Uhrzeit 0–23): geschlossen 0, nachmittags 1, abends <see cref="GastwirtschaftDaten.AbendGewicht"/>.</summary>
    private double Gewicht(int stunde) =>
        stunde < Daten.OeffnetUm || stunde >= SchliesstUm ? 0 : stunde >= Daten.AbendAb ? Daten.AbendGewicht : 1;

    /// <summary>
    /// Ab dieser Uhrzeit kehrt niemand mehr ein: 22 Uhr, der Feierabend der Wirtshäuser (8–22 Uhr). Bis Datenversion 55 endete hier der
    /// Spieltag; seit er bis 23 Uhr geht, bleibt die letzte Stunde ohne Gäste – Gäste und Abendanteil des Jahres bleiben dieselben.
    /// </summary>
    public const int SchliesstUm = 22;

    private bool IstAbend(int stunde) => stunde >= Daten.AbendAb;

    /// <summary>Summe der Gewichte über alle gespielten Stunden (04–23 Uhr).</summary>
    private static IEnumerable<int> Spielstunden => Enumerable.Range(Spielzeit.TagesbeginnMinute / 60, Spielzeit.StundenProTag);

    private double GewichtSumme => Spielstunden.Sum(Gewicht);

    /// <summary>Erwartete Gäste im Jahr für diese Schänke bei der heutigen Konkurrenz (ohne Barde).</summary>
    public double ErwarteteGaeste(Betrieb b)
    {
        var alle = Schaenken.Append(b).Distinct().Sum(Anziehung);
        return alle <= 0 ? 0 : Daten.GaesteJeJahr * Anziehung(b) / alle;
    }

    /// <summary>Was ein Barde dieser Schänke im Jahr ungefähr mehr einbringt (Gäste am Abend × Zeche) – für Spieler und KI.</summary>
    public double BardeNutzen(Betrieb b)
    {
        var d = Daten;
        var abendAnteil = Spielstunden.Where(s => s >= d.AbendAb).Sum(Gewicht) / GewichtSumme;
        var abendGaeste = ErwarteteGaeste(b) * abendAnteil;
        var zeche = d.Ausschank.Where(b.Artdaten.Produkte.Contains).Select(Ausschankpreis).DefaultIfEmpty(0).Average();
        return abendGaeste * d.Barde.Gaeste * zeche + abendGaeste * (1 + d.Barde.Gaeste) * zeche * d.Barde.Zeche
               - abendGaeste * d.Barde.Gaeste * zeche / d.Aufschlag; // die Zutaten der zusätzlichen Gäste
    }

    /// <summary>Ansehensverlust eines Hauses durch Würfeltische.</summary>
    public double AnsehenAbzug(Konto konto) =>
        IstAktiv && _w.Betriebe.Any(b => b.Konto == konto && _wuerfeltisch.Contains(b.Name)) ? Daten.Wuerfeltisch.Ansehen : 0;

    // ── Abfragen für die Darstellung (nur lesend, ohne Zufall) ───────────────

    /// <summary>So viele Gäste zeigt der Raum höchstens, wenn die Darstellung nichts anderes vorgibt.</summary>
    public const int SitzplaetzeImRaum = 12;

    private int StundeJetzt => _w.Uhr.UhrzeitInMinuten / 60;

    /// <summary>Geöffnet und etwas zum Ausschenken da: Schänke besetzt, Haus im Spiel, ab <see cref="GastwirtschaftDaten.OeffnetUm"/>.</summary>
    private bool SchenktAus(Betrieb b, int stunde) =>
        IstAktiv && Gewicht(stunde) > 0 && Schaenken.Contains(b) && Daten.Ausschank.Any(x => b.ImLager(x) > 0);

    /// <summary>
    /// Wie viele Gäste gerade in dieser Schänke sitzen – für die Darstellung, abgeleitet aus dem Gästemodell:
    /// Ein Gast bleibt etwa eine Stunde, also sitzen so viele, wie in dieser Stunde kommen:
    /// <see cref="ErwarteteGaeste"/> × Gewicht(Stunde) ÷ ΣGewicht, abends mit Barde × (1 + barde.gaeste),
    /// gerundet, mindestens 1 und höchstens <paramref name="hoechstens"/>.
    /// 0 vor <see cref="GastwirtschaftDaten.OeffnetUm"/>, ohne Arbeiter, wenn das Haus nicht mehr spielt
    /// und wenn keine Ausschankware im Lager ist (dann würde jeder Gast abgewiesen).
    /// </summary>
    public int GaesteJetzt(Betrieb b, int hoechstens = SitzplaetzeImRaum)
    {
        var stunde = StundeJetzt;
        if (!SchenktAus(b, stunde)) return 0;
        var gaeste = ErwarteteGaeste(b) * Gewicht(stunde) / GewichtSumme;
        if (_barde.Contains(b.Name) && IstAbend(stunde)) gaeste *= 1 + Daten.Barde.Gaeste;
        return Math.Clamp((int)Math.Round(gaeste, MidpointRounding.AwayFromZero), 1, Math.Max(1, hoechstens));
    }

    /// <summary>Der Barde spielt gerade: gebucht, ab <see cref="GastwirtschaftDaten.AbendAb"/>, und die Schänke ist besetzt.</summary>
    public bool BardeSpielt(Betrieb b) =>
        IstAktiv && _barde.Contains(b.Name) && IstAbend(StundeJetzt) && Schaenken.Contains(b);

    /// <summary>Am Würfeltisch wird gerade gespielt: aufgestellt (ab Stufe 2) und mindestens zwei Gäste da.</summary>
    public bool WuerfeltischBesetzt(Betrieb b) => HatWuerfeltisch(b) && GaesteJetzt(b) >= 2;

    /// <summary>Am Stammtisch sitzen gerade die Ratsherren: ab Stufe 3, abends, wenn ausgeschenkt wird.</summary>
    public bool StammtischBesetzt(Betrieb b) => HatStammtisch(b) && IstAbend(StundeJetzt) && SchenktAus(b, StundeJetzt);

    // ── Ablauf ──────────────────────────────────────────────────────────────

    /// <summary>Jede volle Stunde: Gäste der vergangenen Stunde kehren ein.</summary>
    internal void Stunde(Spielzeit zeit)
    {
        if (!IstAktiv) return;
        var d = Daten;
        var stunde = zeit.StundeDieEndete;
        var gewicht = Gewicht(stunde);
        if (gewicht <= 0) return;
        var schaenken = Schaenken.ToList();
        var summe = schaenken.Sum(Anziehung);
        if (summe <= 0) return;
        var gaeste = d.GaesteJeJahr * gewicht / GewichtSumme * _w.Ratsantraege.Gaestefaktor; // im Festjahr kommen mehr (PR #200)
        // Bürger-KI: Wie viele Einwohner diese Stunde wirklich eingekehrt sind, verschiebt die Gäste (Faktor um 1).
        // Ohne Kopplung (Abschnitt „buerger" oder „kopplung.schaenke" fehlt) genau wie bisher: gaeste × Anziehung ÷ Summe.
        var anteile = _w.Einwohnerschaft.Gaesteanteile(schaenken, gaeste, Anziehung, stunde);
        foreach (var b in schaenken)
        {
            var anteil = anteile[b];
            var barde = _barde.Contains(b.Name) && IstAbend(stunde);
            if (barde) anteil *= 1 + d.Barde.Gaeste;
            var offen = _offen.GetValueOrDefault(b.Name) + anteil;
            while (offen >= 1)
            {
                offen -= 1;
                Bewirten(b, barde);
            }
            _offen[b.Name] = offen;
        }
    }

    private void Bewirten(Betrieb b, bool barde)
    {
        var d = Daten;
        var buch = DiesesJahr(b);
        // Der Gast bestellt, wovon am meisten da ist.
        var ware = d.Ausschank.Where(x => b.ImLager(x) > 0).OrderByDescending(b.ImLager).FirstOrDefault();
        if (ware is null)
        {
            _diesesJahr[b.Name] = buch with { Abgewiesen = buch.Abgewiesen + 1 };
            return;
        }
        b.Auslagern(ware, 1);
        var preis = Ausschankpreis(ware) * (barde ? 1 + d.Barde.Zeche : 1);
        b.UmsatzBuchen(preis);
        b.SteuerBuchen(preis * _w.Umsatzsteuer);
        var wuerfel = 0.0;
        if (_wuerfeltisch.Contains(b.Name))
        {
            wuerfel = d.Wuerfeltisch.EinsatzJeGast;
            b.UmsatzBuchen(wuerfel);
        }
        _diesesJahr[b.Name] = buch with { Bewirtet = buch.Bewirtet + 1, Einnahmen = buch.Einnahmen + preis, Wuerfelgeld = buch.Wuerfelgeld + wuerfel };
    }

    /// <summary>
    /// Jahresende (vor der Ratssitzung): Stammtisch – die Ratsherren sitzen im Gasthaus und mögen den Wirt.
    /// Danach wird das Gästebuch zum Vorjahr, und die Barden ziehen weiter.
    /// </summary>
    internal void Jahresende()
    {
        if (!IstAktiv) return;
        var d = Daten;
        if (_w.Rat.IstAktiv)
            foreach (var konto in _w.Konten.Where(k => k.IstAktiv))
            {
                var gasthaus = _w.Betriebe.Where(b => b.Konto == konto && HatStammtisch(b) && b.Belegt > 0).OrderBy(b => b.Name, StringComparer.Ordinal).FirstOrDefault();
                if (gasthaus is null) continue;
                var mitBarde = _w.Betriebe.Any(b => b.Konto == konto && HatStammtisch(b) && _barde.Contains(b.Name));
                var gunst = d.Stammtisch.Gunst * (mitBarde ? d.Barde.Stammtisch : 1);
                foreach (var ratsherr in _w.Rat.Ratsherren)
                    _w.Rat.GunstHinzufuegen(ratsherr, konto, gunst);
                _w.Melden($"Am Stammtisch von {gasthaus.Name} sitzen die Ratsherren{(mitBarde ? " – der Barde spielt auf" : "")}: " +
                          $"{konto.Inhaber} gewinnt bei jedem {Zahlenformat.Ganz(gunst)} Gunst.");
            }
        _vorjahr.Clear();
        foreach (var (name, buch) in _diesesJahr) _vorjahr[name] = buch;
        _diesesJahr.Clear();
        _barde.Clear();
    }

    /// <summary>Die Schänke wurde verkauft: Barde, Würfeltisch, angefangene Gäste und Gästebücher gehen nicht auf den nächsten Besitzer über.</summary>
    internal void BetriebAufgegeben(Betrieb b)
    {
        _offen.Remove(b.Name);
        _diesesJahr.Remove(b.Name);
        _vorjahr.Remove(b.Name);
        _barde.Remove(b.Name);
        _wuerfeltisch.Remove(b.Name);
    }

    /// <summary>Häuser mit dunkler Ecke (eine besetzte Schänke) – je Haus die erste Schänke, für den Gerichtstag.</summary>
    internal IEnumerable<(Konto Konto, Betrieb Schaenke)> DunkleEcken() =>
        IstAktiv
            ? Schaenken.GroupBy(b => b.Konto).Select(g => (g.Key, g.OrderBy(b => b.Name, StringComparer.Ordinal).First()))
            : Enumerable.Empty<(Konto, Betrieb)>();

    // ── Befehle ─────────────────────────────────────────────────────────────

    internal Befehlsergebnis BardeEinladen(Betrieb b)
    {
        if (!IstAktiv || !b.IstSchaenke) return Befehlsergebnis.Abgelehnt($"In {b.Name} spielt kein Barde – das ist keine Schänke.");
        if (_barde.Contains(b.Name)) return Befehlsergebnis.Abgelehnt($"Für {b.Name} ist dieses Jahr schon ein Barde gebucht.");
        var kosten = Daten.Barde.Kosten;
        var waehrung = _w.Daten.Allgemein.Waehrung;
        if (b.Konto.Vermoegen < kosten) return Befehlsergebnis.Abgelehnt($"Ein Barde will {Zahlenformat.Ganz(kosten)} {waehrung}.");
        b.PersonalBuchen(kosten);
        _barde.Add(b.Name);
        return Befehlsergebnis.Ok($"Ein Barde spielt heute Abend in {b.Name} ({Zahlenformat.Ganz(kosten)} {waehrung}): " +
                                  $"ab {Daten.AbendAb} Uhr {Zahlenformat.Ganz(Daten.Barde.Gaeste * 100)} % mehr Gäste, " +
                                  $"jeder zahlt {Zahlenformat.Ganz(Daten.Barde.Zeche * 100)} % mehr.");
    }

    internal Befehlsergebnis WuerfeltischAufstellen(Betrieb b, bool aufstellen)
    {
        if (!IstAktiv || !b.IstSchaenke) return Befehlsergebnis.Abgelehnt($"{b.Name} ist keine Schänke.");
        var w = Daten.Wuerfeltisch;
        if (aufstellen && b.Stufe < w.AbStufe)
            return Befehlsergebnis.Abgelehnt($"Einen Würfeltisch gibt es erst ab Stufe {w.AbStufe} (Wirtshaus).");
        if (aufstellen == _wuerfeltisch.Contains(b.Name))
            return Befehlsergebnis.Ok($"{b.Name} hat {(aufstellen ? "schon einen" : "keinen")} Würfeltisch.");
        if (aufstellen) _wuerfeltisch.Add(b.Name); else _wuerfeltisch.Remove(b.Name);
        return Befehlsergebnis.Ok(aufstellen
            ? $"In {b.Name} steht nun ein Würfeltisch: {Zahlenformat.Ganz(w.EinsatzJeGast)} {_w.Daten.Allgemein.Waehrung} je Gast fürs Haus – " +
              $"aber {Zahlenformat.Ganz(w.Ansehen)} Ansehen weniger, solange er steht."
            : $"Der Würfeltisch in {b.Name} ist abgebaut.");
    }

    // ── Speichern ───────────────────────────────────────────────────────────

    internal GastwirtschaftStand Sichern() => new()
    {
        Offen = _offen.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Value),
        DiesesJahr = _diesesJahr.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Value),
        Vorjahr = _vorjahr.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Value),
        Barde = _barde.OrderBy(x => x, StringComparer.Ordinal).ToList(),
        Wuerfeltisch = _wuerfeltisch.OrderBy(x => x, StringComparer.Ordinal).ToList(),
    };

    internal void Wiederherstellen(GastwirtschaftStand s)
    {
        foreach (var (k, v) in s.Offen) _offen[k] = v;
        foreach (var (k, v) in s.DiesesJahr) _diesesJahr[k] = v;
        foreach (var (k, v) in s.Vorjahr) _vorjahr[k] = v;
        _barde.UnionWith(s.Barde);
        _wuerfeltisch.UnionWith(s.Wuerfeltisch);
    }
}
