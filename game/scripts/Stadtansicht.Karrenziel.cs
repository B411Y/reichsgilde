using Godot;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Karren selbst fahren, Ziel im Bild wählen (Darstellung, Paket K3; Teil der <c>partial class Stadtansicht</c>). Drückt der Spieler in der Fahrtafel
/// „Ziel im Bild wählen“, beginnt die Wahl über den Wählmodus der Gebäude (<see cref="GebaeudeWaehlen"/>, Stadtansicht.Gebaeudewahl.cs): Wählbar sind der Markt
/// und die <b>eigenen Betriebe der Stadt</b>, alle anderen Gebäude nicht (die Klickfläche eines Nachbarn davor verdeckt kein Ziel dahinter). Sichtbar wird das durch
/// goldene Ringe auf dem Boden um jedes mögliche Ziel; das Ziel unter der Maus leuchtet weiß und größer, und der Hinweis oben nennt seinen Namen. Ein Klick auf etwas
/// anderes sagt, was geht, und die Wahl bleibt offen; Esc bricht ab.
/// </summary>
public partial class Stadtansicht
{
    private readonly List<(MeshInstance3D Ring, string Kennung, float Radius)> _karrenZielMarken = new();
    private StandardMaterial3D? _karrenZielGold, _karrenZielWeiss;
    private string? _karrenZielHinweis;
    private string? _karrenZielUeber;
    private Konto? _karrenZielSpieler;

    /// <summary>Läuft die Wahl des Ziels eines Karrens im Bild?</summary>
    public bool KarrenZielWahlLaeuft => _karrenZielMarken.Count > 0;

    /// <summary>
    /// Beginnt die Wahl des Karrenziels für das Haus <paramref name="spieler"/>. <paramref name="gewaehlt"/> bekommt den Namen des Betriebs (null: der Markt);
    /// <paramref name="meldung"/> sagt dem Spieler, was nicht geht (ein anderes Gebäude, ein fremder Betrieb); <paramref name="abgebrochen"/> kommt bei Esc.
    /// Drinnen (Innenansicht, Raum) geht es nicht: false.
    /// </summary>
    public bool KarrenZielWaehlen(Konto spieler, Action<string?> gewaehlt, Action<string> meldung, Action abgebrochen)
    {
        KarrenZielMarkenWeg();
        _karrenZielSpieler = spieler;
        _karrenZielHinweis = "Ziel wählen: der Markt oder einer deiner Betriebe";
        bool Beginnen()
        {
            if (!GebaeudeWaehlen(_karrenZielHinweis, Ziel, k => KarrenZielBetrieb(k, out _))) return false;
            _gebaeudeWahlAbbruch = () => { KarrenZielMarkenWeg(); abgebrochen(); };
            KarrenZielMarkenSetzen();
            return true;
        }
        void Ziel(string kennung)
        {
            if (KarrenZielBetrieb(kennung, out var betrieb))
            {
                KarrenZielMarkenWeg();
                gewaehlt(betrieb);
                return;
            }
            meldung(KarrenZielAbsage(kennung));
            if (!Beginnen()) abgebrochen(); // die Wahl bleibt offen
        }
        return Beginnen();
    }

    /// <summary>Ist diese Kennung ein Ziel (Markt: Betrieb null; eigener Betrieb der Stadt: dessen Name)?</summary>
    private bool KarrenZielBetrieb(string kennung, out string? betrieb)
    {
        betrieb = null;
        if (IstMarktplatz(kennung)) return true;
        if (!kennung.StartsWith("betrieb:", StringComparison.Ordinal)) return false;
        var b = _betriebe.Keys.FirstOrDefault(x => BetriebsKennung(x) == kennung);
        if (b is null || b.Konto != _karrenZielSpieler || !b.InDerStadt) return false;
        betrieb = b.Name;
        return true;
    }

