using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Partie;

// ═════════════════════════════════════════════════════════════════════════════
//  Neue Partie und Höfe (Nutzerentscheidungen 11.11.2026)
//
//  • In einer Partie beginnen bis zu acht Häuser (HoechstensHoefe) – so viele, wie in die Bauzonen im Stadtinneren passen (Startortlos; seit
//    16.11.2026 gibt es keine Hofstellen und keine Plätze der Karte mehr).
//  • Keine festen Dynastien mehr (Nutzer 17.11.2026): Die Karte nennt keine KI-Häuser. Jede Partie würfelt genau so viele KI-Gegner, wie gewählt
//    sind – Name, Wappen, Geschlecht, Volk, Glaube, Beruf. Erst zählt die Hauptfarbe des Spielers, dann bekommt jedes KI-Haus eine noch freie der
//    acht Hausfarben (keine doppelt); kein Name doppelt. Die Karte nennt nur noch die Umland-Startplätze (Abschnitt „start“ → „umland“) und die
//    Familiennamen der Stadt („hausnamen“, z. B. die Kölner Geschlechter) – aus ihnen und den Namen des Volkes wird gewürfelt. Eine ältere Karte
//    mit festen Häusern („haeuser“) lädt weiter; ihre Häuser spielen im Spiel nicht mehr fest mit (nur noch zur Probe: Aufstellungsart.Vorgabe)
//    und geben ihre Umlandbetriebe und Namen her. Ohne Karte (Tests, Messungen) gilt weiter der Ersatz mit seinen drei festen Häusern.
//  • Bei jedem Partiestart wird gewürfelt, was niemand festlegt: für jedes KI-Haus Name, Wappen, Volk, Glaube und Beruf, für den Spieler,
//    was er offen lässt (SpielerWuerfeln). Es gelten die Regeln der Startaufstellung (jeder Beruf einmal, der Spieler zuerst; Glaube nach Volk).
//  • Persönlichkeitszüge sind immer gewürfelt, auch beim Spieler: drei, nie zwei Gegenteile. Vor Partiebeginn darf er sie neu würfeln
//    (Befehl ZuegeNeuWuerfeln) – frei wählen nie.
//  • KI-Gegner: 0 bis (Häuser − 1), Stärke Leicht / Normal / Schwer (wirtschaft.json „kiStaerke“; Normal rechnet wie bisher).
//
//  Gewürfelt wird nur mit eigenen Zufallsquellen aus dem Startwert der Partie (Zugwuerfel.Mischen mit eigenem Schlüssel) – die übrigen Würfe
//  der Partie bleiben, wie sie ohne diese Würfe wären, und jeder Rechner einer Mehrspielerpartie würfelt dasselbe.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Wie stark die KI-Gegner spielen (Neue Partie). Normal ist der Stand vor dem 11.11.2026.</summary>
public enum KiStaerke
{
    /// <summary>Weniger Startgeld, plant vorsichtiger (mehr Reserve, mehr Vorsicht, nur was sich schnell bezahlt macht).</summary>
    Leicht,

    /// <summary>Wie bisher – keine Änderung.</summary>
    Normal,

    /// <summary>Mehr Startgeld, greift Chancen schneller auf (weniger Reserve und Vorsicht, auch was sich erst später bezahlt macht).</summary>
    Schwer,
}

/// <summary>
/// Was für ein Haus festgelegt ist; jedes Feld null = gewürfelt. Für den Spieler (<see cref="Partiebesetzung.SpielerWuerfeln"/>) und für jedes
/// KI-Haus einzeln unter „Erweitert“ (<see cref="NeuePartie.KiVorgaben"/>).
/// </summary>
/// <param name="Name">Familienname („Hammerschmid“; „Haus …“ davor wird abgeschnitten).</param>
/// <param name="Volk">Ohne Orks und Oger (kein Bürgerrecht, <see cref="Startaufstellung.KiVoelker"/>).</param>
/// <param name="Glaube">Muss zum Volk passen (<see cref="Startaufstellung.Startglauben"/>); ohne Volk wird ein Volk gewürfelt, zu dem er passt.</param>
/// <param name="Beruf">Kennung aus <see cref="Startaufstellung.Berufe"/>; jeder Beruf nur einmal je Partie.</param>
public sealed record Hausvorgabe(string? Name = null, Wappen? Wappen = null, Volk? Volk = null, Konfession? Glaube = null, string? Beruf = null);

