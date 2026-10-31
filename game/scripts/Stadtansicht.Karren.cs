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
        /// <summary>Der Mensch am Karren (Zieher, Treiber, Kutscher), eine Figur wie die Passanten; null, wenn das Figurenmodell fehlt.</summary>
        public Figur? Fahrer { get; set; }
        /// <summary>Die sichtbare Fahrt mit fester Geschwindigkeit (Stadtansicht.Karren.cs, <see cref="Karrenzug"/>) und die Profile, mit denen sie zuletzt gerechnet wurde.</summary>
        public Karrenzug Zug { get; } = new();
        public (Fahrprofil Hin, Fahrprofil Zurueck)? ZugProfile { get; set; }
        /// <summary>Wie schnell der Karren im letzten Bild fuhr (Meter je echter Sekunde): treibt Räder und Schritte.</summary>
        public float Geschwindigkeit { get; set; }
    }

    /// <summary>Was an einer Karrenart verschieden ist: das Modell, der Weg einer Schleife „Fahren“ und der Anker für den Menschen.</summary>
    private sealed record KarrenArt(string Datei, float Weg, string Anker, string FigurFahren, string FigurStehen);

    /// <summary>
    /// Die drei Karrenarten, nach <see cref="Karren.Typ"/>. <c>Weg</c> ist die Strecke, die eine Schleife „Fahren“ zurücklegt (Räder genau einmal herum,
    /// 2π · Radhalbmesser aus <c>tools/modelle/karren.py</c>): 2,83 m beim Handkarren, 4,52 m beim Ochsenkarren, 3,77 m beim Kriegskarren.
    /// </summary>
    private static readonly Dictionary<string, KarrenArt> KarrenArten = new()
    {
        ["Handkarren"] = new("fahrzeuge/karren_hand", 2f * Mathf.Pi * 0.45f, "Anker_Zieher", "Ziehen", "Stehen"),
        ["Ochsenkarren"] = new("fahrzeuge/karren_ochse", 2f * Mathf.Pi * 0.72f, "Anker_Treiber", "Gehen", "Stehen"),
        ["Kriegskarren"] = new("fahrzeuge/karren_krieg", 2f * Mathf.Pi * 0.6f, "Anker_Kutscher", "Kutschieren", "Kutschieren"),
    };

    /// <summary>Schneller als so viele Schleifen „Fahren“ je Sekunde dreht kein Rad: bei hohem Tempo rutschen sie lieber, als zu flimmern.</summary>
    private const float KarrenMaxTempo = 3f;

    /// <summary>Ab dieser Kamerahöhe (Meter) ist ein Karren so klein, dass er nur jedes dritte Bild animiert wird.</summary>
    private const float KarrenFernZoom = 100f;

    /// <summary>So weit (in Metern) darf ein Karren außerhalb des Bildes stehen und wird noch animiert (seine Tiere sind rund 5 m lang).</summary>
    private const float KarrenRandMeter = 6f;

    /// <summary>So weit (Meter) stehen wartende Karren hintereinander: ein Ochsenkarren ist mit Ochse 5 m lang, ein Kriegskarren mit zwei Pferden 6 m.</summary>
    private const float KarrenAbstand = 7f;

    /// <summary>So weit (Meter) neben der Mitte des Weges fahren die Karren: auf ihrer rechten Seite (Rechtsverkehr, Schritt 5d).</summary>
    private const float KarrenSpur = 0.9f;

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
        ansicht.Fahrer = KarrenFahrerEinsetzen(ansicht, modell, karren, besitzer);
        return ansicht;
    }

    /// <summary>Die Figur ist ein Mensch: Zieher, Treiber und Kutscher sind für 0,92 m Hüfthöhe an Holmen, Seil und Bock abgestimmt.</summary>
    private const float KarrenGehTempo = GehenTempo;

    /// <summary>
    /// Stellt den Menschen an den Anker des Karrens: Zieher zwischen den Holmen („Ziehen“), Treiber neben dem Ochsen („Gehen“), Kutscher auf
    /// dem Bock („Kutschieren“). Der Anker blickt mit −Z, die Figur mit +Z: Ein Halter am Anker dreht sie um 180° um die Hochachse. Die Figur
    /// kommt aus <see cref="FigurEinsetzen"/> (<c>Stadtansicht.Figuren.cs</c>), immer als Mensch (Nummer durch 4 teilbar), im Gewand in
    /// der gedämpften Hausfarbe; die Nummer stammt aus Betriebs- und Karrenname, damit sie bei jedem Start gleich ist.
    /// </summary>
    private Figur? KarrenFahrerEinsetzen(KarrenAnsicht ansicht, Node3D? modell, Karren karren, Color besitzer)
    {
        if (modell?.FindChild(ansicht.Art.Anker, true, false) is not Node3D anker || anker.GetParent() is not { } eltern) return null;
        var halter = new Node3D { Name = "Fahrer" };
        eltern.AddChild(halter);
        halter.Transform = anker.Transform.RotatedLocal(Vector3.Up, Mathf.Pi);
        var nummer = 4 * (int)(StabilerHash(karren.Heimat.Name + "/" + karren.Name) % 100000);
        var gewand = besitzer.Lerp(new Color(0.45f, 0.38f, 0.28f), 0.45f);
        var aussehen = AussehenAusNummer(nummer, gewand) with { Kopf = Kopfbedeckung.Strohhut };
        return FigurEinsetzen(halter, aussehen, nummer);
    }

    /// <summary>Ein Hash, der bei jedem Programmstart gleich ist (<c>string.GetHashCode</c> ist es nicht).</summary>
    private static uint StabilerHash(string text)
    {
        var h = 2166136261u;
        foreach (var c in text) h = (h ^ c) * 16777619u;
        return h;
    }

    /// <summary>
    /// Der Mensch am Karren bewegt sich mit: beim Fahren „Ziehen“ oder „Gehen“ im Takt der Räder (die Schritte passen zum Tempo, aber
    /// nicht schneller als <see cref="KarrenMaxTempo"/> Schleifen der Räder), der Kutscher „Kutschieren“; im Stand „Stehen“ (der Kutscher
    /// bleibt auf dem Bock bei „Kutschieren“).
    /// </summary>
    private void KarrenFahrerBewegen(KarrenAnsicht karren, float geschwindigkeit, double delta)
    {
        if (karren.Fahrer is not { } fahrer) return;
        if (geschwindigkeit > 0.05f)
        {
            var kutscher = karren.Art.FigurFahren == "Kutschieren";
            var tempo = Mathf.Min(geschwindigkeit, KarrenMaxTempo * karren.Art.Weg);
            BewegungSetzen(fahrer, karren.Art.FigurFahren, kutscher ? 1f : Mathf.Min(tempo / (KarrenGehTempo * fahrer.Beinfaktor), 3f));
        }
        else BewegungSetzen(fahrer, karren.Art.FigurStehen);
        var sekunden = _simulation?.Takt.IstPausiert == true ? 0 : delta; // in der Pause steht auch der Mensch still
        FigurImBildBewegen(fahrer, karren.Wurzel.Position, sekunden);
    }

    /// <summary>Wie weit (Chebyshev-Abstand von der Platzmitte, Meter) die Karren am Markt halten: Das Pflaster reicht bis 12 m, die Tiere stehen am Rand.</summary>
    private float KarrenHaltAmMarkt => _marktHalb + 3.5f;

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Vector3[], Vector3[]> _karrenWege = new();

    /// <summary>Der Weg des Karrens zum Markt, gekürzt auf den Rand des Platzes (<see cref="KarrenWegBisPlatzrand"/>); je Weg nur einmal gerechnet.</summary>
    private Vector3[] KarrenWeg(BetriebsAnsicht ansicht)
    {
        if (!_karrenWege.TryGetValue(ansicht.WegZumMarkt, out var weg))
            _karrenWege.Add(ansicht.WegZumMarkt, weg = KarrenWegBisPlatzrand(ansicht.WegZumMarkt, KarrenHaltAmMarkt));
        return weg;
    }

    /// <summary>
    /// Der Weg endet in der Platzmitte (im Brunnen): Karren halten und wenden dort, wo ihre Zufahrtsgasse auf den Platz trifft. Schneidet den Weg
    /// an dem Punkt ab, an dem er das Quadrat mit der halben Seite <paramref name="halb"/> um die Platzmitte betritt. Beginnt der Weg schon darin
    /// (ein Betrieb am Markt) oder berührt er den Platz nicht, bleibt er, wie er ist.
    /// </summary>
    private Vector3[] KarrenWegBisPlatzrand(Vector3[] weg, float halb)
    {
        float Rand(Vector3 p) // in Koordinaten des Platzes (er darf gedreht sein)
        {
            var lokal = MarktLokal(p);
            return Math.Max(Math.Abs(lokal.X), Math.Abs(lokal.Z));
        }
        if (weg.Length < 2 || Rand(weg[0]) < halb) return weg;
        var gekuerzt = new List<Vector3> { weg[0] };
        for (var i = 0; i + 1 < weg.Length; i++)
        {
            var (a, b) = (weg[i], weg[i + 1]);
            if (Rand(b) >= halb) { gekuerzt.Add(b); continue; }
            float klein = 0, gross = 1; // zwischen a (außen) und b (innen): wo wird der Rand erreicht?
            for (var n = 0; n < 24; n++)
            {
                var mitte = (klein + gross) / 2;
                if (Rand(a.Lerp(b, mitte)) >= halb) klein = mitte; else gross = mitte;
            }
            gekuerzt.Add(a.Lerp(b, klein));
            return gekuerzt.ToArray();
        }
        return weg;
    }

    /// <summary>Liegt der Karren (samt Tieren) im sichtbaren Ausschnitt der Kamera? Nur dann lohnt sich seine Animation.</summary>
    private bool KarrenImBild(Node3D wurzel) => KarrenImBild(wurzel.GlobalPosition);

    /// <summary>Liegt ein Punkt der Karrenstraße (Weltkoordinaten) im sichtbaren Ausschnitt, mit dem Rand der <see cref="KarrenRandMeter"/>?</summary>
    private bool KarrenImBild(Vector3 ort)
    {
        if (_raum is not null) return false; // Raumansicht: die Stadt ist verdeckt
        var flaeche = GetViewport().GetVisibleRect().Size;
        var rand = KarrenRandMeter * flaeche.Y / _kamera.Size;
        var mitte = ort + new Vector3(0, 1, 0);
        if (_kamera.IsPositionBehind(mitte)) return false;
        var punkt = _kamera.UnprojectPosition(mitte);
        return punkt.X > -rand && punkt.Y > -rand && punkt.X < flaeche.X + rand && punkt.Y < flaeche.Y + rand;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Steigung: bergauf langsamer
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wie sich eine Fahrt auf ihre Zeit verteilt: Der Kern gibt die Stücke der Fahrt über die Karrenstraßen (Länge und Anstieg, <c>Stadtwirtschaft.Fahrten</c>) und
    /// bremst jedes nach seiner Steigung (<c>SteigungDaten.Faktor</c>). Hier steht je Stück, wie viel der Zeit und wie viel des Weges bis zu seinem Ende vorbei
    /// sind (je 0 … 1) – so lässt sich aus dem Anteil der Zeit der Anteil des Weges rechnen und umgekehrt, wie schnell der Karren dort ist.
    /// </summary>
    private sealed record Fahrtprofil(double[] Zeit, double[] Weg)
    {
        /// <summary>Wie weit (0 … 1) ist der Karren auf dem Weg, wenn dieser Anteil der Fahrzeit vorbei ist?</summary>
        public float WegBei(double zeit)
        {
            var i = Stueck(zeit);
            var t = (zeit - Zeit[i]) / Math.Max(1e-9, Zeit[i + 1] - Zeit[i]);
            return (float)(Weg[i] + t * (Weg[i + 1] - Weg[i]));
        }

        /// <summary>Wie schnell (1 = so schnell wie im Durchschnitt der Fahrt) fährt er dort?</summary>
        public float Tempo(double zeit)
        {
            var i = Stueck(zeit);
            return (float)((Weg[i + 1] - Weg[i]) / Math.Max(1e-9, Zeit[i + 1] - Zeit[i]));
        }

        private int Stueck(double zeit)
        {
            zeit = Math.Clamp(zeit, 0, 1);
            var i = Array.BinarySearch(Zeit, zeit);
            if (i < 0) i = ~i - 1;
            return Math.Clamp(i, 0, Zeit.Length - 2);
        }
    }

    /// <summary>Hin- und Rückfahrt eines Betriebs (null: keine Steigung zu berücksichtigen – flache Karte, kein Abschnitt <c>steigung</c> oder keine Fahrt).</summary>
    private readonly Dictionary<Betrieb, (Fahrtprofil Hin, Fahrtprofil Zurueck)?> _fahrtprofile = new(ReferenceEqualityComparer.Instance);

    private (Fahrtprofil Hin, Fahrtprofil Zurueck)? FahrtprofileVon(Betrieb betrieb)
    {
        if (_fahrtprofile.TryGetValue(betrieb, out var bekannt)) return bekannt;
        if (_wirtschaft is not { } wirtschaft) return null;
        // Steigung bremst nur auf Karten mit Höhen; Furten und Fähren halten auf, wo es sie gibt (Schritt 5c, Abschnitt logistik.wasser);
        // der Belag der Wege bremst oder beschleunigt jedes Stück (Schritt 5d, Wegebau.Faktor – wie der Kern die Fahrzeit rechnet)
        var steigung = BodenBewegt && _gelaende is { HatHoehen: true } ? wirtschaft.Daten.Logistik.Steigung : null;
        var wasser = wirtschaft.Karte?.Wasserplan.HaeltAuf == true ? wirtschaft.Daten.Logistik.Wasser : null;
        var belag = wirtschaft.Wegebau.IstAktiv ? wirtschaft.Wegebau : null;
        if (steigung is null && wasser is null && belag is null) return null;
        if (wirtschaft.Fahrten(betrieb) is not var (hin, zurueck)) return null; // noch kein Platz: nicht merken
        var basis = wirtschaft.Daten.Logistik.FahrzeitAb(betrieb.Ort);
        Fahrtprofil? Profil(IReadOnlyList<Reichsgilde.Core.Bauen.Fahrtstueck> fahrt)
        {
            var laenge = fahrt.Sum(s => s.Laenge);
            if (fahrt.Count == 0 || laenge <= 0) return null;
            // Zeit in Spielminuten, wie der Kern sie rechnet: die feste Fahrzeit auf die Stücke nach Länge und Bremse verteilt, dazu die Minuten
            // einer Furt (verteilt auf ihr Stück: langsam durchs Wasser) und einer Fähre (ein Stillstand vor dem Stück: der Karren wartet am Ufer)
            var gewicht = fahrt.Select(s => s.Laenge * (steigung?.Faktor(s.Steigung) ?? 1) * (belag?.Faktor(s) ?? 1)).ToList();
            var summe = gewicht.Sum();
            var (zeit, weg) = (new List<double> { 0 }, new List<double> { 0 });
            var vorher = Reichsgilde.Core.Bauen.Fahrbahn.Strasse;
            for (var i = 0; i < fahrt.Count; i++)
            {
                var art = fahrt[i].Art;
                var wartet = 0.0;
                var langsam = 0.0;
                if (art != vorher && wasser is not null)
                {
                    if (art == Reichsgilde.Core.Bauen.Fahrbahn.Faehre) wartet = wasser.FaehreMinuten;
                    else if (art == Reichsgilde.Core.Bauen.Fahrbahn.Furt) langsam = wasser.FurtMinuten;
                }
                vorher = art;
                if (wartet > 0)
                {
                    zeit.Add(zeit[^1] + wartet);
                    weg.Add(weg[^1]);
                }
                zeit.Add(zeit[^1] + basis * gewicht[i] / summe + langsam);
                weg.Add(weg[^1] + fahrt[i].Laenge / laenge);
            }
            var (zeitSumme, wegSumme) = (zeit[^1], weg[^1]);
            return new Fahrtprofil(zeit.Select(z => z / zeitSumme).ToArray(), weg.Select(w => w / wegSumme).ToArray());
        }
        var (h, z) = (Profil(hin), Profil(zurueck));
        return _fahrtprofile[betrieb] = h is null || z is null ? null : (h, z);
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
        var neu = false;
        if (!_karren.TryGetValue(karren, out var gezeigt))
        {
            neu = true;
            gezeigt = KarrenErzeugen(karren, ansicht.Farbe, nummer);
            gezeigt.Wurzel.Position = ansicht.Hof;
            _karren[karren] = gezeigt;
        }
        var knoten = gezeigt.Wurzel;

        // Mehrere Karren eines Betriebs fahren hintereinander statt ineinander (mit den Tieren sind sie bis zu 6 m lang): Karren Nr. n
        // liegt immer n · KarrenAbstand weiter Richtung Markt als der erste. Am Halteplatz warten sie so in einer Reihe auf dem Weg,
        // und auf der Rückfahrt kommt der erste zuerst an.
        var weg = KarrenWeg(ansicht);
        var laenge = WegLaenge(weg);
        var versatz = laenge > 0 ? nummer * KarrenAbstand / laenge : 0f;
        // Am Markt halten die Karren ebenfalls in der Reihe: der letzte ganz vorn, die übrigen je einen Abstand dahinter
        var spaet = laenge > 0 ? (ansicht.Betrieb.Karren.Count - 1 - nummer) * KarrenAbstand / laenge : 0f;
        float Stelle(float anteil) => Math.Clamp(Math.Min(anteil + versatz, 1f - spaet), 0f, 1f);
        // Der Karren fährt mit fester Geschwindigkeit (Nutzer, 31.10.2026), nicht in der Fahrzeit des Kerns: Er braucht meist länger und kommt später
        // an. Wie der Rückstand aufgelöst wird, steht bei Karrenzug; der Kern bleibt unverändert und gilt, wo keiner hinsieht.
        var (hin, zurueck) = ZugProfileVon(ansicht.Betrieb, karren, ansicht);
        var zug = gezeigt.Zug;
        if (gezeigt.ZugProfile is { } alt && !ReferenceEquals(alt.Hin, hin)) zug.Umstellen(alt.Hin, alt.Zurueck, hin, zurueck); // der Belag hat sich geändert
        gezeigt.ZugProfile = (hin, zurueck);
        var kernAnteil = KernAnteil(karren, ansicht, fahrzeit, jetztGenau);
        var sim = _simulation!;
        if (neu) zug.Abgleichen(karren.Zustand, kernAnteil, hin, zurueck);
        else
        {
            zug.Weiter(sim.Takt.IstPausiert ? 0 : delta * sim.Takt.TicksProSekunde, karren.Zustand, hin, zurueck);
            // Sieht keiner hin (der Karren und der Ort, an dem der Kern ihn hätte, liegen außerhalb des Bildes), gilt der Kern
            if (!KarrenImBild(knoten.GlobalPosition) && !KarrenImBild(ToGlobal(PunktAufWeg(weg, Stelle((float)kernAnteil)))))
                zug.Abgleichen(karren.Zustand, kernAnteil, hin, zurueck);
        }
        gezeigt.Geschwindigkeit = sim.Takt.IstPausiert ? 0 : (float)(zug.MeterJeMinute(hin, zurueck) * sim.Takt.TicksProSekunde);
        Vector3 ziel;
        Vector3 blick;
        if (zug.Etappe == Etappe.Hof && ansicht.KarrenReihe is { } reihe)
        {
            // Umland: am Halteplatz stehen die Karren nebeneinander (4 m, Reihe aus Stadtansicht.Umland.cs), mittig um den Anker verteilt; bei der Abfahrt rücken sie auf den Weg
            ziel = ansicht.Hof + reihe * (nummer - (ansicht.Betrieb.Karren.Count - 1) / 2f);
            blick = ziel + (weg[1] - ansicht.Hof); // alle mit derselben Blickrichtung, nicht zum selben Punkt
        }
        else if (zug.Etappe == Etappe.Hof)
        {
            ziel = PunktAufWeg(weg, Stelle(0));
            blick = PunktAufWeg(weg, Math.Min(1f, Stelle(0) + (laenge > 0 ? 1f / laenge : 0f)));
        }
        else
        {
            var stelle = Stelle((float)zug.Weg(hin, zurueck));
            ziel = PunktAufWeg(weg, stelle);
            blick = PunktAufWeg(weg, Math.Clamp(stelle + (zug.ZumMarkt ? 0.02f : -0.02f), 0f, 1f));
        }

        // Rechtsverkehr (Schritt 5d): unterwegs und am Markt halten sich die Karren rechts, so begegnen sich Hin- und Rückfahrt auf ihren Seiten
        if (zug.Etappe != Etappe.Hof && ansicht.KarrenReihe is null)
        {
            var fahrt = blick - ziel;
            fahrt.Y = 0;
            if (fahrt.LengthSquared() > 1e-6f)
            {
                var rechts = new Vector3(-fahrt.Z, 0, fahrt.X).Normalized() * KarrenSpur;
                ziel += rechts;
                blick += rechts;
            }
        }

        // Auf Gelände fährt der Karren auf dem Boden (Stadtansicht.Gelaende.cs)
        ziel = AufFuss(ziel);
        // Das Ziel wandert dank des Minuten-Bruchteils schon stetig. Das kurze Hinterhergleiten
        // fängt nur noch echte Sprünge ab (Abfahrt vom Hof, Ankunft, Tempowechsel).
        knoten.Position = knoten.Position.Lerp(ziel, (float)Math.Min(1, delta * 10));
        var richtung = blick - knoten.Position;
        richtung.Y = 0;
        if (richtung.LengthSquared() > 0.01f)
        {
            // Am Markt wendet der Karren, statt sich herumzuwerfen; ein neuer Karren steht gleich richtig
            var soll = Mathf.Atan2(richtung.X, richtung.Z);
            knoten.Rotation = new Vector3(0, neu ? soll : Mathf.LerpAngle(knoten.Rotation.Y, soll, (float)Math.Min(1, delta * 6)), 0); // das Nicken kommt gleich
        }
        if (BodenBewegt) // am Hang nickt der Karren mit dem Gefälle (3 m vor und hinter der Achse)
        {
            var vorne = new Vector3(Mathf.Sin(knoten.Rotation.Y), 0, Mathf.Cos(knoten.Rotation.Y));
            var (vorn, hinten) = (FahrHoehe(knoten.Position.X + vorne.X * 1.5f, knoten.Position.Z + vorne.Z * 1.5f), FahrHoehe(knoten.Position.X - vorne.X * 1.5f, knoten.Position.Z - vorne.Z * 1.5f)); // Brücken und Fähren tragen
            var nick = -Mathf.Atan2(vorn - hinten, 3f);
            knoten.Rotation = new Vector3(nick, knoten.Rotation.Y, 0);
        }

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

        KarrenAnimieren(gezeigt, gezeigt.Geschwindigkeit, delta);
        KarrenFahrerBewegen(gezeigt, gezeigt.Geschwindigkeit, delta);
    }

    /// <summary>
    /// Wo der Kern den Karren auf dem Weg hat (0 = Hof, 1 = Markt): aus der Zeit der Fahrt, mit Steigung, Belag und Furten. Nur der Abgleich
    /// braucht das (Stadtansicht.Karren.cs, <see cref="Karrenzug.Abgleichen"/>); gezeigt wird der Karren an der Stelle seines Zuges.
    /// </summary>
    private double KernAnteil(Karren karren, BetriebsAnsicht ansicht, int fahrzeit, double jetztGenau)
    {
        if (karren.Zustand == KarrenZustand.AmBetrieb) return 0;
        var rest = Math.Clamp((karren.Ankunft - jetztGenau) / Math.Max(fahrzeit, 1), 0, 1);
        if (FahrtprofileVon(ansicht.Betrieb) is var (h, z))
            return karren.Zustand == KarrenZustand.ZumMarkt ? h.WegBei(1 - rest) : 1 - z.WegBei(1 - rest);
        return karren.Zustand == KarrenZustand.ZumMarkt ? 1 - rest : rest;
    }

    /// <summary>Die Fahrten mit fester Geschwindigkeit (Hin- und Rückweg) je Betrieb und Karrenart; mit dem Belag neu gerechnet (<c>BelagBild</c>).</summary>
    private readonly Dictionary<(Betrieb, string), (Fahrprofil Hin, Fahrprofil Zurueck)> _zugprofile = new();

    /// <summary>
    /// Das Tempo des Karrens auf der Karrenstraße: <see cref="Karrentempo.Grund"/> der Karrenart auf dem Bezugsbelag, bergauf und auf schlechtem Belag
    /// langsamer (Zeitfaktoren des Kerns), auf gutem Belag höchstens <see cref="Karrentempo.AusbauHoechstens"/> schneller. Furten bremsen, Fähren halten auf
    /// (wie im Kern). Die Länge ist die des gezeigten Weges; sie wird nach den Stücken des Kerns aufgeteilt.
    /// </summary>
    private (Fahrprofil Hin, Fahrprofil Zurueck) ZugProfileVon(Betrieb betrieb, Karren karren, BetriebsAnsicht ansicht)
    {
        if (_zugprofile.TryGetValue((betrieb, karren.Typ), out var bekannt)) return bekannt;
        var laenge = WegLaenge(KarrenWeg(ansicht));
        var sekundenJeMinute = _simulation!.Takt.RundenDauerSekunden / Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag;
        var meterJeMinute = Karrentempo.Grund(karren.Typ) * sekundenJeMinute;
        var fahrten = _wirtschaft?.Fahrten(betrieb);
        var karte = _wirtschaft?.Karte;
        var steigung = karte?.Gelaendeform.HatHoehen == true ? _wirtschaft!.Daten.Logistik.Steigung : null;
        var wasser = karte?.Wasserplan.HaeltAuf == true ? _wirtschaft!.Daten.Logistik.Wasser : null;
        var belag = _wirtschaft?.Wegebau.IstAktiv == true ? _wirtschaft.Wegebau : null;
        double Zeitfaktor(Reichsgilde.Core.Bauen.Fahrtstueck s) => (steigung?.Faktor(s.Steigung) ?? 1) * (belag?.Faktor(s) ?? 1);
        Fahrprofil Profil(IReadOnlyList<Reichsgilde.Core.Bauen.Fahrtstueck>? fahrt) =>
            new(Karrentempo.Stuecke(fahrt, Zeitfaktor, wasser?.FurtMinuten ?? 0, wasser?.FaehreMinuten ?? 0), laenge, meterJeMinute);
        var profile = (Profil(fahrten?.Hin), Profil(fahrten?.Zurueck));
        if (fahrten is not null || karte is null) _zugprofile[(betrieb, karren.Typ)] = profile; // ohne Platz noch nicht merken: er kann kommen
        return profile;
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
