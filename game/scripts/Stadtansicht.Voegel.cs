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
        public Vector3 Nach { get; set; }
        /// <summary>Flug: vergangene Sekunden (negativ = wartet noch auf den Abflug); am Boden: Sekunden bis zum nächsten Schritt.</summary>
        public float Zeit { get; set; }
        public float Dauer { get; set; }
        public float Bogen { get; set; }
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
            SchwarmAnlegen(true, Marktplatz, Mauerrand);
            SchwarmAnlegen(false, Marktplatz, Mauerrand);
        }
        var draussen = Umlandstellen.Values.Concat(Stadtplan.Values.Where(p => Mathf.Max(Mathf.Abs(p.X), Mathf.Abs(p.Z)) > Mauerrand + 5)).Distinct().ToList();
        for (var i = 0; i < draussen.Count; i++)
            SchwarmAnlegen(i % 3 == 0, draussen[i], 30);
    }

    private void SchwarmAnlegen(bool tauben, Vector3 heimat, float reichweite)
    {
        if (Modell(tauben ? "vogel_taube" : "vogel_spatz") is not { } szene || _vogelEbene is null) return;
        var schwarm = new Schwarm { Tauben = tauben, Heimat = heimat, Reichweite = reichweite, Warten = _vogelZufall.RandfRange(2, 20) };
        var start = Dachplatz(schwarm, heimat) ?? heimat;
        var anzahl = _vogelZufall.RandiRange(3, 5);
        for (var i = 0; i < anzahl; i++)
        {
            var knoten = szene.Instantiate<Node3D>();
            _vogelEbene.AddChild(knoten);
            var spieler = knoten.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>().FirstOrDefault();
            if (spieler is not null)
                foreach (var name in spieler.GetAnimationList())
                    spieler.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
            var vogel = new Vogel { Knoten = knoten, Spieler = spieler, Taube = tauben };
            var platz = NaechsterDachpunkt(start, i) ?? start;
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

    /// <summary>Boden zum Picken: Marktplatz (um den Brunnen), Knoten des Gassennetzes, rund um Umlandstellen und Betriebe draußen.</summary>
    private void BodenpunkteSammeln()
    {
        _bodenpunkte.Clear();
        for (var i = 0; i < 16; i++)
        {
            var w = i * Mathf.Tau / 16;
            _bodenpunkte.Add(Marktplatz + new Vector3(Mathf.Cos(w), 0, Mathf.Sin(w)) * (i % 2 == 0 ? 3f : 5.5f));
        }
        _bodenpunkte.AddRange(_netz.Knoten);
        foreach (var p in Umlandstellen.Values.Concat(Stadtplan.Values.Where(p => Mathf.Max(Mathf.Abs(p.X), Mathf.Abs(p.Z)) > Mauerrand + 5)))
            for (var i = 0; i < 6; i++)
                _bodenpunkte.Add(p + new Vector3(_vogelZufall.RandfRange(-12, 12), 0, _vogelZufall.RandfRange(-12, 12)));
    }

    private Vector3? Dachplatz(Schwarm s, Vector3 nahBei)
    {
        var moeglich = _dachpunkte.Where(d => d.DistanceTo(s.Heimat with { Y = d.Y }) < s.Reichweite && d.DistanceTo(nahBei) > 4).ToList();
        if (moeglich.Count == 0) return null;
        // lieber das nächste Haus als ans andere Ende der Stadt
        var nah = moeglich.OrderBy(d => d.DistanceTo(nahBei)).Take(Math.Max(3, moeglich.Count / 4)).ToList();
        return nah[_vogelZufall.RandiRange(0, nah.Count - 1)];
    }

    private Vector3? Bodenplatz(Schwarm s, Vector3 nahBei)
    {
        var moeglich = _bodenpunkte.Where(b => b.DistanceTo(s.Heimat) < s.Reichweite && b.DistanceTo(nahBei) < 30 && !PassantNahe(b, 3)).ToList();
        return moeglich.Count == 0 ? null : moeglich[_vogelZufall.RandiRange(0, moeglich.Count - 1)];
    }

    /// <summary>Für den <paramref name="nr"/>-ten Vogel eines Schwarms: ein Firstpunkt nahe <paramref name="ziel"/> (mehrere nebeneinander auf einem First).</summary>
    private Vector3? NaechsterDachpunkt(Vector3 ziel, int nr)
    {
        if (_dachpunkte.Count == 0) return null;
        var nah = _dachpunkte.OrderBy(d => d.DistanceSquaredTo(ziel)).First();
        // auf dem First nebeneinander: kleiner Versatz längs x/z, Höhe bleibt
        return nah + new Vector3((nr % 3 - 1) * 0.35f, 0, (nr / 3) * 0.35f);
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
                if (erschreckt || (s.AmBoden && nacht)) SchwarmFliegt(s, boden: false);
                else if (s.Warten <= 0 && !(nacht && !s.AmBoden && _vogelZufall.Randf() < 0.9f))
                    SchwarmFliegt(s, boden: !nacht && !s.AmBoden && _vogelZufall.Randf() < (s.Tauben ? 0.5f : 0.6f));
                else if (s.Warten <= 0) s.Warten = _vogelZufall.RandfRange(20, 60); // nachts meist sitzen bleiben
            }
            foreach (var v in s.Voegel) VogelBewegen(s, v, dt);
        }
    }

    /// <summary>Der ganze Schwarm zieht um: auf ein Dach (nächstes Haus) oder zum Boden.</summary>
    private void SchwarmFliegt(Schwarm s, bool boden)
    {
        var mitte = s.Voegel[0].Knoten.Position;
        var ziel = boden ? Bodenplatz(s, mitte) : Dachplatz(s, mitte);
        if (ziel is not { } z) { s.Warten = _vogelZufall.RandfRange(5, 15); return; }
        s.AmBoden = boden;
        s.Warten = boden ? _vogelZufall.RandfRange(15, 40) : _vogelZufall.RandfRange(20, 70);
        for (var i = 0; i < s.Voegel.Count; i++)
        {
            var v = s.Voegel[i];
            v.Von = v.Knoten.Position;
            v.Nach = boden
                ? z + new Vector3(_vogelZufall.RandfRange(-1.2f, 1.2f), 0, _vogelZufall.RandfRange(-1.2f, 1.2f))
                : NaechsterDachpunkt(z, i) ?? z;
            var weite = v.Von.DistanceTo(v.Nach);
            v.Dauer = Mathf.Max(0.8f, weite / (v.Taube ? 7f : 6f));
            v.Bogen = 1.5f + weite * 0.2f;
            v.Zeit = -_vogelZufall.RandfRange(0, 0.6f); // nicht alle im selben Augenblick
            v.Zustand = Vogelzustand.Fliegt;
        }
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
                var p = v.Von.Lerp(v.Nach, t);
                p.Y += Mathf.Sin(t * Mathf.Pi) * v.Bogen;
                var richtung = (v.Nach - v.Von) with { Y = 0 };
                if (richtung.LengthSquared() > 0.01f) Ausrichten(v, richtung);
                v.Knoten.Position = p;
                // auf langen Flügen in der Mitte gleiten, sonst schlagen
                Bewegen(v, v.Dauer > 2.5f && t is > 0.35f and < 0.7f ? "Gleiten" : "Fliegen");
                if (t >= 1)
                {
                    v.Zustand = s.AmBoden ? Vogelzustand.AmBoden : Vogelzustand.Sitzt;
                    v.Zeit = 0;
                    v.Bewegungsrest = 0;
                }
                break;
            case Vogelzustand.Sitzt:
                if (v.Bewegungsrest <= 0)
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
