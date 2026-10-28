using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Gesundheit;

/// <summary>
/// Eine Krankheit, an der eine Person gerade leidet. Die Werte stammen aus den Daten; gespeichert werden
/// nur Name, Ende und Behandlung (<see cref="Aus"/> baut sie beim Laden wieder auf).
/// </summary>
/// <param name="Bis">Letztes Jahr der Krankheit: Am Ende dieses Jahres ist sie überstanden (wenn man sie überlebt).</param>
public sealed record Erkrankung(string Name, int Bis, IReadOnlyDictionary<Talent, double> Talentabzug, double AnsehenAbzug,
    double SterbeChance, bool Schnapsheilbar, bool IstPest, bool NurGegengift = false)
{
    public const string PestName = "Pest";

    /// <summary>Ein Medicus kam (nur bei der Pest – gewöhnliche Krankheiten heilt er sofort).</summary>
    public bool Medicus { get; init; }

    /// <summary>Kräuterschnaps wurde gegeben (nur bei der Pest – leichte Krankheiten heilt er sofort).</summary>
    public bool Schnaps { get; init; }

    public double Abzug(Talent t) => Talentabzug.GetValueOrDefault(t);

    /// <summary>„Husten (Rhetorik −1)"</summary>
    public string Beschreibung
    {
        get
        {
            var teile = Talentabzug.Where(x => x.Value > 0).OrderBy(x => x.Key)
                .Select(x => $"{x.Key.Kurz()} −{TalentTexte.Sterne(x.Value)}").ToList();
            if (AnsehenAbzug > 0) teile.Add($"Ansehen −{Zahlenformat.Ganz(AnsehenAbzug)}");
            return teile.Count == 0 ? Name : $"{Name} ({string.Join(", ", teile)})";
        }
    }

    public static Erkrankung Aus(GesundheitDaten d, string name, int bis)
    {
        if (name == PestName)
        {
            var p = d.Pest ?? new PestDaten();
            return new Erkrankung(PestName, bis, TalentTexte.Alle.ToDictionary(t => t, _ => p.TalentAbzug), 0, p.Sterben, false, true);
        }
        if (!d.Krankheiten.TryGetValue(name, out var k))
            throw new InvalidDataException($"Die Krankheit „{name}“ gibt es in den Daten nicht.");
        return new Erkrankung(name, bis, new Dictionary<Talent, double>(k.Talente), k.Ansehen, k.Sterben, k.Schnaps && !k.Gegengift, false, k.Gegengift);
    }
}

/// <summary>
/// Krankheiten, Pest und Heilmittel der Stadt.
///
/// Ablauf je Jahr (in der Jahresabrechnung):
///   1. <see cref="Jahresende"/> – vor dem Jahreswechsel der Familien: Wer krank ist, kann daran sterben;
///      wessen Krankheit endet, wird gesund.
///   2. Familien-Jahreswechsel (Alter, Tod, Geburt …).
///   3. <see cref="NeuesJahr"/> – die Pest bricht aus (alle 25–40 Jahre), dann erkranken Einzelne.
/// Eine Krankheit dauert damit mindestens ein ganzes Spieljahr – wenn niemand sie behandelt.
///
/// Eigener Zufallsgenerator: So verschieben Krankheiten nicht die Würfe von Familien, Rat oder Markt.
/// </summary>
public sealed class Gesundheitswesen
{
    private readonly Stadtwirtschaft _w;
    private readonly GesundheitDaten? _d;
    private Zufallsquelle _zufall;
    private readonly Dictionary<Konto, int> _apotheke = new();

    public bool IstAktiv => _d is not null;
    public GesundheitDaten Daten => _d ?? throw new InvalidOperationException("Es gibt keine Krankheiten (gesundheit fehlt in den Daten).");

    /// <summary>In diesem Jahr bricht die nächste Pest aus (0 = nie).</summary>
    public int NaechstePest { get; internal set; }

    /// <summary>Wütet in diesem Jahr die Pest?</summary>
    public bool IstPestjahr { get; private set; }

