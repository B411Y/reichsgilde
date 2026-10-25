using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Oberflächendetails (immer an, seit 25.10.2026 keine Grafikoption mehr) – ein Teil der <c>partial class Stadtansicht</c>.
///
/// Die Arbeit mit den Materialien steht in <see cref="Oberflaechendetails"/>; hier ist die Verdrahtung:
/// <list type="bullet">
/// <item>Beim Start wird alles umgestellt, was schon da ist, und <see cref="SceneTree.NodeAdded"/> meldet alles, was danach
/// dazukommt (Neubau, Ausbau, Karrenkauf, Passanten, Innenräume): am Ende des Bildes, wenn das Spiel das Material des neuen Knotens
/// gesetzt hat, bekommt er sein Material mit Struktur.</item>
/// <item>Verlässt die Stadtansicht den Baum, gehen die Materialien zurück an die Knoten: kein Rest.</item>
/// <item>Je Bild: das Nachziehen umgefärbter Materialien.</item>
/// <item>Die Stufe der Material-Bibliothek folgt der Einstellung „Boden“ (<see cref="OberflaecheStufeNachfuehren"/>).</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Die Materialien, die die Oberflächendetails ersetzt haben; null vor <c>_Ready</c> und nach <c>_ExitTree</c>.</summary>
    private Oberflaechendetails? _oberflaeche;

    private readonly List<Node> _oberflaecheNeu = new();
    private bool _oberflaechePlanung;

    /// <summary>Einmal in <c>_Ready</c>, vor dem Aufbau: Was schon da ist, bekommt seine Details, und alles Neue danach auch.</summary>
    private void OberflaecheEinrichten()
    {
        _oberflaeche = new Oberflaechendetails();
        OberflaecheStufeNachfuehren();
        GetTree().NodeAdded += OberflaecheKnotenNeu;
        foreach (var teil in FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()) _oberflaeche.Umstellen(teil);
        foreach (var teil in FindChildren("*", nameof(MultiMeshInstance3D), true, false).OfType<MultiMeshInstance3D>()) _oberflaeche.Umstellen(teil);
    }

    /// <summary>
    /// In <c>_ExitTree</c>: dem RenderingServer sein altes Bild zurückgeben. Die abgeleiteten Materialien gehen danach an den
    /// Müllsammler, die Knoten stehen aber noch bis zum Ende des Baums: Keiner soll dann noch auf ein freigegebenes Material zeigen.
    /// </summary>
    private void OberflaecheAufgeben()
    {
        if (_oberflaeche is null) return;
        GetTree().NodeAdded -= OberflaecheKnotenNeu;
        foreach (var teil in FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()) Oberflaechendetails.Zuruecksetzen(teil);
        foreach (var teil in FindChildren("*", nameof(MultiMeshInstance3D), true, false).OfType<MultiMeshInstance3D>()) _oberflaeche.Zuruecksetzen(teil);
        _oberflaeche = null;
        _oberflaecheNeu.Clear();
    }

    /// <summary>Ein Knoten kam in den Baum: Netze und MultiMesh der Stadt merken und am Ende des Bildes umstellen (dann hat das Spiel sein Material gesetzt).</summary>
    private void OberflaecheKnotenNeu(Node knoten)
    {
        if (knoten is not (MeshInstance3D or MultiMeshInstance3D) || !IsAncestorOf(knoten)) return;
        _oberflaecheNeu.Add(knoten);
        if (_oberflaechePlanung) return;
        _oberflaechePlanung = true;
        Callable.From(OberflaecheNeueUmstellen).CallDeferred();
    }

    private void OberflaecheNeueUmstellen()
    {
        _oberflaechePlanung = false;
        var neu = _oberflaecheNeu.ToList();
        _oberflaecheNeu.Clear();
        if (_oberflaeche is null) return;
        foreach (var knoten in neu)
        {
            if (!IsInstanceValid(knoten)) continue;
            if (knoten is MeshInstance3D teil) _oberflaeche.Umstellen(teil);
            else if (knoten is MultiMeshInstance3D vielfach) _oberflaeche.Umstellen(vielfach);
        }
    }

    /// <summary>Das Netz, das ein MultiMesh jetzt zeigen soll: mit Struktur (Jahreszeitwechsel der Natur).</summary>
    private Mesh OberflaecheNetz(Mesh netz) => _oberflaeche?.Netz(netz) ?? netz;

    /// <summary>Stufe der Material-Bibliothek nach der Einstellung „Boden“: Niedrig die Struktur wie bisher, Mittel Farbe, Hoch dazu Normalen und Rauheit.</summary>
    private void OberflaecheStufeNachfuehren()
    {
        if (_oberflaeche is not null) _oberflaeche.Stufe = (int)Fenster.AktuelleBodenqualitaet;
    }

    /// <summary>Jedes Bild: umgefärbte Materialien nachziehen.</summary>
    private void OberflaecheBild() => _oberflaeche?.Abgleichen();
}
