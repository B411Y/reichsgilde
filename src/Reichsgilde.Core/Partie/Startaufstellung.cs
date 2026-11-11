using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Partie;

/// <summary>Wie die KI-Häuser einer neuen Partie ihren Beruf, ihr Volk und ihren Glauben bekommen (<see cref="NeuePartie.Aufstellung"/>).</summary>
public enum Aufstellungsart
{
    /// <summary>Wie im Spiel: Beruf, Volk und Glaube jedes KI-Hauses gelost, jeder Beruf höchstens einmal je Partie (<see cref="Startaufstellung"/>).</summary>
    Gelost,

    /// <summary>
    /// Jedes KI-Haus genau so, wie die Startbelegung der Karte (ohne Karte: <see cref="Startbelegung.Ersatz"/>) es setzt – ohne Los und ohne die
    /// Regeln der Aufstellung (zwei Schmiede sind dann möglich). Nur für Tests und Vergleiche; das Hauptmenü bietet es nicht an.
    /// </summary>
    Vorgabe,
}

/// <summary>
/// Die Startaufstellung einer neuen Partie (Nutzerentscheidung 11.11.2026):
///
///   • <b>Jeder Beruf höchstens einmal je Partie</b> – der Spieler und alle KI-Häuser zusammen. Der Beruf des Spielers steht zuerst fest.
///   • Jedes mitspielende KI-Haus bekommt in der Reihenfolge der Karte <b>Volk, Glaube und Beruf gelost</b>, in dieser Reihenfolge, mit einer
///     eigenen Zufallsquelle aus dem Startwert der Partie (<see cref="Losquelle"/>, wie das Los der Hofstellen): Alle übrigen Würfe bleiben, wie sie
///     ohne dieses Los wären, und jeder Rechner einer Mehrspielerpartie lost dasselbe. Je Haus genau drei Würfe (auch wenn eine Liste nur einen
///     Eintrag hat – die Reihe der Würfe bleibt fest).
///   • Volk: eines der Völker mit Bürgerrecht (<see cref="KiVoelker"/>; Orks und Oger sind nicht spielbar).
///   • Glaube: einer aus <see cref="Startglauben"/> des gelosten Volkes, gleich verteilt, unabhängig vom Volk sonst.
///   • Beruf: einer der noch freien Berufe (<see cref="FreieBerufe"/>), gleich verteilt – ohne die, die für diesen Glauben die Kirchengrenze
///     überschreiten würden (kirche.kirchenJeGlaube; mit „jeder Beruf einmal“ gibt es ohnehin höchstens einen Prediger). Das ersetzt die Startregel
///     aus PR 1a (KI-Haus nimmt statt einer zweiten Kirche einen anderen Beruf).
///   • Plätze: Die Werkstatt des neuen Berufs steht auf dem Werkstattplatz des Hauses (jeder Werkstattplatz taugt für jede Werkstatt). Der
///     Umlandbetrieb kommt auf einen Umland-Startplatz der Karte dieser Art (Startbetrieb eines mitspielenden Hauses): den ersten, den noch
///     niemand hat, in der Reihenfolge der Häuser. Ein Haus, das seinen Beruf behält, behält auch seinen Platz – jede Umlandart gehört zu genau
///     einem Beruf, und jeder Beruf kommt nur einmal vor. Gibt es keinen (die Karten haben heute keine Start-Duftgärten, Klosterhöfe, Wachtürme), beginnt das
///     Haus nur mit der Werkstatt – bei „Normal“ ändert das nichts, dort steht der Umlandbetrieb ohnehin frei zur Pacht. Die freien Liegenschaften
///     der Daten bleiben für alle. Umland-Startplätze, die niemand bekommt, stehen frei zur Pacht (<see cref="Ergebnis.Frei"/>).
///   • Behält ein Haus den Beruf der Karte, bleiben seine Startbetriebe genau wie dort (Plätze, Namen, Startprodukt).
///   • Notausweg: Reichen die Berufe nicht (mehr Häuser als freie Berufe, oder ein Haus ohne Werkstattplatz, dessen Beruf schon vergeben ist),
///     spielen weniger KI-Häuser mit, und die Partie meldet es. Zwei Häuser mit demselben Beruf gibt es nie.
/// </summary>
public static class Startaufstellung
{
    /// <summary>Schlüssel der eigenen Zufallsquelle (<see cref="Losquelle"/>), gemischt mit dem Startwert der Partie.</summary>
    public const int LosSchluessel = 0x417566; // „Auf“

    internal static Zufallsquelle Losquelle(int zufallsstartwert) => new(Zugwuerfel.Mischen(zufallsstartwert, LosSchluessel, 97));

    /// <summary>Die Völker, die ein KI-Haus haben kann (Orks und Oger haben kein Bürgerrecht), in fester Reihenfolge.</summary>
    public static readonly IReadOnlyList<Volk> KiVoelker = new[] { Volk.Mensch, Volk.Zwerg, Volk.Elb, Volk.Halbling };

