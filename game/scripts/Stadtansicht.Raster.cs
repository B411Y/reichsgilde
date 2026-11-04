using Godot;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Das Raster als Hilfe (Nutzer, 04.11.2026) – ein Teil der <c>partial class Stadtansicht</c>. Nur im Baumodus des Spiels und in der Kartenwerkstatt
/// liegen feine Linien alle 2 m (das Raster der Karte) und kräftigere alle 10 m auf dem Gelände. Sie folgen der Höhe des Bodens, gibt es nur in der
/// Nähe der Maus (bzw. des Umrisses beim Bauen) und blenden zum Rand hin aus. Das Netz wird nur neu gebaut, wenn die Maus in ein anderes Rasterfeld
/// wechselt oder sich der Zoom ändert (kein Flimmern, kaum Kosten).
/// <list type="bullet">
/// <item><b>Taste V</b> blendet das Raster ein und aus (<see cref="RasterAn"/>, Standard: an); Startoption <c>--raster=0|1</c>.</item>
/// <item><b>Linienbreite</b> richtet sich nach der Weltpixelgröße des Pixel-Looks (<see cref="Fenster.PixelGroesse"/>): genau 1 Weltpixel (beide Stärken), damit
/// die Linien im Pixel-Look nicht flimmern; der Unterschied liegt nur in der Deckkraft (0,15 und 0,30). In Bauzonen entfallen die feinen Linien (die Zone hat ihr eigenes Gitter). Ist die Karte so weit herausgezoomt, dass die Linien zu dicht würden, bleibt das Raster aus.</item>
/// <item><b>Einrasten</b> in der Kartenwerkstatt (<see cref="RasterEinrasten"/>): Ecken von Wegen, Flächen und Mauern rasten auf das Raster,
/// solange das Raster an ist und Alt nicht gehalten wird; mit Umschalt rasten Wege und Mauern zusätzlich in 45°-Schritten zum letzten Punkt
/// (<see cref="Winkelfang"/>). Im Spiel gibt es nichts zu zeichnen: Gebäude, Bauplätze und Felder rasten schon im Kern auf 2 m und 90°
/// (<c>Bauwesen.Einrasten</c>); das Raster zeigt das nur.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Raster-Hilfslinien an (Taste V; Startoption <c>--raster=0</c> schaltet sie ab).</summary>
    public static bool RasterAn { get; set; } = Wasserarten.Startoption("raster") is not ("0" or "aus" or "nein");

    private MeshInstance3D? _rasterNetz;
    private (int Mx, int Mz, int Breite, int Radius)? _rasterStand;
    private Vector2 _rasterLetzte;
    private bool _rasterLetzteGesetzt;

    private static readonly Color RasterFein = new(0.93f, 0.90f, 0.78f, 0.15f), RasterStark = new(0.98f, 0.92f, 0.68f, 0.30f);

    /// <summary>Soll das Raster gerade gezeigt werden (Baumodus ohne Wege-Modus, oder Kartenwerkstatt)?</summary>
    private bool RasterGefragt => RasterAn && _raum is null && _innen is null && (_entwurf is not null || (_baumodus && !_wegeModus));

    /// <summary>Das Raster der Karte in Metern (2).</summary>
    private float RasterMass => (float)(_entwurf?.Raster ?? _karte?.Raster ?? 2.0);

    /// <summary>Taste V: Raster ein/aus. Gibt true zurück, wenn die Taste verbraucht wurde (nur im Baumodus und in der Kartenwerkstatt).</summary>
    private bool RasterTaste(InputEvent eingabe)
    {
        if (eingabe is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.V, CtrlPressed: false, AltPressed: false, ShiftPressed: false, MetaPressed: false }) return false;
        if (_raum is not null || _innen is not null || !(_entwurf is not null || (_baumodus && !_wegeModus))) return false;
        if (GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit) return false;
        RasterAn = !RasterAn;
        if (_entwurf is not null) WerkstattMeldung?.Invoke(RasterAn ? "Raster an (Taste V)" : "Raster aus (Taste V)", false);
        return true;
    }

    /// <summary>Im Baumodus jedes Bild: Das Raster liegt um den Umriss (beim Platzieren) bzw. die Maus.</summary>
    private void RasterImBaumodus()
    {
        if (_entwurf is not null) return; // die Kartenwerkstatt ruft RasterBild selbst
        Vector2? mitte = null;
        if (BauPlatziertGerade && BauAuftrag is { } b) mitte = new Vector2((float)b.X, (float)b.Z);
        else if (_bauOrt is { } ort) mitte = ort;
        else if (GetViewport().GuiGetHoveredControl() is null) mitte = BodenpunktBei(GetViewport().GetMousePosition());
        RasterBild(mitte);
    }

    /// <summary>Zeichnet das Raster um <paramref name="wunsch"/> (null: wo es zuletzt war, zu Beginn die Kameramitte) – oder blendet es aus.</summary>
    private void RasterBild(Vector2? wunsch)
    {
        if (!RasterGefragt)
        {
            if (_rasterNetz is not null) _rasterNetz.Visible = false;
            return;
        }
        if (wunsch is { } w) { _rasterLetzte = w; _rasterLetzteGesetzt = true; }
        else if (!_rasterLetzteGesetzt) { _rasterLetzte = new Vector2(_kameraDrehung.Position.X, _kameraDrehung.Position.Z); _rasterLetzteGesetzt = true; }

        var breite = MeterJePixel * Fenster.PixelGroesse; // Meter je Linie: genau 1 Weltpixel
        if (breite > 0.35f) // zu weit heraus: die Linien lägen dicht an dicht
        {
            if (_rasterNetz is not null) _rasterNetz.Visible = false;
            return;
        }
        var raster = RasterMass;
        var radius = Mathf.Clamp(Mathf.Round(_kamera.Size * 0.5f / 4f) * 4f, 20f, 48f);
        var stand = ((int)Mathf.Round(_rasterLetzte.X / raster), (int)Mathf.Round(_rasterLetzte.Y / raster), (int)Mathf.Round(breite * 20f), (int)radius);
        if (_wsGelaendeStrich || _wsBodenStrich) _rasterStand = null; // das Gelände ändert sich unter dem Pinsel
        if (_rasterNetz is null)
        {
            _rasterNetz = new MeshInstance3D
            {
                Name = "Raster", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                },
            };
            AddChild(_rasterNetz);
        }
        _rasterNetz.Visible = true;
        if (_rasterStand == stand) return;
        _rasterStand = stand;
        _rasterNetz.Mesh = RasterNetzBauen(stand.Item1 * raster, stand.Item2 * raster, raster, radius, breite);
    }

    /// <summary>Baut die Linien um die Mitte (cx, cz): feine alle <paramref name="raster"/>, kräftige jede fünfte; Alpha fällt zum Rand hin auf null.</summary>
    private ArrayMesh RasterNetzBauen(float cx, float cz, float raster, float radius, float breite)
    {
        var ecken = new List<Vector3>();
        var farben = new List<Color>();
        var n = Mathf.CeilToInt(radius / raster);
        var (mx, mz) = ((int)Mathf.Round(cx / raster), (int)Mathf.Round(cz / raster));
        const float anheben = 0.12f;

        static float Ausblenden(float abstand, float radius)
        {
            var t = Mathf.Clamp(abstand / radius, 0f, 1f);
            var s = Mathf.Clamp((t - 0.35f) / 0.65f, 0f, 1f);
            return 1f - s * s * (3f - 2f * s);
        }

        void Linie(bool querZuX, int index, bool stark)
        {
            var feste = index * raster;
            var versatz = feste - (querZuX ? cx : cz);
            if (Mathf.Abs(versatz) >= radius) return;
            var halbe = Mathf.Sqrt(radius * radius - versatz * versatz);
            var mitte = querZuX ? cz : cx;
            var (von, bis) = (Mathf.Floor((mitte - halbe) / raster) * raster, Mathf.Ceil((mitte + halbe) / raster) * raster);
            var w = breite / 2f; // beide Stärken 1 Weltpixel breit, die starke nur deckender
            var zonen = _baumodus && _bauzonen is { Visible: true } && _entwurf is null ? _karte?.Bauzonen : null;
            var farbe = stark ? RasterStark : RasterFein;
            for (var s = von; s < bis - 0.001f; s += raster)
            {
                var (a, b) = (s, s + raster);
                Vector3 P(float t, float seite) => querZuX
                    ? new Vector3(feste + seite, 0, t) : new Vector3(t, 0, feste + seite);
                float Alpha(float t) => farbe.A * Ausblenden(querZuX ? new Vector2(feste - cx, t - cz).Length() : new Vector2(t - cx, feste - cz).Length(), radius);
                var (alphaA, alphaB) = (Alpha(a), Alpha(b));
                if (alphaA < 0.01f && alphaB < 0.01f) continue;
                // In Bauzonen zeichnen die Zonen ihr 2-m-Gitter selbst: die feinen Linien entfallen dort, die 10-m-Linien bleiben, blasser
                if (zonen is not null)
                {
                    var (mx2, mz2) = querZuX ? (feste, (a + b) / 2f) : ((a + b) / 2f, feste);
                    if (zonen.Any(z => z.Enthaelt(mx2, mz2))) { if (!stark) continue; alphaA *= 0.5f; alphaB *= 0.5f; }
                }
                Vector3 Hoch(Vector3 p) => new(p.X, BodenHoehe(p.X, p.Z) + anheben, p.Z);
                var q = new[] { Hoch(P(a, -w)), Hoch(P(a, w)), Hoch(P(b, w)), Hoch(P(b, -w)) };
                var c = new[] { new Color(farbe, alphaA), new Color(farbe, alphaA), new Color(farbe, alphaB), new Color(farbe, alphaB) };
                foreach (var i in new[] { 0, 1, 2, 0, 2, 3 }) { ecken.Add(q[i]); farben.Add(c[i]); }
            }
        }

        const int Stark = 5; // jede fünfte Linie = 10 m
        for (var i = mx - n; i <= mx + n; i++)
        {
            var stark = ((i % Stark) + Stark) % Stark == 0;
            Linie(true, i, stark);
        }
        for (var j = mz - n; j <= mz + n; j++)
        {
            var stark = ((j % Stark) + Stark) % Stark == 0;
            Linie(false, j, stark);
        }

        var netz = new ArrayMesh();
        if (ecken.Count > 0)
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = ecken.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = farben.ToArray();
            netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        }
        return netz;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Einrasten in der Kartenwerkstatt
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Rastet das Zeichnen gerade auf das Raster (Raster an, Alt nicht gehalten)?</summary>
    private static bool RasterRastet => RasterAn && !Input.IsKeyPressed(Key.Alt);

    /// <summary>Der Punkt auf dem nächsten Rasterpunkt (Raster der Karte); mit Alt oder bei ausgeblendetem Raster unverändert.</summary>
    private Vector2 RasterEinrasten(Vector2 p) => RasterRastet && _entwurf is { } e
        ? new Vector2((float)Kartenraster.Runden(p.X, e.Raster), (float)Kartenraster.Runden(p.Y, e.Raster))
        : p;

    /// <summary>
    /// Winkelfang (Umschalt gehalten): der Punkt <paramref name="p"/> rückt auf die nächste der acht Richtungen (0°, 45°, 90° …) vom letzten gesetzten
    /// Punkt aus – bei eingerastetem Raster auf Rasterpunkte (Achse: ganze Rasterschritte, Diagonale: gleich viele Schritte in x und z), sonst
    /// mit der Länge der Mausstrecke. Ohne gesetzten Punkt oder ohne Umschalt unverändert.
    /// </summary>
    private Vector2 Winkelfang(Vector2 p)
    {
        if (_wsPunkte.Count == 0 || !Input.IsKeyPressed(Key.Shift)) return p;
        var a = _wsPunkte[^1];
        var d = p - a;
        if (d.LengthSquared() < 1e-6f) return p;
        if (RasterRastet && _entwurf is { } e)
        {
            var r = (float)e.Raster;
            float Q(float v) => Mathf.Round(v / r) * r;
            var diag = Q((Mathf.Abs(d.X) + Mathf.Abs(d.Y)) / 2f);
            var kandidaten = new[]
            {
                new Vector2(Q(d.X), 0), new Vector2(0, Q(d.Y)),
                new Vector2(Mathf.Sign(d.X) * diag, Mathf.Sign(d.Y) * diag),
            };
            return a + kandidaten.OrderBy(k => k.DistanceSquaredTo(d)).First();
        }
        var winkel = Mathf.Round(d.Angle() / (Mathf.Pi / 4f)) * (Mathf.Pi / 4f);
        var richtung = new Vector2(Mathf.Cos(winkel), Mathf.Sin(winkel));
        return a + richtung * Mathf.Max(0f, d.Dot(richtung));
    }
}
