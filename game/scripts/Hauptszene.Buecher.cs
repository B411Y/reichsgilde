using Godot;

namespace Reichsgilde;

/// <summary>
/// <b>3D-Bücher der Innenansichten</b> – ein Teil der <c>partial class Hauptszene</c>: verbindet die Buchschau der Stadtansicht
/// (<c>Stadtansicht.Innenbuecher.cs</c>: Kamera-Schwenk, Aufklappen, Umblättern, Zuklappen) mit den echten 2D-Tafeln.
///
///   • Personalbuch (Lesepult) → Personaltafel, Familienbuch (Stubentisch) → Familienbuch, Kontorbuch (Schreibpult) → Bericht
///     (Jahresrechnung, Rivalen, Umsätze, Verlauf; er öffnet wie alle Fenster in der Bildmitte, also über dem Buch).
///   • Die Tafel öffnet sich erst, wenn die Seiten flach liegen. Wechselt ihr Reiter, blättert das Buch um; die Tafel ist so lange ausgeblendet.
///   • Schließt der Spieler die Tafel (Esc, ✕, eine andere Tafel) oder klickt er neben das Buch, klappt es zu und die Kamera fährt zurück.
///   • Während des Schwenks liegt eine unsichtbare Sperre über der ganzen Oberfläche: nichts ist anklickbar, Esc tut nichts.
/// </summary>
public partial class Hauptszene
{
    private enum BuchPhase { Keine, WartetAufSeiten, TafelOffen, Schliesst }

    private Control _buchSperre = null!;
    private BuchPhase _buchPhase;
    private BuchArt _buchArt;
    private int _buchReiter;

    /// <summary>Einmal beim Aufbau: Ereignisse der Stadtansicht und die Sperre über der Oberfläche.</summary>
    private void BuecherVerbinden()
    {
        _buchSperre = new Control { Name = "BuchSperre", MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        _buchSperre.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_buchSperre);
        _stadt.BuchAngeklickt += BuchAngeklickt;
        _stadt.BuchSchliessenGewuenscht += BuchSchliessen;
    }

    /// <summary>Ein Buch im Raum wurde angeklickt (auch Startoption <c>--buch=</c>): Personal- und Kontorbuch nur in der eigenen Werkstatt.</summary>
    private void BuchAngeklickt(BuchArt art)
    {
        if (_spieler is null || _buchPhase != BuchPhase.Keine) return;
        if (art != BuchArt.Familie && _stadt.InnenBetrieb is { } betrieb && betrieb.Konto != _spieler)
        {
            Protokollieren($"{betrieb.Name} gehört {betrieb.Konto.Inhaber} – die Bücher und Werkzeuge dort sind nicht für Euch.");
            return;
        }
        TafelnSchliessen();
        if (!_stadt.BuchSchauOeffnen(art)) return;
        _buchArt = art;
        _buchPhase = BuchPhase.WartetAufSeiten;
    }

    private Control BuchTafel(BuchArt art) => art switch
    {
        BuchArt.Personal => _personalTafel.Tafel,
        BuchArt.Familie => _familientafel,
        _ => _berichtTafel,
    };

    private int BuchReiter(BuchArt art) => art switch
    {
        BuchArt.Familie => (int)_familientafel.ReiterJetzt,
        BuchArt.Kontor => _berichtTafel.ReiterNummer,
        _ => 0,
    };

    /// <summary>Tafel zu, Buch zu, Kamera zurück (Klick neben das Buch, Esc ohne Fenster).</summary>
    private void BuchSchliessen()
    {
        if (_buchPhase != BuchPhase.TafelOffen || _stadt.BuchGesperrt) return;
        var tafel = BuchTafel(_buchArt);
        _buchPhase = BuchPhase.Schliesst;
        if (_buchArt == BuchArt.Personal) _personalTafel.Hide();
        else if (_buchArt == BuchArt.Familie) Fensterrahmen.SchliessenFallsLose(tafel, true);
        else tafel.Hide();
        tafel.Modulate = Colors.White;
        _stadt.BuchSchauSchliessen();
    }

    /// <summary>Jedes Bild: Sperre, Tafel öffnen, Reiterwechsel erkennen, Schließen der Tafel erkennen.</summary>
    private void BuecherAktualisieren(double delta)
    {
        _buchSperre.Visible = _stadt.BuchGesperrt;
        if (_buchPhase == BuchPhase.Keine) return;
        var tafel = BuchTafel(_buchArt);
        if (_stadt.BuchOffen is null) // fertig zugeklappt oder abgebrochen (Raumwechsel, Haus verlassen)
        {
            tafel.Modulate = Colors.White;
            _buchPhase = BuchPhase.Keine;
            return;
        }
        switch (_buchPhase)
        {
            case BuchPhase.WartetAufSeiten when _stadt.BuchTafelSichtbar:
                switch (_buchArt)
                {
                    case BuchArt.Personal: InnenTafelOeffnen("personal"); break;
                    case BuchArt.Familie: FamilieOeffnen(); break;
                    default: BerichtOeffnen(BerichtAnzeige.Reiter.Jahresrechnung); break;
                }
                _buchReiter = BuchReiter(_buchArt);
                _buchPhase = BuchPhase.TafelOffen;
                break;
            case BuchPhase.TafelOffen when !tafel.Visible: // der Spieler hat die Tafel geschlossen (Esc, ✕, andere Tafel)
                tafel.Modulate = Colors.White;
                _buchPhase = BuchPhase.Schliesst;
                _stadt.BuchSchauSchliessen();
                break;
            case BuchPhase.TafelOffen:
                var reiter = BuchReiter(_buchArt);
                if (reiter != _buchReiter)
                {
                    _stadt.BuchUmblaettern(reiter > _buchReiter);
                    _buchReiter = reiter;
                }
                // Beim Umblättern ist die Tafel weg (sofort), danach blendet sie kurz ein
                var soll = _stadt.BuchTafelSichtbar ? 1f : 0f;
                var ist = tafel.Modulate.A;
                tafel.Modulate = new Color(1, 1, 1, soll < ist ? 0 : Mathf.MoveToward(ist, soll, (float)delta * 10));
                break;
        }
    }
}
