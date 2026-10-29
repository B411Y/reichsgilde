using Godot;

namespace Reichsgilde;

/// <summary>Welche Technik ein Feuer zeichnet: A = Shader-Flamme (billig, für Niedrig und Mittel), B = Partikel-Flamme (für Hoch und Ultra).</summary>
public enum FeuerTechnik { Shader, Partikel }

/// <summary>Was brennt: eine aufrechte Flamme, ein flaches Glutbett (Esse) oder nur Rauch (Kamin, Schornstein).</summary>
public enum FeuerForm { Flamme, Glut, NurRauch }

/// <summary>Die fertigen Größen: Kerze, Fackel, Laterne, Feuerkorb, Esse, Kamin, Gebäudebrand (Dach) und Fensterbrand (Flammen aus einem Fenster).</summary>
public enum FeuerVorgabe { Kerze, Fackel, Laterne, Feuerkorb, Esse, Kamin, Gebaeudebrand, Fensterbrand }

/// <summary>
/// Grafikstufe der Feuer (Phase 1b, Nutzer 29.10.2026): Auf Niedrig zeichnet jedes Feuer Flamme und Rauch in Technik A, ab Mittel gilt die
/// Zuordnung je Feuerart (<see cref="FeuerArt.TechnikAbMittel"/>) und der Rauch ist immer Technik B. Mittel und Hoch unterscheiden sich bei den
/// Flammen nicht; im Spiel wird es dort nur mehr echte Lichter geben (Pool wie bei den Nachtlichtern).
/// </summary>
public enum Feuerqualitaet { Niedrig, Mittel, Hoch }

/// <summary>Welche Fassung der Technik A: die gerechnete Rauschen-Flamme (Phase 1, <c>flamme.gdshader</c>) oder das Flipbook aus der Blender-Simulation (Phase 1b, <c>flamme_sim.gdshader</c>); beide eine Fläche je Zunge.</summary>
public enum ShaderFassung { Rauschen, Simulation }

/// <summary>Welches Flipbook der Blender-Simulation (<c>tools/feuer/flammen_sim.py</c>): ruhig (Fackel, Feuerkorb) oder wild mit Stichflammen (Brand). Keins: Kerze, Laterne und Esse bleiben in Technik A bei der Rauschen-Flamme (kleine, niedrige Flammen; das Flipbook wird dort nur ein heller Fleck im Bloom).</summary>
public enum FeuerFlipbook { Keins, Ruhig, Wild }

/// <summary>
/// Alle Maße und Werte eines Feuers (Phase 1 „Feuer und Flammen“): Größe der Flamme, Farben, Flackern, Funken, Rauch und das Licht. Beide Techniken
/// (<see cref="FeuerTechnik"/>) lesen dieselbe Ressource, so sieht ein Feuer in A und B gleich groß und gleich hell aus. Die fertigen Größen stehen
/// in <see cref="Neu"/> (je Vorgabe eine geteilte Ressource, damit sich alle Fackeln ein Material teilen); im Godot-Editor lässt sich eine Art auch
/// selbst anlegen (Neue Ressource → FeuerArt) und an einen <see cref="Feuer"/>-Knoten hängen.
/// </summary>
[GlobalClass]
public partial class FeuerArt : Resource
{
    [ExportGroup("Flamme")]
    [Export] public string Bezeichnung { get; set; } = "Fackel";
    [Export] public FeuerForm Form { get; set; } = FeuerForm.Flamme;
    /// <summary>Höhe einer Flammenzunge in Metern.</summary>
    [Export] public float Hoehe { get; set; } = 0.42f;
    /// <summary>Breite einer Zunge in Metern (am dicksten Teil).</summary>
    [Export] public float Breite { get; set; } = 0.2f;
    /// <summary>Wie viele Zungen nebeneinander brennen (Feuerkorb, Brand).</summary>
    [Export] public int Zungen { get; set; } = 1;
    /// <summary>Auf einer Scheibe dieses Halbmessers (Metern) stehen die Zungen verstreut.</summary>
    [Export] public float Radius { get; set; }
    /// <summary>0 = ruhig (Kerze) … 1 = wild.</summary>
    [Export(PropertyHint.Range, "0,1")] public float Flackern { get; set; } = 0.35f;
    [Export] public float Tempo { get; set; } = 1f;
    /// <summary>HDR-Helligkeit des Kerns (über 1 erfasst ihn der Bloom).</summary>
    [Export] public float Helligkeit { get; set; } = 1.7f;
    [Export] public Color FarbeKern { get; set; } = new(1.0f, 0.96f, 0.70f);
    [Export] public Color FarbeMitte { get; set; } = new(1.0f, 0.55f, 0.08f);
    [Export] public Color FarbeRand { get; set; } = new(0.75f, 0.10f, 0.02f);

