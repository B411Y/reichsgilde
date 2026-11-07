using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Buerger;

/// <summary>
/// Ein Gang eines Trägers mit einem Stück: <see cref="Platz"/> = welcher Träger (0 … n − 1), <see cref="Stueck"/> = Nummer des Hubs des Krans
/// (Entladen: das h-te abgeladene Stück, Beladen: das j-te aufgeladene). Alle Zeiten in Minuten seit Spielbeginn (mit Bruchteil).
/// Entladen: Der Kran setzt das Stück um <see cref="AufStapel"/> auf den Stapel, der Träger nimmt es um <see cref="VomStapel"/> auf, legt es um
/// <see cref="ImSchuppen"/> im Lagerschuppen ab und ist um <see cref="Zurueck"/> wieder am Stapel. Beladen: Der Träger nimmt es um
/// <see cref="ImSchuppen"/> im Schuppen auf, legt es um <see cref="AufStapel"/> auf den Stapel und ist um <see cref="Zurueck"/> wieder im Schuppen;
/// der Kran hebt es um <see cref="VomStapel"/> vom Stapel.
/// </summary>
public readonly record struct Hafengang(int Platz, int Stueck, double AufStapel, double VomStapel, double ImSchuppen, double Zurueck);

/// <summary>
/// Die Hafenarbeit an einem Besuch des Zierschiffs: <see cref="Entladen"/> (Niederländer) oder Beladen (Oberländer). Getragen wird von
/// <see cref="Beginn"/> bis <see cref="Ende"/> (Minuten seit Spielbeginn) in festen <see cref="Gaenge"/>n. Am Hafen sind die Träger in den
/// Stunden <see cref="VonStunde"/> bis <see cref="BisStunde"/> − 1 (Uhrzeit); sie kommen zur vollen Stunde und gehen zur nächsten nach dem Ende.
/// </summary>
public sealed record Hafenschicht(bool Entladen, long Fenster, double Beginn, double Ende, int VonStunde, int BisStunde, int Traeger, int Runde,
                                  IReadOnlyList<Hafengang> Gaenge)
{
    /// <summary>Stücke der Ladung.</summary>
    public int Stuecke => Gaenge.Count;

    /// <summary>Wie viele Stücke stehen zur Minute <paramref name="minute"/> auf dem Stapel am Steg (der Stapel ist nur Puffer zwischen Kran und Trägern)?</summary>
    public int StapelUm(double minute) => Gaenge.Count(g => g.AufStapel <= minute && minute < g.VomStapel);

    /// <summary>Wie viele Stücke liegen zur Minute <paramref name="minute"/> im Lagerschuppen (abgelegt bzw. noch nicht geholt)?</summary>
    public int SchuppenUm(double minute) => Entladen ? Gaenge.Count(g => g.ImSchuppen <= minute) : Gaenge.Count(g => minute < g.ImSchuppen);
}

/// <summary>
/// Hafenarbeit der Tagelöhner (Spiel-KI, 6.11.2026; Entladen Stufe 3, Option „Kern-Zustand“; Ablauf nach Entscheidung des Nutzers vom 6.11.):
/// Ruht der Schiffshandel, legt das Zierschiff zweimal am Tag an (<see cref="Zierfahrplan"/>). Solange es liegt – auch vor 8 Uhr –, tragen
/// 2–4 Tagelöhner Hand in Hand mit dem Kran: beim Niederländer vom Stapel in den Lagerschuppen, beim Oberländer aus dem Schuppen auf den Stapel.
/// Der Stapel ist nur Puffer; was beim Ablegen noch darauf steht, tragen sie danach weg. Zwischen den Besuchen liegt die Ware im Stapelhaus.
/// Ein Vorarbeiter (der älteste verfügbare Tagelöhner) beaufsichtigt am Stegkopf, trägt nicht, kommt früher und geht als Letzter.
///
/// <b>Ohne Wirkung auf die Wirtschaft:</b> Hafenarbeit ändert nur Ort (<see cref="Stadtort.Hafen"/>, Stelle <see cref="StelleTraeger"/> bzw.
/// <see cref="StelleVorarbeiter"/>) und Weg – nie die Handlung. Sie trifft nur Einwohner der Rolle, die gerade ruhen, flanieren, einkaufen oder
/// (bei Arbeitsort Markt) arbeiten – dort zählt der Ort für nichts; wer einkehrt oder betet, bleibt, wo er dafür ist. Bedürfnisse, Passanten der Diebe
/// (<see cref="Einwohner.PassantOhnePlan"/>) und der Generator der Bürger rechnen wie ohne Hafen (Test: Spielstand ohne Orte und Wege bitgleich).
/// Kein Lohn, keine Ware. Wer trägt, folgt fest aus Nummer, Tag und Besuch – ohne Würfel; der Zustand steckt in Ort und Weg der Einwohner und
/// braucht kein eigenes Feld im Spielstand.
/// </summary>
public sealed class Hafenarbeit
{
    /// <summary><see cref="Einwohner.Stelle"/> eines Trägers am Hafen.</summary>
    public const string StelleTraeger = "Stapel";

