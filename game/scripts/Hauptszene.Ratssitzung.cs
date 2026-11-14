using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die sichtbare Ratssitzung in der Hauptszene (PR #202) – ein Teil der <c>partial class Hauptszene</c>.
///
/// Stimmrunde und Wiedergabe (Ratssitzung PR 8, Entscheidung F5): In den letzten Minuten des Jahres (<see cref="Stadtrat.StimmrundeLaeuft"/>) öffnet sich die
/// kleine Tafel <see cref="StimmrundeAnzeige"/>, wenn der Spieler noch stimmen darf; er stimmt dort mit Befehlen ab. Der Kern hält die Sitzung am Jahresende
/// immer in einem Zug ab (kein Halt mehr, <see cref="Stadtwirtschaft.RatssitzungAnhalten"/> bleibt leer). Danach zeigt die Szene das feste Ergebnis als
/// Wiedergabe – nach der Einstellung „Ratssitzung zeigen“ (<see cref="Fenster.Ratssitzung"/>: immer, wenn der Spieler im Rat sitzt oder betroffen ist, nie):
/// Die Ansicht geht in den Ratssaal, die Oberfläche <see cref="RatssitzungAnzeige"/> (Spruchband unten, Ratsbuch rechts) führt durch die Punkte, und die
/// Stadtansicht zeigt jeden Schritt (Stadtansicht.Ratssitzung.cs). Im Einzelspiel ruht die Zeit währenddessen (wie bei den Entscheidungen nur ohne
/// Befehlsverzögerung), im Mehrspieler läuft sie weiter. Während der Szene ruhen Raumleiste, Meldungen und Tastenkürzel; Esc öffnet das Spielmenü.
/// Der Jahresbericht erscheint erst, wenn die Tafel zu ist.
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
    /// <summary>Die kleine Tafel der Stimmrunde vor der Sitzung.</summary>
    private StimmrundeAnzeige _stimmrunde = null!;
    /// <summary>Für welches Jahr die Stimmrunde schon geöffnet wurde (sie öffnet sich je Jahr einmal von selbst).</summary>
    private int _stimmrundeJahr;
    /// <summary>Pause vor der Stimmrunde (null: die Stimmrunde hat nicht angehalten).</summary>
    private bool? _pauseVorStimmrunde;
    /// <summary>Die eben abgehaltene Sitzung, die als Wiedergabe gezeigt wird, sobald nicht mehr vorgespult wird.</summary>
    private Ratssitzung? _sitzungZuZeigen;
    /// <summary>Die laufende Wiedergabe ist die der Jahresend-Sitzung (danach kommt der Jahresbericht), nicht „Letzte Ratssitzung ansehen“.</summary>
    private bool _wiedergabeJahresende;
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
        _sitzungsTafel.Gezaehlt += (stimme, nummer, stand) => _stadt.RatssitzungGezaehlt(stimme, nummer, stand);
        _sitzungsTafel.ErgebnisGezeigt += punkt =>
        {
            _stadt.RatssitzungErgebnis(punkt);
            SitzungErgebnisAnsagen(punkt); // Wahl gewonnen oder verloren (Haushofmeister)
        };
        _sitzungsTafel.Gemeldet += Protokollieren;
        _sitzungsTafel.Beendet += wiederholung =>
        {
            _stadt.RatssitzungVerlassen();
            RaumleisteAufbauen();
            if (wiederholung)
            {
                _simulation.Takt.IstPausiert = _pauseVorWiederholung;
                if (!_wiedergabeJahresende) return;
                _wiedergabeJahresende = false;
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

        _stimmrunde = new StimmrundeAnzeige();
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_stimmrunde);
        _stimmrunde.Gemeldet += Protokollieren;
        _stimmrunde.Geschlossen += () =>
        {
            if (_pauseVorStimmrunde is { } vorher) _simulation.Takt.IstPausiert = vorher;
            _pauseVorStimmrunde = null;
        };
    }

    /// <summary>Verbindet Sitzungstafel und Stimmrunde mit der Partie (bei jeder neuen oder geladenen). Der Kern hält nie vor der Sitzung an.</summary>
    private void RatssitzungVerbinden(Simulation simulation, Stadtwirtschaft w, Konto spieler)
    {
        _sitzungsTafel.Verbinden(simulation, spieler);
        _stimmrunde.Verbinden(simulation, spieler);
        w.RatssitzungAnhalten = null; // Szene als Wiedergabe: Die Sitzung läuft im Kern in einem Zug
        _stimmrundeJahr = 0;
        _sitzungZuZeigen = null;
    }

    /// <summary>
    /// Am Jahresende (Ereignis <c>JahrBeendet</c>): Soll die eben abgehaltene Sitzung als Wiedergabe gezeigt werden? Dann merkt sie sich die Szene,
    /// und der Jahresbericht wartet, bis sie zu Ende ist.
    /// </summary>
    private bool SitzungVormerken(int jahr)
    {
        if (_spieler is null || _simulation.Wirtschaft?.Rat.LetzteRatssitzung is not { Jahr: var j, Abgehalten.Count: > 0 } letzte || j != jahr) return false;
        var zeigen = Fenster.Ratssitzung switch
        {
            RatssitzungZeigen.Immer => true,
            RatssitzungZeigen.Nie => false,
            _ => SitzungBetrifft(letzte, _spieler),
        };
        if (zeigen) _sitzungZuZeigen = letzte;
        return zeigen;
    }

    /// <summary>Ohne Befehlsverzögerung (Einzelspiel) hält die Zeit für Stimmrunde und Szene an; im Mehrspieler läuft sie weiter.</summary>
    private bool DarfAnhalten => _simulation.Bote.Verzoegerung == 0;

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
        }
        StimmrundePruefen();
        if (_sitzungZuZeigen is { } gezeigt && !_sitzungsTafel.Laeuft && _spulen == 0)
        {
            _sitzungZuZeigen = null;
            _pauseVorWiederholung = _simulation.Takt.IstPausiert;
            if (DarfAnhalten) _simulation.Takt.IstPausiert = true;
            _wiedergabeJahresende = true;
            SaalVorbereiten();
            _sitzungsTafel.WiederholungZeigen(gezeigt, gerade: true);
            RaumleisteAufbauen();
            Protokollieren("Die Ratssitzung beginnt – der Rat hat schon abgestimmt, die Szene zeigt das Ergebnis.");
            SitzungAnsagen(gezeigt); // Haushofmeister
            return;
        }
        if (_simulation.Ratssitzung is null || _sitzungsTafel.Laeuft) return;
        SaalVorbereiten();
        _sitzungsTafel.SitzungZeigen();
        RaumleisteAufbauen();
        Protokollieren("Die Ratssitzung beginnt – die Partie steht, bis alle Punkte abgehalten sind.");
        SitzungAnsagen(_simulation.Ratssitzung!); // Haushofmeister
    }

    /// <summary>Öffnet die Stimmrunde einmal je Jahr, sobald sie läuft und der Spieler noch stimmen darf (nicht beim Vorspulen, nicht in der Szene).</summary>
    private void StimmrundePruefen()
    {
        if (_spieler is null || _simulation.Wirtschaft is not { } w || _stimmrundeJahr == w.Jahr || _spulen != 0 || _sitzungsTafel.Laeuft) return;
        if (!w.Rat.StimmrundeLaeuft || w.Rat.Stimmrunde(_spieler).Count == 0) return;
        _stimmrundeJahr = w.Jahr;
        _stimmrunde.Zeigen();
        Protokollieren("Die Stimmrunde vor der Ratssitzung beginnt – bis 23 Uhr könnt ihr eure Stimmen abgeben.");
        if (!DarfAnhalten) return;
        _pauseVorStimmrunde = _simulation.Takt.IstPausiert;
        _simulation.Takt.IstPausiert = true;
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
            case Key.Escape when _lexikon.Offen:
                _lexikon.Schliessen();
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
    /// Startoptionen für Klicktests: <c>--sitzungzeigen=&lt;immer|betroffen|nie&gt;</c> gilt nur für diesen Start. <c>--domwahl=1</c> lässt alle geeigneten Häuser (auch den Spieler) sich um das Amt des Dompredigers bewerben (mit <c>--sitzung=1</c> kommt der Dom-Punkt in die Sitzung). <c>--sitzung=1</c> bereitet eine Sitzung mit
    /// dem Spieler im Rat vor – ohne <c>--amt</c> bekommt er ein Amt der untersten Stufe und bewirbt sich um eines darüber (eine Wahl; braucht den Stand Bürger,
    /// also <c>--aufsteigen=1 --vermoegen=30000</c>), ohne <c>--antrag</c> wird ein Rivale Kämmerer und beantragt ein Fest (darüber stimmt der Spieler live ab; dafür bekommt er
    /// Aktionspunkte) –
    /// und spult bis zum Jahresende vor (die Partie hält dann vor dem ersten Punkt). <c>--ohneamt=1</c> (zusätzlich) lässt den Spieler ohne Amt und ohne Sitz im Rat: Er bewirbt sich um ein Amt der Stufe 1 und wartet auf der Zuhörerbank. <c>--sitzung=letzte</c> spult ebenso vor, hält aber nicht an und zeigt
    /// danach das Rathaus auf dem Reiter Rat mit dem Knopf „Letzte Ratssitzung ansehen“. Nach <see cref="RatStartoptionen"/> ausgewertet.
    /// </summary>
    private void RatssitzungStartoptionen(Dictionary<string, string> optionen)
    {
        if (optionen.TryGetValue("sitzungzeigen", out var zeigen))
        {
            if (Enum.TryParse<RatssitzungZeigen>(zeigen, true, out var wert)) Fenster.RatssitzungSetzen(wert, speichern: false);
            else Protokollieren($"--sitzungzeigen={zeigen}: gemeint ist immer, betroffen oder nie.");
        }
        if (optionen.TryGetValue("domwahl", out var domwahl) && domwahl == "1" && _simulation.Wirtschaft is { } dw && dw.Domamt.IstAktiv)
        {
            // Prediger und Dom: alle geeigneten Häuser bewerben sich um das Amt (steht der Spieler selbst zur Wahl, stimmt er nicht mit)
            foreach (var kandidat in dw.Konten.Where(k => k.IstAktiv))
                if (dw.Domamt.EignungVerboten(kandidat) is null)
                    _simulation.Bote.Einreihen(kandidat, new DomBewerben(), e => GD.Print($"--domwahl: {kandidat.Inhaber}: {e.Meldung}"));
                else GD.Print($"--domwahl: {kandidat.Inhaber} nicht geeignet: {dw.Domamt.EignungVerboten(kandidat)}");
        }
        if (!optionen.TryGetValue("sitzung", out var sitzung) || sitzung is not ("1" or "letzte")) return;
        if (_simulation.Wirtschaft is not { } w || _spieler is null || !w.Rat.IstAktiv) return;
        var ohneAmt = optionen.TryGetValue("ohneamt", out var oa) && oa == "1"; // der Spieler sitzt nicht im Rat: Er wartet als Bewerber auf der Zuhörerbank
        if (!ohneAmt && w.Rat.AmtVon(_spieler) is null && w.Rat.Aemter.Where(a => a.Stufe == 1).OrderBy(a => a.Name).FirstOrDefault() is { } unten)
            AmtFuerKlicktest(w, unten);
        if (ohneAmt && w.Rat.AmtVon(_spieler) is null && w.Rat.Aemter.Where(a => a.Stufe == 1).OrderBy(a => a.Name).FirstOrDefault() is { } erstes)
            _simulation.Bote.Einreihen(_spieler, new AmtBewerben(erstes.Name), e => Melden("Bewerbung um " + erstes.Name, e));
        if (w.Rat.AmtVon(_spieler) is { } amt && w.Rat.Aemter.Where(a => a.Stufe == amt.Stufe + 1).OrderBy(a => a.Name).FirstOrDefault() is { } hoeher)
            _simulation.Bote.Einreihen(_spieler, new AmtBewerben(hoeher.Name), e => Melden("Bewerbung um " + hoeher.Name, e));
        // Ein Rivale wird Kämmerer und beantragt ein Fest: Darüber stimmt der Spieler live ab
        if (!optionen.ContainsKey("antrag") && w.Ratsantraege.IstAktiv && w.Konten.Where(k => k != _spieler && k.IstAktiv).OrderByDescending(k => k.Aktionspunkte.Vorrat).FirstOrDefault() is { } rivale
            && w.Rat.AmtNamens("Kämmerer") is { } kaemmerer && kaemmerer.Konto is null)
        {
            AmtFuerKlicktest(w, kaemmerer, rivale);
            // nur zum Testen: Die KI hat ihre Aktionspunkte zu Jahresbeginn meist schon verbraucht, der Kern hat dafür keinen Befehl
            typeof(Reichsgilde.Core.Partie.Aktionspunkte).GetProperty(nameof(Reichsgilde.Core.Partie.Aktionspunkte.Vorrat))!.SetValue(rivale.Aktionspunkte, 6);
            _simulation.Bote.Einreihen(rivale, new FestBeantragen(), e => Melden("Fest beantragen (" + rivale.Inhaber + ")", e));
        }
        if (sitzung == "letzte")
        {
            Fenster.RatssitzungSetzen(RatssitzungZeigen.Nie, speichern: false);
            _wiederholungWartet = true;
        }
        // „1“: bis zum Beginn der Stimmrunde (die Tafel öffnet sich, nach „Fertig“ läuft die Zeit bis zur Sitzung); „letzte“: bis zum Jahreswechsel
        _spulen = sitzung == "1" && w.Rat.StimmrundeLaeuft is false
            ? (int)(w.Rat.StimmrundeBeginn - _simulation.Zeit.VergangeneMinuten)
            : Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag - _simulation.Zeit.MinuteImTag;

        void Melden(string was, Befehlsergebnis ergebnis)
        {
            var text = ergebnis.Erfolg ? ergebnis.Meldung : $"✗ --sitzung: {was}: {ergebnis.Meldung}";
            Protokollieren(text);
            GD.Print(text); // im Log des Klicktests
        }
    }
}