/// <summary>Ein ganz festgelegtes Haus: was der Spieler gewählt hat und was für ihn gewürfelt wurde.</summary>
/// <param name="Familienname">Ohne „Haus“ davor (für <see cref="NeuePartie.Familienname"/>).</param>
public sealed record Hausbesetzung(string Familienname, Wappen Wappen, Volk Volk, Konfession Glaube, string Beruf);

/// <summary>Ein Befehl vor Partiebeginn (Startbildschirm). Spieleraktionen gehen auch hier nur über Befehle (Mehrspieler).</summary>
public abstract record Vorbereitungsbefehl;

/// <summary>Würfelt die Persönlichkeitszüge des Spielers neu (<see cref="NeuePartie.ZugWurf"/> + 1).</summary>
public sealed record ZuegeNeuWuerfeln : Vorbereitungsbefehl;

/// <summary>Faktoren einer Stärke der KI-Gegner (je 1 = wie Normal).</summary>
public sealed class KiStaerkeStufe
{
    /// <summary>Faktor auf das Startgeld (<c>allgemein.startvermoegen</c>).</summary>
    public double Startgeld { get; init; } = 1;

    /// <summary>Faktor auf die Reserve (<see cref="KiProfil.Reserve"/>): mehr = vorsichtiger.</summary>
    public double Reserve { get; init; } = 1;

    /// <summary>Faktor auf die Vorsicht (<see cref="KiProfil.Vorsicht"/>, Raub und Eskorte): mehr = vorsichtiger.</summary>
    public double Vorsicht { get; init; } = 1;

    /// <summary>Faktor auf die längste Amortisation (<see cref="KiProfil.MaxAmortisationJahre"/>): mehr = greift auch spätere Chancen auf.</summary>
    public double Amortisation { get; init; } = 1;

    internal void Pruefen(List<string> f, string stufe)
    {
        foreach (var (name, wert) in new[] { ("startgeld", Startgeld), ("reserve", Reserve), ("vorsicht", Vorsicht), ("amortisation", Amortisation) })
            if (!(wert > 0) || wert > 4) f.Add($"kiStaerke.{stufe}.{name} muss größer als 0 und höchstens 4 sein.");
    }
}

/// <summary>Abschnitt „kiStaerke“ in wirtschaft.json (Datenversion 85). „Normal“ steht nicht darin – es rechnet immer wie bisher.</summary>
public sealed class KiStaerkeDaten
{
    public KiStaerkeStufe? Leicht { get; init; }
    public KiStaerkeStufe? Schwer { get; init; }

    /// <summary>Die Faktoren dieser Stärke; null bei Normal oder wenn die Stufe fehlt.</summary>
    public KiStaerkeStufe? Stufe(KiStaerke staerke) => staerke switch
    {
        KiStaerke.Leicht => Leicht,
        KiStaerke.Schwer => Schwer,
        _ => null,
    };

    internal void Pruefen(List<string> f)
    {
        Leicht?.Pruefen(f, "leicht");
        Schwer?.Pruefen(f, "schwer");
        if (Leicht is { } l && (l.Startgeld > 1 || l.Reserve < 1 || l.Vorsicht < 1 || l.Amortisation > 1))
            f.Add("kiStaerke.leicht: weniger Startgeld und vorsichtiger als Normal (startgeld, amortisation ≤ 1; reserve, vorsicht ≥ 1).");
        if (Schwer is { } s && (s.Startgeld < 1 || s.Reserve > 1 || s.Vorsicht > 1 || s.Amortisation < 1))
            f.Add("kiStaerke.schwer: mehr Startgeld und wagemutiger als Normal (startgeld, amortisation ≥ 1; reserve, vorsicht ≤ 1).");
    }
}

