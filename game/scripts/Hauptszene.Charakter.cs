using Godot;

namespace Reichsgilde;

/// <summary>Startoption <c>--charakter=</c> der Hauptszene (PR #227), ein Teil der <c>partial class Hauptszene</c>.</summary>
public partial class Hauptszene
{
    /// <summary>
    /// <c>oberhaupt</c>, <c>ehepartner</c>, <c>kind</c> (das erste des eigenen Hauses, sonst irgendeines), <c>ratsherr</c> (der erste Ratsherr mit Amt), <c>ratsherr:&lt;n&gt;</c> (der n-te Ratsherr, 1 = erster)
    /// oder eine Personennummer: öffnet das Charakterfenster dieser Person. Mit dem Zusatz <c>:lebensweg</c> (z. B. <c>oberhaupt:lebensweg</c>) gleich im Reiter „Lebensweg“.
    /// </summary>
    private void CharakterStartoption(string wert)
    {
        if (_simulation.Wirtschaft is not { } w) return;
        var lebensweg = wert.EndsWith(":lebensweg");
        if (lebensweg) wert = wert[..^":lebensweg".Length];
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
            "rivale" => Rivale(w), // das Oberhaupt des ersten anderen Hauses (Geheimnisse, Teil 3c)
            "kind" => familie?.Kinder.FirstOrDefault()
                      ?? Enumerable.Range(1, 400).Select(w.Familien.PersonNummer).FirstOrDefault(x => x is { Lebt: true, Vater: not null } && !w.Familien.IstVolljaehrig(x)), // sonst das erste Kind irgendeines Hauses
            _ => int.TryParse(wert, out var nr) ? w.Familien.PersonNummer(nr) : null,
        };
        if (person is null) Protokollieren($"--charakter={wert}: Diese Person gibt es nicht (oberhaupt, ehepartner, kind, ratsherr[:n] oder eine Nummer).");
        else if (lebensweg) _charakter.LebensweiseOeffnen(person);
        else _charakter.Oeffnen(person);
    }

    /// <summary>Startoption <c>--stress=</c>, wartet wie <c>--charakter=</c> bis nach dem Vorspulen.</summary>
    private string? _stressWartet;

    /// <summary>
    /// <c>--stress=&lt;Wert&gt;[:oberhaupt|ehepartner|Personennummer]</c> (Charaktersystem Teil 3b): setzt den Stress dieser Person des eigenen Hauses
    /// (ohne Angabe das Oberhaupt) über den Entwicklerbefehl <see cref="Core.Entwickler.EntwicklerStress"/> – darum nur mit <c>--entwickler=1</c>;
    /// 100 löst den Zusammenbruch aus. Für den Klicktest des Stressbalkens, z. B. <c>--stress=75 --charakter=oberhaupt</c>.
    /// </summary>
    private void StressStartoption(string wert)
    {
        if (_simulation.Wirtschaft is not { } w || _spieler is null) return;
        var teile = wert.Split(':');
        var familie = w.Familien.Von(_spieler);
        var person = teile.Length < 2 || teile[1] == "oberhaupt" ? familie?.Oberhaupt
            : teile[1] == "ehepartner" ? familie?.Ehepartner
            : int.TryParse(teile[1], out var nr) ? w.Familien.PersonNummer(nr) : null;
        if (person is null || !double.TryParse(teile[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var stress))
        {
            Protokollieren($"--stress={wert}: erwartet <Wert>[:oberhaupt|ehepartner|Personennummer].");
            return;
        }
        Protokollieren("--stress: " + UiHilfe.MeldungText(_simulation.Bote.SofortAusfuehren(_spieler, new Core.Entwickler.EntwicklerStress(person.Nummer, stress))));
    }

    /// <summary>Das Oberhaupt des ersten anderen Hauses, das noch spielt (für <c>rivale</c>).</summary>
    private Core.Dynastie.Person? Rivale(Core.Wirtschaft.Stadtwirtschaft w) =>
        w.Konten.Where(k => k != _spieler && k.IstAktiv).Select(w.Familien.Von).OfType<Core.Dynastie.Familie>().FirstOrDefault()?.Oberhaupt;

    /// <summary>Startoption <c>--geheimnis=</c>, wartet wie <c>--stress=</c> bis nach dem Vorspulen.</summary>
    private string? _geheimnisWartet;

    /// <summary>
    /// <c>--geheimnis=&lt;Ehebruch|Ketzerei|Schulden&gt;[:oberhaupt|ehepartner|rivale|ratsherr[:n]|Personennummer]</c> (Charaktersystem Teil 3c): Die Person
    /// bekommt ein Geheimnis dieser Art, und Euer Haus weiß davon – über den Entwicklerbefehl <see cref="Core.Entwickler.EntwicklerGeheimnis"/>, darum
    /// nur mit <c>--entwickler=1</c>. Ohne Ziel: <c>rivale</c>. Für den Klicktest der Knöpfe im Charakterfenster, z. B.
    /// <c>--entwickler=1 --geheimnis=Ehebruch:rivale --charakter=rivale</c> oder <c>--geheimnis=Ketzerei:ratsherr --charakter=ratsherr</c>.
    /// </summary>
    private void GeheimnisStartoption(string wert)
    {
        if (_simulation.Wirtschaft is not { } w || _spieler is null) return;
        var teile = wert.Split(':');
        var familie = w.Familien.Von(_spieler);
        var ratsherren = w.Rat.Aemter.Select(a => a.Ratsherr).OfType<Reichsgilde.Core.Politik.Ratsherr>().ToList();
        var ziel = teile.Length > 1 ? teile[1] : "rivale";
        Core.Dynastie.ICharakter? person = ziel switch
        {
            "oberhaupt" => familie?.Oberhaupt,
            "ehepartner" => familie?.Ehepartner,
            "rivale" => Rivale(w),
            "ratsherr" => (teile.Length > 2 && int.TryParse(teile[2], out var n) ? n : 1) is var i && i >= 1 && i <= ratsherren.Count ? ratsherren[i - 1] : null,
            _ => int.TryParse(ziel, out var nr) ? w.Familien.PersonNummer(nr) : null,
        };
        if (person is null || !Enum.TryParse<Core.Geheimnisse.Geheimnisart>(teile[0], out var art))
        {
            Protokollieren($"--geheimnis={wert}: erwartet <Ehebruch|Ketzerei|Schulden>[:oberhaupt|ehepartner|rivale|ratsherr[:n]|Personennummer].");
            return;
        }
        Protokollieren("--geheimnis: " + UiHilfe.MeldungText(_simulation.Bote.SofortAusfuehren(_spieler, new Core.Entwickler.EntwicklerGeheimnis(person.Kennung, art))));
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