    /// <summary>Technik A: Funken je Zunge (höchstens 12).</summary>
    [ExportGroup("Funken")]
    [Export] public int Funken { get; set; }
    /// <summary>Technik B: Funken im Ganzen.</summary>
    [Export] public int PartikelFunken { get; set; }
    /// <summary>Wie hoch die Funken steigen, in Flammenhöhen (A) bzw. Metern (B: Höhe · dieser Wert).</summary>
    [Export] public float FunkenHoehe { get; set; } = 2.5f;
    /// <summary>Größe eines Funkens in Metern.</summary>
    [Export] public float FunkenGroesse { get; set; } = 0.012f;

    /// <summary>0 = kein Rauch … 1 = dicker Rauch.</summary>
    [ExportGroup("Rauch")]
    [Export(PropertyHint.Range, "0,1")] public float RauchMenge { get; set; }
    /// <summary>Wie hoch die Säule reicht, in Metern.</summary>
    [Export] public float RauchHoehe { get; set; } = 2f;
    /// <summary>Breite der Säule oben, in Metern.</summary>
    [Export] public float RauchBreite { get; set; } = 0.5f;
    /// <summary>0 = heller Dampf … 1 = schwarzer Ruß.</summary>
    [Export(PropertyHint.Range, "0,1")] public float RauchRuss { get; set; } = 0.4f;
    /// <summary>Wie stark das Feuer den Fuß der Säule anleuchtet (Technik A; B leuchtet das echte Licht an).</summary>
    [Export(PropertyHint.Range, "0,1")] public float RauchGlut { get; set; } = 0.5f;
    /// <summary>Technik A: so viele Flächen mit verschiedenem Startwert ergeben eine dichtere Säule.</summary>
    [Export] public int RauchSchichten { get; set; } = 1;

    /// <summary>Energie des OmniLight bei mittlerer Helligkeit; 0 = kein Licht (Kamin).</summary>
    [ExportGroup("Licht")]
    [Export] public float LichtEnergie { get; set; } = 2.4f;
    [Export] public float LichtReichweite { get; set; } = 9f;
    /// <summary>Wie stark die Energie schwankt (0–1; die Flamme flackert im selben Takt).</summary>
    [Export(PropertyHint.Range, "0,1")] public float LichtFlackern { get; set; } = 0.3f;
    /// <summary>Farbe, wenn die Flamme klein ist (rötlich), und wenn sie hoch ist (gelblicher).</summary>
    [Export] public Color LichtFarbeKalt { get; set; } = new(1.0f, 0.45f, 0.16f);
    [Export] public Color LichtFarbeHeiss { get; set; } = new(1.0f, 0.68f, 0.34f);
    /// <summary>Fernausblendung (DistanceFade): ab dieser Entfernung zur Kamera (Meter) wird das Licht schwächer, nach der Länge ist es aus.</summary>
    [Export] public float LichtAusblendenAb { get; set; } = 45f;
    [Export] public float LichtAusblendenLaenge { get; set; } = 20f;

    /// <summary>Technik der Flamme ab Mittel (Nutzer 29.10.2026): A für Kerze, Laterne und Esse, B für Fackel, Feuerkorb, Kamin und Gebäudebrand. Auf Niedrig immer A.</summary>
    [ExportGroup("Zuordnung")]
    [Export] public FeuerTechnik TechnikAbMittel { get; set; } = FeuerTechnik.Partikel;
    /// <summary>Welches Flipbook der Simulation die Flamme zeigt (Technik B und Technik A in der Fassung „Simulation“).</summary>
    [Export] public FeuerFlipbook Flipbook { get; set; } = FeuerFlipbook.Ruhig;
    /// <summary>Technik A, Fassung „Rauschen“: 0 = ruhig … 1 = viel Bewegung und häufige Stichflammen (kurze, hohe Ausbrüche) für große Feuer.</summary>
    [Export(PropertyHint.Range, "0,1")] public float Stichflammen { get; set; }

    /// <summary>Die Technik der Flamme auf einer Grafikstufe: Niedrig immer A, sonst <see cref="TechnikAbMittel"/>.</summary>
    public FeuerTechnik FlammeFuer(Feuerqualitaet stufe) => stufe == Feuerqualitaet.Niedrig ? FeuerTechnik.Shader : TechnikAbMittel;

