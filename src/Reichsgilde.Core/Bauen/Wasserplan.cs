using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Übergänge über das Wasser: Brücken und Furten entstehen von selbst, Fähren stehen in der Karte (Kartenwerkstatt, Schritt 5c)
//
//  Wo ein Weg einen Fluss oder Bach quert, entsteht automatisch ein Übergang – gesucht wird entlang des Wegverlaufs alle 0,5 m, ob der Ort
//  im Wasser liegt; Anfang und Ende jeder Strecke im Wasser werden auf wenige Zentimeter genau eingegrenzt:
//    • eine Furt, wenn der Fluss dort flacher als 0,5 m ist (Karren fahren hindurch, langsamer: logistik.wasser.furtMinuten),
//    • sonst eine Brücke: aus Holz bis 20 m Spannweite über dem Wasser, darüber aus Stein.
//  Gebaut wird sie wie die Modelle (game/assets/modelle/wasser/): Zwischen den beiden Uferkanten (wo das Ufer ins Gelände übergeht,
//  Wasserform.Uferbreite) liegt die Weite W. Holz: n = max(1, round(W / 4)) Joche, je W / n lang, der Holm jedes Jochs in seiner Mitte; an
//  jeder Uferkante ein Endstück, 4 m aufs Land. Stein: n = max(1, round((W + 2) / 10)) Bögen mit n − 1 Pfeilern zu 2 m dazwischen; an jeder
//  Uferkante eine Rampe, 10 m aufs Land. Alle Teile sitzen mit ihrer Höhe 0 auf der Uferhöhe (Mittel der Weghöhe an beiden Uferkanten),
//  das Deck liegt 0,6 m (Holz) bzw. 1,4 m (Stein) darüber. Zwischen Anfang und Ende (mit Endstücken bzw. Rampen) zeichnet die Darstellung
//  statt des Wegbands die Brücke.
//  Ein Weg durch einen See bekommt keinen Übergang: Dort ist er gesperrt (die Prüfung meldet einen Fehler, Karren fahren nicht hindurch).
//  Eine Fährstelle (uebergaenge mit art „faehre“) verbindet zwei Anleger an beiden Ufern; Karren warten dort (logistik.wasser.faehreMinuten).
//
//  Abfragen: Querungen (Brücken und Furten mit Lage, Länge, Art, Deckhöhe, Pfeilern), Faehren, Sperren (Wege im See). Gleiche Karte,
//  gleiche Übergänge – ohne Zufall.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Die Art eines Übergangs über das Wasser.</summary>
public enum Uebergangsart
{
    /// <summary>Brücke aus Holz (bis 20 m Spannweite): Joche alle 4 m, Endstücke, Geländer.</summary>
    Holzbruecke,

    /// <summary>Brücke aus Stein (über 20 m Spannweite): Bögen bis 8 m, Pfeiler, Rampen, Brüstung.</summary>
    Steinbruecke,

    /// <summary>Furt durch einen flachen Fluss (unter 0,5 m): Karren fahren hindurch, langsamer.</summary>
    Furt,

    /// <summary>Fähre zwischen zwei Anlegern: Karren warten.</summary>
    Faehre,
}

/// <summary>Ein Pfeiler (Joch) einer Brücke: Ort auf der Mittellinie des Weges und Höhe des Bodens dort (Fuß im Flussbett).</summary>
public readonly record struct Brueckenpfeiler(Kartenort Ort, double Fuss);

/// <summary>
/// Eine Brücke oder Furt, wo ein Weg ein fließendes Gewässer quert. <see cref="VonStelle"/> und <see cref="BisStelle"/> sind Meter entlang des
/// Wegverlaufs (dazwischen zeichnet die Darstellung statt des Wegbands die Brücke bzw. die Furtsteine); <see cref="Richtung"/> in Grad wie
/// Godot <c>Rotation.Y</c>, lokal +z zeigt von <see cref="Anfang"/> nach <see cref="Ende"/>.
/// </summary>
public sealed record Wasserquerung
{
    public Uebergangsart Art { get; init; }

    /// <summary>Welcher Weg (Index in der Liste der Wege der Karte) und sein Name.</summary>
    public int Weg { get; init; }

    public string WegName { get; init; } = "";

