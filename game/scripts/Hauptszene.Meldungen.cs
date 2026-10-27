using System.Linq;
using Godot;

namespace Reichsgilde;

/// <summary>
/// <b>Meldungen</b> unten links und die <b>Hilfe</b> (PR #197) – ein Teil der <c>partial class Hauptszene</c>.
///
/// Meldungen: die letzten vier, jede mit Uhrzeit; nach 20 Sekunden verblassen sie. Warnungen (ein Befehl ging nicht „✗“, eine Gefahr „⚠“) bleiben
/// stehen, bis man sie anklickt. Ein Klick auf eine Meldung öffnet das Protokoll des Jahres (Reiter im Bericht), dort stehen alle.
///
/// Hilfe: die Steuerung (früher im Kasten oben links) in einem Fenster in der Bildmitte – F1, Eintrag im Spielmenü, beim allerersten Spiel einmal von selbst.
/// </summary>
public partial class Hauptszene
{
    private const int MeldungenSichtbar = 4;
    private const double MeldungSekunden = 20, VerblassenSekunden = 2;

    private VBoxContainer _meldungen = null!;
    private readonly List<(PanelContainer Tafel, bool Warnung, double Alter)> _meldungsZeilen = new();
    /// <summary>Alle Meldungen des laufenden Jahres, die neueste zuerst (Reiter „Protokoll“ im Bericht).</summary>
    private readonly List<(string Zeit, string Text, bool Warnung)> _jahresProtokoll = new();
    private const int ProtokollHoechstens = 1000;

    private PanelContainer _hilfe = null!;
    private Label _hilfeKopf = null!;