    /// <summary>Die Technik des Rauchs auf einer Grafikstufe: Niedrig A, sonst immer B (Nutzer 29.10.2026: der Rauch von B gefällt überall besser).</summary>
    public static FeuerTechnik RauchFuer(Feuerqualitaet stufe) => stufe == Feuerqualitaet.Niedrig ? FeuerTechnik.Shader : FeuerTechnik.Partikel;

    /// <summary>Flammenteilchen je Zunge.</summary>
    [ExportGroup("Partikel (Technik B)")]
    [Export] public int PartikelProZunge { get; set; } = 6;
    /// <summary>Wie lange ein Flammenteilchen lebt (Sekunden).</summary>
    [Export] public float PartikelLebensdauer { get; set; } = 0.75f;

    private static readonly System.Collections.Generic.Dictionary<FeuerVorgabe, FeuerArt> _vorgaben = new();

    /// <summary>Die fertige Art zu einer Vorgabe (geteilt: dieselbe Ressource für alle, die sie wählen).</summary>
    public static FeuerArt Neu(FeuerVorgabe vorgabe)
    {
        if (_vorgaben.TryGetValue(vorgabe, out var art)) return art;
        return _vorgaben[vorgabe] = Erzeugen(vorgabe);
    }

    private static FeuerArt Erzeugen(FeuerVorgabe v) => v switch
    {
        // Kerze: klein, ruhig, ein sanftes Licht
        FeuerVorgabe.Kerze => new FeuerArt
        {
            Bezeichnung = "Kerze", TechnikAbMittel = FeuerTechnik.Shader, Flipbook = FeuerFlipbook.Keins, Hoehe = 0.05f, Breite = 0.022f, Flackern = 0.16f, Tempo = 0.8f, Helligkeit = 1.9f,
            PartikelProZunge = 2, PartikelLebensdauer = 0.6f,
            LichtEnergie = 0.55f, LichtReichweite = 3.2f, LichtFlackern = 0.14f, LichtFarbeKalt = new Color(1.0f, 0.55f, 0.22f), LichtFarbeHeiss = new Color(1.0f, 0.72f, 0.38f),
            LichtAusblendenAb = 12f, LichtAusblendenLaenge = 6f,
        },
        // Fackel: eine Hand voll Flammen, ein paar Funken, ein Faden dunkler Rauch
        FeuerVorgabe.Fackel => new FeuerArt
        {
            Bezeichnung = "Fackel", TechnikAbMittel = FeuerTechnik.Partikel, Flipbook = FeuerFlipbook.Ruhig, Hoehe = 0.42f, Breite = 0.2f, Zungen = 2, Radius = 0.025f, Flackern = 0.4f, Tempo = 1f,
            Funken = 3, PartikelFunken = 8, FunkenHoehe = 3.2f, FunkenGroesse = 0.011f,
            RauchMenge = 0.4f, RauchHoehe = 1.9f, RauchBreite = 0.55f, RauchRuss = 0.6f, RauchGlut = 0.6f,
            LichtEnergie = 2.4f, LichtReichweite = 9f, LichtFlackern = 0.32f,
        },
        // Laterne: ein kleines, ruhiges Flämmchen hinter Glas, kein Rauch
        FeuerVorgabe.Laterne => new FeuerArt
        {
            Bezeichnung = "Laterne", TechnikAbMittel = FeuerTechnik.Shader, Flipbook = FeuerFlipbook.Keins, Hoehe = 0.11f, Breite = 0.055f, Flackern = 0.12f, Tempo = 0.7f, Helligkeit = 1.8f,
            PartikelProZunge = 3, PartikelLebensdauer = 0.6f,
            LichtEnergie = 1.1f, LichtReichweite = 5.5f, LichtFlackern = 0.12f, LichtFarbeKalt = new Color(1.0f, 0.55f, 0.2f), LichtFarbeHeiss = new Color(1.0f, 0.7f, 0.35f),
            LichtAusblendenAb = 35f, LichtAusblendenLaenge = 15f,
        },
        // Feuerkorb: breit, mehrere Zungen, viele Funken, Rauch
        FeuerVorgabe.Feuerkorb => new FeuerArt
        {
            Bezeichnung = "Feuerkorb", TechnikAbMittel = FeuerTechnik.Partikel, Flipbook = FeuerFlipbook.Ruhig, Stichflammen = 0.15f, Hoehe = 0.72f, Breite = 0.46f, Zungen = 4, Radius = 0.17f, Flackern = 0.45f, Tempo = 1f, Helligkeit = 1.8f,
            Funken = 5, PartikelFunken = 14, FunkenHoehe = 3.4f, FunkenGroesse = 0.013f,
            RauchMenge = 0.5f, RauchHoehe = 3.2f, RauchBreite = 1.0f, RauchRuss = 0.5f, RauchGlut = 0.6f, RauchSchichten = 2,
            LichtEnergie = 4.5f, LichtReichweite = 12f, LichtFlackern = 0.3f, LichtAusblendenAb = 60f,
        },
        // Esse: flaches Glutbett mit niedrigen Flammen und aufstiebenden Glutstückchen
        FeuerVorgabe.Esse => new FeuerArt
        {
            Bezeichnung = "Esse", TechnikAbMittel = FeuerTechnik.Shader, Flipbook = FeuerFlipbook.Keins, Form = FeuerForm.Glut, Hoehe = 0.2f, Breite = 0.3f, Zungen = 3, Radius = 0.2f, Flackern = 0.25f, Tempo = 0.9f, Helligkeit = 1.8f,
            FarbeMitte = new Color(1.0f, 0.42f, 0.06f),
            Funken = 5, PartikelFunken = 14, FunkenHoehe = 6f, FunkenGroesse = 0.010f,
            RauchMenge = 0.3f, RauchHoehe = 2.6f, RauchBreite = 0.6f, RauchRuss = 0.4f, RauchGlut = 0.5f,
            LichtEnergie = 3.2f, LichtReichweite = 7.5f, LichtFlackern = 0.22f, LichtFarbeKalt = new Color(1.0f, 0.38f, 0.1f), LichtFarbeHeiss = new Color(1.0f, 0.6f, 0.25f),
            LichtAusblendenAb = 40f,
        },
        // Kamin: nur Rauch (das Feuer brennt im Haus), kein Licht
        FeuerVorgabe.Kamin => new FeuerArt
        {
            Bezeichnung = "Kamin", TechnikAbMittel = FeuerTechnik.Partikel, Flipbook = FeuerFlipbook.Keins, Form = FeuerForm.NurRauch, Zungen = 0,
            RauchMenge = 0.6f, RauchHoehe = 7f, RauchBreite = 1.8f, RauchRuss = 0.25f, RauchGlut = 0f, RauchSchichten = 2,
            LichtEnergie = 0f,
        },
        // Gebäudebrand: Flammen aus dem Dach, viele Funken, dicker schwarzer Rauch, großes Licht
        FeuerVorgabe.Gebaeudebrand => new FeuerArt
        {
            Bezeichnung = "Gebäudebrand", TechnikAbMittel = FeuerTechnik.Partikel, Flipbook = FeuerFlipbook.Wild, Stichflammen = 1f, Hoehe = 2.8f, Breite = 1.6f, Zungen = 10, Radius = 2.3f, Flackern = 0.5f, Tempo = 1.1f, Helligkeit = 1.9f,
            Funken = 8, PartikelFunken = 50, FunkenHoehe = 2.6f, FunkenGroesse = 0.05f, PartikelProZunge = 5, PartikelLebensdauer = 0.95f,
            RauchMenge = 1f, RauchHoehe = 16f, RauchBreite = 6f, RauchRuss = 0.85f, RauchGlut = 0.7f, RauchSchichten = 3,
            LichtEnergie = 16f, LichtReichweite = 30f, LichtFlackern = 0.35f, LichtAusblendenAb = 140f, LichtAusblendenLaenge = 50f,
        },
        // Fensterbrand: Flammen schlagen aus einem Fenster (gehört zum Gebäudebrand, ohne eigenes Licht)
        FeuerVorgabe.Fensterbrand => new FeuerArt
        {
            Bezeichnung = "Fensterbrand", TechnikAbMittel = FeuerTechnik.Partikel, Flipbook = FeuerFlipbook.Wild, Stichflammen = 0.6f, Hoehe = 1.1f, Breite = 0.85f, Zungen = 3, Radius = 0.3f, Flackern = 0.5f, Tempo = 1.15f,
            Funken = 3, PartikelFunken = 10, FunkenHoehe = 2.2f, FunkenGroesse = 0.03f, PartikelProZunge = 5,
            RauchMenge = 0.6f, RauchHoehe = 4.5f, RauchBreite = 1.8f, RauchRuss = 0.8f, RauchGlut = 0.7f,
            LichtEnergie = 0f,
        },
        _ => new FeuerArt(),
    };
}
