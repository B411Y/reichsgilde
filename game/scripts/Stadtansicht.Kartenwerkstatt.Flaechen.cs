using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Flächen in der Kartenwerkstatt – ein Teil der <c>partial class Stadtansicht</c> (Überblick in Stadtansicht.Kartenwerkstatt.cs).
///
/// <b>Zeichnen.</b> Bauzonen wie im Baumodus des Spiels (<see cref="BauzoneZeichnen"/>: Farbe der Lage, Raster, Kennung), Naturflächen halbdurchsichtig
/// grün (Wald) bzw. olivgrün (Streu), ihre Aussparungen mit braunem Rand (gewählt auch gefüllt); jede mit einem Rand – gold, wenn gewählt, rot bei einem Fehler. Die Natur darauf
/// streut Stadtansicht.Kartenwerkstatt.Natur.cs.
///
/// <b>Werkzeuge „Bauzone“, „Waldfläche“, „Streufläche“, „Aussparung“</b> (und das Zeichnen der Wege, das dieselben Punkte sammelt): Ecken klicken,
/// Doppelklick, Enter oder ein Klick auf die erste Ecke schließt; Esc bricht ab, die Rücktaste nimmt die letzte Ecke weg. Bauzonen rasten auf das
/// Raster der Karte (2 m). Die Aussparung gehört zur gewählten Naturfläche; danach ist wieder die Auswahl dran.
///
/// <b>Kapazität.</b> Auf Wunsch (Klick auf den Kapazitäts-Befund der Prüfung) zeigt die Werkstatt, wie die Kapazitätsprüfung des Kerns die Gebäude
/// einer Partie mit 8 Häusern in die Bauzonen der Stadt setzt (<see cref="WerkstattKapazitaetZeigen"/>): jedes Rasterfeld in der Farbe seines
/// Gebäudes, jedes Gebäude mit Rahmen; bis zur nächsten Änderung.
/// </summary>
public partial class Stadtansicht
{
    private Node3D? _wsFlaechenKnoten;
    private readonly Dictionary<EntwurfFlaeche, Node3D> _wsFlaechen = new();

    /// <summary>Die Lage neuer Bauzonen (Werkzeug „Bauzone“).</summary>
    public Lage WerkstattZonenlage { get; set; } = Lage.Vorstadt;

    /// <summary>Die Punkte, die beim Zeichnen eines Weges oder einer Fläche schon gesetzt sind.</summary>
    private readonly List<Vector2> _wsPunkte = new();

    private static readonly Color Waldgruen = new(0.18f, 0.55f, 0.2f);
    private static readonly Color Feldgold = new(0.9f, 0.74f, 0.25f);
    private static readonly Color Streugruen = new(0.62f, 0.68f, 0.25f);
    private static readonly Color Aussparungsbraun = new(0.62f, 0.38f, 0.18f);

    // ═════════════════════════════════════════════════════════════════════════
    //  Flächen zeichnen
    // ═════════════════════════════════════════════════════════════════════════

    private void FlaechenAufbauen()
    {
        _wsFlaechenKnoten = new Node3D { Name = "Flaechen" };
        AddChild(_wsFlaechenKnoten);
        foreach (var z in _entwurf!.Bauzonen) FlaecheBauen(z, false);
        foreach (var f in _entwurf.Naturflaechen) FlaecheBauen(f, false);
        foreach (var f in _entwurf.Felder) FlaecheBauen(f, false);
    }

    /// <summary>Die Grundfarbe einer Fläche: Bauzone nach ihrer Lage, Wald grün, Streu olivgrün.</summary>
    private static Color FlaechenFarbe(EntwurfFlaeche f) => f switch
    {
        EntwurfBauzone z => LageFarbe(z.Art),
        EntwurfNaturflaeche { Art: "streu" } => Streugruen,
        EntwurfFeld => Feldgold,
        _ => Waldgruen,
    };

