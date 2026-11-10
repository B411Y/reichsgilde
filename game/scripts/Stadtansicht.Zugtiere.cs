using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Zugtiere und Fuhrmann der Karren (Teil der <c>partial class Stadtansicht</c>, Karren-Umbau Teil d): Der Karren ist ein Modell ohne Tier und ohne Figur
/// (<c>Stadtansicht.Karren.cs</c>); Ochse, Pferde und der Mensch am Karren hängen als eigene Figuren an seinen Ankern.
///
/// <para><b>Zugtiere</b> (<c>tiere/tier_&lt;pferd|ochse&gt;_&lt;fell&gt;.glb</c>, Skelett, Regeln in <c>daten/zugtiere.json</c>, gelesen von <see cref="Zugtierdaten"/>):
/// Platzierung: Tier-Wurzel = Position(Anker_Deichsel bzw. Anker_Pferd_L/R) − <c>anker_ziehen_godot</c> der Tierart (der Anker_Geschirr hängt am nie bewegten
/// Knochen „Geschirr“ und liegt in allen Clips fest). Das Fell (zwei je Tier) und die Anfangsstelle der Stehen-Varianten folgen aus dem Fuhrmann des Karrens
/// (<see cref="Karren.FuhrmannIn"/>, nichts davon wird gespeichert, kein Einfluss auf den Kern). Stufen wie die Passanten: nah (Zoom unter 50) ein echtes
/// <see cref="Skeleton3D"/> mit Haarkarten, ab Zoom 50 gebackene Vertex-Animation im <see cref="MultiMesh"/> je Netz (24 Bilder/s für Gehen, Ziehen zeigt es wie Gehen,
/// Stehen 2/s; Shader <c>tier_vat.gdshader</c>, gleicher Zwischenspeicher wie die Passanten), ab Zoom 100 das Fernnetz „Fern_&lt;tier&gt;“.</para>
///
/// <para><b>Ablauf je Tier</b> (<see cref="ZugtierSchritt"/>): Stehen (Varianten Stehen, Ruhen, Umschauen, Fressen, nacheinander ohne Blendzeit, Wahl aus dem Seed des
/// Karrens) → Anfahren_&lt;Gehen|Ziehen&gt; (läuft nach der gefahrenen Strecke, die Hufe rutschen nicht) → Gehen (leer) oder Ziehen (beladen), dazwischen Überblenden
/// mit der Blendzeit der Tierart bei gleicher Abspielstelle → beim Halt den Durchlauf zu Ende (Bild 0), Anhalten_… → Stehen. Das zweite Pferd geht eine halbe
/// Schleife später los (Phasenversatz 0,5), der Ochse ist ein Tier. Die Schritte laufen im Takt der gezeigten Geschwindigkeit.</para>
///
/// <para><b>Fuhrmann</b>: eine Passantenfigur (<see cref="ProbefigurEinsetzen"/>, Rolle „Fuhrmann“) aus <see cref="Karren.FuhrmannIn"/> (Volk, Geschlecht): Handkarren
/// „Ziehen“ (Hände an der Zugstange vorn), Ochsenkarren „Nebenhergehen“ mit dem Stecken, Pferdekarren „SitzenBock“ auf dem Bock. Das Gewand ist gedeckt wie bei allen
/// Passanten; die Hausfarbe tragen nur Plane, Wimpel und Decke am Karren. Nachtregel nach dem Zustand des Karrens (<see cref="Karren.Faehrt"/>), nicht nach der Uhr.</para>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Wo ein Zugtier im Ablauf Stehen, Anfahren, Gehen/Ziehen, Anhalten gerade steht.</summary>
    private enum TierZustand { Stehen, Anfahren, Lauf, Bremsen, Anhalten }

    /// <summary>Ein Zugtier am Karren: der Halter am Karrenmodell, das Modell darunter, sein Spieler und der Ablauf der Clips (<see cref="ZugtierSchritt"/>).</summary>
    private sealed class Zugtier
    {
        public required Node3D Halter { get; init; }
        public required Node3D Modell { get; init; }
        public required AnimationPlayer Spieler { get; init; }
        /// <summary>Art in <see cref="ProbeArten"/> (Tier und Fell).</summary>
        public required int Art { get; init; }
        /// <summary>Regeln der Tierart (Dauer, Weg, Tempo, Blendzeit) aus <c>zugtiere.json</c>.</summary>
        public required Zugtierdaten.Tierart Daten { get; init; }
        /// <summary>Anteil einer Schleife, um den dieses Tier später losgeht (zwei Pferde nie im Gleichschritt; das zweite mit 0,5).</summary>
        public required double Versatz { get; init; }
        /// <summary>Fester Wert aus dem Karren (nicht aus der Zeit): wählt die Stehen-Varianten und ihre Anfangsstelle, ohne den Kern zu berühren.</summary>
        public required uint Seed { get; init; }
        public TierZustand Zustand { get; set; } = TierZustand.Stehen;
        public string Klip { get; set; } = "";
        /// <summary>Abspielstelle (Sekunden) im laufenden Clip; <see cref="Dauer"/> aus <c>zugtiere.json</c>.</summary>
        public double Phase { get; set; }
        public double Dauer { get; set; } = 1;
        /// <summary>Strecke (Meter), die der Karren seit dem Anfahren gefahren ist: Anfahren läuft nach der Strecke, damit die Hufe nicht rutschen.</summary>
        public double Strecke { get; set; }
        /// <summary>Wartezeit vor dem Anfahren (Sekunden): das zweite Pferd geht eine halbe Schleife später los.</summary>
        public double Wartet { get; set; }
        public int StehenNr { get; set; }
        /// <summary>Stelle der Schleife im gebackenen Gehen/Ziehen (Sekunden), läuft auch beim Anfahren und Bremsen mit.</summary>
        public double LaufPhase { get; set; }
        /// <summary>Wurde die Pose zuletzt am Skelett gezeigt (sonst beim nächsten Zeigen auf die Stelle setzen)?</summary>
        public bool Gezeigt { get; set; }
    }

    private static readonly string[] TierStehenVarianten = { "Stehen", "Stehen", "Stehen_Ruhen", "Stehen_Umschauen", "Fressen" };

    /// <summary>Name des Tiers einer Art („pferd“, „ochse“) – so heißen die Netze „Nah_…“ und „Fern_…“.</summary>
    private static string TierNameVon(int art) => ProbeArten[art].Varianten[0];

    /// <summary>
    /// Luft (Meter), um die das Spiel das Tier zusätzlich zur Formel „Tier-Wurzel = Anker − anker_ziehen“ nach vorn (+Z) schiebt, damit zwischen Tier und Karren
    /// Luft bleibt; Wert nach der Messung (<c>--karrenmessung=1</c>, Doku im Bereich darstellung).
    /// </summary>
    private static readonly float ZugtierLuft = ZahlOption("karrenluft", 0f);

    /// <summary>Wie weit (Meter) jedes der zwei Pferde zur Mitte hin vom Anker abrückt, damit die Zugstränge außen an den Flanken Luft haben (Messung <c>--karrenmessung=1</c>).</summary>
    private static readonly float ZugtierSeite = ZahlOption("karrenseite", 0.025f);

    private static readonly bool ZugtierProtokoll = Wasserarten.Startoption("karrenmessung") is "1";

    private static float ZahlOption(string name, float standard) =>
        float.TryParse(Wasserarten.Startoption(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w) ? w : standard;

    /// <summary>Stellt die Zugtiere der Karrenart an ihre Anker; leer ohne Tier (Handkarren) oder ohne Modell.</summary>
    private Zugtier[] ZugtiereEinsetzen(Node3D modell, KarrenArt art, Fuhrmann fuhrmann)
    {
        if (art.Tier is not { } tier || Zugtierdaten.Von(tier) is not { } daten) return Array.Empty<Zugtier>();
        var tiere = new List<Zugtier>();
        var ankerPunkte = new List<Vector3>();
        for (var i = 0; i < art.TierAnker.Length; i++)
        {
            if (modell.FindChild(art.TierAnker[i], true, false) is not Node3D anker || anker.GetParent() is not { } eltern) continue;
            ankerPunkte.Add(anker.Position);
            var fell = (fuhrmann.Startwert >> i) & 1; // beide Pferde können verschiedene Felle haben
            var nummer = (tier == "pferd" ? ProbeErsteTierart : ProbeErsteTierart + 2) + fell;
            if (Modell(ProbeArten[nummer].Datei) is not { } szene) continue;
            var tierModell = szene.Instantiate<Node3D>();
            var halter = new Node3D { Name = $"Zugtier{i + 1}" };
            eltern.AddChild(halter);
            // Tier-Wurzel = Anker_Deichsel (Jochmitte) bzw. Anker_Pferd_L/R − anker_ziehen_godot (Anker_Geschirr am Knochen „Geschirr“, Modellraum des Tiers);
            // beide blicken nach +Z, der Karren ist im Modell nicht gedreht. Die Hufe stehen so auf dem Boden des Karrenmodells.
            halter.Position = anker.Position - daten.AnkerZiehen + new Vector3(art.TierAnker.Length > 1 ? -Math.Sign(anker.Position.X) * ZugtierSeite : 0, 0, ZugtierLuft);
            halter.AddChild(tierModell);
            foreach (var fern in tierModell.FindChildren("Fern_*", nameof(MeshInstance3D), true, false)) fern.QueueFree(); // das Fernnetz zeichnet nur der VAT
            foreach (var teil in tierModell.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
                for (var s = 0; s < (teil.Mesh?.GetSurfaceCount() ?? 0); s++)
                    if (teil.Mesh!.SurfaceGetMaterial(s) is BaseMaterial3D stoff) FarbenEinrichten(stoff); // Eckenfarben (sRGB), wie bei allen Modellen
            var spieler = tierModell.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>().First();
            spieler.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual; // das Spiel setzt die Pose selbst
            Zugtierdaten.KlipsEinrichten(spieler, tier); // Länge = Dauer der Datei, Schleifen linear (Godot nennt sie sonst um 1/24 s zu lang)
            var seed = StabilerHash($"{fuhrmann.Startwert}/{i}/{tier}");
            var t = new Zugtier
            {
                Halter = halter, Modell = tierModell, Spieler = spieler, Art = nummer, Daten = daten, Seed = seed,
                Versatz = art.TierAnker.Length > 1 ? i * 0.5 : 0, // Zweispänner: das zweite Pferd um eine halbe Schleife versetzt
            };
            TierKlipSpielen(t, "Stehen", 0, seed % 1000 / 1000.0 * daten.Klips["Stehen"].Dauer); // zufällige Anfangsstelle: nicht alle im Gleichtakt
            tiere.Add(t);
        }
        if (Wasserarten.Startoption("karrenmessung") is "1") ZugtierMessen(modell, tiere, ankerPunkte, art);
        return tiere.ToArray();
    }

    /// <summary>Beginnt einen Clip: mit der Blendzeit <paramref name="blend"/> und an der Stelle <paramref name="phase"/> (Sekunden).</summary>
    private static void TierKlipSpielen(Zugtier t, string klip, double blend, double phase)
    {
        t.Klip = klip;
        t.Dauer = Math.Max(0.1, t.Daten.Klips[klip].Dauer);
        t.Phase = phase % t.Dauer;
        t.Spieler.Play(klip, blend);
        t.Spieler.Seek(t.Phase, false);
    }

    /// <summary>Die nächste Stehen-Variante, gewählt aus dem Seed des Karrens (kein Zufall der Partie, kein Einfluss auf den Kern).</summary>
    private static string TierNaechsteVariante(Zugtier t)
    {
        t.StehenNr++;
        return TierStehenVarianten[StabilerHash($"{t.Seed}/{t.StehenNr}") % TierStehenVarianten.Length];
    }

    /// <summary>
    /// Ein Schritt des Ablaufs eines Tiers (Regeln: <c>zugtiere.json</c>, <c>regeln</c>). <paramref name="klipSchritt"/>: um wie viele Sekunden der Spieler vorrückt
    /// (0 nach einem Clipwechsel, der die Stelle selbst setzt).
    /// </summary>
    private static void ZugtierSchritt(Zugtier t, bool rollt, bool beladen, float geschwindigkeit, double dt, out double klipSchritt)
    {
        var lauf = beladen ? "Ziehen" : "Gehen";
        var laufDauer = t.Daten.Klips[lauf].Dauer;
        var takt = rollt ? Math.Clamp(geschwindigkeit / t.Daten.Tempo, 0.1, 3.0) : 1.0;
        klipSchritt = dt;
        switch (t.Zustand)
        {
            case TierZustand.Stehen:
                if (rollt)
                {
                    t.Wartet += dt;
                    if (t.Wartet >= t.Versatz * laufDauer)
                    {
                        t.Zustand = TierZustand.Anfahren;
                        t.Strecke = 0;
                        t.LaufPhase = 0;
                        TierKlipSpielen(t, "Anfahren_" + lauf, 0.05, 0);
                        klipSchritt = 0;
                        return;
                    }
                }
                else t.Wartet = 0;
                t.Phase += dt;
                if (t.Phase >= t.Dauer) { TierKlipSpielen(t, TierNaechsteVariante(t), 0, t.Phase - t.Dauer); klipSchritt = 0; }
                break;
            case TierZustand.Anfahren:
            {
                if (!rollt) { t.Zustand = TierZustand.Stehen; t.Wartet = 0; TierKlipSpielen(t, "Stehen", 0.25, 0); klipSchritt = 0; return; }
                t.Strecke += geschwindigkeit * dt;
                t.LaufPhase = (t.LaufPhase + dt * takt) % laufDauer;
                // nach der Strecke: so weit, wie der Karren gefahren ist, höchstens dreimal schneller als die Zeit und nie ganz stehend
                var ziel = Zugtierdaten.ZeitFuerWeg(t.Daten.Klips[t.Klip], t.Strecke);
                var neu = Math.Clamp(ziel, t.Phase + 0.3 * dt, t.Phase + 3.0 * dt);
                klipSchritt = neu - t.Phase;
                t.Phase = neu;
                if (t.Phase >= t.Dauer - 1e-6)
                {
                    t.Zustand = TierZustand.Lauf;
                    TierKlipSpielen(t, lauf, 0, 0); // Anfahren endet in Bild 0 von Gehen/Ziehen
                    t.LaufPhase = 0;
                    klipSchritt = 0;
                }
                break;
            }
            case TierZustand.Lauf:
                if (!rollt) { t.Zustand = TierZustand.Bremsen; goto case TierZustand.Bremsen; }
                if (t.Klip != lauf)
                {
                    // Gehen <-> Ziehen: dieselbe Abspielstelle, Blendzeit je Tierart (die Hufe sind in beiden gleich geplant); das zweite Pferd behält seinen Versatz
                    TierKlipSpielen(t, lauf, t.Daten.BlendGehenZiehen, t.Phase);
                    klipSchritt = 0;
                }
                t.Phase = (t.Phase + dt * takt) % t.Dauer;
                t.LaufPhase = t.Phase;
                klipSchritt = dt * takt;
                break;
            case TierZustand.Bremsen:
            {
                if (rollt) { t.Zustand = TierZustand.Lauf; break; }
                // der Karren steht schon: den Durchlauf rasch zu Ende (Bild 0), dann Anhalten
                var schritt = dt * 3.0;
                if (t.Phase + schritt >= t.Dauer)
                {
                    t.Zustand = TierZustand.Anhalten;
                    TierKlipSpielen(t, "Anhalten_" + (t.Klip == "Ziehen" ? "Ziehen" : "Gehen"), 0, 0);
                    klipSchritt = 0;
                }
                else { t.Phase += schritt; klipSchritt = schritt; t.LaufPhase = t.Phase; }
                break;
            }
            case TierZustand.Anhalten:
                if (rollt)
                {
                    t.Zustand = TierZustand.Stehen;
                    t.Wartet = t.Versatz * laufDauer; // gleich wieder anfahren
                    TierKlipSpielen(t, "Stehen", 0.25, 0);
                    klipSchritt = 0;
                    return;
                }
                t.Phase += dt;
                if (t.Phase >= t.Dauer - 1e-6) { t.Zustand = TierZustand.Stehen; t.Wartet = 0; TierKlipSpielen(t, "Stehen", 0.05, 0); klipSchritt = 0; }
                break;
        }
    }

    /// <summary>Treibt den Ablauf der Zugtiere voran (nach Fahrt und Ladung); Skelett oder gebacken (VAT) nach der Stufe der Passanten.</summary>
    private void ZugtiereBewegen(KarrenAnsicht karren, bool rollt, bool beladen, float geschwindigkeit, double delta, bool imBild)
    {
        var vat = ProbeAktiv && _probeVat && _probeVatNetze is not null;
        var pause = _simulation?.Takt.IstPausiert == true;
        foreach (var tier in karren.Tiere)
        {
            if (tier.Modell.Visible == vat) tier.Modell.Visible = !vat;
            if (pause) continue;
            var vorher = tier.Klip;
            ZugtierSchritt(tier, rollt, beladen, geschwindigkeit, delta, out var schritt);
            if (ZugtierProtokoll && tier.Klip != vorher) GD.Print($"[zugtier] {tier.Halter.GetParent()?.GetParent()?.GetInstanceId()} {tier.Halter.Name}: {vorher} -> {tier.Klip} ({tier.Zustand}), Takt {geschwindigkeit / tier.Daten.Tempo:0.00}, Stelle {tier.Phase:0.00}/{tier.Dauer:0.00} s");
            if (!imBild || vat) { tier.Gezeigt = false; continue; }
            if (!tier.Gezeigt) { tier.Spieler.Seek(tier.Phase, true); tier.Gezeigt = true; } // wieder im Bild: die Pose steht an der richtigen Stelle
            tier.Spieler.Advance(schritt);
        }
    }

    /// <summary>
    /// Misst den kleinsten Abstand zwischen den Zugtieren (Ruhepose, Netz „Nah“) und dem Karrenmodell (Dreiecke, ohne die Zugstrang-Enden 0,3 m um die Anker)
    /// und schreibt ihn ins Protokoll (<c>--karrenmessung=1</c>), dazu den Anker im Geschirr gegen den Anker des Karrens.
    /// </summary>
    private static readonly float AusschlussRadius = float.TryParse(Wasserarten.Startoption("karrenausschluss"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 0.3f;

    private void ZugtierMessen(Node3D modell, List<Zugtier> tiere, List<Vector3> ankerPunkte, KarrenArt art)
    {
        var tierKnoten = tiere.SelectMany(t => t.Modell.FindChildren("*", nameof(MeshInstance3D), true, false)).ToHashSet();
        var ankerWelt = ankerPunkte.Select(a => modell.ToGlobal(a)).ToList(); // die Anker liegen im Modell (eltern = Modellwurzel)
        foreach (var t in tiere)
            if (t.Modell.FindChild("Anker_Geschirr", true, false) is Node3D g)
                GD.Print($"[zugtier] {art.Datei} {t.Halter.Name}: Anker_Geschirr {g.GlobalPosition} gegen Karrenanker {string.Join(" / ", ankerWelt)} " +
                         $"(Abweichung {ankerWelt.Min(a => a.DistanceTo(g.GlobalPosition)) * 1000:0.0} mm)");
        var dreiecke = new List<(Vector3 A, Vector3 B, Vector3 C, string Teil)>();
        foreach (var teil in modell.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (tierKnoten.Contains(teil) || teil.Mesh is not { } netz || teil.Name.ToString().StartsWith("Rad")) continue;
            if (teil.GetParent() is Node3D eltern && tiere.Any(t => t.Halter == eltern || t.Modell == eltern)) continue;
            for (var s = 0; s < netz.GetSurfaceCount(); s++)
            {
                var arrays = netz.SurfaceGetArrays(s);
                var v = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var idx = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                for (var k = 0; k + 2 < idx.Length; k += 3)
                {
                    var (a, b, c) = (teil.GlobalTransform * v[idx[k]], teil.GlobalTransform * v[idx[k + 1]], teil.GlobalTransform * v[idx[k + 2]]);
                    var mitte = (a + b + c) / 3f;
                    if (ankerWelt.Any(p => p.DistanceTo(mitte) < AusschlussRadius)) continue;
                    dreiecke.Add((a, b, c, teil.Name.ToString()));
                }
            }
        }
        var jeTeil = new Dictionary<string, (double D, Vector3 Wo)>();
        foreach (var t in tiere)
            foreach (var teil in t.Modell.FindChildren("Nah*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            {
                if (teil.Mesh is not { } netz) continue;
                var v = netz.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                for (var i = 0; i < v.Length; i++)
                {
                    var p = teil.GlobalTransform * v[i];
                    foreach (var (a, b, c, name) in dreiecke)
                    {
                        var d = AbstandPunktDreieck(p, a, b, c);
                        if (!jeTeil.TryGetValue(name, out var alt) || d < alt.D) jeTeil[name] = (d, p);
                    }
                }
            }
        // dieselbe Messung in Bewegung: das Netz mit Haut (CPU) in 8 Stellen von Ziehen und Gehen; der Karren steht, die Zugstränge geben nicht mit
        foreach (var klip in new[] { "Gehen", "Ziehen" })
        {
            var kleinst = (D: double.MaxValue, Name: "", Wo: Vector3.Zero);
            for (var ph = 0; ph < 8; ph++)
                foreach (var t in tiere)
                {
                    t.Spieler.Play(klip, 0);
                    t.Spieler.Seek(ph / 8.0 * t.Daten.Klips[klip].Dauer, true);
                    var nah = dreiecke.Where(d => d.A.DistanceTo(t.Modell.GlobalPosition) < 3.5f).ToList();
                    foreach (var teil in t.Modell.FindChildren("Nah*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
                        foreach (var p in HautPunkte(teil))
                            foreach (var (a, b, c, name) in nah)
                            {
                                var d = AbstandPunktDreieck(p, a, b, c);
                                if (d < kleinst.D) kleinst = (d, name, p);
                            }
                }
            GD.Print($"[zugtier] {art.Datei}: in {klip} (8 Stellen, Haut gerechnet) kleinster Abstand zu {kleinst.Name} {kleinst.D * 1000:0.0} mm bei {kleinst.Wo} (Ausschluss {AusschlussRadius} m um die Anker)");
        }
        foreach (var t in tiere) { t.Spieler.Play("Stehen", 0); t.Spieler.Seek(t.Phase, true); }
        foreach (var (name, (d, wo)) in jeTeil.OrderBy(k => k.Value.D))
            GD.Print($"[zugtier] {art.Datei}: kleinster Abstand Tier – Karrenteil {name} (Ruhepose, ohne Zugstrang-Enden 0,3 m um die Anker) {d * 1000:0.0} mm bei {wo}, Zuschlag {ZugtierLuft * 1000:0} mm");
    }

    /// <summary>Die Ecken eines Skelett-Netzes in der gerade gezeigten Haltung (Weltkoordinaten), von Hand gehäutet: Σ Gewicht · (Skelett · Knochenhaltung · Bindepose) · Ecke.</summary>
    private static IEnumerable<Vector3> HautPunkte(MeshInstance3D teil)
    {
        var skelett = teil.GetNodeOrNull<Skeleton3D>(teil.Skeleton) ?? teil.GetParent() as Skeleton3D;
        if (skelett is null || teil.Skin is not { } haut || teil.Mesh is not { } netz) yield break;
        var arrays = netz.SurfaceGetArrays(0);
        var ecken = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var knochen = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
        var gewichte = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
        var anzahl = knochen.Length / ecken.Length; // 4 oder 8 Knochen je Ecke
        var matrizen = new Transform3D[haut.GetBindCount()];
        for (var b = 0; b < matrizen.Length; b++)
        {
            var nr = haut.GetBindBone(b);
            if (nr < 0) nr = skelett.FindBone(haut.GetBindName(b));
            matrizen[b] = nr < 0 ? Transform3D.Identity : skelett.GlobalTransform * skelett.GetBoneGlobalPose(nr) * haut.GetBindPose(b);
        }
        for (var i = 0; i < ecken.Length; i++)
        {
            var p = Vector3.Zero;
            for (var k = 0; k < anzahl; k++)
            {
                var w = gewichte[i * anzahl + k];
                if (w > 0) p += w * (matrizen[knochen[i * anzahl + k]] * ecken[i]);
            }
            yield return p;
        }
    }

    /// <summary>Kürzester Abstand eines Punktes zu einem Dreieck (Ericson, Real-Time Collision Detection).</summary>
    private static double AbstandPunktDreieck(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        var ab = b - a; var ac = c - a; var ap = p - a;
        var d1 = ab.Dot(ap); var d2 = ac.Dot(ap);
        if (d1 <= 0 && d2 <= 0) return p.DistanceTo(a);
        var bp = p - b; var d3 = ab.Dot(bp); var d4 = ac.Dot(bp);
        if (d3 >= 0 && d4 <= d3) return p.DistanceTo(b);
        var vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) return p.DistanceTo(a + ab * (d1 / (d1 - d3)));
        var cp = p - c; var d5 = ab.Dot(cp); var d6 = ac.Dot(cp);
        if (d6 >= 0 && d5 <= d6) return p.DistanceTo(c);
        var vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) return p.DistanceTo(a + ac * (d2 / (d2 - d6)));
        var va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return p.DistanceTo(b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6))));
        var denom = 1f / (va + vb + vc);
        return p.DistanceTo(a + ab * (vb * denom) + ac * (vc * denom));
    }

    /// <summary>Seitlicher Abstand (Meter) des Läufers von der Mitte des Ochsen und sein Vorsprung (Meter) vor dem Joch: neben Schulter und Kopf.</summary>
    private const float KarrenLaeuferAbstand = 1.1f;
    private const float KarrenLaeuferVorn = 0.9f;

    /// <summary>
    /// Stellt den Fuhrmann an den Anker des Karrens (Handkarren: <c>Anker_Ziehen</c> vor der Zugstange, Ochsenkarren: <c>Anker_Fahrer_Seite</c>, Pferdekarren:
    /// <c>Anker_Bock</c>, die Hüfte auf der Sitzfläche). Der Anker blickt mit −Z, die Figur mit +Z: Ein Halter am Anker dreht sie um 180° um die Hochachse.
    /// Ohne Passantenfiguren (<c>--passantenprobe=0</c>) gibt es keinen Fuhrmann am Karren.
    /// </summary>
    private Figur? KarrenFuhrmannEinsetzen(KarrenAnsicht ansicht, Node3D modell, KarrenArt art, Fuhrmann fuhrmann)
    {
        if (!ProbeAktiv || modell.FindChild(art.ZieherAnker, true, false) is not Node3D anker || anker.GetParent() is not { } eltern) return null;
        var halter = new Node3D { Name = "Fuhrmann" };
        eltern.AddChild(halter);
        halter.Transform = anker.Transform.RotatedLocal(Vector3.Up, Mathf.Pi);
        if (art.ZieherClip == "Nebenhergehen" && art.TierAnker.Length > 0 && modell.FindChild(art.TierAnker[0], true, false) is Node3D joch)
        {
            // Der Läufer geht neben dem Ochsen auf Höhe von Schulter und Kopf (nicht am Wagen): seitlich 1,1 m von der Mitte des Tiers, 0,9 m vor dem Joch; die Höhe bleibt die des Ankers (Boden)
            var seite = anker.Position.X >= 0 ? 1f : -1f;
            halter.Position = new Vector3(seite * KarrenLaeuferAbstand, anker.Position.Y, joch.Position.Z + KarrenLaeuferVorn);
        }
        var frau = fuhrmann.Geschlecht == Geschlecht.Frau;
        var haare = Haarfarben(fuhrmann.Volk);
        // Gewand, Haut und Haar wählt die Probefigur selbst (gedeckte Pflanzenfarben, nach der Nummer); die Hausfarbe trägt nur der Karren
        var aussehen = new FigurAussehen(fuhrmann.Volk, frau, Gewandfarben[0], Beinkleidfarben[0], HautVon(fuhrmann.Volk), haare[fuhrmann.Startwert % haare.Length], Leinenfarbe,
            Kopfbedeckung.Strohhut, art.ZieherClip == "Nebenhergehen" ? Last.Stab : Last.Keine);
        var figur = ProbefigurEinsetzen(halter, aussehen, fuhrmann.Startwert, "Fuhrmann");
        if (figur is null) { halter.QueueFree(); return null; }
        if (art.ZieherClip == "SitzenBock") halter.Position -= new Vector3(0, figur.Kniehoehe - 0.02f, 0); // die Hüfte sitzt auf der Sitzfläche
        ansicht.FahrerHalter = halter;
        halter.Visible = false; // gezeigt wird er erst, wenn der Karren fährt (KarrenFuhrmannBewegen)
        ansicht.FahrerSichtbar = false;
        return figur;
    }

    /// <summary>
    /// Der Fuhrmann bewegt sich mit: fahrend „Ziehen“, „Nebenhergehen“ bzw. auf dem Bock „SitzenBock“ im Takt des Karrens (die Schritte passen zum Tempo), am Halt
    /// „Stehen“ (auf dem Bock bleibt er sitzen). Am Betrieb und am geschlossenen Tor ist er nicht da.
    /// </summary>
    private void KarrenFuhrmannBewegen(KarrenAnsicht karren, bool faehrt, bool rollt, float geschwindigkeit, double delta)
    {
        if (karren.Fahrer is not { } fahrer || karren.FahrerHalter is not { } halter) return;
        if (karren.FahrerSichtbar != faehrt)
        {
            karren.FahrerSichtbar = faehrt;
            halter.Visible = faehrt;
            fahrer.Neu = true; // erscheint er wieder, steht die Pose gleich
        }
        if (!faehrt) return;
        var clip = karren.Art.ZieherClip;
        if (clip == "SitzenBock") BewegungSetzen(fahrer, "SitzenBock");
        else if (rollt) BewegungSetzen(fahrer, clip, Mathf.Min(geschwindigkeit / (GehenTempo * fahrer.Beinfaktor), 3f));
        else BewegungSetzen(fahrer, "Stehen");
        var sekunden = _simulation?.Takt.IstPausiert == true ? 0 : delta; // in der Pause steht auch der Mensch still
        FigurImBildBewegen(fahrer, karren.Wurzel.Position, sekunden);
    }

    /// <summary>Trägt Zugtiere und Fuhrleute der Karren in die MultiMeshes der gebackenen Stufe ein (aus <see cref="ProbeVatZeichnen"/>).</summary>
    private void KarrenVatEintragen()
    {
        foreach (var karren in _karren.Values)
        {
            if (!karren.Wurzel.Visible || !ImBild(karren.Wurzel.Position, 4f)) continue;
            if (karren.Fahrer is { } fahrer && karren.FahrerSichtbar) ProbeVatEintragen(fahrer, karren.Wurzel.Position);
            foreach (var tier in karren.Tiere)
            {
                // gebacken sind Stehen (alle Stehen-Varianten und Fressen), Gehen und Ziehen; Anfahren und Bremsen zeigen Gehen/Ziehen im Takt der Fahrt
                var stehen = tier.Zustand is TierZustand.Stehen or TierZustand.Anhalten;
                var nr = stehen ? 0 : 1; // Ziehen zeigt das gebackene Tier wie Gehen (siehe TierKlips)
                var dauer = tier.Daten.Klips[nr == 0 ? "Stehen" : "Gehen"].Dauer;
                var phase = stehen ? tier.Phase : tier.LaufPhase;
                var (_, bilder) = _probeKlipZeilen[tier.Art][nr];
                var bild = (float)(phase % dauer / dauer * bilder);
                Eintragen($"{tier.Art}:{(_probeFern ? "Fern" : "Nah")}_{TierNameVon(tier.Art)}", tier.Modell.GlobalTransform, bild, nr, 0f);
                _probeGezeichnet++;
            }
        }
    }
}
