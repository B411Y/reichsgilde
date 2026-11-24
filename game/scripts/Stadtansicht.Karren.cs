using Godot;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Karren (Teil der <c>partial class Stadtansicht</c>): Handkarren, Pferdekarren und Großer Pferdekarren als Modelle aus Blender
/// (<c>karren/karren_hand.glb</c>, <c>karren_pferd.glb</c>, <c>karren_pferd_gross.glb</c>, Fahrtrichtung +Z) – ohne Tier und ohne Figur, mit Rädern als
/// eigenen Knoten (<c>Rad_*</c>), die sich nach der gefahrenen Strecke drehen. Zieher und Zugtiere sind eigene Figuren an den Ankern
/// (<c>Stadtansicht.Zugtiere.cs</c>): ein Passant aus dem Fuhrmann des Karrens, Pferde mit Skelett (nah), gebacken (ab Zoom 50) und als Fernnetz (ab 100).
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
        /// <summary>Die Zugtiere (ein oder zwei Pferde) an den Ankern des Karrens.</summary>
        public Zugtier[] Tiere { get; set; } = Array.Empty<Zugtier>();
        /// <summary>Der Halter des Ziehers am Anker (null ohne Figur) und ob er gerade gezeigt wird (Nachtregel: nur ein fahrender Karren hat seinen Fuhrmann bei sich).</summary>
        public Node3D? FahrerHalter { get; set; }
        /// <summary>Lage und Ausrichtung des Ziehers zum Zugpunkt (Bild des Tiers; Stadtansicht.Zugtiere.cs). Der Kutscher auf dem Bock fährt mit dem Wagen (FahrerVersatz null).</summary>
        public Vector3? FahrerVersatz { get; set; }
        public Basis FahrerBasis { get; set; } = Basis.Identity;
        /// <summary>Z des Zugpunkts im Modell des Karrens (Anker am Deichselende bzw. beim Handkarren der Standpunkt des Ziehers) und Z des Drehpunkts (vorderste Radachse; Einachser 0).</summary>
        public float ZugZ { get; init; }
        public float DrehpunktZ { get; init; }
        /// <summary>Der Vorderwagen des Drehschemels (Knoten „Vorderwagen“, Ursprung = Reibnagel; null beim Einachser und bei starren Wagen): er dreht gegen den Kasten.</summary>
        public Node3D? Vorderwagen { get; init; }
        /// <summary>Die Schleppkurve (Gespannlauf.cs): Zugtier am Weg, Wagen hängt nach. Null ohne Modell.</summary>
        public Gespannlauf? Lauf { get; init; }
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
        /// <summary>Der Fuhrmann am Karren (Zieher, Kutscher), eine Passantenfigur mit Skelett; null, wenn das Figurenmodell oder der Kern fehlt.</summary>
        public Figur? Fahrer { get; set; }
        /// <summary>Die sichtbare Fahrt mit fester Geschwindigkeit (Stadtansicht.Karren.cs, <see cref="Karrenzug"/>) und die Profile, mit denen sie zuletzt gerechnet wurde.</summary>
        public Karrenzug Zug { get; } = new();
        /// <summary>Wo der Karren auf dem Weg gezeigt wird (Meter vom Hof), ob er auf dem Weg ist (nicht am Hof), ob er dem Markt zu fährt, auf welchem Weg und sein letztes Tempo (Meter je Sekunde): die Folgeregel (<see cref="KarrenFolgen"/>) liest das von den Karren desselben Betriebs.</summary>
        public float Meter { get; set; }
        public bool AufWeg { get; set; }
        public bool ZumMarkt { get; set; }
        public Vector3[]? WegRef { get; set; }
        public float Tempo { get; set; } = 1f;
        public (Fahrprofil Hin, Fahrprofil Zurueck)? ZugProfile { get; set; }
        /// <summary>Der Karren wird gerade auf einer Sonderfahrt gezeigt (selbst gefahren: am anderen Betrieb, von oder zu einem anderen Betrieb; Stadtansicht.Karrenfahrt.cs) und die Angaben dazu.</summary>
        public bool Sonder { get; set; }
        public (KarrenZustand Zustand, long Ankunft, string Von, string Nach)? SonderSchluessel { get; set; }
        public double SonderDauer { get; set; } = 1;
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
        /// <summary>Letzter Ort des Rades in der Welt (für den eigenen Weg des Rades, <see cref="KarrenRaederDrehen"/>).</summary>
        public Vector3 Letzte { get; set; }
        public bool LetzteGueltig { get; set; }
        /// <summary>Das Rad hängt am Vorderwagen (Drehschemel) und fährt in dessen Richtung, nicht in der des Kastens.</summary>
        public bool AmVorderwagen { get; init; }
    }

    /// <summary>
    /// Was an einer Karrenart verschieden ist: das Modell, die Räder (Knotennamen, Halbmesser nach <c>tools/modelle/karren.py</c>, falls das Modell ihn nicht als
    /// Extra <c>radius</c> mitbringt), der Anker und der Clip des Ziehers, die Zugtierart (<c>pferd</c>; null: keines) mit ihren Ankern.
    /// </summary>
    private sealed record KarrenArt(string Datei, (string Name, float Radius)[] Raeder, string ZieherAnker, string ZieherClip, string? Tier, string[] TierAnker, bool Pferd = false,
        string? Drehschemel = null, bool TierFolgtDeichsel = false);

    /// <summary>
    /// Die drei Karrenarten (Hand, Pferd, Großer Pferdekarren), nach <see cref="Karren.Typ"/>. Der Handkarren wird gezogen (Nutzer, 10.11.2026): der Zieher steht an der Zugstange vorn
    /// (<c>Anker_Ziehen</c>, Hände hinter dem Körper, Clip „Ziehen“); an den Pferdekarren sitzt er auf dem Bock („SitzenBock“, auch beim Halt).
    /// </summary>
    private static readonly Dictionary<string, KarrenArt> KarrenArten = new()
    {
        ["Handkarren"] = new("karren/karren_hand", new[] { ("Rad_L", 0.45f), ("Rad_R", 0.45f) }, "Anker_Ziehen", "Ziehen", null, Array.Empty<string>()),
        // Pferdekarren (1 Pferd, Zweirad, Gabeldeichsel) und Großer Pferdekarren (2 Pferde, Vierrad mit Drehschemel, Mittelbaum zwischen den Pferden): der Fuhrmann sitzt auf dem Bock;
        // die Tiere gehen in Richtung der Deichsel (die Schere bzw. der Mittelbaum führt sie, kein Knick Tier/Deichsel).
        ["Pferdekarren"] = new("karren/karren_pferd", new[] { ("Rad_L", 0.72f), ("Rad_R", 0.72f) }, "Anker_Bock", "SitzenBock", "pferd",
            new[] { "Anker_Deichsel" }, Pferd: true, TierFolgtDeichsel: true),
        ["Großer Pferdekarren"] = new("karren/karren_pferd_gross", new[] { ("Rad_HL", 0.66f), ("Rad_HR", 0.66f), ("Rad_VL", 0.42f), ("Rad_VR", 0.42f) }, "Anker_Bock", "SitzenBock", "pferd",
            new[] { "Anker_Pferd_L", "Anker_Pferd_R" }, Pferd: true, Drehschemel: "Vorderwagen", TierFolgtDeichsel: true),
    };

    /// <summary>So weit (in Metern) darf ein Karren außerhalb des Bildes stehen und wird noch animiert (seine Tiere sind rund 5 m lang).</summary>
    private const float KarrenRandMeter = 6f;

    /// <summary>Luft (Meter) zwischen zwei Gespannen in der Reihe auf dem Weg.</summary>
    private const float KarrenLuft = 0.8f;

    /// <summary>Alter Abstand (Meter) der wartenden Karren am Hof nebeneinander (nur noch für die Messung „vorher“): jetzt gilt <see cref="KarrenHofQuer"/>.</summary>
    private const float KarrenHofBreite = 2.6f;

    /// <summary>Bis zu welcher Strecke (Meter) ab dem Hof ein Karren aus seinem Platz am Hof auf den Weg einschwenkt.</summary>
    private const float KarrenEinschwenken = 6f;

    /// <summary>
    /// Länge des Gespanns vor und hinter dem Ursprung des Karrens (Meter, Fahrtrichtung vorn), gemessen aus den Modellen (Karren und Zugtier an den Ankern,
    /// <c>tools/modelle/karren.py</c>, Messung <c>karren_neu_pruefen.py</c>): Großer Pferdekarren 7,25 + 1,22 (Zugpunkt 6,47 m vor der Hinterachse, Kopf des Pferds 0,78 m vor dem Ring), Pferdekarren 4,75 + 1,75 (Zugpunkt 3,94 m), Handkarren 2,93 (mit Zieher) + 0,88.
    /// </summary>
    internal static (float Vorn, float Hinten) KarrenGespann(string typ) => typ switch
    {
        "Großer Pferdekarren" => (7.25f, 1.22f),
        "Pferdekarren" => (4.75f, 1.75f),
        _ => (2.93f, 0.88f),
    };

    /// <summary>Reihenabstand zweier Gespanne (Meter), gültig in beiden Fahrtrichtungen: max(vorn + hinten des anderen) + Luft.</summary>
    internal static float KarrenReihenabstand(string typA, string typB)
    {
        var (va, ha) = KarrenGespann(typA);
        var (vb, hb) = KarrenGespann(typB);
        return Math.Max(va + hb, vb + ha) + KarrenLuft;
    }

    /// <summary>
    /// Bis zu dieser Strecke (Meter vom Hof) bleibt der Karren auf seinem Platz neben den anderen: Beim Losfahren, bis sein Heck am Bug des lÃ¤ngsten anderen Gespanns
    /// vorbei ist, beim Heimkommen (<paramref name="ankunft"/>), bis sein Bug an dessen Bug vorbei ist (Bug = Gespann vorn); ohne andere Karren 0.
    /// </summary>
    internal static float KarrenHofZone(IReadOnlyList<string> typen, int nummer, bool ankunft)
    {
        if (typen.Count < 2) return 0f;
        var laengster = 0f;
        for (var i = 0; i < typen.Count; i++) if (i != nummer) laengster = Math.Max(laengster, KarrenGespann(typen[i]).Vorn);
        var (vorn, hinten) = KarrenGespann(typen[nummer]);
        return laengster + (ankunft ? vorn : hinten) + 0.3f;
    }

    /// <summary>Wie weit (Meter) der Karren Nr. <paramref name="nummer"/> am Markt hinter dem ersten Halteplatz steht: die Summe der Reihenabstände bis zum letzten Karren des Betriebs.</summary>
    internal static float KarrenHaltRueckstand(IReadOnlyList<string> typen, int nummer)
    {
        var summe = 0f;
        for (var i = nummer; i < typen.Count - 1; i++) summe += KarrenReihenabstand(typen[i], typen[i + 1]);
        return summe;
    }

    /// <summary>
    /// Folgeregel unterwegs: Wohin darf der Karren (<paramref name="ich"/>, Karrenart <paramref name="typ"/>) höchstens fahren, ohne einem Karren desselben Betriebs
    /// in gleicher Richtung auf demselben Weg näher zu kommen als Gespannlänge vorn + hinten des Vordermanns + Luft? Gibt die Stelle (Meter) zurück, die er
    /// nicht überschreitet (Hinfahrt: nach oben, Rückfahrt: nach unten); <paramref name="ziel"/> bleibt unverändert, wenn keiner im Weg ist.
    /// Wartende am Hof (nicht auf dem Weg) zählen nicht, sie stehen nebeneinander.
    /// </summary>
    internal static float KarrenFolgen(float ziel, bool zumMarkt, string typ, IEnumerable<(string Typ, float Meter)> vorleute)
    {
        var (vorn, _) = KarrenGespann(typ);
        foreach (var (andereTyp, meter) in vorleute)
        {
            var luecke = vorn + KarrenGespann(andereTyp).Hinten + KarrenLuft;
            ziel = zumMarkt ? Math.Min(ziel, meter - luecke) : Math.Max(ziel, meter + luecke);
        }
        return ziel;
    }

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
                    raeder.Add(new Rad { Knoten = rad, Ruhe = rad.Transform, Radius = RadHalbmesser(rad, radius), AmVorderwagen = art.Drehschemel is not null && rad.Name.ToString().StartsWith("Rad_V") });
        }
        // Die Eskorte bleibt vorerst ein Begleiter neben dem Karren
        var eskorte = new MeshInstance3D { Name = "Eskorte", Mesh = new CapsuleMesh { Radius = 0.35f, Height = 1.8f }, MaterialOverride = Material(new Color(0.35f, 0.35f, 0.38f)), Position = new Vector3(1.8f, 0.9f, 0.6f), Visible = false };
        wurzel.AddChild(eskorte);
        // Zugpunkt und Drehpunkt des Gespanns aus den vorhandenen Ankern und Rädern (Anker_Deichsel bzw. Anker_Ziehen; vorderste Radachse)
        var zugZ = modell is null ? 0f : (art.TierAnker.Length > 0 ? art.TierAnker[0] : art.ZieherAnker) is { } zugName && modell.FindChild(zugName, true, false) is Node3D zugAnker ? zugAnker.Position.Z : 0f;
        // Drehschemel: der Ursprung des Knotens „Vorderwagen“ ist der Reibnagel (Drehpunkt, vordere Achse); sein Abstand zum Modellursprung (hintere Achse) ist der Radstand
        var vorderwagen = modell is not null && art.Drehschemel is { } vwName ? modell.FindChild(vwName, true, false) as Node3D : null;
        var drehZ = vorderwagen is not null ? vorderwagen.Position.Z : raeder.Count == 0 ? 0f : Math.Max(0f, raeder.Max(r => r.Ruhe.Origin.Z));
        var radstand = vorderwagen is not null ? drehZ : 0f;
        var lauf = modell is null || zugZ - drehZ < 0.3f ? null
            : art.Tier is null ? new Gespannlauf(zugZ - drehZ, 0, 80, 1.0)
            : art.TierFolgtDeichsel ? new Gespannlauf(zugZ - drehZ, radstand, 30, 3.0, 40) // Schere bzw. Mittelbaum führen das Tier: kleiner Knick; Drehschemel höchstens 40° gegen den Kasten (Unterlauf)
            : art.Pferd ? new Gespannlauf(zugZ - drehZ, 0, 45, 3.0) : new Gespannlauf(zugZ - drehZ, 0, 50, 2.5);
        var ansicht = new KarrenAnsicht
        {
            ZugZ = zugZ, DrehpunktZ = drehZ, Lauf = lauf, Vorderwagen = vorderwagen,
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
        if (karren.Raeder.Length == 0) return;
        var sichtbar = KarrenImBild(karren.Wurzel);
        var vorwaerts = karren.Wurzel.GlobalBasis * Vector3.Back; // Fahrtrichtung des Modells (+Z)
        var vorwaertsVorn = karren.Vorderwagen is { } vw ? vw.GlobalBasis * Vector3.Back : vorwaerts; // Vorderräder des Drehschemels fahren in Richtung des Vorderwagens
        var pause = _simulation?.Takt.IstPausiert == true;
        foreach (var rad in karren.Raeder)
        {
            // Jedes Rad dreht nach seinem eigenen Weg (Ortsänderung längs der Fahrtrichtung): innen langsamer als außen, bei der Wende gegenläufig
            var ort = rad.Knoten.GlobalPosition;
            var meter = rad.LetzteGueltig ? (ort - rad.Letzte).Dot(rad.AmVorderwagen ? vorwaertsVorn : vorwaerts) : 0f;
            (rad.Letzte, rad.LetzteGueltig) = (ort, true);
            if (pause || Math.Abs(meter) > KarrenMaxTempo * Math.Max((float)delta, 1f / 60f)) continue; // Sprünge (Abgleich, Wende der Zeit) drehen nicht mit
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
        // Selbst gefahren: am anderen Betrieb stehen oder von/zu einem anderen Betrieb fahren (Stadtansicht.Karrenfahrt.cs); sonst die gewohnte Fahrt Heimat <-> Markt
        if (KarrenSonderBewegen(karren, nummer, gezeigt, jetztGenau, delta)) return;
        var ausSonder = gezeigt.Sonder; // gerade aus der Sonderfahrt zurück auf den gewohnten Weg: Zug und Gespann auf den Ort des Kerns setzen
        gezeigt.Sonder = false;
        neu |= ausSonder;

        // Mehrere Karren eines Betriebs fahren hintereinander statt ineinander (mit den Tieren sind sie bis zu 6 m lang): Karren Nr. n
        // liegt immer n · KarrenAbstand weiter Richtung Markt als der erste. Am Halteplatz warten sie so in einer Reihe auf dem Weg,
        // und auf der Rückfahrt kommt der erste zuerst an.
        // Der Weg führt zu dem Platz, zu dem der Betrieb gehört (wie im Kern), und endet am Halt am Rand seines Pflasters
        // Zum Marktplatz oder – Datenversion 73 – zum Hafen: der Weg, den der Kern dieser Fahrt gibt
        var zumHafen = ZumHafen(karren, ansicht);
        var weg = KarrenWeg(ansicht, zumHafen);
        if (KarrenKlangTest) gezeigt.KlangProbeWeg = weg; // nur mit --karrenklangtest=1 (Stadtansicht.KarrenKlang.cs)
        var laenge = WegLaenge(weg);
        // Am Hof warten die Karren nebeneinander (wie im Umland), unterwegs gilt die Folgeregel (KarrenFolgen), am Markt halten sie in der Reihe nach
        // Gespannlänge: der letzte Karren ganz vorn, die übrigen je einen Reihenabstand dahinter
        var typen = ansicht.Betrieb.Karren.Select(k => k.Typ).ToList();
        var rueckstand = KarrenHaltRueckstand(typen, nummer);
        var spaet = laenge > 0 ? rueckstand / laenge : 0f;
        float Stelle(float anteil) => Math.Clamp(Math.Min(anteil, 1f - spaet), 0f, 1f);
        // Der Karren fährt im Tempo seiner Art (Nutzer, 31.10.2026: Hand 1,0, Pferdekarren 0,8, Großer Pferdekarren 1,3 m/s bei Tempo 1) – nie gestreckt: Der Kern
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
        // Klappbrücke (Kern: Karren.WartetAnBruecke): Wartet der Karren an der offenen Brücke, hält er mit der Spitze an der Schranke; die Zeit seines Zuges steht, bis die Brücke zugeht –
        // so kommt er um die Wartezeit später an, wie es der Kern in Ankunft rechnet (Stadtansicht.Klappbruecke.cs)
        var halt = !neu && karren.WartetAnBruecke(sim.Zeit) ? KlappbrueckeHaltMeter(weg, zug.ZumMarkt, karren.Typ) : null;
        var steht = halt is { } h && zug.Etappe is Etappe.Hin or Etappe.Zurueck && (zug.ZumMarkt ? zug.Weg(hin, zurueck) * laenge >= h - 0.05f : zug.Weg(hin, zurueck) * laenge <= h + 0.05f);
        KlappbrueckeWartenMelden(karren, steht, halt);
        if (neu || ausSonder) zug.Abgleichen(karren.Zustand, kernAnteil, hin, zurueck);
        else
        {
            zug.Weiter(sim.Takt.IstPausiert || steht ? 0 : delta * sim.Takt.TicksProSekunde, karren.Zustand, hin, zurueck);
            // Sieht keiner hin (der Karren und der Ort, an dem der Kern ihn hätte, liegen außerhalb des Bildes), gilt der Kern
            if (!KarrenImBild(knoten.GlobalPosition) && !KarrenImBild(ToGlobal(AufFuss(PunktAufWeg(weg, Stelle((float)kernAnteil))))))
                zug.Abgleichen(karren.Zustand, kernAnteil, hin, zurueck);
        }
        gezeigt.Geschwindigkeit = sim.Takt.IstPausiert ? 0 : (float)(zug.MeterJeMinute(hin, zurueck) * sim.Takt.TicksProSekunde);
        if (steht) gezeigt.Geschwindigkeit = 0;                       // wartet vor der Schranke: die Räder stehen
        // Stelle auf dem Weg (Meter): wohin der Zug will (am Markt höchstens bis zum eigenen Halteplatz), dann die Folgeregel hinter dem Vordermann
        var wunsch = zug.Etappe == Etappe.Hof ? 0f : Stelle((float)zug.Weg(hin, zurueck)) * laenge;
        if (halt is { } schranke && zug.Etappe != Etappe.Hof) wunsch = zug.ZumMarkt ? Math.Min(wunsch, schranke) : Math.Max(wunsch, schranke);
        var amHalt = zug.Etappe is Etappe.Hin or Etappe.Zurueck && zug.Weg(hin, zurueck) * laenge >= laenge - rueckstand - 0.01f;
        var zone = KarrenHofZone(typen, nummer, !zug.ZumMarkt);
        var vorleute = ansicht.Betrieb.Karren
            .Select((k, i) => (k, i))
            .Where(p => p.k != karren && _karren.TryGetValue(p.k, out var a) && a.AufWeg && ReferenceEquals(a.WegRef, weg) && a.ZumMarkt == zug.ZumMarkt
                        && (zug.ZumMarkt ? a.Meter >= gezeigt.Meter : a.Meter <= gezeigt.Meter)
                        && !(a.Meter < KarrenHofZone(typen, p.i, !zug.ZumMarkt) && gezeigt.Meter < zone)) // beide noch auf ihren Plätzen nebeneinander: sie behindern sich nicht
            .Select(p => (p.k.Typ, _karren[p.k].Meter)).ToList();
        var stelleM = zug.Etappe == Etappe.Hof ? 0f : KarrenFolgen(wunsch, zug.ZumMarkt, karren.Typ, vorleute);
        stelleM = Math.Clamp(stelleM, 0f, laenge);
        var vorherM = gezeigt.Meter;
        var imBild = KarrenImBild(knoten.GlobalPosition);
        if (neu || ausSonder || !imBild || zug.Etappe == Etappe.Hof || Math.Abs(stelleM - vorherM) > 12f) gezeigt.Meter = stelleM; // unbeobachtet oder weit weg: gleich dort
        else
        {
            // Aufholen nach dem Warten höchstens im Tempo der Fahrt, nie schneller als die Zeit
            var schritt = sim.Takt.IstPausiert ? 0f : (float)delta * Math.Max(gezeigt.Tempo, 1f);
            gezeigt.Meter = stelleM > vorherM ? Math.Min(stelleM, vorherM + schritt) : Math.Max(stelleM, vorherM - schritt);
        }
        gezeigt.AufWeg = zug.Etappe != Etappe.Hof;
        gezeigt.ZumMarkt = zug.ZumMarkt;
        gezeigt.WegRef = weg;
        if (gezeigt.Geschwindigkeit > 0.01f) gezeigt.Tempo = gezeigt.Geschwindigkeit;
        // Am Halt (Ende der Fahrt, auch wenn der Karren hinter einem anderen wartet) rollt er nicht mehr; wer von der Folgeregel gebremst wird, rollt im Tempo seiner Verschiebung
        if (amHalt) gezeigt.Geschwindigkeit = 0;
        else if (zug.Etappe is Etappe.Hin or Etappe.Zurueck && Math.Abs(stelleM - wunsch) > 0.02f && delta > 0)
            gezeigt.Geschwindigkeit = Math.Min(gezeigt.Geschwindigkeit, Math.Abs(gezeigt.Meter - vorherM) / (float)delta);
        // Hofreihe (Stadtansicht.KarrenHof.cs): der Platz am Hof liegt um den Versatz weiter auf dem Weg, damit das Heck nicht im Sockel steht; der Rest des Weges wird entsprechend gestaucht
        var hofLage = ansicht.KarrenReihe is null && weg.Length > 1 ? KarrenHofLage(ansicht, weg, typen) : (Quer: Array.Empty<float>(), Versatz: 0f, Achse: Vector3.Zero);
        var versatz = laenge > hofLage.Versatz + 2f ? hofLage.Versatz : 0f;
        var meterAnteil = laenge > 0 ? (versatz + gezeigt.Meter * (laenge - versatz) / laenge) / laenge : 0f;
        Vector3 ziel;
        Vector3 blick;
        var hofGewicht = 0f;
        if (zug.Etappe == Etappe.Hof && ansicht.KarrenReihe is { } reihe)
        {
            // Umland: am Halteplatz stehen die Karren nebeneinander (4 m, Reihe aus Stadtansicht.Umland.cs), mittig um den Anker verteilt; bei der Abfahrt rücken sie auf den Weg
            ziel = ansicht.Hof + reihe * (nummer - (ansicht.Betrieb.Karren.Count - 1) / 2f);
            blick = ziel + (weg[1] - ansicht.Hof); // alle mit derselben Blickrichtung, nicht zum selben Punkt
            var richtungHof = blick - ziel;
            richtungHof.Y = 0;
            if (gezeigt.Lauf is { } lauf1 && richtungHof.LengthSquared() > 1e-6f) { var vor = richtungHof.Normalized() * (float)lauf1.Deichsel; ziel += vor; blick += vor; } // Zugpunkt vor dem Wagen
        }
        else
        {
            // Hofgewicht: 1 auf dem Platz am Hof (bis zur Ausfahrt- bzw. Einfahrtzone), dann in KarrenEinschwenken Metern bis 0 auf der Mitte des Weges
            hofGewicht = Math.Clamp(1f - (gezeigt.Meter - zone) / KarrenEinschwenken, 0f, 1f);
            // Der Zugpunkt (Tier bzw. Zieher) läuft den Weg: er liegt um die Deichsellänge voraus (Fahrtrichtung), über den Wegrand hinaus gerade verlängert; der Wagen hängt nach (Gespannlauf)
            var vorAnteil = zug.Etappe == Etappe.Hof || zug.ZumMarkt ? 1f : -1f;
            var deichsel = gezeigt.Lauf is { } lauf0 ? (float)lauf0.Deichsel : 0f;
            ziel = PunktAufWegVerlaengert(weg, meterAnteil + vorAnteil * deichsel / Math.Max(laenge, 1f), laenge);
            var dAnteil = zug.Etappe == Etappe.Hof ? 1f / Math.Max(laenge, 1f) : zug.ZumMarkt ? 0.02f : -0.02f;
            blick = PunktAufWegVerlaengert(weg, meterAnteil + vorAnteil * deichsel / Math.Max(laenge, 1f) + dAnteil, laenge);
            if (ansicht.KarrenReihe is null && weg.Length > 1 && ansicht.Betrieb.Karren.Count > 1 && nummer < hofLage.Quer.Length)
            {
                // Hof: nebeneinander quer zum Weg (Querlage nach Gespannbreite plus Luft, KarrenHofQuer), beim Losfahren in den ersten Metern auf die Mitte des Weges einschwenken
                var rechtsVomWeg = new Vector3(-hofLage.Achse.Z, 0, hofLage.Achse.X);
                var seite = rechtsVomWeg * hofLage.Quer[nummer] * hofGewicht;
                ziel += seite;
                blick += seite;
            }
        }

        // Rechtsverkehr (Schritt 5d): unterwegs und am Markt halten sich die Karren rechts, so begegnen sich Hin- und Rückfahrt auf ihren Seiten
        if (zug.Etappe != Etappe.Hof && ansicht.KarrenReihe is null)
        {
            var fahrt = blick - ziel;
            fahrt.Y = 0;
            if (fahrt.LengthSquared() > 1e-6f)
            {
                var rechts = new Vector3(-fahrt.Z, 0, fahrt.X).Normalized() * KarrenSpur * (1f - hofGewicht); // auf dem Platz am Hof gilt die Reihe, nicht die Spur
                ziel += rechts;
                blick += rechts;
            }
        }

        KarrenPoseSetzen(gezeigt, ziel, blick, neu || ausSonder || zug.Etappe == Etappe.Hof, imBild, delta);
        KarrenMessen(gezeigt, karren, ansicht, zug.Etappe, zug.Etappe == Etappe.Hin && amHalt, jetztGenau, gezeigt.Meter, knoten.Position, neu);
        KarrenLadungZeigen(karren, gezeigt);

        KarrenAnimieren(karren, gezeigt, gezeigt.Geschwindigkeit, delta);
    }

    /// <summary>
    /// Setzt den Karren an den Zugpunkt <paramref name="ziel"/> mit Blick auf <paramref name="blick"/>: Schleppkurve (Gespannlauf.cs: Zugtier am Weg, Wagen hängt nach),
    /// Boden, Nicken am Hang und das Gespann. <paramref name="setzen"/>: gleich dort (neu, am Hof, unbeobachtet), sonst folgt das Gespann der Kurve.
    /// </summary>
    private void KarrenPoseSetzen(KarrenAnsicht gezeigt, Vector3 ziel, Vector3 blick, bool setzen, bool imBild, double delta)
    {
        var sim = _simulation!;
        var knoten = gezeigt.Wurzel;
        // Auf Gelände fährt der Karren auf dem Boden (Stadtansicht.Gelaende.cs)
        ziel = AufFuss(ziel);
        // Das Ziel wandert dank des Minuten-Bruchteils schon stetig. Das kurze Hinterhergleiten
        // fängt nur noch echte Sprünge ab (Abfahrt vom Hof, Ankunft, Tempowechsel).
        // Schleppkurve (Gespannlauf.cs): ziel ist jetzt der Zugpunkt. Das Tier läuft den Weg, der Wagen hängt an der Deichsel und folgt nach der Traktrix, ohne
        // Glättung nach Bildzeit: gleiches Bild bei 30 und 144 Bildern. Am Hof, unbeobachtet oder neu steht das Gespann gestreckt hinter dem Zugpunkt.
        var fahrtRichtung = blick - ziel;
        fahrtRichtung.Y = 0;
        if (gezeigt.Lauf is { } lauf && fahrtRichtung.LengthSquared() > 1e-6f)
        {
            var sekunden = sim.Takt.IstPausiert ? 0.0 : delta;
            if (!imBild || setzen || KarrenStarr) lauf.Setzen(ziel.X, ziel.Z, fahrtRichtung.X, fahrtRichtung.Z);
            else
            {
                var t0 = KarrenKurveStart();
                lauf.Weiter(ziel.X, ziel.Z, fahrtRichtung.X, fahrtRichtung.Z, sekunden * Math.Max(gezeigt.Tempo, 1f) * 2.5); // Wenden und Aufholen: das Tier trabt
                KarrenKurveMessen(t0);
            }
            var dx = (float)lauf.DeichselX; var dz = (float)lauf.DeichselZ;
            var achse = new Vector3((float)lauf.VorneX - dx * gezeigt.DrehpunktZ, 0, (float)lauf.VorneZ - dz * gezeigt.DrehpunktZ); // Ursprung des Modells (hintere Achse)
            var gier = Mathf.Atan2(dx, dz);
            if (gezeigt.Vorderwagen is { } vorder) // Drehschemel: der Kasten steht auf der Hinterachse und folgt der Vorderachse im Radstand, der Vorderwagen dreht mit der Deichsel
            {
                achse = new Vector3((float)lauf.HintenX, 0, (float)lauf.HintenZ);
                var kasten = Mathf.Atan2((float)lauf.WagenX, (float)lauf.WagenZ);
                vorder.Rotation = new Vector3(0, Mathf.Wrap(gier - kasten, -Mathf.Pi, Mathf.Pi), 0);
                gier = kasten;
            }
            knoten.Position = AufFuss(achse);
            knoten.Rotation = new Vector3(0, gier, 0); // das Nicken kommt gleich
        }
        else
        {
            knoten.Position = AufFuss(ziel);
            if (fahrtRichtung.LengthSquared() > 1e-6f) knoten.Rotation = new Vector3(0, Mathf.Atan2(fahrtRichtung.X, fahrtRichtung.Z), 0);
        }
        if (BodenBewegt) // am Hang nickt der Karren mit dem Gefälle (3 m vor und hinter der Achse)
        {
            var vorne = new Vector3(Mathf.Sin(knoten.Rotation.Y), 0, Mathf.Cos(knoten.Rotation.Y));
            var (vorn, hinten) = (FahrHoehe(knoten.Position.X + vorne.X * 1.5f, knoten.Position.Z + vorne.Z * 1.5f), FahrHoehe(knoten.Position.X - vorne.X * 1.5f, knoten.Position.Z - vorne.Z * 1.5f)); // Brücken und Fähren tragen
            var nick = -Mathf.Atan2(vorn - hinten, 3f);
            knoten.Rotation = new Vector3(nick, knoten.Rotation.Y, 0);
        }
        KarrenGespannSetzen(gezeigt);

    }

    /// <summary>Ladung nach der Zahl der geladenen Sorten, Plane darüber, Eskorte als Begleiter (nur beim Wechsel schalten).</summary>
    private static void KarrenLadungZeigen(Karren karren, KarrenAnsicht gezeigt)
    {
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
        if (karren.Zustand == KarrenZustand.AmMarkt) return 1; // selbst gefahren: steht am Markt
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

    /// <summary>Wie <see cref="PunktAufWeg"/>, aber über die Enden hinaus gerade weiter (in Richtung des ersten bzw. letzten Wegstücks): das Tier läuft vor dem Wagen her, auch am Rand des Weges.</summary>
    private static Vector3 PunktAufWegVerlaengert(Vector3[] weg, float anteil, float laenge)
    {
        if (anteil >= 0f && anteil <= 1f) return PunktAufWeg(weg, anteil);
        var (ende, davor) = anteil > 1f ? (weg[^1], weg[^2]) : (weg[0], weg[1]);
        var r = ende - davor;
        r.Y = 0;
        var mehr = (anteil > 1f ? anteil - 1f : -anteil) * laenge;
        return r.LengthSquared() < 1e-8f ? ende : ende + r.Normalized() * mehr;
    }

    /// <summary>
    /// Setzt Zugtiere und Zieher an den Zugpunkt der Schleppkurve: Sie laufen den Weg (Kurs des Zugtiers), der Karren hängt an ihnen
    /// (der Anker am Deichselende liegt auf dem Zugpunkt). Der Kutscher auf dem Bock bleibt am Wagen.
    /// </summary>
    private void KarrenGespannSetzen(KarrenAnsicht k)
    {
        if (k.Lauf is not { } lauf) return;
        var kurs = k.Art.TierFolgtDeichsel ? new Vector3((float)lauf.DeichselX, 0, (float)lauf.DeichselZ) : new Vector3((float)lauf.KursX, 0, (float)lauf.KursZ); // Schere/Mittelbaum: das Gespann geht in Richtung der Deichsel
        var zug = new Vector3((float)lauf.ZugX, 0, (float)lauf.ZugZ);
        zug = AufFuss(zug);
        var basis = new Basis(Vector3.Up, Mathf.Atan2(kurs.X, kurs.Z));
        if (BodenBewegt) // am Hang nickt auch das Tier (1,2 m vor und hinter dem Zugpunkt)
        {
            var (vorn, hinten) = (FahrHoehe(zug.X + kurs.X * 1.2f, zug.Z + kurs.Z * 1.2f), FahrHoehe(zug.X - kurs.X * 1.2f, zug.Z - kurs.Z * 1.2f));
            basis = basis * new Basis(Vector3.Right, -Mathf.Atan2(vorn - hinten, 2.4f));
        }
        var welt = GlobalTransform;
        foreach (var tier in k.Tiere)
            tier.Halter.GlobalTransform = welt * new Transform3D(basis, zug + basis * tier.ZugVersatz);
        if (k.FahrerHalter is { } fahrer && k.FahrerVersatz is { } versatz)
            fahrer.GlobalTransform = welt * new Transform3D(basis * k.FahrerBasis, zug + basis * versatz);
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
