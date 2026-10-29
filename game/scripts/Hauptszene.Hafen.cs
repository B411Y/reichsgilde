using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Hafen und Schiffe in der Oberfläche (PR #199) – ein Teil der <c>partial class Hauptszene</c>: Ein Klick auf den Hafen (auch Kran und Lagerschuppen) oder
/// ein eigenes Schiff öffnet rechts die <see cref="HafenAnzeige"/>, ein fremdes Schiff zeigt in der Auswahl Besitzer, Zustand und Ladung. In der Betriebsleiste
/// steht ein Knopf „Hafen“ (ein Knopf für alle Schiffe, nicht einer je Schiff: Schiffe sind wenige, und alles zu ihnen steht in der Hafentafel).
/// Meldungen: Überfälle auf eigene Schiffe sind Warnungen; Meldungen zu Schiffen anderer Häuser stehen nur im Protokoll (wie fremde Stillstände, PR #198).
/// </summary>
public partial class Hauptszene
{
    private Button? _leisteHafen;
    private Label? _leisteHafenZustand;

    /// <summary>Hafen, Kran, Schuppen („hafen:…“) oder ein eigenes Schiff („schiff:…“) angeklickt: die Hafentafel. False, wenn die Kennung keins davon ist.</summary>
    private bool HafenAngeklickt(string kennung)
    {
        if (_simulation.Wirtschaft is not { } w) return false;
        if (Stadtansicht.HafenAusKennung(kennung) is { } hafen)
        {
            HafenOeffnen(hafen, null, hinfahren: false);
            return true;
        }
        if (Stadtansicht.SchiffName(kennung) is { } name && w.Schiffahrt.Schiff(name) is { } schiff && schiff.Besitzer == _spieler)
        {
            HafenOeffnen(schiff.Hafen, schiff.Name, hinfahren: false);
            return true;
        }
        return false; // fremdes Schiff: Auswahl wie bei Gebäuden und Einwohnern
    }

    /// <summary>Die Hafentafel rechts (schließt die übrigen Tafeln rechts; der Baumodus endet).</summary>
    private void HafenOeffnen(string hafen, string? schiff, bool hinfahren, HafenAnzeige.Reiter reiter = HafenAnzeige.Reiter.Schiffe)
    {
        if (_bau.Aktiv) _bau.Schliessen();
        RechtsSchliessen();
        _hafenTafel.Zeigen(hafen, schiff, reiter);
        if (hinfahren) _stadt.Hinfahren(Stadtansicht.HafenKennung(hafen));
    }

    /// <summary>Der Hafen, den der Knopf der Leiste öffnet: der des ersten eigenen Schiffs, sonst der erste der Karte.</summary>
    private string? HafenDesSpielers(Stadtwirtschaft w) =>
        (_spieler is null ? null : w.Schiffahrt.SchiffeVon(_spieler).FirstOrDefault()?.Hafen) ?? w.Schiffahrt.Haefen.FirstOrDefault()?.Kennung;

    /// <summary>Text der Auswahl für das Schiff eines anderen Hauses.</summary>
    private string FremdesSchiffText(Schiff s, Stadtwirtschaft w)
    {
        var zeilen = new List<string>
        {
            s.Name,
            $"{s.Typ} von {s.Besitzer.Inhaber}",
            w.Schiffahrt.ZustandText(s),
        };
        if (s.Geladen > 0) zeilen.Add("Ladung: " + string.Join(", ", s.Ladung.Where(x => x.Value > 0).Select(x => $"{x.Value} {x.Key}")));
        zeilen.Add($"Geleit: {s.Geleit}");
        return string.Join("\n", zeilen);
    }

    /// <summary>Überfälle der Flusspiraten auf eigene Schiffe werden zur Warnung (sie bleiben unten links stehen, bis man sie anklickt).</summary>
    private string SchiffsMeldung(string meldung)
    {
        if (_simulation.Wirtschaft is not { Schiffahrt: { Daten: { } daten } schiffahrt } || _spieler is null) return meldung;
        if (!meldung.StartsWith(daten.Piraten.Name + " überfallen ")) return meldung;
        return schiffahrt.SchiffeVon(_spieler).Any(s => meldung.Contains($"{s.Name} ({_spieler.Inhaber})")) ? "⚠ " + meldung : meldung;
    }

    /// <summary>
    /// Eine Meldung zum Schiff eines anderen Hauses (Kauf, Auftrag und Geleit der KI, Ankunft in der fernen Stadt, Heimkehr, Piraten) oder der Kauf eines
    /// eigenen (den meldet schon der Befehl)? Sie steht nur im Protokoll des Jahres, nicht unten links. Der Kern nennt das Schiff mit Besitzer („Möwe (Haus Eisenbart)“) bzw. beim
    /// Kauf mit Namen in Anführungszeichen.
    /// </summary>
    private bool IstFremdeSchiffsmeldung(string meldung)
    {
        if (_simulation?.Wirtschaft is not { } w || w.Schiffahrt.Schiffe.Count == 0) return false;
        foreach (var s in w.Schiffahrt.Schiffe)
        {
            // Den Kauf melden Stadt und Befehl: das eigene Schiff nur einmal (die Antwort des Befehls nennt Preis und Unterhalt)
            if (meldung.StartsWith(s.Besitzer.Inhaber + " kauft ") && meldung.Contains($"„{s.Name}“")) return true;
            if (s.Besitzer == _spieler) continue;
            if (meldung.Contains($"{s.Name} ({s.Besitzer.Inhaber})")) return true;
            if (meldung.StartsWith(s.Besitzer.Inhaber + ": ") && meldung.Contains(s.Name)) return true; // Züge der KI: „Haus Brendel: schickt Möwe nach …“
        }
        return false;
    }

    /// <summary>Der Knopf „Hafen“ in der Betriebsleiste (vor „+ Bauen“): nur, wenn die Stadt Schiffshandel hat. Klick: hinfahren und die Hafentafel.</summary>
    private void HafenKnopfBauen(HBoxContainer reihe)
    {
        var knopf = new Button { ToggleMode = true, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(150, 48), ClipContents = true, Visible = false,
                                 SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        reihe.AddChild(knopf);
        var spalte = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        spalte.AddThemeConstantOverride("separation", 0);
        spalte.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, 6);
        knopf.AddChild(spalte);
        Hauptmenue.Text(spalte, "Hafen", 13).MouseFilter = Control.MouseFilterEnum.Ignore;
        _leisteHafenZustand = Hauptmenue.Text(spalte, "", 12, new Color(0.75f, 0.75f, 0.75f));
        _leisteHafenZustand.MouseFilter = Control.MouseFilterEnum.Ignore;
        _leisteHafenZustand.ClipText = true;
        knopf.Pressed += () =>
        {
            if (_simulation.Wirtschaft is not { } w || HafenDesSpielers(w) is not { } hafen) return;
            if (_hafenTafel.Visible) _hafenTafel.Hide();
            else HafenOeffnen(hafen, null, hinfahren: true);
        };
        _leisteHafen = knopf;
    }

    /// <summary>Viermal je Sekunde (mit der Leiste): sichtbar, gedrückt, Zustand der eigenen Schiffe.</summary>
    private void HafenKnopfAnzeigen(Stadtwirtschaft w)
    {
        if (_leisteHafen is not { } knopf || _leisteHafenZustand is not { } zustand) return;
        var sichtbar = w.Schiffahrt.IstAktiv;
        if (knopf.Visible != sichtbar) knopf.Visible = sichtbar;
        if (!sichtbar || _spieler is null) return;
        if (knopf.ButtonPressed != _hafenTafel.Visible) knopf.SetPressedNoSignal(_hafenTafel.Visible);
        var schiffe = w.Schiffahrt.SchiffeVon(_spieler).ToList();
        var unterwegs = schiffe.Count(s => s.IstUnterwegs);
        var text = schiffe.Count == 0 ? "kein Schiff" : unterwegs == 0 ? $"{schiffe.Count} am Kai" : $"{unterwegs} von {schiffe.Count} unterwegs";
        if (zustand.Text != text) zustand.Text = text;
        var tipp = "Hafen: eigene Schiffe, Aufträge und die Preise der fernen Städte\n" +
                   (schiffe.Count == 0 ? "Noch kein Schiff – am Hafen zu kaufen." : string.Join("\n", schiffe.Select(s => $"{s.Name}: {w.Schiffahrt.ZustandText(s)}")));
        if (knopf.TooltipText != tipp) knopf.TooltipText = tipp;
    }

    /// <summary>
    /// Startoptionen für Klicktests: <c>--schiff=N</c> kauft am ersten Hafen N Flussschiffe, <c>--schiffsauftrag=vorschlag</c> gibt jedem eigenen Schiff am Kai
    /// den Vorschlag (<c>=&lt;Ziel&gt;</c>: den für diese ferne Stadt), <c>--hafen=1</c> öffnet die Hafentafel (<c>=&lt;Kennung&gt;</c> eines Hafens,
    /// <c>=staedte</c>: Reiter „Ferne Städte“). Alles mit den gewöhnlichen Befehlen; die Meldung steht im Protokoll.
    /// </summary>
    private void HafenStartoptionen(Dictionary<string, string> optionen)
    {
        if (_simulation.Wirtschaft is not { } w || _spieler is null) return;
        if (optionen.TryGetValue("schiff", out var anzahl) && int.TryParse(anzahl, out var n))
            for (var i = 0; i < n; i++)
            {
                var ergebnis = _simulation.Bote.SofortAusfuehren(_spieler, new SchiffKaufen());
                Protokollieren(ergebnis.Erfolg ? ergebnis.Meldung : "✗ --schiff: " + ergebnis.Meldung);
                if (!ergebnis.Erfolg) break;
            }
        if (optionen.TryGetValue("schiffsauftrag", out var auftrag))
        {
            var ziel = auftrag.Equals("vorschlag", StringComparison.OrdinalIgnoreCase) || auftrag == "1" ? null : auftrag.Replace('_', ' ');
            foreach (var schiff in w.Schiffahrt.SchiffeVon(_spieler).Where(s => s.Zustand == SchiffZustand.AmKai).ToList())
            {
                if (Kalkulation.BesteSchiffsfahrt(w, _spieler, schiff.Daten, _spieler.Vermoegen, schiff.Geleit, ziel: ziel) is not { } v)
                {
                    Protokollieren($"✗ --schiffsauftrag={auftrag}: Keine Fahrt {(ziel is null ? "" : $"nach {ziel} ")}bringt zu heutigen Preisen Gewinn.");
                    continue;
                }
                _simulation.Bote.Einreihen(_spieler, new SchiffsauftragErteilen(schiff.Name, v.Ziel, v.Hinfracht, v.Rueckfracht), ergebnis =>
                    Protokollieren(ergebnis.Erfolg ? ergebnis.Meldung : "✗ --schiffsauftrag: " + ergebnis.Meldung));
            }
        }
        if (optionen.TryGetValue("hafen", out var hafen) && hafen != "0")
        {
            var reiter = hafen.Equals("staedte", StringComparison.OrdinalIgnoreCase) ? HafenAnzeige.Reiter.Staedte : HafenAnzeige.Reiter.Schiffe;
            var kennung = hafen is "1" || reiter == HafenAnzeige.Reiter.Staedte ? HafenDesSpielers(w) : hafen;
            if (kennung is not null && w.Schiffahrt.Hafen(kennung) is not null) HafenOeffnen(kennung, null, hinfahren: !optionen.ContainsKey("blick"), reiter);
            else Protokollieren($"--hafen={hafen}: Diesen Hafen gibt es nicht (Häfen: {string.Join(", ", w.Schiffahrt.Haefen.Select(h => h.Kennung))}).");
        }
    }
}
