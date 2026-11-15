using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

// ═════════════════════════════════════════════════════════════════════════════
//  Figuren-Baukasten, Godot-Seite: aus dem Rezept (FigurenBaukasten.cs) die Figur bauen
//
//  Das Rezept ist die eine Quelle für das Aussehen. Gebaut wird mit dem Passanten-Skelett (Probe-Rig, Stadtansicht.Passantenprobe.cs),
//  wo dessen Bewegungen reichen (Stehen, Sitzen, Reden …); Arbeitsbewegungen (Hämmern, Schreiben, Beten, Laute …) gibt es vorerst nur
//  im alten Rig (figur_<volk>.glb) – dort trägt die Figur dasselbe Rezept. Innenraumfiguren bleiben immer beim Skelett (keine VAT).
// ═════════════════════════════════════════════════════════════════════════════

public partial class Stadtansicht
{
    /// <summary>Größe eines Kindes gegenüber einem Erwachsenen seines Volks (wie bisher in der Wohnstube).</summary>
    private const float KindGroesse = 0.68f;

    private static Color Farbe(Farbwert f) => new(f.R, f.G, f.B);

    /// <summary>
    /// Das Aussehen (Farben und Tracht) aus dem Rezept: Gewand aus dem Katalog oder nach der Figurnummer aus der Palette, Haar wie im Rezept (grau im
    /// Alter; „kahl“ aus der Palette des Volks), Haut nach Volk.
    /// </summary>
    private static FigurAussehen AussehenAusRezept(FigurRezept r)
    {
        var n = (long)r.Nummer;
        var gewand = r.Tracht.Gewand is { } g ? Farbe(g) : Gewandfarben[n * 7 % Gewandfarben.Length];
        var haare = Haarfarben(r.Volk);
        var haar = r.Grau ? GrauesHaar : r.Haar == Haarfarbe.Kahl ? haare[n * 5 % haare.Length] : PortraetAnzeige.Farbe(r.Haar);
        return new FigurAussehen(r.Volk, r.Frau, gewand, Beinkleidfarben[n * 3 % Beinkleidfarben.Length], HautVon(r.Volk), haar,
            r.Tracht.Kopffarbe is { } k ? Farbe(k) : gewand, r.Tracht.Kopf, r.Tracht.Last, r.Tracht.Schuerze);
    }

    /// <summary>Setzt eine Figur mit Passanten-Skelett nach dem Rezept ein (das Erbgut des Rezepts gilt für Bart und Bartton); Kinder kleiner.</summary>
    private Figur? BaukastenProbeEinsetzen(Node3D eltern, FigurRezept r, bool innen)
    {
        _probeErbgut[(r.Volk, r.Nummer)] = r.Erbgut;
        var figur = ProbefigurEinsetzen(eltern, AussehenAusRezept(r), r.Nummer, r.Rolle, innen);
        if (figur is not null && r.Kind) KindVerkleinern(figur);
        return figur;
    }

    /// <summary>Ein Kind ist kleiner; seine Kniehöhe (für das Sitzen) rechnet <see cref="InnenFigurBauen"/> mit demselben Faktor.</summary>
    private static void KindVerkleinern(Figur f) => f.Modell.Scale *= KindGroesse;

    /// <summary>
    /// Eine Figur aus dem Baukasten: mit Passanten-Skelett und <paramref name="probeBewegung"/>, wenn es die Passantenfiguren gibt und die Bewegung
    /// dort vorkommt (null: nur im alten Rig), sonst im alten Rig mit <paramref name="bewegung"/>. Das Aussehen ist in beiden Fällen das des Rezepts.
    /// </summary>
    /// <param name="vorderkante">Beim Sitzen: Abstand der Sitzvorderkante von der Sitzmitte (Meter), für das Vorrücken kleiner Völker.</param>
    private Figur? BaukastenFigurEinsetzen(Node3D eltern, FigurRezept r, string bewegung, string? probeBewegung, float takt = 1, float vorderkante = 0.30f)
    {
        Figur? figur;
        if (ProbeAktiv && probeBewegung is not null)
        {
            figur = BaukastenProbeEinsetzen(eltern, r, innen: true);
            if (figur is not null)
            {
                BewegungSetzen(figur, probeBewegung, takt);
                if (probeBewegung == "Sitzen") SitzVersatz(figur, vorderkante);
            }
        }
        else
        {
            figur = FigurEinsetzen(eltern, AussehenAusRezept(r), r.Nummer);
            if (figur is not null)
            {
                if (r.Kind) KindVerkleinern(figur);
                BewegungSetzen(figur, bewegung, takt);
            }
        }
        if (figur is not null && r.Tracht.Amtskette) AmtsketteAnlegen(figur);
        return figur;
    }

