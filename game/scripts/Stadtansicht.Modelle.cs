using Godot;

namespace Reichsgilde;

/// <summary>
/// Die gemeinsame Einsetz-Hilfe für Modelle aus Blender (<c>game/assets/modelle/*.glb</c>) – zehnter Teil der <c>partial class Stadtansicht</c>.
/// Wohnhäuser der Spielhäuser, Einwohnerhäuser und Werkstätten setzen ihre Modelle alle über <see cref="ModellEinsetzen"/> ein:
/// Laden (jede Datei nur einmal), Zustände nach Namen schalten, Hausfarbe, Ruß, Fahne, Fensterlicht und Ankerpunkte.
/// Konventionen der Modelle: <c>docs/bereiche/darstellung.md</c>.
/// </summary>
public partial class Stadtansicht
{
    // ═════════════════════════════════════════════════════════════════════════
    //  Laden
    // ═════════════════════════════════════════════════════════════════════════

    private readonly Dictionary<string, PackedScene?> _modellSzenen = new();

    /// <summary>
    /// Das Modell <c>res://assets/modelle/&lt;datei&gt;.glb</c>, einmal geladen. Fehlt die Datei, meldet das Spiel einen Fehler
    /// (der Rauchtest schlägt an) und gibt null zurück – es gibt keinen Rückfall auf Grundkörper.
    /// </summary>
    private PackedScene? Modell(string datei)
    {
        if (_modellSzenen.TryGetValue(datei, out var szene)) return szene;
        var pfad = $"res://assets/modelle/{datei}.glb";
        szene = ResourceLoader.Exists(pfad) ? ResourceLoader.Load<PackedScene>(pfad) : null;
        if (szene is null) GD.PushError($"Modell fehlt: {pfad}");
        return _modellSzenen[datei] = szene;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Einsetzen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Welche Zustandsknoten eines Modells sichtbar sind. Ein Modell kennt „Stufe1“–„Stufe3“ (genau eine sichtbar), „Variante1“–„Variante3“
    /// (genau eine sichtbar), „Geruest“, „Leerstand“ sowie „Wappen“ und „Fahne“ (nur mit Besitzer, nie im Leerstand).
    /// </summary>
    /// <param name="Stufe">Betriebsstufe 1 bis 3; 0 blendet alle Stufenknoten aus (Modelle ohne Stufen haben keine).</param>
    /// <param name="Variante">1 bis 3 zeigt nur diese Variante; 0 lässt alle Varianten stehen.</param>
    /// <param name="Geruest">Gerüst vor dem Haus (Ausbau, Sanierung, Brand).</param>
    /// <param name="Leerstand">Bretter vor Tür und Fenstern, dunkles Glas statt Fensterlicht, ohne Wappen und Fahne.</param>
    /// <param name="Brand">Rußige Dachmaterialien.</param>
    /// <param name="Besitzer">Farbe des Besitzers für „Hausfarbe“, Wappen und Fahne; ohne Besitzer verwittertes Holz.</param>
    private sealed record ModellZustand(int Stufe = 0, int Variante = 0, bool Geruest = false, bool Leerstand = false, bool Brand = false, Color? Besitzer = null);

    /// <summary>
    /// Ein eingesetztes Modell: der Wurzelknoten und die eigenen Kopien des Materials „Glut“ (leer, wenn das Modell keins hat) samt ihrer
    /// Stärke bei der Arbeit (<paramref name="GlutAn"/>, die Emissionsstärke aus der Datei).
    /// </summary>
    private sealed record ModellInstanz(Node3D Wurzel, StandardMaterial3D[] Glut, float GlutAn);

    /// <summary>
    /// Setzt das Modell <paramref name="datei"/> als Kind von <paramref name="eltern"/> an die Stelle <paramref name="lage"/> (lokal im Elternknoten):
    /// blendet die Zustandsknoten nach <paramref name="zustand"/> ein oder aus, tauscht das Material „Hausfarbe“ gegen die Besitzerfarbe,
    /// färbt nach einem Brand das Dach rußig, belebt die Fahne, meldet die Fenster fürs Nachtlicht (<see cref="FensterlichtSetzen"/>) und
    /// merkt sich „Anker_Tuer“ und „Anker_Karren“ unter <paramref name="ankerKennung"/> (<see cref="TuerAnker"/>, <see cref="KarrenAnker"/>).
    /// Null, wenn die Datei fehlt.
    /// </summary>
    private ModellInstanz? ModellEinsetzen(string datei, Node3D eltern, Vector3 lage, ModellZustand zustand, string? ankerKennung = null)
    {
        if (Modell(datei) is not { } szene) return null;
        var modell = szene.Instantiate<Node3D>();
        modell.Position = lage;
        eltern.AddChild(modell);
        var glas = ModellGlas(datei, modell);
        var glut = new List<StandardMaterial3D>();
        var glutAn = 2.5f;
        foreach (var teil in modell.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            var name = teil.Name.ToString();
            teil.Visible = name switch
            {
                "Stufe1" => zustand.Stufe == 1,
                "Stufe2" => zustand.Stufe == 2,
                "Stufe3" => zustand.Stufe == 3,
                "Variante1" => zustand.Variante is 0 or 1,
                "Variante2" => zustand.Variante is 0 or 2,
                "Variante3" => zustand.Variante is 0 or 3,
                "Geruest" => zustand.Geruest,
                "Leerstand" => zustand.Leerstand,
                "Wappen" or "Fahne" => !zustand.Leerstand && zustand.Besitzer is not null,
                _ => true,
            };
            if (name == "Fahne")
            {
                // das Tuch der Fahne flattert im Wind (eigenes Netz mit Shader); ohne Besitzer ist die Fahne ohnehin ausgeblendet
                if (teil.Visible && zustand.Besitzer is { } tuchfarbe) ModellFahneBelebt(teil, tuchfarbe);
                continue;
            }
            if (teil.Mesh is not { } mesh) continue;
            for (var i = 0; i < mesh.GetSurfaceCount(); i++)
            {
                if (mesh.SurfaceGetMaterial(i) is not BaseMaterial3D stoff) continue;
                switch (stoff.ResourceName)
                {
                    case "Hausfarbe":
                        teil.SetSurfaceOverrideMaterial(i, Umgefaerbt(stoff, zustand.Besitzer ?? OhneHausfarbe));
                        break;
                    case "Fenster" when zustand.Leerstand:
                        teil.SetSurfaceOverrideMaterial(i, glas); // leere Häuser: dunkle Scheiben, kein Licht dahinter
                        break;
                    case "Fenster":
                        FensterMelden(stoff);
                        break;
                    case "Glut" when stoff is StandardMaterial3D glutVorlage:
                        // jede Werkstatt glüht für sich: eigene Kopie, nicht das Material, das alle teilen
                        var kopie = (StandardMaterial3D)glutVorlage.Duplicate();
                        glutAn = glutVorlage.EmissionEnergyMultiplier;
                        kopie.EmissionEnergyMultiplier = 0; // aus, bis gearbeitet wird (GlutSetzen)
                        teil.SetSurfaceOverrideMaterial(i, kopie);
                        glut.Add(kopie);
                        break;
                    default:
                        if (zustand.Brand && Dachstoffe.Contains(stoff.ResourceName))
                            teil.SetSurfaceOverrideMaterial(i, Umgefaerbt(stoff, stoff.AlbedoColor.Lerp(Russ, 0.8f)));
                        break;
                }
            }
        }
        if (ankerKennung is not null)
            _modellAnker[ankerKennung] = (modell.FindChild("Anker_Tuer", true, false) as Node3D, modell.FindChild("Anker_Karren", true, false) as Node3D);
        return new ModellInstanz(modell, glut.ToArray(), glutAn);
    }

    /// <summary>Zeigt von „Stufe1“ bis „Stufe3“ nur die Betriebsstufe und blendet das Gerüst ein oder aus (Ausbau einer Werkstatt).</summary>
    private static void ModellStufeSetzen(ModellInstanz modell, int stufe, bool geruest)
    {
        foreach (var teil in modell.Wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            switch (teil.Name.ToString())
            {
                case "Stufe1": teil.Visible = stufe == 1; break;
                case "Stufe2": teil.Visible = stufe == 2; break;
                case "Stufe3": teil.Visible = stufe == 3; break;
                case "Geruest": teil.Visible = geruest; break;
            }
    }

    /// <summary>Schaltet das Material „Glut“ dieses Modells (Esse, Ofen): hell, solange gearbeitet wird, sonst aus. Jedes Modell hat eigene Kopien.</summary>
    private static void GlutSetzen(ModellInstanz modell, bool an)
    {
        foreach (var stoff in modell.Glut) stoff.EmissionEnergyMultiplier = an ? modell.GlutAn : 0f;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Farben
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Hausfarbe eines Hauses ohne Besitzerfarbe (Leerstand, Sanierung): verwittertes Holz statt Spielerblau.</summary>
    private static readonly Color OhneHausfarbe = new(0.36f, 0.31f, 0.25f);

    private static readonly Color Russ = new(0.16f, 0.14f, 0.13f);

    /// <summary>Die Dachmaterialien der Modelle – nach einem Brand werden sie rußig.</summary>
    private static readonly HashSet<string> Dachstoffe = new()
    {
        "Dachziegel", "Ziegelkante", "Firstziegel", "Stroh", "Strohkante", "Firstsoden", "Schiefer", "Schieferkante", "Firstblech",
    };

    private readonly Dictionary<(Material Vorlage, Color Farbe), Material> _modellStoffe = new();

    /// <summary>Eine Kopie des Materials in anderer Farbe – je Vorlage und Farbe nur einmal.</summary>
    private Material Umgefaerbt(BaseMaterial3D vorlage, Color farbe)
    {
        if (_modellStoffe.TryGetValue((vorlage, farbe), out var stoff)) return stoff;
        var kopie = (BaseMaterial3D)vorlage.Duplicate();
        kopie.AlbedoColor = farbe;
        return _modellStoffe[(vorlage, farbe)] = kopie;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Fensterlicht
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Warmes Kerzenlicht hinter den Scheiben (sRGB).</summary>
    private static readonly Color FensterLichtFarbe = new(1f, 0.62f, 0.28f);

    /// <summary>Stärke der Emission des Fensterlichts bei Nacht.</summary>
    private const float FensterLichtStaerke = 2.2f;

    /// <summary>Das Material „Fenster“ jedes geladenen Modells – alle Instanzen einer Datei teilen es.</summary>
    private readonly HashSet<BaseMaterial3D> _fensterStoffe = new();
    private readonly Dictionary<string, Material> _modellGlas = new();
    private bool _fensterNacht;

    private void FensterMelden(BaseMaterial3D stoff)
    {
        if (_fensterStoffe.Add(stoff)) FensterSchalten(stoff, _fensterNacht);
    }

    /// <summary>Das dunkle Material „Glas“ eines Modells (für leere Häuser) – oder ein dunkles Ersatzglas, wenn das Modell keins hat.</summary>
    private Material ModellGlas(string datei, Node3D modell)
    {
        if (_modellGlas.TryGetValue(datei, out var glas)) return glas;
        foreach (var teil in modell.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            for (var i = 0; i < (teil.Mesh?.GetSurfaceCount() ?? 0); i++)
                if (teil.Mesh!.SurfaceGetMaterial(i) is { ResourceName: "Glas" } g)
                    return _modellGlas[datei] = g;
        return _modellGlas[datei] = Stoff(new Color(0.20f, 0.22f, 0.26f));
    }

    /// <summary>
    /// Schaltet das Licht hinter allen Fenstern der Stadt (Nacht an, Tag aus). Alle Häuser teilen je Datei ein Material „Fenster“,
    /// darum genügt ein Schalter; geschaltet wird nur beim Wechsel (<see cref="LichtNachUhrzeit"/>), nicht jedes Bild.
    /// </summary>
    private void FensterlichtSetzen(bool nacht)
    {
        if (nacht == _fensterNacht) return;
        _fensterNacht = nacht;
        foreach (var stoff in _fensterStoffe) FensterSchalten(stoff, nacht);
    }

    private static void FensterSchalten(BaseMaterial3D stoff, bool nacht)
    {
        stoff.EmissionEnabled = nacht;
        if (!nacht) return;
        stoff.Emission = FensterLichtFarbe;
        stoff.EmissionEnergyMultiplier = FensterLichtStaerke;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Ankerpunkte
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// „Anker_Tuer“ und „Anker_Karren“ der eingesetzten Modelle, je Kennung des Gebäudes: bei Werkstätten der Betriebsname, bei den
    /// Wohnhäusern der Spielhäuser <see cref="WohnhausKennung"/>. Gemerkt werden die Knoten selbst, nicht ihre Lage: So folgen die
    /// Anker einem Gebäude, das nachher gedreht oder in eine Gruppe verschoben wird. Setzt ein Neubau dieselbe Kennung neu, ersetzt er den Eintrag.
    /// </summary>
    private readonly Dictionary<string, (Node3D? Tuer, Node3D? Karren)> _modellAnker = new();

    /// <summary>Ort, an dem Personen das Gebäude betreten (in Koordinaten der Stadt, mit der Höhe der obersten Stufe) – null, wenn es keinen Anker gibt.</summary>
    private Vector3? TuerAnker(string kennung) =>
        _modellAnker.TryGetValue(kennung, out var anker) ? AnkerLage(anker.Tuer) : null;

    /// <summary>Ort, an dem Karren am Gebäude halten (in Koordinaten der Stadt) – null, wenn es keinen Anker gibt.</summary>
    private Vector3? KarrenAnker(string kennung) =>
        _modellAnker.TryGetValue(kennung, out var anker) ? AnkerLage(anker.Karren) : null;

    private Vector3? AnkerLage(Node3D? anker) =>
        anker is not null && IsInstanceValid(anker) && anker.IsInsideTree() && !anker.IsQueuedForDeletion() ? ToLocal(anker.GlobalPosition) : null;
}
