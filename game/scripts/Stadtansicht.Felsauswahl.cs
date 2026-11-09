using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Felsstücke wählen und kopieren wie im Unreal-Editor (Fels P2) – ein Teil der <c>partial class Stadtansicht</c>, neben dem Gizmo
/// (Stadtansicht.Felsgizmo.cs):
/// <list type="bullet">
/// <item><b>Auswahlrahmen</b>: Mit dem Werkzeug „Auswahl“ ins Leere drücken und ziehen (ab ein paar Bildpunkten) zieht einen Rahmen auf dem Bildschirm;
/// Loslassen wählt alle Felsstücke, deren Mitte darin liegt – auch verdeckte. Mit Umschalt kommen sie zur Auswahl dazu. Bleibt die Maus stehen, ist es
/// ein Klick wie bisher (Fläche wählen oder Auswahl aufheben). Esc bricht den Rahmen ab. Der Rahmen wählt nur Felsen (Entscheidung des Nutzers, P2).</item>
/// <item><b>Strg+A</b> wählt alle Felsstücke, <b>Strg+I</b> kehrt die Auswahl um; die Tafel wählt dazu alle derselben Form oder desselben Gesteins.</item>
/// <item><b>Strg+D</b> kopiert die gewählten Stücke an Ort und Stelle (<see cref="Kartenentwurf.FelsKopieren"/>, ein Schritt) und wählt die Kopien –
/// der Gizmo steht an ihnen und schiebt sie weg. Strg allein bleibt das Einrasten des Gizmos.</item>
/// <item><b>Q/E</b> drehen die gewählten Stücke wie Gebäude und Pflanzen um die Senkrechte (15°, mit Alt 1°; mehrere um ihre Mitte) – seit die Pfeiltasten
/// dem Gizmo gehören, ist E dafür wieder frei.</item>
/// </list>
/// Viele Stücke auf einmal werden in einem Zug gewählt (<see cref="FelsMehrfachWaehlen"/>): eine Meldung an die Tafel statt einer je Stück.
/// </summary>
public partial class Stadtansicht
{
    // ═════════════════════════════════════════════════════════════════════════
    //  Viele Stücke auf einmal
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wählt viele Felsstücke in einem Zug (Rahmen, alle, Kopien, Startoptionen): mit <paramref name="dazu"/> bleiben die bisher gewählten Stücke; das
    /// letzte der Liste ist das gewählte (die Tafel zeigt seine Werte). Stücke außerhalb des Entwurfs fallen weg; ohne Stücke ist nichts mehr gewählt
    /// (mit <paramref name="dazu"/> bleibt die Auswahl dann, wie sie war).
    /// </summary>
    public void FelsMehrfachWaehlen(IReadOnlyList<EntwurfFels> stuecke, bool dazu)
    {
        if (_entwurf is null) return;
        var imEntwurf = new HashSet<EntwurfFels>(_entwurf.Felsen, ReferenceEqualityComparer.Instance);
        var gesehen = new HashSet<EntwurfFels>(ReferenceEqualityComparer.Instance);
        var liste = new List<EntwurfFels>();
        if (dazu && WerkstattGewaehlt is EntwurfFels)
            foreach (var f in _felsAuswahl)
                if (imEntwurf.Contains(f) && gesehen.Add(f)) liste.Add(f);
        var neu = 0;
        foreach (var f in stuecke)
            if (imEntwurf.Contains(f) && gesehen.Add(f))
            {
                liste.Add(f);
                neu++;
            }
        if (liste.Count == 0 || (dazu && neu == 0))
        {
            if (!dazu) WerkstattWaehlen((EntwurfTeil?)null);
            return;
        }
        var primaer = liste[^1];
        if (!ReferenceEquals(WerkstattGewaehlt, primaer)) WerkstattWaehlen(primaer); // Markierung, Griffe, Tafel; die Auswahl ist danach nur dieses Stück
        _felsAuswahl.Clear();
        _felsAuswahl.AddRange(liste);
        FelsGizmoGeaendert?.Invoke();
    }

