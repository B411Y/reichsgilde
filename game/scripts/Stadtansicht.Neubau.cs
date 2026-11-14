using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Neubau der Gebäude, Probe (Wohnhaus Mensch, Stufe 1, Grundform 1) – Teil der <c>partial class Stadtansicht</c>.
///
/// Die neuen Häuser (<c>assets/modelle/gebaeude/neubau/</c>, gebaut von <c>tools/modelle/neubau/</c>) folgen dem Style-Guide
/// „3D-Asset-Pipeline“: ein gemeinsames Trim-Sheet (<c>assets/texturen/neubau/trim_*.png</c>) mit dem Shader
/// <c>building_trim.gdshader</c>, gebackene Verdeckung auf UV2, Putz- und Akzentfarbe je Haus als Instanz-Uniform.
///
/// <b>Zusammenspiel mit dem Bestand.</b> Brandschaden, Fensterlicht, Traufen und Schattenproxys erkennen ihre Flächen am
/// Materialnamen eines <see cref="BaseMaterial3D"/> im Netz. Deshalb trägt das Netz benannte Standardmaterialien mit den
/// Trim-Texturen („Stroh“, „Strohkante“, „Fachwerk“, „Lehmputz“, „Bruchstein“, „Fenster“), und der Trim-Shader liegt als
/// Override der Fläche darüber: Der Brand tauscht das Override und stellt es nach der Reparatur wieder her. Die
/// Oberflächendetails (Relief, Pixel-Fassung, Nässe: <c>Oberflaechendetails.cs</c>) lassen Materialien mit eigener Textur
/// unangetastet – die neuen Häuser bekommen davon also nichts (offene Frage, <c>docs/bereiche/darstellung.md</c>).
///
/// <b>Innenansicht am Ort.</b> Der Innenraum (<c>…_innen.glb</c>) hat dieselben Maße wie die Hülle. Betritt der Spieler die
/// Wohnstube, hängt er als Knoten „InteriorRoot“ im Haus in der Stadt; die Raumkamera steht darin und sieht durch die
/// Fensteröffnungen die echte Stadt samt Passanten (die Scheiben mit dem Fensterlicht sind solange ausgeblendet). Beim
/// Verlassen wird „InteriorRoot“ unsichtbar und nimmt nicht mehr an der Verarbeitung teil.
///
/// Alles hier ist nur mit Startoption aktiv (<c>docs/STARTOPTIONEN.md</c>): <c>--neubauprobe=1</c>, <c>--neubaureihe=…</c>,
/// <c>--neubaumessung=…</c>. Ohne sie steht überall das bisherige <c>fachwerkhaus.glb</c>.
/// </summary>
public partial class Stadtansicht
{
    private const string NeubauOrdner = "gebaeude/neubau/";
    private const string NeubauWohnhaus = NeubauOrdner + "wohnhaus_mensch_s1_g1";
    private const string AltesWohnhaus = "gebaeude/fachwerkhaus";
    private const string NeubauTexturen = "res://assets/texturen/neubau/";

