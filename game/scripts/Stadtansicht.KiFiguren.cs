using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Dynastie;

namespace Reichsgilde;

/// <summary>
/// KI-Schmied aus TRELLIS (Teil der <c>partial class Stadtansicht</c>): ein Modell aus einem Bild (TRELLIS.2), auf 20 000 Dreiecke und eine
/// 2048er-Textur gebracht (AssetFurnace), mit Skelett (SkinTokens) und Gehen-Animation (Kimodo). Es ersetzt die Arbeiter in der Schmiede
/// und einen Teil der männlichen Handwerker-Passanten (Knechte, Tagelöhner) in der Stadt.
///
/// Anders als die Figuren aus Blender hat es kein Zonen-Material (die Farben sind in die Textur gebacken) und kein Trachtzubehör; es nutzt aber
/// dieselbe <see cref="Figur"/>-Hülle, damit Passanten, Innenraum, Hervorheben und Rechenzeit-Regeln unverändert gelten.
///
/// Bewegungen: Eine Datei <c>schmied_ki.glb</c> bringt Mesh, Skelett und die Gehen-Animation. Weitere Clips (Stehen, Hämmern …) kommen als
/// eigene Dateien <c>schmied_ki_&lt;bewegung&gt;.glb</c> mit demselben Skelett in denselben Ordner – sie werden beim ersten Einsetzen automatisch
/// in die Animationsbibliothek aufgenommen (Namen siehe <see cref="KiKlipNamen"/>). Fehlt ein Clip, steht die Figur still (Pose aus dem Anfang der Gehen-Datei).
/// </summary>
public partial class Stadtansicht
{
    private const string KiSchmiedDatei = "figuren/ki/schmied_ki";
    private const string KiSchmiedOrdner = "res://assets/modelle/figuren/ki/";

    /// <summary>Körpergröße in Metern: Das Modell ist 1,0 Einheiten hoch (Scheitel bis Sohle) und wird auf die Größe der Menschen aus Blender gebracht.</summary>
    private const float KiSchmiedGroesse = 1.78f;
    /// <summary>Die Sohlen liegen im Modell bei −0,501: so weit (in Modelleinheiten) anheben, damit die Figur auf dem Boden steht.</summary>
    private const float KiSchmiedFusshoehe = 0.501f;
    /// <summary>Hüfthöhe in Metern (Hüftgelenk im Modell bei 0,002 über der Mitte): Der Beinfaktor sagt, wie weit ein Schritt trägt.</summary>
    private const float KiSchmiedHuefte = (0.002f + KiSchmiedFusshoehe) * KiSchmiedGroesse;

    // Schleife der Gehen-Animation: Kimodo liefert vier Sekunden (Anlauf, dann drei Doppelschritte). Die Pose passt am besten
    // von 2,000 s bis 3,033 s wieder zu sich selbst (gemessen: Abweichung 0,0065 über alle 52 Knochen); darin läuft die Wurzel
    // 0,999 Einheiten vorwärts, also 0,967 Einheiten je Sekunde.
    private const double KiGehenAnfang = 2.0, KiGehenDauer = 1.033;
    private const float KiGehenModellTempo = 0.967f;
    /// <summary>Meter je Sekunde, die eine Schleife mit Takt 1 schafft: Mit diesem Tempo rutschen die Füße nicht.</summary>
    private const float KiGehTempo = KiGehenModellTempo * KiSchmiedGroesse;
    /// <summary>Stehen, solange es keinen eigenen Clip gibt: eine Pose aus dem Anfang der Gehen-Datei (die Füße stehen bis etwa 0,8 s).</summary>
    private const double KiStehPose = 0.4;
    private const string KiOriginalKlip = "ArmatureAction";

