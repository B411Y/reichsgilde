using Godot;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Runden der Streife (Teil der <c>partial class Stadtansicht</c>; Wachen und Diebe, Paket P5, Nutzer 06.11.2026). Die Wachleute, die der Kern auf
/// Streife hat (<see cref="Reichsgilde.Core.Ordnung.Wachwesen.AnwesenheitJetzt"/>), gehen nicht mehr alle dieselbe Runde, sondern je Wachwechsel und Nummer
/// eine von bis zu drei:
/// <list type="bullet">
/// <item><b>Markt</b>: Tür, Marktplatz und zurück.</item>
/// <item><b>Viertel</b>: drei Punkte der Gassen des eigenen Viertels (des Wachhauses; Viertel wie im Kern, <see cref="Amtsbefugnisse.ViertelBei"/>).</item>
/// <item><b>Mauer</b>: ein Stück Weg an der Stadtmauer (Gassenpunkte innerhalb von <see cref="MauerAbstand"/> m der Mauer, in der Reihenfolge entlang der
/// Mauer), wo die Karte eine Mauer mit Gassen daran hat; sonst entfällt diese Runde.</item>
/// </list>
/// Hat der Stadthauptmann die Streife verlegt (<see cref="Amtsbefugnisse.Streife"/>), gehen drei von vier Wachleuten in dessen Viertel, einer über den Markt:
/// die Streife ist dort sichtbar zu sehen.
/// <para><b>Keine Regel</b>: Die Wahl folgt nur aus Wachwechsel (zwei Stunden) und Nummer des Wachmanns, ohne Zufall und ohne Wirkung auf den Kern (der Schutz
/// der Streife bleibt stadtweit bzw. im Viertel des Hauptmanns, wie er ist); die Wege sind reine Darstellung.</para>
/// <para><b>Leistung</b>: Eine Runde wird je Haus, Art und Viertel nur einmal geplant und gemerkt (Wege des Gassennetzes sind ohnehin gemerkt); je Bild wird höchstens
/// eine neue Runde geplant, ein Wachmann ohne Runde wartet so lange am Wachhaus. Geplant wird erst, wenn ein Wachmann sie braucht, also erst wenn die Stadt gezeigt wird
/// (<see cref="Fenster.PassantenMenge"/> über 0, kein Raum). Gezeichnet und bewegt wird wie bisher mit den Sparstufen der Figuren (<see cref="FigurImBildBewegen"/>).</para>
/// Testhilfe: <c>--streifentest=markt|viertel|mauer</c> (alle Wachleute gehen diese Art) und <c>--streifenviertel=nord|ost|sued|west</c> (tut so, als habe der
/// Stadthauptmann die Streife dorthin verlegt; nur Bild), siehe <c>docs/STARTOPTIONEN.md</c>.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Gassenpunkte bis zu diesem Abstand (Meter) von der Mauer gehören zum Mauerweg.</summary>
    private const float MauerAbstand = 12f;

    /// <summary>Mindestabstand (Meter) zwischen zwei Haltepunkten einer Runde.</summary>
    private const float HalteAbstand = 22f;

    /// <summary>Die Linien der Stadtmauern der Karte (Spielfeld) und ob sie geschlossen sind – gefüllt beim Lesen der Karte.</summary>
    private readonly List<(Vector3[] Linie, bool Geschlossen)> _mauerLinien = new();

    private readonly Dictionary<(string Haus, Streifenart Art, Stadtviertel Viertel), (Vector3[] Runde, float Laenge)?> _streifenrunden = new();
    private int _streifePlanBild = -1;
    private static readonly bool StreifeLog = Wasserarten.Startoption("streifentest") is { Length: > 0 } || Wasserarten.Startoption("streifenviertel") is { Length: > 0 };
    private Streifenart? _streifeProbeArt;
    private Stadtviertel? _streifeProbeViertel;
    private bool _streifeProbeGelesen;

    /// <summary>Die Karte wird neu gelesen (Kartenwerkstatt): Mauerlinien, Mauerknoten und gemerkte Runden gelten nicht mehr.</summary>
    private void StreifeVergessen()
    {
        _mauerLinien.Clear();
        _mauerGruppen = null;
        _streifenrunden.Clear();
        _streifeFehl.Clear();
        _streifeWahl.Clear();
        _mauerWegHaus.Clear();
    }

    /// <summary>Fest gemerkte Wahl je Wachmann: gilt für den ganzen Wachwechsel (ein Wechsel des Hauptmanns wirkt erst ab der nächsten Schicht).</summary>
    private readonly Dictionary<(string Haus, int Nummer), (int Wechsel, Streifenart Art, Stadtviertel Viertel)> _streifeWahl = new();
    private readonly Dictionary<string, bool> _mauerWegHaus = new();
    /// <summary>Runden, die nicht zu planen waren: erst nach dieser Bildnummer wieder versuchen (die Karte kann sich ändern).</summary>
    private readonly Dictionary<(string, Streifenart, Stadtviertel), int> _streifeFehl = new();

    /// <summary>Darf in diesem Bild noch geplant werden? Höchstens eine Planung (Runde, Mauerpunkte) je Bild; verbraucht das Budget.</summary>
    private bool StreifeBudget()
    {
        if (_streifePlanBild == _figurenBild) return false;
        _streifePlanBild = _figurenBild;
        return true;
    }

    private void StreifeProbeLesen()
    {
        if (_streifeProbeGelesen) return;
        _streifeProbeGelesen = true;
        if (Wasserarten.Startoption("streifentest") is { Length: > 0 } art && Enum.TryParse<Streifenart>(art, true, out var a)) _streifeProbeArt = a;
        if (Wasserarten.Startoption("streifenviertel") is { Length: > 0 } v)
            _streifeProbeViertel = v.ToLowerInvariant() switch { "nord" => Stadtviertel.Nord, "ost" => Stadtviertel.Ost, "sued" => Stadtviertel.Sued, "west" => Stadtviertel.West, _ => null };
    }

    /// <summary>
    /// Art und Viertel der Runde für diesen Wachmann, einmal je Wachwechsel gemerkt (<paramref name="wechsel"/>: Stunde / 2). Null, solange das Budget des Bildes
    /// aufgebraucht ist (die Prüfung, ob das Haus einen Mauerweg hat, wird einmal je Haus gerechnet und zählt zum Budget).
    /// </summary>
    private (Streifenart Art, Stadtviertel Viertel)? StreifeWahl(Stadtwirtschaft w, Betrieb haus, int nummer, int wechsel)
    {
        var schluessel = (haus.Name, nummer);
        if (_streifeWahl.TryGetValue(schluessel, out var alt) && alt.Wechsel == wechsel) return (alt.Art, alt.Viertel);
        StreifeProbeLesen();
        var mauer = false;
        if (_streifeProbeArt is null)
        {
            if (!_mauerWegHaus.TryGetValue(haus.Name, out mauer))
            {
                if (!StreifeBudget()) return null;
                mauer = TuerAnker(haus.Name) is { } t && MauerPunkte(t, "Mauer").Count >= 3;
                _mauerWegHaus[haus.Name] = mauer;
            }
        }
        var eigenes = w.Befugnisse.ViertelVon(haus);
        var verlegt = _streifeProbeViertel ?? w.Befugnisse.Streife;
        var art = _streifeProbeArt ?? Streifengang.Waehlen(wechsel, nummer, verlegt is not null, mauer);
        var viertel = art == Streifenart.Viertel ? verlegt ?? eigenes : eigenes;
        _streifeWahl[schluessel] = (wechsel, art, viertel);
        return (art, viertel);
    }

    /// <summary>
    /// Die Runde (hin und auf demselben Weg zurück) für Haus, Art und Viertel, einmal gemerkt. Null, wenn sie nicht zu planen ist (keine Tür, kein Weg, keine
    /// Punkte) – dann wird ersatzweise die Marktrunde genommen. Je Bild höchstens eine neue Planung; sonst „noch nicht“ (<paramref name="geplant"/> falsch).
    /// </summary>
    private (Vector3[] Runde, float Laenge)? StreifeRunde(Stadtwirtschaft w, Betrieb haus, Streifenart art, Stadtviertel viertel, out bool geplant)
    {
        if (art != Streifenart.Viertel) viertel = Stadtviertel.Nord; // Markt- und Mauerrunde hängen nicht vom Viertel ab: je Haus nur eine
        var schluessel = (haus.Name, art, viertel);
        geplant = true;
        if (_streifenrunden.TryGetValue(schluessel, out var bekannt)) return bekannt;
        if (_streifeFehl.TryGetValue(schluessel, out var erstAb) && _figurenBild < erstAb) return null; // kürzlich gescheitert
        if (!StreifeBudget()) { geplant = false; return null; } // höchstens eine Planung je Bild
        (Vector3[] Runde, float Laenge)? runde = null;
        if (TuerAnker(haus.Name) is { } tuer && _netz.Knoten.Count > 0)
        {
            var start = tuer;
            var punkte = art switch
            {
                Streifenart.Viertel => ViertelPunkte(w, haus, tuer, viertel, out start),
                Streifenart.Mauer => MauerPunkte(tuer, haus.Name),
                _ => new List<Vector3> { (NaechsterMarkt(tuer)?.Mitte ?? _markt) },
            };
            if (punkte.Count == 0) { punkte = new List<Vector3> { (NaechsterMarkt(tuer)?.Mitte ?? _markt) }; start = tuer; }
            var hin = new List<Vector3> { start };
            var von = start;
            foreach (var ziel in punkte)
            {
                foreach (var punkt in _netz.Pfad(_netz.KnotenBei(von), _netz.KnotenBei(ziel)))
                    if (hin[^1].DistanceTo(punkt) > 0.05f) hin.Add(punkt);
                von = ziel;
            }
            if (hin.Count >= 2)
            {
                var ganz = hin.Concat(Enumerable.Reverse(hin).Skip(1)).ToArray();
                runde = (ganz, Gassennetz.Laenge(ganz));
            }
        }
        if (runde is null) _streifeFehl[schluessel] = _figurenBild + 900; // später neu versuchen (etwa alle 15 s)
        else _streifenrunden[schluessel] = runde;
        if (StreifeLog) GD.Print($"[streife] {haus.Name} ({w.Befugnisse.ViertelVon(haus)}): {art} {viertel}, " + (runde is { } r ? $"{r.Laenge / 2:0} m hin, Tür {TuerAnker(haus.Name)?.X:0}|{TuerAnker(haus.Name)?.Z:0}, Ziel {r.Runde[r.Runde.Length / 2].X:0}|{r.Runde[r.Runde.Length / 2].Z:0}" : "keine Runde"));
        return runde;
    }

    /// <summary>
    /// Wie weit (Meter) ein Wachmann in einer Schicht etwa kommt: zwei Stunden Spielzeit sind bei Tempo 1 rund 95 Sekunden, bei <see cref="WachTempo"/> also gut 140 m.
    /// Runden werden so gelegt, dass Haltepunkte in dieser Nähe liegen; ein Viertel, das weiter weg ist, beginnt dort (der Wachmann ist schon im Viertel stationiert).
    /// </summary>
    private const float StreifeSchichtWeg = 160f;

    /// <summary>Liegt das Viertel weiter als so viel (Meter) von der Tür, beginnt die Runde dort: Der Wachmann ist schon im Viertel stationiert und kommt nicht erst eine halbe Schicht lang durch fremde Gassen.</summary>
    private const float ViertelNaehe = 60f;

    /// <summary>
    /// Drei Gassenpunkte im Viertel (innerhalb der Mauer), fest aus dem Namen des Hauses gewählt, mindestens <see cref="HalteAbstand"/> auseinander, nach Nähe geordnet.
    /// Liegt kein Punkt des Viertels in <see cref="ViertelNaehe"/> m der Tür, beginnt die Runde am nächsten Punkt des Viertels (<paramref name="start"/>), sonst an der Tür.
    /// </summary>
    private List<Vector3> ViertelPunkte(Stadtwirtschaft w, Betrieb haus, Vector3 tuer, Stadtviertel viertel, out Vector3 start)
    {
        start = tuer;
        var kandidaten = _netz.Knoten.Where(k => w.Befugnisse.ViertelBei(k.X, k.Z) == viertel && InDerStadt(k)).ToList();
        if (kandidaten.Count == 0) kandidaten = _netz.Knoten.Where(k => w.Befugnisse.ViertelBei(k.X, k.Z) == viertel).ToList();
        if (kandidaten.Count == 0) return new List<Vector3>();
        var naechster = kandidaten.OrderBy(k => k.DistanceTo(tuer)).First();
        var anker = naechster.DistanceTo(tuer) > ViertelNaehe ? naechster : tuer;
        start = anker;
        var samen = haus.Name.Sum(c => c);
        var gewaehlt = new List<Vector3>();
        foreach (var k in kandidaten.Where(k => k.DistanceTo(anker) is > HalteAbstand and < StreifeSchichtWeg).OrderBy(k => Streuung(samen, (long)(k.X * 7 + k.Z * 13) + 1000)))
        {
            if (gewaehlt.Any(g => g.DistanceTo(k) < HalteAbstand)) continue;
            gewaehlt.Add(k);
            if (gewaehlt.Count == 3) break;
        }
        return Nahgeordnet(anker, gewaehlt);
    }

    /// <summary>
    /// Punkte der Gassen an der Mauer: ab dem Mauerpunkt, der der Tür am nächsten ist, bis zu drei weitere Haltepunkte entlang der Mauer (Richtung fest aus dem Namen),
    /// höchstens <see cref="StreifeSchichtWeg"/> m von der Tür. Leer, wenn die Mauer zu weit weg ist (kein Mauerweg für dieses Haus).
    /// </summary>
    private List<Vector3> MauerPunkte(Vector3 tuer, string haus)
    {
        var gruppen = MauerGruppen();
        if (gruppen.Count == 0) return new List<Vector3>();
        // die Mauer, an der der der Tür nächste Gassenpunkt liegt
        Gruppe? beste = null;
        var bestIndex = -1;
        var abstand = float.MaxValue;
        foreach (var g in gruppen)
            for (var i = 0; i < g.Knoten.Count; i++)
            {
                var d = g.Knoten[i].Ort.DistanceTo(tuer);
                if (d < abstand) { abstand = d; beste = g; bestIndex = i; }
            }
        if (beste is null || abstand > StreifeSchichtWeg) return new List<Vector3>();
        var richtung = Streuung(haus.Length, haus.Sum(c => c)) % 2 == 0 ? 1 : -1;
        var n = beste.Knoten.Count;
        List<Vector3> Gehen(int r)
        {
            var liste = new List<Vector3>();
            for (var i = 0; i < n && liste.Count < 4; i++)
            {
                var index = bestIndex + r * i;
                if (beste.Geschlossen) index = (index % n + n) % n;
                else if (index < 0 || index >= n) break; // offene Mauer: am Ende ist Schluss, der Wachmann kehrt um (die Runde läuft hin und zurück)
                var k = beste.Knoten[index].Ort;
                if (liste.Count > 0 && liste[^1].DistanceTo(k) < HalteAbstand) continue;
                if (k.DistanceTo(tuer) > StreifeSchichtWeg * 1.6f) break;
                liste.Add(k);
            }
            return liste;
        }
        var gewaehlt = Gehen(richtung);
        if (gewaehlt.Count < 3 && !beste.Geschlossen) { var andere = Gehen(-richtung); if (andere.Count > gewaehlt.Count) gewaehlt = andere; }
        return gewaehlt;
    }

    /// <summary>Die Gassenpunkte an einer Mauer, geordnet nach der Lage entlang der Linie.</summary>
    private sealed class Gruppe
    {
        public required bool Geschlossen { get; init; }
        public List<(Vector3 Ort, float Lage)> Knoten { get; } = new();
    }

    private List<Gruppe>? _mauerGruppen;

    /// <summary>
    /// Alle Gassenpunkte innerhalb von <see cref="MauerAbstand"/> m einer Mauer, je Mauerlinie getrennt (jeder Punkt gehört nur der nächsten Linie) und nach der
    /// Lage entlang der Linie geordnet. Einmal je Karte gerechnet (zählt zum Budget des Bildes, <see cref="StreifeBudget"/>, über den Aufrufer).
    /// </summary>
    private List<Gruppe> MauerGruppen()
    {
        if (_mauerGruppen is not null) return _mauerGruppen;
        var gruppen = _mauerLinien.Select(l => new Gruppe { Geschlossen = l.Geschlossen }).ToList();
        foreach (var k in _netz.Knoten)
        {
            var beste = float.MaxValue;
            var lage = 0f;
            var wahl = -1;
            for (var m = 0; m < _mauerLinien.Count; m++)
            {
                var (linie, geschlossen) = _mauerLinien[m];
                var strecken = linie.Length - 1 + (geschlossen ? 1 : 0);
                var davor = 0f;
                for (var i = 0; i < strecken; i++)
                {
                    var (a, b) = (linie[i], linie[(i + 1) % linie.Length]);
                    var q = Gassennetz.NaechsterPunkt(k, a, b);
                    var d = new Vector2(q.X - k.X, q.Z - k.Z).Length();
                    if (d < beste) { beste = d; lage = davor + a.DistanceTo(q); wahl = m; }
                    davor += a.DistanceTo(b);
                }
            }
            if (wahl >= 0 && beste <= MauerAbstand && beste >= 1.2f) gruppen[wahl].Knoten.Add((k, lage)); // nicht in der Mauer selbst (Tor, Torweg zählt nicht)
        }
        foreach (var g in gruppen) g.Knoten.Sort((x, y) => x.Lage.CompareTo(y.Lage));
        return _mauerGruppen = gruppen.Where(g => g.Knoten.Count > 0).ToList();
    }

    private static List<Vector3> Nahgeordnet(Vector3 start, List<Vector3> punkte)
    {
        var rest = new List<Vector3>(punkte);
        var ordnung = new List<Vector3>();
        var hier = start;
        while (rest.Count > 0)
        {
            var naechster = rest.OrderBy(p => p.DistanceTo(hier)).First();
            ordnung.Add(naechster);
            rest.Remove(naechster);
            hier = naechster;
        }
        return ordnung;
    }
}
