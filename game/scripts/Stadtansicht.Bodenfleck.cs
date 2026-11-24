using Godot;

namespace Reichsgilde;

/// <summary>
/// Bodenflecke unter den Gebäuden – ein Teil der <c>partial class Stadtansicht</c>. Ein Haus soll nicht mit harter Kante auf dem Boden stehen: Rund um
/// seine Grundfläche liegt ein <see cref="Decal"/> mit dem Boden, den die Nutzung hinterlässt (Nutzer 24.11.2026):
/// <list type="bullet">
/// <item><b>Eine Tabelle</b> (<see cref="BodenfleckRegeln"/>) sagt je Modell (Anfang des Dateinamens), welche Art Fleck es bekommt und wie groß er ist;
/// die erste passende Zeile gilt. Die Textur hängt nur an Art und Größenklasse (<see cref="BodenfleckTextur"/>) – wer eine neue Gebäudeart
/// ausstattet, ergänzt eine Zeile, keinen Code.</item>
/// <item><b>Größe</b>: fest aus der Tabelle (die Neubau-Häuser), als Rechtecke im Modell (Hof und Mühle des Bauernhofs) oder aus der Grundfläche des
/// Modells gerechnet, plus Rand ringsum.</item>
/// <item><b>Zwei Größenklassen</b>: Das Bild wird über die ganze Fläche gezogen. Ab <see cref="BodenfleckGrossAb"/> m Kante gilt das große Bild, in dem
/// Rand, Bänder und Flecken halb so groß sind – so bleibt der Ring um das Haus in Metern etwa gleich breit.</item>
/// <item><b>Am Hang</b> steht das Gebäude auf einem Sockel (<see cref="SockelBauen"/>). Dann liegt der Fleck auf der Sockelplatte und deckt sie ganz
/// (der weiche Rand fällt über die Kante ins Leere), und um den Fuß des Sockels liegt ein Saum aus dunkler Erde (<see cref="SockelsaumAnlegen"/>,
/// ein zweites Decal nur auf dem Gelände).</item>
/// <item><b>Texturen</b>: <c>game/assets/texturen/bodenflecke/</c> aus CC0-Aufnahmen, gebaut von <c>tools/modelle/bodenflecke/bodenflecke.py</c>:
/// grobe Formen mit klarem Hell-Dunkel und weichem Rand, damit sie im Pixel-Look lesbar bleiben.</item>
/// </list>
/// Aufgerufen aus <see cref="ModellEinsetzen"/> für jedes Modell und aus <see cref="SockelBauen"/>. Nur zum Vergleichen: <c>--bodenflecke=0</c>
/// (keine), <c>alt</c> (Stand vor dem 24.11.2026: nur Neubau-Häuser, alle mit der Erde, kein Saum) und <c>alle</c> (jedes Gebäude ohne eigene Zeile
/// bekommt die Erde, zum Messen der Kosten). Einwohnerhäuser haben keine Zeile: Sie bekommen später einen Streifen je Häuserzeile.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Die Arten der Bodenflecke; jede hat ein kleines und ein großes Bild (die Erde nur eines).</summary>
    private enum Bodenfleckart
    {
        /// <summary>Festgetretene Erde (das Bild von vor dem 24.11.2026, nur noch für die Vergleichsoptionen).</summary>
        Erde,
        /// <summary>Wohnhaus: heller Traufkies am Haus, außen Erde.</summary>
        Wohnhaus,
        /// <summary>Schmiede: Kohlestaub und Asche.</summary>
        Schmiede,
        /// <summary>Hof und Mühle: Matsch und Stroh.</summary>
        Hof,
        /// <summary>Schreiner: Hobelspäne und Sägemehl.</summary>
        Schreiner,
        /// <summary>Steinmetz: Steinstaub und Splitt.</summary>
        Steinmetz,
        /// <summary>Wirt: nasse Erde und zertretenes Stroh.</summary>
        Wirt,
        /// <summary>Alchimist und Parfumeur: fleckig-dunkler Grund.</summary>
        Dunkel,
        /// <summary>Dom, Rathaus, Kirchen, Stapelhaus: gepflasterter Vorplatz.</summary>
        Steinplatten,
        /// <summary>Marktstände: dünnes, zertretenes Stroh.</summary>
        Marktstroh,
        /// <summary>Der Erdsaum am Fuß eines Sockels.</summary>
        Saum,
    }

    /// <summary>
    /// Eine Zeile der Tabelle: Modelle, deren Datei so anfängt, bekommen diese Art. <paramref name="Groesse"/>: feste Breite und Tiefe (Meter) um den
    /// Ursprung des Modells, um <paramref name="VersatzZ"/> verschoben; <paramref name="Flaechen"/>: Rechtecke im Modell (x, z), je eines ein Fleck;
    /// ohne beides die gerechnete Grundfläche plus <paramref name="Rand"/> je Seite. <paramref name="AufSockel"/>: Steht das Gebäude auf einem
    /// Sockel, deckt der Fleck dessen Platte (nicht für Teile, die nur auf einem fremden Platz stehen, wie die Marktstände).
    /// </summary>
    private sealed record BodenfleckRegel(string Anfang, Bodenfleckart Art, Vector2? Groesse = null, float VersatzZ = 0, Rect2[]? Flaechen = null,
        float Rand = 1.8f, bool AufSockel = true);

    /// <summary>Welche Gebäudeart welchen Bodenfleck bekommt; die erste passende Zeile gilt.</summary>
    private static readonly BodenfleckRegel[] BodenfleckRegeln =
    [
        new(NeubauOrdner + "dom_", Bodenfleckart.Steinplatten, new Vector2(21f, 39f)),
        new(NeubauOrdner + "rathaus", Bodenfleckart.Steinplatten, new Vector2(17.5f, 11.5f)),
        new(NeubauOrdner + "wohnhaus_", Bodenfleckart.Wohnhaus, new Vector2(10.6f, 10.2f), -0.2f),
        new("gebaeude/schmied", Bodenfleckart.Schmiede),
        new("gebaeude/tischler", Bodenfleckart.Schreiner),
        new("gebaeude/steinmetz", Bodenfleckart.Steinmetz),
        new("gebaeude/wirt", Bodenfleckart.Wirt),
        new("gebaeude/alchimist", Bodenfleckart.Dunkel),
        new("gebaeude/parfumeur", Bodenfleckart.Dunkel),
        new("gebaeude/prediger", Bodenfleckart.Wohnhaus),
        new("gebaeude/geldleiher", Bodenfleckart.Wohnhaus),
        new("gebaeude/gardist", Bodenfleckart.Wohnhaus),
        new("gebaeude/dieb", Bodenfleckart.Wohnhaus),
        new("gebaeude/dom", Bodenfleckart.Steinplatten),
        new("gebaeude/koelner_dom", Bodenfleckart.Steinplatten),
        new("gebaeude/koelner_rathaus", Bodenfleckart.Steinplatten),
        new("gebaeude/koeln_", Bodenfleckart.Steinplatten), // die romanischen Kirchen Kölns
        new("gebaeude/stapelhaus", Bodenfleckart.Steinplatten),
        new("gebaeude/marktstaende", Bodenfleckart.Marktstroh, Rand: 0.9f, AufSockel: false),
        // der Hof zwischen Haus, Scheune und Stall (dieselbe Fläche wie der Erdstempel in game/daten/bodenstempel.json) und der Platz um die Bockwindmühle
        new("umland/bauernhof", Bodenfleckart.Hof, Flaechen: [new Rect2(-13f, 0.6f, 10.2f, 9.4f), new Rect2(-9.5f, -9.8f, 8f, 8f)], AufSockel: false),
    ];

    /// <summary>Ab dieser Kante (Meter, längere Seite) nimmt ein Fleck das große Bild.</summary>
    private const float BodenfleckGrossAb = 13f;

    /// <summary>So breit ist der weiche Rand im Bild, als Anteil der halben Kante (klein, groß) – wie in <c>bodenflecke.py</c>.</summary>
    private const float BodenfleckRandKlein = 0.17f, BodenfleckRandGross = 0.09f;

    /// <summary>So weit reicht der Erdsaum über die Kante eines Sockels hinaus (Meter).</summary>
    private const float SockelsaumBreite = 1.6f;

    /// <summary>Startoption <c>--bodenflecke=&lt;0|alt|alle&gt;</c>, nur zum Vergleichen und Messen (docs/STARTOPTIONEN.md).</summary>
    private static readonly string? BodenfleckOption = Wasserarten.Startoption("bodenflecke");

    private static readonly Dictionary<(Bodenfleckart, bool), Texture2D> _bodenfleckTexturen = new();

    /// <summary>Das Bild einer Art in der Größenklasse (<paramref name="gross"/>), einmal geladen.</summary>
    private static Texture2D BodenfleckTextur(Bodenfleckart art, bool gross)
    {
        if (art == Bodenfleckart.Erde) gross = false;
        if (_bodenfleckTexturen.TryGetValue((art, gross), out var textur)) return textur;
        var pfad = art == Bodenfleckart.Erde
            ? NeubauTexturen + "bodenfleck.png"
            : $"res://assets/texturen/bodenflecke/bodenfleck_{art.ToString().ToLowerInvariant()}_{(gross ? "gross" : "klein")}.png";
        return _bodenfleckTexturen[(art, gross)] = GD.Load<Texture2D>(pfad);
    }

    private static bool BodenfleckGross(Vector2 groesse) => Mathf.Max(groesse.X, groesse.Y) >= BodenfleckGrossAb;

    /// <summary>Die Zeile der Tabelle für ein Modell, null wenn es keinen Bodenfleck bekommt (Innenräume nie).</summary>
    private static BodenfleckRegel? BodenfleckRegelVon(string datei)
    {
        if (datei.EndsWith("_innen") || BodenfleckOption == "0") return null;
        var regel = BodenfleckRegeln.FirstOrDefault(r => datei.StartsWith(r.Anfang, StringComparison.Ordinal));
        if (BodenfleckOption == "alt") // wie vor dem 24.11.2026: nur der Neubau, überall die Erde, das Wohnhaus mit dem kleineren Fleck von damals
            return regel is not null && datei.StartsWith(NeubauOrdner)
                ? regel with { Art = Bodenfleckart.Erde, AufSockel = false, Groesse = regel.Art == Bodenfleckart.Wohnhaus ? new Vector2(8.6f, 8.2f) : regel.Groesse } : null;
        if (regel is null && BodenfleckOption == "alle" && (datei.StartsWith("gebaeude/") || datei.StartsWith("umland/")) && !datei.Contains("stadtmauer"))
            return new BodenfleckRegel(datei, Bodenfleckart.Erde, AufSockel: false);
        return regel;
    }

    /// <summary>Die Sockel der Stadt (für die Flecke auf ihrer Platte): der Quader und seine Fläche.</summary>
    private readonly List<(MeshInstance3D Sockel, Vector2 Groesse)> _fleckSockel = new();

    /// <summary>Die Bodenflecke, die auf eine Sockelplatte gehören, wenn ihr Gebäude auf einem Sockel steht.</summary>
    private readonly List<(Decal Fleck, Bodenfleckart Art)> _sockelFlecke = new();

    /// <summary>
    /// Legt einem eingesetzten Modell seinen Bodenfleck unter (aus <see cref="ModellEinsetzen"/>): ein Decal, nur dünn über dem Boden (Style-Guide 5.1),
    /// in die Ferne ausgeblendet. Modelle ohne Zeile in der Tabelle: nichts.
    /// </summary>
    private void BodenfleckAnlegen(string datei, Node3D modell)
    {
        if (modell.HasNode("Bodenfleck") || BodenfleckRegelVon(datei) is not { } regel) return;
        Rect2[] flaechen;
        if (regel.Flaechen is { } fest) flaechen = fest;
        else if (regel.Groesse is { } groesse) flaechen = [new Rect2(-groesse.X / 2, regel.VersatzZ - groesse.Y / 2, groesse.X, groesse.Y)];
        else if (ModellGrundflaeche(modell) is { } grund) flaechen = [grund.Grow(regel.Rand)];
        else return;
        for (var i = 0; i < flaechen.Length; i++)
        {
            var (mitte, masse) = (flaechen[i].GetCenter(), flaechen[i].Size);
            var fleck = new Decal
            {
                Name = i == 0 ? "Bodenfleck" : $"Bodenfleck{i + 1}", Position = new Vector3(mitte.X, 0.02f, mitte.Y),
                Size = new Vector3(masse.X, 0.36f, masse.Y),
                TextureAlbedo = BodenfleckTextur(regel.Art, BodenfleckGross(masse)), AlbedoMix = 0.85f, NormalFade = 0.6f, UpperFade = 0.6f, LowerFade = 0.6f,
                DistanceFadeEnabled = true, DistanceFadeBegin = 160, DistanceFadeLength = 40,
            };
            modell.AddChild(fleck);
            if (!regel.AufSockel) continue;
            _sockelFlecke.RemoveAll(f => !IsInstanceValid(f.Fleck));
            _sockelFlecke.Add((fleck, regel.Art));
            if (FleckAufEinenSockel(fleck, regel.Art)) continue;
            // manche Bauten werden am Ursprung eingesetzt und erst danach an ihren Platz gerückt (Stapelhaus, Kirchen): noch einmal, wenn alles steht
            var art = regel.Art;
            Callable.From(() => { if (IsInstanceValid(fleck)) FleckAufEinenSockel(fleck, art); }).CallDeferred();
        }
    }

    /// <summary>Legt den Fleck auf die Platte des Sockels, auf dem sein Gebäude steht; false, wenn es auf keinem steht.</summary>
    private bool FleckAufEinenSockel(Decal fleck, Bodenfleckart art)
    {
        _fleckSockel.RemoveAll(s => !IsInstanceValid(s.Sockel));
        foreach (var (sockel, flaeche) in _fleckSockel)
            if (FleckAufSockel(fleck, art, sockel, flaeche)) return true;
        return false;
    }

    /// <summary>
    /// Merkt einen neuen Sockel (aus <see cref="SockelBauen"/>), legt die Flecke der Gebäude, die schon darauf stehen, auf seine Platte und gibt ihm
    /// den Erdsaum um den Fuß. Sockel aus Erde (Umland) bekommen nichts: Dort liegt die Böschung bzw. der Bodenstempel.
    /// </summary>
    private void SockelFleckeAnlegen(MeshInstance3D sockel, Vector2 groesse)
    {
        if (BodenfleckOption is "0" or "alt") return;
        _fleckSockel.RemoveAll(s => !IsInstanceValid(s.Sockel));
        _fleckSockel.Add((sockel, groesse));
        _sockelFlecke.RemoveAll(f => !IsInstanceValid(f.Fleck));
        foreach (var (fleck, art) in _sockelFlecke) FleckAufSockel(fleck, art, sockel, groesse);
        SockelsaumAnlegen(sockel, groesse);
    }

    /// <summary>Die Lage eines Knotens gegenüber der Stadt (oder der Wurzel seines Astes, solange der noch nicht in der Stadt hängt).</summary>
    private Transform3D LageInDerStadt(Node3D knoten)
    {
        var lage = Transform3D.Identity;
        for (Node? k = knoten; k is not null && k != this; k = k.GetParent())
            if (k is Node3D raum) lage = raum.Transform * lage;
        return lage;
    }

    /// <summary>
    /// Steht das Gebäude des Flecks auf diesem Sockel (der Ursprung des Modells liegt über der Platte), dann deckt der Fleck die ganze Platte: so groß
    /// wie sie, vergrößert um den weichen Rand des Bildes, der damit über die Kante fällt. False, wenn der Sockel nicht zu diesem Gebäude gehört.
    /// </summary>
    private bool FleckAufSockel(Decal fleck, Bodenfleckart art, MeshInstance3D sockel, Vector2 groesse)
    {
        if (fleck.GetParent() is not Node3D modell || sockel.Mesh is not BoxMesh quader) return false;
        var oben = LageInDerStadt(sockel) * new Transform3D(Basis.Identity, new Vector3(0, quader.Size.Y / 2, 0)); // Mitte der Platte
        var lageModell = LageInDerStadt(modell);
        var ort = oben.AffineInverse() * lageModell.Origin;
        if (Mathf.Abs(ort.X) > groesse.X / 2 || Mathf.Abs(ort.Z) > groesse.Y / 2 || Mathf.Abs(ort.Y) > 0.5f) return false;
        var gross = BodenfleckGross(groesse);
        var mehr = 1f / (1f - (gross ? BodenfleckRandGross : BodenfleckRandKlein));
        fleck.Transform = lageModell.AffineInverse() * oben * new Transform3D(Basis.Identity, new Vector3(0, 0.02f, 0));
        fleck.Size = new Vector3(groesse.X * mehr, 0.36f, groesse.Y * mehr);
        fleck.TextureAlbedo = BodenfleckTextur(art, gross);
        fleck.AlbedoMix = 1f; // die Platte ist helles Pflaster: Der Fleck soll sie ganz zeigen, nicht nur tönen
        return true;
    }

    /// <summary>
    /// Der Erdsaum am Fuß eines Sockels: ein Decal von der Platte bis unter den Sockel, <see cref="SockelsaumBreite"/> m über die Kante hinaus, nur auf
    /// dem Gelände (Ebene der Torerde – Sockelwände, Figuren und Karren bekommen nichts ab). Dunkle, feuchte Erde, die weich in den Boden ausläuft.
    /// </summary>
    private static void SockelsaumAnlegen(MeshInstance3D sockel, Vector2 groesse)
    {
        if (sockel.Mesh is not BoxMesh quader || sockel.HasNode("Sockelsaum")) return;
        var flaeche = groesse + new Vector2(SockelsaumBreite, SockelsaumBreite) * 2;
        sockel.AddChild(new Decal
        {
            Name = "Sockelsaum", Size = new Vector3(flaeche.X, quader.Size.Y + 0.4f, flaeche.Y),
            TextureAlbedo = BodenfleckTextur(Bodenfleckart.Saum, BodenfleckGross(flaeche)), AlbedoMix = 0.9f,
            NormalFade = 0.3f, UpperFade = 0.05f, LowerFade = 0.05f, CullMask = TorErdeEbene,
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
