using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Ki;

/// <summary>
/// Schiffshandel der KI-Häuser (PR #199): dieselbe Gewinnrechnung wie die Oberfläche des Spielers
/// (<see cref="Kalkulation.BesteSchiffsfahrt"/>, <see cref="Kalkulation.Schiffsfahrt"/>), das Geleit wie bei den Karren nach erwartetem Verlust.
/// Die KI handelt nur über den Markt (Hinfracht kaufen, Rückfracht verkaufen) – nicht aus den Lagern ihrer Betriebe.
/// </summary>
public sealed partial class KiRivale
{
    /// <summary>
    /// So viele Jahre darf es höchstens dauern, bis ein Schiff seinen Preis eingebracht hat (<c>ki.schiffe.hoechstensJahre</c>, nach Charakter
    /// wie bei der Einrichtung). null: Die KI kauft keine Schiffe (Abschnitt fehlt).
    /// </summary>
    public double? SchiffHoechstensJahre =>
        _w.Daten.Ki?.Schiffe is { } d
            ? d.HoechstensJahre + d.CharakterJahre * Math.Sign(Profil.MaxAmortisationJahre - KiProfil.Standard.MaxAmortisationJahre)
            : null;

    /// <summary>
    /// Jede Stunde: Jedes eigene Schiff am Kai bekommt den einträglichsten Auftrag, wenn er mindestens <c>ki.schiffe.mindestGewinnJeFahrt</c>
    /// erwarten lässt und die Hinfracht aus dem Geld über der <see cref="KiProfil.Reserve"/> bezahlt werden kann. Vorher wählt sie das Geleit,
    /// das Geleitlohn und erwarteten Raub zusammen am kleinsten hält.
    /// </summary>
    private void SchiffeLosschicken()
    {
        var s = _w.Schiffahrt;
        if (!s.IstAktiv || _w.Daten.Ki?.Schiffe is not { } d) return;
        foreach (var schiff in s.SchiffeVon(Konto).Where(x => x.Zustand == SchiffZustand.AmKai).ToList())
        {
            var budget = Konto.Vermoegen - Profil.Reserve;
            if (budget <= 0) return;
            if (Kalkulation.BesteSchiffsfahrt(_w, Konto, schiff.Daten, budget, schiff.Geleit, Vorsicht) is not { } v) continue;
            var geleit = BestesGeleit(schiff, v);
            if (geleit != schiff.Geleit)
            {
                Befehlen(new SchiffGeleitWaehlen(schiff.Name, geleit), $"{schiff.Name} fährt nun mit Geleit „{geleit}“ statt „{schiff.Geleit}“.");
                if (Kalkulation.BesteSchiffsfahrt(_w, Konto, schiff.Daten, budget, schiff.Geleit, Vorsicht) is not { } neu) continue;
                v = neu;
            }
            if (v.Rechnung.Gewinn < d.MindestGewinnJeFahrt) continue;
            var waehrung = _w.Daten.Allgemein.Waehrung;
            string Liste(IReadOnlyDictionary<string, int> x) => x.Count == 0 ? "nichts" : string.Join(", ", x.Select(y => $"{y.Value} {y.Key}"));
            Befehlen(new SchiffsauftragErteilen(schiff.Name, v.Ziel, v.Hinfracht, v.Rueckfracht),
                $"schickt {schiff.Name} nach {v.Ziel}: hin {Liste(v.Hinfracht)}, zurück {Liste(v.Rueckfracht)} – " +
                $"erwarteter Gewinn ≈ {Zahlenformat.Ganz(v.Rechnung.Gewinn)} {waehrung}.");
        }
    }

    /// <summary>Das verfügbare Geleit mit den kleinsten erwarteten Kosten (Lohn + Raub × Vorsicht) für diesen Auftrag.</summary>
    private string BestesGeleit(Schiff schiff, Fahrtvorschlag v)
    {
        var stadt = _w.Schiffahrt.Stadt(v.Ziel)!;
        return _w.Daten.Logistik.Eskorten.Keys.Where(_w.Wache.EskorteVerfuegbar)
            .Select(e => (Name: e, Rechnung: Kalkulation.Schiffsfahrt(_w, Konto, schiff.Daten, stadt, v.Hinfracht, v.Rueckfracht, false, e, Vorsicht)))
            .OrderBy(x => x.Rechnung.Geleit + x.Rechnung.Risiko)
            .ThenBy(x => _w.Daten.Logistik.Eskorten[x.Name].KostenJeFahrt)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => x.Name)
            .FirstOrDefault() ?? schiff.Geleit;
    }

    /// <summary>
    /// Zum Jahresende: ein Schiff kaufen, wenn das Haus weniger als <c>ki.schiffe.hoechstens</c> hat und sich eines in höchstens
    /// <see cref="SchiffHoechstensJahre"/> Jahren bezahlt macht – gerechnet mit dem besten Auftrag zu heutigen Preisen
    /// (<see cref="Fahrtrechnung.GewinnJeJahr"/> × <c>auslastung</c> − Unterhalt). Danach bleibt <c>reserveFaktor</c> × Reserve als Bargeld;
    /// nicht in Not und nicht, solange die Vorsichtige auf den Stand spart.
    /// </summary>
    private void SchiffKaufen()
    {
        var s = _w.Schiffahrt;
        if (!s.IstAktiv || _w.Daten.Ki?.Schiffe is not { } d || SchiffHoechstensJahre is not { } grenze) return;
        if (InNot || (Spart && Profil.Gaunerei <= 0) || s.SchiffeVon(Konto).Count() >= d.Hoechstens) return;
        var (name, typ) = s.Daten!.Schiffe.First();
        var budget = Konto.Vermoegen - typ.Preis - d.ReserveFaktor * Profil.Reserve;
        if (budget <= 0) return;
        if (Kalkulation.BesteSchiffsfahrt(_w, Konto, typ, budget, "Keine", Vorsicht) is not { } v) return;
        var jahresgewinn = v.Rechnung.GewinnJeJahr * d.Auslastung - typ.Unterhalt;
        if (jahresgewinn <= 0 || typ.Preis / jahresgewinn > grenze) return;
        var waehrung = _w.Daten.Allgemein.Waehrung;
        Befehlen(new Wirtschaft.SchiffKaufen(Typ: name),
            $"kauft ein {name} ({Zahlenformat.Ganz(typ.Preis)} {waehrung}) – erwartet ≈ {Zahlenformat.Ganz(jahresgewinn)} {waehrung} im Jahr " +
            $"(Fahrten nach {v.Ziel}), bezahlt in {Zahlenformat.Kommazahl(typ.Preis / jahresgewinn)} Jahren.");
    }
}
