using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Wegebau: der Belag der Wege im Spiel (Kartenwerkstatt, Schritt 5d; Abschnitt „wegebau“, ab Datenversion 52)
//
//  • Jeder Weg der Karte ist in Stücke zu 5 m geteilt (wegebau.stueck). Jedes Stück hat eine Stufe (Trampelpfad, Fahrweg, Kies,
//    Pflaster), dazu Austritt (Trampelpfad, Fahrweg: 0 … 1) bzw. Zustand (Kies, Pflaster: 1 = neu). Den Start liest der Kern aus der
//    Karte (Feld „belag“ der Wege, sonst die Vorgabe der Klasse: Karrenstraßen Fahrweg, Gassen und Fußwege Trampelpfad).
//  • Tempo: Karren fahren je Stufe verschieden schnell (wegebau.tempo, Pflaster 1). Die feste Fahrzeit gilt für den Bezug (Fahrweg):
//    ein Stück dauert Bezugstempo ÷ Tempo so lang (Stadtwirtschaft.Fahrzeit) – Greybox und Köln spielen am Start genau wie vorher.
//  • Verkehr (zum Jahreswechsel, ohne Zufall): Jede Karrenfahrt eines Betriebs zählt auf jedem Stück seiner Strecke zum Markt; dazu
//    Grundverkehr je Wegklasse und Fußverkehr aus den Gebäuden in der Nähe. Viel Verkehr tritt einen Trampelpfad zum Fahrweg aus, ohne
//    Verkehr verwildert ein Fahrweg wieder. Kies und Pflaster verfallen leicht und fallen unter einer Schwelle eine Stufe zurück.
//  • Bauaufträge: Spieler, KI-Häuser und die Stadt lassen ein Stück Weg zu Kies oder Pflaster ausbauen (Preis je Meter, Bauzeit,
//    Baustelle); gleiche Stufe bessert aus (kostet, was am Zustand fehlt).
//  • Stadtrat: Die Stadt legt jedes Jahr Geld in die Wegekasse, bessert ihr Pflaster an Hauptstraßen und Markt aus und pflastert dort
//    den Abschnitt mit dem meisten Verkehr. Ratsantrag „Weg pflastern“ (ein Antrag je Jahr): angenommen zahlt die Wegekasse, und der
//    Antragsteller gewinnt Ansehen.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Bauauftrag an einem Weg: Stücke von <see cref="Von"/> bis <see cref="Bis"/> (Meter), fertig ab <see cref="FertigAb"/>.</summary>
/// <param name="Bauherr">Wer bezahlt hat (null: die Stadt).</param>
public sealed record Wegbaustelle(int Weg, double Von, double Bis, Belagstufe Stufe, Konto? Bauherr, long FertigAb, double Kosten)
{
    internal int VonStueck { get; init; }
    internal int BisStueck { get; init; }
    public string BauherrName => Bauherr?.Inhaber ?? "die Stadt";
}

/// <summary>Was ein Bauauftrag kosten und dauern würde – oder warum er nicht geht (<see cref="Verboten"/>).</summary>
/// <param name="Von">Anfang des ersten Stücks (Meter).</param>
/// <param name="Bis">Ende des letzten Stücks (Meter).</param>
/// <param name="Meter">So viele Meter werden wirklich gebaut (Stücke, die schon besser sind, bleiben).</param>
public sealed record Wegebauvorschau(double Von, double Bis, double Meter, double Kosten, int BauzeitMinuten, string? Verboten);

/// <summary>Der Belag der Wege im Spiel (siehe Kopf der Datei).</summary>
public sealed class Wegebau
{
    private readonly Stadtwirtschaft _w;

    public WegebauDaten? Daten { get; }

    internal Wegebau(Stadtwirtschaft w, WegebauDaten? daten)
    {
        _w = w;
        Daten = daten;
    }

    /// <summary>Gibt es Belag im Spiel? (Abschnitt „wegebau“ und eine Karte)</summary>
    public bool IstAktiv => Daten is not null && _w.Karte is not null;

    /// <summary>Ein Weg im Spiel: je Stück Stufe, Austritt, Zustand, Karrenfahrten des Vorjahres; fest: Fußverkehr und Lage.</summary>
    private sealed class Wegzustand
    {
        public required double Laenge;
        public required Belagstufe[] Stufe;
        public required double[] Austritt;
        public required double[] Zustand;
        public required double[] Karren;
        public required double[] Fuss;
        public required bool[] Ratsstueck;
        public required double Grundverkehr;
        public int Anzahl => Stufe.Length;
    }

    private Wegzustand[]? _wege;
    private Wegzustand[] Wege => _wege ??= Start();

    /// <summary>Einfache Karrenfahrten im laufenden Jahr je Betrieb.</summary>
    private readonly Dictionary<string, int> _fahrten = new(StringComparer.Ordinal);

    private Dictionary<string, int> _fahrtenVorjahr = new(StringComparer.Ordinal);
    private readonly List<Wegbaustelle> _baustellen = new();
    private readonly Dictionary<Konto, int> _ansehenBis = new(ReferenceEqualityComparer.Instance);

    /// <summary>Laufende Bauaufträge.</summary>
    public IReadOnlyList<Wegbaustelle> Baustellen => _baustellen;

    /// <summary>Geld der Stadt für Wege (Gulden).</summary>
    public double Wegekasse { get; private set; }

    /// <summary>Der Ratsantrag „Weg pflastern“ dieses Jahres (null: keiner).</summary>
    public (Konto Antragsteller, int Weg, double Von, double Bis)? Antrag
    {
        get => _antrag;
        private set
        {
            _antrag = value;
            _stimmen.Clear(); // ein neuer oder kein Antrag: die Stimmen zum alten gelten nicht mehr
        }
    }