    /// <summary><see cref="Einwohner.Stelle"/> des Vorarbeiters am Hafen (steht am Stegkopf).</summary>
    public const string StelleVorarbeiter = "Stegkopf";

    /// <summary>Anteil eines Hubs, zu dem der Kran das Stück anhebt (wie <c>KranSenkenBis</c> in der Darstellung): Dann verlässt es den Stapel.</summary>
    public const double KranHebtBei = 0.42;

    private readonly Stadtwirtschaft _w;
    private readonly Einwohnerschaft _ew;
    private readonly List<Einwohner> _traeger = new();
    private Einwohner? _vorarbeiter;

    internal Hafenarbeit(Stadtwirtschaft w, Einwohnerschaft ew)
    {
        _w = w;
        _ew = ew;
    }

    private HafenarbeitDaten? Daten => _ew.IstAktiv ? _ew.Daten.Hafenarbeit : null;

    /// <summary>So viele Minuten nach der vollen Stunde ist jeder Träger sicher am Hafen: spätester Abmarsch + längster Weg in der Stadt.</summary>
    public static int Puffer(WegeDaten wege) => wege.Verzoegerung + wege.Stadt + wege.Streuung;

    /// <summary>Gibt es Hafenarbeit (Abschnitt „buerger.hafenarbeit“, ein Hafen mit Wasserweg, Schiffshandel ruht)? Im Winter ist der Hafen zu – siehe <see cref="Schicht"/>.</summary>
    public bool IstAktiv
    {
        get
        {
            if (Daten is null) return false;
            var sf = _w.Schiffahrt;
            if (!sf.Ruht || sf.Haefen.Count == 0) return false;
            var hafen = sf.Haefen[0].Kennung;
            return sf.RoutenLaenge(hafen, true) > 0 || sf.RoutenLaenge(hafen, false) > 0;
        }
    }

    /// <summary>Ist der Hafen an diesem Tag (Spieljahr) offen? Im Winter nicht (wie <see cref="Schiffahrt.HafenOffen"/>; gefroren ist der Fluss nur im Winter).</summary>
    private bool OffenAm(int jahr) =>
        !(_w.Jahreszeiten.IstAktiv && (Jahreszeit)((jahr - Spielzeit.StartJahr) % 4) == Jahreszeit.Winter);

    /// <summary>Die Hafenarbeit am Besuch im Fenster <paramref name="fenster"/> (zwei je Tag: morgens Entladen, nachmittags Beladen) – null ohne Hafenarbeit oder im Winter.</summary>
    public Hafenschicht? Schicht(long fenster)
    {
        if (fenster < 0 || !IstAktiv || Daten is not { } d) return null;
        var tag = (int)(fenster / 2);
        if (!OffenAm(Spielzeit.StartJahr + tag)) return null;
        return Planen(d, _ew.Daten.Wege, fenster, Traegerzahl(d, tag, (int)(fenster % 2)));
    }

    /// <summary>Wie viele Träger an diesem Besuch: fest aus Tag und Fensterhälfte, zwischen traegerVon und traegerBis.</summary>
    private static int Traegerzahl(HafenarbeitDaten d, int tag, int haelfte) =>
        d.TraegerVon + (int)(Einwohnerschaft.Wurf(0, tag, 500 + haelfte) * (d.TraegerBis - d.TraegerVon + 1));