    private static string? NeubauOption(string name) =>
        OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith(name + "="))?[(name.Length + 1)..].Trim();

    /// <summary>Startoption <c>--neubauprobe=1</c>: das Wohnhaus der Stufe 0 kommt aus dem Neubau, die Wohnstube liegt im Haus.</summary>
    private static readonly bool NeubauProbeAn = NeubauOption("--neubauprobe") is "1" or "an";

    // Putz- und Akzentfarben aus dem Style-Guide (5.3); fest je Standort
    private static readonly Color[] NeubauPutz = { new(1.0f, 0.98f, 0.94f), new(0.95f, 0.88f, 0.72f), new(0.88f, 0.86f, 0.82f), new(0.92f, 0.80f, 0.75f) };
    private static readonly Color[] NeubauAkzent = { new(0.22f, 0.35f, 0.55f), new(0.25f, 0.42f, 0.28f), new(0.55f, 0.22f, 0.20f), new(0.45f, 0.36f, 0.28f) };
    private readonly Dictionary<string, ShaderMaterial> _neubauStoffe = new();   // je Gruppe (Trim-Sheet und AO-Atlas) ein Material
    private readonly Dictionary<string, StandardMaterial3D> _neubauGrund = new(); // benannte Grundmaterialien fürs Netz
    private readonly HashSet<Mesh> _neubauNetze = new();
    private readonly Dictionary<string, Node3D> _neubauHaeuser = new();          // Kennung (wohnhaus:…) → Wurzel des Modells
    private Shader? _neubauShader;
    private Node3D? _neubauInnen;      // der sichtbare „InteriorRoot“ (null: niemand ist drinnen)
    private bool NeubauInnenAktiv => _neubauInnen is not null;

    /// <summary>Welche Datei für das Wohnhaus eingesetzt wird: mit <c>--neubauprobe=1</c> das neue Haus statt des Fachwerkhauses.</summary>
    private static string NeubauModell(string datei) => NeubauProbeAn && datei == AltesWohnhaus ? NeubauWohnhaus : datei;

    private static T NeubauTextur<T>(string name) where T : Resource => GD.Load<T>(NeubauTexturen + name);

    /// <summary>
    /// Die Gruppe eines Modells: der Dateiname ohne Stufe und Grundform („wohnhaus_mensch_s1_g1“ → „wohnhaus_mensch“), innen mit
    /// „_innen“. Alle Häuser einer Gruppe liegen im selben AO-Atlas (<c>tools/modelle/neubau/ao_atlas.json</c>) und teilen ein Material.
    /// </summary>
    private static string NeubauGruppe(string datei)
    {
        var name = datei[(datei.LastIndexOf('/') + 1)..];
        var innen = name.EndsWith("_innen");
        if (innen) name = name[..^"_innen".Length];
        name = System.Text.RegularExpressions.Regex.Replace(name, "_s\\d+_g\\d+$", "");
        return innen ? name + "_innen" : name;
    }

    /// <summary>Das Trim-Material einer Gruppe: Trim-Sheet (außen oder innen) und der AO-Atlas der Gruppe.</summary>
    private ShaderMaterial NeubauStoff(string datei)
    {
        var gruppe = NeubauGruppe(datei);
        if (_neubauStoffe.TryGetValue(gruppe, out var stoff)) return stoff;
        var innen = gruppe.EndsWith("_innen");
        var blatt = innen ? "innen_trim" : "trim";
        _neubauShader ??= GD.Load<Shader>("res://assets/shader/building_trim.gdshader");
        stoff = new ShaderMaterial { Shader = _neubauShader, ResourceName = "Trim " + gruppe };
        stoff.SetShaderParameter("albedo_tex", NeubauTextur<Texture2D>(blatt + "_albedo.png"));
        stoff.SetShaderParameter("normal_tex", NeubauTextur<Texture2D>(blatt + "_normal.png"));
        stoff.SetShaderParameter("orm_tex", NeubauTextur<Texture2D>(blatt + "_orm.png"));
        stoff.SetShaderParameter("ao_uv2_tex", NeubauTextur<Texture2D>(gruppe + "_ao_atlas.png"));
        stoff.SetShaderParameter("wetter_an", innen ? 0f : 1f);      // im Haus regnet und schneit es nicht
        stoff.SetShaderParameter("pixel_look", Fenster.PixelLookAn ? 1f : 0f);
        return _neubauStoffe[gruppe] = stoff;
    }

    /// <summary>Pixel-Look an oder aus: die Trim-Materialien lesen eine gröbere Mip-Stufe (aus Stadtansicht.Oberflaechendetails.cs).</summary>
    private void NeubauPixelSetzen(bool an)
    {
        foreach (var stoff in _neubauStoffe.Values) stoff.SetShaderParameter("pixel_look", an ? 1f : 0f);
        // Die ausgefransten Strohkanten (Alpha-Cards) zerfallen in groben Bildpunkten zu schwarzen Punkten: im Pixel-Look ganz aus
        if (_neubauGrund.TryGetValue("trim/Strohkante", out var kante)) kante.AlphaScissorThreshold = an ? 1.01f : 0.5f;
    }

    /// <summary>Kennung der Klickfläche auf der Haustür im Innenraum: ein Klick verlässt das Haus (mit Klinke und Blende).</summary>
    public const string NeubauAusgang = "neubau:haustuer";

    /// <summary>Vorsatz der Klickflächen auf den Gegenständen im Raum („neubau:gegenstand:Familienbuch“).</summary>
    public const string NeubauGegenstand = "neubau:gegenstand:";

    /// <summary>
    /// Welcher Gegenstand im Raum angeklickt wurde (null: keiner). Jeder öffnet genau eine Tafel (Hauptszene.RaumAngeklickt):
    /// Familienbuch → Familie, Truhe → Einrichtung kaufen, Bauplan → Haus (Ausbau, Hauswächter), Werkzeug → Handwerk, Geld → Geld und Kredit.
    /// </summary>
    public static string? NeubauGegenstandVon(string kennung) => kennung.StartsWith(NeubauGegenstand) ? kennung[NeubauGegenstand.Length..] : null;

    private static readonly Dictionary<string, string> NeubauGegenstandTitel = new()
    {
        ["Familienbuch"] = "Familienbuch", ["Truhe"] = "Truhe · Einrichtung", ["Bauplan"] = "Bauplan · Haus und Ausbau",
        ["Werkzeug"] = "Werkzeug · Handwerk", ["Geld"] = "Geldkassette · Geld und Kredit",
    };

    /// <summary>
    /// Räume je Stufe (Nutzer, 14.11.2026): Das Haus der Stufe 1 (im Kern <c>WohnhausStufe</c> 0) ist ein einziger Raum, ohne
    /// Hinterzimmer und ohne Kontor. Gilt in der Probe; für alle Häuser entscheidet es der Kern (offen, docs/bereiche/darstellung.md).
    /// </summary>
    public static bool NeubauOhneHinterzimmer(Konto? konto) => NeubauProbeAn && konto is { WohnhausStufe: 0 };

    /// <summary>Ist das die Haustür von innen? Dann zeigt der Mauszeiger das Schloss, und der Klick führt hinaus.</summary>
    public bool IstAusgangstuer(string? kennung) => kennung == NeubauAusgang && NeubauInnenAktiv;

    private ShaderMaterial? _neubauBleinetz, _neubauButzenglas;

    /// <summary>Das Bleinetz der Butzenscheiben außen vor der Scheibe mit dem Fensterlicht (bleinetz.gdshader).</summary>
    private ShaderMaterial NeubauBleinetz() => _neubauBleinetz ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/bleinetz.gdshader"), ResourceName = "Bleinetz" };

    /// <summary>
    /// Das benannte Standardmaterial im Netz (für alles, was nach dem Materialnamen sucht, und als Grundlage des
    /// Brandschadens: <see cref="RussStoff"/> kopiert es): dieselben Trim-Texturen, ohne Farbvariation und ohne UV2.
    /// </summary>
    private StandardMaterial3D NeubauGrundstoff(string name, string blatt)
    {
        var schluessel = blatt + "/" + name;
        if (_neubauGrund.TryGetValue(schluessel, out var stoff)) return stoff;
        var orm = NeubauTextur<Texture2D>(blatt + "_orm.png");
        stoff = new StandardMaterial3D
        {
            ResourceName = name,
            AlbedoTexture = NeubauTextur<Texture2D>(blatt + "_albedo.png"),
            NormalEnabled = true, NormalTexture = NeubauTextur<Texture2D>(blatt + "_normal.png"),
            AOEnabled = true, AOTexture = orm, AOTextureChannel = BaseMaterial3D.TextureChannel.Red,
            Roughness = 1, RoughnessTexture = orm, RoughnessTextureChannel = BaseMaterial3D.TextureChannel.Green,
            Metallic = 1, MetallicTexture = orm, MetallicTextureChannel = BaseMaterial3D.TextureChannel.Blue,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        if (name == "Strohkante")
        {
            // Alpha-Cards an Traufe, First und Giebel (Style-Guide 5.1): Alpha Scissor 0,5, von beiden Seiten sichtbar
            stoff.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
            stoff.AlphaScissorThreshold = Fenster.PixelLookAn ? 1.01f : 0.5f; // im Pixel-Look aus, siehe NeubauPixelSetzen
            stoff.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        }
        return _neubauGrund[schluessel] = stoff;
    }

    /// <summary>
    /// Kleidet ein eingesetztes Neubau-Modell ein (aus <see cref="ModellEinsetzen"/>, vor dem Brandschaden): Grundmaterialien ins
    /// Netz (einmal je Netz), den Trim-Shader als Override, Putz- und Akzentfarbe fest aus dem Standort. Andere Modelle: nichts.
    /// </summary>
    private void NeubauEinkleiden(string datei, Node3D modell)
    {
        if (!datei.StartsWith(NeubauOrdner)) return;
        var innen = datei.EndsWith("_innen");
        var blatt = innen ? "innen_trim" : "trim";
        var trim = NeubauStoff(datei);
        var ort = modell.GlobalPosition;
        var saat = (uint)HashCode.Combine(Mathf.RoundToInt(ort.X * 2), Mathf.RoundToInt(ort.Z * 2));
        var putz = NeubauPutz[saat % (uint)NeubauPutz.Length];
        var akzent = NeubauAkzent[(saat / 7) % (uint)NeubauAkzent.Length]; // liegt auf dem hellen Teilband „gestrichenes Holz“
        foreach (var teil in modell.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (teil.Mesh is not { } netz) continue;
            if (teil.Name == "Fahne" || teil.GetParent()?.Name == "Fahne") continue; // Mast und wehendes Tuch: Stadtansicht.Fahnen.cs
            var neu = _neubauNetze.Add(netz);
            for (var i = 0; i < netz.GetSurfaceCount(); i++)
            {
                var name = netz.SurfaceGetMaterial(i)?.ResourceName ?? "";
                switch (name)
                {
                    case "Fenster":
                        break; // Fensterlicht: ModellEinsetzen hat das Material der Gruppe schon gesetzt
                    case "Hausfarbe" or "Holz" or "Eisen":
                        break; // Wappen und Fahne: die Farbe des Besitzers setzt ModellEinsetzen wie bei den bisherigen Häusern
                    case "Bleinetz":
                        if (neu) netz.SurfaceSetMaterial(i, NeubauBleinetz());
                        break;
                    case "Innenglas":
                        if (neu) netz.SurfaceSetMaterial(i, NeubauGlas());
                        break;
                    case "Strohkante":
                        if (neu) netz.SurfaceSetMaterial(i, NeubauGrundstoff(name, blatt));
                        break;
                    default:
                        if (neu) netz.SurfaceSetMaterial(i, NeubauGrundstoff(name, blatt));
                        teil.SetSurfaceOverrideMaterial(i, trim);
                        break;
                }
            }
            if (innen) continue;
            teil.SetInstanceShaderParameter("plaster_tint", putz);
            teil.SetInstanceShaderParameter("accent_tint", akzent);
        }
        if (innen || modell.HasNode("Bodenfleck")) return;
        // Bodenübergang (Style-Guide 5.1): festgetretene Erde rund ums Haus als Decal, nur dünn über dem Boden (trifft keine Wände)
        modell.AddChild(new Decal
        {
            Name = "Bodenfleck", Position = new Vector3(0, 0.02f, -0.2f), Size = new Vector3(8.6f, 0.36f, 8.2f),
            TextureAlbedo = NeubauTextur<Texture2D>("bodenfleck.png"), AlbedoMix = 0.85f, NormalFade = 0.6f, UpperFade = 0.6f, LowerFade = 0.6f,
            DistanceFadeEnabled = true, DistanceFadeBegin = 160, DistanceFadeLength = 40,
        });
    }

    /// <summary>
    /// Das Glas der Innenansicht: Butzenscheiben in Blei (butzenglas.gdshader) – grünliches Waldglas mit Schleuderringen, durch das
    /// man die Gasse verzogen und getönt sieht (die Scheiben außen sind solange ausgeblendet).
    /// </summary>
    private ShaderMaterial NeubauGlas() => _neubauButzenglas ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/butzenglas.gdshader"), ResourceName = "Innenglas" };

    /// <summary>Merkt das Haus zu seiner Kennung (aus <see cref="SpielhausModell"/>), damit die Innenansicht es findet.</summary>
    private void NeubauMerken(string? kennung, string datei, ModellInstanz? instanz)
    {
        if (kennung is null || instanz is null) return;
        if (datei.StartsWith(NeubauOrdner)) _neubauHaeuser[kennung] = instanz.Wurzel;
        else _neubauHaeuser.Remove(kennung);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Innenansicht am Ort
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Nach jedem Wechsel in der Raumansicht (<see cref="RaumZeigen"/>, <see cref="RaumansichtUmschalten"/>): Steht der Spieler in der
    /// Wohnstube seines Neubau-Hauses, zeigt die Kamera den Innenraum im Haus selbst (true); sonst wird er abgeschaltet (false).
    /// </summary>
    private bool NeubauInnenAbgleichen()
    {
        Node3D? haus = null;
        var soll = _innen is not null && _innen == _wohnhausAnsicht && _raum is not null && _raum == _innen.Raeume[0]
                   && _neubauHaeuser.TryGetValue(_innen.Kennung, out haus) && IsInstanceValid(haus);
        if (soll) NeubauInnenAn(haus!);
        else NeubauInnenAus();
        return soll;
    }

    private void NeubauInnenAn(Node3D haus)
    {
        var wurzel = haus.GetNodeOrNull<Node3D>("InteriorRoot");
        if (wurzel is null)
        {
            if (Modell(NeubauWohnhaus + "_innen") is not { } szene) return;
            wurzel = szene.Instantiate<Node3D>();
            wurzel.Name = "InteriorRoot";
            haus.AddChild(wurzel);
            NeubauEinkleiden(NeubauWohnhaus + "_innen", wurzel);
            NeubauInnenBeleben(wurzel);
            // Herdfeuer und ein schwaches Grundlicht unter dem Dach (ohne Schatten): sonst ist der Raum hinter den kleinen Fenstern schwarz
            if (wurzel.FindChild("Licht_Herd", true, false) is Node3D herd)
                herd.AddChild(new OmniLight3D { LightColor = new Color(1.0f, 0.62f, 0.30f), LightEnergy = 3.4f, OmniRange = 6.5f, ShadowEnabled = true, OmniAttenuation = 1.1f });
            if (wurzel.FindChild("Licht_Raum", true, false) is Node3D raum)
                raum.AddChild(new OmniLight3D { LightColor = new Color(0.95f, 0.88f, 0.76f), LightEnergy = 1.3f, OmniRange = 8f, ShadowEnabled = false });
        }
        if (_neubauInnen is not null && _neubauInnen != wurzel) NeubauInnenAus();
        _neubauInnen = wurzel;
        wurzel.Visible = true;
        wurzel.ProcessMode = ProcessModeEnum.Inherit;
        if (haus.FindChild("Scheiben", true, false) is Node3D scheiben) scheiben.Visible = false;
        if (_wohnhausAnsicht?.Innen is { } unten) unten.Visible = false; // die alte Wohnstube 200 m unter der Stadt wird nicht gebraucht
        _sonne.Visible = true;                // draußen ist Tag oder Nacht wie in der Stadt
        _raumKamera.Environment = null;       // Himmel, Nebel und Licht der Stadt statt des dunklen Raumhintergrunds
        _raumKamera.Fov = 66;
        if (wurzel.FindChild("Blick_Auge", true, false) is Node3D auge && wurzel.FindChild("Blick_Ziel", true, false) is Node3D ziel)
            _raumKamera.LookAtFromPosition(auge.GlobalPosition, ziel.GlobalPosition, Vector3.Up);
        _neubauBlickGrund = _raumKamera.GlobalTransform;
        (_neubauGier, _neubauNick, _neubauGierZiel, _neubauNickZiel) = (0, 0, 0, 0);
    }

    /// <summary>
    /// Was im Raum lebt, über dieselben Wege wie in der alten Wohnstube (Stadtansicht.Raeume.cs): die gekaufte Einrichtung
    /// (Knoten „Einrichtung_&lt;Kennung&gt;“, sichtbar, wenn das Haus das Stück hat), die Hausfarbe, die Familie an den Plätzen
    /// „Platz_…“, das Herdfeuer und die Klickfläche für die Familientafel. Einmal je Innenraum.
    /// </summary>
    private void NeubauInnenBeleben(Node3D wurzel)
    {
        if (_wohnhausAnsicht is not { } a) return;
        var stuecke = new Dictionary<string, MeshInstance3D>();
        var hausfarben = new List<StandardMaterial3D>();
        foreach (var teil in wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            var name = teil.Name.ToString();
            if (name.StartsWith("Einrichtung_"))
            {
                stuecke[name["Einrichtung_".Length..]] = teil;
                teil.Visible = false;
            }
            for (var i = 0; i < (teil.Mesh?.GetSurfaceCount() ?? 0); i++)
                if (teil.Mesh!.SurfaceGetMaterial(i) is StandardMaterial3D { ResourceName: "Hausfarbe" } stoff)
                {
                    var kopie = (StandardMaterial3D)stoff.Duplicate();
                    kopie.VertexColorUseAsAlbedo = false;
                    teil.SetSurfaceOverrideMaterial(i, kopie);
                    hausfarben.Add(kopie);
                }
        }
        Vector3? Ort(string name) => wurzel.FindChild(name, true, false) is Node3D n ? wurzel.ToLocal(n.GlobalPosition) : null;
        if (Ort("Feuer_Herd") is { } feuer)
            foreach (var dz in new[] { -0.2f, 0.2f }) // zwei Flammen links und rechts unter dem Kessel, zum Raum hin
                FlammeAnlegen(a, feuer + new Vector3(-0.18f, 0, dz) - a.Mitte, FeuerVorgabe.Esse, 1.25f, () => NeubauInnenAktiv, wurzel, herd: true);
        // Gegenstände statt Reiterfenster: jeder Knoten „Gegenstand_<Name>“ bekommt eine Klickfläche um sein Netz, ein Namensschild
        // und leuchtet auf, solange die Maus darüber steht; der Klick öffnet seine Tafel (NeubauGegenstandVon, Hauptszene.RaumAngeklickt)
        var gegenstaende = new List<(string Kennung, MeshInstance3D Teil, Label3D Schild)>();
        foreach (var teil in wurzel.FindChildren("Gegenstand_*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            var name = teil.Name.ToString()["Gegenstand_".Length..];
            var kasten = teil.GetAabb();
            var mitte = wurzel.ToLocal(teil.ToGlobal(kasten.GetCenter()));
            var groesse = new Vector3(Mathf.Max(kasten.Size.X + 0.06f, 0.14f), Mathf.Max(kasten.Size.Y + 0.06f, 0.14f), Mathf.Max(kasten.Size.Z + 0.06f, 0.14f));
            var flaeche = Klickflaeche(NeubauGegenstand + name, mitte, groesse, wurzel);
            flaeche.Disabled = false; // der Raum wird beim Betreten angelegt: gleich scharf; beim Verlassen schaltet RaumansichtUmschalten alle Raumklicks ab
            a.Raumklicks.Add(flaeche);
            var schild = new Label3D
            {
                Text = NeubauGegenstandTitel.GetValueOrDefault(name, name), Position = mitte + new Vector3(0, groesse.Y / 2 + 0.14f, 0),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, PixelSize = 0.0017f, FontSize = 64, OutlineSize = 14,
                Modulate = new Color(1f, 0.93f, 0.72f), OutlineModulate = new Color(0.12f, 0.08f, 0.05f), Visible = false,
            };
            wurzel.AddChild(schild);
            gegenstaende.Add((NeubauGegenstand + name, teil, schild));
        }
        _neubauGlanz ??= new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add, AlbedoColor = new Color(1f, 0.82f, 0.42f, 0.30f),
        };
        // Stufe 1 ist ein einziger Raum: das Kontor (bisher der zweite Raum des Wohnhauses) ist hier nicht zugänglich
        if (a.Raeume.Count > 1 && !_neubauKontorGeregelt)
        {
            _neubauKontorGeregelt = true;
            var vorher = a.Raeume[1].Zugaenglich;
            a.Raeume[1] = a.Raeume[1] with { Zugaenglich = () => !NeubauOhneHinterzimmer(_spielerKonto) && (vorher?.Invoke() ?? true) };
        }
        // Die Haustür von innen: Klick führt hinaus, mit Klinke und Schloss wie beim Betreten (Mauszeiger.cs, Hauptszene.cs)
        if (Ort("Tuer_Innen") is { } tuer)
        {
            var hinaus = Klickflaeche(NeubauAusgang, tuer + new Vector3(0, 0, -0.08f), new Vector3(1.0f, 2.0f, 0.3f), wurzel);
            hinaus.Disabled = false;
            a.Raumklicks.Add(hinaus);
        }

        // Plätze der Familie aus dem Modell: Lage und Blickrichtung des Ankers, Haltung aus seinen extras („sitzt“, „ruehrt“,
        // „spielt“). Der Figuren-Baukasten braucht nur diese Liste (Reihenfolge: Oberhaupt, Ehepartner, Kinder).
        var plaetze = new[] { "Platz_Oberhaupt", "Platz_Partner", "Platz_Kind_1", "Platz_Kind_2", "Platz_Kind_3", "Platz_Kind_4" }.Select(name =>
        {
            if (wurzel.FindChild(name, true, false) is not Node3D anker) return new Arbeitsplatz(Vector3.Zero, Nord, Bewegung.Beten, Werkzeug.Keins);
            var lage = wurzel.GlobalTransform.AffineInverse() * anker.GlobalTransform;
            return ExtraText(anker, "haltung") switch
            {
                "ruehrt" => new Arbeitsplatz(lage.Origin, lage.Basis.GetEuler().Y, Bewegung.Ruehren, Werkzeug.Kelle),
                "spielt" => new Arbeitsplatz(lage.Origin, lage.Basis.GetEuler().Y, Bewegung.Spielen, Werkzeug.Keins),
                _ => new Arbeitsplatz(lage.Origin, lage.Basis.GetEuler().Y, Bewegung.Beten, Werkzeug.Keins, true),
            };
        }).ToArray();
        var figuren = new List<(Arbeiterfigur Figur, bool NurAbends)>();
        var familienStand = "";
        a.Raumzustaende.Add((zeit, arbeitszeit) =>
        {
            if (!IsInstanceValid(wurzel) || _neubauInnen != wurzel) return;
            foreach (var (kennung, teil, schild) in gegenstaende)
            {
                var an = _unterDerMaus == kennung;
                if (schild.Visible == an) continue;
                schild.Visible = an;
                teil.MaterialOverlay = an ? _neubauGlanz : null;
            }
            var w = _simulation?.Wirtschaft;
            var konto = _spielerKonto;
            var farbe = konto is not null ? Besitzerfarbe(konto) : OhneHausfarbe;
            foreach (var (kennung, knoten) in stuecke)
                knoten.Visible = konto is not null && w?.Wohnen.Daten?.Stueck(kennung) is { } stueck && w.Wohnen.HatEinrichtung(konto, stueck);
            foreach (var stoff in hausfarben) stoff.AlbedoColor = farbe;
            var familie = konto is not null ? w?.Familien.Von(konto) : null;
            var stand = familie is null ? "" : farbe + string.Join(",", familie.Lebende.Select(p => $"{p.Nummer}:{(w!.Familien.IstVolljaehrig(p) ? 1 : 0)}"));
            if (stand != familienStand)
            {
                foreach (var (f, _) in figuren) { a.Besucher.Remove(f); KnotenEntfernen(f.Koerper); }
                figuren.Clear();
                if (familie is not null) FamilieSetzen(a, Vector3.Zero, familie, w!.Familien, figuren, farbe, wurzel, plaetze);
                familienStand = stand;
            }
            foreach (var (f, nurAbends) in figuren) f.Koerper.Visible = !nurAbends || !arbeitszeit;
        });
    }

    private StandardMaterial3D? _neubauGlanz;
    private bool _neubauKontorGeregelt;

    // Umschauen wie in den Modellräumen, aber stufenlos: Steht die Maus am Bildrand, dreht sich die Raumkamera dorthin –
    // sanft, begrenzt (zur Seite je 55°, nach oben 35°, nach unten 25°), um den festen Standpunkt.
    private const float NeubauBlickTempo = 0.9f, NeubauGierMax = 0.96f, NeubauNickOben = 0.61f, NeubauNickUnten = -0.44f;
    private Transform3D _neubauBlickGrund;
    private float _neubauGier, _neubauNick, _neubauGierZiel, _neubauNickZiel;

    /// <summary>Je Bild in der Raumansicht (aus <see cref="RaumBildAktualisieren"/>): true, wenn der Raum im neuen Haus gezeigt wird.</summary>
    private bool NeubauUmschauen(double delta)
    {
        if (!NeubauInnenAktiv) return false;
        var fenster = GetViewport();
        var maus = fenster.GetMousePosition();
        var bild = fenster.GetVisibleRect().Size;
        var frei = !UnterTafel && fenster.GuiGetHoveredControl() is null && maus.X >= 0 && maus.Y >= 0 && maus.X <= bild.X && maus.Y <= bild.Y;
        if (frei)
        {
            var seite = maus.X < RandZone ? 1f : maus.X > bild.X - RandZone ? -1f : 0f;
            var hoehe = maus.Y < RandZone ? 1f : maus.Y > bild.Y - RandZone ? -1f : 0f;
            _neubauGierZiel = Mathf.Clamp(_neubauGierZiel + seite * NeubauBlickTempo * (float)delta, -NeubauGierMax, NeubauGierMax);
            _neubauNickZiel = Mathf.Clamp(_neubauNickZiel + hoehe * NeubauBlickTempo * 0.7f * (float)delta, NeubauNickUnten, NeubauNickOben);
        }
        var t = 1f - Mathf.Exp(-6f * (float)delta);
        _neubauGier = Mathf.Lerp(_neubauGier, _neubauGierZiel, t);
        _neubauNick = Mathf.Lerp(_neubauNick, _neubauNickZiel, t);
        var basis = new Basis(Vector3.Up, _neubauGier) * _neubauBlickGrund.Basis * new Basis(Vector3.Right, _neubauNick);
        _raumKamera.GlobalTransform = new Transform3D(basis, _neubauBlickGrund.Origin);
        return true;
    }

    /// <summary>Innenraum aus: unsichtbar und ohne Verarbeitung; Scheiben, Raumkamera und Sonne wie vorher.</summary>
    private void NeubauInnenAus()
    {
        if (_neubauInnen is not { } wurzel) return;
        _neubauInnen = null;
        if (IsInstanceValid(wurzel))
        {
            wurzel.Visible = false;
            wurzel.ProcessMode = ProcessModeEnum.Disabled;
            if (wurzel.GetParent()?.FindChild("Scheiben", true, false) is Node3D scheiben) scheiben.Visible = true;
        }
        _raumKamera.Environment = _raumUmgebung;
        _raumKamera.Fov = 50;
        _sonne.Visible = _raum is null;
        if (_raum is not null && _innen == _wohnhausAnsicht && _wohnhausAnsicht?.Innen is { } unten) unten.Visible = true; // weiter ins Kontor
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Probe: Häuserreihe und Messung (nur mit Startoption)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Startoptionen der Probe, einmal nach dem Aufbau der Stadt.
    /// <c>--neubaureihe=alt|neu|beide[@x,z]</c>: 16 Häuser im Raster 4 × 4 um die Stelle x,z (beide: links acht alte, rechts acht neue);
    /// <c>--neubaubrand=1</c>: je Viererreihe ein Haus mit Brandschaden, eins mit Brandschaden und Gerüst, eins im Leerstand. <c>--neubaumessung=&lt;Sekunden Vorlauf&gt;</c>: misst Zeichenaufrufe
    /// und GPU-Zeit vor dem Betreten, im Haus, nach dem Verlassen und zum Vergleich ohne den Knoten im Baum, schreibt sie ins Protokoll
    /// und beendet das Spiel.
    /// </summary>
    private void NeubauProbeStarten()
    {
        if (NeubauOption("--neubaureihe") is { Length: > 0 } reihe)
        {
            var teile = reihe.Split('@');
            var ort = Vector3.Zero;
            if (teile.Length > 1 && teile[1].Split(',') is { Length: 2 } xz
                && float.TryParse(xz[0], System.Globalization.CultureInfo.InvariantCulture, out var x)
                && float.TryParse(xz[1], System.Globalization.CultureInfo.InvariantCulture, out var z)) ort = new Vector3(x, 0, z);
            var brand = NeubauOption("--neubaubrand") is "1";
            for (var i = 0; i < 16; i++)
            {
                var (spalte, zeile) = (i % 4, i / 4);
                var datei = teile[0] switch { "neu" => NeubauWohnhaus, "beide" => spalte < 2 ? AltesWohnhaus : NeubauWohnhaus, _ => AltesWohnhaus };
                var mitte = ort + new Vector3((spalte - 1.5f) * 8.5f, 0, (zeile - 1.5f) * 11f);
                var g = new Node3D { Name = $"Neubaureihe_{i}", Position = AufFundament(mitte, 0, new Vector2(8, 10)) };
                AddChild(g);
                GruppenSockel(g, 0, new Vector2(8, 10)); // am Hang ein Bruchsteinsockel wie bei den Spielhäusern (Stadtansicht.Gelaende.cs)
                ModellEinsetzen(datei, g, new Vector3(0, 0, 0), new ModellZustand(Brand: brand && i % 4 is 1 or 2, Geruest: brand && i % 4 == 2, Leerstand: brand && i % 4 == 3, Besitzer: i % 3 == 0 ? Spieler : Rivale));
            }
        }
        if (NeubauOption("--neubaumessung") is { Length: > 0 } vorlauf)
            NeubauMessungStarten(float.TryParse(vorlauf, System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 15f);
    }

    // Messung der Probe: Abschnitte mit je 300 Bildern; Median und Mittel ins Protokoll. Die Stadt lebt (Passanten, Uhrzeit), deshalb
    // schwanken die Werte von Abschnitt zu Abschnitt etwas. Ob der ausgeschaltete Innenraum etwas kostet, zeigt der Vergleich der drei
    // letzten Abschnitte: ausgeschaltet im Baum – ganz aus dem Baum genommen – wieder ausgeschaltet im Baum.
    private const int NeubauMessBilder = 300;
    private static readonly string[] NeubauAbschnitte =
        { "vor dem Betreten", "im Haus", "nach dem Verlassen (InteriorRoot aus)", "InteriorRoot aus dem Baum genommen", "InteriorRoot wieder im Baum, aus" };
    private readonly List<(double Aufrufe, double Gpu, double Objekte, double Ecken)> _neubauWerte = new();
    private double _neubauMessZeit;
    private float _neubauVorlauf;
    private int _neubauAbschnitt;   // Index in NeubauAbschnitte
    private bool _neubauMisst;      // false: Wartezeit vor dem Abschnitt
    private Node3D? _neubauEntnommen;

    private void NeubauMessungStarten(float vorlauf)
    {
        _neubauVorlauf = vorlauf;
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        GetTree().ProcessFrame += NeubauMessSchritt;
    }

    private void NeubauMessSchritt()
    {
        _neubauMessZeit += GetProcessDeltaTime();
        if (!_neubauMisst)
        {
            if (_neubauMessZeit < (_neubauAbschnitt == 0 ? _neubauVorlauf : 4)) return;
            _neubauMisst = true;
            _neubauWerte.Clear();
            return;
        }
        _neubauWerte.Add((RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame),
                          RenderingServer.ViewportGetMeasuredRenderTimeGpu(GetViewport().GetViewportRid()) + (Fenster.PixelGpuZeit?.Invoke() ?? 0),
                          RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalObjectsInFrame),
                          RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalPrimitivesInFrame)));
        if (_neubauWerte.Count >= NeubauMessBilder) NeubauMessMelden();
    }

    private void NeubauMessMelden()
    {
        double Median(Func<(double Aufrufe, double Gpu, double Objekte, double Ecken), double> f) => _neubauWerte.Select(f).OrderBy(x => x).ElementAt(_neubauWerte.Count / 2);
        var haus = _wohnhausAnsicht is { } ansicht && _neubauHaeuser.TryGetValue(ansicht.Kennung, out var h) && IsInstanceValid(h) ? h : null;
        var innen = haus?.GetNodeOrNull<Node3D>("InteriorRoot");
        var zustand = innen is null ? "InteriorRoot: nicht im Baum"
            : $"InteriorRoot: sichtbar={innen.IsVisibleInTree()}, Verarbeitung={(innen.CanProcess() ? "an" : "aus")}";
        GD.Print($"[neubaumessung] {NeubauAbschnitte[_neubauAbschnitt]}: Zeichenaufrufe Median {Median(w => w.Aufrufe):0} (Mittel {_neubauWerte.Average(w => w.Aufrufe):0.0}), " +
                 $"GPU Median {Median(w => w.Gpu):0.00} ms (Mittel {_neubauWerte.Average(w => w.Gpu):0.00}), Objekte {Median(w => w.Objekte):0}, " +
                 $"Dreiecke {Median(w => w.Ecken):0}, Bilder {_neubauWerte.Count}; {zustand}");
        switch (_neubauAbschnitt)
        {
            case 0:
                var kennung = _wohnhausAnsicht?.Kennung;
                var drin = kennung is not null && InnenansichtBetreten(kennung);
                GD.Print($"[neubaumessung] betreten: {kennung} → {drin}, Innenraum am Ort: {NeubauInnenAktiv}");
                if (!drin) { GetTree().Quit(); return; }
                break;
            case 1:
                InnenansichtVerlassen();
                break;
            case 2 when innen is not null:
                _neubauEntnommen = innen;
                haus!.RemoveChild(innen);
                break;
            case 3 when _neubauEntnommen is not null && haus is not null:
                haus.AddChild(_neubauEntnommen);
                _neubauEntnommen = null;
                break;
            default:
                GetTree().Quit();
                return;
        }
        _neubauAbschnitt++;
        _neubauMessZeit = 0;
        _neubauMisst = false;
    }
}