    /// <summary>
    /// Höhe des Modells über dem Boden des Platzes für eine Sitzfläche dieser Höhe. Passanten-Skelett: Gesäß (Sitzhöhe des Clips × Maßstab) mit 1 cm Luft
    /// auf der Sitzfläche, Füße nie unter dem Boden (<see cref="FigurenBaukasten.SitzUrsprung"/>). Altes Rig: wie bisher Sitzfläche − (Kniehöhe − 0,02).
    /// </summary>
    private static float SitzHoehe(Figur f, float sitzflaeche, bool kind = false)
    {
        var knie = f.Kniehoehe * (kind ? KindGroesse : 1);
        return f.Probe ? FigurenBaukasten.SitzUrsprung(sitzflaeche, knie - 0.02f * f.Modell.Scale.Y) : sitzflaeche - (knie - 0.02f);
    }

    /// <summary>Hüftgelenk des Clips „Sitzen“ (Bild 0) im Modellraum ohne Maßstab, je Modelldatei einmal gerechnet.</summary>
    private static readonly Dictionary<string, Vector3> _sitzHuefte = new();

    /// <summary>
    /// Sitzregel des Baukastens: Das Hüftgelenk der Pose „Sitzen“ kommt über den Sitzpunkt (Platz bzw. Anker) – wie beim alten Rig, dessen Hüfte im
    /// Sitzen über dem Ursprung liegt. Beim Passanten-Skelett liegt sie 23–30 cm hinter dem Ursprung; ohne Versatz säße die Figur in Lehne und Bank.
    /// Gerechnet aus den Spuren „Root“ und „Hips“ der Animation (ohne Bildschirm, ohne Pose abzuwarten).
    /// </summary>
    private static void SitzVersatz(Figur f, float vorderkante)
    {
        var datei = f.Modell.SceneFilePath;
        if (!_sitzHuefte.TryGetValue(datei, out var huefte))
        {
            huefte = Vector3.Zero;
            var skelett = f.Modell.FindChildren("*", nameof(Skeleton3D), true, false).OfType<Skeleton3D>().FirstOrDefault();
            if (skelett is not null && f.Spieler.HasAnimation("Sitzen"))
            {
                var anim = f.Spieler.GetAnimation("Sitzen");
                Transform3D Knochen(string name)
                {
                    var k = skelett.FindBone(name);
                    var lage = k >= 0 ? skelett.GetBoneRest(k) : Transform3D.Identity;
                    var basis = lage.Basis.GetRotationQuaternion();
                    var skala = lage.Basis.Scale;
                    var ort = lage.Origin;
                    for (var i = 0; i < anim.GetTrackCount(); i++)
                    {
                        if (anim.TrackGetPath(i).GetConcatenatedSubNames() != name) continue;
                        switch (anim.TrackGetType(i))
                        {
                            case Animation.TrackType.Position3D: ort = anim.PositionTrackInterpolate(i, 0); break;
                            case Animation.TrackType.Rotation3D: basis = anim.RotationTrackInterpolate(i, 0); break;
                            case Animation.TrackType.Scale3D: skala = anim.ScaleTrackInterpolate(i, 0); break;
                        }
                    }
                    return new Transform3D(new Basis(basis).Scaled(skala), ort);
                }
                var imSkelett = (Knochen("Root") * Knochen("Hips")).Origin;
                // vom Skelett zum Modell: die Knoten dazwischen
                Node? n = skelett;
                var lageImModell = Transform3D.Identity;
                while (n is Node3D k3 && n != f.Modell)
                {
                    lageImModell = k3.Transform * lageImModell;
                    n = n.GetParent();
                }
                huefte = lageImModell * imSkelett;
                GD.Print($"[baukasten] Sitzen {datei.GetFile()}: Hüftgelenk x {huefte.X:0.000} z {huefte.Z:0.000} m vom Ursprung");
            }
            _sitzHuefte[datei] = huefte;
        }
        var s = f.Modell.Scale.X;
        // kleine Völker weiter vor (das Schienbein bleibt vor der Sitzkante, FigurenBaukasten.VorrueckenFuer); das Modell blickt nach +Z
        f.Modell.Position -= new Vector3(huefte.X * s, 0, huefte.Z * s - FigurenBaukasten.VorrueckenFuer(datei, vorderkante));
    }

    private static StandardMaterial3D? _amtsgold;

