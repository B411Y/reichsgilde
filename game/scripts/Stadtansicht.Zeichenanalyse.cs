using System.Globalization;
using System.Text;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Zeichenanalyse der Stadtansicht (Startoption <c>--zeichenanalyse=1</c>, nur zum Messen; ein Teil der <c>partial class Stadtansicht</c>):
/// Nach dem Vorlauf blendet sie nacheinander jede Gruppe gleichnamiger Kinder der Stadt aus und liest aus, um wie viele Zeichenaufrufe
/// (<see cref="RenderingServer.RenderingInfo.TotalDrawCallsInFrame"/>, beide Ansichten des Pixel-Looks zusammen) das Bild dadurch billiger wird.
/// Große Gruppen werden eine Ebene tiefer zerlegt. Dazu je Gruppe: sichtbare Netze, Flächen (Surfaces), Instanzen der MultiMeshes, Partikel, Lichter.
/// Das Ergebnis steht als <c>[zeichenanalyse]</c>-Zeilen im Protokoll, danach beendet sich das Spiel. Ohne die Option kostet es nichts.
/// <c>--zeichenanalyse=2</c>: dazu schaltet sie am Ende Schatten der Sonne und der Lichter einzeln ab (Anteil der Schatten-Durchgänge).
/// </summary>
public partial class Stadtansicht
{
    private static readonly string? ZeichenAnalyseWert =
        OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--zeichenanalyse="))?["--zeichenanalyse=".Length..];

    private int _zaBild;
    private bool _zaFertig;
    private readonly StringBuilder _zaAusgabe = new();

