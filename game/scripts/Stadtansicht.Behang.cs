using System;
using System.Linq;
using Godot;

namespace Reichsgilde;

// Wandbehang der Steinhaus-Stube (S3, Mensch und Zwerg): ganz leichte Feder im Luftzug (Nutzer 17.11.2026).
//
// Das Innenmodell (…_s3_g<n>_innen.glb) bringt das Skelett „Behang_Skelett“ mit: Wurzel „Behang“ an der Stange, fünf Ketten
// „Behang_<0..4>_<0..3>“ nach unten, Glied 3 ist der Saum. Die Feder (Godots SpringBoneSimulator3D, MIT) entsteht erst beim Betreten
// der Stube und stirbt mit ihr; sie läuft nur, solange die Stube gezeigt wird und das Spiel nicht pausiert. Nachts (außerhalb der
// Arbeitszeit) ruht der Luftzug, der Stoff pendelt aus. Oben hängt der Behang fest (die Wurzel jeder Kette wird nicht bewegt).
// Testoption: --behangfeder=0 schaltet die Feder aus (docs/STARTOPTIONEN.md).
public partial class Stadtansicht
{
    private static readonly bool BehangFederAn = Wasserarten.Startoption("behangfeder") != "0";

    /// <summary>Ketten und Glieder des Behangs im Modell.</summary>
    private const int BehangKetten = 5, BehangGlieder = 3;

    /// <summary>Zug zur Hängelage (hoch: nur ganz leichtes Schwingen), Dämpfung, Halbmesser der Gelenke und Stärke des Luftzugs.</summary>
    private const float BehangSteife = 1.6f, BehangDaempfung = 0.6f, BehangGelenk = 0.01f, BehangLuftzug = 0.08f;

    /// <summary>Baut die Feder am Skelett des Behangs unter <paramref name="wurzel"/>; null ohne Behang oder bei <c>--behangfeder=0</c>.</summary>
    private static SpringBoneSimulator3D? BehangFederBauen(Node3D wurzel)
    {
        if (!BehangFederAn) return null;
        if (wurzel.FindChildren("*", nameof(Skeleton3D), true, false).OfType<Skeleton3D>().FirstOrDefault(s => s.FindBone("Behang") >= 0) is not { } skelett) return null;
        var feder = new SpringBoneSimulator3D { Name = "BehangFeder", Active = false };
        skelett.AddChild(feder);
        feder.SettingCount = BehangKetten;
        for (var k = 0; k < BehangKetten; k++)
        {
            int wurzelKnochen = skelett.FindBone($"Behang_{k}_0"), ende = skelett.FindBone($"Behang_{k}_{BehangGlieder}");
            if (wurzelKnochen < 0 || ende < 0) { feder.SettingCount = k; break; }
            feder.SetRootBone(k, wurzelKnochen);
            feder.SetEndBone(k, ende);
            feder.SetRadius(k, BehangGelenk);
            feder.SetStiffness(k, BehangSteife);
            feder.SetDrag(k, BehangDaempfung);
            feder.SetGravity(k, 0f);
        }
        return feder;
    }

    /// <summary>Je Bild aus dem Raumzustand: Feder an, wenn die Stube läuft; der Luftzug wechselt langsam die Richtung und ruht nachts.</summary>
    private void BehangFederLaufen(SpringBoneSimulator3D? feder, bool arbeitszeit)
    {
        if (feder is null || !IsInstanceValid(feder)) return;
        var laufen = NeubauInnenAktiv && _simulation is { Takt.IstPausiert: false };
        if (feder.Active != laufen) { feder.Active = laufen; if (laufen) feder.Reset(); }
        if (!laufen) return;
        var t = (float)_animationszeit;
        var staerke = arbeitszeit ? BehangLuftzug : 0f;
        feder.ExternalForce = new Vector3(Mathf.Sin(t * 0.55f), 0f, Mathf.Sin(t * 0.37f + 1.3f)) * staerke;
    }
}
