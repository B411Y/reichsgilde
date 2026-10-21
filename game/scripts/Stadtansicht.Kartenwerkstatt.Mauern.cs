using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Mauern und Tore in der Kartenwerkstatt – ein Teil der <c>partial class Stadtansicht</c> (Überblick in Stadtansicht.Kartenwerkstatt.cs).
///
/// <b>Zeichnen.</b> Jede Mauer des Entwurfs hat einen Knoten mit dem Pflaster darin und den Modellen des Mauer-Baukastens, gesetzt nach dem
/// <see cref="Mauerplan"/> des Kerns wie im Spiel (Stadtansicht.Mauer.cs) – nach jeder Änderung neu. Gewählt liegt ein goldenes Band auf der
/// Linie, die Tore tragen ihren Namen; mit einem Fehler ist das Band rot.
///
/// <b>Werkzeug „Mauer“.</b> Ecken klicken (1-m-Raster, mit Alt frei); ein Klick auf die erste Ecke schließt die Mauer zum Ring, Doppelklick oder
/// Enter lässt sie offen (wie Köln am Rhein). Die Vorschau zeigt den Plan bis zur Maus: Mauerstücke, Ecktürme und Mauertürme.
/// <b>Werkzeug „Tor“.</b> Ein Klick auf eine Mauer setzt dort ein Tor (<see cref="Kartenentwurf.TorSetzen"/>, es rastet auf die Linie); die
/// Vorschau zeigt die Torburg an der Stelle. Das Werkzeug bleibt, bis Esc oder Rechtsklick.
///
/// <b>Bearbeiten.</b> Eine gewählte Mauer zeigt Griffe an den Ecken (gold) und an den Toren (blau): Ecken ziehen, ein Klick auf die Linie fügt
/// eine Ecke ein (gleich weiterziehbar), Tore gleiten beim Ziehen an der Mauer entlang; Entf löscht den gewählten Griff, ohne Griff die Mauer.
/// </summary>
public partial class Stadtansicht
{
    private Node3D? _wsMauernKnoten;
    private readonly Dictionary<EntwurfMauer, Node3D> _wsMauern = new();

    /// <summary>Vieleck der Tor-Griffe einer Mauer (die Ecken haben −1 wie der Umriss einer Fläche).</summary>
    private const int TorGriffe = -2;

    private static readonly Color Torblau = new(0.45f, 0.75f, 1f);
    private static readonly Color Mauergrau = new(0.82f, 0.78f, 0.7f);

    /// <summary>So breit zeigt die Vorschau eine Mauer, so groß einen Turm und eine Torburg (Meter, wie die Modelle ungefähr).</summary>
    private const float VorschauMauerdicke = 2.4f, VorschauTurm = 5.5f, VorschauEckturm = 7f, VorschauTorBreite = 16, VorschauTorTiefe = 7;

    // ═════════════════════════════════════════════════════════════════════════
    //  Mauern zeichnen
    // ═════════════════════════════════════════════════════════════════════════

    private void MauernAufbauen()
    {
        _wsMauernKnoten = new Node3D { Name = "Mauern" };
        AddChild(_wsMauernKnoten);
        foreach (var m in _entwurf!.Mauern) MauerBauen(m, false);
    }

    /// <summary>Baut den Knoten einer Mauer neu (<paramref name="weg"/>: sie ist gelöscht – nur entfernen): Pflaster, Modelle, Markierung.</summary>
    private void MauerBauen(EntwurfMauer m, bool weg)
    {
        if (_wsMauern.Remove(m, out var alt)) KnotenEntfernen(alt);
        if (weg || _wsMauernKnoten is null || m.Ecken.Count < 2) return;
        var knoten = new Node3D { Name = "Mauer" };
        _wsMauernKnoten.AddChild(knoten);
        _wsMauern[m] = knoten;
        // Pflaster in der Mauer wie im Spiel (eine offene Linie wird mit einer Geraden geschlossen)
        if (m.Ecken.Count >= 3) Vieleck(m.Ecken.Select(V2).ToArray(), Pflaster, 0f, knoten);
        MauerAufbauen(MauerDatei(m.Katalog), m.Plan, knoten, spiel: false);
        MauerMarkieren(m);
        if (BereichVon(m) is { } bereich) _wsBereiche[m] = bereich;
    }

