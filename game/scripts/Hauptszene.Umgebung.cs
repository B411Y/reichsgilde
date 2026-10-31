namespace Reichsgilde;

/// <summary>
/// Hängt die <b>Umgebungsklänge</b> (<see cref="KlangUmgebung"/>, Ton Schritt 4) an die Partie – ein Teil der <c>partial class Hauptszene</c>.
/// Der Knoten entsteht vor dem Ladebildschirm (er lädt seine Dateien im Hintergrund) und beginnt erst, wenn der Ladebildschirm geht
/// (<see cref="_umgebungBereit"/>). Er liest nur: Uhr, Bezugsstunde, Jahreszeit, Zoom und Innenansicht aus der Stadtansicht
/// (Stadtansicht.Klang.cs: dort auch das Momentbild der Menschen, wer draußen geht, auf dem Markt ist, in den Schänken sitzt, wo gearbeitet wird),
/// Pause und Tempo aus dem Takt. Hauptmenü und Kartenwerkstatt haben keine Hauptszene, also keine Umgebung.
/// </summary>
public partial class Hauptszene
{
    private KlangUmgebung? _umgebung;
    private bool _umgebungBereit;

    private void UmgebungAnlegen()
    {
        _umgebung = new KlangUmgebung { Name = "KlangUmgebung" };
        _umgebung.Quelle = () => _umgebungBereit && _simulation is { } s
            ? new KlangUmgebungModell.Eingaben(_stadt.KlangUhrMinute, _stadt.KlangBezugStunde, _stadt.KlangJahreszeit, _stadt.KlangZoom,
                _stadt.KlangZoomGroesst, _stadt.KlangInnenart, s.Takt.IstPausiert, s.Takt.TicksProSekunde, KlangDienst.HintergrundIstStumm)
            : null;
        _umgebung.LebenQuelle = m => _umgebungBereit && _stadt.KlangMomentbild(m); // wer draußen geht, wer auf dem Markt ist, Gäste, Werkstätten
        _umgebung.UhrVorgeben = stunde => _stadt.LichtUhrVorgeben(stunde); // nur die Hörprobe --umgebungfahrt=1
        _umgebung.ZoomVorgeben = groesse => _stadt.KlangZoomVorgeben(groesse);
        AddChild(_umgebung);
    }

    /// <summary>Vor einem Szenenwechsel: die Umgebung weich ausblenden (sonst stünde sie während des Ladens mit dem letzten Pegel).</summary>
    private void UmgebungAusblenden() => _umgebung?.Ausblenden(0.5f);
}
