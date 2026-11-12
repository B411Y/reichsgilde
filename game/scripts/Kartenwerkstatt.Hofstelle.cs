using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Die Gruppe „Hofstelle“ in den Eigenschaften eines Werkstatt- oder Wohnhausplatzes – ein Teil der <c>partial class Kartenwerkstatt</c>.
/// Gehört der Platz zu einer Hofstelle (<see cref="EntwurfHofstelle"/>): Feld Name, Haken „Startplatz“, Knöpfe „Hofstelle auflösen“ (die Plätze
/// bleiben) und „Hofstelle löschen“ (mit beiden Plätzen). Gehört er zu keiner: ein Hinweis und, wenn ein freier Platz der anderen Art da ist,
/// der Knopf „Hofstelle bilden“ mit dem nächstgelegenen. Jede Änderung ist ein Befehl des Entwurfs. Die Gruppe baut sich neu, wenn sich die
/// Zugehörigkeit ändert (<see cref="HofstelleAuffrischen"/>, aufgerufen bei Änderungen der Startbelegung).
/// </summary>
public partial class Kartenwerkstatt
{
    private VBoxContainer? _hofBox;
    private EntwurfHofstelle? _hofGezeigt;
    private LineEdit? _hofName;
    private CheckBox? _hofStart;

    /// <summary>Hängt die Gruppe an die Eigenschaften des Platzes (nur Werkstatt- und Wohnhausplätze).</summary>
    private void HofstelleGruppe(EntwurfGebaeude g)
    {
        _hofBox = null;
        if (g.Art is not (Bausteinart.Werkstattplatz or Bausteinart.Wohnhausplatz)) return;
        _hofBox = new VBoxContainer();
        _hofBox.AddThemeConstantOverride("separation", 6);
        _eigenschaften.AddChild(_hofBox);
        HofstelleFuellen(g);
    }

    private void HofstelleFuellen(EntwurfGebaeude g)
    {
        if (_hofBox is null) return;
        UiHilfe.KinderEntfernen(_hofBox);
        var h = _entwurf.Start.HofstelleVon(g.Kennung);
        _hofGezeigt = h;
        (_hofName, _hofStart) = (null, null);
        var kopf = Hauptmenue.Text(_hofBox, h is null ? "Hofstelle" : $"Hofstelle {h.Kennung}", 15, Gold);
        kopf.CustomMinimumSize = new Vector2(0, 26);
        if (h is null)
        {
            Hauptmenue.Text(_hofBox, "Gehört zu keiner Hofstelle.", 13, Grau, umbruchBreite: Rechtsbreite - 30);
            var andere = g.Art == Bausteinart.Werkstattplatz ? Bausteinart.Wohnhausplatz : Bausteinart.Werkstattplatz;
            var nah = _entwurf.Gebaeude.Where(x => x.Art == andere && _entwurf.Start.HofstelleVon(x.Kennung) is null)
                .OrderBy(x => (x.X - g.X) * (x.X - g.X) + (x.Z - g.Z) * (x.Z - g.Z)).FirstOrDefault();
            if (nah is not null)
            {
                var knoepfe = new HBoxContainer();
                _hofBox.AddChild(knoepfe);
                var (werkstatt, wohnhaus) = g.Art == Bausteinart.Werkstattplatz ? (g.Kennung, nah.Kennung) : (nah.Kennung, g.Kennung);
                Knopf(knoepfe, "Hofstelle bilden", () => HofBefehl(() => _entwurf.HofstelleBilden(werkstatt, wohnhaus)),
                    $"Macht aus diesem Platz und dem nächsten freien {(andere == Bausteinart.Werkstattplatz ? "Werkstattplatz" : "Wohnhausplatz")} ({nah.Name}) eine Hofstelle");
            }
            return;
        }
        var raster = new GridContainer { Columns = 2 };
        raster.AddThemeConstantOverride("h_separation", 10);
        raster.AddThemeConstantOverride("v_separation", 6);
        _hofBox.AddChild(raster);
        _hofName = new LineEdit { Text = h.Name ?? "", PlaceholderText = "(Lage, z. B. Heumarkt)", TooltipText = "Die Lage; nach ihr heißt die Werkstatt des Spielers („Gießerei am Heumarkt“) und so steht sie in der Wahl des Startmenüs" };
        _hofName.TextSubmitted += _ => _hofName.ReleaseFocus();
        var kennung = h.Kennung;
        _hofName.FocusExited += () =>
        {
            var jetzt = _entwurf.Start.HofstelleMit(kennung);
            var text = _hofName?.Text.Trim();
            if (jetzt is not null && (string.IsNullOrEmpty(text) ? null : text) != jetzt.Name) HofBefehl(() => _entwurf.HofstelleUmbenennen(kennung, string.IsNullOrEmpty(text) ? null : text));
        };
        Zeile(raster, "Name", _hofName);
        if (h.Nummer is { } nr) Zeile(raster, "Hof", Wert($"Hof {nr} (Spieler {nr})"));
        Zeile(raster, "Werkstatt", Wert(h.Werkstatt));
        Zeile(raster, "Wohnhaus", Wert(h.Wohnhaus));
        _hofStart = Haken("Startplatz", h.Start, an => HofBefehl(() => _entwurf.HofstelleStartSetzen(kennung, an)),
            "An: Der Spieler darf diese Hofstelle im Startmenü wählen, sie wird gelost, und für sie gilt die Fairness (Karrenweg zum Markt ±10 %). Aus: nur zum späteren Bauen (z. B. am Hafen).");
        _hofBox.AddChild(_hofStart);
        var knoepfeZeile = new HBoxContainer();
        knoepfeZeile.AddThemeConstantOverride("separation", 8);
        _hofBox.AddChild(knoepfeZeile);
        Knopf(knoepfeZeile, "Hofstelle auflösen", () => HofBefehl(() => _entwurf.HofstelleAufloesen(kennung)), "Die Hofstelle fällt weg, beide Plätze bleiben stehen");
        Knopf(knoepfeZeile, "Hofstelle löschen", () => HofBefehl(() => _entwurf.HofLoeschen(kennung)), "Die Hofstelle mit Werkstatt- und Wohnhausplatz aus der Karte nehmen (nummerierte Höfe: die höheren Nummern rücken nach)");
    }

    /// <summary>Nach einer Änderung der Startbelegung: Gruppe neu bauen, wenn sich die Zugehörigkeit änderte, sonst nur Name und Haken nachziehen.</summary>
    private void HofstelleAuffrischen()
    {
        if (_hofBox is null || !IsInstanceValid(_hofBox) || _gezeigt is not EntwurfGebaeude g) return;
        var h = _entwurf.Start.HofstelleVon(g.Kennung);
        if (h == _hofGezeigt) return;
        if (h is not null && _hofGezeigt is { } alt && h.Kennung == alt.Kennung && h.Werkstatt == alt.Werkstatt && h.Wohnhaus == alt.Wohnhaus)
        {
            _hofGezeigt = h;
            if (_hofName is { } name && !name.HasFocus()) name.Text = h.Name ?? "";
            _hofStart?.SetPressedNoSignal(h.Start);
            return;
        }
        HofstelleFuellen(g);
    }

    private void HofBefehl(Action aktion)
    {
        try { aktion(); }
        catch (ArgumentException e) { GD.PushWarning($"Kartenwerkstatt: {e.Message}"); }
    }
}
