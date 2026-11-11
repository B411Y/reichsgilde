using Godot;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;

namespace Reichsgilde;

/// <summary>
/// Der Hof-Knopf in der Tafel „Start“ – ein Teil der <c>partial class Kartenwerkstatt</c> (Neue Partie, Nutzerwunsch 11.11.2026).
/// Oben „Höfe (n von 8)“: der Knopf „Hof setzen“ (ein Klick in die Karte setzt Wohnhaus + Betrieb mit der nächsten Nummer, Q/E dreht, Esc beendet),
/// darunter jeder Hof mit Nummer, Drehen (−15° und +15°) und Löschen (die Nummern rücken nach), bei 8 von 8 ein Hinweis. Namen, Wappen und Berufe gibt es
/// hier nicht – die würfelt jede Partie neu. Die bisherige ausführliche Bearbeitung (Spieler, KI-Häuser mit Wappen, Betrieben …) steht
/// darunter unter „Erweitert“ (<see cref="StartZeigen"/>).
/// </summary>
public partial class Kartenwerkstatt
{
    /// <summary>Ist „Erweitert“ in der Tafel „Start“ aufgeklappt? Null: noch nicht gewählt (offen nur, wenn die Karte keine nummerierten Höfe hat).</summary>
    private bool? _startErweitert;

    private void HoefeTeil()
    {
        var anzahl = _entwurf.NummerierteHoefe;
        var hoechstens = Partiebesetzung.HoechstensHoefe;
        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 8);
        _startTafel.AddChild(kopf);
        var titel = Hauptmenue.Text(kopf, $"Höfe ({anzahl} von {hoechstens})", 16, Gold);
        titel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var setzen = Knopf(kopf, "Hof setzen", HofSetzenWaehlen,
            "Ein Klick in die Karte setzt einen Hof: Wohnhaus und Betrieb zusammen. Der erste ist Hof 1 (Spieler 1), der zweite Hof 2 …, höchstens 8. Q und E drehen, Esc beendet.");
        setzen.Disabled = anzahl >= hoechstens;
        Hauptmenue.Text(_startTafel, anzahl >= hoechstens
            ? $"{hoechstens} von {hoechstens} Höfen gesetzt – mehr gehen nicht. Wer einen löscht, schafft Platz."
            : "Hof 1 ist Spieler 1, Hof 2 Spieler 2 und so weiter – ob Mensch oder KI, bestimmt die Partie. Name, Wappen und Beruf würfelt jede Partie neu.",
            12, anzahl >= hoechstens ? Gold : Grau, umbruchBreite: Rechtsbreite - 44);
        foreach (var h in _entwurf.Start.Hofstellen.Where(h => h.Nummer is not null).OrderBy(h => h.Nummer))
        {
            var kennung = h.Kennung;
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 6);
            _startTafel.AddChild(zeile);
            var name = Knopf(zeile, $"Hof {h.Nummer}", () => _stadt.WerkstattWaehlen(h.Werkstatt, hinfahren: true), $"Hof {h.Nummer} ({kennung}) – hinfahren");
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            name.Alignment = HorizontalAlignment.Left;
            Knopf(zeile, "−15°", () => HofBefehl(() => _entwurf.HofDrehen(kennung, -15)), "15° gegen den Uhrzeigersinn drehen");
            Knopf(zeile, "+15°", () => HofBefehl(() => _entwurf.HofDrehen(kennung, 15)), "15° im Uhrzeigersinn drehen");
            Knopf(zeile, "Löschen", () => HofBefehl(() => _entwurf.HofLoeschen(kennung)), "Den Hof mit Wohnhaus und Betrieb löschen; die Höfe mit höherer Nummer rücken nach");
        }
    }

    /// <summary>Wählt das Werkzeug „Hof“; die Tafel „Start“ bleibt offen, damit man die Höfe beim Setzen sieht.</summary>
    private void HofSetzenWaehlen()
    {
        PaletteSchliessen();
        KatalogWahlAufheben();
        _werkzeugName = "Start › Hof setzen";
        _stadt.WerkstattWerkzeugWaehlen(Werkstattwerkzeug.Hofstelle);
    }

    /// <summary>Der Kopf von „Erweitert“: ein Knopf, der den Rest der Tafel auf- und zuklappt; gibt den Behälter für den Inhalt zurück.</summary>
    private VBoxContainer ErweitertTeil()
    {
        var offen = _startErweitert ?? _entwurf.NummerierteHoefe == 0;
        var schalter = new Button
        {
            Text = (offen ? "– " : "+ ") + "Erweitert", Flat = true, Alignment = HorizontalAlignment.Left, FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Die ausführliche Bearbeitung: Spieler, KI-Häuser mit Wappen und Betrieben, Hofstellen einzeln wählen",
        };
        schalter.AddThemeFontSizeOverride("font_size", 16);
        schalter.AddThemeColorOverride("font_color", Gold);
        _startTafel.AddChild(schalter);
        var inhalt = new VBoxContainer { Visible = offen };
        inhalt.AddThemeConstantOverride("separation", 8);
        _startTafel.AddChild(inhalt);
        schalter.Pressed += () =>
        {
            _startErweitert = !offen;
            StartZeigen();
        };
        return inhalt;
    }
}