    /// <summary>
    /// Die Amtskette des Bürgermeisters: ein goldener Kettenring um den Halsansatz, vorn tiefer auf der Brust, daran ein Schaustück. Hängt am Brustknochen
    /// (Passanten-Skelett „UpperChest“, altes Rig „Ruecken“) und folgt so jeder Bewegung; Maße aus der Ruhelage des Halsknochens.
    /// </summary>
    private static void AmtsketteAnlegen(Figur f)
    {
        var skelett = f.Modell.FindChildren("*", nameof(Skeleton3D), true, false).OfType<Skeleton3D>().FirstOrDefault();
        if (skelett is null) return;
        var brust = new[] { "UpperChest", "Chest", "Ruecken" }.Select(skelett.FindBone).FirstOrDefault(i => i >= 0, -1);
        var hals = new[] { "Neck", "Kopf" }.Select(skelett.FindBone).FirstOrDefault(i => i >= 0, -1);
        if (brust < 0 || hals < 0) return;
        _amtsgold ??= new StandardMaterial3D { AlbedoColor = Farbe(FigurenBaukasten.Amtsgold), Metallic = 0.8f, Roughness = 0.35f };
        var halter = new BoneAttachment3D { BoneName = skelett.GetBoneName(brust) };
        skelett.AddChild(halter);
        // Lage im Skelettraum (vorn ist +Z, oben +Y): Ring um den Halsansatz, 25° nach vorn geneigt; Schaustück vorn unten
        var halsansatz = skelett.GetBoneGlobalRest(hals).Origin;
        var zurBrust = skelett.GetBoneGlobalRest(brust).AffineInverse();
        var neigung = new Basis(Vector3.Right, 0.45f);
        var ring = new Transform3D(neigung, halsansatz + new Vector3(0, -0.035f, 0.015f));
        halter.AddChild(new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.095f, OuterRadius = 0.115f, Rings = 36, RingSegments = 8 },
            MaterialOverride = _amtsgold, Transform = zurBrust * ring,
        });
        var stueck = new Transform3D(new Basis(Vector3.Right, Mathf.Pi / 2), ring * new Vector3(0, -0.012f, 0.112f) + new Vector3(0, -0.045f, 0.012f));
        halter.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.038f, BottomRadius = 0.038f, Height = 0.012f, RadialSegments = 16 },
            MaterialOverride = _amtsgold, Transform = zurBrust * stueck,
        });
    }

    /// <summary>
    /// Eine Figur des Baukastens in einem Raum aus Grundkörpern (Wohnstube, Kontor, Ratssaal, Dom). Sie gibt sich als <see cref="Arbeiterfigur"/>
    /// aus, damit der Raum sie wie die übrigen Besucher zeigt und wegräumt; der Arm ist ein leerer Knoten, denn das Modell bewegt sich selbst.
    /// Wer sitzt (<paramref name="sitzhoehe"/>), hat die Hüfte auf der Sitzfläche (kleine Völker sitzen höher, Kinder tiefer).
    /// </summary>
    private Arbeiterfigur InnenFigurBauen(InnenAnsicht a, Arbeitsplatz platz, FigurRezept r, string bewegung, string? probeBewegung, float? sitzhoehe = null,
        float hoehe = 0, float vorderkante = 0.30f, Node3D? eltern = null, Vector3? mitte = null)
    {
        // Die Plätze blicken bei Drehung 0 nach −Z, die Modelle nach +Z: eine halbe Drehung mehr (Konvention des Baukastens).
        // eltern/mitte: ein anderer Elternknoten (Wohnstube im neu gebauten Haus, Orte dort ohne a.Mitte)
        var koerper = new Node3D { Position = (mitte ?? a.Mitte) + platz.Ort + new Vector3(0, hoehe, 0), Rotation = new Vector3(0, FigurenBaukasten.Modelldrehung(platz.Drehung), 0) };
        (eltern ?? a.Innen!).AddChild(koerper);
        var arm = new Node3D();
        koerper.AddChild(arm);
        if (BaukastenFigurEinsetzen(koerper, r, bewegung, probeBewegung, 0.9f + r.Nummer % 7 * 0.04f, vorderkante) is { } figur)
        {
            if (sitzhoehe is { } s) koerper.Position += new Vector3(0, SitzHoehe(figur, s, r.Kind), 0);
            _sitzende[koerper] = figur;
        }
        return new Arbeiterfigur(koerper, arm, r.Nummer % 97 * 0.7f, Bewegung.Stehen, Vector3.Zero);
    }

    /// <summary>
    /// Rezept eines Ratsmitglieds: ein Haus zeigt sein Oberhaupt (die echte Person, in der Farbe des Hauses), ein Ratsherr der Stadt sich selbst
    /// (Volk, Geschlecht und Haar aus den Daten „politik“). Der Bürgermeister trägt die Amtskette statt einer Kopfbedeckung (<see cref="FigurenBaukasten.Amtstracht"/>).
    /// </summary>
    private FigurRezept RatsRezept(Stadtwirtschaft w, string name, Konto? haus, bool buergermeister)
    {
        FigurRezept r;
        if (haus is not null && w.Familien.Von(haus)?.Oberhaupt is { } oberhaupt)
        {
            var farbe = Besitzerfarbe(haus);
            r = FigurenBaukasten.Von(oberhaupt, _simulation?.Zeit.Jahr ?? 0, true, true, new Farbwert(farbe.R, farbe.G, farbe.B));
        }
        else
        {
            var daten = w.Rat.Ratsherren.FirstOrDefault(x => x.Name == name)?.Daten;
            r = FigurenBaukasten.Ratsherr(name, daten?.Volk ?? Volk.Mensch, daten?.Geschlecht, daten?.Aussehen?.Haar);
        }
        return buergermeister ? r with { Tracht = FigurenBaukasten.Amtstracht(r.Tracht) } : r;
    }

    /// <summary>Protokollzeile, wie viele Figuren ein Raum aus dem Baukasten gebaut hat (zum Messen und für den Klicktest).</summary>
    private static void BaukastenMelden(string raum, int anzahl) =>
        GD.Print($"[baukasten] {raum}: Figuren: {anzahl} Personen aus Baukasten");
}
