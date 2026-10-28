using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Dynastie;

/// <summary>
/// Was eine Aktivität mit dieser Person verspricht – für die Oberfläche (eine Zeile je Aktivität) und die KI.
/// </summary>
/// <param name="Art">Kennung für den Befehl <see cref="Aktivitaet"/> („Gastmahl“).</param>
/// <param name="Gulden">Kosten; beim Geschenk der Betrag, nach dem gefragt wurde.</param>
/// <param name="Gefahr">Chance, dass es schiefgeht (0 … 1), nach Zügen und Talent.</param>
/// <param name="Wirkung">Meinung bei Erfolg, nach den Zügen der Person.</param>
/// <param name="Unglueck">Meinung, wenn es schiefgeht (negativ oder 0).</param>
/// <param name="UnglueckText">„Streit bei Tisch“.</param>
/// <param name="Erwartet">Im Mittel: (1 − Gefahr) × Wirkung + Gefahr × Unglück.</param>
/// <param name="Hinweis">„Gesellig: mag es. Scheu: mag es nicht.“ – welche Züge der Person mitspielen (leer, wenn keine).</param>
/// <param name="Sperrgrund">null = möglich, sonst warum nicht (schon in diesem Jahr, anderer Glaube, zu wenig Gulden oder AP …).</param>
public sealed record Aktivitaetsangebot(
    string Art, string Name, string Beschreibung, int Ap, double Gulden, double Gefahr, double Wirkung, double Unglueck, string UnglueckText,
    double Erwartet, string Hinweis, string? Sperrgrund);

public sealed partial class Meinungswesen
{
    /// <summary>Die Aktivitäten aus den Daten, in ihrer Reihenfolge (leer ohne Abschnitt "meinungen").</summary>
    public IEnumerable<(string Art, AktivitaetDaten Daten)> Aktivitaetsarten =>
        _d is null ? Enumerable.Empty<(string, AktivitaetDaten)>() : _d.Aktivitaeten.Select(a => (a.Key, a.Value));

    /// <summary>Die Art, die das Geschenk ist (null, wenn es keine gibt).</summary>
    public string? GeschenkArt => _d?.Aktivitaeten.FirstOrDefault(a => a.Value.Geschenk).Key;

    /// <summary>Was eine Aktivität an AP kostet (0, wenn es sie nicht gibt).</summary>
    public int ApKosten(string art) => _d?.Aktivitaeten.GetValueOrDefault(art)?.Ap ?? 0;

    /// <summary>
    /// Alle Aktivitäten, die das Oberhaupt dieses Hauses mit dieser Person unternehmen könnte – mit Kosten, Gefahr, erwarteter Wirkung
    /// und Sperrgrund. <paramref name="geschenk"/>: der Betrag, für den das Geschenk gerechnet wird.
    /// </summary>
    public IReadOnlyList<Aktivitaetsangebot> Aktivitaeten(Konto konto, ICharakter mit, double geschenk = 100)
    {
        var liste = new List<Aktivitaetsangebot>();
        if (_d is null) return liste;
        foreach (var (art, a) in _d.Aktivitaeten)
            liste.Add(Angebot(konto, mit, art, a, geschenk));
        return liste;
    }

    /// <summary>Eine einzelne Aktivität (null, wenn es sie nicht gibt).</summary>
    public Aktivitaetsangebot? Aktivitaet(Konto konto, ICharakter mit, string art, double geschenk = 100) =>
        _d?.Aktivitaeten.GetValueOrDefault(art) is { } a ? Angebot(konto, mit, art, a, geschenk) : null;

