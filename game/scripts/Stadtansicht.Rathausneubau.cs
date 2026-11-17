using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das Rathaus (Neubau, seit Stufe 8 das einzige) – Teil der <c>partial class Stadtansicht</c>.
///
/// Außen steht <c>gebaeude/neubau/rathaus.glb</c> (Saalbau mit Arkade, Fachwerk-Obergeschoss, Turm mit Uhr und Glockenstube,
/// Anbau der Amtsstube), in Köln <c>koelner_rathaus.glb</c>. Innen liegen die Räume als eigene Modelle
/// <c>gebaeude/neubau/rathaus_&lt;raum&gt;_innen.glb</c> unter dem Knoten „InteriorRoot“ im Haus selbst – wie die Wohnstube der
/// Wohnhaus-Probe (Stadtansicht.Neubau.cs): Die Raumkamera steht im jeweiligen Raum auf seinem Anker <c>Blick_Auge</c>, durch die
/// Fenster sieht man die echte Stadt. Alle gebauten Räume hängen gleichzeitig unter „InteriorRoot“ (durch Treppenlöcher sieht man
/// den Nachbarraum); die Kamera wechselt nur den Raum.
///
/// <b>Wege.</b> Die Anker <c>Tuer_&lt;von&gt;_&lt;nach&gt;</c> (extras: ziel, breite, hoehe, achse) werden Klickflächen
/// „rathausneu:weg:tuer|&lt;ziel&gt;“, die Anker <c>Treppe_…</c> und <c>Leiter_…</c> (extras: ziel, groesse) „rathausneu:weg:treppe|&lt;ziel&gt;“.
/// Ein Klick wechselt den Raum mit Schwarzblende; bei Türen greift vorher die Hand die Klinke (Uebergangsblende, KlinkeFolge).
/// Das Ziel „stadt“ (Portal) führt hinaus wie die Haustür des Wohnhauses.
///
/// <b>Gegenstände.</b> Knoten „Gegenstand_&lt;Name&gt;“ werden wie im Wohnhaus anklickbar (Hover-Leuchten, Namensschild,
/// Klick öffnet die Tafel in Hauptszene.RaumAngeklickt).
///
/// <b>Köln.</b> Die Kölner Hülle (22 × 13 m) ist größer als der Neubau (16 × 10 m): Dieselben Räume stehen dort in ihren Maßen mittig
/// in der Hülle (Versatz aus den Hüllkisten, <see cref="RathausNeuVersatz"/>); dass die Fenster innen nicht zu außen passen, gilt bis
/// zum Umbau der Kölner Hülle.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Kennungen der Räume, die es im alten Rathaus nicht gab (die übrigen: RaumRat, RaumGericht, RaumAmt, RaumKerker).</summary>
    public const string RaumHalle = "rathaus:halle", RaumRathausturm = "rathaus:turm", RaumTurmspitze = "rathaus:spitze";

    /// <summary>Die sieben Räume: Dateiname des Modells, Kennung im Spiel, Titel (Raumleiste). Reihenfolge = Raumleiste.</summary>
    private static readonly (string Datei, string Kennung, string Titel)[] RathausNeuRaeume =
    {
        ("eingangshalle", RaumHalle, "Eingangshalle"), ("amtsstube", RaumAmt, "Amtsstube"), ("gerichtssaal", RaumGericht, "Gerichtssaal"),
        ("kerker", RaumKerker, "Kerker"), ("ratshalle", RaumRat, "Ratshalle"), ("turm_treppe", RaumRathausturm, "Turm-Treppe"), ("turm_spitze", RaumTurmspitze, "Turm-Spitze"),
    };

    /// <summary>Vorsatz der Klickflächen auf Türen und Treppen im neuen Rathaus.</summary>
    private const string RathausWeg = "rathausneu:weg:";

    private Node3D? _rathausNeuHuelle;      // Wurzel des Modells in der Stadt
    private Node3D? _rathausNeuInnen;       // „InteriorRoot“ mit allen gebauten Räumen
    private readonly Dictionary<string, Node3D> _rathausNeuRaum = new();   // Kennung → Wurzel des Raummodells

    private static string RathausRaumDatei(string raum) => $"{NeubauOrdner}rathaus_{raum}_innen";

    /// <summary>Gibt es das Modell dieses Raums schon? (Die Räume entstehen nacheinander.)</summary>
    private static bool RathausRaumGebaut(string raum) => ResourceLoader.Exists($"res://assets/modelle/{RathausRaumDatei(raum)}.glb");

    /// <summary>
    /// Aus <see cref="RathausBauen"/>: merkt die Hülle und legt die Räume der Raumleiste an (Eingangshalle zuerst, wer das Rathaus betritt,
    /// steht dort). <paramref name="fremdeHuelle"/>: die Hülle ist nicht der Neubau (Köln) – keine Farben des Trim-Materials, Räume mittig.
    /// </summary>
    private void RathausNeuVorbereiten(SonderbauAnsicht a, ModellInstanz? huelle, bool fremdeHuelle)
    {
        if (huelle is null) return;
        _rathausNeuHuelle = huelle.Wurzel;
        _rathausNeuFremdeHuelle = fremdeHuelle;
        // Fensterläden in Stadtrot, Putz hell: das Rathaus gehört der Stadt (keine Hausfarbe)
        if (!fremdeHuelle)
            foreach (var teil in huelle.Wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            {
                teil.SetInstanceShaderParameter("plaster_tint", NeubauPutz[0]);
                teil.SetInstanceShaderParameter("accent_tint", new Color(0.62f, 0.14f, 0.11f));
            }
        foreach (var (datei, kennung, titel) in RathausNeuRaeume)
            if (RathausRaumGebaut(datei)) a.Raeume.Add(new Raum(kennung, titel, Vector3.Zero, new Vector3(5, 4, 6)));
        _rathaus = a;               // schon hier: die Räume werden gleich angelegt, Figuren und Schilder brauchen ihre Anker
        RathausNeuInnenAnlegen();
    }

    /// <summary>Ein Anker in einem neuen Raum (null: den Raum gibt es noch nicht als Modell oder ohne diesen Anker).</summary>
    private Node3D? RathausNeuAnker(string raumKennung, string name) =>
        _rathausNeuRaum.TryGetValue(raumKennung, out var raum) && IsInstanceValid(raum) ? raum.FindChild(name, true, false) as Node3D : null;

    /// <summary>
    /// Ein Platz für eine Figur aus einem Anker des neuen Raums: Eltern „InteriorRoot“, Lage relativ zu <c>a.Mitte</c> (wie
    /// <see cref="PersonBauen"/> sie erwartet), Blick aus der Drehung des Ankers. Null, wenn es den Raum oder den Anker nicht gibt.
    /// </summary>
    private (Node3D Eltern, Arbeitsplatz Platz)? RathausNeuPlatz(SonderbauAnsicht a, string raumKennung, string anker, Bewegung bewegung, Werkzeug werkzeug, bool sitzt)
    {
        if (_rathausNeuInnen is not { } wurzel || RathausNeuAnker(raumKennung, anker) is not { } k) return null;
        var lage = wurzel.GlobalTransform.AffineInverse() * k.GlobalTransform;
        return (wurzel, new Arbeitsplatz(lage.Origin - a.Mitte, lage.Basis.GetEuler().Y, bewegung, werkzeug, sitzt));
    }

    /// <summary>Das Schild eines Raums (Verfahren, Kerker, Stand) am Anker <c>Schild_…</c> des neuen Raums; null ohne ihn.</summary>
    private Label3D? RathausNeuSchild(SonderbauAnsicht a, string raumKennung, string anker)
    {
        if (_rathausNeuInnen is not { } wurzel || RathausNeuAnker(raumKennung, anker) is not { } k) return null;
        var schild = RathausNeuText("", wurzel.ToLocal(k.GlobalPosition));
        _rathausNeuRaumschilder.Add(schild);
        return schild;
    }

    private readonly List<Label3D> _rathausNeuRaumschilder = new();   // die Raumschilder (Gericht, Kerker, Amtsstube), nicht die Namensschilder
    private readonly HashSet<string> _rathausNeuSchilderGeprueft = new();
    private readonly HashSet<string> _rathausNeuKameraGeprueft = new();
    private readonly HashSet<string> _rathausNeuFensterGeprueft = new();

    /// <summary>
    /// Prüfung bei jedem Raumwechsel (Nachprüfung der Schilder): Die Schilderlisten halten keine freigegebenen Knoten – die Schilder der Sitzung
    /// kommen und gehen. Räumt auf und schreibt ins Log, wie viele tote Einträge vorher darin standen (Gegenprobe) und wie viele danach (0).
    /// </summary>
    private void RathausNeuSchilderlistenPruefen()
    {
        int Tote() => _rathausNeuTexte.Count(l => !IsInstanceValid(l)) + _rathausNeuRaumschilder.Count(l => !IsInstanceValid(l))
                      + _rathausNeuNamensschilder.Count(l => !IsInstanceValid(l)) + _rathausNeuLeiter.Count(l => !IsInstanceValid(l));
        var vorher = Tote();
        _rathausNeuTexte.RemoveAll(l => !IsInstanceValid(l));
        _rathausNeuRaumschilder.RemoveAll(l => !IsInstanceValid(l));
        _rathausNeuNamensschilder.RemoveAll(l => !IsInstanceValid(l));
        _rathausNeuLeiter.RemoveAll(l => !IsInstanceValid(l));
        var nachher = Tote();
        GD.Print($"[rathausneu] Schilderlisten: {_rathausNeuTexte.Count} Schilder, tote Einträge vorher {vorher}, nachher {nachher} ({(nachher == 0 ? "ok" : "FEHLER")})");
    }

    /// <summary>
    /// Fensterblick-Probe (einmal je Raum ins Log, Zeile <c>[rathausneu] Fensterblick …</c>; Klicktest 4: In der Probestadt zeigte das Fenster der
    /// Amtsstube nur dunkles Glas): Von der Mitte jeder Scheibe (<c>Glas_…</c>) geht ein Strahl 150 m nach außen; verdeckt ist sie, wenn er näher als
    /// 1,5 m auf ein sichtbares Netz außerhalb des Hauses trifft (Hülle, Gelände). Gegenprobe „ohne Ausblenden“: dieselbe Frage mit den
    /// ausgeblendeten Eindringlingen.
    /// </summary>
    private void RathausNeuFensterblickPruefen(Node3D raum)
    {
        if (!_rathausNeuFensterGeprueft.Add(raum.Name) || !IsInstanceValid(raum) || _rathausNeuInnen is not { } wurzel) return;
        var glaeser = raum.FindChildren("Glas_*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().ToList();
        if (glaeser.Count == 0) { GD.Print($"[rathausneu] Fensterblick {raum.Name}: keine Fenster"); return; }
        var raumMitte = (wurzel.GlobalTransform * _rathausNeuGrenzen.GetValueOrDefault(_rathausNeuRaum.FirstOrDefault(r => r.Value == raum).Key)).GetCenter();
        var netze = FindChildren("*", nameof(GeometryInstance3D), true, false).OfType<GeometryInstance3D>()
            .Where(n => !wurzel.IsAncestorOf(n) && n is not (Label3D or Sprite3D or GpuParticles3D or CpuParticles3D) && n.GetParent() is not Camera3D
                        && !(_rathausNeuHuelle is { } h && h.IsAncestorOf(n) && n.Name.ToString().StartsWith("Scheiben")))   // das Außenglas ist im Haus aus
            .ToList();
        int scheiben = 0, frei = 0, zu = 0, zuOhne = 0;
        var naechste = new List<string>();
        var gruende = new List<string>();
        foreach (var glas in glaeser)
        {
            var e = RathausNeuEcken(glas);
            for (var i = 0; i + 2 < e.Length; i += 6)   // je Scheibe zwei Dreiecke
            {
                var mitte = (e[i] + e[i + 1] + e[i + 2]) / 3f;
                var n = (e[i + 1] - e[i]).Cross(e[i + 2] - e[i]).Normalized();
                if (n.Dot(mitte - raumMitte) < 0) n = -n;   // nach außen
                scheiben++;
                var von = mitte + n * 0.05f; var bis = mitte + n * 150f;
                float? naechster = null, naechsterOhne = null; string? wer = null;
                foreach (var netz in netze)
                {
                    var k = netz.GlobalTransform * netz.GetAabb();
                    if (!k.IntersectsSegment(von, bis)) continue;
                    var ecken = RathausNeuEcken(netz);
                    for (var j = 0; j + 2 < ecken.Length; j += 3)
                    {
                        var treffer = Geometry3D.SegmentIntersectsTriangle(von, bis, ecken[j], ecken[j + 1], ecken[j + 2]);
                        if (treffer.VariantType != Variant.Type.Vector3) continue;
                        var d = von.DistanceTo(treffer.AsVector3());
                        if (naechsterOhne is null || d < naechsterOhne) naechsterOhne = d;
                        if (netz.IsVisibleInTree() && (naechster is null || d < naechster)) { naechster = d; wer = netz.Name; }
                    }
                }
                if (naechster is < 1.5f) { zu++; gruende.Add($"{wer} {naechster:0.00} m"); } else frei++;
                naechste.Add(naechster is null ? "frei bis 150 m (Himmel)" : $"{wer} in {naechster:0.0} m");
                if (naechsterOhne is < 1.5f) zuOhne++;
            }
        }
        GD.Print($"[rathausneu] Fensterblick {raum.Name}: {scheiben} Scheiben, frei {frei}, verdeckt {zu} ({(zu == 0 ? "ok" : "FEHLER: " + string.Join(", ", gruende.Distinct()))}), ohne Ausblenden verdeckt {zuOhne}; erster Treffer je Scheibe: {string.Join("; ", naechste)}");
    }

    /// <summary>
    /// Prüfung (einmal je Raum ins Log, Zeile <c>[rathausneu] Kamera …</c>): Die Raumkamera steht in ihrem Raum mit Abstand zu den Wänden (Kiste der
    /// Raumhülle, mindestens 0,15 m) und in keinem fremden Netz, das noch zu sehen ist. Gegenprobe: dieselbe Frage ohne das Ausblenden der
    /// Eindringlinge (in Köln stand die Kamera in der Kölner Hülle).
    /// </summary>
    private void RathausNeuKameraPruefen(Node3D raum)
    {
        if (!_rathausNeuKameraGeprueft.Add(raum.Name) || !IsInstanceValid(raum) || _rathausNeuInnen is not { } wurzel) return;
        var kennung = _rathausNeuRaum.FirstOrDefault(r => r.Value == raum).Key;
        if (kennung is null || !_rathausNeuGrenzen.TryGetValue(kennung, out var kasten)) return;
        var kiste = wurzel.GlobalTransform * kasten;
        var augen = new List<(string Name, Vector3 Ort)>();
        if (raum.FindChild("Blick_Auge", true, false) is Node3D blick) augen.Add(("Raumkamera", blick.GlobalPosition));   // der Anker: die Kamera kann schon im nächsten Raum stehen
        if (_ratssaal?.Wurzel == raum)   // die Kameras der Sitzung in der Ratshalle
        {
            if (_ratssaal.Auge is { } sa) augen.Add(("Sitzung", sa.GlobalPosition));
            if (_ratssaal.RednerBlick is { } rb) augen.Add(("Redner", rb.GlobalPosition));
        }
        foreach (var (wer, auge) in augen)
        {
        var abstand = Mathf.Min(Mathf.Min(Mathf.Min(auge.X - kiste.Position.X, kiste.End.X - auge.X), Mathf.Min(auge.Z - kiste.Position.Z, kiste.End.Z - auge.Z)),
            Mathf.Min(auge.Y - kiste.Position.Y, kiste.End.Y - auge.Y));
        var sichtbar = new List<string>(); var ohne = 0;
        foreach (var netz in FindChildren("*", nameof(GeometryInstance3D), true, false).OfType<GeometryInstance3D>())
        {
            if (wurzel.IsAncestorOf(netz) || netz is Label3D or Sprite3D or GpuParticles3D or CpuParticles3D) continue;
            if (!_rathausNeuFremdeHuelle && _rathausNeuHuelle is { } h && h.IsAncestorOf(netz)) continue;
            var k = netz.GlobalTransform * netz.GetAabb();
            if (k.Size.X > 60 || k.Size.Z > 60 || !k.HasPoint(auge) || netz.GetParent() is Camera3D) continue;   // nicht das Viereck des Pixel-Looks an der Kamera
            ohne++;
            if (netz.IsVisibleInTree()) sichtbar.Add($"{netz.GetParent()?.Name}/{netz.Name} {k.Size.X:0}×{k.Size.Y:0}×{k.Size.Z:0} m");
        }
        GD.Print($"[rathausneu] Kamera {raum.Name} ({wer}): Abstand zur Raumkiste {abstand:0.00} m ({(abstand >= 0.10f ? "ok" : "ZU NAH")}), in fremder Geometrie {sichtbar.Count} ({(sichtbar.Count == 0 ? "ok" : "FEHLER: " + string.Join(", ", sichtbar))}), ohne Ausblenden {ohne}");
        }
    }
    private readonly List<Label3D> _rathausNeuNamensschilder = new();   // Namensschilder der Gegenstände (Hover)

    /// <summary>
    /// Prüfung (einmal je Raum ins Log): Rechtecke der Schilder im Bild der Raumkamera – je Namensschild eines Gegenstands gegen das Raumschild.
    /// „ohne Ausblenden“ zählt die Paare, die sich überdecken würden, wenn beide zugleich stünden (Stand bis Klicktest 3, Gegenprobe), „im Spiel“
    /// die Paare, die wirklich zugleich zu sehen sind (das Raumschild ist aus, solange ein Namensschild steht: 0). Größe geschätzt aus Schrift und Zeichenzahl.
    /// </summary>
    private void RathausNeuSchilderPruefen(Node3D raum)
    {
        if (!_rathausNeuSchilderGeprueft.Add(raum.Name) || !IsInstanceValid(raum)) return;
        var kamera = _raumKamera;
        var hoehe = GetViewport().GetVisibleRect().Size.Y;
        Rect2? Rechteck(Label3D l, string text)
        {
            if (kamera.IsPositionBehind(l.GlobalPosition) || text.Length == 0) return null;
            var px = l.FontSize * l.PixelSize * hoehe / (2f * Mathf.Tan(Mathf.DegToRad(kamera.Fov) / 2f));
            var zeilen = text.Split('\n');
            var groesse = new Vector2(zeilen.Max(z => z.Length) * 0.52f * px, zeilen.Length * 1.25f * px);
            var mitte = kamera.UnprojectPosition(l.GlobalPosition);
            return new Rect2(mitte - new Vector2(groesse.X / 2, l.VerticalAlignment == VerticalAlignment.Bottom ? groesse.Y : groesse.Y / 2), groesse);
        }
        var gruppe = _rathausNeuSchilder.FirstOrDefault(g => _rathausNeuRaum.GetValueOrDefault(g.Key) == raum).Value;
        if (gruppe is null) return;
        var raumschilder = _rathausNeuRaumschilder.Where(s => IsInstanceValid(s) && s.GetParent() == gruppe).ToList();
        var namen = _rathausNeuNamensschilder.Where(s => IsInstanceValid(s) && s.GetParent() == gruppe).ToList();
        int ohne = 0;
        foreach (var n in namen)
            foreach (var r in raumschilder)
                if (Rechteck(n, n.Text) is { } a && Rechteck(r, r.Text.Length > 0 ? r.Text : "Raum\nZeile\nZeile\nZeile") is { } b && a.Intersects(b)) ohne++;
        // Ständeleiter: stehen immer – keine Zeile darf das Raumschild oder eine andere Zeile überdecken
        var leiter = _rathausNeuLeiter.Where(s => IsInstanceValid(s) && s.GetParent() == gruppe).ToList();
        string Muster(Label3D s) => s.Text.Length > 0 ? s.Text : "» Freiherr · ab 100.000 Gulden «";
        int leiterUeber = 0;
        for (var i = 0; i < leiter.Count; i++)
        {
            foreach (var r in raumschilder)
                if (Rechteck(leiter[i], Muster(leiter[i])) is { } a && Rechteck(r, r.Text.Length > 0 ? r.Text : "Raum\nZeile\nZeile\nZeile") is { } b && a.Intersects(b)) { leiterUeber++; GD.Print($"[rathausneu] Ständeleiter: Zeile {i} {a} überdeckt Raumschild {b}"); }
            for (var j = i + 1; j < leiter.Count; j++)
                if (Rechteck(leiter[i], Muster(leiter[i])) is { } a && Rechteck(leiter[j], Muster(leiter[j])) is { } b && a.Intersects(b)) { leiterUeber++; GD.Print($"[rathausneu] Ständeleiter: Zeile {i} {a} überdeckt Zeile {j} {b}"); }
        }
        if (leiter.Count > 0) GD.Print($"[rathausneu] Ständeleiter {raum.Name}: {leiter.Count} Zeilen, Überdeckungen {leiterUeber}");
        GD.Print($"[rathausneu] Schilder {raum.Name}: {namen.Count} Namensschilder, {raumschilder.Count} Raumschild(er), Überdeckung ohne Ausblenden {ohne} Paare, im Spiel 0 (Raumschild aus, solange ein Namensschild steht)");
    }

    // Schilder im Haus: Die Schilder der Stadt (Beschriftung) sind für die Stadtkamera gemacht (0,04 m je Bildpunkt, wachsen mit dem Zoom)
    // und schienen hier riesig und – ohne Tiefentest – durch Wände und Decken aus allen Räumen zugleich. Im Haus darum kleine Schilder wie die
    // Namensschilder der Gegenstände (Wohnhaus-Probe), je Raum in einer eigenen Gruppe, die nur im gezeigten Raum sichtbar ist.
    private readonly Dictionary<string, Node3D> _rathausNeuSchilder = new();   // Raumkennung → Gruppe der Schilder
    private readonly Dictionary<string, Aabb> _rathausNeuGrenzen = new();      // Raumkennung → Hülle der Raummauern in „InteriorRoot“
    // Klicktest 2: In der kleinen Amtsstube steht das Schild 1,5 bis 2 m vor der Kamera, in der Halle 6 bis 8 m – bei fester Größe in der
    // Welt (0,115 m je Zeile) war es dort viermal so groß im Bild. Darum feste Größe im Bild (Label3D.FixedSize): etwa 20 Bildpunkte je Zeile
    // bei 900 Bildpunkten Höhe, in jedem Raum gleich.
    private const float SchildPixel = 0.0007f;
    private StandardMaterial3D? _rathausNeuGlanz;   // Hover-Leuchten der Gegenstände im Rathaus
    private Node3D? _rathausNeuGezeigt;               // der Raum, den die Kamera gerade zeigt (Lichter mit Schatten und Feuer nur dort)
    private readonly List<(Node3D Raum, OmniLight3D Licht)> _rathausNeuSchattenlichter = new();

    /// <summary>
    /// <c>--innenmessung</c>: Bildzeiten der ersten 240 Bilder nach dem Zeigen eines Raums ins Log (<c>[rathausneu] Raumansicht: …</c>) –
    /// erstes Bild, längstes, Summe bis zum ersten ruhigen Bild (unter 50 ms) und Mittel der letzten 60. Lange Einzelbilder am Anfang
    /// sprechen für Shader-Übersetzung, dauerhaft lange Bilder für die Last im Bild (Lichter, Schatten, Feuer).
    /// </summary>
    private async void RathausNeuBildzeitenMessen(string raum)
    {
        var zeiten = new List<double>();
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 240; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            zeiten.Add(uhr.Elapsed.TotalMilliseconds);
            uhr.Restart();
        }
        var ruhig = zeiten.FindIndex(z => z < 50);
        GD.Print($"[rathausneu] Raumansicht: {raum} erstes Bild {zeiten[0]:0} ms, längstes {zeiten.Max():0} ms, bis ruhig {(ruhig < 0 ? zeiten.Sum() : zeiten.Take(ruhig).Sum()):0} ms ({(ruhig < 0 ? "nie" : ruhig + " Bilder")}), "
            + $"Mittel zuletzt {zeiten.Skip(180).Average():0.0} ms, {Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame):0} Objekte, {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):0} Zeichenaufrufe");
    }

    /// <summary>Die Gruppe der Schilder des Raums, in dem <paramref name="lokal"/> (in „InteriorRoot“) liegt; ohne Treffer die nächste.</summary>
    private Node3D? RathausNeuSchildGruppe(Vector3 lokal)
    {
        string? beste = null; var abstand = float.MaxValue;
        foreach (var (kennung, kasten) in _rathausNeuGrenzen)
        {
            var d = kasten.HasPoint(lokal) ? 0 : kasten.GetCenter().DistanceTo(lokal);
            if (d < abstand) { abstand = d; beste = kennung; }
        }
        return beste is not null ? _rathausNeuSchilder.GetValueOrDefault(beste) : null;
    }

    /// <summary>Kleines, scharfes Schild im Haus (Lage in „InteriorRoot“), in der Gruppe seines Raums.</summary>
    private Label3D RathausNeuText(string text, Vector3 lokal, bool sichtbar = true)
    {
        var label = new Label3D
        {
            Text = text, Position = lokal, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, FixedSize = true,
            PixelSize = SchildPixel, FontSize = 48, OutlineSize = 12, LineSpacing = 2, Visible = sichtbar,
            Modulate = new Color(1f, 0.93f, 0.72f), OutlineModulate = new Color(0.10f, 0.07f, 0.05f),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic, RenderPriority = 10,
        };
        (RathausNeuSchildGruppe(lokal) ?? _rathausNeuInnen!).AddChild(label);
        _rathausNeuTexte.Add(label);
        return label;
    }

    // Klicktest 3: Im Pixel-Look wurden die Schilder mit der Welt grob gerastert und waren kaum lesbar. Wie die Beschriftungen der Stadt
    // (PixelNamen) stehen sie dann als Control in voller Auflösung über dem Pixelbild; die Welt selbst bleibt im Pixel-Look.
    private readonly List<Label3D> _rathausNeuTexte = new();
    private readonly List<Label> _rathausNeuTexteScharf = new();
    private CanvasLayer? _rathausNeuTextEbene;

    /// <summary>
    /// Aus <see cref="PixelBild"/>: <paramref name="kamera"/> ist die Raumkamera im Pixel-Look (sonst null). Dann werden die Schilder des Hauses
    /// nicht in der Welt gezeichnet (Sichtebene 0), sondern als scharfe Beschriftung an ihrer projizierten Stelle; Größe wie das Schild mit
    /// <c>FixedSize</c>: Schrift · Pixelgröße / (2 · tan(Öffnungswinkel/2)) · Bildhöhe.
    /// </summary>
    private void RathausNeuTexteScharf(Camera3D? kamera)
    {
        var zahl = 0;
        var hoehe = GetViewport().GetVisibleRect().Size.Y;
        _rathausNeuTexte.RemoveAll(l => !IsInstanceValid(l));   // die Schilder der Sitzung kommen und gehen
        foreach (var label in _rathausNeuTexte)
        {
            if (!IsInstanceValid(label)) continue;
            var ziel = kamera is not null ? 0u : 1u;
            if (label.Layers != ziel) label.Layers = ziel;
            if (kamera is null || !label.IsVisibleInTree() || label.Text.Length == 0 || kamera.IsPositionBehind(label.GlobalPosition)) continue;
            _rathausNeuTextEbene ??= new CanvasLayer { Name = "RathausSchilder", Layer = 0 };
            if (_rathausNeuTextEbene.GetParent() is null) AddChild(_rathausNeuTextEbene);
            if (zahl == _rathausNeuTexteScharf.Count)
            {
                var neu = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center };
                _rathausNeuTextEbene.AddChild(neu);
                _rathausNeuTexteScharf.Add(neu);
            }
            var l = _rathausNeuTexteScharf[zahl++];
            var massstab = label.PixelSize * hoehe / (2f * Mathf.Tan(Mathf.DegToRad(kamera.Fov) / 2f));
            l.Text = label.Text;
            l.Visible = true;
            l.AddThemeFontSizeOverride("font_size", Mathf.Max(8, Mathf.RoundToInt(label.FontSize * massstab)));
            l.AddThemeColorOverride("font_color", label.Modulate);
            l.AddThemeColorOverride("font_outline_color", label.OutlineModulate);
            l.AddThemeConstantOverride("outline_size", Mathf.Max(1, Mathf.RoundToInt(label.OutlineSize * massstab)));
            l.AddThemeConstantOverride("line_spacing", Mathf.RoundToInt(label.LineSpacing * massstab));
            if (label.Font is not null) l.AddThemeFontOverride("font", label.Font); else l.RemoveThemeFontOverride("font");
            l.Size = Vector2.Zero;
            var mass = l.GetCombinedMinimumSize();
            var x = label.HorizontalAlignment switch { HorizontalAlignment.Left => 0f, HorizontalAlignment.Right => mass.X, _ => mass.X / 2f };
            l.Position = (kamera.UnprojectPosition(label.GlobalPosition) - new Vector2(x, label.VerticalAlignment == VerticalAlignment.Bottom ? mass.Y : mass.Y / 2f)).Round();
        }
        for (var i = zahl; i < _rathausNeuTexteScharf.Count; i++) _rathausNeuTexteScharf[i].Visible = false;
    }

    /// <summary>Schild über einer Figur (Amt, Kläger …): im Haus klein in seiner Raumgruppe.</summary>
    private Label3D RathausBeschriftung(string text, Vector3 position) => RathausNeuText(text, position);

    // Echte Figuren statt der Kapseln der Raumansicht: im Haus über FigurEinsetzen wie die Ratssitzung (SitzungsFigur)
    private readonly List<Figur> _rathausNeuFiguren = new();
    private int _rathausNeuFigurNummer;

    /// <summary>
    /// Aus <see cref="PersonBauen"/>: Steht die Figur im neuen Rathaus, eine echte Figur (Modell mit Skelett, Clip Stehen, Sitzen oder Schreiben) an
    /// ihrem Platz; die Hülle <c>Arbeiterfigur</c> behält Körper (für Sichtbarkeit, Amtskette, Wegräumen) und Takt. Null: wie bisher die Kapsel.
    /// </summary>
    private Arbeiterfigur? RathausNeuFigur(InnenAnsicht a, Node3D eltern, Arbeitsplatz platz, Volk volk, Color gewand, Kopfputz kopf, float takt)
    {
        if (_rathausNeuInnen is null || eltern != _rathausNeuInnen) return null;
        var koerper = new Node3D { Position = a.Mitte + platz.Ort, Rotation = new Vector3(0, platz.Drehung, 0) };   // wie die Kapsel: Blick nach -Z
        eltern.AddChild(koerper);
        var halter = new Node3D { Rotation = new Vector3(0, Mathf.Pi, 0) };                                          // die Figur blickt mit +Z
        koerper.AddChild(halter);
        var nummer = 900 + _rathausNeuFigurNummer++;
        var haare = Haarfarben(volk);
        var aussehen = new FigurAussehen(volk, false, gewand, Beinkleidfarben[nummer % Beinkleidfarben.Length], HautVon(volk), haare[nummer % haare.Length],
            gewand.Darkened(0.35f), kopf switch { Kopfputz.Barett => Kopfbedeckung.Barett, Kopfputz.Helm => Kopfbedeckung.Eisenhut, _ => Kopfbedeckung.Keine });
        if (FigurEinsetzen(halter, aussehen, nummer) is not { } figur) { koerper.QueueFree(); return null; }
        if (platz.Sitzt) figur.Modell.Position = new Vector3(0, 0.44f + 0.04f - figur.Kniehoehe, 0);   // Sitzhöhe der Anker (wie SitzungsFigur)
        BewegungSetzen(figur, platz.Sitzt ? (platz.Art == Bewegung.Schreiben ? "Schreiben" : "Sitzen") : "Stehen", 0.9f + (nummer % 7) * 0.03f);
        _rathausNeuFiguren.Add(figur);
        GD.Print($"[rathausneu] Figur {volk} {(platz.Sitzt ? "sitzt" : "steht")} bei {(a.Mitte + platz.Ort).ToString("0.0")}");
        var arm = new Node3D();
        koerper.AddChild(arm);
        return new Arbeiterfigur(koerper, arm, takt, platz.Art, Vector3.Zero);
    }

    /// <summary>Die Standmarke auf der Ständetafel der neuen Amtsstube und die Höhen ihrer untersten und obersten Zeile; null ohne Amtsstube.</summary>
    private (Node3D Marke, float Unten, float Oben)? RathausNeuStandmarke() =>
        RathausNeuAnker(RaumAmt, "Standmarke") is { } m && RathausNeuAnker(RaumAmt, "Stand_Unten") is { } u && RathausNeuAnker(RaumAmt, "Stand_Oben") is { } o
            ? (m, u.Position.Y, o.Position.Y) : null;

    private readonly List<Label3D> _rathausNeuLeiter = new();   // die Zeilen der Ständeleiter (für die Prüfung der Schilder)

    private static readonly Color StandEigen = new(0.98f, 0.84f, 0.42f), StandAndere = new(1f, 0.93f, 0.72f);

    /// <summary>
    /// Die Ständeleiter auf der Ständetafel (Nutzer, Rathaus Stufe 10): je Stand ein Schild in der Höhe, auf der die Standmarke für ihn steht
    /// (zwischen <c>Stand_Unten</c> und <c>Stand_Oben</c>), mitten auf der Tafel. Leer ohne Amtsstube. Text und Hervorhebung setzt
    /// <see cref="RathausNeuStaendeleiterSetzen"/>; im Pixel-Look scharf wie die übrigen Schilder (<see cref="RathausNeuTexteScharf"/>).
    /// </summary>
    private List<Label3D> RathausNeuStaendeleiterAnlegen(int anzahl)
    {
        var liste = new List<Label3D>();
        if (anzahl == 0 || _rathausNeuInnen is not { } wurzel || RathausNeuStandmarke() is not ({ } marke, var unten, var oben) || marke.GetParent() is not Node3D traeger)
        {
            GD.Print($"[rathausneu] Ständeleiter: nicht angelegt (Stände {anzahl}, Amtsstube {(RathausNeuStandmarke() is null ? "ohne Standmarke" : "da")})");
            return liste;
        }
        var tafel = _rathausNeuRaum.GetValueOrDefault(RaumAmt)?.FindChild("Gegenstand_Staendetafel", true, false) as MeshInstance3D;
        var mitte = tafel is not null ? tafel.ToGlobal(tafel.GetAabb().GetCenter()) : marke.GlobalPosition;
        for (var i = 0; i < anzahl; i++)
        {
            var hoehe = traeger.ToGlobal(new Vector3(0, Mathf.Lerp(unten, oben, anzahl > 1 ? i / (float)(anzahl - 1) : 0), 0)).Y;
            var schild = RathausNeuText("", wurzel.ToLocal(new Vector3(mitte.X, hoehe, mitte.Z)));
            schild.FontSize = 40;
            liste.Add(schild);
            _rathausNeuLeiter.Add(schild);
        }
        return liste;
    }

    /// <summary>Text und Hervorhebung der Ständeleiter für den Stand <paramref name="eigenerStand"/> (Zeilen aus <see cref="Staendeleiter"/>).</summary>
    private static void RathausNeuStaendeleiterSetzen(List<Label3D> schilder, IReadOnlyList<StandDaten> staende, int eigenerStand, string waehrung)
    {
        var zeilen = Staendeleiter.Zeilen(staende, eigenerStand, waehrung);
        for (var i = 0; i < schilder.Count && i < zeilen.Count; i++)
        {
            if (schilder[i].Text != zeilen[i].Text) schilder[i].Text = zeilen[i].Text;
            schilder[i].Modulate = zeilen[i].Eigener ? StandEigen : StandAndere;
        }
    }

    /// <summary>Der gezeigte Raum liegt im neuen Rathaus und hat ein Modell: dessen Wurzel, sonst null.</summary>
    private Node3D? RathausNeuRaumKnoten()
    {
        if (_innen is null || _innen != _rathaus || _raum is null || _rathausNeuHuelle is null || !IsInstanceValid(_rathausNeuHuelle)) return null;
        if (RathausNeuRaeume.FirstOrDefault(r => r.Kennung == _raum.Kennung) is not { Datei: { } datei } || !RathausRaumGebaut(datei)) return null;
        RathausNeuInnenAnlegen();
        return _rathausNeuRaum.GetValueOrDefault(_raum.Kennung);
    }

    /// <summary>Legt „InteriorRoot“ einmal an: alle gebauten Räume, eingekleidet, mit Licht, Feuer, Wegen und Gegenständen.</summary>
    private void RathausNeuInnenAnlegen()
    {
        if (_rathausNeuInnen is not null && IsInstanceValid(_rathausNeuInnen)) return;
        if (_rathaus is not { } a || _rathausNeuHuelle is null) return;
        var wurzel = new Node3D { Name = "InteriorRoot", Visible = false, ProcessMode = ProcessModeEnum.Disabled };
        _rathausNeuHuelle.AddChild(wurzel);
        _rathausNeuInnen = wurzel;
        _rathausNeuRaum.Clear();
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        double laden = 0, einsetzen = 0, kleiden = 0;
        foreach (var (datei, kennung, _) in RathausNeuRaeume)
        {
            uhr.Restart();
            if (!RathausRaumGebaut(datei) || Modell(RathausRaumDatei(datei)) is not { } szene) continue;
            laden += uhr.Elapsed.TotalMilliseconds; uhr.Restart();
            var raum = szene.Instantiate<Node3D>();
            raum.Name = "Raum_" + datei;
            wurzel.AddChild(raum);
            einsetzen += uhr.Elapsed.TotalMilliseconds; uhr.Restart();
            NeubauEinkleiden(RathausRaumDatei(datei), raum);
            kleiden += uhr.Elapsed.TotalMilliseconds;
            _rathausNeuRaum[kennung] = raum;
            var gruppe = new Node3D { Name = "Schilder_" + datei, Visible = false };
            wurzel.AddChild(gruppe);
            _rathausNeuSchilder[kennung] = gruppe;
            if (raum.FindChild("Raum_*", true, false) is MeshInstance3D huelle)
                _rathausNeuGrenzen[kennung] = new Aabb(wurzel.ToLocal(huelle.ToGlobal(huelle.GetAabb().Position)), Vector3.Zero).Expand(wurzel.ToLocal(huelle.ToGlobal(huelle.GetAabb().End)));
        }
        if (_rathausNeuFremdeHuelle) wurzel.Position = RathausNeuVersatz(_rathausNeuHuelle, wurzel);   // Köln: Räume mittig in der Hülle
        uhr.Restart();
        foreach (var (_, kennung, _) in RathausNeuRaeume)
            if (_rathausNeuRaum.TryGetValue(kennung, out var raum)) RathausNeuBeleben(a, wurzel, raum);
        if (Uebergangsblende.Messen)
            GD.Print($"[rathausneu] Raumansicht: GLB laden {laden:0} ms, Einsetzen {einsetzen:0} ms, Einkleiden (Stoffe) {kleiden:0} ms, Beleben (Licht, Feuer, Wege, Gegenstände) {uhr.Elapsed.TotalMilliseconds:0} ms, {_rathausNeuSchattenlichter.Count} Lichter mit Schatten");
        // Figuren im Haus bewegen (die Kapseln der alten Räume bewegt BesucherBewegen; echte Figuren brauchen FigurBewegen)
        a.Raumzustaende.Add((_, _) =>
        {
            if (_neubauInnen != wurzel) return;
            var delta = GetProcessDeltaTime();
            _rathausNeuFiguren.RemoveAll(f => !IsInstanceValid(f.Modell));
            foreach (var f in _rathausNeuFiguren) FigurBewegen(f, delta);
        });
        GD.Print($"[rathausneu] InteriorRoot: {_rathausNeuRaum.Count} Räume ({string.Join(", ", _rathausNeuRaum.Keys)}), Klickflächen im Haus: {a.Raumklicks.Count(k => IsInstanceValid(k) && wurzel.IsAncestorOf(k))}");
        RathausNeuVorwaermen(wurzel);
    }

    /// <summary>
    /// Klicktest 3 (NAS): Das erste Bild im Haus brauchte 2,6 s, ruhig wurde es nach 7–8 s – die CPU-Arbeit liegt headless bei 73 ms, der Rest entsteht
    /// auf der Grafikkarte beim ersten Zeichnen der Räume (Pipelines für Innenstoffe, Schatten der Raumlichter, Feuer, Schilder). Darum zeichnet eine
    /// eigene kleine Kamera die Räume einmal kurz nach dem Laden (siehe unten) – die Übersetzung fällt dann in den Ladebildschirm statt ins Betreten.
    /// Die Dauer steht im Log (<c>[rathausneu] Raumansicht: Vorwärmen …</c>).
    /// </summary>
    private async void RathausNeuVorwaermen(Node3D wurzel)
    {
        // Klicktest 4 (NAS): Erstes Betreten kalt 10,6 s (Probestadt) bzw. 12,5 s (Köln), beim zweiten Mal schnell – Shader-Übersetzung. Das
        // Mitzeichnen mit der Stadtkamera half nur, wenn das Rathaus im Bild war. Jetzt sieht eine eigene kleine Kamera (SubViewport 256 × 144,
        // dieselbe Welt, hinter dem Ladebild) jeden Raum zwei Bilder lang von seinem Blickpunkt aus, mit allen Lichtern, Schatten und Schildern:
        // Die Pipelines der Innenstoffe, Schatten der Raumlichter und Schilder entstehen dabei. Nichts davon ist im Spielbild zu sehen.
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var bild = new SubViewport { Size = new Vector2I(256, 144), RenderTargetUpdateMode = SubViewport.UpdateMode.Always, Name = "RathausVorwaermen" };
        var kamera = new Camera3D { Fov = 70, Current = true };
        bild.AddChild(kamera);
        AddChild(bild);
        wurzel.Visible = true;
        foreach (var (_, licht) in _rathausNeuSchattenlichter) licht.Visible = true;
        foreach (var gruppe in _rathausNeuSchilder.Values) gruppe.Visible = true;
        var raeume = 0;
        foreach (var raum in _rathausNeuRaum.Values)
        {
            if (!IsInstanceValid(raum) || raum.FindChild("Blick_Auge", true, false) is not Node3D auge) continue;
            var ziel = raum.FindChild("Blick_Ziel", true, false) as Node3D;
            kamera.LookAtFromPosition(auge.GlobalPosition, ziel?.GlobalPosition ?? auge.GlobalPosition - auge.GlobalBasis.Z, Vector3.Up);
            for (var i = 0; i < 2; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            raeume++;
        }
        bild.QueueFree();
        if (!IsInstanceValid(wurzel) || _neubauInnen == wurzel) return;   // inzwischen betreten: RathausNeuZeigen hat alles gesetzt
        wurzel.Visible = false;
        foreach (var (_, licht) in _rathausNeuSchattenlichter) licht.Visible = false;
        foreach (var gruppe in _rathausNeuSchilder.Values) gruppe.Visible = false;
        GD.Print($"[rathausneu] Raumansicht: Vorwärmen nach dem Laden {uhr.Elapsed.TotalMilliseconds:0} ms ({raeume} Räume je zwei Bilder in eigener Kamera)");
    }

    /// <summary>Licht, Fackeln, Wege und Gegenstände eines Raums aus seinen Ankern (einmal je Raum).</summary>
    private void RathausNeuBeleben(SonderbauAnsicht a, Node3D wurzel, Node3D raum)
    {
        foreach (var knoten in raum.FindChildren("*", nameof(Node3D), true, false).OfType<Node3D>().ToList())
        {
            var name = knoten.Name.ToString();
            var lokal = wurzel.ToLocal(knoten.GlobalPosition);
            if (name.StartsWith("Licht_"))
            {
                var art = ExtraText(knoten, "art");
                OmniLight3D licht = art switch
                {
                    "Raum" => new OmniLight3D { LightColor = new Color(0.95f, 0.88f, 0.76f), LightEnergy = 1.2f, OmniRange = 9f, ShadowEnabled = false },
                    "Kerze" => new OmniLight3D { LightColor = new Color(1f, 0.8f, 0.52f), LightEnergy = 1.6f, OmniRange = 8f, ShadowEnabled = true },
                    "Kamin" => new OmniLight3D { LightColor = new Color(1.0f, 0.6f, 0.3f), LightEnergy = 2.6f, OmniRange = 7f, ShadowEnabled = true, OmniAttenuation = 1.1f },
                    _ => new OmniLight3D { LightColor = new Color(1.0f, 0.66f, 0.34f), LightEnergy = 2.2f, OmniRange = 6.5f, ShadowEnabled = true, OmniAttenuation = 1.1f },
                };
                knoten.AddChild(licht);
                // Lichter mit Schatten nur im gezeigten Raum (RathausNeuZeigen): sonst zeichnen 13 Schattenlichter aller sieben Räume zugleich
                if (licht.ShadowEnabled) { licht.Visible = false; _rathausNeuSchattenlichter.Add((raum, licht)); }
            }
            else if (name.StartsWith("Feuer_"))
            {
                // Feuer_<Art>_<n>: Fackeln und Kamin brennen, solange man im Haus ist; die Kerzen des Leuchters zur Sitzung
                var art = name.Split('_') is { Length: >= 2 } t ? t[1] : "Fackel";
                var (vorgabe, groesse) = art switch { "Kerze" => (FeuerVorgabe.Kerze, 0.22f), "Kamin" => (FeuerVorgabe.Kamin, 1.0f), _ => (FeuerVorgabe.Fackel, 0.8f) };
                // nur im gezeigten Raum (die Feuer der übrigen sechs Räume sieht man nicht)
                Func<bool> brennt = art == "Kerze" ? () => _neubauInnen == wurzel && _rathausNeuGezeigt == raum && _ratssitzung is not null
                    : () => _neubauInnen == wurzel && _rathausNeuGezeigt == raum;
                FlammeAnlegen(a, lokal - a.Mitte, vorgabe, groesse, brennt, wurzel, art: art == "Kamin" ? Raumkamin() : null); // Kamin der Ratshalle: ohne Rauch (er füllte das Bild), die Glut zeichnet das Modell
            }
            else if (name.StartsWith("Tuer_") || name.StartsWith("Treppe_") || name.StartsWith("Leiter_"))
            {
                var ziel = ExtraText(knoten, "ziel");
                if (string.IsNullOrEmpty(ziel)) continue;
                var tuer = name.StartsWith("Tuer_");
                Vector3 groesse;
                if (tuer)
                {
                    var b = ExtraZahlKomma(knoten, "breite", 1.2f);
                    var h = ExtraZahlKomma(knoten, "hoehe", 2.6f);
                    // Achse "x": die Wand steht quer zur X-Achse des Modells (dünn in x); "y": quer zur Y-Achse (im Spiel z)
                    groesse = ExtraText(knoten, "achse") == "x" ? new Vector3(0.3f, h, b) : new Vector3(b, h, 0.3f);
                }
                else
                {
                    var teile = (ExtraText(knoten, "groesse") ?? "1.2,2.4,2.4").Split(',').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    groesse = new Vector3(teile[0], teile[2], teile[1]);   // Blender x, y, z → Spiel x, z, y
                }
                var flaeche = Klickflaeche(RathausGegenstaende.WegKennung(tuer, ziel), lokal, groesse, wurzel);
                flaeche.Disabled = false;
                a.Raumklicks.Add(flaeche);
            }
        }
        // Gegenstände: wie im Wohnhaus (Hover-Leuchten und Namensschild), Klick öffnet die Tafel (Hauptszene.RaumAngeklickt)
        var gegenstaende = new List<(string Kennung, MeshInstance3D Teil, Label3D Schild)>();
        foreach (var teil in raum.FindChildren("Gegenstand_*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            var name = teil.Name.ToString()["Gegenstand_".Length..];
            var kasten = teil.GetAabb();
            var mitte = wurzel.ToLocal(teil.ToGlobal(kasten.GetCenter()));
            // Klickfläche mindestens 0,40 m je Richtung: flache Dinge wie das Ratsbuch sind sonst kaum zu treffen
            var groesse = new Vector3(Mathf.Max(kasten.Size.X + 0.12f, 0.40f), Mathf.Max(kasten.Size.Y + 0.12f, 0.40f), Mathf.Max(kasten.Size.Z + 0.12f, 0.40f));
            var flaeche = Klickflaeche(NeubauGegenstand + name, mitte, groesse, wurzel);
            flaeche.Disabled = false;
            a.Raumklicks.Add(flaeche);
            var schild = RathausNeuText(RathausGegenstandTitel.GetValueOrDefault(name, name), mitte + new Vector3(0, groesse.Y / 2 + 0.14f, 0), sichtbar: false);
            gegenstaende.Add((NeubauGegenstand + name, teil, schild));
            _rathausNeuNamensschilder.Add(schild);
        }
        _rathausNeuGlanz ??= new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            // schwach: mit 0,30 deckte das Leuchten dunkles Holz (Kanzleipult) zu einer einfarbig gelben Fläche zu (Klicktest 2)
            BlendMode = BaseMaterial3D.BlendModeEnum.Add, AlbedoColor = new Color(1f, 0.82f, 0.42f, 0.12f),
        };
        if (gegenstaende.Count == 0) return;
        a.Raumzustaende.Add((_, _) =>
        {
            if (!IsInstanceValid(wurzel) || _neubauInnen != wurzel) return;
            foreach (var (kennung, teil, schild) in gegenstaende)
            {
                var an = _unterDerMaus == kennung;
                if (schild.Visible == an) continue;
                schild.Visible = an;
                teil.MaterialOverlay = an ? _rathausNeuGlanz : null;
            }
            // Klicktest 3: Das Namensschild eines Gegenstands lag über dem Raumschild (Ständetafel über „Amtsstube“) – solange eines zu sehen
            // ist, ist das Raumschild dieses Raums aus
            var zeigt = gegenstaende.Any(g => g.Schild.Visible);
            foreach (var raumschild in _rathausNeuRaumschilder)
                if (IsInstanceValid(raumschild) && raumschild.GetParent() == gegenstaende[0].Schild.GetParent()) raumschild.Visible = !zeigt;
        });
    }

    /// <summary>Namensschilder der Gegenstände im Rathaus (was der Klick öffnet).</summary>
    private static readonly Dictionary<string, string> RathausGegenstandTitel = new()
    {
        ["Anschlagbrett"] = "Anschlagbrett · Rathaus und Stadtkasse",
        ["Ratsbuch"] = "Ratsbuch · Anträge", ["Aemtertafel"] = "Ämtertafel · Ämter und Ratsbaum",
        ["Richtertisch"] = "Richtertisch · Stadtgericht", ["Zellengitter"] = "Zellengitter · Stadtgericht",
        ["Kanzleipult"] = "Kanzleipult · Stand und Bürgerrecht", ["Staendetafel"] = "Ständetafel · Euer Stand", ["Stadtkasse"] = "Stadtkasse",
    };

    private bool _rathausNeuFremdeHuelle;   // Köln: die Räume stehen in der Kölner Hülle (Versatz)

    // Klicktest 3: In Köln ragte die Kölner Hülle in die Räume (Kamera „in der Wand“, Fenster auf Wand), in der Probestadt etwas der Stadt in den
    // Kerker (gestreckte Fläche vor dem Gitter). Was in die Räume ragt, ist im Haus aus – nur diese Netze, nicht die Stadt.
    private List<(GeometryInstance3D Netz, bool War)>? _rathausNeuEindringlinge;

    /// <summary>Die Hüllkisten der Räume in Weltkoordinaten (Raumhülle „Raum_…“ je Raum), 5 cm nach innen gezogen.</summary>
    private List<(string Raum, Aabb Kiste)> RathausNeuRaumkisten()
    {
        var liste = new List<(string, Aabb)>();
        if (_rathausNeuInnen is not { } wurzel) return liste;
        foreach (var (kennung, kasten) in _rathausNeuGrenzen)
            liste.Add((kennung, (wurzel.GlobalTransform * kasten).Grow(-0.05f)));
        return liste;
    }

    /// <summary>
    /// Netze außerhalb des Hauses, deren Hüllkiste eine Raumkiste schneidet: in Köln die Kölner Hülle, sonst was die Stadt in das Rathaus legt.
    /// Große Sammelnetze (über 60 m, Gelände) zählen nicht – sie liegen unter dem Boden und tragen den Blick aus dem Fenster.
    /// </summary>
    private List<(GeometryInstance3D Netz, string Raum)> RathausNeuEindringlingeSuchen()
    {
        var erg = new List<(GeometryInstance3D, string)>();
        if (_rathausNeuInnen is not { } wurzel || _rathausNeuHuelle is null) return erg;
        var kisten = RathausNeuRaumkisten();
        foreach (var netz in FindChildren("*", nameof(GeometryInstance3D), true, false).OfType<GeometryInstance3D>())
        {
            if (wurzel.IsAncestorOf(netz) || netz is Label3D or Sprite3D or GpuParticles3D or CpuParticles3D) continue;
            if (!_rathausNeuFremdeHuelle && _rathausNeuHuelle.IsAncestorOf(netz)) continue;    // der Neubau selbst ist die Hülle der Räume
            if (!netz.IsVisibleInTree()) continue;
            var kiste = netz.GlobalTransform * netz.GetAabb();
            if (kiste.Size.X > 60 || kiste.Size.Z > 60) continue;
            // Dünne Schichten (unter 0,3 m, z. B. Pflaster der Straße über dem Kerker) bleiben: von unten unsichtbar, durchs Fenster gebraucht
            foreach (var (raum, k) in kisten)
                if (k.Intersects(kiste) && k.Intersection(kiste).Size.Y >= 0.3f) { erg.Add((netz, raum)); break; }
        }
        return erg;
    }

    // Klicktest 4: In der Probestadt füllte eine gestreifte Fläche den Kerker – Gelände (große Netze über 60 m), das die erste Prüfung bewusst
    // ausließ, damit der Blick aus dem Fenster den Boden behält. Darum zweite Stufe je Raum: Große Netze, von denen eine Ecke in der Kiste des
    // gezeigten Raums liegt, sind aus, solange dieser Raum gezeigt wird (im Kerker das Gelände, in den Räumen mit Fenstern nur, wenn es wirklich hineinragt).
    private List<(GeometryInstance3D Netz, bool War)>? _rathausNeuRaumEindringlinge;
    private readonly Dictionary<GeometryInstance3D, Vector3[]> _rathausNeuEcken = new();

    /// <summary>Ecken eines Netzes in Weltkoordinaten (aus den Dreiecken, einmal je Netz gemerkt); leer, wenn es keine einfachen Dreiecke hat.</summary>
    private Vector3[] RathausNeuEcken(GeometryInstance3D netz)
    {
        if (_rathausNeuEcken.TryGetValue(netz, out var ecken)) return ecken;
        ecken = netz is MeshInstance3D { Mesh: { } m } ? m.GetFaces().Select(e => netz.GlobalTransform * e).ToArray() : Array.Empty<Vector3>();
        return _rathausNeuEcken[netz] = ecken;
    }

    /// <summary>Große Netze außerhalb des Hauses (über 60 m, Gelände), von denen eine Ecke in der Kiste dieses Raums liegt.</summary>
    private List<GeometryInstance3D> RathausNeuRaumEindringlingeSuchen(string kennung)
    {
        var erg = new List<GeometryInstance3D>();
        if (_rathausNeuInnen is not { } wurzel || RathausNeuRaumkisten().FirstOrDefault(k => k.Raum == kennung) is not { Raum: not null } eintrag) return erg;
        var kiste = eintrag.Kiste;
        foreach (var netz in FindChildren("*", nameof(GeometryInstance3D), true, false).OfType<GeometryInstance3D>())
        {
            if (wurzel.IsAncestorOf(netz) || netz is Label3D or Sprite3D or GpuParticles3D or CpuParticles3D || !netz.IsVisibleInTree()) continue;
            if (!_rathausNeuFremdeHuelle && _rathausNeuHuelle is { } h && h.IsAncestorOf(netz)) continue;
            var k = netz.GlobalTransform * netz.GetAabb();
            if ((k.Size.X <= 60 && k.Size.Z <= 60) || !k.Intersects(kiste)) continue;   // die kleinen prüft RathausNeuEindringlingeSuchen
            if (RathausNeuEcken(netz).Any(kiste.HasPoint)) erg.Add(netz);
        }
        return erg;
    }

    /// <summary>Die großen Eindringlinge des gezeigten Raums aus, die des vorigen wieder an; <paramref name="kennung"/> null: alle wieder an.</summary>
    private void RathausNeuRaumEindringlingeZeigen(string? kennung)
    {
        if (_rathausNeuRaumEindringlinge is { } alt)
            foreach (var (netz, war) in alt) if (IsInstanceValid(netz)) netz.Visible = war;
        _rathausNeuRaumEindringlinge = null;
        if (kennung is null) return;
        var gefunden = RathausNeuRaumEindringlingeSuchen(kennung);
        _rathausNeuRaumEindringlinge = gefunden.Select(n => (n, n.Visible)).ToList();
        foreach (var netz in gefunden)
        {
            netz.Visible = false;
            GD.Print($"[rathausneu] Gelände im Raum {kennung} ausgeblendet: {netz.GetPath()}");
        }
    }

    /// <summary>Im Haus: die Eindringlinge aus (beim Betreten gesucht und ins Log geschrieben); draußen wieder wie vorher.</summary>
    private void RathausNeuEindringlingeZeigen(bool draussen)
    {
        if (draussen)
        {
            RathausNeuRaumEindringlingeZeigen(null);
            _rathausNeuEcken.Clear();
            if (_rathausNeuEindringlinge is { } alt)
                foreach (var (netz, war) in alt) if (IsInstanceValid(netz)) netz.Visible = war;
            _rathausNeuEindringlinge = null;
            return;
        }
        if (_rathausNeuEindringlinge is not null) return;
        var gefunden = RathausNeuEindringlingeSuchen();
        _rathausNeuEindringlinge = gefunden.Select(e => (e.Netz, e.Netz.Visible)).ToList();
        foreach (var (netz, raum) in gefunden)
        {
            netz.Visible = false;
            GD.Print($"[rathausneu] Eindringling im Raum {raum} ausgeblendet: {netz.GetPath()}");
        }
        if (gefunden.Count == 0) GD.Print("[rathausneu] Eindringlinge: keine");
    }

    /// <summary>
    /// Versatz von „InteriorRoot“ in einer fremden Hülle (Köln): die Mitte der Raumhüllen (Grundriss) auf die Mitte der Hüllkiste des
    /// Außenmodells, in der Höhe unverändert (Böden wie im Neubau über dem Gelände). Keine Skalierung – die Räume behalten ihre Maße.
    /// </summary>
    private static Vector3 RathausNeuVersatz(Node3D huelle, Node3D wurzel)
    {
        Aabb? aussen = null, innen = null;
        var zurueck = huelle.GlobalTransform.AffineInverse();
        foreach (var teil in huelle.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            var kasten = zurueck * teil.GlobalTransform * teil.GetAabb();
            if (wurzel.IsAncestorOf(teil))
            {
                if (teil.Name.ToString().StartsWith("Raum_")) innen = innen?.Merge(kasten) ?? kasten;
            }
            else aussen = aussen?.Merge(kasten) ?? kasten;
        }
        if (aussen is not { } a || innen is not { } i) return Vector3.Zero;
        var d = a.GetCenter() - i.GetCenter();
        GD.Print($"[rathausneu] Räume in fremder Hülle: Hülle {a.Size.X:0.0} × {a.Size.Z:0.0} m, Räume {i.Size.X:0.0} × {i.Size.Z:0.0} m, Versatz ({d.X:0.00}, 0, {d.Z:0.00})");
        return new Vector3(d.X, 0, d.Z);
    }

    /// <summary>
    /// Aus <see cref="RatssaalModellBauen"/>: Im neuen Rathaus ist der Saal der Sitzung die Ratshalle im Haus. Die Stühle, der Redner, der
    /// Schreiber, die Zuhörerbank und die Kameras kommen aus ihren Ankern (<c>tools/modelle/neubau/rathaus_ratshalle.py</c>). Gibt false
    /// zurück, wenn es die Ratshalle nicht gibt (dann keine Sitzung im Bild).
    /// </summary>
    private bool RathausNeuRatssaal(SonderbauAnsicht a)
    {
        if (_rathausNeuHuelle is null) return false;
        RathausNeuInnenAnlegen();
        if (!_rathausNeuRaum.TryGetValue(RaumRat, out var halle)) return false;
        var m = new Ratssaalmodell { Wurzel = halle };
        Stuhl? buergermeister = null;
        var sitze = new List<(int Platz, Stuhl Stuhl)>();
        var bank = new List<(int Nummer, Stuhl Stuhl)>();
        foreach (var knoten in halle.FindChildren("*", nameof(Node3D), true, false).OfType<Node3D>())
        {
            var name = knoten.Name.ToString();
            var hoehe = ExtraZahlKomma(knoten, "sitzhoehe", 0.44f);
            if (name == "Anker_Buergermeister") buergermeister = new Stuhl(knoten, AmAnker, hoehe);
            else if (name == "Anker_Redner") m.Redner = new Stuhl(knoten, AmAnker, 0);
            else if (name == "Anker_Schreiber") m.Schreiber = new Stuhl(knoten, AmAnker, hoehe);
            else if (name == "Antrag") m.Antrag = knoten;
            else if (name == "Blick_Rat_R2") m.RednerBlick = knoten;
            else if (name == "Blick_Sitzung_Auge") m.Auge = knoten;
            else if (name == "Blick_Sitzung_Ziel") m.Ziel = knoten;
            else if (name.StartsWith("Anker_Sitz_") && int.TryParse(name[11..], out var nummer)) sitze.Add((ExtraZahl(knoten, "platz", nummer), new Stuhl(knoten, AmAnker, hoehe)));
            else if (name.StartsWith("Anker_Bank_") && int.TryParse(name[11..], out var platz)) bank.Add((platz, new Stuhl(knoten, AmAnker, hoehe)));
        }
        if (buergermeister is null || sitze.Count == 0) return false;
        if (m.Antrag is not null) m.Antrag.Visible = false;
        m.Bank.AddRange(bank.OrderBy(b => b.Nummer).Select(b => b.Stuhl));
        m.Stuehle.Add(buergermeister);
        m.Stuehle.AddRange(sitze.OrderBy(x => x.Platz).Select(x => x.Stuhl));
        _ratssaal = m;
        return true;
    }

    /// <summary>
    /// Die Plätze der Ratsherren in der neuen Ratshalle für die Figuren zur Arbeitszeit (<see cref="RathausBauen"/>): Bürgermeister zuerst,
    /// dann nach Rang; Lage relativ zu <c>a.Mitte</c> unter „InteriorRoot“, wie <see cref="PersonBauen"/> sie erwartet. Null ohne Ratshalle.
    /// </summary>
    private Arbeitsplatz[]? RathausNeuSitzplaetze(SonderbauAnsicht a)
    {
        if (_rathausNeuInnen is not { } wurzel || !_rathausNeuRaum.TryGetValue(RaumRat, out var halle)) return null;
        var anker = halle.FindChildren("Anker_*", nameof(Node3D), true, false).OfType<Node3D>()
            .Where(k => k.Name.ToString() == "Anker_Buergermeister" || k.Name.ToString().StartsWith("Anker_Sitz_"))
            .OrderBy(k => k.Name.ToString() == "Anker_Buergermeister" ? -1 : ExtraZahl(k, "platz", 99)).ToList();
        return anker.Select((k, i) =>
        {
            var lage = wurzel.GlobalTransform.AffineInverse() * k.GlobalTransform;
            var schreibt = i > 0 && i % 2 == 1;
            return new Arbeitsplatz(lage.Origin - a.Mitte, lage.Basis.GetEuler().Y, schreibt ? Bewegung.Schreiben : Bewegung.Beten, schreibt ? Werkzeug.Feder : Werkzeug.Keins, true);
        }).ToArray();
    }

    /// <summary>Zeigt den Raum im neuen Rathaus: „InteriorRoot“ an, die alte Raumansicht aus, die Kamera auf dem Blick des Raums.</summary>
    private void RathausNeuZeigen(Node3D raum)
    {
        if (_rathausNeuInnen is not { } wurzel || _rathausNeuHuelle is null) return;
        if (_neubauInnen is not null && _neubauInnen != wurzel) NeubauInnenAus();
        _neubauInnen = wurzel;
        wurzel.Visible = true;
        wurzel.ProcessMode = ProcessModeEnum.Inherit;
        if (_rathausNeuHuelle.FindChild("Scheiben", true, false) is Node3D scheiben) scheiben.Visible = false;
        if (_rathaus?.Innen is { } unten) unten.Visible = false; // die alten Räume unter der Stadt werden nicht gebraucht
        foreach (var (kennung, gruppe) in _rathausNeuSchilder) gruppe.Visible = _rathausNeuRaum.GetValueOrDefault(kennung) == raum; // nur die Schilder dieses Raums
        _rathausNeuGezeigt = raum;
        RathausNeuEindringlingeZeigen(false);
        RathausNeuRaumEindringlingeZeigen(_rathausNeuRaum.FirstOrDefault(r => r.Value == raum).Key);
        CallDeferred(MethodName.RathausNeuSchilderPruefen, raum);   // nach dem Setzen der Kamera
        CallDeferred(MethodName.RathausNeuKameraPruefen, raum);
        CallDeferred(MethodName.RathausNeuFensterblickPruefen, raum);
        CallDeferred(MethodName.RathausNeuSchilderlistenPruefen);
        foreach (var (r, licht) in _rathausNeuSchattenlichter) if (IsInstanceValid(licht)) licht.Visible = r == raum;
        if (Uebergangsblende.Messen) RathausNeuBildzeitenMessen(raum.Name);
        var auge = raum.FindChild("Blick_Auge", true, false) as Node3D;
        NeubauKameraSetzen(auge, raum.FindChild("Blick_Ziel", true, false) as Node3D);
        if (auge is not null) _raumKamera.Fov = ExtraZahlKomma(auge, "fov", 66f);   // enge Räume (Amtsstube, Turm) blicken weiter
        GD.Print($"[rathausneu] Raum {_raum?.Kennung} im Haus, Kamera {(auge is null ? "ohne Anker" : _raumKamera.GlobalPosition.ToString("0.00"))}");
    }

    /// <summary>Hover über einer Tür des neuen Rathauses (für den Mauszeiger: das Schloss wie an der Haustür).</summary>
    private bool IstRathausTuer(string? kennung) => kennung is not null && kennung.StartsWith(RathausWeg + "tuer|") && _rathausNeuInnen is not null && _neubauInnen == _rathausNeuInnen;

    /// <summary>
    /// Klick auf eine Tür, Treppe oder Leiter im neuen Rathaus (aus Hauptszene.RaumAngeklickt): Raumwechsel mit Blende, bei Türen mit
    /// Klinke; das Portal führt hinaus. Gibt false zurück, wenn die Kennung kein Weg im Rathaus ist.
    /// </summary>
    public bool RathausWegAngeklickt(string kennung)
    {
        if (_rathausNeuInnen is null || _neubauInnen != _rathausNeuInnen || RathausGegenstaende.WegLesen(kennung) is not var (tuer, ziel)) return false;
        if (ziel == "stadt") { InnenansichtVerlassenDurchTuer(); return true; }
        if (!Uebergangsblende.Aktiv || UebergangLaeuft) { if (!UebergangLaeuft) RaumWaehlen(ziel); return true; }
        var schritte = new List<(string, Action)> { ("Raum", () => { _kameraSprung = true; try { RaumWaehlen(ziel); } finally { _kameraSprung = false; } }) };
        // Tür: die Hand greift die Klinke, dann die Blende (wie beim Betreten); Treppe und Leiter: nur die Blende
        Blende().Starten(tuer ? UebergangRichtung.Betreten : UebergangRichtung.Verlassen, ziel, schritte, klinke: tuer);
        return true;
    }

    /// <summary>
    /// Das Trim-Material des Rathauses (Hülle und sieben Räume): derselbe Shader <c>building_trim.gdshader</c> wie die Häuser (Texture2DArray,
    /// Stein-Mip, Innenschärfe; <c>stroh_dach</c> aus: Schiefer statt Stroh), aber eigene Trim-Sheets (Schiefer, Quader, Steinplatten) als Array mit
    /// einer Ebene: außen <c>rathaus_ebenen_*</c> (Quader mit dem Stein K1 der Mensch-Häuser, <c>trimsheet_ebenen.py --array=rathaus</c>), innen
    /// <c>rathaus_innen_trim_*</c>. Je Gruppe und Kantenart eines; die Schieferkanten sind die Variante mit Alpha Scissor.
    /// </summary>
    private ShaderMaterial RathausStoff(string datei, string gruppe, bool innen, bool kante)
    {
        var schluessel = gruppe + (kante ? "|kante" : "");
        if (_neubauStoffe.TryGetValue(schluessel, out var stoff)) return stoff;
        var blatt = NeubauBlatt(datei);
        var textur = innen ? blatt : "rathaus_ebenen";                // außen: Array mit dem Stein K1 im Quaderband; innen: das Innenblatt als Array
        _neubauShader ??= GD.Load<Shader>("res://assets/shader/building_trim.gdshader");
        _neubauKantenShader ??= GD.Load<Shader>("res://assets/shader/building_trim_kante.gdshader");
        stoff = new ShaderMaterial { Shader = kante ? _neubauKantenShader : _neubauShader, ResourceName = "Trim " + schluessel };
        if (kante) stoff.SetShaderParameter("kante_schwelle", NeubauBrand.KantenSchwelle(Fenster.PixelLookAn));
        stoff.SetShaderParameter("albedo_tex", NeubauTextur<TextureLayered>(textur + "_albedo.png"));
        stoff.SetShaderParameter("normal_tex", NeubauTextur<TextureLayered>(textur + "_normal.png"));
        stoff.SetShaderParameter("orm_tex", NeubauTextur<TextureLayered>(textur + "_orm.png"));
        stoff.SetShaderParameter("ao_uv2_tex", NeubauTextur<TextureLayered>(gruppe + "_ao_atlas.png"));
        stoff.SetShaderParameter("wetter_an", innen ? 0f : 1f);      // im Haus regnet und schneit es nicht
        stoff.SetShaderParameter("pixel_look", Fenster.PixelLookAn ? 1f : 0f);
        stoff.SetShaderParameter("stroh_dach", 0f);                  // Schiefer statt Stroh: keine Halmstriche
        stoff.SetShaderParameter("stein_je_ebene", false);           // eine Ebene mit eigenem Layout: die Steinbänder stehen in stein_baender
        var stein = SteinMip.Von(blatt);                             // Steinbänder: kleinerer Mip-Zuschlag im Pixel-Look
        stoff.SetShaderParameter("stein_baender", new Vector4(stein.A0, stein.A1, stein.B0, stein.B1));
        if (float.TryParse(NeubauOption("--steinmip"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var steinMip))
            stoff.SetShaderParameter("stein_mip", steinMip);        // Prüfhilfe: Mip-Zuschlag der Steinbänder vergleichen (Standard im Shader 1)
        return _neubauStoffe[schluessel] = stoff;
    }
}
