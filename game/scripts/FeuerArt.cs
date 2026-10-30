using Godot;

namespace Reichsgilde;

/// <summary>Was brennt: eine aufrechte Flamme, ein flaches Glutbett mit niedrigen Flammen (Esse) oder nur Rauch (Kamin, Schornstein).</summary>
public enum FeuerForm { Flamme, Glut, NurRauch }

/// <summary>Die fertigen Größen: Kerze, Fackel, Laterne, Feuerkorb, Esse, Kamin, Gebäudebrand (Dach) und Fensterbrand (Flammen aus einem Fenster).</summary>
public enum FeuerVorgabe { Kerze, Fackel, Laterne, Feuerkorb, Esse, Kamin, Gebaeudebrand, Fensterbrand }

/// <summary>Welches Flipbook der Blender-Simulation (<c>tools/feuer/flammen_sim.py</c>) die Flamme zeigt: ruhig (alle kleinen Feuer) oder wild mit Stichflammen (Brand).</summary>
public enum FeuerFlipbook { Ruhig, Wild }

/// <summary>
/// Alle Maße und Werte eines Feuers: Größe der Flamme, Tönung, Funken, Rauch und das Licht. Seit Phase 2 (Nutzer 30.10.2026) gibt es nur noch
/// eine Fassung für alle Grafikstufen: die Flamme ist das Flipbook der Blender-Simulation auf einer Fläche je Zunge (<c>flamme_sim.gdshader</c>),
/// der Rauch eine Säule aus Flächen mit <c>rauch.gdshader</c>, die Funken rechnet der Flammen-Shader mit. Die fertigen Größen stehen in
/// <see cref="Neu"/> (je Vorgabe eine geteilte Ressource, damit sich alle Fackeln ein Material teilen); im Godot-Editor lässt sich eine Art auch
/// selbst anlegen (Neue Ressource → FeuerArt) und an einen <see cref="Feuer"/>-Knoten hängen.
/// </summary>
[GlobalClass]
public partial class FeuerArt : Resource
{
    [ExportGroup("Flamme")]
    [Export] public string Bezeichnung { get; set; } = "Fackel";
    [Export] public FeuerForm Form { get; set; } = FeuerForm.Flamme;
    /// <summary>Welche Bildfolge der Simulation die Flamme zeigt.</summary>
    [Export] public FeuerFlipbook Flipbook { get; set; } = FeuerFlipbook.Ruhig;
    /// <summary>Höhe einer Flammenzunge in Metern.</summary>
    [Export] public float Hoehe { get; set; } = 0.42f;
    /// <summary>Breite einer Zunge in Metern (am dicksten Teil); das Bild der Simulation wird nur mäßig auf sie gestreckt.</summary>
    [Export] public float Breite { get; set; } = 0.2f;
    /// <summary>Wie viele Zungen nebeneinander brennen (Feuerkorb, Brand).</summary>
    [Export] public int Zungen { get; set; } = 1;
    /// <summary>Auf einer Scheibe dieses Halbmessers (Metern) stehen die Zungen verstreut.</summary>
    [Export] public float Radius { get; set; }
    /// <summary>Wie lebhaft das Glutbett atmet (nur <see cref="FeuerForm.Glut"/>; 0 = ruhig … 1 = unruhig).</summary>
    [Export(PropertyHint.Range, "0,1")] public float Flackern { get; set; } = 0.35f;
    /// <summary>Tempo der Bildfolge (1 = wie gebacken); eine Kerze brennt langsamer und ruhiger als eine Fackel.</summary>
    [Export] public float Tempo { get; set; } = 1f;
    /// <summary>HDR-Helligkeit (über 1 erfasst der Bloom den hellen Fuß der Flamme).</summary>
    [Export] public float Helligkeit { get; set; } = 1.7f;
    /// <summary>Färbt das Leuchten der gebackenen Flamme (Weiß = wie gebacken; die Esse etwas röter).</summary>
    [Export] public Color Toenung { get; set; } = Colors.White;