    /// <summary>Welches Gewässer (Nummer in der Liste <c>wasser</c>) und sein Name.</summary>
    public int Gewaesser { get; init; }

    public string GewaesserName { get; init; } = "";

    /// <summary>
    /// Anfang und Ende auf der Mittellinie des Weges: bei einer Brücke mit den Endstücken (Holz, 4 m) bzw. Rampen (Stein, 10 m) auf dem Land,
    /// bei einer Furt die Uferkanten.
    /// </summary>
    public Kartenort Anfang { get; init; }

    public Kartenort Ende { get; init; }

    /// <summary>Anfang und Ende in Metern entlang des Wegverlaufs (dazwischen kein Wegband).</summary>
    public double VonStelle { get; init; }

    public double BisStelle { get; init; }

    /// <summary>Die beiden Uferkanten auf der Mittellinie des Weges (wo das Ufer ins Gelände übergeht) – dort setzen Endstücke und Rampen an.</summary>
    public Kartenort UferA { get; init; }

    public Kartenort UferB { get; init; }

    /// <summary>Die Uferkanten in Metern entlang des Wegverlaufs.</summary>
    public double UferAStelle { get; init; }

    public double UferBStelle { get; init; }

    /// <summary>Die Weite zwischen den Uferkanten (Meter) – darüber spannen Joche bzw. Bögen und Pfeiler.</summary>
    public double Weite => UferBStelle - UferAStelle;

    /// <summary>Wie weit der Weg über Wasser führt (Meter) – entscheidet Holz (bis 20 m) oder Stein.</summary>
    public double Spannweite { get; init; }

    /// <summary>Die Breite des Weges (Meter).</summary>
    public double Breite { get; init; }

    /// <summary>Grad wie <c>Rotation.Y</c> (0 = von Norden nach Süden, 90 = von Westen nach Osten).</summary>
    public double Richtung { get; init; }

    /// <summary>Die Uferhöhe (Meter): Mittel der Weghöhe an beiden Uferkanten – darauf sitzen die Teile der Brücke mit ihrer Höhe 0.</summary>
    public double Uferhoehe { get; init; }

    /// <summary>Brücke: Höhe des Decks (Uferhöhe + 0,6 m Holz bzw. 1,4 m Stein). Furt: Höhe der Sohle in der Mitte.</summary>
    public double Deckhoehe { get; init; }

    /// <summary>Höhe des Weges (Längsprofil) an Anfang und Ende – dorthin führen Endstück bzw. Rampe hinab.</summary>
    public double AnfangHoehe { get; init; }

    public double EndeHoehe { get; init; }

    /// <summary>Spiegel und Tiefe des Gewässers in der Mitte des Übergangs (Meter).</summary>
    public double Spiegel { get; init; }

    public double Tiefe { get; init; }

    /// <summary>Brücke: Holz die Holme der Joche (je in der Mitte eines Jochs), Stein die Pfeiler zwischen den Bögen (2 m breit). Furt: keine.</summary>
    public IReadOnlyList<Brueckenpfeiler> Pfeiler { get; init; } = Array.Empty<Brueckenpfeiler>();

    /// <summary>Wie viele Joche (Holz) bzw. Bögen (Stein) die Brücke hat (Furt: 0).</summary>
    public int Felder { get; init; }

    /// <summary>Die Länge eines Jochs bzw. Bogens (Meter): Holz W / n (Modell 4 m), Stein (W − 2 (n − 1)) / n (Modell 8 m) – so weit strecken.</summary>
    public double Feldlaenge { get; init; }

    /// <summary>
    /// Quert der Weg das Gewässer wirklich (Anfang und Ende an verschiedenen Ufern)? Sonst streift er es nur oder läuft im Wasser entlang –
    /// die Prüfung meldet das.
    /// </summary>
    public bool Quert { get; init; }

    /// <summary>Ist der Weg eine Karrenstraße?</summary>
    public bool Karren { get; init; }

    public bool IstBruecke => Art is Uebergangsart.Holzbruecke or Uebergangsart.Steinbruecke;

    /// <summary>Die Länge über alles (Meter, mit Endstücken bzw. Rampen).</summary>
    public double Laenge => BisStelle - VonStelle;

    public Kartenort Mitte => new((Anfang.X + Ende.X) / 2, (Anfang.Z + Ende.Z) / 2);
}