    /// <summary>Bewegungen, für die ein Clip-Datei <c>schmied_ki_&lt;name klein&gt;.glb</c> gesucht wird (die Namen der Bewegungen der Blender-Figuren).</summary>
    private static readonly string[] KiKlipNamen =
    {
        "Stehen", "Sitzen", "Reden", "Haemmern", "Hobeln", "Saegen", "Drechseln", "Behauen", "Bildhauen", "Moersern", "Ruehren", "Mischen",
        "Zapfen", "Trinken", "Laute", "Kerzenziehen", "Schoepfen", "Schreiben", "Knacken", "Zaehlen", "Putzen", "Fechten", "Rechnen", "Wiegen",
        "Feinarbeit", "Blasebalg", "Schleifen", "Pressen", "Destillieren", "Beten", "Knien", "Predigen",
    };

    /// <summary>Startoption <c>--kischmied=aus|wenige|alle</c>: wie viele Passanten den KI-Schmied tragen (Standard: wenige).</summary>
    private string _kiSchmiedModus = "wenige";
    public void KiSchmiedVorgeben(string modus) => _kiSchmiedModus = modus.ToLowerInvariant();

    /// <summary>Nummern der Einwohner mit KI-Schmied: Sie setzen sich nicht auf Bänke (das Modell hat noch kein Sitzen).</summary>
    private readonly HashSet<int> _kiPassanten = new();

    /// <summary>Trägt dieser Einwohner den KI-Schmied? Nur Menschen, Männer, nicht gebeugt, ohne Last; Standard: jeder zweite Knecht und Tagelöhner.</summary>
    private bool KiSchmiedFuer(Einwohner e, PassantenTracht tracht, bool gebeugt)
    {
        if (_kiSchmiedModus == "aus" || e.Volk != Volk.Mensch || e.Geschlecht == Geschlecht.Frau || gebeugt || tracht.Last != Last.Keine) return false;
        if (_kiSchmiedModus == "alle") return true;
        return e.Rolle is "Knecht" or "Tagelöhner" && e.Nummer % 2 == 0;
    }