    private (Konto Antragsteller, int Weg, double Von, double Bis)? _antrag;

    /// <summary>Stimmen zum Antrag „Weg pflastern“, nur während der Ratssitzung gesetzt (fehlt = Enthaltung; nicht im Spielstand).</summary>
    private readonly Dictionary<Konto, bool> _stimmen = new();

    /// <summary>Der Belag hat sich geändert (Jahreswechsel, fertige Baustelle, neuer Auftrag) – die Darstellung zeichnet neu.</summary>
    public event Action? BelagGeaendert;

    private double StueckLaenge => Daten!.Stueck;

    // ═════════════════════════════════════════════════════════════════════════
    //  Abfragen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Wie viele Stücke dieser Weg hat (0 ohne Belag im Spiel oder bei einer falschen Nummer).</summary>
    public int StueckZahl(int weg) => IstAktiv && weg >= 0 && weg < Wege.Length ? Wege[weg].Anzahl : 0;

    /// <summary>Das Stück an dieser Stelle (Meter entlang des Verlaufs; außerhalb: das erste bzw. letzte).</summary>
    public int StueckBei(int weg, double stelle) => Math.Clamp((int)Math.Floor(stelle / StueckLaenge), 0, Math.Max(0, StueckZahl(weg) - 1));

    /// <summary>Der Belag an dieser Stelle eines Weges – ohne Belag im Spiel die Vorgabe der Klasse.</summary>
    public Belagstufe BelagBei(int weg, double stelle)
    {
        if (StueckZahl(weg) == 0)
            return _w.Karte is { } k && weg >= 0 && weg < k.Wege.Count ? Wegbelag.StufeBei(k.Wege[weg].Klasse, k.Wege[weg].Laenge, k.Wege[weg].Belag, stelle).Stufe
                : Belagstufe.Fahrweg;
        return Wege[weg].Stufe[StueckBei(weg, stelle)];
    }

    /// <summary>Ein Stück mit allem, was die Darstellung braucht (Stufe, Austritt, Zustand, Verkehr, Tempo, Baustelle).</summary>
    public Belagabschnitt Stueck(int weg, int i)
    {
        var z = Wege[weg];
        var (von, bis) = (i * StueckLaenge, Math.Min((i + 1) * StueckLaenge, z.Laenge));
        if (i == z.Anzahl - 1) bis = z.Laenge;
        return new Belagabschnitt(von, bis, z.Stufe[i], z.Austritt[i], z.Zustand[i], Verkehr(z, i), Tempo(z, i), ImBau(weg, i));
    }

    /// <summary>Alle Stücke eines Weges (leer ohne Belag im Spiel).</summary>
    public IReadOnlyList<Belagabschnitt> Stuecke(int weg) => Enumerable.Range(0, StueckZahl(weg)).Select(i => Stueck(weg, i)).ToList();

    /// <summary>
    /// Die Abschnitte eines Weges: Nachbarstücke mit gleicher Stufe, gleicher Baustelle und auf 0,1 gleichem Austritt und Zustand sind
    /// ein Abschnitt (Austritt, Zustand, Verkehr und Tempo nach Länge gemittelt). Ohne Belag im Spiel der Startbelag der Karte.
    /// </summary>
    public IReadOnlyList<Belagabschnitt> Abschnitte(int weg)
    {
        if (StueckZahl(weg) == 0)
            return _w.Karte is { } k && weg >= 0 && weg < k.Wege.Count ? Wegbelag.Startabschnitte(k.Wege[weg].Klasse, k.Wege[weg].Laenge, k.Wege[weg].Belag)
                : Array.Empty<Belagabschnitt>();
        var liste = new List<(Belagabschnitt Summe, double A, double Z)>();
        foreach (var s in Stuecke(weg))
        {
            var (a, z) = (Math.Round(s.Austritt, 1), Math.Round(s.Zustand, 1));
            if (liste.Count > 0 && liste[^1].Summe.Stufe == s.Stufe && liste[^1].Summe.Baustelle == s.Baustelle && liste[^1].A == a && liste[^1].Z == z)
            {
                var v = liste[^1].Summe;
                liste[^1] = (v with
                {
                    Bis = s.Bis, Austritt = v.Austritt + s.Austritt * s.Laenge, Zustand = v.Zustand + s.Zustand * s.Laenge,
                    Verkehr = v.Verkehr + s.Verkehr * s.Laenge, Tempo = v.Tempo + s.Tempo * s.Laenge,
                }, a, z);
            }
            else
                liste.Add((s with { Austritt = s.Austritt * s.Laenge, Zustand = s.Zustand * s.Laenge, Verkehr = s.Verkehr * s.Laenge, Tempo = s.Tempo * s.Laenge }, a, z));
        }
        return liste.Select(x =>
        {
            var l = Math.Max(1e-9, x.Summe.Laenge);
            return x.Summe with { Austritt = x.Summe.Austritt / l, Zustand = x.Summe.Zustand / l, Verkehr = x.Summe.Verkehr / l, Tempo = x.Summe.Tempo / l };
        }).ToList();
    }

    /// <summary>Verkehr an dieser Stelle (Fahrten im Jahr: Karren des Vorjahres, Grund- und Fußverkehr) – 0 ohne Belag im Spiel.</summary>
    public double Verkehr(int weg, double stelle) => StueckZahl(weg) == 0 ? 0 : Verkehr(Wege[weg], StueckBei(weg, stelle));

    /// <summary>Wie viele einfache Fahrten die Karren dieses Betriebs im Vorjahr machten.</summary>
    public int FahrtenVorjahr(string betrieb) => _fahrtenVorjahr.GetValueOrDefault(betrieb);

