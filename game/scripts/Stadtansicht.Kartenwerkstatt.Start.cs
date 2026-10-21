using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Die Startbelegung in der Kartenwerkstatt – ein Teil der <c>partial class Stadtansicht</c> (Überblick in Stadtansicht.Kartenwerkstatt.cs; die
/// Tafel „Start“ steht in Kartenwerkstatt.Start.cs).
///
/// <b>Zeigen.</b> Wer einen Platz am Start nutzt (<see cref="Kartenentwurf.Belegung"/>), sieht man am Stück: Das Modell trägt die Farbe des Hauses
/// (Dach, Fahne), ein Werkstattplatz mit Startbetrieb zeigt die Werkstatt dieses Berufs, und an der vorderen linken Ecke steht eine hohe Fahne
/// in der Hausfarbe – beim Spieler gold mit Schwalbenschwanz (ein Rahmen wäre mit Auswahl und Fehlern zu verwechseln). Unter der Maus (oder mit
/// Alt) steht unter dem Namen, wem der Platz gehört. Ändert sich die Belegung, werden nur die Stücke neu gebaut, deren Anzeige sich ändert.
///
/// <b>Platz wählen.</b> Die Tafel „Start“ fragt mit <see cref="WerkstattPlatzWaehlen"/> nach einem Platz (Wohnhaus, Betrieb, Startplatz): Die
/// passenden Plätze tragen einen grünen Rahmen, unter der Maus grün oder rot mit dem Grund; ein Klick auf einen passenden übergibt ihn, Esc oder
/// Rechtsklick bricht ab.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>
    /// Wie ein belegter Platz aussieht: Farbe der Fahne (Spieler gold), Farbe des Modells (die Hausfarbe, beim Spieler seine Farbe, sonst gold),
    /// Text unter dem Namen, der Beruf, dessen Werkstatt ein Werkstattplatz zeigt (null: die des Schmieds), und ob es der Spieler ist.
    /// </summary>
    private readonly record struct Belegungsanzeige(Color Farbe, Color Modellfarbe, string Text, string? Beruf, bool Spieler = false);

    private static readonly Color Mastholz = new(0.32f, 0.24f, 0.16f);

    /// <summary>Hat sich die Startbelegung geändert? (Am Ende des Bildes werden die betroffenen Stücke neu gebaut.)</summary>
    private bool _wsBelegungNeu;

    /// <summary>Eine Farbe aus der Karte („#rrggbb“) – null, wenn keine oder unlesbar.</summary>
    private static Color? Farbe(string? text) => text is { Length: > 0 } && Color.HtmlIsValid(text) ? Color.FromHtml(text) : null;

    /// <summary>Wie der Platz am Start belegt ist (null: frei). Mehrere Belegungen – ein Fehler der Prüfung – zeigt der Text an.</summary>
    private Belegungsanzeige? BelegungsanzeigeVon(EntwurfGebaeude g)
    {
        if (_entwurf is not { } e || e.BelegungVon(g.Kennung) is not [var b, ..] liste) return null;
        var start = e.Start;
        Belegungsanzeige anzeige;
        if (b.IstSpieler)
        {
            var text = b.Art switch
            {
                Belegungsart.Wohnhaus => "Spieler · Wohnhaus",
                _ when b.Beruf is { } beruf => $"Spieler · Startplatz {beruf}",
                _ => "Spieler · Startplatz",
            };
            anzeige = new Belegungsanzeige(Auswahlgold, Farbe(start.SpielerFarbe) ?? Auswahlgold, text, b.Beruf, Spieler: true);
        }
        else
        {
            var haus = start.Haeuser[b.Haus];
            var farbe = Farbe(haus.Farbe) ?? new Color(0.6f, 0.6f, 0.6f);
            if (b.Art == Belegungsart.Betrieb && b.Betrieb >= 0 && b.Betrieb < haus.Betriebe.Count)
            {
                var art = haus.Betriebe[b.Betrieb].Art;
                var daten = e.Daten is { } d && d.KenntArt(art) ? d.Art(art) : null;
                anzeige = new Belegungsanzeige(farbe, farbe, $"{haus.Name} · {daten?.Name ?? art.ToString()}", daten is { InDerStadt: true } ? daten.Beruf : null);
            }
            else anzeige = new Belegungsanzeige(farbe, farbe, $"{haus.Name} · Wohnhaus", null);
        }
        return liste.Count > 1 ? anzeige with { Farbe = Fehlerrot, Text = anzeige.Text + $" (und {liste.Count - 1} weitere – doppelt vergeben)" } : anzeige;
    }

    /// <summary>Die Fahne der Belegung an der vorderen linken Ecke der Grundfläche: Mast und Tuch in der Hausfarbe, beim Spieler mit Schwalbenschwanz.</summary>
    private void BelegungsfahneBauen(Vector2 groesse, Belegungsanzeige b, Node3D wurzel)
    {
        const float hoehe = 11;
        var fahne = new Node3D { Name = "Belegung", Position = new Vector3(-groesse.X / 2 + 0.6f, 0, groesse.Y / 2 - 0.6f) };
        wurzel.AddChild(fahne);
        fahne.AddChild(new MeshInstance3D
        {
            Name = "Mast", Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.16f, Height = hoehe, RadialSegments = 8 },
            Position = new Vector3(0, hoehe / 2, 0), MaterialOverride = Material(Mastholz),
        });
        FahnenTuchAmMast(new Vector3(0, hoehe - 1.5f, 0), Vector3.Right, 4.5f, 2.8f, b.Farbe, fahne, kerbe: b.Spieler ? 1.4f : 0);
    }

    /// <summary>Am Ende des Bildes nach einer Änderung der Startbelegung: jedes Stück, dessen Anzeige sich ändert, neu bauen.</summary>
    private void BelegungNachziehen()
    {
        _wsBelegungNeu = false;
        if (_entwurf is null) return;
        foreach (var g in _entwurf.Gebaeude)
        {
            if (!_stuecke.TryGetValue(g, out var s) || Equals(s.Belegung, BelegungsanzeigeVon(g))) continue;
            StueckEntfernen(g);
            StueckBauen(g);
            if (ReferenceEquals(WerkstattGewaehlt, g)) Auswahl = StueckKennung(g);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Einen Platz wählen (Werkzeug „Platz“)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Wonach gefragt wird: Hinweis, Prüfung je Gebäude (null: passt, sonst der Grund) und was mit dem gewählten geschieht.</summary>
    private (string Hinweis, Func<EntwurfGebaeude, string?> Pruefen, Action<EntwurfGebaeude> Gewaehlt)? _wsPlatzwahl;

    /// <summary>Was die Platzwahl gerade fragt (für die Tastenhilfe) – null: keine.</summary>
    public string? WerkstattPlatzHinweis => _wsPlatzwahl?.Hinweis;

    /// <summary>
    /// Fragt nach einem Platz in der Karte (Werkzeug „Platz“): <paramref name="pruefen"/> sagt je Gebäude, ob es passt (null) oder warum nicht;
    /// ein Klick auf ein passendes ruft <paramref name="gewaehlt"/> und beendet das Werkzeug. Esc oder Rechtsklick brechen ab.
    /// </summary>
    public void WerkstattPlatzWaehlen(string hinweis, Func<EntwurfGebaeude, string?> pruefen, Action<EntwurfGebaeude> gewaehlt)
    {
        WerkstattWerkzeugWaehlen(Werkstattwerkzeug.Platz);
        _wsPlatzwahl = (hinweis, pruefen, gewaehlt);
        WerkstattWerkzeugGeaendert?.Invoke(); // die Tastenhilfe zeigt jetzt den Hinweis
        WerkstattMeldung?.Invoke(hinweis, false);
    }

    private void PlatzwahlAus()
    {
        _wsPlatzwahl = null;
        _wsPlatzText?.Hide();
    }

    private Label3D? _wsPlatzText;

    /// <summary>Das Gebäude unter der Maus: das getroffene Modell, sonst die Grundfläche unter dem Boden der Maus.</summary>
    private EntwurfGebaeude? GebaeudeUnter(Vector2 maus, Vector2? boden) => StueckTreffer(maus)?.Gebaeude ?? (boden is { } p ? GebaeudeAufGrund(p) : null);

    /// <summary>Jedes Bild bei der Platzwahl: die passenden Plätze mit grünem Rahmen, der unter der Maus breiter in Grün oder Rot mit dem Grund.</summary>
    private void PlatzVorschau(Vector2? boden)
    {
        if (_entwurf is null || _wsPlatzwahl is not { } wahl) return;
        var passend = new List<Vector3>();
        foreach (var g in _entwurf.Gebaeude)
            if (g.Groesse is not null && wahl.Pruefen(g) is null) passend.AddRange(BandDreiecke(Geschlossen(g.Ecken().Select(V2)), 0.35f, 0.25f));
        VorschauDreiecke(passend.ToArray(), new Color(0.45f, 0.95f, 0.45f, 0.55f));
        var maus = GetViewport().GetMousePosition();
        if (boden is null || GebaeudeUnter(maus, boden) is not { Groesse: not null } unter)
        {
            _wsPlatzText?.Hide();
            return;
        }
        var grund = wahl.Pruefen(unter);
        var farbe = grund is null ? new Color(0.45f, 0.95f, 0.45f) : Fehlerrot;
        VorschauDreiecke(BandDreiecke(Geschlossen(unter.Ecken().Select(V2)), 0.8f, 0.3f), new Color(farbe, 0.9f));
        if (_wsPlatzText is null)
        {
            _wsPlatzText = Beschriftung("", Vector3.Zero, 40);
            _wsPlatzText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _wsPlatzText.Width = 620;
        }
        _wsPlatzText.Show();
        _wsPlatzText.Position = V3(unter.Punkt, 14);
        _wsPlatzText.Text = grund ?? "Klick wählt";
        _wsPlatzText.Modulate = farbe.Lightened(0.35f);
    }

    /// <summary>Ein Klick bei der Platzwahl: ein passender Platz wird übergeben (das Werkzeug endet), sonst steht der Grund unten.</summary>
    private void PlatzKlick(Vector2 maus, Vector2? boden)
    {
        if (_wsPlatzwahl is not { } wahl) return;
        if (GebaeudeUnter(maus, boden) is not { } g)
        {
            WerkstattMeldung?.Invoke(wahl.Hinweis, true);
            return;
        }
        if (wahl.Pruefen(g) is { } grund)
        {
            WerkstattMeldung?.Invoke($"{g.Name}: {grund}", true);
            return;
        }
        WerkzeugBeenden();
        wahl.Gewaehlt(g);
    }
}
