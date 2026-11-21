using Godot;

namespace Reichsgilde;

/// <summary>
/// <b>Der Einzug der Bürger</b> – ein Teil der <c>partial class Hauptszene</c> („Ladezeit“, Entscheidung 21.11.2026). Die Einwohnerhäuser der Karte
/// kann die Stadt erst bauen, wenn der Kern die Einwohner vergeben hat; das geschieht im ersten Takt der Partie (<c>Einwohnerschaft.Gruenden</c>),
/// bei der Platzwahl erst nach ihr (die Einwohnerhäuser nehmen die Bauzonen, die der Spieler nicht gewählt hat). Früher baute die Stadt dann
/// 80 und mehr Häuser in einem Bild, das Spiel stockte einige Sekunden, und die Häuser standen plötzlich da.
/// Jetzt sperrt die Hauptszene den Bau der Einwohnerhäuser (<see cref="Stadtansicht.WohnhaeuserSperre"/>), solange der Kern sie nicht vergeben hat,
/// und baut sie unter dem Ladebildschirm:
///
///   • Ohne Platzwahl (Spielstand, Mehrspieler, Startoption) am Ende des ersten Ladens: Der Ladebildschirm bleibt („Die Bürger ziehen ein …“),
///     bis der Kern die Häuser vergeben hat und die Stadt sie gebaut hat.
///   • Mit Platzwahl: Sobald die Werkstatt steht, kommt der Ladebildschirm noch einmal, bis die Häuser stehen.
///
/// Die Zeit läuft dabei nur bis zum ersten Takt, der die Einwohner vergibt; danach hält sie (Haltegrund <see cref="HaltegrundEinzug"/>, nur im
/// Einzelspiel, abgeglichen in <c>ZeitHalteAbgleichen</c>) und läuft erst weiter, wenn alles steht. Die Hauptszene verändert den Kern nicht,
/// sie wartet nur auf seine Antwort (<c>Einwohnerschaft.Wohnhaeuser</c>).
/// </summary>
public partial class Hauptszene
{
    /// <summary>Der Haltegrund des Takts, solange die Stadt ihre Einwohnerhäuser baut (nur Einzelspiel).</summary>
    private const string HaltegrundEinzug = "der Einzug der Bürger";

    /// <summary>Längste Wartezeit (Sekunden) auf den Kern bzw. den Bau der Häuser; danach geht das Spiel auch ohne sie weiter.</summary>
    private const double EinzugHoechstSekunden = 25;

    private bool _einzugHaelt;
    private bool _einzugLaeuft;

    /// <summary>Hält der Einzug gerade die Zeit an?</summary>
    private bool EinzugHaelt => _einzugHaelt && DarfAnhalten && !_simulation.IstBeendet;

    /// <summary>Hat der Kern die Einwohnerhäuser schon vergeben?</summary>
    private bool EinwohnerHaeuserVergeben => _simulation.Wirtschaft?.Einwohnerschaft is not { IstAktiv: true } e || e.Wohnhaeuser.Count > 0;

    /// <summary>Nach dem Aufstellen der Partie: Hat der Kern noch keine Häuser vergeben, baut die Stadt sie erst nach dem Einzug (Sperre).</summary>
    private void EinzugVorbereiten()
    {
        _stadt.WohnhaeuserSperre = !EinwohnerHaeuserVergeben;
    }

    /// <summary>Ist der Einzug noch offen (Sperre gesetzt, Häuser fehlen)?</summary>
    private bool EinzugOffen => _stadt.WohnhaeuserSperre && !_stadt.WohnhaeuserStehen;

    /// <summary>Am Ende der Platzwahl: der Ladebildschirm noch einmal, bis die Häuser stehen.</summary>
    private async void EinzugNachPlatzwahl()
    {
        if (_einzugLaeuft || !EinzugOffen) return;
        _einzugLaeuft = true;
        SetProcessUnhandledInput(false);
        var lade = Ladebildschirm.Zeigen(GetTree(), Stadtkarten.StartKennung);
        lade.Fortschritt(0.05f, "Die Bürger ziehen ein …");
        _stadt.Welt3DSperren(true);
        await Ladebildschirm.Bild(GetTree());
        await EinzugAbwarten(lade, 0.05f);
        Ladezeit.Marke("Einzug nach der Platzwahl");
        _stadt.Welt3DSperren(false);
        for (var i = 0; i < 2; i++) await Ladebildschirm.Bild(GetTree()); // die neuen Häuser einmal zeichnen, ehe der Schirm geht (Shader)
        _einzugHaelt = false;
        SetProcessUnhandledInput(true);
        lade.Schliessen();
        _einzugLaeuft = false;
    }

    /// <summary>
    /// Wartet unter dem Ladebildschirm, bis der Kern die Häuser vergeben und die Stadt sie gebaut hat. Die Zeit läuft bis zum ersten Takt mit
    /// Häusern, danach hält sie. <paramref name="von"/> ist der Stand des Balkens am Anfang.
    /// </summary>
    private async Task EinzugAbwarten(Ladebildschirm lade, float von)
    {
        var anfang = Time.GetTicksMsec();
        bool Zeit() => (Time.GetTicksMsec() - anfang) / 1000.0 < EinzugHoechstSekunden;
        while (!EinwohnerHaeuserVergeben && Zeit()) await Ladebildschirm.Bild(GetTree());
        _einzugHaelt = true; // ab jetzt steht die Zeit (ZeitHalteAbgleichen hält den Grund)
        if (DarfAnhalten) _simulation.Takt.Anhalten(HaltegrundEinzug);
        _stadt.WohnhaeuserSperre = false;
        lade.Fortschritt(von + (1f - von) * 0.5f, "Die Bürger ziehen ein …"); // der Balken geht nie zurück
        var bild = 0;
        while (!_stadt.WohnhaeuserStehen && Zeit()) { await Ladebildschirm.Bild(GetTree()); bild++; }
        // Dazu im selben Bild gebaute Grundstücke und die ersten Passanten brauchen noch ein paar Bilder
        for (var i = 0; i < 3; i++) await Ladebildschirm.Bild(GetTree());
        lade.Fortschritt(1f, "Die Bürger ziehen ein …");
        if (Ladezeit.An) GD.Print($"Ladezeit: Einzug der Bürger: {Time.GetTicksMsec() - anfang} ms, {bild} Bilder bis zum Bau");
    }
}