    // ── Glaubensliste je Volk ───────────────────────────────────────────────
    // ERWEITERUNG Ahtnentum (Prediger-PR 5): Kommen die Konfessionen Altahtnisch und Neuahtnisch, werden sie nur hier eingetragen –
    //   Menschen:           Neuahtnisch dazu (nie Altahtnisch),
    //   Zwerge, Elben, Halblinge: Altahtnisch und Neuahtnisch dazu.
    // Der Naturglaube bleibt allein bei Orks und Ogern. Die Tests (StartaufstellungTests) prüfen diese Regeln schon über die Namen.

    private static readonly Konfession[] GlaubenMenschen = { Konfession.Katholisch, Konfession.Evangelisch /* später: Neuahtnisch */ };
    private static readonly Konfession[] GlaubenAndere = { Konfession.Katholisch, Konfession.Evangelisch /* später: Altahtnisch, Neuahtnisch */ };
    private static readonly Konfession[] GlaubenNatur = { Konfession.Naturglaube };

    /// <summary>Mit welchen Glauben ein Haus dieses Volkes beginnen kann (gelost wird gleich verteilt) – in fester Reihenfolge.</summary>
    public static IReadOnlyList<Konfession> Startglauben(Volk volk) => volk switch
    {
        Volk.Ork or Volk.Oger => GlaubenNatur,
        Volk.Mensch => GlaubenMenschen,
        _ => GlaubenAndere,
    };

    // ── Berufe ──────────────────────────────────────────────────────────────

    /// <summary>Alle Berufe, mit denen ein Haus beginnen kann (Werkstatt in der Stadt, die die Daten kennen), nach Namen geordnet (ordinal).</summary>
    public static IReadOnlyList<string> Berufe(Wirtschaftsdaten daten) =>
        daten.Berufe.Where(p => daten.KenntArt(p.Value.Werkstatt) && daten.Art(p.Value.Werkstatt).InDerStadt)
            .Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();

    /// <summary>
    /// Die Berufe, die noch frei sind, wenn die <paramref name="vergeben"/>en schon jemand hat (nach Namen geordnet). Mehrspieler: Wer zuerst wählt,
    /// bekommt den Beruf; im Startbildschirm sind die übrigen gesperrt (<see cref="BerufVergeben"/>).
    /// </summary>
    public static IReadOnlyList<string> FreieBerufe(Wirtschaftsdaten daten, IEnumerable<string> vergeben)
    {
        var weg = vergeben.ToList();
        return Berufe(daten).Where(b => !weg.Contains(b, StringComparer.Ordinal)).ToList();
    }

    /// <summary>
    /// Vergibt einen Beruf an den, der ihn zuerst wählt (Mehrspieler-Startbildschirm): Ist er frei, steht er danach in <paramref name="vergeben"/>,
    /// und das Ergebnis ist null; sonst bleibt alles, und das Ergebnis ist der Grund.
    /// </summary>
    public static string? BerufVergeben(Wirtschaftsdaten daten, ICollection<string> vergeben, string beruf)
    {
        if (!Berufe(daten).Contains(beruf, StringComparer.Ordinal)) return $"Den Beruf „{beruf}“ gibt es nicht.";
        if (vergeben.Contains(beruf)) return $"Den Beruf „{beruf}“ hat schon ein anderes Haus gewählt.";
        vergeben.Add(beruf);
        return null;
    }

    // ── Los ─────────────────────────────────────────────────────────────────

    /// <summary>Was die Aufstellung ergibt.</summary>
    /// <param name="Belegung">Die Startbelegung mit den gelosten Häusern (nur die, die mitspielen).</param>
    /// <param name="Karte">Die Karte mit den Namen der Betriebe an den Plätzen, die die Häuser jetzt haben (null ohne Karte).</param>
    /// <param name="Frei">Umland-Startplätze (Name, Art), die niemand bekommt: Sie stehen frei zur Pacht.</param>
    /// <param name="Meldung">Null, oder warum weniger KI-Häuser mitspielen.</param>
    public sealed record Ergebnis(Startbelegung Belegung, Stadtkarte? Karte, IReadOnlyList<(string Name, Betriebsart Art)> Frei, string? Meldung);