    /// <summary>Funken je Zunge (höchstens 12), im Flammen-Shader gerechnet.</summary>
    [ExportGroup("Funken")]
    [Export] public int Funken { get; set; }
    /// <summary>Wie hoch die Funken steigen, in Flammenhöhen.</summary>
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
    /// <summary>Wie stark das Feuer den Fuß der Säule anleuchtet.</summary>
    [Export(PropertyHint.Range, "0,1")] public float RauchGlut { get; set; } = 0.5f;
    /// <summary>So viele Flächen mit verschiedenem Startwert ergeben eine dichtere Säule.</summary>
    [Export] public int RauchSchichten { get; set; } = 1;
    /// <summary>Deckkraft der Säule bei Tag und bei Nacht (Faktor, 1 = wie <see cref="RauchMenge"/> sagt); dazwischen nach der Umgebung.</summary>
    [Export(PropertyHint.Range, "0,1")] public float RauchDeckungTag { get; set; } = 1f;
    [Export(PropertyHint.Range, "0,1")] public float RauchDeckungNacht { get; set; } = 1f;

    /// <summary>Energie des OmniLight bei mittlerer Helligkeit; 0 = kein Licht (Kamin).</summary>
    [ExportGroup("Licht")]
    [Export] public float LichtEnergie { get; set; } = 2.4f;
    [Export] public float LichtReichweite { get; set; } = 9f;
    /// <summary>Wie stark die Energie schwankt (0–1; sie folgt der Helligkeit des Bildes, das die Flamme gerade zeigt).</summary>
    [Export(PropertyHint.Range, "0,1")] public float LichtFlackern { get; set; } = 0.3f;
    /// <summary>Farbe, wenn die Flamme klein ist (rötlich), und wenn sie hoch ist (gelblicher).</summary>
    [Export] public Color LichtFarbeKalt { get; set; } = new(1.0f, 0.45f, 0.16f);
    [Export] public Color LichtFarbeHeiss { get; set; } = new(1.0f, 0.68f, 0.34f);
    /// <summary>Fernausblendung (DistanceFade): ab dieser Entfernung zur Kamera (Meter) wird das Licht schwächer, nach der Länge ist es aus.</summary>
    [Export] public float LichtAusblendenAb { get; set; } = 45f;
    [Export] public float LichtAusblendenLaenge { get; set; } = 20f;

    private static readonly System.Collections.Generic.Dictionary<FeuerVorgabe, FeuerArt> _vorgaben = new();

    /// <summary>Die fertige Art zu einer Vorgabe (geteilt: dieselbe Ressource für alle, die sie wählen).</summary>
    public static FeuerArt Neu(FeuerVorgabe vorgabe)
    {
        if (_vorgaben.TryGetValue(vorgabe, out var art)) return art;
        return _vorgaben[vorgabe] = Erzeugen(vorgabe);
    }

