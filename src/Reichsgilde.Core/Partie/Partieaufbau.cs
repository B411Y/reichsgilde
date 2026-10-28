using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Partie;

/// <summary>Was der Spieler im Menü „Neue Partie" einstellt – zusätzlich zu den <see cref="Partieeinstellungen"/>.</summary>
/// <param name="Familienname">Name des eigenen Hauses ohne Vorsatz („Hammerschmid“) – das Spiel nennt es „Haus Hammerschmid“.
/// „Familie …“ oder „Haus …“ davor wird abgeschnitten.</param>
/// <param name="Startvermoegen">Gulden zu Beginn; null = Wert aus wirtschaft.json.</param>
/// <param name="Rivale">Charakter des KI-Rivalen.</param>
/// <param name="Volk">Volk des eigenen Hauses.</param>
/// <param name="Geschlecht">Patriarch oder Matriarchin.</param>
/// <param name="Vorname">Vorname des Oberhaupts; null = ein zufälliger Name des Volkes.</param>
/// <param name="Beruf">Startberuf: bestimmt die erste Werkstatt (Schmied → Gießerei, Tischler → Tischlerei, Steinmetz → Maurerei).</param>
/// <param name="Rivalen">Wie viele KI-Häuser mitspielen: 1 = Haus Eisenbart (Schmied), 2 = dazu Haus Eichwald (Tischler),
/// 3 = dazu Haus Kieselbach (Steinmetz).</param>
/// <param name="Glaube">Konfession des eigenen Hauses; null = Startglaube des Volkes.</param>
/// <param name="Wappen">Wappen des eigenen Hauses; null = die erste Vorlage. Darf keinem KI-Haus gehören.</param>
/// <param name="Aussehen">Gesicht, Haar und Augen des Oberhaupts im Porträt; null = gewürfelt. Muss zum Volk passen
/// (<see cref="Aussehenspalette.Verboten"/>).</param>
public sealed record NeuePartie(
    string Familienname = "Hammerschmid",
    double? Startvermoegen = null,
    KiCharakter Rivale = KiCharakter.Ausgewogen,
    Volk Volk = Volk.Mensch,
    Geschlecht Geschlecht = Geschlecht.Mann,
    string? Vorname = null,
    string Beruf = "Schmied",
    int Rivalen = 3,
    Konfession? Glaube = null,
    Wappen? Wappen = null,
    Aussehen? Aussehen = null);

/// <summary>Wie der KI-Rivale spielt – im Menü als Schwierigkeit wählbar.</summary>
public enum KiCharakter
{
    /// <summary>Viel Reserve, fürchtet Raub, kauft nur, was sich schnell bezahlt macht.</summary>
    Vorsichtig,
    Ausgewogen,
    /// <summary>Wenig Reserve, nimmt Risiken, expandiert früh.</summary>
    Wagemutig,
}

/// <summary>
/// Stellt eine neue Partie auf: das Haus des Spielers mit der Werkstatt seines Startberufs und – je nach Zahl der Rivalen – die
/// KI-Häuser aus dem Abschnitt „start“ der Karte (<see cref="Startbelegung"/>), alle von der KI geführt. Ohne Karte gilt
/// <see cref="Startbelegung.Ersatz"/>: Haus Eisenbart (Zwerge, Schmiede: Gießerei und Grube), Haus Eichwald (Elben, Tischler:
/// Tischlerei und Holzschlag) und Haus Kieselbach (Halblinge, Steinmetze: Maurerei und Steinbruch). Alle Oberhäupter beginnen ledig.
/// </summary>
public static class Partieaufbau
{
    /// <summary>Der erste KI-Rivale im Ersatz ohne Karte (die Greybox-Karte führt dieselben drei Häuser).</summary>
    public const string Rivalenname = "Haus Eisenbart";

    /// <summary>Der zweite KI-Rivale: ein Elbenhaus von Tischlern.</summary>
    public const string ZweiterRivale = "Haus Eichwald";
    public const Volk ZweiterRivaleVolk = Volk.Elb;
    public const Volk RivalenVolk = Volk.Zwerg;

    /// <summary>Der dritte KI-Rivale: ein Halblingshaus von Steinmetzen.</summary>
    public const string DritterRivale = "Haus Kieselbach";
    public const Volk DritterRivaleVolk = Volk.Halbling;

