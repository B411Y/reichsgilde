using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Dynastie;

/// <summary>
/// Wer eine Meinung hat und über wen man eine hat: eine Person der Häuser (auch Heiratskandidaten) oder ein Ratsherr.
/// </summary>
public interface ICharakter
{
    /// <summary>Eindeutig in der Partie: „P12“ für die Person Nummer 12, „R:Ulrich Wendelstein“ für einen Ratsherrn. Für Befehle und Spielstand.</summary>
    string Kennung { get; }
    string Name { get; }
    Volk Volk { get; }
    Geschlecht Geschlecht { get; }
    IReadOnlyList<Charakterzug> Zuege { get; }
    bool HatZug(string kennung);
}

/// <summary>Ein Grund der Meinung, wie ihn der Tooltip zeigt: „Beide gierig +10“, „Geschenke +7“.</summary>
/// <param name="Verblasst">Ein Grund aus einer Tat – er verblasst jedes Jahr ein Stück. Feste Gründe (Züge, Verwandtschaft, Volk, Glaube, Stand) bleiben.</param>
public sealed record Meinungsgrund(string Text, double Wert, bool Verblasst);

/// <summary>Eine wichtige Person der Stadt: Oberhaupt, Ehepartner oder Erbe eines Hauses oder ein Ratsherr.</summary>
/// <param name="Haus">Das Haus; null für einen Ratsherrn.</param>
/// <param name="Rolle">„Patriarch“, „Ehefrau von Agnes“, „Erbe“, „Bürgermeister“ …</param>
public sealed record WichtigePerson(ICharakter Charakter, Konto? Haus, string Rolle);

/// <summary>
/// Meinungen (Charaktersystem Teil 2, PR #231): Was eine wichtige Person von einer anderen hält, −100 … +100.
///
/// Die Meinung wird bei Bedarf aus Gründen berechnet:
///   • fest – Charakterzüge (gleiche +, gegensätzliche −, dazu Schön, Klug, Trinker … für alle), Verwandtschaft und Ehe, Volk, Glaube,
///     Stand und die Ungeduld eines Erben;
///   • aus Taten – Geschenke, Stammtisch, Moschus, Zwietracht, Bekehrung (die frühere Gunst der Ratsherren), dazu ein Fest der Stadt,
///     die Stimme im Rat, eine Klage, ein ertappter Anschlag und die Aktivitäten (<see cref="Aktivitaeten"/>). Sie verblassen jedes Jahr.
/// Im Spielstand stehen nur die Gründe aus Taten.
///
/// Fehlt der Abschnitt "meinungen" (alte Spielstände), bleibt es bei dem, was es vorher gab: Taten, die früher Gunst brachten, Volk und Glaube.
/// </summary>
public sealed partial class Meinungswesen
{
    private readonly Stadtwirtschaft _w;
    private readonly MeinungDaten? _d;
    private Zufallsquelle _zufall;

    /// <summary>Gründe aus Taten: (von, über) → Art → Text und Wert. Innen sortiert, damit jede Summe in derselben Reihenfolge entsteht.</summary>
    private readonly Dictionary<(string Von, string Ueber), SortedDictionary<string, (string Text, double Wert)>> _taten = new();

    /// <summary>Aktivitäten dieses Jahres: (Art, von, über) → wie oft (Geschenke zählen, alle anderen gibt es einmal).</summary>
    private readonly Dictionary<(string Art, string Von, string Ueber), int> _diesesJahr = new();

    /// <summary>Streit im Haus: Haus → bis zu diesem Jahr fehlt Ansehen.</summary>
    private readonly Dictionary<Konto, int> _streitBis = new();

    public MeinungDaten? Daten => _d;

    /// <summary>Gibt es die Meinungen mit allen Gründen und die Aktivitäten? (Abschnitt "meinungen")</summary>
    public bool IstAktiv => _d is not null;

    internal Meinungswesen(Stadtwirtschaft w, MeinungDaten? daten, int zufallsstartwert)
    {
        _w = w;
        _d = daten;
        _zufall = new Zufallsquelle(unchecked(zufallsstartwert * 61 + 37)); // eigener Generator: alle übrigen Würfe bleiben wie vorher
    }

    private double Hoechstwert => _d?.Hoechstwert ?? _w.Daten.Politik.Gunst.Hoechstwert;