    public Gesundheitswesen(Stadtwirtschaft w, GesundheitDaten? daten, int zufallsstartwert)
    {
        _w = w;
        _d = daten;
        _zufall = new Zufallsquelle(unchecked(zufallsstartwert * 41 + 19));
        if (_d?.Pest is { } p)
            NaechstePest = Spielzeit.StartJahr + PestAbstand(p);
    }

    private int PestAbstand(PestDaten p) => p.AbstandMin + _zufall.Next(p.AbstandMax - p.AbstandMin + 1);

    /// <summary>Kräuterschnaps in der Hausapotheke.</summary>
    public int Apotheke(Konto konto) => _apotheke.GetValueOrDefault(konto);

    /// <summary>Anteil der Arbeitskräfte, die gerade krank fehlen (nur im Pestjahr).</summary>
    public double Krankenstand => IstPestjahr && _d?.Pest is { } p ? p.Krankenstand : 0;

    /// <summary>Was die Krankheit des Oberhaupts am Ansehen des Hauses kostet (Haarausfall, Lachanfälle).</summary>
    public double AnsehenAbzug(Konto konto) => _w.Familien.Von(konto)?.Oberhaupt.Krankheit?.AnsehenAbzug ?? 0;

    /// <summary>Chance, am Jahresende an dieser Krankheit zu sterben.</summary>
    public double Sterberisiko(Erkrankung k)
    {
        if (!k.IstPest) return k.SterbeChance;
        var p = _d?.Pest ?? new PestDaten();
        return k.SterbeChance * (k.Medicus ? p.MedicusFaktor : 1) * (k.Schnaps ? p.SchnapsFaktor : 1);
    }

    /// <summary>Was der Medicus für diese Krankheit verlangt.</summary>
    public double MedicusKosten(Erkrankung k) => k.IstPest ? Daten.MedicusKostenPest : Daten.MedicusKosten;

    /// <summary>Was ein Kräuterschnaps heute auf dem Markt kostet.</summary>
    public double SchnapsPreis => _w.Markt[Daten.Schnaps].Preis;

    // ═════════════════════════════════════════════════════════════════════════
    //  Jahreslauf
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Alle Personen, die krank werden können: Familien der aktiven Häuser und die Brautschau.</summary>
    private IEnumerable<(Konto? Konto, Person Person)> Personen()
    {
        foreach (var konto in _w.Konten.Where(k => k.IstAktiv))
            if (_w.Familien.Von(konto) is { } f)
                foreach (var p in f.Lebende.ToList())
                    yield return (konto, p);
        foreach (var k in _w.Familien.Kandidaten.ToList())
            yield return (null, k.Person);
    }

    /// <summary>Jahresende: Kranke sterben oder genesen. Läuft vor dem Jahreswechsel der Familien.</summary>
    internal void Jahresende()
    {
        if (!IstAktiv || !_w.Familien.IstAktiv) return;
        var jahr = _w.Familien.Jahr;
        foreach (var (konto, p) in Personen().ToList())
        {
            if (p.Krankheit is not { } k || !p.Lebt) continue;
            // Gift wirkt langsam: Tödlich wird es erst am Ende seines letzten Jahres – bis dahin hilft Gegengift.
            if (k.NurGegengift && k.Bis > jahr) continue;
            if (_zufall.NextDouble() < Sterberisiko(k))
            {
                _w.Familien.AnKrankheitSterben(p, k.IstPest ? "an der Pest" : $"an {k.Name}");
                continue;
            }
            if (k.Bis > jahr) continue;
            p.Krankheit = null;
            if (konto is not null)
            {
                _w.Familien.Ereignis(konto, $"{p.Vorname} {p.Familienname} hat {(k.IstPest ? "die Pest" : k.Name)} überstanden.");
                _w.Familien.ZugAnlass(konto, p, $"ueberstanden:{k.Name}"); // Pest oder Gift überlebt → vielleicht eine Narbe (PR #227)
            }
        }
    }

