using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Der Hang als eigene Schicht in der Darstellung (Felspinsel P4b) – ein Teil der <c>partial class Stadtansicht</c>. Der Kern (P4a) rechnet die Schicht und
/// meldet jede Änderung des wirksamen Geländes als „Gelände“-Rechteck; die Werkstatt baut dann nur die betroffenen Kacheln neu
/// (<c>GelaendeVormerken</c>, Stadtansicht.Kartenwerkstatt.Gelaende.cs). Hier steht, was P4b dazutut:
/// <list type="bullet">
/// <item><b>Hang-Geist</b>: ein durchscheinendes Gitternetz über dem Gelände an den Rasterpunkten, deren Höhe sich ändern würde
/// (<see cref="Kartenentwurf.FelsHangVorschau"/>) – beim Ziehen am Gizmo (zusammen mit <see cref="Kartenentwurf.FelsVorschau"/>) und beim Stempeln (die
/// Lagen der Geister mit „hang“). Das Netz wird nur neu gebaut, wenn sich der Zug oder die Lage ändert, nicht jedes Bild.</item>
/// <item><b>Tafel-Knöpfe</b>: Hang an/aus für die Auswahl, „Hebung ablösen“ (mit der Vorschau <see cref="Kartenentwurf.FelsHebungVorschau"/> als Text),
/// „Schwebende wählen“ (<see cref="Kartenentwurf.FelsSchwebende"/>).</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Wer gerade den Hang-Geist zeigt (jeder löscht nur seinen eigenen).</summary>
    private enum FelsHangBesitzer
    {
        Keiner,
        Zug,
        Stempel,
    }

    private MeshInstance3D? _felsHangGeist;
    private StandardMaterial3D? _felsHangStoff;
    private FelsHangBesitzer _felsHangBesitzer;
    private object? _felsHangSchluessel;

    /// <summary>Wie viele Rasterpunkte der Geist höchstens zeigt (darüber wird er weggelassen: so große Hänge sind keine Vorschau mehr).</summary>
    private const int FelsHangGeistMax = 20000;

    /// <summary>Wie viele Rasterpunkte der Geist beim letzten Bauen zeigte (für Messung und Protokoll).</summary>
    public int FelsHangGeistPunkte { get; private set; }

    /// <summary>
    /// Zeigt den Geist für diese Punkte (Rasterpunkt und neue Höhe in Metern). Dasselbe <paramref name="schluessel"/> wie zuletzt: nichts neu bauen
    /// (Gleichheit nach <see cref="object.Equals(object?)"/>, bei Records also nach Werten).
    /// </summary>
    private void FelsHangGeistSetzen(FelsHangBesitzer besitzer, object schluessel, Func<IReadOnlyList<(int I, int J, double Hoehe)>> punkte)
    {
        if (_felsHangGeist is { Visible: true } && _felsHangBesitzer == besitzer && Equals(_felsHangSchluessel, schluessel)) return;
        var liste = punkte();
        if (liste.Count == 0 || liste.Count > FelsHangGeistMax)
        {
            FelsHangGeistAus(besitzer);
            return;
        }
        if (_felsHangGeist is null)
        {
            _felsHangGeist = new MeshInstance3D { Name = "FelsHangGeist", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _felsHangStoff = Transparent(new Color(0.95f, 0.78f, 0.30f), 0.42f);
            _felsHangGeist.MaterialOverride = _felsHangStoff;
            AddChild(_felsHangGeist);
        }
        _felsHangGeist.Mesh = FelsHangNetz(liste);
        _felsHangGeist.Visible = true;
        (_felsHangBesitzer, _felsHangSchluessel, FelsHangGeistPunkte) = (besitzer, schluessel, liste.Count);
    }

    /// <summary>Nimmt den Geist weg – aber nur, wenn er <paramref name="besitzer"/> gehört.</summary>
    private void FelsHangGeistAus(FelsHangBesitzer besitzer)
    {
        if (_felsHangGeist is null || _felsHangBesitzer != besitzer) return;
        _felsHangGeist.Visible = false;
        (_felsHangBesitzer, _felsHangSchluessel, FelsHangGeistPunkte) = (FelsHangBesitzer.Keiner, null, 0);
    }

    /// <summary>
    /// Das Netz des Geists: je Zelle (vier Rasterpunkte), an der sich ein Punkt ändert, zwei Dreiecke; die Ecken sind die neue Höhe, sonst die jetzige
    /// des Entwurfs. Ein paar Zentimeter über dem Boden, damit es nicht flimmert.
    /// </summary>
    private ArrayMesh FelsHangNetz(IReadOnlyList<(int I, int J, double Hoehe)> punkte)
    {
        var neu = new Dictionary<(int, int), double>(punkte.Count);
        foreach (var (i, j, h) in punkte) neu[(i, j)] = h;
        var zellen = new SortedSet<(int J, int I)>();
        foreach (var (i, j, _) in punkte)
            foreach (var (di, dj) in new[] { (0, 0), (-1, 0), (0, -1), (-1, -1) }) zellen.Add((j + dj, i + di));
        var roh = _entwurf!.Hoehen;
        double Hoehe(int i, int j) => neu.TryGetValue((i, j), out var h) ? h : roh.Wert(i, j) / 100.0;
        var a = (float)Hoehenraster.Abstand;
        var ecken = new Vector3[zellen.Count * 6];
        var k = 0;
        foreach (var (j, i) in zellen)
        {
            Vector3 P(int pi, int pj) => new(pi * a, (float)Hoehe(pi, pj) + 0.06f, pj * a);
            var (p00, p10, p01, p11) = (P(i, j), P(i + 1, j), P(i, j + 1), P(i + 1, j + 1));
            (ecken[k++], ecken[k++], ecken[k++]) = (p00, p10, p01);
            (ecken[k++], ecken[k++], ecken[k++]) = (p10, p11, p01);
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = ecken;
        var netz = new ArrayMesh();
        netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return netz;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Vorschau beim Ziehen und beim Stempeln
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Beim Ziehen am Gizmo: der Hang der gewählten Stücke mit ihren Vorschau-Lagen (nur wenn einer „hang“ trägt oder eine Hang-Schicht besteht).</summary>
    private void FelsHangZugZeigen(Felsaenderung aenderung, IReadOnlyList<(EntwurfFels Fels, Felslage Lage)> vorschau)
    {
        if (_entwurf is null || !(_entwurf.HatHangschicht || vorschau.Any(v => v.Lage.Hang)))
        {
            FelsHangGeistAus(FelsHangBesitzer.Zug);
            return;
        }
        FelsHangGeistSetzen(FelsHangBesitzer.Zug, aenderung, () => _entwurf.FelsHangVorschau(vorschau));
    }

    /// <summary>Beim Stempeln mit „Hang mitformen“ als Schicht: der Hang, den der Klick heben würde (die Geister mit „hang“).</summary>
    private void FelsHangStempelZeigen(IReadOnlyList<Felslage> lagen, Vector2 maus)
    {
        if (_entwurf is null || !FelsHangFormen || !FelsHangSchicht || lagen.Count == 0 || !lagen.Any(l => Felsschicht.HatHang(l.Form)))
        {
            FelsHangGeistAus(FelsHangBesitzer.Stempel);
            return;
        }
        var schluessel = (FelsStempelStartwert, (int)Math.Round(maus.X * 100), (int)Math.Round(maus.Y * 100), _entwurf.Geschichte.Schritte, FelsGruppe, FelsGroesse, FelsStreuung);
        FelsHangGeistSetzen(FelsHangBesitzer.Stempel, schluessel,
            () => _entwurf.FelsHangVorschau(Array.Empty<(EntwurfFels, Felslage)>(), lagen.Select(l => l with { Hang = Felsschicht.HatHang(l.Form) })));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Befehle der Tafel
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Tafel „Eigener Hang“: schaltet den Hang der gewählten Stücke an oder aus (ein Schritt). Gibt zurück, ob sich etwas änderte.</summary>
    public bool FelsAuswahlHang(bool an)
    {
        if (_entwurf is null || _felsAuswahl.Count == 0) return false;
        var liste = _felsAuswahl.ToList();
        return Befehl(() => _entwurf.FelsHangSetzen(liste, an));
    }

    /// <summary>Tafel „Hebung ablösen“ an den gewählten Stücken (ein Schritt). Gibt zurück, ob sich etwas änderte.</summary>
    public bool FelsAuswahlHebungAbloesen()
    {
        if (_entwurf is null || _felsAuswahl.Count == 0) return false;
        var liste = _felsAuswahl.ToList();
        var ok = Befehl(() => _entwurf.FelsHebungAbloesen(liste));
        if (!ok) WerkstattMeldung?.Invoke("Hier gibt es keine alte Hebung zu lösen – die gewählten Stücke haben keinen Hang oder das Gelände ist schon so.", true);
        return ok;
    }

    /// <summary>Der Hinweistext vor „Hebung ablösen“ (aus <see cref="Kartenentwurf.FelsHebungVorschau"/>); null, wenn nichts zu senken ist.</summary>
    public string? FelsHebungText()
    {
        if (_entwurf is null || _felsAuswahl.Count == 0 || _felsAuswahl.Count > 200) return null; // bei sehr vielen Stücken keine Rechnung beim Aufbau der Tafel
        if (_entwurf.FelsHebungVorschau(_felsAuswahl) is not { } v) return null;
        var rechteck = v.Bereich;
        return $"Löst die alte Hebung: {v.Punkte} Rasterpunkte (2 m) sinken, die tiefste um {v.Tiefste.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("de-DE"))} m, " +
               $"auf etwa {rechteck.Breite:0} × {rechteck.Tiefe:0} m.";
    }

    /// <summary>Wie viele Stücke schweben (Unterkante über dem Boden, etwa weil das Gelände darunter gesenkt wurde).</summary>
    public int FelsSchwebendeAnzahl() => _entwurf?.FelsSchwebende().Count ?? 0;

    /// <summary>
    /// Tafel „Schwebende wählen“: wählt die schwebenden Stücke (die Ringe und der Gizmo stehen dann an ihnen; „Auf den Boden“ setzt sie auf).
    /// Gibt zurück, wie viele es sind (0: nichts geändert).
    /// </summary>
    public int FelsSchwebendeWaehlen()
    {
        if (_entwurf is null) return 0;
        var liste = _entwurf.FelsSchwebende();
        if (liste.Count == 0) return 0;
        FelsMehrfachWaehlen(liste, dazu: false);
        return liste.Count;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Messung (Pinselmessung): ein Zug an einer Gruppe von Wänden, jedes Bild die Hang-Vorschau
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wie ein Zug am Gizmo, ohne Maus: Die gewählten Stücke werden um (<paramref name="dx"/>, <paramref name="dz"/>) verschoben vorgeschaut
    /// (<see cref="Kartenentwurf.FelsVorschau"/> und <see cref="Kartenentwurf.FelsHangVorschau"/>) und der Geist neu gebaut – jedes Mal, auch bei gleichem Zug.
    /// Gibt die Zahl der Hang-Punkte zurück. Nur für die Pinselmessung.
    /// </summary>
    public int MessHangZug(double dx, double dz)
    {
        if (_entwurf is null || _felsAuswahl.Count == 0) return 0;
        var uhr = Pinselmessung.Uhr();
        var vorschau = _entwurf.FelsVorschau(_felsAuswahl.ToList(), new Felsaenderung(DX: dx, DZ: dz));
        Pinselmessung.Dazu("Fels: Zug-Vorschau (Kern, FelsVorschau)", uhr);
        uhr = Pinselmessung.Uhr();
        var punkte = _entwurf.FelsHangVorschau(vorschau);
        Pinselmessung.Dazu("Fels: Hang-Vorschau (Kern, FelsHangVorschau)", uhr);
        uhr = Pinselmessung.Uhr();
        _felsHangSchluessel = null; // jedes Mal neu bauen
        FelsHangGeistSetzen(FelsHangBesitzer.Zug, new object(), () => punkte);
        Pinselmessung.Dazu("Fels: Hang-Geist (Netz bauen)", uhr);
        return punkte.Count;
    }

    /// <summary>Nach der Messung: Geist weg.</summary>
    public void MessHangZugEnde() => FelsHangGeistAus(FelsHangBesitzer.Zug);
}