/// <summary>
/// Eine Fährstelle der Karte mit dem, was das Wasser dazu sagt: welches Gewässer sie überquert (null: keins – ein Fehler), Länge, Spiegel,
/// Richtung (Grad wie <c>Rotation.Y</c>, lokal +z von <see cref="Von"/> nach <see cref="Bis"/>), ob ein Anleger im Wasser steht und wie weit jeder
/// vom Wasser entfernt ist.
/// </summary>
public sealed record Faehrverbindung
{
    /// <summary>Welcher Eintrag der Liste <c>uebergaenge</c> (0 …).</summary>
    public int Nummer { get; init; }

    public string Name { get; init; } = "";
    public Kartenort Von { get; init; }
    public Kartenort Bis { get; init; }

    /// <summary>Das Gewässer, über das die Fähre fährt (die längste Strecke über Wasser) – null, wenn sie über kein Wasser fährt.</summary>
    public string? Gewaesser { get; init; }

    public double Laenge { get; init; }

    /// <summary>Wie viel der Strecke über Wasser führt (Meter).</summary>
    public double UeberWasser { get; init; }

    public double Spiegel { get; init; }
    public double Richtung { get; init; }

    public bool VonImWasser { get; init; }
    public bool BisImWasser { get; init; }

    /// <summary>
    /// Wo die Strecke von <see cref="Von"/> bzw. <see cref="Bis"/> aus die Uferkante erreicht (dort beginnt das Ufer) – dort steht der Anlegesteg
    /// mit seiner Landseite; ohne Ufer auf der Strecke der Anleger selbst.
    /// </summary>
    public Kartenort VonUferkante { get; init; }

    public Kartenort BisUferkante { get; init; }

    /// <summary>Abstand der Anleger zum Wasser (Meter; <see cref="double.MaxValue"/>: kein Wasser in der Nähe).</summary>
    public double VonUfer { get; init; }

    public double BisUfer { get; init; }
}

/// <summary>Ein Stück Weg in einem See (gesperrt: kein Übergang).</summary>
public readonly record struct Wegsperre(int Weg, string WegName, string See, Kartenort Ort, double VonStelle, double BisStelle);

/// <summary>Ein Weg, wie der Wasserplan ihn braucht: Name, Verlauf, Breite, trägt er Karren.</summary>
public readonly record struct Planweg(string Name, IReadOnlyList<Kartenort> Verlauf, double Breite, bool Karren);

/// <summary>Brücken, Furten und Fähren einer Karte (siehe Kopf der Datei).</summary>
public sealed class Wasserplan
{
    /// <summary>Flacher als das (Meter) wird statt einer Brücke eine Furt.</summary>
    public const double FurtTiefe = 0.5;

    /// <summary>Bis zu dieser Spannweite (Meter) ist eine Brücke aus Holz, darüber aus Stein.</summary>
    public const double HolzBis = 20;

    /// <summary>So weit reicht eine Brücke hinter der Uferkante aufs Land (Meter): das Endstück aus Holz, die Rampe aus Stein (wie die Modelle).</summary>
    public const double HolzEnde = 4, SteinRampe = 10;

    /// <summary>Länge eines Jochs (Holz) und eines Bogens (Stein) im Modell, Breite eines Steinpfeilers (Meter).</summary>
    public const double HolzJoch = 4, SteinBogen = 8, SteinPfeiler = 2;

    /// <summary>So hoch liegt das Deck über der Uferhöhe (Meter), wie in den Modellen; die Fähre trägt ihr Deck 0,3 m über dem Wasser.</summary>
    public const double HolzDeck = 0.6, SteinDeck = 1.4, FaehreDeck = 0.3;

    /// <summary>Ab dieser Weite zwischen den Uferkanten (Meter) meldet die Prüfung eine Brücke als sehr lang (Hinweis).</summary>
    public const double LangeBruecke = 60;

    /// <summary>In diesen Schritten (Meter) wird ein Weg auf Wasser abgetastet.</summary>
    public const double Abtastung = 0.5;

    private readonly Gelaendeform _gelaende;
    private readonly IReadOnlyList<Planweg> _wege;
    private readonly IReadOnlyList<Uebergang> _uebergaenge;
    private (List<Wasserquerung> Querungen, List<Wegsperre> Sperren, List<Faehrverbindung> Faehren)? _stand;