    /// <summary>Alle Namen, die schon ein Rivale trägt – im Ersatz ohne Karte (mit Karte: <see cref="Startbelegung.Rivalennamen"/>).</summary>
    public static readonly IReadOnlyList<string> Rivalennamen = new[] { Rivalenname, ZweiterRivale, DritterRivale };

    /// <summary>„Hammerschmid“, „Familie Hammerschmid“ und „Haus Hammerschmid“ → „Haus Hammerschmid“ – alle Häuser heißen gleich gebaut.</summary>
    public static string Hausname(string eingabe)
    {
        var nachname = Familie.NachnameAus((eingabe ?? "").Trim());
        return "Haus " + (nachname == "" ? "Hammerschmid" : nachname);
    }

    public static KiProfil ProfilFuer(KiCharakter charakter) => charakter switch
    {
        KiCharakter.Vorsichtig => KiProfil.Standard with { Reserve = 2500, Vorsicht = 2, MaxAmortisationJahre = 4, Gaunerei = 0 },
        KiCharakter.Wagemutig => KiProfil.Standard with { Reserve = 800, Vorsicht = 0.7, MaxAmortisationJahre = 8, Gaunerei = 2 },
        _ => KiProfil.Standard,
    };

    /// <returns>Die Simulation und das Konto des Spielers.</returns>
    /// <param name="startzeit">Abweichender Startzeitpunkt (zum Testen); ohne Angabe Frühling 1400, 04:00.</param>
    /// <param name="karte">Die Karte der Stadt (game/karten/…): Belegung am Start (<see cref="Stadtkarte.Start"/>), Wohnhausplätze,
    /// Bauzonen. Ohne Karte gilt <see cref="Startbelegung.Ersatz"/>, und es wird nicht gebaut.</param>
    public static (Simulation Simulation, Konto Spieler) Erstellen(Partieeinstellungen einstellungen, Wirtschaftsdaten daten, NeuePartie neu,
        Spielzeit? startzeit = null, Stadtkarte? karte = null)
    {
        var start = karte?.Startbelegung ?? Startbelegung.Ersatz;
        var name = Hausname(neu.Familienname);
        if (start.Rivalennamen.Contains(name))
            throw new ArgumentException($"„{name}“ ist schon der Name eines Rivalen.");
        var wappen = neu.Wappen ?? Wappen.Vorlagen[0];
        if (wappen.Fehler() is { } wappenFehler)
            throw new ArgumentException(wappenFehler);
        if (start.Rivalenwappen.Contains(wappen))
            throw new ArgumentException("Dieses Wappen führt schon ein KI-Haus.");
        if (neu.Rivalen < 1 || neu.Rivalen > start.Haeuser.Count)
            throw new ArgumentException(start.Haeuser.Count == 1 ? "Hier spielt genau ein KI-Haus mit."
                : $"Es spielen ein bis {Zahlwort(start.Haeuser.Count)} KI-Häuser mit.");

        var simulation = new Simulation(einstellungen, startzeit, daten);
        var w = simulation.Wirtschaft!;
        if (karte is not null) w.KarteSetzen(karte);

        if (neu.Volk is Volk.Ork or Volk.Oger)
            throw new ArgumentException("Orks und Oger sind nicht spielbar.");
        if (neu.Aussehen is { } aussehen && Aussehenspalette.Verboten(neu.Volk, aussehen) is { } unpassend)
            throw new ArgumentException(unpassend);
        var startArt = daten.Berufe.TryGetValue(neu.Beruf, out var startberuf) ? startberuf.Werkstatt
            : neu.Beruf == "Schmied" ? Betriebsart.Giesserei // ältere Daten ohne „berufe"
            : throw new ArgumentException($"Den Beruf „{neu.Beruf}“ gibt es nicht.");
        var spieler = w.KontoEroeffnen(name, startvermoegen: neu.Startvermoegen);
        spieler.Wappen = wappen;
        var werkstatt = w.BetriebEroeffnen(start.Spieler.WerkstattFuer(neu.Beruf).NameFuer(daten.Art(startArt).Name), startArt, spieler);

        // Orks nur, wenn das Startstatut sie im Umland erlaubt (Statut B oder C) – sonst Bergleute (bzw. die Kraft des Umlandbetriebs).
        var orks = daten.Arbeitskraefte.TryGetValue("Ork", out var ork) && ork.IstErlaubt(w.Statut, Arbeitsort.Umland);

        // Die KI-Häuser in der Reihenfolge der Karte; das erste spielt immer mit. Sein Konto entsteht noch vor der Familie des Spielers
        // (so war die Reihenfolge schon vor der Karte – die Zufallszahlen der Familien bleiben dieselben).
        var haeuser = start.Haeuser.Take(neu.Rivalen).Where(h => h.Betriebe.All(b => daten.KenntArt(b.Art))).ToList();
        var konten = new Dictionary<Starthaus, Konto>();
        Konto KontoVon(Starthaus h)
        {
            if (konten.TryGetValue(h, out var k)) return k;
            k = konten[h] = w.KontoEroeffnen(h.Name);
            k.Wappen = h.Wappen;
            return k;
        }
        if (haeuser.Count > 0) KontoVon(haeuser[0]);
        if (w.Familien.IstAktiv)
            w.Familien.Gruenden(spieler, neu.Volk, neu.Geschlecht, neu.Vorname, glaube: neu.Glaube, aussehen: neu.Aussehen);
        // Erst nach der Familie, damit die Arbeiter das Volk des Hauses kennen (Einstellen würfelt nicht mit dem Zufall der Partie).
        // Mit „personal“ (PR #224) beginnt jedes Haus mit zwei Anfängern (Lehrlingen), sonst mit zwei Gesellen wie früher.
        w.Einstellen(werkstatt, w.Personal.AnfaengerFuer(startArt, "Geselle"), 2);
        foreach (var h in haeuser)
            HausAufstellen(w, daten, karte, h, KontoVon(h), orks ? "Ork" : null, neu.Rivale, einstellungen.Schwierigkeit);
        w.EinwohnerZaehlen(); // die Nachfrage der Stadt nach ihren Häusern – schon vor der ersten Minute
        return (simulation, spieler);
    }

