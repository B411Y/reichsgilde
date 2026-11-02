using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Ein Hafen der Karte (ein Gebäude am Ufer eines Flusses): Liegeplatz der Schiffe und Anfang ihrer Routen.
/// </summary>
/// <param name="Kennung">Kennung des Gebäudes in der Karte (für Befehle).</param>
/// <param name="Liegeplatz">Wo die Schiffe am Kai liegen (auf dem Wasser vor der Kaimauer).</param>
/// <param name="RichtungX">Vom Kai aufs Wasser (Einheitsvektor) – so liegen die Schiffe längsseits.</param>
/// <param name="Fluss">Name des Flusses.</param>
public sealed record Hafenplatz(string Kennung, string Name, Kartenort Liegeplatz, double RichtungX, double RichtungZ, string Fluss);

/// <summary>
/// Der Schiffshandel der Stadt (Abschnitt „schiffshandel“, PR #199): Schiffe der Häuser an den Häfen der Karte, ihre Fahrten zu den
/// fernen Städten am Kartenrand und deren Märkte. Ohne den Abschnitt, ohne Karte oder ohne Hafen in der Karte gibt es keinen
/// Schiffshandel (<see cref="IstAktiv"/>).
///
/// Ablauf einer Fahrt (siehe <see cref="Schiff"/>):
///   • Auftrag: Die Hinfracht kommt aus dem Lager eines eigenen Betriebs oder wird am Markt gekauft (wie der Handel von Hand, mit
///     Händlerspanne) und geladen; nach der Ladezeit legt das Schiff ab.
///   • Jede beladene Fahrt (hin und zurück): Zoll an jeder Zollstelle (Anteil vom Wert der Ladung zum Marktpreis der Stadt), Geleit
///     (Eskorte der Karren × <see cref="SchiffshandelDaten.GeleitFaktor"/>), selten Flusspiraten (wie die Banden an den Karren).
///   • In der fernen Stadt: alles verkaufen, die Rückfracht kaufen, so weit das Geld reicht; dann die Liegezeit.
///   • Daheim: Die Rückfracht geht ins Lager des Betriebs (was nicht passt, auf den Markt) oder ohne Betrieb auf den Markt.
///   • Zum Jahresende: Unterhalt je Schiff (Fixkosten).
/// Das Wetter wirkt nicht (Entscheidung zu PR #195). Im strengen Winter friert der Fluss zu (Datenversion 67, <see cref="Jahreszeitenwesen"/>): Dann nimmt
/// der Hafen keine Aufträge an, und jedes Schiff, das nicht am Kai liegt, ruht – seine Zeiten rücken Minute für Minute mit.
/// </summary>
public sealed class Schiffahrt
{
    private readonly Stadtwirtschaft _w;
    private readonly List<Schiff> _schiffe = new();
    private readonly List<FerneStadt> _staedte = new();
    private int _getauft;

    // Häfen und Routen folgen aus der Karte – einmal gerechnet, nicht gespeichert.
    private List<Hafenplatz>? _haefen;
    private readonly Dictionary<(string Hafen, bool Flussauf), (List<Kartenort> Punkte, double Laenge)> _routen = new();

    public SchiffshandelDaten? Daten { get; }

    /// <summary>Gibt es Schiffshandel? Der Abschnitt in den Daten, eine Karte und mindestens ein Hafen an einem Fluss.</summary>
    public bool IstAktiv => Daten is not null && Haefen.Count > 0;

    internal Schiffahrt(Stadtwirtschaft w, SchiffshandelDaten? daten)
    {
        _w = w;
        Daten = daten;
        if (daten is not null)
            foreach (var s in daten.Staedte)
                _staedte.Add(new FerneStadt(s, w.Daten));
    }

    /// <summary>Die fernen Städte mit ihren Märkten.</summary>
    public IReadOnlyList<FerneStadt> Staedte => _staedte;

    public FerneStadt? Stadt(string name) => _staedte.FirstOrDefault(s => s.Name == name);

    /// <summary>Alle Schiffe aller Häuser, in der Reihenfolge des Kaufs.</summary>
    public IReadOnlyList<Schiff> Schiffe => _schiffe;

    public IEnumerable<Schiff> SchiffeVon(Konto konto) => _schiffe.Where(s => s.Besitzer == konto);

    public Schiff? Schiff(string name) => _schiffe.FirstOrDefault(s => s.Name == name);

    // ═════════════════════════════════════════════════════════════════════════
    //  Häfen und Routen (aus der Karte)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Wie weit (Meter) der Liegeplatz vor der Kaimauer im Wasser liegt.</summary>
    public const double LiegeplatzAbstand = 3;

    /// <summary>Abstand der Punkte einer Route (Meter).</summary>
    public const double RoutenSchritt = 4;

    /// <summary>
    /// Die Häfen der Karte: Gebäude am Ufer (Katalog mit <c>amUfer</c>, z. B. „hafenplatz“) oder der Hafen von Köln (Katalog „hafen“), deren
    /// Kaimauer bzw. Mitte höchstens <see cref="SchiffshandelDaten.HafenAbstand"/> Meter von einem Fluss (Art „fluss“, kein Bach) entfernt liegt.
    /// </summary>
    public IReadOnlyList<Hafenplatz> Haefen => _haefen ??= HaefenSuchen();

