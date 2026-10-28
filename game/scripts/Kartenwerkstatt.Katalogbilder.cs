using Godot;

namespace Reichsgilde;

/// <summary>
/// Vorab gerenderte Vorschaubilder des Katalogs – ein Teil der <c>partial class Kartenwerkstatt</c>. Die Palette zeigt für Bäume, Büsche, Requisiten und
/// Gebäude ein kleines Bild des 3D-Objekts (<c>game/assets/oberflaeche/katalog/&lt;kennung&gt;.png</c>); fehlt es, bleibt das allgemeine Symbol
/// (Gebäude werden dann beim ersten Aufklappen gerendert, <see cref="VorschauenRendern"/>). Neu rendern: <c>tools/katalogbilder/katalogbilder.sh</c>.
/// </summary>
public partial class Kartenwerkstatt
{
    private const string KatalogbildOrdner = "res://assets/oberflaeche/katalog/";

    private static readonly Dictionary<string, Texture2D?> _katalogbilder = new();

    /// <summary>Das vorab gerenderte Bild eines Bausteins (null: es gibt keins).</summary>
    private static Texture2D? KatalogBild(string kennung)
    {
        if (_katalogbilder.TryGetValue(kennung, out var bild)) return bild;
        var pfad = $"{KatalogbildOrdner}{kennung}.png";
        return _katalogbilder[kennung] = ResourceLoader.Exists(pfad) ? ResourceLoader.Load<Texture2D>(pfad) : null;
    }

    /// <summary>
    /// <c>--katalogbilder=1</c>: rendert zu jedem Baustein mit Modell ein Bild nach <c>game/assets/oberflaeche/katalog/</c> und beendet das Spiel
    /// (<c>--katalogbilder=&lt;kennung&gt;,&lt;kennung&gt;</c> nur diese). Braucht ein Fenster (Xvfb), siehe <c>tools/katalogbilder/katalogbilder.sh</c>.
    /// </summary>
    private async void KatalogbilderSchreiben(string auswahl)
    {
        var nur = auswahl == "1" ? null : auswahl.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var ordner = ProjectSettings.GlobalizePath(KatalogbildOrdner);
        Directory.CreateDirectory(ordner);
        var bausteine = _entwurf.Katalog.Bausteine.Where(b => nur is null || nur.Contains(b.Kennung)).ToList();
        var geschrieben = 0;
        const int Stapel = 24; // nicht alle Modelle auf einmal
        for (var start = 0; start < bausteine.Count; start += Stapel)
        {
            var halter = new Node { Name = "Katalogbilder" };
            AddChild(halter);
            var fenster = new List<(string Kennung, SubViewport Fenster)>();
            foreach (var b in bausteine.Skip(start).Take(Stapel))
                if (_stadt.WerkstattVorschaumodell(b) is { } modell) fenster.Add((b.Kennung, VorschauFenster(halter, modell, 96)));
            for (var i = 0; i < 4; i++) await ToSignal(RenderingServer.Singleton, RenderingServerInstance.SignalName.FramePostDraw);
            foreach (var (kennung, f) in fenster)
            {
                var fehler = f.GetTexture().GetImage().SavePng($"{ordner}{kennung}.png");
                if (fehler == Error.Ok) geschrieben++;
                else GD.PushError($"Katalogbild {kennung}: {fehler}");
            }
            halter.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GD.Print($"Katalogbilder: {geschrieben} von {bausteine.Count} Bausteinen geschrieben nach {ordner}");
        GetTree().Quit();
    }
}