    /// <summary>
    /// Setzt den KI-Schmied als Kind von <paramref name="eltern"/> ein (Fußpunkt am Ursprung, vorne +Z wie bei allen Figuren). Null, wenn die Datei fehlt
    /// (dann nimmt <see cref="FigurEinsetzen"/> den Menschen aus Blender).
    /// </summary>
    private Figur? KiSchmiedEinsetzen(Node3D eltern, int nummer)
    {
        if (Modell(KiSchmiedDatei) is not { } szene) return null;
        var modell = szene.Instantiate<Node3D>();
        modell.Position = new Vector3(0, KiSchmiedFusshoehe, 0);
        var wurzel = new Node3D { Scale = Vector3.One * KiSchmiedGroesse };
        wurzel.AddChild(modell);
        eltern.AddChild(wurzel);
        var spieler = modell.GetNode<AnimationPlayer>("AnimationPlayer");
        spieler.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual; // das Spiel setzt die Pose selbst (FigurBewegen)
        KiKlipsVorbereiten(spieler, modell.FindChild("Skeleton3D", true, false) as Skeleton3D);

        var figur = new Figur
        {
            Modell = wurzel, Spieler = spieler, Nummer = nummer, Ki = true,
            Beinfaktor = KiSchmiedHuefte / 0.92f, Kniehoehe = 0.27f * KiSchmiedGroesse,
        };
        foreach (var teil in modell.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            figur.Teile.Add(teil);
        if (spieler.HasAnimation("Haemmern") && modell.FindChild("Skeleton3D", true, false) is Skeleton3D skelett) KiHammerAnbringen(figur, skelett);
        BewegungSetzen(figur, "Stehen");
        return figur;
    }

    // ── Hämmern ────────────────────────────────────────────────────────────
    // Der Clip „Haemmern“ ist von Hand auf dem Skelett gebaut (kein Kimodo-Clip): Aus der Stehen-Pose wird je Schlüsselbild das Ziel der rechten Hand
    // und die Richtung des Hammers vorgegeben, ein Zwei-Knochen-IK (Schulter, Ellbogen) und eine Handdrehung rechnen daraus die Knochen-Drehungen,
    // der Oberkörper beugt mit. Maße in Metern der Figur (Fußpunkt am Ursprung, vorne +Z, rechts −X).

    /// <summary>So viel Meter rückt der KI-Schmied am Amboss zum Werkstück vor (der Platz ist für den Blender-Menschen mit dem Werkstück 0,68 m vor den Füßen gebaut, der Hammer des KI-Schmieds braucht 0,43 m).</summary>
    private const float KiAmbossAbstand = 0.25f;
    private const double KiHaemmerDauer = 1.1;
    private const int KiSchulter = 26, KiHandwurzel = 28, KiRueckenUnten = 2, KiRueckenOben = 3;
    /// <summary>Länge des Hammers von der Faust bis zur Mitte des Kopfes (Meter).</summary>
    private const float KiHammerLaenge = 0.33f;

    /// <summary>Schlüsselbilder eines Schlags: Zeit (s), Hand (Meter), Richtung des Hammers, Beuge des Oberkörpers (Grad). Der Amboss liegt 0,43 m vor und 0,86 m über den Füßen.</summary>
    private static readonly (double Zeit, Vector3 Hand, Vector3 Hammer, float Beuge)[] KiHaemmerBilder =
    {
        (0.00, new Vector3(-0.21f, 1.28f, 0.30f), new Vector3(0, 0.75f, 0.65f), 0f),   // Hammer vor der Schulter
        (0.22, new Vector3(-0.20f, 1.38f, 0.22f), new Vector3(0, 0.90f, 0.40f), -2f),  // ausholen
        (0.40, new Vector3(-0.06f, 1.05f, 0.15f), new Vector3(0, -0.55f, 0.83f), 10f), // Schlag: der Kopf trifft (−0,06 | 0,87 | 0,43)
        (0.47, new Vector3(-0.07f, 1.10f, 0.16f), new Vector3(0, -0.30f, 0.95f), 9f),  // Nachfedern
        (0.75, new Vector3(-0.15f, 1.21f, 0.25f), new Vector3(0, 0.40f, 0.90f), 4f),
        (1.10, new Vector3(-0.21f, 1.28f, 0.30f), new Vector3(0, 0.75f, 0.65f), 0f),
    };

    private static Vector3 KiModellRaum(Vector3 meter) => new(meter.X / KiSchmiedGroesse, meter.Y / KiSchmiedGroesse - KiSchmiedFusshoehe, meter.Z / KiSchmiedGroesse);

    /// <summary>Dreht den Knochen (um seinen Ursprung, im Modellraum) so, dass der Vektor <paramref name="von"/> nach <paramref name="nach"/> zeigt.</summary>
    private static void KiAusrichten(Skeleton3D sk, int knochen, Vector3 von, Vector3 nach)
    {
        var global = sk.GetBoneGlobalPose(knochen).Basis.Orthonormalized();
        var neu = new Basis(new Quaternion(von.Normalized(), nach.Normalized())) * global;
        var eltern = sk.GetBoneParent(knochen);
        var elternBasis = eltern >= 0 ? sk.GetBoneGlobalPose(eltern).Basis.Orthonormalized() : Basis.Identity;
        sk.SetBonePoseRotation(knochen, (elternBasis.Inverse() * neu).GetRotationQuaternion());
    }

    private static Animation KiHaemmernBauen(Skeleton3D sk, Animation stehen)
    {
        var neu = new Animation { Length = (float)KiHaemmerDauer, LoopMode = Animation.LoopModeEnum.Linear };
        // Ausgangspose: erste Stellung des Stehen-Clips
        for (var i = 0; i < stehen.GetTrackCount(); i++)
        {
            var knochen = sk.FindBone(stehen.TrackGetPath(i).GetConcatenatedSubNames());
            if (knochen < 0) continue;
            var wert = stehen.TrackGetKeyValue(i, 0);
            switch (stehen.TrackGetType(i))
            {
                case Animation.TrackType.Rotation3D: sk.SetBonePoseRotation(knochen, (Quaternion)wert); break;
                case Animation.TrackType.Position3D: sk.SetBonePosePosition(knochen, (Vector3)wert); break;
                case Animation.TrackType.Scale3D: sk.SetBonePoseScale(knochen, (Vector3)wert); break;
            }
        }
        int[] bewegt = { KiRueckenUnten, KiRueckenOben, 25, KiSchulter, KiSchulter + 1, KiHandwurzel };
        var basis = bewegt.ToDictionary(b => b, b => sk.GetBonePoseRotation(b));
        var spuren = new Dictionary<int, int>();
        for (var i = 0; i < stehen.GetTrackCount(); i++)
        {
            var knochen = sk.FindBone(stehen.TrackGetPath(i).GetConcatenatedSubNames());
            var spur = neu.AddTrack(stehen.TrackGetType(i));
            neu.TrackSetPath(spur, stehen.TrackGetPath(i));
            if (stehen.TrackGetType(i) == Animation.TrackType.Rotation3D && bewegt.Contains(knochen)) { spuren[knochen] = spur; continue; }
            neu.TrackInsertKey(spur, 0, stehen.TrackGetKeyValue(i, 0));
        }

        var schulter = sk.GetBoneGlobalPose(KiSchulter).Origin;
        var l1 = (sk.GetBoneGlobalPose(KiSchulter + 1).Origin - schulter).Length();
        var l2 = (sk.GetBoneGlobalPose(KiHandwurzel).Origin - sk.GetBoneGlobalPose(KiSchulter + 1).Origin).Length();
        foreach (var (zeit, hand, hammer, beuge) in KiHaemmerBilder)
        {
            foreach (var (b, q) in basis) sk.SetBonePoseRotation(b, q);
            // Oberkörper beugt nach vorn (Drehung um die X-Achse des Modells, je zur Hälfte in zwei Rückenknochen)
            foreach (var b in new[] { KiRueckenUnten, KiRueckenOben })
            {
                var g = sk.GetBoneGlobalPose(b).Basis.Orthonormalized();
                var eltern = sk.GetBoneGlobalPose(sk.GetBoneParent(b)).Basis.Orthonormalized();
                sk.SetBonePoseRotation(b, (eltern.Inverse() * new Basis(Vector3.Right, Mathf.DegToRad(beuge / 2)) * g).GetRotationQuaternion());
            }
            // Zwei-Knochen-IK: Ellbogen nach unten und außen
            schulter = sk.GetBoneGlobalPose(KiSchulter).Origin;
            var ziel = KiModellRaum(hand);
            var weg = ziel - schulter;
            var d = Mathf.Min(weg.Length(), l1 + l2 - 0.002f);
            var richtung = weg.Normalized();
            var a = (l1 * l1 - l2 * l2 + d * d) / (2 * d);
            var h = Mathf.Sqrt(Mathf.Max(l1 * l1 - a * a, 0));
            var pol = new Vector3(-0.5f, -1f, -0.4f);
            pol = (pol - richtung * pol.Dot(richtung)).Normalized();
            var ellbogen = schulter + richtung * a + pol * h;
            var ellbogenJetzt = sk.GetBoneGlobalPose(KiSchulter + 1).Origin;
            KiAusrichten(sk, KiSchulter, ellbogenJetzt - schulter, ellbogen - schulter);
            ellbogenJetzt = sk.GetBoneGlobalPose(KiSchulter + 1).Origin;
            KiAusrichten(sk, KiSchulter + 1, sk.GetBoneGlobalPose(KiHandwurzel).Origin - ellbogenJetzt, schulter + richtung * d - ellbogenJetzt);
            KiAusrichten(sk, KiHandwurzel, sk.GetBoneGlobalPose(KiHandwurzel).Basis.Y, hammer);
            foreach (var (b, spur) in spuren) neu.TrackInsertKey(spur, zeit, sk.GetBonePoseRotation(b));
        }
        return neu;
    }

    private static StandardMaterial3D? _kiHammerStoff;

    /// <summary>Hängt den Hammer der Blender-Figur (Netz „Hammer“ in <c>figur_mensch.glb</c>) an die rechte Hand; sichtbar nur bei „Haemmern“ (<see cref="Figur.Werkzeuge"/>).</summary>
    private void KiHammerAnbringen(Figur figur, Skeleton3D skelett)
    {
        if (Modell("figuren/figur_mensch") is not { } mensch) return;
        var quelle = mensch.Instantiate<Node3D>();
        var netz = quelle.FindChildren("Hammer", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().FirstOrDefault()?.Mesh;
        quelle.Free();
        if (netz is null) return;
        var anker = new BoneAttachment3D { BoneName = $"bone_{KiHandwurzel}" };
        skelett.AddChild(anker);
        _kiHammerStoff ??= new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.7f };
        // Das Netz ist in Metern, das Modell in Einheiten zu 1,78 m; der Kopf sitzt im Netz am unteren Ende (y 0,46), der Griff am oberen (y 0,80):
        // um 180° gedreht zeigt der Kopf von der Hand weg, in die Richtung des Unterarms (die Hand wird zum Hammer hin ausgerichtet).
        var lage = new Basis(Vector3.Right, Mathf.Pi).Scaled(Vector3.One / KiSchmiedGroesse);
        var hammer = new MeshInstance3D { Mesh = netz, MaterialOverride = _kiHammerStoff, Visible = false, Name = "Hammer" };
        hammer.Transform = new Transform3D(lage, -(lage * new Vector3(-0.21f, 0.80f, 0.015f)));
        anker.AddChild(hammer);
        figur.Werkzeuge["Hammer"] = hammer;
    }

    /// <summary>
    /// Bereitet die Animationen einmal je Bibliothek vor (die Clips teilen sich alle Figuren): „Gehen“ ist die Kimodo-Animation, auf der Stelle gemacht –
    /// die Wurzel läuft in der Datei vorwärts, im Spiel bewegt der Knoten die Figur, sonst liefe sie ihrem eigenen Gang davon. Dann kommen die
    /// Clips aus <c>schmied_ki_&lt;bewegung&gt;.glb</c> dazu, soweit es sie gibt.
    /// </summary>
    private void KiKlipsVorbereiten(AnimationPlayer spieler, Skeleton3D? skelett)
    {
        if (spieler.HasAnimation("Gehen")) return;
        var bibliothek = spieler.GetAnimationLibrary("");
        var gehen = (Animation)spieler.GetAnimation(KiOriginalKlip).Duplicate();
        for (var i = 0; i < gehen.GetTrackCount(); i++)
        {
            if (gehen.TrackGetType(i) != Animation.TrackType.Position3D || !gehen.TrackGetPath(i).ToString().EndsWith(":bone_0")) continue;
            var erste = (Vector3)gehen.TrackGetKeyValue(i, 0);
            var a = gehen.PositionTrackInterpolate(i, (float)KiGehenAnfang);
            var b = gehen.PositionTrackInterpolate(i, (float)(KiGehenAnfang + KiGehenDauer));
            var proSekunde = (b - a) / (float)KiGehenDauer;
            for (var k = 0; k < gehen.TrackGetKeyCount(i); k++)
            {
                var t = (float)(gehen.TrackGetKeyTime(i, k) - KiGehenAnfang);
                var p = (Vector3)gehen.TrackGetKeyValue(i, k);
                // Waagerechte Bewegung der Wurzel abziehen (Höhe bleibt: das Wippen der Hüfte), die Figur bleibt bei der ersten Stellung
                gehen.TrackSetKeyValue(i, k, new Vector3(p.X - (a.X - erste.X) - proSekunde.X * t, p.Y, p.Z - (a.Z - erste.Z) - proSekunde.Z * t));
            }
        }
        bibliothek.AddAnimation("Gehen", gehen);
        foreach (var name in KiKlipNamen)
        {
            var pfad = $"{KiSchmiedOrdner}schmied_ki_{name.ToLowerInvariant()}.glb";
            if (!ResourceLoader.Exists(pfad)) continue;
            var quelle = ResourceLoader.Load<PackedScene>(pfad).Instantiate<Node3D>();
            var quellSpieler = quelle.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>().FirstOrDefault();
            var klip = quellSpieler?.GetAnimationList().Where(n => n != "RESET").Select(n => quellSpieler.GetAnimation(n)).FirstOrDefault();
            quelle.Free();
            if (klip is null) { GD.PushError($"KI-Schmied: {pfad} enthält keine Animation"); continue; }
            var kopie = (Animation)klip.Duplicate();
            kopie.LoopMode = Animation.LoopModeEnum.Linear;
            bibliothek.AddAnimation(name, kopie);
        }
        if (skelett is not null && !bibliothek.HasAnimation("Haemmern") && bibliothek.HasAnimation("Stehen"))
            bibliothek.AddAnimation("Haemmern", KiHaemmernBauen(skelett, bibliothek.GetAnimation("Stehen")));
    }

    /// <summary>
    /// <see cref="BewegungSetzen"/> für den KI-Schmied: Gehen, Eilen, Tragen und Gebeugt teilen sich den Gehen-Clip (der Takt gleicht das Tempo an,
    /// damit die Füße nicht rutschen), jede andere Bewegung nimmt ihren Clip, wenn es ihn gibt, sonst „Stehen“, sonst die stehende Pose.
    /// </summary>
    private static void KiBewegungSetzen(Figur f, string bewegung, float takt)
    {
        string klip;
        double anfang, dauer;
        float kiTakt;
        if (bewegung is "Gehen" or "Eilen" or "Tragen" or "Gebeugt")
        {
            var grund = bewegung switch { "Eilen" => EilenTempo, "Gebeugt" => GebeugtTempo, "Tragen" => TragenTempo, _ => GehenTempo };
            klip = "Gehen"; anfang = KiGehenAnfang; dauer = KiGehenDauer;
            kiTakt = takt * grund * f.Beinfaktor / KiGehTempo; // gewünschte Meter je Sekunde ÷ Meter je Schleife
        }
        else if (f.Spieler.HasAnimation(bewegung))
        {
            klip = bewegung; anfang = 0; dauer = Math.Max(0.1, f.Spieler.GetAnimation(bewegung).Length); kiTakt = takt;
        }
        else if (f.Spieler.HasAnimation("Stehen"))
        {
            klip = "Stehen"; anfang = 0; dauer = Math.Max(0.1, f.Spieler.GetAnimation("Stehen").Length); kiTakt = 1;
        }
        else
        {
            klip = KiOriginalKlip; anfang = KiStehPose; dauer = 1; kiTakt = 0; // eine feste Pose
        }
        f.Takt = kiTakt;
        if (f.Bewegung == bewegung) return;
        f.Bewegung = bewegung;
        foreach (var (name, teil) in f.Werkzeuge) teil.Visible = WerkstattWerkzeug.TryGetValue(bewegung, out var sichtbar) && sichtbar.Contains(name);
        if (klip == f.KiKlip) return; // Gehen ↔ Eilen: die Schleife läuft in derselben Phase weiter
        f.KiKlip = klip;
        f.Spieler.Play(klip);
        f.Anfang = anfang;
        f.Dauer = dauer;
        f.Phase = kiTakt == 0 ? 0 : (Streuung(f.Nummer, 131) & 0xFFFF) / 65536.0 * dauer;
        f.Neu = true;
        f.Langsam = kiTakt == 0 || bewegung is "Stehen" or "Sitzen" or "Beten" or "Knien";
    }

    private static readonly Dictionary<Mesh, Material> _kiHervorhebung = new();

    /// <summary>
    /// Das Material des KI-Schmieds mit warmem Eigenleuchten (Hervorheben, Auswahl): ein Abbild des Materials aus der Datei, einmal je Netz.
    /// Die helle Schicht der Blender-Figuren (<c>MaterialOverlay</c>) macht das PBR-Material durchscheinend gelb.
    /// </summary>
    private static Material? KiHervorhebung(MeshInstance3D teil)
    {
        if (teil.Mesh is not { } netz || netz.SurfaceGetMaterial(0) is not StandardMaterial3D grund) return null;
        if (_kiHervorhebung.TryGetValue(netz, out var vorhanden)) return vorhanden;
        var hell = (StandardMaterial3D)grund.Duplicate();
        hell.EmissionEnabled = true;
        hell.Emission = new Color(1f, 0.85f, 0.45f);
        hell.EmissionEnergyMultiplier = 0.12f;
        return _kiHervorhebung[netz] = hell;
    }
}
