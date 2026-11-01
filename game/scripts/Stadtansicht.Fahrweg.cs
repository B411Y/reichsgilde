using System.Diagnostics;
using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Der Fahrweg im Baumodus (Wunsch des Nutzers vom 01.11.2026) – Teil der <c>partial class Stadtansicht</c>.
///
/// Wählt der Spieler eine Werkstatt und steht der Umriss auf einer erlaubten (grünen) Stelle, zeigt die Stadt die Wege ihrer Karren zu allen
/// Marktplätzen, die sie erreichen (<see cref="Stadtwirtschaft.FahrwegeAmBauort"/>; der beste ist genau die Strecke, die der Kern nach dem Bau
/// rechnet):
///   • ein Band in der ganzen Breite des Weges (Breite aus der Karte, Zubringer in der Breite des Hofwegs), 20 cm über dem Boden, gefärbt nach der
///     aufgelaufenen Fahrzeit des Startkarrens (<see cref="Fahrzeitstufen"/>: grün, gelb, rot). Der beste Weg kräftig, die anderen schwächer und
///     erst ab der Stelle, an der sie sich vom besten trennen. Ein Schablonenpuffer (Stencil) sorgt dafür, dass jede Stelle des Bodens nur einmal
///     gefärbt wird – an Ecken, Kreuzungen und wo sich zwei Wege überlagern, wird nichts doppelt dunkel;
///   • am Ende jedes Weges ein Schild (<see cref="FahrwegSchilder"/>, gezeichnet von <see cref="FahrwegAnzeige"/>): Name des Platzes, Minuten hin und
///     zurück mit dem Startkarren und mit dem schnellsten Karren.
/// Dazu im Baumodus die <b>Ampeln</b> (<see cref="FahrwegAmpeln"/>): kleine Marken an freien Stellen der Bauzonen und auf Bauplätzen, gefärbt nach
/// der besten Fahrzeit von dort. Sie werden nach und nach gesucht (höchstens <see cref="AmpelBudgetMs"/> je Bild), damit das Bild nicht stockt.
///
/// Zwischengespeichert: Die Wege rechnet der Kern nur neu, wenn der eingerastete Umriss (2-m-Raster), die Werkstatt oder der Belag sich ändert;
/// die Ampeln, wenn Werkstatt, Grundstücke oder Belag sich ändern. Die Kosten stehen mit <c>--fahrwegzeit=1</c> im Protokoll.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Ein Schild am Ende eines Weges: wo, welcher Platz, Minuten hin und zurück mit dem Startkarren und dem schnellsten Karren.</summary>
    public sealed record FahrwegSchild(Vector3 Ort, string Markt, int Hin, int Zurueck, string Karren, int SchnellHin, int SchnellZurueck, string SchnellKarren, bool Bester);

    /// <summary>Eine Ampel an einem freien Bauort: wo und wie viele Minuten der Startkarren zum nächsten Platz fährt.</summary>
    public sealed record FahrwegAmpel(Vector3 Ort, int Minuten);

    /// <summary>Höhe des Bandes über dem Boden (Meter): über der Oberseite der Wege (7 cm) und ihren Kanten, ohne Flimmern.</summary>
    private const float FahrwegHoehe = 0.2f;

    /// <summary>So viele Millisekunden je Bild darf die Suche nach Ampeln höchstens kosten.</summary>
    private const double AmpelBudgetMs = 3;

    /// <summary>Die Ampeln stehen höchstens eine je Feld dieser Größe (Meter): die beste Fahrzeit der erlaubten Orte im Feld.</summary>
    private const double AmpelFeld = 12;

    private Node3D? _fahrwegKnoten;
    private Func<double, double, int, BauBefehl>? _fahrwegErzeuger;
    private Betriebsart? _fahrwegArt;
    private readonly StandardMaterial3D?[] _fahrwegStoffe = new StandardMaterial3D?[2]; // einmal je Rang, für alle Bänder
    private string _fahrwegSchluessel = "";
    private int _fahrwegBelag; // zählt Änderungen des Belags (die Fahrzeiten ändern sich, die Wege nicht)
    private List<FahrwegSchild> _fahrwegSchilder = new();
    private readonly List<FahrwegAmpel> _ampeln = new();
    private readonly Dictionary<(int, int), int> _ampelJeFeld = new();
    private IEnumerator<bool>? _ampelSuche;
    private int _ampelOrte, _ampelPruefungen;
    private (Betriebsart? Art, int Anzahl, long Summe, int Belag) _ampelSchluessel;
    private static readonly bool FahrwegZeit = OS.GetCmdlineUserArgs().Contains("--fahrwegzeit=1"); // Kosten ins Protokoll
    private double _ampelMsGesamt;

    /// <summary>Die Schilder der gerade gezeigten Wege (leer: kein Weg zu sehen). Der beste Weg zuerst.</summary>
    public IReadOnlyList<FahrwegSchild> FahrwegSchilder => _fahrwegSchilder;

    /// <summary>Die Ampeln der freien Bauorte (leer außerhalb des Baumodus und bei einem Wohnhaus).</summary>
    public IReadOnlyList<FahrwegAmpel> FahrwegAmpeln => _ampeln;

    /// <summary>Wo der Umriss gerade steht (Mitte, für die Ampeln: die unter ihm wird nicht gezeigt) – null ohne Umriss.</summary>
    public Vector3? FahrwegBauort { get; private set; }

    /// <summary>Wie weit die Kamera herausgezoomt ist (Größe des Bildausschnitts in Metern) – die Anzeige zeigt bei großem Abstand weniger Text.</summary>
    public float KameraZoom => _kamera.Size;

    /// <summary>Die Bildschirmstelle eines Weltpunkts (null: hinter der Kamera oder ohne Kamera).</summary>
    public Vector2? Bildpunkt(Vector3 welt)
    {
        var kamera = GetViewport().GetCamera3D();
        if (kamera is null || kamera.IsPositionBehind(welt)) return null;
        return kamera.UnprojectPosition(welt);
    }

    /// <summary>Jedes Bild (nach dem Umriss): Wege und Ampeln nachziehen, aber nur rechnen, wenn sich etwas geändert hat.</summary>
    private void FahrwegBild()
    {
        var w = _simulation?.Wirtschaft;
        if (!ReferenceEquals(_fahrwegErzeuger, _bauErzeuger)) // die Betriebsart nur einmal je gewähltem Gebäude erfragen
        {
            _fahrwegErzeuger = _bauErzeuger;
            _fahrwegArt = (_bauErzeuger?.Invoke(0, 0, 0) as WerkstattBauen)?.Art;
        }
        var art = w is { Bauen.IstAktiv: true } && _baumodus && !_wegeModus && _innen is null ? _fahrwegArt : null;
        AmpelnSuchen(w, art);
        // Steht die Maus über einer Tafel, hat der Umriss keinen Ort – dann bleibt der zuletzt gezeigte Weg stehen (sonst verschwände die Zeile
        // „Fahrt zum Markt“ der Bautafel samt Tooltip, sobald man auf sie zeigt; Befund der Prüfung)
        if (art is not null && BauAuftrag is null && GetViewport().GuiGetHoveredControl() is not null) return;
        var ort = art is not null && BauAuftrag is WerkstattBauen b && BauGrund is null ? b : null;
        FahrwegBauort = BauAuftrag is { } auftrag && art is not null ? new Vector3((float)auftrag.X, BodenHoehe((float)auftrag.X, (float)auftrag.Z), (float)auftrag.Z) : null;
        var schluessel = ort is null ? "" : $"{ort.Art}|{ort.X}|{ort.Z}|{ort.Drehung}|{_fahrwegBelag}";
        if (schluessel == _fahrwegSchluessel) return;
        _fahrwegSchluessel = schluessel;
        FahrwegLoeschen();
        if (ort is null || w is null) return;
        var uhr = Stopwatch.StartNew();
        var wege = w.FahrwegeAmBauort(ort);
        var rechnen = uhr.Elapsed.TotalMilliseconds;
        FahrwegZeichnen(w, ort, wege);
        if (FahrwegZeit)
            GD.Print($"[fahrweg] {wege.Count} Wege bei {Zahlenformat.Knapp(ort.X)} | {Zahlenformat.Knapp(ort.Z)}: Kern {rechnen:0.0} ms, Bild {uhr.Elapsed.TotalMilliseconds - rechnen:0.0} ms");
    }

    /// <summary>Der Belag hat sich geändert: Wege und Ampeln beim nächsten Bild neu (aus <see cref="BelagVeraltetMelden"/>).</summary>
    private void FahrwegBelagGeaendert() => _fahrwegBelag++;

    private void FahrwegLoeschen()
    {
        if (_fahrwegKnoten is not null) KnotenEntfernen(_fahrwegKnoten);
        _fahrwegKnoten = null;
        _fahrwegSchilder = new List<FahrwegSchild>();
    }

    /// <summary>Die schnellste Karrenart (die kürzeste Hinfahrt auf diesem Weg; bei Gleichstand die erste der Daten).</summary>
    private static (string Art, int Hin, int Zurueck) Schnellster(Stadtwirtschaft w, Fahrweg weg, Betriebsart art)
    {
        (string, int, int) best = ("", int.MaxValue, int.MaxValue);
        foreach (var (name, daten) in w.Daten.Logistik.Karren)
        {
            if (daten.Tempo <= 0) continue;
            var (hin, zurueck) = w.FahrzeitenAuf(weg, art, name);
            if (hin < best.Item2) best = (name, hin, zurueck);
        }
        return best;
    }

    private void FahrwegZeichnen(Stadtwirtschaft w, WerkstattBauen ort, IReadOnlyList<Fahrweg> wege)
    {
        if (wege.Count == 0 || _karte is not { } karte) return;
        _fahrwegKnoten = new Node3D { Name = "Fahrweg" };
        AddChild(_fahrwegKnoten);
        var start = w.Daten.Art(ort.Art).Startkarren;
        var schilder = new List<FahrwegSchild>();
        var bester = wege[0];
        foreach (var weg in wege)
        {
            var minuten = w.Zwischenzeiten(weg, start);
            if (minuten is null) continue;
            // ein anderer Weg erst ab der Stelle, an der er sich vom besten trennt (davor liegt er genau darauf)
            var ab = 0;
            if (!weg.Bester)
                while (ab + 1 < weg.Punkte.Count && ab + 1 < bester.Punkte.Count && weg.Punkte[ab + 1].Abstand(bester.Punkte[ab + 1]) < 0.05) ab++;
            var band = FahrwegBand(karte, weg, minuten, ab, weg.Bester ? 0.62f : 0.3f);
            if (band is not null)
            {
                band.MaterialOverride = _fahrwegStoffe[weg.Bester ? 0 : 1] ??= FahrwegStoff(weg.Bester ? 0 : 1);
                _fahrwegKnoten.AddChild(band);
            }
            var (hin, zurueck) = w.FahrzeitenAuf(weg, ort.Art, start);
            var (schnell, schnellHin, schnellZurueck) = Schnellster(w, weg, ort.Art);
            var ende = weg.Punkte[^1];
            var fuss = new Vector3((float)ende.X, (DeckHoehe((float)ende.X, (float)ende.Z) ?? FussHoehe((float)ende.X, (float)ende.Z)) + 2.5f, (float)ende.Z);
            schilder.Add(new FahrwegSchild(fuss, weg.Markt.Name is { Length: > 0 } name ? name : "Marktplatz", hin, zurueck, start,
                schnellHin, schnellZurueck, schnell, weg.Bester));
        }
        _fahrwegSchilder = schilder;
    }

    /// <summary>
    /// Der Stoff der Bänder: unbeleuchtet, durchscheinend, Farbe aus den Ecken. Der Schablonenpuffer lässt jede Stelle nur einmal färben (der beste
    /// Weg wird zuerst gezeichnet, <paramref name="rang"/> 0, und behält seine Stellen).
    /// </summary>
    private static StandardMaterial3D FahrwegStoff(int rang) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        VertexColorUseAsAlbedo = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        RenderPriority = rang,
        StencilMode = BaseMaterial3D.StencilModeEnum.Custom,
        StencilFlags = (int)(BaseMaterial3D.StencilFlagsEnum.Read | BaseMaterial3D.StencilFlagsEnum.Write),
        StencilCompare = BaseMaterial3D.StencilCompareEnum.NotEqual,
        StencilReference = 1,
    };

    /// <summary>Breite eines Stücks: der Weg der Karte, auf dem es liegt, sonst (Zubringer vom Hof) die Breite des Hofwegs.</summary>
    private static float StueckBreite(Stadtkarte karte, Fahrtstueck s) =>
        s.Weg >= 0 && s.Weg < karte.Wege.Count ? (float)karte.Wege[s.Weg].BreiteWirksam : HofwegBreite;

    /// <summary>
    /// Das Band eines Weges ab Punkt <paramref name="ab"/>: alle Meter eine Reihe von fünf Ecken quer über den Weg (Mitte kräftiger, Ränder weicher, ein
    /// wenig unregelmäßig wie mit dem Pinsel), jede auf dem Boden. Die Breite wechselt an den Punkten des Verlaufs (dort die breitere der beiden
    /// Seiten); an Knicken liegen die Ränder auf der Winkelhalbierenden wie bei <see cref="BandDreiecke"/>.
    /// </summary>
    private MeshInstance3D? FahrwegBand(Stadtkarte karte, Fahrweg weg, double[] minuten, int ab, float deckung)
    {
        var p = weg.Punkte;
        var linie = new List<(Vector2 Ort, float Halb, float Minute)>();
        for (var i = ab; i + 1 < p.Count; i++)
        {
            var (a, b) = (new Vector2((float)p[i].X, (float)p[i].Z), new Vector2((float)p[i + 1].X, (float)p[i + 1].Z));
            var halb = StueckBreite(karte, weg.Hin[i]) / 2;
            var laenge = a.DistanceTo(b);
            if (laenge < 0.01f) continue;
            var vorher = i > ab ? StueckBreite(karte, weg.Hin[i - 1]) / 2 : halb;
            var teile = Mathf.Max(1, Mathf.CeilToInt(laenge));
            for (var k = linie.Count == 0 ? 0 : 1; k <= teile; k++)
            {
                var t = (float)k / teile;
                // am Anfang und am Ende des Stücks die breitere der beiden Seiten (so deckt das Band auch die Einmündung)
                var h = k == 0 ? Mathf.Max(halb, vorher) : k == teile && i + 2 < p.Count ? Mathf.Max(halb, StueckBreite(karte, weg.Hin[i + 1]) / 2) : halb;
                linie.Add((a.Lerp(b, t), h, (float)Mathf.Lerp(minuten[i], minuten[i + 1], t)));
            }
        }
        if (linie.Count < 2) return null;

        float[] spalten = { -1f, -0.5f, 0f, 0.5f, 1f };
        float[] profil = { 0.55f, 0.9f, 1f, 0.9f, 0.55f };
        var ecken = new Vector3[linie.Count * spalten.Length];
        var farben = new Color[ecken.Length];
        for (var i = 0; i < linie.Count; i++)
        {
            var vor = i > 0 ? (linie[i].Ort - linie[i - 1].Ort).Normalized() : Vector2.Zero;
            var nach = i + 1 < linie.Count ? (linie[i + 1].Ort - linie[i].Ort).Normalized() : Vector2.Zero;
            var richtung = (vor + nach).Normalized();
            if (richtung == Vector2.Zero) richtung = nach != Vector2.Zero ? nach : vor != Vector2.Zero ? vor : Vector2.Right;
            var quer = new Vector2(-richtung.Y, richtung.X);
            var bezug = nach != Vector2.Zero ? nach : vor;
            var gehrung = Mathf.Min(3, 1 / Mathf.Max(0.33f, Mathf.Abs(quer.Dot(new Vector2(-bezug.Y, bezug.X)))));
            var farbe = Fahrzeitstufen.Farbe(linie[i].Minute);
            // ein wenig unregelmäßig wie mit dem Pinsel: die Deckung schwankt längs des Weges (fest nach der Stelle, kein Zufall)
            var pinsel = 0.85f + 0.15f * Mathf.Sin(linie[i].Ort.X * 0.9f + linie[i].Ort.Y * 1.3f);
            // Höhe wie bei den Karren: Brücken und Fähren tragen (FahrHoehe); liegt die Mitte auf einem Fundament (Pflaster eines Platzes), die ganze Reihe darauf
            var fundament = DeckHoehe(linie[i].Ort.X, linie[i].Ort.Y) ?? FussHoehe(linie[i].Ort.X, linie[i].Ort.Y);
            for (var s = 0; s < spalten.Length; s++)
            {
                var o = linie[i].Ort + quer * linie[i].Halb * gehrung * spalten[s];
                ecken[i * spalten.Length + s] = new Vector3(o.X, Mathf.Max(FahrHoehe(o.X, o.Y), fundament) + FahrwegHoehe, o.Y);
                farben[i * spalten.Length + s] = new Color(farbe, deckung * profil[s] * pinsel);
            }
        }
        var indizes = new List<int>();
        for (var i = 0; i + 1 < linie.Count; i++)
            for (var s = 0; s + 1 < spalten.Length; s++)
            {
                var (a, b, c, d) = (i * spalten.Length + s, i * spalten.Length + s + 1, (i + 1) * spalten.Length + s, (i + 1) * spalten.Length + s + 1);
                indizes.AddRange(new[] { a, c, b, b, c, d });
            }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = ecken;
        arrays[(int)Mesh.ArrayType.Color] = farben;
        arrays[(int)Mesh.ArrayType.Index] = indizes.ToArray();
        var netz = new ArrayMesh();
        netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return new MeshInstance3D { Name = weg.Markt.Name, Mesh = netz, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Ampeln an freien Bauorten
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sucht nach und nach die Ampeln für die gewählte Werkstatt (<see cref="AmpelSuche"/>, höchstens <see cref="AmpelBudgetMs"/> je Bild) und fängt neu an,
    /// wenn Werkstatt, Grundstücke oder Belag sich ändern. Ohne Werkstatt (null) verschwinden die Ampeln.
    /// </summary>
    private void AmpelnSuchen(Stadtwirtschaft? w, Betriebsart? art)
    {
        // Schlüssel ohne Text je Bild: Werkstatt, Zahl der Grundstücke, Summe der Nummern und Arten (ein Abriss macht ein Grundstück zum Bauplatz), Belag
        var stand = (Art: art, Anzahl: 0, Summe: 0L, Belag: _fahrwegBelag);
        if (art is not null && w is not null)
            foreach (var g in w.Bauen.Grundstuecke) { stand.Anzahl++; stand.Summe += g.Nummer * 7L + (int)g.Art; }
        if (stand != _ampelSchluessel)
        {
            _ampelSchluessel = stand;
            _ampeln.Clear();
            _ampelJeFeld.Clear();
            _ampelMsGesamt = 0;
            _ampelOrte = _ampelPruefungen = 0;
            _ampelSuche = art is not null && w is not null ? AmpelSuche(w, art.Value).GetEnumerator() : null;
        }
        if (_ampelSuche is null) return;
        var uhr = Stopwatch.StartNew();
        var fertig = false;
        while (uhr.Elapsed.TotalMilliseconds < AmpelBudgetMs)
            if (!_ampelSuche.MoveNext()) { fertig = true; break; }
        _ampelMsGesamt += uhr.Elapsed.TotalMilliseconds;
        if (!fertig) return;
        _ampelSuche = null;
        if (FahrwegZeit)
            GD.Print($"[fahrweg] Ampeln: {_ampeln.Count} Felder, {_ampelOrte} erlaubte Orte aus {_ampelPruefungen} Prüfungen, {_ampelMsGesamt:0} ms (verteilt, höchstens {AmpelBudgetMs:0} ms je Bild)");
    }

    /// <summary>
    /// Die Suche der Ampeln in kleinen Schritten (jedes <c>yield</c> ist eine Prüfung oder eine Fahrzeit). Gesucht wird wie in <see cref="Bauwesen.Bauorte"/>
    /// – erst die freien Bauplätze, dann die Rasterstellen der Bauzonen, in denen die Werkstatt stehen darf, je Stelle vier Drehungen –, aber nur dort,
    /// wo mindestens zwei Felder des Vorplatzes nah genug an einer Karrenstraße liegen (vorab aus einer Karte der Straßennähe, das spart die teure
    /// Prüfung des Platzes an den meisten Stellen). Je Feld von <see cref="AmpelFeld"/> m und je Drehung zählt der erste erlaubte Ort; die Ampel des
    /// Feldes zeigt die beste Fahrzeit des Startkarrens unter ihnen (Kern: <see cref="Stadtwirtschaft.FahrzeitAmBauort"/>).
    /// </summary>
    private IEnumerable<bool> AmpelSuche(Stadtwirtschaft w, Betriebsart art)
    {
        if (_karte is not { } karte || w.Bauen.Daten is not { } bd || w.Bauen.Mass(new WerkstattBauen(art, 0, 0, 0)) is not { } mass) yield break;
        var r = bd.Raster;
        var abstand = bd.StrassenAbstand;
        var start = w.Daten.Art(art).Startkarren;

        // Straßennähe: alle Rasterfelder, deren Mitte höchstens „strassenAbstand“ von einer Karrenstraße liegt (wie die Regel in Bauwesen.PlatzVerboten)
        var nah = new HashSet<(int, int)>();
        foreach (var weg in karte.Wege.Where(x => x.IstKarrenstrasse))
        {
            var v = weg.Verlauf;
            for (var k = 0; k + 1 < v.Count; k++)
            {
                var (ax, az, bx, bz) = (v[k][0], v[k][1], v[k + 1][0], v[k + 1][1]);
                var (dx, dz) = (bx - ax, bz - az);
                var l2 = dx * dx + dz * dz;
                for (var i = (int)Math.Floor((Math.Min(ax, bx) - abstand) / r); i <= (int)Math.Floor((Math.Max(ax, bx) + abstand) / r); i++)
                    for (var j = (int)Math.Floor((Math.Min(az, bz) - abstand) / r); j <= (int)Math.Floor((Math.Max(az, bz) + abstand) / r); j++)
                    {
                        var (px, pz) = (i * r + r / 2, j * r + r / 2);
                        var t = l2 > 0 ? Math.Clamp(((px - ax) * dx + (pz - az) * dz) / l2, 0, 1) : 0;
                        var (qx, qz) = (ax + dx * t - px, az + dz * t - pz);
                        if (qx * qx + qz * qz <= abstand * abstand) nah.Add((i, j));
                    }
            }
            yield return true;
        }
        bool AnDerStrasse(BauBefehl b) =>
            w.Bauen.Grundriss(b).Felder(r, nurVorplatz: true).Count(f => nah.Contains(((int)Math.Floor(f.X / r), (int)Math.Floor(f.Z / r)))) >= 2;

        var gesehen = new HashSet<((int, int) Feld, int Drehung)>();
        var geprueft = new HashSet<(double, double, int)>(); // überlappende Zonen: jede Stelle nur einmal
        var zonen = karte.Bauzonen.Where(z => mass.Lagen.Contains(z.Art)).ToList();
        var freiePlaetze = w.Bauen.Bauplaetze.Where(p => !Bauwesen.IstWohnhausplatz(p)).ToList();
        // wie in Bauwesen.Bauorte: Liegt ein Eckfeld weder in einer passenden Zone noch auf einem Bauplatz, lehnt PlatzVerboten die Stelle sicher ab
        bool Bebaubar(double fx, double fz) => freiePlaetze.Any(p => p.Grundriss.Enthaelt(fx, fz)) || zonen.Any(z => z.Enthaelt(fx, fz));
        bool EckenBebaubar(Grundriss g, WerkstattBauen b)
        {
            var (ax, az) = (g.HalbX - r / 2, g.HalbZ - r / 2);
            return Bebaubar(b.X - ax, b.Z - az) && Bebaubar(b.X + ax, b.Z - az) && Bebaubar(b.X - ax, b.Z + az) && Bebaubar(b.X + ax, b.Z + az);
        }
        var kandidaten = new List<WerkstattBauen>();
        foreach (var platz in w.Bauen.Bauplaetze.Where(p => !Bauwesen.IstWohnhausplatz(p)).OrderBy(p => p.Nummer))
            for (var d = 0; d < 4; d++)
                kandidaten.Add((WerkstattBauen)w.Bauen.Einrasten(new WerkstattBauen(art, platz.Grundriss.X, platz.Grundriss.Z, d)));
        foreach (var b in kandidaten)
            foreach (var schritt in Pruefen(b)) yield return schritt;
        foreach (var zone in zonen)
        {
            var (x0, x1) = (Math.Floor(zone.Flaeche.Min(e => e[0]) / r) * r, zone.Flaeche.Max(e => e[0]));
            var (z0, z1) = (Math.Floor(zone.Flaeche.Min(e => e[1]) / r) * r, zone.Flaeche.Max(e => e[1]));
            for (var ez = z0; ez < z1; ez += r)
            {
                for (var ex = x0; ex < x1; ex += r)
                    for (var d = 0; d < 4; d++)
                    {
                        var g = new Grundriss(ex, ez, mass.Breite, mass.Tiefe, d); // (ex, ez) ist die nordwestliche Ecke, wie in Bauwesen.Bauorte
                        var b = new WerkstattBauen(art, ex + g.HalbX, ez + g.HalbZ, d);
                        if (!zone.Enthaelt(b.X, b.Z) || !geprueft.Add((b.X, b.Z, d)) || !EckenBebaubar(g, b) || !AnDerStrasse(b)) continue;
                        foreach (var schritt in Pruefen(b)) yield return schritt;
                    }
                yield return true;
            }
        }

        // Ein Ort: schon ein erlaubter Ort dieser Drehung im Feld? Sonst prüfen und die Fahrzeit nehmen.
        IEnumerable<bool> Pruefen(WerkstattBauen b)
        {
            var feld = ((int)Math.Floor(b.X / AmpelFeld), (int)Math.Floor(b.Z / AmpelFeld));
            if (gesehen.Contains((feld, b.Drehung))) yield break;
            _ampelPruefungen++;
            var frei = w.Bauen.PlatzVerboten(b) is null;
            yield return true;
            if (!frei) yield break;
            gesehen.Add((feld, b.Drehung));
            _ampelOrte++;
            if (w.FahrzeitAmBauort(b, start) is not { } minuten) yield break;
            var ort = new Vector3((float)b.X, BodenHoehe((float)b.X, (float)b.Z) + 1f, (float)b.Z);
            if (!_ampelJeFeld.TryGetValue(feld, out var nr)) { _ampelJeFeld[feld] = _ampeln.Count; _ampeln.Add(new FahrwegAmpel(ort, minuten)); }
            else if (minuten < _ampeln[nr].Minuten) _ampeln[nr] = new FahrwegAmpel(ort, minuten);
            yield return true;
        }
    }
}