    /// <summary>Baut den Knoten einer Fläche neu (<paramref name="weg"/>: sie ist gelöscht – nur entfernen); bei Naturflächen auch die Natur.</summary>
    private void FlaecheBauen(EntwurfFlaeche f, bool weg)
    {
        if (_wsFlaechen.Remove(f, out var alt)) KnotenEntfernen(alt);
        if (f is EntwurfNaturflaeche natur)
        {
            if (weg) NaturEntfernen(natur);
            else _wsNaturNeu.Add(natur);
        }
        if (f is EntwurfFeld feld)
        {
            if (weg) FeldEntfernen(feld);
            else _wsFelderNeu.Add(feld);
        }
        if (weg || _wsFlaechenKnoten is null || f.Ecken.Count < 3) return;
        var knoten = new Node3D { Name = f is EntwurfBauzone ? "Bauzone" : f is EntwurfFeld ? "Feldflaeche" : "Naturflaeche" };
        _wsFlaechenKnoten.AddChild(knoten);
        _wsFlaechen[f] = knoten;
        if (f is EntwurfBauzone zone)
        {
            BauzoneZeichnen(zone.AlsBauzone(), (float)_entwurf!.Raster, knoten); // Stadtansicht.Bauen.cs
            if (BereichVon(zone) is { } bereich) _wsBereiche[zone] = bereich;
        }
        else if (f is not EntwurfFeld) knoten.AddChild(DurchsichtigesNetz(VieleckDreiecke(f.Ecken.Select(V2).ToList(), 0.09f), FlaechenFarbe(f), 0.12f)); // ein Feld zeigt seine Äcker selbst
        FlaecheMarkieren(f);
    }

    /// <summary>Der Rand einer Fläche (und ihrer Aussparungen): in ihrer Farbe, gold und breiter, wenn gewählt, rot bei einem Fehler; gewählt mit Namen.</summary>
    private void FlaecheMarkieren(EntwurfFlaeche f)
    {
        if (!_wsFlaechen.TryGetValue(f, out var knoten)) return;
        if (knoten.GetNodeOrNull<Node3D>("Markierung") is { } alt)
        {
            knoten.RemoveChild(alt);
            KnotenEntfernen(alt);
        }
        var gewaehlt = ReferenceEquals(WerkstattGewaehlt, f);
        var markierung = new Node3D { Name = "Markierung" };
        knoten.AddChild(markierung);
        var farbe = Fehlerhaft(f) ? Fehlerrot : gewaehlt ? Auswahlgold : FlaechenFarbe(f).Lightened(0.2f);
        var halb = gewaehlt ? 0.45f : 0.25f;
        markierung.AddChild(DurchsichtigesNetz(BandDreiecke(Geschlossen(f.Ecken.Select(V2)), halb, 0.16f), farbe, 0.9f));
        foreach (var loch in f.Aussparungen)
        {
            // Aussparungen: nur der Rand – gewählt auch durchscheinend braun gefüllt (die Greybox spart die ganze Stadt aus)
            markierung.AddChild(DurchsichtigesNetz(BandDreiecke(Geschlossen(loch.Select(V2)), halb, 0.16f), Aussparungsbraun.Lightened(0.2f), 0.9f));
            if (gewaehlt) markierung.AddChild(DurchsichtigesNetz(VieleckDreiecke(loch.Select(V2).ToList(), 0.1f), Aussparungsbraun, 0.25f));
        }
        if (gewaehlt && f is EntwurfNaturflaeche natur)
        {
            var name = Beschriftung($"{natur.Bezeichnung} · {(natur.Art == "streu" ? "Streufläche" : "Waldfläche")}", V3(MitteVon(f)) + new Vector3(0, 5, 0), 36, markierung);
            name.Modulate = farbe.Lightened(0.4f);
        }
        else if (gewaehlt && f is EntwurfFeld feld)
        {
            var name = Beschriftung(feld.Name.StartsWith("Feld") ? feld.Name : $"{feld.Bezeichnung} · Feld", V3(MitteVon(f)) + new Vector3(0, 5, 0), 36, markierung);
            name.Modulate = farbe.Lightened(0.4f);
        }
    }

    /// <summary>Die Punkte mit dem ersten noch einmal am Ende (ein geschlossener Rand).</summary>
    private static List<Vector2> Geschlossen(IEnumerable<Vector2> punkte)
    {
        var liste = punkte.ToList();
        if (liste.Count > 0) liste.Add(liste[0]);
        return liste;
    }