    /// <param name="gelaende">Das Gelände mit seinem Wasser (für Spiegel, Boden und das Längsprofil der Wege).</param>
    /// <param name="wege">Alle Wege der Karte in ihrer Reihenfolge.</param>
    /// <param name="uebergaenge">Die Liste <c>uebergaenge</c> der Karte (Fährstellen; Einträge mit „bruecke“ zählen hier nicht).</param>
    public Wasserplan(Gelaendeform gelaende, IReadOnlyList<Planweg> wege, IReadOnlyList<Uebergang> uebergaenge) =>
        (_gelaende, _wege, _uebergaenge) = (gelaende, wege, uebergaenge);

    /// <summary>Der Wasserplan einer Stadtkarte.</summary>
    public static Wasserplan Von(Stadtkarte karte) => new(karte.Gelaendeform,
        karte.Wege.Select(w => new Planweg(w.Name, w.Linie.Count >= 2 ? w.Verlauf.Select(Kartenort.Aus).ToList() : new List<Kartenort>(), w.BreiteWirksam, w.IstKarrenstrasse)).ToList(),
        karte.Uebergaenge);

    private (List<Wasserquerung> Querungen, List<Wegsperre> Sperren, List<Faehrverbindung> Faehren) Stand => _stand ??= Rechnen();

    /// <summary>Alle Brücken und Furten, Weg für Weg in Richtung des Weges.</summary>
    public IReadOnlyList<Wasserquerung> Querungen => Stand.Querungen;

    /// <summary>Die Brücken.</summary>
    public IEnumerable<Wasserquerung> Bruecken => Querungen.Where(q => q.IstBruecke);

    /// <summary>Die Furten.</summary>
    public IEnumerable<Wasserquerung> Furten => Querungen.Where(q => q.Art == Uebergangsart.Furt);

    /// <summary>Wo Wege durch einen See laufen (gesperrt).</summary>
    public IReadOnlyList<Wegsperre> Sperren => Stand.Sperren;

    /// <summary>Die Fährstellen mit ihrem Gewässer.</summary>
    public IReadOnlyList<Faehrverbindung> Faehren => Stand.Faehren;

    /// <summary>Gibt es etwas, das Karren aufhält (eine Furt an einer Karrenstraße oder eine Fähre)?</summary>
    public bool HaeltAuf => Querungen.Any(q => q.Karren && q.Art == Uebergangsart.Furt) || Faehren.Count > 0;

    // ── Rechnen ──────────────────────────────────────────────────────────────

    private (List<Wasserquerung>, List<Wegsperre>, List<Faehrverbindung>) Rechnen()
    {
        var querungen = new List<Wasserquerung>();
        var sperren = new List<Wegsperre>();
        var faehren = new List<Faehrverbindung>();
        var wasser = _gelaende.Wasser;
        if (!wasser.IstLeer)
            for (var w = 0; w < _wege.Count; w++)
                WegRechnen(w, querungen, sperren);
        for (var n = 0; n < _uebergaenge.Count; n++)
            if (_uebergaenge[n] is { Art: Uebergang.Faehre } u && u.Von is { Length: >= 2 } && u.Bis is { Length: >= 2 })
                faehren.Add(FaehreRechnen(n, u));
        return (querungen, sperren, faehren);
    }

    /// <summary>Ein Punkt des Verlaufs an der Stelle (Meter ab dem Anfang).</summary>
    private static Kartenort OrtAn(IReadOnlyList<Kartenort> v, double[] stellen, double s)
    {
        if (s <= 0) return v[0];
        if (s >= stellen[^1]) return v[^1];
        var k = Array.BinarySearch(stellen, s);
        if (k >= 0) return v[k];
        k = ~k;
        var t = (s - stellen[k - 1]) / (stellen[k] - stellen[k - 1]);
        return new Kartenort(v[k - 1].X + (v[k].X - v[k - 1].X) * t, v[k - 1].Z + (v[k].Z - v[k - 1].Z) * t);
    }

    /// <summary>Welches Gewässer am Ort ist: (Nummer, ist es ein See) – null an Land.</summary>
    private (int Nummer, bool See)? Wasser(Kartenort p) => _gelaende.Wasser.An(p.X, p.Z) is { } w ? (w.Nummer, w.See is not null) : null;