    /// <summary>Strg+A: alle Felsstücke wählen (nur, wenn die Ebene „Baum- und Naturzonen“ an ist). Gibt zurück, wie viele es sind.</summary>
    public int FelsAlleWaehlen()
    {
        if (_entwurf is null || !WerkstattEbeneSichtbar(Werkstattebene.Natur)) return 0;
        FelsMehrfachWaehlen(_entwurf.Felsen.ToList(), dazu: false);
        return _felsAuswahl.Count;
    }

    /// <summary>Strg+I: Die gewählten Felsstücke fallen heraus, alle anderen werden gewählt.</summary>
    public int FelsAuswahlUmkehren()
    {
        if (_entwurf is null || !WerkstattEbeneSichtbar(Werkstattebene.Natur)) return 0;
        var gewaehlt = WerkstattGewaehlt is EntwurfFels ? new HashSet<EntwurfFels>(_felsAuswahl, ReferenceEqualityComparer.Instance) : new HashSet<EntwurfFels>();
        FelsMehrfachWaehlen(_entwurf.Felsen.Where(f => !gewaehlt.Contains(f)).ToList(), dazu: false);
        return _felsAuswahl.Count;
    }

    /// <summary>
    /// Tafel: alle Felsstücke derselben Form (<paramref name="nachGestein"/> false) bzw. desselben Gesteins wie die gewählten – das gezeichnete Gestein,
    /// also auch das Grundgestein. Das bisher gewählte Stück bleibt das gewählte.
    /// </summary>
    public int FelsGleicheWaehlen(bool nachGestein)
    {
        if (_entwurf is null || _felsAuswahl.Count == 0 || WerkstattGewaehlt is not EntwurfFels primaer) return 0;
        string Art(EntwurfFels f) => nachGestein ? _entwurf.GesteinVon(f) : f.Form;
        var arten = _felsAuswahl.Select(Art).ToHashSet();
        var liste = _entwurf.Felsen.Where(f => arten.Contains(Art(f)) && !ReferenceEquals(f, primaer)).Append(primaer).ToList();
        FelsMehrfachWaehlen(liste, dazu: false);
        return _felsAuswahl.Count;
    }