/// <summary>Wer auf welchem Hof beginnt und was dafür gewürfelt wird (siehe Kopf).</summary>
public static class Partiebesetzung
{
    /// <summary>So viele Häuser (Spieler und KI zusammen) beginnen in einer Partie höchstens.</summary>
    public const int HoechstensHoefe = 8;

    /// <summary>Schlüssel der Zufallsquellen für Namen, Wappen und Geschlecht (<see cref="Besetzungsquelle"/>).</summary>
    public const int BesetzungSchluessel = 0x426573; // „Bes“

    /// <summary>Schlüssel der Zufallsquelle für die Züge des Spielers (<see cref="SpielerZuege"/>).</summary>
    public const int ZugSchluessel = 0x5A7567; // „Zug“

    private const int ZweckSpieler = 1, ZweckKiHaeuser = 2, ZweckNamen = 3, ZweckKartenwappen = 4;

    internal static Zufallsquelle Besetzungsquelle(int zufallsstartwert, int zweck) => new(Zugwuerfel.Mischen(zufallsstartwert, BesetzungSchluessel, zweck));

    /// <summary>
    /// Die Hausfarben („#rrggbb“, Fahnen und Dächer). Seit dem 17.11.2026 trägt jedes Haus die seiner ersten Wappenfarbe
    /// (<see cref="Wappen.Palettenfarbe"/>: Blau, Rot, Grün, Gelb, Purpur, Orange, Grau, Ocker – acht Farben für acht Häuser); die übrigen kommen nur noch in älteren Karten und Spielständen vor.
    /// </summary>
    public static readonly IReadOnlyList<string> Hausfarben = new[]
    {
        "#336bb8", "#b33329", "#388c47", "#d9ad2e", "#7a3b8f", "#2a8c8a", "#c4672a", "#5c5c5c", "#8c6239", "#b84d86", "#a88a3a",
    };

    // ── Höfe ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Wie viele Häuser (Spieler und KI) die Startbelegung dieser Karte nennt: der Spieler und ihre festen KI-Häuser (ohne Karte:
    /// <see cref="Startbelegung.Ersatz"/>; die Karten des Spiels nennen seit dem 17.11.2026 keine mehr – dann 1). Wie viele wirklich Platz
    /// finden, sagt <see cref="HoechstensKi"/> mit den Daten.
    /// </summary>
    public static int Hoefe(Stadtkarte? karte) => 1 + (karte?.Startbelegung ?? Startbelegung.Ersatz).Haeuser.Count;

    /// <summary>
    /// Wie viele KI-Gegner höchstens mitspielen. Seit „nur noch Bauzonen“ (15.11.2026) mit <paramref name="daten"/>: so viele Häuser, wie in
    /// die Bauzonen im Stadtinneren passen (<see cref="Startortlos.Haeuser"/>, höchstens <see cref="HoechstensHoefe"/>), minus der Spieler.
    /// Ohne Los (<see cref="Aufstellungsart.Vorgabe"/>) höchstens die KI-Häuser der Startbelegung – gewürfelt wird dann nichts. Ohne Karte:
    /// die Häuser des Ersatzes. Ohne <paramref name="daten"/> (oder ohne Bauwesen in den Daten) die Häuser der Startbelegung –
    /// nur als Anhalt; die Partie prüft mit den Daten.
    /// </summary>
    public static int HoechstensKi(Stadtkarte? karte, Aufstellungsart aufstellung = Aufstellungsart.Gelost, Wirtschaftsdaten? daten = null)
    {
        var haeuser = (karte?.Startbelegung ?? Startbelegung.Ersatz).Haeuser.Count;
        if (karte is not null && daten is not null && Startortlos.IstAktiv(karte, daten))
        {
            var platz = Math.Max(0, Startortlos.Haeuser(karte, daten) - 1);
            return aufstellung == Aufstellungsart.Vorgabe ? Math.Min(haeuser, platz) : platz;
        }
        return aufstellung == Aufstellungsart.Vorgabe ? haeuser : Math.Max(0, Hoefe(karte) - 1);
    }

    // ── Züge des Spielers ───────────────────────────────────────────────────