    /// <summary>
    /// Wie viel mal so lang ein Stück einer Fahrt dauert als auf dem Bezugsbelag (<see cref="WegebauDaten.Bezug"/>): Bezugstempo ÷ Tempo des
    /// Stücks. 1 auf Brücken, in Furten, auf Fähren und ohne Belag im Spiel.
    /// </summary>
    public double Faktor(Fahrtstueck s)
    {
        if (s.Art != Fahrbahn.Strasse || StueckZahl(s.Weg) == 0) return 1;
        return Daten!.TempoVon(Daten.Bezug) / Tempo(Wege[s.Weg], StueckBei(s.Weg, s.Stelle));
    }

    /// <summary>
    /// Der nächste Weg zu einem Punkt (höchstens <paramref name="hoechstens"/> Meter neben seiner Mittellinie): Nummer, Stelle (Meter entlang
    /// des Verlaufs) und Abstand – null, wenn keiner so nah ist.
    /// </summary>
    public (int Weg, double Stelle, double Abstand)? WegstelleBei(double x, double z, double hoechstens = 10)
    {
        if (_w.Karte is not { } karte) return null;
        (int, double, double)? best = null;
        for (var i = 0; i < karte.Wege.Count; i++)
        {
            var (stelle, d) = Wegbelag.Projektion(karte.Wege[i].Verlauf, x, z);
            if (d <= hoechstens && (best is null || d < best.Value.Item3)) best = (i, stelle, d);
        }
        return best;
    }

    /// <summary>Ansehen aus einem angenommenen Ratsantrag „Weg pflastern“ (für <see cref="Stadtrat.Ansehen"/>).</summary>
    public double AnsehenBonus(Konto konto) =>
        IstAktiv && _ansehenBis.TryGetValue(konto, out var bis) && bis >= _w.Jahr ? Daten!.Rat.AntragAnsehen : 0;

    private bool ImBau(int weg, int i) => _baustellen.Any(b => b.Weg == weg && i >= b.VonStueck && i <= b.BisStueck);

    private double Verkehr(Wegzustand z, int i) => z.Karren[i] + z.Grundverkehr + z.Fuss[i] * Daten!.Verkehr.FussFaktor;

