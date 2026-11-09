using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Himmel und Horizont für die perspektivischen Kameras (Ego-Perspektive, freie Kamera; Auftrag des Nutzers 09.11.2026) – ein Teil der <c>partial class Stadtansicht</c>.
/// Die Spielkamera blickt schräg von oben auf die Karte und sieht den Horizont nie; Ego und freie Kamera zeigen ihn. Drei Dinge, die nur dann gelten:
/// <list type="bullet">
/// <item><b>Himmel</b> (<c>himmel.gdshader</c>, ein <see cref="Sky"/> im Hintergrund der Umgebung): Verlauf vom dunstigen Horizont zum Zenit, Wolken im Wind,
/// bei Nacht Sterne, ein Hof um Sonne und Mond. Die Farben bleiben die der Lichtstimmung (<see cref="LichtNachUhrzeit"/>: Tageszeit, Jahreszeit, Wetter) –
/// Zenit = Hintergrundfarbe, Horizont = Farbe des Dunstrings (<see cref="DunstFarbeNachfuehren"/>), damit der Boden ohne Kante in den Himmel übergeht.
/// Zeichnen kostet der Himmel nur dort, wo kein Gelände davor steht (Tiefentest); die Spiegelung (Rückstrahlung) bleibt die einfarbige Himmelsfarbe wie früher.</item>
/// <item><b>Fernland</b> (<c>fernland.gdshader</c>, <see cref="FernlandBauen"/>): ein flaches Netz mit sanften Hügeln und Flicken aus Äckern, Weiden und Wald hinter
/// dem Dunstring der Karte, das mit dem Abstand in den Dunst ausläuft. Der Dunstring selbst deckt dort nur noch zu <see cref="RingDeckung"/> zu.</item>
/// <item><b>Kulissenberge</b> (<c>kulisse.gdshader</c>): ihr Dunst nimmt die Himmelsfarbe in der Blickrichtung an (Luftperspektive); das gehört zu
/// <see cref="KulisseAusKarte"/>, die Farben dafür setzt <see cref="HimmelBild"/>.</item>
/// </list>
/// Mit der Spielkamera (und in der Kartenwerkstatt, in Räumen) ändert sich nichts: Der Hintergrund bleibt einfarbig, das Fernland ist unsichtbar, der Ring
/// deckt voll zu – kein zusätzlicher Zeichenaufruf, kein Himmel.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Wie dicht der Dunstring außen wird, solange man den Horizont sieht (die Spielkamera: 1).</summary>
    private const float RingDeckung = 0.45f;

    /// <summary>Das Fernland reicht so weit (m) hinter den Dunstring.</summary>
    private const float FernlandReichweite = 3200f;

    /// <summary>Startoption <c>--himmel=0</c>: kein Himmel, kein Fernland, voller Dunstring (der Zustand vor dem 09.11.2026; für Vorher-Nachher-Bilder und Messungen).</summary>
    private static readonly bool HimmelGewuenscht = Wasserarten.Startoption("himmel") is not ("0" or "aus" or "nein");

    private Sky? _himmelSky;
    private ShaderMaterial? _himmelStoff;
    private MeshInstance3D? _fernland;
    private ShaderMaterial? _fernlandStoff;
    private bool _himmelAn;
    private Vector2 _himmelDrift;

    /// <summary>Wird der Himmel gerade gezeigt (perspektivische Kamera auf der Spielkarte)?</summary>
    public bool HimmelSichtbar => _himmelAn;

    // ═════════════════════════════════════════════════════════════════════════
    //  Himmel
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Jedes Bild (aus <see cref="GrafikBild"/>): den Himmel zu- oder abschalten und seine Werte nachführen.</summary>
    private void HimmelBild(double delta)
    {
        var perspektive = HimmelGewuenscht && FreiAktiv && _raum is null && _innen is null && _entwurf is null;
        if (perspektive != _himmelAn) HimmelUmschalten(perspektive);
        if (!_himmelAn || _himmelStoff is null) return;

        var umgebung = _umgebung.Environment;
        // Horizont: die Hintergrundfarbe der Tageszeit, genau wie der Dunstring (er nimmt mit dem Himmel die Farbe unverändert, DunstFarbeNachfuehren); Zenit: so tief
        // wie der frühere einfarbige Hintergrund (Godot zeichnete ihn dunkler als dieselbe Farbe auf einer Fläche: einmal mehr von sRGB nach linear)
        var horizont = umgebung.BackgroundColor;
        var zenit = horizont.SrgbToLinear();

        // Sonne oder Mond: ein Licht (Stadtansicht.Licht.cs); die Richtung zu ihm ist die +Z-Achse des Lichts
        var licht = _sonne.LightColor;
        var istSonne = licht.R > licht.B;
        var sonne = istSonne ? Mathf.Clamp(_sonne.LightEnergy / SonnenMaximum, 0f, 1f) : 0f;
        var mond = istSonne ? 0f : Mathf.Clamp(_sonne.LightEnergy / 0.35f, 0f, 1f);
        var richtung = _sonne.GlobalBasis.Z.Normalized();

        // Wolken: Deckung aus dem Wetter (klar: vereinzelte Flecken), Drift mit dem Wind; bei Pause stehen sie still
        var deckung = Mathf.Clamp(0.22f + 0.9f * _wetter.Wolken, 0f, 1f);
        var truebe = Mathf.Clamp(1.2f * _wetter.Regen + 0.8f * _wetter.Schnee + 0.6f * (_wetter.Wolken - 0.5f), 0f, 1f);
        if (_simulation is not { Takt.IstPausiert: true })
            _himmelDrift += _windRichtung * (0.006f + 0.02f * _wetter.Wind) * (float)delta;
        var (nebelLand, _) = NebelStaerkeJetzt();
        var dunst = Mathf.Clamp(Mathf.Max(_wetter.Nebel, Mathf.Max(0.6f * _wetter.Regen, 0.5f * nebelLand)), 0f, 1f);

        // Die Wolken ziehen über eine globale Uniform (sie ändert den Stoff nicht: Godot zeichnet die Rückstrahlung des Himmels nur bei Änderung der Werte neu);
        // alles andere ändert sich langsam und wird nur bei merklichem Unterschied gesetzt
        RenderingServer.GlobalShaderParameterSet("himmel_drift", _himmelDrift);
        HimmelSetzen("zenit", zenit, new Vector4(zenit.R, zenit.G, zenit.B, 0));
        HimmelSetzen("horizont", horizont, new Vector4(horizont.R, horizont.G, horizont.B, 0));
        HimmelSetzen("sonne_richtung", richtung, new Vector4(richtung.X, richtung.Y, richtung.Z, 0));
        HimmelSetzen("sonne_farbe", new Color(licht.R, licht.G, licht.B), new Vector4(licht.R, licht.G, licht.B, 0));
        HimmelSetzen("sonne_staerke", sonne, new Vector4(sonne, 0, 0, 0));
        HimmelSetzen("mond_staerke", mond, new Vector4(mond, 0, 0, 0));
        HimmelSetzen("nacht", NebelDunkel(), new Vector4(NebelDunkel(), 0, 0, 0));
        HimmelSetzen("deckung", deckung, new Vector4(deckung, 0, 0, 0));
        HimmelSetzen("truebe", truebe, new Vector4(truebe, 0, 0, 0));
        HimmelSetzen("nebel", Mathf.Clamp(_wetter.Nebel, 0f, 1f), new Vector4(Mathf.Clamp(_wetter.Nebel, 0f, 1f), 0, 0, 0));

        // Kulissenberge und Fernland: Himmelsfarbe und Wetter-Dunst (die Kulisse liest den Rest in KulisseBild)
        _kulisseStoff?.SetShaderParameter("zenit", zenit);
        if (_fernlandStoff is not null)
        {
            _fernlandStoff.SetShaderParameter("himmel", horizont);
            _fernlandStoff.SetShaderParameter("dunst", dunst);
        }
    }

    private readonly Dictionary<string, Vector4> _himmelWerte = new();

    /// <summary>Setzt einen Wert des Himmels, wenn er sich merklich geändert hat (Godot rechnet die Rückstrahlung des Himmels bei jeder Änderung neu).</summary>
    private void HimmelSetzen(string name, Variant wert, Vector4 vergleich)
    {
        if (_himmelWerte.TryGetValue(name, out var alt) && (alt - vergleich).Length() < 0.004f) return;
        _himmelWerte[name] = vergleich;
        _himmelStoff!.SetShaderParameter(name, wert);
    }

    /// <summary>Schaltet den Himmel (und das Fernland, den schwächeren Dunstring) zu oder ab. Aus: Hintergrund einfarbig wie bisher.</summary>
    private void HimmelUmschalten(bool an)
    {
        _himmelAn = an;
        _dunstFarbe = new Color(-1, 0, 0); // der Dunstring nimmt mit dem Himmel eine andere Farbe an (DunstFarbeNachfuehren)
        var umgebung = _umgebung.Environment;
        if (an)
        {
            if (_himmelSky is null)
            {
                RenderingServer.GlobalShaderParameterAdd("himmel_drift", RenderingServer.GlobalShaderParameterType.Vec2, Vector2.Zero); // vor dem Laden des Shaders
                _himmelStoff = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/himmel.gdshader"), ResourceName = "Himmel" };
                _himmelSky = new Sky
                {
                    SkyMaterial = _himmelStoff,
                    // Rückstrahlung wie früher einfarbig (im Shader), klein und nur bei Änderung der Farben neu gerechnet: kostet kaum etwas
                    ProcessMode = Sky.ProcessModeEnum.Quality,
                    RadianceSize = Sky.RadianceSizeEnum.Size32,
                };
            }
            umgebung.Sky = _himmelSky;
            umgebung.BackgroundMode = Godot.Environment.BGMode.Sky;
            umgebung.FogSkyAffect = 0f; // Nebel und Dunst des Wetters färbt der Himmel selbst (nebel), Godots Nebel soll ihn nicht überdecken
        }
        else umgebung.BackgroundMode = Godot.Environment.BGMode.Color;
        _dunstRing?.SetShaderParameter("deckung_max", an ? RingDeckung : 1f);
        if (_fernland is not null) _fernland.Visible = an;
        GD.Print($"[himmel] {(an ? "an (Himmel, Fernland, Dunstring auf " + RingDeckung.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ")" : "aus (Hintergrund einfarbig)")}");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Fernland
    // ═════════════════════════════════════════════════════════════════════════

    private FastNoiseLite? _fernRauschen;

    /// <summary>Höhe des Fernlandes bei (x, z): am Rand des Dunstrings eben (knapp unter dem Boden), dann sanfte Hügel, bis zu etwa 40 m.</summary>
    private float FernlandHoehe(float x, float z, Kartenrechteck ring)
    {
        var dx = Mathf.Max(Mathf.Max((float)ring.X0 - x, x - (float)ring.X1), 0f);
        var dz = Mathf.Max(Mathf.Max((float)ring.Z0 - z, z - (float)ring.Z1), 0f);
        var abstand = Mathf.Sqrt(dx * dx + dz * dz);
        var rampe = Mathf.SmoothStep(0f, 700f, abstand);
        var grob = _fernRauschen!.GetNoise2D(x, z) * 0.5f + 0.5f;
        var fein = _fernRauschen.GetNoise2D(x * 3.1f + 100f, z * 3.1f) * 0.5f + 0.5f;
        return -0.25f + rampe * (6f + 30f * grob + 6f * fein);
    }

    /// <summary>Gitterlinien eines Streifens von <paramref name="a"/> bis <paramref name="b"/> (die Karte, ein Feld) und nach außen immer weiter auseinander, bis <see cref="FernlandReichweite"/>.</summary>
    private static float[] FernlandLinien(float a, float b)
    {
        var links = new List<float>();
        var rechts = new List<float>();
        var schritt = 40f;
        for (var s = 0f; s < FernlandReichweite; s += schritt, schritt *= 1.13f) { links.Add(a - s); rechts.Add(b + s); }
        links.Reverse();
        links.AddRange(rechts);
        return links.ToArray();
    }

    /// <summary>
    /// Das Fernland um die Karte: ein Netz ohne die Fläche der Karte selbst (dort liegt der Boden), außen immer gröber. <paramref name="ring"/> ist das Rechteck, bis
    /// zu dem der Dunstring reicht, <paramref name="innen"/> das, in dem der Dunst noch nicht beginnt.
    /// </summary>
    private void FernlandBauen(Kartenrechteck ring, Kartenrechteck innen)
    {
        _fernland = null;
        _fernlandStoff = null;
        if (_entwurf is not null || _randKnoten is null) return;
        _fernRauschen ??= new FastNoiseLite { Seed = 4711, Frequency = 1f / 520f, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth };
        var xs = FernlandLinien((float)ring.X0, (float)ring.X1);
        var zs = FernlandLinien((float)ring.Z0, (float)ring.Z1);
        var nx = xs.Length;
        var eckpunkte = new Vector3[nx * zs.Length];
        var normalen = new Vector3[eckpunkte.Length];
        const float d = 12f;
        for (var j = 0; j < zs.Length; j++)
            for (var i = 0; i < nx; i++)
            {
                var (x, z) = (xs[i], zs[j]);
                eckpunkte[j * nx + i] = new Vector3(x, FernlandHoehe(x, z, ring), z);
                var hx = FernlandHoehe(x + d, z, ring) - FernlandHoehe(x - d, z, ring);
                var hz = FernlandHoehe(x, z + d, ring) - FernlandHoehe(x, z - d, ring);
                normalen[j * nx + i] = new Vector3(-hx, 2f * d, -hz).Normalized();
            }
        var indizes = new List<int>();
        for (var j = 0; j < zs.Length - 1; j++)
            for (var i = 0; i < nx - 1; i++)
            {
                // Die Fläche der Karte selbst bleibt leer: dort liegt der Boden
                if (xs[i] >= (float)ring.X0 - 0.01f && xs[i + 1] <= (float)ring.X1 + 0.01f && zs[j] >= (float)ring.Z0 - 0.01f && zs[j + 1] <= (float)ring.Z1 + 0.01f) continue;
                var (a, b, c, e) = (j * nx + i, j * nx + i + 1, (j + 1) * nx + i + 1, (j + 1) * nx + i);
                indizes.AddRange(new[] { a, b, c, a, c, e }); // Vorderseite nach oben (gleiche Reihenfolge wie DreieckFlach beim Dunstring)
            }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = eckpunkte;
        arrays[(int)Mesh.ArrayType.Normal] = normalen;
        arrays[(int)Mesh.ArrayType.Index] = indizes.ToArray();
        var netz = new ArrayMesh();
        netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        _fernlandStoff = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/fernland.gdshader"), ResourceName = "Fernland" };
        _fernlandStoff.SetShaderParameter("innen", new Vector4((float)innen.X0, (float)innen.Z0, (float)innen.X1, (float)innen.Z1));
        _fernlandStoff.SetShaderParameter("rand_aus", (float)Math.Min(ring.X1 - innen.X1, ring.Z1 - innen.Z1));
        _fernlandStoff.SetShaderParameter("anfang_dunst", RingDeckung);
        _fernland = new MeshInstance3D
        {
            Name = "Fernland", Mesh = netz, MaterialOverride = _fernlandStoff, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            GIMode = GeometryInstance3D.GIModeEnum.Disabled, Visible = _himmelAn,
            ExtraCullMargin = 4000f,
        };
        _randKnoten.AddChild(_fernland);
    }
}
