using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Reichsgilde;

// ═════════════════════════════════════════════════════════════════════════════
//  Stoff mit Federketten: Rock, Kleid und Schürze der Frauenfiguren (Auftrag 15.11.2026)
//
//  Die vier Frauen-Dateien haben 8 Bahnen x 3 Glieder Kettenknochen unter „Hips“ (tools/modelle/passanten_stoff.py). Jeder Clip enthält
//  ihre vorberechnete Bewegung als Knochenspuren: Das spielt die mittlere Stufe ab, die VAT (Fern) backt es mit.
//  Nah legt Godots eingebauter SpringBoneSimulator3D (Teil der Engine, MIT) darüber: Er zieht jedes Glied zur gebackenen Haltung
//  (ohne Schwerkraft – nichts sackt durch den Sitz), lässt es nachschwingen und hält es mit Kapseln an Oberschenkel und Schienbein
//  und einer Kugel am Gesäß aus den Beinen. Nur die nächsten FigurenLod.StoffFederMax Figuren bekommen eine Feder; der Knoten
//  entsteht erst dann (ein Skelett mit Modifier rechnet jedes Bild, auch wenn der Modifier ruht).
//  Clipwechsel, Platzwechsel und Einschalten beginnen die Feder aus der gebackenen Haltung neu (kein Zittern, kein Ausschlag).
// ═════════════════════════════════════════════════════════════════════════════

public partial class Stadtansicht
{
    /// <summary>Federketten einer Figur mit Rock: Skelett, die Feder (nur solange sie zu den nächsten gehört) und der Merker „neu beginnen“.</summary>
    private sealed class StoffFederung
    {
        public required Skeleton3D Skelett { get; init; }
        public bool Innen { get; init; }
        public SpringBoneSimulator3D? Feder { get; set; }
        /// <summary>Nach dem nächsten Stellen der Pose neu beginnen (Clipwechsel).</summary>
        public bool NeuBeginnen { get; set; }
        public Vector3 Lage { get; set; }
    }

    /// <summary>Alle Figuren mit Kettenknochen (Passantinnen draußen, Frauen in Innenräumen).</summary>
    private readonly List<Figur> _stoffFiguren = new();
    private readonly List<float> _stoffAbstand = new();
    private readonly List<bool> _stoffBisher = new();

    /// <summary>Messhilfe (docs/STARTOPTIONEN.md): --stofffeder=&lt;n&gt; setzt die Obergrenze gleichzeitig federnder Figuren (0 = keine Feder, nur gebacken).</summary>
    private static readonly int StoffFederGrenze =
        int.TryParse(Wasserarten.Startoption("stofffeder"), out var n) && n >= 0 ? n : FigurenLod.StoffFederMax;

    /// <summary>Beim Bau einer Probefigur: Hat das Skelett Kettenknochen, die Figur für die Federwahl merken.</summary>
    private void StoffAnlegen(Figur figur, Node3D modell, bool innen)
    {
        var skelett = modell.FindChildren("*", nameof(Skeleton3D), true, false).OfType<Skeleton3D>().FirstOrDefault();
        if (skelett is null || skelett.FindBone(FigurenLod.StoffKnochen(0, 0)) < 0) return;
        figur.Stoff = new StoffFederung { Skelett = skelett, Innen = innen };
        _stoffFiguren.Add(figur);
    }

    /// <summary>Clipwechsel: Die Feder beginnt nach dem nächsten Stellen der Pose neu (<see cref="StoffNachPose"/>).</summary>
    private static void StoffClipwechsel(Figur f)
    {
        if (f.Stoff is { } s) s.NeuBeginnen = true;
    }

    /// <summary>Nach <c>Seek</c>: Die Knochen stehen in der gebackenen Haltung des neuen Clips, von dort beginnt die Feder.</summary>
    private static void StoffNachPose(Figur f)
    {
        if (f.Stoff is not { NeuBeginnen: true } s) return;
        s.NeuBeginnen = false;
        if (s.Feder is { } feder && IsInstanceValid(feder)) feder.Reset();
    }

