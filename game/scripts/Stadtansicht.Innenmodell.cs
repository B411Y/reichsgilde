using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Innenräume aus Blender-Modellen – elfter Teil der <c>partial class Stadtansicht</c> (PR #152).
///
/// Ein Beruf bekommt seinen Innenraum, indem <see cref="InnenModellDatei"/> eine Datei nennt (Schmied: <c>schmied_innen.glb</c>, Tischler:
/// <c>tischler_innen.glb</c>). Alles Weitere liest dieses Gerüst aus den Knotennamen des Modells (Verträge: <c>docs/bereiche/darstellung.md</c>):
/// <list type="bullet">
/// <item><c>Raum_Werk</c>, <c>Raum_Lager</c>, <c>Raum_Saege</c> – die Räume (glTF-extras „stufe“ auf dem Raumknoten: erst ab dieser Betriebsstufe da, so die
/// Sägehalle ab Stufe 3); <c>Blick_&lt;Raum&gt;_&lt;0|L1|R1|O1|U1…&gt;</c> – feste Blickpunkte der Kamera (Stadtansicht.Innenkamera.cs)</item>
/// <item><c>Stufe1</c>–<c>Stufe3</c> – Ausstattung der Betriebsstufe (genau eine sichtbar); <c>Einrichtung_&lt;Kennung&gt;</c> – gekaufte Einrichtung (erst nach dem Kauf sichtbar)</item>
/// <item><c>Bedienung_&lt;Tafel&gt;_&lt;Name&gt;</c> und <c>Tuer_&lt;von&gt;_&lt;nach&gt;</c> – anklickbare Gegenstände (Klickfläche aus dem Umriss des Netzes; eine Tür mit „stufe“ ist darunter unsichtbar und nicht anklickbar)</item>
/// <item><c>Arbeit_&lt;n&gt;_&lt;Bewegung&gt;</c> – Arbeitsplätze; <c>Werkstueck_&lt;n&gt;</c> – Stück in Arbeit; <c>Lager_&lt;n&gt;</c> – Stellplätze der Warenstapel; <c>Licht_&lt;n&gt;</c> – Lichter (extras „art“: Glut, Fenster, Lampe)</item>
/// <item><c>Spaene1</c>–<c>Spaene3</c> – Hobelspäne, höchstens einer sichtbar, nach der Arbeit des Tages; die Animation „Arbeit“ des Modells läuft als Schleife, solange gearbeitet wird</item>
/// </list>
/// Der Raum folgt dem Kern: Stufe, Einrichtung, Glut, Arbeiter, Werkstücke und Warenstapel werden nur angefasst, wenn sich etwas geändert hat.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Welche Datei den Innenraum eines Werkstils stellt (null: noch der alte Raum aus Grundkörpern). Pro neuem Beruf eine Zeile mehr.</summary>
    private static string? InnenModellDatei(Werkstil stil) => stil switch
    {
        Werkstil.Giesserei => "schmied_innen",
        Werkstil.Tischlerei => "tischler_innen",
        _ => null,
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
        public Node3D? WerkstueckAnker { get; set; }
        public MeshInstance3D? Werkstueck { get; set; }
        public string WerkstueckWare { get; set; } = "";
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

    /// <summary>Wie ein Licht des Modells leuchtet (extras „art“): Glut flackert orange bei der Arbeit, Fenster werfen Tageslicht, Lampen leuchten warm und gleichmäßig.</summary>
    private enum LichtArt { Fenster, Glut, Lampe }

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
        public List<(Light3D Licht, LichtArt Art, int AbStufe)> Lichter { get; } = new();
        /// <summary>Knoten, die erst ab einer Betriebsstufe da sind (Raum „Saege“, Türen dorthin; extras „stufe“): darunter unsichtbar.</summary>
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
        public bool ArbeitLaeuft { get; set; }
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
            spieler.GetAnimation("Arbeit").LoopMode = Animation.LoopModeEnum.Linear; // eine Schleife, solange gearbeitet wird (InnenModellAktualisieren)
            md.Animation = spieler;
        }

        foreach (var plan in md.Plaetze) // Werkstück-Anker zum Platz gleicher Nummer
        {
            if (wurzel.FindChild($"Werkstueck_{plan.Nr}", true, false) is not Node3D anker) continue;
            plan.WerkstueckAnker = anker;
            plan.Werkstueck = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.42f, 0.08f, 0.22f) },
                MaterialOverride = Material(Colors.Gray),
                Visible = false,
            };
            anker.AddChild(plan.Werkstueck);
        }
        md.Plaetze.Sort((x, y) => x.Nr.CompareTo(y.Nr));

        // Räume in der Reihenfolge der Raumleiste (Mitte, Maße und Wände stehen im Modell). Ein Raum mit „stufe“ (die Sägehalle ab 3) steht erst ab dieser Stufe in der Leiste.
        foreach (var (kennung, schluessel, name) in new[] { (RaumWerk, "Werk", WerkraumName(stil)), (RaumLager, "Lager", "Lager"), (RaumSaege, "Saege", "Sägehalle") })
            if (md.Blicke.TryGetValue(schluessel, out var blicke) && blicke.Count > 0)
            {
                blicke.Sort((x, y) => x.Rang.CompareTo(y.Rang));
                var raumKnoten = wurzel.FindChild("Raum_" + schluessel, true, false) as Node3D;
                var vorgabe = schluessel == "Saege" ? 3 : 1; // gilt, wenn die „extras“ fehlen
                var abStufe = raumKnoten is null ? vorgabe : ExtraZahl(raumKnoten, "stufe", vorgabe);
                if (raumKnoten is not null && abStufe > 1) md.StufenKnoten.Add((raumKnoten, abStufe));
                a.Raeume.Add(new Raum(kennung, name, Vector3.Zero, new Vector3(9, 3.6f, 6.5f),
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
                InnenKlickAnlegen(a, md, knoten, $"bedienung:{teile[1]}:{teile[2]}");
                break;
            case "Tuer" when teile.Length >= 3:
                InnenKlickAnlegen(a, md, knoten, $"tuer:{teile[1]}:{teile[2]}");
                if (ExtraZahl(knoten, "stufe", 1) is > 1 and var tuerStufe) md.StufenKnoten.Add((knoten, tuerStufe)); // die Tür zur Sägehalle gibt es erst mit ihr
                break;
            case "Spaene1" or "Spaene2" or "Spaene3":
                md.Spaene[name[^1] - '0'] = knoten;
                knoten.Visible = false; // bis zur Arbeit des Tages
                break;
            case "Blick" when teile.Length >= 3:
                if (!md.Blicke.TryGetValue(teile[1], out var liste)) md.Blicke[teile[1]] = liste = new List<Blickpunkt>();
                liste.Add(new Blickpunkt(teile[2], knoten));
                break;
            case "Arbeit" when teile.Length >= 3 && int.TryParse(teile[1], out var nr):
                md.Plaetze.Add(new ModellPlatz { Nr = nr, Bewegung = teile[2], Anker = knoten, Raum = RaumSchluesselVon(knoten),
                    AbStufe = ExtraZahl(knoten, "stufe", nr <= 2 ? 1 : nr - 1) });
                break;
            case "Lager" when teile.Length == 2 && int.TryParse(teile[1], out _):
                md.Lagerplaetze.Add(knoten);
                break;
            case "Licht" when teile.Length == 2:
                InnenLichtAnlegen(md, knoten);
                break;
        }
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

    /// <summary>Schlüssel des Raums, in dem der Knoten liegt: der Name des nächsten Vorfahren „Raum_&lt;Schlüssel&gt;“ („Werk“, „Lager“, „Saege“), sonst leer.</summary>
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
            foreach (var (licht, art, abStufe) in md.Lichter)
            {
                licht.Visible = stufe >= abStufe;
                licht.LightEnergy = art switch
                {
                    LichtArt.Glut => arbeitet ? 1.6f : 0.25f,
                    LichtArt.Lampe => 0.45f + 0.5f * (1f - _raumHell), // nachts heller
                    _ => 0.05f + 0.8f * _raumHell,
                };
            }
        }

        InnenSpaeneAktualisieren(md, b, zeit);
        if (md.Animation is { } animation)
        {
            var laeuft = arbeitet && !_simulation!.Takt.IstPausiert;
            if (laeuft != md.ArbeitLaeuft)
            {
                md.ArbeitLaeuft = laeuft;
                if (laeuft) animation.Play("Arbeit"); // nach einer Pause geht es an der Stelle weiter
                else animation.Pause();
            }
        }

        InnenArbeiterAktualisieren(a, md, b, stufe, arbeitszeit, arbeitet, delta);
        InnenLagerAktualisieren(a, md, b);
    }

    /// <summary>Hält die Animation „Arbeit“ an (beim Verlassen des Hauses), damit sie nicht im Hintergrund weiterläuft.</summary>
    private static void InnenModellAnhalten(ModellRaumDaten md)
    {
        if (md.Animation is null || !md.ArbeitLaeuft) return;
        md.ArbeitLaeuft = false;
        md.Animation.Pause();
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
        var arbeitsminuten = _simulation?.Wirtschaft?.Daten.Allgemein.Arbeitsminuten ?? 720;
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
            if (bewegung != arbeiter.Bewegung)
            {
                arbeiter.Bewegung = bewegung;
                BewegungSetzen(arbeiter.Figur, bewegung, (float)Mathf.Clamp(zeile.TempoFaktor, 0.5, 1.5));
            }
            if (da && (bewegt || arbeiter.Figur.Neu)) FigurBewegen(arbeiter.Figur, bewegt ? delta : 0); // in der Pause nur die erste Pose der neuen Bewegung setzen
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
                ((StandardMaterial3D)platz.Werkstueck.MaterialOverride).AlbedoColor = Warenfarbe(ware);
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
        return new LagerStapel { Wurzel = platz, Kisten = kisten, Stoff = stoff, Schild = schild, Klick = form, Koerper = koerper, Ware = ware };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Namen unter der Maus
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Der Text zum Schild an der Maus: erste Zeile der Name, darunter in kleiner Schrift, was ein Klick tut. Leer: nichts anzeigen.</summary>
    private (string Name, string Hinweis) InnenText(BetriebsAnsicht a, string tag)
    {
        var teile = tag.Split(':');
        var w = _simulation?.Wirtschaft;
        switch (teile[0])
        {
            case "bedienung" when teile.Length >= 3:
                return (teile[1], teile[2]) switch
                {
                    ("Personal", _) => ("Personalbuch", "Klick: Arbeiter einstellen, befördern, entlassen, Lohn und Prämie"),
                    ("Produktion", var ding) => (ding, "Klick: Waren herstellen und Arbeiter zuteilen"), // Amboss, Werkbank, Hobelbank, Leimtisch
                    ("Lager", "Bretter") => ("Bretterstapel", "Klick: Bestand und Zurückhalten"),
                    ("Lager", var ding) => (ding, "Klick: Bestand und Zurückhalten"),                  // Regal, Kisten
                    ("Karren", _) => ("Hoftor", "Klick: Karren des Hauses"),
                    ("Einrichten", _) => ("Werkzeugwand", "Klick: Einrichtung kaufen"),
                    _ => (teile[2], ""),
                };
            case "tuer" when teile.Length >= 3:
                return (teile[2] switch { "Lager" => "Tür zum Lager", "Saege" => "Tür zur Sägehalle", _ => "Tür zum Werkraum" }, "Klick: Raum wechseln");
            case "einrichtung" when teile.Length >= 2:
                var stueck = w?.Werkstaetten.Einrichtungsliste(a.Betrieb).FirstOrDefault(e => e.Kennung == teile[1]);
                return (stueck?.Name ?? teile[1], stueck is null ? "" : "Eingerichtet: " + stueck.Wirkung);
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