    /// <summary>Das Band auf der Mauerlinie: gold mit den Namen von Mauer und Toren, wenn gewählt, rot bei einem Fehler, sonst keins.</summary>
    private void MauerMarkieren(EntwurfMauer m)
    {
        if (!_wsMauern.TryGetValue(m, out var knoten)) return;
        if (knoten.GetNodeOrNull<Node3D>("Markierung") is { } alt)
        {
            knoten.RemoveChild(alt);
            KnotenEntfernen(alt);
        }
        var gewaehlt = ReferenceEquals(WerkstattGewaehlt, m);
        var fehler = Fehlerhaft(m);
        if (!gewaehlt && !fehler) return;
        var markierung = new Node3D { Name = "Markierung" };
        knoten.AddChild(markierung);
        var farbe = fehler ? Fehlerrot : Auswahlgold;
        var band = DurchsichtigesNetz(BandDreiecke(m.Linie.Select(V2).ToList(), 2.2f, 0.3f), farbe, 0.4f);
        ImmerSichtbar((StandardMaterial3D)band.MaterialOverride);
        markierung.AddChild(band);
        if (!gewaehlt) return;
        var name = Beschriftung($"{m.Bezeichnung} · {(m.Geschlossen ? "geschlossen" : "offen")}", V3(MitteVon(m)) + new Vector3(0, 6, 0), 40, markierung);
        name.Modulate = farbe.Lightened(0.4f);
        foreach (var tor in m.Tore)
        {
            var schild = Beschriftung(tor.Name + (tor.SchliesstNachts ? "" : " (nachts offen)"), V3(tor.Punkt) + new Vector3(0, 18, 0), 36, markierung);
            schild.Modulate = Torblau.Lightened(0.3f);
        }
    }

    /// <summary>Die Mauer unter dem Punkt (Linie höchstens <paramref name="toleranz"/> plus halbe Mauerdicke daneben) – null: keine.</summary>
    private EntwurfMauer? MauerBei(Vector2 p, float toleranz) =>
        _entwurf?.MauerBei(p.X, p.Y, toleranz + VorschauMauerdicke / 2)?.Mauer;

    // ═════════════════════════════════════════════════════════════════════════
    //  Vorschau eines Plans (Zeichnen, Ziehen, Tor setzen)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Ein Mauerplan als Vorschau: Mauerstücke als graues Band, Türme als Quadrate, Torburgen blau.</summary>
    private void PlanVorschau(Mauerplan plan)
    {
        var mauer = new List<Vector3>();
        foreach (var s in plan.Stuecke) Rechteck(mauer, s, (float)s.Laenge, VorschauMauerdicke);
        VorschauDreiecke(mauer.ToArray(), new Color(Mauergrau, 0.75f));
        var tuerme = new List<Vector3>();
        foreach (var t in plan.Tuerme) Rechteck(tuerme, t, VorschauTurm, VorschauTurm);
        foreach (var t in plan.Ecktuerme) Rechteck(tuerme, t, VorschauEckturm, VorschauEckturm);
        VorschauDreiecke(tuerme.ToArray(), new Color(0.45f, 0.4f, 0.36f, 0.9f));
        var tore = new List<Vector3>();
        foreach (var t in plan.Tore) Rechteck(tore, t.Lage, VorschauTorBreite, VorschauTorTiefe);
        VorschauDreiecke(tore.ToArray(), new Color(Torblau, 0.75f));
    }

    /// <summary>Ein Rechteck an einer Mauerlage: <paramref name="laenge"/> entlang der Mauer, <paramref name="tiefe"/> quer dazu.</summary>
    private static void Rechteck(List<Vector3> ecken, Mauerlage l, float laenge, float tiefe)
    {
        var mitte = new Vector2((float)l.X, (float)l.Z);
        var entlang = new Vector2((float)l.EntlangX, (float)l.EntlangZ) * laenge / 2;
        var quer = new Vector2((float)l.FeldX, (float)l.FeldZ) * tiefe / 2;
        Dreieck(ecken, mitte - entlang - quer, mitte + entlang - quer, mitte + entlang + quer, 0.35f);
        Dreieck(ecken, mitte - entlang - quer, mitte + entlang + quer, mitte - entlang + quer, 0.35f);
    }