    private void WegRechnen(int index, List<Wasserquerung> querungen, List<Wegsperre> sperren)
    {
        var weg = _wege[index];
        var v = weg.Verlauf;
        if (v.Count < 2) return;
        var stellen = new double[v.Count];
        for (var k = 1; k < v.Count; k++) stellen[k] = stellen[k - 1] + v[k - 1].Abstand(v[k]);
        var laenge = stellen[^1];
        if (laenge <= 0) return;
        var rahmen = Kartenrechteck.Um(v)!.Value.Vergroessert(1);
        if (!_gelaende.Wasser.Bereiche.Any(b => b.Ueberdeckt(rahmen))) return;

        // Abtasten: Strecken im Wasser desselben Gewässers
        var schritte = Math.Max(1, (int)Math.Ceiling(laenge / Abtastung));
        var laeufe = new List<(int Nummer, bool See, double Von, double Bis)>();
        (int Nummer, bool See)? vorher = null;
        var vorherStelle = 0.0;
        for (var i = 0; i <= schritte; i++)
        {
            var s = laenge * i / schritte;
            var hier = Wasser(OrtAn(v, stellen, s));
            if (hier != vorher)
            {
                // Grenze zwischen vorherStelle und s genau suchen
                var grenze = i == 0 ? 0 : Grenze(v, stellen, vorherStelle, s, vorher);
                if (vorher is not null) laeufe[^1] = laeufe[^1] with { Bis = grenze };
                if (hier is { } neu) laeufe.Add((neu.Nummer, neu.See, grenze, laenge));
            }
            (vorher, vorherStelle) = (hier, s);
        }

        foreach (var (nummer, see, von, bis) in laeufe)
        {
            if (bis - von < 1e-3) continue;
            var mitte = OrtAn(v, stellen, (von + bis) / 2);
            if (see)
            {
                var name = _gelaende.Wasser.Seen.FirstOrDefault(x => x.Nummer == nummer)?.Name ?? "";
                sperren.Add(new Wegsperre(index, weg.Name, name, mitte, von, bis));
                continue;
            }
            if (_gelaende.Wasser.Laeufe.FirstOrDefault(x => x.Nummer == nummer) is not { } lauf) continue;
            querungen.Add(Querung(index, weg, v, stellen, lauf, von, bis));
        }
    }

    /// <summary>Wo zwischen zwei Stellen das Wasser beginnt bzw. endet (Halbieren, auf 1 mm genau).</summary>
    private double Grenze(IReadOnlyList<Kartenort> v, double[] stellen, double a, double b, (int, bool)? beiA)
    {
        for (var n = 0; n < 20 && b - a > 0.001; n++)
        {
            var m = (a + b) / 2;
            if (Wasser(OrtAn(v, stellen, m)) == beiA) a = m;
            else b = m;
        }
        return (a + b) / 2;
    }

