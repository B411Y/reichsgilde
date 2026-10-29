using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Wege-Modus des Baumenüs in der Oberfläche – ein Teil der <c>partial class Hauptszene</c>: Wege lassen sich nur im Baumenü anklicken (Umschalter
/// „Gebäude | Wege“ in der <see cref="BauAnzeige"/>, Taste G). Der Weg unter der Maus bekommt einen kleinen Tooltip mit Belag, nächster Stufe, Kosten und Bauzeit
/// (gleiche Zahlen wie die Wegtafel, aus <see cref="Wegebau.Vorschau"/>); der Klick öffnet die Wegtafel links neben der Bautafel.
/// </summary>
public partial class Hauptszene
{
    private PanelContainer? _wegTipp;
    private Label? _wegTippText;
    private Vector2 _wegTippGroesse;

    private void WegeModusVerbinden()
    {
        _wegTipp = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 100 };
        _wegTipp.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.11f, 0.09f, 0.96f), BorderColor = new Color(0.95f, 0.82f, 0.45f, 0.8f),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 6, ContentMarginBottom = 6,
        });
        _wegTippText = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        _wegTippText.AddThemeFontSizeOverride("font_size", 14);
        _wegTipp.AddChild(_wegTippText);
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_wegTipp);
        _stadt.WegUnterDerMaus += WegTippZeigen;
    }

    private void WegTippZeigen((int Weg, double Stelle)? ziel)
    {
        if (_wegTipp is null || _wegTippText is null) return;
        if (ziel is not var (weg, stelle) || _simulation.Wirtschaft is not { } w || _spieler is null || w.Karte is not { } karte || !w.Wegebau.IstAktiv)
        {
            _wegTipp.Hide();
            return;
        }
        var bau = w.Wegebau;
        var waehrung = w.Daten.Allgemein.Waehrung;
        var stueck = bau.Stueck(weg, bau.StueckBei(weg, stelle));
        var text = $"{_stadt.WegName(weg)}\nBelag: {Wegbelag.Anzeigename(stueck.Stufe)}{(stueck.Baustelle ? " (wird gebaut)" : "")}";
        var naechste = stueck.Stufe switch { Belagstufe.Kies => Belagstufe.Pflaster, Belagstufe.Pflaster => (Belagstufe?)null, _ => Belagstufe.Kies };
        if (naechste is { } stufe)
        {
            var (von, bis) = WegAnzeige.Vorwahl(bau, karte.Wege[weg].Laenge, stelle);
            var v = bau.Vorschau(_spieler, weg, von, bis, stufe);
            text += $"\nNächste Stufe: {Wegbelag.Anzeigename(stufe)}\n{Zahlenformat.Ganz(v.Meter)} m: {Zahlenformat.Ganz(v.Kosten)} {waehrung} · {BauAnzeige.BauzeitText(v.BauzeitMinuten)}";
            if (v.Verboten is not null) text += "\n✗ " + v.Verboten;
        }
        else
            text += "\nBesser geht es nicht.";
        text += "\nKlick: Wegtafel";
        _wegTippText.Text = text;
        _wegTipp.Show();
        _wegTippGroesse = _wegTipp.GetCombinedMinimumSize();
        WegTippNachfuehren();
    }

    /// <summary>Der Tooltip folgt der Maus (unten rechts, am Rand nach links bzw. oben).</summary>
    private void WegTippNachfuehren()
    {
        if (_wegTipp is not { Visible: true }) return;
        _wegTipp.Position = Tooltips.Lage(GetViewport().GetMousePosition(), _wegTippGroesse, GetViewport().GetVisibleRect().Size);
    }
}