    // ═════════════════════════════════════════════════════════════════════════
    //  Auskunft
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Was <paramref name="von"/> über <paramref name="ueber"/> denkt, −100 … +100.</summary>
    public double Meinung(ICharakter von, ICharakter ueber) => Begrenzen(Rechnen(von, ueber, null));

    /// <summary>Alle Gründe, die festen zuerst, dann die aus Taten – für den Tooltip. Die Summe (begrenzt) ist <see cref="Meinung(ICharakter, ICharakter)"/>.</summary>
    public IReadOnlyList<Meinungsgrund> Gruende(ICharakter von, ICharakter ueber)
    {
        var liste = new List<Meinungsgrund>();
        Rechnen(von, ueber, liste);
        return liste;
    }

    /// <summary>Was <paramref name="von"/> über das Oberhaupt dieses Hauses denkt (ohne Familie: nur die Taten).</summary>
    public double Meinung(ICharakter von, Konto ueber) =>
        Oberhaupt(ueber) is { } o ? Meinung(von, o) : Begrenzen(TatenSumme(von.Kennung, HausKennung(ueber)));

    /// <summary>Was das Oberhaupt von <paramref name="von"/> über das Oberhaupt von <paramref name="ueber"/> denkt (0 ohne Familien).</summary>
    public double Meinung(Konto von, Konto ueber) =>
        Oberhaupt(von) is { } a && Oberhaupt(ueber) is { } b ? Meinung(a, b) : 0;

    /// <summary>Die Gründe aus Taten allein (für die frühere Anzeige „Gunst“).</summary>
    public double Taten(ICharakter von, Konto ueber) => Math.Clamp(TatenSumme(von.Kennung, HausKennung(ueber)), -Hoechstwert, Hoechstwert);

    public Person? Oberhaupt(Konto konto) => _w.Familien.Von(konto)?.Oberhaupt;

    /// <summary>Oberhaupt, Ehepartner und Erbe jedes Hauses im Spiel, dann die Ratsherren.</summary>
    public IReadOnlyList<WichtigePerson> WichtigePersonen()
    {
        var liste = new List<WichtigePerson>();
        foreach (var konto in _w.Konten.Where(k => k.IstAktiv))
        {
            if (_w.Familien.Von(konto) is not { } f) continue;
            liste.Add(new WichtigePerson(f.Oberhaupt, konto, $"{f.Oberhaupt.OberhauptTitel}, {konto.Inhaber}"));
            if (f.Ehepartner is { Lebt: true } e)
                liste.Add(new WichtigePerson(e, konto, $"{e.Ehepartnertitel} von {f.Oberhaupt.Vorname}"));
            if (_w.Familien.Erbe(f) is { } erbe && erbe != f.Ehepartner)
                liste.Add(new WichtigePerson(erbe, konto, $"{(erbe.Geschlecht == Geschlecht.Frau ? "Erbin" : "Erbe")} von {f.Oberhaupt.Vorname}"));
        }
        foreach (var r in _w.Rat.Ratsherren)
            liste.Add(new WichtigePerson(r, null,
                _w.Rat.Aemter.FirstOrDefault(a => a.Ratsherr == r)?.Name ?? (r.Geschlecht == Geschlecht.Frau ? "Ratsfrau" : "Ratsherr")));
        return liste;
    }

    /// <summary>Ist diese Person gerade eine wichtige Person (Oberhaupt, Ehepartner, Erbe, Ratsherr)?</summary>
    public bool IstWichtig(ICharakter c) => c is Ratsherr || WichtigePersonen().Any(w => w.Charakter.Kennung == c.Kennung);

    /// <summary>Die Person oder den Ratsherrn zu einer <see cref="ICharakter.Kennung"/> – null, wenn es sie nicht gibt.</summary>
    public ICharakter? Charakter(string kennung)
    {
        if (kennung.StartsWith("R:", StringComparison.Ordinal)) return _w.Rat.RatsherrNamens(kennung[2..]);
        if (kennung.Length < 2 || kennung[0] != 'P' || !int.TryParse(kennung.AsSpan(1), out var nummer)) return null;
        return PersonNummer(nummer);
    }

    private Person? PersonNummer(int nummer)
    {
        foreach (var konto in _w.Konten)
            if (_w.Familien.Von(konto)?.MitgliederListe.FirstOrDefault(p => p.Nummer == nummer) is { } p) return p;
        return _w.Familien.KandidatNummer(nummer)?.Person;
    }

