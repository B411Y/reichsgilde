using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Innenräume aus Blender-Modellen – elfter Teil der <c>partial class Stadtansicht</c> (PR #152).
///
/// Ein Beruf bekommt seinen Innenraum, indem <see cref="InnenModellDatei"/> eine Datei nennt (Schmied: <c>schmied_innen.glb</c>, Tischler:
/// <c>tischler_innen.glb</c>, Steinmetz: <c>steinmetz_innen.glb</c>, Alchimist: <c>alchimist_innen.glb</c>, Parfumeur: <c>parfumeur_innen.glb</c>, Wirt: <c>wirt_innen.glb</c>). Alles Weitere liest dieses Gerüst aus den Knotennamen des Modells (Verträge: <c>docs/bereiche/darstellung.md</c>):
/// <list type="bullet">
/// <item><c>Raum_Werk</c>, <c>Raum_Lager</c>, <c>Raum_Saege</c>, <c>Raum_Huette</c>, <c>Raum_Laden</c>, <c>Raum_Turm</c>, <c>Raum_Pavillon</c>, <c>Raum_Kueche</c>, <c>Raum_Saal</c> – die Räume (glTF-extras „stufe“ auf dem Raumknoten: erst ab dieser Betriebsstufe da,
/// so die Sägehalle des Tischlers, die Bauhütte des Steinmetzen und der Duftpavillon des Parfumeurs ab Stufe 3, siehe <see cref="ModellRaeume"/>); <c>Blick_&lt;Raum&gt;_&lt;0|L1|R1|O1|U1…&gt;</c> – feste Blickpunkte der Kamera (Stadtansicht.Innenkamera.cs)</item>
/// <item><b>Stufenregel:</b> jeder Knoten mit extras „stufe“ &gt; 1 ist erst ab dieser Betriebsstufe sichtbar (<see cref="ModellRaumDaten.StufenKnoten"/>); ausgenommen sind die Anker mit eigener Regel
/// (<c>Arbeit_</c>, <c>Werkstueck_</c>, <c>Licht_</c>, <c>Dampf_</c>, <c>Auslage_</c>, <c>Blick_</c>), die ihre Stufe selbst auswerten</item>
/// <item><c>Stufe1</c>–<c>Stufe3</c> – Ausstattung der Betriebsstufe (genau eine sichtbar); <c>Einrichtung_&lt;Kennung&gt;</c> – gekaufte Einrichtung (erst nach dem Kauf sichtbar)</item>
/// <item><c>Bedienung_&lt;Tafel&gt;_&lt;Name&gt;</c> und <c>Tuer_&lt;von&gt;_&lt;nach&gt;</c> – anklickbare Gegenstände (Klickfläche aus dem Umriss des Netzes; eine Tür mit „stufe“ ist darunter unsichtbar und nicht anklickbar, extras „schild“ ersetzt ihren Namen unter der Maus)</item>
/// <item><c>Arbeit_&lt;n&gt;_&lt;Bewegung&gt;</c> – Arbeitsplätze (extras „requisit“: ersetzt die Werkzeuge der Bewegung, „keins“ = keine); <c>Werkstueck_&lt;n&gt;</c> – Stück in Arbeit; <c>Lager_&lt;n&gt;</c> – Stellplätze der Warenstapel; <c>Licht_&lt;n&gt;</c> – Lichter (extras „art“: Glut, Fenster, Lampe)</item>
/// <item><c>Spaene1</c>–<c>Spaene3</c> – Hobelspäne, höchstens einer sichtbar, nach der Arbeit des Tages; die Animation „Arbeit“ des Modells läuft als Schleife, solange gearbeitet wird</item>
/// <item><c>Werkstueck_&lt;n&gt;</c> mit Netzen aus dem Material „Fluessigkeit“ (Mörser, Kessel, Kolben): die Flüssigkeit selbst ist das Stück in Arbeit und hat die Farbe der Ware;
/// <c>Dampf_&lt;n&gt;</c> – Dampf über dem Gefäß, solange an seinem Arbeitsplatz gearbeitet wird; <c>Auslage_&lt;n&gt;</c> – Regalplätze im Laden, je Fertigware eine Reihe Flaschen und Tiegel nach dem Lager</item>
/// <item><c>Gast_&lt;n&gt;</c>, <c>Gast_Wuerfel_&lt;n&gt;</c>, <c>Gast_Stamm_&lt;n&gt;</c>, <c>Barde_1</c> – Sitzplätze der Gäste und Standort des Barden in der Schänke; wer dort sitzt, bestimmt die Gastwirtschaft des Kerns
/// (Stadtansicht.Innengaeste.cs); <c>Krug_&lt;n&gt;</c> – wo der Krug auf dem Tisch steht (den hält die Figur selbst, der Anker bleibt leer)</item>
/// </list>
/// Der Raum folgt dem Kern: Stufe, Einrichtung, Glut, Arbeiter, Werkstücke und Warenstapel werden nur angefasst, wenn sich etwas geändert hat.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Welche Datei den Innenraum eines Werkstils stellt (null: noch der alte Raum aus Grundkörpern). Pro neuem Beruf eine Zeile mehr.</summary>
    private static string? InnenModellDatei(Werkstil stil) => stil switch
    {
        Werkstil.Giesserei => "innen/schmied_innen",
        Werkstil.Tischlerei => "innen/tischler_innen",
        Werkstil.Maurerei => "innen/steinmetz_innen",
        Werkstil.Tinkturei => "innen/alchimist_innen",
        Werkstil.Siederei => "innen/parfumeur_innen",
        Werkstil.Schaenke => "innen/wirt_innen",
        _ => null,
    };

    /// <summary>
    /// Die Räume, die ein Innenmodell haben kann, in der Reihenfolge der Raumleiste: Kennung, Schlüssel im Knotennamen (<c>Raum_&lt;Schlüssel&gt;</c>, <c>Blick_&lt;Schlüssel&gt;_…</c>,
    /// <c>Tuer_&lt;von&gt;_&lt;nach&gt;</c>), Name in der Leiste und ab welcher Betriebsstufe es den Raum gibt, wenn die extras „stufe“ am Raumknoten fehlen.
    /// Hat das Modell einen Raum nicht (keine Blickpunkte), fehlt er einfach. Ein weiterer Raum ist eine Zeile hier, eine Kennung in <c>Stadtansicht.Raeume.cs</c> und ein Satz in <see cref="InnenText"/>.
    /// </summary>
    private static (string Kennung, string Schluessel, string Name, int Vorgabe)[] ModellRaeume(Werkstil stil) => new[]
    {
        (RaumWerk, "Werk", WerkraumName(stil), 1),
        (RaumKueche, "Kueche", "Küche", 1),
        (RaumLager, "Lager", stil == Werkstil.Schaenke ? "Fasskeller" : "Lager", 1),
        (RaumSaege, "Saege", "Sägehalle", 3),
        (RaumHuette, "Huette", "Bauhütte", 3),
        (RaumLaden, "Laden", "Laden", 2),
        (RaumTurm, "Turm", "Laborturm", 3),
        (RaumPavillon, "Pavillon", "Duftpavillon", 3),
        (RaumSaal, "Saal", "Saal", 3),
    };

    /// <summary>
    /// Sekunden einer Schleife, in der die Figur eines Arbeitsplatzes mit den bewegten Teilen des Raums (Animation „Arbeit“) läuft – nur dort, wo ein Teil der Maschine
    /// der Hand oder dem Fuß der Figur folgt (Werkstil, Bewegung des Platzes). Alles andere (Hobeln, die Handsäge am Sägebock …) läuft mit eigener Startphase und Laune-Tempo.
    /// Der Wert ist die Dauer der Bewegung in Blender (<c>BEWEGUNGEN</c> in <c>figuren.py</c>), nach der das Modell seine Teile gebaut hat.
    /// </summary>
    private static readonly Dictionary<(Werkstil, string), double> RaumTakt = new()
    {
        [(Werkstil.Tischlerei, "Drechseln")] = 1.0,  // Fuß am Pedal der Wippdrehbank
        [(Werkstil.Tischlerei, "Gatter")] = 2.0,     // Stamm nachschieben, der Rahmen der Gattersäge geht mit
        [(Werkstil.Maurerei, "Saegen")] = 1.0,       // die rechte Hand führt den Rahmen der Steinsäge
        [(Werkstil.Maurerei, "Schleifen")] = 1.2,    // der Fuß tritt den Tritt, die Kurbel dreht den Schleifstein
        [(Werkstil.Siederei, "Pressen")] = 3.0,      // die Hände liegen am Querhebel, die Spindel der Presse dreht genau nach der Bewegung
        [(Werkstil.Schaenke, "Zapfen")] = 4.0,       // Hahn und Strahl in der Animation „Arbeit“ folgen der Linken des Zapfers
    };

    /// <summary>Mit so vielen Kisten steht ein volles Lager einer Ware (Höhe der Warenstapel).</summary>
    private const int StapelHoehe = 5;

    /// <summary>Anklickbarer Gegenstand: seine Klickfläche ist nur aktiv, solange <see cref="Knoten"/> im Bild steht (selbst sichtbar, ebenso Stufe, Raum und Elternknoten).</summary>
    private sealed class ModellKlick
    {
        public required CollisionShape3D Form { get; init; }
        public required Node3D Knoten { get; init; }
    }

    /// <summary>Ein Arbeitsplatz des Modells (<c>Arbeit_&lt;n&gt;_&lt;Bewegung&gt;</c>) und sein Werkstück.</summary>
    private sealed class ModellPlatz
    {
        public required int Nr { get; init; }
        public required string Bewegung { get; init; }
        public required Node3D Anker { get; init; }
        public int AbStufe { get; init; } = 1;
        /// <summary>Schlüssel des Raums, in dem der Platz liegt („Werk“, „Saege“ …; leer: unter keinem Raumknoten).</summary>
        public string Raum { get; init; } = "";
        /// <summary>Werkzeuge der Figur an diesem Platz statt derer der Bewegung (extras „requisit“; leer: keine), null: die der Bewegung.</summary>
        public string[]? Werkzeug { get; init; }
        /// <summary>&gt; 0: Die Figur läuft im Takt des Raums, so viele Sekunden dauert ihre Schleife (<see cref="RaumTakt"/>); 0: eigene Phase.</summary>
        public double Takt { get; init; }
        public Node3D? WerkstueckAnker { get; set; }
        /// <summary>Das Stück in Arbeit: ein grauer Quader als Kind des Ankers, oder – hat <c>Werkstueck_&lt;n&gt;</c> Netze mit dem Material „Fluessigkeit“ – der Anker selbst (samt Blasen, Tropfen).</summary>
        public Node3D? Werkstueck { get; set; }
        /// <summary>Das Material des Stücks (Quader oder Flüssigkeit; eigene Kopie je Platz), das die Farbe der Ware trägt.</summary>
        public StandardMaterial3D? WerkstueckStoff { get; set; }
        public string WerkstueckWare { get; set; } = "";
    }

    /// <summary>Dampf über einem Gefäß (Knoten <c>Dampf_&lt;n&gt;</c>): steigt auf, solange am zugehörigen Arbeitsplatz gearbeitet wird.</summary>
    private sealed class DampfQuelle
    {
        public required CpuParticles3D Teilchen { get; init; }
        public required int AbStufe { get; init; }
        /// <summary>Der Arbeitsplatz, dessen Werkstück neben der Quelle liegt (der Dampf steht über dem Kessel, dem Kolben, dem Athanor); null: Dampf bei jeder Arbeit des Hauses.</summary>
        public ModellPlatz? Platz { get; init; }
    }

    /// <summary>Ein Platz im Regal des Ladens (<c>Auslage_&lt;n&gt;</c>): eine Reihe Tiegel und Flaschen einer Fertigware.</summary>
    private sealed class AuslagePlatz
    {
        public required Node3D Anker { get; init; }
        public required Node3D[] Gefaesse { get; init; }
        public required StandardMaterial3D Stoff { get; init; }
        public required CollisionShape3D Klick { get; init; }
        public required Label3D Schild { get; init; }
        public string Ware { get; set; } = "";
        public string Stand { get; set; } = "";
    }

    private sealed class ModellArbeiter
    {
        public required int Nummer { get; init; }
        public required Figur Figur { get; init; }
        public required Node3D Halter { get; init; }
        public required ModellPlatz Platz { get; init; }
        public required CollisionShape3D Klick { get; init; }
        /// <summary>Goldener Ring auf dem Boden: zeigt, wer ausgewählt ist (die helle Schicht der Figur allein fällt im dunklen Raum kaum auf).</summary>
        public required MeshInstance3D Ring { get; init; }
        public string Bewegung { get; set; } = "";
    }

    private sealed class LagerStapel
    {
        public required Node3D Wurzel { get; init; }
        public required MeshInstance3D[] Kisten { get; init; }
        public required StandardMaterial3D Stoff { get; init; }
        public required Label3D Schild { get; init; }
        public required CollisionShape3D Klick { get; init; }
        public required StaticBody3D Koerper { get; init; }
        public string Ware { get; set; } = "";
        public string Stand { get; set; } = "";
    }

    /// <summary>Wie ein Licht des Modells leuchtet (extras „art“): Glut flackert orange bei der Arbeit, Fenster werfen Tageslicht, Lampen leuchten warm und gleichmäßig, solange gearbeitet wird.</summary>
    private enum LichtArt { Fenster, Glut, Lampe }

    /// <summary>Was die Animation „Arbeit“ des Raums gerade tut: in Ruhelage (Zeit 0, nichts wird gearbeitet), Schleife oder bei Spielpause an der Stelle angehalten.</summary>
    private enum AnimationStand { Ruht, Laeuft, Haelt }

    /// <summary>Alles, was ein Modellraum an Knoten und Zustand braucht (je Betrieb).</summary>
    private sealed class ModellRaumDaten
    {
        public required Node3D Wurzel { get; init; }
        public List<StandardMaterial3D> Glut { get; } = new();
        public float GlutAn { get; set; } = 2.5f;
        public List<StandardMaterial3D> Hausfarben { get; } = new();
        public Node3D?[] Stufen { get; } = new Node3D?[4];
        public Dictionary<string, Node3D> Einrichtung { get; } = new();
        public Node3D? Holztuer { get; set; }
        public Dictionary<string, List<Blickpunkt>> Blicke { get; } = new();
        public List<ModellPlatz> Plaetze { get; } = new();
        public List<Node3D> Lagerplaetze { get; } = new();
        /// <summary>Die Anker der Regalplätze im Laden (<c>Auslage_&lt;n&gt;</c>, nach Nummer) und was darauf steht (je Platz eine Fertigware, gebaut beim ersten Stück im Lager).</summary>
        public List<Node3D> Auslageplaetze { get; } = new();
        public List<AuslagePlatz?> Auslagen { get; } = new();
        /// <summary>Dampf über Kessel, Kolben und Athanor; sein Material (ein weicher Fleck, im Raum nach der Tageshelligkeit getönt).</summary>
        public List<DampfQuelle> Dampf { get; } = new();
        public List<Node3D> DampfAnker { get; } = new();
        /// <summary>Die Sitzplätze der Gäste (<c>Gast_&lt;n&gt;</c>, <c>Gast_Wuerfel_&lt;n&gt;</c>, <c>Gast_Stamm_&lt;n&gt;</c>) und der Platz des Barden (<c>Barde_1</c>), nach Tisch und Nummer geordnet; siehe Stadtansicht.Innengaeste.cs.</summary>
        public List<GastPlatz> Gaeste { get; } = new();
        public GastenStand GaesteStand { get; set; } = new(-1, 0, 0, false);
        public double GaesteZeit { get; set; }
        public StandardMaterial3D? DampfStoff { get; set; }
        /// <summary>Die Bedienungsstücke des Raums (Tafel, Name) in der Reihenfolge der Tafeln – für den Hinweis beim Betreten.</summary>
        public List<(string Tafel, string Name, Node3D Knoten)> Bedienungen { get; } = new();
        public List<(Light3D Licht, LichtArt Art, int AbStufe)> Lichter { get; } = new();
        /// <summary>Knoten, die erst ab einer Betriebsstufe da sind (Räume ab der zweiten oder dritten Stufe, Türen dorthin, Schlot2, Fischgrät …; extras „stufe“): darunter unsichtbar.</summary>
        public List<(Node3D Knoten, int AbStufe)> StufenKnoten { get; } = new();
        /// <summary>Hobelspäne in drei Mengen (Index 1–3); höchstens einer sichtbar.</summary>
        public Node3D?[] Spaene { get; } = new Node3D?[4];
        /// <summary>Die Animation „Arbeit“ des Modells (Gatter, Drechselbank …), sonst null.</summary>
        public AnimationPlayer? Animation { get; set; }
        public List<ModellKlick> Klicks { get; } = new();
        public Dictionary<int, ModellArbeiter> Arbeiter { get; } = new();
        public List<LagerStapel> Stapel { get; } = new();

        // gezeigter Zustand – so wird nur bei Änderung etwas angefasst
        public int Stufe { get; set; }
        public string EinrichtungStand { get; set; } = "";
        public string ArbeiterStand { get; set; } = "";
        public bool Glueht { get; set; }
        public Color Hausfarbe { get; set; }
        public float Hell { get; set; } = -1;
        public string WerkstueckStand { get; set; } = "";
        public int SpaeneStand { get; set; } = -1;
        public AnimationStand ArbeitStand { get; set; }
        /// <summary>Länge einer Schleife der Animation „Arbeit“ (Sekunden) und Stand in der Schleife: Figuren mit Bewegungen im Takt des Raums (Drechseln, Gatter) übernehmen die Phase.</summary>
        public double ArbeitDauer { get; set; } = 1;
        public double ArbeitPhase { get; set; }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Stellt das Modell in den Knoten „Innen" des Betriebs, liest Räume, Blickpunkte, Bedienung, Arbeitsplätze und Lichter aus den Knotennamen
    /// und legt die Räume an. Der Betrieb darf nur aufrufen, wenn <see cref="Modell"/> die Datei kennt.
    /// </summary>
    private void InnenModellBauen(BetriebsAnsicht a, Werkstil stil, string datei)
    {
        var innen = new Node3D { Name = "Innen", Visible = false, Position = new Vector3(0, RaumTiefe, 0) };
        AddChild(innen);
        a.Innen = innen;
        a.Stil = stil;
        var wurzel = Modell(datei)!.Instantiate<Node3D>();
        wurzel.Position = a.Mitte;
        innen.AddChild(wurzel);
        var md = new ModellRaumDaten { Wurzel = wurzel };
        a.Modellraum = md;

        InnenModellStoffe(md, wurzel);
        foreach (var knoten in wurzel.FindChildren("*", nameof(Node3D), true, false).OfType<Node3D>())
            InnenModellKnoten(a, md, knoten);
        if (wurzel.FindChild("AnimationPlayer", true, false) is AnimationPlayer spieler && spieler.HasAnimation("Arbeit"))
        {
            // Wie bei den Figuren rückt das Spiel die Pose selbst weiter (Seek in InnenAnimationSetzen): So laufen Raum und Figuren (Drechsler am Pedal,
            // Gatterarbeiter) im selben Takt und halten gemeinsam an. Die Schleife läuft ab dem zweiten Bild (SchleifenAnfang), wie bei den Figuren.
            spieler.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
            spieler.GetAnimation("Arbeit").LoopMode = Animation.LoopModeEnum.Linear;
            spieler.Play("Arbeit"); // wählt die Animation aus, weiter geht es nur mit Seek
            md.Animation = spieler;
            md.ArbeitDauer = Math.Max(0.1, spieler.GetAnimation("Arbeit").Length - SchleifenAnfang);
        }

        foreach (var plan in md.Plaetze) // Werkstück-Anker zum Platz gleicher Nummer
        {
            if (wurzel.FindChild($"Werkstueck_{plan.Nr}", true, false) is not Node3D anker) continue;
            plan.WerkstueckAnker = anker;
            if (FluessigkeitKopieren(anker) is { } fluessigkeit)
            {
                // Mörser, Kessel, Kolben, Athanor: Das Stück ist die Flüssigkeit im Gefäß (samt Blasen und Tropfen) und hat die Farbe der Ware
                plan.Werkstueck = anker;
                plan.WerkstueckStoff = fluessigkeit;
                anker.Visible = false;
                continue;
            }
            plan.WerkstueckStoff = Material(Colors.Gray);
            plan.Werkstueck = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.42f, 0.08f, 0.22f) },
                MaterialOverride = plan.WerkstueckStoff,
                Visible = false,
            };
            anker.AddChild(plan.Werkstueck);
        }
        md.Plaetze.Sort((x, y) => x.Nr.CompareTo(y.Nr));
        md.Gaeste.Sort((x, y) => x.Tisch != y.Tisch ? x.Tisch.CompareTo(y.Tisch) : x.Nr.CompareTo(y.Nr));
        DampfAnlegen(wurzel, md);
        md.Auslageplaetze.Sort((x, y) => NummerImNamen(x).CompareTo(NummerImNamen(y)));
        RaumEbeneUebernehmen(wurzel); // alles, was bis hier im Modell steht, auch Lichter, Werkstücke und Dampf

        // Räume in der Reihenfolge der Raumleiste (Mitte, Maße und Wände stehen im Modell). Ein Raum mit „stufe“ (Sägehalle, Bauhütte, Laden, Laborturm, Duftpavillon) steht erst ab dieser Stufe in der Leiste.
        foreach (var (kennung, schluessel, name, vorgabe) in ModellRaeume(stil)) // die Vorgabe gilt, wenn die „extras“ fehlen
            if (md.Blicke.TryGetValue(schluessel, out var blicke) && blicke.Count > 0)
            {
                blicke.Sort((x, y) => x.Rang.CompareTo(y.Rang));
                var raumKnoten = wurzel.FindChild("Raum_" + schluessel, true, false) as Node3D;
                var abStufe = raumKnoten is null ? vorgabe : ExtraZahl(raumKnoten, "stufe", vorgabe);
                if (raumKnoten is not null && abStufe > 1 && md.StufenKnoten.All(x => x.Knoten != raumKnoten)) md.StufenKnoten.Add((raumKnoten, abStufe)); // ohne extras: die Vorgabe der Tabelle
                a.Raeume.Add(new Raum(kennung, name, Vector3.Zero, Vector3.Zero, // Mitte und Maße braucht nur der alte Raum aus Grundkörpern: Wände und Lage stehen im Modell, die Kamera folgt den Blickpunkten
                    abStufe > 1 ? () => Modellstufe(a.Betrieb.Stufe) >= abStufe : null, blicke));
            }
        KontorUndWohnstubeAnlegen(a, WandVon(stil));
    }

    /// <summary>Materialien der Modellfläche: „Glut" und „Hausfarbe" als eigene Kopien, „Fenster" wie in den alten Räumen vom Himmel beleuchtet.</summary>
    private void InnenModellStoffe(ModellRaumDaten md, Node3D wurzel)
    {
        foreach (var teil in wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (teil.Mesh is not { } netz) continue;
            for (var i = 0; i < netz.GetSurfaceCount(); i++)
            {
                if (netz.SurfaceGetMaterial(i) is not BaseMaterial3D stoff) continue;
                switch (stoff.ResourceName)
                {
                    case "Farben":
                        FarbenEinrichten(stoff);
                        break;
                    case "Fenster":
                        teil.SetSurfaceOverrideMaterial(i, _fensterglas);
                        break;
                    case "Glut" when stoff is StandardMaterial3D vorlage:
                        var kopie = (StandardMaterial3D)vorlage.Duplicate();
                        md.GlutAn = vorlage.EmissionEnergyMultiplier;
                        kopie.EmissionEnergyMultiplier = 0; // aus, bis gearbeitet wird
                        teil.SetSurfaceOverrideMaterial(i, kopie);
                        md.Glut.Add(kopie);
                        break;
                    case "Hausfarbe" when stoff is StandardMaterial3D haus:
                        var farbe = (StandardMaterial3D)haus.Duplicate();
                        teil.SetSurfaceOverrideMaterial(i, farbe);
                        md.Hausfarben.Add(farbe);
                        break;
                }
            }
        }
    }

    /// <summary>Sortiert einen Knoten des Modells nach seinem Namen ein.</summary>
    private void InnenModellKnoten(BetriebsAnsicht a, ModellRaumDaten md, Node3D knoten)
    {
        var name = knoten.Name.ToString();
        var teile = name.Split('_');
        // Allgemeine Stufenregel: Ab dieser Betriebsstufe ist der Knoten da, darunter unsichtbar (Raum, Tür, Schlot2, Fischgrät, Späne …).
        // Die Anker mit eigener Regel werten ihre Stufe selbst aus; ohne sie schaltete sich Arbeitsplatz, Werkstück und Licht doppelt.
        if (ExtraZahl(knoten, "stufe", 1) is > 1 and var abStufe && teile[0] is not ("Arbeit" or "Werkstueck" or "Licht" or "Dampf" or "Auslage" or "Blick"))
            md.StufenKnoten.Add((knoten, abStufe));
        switch (teile[0])
        {
            case "Stufe1" or "Stufe2" or "Stufe3":
                md.Stufen[name[^1] - '0'] = knoten;
                break;
            case "Holztuer":
                md.Holztuer = knoten;
                break;
            case "Einrichtung" when teile.Length >= 2:
                md.Einrichtung[teile[1]] = knoten;
                knoten.Visible = false; // bis zum Kauf (steht im Modell auch als Zustand „sichtbar: false")
                InnenKlickAnlegen(a, md, knoten, "einrichtung:" + teile[1]);
                break;
            case "Bedienung" when teile.Length >= 3:
                md.Bedienungen.Add((teile[1], teile[2], knoten));
                InnenKlickAnlegen(a, md, knoten, $"bedienung:{teile[1]}:{teile[2]}");
                break;
            case "Tuer" when teile.Length >= 3:
                // extras „schild“: eigener Text fürs Schild, z. B. „Treppe zum Lager“, wenn die Tür eine Treppe oder Luke ist
                InnenKlickAnlegen(a, md, knoten, $"tuer:{teile[1]}:{teile[2]}" + (ExtraText(knoten, "schild") is { Length: > 0 } schild ? ":" + schild : ""));
                break;
            case "Spaene1" or "Spaene2" or "Spaene3" when teile.Length == 1:
                md.Spaene[name[^1] - '0'] = knoten;
                knoten.Visible = false; // bis zur Arbeit des Tages
                break;
            // (Teil einer Menge Späne, Spaene<n>_Drechselbank, liegt erst ab seiner Stufe da: Stufenregel oben)
            case "Blick" when teile.Length >= 3:
                if (!md.Blicke.TryGetValue(teile[1], out var liste)) md.Blicke[teile[1]] = liste = new List<Blickpunkt>();
                liste.Add(new Blickpunkt(teile[2], knoten));
                break;
            case "Arbeit" when teile.Length >= 3 && int.TryParse(teile[1], out var nr):
                md.Plaetze.Add(new ModellPlatz { Nr = nr, Bewegung = teile[2], Anker = knoten, Raum = RaumSchluesselVon(knoten),
                    AbStufe = ExtraZahl(knoten, "stufe", nr <= 2 ? 1 : nr - 1), Werkzeug = PlatzWerkzeug(knoten), Takt = RaumTakt.GetValueOrDefault((a.Stil, teile[2])) });
                break;
            case "Lager" when teile.Length == 2 && int.TryParse(teile[1], out _):
                md.Lagerplaetze.Add(knoten);
                break;
            case "Auslage" when teile.Length == 2 && int.TryParse(teile[1], out _):
                md.Auslageplaetze.Add(knoten);
                break;
            case "Dampf" when teile.Length == 2:
                md.DampfAnker.Add(knoten);
                break;
            case "Gast" or "Barde" when GastPlatzVon(knoten) is { } gastplatz:
                md.Gaeste.Add(gastplatz);
                break;
            case "Licht" when teile.Length == 2:
                InnenLichtAnlegen(md, knoten);
                break;
        }
    }

    /// <summary>Die Nummer am Ende eines Knotennamens wie <c>Auslage_3</c> (sonst 0).</summary>
    private static int NummerImNamen(Node knoten) => int.TryParse(knoten.Name.ToString().Split('_')[^1], out var nr) ? nr : 0;

    /// <summary>
    /// Hat der Knoten – er selbst oder darunter (Blasen, Tropfen) – Netze mit dem Material „Fluessigkeit“, bekommen alle zusammen eine eigene Kopie dieses Materials
    /// (je Werkstück eine, damit jede Flüssigkeit ihre Farbe hat); die Kopie wird zurückgegeben. Sonst null: Dann ist das Werkstück ein Quader (Schmied, Tischler, Steinmetz).
    /// </summary>
    private static StandardMaterial3D? FluessigkeitKopieren(Node3D anker)
    {
        StandardMaterial3D? kopie = null;
        var teile = anker.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().ToList();
        if (anker is MeshInstance3D selbst) teile.Add(selbst);
        foreach (var teil in teile)
            for (var i = 0; teil.Mesh is { } netz && i < netz.GetSurfaceCount(); i++)
                if (netz.SurfaceGetMaterial(i) is StandardMaterial3D { ResourceName: "Fluessigkeit" } vorlage)
                {
                    kopie ??= (StandardMaterial3D)vorlage.Duplicate();
                    teil.SetSurfaceOverrideMaterial(i, kopie);
                }
        return kopie;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Dampf
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Wie weit (Meter, waagerecht) ein Werkstück von einem <c>Dampf_&lt;n&gt;</c> höchstens liegen darf, damit der Dampf zu seinem Arbeitsplatz gehört.</summary>
    private const float DampfReichweite = 0.6f;

    private static GradientTexture2D? _dampfFleck;

    /// <summary>Ein weicher heller Fleck (radialer Verlauf, außen durchsichtig) als Gestalt eines Dampfballens; alle Dampfquellen teilen ihn.</summary>
    private static GradientTexture2D DampfFleck() => _dampfFleck ??= new GradientTexture2D
    {
        Width = 64, Height = 64,
        Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
        Gradient = new Gradient
        {
            Offsets = new[] { 0f, 0.45f, 1f },
            Colors = new[] { new Color(1, 1, 1, 1), new Color(1, 1, 1, 0.45f), new Color(1, 1, 1, 0) },
        },
    };

    /// <summary>
    /// Legt an jedes <c>Dampf_&lt;n&gt;</c> (leerer Knoten, der Dampf steigt nach +Y) eine Quelle: wenige weiche Flecken, die sanft aufsteigen, wachsen und vergehen.
    /// Der Fleck ist unbeschattet und durchsichtig; die Oberflächendetails lassen ihn darum, wie er ist.
    /// Der Fleck ist unbeschattet und durchsichtig; der Holzschnitt lässt ihn darum, wie er ist (hell und schlicht).
    /// </summary>
    private static void DampfAnlegen(Node3D wurzel, ModellRaumDaten md)
    {
        if (md.DampfAnker.Count == 0) return;
        md.DampfStoff = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            VertexColorUseAsAlbedo = true,
            AlbedoTexture = DampfFleck(),
            AlbedoColor = new Color(0.8f, 0.8f, 0.78f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var kurve = new Curve { MaxValue = 2f };
        kurve.AddPoint(new Vector2(0f, 0.5f));
        kurve.AddPoint(new Vector2(1f, 1.7f));
        var verlauf = new Gradient // durchsichtig → dünner Dampf → verweht
        {
            Offsets = new[] { 0f, 0.25f, 1f },
            Colors = new[] { new Color(1, 1, 1, 0), new Color(1, 1, 1, 0.28f), new Color(1, 1, 1, 0) },
        };
        foreach (var anker in md.DampfAnker)
        {
            var teilchen = new CpuParticles3D
            {
                Emitting = false,
                Amount = 9,
                Lifetime = 3.2,
                Randomness = 0.6f,
                LocalCoords = false,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Mesh = new QuadMesh { Size = new Vector2(0.45f, 0.45f), Material = md.DampfStoff },
                EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
                EmissionSphereRadius = 0.07f,
                Direction = Vector3.Up,
                Spread = 14f,
                Gravity = Vector3.Zero,
                InitialVelocityMin = 0.16f,
                InitialVelocityMax = 0.30f,
                AngleMin = 0f,
                AngleMax = 360f,
                ScaleAmountMin = 0.8f,
                ScaleAmountMax = 1.2f,
                ScaleAmountCurve = kurve,
                ColorRamp = verlauf,
            };
            anker.AddChild(teilchen);
            var lage = LageImModell(wurzel, anker).Origin;
            float Abstand(ModellPlatz p) // waagerecht
            {
                var stueck = LageImModell(wurzel, p.WerkstueckAnker!).Origin;
                return new Vector2(stueck.X - lage.X, stueck.Z - lage.Z).Length();
            }
            var platz = md.Plaetze.Where(p => p.WerkstueckAnker is not null).OrderBy(Abstand).FirstOrDefault(p => Abstand(p) < DampfReichweite);
            md.Dampf.Add(new DampfQuelle { Teilchen = teilchen, AbStufe = ExtraZahl(anker, "stufe", 1), Platz = platz });
        }
    }

    /// <summary>Lage eines Knotens im Koordinatensystem der Modellwurzel (ohne dass das Modell schon im Baum stehen muss).</summary>
    private static Transform3D LageImModell(Node3D wurzel, Node3D knoten)
    {
        var lage = Transform3D.Identity;
        for (Node3D? k = knoten; k is not null && k != wurzel; k = k.GetParent() as Node3D) lage = k.Transform * lage;
        return lage;
    }

    private void InnenLichtAnlegen(ModellRaumDaten md, Node3D anker)
    {
        var art = ExtraText(anker, "art") switch { "Glut" => LichtArt.Glut, "Lampe" => LichtArt.Lampe, _ => LichtArt.Fenster };
        Light3D licht = art switch
        {
            LichtArt.Glut => new OmniLight3D { LightColor = new Color(1f, 0.55f, 0.22f), OmniRange = 5f, LightEnergy = 0.3f },
            LichtArt.Lampe => new OmniLight3D { LightColor = new Color(1f, 0.82f, 0.55f), OmniRange = 6f, LightEnergy = 0.5f },
            _ => new SpotLight3D { LightColor = new Color(0.86f, 0.90f, 1f), SpotRange = 4.5f, SpotAngle = 50f, LightEnergy = 0f },
        };
        anker.AddChild(licht);
        md.Lichter.Add((licht, art, ExtraZahl(anker, "stufe", 1)));
    }

    /// <summary>Eine Klickfläche aus dem Umriss des Knotens (sein Netz oder die Netze darunter), mit dem Tag „art:…" für Maus und Klick.</summary>
    private void InnenKlickAnlegen(BetriebsAnsicht a, ModellRaumDaten md, Node3D knoten, string tag)
    {
        var umriss = LokalerUmriss(knoten);
        if (umriss is not { } box) return;
        var koerper = new StaticBody3D();
        koerper.SetMeta("innen", tag);
        var form = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(Mathf.Max(box.Size.X, 0.12f), Mathf.Max(box.Size.Y, 0.12f), Mathf.Max(box.Size.Z, 0.12f)) },
            Position = box.GetCenter(),
            Disabled = true,
        };
        koerper.AddChild(form);
        knoten.AddChild(koerper);
        a.Raumklicks.Add(form); // nur aktiv, solange man im Haus ist
        md.Klicks.Add(new ModellKlick { Form = form, Knoten = knoten });
    }

    /// <summary>
    /// Die Werkzeuge der Figur an einem Arbeitsplatz nach seinen extras „requisit“: ein oder mehrere Teilnamen, durch Komma getrennt (<c>Schlaegel,Meissel</c>),
    /// oder „keins“ (der Platz hat sein Werkzeug selbst, etwa die Steinsäge: die Figur trägt keines). Fehlt „requisit“: null, es gelten die Werkzeuge der Bewegung.
    /// </summary>
    private static string[]? PlatzWerkzeug(Node knoten) => ExtraText(knoten, "requisit") switch
    {
        "" => null,
        "keins" => Array.Empty<string>(),
        var liste => liste.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
    };

    /// <summary>Raumschlüssel in der Reihenfolge ihrer Render-Ebene (11 bis 19): Jeder Raum eines Modells hat seine eigene.</summary>
    private static readonly string[] RaumEbenen = { "Werk", "Lager", "Saege", "Huette", "Laden", "Turm", "Pavillon", "Kueche", "Saal" };

    /// <summary>
    /// Legt alles unter <paramref name="knoten"/> auf die Render-Ebene seines Raums und lässt jedes Licht nur diese Ebene beleuchten. Die Räume liegen
    /// im Modell aneinander oder übereinander (das Lager über dem Werkraum), und die Raumlichter haben keine Schatten: ohne eigene Ebene schienen
    /// Esse und Laterne durch die Decke ins Lager und die Fenster des Lagers in den Werkraum. Die Raumkamera sieht alle Ebenen.
    /// </summary>
    private static void RaumEbeneUebernehmen(Node knoten)
    {
        if (knoten.Name.ToString() is not { } name || !name.StartsWith("Raum_"))
        {
            var schluessel = RaumSchluesselVon(knoten);
            if (schluessel.Length > 0) { EbeneSetzen(knoten, RaumEbene(schluessel)); return; }
        }
        foreach (var kind in knoten.GetChildren()) // die Wurzel des Modells: jeden Raum für sich
            if (kind.Name.ToString().StartsWith("Raum_")) EbeneSetzen(kind, RaumEbene(kind.Name.ToString()[5..]));
            else RaumEbeneUebernehmen(kind);
    }

    private static uint RaumEbene(string schluessel) => 1u << (10 + Math.Max(0, Array.IndexOf(RaumEbenen, schluessel)));

    private static void EbeneSetzen(Node knoten, uint ebene)
    {
        if (knoten is Light3D licht) licht.LightCullMask = ebene;
        else if (knoten is VisualInstance3D sichtbar) sichtbar.Layers = ebene;
        foreach (var kind in knoten.GetChildren()) EbeneSetzen(kind, ebene);
    }

    /// <summary>Schlüssel des Raums, in dem der Knoten liegt: der Name des nächsten Vorfahren „Raum_&lt;Schlüssel&gt;“ („Werk“, „Lager“, „Saege“, „Huette“, „Laden“, „Turm“, „Pavillon“), sonst leer.</summary>
    private static string RaumSchluesselVon(Node knoten)
    {
        for (var k = knoten; k is not null; k = k.GetParent())
            if (k.Name.ToString() is var name && name.StartsWith("Raum_")) return name[5..];
        return "";
    }

    /// <summary>Der Umriss eines Knotens in seinen eigenen Koordinaten: sein Netz, sonst die Netze seiner Kinder.</summary>
    private static Aabb? LokalerUmriss(Node3D knoten)
    {
        Aabb? summe = null;
        void Sammeln(Node3D k, Transform3D lage)
        {
            if (k is MeshInstance3D { Mesh: not null } netz)
            {
                var box = lage * netz.GetAabb();
                summe = summe is { } s ? s.Merge(box) : box;
            }
            foreach (var kind in k.GetChildren().OfType<Node3D>())
                if (kind is MeshInstance3D || kind.GetChildCount() > 0) Sammeln(kind, lage * kind.Transform);
        }
        Sammeln(knoten, Transform3D.Identity);
        return summe;
    }

    /// <summary>Eine Zahl aus den „extras" eines Knotens (glTF-Zusatzdaten), sonst der Vorgabewert.</summary>
    private static int ExtraZahl(Node knoten, string schluessel, int vorgabe)
    {
        var extras = Extras(knoten);
        return extras is not null && extras.ContainsKey(schluessel) ? extras[schluessel].AsInt32() : vorgabe;
    }

    private static string ExtraText(Node knoten, string schluessel)
    {
        var extras = Extras(knoten);
        return extras is not null && extras.ContainsKey(schluessel) ? extras[schluessel].AsString() : "";
    }

    private static Godot.Collections.Dictionary? Extras(Node knoten) =>
        knoten.HasMeta("extras") && knoten.GetMeta("extras").VariantType == Variant.Type.Dictionary ? knoten.GetMeta("extras").AsGodotDictionary() : null;

    // ═════════════════════════════════════════════════════════════════════════
    //  Jedes Bild: der Raum folgt dem Kern
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Stufe, Einrichtung, Glut, Arbeiter, Werkstücke und Warenstapel nach dem Zustand des Betriebs – angefasst wird nur, was sich geändert hat.</summary>
    private void InnenModellAktualisieren(BetriebsAnsicht a, ModellRaumDaten md, Spielzeit zeit, bool arbeitszeit, bool arbeitet, double delta)
    {
        var b = a.Betrieb;
        var stufe = Modellstufe(b.Stufe);
        if (stufe != md.Stufe)
        {
            var erstes = md.Stufe == 0;
            md.Stufe = stufe;
            for (var i = 1; i <= 3; i++)
                if (md.Stufen[i] is { } teil) teil.Visible = i == stufe;
            foreach (var (knoten, abStufe) in md.StufenKnoten) knoten.Visible = stufe >= abStufe; // Sägehalle und ihre Tür
            md.ArbeiterStand = ""; // mehr Plätze: Arbeiter neu setzen
            md.Hell = -1;          // Lichter der Stufe
            if (!erstes && md.StufenKnoten.Count > 0) InnenRaeumeNeuPruefen(); // nach einem Ausbau steht der neue Raum sofort in der Leiste
        }

        if (a.Farbe != md.Hausfarbe)
        {
            md.Hausfarbe = a.Farbe;
            foreach (var stoff in md.Hausfarben) stoff.AlbedoColor = a.Farbe;
        }

        var einrichtung = string.Join(",", b.Einrichtung.OrderBy(x => x));
        if (einrichtung != md.EinrichtungStand)
        {
            md.EinrichtungStand = einrichtung;
            foreach (var (kennung, knoten) in md.Einrichtung) knoten.Visible = b.HatEinrichtung(kennung);
            if (md.Holztuer is not null) md.Holztuer.Visible = !b.HatEinrichtung("Eisentuer");
        }
        foreach (var klick in md.Klicks) // anklickbar nur, was zu sehen ist: Einrichtung nach dem Kauf, Tür und Raum ab ihrer Stufe
        {
            var sichtbar = klick.Knoten.IsVisibleInTree();
            if (klick.Form.Disabled == sichtbar) klick.Form.Disabled = !sichtbar;
        }

        if (arbeitet != md.Glueht)
        {
            md.Glueht = arbeitet;
            foreach (var stoff in md.Glut) stoff.EmissionEnergyMultiplier = arbeitet ? md.GlutAn : 0f;
            md.Hell = -1;
        }
        if (Mathf.Abs(_raumHell - md.Hell) > 0.01f)
        {
            md.Hell = _raumHell;
            if (md.DampfStoff is { } dampf) dampf.AlbedoColor = new Color(0.5f, 0.5f, 0.5f).Lerp(new Color(0.9f, 0.9f, 0.87f), _raumHell); // unbeschattet: nachts dunkler, damit er nicht leuchtet
            foreach (var (licht, art, abStufe) in md.Lichter)
            {
                licht.Visible = stufe >= abStufe;
                licht.LightEnergy = art switch
                {
                    LichtArt.Glut => arbeitet ? 1.6f : 0.25f,
                    LichtArt.Lampe => arbeitet ? 0.55f + 0.5f * (1f - _raumHell) : 0.05f, // brennt wie die Laternenscheibe (Material „Glut“) nur bei der Arbeit, nachts heller
                    _ => 0.05f + 0.8f * _raumHell,
                };
            }
        }

        InnenSpaeneAktualisieren(md, b, zeit);
        if (md.Animation is not null)
            InnenAnimationSetzen(md, !arbeitet ? AnimationStand.Ruht : _simulation!.Takt.IstPausiert ? AnimationStand.Haelt : AnimationStand.Laeuft, delta);

        InnenArbeiterAktualisieren(a, md, b, stufe, arbeitszeit, arbeitet, delta);
        InnenGaesteAktualisieren(a, md, b, delta);
        InnenDampfAktualisieren(md, stufe, arbeitet);
        InnenLagerAktualisieren(a, md, b);
        InnenAuslageAktualisieren(a, md, b);
    }

    /// <summary>
    /// Dampf steigt, solange das Werkstück seines Arbeitsplatzes im Gefäß steht (also an diesem Platz gearbeitet wird und die Stufe da ist); eine Quelle ohne Platz dampft bei jeder Arbeit.
    /// Ohne Arbeit hört die Quelle auf, was aufgestiegen ist, vergeht von allein. In der Spielpause stehen die Flecken still.
    /// </summary>
    private void InnenDampfAktualisieren(ModellRaumDaten md, int stufe, bool arbeitet)
    {
        var tempo = _simulation!.Takt.IstPausiert ? 0f : 1f;
        foreach (var quelle in md.Dampf)
        {
            var an = stufe >= quelle.AbStufe && (quelle.Platz is { Werkstueck: { } stueck } ? stueck.Visible : arbeitet);
            if (quelle.Teilchen.Emitting != an) quelle.Teilchen.Emitting = an;
            if (!Mathf.IsEqualApprox(quelle.Teilchen.SpeedScale, tempo)) quelle.Teilchen.SpeedScale = tempo;
        }
    }

    /// <summary>
    /// Führt die Animation „Arbeit“: Läuft die Arbeit, rückt die Phase um <paramref name="delta"/> weiter (Schleife); bei Spielpause bleibt sie stehen
    /// (danach geht es dort weiter); wird nicht gearbeitet (Feierabend, Ausfall, Stillstand), steht sie in der Ruhelage (Zeit 0, Phase 0).
    /// Die Pose wird selbst gesetzt (Seek), der Player läuft nicht von allein: So bleiben Raum und Figuren im Takt.
    /// </summary>
    private static void InnenAnimationSetzen(ModellRaumDaten md, AnimationStand stand, double delta)
    {
        if (md.Animation is not { } animation) return;
        if (stand != md.ArbeitStand)
        {
            md.ArbeitStand = stand;
            if (stand == AnimationStand.Ruht)
            {
                md.ArbeitPhase = 0;
                animation.Seek(0, true);
            }
        }
        if (stand != AnimationStand.Laeuft || delta <= 0) return;
        md.ArbeitPhase = (md.ArbeitPhase + delta) % md.ArbeitDauer;
        animation.Seek(SchleifenAnfang + md.ArbeitPhase, true);
    }

    /// <summary>Die Raumleiste hat sich geändert (z. B. kam die Sägehalle dazu); steht man in einem Raum, der nicht mehr zu betreten ist, geht es in den ersten.</summary>
    private void InnenRaeumeNeuPruefen()
    {
        if (_innen is null) return;
        var raeume = Zugaengliche(_innen).ToList();
        if (_raum is not null && raeume.Count > 0 && !raeume.Contains(_raum)) RaumZeigen(raeume[0]); // meldet RaumGewechselt: die Leiste baut sich neu
        else InnenRaeumeGeaendert?.Invoke();
    }

    /// <summary>Ab so vielen Minuten Arbeit am Tag (nach dem Arbeitsbeginn, ohne Stillstand) liegt die erste, zweite und dritte Menge Späne (Index 1–3): 10, 13 und 16 Uhr.</summary>
    private static readonly int[] SpaeneAbMinuten = { 0, 120, 300, 480 };

    /// <summary>
    /// Hobelspäne nach der Arbeit des Tages: keine, bis zwei Stunden gearbeitet wurde (10 Uhr), dann eine Menge, ab 13 Uhr mehr, ab 16 Uhr am meisten.
    /// Gezählt werden die Minuten seit Arbeitsbeginn ohne den Stillstand des Tages; ohne Arbeiter wird nicht gearbeitet. Zum Jahreswechsel
    /// (der Tag beginnt um 5 Uhr) ist alles weggefegt. Reine Darstellung, angefasst wird nur bei einer neuen Menge.
    /// </summary>
    private void InnenSpaeneAktualisieren(ModellRaumDaten md, Betrieb b, Spielzeit zeit)
    {
        if (md.Spaene.All(x => x is null)) return;
        var arbeitsminuten = b.Arbeitsminuten;
        var gearbeitet = b.Belegt > 0
            ? Math.Clamp(zeit.UhrzeitInMinuten - Stadtwirtschaft.Arbeitsbeginn, 0, arbeitsminuten) - b.StillstandMinuten
            : 0;
        var menge = 0;
        for (var i = 1; i < SpaeneAbMinuten.Length; i++)
            if (gearbeitet >= SpaeneAbMinuten[i]) menge = i;
        if (menge == md.SpaeneStand) return;
        md.SpaeneStand = menge;
        for (var i = 1; i < md.Spaene.Length; i++)
            if (md.Spaene[i] is { } spaene) spaene.Visible = i == menge;
    }

    /// <summary>
    /// Farbe des Werkstücks auf der Werkbank. Meist die Farbe der Ware; in der Tischlerei ist das Stück Holz: der Ton der Ware „Holz“, in der Maurerei Stein: der Ton
    /// der Ware „Granit“, jeweils nur zu einem Zehntel zur Warenfarbe getönt, damit man den Wechsel der Ware noch ahnt (ein hellblauer Klotz auf dem Stamm wirkte wie ein Fremdkörper).
    /// </summary>
    private static Color WerkstueckFarbe(Werkstil stil, string ware) => stil switch
    {
        Werkstil.Tischlerei => Warenfarbe("Holz").Lerp(Warenfarbe(ware), 0.1f),
        Werkstil.Maurerei => Warenfarbe("Granit").Lerp(Warenfarbe(ware), 0.1f),
        _ => Warenfarbe(ware),
    };

    /// <summary>
    /// Je Arbeiter eine Figur am nächsten freien Arbeitsplatz (Reihenfolge der Nummern), mit der Bewegung des Platzes – nur zur Arbeitszeit und
    /// nicht während eines Ausfalls. Gearbeitet wird, solange der Betrieb arbeitet und seine Ware nicht stillsteht; sonst stehen sie da.
    /// </summary>
    private void InnenArbeiterAktualisieren(BetriebsAnsicht a, ModellRaumDaten md, Betrieb b, int stufe, bool arbeitszeit, bool arbeitet, double delta)
    {
        var w = _simulation?.Wirtschaft;
        if (w is null) return;
        var zeilen = w.Werkstaetten.Arbeiterliste(b);
        var stand = $"{stufe}|" + string.Join(",", zeilen.Select(z => $"{z.Nummer}:{z.Arbeitskraft}"));
        if (stand != md.ArbeiterStand)
        {
            md.ArbeiterStand = stand;
            ArbeiterNeuStellen(a, md, zeilen, stufe);
        }

        var still = new HashSet<string>(w.Werkstaetten.WarenInArbeit(b).Where(x => x.Stillstand is not null).Select(x => x.Ware));
        var bewegt = !_simulation!.Takt.IstPausiert;
        var werkstueckStand = new List<string>();
        foreach (var zeile in zeilen)
        {
            if (!md.Arbeiter.TryGetValue(zeile.Nummer, out var arbeiter)) continue;
            var da = arbeitszeit && zeile.FaelltAusBis is null;
            if (arbeiter.Halter.Visible != da) arbeiter.Halter.Visible = da;
            if (arbeiter.Klick.Disabled == da) arbeiter.Klick.Disabled = !da; // Einsteigen setzt alle Klickflächen an: abgleichen
            var arbeitet1 = da && arbeitet && !still.Contains(zeile.Ware);
            var bewegung = arbeitet1 ? FigurBewegungVon(arbeiter) : "Stehen";
            // Bewegungen, deren Hand oder Fuß ein bewegtes Teil des Raums führt (Animation „Arbeit“, siehe RaumTakt): Die Figur läuft in dessen Takt, nicht mit eigener Startphase und Tempo.
            var eigene = bewegung == arbeiter.Platz.Bewegung; // sonst steht sie oder hilft sich mit „Gebeugt“
            var takt = eigene && md.Animation is not null ? arbeiter.Platz.Takt : 0;
            if (bewegung != arbeiter.Bewegung)
            {
                arbeiter.Bewegung = bewegung;
                BewegungSetzen(arbeiter.Figur, bewegung, takt > 0 ? 1f : (float)Mathf.Clamp(zeile.TempoFaktor, 0.5, 1.5), eigene ? arbeiter.Platz.Werkzeug : null);
            }
            if (da && takt > 0)
            {
                // Phase des Raums in der Schleife des Platzes (Sekunden: Drechseln 1, Gatter 2, Sägen 1, Schleifen 1,2), umgerechnet auf die Schleife der Figur
                arbeiter.Figur.Phase = md.ArbeitPhase % takt / takt * arbeiter.Figur.Dauer;
                FigurBewegen(arbeiter.Figur, 0);
            }
            else if (da && (bewegt || arbeiter.Figur.Neu)) FigurBewegen(arbeiter.Figur, bewegt ? delta : 0); // in der Pause nur die erste Pose der neuen Bewegung setzen
            var gewaehlt = _gewaehlt.Contains(zeile.Nummer);
            FigurHervorheben(arbeiter.Figur, gewaehlt || _hoverArbeiter == zeile.Nummer);
            if (arbeiter.Ring.Visible != gewaehlt) arbeiter.Ring.Visible = gewaehlt;
            werkstueckStand.Add($"{arbeiter.Platz.Nr}:{(arbeitet1 ? zeile.Ware : "")}");
        }
        var stueck = string.Join(",", werkstueckStand);
        if (stueck == md.WerkstueckStand) return;
        md.WerkstueckStand = stueck;
        foreach (var platz in md.Plaetze)
        {
            if (platz.Werkstueck is null) continue;
            var ware = werkstueckStand.FirstOrDefault(s => s.StartsWith($"{platz.Nr}:"))?.Split(':', 2)[1] ?? "";
            platz.Werkstueck.Visible = ware != "" && platz.AbStufe <= stufe;
            if (ware != "" && ware != platz.WerkstueckWare)
            {
                platz.WerkstueckWare = ware;
                platz.WerkstueckStoff!.AlbedoColor = WerkstueckFarbe(a.Stil, ware);
            }
        }
    }

    /// <summary>
    /// Name der Bewegung in den Figurenmodellen: die Bewegung des Arbeitsplatzes. Kennt das Modell sie noch nicht, hilft sich die Figur mit
    /// „Gebeugt“ (arbeitet vorgebeugt), sonst bleibt sie stehen.
    /// </summary>
    private static string FigurBewegungVon(ModellArbeiter arbeiter)
    {
        var spieler = arbeiter.Figur.Spieler;
        if (spieler.HasAnimation(arbeiter.Platz.Bewegung)) return arbeiter.Platz.Bewegung;
        return spieler.HasAnimation("Gebeugt") ? "Gebeugt" : "Stehen";
    }

    private void ArbeiterNeuStellen(BetriebsAnsicht a, ModellRaumDaten md, IReadOnlyList<ArbeiterZeile> zeilen, int stufe)
    {
        foreach (var alt in md.Arbeiter.Values)
        {
            a.Raumklicks.Remove(alt.Klick);
            alt.Halter.GetParent()?.RemoveChild(alt.Halter);
            alt.Halter.QueueFree();
        }
        md.Arbeiter.Clear();
        var plaetze = md.Plaetze.Where(p => p.AbStufe <= stufe).ToList();
        foreach (var (zeile, platz) in zeilen.OrderBy(z => z.Nummer).Zip(plaetze))
        {
            // Der Anker blickt mit −Z auf sein Werkstück, die Figur mit +Z: um 180° gedreht einsetzen.
            var halter = new Node3D { Rotation = new Vector3(0, Mathf.Pi, 0) };
            platz.Anker.AddChild(halter);
            if (FigurEinsetzen(halter, ArbeiterAussehen(zeile), zeile.Nummer) is not { } figur)
            {
                halter.QueueFree();
                continue;
            }
            // Wer sitzt (Feinarbeit), hat die Hüfte auf Sitzhöhe des Menschen: kleine Völker sitzen höher, Orks tiefer (wie die Sitzenden im Dom).
            if (platz.Bewegung == "Feinarbeit") halter.Position = new Vector3(0, SitzKniehoehe - figur.Kniehoehe, 0);
            var koerper = new StaticBody3D();
            koerper.SetMeta("innen", "arbeiter:" + zeile.Nummer);
            var form = new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.38f, Height = 1.75f }, Position = new Vector3(0, 0.88f, 0) };
            koerper.AddChild(form);
            halter.AddChild(koerper);
            a.Raumklicks.Add(form);
            var ring = new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 0.46f, OuterRadius = 0.58f, RingSegments = 6, Rings = 24 },
                MaterialOverride = AuswahlRing(), Position = new Vector3(0, 0.04f, 0), Visible = false,
            };
            halter.AddChild(ring);
            RaumEbeneUebernehmen(halter);
            md.Arbeiter[zeile.Nummer] = new ModellArbeiter { Nummer = zeile.Nummer, Figur = figur, Halter = halter, Platz = platz, Klick = form, Ring = ring };
        }
        if (_gewaehlt.RemoveWhere(n => !md.Arbeiter.ContainsKey(n)) > 0) ArbeiterAuswahlGeaendert?.Invoke(); // Gegangene sind nicht mehr gewählt
    }

    /// <summary>Kniehöhe des Menschen (Meter): Der Hocker der Feinarbeit ist für ihn gebaut.</summary>
    private const float SitzKniehoehe = 0.50f;

    private static StandardMaterial3D? _auswahlRing;

    /// <summary>Das goldene Material der Auswahlringe (ohne Schattierung, damit es im dunklen Raum leuchtet).</summary>
    private static StandardMaterial3D AuswahlRing() => _auswahlRing ??= new StandardMaterial3D
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.98f, 0.82f, 0.35f),
    };

    /// <summary>Wie ein Arbeiter aussieht: Lehrlinge heller und schlichter, Gesellen in Braun, Orks grün; alle mit Lederschürze.</summary>
    private static FigurAussehen ArbeiterAussehen(ArbeiterZeile zeile)
    {
        var volk = zeile.Arbeitskraft == "Ork" ? Volk.Ork : Volk.Mensch;
        var gewand = zeile.Arbeitskraft switch
        {
            "Lehrling" => new Color(0.62f, 0.55f, 0.40f),
            "Ork" => new Color(0.30f, 0.27f, 0.22f),
            "Leibeigener" => new Color(0.45f, 0.43f, 0.39f),
            _ => new Color(0.42f, 0.30f, 0.20f),
        };
        var haare = Haarfarben(volk);
        var frau = volk == Volk.Mensch && Streuung(zeile.Nummer, 53) % 5 == 0;
        return new FigurAussehen(volk, frau, gewand, Beinkleidfarben[zeile.Nummer % Beinkleidfarben.Length], HautVon(volk),
            haare[zeile.Nummer * 5 % haare.Length], Leinenfarbe, frau ? Kopfbedeckung.Haube : Kopfbedeckung.Keine, Last.Keine, Schuerze: true);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Warenstapel
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Je Ware im Betrieb ein Stapel Kisten an einem <c>Lager_&lt;n&gt;</c>: in der Farbe der Ware, die Höhe nach dem Anteil am vollen Lager.
    /// Erst Zutaten, dann das Produkt, dann was sonst im Lager liegt (wie bei den alten Räumen).
    /// </summary>
    private void InnenLagerAktualisieren(BetriebsAnsicht a, ModellRaumDaten md, Betrieb b)
    {
        var waren = b.Zutaten.Keys.Append(b.Produkt).Concat(b.Lager.Keys).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        foreach (var ware in waren)
        {
            var stapel = md.Stapel.FirstOrDefault(s => s.Ware == ware);
            if (stapel is null)
            {
                if (md.Stapel.Count >= md.Lagerplaetze.Count) continue;
                stapel = StapelBauen(a, md, md.Lagerplaetze[md.Stapel.Count], ware);
                md.Stapel.Add(stapel);
            }
            var menge = b.ImLager(ware);
            if (stapel.Klick.Disabled == (menge > 0)) stapel.Klick.Disabled = menge <= 0; // leere Stapel sind nicht anklickbar
            // Das Schild sieht durch Wände (kein Tiefentest): nur im Lager zeigen, und nur bei Ware
            var schildSichtbar = menge > 0 && _raum?.Kennung == RaumLager;
            if (stapel.Schild.Visible != schildSichtbar) stapel.Schild.Visible = schildSichtbar;
            var stand = $"{menge}/{b.LagerKapazitaetJeWare}";
            if (stand == stapel.Stand) continue;
            stapel.Stand = stand;
            var kisten = menge <= 0 ? 0 : Math.Clamp((int)Math.Ceiling(StapelHoehe * menge / (double)b.LagerKapazitaetJeWare), 1, StapelHoehe);
            for (var i = 0; i < stapel.Kisten.Length; i++) stapel.Kisten[i].Visible = i < kisten;
            stapel.Schild.Text = $"{ware} {menge}";
            stapel.Schild.Position = new Vector3(0, 0.55f + kisten * 0.46f, 0);
        }
    }

    private LagerStapel StapelBauen(BetriebsAnsicht a, ModellRaumDaten md, Node3D platz, string ware)
    {
        var stoff = Material(Warenfarbe(ware));
        var kisten = new MeshInstance3D[StapelHoehe];
        for (var i = 0; i < kisten.Length; i++)
        {
            kisten[i] = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.78f, 0.42f, 0.62f) },
                MaterialOverride = stoff,
                Position = new Vector3(0.02f * (i % 2 == 0 ? 1 : -1), 0.23f + i * 0.46f, 0),
                RotationDegrees = new Vector3(0, i % 2 == 0 ? 2 : -3, 0),
                Visible = false,
            };
            platz.AddChild(kisten[i]);
        }
        var schild = Beschriftung(ware, Vector3.Zero, 18, platz);
        schild.Position = new Vector3(0, 0.7f, 0);
        var koerper = new StaticBody3D();
        koerper.SetMeta("innen", "ware:" + ware);
        var form = new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.85f, 2.3f, 0.7f) }, Position = new Vector3(0, 1.15f, 0), Disabled = true };
        koerper.AddChild(form);
        platz.AddChild(koerper);
        a.Raumklicks.Add(form);
        RaumEbeneUebernehmen(platz);
        return new LagerStapel { Wurzel = platz, Kisten = kisten, Stoff = stoff, Schild = schild, Klick = form, Koerper = koerper, Ware = ware };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Auslage im Laden
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>So viele Gefäße hat eine volle Reihe (wie die Kisten eines vollen Stapels).</summary>
    private const int AuslageReihe = 5;

    /// <summary>Abstand der Gefäße in der Reihe (Meter); der Platz im Regal ist 0,85 m breit.</summary>
    private const float AuslageAbstand = 0.17f;

    /// <summary>In dieser Reihenfolge füllt sich die Reihe: erst die Mitte, dann abwechselnd links und rechts.</summary>
    private static readonly int[] AuslageFolge = { 2, 1, 3, 0, 4 };

    private static StandardMaterial3D? _auslageKork, _auslageMessing;
    private static readonly Dictionary<string, Mesh> _auslageNetze = new();

    /// <summary>Die Netze der Gefäße (Flaschenbauch, Hals, Kork, Tiegel, Deckel, beim Parfumeur Flakon und Phiole), alle Reihen teilen sie.</summary>
    private static Mesh AuslageNetz(string art) => _auslageNetze.TryGetValue(art, out var netz) ? netz : _auslageNetze[art] = art switch
    {
        "Bauch" => new CylinderMesh { TopRadius = 0.046f, BottomRadius = 0.062f, Height = 0.16f, RadialSegments = 12, Rings = 1 },
        "Hals" => new CylinderMesh { TopRadius = 0.020f, BottomRadius = 0.026f, Height = 0.09f, RadialSegments = 10, Rings = 1 },
        "Kork" => new CylinderMesh { TopRadius = 0.026f, BottomRadius = 0.022f, Height = 0.035f, RadialSegments = 10, Rings = 1 },
        "Tiegel" => new CylinderMesh { TopRadius = 0.062f, BottomRadius = 0.066f, Height = 0.105f, RadialSegments = 12, Rings = 1 },
        // Parfumeur: Flakon mit achteckigem Bauch, kurzem Hals und Kugelstöpsel; schlanke Phiole mit Korken
        "FlakonBauch" => new CylinderMesh { TopRadius = 0.052f, BottomRadius = 0.064f, Height = 0.12f, RadialSegments = 8, Rings = 1 },
        "FlakonHals" => new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.022f, Height = 0.05f, RadialSegments = 8, Rings = 1 },
        "Stoepsel" => new SphereMesh { Radius = 0.03f, Height = 0.06f, RadialSegments = 10, Rings = 5 },
        "PhioleBauch" => new CylinderMesh { TopRadius = 0.026f, BottomRadius = 0.04f, Height = 0.19f, RadialSegments = 10, Rings = 1 },
        "PhioleKork" => new CylinderMesh { TopRadius = 0.022f, BottomRadius = 0.018f, Height = 0.03f, RadialSegments = 8, Rings = 1 },
        _ => new CylinderMesh { TopRadius = 0.066f, BottomRadius = 0.07f, Height = 0.03f, RadialSegments = 12, Rings = 1 }, // Deckel
    };

    /// <summary>
    /// Im Laden steht je Fertigware des Hauses (<c>Betriebsart.Produkte</c>, der Reihe nach an <c>Auslage_1</c> …) eine Reihe Flaschen und Tiegel in der Farbe der Ware, so viele
    /// wie der Anteil am vollen Lager (höchstens fünf, mindestens eines, solange etwas im Lager liegt). Ein Platz bleibt einer Ware treu, auch wenn ihr Lager leer ist.
    /// Ein Klick auf die Reihe öffnet das Lager der Ware (wie bei den Stapeln im Dachboden).
    /// </summary>
    private void InnenAuslageAktualisieren(BetriebsAnsicht a, ModellRaumDaten md, Betrieb b)
    {
        if (md.Auslageplaetze.Count == 0) return;
        var produkte = b.Artdaten.Produkte;
        while (md.Auslagen.Count < md.Auslageplaetze.Count) md.Auslagen.Add(null);
        for (var i = 0; i < md.Auslageplaetze.Count; i++)
        {
            var ware = i < produkte.Count ? produkte[i] : "";
            var menge = ware == "" ? 0 : b.ImLager(ware);
            var platz = md.Auslagen[i];
            if (platz is null)
            {
                if (menge <= 0) continue; // gebaut wird mit dem ersten Stück im Lager
                md.Auslagen[i] = platz = AuslageBauen(a, md.Auslageplaetze[i]);
            }
            var kapazitaet = Math.Max(1, b.LagerKapazitaetJeWare);
            var stand = $"{ware}|{menge}/{kapazitaet}";
            if (stand != platz.Stand)
            {
                platz.Stand = stand;
                if (ware != platz.Ware)
                {
                    platz.Ware = ware;
                    platz.Stoff.AlbedoColor = Warenfarbe(ware);
                    platz.Klick.GetParent().SetMeta("innen", "ware:" + ware);
                }
                var anzahl = menge <= 0 ? 0 : Math.Clamp((int)Math.Ceiling(AuslageReihe * menge / (double)kapazitaet), 1, AuslageReihe);
                for (var k = 0; k < AuslageReihe; k++) platz.Gefaesse[AuslageFolge[k]].Visible = k < anzahl;
                platz.Schild.Text = $"{ware} {menge}";
            }
            var aktiv = menge > 0 && platz.Anker.IsVisibleInTree(); // ohne Ware oder ohne Laden (Stufe 1) nicht anklickbar
            if (platz.Klick.Disabled == aktiv) platz.Klick.Disabled = !aktiv;
            var schildSichtbar = aktiv && _raum?.Kennung == RaumLaden; // das Schild sieht durch Wände: nur im Laden
            if (platz.Schild.Visible != schildSichtbar) platz.Schild.Visible = schildSichtbar;
        }
    }

    /// <summary>
    /// Baut die fünf Gefäße einer Reihe am Anker (Mitte der Brettoberkante, −Z zeigt in den Laden): abwechselnd Flasche und Tiegel, beim Parfumeur (Siederei) abwechselnd
    /// Flakon mit Kugelstöpsel und schlanke Phiole, alle unsichtbar, bis Ware da ist.
    /// Die Farbe der Ware trägt ein Material je Reihe, Hals, Kork und Deckel sind aus dunklem Holz und Wachs, die Stöpsel der Flakons aus Messing.
    /// </summary>
    private AuslagePlatz AuslageBauen(BetriebsAnsicht a, Node3D anker)
    {
        var stoff = Material(Colors.White);
        stoff.Roughness = 0.45f;
        _auslageKork ??= Material(new Color(0.34f, 0.23f, 0.14f));
        _auslageMessing ??= Material(new Color(0.72f, 0.56f, 0.24f));
        var duft = a.Stil == Werkstil.Siederei;
        var gefaesse = new Node3D[AuslageReihe];
        for (var i = 0; i < AuslageReihe; i++)
        {
            var gefaess = new Node3D
            {
                Position = new Vector3((i - AuslageReihe / 2) * AuslageAbstand, 0, ((i * 7) % 3 - 1) * 0.03f),
                RotationDegrees = new Vector3(0, i * 53 % 360, 0),
                Visible = false,
            };
            MeshInstance3D Teil(string art, float hoehe, StandardMaterial3D material) => new() { Mesh = AuslageNetz(art), MaterialOverride = material, Position = new Vector3(0, hoehe, 0) };
            if (duft && i % 2 == 0) // Flakon: achteckiger Bauch, Hals, Kugelstöpsel aus Messing
            {
                gefaess.AddChild(Teil("FlakonBauch", 0.06f, stoff));
                gefaess.AddChild(Teil("FlakonHals", 0.145f, stoff));
                gefaess.AddChild(Teil("Stoepsel", 0.2f, _auslageMessing));
            }
            else if (duft) // Phiole: schlankes Fläschchen mit Kork
            {
                gefaess.AddChild(Teil("PhioleBauch", 0.095f, stoff));
                gefaess.AddChild(Teil("PhioleKork", 0.205f, _auslageKork));
            }
            else if (i % 2 == 0) // Flasche: Bauch, Hals, Kork
            {
                gefaess.AddChild(Teil("Bauch", 0.08f, stoff));
                gefaess.AddChild(Teil("Hals", 0.205f, stoff));
                gefaess.AddChild(Teil("Kork", 0.268f, _auslageKork));
            }
            else // Tiegel mit Deckel
            {
                gefaess.AddChild(Teil("Tiegel", 0.0525f, stoff));
                gefaess.AddChild(Teil("Deckel", 0.12f, _auslageKork));
            }
            anker.AddChild(gefaess);
            gefaesse[i] = gefaess;
        }
        var schild = Beschriftung("", Vector3.Zero, 12, anker);
        schild.Position = new Vector3(0, 0.38f, 0);
        schild.Visible = false;
        var koerper = new StaticBody3D();
        koerper.SetMeta("innen", "ware:");
        var form = new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.85f, 0.39f, 0.32f) }, Position = new Vector3(0, 0.195f, 0), Disabled = true };
        koerper.AddChild(form);
        anker.AddChild(koerper);
        a.Raumklicks.Add(form);
        RaumEbeneUebernehmen(anker);
        return new AuslagePlatz { Anker = anker, Gefaesse = gefaesse, Stoff = stoff, Klick = form, Schild = schild };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Namen unter der Maus
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Der Name eines Bedienungsstücks auf seinem Schild: Das Personalbuch, das Hoftor und die Werkzeugwand heißen immer so, die anderen nach dem Knoten
    /// (Amboss, Werkbank, Hobelbank …); Umlaute stehen im Knotennamen umschrieben (<c>Kraeutertisch</c>), das Schild schreibt sie aus.
    /// </summary>
    private static string BedienungName(Werkstil stil, string tafel, string ding) => (tafel, ding) switch
    {
        ("Personal", _) => "Personalbuch",
        ("Karren", _) => "Hoftor",
        ("Einrichten", _) => "Werkzeugwand",
        ("Lager", "Bretter") => "Bretterstapel",
        ("Lager", "Quader") => "Quaderstapel",
        ("Lager", "Kraeuter") => "Kräuterbündel",
        ("Lager", "Koerbe") => "Blütenkörbe",
        ("Lager", "Faesser") => "Fässer",
        ("Produktion", "Kessel") when stil == Werkstil.Siederei => "Brennkessel",
        (_, "Kraeutertisch") => "Kräutertisch",
        _ => ding,
    };

    /// <summary>
    /// Die Gegenstände des betretenen Raums, die man anklicken kann, nach den Tafeln geordnet (Buch, Werkbank, Lager, Hoftor, Werkzeugwand …) – für den Hinweis beim Betreten.
    /// Leer, wenn das Haus kein Modell hat.
    /// </summary>
    public string InnenBedienungen => _innen is BetriebsAnsicht { Modellraum: { } md } a
        ? string.Join(", ", md.Bedienungen.Where(x => IstDaAbStufe(x.Knoten, md, Modellstufe(a.Betrieb.Stufe)))
            .OrderBy(x => Array.IndexOf(BedienungTafeln, x.Tafel)).Select(x => BedienungName(a.Stil, x.Tafel, x.Name)).Distinct())
        : "";

    /// <summary>Gibt es den Knoten in dieser Betriebsstufe? Nicht, wenn er selbst oder einer seiner Vorfahren (der Laden, der Turm) erst später dazukommt (<see cref="ModellRaumDaten.StufenKnoten"/>).</summary>
    private static bool IstDaAbStufe(Node knoten, ModellRaumDaten md, int stufe) =>
        md.StufenKnoten.All(x => stufe >= x.AbStufe || !(x.Knoten == knoten || x.Knoten.IsAncestorOf(knoten)));

    private static readonly string[] BedienungTafeln = { "Personal", "Produktion", "Lager", "Karren", "Einrichten" };

    /// <summary>Der Text zum Schild an der Maus: erste Zeile der Name, darunter in kleiner Schrift, was ein Klick tut. Leer: nichts anzeigen.</summary>
    private (string Name, string Hinweis) InnenText(BetriebsAnsicht a, string tag)
    {
        var teile = tag.Split(':');
        var w = _simulation?.Wirtschaft;
        switch (teile[0])
        {
            case "bedienung" when teile.Length >= 3:
                return (BedienungName(a.Stil, teile[1], teile[2]), teile[1] switch
                {
                    "Personal" => "Klick: Arbeiter einstellen, befördern, entlassen, Lohn und Prämie",
                    "Produktion" => "Klick: Waren herstellen und Arbeiter zuteilen",
                    "Lager" => "Klick: Bestand und Zurückhalten",
                    "Karren" => "Klick: Karren des Hauses",
                    "Einrichten" => "Klick: Einrichtung kaufen",
                    _ => "",
                });
            case "tuer" when teile.Length >= 4:
                return (string.Join(':', teile[3..]), "Klick: Raum wechseln");
            case "tuer" when teile.Length >= 3:
                return (teile[2] switch
                {
                    "Lager" => "Tür zum Lager", "Saege" => "Tür zur Sägehalle", "Huette" => "Tür zur Bauhütte", "Laden" => "Tür zum Laden", "Turm" => "Tür zum Laborturm", "Pavillon" => "Tür zum Duftpavillon", "Kueche" => "Tür zur Küche", "Saal" => "Tür zum Saal",
                    _ => "Tür zum Werkraum",
                }, "Klick: Raum wechseln");
            case "einrichtung" when teile.Length >= 2:
                var stueck = w?.Werkstaetten.Einrichtungsliste(a.Betrieb).FirstOrDefault(e => e.Kennung == teile[1]);
                return (stueck?.Name ?? teile[1], stueck is null ? "" : "Eingerichtet: " + stueck.Wirkung);
            case "gast":
                return ("Gast", "");
            case "barde":
                return ("Barde", "spielt zur Laute");
            case "ware" when teile.Length >= 2:
                var kapazitaet = a.Betrieb.LagerKapazitaetJeWare;
                return (teile[1], $"{a.Betrieb.ImLager(teile[1])} von {kapazitaet} im Lager");
            case "arbeiter" when teile.Length >= 2 && int.TryParse(teile[1], out var nr):
                var zeile = w?.Werkstaetten.Arbeiterliste(a.Betrieb).FirstOrDefault(z => z.Nummer == nr);
                if (zeile is null) return ("", "");
                return ($"{zeile.Arbeitskraft} Nr. {zeile.Nummer}", $"arbeitet an: {zeile.Ware} · Laune {Zahlenformat.Ganz(zeile.Laune)}"
                                                                    + (_gewaehlt.Contains(nr) ? " · ausgewählt" : ""));
        }
        return ("", "");
    }
}
