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
        // Markt: Stände (drei Kreise je Tisch), Brunnenrand, Trog, Marktkreuz, Bänke – dieselben Kreise wie für die Passanten
        foreach (var (mitte, radius) in _marktHindernisse)
        {
            var fuss = FussHoehe(mitte.X, mitte.Z);
            EgoKleinAblegen(new EgoHindernis(new Vector2(mitte.X, mitte.Z), Vector2.Right, Vector2.Down, radius, radius, fuss - 0.3f, fuss + 2.5f, true));
        }
        if (Wasserarten.Startoption("egolog") is not null && (um ?? _ego?.Ort) is { } figur) // für Klicktests: die nächsten Hindernisse
            foreach (var h in _egoGitter.Values.SelectMany(l => l).OrderBy(h => h.Mitte.DistanceTo(figur)).Take(8))
                GD.Print($"[egolog] nah: {(h.Rund ? "Kreis" : "Kiste")} bei {h.Mitte.X:0.0},{h.Mitte.Y:0.0} Maß {h.Hx:0.00}x{h.Hz:0.00} Abstand {h.Mitte.DistanceTo(figur):0.0}");
        GD.Print($"[ego] kleine Hindernisse gelesen: {_egoKleineZahl} in {uhr.ElapsedMilliseconds} ms (Natur {_egoNaturTeile.Count} Teile, Füllung {_fuellZellen.Count} Zellen)");
    }

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
