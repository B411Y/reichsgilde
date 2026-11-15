using System.Collections.Generic;
using System.Linq;
using Godot;
using Reichsgilde.Core;

namespace Reichsgilde;

/// <summary>
/// Prüfhilfen für den Klicktest der Figuren (nur mit Startoption, ändern kein Spielverhalten, nur was man sieht):
/// <list type="bullet">
/// <item><c>--figurprobe=&lt;volk&gt;:&lt;m|w&gt;:&lt;clip&gt;</c> – im Werkraum eines Raummodells (<c>--innen=1</c>) arbeiten die Arbeiter als Passanten-Figur
/// dieses Volks und Geschlechts, der erste am Platz mit diesem Clip, immer (auch außerhalb der Arbeitszeit); kleine Völker auf ihrem Hocker
/// (<see cref="ArbeitsTritt"/>). Ohne <c>--innen</c> steht eine solche Figur draußen am Blickpunkt (Hüten mit dem Stecken des Hirten).</item>
/// <item><c>--gaesteprobe=&lt;n&gt;</c> – in der Schänke sitzen n Gäste an den Tischen, unabhängig von der Uhrzeit (alle Plätze: 99).</item>
/// <item><c>--armprobe=&lt;Sekunden&gt;</c> – nach so vielen Sekunden im Raum: Abspreizwinkel der Oberarme aller sichtbaren Figuren mit Passanten-Skelett
/// im gezeigten Raum (gegen die Senkrechte, Grenzwert <see cref="ArmGrenze"/>) ins Log; die Ruhelage der Dateien (ohne Pose) hat 42°.</item>
/// </list>
/// Das Log nennt „Prüfhilfe: &lt;Szene&gt; &lt;Figur&gt; &lt;Clip&gt;“.
/// </summary>
public partial class Stadtansicht
{
    private static readonly string? FigurProbeText = Wasserarten.Startoption("figurprobe");
    private static readonly FigurenBaukasten.FigurProbe? FigurProbeOption = FigurenBaukasten.FigurProbeLesen(FigurProbeText);
    private static readonly int? GaesteProbe = int.TryParse(Wasserarten.Startoption("gaesteprobe"), out var gaeste) && gaeste >= 0 ? gaeste : null;

    private static readonly double? ArmProbeNach = double.TryParse(Wasserarten.Startoption("armprobe"), System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out var nach) && nach > 0 ? nach : null;
    /// <summary>Grenzwert der Armprüfung (Grad, Mittel beider Oberarme gegen die Senkrechte): Stehen, Gehen, Reden, Sitzen der Dateien liegen bei 5–28°.</summary>
    private const float ArmGrenze = 35f;
    private double _armProbeZeit;
    private bool _armProbeFertig;

    private ArbeitsTritt? _arbeitsTritt;
    private bool _figurProbeGemeldet, _figurProbeDraussenGebaut;
    private Figur? _figurProbeDraussen;

    /// <summary>Tritt-Tabelle (game/daten/arbeitstritt.json), einmal geladen.</summary>
    private ArbeitsTritt Tritte => _arbeitsTritt ??= ArbeitsTritt.Laden(Godot.FileAccess.GetFileAsString("res://daten/arbeitstritt.json"));

    /// <summary>Meldet eine falsch geschriebene Option einmal ins Log.</summary>
    private void FigurProbeFehlerMelden()
    {
        if (_figurProbeGemeldet || FigurProbeText is null || FigurProbeOption is not null) return;
        _figurProbeGemeldet = true;
        GD.Print($"Prüfhilfe: --figurprobe={FigurProbeText} nicht verstanden – gemeint ist <volk>:<m|w>:<clip>, z. B. Zwerg:m:Hobeln (Clips: {string.Join(", ", FigurenBaukasten.FigurProbeClips)})");
    }

    /// <summary>Werkraum: Plätze so ordnen, dass der erste Arbeiter am Platz mit dem Clip der Prüfhilfe steht (sonst unverändert).</summary>
    private List<ModellPlatz> FigurProbePlaetze(List<ModellPlatz> plaetze, string betrieb)
    {
        if (FigurProbeOption is not { } p) return plaetze;
        var treffer = plaetze.FirstOrDefault(x => x.Bewegung == p.Clip);
        if (treffer is null)
        {
            GD.Print($"Prüfhilfe: {betrieb} hat keinen Platz mit {p.Clip} (Plätze: {string.Join(", ", plaetze.Select(x => x.Bewegung).Distinct())})");
            return plaetze;
        }
        return plaetze.Where(x => x == treffer).Concat(plaetze.Where(x => x != treffer)).ToList();
    }