    /// <summary>Wer im Spielstand und in den Taten für ein Haus steht: sein Oberhaupt – ohne Familie das Haus selbst.</summary>
    internal string HausKennung(Konto konto) => Oberhaupt(konto)?.Kennung ?? "K:" + konto.Inhaber;

    // ═════════════════════════════════════════════════════════════════════════
    //  Rechnung
    // ═════════════════════════════════════════════════════════════════════════

    private double Begrenzen(double wert) => Math.Clamp(wert, -Hoechstwert, Hoechstwert);

    private double TatenSumme(string von, string ueber) =>
        _taten.TryGetValue((von, ueber), out var t) ? t.Values.Sum(x => x.Wert) : 0;

    /// <summary>Summe aller Gründe (unbegrenzt); mit <paramref name="liste"/> auch die Gründe mit Text.</summary>
    private double Rechnen(ICharakter von, ICharakter ueber, List<Meinungsgrund>? liste)
    {
        var summe = 0.0;
        void Grund(Func<string> text, double wert, bool verblasst = false)
        {
            if (Math.Abs(wert) < 1e-9) return;
            summe += wert;
            liste?.Add(new Meinungsgrund(text(), wert, verblasst));
        }
        if (von.Kennung == ueber.Kennung) return 0;

        var vonP = von as Person;
        var ueberP = ueber as Person;
        var fb = _w.Familien;
        var hausUeber = ueberP is null ? null : fb.FamilieVon(ueberP);
        var hausVon = vonP is null ? null : fb.FamilieVon(vonP);

        if (_d is not null)
        {
            // Charakterzüge
            foreach (var z in von.Zuege)
            {
                var m = z.Daten.Meinung;
                if (ueber.HatZug(z.Kennung)) Grund(() => $"Beide {z.Name.ToLowerInvariant()}", m.Gleich);
                else if (z.Gegenteil is { } g && ueber.HatZug(g))
                    Grund(() => $"{z.Name} gegen {ueber.Zuege.First(x => x.Kennung == g).Name.ToLowerInvariant()}", m.Gegenteil);
            }
            foreach (var z in ueber.Zuege)
                Grund(() => z.Name, z.Daten.Meinung.Allgemein);

            // Verwandtschaft und Ehe
            if (vonP is not null && ueberP is not null)
            {
                var v = _d.Verwandtschaft;
                var ehe = hausVon is not null && hausVon == hausUeber
                          && (hausVon.Oberhaupt == vonP && hausVon.Ehepartner == ueberP || hausVon.Oberhaupt == ueberP && hausVon.Ehepartner == vonP);
                if (ehe) Grund(() => ueberP.Ehepartnertitel, v.Ehe);
                if (vonP.IstKindVon(ueberP)) Grund(() => ueberP.Geschlecht == Geschlecht.Frau ? "Mutter" : "Vater", v.ElternKind);
                else if (ueberP.IstKindVon(vonP)) Grund(() => ueberP.SohnOderTochter, v.ElternKind);
                else if (vonP.Vater is { } va && va == ueberP.Vater || vonP.Mutter is { } mu && mu == ueberP.Mutter)
                    Grund(() => ueberP.Geschlecht == Geschlecht.Frau ? "Schwester" : "Bruder", v.Geschwister);

                // Stand: Wer höher steht, wird geachtet.
                var stufen = fb.StandVon(ueberP) - fb.StandVon(vonP);
                var s = _d.Stand;
                if (stufen != 0)
                    Grund(() => stufen > 0 ? $"Höherer Stand ({_w.Daten.Staende[fb.StandVon(ueberP)].Name})" : $"Niedrigerer Stand ({_w.Daten.Staende[fb.StandVon(ueberP)].Name})",
                        Math.Clamp(stufen * s.JeStufe, -s.Hoechstens, s.Hoechstens));

                // Ein Erbe, der zu lange warten muss
                if (hausUeber is not null && hausUeber.Oberhaupt == ueberP && fb.Erbe(hausUeber) == vonP && hausUeber.Ehepartner != vonP)
                {
                    var e = _d.Erbe;
                    var jahre = Math.Floor(fb.Menschenjahre(vonP) - e.UngeduldAb);
                    if (jahre > 0) Grund(() => "Wartet aufs Erbe", Math.Max(e.Hoechstens, jahre * e.JeJahr));
                }
            }
        }

        // Volk – wie die frühere Volksnähe im Rat; eine Ehe zwischen Völkern färbt den Blick auf das Oberhaupt.
        var gunst = _w.Daten.Politik.Gunst;
        if (von.Volk == ueber.Volk) Grund(() => $"Gleiches Volk ({ueber.Volk.Mehrzahl()})", gunst.Volksnaehe);
        if (fb.IstAktiv && hausUeber is not null && hausUeber.Oberhaupt == ueberP && hausUeber.Ehepartner is { } partner && partner.Volk != ueberP.Volk)
        {
            var mischehe = fb.Daten.Mischehe;
            if (von.Volk == ueberP.Volk) Grund(() => $"Ehe mit {partner.Vorname} ({partner.Volk.Einzahl()})", mischehe.AnsehenEigenesVolk);
            if (von.Volk == partner.Volk) Grund(() => $"Ehe mit {partner.Vorname} aus dem eigenen Volk", mischehe.AnsehenPartnervolk);
        }

        // Glaube – wie die frühere Glaubensnähe im Rat.
        if (_w.Glaube.IstAktiv && GlaubeVon(von) is { } gv && GlaubeVon(ueber) is { } gu)
        {
            var g = _w.Glaube.Daten.Gunst;
            if (gu == Konfession.Naturglaube && gv != Konfession.Naturglaube) Grund(() => "Ein Heide", g.Heide);
            else if (gu == gv) Grund(() => $"Gleicher Glaube ({gu.Name()})", g.Gleich);
            else Grund(() => $"Anderer Glaube ({gu.Name()})", g.Anders);
            if (hausUeber is not null && hausUeber.Oberhaupt == ueberP && hausUeber.Ehepartner is { } e2 && e2.Herkunftsglaube != gu && e2.Herkunftsglaube == gv)
                Grund(() => $"{e2.Vorname} trat vom {gv.Glaube()} über", g.Mischehe);
        }

        // Taten
        if (_taten.TryGetValue((von.Kennung, ueber.Kennung), out var taten))
            foreach (var (_, (text, wert)) in taten)
                Grund(() => text, wert, verblasst: true);
        return summe;
    }