    /// <summary>
    /// Die Persönlichkeitszüge des Spielers für Wurf Nummer <paramref name="wurf"/> (0 = der erste, jeder „Züge neu würfeln“ zählt eins weiter):
    /// <see cref="CharakterzuegeDaten.JePerson"/> Züge, nie zwei Gegenteile, gewichtet nach Volk wie bei jedem Erwachsenen ohne Eltern. Gleicher
    /// Startwert, gleiches Volk, gleicher Wurf = gleiche Züge. Leer ohne Charakterzüge in den Daten.
    /// </summary>
    public static IReadOnlyList<Charakterzug> SpielerZuege(Wirtschaftsdaten daten, int zufallsstartwert, Volk volk, int wurf)
    {
        if (daten.Charakterzuege is not { IstAktiv: true } d) return Array.Empty<Charakterzug>();
        var z = new Zufallsquelle(Zugwuerfel.Mischen(zufallsstartwert, ZugSchluessel, wurf));
        return Zugwuerfel.Persoenlichkeit(d, z, volk, null, Array.Empty<Charakterzug>(), Array.Empty<Charakterzug>());
    }

    /// <summary>Führt einen Befehl vor Partiebeginn aus: das neue Startmenü-Ergebnis.</summary>
    public static NeuePartie Ausfuehren(NeuePartie neu, Vorbereitungsbefehl befehl) => befehl switch
    {
        ZuegeNeuWuerfeln => neu with { ZugWurf = (neu.ZugWurf ?? 0) + 1 },
        _ => throw new ArgumentException($"Unbekannter Befehl {befehl.GetType().Name}.", nameof(befehl)),
    };

    // ── Spieler ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Würfelt für den Spieler, was er nicht festlegt (<paramref name="festgelegt"/>, null = alles): Volk (ohne Orks und Oger), Glaube zum Volk,
    /// Beruf (frei von den Berufen der KI-Vorgaben – der Spieler wählt zuerst), Familienname aus den Namen seines Volkes (keiner eines KI-Hauses)
    /// und Wappen (keines eines KI-Hauses). Immer dieselbe Reihe von Würfen, gleich was festgelegt ist. Wirft, wenn Festgelegtes nicht geht.
    /// </summary>
    public static Hausbesetzung SpielerWuerfeln(Wirtschaftsdaten daten, Stadtkarte? karte, int zufallsstartwert, Hausvorgabe? festgelegt = null,
        IReadOnlyList<Hausvorgabe?>? kiVorgaben = null)
    {
        festgelegt ??= new Hausvorgabe();
        var start = karte?.Startbelegung ?? Startbelegung.Ersatz;
        var z = Besetzungsquelle(zufallsstartwert, ZweckSpieler);
        var vorgaben = (kiVorgaben ?? Array.Empty<Hausvorgabe?>()).OfType<Hausvorgabe>().ToList();

        var volk = VolkWuerfeln(z, festgelegt);
        var glauben = Startaufstellung.Startglauben(volk);
        var glaube = glauben[z.Next(glauben.Count)];
        if (festgelegt.Glaube is { } g) glaube = g;

        var vergeben = vorgaben.Select(v => v.Beruf).OfType<string>().ToList();
        var frei = Startaufstellung.FreieBerufe(daten, vergeben);
        var wurf = z.Next(Math.Max(1, frei.Count));
        string beruf;
        if (festgelegt.Beruf is { } b)
        {
            // Der Spieler wählt zuerst: Sein Beruf gilt, auch wenn ihn eine KI-Vorgabe nennt (die weist dann der Aufbau ab).
            if (Startaufstellung.BerufVergeben(daten, new List<string>(), b) is { } grund) throw new ArgumentException(grund);
            beruf = b;
        }
        else beruf = frei.Count > 0 ? frei[wurf] : throw new ArgumentException("Für den Spieler ist kein Beruf mehr frei.");

        var belegteNamen = start.Rivalennamen.Concat(vorgaben.Select(v => v.Name).OfType<string>().Select(Partieaufbau.Hausname)).ToList();
        var name = NameWuerfeln(daten, z, volk, belegteNamen);
        if (festgelegt.Name is { } n) name = Familie.NachnameAus(Partieaufbau.Hausname(n));

        var belegteWappen = start.Rivalenwappen.Concat(vorgaben.Select(v => v.Wappen).OfType<Wappen>()).ToList();
        var wappen = Wappen.Zufall(z, belegteWappen);
        if (festgelegt.Wappen is { } w) wappen = w;
        return new Hausbesetzung(name, wappen, volk, glaube, beruf);
    }

