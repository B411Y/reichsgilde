using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Ordnung;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Nachtlichter (PR #207, Teil der <c>partial class Stadtansicht</c>): nur Stimmung, ohne Wirkung auf das Spiel. Die Teile stehen in
/// <c>gebaeude/lichter.glb</c> (Handlaterne, Fackel, Wandlaterne, Feuerkorb, Ewiges Licht).
/// <list type="bullet">
/// <item><b>Feste Lichter</b> hängen an Ankern der Gebäude: Feuerkörbe an <c>Anker_Feuer_&lt;n&gt;</c> (Torburgen, Wachturm, Schilderhäuschen),
/// Wandlaternen an <c>Anker_Laterne</c> (Wirtshaus, Rathaus), das Ewige Licht an <c>Anker_EwigesLicht</c> (Dom, rot). Die Torfeuer sind
/// ein <see cref="MultiMeshInstance3D"/> je Mauer, die übrigen Lichter Kinder ihres Ankers – so folgen sie dem Gebäude.</item>
/// <item><b>Nachtwächter</b>: Die Wachleute auf Streife (<see cref="Wachwesen.AnwesenheitJetzt"/>, nur gelesen) gehen eine feste Runde durch das
/// Gassennetz – vom Wachhaus über den Markt zum Tor und zurück – und tragen nachts eine Handlaterne.</item>
/// <item><b>Passanten</b> tragen im Dunkeln eine Handlaterne, jeder dritte eine Fackel (<see cref="PassantLichtStellen"/>).</item>
/// <item><b>Leuchten</b>: <c>nachtlicht.gdshader</c> mit der Flacker-Technik des Fensterlichts (<c>licht_flackern.gdshaderinc</c>). Echte
/// <see cref="OmniLight3D"/> gibt es nur für die nächsten Lichter vor der Kamera (ohne Schatten; Zahl nach der Schattenqualität: Hoch 8,
/// Mittel 4, Niedrig keine); alle übrigen leuchten nur über ihre Emission.</item>
/// </list>
/// Wann Lichter brennen, sagt der gemeinsame Dämmerungswert der Lichtstimmung (<see cref="_daemmerung"/>, Stadtansicht.Licht.cs): Er folgt der Jahreszeit.
/// </summary>
public partial class Stadtansicht
{
    private enum Lichtart { Feuerkorb, Wandlaterne, EwigesLicht, Handlaterne, Fackel }

    private const string LichterDatei = "gebaeude/lichter";
    private const string NachtlichtShaderPfad = "res://assets/shader/nachtlicht.gdshader";

    /// <summary>Knoten in <c>lichter.glb</c> je Art.</summary>
    private static readonly (string Knoten, Lichtart Art)[] LichterTeile =
    {
        ("Feuerkorb", Lichtart.Feuerkorb), ("Wandlaterne", Lichtart.Wandlaterne), ("EwigesLicht", Lichtart.EwigesLicht),
        ("Handlaterne", Lichtart.Handlaterne), ("Fackel", Lichtart.Fackel),
    };

    /// <summary>Wo die Flamme sitzt, im Koordinatensystem des Teils (Meter): dort steht das echte Licht.</summary>
    private static Vector3 FlammenOrt(Lichtart art) => art switch
    {
        Lichtart.Feuerkorb => new Vector3(0, 2.35f, 0),
        Lichtart.Wandlaterne => new Vector3(0, -0.4f, -0.58f),
        Lichtart.EwigesLicht => new Vector3(0, -0.5f, 0),
        Lichtart.Handlaterne => new Vector3(0, -0.14f, 0),
        _ => new Vector3(0, 0.7f, 0),
    };

    /// <summary>Echtes Licht je Art: Farbe, Stärke und Reichweite (Meter).</summary>
    private static (Color Farbe, float Staerke, float Reichweite) PunktLicht(Lichtart art) => art switch
    {
        Lichtart.Feuerkorb => (new Color(1f, 0.5f, 0.18f), 3.2f, 10f),
        Lichtart.Wandlaterne => (new Color(1f, 0.58f, 0.25f), 2.0f, 7f),
        Lichtart.EwigesLicht => (new Color(1f, 0.12f, 0.06f), 0.9f, 4f),
        Lichtart.Fackel => (new Color(1f, 0.48f, 0.16f), 2.2f, 7.5f),
        _ => (new Color(1f, 0.6f, 0.28f), 1.5f, 6f),
    };

    private readonly Dictionary<Lichtart, (Mesh Netz, Transform3D Lage)> _lichtNetze = new();
    private ShaderMaterial? _flammenStoff, _laternenStoff, _ewigesStoff;
    private static Shader? _nachtlichtShader;
    private bool _lichterGeladen;

    /// <summary>0 = Lichter aus (Tag), 1 = alle brennen; dazwischen die Dämmerung. Der gestellte Wert wird nur bei merklicher Änderung weitergegeben.</summary>
    private float _lampenAn, _lampenGestellt = -1;

    // ═════════════════════════════════════════════════════════════════════════
    //  Teile und Materialien
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Lädt <c>lichter.glb</c> einmal: je Teil eine Kopie des Netzes, deren Flammen und Laternenscheiben das Nachtlicht-Material tragen.</summary>
    private void LichterLaden()
    {
        if (_lichterGeladen) return;
        _lichterGeladen = true;
        if (Modell(LichterDatei)?.Instantiate<Node3D>() is not { } kasten) return;
        foreach (var (name, art) in LichterTeile)
        {
            if (kasten.FindChild(name, true, false) is not MeshInstance3D teil || teil.Mesh is not { } original) continue;
            var netz = (ArrayMesh)original.Duplicate();
            for (var i = 0; i < netz.GetSurfaceCount(); i++)
            {
                if (netz.SurfaceGetMaterial(i) is not BaseMaterial3D stoff) continue;
                switch (stoff.ResourceName)
                {
                    case "Flamme": netz.SurfaceSetMaterial(i, _flammenStoff ??= NachtlichtStoff("Flamme", stoff)); break;
                    case "Laternenlicht":
                        netz.SurfaceSetMaterial(i, art == Lichtart.EwigesLicht ? _ewigesStoff ??= NachtlichtStoff("Ewig", stoff) : _laternenStoff ??= NachtlichtStoff("Laternenlicht", stoff));
                        break;
                    case "Farben": FarbenEinrichten(stoff); break;
                }
            }
            _lichtNetze[art] = (netz, teil.Transform);
        }
        kasten.Free();
    }

    /// <summary>
    /// Das Material einer Lichtart für die ganze Stadt: die Farbe der Datei bei Tag, nachts Emission in der Farbe des Lichts. Offenes Feuer
    /// (Flamme) etwas stärker, rötlicher und unruhiger als das Licht hinter dem Horn einer Laterne (etwa 1.800–2.000 K); das Ewige Licht rot und ruhig.
    /// </summary>
    private ShaderMaterial NachtlichtStoff(string art, BaseMaterial3D vorlage)
    {
        _nachtlichtShader ??= GD.Load<Shader>(NachtlichtShaderPfad);
        var stoff = new ShaderMaterial { Shader = _nachtlichtShader, ResourceName = vorlage.ResourceName };
        stoff.SetShaderParameter("farbe", vorlage.AlbedoColor);
        stoff.SetShaderParameter("rauheit", vorlage.Roughness);
        var (farbe, staerke, flackern, tempo) = art switch
        {
            "Flamme" => (new Color(1f, 0.42f, 0.1f), 4.2f, 0.55f, 1.5f),
            "Ewig" => (new Color(1f, 0.07f, 0.04f), 2.6f, 0.16f, 0.6f),
            _ => (new Color(1f, 0.52f, 0.16f), 3.0f, 0.32f, 1.0f),
        };
        stoff.SetShaderParameter("licht_farbe", farbe);
        stoff.SetShaderParameter("staerke", staerke);
        stoff.SetShaderParameter("flackern", flackern);
        stoff.SetShaderParameter("tempo", tempo);
        stoff.SetShaderParameter("kalt", art == "Flamme" ? 0.3f : 1f);
        return stoff;
    }

    /// <summary>Gibt den Materialien die Helligkeit der Tageszeit – nur bei merklicher Änderung. Das Ewige Licht brennt immer, bei Tag nur schwach.</summary>
    private void LichterStoffStellen()
    {
        if (Mathf.Abs(_lampenAn - _lampenGestellt) < 0.004f && (_lampenAn > 0) == (_lampenGestellt > 0)) return;
        _lampenGestellt = _lampenAn;
        _flammenStoff?.SetShaderParameter("helligkeit", _lampenAn);
        _laternenStoff?.SetShaderParameter("helligkeit", _lampenAn);
        _ewigesStoff?.SetShaderParameter("helligkeit", Mathf.Max(_lampenAn, 0.22f));
    }

    /// <summary>Ein Licht aus dem Baukasten als eigener Knoten; <paramref name="nummer"/> gibt ihm seinen eigenen Takt.</summary>
    private MeshInstance3D? LichtKnoten(Lichtart art, int nummer)
    {
        LichterLaden();
        if (!_lichtNetze.TryGetValue(art, out var teil)) return null;
        var knoten = new MeshInstance3D { Name = art.ToString(), Mesh = teil.Netz, Transform = teil.Lage, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        knoten.SetInstanceShaderParameter("phase", (Streuung(nummer, 207) & 0xFFFF) / 65535f);
        LichterStoffStellen();
        return knoten;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Feste Lichter
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Eine Stelle, an der ein Licht brennt: der Knoten des Lichts, wo die Flamme sitzt (in seinem Raum) und welche Art es ist.</summary>
    private sealed class Lichtquelle
    {
        public required Node3D Knoten { get; init; }
        public required Vector3 Versatz { get; init; }
        public required Lichtart Art { get; init; }
        public float Phase { get; init; }
    }

    private readonly List<Lichtquelle> _lichtquellen = new();

    private void LichtquelleMerken(Node3D knoten, Lichtart art) =>
        _lichtquellen.Add(new Lichtquelle { Knoten = knoten, Versatz = FlammenOrt(art), Art = art, Phase = _lichtquellen.Count * 1.7f });

    /// <summary>
    /// Hängt die Lichter an die Anker eines eben eingesetzten Modells: <c>Anker_Feuer_&lt;n&gt;</c> einen Feuerkorb, <c>Anker_Laterne</c> eine
    /// Wandlaterne, <c>Anker_EwigesLicht</c> das Ewige Licht. Die Lichter sind Kinder der Anker und gehen mit dem Modell.
    /// </summary>
    private void NachtlichterAnbringen(Node3D modell)
    {
        foreach (var anker in modell.FindChildren("Anker_*", nameof(Node3D), true, false).OfType<Node3D>())
        {
            var name = anker.Name.ToString();
            var art = name.StartsWith("Anker_Feuer_") ? Lichtart.Feuerkorb
                    : name == "Anker_Laterne" ? Lichtart.Wandlaterne
                    : name == "Anker_EwigesLicht" ? Lichtart.EwigesLicht
                    : (Lichtart?)null;
            if (art is not { } a || LichtKnoten(a, _lichtquellen.Count) is not { } licht) continue;
            anker.AddChild(licht);
            LichtquelleMerken(licht, a);
        }
    }

    /// <summary>Viele Feuerkörbe in einem Zeichenaufruf je Material (die Torburgen einer Mauer); je Korb eine leere Marke für das echte Licht.</summary>
    private void FeuerkoerbeVielfach(IReadOnlyList<Transform3D> lagen, Node3D eltern)
    {
        LichterLaden();
        if (lagen.Count == 0 || !_lichtNetze.TryGetValue(Lichtart.Feuerkorb, out var teil)) return;
        var vielfach = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = teil.Netz, InstanceCount = lagen.Count };
        for (var i = 0; i < lagen.Count; i++) vielfach.SetInstanceTransform(i, lagen[i] * teil.Lage);
        eltern.AddChild(new MultiMeshInstance3D { Name = "Torfeuer", Multimesh = vielfach, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        foreach (var lage in lagen)
        {
            var marke = new Node3D { Name = "Feuerstelle", Transform = lage };
            eltern.AddChild(marke);
            LichtquelleMerken(marke, Lichtart.Feuerkorb);
        }
        LichterStoffStellen();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Licht in der Hand (Passanten und Nachtwächter)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Eine Laterne oder Fackel in der rechten Hand einer Figur: ein <see cref="BoneAttachment3D"/> auf <c>Unterarm_R</c>, das dem Arm folgt.</summary>
    private sealed class Traglicht
    {
        public required BoneAttachment3D Anhang { get; init; }
        public bool An { get; set; }
    }

    /// <summary>
    /// Wo die Mitte der Hand im Knochenraum von <c>Unterarm_R</c> liegt (Meter vom Ellbogen, 0,72 · Unterarm + 0,06; siehe
    /// <c>docs/bereiche/darstellung.md</c>, „Nachtlichter: Modelle“). Orks und Oger nehmen das Maß des Menschen (Modell mit Größe).
    /// </summary>
    private static float HandAbstand(Volk volk) => volk switch
    {
        Volk.Zwerg => 0.276f, Volk.Elb => 0.3624f, Volk.Halbling => 0.2472f, _ => 0.3336f,
    };

    /// <summary>
    /// Gibt der Figur eine Laterne oder Fackel in die rechte Hand (zunächst aus). Die Knochenachse zeigt in der Ruhelage nach unten,
    /// darum bekommt das Licht eine halbe Drehung um die X-Achse – dann steht es aufrecht und folgt dem Armschwung. Null, wenn das Skelett fehlt.
    /// </summary>
    private Traglicht? TraglichtBauen(Figur figur, Volk volk, Lichtart art, int nummer)
    {
        var skelett = figur.Modell.FindChild("Skelett", true, false) as Skeleton3D
                      ?? figur.Modell.FindChildren("*", nameof(Skeleton3D), true, false).OfType<Skeleton3D>().FirstOrDefault();
        if (skelett is null || skelett.FindBone("Unterarm_R") < 0 || LichtKnoten(art, nummer) is not { } lampe) return null;
        var anhang = new BoneAttachment3D { Name = "Licht", Visible = false };
        skelett.AddChild(anhang);
        anhang.BoneName = "Unterarm_R";
        var halter = new Node3D { Position = new Vector3(0, HandAbstand(volk), 0), Basis = new Basis(Vector3.Right, Mathf.Pi) };
        anhang.AddChild(halter);
        halter.AddChild(lampe);
        _lichtquellen.Add(new Lichtquelle { Knoten = lampe, Versatz = FlammenOrt(art), Art = art, Phase = nummer * 0.37f });
        return new Traglicht { Anhang = anhang };
    }

    /// <summary>Schaltet das Licht in der Hand ein oder aus – nur beim Wechsel.</summary>
    private static void TraglichtSchalten(Traglicht licht, bool an)
    {
        if (licht.An == an) return;
        licht.An = an;
        licht.Anhang.Visible = an;
    }

    /// <summary>Ab wann Lichter in der Hand getragen werden (Anteil von <see cref="_lampenAn"/>).</summary>
    private const float TragenAb = 0.05f;

    /// <summary>
    /// Wer draußen im Dunkeln unterwegs ist, trägt ein Licht: etwa zwei von drei eine Handlaterne, jeder dritte eine Fackel (fest je Einwohner).
    /// Das Licht entsteht erst, wenn es gebraucht wird; bei Tag hängt es unsichtbar am Arm.
    /// </summary>
    private void PassantLichtStellen(Passant p, Einwohner e)
    {
        var soll = _lampenAn > TragenAb && p.Knoten.Visible && p.Figur is not null;
        if (!soll && p.Licht is null) return;
        if (soll && p.Licht is null)
        {
            var art = Streuung(e.Nummer, 313) % 3 == 0 ? Lichtart.Fackel : Lichtart.Handlaterne;
            p.Licht = TraglichtBauen(p.Figur!, e.Volk, art, e.Nummer);
            if (p.Licht is null) return;
        }
        TraglichtSchalten(p.Licht!, soll);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Nachtwächter
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Ein Wachmann auf Streife: Figur, Laterne und die Runde, die er geht.</summary>
    private sealed class Wachgang
    {
        public required Node3D Knoten { get; init; }
        public required Figur? Figur { get; init; }
        public Traglicht? Licht { get; set; }
        public Vector3[] Runde { get; init; } = Array.Empty<Vector3>();
        public float Laenge { get; init; }
        public int Stempel { get; set; }
        public bool Sichtbar { get; set; }
    }

    private readonly Dictionary<(string Haus, int Nummer), Wachgang> _wachgaenge = new();
    private readonly Dictionary<string, (Vector3[] Runde, float Laenge)?> _wachrunden = new();
    private readonly List<(Betrieb Haus, Wachstand Stand, int Reihe)> _streifen = new();
    private Node3D? _wachEbene;
    private long _wachAbfrage = -1;
    private int _wachStempel;

    /// <summary>Gehtempo der Nachtwächter in Metern je echter Sekunde (bei Tempo 1): gemächlich, sie schauen sich um.</summary>
    private const float WachTempo = 1.5f;

    /// <summary>Abstand (Meter) zwischen zwei Wachleuten desselben Hauses, wenn eine Schicht aufbricht.</summary>
    private const float WachAbstand = 18f;

    /// <summary>
    /// Die Runde eines Wachhauses, fest für alle Wachleute des Hauses: von der Tür über den Markt zu einem Tor der Stadt (je Haus ein anderes,
    /// ohne Tor der Dom) und auf demselben Weg zurück. Gemerkt je Haus; null, wenn das Haus keine Tür hat oder das Netz keinen Weg kennt.
    /// </summary>
    private (Vector3[] Runde, float Laenge)? WachRunde(Betrieb haus)
    {
        if (_wachrunden.TryGetValue(haus.Name, out var bekannt)) return bekannt;
        (Vector3[], float)? runde = null;
        if (TuerAnker(haus.Name) is { } tuer && _netz.Knoten.Count > 0)
        {
            var ziel = _tore.Count > 0 ? _tore[(int)(Streuung(haus.Name.Length, haus.Name.Sum(c => c)) % (uint)_tore.Count)].Ort : _domTuer ?? _markt;
            var hin = new List<Vector3> { tuer };
            foreach (var teil in new[] { _netz.Pfad(_netz.KnotenBei(tuer), _netz.KnotenBei(_markt)), _netz.Pfad(_netz.KnotenBei(_markt), _netz.KnotenBei(ziel)) })
                foreach (var punkt in teil)
                    if (hin[^1].DistanceTo(punkt) > 0.05f) hin.Add(punkt);
            if (hin.Count >= 2)
            {
                var ganz = hin.Concat(Enumerable.Reverse(hin).Skip(1)).ToArray();
                runde = (ganz, Gassennetz.Laenge(ganz));
            }
        }
        _wachrunden[haus.Name] = runde;
        return runde;
    }

    /// <summary>
    /// Jedes Bild: Die Wachleute auf Streife (Kern: <see cref="Wachwesen.AnwesenheitJetzt"/>, einmal je Spielminute abgefragt) gehen ihre Runde.
    /// Die Schicht wechselt alle zwei Stunden (gerade Stunde): Wer neu auf Streife ist, bricht am Wachhaus auf, der Platz in der Reihe
    /// verzögert ihn um <see cref="WachAbstand"/> Meter. Wo er auf der Runde steht, folgt nur aus der Spielzeit – bei Pause, Sprung oder Laden
    /// stimmt es also immer. Die Wachleute, die der Kern daheim hat, sind nicht zu sehen (Innenansicht des Wachhauses).
    /// </summary>
    private void NachtwaechterAktualisieren(Stadtwirtschaft w, Spielzeit zeit, double bruchteil, double delta)
    {
        if (!w.Wache.IstAktiv || _simulation is null) return;
        if (zeit.VergangeneMinuten != _wachAbfrage)
        {
            _wachAbfrage = zeit.VergangeneMinuten;
            _wachStempel++;
            _streifen.Clear();
            foreach (var haus in w.Betriebe.Where(w.Wache.IstWachbetrieb))
            {
                var reihe = 0;
                foreach (var stand in w.Wache.AnwesenheitJetzt(haus).Where(s => s.Ort == Wachort.Patrouille))
                    _streifen.Add((haus, stand, reihe++));
            }
        }
        var sekunden = _simulation.Takt.IstPausiert ? 0 : delta;
        var gezeigt = Fenster.PassantenMenge > 0 && _raum is null;
        var sekundenJeMinute = _simulation.Takt.RundenDauerSekunden / Spielzeit.MinutenProTag;
        var vergangen = zeit.UhrzeitInMinuten - zeit.UhrzeitInMinuten / 120 * 120 + bruchteil; // Minuten seit dem Wachwechsel
        var an = _lampenAn > TragenAb;

        if (gezeigt)
            foreach (var (haus, stand, reihe) in _streifen)
            {
                if (WachRunde(haus) is not { } runde) continue;
                var schluessel = (haus.Name, stand.Arbeiter.Nummer);
                if (!_wachgaenge.TryGetValue(schluessel, out var gang))
                    _wachgaenge[schluessel] = gang = WachgangBauen(haus, stand, runde);
                gang.Stempel = _wachStempel;
                var meter = (float)(vergangen * WachTempo * sekundenJeMinute) - reihe * WachAbstand;
                var steht = meter < 0 || runde.Laenge < 1;
                if (gang.Sichtbar == steht) gang.Knoten.Visible = gang.Sichtbar = !steht;
                if (steht) continue;
                var anteil = meter % runde.Laenge / runde.Laenge;
                var hier = Gassennetz.PunktAuf(runde.Runde, anteil);
                var weiter = Gassennetz.PunktAuf(runde.Runde, Mathf.Min(1, anteil + 0.5f / runde.Laenge));
                var richtung = weiter - hier;
                if (richtung.LengthSquared() > 1e-6f)
                {
                    richtung = richtung.Normalized();
                    gang.Knoten.Rotation = new Vector3(0, Mathf.Atan2(richtung.X, richtung.Z), 0);
                    hier += new Vector3(-richtung.Z, 0, richtung.X) * 0.7f; // Rechtsverkehr wie die Passanten
                }
                gang.Knoten.Position = anteil < 0.002f ? hier : AufFuss(hier);
                if (gang.Figur is { } figur)
                {
                    BewegungSetzen(figur, "Gehen", WachTempo * (float)_simulation.Takt.Tempo / (GehenTempo * figur.Beinfaktor));
                    FigurImBildBewegen(figur, gang.Knoten.Position, sekunden);
                }
                if (an && gang.Licht is null && gang.Figur is { } f)
                    gang.Licht = TraglichtBauen(f, Volk.Mensch, Lichtart.Handlaterne, 5000 + stand.Arbeiter.Nummer);
                if (gang.Licht is { } licht) TraglichtSchalten(licht, an);
            }

        foreach (var gang in _wachgaenge.Values)
            if (gang.Stempel != _wachStempel || !gezeigt)
            {
                if (gang.Sichtbar) gang.Knoten.Visible = gang.Sichtbar = false;
                if (gang.Licht is { } licht) TraglichtSchalten(licht, false);
            }
    }

    private Wachgang WachgangBauen(Betrieb haus, Wachstand stand, (Vector3[] Runde, float Laenge) runde)
    {
        _wachEbene ??= new Node3D { Name = "Nachtwaechter" };
        if (_wachEbene.GetParent() is null) AddChild(_wachEbene);
        var nummer = 5000 + stand.Arbeiter.Nummer;
        var knoten = new Node3D { Name = $"Wache_{haus.Name}_{stand.Arbeiter.Nummer}", Visible = false };
        _wachEbene.AddChild(knoten);
        return new Wachgang
        {
            Knoten = knoten, Runde = runde.Runde, Laenge = runde.Laenge,
            Figur = FigurEinsetzen(knoten, WacheAussehen(nummer, stand.Arbeiter.Arbeitskraft, false), nummer),
        };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Echte Lichter nahe der Kamera
    // ═════════════════════════════════════════════════════════════════════════

    private sealed class PoolLicht
    {
        public required OmniLight3D Licht { get; init; }
        public Lichtquelle? Quelle { get; set; }
        public float Stand { get; set; }
        public float Ziel { get; set; }
    }

    private readonly List<PoolLicht> _lichtPool = new();
    private readonly List<(float Abstand, Lichtquelle Quelle)> _lichtKandidaten = new();
    private double _lichtUhr;
    private int _lichtBilder;

    /// <summary>Weiter herausgezoomt als so viel (Kamerahöhe in Metern) bringen echte Lichter nichts mehr – dann leuchten alle nur über die Emission.</summary>
    private const float LichtMaxZoom = 90f;

    /// <summary>Wie viele echte Lichter es gibt: nach der Schattenqualität Hoch 8, Mittel 4, Niedrig keines.</summary>
    private static int LichterMaximal() => Fenster.AktuelleSchattenqualitaet switch { Schattenqualitaet.Hoch => 8, Schattenqualitaet.Mittel => 4, _ => 0 };

    /// <summary>Der Punkt auf dem Boden, auf den die Kamera schaut.</summary>
    private Vector3 BlickpunktAmBoden()
    {
        var lage = _kamera.GlobalTransform;
        var strahl = -lage.Basis.Z;
        return strahl.Y > -0.01f ? lage.Origin : lage.Origin + strahl * (-lage.Origin.Y / strahl.Y);
    }

    /// <summary>
    /// Jedes Bild: Die nächsten brennenden Lichter vor der Kamera bekommen ein echtes Licht (ohne Schatten). Ein Licht blendet weich ein und aus,
    /// wenn es dazukommt oder wegfällt, und flackert leicht (±12 %); ein Licht, das noch ausblendet, behält seinen Platz, bis es erloschen ist.
    /// </summary>
    private void LichterPoolStellen(double delta)
    {
        var maximal = LichterMaximal();
        var aktiv = maximal > 0 && _lampenAn > 0.02f && _raum is null && _innen is null && _kamera.Size <= LichtMaxZoom;
        if (!aktiv && _lichtPool.Count == 0) return;
        _lichtBilder++;
        while (_lichtPool.Count < maximal && aktiv)
        {
            var licht = new OmniLight3D { Name = "Nachtlicht", Visible = false, ShadowEnabled = false, OmniAttenuation = 1.4f };
            AddChild(licht);
            _lichtPool.Add(new PoolLicht { Licht = licht });
        }

        _lichtKandidaten.Clear();
        if (aktiv)
        {
            if (_lichtBilder % 120 == 0) _lichtquellen.RemoveAll(q => !IsInstanceValid(q.Knoten));
            var fokus = BlickpunktAmBoden();
            var reichweite = Mathf.Max(20f, _kamera.Size * 0.9f);
            foreach (var q in _lichtquellen)
            {
                if (!IsInstanceValid(q.Knoten) || !q.Knoten.IsVisibleInTree()) continue;
                var ort = q.Knoten.GlobalTransform * q.Versatz;
                var abstand = new Vector2(ort.X - fokus.X, ort.Z - fokus.Z).Length();
                if (abstand < reichweite) _lichtKandidaten.Add((abstand, q));
            }
            _lichtKandidaten.Sort((a, b) => a.Abstand.CompareTo(b.Abstand));
            if (_lichtKandidaten.Count > maximal) _lichtKandidaten.RemoveRange(maximal, _lichtKandidaten.Count - maximal);
        }

        foreach (var p in _lichtPool) p.Ziel = 0;
        foreach (var (_, q) in _lichtKandidaten)
            if (_lichtPool.FirstOrDefault(p => p.Quelle == q) is { } vorhanden) vorhanden.Ziel = 1;
        foreach (var (_, q) in _lichtKandidaten)
        {
            if (_lichtPool.Any(p => p.Quelle == q)) continue;
            if (_lichtPool.FirstOrDefault(p => p.Stand < 0.02f && p.Ziel == 0) is not { } frei) break;
            frei.Quelle = q;
            frei.Ziel = 1;
        }

        var zeit = Time.GetTicksMsec() / 1000.0;
        for (var i = 0; i < _lichtPool.Count; i++)
        {
            var p = _lichtPool[i];
            if (i >= maximal) p.Ziel = 0;
            p.Stand = Mathf.MoveToward(p.Stand, p.Ziel, (float)delta * 3f);
            if (p.Quelle is { } q && !IsInstanceValid(q.Knoten)) p.Quelle = null;
            if (p.Quelle is null || p.Stand < 0.01f)
            {
                if (p.Licht.Visible) p.Licht.Visible = false;
                if (p.Stand < 0.01f) p.Quelle = null;
                continue;
            }
            var (farbe, staerke, reichweite2) = PunktLicht(p.Quelle.Art);
            var t = zeit * (p.Quelle.Art == Lichtart.Feuerkorb || p.Quelle.Art == Lichtart.Fackel ? 1.6 : 1.0) + p.Quelle.Phase * 7;
            var flackern = 1 + 0.12 * (0.55 * Math.Sin(t * 9.1) + 0.3 * Math.Sin(t * 23.7 + 1.3) + 0.15 * Math.Sin(t * 41.0 + 2.1));
            var tag = p.Quelle.Art == Lichtart.EwigesLicht ? Mathf.Max(_lampenAn, 0.22f) : _lampenAn;
            p.Licht.GlobalPosition = p.Quelle.Knoten.GlobalTransform * p.Quelle.Versatz;
            p.Licht.LightColor = farbe;
            p.Licht.LightEnergy = staerke * p.Stand * tag * (float)flackern;
            p.Licht.OmniRange = reichweite2;
            if (!p.Licht.Visible) p.Licht.Visible = true;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Einstieg
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Jedes Bild aus <see cref="Aktualisieren"/>, nach dem Licht der Tageszeit: wie viele Lichter brennen, und die echten Lichter nahe der Kamera.</summary>
    private void NachtlichterAktualisieren(double delta)
    {
        _lampenAn = _daemmerung; // der gemeinsame Dämmerungswert aus Stadtansicht.Licht.cs: keine eigenen Zeiten
        if (_lichterGeladen) LichterStoffStellen();
        LichterPoolStellen(delta);
    }
}
