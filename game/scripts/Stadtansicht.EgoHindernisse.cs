using Godot;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// <b>Kleine Hindernisse der Ego-Perspektive</b> – ein Teil der <c>partial class Stadtansicht</c> (Nutzer, 10.11.2026). Neben den Gebäuden und Mauern
/// (<c>Stadtansicht.Ego.cs</c>) stoßen die Füße an Bäume, Büsche und große Steine, an die Zäune, Gatter, Holzstapel und Tröge der Restflächen, an Marktstände,
/// Bänke, Marktkreuz und Viehtrog sowie an Karren mit ihren Zugtieren. Gebaut wird alles erst beim Start der Ego-Perspektive aus dem, was die Ansicht ohnehin kennt
/// (Natur-MultiMeshes, Füllzellen, Marktkreise, Karrenmodelle), und beim Ende verworfen – die Spielkamera zahlt nichts.
/// <list type="bullet">
/// <item><b>Form</b>: Bäume, Büsche, Steine und Tiere sind Kreise (<see cref="EgoHindernis.Rund"/>; der Stamm ist dünner als die Krone), alles andere eine Kiste im Maß seines
/// Netzes. Ein Zaun ist so eine Kette von Kisten (jedes Feld ein Stück). Teile unter 35 cm Höhe (Beete, Pfützen, Gras) zählen nicht.</item>
/// <item><b>Gitter</b>: Die kleinen Hindernisse liegen in Zellen zu 8 m; gefragt wird nur nach den neun Zellen um die Figur, so bleibt es bei Tausenden Bäumen billig.
/// Größere als 6 m kommen in eine eigene Liste.</item>
/// <item><b>Bewegtes</b> (Karren samt Ochse oder Pferden) wird jedes Bild aus den Netzen der Knoten „Karren“ und „…_Rumpf“ neu gelesen (nur die in 40 m Nähe).
/// Passanten und Streuner sind keine Hindernisse (sonst Stau).</item>
/// <item>Gelesen wird neu, wenn sich die Natur-Dichte, die Füllung oder die Jahreszeit ändert, die Marktkreise wachsen nicht mehr.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Was aus einem Baukasten ein Hindernis wird: nichts, ein runder Stamm, ein Stein oder Tier (Kreis aus dem Netz) oder eine Kiste.</summary>
    private enum EgoFormArt { Keine, Baum, Busch, Stein, Kiste }

    /// <summary>Die Natur-MultiMeshes, die Hindernisse sind (Bäume, Steine, Stümpfe), mit ihrer Datei; gefüllt in <see cref="NaturAufbauen"/>.</summary>
    private readonly List<(MultiMeshInstance3D Teil, string Datei)> _egoNaturTeile = new();

    private const float EgoGitter = 8f, EgoGrossAb = 6f, EgoMindesthoehe = 0.35f, EgoBewegtReichweite = 40f;
    private readonly Dictionary<(int, int), List<EgoHindernis>> _egoGitter = new();
    private readonly List<EgoHindernis> _egoGross = new();
    private readonly List<EgoHindernis> _egoBewegte = new();
    private readonly List<EgoHindernis> _egoAuswahl = new();
    private int _egoKleineZahl;
    /// <summary>Startoption <c>--egokollision=0</c>: nur Gebäude und Mauern zählen, wie vor dem 10.11.2026 (für Vorher-Nachher-Bilder und -Messungen).</summary>
    private readonly bool _egoKleinAus = Wasserarten.Startoption("egokollision") == "0";
    private (Node? Fuellung, int Dichte, bool Winter) _egoKleinStand;

    private static EgoFormArt EgoNaturArt(string datei) => datei switch
    {
        "natur/natur_baeume" or "natur/natur_kit_baeume" => EgoFormArt.Baum,
        "natur/natur_steine" or "natur/natur_kit_steine" or "natur/natur_holz" => EgoFormArt.Stein,
        _ => EgoFormArt.Keine,
    };

    private static EgoFormArt EgoFuellArt(string datei, string knoten)
    {
        if (knoten.StartsWith("Huhn", StringComparison.Ordinal) || knoten.StartsWith("Waescheleine", StringComparison.Ordinal)) return EgoFormArt.Keine; // Hühner huschen, die Leine hängt hoch
        if (datei == "props/fuellung_tiere") return EgoFormArt.Stein;
        return datei switch
        {
            "natur/natur_baeume" or "natur/natur_kit_baeume" => EgoFormArt.Baum,
            "natur/natur_buesche" => EgoFormArt.Busch,
            "natur/natur_steine" or "natur/natur_kit_steine" or "natur/natur_holz" => EgoFormArt.Stein,
            "natur/natur_boden" or "natur/natur_kit_boden" => EgoFormArt.Keine,
            _ when datei.StartsWith("props/", StringComparison.Ordinal) => EgoFormArt.Kiste,
            _ => EgoFormArt.Keine,
        };
    }

    /// <summary>Die kleinen Hindernisse in der Nähe von p (die neun Gitterzellen und die großen); die Liste gilt bis zum nächsten Aufruf.</summary>
    private List<EgoHindernis> EgoKleine(Vector2 p)
    {
        _egoAuswahl.Clear();
        var (cx, cz) = (Mathf.FloorToInt(p.X / EgoGitter), Mathf.FloorToInt(p.Y / EgoGitter));
        for (var x = cx - 1; x <= cx + 1; x++)
            for (var z = cz - 1; z <= cz + 1; z++)
                if (_egoGitter.TryGetValue((x, z), out var liste)) _egoAuswahl.AddRange(liste);
        _egoAuswahl.AddRange(_egoGross);
        _egoAuswahl.AddRange(_egoBewegte);
        return _egoAuswahl;
    }

    private void EgoKleineLeeren()
    {
        _egoGitter.Clear();
        _egoGross.Clear();
        _egoBewegte.Clear();
        _egoAuswahl.Clear();
        _egoKarrenNetze.Clear();
        _egoKleineZahl = 0;
    }

    private void EgoKleinAblegen(EgoHindernis h)
    {
        _egoKleineZahl++;
        if (Mathf.Max(h.Hx, h.Hz) > EgoGrossAb) { _egoGross.Add(h); return; }
        var schluessel = (Mathf.FloorToInt(h.Mitte.X / EgoGitter), Mathf.FloorToInt(h.Mitte.Y / EgoGitter));
        if (!_egoGitter.TryGetValue(schluessel, out var liste)) _egoGitter[schluessel] = liste = new();
        liste.Add(h);
    }

    private void EgoRundAblegen(Vector3 ort, float radius, float oben) =>
        EgoKleinAblegen(new EgoHindernis(new Vector2(ort.X, ort.Z), Vector2.Right, Vector2.Down, radius, radius, ort.Y - 0.3f, ort.Y + oben, true));

    /// <summary>Liest alle kleinen Hindernisse neu: Natur, Füllung, Marktkreise.</summary>
    private void EgoKleineLesen(Vector2? um = null)
    {
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        EgoKleineLeeren();
        _egoKleinStand = (_fuellWurzel, Fenster.NaturDichte, _jahreszeit == Jahreszeit.Winter);
        if (_egoKleinAus) return;

        // Natur: je Kopie in einem MultiMesh (die sichtbaren, die Natur-Dichte blendet den Rest aus)
        _egoNaturTeile.RemoveAll(t => !IsInstanceValid(t.Teil));
        foreach (var (teil, datei) in _egoNaturTeile)
        {
            var vielfach = teil.Multimesh;
            if (vielfach?.Mesh is not { } netz) continue;
            var art = EgoNaturArt(datei);
            var aabb = netz.GetAabb();
            var anzahl = vielfach.VisibleInstanceCount < 0 ? vielfach.InstanceCount : Math.Min(vielfach.VisibleInstanceCount, vielfach.InstanceCount);
            var weltlage = teil.GlobalTransform;
            for (var i = 0; i < anzahl; i++) EgoFormAblegen(art, aabb, weltlage * vielfach.GetInstanceTransform(i));
        }
        // Füllung der Restflächen: Zäune, Gatter, Stapel, Tröge, Büsche, Tiere
        var winter = _jahreszeit == Jahreszeit.Winter;
        foreach (var zelle in _fuellZellen)
            foreach (var t in zelle.Teile)
            {
                if (winter && t.Sommer) continue;
                var art = EgoFuellArt(t.Datei, t.Knoten);
                if (art == EgoFormArt.Keine || NaturNetz(t.Datei, t.Knoten, pflicht: false) is not { } netz) continue;
                EgoFormAblegen(art, netz.GetAabb(), t.Lage);
            }
        EgoFelsenLesen();
        EgoBergeLesen();
        EgoStellenLesen();
        // Markt: Stände (drei Kreise je Tisch), Brunnenrand, Trog, Marktkreuz, Bänke – dieselben Kreise wie für die Passanten
        foreach (var (mitte, radius) in _marktHindernisse)
        {
            var fuss = FussHoehe(mitte.X, mitte.Z);
            EgoKleinAblegen(new EgoHindernis(new Vector2(mitte.X, mitte.Z), Vector2.Right, Vector2.Down, radius, radius, fuss - 0.3f, fuss + 2.5f, true));
        }
        if (Wasserarten.Startoption("egolog") is not null && (um ?? _ego?.Ort) is { } figur) // für Klicktests: die nächsten Hindernisse
            foreach (var h in _egoGitter.Values.SelectMany(l => l).OrderBy(h => h.Mitte.DistanceTo(figur)).Take(8))
                GD.Print($"[egolog] nah: {(h.Rund ? "Kreis" : "Kiste")} bei {h.Mitte.X:0.0},{h.Mitte.Y:0.0} Maß {h.Hx:0.00}x{h.Hz:0.00} Abstand {h.Mitte.DistanceTo(figur):0.0}");
        if (Wasserarten.Startoption("egolog") is not null) EgoProbeLog();
        GD.Print($"[ego] kleine Hindernisse gelesen: {_egoKleineZahl} in {uhr.ElapsedMilliseconds} ms (Natur {_egoNaturTeile.Count} Teile, Füllung {_fuellZellen.Count} Zellen)");
    }

    /// <summary>
    /// Die Felsstücke der Karte (Felspinsel, Felsen am Rand und in den Hängen) als Kisten im Maß ihres Netzes, auf 80 % verkleinert (der Fels ist rund, die Kiste
    /// eckig); Stücke unter 35 cm Höhe und der Schutt zählen nicht. Die Lage ist die der Anzeige (<see cref="FelsLage"/>), auch geneigt und gekippt.
    /// </summary>
    private void EgoFelsenLesen()
    {
        foreach (var (schluessel, bild) in _felsBilder)
        {
            if (FelsKasten(bild.Form) is not { } kasten) continue;
            var lage = _felsLagen.TryGetValue(schluessel, out var l) ? l : FelsLage(bild);
            if (kasten.Size.Y * lage.Basis.Y.Length() < EgoMindesthoehe) continue;
            EgoKleinAblegen(EgoHindernisVon(lage * new Transform3D(Basis.Identity, kasten.GetCenter()), kasten.Size / 2f * 0.8f));
        }
    }

    /// <summary>
    /// Die Felsberge der Karte (Bausteine der Art „berg“, <c>Berg_…</c>, z. B. der Felsberg hinter der Grube) als eine Kiste um das Netz „Berg“, auf 70 % der Grundfläche verkleinert
    /// (der Hang ist schräg, am Rand soll man nicht am Nichts hängen bleiben). Die fernen Kulissenberge am Kartenrand (<c>kulisse_*.glb</c>) bleiben ohne Kollision: Sie liegen Kilometer weit weg.
    /// </summary>
    private void EgoBergeLesen()
    {
        foreach (var wurzel in GetChildren().OfType<Node3D>().Where(n => n.Name.ToString().StartsWith("Berg_", StringComparison.Ordinal)))
            foreach (var teil in wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            {
                if (teil.Mesh is not { } netz || !teil.IsVisibleInTree()) continue;
                var kasten = netz.GetAabb();
                if (kasten.Size.Y < EgoMindesthoehe) continue;
                var halb = new Vector3(kasten.Size.X * 0.35f, kasten.Size.Y / 2f, kasten.Size.Z * 0.35f);
                EgoKleinAblegen(EgoHindernisVon(teil.GlobalTransform * new Transform3D(Basis.Identity, kasten.GetCenter()), halb));
            }
    }

    /// <summary>
    /// Die Arbeitsstellen im Umland (<c>Stelle_…</c>, <see cref="UmlandstellenBauen"/>): Der Zaun der Weide steht als Kette von Kisten da, das Tor vorn (2 m) bleibt offen,
    /// dazu Tränke und Weidestein. Maße aus <c>tools/modelle/umland_stellen.py</c> (Spielkoordinaten x, z = Blender x, −y; Zaun 14 × 10 m, Pfosten 12 cm).
    /// </summary>
    private void EgoStellenLesen()
    {
        foreach (var knoten in FindChildren("Stelle_*", nameof(MeshInstance3D), false, false).OfType<MeshInstance3D>())
        {
            if (!IsInstanceValid(knoten) || !knoten.HasMeta("stelle_art") || knoten.GetMeta("stelle_art").AsString() != "Weide") continue;
            var lage = knoten.GlobalTransform;
            foreach (var k in EgoZaun.Kisten(WeideZaunPunkte, 0.12f, geschlossen: true, WeideTor))
            {
                var welt = lage * new Vector3(k.MitteX, 0, k.MitteZ);
                var richtung = lage.Basis * new Vector3(k.AchseX, 0, k.AchseZ);
                var achse = new Vector2(richtung.X, richtung.Z).Normalized();
                var fuss = FussHoehe(welt.X, welt.Z);
                EgoKleinAblegen(new EgoHindernis(new Vector2(welt.X, welt.Z), achse, new Vector2(-achse.Y, achse.X), k.HalbLaenge, k.HalbDicke, fuss - 0.3f, fuss + 1.0f));
            }
            EgoKleinAblegen(EgoHindernisVon(lage * new Transform3D(Basis.Identity, new Vector3(4.6f, 0.25f, -2.6f)), new Vector3(1.1f, 0.25f, 0.35f))); // Tränke
            var stein = lage * new Vector3(-2f, 0f, -1.4f);
            EgoRundAblegen(new Vector3(stein.X, FussHoehe(stein.X, stein.Z), stein.Z), 0.5f, 0.5f); // Weidestein
        }
    }

    /// <summary>Für Klicktests (<c>--egolog=1</c>): Stößt die Figur an den Zaun der Weide, geht sie durch das Tor, und bremsen die Felsen? Je Weide fünf Proben, dazu fünf Felsen.</summary>
    private void EgoProbeLog()
    {
        bool Block(Vector3 w) => EgoHindernisBei(new Vector2(w.X, w.Z), FussHoehe(w.X, w.Z));
        foreach (var knoten in FindChildren("Stelle_*", nameof(MeshInstance3D), false, false).OfType<MeshInstance3D>())
        {
            if (!knoten.HasMeta("stelle_art") || knoten.GetMeta("stelle_art").AsString() != "Weide") continue;
            var l = knoten.GlobalTransform;
            GD.Print($"[egolog] Weide {knoten.Name} bei {l.Origin.X:0.0},{l.Origin.Z:0.0}: Tor {(Block(l * new Vector3(0, 0, 5)) ? "ZU" : "offen")}, " +
                     $"vor dem Tor {(Block(l * new Vector3(0, 0, 7)) ? "ZU" : "offen")}, Mitte {(Block(l * new Vector3(0, 0, 1.5f)) ? "ZU" : "offen")}, " +
                     $"Zaun links {(Block(l * new Vector3(-4, 0, 5)) ? "fest" : "DURCHLAESSIG")}, Zaun hinten {(Block(l * new Vector3(0, 0, -5)) ? "fest" : "DURCHLAESSIG")}, Zaun rechts {(Block(l * new Vector3(7, 0, 0)) ? "fest" : "DURCHLAESSIG")}");
        }
        foreach (var wurzel in GetChildren().OfType<Node3D>().Where(w => w.Name.ToString().StartsWith("Berg_", StringComparison.Ordinal)))
            foreach (var teil in wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().Where(t => t.Mesh is not null))
            {
                var kasten = teil.Mesh.GetAabb();
                var mitte = teil.GlobalTransform * kasten.GetCenter();
                var rand = teil.GlobalTransform * (kasten.GetCenter() + new Vector3(kasten.Size.X * 0.45f, 0, 0));
                GD.Print($"[egolog] Berg {wurzel.Name}/{teil.Name} bei {mitte.X:0.0},{mitte.Z:0.0} Kasten {kasten.Size.X:0}x{kasten.Size.Y:0}x{kasten.Size.Z:0}: Mitte {(Block(new Vector3(mitte.X, FussHoehe(mitte.X, mitte.Z), mitte.Z)) ? "fest" : "DURCHLAESSIG")}, Rand {(Block(new Vector3(rand.X, FussHoehe(rand.X, rand.Z), rand.Z)) ? "fest" : "frei")}");
            }
        var n = 0;
        foreach (var (schluessel, bild) in _felsBilder)
        {
            if (FelsKasten(bild.Form) is not { } kasten || n >= 5) continue;
            var lage = _felsLagen.TryGetValue(schluessel, out var l) ? l : FelsLage(bild);
            var mitte = lage * kasten.GetCenter();
            GD.Print($"[egolog] Fels {bild.Form} bei {mitte.X:0.0},{mitte.Z:0.0} Kasten {kasten.Size.X * lage.Basis.X.Length():0.0}x{kasten.Size.Y * lage.Basis.Y.Length():0.0}x{kasten.Size.Z * lage.Basis.Z.Length():0.0}: Mitte {(Block(new Vector3(mitte.X, lage.Origin.Y, mitte.Z)) ? "fest" : "DURCHLAESSIG")}");
            n++;
        }
        GD.Print($"[egolog] Felsstücke {_felsBilder.Count}, davon als Hindernis ab 35 cm Höhe gelesen");
    }

    /// <summary>Pfostenreihe des Zauns der Weide und ihr Tor (vorn, +z) – gleiche Zahlen wie im Baukasten.</summary>
    private static readonly (float X, float Z)[] WeideZaunPunkte = { (-7f, 5f), (-7f, -5f), (7f, -5f), (7f, 5f) };
    private static readonly (float X, float Z, float Breite)[] WeideTor = { (0f, 5f, 2f) };

    /// <summary>Aus einem Netzmaß und seiner Lage ein Hindernis: Kreis oder Kiste, zu niedrige Dinge nicht.</summary>
    private void EgoFormAblegen(EgoFormArt art, Aabb aabb, Transform3D lage)
    {
        var skala = lage.Basis.Y.Length();
        var hoehe = aabb.Size.Y * skala;
        if (hoehe < EgoMindesthoehe) return;
        switch (art)
        {
            case EgoFormArt.Baum:
                EgoRundAblegen(lage.Origin, Mathf.Clamp(0.045f * hoehe, 0.2f, 0.6f), hoehe); // der Stamm, nicht die Krone
                break;
            case EgoFormArt.Busch:
                EgoRundAblegen(lage.Origin, Mathf.Max(0.3f, 0.35f * Mathf.Min(aabb.Size.X, aabb.Size.Z) * skala), hoehe);
                break;
            case EgoFormArt.Stein:
                EgoRundAblegen(lage.Origin, Mathf.Max(0.2f, 0.4f * Mathf.Min(aabb.Size.X, aabb.Size.Z) * skala), hoehe);
                break;
            case EgoFormArt.Kiste:
                EgoKleinAblegen(EgoHindernisVon(lage * new Transform3D(Basis.Identity, aabb.GetCenter()), aabb.Size / 2f));
                break;
        }
    }

    /// <summary>Alle paar Sekunden: lesen, wenn sich Natur-Dichte, Füllung oder Jahreszeit geändert haben.</summary>
    private void EgoKleinePruefen()
    {
        var jetzt = (_fuellWurzel, Fenster.NaturDichte, _jahreszeit == Jahreszeit.Winter);
        if (jetzt != _egoKleinStand) EgoKleineLesen();
    }

    /// <summary>Die Netze „Karren“ und „…_Rumpf“ je Karren, beim ersten Bild der Ego-Perspektive gesucht (FindChildren jedes Bild kostete den größten Teil der Rechenzeit).</summary>
    private readonly Dictionary<KarrenAnsicht, MeshInstance3D[]> _egoKarrenNetze = new();

    /// <summary>Jedes Bild: Karren und ihre Zugtiere in der Nähe der Figur als Kisten (Netze „Karren“, „…_Rumpf“) in ihrer jetzigen Lage.</summary>
    private void EgoBewegteLesen(Vector3 figur)
    {
        _egoBewegte.Clear();
        if (_egoKleinAus) return;
        foreach (var karren in _karren.Values)
        {
            var wurzel = karren.Wurzel;
            if (!IsInstanceValid(wurzel) || !wurzel.IsVisibleInTree()) continue;
            if (new Vector2(wurzel.GlobalPosition.X - figur.X, wurzel.GlobalPosition.Z - figur.Z).LengthSquared() > EgoBewegtReichweite * EgoBewegtReichweite) continue;
            if (!_egoKarrenNetze.TryGetValue(karren, out var netze))
                _egoKarrenNetze[karren] = netze = wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
                    .Where(t => t.Mesh is not null && t.Name.ToString() is var name && (name == "Karren" || name.EndsWith("_Rumpf", StringComparison.Ordinal))).ToArray();
            foreach (var teil in netze)
            {
                if (!IsInstanceValid(teil) || !teil.Visible) continue;
                var aabb = teil.Mesh.GetAabb();
                _egoBewegte.Add(EgoHindernisVon(teil.GlobalTransform * new Transform3D(Basis.Identity, aabb.GetCenter()), aabb.Size / 2f));
            }
        }
    }
}