    /// <summary>Ein Volk mit Bürgerrecht – das festgelegte, sonst gewürfelt (nur Völker, zu denen ein festgelegter Glaube passt). Genau ein Wurf.</summary>
    internal static Volk VolkWuerfeln(Zufallsquelle z, Hausvorgabe? v)
    {
        var voelker = v?.Glaube is { } g && v.Volk is null
            ? Startaufstellung.KiVoelker.Where(x => Startaufstellung.Startglauben(x).Contains(g)).ToList()
            : Startaufstellung.KiVoelker;
        if (voelker.Count == 0) throw new ArgumentException($"Den Glauben „{v!.Glaube}“ hat kein Volk mit Bürgerrecht.");
        var volk = voelker[z.Next(voelker.Count)];
        if (v?.Volk is { } fest)
        {
            if (!Startaufstellung.KiVoelker.Contains(fest)) throw new ArgumentException("Orks und Oger haben kein Bürgerrecht – sie führen kein Haus.");
            volk = fest;
        }
        if (v?.Glaube is { } glaube && !Startaufstellung.Startglauben(volk).Contains(glaube))
            throw new ArgumentException($"Ein Haus der {volk.Mehrzahl()} kann nicht mit dem Glauben „{glaube}“ beginnen.");
        return volk;
    }

    /// <summary>
    /// Ein Familienname, den noch kein Haus trägt (genau ein Wurf; sind alle vergeben, mit angehängter Zahl): aus den Namen des Volkes und –
    /// für die KI-Häuser (17.11.2026) – den Familiennamen der Stadt (<paramref name="stadtnamen"/>, <see cref="Startbelegung.Hausnamen"/>;
    /// sie stehen vorn, jeder Name zählt einmal).
    /// </summary>
    internal static string NameWuerfeln(Wirtschaftsdaten daten, Zufallsquelle z, Volk volk, IReadOnlyCollection<string> belegt,
        IReadOnlyList<string>? stadtnamen = null)
    {
        var namen = daten.Dynastie.Voelker.TryGetValue(volk, out var v) && v.Familiennamen.Count > 0 ? v.Familiennamen : new List<string> { "Hammerschmid" };
        if (stadtnamen is { Count: > 0 })
        {
            var alle = new List<string>();
            foreach (var n in stadtnamen.Select(Familie.NachnameAus).Concat(namen))
                if (n != "" && !alle.Contains(n, StringComparer.Ordinal)) alle.Add(n);
            namen = alle;
        }
        var frei = namen.Where(n => !belegt.Contains(Partieaufbau.Hausname(n))).ToList();
        var wurf = z.Next(Math.Max(1, frei.Count));
        if (frei.Count > 0) return frei[wurf];
        var basis = namen[wurf % namen.Count];
        var k = 2;
        while (belegt.Contains(Partieaufbau.Hausname($"{basis} {k}"))) k++;
        return $"{basis} {k}";
    }

    // ── KI-Häuser ───────────────────────────────────────────────────────────