    /// <summary>
    /// Strg+D (kopieren), Strg+A (alle) und Strg+I (umkehren) – nur mit dem Werkzeug „Auswahl“ bzw. im Felswerkzeug beim Stempeln, nicht während eines
    /// Zugs. True, wenn die Taste den Felsen gehörte.
    /// </summary>
    private bool FelsAuswahlTaste(Key taste)
    {
        if (_entwurf is null || _gizmoZug is not null || _wsTeilZiehen is not null || _wsZiehen is not null || _felsRahmen is not null) return false;
        var werkzeug = Kartenwerkzeug == Werkstattwerkzeug.Auswahl || (Kartenwerkzeug == Werkstattwerkzeug.Fels && FelsModus == FelsArt.Stempeln);
        if (!werkzeug) return false;
        switch (taste)
        {
            case Key.D:
                if (!FelsGizmoAktiv) return false;
                FelsKopieren();
                return true;
            case Key.A:
                if (_entwurf.Felsen.Count == 0) return false;
                WerkstattMeldung?.Invoke($"{FelsAlleWaehlen()} Felsstücke gewählt.", false);
                return true;
            case Key.I:
                if (_entwurf.Felsen.Count == 0) return false;
                WerkstattMeldung?.Invoke($"Auswahl umgekehrt: {FelsAuswahlUmkehren()} Felsstücke gewählt.", false);
                return true;
        }
        return false;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Kopieren und Drehen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Strg+D: kopiert die gewählten Stücke (um <paramref name="dx"/>/<paramref name="dz"/> Meter versetzt, sonst an Ort und Stelle; ein Schritt) und
    /// wählt die Kopien. False: nichts gewählt oder ein Zug am Gizmo läuft.
    /// </summary>
    public bool FelsKopieren(double dx = 0, double dz = 0)
    {
        if (_entwurf is null || _felsAuswahl.Count == 0 || WerkstattGewaehlt is not EntwurfFels || _gizmoZug is not null) return false;
        var auswahl = _felsAuswahl.ToList();
        var neue = Befehl(() => _entwurf.FelsKopieren(auswahl, dx, dz));
        if (neue is not { Count: > 0 }) return false;
        FelsMehrfachWaehlen(neue, dazu: false);
        WerkstattMeldung?.Invoke((neue.Count == 1 ? "Ein Felsstück kopiert" : $"{neue.Count} Felsstücke kopiert")
            + (dx == 0 && dz == 0 ? " – die Kopien liegen auf den alten und sind gewählt: mit dem Gizmo wegziehen." : "."), false);
        return true;
    }

    /// <summary>Q/E bei mehreren gewählten Stücken: alle um ihre gemeinsame Mitte um die Senkrechte drehen (ein Schritt).</summary>
    private void FelsSenkrechtDrehen(double winkel)
    {
        if (_entwurf is null || _felsAuswahl.Count == 0) return;
        var auswahl = _felsAuswahl.ToList();
        Befehl(() => _entwurf.FelsTransformieren(auswahl, new Felsaenderung(Drehachse: Felsachse.Y, Winkel: winkel, UmMitte: true, Weltachsen: true)));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Auswahlrahmen
    // ═════════════════════════════════════════════════════════════════════════

    private sealed class FelsRahmenZug
    {
        public required Vector2 Start { get; init; }
        public Vector2 Jetzt { get; set; }

        /// <summary>Umschalt beim Drücken: Die Stücke im Rahmen kommen zur Auswahl dazu.</summary>
        public bool Dazu { get; init; }

        /// <summary>Hat sich die Maus weit genug bewegt? Sonst ist es ein einfacher Klick.</summary>
        public bool Aktiv { get; set; }

        /// <summary>Der Boden unter dem Klick (für den einfachen Klick: die Fläche dort wählen).</summary>
        public Vector2? Boden { get; init; }

        public Rect2 Rechteck => new Rect2(Start, Vector2.Zero).Expand(Jetzt);
    }

    private FelsRahmenZug? _felsRahmen;
    private CanvasLayer? _felsRahmenEbene;
    private Panel? _felsRahmenFeld;

    /// <summary>
    /// Ein Klick des Werkzeugs „Auswahl“ ins Leere (nichts zum Ziehen getroffen): Erst beim Loslassen zeigt sich, ob es ein Rahmen war oder ein Klick. Ohne
    /// sichtbare Felsen (Ebene aus) bleibt es beim Klick wie bisher.
    /// </summary>
    private void FelsRahmenOderKlick(Vector2 maus, Vector2? boden, bool dazu)
    {
        if (!WerkstattEbeneSichtbar(Werkstattebene.Natur))
        {
            WerkstattWaehlen(boden is { } p ? FlaecheBei(p) : null);
            return;
        }
        _felsRahmen = new FelsRahmenZug { Start = maus, Jetzt = maus, Dazu = dazu, Boden = boden };
    }

    /// <summary>Mausbewegung: Der Rahmen folgt (ab <see cref="ZiehSchwelle"/> Bildpunkten wird aus dem Klick ein Rahmen).</summary>
    private void FelsRahmenWeiter(Vector2 maus)
    {
        if (_felsRahmen is not { } r) return;
        r.Jetzt = maus;
        if (!r.Aktiv && maus.DistanceTo(r.Start) > ZiehSchwelle) r.Aktiv = true;
        if (r.Aktiv) FelsRahmenZeigen(r.Rechteck);
    }

    /// <summary>Loslassen: wählt die Stücke im Rahmen – oder, wenn die Maus stand, wie ein Klick ins Leere die Fläche unter ihr (bzw. nichts).</summary>
    private void FelsRahmenBeenden()
    {
        if (_felsRahmen is not { } r) return;
        _felsRahmen = null;
        FelsRahmenVerstecken();
        if (!r.Aktiv)
        {
            WerkstattWaehlen(r.Boden is { } p ? FlaecheBei(p) : null);
            return;
        }
        FelsMehrfachWaehlen(FelsenImRahmen(r.Rechteck), r.Dazu);
    }

    /// <summary>Esc beim Aufziehen: kein Rahmen, keine Änderung der Auswahl. False: es lief keiner.</summary>
    private bool FelsRahmenAbbrechen()
    {
        if (_felsRahmen is null) return false;
        _felsRahmen = null;
        FelsRahmenVerstecken();
        return true;
    }

    /// <summary>
    /// Die Felsstücke, deren Mitte (Mitte ihres Kastens in der Welt) auf dem Bildschirm im Rechteck liegt – auch verdeckte, hinter Kuppen oder unter
    /// anderen Stücken (wie in Unreal). In der Reihenfolge des Entwurfs; das letzte wird das gewählte.
    /// </summary>
    private List<EntwurfFels> FelsenImRahmen(Rect2 rechteck)
    {
        var aus = new List<EntwurfFels>();
        if (_entwurf is null) return aus;
        var kamera = StadtKamera;
        foreach (var f in _entwurf.Felsen)
        {
            var lage = _felsLagen.TryGetValue(f, out var l) ? l : FelsLage(FelsBildVon(f));
            var mitte = FelsKasten(f.Form) is { } kasten ? lage * kasten.GetCenter() : lage.Origin;
            if (kamera.IsPositionBehind(mitte)) continue;
            var p = kamera.UnprojectPosition(mitte);
            if (p.X >= rechteck.Position.X && p.X <= rechteck.End.X && p.Y >= rechteck.Position.Y && p.Y <= rechteck.End.Y) aus.Add(f);
        }
        return aus;
    }

    /// <summary>Das Rechteck auf dem Bildschirm: goldener Rand, kaum gefüllt, durchlässig für die Maus.</summary>
    private void FelsRahmenZeigen(Rect2 rechteck)
    {
        if (_felsRahmenFeld is null)
        {
            _felsRahmenEbene = new CanvasLayer { Name = "FelsRahmen", Layer = 0 };
            AddChild(_felsRahmenEbene);
            var stil = new StyleBoxFlat { BgColor = new Color(Auswahlgold, 0.12f), BorderColor = new Color(Auswahlgold, 0.95f) };
            stil.SetBorderWidthAll(2);
            _felsRahmenFeld = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
            _felsRahmenFeld.AddThemeStyleboxOverride("panel", stil);
            _felsRahmenEbene.AddChild(_felsRahmenFeld);
        }
        _felsRahmenFeld.Position = rechteck.Position;
        _felsRahmenFeld.Size = rechteck.Size;
        _felsRahmenEbene!.Visible = true;
    }

    private void FelsRahmenVerstecken()
    {
        if (_felsRahmenEbene is not null) _felsRahmenEbene.Visible = false;
    }

    /// <summary>
    /// Für Startoptionen und Klicktests: ein Rahmen zwischen zwei Orten der Karte (auf den Bildschirm projiziert, wie die Kamera gerade steht) – derselbe
    /// Weg wie mit der Maus. Gibt zurück, wie viele Stücke danach gewählt sind.
    /// </summary>
    public int WerkstattFelsRahmen(Kartenort a, Kartenort b, bool dazu)
    {
        if (_entwurf is null) return 0;
        Vector2 Bild(Kartenort o) => StadtKamera.UnprojectPosition(new Vector3((float)o.X, BodenHoehe((float)o.X, (float)o.Z), (float)o.Z));
        var (p, q) = (Bild(a), Bild(b));
        _felsRahmen = new FelsRahmenZug { Start = p, Jetzt = q, Dazu = dazu, Aktiv = true };
        FelsRahmenBeenden();
        return WerkstattGewaehlt is EntwurfFels ? _felsAuswahl.Count : 0;
    }
}
