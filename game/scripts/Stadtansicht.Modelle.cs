using Godot;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die gemeinsame Einsetz-Hilfe für Modelle aus Blender (<c>game/assets/modelle/&lt;unterordner&gt;/*.glb</c>) – zehnter Teil der <c>partial class Stadtansicht</c>.
/// Wohnhäuser der Spielhäuser, Einwohnerhäuser und Werkstätten setzen ihre Modelle alle über <see cref="ModellEinsetzen"/> ein:
/// Laden (jede Datei nur einmal), Zustände nach Namen schalten, Hausfarbe, Ruß, Fahne, Fensterlicht und Ankerpunkte.
/// Konventionen der Modelle: <c>docs/bereiche/darstellung.md</c>.
/// </summary>
public partial class Stadtansicht
{
    // ═════════════════════════════════════════════════════════════════════════
    //  Laden
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die geladenen Modelle. Statisch, damit sie einen Szenenwechsel überleben (die Kartenwerkstatt lädt ihre Szene bei Öffnen und Neu neu): Ginge
    /// der Verweis mit der alten Stadtansicht verloren, könnte der Müllsammler den C#-Mantel eines Modells einsammeln, das Godot noch im Zwischenspeicher
    /// hält – das nächste <c>ResourceLoader.Load</c> in der neuen Szene wirft dann „Handle is not initialized“ (kam nach „Neu“ gelegentlich vor).
    /// </summary>
    private static readonly Dictionary<string, PackedScene?> _modellSzenen = new();

    /// <summary>
    /// Das Modell <c>res://assets/modelle/&lt;datei&gt;.glb</c>, einmal geladen; <paramref name="datei"/> nennt den Unterordner mit
    /// (<c>gebaeude/schmied</c>, wie <c>modell</c> im Gebäudekatalog). Fehlt die Datei, meldet das Spiel einen Fehler
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
    /// Umlandbetriebe haben zusätzlich „Arbeit“ (nur im Betrieb, nie im Leerstand).
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
    /// Stärke bei der Arbeit (<paramref name="GlutAn"/>), dazu die Flächen mit dem Material „Fenster“ (leer im Leerstand), deren Gruppe
    /// <see cref="FenstergruppeSetzen"/> wechselt.
    /// </summary>
    private sealed record ModellInstanz(Node3D Wurzel, StandardMaterial3D[] Glut, float GlutAn, (MeshInstance3D Teil, int Flaeche)[] Fenster)
    {
        /// <summary>Die Gruppe, deren Material die Fenster gerade tragen (<see cref="FenstergruppeSetzen"/> schaltet nur beim Wechsel).</summary>
        public Fenstergruppe Gruppe { get; set; } = Fenstergruppe.Wohnen;
    }

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
        var fenster = new List<(MeshInstance3D, int)>();
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
                "Arbeit" => !zustand.Leerstand, // Umland: Ware, Werkzeug und Pflanzen gibt es nur im Betrieb
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
            if (!zustand.Leerstand) teil.Mesh = mesh = FensterNetz(mesh); // jedes Fenster mit eigener Kennung (Fensterlicht unten)
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
                        teil.SetSurfaceOverrideMaterial(i, FensterStoff(Fenstergruppe.Wohnen, stoff));
                        fenster.Add((teil, i));
                        break;
                    case "Laub" or "Laub hell" or "Laub dunkel":
                        LaubMelden(stoff); // färbt sich mit der Jahreszeit (Stadtansicht.Natur.cs)
                        break;
                    case "Farben":
                        FarbenEinrichten(stoff);
                        break;
                    case "Glut" when stoff is StandardMaterial3D glutVorlage:
                        // jede Werkstatt glüht für sich: eigene Kopie, nicht das Material, das alle teilen
                        var kopie = (StandardMaterial3D)glutVorlage.Duplicate();
                        glutAn = glutVorlage.EmissionEnergyMultiplier * GlutVerstaerkung;
                        kopie.Emission = GlutFarbe(glutVorlage.Emission);
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
        KronenMelden(modell); // Laubkrone im Sommer, kahle Äste im Winter (Stadtansicht.Natur.cs)
        if (!zustand.Leerstand) NachtlichterAnbringen(modell); // Feuerkörbe, Wandlaternen, Ewiges Licht an den Ankern (Stadtansicht.Nachtlichter.cs)
        if (modell.FindChild("Tretrad", true, false) is not null) HafenBelebt(modell); // das Tretrad dreht sich, die Kähne sitzen auf dem Wasser (Stadtansicht.Wasser.Uebergaenge.cs)
        if (ankerKennung is not null)
            _modellAnker[ankerKennung] = (modell.FindChild("Anker_Tuer", true, false) as Node3D, modell.FindChild("Anker_Karren", true, false) as Node3D);
        return new ModellInstanz(modell, glut.ToArray(), glutAn, fenster.ToArray());
    }

    /// <summary>
    /// Das Material „Farben“ (Umlandbetriebe, Felsberg): alle einfachen Farben des Modells stehen als Eckenfarbe im Netz. Godot setzt beim Import nicht
    /// in jeder Datei <c>vertex_color_use_as_albedo</c> (beim Felsberg fehlte es, er war weiß); hier ist es immer an. Die Werte der Datei sind sRGB
    /// (<c>vertex_color_is_srgb</c>): ohne das sind die Modelle in Forward+ viel zu hell (geprüft gegen die Grube vor der Zusammenlegung der Materialien).
    /// </summary>
    private static void FarbenEinrichten(BaseMaterial3D stoff)
    {
        stoff.VertexColorUseAsAlbedo = true;
        stoff.VertexColorIsSrgb = true;
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

    /// <summary>
    /// Wer hinter den Fenstern eines Hauses wach ist. Je Gruppe ein Material mit <c>fenster.gdshader</c>: Es bestimmt, wie viele Fenster
    /// über den Abend leuchten (<see cref="FensterWerte"/>); welche, entscheidet der Shader je Fenster und Nacht. Werkstätten in Betrieb,
    /// Wirtshäuser und Wachen bleiben länger hell, Kirchen haben nur einen schwachen Schein, zur Messe mehr.
    /// </summary>
    private enum Fenstergruppe { Wohnen, Werkstatt, Wirtshaus, Wache, Kirche, Messe }

    /// <summary>Kerzen- und Herdlicht hinter den Butzenscheiben (sRGB), etwa 1.900 K: tiefes, warmes Orange.</summary>
    private static readonly Color FensterLichtFarbe = new(1f, 0.5f, 0.16f);

    /// <summary>Emission eines voll leuchtenden Fensters bei Nacht (der Shader streut sie je Fenster 0,7- bis 1,2-fach und lässt sie flackern).</summary>
    private const float FensterLichtStaerke = 2.6f;

    /// <summary>
    /// So viele Scheiben eines Modells tragen das Material „Fenster“, die übrigen sind dunkles „Glas“ (tools/modelle/bausteine.py, <c>scheibe</c>).
    /// Die Anteile in <see cref="FensterWerte"/> gelten für alle Fenster eines Hauses, darum teilt der Shader-Anteil durch diesen Wert.
    /// </summary>
    private const float FensterMitLicht = 2f / 3f;

    /// <summary>Kirchen und Dom nachts: nur ein schwacher Schein (wenige Kerzen, das Ewige Licht) – so viel der vollen Helligkeit.</summary>
    private const float KirchenSchein = 0.22f;

    /// <summary>Während der Messe (am Vormittag) leuchten die Kirchenfenster auch bei Tag – so hell; in der Nacht wären sie voll hell.</summary>
    private const float MesseAmTag = 0.04f;

    /// <summary>Glut (Esse, Ofen, Feuerkorb): so viel stärker als in der Datei, dazu etwas rötlicher (<see cref="GlutFarbe"/>).</summary>
    private const float GlutVerstaerkung = 1.4f;

    private static Color GlutFarbe(Color datei) => new(datei.R, datei.G * 0.75f, datei.B * 0.6f, datei.A);

    private static Shader? _fensterShader;
    private readonly Dictionary<Fenstergruppe, ShaderMaterial> _fensterStoffe = new();
    private readonly Dictionary<Fenstergruppe, (float Helligkeit, float Anteil, float Nacht)> _fensterGestellt = new();
    private readonly Dictionary<Mesh, Mesh> _fensterNetze = new();
    private readonly Dictionary<string, Material> _modellGlas = new();
    private (Color Farbe, float Rauheit) _fensterGlas = (new Color(0.09f, 0.11f, 0.14f), 0.2f);
    private float _fensterLicht;      // 0 = aus (Tag), 1 = volle Nacht
    private float _fensterStunde = 12; // Uhrzeit für die Anteile
    private float _fensterNacht;      // Nummer der Nacht (wechselt mittags, wenn kein Fenster leuchtet)
    private bool _messeVorgegeben;    // Startoption --messe=1: Kirchen leuchten, als liefe gerade die Messe
    private ModellInstanz? _domFenster; // die Hülle des Doms (Stadtansicht.Sonderbauten.cs)

    /// <summary>Das Material der Fenster dieser Gruppe – je Gruppe eines für die ganze Stadt. Die erste Vorlage gibt Farbe und Rauheit des Glases.</summary>
    private ShaderMaterial FensterStoff(Fenstergruppe gruppe, BaseMaterial3D? vorlage = null)
    {
        if (_fensterStoffe.TryGetValue(gruppe, out var stoff)) return stoff;
        if (vorlage is not null && _fensterStoffe.Count == 0) _fensterGlas = (vorlage.AlbedoColor, vorlage.Roughness);
        _fensterShader ??= GD.Load<Shader>("res://assets/shader/fenster.gdshader");
        stoff = new ShaderMaterial { Shader = _fensterShader, ResourceName = "Fenster" };
        stoff.SetShaderParameter("farbe", _fensterGlas.Farbe);
        stoff.SetShaderParameter("rauheit", _fensterGlas.Rauheit);
        stoff.SetShaderParameter("licht_farbe", FensterLichtFarbe);
        stoff.SetShaderParameter("staerke", FensterLichtStaerke);
        _fensterStoffe[gruppe] = stoff;
        FensterStoffStellen(gruppe, stoff);
        return stoff;
    }

    /// <summary>Gibt den Fenstern des Modells das Material dieser Gruppe – nur beim Wechsel (die Stadtansicht fragt jedes Bild).</summary>
    private void FenstergruppeSetzen(ModellInstanz modell, Fenstergruppe gruppe)
    {
        if (modell.Gruppe == gruppe || modell.Fenster.Length == 0) return;
        modell.Gruppe = gruppe;
        var stoff = FensterStoff(gruppe);
        foreach (var (teil, flaeche) in modell.Fenster) teil.SetSurfaceOverrideMaterial(flaeche, stoff);
    }

    /// <summary>
    /// Die Gruppe eines Betriebs: Kirchen nach der Messe des Kerns, sonst nach der Art – aber nur, solange jemand dort arbeitet
    /// (belegt, ohne Stillstand); ein stilles Haus leuchtet wie ein Wohnhaus. Mit Absicht nicht nach der Arbeitszeit: Die Werkstätten
    /// blenden um 20 Uhr selbst über (<see cref="FensterWerte"/>), statt hart umzuschalten.
    /// </summary>
    private Fenstergruppe FenstergruppeVon(Betrieb b, Stadtwirtschaft w)
    {
        if (b.Art is Betriebsart.Kirche or Betriebsart.Dom or Betriebsart.Kathedrale)
            return _messeVorgegeben || w.Kirchenwesen.MesseLaeuft(b) ? Fenstergruppe.Messe : Fenstergruppe.Kirche;
        if (b.Belegt == 0 || b.Stillstand is not null) return Fenstergruppe.Wohnen;
        return b.Art switch
        {
            Betriebsart.Schaenke or Betriebsart.Wirtshaus or Betriebsart.Gasthaus => Fenstergruppe.Wirtshaus,
            Betriebsart.Wachhaus or Betriebsart.Stadtwache or Betriebsart.Garnison or Betriebsart.Wachturm => Fenstergruppe.Wache,
            _ => Fenstergruppe.Werkstatt,
        };
    }

    /// <summary>Startoption <c>--messe=1</c>: Kirchen und Dom leuchten, als liefe gerade die Messe (für Bildschirmfotos bei Nacht).</summary>
    public void MesseVorgeben() => _messeVorgegeben = true;

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
    /// Stellt das Licht hinter allen Fenstern der Stadt ein (<paramref name="staerke"/> 0 = Tag, 1 = Nacht; dazwischen die Dämmerung) –
    /// jedes Bild aus <see cref="LichtNachUhrzeit"/>. Die Materialien werden nur angefasst, wenn sich ein Wert merklich ändert.
    /// </summary>
    private void FensterlichtSetzen(float staerke, float stunde)
    {
        _fensterLicht = Mathf.Clamp(staerke, 0, 1);
        _fensterStunde = stunde;
        foreach (var (gruppe, stoff) in _fensterStoffe) FensterStoffStellen(gruppe, stoff);
    }

    /// <summary>
    /// Die Nummer der Nacht aus der Spielzeit: Sie wechselt mittags, wenn kein Fenster leuchtet – um 22 Uhr springt die Uhr auf 05 Uhr,
    /// dort leuchten dieselben Fenster weiter.
    /// </summary>
    private void FensterNachtSetzen(long vergangeneMinuten)
    {
        const int mittag = 12 * 60 - Spielzeit.TagesbeginnMinute;
        _fensterNacht = (vergangeneMinuten + Spielzeit.MinutenProTag - mittag) / Spielzeit.MinutenProTag % 100000;
    }

    private void FensterStoffStellen(Fenstergruppe gruppe, ShaderMaterial stoff)
    {
        var messeUm = _simulation?.Wirtschaft?.Kirchenwesen.Daten.MesseUm ?? 10;
        var messe = _messeVorgegeben ? 1 : Mathf.SmoothStep(messeUm, messeUm + 0.1f, _fensterStunde) * (1 - Mathf.SmoothStep(messeUm + 0.9f, messeUm + 1, _fensterStunde));
        var (helligkeit, anteil) = FensterWerte(gruppe, _fensterLicht, _fensterStunde, messe);
        anteil = Mathf.Min(anteil / FensterMitLicht, 1.1f);
        var neu = (Helligkeit: helligkeit, Anteil: anteil, Nacht: _fensterNacht);
        if (_fensterGestellt.TryGetValue(gruppe, out var alt) && alt.Nacht == neu.Nacht
            && Mathf.Abs(alt.Helligkeit - neu.Helligkeit) < 0.004f && Mathf.Abs(alt.Anteil - neu.Anteil) < 0.002f
            && (neu.Helligkeit > 0 || alt.Helligkeit == 0))
            return;
        _fensterGestellt[gruppe] = neu;
        stoff.SetShaderParameter("helligkeit", neu.Helligkeit);
        stoff.SetShaderParameter("anteil", neu.Anteil);
        stoff.SetShaderParameter("nacht", neu.Nacht);
    }

    /// <summary>
    /// Helligkeit und Anteil leuchtender Fenster einer Gruppe zur Uhrzeit (<paramref name="stunde"/> mit Bruchteil). <paramref name="licht"/>
    /// ist das Fensterlicht der Tageszeit (0 Tag, 1 Nacht), <paramref name="messe"/> 0–1 während der Messe. Der Anteil zählt alle Fenster:
    /// <list type="bullet">
    /// <item>Wohnen: ab 18 Uhr gehen nach und nach 55 % an, ab 20 Uhr wieder aus bis auf 18 % um 22 Uhr – und um 05 Uhr (nach dem Sprung
    /// über den Jahreswechsel) ebenso viele; dann stehen die Frühaufsteher auf (35 %), bis das Tageslicht die Fenster löscht.</item>
    /// <item>Werkstatt in Betrieb: alle bis zum Arbeitsende um 20 Uhr, dann in einer halben Stunde wie ein Wohnhaus.</item>
    /// <item>Wirtshaus: alle bis 21:30, um 22 Uhr wie ein Wohnhaus. Wache: die ganze Nacht mindestens 55 %.</item>
    /// <item>Kirche: alle Fenster, aber nur ein schwacher Schein (<see cref="KirchenSchein"/>); Messe: heller, auch bei Tag.</item>
    /// </list>
    /// </summary>
    private static (float Helligkeit, float Anteil) FensterWerte(Fenstergruppe gruppe, float licht, float stunde, float messe)
    {
        var abend = stunde >= 12;
        var dunkel = abend ? Mathf.SmoothStep(17.5f, 18.5f, stunde) : 1f; // die länger hellen Häuser zünden ihr Licht früh an
        var wohnen = abend
            ? Mathf.SmoothStep(18f, 19.5f, stunde) * Mathf.Lerp(0.55f, 0.18f, Mathf.SmoothStep(20f, 22f, stunde))
            : Mathf.Lerp(0.18f, 0.35f, Mathf.SmoothStep(5f, 5.75f, stunde));
        return gruppe switch
        {
            Fenstergruppe.Werkstatt => (licht, abend ? Mathf.Max(wohnen, dunkel * (1 - Mathf.SmoothStep(20f, 20.5f, stunde))) : wohnen),
            Fenstergruppe.Wirtshaus => (licht, abend ? Mathf.Max(wohnen, dunkel * (1 - Mathf.SmoothStep(21.5f, 22f, stunde))) : wohnen),
            Fenstergruppe.Wache => (licht, Mathf.Max(wohnen, 0.55f * dunkel)),
            Fenstergruppe.Kirche => (licht * KirchenSchein, 1f),
            Fenstergruppe.Messe => (Mathf.Lerp(licht * KirchenSchein, MesseAmTag + (1 - MesseAmTag) * licht, messe), 1f),
            _ => (licht, wohnen),
        };
    }

    /// <summary>
    /// Das Netz mit einer Kennung je Fenster: In den Flächen mit dem Material „Fenster“ bekommt jede zusammenhängende Scheibe eine eigene
    /// Eckenfarbe (<see cref="FensterKennungen"/>), die <c>fenster.gdshader</c> liest. Einmal je Netz beim Laden; alle übrigen Flächen
    /// bleiben samt ihren Detailstufen (LOD) unverändert. Netze ohne Fenster bleiben, wie sie sind.
    /// </summary>
    private Mesh FensterNetz(Mesh original)
    {
        if (_fensterNetze.TryGetValue(original, out var fertig)) return fertig;
        var fensterFlaechen = Enumerable.Range(0, original.GetSurfaceCount())
                                        .Where(i => original.SurfaceGetMaterial(i) is { ResourceName: "Fenster" }).ToArray();
        if (original is not ArrayMesh netz || fensterFlaechen.Length == 0 || netz.GetBlendShapeCount() > 0)
            return _fensterNetze[original] = original;
        // Die Flächen so, wie Godot sie speichert (mit Detailstufen); nur die Fensterflächen werden mit Eckenfarbe neu gebaut
        var flaechen = netz.Get("_surfaces").AsGodotArray();
        foreach (var i in fensterFlaechen)
        {
            var felder = netz.SurfaceGetArrays(i);
            var ecken = felder[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var dreiecke = felder[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil ? [] : felder[(int)Mesh.ArrayType.Index].AsInt32Array();
            felder[(int)Mesh.ArrayType.Color] = FensterKennungen(ecken, dreiecke);
            var hilfe = new ArrayMesh();
            hilfe.AddSurfaceFromArrays(netz.SurfaceGetPrimitiveType(i), felder);
            var neu = hilfe.Get("_surfaces").AsGodotArray()[0].AsGodotDictionary();
            var alt = flaechen[i].AsGodotDictionary();
            foreach (var schluessel in new[] { "material", "name" })
                if (alt.ContainsKey(schluessel)) neu[schluessel] = alt[schluessel];
            flaechen[i] = neu;
        }
        var kopie = new ArrayMesh { ResourceName = netz.ResourceName, CustomAabb = netz.CustomAabb, ShadowMesh = netz.ShadowMesh };
        kopie.Set("_surfaces", flaechen);
        _fensterNetze[kopie] = kopie;
        return _fensterNetze[original] = kopie;
    }

    /// <summary>
    /// Eine Eckenfarbe je Scheibe: Ecken desselben Dreiecks und Ecken an derselben Stelle gehören zusammen (Union-Find). Rot und Grün sind
    /// zwei Zufallsbytes aus der Mitte der Scheibe – fest, bei jedem Laden gleich.
    /// </summary>
    private static Color[] FensterKennungen(Vector3[] ecken, int[] dreiecke)
    {
        var wurzel = Enumerable.Range(0, ecken.Length).ToArray();
        int Finden(int i)
        {
            while (wurzel[i] != i) i = wurzel[i] = wurzel[wurzel[i]];
            return i;
        }
        void Vereinen(int a, int b) => wurzel[Finden(a)] = Finden(b);

        if (dreiecke.Length == 0) dreiecke = Enumerable.Range(0, ecken.Length).ToArray();
        for (var i = 0; i + 2 < dreiecke.Length; i += 3)
        {
            Vereinen(dreiecke[i], dreiecke[i + 1]);
            Vereinen(dreiecke[i], dreiecke[i + 2]);
        }
        var stellen = new Dictionary<(int, int, int), int>();
        for (var i = 0; i < ecken.Length; i++)
        {
            var stelle = (Millimeter(ecken[i].X), Millimeter(ecken[i].Y), Millimeter(ecken[i].Z));
            if (stellen.TryGetValue(stelle, out var andere)) Vereinen(i, andere);
            else stellen[stelle] = i;
        }
        var mitten = new Dictionary<int, (Vector3 Summe, int Anzahl)>();
        for (var i = 0; i < ecken.Length; i++)
        {
            var w = Finden(i);
            var (summe, anzahl) = mitten.GetValueOrDefault(w);
            mitten[w] = (summe + ecken[i], anzahl + 1);
        }
        var farben = new Color[ecken.Length];
        for (var i = 0; i < ecken.Length; i++)
        {
            var (summe, anzahl) = mitten[Finden(i)];
            var mitte = summe / anzahl;
            var h = Streuung(Millimeter(mitte.X) / 10, Millimeter(mitte.Y) / 10, Millimeter(mitte.Z) / 10);
            farben[i] = new Color((h & 0xFF) / 255f, ((h >> 8) & 0xFF) / 255f, 0, 1);
        }
        return farben;

        static int Millimeter(float meter) => (int)Mathf.Round(meter * 1000);
        static uint Streuung(int x, int y, int z)
        {
            unchecked
            {
                var h = (uint)x * 73856093u ^ (uint)y * 19349663u ^ (uint)z * 83492791u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                return h ^ (h >> 15);
            }
        }
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