    private static Mauerplan PlanVon(IEnumerable<Vector2> ecken, bool geschlossen, IEnumerable<(string Name, Vector2 Ort)> tore) =>
        Mauerplan.Von(ecken.Select(p => new double[] { p.X, p.Y }).ToList(), geschlossen, tore.Select(t => (t.Name, (double)t.Ort.X, (double)t.Ort.Y)).ToList());

    /// <summary>Die Vorschau beim Zeichnen einer Mauer: Plan bis zur Maus, die Ecken, die erste mit Ring, wenn ein Klick dort den Ring schließt.</summary>
    private void MauerVorschau(Vector2? maus)
    {
        var punkte = _wsPunkte.ToList();
        var schliesst = false;
        if (maus is { } m)
        {
            schliesst = punkte.Count >= 3 && _kamera.UnprojectPosition(new Vector3(punkte[0].X, 0, punkte[0].Y)).DistanceTo(GetViewport().GetMousePosition()) <= 10;
            if (!schliesst) punkte.Add(Mauerpunkt(m));
        }
        if (punkte.Count >= 2) PlanVorschau(PlanVon(punkte, schliesst, Array.Empty<(string, Vector2)>()));
        VorschauPunkte(_wsPunkte, Colors.White);
        if (schliesst) FangmarkeZeigen(_wsPunkte[0], Colors.White);
    }

    /// <summary>Eine Ecke der Mauer an der Maus: auf das 1-m-Raster, mit gehaltener Alt-Taste frei.</summary>
    private static Vector2 Mauerpunkt(Vector2 p)
    {
        var (x, z) = Einrasten(p.X, p.Y);
        return new Vector2((float)x, (float)z);
    }