    /// <summary>Einmal je Bild: die nächsten nahen Figuren mit Rock bekommen eine Feder, alle anderen geben sie ab.</summary>
    private void StoffFedernWaehlen()
    {
        if (_stoffFiguren.Count == 0) return;
        _stoffFiguren.RemoveAll(f => f.Stoff is null || !IsInstanceValid(f.Modell) || !IsInstanceValid(f.Stoff.Skelett));
        _stoffAbstand.Clear();
        _stoffBisher.Clear();
        var ego = _ego is { } e ? e.GlobalPosition : (Vector3?)null;
        var auge = _kamera.GlobalPosition;
        var blick = -_kamera.GlobalBasis.Z;
        foreach (var f in _stoffFiguren)
        {
            var s = f.Stoff!;
            var nah = f.Modell.IsInsideTree() && f.Modell.IsVisibleInTree() && (s.Innen || f.Finger is not { Stufe: not FigurStufe.Nah });
            var abstand = -1f;
            if (nah)
            {
                var ort = f.Modell.GlobalPosition;
                if (ego is { } a) abstand = (ort - a).Length();
                else
                {
                    // schräge Parallelkamera: Abstand zur Blickachse (Bildmitte), nicht zur weit entfernten Kamera
                    var v = ort - auge;
                    abstand = (v - blick * v.Dot(blick)).Length();
                }
                if (FigurenLod.StoffNeuBeginnen((ort - s.Lage).Length())) s.NeuBeginnen = true; // Platzwechsel, Raum betreten
                s.Lage = ort;
            }
            _stoffAbstand.Add(abstand);
            _stoffBisher.Add(s.Feder is not null);
        }
        var wahl = FigurenLod.StoffWaehlen(_stoffAbstand, _stoffBisher, StoffFederGrenze);
        for (var i = 0; i < _stoffFiguren.Count; i++)
        {
            var s = _stoffFiguren[i].Stoff!;
            if (wahl[i] && s.Feder is null)
            {
                s.Feder = StoffFederBauen(s.Skelett);
                _stoffFiguren[i].Neu = true; // gleich die Pose stellen: Die Feder beginnt aus der gebackenen Haltung
            }
            else if (!wahl[i] && s.Feder is { } feder)
            {
                if (IsInstanceValid(feder)) feder.QueueFree();
                s.Feder = null;
                _stoffFiguren[i].Neu = true; // zurück auf die gebackene Haltung
            }
            else if (wahl[i] && s.NeuBeginnen && !_stoffFiguren[i].Neu && s.Feder is { } laufend && IsInstanceValid(laufend))
            {
                laufend.Reset(); // Sprung ohne Clipwechsel (die Pose steht schon)
                s.NeuBeginnen = false;
            }
        }
    }

    /// <summary>Für die Messzeile: Figuren mit Rock und wie viele davon gerade federn.</summary>
    private string StoffZaehlen() => $"Stoff {_stoffFiguren.Count(f => f.Stoff?.Feder is not null)} von {_stoffFiguren.Count} mit Feder";

    /// <summary>Feder mit acht Bahnen und den Kollisionskörpern der Beine als Kind des Skeletts.</summary>
    private static SpringBoneSimulator3D StoffFederBauen(Skeleton3D skelett)
    {
        var feder = new SpringBoneSimulator3D { Name = "StoffFeder" };
        skelett.AddChild(feder);
        feder.SettingCount = FigurenLod.StoffBahnen;
        for (var b = 0; b < FigurenLod.StoffBahnen; b++)
        {
            feder.SetRootBone(b, skelett.FindBone(FigurenLod.StoffKnochen(b, 0)));
            feder.SetEndBone(b, skelett.FindBone(FigurenLod.StoffKnochen(b, FigurenLod.StoffGlieder)));
            feder.SetRadius(b, FigurenLod.StoffGelenk);
            feder.SetStiffness(b, FigurenLod.StoffSteife);
            feder.SetDrag(b, FigurenLod.StoffDaempfung);
            feder.SetGravity(b, 0f);
            feder.SetEnableAllChildCollisions(b, true);
        }
        foreach (var seite in new[] { "Left", "Right" })
        {
            StoffKapsel(feder, skelett, seite + "UpperLeg", seite + "LowerLeg", FigurenLod.StoffSchenkel);
            StoffKapsel(feder, skelett, seite + "LowerLeg", seite + "Foot", FigurenLod.StoffSchienbein);
        }
        var huefte = skelett.FindBone("Hips");
        if (huefte >= 0)
            feder.AddChild(new SpringBoneCollisionSphere3D { Name = "Gesaess", Bone = huefte, Radius = FigurenLod.StoffGesaess });
        return feder;
    }

    /// <summary>Kapsel entlang eines Beinknochens (von seinem Gelenk bis zum Gelenk des Kindes; die Knochen zeigen mit +Y zum Kind).</summary>
    private static void StoffKapsel(SpringBoneSimulator3D feder, Skeleton3D skelett, string knochen, string kind, float halbmesser)
    {
        int von = skelett.FindBone(knochen), bis = skelett.FindBone(kind);
        if (von < 0 || bis < 0) return;
        var versatz = skelett.GetBoneRest(bis).Origin;
        var laenge = versatz.Length();
        if (laenge < 0.001f) return;
        feder.AddChild(new SpringBoneCollisionCapsule3D
        {
            Name = "Bein_" + knochen, Bone = von, Radius = halbmesser, Height = laenge + 2f * halbmesser,
            PositionOffset = versatz * 0.5f, RotationOffset = new Quaternion(Vector3.Up, versatz / laenge),
        });
    }
}
