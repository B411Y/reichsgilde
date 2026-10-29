using Godot;

namespace Reichsgilde;

/// <summary>Startoption <c>--charakter=</c> der Hauptszene (PR #227), ein Teil der <c>partial class Hauptszene</c>.</summary>
public partial class Hauptszene
{
    /// <summary>
    /// <c>oberhaupt</c>, <c>ehepartner</c>, <c>kind</c> (das erste des eigenen Hauses, sonst irgendeines), <c>ratsherr</c> (der erste Ratsherr mit Amt), <c>ratsherr:&lt;n&gt;</c> (der n-te Ratsherr, 1 = erster)
    /// oder eine Personennummer: öffnet das Charakterfenster dieser Person.
    /// </summary>
    private void CharakterStartoption(string wert)
    {
        if (_simulation.Wirtschaft is not { } w) return;
        var familie = w.Familien.Von(_spieler!);
        var ratsherren = w.Rat.Aemter.Select(a => a.Ratsherr).OfType<Reichsgilde.Core.Politik.Ratsherr>().ToList();
        if (wert.StartsWith("ratsherr"))
        {
            var nummer = wert.Contains(':') && int.TryParse(wert[(wert.IndexOf(':') + 1)..], out var n) ? n : 1;
            if (nummer >= 1 && nummer <= ratsherren.Count) _charakter.Oeffnen(ratsherren[nummer - 1]);
            else Protokollieren($"--charakter={wert}: Es gibt {ratsherren.Count} Ratsherren mit Amt.");
            return;
        }
        var person = wert switch
        {
            "oberhaupt" => familie?.Oberhaupt,
            "ehepartner" => familie?.Ehepartner,
            "kind" => familie?.Kinder.FirstOrDefault()
                      ?? Enumerable.Range(1, 400).Select(w.Familien.PersonNummer).FirstOrDefault(x => x is { Lebt: true, Vater: not null } && !w.Familien.IstVolljaehrig(x)), // sonst das erste Kind irgendeines Hauses
            _ => int.TryParse(wert, out var nr) ? w.Familien.PersonNummer(nr) : null,
        };
        if (person is null) Protokollieren($"--charakter={wert}: Diese Person gibt es nicht (oberhaupt, ehepartner, kind, ratsherr[:n] oder eine Nummer).");
        else _charakter.Oeffnen(person);
    }

    /// <summary>
    /// <c>--charakter-ehe=1</c>: das ledige Oberhaupt wirbt um den Kandidaten, der am ehesten einwilligt (mit viel Geld, <c>--vermoegen=</c>),
    /// und heiratet – für Bildschirmfotos mit Familie. Seit PR #231 zählt die Meinung des Kandidaten mit; darum nicht mehr einfach der erste.
    /// </summary>
    private void CharakterVerheiraten()
    {
        if (_simulation.Wirtschaft is not { } w) return;
        var buch = w.Familien;
        if (_spieler is null || buch.Von(_spieler)?.Ehepartner is not null
            || buch.Kandidaten.Where(x => buch.WerbenVerboten(_spieler, x) is null)
                .OrderByDescending(x => buch.Bereitschaft(_spieler, x) + buch.ZuneigungRest(_spieler, x) - buch.Heiratsschwelle(x))
                .ThenBy(x => x.Person.Nummer).FirstOrDefault() is not { } k) return;
        for (var i = 0; i < 12; i++)
        {
            var werbung = _simulation.Bote.SofortAusfuehren(_spieler, new Core.Wirtschaft.Werben(k.Person.Nummer, 2000));
            var heirat = _simulation.Bote.SofortAusfuehren(_spieler, new Core.Wirtschaft.Heiraten(k.Person.Nummer));
            if (heirat.Erfolg) return;
            if (i == 0) Protokollieren($"Werben: {werbung.Meldung} Heiraten: {heirat.Meldung}");
        }
        Protokollieren("--charakter-ehe=1: Die Heirat kam nicht zustande.");
    }
}
