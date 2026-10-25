using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Schiffe (Teil der <c>partial class Stadtansicht</c>, PR #199): die Flussschiffe aller Häuser als Modell <c>wasser_schiff.glb</c> (Lastkahn 20 × 4,2 m,
/// Bug +X, Kaiseite +Z, Ursprung auf der Wasserlinie).
///
/// Die Darstellung rechnet nichts aus, sie liest nur den Kern: Wo das Schiff gerade ist und wohin es fährt, sagt <see cref="Schiffahrt.Lage"/> (mit dem
/// Bruchteil der laufenden Minute, damit es gleitet). Am Kai liegt es längsseits, die Kaiseite an der Kaimauer (<c>Anker_Kai</c> auf der Kaikante); liegen
/// mehrere Schiffe am selben Hafen, machen sie im Päckchen nebeneinander fest (das äußere an der Bordwand des inneren); die Kähne des Hafenmodells weichen,
/// solange ein Schiff am Kai liegt. Segel gesetzt in Fahrt, gerefft am
/// Kai; die Laufplanke nur am Kai und nur beim inneren Schiff; <c>Ladung_1</c>–<c>Ladung_6</c> nach dem Anteil der Ladung; der Wimpel in der Hausfarbe flattert
/// (<see cref="ModellFahneBelebt"/>). Der Schiffer steht am Heck (<c>Anker_Schiffer</c>). Ein Klick auf das Schiff wählt es (Kennung <c>schiff:&lt;Name&gt;</c>).
///
/// Dazu bekommt das Hafenmodell eine Klickfläche (<c>hafen:&lt;Kennung&gt;</c>, Stadtansicht.Umland.cs) – Kran und Lagerschuppen in seiner Nähe ebenso –, die
/// Hauptszene öffnet damit die Hafentafel.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Ein Schiff in der Stadtansicht: der Knoten mit Ort und Drehung, das Modell darunter und was wir davon schalten.</summary>
    private sealed class SchiffAnsicht
    {
        public required Node3D Wurzel { get; init; }
        public MeshInstance3D? SegelGesetzt { get; init; }
        public MeshInstance3D? SegelGerefft { get; init; }
        public Node3D? Planke { get; init; }
        public required MeshInstance3D[] Ladung { get; init; }
        /// <summary>Anker_Kai im Modell (lokal): Dieser Punkt der Wasserlinie gehört auf die Kaikante.</summary>
        public Vector3 AnkerKai { get; init; }
        public Figur? Schiffer { get; set; }
        /// <summary>Was gerade gezeigt wird (-1 bzw. null = noch nie gesetzt); geschaltet wird nur beim Wechsel.</summary>
        public int GezeigteLadung { get; set; } = -1;
        public bool? GezeigtesSegel { get; set; }
        public bool? GezeigtePlanke { get; set; }
        public bool Sichtbar { get; set; }
        /// <summary>Höhe des Wasserspiegels unter dem Schiff (gemerkt, falls es kurz über Land gleitet).</summary>
        public float Spiegel { get; set; }
        public bool Hervorgehoben { get; set; }
        public int Nummer { get; init; }
    }

    private readonly Dictionary<Schiff, SchiffAnsicht> _schiffe = new(ReferenceEqualityComparer.Instance);

    private const string SchiffModell = "wasser/wasser_schiff";

    /// <summary>So weit (Meter) liegen zwei Schiffe im Päckchen auseinander: Breite des Rumpfs (4,2 m) und etwas Luft für die Fender.</summary>
    private const float SchiffPaeckchen = 4.6f;

    /// <summary>Wie viele Knoten <c>Ladung_1</c> … das Modell hat (so viele Stapel zeigt ein volles Schiff).</summary>
    private const int SchiffLadungsknoten = 6;

    /// <summary>Kennung eines Schiffs für Klick und Auswahl.</summary>
    public static string SchiffKennung(Schiff s) => "schiff:" + s.Name;

    /// <summary>Kennung eines Hafens (Kennung des Gebäudes der Karte) für Klick und Auswahl.</summary>
    public static string HafenKennung(string hafen) => "hafen:" + hafen;

    /// <summary>Der Name des Schiffs aus seiner Kennung, sonst null.</summary>
    public static string? SchiffName(string? kennung) => kennung is not null && kennung.StartsWith("schiff:") ? kennung["schiff:".Length..] : null;

    /// <summary>Die Kennung des Hafens (Gebäude der Karte) aus der Kennung „hafen:…“, sonst null.</summary>
    public static string? HafenAusKennung(string? kennung) => kennung is not null && kennung.StartsWith("hafen:") ? kennung["hafen:".Length..] : null;

    private SchiffAnsicht SchiffErzeugen(Schiff schiff, int nummer)
    {
        var wurzel = new Node3D { Name = "Schiff_" + schiff.Name.Replace(" ", "_") };
        AddChild(wurzel);
        var farbe = Besitzerfarbe(schiff.Besitzer);
        // Hausfarbe und Wimpel (flattert); Segel, Planke und Ladung schaltet SchiffBewegen
        var modell = ModellEinsetzen(SchiffModell, wurzel, Vector3.Zero, new ModellZustand(Besitzer: farbe))?.Wurzel;
        var ladung = new List<MeshInstance3D>();
        MeshInstance3D? gesetzt = null, gerefft = null;
        Node3D? planke = null;
        var ankerKai = new Vector3(0, 0, 2.4f);
        if (modell is not null)
        {
            for (var i = 1; i <= SchiffLadungsknoten && modell.FindChild($"Ladung_{i}", true, false) is MeshInstance3D teil; i++)
            {
                teil.Visible = false;
                ladung.Add(teil);
            }
            gesetzt = modell.FindChild("Segel_Gesetzt", true, false) as MeshInstance3D;
            gerefft = modell.FindChild("Segel_Gerefft", true, false) as MeshInstance3D;
            planke = modell.FindChild("Laufplanke", true, false) as Node3D;
            if (modell.FindChild("Anker_Kai", true, false) is Node3D kai) ankerKai = LageUnter(modell, kai).Origin;
        }
        // Die Klickfläche umfasst den Rumpf (20 × 4,2 m) bis über das Dollbord; das Segel nicht, sonst verdeckt es den Hafen dahinter
        Klickflaeche(SchiffKennung(schiff), new Vector3(0, 1, 0), new Vector3(20, 3, 4.6f), wurzel);
        Beschriftung(schiff.Name, new Vector3(0, 14, 0), 40, wurzel, SchiffKennung(schiff));
        var ansicht = new SchiffAnsicht
        {
            Wurzel = wurzel, SegelGesetzt = gesetzt, SegelGerefft = gerefft, Planke = planke, Ladung = ladung.ToArray(), AnkerKai = ankerKai, Nummer = nummer,
        };
        ansicht.Schiffer = SchifferEinsetzen(modell, schiff, farbe);
        return ansicht;
    }

    /// <summary>Der Schiffer am Heck (<c>Anker_Schiffer</c>, blickt mit −Z wie die Anker der Karren): ein Mensch im Gewand der gedämpften Hausfarbe.</summary>
    private Figur? SchifferEinsetzen(Node3D? modell, Schiff schiff, Color besitzer)
    {
        if (modell?.FindChild("Anker_Schiffer", true, false) is not Node3D anker || anker.GetParent() is not { } eltern) return null;
        var halter = new Node3D { Name = "Schiffer" };
        eltern.AddChild(halter);
        halter.Transform = anker.Transform.RotatedLocal(Vector3.Up, Mathf.Pi);
        var nummer = 4 * (int)(StabilerHash("schiff/" + schiff.Name) % 100000);
        var gewand = besitzer.Lerp(new Color(0.45f, 0.38f, 0.28f), 0.45f);
        var figur = FigurEinsetzen(halter, AussehenAusNummer(nummer, gewand) with { Kopf = Kopfbedeckung.Strohhut }, nummer);
        if (figur is not null) BewegungSetzen(figur, "Stehen");
        return figur;
    }

    /// <summary>Jedes Bild: Schiffe der Häuser anlegen, verkaufte entfernen, alle an ihren Ort setzen (aus <see cref="Aktualisieren"/>).</summary>
    private void SchiffeAktualisieren(Stadtwirtschaft w, double minute, double delta)
    {
        if (!w.Schiffahrt.IstAktiv && _schiffe.Count == 0) return;
        var liste = w.Schiffahrt.Schiffe;
        if (_schiffe.Count > 0)
            foreach (var weg in _schiffe.Keys.Where(s => !liste.Contains(s)).ToList())
            {
                KnotenEntfernen(_schiffe[weg].Wurzel);
                _schiffe.Remove(weg);
            }
        // Am Kai im Päckchen: je Hafen zählen, wie viele Schiffe schon innen liegen (in der Reihenfolge des Kaufs)
        var amKai = new Dictionary<string, int>();
        var auswahl = SchiffName(Auswahl);
        var maus = SchiffName(_unterDerMaus);
        for (var i = 0; i < liste.Count; i++)
        {
            var schiff = liste[i];
            var liegt = schiff.Zustand is SchiffZustand.AmKai or SchiffZustand.Beladen;
            var stelle = 0;
            if (liegt)
            {
                stelle = amKai.GetValueOrDefault(schiff.Hafen);
                amKai[schiff.Hafen] = stelle + 1;
            }
            if (!_schiffe.TryGetValue(schiff, out var ansicht)) _schiffe[schiff] = ansicht = SchiffErzeugen(schiff, i);
            SchiffBewegen(w, schiff, ansicht, minute, liegt, stelle, delta);
            var hell = schiff.Name == auswahl || schiff.Name == maus;
            if (ansicht.Schiffer is { } schiffer && hell != ansicht.Hervorgehoben)
            {
                ansicht.Hervorgehoben = hell;
                FigurHervorheben(schiffer, hell);
            }
        }
        // Liegt ein Schiff am Kai, weichen die Kähne des Hafenmodells (sie lägen sonst unter ihm)
        foreach (var hafen in w.Schiffahrt.Haefen)
        {
            var frei = !amKai.ContainsKey(hafen.Kennung);
            if (_kaehneFrei.GetValueOrDefault(hafen.Kennung, true) == frei) continue;
            _kaehneFrei[hafen.Kennung] = frei;
            HafenKaehneZeigen(hafen.Kennung, frei);
        }
    }

    /// <summary>Je Hafen: Sind seine Kähne zu sehen (kein Schiff am Kai)? Geschaltet wird nur beim Wechsel.</summary>
    private readonly Dictionary<string, bool> _kaehneFrei = new();

    private void SchiffBewegen(Stadtwirtschaft w, Schiff schiff, SchiffAnsicht ansicht, double minute, bool liegt, int paeckchen, double delta)
    {
        var lage = w.Schiffahrt.Lage(schiff, minute);
        var knoten = ansicht.Wurzel;
        if (knoten.Visible != lage.Sichtbar) knoten.Visible = lage.Sichtbar;
        if (!lage.Sichtbar)
        {
            ansicht.Sichtbar = false;
            return;
        }

        Vector3 ziel;
        float drehung;
        if (liegt && w.Schiffahrt.Hafen(schiff.Hafen) is { } hafen)
        {
            // Längsseits am Kai: die Kaiseite (+Z) zur Kaimauer, Anker_Kai auf der Kaikante; das Päckchen liegt weiter draußen
            var nach = new Vector3((float)hafen.RichtungX, 0, (float)hafen.RichtungZ);
            var kante = new Vector3((float)hafen.Liegeplatz.X, 0, (float)hafen.Liegeplatz.Z) - nach * (float)Schiffahrt.LiegeplatzAbstand;
            drehung = Mathf.Atan2(-nach.X, -nach.Z);
            var anker = new Basis(Vector3.Up, drehung) * new Vector3(ansicht.AnkerKai.X, 0, ansicht.AnkerKai.Z);
            ziel = kante - anker + nach * (paeckchen * SchiffPaeckchen);
        }
        else
        {
            // In Fahrt: der Bug (+X) in Fahrtrichtung
            ziel = new Vector3((float)lage.Ort.X, 0, (float)lage.Ort.Z);
            drehung = Mathf.Atan2(-(float)lage.RichtungZ, (float)lage.RichtungX);
        }

        // Auf dem Wasserspiegel (im Fluss oder im Hafenbecken); über Land (kurz beim Ablegen) gilt der zuletzt gefundene Spiegel
        if (SpiegelMitBecken(ziel.X, ziel.Z) is { } spiegel) ansicht.Spiegel = (float)spiegel;
        else if (!ansicht.Sichtbar) ansicht.Spiegel = BodenHoehe(ziel.X, ziel.Z);
        var zeit = (float)_wasserZeit;
        ziel.Y = ansicht.Spiegel + 0.05f * Mathf.Sin(zeit * 0.8f + ansicht.Nummer * 1.7f);

        // Erscheint das Schiff (Rückfahrt vom Kartenrand, Partiebeginn), steht es gleich richtig; sonst gleitet es (Ablegen, Päckchen)
        var sofort = !ansicht.Sichtbar;
        ansicht.Sichtbar = true;
        knoten.Position = sofort ? ziel : knoten.Position.Lerp(ziel, (float)Math.Min(1, delta * 4));
        var gier = sofort ? drehung : Mathf.LerpAngle(knoten.Rotation.Y, drehung, (float)Math.Min(1, delta * 1.5));
        // leichtes Rollen und Stampfen
        knoten.Rotation = new Vector3(0.010f * Mathf.Sin(zeit * 0.6f + ansicht.Nummer), gier, 0.014f * Mathf.Sin(zeit * 0.75f + ansicht.Nummer * 1.3f));

        // Segel, Planke und Ladung nur beim Wechsel schalten
        var faehrt = schiff.Zustand is SchiffZustand.Hinfahrt or SchiffZustand.Rueckfahrt;
        if (ansicht.GezeigtesSegel != faehrt)
        {
            ansicht.GezeigtesSegel = faehrt;
            if (ansicht.SegelGesetzt is not null) ansicht.SegelGesetzt.Visible = faehrt;
            if (ansicht.SegelGerefft is not null) ansicht.SegelGerefft.Visible = !faehrt;
        }
        var planke = liegt && paeckchen == 0;
        if (ansicht.GezeigtePlanke != planke)
        {
            ansicht.GezeigtePlanke = planke;
            if (ansicht.Planke is not null) ansicht.Planke.Visible = planke;
        }
        var kapazitaet = Math.Max(1, schiff.Daten.Gesamtkapazitaet);
        var stapel = schiff.Geladen <= 0 ? 0 : Math.Clamp((int)Math.Ceiling((double)schiff.Geladen / kapazitaet * ansicht.Ladung.Length), 1, ansicht.Ladung.Length);
        if (stapel != ansicht.GezeigteLadung)
        {
            ansicht.GezeigteLadung = stapel;
            for (var i = 0; i < ansicht.Ladung.Length; i++) ansicht.Ladung[i].Visible = i < stapel;
        }

        if (ansicht.Schiffer is { } schiffer)
            FigurImBildBewegen(schiffer, knoten.Position, _simulation?.Takt.IstPausiert == true ? 0 : delta);
    }

    /// <summary>Wo die Kamera für einen Hafen bzw. ein Schiff hinfährt (Betriebsleiste, Hafentafel); null, wenn die Kennung keins ist.</summary>
    private Vector3? HafenOderSchiffMitte(string kennung)
    {
        if (_wirtschaft is not { } w) return null;
        if (HafenAusKennung(kennung) is { } h && w.Schiffahrt.Hafen(h) is { } hafen)
            return new Vector3((float)hafen.Liegeplatz.X, 0, (float)hafen.Liegeplatz.Z);
        if (SchiffName(kennung) is { } name && w.Schiffahrt.Schiff(name) is { } schiff && _schiffe.TryGetValue(schiff, out var ansicht) && ansicht.Sichtbar)
            return ansicht.Wurzel.Position with { Y = 0 };
        return null;
    }

    /// <summary>
    /// Klickfläche und Name eines Hafens der Karte (Umlandstelle mit Wassermodell, aus <see cref="UmlandstellenBauen"/>): ein Klick öffnet die Hafentafel.
    /// Die Fläche deckt Kai und Gebäude des Modells (Größe aus dem Katalog), nicht das Wasser davor – dort liegen die Schiffe mit ihrer eigenen.
    /// <paramref name="wurzel"/> trägt schon Lage, Drehung und Maßstab des Gebäudes (<see cref="ModellLage"/>): die Maße hier ungedreht und ohne Maßstab.
    /// </summary>
    private void HafenKlickbar(Kartengebaeude g, Node3D wurzel)
    {
        var groesse = g.Baustein?.Groesse is { Length: 2 } gr ? new Vector3((float)gr[0], 8, (float)gr[1]) : new Vector3(16, 8, 14);
        var kennung = HafenKennung(g.Kennung);
        Klickflaeche(kennung, new Vector3(0, 4, 0), groesse, wurzel);
        Beschriftung(g.Name != "" ? g.Name : "Hafen", new Vector3(0, 13, 0), 44, wurzel, kennung);
    }

    /// <summary>Kran und Lagerschuppen gehören zum Hafen: Wer bis zu so viele Meter vom Hafen entfernt steht, öffnet bei Klick dessen Tafel.</summary>
    private const double HafenbautenReichweite = 60;

    /// <summary>
    /// Hafenkran und Lagerschuppen (Sonderbauten der Karte, aus <see cref="SonderbautenBauen"/>): ein Klick öffnet die Tafel des nächsten Hafens, wenn einer
    /// nah genug steht. <paramref name="wurzel"/> trägt Lage, Drehung und Maßstab wie bei <see cref="HafenKlickbar"/>.
    /// </summary>
    private void HafenbauKlickbar(Stadtkarte karte, Kartengebaeude g, Node3D wurzel)
    {
        if (g.Katalog is not ("hafenkran" or "lagerschuppen") || g.Baustein?.Groesse is not { Length: 2 } gr) return;
        var naechster = karte.GebaeudeDerArt(Bausteinart.Umlandstelle)
            .Where(h => h.Baustein?.Modell is { } m && m.StartsWith("wasser/"))
            .Select(h => (h.Kennung, Abstand: Math.Sqrt(Math.Pow(h.Punkt[0] - g.Punkt[0], 2) + Math.Pow(h.Punkt[1] - g.Punkt[1], 2))))
            .Where(h => h.Abstand <= HafenbautenReichweite).OrderBy(h => h.Abstand).Select(h => h.Kennung).FirstOrDefault();
        if (naechster is null) return;
        Klickflaeche(HafenKennung(naechster), new Vector3(0, 4, 0), new Vector3((float)gr[0], 8, (float)gr[1]), wurzel);
    }
}
