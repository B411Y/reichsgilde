using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die sichtbare Ratssitzung in der Hauptszene (PR #202) – ein Teil der <c>partial class Hauptszene</c>.
///
/// Am Jahresende fragt der Kern, ob die Partie vor der Sitzung anhalten soll (<see cref="Stadtwirtschaft.RatssitzungAnhalten"/>); das entscheidet die
/// Einstellung „Ratssitzung zeigen“ (<see cref="Fenster.Ratssitzung"/>: immer, wenn der Spieler im Rat sitzt oder betroffen ist, nie). Hält sie an, geht die
/// Ansicht in den Ratssaal, die Tafel <see cref="RatssitzungAnzeige"/> führt durch die Punkte, und die Stadtansicht zeigt jeden Schritt
/// (Stadtansicht.Ratssitzung.cs). Während der Sitzung ruhen Raumleiste, Meldungen und Tastenkürzel; Esc öffnet das Spielmenü, Speichern ist dort gesperrt
/// (<see cref="Spielstaende.Speichern"/>). Der Jahresbericht, den der Kern nach dem letzten Punkt auslöst, erscheint erst, wenn die Tafel zu ist.
///
/// „Letzte Sitzung“ im Rathaus (Reiter Rat) zeigt die abgehaltene Sitzung noch einmal im Ratssaal; die Partie steht so lange.
/// </summary>
public partial class Hauptszene
{
    private RatssitzungAnzeige _sitzungsTafel = null!;
    /// <summary>Den Jahresbericht hat der Kern während der Sitzung ausgelöst – er kommt, sobald die Tafel zu ist.</summary>
    private bool _jahresberichtWartet;
    /// <summary>Pause vor der Wiederholung der letzten Sitzung (danach wieder so).</summary>
    private bool _pauseVorWiederholung;
    /// <summary>Startoption <c>--sitzung=letzte</c>: nach dem Vorspulen das Rathaus auf dem Reiter Rat zeigen (dort der Knopf „Letzte Ratssitzung ansehen“).</summary>
    private bool _wiederholungWartet;

    /// <summary>Baut die Tafel der Sitzung und verbindet sie mit der Stadtansicht (in _Ready, vor dem Aufbau der Partie).</summary>
    private void RatssitzungBauen()
    {
        _sitzungsTafel = new RatssitzungAnzeige();
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_sitzungsTafel);
        _sitzungsTafel.PunktGezeigt += punkt => _stadt.RatssitzungPunkt(punkt);
        _sitzungsTafel.RedeBeendet += _stadt.RatssitzungRednerFort;
        _sitzungsTafel.AmZug += amt => _stadt.RatssitzungAmZug(amt);
        _sitzungsTafel.StimmeGezeigt += stimme => _stadt.RatssitzungStimme(stimme);
        _sitzungsTafel.ErgebnisGezeigt += punkt => _stadt.RatssitzungErgebnis(punkt);
        _sitzungsTafel.Gemeldet += Protokollieren;
        _sitzungsTafel.Beendet += wiederholung =>
        {
            _stadt.RatssitzungVerlassen();
            RaumleisteAufbauen();
            if (wiederholung)
            {
                _simulation.Takt.IstPausiert = _pauseVorWiederholung;
                return;
            }
            Protokollieren("Die Ratssitzung ist geschlossen.");
            if (_jahresberichtWartet && _spieler is not null && !_simulation.IstBeendet)
            {
                _jahresbericht.Zeigen(_simulation, _spieler);
                if (BeiJahresberichtPausieren) _simulation.Takt.IstPausiert = true;
            }
            _jahresberichtWartet = false;
        };
        _rathausTafel.LetzteSitzungGewuenscht += WiederholungStarten;
    }

    /// <summary>Sagt dem Kern, wann er vor der Sitzung anhalten soll (bei jeder neuen oder geladenen Partie).</summary>
    private void RatssitzungVerbinden(Simulation simulation, Stadtwirtschaft w, Konto spieler)
    {
        _sitzungsTafel.Verbinden(simulation, spieler);
        w.RatssitzungAnhalten = sitzung => Fenster.Ratssitzung switch
        {
            RatssitzungZeigen.Immer => true,
            RatssitzungZeigen.Nie => false,
            _ => SitzungBetrifft(sitzung, spieler),
        };
    }

    /// <summary>Sitzt der Spieler im Rat, hat er etwas beantragt oder bewirbt er sich um ein Amt?</summary>
    private static bool SitzungBetrifft(Ratssitzung sitzung, Konto spieler) =>
        sitzung.Punkte.Any(p => p.Antragsteller == spieler || p.Stimmen.Any(s => s.Haus == spieler) || p.Kandidaten.Any(k => k.Haus == spieler));

    /// <summary>Jedes Bild: Steht die Partie vor einer Sitzung, die noch nicht gezeigt wird? Dann in den Ratssaal. Ist das Spielmenü offen, wartet die Sitzung.</summary>
    private void RatssitzungPruefen()
    {
        _sitzungsTafel.Angehalten = _spielmenue.Visible || _einstellungen.Tafel.Visible || _hilfe.Visible;
        if (_wiederholungWartet && _spulen == 0 && _bauAutoKlick == 0)
        {
            _wiederholungWartet = false;
            _jahresbericht.Hide(); // nur in diesem Klicktest: der Bericht des vorgespulten Jahres läge über dem Rathaus (die Partie bleibt angehalten)
            _auswahlKennung = "Rathaus";
            _rathausTafel.ReiterWaehlen(RathausTafel.Reiter.Rat);
        }
        if (_simulation.Ratssitzung is null || _sitzungsTafel.Laeuft) return;
        SaalVorbereiten();
        _sitzungsTafel.SitzungZeigen();
        RaumleisteAufbauen();
        Protokollieren("Die Ratssitzung beginnt – die Partie steht, bis alle Punkte abgehalten sind.");
    }

    /// <summary>Zeigt die letzte abgehaltene Sitzung noch einmal (Knopf „Letzte Ratssitzung ansehen“ im Rathaus).</summary>
    private void WiederholungStarten()
    {
        if (_sitzungsTafel.Laeuft || _simulation.Ratssitzung is not null) return;
        if (_simulation.Wirtschaft?.Rat.LetzteRatssitzung is not { Abgehalten.Count: > 0 } letzte)
        {
            Protokollieren("Seit Spielbeginn (oder dem Laden) gab es keine Ratssitzung, die sich zeigen ließe – das Protokoll steht im Bericht.");
            return;
        }
        _pauseVorWiederholung = _simulation.Takt.IstPausiert;
        _simulation.Takt.IstPausiert = true;
        SaalVorbereiten();
        _sitzungsTafel.WiederholungZeigen(letzte);
        RaumleisteAufbauen();
    }

    /// <summary>Alles Offene schließen und in den Ratssaal gehen.</summary>
    private void SaalVorbereiten()
    {
        TafelnSchliessen(auchFeste: true);
        Tooltips.AlleSchliessen();
        if (_bau.Aktiv) _bau.Schliessen();
        if (_hilfe.Visible) HilfeSchliessen();
        _stadt.RatssitzungBetreten(); // ohne Rathaus in der Karte läuft die Sitzung nur auf der Tafel
        RechtsSchliessen(auchFeste: true); // das Betreten wählt das Rathaus aus – rechts bleibt es frei
    }

    /// <summary>Tastatur während der Sitzung: Esc öffnet bzw. schließt das Spielmenü, Leertaste = Weiter, F1 = Hilfe; alles andere ruht.</summary>
    private bool RatssitzungEingabe(InputEvent eingabe)
    {
        if (!_sitzungsTafel.Laeuft || eingabe is not InputEventKey { Pressed: true, Echo: false } taste) return false;
        switch (taste.Keycode)
        {
            case Key.Escape when _einstellungen.Tafel.Visible:
                EinstellungenSchliessen();
                break;
            case Key.Escape when _hilfe.Visible:
                HilfeSchliessen();
                break;
            case Key.Escape:
                if (_spielmenue.Visible) SpielmenueSchliessen();
                else SpielmenueOeffnen();
                break;
            case Key.Space when !_spielmenue.Visible:
                _sitzungsTafel.WeiterGedrueckt();
                break;
            case Key.F1:
                if (_hilfe.Visible) HilfeSchliessen();
                else HilfeOeffnen();
                break;
        }
        GetViewport().SetInputAsHandled();
        return true;
    }

    /// <summary>
    /// Startoptionen für Klicktests: <c>--sitzungzeigen=&lt;immer|betroffen|nie&gt;</c> gilt nur für diesen Start. <c>--sitzung=1</c> bereitet eine Sitzung mit
    /// dem Spieler im Rat vor – ohne <c>--amt</c> bekommt er ein Amt der untersten Stufe und bewirbt sich um eines darüber (eine Wahl; braucht den Stand Bürger,
    /// also <c>--aufsteigen=1 --vermoegen=30000</c>), ohne <c>--antrag</c> wird ein Rivale Kämmerer und beantragt ein Fest (darüber stimmt der Spieler live ab; dafür bekommt er
    /// Aktionspunkte) –
    /// und spult bis zum Jahresende vor (die Partie hält dann vor dem ersten Punkt). <c>--sitzung=letzte</c> spult ebenso vor, hält aber nicht an und zeigt
    /// danach das Rathaus auf dem Reiter Rat mit dem Knopf „Letzte Ratssitzung ansehen“. Nach <see cref="RatStartoptionen"/> ausgewertet.
    /// </summary>
    private void RatssitzungStartoptionen(Dictionary<string, string> optionen)
    {
        if (optionen.TryGetValue("sitzungzeigen", out var zeigen))
        {
            if (Enum.TryParse<RatssitzungZeigen>(zeigen, true, out var wert)) Fenster.RatssitzungSetzen(wert, speichern: false);
            else Protokollieren($"--sitzungzeigen={zeigen}: gemeint ist immer, betroffen oder nie.");
        }
        if (!optionen.TryGetValue("sitzung", out var sitzung) || sitzung is not ("1" or "letzte")) return;
        if (_simulation.Wirtschaft is not { } w || _spieler is null || !w.Rat.IstAktiv) return;
        if (w.Rat.AmtVon(_spieler) is null && w.Rat.Aemter.Where(a => a.Stufe == 1).OrderBy(a => a.Name).FirstOrDefault() is { } unten)
            AmtFuerKlicktest(w, unten);
        if (w.Rat.AmtVon(_spieler) is { } amt && w.Rat.Aemter.Where(a => a.Stufe == amt.Stufe + 1).OrderBy(a => a.Name).FirstOrDefault() is { } hoeher)
            Melden("Bewerbung um " + hoeher.Name, w.Ausfuehren(_spieler, new AmtBewerben(hoeher.Name)));
        // Ein Rivale wird Kämmerer und beantragt ein Fest: Darüber stimmt der Spieler live ab
        if (!optionen.ContainsKey("antrag") && w.Ratsantraege.IstAktiv && w.Konten.Where(k => k != _spieler && k.IstAktiv).OrderByDescending(k => k.Aktionspunkte.Vorrat).FirstOrDefault() is { } rivale
            && w.Rat.AmtNamens("Kämmerer") is { } kaemmerer && kaemmerer.Konto is null)
        {
            AmtFuerKlicktest(w, kaemmerer, rivale);
            // nur zum Testen: Die KI hat ihre Aktionspunkte zu Jahresbeginn meist schon verbraucht, der Kern hat dafür keinen Befehl
            typeof(Reichsgilde.Core.Partie.Aktionspunkte).GetProperty(nameof(Reichsgilde.Core.Partie.Aktionspunkte.Vorrat))!.SetValue(rivale.Aktionspunkte, 6);
            Melden("Fest beantragen (" + rivale.Inhaber + ")", w.Ausfuehren(rivale, new FestBeantragen()));
        }
        if (sitzung == "letzte")
        {
            Fenster.RatssitzungSetzen(RatssitzungZeigen.Nie, speichern: false);
            _wiederholungWartet = true;
        }
        _spulen = Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag - _simulation.Zeit.MinuteImTag; // bis zum Jahreswechsel

        void Melden(string was, Befehlsergebnis ergebnis)
        {
            var text = ergebnis.Erfolg ? ergebnis.Meldung : $"✗ --sitzung: {was}: {ergebnis.Meldung}";
            Protokollieren(text);
            GD.Print(text); // im Log des Klicktests
        }
    }
}
