using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Hufklang der Pferdekarren (ein Teil der <c>partial class Stadtansicht</c>, Klang in <see cref="KlangKarren"/>; Entscheidung des Nutzers, 04.11.2026):
/// Nur fahrende Pferdekarren (heute der Kriegskarren, <see cref="KarrenArt.Pferd"/>) klingen. Der Untergrund kommt aus dem Wegenetz des Kerns
/// (<c>Wegebau.WegstelleBei</c>, <c>BelagBei</c>, nur gelesen): Pflaster gibt harte Hufe, Kies, Fahrweg, Trampelpfad und alles abseits der Wege (Wiese,
/// Feldweg, Furt) gedämpfte. Ochsen- und Handkarren bleiben ohne Hufe. Gehört wird nur im Freien (nicht in Innenansichten) und nur der Nächsten drei.
/// </summary>
public partial class Stadtansicht
{
    private KlangKarren? _karrenKlang;

    /// <summary>Startoption <c>--karrenklangtest=1</c>: je Pferdekarren einmal im Protokoll, welcher Untergrund (und Weg) bei 0, 25, 50, 75 und 100 % seines Weges gilt – auch am Hof, ohne dass er fährt.</summary>
    private static readonly bool KarrenKlangTest = OS.GetCmdlineUserArgs().Contains("--karrenklangtest=1");

    /// <summary>So oft (echte Sekunden) wird der Untergrund eines Karrens neu gelesen; unterwegs ändert er sich nur alle paar Meter.</summary>
    private const double KarrenKlangLesen = 0.25;

    /// <summary>Darf der Karren so weit (Meter) neben der Mittellinie des Weges liegen und gilt noch als auf ihm (halbe Breite plus Spur und Rand).</summary>
    private const float KarrenKlangRand = 2.5f;

    /// <summary>Der Hufklang für den Entwicklerreiter und die Hörprobe: letzte Meldung und Zahl hörbarer Karren.</summary>
    public string KarrenKlangText => _karrenKlang is null ? "–" : $"{_karrenKlang.Hoerbar} hörbar, zuletzt: {_karrenKlang.Letzter}";

    /// <summary>Die Karren mit Hufklang (Name, Untergrund, Verstärkung 0–1) – für die Hörprobe im Protokoll.</summary>
    public IEnumerable<(string Name, KlangKarren.Untergrund Boden, float Verstaerkung)> KarrenKlangBelegt =>
        _karrenKlang?.Belegt ?? Enumerable.Empty<(string, KlangKarren.Untergrund, float)>();

    private double _karrenKlangProbeZeit;

    /// <summary>Mit <c>--karrenklangtest=1</c> alle 2 Sekunden: Untergrund an 5 Stellen des Weges jedes Pferdekarrens, geschrieben nur bei Änderung (auch wenn ein Ausbau fertig wird).</summary>
    private void KarrenKlangProbe()
    {
        var jetzt = Time.GetTicksMsec() / 1000.0;
        if (!KarrenKlangTest || jetzt - _karrenKlangProbeZeit < 2) return;
        _karrenKlangProbeZeit = jetzt;
        foreach (var karren in _karren.Values)
        {
            if (!karren.Art.Pferd || karren.KlangProbeWeg is not { Length: >= 2 } weg) continue;
            var teile = new List<string>();
            for (var i = 0; i <= 4; i++)
            {
                var (boden, wo) = UntergrundUnter(PunktAufWeg(weg, i / 4f));
                teile.Add($"{i * 25} %: {(boden is null ? "–" : boden.ToString())} ({wo})");
            }
            var text = string.Join("; ", teile);
            if (text == karren.KlangProbeText) continue;
            karren.KlangProbeText = text;
            GD.Print($"[karrenklang] Probe {karren.KlangName} ({WegLaenge(weg):0} m): {text}");
        }
    }

    private void KarrenKlangAbgleichen(double delta)
    {
        KarrenKlangProbe();
        if (_karrenKlang is null && !_karren.Values.Any(k => k.Art.Pferd)) return; // ohne Pferdekarren kostet es nichts
        _karrenKlang ??= new KlangKarren();
        var stumm = _raum is not null || _innen is not null; // in Gebäuden, im Dom und in Räumen hört man draußen nichts
        var zoom = KlangZoom;
        var blick = FreiAktiv ? StadtKamera.GlobalPosition : _kameraDrehung.GlobalPosition;
        var reichweite = KlangKarren.Reichweite(zoom);
        var kandidaten = new List<KlangKarren.Kandidat>();
        var jetzt = Time.GetTicksMsec() / 1000.0;
        if (!stumm)
        {
            foreach (var karren in _karren.Values)
            {
                if (!karren.Art.Pferd || karren.Geschwindigkeit <= 0.05f || !karren.Wurzel.Visible) continue;
                var ort = karren.Wurzel.GlobalPosition;
                var abstand = new Vector2(ort.X - blick.X, ort.Z - blick.Z).Length();
                // Plätze behalten sie etwas länger als sie neu bekommen (Spielraum gegen Flackern am Rand der Hörweite)
                if (abstand > reichweite) continue;
                if (jetzt - karren.KlangGelesen >= KarrenKlangLesen)
                {
                    karren.KlangGelesen = jetzt;
                    var (boden, wo) = UntergrundUnter(karren.Wurzel.Position);
                    karren.KlangBoden = boden ?? karren.KlangBoden;
                    if (wo != "") karren.KlangWo = wo;
                }
                kandidaten.Add(new KlangKarren.Kandidat(karren, karren.KlangName, ort, abstand, karren.KlangBoden, karren.KlangWo));
            }
        }
        _karrenKlang.Aktualisieren(this, kandidaten, blick, StadtKamera.GlobalPosition, zoom, stumm, delta);
    }

    /// <summary>
    /// Der Untergrund unter einem Punkt der Stadt: Pflaster, wenn der Weg dort gepflastert ist, sonst Erde (Kies, Fahrweg, Trampelpfad); abseits aller Wege
    /// null (dann bleibt, was der Karren zuletzt hatte (Plätze, Furten, Kreuzungen wechseln nicht hin und her).
    /// </summary>
    private (KlangKarren.Untergrund? Boden, string Wo) UntergrundUnter(Vector3 p)
    {
        if (_wirtschaft?.Wegebau is not { IstAktiv: true } bau || _karte is not { } karte) return (null, "");
        if (bau.WegstelleBei(p.X, p.Z, 8) is not var (weg, stelle, abstand)) return (null, "kein Weg in der Nähe");
        if (abstand > karte.Wege[weg].BreiteWirksam / 2 + KarrenKlangRand) return (null, "abseits der Wege");
        var stufe = bau.BelagBei(weg, stelle);
        return (stufe == Belagstufe.Pflaster ? KlangKarren.Untergrund.Pflaster : KlangKarren.Untergrund.Erde, $"Weg {weg} „{WegName(weg)}“ bei {stelle:0} m, Belag {Wegbelag.Anzeigename(stufe)}");
    }
}