    private Aktivitaetsangebot Angebot(Konto konto, ICharakter mit, string art, AktivitaetDaten a, double geschenk)
    {
        var ich = Oberhaupt(konto);
        var gulden = a.Geschenk ? geschenk : a.Gulden;
        var sperre = Sperrgrund(konto, ich, mit, art, a, gulden);
        if (sperre is null && _w.HatAktionspunkte && !konto.Aktionspunkte.Reicht(a.Ap))
            sperre = $"Das kostet {a.Ap} AP – {konto.Inhaber} hat noch {konto.Aktionspunkte.Vorrat}.";
        if (a.Geschenk)
        {
            var wirkung = ich is null ? 0 : gulden * GeschenkJeGulden(mit, konto);
            var zuege = mit.Zuege.Where(z => Math.Abs(z.Daten.Bestechlich - 1) > 1e-9)
                .Select(z => $"{z.Name}: nimmt {(z.Daten.Bestechlich > 1 ? "gern" : "ungern")}.");
            var schon = ich is null ? 0 : _diesesJahr.GetValueOrDefault((art, mit.Kennung, ich.Kennung));
            var hinweis = string.Join(" ", zuege.Concat(schon > 0 ? new[] { $"Schon {schon}× beschenkt in diesem Jahr – jedes weitere wirkt weniger." } : Array.Empty<string>()));
            return new Aktivitaetsangebot(art, a.Name, a.Beschreibung, a.Ap, gulden, 0, wirkung, 0, "", wirkung, hinweis, sperre);
        }
        var (gefahr, wirkung2) = ich is null ? (a.Gefahr, a.Wirkung) : Bewerten(a, ich, mit);
        var mag = mit.Zuege.Where(z => a.Mag.Contains(z.Kennung)).Select(z => $"{z.Name}: mag es.");
        var magNicht = mit.Zuege.Where(z => a.MagNicht.Contains(z.Kennung)).Select(z => $"{z.Name}: mag es nicht.");
        return new Aktivitaetsangebot(art, a.Name, a.Beschreibung, a.Ap, gulden, gefahr, wirkung2, a.UnglueckWirkung, a.Unglueck,
            (1 - gefahr) * wirkung2 + gefahr * a.UnglueckWirkung, string.Join(" ", mag.Concat(magNicht)), sperre);
    }

    /// <summary>Gefahr und Wirkung nach den Zügen der Person (je Zug, der es mag: Wirkung × 1,5; der es nicht mag: × 0,5 und Gefahr × 2) und dem Talent.</summary>
    private (double Gefahr, double Wirkung) Bewerten(AktivitaetDaten a, Person ich, ICharakter mit)
    {
        var zw = _d!.Zugwirkung;
        double gefahr = a.Gefahr, wirkung = a.Wirkung;
        foreach (var z in mit.Zuege)
        {
            if (a.Mag.Contains(z.Kennung)) wirkung *= zw.Mag;
            if (a.MagNicht.Contains(z.Kennung)) { wirkung *= zw.MagNicht; gefahr *= zw.Gefahr; }
        }
        if (a.Talent is { } t && _w.Familien.HatTalente) gefahr -= ich.Talent(t) * a.GefahrJeStern;
        return (Math.Clamp(gefahr, 0, 0.95), wirkung);
    }

    /// <summary>
    /// Meinung je geschenktem Gulden von diesem Haus an diese Person: <c>politik.gunst.jeGulden</c> × Bestechlichkeit × Abnahme je Geschenk,
    /// das sie in diesem Jahr schon von ihm bekam. Für Ratsherren ist das die Gunst je Gulden (<see cref="Stadtrat.GunstJeGulden(Ratsherr, Konto)"/>).
    /// </summary>
    public double GeschenkJeGulden(ICharakter mit, Konto von)
    {
        var grund = _w.Daten.Politik.Gunst.JeGulden * Bestechlichkeit(mit);
        if (GeschenkArt is not { } art) return grund;
        var schon = _diesesJahr.GetValueOrDefault((art, mit.Kennung, HausKennung(von)));
        return grund * Math.Pow(_d!.Aktivitaeten[art].Abnahme, schon);
    }

    /// <summary>Warum diese Aktivität nicht geht (null = sie geht). Aktionspunkte prüft <see cref="Stadtwirtschaft.Ausfuehren"/>.</summary>
    private string? Sperrgrund(Konto konto, Person? ich, ICharakter mit, string art, AktivitaetDaten a, double gulden)
    {
        if (_d is null) return "In dieser Stadt gibt es keine Aktivitäten.";
        if (!konto.IstAktiv) return $"{konto.Inhaber} ist nicht mehr im Spiel.";
        if (ich is null) return $"{konto.Inhaber} hat keine Familie.";
        if (mit.Kennung == ich.Kennung) return "Mit sich selbst unternimmt man nichts.";
        if (mit is Person p && (!p.Lebt || _w.Diebe.IstGeisel(p))) return $"{p.Name} ist nicht da.";
        if (!IstWichtig(mit)) return $"{mit.Name} gehört nicht zu den wichtigen Personen der Stadt.";
        if (!a.Geschenk && _diesesJahr.ContainsKey((art, mit.Kennung, ich.Kennung)))
            return $"{a.Name} mit {mit.Name} gab es in diesem Jahr schon – im nächsten Jahr wieder.";
        if (a.GleicherGlaube && _w.Glaube.IstAktiv && GlaubeVon(mit) != ich.Glaube)
            return $"{mit.Name} hat einen anderen Glauben ({GlaubeVon(mit)?.Name()}).";
        if (a.MindestStand is { } st && konto.Stand < _w.Daten.StandStufe(st))
            return $"Erst ab dem Stand {st}.";
        if (a.Geschenk && (gulden <= 0 || double.IsNaN(gulden))) return "Ein Geschenk muss etwas wert sein.";
        if (konto.Vermoegen < gulden) return $"So viel Geld hat {konto.Inhaber} nicht ({Zahlenformat.Ganz(gulden)} {_w.Daten.Allgemein.Waehrung}).";
        return null;
    }

