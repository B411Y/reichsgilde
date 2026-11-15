using Godot;

namespace Reichsgilde;

/// <summary>
/// Sicherung und Prüfung der Schilderliste <c>_beschriftungen</c> (ein Teil der <c>partial class Stadtansicht</c>, Nachprüfung zu PR #453):
/// Wer einen Knoten mit Beschriftungen freigibt, nimmt ihn über <see cref="KnotenEntfernen"/> heraus. Vergisst eine Stelle das, stünde ein
/// freigegebenes Schild in der Liste, und der Zugriff darauf (<c>label.Scale</c> in <see cref="ProzessBild"/>) würfe in jedem Bild eine
/// ObjectDisposedException – <c>_Process</c> stünde still (Kamera, Raum, Pixel-Look). Die Sicherung wirft solche Schilder vor der Schleife
/// hinaus und meldet es als Fehler im Protokoll (<c>[schilder]</c>), damit der vergessene Fall auffällt statt verschluckt zu werden.
/// Startoption <c>--schilderpruefung=1</c>: dazu alle 10 s eine Zeile mit Bildzahl, Zahl der Schilder und der bisher gefundenen
/// freigegebenen Schilder – sie kommt am Ende von <see cref="_Process"/>, zeigt also auch, dass das Bild bis zum Schluss läuft.
/// </summary>
public partial class Stadtansicht
{
    private static readonly bool SchilderPruefungAn =
        OS.GetCmdlineUserArgs().Contains("--schilderpruefung=1");

    private int _schilderVerwaist;   // bisher gefundene freigegebene Schilder in _beschriftungen (soll 0 bleiben)
    private long _schilderBilder;
    private double _schilderZeit;

    /// <summary>Aus <see cref="ProzessBild"/> vor jedem Zugriff auf die Schilder: freigegebene hinaus, mit Fehlermeldung.</summary>
    private void SchilderAufraeumen()
    {
        var verwaist = _beschriftungen.RemoveAll(label => !IsInstanceValid(label));
        if (verwaist == 0) return;
        _schilderVerwaist += verwaist;
        GD.PushError($"[schilder] {verwaist} freigegebene Schilder standen noch in _beschriftungen – ein Knoten mit Beschriftung wurde ohne KnotenEntfernen freigegeben");
    }

    /// <summary>Am Ende jedes Bildes (aus <see cref="_Process"/>); ohne <c>--schilderpruefung=1</c> nichts.</summary>
    private void SchilderPruefungBild(double delta)
    {
        if (!SchilderPruefungAn) return;
        _schilderBilder++;
        _schilderZeit += delta;
        if (_schilderZeit < 10) return;
        _schilderZeit = 0;
        GD.Print($"[schilder] Bild {_schilderBilder}: {_beschriftungen.Count} Schilder, freigegeben gefunden {_schilderVerwaist}, innen={(InnenKennung ?? "nein")}");
    }
}