    /// <summary>Neues Jahr: Pest (wenn es so weit ist), dann gewöhnliche Krankheiten. Läuft nach dem Jahreswechsel der Familien.</summary>
    internal void NeuesJahr()
    {
        if (!IstAktiv || !_w.Familien.IstAktiv) return;
        var d = Daten;
        var jahr = _w.Familien.Jahr;
        IstPestjahr = false;

        if (d.Pest is { } pest && NaechstePest > 0 && jahr >= NaechstePest)
        {
            IstPestjahr = true;
            NaechstePest = jahr + PestAbstand(pest);
            _w.Melden($"Die Pest ist in der Stadt! In diesem Jahr fehlen {Zahlenformat.Ganz(pest.Krankenstand * 100)} % der Arbeitskräfte.");
            foreach (var (konto, p) in Personen().ToList())
            {
                if (_zufall.NextDouble() >= pest.Ansteckung * pest.Anfaelligkeit(p.Volk) * _w.Ratsantraege.Krankheitsfaktor // Brunnen
                    * _w.Familien.KrankheitFaktor(p)) continue; // Charakterzüge (Kränklich, Stark – PR #227)
                p.Krankheit = Erkrankung.Aus(d, Erkrankung.PestName, jahr);
                if (konto is not null)
                    _w.Familien.Ereignis(konto, $"{p.Vorname} {p.Familienname} hat die Pest! Ohne Hilfe stirbt {Er(p)} mit {Zahlenformat.Ganz(pest.Sterben * 100)} % Wahrscheinlichkeit.");
            }
        }

        // Gewöhnliche Krankheiten – nur für Familienmitglieder (die Brautschau wechselt ohnehin ständig).
        var krankheiten = d.Krankheiten.OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
        foreach (var (konto, p) in Personen().Where(x => x.Konto is not null).ToList())
        {
            if (p.Krankheit is not null) continue;
            var wurf = _zufall.NextDouble();
            var summe = 0.0;
            foreach (var (name, k) in krankheiten)
            {
                summe += k.Chance * (k.AlterAb > 0 && p.Alter(jahr) >= k.AlterAb ? 3 : 1) * _w.Ratsantraege.Krankheitsfaktor // Brunnen der Stadt (PR #200)
                         * _w.Wohnen.KrankheitFaktor(konto!) // Küche im Wohnhaus (PR #224)
                         * _w.Familien.KrankheitFaktor(p); // Charakterzüge (PR #227)
                if (wurf >= summe) continue;
                Erkranken(konto!, p, name);
                break;
            }
        }
    }

    private static string Er(Person p) => p.Geschlecht == Geschlecht.Frau ? "sie" : "er";

    /// <summary>Eine Person erkrankt (Zufall oder Krötenschleim). Wer schon krank ist, bleibt bei seiner Krankheit.</summary>
    internal bool Erkranken(Konto konto, Person p, string name)
    {
        if (!IstAktiv || p.Krankheit is not null || !p.Lebt) return false;
        var dauer = Daten.Krankheiten.TryGetValue(name, out var k) ? k.Dauer : 1;
        p.Krankheit = Erkrankung.Aus(Daten, name, _w.Familien.Jahr + dauer - 1);
        var e = p.Krankheit;
        var bis = dauer == 1 ? "bis zum Jahresende" : $"bis Ende {e.Bis}";
        _w.Familien.Ereignis(konto, $"{p.Vorname} {p.Familienname} ist krank: {e.Beschreibung} – {bis}" +
                                    (e.SterbeChance > 0 ? $", gefährlich ({Zahlenformat.Ganz(e.SterbeChance * 100)} % tödlich)." : "."));
        return true;
    }

