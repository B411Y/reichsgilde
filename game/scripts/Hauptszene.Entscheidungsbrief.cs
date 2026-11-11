using Godot;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Entscheidungen;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Der <b>Entscheidungsbrief</b> der Hauptszene (Benachrichtigungen Stufe 3, ein Teil der <c>partial class Hauptszene</c>; Brief selbst: <see cref="EntscheidungsbriefAnzeige"/>,
/// Wahl je Art: <see cref="Entscheidungseinstellungen"/>). Jedes Bild gleicht die offenen Fragen des Kerns (<c>Entscheidungswesen.OffenFuer</c>) mit dem Brief ab –
/// dadurch kommt auch nach dem Laden jede offene Frage wieder (aus dem Kernzustand, nicht aus dem Verlauf):
///   • Art auf „Automatisch“: <c>EntscheidungTreffen(nr, −1)</c> gleich beim Eintreffen, zurück bleibt ein gewöhnlicher Brief mit dem Ergebnis. Schlägt es fehl, kommt die Frage als Brief.
///   • Sonst der Entscheidungsbrief (Stapel, dringendste Frist zuerst). Ist im Einzelspiel die Verzögerung des Befehlsboten 0 und „Bei Entscheidungen anhalten“ an, steht die Zeit,
///     solange er offen ist; „Später“ legt ihn beiseite (die Zeit läuft wie vorher weiter, die Frist ebenso). Im Mehrspieler nie angehalten.
///   • Beiseitegelegte Fragen zeigt ein Feder-Siegel mit Zähler in der Siegel-Ecke; ein Klick holt den Brief zurück.
///   • Nie über der Ratssitzung, dem Spielmenü, den Einstellungen, der Hilfe oder dem Jahresbericht: Dann ruht der Brief und kommt danach wieder.
///   • Läuft die Frist ab, kommt ein kurzer Brief, was an ihrer Stelle galt; der Verlauf trägt die Markierung „Entscheidung“.
/// </summary>
public partial class Hauptszene
{
    private EntscheidungsbriefAnzeige _entscheidungsbrief = null!;
    private static readonly Meldethema EntscheidungThema = new("entscheidung", "Entscheidung", "#7a3b5e");
    private readonly HashSet<int> _entscheidungSpaeter = new(), _entscheidungGesehen = new(), _entscheidungAngehalten = new(), _entscheidungAutoWartet = new(), _entscheidungAutoVersucht = new();
    private bool? _pauseVorEntscheidung;
    private Siegel? _entscheidungSiegel;

    private void EntscheidungsbriefBauen()
    {
        _entscheidungsbrief = new EntscheidungsbriefAnzeige();
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_entscheidungsbrief);
        _entscheidungsbrief.Gemeldet += Protokollieren;
        _entscheidungsbrief.Beantwortet += () => EntscheidungenAbgleichen();
        _entscheidungsbrief.Spaeter += () =>
        {
            foreach (var nr in _entscheidungSichtbar) _entscheidungSpaeter.Add(nr);
            EntscheidungenAbgleichen();
        };
        _entscheidungsbrief.Wegweiser += EntscheidungWegweiser;
    }

    /// <summary>Die Nummern der Fragen, die der Brief gerade zeigen soll (für „Später“).</summary>
    private List<int> _entscheidungSichtbar = new();

    private void EntscheidungenVerbinden(Core.Simulation simulation, Stadtwirtschaft w, Konto spieler)
    {
        _entscheidungsbrief.Verbinden(simulation, spieler);
        w.Entscheidungen.Geschlossen += (e, abschluss) =>
        {
            if (e.Haus != spieler || abschluss is Entscheidungsabschluss.Entschieden or Entscheidungsabschluss.Hinfaellig) return;
            var wahlen = w.Entscheidungen.Wahlen(e);
            var vorgabe = e.Voreinstellung >= 0 && e.Voreinstellung < wahlen.Count ? wahlen[e.Voreinstellung] : "nichts";
            var thema = EntscheidungThema;
            var brief = new Brief
            {
                Titel = "Frist verstrichen", Absender = "Euer Schreiber", Farbe = Color.FromHtml(thema.Farbe), Thema = thema.Id, Ziel = "bericht",
                Zeilen = { new Briefzeile(w.Entscheidungen.Titel(e), abschluss == Entscheidungsabschluss.Frist
                    ? $"Ihr habt nicht entschieden. Es galt die Voreinstellung: {vorgabe}."
                    : $"Zu viele Fragen waren offen; die älteste lief aus. Es galt die Voreinstellung: {vorgabe}.", false) },
            };
            BriefEinreihen(brief);
        };
    }

    /// <summary>Etwas hält den Brief zurück: Ratssitzung, Spielmenü, Einstellungen, Hilfe, Jahresbericht oder ein beendetes Spiel.</summary>
    private bool EntscheidungGesperrt() =>
        _simulation.IstBeendet || _simulation.Ratssitzung is not null || _sitzungsTafel.Laeuft || _spielmenue.Visible || _einstellungen.Tafel.Visible
        || _hilfe.Visible || _jahresbericht.Visible;

    /// <summary>Jedes Bild: offene Fragen, Automatik, Brief, Pause und Feder-Siegel abgleichen.</summary>
    private void EntscheidungenAbgleichen()
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null || !w.Entscheidungen.BekommtEntscheidungen(_spieler)) return;
        var wesen = w.Entscheidungen;
        var offen = wesen.OffenFuer(_spieler);
        var nummern = offen.Select(e => e.Nummer).ToHashSet();
        _entscheidungSpaeter.IntersectWith(nummern);
        _entscheidungGesehen.IntersectWith(nummern);
        _entscheidungAngehalten.IntersectWith(nummern);
        _entscheidungAutoWartet.IntersectWith(nummern);
        _entscheidungAutoVersucht.IntersectWith(nummern);

        // „Automatisch“: die Voreinstellung gleich beim Eintreffen (einmal je Frage)
        foreach (var e in offen.ToList())
        {
            if (_entscheidungAutoVersucht.Contains(e.Nummer) || !Entscheidungseinstellungen.Automatisch(Entscheidungseinstellungen.Schluessel(e, w.Familien))) continue;
            _entscheidungAutoVersucht.Add(e.Nummer);
            _entscheidungAutoWartet.Add(e.Nummer);
            AutomatischTreffen(e);
        }
        if (_entscheidungAutoVersucht.Count > 0) // im Einzelspiel wirkt der Befehl sofort: die beantworteten Fragen nicht mehr zeigen
        {
            offen = wesen.OffenFuer(_spieler);
            nummern = offen.Select(e => e.Nummer).ToHashSet();
            _entscheidungAutoWartet.IntersectWith(nummern);
        }

        var wartend = offen.Where(e => !_entscheidungAutoWartet.Contains(e.Nummer)).ToList();
        var sichtbar = wartend.Where(e => !_entscheidungSpaeter.Contains(e.Nummer)).ToList();
        var gesperrt = EntscheidungGesperrt();
        _entscheidungSichtbar = sichtbar.Select(e => e.Nummer).ToList();

        // Neue Fragen: der Siegelton; die Pause, sobald der Brief wirklich zu sehen ist (einmal je Frage – „Später“ und Zurückholen halten nicht wieder an)
        var neuGehoert = false;
        var nurMahnung = true; // die Mahnung des Kämmerers hat ihren eigenen Ton; kommt zugleich eine andere Frage, gilt der Entscheidungston
        foreach (var e in wartend)
            if (_entscheidungGesehen.Add(e.Nummer))
            {
                neuGehoert = true;
                if (e.Art != Entscheidungsart.Mahnung) nurMahnung = false;
            }
        if (neuGehoert) KlangOberflaeche.Spielen(nurMahnung ? KlangOberflaeche.Ereignis.Mahnung : KlangOberflaeche.Ereignis.EntscheidungSiegel);
        if (!gesperrt && Entscheidungseinstellungen.Anhalten && _simulation.Bote.Verzoegerung == 0)
        {
            var neuSichtbar = false;
            foreach (var e in sichtbar)
                if (_entscheidungAngehalten.Add(e.Nummer)) neuSichtbar = true;
            if (neuSichtbar)
            {
                _pauseVorEntscheidung ??= _simulation.Takt.IstPausiert;
                _simulation.Takt.IstPausiert = true;
            }
        }

        _entscheidungsbrief.AbstandUnten = _raumleiste.Visible ? 40 : 0;
        _entscheidungsbrief.Setzen(gesperrt ? Array.Empty<Entscheidung>() : sichtbar);

        // Nichts mehr zu entscheiden (alles beantwortet oder beiseitegelegt): die Pause wie vorher
        if (_pauseVorEntscheidung is { } vorher && sichtbar.Count == 0)
        {
            if (!vorher && _simulation.Takt.IstPausiert) _simulation.Takt.IstPausiert = false;
            _pauseVorEntscheidung = null;
        }

        FederSiegelAbgleichen(wartend, !_entscheidungsbrief.Visible);
    }

    /// <summary>Das Feder-Siegel zeigt, wie viele Fragen offen sind, solange der Brief nicht vorn liegt (beiseite gelegt oder gesperrt).</summary>
    private void FederSiegelAbgleichen(IReadOnlyList<Entscheidung> offen, bool briefZu)
    {
        if (!briefZu || offen.Count == 0)
        {
            if (_entscheidungSiegel is { } alt && IsInstanceValid(alt)) _ecke.Entfernen(alt);
            _entscheidungSiegel = null;
            return;
        }
        var wesen = _simulation.Wirtschaft!.Entscheidungen;
        var dringend = offen[0];
        var text = offen.Count == 1 ? "Offene Entscheidung: " + wesen.Titel(dringend)
            : $"{offen.Count} offene Entscheidungen, die dringendste: " + wesen.Titel(dringend);
        var posten = new Meldungsposten
        {
            Zeit = _simulation.Zeit.UhrzeitText, Jahr = _simulation.Zeit.Jahr, Text = $"{text}. Noch {EntscheidungsbriefAnzeige.Dauer(wesen.RestMinuten(dringend))} Std. bis zur Voreinstellung.",
            Original = "", Art = Ordnung.Art("Entscheidung"), Bezug = Betroffenheit.Eigen, Stufe = Meldestufe.Siegel,
        };
        if (_entscheidungSiegel is null || !IsInstanceValid(_entscheidungSiegel) || _ecke.Finden("entscheidung") is null)
        {
            _ecke.Hinzufuegen("entscheidung", EntscheidungThema, posten, _ => new Brief { Titel = "Entscheidung" }, dauerhaft: true);
            _entscheidungSiegel = _ecke.Finden("entscheidung");
            if (_entscheidungSiegel is not null) _entscheidungSiegel.Dauerhaft = true;
        }
        if (_entscheidungSiegel is null) return;
        _entscheidungSiegel.Posten[0] = posten;
        _entscheidungSiegel.Anzahl = offen.Count;
    }

    private void AutomatischTreffen(Entscheidung e)
    {
        var w = _simulation.Wirtschaft!;
        var wesen = w.Entscheidungen;
        var titel = wesen.Titel(e);
        var person = e.Person is int n ? w.Familien.PersonNummer(n) : null;
        UiHilfe.Befehlen(_simulation, _spieler, new EntscheidungTreffen(e.Nummer, Entscheidungswesen.Voreinstellungswahl), this, ergebnis =>
        {
            _entscheidungAutoWartet.Remove(e.Nummer);
            if (!ergebnis.Erfolg)
            {
                Protokollieren(UiHilfe.MeldungText(ergebnis)); // die Frage bleibt offen und kommt als Brief
                return;
            }
            var brief = new Brief
            {
                Titel = "Entschieden", Absender = "Euer Schreiber", Farbe = Color.FromHtml(EntscheidungThema.Farbe), Thema = EntscheidungThema.Id, Ziel = "bericht",
                Zeilen = { new Briefzeile($"{titel} · Automatisch", ergebnis.Meldung, false) },
            };
            if (person is not null) { brief.Person = person; brief.Buch = w.Familien; }
            BriefEinreihen(brief);
        });
    }

    /// <summary>Die Mahnung führt weiter: Kredit → Banktafel (in der eigenen Werkstatt), Verkauf → Auswahl des ersten eigenen Betriebs.</summary>
    private void EntscheidungWegweiser(string ziel)
    {
        if (_simulation.Wirtschaft is not { } w || _spieler is null) return;
        var eigene = w.Betriebe.Where(b => b.Konto == _spieler).ToList();
        if (eigene.Count == 0) { Protokollieren("Ihr habt keinen Betrieb – ein Kredit braucht eine Werkstatt, ein Verkauf einen Betrieb."); return; }
        if (ziel == "bank")
        {
            var werkstatt = _stadt.InnenBetrieb is { } innen && innen.Konto == _spieler ? innen : eigene[0];
            if (_stadt.InnenBetrieb != werkstatt && !_stadt.InnenansichtBetreten(werkstatt)) { BetriebAuswaehlen(werkstatt, hinfahren: true); return; }
            InnenTafelOeffnen("bank");
        }
        else BetriebAuswaehlen(eigene[0], hinfahren: true);
    }
}