    /// <summary>Lost die mitspielenden KI-Häuser aus (siehe Kopf). <paramref name="spielerglaube"/>: zählt für die Kirchengrenze, wenn der Spieler Prediger ist.</summary>
    public static Ergebnis Losen(Stadtkarte? karte, Startbelegung start, Wirtschaftsdaten daten, string spielerberuf, Konfession spielerglaube,
        IReadOnlyList<Starthaus> mitspielend, int zufallsstartwert)
    {
        var los = Losquelle(zufallsstartwert);
        var vergeben = new List<string> { spielerberuf };
        var kirchen = new List<Konfession>(); // je Kirche ihr Glaube
        var grenze = daten.Kirche?.KirchenJeGlaube ?? 0;
        bool IstKirche(string beruf) => daten.Kirche is { } k && beruf == k.Beruf;
        if (IstKirche(spielerberuf)) kirchen.Add(spielerglaube);

        bool AufWerkstattplatz(Startbetrieb b) => karte is null ? daten.Art(b.Art).InDerStadt : karte.GebaeudeMit(b.Platz) is { Art: Bausteinart.Werkstattplatz };
        bool AufUmlandplatz(Startbetrieb b) => karte is not null && karte.GebaeudeMit(b.Platz) is { Art: Bausteinart.Umlandbetrieb };

        // Die Umland-Startplätze der mitspielenden Häuser (in ihrer Reihenfolge) – wer welchen bekommt, steht unten
        var umland = mitspielend.SelectMany(h => h.Betriebe.Where(AufUmlandplatz)).ToList();
        var belegt = new List<Startbetrieb>();

        var namen = new List<(string Platz, string Name)>(); // neue Namen an Plätzen der Karte
        var haeuser = new List<Starthaus>();
        var ohne = new List<string>();
        foreach (var h in mitspielend)
        {
            // Je Haus genau drei Würfe: Volk, Glaube, Beruf
            var volk = KiVoelker[los.Next(KiVoelker.Count)];
            var glauben = Startglauben(volk);
            var glaube = glauben[los.Next(glauben.Count)];
            var werkstatt = h.Betriebe.FirstOrDefault(AufWerkstattplatz);
            var alterBeruf = werkstatt is null ? h.Betriebe.Select(b => daten.Art(b.Art).Beruf).FirstOrDefault(b => b != "") : daten.Art(werkstatt.Art).Beruf;
            var kandidaten = werkstatt is null
                ? (alterBeruf is not null && !vergeben.Contains(alterBeruf) ? new List<string> { alterBeruf } : new List<string>())
                : FreieBerufe(daten, vergeben).Where(b => !IstKirche(b) || grenze <= 0 || kirchen.Count(k => k == glaube) < grenze).ToList();
            var wurf = los.Next(Math.Max(1, kandidaten.Count));
            if (kandidaten.Count == 0)
            {
                ohne.Add(h.Name);
                continue;
            }
            var beruf = kandidaten[wurf];
            vergeben.Add(beruf);
            if (IstKirche(beruf)) kirchen.Add(glaube);

            List<Startbetrieb> betriebe;
            if (beruf == alterBeruf)
            {
                betriebe = h.Betriebe; // wie die Karte: Plätze, Namen, Startprodukt
                belegt.AddRange(h.Betriebe.Where(AufUmlandplatz));
            }
            else
            {
                var b = daten.Berufe[beruf];
                var nachname = Familie.NachnameAus(h.Name);
                betriebe = new List<Startbetrieb> { new() { Platz = werkstatt!.Platz, Art = b.Werkstatt, ErsatzName = "{art} " + nachname } };
                if (karte is not null) namen.Add((werkstatt.Platz, $"{daten.Art(b.Werkstatt).Name} {nachname}"));
                if (b.Umland is { } u && daten.KenntArt(u))
                {
                    if (karte is null)
                        betriebe.Add(new Startbetrieb { Art = u, ErsatzName = "{art} " + nachname });
                    else if (umland.FirstOrDefault(x => x.Art == u && !belegt.Contains(x)) is { } platz)
                    {
                        belegt.Add(platz);
                        betriebe.Add(new Startbetrieb { Platz = platz.Platz, Art = u });
                        namen.Add((platz.Platz, $"{daten.Art(u).Name} {nachname}"));
                    }
                }
            }
            haeuser.Add(new Starthaus
            {
                Name = h.Name, Volk = volk, Glaube = glaube, Geschlecht = h.Geschlecht, Wappen = h.Wappen, Farbe = h.Farbe, Wohnhaus = h.Wohnhaus,
                Betriebe = betriebe,
            });
        }

        var frei = umland.Where(x => !belegt.Contains(x))
            .Select(x => (karte!.GebaeudeMit(x.Platz)?.Betrieb ?? $"{daten.Art(x.Art).Name} {x.Platz}", x.Art)).ToList();
        var belegung = new Startbelegung { Spieler = start.Spieler, Haeuser = haeuser, Hofstellen = start.Hofstellen };
        var neueKarte = karte?.MitBelegung(belegung, namen.ToDictionary(n => n.Platz, n => (string?)n.Name, StringComparer.Ordinal));
        string? meldung = ohne.Count == 0 ? null
            : $"Für {string.Join(", ", ohne)} war kein Beruf mehr frei (jeder Beruf nur einmal je Partie) – {(ohne.Count == 1 ? "dieses Haus spielt" : "diese Häuser spielen")} nicht mit.";
        return new Ergebnis(belegung, neueKarte, frei, meldung);
    }
}