    /// <summary>Die Dreiecke eines Vielecks (Vorderseite oben) – leer, wenn es sich nicht zerlegen lässt (etwa, weil es sich selbst schneidet).</summary>
    private Vector3[] VieleckDreiecke(List<Vector2> punkte, float hoehe)
    {
        if (punkte.Count < 3) return Array.Empty<Vector3>();
        var feld = punkte.ToArray();
        var dreiecke = Geometry2D.TriangulatePolygon(feld);
        var ecken = new List<Vector3>();
        for (var i = 0; i + 2 < dreiecke.Length; i += 3) Dreieck(ecken, feld[dreiecke[i]], feld[dreiecke[i + 1]], feld[dreiecke[i + 2]], hoehe);
        return ecken.ToArray();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Finden
    // ═════════════════════════════════════════════════════════════════════════

    private static bool InFlaeche(EntwurfFlaeche f, Vector2 p) => f.Ecken.Count >= 3 && Geometry2D.IsPointInPolygon(p, f.Ecken.Select(V2).ToArray());

    /// <summary>Liegt der Punkt am Rand der Fläche oder einer Aussparung (höchstens <paramref name="toleranz"/> daneben)? Vieleck und Kante (von Ecke i nach i + 1).</summary>
    private static (int Vieleck, int Kante)? AmRand(EntwurfFlaeche f, Vector2 p, float toleranz)
    {
        (int, int)? beste = null;
        var abstand = toleranz;
        for (var v = -1; v < f.Aussparungen.Count; v++)
        {
            var poly = f.Vieleck(v);
            for (var i = 0; i < poly.Count; i++)
            {
                var d = p.DistanceTo(Geometry2D.GetClosestPointToSegment(p, V2(poly[i]), V2(poly[(i + 1) % poly.Count])));
                if (d <= abstand) (beste, abstand) = ((v, i), d);
            }
        }
        return beste;
    }

    /// <summary>Die Fläche unter dem Punkt: Bauzonen vor Naturflächen, die kleinste zuerst – null: keine.</summary>
    private EntwurfFlaeche? FlaecheBei(Vector2 p)
    {
        static float Groesse(EntwurfFlaeche f) => Mathf.Abs(Flaecheninhalt(f.Ecken.Select(V2).ToList()));
        return _entwurf!.Bauzonen.Where(z => InFlaeche(z, p)).OrderBy(Groesse).FirstOrDefault()
            ?? (EntwurfFlaeche?)_entwurf.Naturflaechen.Cast<EntwurfFlaeche>().Concat(_entwurf.Felder).Where(f => InFlaeche(f, p)).OrderBy(Groesse).FirstOrDefault();
    }

    /// <summary>Eine Ecke an der Maus: bei Bauzonen auf dem Raster der Karte, sonst, wo sie ist (der Kern rundet auf 5 cm).</summary>
    private Vector2 Eckpunkt(EntwurfFlaeche? f, Vector2 p) => f is EntwurfBauzone || (f is null && Kartenwerkzeug == Werkstattwerkzeug.Bauzone)
        ? new Vector2((float)Kartenraster.Runden(p.X, _entwurf!.Raster), (float)Kartenraster.Runden(p.Y, _entwurf.Raster))
        : p;

    // ═════════════════════════════════════════════════════════════════════════
    //  Zeichnen von Wegen und Flächen: Punkte sammeln
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ein Klick beim Zeichnen: setzt einen Punkt (Wege rasten ein, Bauzonen auf das Raster der Karte, Mauern auf 1 m). Doppelklick beendet; bei
    /// Flächen und Mauern auch ein Klick auf die erste Ecke (eine Mauer wird dann geschlossen, mit Doppelklick bleibt sie offen).
    /// </summary>
    private void ZeichnenKlick(Vector2 maus, Vector2? boden, bool doppelklick)
    {
        if (doppelklick)
        {
            ZeichnenBeenden();
            return;
        }
        if (boden is not { } b) return;
        var weg = Kartenwerkzeug == Werkstattwerkzeug.Weg;
        if (!weg && _wsPunkte.Count >= 3 && _kamera.UnprojectPosition(AufBoden(new Vector3(_wsPunkte[0].X, 0, _wsPunkte[0].Y))).DistanceTo(maus) <= 10)
        {
            ZeichnenBeenden(ersteEcke: true);
            return;
        }
        // Der Anfang eines Weges rastet ein wie im Kern (Hof, Tor, Halteplatz, Weg), Punkte dazwischen nur in Tore (das Ende rastet beim Anlegen):
        // ein Punkt dazwischen, der neben einer Gasse liegt, bleibt, wo er ist
        var p = Kartenwerkzeug switch
        {
            Werkstattwerkzeug.Weg when _wsPunkte.Count == 0 => Fangen(b) is { } f ? V2(f.Ort) : b,
            Werkstattwerkzeug.Weg => Zwischenpunkt(b) is { } t ? V2(t.Ort) : b,
            Werkstattwerkzeug.Mauer => Mauerpunkt(b),
            _ => Eckpunkt(null, b),
        };
        if (_wsPunkte.Count > 0 && _wsPunkte[^1].DistanceTo(p) < 0.05f) return;
        _wsPunkte.Add(p);
    }

    /// <summary>
    /// Doppelklick, Enter, Klick auf die erste Ecke (<paramref name="ersteEcke"/>): legt den Weg, die Fläche bzw. die Mauer an (ein Schritt; eine
    /// Mauer nur nach dem Klick auf die erste Ecke geschlossen). Zu wenige Punkte: eine Meldung, die Punkte bleiben.
    /// </summary>
    private void ZeichnenBeenden(bool ersteEcke = false)
    {
        if (_entwurf is null) return;
        var werkzeug = Kartenwerkzeug;
        if (werkzeug == Werkstattwerkzeug.Weg)
        {
            if (_wsPunkte.Count < 2) WerkstattMeldung?.Invoke("Ein Weg braucht mindestens zwei Punkte.", true);
            else WegFertig(); // Stadtansicht.Kartenwerkstatt.Wege.cs
            return;
        }
        if (werkzeug == Werkstattwerkzeug.Mauer)
        {
            MauerFertig(geschlossen: ersteEcke); // Stadtansicht.Kartenwerkstatt.Mauern.cs
            return;
        }
        if (_wsPunkte.Count < 3)
        {
            WerkstattMeldung?.Invoke("Eine Fläche braucht mindestens drei Ecken.", true);
            return;
        }
        var ecken = _wsPunkte.Select(p => new Kartenort(p.X, p.Y)).ToList();
        _wsPunkte.Clear();
        var e = _entwurf;
        switch (werkzeug)
        {
            case Werkstattwerkzeug.Bauzone when Befehl(() => e.BauzoneAnlegen(WerkstattZonenlage, ecken)) is { } zone:
                WerkstattWaehlen(zone);
                break;
            case Werkstattwerkzeug.Wald or Werkstattwerkzeug.Streu when Befehl(() => e.NaturflaecheAnlegen(werkzeug == Werkstattwerkzeug.Wald ? "wald" : "streu", ecken)) is { } natur:
                WerkstattWaehlen(natur);
                break;
            case Werkstattwerkzeug.Feld when Befehl(() => e.FeldAnlegen(ecken, null, FeldRichtungVon(ecken))) is { } feld:
                WerkstattWaehlen(feld);
                break;
            case Werkstattwerkzeug.Aussparung when WerkstattGewaehlt is EntwurfFlaeche { KannAussparungen: true } f:
                Befehl(() => e.AussparungAnlegen(f, ecken));
                WerkzeugBeenden(); // die Fläche bleibt gewählt
                break;
        }
    }

    /// <summary>Esc beim Zeichnen: Die gesetzten Punkte verfallen, das Werkzeug bleibt.</summary>
    private void ZeichnenAbbrechen() => _wsPunkte.Clear();

    /// <summary>Die Vorschau beim Zeichnen einer Fläche: das Vieleck bis zur Maus, die Ecken, die erste größer, wenn ein Klick dort schließt.</summary>
    private void FlaechenVorschau(Vector2? maus)
    {
        var punkte = _wsPunkte.ToList();
        var farbe = Kartenwerkzeug switch
        {
            Werkstattwerkzeug.Bauzone => LageFarbe(WerkstattZonenlage),
            Werkstattwerkzeug.Streu => Streugruen,
            Werkstattwerkzeug.Feld => Feldgold,
            Werkstattwerkzeug.Aussparung => Aussparungsbraun,
            _ => Waldgruen,
        };
        var schliesst = false;
        if (maus is { } m)
        {
            schliesst = punkte.Count >= 3 && _kamera.UnprojectPosition(AufBoden(new Vector3(punkte[0].X, 0, punkte[0].Y))).DistanceTo(GetViewport().GetMousePosition()) <= 10;
            if (!schliesst) punkte.Add(Eckpunkt(null, m));
        }
        VorschauVieleck(punkte, farbe, geschlossen: punkte.Count >= 3);
        VorschauPunkte(_wsPunkte, Colors.White);
        if (schliesst) FangmarkeZeigen(_wsPunkte[0], Colors.White);
    }

    /// <summary>Ein Vieleck als Vorschau: Füllung (wenn es sich zerlegen lässt) und Rand.</summary>
    private void VorschauVieleck(List<Vector2> punkte, Color farbe, bool geschlossen)
    {
        if (punkte.Count >= 3) VorschauDreiecke(VieleckDreiecke(punkte, 0.17f), new Color(farbe, 0.3f));
        var rand = geschlossen ? Geschlossen(punkte) : punkte;
        if (rand.Count >= 2) VorschauDreiecke(BandDreiecke(rand, Mathf.Max(0.15f, MeterJePixel * 1.5f), 0.2f), new Color(farbe.Lightened(0.3f), 0.95f));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Kapazität: wo die Prüfung die Gebäude unterbringt
    // ═════════════════════════════════════════════════════════════════════════

    private Node3D? _wsKapazitaet;

    /// <summary>Zeigt die Werkstatt gerade die Belegung der Kapazitätsprüfung?</summary>
    public bool WerkstattKapazitaetSichtbar => _wsKapazitaet is not null;

    /// <summary>
    /// Die Farbe je Gebäudeart der Kapazitätsprüfung (Schlüssel wie <see cref="Kapazitaetspruefung.Groessen"/>, „fest“ = schon auf der Karte) – keine
    /// davon ist eine Farbe der Lagen (Kern orange, Mauerring blau, Vorstadt türkis), damit sie sich auf den Bauzonen abheben.
    /// </summary>
    public static Color KapazitaetsFarbe(string art) => art switch
    {
        "fest" => new Color(0.25f, 0.25f, 0.25f),
        "Werkstatt" => new Color(0.9f, 0.15f, 0.12f),
        "Ratsherrenhaus" => new Color(1f, 0.85f, 0.15f),
        "Einwohner-Bürgerhaus" => new Color(0.97f, 0.97f, 0.95f),
        "Einwohner-Wohnhaus" => new Color(0.5f, 0.95f, 0.25f),
        "Stadthof" => new Color(0.6f, 0.25f, 0.95f),
        _ => new Color(1f, 0.45f, 0.75f), // Stadtpalais
    };

    /// <summary>
    /// Zeigt die Belegung einer Kapazitätsprüfung über der Karte (null: wieder weg): jedes belegte Rasterfeld etwas eingerückt in der Farbe seines
    /// Gebäudes (<see cref="KapazitaetsFarbe"/>), um jedes gesetzte Gebäude ein Rahmen; freie Felder bleiben in der Farbe ihrer Bauzone. Über allem
    /// (ohne Tiefentest), damit Felder unter Häusern zu sehen sind. Die nächste Änderung am Entwurf blendet es aus (es gälte nicht mehr).
    /// </summary>
    public void WerkstattKapazitaetZeigen(Kapazitaet? k)
    {
        if (_wsKapazitaet is not null) KnotenEntfernen(_wsKapazitaet);
        _wsKapazitaet = null;
        if (k is null) return;
        _wsKapazitaet = new Node3D { Name = "Kapazitaet" };
        AddChild(_wsKapazitaet);
        var r = (float)k.Raster;
        const float einzug = 0.15f;
        foreach (var gruppe in k.Plaetze.GroupBy(p => p.Art))
        {
            var felder = new List<Vector3>();
            var rahmen = new List<Vector3>();
            foreach (var platz in gruppe)
            {
                foreach (var (i, j) in platz.Felder)
                {
                    var (a, b) = (new Vector2(i * r + einzug, j * r + einzug), new Vector2((i + 1) * r - einzug, (j + 1) * r - einzug));
                    Dreieck(felder, a, new Vector2(b.X, a.Y), b, 0.22f);
                    Dreieck(felder, a, b, new Vector2(a.X, b.Y), 0.22f);
                }
                if (gruppe.Key == "fest" || platz.Felder.Count == 0) continue;
                var (i0, i1, j0, j1) = (platz.Felder.Min(f => f.I), platz.Felder.Max(f => f.I) + 1, platz.Felder.Min(f => f.J), platz.Felder.Max(f => f.J) + 1);
                rahmen.AddRange(BandDreiecke(new List<Vector2> { new(i0 * r, j0 * r), new(i1 * r, j0 * r), new(i1 * r, j1 * r), new(i0 * r, j1 * r), new(i0 * r, j0 * r) }, 0.2f, 0.24f));
            }
            var farbe = KapazitaetsFarbe(gruppe.Key);
            foreach (var (ecken, alpha, farbton) in new[] { (felder, 0.75f, farbe), (rahmen, 0.95f, farbe.Darkened(0.4f)) })
            {
                if (ecken.Count == 0) continue;
                var netz = DurchsichtigesNetz(ecken.ToArray(), farbton, alpha);
                ImmerSichtbar((StandardMaterial3D)netz.MaterialOverride);
                _wsKapazitaet.AddChild(netz);
            }
        }
    }
}