    private string KarrenZielAbsage(string kennung)
    {
        if (kennung.StartsWith("betrieb:", StringComparison.Ordinal))
        {
            var b = _betriebe.Keys.FirstOrDefault(x => BetriebsKennung(x) == kennung);
            if (b is not null && b.Konto != _karrenZielSpieler) return $"{b.Name} gehört dir nicht – Karren fahren nur zum Markt oder zu eigenen Betrieben.";
            if (b is not null && !b.InDerStadt) return $"{b.Name} liegt im Umland – Ziele sind der Markt und eigene Betriebe in der Stadt.";
        }
        return "Das ist kein Ziel – Karren fahren nur zum Markt oder zu eigenen Betrieben der Stadt.";
    }

    /// <summary>Die goldenen Ringe um jedes mögliche Ziel (nur beim Beginn der Wahl und nachdem ein falscher Klick sie abgebaut hat).</summary>
    private void KarrenZielMarkenSetzen()
    {
        KarrenZielMarkenWeg();
        _karrenZielGold ??= RingStoff(new Color(1f, 0.78f, 0.22f, 0.85f));
        _karrenZielWeiss ??= RingStoff(new Color(1f, 1f, 0.92f, 0.95f));
        foreach (var platz in _maerkte)
            RingSetzen(platz.Kennung, platz.Mitte, platz.Halb * 0.98f);
        foreach (var (betrieb, ansicht) in _betriebe)
            if (betrieb.Konto == _karrenZielSpieler && betrieb.InDerStadt)
                RingSetzen(BetriebsKennung(betrieb), ansicht.Mitte, 8.5f);

        static StandardMaterial3D RingStoff(Color farbe) => new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = farbe, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            NoDepthTest = true, RenderPriority = 90, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        void RingSetzen(string kennung, Vector3 mitte, float radius)
        {
            var ring = new MeshInstance3D
            {
                Name = "KarrenZielRing", Mesh = new TorusMesh { InnerRadius = radius - 0.45f, OuterRadius = radius + 0.45f, Rings = 64, RingSegments = 4 },
                MaterialOverride = _karrenZielGold, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = AufFuss(mitte) + new Vector3(0, 0.5f, 0), Scale = new Vector3(1, 0.25f, 1),
            };
            AddChild(ring);
            _karrenZielMarken.Add((ring, kennung, radius));
        }
    }

    private void KarrenZielMarkenWeg()
    {
        foreach (var (ring, _, _) in _karrenZielMarken)
            if (IsInstanceValid(ring)) ring.QueueFree();
        _karrenZielMarken.Clear();
        _karrenZielUeber = null;
    }

    /// <summary>Jedes Bild der Wahl: die Ringe pulsieren, das Ziel unter der Maus leuchtet weiß (und der Hinweis nennt es).</summary>
    private void KarrenZielAktualisieren()
    {
        if (_karrenZielMarken.Count == 0 || !GebaeudeWahlLaeuft) return;
        string? ueber = null;
        if (!MausUeberOberflaeche && GebaeudeWahlKennung(GetViewport().GetMousePosition(), still: true) is { } kennung && KarrenZielBetrieb(kennung, out _)) ueber = kennung;
        var puls = 1f + 0.04f * Mathf.Sin((float)Time.GetTicksMsec() / 1000f * 4f);
        foreach (var (ring, k, radius) in _karrenZielMarken)
        {
            if (!IsInstanceValid(ring)) continue;
            var hier = k == ueber;
            ring.MaterialOverride = hier ? _karrenZielWeiss : _karrenZielGold;
            ring.Scale = new Vector3(hier ? 1.1f * puls : puls, 0.25f, hier ? 1.1f * puls : puls);
        }
        if (ueber != _karrenZielUeber && _gebaeudeWahlText is not null)
        {
            _karrenZielUeber = ueber;
            var name = ueber is null ? null : IstMarktplatz(ueber) ? "Markt" : _betriebe.Keys.FirstOrDefault(x => BetriebsKennung(x) == ueber)?.Name;
            _gebaeudeWahlText.Text = (name is null ? _karrenZielHinweis : $"Ziel: {name} – Klick wählt") + " · Esc: abbrechen";
        }
    }
}