    /// <summary>
    /// Plant die Gänge eines Besuchs (reine Rechnung). Entladen: Jedes Stück kommt mit dem Kran auf den Stapel; der Träger, der als Erster frei ist,
    /// nimmt es, sobald es steht. Beladen: rückwärts vom Kran her – jedes Stück liegt eine halbe Minute vor dem Hub auf dem Stapel, so spät wie möglich
    /// (der Stapel bleibt klein); der Träger, der als Letzter wieder gebraucht wird, bringt es.
    /// </summary>
    internal static Hafenschicht Planen(HafenarbeitDaten d, WegeDaten wege, long fenster, int n)
    {
        var (ankunft, liegen, _, _) = Zierfahrplan.Streuung(fenster);
        var a = (double)(Zierfahrplan.Anfang(fenster) + ankunft);
        var stuecke = d.Stuecke;
        var halb = d.Runde / 2.0;
        var gaenge = new Hafengang[stuecke];
        var entladen = Zierfahrplan.IstNieder(fenster);
        var tagesanfang = fenster / 2 * Spielzeit.MinutenProTag;
        double Uhr(double minute) => Spielzeit.TagesbeginnMinute + (minute - tagesanfang);
        var puffer = Puffer(wege);
        var huebe = (stuecke + Zierfahrplan.HubGroesse - 1) / Zierfahrplan.HubGroesse; // ein Hub des Krans hebt ein Bündel von HubGroesse Stücken
        int von;
        if (entladen)
        {
            var w = (Zierfahrplan.NiederEntladeBis - Zierfahrplan.EntladeAb) / huebe;
            // Sie kommen in der Stunde, aus der sie sicher vor dem ersten abgesetzten Stück da sind
            var ersterHub = a + liegen * (Zierfahrplan.EntladeAb + w);
            von = (int)Math.Floor((Uhr(ersterHub) - puffer) / 60);
            var da = tagesanfang + von * 60.0 - Spielzeit.TagesbeginnMinute + puffer;
            var frei = Enumerable.Repeat(da, n).ToArray();
            for (var h = 0; h < stuecke; h++)
            {
                var auf = a + liegen * (Zierfahrplan.EntladeAb + (h / Zierfahrplan.HubGroesse + 1) * w); // der Haken setzt das Bündel ab
                var p = 0;
                for (var i = 1; i < n; i++) if (frei[i] < frei[p]) p = i;
                var nimmt = Math.Max(auf + 0.25 + 0.05 * (h % Zierfahrplan.HubGroesse), frei[p]); // greift zu, sobald es steht (die Stücke eines Bündels nacheinander)
                gaenge[h] = new Hafengang(p, h, auf, nimmt, nimmt + halb, nimmt + d.Runde);
                frei[p] = nimmt + d.Runde;
            }
        }
        else
        {
            var wr = (Zierfahrplan.OberLadeBis - Zierfahrplan.OberLadeAb) / huebe;
            var gebraucht = Enumerable.Repeat(double.MaxValue, n).ToArray(); // spätester Beginn des nächsten Gangs je Träger
            for (var j = stuecke - 1; j >= 0; j--)
            {
                var hebt = a + liegen * (Zierfahrplan.OberLadeAb + (j / Zierfahrplan.HubGroesse + KranHebtBei) * wr); // der Haken hebt das ganze Bündel zugleich
                var p = 0;
                for (var i = 1; i < n; i++) if (gebraucht[i] > gebraucht[p]) p = i;
                var ab = Math.Min(hebt - 0.5 - 0.05 * (j % Zierfahrplan.HubGroesse), gebraucht[p] - halb); // legt auf den Stapel (die Stücke eines Bündels nacheinander)
                gaenge[j] = new Hafengang(p, j, ab, hebt, ab - halb, ab + halb);
                gebraucht[p] = ab - halb;
            }
            var erster = gaenge.Min(g => g.ImSchuppen);
            von = (int)Math.Floor((Uhr(erster) - puffer) / 60);
        }
        var beginn = gaenge.Min(g => entladen ? g.VomStapel : g.ImSchuppen);
        var ende = gaenge.Max(g => g.Zurueck);
        var bis = (int)Math.Ceiling(Uhr(ende) / 60);
        return new Hafenschicht(entladen, fenster, beginn, ende, von, bis, n, d.Runde, gaenge);
    }

    /// <summary>Die Hafenarbeit des Tages (Spieljahr) <paramref name="jahr"/>: morgens Entladen, nachmittags Beladen; leer ohne Hafenarbeit oder im Winter.</summary>
    public IReadOnlyList<Hafenschicht> Schichten(int jahr)
    {
        var tag = (long)(jahr - Spielzeit.StartJahr);
        var liste = new List<Hafenschicht>(2);
        for (var s = 0; s < 2; s++)
            if (Schicht(2 * tag + s) is { } x) liste.Add(x);
        return liste;
    }

    /// <summary>Die Schicht, in deren Stunden <paramref name="stunde"/> (Uhrzeit) am Tag <paramref name="jahr"/> fällt – sonst null.</summary>
    public Hafenschicht? SchichtInStunde(int jahr, int stunde) =>
        Schichten(jahr).FirstOrDefault(s => stunde >= s.VonStunde && stunde < s.BisStunde);

    /// <summary>Die Träger dieser Stunde, in ihrer festen Reihenfolge (Platz 0, 1, … der <see cref="Hafengang"/>e). Leer außerhalb der Schichten.</summary>
    public IReadOnlyList<Einwohner> Traeger => _traeger;

    /// <summary>Der Vorarbeiter dieser Stunde (beaufsichtigt am Stegkopf, trägt nicht) – null außerhalb der Schichten.</summary>
    public Einwohner? Vorarbeiter => _vorarbeiter;

    /// <summary>Arbeitet er gerade am Hafen bzw. ist dorthin unterwegs (Träger oder Vorarbeiter)?</summary>
    public static bool ArbeitetAmHafen(Einwohner e) => e.Ort == Stadtort.Hafen;