    /// <summary>
    /// Die mitspielenden KI-Häuser vor dem Los. Seit dem 17.11.2026 nennen die Karten keine festen Häuser mehr: Jedes KI-Haus ist gewürfelt
    /// (<see cref="Starthaus.Gewuerfelt"/>) – Wappen (keines doppelt, keine schon vergebene Hauptfarbe: erst der Spieler, dann jedes KI-Haus eine
    /// noch freie, <see cref="Wappen.Zufall"/>) und Geschlecht hier, ohne Platz: Wo es wohnt und arbeitet, lost das <see cref="Startortlos"/>.
    /// Name, Volk, Glaube und Beruf würfelt danach die <see cref="Startaufstellung"/>. Eine ältere Karte (oder der Ersatz ohne Karte) mit festen
    /// Häusern: zuerst die ersten <paramref name="anzahl"/> der Startbelegung (deren Betriebsarten die Daten kennen), dann für jedes weitere ein gewürfeltes.
    /// </summary>
    internal static List<Starthaus> KiHaeuser(Stadtkarte? karte, Startbelegung start, Wirtschaftsdaten daten, int anzahl, Wappen spielerWappen,
        IReadOnlyList<Hausvorgabe?>? vorgaben, int zufallsstartwert)
    {
        // Feste Häuser spielen nur noch ohne Karte mit (der Ersatz der Tests und Messungen): Auf einer Karte ist jedes KI-Haus gewürfelt, auch
        // wenn eine ältere Karte noch Häuser nennt – die geben dann nur ihre Umland-Startplätze und Namen her (Startaufstellung).
        var ausKarte = karte is null ? start.Haeuser.Take(anzahl).ToList() : new List<Starthaus>();
        var feste = karte is null ? start.Haeuser : new List<Starthaus>();
        var haeuser = KartenwappenEindeutig(ausKarte.Where(h => h.Betriebe.All(b => daten.KenntArt(b.Art))).ToList(), spielerWappen, vorgaben, zufallsstartwert);
        var fehlen = anzahl - ausKarte.Count;
        if (fehlen <= 0) return haeuser;
        if (karte is null)
            throw new ArgumentException($"Ohne Karte spielen höchstens {start.Haeuser.Count} KI-Häuser mit.");

        var art = daten.Berufe[Startaufstellung.Berufe(daten)[0]].Werkstatt; // Platzhalter: den Beruf würfelt die Startaufstellung
        var wappen = feste.Select(h => h.Wappen).Append(spielerWappen)
            .Concat((vorgaben ?? Array.Empty<Hausvorgabe?>()).Select(v => v?.Wappen).OfType<Wappen>())
            .Concat(haeuser.Select(h => h.Wappen).Where(w => !feste.Any(k => k.Wappen == w))).ToList(); // neu gewürfelte Kartenwappen
        // vergebene Hausfarben: zuerst der Spieler, dann die Wappen aus „Erweitert“ (und ohne Karte die mitspielenden festen Häuser)
        var farben = haeuser.Select(h => h.Hausfarbe).Append(start.Spieler.Farbe ?? spielerWappen.Hausfarbe())
            .Concat((vorgaben ?? Array.Empty<Hausvorgabe?>()).Select(v => v?.Wappen?.Hausfarbe()).OfType<string>()).ToList();
        var z = Besetzungsquelle(zufallsstartwert, ZweckKiHaeuser);
        for (var i = 0; i < fehlen; i++)
        {
            var geschlecht = z.Next(2) == 0 ? Geschlecht.Mann : Geschlecht.Frau;
            var w = Wappen.Zufall(z, wappen, farben); // keine schon vergebene Hauptfarbe, solange eine frei ist (17.11.2026)
            // Führt das Haus ein Wappen aus „Erweitert“, zählt das gewürfelte nicht (es wird nie getragen) – sonst fehlte den letzten Häusern eine freie Farbe.
            var platz = haeuser.Count;
            if (vorgaben is null || platz >= vorgaben.Count || vorgaben[platz]?.Wappen is null)
            {
                wappen.Add(w);
                farben.Add(w.Hausfarbe());
            }
            haeuser.Add(new Starthaus
            {
                Name = "", Gewuerfelt = true, Volk = Volk.Mensch, Geschlecht = geschlecht, Wappen = w, // die Farbe kommt aus dem Wappen (Partieaufbau, 17.11.2026)
                Betriebe = { new Startbetrieb { Art = art } },
            });
        }
        return haeuser;
    }

    /// <summary>Die eigene Zufallsquelle der Namen gewürfelter KI-Häuser (in der <see cref="Startaufstellung"/>).</summary>
    internal static Zufallsquelle Namensquelle(int zufallsstartwert) => Besetzungsquelle(zufallsstartwert, ZweckNamen);

