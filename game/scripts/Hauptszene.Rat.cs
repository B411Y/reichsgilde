using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Stadtkasse, Anträge und Ämter in der Hauptszene (PR #200) – ein Teil der <c>partial class Hauptszene</c>: die Startoptionen für Klicktests
/// und welche Meldungen zu Rat und Ämtern unten links erscheinen.
///
/// Meldungen (wie PR #198): Beschlüsse der Ratssitzung, fertige Bauten der Stadt und das Fest betreffen die ganze Stadt und stehen unten links.
/// Anträge und Amtshandlungen anderer Häuser („Haus Brendel: beantragt ein Fest …“, „schickt als Stadthauptmann die Streife …“) und abgelehnte
/// fremde Anträge zu Steuer, Bau und Fest stehen nur im Protokoll des Jahres – außer, sie nennen das eigene Haus.
/// </summary>
public partial class Hauptszene
{
    /// <summary>Was die KI-Häuser mit Antrag oder Amt tun (der Text nach „Haus …: “, siehe <c>KiRivale.Stadtkasse.cs</c>).</summary>
    private static readonly string[] KiRatszuege =
    {
        "beantragt als Kämmerer", "beantragt eine Umsatzsteuer", "beantragt ein Fest", "beantragt ein Bauvorhaben",
        "setzt als Stadtrichter", "schickt als Stadthauptmann", "lässt als Gefängniswärter",
    };

    /// <summary>Die Beschreibungen der Anträge zu Steuer, Bau und Fest im Protokoll der Sitzung (<see cref="Ratsantraege.Beschreibung"/>).</summary>
    private static readonly string[] KassenAntragsTexte = { " – Steuersatz ", " – Bauvorhaben „", " – Fest im Jahr " };

    /// <summary>Eine Meldung zu Antrag oder Amt eines anderen Hauses, die nur ins Protokoll gehört (siehe oben)?</summary>
    private bool IstFremdeRatsmeldung(string meldung)
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null || !w.Kasse.IstAktiv) return false;
        if (meldung.Contains(_spieler.Inhaber)) return false; // betrifft das eigene Haus
        if (meldung.StartsWith("Der Gefängniswärter ")) return true; // fremde Freilassung
        if (meldung.StartsWith("Ratssitzung ") && meldung.Contains(" abgelehnt (") && KassenAntragsTexte.Any(meldung.Contains)) return true;
        foreach (var k in w.Konten)
            if (k != _spieler && meldung.StartsWith(k.Inhaber + ": ") && KiRatszuege.Any(meldung[(k.Inhaber.Length + 2)..].StartsWith))
                return true;
        return false;
    }

    /// <summary>
    /// Startoptionen für Klicktests (vor <c>--rat=1</c> ausgewertet):
    /// <c>--amt=&lt;Amt&gt;</c> setzt den Spieler in dieses Amt (wie nach einer Wahl; Leerzeichen als _), <c>--kasse=&lt;Gulden&gt;</c> setzt den Stand der
    /// Stadtkasse, <c>--antrag=steuer:&lt;Prozent&gt;|bau:&lt;Kennung&gt;|fest</c> stellt einen Antrag (mehrere mit Komma), <c>--rathaus=&lt;reiter&gt;</c>
    /// wählt das Rathaus mit diesem Reiter (stadtkasse, rat, amt).
    /// </summary>
    private void RatStartoptionen(Dictionary<string, string> optionen)
    {
        if (_simulation.Wirtschaft is not { } w || _spieler is null) return;
        if (optionen.TryGetValue("amt", out var amtName))
        {
            amtName = amtName.Replace('_', ' ');
            if (w.Rat.AmtNamens(amtName) is not { } amt)
                Protokollieren($"--amt={amtName}: Dieses Amt gibt es nicht ({string.Join(", ", w.Rat.Aemter.Select(a => a.Name))}).");
            else
                AmtFuerKlicktest(w, amt);
        }
        if (optionen.TryGetValue("kasse", out var kasse) && double.TryParse(kasse, System.Globalization.CultureInfo.InvariantCulture, out var betrag))
        {
            if (!w.Kasse.IstAktiv) Protokollieren("--kasse: In dieser Stadt gibt es keine Stadtkasse.");
            else typeof(Stadtkasse).GetProperty(nameof(Stadtkasse.Stand))!.SetValue(w.Kasse, betrag); // nur zum Testen: der Kern hat dafür keinen Befehl
        }
        if (optionen.TryGetValue("antrag", out var antraege))
            foreach (var antrag in antraege.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var teile = antrag.Split(':', 2);
                Befehl? befehl = teile[0] switch
                {
                    "steuer" when teile.Length == 2 && int.TryParse(teile[1], out var prozent) => new SteuersatzBeantragen(prozent),
                    "bau" when teile.Length == 2 => new BauvorhabenBeantragen(teile[1]),
                    "fest" => new FestBeantragen(),
                    _ => null,
                };
                if (befehl is null)
                {
                    Protokollieren($"--antrag={antrag}: gemeint ist steuer:<Prozent>, bau:<Kennung> oder fest.");
                    continue;
                }
                _simulation.Bote.Einreihen(_spieler, befehl, ergebnis =>
                    Protokollieren(ergebnis.Erfolg ? ergebnis.Meldung : $"✗ --antrag={antrag}: {ergebnis.Meldung}"));
            }
        if (optionen.TryGetValue("rathaus", out var reiterName))
        {
            if (Enum.TryParse<RathausTafel.Reiter>(reiterName, true, out var reiter))
            {
                _auswahlKennung = "Rathaus";
                _rathausTafel.ReiterWaehlen(reiter);
            }
            else Protokollieren($"--rathaus={reiterName}: gemeint ist stadtkasse, rat oder amt.");
        }
    }

    /// <summary>
    /// Klicktest: Der Spieler (oder <paramref name="haus"/>) hält dieses Amt, als hätte er die Wahl gewonnen (ein bisheriges Amt wird frei, der Ratsherr rückt ab). Der Kern hat
    /// dafür keinen Befehl – ein Amt gibt es nur durch eine Wahl –, darum setzt die Startoption die Inhaber über Reflection.
    /// </summary>
    private void AmtFuerKlicktest(Stadtwirtschaft w, Amt amt, Konto? haus = null)
    {
        haus ??= _spieler!;
        var konto = typeof(Amt).GetProperty(nameof(Amt.Konto))!;
        var ratsherr = typeof(Amt).GetProperty(nameof(Amt.Ratsherr))!;
        foreach (var alt in w.Rat.Aemter.Where(a => a.Konto == haus)) konto.SetValue(alt, null);
        ratsherr.SetValue(amt, null);
        konto.SetValue(amt, haus);
        Protokollieren($"Klicktest: {haus.Inhaber} ist {amt.Name}.");
    }
}
