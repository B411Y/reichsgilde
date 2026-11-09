using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Ego-Perspektive in der Hauptszene (Knopf im Entwicklerfenster, Nutzer 09.11.2026) – ein Teil der <c>partial class Hauptszene</c>. Die Ansicht selbst steckt in
/// <c>Stadtansicht.Ego.cs</c> und <c>EgoKamera.cs</c>; hier nur, was zur Oberfläche gehört: Solange man in der Ego-Perspektive steht, ist die Oberfläche
/// ausgeblendet (zu sehen bleibt allein der Hinweis „Esc: zurück“ der Ego-Kamera) und danach wieder so, wie sie vorher war (auch bei <c>--ui=0</c>).
/// Öffnet sich in der Zeit ein Bericht, das Spielmenü oder eine Ratssitzung, endet die Ego-Perspektive, damit nichts Unsichtbares im Weg steht.
/// Startoption <c>--ego=x,z[,gier[,nick]]</c> (Hauptszene.cs ruft <see cref="EgoVerbinden"/> nach dem Aufbau).
/// </summary>
public partial class Hauptszene
{
    private bool _egoOberflaecheWar = true;

    private void EgoVerbinden(Dictionary<string, string> optionen)
    {
        _stadt.EgoGeaendert += an =>
        {
            var oberflaeche = GetNode<CanvasLayer>("Oberflaeche");
            if (an)
            {
                _egoOberflaecheWar = oberflaeche.Visible;
                oberflaeche.Visible = false;
            }
            else oberflaeche.Visible = _egoOberflaecheWar;
        };
        if (optionen.TryGetValue("ego", out var wert)) _stadt.EgoVorgeben(wert);
    }

    /// <summary>Jedes Bild: Endet die Ego-Perspektive, wenn eine Tafel aufgeht, die man nicht sieht?</summary>
    private void EgoPruefen()
    {
        if (_stadt.EgoAktiv && (_jahresbericht.Visible || _spielmenue.Visible || _sitzungsTafel.Laeuft)) _stadt.EgoBeenden();
    }
}