    public Hafenplatz? Hafen(string kennung) => Haefen.FirstOrDefault(h => h.Kennung == kennung);

    private List<Hafenplatz> HaefenSuchen()
    {
        var liste = new List<Hafenplatz>();
        if (Daten is null || _w.Karte is not { } karte) return liste;
        var fluesse = karte.Wasser.Any(g => g.Art == "fluss")
            ? karte.Gelaendeform.Wasser.Laeufe.Where(l => l.Art == "fluss" && l.Punkte.Count >= 2).ToList()
            : new List<Bauen.Wasserlauf>();
        if (fluesse.Count == 0) return liste;
        foreach (var g in karte.Gebaeude)
        {
            // Hafen ist nur der Hafenplatz (bzw. „hafen“ auf Köln); Zollstelle und Hafenkran stehen auch am Ufer, sind aber keiner
            if (g.Katalog != Kartenentwurf.HafenKatalog && g.Katalog != "hafen") continue;
            var amUfer = g.Baustein?.AmUfer == true || g.Katalog == Kartenentwurf.HafenKatalog;
            Kartenort anker;
            (double X, double Z)? nach = null;
            if (amUfer)
            {
                var groesse = g.Baustein?.Groesse is { Length: 2 } gr ? gr : new double[] { 16, 14 };
                var kai = Bauen.Kaimauer.Von(g.Kennung, g.Punkt[0], g.Punkt[1], g.Drehung, groesse[0] * g.Massstab, groesse[1] * g.Massstab);
                anker = new Kartenort((kai.A.X + kai.B.X) / 2, (kai.A.Z + kai.B.Z) / 2);
                nach = (kai.NachX, kai.NachZ);
            }
            else anker = new Kartenort(g.Punkt[0], g.Punkt[1]);

            // Der nächste Punkt eines Flusses
            Bauen.Wasserlauf? bester = null;
            var (k, abstand) = (-1, double.MaxValue);
            foreach (var lauf in fluesse)
                for (var i = 0; i < lauf.Punkte.Count; i++)
                {
                    var d = lauf.Punkte[i].Ort.Abstand(anker);
                    if (d < abstand) (bester, k, abstand) = (lauf, i, d);
                }
            if (bester is null || abstand > Daten.HafenAbstand) continue;

            var mitte = bester.Punkte[k].Ort;
            if (nach is null)
            {
                var (dx, dz) = (mitte.X - anker.X, mitte.Z - anker.Z);
                var l = Math.Sqrt(dx * dx + dz * dz);
                nach = l < 1e-6 ? (1, 0) : (dx / l, dz / l);
            }
            var liegeplatz = amUfer ? new Kartenort(anker.X + nach.Value.X * LiegeplatzAbstand, anker.Z + nach.Value.Z * LiegeplatzAbstand) : mitte;
            var hafen = new Hafenplatz(g.Kennung, g.Name != "" ? g.Name : "Hafen", liegeplatz, nach.Value.X, nach.Value.Z, bester.Name);
            liste.Add(hafen);

            // Die Routen: vom Liegeplatz zur Mitte des Flusses, dann mit bzw. gegen die Strömung bis zum Ende der Linie (hinter dem Kartenrand).
            foreach (var flussauf in new[] { false, true })
            {
                var punkte = new List<Kartenort> { liegeplatz };
                var folge = flussauf ? Enumerable.Range(0, k + 1).Reverse() : Enumerable.Range(k, bester.Punkte.Count - k);
                var letzter = bester.Punkte[flussauf ? 0 : bester.Punkte.Count - 1].Ort;
                foreach (var i in folge)
                {
                    var p = bester.Punkte[i].Ort;
                    if (p.Abstand(punkte[^1]) >= RoutenSchritt || i == (flussauf ? 0 : bester.Punkte.Count - 1)) punkte.Add(p);
                }
                if (punkte[^1] != letzter) punkte.Add(letzter);
                var laenge = 0.0;
                for (var i = 1; i < punkte.Count; i++) laenge += punkte[i].Abstand(punkte[i - 1]);
                _routen[(g.Kennung, flussauf)] = (punkte, laenge);
            }
        }
        return liste;
    }

    /// <summary>
    /// Die Route eines Hafens flussauf oder flussab: Punkte (etwa alle <see cref="RoutenSchritt"/> m) vom Liegeplatz über die Mitte des Flusses
    /// bis zum Ende seiner Linie hinter dem Kartenrand. Leer, wenn es den Hafen nicht gibt.
    /// </summary>
    public IReadOnlyList<Kartenort> Route(string hafen, bool flussauf) =>
        Haefen.Count > 0 && _routen.TryGetValue((hafen, flussauf), out var r) ? r.Punkte : Array.Empty<Kartenort>();

    /// <summary>Die Route zur fernen Stadt (ihrer Richtung nach).</summary>
    public IReadOnlyList<Kartenort> Route(string hafen, FerneStadt stadt) => Route(hafen, stadt.Daten.IstFlussauf);

    /// <summary>Länge einer Route in Metern.</summary>
    public double RoutenLaenge(string hafen, bool flussauf) =>
        Haefen.Count > 0 && _routen.TryGetValue((hafen, flussauf), out var r) ? r.Laenge : 0;

