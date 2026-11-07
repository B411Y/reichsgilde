using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Das Gelände – ein Teil der <c>partial class Stadtansicht</c> (Kartenwerkstatt, Schritt 5b). Der Kern führt ein Höhenraster (alle 2 m ein Punkt)
/// und rechnet daraus den Boden mit dem Bett der Wege (<see cref="Gelaendeform"/>); hier wird er gezeichnet und alles darauf gestellt:
/// <list type="bullet">
/// <item><b>Boden</b> (<see cref="GelaendeBauen"/>): Kacheln zu 64 × 64 Zellen (128 m), wie der Kern sie speichert. Eine Kachel ohne Höhe ist ein einziges
/// Viereck, eine mit Höhen ein Netz mit Normalen (im Spiel mit gröberem Netz weit weg vom Spielfeld, dazu Schürzen gegen Risse). Eine <b>flache</b> Karte
/// hat keine Kacheln: Sie bleibt eine einzige Ebene wie bisher, alles darauf kostet nichts. In der Werkstatt baut <see cref="GelaendeKachelnNeu"/> nur die
/// Kacheln neu, die ein Pinselstrich oder ein Weg berührt.</item>
/// <item><b>Höhe an einem Ort</b> (<see cref="BodenHoehe(float, float)"/>): genau die Fläche des Netzes (Dreieck für Dreieck wie in den Kacheln), damit
/// Wege, Pflanzen und Figuren nicht in den Boden sinken oder darüber schweben. <see cref="FussHoehe"/> gilt für Figuren und Karren: Auf dem
/// Fundament eines Gebäudes (Marktplatz, Hof) ist es dessen Höhe.</item>
/// <item><b>Fundament</b> (<see cref="FundamentVon"/>, <see cref="SockelBauen"/>): Ein Gebäude steht auf dem höchsten Punkt unter seiner Fläche; darunter
/// wächst ein Sockel aus Bruchstein bis zum tiefsten – das Gelände bleibt, wie es ist.</item>
/// <item><b>Maus</b> (<see cref="BodenTrefferBei"/>): Der Strahl der Kamera trifft das Gelände, nicht die Ebene y = 0.</item>
/// <item><b>Kamera</b> (<see cref="KameraHoeheNachfuehren"/>): Der Blickpunkt steigt und fällt weich mit dem Boden.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Der Boden, den die Ansicht zeichnet: im Spiel der der Karte, in der Werkstatt der des Entwurfs (null: noch keiner).</summary>
    private Gelaendeform? _gelaende;

    /// <summary>Hat der Boden Höhen? Eine flache Karte (und jede ohne Gelände) rechnet überall 0 und zeichnet wie bisher.</summary>
    private bool BodenBewegt => _gelaende is { IstFlach: false };

    private const int KachelZellen = Hoehenraster.Kachel;

    /// <summary>Rasterpunkte rings um eine Kachel, die für die Normalen am Rand mitgelesen werden (größer als der gröbste Schritt).</summary>
    private const int KachelRand = 4;

    /// <summary>Die Kacheln des Bodens und ihr Knoten. Eine flache Karte hat keine.</summary>
    private Node3D? _gelaendeKnoten;
    private readonly Dictionary<(int I, int J), MeshInstance3D> _gelaendeKacheln = new();

    /// <summary>Die flachen Streifen ringsum (Stoff: <see cref="BodenRandStoff"/>).</summary>
    private readonly List<MeshInstance3D> _bodenStreifen = new();

    /// <summary>Hatte der Boden beim letzten Aufbau Höhen? Wechselt das (erster Pinselstrich, Rückgängig bis zum Anfang), wird der Boden ganz neu gebaut.</summary>
    private bool _bodenGebaut;

    /// <summary>Im Spiel wird der Boden weit vom Spielfeld gröber gezeichnet; in der Werkstatt immer fein (man malt darauf).</summary>
    private bool GelaendeGrob => _entwurf is null;

    // ═════════════════════════════════════════════════════════════════════════
    //  Höhe lesen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die Höhe des gezeichneten Bodens in Metern. Das Netz teilt jede Zelle des Rasters entlang der Diagonale von links oben nach rechts unten in zwei
    /// Dreiecke; hier wird genau auf diesen Dreiecken gerechnet (nicht bilinear wie im Kern), damit alles auf dem Boden liegt, der zu sehen ist.
    /// </summary>
    private float BodenHoehe(float x, float z)
    {
        if (!BodenBewegt) return 0f;
        var (fx, fz) = (x / (float)Hoehenraster.Abstand, z / (float)Hoehenraster.Abstand);
        var (i, j) = ((int)Mathf.Floor(fx), (int)Mathf.Floor(fz));
        var (tx, tz) = (fx - i, fz - j);
        var a = (float)_gelaende!.Punkthoehe(i, j);
        var d = (float)_gelaende.Punkthoehe(i + 1, j + 1);
        return tx >= tz
            ? a + ((float)_gelaende.Punkthoehe(i + 1, j) - a) * tx + (d - (float)_gelaende.Punkthoehe(i + 1, j)) * tz
            : a + (d - (float)_gelaende.Punkthoehe(i, j + 1)) * tx + ((float)_gelaende.Punkthoehe(i, j + 1) - a) * tz;
    }

    private float BodenHoehe(Vector3 p) => BodenHoehe(p.X, p.Z);
    private float BodenHoehe(Vector2 p) => BodenHoehe(p.X, p.Y);

    /// <summary>Der Punkt mit der Höhe des Bodens darunter, dazu <paramref name="ueber"/> Meter.</summary>
    private Vector3 AufBoden(Vector3 p, float ueber = 0f) => new(p.X, BodenHoehe(p.X, p.Z) + ueber, p.Z);

    /// <summary>Der Boden an einer Stelle als Normale (Einheitsvektor); ohne Gelände nach oben.</summary>
    private Vector3 BodenNormale(float x, float z)
    {
        if (!BodenBewegt) return Vector3.Up;
        var n = _gelaende!.Normale(x, z);
        return new Vector3((float)n.X, (float)n.Y, (float)n.Z);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Fundamente
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Grundfläche eines Gebäudes mit Höhe des Fußbodens, wie sie für Figuren und Karren gilt (<see cref="FussHoehe"/>).</summary>
    private readonly List<(Vector2[] Ecken, Rect2 Rahmen, float Hoehe)> _fundamente = new();

    /// <summary>Die Ecken einer Fläche (Mitte, Drehung im Bogenmaß wie <c>Rotation.Y</c>, Breite, Tiefe) – dieselbe Rechnung wie im Kern (<c>GebaeudeEcken</c>).</summary>
    private static Kartenort[] FlaechenEcken(Vector3 mitte, float drehung, Vector2 groesse)
    {
        var (w, t) = (groesse.X / 2.0, groesse.Y / 2.0);
        var grad = drehung * 180.0 / Math.PI;
        return new[] { (-w, -t), (w, -t), (w, t), (-w, t) }.Select(e =>
        {
            var d = Kartengeometrie.Dreh(e.Item1, e.Item2, grad);
            return new Kartenort(mitte.X + d.X, mitte.Z + d.Z);
        }).ToArray();
    }

    /// <summary>Das Fundament einer Fläche: Höhe des Fußbodens (der höchste Punkt des Bodens darunter) und wie tief der Sockel bis zum tiefsten reicht. Flach: (0, 0).</summary>
    private (float Hoehe, float Tiefe) FundamentVon(Vector3 mitte, float drehung, Vector2 groesse)
    {
        if (!BodenBewegt || groesse == Vector2.Zero) return (0f, 0f);
        var f = _gelaende!.Fundament(FlaechenEcken(mitte, drehung, groesse));
        return ((float)f.Hoehe, (float)f.Tiefe);
    }

    /// <summary>Der Punkt mit der Höhe des Fußbodens eines Gebäudes auf dieser Fläche.</summary>
    private Vector3 AufFundament(Vector3 mitte, float drehung, Vector2 groesse) => new(mitte.X, FundamentVon(mitte, drehung, groesse).Hoehe, mitte.Z);

    /// <summary>Die Fundamente der Gebäude der Karte im Spiel – der Boden ändert sich dort nicht mehr, gerechnet wird je Gebäude einmal.</summary>
    private readonly Dictionary<Kartengebaeude, (float Hoehe, float Tiefe)> _fundamentCache = new();

    /// <summary>Das Fundament eines Gebäudes der Karte (die ganze Fläche mit Vorplatz).</summary>
    private (float Hoehe, float Tiefe) FundamentVon(Kartengebaeude g)
    {
        if (!BodenBewegt) return (0f, 0f);
        if (_entwurf is not null) return FundamentVon(Punkt3(g.Punkt), DrehungVon(g), GroesseVon(g));
        if (!_fundamentCache.TryGetValue(g, out var f)) _fundamentCache[g] = f = FundamentVon(Punkt3(g.Punkt), DrehungVon(g), GroesseVon(g));
        return f;
    }

    /// <summary>
    /// Der Sockel aus Bruchstein unter einem Gebäude: ein Quader von der Höhe des Fußbodens (lokal y = 0) <paramref name="tiefe"/> Meter nach unten, etwas
    /// in den Boden hinein, auf der Fläche des Gebäudes. <paramref name="mitte"/> und <paramref name="drehung"/> sind lokal im Knoten <paramref name="eltern"/>.
    /// Merkt die Fläche für <see cref="FussHoehe"/>. Unter 12 cm Unterschied braucht es keinen Sockel (null).
    /// </summary>
    private MeshInstance3D? SockelBauen(Node3D eltern, Vector3 mitte, float drehung, Vector2 groesse, float tiefe, bool erde = false)
    {
        if (tiefe < SockelMindestTiefe || groesse == Vector2.Zero) return null;
        const float einsatz = 0.35f;
        var hoehe = tiefe + einsatz;
        var sockel = new MeshInstance3D
        {
            Name = "Sockel",
            Mesh = new BoxMesh { Size = new Vector3(groesse.X - 0.15f, hoehe, groesse.Y - 0.15f) },
            MaterialOverride = Material(Bruchstein),
            Position = mitte + new Vector3(0, -hoehe / 2, 0),
            Rotation = new Vector3(0, drehung, 0),
        };
        eltern.AddChild(sockel);
        // Höfe vor der Stadt (Boden Stufe 2): oben festgetretene Erde wie der Hof des Modells (Stadtansicht.Bodenstempel.cs), kein helles Pflaster – das war die
        // graue Fläche um die Mühle auf hängigem Gelände
        if (erde) sockel.MaterialOverride = Material(Weg);
        // oben liegt das Pflaster (nicht der helle Bruchstein): So sieht der Sockel wie ein gepflasterter Vorplatz aus
        else sockel.AddChild(new MeshInstance3D
        {
            Name = "Pflaster", Mesh = new PlaneMesh { Size = new Vector2(groesse.X - 0.15f, groesse.Y - 0.15f) }, MaterialOverride = Material(Pflaster),
            Position = new Vector3(0, hoehe / 2 + 0.02f, 0),
        });
        return sockel;
    }

    /// <summary>So weit (Meter) geht die Böschung eines Erdsockels höchstens ins Gelände hinaus; am Rand schwankt sie mit Rauschen um etwa ein Drittel.</summary>
    private const float BoeschungBreite = 4.5f;

    /// <summary>Ein Wert zwischen -1 und 1 aus Ort und Saat (Wertrauschen auf einem Netz von 3 m, glatt dazwischen) – nur für die Darstellung, nichts davon geht in den Kern.</summary>
    private static float BoeschungRauschen(float x, float z, int saat)
    {
        static float Wert(int i, int j, int s)
        {
            var h = unchecked((uint)(i * 73856093 ^ j * 19349663 ^ s * 83492791));
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 32767.5f - 1f;
        }
        var (fx, fz) = (x / 3f, z / 3f);
        var (i, j) = ((int)Mathf.Floor(fx), (int)Mathf.Floor(fz));
        var (tx, tz) = (fx - i, fz - j);
        (tx, tz) = (tx * tx * (3 - 2 * tx), tz * tz * (3 - 2 * tz));
        var oben = Mathf.Lerp(Wert(i, j, saat), Wert(i + 1, j, saat), tx);
        var unten = Mathf.Lerp(Wert(i, j + 1, saat), Wert(i + 1, j + 1, saat), tx);
        return Mathf.Lerp(oben, unten, tz);
    }

    /// <summary>
    /// Der Erdsockel eines Umlandplatzes am Hang als Böschung statt als Quader: oben eine ebene Fläche auf der Höhe des Fußbodens, ringsum ein Netz, das in einem
    /// weichen Übergang (bis <see cref="BoeschungBreite"/> m, am Rand mit Rauschen) von dieser Höhe auf die des Bodens sinkt und knapp unter ihm endet. Wo der Boden
    /// höher liegt, verdeckt er das Netz. Rein Darstellung: Boden, Wege und <see cref="FussHoehe"/> bleiben, wie sie sind. Die Punkte stehen in Weltkoordinaten, <paramref name="eltern"/> ist die Stadt (am Ursprung).
    /// </summary>
    private MeshInstance3D? BoeschungBauen(Node3D eltern, Vector3 mitte, float drehung, Vector2 groesse, float tiefe, int saat)
    {
        if (tiefe < SockelMindestTiefe || groesse == Vector2.Zero) return null;
        const float schritt = 1f, unter = 0.04f;
        var (halb, grad) = (groesse / 2f - new Vector2(0.075f, 0.075f), drehung * 180f / Mathf.Pi);
        var (sinus, kosinus) = (Mathf.Sin(-drehung), Mathf.Cos(-drehung));
        var rand = BoeschungBreite * 1.35f;
        var zellen = (int)Mathf.Ceil((Mathf.Max(halb.X, halb.Y) * 1.42f + rand) / schritt);
        // eine Stelle (x, z in Weltkoordinaten): wie weit sie ausserhalb des Rechtecks liegt, und die Höhe des Netzes dort
        float Hoehe(float x, float z)
        {
            var (dx, dz) = (x - mitte.X, z - mitte.Z);
            var (lx, lz) = (dx * kosinus - dz * sinus, dx * sinus + dz * kosinus); // ins Rechteck gedreht
            var (ax, az) = (Mathf.Max(Mathf.Abs(lx) - halb.X, 0f), Mathf.Max(Mathf.Abs(lz) - halb.Y, 0f));
            var abstand = Mathf.Sqrt(ax * ax + az * az);
            if (abstand <= 0f) return mitte.Y;
            // so breit, wie das Gefälle an der nächsten Stelle der Kante es braucht (etwa 1 : 2,5), höchstens BoeschungBreite: Wo der Boden fast ebenso hoch liegt, bleibt nur ein schmaler Saum
            var (kx, kz) = (Mathf.Clamp(lx, -halb.X, halb.X), Mathf.Clamp(lz, -halb.Y, halb.Y));
            var kante = BodenHoehe(mitte.X + kx * kosinus + kz * sinus, mitte.Z - kx * sinus + kz * kosinus);
            var reichweite = Mathf.Clamp((mitte.Y - kante) * 2.5f + 1f, 1f, BoeschungBreite) * (1f + 0.3f * BoeschungRauschen(x, z, saat));
            var s = Mathf.Clamp(abstand / reichweite, 0f, 1f);
            var boden = BodenHoehe(x, z);
            if (s >= 1f) return boden - unter;
            s = s * s * (3f - 2f * s);
            return Mathf.Lerp(mitte.Y, boden, s) - unter * s;
        }
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var hoehen = new float[2 * zellen + 1, 2 * zellen + 1];
        var (ux, uz) = (mitte.X - zellen * schritt, mitte.Z - zellen * schritt);
        for (var i = 0; i <= 2 * zellen; i++)
            for (var j = 0; j <= 2 * zellen; j++)
                hoehen[i, j] = Hoehe(ux + i * schritt, uz + j * schritt);
        for (var i = 0; i < 2 * zellen; i++)
            for (var j = 0; j < 2 * zellen; j++)
            {
                var (x0, z0, x1, z1) = (ux + i * schritt, uz + j * schritt, ux + (i + 1) * schritt, uz + (j + 1) * schritt);
                var (a, b, c, d) = (hoehen[i, j], hoehen[i + 1, j], hoehen[i + 1, j + 1], hoehen[i, j + 1]);
                // Zelle ganz im Boden (alles unter ihm, nirgends sichtbar) auslassen
                if (a <= BodenHoehe(x0, z0) - unter + 0.001f && b <= BodenHoehe(x1, z0) - unter + 0.001f
                    && c <= BodenHoehe(x1, z1) - unter + 0.001f && d <= BodenHoehe(x0, z1) - unter + 0.001f
                    && a < mitte.Y - 0.001f && b < mitte.Y - 0.001f && c < mitte.Y - 0.001f && d < mitte.Y - 0.001f) continue;
                var (pa, pb, pc, pd) = (new Vector3(x0, a, z0), new Vector3(x1, b, z0), new Vector3(x1, c, z1), new Vector3(x0, d, z1));
                var (ua, ub, uc, ud) = (new Vector2(x0, z0) / 4f, new Vector2(x1, z0) / 4f, new Vector2(x1, z1) / 4f, new Vector2(x0, z1) / 4f);
                // Diagonale wie im Boden (links oben nach rechts unten), im Uhrzeigersinn von oben gesehen = Vorderseite in Godot
                foreach (var (p, uv) in new[] { (pa, ua), (pb, ub), (pc, uc), (pa, ua), (pc, uc), (pd, ud) })
                {
                    st.SetUV(uv);
                    st.AddVertex(p);
                }
            }
        st.GenerateNormals();
        var netz = new MeshInstance3D { Name = "Boeschung", Mesh = st.Commit(), MaterialOverride = Material(Weg) };
        eltern.AddChild(netz);
        return netz;
    }

    /// <summary>Ein Gebäude der Karte auf seinem Fundament: Sockel unter der Fläche (als Kind der Stadt) und die Fläche für <see cref="FussHoehe"/>.</summary>
    private void GebaeudeSockel(Kartengebaeude g)
    {
        var (hoehe, tiefe) = FundamentVon(g);
        if (hoehe == 0 && tiefe == 0) return;
        FundamentMerken(Punkt3(g.Punkt), DrehungVon(g), GroesseVon(g), hoehe);
        var erde = g.Art is Bausteinart.Umlandbetrieb or Bausteinart.Umlandstelle && g.Baustein?.Modell?.StartsWith("wasser/") != true;
        var mitte = new Vector3((float)g.Punkt[0], hoehe, (float)g.Punkt[1]);
        if (erde && !SockelQuader)
            BoeschungBauen(this, mitte, DrehungVon(g), GroesseVon(g), tiefe, (int)Restflaechenfuellung.Wuerfel(g.Kennung, 5));
        else SockelBauen(this, mitte, DrehungVon(g), GroesseVon(g), tiefe, erde);
    }

    /// <summary>Mit <c>--sockelquader=1</c> bleibt der alte Quader aus Erde unter Umlandplätzen (nur für Vergleiche).</summary>
    private static bool SockelQuader => Wasserarten.Startoption("sockelquader") is "1" or "ja" or "an";

    /// <summary>
    /// Ein Gebäude in einer Gruppe, deren Ursprung die Mitte der Fläche ist (Wohnhaus, Baustelle, Bauplatz; die Gruppe steht schon auf der Höhe des
    /// Fußbodens): Sockel darunter als Kind der Gruppe, dazu die Fläche für <see cref="FussHoehe"/>.
    /// </summary>
    private void GruppenSockel(Node3D gruppe, float drehung, Vector2 groesse)
    {
        var (hoehe, tiefe) = FundamentVon(gruppe.Position, drehung, groesse);
        if (hoehe == 0 && tiefe == 0) return;
        FundamentMerken(gruppe.Position, drehung, groesse, hoehe);
        SockelBauen(gruppe, Vector3.Zero, 0, groesse, tiefe);
    }

    /// <summary>
    /// Der Sockel unter dem Platz eines neu gebauten Betriebs (auf festen Plätzen der Karte steht er schon, <see cref="GebaeudeSockel"/>). Der Platz ist
    /// noch ungedreht gebaut – die Gruppe des Betriebs dreht den Sockel mit.
    /// </summary>
    private void PlatzSockel(Vector3 mitte, float drehung, Vector2 groesse)
    {
        var (hoehe, tiefe) = FundamentVon(new Vector3(mitte.X, 0, mitte.Z), drehung, groesse);
        if (hoehe == 0 && tiefe == 0) return;
        FundamentMerken(new Vector3(mitte.X, 0, mitte.Z), drehung, groesse, hoehe);
        SockelBauen(this, new Vector3(mitte.X, hoehe, mitte.Z), 0, groesse, tiefe);
    }

    /// <summary>Merkt die Fläche eines Fundaments (eine neue Fläche an derselben Stelle ersetzt die alte: Häuser werden bei jedem Ausbau neu gebaut).</summary>
    private void FundamentMerken(Vector3 mitte, float drehung, Vector2 groesse, float hoehe)
    {
        if (groesse == Vector2.Zero) return;
        var ecken = FlaechenEcken(mitte, drehung, groesse).Select(V2).ToArray();
        var rahmen = new Rect2(ecken[0], Vector2.Zero);
        foreach (var e in ecken) rahmen = rahmen.Expand(e);
        _fundamente.RemoveAll(f => f.Ecken[0].DistanceTo(ecken[0]) < 0.01f && f.Ecken[2].DistanceTo(ecken[2]) < 0.01f);
        _fundamente.Add((ecken, rahmen, hoehe));
    }

    /// <summary>
    /// Wo Figuren und Karren stehen: die Höhe des Bodens, auf einer Grundfläche mit Fundament (Marktplatz, Hof, Haus) die Höhe des Fußbodens. Ohne
    /// Gelände immer 0.
    /// </summary>
    private float FussHoehe(float x, float z)
    {
        if (!BodenBewegt) return 0f;
        if (DeckHoehe(x, z) is { } deck) return deck; // Brücke, Steg, Fähre (Stadtansicht.Wasser.Uebergaenge.cs)
        if (_fundamente.Count > 0)
        {
            var p = new Vector2(x, z);
            foreach (var (ecken, rahmen, hoehe) in _fundamente)
                if (rahmen.HasPoint(p) && Geometry2D.IsPointInPolygon(p, ecken)) return hoehe;
        }
        return BodenHoehe(x, z);
    }

    private float FussHoehe(Vector3 p) => FussHoehe(p.X, p.Z);

    /// <summary>Der Punkt mit der Höhe, auf der Figuren und Karren dort stehen.</summary>
    private Vector3 AufFuss(Vector3 p) => new(p.X, FussHoehe(p.X, p.Z), p.Z);

    // ═════════════════════════════════════════════════════════════════════════
    //  Boden zeichnen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet den Boden unter <paramref name="eltern"/>: bei einer flachen Karte die eine Ebene (wie bisher), sonst die Kacheln über der ganzen Karte und
    /// ringsum die flache Zugabe (<paramref name="zugabe"/>: so weit liegt Boden über die Außenkante hinaus).
    /// </summary>
    private void GelaendeBauen(Node3D eltern, Kartenrechteck aussen, float zugabe)
    {
        _gelaendeKacheln.Clear();
        _bodenStoffe.Clear();
        _bodenStreifen.Clear();
        _gelaendeKnoten = null;
        BueschelVergessen(); // die Knoten der Grasbüschel hängen am Boden (Stadtansicht.Bueschel.cs)
        _bodenGebaut = BodenKacheln;
        var gross = aussen.Vergroessert(zugabe);
        if (!_bodenGebaut)
        {
            var mitte = gross.Mitte;
            eltern.AddChild(new MeshInstance3D
            {
                Name = "Wiese", Mesh = new PlaneMesh { Size = new Vector2((float)gross.Breite, (float)gross.Tiefe) }, MaterialOverride = Material(Wiese),
                Position = new Vector3((float)mitte.X, -0.05f, (float)mitte.Z),
            });
            return;
        }
        _gelaendeKnoten = new Node3D { Name = "Gelaende" };
        eltern.AddChild(_gelaendeKnoten);
        var (ti0, tj0, ti1, tj1) = KachelBereich(aussen);
        Ladezeit.Teil("Boden: Gewichtsbilder vorrechnen (Parallel)", () => BodenVorrechnen(ti0, tj0, ti1, tj1));
        for (var tj = tj0; tj <= tj1; tj++)
            for (var ti = ti0; ti <= ti1; ti++)
                GelaendeKachelBauen(ti, tj);
        _splatVorrat = null;
        // ringsum flach: über die Kacheln hinaus bis zur Zugabe (ein Streifen je Seite, Ecken zu den Streifen Nord und Süd)
        var k = KachelRechteck(ti0, tj0, ti1, tj1);
        void Streifen(string name, double x0, double z0, double x1, double z1)
        {
            if (x1 - x0 < 0.01 || z1 - z0 < 0.01) return;
            var streifen = new MeshInstance3D
            {
                Name = name, Mesh = new PlaneMesh { Size = new Vector2((float)(x1 - x0), (float)(z1 - z0)) }, MaterialOverride = BodenRandStoff(),
                Position = new Vector3((float)(x0 + x1) / 2, 0f, (float)(z0 + z1) / 2),
            };
            eltern.AddChild(streifen);
            _bodenStreifen.Add(streifen);
        }
        Streifen("Wiese_Nord", gross.X0, gross.Z0, gross.X1, k.Z0);
        Streifen("Wiese_Sued", gross.X0, k.Z1, gross.X1, gross.Z1);
        Streifen("Wiese_West", gross.X0, k.Z0, k.X0, k.Z1);
        Streifen("Wiese_Ost", k.X1, k.Z0, gross.X1, k.Z1);
    }

    /// <summary>Die Kacheln (Kachelindex), die eine Karte deckt.</summary>
    private static (int I0, int J0, int I1, int J1) KachelBereich(Kartenrechteck r)
    {
        const double kachel = KachelZellen * Hoehenraster.Abstand;
        return ((int)Math.Floor(r.X0 / kachel), (int)Math.Floor(r.Z0 / kachel), (int)Math.Ceiling(r.X1 / kachel) - 1, (int)Math.Ceiling(r.Z1 / kachel) - 1);
    }

    private static Kartenrechteck KachelRechteck(int i0, int j0, int i1, int j1)
    {
        const double kachel = KachelZellen * Hoehenraster.Abstand;
        return new Kartenrechteck(i0 * kachel, j0 * kachel, (i1 + 1) * kachel, (j1 + 1) * kachel);
    }

    /// <summary>
    /// Die Kacheln im Rechteck neu bauen (aus der Meldung des Kerns: Pinsel, Rückgängig, Bett eines Weges). Hat sich nur geändert, <i>ob</i> die Karte
    /// Höhen hat, wird der ganze Boden neu gebaut.
    /// </summary>
    private void GelaendeKachelnNeu(Kartenrechteck bereich)
    {
        if (_randKnoten is null || _grenzen is null) return;
        if (BodenKacheln != _bodenGebaut)
        {
            RandNeuBauen();
            return;
        }
        if (!_bodenGebaut) return;
        // ein Rand von 3 Rasterpunkten für Normalen und die Zellen am Rand
        var weit = bereich.Vergroessert((KachelRand - 1) * Hoehenraster.Abstand);
        var (i0, j0, i1, j1) = KachelBereich(weit);
        var (a0, b0, a1, b1) = KachelBereich(_grenzen.Aussen);
        for (var j = Math.Max(j0, b0); j <= Math.Min(j1, b1); j++)
            for (var i = Math.Max(i0, a0); i <= Math.Min(i1, a1); i++)
                GelaendeKachelBauen(i, j);
    }

    /// <summary>Wie fein eine Kachel gezeichnet wird: Schritt in Rasterpunkten (1 = jeder Punkt, 2 = jeder zweite). Im Spiel weit vom Spielfeld gröber.</summary>
    private int KachelSchritt(int ti, int tj)
    {
        if (!GelaendeGrob || _grenzen is null) return 1;
        const double kachel = KachelZellen * Hoehenraster.Abstand;
        var spielfeld = _grenzen.Spielfeld;
        var (mx, mz) = ((ti + 0.5) * kachel, (tj + 0.5) * kachel);
        var d = Math.Max(Math.Max(spielfeld.X0 - mx, mx - spielfeld.X1), Math.Max(spielfeld.Z0 - mz, mz - spielfeld.Z1)) - kachel / 2;
        return d < 64 ? 1 : 2; // nie gröber als 4 m: Äcker (10 cm über dem Boden) und Pflanzen liegen auf dem feinen Boden, das grobe Netz weicht auf unruhigem Gelände bis etwa 7 cm ab
    }

    /// <summary>Baut (oder ersetzt) eine Kachel des Bodens.</summary>
    private void GelaendeKachelBauen(int ti, int tj)
    {
        if (_gelaendeKnoten is null || _gelaende is null) return;
        var (netz, flach) = Ladezeit.Zaehlen("Kachel: Netz", () => KachelNetz(ti, tj));
        if (!_gelaendeKacheln.TryGetValue((ti, tj), out var kachel))
        {
            kachel = new MeshInstance3D { Name = $"Kachel_{ti}_{tj}", Layers = 1 | TorErdeEbene }; // Ebene der Erde im Torweg (Stadtansicht.Torerde.cs)
            _gelaendeKnoten.AddChild(kachel);
            _gelaendeKacheln[(ti, tj)] = kachel;
        }
        kachel.Mesh = netz;
        kachel.MaterialOverride = Ladezeit.Zaehlen("Kachel: Stoff", () => KachelStoff(ti, tj)); // Wiese oder Boden-Shader (Stadtansicht.Boden.cs)
        Ladezeit.Teil("Kachel: Büschel-Höhe", () => BueschelHoeheGeaendert(ti, tj)); // die Grasbüschel stehen auf der neuen Höhe (Stadtansicht.Bueschel.cs)
        kachel.CastShadow = flach ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On;
    }

    /// <summary>Das Netz einer Kachel: ein Viereck, wenn sie (samt der Zellen zur nächsten) ganz flach ist, sonst ein Netz mit Normalen.</summary>
    private (ArrayMesh Netz, bool Flach) KachelNetz(int ti, int tj)
    {
        const int n = KachelZellen, rand = KachelRand, breite = n + 1 + 2 * rand;
        var hoehen = _gelaende!.Ausschnitt(n * ti - rand, n * tj - rand, breite, breite);
        float H(int i, int j) => hoehen[(j + rand) * breite + i + rand];
        const double kachel = n * Hoehenraster.Abstand;
        var netz = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);

        var flach = true;
        for (var j = 0; j <= n && flach; j++)
            for (var i = 0; i <= n; i++)
                if (Mathf.Abs(H(i, j)) > 0.0005f) { flach = false; break; }
        var (x0, z0) = ((float)(ti * kachel), (float)(tj * kachel));
        if (flach)
        {
            var seite = (float)kachel;
            arrays[(int)Mesh.ArrayType.Vertex] = new[] { new Vector3(x0, 0, z0), new Vector3(x0 + seite, 0, z0), new Vector3(x0 + seite, 0, z0 + seite), new Vector3(x0, 0, z0 + seite) };
            arrays[(int)Mesh.ArrayType.Normal] = new[] { Vector3.Up, Vector3.Up, Vector3.Up, Vector3.Up };
            arrays[(int)Mesh.ArrayType.Index] = new[] { 0, 1, 2, 0, 2, 3 };
            netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            return (netz, true);
        }

        var s = KachelSchritt(ti, tj);
        var m = n / s + 1;
        var ecken = new List<Vector3>(m * m);
        var normalen = new List<Vector3>(m * m);
        var abstand = (float)Hoehenraster.Abstand;
        for (var c = 0; c < m; c++)
            for (var a = 0; a < m; a++)
            {
                var (i, j) = (a * s, c * s);
                ecken.Add(new Vector3(x0 + i * abstand, H(i, j), z0 + j * abstand));
                var (dx, dz) = ((H(i + s, j) - H(i - s, j)) / (2 * s * abstand), (H(i, j + s) - H(i, j - s)) / (2 * s * abstand));
                normalen.Add(new Vector3(-dx, 1, -dz).Normalized());
            }
        var indizes = new List<int>(6 * (m - 1) * (m - 1));
        for (var c = 0; c + 1 < m; c++)
            for (var a = 0; a + 1 < m; a++)
            {
                int v00 = c * m + a, v10 = v00 + 1, v01 = v00 + m, v11 = v01 + 1;
                indizes.AddRange(new[] { v00, v10, v11, v00, v11, v01 }); // Diagonale von links oben nach rechts unten (wie BodenHoehe)
            }
        var land = LandAnteile(ecken, x0, z0, (float)kachel);
        if (GelaendeGrob) SchuerzenAnlegen(ecken, normalen, indizes, m, land);
        arrays[(int)Mesh.ArrayType.Vertex] = ecken.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normalen.ToArray();
        if (land is not null) arrays[(int)Mesh.ArrayType.Color] = land.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indizes.ToArray();
        netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return (netz, false);
    }

    /// <summary>
    /// Land oder Wasser je Ecke einer Kachel als Eckenfarbe (Rot): 1 an Land, 0 ab 15 cm unter dem Spiegel eines Flusses, Sees oder Hafenbeckens
    /// (dazwischen weich). Der Boden-Shader zeichnet dort keine Regentropfen: Das Bett scheint durch das Wasser, und die Wasserfläche hat ihre eigenen
    /// Ringe (Regen 4, sonst doppelt am Ufer). Null, wenn die Kachel kein Gewässer berührt (der Shader nimmt dann 1).
    /// </summary>
    private List<Color>? LandAnteile(List<Vector3> ecken, float x0, float z0, float seite)
    {
        if (_gelaende?.Wasser is not { } wasser) return null;
        bool Beruehrt(Kartenrechteck b) => b.X1 >= x0 - 1 && b.X0 <= x0 + seite + 1 && b.Z1 >= z0 - 1 && b.Z0 <= z0 + seite + 1;
        var becken = wasser.Becken.Where(b => Beruehrt(b.Bereich)).ToList();
        if (!wasser.Laeufe.Any(l => Beruehrt(l.Bereich)) && !wasser.Seen.Any(s => Beruehrt(s.Bereich)) && becken.Count == 0) return null;
        var farben = new List<Color>(ecken.Count);
        var unter = false;
        foreach (var e in ecken)
        {
            double? spiegel = wasser.An(e.X, e.Z)?.Spiegel;
            foreach (var b in becken)
                if (b.Enthaelt(new Kartenort(e.X, e.Z))) spiegel = Math.Max(spiegel ?? double.MinValue, b.Spiegel);
            var anteil = spiegel is { } sp ? Mathf.Clamp(1f - ((float)sp - e.Y) / 0.15f, 0f, 1f) : 1f;
            unter |= anteil < 1f;
            farben.Add(new Color(anteil, 1f, 1f));
        }
        return unter ? farben : null;
    }

    /// <summary>
    /// Schürzen: ein Streifen von 3 m nach unten an jeder Kante der Kachel, in beiden Richtungen sichtbar. Wo ein feines Netz an ein grobes grenzt,
    /// fallen die Kanten nicht aufeinander – die Schürze deckt den Riss.
    /// </summary>
    private static void SchuerzenAnlegen(List<Vector3> ecken, List<Vector3> normalen, List<int> indizes, int m, List<Color>? farben = null)
    {
        const float tiefe = 3f;
        var kanten = new List<int[]>();
        kanten.Add(Enumerable.Range(0, m).ToArray());                        // Nord: erste Zeile
        kanten.Add(Enumerable.Range(0, m).Select(a => (m - 1) * m + a).ToArray()); // Süd: letzte Zeile
        kanten.Add(Enumerable.Range(0, m).Select(c => c * m).ToArray());    // West: erste Spalte
        kanten.Add(Enumerable.Range(0, m).Select(c => c * m + m - 1).ToArray()); // Ost: letzte Spalte
        foreach (var kante in kanten)
        {
            var oben = ecken.Count;
            foreach (var v in kante)
            {
                ecken.Add(ecken[v]);
                normalen.Add(normalen[v]);
                farben?.Add(farben[v]);
            }
            var unten = ecken.Count;
            foreach (var v in kante)
            {
                ecken.Add(ecken[v] + new Vector3(0, -tiefe, 0));
                normalen.Add(normalen[v]);
                farben?.Add(farben[v]);
            }
            for (var k = 0; k + 1 < kante.Length; k++)
            {
                int a = oben + k, b = oben + k + 1, c = unten + k, d = unten + k + 1;
                indizes.AddRange(new[] { a, b, d, a, d, c, a, d, b, a, c, d }); // beide Seiten
            }
        }
    }

    /// <summary>
    /// Ein Vieleck (x, z), <paramref name="ueber"/> Meter über dem Boden, genau auf den Dreiecken des Netzes: Jede Zelle des Rasters (2 m) liegt im Vieleck
    /// ganz – dann sind es ihre zwei Dreiecke selbst –, ganz draußen – dann nichts – oder wird an seinem Rand mit den zwei Dreiecken zugeschnitten
    /// (<see cref="Geometry2D.IntersectPolygons"/>). So passt die Fläche ohne Riss auf den Boden (kein Teilen nach Gefühl wie bei <see cref="Dreieck"/>),
    /// auch auf unruhigem Gelände. Gibt je drei Ecken ein Dreieck, Vorderseite nach oben. Mit <paramref name="flach"/> liegen alle Ecken auf dieser Höhe
    /// (die Fläche eines Sees: dieselben Zellen, aber waagrecht).
    /// </summary>
    private List<Vector3> RasterDreiecke(Vector2[] vieleck, float ueber, float? flach = null)
    {
        var ecken = new List<Vector3>();
        if (vieleck.Length < 3) return ecken;
        const float z = (float)Hoehenraster.Abstand;
        var rahmen = new Rect2(vieleck[0], Vector2.Zero);
        foreach (var p in vieleck) rahmen = rahmen.Expand(p);
        var (i0, j0, i1, j1) = ((int)Mathf.Floor(rahmen.Position.X / z), (int)Mathf.Floor(rahmen.Position.Y / z), (int)Mathf.Floor(rahmen.End.X / z), (int)Mathf.Floor(rahmen.End.Y / z));

        // Randzellen: alle, die eine Kante des Vielecks berührt (die Kanten in Schritten von einem halben Meter abgetastet)
        var rand = new HashSet<(int, int)>();
        for (var k = 0; k < vieleck.Length; k++)
        {
            var (a, b) = (vieleck[k], vieleck[(k + 1) % vieleck.Length]);
            var teile = Mathf.Max(1, Mathf.CeilToInt(a.DistanceTo(b) / 0.5f));
            for (var t = 0; t <= teile; t++)
            {
                var q = a.Lerp(b, (float)t / teile);
                rand.Add(((int)Mathf.Floor(q.X / z), (int)Mathf.Floor(q.Y / z)));
            }
        }

        Vector3 Punkt(float x, float zz) => new(x, flach ?? BodenHoehe(x, zz) + ueber, zz); // flach: alle Ecken auf dieser Höhe (Wasserfläche)
        for (var j = j0; j <= j1; j++)
            for (var i = i0; i <= i1; i++)
            {
                var (x0, z0, x1, z1) = (i * z, j * z, (i + 1) * z, (j + 1) * z);
                var (v00, v10, v11, v01) = (new Vector2(x0, z0), new Vector2(x1, z0), new Vector2(x1, z1), new Vector2(x0, z1));
                if (!rand.Contains((i, j)))
                {
                    if (!Geometry2D.IsPointInPolygon(new Vector2(x0 + z / 2, z0 + z / 2), vieleck)) continue;
                    foreach (var p in new[] { v00, v10, v11, v00, v11, v01 }) ecken.Add(Punkt(p.X, p.Y));
                    continue;
                }
                foreach (var dreieck in new[] { new[] { v00, v10, v11 }, new[] { v00, v11, v01 } })
                    foreach (var stueck in Geometry2D.IntersectPolygons(vieleck, dreieck))
                    {
                        var indizes = Geometry2D.TriangulatePolygon(stueck);
                        for (var n = 0; n + 2 < indizes.Length; n += 3)
                        {
                            var (p, q, r) = (stueck[indizes[n]], stueck[indizes[n + 1]], stueck[indizes[n + 2]]);
                            if ((q - p).Cross(r - p) < 0) (q, r) = (r, q); // Vorderseite nach oben
                            ecken.Add(Punkt(p.X, p.Y));
                            ecken.Add(Punkt(q.X, q.Y));
                            ecken.Add(Punkt(r.X, r.Y));
                        }
                    }
            }
        return ecken;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Maus: Strahl gegen das Gelände
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Wo der Strahl durch diese Bildschirmstelle den Boden trifft (mit Höhe), null: er trifft ihn nicht (über dem Horizont, außerhalb der Höhen).</summary>
    private Vector3? BodenTrefferBei(Vector2 bildschirm)
    {
        var von = StadtKamera.ProjectRayOrigin(bildschirm);
        var richtung = StadtKamera.ProjectRayNormal(bildschirm);
        if (richtung.Y >= -1e-4f) return null;
        if (!BodenBewegt)
            return new Plane(Vector3.Up, 0).IntersectsRay(von, richtung);
        // von der höchsten möglichen Höhe aus Schritt für Schritt, bis der Strahl unter den Boden taucht; dann halbieren
        var hoch = (float)Hoehenraster.Hoechste + 2f;
        var tief = (float)Hoehenraster.Tiefste - 2f;
        var t0 = (hoch - von.Y) / richtung.Y;
        var t1 = (tief - von.Y) / richtung.Y;
        if (t0 < 0) t0 = 0;
        var schritt = 1.5f / Mathf.Max(0.2f, new Vector2(richtung.X, richtung.Z).Length()); // etwa 1,5 m am Boden je Schritt
        schritt = Mathf.Min(schritt, 3f);
        var vorher = t0;
        for (var t = t0; t <= t1; t += schritt)
        {
            var p = von + richtung * t;
            if (p.Y <= BodenHoehe(p.X, p.Z))
            {
                float klein = vorher, gross = t;
                for (var n = 0; n < 14; n++)
                {
                    var mitte = (klein + gross) / 2;
                    var q = von + richtung * mitte;
                    if (q.Y <= BodenHoehe(q.X, q.Z)) gross = mitte; else klein = mitte;
                }
                var treffer = von + richtung * gross;
                return new Vector3(treffer.X, BodenHoehe(treffer.X, treffer.Z), treffer.Z);
            }
            vorher = t;
        }
        return null;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Kamera
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Neigung der Kamera (Kind der Drehung): Ihre Höhe folgt dem Boden unter dem Blickpunkt.</summary>
    private Node3D _kameraNeigung = null!;

    private float _kameraHoehe;

    /// <summary>
    /// Jedes Bild: Der Blickpunkt steigt und fällt weich mit dem Boden (in einem Gebäude mit seinem Fußboden). Auf flachen Karten bleibt alles bei 0.
    /// Die Höhe sitzt an der Neigung, nicht am Blickpunkt – so überschreibt sie keine Kamerafahrt, die den Blickpunkt auf y = 0 setzt.
    /// </summary>
    private void KameraHoeheNachfuehren(double delta)
    {
        if (_kameraNeigung is null) return;
        var ziel = 0f;
        if (BodenBewegt)
        {
            var p = _kameraDrehung.Position;
            ziel = _innen is not null ? _innen.Mitte.Y : BodenHoehe(p.X, p.Z);
        }
        if (Mathf.Abs(ziel - _kameraHoehe) < 0.002f) _kameraHoehe = ziel;
        else _kameraHoehe = Mathf.Lerp(_kameraHoehe, ziel, 1 - Mathf.Exp(-(float)delta * 5));
        if (_kameraNeigung.Position.Y != _kameraHoehe) _kameraNeigung.Position = new Vector3(0, _kameraHoehe, 0);
    }

    /// <summary>Stellt die Kamerahöhe sofort auf den Boden unter dem Blickpunkt (nach dem Aufbau der Karte, beim Laden).</summary>
    private void KameraHoeheSofort()
    {
        _kameraHoehe = BodenBewegt ? BodenHoehe(_kameraDrehung.Position.X, _kameraDrehung.Position.Z) : 0f;
        if (_kameraNeigung is not null) _kameraNeigung.Position = new Vector3(0, _kameraHoehe, 0);
    }
}