    /// <summary>
    /// Mitspielende Häuser der Karte führen ihr Wappen aus der Karte – außer es gleicht dem des Spielers, einer Vorgabe aus „Erweitert“ oder dem
    /// eines vorher stehenden mitspielenden Hauses (Nutzer 15.11.2026: die KI würfelt neu, bis es anders ist). Dann würfelt das Haus ein neues aus
    /// einer eigenen Zufallsquelle, verschieden von allen anderen. Ein Haus mit eigener Vorgabe behält sie (die prüft <see cref="VorgabenPruefen"/>).
    /// Nicht mitspielende Häuser der Karte zählen nicht. Ohne Gleichstand bleibt alles wie in der Karte (kein Wurf).
    /// </summary>
    internal static List<Starthaus> KartenwappenEindeutig(List<Starthaus> haeuser, Wappen spielerWappen, IReadOnlyList<Hausvorgabe?>? vorgaben,
        int zufallsstartwert)
    {
        Wappen? Vorgabe(int i) => vorgaben is not null && i < vorgaben.Count ? vorgaben[i]?.Wappen : null;
        var fest = haeuser.Select((_, i) => Vorgabe(i)).OfType<Wappen>().Append(spielerWappen).ToList();
        var vergeben = new List<Wappen>();
        Zufallsquelle? z = null;
        var ergebnis = new List<Starthaus>(haeuser.Count);
        for (var i = 0; i < haeuser.Count; i++)
        {
            var h = haeuser[i];
            if (Vorgabe(i) is null && (fest.Contains(h.Wappen) || vergeben.Contains(h.Wappen)))
            {
                z ??= Besetzungsquelle(zufallsstartwert, ZweckKartenwappen);
                var belegt = fest.Concat(vergeben).Concat(haeuser.Select(k => k.Wappen)).ToList();
                // die Hauptfarben der anderen (17.11.2026): das neue Wappen meidet sie, solange eine Hausfarbe frei ist
                var farben = fest.Concat(vergeben).Concat(haeuser.Where((_, k) => k > i).Select(k => k.Wappen)).Select(w => w.Hausfarbe()).ToList();
                h = new Starthaus
                {
                    Name = h.Name, Volk = h.Volk, Glaube = h.Glaube, Geschlecht = h.Geschlecht, Wappen = Wappen.Zufall(z, belegt, farben), Farbe = h.Farbe,
                    Wohnhaus = h.Wohnhaus, Betriebe = h.Betriebe.ToList(), Gewuerfelt = h.Gewuerfelt,
                };
            }
            if (Vorgabe(i) is null) vergeben.Add(h.Wappen);
            ergebnis.Add(h);
        }
        return ergebnis;
    }

    /// <summary>
    /// Welche Wappen für ein Haus des Menüs „Erweitert“ schon vergeben sind (nur lesend, für die Sperre im Wappenfeld): die der Häuser der Karte (die ersten
    /// <paramref name="rivalen"/> der Startbelegung, solange ihre Zeile kein eigenes Wappen wählt), die gewählten der anderen Zeilen und das des Spielers.
    /// <paramref name="zeile"/> ist die Zeile (0 …), für die gefragt wird; sie selbst zählt nicht mit, -1 fragt für den Spieler (dessen Wappen zählt dann nicht mit).
    /// Wählt eine andere Zeile ein anderes Wappen, ist ihr altes (Kartenhaus) wieder frei.
    /// </summary>
    /// <param name="wahl">Das gewählte Wappen je Zeile (null = gewürfelt).</param>
    public static IReadOnlyList<(Wappen Wappen, string Haus)> VergebeneWappen(Startbelegung start, int rivalen, Wappen spielerWappen, string spielername,
        IReadOnlyList<Wappen?> wahl, int zeile)
    {
        var liste = new List<(Wappen, string)>();
        if (zeile >= 0) liste.Add((spielerWappen, spielername));
        for (var j = 0; j < rivalen; j++)
        {
            if (j == zeile) continue;
            var karte = j < start.Haeuser.Count ? start.Haeuser[j] : null;
            var wappen = (j < wahl.Count ? wahl[j] : null) ?? karte?.Wappen;
            if (wappen is not null) liste.Add((wappen, karte is { Name.Length: > 0 } ? karte.Name : $"KI-Haus {j + 1}"));
        }
        return liste;
    }