    private void MeldungenBauen()
    {
        _meldungen = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End };
        _meldungen.AddThemeConstantOverride("separation", 4);
        _meldungen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft, Control.LayoutPresetMode.KeepSize, 16);
        _meldungen.OffsetBottom = -Rollbereich.LeistenHoehe;
        _meldungen.GrowVertical = Control.GrowDirection.Begin;
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_meldungen);
    }

    /// <summary>Ist es eine Warnung? Abgelehnte Befehle („✗“) und Gefahren („⚠“) bleiben bis zum Klick stehen.</summary>
    private static bool IstWarnung(string meldung) => meldung.StartsWith('✗') || meldung.StartsWith('⚠');

    /// <summary>
    /// Stillstand eines fremden Betriebs („Tischlerei Eichwald: Es fehlt Holz – die Arbeit ruht.“)? Solche Meldungen der Rivalen
    /// füllten sonst die Meldungen unten links; sie stehen nur im Protokoll des Jahres. Eigene Betriebe melden sich weiter.
    /// </summary>
    private bool IstFremderStillstand(string meldung)
    {
        if (!meldung.EndsWith("die Arbeit ruht.") || _simulation?.Wirtschaft is not { } w) return false;
        var name = meldung.Split(':')[0];
        var betrieb = w.Betriebe.FirstOrDefault(b => b.Name == name);
        return betrieb is not null && betrieb.Konto != _spieler;
    }

    private void Protokollieren(string meldung)
    {
        // Ein Befehl meldet sein Ergebnis, die Stadt dieselbe Nachricht für alle – nur einmal zeigen.
        if (_jahresProtokoll.Count > 0 && _jahresProtokoll[0].Text == meldung) return;
        var warnung = IstWarnung(meldung);
        var zeit = _simulation?.Zeit.UhrzeitText ?? "";
        _jahresProtokoll.Insert(0, (zeit, meldung, warnung));
        if (_jahresProtokoll.Count > ProtokollHoechstens) _jahresProtokoll.RemoveAt(_jahresProtokoll.Count - 1);
        if (IstFremderStillstand(meldung) || IstFremdeSchiffsmeldung(meldung) || IstFremdeRatsmeldung(meldung))
            return; // steht nur im Protokoll, nicht unten links (Wunsch des Nutzers; Schiffe: Hauptszene.Hafen.cs, Rat und Ämter: Hauptszene.Rat.cs)

        var tafel = Leistentafel(10, 5);
        tafel.MouseFilter = Control.MouseFilterEnum.Stop;
        tafel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        tafel.TooltipText = warnung ? "Bleibt stehen, bis ihr sie anklickt. Klick: Protokoll des Jahres" : "Klick: Protokoll des Jahres";
        var text = Hauptmenue.Text(tafel, (zeit == "" ? "" : zeit + "  ") + meldung, 13, warnung ? new Color(0.98f, 0.55f, 0.45f) : null, umbruchBreite: 380);
        text.MouseFilter = Control.MouseFilterEnum.Ignore;
        tafel.GuiInput += eingabe =>
        {
            if (eingabe is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
            MeldungEntfernen(tafel);
            BerichtOeffnen(BerichtAnzeige.Reiter.Protokoll);
        };
        _meldungen.AddChild(tafel); // die neueste unten, gleich über der Leiste
        _meldungsZeilen.Add((tafel, warnung, 0));
        // Zu viele: zuerst die älteste gewöhnliche Meldung weg, sind es nur Warnungen, die älteste Warnung
        while (_meldungsZeilen.Count > MeldungenSichtbar)
        {
            var alt = _meldungsZeilen.FindIndex(z => !z.Warnung);
            MeldungEntfernen(_meldungsZeilen[alt >= 0 ? alt : 0].Tafel);
        }
    }

    private void MeldungEntfernen(PanelContainer tafel)
    {
        var index = _meldungsZeilen.FindIndex(z => z.Tafel == tafel);
        if (index < 0) return;
        _meldungsZeilen.RemoveAt(index);
        _meldungen.RemoveChild(tafel);
        tafel.QueueFree();
    }

    /// <summary>Jedes Bild: gewöhnliche Meldungen altern, verblassen nach 20 Sekunden und verschwinden (Echtzeit, auch in der Pause).</summary>
    private void MeldungenAltern(double delta)
    {
        for (var i = _meldungsZeilen.Count - 1; i >= 0; i--)
        {
            var (tafel, warnung, alter) = _meldungsZeilen[i];
            if (warnung) continue;
            alter += delta;
            _meldungsZeilen[i] = (tafel, warnung, alter);
            if (alter >= MeldungSekunden + VerblassenSekunden) MeldungEntfernen(tafel);
            else if (alter > MeldungSekunden) tafel.Modulate = new Color(1, 1, 1, (float)(1 - (alter - MeldungSekunden) / VerblassenSekunden));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Hilfe
    // ═════════════════════════════════════════════════════════════════════════

    private void HilfeBauen()
    {
        var (tafel, spalte) = Hauptmenue.Tafel(620);
        _hilfe = tafel;
        var mitte = Hauptmenue.Mitte();
        GetNode<CanvasLayer>("Oberflaeche").AddChild(mitte);
        mitte.AddChild(tafel);
        Hauptmenue.Text(spalte, "Hilfe · Steuerung", 24, new Color(0.95f, 0.82f, 0.45f), zentriert: true);
        _hilfeKopf = Hauptmenue.Text(spalte, "", 13, new Color(0.75f, 0.75f, 0.75f), zentriert: true, umbruchBreite: 560);
        var raster = new GridContainer { Columns = 2 };
        raster.AddThemeConstantOverride("h_separation", 18);
        raster.AddThemeConstantOverride("v_separation", 4);
        spalte.AddChild(raster);
        foreach (var (taste, was) in new[]
        {
            ("Leertaste", "Pause / weiter"),
            ("1 – 4", "Tempo (5 – 6: Testtempo, orange)"),
            ("Mausrad", "Zoom"),
            ("Rechte Maustaste, W A S D", "Karte verschieben"),
            ("Q / E", "drehen (drinnen: Raum wechseln)"),
            ("Alt (halten)", "alle Namen zeigen"),
            ("Klick", "Gebäude auswählen – rechts erscheint seine Tafel"),
            ("Doppelklick", "hineingehen (Werkstatt, Wohnhaus, Rathaus, Dom)"),
            ("Esc", "einen Schritt zurück, zuletzt das Spielmenü"),
            ("F", "Familienbuch"),
            ("H", "Hinterzimmer"),
            ("R", "Ratskammer (im Baumodus: Umriss drehen)"),
            ("M", "Marktplatz"),
            ("B", "Bauen"),
            ("L", "Lexikon"),
            ("F1", "diese Hilfe"),
            ("F11", "Vollbild"),
        })
        {
            Hauptmenue.Text(raster, taste, 14, new Color(0.95f, 0.82f, 0.45f));
            Hauptmenue.Text(raster, was, 14);
        }
        Hauptmenue.Text(spalte,
            "Oben links steht euer Haus (ein Klick öffnet das Wohnhaus), oben in der Mitte die Zeit. Unten die Betriebsleiste: ein Klick fährt zum Betrieb und " +
            "zeigt seine Kurzübersicht, ein Doppelklick geht hinein – drinnen stellt ihr Produktion, Personal, Lager, Karren und Einrichtung ein. " +
            "Ein Klick auf das Rathaus zeigt Rat, Ämter, Statut und Gericht, ein Klick auf den Markt die Preise. Meldungen stehen unten links; " +
            "ein Klick darauf öffnet das Protokoll des Jahres.", 13, new Color(0.85f, 0.85f, 0.85f), umbruchBreite: 560);
        var zu = Hauptmenue.Knopf(spalte, "Schließen", HilfeSchliessen);
        zu.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        tafel.Hide();
    }

    private void HilfeOeffnen()
    {
        _hilfeKopf.Text = $"1 Spieljahr = {_simulation.Takt.RundenDauerSekunden / 60:0} Min. · {_simulation.Einstellungen.SiegbedingungText} · Schwierigkeit {_simulation.Einstellungen.Schwierigkeit}";
        if (_einstellungen.Tafel.Visible) EinstellungenSchliessen();
        if (_spielmenue.Visible) SpielmenueSchliessen(); // die Pause wie vor dem Menü
        _hilfe.Show();
    }

    private void HilfeSchliessen()
    {
        _hilfe.Hide();
        Fenster.HilfeGesehen = true;
    }
}