    /// <summary>Ist er der Vorarbeiter am Hafen?</summary>
    public static bool IstVorarbeiter(Einwohner e) => e.Ort == Stadtort.Hafen && e.Stelle == StelleVorarbeiter;

    /// <summary>Seine Reihenfolge unter den Trägern eines Besuchs: fest aus Nummer, Tag und Fensterhälfte (kleiner = früher an der Reihe).</summary>
    private static double Rang(Einwohner e, long fenster) => Einwohnerschaft.Wurf(e.Nummer, (int)(fenster / 2), 600 + (int)(fenster % 2));

    /// <summary>
    /// Kann er in dieser Stunde an den Hafen? Wer ruht, flaniert oder einkauft (dort zählt der Ort für keine Wirtschaft) und wer arbeitet, wenn seine
    /// Rolle am Markt arbeitet (in einer Schänke oder Kirche zählte er, und <c>OhnePlan</c> bildet die Arbeit auf den Markt zurück).
    /// </summary>
    private bool Frei(Einwohner e, string rolle) =>
        e.Rolle == rolle && (e.Handlung is Handlung.Ruhen or Handlung.Flanieren or Handlung.Einkaufen
                             || e.Handlung == Handlung.Arbeiten && _ew.Daten.Rollen.TryGetValue(rolle, out var r) && r.Arbeitsort == Stadtort.Markt);

    /// <summary>
    /// Zur vollen Stunde (aus <see cref="Einwohnerschaft"/>, nach der Wahl der Handlungen; <see cref="Einwohner.Ort"/> ist noch der der letzten
    /// Stunde): Wer ist in dieser Stunde am Hafen? Der Vorarbeiter (der der letzten Stunde, wenn er frei bleibt, sonst der älteste freie Einwohner
    /// der Rolle) und n Träger (die der letzten Stunde, die frei bleiben, dazu die nächsten nach dem Rang). Ohne Würfel.
    /// </summary>
    internal (IReadOnlyList<Einwohner> Traeger, Einwohner? Vorarbeiter) Waehlen(Spielzeit zeit, int stunde, IReadOnlyList<Einwohner> einwohner)
    {
        _traeger.Clear();
        _vorarbeiter = null;
        if (Daten is not { } d || SchichtInStunde(zeit.Jahr, stunde) is not { } s) return (_traeger, null);
        var frei = einwohner.Where(e => Frei(e, d.Rolle)).ToList();
        // Ein neuer Vorarbeiter kommt möglichst nicht aus den Trägern (sonst fehlte einer, bis Ersatz da ist)
        _vorarbeiter = frei.FirstOrDefault(IstVorarbeiter)
                       ?? frei.Where(e => e.Ort != Stadtort.Hafen).OrderBy(e => e.Geburtsjahr).ThenBy(e => e.Nummer).FirstOrDefault()
                       ?? frei.OrderBy(e => e.Geburtsjahr).ThenBy(e => e.Nummer).FirstOrDefault();
        var rest = frei.Where(e => !ReferenceEquals(e, _vorarbeiter)).OrderBy(e => Rang(e, s.Fenster)).ThenBy(e => e.Nummer).ToList();
        // Wer schon in der letzten Stunde trug, bleibt; frei gewordene Plätze füllen die nächsten nach dem Rang
        var bleiben = rest.Where(e => e.Ort == Stadtort.Hafen).Take(s.Traeger).ToList();
        _traeger.AddRange(bleiben.Concat(rest.Where(e => e.Ort != Stadtort.Hafen).Take(s.Traeger - bleiben.Count))
            .OrderBy(e => Rang(e, s.Fenster)).ThenBy(e => e.Nummer));
        return (_traeger, _vorarbeiter);
    }

    /// <summary>Nach dem Laden: Träger und Vorarbeiter aus Ort und Stelle der Einwohner (gleiche Reihenfolge wie beim Wählen).</summary>
    internal void Wiederherstellen(Spielzeit zeit, IReadOnlyList<Einwohner> einwohner)
    {
        _traeger.Clear();
        _vorarbeiter = null;
        if (Daten is null) return;
        // Ein Stand zur vollen Stunde enthält den Zustand vor deren Planung: dann gilt noch die Vorstunde
        var stunde = (zeit.UhrzeitInMinuten - (zeit.MinuteImTag > 0 && zeit.MinuteImTag % 60 == 0 ? 1 : 0)) / 60;
        if (SchichtInStunde(zeit.Jahr, stunde) is not { } s) return;
        _vorarbeiter = einwohner.FirstOrDefault(IstVorarbeiter);
        _traeger.AddRange(einwohner.Where(e => e.Ort == Stadtort.Hafen && !IstVorarbeiter(e))
            .OrderBy(e => Rang(e, s.Fenster)).ThenBy(e => e.Nummer));
    }
}