    private static FeuerArt Erzeugen(FeuerVorgabe v) => v switch
    {
        // Kerze: klein und ruhig (die Bildfolge läuft langsamer), ein sanftes Licht
        FeuerVorgabe.Kerze => new FeuerArt
        {
            Bezeichnung = "Kerze", Hoehe = 0.055f, Breite = 0.022f, Tempo = 0.55f, Helligkeit = 1.6f,
            LichtEnergie = 0.55f, LichtReichweite = 3.2f, LichtFlackern = 0.14f, LichtFarbeKalt = new Color(1.0f, 0.55f, 0.22f), LichtFarbeHeiss = new Color(1.0f, 0.72f, 0.38f),
            LichtAusblendenAb = 12f, LichtAusblendenLaenge = 6f,
        },
        // Fackel: zwei Zungen, ein paar Funken, ein Faden dunkler Rauch
        FeuerVorgabe.Fackel => new FeuerArt
        {
            Bezeichnung = "Fackel", Hoehe = 0.42f, Breite = 0.2f, Zungen = 2, Radius = 0.025f, Tempo = 1f,
            Funken = 3, FunkenHoehe = 3.2f, FunkenGroesse = 0.011f,
            RauchMenge = 0.4f, RauchHoehe = 1.9f, RauchBreite = 0.55f, RauchRuss = 0.6f, RauchGlut = 0.6f,
            LichtEnergie = 2.4f, LichtReichweite = 9f, LichtFlackern = 0.32f,
        },
        // Laterne: ein kleines, ruhiges Flämmchen hinter Glas oder Horn, kein Rauch
        FeuerVorgabe.Laterne => new FeuerArt
        {
            Bezeichnung = "Laterne", Hoehe = 0.1f, Breite = 0.045f, Tempo = 0.65f, Helligkeit = 1.6f,
            LichtEnergie = 1.1f, LichtReichweite = 5.5f, LichtFlackern = 0.12f, LichtFarbeKalt = new Color(1.0f, 0.55f, 0.2f), LichtFarbeHeiss = new Color(1.0f, 0.7f, 0.35f),
            LichtAusblendenAb = 35f, LichtAusblendenLaenge = 15f,
        },
        // Feuerkorb: breit, mehrere Zungen, viele Funken, Rauch
        FeuerVorgabe.Feuerkorb => new FeuerArt
        {
            Bezeichnung = "Feuerkorb", Hoehe = 0.72f, Breite = 0.46f, Zungen = 4, Radius = 0.17f, Tempo = 1f, Helligkeit = 1.8f,
            Funken = 5, FunkenHoehe = 3.4f, FunkenGroesse = 0.013f,
            RauchMenge = 0.5f, RauchHoehe = 3.2f, RauchBreite = 1.0f, RauchRuss = 0.5f, RauchGlut = 0.6f, RauchSchichten = 2,
            LichtEnergie = 4.5f, LichtReichweite = 12f, LichtFlackern = 0.3f, LichtAusblendenAb = 60f,
        },
        // Esse: flaches Glutbett mit niedrigen, gedämpften Flammen (sonst nur ein heller Fleck im Bloom) und aufstiebenden Glutstückchen
        FeuerVorgabe.Esse => new FeuerArt
        {
            Bezeichnung = "Esse", Form = FeuerForm.Glut, Hoehe = 0.22f, Breite = 0.18f, Zungen = 3, Radius = 0.18f, Flackern = 0.25f, Tempo = 0.85f, Helligkeit = 1.0f,
            Toenung = new Color(1.0f, 0.8f, 0.7f),
            Funken = 5, FunkenHoehe = 5f, FunkenGroesse = 0.010f,
            RauchMenge = 0.3f, RauchHoehe = 2.6f, RauchBreite = 0.6f, RauchRuss = 0.4f, RauchGlut = 0.5f,
            LichtEnergie = 3.2f, LichtReichweite = 7.5f, LichtFlackern = 0.22f, LichtFarbeKalt = new Color(1.0f, 0.38f, 0.1f), LichtFarbeHeiss = new Color(1.0f, 0.6f, 0.25f),
            LichtAusblendenAb = 40f,
        },
        // Kamin: nur Rauch (das Feuer brennt im Haus), kein Licht; heller als der Rauch eines Brandes. Nur ein Hauch (Nutzer 30.10.2026):
        // bei Tag kaum zu sehen, nachts dezent
        FeuerVorgabe.Kamin => new FeuerArt
        {
            Bezeichnung = "Kamin", Form = FeuerForm.NurRauch, Zungen = 0,
            RauchMenge = 0.55f, RauchHoehe = 6f, RauchBreite = 1.8f, RauchRuss = 0.3f, RauchGlut = 0f, RauchSchichten = 2,
            RauchDeckungTag = 0.3f, RauchDeckungNacht = 0.5f,
            LichtEnergie = 0f,
        },
        // Gebäudebrand: das wilde Flipbook (Stichflammen) aus dem Dach, viele Funken, dicker schwarzer Rauch, großes Licht
        FeuerVorgabe.Gebaeudebrand => new FeuerArt
        {
            Bezeichnung = "Gebäudebrand", Flipbook = FeuerFlipbook.Wild, Hoehe = 3.0f, Breite = 1.9f, Zungen = 10, Radius = 2.2f, Tempo = 1.1f, Helligkeit = 1.8f,
            Funken = 8, FunkenHoehe = 2.6f, FunkenGroesse = 0.05f,
            RauchMenge = 1f, RauchHoehe = 16f, RauchBreite = 6f, RauchRuss = 0.85f, RauchGlut = 0.7f, RauchSchichten = 3,
            LichtEnergie = 16f, LichtReichweite = 30f, LichtFlackern = 0.35f, LichtAusblendenAb = 140f, LichtAusblendenLaenge = 50f,
        },
        // Fensterbrand: Flammen schlagen aus einem Fenster (gehört zum Gebäudebrand, ohne eigenes Licht)
        FeuerVorgabe.Fensterbrand => new FeuerArt
        {
            Bezeichnung = "Fensterbrand", Flipbook = FeuerFlipbook.Wild, Hoehe = 1.1f, Breite = 0.85f, Zungen = 3, Radius = 0.3f, Tempo = 1.15f,
            Funken = 3, FunkenHoehe = 2.2f, FunkenGroesse = 0.03f,
            RauchMenge = 0.6f, RauchHoehe = 4.5f, RauchBreite = 1.8f, RauchRuss = 0.8f, RauchGlut = 0.7f,
            LichtEnergie = 0f,
        },
        _ => new FeuerArt(),
    };
}