    // ═════════════════════════════════════════════════════════════════════════
    //  Abfragen für Darstellung und Oberfläche
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Spielminuten für die Fahrt zu dieser Stadt (hin) bzw. von ihr zurück – flussauf langsamer als flussab.</summary>
    public static int Fahrzeit(SchiffstypDaten typ, FerneStadt stadt, bool hin)
    {
        var gegenDenStrom = stadt.Daten.IstFlussauf == hin; // hin zur Stadt flussauf oder zurück von einer Stadt flussab
        return Math.Max(1, (int)Math.Round(stadt.Daten.Entfernung * (gegenDenStrom ? typ.MinutenFlussauf : typ.MinutenFlussab)));
    }

    /// <summary>Spielminuten einer ganzen Fahrt: beladen, hin, liegen, zurück.</summary>
    /// <param name="hafenfaktor">Faktor auf Lade- und Liegezeit (erweiterter Hafen, <see cref="Politik.Ratsantraege.Hafenfaktor"/>).</param>
    public static int Rundreise(SchiffstypDaten typ, FerneStadt stadt, double hafenfaktor = 1) =>
        Gerundet(typ.LadeMinuten * hafenfaktor) + Fahrzeit(typ, stadt, true) + Gerundet(typ.LiegeMinuten * hafenfaktor) + Fahrzeit(typ, stadt, false);

    private static int Gerundet(double minuten) => (int)Math.Round(minuten, MidpointRounding.AwayFromZero);

    /// <summary>Wann ist das Schiff (voraussichtlich) wieder am Kai? Spielminute; am Kai ohne Auftrag die aktuelle.</summary>
    public long ZurueckUm(Schiff schiff)
    {
        var jetzt = _w.Uhr.VergangeneMinuten;
        if (schiff.Auftrag is not { } a || Stadt(a.Ziel) is not { } stadt) return jetzt;
        return schiff.Zustand switch
        {
            SchiffZustand.Beladen => schiff.Ende + Fahrzeit(schiff.Daten, stadt, true) + LiegeMinuten(schiff.Daten) + Fahrzeit(schiff.Daten, stadt, false),
            SchiffZustand.Hinfahrt => schiff.Ende + LiegeMinuten(schiff.Daten) + Fahrzeit(schiff.Daten, stadt, false),
            SchiffZustand.InFernerStadt => schiff.Ende + Fahrzeit(schiff.Daten, stadt, false),
            SchiffZustand.Rueckfahrt => schiff.Ende,
            _ => jetzt,
        };
    }

    /// <summary>
    /// Wo das Schiff zur Spielminute <paramref name="minute"/> zu sehen ist – mit Bruchteilen, damit die Darstellung zwischen den Minuten
    /// gleiten kann (z. B. <c>VergangeneMinuten + Anteil der laufenden Minute</c>). Auf der Karte fährt es mit
    /// <see cref="SchiffstypDaten.MeterJeMinute"/> (flussauf langsamer), mindestens aber so schnell, dass es die Route in der Fahrtzeit schafft;
    /// hinter dem Ende der Route (Kartenrand) und in der fernen Stadt ist es unsichtbar.
    /// </summary>
    public Schiffslage Lage(Schiff schiff, double minute)
    {
        var hafen = Hafen(schiff.Hafen);
        var amKai = hafen is null ? new Schiffslage(false, default, 1, 0, 0) : new Schiffslage(true, hafen.Liegeplatz, hafen.RichtungX, hafen.RichtungZ, 0);
        if (schiff.Zustand is SchiffZustand.AmKai or SchiffZustand.Beladen || schiff.Auftrag is not { } a || Stadt(a.Ziel) is not { } stadt || hafen is null)
            return amKai;
        if (schiff.Zustand == SchiffZustand.InFernerStadt)
            return amKai with { Sichtbar = false, Fortschritt = 1 };

        var hin = schiff.Zustand == SchiffZustand.Hinfahrt;
        var dauer = Math.Max(1, schiff.Ende - schiff.Beginn);
        var fortschritt = Math.Clamp((minute - schiff.Beginn) / dauer, 0, 1);
        var flussauf = stadt.Daten.IstFlussauf;
        var route = Route(hafen.Kennung, flussauf);
        var laenge = RoutenLaenge(hafen.Kennung, flussauf);
        if (route.Count < 2 || laenge <= 0) return amKai with { Sichtbar = false, Fortschritt = fortschritt };

        var gegenDenStrom = flussauf == hin;
        var tempo = schiff.Daten.MeterJeMinute * (gegenDenStrom ? (double)schiff.Daten.MinutenFlussab / schiff.Daten.MinutenFlussauf : 1);
        tempo = Math.Max(tempo, laenge / dauer);
        // Abstand vom Liegeplatz entlang der Route: hin wächst er vom Ablegen an, zurück schrumpft er bis zur Ankunft.
        var stelle = hin ? fortschritt * dauer * tempo : (1 - fortschritt) * dauer * tempo;
        if (stelle >= laenge) return amKai with { Sichtbar = false, Fortschritt = fortschritt };

        var (ort, rx, rz) = AufRoute(route, stelle);
        return new Schiffslage(true, ort, hin ? rx : -rx, hin ? rz : -rz, fortschritt);
    }