    private static string Zahlwort(int n) => n switch { 2 => "zwei", 3 => "drei", 4 => "vier", 5 => "fünf", 6 => "sechs", 7 => "sieben", _ => n.ToString() };

    /// <summary>
    /// Ein KI-Haus mit seinen Startbetrieben, ab jetzt von der KI geführt. Der Name eines Betriebs steht am Platz der Karte, ohne Karte im Ersatz.
    /// <see cref="Schwierigkeit.Normal"/>: nur die erste Werkstatt in der Stadt („Gießerei Eisenbart“ mit zwei Anfängern) – wie der Spieler;
    /// die übrigen Startbetriebe stehen frei zur Pacht bzw. zum Kauf (<see cref="Stadtwirtschaft.StartbetriebFreigeben"/>).
    /// <see cref="Schwierigkeit.Schwer"/>: alle Startbetriebe, dazu „Grube Eisenbart“ mit drei Umlandkräften.
    /// </summary>
    private static void HausAufstellen(Stadtwirtschaft w, Wirtschaftsdaten daten, Stadtkarte? karte, Starthaus h, Konto konto, string? umlandKraft,
        KiCharakter charakter, Schwierigkeit schwierigkeit)
    {
        if (w.Familien.IstAktiv)
            w.Familien.Gruenden(konto, h.Volk, h.Geschlecht);
        // Bei „Normal“ nur die erste Werkstatt (hat ein Haus keine in der Stadt: seinen ersten Betrieb).
        var erste = h.Betriebe.FirstOrDefault(b => daten.Art(b.Art).InDerStadt) ?? h.Betriebe.FirstOrDefault();
        foreach (var b in h.Betriebe)
        {
            var art = daten.Art(b.Art);
            var name = karte?.GebaeudeMit(b.Platz)?.Betrieb ?? (b.ErsatzName ?? "{art} " + Familie.NachnameAus(h.Name)).Replace("{art}", art.Name);
            if (schwierigkeit == Schwierigkeit.Normal && b != erste)
            {
                w.StartbetriebFreigeben(name, b.Art);
                continue;
            }
            var betrieb = w.BetriebEroeffnen(name, b.Art, konto);
            if (b.Produkt is { } produkt) betrieb.Produkt = produkt;
            if (art.InDerStadt)
                w.Einstellen(betrieb, w.Personal.AnfaengerFuer(b.Art, "Geselle"), 2);
            else
                w.Einstellen(betrieb, umlandKraft ?? w.Personal.AnfaengerFuer(b.Art, "Altknecht"), 3);
        }
        w.KiAnmelden(konto, ProfilFuer(charakter));
    }
}