    /// <summary>
    /// Wechselstube, Wachstube, Diebesversteck (eigene Figuren statt Raummodell-Arbeiter): unter --figurprobe Volk und Geschlecht der Option, die
    /// Bewegung bleibt die des Platzes (Schreiben, Rechnen, Zählen, Putzen …).
    /// </summary>
    private FigurAussehen FigurProbeAussehen(FigurAussehen a)
    {
        if (FigurProbeOption is not { } p) return a;
        GD.Print($"Prüfhilfe: {_raum?.Kennung ?? "Raum"} {p.Volk} {(p.Frau ? "w" : "m")} (Bewegung des Platzes; gewünscht {p.Clip})");
        return a with { Volk = p.Volk, Frau = p.Frau, Haut = HautVon(p.Volk) };
    }

    /// <summary>Rezept eines Arbeiters unter der Prüfhilfe: Volk und Geschlecht der Option, seine Rolle (Tracht) bleibt.</summary>
    private static FigurRezept FigurProbeRezept(FigurRezept r, int nummer) =>
        FigurProbeOption is { } p ? FigurenBaukasten.Statist($"Pruefhilfe:{nummer}", p.Volk, p.Frau, r.Rolle) : r;

    /// <summary>Kleine Völker: Figur auf den Hocker des Möbels stellen (Vor, Seitlich, Hoch aus der Tabelle) und den Hocker zeigen.</summary>
    private string FigurProbeTritt(Figur figur, FigurenBaukasten.FigurProbe p, string clip) => TrittAnwenden(figur, p.Volk, p.Frau, clip);

    /// <summary>
    /// Hocker und Standort der kleinen Völker am Arbeitsplatz (game/daten/arbeitstritt.json, ArbeitsTritt): rückt die Figur um Vor und Seitlich, hebt sie
    /// um Hoch und zeigt das Netz „Tritt_&lt;Möbel&gt;“. Für jede Arbeiterfigur mit Passanten-Skelett (Werkräume, seit dem Baukasten für alle).
    /// </summary>
    private string TrittAnwenden(Figur figur, Volk volk, bool frau, string clip)
    {
        var lage = Tritte.Lage(volk, frau, clip);
        if (lage.Vor == 0 && lage.Seitlich == 0 && !lage.MitTritt) return "ohne Hocker";
        figur.Modell.Position += new Vector3(lage.Seitlich, lage.Hoch, lage.Vor); // Modell blickt nach +Z, links ist +X
        if (lage.Netz is { } netz && figur.Werkzeuge.TryGetValue(netz, out var hocker)) hocker.Visible = true;
        return lage.MitTritt ? $"Hocker {lage.Moebel} {lage.Hoch:0.00} m, vor {lage.Vor:0.00} m, seitlich {lage.Seitlich:0.00} m" : $"ohne Hocker, vor {lage.Vor:0.00} m";
    }

    /// <summary>Meldet den ersten Arbeiter unter der Prüfhilfe.</summary>
    private void FigurProbeWerkraumMelden(string betrieb, ModellPlatz platz, string tritt)
    {
        if (FigurProbeOption is not { } p) return;
        GD.Print($"Prüfhilfe: {betrieb} (Raum {(platz.Raum == "" ? "Werk" : platz.Raum)}, Platz {platz.Nr}) {p.Volk} {(p.Frau ? "w" : "m")} {platz.Bewegung}, {tritt}");
    }

