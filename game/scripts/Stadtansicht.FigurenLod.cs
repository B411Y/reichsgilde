using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Reichsgilde;

// ═════════════════════════════════════════════════════════════════════════════
//  Fingerknochen der Passanten-Figuren in drei Stufen (Nutzerentscheidung 14.11.2026, „Finger C“; Stufenwahl ohne Godot: FigurenLod.cs)
//
//    Nah     volles Skelett: die Finger folgen den Clips (Griffe in den Arbeitsbewegungen)
//    Mittel  die 20 Fingerplätze der Haut zeigen auf den Handknochen (mit dessen Bindung): Die Hand ist die halb geschlossene Ruhehand und
//            folgt starr der Hand; die Fingerknochen wirken nicht. Kein zweites Netz: je Netz der Datei EINE zweite Haut, geteilt.
//    Fern    VAT wie bisher (Stadtansicht.Passantenprobe.cs); die Finger sind dort in der Ruhelage mitgebacken.
//  Wechsel Nah ↔ Mittel weich: Das Fingergewicht läuft in FigurenLod.Ueberblendung Sekunden zwischen Griff und Ruhehand (die Fingerknochen
//  werden dazu nach dem Abspielen zur Ruhelage gemischt); erst bei Gewicht 0 wird die Faust-Haut eingesetzt – gleiche Pose, kein Sprung.
// ═════════════════════════════════════════════════════════════════════════════

public partial class Stadtansicht
{
    /// <summary>Fingerstufe einer Figur: Häute (voll/Faust), Fingerknochen mit Ruhedrehung, Stufe und Fingergewicht.</summary>
    private sealed class FingerStufe
    {
        public required Skeleton3D Skelett { get; init; }
        public required List<(MeshInstance3D Teil, Skin Voll, Skin Faust)> Haeute { get; init; }
        public required int[] Knochen { get; init; }
        public required Quaternion[] Ruhe { get; init; }
        public bool Innen { get; init; }
        public FigurStufe Stufe { get; set; } = FigurStufe.Nah;
        public float Gewicht { get; set; } = 1f;
        public bool Faust { get; set; }
    }

    /// <summary>Faust-Haut je voller Haut (die Häute einer .glb teilen sich alle Figuren derselben Datei).</summary>
    private static readonly Dictionary<Skin, Skin> _faustHaeute = new();

    /// <summary>Alle Figuren mit Fingerknochen (Passanten draußen, Personen in Innenräumen).</summary>
    private readonly HashSet<Figur> _fingerFiguren = new();

    private static readonly string[] FingerNamen = { "Thumb", "Index", "Middle", "Ring", "Little" };

    private static bool IstFinger(string knochen) =>
        (knochen.StartsWith("Left") || knochen.StartsWith("Right")) && FingerNamen.Any(f => knochen.Contains(f));

    /// <summary>Haut, deren Fingerplätze auf die Hand zeigen (Knochen und Bindung der Hand): Die Finger bleiben in ihrer Ruhelage an der Hand.</summary>
    private static Skin FaustHaut(Skin voll, Skeleton3D skelett)
    {
        if (_faustHaeute.TryGetValue(voll, out var faust)) return faust;
        faust = (Skin)voll.Duplicate();
        string Name(int b) => voll.GetBindBone(b) >= 0 ? skelett.GetBoneName(voll.GetBindBone(b)) : voll.GetBindName(b).ToString();
        var plaetze = Enumerable.Range(0, voll.GetBindCount()).ToDictionary(Name, b => b);
        for (var b = 0; b < voll.GetBindCount(); b++)
        {
            var name = Name(b);
            if (!IstFinger(name)) continue;
            var hand = name.StartsWith("Left") ? "LeftHand" : "RightHand";
            if (!plaetze.TryGetValue(hand, out var h)) continue;
            faust.SetBindName(b, hand);
            faust.SetBindBone(b, voll.GetBindBone(h) >= 0 ? voll.GetBindBone(h) : skelett.FindBone(hand));
            faust.SetBindPose(b, voll.GetBindPose(h));
        }
        _faustHaeute[voll] = faust;
        return faust;
    }

