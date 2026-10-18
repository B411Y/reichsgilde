using Godot;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Karren (Teil der <c>partial class Stadtansicht</c>): Handkarren, Ochsenkarren und Kriegskarren als Modelle aus Blender
/// (<c>karren_hand.glb</c>, <c>karren_ochse.glb</c>, <c>karren_krieg.glb</c>, Fahrtrichtung +Z) mit Skelett und den Bewegungen „Fahren“ und „Stehen“.
///
/// Die Darstellung rechnet nichts aus, sie liest nur den <see cref="Karren"/> des Kerns: Zustand (fährt oder steht), Ladung (wie viele Sorten)
/// und Besitzer (Farbe von Plane und Hausfarbe). Die Animationen laufen am Ort; <see cref="KarrenBewegen"/> schiebt den Karren und stellt das
/// Tempo der Bewegung so ein, dass die Räder nicht rutschen. Karren außerhalb des Bildes werden nicht animiert, sehr kleine (weit herausgezoomt) seltener.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Ein Karren in der Stadtansicht: der Knoten mit Ort und Drehung, das Modell darunter und alles, was wir davon schalten.</summary>
    private sealed class KarrenAnsicht
    {
        public required Node3D Wurzel { get; init; }
        public required KarrenArt Art { get; init; }
        public AnimationPlayer? Spieler { get; init; }
        /// <summary>„Ladung1“ bis „LadungN“ des Modells (Kisten, Säcke, Fässer): so viele sichtbar, wie Sorten geladen sind.</summary>
        public required MeshInstance3D[] Ladung { get; init; }
        public MeshInstance3D? Plane { get; init; }
        public required Node3D Eskorte { get; init; }
        /// <summary>Wie viele Ladungsknoten gerade sichtbar sind (-1 = noch nie gesetzt); geschaltet wird nur beim Wechsel.</summary>
        public int GezeigteLadung { get; set; } = -1;
        public bool Eskortiert { get; set; }
        public string Bewegung { get; set; } = "";
        /// <summary>Animationszeit, die noch nicht abgespielt ist (der Karren war aus dem Bild oder das Bild wurde übersprungen).</summary>
        public double Rueckstand { get; set; }
        /// <summary>Versetzt die Bilder, in denen Karren animiert werden, damit nicht alle im selben Bild dran sind.</summary>
        public int Versatz { get; init; }
        /// <summary>Hier steht die Passanten-Figur (Knecht, Treiber, Kutscher), sobald <see cref="KarrenFigurAnhaengen"/> eine anhängt.</summary>
        public Node3D? Figur { get; set; }
    }

    /// <summary>Was an einer Karrenart verschieden ist: das Modell, der Weg einer Schleife „Fahren“ und der Anker für den Menschen.</summary>
    private sealed record KarrenArt(string Datei, float Weg, string Anker, string FigurFahren, string FigurStehen);

    /// <summary>
    /// Die drei Karrenarten, nach <see cref="Karren.Typ"/>. <c>Weg</c> ist die Strecke, die eine Schleife „Fahren“ zurücklegt (Räder genau einmal herum,
    /// 2π · Radhalbmesser aus <c>tools/modelle/karren.py</c>): 2,83 m beim Handkarren, 4,52 m beim Ochsenkarren, 3,77 m beim Kriegskarren.
    /// </summary>
    private static readonly Dictionary<string, KarrenArt> KarrenArten = new()
    {
        ["Handkarren"] = new("karren_hand", 2f * Mathf.Pi * 0.45f, "Anker_Zieher", "Ziehen", "Stehen"),
        ["Ochsenkarren"] = new("karren_ochse", 2f * Mathf.Pi * 0.72f, "Anker_Treiber", "Gehen", "Stehen"),
        ["Kriegskarren"] = new("karren_krieg", 2f * Mathf.Pi * 0.6f, "Anker_Kutscher", "Kutschieren", "Kutschieren"),
    };

    /// <summary>Schneller als so viele Schleifen „Fahren“ je Sekunde dreht kein Rad: bei hohem Tempo rutschen sie lieber, als zu flimmern.</summary>
    private const float KarrenMaxTempo = 3f;

    /// <summary>Ab dieser Kamerahöhe (Meter) ist ein Karren so klein, dass er nur jedes dritte Bild animiert wird.</summary>
    private const float KarrenFernZoom = 100f;

    /// <summary>So weit (in Metern) darf ein Karren außerhalb des Bildes stehen und wird noch animiert (seine Tiere sind rund 5 m lang).</summary>
    private const float KarrenRandMeter = 6f;

    /// <summary>So weit (Meter) stehen wartende Karren hintereinander: ein Ochsenkarren ist mit Ochse 5 m lang, ein Kriegskarren mit zwei Pferden 6 m.</summary>
    private const float KarrenAbstand = 7f;

    private readonly HashSet<string> _karrenSchleifen = new();

    private KarrenAnsicht KarrenErzeugen(Karren karren, Color besitzer, int nummer)
    {
        var art = KarrenArten.GetValueOrDefault(karren.Typ) ?? KarrenArten["Handkarren"];
        var wurzel = new Node3D { Name = "Karren" };
        AddChild(wurzel);
        // Zustand: nur die Hausfarbe (Besitzer); „Ladung1–3“ und „Plane“ schaltet KarrenBewegen
        var modell = ModellEinsetzen(art.Datei, wurzel, Vector3.Zero, new ModellZustand(Besitzer: besitzer))?.Wurzel;
        AnimationPlayer? spieler = null;
        var ladung = new List<MeshInstance3D>();
        MeshInstance3D? plane = null;
        if (modell is not null)
        {
            spieler = modell.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>().FirstOrDefault();
            if (spieler is not null)
            {
                // Die Bewegungen sind Schleifen; Godot übernimmt das nicht aus der Datei. Die Animationen teilen sich alle Karren einer Art.
                if (_karrenSchleifen.Add(art.Datei))
                    foreach (var name in spieler.GetAnimationList())
                        spieler.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
                // Wir schalten den Takt selbst (außerhalb des Bildes ruht der Karren): kein automatischer Lauf
                spieler.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
                spieler.Play("Stehen");
                spieler.Seek(GD.Randf() * spieler.CurrentAnimationLength, true); // Tiere und Räder nicht im Gleichschritt
            }
            for (var i = 1; modell.FindChild($"Ladung{i}", true, false) is MeshInstance3D teil; i++)
            {
                teil.Visible = false;
                ladung.Add(teil);
            }
            plane = modell.FindChild("Plane", true, false) as MeshInstance3D;
            if (plane is not null) plane.Visible = false;
        }
        // Die Eskorte bleibt vorerst ein Begleiter neben dem Karren
        var eskorte = new MeshInstance3D { Name = "Eskorte", Mesh = new CapsuleMesh { Radius = 0.35f, Height = 1.8f }, MaterialOverride = Material(new Color(0.35f, 0.35f, 0.38f)), Position = new Vector3(1.8f, 0.9f, 0.6f), Visible = false };
        wurzel.AddChild(eskorte);
        var ansicht = new KarrenAnsicht
        {
            Wurzel = wurzel, Art = art, Spieler = spieler, Ladung = ladung.ToArray(), Plane = plane, Eskorte = eskorte, Versatz = nummer,
        };
        KarrenFigurAnhaengen(ansicht, modell);
        return ansicht;
    }

    /// <summary>
    /// Stellt die Passanten-Figur an den Anker des Karrens: Zieher zwischen den Holmen („Ziehen“), Treiber neben dem Ochsen („Gehen“),
    /// Kutscher auf dem Bock („Kutschieren“). Der Anker blickt mit −Z, die Figur mit +Z: Beim Einsetzen um 180° um die Hochachse drehen.
    /// Die Figur kommt aus dem Passanten-Teil (<c>Stadtansicht.Passanten.cs</c>), nicht aus einer eigenen Datei.
    /// Noch ohne Figur: hier bleibt der Anker leer, bis die Passanten-Figuren eingebaut sind.
    /// </summary>
    private void KarrenFigurAnhaengen(KarrenAnsicht ansicht, Node3D? modell)
    {
    }

    /// <summary>Liegt der Karren (samt Tieren) im sichtbaren Ausschnitt der Kamera? Nur dann lohnt sich seine Animation.</summary>
    private bool KarrenImBild(Node3D wurzel)
    {
        if (_raum is not null) return false; // Raumansicht: die Stadt ist verdeckt
        var flaeche = GetViewport().GetVisibleRect().Size;
        var rand = KarrenRandMeter * flaeche.Y / _kamera.Size;
        var mitte = wurzel.GlobalPosition + new Vector3(0, 1, 0);
        if (_kamera.IsPositionBehind(mitte)) return false;
        var punkt = _kamera.UnprojectPosition(mitte);
        return punkt.X > -rand && punkt.Y > -rand && punkt.X < flaeche.X + rand && punkt.Y < flaeche.Y + rand;
    }

    /// <summary>
    /// Wie schnell der Karren gerade fährt (Meter je echter Sekunde): Weg zum Markt geteilt durch die Fahrzeit in Spielminuten,
    /// mal Spielminuten je Sekunde bei dem eingestellten Tempo. 0 bei Pause, am Betrieb und am geschlossenen Tor.
    /// </summary>
    private float KarrenGeschwindigkeit(Karren karren, BetriebsAnsicht ansicht, int fahrzeit)
    {
        if (karren.Zustand == KarrenZustand.AmBetrieb || fahrzeit <= 0 || _simulation is not { } sim || sim.Takt.IstPausiert || karren.WartetAmTor(sim.Zeit))
            return 0;
        return WegLaenge(ansicht.WegZumMarkt) / fahrzeit * (float)sim.Takt.TicksProSekunde;
    }

    /// <summary>
    /// Wählt „Fahren“ oder „Stehen“ und treibt die Bewegung voran. Beim Fahren läuft die Schleife so schnell, dass die Räder auf dem Boden abrollen:
    /// <c>Geschwindigkeit / Weg</c> Schleifen je Sekunde.
    /// </summary>
    private void KarrenAnimieren(KarrenAnsicht karren, float geschwindigkeit, double delta)
    {
        if (karren.Spieler is not { } spieler) return;
        var faehrt = geschwindigkeit > 0.05f;
        var bewegung = faehrt ? "Fahren" : "Stehen";
        if (bewegung != karren.Bewegung)
        {
            karren.Bewegung = bewegung;
            spieler.Play(bewegung, 0.25);
        }
        var tempo = faehrt ? Mathf.Min(geschwindigkeit / karren.Art.Weg, KarrenMaxTempo) * spieler.GetAnimation("Fahren").Length : 1f;
        if (_simulation?.Takt.IstPausiert == true || !KarrenImBild(karren.Wurzel))
        {
            karren.Rueckstand = 0;
            return;
        }
        karren.Rueckstand += delta * tempo;
        // weit herausgezoomt sieht man die Bewegung kaum: nur jedes dritte Bild (die Zeit läuft in einem Stück nach)
        if (_kamera.Size >= KarrenFernZoom && (Engine.GetProcessFrames() + (ulong)karren.Versatz) % 3 != 0) return;
        spieler.Advance(karren.Rueckstand);
        karren.Rueckstand = 0;
    }

    private void KarrenBewegen(Karren karren, int nummer, BetriebsAnsicht ansicht, int fahrzeit, double jetztGenau, double delta, bool spieler)
    {
        if (!_karren.TryGetValue(karren, out var gezeigt))
        {
            gezeigt = KarrenErzeugen(karren, ansicht.Farbe, nummer);
            gezeigt.Wurzel.Position = ansicht.Hof;
            _karren[karren] = gezeigt;
        }
        var knoten = gezeigt.Wurzel;

        // Mehrere Karren eines Betriebs fahren hintereinander statt ineinander (mit den Tieren sind sie bis zu 6 m lang): Karren Nr. n
        // liegt immer n · KarrenAbstand weiter Richtung Markt als der erste. Am Halteplatz warten sie so in einer Reihe auf dem Weg,
        // und auf der Rückfahrt kommt der erste zuerst an.
        var laenge = WegLaenge(ansicht.WegZumMarkt);
        var versatz = laenge > 0 ? nummer * KarrenAbstand / laenge : 0f;
        float Stelle(float anteil) => Math.Min(1f, anteil + versatz);
        Vector3 ziel;
        Vector3 blick;
        if (karren.Zustand == KarrenZustand.AmBetrieb)
        {
            ziel = PunktAufWeg(ansicht.WegZumMarkt, Stelle(0));
            blick = PunktAufWeg(ansicht.WegZumMarkt, Math.Min(1f, Stelle(0) + (laenge > 0 ? 1f / laenge : 0f)));
        }
        else
        {
            var rest = Math.Clamp((karren.Ankunft - jetztGenau) / fahrzeit, 0, 1);
            var anteil = (float)(karren.Zustand == KarrenZustand.ZumMarkt ? 1 - rest : rest);
            var stelle = Stelle(anteil);
            ziel = PunktAufWeg(ansicht.WegZumMarkt, stelle);
            blick = PunktAufWeg(ansicht.WegZumMarkt, Math.Clamp(stelle + (karren.Zustand == KarrenZustand.ZumMarkt ? 0.02f : -0.02f), 0f, 1f));
        }

        // Das Ziel wandert dank des Minuten-Bruchteils schon stetig. Das kurze Hinterhergleiten
        // fängt nur noch echte Sprünge ab (Abfahrt vom Hof, Ankunft, Tempowechsel).
        knoten.Position = knoten.Position.Lerp(ziel, (float)Math.Min(1, delta * 10));
        var richtung = blick - knoten.Position;
        richtung.Y = 0;
        if (richtung.LengthSquared() > 0.01f)
            knoten.Rotation = new Vector3(0, Mathf.Atan2(richtung.X, richtung.Z), 0);

        // Ladung nach der Zahl der geladenen Sorten, Plane darüber, Eskorte als Begleiter (nur beim Wechsel schalten)
        var sorten = Math.Min(karren.Ladung.Count(l => l.Value > 0), gezeigt.Ladung.Length);
        if (sorten != gezeigt.GezeigteLadung)
        {
            gezeigt.GezeigteLadung = sorten;
            for (var i = 0; i < gezeigt.Ladung.Length; i++) gezeigt.Ladung[i].Visible = i < sorten;
            if (gezeigt.Plane is not null) gezeigt.Plane.Visible = sorten > 0;
        }
        var eskortiert = karren.Eskorte != "Keine" && karren.Ladung.Count > 0;
        if (eskortiert != gezeigt.Eskortiert)
        {
            gezeigt.Eskortiert = eskortiert;
            gezeigt.Eskorte.Visible = eskortiert;
        }

        KarrenAnimieren(gezeigt, KarrenGeschwindigkeit(karren, ansicht, fahrzeit), delta);
    }

    private static float WegLaenge(Vector3[] weg)
    {
        var laenge = 0f;
        for (var i = 0; i + 1 < weg.Length; i++) laenge += weg[i].DistanceTo(weg[i + 1]);
        return laenge;
    }

    private static Vector3 PunktAufWeg(Vector3[] weg, float anteil)
    {
        var laengen = new float[weg.Length - 1];
        float gesamt = 0;
        for (var i = 0; i < laengen.Length; i++) { laengen[i] = weg[i].DistanceTo(weg[i + 1]); gesamt += laengen[i]; }
        var strecke = anteil * gesamt;
        for (var i = 0; i < laengen.Length; i++)
        {
            if (strecke <= laengen[i]) return weg[i].Lerp(weg[i + 1], laengen[i] > 0 ? strecke / laengen[i] : 0);
            strecke -= laengen[i];
        }
        return weg[^1];
    }
}