    private static int ZeichenAufrufe() => (int)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame);

    /// <summary>Wird jedes Bild aufgerufen (aus <see cref="_Process"/>), tut ohne die Startoption nichts.</summary>
    private void ZeichenAnalyseBild()
    {
        if (ZeichenAnalyseWert is null || _zaFertig) return;
        _zaBild++;
        if (_zaBild < 600) return; // Vorlauf: Stadt, Figuren und Shader sind fertig (bei 60 Bildern/s etwa 10 s, wir haben mehr)
        _zaFertig = true;
        _ = ZeichenAnalyseLaufAsync();
    }

    private async System.Threading.Tasks.Task<int> ZaMessenAsync(int bilder = 8)
    {
        for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var werte = new List<int>();
        for (var i = 0; i < bilder; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            werte.Add(ZeichenAufrufe());
        }
        werte.Sort();
        return werte[werte.Count / 2];
    }

    private static string ZaSchluessel(Node knoten)
    {
        var name = knoten.Name.ToString();
        var sb = new StringBuilder();
        foreach (var c in name) if (!char.IsDigit(c) && c != '@' && c != '_' && c != '-') sb.Append(c);
        return sb + "|" + knoten.GetType().Name;
    }

    private static string ZaBeschreibung(List<Node> mitglieder)
    {
        int netze = 0, flaechen = 0, multi = 0, instanzen = 0, partikel = 0, lichter = 0, schattenLichter = 0, schattenNetze = 0;
        void Zaehlen(Node k)
        {
            if (k is Node3D n3 && !n3.IsVisibleInTree()) return;
            switch (k)
            {
                case MultiMeshInstance3D mm when mm.Multimesh is { } m:
                    multi++;
                    instanzen += m.VisibleInstanceCount < 0 ? m.InstanceCount : m.VisibleInstanceCount;
                    break;
                case MeshInstance3D mi when mi.Mesh is { } mesh:
                    netze++;
                    flaechen += mesh.GetSurfaceCount();
                    if (mi.CastShadow != GeometryInstance3D.ShadowCastingSetting.Off) schattenNetze++;
                    break;
                case GpuParticles3D or CpuParticles3D: partikel++; break;
                case Light3D l:
                    lichter++;
                    if (l.ShadowEnabled) schattenLichter++;
                    break;
            }
            foreach (var kind in k.GetChildren()) Zaehlen(kind);
        }
        foreach (var m in mitglieder) Zaehlen(m);
        return $"Netze {netze} (Flächen {flaechen}, werfen Schatten {schattenNetze}), MultiMesh {multi} ({instanzen} Instanzen), Partikel {partikel}, Lichter {lichter} (mit Schatten {schattenLichter})";
    }

    private async System.Threading.Tasks.Task ZaGruppeAsync(List<Node> kinder, string einzug, int tiefe, int grundlinie)
    {
        // gleichnamige Geschwister zu einer Gruppe, Reihenfolge nach erstem Auftreten
        var gruppen = new List<(string Schluessel, List<Node> Mitglieder)>();
        foreach (var k in kinder)
        {
            if (k is not Node3D) continue;
            var s = ZaSchluessel(k);
            var g = gruppen.FindIndex(x => x.Schluessel == s);
            if (g < 0) gruppen.Add((s, new List<Node> { k })); else gruppen[g].Mitglieder.Add(k);
        }
        var ergebnis = new List<(string Schluessel, List<Node> Mitglieder, int Delta)>();
        foreach (var (schluessel, mitglieder) in gruppen)
        {
            var sichtbar = mitglieder.Where(m => m is Node3D n && n.Visible && m is not DirectionalLight3D).Cast<Node3D>().ToList();
            if (sichtbar.Count == 0) continue;
            var davor = await ZaMessenAsync(5);   // jede Gruppe gegen ihre eigene Grundlinie (Passanten und Spielzeit ändern die Zahl laufend)
            int danach;
            if (ZeichenAnalyseWert == "4")
            {
                // nur die Schatten der Gruppe ausschalten (Kosten der Schattenkarte, beide Ansichten zusammen)
                var werfer = new List<GeometryInstance3D>();
                void Sammeln(Node k)
                {
                    if (k is GeometryInstance3D g && g.CastShadow != GeometryInstance3D.ShadowCastingSetting.Off) werfer.Add(g);
                    foreach (var kind in k.GetChildren()) Sammeln(kind);
                }
                foreach (var m in sichtbar) Sammeln(m);
                var vorher = werfer.Select(w => w.CastShadow).ToList();
                foreach (var w in werfer) w.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                danach = await ZaMessenAsync(5);
                for (var i = 0; i < werfer.Count; i++) werfer[i].CastShadow = vorher[i];
            }
            else
            {
                foreach (var m in sichtbar) m.Visible = false;
                danach = await ZaMessenAsync(5);
                foreach (var m in sichtbar) m.Visible = true;
            }
            ergebnis.Add((schluessel, mitglieder, davor - danach));
        }
        foreach (var (schluessel, mitglieder, delta) in ergebnis.OrderByDescending(e => e.Delta))
        {
            if (delta == 0 && tiefe > 0) continue;
            _zaAusgabe.AppendLine($"[zeichenanalyse] {einzug}{schluessel.Replace("|", " (")}) x{mitglieder.Count}: {delta} Aufrufe; {ZaBeschreibung(mitglieder)}");
            GD.Print(_zaAusgabe.ToString().TrimEnd('\n').Split('\n').Last());
            if (delta >= 40 && tiefe < 3)
            {
                var enkel = mitglieder.SelectMany(m => m.GetChildren()).ToList();
                if (enkel.Count > 0) await ZaGruppeAsync(enkel, einzug + "  ", tiefe + 1, grundlinie);
            }
        }
    }

    private async System.Threading.Tasks.Task ZeichenAnalyseLaufAsync()
    {
        var grundlinie = await ZaMessenAsync(20);
        GD.Print($"[zeichenanalyse] Grundlinie {grundlinie} Zeichenaufrufe, Kamera {_kamera.Size:0} m; Schattenproxys: {SchattenProxyNetze} Netze verschmolzen ({SchattenProxyFlaechenVorher} Flächen), {SchattenProxyUebrig} Netze unverändert; zusammengelegt: {FlaechenMergeNetze} Netze, Flächen {FlaechenMergeVorher} auf {FlaechenMergeNachher}");
        foreach (var (grund, anzahl) in SchattenProxyGruende.OrderByDescending(g => g.Value)) GD.Print($"[zeichenanalyse] kein Schattenproxy wegen {grund}: {anzahl}");
        if (ZeichenAnalyseWert != "3") await ZaGruppeAsync(GetChildren().ToList(), "", 0, grundlinie);
        // Schatten: Sonne, dann Lichter
        _sonne.ShadowEnabled = false;
        var ohneSonne = await ZaMessenAsync();
        _sonne.ShadowEnabled = true;
        GD.Print($"[zeichenanalyse] Schatten der Sonne aus: {ohneSonne} (Anteil {grundlinie - ohneSonne})");
        var lichter = GetTree().Root.FindChildren("*", "Light3D", true, false).Cast<Light3D>().Where(l => l is not DirectionalLight3D && l.IsVisibleInTree() && l.ShadowEnabled).ToList();
        foreach (var l in lichter) l.ShadowEnabled = false;
        var ohneLichter = await ZaMessenAsync();
        foreach (var l in lichter) l.ShadowEnabled = true;
        GD.Print($"[zeichenanalyse] Schatten der {lichter.Count} Lichter aus: {ohneLichter} (Anteil {grundlinie - ohneLichter})");
        // Gesamtzahl Zeichenaufrufe der Ansichten
        GD.Print($"[zeichenanalyse] Ende, Grundlinie {grundlinie}; Schattenproxys: {SchattenProxyNetze} Netze verschmolzen ({SchattenProxyFlaechenVorher} Flächen), {SchattenProxyUebrig} Netze unverändert");
        GetTree().Quit();
    }
}