    /// <summary>Prüfhilfe --armprobe: einmal nach der Wartezeit im Raum die Oberarmwinkel aller sichtbaren Skelette mit Passanten-Knochen.</summary>
    private void ArmProbeBild(double delta)
    {
        if (ArmProbeNach is not { } nach || _armProbeFertig || _raum is null) return;
        _armProbeZeit += delta;
        if (_armProbeZeit < nach) return;
        _armProbeFertig = true;
        var werte = new List<float>();
        var hoch = new List<string>();
        foreach (var sk in GetTree().Root.FindChildren("*", nameof(Skeleton3D), true, false).OfType<Skeleton3D>())
        {
            if (!sk.IsVisibleInTree() || _innen?.Innen is not { } innen || !innen.IsAncestorOf(sk) || !IstImGezeigtenRaum(sk)) continue; // nur der gezeigte Raum (die anderen Räume stellt das Spiel erst, wenn man sie ansieht)
            var summe = 0f;
            var gezaehlt = 0;
            foreach (var seite in new[] { "Left", "Right" })
            {
                int o = sk.FindBone(seite + "UpperArm"), u = sk.FindBone(seite + "LowerArm");
                if (o < 0 || u < 0) continue;
                var a = sk.GlobalTransform * sk.GetBoneGlobalPose(o).Origin;
                var b = sk.GlobalTransform * sk.GetBoneGlobalPose(u).Origin;
                summe += Mathf.RadToDeg((b - a).Normalized().AngleTo(Vector3.Down));
                gezaehlt++;
            }
            if (gezaehlt != 2) continue;
            werte.Add(summe / 2);
            if (summe / 2 > ArmGrenze) // welcher Clip läuft dort (zum Finden der Ursache)?
            {
                var modell = sk.Owner as Node ?? sk.GetParent();
                var spieler = modell?.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>().FirstOrDefault();
                hoch.Add($"{modell?.GetParent()?.GetParent()?.Name}/{spieler?.AssignedAnimation}@{(spieler is null ? 0 : spieler.CurrentAnimationPosition):0.00}");
            }
        }
        var ueber = werte.Count(w => w > ArmGrenze);
        GD.Print($"Prüfhilfe: Armwinkel {_raum.Kennung}: {werte.Count} Figuren mit Passanten-Skelett, Mittel {(werte.Count > 0 ? werte.Average() : 0):0}°, "
                 + $"höchstens {(werte.Count > 0 ? werte.Max() : 0):0}°, über {ArmGrenze:0}°: {ueber} ({string.Join(", ", werte.Select(w => $"{w:0}°"))})"
                 + (hoch.Count > 0 ? $"; darüber: {string.Join(", ", hoch)}" : ""));
    }

    /// <summary>Draußen (ohne Innenraum): eine Figur am Blickpunkt (mit <c>--blick=x,z,zoom</c> genau dort), im Clip der Option.</summary>
    private void FigurProbeDraussenAktualisieren(double delta)
    {
        FigurProbeFehlerMelden();
        if (FigurProbeOption is not { } p || _raum is not null) return;
        if (!_figurProbeDraussenGebaut)
        {
            _figurProbeDraussenGebaut = true;
            var mitteBild = GetViewport().GetVisibleRect().Size / 2;
            var o = _kamera.ProjectRayOrigin(mitteBild);
            var r = _kamera.ProjectRayNormal(mitteBild);
            var welt = Mathf.Abs(r.Y) > 1e-4f ? o + r * (-o.Y / r.Y) : o;
            _passantenEbene ??= NeueEbene();
            var mitte = _passantenEbene.ToLocal(welt);
            if (Wasserarten.Startoption("blick")?.Split(',') is { Length: >= 2 } blick
                && float.TryParse(blick[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var bx)
                && float.TryParse(blick[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var bz))
                mitte = new Vector3(bx, 0, bz);
            var knoten = new Node3D { Name = "Pruefhilfe_Figur", Position = mitte };
            _passantenEbene.AddChild(knoten);
            var rezept = FigurenBaukasten.Statist("Pruefhilfe:draussen", p.Volk, p.Frau, p.Clip == "Hueten" ? "Hirte" : "Gast");
            _figurProbeDraussen = BaukastenFigurEinsetzen(knoten, rezept, p.Clip, p.Clip);
            var tritt = _figurProbeDraussen is { } f ? FigurProbeTritt(f, p, p.Clip) : "keine Figur";
            GD.Print($"Prüfhilfe: draußen ({mitte.X:0.0}, {mitte.Z:0.0}) {p.Volk} {(p.Frau ? "w" : "m")} {p.Clip} ({rezept.Rolle}), {tritt}");
        }
        if (_figurProbeDraussen is { } figur) FigurImBildBewegen(figur, null, delta);
    }
}