    private double Tempo(Wegzustand z, int i)
    {
        var s = z.Stufe[i];
        if (!Wegbelag.IstBefestigt(s)) return Daten!.TempoVon(s);
        var unten = Daten!.TempoVon(s - 1);
        return unten + (Daten.TempoVon(s) - unten) * z.Zustand[i];
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Start aus der Karte
    // ═════════════════════════════════════════════════════════════════════════

    private Wegzustand[] Start()
    {
        if (!IstAktiv) return Array.Empty<Wegzustand>();
        var karte = _w.Karte!;
        var d = Daten!;
        var maerkte = karte.Gebaeude.Where(g => g.Katalog == "markt").Select(g => new Kartenort(g.Punkt[0], g.Punkt[1])).ToList();
        var fussorte = karte.Gebaeude.Select(g => (Ort: new Kartenort(g.Punkt[0], g.Punkt[1]), Gewicht: d.Verkehr.FussGewicht.GetValueOrDefault(g.Art)))
            .Where(x => x.Gewicht > 0).ToList();
        var wege = new Wegzustand[karte.Wege.Count];
        for (var w = 0; w < wege.Length; w++)
        {
            var weg = karte.Wege[w];
            var laenge = weg.Laenge;
            var n = Math.Max(1, (int)Math.Ceiling(laenge / d.Stueck - 1e-9));
            var z = new Wegzustand
            {
                Laenge = laenge, Stufe = new Belagstufe[n], Austritt = new double[n], Zustand = new double[n], Karren = new double[n],
                Fuss = new double[n], Ratsstueck = new bool[n], Grundverkehr = d.Verkehr.GrundverkehrVon(weg.Klasse),
            };
            for (var i = 0; i < n; i++)
            {
                var mitte = Math.Min((i + 0.5) * d.Stueck, (i * d.Stueck + laenge) / 2);
                var (stufe, zustand) = Wegbelag.StufeBei(weg.Klasse, laenge, weg.Belag, mitte);
                z.Stufe[i] = stufe;
                z.Zustand[i] = zustand;
                z.Austritt[i] = stufe == Belagstufe.Trampelpfad ? 0 : 1;
                var ort = Wegbelag.Punkt(weg.Verlauf, mitte);
                foreach (var (o, gewicht) in fussorte)
                    if (o.Abstand(ort) <= d.Verkehr.FussRadius) z.Fuss[i] += gewicht;
                z.Ratsstueck[i] = weg.Klasse == Wegklasse.Haupt || maerkte.Any(m => m.Abstand(ort) <= d.Rat.MarktRadius);
            }
            wege[w] = z;
        }
        return wege;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Verkehr und Jahreswechsel
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Ein Karren dieses Betriebs fährt los (zum Markt oder zurück) – zählt für den Verkehr seiner Strecke.</summary>
    internal void FahrtZaehlen(Betrieb betrieb)
    {
        if (IstAktiv) _fahrten[betrieb.Name] = _fahrten.GetValueOrDefault(betrieb.Name) + 1;
    }

    /// <summary>
    /// Zum Jahreswechsel, vor der Ratssitzung: die Karrenfahrten des Jahres auf die Strecken verteilen, Austritt und Verfall, Geld in die
    /// Wegekasse.
    /// </summary>
    internal void Jahresende()
    {
        if (!IstAktiv) return;
        var d = Daten!;
        var wege = Wege;
        foreach (var z in wege) Array.Clear(z.Karren);
        foreach (var b in _w.Betriebe.OrderBy(b => b.Name, StringComparer.Ordinal))
        {
            if (_fahrten.GetValueOrDefault(b.Name) is not (> 0 and var anzahl) || _w.Fahrten(b) is not var (hin, _)) continue;
            foreach (var (weg, i) in hin.Where(s => s.Art == Fahrbahn.Strasse && s.Weg >= 0 && s.Weg < wege.Length)
                         .Select(s => (s.Weg, StueckBei(s.Weg, s.Stelle))).Distinct())
                wege[weg].Karren[i] += anzahl;
        }
        for (var w = 0; w < wege.Length; w++)
        {
            var z = wege[w];
            for (var i = 0; i < z.Anzahl; i++)
            {
                if (ImBau(w, i)) continue;
                if (!Wegbelag.IstBefestigt(z.Stufe[i]))
                {
                    var v = d.Verkehr;
                    var delta = Math.Clamp((Verkehr(z, i) - v.Halten) / v.Voll, -v.Verwildern, v.HoechstensJeJahr);
                    z.Austritt[i] = Math.Clamp(z.Austritt[i] + delta, 0, 1);
                    if (z.Austritt[i] < 1e-9) z.Austritt[i] = 0; // zehnmal −0,1 ist sonst nicht ganz 0
                    else if (z.Austritt[i] > 1 - 1e-9) z.Austritt[i] = 1;
                    if (z.Stufe[i] == Belagstufe.Trampelpfad && z.Austritt[i] >= 1) z.Stufe[i] = Belagstufe.Fahrweg;
                    else if (z.Stufe[i] == Belagstufe.Fahrweg && z.Austritt[i] <= 0) z.Stufe[i] = Belagstufe.Trampelpfad;
                    continue;
                }
                z.Zustand[i] = Math.Max(0, Math.Round(z.Zustand[i] - d.VerfallVon(z.Stufe[i]), 9));
                if (z.Zustand[i] >= d.VerfallSchwelle) continue;
                // eine Stufe zurück: Pflaster wird zu Kies, Kies zum Fahrweg
                z.Stufe[i]--;
                z.Zustand[i] = 1;
                z.Austritt[i] = 1;
            }
        }
        _fahrtenVorjahr = new Dictionary<string, int>(_fahrten, StringComparer.Ordinal);
        _fahrten.Clear();
        Wegekasse = Math.Min(d.Rat.KasseHoechstens, Wegekasse + d.Rat.BudgetJeJahr);
        Geaendert();
    }

    private void Geaendert()
    {
        _w.FahrzeitenVeraltet();
        BelagGeaendert?.Invoke();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Bauaufträge
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Was es kostet, den Abschnitt von <paramref name="von"/> bis <paramref name="bis"/> (Meter entlang des Verlaufs, auf ganze Stücke
    /// erweitert) zu <paramref name="stufe"/> auszubauen, und wie lange es dauert – oder warum es nicht geht. <paramref name="bauherr"/>
    /// null: die Stadt (zahlt aus der Wegekasse).
    /// </summary>
    public Wegebauvorschau Vorschau(Konto? bauherr, int weg, double von, double bis, Belagstufe stufe)
    {
        Wegebauvorschau Nein(string grund) => new(von, bis, 0, 0, 0, grund);
        if (!IstAktiv) return Nein("In dieser Stadt werden keine Wege gebaut.");
        if (weg < 0 || weg >= Wege.Length) return Nein("Diesen Weg gibt es nicht.");
        if (!Wegbelag.IstBefestigt(stufe))
            return Nein("Ausbauen lässt sich ein Weg nur mit Kies oder Pflaster – Trampelpfad und Fahrweg tritt der Verkehr aus.");
        if (double.IsNaN(von) || double.IsNaN(bis)) return Nein("Der Abschnitt ist leer.");
        if (von > bis) (von, bis) = (bis, von);
        var z = Wege[weg];
        von = Math.Clamp(von, 0, z.Laenge);
        bis = Math.Clamp(bis, 0, z.Laenge);
        if (bis - von < 0.01) return Nein("Der Abschnitt ist leer.");
        var (i0, i1) = (StueckBei(weg, von), Math.Max(StueckBei(weg, von), (int)Math.Ceiling(bis / StueckLaenge - 1e-9) - 1));
        i1 = Math.Min(i1, z.Anzahl - 1);
        var (anfang, ende) = (Stueck(weg, i0).Von, Stueck(weg, i1).Bis);
        var d = Daten!;
        if (ende - anfang > d.HoechstensMeter + 1e-6)
            return Nein($"Ein Bauauftrag ist höchstens {Zahlenformat.Ganz(d.HoechstensMeter)} m lang – dieser wäre {Zahlenformat.Ganz(ende - anfang)} m.");
        if (_baustellen.Any(b => b.Weg == weg && b.VonStueck <= i1 && b.BisStueck >= i0))
            return Nein("Auf diesem Abschnitt wird schon gebaut.");
        var (kosten, meter, zeit) = (0.0, 0.0, 0.0);
        for (var i = i0; i <= i1; i++)
        {
            var l = Stueck(weg, i).Laenge;
            var anteil = z.Stufe[i] > stufe ? 0 : z.Stufe[i] == stufe ? 1 - z.Zustand[i] : 1;
            if (anteil <= 1e-9) continue;
            kosten += d.PreisVon(stufe) * l * anteil;
            meter += l;
            zeit += d.BauzeitVon(stufe) * l;
        }
        var minuten = d.BauzeitGrund + (int)Math.Round(zeit, MidpointRounding.AwayFromZero);
        string? grund = null;
        if (kosten <= 1e-6) grund = $"Der Abschnitt ist schon mit {Wegbelag.Anzeigename(stufe)} oder besser ausgebaut.";
        else if (bauherr is not null && bauherr.Vermoegen < kosten)
            grund = $"Der Ausbau kostet {Zahlenformat.Ganz(kosten)} {_w.Daten.Allgemein.Waehrung} – so viel Geld hat {bauherr.Inhaber} nicht.";
        else if (bauherr is null && Wegekasse < kosten - 1e-6)
            grund = $"Der Ausbau kostet {Zahlenformat.Ganz(kosten)} {_w.Daten.Allgemein.Waehrung} – in der Wegekasse liegen nur {Zahlenformat.Ganz(Wegekasse)}.";
        return new Wegebauvorschau(anfang, ende, meter, kosten, minuten, grund);
    }

    /// <summary>Befehl „Weg ausbauen“ (Spieler, KI) bzw. ein Bau der Stadt (<paramref name="bauherr"/> null): bezahlen, die Baustelle beginnt.</summary>
    internal Befehlsergebnis Ausbauen(Konto? bauherr, int weg, double von, double bis, Belagstufe stufe, Spielzeit zeit)
    {
        var v = Vorschau(bauherr, weg, von, bis, stufe);
        if (v.Verboten is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        if (bauherr is not null) bauherr.Investition(v.Kosten);
        else Wegekasse -= v.Kosten;
        var b = new Wegbaustelle(weg, v.Von, v.Bis, stufe, bauherr, zeit.VergangeneMinuten + v.BauzeitMinuten, v.Kosten)
        {
            VonStueck = StueckBei(weg, v.Von + 1e-6), BisStueck = StueckBei(weg, v.Bis - 1e-6),
        };
        _baustellen.Add(b);
        var name = WegName(weg);
        var waehrung = _w.Daten.Allgemein.Waehrung;
        var text = $"{b.BauherrName} lässt {name} auf {Zahlenformat.Ganz(v.Meter)} m mit {Wegbelag.Anzeigename(stufe)} ausbauen " +
                   $"({Zahlenformat.Ganz(v.Kosten)} {waehrung}, fertig in {Dauer(v.BauzeitMinuten)}).";
        _w.Melden(text);
        BelagGeaendert?.Invoke();
        return Befehlsergebnis.Ok(text);
    }

    /// <summary>Jede Spielminute: fertige Baustellen übernehmen.</summary>
    internal void Minute(Spielzeit zeit)
    {
        if (_baustellen.Count == 0 || !_baustellen.Any(b => zeit.VergangeneMinuten >= b.FertigAb)) return;
        foreach (var b in _baustellen.Where(b => zeit.VergangeneMinuten >= b.FertigAb).ToList())
        {
            _baustellen.Remove(b);
            var z = Wege[b.Weg];
            for (var i = b.VonStueck; i <= b.BisStueck; i++)
            {
                if (z.Stufe[i] > b.Stufe) continue;
                z.Stufe[i] = b.Stufe;
                z.Zustand[i] = 1;
                z.Austritt[i] = 1;
            }
            _w.Melden($"{WegName(b.Weg)}: Der Ausbau mit {Wegbelag.Anzeigename(b.Stufe)} ist fertig ({b.BauherrName}).");
        }
        Geaendert();
    }

    internal string WegName(int weg) =>
        _w.Karte!.Wege[weg] is { } w ? (w.Name != "" ? w.Name : $"die {Kartenweg.Anzeigename(w.Klasse)}") : "der Weg";

    private static string Dauer(int minuten) =>
        minuten >= Spielzeit.MinutenProTag ? $"{Zahlenformat.Knapp(minuten / (double)Spielzeit.MinutenProTag)} Jahren"
        : minuten >= 60 ? $"{Zahlenformat.Knapp(minuten / 60.0)} Stunden" : $"{minuten} Minuten";

    // ═════════════════════════════════════════════════════════════════════════
    //  Was ein Ausbau den Karren eines Hauses bringt (für die KI)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// So viele Spielminuten im Jahr wären die Karren dieses Hauses schneller, wenn die Stücke <paramref name="vonStueck"/> bis
    /// <paramref name="bisStueck"/> des Weges <paramref name="stufe"/> hätten (neu) – mit den Fahrten des Vorjahres, hin und zurück gemittelt.
    /// </summary>
    public double ErsparteMinutenJeJahr(Konto konto, int weg, int vonStueck, int bisStueck, Belagstufe stufe)
    {
        if (StueckZahl(weg) == 0) return 0;
        var d = Daten!;
        var z = Wege[weg];
        var steigung = _w.Karte!.Gelaendeform.HatHoehen ? _w.Daten.Logistik.Steigung : null;
        var neu = d.TempoVon(d.Bezug) / d.TempoVon(stufe);
        var summe = 0.0;
        foreach (var b in _w.Betriebe.Where(b => b.Konto == konto))
        {
            if (FahrtenVorjahr(b.Name) is not (> 0 and var anzahl) || _w.Fahrten(b) is not var (hin, zurueck)) continue;
            double Ersparnis(IReadOnlyList<Fahrtstueck> fahrt)
            {
                var laenge = fahrt.Sum(s => s.Laenge);
                if (laenge <= 0) return 0;
                var gespart = 0.0;
                foreach (var s in fahrt)
                {
                    if (s.Weg != weg || s.Art != Fahrbahn.Strasse) continue;
                    var i = StueckBei(weg, s.Stelle);
                    if (i < vonStueck || i > bisStueck || z.Stufe[i] > stufe) continue;
                    gespart += s.Laenge * (steigung?.Faktor(s.Steigung) ?? 1) * Math.Max(0, Faktor(s) - neu);
                }
                return _w.Daten.Logistik.FahrzeitAb(b.Ort) * gespart / laenge;
            }
            summe += anzahl * (Ersparnis(hin) + Ersparnis(zurueck)) / 2;
        }
        return summe;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Stadtrat: Antrag „Weg pflastern“, Pflaster der Stadt
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Befehl „Weg pflastern“ im Rat beantragen (nur Ratsmitglieder, ein Antrag je Jahr; abgestimmt wird am Jahresende).</summary>
    internal Befehlsergebnis Beantragen(Konto konto, int weg, double von, double bis)
    {
        if (!IstAktiv) return Befehlsergebnis.Abgelehnt("In dieser Stadt werden keine Wege gebaut.");
        if (!_w.Rat.IstAktiv || _w.Rat.AmtVon(konto) is null) return Befehlsergebnis.Abgelehnt("Anträge stellen dürfen nur Mitglieder des Rates.");
        if (Antrag is { } vorliegend)
            return Befehlsergebnis.Abgelehnt($"Für dieses Jahr liegt schon ein Antrag auf Pflaster vor ({vorliegend.Antragsteller.Inhaber}).");
        var v = Vorschau(null, weg, von, bis, Belagstufe.Pflaster);
        if (v.Verboten is { } grund && v.Kosten <= 0) return Befehlsergebnis.Abgelehnt(grund);
        Antrag = (konto, weg, v.Von, v.Bis);
        return Befehlsergebnis.Ok($"{konto.Inhaber} beantragt, {WegName(weg)} auf {Zahlenformat.Ganz(v.Meter)} m pflastern zu lassen " +
                                  $"(etwa {Zahlenformat.Ganz(v.Kosten)} {_w.Daten.Allgemein.Waehrung} aus der Wegekasse). Der Rat stimmt am Jahresende ab.");
    }

    /// <summary>
    /// Wie ginge die Abstimmung über einen Antrag auf Pflaster heute aus? Ratsherren stimmen dafür bei Gunst ab <c>rat.antragGunst</c> oder
    /// Verkehr ab <c>rat.antragVerkehr</c> (Mittel des Abschnitts), sonst dagegen; KI-Häuser nach eigenem Ermessen; der Antragsteller dafür;
    /// andere Häuser enthalten sich.
    /// </summary>
    public (int Ja, int Nein, int Enthaltung, bool? Buergermeister) AntragPrognose(Konto antragsteller, int weg, double von, double bis)
    {
        var verkehr = MittlererVerkehr(weg, von, bis);
        return _w.Rat.Zaehlen(sitz => StimmeVonSitz(sitz, antragsteller, weg, von, bis, verkehr));
    }

    /// <summary>
    /// Die Stimme eines Sitzes zum Antrag „Weg pflastern“ (true dafür, false dagegen, null Enthaltung) – in Prognose und Sitzung dieselbe.
    /// <paramref name="verkehr"/>: <see cref="MittlererVerkehr"/> des Abschnitts.
    /// </summary>
    internal bool? StimmeVonSitz(Amt sitz, Konto antragsteller, int weg, double von, double bis, double verkehr) =>
        sitz.Ratsherr is { } r ? _w.Rat.Meinung(r, antragsteller) >= Daten!.Rat.AntragGunst || verkehr >= Daten.Rat.AntragVerkehr
        : sitz.Konto == antragsteller ? true
        : _w.Rivalen.FirstOrDefault(k => k.Konto == sitz.Konto) is { } ki ? ki.StimmtFuerWegantrag(weg, von, bis)
        : Antrag == (antragsteller, weg, von, bis) ? StimmeVon(sitz.Konto!) // die Stimme aus der Sitzung gilt nur dem vorliegenden Antrag
        : null;

    /// <summary>
    /// Darf dieses Haus zum Antrag „Weg pflastern“ stimmen? (null = ja, sonst der Grund) Anders als bei den übrigen Anträgen nur live
    /// in der laufenden Ratssitzung – vorher enthält sich das Haus.
    /// </summary>
    public string? StimmeVerboten(Konto konto)
    {
        if (!IstAktiv) return "In dieser Stadt werden keine Wege gebaut.";
        if (_w.Rat.StimmeVerboten(konto) is { } grund) return grund;
        if (Antrag is not { } antrag) return "Dieses Jahr liegt kein Antrag auf Pflaster vor.";
        if (antrag.Antragsteller == konto) return $"{konto.Inhaber} hat den Antrag gestellt und stimmt ohnehin dafür.";
        if (_w.Rat.LaufendeSitzung is null) return "Über das Pflaster stimmt man erst in der Ratssitzung ab – bis dahin gilt Enthaltung.";
        return null;
    }

    /// <summary>Die Stimme dieses Hauses zum vorliegenden Antrag (true = dafür, false = dagegen, null = Enthaltung).</summary>
    public bool? StimmeVon(Konto konto) => StimmeVerboten(konto) is null && _stimmen.TryGetValue(konto, out var ja) ? ja : null;

    /// <summary>Stimme zum Antrag „Weg pflastern“ in der Ratssitzung setzen (true/false) oder sich enthalten (null).</summary>
    internal Befehlsergebnis StimmeFestlegen(Konto konto, bool? ja)
    {
        if (StimmeVerboten(konto) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var antrag = Antrag!.Value;
        var worum = $"den Antrag von {antrag.Antragsteller.Inhaber}, {WegName(antrag.Weg)} zu pflastern";
        if (ja is not { } stimme)
        {
            _stimmen.Remove(konto);
            return Befehlsergebnis.Ok($"{konto.Inhaber} enthält sich bei der Abstimmung über {worum}.");
        }
        _stimmen[konto] = stimme;
        return Befehlsergebnis.Ok($"{konto.Inhaber} stimmt {(stimme ? "für" : "gegen")} {worum}.");
    }

    /// <summary>Mittlerer Verkehr eines Abschnitts (Fahrten im Jahr).</summary>
    public double MittlererVerkehr(int weg, double von, double bis)
    {
        if (StueckZahl(weg) == 0) return 0;
        var (i0, i1) = (StueckBei(weg, Math.Min(von, bis)), StueckBei(weg, Math.Max(von, bis) - 1e-6));
        return Enumerable.Range(i0, Math.Max(1, i1 - i0 + 1)).Average(i => Verkehr(Wege[weg], i));
    }

    /// <summary>Ein bankrottes oder erloschenes Haus verliert seinen Antrag.</summary>
    internal void Ausscheiden(Konto konto)
    {
        if (Antrag?.Antragsteller == konto) Antrag = null;
        _stimmen.Remove(konto);
    }

    /// <summary>In der Ratssitzung (nach dem Statut): Abstimmung über den vorliegenden Antrag „Weg pflastern“.</summary>
    internal (int Ja, int Nein, int Enthaltung, bool? Buergermeister, bool Angenommen) AntragAbstimmen(int jahr, Action<string> protokoll)
    {
        var antrag = Antrag!.Value;
        var (ja, nein, enthaltung, bm) = AntragPrognose(antrag.Antragsteller, antrag.Weg, antrag.Von, antrag.Bis);
        var angenommen = Stadtrat.IstAngenommen(ja, nein, bm);
        var was = $"Ratssitzung {jahr}: Antrag von {antrag.Antragsteller.Inhaber}, {WegName(antrag.Weg)} zu pflastern,";
        var stimmen = $"({ja} dafür, {nein} dagegen{(ja == nein ? ", der Bürgermeister entscheidet" : "")})";
        if (!angenommen) protokoll($"{was} abgelehnt {stimmen}.");
        else if (Ausbauen(null, antrag.Weg, antrag.Von, antrag.Bis, Belagstufe.Pflaster, _w.Uhr) is { Erfolg: false } e)
            protokoll($"{was} angenommen {stimmen}, aber es wird nichts daraus: {e.Meldung}");
        else
        {
            _ansehenBis[antrag.Antragsteller] = _w.Jahr + Daten!.Rat.AnsehenJahre - 1;
            protokoll($"{was} ANGENOMMEN {stimmen}. {antrag.Antragsteller.Inhaber} gewinnt Ansehen.");
        }
        Antrag = null;
        return (ja, nein, enthaltung, bm, angenommen);
    }

    /// <summary>Antrag „Weg pflastern“ und danach der Bau der Stadt in einem Zug (wie in der Sitzung; für Tests).</summary>
    internal void RatSitzung(int jahr, Action<string> protokoll)
    {
        if (!IstAktiv) return;
        if (Antrag is not null) AntragAbstimmen(jahr, protokoll);
        StadtBaut(protokoll);
    }

    /// <summary>In der Ratssitzung nach dem Antrag „Weg pflastern“ (auch ohne): Die Stadt bessert aus und pflastert.</summary>
    internal void NachDemAntrag(Action<string> protokoll)
    {
        if (IstAktiv) StadtBaut(protokoll);
    }

    /// <summary>
    /// Die Stadt baut aus der Wegekasse an Hauptstraßen und Markt: zuerst das schlechteste eigene Kies oder Pflaster unter
    /// <c>rat.ausbessernAb</c> ausbessern, dann pflastern, wo der meiste Verkehr ist – je höchstens <c>rat.hoechstensMeterJeJahr</c>.
    /// </summary>
    private void StadtBaut(Action<string> protokoll)
    {
        var d = Daten!;
        var wege = Wege;
        var hoechstens = Math.Max(1, (int)Math.Floor(d.Rat.HoechstensMeterJeJahr / d.Stueck + 1e-9));

        // Ausbessern: das Stück mit dem schlechtesten Zustand, dann seine Nachbarn mit derselben Stufe
        var schlecht = Kandidaten(i => Wegbelag.IstBefestigt(wege[i.Weg].Stufe[i.I]) && wege[i.Weg].Zustand[i.I] < d.Rat.AusbessernAb)
            .OrderBy(x => wege[x.Weg].Zustand[x.I]).ThenBy(x => x.Weg).ThenBy(x => x.I).FirstOrDefault((Weg: -1, I: -1));
        if (schlecht.Weg >= 0)
        {
            var stufe = wege[schlecht.Weg].Stufe[schlecht.I];
            Bauen(schlecht, stufe, i => wege[schlecht.Weg].Stufe[i] == stufe && wege[schlecht.Weg].Zustand[i] < d.Rat.AusbessernAb, hoechstens, protokoll);
        }

        // Pflastern: das Stück mit dem meisten Verkehr, das noch kein Pflaster hat
        var best = Kandidaten(i => wege[i.Weg].Stufe[i.I] < Belagstufe.Pflaster)
            .OrderByDescending(x => Verkehr(wege[x.Weg], x.I)).ThenBy(x => x.Weg).ThenBy(x => x.I).FirstOrDefault((Weg: -1, I: -1));
        if (best.Weg >= 0)
            Bauen(best, Belagstufe.Pflaster, i => wege[best.Weg].Stufe[i] < Belagstufe.Pflaster, hoechstens, protokoll);
    }

    /// <summary>Stücke an Hauptstraßen und Markt, auf denen nicht gebaut wird und die die Bedingung erfüllen.</summary>
    private IEnumerable<(int Weg, int I)> Kandidaten(Func<(int Weg, int I), bool> bedingung)
    {
        var wege = Wege;
        for (var w = 0; w < wege.Length; w++)
            for (var i = 0; i < wege[w].Anzahl; i++)
                if (wege[w].Ratsstueck[i] && !ImBau(w, i) && bedingung((w, i)))
                    yield return (w, i);
    }

    /// <summary>
    /// Baut von einem Stück aus nach beiden Seiten (abwechselnd, erst zum Ende hin), solange die Nachbarn Ratsstücke sind, die Bedingung
    /// erfüllen und die Wegekasse reicht – höchstens <paramref name="hoechstens"/> Stücke.
    /// </summary>
    private void Bauen((int Weg, int I) start, Belagstufe stufe, Func<int, bool> passt, int hoechstens, Action<string> protokoll)
    {
        var z = Wege[start.Weg];
        bool Frei(int i) => i >= 0 && i < z.Anzahl && z.Ratsstueck[i] && !ImBau(start.Weg, i) && passt(i);
        double Kosten(int a, int b) => Vorschau(null, start.Weg, Stueck(start.Weg, a).Von, Stueck(start.Weg, b).Bis, stufe).Kosten;
        var (a, b) = (start.I, start.I);
        if (Kosten(a, b) > Wegekasse + 1e-6) return;
        var vorwaerts = true;
        while (b - a + 1 < hoechstens)
        {
            var (na, nb) = vorwaerts ? (a, b + 1) : (a - 1, b);
            var neu = vorwaerts ? nb : na;
            if (!Frei(neu) || Kosten(na, nb) > Wegekasse + 1e-6)
            {
                // diese Seite ist zu Ende – die andere versuchen; geht auch die nicht, ist Schluss
                var (ma, mb) = vorwaerts ? (a - 1, b) : (a, b + 1);
                var anders = vorwaerts ? ma : mb;
                if (!Frei(anders) || Kosten(ma, mb) > Wegekasse + 1e-6) break;
                (a, b) = (ma, mb);
                continue;
            }
            (a, b) = (na, nb);
            vorwaerts = !vorwaerts;
        }
        var e = Ausbauen(null, start.Weg, Stueck(start.Weg, a).Von, Stueck(start.Weg, b).Bis, stufe, _w.Uhr);
        if (e.Erfolg) protokoll(e.Meldung);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Speichern
    // ═════════════════════════════════════════════════════════════════════════

    internal WegebauStand? Sichern(Func<Konto, int> nummer) => !IstAktiv ? null : new WegebauStand
    {
        Wege = Wege.Select(z =>
        {
            var laeufe = new List<WegLaufStand>();
            for (var i = 0; i < z.Anzahl; i++)
            {
                if (laeufe.Count > 0 && laeufe[^1] is var l && l.Stufe == z.Stufe[i] && l.Austritt == z.Austritt[i] && l.Zustand == z.Zustand[i] && l.Karren == z.Karren[i])
                    laeufe[^1] = new WegLaufStand { Anzahl = l.Anzahl + 1, Stufe = l.Stufe, Austritt = l.Austritt, Zustand = l.Zustand, Karren = l.Karren };
                else
                    laeufe.Add(new WegLaufStand { Anzahl = 1, Stufe = z.Stufe[i], Austritt = z.Austritt[i], Zustand = z.Zustand[i], Karren = z.Karren[i] });
            }
            return laeufe;
        }).ToList(),
        Fahrten = new Dictionary<string, int>(_fahrten),
        FahrtenVorjahr = new Dictionary<string, int>(_fahrtenVorjahr),
        Baustellen = _baustellen.Select(b => new WegbaustelleStand
        {
            Weg = b.Weg, Von = b.Von, Bis = b.Bis, VonStueck = b.VonStueck, BisStueck = b.BisStueck, Stufe = b.Stufe,
            Bauherr = b.Bauherr is { } k ? nummer(k) : null, FertigAb = b.FertigAb, Kosten = b.Kosten,
        }).ToList(),
        Wegekasse = Wegekasse,
        AntragKonto = Antrag is { } a ? nummer(a.Antragsteller) : null,
        AntragWeg = Antrag?.Weg ?? 0, AntragVon = Antrag?.Von ?? 0, AntragBis = Antrag?.Bis ?? 0,
        AnsehenBis = _ansehenBis.OrderBy(x => nummer(x.Key)).ToDictionary(x => nummer(x.Key).ToString(), x => x.Value),
    };

    /// <summary>
    /// Nach dem Laden: Ohne Stand (alter Spielstand) beginnt der Belag wie in der Karte. Passt ein gespeicherter Weg nicht mehr zur Karte
    /// (andere Zahl von Stücken), beginnt er neu.
    /// </summary>
    internal void Wiederherstellen(WegebauStand? s, IReadOnlyList<Konto> konten)
    {
        if (!IstAktiv || s is null) return;
        var wege = Wege;
        for (var w = 0; w < wege.Length && w < s.Wege.Count; w++)
        {
            var z = wege[w];
            if (s.Wege[w].Sum(l => l.Anzahl) != z.Anzahl) continue;
            var i = 0;
            foreach (var l in s.Wege[w])
                for (var k = 0; k < l.Anzahl; k++, i++)
                    (z.Stufe[i], z.Austritt[i], z.Zustand[i], z.Karren[i]) = (l.Stufe, l.Austritt, l.Zustand, l.Karren);
        }
        foreach (var (name, n) in s.Fahrten) _fahrten[name] = n;
        _fahrtenVorjahr = new Dictionary<string, int>(s.FahrtenVorjahr, StringComparer.Ordinal);
        foreach (var b in s.Baustellen.Where(b => b.Weg >= 0 && b.Weg < wege.Length && b.BisStueck < wege[b.Weg].Anzahl))
            _baustellen.Add(new Wegbaustelle(b.Weg, b.Von, b.Bis, b.Stufe, b.Bauherr is int n ? konten[n] : null, b.FertigAb, b.Kosten)
            {
                VonStueck = b.VonStueck, BisStueck = b.BisStueck,
            });
        Wegekasse = s.Wegekasse;
        Antrag = s.AntragKonto is int ak && s.AntragWeg >= 0 && s.AntragWeg < wege.Length ? (konten[ak], s.AntragWeg, s.AntragVon, s.AntragBis) : null;
        foreach (var (nummer, bis) in s.AnsehenBis)
            _ansehenBis[konten[int.Parse(nummer)]] = bis;
    }
}