    private Konfession? GlaubeVon(ICharakter c) => c switch
    {
        Person p => p.Glaube,
        Ratsherr r => _w.Glaube.GlaubeVon(r),
        _ => null,
    };

    /// <summary>Faktor auf die Wirkung eines Geschenks: Gierig nimmt gern, Ehrlich und Bescheiden ungern (Zug-Wert <c>bestechlich</c>).</summary>
    public static double Bestechlichkeit(ICharakter c) => c.Zuege.Aggregate(1.0, (f, z) => f * z.Daten.Bestechlich);

    // ═════════════════════════════════════════════════════════════════════════
    //  Taten eintragen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Ein Grund aus einer Tat kommt dazu (oder wächst) – jede Art höchstens bis zum Höchstwert in jede Richtung.</summary>
    internal void TatEintragen(string von, string ueber, string art, string text, double wert)
    {
        if (von == ueber || Math.Abs(wert) < 1e-12) return;
        if (!_taten.TryGetValue((von, ueber), out var taten))
            _taten[(von, ueber)] = taten = new SortedDictionary<string, (string, double)>(StringComparer.Ordinal);
        var vorher = taten.TryGetValue(art, out var t) ? t.Wert : 0;
        taten[art] = (text, Math.Clamp(vorher + wert, -Hoechstwert, Hoechstwert));
    }

    /// <summary>Die frühere Gunst: ein Ratsherr über ein Haus (Stammtisch, Moschus, Zwietracht, Bekehrung).</summary>
    internal void GunstAendern(Ratsherr r, Konto konto, string art, string text, double wert) =>
        TatEintragen(r.Kennung, HausKennung(konto), art, text, wert);

    /// <summary>Nach einer Verurteilung rückt der Ratsherr ab: Alle Gründe aus Taten über das Haus sind weg.</summary>
    internal void TatenEntfernen(Ratsherr r, Konto konto) => _taten.Remove((r.Kennung, HausKennung(konto)));

    /// <summary>Ein Fest auf Antrag: Alle wichtigen Personen anderer Häuser und die Ratsherren denken besser über das Oberhaupt.</summary>
    internal void FestAusgerichtet(Konto antragsteller)
    {
        if (_d is null || _d.Taten.Fest == 0) return;
        var ueber = HausKennung(antragsteller);
        foreach (var w in WichtigePersonen().Where(w => w.Haus != antragsteller))
            TatEintragen(w.Charakter.Kennung, ueber, "fest", "Hat ein Fest ausgerichtet", _d.Taten.Fest);
    }

