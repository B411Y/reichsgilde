using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die Füllung der Restflächen zeichnen – ein Teil der <c>partial class Stadtansicht</c> (Setzen der Teile: <c>Stadtansicht.Restflaechen.cs</c>).
///
/// Ein MultiMesh je Art zeichnete jede Art mit jedem ihrer Stoffe einmal: in Köln +186 bis +206 Zeichenaufrufe (und nur bis Zoom 75, dort nur große Teile). Jetzt wird alles zu
/// **Netzen mit einer Fläche je Stoff** gebacken (<see cref="Fuellnetz"/>; gleiche Stoffe aus verschiedenen Baukästen kommen in eine Fläche). Godot zeichnet jede Fläche zweimal
/// (Tiefenvorlauf und Bild), die Zeichenaufrufe sind also etwa das Doppelte der Flächen. Drei Netze-Arten, je nach Zoom:
/// <list type="bullet">
/// <item><b>Nah</b> (Zoom bis <see cref="FuellNahBisZoom"/>, 25 m): je Zelle (nahe Restflächen eines Themas) ein Netz mit allen Teilen, unverändert im Aussehen; was nicht im Bild liegt,
/// fällt beim Ausblenden weg, im dichtesten Blick von Köln sind es rund 60 Zeichenaufrufe. Gebacken beim Hinsehen, höchstens <see cref="FuellBackZeit"/> Millisekunden je Bild.</item>
/// <item><b>Mittel und fern</b> (50 und 100 m, <see cref="FuellFernAbZoom"/>): je ein Netz der <i>ganzen</i> Füllung (<see cref="Fuellsatz"/>), also unabhängig davon, wie viele Zellen im
/// Bild liegen, nur mit den Stoffen der Füllung; seltene Stoffe nehmen dafür einen ähnlichen häufigen (<c>fern_stoffe</c> in <c>restflaechen.json</c>). Das mittlere zeigt alle Teile, das
/// weite lässt die Kleinstteile weg (Gras, Kraut, Steine, Reisig, Pilze, Blumen). Gebacken in Schritten über mehrere Bilder, beim ersten Bedarf.</item>
/// <item><b>Schatten</b> (je Zelle): Die Schattenwerfer stehen zusätzlich in einem Netz mit einer Fläche und <c>ShadowsOnly</c>; die Netze fürs Bild werfen selbst keinen Schatten.
/// Gezeigt bis Zoom 37 (alle) bzw. 75 (nur die großen).</item>
/// </list>
/// Je Jahreszeit eine Fassung (Winter: kahle Fassungen <c>_Winter</c>, ohne die Sommerteile); bis die neue gebacken ist, bleibt die alte stehen.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Bis zu diesem Zoom (sichtbare Höhe in Metern) zeigen die Schatten aller Schattenwerfer (nahe Pixel-Zoomstufe 25 m); bis <see cref="FuellGrossBisZoom"/> nur die der großen Teile (50 m).</summary>
    private const float FuellNahBisZoom = 37f, FuellGrossBisZoom = 75f;

    /// <summary>Über diesem Zoom zeigt das ferne Bildnetz die Kleinstteile nicht mehr (weite Pixel-Zoomstufe 100 m): Gras, Kraut, Steine, Reisig, Pilze, Blumen sind dort nur noch Flimmern.</summary>
    private const float FuellFernAbZoom = 75f;

    /// <summary>Wie weit (Meter) Restflächen eines Themas auseinanderliegen dürfen und trotzdem eine Zelle bilden, und wie groß eine Zelle höchstens wird.</summary>
    private const float FuellAbstand = 12f, FuellGroesst = 64f;

    /// <summary>Wie lange (Millisekunden) ein Bild höchstens mit dem Backen der Füllung verbringt (mindestens ein Schritt).</summary>
    private const int FuellBackZeit = 8;

    private Node3D? _fuellWurzel;
    private readonly List<Fuellzelle> _fuellZellen = new();
    private readonly Fuellnetz _fuellBacker = new();
    private readonly StandardMaterial3D _fuellSchattenStoff = new() { ResourceName = "Fuellung Schatten", CullMode = BaseMaterial3D.CullModeEnum.Disabled };
    private readonly Fuellsatz?[,] _fuellSaetze = new Fuellsatz?[2, 2]; // [Jahreszeit 0 Sommer/1 Winter, Stufe 0 alle Teile/1 ohne Kleinstteile]
    private (bool Nah, bool Gross, bool Winter, int Stufe, bool ImBild)? _fuellSichtbar;
    private Stadtkarte? _fuellKarte;
    private int _fuellDichte = 100, _fuellGebacken;
    private double _fuellDichteSeit;

    private Shader? _pfuetzeShader;
    private ShaderMaterial? _pfuetzeStoff;

    /// <summary>
    /// Das Material der Pfützen und des stehenden Wassers der Füllung (<c>pfuetze.gdshader</c>): Himmelsspiegelung, bei Regen glänzender, in Hoch mit Tropfenringen. Eines für alle
    /// Netze (eine Fläche je Netz wie vorher das „Wasser“). Null, wenn die Datei fehlt: dann bleibt das einfache Material des Modells.
    /// </summary>
    private ShaderMaterial? PfuetzeStoff()
    {
        if (_pfuetzeStoff is not null) return _pfuetzeStoff;
        const string pfad = "res://assets/shader/pfuetze.gdshader";
        if (!ResourceLoader.Exists(pfad)) return null;
        _pfuetzeShader ??= GD.Load<Shader>(pfad);
        _pfuetzeStoff = new ShaderMaterial { Shader = _pfuetzeShader, ResourceName = "Pfuetze" };
        PfuetzeStufeSetzen();
        return _pfuetzeStoff;
    }

    /// <summary>Die Stufe der Pfütze folgt der Einstellung „Boden“ (Niedrig, Mittel, Hoch).</summary>
    private void PfuetzeStufeSetzen() => _pfuetzeStoff?.SetShaderParameter("stufe", (int)Fenster.AktuelleBodenqualitaet);

    /// <summary>Gibt den Flächen „Pfuetze“ eines frisch gebackenen Netzes das Pfützen-Material (ferne Netze haben sie meist schon mit dem „Wasser“ zusammengelegt).</summary>
    private void PfuetzenTauschen(ArrayMesh netz)
    {
        for (var i = 0; i < netz.GetSurfaceCount(); i++)
            if (netz.SurfaceGetMaterial(i) is { ResourceName: "Pfuetze" } && PfuetzeStoff() is { } stoff) netz.SurfaceSetMaterial(i, stoff);
    }

    /// <summary>Ein Bildnetz der ganzen Füllung (je Jahreszeit und Stufe): zuerst die Arbeit (Sammeln, dann Fläche für Fläche backen), danach der Knoten.</summary>
    private sealed class Fuellsatz
    {
        public IEnumerator<int>? Arbeit;
        public MeshInstance3D? Knoten;
        public bool Gestartet;
        public int Ecken, Flaechen;
    }

    /// <summary>Eine Zelle der Füllung (nahe Restflächen eines Themas): ihre Teile (nach Natur-Dichte), ihr Rahmen zum Ausblenden und die gebackenen Schattennetze.</summary>
    private sealed class Fuellzelle
    {
        public readonly List<Fuellteil> Teile = new();
        public Vector3 Mitte;
        public float Radius;
        public bool HatGrosse;
        /// <summary>Das Bildnetz der Zelle mit allen Teilen (nah) je Jahreszeit; null = noch nicht gebacken.</summary>
        public readonly MeshInstance3D?[] Bild = new MeshInstance3D?[2];
        public readonly bool[] BildGebacken = new bool[2];
        /// <summary>Schattennetz je [Jahreszeit, Art (0 alle Schattenwerfer, 1 nur die großen)]; null = noch nicht gebacken, leere Liste = keine Schattenwerfer.</summary>
        public readonly List<MeshInstance3D>?[,] Schatten = new List<MeshInstance3D>?[2, 2];
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sortiert die Füllung in Zellen (nahe Restflächen eines Themas) und räumt die alten Netze weg; gebacken wird erst, wenn die Füllung im Bild liegt (<see cref="FuellungNachZoom"/>).
    /// Kästen, auf denen ein Gebäude steht, bleiben leer. Auch nach Änderungen der Karte aufrufbar (Kartenwerkstatt) – es wird nichts neu gewürfelt, nur neu gezeichnet.
    /// </summary>
    internal void FuellungAufbauen(Stadtkarte karte)
    {
        if (_fuellWurzel is not null && IsInstanceValid(_fuellWurzel)) _fuellWurzel.QueueFree();
        _fuellKarte = karte;
        _fuellZellen.Clear();
        Array.Clear(_fuellSaetze);
        _fuellSichtbar = null;
        _fuellDichte = Fenster.NaturDichte;
        _fuellDichteSeit = 0;
        _fuellWurzel = new Node3D { Name = "Restflaechenfuellung" };
        AddChild(_fuellWurzel);
        if (_fuellFlaechen.Count == 0) return;

        var frei = Naturfreiraum.Aus(karte, Modellmass);
        var weggelassen = 0;
        var anteil = Fenster.NaturDichte / 100f;
        // Zellen: Restflächen desselben Themas, die nah beieinander liegen (höchstens FuellAbstand m Lücke, die Zelle nie breiter als FuellGroesst m), bilden eine Zelle (Schatten, Ausblenden)
        var zellen = new List<(string Thema, Vector2 Min, Vector2 Max, List<Fuellteil> Teile)>();
        foreach (var (kennung, kaesten) in _fuellFlaechen)
        {
            var thema = _fuellThemen?.GetValueOrDefault(kennung) ?? "";
            var teile = new List<Fuellteil>();
            foreach (var (kasten, kastenTeile) in kaesten)
            {
                if (Restflaechenfuellung.Bebaut(kasten, frei)) { weggelassen++; continue; }
                teile.AddRange(kastenTeile);
            }
            if (teile.Count == 0) continue;
            var (min, max) = (new Vector2(teile.Min(t => t.Lage.Origin.X), teile.Min(t => t.Lage.Origin.Z)), new Vector2(teile.Max(t => t.Lage.Origin.X), teile.Max(t => t.Lage.Origin.Z)));
            var ziel = zellen.FindIndex(z => z.Thema == thema && z.Min.X - FuellAbstand <= max.X && max.X - FuellAbstand <= z.Max.X && z.Min.Y - FuellAbstand <= max.Y && max.Y - FuellAbstand <= z.Max.Y
                                             && Math.Max(z.Max.X, max.X) - Math.Min(z.Min.X, min.X) <= FuellGroesst && Math.Max(z.Max.Y, max.Y) - Math.Min(z.Min.Y, min.Y) <= FuellGroesst);
            if (ziel < 0) zellen.Add((thema, min, max, teile));
            else
            {
                var z = zellen[ziel];
                z.Teile.AddRange(teile);
                zellen[ziel] = (thema, new Vector2(Math.Min(z.Min.X, min.X), Math.Min(z.Min.Y, min.Y)), new Vector2(Math.Max(z.Max.X, max.X), Math.Max(z.Max.Y, max.Y)), z.Teile);
            }
        }
        foreach (var (_, _, _, liste) in zellen)
        {
            // Natur-Dichte: Kleinteile je Art gemischt (fest aus der Lage) und nur die ersten gezeigt, so verteilt sich das Fehlende über die ganze Zelle
            var zelle = new Fuellzelle();
            foreach (var art in liste.GroupBy(t => (t.Datei, t.Knoten)).OrderBy(g => g.Key.Datei, StringComparer.Ordinal).ThenBy(g => g.Key.Knoten, StringComparer.Ordinal))
            {
                var arten = art.OrderBy(t => Restflaechenfuellung.Wuerfel("f", (int)(t.Lage.Origin.X * 10), (int)(t.Lage.Origin.Z * 10))).ToList();
                zelle.Teile.AddRange(arten.Take(arten[0].Klein && anteil < 1 ? Mathf.RoundToInt(arten.Count * anteil) : arten.Count));
            }
            if (zelle.Teile.Count == 0) continue;
            var (min, max) = (new Vector3(float.MaxValue, 0, float.MaxValue), new Vector3(float.MinValue, 0, float.MinValue));
            foreach (var t in zelle.Teile)
            {
                min = new Vector3(Math.Min(min.X, t.Lage.Origin.X), 0, Math.Min(min.Z, t.Lage.Origin.Z));
                max = new Vector3(Math.Max(max.X, t.Lage.Origin.X), 0, Math.Max(max.Z, t.Lage.Origin.Z));
            }
            zelle.Mitte = new Vector3((min.X + max.X) / 2, zelle.Teile.Average(t => t.Lage.Origin.Y), (min.Z + max.Z) / 2);
            zelle.Radius = new Vector2(max.X - min.X, max.Z - min.Z).Length() / 2;
            zelle.HatGrosse = zelle.Teile.Any(t => t.Gross && t.Schatten);
            _fuellZellen.Add(zelle);
        }
        if (weggelassen > 0) GD.Print($"Restflächen: {weggelassen} Kästen unter Gebäuden ohne Füllung.");
        FuellNetzeVorladen(_fuellZellen.SelectMany(z => z.Teile).Select(t => (t.Datei, t.Knoten)));
        if (Wasserarten.Startoption("fuellung") == "analyse") FuellungAnalyse();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Backen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Das Netz eines Teils in der Jahreszeit: im Winter die kahle Fassung, wo es eine gibt.</summary>
    private Mesh? FuellNetz(Fuellteil t, bool winter) =>
        winter && HatWinterfassung(t.Datei) && NaturNetz(t.Datei, t.Knoten + "_Winter", pflicht: false) is { } kahl ? kahl : NaturNetz(t.Datei, t.Knoten);

    /// <summary>
    /// Das Bildnetz einer Jahreszeit und Stufe als Arbeit in Schritten (jeder Schritt höchstens eine Fläche oder 150 Teile): erst sammeln, dann Fläche für Fläche backen, am Ende den Knoten anhängen.
    /// Stufe 1 lässt die Kleinstteile weg (<see cref="Fuellteil.Klein"/>), der Winter die Sommerteile.
    /// </summary>
    private IEnumerator<int> FuellSatzBacken(Fuellsatz satz, int jahr, int stufe, Node3D wurzel)
    {
        var winter = jahr == 1;
        var sammler = new Fuellnetz.Sammler();
        var ersatz = FuellRezepte()?.FernStoffe;
        var n = 0;
        foreach (var zelle in _fuellZellen) // erst alle Netze lesen, damit die Ziele der Ersatzstoffe bekannt sind
            foreach (var t in zelle.Teile)
            {
                if (FuellNetz(t, winter) is { } netz) _fuellBacker.Vorlesen(netz);
                if (++n % 150 == 0) yield return 0;
            }
        foreach (var zelle in _fuellZellen)
            foreach (var t in zelle.Teile)
            {
                if ((stufe == 1 && t.Klein) || (winter && t.Sommer)) continue;
                if (FuellNetz(t, winter) is { } netz) _fuellBacker.Hinzu(sammler, netz, t.Lage, ersatz);
                if (++n % 150 == 0) yield return 0;
            }
        var gebacken = new ArrayMesh();
        foreach (var gruppe in sammler.Reihenfolge)
        {
            _fuellBacker.FlaecheBacken(gebacken, gruppe);
            yield return 0;
        }
        if (!IsInstanceValid(wurzel) || gebacken.GetSurfaceCount() == 0) yield break;
        PfuetzenTauschen(gebacken);
        satz.Ecken = sammler.Ecken;
        satz.Flaechen = gebacken.GetSurfaceCount();
        satz.Knoten = new MeshInstance3D { Name = $"bild_{(winter ? "winter" : "sommer")}_{(stufe == 1 ? "fern" : "alle")}", Mesh = gebacken, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
        wurzel.AddChild(satz.Knoten);
        _fuellGebacken++;
        _fuellSichtbar = null; // neu zeigen
    }

    /// <summary>Backt das Bildnetz einer Zelle für eine Jahreszeit: alle Teile, Stoffe unverändert (nahe Zoomstufe).</summary>
    private void FuellBildBacken(Fuellzelle zelle, int jahr)
    {
        var winter = jahr == 1;
        var sammler = new Fuellnetz.Sammler();
        foreach (var t in zelle.Teile)
            if (!(winter && t.Sommer) && FuellNetz(t, winter) is { } netz) _fuellBacker.Hinzu(sammler, netz, t.Lage);
        if (_fuellBacker.Backen(sammler) is { } gebacken)
        {
            PfuetzenTauschen(gebacken);
            var knoten = new MeshInstance3D { Name = $"bild_{(winter ? "winter" : "sommer")}", Mesh = gebacken, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
            _fuellWurzel!.AddChild(knoten);
            zelle.Bild[jahr] = knoten;
        }
        zelle.BildGebacken[jahr] = true;
        _fuellGebacken++;
        _fuellSichtbar = null;
    }

    /// <summary>Backt das Schattennetz einer Zelle für eine Jahreszeit und Art (0 alle Schattenwerfer, 1 nur die großen).</summary>
    private void FuellSchattenBacken(Fuellzelle zelle, int jahr, int art)
    {
        var winter = jahr == 1;
        var sammler = new Fuellnetz.Sammler();
        foreach (var t in zelle.Teile)
            if (t.Schatten && (art == 0 || t.Gross) && !(winter && t.Sommer) && FuellNetz(t, winter) is { } netz) _fuellBacker.Hinzu(sammler, netz, t.Lage);
        var liste = new List<MeshInstance3D>(1);
        if (_fuellBacker.BackenSchatten(sammler, _fuellSchattenStoff) is { } gebacken)
        {
            var knoten = new MeshInstance3D
            {
                Name = $"schatten_{(art == 1 ? "gross" : "alle")}_{(winter ? "winter" : "sommer")}", Mesh = gebacken,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.ShadowsOnly, Visible = false,
            };
            _fuellWurzel!.AddChild(knoten);
            liste.Add(knoten);
        }
        zelle.Schatten[jahr, art] = liste;
        _fuellSichtbar = null;
    }

    /// <summary>
    /// Holt die Netze aller gebrauchten Knoten (und die kahlen Fassungen der Büsche) mit einmal Instanziieren je Baukasten – <see cref="NaturNetz"/> allein
    /// instanziiert den ganzen Baukasten für jeden einzelnen Knoten.
    /// </summary>
    private void FuellNetzeVorladen(IEnumerable<(string Datei, string Knoten)> gebraucht)
    {
        foreach (var gruppe in gebraucht.Distinct().GroupBy(g => g.Datei))
        {
            var namen = gruppe.Select(g => g.Knoten).ToList();
            if (HatWinterfassung(gruppe.Key)) namen.AddRange(namen.Select(n => n + "_Winter").ToList());
            namen = namen.Where(n => !_naturNetze.ContainsKey((gruppe.Key, n))).Distinct().ToList();
            if (namen.Count == 0 || Modell(gruppe.Key)?.Instantiate<Node3D>() is not { } kasten) continue;
            foreach (var n in namen)
            {
                var netz = (kasten.FindChild(n, true, false) as MeshInstance3D)?.Mesh;
                if (netz is not null) LaubMelden(netz);
                _naturNetze[(gruppe.Key, n)] = netz;
            }
            kasten.Free();
        }
    }

    /// <summary>Baukästen, deren Teile eine kahle bzw. verschneite Fassung <c>&lt;Name&gt;_Winter</c> haben (das Netz wird mit der Jahreszeit getauscht).</summary>
    private static bool HatWinterfassung(string datei) => datei is "natur/natur_buesche" or "natur/natur_baeume" || datei.StartsWith("props/fuellung_", StringComparison.Ordinal);

    // ═════════════════════════════════════════════════════════════════════════
    //  Jedes Bild
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Jedes Bild (aus <see cref="GrafikBild"/>): bei geänderter Natur-Dichte (nach einer kurzen Ruhe, der Regler schickt viele Werte) neu aufbauen; sonst prüfen, welche Zellen im
    /// (oder nahe am) Bild liegen, Fehlendes backen (höchstens <see cref="FuellBackZeit"/> Millisekunden) und die passenden Netze zeigen.
    /// </summary>
    private void FuellungNachZoom(double delta)
    {
        BrunnenNachZoom(); // Strahl des Marktbrunnens nur nah (Stadtansicht.Brunnen.cs)
        if (Wasserarten.Startoption("fuellung") == "vergleich") FuellungVergleich();
        if (_fuellKarte is null || _fuellZellen.Count == 0) return;
        if (_fuellDichte != Fenster.NaturDichte)
        {
            _fuellDichteSeit += delta;
            if (_fuellDichteSeit >= 0.5) FuellungAufbauen(_fuellKarte);
            return;
        }
        var innen = _raum is not null;
        var winter = _jahreszeit == Jahreszeit.Winter;
        var jahr = winter ? 1 : 0;
        var zoom = _kamera.Size;
        var stufe = zoom <= FuellFernAbZoom ? 0 : 1;
        var nah = !innen && zoom <= FuellNahBisZoom;
        var gross = !innen && !nah && zoom <= FuellGrossBisZoom;

        // Welche Zellen liegen im Bild? Rahmen: Radius der Zelle, Baumhöhe und etwas Vorlauf, in Bildpunkten
        var imBild = new List<Fuellzelle>();
        if (!innen)
        {
            var bild = _kamera.GetViewport().GetVisibleRect();
            var punkteJeMeter = bild.Size.Y / Math.Max(1f, zoom);
            var mitte = bild.GetCenter();
            imBild = _fuellZellen.Where(z => bild.Grow((z.Radius + 22f) * punkteJeMeter).HasPoint(_kamera.UnprojectPosition(z.Mitte)))
                                 .OrderBy(z => _kamera.UnprojectPosition(z.Mitte).DistanceSquaredTo(mitte)).ToList();
        }

        // Backen (höchstens FuellBackZeit ms je Bild): nah die Bildnetze der Zellen im Bild, sonst das Netz der ganzen Füllung; dazu die Schattennetze der Zellen im Bild
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        if (imBild.Count > 0)
        {
            if (!nah)
            {
                var satz = _fuellSaetze[jahr, stufe] ??= new Fuellsatz();
                if (!satz.Gestartet) { satz.Gestartet = true; satz.Arbeit = FuellSatzBacken(satz, jahr, stufe, _fuellWurzel!); }
                while (satz.Arbeit is not null && uhr.ElapsedMilliseconds < FuellBackZeit)
                    if (!satz.Arbeit.MoveNext()) satz.Arbeit = null;
            }
            var arten = nah ? new[] { 0 } : gross ? new[] { 1 } : Array.Empty<int>();
            foreach (var zelle in imBild)
            {
                if (uhr.ElapsedMilliseconds >= FuellBackZeit) break;
                if (nah && !zelle.BildGebacken[jahr]) FuellBildBacken(zelle, jahr);
                foreach (var art in arten)
                    if (zelle.Schatten[jahr, art] is null) { if (art == 1 && !zelle.HatGrosse) zelle.Schatten[jahr, art] = new(); else FuellSchattenBacken(zelle, jahr, art); }
            }
        }

        var zustand = (nah, gross, winter, stufe, imBild.Count > 0);
        if (_fuellSichtbar == zustand) return;
        _fuellSichtbar = zustand;

        // Zeigen. Nah: das Bildnetz jeder Zelle im Bild (noch nicht gebackene Jahreszeit: bis dahin die andere). Sonst das Netz der ganzen Füllung (fehlt das passende noch, ein anderes
        // gebackenes: lieber zu viel als nichts), solange eine Zelle im Bild liegt.
        MeshInstance3D? gewaehlt = null;
        if (!nah)
            foreach (var (j, s) in new[] { (jahr, stufe), (jahr, 1 - stufe), (1 - jahr, stufe), (1 - jahr, 1 - stufe) })
                if (_fuellSaetze[j, s]?.Knoten is { } k && IsInstanceValid(k)) { gewaehlt = k; break; }
        foreach (var satz in _fuellSaetze)
            if (satz?.Knoten is { } k && IsInstanceValid(k)) k.Visible = zustand.Item5 && k == gewaehlt;
        foreach (var zelle in _fuellZellen)
        {
            var bildJahr = zelle.Bild[jahr] is not null || zelle.BildGebacken[jahr] ? jahr : 1 - jahr;
            for (var j = 0; j < 2; j++)
                if (zelle.Bild[j] is { } bildKnoten && IsInstanceValid(bildKnoten)) bildKnoten.Visible = nah && j == bildJahr;
            for (var art = 0; art < 2; art++)
            {
                var gewollt = art == 0 ? nah : gross;
                var zeigen = zelle.Schatten[jahr, art] is not null ? jahr : 1 - jahr;
                for (var j = 0; j < 2; j++)
                    if (zelle.Schatten[j, art] is { } knoten)
                        foreach (var teil in knoten)
                            if (IsInstanceValid(teil)) teil.Visible = gewollt && j == zeigen;
            }
        }
    }

    /// <summary>Die Jahreszeit hat gewechselt: <see cref="FuellungNachZoom"/> zeigt die Fassung der neuen Jahreszeit (und backt sie nach).</summary>
    private void FuellungJahreszeit() => _fuellSichtbar = null;

    // ═════════════════════════════════════════════════════════════════════════
    //  Messen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Nur für <c>--fuellung=analyse</c>: backt alle vier Bildnetze (und prüft die Stoffe Eigenschaft für Eigenschaft) und meldet Zahlen.</summary>
    private void FuellungAnalyse()
    {
        _fuellBacker.Pruefen = true;
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var meldung = "";
        foreach (var (jahr, stufe) in new[] { (0, 0), (0, 1), (1, 0), (1, 1) })
        {
            var satz = _fuellSaetze[jahr, stufe] = new Fuellsatz();
            var arbeit = FuellSatzBacken(satz, jahr, stufe, _fuellWurzel!);
            var vor = uhr.ElapsedMilliseconds;
            while (arbeit.MoveNext()) { }
            meldung += $"{(jahr == 1 ? "Winter" : "Sommer")} {(stufe == 1 ? "fern" : "alle")}: {satz.Flaechen} Flächen, {satz.Ecken} Ecken, {uhr.ElapsedMilliseconds - vor} ms; ";
            if (satz.Knoten?.Mesh is ArrayMesh netz && jahr == 0)
                GD.Print($"[analyse] Stoffe {(stufe == 1 ? "fern" : "alle")}: " + string.Join(", ", Enumerable.Range(0, netz.GetSurfaceCount()).Select(i => $"{netz.SurfaceGetMaterial(i)?.ResourceName}:{netz.SurfaceGetArrayLen(i) / 1000}k").OrderBy(x => x, StringComparer.Ordinal)));
        }
        GD.Print($"[analyse] Zellen {_fuellZellen.Count}, {_fuellZellen.Sum(z => z.Teile.Count)} Teile, {_fuellFlaechen.Count} Flächen; {meldung}Stoffe {_fuellBacker.Stoffe}, Widersprüche beim Zusammenfassen {_fuellBacker.Widersprueche}");
        _fuellBacker.Pruefen = false;
        Array.Clear(_fuellSaetze);
        _fuellSichtbar = null;
        foreach (var kind in _fuellWurzel!.GetChildren()) kind.QueueFree();
    }

    private int _vglBild;
    private long _vglAn, _vglAus;
    private int _vglAnN, _vglAusN;
    private bool _vglAnZustand;

    /// <summary>
    /// Nur für <c>--fuellung=vergleich</c> (mit <c>--messung</c>): schaltet die Füllung alle drei Bilder um (an, aus, an, aus …) und liest die Zeichenaufrufe des vorigen Bildes; nach 200 Messungen
    /// (die ersten 120 Bilder zählen nicht: gebacken wird noch) meldet <c>[fuellvergleich]</c> den Unterschied der Mittelwerte – was die Füllung allein kostet. So dicht umzuschalten
    /// lässt das Anwachsen der Zeichenaufrufe mit der Zeit (die Passanten kommen) herausfallen.
    /// </summary>
    private void FuellungVergleich()
    {
        _vglBild++;
        if (_fuellWurzel is null || !IsInstanceValid(_fuellWurzel)) return;
        if (_vglBild > 120 && _vglBild % 3 == 2) // 3 Bilder je Zustand, gelesen am dritten: das gelesene Bild gehört auch bei einem Bild Verzögerung zum selben Zustand
        {
            var aufrufe = (int)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame); // das vorige Bild, mit dem Zustand von damals
            if (_vglAnZustand) { _vglAn += aufrufe; _vglAnN++; } else { _vglAus += aufrufe; _vglAusN++; }
            if (_vglAnN + _vglAusN >= 200)
            {
                var flaechen = _fuellSaetze.Cast<Fuellsatz?>().Where(s => s?.Knoten is { Visible: true }).Sum(s => s!.Flaechen)
                               + _fuellZellen.Sum(z => z.Bild.Where(k => k is { Visible: true }).Sum(k => k!.Mesh.GetSurfaceCount()));
                GD.Print($"[fuellvergleich] an {(double)_vglAn / _vglAnN:F1} aus {(double)_vglAus / _vglAusN:F1} | Füllung kostet {(double)_vglAn / _vglAnN - (double)_vglAus / _vglAusN:F0} | sichtbare Flächen {flaechen}, gebacken {_fuellGebacken}");
                (_vglAn, _vglAus, _vglAnN, _vglAusN) = (0, 0, 0, 0);
            }
        }
        _vglAnZustand = _vglBild / 3 % 2 == 0;
        _fuellWurzel.Visible = _vglAnZustand;
    }
}
