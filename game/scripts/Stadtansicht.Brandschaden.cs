using Godot;

namespace Reichsgilde;

/// <summary>
/// Der sichtbare Brandschaden am Haus (Teil der <c>partial class Stadtansicht</c>; Nutzerwunsch 12.11.2026): Rußfahnen über allen Öffnungen,
/// verkohlte Balken, geschwärztes Strohdach, abgedunkelte Wände. Er hängt am Zustand „Brand“ (Werkstatt: Reparaturminuten des Kerns,
/// Wohnhaus: Instandsetzung, Blitz: bis zum Jahreswechsel); mit der Reparatur kommen die Originalmaterialien zurück. Nur Material-Überschreibungen
/// und eine kleine Textur mit den Öffnungen; keine neuen Teile, keine Partikel, keine Trümmer.
/// <list type="bullet">
/// <item><b>Dach</b> (<see cref="Dachstoffe"/>): <c>russ.gdshader</c> als zweite Schicht; Stroh deckender und ohne Asche (<see cref="RussStoff"/>).</item>
/// <item><b>Holz</b> (<see cref="Holzstoffe"/>): <c>verkohlt.gdshader</c> ersetzt den Stoff (schwarze Kohle, Krokodilhaut).</item>
/// <item><b>Wand</b> (<see cref="Wandstoffe"/>): <c>brand_wand.gdshader</c> als zweite Schicht: Fahnen über den Öffnungen, Saum, leichter Schleier.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    private static readonly HashSet<string> Strohstoffe = new() { "Stroh", "Strohkante", "Firstsoden" };
    private static readonly HashSet<string> Holzstoffe = new() { "Fachwerk", "Holz", "Holz alt", "Bretter", "Schindel", "Schindelkante", "Schnittholz" };
    private static readonly HashSet<string> Wandstoffe = new()
    {
        "Bruchstein", "Bruchstein dunkel", "Haustein", "Sandstein", "Rotsandstein", "Palaststein", "Putz", "Lehmputz", "Putz rot", "Putz grau",
        "Ratsputz", "Hausfarbe", "Fasche", "Stadtweiss", "Fuge", "Lehm", "Ocker",
    };

    private static ShaderMaterial? _verkohlt;
    /// <summary>Die Originale unter den Überschreibungen (je Teil und Fläche), damit die Reparatur sie zurückgibt; null = es gab keine Überschreibung.</summary>
    private readonly Dictionary<(ulong Teil, int Flaeche), Material?> _brandOriginale = new();

    /// <summary>Kanten und First der Dächer: Streifen, die auf der Dachfläche liegen; sie bekommen nur die dunklere Farbe, keine Rußschicht.</summary>
    private static bool Dachkante(string name) => name.EndsWith("kante") || name.StartsWith("First");

    private static StandardMaterial3D? _russSchichtDach, _russSchichtStroh;

    /// <summary>
    /// Die Rußschicht über einem Dach (zweite Schicht, <c>NextPass</c>): ein selbst erzeugtes, kachelndes Rußmuster (<see cref="RussTextur"/>), im Raum
    /// auf die Fläche projiziert (Weltlage, kein Vorzugsrichtung), ohne Shader. Nur auf den Flächen der Dachfläche selbst, nicht auf Kanten und First
    /// (<see cref="Dachkante"/>): Beim Rathaus liegen Schiefer und Schieferkante an den Gauben übereinander, und zwei Schichten an derselben Stelle
    /// gaben im Pixel-Look ein regelmäßiges Punktraster aus doppelt abgedunkelten Bildpunkten (Schablonenpuffer half nicht, Probe 12.11.2026).
    /// </summary>
    private static StandardMaterial3D RussSchicht(bool stroh)
    {
        ref var feld = ref (stroh ? ref _russSchichtStroh : ref _russSchichtDach);
        return feld ??= new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoTexture = stroh ? RussTextur(0.34f, 0.5f, 0.1f, 7) : RussTextur(0.14f, 0.72f, 0.65f, 3),
            Uv1Triplanar = true,
            Uv1WorldTriplanar = true,
            Uv1Scale = Vector3.One / RussKachel,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
            CullMode = BaseMaterial3D.CullModeEnum.Back,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
            Grow = true,
            GrowAmount = 0.012f, // 1 cm vor der Fläche (gegen Tiefenstreit mit dem Dach)
            RenderPriority = 1,
        };
    }

    private const float RussKachel = 13.2f; // Meter je Kachel des Rußmusters (vier große Zellen zu je 3,3 m)

    private static uint NameHash(string text)
    {
        var h = 2166136261u;
        foreach (var c in text) h = (h ^ c) * 16777619u; // FNV: .NET-GetHashCode ist je Start anders
        return h;
    }

    private static float Hash01(uint x, uint y, uint saat)
    {
        var h = x * 374761393u + y * 668265263u + saat * 2246822519u;
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFFFF) / 16777216f;
    }

    /// <summary>Glattes Rauschen, das nach <paramref name="periode"/> Zellen in beide Richtungen von vorn beginnt (die Kachel schließt nahtlos).</summary>
    private static float KachelRauschen(float u, float v, int periode, uint saat)
    {
        var x = u * periode;
        var y = v * periode;
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var fx = x - x0;
        var fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        uint X(int i) => (uint)(((i % periode) + periode) % periode);
        var a = Mathf.Lerp(Hash01(X(x0), X(y0), saat), Hash01(X(x0 + 1), X(y0), saat), fx);
        var b = Mathf.Lerp(Hash01(X(x0), X(y0 + 1), saat), Hash01(X(x0 + 1), X(y0 + 1), saat), fx);
        return Mathf.Lerp(a, b, fy);
    }

    private static float Stufe(float a, float b, float x)
    {
        var t = Mathf.Clamp((x - a) / (b - a), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// Das Rußmuster als Bild (selbst erzeugt, kein Fremdmaterial): <paramref name="grund"/> = Deckung des Schleiers, <paramref name="tiefe"/> = zusätzliche Deckung
    /// in den Rußwolken, <paramref name="asche"/> = Anteil der grauen Aschefelder; dazu dichte Brandflecken. Alles groß und rund, damit nichts flimmert.
    /// </summary>
    private static ImageTexture RussTextur(float grund, float tiefe, float asche, uint saat)
    {
        const int n = 256;
        var daten = new byte[n * n * 4];
        for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                var r = KachelRauschen(u, v, 4, saat) * 0.55f + KachelRauschen(u, v, 9, saat + 1) * 0.3f + KachelRauschen(u, v, 24, saat + 2) * 0.15f;
                var wolke = Stufe(0.38f, 0.7f, r);
                var brand = Stufe(0.62f, 0.74f, KachelRauschen(u, v, 5, saat + 3) * 0.7f + r * 0.3f);
                var feld = Stufe(0.5f, 0.68f, KachelRauschen(u, v, 3, saat + 4)) * (1f - brand);
                var ascheAnteil = feld * asche * (1f - wolke * 0.5f);
                var a = grund + tiefe * wolke + 0.3f * brand;
                a = Mathf.Max(a * (1f - ascheAnteil), ascheAnteil * 0.62f);
                var grau = Mathf.Lerp(0.02f, 0.2f, ascheAnteil);
                var i = (y * n + x) * 4;
                daten[i] = daten[i + 1] = daten[i + 2] = (byte)Mathf.Clamp((int)MathF.Round(MathF.Pow(grau, 0.4545f) * 255f), 0, 255);
                daten[i + 3] = (byte)Mathf.Clamp((int)MathF.Round(Mathf.Clamp(a, 0f, 0.92f) * 255f), 0, 255);
            }
        var bild = Image.CreateFromData(n, n, false, Image.Format.Rgba8, daten);
        bild.GenerateMipmaps();
        return ImageTexture.CreateFromImage(bild);
    }

    private static ShaderMaterial VerkohltStoff() =>
        _verkohlt ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/verkohlt.gdshader") };

    /// <summary>
    /// Alle Öffnungen eines Modells in Weltlage: die Scheiben (Material „Fenster“ und „Glas“) und die Löcher (Türen, Torbögen; Material „Loch“), je mit Mitte,
    /// waagerechter Normale, Breite und Höhe. Zu kleine Stücke und waagerechte Flächen zählen nicht.
    /// </summary>
    private static List<FensterAnker> OeffnungenSuchen(ModellInstanz modell)
    {
        var welt = modell.Wurzel.GlobalTransform;
        var ergebnis = new List<FensterAnker>();
        void Sammeln(MeshInstance3D teil, int flaeche, float mindestHoehe)
        {
            if (!teil.IsVisibleInTree() || teil.Mesh is not { } netz) return;
            foreach (var b in Bauteile(netz, flaeche, LageUnter(modell.Wurzel, teil)))
            {
                var n = new Vector3(b.Normale.X, 0, b.Normale.Z);
                if (b.Groesse.Y < mindestHoehe || Mathf.Abs(b.Normale.Y) > 0.5f || n.LengthSquared() < 0.01f) continue;
                n = n.Normalized();
                var quer = Mathf.Abs(n.X) > Mathf.Abs(n.Z) ? b.Groesse.Z : b.Groesse.X;
                if (quer < 0.25f) continue;
                ergebnis.Add(new FensterAnker(welt * b.Mitte, (welt.Basis * n).Normalized(), quer, b.Groesse.Y));
            }
        }
        foreach (var (teil, flaeche) in modell.Fenster) Sammeln(teil, flaeche, 0.3f);
        foreach (var teil in modell.Wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (Nebenteil(teil.Name) || teil.Mesh is not { } netz) continue;
            for (var i = 0; i < netz.GetSurfaceCount(); i++)
                if (netz.SurfaceGetMaterial(i) is BaseMaterial3D { ResourceName: "Loch" }) Sammeln(teil, i, 0.8f);
                else if (netz.SurfaceGetMaterial(i) is BaseMaterial3D { ResourceName: "Glas" }) Sammeln(teil, i, 0.3f); // manche Häuser haben Scheiben aus „Glas“ statt „Fenster“
        }
        // Scheiben können nach innen zeigen: die Normale zeigt immer vom Haus weg (von der Mitte aller Öffnungen aus gesehen)
        if (ergebnis.Count > 0)
        {
            var mitte = ergebnis.Aggregate(Vector3.Zero, (summe, a) => summe + a.Mitte) / ergebnis.Count;
            for (var i = 0; i < ergebnis.Count; i++)
            {
                var a = ergebnis[i];
                var raus = new Vector3(a.Mitte.X - mitte.X, 0, a.Mitte.Z - mitte.Z);
                if (a.Normale.Dot(raus) < 0) ergebnis[i] = a with { Normale = -a.Normale };
            }
        }
        return ergebnis;
    }

    /// <summary>Die Textur mit den Öffnungen für <c>brand_gemein.gdshaderinc</c> (Spalte je Öffnung; Zeile 0 Mitte und Breite, Zeile 1 Normale und Höhe).</summary>
    private static ImageTexture OeffnungenTextur(IReadOnlyList<FensterAnker> o, string haus)
    {
        var breite = Mathf.Max(o.Count, 1);
        var daten = new float[breite * 12];
        for (var i = 0; i < o.Count; i++)
        {
            var a = o[i];
            var z0 = i * 4;
            var z1 = (breite + i) * 4;
            (daten[z0], daten[z0 + 1], daten[z0 + 2], daten[z0 + 3]) = (a.Mitte.X, a.Mitte.Y, a.Mitte.Z, a.Breite);
            (daten[z1], daten[z1 + 1], daten[z1 + 2], daten[z1 + 3]) = (a.Normale.X, a.Normale.Y, a.Normale.Z, a.Hoehe);
            // Länge der Fahne: nur Darstellung, fest je Haus und Öffnung (Name und Nummer gehasht, kein Zufall zur Laufzeit): 0,6- bis 2,4-fach
            daten[(2 * breite + i) * 4] = 0.6f + 1.8f * Hash01(NameHash(haus), (uint)i, 17);
        }
        var bytes = new byte[daten.Length * sizeof(float)];
        Buffer.BlockCopy(daten, 0, bytes, 0, bytes.Length);
        return ImageTexture.CreateFromImage(Image.CreateFromData(breite, 3, false, Image.Format.Rgbaf, bytes));
    }

    /// <summary>
    /// Brandschaden an einem Modell anbringen (<paramref name="an"/>) oder abnehmen (Reparatur, Jahreswechsel). Wiederholtes Anbringen ist harmlos.
    /// Steht das Modell noch nicht im Baum (frisch eingesetzt), läuft es im nächsten Bild, denn die Öffnungen brauchen die Weltlage.
    /// </summary>
    private void BrandschadenSetzen(ModellInstanz modell, bool an)
    {
        if (!IsInstanceValid(modell.Wurzel)) return;
        const string merke = "rg_brandschaden";
        if (an == modell.Wurzel.HasMeta(merke)) return;
        if (an && !modell.Wurzel.IsInsideTree())
        {
            Callable.From(() => BrandschadenSetzen(modell, true)).CallDeferred();
            return;
        }
        if (OS.GetEnvironment("RG_DACHSTOFF") == "1") DachStoffLogen(modell, an ? "vor dem Ruß" : "vor dem Abnehmen des Ruß");
        ShaderMaterial? wand = null;
        if (an)
        {
            modell.Wurzel.SetMeta(merke, true);
            var oeffnungen = OeffnungenSuchen(modell);
            wand = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/brand_wand.gdshader"), RenderPriority = 1 };
            wand.SetShaderParameter("oeffnungen", OeffnungenTextur(oeffnungen, modell.Wurzel.Name + "@" + modell.Wurzel.GlobalPosition));
            wand.SetShaderParameter("oeffnungen_anzahl", oeffnungen.Count);
            // Brennt das Haus gerade, wachsen die Fahnen während des Brandes ein (BraendeAktualisieren), sonst stehen sie ganz
            wand.SetShaderParameter("fahnen_wuchs", _braende.Values.Any(b => IsInstanceValid(b.Wurzel) && b.Wurzel.GetParent() == modell.Wurzel) ? 0f : 1f);
            modell.Wurzel.SetMeta("rg_brandwand", wand);
            if (OS.GetEnvironment("RG_DACHSTOFF") == "1") GD.Print($"[brandschaden] {modell.Wurzel.Name} bei {modell.Wurzel.GlobalPosition}: {oeffnungen.Count} Öffnungen: " + string.Join("; ", oeffnungen.Select(o => $"{o.Mitte} n {o.Normale} b {o.Breite:0.0} h {o.Hoehe:0.0}")));
        }
        else
        {
            modell.Wurzel.RemoveMeta(merke);
            if (modell.Wurzel.HasMeta("rg_brandwand")) modell.Wurzel.RemoveMeta("rg_brandwand");
        }
        var waende = new Dictionary<BaseMaterial3D, BaseMaterial3D>(); // je Ausgangsmaterial eine Kopie mit Wandschicht (je Haus)
        foreach (var teil in modell.Wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (teil.Mesh is not { } netz) continue;
            var geaendert = false;
            for (var i = 0; i < netz.GetSurfaceCount(); i++)
            {
                if (netz.SurfaceGetMaterial(i) is not BaseMaterial3D stoff) continue;
                var name = stoff.ResourceName;
                var schluessel = (teil.GetInstanceId(), i);
                if (!an)
                {
                    if (_brandOriginale.Remove(schluessel, out var original))
                    {
                        teil.SetSurfaceOverrideMaterial(i, original);
                        geaendert = true;
                    }
                    continue;
                }
                var aktuell = teil.GetSurfaceOverrideMaterial(i);
                Material? neu = null;
                if (Dachstoffe.Contains(name) || Strohstoffe.Contains(name)) neu = RussStoff(stoff);
                else if (Holzstoffe.Contains(name)) neu = VerkohltStoff();
                else if (Wandstoffe.Contains(name) && (aktuell ?? stoff) is BaseMaterial3D grund)
                {
                    if (!waende.TryGetValue(grund, out var kopie))
                    {
                        waende[grund] = kopie = (BaseMaterial3D)grund.Duplicate();
                        kopie.NextPass = wand;
                        // Die Oberflächendetails zeichnen statt des Materials ihr abgeleitetes (Mauerwerk, Putz): die Schicht muss auch dort hängen
                        if (_oberflaeche?.Ableiten(kopie) is { } abgeleitet) abgeleitet.NextPass = wand;
                    }
                    neu = kopie;
                }
                if (neu is null) continue;
                _brandOriginale.TryAdd(schluessel, aktuell);
                teil.SetSurfaceOverrideMaterial(i, neu);
                geaendert = true;
            }
            if (geaendert) _oberflaeche?.Umstellen(teil); // sonst zeichnet der RenderingServer das rohe Material ohne Struktur
        }
    }
}