    private Wasserquerung Querung(int index, Planweg weg, IReadOnlyList<Kartenort> v, double[] stellen, Wasserlauf lauf, double von, double bis)
    {
        var laenge = stellen[^1];
        // Tiefe und Spiegel: die tiefste Stelle unter dem Weg (Mitte des Flusses)
        var (tiefe, spiegel) = (0.0, 0.0);
        var mitteOrt = OrtAn(v, stellen, (von + bis) / 2);
        for (var s = von; s <= bis + 1e-9; s += Math.Max(0.25, (bis - von) / 20))
            if (lauf.Naechste(OrtAn(v, stellen, s)) is ({ } k, { } t, _) && lauf.Bei(k, t) is var w && w.Tiefe > tiefe)
                (tiefe, spiegel) = (w.Tiefe, w.Spiegel);
        if (tiefe == 0 && lauf.Naechste(mitteOrt) is ({ } k0, { } t0, _)) (tiefe, spiegel) = (lauf.Bei(k0, t0).Tiefe, lauf.Bei(k0, t0).Spiegel);

        // Quert der Weg (Anfang und Ende an verschiedenen Ufern)?
        int Seite(double s)
        {
            var p = OrtAn(v, stellen, Math.Clamp(s, 0, laenge));
            if (lauf.Naechste(p) is not ({ } k, { } t, _)) return 0;
            var w = lauf.Bei(k, t);
            return Math.Sign(w.RichtungX * (p.Z - w.Ort.Z) - w.RichtungZ * (p.X - w.Ort.X));
        }
        var quert = Seite(von - 0.05) * Seite(bis + 0.05) < 0;

        var spannweite = bis - von;
        // (1 cm Spiel: die Grenzen sind auf 1 mm genau, eine 20 m breite Furt ist dann auch eine)
        var art = tiefe < FurtTiefe ? Uebergangsart.Furt : spannweite <= HolzBis + 0.01 ? Uebergangsart.Holzbruecke : Uebergangsart.Steinbruecke;
        // die Uferkanten: vom Wasser aus entlang des Weges, bis das Ufer ins Gelände übergeht
        double Uferkante(double imWasser, double richtung)
        {
            var aussen = imWasser;
            while (aussen > 0 && aussen < laenge && lauf.ImUfer(OrtAn(v, stellen, aussen))) aussen = Math.Clamp(aussen + richtung * Abtastung, 0, laenge);
            if (lauf.ImUfer(OrtAn(v, stellen, aussen))) return aussen; // der Weg endet im Ufer
            var (innen, rand) = (imWasser, aussen);
            for (var n = 0; n < 20 && Math.Abs(rand - innen) > 0.001; n++)
            {
                var m = (innen + rand) / 2;
                if (lauf.ImUfer(OrtAn(v, stellen, m))) innen = m;
                else rand = m;
            }
            return (innen + rand) / 2;
        }
        var (ua, ub) = (Uferkante(von, -1), Uferkante(bis, 1));
        var weite = ub - ua;
        var (a, b) = art switch
        {
            Uebergangsart.Furt => (ua, ub),
            Uebergangsart.Holzbruecke => (Math.Max(0, ua - HolzEnde), Math.Min(laenge, ub + HolzEnde)),
            _ => (Math.Max(0, ua - SteinRampe), Math.Min(laenge, ub + SteinRampe)),
        };
        var (anfang, ende) = (OrtAn(v, stellen, a), OrtAn(v, stellen, b));
        var uferhoehe = (ProfilHoehe(v, ua) + ProfilHoehe(v, ub)) / 2;
        var deck = art switch
        {
            Uebergangsart.Furt => spiegel - tiefe,
            Uebergangsart.Holzbruecke => uferhoehe + HolzDeck,
            _ => uferhoehe + SteinDeck,
        };
        // Holz: der Holm jedes Jochs in seiner Mitte; Stein: die Pfeiler zwischen den Bögen
        var pfeilerStellen = new List<double>();
        var (felder, feldlaenge) = (0, 0.0);
        if (art == Uebergangsart.Holzbruecke)
        {
            felder = Math.Max(1, (int)Math.Round(weite / HolzJoch, MidpointRounding.AwayFromZero));
            feldlaenge = weite / felder;
            for (var i = 0; i < felder; i++) pfeilerStellen.Add(ua + (i + 0.5) * feldlaenge);
        }
        else if (art == Uebergangsart.Steinbruecke)
        {
            felder = Math.Max(1, (int)Math.Round((weite + SteinPfeiler) / (SteinBogen + SteinPfeiler), MidpointRounding.AwayFromZero));
            feldlaenge = (weite - SteinPfeiler * (felder - 1)) / felder;
            for (var i = 1; i < felder; i++) pfeilerStellen.Add(ua + i * (feldlaenge + SteinPfeiler) - SteinPfeiler / 2);
        }
        var pfeiler = pfeilerStellen.Select(x => OrtAn(v, stellen, x)).Select(p => new Brueckenpfeiler(p, _gelaende.Hoehe(p.X, p.Z))).ToList();
        return new Wasserquerung
        {
            Art = art, Weg = index, WegName = weg.Name, Gewaesser = lauf.Nummer, GewaesserName = lauf.Name, Anfang = anfang, Ende = ende,
            VonStelle = a, BisStelle = b, UferA = OrtAn(v, stellen, ua), UferB = OrtAn(v, stellen, ub), UferAStelle = ua, UferBStelle = ub,
            Spannweite = spannweite, Breite = weg.Breite, Richtung = Richtung(anfang, ende), Uferhoehe = uferhoehe, Deckhoehe = deck,
            AnfangHoehe = ProfilHoehe(v, a), EndeHoehe = ProfilHoehe(v, b), Spiegel = spiegel, Tiefe = tiefe, Pfeiler = pfeiler, Felder = felder,
            Feldlaenge = feldlaenge, Quert = quert, Karren = weg.Karren,
        };
    }

