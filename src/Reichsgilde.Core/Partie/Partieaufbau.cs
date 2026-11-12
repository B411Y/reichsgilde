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
/// <param name="Hofstelle">Startplatz des Spielers auf einer Karte mit Hofstellen: Kennung einer Start-Hofstelle (<see cref="Hofstellenvergabe.Angebot"/>);
/// null = gelost (Knopf „Zufall“). Ohne Hofstellen ohne Wirkung.</param>
/// <param name="Startplaetze">Gelost (wie im Spiel: die KI-Häuser bekommen geloste Hofstellen) oder Vorgabe (jedes Haus, wo die Startbelegung
/// der Karte es hinsetzt – für Tests und Vergleiche).</param>
/// <param name="Aufstellung">Gelost (wie im Spiel: Beruf, Volk und Glaube der KI-Häuser gelost, jeder Beruf einmal, <see cref="Startaufstellung"/>) oder
/// Vorgabe (jedes KI-Haus wie in der Startbelegung der Karte – nur für Tests und Vergleiche).</param>
/// <param name="KiStaerke">Stärke der KI-Gegner (11.11.2026): Leicht, Normal (wie bisher), Schwer – Startgeld und Profil (<see cref="Partiebesetzung.Profil"/>).</param>
/// <param name="KiVorgaben">„Erweitert“ (11.11.2026): je KI-Haus in Kontoreihenfolge, was festgelegt ist (Name, Wappen, Volk, Glaube, Beruf);
/// null oder ein null-Eintrag = alles gewürfelt. Nur mit <see cref="Aufstellungsart.Gelost"/>.</param>
/// <param name="ZugWurf">Die Persönlichkeitszüge des Spielers aus dem Startbildschirm: Wurf Nummer n (<see cref="Partiebesetzung.SpielerZuege"/>,
/// Befehl <see cref="ZuegeNeuWuerfeln"/>); null = gewürfelt wie bei jedem Erwachsenen (wie vor dem 11.11.2026).</param>
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
    Aussehen? Aussehen = null,
    string? Hofstelle = null,
    Startplatzvergabe Startplaetze = Startplatzvergabe.Gelost,
    Aufstellungsart Aufstellung = Aufstellungsart.Gelost,
    KiStaerke KiStaerke = KiStaerke.Normal,
    IReadOnlyList<Hausvorgabe?>? KiVorgaben = null,
    int? ZugWurf = null);

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
/// Im Spiel (<see cref="Aufstellungsart.Gelost"/>) lost die <see cref="Startaufstellung"/> Beruf, Volk und Glauben jedes KI-Hauses neu – jeder Beruf
/// höchstens einmal je Partie; Name, Wappen, Farbe, Geschlecht und Plätze bleiben die der Karte.
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
        // KI-Gegner (11.11.2026): 0 bis Höfe − 1; ohne Los nur die Häuser der Startbelegung
        var hoechstens = Partiebesetzung.HoechstensKi(karte, neu.Aufstellung);
        if (neu.Rivalen < 0 || neu.Rivalen > hoechstens)
            throw new ArgumentException(hoechstens == 0 ? "Hier spielt kein KI-Haus mit."
                : hoechstens == 1 ? "Hier spielt höchstens ein KI-Haus mit."
                : $"Es spielen null bis {Zahlwort(hoechstens)} KI-Häuser mit.");
        if (neu.KiVorgaben is { Count: > 0 } && neu.Aufstellung != Aufstellungsart.Gelost)
            throw new ArgumentException("KI-Häuser einzeln festlegen geht nur mit gewürfelter Aufstellung.");
        if (neu.ZugWurf is < 0) throw new ArgumentException("Der Wurf der Züge zählt ab 0.");

        // Startaufstellung (11.11.2026): Beruf, Volk und Glaube der KI-Häuser gelost, jeder Beruf höchstens einmal je Partie – vor den Hofstellen,
        // denn die Werkstatt eines Hauses zieht mit ihrem Namen auf seine Hofstelle.
        var rivalen = neu.Rivalen;
        IReadOnlyList<(string Name, Betriebsart Art)> freiesUmland = Array.Empty<(string, Betriebsart)>();
        string? aufstellungsmeldung = null;
        if (neu.Aufstellung == Aufstellungsart.Gelost)
        {
            var spielerglaube = neu.Glaube ?? daten.Glaube?.Startglaube(neu.Volk) ?? Konfession.Katholisch;
            var mitspielend = Partiebesetzung.KiHaeuser(karte, start, daten, rivalen, wappen, neu.KiVorgaben, einstellungen.Zufallsstartwert);
            if (neu.KiVorgaben is { } vorgaben) Partiebesetzung.VorgabenPruefen(daten, vorgaben, mitspielend, name, wappen, neu.Beruf);
            var a = Startaufstellung.Losen(karte, start, daten, neu.Beruf, spielerglaube, mitspielend, einstellungen.Zufallsstartwert, neu.KiVorgaben, name);
            (start, karte, freiesUmland, aufstellungsmeldung) = (a.Belegung, a.Karte ?? karte, a.Frei, a.Meldung);
            rivalen = start.Haeuser.Count;
        }

        // Hofstellen (09.11.2026): Die Partie spielt auf der Partiekarte – die Startbelegung auf den gewählten bzw. gelosten Hofstellen.
        if (karte is { HatHofstellen: true })
        {
            karte = Hofstellenvergabe.Belegen(karte, daten, neu, einstellungen.Zufallsstartwert, Mitspielend(start, daten, rivalen));
            start = karte.Startbelegung;
        }

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
        var haeuser = Mitspielend(start, daten, rivalen);
        var konten = new Dictionary<Starthaus, Konto>();
        Konto KontoVon(Starthaus h)
        {
            if (konten.TryGetValue(h, out var k)) return k;
            k = konten[h] = w.KontoEroeffnen(h.Name, startvermoegen: Partiebesetzung.Startgeld(daten, neu.KiStaerke));
            k.Wappen = h.Wappen;
            return k;
        }
        if (haeuser.Count > 0) KontoVon(haeuser[0]);
        if (w.Familien.IstAktiv)
            w.Familien.Gruenden(spieler, neu.Volk, neu.Geschlecht, neu.Vorname, glaube: neu.Glaube, aussehen: neu.Aussehen,
                zuege: neu.ZugWurf is { } zugWurf ? Partiebesetzung.SpielerZuege(daten, einstellungen.Zufallsstartwert, neu.Volk, zugWurf) : null);
        // Erst nach der Familie, damit die Arbeiter das Volk des Hauses kennen (Einstellen würfelt nicht mit dem Zufall der Partie).
        // Mit „personal“ (PR #224) beginnt jedes Haus mit Anfängern (Lehrlingen), sonst mit zwei Gesellen wie früher; wie viele, sagt
        // personal.startJeBetrieb (ab Datenversion 72: einer, für Spieler und KI-Häuser gleich).
        w.Einstellen(werkstatt, w.Personal.AnfaengerFuer(startArt, "Geselle"), w.Personal.StartAnzahl(startArt, 2));
        foreach (var h in haeuser)
            HausAufstellen(w, daten, karte, h, KontoVon(h), orks ? "Ork" : null, neu.Rivale, einstellungen.Schwierigkeit, neu.KiStaerke);
        // Umland-Startplätze, die bei der Aufstellung niemand bekommen hat, stehen frei zur Pacht (wie bei „Normal“ der Umlandbetrieb eines KI-Hauses)
        foreach (var (frei, art) in freiesUmland)
            w.StartbetriebFreigeben(frei, art);
        if (aufstellungsmeldung is not null) w.Melden(aufstellungsmeldung);
        w.Bauen.FreieHofstellenFreigeben(); // was niemand bekommen hat, ist ab jetzt Bauplatz (nur mit Hofstellen)
        w.EinwohnerZaehlen(); // die Nachfrage der Stadt nach ihren Häusern – schon vor der ersten Minute
        return (simulation, spieler);
    }

    /// <summary>Die KI-Häuser, die mitspielen: die ersten <paramref name="rivalen"/> der Startbelegung, deren Betriebsarten die Daten kennen.</summary>
    private static List<Starthaus> Mitspielend(Startbelegung start, Wirtschaftsdaten daten, int rivalen) =>
        start.Haeuser.Take(rivalen).Where(h => h.Betriebe.All(b => daten.KenntArt(b.Art))).ToList();

    private static string Zahlwort(int n) => n switch { 2 => "zwei", 3 => "drei", 4 => "vier", 5 => "fünf", 6 => "sechs", 7 => "sieben", 8 => "acht", _ => n.ToString() };

    /// <summary>
    /// Ein KI-Haus mit seinen Startbetrieben, ab jetzt von der KI geführt. Der Name eines Betriebs steht am Platz der Karte, ohne Karte im Ersatz.
    /// <see cref="Schwierigkeit.Normal"/>: nur die erste Werkstatt in der Stadt („Gießerei Eisenbart“ mit einem Lehrling) – wie der Spieler;
    /// die übrigen Startbetriebe stehen frei zur Pacht bzw. zum Kauf (<see cref="Stadtwirtschaft.StartbetriebFreigeben"/>).
    /// <see cref="Schwierigkeit.Schwer"/>: alle Startbetriebe, dazu „Grube Eisenbart“ mit Umlandkräften (ab Datenversion 72 auch dort eine,
    /// vorher drei). Wie viele Leute jeder Startbetrieb hat, sagt <see cref="PersonalDaten.StartJeBetrieb"/>.
    /// </summary>
    private static void HausAufstellen(Stadtwirtschaft w, Wirtschaftsdaten daten, Stadtkarte? karte, Starthaus h, Konto konto, string? umlandKraft,
        KiCharakter charakter, Schwierigkeit schwierigkeit, KiStaerke staerke)
    {
        if (w.Familien.IstAktiv)
            w.Familien.Gruenden(konto, h.Volk, h.Geschlecht, glaube: h.Glaube);
        var startbetriebe = h.Betriebe;
        // Bei „Normal“ nur die erste Werkstatt (hat ein Haus keine in der Stadt: seinen ersten Betrieb).
        var erste = startbetriebe.FirstOrDefault(b => daten.Art(b.Art).InDerStadt) ?? startbetriebe.FirstOrDefault();
        foreach (var b in startbetriebe)
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
                w.Einstellen(betrieb, w.Personal.AnfaengerFuer(b.Art, "Geselle"), w.Personal.StartAnzahl(b.Art, 2));
            else
                w.Einstellen(betrieb, umlandKraft ?? w.Personal.AnfaengerFuer(b.Art, "Altknecht"), w.Personal.StartAnzahl(b.Art, 3));
        }
        w.KiAnmelden(konto, Partiebesetzung.Profil(ProfilFuer(charakter), daten, staerke));
    }
}