    /// <summary>Eine zufällige Krankheit für Krötenschleim (aus der Liste in den Daten, sonst alle gewöhnlichen).</summary>
    internal string? KroetenschleimKrankheit()
    {
        if (!IstAktiv) return null;
        var liste = Daten.Kroetenschleim.Count > 0 ? Daten.Kroetenschleim : Daten.Krankheiten.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
        return liste.Count == 0 ? null : liste[_zufall.Next(liste.Count)];
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Befehle
    // ═════════════════════════════════════════════════════════════════════════

    private Befehlsergebnis? Kranker(Konto konto, int nummer, out Person person, out Erkrankung krankheit)
    {
        person = null!;
        krankheit = null!;
        if (!IstAktiv) return Befehlsergebnis.Abgelehnt("In dieser Stadt wird niemand krank.");
        if (_w.Familien.Von(konto) is not { } f) return Befehlsergebnis.Abgelehnt($"{konto.Inhaber} hat keine Familie.");
        if (f.Lebende.FirstOrDefault(x => x.Nummer == nummer) is not { } p) return Befehlsergebnis.Abgelehnt("Diese Person gibt es im Haus nicht.");
        if (p.Krankheit is not { } k) return Befehlsergebnis.Abgelehnt($"{p.Vorname} ist gesund.");
        person = p;
        krankheit = k;
        return null;
    }

    internal Befehlsergebnis MedicusRufen(Konto konto, int nummer)
    {
        if (Kranker(konto, nummer, out var p, out var k) is { } fehler) return fehler;
        if (k.IstPest && k.Medicus) return Befehlsergebnis.Abgelehnt($"Der Medicus pflegt {p.Vorname} schon.");
        if (k.NurGegengift) return Befehlsergebnis.Abgelehnt($"Gegen {k.Name} weiß der Medicus nichts – da hilft nur {Daten.Gegengift}.");
        var kosten = MedicusKosten(k);
        var waehrung = _w.Daten.Allgemein.Waehrung;
        if (konto.Vermoegen < kosten)
            return Befehlsergebnis.Abgelehnt($"Der Medicus verlangt {Zahlenformat.Ganz(kosten)} {waehrung} – so viel habt ihr nicht.");
        konto.Familienkosten(kosten);
        if (k.IstPest)
        {
            p.Krankheit = k with { Medicus = true };
            return Befehlsergebnis.Ok($"Der Medicus pflegt {p.Vorname} ({Zahlenformat.Ganz(kosten)} {waehrung}): " +
                                      $"Das Sterberisiko sinkt auf {Zahlenformat.Ganz(Sterberisiko(p.Krankheit) * 100)} %.");
        }
        p.Krankheit = null;
        return Befehlsergebnis.Ok($"Der Medicus kuriert {p.Vorname} ({Zahlenformat.Ganz(kosten)} {waehrung}): {k.Name} ist geheilt.");
    }

    internal Befehlsergebnis SchnapsKaufen(Konto konto, int stueck)
    {
        if (!IstAktiv) return Befehlsergebnis.Abgelehnt("In dieser Stadt wird niemand krank.");
        if (stueck is < 1 or > 20) return Befehlsergebnis.Abgelehnt("Zwischen 1 und 20 Flaschen auf einmal.");
        var preis = _w.Markt[Daten.Schnaps].Kaufen(stueck);
        if (konto.Vermoegen < preis)
            return Befehlsergebnis.Abgelehnt($"{stueck} × {Daten.Schnaps} kosten {Zahlenformat.Ganz(preis)} {_w.Daten.Allgemein.Waehrung} – so viel habt ihr nicht.");
        konto.Familienkosten(preis);
        _apotheke[konto] = Apotheke(konto) + stueck;
        return Befehlsergebnis.Ok($"{stueck} × {Daten.Schnaps} für {Zahlenformat.Ganz(preis)} {_w.Daten.Allgemein.Waehrung} gekauft – " +
                                  $"in der Hausapotheke stehen jetzt {Apotheke(konto)}.");
    }

    internal Befehlsergebnis SchnapsGeben(Konto konto, int nummer)
    {
        if (Kranker(konto, nummer, out var p, out var k) is { } fehler) return fehler;
        if (Apotheke(konto) <= 0) return Befehlsergebnis.Abgelehnt($"Die Hausapotheke ist leer – erst {Daten.Schnaps} kaufen.");
        if (k.IstPest)
        {
            if (k.Schnaps) return Befehlsergebnis.Abgelehnt($"{p.Vorname} hat schon {Daten.Schnaps} bekommen – mehr hilft nicht.");
            p.Krankheit = k with { Schnaps = true };
            _apotheke[konto]--;
            return Befehlsergebnis.Ok($"{p.Vorname} trinkt {Daten.Schnaps}: Das Sterberisiko sinkt auf {Zahlenformat.Ganz(Sterberisiko(p.Krankheit) * 100)} %.");
        }
        if (!k.Schnapsheilbar) return Befehlsergebnis.Abgelehnt($"Gegen {k.Name} hilft {Daten.Schnaps} nicht – nur der Medicus.");
        p.Krankheit = null;
        _apotheke[konto]--;
        return Befehlsergebnis.Ok($"{p.Vorname} trinkt {Daten.Schnaps}: {k.Name} ist geheilt.");
    }

    /// <summary>Was ein Gegengift dieses Haus kostet: 0 aus dem eigenen Lager, sonst der Marktpreis.</summary>
    public double GegengiftPreis(Konto konto) =>
        !IstAktiv || Daten.Gegengift == "" ? 0
        : _w.Betriebe.Any(b => b.Konto == konto && b.ImLager(Daten.Gegengift) > 0) ? 0
        : _w.Markt[Daten.Gegengift].Preis;

    /// <summary>Eine Vergiftung heilen – mit Gegengift aus dem eigenen Lager oder vom Markt (Familienkosten).</summary>
    internal Befehlsergebnis GegengiftGeben(Konto konto, int nummer)
    {
        if (Kranker(konto, nummer, out var p, out var k) is { } fehler) return fehler;
        if (Daten.Gegengift == "") return Befehlsergebnis.Abgelehnt("In dieser Stadt gibt es kein Gegengift.");
        if (!k.NurGegengift) return Befehlsergebnis.Abgelehnt($"{p.Vorname} ist nicht vergiftet – gegen {k.Name} hilft {Daten.Gegengift} nicht.");
        var waehrung = _w.Daten.Allgemein.Waehrung;
        var preis = GegengiftPreis(konto);
        if (konto.Vermoegen < preis)
            return Befehlsergebnis.Abgelehnt($"{Daten.Gegengift} kostet am Markt {Zahlenformat.Ganz(preis)} {waehrung} – so viel habt ihr nicht.");
        if (preis > 0) konto.Familienkosten(_w.Markt[Daten.Gegengift].Kaufen(1));
        else _w.Betriebe.First(b => b.Konto == konto && b.ImLager(Daten.Gegengift) > 0).Auslagern(Daten.Gegengift, 1);
        p.Krankheit = null;
        return Befehlsergebnis.Ok($"{p.Vorname} trinkt {Daten.Gegengift} ({(preis > 0 ? $"{Zahlenformat.Ganz(preis)} {waehrung} am Markt" : "aus dem eigenen Lager")}): " +
                                  $"Die {k.Name} ist überstanden.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Speichern
    // ═════════════════════════════════════════════════════════════════════════

    internal GesundheitStand Sichern(Func<Konto, int> kontoNummer) => new()
    {
        ZufallWuerfe = _zufall.Wuerfe,
        NaechstePest = NaechstePest,
        Pestjahr = IstPestjahr,
        // Sortiert: Ein Dictionary zählt sonst in Einfügereihenfolge auf – nach dem Laden wäre die Datei eine andere.
        Apotheke = _apotheke.Where(x => x.Value > 0).OrderBy(x => kontoNummer(x.Key)).ToDictionary(x => kontoNummer(x.Key), x => x.Value),
    };

    internal void Wiederherstellen(GesundheitStand s, IReadOnlyList<Konto> konten)
    {
        _zufall = new Zufallsquelle(_zufall.Startwert, s.ZufallWuerfe);
        NaechstePest = s.NaechstePest;
        IstPestjahr = s.Pestjahr;
        _apotheke.Clear();
        foreach (var (nummer, stueck) in s.Apotheke)
            _apotheke[konten[nummer]] = stueck;
    }
}