    /// <summary>
    /// Prüft die KI-Vorgaben („Erweitert“) gegen den Spieler und die mitspielenden Häuser: nicht mehr Vorgaben als KI-Häuser, Namen und Wappen
    /// eindeutig (auch gegen die Häuser der Karte, die nicht vorgegeben sind), gültige Wappen, Berufe eindeutig und nicht der des Spielers.
    /// Volk und Glaube prüft die <see cref="Startaufstellung"/> beim Würfeln.
    /// </summary>
    internal static void VorgabenPruefen(Wirtschaftsdaten daten, IReadOnlyList<Hausvorgabe?> vorgaben, IReadOnlyList<Starthaus> mitspielend,
        string spielername, Wappen spielerWappen, string spielerberuf)
    {
        if (vorgaben.Count > mitspielend.Count)
            throw new ArgumentException($"Es gibt {vorgaben.Count} KI-Vorgaben, aber nur {mitspielend.Count} KI-Häuser.");
        Hausvorgabe? V(int i) => i < vorgaben.Count ? vorgaben[i] : null;
        var namen = mitspielend.Select((h, i) => V(i)?.Name is { } n ? Partieaufbau.Hausname(n) : h.Gewuerfelt ? null : h.Name)
            .OfType<string>().Prepend(spielername).ToList();
        if (namen.GroupBy(n => n).FirstOrDefault(g => g.Count() > 1) is { } doppelt)
            throw new ArgumentException($"„{doppelt.Key}“ heißen zwei Häuser.");
        var wappen = mitspielend.Select((h, i) => (Haus: V(i)?.Name is { } vn ? Partieaufbau.Hausname(vn) : h.Gewuerfelt ? $"KI-Haus {i + 1}" : h.Name,
                                                   Wappen: V(i)?.Wappen ?? (h.Gewuerfelt ? null : h.Wappen)))
            .Where(x => x.Wappen is not null).Select(x => (x.Haus, Wappen: x.Wappen!)).Prepend((Haus: spielername, Wappen: spielerWappen)).ToList();
        foreach (var (_, w) in wappen)
            if (w.Fehler() is { } fehler) throw new ArgumentException(fehler);
        if (wappen.GroupBy(x => x.Wappen).FirstOrDefault(g => g.Count() > 1) is { } gleich)
            throw new ArgumentException($"Zwei Häuser führen dasselbe Wappen ({string.Join(" und ", gleich.Select(x => x.Haus))}).");
        var vergeben = new List<string> { spielerberuf };
        foreach (var b in vorgaben.Select(v => v?.Beruf).OfType<string>())
            if (Startaufstellung.BerufVergeben(daten, vergeben, b) is { } grund) throw new ArgumentException(grund);
    }

    // ── Stärke ──────────────────────────────────────────────────────────────

    /// <summary>Startgeld eines KI-Hauses dieser Stärke; null = der Wert aus wirtschaft.json wie bisher (Normal, oder die Stufe fehlt).</summary>
    public static double? Startgeld(Wirtschaftsdaten daten, KiStaerke staerke) =>
        daten.KiStaerke?.Stufe(staerke) is { } s ? daten.Allgemein.Startvermoegen * s.Startgeld : null;

    /// <summary>Das Profil eines KI-Hauses dieser Stärke: Normal (oder ohne Daten) unverändert.</summary>
    public static KiProfil Profil(KiProfil profil, Wirtschaftsdaten daten, KiStaerke staerke) =>
        daten.KiStaerke?.Stufe(staerke) is { } s
            ? profil with { Reserve = profil.Reserve * s.Reserve, Vorsicht = profil.Vorsicht * s.Vorsicht, MaxAmortisationJahre = profil.MaxAmortisationJahre * s.Amortisation }
            : profil;
}
