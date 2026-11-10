using Godot;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Karren (Teil der <c>partial class Stadtansicht</c>): Handkarren, Ochsenkarren und Pferdekarren als Modelle aus Blender
/// (<c>karren/karren_hand.glb</c>, <c>karren_ochse.glb</c>, <c>karren_pferd.glb</c>, Fahrtrichtung +Z) – ohne Tier und ohne Figur, mit Rädern als
/// eigenen Knoten (<c>Rad_*</c>), die sich nach der gefahrenen Strecke drehen. Zieher und Zugtiere sind eigene Figuren an den Ankern
/// (<c>Stadtansicht.Zugtiere.cs</c>): ein Passant aus dem Fuhrmann des Karrens, Ochse oder Pferde mit Skelett (nah), gebacken (ab Zoom 50) und als Fernnetz (ab 100).
///
/// Die Darstellung rechnet nichts aus, sie liest nur den <see cref="Karren"/> des Kerns: Zustand (fährt, steht am Betrieb oder am Tor), Ladung (wie viele Sorten),
/// Besitzer (Farbe von Plane, Wimpel und Decke) und den Fuhrmann (<see cref="Karren.FuhrmannIn"/>). <see cref="KarrenBewegen"/> schiebt den Karren und stellt das
/// Tempo der Räder, Schritte und Hufe so ein, dass nichts rutscht. Karren außerhalb des Bildes werden nicht animiert.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Ein Karren in der Stadtansicht: der Knoten mit Ort und Drehung, das Modell darunter und alles, was wir davon schalten.</summary>
    private sealed class KarrenAnsicht
    {
        public required Node3D Wurzel { get; init; }
        public required KarrenArt Art { get; init; }
        /// <summary>Die Räder des Modells (Knoten, Ruhelage, Halbmesser, Drehwinkel): <see cref="KarrenRaederDrehen"/>.</summary>
        public Rad[] Raeder { get; init; } = Array.Empty<Rad>();
        /// <summary>Die Stützbeine des Handkarrens (nur im Stand gezeigt) und ob sie gerade sichtbar sind.</summary>
        public Node3D? Stuetze { get; init; }
        public bool StuetzeSichtbar { get; set; }
        /// <summary>Die Zugtiere (Ochse, ein oder zwei Pferde) an den Ankern des Karrens.</summary>
        public Zugtier[] Tiere { get; set; } = Array.Empty<Zugtier>();
        /// <summary>Der Halter des Ziehers am Anker (null ohne Figur) und ob er gerade gezeigt wird (Nachtregel: nur ein fahrender Karren hat seinen Fuhrmann bei sich).</summary>
        public Node3D? FahrerHalter { get; set; }
        public bool FahrerSichtbar { get; set; } = true;
        /// <summary>„Ladung1“ bis „LadungN“ des Modells (Kisten, Säcke, Fässer): so viele sichtbar, wie Sorten geladen sind.</summary>
        public required MeshInstance3D[] Ladung { get; init; }
        public MeshInstance3D? Plane { get; init; }
        public required Node3D Eskorte { get; init; }
        /// <summary>Wie viele Ladungsknoten gerade sichtbar sind (-1 = noch nie gesetzt); geschaltet wird nur beim Wechsel.</summary>
        public int GezeigteLadung { get; set; } = -1;
        public bool Eskortiert { get; set; }
        /// <summary>Versetzt die Bilder, in denen Karren animiert werden, damit nicht alle im selben Bild dran sind.</summary>
        public int Versatz { get; init; }
        /// <summary>Der Fuhrmann am Karren (Zieher, Läufer am Ochsen, Kutscher), eine Passantenfigur mit Skelett; null, wenn das Figurenmodell oder der Kern fehlt.</summary>
        public Figur? Fahrer { get; set; }
        /// <summary>Die sichtbare Fahrt mit fester Geschwindigkeit (Stadtansicht.Karren.cs, <see cref="Karrenzug"/>) und die Profile, mit denen sie zuletzt gerechnet wurde.</summary>
        public Karrenzug Zug { get; } = new();
        public (Fahrprofil Hin, Fahrprofil Zurueck)? ZugProfile { get; set; }
        /// <summary>Wie schnell der Karren im letzten Bild fuhr (Meter je echter Sekunde): treibt Räder und Schritte.</summary>
        public float Geschwindigkeit { get; set; }
        /// <summary>Hufklang (Stadtansicht.KarrenKlang.cs): der Untergrund unter dem Karren, wann er zuletzt gelesen wurde (echte Sekunden) und der Betrieb für das Protokoll.</summary>
        public KlangKarren.Untergrund KlangBoden { get; set; } = KlangKarren.Untergrund.Erde;
        public double KlangGelesen { get; set; } = double.NegativeInfinity;
        public string KlangName { get; set; } = "";
        public string KlangWo { get; set; } = "";
        public Vector3[]? KlangProbeWeg { get; set; }
        public string KlangProbeText { get; set; } = "";
        /// <summary>Messung der sichtbaren Hinfahrt: Spielminute des Starts (−1 keine läuft, −2 ungültig: der Karren war aus dem Bild oder wurde abgeglichen) und die gezeigte Strecke in Metern.</summary>
        public double HinStart { get; set; } = -1;
        public float HinStrecke { get; set; }
        public float HinLetzte { get; set; }
    }

    /// <summary>Ein Rad des Karrens: der Knoten und seine Ruhelage, der Halbmesser (Meter) und der gesamte Drehwinkel (Bogenmaß) um die lokale X-Achse.</summary>
    private sealed class Rad
    {
        public required Node3D Knoten { get; init; }
        public required Transform3D Ruhe { get; init; }
        public required float Radius { get; init; }
        public float Winkel { get; set; }
    }

    /// <summary>
    /// Was an einer Karrenart verschieden ist: das Modell, die Räder (Knotennamen, Halbmesser nach <c>tools/modelle/karren.py</c>, falls das Modell ihn nicht als
    /// Extra <c>radius</c> mitbringt), der Anker und der Clip des Ziehers, die Zugtierart (<c>pferd</c>, <c>ochse</c>; null: keines) mit ihren Ankern.
    /// </summary>
    private sealed record KarrenArt(string Datei, (string Name, float Radius)[] Raeder, string ZieherAnker, string ZieherClip, string? Tier, string[] TierAnker, bool Pferd = false);

    /// <summary>
    /// Die drei Karrenarten, nach <see cref="Karren.Typ"/>. Der Handkarren wird gezogen (Nutzer, 10.11.2026): der Zieher steht an der Zugstange vorn
    /// (<c>Anker_Ziehen</c>, Hände hinter dem Körper, Clip „Ziehen“); am Ochsenkarren geht der Fuhrmann mit dem Stecken neben dem Ochsen
    /// („Nebenhergehen“), am Pferdekarren sitzt er auf dem Bock („SitzenBock“, auch beim Halt).
    /// </summary>
    private static readonly Dictionary<string, KarrenArt> KarrenArten = new()
    {
        ["Handkarren"] = new("karren/karren_hand", new[] { ("Rad_L", 0.45f), ("Rad_R", 0.45f) }, "Anker_Ziehen", "Ziehen", null, Array.Empty<string>()),
        ["Ochsenkarren"] = new("karren/karren_ochse", new[] { ("Rad_L", 0.72f), ("Rad_R", 0.72f) }, "Anker_Fahrer_Seite", "Nebenhergehen", "ochse", new[] { "Anker_Deichsel" }),
        ["Pferdekarren"] = new("karren/karren_pferd", new[] { ("Rad_HL", 0.62f), ("Rad_HR", 0.62f), ("Rad_VL", 0.5f), ("Rad_VR", 0.5f) }, "Anker_Bock", "SitzenBock", "pferd",
            new[] { "Anker_Pferd_L", "Anker_Pferd_R" }, Pferd: true),
    };

    /// <summary>So weit (in Metern) darf ein Karren außerhalb des Bildes stehen und wird noch animiert (seine Tiere sind rund 5 m lang).</summary>
    private const float KarrenRandMeter = 6f;

    /// <summary>So weit (Meter) stehen wartende Karren hintereinander: ein Ochsenkarren ist mit Ochse 5 m lang, ein Pferdekarren mit zwei Pferden 6 m.</summary>
    private const float KarrenAbstand = 7f;

    /// <summary>So weit (Meter) neben der Mitte des Weges fahren die Karren: auf ihrer rechten Seite (Rechtsverkehr, Schritt 5d).</summary>
    private const float KarrenSpur = 0.9f;

    private KarrenAnsicht KarrenErzeugen(Karren karren, Color besitzer, int nummer)
    {
        var art = KarrenArten.GetValueOrDefault(karren.Typ) ?? KarrenArten["Handkarren"];
        var wurzel = new Node3D { Name = "Karren" };
        AddChild(wurzel);
        // Zustand: nur die Hausfarbe (Besitzer); „Ladung1–3“, „Plane“ und „Stuetze“ schaltet KarrenBewegen
        var modell = ModellEinsetzen(art.Datei, wurzel, Vector3.Zero, new ModellZustand(Besitzer: besitzer))?.Wurzel;
        var ladung = new List<MeshInstance3D>();
        MeshInstance3D? plane = null;
        var raeder = new List<Rad>();
        Node3D? stuetze = null;
        if (modell is not null)
        {
            for (var i = 1; modell.FindChild($"Ladung{i}", true, false) is MeshInstance3D teil; i++)
            {
                teil.Visible = false;
                ladung.Add(teil);
            }
            plane = modell.FindChild("Plane", true, false) as MeshInstance3D;
            if (plane is not null) plane.Visible = false;
            stuetze = modell.FindChild("Stuetze", true, false) as Node3D;
            if (stuetze is not null) stuetze.Visible = false;
            foreach (var (name, radius) in art.Raeder)
                if (modell.FindChild(name, true, false) is Node3D rad)
                    raeder.Add(new Rad { Knoten = rad, Ruhe = rad.Transform, Radius = RadHalbmesser(rad, radius) });
        }
        // Die Eskorte bleibt vorerst ein Begleiter neben dem Karren
        var eskorte = new MeshInstance3D { Name = "Eskorte", Mesh = new CapsuleMesh { Radius = 0.35f, Height = 1.8f }, MaterialOverride = Material(new Color(0.35f, 0.35f, 0.38f)), Position = new Vector3(1.8f, 0.9f, 0.6f), Visible = false };
        wurzel.AddChild(eskorte);
        var ansicht = new KarrenAnsicht
        {
            Wurzel = wurzel, Art = art, Raeder = raeder.ToArray(), Stuetze = stuetze, Ladung = ladung.ToArray(), Plane = plane, Eskorte = eskorte, Versatz = nummer,
            KlangName = $"{karren.Heimat.Name} {karren.Typ} {nummer + 1}",
        };
        if (modell is not null && _wirtschaft is { } w)
        {
            var fuhrmann = karren.FuhrmannIn(w); // abgeleitet aus Haus und Karren: derselbe Karren hat immer denselben Fuhrmann und dieselben Fellfarben
            ansicht.Tiere = ZugtiereEinsetzen(modell, art, fuhrmann);
            ansicht.Fahrer = KarrenFuhrmannEinsetzen(ansicht, modell, art, fuhrmann);
        }
        return ansicht;
    }

    /// <summary>Der Halbmesser eines Rades: das Extra <c>radius</c> des Modells (aus <c>karren.py</c>), sonst der Wert der Karrenart.</summary>
    private static float RadHalbmesser(Node3D rad, float standard) =>
        rad.HasMeta("extras") && rad.GetMeta("extras").AsGodotDictionary() is { } extras && extras.ContainsKey("radius") ? (float)extras["radius"].AsDouble() : standard;

    /// <summary>
    /// Die Räder drehen sich nach der gefahrenen Strecke: Winkel = Weg ÷ Halbmesser um die lokale X-Achse (vorwärts ist +Z, die Oberseite läuft nach vorn). Nur wenn
    /// der Karren im Bild ist und fährt; das Rad bleibt sonst, wie es ist.
    /// </summary>
    private void KarrenRaederDrehen(KarrenAnsicht karren, float geschwindigkeit, double delta)
    {
        if (karren.Raeder.Length == 0 || geschwindigkeit <= 0.05f || _simulation?.Takt.IstPausiert == true) return;
        var meter = Mathf.Min(geschwindigkeit, KarrenMaxTempo) * (float)delta; // bei sehr hohem Tempo rutschen die Räder lieber, als zu flimmern
        var sichtbar = KarrenImBild(karren.Wurzel);
        foreach (var rad in karren.Raeder)
        {
            rad.Winkel = Mathf.PosMod(rad.Winkel + meter / rad.Radius, Mathf.Tau);
            if (sichtbar) rad.Knoten.Transform = rad.Ruhe.RotatedLocal(Vector3.Right, rad.Winkel);
        }
    }

    /// <summary>Schneller als so viele Meter je Sekunde drehen sich die Räder nicht mehr mit (<see cref="KarrenRaederDrehen"/>).</summary>
    private const float KarrenMaxTempo = 12f;

    /// <summary>Ein Hash, der bei jedem Programmstart gleich ist (<c>string.GetHashCode</c> ist es nicht).</summary>
    private static uint StabilerHash(string text)
    {
        var h = 2166136261u;
        foreach (var c in text) h = (h ^ c) * 16777619u;
        return h;
    }

    /// <summary>
    /// Wie weit (Chebyshev-Abstand von der Platzmitte, Meter) die Karren am Markt halten: Das Pflaster reicht bis 12 m, die Tiere stehen am Rand
    /// (<see cref="Reichsgilde.Core.Bauen.Karrenweg.HaltVorPflaster"/> davor, wie im Kern). Nur noch für den Weg aus dem Gassennetz (alte Daten).
    /// </summary>
    private static float KarrenHaltAmMarkt(MarktplatzAnsicht platz) => platz.Halb + (float)Reichsgilde.Core.Bauen.Karrenweg.HaltVorPflaster;

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Vector3[], Vector3[]> _karrenWege = new();

    /// <summary>
    /// Der Weg des Karrens zum Markt seines Betriebs. Der Weg des Kerns (<see cref="BetriebsAnsicht.WegVomKern"/>) endet schon am Halt und bleibt, wie er ist.
    /// Der Weg aus dem Gassennetz (alte Daten) reicht bis zur Platzmitte (das Gassennetz kennt den Brunnen als Knoten) und wird auf den Halt am Rand des
    /// Pflasters gekürzt (<see cref="KarrenWegBisPlatzrand"/>); je Weg nur einmal gerechnet.
    /// </summary>
    private Vector3[] KarrenWeg(BetriebsAnsicht ansicht, bool zumHafen = false)
    {
        // Datenversion 73: ein Karren auf dem Weg zum Hafen fährt den Weg des Kerns dorthin (endet am Halt vor dem Hafen)
        if (zumHafen && HafenWeg(ansicht) is { } hafen) return hafen;
        if (ansicht.WegVomKern || ansicht.Markt is not { } platz) return ansicht.WegZumMarkt;
        if (!_karrenWege.TryGetValue(ansicht.WegZumMarkt, out var weg))
            _karrenWege.Add(ansicht.WegZumMarkt, weg = KarrenWegBisPlatzrand(ansicht.WegZumMarkt, platz, KarrenHaltAmMarkt(platz)));
        return weg;
    }

    /// <summary>Der Weg des Kerns zum Hafen (<see cref="Stadtwirtschaft.KarrenverlaufZumHafen"/>), je Betrieb einmal geholt; null ohne Weg dorthin.</summary>
    private Vector3[]? HafenWeg(BetriebsAnsicht ansicht) =>
        ansicht.WegZumHafen ??= _wirtschaft?.KarrenverlaufZumHafen(ansicht.Betrieb) is { Count: >= 2 } punkte
            ? punkte.Select(p => new Vector3((float)p.X, 0, (float)p.Z)).ToArray() : null;

    /// <summary>Fährt (bzw. fuhr zuletzt) dieser Karren zum Hafen, und gibt es den Weg dorthin? (Datenversion 73, <see cref="Karren.ZielHafen"/>)</summary>
    private bool ZumHafen(Karren karren, BetriebsAnsicht ansicht) => karren.ZielHafen && HafenWeg(ansicht) is not null;

    /// <summary>
    /// Der Weg endet in der Platzmitte (im Brunnen): Karren halten und wenden dort, wo ihre Zufahrtsgasse auf den Platz trifft. Schneidet den Weg
    /// an dem Punkt ab, an dem er das Quadrat mit der halben Seite <paramref name="halb"/> um die Mitte des Platzes <paramref name="platz"/> betritt. Beginnt der
    /// Weg schon darin (ein Betrieb am Markt) oder berührt er den Platz nicht, bleibt er, wie er ist.
    /// </summary>
    private static Vector3[] KarrenWegBisPlatzrand(Vector3[] weg, MarktplatzAnsicht platz, float halb)
    {
        float Rand(Vector3 p) // in Koordinaten des Platzes (er darf gedreht sein)
        {
            var lokal = platz.Lokal(p);
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

    /// <summary>
    /// Einmal je Bild und Karren: Räder drehen, Stützbeine des Handkarrens im Stand zeigen, Zugtiere und Fuhrmann bewegen. Nachtregel nach dem
    /// <b>Zustand</b> des Karrens, nicht nach der Uhr: am Betrieb oder am geschlossenen Tor steht der Karren ohne Fuhrmann (<see cref="Karren.Faehrt"/>), das Zugtier
    /// steht angebunden (Clip „Stehen“); sobald er fährt, ist der Fuhrmann wieder da.
    /// </summary>
    private void KarrenAnimieren(Karren karren, KarrenAnsicht ansicht, float geschwindigkeit, double delta)
    {
        var faehrt = karren.Faehrt(_simulation!.Zeit);
        var rollt = faehrt && geschwindigkeit > 0.05f;
        KarrenRaederDrehen(ansicht, rollt ? geschwindigkeit : 0f, delta);
        if (ansicht.Stuetze is { } stuetze && ansicht.StuetzeSichtbar != !rollt)
        {
            ansicht.StuetzeSichtbar = !rollt; // der Handkarren steht auf seinen Stützbeinen, solange er nicht rollt
            stuetze.Visible = !rollt;
        }
        var beladen = karren.Ladung.Values.Any(l => l > 0);
        var imBild = _raum is null && KarrenImBild(ansicht.Wurzel);
        ZugtiereBewegen(ansicht, rollt, beladen, geschwindigkeit, delta, imBild);
        KarrenFuhrmannBewegen(ansicht, faehrt, rollt, geschwindigkeit, delta);
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
        // Der Weg führt zu dem Platz, zu dem der Betrieb gehört (wie im Kern), und endet am Halt am Rand seines Pflasters
        // Zum Marktplatz oder – Datenversion 73 – zum Hafen: der Weg, den der Kern dieser Fahrt gibt
        var zumHafen = ZumHafen(karren, ansicht);
        var weg = KarrenWeg(ansicht, zumHafen);
        if (KarrenKlangTest) gezeigt.KlangProbeWeg = weg; // nur mit --karrenklangtest=1 (Stadtansicht.KarrenKlang.cs)
        var laenge = WegLaenge(weg);
        var versatz = laenge > 0 ? nummer * KarrenAbstand / laenge : 0f;
        // Am Markt halten die Karren ebenfalls in der Reihe: der letzte ganz vorn, die übrigen je einen Abstand dahinter
        var spaet = laenge > 0 ? (ansicht.Betrieb.Karren.Count - 1 - nummer) * KarrenAbstand / laenge : 0f;
        float Stelle(float anteil) => Math.Clamp(Math.Min(anteil + versatz, 1f - spaet), 0f, 1f);
        // Der Karren fährt im Tempo seiner Art (Nutzer, 31.10.2026: Ochse 0,8, Hand 1,0, Pferd 1,3 m/s bei Tempo 1) – nie gestreckt: Der Kern
        // rechnet seine Fahrzeit seit Datenversion 65 nach Weglänge mit demselben Tempo und auf demselben Weg (Halteplatz, Hof, Zubringer, Wege,
        // Halt vor dem Pflaster), bei der Standard-Rundendauer kommen beide bis auf die Rundung auf ganze Minuten zugleich an. Wer früher da ist
        // (längere Runden), wartet am Markt, bis der Kern verkauft; wer später kommt (kürzere Runden, eine vom Kern gekappte Fahrt über der
        // Höchstdauer), kehrt dort sofort um (Karrenzug). Wo keiner hinsieht, gilt der Kern (Abgleichen).
        var (hin, zurueck) = ZugProfileVon(ansicht.Betrieb, karren, ansicht, zumHafen);
        var zug = gezeigt.Zug;
        if (gezeigt.ZugProfile is { } alt && !ReferenceEquals(alt.Hin, hin)) zug.Umstellen(alt.Hin, alt.Zurueck, hin, zurueck); // der Belag hat sich geändert
        gezeigt.ZugProfile = (hin, zurueck);
        var kernAnteil = KernAnteil(karren, hin, zurueck, fahrzeit, jetztGenau);
        var sim = _simulation!;
        if (neu) zug.Abgleichen(karren.Zustand, kernAnteil, hin, zurueck);
        else
        {
            zug.Weiter(sim.Takt.IstPausiert ? 0 : delta * sim.Takt.TicksProSekunde, karren.Zustand, hin, zurueck);
            // Sieht keiner hin (der Karren und der Ort, an dem der Kern ihn hätte, liegen außerhalb des Bildes), gilt der Kern
            if (!KarrenImBild(knoten.GlobalPosition) && !KarrenImBild(ToGlobal(AufFuss(PunktAufWeg(weg, Stelle((float)kernAnteil))))))
                zug.Abgleichen(karren.Zustand, kernAnteil, hin, zurueck);
        }
        gezeigt.Geschwindigkeit = sim.Takt.IstPausiert ? 0 : (float)(zug.MeterJeMinute(hin, zurueck) * sim.Takt.TicksProSekunde);
        // Am Halt (Ende der Fahrt, auch wenn der Karren hinter einem anderen wartet) rollt er nicht mehr
        if (zug.Etappe is Etappe.Hin or Etappe.Zurueck && zug.Weg(hin, zurueck) + versatz > 1f - spaet) gezeigt.Geschwindigkeit = 0;
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
        KarrenMessen(gezeigt, karren, ansicht, zug.Etappe, zug.Etappe == Etappe.Hin && zug.Weg(hin, zurueck) + versatz > 1f - spaet, jetztGenau, Stelle((float)zug.Weg(hin, zurueck)) * laenge, knoten.Position, neu);
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

        KarrenAnimieren(karren, gezeigt, gezeigt.Geschwindigkeit, delta);
    }

    /// <summary>
    /// Messzeile der sichtbaren Hinfahrt (<c>[karren] …</c> im Log): Der Karren wird vom Abfahren bis zum Halt am Pflasterrand begleitet – gefahrene Strecke auf dem Weg (die Stelle, an der der Karren gezeigt wird; ohne die Rechtsspur),
    /// Spielminuten und daraus das Tempo in Metern je echter Sekunde bei Tempo 1 (eine Spielminute = eingestellte Rundendauer ÷ 1.140 Sekunden, wie das
    /// Bild fährt), dazu die Kern-Fahrzeit. Nur mit der Startoption <c>--karrenmessung=1</c>. Nur Fahrten, die man von der Abfahrt an im Bild sah (nicht abgeglichen, nicht aus dem Bild).
    /// </summary>
    private void KarrenMessen(KarrenAnsicht k, Karren karren, BetriebsAnsicht a, Etappe etappe, bool amHalt, double jetzt, float meter, Vector3 ort, bool neu)
    {
        if (etappe != Etappe.Hin)
        {
            // Die Hinfahrt ist zu Ende (am Halt angekommen): messen, wenn sie von der Abfahrt an im Bild war
            var gueltig = k.HinStart >= 0 && etappe == Etappe.Markt;
            var (start, strecke) = (k.HinStart, k.HinStrecke);
            k.HinStart = -1;
            if (gueltig) KarrenfahrtGesehen(karren, a, strecke, jetzt - start);
            return;
        }
        if (k.HinStart == -2) return;
        if (!KarrenImBild(ToGlobal(ort)) && !amHalt) { k.HinStart = -2; return; }
        if (k.HinStart == -1)
        {
            if (neu || amHalt) { k.HinStart = -2; return; }
            (k.HinStart, k.HinStrecke, k.HinLetzte) = (jetzt, 0, meter);
            return;
        }
        k.HinStrecke += Math.Abs(meter - k.HinLetzte);
        k.HinLetzte = meter;
        if (!amHalt) return;
        var minuten = jetzt - k.HinStart;
        var gezeigt = k.HinStrecke;
        k.HinStart = -2;
        KarrenfahrtGesehen(karren, a, gezeigt, minuten);
    }

    /// <summary>Startoption <c>--karrenmessung=1</c>: die Messzeilen <c>[karren] Profil</c> und <c>[karren] Fahrt gesehen</c> ins Protokoll (zum Prüfen von Bild gegen Kern).</summary>
    private static readonly bool KarrenMessung = OS.GetCmdlineUserArgs().Contains("--karrenmessung=1");

    private void KarrenfahrtGesehen(Karren karren, BetriebsAnsicht a, float strecke, double minuten)
    {
        if (minuten <= 0 || !KarrenMessung) return;
        // Meter je echter Sekunde bei Tempo 1 mit der eingestellten Rundendauer (wie das Bild fährt, Gehtempo.MeterJeSpielminute)
        var tempo = strecke / (minuten * (_simulation!.Takt.RundenDauerSekunden / Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag));
        var kern = _wirtschaft?.Fahrzeit(karren, true) ?? 0;
        var ziel = ZumHafen(karren, a) ? _wirtschaft?.HafenVon(a.Betrieb)?.Name ?? "Hafen" : a.Markt?.Name ?? "Ursprung";
        GD.Print($"[karren] Fahrt gesehen: {a.Betrieb.Name} {karren.Typ} {karren.Name} -> {ziel}: {strecke:0.0} m in {minuten:0.0} Spielminuten = {tempo:0.00} m/s bei Tempo 1 " +
                 $"(Tempo der Art {karren.Daten.Tempo:0.0} m/s), Kern-Fahrzeit {kern} min, Unterschied {minuten - kern:+0.0;-0.0;0.0} min");
    }

    private readonly HashSet<(Betrieb, string, bool)> _karrenfahrtenGemeldet = new();

    /// <summary>
    /// Einmal je Betrieb und Karrenart eine Messzeile <c>[karren] Profil …</c>: der gezeigte Weg (vom Halteplatz bis zum Halt am Pflasterrand) und der Weg
    /// des Kerns, die Fahrzeit des Kerns und die, die der Karren im festen Tempo seiner Art (mit Steigung, Belag, Furt und Fähre) für den gezeigten Weg
    /// braucht – dazu, um wie viel er früher (−) oder später (+) am Halt ist als der Kern. Mit Datenversion 65 fährt das Bild den Weg des Kerns
    /// (<see cref="BetriebsAnsicht.WegVomKern"/>): Die Längen sind gleich, die Zeiten bis auf die Rundung des Kerns auf ganze Minuten; dazu, wie weit der
    /// „Anker_Karren“ des Modells vom Anfang des Kernwegs liegt (der Katalog muss ihn kennen, <c>karrenAnker</c>). Die Höchstdauer des Kerns
    /// (<c>logistik.weglaenge</c>) kappt lange Fahrten.
    /// </summary>
    private void KarrenfahrtMelden(Betrieb betrieb, Karren karren, BetriebsAnsicht ansicht, float laenge,
        (IReadOnlyList<Reichsgilde.Core.Bauen.Fahrtstueck> Hin, IReadOnlyList<Reichsgilde.Core.Bauen.Fahrtstueck> Zurueck)? fahrten, Stadtwirtschaft w, Fahrprofil hin,
        bool zumHafen)
    {
        if (!KarrenMessung || !_karrenfahrtenGemeldet.Add((betrieb, karren.Typ, zumHafen)) || fahrten is null) return;
        var kern = zumHafen ? w.FahrzeitZumHafen(betrieb, karren.Typ, true) ?? 0 : w.Fahrzeit(betrieb, karren.Typ, true);
        var kernLaenge = fahrten.Value.Hin.Sum(s => s.Laenge);
        var woher = ansicht.WegVomKern ? "Weg des Kerns" : "Gassennetz, Kern bis zur Platzmitte";
        var anker = ansicht.WegVomKern && KarrenAnker(betrieb.Name) is { } a ? $", Anker im Modell {new Vector2(a.X - ansicht.WegZumMarkt[0].X, a.Z - ansicht.WegZumMarkt[0].Z).Length():0.00} m vom Start" : "";
        var ziel = zumHafen ? w.HafenVon(betrieb)?.Name ?? "Hafen" : ansicht.Markt?.Name ?? "Ursprung";
        GD.Print($"[karren] Profil: {betrieb.Name} {karren.Typ} -> {ziel}: Weg im Bild {laenge:0.0} m bis zum Halt (Kern {kernLaenge:0.0} m, {woher}{anker}), " +
                 $"im festen Tempo {hin.Dauer:0.0} min, Kern-Fahrzeit {kern} min, Ankunft {hin.Dauer - kern:+0.0;-0.0;0.0} min ({(hin.Dauer - kern) / Math.Max(kern, 1) * 100:+0;-0;0} %)");
    }

    /// <summary>
    /// Wo der Kern den Karren auf dem Weg hat (0 = Hof, 1 = Markt): aus der Zeit der Fahrt, über dieselben Profile wie der Zug (Steigung, Belag,
    /// Furten und Fähren). Nur der Abgleich braucht das (<see cref="Karrenzug.Abgleichen"/>); gezeigt wird der Karren an der Stelle seines Zuges.
    /// </summary>
    private static double KernAnteil(Karren karren, Fahrprofil hin, Fahrprofil zurueck, int fahrzeit, double jetztGenau)
    {
        if (karren.Zustand == KarrenZustand.AmBetrieb) return 0;
        var vorbei = 1 - Math.Clamp((karren.Ankunft - jetztGenau) / Math.Max(fahrzeit, 1), 0, 1); // Anteil der Fahrzeit, der vorbei ist
        return karren.Zustand == KarrenZustand.ZumMarkt ? hin.WegBei(vorbei * hin.Dauer) : 1 - zurueck.WegBei(vorbei * zurueck.Dauer);
    }

    /// <summary>
    /// Die Fahrten (Hin- und Rückweg) je Betrieb, Karrenart und Ziel (Marktplatz oder Hafen, Datenversion 73); mit dem Belag neu gerechnet
    /// (<c>BelagBild</c>), wie der Kern die Fahrzeiten.
    /// </summary>
    private readonly Dictionary<(Betrieb, string, bool), (Fahrprofil Hin, Fahrprofil Zurueck)> _zugprofile = new();

    /// <summary>
    /// Das Tempo des Karrens auf der Karrenstraße, alles aus dem Kern: das Tempo seiner Art (<see cref="KarrenDaten.Tempo"/>, Meter je echter Sekunde
    /// bei Tempo 1 mit der eingestellten Rundendauer wie die Figuren, <see cref="Gehtempo.MeterJeSpielminute"/>; bei 15 Minuten je Runde genau das
    /// Tempo des Kerns) auf ebenem Fahrweg, je Stück geteilt durch den Zeitfaktor des Kerns (<c>Stadtwirtschaft.Zeitfaktor</c>:
    /// bergauf und auf schlechtem Belag langsamer, Ausbau höchstens 15 % schneller). Furten bremsen, Fähren halten auf. Die Länge ist die des
    /// gezeigten Weges (vom Hof bis zum Halt am Rand des Pflasters); das Profil wird nicht auf die Fahrzeit des Kerns gestreckt (Tempo bleibt fest),
    /// die Abweichung zum Kern steht einmal je Betrieb im Log (<see cref="KarrenfahrtMelden"/>).
    /// </summary>
    private (Fahrprofil Hin, Fahrprofil Zurueck) ZugProfileVon(Betrieb betrieb, Karren karren, BetriebsAnsicht ansicht, bool zumHafen = false)
    {
        if (_zugprofile.TryGetValue((betrieb, karren.Typ, zumHafen), out var bekannt)) return bekannt;
        var laenge = WegLaenge(KarrenWeg(ansicht, zumHafen)); // bis zum Halt am Pflasterrand bzw. vor dem Hafen
        var meterJeMinute = Gehtempo.MeterJeSpielminute(karren.Daten.Tempo > 0 ? karren.Daten.Tempo : 1,
            _simulation?.Takt.RundenDauerSekunden ?? Reichsgilde.Core.Zeit.Taktgeber.StandardRundenDauerSekunden); // ohne Tempo: 1 m/s
        if (_wirtschaft is not { } w)
            return (new Fahrprofil(Karrentempo.Stuecke(null, _ => 1, 0, 0), laenge, meterJeMinute), new Fahrprofil(Karrentempo.Stuecke(null, _ => 1, 0, 0), laenge, meterJeMinute));
        var fahrten = zumHafen ? w.FahrtenZumHafen(betrieb) : w.Fahrten(betrieb);
        var wasser = w.Wasserhalt; // wie der Kern: Furten (ab Datenversion 65 auf jedem Weg) und Fähren
        Fahrprofil Profil(IReadOnlyList<Reichsgilde.Core.Bauen.Fahrtstueck>? fahrt) =>
            new(Karrentempo.Stuecke(fahrt, w.Zeitfaktor, wasser?.FurtMinuten ?? 0, wasser?.FaehreMinuten ?? 0), laenge, meterJeMinute);
        var profile = (Profil(fahrten?.Hin), Profil(fahrten?.Zurueck));
        KarrenfahrtMelden(betrieb, karren, ansicht, laenge, fahrten, w, profile.Item1, zumHafen);
        if (fahrten is not null || w.Karte is null) _zugprofile[(betrieb, karren.Typ, zumHafen)] = profile; // ohne Platz noch nicht merken: er kann kommen
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
