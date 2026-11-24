using Godot;

namespace Reichsgilde;

/// <summary>
/// Bodenflecke unter den Gebäuden – ein Teil der <c>partial class Stadtansicht</c>. Ein Haus soll nicht mit harter Kante auf der Wiese stehen: Rund um
/// seine Grundfläche liegt ein <see cref="Decal"/> mit dem Boden, den die Nutzung hinterlässt (Probe, Nutzer 24.11.2026):
/// <list type="bullet">
/// <item><b>Eine Tabelle</b> (<see cref="BodenfleckRegeln"/>) sagt je Modell (Anfang des Dateinamens), welche Art Fleck es bekommt und wie groß er ist;
/// die erste passende Zeile gilt. Die Textur hängt nur an der Art (<see cref="BodenfleckTexturen"/>) – wer eine neue Gebäudeart ausstattet, ergänzt
/// eine Zeile, keinen Code.</item>
/// <item><b>Größe</b>: fest aus der Tabelle (die Neubau-Häuser, wie bisher), als Rechteck im Modell (Hof des Bauernhofs) oder aus der Grundfläche des
/// Modells gerechnet, plus <see cref="BodenfleckRand"/> ringsum.</item>
/// <item><b>Texturen</b>: <c>game/assets/texturen/bodenflecke/</c> aus CC0-Aufnahmen, gebaut von <c>tools/modelle/bodenflecke/bodenflecke.py</c>:
/// grobe Formen mit klarem Hell-Dunkel und weichem Rand, damit sie im Pixel-Look lesbar bleiben.</item>
/// </list>
/// Aufgerufen aus <see cref="ModellEinsetzen"/> für jedes Modell. Nur zum Vergleichen: <c>--bodenflecke=0</c> (keine), <c>alt</c> (Stand vor der Probe:
/// nur Neubau-Häuser, alle mit der Erde) und <c>alle</c> (jedes Gebäude ohne eigene Zeile bekommt die Erde, zum Messen der Kosten).
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Die Arten der Bodenflecke; jede hat genau eine Textur.</summary>
    private enum Bodenfleckart
    {
        /// <summary>Festgetretene Erde (die bisherige Textur für alle).</summary>
        Erde,
        /// <summary>Wohnhaus: heller Traufkies am Haus, außen Erde.</summary>
        Wohnhaus,
        /// <summary>Schmiede: Kohlestaub und Asche.</summary>
        Schmiede,
        /// <summary>Hof: Matsch und Stroh.</summary>
        Hof,
    }

    private static readonly Dictionary<Bodenfleckart, string> BodenfleckTexturen = new()
    {
        [Bodenfleckart.Erde] = NeubauTexturen + "bodenfleck.png",
        [Bodenfleckart.Wohnhaus] = "res://assets/texturen/bodenflecke/bodenfleck_wohnhaus.png",
        [Bodenfleckart.Schmiede] = "res://assets/texturen/bodenflecke/bodenfleck_schmiede.png",
        [Bodenfleckart.Hof] = "res://assets/texturen/bodenflecke/bodenfleck_hof.png",
    };

    /// <summary>
    /// Eine Zeile der Tabelle: Modelle, deren Datei so anfängt, bekommen diese Art. <paramref name="Groesse"/>: feste Breite und Tiefe (Meter) um den
    /// Ursprung des Modells, um <paramref name="VersatzZ"/> verschoben; <paramref name="Flaeche"/>: ein Rechteck im Modell (x, z); ohne beides die
    /// gerechnete Grundfläche plus Rand.
    /// </summary>
    private sealed record BodenfleckRegel(string Anfang, Bodenfleckart Art, Vector2? Groesse = null, float VersatzZ = 0, Rect2? Flaeche = null);

    /// <summary>Welche Gebäudeart welchen Bodenfleck bekommt; die erste passende Zeile gilt.</summary>
    private static readonly BodenfleckRegel[] BodenfleckRegeln =
    [
        new(NeubauOrdner + "dom_", Bodenfleckart.Erde, new Vector2(21f, 39f)),
        new(NeubauOrdner + "rathaus", Bodenfleckart.Erde, new Vector2(17.5f, 11.5f)),
        new(NeubauOrdner + "wohnhaus_", Bodenfleckart.Wohnhaus, new Vector2(10.6f, 10.2f), -0.2f),
        new("gebaeude/schmied", Bodenfleckart.Schmiede),
        // der Hof zwischen Haus, Scheune und Stall: dieselbe Fläche wie der Erdstempel in game/daten/bodenstempel.json
        new("umland/bauernhof", Bodenfleckart.Hof, Flaeche: new Rect2(-13f, 0.6f, 10.2f, 9.4f)),
    ];

    /// <summary>So weit reicht ein gerechneter Bodenfleck über die Grundfläche des Modells hinaus (Meter, je Seite).</summary>
    private const float BodenfleckRand = 1.8f;

    /// <summary>Startoption <c>--bodenflecke=&lt;0|alt|alle&gt;</c>, nur zum Vergleichen und Messen (docs/STARTOPTIONEN.md).</summary>
    private static readonly string? BodenfleckOption = Wasserarten.Startoption("bodenflecke");

    private static readonly Dictionary<Bodenfleckart, Texture2D> _bodenfleckTexturen = new();

    private static Texture2D BodenfleckTextur(Bodenfleckart art) =>
        _bodenfleckTexturen.TryGetValue(art, out var textur) ? textur : _bodenfleckTexturen[art] = GD.Load<Texture2D>(BodenfleckTexturen[art]);

    /// <summary>Die Zeile der Tabelle für ein Modell, null wenn es keinen Bodenfleck bekommt (Innenräume nie).</summary>
    private static BodenfleckRegel? BodenfleckRegelVon(string datei)
    {
        if (datei.EndsWith("_innen") || BodenfleckOption == "0") return null;
        var regel = BodenfleckRegeln.FirstOrDefault(r => datei.StartsWith(r.Anfang, StringComparison.Ordinal));
        if (BodenfleckOption == "alt") // wie vor der Probe: nur der Neubau, überall die Erde, das Wohnhaus mit dem kleineren Fleck von damals
            return regel is not null && datei.StartsWith(NeubauOrdner)
                ? regel with { Art = Bodenfleckart.Erde, Groesse = regel.Art == Bodenfleckart.Wohnhaus ? new Vector2(8.6f, 8.2f) : regel.Groesse } : null;
        if (regel is null && BodenfleckOption == "alle" && (datei.StartsWith("gebaeude/") || datei.StartsWith("umland/")) && !datei.Contains("stadtmauer"))
            return new BodenfleckRegel(datei, Bodenfleckart.Erde);
        return regel;
    }

    /// <summary>
    /// Legt einem eingesetzten Modell seinen Bodenfleck unter (aus <see cref="ModellEinsetzen"/>): ein Decal, nur dünn über dem Boden (Style-Guide 5.1),
    /// in die Ferne ausgeblendet. Modelle ohne Zeile in der Tabelle: nichts.
    /// </summary>
    private static void BodenfleckAnlegen(string datei, Node3D modell)
    {
        if (modell.HasNode("Bodenfleck") || BodenfleckRegelVon(datei) is not { } regel) return;
        Rect2 flaeche;
        if (regel.Flaeche is { } fest) flaeche = fest;
        else if (regel.Groesse is { } groesse) flaeche = new Rect2(-groesse.X / 2, regel.VersatzZ - groesse.Y / 2, groesse.X, groesse.Y);
        else if (ModellGrundflaeche(modell) is { } grund) flaeche = grund.Grow(BodenfleckRand);
        else return;
        var mitte = flaeche.GetCenter();
        modell.AddChild(new Decal
        {
            Name = "Bodenfleck", Position = new Vector3(mitte.X, 0.02f, mitte.Y),
            Size = new Vector3(flaeche.Size.X, 0.36f, flaeche.Size.Y),
            TextureAlbedo = BodenfleckTextur(regel.Art), AlbedoMix = 0.85f, NormalFade = 0.6f, UpperFade = 0.6f, LowerFade = 0.6f,
            DistanceFadeEnabled = true, DistanceFadeBegin = 160, DistanceFadeLength = 40,
        });
    }

    /// <summary>
    /// Die Grundfläche eines Modells (x, z im Modell): alle sichtbaren Netze, die bis an den Boden reichen (Unterkante unter einem halben Meter) –
    /// Dächer, Fahnen und ausgeblendete Stufen zählen nicht. Null, wenn das Modell nichts am Boden hat.
    /// </summary>
    private static Rect2? ModellGrundflaeche(Node3D modell)
    {
        Rect2? flaeche = null;
        foreach (var teil in modell.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (teil.Mesh is null) continue;
            var lage = Transform3D.Identity;
            var sichtbar = true;
            for (Node? knoten = teil; knoten is not null && knoten != modell; knoten = knoten.GetParent())
            {
                if (knoten is not Node3D raum) continue;
                sichtbar &= raum.Visible;
                lage = raum.Transform * lage;
            }
            if (!sichtbar) continue;
            var kasten = lage * teil.GetAabb();
            if (kasten.Position.Y > 0.5f) continue;
            var rechteck = new Rect2(kasten.Position.X, kasten.Position.Z, kasten.Size.X, kasten.Size.Z);
            flaeche = flaeche is { } bisher ? bisher.Merge(rechteck) : rechteck;
        }
        return flaeche;
    }
}