    /// <summary>Der Punkt in diesem Abstand vom Anfang der Route und die Richtung dort (vom Anfang weg).</summary>
    private static (Kartenort Ort, double X, double Z) AufRoute(IReadOnlyList<Kartenort> route, double stelle)
    {
        for (var i = 1; i < route.Count; i++)
        {
            var (a, b) = (route[i - 1], route[i]);
            var l = a.Abstand(b);
            if (l <= 1e-9) continue;
            if (stelle <= l || i == route.Count - 1)
            {
                var t = Math.Clamp(stelle / l, 0, 1);
                return (new Kartenort(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t), (b.X - a.X) / l, (b.Z - a.Z) / l);
            }
            stelle -= l;
        }
        return (route[^1], 1, 0);
    }

    /// <summary>Kurzer Text zum Zustand für die Anzeige.</summary>
    public string ZustandText(Schiff schiff)
    {
        var jetzt = _w.Uhr.VergangeneMinuten;
        var ziel = schiff.Auftrag?.Ziel ?? "";
        var noch = Math.Max(0, schiff.Ende - jetzt);
        return schiff.Zustand switch
        {
            SchiffZustand.AmKai => "liegt am Kai",
            SchiffZustand.Beladen => $"wird beladen (legt in {noch} Min. ab nach {ziel})",
            SchiffZustand.Hinfahrt => $"unterwegs nach {ziel} (noch {noch} Min.)",
            SchiffZustand.InFernerStadt => $"liegt in {ziel} (noch {noch} Min.)",
            SchiffZustand.Rueckfahrt => $"auf der Rückfahrt von {ziel} (noch {noch} Min.)",
            _ => schiff.Zustand.ToString(),
        };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Befehle
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Namen für neue Schiffe (danach „Flussschiff 13“ …).</summary>
    private static readonly string[] Taufnamen =
    {
        "Sankt Nikolaus", "Gute Hoffnung", "Möwe", "Sankt Christophorus", "Fortuna", "Eisvogel",
        "Sankt Barbara", "Wildente", "Treue Magd", "Graureiher", "Sankt Klemens", "Silberfisch",
    };

    /// <summary>Warum dieses Haus hier kein Schiff kaufen kann – oder null.</summary>
    /// <param name="hafen">Kennung des Hafens; "" = der erste.</param>
    /// <param name="typ">Schiffstyp; "" = der erste in den Daten.</param>
    public string? KaufVerboten(Konto konto, string hafen = "", string typ = "")
    {
        if (Daten is null) return "In dieser Stadt gibt es keinen Schiffshandel.";
        if (Haefen.Count == 0) return "Die Stadt hat keinen Hafen an einem Fluss.";
        if (hafen != "" && Hafen(hafen) is null) return $"Einen Hafen „{hafen}“ gibt es nicht.";
        if (typ != "" && !Daten.Schiffe.ContainsKey(typ)) return $"Den Schiffstyp „{typ}“ gibt es nicht.";
        var preis = Daten.Schiffe[typ != "" ? typ : Daten.Schiffe.Keys.First()].Preis;
        if (konto.Vermoegen < preis)
            return $"Ein Schiff kostet {Zahlenformat.Ganz(preis)} {_w.Daten.Allgemein.Waehrung} – es fehlen {Zahlenformat.Ganz(preis - konto.Vermoegen)}.";
        return null;
    }

    /// <summary>Warum dieser Auftrag nicht geht – oder null. Dieselbe Prüfung wie beim Befehl (für die Oberfläche vorab).</summary>
    public string? AuftragVerboten(Konto konto, string schiffName, Schiffsauftrag a)
    {
        if (!IstAktiv) return "In dieser Stadt gibt es keinen Schiffshandel.";
        if (Schiff(schiffName) is not { } schiff) return $"Ein Schiff „{schiffName}“ gibt es nicht.";
        if (schiff.Besitzer != konto) return $"{schiff.Name} gehört nicht {konto.Inhaber}.";
        if (schiff.Zustand != SchiffZustand.AmKai) return $"{schiff.Name} ist unterwegs – erst wenn es wieder am Kai liegt.";
        if (_w.Jahreszeiten.FlussZugefroren)
            return $"Der {_w.Jahreszeiten.Flussname} ist zugefroren – der Hafen ist geschlossen, bis das Eis bricht.";
        if (Stadt(a.Ziel) is not { } stadt) return $"Eine ferne Stadt „{a.Ziel}“ gibt es nicht.";
        if (a.Hinfracht.Concat(a.Rueckfracht).Any(x => x.Value < 0)) return "Mengen dürfen nicht negativ sein.";
        var hin = a.Hinfracht.Where(x => x.Value > 0).ToList();
        var rueck = a.Rueckfracht.Where(x => x.Value > 0).ToList();
        if (hin.Count == 0 && rueck.Count == 0) return "Der Auftrag ist leer.";
        foreach (var (ware, _) in hin)
            if (!stadt.Kauft(ware)) return $"{stadt.Name} kauft kein {ware}.";
        foreach (var (ware, _) in rueck)
            if (!stadt.Verkauft(ware)) return $"In {stadt.Name} gibt es kein {ware} zu kaufen.";
        int Raeume(IEnumerable<KeyValuePair<string, int>> teil) => teil.Sum(x => (x.Value + schiff.Daten.KapazitaetJeSorte - 1) / schiff.Daten.KapazitaetJeSorte);
        var fasst = $"{schiff.Name} fasst {schiff.Daten.Sorten} × {schiff.Daten.KapazitaetJeSorte} Stück (je Laderaum eine Ware).";
        if (Raeume(hin) > schiff.Daten.Sorten) return "Zu viel Hinfracht: " + fasst;
        if (Raeume(rueck) > schiff.Daten.Sorten) return "Zu viel Rückfracht: " + fasst;
        Betrieb? betrieb = null;
        if (a.Betrieb is { } name)
        {
            betrieb = _w.Betriebe.FirstOrDefault(b => b.Name == name);
            if (betrieb is null) return $"Einen Betrieb „{name}“ gibt es nicht.";
            if (betrieb.Konto != konto) return $"{betrieb.Name} gehört nicht {konto.Inhaber}.";
            foreach (var (ware, stueck) in hin)
                if (betrieb.ImLager(ware) < stueck) return $"Im Lager von {betrieb.Name} liegen nur {betrieb.ImLager(ware)} {ware}.";
        }
        var waehrung = _w.Daten.Allgemein.Waehrung;
        var einkauf = betrieb is null ? hin.Sum(x => _w.HandKaufpreis(konto, x.Key, x.Value, Daten!.HeimatmarktAnteil)) : 0;
        if (einkauf > konto.Vermoegen)
            return $"Die Hinfracht kostet am Markt etwa {Zahlenformat.Ganz(einkauf)} {waehrung} – so viel habt ihr nicht.";
        var erloes = hin.Sum(x => stadt.Erloes(x.Key, x.Value)) * _w.Familien.VerkaufsFaktor(konto);
        var rueckkosten = rueck.Sum(x => stadt.Kosten(x.Key, x.Value)) * _w.Familien.EinkaufsFaktor(konto);
        if (rueckkosten > konto.Vermoegen - einkauf + erloes)
            return $"Die Rückfracht kostet in {stadt.Name} etwa {Zahlenformat.Ganz(rueckkosten)} {waehrung} – so viel habt ihr nicht.";
        return null;
    }

    /// <summary>Führt einen Befehl des Schiffshandels aus (aufgerufen von <see cref="Stadtwirtschaft.Ausfuehren"/>).</summary>
    internal Befehlsergebnis Ausfuehren(Konto konto, SchiffahrtBefehl befehl)
    {
        if (Daten is null || !IstAktiv) return Befehlsergebnis.Abgelehnt(Daten is null ? "In dieser Stadt gibt es keinen Schiffshandel." : "Die Stadt hat keinen Hafen an einem Fluss.");
        var waehrung = _w.Daten.Allgemein.Waehrung;
        switch (befehl)
        {
            case SchiffKaufen k:
            {
                if (KaufVerboten(konto, k.Hafen, k.Typ) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
                var typ = k.Typ != "" ? k.Typ : Daten.Schiffe.Keys.First();
                var hafen = k.Hafen != "" ? Hafen(k.Hafen)! : Haefen[0];
                var schiff = new Schiff(Taufen(typ), typ, Daten.Schiffe[typ], konto, hafen.Kennung);
                konto.Investition(schiff.Daten.Preis);
                _schiffe.Add(schiff);
                _w.Melden($"{konto.Inhaber} kauft das {typ} „{schiff.Name}“ am {hafen.Name}.");
                return Befehlsergebnis.Ok($"Das {typ} „{schiff.Name}“ liegt am {hafen.Name} ({Zahlenformat.Ganz(schiff.Daten.Preis)} {waehrung}, " +
                                          $"Unterhalt {Zahlenformat.Ganz(schiff.Daten.Unterhalt)} im Jahr).");
            }
            case SchiffVerkaufen v:
            {
                if (EigenesSchiff(konto, v.Schiff, out var schiff) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
                if (schiff.Zustand != SchiffZustand.AmKai) return Befehlsergebnis.Abgelehnt($"{schiff.Name} ist unterwegs – verkaufen geht nur am Kai.");
                var erloes = schiff.Daten.Preis * Daten.VerkaufAnteil;
                konto.Verkauf(erloes);
                _schiffe.Remove(schiff);
                return Befehlsergebnis.Ok($"{schiff.Name} ist verkauft ({Zahlenformat.Ganz(erloes)} {waehrung}).");
            }
            case SchiffGeleitWaehlen g:
            {
                if (EigenesSchiff(konto, g.Schiff, out var schiff) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
                if (!_w.Daten.Logistik.Eskorten.ContainsKey(g.Geleit)) return Befehlsergebnis.Abgelehnt($"Das Geleit „{g.Geleit}“ gibt es nicht.");
                if (!_w.Wache.EskorteVerfuegbar(g.Geleit))
                    return Befehlsergebnis.Abgelehnt($"Die {g.Geleit} gibt Geleitschutz erst, wenn ein Haus ein Wachhaus mit Leuten führt.");
                schiff.Geleit = g.Geleit;
                return Befehlsergebnis.Ok($"{schiff.Name} fährt nun mit Geleit „{g.Geleit}“ " +
                                          $"({Zahlenformat.Ganz(GeleitKosten(g.Geleit))} {waehrung} je beladener Fahrt).");
            }
            case SchiffsauftragBeenden e:
            {
                if (EigenesSchiff(konto, e.Schiff, out var schiff) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
                if (schiff.Auftrag is not { Wiederholen: true } a) return Befehlsergebnis.Abgelehnt($"{schiff.Name} fährt keinen wiederkehrenden Auftrag.");
                schiff.Auftrag = a with { Wiederholen = false };
                return Befehlsergebnis.Ok($"{schiff.Name} bleibt nach dieser Fahrt am Kai.");
            }
            case SchiffsauftragErteilen a:
            {
                var auftrag = new Schiffsauftrag(a.Ziel, new Dictionary<string, int>(a.Hinfracht), new Dictionary<string, int>(a.Rueckfracht), a.Betrieb, a.Wiederholen);
                if (AuftragVerboten(konto, a.Schiff, auftrag) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
                return AuftragStarten(Schiff(a.Schiff)!, auftrag);
            }
            default:
                return Befehlsergebnis.Abgelehnt($"Unbekannter Befehl: {befehl.GetType().Name}");
        }
    }

    private string? EigenesSchiff(Konto konto, string name, out Schiff schiff)
    {
        schiff = Schiff(name)!;
        if (schiff is null) return $"Ein Schiff „{name}“ gibt es nicht.";
        return schiff.Besitzer != konto ? $"{schiff.Name} gehört nicht {konto.Inhaber}." : null;
    }

    private string Taufen(string typ)
    {
        while (true)
        {
            var name = _getauft < Taufnamen.Length ? Taufnamen[_getauft] : $"{typ} {_getauft + 1}";
            _getauft++;
            if (Schiff(name) is null) return name;
        }
    }

    /// <summary>An so vielen Zollstellen zahlt dieses Haus je beladener Fahrt – der Zöllner nicht an der ersten, die der Stadt gehört (PR #200).</summary>
    public int Zollstellen(Konto konto, FerneStadt stadt) =>
        stadt.Daten.Zollstellen > 0 && _w.Befugnisse.IstZollfrei(konto) ? stadt.Daten.Zollstellen - 1 : stadt.Daten.Zollstellen;

    /// <summary>Ladezeit am Kai – mit erweitertem Hafen kürzer (Bauvorhaben der Stadt, PR #200).</summary>
    public int LadeMinuten(SchiffstypDaten typ) => Gerundet(typ.LadeMinuten * _w.Ratsantraege.Hafenfaktor);

    /// <summary>Liegezeit in der fernen Stadt – die Mannschaft ist eingespielt, wenn der Hafen erweitert ist.</summary>
    public int LiegeMinuten(SchiffstypDaten typ) => Gerundet(typ.LiegeMinuten * _w.Ratsantraege.Hafenfaktor);

    /// <summary>Was das Geleit einer beladenen Fahrt kostet (Gulden).</summary>
    public double GeleitKosten(string geleit) =>
        _w.Daten.Logistik.Eskorten.TryGetValue(geleit, out var e) ? e.KostenJeFahrt * (Daten?.GeleitFaktor ?? 0) : 0;

    /// <summary>Geprüft: Hinfracht laden (aus dem Lager oder vom Markt), dann die Ladezeit.</summary>
    private Befehlsergebnis AuftragStarten(Schiff schiff, Schiffsauftrag a)
    {
        var konto = schiff.Besitzer;
        var betrieb = a.Betrieb is { } name ? _w.Betriebe.First(b => b.Name == name) : null;
        double einkauf = 0;
        foreach (var (ware, stueck) in a.Hinfracht.Where(x => x.Value > 0).OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (betrieb is not null)
                schiff.Laden(ware, betrieb.Auslagern(ware, stueck));
            else
            {
                var kosten = _w.Markt[ware].HandKaufen(stueck, _w.HandKaufFaktor(konto), Daten!.HeimatmarktAnteil);
                konto.Zutaten(kosten);
                einkauf += kosten;
                schiff.Laden(ware, stueck);
            }
        }
        schiff.ErtragLaufend -= einkauf;
        schiff.Auftrag = a;
        schiff.Zustand = SchiffZustand.Beladen;
        schiff.Beginn = _w.Uhr.VergangeneMinuten;
        schiff.Ende = schiff.Beginn + LadeMinuten(schiff.Daten);
        var stadt = Stadt(a.Ziel)!;
        var zurueck = ZurueckUm(schiff);
        var um = _w.Uhr.UhrzeitInMinuten + (int)(zurueck - _w.Uhr.VergangeneMinuten);
        var uhr = um >= Spielzeit.TagesendeMinute ? "im nächsten Jahr" : $"gegen {um / 60:00}:{um % 60:00}";
        return Befehlsergebnis.Ok($"{schiff.Name} wird beladen und fährt nach {stadt.Name} ({(stadt.Daten.IstFlussauf ? "flussauf" : "flussab")}); " +
                                  $"zurück {uhr}." +
                                  (einkauf > 0 ? $" Hinfracht am Markt gekauft für {Zahlenformat.Ganz(einkauf)} {_w.Daten.Allgemein.Waehrung}." : "") +
                                  (a.Wiederholen ? " Der Auftrag wiederholt sich." : ""));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Ablauf
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Eine Spielminute: Schiffe legen ab, kommen an, kehren heim (in der Reihenfolge des Kaufs).</summary>
    internal void Minute(Spielzeit zeit)
    {
        if (_schiffe.Count == 0) return;
        var jetzt = zeit.VergangeneMinuten;
        foreach (var schiff in _schiffe.ToList())
        {
            if (schiff.Zustand == SchiffZustand.AmKai || jetzt < schiff.Ende) continue;
            switch (schiff.Zustand)
            {
                case SchiffZustand.Beladen: Ablegen(schiff, hin: true, jetzt); break;
                case SchiffZustand.Hinfahrt: InDerStadt(schiff, jetzt); break;
                case SchiffZustand.InFernerStadt: Ablegen(schiff, hin: false, jetzt); break;
                case SchiffZustand.Rueckfahrt: Heimkehren(schiff); break;
            }
        }
    }

    /// <summary>Eine volle Stunde: Die Märkte der fernen Städte erholen sich.</summary>
    internal void Stunde()
    {
        if (Daten is null) return;
        foreach (var stadt in _staedte) stadt.Erholen(Daten.ErholungJeStunde);
    }

    /// <summary>Jahresende (vor dem Abschluss der Konten): Unterhalt je Schiff; der Ertrag des Jahres wird zum Vorjahr.</summary>
    internal void Jahresende()
    {
        foreach (var schiff in _schiffe)
        {
            var unterhalt = schiff.Besitzer.IstAktiv ? schiff.Daten.Unterhalt : 0;
            schiff.Besitzer.Fixkosten(unterhalt);
            schiff.ErtragVorjahr = schiff.ErtragLaufend - unterhalt;
            schiff.ErtragLaufend = 0;
        }
    }

    /// <summary>Marktwert einer Ladung in der Stadt (für Zoll und Raub).</summary>
    public double Ladungswert(IReadOnlyDictionary<string, int> ladung) => ladung.Sum(x => _w.Markt[x.Key].Preis * x.Value);

    private void Ablegen(Schiff schiff, bool hin, long jetzt)
    {
        var stadt = Stadt(schiff.Auftrag!.Ziel)!;
        var konto = schiff.Besitzer;
        schiff.Zustand = hin ? SchiffZustand.Hinfahrt : SchiffZustand.Rueckfahrt;
        schiff.Beginn = jetzt;
        schiff.Ende = jetzt + Fahrzeit(schiff.Daten, stadt, hin);
        if (schiff.Ladung.Count == 0) return; // leer: kein Zoll, kein Geleit, keine Piraten

        var waehrung = _w.Daten.Allgemein.Waehrung;
        // Die erste Zollstelle gehört der Stadt (Stadtkasse, PR #200) – der Zöllner zahlt dort nichts.
        var stadtstelle = stadt.Daten.Zollstellen > 0 && _w.Kasse.IstAktiv;
        var zollfrei = stadtstelle && _w.Befugnisse.IstZollfrei(konto);
        var stellen = Zollstellen(konto, stadt);
        var zoll = stellen * Daten!.ZollJeStelle * Ladungswert(schiff.Ladung);
        if (zoll > 0)
        {
            konto.Transport(zoll);
            schiff.ErtragLaufend -= zoll;
            if (stadtstelle && !zollfrei)
                _w.Kasse.Buchen(Politik.Kassenposten.Zoelle, zoll / stellen);
        }

        // Das Geleit gibt es nur, solange es verfügbar ist (die Stadtwache nur, wenn ein Haus sie stellt).
        var geleit = _w.Wache.EskorteVerfuegbar(schiff.Geleit) ? _w.Daten.Logistik.Eskorten[schiff.Geleit] : new EskorteDaten();
        var geleitKosten = geleit.KostenJeFahrt * Daten.GeleitFaktor;
        if (geleitKosten > 0)
        {
            if (geleit.Wache) _w.Wache.GeleitBezahlen(konto, geleitKosten);
            else konto.Transport(geleitKosten);
            schiff.ErtragLaufend -= geleitKosten;
        }

        // Flusspiraten: erst, ob sie angreifen, dann, ob es gelingt – wie die Banden an den Karren.
        var p = Daten.Piraten;
        if (_w.Zufall.NextDouble() >= p.Chance) return;
        var schutz = Math.Min(0.95, geleit.Schutz + _w.Familien.KampfSchutz(konto));
        var erfolg = Math.Min(1, p.Staerke * (1 - schutz) * _w.Wache.UeberfallFaktor(geleit));
        var wo = hin ? $"auf dem Weg nach {stadt.Name}" : $"auf der Rückfahrt von {stadt.Name}";
        if (_w.Zufall.NextDouble() < erfolg)
        {
            var beute = schiff.Ausrauben(p.BeuteAnteil);
            var wert = Ladungswert(beute);
            konto.Raub(wert);
            _w.Melden($"{p.Name} überfallen {schiff.Name} ({konto.Inhaber}) {wo} und erbeuten {string.Join(" und ", beute.Select(b => $"{b.Value} {b.Key}"))} " +
                      $"(Wert {Zahlenformat.Ganz(wert)} {waehrung}).");
        }
        else
            _w.Melden($"{p.Name} greifen {schiff.Name} ({konto.Inhaber}) {wo} an – die Mannschaft schlägt sie zurück.");
    }

    private void InDerStadt(Schiff schiff, long jetzt)
    {
        var a = schiff.Auftrag!;
        var stadt = Stadt(a.Ziel)!;
        var konto = schiff.Besitzer;
        var waehrung = _w.Daten.Allgemein.Waehrung;

        double erloes = 0;
        foreach (var (ware, stueck) in schiff.Entladen().OrderBy(x => x.Key, StringComparer.Ordinal))
            erloes += stadt.Verkaufen(ware, stueck) * _w.Familien.VerkaufsFaktor(konto);
        if (erloes > 0) konto.Umsatz(erloes);

        // Die Rückfracht: so viel, wie das Geld reicht.
        var faktor = _w.Familien.EinkaufsFaktor(konto);
        double ausgaben = 0;
        var gekauft = new List<string>();
        foreach (var (ware, wunsch) in a.Rueckfracht.Where(x => x.Value > 0).OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var stueck = wunsch;
            while (stueck > 0 && stadt.Kosten(ware, stueck) * faktor > konto.Vermoegen) stueck--;
            if (stueck <= 0) { gekauft.Add($"{ware}: kein Geld"); continue; }
            var kosten = stadt.Kaufen(ware, stueck) * faktor;
            konto.Zutaten(kosten);
            ausgaben += kosten;
            schiff.Laden(ware, stueck);
            gekauft.Add($"{stueck} {ware}");
        }
        schiff.ErtragLaufend += erloes - ausgaben;
        schiff.Zustand = SchiffZustand.InFernerStadt;
        schiff.Beginn = jetzt;
        schiff.Ende = jetzt + LiegeMinuten(schiff.Daten);
        schiff.LetzteFahrt = $"{schiff.Name} ({konto.Inhaber}) in {stadt.Name}: " +
                             (erloes > 0 ? $"verkauft für {Zahlenformat.Ganz(erloes)} {waehrung}" : "nichts verkauft") +
                             (gekauft.Count > 0 ? $"; gekauft {string.Join(", ", gekauft)} für {Zahlenformat.Ganz(ausgaben)} {waehrung}" : "") + ".";
        _w.Melden(schiff.LetzteFahrt);
    }

    private void Heimkehren(Schiff schiff)
    {
        var a = schiff.Auftrag!;
        var konto = schiff.Besitzer;
        var waehrung = _w.Daten.Allgemein.Waehrung;
        var betrieb = a.Betrieb is { } name ? _w.Betriebe.FirstOrDefault(b => b.Name == name && b.Konto == konto) : null;
        double erloes = 0, eingelagert = 0;
        var teile = new List<string>();
        foreach (var (ware, stueck) in schiff.Entladen().OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var rest = stueck;
            if (betrieb is not null)
            {
                var platz = Math.Clamp(betrieb.LagerKapazitaetJeWare - betrieb.ImLager(ware), 0, rest);
                if (platz > 0)
                {
                    betrieb.Einlagern(ware, platz);
                    eingelagert += _w.Markt[ware].Preis * platz;
                    teile.Add($"{platz} {ware} ins Lager von {betrieb.Name}");
                    rest -= platz;
                }
            }
            if (rest <= 0) continue;
            var betrag = _w.Markt[ware].Verkaufen(rest) * _w.Familien.VerkaufsFaktor(konto);
            konto.Umsatz(betrag);
            var steuer = _w.SteuersatzMarkt(konto); // der Marktmeister zahlt weniger
            konto.Steuer(betrag * steuer);
            erloes += betrag * (1 - steuer);
            teile.Add($"{rest} {ware} am Markt");
        }
        schiff.ErtragLaufend += erloes + eingelagert;
        schiff.Fahrten++;
        schiff.Zustand = SchiffZustand.AmKai;
        schiff.Beginn = schiff.Ende = _w.Uhr.VergangeneMinuten;
        if (teile.Count > 0)
            _w.Melden($"{schiff.Name} ({konto.Inhaber}) ist zurück: {string.Join(", ", teile)}" +
                      (erloes > 0 ? $" (Erlös {Zahlenformat.Ganz(erloes)} {waehrung} nach Steuer)" : "") + ".");

        if (!a.Wiederholen || !konto.IstAktiv)
        {
            schiff.Auftrag = null;
            return;
        }
        // Wiederkehrender Auftrag: gleich wieder beladen – oder melden, warum nicht.
        schiff.Auftrag = null;
        if (AuftragVerboten(konto, schiff.Name, a) is { } grund)
        {
            _w.Melden($"{schiff.Name} ({konto.Inhaber}) bleibt am Kai – der wiederkehrende Auftrag geht nicht mehr: {grund}");
            return;
        }
        AuftragStarten(schiff, a);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Speichern
    // ═════════════════════════════════════════════════════════════════════════

    internal SchiffahrtStand? Sichern(Func<Konto, int> nummer) => Daten is null ? null : new SchiffahrtStand
    {
        Schiffe = _schiffe.Select(s => s.Sichern(nummer(s.Besitzer))).ToList(),
        Druck = _staedte.ToDictionary(s => s.Name, s => s.Sichern()),
        Getauft = _getauft,
    };

    internal void Wiederherstellen(SchiffahrtStand? s, IReadOnlyList<Konto> konten)
    {
        if (Daten is null || s is null) return;
        _getauft = s.Getauft;
        foreach (var stadt in _staedte)
            if (s.Druck.TryGetValue(stadt.Name, out var druck)) stadt.Wiederherstellen(druck);
        foreach (var ss in s.Schiffe)
        {
            if (!Daten.Schiffe.TryGetValue(ss.Typ, out var typ))
                throw new InvalidDataException($"Den Schiffstyp „{ss.Typ}“ gibt es in den Daten nicht.");
            var schiff = new Schiff(ss.Name, ss.Typ, typ, konten[ss.Konto], ss.Hafen);
            schiff.Wiederherstellen(ss);
            _schiffe.Add(schiff);
        }
    }
}