    /// <summary>Legt die gezeichnete Mauer an (ein Schritt) und wählt sie: <paramref name="geschlossen"/> nach einem Klick auf die erste Ecke.</summary>
    private void MauerFertig(bool geschlossen)
    {
        if (_entwurf is not { } e) return;
        if (_wsPunkte.Count < (geschlossen ? 3 : 2))
        {
            WerkstattMeldung?.Invoke(geschlossen ? "Eine geschlossene Mauer braucht mindestens drei Ecken." : "Eine Mauer braucht mindestens zwei Ecken.", true);
            return;
        }
        var ecken = _wsPunkte.Select(p => new Kartenort(p.X, p.Y)).ToList();
        _wsPunkte.Clear();
        if (Befehl(() => e.MauerAnlegen(ecken, geschlossen)) is { } neu)
        {
            WerkstattWaehlen(neu);
            WerkstattMeldung?.Invoke($"{neu.Bezeichnung} angelegt ({(geschlossen ? "geschlossen" : "offen")}, {neu.Plan.AlleTuerme.Count()} Türme) – Tore setzt das Werkzeug „Tor“.", false);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Werkzeug „Tor“
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Wie nah (Bildpunkte) der Klick an der Mauerlinie liegen muss, damit dort ein Tor entsteht – mindestens 3 m.</summary>
    private float TorToleranz => Mathf.Max(3, MeterJePixel * 14);

    /// <summary>Die Vorschau des Tor-Werkzeugs: die Torburg an der nächsten Stelle der Mauer unter der Maus (blau), sonst nichts.</summary>
    private void TorVorschau(Vector2? maus)
    {
        if (maus is not { } m || _entwurf?.MauerBei(m.X, m.Y, TorToleranz) is not { } fang) return;
        var (a, b) = fang.Mauer.Strecke(fang.Strecke);
        var entlang = new Vector2((float)(b.X - a.X), (float)(b.Z - a.Z)).Normalized();
        var lage = new Mauerlage(fang.Ort.X, fang.Ort.Z, entlang.X, entlang.Y);
        var ecken = new List<Vector3>();
        Rechteck(ecken, lage, VorschauTorBreite, VorschauTorTiefe);
        VorschauDreiecke(ecken.ToArray(), new Color(Torblau, 0.7f));
        FangmarkeZeigen(V2(fang.Ort), Torblau);
    }

    /// <summary>Ein Klick mit dem Tor-Werkzeug: auf der Mauer ein Tor setzen (ein Schritt), die Mauer wählen, den neuen Tor-Griff markieren.</summary>
    private void TorKlick(Vector2? boden)
    {
        if (_entwurf is not { } e || boden is not { } p) return;
        if (e.MauerBei(p.X, p.Y, TorToleranz) is not { } fang)
        {
            WerkstattMeldung?.Invoke(e.Mauern.Count == 0 ? "Ein Tor braucht eine Mauer – erst mit dem Werkzeug „Mauer“ eine zeichnen." : "Ein Tor gehört auf eine Mauer – näher an die Mauerlinie klicken.", true);
            return;
        }
        if (Befehl<int?>(() => e.TorSetzen(fang.Mauer, p.X, p.Y)) is not { } tor) return;
        WerkstattWaehlen(fang.Mauer);
        _wsGriff = new Griff(TorGriffe, tor);
        GriffeBauen();
        WerkstattMeldung?.Invoke($"{fang.Mauer.Tore[tor].Name} gesetzt – ein Weg durch das Tor rastet in seiner Mitte ein.", false);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Ziehen von Ecken und Toren
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Jedes Bild beim Ziehen eines Griffs der Mauer: die vorläufige Lage und der Plan damit als Vorschau (die Mauer selbst ist so lange versteckt).</summary>
    private void MauerZiehenNachfuehren(Teilziehvorgang t, EntwurfMauer m, Vector2 maus)
    {
        if (_wsMauern.TryGetValue(m, out var knoten)) knoten.Visible = false;
        var ecken = m.Ecken.Select(V2).ToList();
        var tore = m.Tore.Select(x => (x.Name, V2(x.Punkt))).ToList();
        if (t.Vieleck == TorGriffe)
        {
            t.Ort = V2(m.NaechsteStelle(new Kartenort(maus.X, maus.Y)).Ort);
            tore[t.Index] = (tore[t.Index].Name, t.Ort);
        }
        else
        {
            t.Ort = Mauerpunkt(maus);
            if (t.Art == Ziehart.Griff) ecken[t.Index] = t.Ort;
            else ecken.Insert(t.Index, t.Ort);
        }
        PlanVorschau(PlanVon(ecken, m.Geschlossen, tore));
        VorschauPunkte(new[] { t.Ort }, Colors.White);
    }

    /// <summary>Loslassen eines Griffs der Mauer: Ecke verschieben bzw. einfügen, Tor verschieben (ein Schritt) – gibt den Griff zurück, der gewählt bleibt.</summary>
    private Griff? MauerZiehenBeenden(Teilziehvorgang t, EntwurfMauer m)
    {
        var e = _entwurf!;
        var (x, z) = ((double)t.Ort.X, (double)t.Ort.Y);
        if (t.Vieleck == TorGriffe)
        {
            if (t.Aktiv) Befehl(() => e.TorVerschieben(m, t.Index, x, z));
            return new Griff(TorGriffe, t.Index);
        }
        if (t.Art == Ziehart.Griff)
        {
            if (t.Aktiv) Befehl(() => e.MauerEckeVerschieben(m, t.Index, x, z));
            return new Griff(-1, t.Index);
        }
        if (t.Aktiv) Befehl(() => e.MauerEckeEinfuegen(m, t.Index, x, z));
        var ecke = t.Aktiv ? t.Index : Befehl<int?>(() => e.MauerEckeEinfuegenBei(m, t.Anfang.X, t.Anfang.Y)) ?? -1;
        return ecke >= 0 ? new Griff(-1, ecke) : null;
    }

    /// <summary>Entf auf einem Griff der Mauer: das Tor bzw. die Ecke löschen (eine Mauer behält mindestens zwei, geschlossen drei Ecken).</summary>
    private bool MauerGriffLoeschen(EntwurfMauer m, Griff griff)
    {
        var e = _entwurf!;
        if (griff.Vieleck == TorGriffe)
        {
            Befehl(() => e.TorLoeschen(m, griff.Index));
            return true;
        }
        if (Befehl(() => e.MauerEckeLoeschen(m, griff.Index))) return true;
        WerkstattMeldung?.Invoke($"Eine {(m.Geschlossen ? "geschlossene Mauer braucht mindestens drei" : "Mauer braucht mindestens zwei")} Ecken – Entf ohne gewählten Griff löscht die ganze Mauer.", true);
        return false;
    }
}