    /// <summary>Die Stimme im Rat: Der Bewerber bzw. Antragsteller merkt sich, wer für und wer gegen ihn stimmte.</summary>
    internal void StimmeGemerkt(Amt sitz, Konto fuer, bool dafuer)
    {
        if (_d is null || sitz.Konto == fuer) return;
        var waehler = sitz.Ratsherr?.Kennung ?? (sitz.Konto is { } k ? HausKennung(k) : null);
        if (waehler is null) return;
        TatEintragen(HausKennung(fuer), waehler, dafuer ? "stimme" : "stimmeGegen",
            dafuer ? "Stimmte im Rat für ihn" : "Stimmte im Rat gegen ihn", dafuer ? _d.Taten.StimmeFuer : _d.Taten.StimmeGegen);
    }

    /// <summary>Der Beklagte über den Kläger.</summary>
    internal void Verklagt(Konto anklaeger, Konto beklagter)
    {
        if (_d is null) return;
        TatEintragen(HausKennung(beklagter), HausKennung(anklaeger), "klage", "Hat ihn verklagt", _d.Taten.Klage);
    }

    /// <summary>
    /// Ein Zeuge hat den Täter erkannt: Das Opfer denkt schlechter über ihn (nur Vergehen aus <c>meinungen.taten.ertappt</c>, und nur,
    /// wenn die Tat einen seiner Betriebe oder sein Haus traf – die Wache oder ein Prediger mit einem Beweis sind keine Opfer).
    /// </summary>
    internal void Ertappt(Konto opfer, Spur spur)
    {
        if (_d is null || opfer == spur.Taeter || !_d.Taten.Ertappt.TryGetValue(spur.Art, out var wert)) return;
        var eigenes = _w.Betriebe.Where(b => b.Konto == opfer).Select(b => b.Name).Append(opfer.Inhaber);
        if (!eigenes.Any(n => spur.Betroffener.Contains(n, StringComparison.Ordinal))) return;
        TatEintragen(HausKennung(opfer), HausKennung(spur.Taeter), "ertappt:" + spur.Art, $"Ertappt: {spur.Art.Name()}", wert);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Wirkung
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Faktor auf die Haushaltskosten: Ein Ehepartner, der gut über das Oberhaupt denkt, wirtschaftet sparsamer.</summary>
    public double HaushaltFaktor(Familie f)
    {
        if (_d is null || f.Ehepartner is not { Lebt: true } e) return 1;
        var w = _d.Wirkung;
        return 1 - Math.Clamp(Meinung(e, f.Oberhaupt) * w.HaushaltJePunkt, -w.HaushaltHoechstens, w.HaushaltHoechstens);
    }

    /// <summary>Ansehen, das ein Streit im Haus kostet (bis zum Ende des Jahres danach).</summary>
    public double AnsehenAbzug(Konto konto) =>
        _d is not null && _streitBis.TryGetValue(konto, out var bis) && bis >= _w.Familien.Jahr ? _d.Wirkung.Streit.Ansehen : 0;

    /// <summary>Brautschau: Was die Meinung des Kandidaten über das Oberhaupt zur Bereitschaft beiträgt.</summary>
    public double HeiratBereitschaft(Konto konto, Kandidat k) =>
        _d is null || Oberhaupt(konto) is not { } o ? 0 : Meinung(k.Person, o) * _d.Wirkung.HeiratBereitschaftJePunkt;

    /// <summary>Brautschau: Faktor auf die Gulden je Zuneigung (wer gemocht wird, wirbt billiger; 0,75 bis 1,25).</summary>
    public double HeiratKostenFaktor(Konto konto, Kandidat k) =>
        _d is null || Oberhaupt(konto) is not { } o ? 1 : Math.Clamp(1 - Meinung(k.Person, o) * _d.Wirkung.HeiratKostenJePunkt, 0.75, 1.25);

    // ═════════════════════════════════════════════════════════════════════════
    //  Jahresende
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Nach der Ratssitzung: Streit im Haus (Erbe mit schlechter Meinung über das Oberhaupt), dann verblassen die Gründe aus Taten
    /// (wie früher die Gunst), und die Aktivitäten des Jahres sind wieder frei.
    /// </summary>
    internal void Jahresende()
    {
        if (_d is not null && _w.Familien.IstAktiv)
        {
            var s = _d.Wirkung.Streit;
            foreach (var konto in _w.Konten.Where(k => k.IstAktiv))
            {
                if (_w.Familien.Von(konto) is not { } f || _w.Familien.Erbe(f) is not { } erbe || erbe == f.Ehepartner) continue;
                var meinung = Meinung(erbe, f.Oberhaupt);
                if (meinung > s.Ab || _zufall.NextDouble() >= s.Chance) continue;
                _streitBis[konto] = _w.Familien.Jahr;
                _w.Familien.Ereignis(konto, $"Streit im Haus {f.Nachname}: {erbe.Vorname} überwirft sich mit {f.Oberhaupt.Vorname} (Meinung {Zahlenformat.Ganz(meinung)}). " +
                                            $"Die Stadt redet darüber – {Zahlenformat.Ganz(s.Ansehen)} Ansehen weniger bis Ende {_w.Familien.Jahr}.");
            }
        }

        var bleibt = 1 - (_d?.VerfallJeJahr ?? _w.Daten.Politik.Gunst.VerfallJeJahr);
        foreach (var (schluessel, taten) in _taten.ToList())
        {
            foreach (var art in taten.Keys.ToList())
            {
                var (text, wert) = taten[art];
                if (Math.Abs(wert * bleibt) < 0.01) taten.Remove(art);
                else taten[art] = (text, wert * bleibt);
            }
            if (taten.Count == 0) _taten.Remove(schluessel);
        }
        _diesesJahr.Clear();
        foreach (var k in _streitBis.Where(x => x.Value < _w.Familien.Jahr).Select(x => x.Key).ToList())
            _streitBis.Remove(k);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Speichern
    // ═════════════════════════════════════════════════════════════════════════

    internal MeinungenStand Sichern(Func<Konto, int> kontoNummer) => new()
    {
        Gruende = _taten.OrderBy(t => t.Key.Von, StringComparer.Ordinal).ThenBy(t => t.Key.Ueber, StringComparer.Ordinal)
            .SelectMany(t => t.Value.Select(x => new MeinungsgrundStand { Von = t.Key.Von, Ueber = t.Key.Ueber, Art = x.Key, Text = x.Value.Text, Wert = x.Value.Wert }))
            .ToList(),
        DiesesJahr = _diesesJahr.OrderBy(x => x.Key.Art, StringComparer.Ordinal).ThenBy(x => x.Key.Von, StringComparer.Ordinal).ThenBy(x => x.Key.Ueber, StringComparer.Ordinal)
            .Select(x => new MeinungJahrStand { Art = x.Key.Art, Von = x.Key.Von, Ueber = x.Key.Ueber, Anzahl = x.Value }).ToList(),
        StreitBis = _streitBis.OrderBy(x => kontoNummer(x.Key)).ToDictionary(x => kontoNummer(x.Key).ToString(), x => x.Value),
        Wuerfe = _zufall.Wuerfe,
    };

    /// <summary>
    /// Nach Rat und Familien. Alte Spielstände (ohne <paramref name="s"/>): Die Gunst der Ratsherren wird zum Grund „Geschenke“.
    /// </summary>
    internal void Wiederherstellen(MeinungenStand? s, IReadOnlyList<GunstStand>? alteGunst, IReadOnlyList<Konto> konten)
    {
        _taten.Clear();
        _diesesJahr.Clear();
        _streitBis.Clear();
        if (s is null)
        {
            foreach (var g in alteGunst ?? Array.Empty<GunstStand>())
                if (_w.Rat.RatsherrNamens(g.Ratsherr) is { } r && g.Konto >= 0 && g.Konto < konten.Count)
                    TatEintragen(r.Kennung, HausKennung(konten[g.Konto]), "geschenk", "Geschenke", g.Wert);
            return;
        }
        foreach (var g in s.Gruende)
        {
            if (!_taten.TryGetValue((g.Von, g.Ueber), out var taten))
                _taten[(g.Von, g.Ueber)] = taten = new SortedDictionary<string, (string, double)>(StringComparer.Ordinal);
            taten[g.Art] = (g.Text, g.Wert);
        }
        foreach (var j in s.DiesesJahr)
            _diesesJahr[(j.Art, j.Von, j.Ueber)] = j.Anzahl;
        foreach (var (nummer, bis) in s.StreitBis)
            _streitBis[konten[int.Parse(nummer)]] = bis;
        _zufall = new Zufallsquelle(_zufall.Startwert, s.Wuerfe);
    }
}
