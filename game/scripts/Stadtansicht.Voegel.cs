using Godot;

namespace Reichsgilde;

/// <summary>
/// Tauben und Spatzen in Stadt und Umland – Teil der <c>partial class Stadtansicht</c>. Reine Darstellung: Die Vögel kennen die
/// Simulation nicht, sie lesen nur die Uhrzeit und wo Passanten gehen.
/// <para>Jeder Schwarm (3–5 Vögel einer Art) hat ein Heimatgebiet (Stadt oder eine Stelle im Umland) und entscheidet selbst: auf einem
/// Dachfirst sitzen und sich putzen, zum Boden fliegen und picken, zum nächsten Haus weiterfliegen. Kommt ein Passant zu nahe, fliegt der
/// Schwarm auf ein Dach. Nachts (vor 5:30 und nach 21 Uhr) sitzen alle auf den Dächern.</para>
/// <para>Die Landeplätze auf den Dächern rechnet <see cref="DachpunkteSuchen"/> aus den Modellen: die höchsten Ecken jedes Gebäudes (der
/// First). Neu gebaute Häuser kommen beim nächsten Durchlauf dazu.</para>
/// </summary>
public partial class Stadtansicht
{
    private enum Vogelzustand { Sitzt, AmBoden, Fliegt }

    private sealed class Vogel
    {
        public required Node3D Knoten { get; init; }
        public AnimationPlayer? Spieler { get; init; }
        public required bool Taube { get; init; }
        public Vogelzustand Zustand { get; set; } = Vogelzustand.Sitzt;
        public Vector3 Von { get; set; }
        /// <summary>Landeplatz (auch beim Trippeln auf dem First); zählt für die Belegung der Plätze schon während des Flugs.</summary>
        public Vector3 Nach { get; set; }
        /// <summary>Flug: vergangene Sekunden (negativ = wartet noch auf den Abflug); am Boden: Sekunden bis zum nächsten Schritt.</summary>
        public float Zeit { get; set; }
        public float Dauer { get; set; }
        /// <summary>Stützpunkte der Flugbahn (Abflug, Steigen, Scheitel oder Runde, Anflug, Landung); dazwischen eine Catmull-Rom-Kurve.</summary>
        public Vector3[] Bahn { get; set; } = Array.Empty<Vector3>();
        /// <summary>Weg entlang der Bahn bis zu jedem Stützpunkt (Meter), für gleichmäßiges Tempo.</summary>
        public float[] BahnWeg { get; set; } = Array.Empty<float>();
        public string Bewegung { get; set; } = "";
        public float Bewegungsrest { get; set; }
    }

    private sealed class Schwarm
    {
        public List<Vogel> Voegel { get; } = new();
        public required bool Tauben { get; init; }
        /// <summary>Mitte des Heimatgebiets und sein Halbmesser (Meter).</summary>
        public required Vector3 Heimat { get; init; }
        public required float Reichweite { get; init; }
        public bool AmBoden { get; set; }
        /// <summary>Sekunden, bis der Schwarm sich einen neuen Platz sucht.</summary>
        public float Warten { get; set; }
    }

    private readonly List<Schwarm> _schwaerme = new();
    private readonly List<Vector3> _dachpunkte = new();
    private readonly List<Vector3> _bodenpunkte = new();
    /// <summary>Landeplätze je Mesh (lokal), einmal gerechnet.</summary>
    private readonly Dictionary<Mesh, Vector3[]> _firstJeMesh = new();
    private Node3D? _vogelEbene;
    /// <summary>Sekunden seit der letzten Suche nach Dachpunkten (vor dem Anlegen: seit dem ersten Bild).</summary>
    private float _dachSuche;
    private readonly RandomNumberGenerator _vogelZufall = new() { Seed = 4711 };

    private const float VogelNachtVon = 21 * 60, VogelNachtBis = 5 * 60 + 30;
    private const float VogelScheu = 1.5f;
    /// <summary>Ein Flug von Dach zu Dach führt mindestens so weit (Meter, waagerecht) – auf demselben Dach wird nur getrippelt.</summary>
    private const float VogelMindestflug = 25f;
    /// <summary>Was in diesem Umkreis (Meter) um einen Vogel liegt, ist besetzt: Auf einem Platz sitzt immer nur ein Vogel, der nächste mindestens 2,5 m weiter.</summary>
    private const float VogelPlatzUmkreis = 2.5f;
    /// <summary>Anteil der Taubenflüge, die vor der Landung eine Runde über den Dächern drehen (davon ein Teil zwei Runden).</summary>
    private const float TaubenRunde = 0.5f;

