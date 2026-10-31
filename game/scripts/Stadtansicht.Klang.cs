using Godot;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Was die Umgebungsklänge (<see cref="KlangUmgebung"/>, Ton Schritt 4) von der Stadtansicht lesen – ein Teil der <c>partial class Stadtansicht</c>.
/// Nur Getter (die Darstellung bleibt unberührt), dazu ein Setzer des Zooms für die Hörprobe <c>--umgebungfahrt=1</c>.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Kameragröße (sichtbare Bildhöhe in Metern, 25–140), auch mit freier Kamera (deren Größe folgt ihr ungefähr).</summary>
    public float KlangZoom => Mathf.Clamp(_kamera.Size, KameraMinZoom, KameraMaxZoom);

    /// <summary>Der wirkliche Höchstzoom dieser Karte und dieses Seitenverhältnisses (<see cref="KameraGroessterZoom"/>).</summary>
    public float KlangZoomGroesst => KameraGroessterZoom();

    /// <summary>Die Bezugsstunde des Lichts (5 = Dämmerung beginnt … 22 = Nacht, je Jahreszeit gestreckt, Stadtansicht.Licht.cs).</summary>
    public float KlangBezugStunde => _bezugStunde;

    /// <summary>Die gezeigte Uhr in Minuten mit Bruchteil (berücksichtigt <c>--lichtuhr</c>); ohne Partie 12 Uhr.</summary>
    public float KlangUhrMinute => _lichtUhrVorgabe is { } l ? l * 60f
        : _simulation is { } s ? s.Zeit.UhrzeitInMinuten + (float)s.Takt.AngebrocheneMinute : 12 * 60f;

    /// <summary>Die gezeigte Jahreszeit (auch <c>--jahreszeit</c>, <c>--winter</c>).</summary>
    public Jahreszeit KlangJahreszeit => GezeigteJahreszeit(_simulation?.Zeit.Jahreszeit ?? Jahreszeit.Fruehling);

    /// <summary>
    /// Draußen, im Dom (ein Diorama, die Kamera bleibt oben: „innen leicht“) oder in einem Raum. Jede andere Innenansicht zählt als Raum, auch in
    /// den Bildern des Übergangs, bevor der Raum gesetzt ist.
    /// </summary>
    public KlangUmgebungModell.Innenart KlangInnenart =>
        _raum is not null ? KlangUmgebungModell.Innenart.Raum
        : _innen is null ? KlangUmgebungModell.Innenart.Draussen
        : _innen.Kennung == "Dom" ? KlangUmgebungModell.Innenart.InnenLeicht
        : KlangUmgebungModell.Innenart.Raum;

    /// <summary>Nur für die Hörprobe: Zoom fest vorgeben (wie das Mausrad, in den Grenzen der Karte).</summary>
    public void KlangZoomVorgeben(float groesse) => _kamera.Size = Mathf.Clamp(groesse, KameraMinZoom, KameraGroessterZoom());

    // ── Die Menschen der Stadt (KlangStadtlebenModell) ─────────────────────────────────────────────────────────────

    private double _klangMomentZeit = double.NegativeInfinity;

    /// <summary>
    /// Füllt das Momentbild der Menschen für die Umgebungsklänge – gelesen wird nur, was die Ansicht ohnehin kennt: die sichtbaren Passanten
    /// draußen (Gasse, Markt, Tor – nicht Umland, nicht Dom), die Verkäufer bei offenem Markt, die Gäste in den geöffneten Schänken des Kerns
    /// (<c>Einwohnerschaft.Am</c>) und die Betriebe, die gerade arbeiten (Schmied, Steinmetz; Arbeitszeit, Arbeiter da, kein Stillstand, kein Umbau,
    /// keine Sabotage). Ort in Metern, Seite im Bild aus der Kamera. Höchstens zehnmal je Sekunde neu (die Zeitkonstanten der Pegel sind Sekunden);
    /// dazwischen bleibt das Bild stehen. Gibt false zurück, solange es keine Partie mit Einwohnern gibt.
    /// </summary>
    public bool KlangMomentbild(KlangStadtlebenModell.Momentbild m)
    {
        if (_simulation?.Wirtschaft is not { } w) return false;
        var jetzt = Time.GetTicksMsec() / 1000.0;
        if (jetzt - _klangMomentZeit < 0.1) return true;
        _klangMomentZeit = jetzt;
        m.Leeren();
        var kamera = StadtKamera;
        var fokus = FreiAktiv ? kamera.GlobalPosition : _kameraDrehung.GlobalPosition;
        m.FokusX = fokus.X;
        m.FokusZ = fokus.Z;
        var breite = Math.Max(GetViewport().GetVisibleRect().Size.X, 1f);
        float Seite(Vector3 ort) => _raum is not null ? 0f : kamera.UnprojectPosition(ort).X / breite * 2f - 1f;

        if (w.Einwohnerschaft is { IstAktiv: true } ew)
        {
            foreach (var e in ew.Einwohner)
            {
                if (!_passanten.TryGetValue(e.Nummer, out var p) || !p.Knoten.Visible || _imDom.Contains(e.Nummer)) continue;
                var ort = p.Knoten.GlobalPosition;
                var geht = p.Stand is null && p.LaufEnde > p.LaufStart;
                // Wo er ist: unterwegs von oder ins Umland zählt nicht (Felder murmeln nicht), auf dem Platz zählt er als Marktbesucher
                var stand = p.Stand?.Ort;
                if (geht && e.Weg is { } weg && (weg.Von.Ort == Stadtort.Umland || weg.Nach.Ort == Stadtort.Umland)) continue;
                if (stand?.Ort == Stadtort.Umland) continue;
                var q = new KlangStadtlebenModell.Quelle(ort.X, ort.Z, Seite(ort), 1, geht);
                if (stand?.Ort == Stadtort.Markt) m.Marktleute.Add(q);
                else m.Leute.Add(q);
            }
            // Gäste: nur geöffnete Schänken des Kerns (Wirtshaus 8–22 Uhr) mit Einwohnern darin
            foreach (var (betrieb, anzahl) in KlangStadtlebenKern.OffeneSchaenken(w, _simulation.Zeit.UhrzeitInMinuten))
                if (_betriebe.TryGetValue(betrieb, out var ansicht))
                    m.Schenken.Add(new KlangStadtlebenModell.Quelle(ansicht.Mitte.X, ansicht.Mitte.Z, Seite(ansicht.Mitte), anzahl));
        }

        m.MarktOffen = w.IstArbeitszeit(_simulation.Zeit);
        if (m.MarktOffen)
            foreach (var stand in _marktstaende)
                if (stand.Gezeigt is not null && stand.VerkaeuferKnoten is { } v)
                {
                    var ort = v.GlobalPosition;
                    m.Marktleute.Add(new KlangStadtlebenModell.Quelle(ort.X, ort.Z, Seite(ort)));
                }

        foreach (var (betrieb, arbeiter) in KlangStadtlebenKern.ArbeitendeWerkstaetten(w, _simulation.Zeit.UhrzeitInMinuten))
            if (_betriebe.TryGetValue(betrieb, out var ansicht))
                m.Werkstaetten.Add(new KlangStadtlebenModell.Quelle(ansicht.Mitte.X, ansicht.Mitte.Z, Seite(ansicht.Mitte), arbeiter));
        return true;
    }

    /// <summary>Nur für die Hörprobe auf der Spielkarte: den Blickpunkt der Kamera setzen (am Boden).</summary>
    public void KlangFokusVorgeben(Vector3 ort) => _kameraDrehung.Position = new Vector3(ort.X, 0, ort.Z);

    /// <summary>Mitte des Marktplatzes (Hörprobe, Log).</summary>
    public Vector3 KlangMarktMitte => _markt;
}