    /// <summary>Der Befehl <see cref="Wirtschaft.Aktivitaet"/>. Ein Geschenk an einen Ratsherrn geht über <see cref="Stadtrat"/> (Bestechung).</summary>
    internal Befehlsergebnis Ausfuehren(Konto konto, string art, string mit, double betrag)
    {
        if (_d is null) return Befehlsergebnis.Abgelehnt("In dieser Stadt gibt es keine Aktivitäten.");
        if (!_d.Aktivitaeten.TryGetValue(art, out var a)) return Befehlsergebnis.Abgelehnt($"Eine Aktivität „{art}“ gibt es nicht.");
        if (Charakter(mit) is not { } person) return Befehlsergebnis.Abgelehnt("Diese Person gibt es nicht.");
        var ich = Oberhaupt(konto);
        var gulden = a.Geschenk ? betrag : a.Gulden;
        if (Sperrgrund(konto, ich, person, art, a, gulden) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var waehrung = _w.Daten.Allgemein.Waehrung;

        if (a.Geschenk)
        {
            if (person is Ratsherr r) return _w.Rat.Schenken(konto, r.Name, gulden);
            var wert = Schenken(konto, person, gulden);
            return Befehlsergebnis.Ok($"{person.Name} nimmt das Geschenk an ({Zahlenformat.Ganz(gulden)} {waehrung}): Meinung {Zahlenformat.MitVorzeichen(wert)}, " +
                                      $"jetzt {Zahlenformat.Ganz(Meinung(person, ich!))}.");
        }

        if (gulden > 0) konto.Familienkosten(gulden);
        var (gefahr, wirkung) = Bewerten(a, ich!, person);
        _diesesJahr[(art, person.Kennung, ich!.Kennung)] = 1;
        var kosten = gulden > 0 ? $" ({Zahlenformat.Ganz(gulden)} {waehrung})" : "";
        if (gefahr > 0 && _zufall.NextDouble() < gefahr)
        {
            TatEintragen(person.Kennung, ich.Kennung, art + ":unglueck", a.Unglueck, a.UnglueckWirkung);
            var verletzt = a.Verletzung is { } krankheit && a.VerletzungChance > 0 && _zufall.NextDouble() < a.VerletzungChance
                           && _w.Gesundheit.Erkranken(konto, ich, krankheit);
            return Befehlsergebnis.Ok($"{a.Name} mit {person.Name}{kosten}: {a.Unglueck}! Meinung {Zahlenformat.MitVorzeichen(a.UnglueckWirkung)}, " +
                                      $"jetzt {Zahlenformat.Ganz(Meinung(person, ich))}." + (verletzt ? $" {ich.Vorname} hat sich verletzt: {a.Verletzung}." : ""));
        }
        TatEintragen(person.Kennung, ich.Kennung, art, a.Name, wirkung);
        return Befehlsergebnis.Ok($"{a.Name} mit {person.Name}{kosten}: Meinung {Zahlenformat.MitVorzeichen(wirkung)}, jetzt {Zahlenformat.Ganz(Meinung(person, ich))}.");
    }

    /// <summary>Ein Geschenk (auch an Ratsherren, vom Stadtrat aufgerufen): bezahlen, Meinung eintragen, mitzählen. Gibt die Wirkung zurück.</summary>
    internal double Schenken(Konto konto, ICharakter person, double betrag)
    {
        var wert = betrag * GeschenkJeGulden(person, konto);
        konto.Geschenk(betrag);
        TatEintragen(person.Kennung, HausKennung(konto), "geschenk", "Geschenke", wert);
        if (GeschenkArt is { } art)
        {
            var schluessel = (art, person.Kennung, HausKennung(konto));
            _diesesJahr[schluessel] = _diesesJahr.GetValueOrDefault(schluessel) + 1;
        }
        return wert;
    }
}