    /// <summary>Setzt die Schwärme aus (einmal, beim ersten Bild mit Simulation).</summary>
    private void VoegelAnlegen()
    {
        _vogelEbene = new Node3D { Name = "Voegel" };
        AddChild(_vogelEbene);
        DachpunkteSuchen();
        BodenpunkteSammeln();

        // Stadt: je drei Schwärme Tauben und Spatzen; Umland: an jeder Stelle und bei jedem Betrieb draußen ein Spatzen- oder Taubenschwarm.
        for (var i = 0; i < 3; i++)
        {
            SchwarmAnlegen(true, _markt, StadtReichweite);
            SchwarmAnlegen(false, _markt, StadtReichweite);
        }
        var draussen = VogelplaetzeDraussen();
        for (var i = 0; i < draussen.Count; i++)
            SchwarmAnlegen(i % 3 == 0, draussen[i], 30);
    }

    private void SchwarmAnlegen(bool tauben, Vector3 heimat, float reichweite)
    {
        if (Modell(tauben ? "vogel_taube" : "vogel_spatz") is not { } szene || _vogelEbene is null) return;
        var schwarm = new Schwarm { Tauben = tauben, Heimat = heimat, Reichweite = reichweite, Warten = _vogelZufall.RandfRange(2, 20) };
        var anzahl = _vogelZufall.RandiRange(3, 5);
        var plaetze = DachplaetzeVerteilen(schwarm, Enumerable.Repeat(heimat, anzahl).ToList());
        anzahl = Math.Max(1, plaetze.Count(p => p is not null));
        plaetze = plaetze.Where(p => p is not null).DefaultIfEmpty(heimat).ToList();
        for (var i = 0; i < anzahl; i++)
        {
            var knoten = szene.Instantiate<Node3D>();
            _vogelEbene.AddChild(knoten);
            var spieler = knoten.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>().FirstOrDefault();
            if (spieler is not null)
                foreach (var name in spieler.GetAnimationList())
                    spieler.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
            var vogel = new Vogel { Knoten = knoten, Spieler = spieler, Taube = tauben };
            var platz = plaetze[i] ?? heimat;
            vogel.Von = vogel.Nach = platz;
            knoten.Position = platz;
            knoten.Rotation = new Vector3(0, _vogelZufall.RandfRange(0, Mathf.Tau), 0);
            Bewegen(vogel, "Sitzen");
            schwarm.Voegel.Add(vogel);
        }
        _schwaerme.Add(schwarm);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Landeplätze
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sucht in allen sichtbaren Meshes der Stadtansicht die Ecken nahe ihrer höchsten Stelle (bis 25 cm darunter), die mindestens 3,5 m
    /// über dem Boden liegen – bei einem Dach der First, bei einem Turm die Spitze. Liegt über einer Ecke noch ein anderes Bauteil (die
    /// Mauerkrone unter dem Dach), fällt sie weg. Punkte unter 1,2 m Abstand fallen zusammen.
    /// </summary>
    private void DachpunkteSuchen()
    {
        _dachpunkte.Clear();
        var teile = new List<(MeshInstance3D Teil, Mesh Mesh, Aabb Huelle)>();
        foreach (var teil in FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (teil.Mesh is not { } mesh || !teil.IsVisibleInTree() || _vogelEbene?.IsAncestorOf(teil) == true) continue;
            var huelle = teil.GlobalTransform * mesh.GetAabb();
            if (huelle.Size.X > 60 || huelle.Size.Z > 60) continue; // Boden und Mauerring nicht
            teile.Add((teil, mesh, huelle));
        }
        foreach (var (teil, mesh, huelle) in teile)
        {
            if (huelle.End.Y < 3.5f) continue;
            if (!_firstJeMesh.TryGetValue(mesh, out var first))
                _firstJeMesh[mesh] = first = Firstpunkte(mesh);
            foreach (var p in first)
            {
                var g = teil.GlobalTransform * p;
                if (g.Y < 3.5f || _dachpunkte.Any(d => d.DistanceSquaredTo(g) < 1.44f)) continue;
                var darueber = g + new Vector3(0, 0.3f, 0);
                if (teile.Any(t => t.Teil != teil && t.Huelle.HasPoint(darueber))) continue;
                _dachpunkte.Add(g);
            }
        }
    }

    /// <summary>
    /// Landeplätze eines Meshes (lokal): Es wird von oben in ein Raster aus 25-cm-Zellen „gerastert“ (je Zelle die höchste Fläche). Ein
    /// Platz ist eine Zelle, um die herum bis 1 m nichts höher liegt (First, Turmspitze, Mauerkrone, Schornstein) und die genug
    /// Nachbarn fast gleicher Höhe hat, um darauf zu stehen (keine Fahnenspitze). Punkte unter 1,2 m Abstand fallen zusammen.
    /// </summary>
    private static Vector3[] Firstpunkte(Mesh mesh)
    {
        const float zelle = 0.25f;
        var huelle = mesh.GetAabb();
        int nx = (int)(huelle.Size.X / zelle) + 2, nz = (int)(huelle.Size.Z / zelle) + 2;
        if (nx * nz > 400_000) return Array.Empty<Vector3>(); // riesige Flächen (Boden) nicht
        var hoehe = new float[nx * nz];
        Array.Fill(hoehe, float.NegativeInfinity);
        var flaechen = mesh.GetFaces(); // je drei Ecken ein Dreieck – auch für Grundkörper (Box, Prisma, Kugel)
        for (var i = 0; i + 2 < flaechen.Length; i += 3)
            Dreieck(flaechen[i], flaechen[i + 1], flaechen[i + 2]);

        void Dreieck(Vector3 a, Vector3 b, Vector3 c)
        {
            var nenner = (b.Z - c.Z) * (a.X - c.X) + (c.X - b.X) * (a.Z - c.Z);
            if (Mathf.Abs(nenner) < 1e-6f) return; // senkrechte Fläche: von oben unsichtbar
            int x0 = Zx(Mathf.Min(a.X, Mathf.Min(b.X, c.X))), x1 = Zx(Mathf.Max(a.X, Mathf.Max(b.X, c.X)));
            int z0 = Zz(Mathf.Min(a.Z, Mathf.Min(b.Z, c.Z))), z1 = Zz(Mathf.Max(a.Z, Mathf.Max(b.Z, c.Z)));
            for (var ix = x0; ix <= x1; ix++)
                for (var iz = z0; iz <= z1; iz++)
                {
                    float px = huelle.Position.X + (ix + 0.5f) * zelle, pz = huelle.Position.Z + (iz + 0.5f) * zelle;
                    var u = ((b.Z - c.Z) * (px - c.X) + (c.X - b.X) * (pz - c.Z)) / nenner;
                    var v = ((c.Z - a.Z) * (px - c.X) + (a.X - c.X) * (pz - c.Z)) / nenner;
                    var w = 1 - u - v;
                    if (u < -0.02f || v < -0.02f || w < -0.02f) continue;
                    ref var h = ref hoehe[ix * nz + iz];
                    h = Mathf.Max(h, u * a.Y + v * b.Y + w * c.Y);
                }
        }
        int Zx(float x) => Math.Clamp((int)((x - huelle.Position.X) / zelle), 0, nx - 1);
        int Zz(float z) => Math.Clamp((int)((z - huelle.Position.Z) / zelle), 0, nz - 1);

        var punkte = new List<Vector3>();
        const int umkreis = 4; // 1 m
        for (var ix = 0; ix < nx; ix++)
            for (var iz = 0; iz < nz; iz++)
            {
                var h = hoehe[ix * nz + iz];
                if (float.IsNegativeInfinity(h) || h < huelle.Position.Y + 2.5f) continue;
                var gipfel = true;
                var stand = 0;
                for (var dx = -umkreis; dx <= umkreis && gipfel; dx++)
                    for (var dz = -umkreis; dz <= umkreis; dz++)
                    {
                        int jx = ix + dx, jz = iz + dz;
                        if (jx < 0 || jz < 0 || jx >= nx || jz >= nz) continue;
                        var n = hoehe[jx * nz + jz];
                        if (n > h + 0.05f) { gipfel = false; break; }
                        if (Math.Abs(dx) <= 1 && Math.Abs(dz) <= 1 && n > h - 0.5f) stand++;
                    }
                if (!gipfel || stand < 4) continue; // die Zelle selbst zählt mit
                var p = new Vector3(huelle.Position.X + (ix + 0.5f) * zelle, h, huelle.Position.Z + (iz + 0.5f) * zelle);
                if (!punkte.Any(q => q.DistanceSquaredTo(p) < 1.44f)) punkte.Add(p);
            }
        return punkte.ToArray();
    }

    /// <summary>
    /// Die Heimat der Schwärme draußen: jede Arbeitsstelle der Einwohner und jeder Umlandbetrieb der Karte (ob bewirtschaftet oder nicht),
    /// in der Reihenfolge der Karte.
    /// </summary>
    private List<Vector3> VogelplaetzeDraussen() => _karte is null ? new List<Vector3>()
        : _karte.GebaeudeDerArt(Reichsgilde.Core.Bauen.Bausteinart.Umlandstelle).Concat(_karte.GebaeudeDerArt(Reichsgilde.Core.Bauen.Bausteinart.Umlandbetrieb))
                .Select(g => Punkt3(g.Punkt)).Where(p => !InDerStadt(p)).Distinct().ToList();

    /// <summary>So weit fliegen die Schwärme der Stadt um den Markt: die halbe Ausdehnung der Mauer (50 m in der Greybox), ohne Mauer 50 m.</summary>
    private float StadtReichweite => _mauerFlaechen.Count == 0 ? 50
        : _mauerFlaechen.SelectMany(f => f).Aggregate(new Rect2(_mauerFlaechen[0][0], Vector2.Zero), (r, p) => r.Expand(p)) is var r ? Mathf.Max(r.Size.X, r.Size.Y) / 2 : 50;

    /// <summary>Boden zum Picken: Marktplatz (um den Brunnen), Knoten des Gassennetzes, rund um Umlandstellen und Betriebe draußen.</summary>
    private void BodenpunkteSammeln()
    {
        _bodenpunkte.Clear();
        for (var i = 0; i < 16; i++)
        {
            var w = i * Mathf.Tau / 16;
            _bodenpunkte.Add(_markt + new Vector3(Mathf.Cos(w), 0, Mathf.Sin(w)) * (i % 2 == 0 ? 3f : 5.5f));
        }
        _bodenpunkte.AddRange(_netz.Knoten);
        foreach (var p in VogelplaetzeDraussen())
            for (var i = 0; i < 6; i++)
                _bodenpunkte.Add(p + new Vector3(_vogelZufall.RandfRange(-12, 12), 0, _vogelZufall.RandfRange(-12, 12)));
    }

    private Vector3? Bodenplatz(Schwarm s, Vector3 nahBei)
    {
        var moeglich = _bodenpunkte.Where(b => b.DistanceTo(s.Heimat) < s.Reichweite && b.DistanceTo(nahBei) < 30 && !PassantNahe(b, 3)
            && !_schwaerme.Any(o => o != s && o.AmBoden && o.Voegel[0].Nach.DistanceTo(b) < 4)).ToList();
        return moeglich.Count == 0 ? null : moeglich[_vogelZufall.RandiRange(0, moeglich.Count - 1)];
    }

    /// <summary>
    /// Je Vogel ein eigener Landeplatz irgendwo im Heimatgebiet: frei (kein anderer Vogel sitzt oder landet im Umkreis von
    /// <see cref="VogelPlatzUmkreis"/>) und mindestens <see cref="VogelMindestflug"/> waagerecht von der jetzigen Lage des Vogels
    /// <paramref name="von"/> entfernt – der Schwarm fliegt gemeinsam auf und verstreut sich. Gibt es so weit keinen (wenige Dächer im
    /// Umland), einer der weitesten freien. Die Vögel von <paramref name="eigener"/> zählen nicht mit, sie fliegen ja weg. Ist für einen
    /// Vogel kein Platz mehr frei, steht dort <c>null</c> – er bleibt sitzen, statt sich zu einem anderen zu setzen.
    /// </summary>
    private List<Vector3?> DachplaetzeVerteilen(Schwarm eigener, List<Vector3> von)
    {
        var plaetze = new List<Vector3?>();
        var frei = _dachpunkte.Where(d => d.DistanceTo(eigener.Heimat with { Y = d.Y }) < eigener.Reichweite && Belegung(d, eigener) == 0).ToList();
        foreach (var start in von)
        {
            var offen = frei.Where(d => plaetze.All(p => p is not { } q || q.DistanceTo(d) >= VogelPlatzUmkreis)).ToList();
            var weit = offen.Where(d => Waagerecht(d, start) >= VogelMindestflug).ToList();
            if (weit.Count == 0) weit = offen.Where(d => Waagerecht(d, start) > 4).OrderByDescending(d => Waagerecht(d, start)).Take(3).ToList();
            if (weit.Count == 0) weit = offen;
            plaetze.Add(weit.Count == 0 ? null : weit[_vogelZufall.RandiRange(0, weit.Count - 1)]);
        }
        return plaetze;
    }

    /// <summary>
    /// Wie viele Vögel im Umkreis von <see cref="VogelPlatzUmkreis"/> um <paramref name="punkt"/> sitzen oder dorthin fliegen (ohne den
    /// Schwarm <paramref name="ohne"/> und ohne den Vogel <paramref name="ohneVogel"/>).
    /// </summary>
    private int Belegung(Vector3 punkt, Schwarm? ohne, Vogel? ohneVogel = null)
    {
        var n = 0;
        foreach (var s in _schwaerme)
            if (s != ohne && !s.AmBoden)
                foreach (var v in s.Voegel)
                    if (v != ohneVogel && v.Nach.DistanceTo(punkt) < VogelPlatzUmkreis) n++;
        return n;
    }

    private static float Waagerecht(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    private static Vector3 Schwerpunkt(List<Vector3> punkte) => punkte.Aggregate(Vector3.Zero, (summe, p) => summe + p) / punkte.Count;

    /// <summary>Ein freier Dachpunkt auf demselben First (0,9 bis 4 m weiter, kaum höher oder tiefer), zu dem ein Vogel trippeln kann.</summary>
    private Vector3? Trippelplatz(Vogel v)
    {
        var nachbarn = _dachpunkte.Where(d => d.DistanceTo(v.Nach) is > 0.9f and < 4f && Mathf.Abs(d.Y - v.Nach.Y) < 0.4f).ToList();
        if (nachbarn.Count == 0) return null;
        var punkt = nachbarn[_vogelZufall.RandiRange(0, nachbarn.Count - 1)];
        return Belegung(punkt, null, v) == 0 ? punkt : null;
    }

    /// <summary>
    /// Kleiner Gag: Ein Klick auf einen Vogel scheucht ihn auf, und sein ganzer Schwarm fliegt erschrocken davon. Getroffen ist der
    /// Vogel, der dem Mauszeiger auf dem Bildschirm am nächsten ist (wie bei <see cref="PassantBei"/>). Gibt <c>true</c> zurück,
    /// wenn ein Vogel getroffen war – dann wählt der Klick nichts anderes aus.
    /// </summary>
    private bool VogelAufscheuchen(Vector2 bildschirm)
    {
        if (_raum is not null || _innen is not null || _vogelEbene is null) return false;
        var pixelJeMeter = GetViewport().GetVisibleRect().Size.Y / _kamera.Size;
        Schwarm? getroffen = null;
        var besterAbstand = Mathf.Max(10, 0.5f * pixelJeMeter);
        foreach (var s in _schwaerme)
            foreach (var v in s.Voegel)
            {
                var mitte = v.Knoten.GlobalPosition + new Vector3(0, 0.15f, 0);
                if (_kamera.IsPositionBehind(mitte)) continue;
                var abstand = _kamera.UnprojectPosition(mitte).DistanceTo(bildschirm);
                if (abstand < besterAbstand) { besterAbstand = abstand; getroffen = s; }
            }
        if (getroffen is null) return false;
        if (getroffen.Voegel.All(v => v.Zustand != Vogelzustand.Fliegt)) SchwarmFliegt(getroffen, boden: false, erschreckt: true);
        return true;
    }

    private bool PassantNahe(Vector3 p, float abstand)
    {
        foreach (var passant in _passanten.Values)
            if (passant.Knoten.Visible && passant.Knoten.GlobalPosition.DistanceSquaredTo(p) < abstand * abstand) return true;
        return false;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Jedes Bild
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Bewegt die Vögel; <paramref name="uhrzeit"/> in Minuten seit Mitternacht. Steht das Spiel, stehen auch die Vögel.</summary>
    private void VoegelAktualisieren(float uhrzeit, double delta, bool pausiert)
    {
        if (_vogelEbene is null)
        {
            // erst, wenn die Wohnhäuser stehen (die setzt die Stadtansicht in den ersten Bildern nach dem Start)
            _dachSuche += (float)delta;
            if (_dachSuche < 2) return;
            _dachSuche = 0;
            VoegelAnlegen();
        }
        foreach (var s in _schwaerme)
            foreach (var v in s.Voegel)
                if (v.Spieler is { } sp) sp.SpeedScale = pausiert ? 0 : 1;
        if (pausiert) return;
        var dt = (float)delta;

        _dachSuche += dt;
        if (_dachSuche > 45) { _dachSuche = 0; DachpunkteSuchen(); }

        var nacht = uhrzeit >= VogelNachtVon || uhrzeit < VogelNachtBis;
        foreach (var s in _schwaerme)
        {
            var fliegt = s.Voegel.Any(v => v.Zustand == Vogelzustand.Fliegt);
            if (!fliegt)
            {
                s.Warten -= dt;
                var erschreckt = s.AmBoden && s.Voegel.Any(v => PassantNahe(v.Knoten.Position, VogelScheu));
                if (erschreckt || (s.AmBoden && nacht)) SchwarmFliegt(s, boden: false, erschreckt);
                else if (s.Warten <= 0 && !(nacht && !s.AmBoden && _vogelZufall.Randf() < 0.9f))
                    SchwarmFliegt(s, boden: !nacht && !s.AmBoden && _vogelZufall.Randf() < (s.Tauben ? 0.5f : 0.6f));
                else if (s.Warten <= 0) s.Warten = _vogelZufall.RandfRange(20, 60); // nachts meist sitzen bleiben
            }
            foreach (var v in s.Voegel) VogelBewegen(s, v, dt);
        }
    }

    /// <summary>
    /// Der ganze Schwarm zieht um: auf ein Dach (anderes Haus) oder zum Boden. <paramref name="erschreckt"/>: alle auf einmal und
    /// schneller, ohne Runde (ein Passant kam zu nahe oder jemand hat geklickt).
    /// </summary>
    private void SchwarmFliegt(Schwarm s, bool boden, bool erschreckt = false)
    {
        var jetzt = s.Voegel.Select(v => v.Knoten.Position).ToList();
        var mitte = Schwerpunkt(jetzt);
        List<Vector3?>? plaetze = null;
        Vector3 z;
        if (boden)
        {
            if (Bodenplatz(s, mitte) is not { } b) { s.Warten = _vogelZufall.RandfRange(5, 15); return; }
            z = b;
        }
        else
        {
            plaetze = DachplaetzeVerteilen(s, jetzt);
            var ziele = plaetze.OfType<Vector3>().ToList();
            if (ziele.Count == 0) { s.Warten = _vogelZufall.RandfRange(5, 15); return; }
            z = Schwerpunkt(ziele);
        }
        // jeder zweite Taubenflug dreht erst gemeinsam eine Runde (manchmal zwei) über den Dächern, dann verstreut sich der Schwarm
        (Vector3 Mitte, float Halbmesser, int Runden)? runde = null;
        if (!boden && !erschreckt && s.Tauben && _vogelZufall.Randf() < TaubenRunde)
            runde = (mitte.Lerp(z, 0.5f) with { Y = Mathf.Max(mitte.Y, z.Y) + _vogelZufall.RandfRange(6, 10) }, _vogelZufall.RandfRange(10, 15),
                _vogelZufall.Randf() < 0.3f ? 2 : 1);
        s.AmBoden = boden;
        s.Warten = boden ? _vogelZufall.RandfRange(15, 40) : _vogelZufall.RandfRange(10, 35);
        for (var i = 0; i < s.Voegel.Count; i++)
        {
            var v = s.Voegel[i];
            if (!boden && plaetze?[i] is null) continue; // kein freier Platz: bleibt sitzen
            v.Von = v.Knoten.Position;
            v.Nach = plaetze?[i] ?? z + new Vector3(_vogelZufall.RandfRange(-2.5f, 2.5f), 0, _vogelZufall.RandfRange(-2.5f, 2.5f));
            // in der Runde fliegt jeder etwas versetzt, damit es nach Schwarm aussieht und nicht nach einer Perlenkette
            BahnLegen(v, runde is { } r ? (r.Mitte + new Vector3(0, _vogelZufall.RandfRange(-0.8f, 0.8f), 0), r.Halbmesser + _vogelZufall.RandfRange(-1.2f, 1.2f), r.Runden) : null,
                erschreckt ? 1.6f : 1f);
            v.Zeit = -_vogelZufall.RandfRange(0, erschreckt ? 0.15f : 0.6f); // nicht alle im selben Augenblick
            v.Zustand = Vogelzustand.Fliegt;
        }
    }

    /// <summary>
    /// Legt die Flugbahn von <c>Von</c> nach <c>Nach</c>: steil aufsteigen, dann ein hoher Bogen (oder eine Runde um
    /// <paramref name="runde"/>), schräg anfliegen und landen. Die Stützpunkte verbindet <see cref="Flugpunkt"/> zu einer glatten Kurve.
    /// </summary>
    private static void BahnLegen(Vogel v, (Vector3 Mitte, float Halbmesser, int Runden)? runde, float tempo = 1f)
    {
        var von = v.Von;
        var nach = v.Nach;
        var mitte = new List<Vector3>();
        if (runde is { } r)
        {
            // von der Abflugseite ein- oder zweimal ganz herum und weiter bis zur Seite des Ziels
            var anfang = Mathf.Atan2(von.Z - r.Mitte.Z, von.X - r.Mitte.X);
            var ende = anfang + Mathf.Tau * r.Runden + Mathf.PosMod(Mathf.Atan2(nach.Z - r.Mitte.Z, nach.X - r.Mitte.X) - anfang, Mathf.Tau);
            for (var w = anfang; w <= ende; w += Mathf.Tau / 10)
                mitte.Add(r.Mitte + new Vector3(Mathf.Cos(w), 0, Mathf.Sin(w)) * r.Halbmesser);
        }
        else
            // Spatzen fliegen weitere, höhere Bögen
            mitte.Add(von.Lerp(nach, 0.5f) with { Y = Mathf.Max(von.Y, nach.Y) + 1.5f + Waagerecht(von, nach) * (v.Taube ? 0.15f : 0.22f) });
        var ab = ((mitte[0] - von) with { Y = 0 }).Normalized();
        var an = ((nach - mitte[^1]) with { Y = 0 }).Normalized();
        var bahn = new List<Vector3> { von, von + Vector3.Up * 1.2f + ab * 0.5f };
        bahn.AddRange(mitte);
        bahn.Add(nach + Vector3.Up * 1.0f - an * 0.9f);
        bahn.Add(nach);
        v.Bahn = bahn.ToArray();
        v.BahnWeg = new float[v.Bahn.Length];
        for (var i = 1; i < v.Bahn.Length; i++) v.BahnWeg[i] = v.BahnWeg[i - 1] + v.Bahn[i - 1].DistanceTo(v.Bahn[i]);
        v.Dauer = Mathf.Max(1.5f / tempo, v.BahnWeg[^1] / ((v.Taube ? 7f : 6f) * tempo));
    }

    /// <summary>
    /// Ort auf der Flugbahn nach <paramref name="zeit"/> Sekunden: Beim Abflug und bei der Landung (je bis 0,6 s) ist der Vogel langsamer,
    /// dazwischen gleich schnell; zwischen den Stützpunkten eine Catmull-Rom-Kurve.
    /// </summary>
    private static Vector3 Flugpunkt(Vogel v, float zeit)
    {
        var laenge = v.BahnWeg[^1];
        var dauer = v.Dauer;
        var rampe = Mathf.Min(0.6f, dauer / 4);
        var schnell = laenge / (dauer - rampe);
        zeit = Mathf.Clamp(zeit, 0, dauer);
        var weg = zeit < rampe ? schnell * zeit * zeit / (2 * rampe)
            : zeit > dauer - rampe ? laenge - schnell * (dauer - zeit) * (dauer - zeit) / (2 * rampe)
            : schnell * (zeit - rampe / 2);
        var i = 0;
        while (i < v.Bahn.Length - 2 && v.BahnWeg[i + 1] < weg) i++;
        var stueck = v.BahnWeg[i + 1] - v.BahnWeg[i];
        var anteil = stueck > 1e-4f ? Mathf.Clamp((weg - v.BahnWeg[i]) / stueck, 0, 1) : 1;
        var vor = v.Bahn[Math.Max(0, i - 1)];
        var nach = v.Bahn[Math.Min(v.Bahn.Length - 1, i + 2)];
        return v.Bahn[i].CubicInterpolate(v.Bahn[i + 1], vor, nach, anteil);
    }

    private void VogelBewegen(Schwarm s, Vogel v, float dt)
    {
        v.Bewegungsrest -= dt;
        switch (v.Zustand)
        {
            case Vogelzustand.Fliegt:
                v.Zeit += dt;
                if (v.Zeit < 0) break;
                var t = Mathf.Min(1, v.Zeit / v.Dauer);
                var p = Flugpunkt(v, v.Zeit);
                var richtung = (p - v.Knoten.Position) with { Y = 0 };
                if (richtung.LengthSquared() > 1e-4f) Ausrichten(v, richtung);
                v.Knoten.Position = p;
                // auf langen Flügen in der Mitte gleiten, sonst schlagen
                Bewegen(v, v.BahnWeg[^1] > 25 && t is > 0.3f and < 0.75f ? "Gleiten" : "Fliegen");
                if (t >= 1)
                {
                    v.Zustand = s.AmBoden ? Vogelzustand.AmBoden : Vogelzustand.Sitzt;
                    v.Zeit = 0;
                    v.Bewegungsrest = 0;
                }
                break;
            case Vogelzustand.Sitzt:
                if (v.Bewegung == "Gehen" && v.Bewegungsrest > 0)
                {
                    // auf dem First zum Nachbarplatz trippeln
                    var weg = v.Nach - v.Knoten.Position;
                    var schritt = (v.Taube ? 0.35f : 0.5f) * dt;
                    v.Knoten.Position = weg.Length() > schritt ? v.Knoten.Position + weg.Normalized() * schritt : v.Nach;
                    if (v.Knoten.Position == v.Nach) v.Bewegungsrest = 0;
                }
                else if (v.Bewegungsrest <= 0 && _vogelZufall.Randf() < 0.12f && Trippelplatz(v) is { } neben)
                {
                    v.Nach = neben;
                    Ausrichten(v, neben - v.Knoten.Position);
                    Bewegen(v, "Gehen", v.Knoten.Position.DistanceTo(neben) / (v.Taube ? 0.35f : 0.5f) + 0.5f);
                }
                else if (v.Bewegungsrest <= 0)
                {
                    var putzen = _vogelZufall.Randf() < 0.3f;
                    Bewegen(v, putzen ? "Putzen" : "Sitzen", putzen ? 2f : _vogelZufall.RandfRange(3, 9));
                    if (!putzen && _vogelZufall.Randf() < 0.3f)
                        Ausrichten(v, new Vector3(Mathf.Cos(_vogelZufall.Randf() * Mathf.Tau), 0, Mathf.Sin(_vogelZufall.Randf() * Mathf.Tau)));
                }
                break;
            case Vogelzustand.AmBoden:
                if (v.Bewegung == "Gehen" && v.Bewegungsrest > 0)
                {
                    var weg = (v.Nach - v.Knoten.Position) with { Y = 0 };
                    var schritt = (v.Taube ? 0.35f : 0.5f) * dt;
                    if (weg.Length() > schritt) v.Knoten.Position += weg.Normalized() * schritt;
                }
                else if (v.Bewegungsrest <= 0)
                {
                    if (_vogelZufall.Randf() < 0.4f)
                    {
                        // ein paar Schritte weiter, in der Nähe des Schwarms bleiben
                        v.Nach = v.Knoten.Position + new Vector3(_vogelZufall.RandfRange(-0.8f, 0.8f), 0, _vogelZufall.RandfRange(-0.8f, 0.8f));
                        Ausrichten(v, v.Nach - v.Knoten.Position);
                        Bewegen(v, "Gehen", _vogelZufall.RandfRange(1, 2.5f));
                    }
                    else Bewegen(v, "Picken", _vogelZufall.RandfRange(1, 3));
                }
                break;
        }
    }

    /// <summary>Das Modell schaut nach +Z: dreht es in die Richtung <paramref name="richtung"/> (waagerecht).</summary>
    private static void Ausrichten(Vogel v, Vector3 richtung)
    {
        richtung.Y = 0;
        if (richtung.LengthSquared() < 1e-4f) return;
        v.Knoten.Rotation = new Vector3(0, Mathf.Atan2(richtung.X, richtung.Z), 0);
    }

    /// <summary>Spielt die Bewegung <paramref name="name"/> (aus <c>tools/modelle/voegel.py</c>) für <paramref name="dauer"/> Sekunden.</summary>
    private static void Bewegen(Vogel v, string name, float dauer = 0)
    {
        if (dauer > 0) v.Bewegungsrest = dauer;
        if (v.Bewegung == name) return;
        v.Bewegung = name;
        if (v.Spieler is { } sp && sp.HasAnimation(name)) sp.Play(name, customBlend: 0.15);
    }
}