    /// <summary>Beim Bau einer Probefigur: Fingerknochen und Häute merken (Dateien ohne Finger: nichts).</summary>
    private void FingerStufeAnlegen(Figur figur, Node3D modell, bool innen)
    {
        var skelett = modell.FindChildren("*", nameof(Skeleton3D), true, false).OfType<Skeleton3D>().FirstOrDefault();
        if (skelett is null) return;
        var knochen = Enumerable.Range(0, skelett.GetBoneCount()).Where(i => IstFinger(skelett.GetBoneName(i))).ToArray();
        if (knochen.Length == 0) return;
        var haeute = figur.Teile.Where(t => t.Skin is not null && IsInstanceValid(t)).Select(t => (t, t.Skin!, FaustHaut(t.Skin!, skelett))).ToList();
        figur.Finger = new FingerStufe
        {
            Skelett = skelett, Haeute = haeute, Knochen = knochen,
            Ruhe = knochen.Select(i => skelett.GetBoneRest(i).Basis.GetRotationQuaternion()).ToArray(), Innen = innen,
        };
        _fingerFiguren.Add(figur);
    }

    /// <summary>Einmal je Bild: Stufe jeder Figur mit Fingern wählen und das Fingergewicht weiterzählen; bei Gewicht 0 die Faust-Haut.</summary>
    private void FingerStufenWaehlen(double sekunden)
    {
        if (_fingerFiguren.Count == 0) return;
        var auge = _ego is { } ego ? ego.GlobalPosition : (Vector3?)null;
        // Messhilfe (docs/STARTOPTIONEN.md): --fingerstufe=nah|mittel erzwingt die Stufe der Figuren draußen, zum Vergleich der Bildzeit
        var erzwungen = Wasserarten.Startoption("fingerstufe") switch { "nah" => FigurStufe.Nah, "mittel" => FigurStufe.Mittel, _ => (FigurStufe?)null };
        List<Figur>? weg = null;
        foreach (var f in _fingerFiguren)
        {
            if (f.Finger is not { } s || !IsInstanceValid(f.Modell)) { (weg ??= new()).Add(f); continue; }
            float? abstand = auge is { } a && f.Modell.IsInsideTree() ? (f.Modell.GlobalPosition - a).Length() : null;
            s.Stufe = erzwungen is { } e && !s.Innen && _kamera.Size < FigurenHalbeRateAb ? e
                : FigurenLod.Waehlen(s.Stufe, s.Innen, _kamera.Size, FigurenHalbeRateAb, abstand);
            s.Gewicht = FigurenLod.FingerGewicht(s.Gewicht, s.Stufe, (float)sekunden);
            var faust = s.Gewicht <= 0f;
            if (faust == s.Faust) continue;
            s.Faust = faust;
            foreach (var (teil, voll, faustHaut) in s.Haeute)
                if (IsInstanceValid(teil)) teil.Skin = faust ? faustHaut : voll;
            f.Neu = true;
        }
        if (weg is not null) foreach (var f in weg) _fingerFiguren.Remove(f);
    }

    /// <summary>Für die Messzeile: wie viele Figuren mit Fingern gerade nah (Finger gerechnet) und mittel (Faust) stehen.</summary>
    private string FingerStufenZaehlen() =>
        $"Finger nah {_fingerFiguren.Count(f => f.Finger is { Faust: false })}, Faust {_fingerFiguren.Count(f => f.Finger is { Faust: true })}";

    /// <summary>Nach dem Abspielen: Während der Überblendung die Fingerknochen zwischen Ruhelage und Clip mischen.</summary>
    private static void FingerMischen(Figur f)
    {
        if (f.Finger is not { } s || s.Gewicht >= 1f || s.Faust) return;
        for (var k = 0; k < s.Knochen.Length; k++)
        {
            var i = s.Knochen[k];
            s.Skelett.SetBonePoseRotation(i, s.Ruhe[k].Slerp(s.Skelett.GetBonePoseRotation(i), s.Gewicht));
        }
    }
}