    /// <summary>Die Höhe des Längsprofils eines Weges an einer Stelle (zwischen den Profilpunkten linear).</summary>
    private double ProfilHoehe(IReadOnlyList<Kartenort> v, double stelle)
    {
        if (!_gelaende.HatHoehen) return 0;
        var profil = _gelaende.Profillinie(v);
        var s = 0.0;
        for (var k = 0; k + 1 < profil.Count; k++)
        {
            var l = profil[k].Ort.Abstand(profil[k + 1].Ort);
            if (s + l >= stelle)
                return l <= 0 ? profil[k].Hoehe : profil[k].Hoehe + (profil[k + 1].Hoehe - profil[k].Hoehe) * Math.Clamp((stelle - s) / l, 0, 1);
            s += l;
        }
        return profil[^1].Hoehe;
    }

    /// <summary>Grad wie <c>Rotation.Y</c> (lokal +z zeigt von a nach b), 0 bis unter 360.</summary>
    internal static double Richtung(Kartenort a, Kartenort b)
    {
        var grad = DetMathe.Atan2(b.X - a.X, b.Z - a.Z) * 180 / Math.PI;
        return Math.Round(grad < 0 ? grad + 360 : grad, 3);
    }

    private Faehrverbindung FaehreRechnen(int nummer, Uebergang u)
    {
        var (von, bis) = (Kartenort.Aus(u.Von), Kartenort.Aus(u.Bis));
        var laenge = von.Abstand(bis);
        var wasser = _gelaende.Wasser;
        // die Strecke über Wasser, je Gewässer
        var jeGewaesser = new Dictionary<int, (double Meter, double Spiegel, string Name)>();
        var schritte = Math.Max(1, (int)Math.Ceiling(laenge / Abtastung));
        for (var i = 0; i <= schritte; i++)
        {
            var p = new Kartenort(von.X + (bis.X - von.X) * i / schritte, von.Z + (bis.Z - von.Z) * i / schritte);
            if (wasser.An(p.X, p.Z) is not { } w) continue;
            var alt = jeGewaesser.GetValueOrDefault(w.Nummer, (0, w.Spiegel, w.Name));
            jeGewaesser[w.Nummer] = (alt.Meter + laenge / schritte, w.Spiegel, w.Name);
        }
        var haupt = jeGewaesser.Count == 0 ? ((double, double, string)?)null : jeGewaesser.Values.OrderByDescending(x => x.Meter).First();
        // vom Anleger zur Uferkante: in Schritten, dann halbiert auf 1 mm
        Kartenort Uferkante(Kartenort a, Kartenort b)
        {
            Kartenort Bei(double t) => new(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);
            if (laenge <= 0 || wasser.ImUfer(a.X, a.Z)) return a;
            var schritt = Abtastung / laenge;
            var t = 0.0;
            while (t < 1 && !wasser.ImUfer(Bei(t + schritt).X, Bei(t + schritt).Z)) t += schritt;
            if (t >= 1) return a;
            var (land, ufer) = (t, Math.Min(1, t + schritt));
            for (var n = 0; n < 20 && (ufer - land) * laenge > 0.001; n++)
            {
                var m = (land + ufer) / 2;
                if (wasser.ImUfer(Bei(m).X, Bei(m).Z)) ufer = m;
                else land = m;
            }
            return Bei((land + ufer) / 2);
        }
        return new Faehrverbindung
        {
            Nummer = nummer, Name = u.Name, Von = von, Bis = bis, Gewaesser = haupt?.Item3, Laenge = laenge,
            UeberWasser = jeGewaesser.Values.Sum(x => x.Meter), Spiegel = haupt?.Item2 ?? 0, Richtung = Richtung(von, bis),
            VonImWasser = wasser.IstWasser(von.X, von.Z), BisImWasser = wasser.IstWasser(bis.X, bis.Z),
            VonUferkante = Uferkante(von, bis), BisUferkante = Uferkante(bis, von),
            VonUfer = wasser.Uferabstand(von.X, von.Z), BisUfer = wasser.Uferabstand(bis.X, bis.Z),
        };
    }
}
