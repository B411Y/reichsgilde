using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das Porträt einer Person (PR #211): gemalter Hintergrund nach Standgruppe, davor das freigestellte Brustbild nach Volk, Geschlecht, Gesicht,
/// Altersstufe und Kleidung; Haar und Augen färbt <c>portraet.gdshader</c> über Masken um. Das Bild lebt ein wenig: Blinzeln (alle 3–7 s, unregelmäßig),
/// Atmen (die Person wächst um unten mittig ±0,6 %, der Hintergrund bleibt), Kerzenlicht (warmes, leises Flackern; in der Grafikstufe Niedrig aus) und
/// ein kleines Nicken (<see cref="Nicken"/>, für Gespräche).
///
/// Bilder: <c>res://assets/oberflaeche/portraets/&lt;volk&gt;/&lt;geschlecht&gt;_&lt;gesicht&gt;_&lt;alter&gt;_&lt;kleidung&gt;.webp</c> (Rückfall .png) (512 px, freigestellt, .webp, Rückfall .png), dazu
/// <c>…_zu.webp</c> (geschlossene Augen, je Altersstufe), <c>…_haar.webp</c> und <c>…_augen.webp</c> (Masken) und
/// <c>…/portraets/hintergrund_&lt;standgruppe&gt;.webp</c>. Fehlt ein Bild, zeigt das Porträt eine Silhouette vor dem Hintergrund – das ist kein Fehler.
/// Fehlen die Masken, bleibt es bei den Farben des Bildes; fehlt das Bild mit geschlossenen Augen, blinzelt es nicht.
///
/// Bildfeld wie das Wappen: quadratisch, <see cref="Neu"/> nimmt die Kantenlänge (groß 256, mittel 128, klein 64).
/// </summary>
public partial class PortraetAnzeige : Control
{
    public const int Gross = 256;
    public const int Mittel = 128;
    public const int Klein = 64;

    private const string Ordner = "res://assets/oberflaeche/portraets/";
    private static readonly Shader PersonShader = GD.Load<Shader>("res://assets/shader/portraet.gdshader");
    private static readonly Dictionary<string, Texture2D?> Speicher = new();

    // ── Farben zum Umfärben (Mittelwerte; das Bild liefert die Helligkeit) ──
    public static Color Farbe(Haarfarbe h) => h switch
    {
        Haarfarbe.Schwarz => new Color("2b2623"),
        Haarfarbe.Dunkelbraun => new Color("4a3324"),
        Haarfarbe.Braun => new Color("6b4a2f"),
        Haarfarbe.Kastanie => new Color("8a4e2b"),
        Haarfarbe.Rot => new Color("93502f"),
        Haarfarbe.Blond => new Color("c9a45a"),
        Haarfarbe.Hellblond => new Color("e0cd98"),
        Haarfarbe.Silberblond => new Color("d8d9dc"),
        _ => new Color("7a7a6e"),
    };

    public static Color Farbe(Augenfarbe a) => a switch
    {
        Augenfarbe.Dunkelbraun => new Color("3a2415"),
        Augenfarbe.Braun => new Color("6a4426"),
        Augenfarbe.Haselnuss => new Color("8a6a2c"),
        Augenfarbe.Gruen => new Color("4e7a44"),
        Augenfarbe.Grau => new Color("7d868c"),
        Augenfarbe.Blau => new Color("4b78b0"),
        Augenfarbe.Violett => new Color("7a5aa8"),
        Augenfarbe.Bernstein => new Color("c8921e"),
        _ => new Color("8e3a2c"),
    };

    public static string FarbName(Haarfarbe h) => h switch
    {
        Haarfarbe.Dunkelbraun => "dunkelbraun", Haarfarbe.Silberblond => "silberblond", Haarfarbe.Hellblond => "hellblond",
        _ => h.ToString().ToLowerInvariant(),
    };

    public static string FarbName(Augenfarbe a) => a switch { Augenfarbe.Gruen => "grün", Augenfarbe.Dunkelbraun => "dunkelbraun", _ => a.ToString().ToLowerInvariant() };

    /// <summary>Hintergrundton, wenn die Bilddatei fehlt: Lehm, Holz, Wandteppich, dunkles Gewölbe.</summary>
    private static (Color Oben, Color Unten) Grund(Standgruppe g) => g switch
    {
        Standgruppe.Gemeiner => (new Color("8d6e4e"), new Color("5e4630")),
        Standgruppe.Buerger => (new Color("6b4a2c"), new Color("40291a")),
        Standgruppe.Patrizier => (new Color("6e2f34"), new Color("3a1a1f")),
        _ => (new Color("2c2f3c"), new Color("14161d")),
    };

    // ── Zustand ──
    private readonly TextureRect _grund = new();
    private readonly TextureRect _person = new();
    private readonly Silhouette _silhouette = new();
    private readonly ShaderMaterial _material = new();
    private bool _lebendig = true;
    private Standgruppe _gruppe;
    private double _zeit;
    private double _blinzelnIn;
    private double _blinzelnT = -1;
    private double _nickenT = -1;
    private bool _hatZu;
    private readonly double _phase = GD.Randf() * 100;

    /// <summary>Ein Porträt in <paramref name="groesse"/> Pixeln Kantenlänge. Ohne <paramref name="lebendig"/> steht es still (Vorschau, Bildschirmfoto).</summary>
    public static PortraetAnzeige Neu(int groesse, bool lebendig = true) => new()
    {
        CustomMinimumSize = new Vector2(groesse, groesse),
        MouseFilter = MouseFilterEnum.Pass,
        SizeFlagsVertical = SizeFlags.ShrinkCenter,
        _lebendig = lebendig,
    };

    public override void _Ready()
    {
        ClipContents = true;
        _grund.TextureFilter = _person.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        _grund.TextureRepeat = _person.TextureRepeat = TextureRepeatEnum.Disabled;
        foreach (var t in new[] { _grund, _person })
        {
            t.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            t.StretchMode = TextureRect.StretchModeEnum.Scale;
            t.MouseFilter = MouseFilterEnum.Ignore;
            t.SetAnchorsPreset(LayoutPreset.FullRect);
        }
        _silhouette.MouseFilter = MouseFilterEnum.Ignore;
        _silhouette.SetAnchorsPreset(LayoutPreset.FullRect);
        _material.Shader = PersonShader;
        _person.Material = _material;
        AddChild(_grund);
        AddChild(_silhouette);
        AddChild(_person);
        _blinzelnIn = 1 + GD.Randf() * 4;
        SetProcess(_lebendig);
    }

    /// <summary>Zeigt eine Person des Familienbuchs: Stand, Kleidung und Altersstufe kommen aus dem Kern.</summary>
    public void Zeigen(Familienbuch buch, Person p) =>
        Zeigen(p.Volk, p.Geschlecht, p.Aussehen, buch.AltersstufeVon(p), buch.KleidungVon(p), buch.StandgruppeVon(p));

    /// <summary>Das Oberhaupt eines Hauses (Spieler oder KI). False, wenn das Haus keine Familie hat (alter Spielstand) – dann zeigt das Porträt nichts.</summary>
    public bool ZeigenHaus(Stadtwirtschaft w, Konto haus)
    {
        if (w.Familien.Von(haus)?.Oberhaupt is not { } o) return false;
        Zeigen(w.Familien, o);
        return true;
    }

    /// <summary>Der Inhaber eines Sitzes im Rat: ein Ratsherr der Stadt oder das Oberhaupt eines Hauses. False bei einem freien Sitz.</summary>
    public bool ZeigenAmt(Stadtwirtschaft w, Amt amt)
    {
        if (amt.Ratsherr is { } r) { ZeigenRatsherr(r); return true; }
        return amt.Konto is { } k && ZeigenHaus(w, k);
    }

    /// <summary>
    /// Ein Ratsherr der Stadt. Ratsherren sind keine Personen der Familienbücher und haben noch kein eigenes Aussehen in den Daten:
    /// Gesicht, Haar und Augen werden fest aus dem Namen und dem Volk abgeleitet, das Geschlecht aus dem Vornamen (a, e, „ind“ am Ende = Frau).
    /// Sie tragen vornehme Kleidung vor dem Teppich der Patrizier.
    /// </summary>
    public void ZeigenRatsherr(Ratsherr r)
    {
        var name = r.Name;
        var h = 17;
        foreach (var c in name) h = unchecked(h * 31 + c); // string.GetHashCode wechselt von Start zu Start
        h &= int.MaxValue;
        var volk = r.Daten.Volk;
        var haare = Aussehenspalette.Haarfarben(volk);
        var augen = Aussehenspalette.Augenfarben(volk);
        var vorname = name.Split(' ')[0];
        var frau = vorname.EndsWith('a') || vorname.EndsWith('e') || vorname.EndsWith("ind");
        var alter = (h / 7) % 3 == 0 ? Altersstufe.Alt : Altersstufe.Mittel;
        Zeigen(volk, frau ? Geschlecht.Frau : Geschlecht.Mann,
            new Aussehen(h % Aussehenspalette.Gesichter, haare[(h / 5) % haare.Count], augen[(h / 11) % augen.Count]),
            alter, Kleidung.Vornehm, Standgruppe.Patrizier);
    }

    private (Volk, Geschlecht, Aussehen, Altersstufe, Kleidung, Standgruppe)? _zuletzt;

    public void Zeigen(Volk volk, Geschlecht geschlecht, Aussehen aussehen, Altersstufe alter, Kleidung kleidung, Standgruppe gruppe)
    {
        // Unverändert (die Tafeln bauen sich oft neu auf, die Kopfzeile fragt jedes Bild): nichts neu laden
        var jetzt = (volk, geschlecht, aussehen, alter, kleidung, gruppe);
        if (_zuletzt == jetzt) return;
        _zuletzt = jetzt;
        _gruppe = gruppe;
        var ordner = $"{Ordner}{volk.ToString().ToLowerInvariant()}/";
        var stamm = $"{(geschlecht == Geschlecht.Mann ? "mann" : "frau")}_{aussehen.Gesicht}_{alter.ToString().ToLowerInvariant()}";
        var basis = $"{ordner}{stamm}_{kleidung.ToString().ToLowerInvariant()}";

        _grund.Texture = Laden($"{Ordner}hintergrund_{gruppe.ToString().ToLowerInvariant()}");
        _silhouette.Setzen(Grund(gruppe), volk, geschlecht, alter);
        _person.Texture = Laden(basis);
        _silhouette.Visible = _person.Texture is null;
        _grund.Visible = _grund.Texture is not null;

        // Augen zu und Masken gelten für Gesicht und Alter, nicht für die Kleidung (der Weg der Masken steht noch nicht fest: auch mit Kleidung im Namen)
        var zu = Laden($"{ordner}{stamm}_zu") ?? Laden(basis + "_zu");
        var haar = Laden($"{ordner}{stamm}_haar") ?? Laden(basis + "_haar");
        var augen = Laden($"{ordner}{stamm}_augen") ?? Laden(basis + "_augen");
        _hatZu = zu is not null && _person.Texture is not null;
        _material.SetShaderParameter("geschlossen", Variant.From(zu));
        _material.SetShaderParameter("haarmaske", Variant.From(haar));
        _material.SetShaderParameter("augenmaske", Variant.From(augen));
        _material.SetShaderParameter("hat_geschlossen", _hatZu);
        _material.SetShaderParameter("hat_haarmaske", haar is not null && aussehen.Haar != Haarfarbe.Kahl);
        _material.SetShaderParameter("hat_augenmaske", augen is not null);
        var band = Augenband(augen);
        _material.SetShaderParameter("augen_oben", band.X);
        _material.SetShaderParameter("augen_unten", band.Y);
        var (linkes, rechtes) = Augenzonen(augen);
        _material.SetShaderParameter("auge_links", linkes);
        _material.SetShaderParameter("auge_rechts", rechtes);
        _material.SetShaderParameter("haar_mittel", Mittelhelligkeit(_person.Texture, haar));
        _material.SetShaderParameter("augen_mittel", Mittelhelligkeit(_person.Texture, augen));
        _material.SetShaderParameter("haarfarbe", Farbe(aussehen.Haar));
        _material.SetShaderParameter("augenfarbe", Farbe(aussehen.Augen));
        TooltipText = $"{volk.Einzahl()} · Haar: {FarbName(aussehen.Haar)} · Augen: {FarbName(aussehen.Augen)}";
        QueueRedraw();
    }

    /// <summary>Ein kleines Nicken – bei einem Gespräch oder einer Zusage.</summary>
    public void Nicken() => _nickenT = 0;

    /// <summary>Blinzelt jetzt einmal (für Bildschirmfotos: zwei Bilder kurz nacheinander).</summary>
    public void Blinzeln() => _blinzelnT = 0;

    /// <summary>Hält ein Blinzeln an einer Stelle fest (0 offen … 1 zu) – nur für Bildschirmfotos.</summary>
    public void BlinzelnFesthalten(float wert)
    {
        _lebendig = false;
        SetProcess(false);
        _material.SetShaderParameter("blinzeln", wert);
    }

    public override void _Draw()
    {
        // Ohne Hintergrundbild: Verlauf in den Farben der Standgruppe
        if (_grund.Texture is not null) return;
        var (oben, unten) = Grund(_gruppe);
        DrawPolygon(new Vector2[] { Vector2.Zero, new(Size.X, 0), Size, new(0, Size.Y) }, new[] { oben, oben, unten, unten });
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        _zeit += delta;
        var t = _zeit + _phase;

        // Blinzeln: alle 3–7 s, ein Lidschlag dauert 0,18 s (zu bei 0,09 s); manchmal ein Doppelblinzeln
        _blinzelnIn -= delta;
        if (_blinzelnIn <= 0 && _blinzelnT < 0)
        {
            _blinzelnT = 0;
            _blinzelnIn = 3 + GD.Randf() * 4;
            if (GD.Randf() < 0.15) _blinzelnIn = 0.35;
        }
        var blinzeln = 0f;
        if (_blinzelnT >= 0)
        {
            _blinzelnT += delta;
            const double Dauer = 0.18;
            blinzeln = _blinzelnT >= Dauer ? 0 : (float)Math.Sin(_blinzelnT / Dauer * Math.PI);
            if (_blinzelnT >= Dauer) _blinzelnT = -1;
        }
        if (!_hatZu) blinzeln = 0;

        // Atmen: ~4 s je Zug, ±0,6 %
        var atmen = 1f + 0.006f * (float)Math.Sin(t * Math.Tau / 4.0);

        // Nicken: 0,6 s, 2,5 % der Bildhöhe nach unten und zurück
        var nicken = 0f;
        if (_nickenT >= 0)
        {
            _nickenT += delta;
            nicken = _nickenT >= 0.6 ? 0 : 0.025f * (float)Math.Sin(_nickenT / 0.6 * Math.PI);
            if (_nickenT >= 0.6) _nickenT = -1;
        }

        _material.SetShaderParameter("blinzeln", blinzeln);
        _material.SetShaderParameter("atmen", atmen);
        _material.SetShaderParameter("nicken", nicken);

        // Kerzenlicht: zwei unregelmäßige Wellen, warm getönt; in der Stufe Niedrig aus
        if (Kerzenlicht)
        {
            var f = 0.5f + 0.5f * (float)(Math.Sin(t * 5.3) * 0.5 + Math.Sin(t * 12.1 + 1.7) * 0.3 + Math.Sin(t * 2.1) * 0.2);
            var a = 0.94f + 0.06f * f;
            Modulate = new Color(a, a * 0.975f, a * 0.93f);
        }
        else if (Modulate != Colors.White)
            Modulate = Colors.White;
    }

    /// <summary>Kerzenlicht flackert nur in den höheren Grafikstufen (in „Niedrig“ steht das Bild ruhig).</summary>
    public static bool Kerzenlicht => Fenster.AktuelleVoreinstellung != Voreinstellung.Niedrig;

    /// <summary>Die Masken sind weich gemalt und in dunklen Haarpartien schwach; dieselbe Verstärkung steht im Shader.</summary>
    private static float Maskenstaerke(float m) => Mathf.SmoothStep(0.05f, 0.5f, m);

    private static readonly Dictionary<(Texture2D, Texture2D), float> Mittelwerte = new();
    private static readonly Dictionary<Texture2D, Vector2> Baender = new();
    private static readonly Dictionary<Texture2D, (Vector4, Vector4)> Zonen = new();

    /// <summary>
    /// Die beiden Augen aus der Augenmaske als Ellipsen (Mitte x, y und Halbachsen, in Anteilen der Bildgröße): Nur dort blendet das Blinzeln das Bild mit
    /// geschlossenen Augen ein. Die Iris-Maske ist winzig (etwa 10 Pixel), das Auge mit Lidern etwa dreimal so breit, daher die Aufweitung (Rand weich ausklingend). Die Augen werden
    /// an der Mitte zwischen kleinstem und größtem x getrennt; ohne zweites Auge (Seitenansicht) bleibt dessen Ellipse leer (Halbachsen 0).
    /// </summary>
    private static (Vector4 links, Vector4 rechts) Augenzonen(Texture2D? maske)
    {
        if (maske is null) return (Vector4.Zero, Vector4.Zero);
        if (Zonen.TryGetValue(maske, out var bekannt)) return bekannt;
        var ergebnis = (Vector4.Zero, Vector4.Zero);
        if (maske.GetImage() is { } m)
        {
            if (m.IsCompressed()) m.Decompress();
            int w = m.GetWidth(), h = m.GetHeight(), xmin = int.MaxValue, xmax = -1;
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                    if (m.GetPixel(x, y).R > 0.3f) { xmin = Math.Min(xmin, x); xmax = Math.Max(xmax, x); }
            if (xmax >= 0)
            {
                var mitte = (xmin + xmax) / 2;
                // Ein Auge allein (kleine Breite) gilt als ein Cluster
                var getrennt = xmax - xmin > 0.12f * w;
                Vector4 Zone(int von, int bis)
                {
                    int x0 = int.MaxValue, x1 = -1, y0 = int.MaxValue, y1 = -1;
                    for (var y = 0; y < h; y++)
                        for (var x = von; x <= bis; x++)
                            if (m.GetPixel(x, y).R > 0.3f) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
                    if (x1 < 0) return Vector4.Zero;
                    return new Vector4((x0 + x1 + 1) / 2f / w, (y0 + y1 + 1) / 2f / h, (x1 - x0 + 1) / 2f / w * 4.5f + 0.014f, (y1 - y0 + 1) / 2f / h * 3f + 0.012f);
                }
                ergebnis = getrennt ? (Zone(xmin, mitte), Zone(mitte + 1, xmax)) : (Zone(xmin, xmax), Vector4.Zero);
            }
        }
        Zonen[maske] = ergebnis;
        return ergebnis;
    }

    /// <summary>
    /// Das Augenband (Anteil der Bildhöhe von oben) aus der Augenmaske: von knapp über bis knapp unter der Iris. Nur dort wird beim Blinzeln das Bild
    /// mit geschlossenen Augen eingeblendet; die Gesichter sitzen unterschiedlich hoch. Ohne Maske ein Mittelwert.
    /// </summary>
    private static Vector2 Augenband(Texture2D? maske)
    {
        var standard = new Vector2(0.30f, 0.52f);
        if (maske is null) return standard;
        if (Baender.TryGetValue(maske, out var bekannt)) return bekannt;
        var ergebnis = standard;
        if (maske.GetImage() is { } m)
        {
            if (m.IsCompressed()) m.Decompress();
            int oben = int.MaxValue, unten = -1;
            for (var y = 0; y < m.GetHeight(); y++)
                for (var x = 0; x < m.GetWidth(); x++)
                    if (m.GetPixel(x, y).R > 0.3f) { oben = Math.Min(oben, y); unten = Math.Max(unten, y); }
            if (unten >= 0) ergebnis = new Vector2(oben / (float)m.GetHeight() - 0.045f, unten / (float)m.GetHeight() + 0.045f);
        }
        Baender[maske] = ergebnis;
        return ergebnis;
    }

    /// <summary>
    /// Mittlere Helligkeit der Person im Bereich der Maske (gewichtet mit der Maske). Der Shader teilt die Helligkeit jedes Pixels durch diesen Wert:
    /// Zielfarbe × Helligkeitsverlauf des Originals (dieselbe Formel wie im Bauskript tools/portraets/portraets_bauen.gd).
    /// </summary>
    private static float Mittelhelligkeit(Texture2D? person, Texture2D? maske)
    {
        if (person is null || maske is null) return 0.5f;
        if (Mittelwerte.TryGetValue((person, maske), out var bekannt)) return bekannt;
        float ergebnis = 0.5f;
        var p = person.GetImage();
        var m = maske.GetImage();
        if (p is not null && m is not null)
        {
            if (p.IsCompressed()) p.Decompress();
            if (m.IsCompressed()) m.Decompress();
            var n = Math.Min(p.GetWidth(), m.GetWidth());
            double summe = 0, gewicht = 0;
            for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                {
                    var w = Maskenstaerke(m.GetPixel(x, y).R);
                    if (w <= 0.05f) continue;
                    summe += p.GetPixel(x, y).Luminance * w;
                    gewicht += w;
                }
            if (gewicht >= 1) ergebnis = (float)Math.Max(summe / gewicht, 0.05);
        }
        Mittelwerte[(person, maske)] = ergebnis;
        return ergebnis;
    }

    /// <summary>
    /// Lädt ein Bild (<paramref name="stamm"/> ohne Endung: erst .webp, dann .png) und macht daraus eines mit Mipmaps (sonst flimmert das 512-px-Bild als kleines Porträt); null, wenn es die Datei nicht gibt.
    /// </summary>
    private static Texture2D? Laden(string stamm)
    {
        var pfad = stamm;
        if (Speicher.TryGetValue(pfad, out var bekannt)) return bekannt;
        Texture2D? ergebnis = null;
        var datei = ResourceLoader.Exists(stamm + ".webp") ? stamm + ".webp" : stamm + ".png";
        if (ResourceLoader.Exists(datei) && GD.Load<Texture2D>(datei) is { } textur)
        {
            ergebnis = textur;
            var bild = textur.GetImage();
            if (bild is not null && !bild.HasMipmaps())
            {
                if (bild.IsCompressed()) bild.Decompress();
                bild.GenerateMipmaps();
                ergebnis = ImageTexture.CreateFromImage(bild);
            }
        }
        Speicher[pfad] = ergebnis;
        return ergebnis;
    }

    /// <summary>Ersatz für ein fehlendes Bild: Kopf und Schultern als dunkle Fläche vor dem Hintergrund, davor ein Fragezeichen.</summary>
    private partial class Silhouette : Control
    {
        private (Color Oben, Color Unten) _grund;
        private float _breite = 1f;
        private bool _frau;
        private float _kopf = 0.2f;

        public void Setzen((Color, Color) grund, Volk volk, Geschlecht geschlecht, Altersstufe alter)
        {
            _grund = grund;
            _frau = geschlecht == Geschlecht.Frau;
            _breite = volk is Volk.Zwerg or Volk.Halbling ? 1.1f : volk is Volk.Ork or Volk.Oger ? 1.2f : 1f;
            _kopf = alter == Altersstufe.Kind ? 0.17f : 0.2f;
            QueueRedraw();
        }

        public override void _Draw()
        {
            var s = Size;
            var farbe = _grund.Unten.Darkened(0.45f);
            var mitte = new Vector2(s.X * 0.5f, s.Y * 0.40f);
            DrawCircle(mitte, s.X * _kopf, farbe);
            var br = s.X * 0.36f * _breite;
            var hoehe = s.Y * 0.52f;
            var schultern = new[]
            {
                new Vector2(s.X * 0.5f - br, s.Y), new Vector2(s.X * 0.5f - br * 0.9f, hoehe + s.Y * 0.1f), new Vector2(s.X * 0.5f - br * 0.35f, hoehe),
                new Vector2(s.X * 0.5f + br * 0.35f, hoehe), new Vector2(s.X * 0.5f + br * 0.9f, hoehe + s.Y * 0.1f), new Vector2(s.X * 0.5f + br, s.Y),
            };
            DrawColoredPolygon(schultern, farbe);
            if (_frau) DrawCircle(mitte + new Vector2(0, s.Y * 0.04f), s.X * _kopf * 1.12f, farbe);

            // Platzhalter, bis das Bild da ist: ein Fragezeichen vor dem Schatten (Wunsch des Nutzers, 27.10.2026)
            var schrift = Schriften.Ueberschrift;
            var groesse = Mathf.Max(8, (int)(s.Y * 0.42f));
            var zeichen = schrift.GetStringSize("?", HorizontalAlignment.Left, -1, groesse);
            var lage = new Vector2(s.X * 0.5f - zeichen.X * 0.5f, s.Y * 0.5f + zeichen.Y * 0.3f);
            DrawString(schrift, lage, "?", HorizontalAlignment.Left, -1, groesse, _grund.Oben.Lightened(0.35f));
        }
    }
}
