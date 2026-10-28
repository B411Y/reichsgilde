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
        KiKlipsVorbereiten(spieler);

        var figur = new Figur
        {
            Modell = wurzel, Spieler = spieler, Nummer = nummer, Ki = true,
            Beinfaktor = KiSchmiedHuefte / 0.92f, Kniehoehe = 0.27f * KiSchmiedGroesse,
        };
        foreach (var teil in modell.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            figur.Teile.Add(teil);
        BewegungSetzen(figur, "Stehen");
        return figur;
    }

    /// <summary>
    /// Bereitet die Animationen einmal je Bibliothek vor (die Clips teilen sich alle Figuren): „Gehen“ ist die Kimodo-Animation, auf der Stelle gemacht –
    /// die Wurzel läuft in der Datei vorwärts, im Spiel bewegt der Knoten die Figur, sonst liefe sie ihrem eigenen Gang davon. Dann kommen die
    /// Clips aus <c>schmied_ki_&lt;bewegung&gt;.glb</c> dazu, soweit es sie gibt.
    /// </summary>
    private static void KiKlipsVorbereiten(AnimationPlayer spieler)
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
        if (klip == f.KiKlip) return; // Gehen ↔ Eilen: die Schleife läuft in derselben Phase weiter
        f.KiKlip = klip;
        f.Spieler.Play(klip);
        f.Anfang = anfang;
        f.Dauer = dauer;
        f.Phase = kiTakt == 0 ? 0 : (Streuung(f.Nummer, 131) & 0xFFFF) / 65536.0 * dauer;
        f.Neu = true;
        f.Langsam = kiTakt == 0 || bewegung is "Stehen" or "Sitzen" or "Beten" or "Knien";
    }
}
