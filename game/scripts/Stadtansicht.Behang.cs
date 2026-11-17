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

    /// <summary>Kantenlänge einer Kachel der Stofftextur in Metern (Dreiebenen-Abbildung im Objektraum, kein Umbau des Modells nötig).</summary>
    private const float BehangKachel = 0.9f;

    /// <summary>Schärfe der Dreiebenen-Mischung: hoch, damit auf dem schwingenden Tuch nur die Ebene der Wand zählt und das Gewebe nicht verläuft.</summary>
    private const float BehangKachelSchaerfe = 32f;

    private static Texture2D? _stoffAlbedo, _stoffNormal, _stoffRauheit;

    /// <summary>
    /// Gibt dem Stoff des Behangs (und Saums) die Webstruktur: graue Albedo (wird mit dem gedämpften Hausfarbton aus
    /// <c>Behangfarbe</c> malgenommen), Normal- und Rauheitskarte, alles aus ambientCG „Fabric 045“ (CC0, grob gewebter Wollstoff). Die Textur liegt im Objektraum
    /// des Behangs (Dreiebenen-Abbildung), unabhängig von den UV des Modells. Die Farbe setzt der Aufrufer danach über <c>AlbedoColor</c>.
    /// </summary>
    private static void BehangStoffGeben(StandardMaterial3D stoff)
    {
        _stoffAlbedo ??= GD.Load<Texture2D>("res://assets/texturen/stoff/stoff_albedo_grau.png");
        _stoffNormal ??= GD.Load<Texture2D>("res://assets/texturen/stoff/stoff_normal.png");
        _stoffRauheit ??= GD.Load<Texture2D>("res://assets/texturen/stoff/stoff_rauheit.png");
        stoff.AlbedoTexture = _stoffAlbedo;
        stoff.NormalEnabled = true;
        stoff.NormalTexture = _stoffNormal;
        stoff.NormalScale = 1.5f;
        stoff.RoughnessTexture = _stoffRauheit;
        stoff.RoughnessTextureChannel = BaseMaterial3D.TextureChannel.Red;
        stoff.Roughness = 1f;
        stoff.Uv1Triplanar = true;
        stoff.Uv1WorldTriplanar = false;
        stoff.Uv1TriplanarSharpness = BehangKachelSchaerfe;
        stoff.Uv1Scale = Vector3.One / BehangKachel;
        stoff.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
    }

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
