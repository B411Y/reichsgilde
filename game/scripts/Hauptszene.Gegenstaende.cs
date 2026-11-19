using Godot;

namespace Reichsgilde;

/// <summary>
/// <b>3D-Gegenstände im Kontor</b> – ein Teil der <c>partial class Hauptszene</c>: verbindet die Schau der Stadtansicht
/// (<c>Stadtansicht.Innengegenstaende.cs</c>: Kamera-Schwenk, Karte entrollt sich, Truhendeckel springt auf) mit der Oberfläche.
///
///   • Kartentisch: Die Karte entrollt sich und zeigt das Bild der gespielten Karte; es öffnet sich keine Tafel.
///   • Geldtruhe: Sobald der Deckel oben ist, öffnet der Bericht mit der Jahresrechnung (dieselbe Tafel wie beim Kontorbuch).
///   • Schließt der Spieler die Tafel (Esc, ✕, eine andere Tafel) oder klickt er neben den Gegenstand, schließt er sich und die Kamera fährt zurück.
///   • Während des Schwenks liegt die Sperre der Bücher über der Oberfläche: nichts ist anklickbar, Esc tut nichts.
/// </summary>
public partial class Hauptszene
{
    private enum GegenstandPhase { Keine, WartetAufOffen, Offen, Schliesst }

    private GegenstandPhase _gegenstandPhase;
    private GegenstandArt _gegenstandArt;

    /// <summary>Einmal beim Aufbau: Ereignisse der Stadtansicht (nach <see cref="BuecherVerbinden"/>, die Sperre gehört den Büchern).</summary>
    private void GegenstaendeVerbinden()
    {
        _stadt.GegenstandAngeklickt += GegenstandAngeklickt;
        _stadt.GegenstandSchliessenGewuenscht += GegenstandSchliessen;
    }

    /// <summary>Kartentisch oder Geldtruhe im Raum wurde angeklickt (auch Startoption <c>--gegenstand=</c>): nur im eigenen Haus.</summary>
    private void GegenstandAngeklickt(GegenstandArt art)
    {
        if (_spieler is null || _gegenstandPhase != GegenstandPhase.Keine || _buchPhase != BuchPhase.Keine) return;
        if (_stadt.GegenstandKonto(art) is not { } konto) return;
        if (konto != _spieler)
        {
            Protokollieren($"Dieses Kontor gehört {konto.Inhaber}. Karte und Truhe dort sind nicht für Euch.");
            return;
        }
        TafelnSchliessen();
        if (!_stadt.GegenstandSchauOeffnen(art)) return;
        _gegenstandArt = art;
        _gegenstandPhase = GegenstandPhase.WartetAufOffen;
    }

    /// <summary>Die Tafel, die über dem offenen Gegenstand liegt (null: keine).</summary>
    private Control? GegenstandTafel(GegenstandArt art) => art == GegenstandArt.Geldtruhe ? _berichtTafel : null;

    /// <summary>Tafel zu, Gegenstand zu, Kamera zurück (Klick daneben, Rechtsklick, Esc ohne Fenster).</summary>
    private void GegenstandSchliessen()
    {
        if (_gegenstandPhase != GegenstandPhase.Offen || _stadt.GegenstandGesperrt) return;
        _gegenstandPhase = GegenstandPhase.Schliesst;
        GegenstandTafel(_gegenstandArt)?.Hide();
        _stadt.GegenstandSchauSchliessen();
    }

    /// <summary>Jedes Bild (nach <see cref="BuecherAktualisieren"/>): Sperre, Tafel öffnen, Schließen der Tafel erkennen.</summary>
    private void GegenstaendeAktualisieren()
    {
        if (_stadt.GegenstandGesperrt) _buchSperre.Visible = true;
        if (_gegenstandPhase == GegenstandPhase.Keine) return;
        if (_stadt.GegenstandOffen is null) // fertig geschlossen oder abgebrochen (Raumwechsel, Haus verlassen)
        {
            _gegenstandPhase = GegenstandPhase.Keine;
            return;
        }
        var tafel = GegenstandTafel(_gegenstandArt);
        switch (_gegenstandPhase)
        {
            case GegenstandPhase.WartetAufOffen when _stadt.GegenstandOffenSichtbar:
                if (_gegenstandArt == GegenstandArt.Geldtruhe) BerichtOeffnen(BerichtAnzeige.Reiter.Jahresrechnung);
                _gegenstandPhase = GegenstandPhase.Offen;
                break;
            case GegenstandPhase.Offen when tafel is { Visible: false } && !_stadt.GegenstandGesperrt: // der Spieler hat die Tafel geschlossen (Esc, ✕, andere Tafel)
                _gegenstandPhase = GegenstandPhase.Schliesst;
                _stadt.GegenstandSchauSchliessen();
                break;
        }
    }
}
