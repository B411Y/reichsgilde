using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die Innenräume (Dioramen) der Werkstätten – zweiter Teil der Stadtansicht.
///
/// Jede Werkstatt in der Stadt hat einen Knoten „Innen", der nur in der Innenansicht sichtbar ist.
/// Darin steht die Einrichtung ihres Berufs aus einfachen Grundformen (Greybox), das Lager als
/// Kistenstapel an der Nordwand und die Belegschaft an ihren Arbeitsplätzen.
///
/// Gemeinsames Gerüst für alle Berufe:
///   • Arbeitsplätze: der Reihe nach besetzt – die ersten für Stufe 1, dahinter die an den Stücken
///     späterer Stufen. Eine ausgebaute Werkstatt hat mehr Plätze, also stehen dort auch mehr Leute.
///   • Stufenteile: Einrichtung, die erst ab Stufe 2 oder 3 dasteht (Drechselbank, Würfeltisch, Tresor …).
///   • Feuer: Glut und Flammen, die bei der Arbeit hell leuchten (oder zu ihrer eigenen Zeit, z. B. zur Messe).
///   • Bewegte Teile: Schleifstein, Drechselbank, Sägegatter laufen, solange gearbeitet wird.
///   • Werkstück und Produktfarbe: Was auf der Werkbank liegt oder im Kessel siedet, hat die Farbe der Ware.
///   • Zustand: je Beruf eine kleine Funktion, die Zahlen aus der Simulation zeigt – Gäste in der Schänke,
///     Gläubige zur Messe, die Kasse der Wechselstube, Diebesgut und Aufträge im Versteck, Gefangene der Wache.
///
/// Die Ansicht liest nur – sie ruft ausschließlich Abfragen des Simulationskerns auf und verändert nichts.
/// Grundriss (Blick nach Norden, Mitte = 0): innen x −4,6 … 4,6 (West–Ost), z −3,6 … 3,6 (Nord–Süd);
/// an der Nordwand (z ≈ −2,8) steht das Lager, die Einrichtung verteilt sich auf den Rest.
/// </summary>
public partial class Stadtansicht
{
    // Blickrichtung einer Figur (Drehung um die Hochachse): 0 = nach Norden (−Z), dann gegen den Uhrzeigersinn.
    private const float Nord = 0, West = Mathf.Pi / 2, Sued = Mathf.Pi, Ost = -Mathf.Pi / 2;

    /// <summary>Was eine Figur gerade tut – bestimmt, wie sich ihr Arm bewegt.</summary>
    private enum Bewegung { Haemmern, Meisseln, Saegen, Ruehren, Schreiben, Schenken, Beten, Stehen, Spielen }

    /// <summary>Was eine Figur in der Hand hält.</summary>
    private enum Werkzeug { Keins, Hammer, Saege, Meissel, Kelle, Feder, Krug, Speer, Schwert, Beutel, Laute }

    /// <summary>Kopfbedeckung – Tracht des Berufs.</summary>
    private enum Kopfputz { Keiner, Helm, Kapuze, Barett, Kappe, Mitra }

    /// <summary>Ein Platz im Raum: Ort relativ zur Mitte, Blickrichtung, Tätigkeit, Werkzeug, sitzend oder stehend.</summary>
    private sealed record Arbeitsplatz(Vector3 Ort, float Drehung, Bewegung Art, Werkzeug Werkzeug, bool Sitzt = false);

    /// <summary>Eine Glut oder Flamme: hell, wenn <see cref="An"/> gilt (ohne Bedingung: solange gearbeitet wird), sonst schwach.</summary>
    private sealed record Feuerstelle(OmniLight3D? Licht, StandardMaterial3D? Glut, float LichtAn, float LichtAus, float GlutAn, float GlutAus, Func<bool>? An = null);

    /// <summary>Ein Teil, das sich bei der Arbeit dreht (<see cref="Hub"/> = 0) oder um <see cref="Hub"/> auf und ab geht.</summary>
    private sealed record Bewegtteil(Node3D Knoten, Vector3 Achse, float Tempo, Vector3 Hub, Vector3 Ruhe);

    // ── Farben der Einrichtung ──────────────────────────────────────────────
    private static readonly Color Lehm = new(0.34f, 0.29f, 0.24f);
    private static readonly Color Dielen = new(0.42f, 0.31f, 0.21f);
    private static readonly Color Steinboden = new(0.48f, 0.46f, 0.43f);
    private static readonly Color HellesHolz = new(0.62f, 0.48f, 0.30f);
    private static readonly Color DunklesHolz = new(0.26f, 0.18f, 0.11f);
    private static readonly Color Leinen = new(0.92f, 0.90f, 0.84f);
    private static readonly Color Pergament = new(0.86f, 0.80f, 0.62f);
    private static readonly Color Stroh = new(0.74f, 0.62f, 0.34f);
    private static readonly Color Stadtrot = new(0.72f, 0.12f, 0.12f);
    private static readonly Color Haut = new(0.84f, 0.67f, 0.54f);
    private static readonly Color Bartfarbe = new(0.40f, 0.26f, 0.14f);

    /// <summary>Gäste, Gläubige und Kunden kommen aus allen Völkern der Stadt: Mensch, Zwerg (mit Bart), Elb, Halbling.</summary>
    private static readonly (float Groesse, float Breite, bool Bart)[] Voelker = { (1f, 1f, false), (0.78f, 1.25f, true), (1.1f, 0.88f, false), (0.7f, 1.05f, false) };

    private static readonly Color[] Gewandfarben =
    {
        new(0.45f, 0.20f, 0.18f), new(0.25f, 0.33f, 0.48f), new(0.38f, 0.42f, 0.24f), new(0.55f, 0.45f, 0.30f),
        new(0.35f, 0.26f, 0.40f), new(0.62f, 0.58f, 0.50f), new(0.22f, 0.36f, 0.34f), new(0.50f, 0.32f, 0.16f),
    };

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Baut den Innenraum: Boden, Laterne und die Einrichtung des Berufs. Alles hängt am Knoten „Innen",
    /// der erst beim Betreten sichtbar wird.
    /// </summary>
    private void Innenraum(BetriebsAnsicht ansicht, Werkstil stil, float breite, float tiefe, float dicke)
    {
        if (InnenModellDatei(stil) is { } datei && Modell(datei) is not null)
        {
            InnenModellBauen(ansicht, stil, datei); // Räume aus einem Blender-Modell (Stadtansicht.Innenmodell.cs)
            return;
        }
        var p = ansicht.Mitte;
        // Die Räume liegen unter der Stadt (Raumansicht, siehe Stadtansicht.Raeume.cs).
        var innen = new Node3D { Name = "Innen", Visible = false, Position = new Vector3(0, RaumTiefe, 0) };
        AddChild(innen);
        ansicht.Innen = innen;
        ansicht.Stil = stil;

        var boden = stil switch
        {
            Werkstil.Tischlerei or Werkstil.Schaenke or Werkstil.Bank => Dielen,
            Werkstil.Maurerei or Werkstil.Kirche or Werkstil.Wache or Werkstil.Siederei => Steinboden,
            Werkstil.Diebesnest => new Color(0.24f, 0.21f, 0.18f),
            _ => Lehm, // gestampfter Lehm in Gießerei und Tinkturei
        };
        Quader(p + new Vector3(0, 0.05f, 0), new Vector3(breite - 2 * dicke, 0.1f, tiefe - 2 * dicke), Material(boden), innen);

        // Laterne unter dem Dachbalken – damit man auch nachts etwas sieht (im Diebesnest nur ein trüber Schein)
        innen.AddChild(new OmniLight3D
        {
            LightColor = new Color(1f, 0.85f, 0.6f),
            OmniRange = 9,
            LightEnergy = stil == Werkstil.Diebesnest ? 0.25f : 0.4f,
            Position = p + new Vector3(-1, 4.5f, 0),
        });

        switch (stil)
        {
            case Werkstil.Tischlerei: TischlereiEinrichten(ansicht); break;
            case Werkstil.Maurerei: MaurereiEinrichten(ansicht); break;
            case Werkstil.Tinkturei: TinktureiEinrichten(ansicht); break;
            case Werkstil.Schaenke: SchaenkeEinrichten(ansicht); break;
            case Werkstil.Kirche: KircheEinrichten(ansicht); break;
            case Werkstil.Diebesnest: DiebesnestEinrichten(ansicht); break;
            case Werkstil.Wache: WacheEinrichten(ansicht); break;
            case Werkstil.Bank: BankEinrichten(ansicht); break;
            case Werkstil.Siederei: SiedereiEinrichten(ansicht); break;
            default: GiessereiEinrichten(ansicht); break;
        }
        WerkstattRaeumeAnlegen(ansicht, stil, breite - 2 * dicke, tiefe - 2 * dicke);
    }

    // ── Bausteine (Orte relativ zur Mitte der Werkstatt) ────────────────────

    private MeshInstance3D Teil(InnenAnsicht a, Vector3 ort, Vector3 groesse, StandardMaterial3D m, Node3D? eltern = null) =>
        Quader(a.Mitte + ort, groesse, m, eltern ?? a.Innen);

    /// <summary>Zylinder oder Kegelstumpf; <paramref name="drehung"/> in Grad (z. B. (0, 0, 90) = liegend entlang X).</summary>
    private MeshInstance3D Rund(InnenAnsicht a, Vector3 ort, float radius, float hoehe, StandardMaterial3D m, Node3D? eltern = null,
        float? obenRadius = null, Vector3? drehung = null, int segmente = 14)
    {
        var teil = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = obenRadius ?? radius, BottomRadius = radius, Height = hoehe, RadialSegments = segmente },
            MaterialOverride = m,
            Position = a.Mitte + ort,
            RotationDegrees = drehung ?? Vector3.Zero,
        };
        (eltern ?? a.Innen!).AddChild(teil);
        return teil;
    }

    private MeshInstance3D Kugel(InnenAnsicht a, Vector3 ort, float radius, StandardMaterial3D m, Node3D? eltern = null, float? hoehe = null)
    {
        var teil = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = radius, Height = hoehe ?? radius * 2, RadialSegments = 14, Rings = 7 },
            MaterialOverride = m,
            Position = a.Mitte + ort,
        };
        (eltern ?? a.Innen!).AddChild(teil);
        return teil;
    }

    /// <summary>Ein Balken von einem Punkt zum anderen (Beine eines Dreibeins, schräge Streben).</summary>
    private MeshInstance3D Stab(InnenAnsicht a, Vector3 von, Vector3 bis, float dicke, StandardMaterial3D m, Node3D? eltern = null)
    {
        var richtung = bis - von;
        var teil = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(dicke, richtung.Length(), dicke) },
            MaterialOverride = m,
            Position = a.Mitte + (von + bis) / 2,
        };
        // Drehung, die die Hochachse des Balkens auf die gewünschte Richtung legt
        teil.Quaternion = new Quaternion(Vector3.Up, richtung.Normalized());
        (eltern ?? a.Innen!).AddChild(teil);
        return teil;
    }

    /// <summary>Gruppe für Einrichtung, die erst ab einer Gebäudestufe dasteht.</summary>
    private Node3D Stufenteil(InnenAnsicht a, int abStufe)
    {
        var gruppe = new Node3D { Name = $"AbStufe{abStufe}", Visible = false };
        a.Innen!.AddChild(gruppe);
        a.Stufenteile.Add((gruppe, abStufe));
        return gruppe;
    }

    private static StandardMaterial3D Leuchtend(Color farbe, Color glut)
    {
        var m = Material(farbe);
        m.EmissionEnabled = true;
        m.Emission = glut;
        m.EmissionEnergyMultiplier = 0;
        return m;
    }

    private static StandardMaterial3D Metall(Color farbe)
    {
        var m = Material(farbe);
        m.Metallic = 0.6f;
        m.Roughness = 0.4f;
        return m;
    }

    /// <summary>Ein Licht für eine Glut: hell bei der Arbeit (oder wenn <paramref name="an"/> gilt), sonst schwach.</summary>
    private void FeuerAnlegen(InnenAnsicht a, Vector3 ort, StandardMaterial3D? glut, float lichtAn, float lichtAus = 0.2f,
        float glutAn = 1.6f, float glutAus = 0.25f, Func<bool>? an = null, float reichweite = 7, bool schatten = false)
    {
        var licht = new OmniLight3D
        {
            LightColor = new Color(1f, 0.55f, 0.22f), OmniRange = reichweite, LightEnergy = 0, ShadowEnabled = schatten, Position = a.Mitte + ort,
            LightIndirectEnergy = GlutGi, // gedämpft in der globalen Beleuchtung, sonst färbt die Glut den ganzen Raum rot (Stadtansicht.GlobaleBeleuchtung.cs)
        };
        a.Innen!.AddChild(licht);
        a.Feuer.Add(new Feuerstelle(licht, glut, lichtAn, lichtAus, glutAn, glutAus, an));
    }

    private void Infotafel(InnenAnsicht a, Vector3 ort) => a.Infotafel = Beschriftung("", a.Mitte + ort, 38, a.Innen);

    private void Plaetze(BetriebsAnsicht a, params Arbeitsplatz[] plaetze) => a.Plaetze.AddRange(plaetze);

    // ═════════════════════════════════════════════════════════════════════════
    //  Die Einrichtungen der Berufe
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gießerei: Esse mit Rauchfang und Blasebalg (Ost), Amboss (Mitte), Werkbank (West), Wassertrog.
    /// Stufe 2 (Schmiede): Schleifbock. Stufe 3 (Goldschmiede): Feinwerktisch mit Goldteilen.
    /// </summary>
    private void GiessereiEinrichten(BetriebsAnsicht a)
    {
        var p = a.Mitte;
        var stein = Material(Mauerstein);
        var holz = Material(Holz);
        var eisen = Metall(new Color(0.22f, 0.22f, 0.24f));

        // Esse: gemauerter Herd, glühendes Kohlebett, Rauchfang bis unters Dach, Blasebalg
        var o = new Vector3(3.6f, 0, 0.2f);
        Teil(a, o + new Vector3(0, 0.6f, 0), new Vector3(1.8f, 1.2f, 2.2f), stein);
        var kohle = Leuchtend(new Color(0.15f, 0.08f, 0.05f), new Color(1f, 0.4f, 0.08f));
        Teil(a, o + new Vector3(0, 1.23f, 0), new Vector3(1.3f, 0.08f, 1.6f), kohle);
        // Rauchfang über der Esse bis unter die Decke
        a.Innen!.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.55f, BottomRadius = 1.35f, Height = 2.4f, RadialSegments = 4 },
            MaterialOverride = stein,
            Position = p + o + new Vector3(0, 3.4f, 0),
            RotationDegrees = new Vector3(0, 45, 0),
        });
        Teil(a, o + new Vector3(0, 5.3f, 0), new Vector3(0.9f, 1.4f, 0.9f), stein);
        Teil(a, o + new Vector3(0.1f, 0.45f, 1.7f), new Vector3(0.9f, 0.5f, 1.0f), Material(new Color(0.42f, 0.30f, 0.20f))); // Blasebalg
        FeuerAnlegen(a, o + new Vector3(-0.6f, 2f, 0), kohle, lichtAn: 2.5f, lichtAus: 0.3f, glutAn: 1.6f, glutAus: 0.3f, reichweite: 8, schatten: true);

        // Amboss auf einem Holzklotz
        var amboss = new Vector3(0.8f, 0, -0.2f);
        Teil(a, amboss + new Vector3(0, 0.35f, 0), new Vector3(0.7f, 0.7f, 0.7f), holz);
        Teil(a, amboss + new Vector3(0, 0.85f, 0), new Vector3(1.1f, 0.3f, 0.45f), eisen);

        // Wassertrog zum Abschrecken
        Teil(a, new Vector3(2.0f, 0.35f, 2.6f), new Vector3(1.6f, 0.7f, 0.8f), holz);
        Teil(a, new Vector3(2.0f, 0.66f, 2.6f), new Vector3(1.4f, 0.04f, 0.6f), Material(new Color(0.2f, 0.3f, 0.36f)));

        // Werkbank mit Werkzeug und Gussformen
        var w = new Vector3(-3.9f, 0, 0.4f);
        Teil(a, w + new Vector3(0, 0.5f, 0), new Vector3(1.0f, 1.0f, 3.0f), holz);
        Teil(a, w + new Vector3(0.1f, 1.06f, -0.8f), new Vector3(0.5f, 0.12f, 0.7f), eisen);
        Teil(a, w + new Vector3(0.1f, 1.08f, 0.5f), new Vector3(0.6f, 0.16f, 0.4f), Material(new Color(0.55f, 0.45f, 0.35f)));

        // Stufe 2: Schleifbock mit Schleifstein, der bei der Arbeit läuft
        var s2 = Stufenteil(a, 2);
        Teil(a, new Vector3(-3.8f, 0.4f, 2.8f), new Vector3(0.5f, 0.7f, 0.3f), holz, s2);
        var scheibe = Rund(a, new Vector3(-3.8f, 1.0f, 2.8f), 0.4f, 0.12f, Material(Mauerstein), s2, drehung: new Vector3(0, 0, 90));
        a.Bewegt.Add(new Bewegtteil(scheibe, Vector3.Up, 5, Vector3.Zero, scheibe.Position));

        // Stufe 3: Feinwerktisch der Goldschmiede
        var s3 = Stufenteil(a, 3);
        var gold = Metall(new Color(0.92f, 0.75f, 0.30f));
        Teil(a, new Vector3(-1.8f, 0.45f, 3.0f), new Vector3(1.4f, 0.9f, 0.6f), holz, s3);
        foreach (var dx in new[] { -0.4f, -0.1f, 0.3f })
            Teil(a, new Vector3(-1.8f + dx, 0.95f, 2.95f), new Vector3(0.14f, 0.05f, 0.14f), gold, s3);

        Plaetze(a,
            new(new Vector3(0.8f, 0, 0.55f), Nord, Bewegung.Haemmern, Werkzeug.Hammer),   // am Amboss
            new(new Vector3(2.2f, 0, 0.2f), Ost, Bewegung.Haemmern, Werkzeug.Hammer),     // an der Esse
            new(new Vector3(-2.9f, 0, 0.4f), West, Bewegung.Haemmern, Werkzeug.Hammer),   // an der Werkbank
            new(new Vector3(-1.8f, 0, 2.25f), Sued, Bewegung.Meisseln, Werkzeug.Meissel)); // am Feinwerktisch
    }

    /// <summary>
    /// Tischlerei: Sägebock mit Stamm (Mitte), Hobelbank (West) mit dem Werkstück, Holzlager (Ost).
    /// Stufe 2 (Drechslerei): Drechselbank. Stufe 3 (Sägewerk): Sägegatter, dessen Blatt auf und ab geht.
    /// </summary>
    private void TischlereiEinrichten(BetriebsAnsicht a)
    {
        var holz = Material(Holz);
        var hell = Material(HellesHolz);
        var dunkel = Material(DunklesHolz);
        var stahl = Metall(new Color(0.62f, 0.64f, 0.68f));

        // Hobelbank mit Brett, Hobel und dem Werkstück in Arbeit
        Teil(a, new Vector3(-3.9f, 0.5f, 0.4f), new Vector3(1.0f, 1.0f, 3.0f), holz);
        Teil(a, new Vector3(-3.85f, 1.04f, 0.6f), new Vector3(0.45f, 0.08f, 1.6f), hell);
        Teil(a, new Vector3(-3.85f, 1.14f, 1.2f), new Vector3(0.2f, 0.12f, 0.4f), dunkel);
        a.Werkstueck = Teil(a, new Vector3(-3.85f, 1.25f, -0.6f), new Vector3(0.55f, 0.35f, 0.5f), Material(HellesHolz));

        // Sägebock: zwei Böcke aus schrägen Beinen, darauf ein Stamm; Sägespäne darunter
        foreach (var x in new[] { -0.2f, 1.4f })
        {
            Stab(a, new Vector3(x, 0.1f, -0.35f), new Vector3(x, 0.9f, 0.1f), 0.12f, holz);
            Stab(a, new Vector3(x, 0.1f, 0.55f), new Vector3(x, 0.9f, 0.1f), 0.12f, holz);
        }
        Rund(a, new Vector3(0.6f, 1.1f, 0.1f), 0.24f, 2.4f, hell, drehung: new Vector3(0, 0, 90));
        Rund(a, new Vector3(0.6f, 0.11f, 0.5f), 0.6f, 0.02f, Material(new Color(0.80f, 0.70f, 0.50f)));

        // Holzlager an der Ostwand: liegende Stämme, nach oben weniger
        for (var lage = 0; lage < 3; lage++)
        {
            var anzahl = 3 - lage;
            for (var i = 0; i < anzahl; i++)
                Rund(a, new Vector3(3.8f + (i - (anzahl - 1) / 2f) * 0.48f, 0.34f + lage * 0.42f, 0.6f), 0.22f, 2.4f, holz, drehung: new Vector3(90, 0, 0));
        }

        // Stufe 2: Drechselbank – die Spindel dreht sich bei der Arbeit
        var s2 = Stufenteil(a, 2);
        Teil(a, new Vector3(2.8f, 0.75f, 2.9f), new Vector3(1.8f, 0.12f, 0.4f), holz, s2);
        foreach (var dx in new[] { -0.75f, 0.75f })
            Teil(a, new Vector3(2.8f + dx, 0.4f, 2.9f), new Vector3(0.15f, 0.7f, 0.35f), dunkel, s2);
        Teil(a, new Vector3(2.0f, 1.0f, 2.9f), new Vector3(0.3f, 0.4f, 0.35f), dunkel, s2);
        var spindel = Rund(a, new Vector3(2.85f, 1.05f, 2.9f), 0.12f, 1.3f, hell, s2, drehung: new Vector3(0, 0, 90), segmente: 6);
        a.Bewegt.Add(new Bewegtteil(spindel, Vector3.Up, 12, Vector3.Zero, spindel.Position));

        // Stufe 3: Sägegatter – zwei Pfosten, ein Querbalken und ein Blatt, das auf und ab geht
        var s3 = Stufenteil(a, 3);
        foreach (var dx in new[] { -0.9f, 0.9f })
            Teil(a, new Vector3(-2.0f + dx, 1.4f, 2.9f), new Vector3(0.18f, 2.8f, 0.18f), holz, s3);
        Teil(a, new Vector3(-2.0f, 2.85f, 2.9f), new Vector3(2.0f, 0.18f, 0.25f), holz, s3);
        Rund(a, new Vector3(-2.0f, 0.9f, 2.9f), 0.25f, 1.6f, hell, s3, drehung: new Vector3(0, 0, 90));
        var blatt = Teil(a, new Vector3(-2.0f, 1.7f, 2.9f), new Vector3(0.05f, 1.3f, 0.04f), stahl, s3);
        a.Bewegt.Add(new Bewegtteil(blatt, Vector3.Zero, 7, new Vector3(0, 0.3f, 0), blatt.Position));

        Plaetze(a,
            new(new Vector3(0.6f, 0, 0.85f), Nord, Bewegung.Saegen, Werkzeug.Saege),    // am Sägebock
            new(new Vector3(-2.9f, 0, 0.6f), West, Bewegung.Saegen, Werkzeug.Keins),     // hobelt an der Hobelbank
            new(new Vector3(2.8f, 0, 2.15f), Sued, Bewegung.Meisseln, Werkzeug.Meissel), // an der Drechselbank
            new(new Vector3(-2.0f, 0, 2.05f), Sued, Bewegung.Stehen, Werkzeug.Keins));   // am Sägegatter
    }

    /// <summary>
    /// Maurerei: Werkbock mit dem Werkstein (Mitte), Schleifstein (West), Quaderstapel (Ost).
    /// Stufe 2 (Steinschneiderei): Dreibein mit Flaschenzug. Stufe 3 (Steinmetzhütte): Steinfigur in Arbeit.
    /// </summary>
    private void MaurereiEinrichten(BetriebsAnsicht a)
    {
        var holz = Material(Holz);
        var stein = Material(Bruchstein);
        var grau = Material(Mauerstein);

        Teil(a, new Vector3(0.6f, 0.4f, 0.2f), new Vector3(1.0f, 0.7f, 1.0f), holz);                         // Werkbock
        a.Werkstueck = Teil(a, new Vector3(0.6f, 1.05f, 0.2f), new Vector3(0.85f, 0.6f, 0.75f), Material(Bruchstein));
        Rund(a, new Vector3(0.6f, 0.11f, 0.7f), 0.8f, 0.02f, Material(new Color(0.75f, 0.73f, 0.69f)));      // Steinstaub

        // Schleifstein im Holzgestell
        Teil(a, new Vector3(-3.6f, 0.4f, 0.6f), new Vector3(0.7f, 0.7f, 0.5f), holz);
        var schleifstein = Rund(a, new Vector3(-3.6f, 1.15f, 0.6f), 0.55f, 0.2f, grau, drehung: new Vector3(0, 0, 90));
        a.Bewegt.Add(new Bewegtteil(schleifstein, Vector3.Up, 4, Vector3.Zero, schleifstein.Position));

        // Quaderstapel an der Ostwand
        Teil(a, new Vector3(3.8f, 0.45f, 0.6f), new Vector3(1.2f, 0.7f, 1.6f), stein);
        Teil(a, new Vector3(3.8f, 1.1f, 0.4f), new Vector3(1.0f, 0.6f, 1.0f), stein);
        Teil(a, new Vector3(3.9f, 0.3f, 2.2f), new Vector3(0.9f, 0.4f, 0.9f), grau);

        // Stufe 2: Dreibein mit Flaschenzug – der Block hebt und senkt sich bei der Arbeit
        var s2 = Stufenteil(a, 2);
        var spitze = new Vector3(2.9f, 3.2f, 2.6f);
        foreach (var fuss in new[] { new Vector3(2.2f, 0.1f, 2.0f), new Vector3(3.7f, 0.1f, 2.1f), new Vector3(2.9f, 0.1f, 3.4f) })
            Stab(a, fuss, spitze, 0.14f, holz, s2);
        Teil(a, new Vector3(2.9f, 2.5f, 2.6f), new Vector3(0.04f, 1.4f, 0.04f), Material(Stroh), s2); // Seil
        var block = Teil(a, new Vector3(2.9f, 1.5f, 2.6f), new Vector3(0.7f, 0.5f, 0.7f), stein, s2);
        a.Bewegt.Add(new Bewegtteil(block, Vector3.Zero, 1.2f, new Vector3(0, 0.35f, 0), block.Position));

        // Stufe 3: Steinfigur auf dem Sockel
        var s3 = Stufenteil(a, 3);
        Teil(a, new Vector3(-2.0f, 0.4f, 2.9f), new Vector3(0.9f, 0.6f, 0.9f), grau, s3);
        s3.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.3f, Height = 1.4f }, MaterialOverride = Material(new Color(0.78f, 0.76f, 0.72f)), Position = a.Mitte + new Vector3(-2.0f, 1.4f, 2.9f) });
        Kugel(a, new Vector3(-2.0f, 2.25f, 2.9f), 0.2f, Material(new Color(0.78f, 0.76f, 0.72f)), s3);

        Plaetze(a,
            new(new Vector3(0.6f, 0, 1.05f), Nord, Bewegung.Meisseln, Werkzeug.Meissel), // am Werkstein
            new(new Vector3(-2.75f, 0, 0.6f), West, Bewegung.Ruehren, Werkzeug.Keins),   // dreht den Schleifstein
            new(new Vector3(2.0f, 0, 2.6f), Ost, Bewegung.Schenken, Werkzeug.Keins),      // zieht am Flaschenzug
            new(new Vector3(-2.0f, 0, 2.05f), Sued, Bewegung.Meisseln, Werkzeug.Meissel)); // an der Steinfigur
    }

    /// <summary>
    /// Tinkturei: Kessel über dem Feuer (Ost) – der Sud hat die Farbe der Ware –, Regal mit Flaschen (West),
    /// Mörsertisch (Mitte), Kräuterbündel unter dem Dachbalken.
    /// Stufe 2 (Kräuterladen): Destille. Stufe 3 (Alchimistenstube): Athanor, der Ofen der Alchimisten.
    /// </summary>
    private void TinktureiEinrichten(BetriebsAnsicht a)
    {
        var holz = Material(Holz);
        var stein = Material(Mauerstein);
        var eisen = Metall(new Color(0.18f, 0.18f, 0.20f));

        // Kessel über dem Feuer
        var k = new Vector3(3.5f, 0, 0.4f);
        Rund(a, k + new Vector3(0, 0.3f, 0), 0.75f, 0.4f, stein, segmente: 10);
        var glut = Leuchtend(new Color(0.2f, 0.08f, 0.04f), new Color(1f, 0.45f, 0.1f));
        Rund(a, k + new Vector3(0, 0.52f, 0), 0.6f, 0.06f, glut);
        Rund(a, k + new Vector3(0, 0.95f, 0), 0.55f, 0.7f, eisen, obenRadius: 0.5f);
        var sud = Leuchtend(Kraut, Kraut);
        Rund(a, k + new Vector3(0, 1.29f, 0), 0.46f, 0.03f, sud);
        a.Produktfarben.Add(sud);
        a.Feuer.Add(new Feuerstelle(null, sud, 0, 0, 0.7f, 0.1f));
        FeuerAnlegen(a, k + new Vector3(-0.8f, 0.9f, 0), glut, lichtAn: 1.6f, glutAn: 1.8f, reichweite: 6);

        // Regal mit Flaschen an der Westwand
        Teil(a, new Vector3(-4.38f, 1.2f, 0.4f), new Vector3(0.08f, 2.2f, 2.2f), holz);
        var flaschenfarben = new[] { new Color(0.30f, 0.52f, 0.25f), new Color(0.55f, 0.20f, 0.18f), new Color(0.25f, 0.35f, 0.60f), new Color(0.80f, 0.68f, 0.30f), new Color(0.45f, 0.30f, 0.55f) };
        foreach (var (y, reihe) in new[] { (0.5f, 0), (1.2f, 1), (1.9f, 2) })
        {
            Teil(a, new Vector3(-4.15f, y, 0.4f), new Vector3(0.45f, 0.06f, 2.2f), holz);
            for (var i = 0; i < 5; i++)
                Rund(a, new Vector3(-4.15f, y + 0.16f, -0.4f + i * 0.4f), 0.07f, 0.26f, Material(flaschenfarben[(i + reihe * 2) % flaschenfarben.Length]), segmente: 8);
        }

        // Mörsertisch mit Kräutern
        Teil(a, new Vector3(0, 0.45f, 0.5f), new Vector3(1.4f, 0.9f, 0.8f), holz);
        Rund(a, new Vector3(0.2f, 0.99f, 0.4f), 0.16f, 0.18f, stein, obenRadius: 0.2f);
        Teil(a, new Vector3(-0.35f, 0.95f, 0.5f), new Vector3(0.35f, 0.1f, 0.3f), Material(Kraut));

        // Kräuterbündel unter einem Balken
        Teil(a, new Vector3(-0.5f, 4.2f, -1.4f), new Vector3(6.5f, 0.15f, 0.15f), holz);
        for (var i = 0; i < 7; i++)
            Teil(a, new Vector3(-3.2f + i * 0.9f, 3.85f, -1.4f), new Vector3(0.22f, 0.55f, 0.22f), Material(i % 2 == 0 ? Kraut : new Color(0.42f, 0.48f, 0.22f)));

        // Stufe 2: Destille – Glaskolben mit Rohr und einer kleinen Vorlage in der Farbe der Ware
        var s2 = Stufenteil(a, 2);
        Teil(a, new Vector3(-3.8f, 0.45f, 2.7f), new Vector3(0.8f, 0.9f, 0.9f), holz, s2);
        Kugel(a, new Vector3(-3.8f, 1.18f, 2.8f), 0.28f, Material(new Color(0.70f, 0.82f, 0.86f)), s2);
        Stab(a, new Vector3(-3.8f, 1.45f, 2.8f), new Vector3(-3.55f, 1.05f, 2.4f), 0.05f, Material(new Color(0.70f, 0.82f, 0.86f)), s2);
        var vorlage = Leuchtend(Kraut, Kraut);
        Kugel(a, new Vector3(-3.55f, 1.0f, 2.4f), 0.12f, vorlage, s2);
        a.Produktfarben.Add(vorlage);
        a.Feuer.Add(new Feuerstelle(null, vorlage, 0, 0, 0.5f, 0.05f));

        // Stufe 3: Athanor – runder Ofenturm mit glühendem Schauloch
        var s3 = Stufenteil(a, 3);
        var athanor = new Vector3(3.95f, 0, -2.5f);
        Rund(a, athanor + new Vector3(0, 1.0f, 0), 0.55f, 2.0f, Material(Ziegel), s3);
        Kugel(a, athanor + new Vector3(0, 2.0f, 0), 0.55f, Material(Ziegel), s3, hoehe: 0.8f);
        var ofenglut = Leuchtend(new Color(0.25f, 0.08f, 0.04f), new Color(1f, 0.5f, 0.15f));
        Teil(a, athanor + new Vector3(-0.53f, 0.6f, 0), new Vector3(0.06f, 0.3f, 0.35f), ofenglut, s3);
        a.Feuer.Add(new Feuerstelle(null, ofenglut, 0, 0, 2.0f, 0.4f));

        Plaetze(a,
            new(new Vector3(2.55f, 0, 0.4f), Ost, Bewegung.Ruehren, Werkzeug.Kelle),         // rührt im Kessel
            new(new Vector3(0, 0, 1.25f), Nord, Bewegung.Meisseln, Werkzeug.Meissel),        // am Mörser
            new(new Vector3(-2.95f, 0, 2.75f), West, Bewegung.Schreiben, Werkzeug.Keins),    // an der Destille
            new(new Vector3(3.1f, 0, -1.55f), Ost / 2, Bewegung.Ruehren, Werkzeug.Kelle));   // am Athanor
    }

    /// <summary>
    /// Schänke: Theke mit Krügen (Ost), Fässer, Herd (Nordost), zwei Tische mit Bänken.
    /// Stufe 2 (Wirtshaus): Würfeltisch. Stufe 3 (Gasthaus): runder Stammtisch mit Stadtwimpel.
    /// Zustand: Gäste der letzten Stunde (oder die erwarteten, solange noch nichts gezählt ist) sitzen an den
    /// Tischen, Abgewiesene stehen an der Tür, abends spielt der gebuchte Barde.
    /// </summary>
    private void SchaenkeEinrichten(BetriebsAnsicht a)
    {
        var holz = Material(Holz);
        var dunkel = Material(DunklesHolz);
        var zinn = Metall(new Color(0.60f, 0.60f, 0.62f));
        var stein = Material(Mauerstein);

        // Theke mit Krügen, Fässer dahinter
        Teil(a, new Vector3(2.9f, 0.55f, 0.9f), new Vector3(0.7f, 1.1f, 2.8f), holz);
        Teil(a, new Vector3(2.85f, 1.13f, 0.9f), new Vector3(0.85f, 0.06f, 3.0f), dunkel);
        foreach (var z in new[] { 0.0f, 0.5f, 1.7f })
            Rund(a, new Vector3(2.8f, 1.26f, z), 0.08f, 0.2f, zinn, segmente: 8);
        foreach (var z in new[] { -0.5f, 2.5f, 3.1f })
            Rund(a, new Vector3(4.15f, 0.55f, z), 0.32f, 0.9f, holz, segmente: 10);

        // Herd in der Nordostecke: Glut, wenn die Schänke arbeitet oder ausschenkt
        Teil(a, new Vector3(3.9f, 0.45f, -2.7f), new Vector3(1.2f, 0.8f, 1.0f), stein);
        var herdglut = Leuchtend(new Color(0.2f, 0.08f, 0.04f), new Color(1f, 0.45f, 0.1f));
        Teil(a, new Vector3(3.9f, 0.88f, -2.6f), new Vector3(0.8f, 0.08f, 0.6f), herdglut);
        Rund(a, new Vector3(3.9f, 1.1f, -2.75f), 0.3f, 0.4f, Metall(new Color(0.18f, 0.18f, 0.2f)));
        var herdAn = new bool[1]; // setzt der Zustand (arbeitet oder schenkt aus), liest das Herdfeuer
        FeuerAnlegen(a, new Vector3(3.3f, 1.4f, -2.2f), herdglut, lichtAn: 1.4f, an: () => herdAn[0], reichweite: 6);

        // Zwei Tische mit Bänken
        foreach (var (x, z) in new[] { (-1.0f, -0.6f), (0.6f, 1.9f) })
        {
            Teil(a, new Vector3(x, 0.8f, z), new Vector3(2.0f, 0.1f, 0.9f), holz);
            Teil(a, new Vector3(x, 0.4f, z), new Vector3(1.4f, 0.7f, 0.3f), dunkel);
            foreach (var dz in new[] { -0.7f, 0.7f })
            {
                Teil(a, new Vector3(x, 0.45f, z + dz), new Vector3(2.0f, 0.08f, 0.35f), holz);
                Teil(a, new Vector3(x, 0.22f, z + dz), new Vector3(1.8f, 0.38f, 0.12f), dunkel);
            }
            Rund(a, new Vector3(x + 0.5f, 0.93f, z), 0.07f, 0.16f, zinn, segmente: 8);
        }

        // Stufe 2: Würfeltisch mit zwei Würfeln
        var s2 = Stufenteil(a, 2);
        var wuerfeltisch = new Vector3(-3.3f, 0, 2.5f);
        Teil(a, wuerfeltisch + new Vector3(0, 0.75f, 0), new Vector3(0.9f, 0.08f, 0.9f), holz, s2);
        Teil(a, wuerfeltisch + new Vector3(0, 0.37f, 0), new Vector3(0.2f, 0.7f, 0.2f), dunkel, s2);
        foreach (var (dx, dz) in new[] { (-0.1f, 0.05f), (0.12f, -0.08f) })
            Teil(a, wuerfeltisch + new Vector3(dx, 0.84f, dz), new Vector3(0.1f, 0.1f, 0.1f), Material(Leinen), s2);

        // Stufe 3: runder Stammtisch mit dem Wimpel der Stadt
        var s3 = Stufenteil(a, 3);
        var stammtisch = new Vector3(-3.4f, 0, -0.5f);
        Rund(a, stammtisch + new Vector3(0, 0.78f, 0), 0.7f, 0.08f, holz, s3);
        Rund(a, stammtisch + new Vector3(0, 0.39f, 0), 0.12f, 0.7f, dunkel, s3);
        Teil(a, stammtisch + new Vector3(0, 1.2f, 0), new Vector3(0.04f, 0.8f, 0.04f), dunkel, s3);
        Teil(a, stammtisch + new Vector3(0.2f, 1.45f, 0), new Vector3(0.36f, 0.24f, 0.02f), Material(Stadtrot), s3);

        // Sitz- und Stehplätze der Gäste, in der Reihenfolge, in der sie sich füllen
        var gaestePlaetze = new List<(Arbeitsplatz Platz, Func<bool> Frei)>();
        Func<bool> immer = () => true;
        Func<bool> wuerfel = () => _simulation?.Wirtschaft?.Gastwirtschaft is { IstAktiv: true } g && g.HatWuerfeltisch(a.Betrieb) && a.Betrieb.Stufe >= 2;
        Func<bool> stamm = () => _simulation?.Wirtschaft?.Gastwirtschaft is { IstAktiv: true } g && g.HatStammtisch(a.Betrieb);
        foreach (var (x, z) in new[] { (-1.0f, -0.6f), (0.6f, 1.9f) })
            foreach (var dx in new[] { -0.6f, 0.6f })
            {
                gaestePlaetze.Add((new(new Vector3(x + dx, 0, z - 0.7f), Sued, Bewegung.Schenken, Werkzeug.Krug, true), immer));
                gaestePlaetze.Add((new(new Vector3(x + dx, 0, z + 0.7f), Nord, Bewegung.Schenken, Werkzeug.Krug, true), immer));
            }
        gaestePlaetze.Add((new(new Vector3(2.1f, 0, 0.2f), Ost, Bewegung.Schenken, Werkzeug.Krug), immer));   // an der Theke
        gaestePlaetze.Add((new(new Vector3(2.1f, 0, 1.6f), Ost, Bewegung.Schenken, Werkzeug.Krug), immer));
        gaestePlaetze.Add((new(wuerfeltisch + new Vector3(0, 0, 0.6f), Nord, Bewegung.Ruehren, Werkzeug.Keins), wuerfel));
        gaestePlaetze.Add((new(wuerfeltisch + new Vector3(-0.6f, 0, 0), Ost, Bewegung.Ruehren, Werkzeug.Keins), wuerfel));
        gaestePlaetze.Add((new(wuerfeltisch + new Vector3(0.6f, 0, 0), West, Bewegung.Ruehren, Werkzeug.Keins), wuerfel));
        gaestePlaetze.Add((new(stammtisch + new Vector3(0, 0, 0.8f), Nord, Bewegung.Schenken, Werkzeug.Krug), stamm));
        gaestePlaetze.Add((new(stammtisch + new Vector3(0.8f, 0, 0), West, Bewegung.Schenken, Werkzeug.Krug), stamm));
        gaestePlaetze.Add((new(stammtisch + new Vector3(0, 0, -0.8f), Sued, Bewegung.Schenken, Werkzeug.Krug), stamm));
        var gaeste = gaestePlaetze.Select((x, i) => GastBauen(a, x.Platz, i)).ToList();
        var abgewiesene = new[]
        {
            GastBauen(a, new(new Vector3(-2.0f, 0, 3.1f), Nord, Bewegung.Stehen, Werkzeug.Keins), 20),
            GastBauen(a, new(new Vector3(-1.3f, 0, 3.2f), Nord, Bewegung.Stehen, Werkzeug.Keins), 21),
        };
        var barde = GastBauen(a, new(new Vector3(2.0f, 0, -1.3f), Sued, Bewegung.Spielen, Werkzeug.Laute), 6);
        Infotafel(a, new Vector3(0, 3.4f, 0));

        Plaetze(a,
            new(new Vector3(3.75f, 0, 0.9f), West, Bewegung.Schenken, Werkzeug.Krug),    // der Wirt hinter der Theke
            new(new Vector3(1.3f, 0, 0.6f), West, Bewegung.Schenken, Werkzeug.Krug),     // Schankmagd
            new(new Vector3(3.9f, 0, -1.75f), Nord, Bewegung.Ruehren, Werkzeug.Kelle),   // Koch am Herd
            new(new Vector3(-1.3f, 0, 1.9f), Ost, Bewegung.Schenken, Werkzeug.Krug));    // zweite Magd

        // Gäste zählen: Die Simulation bucht sie am Ende jeder Stunde. Gezeigt werden die Gäste der letzten
        // vollen Stunde – oder, solange noch keine gezählt ist (gerade betreten, gerade geöffnet), die erwarteten.
        int letzteStunde = -1, bewirtetStand = 0, abgewiesenStand = 0, gezeigt = 0, draussen = 0;
        a.Zustand = (zeit, arbeitszeit, arbeitet) =>
        {
            var b = a.Betrieb;
            var stunde = zeit.UhrzeitInMinuten / 60;
            if (_simulation?.Wirtschaft?.Gastwirtschaft is not { IstAktiv: true } g)
            {
                herdAn[0] = arbeitet;
                return;
            }
            var d = g.Daten;
            var buch = g.DiesesJahr(b);
            if (stunde != letzteStunde)
            {
                var gezaehlt = stunde == letzteStunde + 1 && letzteStunde >= d.OeffnetUm && buch.Bewirtet >= bewirtetStand;
                gezeigt = gezaehlt ? buch.Bewirtet - bewirtetStand : (int)Math.Round(GaesteSchaetzung(g, b, stunde));
                draussen = gezaehlt ? Math.Max(0, buch.Abgewiesen - abgewiesenStand) : 0;
                letzteStunde = stunde;
                bewirtetStand = buch.Bewirtet;
                abgewiesenStand = buch.Abgewiesen;
            }
            var offen = stunde >= d.OeffnetUm && b.Belegt > 0;
            herdAn[0] = arbeitet || offen;
            var rest = offen ? gezeigt : 0;
            for (var i = 0; i < gaeste.Count; i++)
            {
                var frei = gaestePlaetze[i].Frei();
                gaeste[i].Koerper.Visible = frei && rest > 0;
                if (frei && rest > 0) rest--;
            }
            for (var i = 0; i < abgewiesene.Length; i++)
                abgewiesene[i].Koerper.Visible = offen && i < draussen;
            var spielt = offen && g.BardeGebucht(b) && stunde >= d.AbendAb;
            barde.Koerper.Visible = spielt;
            a.Infotafel!.Text = !offen ? $"Ausschank ab {d.OeffnetUm} Uhr · dieses Jahr {buch.Bewirtet} Gäste bewirtet"
                : $"{gezeigt} Gäste in der Stunde · dieses Jahr {buch.Bewirtet} bewirtet"
                  + (draussen > 0 ? $" · {draussen} abgewiesen (kein Vorrat)" : "")
                  + (spielt ? "\nDer Barde spielt auf" : "");
        };
    }

    /// <summary>Erwartete Gäste in dieser Stunde – dieselbe Rechnung wie in der Gastwirtschaft (Gewichte nach Tageszeit, Barde).</summary>
    private static double GaesteSchaetzung(Gastwirtschaft g, Betrieb b, int stunde)
    {
        var d = g.Daten;
        double Gewicht(int h) => h < d.OeffnetUm ? 0 : h >= d.AbendAb ? d.AbendGewicht : 1;
        var summe = Enumerable.Range(Spielzeit.TagesbeginnMinute / 60, Spielzeit.StundenProTag).Sum(Gewicht);
        var barde = g.BardeGebucht(b) && stunde >= d.AbendAb ? 1 + d.Barde.Gaeste : 1;
        return summe <= 0 ? 0 : g.ErwarteteGaeste(b) * Gewicht(stunde) / summe * barde;
    }

    /// <summary>
    /// Kirche: Altar mit Tuch, Kerzen und Kollektenschale (Ost), Kreuz an der Ostwand, Leuchter, drei Bankreihen,
    /// Kanzel, Schreibpult für Papier und Schriften (West).
    /// Stufe 2 (Dom): Kerzenziehgestell und Taufbecken. Stufe 3 (Kathedrale): Reliquienschrein und zweites Schreibpult.
    /// Zustand: Zur Messe (10 Uhr) sitzen die Gläubigen in den Bänken – eine Figur je 10 – und die Kollekte liegt in der Schale.
    /// </summary>
    private void KircheEinrichten(BetriebsAnsicht a)
    {
        var p = a.Mitte;
        var stein = Material(Mauerstein);
        var holz = Material(Holz);
        var dunkel = Material(DunklesHolz);
        var weiss = Material(Leinen);
        var gold = Metall(new Color(0.88f, 0.72f, 0.30f));
        var flamme = Leuchtend(new Color(1f, 0.85f, 0.45f), new Color(1f, 0.7f, 0.3f));
        var messe = new bool[1]; // setzt der Zustand, lesen die Kerzen

        // Altar mit Tuch, Kerzen und Kollektenschale
        Teil(a, new Vector3(3.7f, 0.55f, 0), new Vector3(0.9f, 1.0f, 2.0f), stein);
        Teil(a, new Vector3(3.65f, 1.07f, 0), new Vector3(1.0f, 0.05f, 2.1f), weiss);
        foreach (var z in new[] { -0.7f, 0.7f })
        {
            Rund(a, new Vector3(3.6f, 1.3f, z), 0.05f, 0.4f, weiss, segmente: 8);
            Teil(a, new Vector3(3.6f, 1.55f, z), new Vector3(0.06f, 0.1f, 0.06f), flamme);
        }
        Rund(a, new Vector3(3.55f, 1.12f, 0), 0.22f, 0.06f, gold, obenRadius: 0.27f);
        var kollekte = Rund(a, new Vector3(3.55f, 1.17f, 0), 0.15f, 0.06f, gold, segmente: 10);

        // Kreuz an der Ostwand über dem Altar
        Teil(a, new Vector3(4.55f, 3.2f, 0), new Vector3(0.08f, 1.8f, 0.16f), gold);
        Teil(a, new Vector3(4.55f, 3.6f, 0), new Vector3(0.08f, 0.16f, 1.0f), gold);

        // Zwei hohe Leuchter neben dem Altar
        foreach (var z in new[] { -1.6f, 1.6f })
        {
            Rund(a, new Vector3(3.6f, 0.8f, z), 0.05f, 1.4f, Metall(new Color(0.2f, 0.2f, 0.22f)), segmente: 6);
            Rund(a, new Vector3(3.6f, 1.65f, z), 0.07f, 0.3f, weiss, segmente: 8);
            Teil(a, new Vector3(3.6f, 1.88f, z), new Vector3(0.07f, 0.12f, 0.07f), flamme);
        }
        FeuerAnlegen(a, new Vector3(3.0f, 2.2f, 0), flamme, lichtAn: 1.3f, lichtAus: 0.15f, glutAn: 2.5f, glutAus: 0.6f, an: () => messe[0], reichweite: 8);

        // Drei Bankreihen mit Blick zum Altar
        var reihen = new[] { -1.8f, -0.4f, 1.0f };
        foreach (var x in reihen)
        {
            Teil(a, new Vector3(x, 0.45f, 0.2f), new Vector3(0.45f, 0.08f, 2.2f), holz);
            Teil(a, new Vector3(x, 0.22f, 0.2f), new Vector3(0.35f, 0.4f, 2.0f), dunkel);
            Teil(a, new Vector3(x + 0.24f, 0.8f, 0.2f), new Vector3(0.07f, 0.6f, 2.2f), holz);
        }

        // Kanzel (achteckig) und Schreibpult für Papier, Lehr- und Hetzschriften
        Rund(a, new Vector3(2.5f, 0.65f, 2.75f), 0.5f, 1.3f, holz, segmente: 8);
        Rund(a, new Vector3(2.5f, 1.33f, 2.75f), 0.58f, 0.08f, dunkel, segmente: 8);
        Teil(a, new Vector3(-3.85f, 0.45f, 1.2f), new Vector3(0.7f, 0.9f, 1.0f), holz);
        Teil(a, new Vector3(-3.75f, 0.93f, 1.2f), new Vector3(0.4f, 0.03f, 0.55f), Material(Pergament));

        // Stufe 2: Kerzenziehgestell mit Wachstopf, Taufbecken
        var s2 = Stufenteil(a, 2);
        foreach (var z in new[] { -1.3f, -0.3f })
            Teil(a, new Vector3(-3.95f, 1.0f, z), new Vector3(0.1f, 2.0f, 0.1f), holz, s2);
        Teil(a, new Vector3(-3.95f, 2.0f, -0.8f), new Vector3(0.12f, 0.1f, 1.1f), holz, s2);
        for (var i = 0; i < 4; i++)
            Teil(a, new Vector3(-3.95f, 1.6f, -1.1f + i * 0.2f), new Vector3(0.06f, 0.6f, 0.06f), weiss, s2);
        Rund(a, new Vector3(-3.6f, 0.35f, -0.8f), 0.25f, 0.5f, Metall(new Color(0.2f, 0.2f, 0.22f)), s2);
        Rund(a, new Vector3(1.9f, 0.45f, 2.9f), 0.45f, 0.9f, stein, s2, obenRadius: 0.55f);
        Rund(a, new Vector3(1.9f, 0.91f, 2.9f), 0.45f, 0.02f, Material(Wasser), s2);

        // Stufe 3: Reliquienschrein auf einem Sockel, zweites Schreibpult
        var s3 = Stufenteil(a, 3);
        var schrein = new Vector3(3.95f, 0, -2.5f);
        Teil(a, schrein + new Vector3(0, 0.5f, 0), new Vector3(0.7f, 0.9f, 0.7f), stein, s3);
        Teil(a, schrein + new Vector3(0, 1.17f, 0), new Vector3(0.6f, 0.45f, 0.4f), gold, s3);
        Dach(p + schrein + new Vector3(0, 1.39f, 0), new Vector3(0.65f, 0.3f, 0.45f), gold, quer: true, eltern: s3);
        Teil(a, new Vector3(-3.85f, 0.45f, 2.75f), new Vector3(0.7f, 0.9f, 1.0f), holz, s3);
        Teil(a, new Vector3(-3.75f, 0.93f, 2.75f), new Vector3(0.4f, 0.03f, 0.55f), Material(Pergament), s3);

        // Gläubige: neun Plätze in den Bänken, ab Stufe 2 drei Stehplätze dahinter
        var glaeubige = new List<Arbeiterfigur>();
        foreach (var x in reihen)
            foreach (var z in new[] { -0.6f, 0.2f, 1.0f })
                glaeubige.Add(GastBauen(a, new(new Vector3(x, 0, z), Ost, Bewegung.Beten, Werkzeug.Keins, true), glaeubige.Count));
        foreach (var x in reihen)
            glaeubige.Add(GastBauen(a, new(new Vector3(x, 0, 2.35f), Ost, Bewegung.Beten, Werkzeug.Keins), glaeubige.Count));
        Infotafel(a, new Vector3(0, 3.4f, 0));

        Plaetze(a,
            new(new Vector3(2.95f, 0, 0), West, Bewegung.Beten, Werkzeug.Keins),            // der Prediger am Altar
            new(new Vector3(-3.05f, 0, 1.2f), West, Bewegung.Schreiben, Werkzeug.Feder),    // schreibt am Pult
            new(new Vector3(-3.1f, 0, -0.8f), West, Bewegung.Ruehren, Werkzeug.Kelle),      // zieht Kerzen
            new(new Vector3(-3.05f, 0, 2.75f), West, Bewegung.Schreiben, Werkzeug.Feder));  // am zweiten Pult

        a.Zustand = (zeit, arbeitszeit, arbeitet) =>
        {
            var b = a.Betrieb;
            if (_simulation?.Wirtschaft is not { } w || !w.Kirchenwesen.IstAktiv) return;
            var k = w.Kirchenwesen;
            var buch = k.DiesesJahr(b);
            var stunde = zeit.UhrzeitInMinuten / 60;
            messe[0] = stunde == k.Daten.MesseUm && b.Belegt > 0;
            var plaetze = b.Stufe >= 2 ? glaeubige.Count : 9;
            var zahl = messe[0] ? Math.Min(plaetze, (int)Math.Ceiling(buch.Glaeubige / 10)) : 0;
            for (var i = 0; i < glaeubige.Count; i++)
                glaeubige[i].Koerper.Visible = i < zahl;
            // Die Kollekte als Münzstapel in der Schale – je mehr, desto höher
            kollekte.Visible = buch.Kollekte > 0;
            var hoehe = Math.Min(5f, 0.5f + (float)buch.Kollekte / 100f);
            kollekte.Scale = new Vector3(1, hoehe, 1);
            kollekte.Position = p + new Vector3(3.55f, 1.15f + 0.03f * hoehe, 0);
            var gulden = w.Daten.Allgemein.Waehrung;
            a.Infotafel!.Text = messe[0] ? $"Messe: {Zahlenformat.Ganz(buch.Glaeubige)} Gläubige · Kollekte {Zahlenformat.Ganz(buch.Kollekte)} {gulden}"
                : stunde < k.Daten.MesseUm ? $"Messe um {k.Daten.MesseUm} Uhr"
                : $"Messe gehalten: {Zahlenformat.Ganz(buch.Glaeubige)} Gläubige · Kollekte {Zahlenformat.Ganz(buch.Kollekte)} {gulden}";
        };
    }

    /// <summary>
    /// Diebesnest: Strohlager (West), Kartentisch mit dem Stadtplan (Mitte), Hehlertisch mit Sack, Münzen und Waage (Ost),
    /// eine Falltür im Boden. Diebesgut liegt als Kisten im Lager.
    /// Stufe 2 (Unterschlupf): Fälscherpult. Stufe 3 (Diebesgilde): Stuhl für Geiseln.
    /// Zustand: Tagsüber sind die Diebe am Markt – nur der Hehler bleibt. Rote Nadeln auf der Karte = Aufträge
    /// für die Nacht, der Münzhaufen wächst mit der Beute, eine Geisel sitzt da, solange Lösegeld aussteht.
    /// </summary>
    private void DiebesnestEinrichten(BetriebsAnsicht a)
    {
        var p = a.Mitte;
        var morsch = Material(Morsch);
        var holz = Material(Holz);
        var gold = Metall(new Color(0.88f, 0.72f, 0.30f));
        var leder = Material(new Color(0.42f, 0.30f, 0.18f));

        // Strohlager mit einer Decke
        Teil(a, new Vector3(-4.0f, 0.17f, 0.3f), new Vector3(0.9f, 0.14f, 1.8f), Material(Stroh));
        Teil(a, new Vector3(-4.0f, 0.17f, 2.5f), new Vector3(0.9f, 0.14f, 1.6f), Material(Stroh));
        Teil(a, new Vector3(-4.0f, 0.26f, 2.7f), new Vector3(0.8f, 0.05f, 0.9f), Material(new Color(0.40f, 0.16f, 0.14f)));

        // Kartentisch mit dem Stadtplan, Nadeln für die Aufträge der Nacht und einem Kerzenstummel
        Teil(a, new Vector3(0, 0.45f, 0.4f), new Vector3(1.4f, 0.8f, 0.9f), morsch);
        Teil(a, new Vector3(0, 0.88f, 0.4f), new Vector3(1.7f, 0.06f, 1.2f), holz);
        Teil(a, new Vector3(0, 0.92f, 0.4f), new Vector3(1.2f, 0.02f, 0.8f), Material(Pergament));
        var nadeln = new[] { new Vector3(-0.3f, 0, 0.2f), new Vector3(0.25f, 0, 0.55f), new Vector3(0.1f, 0, 0.15f) }
            .Select(o => Teil(a, new Vector3(o.X, 1.0f, o.Z), new Vector3(0.08f, 0.14f, 0.08f), Material(Stadtrot))).ToList();
        var stummel = Leuchtend(new Color(0.95f, 0.85f, 0.55f), new Color(1f, 0.65f, 0.25f));
        Rund(a, new Vector3(0.65f, 0.98f, 0.0f), 0.05f, 0.12f, Material(Leinen), segmente: 6);
        Teil(a, new Vector3(0.65f, 1.09f, 0.0f), new Vector3(0.05f, 0.08f, 0.05f), stummel);
        FeuerAnlegen(a, new Vector3(0.65f, 1.4f, 0), stummel, lichtAn: 0.6f, lichtAus: 0.6f, glutAn: 2f, glutAus: 2f, an: () => true, reichweite: 4);

        // Hehlertisch mit Sack, Waage und dem Münzhaufen der Beute
        Teil(a, new Vector3(3.6f, 0.45f, 0.6f), new Vector3(0.9f, 0.85f, 1.6f), morsch);
        Kugel(a, new Vector3(3.6f, 1.12f, 1.05f), 0.3f, leder, hoehe: 0.5f);
        Teil(a, new Vector3(3.6f, 1.1f, 0.0f), new Vector3(0.05f, 0.4f, 0.05f), Metall(new Color(0.3f, 0.3f, 0.33f)));
        Teil(a, new Vector3(3.6f, 1.3f, 0.0f), new Vector3(0.05f, 0.03f, 0.5f), Metall(new Color(0.3f, 0.3f, 0.33f)));
        var muenzen = Rund(a, new Vector3(3.55f, 0.95f, 0.45f), 0.22f, 0.1f, gold, obenRadius: 0.06f, segmente: 10);

        // Falltür im Boden – der Fluchtweg
        Teil(a, new Vector3(-1.6f, 0.105f, 2.6f), new Vector3(1.2f, 0.02f, 1.2f), Material(DunklesHolz));
        Teil(a, new Vector3(-1.6f, 0.12f, 2.6f), new Vector3(1.0f, 0.02f, 0.08f), Metall(new Color(0.3f, 0.3f, 0.33f)));

        // Stufe 2: Fälscherpult mit Siegel
        var s2 = Stufenteil(a, 2);
        Teil(a, new Vector3(3.7f, 0.45f, 2.7f), new Vector3(0.7f, 0.9f, 0.9f), holz, s2);
        Teil(a, new Vector3(3.65f, 0.92f, 2.6f), new Vector3(0.4f, 0.02f, 0.5f), Material(Pergament), s2);
        Rund(a, new Vector3(3.65f, 0.97f, 2.95f), 0.06f, 0.08f, Material(Stadtrot), s2, segmente: 8);

        // Stufe 3: Stuhl mit Stricken für Geiseln
        var s3 = Stufenteil(a, 3);
        var stuhl = new Vector3(-3.7f, 0, -1.0f);
        Teil(a, stuhl + new Vector3(0, 0.45f, 0), new Vector3(0.6f, 0.08f, 0.6f), holz, s3);
        Teil(a, stuhl + new Vector3(0, 0.22f, 0), new Vector3(0.5f, 0.4f, 0.5f), Material(DunklesHolz), s3);
        Teil(a, stuhl + new Vector3(-0.28f, 0.85f, 0), new Vector3(0.06f, 0.8f, 0.6f), holz, s3);
        var geisel = GastBauen(a, new(stuhl, Ost, Bewegung.Stehen, Werkzeug.Keins, true), 2);
        geisel.Koerper.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.62f, 0.08f, 0.62f) }, MaterialOverride = Material(Stroh), Position = new Vector3(0, 0.95f, 0) }); // Stricke
        Infotafel(a, new Vector3(0, 3.4f, 0));

        Plaetze(a,
            new(new Vector3(2.75f, 0, 0.6f), Ost, Bewegung.Schreiben, Werkzeug.Beutel),  // der Hehler zählt die Beute
            new(new Vector3(0, 0, 1.3f), Nord, Bewegung.Stehen, Werkzeug.Keins),         // um den Kartentisch
            new(new Vector3(-1.15f, 0, 0.4f), Ost, Bewegung.Stehen, Werkzeug.Keins),
            new(new Vector3(1.15f, 0, 0.4f), West, Bewegung.Stehen, Werkzeug.Keins),
            new(new Vector3(-0.4f, 0, -0.55f), Sued, Bewegung.Stehen, Werkzeug.Keins),
            new(new Vector3(2.85f, 0, 2.7f), Ost, Bewegung.Schreiben, Werkzeug.Feder),   // der Fälscher (Stufe 2)
            new(new Vector3(0.5f, 0, -0.55f), Sued, Bewegung.Stehen, Werkzeug.Keins),
            new(new Vector3(-3.2f, 0, 1.4f), West, Bewegung.Stehen, Werkzeug.Beutel));

        a.Zustand = (zeit, arbeitszeit, arbeitet) =>
        {
            var b = a.Betrieb;
            if (_simulation?.Wirtschaft is not { } w || !w.Diebe.IstAktiv) return;
            var diebe = w.Diebe;
            var auftraege = diebe.Auftraege.Count(x => x.Dieb == b.Konto);
            for (var i = 0; i < nadeln.Count; i++) nadeln[i].Visible = i < auftraege;
            var buch = diebe.DiesesJahr(b.Konto);
            muenzen.Visible = buch.Beute > 0;
            var hoehe = Math.Min(6f, 0.6f + (float)buch.Beute / 150f);
            muenzen.Scale = new Vector3(1, hoehe, 1);
            muenzen.Position = p + new Vector3(3.55f, 0.9f + 0.05f * hoehe, 0.45f);
            var hatGeisel = b.Stufe >= 3 && diebe.Briefe.Any(x => x.Dieb == b.Konto && x.Art == Diebesbriefart.Loesegeld && x.Geisel is not null);
            geisel.Koerper.Visible = hatGeisel;
            a.Infotafel!.Text = (arbeitszeit ? "Die Diebe sind am Markt – der Hehler hütet das Versteck\n" : "")
                + $"Beute dieses Jahr: {Zahlenformat.Ganz(buch.Beute)} {w.Daten.Allgemein.Waehrung} · gefasst: {buch.Faenge}"
                + (auftraege > 0 ? $"\nAufträge für die Nacht: {auftraege}" : "")
                + (hatGeisel ? "\nEine Geisel wartet auf das Lösegeld" : "");
        };
    }

    /// <summary>
    /// Wachhaus: Speergestell (Ost), Tisch mit Bänken, Krügen und Würfeln (Mitte), Zelle mit Gittern und Pritsche (West),
    /// Kohlebecken. Stufe 2 (Stadtwache): Rüstungsständer. Stufe 3 (Garnison): Strohpuppe zum Üben.
    /// Zustand: In der Zelle sitzen die Taschendiebe, die die Stadt in diesem Jahr gefasst hat (höchstens drei).
    /// </summary>
    private void WacheEinrichten(BetriebsAnsicht a)
    {
        var holz = Material(Holz);
        var dunkel = Material(DunklesHolz);
        var eisen = Metall(new Color(0.30f, 0.31f, 0.34f));
        var stahl = Metall(new Color(0.62f, 0.64f, 0.68f));
        var zinn = Metall(new Color(0.60f, 0.60f, 0.62f));

        // Speergestell an der Ostwand
        Teil(a, new Vector3(4.15f, 0.15f, -0.4f), new Vector3(0.35f, 0.1f, 2.2f), holz);
        Teil(a, new Vector3(4.15f, 1.9f, -0.4f), new Vector3(0.2f, 0.1f, 2.2f), holz);
        for (var i = 0; i < 6; i++)
        {
            Rund(a, new Vector3(4.15f, 1.4f, -1.3f + i * 0.36f), 0.03f, 2.6f, holz, segmente: 6);
            Teil(a, new Vector3(4.15f, 2.8f, -1.3f + i * 0.36f), new Vector3(0.06f, 0.25f, 0.06f), stahl);
        }

        // Tisch mit Bänken, Krügen und Würfeln
        Teil(a, new Vector3(-0.6f, 0.8f, 0.4f), new Vector3(1.8f, 0.1f, 1.0f), holz);
        Teil(a, new Vector3(-0.6f, 0.4f, 0.4f), new Vector3(1.2f, 0.7f, 0.3f), dunkel);
        foreach (var dz in new[] { -0.7f, 0.7f })
        {
            Teil(a, new Vector3(-0.6f, 0.45f, 0.4f + dz), new Vector3(1.8f, 0.08f, 0.35f), holz);
            Teil(a, new Vector3(-0.6f, 0.22f, 0.4f + dz), new Vector3(1.6f, 0.38f, 0.12f), dunkel);
        }
        foreach (var dx in new[] { -1.1f, 0.3f })
            Rund(a, new Vector3(dx, 0.93f, 0.45f), 0.07f, 0.16f, zinn, segmente: 8);
        Teil(a, new Vector3(-0.5f, 0.9f, 0.3f), new Vector3(0.1f, 0.1f, 0.1f), Material(Leinen));

        // Zelle in der Westecke: Gitter nach Osten und Norden, eine Pritsche
        for (var i = 0; i < 8; i++)
            Teil(a, new Vector3(-2.75f, 1.3f, 0.75f + i * 0.38f), new Vector3(0.06f, 2.6f, 0.06f), eisen);
        Teil(a, new Vector3(-2.75f, 2.6f, 2.1f), new Vector3(0.1f, 0.1f, 2.9f), eisen);
        for (var i = 0; i < 5; i++)
            Teil(a, new Vector3(-4.4f + i * 0.38f, 1.3f, 0.7f), new Vector3(0.06f, 2.6f, 0.06f), eisen);
        Teil(a, new Vector3(-3.65f, 2.6f, 0.7f), new Vector3(1.9f, 0.1f, 0.1f), eisen);
        Teil(a, new Vector3(-4.1f, 0.3f, 2.6f), new Vector3(0.7f, 0.4f, 1.6f), holz);

        // Kohlebecken – brennt, solange der Spieltag läuft
        var glut = Leuchtend(new Color(0.2f, 0.08f, 0.04f), new Color(1f, 0.45f, 0.1f));
        Rund(a, new Vector3(3.6f, 0.2f, 2.7f), 0.06f, 0.4f, eisen, segmente: 6);
        Rund(a, new Vector3(3.6f, 0.5f, 2.7f), 0.35f, 0.3f, eisen, obenRadius: 0.45f);
        Rund(a, new Vector3(3.6f, 0.66f, 2.7f), 0.4f, 0.04f, glut);
        FeuerAnlegen(a, new Vector3(3.2f, 1.2f, 2.3f), glut, lichtAn: 1.2f, lichtAus: 1.2f, glutAn: 1.8f, glutAus: 1.8f, an: () => true, reichweite: 6);

        // Stufe 2: Rüstungsständer mit Brustpanzer und Helm
        var s2 = Stufenteil(a, 2);
        Rund(a, new Vector3(4.0f, 0.7f, 1.5f), 0.05f, 1.3f, holz, s2, segmente: 6);
        s2.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.3f, Height = 0.9f }, MaterialOverride = stahl, Position = a.Mitte + new Vector3(4.0f, 1.3f, 1.5f) });
        Kugel(a, new Vector3(4.0f, 1.95f, 1.5f), 0.2f, stahl, s2);

        // Stufe 3: Strohpuppe zum Üben
        var s3 = Stufenteil(a, 3);
        Rund(a, new Vector3(1.5f, 0.8f, 2.8f), 0.06f, 1.6f, holz, s3, segmente: 6);
        s3.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.28f, Height = 0.9f }, MaterialOverride = Material(Stroh), Position = a.Mitte + new Vector3(1.5f, 1.35f, 2.8f) });
        Teil(a, new Vector3(1.5f, 1.45f, 2.8f), new Vector3(0.9f, 0.1f, 0.1f), holz, s3);
        Kugel(a, new Vector3(1.5f, 1.95f, 2.8f), 0.2f, Material(Stroh), s3);

        // Gefangene in der Zelle
        var gefangene = new[]
        {
            GastBauen(a, new(new Vector3(-4.1f, 0, 2.2f), Ost, Bewegung.Stehen, Werkzeug.Keins, true), 3),
            GastBauen(a, new(new Vector3(-3.4f, 0, 1.5f), Ost, Bewegung.Stehen, Werkzeug.Keins), 0),
            GastBauen(a, new(new Vector3(-4.1f, 0, 3.0f), Ost, Bewegung.Stehen, Werkzeug.Keins, true), 5),
        };
        Infotafel(a, new Vector3(0, 3.4f, 0));

        Plaetze(a,
            new(new Vector3(-1.1f, 0, 1.1f), Nord, Bewegung.Schenken, Werkzeug.Krug, true),   // am Tisch
            new(new Vector3(-0.1f, 0, 1.1f), Nord, Bewegung.Schenken, Werkzeug.Krug, true),
            new(new Vector3(-1.1f, 0, -0.3f), Sued, Bewegung.Schenken, Werkzeug.Krug, true),
            new(new Vector3(3.4f, 0, -0.4f), Ost, Bewegung.Meisseln, Werkzeug.Keins),         // pflegt die Speere
            new(new Vector3(-0.1f, 0, -0.3f), Sued, Bewegung.Ruehren, Werkzeug.Keins, true),  // würfelt
            new(new Vector3(3.25f, 0, 1.5f), Ost, Bewegung.Meisseln, Werkzeug.Keins),         // poliert die Rüstung (Stufe 2)
            new(new Vector3(-2.2f, 0, 2.0f), West, Bewegung.Stehen, Werkzeug.Speer),          // bewacht die Zelle
            new(new Vector3(0.6f, 0, 2.8f), Ost, Bewegung.Haemmern, Werkzeug.Schwert));       // übt an der Strohpuppe (Stufe 3)

        a.Zustand = (zeit, arbeitszeit, arbeitet) =>
        {
            var b = a.Betrieb;
            if (_simulation?.Wirtschaft is not { } w || !w.Wache.IstAktiv) return;
            var gefasst = w.Diebe.IstAktiv ? w.Konten.Sum(k => w.Diebe.DiesesJahr(k).Faenge) : 0;
            for (var i = 0; i < gefangene.Length; i++)
                gefangene[i].Koerper.Visible = i < gefasst;
            var buch = w.Wache.DiesesJahr(b.Konto);
            a.Infotafel!.Text = $"Sold dieses Jahr: {Zahlenformat.Ganz(buch.Sold)} {w.Daten.Allgemein.Waehrung} · Razzien frei: {w.Wache.FreieRazzien(b.Konto)}"
                + (gefasst > 0 ? $"\nTaschendiebe gefasst: {gefasst}" : "");
        };
    }

    /// <summary>
    /// Wechselstube: Wechseltisch mit grünem Tuch, Waage und Münzstapeln (Mitte), Geldtruhe, Schreibpult (West),
    /// Regal mit Schuldscheinen. Stufe 2 (Geldleihe): zweites Pult. Stufe 3 (Bankhaus): eiserner Tresor.
    /// Zustand: Die Münzstapel zeigen, wie voll die Kasse gegen den Rahmen ist; je Kredit an ein Haus liegt eine Rolle im Regal;
    /// während der Arbeitszeit stehen Kunden am Tisch.
    /// </summary>
    private void BankEinrichten(BetriebsAnsicht a)
    {
        var holz = Material(Holz);
        var dunkel = Material(DunklesHolz);
        var gold = Metall(new Color(0.92f, 0.75f, 0.30f));
        var eisen = Metall(new Color(0.30f, 0.31f, 0.34f));
        var tuch = Material(new Color(0.16f, 0.38f, 0.22f));

        // Wechseltisch mit Tuch und Waage
        Teil(a, new Vector3(0.7f, 0.45f, 0.3f), new Vector3(1.0f, 0.85f, 2.4f), holz);
        Teil(a, new Vector3(0.7f, 0.9f, 0.3f), new Vector3(1.1f, 0.06f, 2.5f), tuch);
        Teil(a, new Vector3(0.7f, 1.25f, -0.6f), new Vector3(0.05f, 0.65f, 0.05f), eisen);
        Teil(a, new Vector3(0.7f, 1.56f, -0.6f), new Vector3(0.05f, 0.04f, 0.8f), eisen);
        foreach (var dz in new[] { -0.35f, 0.35f })
            Teil(a, new Vector3(0.7f, 1.35f, -0.6f + dz), new Vector3(0.22f, 0.03f, 0.22f), gold);
        var stapel = new List<MeshInstance3D>();
        foreach (var z in new[] { 0.0f, 0.45f, 0.9f })
            foreach (var x in new[] { 0.5f, 0.85f })
                stapel.Add(Rund(a, new Vector3(x, 1.0f, z), 0.1f, 0.1f, gold, segmente: 10));

        // Geldtruhe mit offenem Deckel – Gold darin, sobald Geld in der Kasse ist
        Teil(a, new Vector3(0.7f, 0.4f, 2.3f), new Vector3(1.0f, 0.6f, 0.6f), dunkel);
        Teil(a, new Vector3(0.7f, 0.95f, 2.62f), new Vector3(1.0f, 0.6f, 0.06f), dunkel);
        var truhengold = Teil(a, new Vector3(0.7f, 0.68f, 2.3f), new Vector3(0.85f, 0.06f, 0.45f), gold);

        // Schreibpult mit Rechnungsbuch, Regal mit Schuldscheinen
        Teil(a, new Vector3(-3.85f, 0.45f, 0.6f), new Vector3(0.7f, 0.9f, 1.0f), holz);
        Teil(a, new Vector3(-3.8f, 0.93f, 0.6f), new Vector3(0.45f, 0.06f, 0.6f), Material(new Color(0.45f, 0.18f, 0.14f)));
        Teil(a, new Vector3(-4.38f, 1.1f, -1.2f), new Vector3(0.08f, 2.0f, 1.1f), holz);
        var rollen = new List<MeshInstance3D>();
        foreach (var y in new[] { 0.7f, 1.4f })
        {
            Teil(a, new Vector3(-4.2f, y, -1.2f), new Vector3(0.4f, 0.05f, 1.1f), holz);
            for (var j = 0; j < 3; j++)
                rollen.Add(Rund(a, new Vector3(-4.2f, y + 0.1f, -1.55f + j * 0.33f), 0.07f, 0.35f, Material(Pergament), drehung: new Vector3(0, 0, 90), segmente: 8));
        }

        // Stufe 2: zweites Pult
        var s2 = Stufenteil(a, 2);
        Teil(a, new Vector3(-3.85f, 0.45f, 2.6f), new Vector3(0.7f, 0.9f, 1.0f), holz, s2);
        Teil(a, new Vector3(-3.8f, 0.93f, 2.6f), new Vector3(0.45f, 0.06f, 0.6f), Material(new Color(0.20f, 0.25f, 0.45f)), s2);

        // Stufe 3: eiserner Tresor mit Bändern und goldenem Schloss
        var s3 = Stufenteil(a, 3);
        var tresor = new Vector3(3.95f, 0, -2.5f);
        Teil(a, tresor + new Vector3(0, 0.75f, 0), new Vector3(1.1f, 1.3f, 1.0f), Metall(new Color(0.20f, 0.21f, 0.24f)), s3);
        foreach (var y in new[] { 0.4f, 1.1f })
            Teil(a, tresor + new Vector3(0, y, 0), new Vector3(1.15f, 0.08f, 1.05f), eisen, s3);
        Teil(a, tresor + new Vector3(-0.56f, 0.8f, 0), new Vector3(0.05f, 0.25f, 0.2f), gold, s3);

        var kunden = new[]
        {
            GastBauen(a, new(new Vector3(1.6f, 0, -0.1f), West, Bewegung.Stehen, Werkzeug.Beutel), 1),
            GastBauen(a, new(new Vector3(1.6f, 0, 0.9f), West, Bewegung.Stehen, Werkzeug.Keins), 2),
        };
        Infotafel(a, new Vector3(0, 3.4f, 0));

        Plaetze(a,
            new(new Vector3(-0.1f, 0, 0.3f), Ost, Bewegung.Schreiben, Werkzeug.Feder),    // der Wechsler am Tisch
            new(new Vector3(-3.05f, 0, 0.6f), West, Bewegung.Schreiben, Werkzeug.Feder),  // Schreiber am Pult
            new(new Vector3(-3.05f, 0, 2.6f), West, Bewegung.Schreiben, Werkzeug.Feder),  // zweites Pult (Stufe 2)
            new(new Vector3(3.1f, 0, -1.6f), Ost / 2, Bewegung.Schreiben, Werkzeug.Beutel)); // am Tresor (Stufe 3)

        a.Zustand = (zeit, arbeitszeit, arbeitet) =>
        {
            var b = a.Betrieb;
            if (_simulation?.Wirtschaft is not { } w || !w.Bank.IstAktiv) return;
            var bank = w.Bank;
            var kasse = bank.Kasse(b);
            var voll = bank.RahmenVoll(b);
            var anteil = voll > 0 ? Math.Clamp(kasse / voll, 0, 1) : 0;
            var zahl = (int)Math.Ceiling(anteil * stapel.Count);
            for (var i = 0; i < stapel.Count; i++)
            {
                stapel[i].Visible = i < zahl;
                var hoehe = 1.5f + (i * 7 % 3) * 0.8f; // ungleich hohe Stapel
                stapel[i].Scale = new Vector3(1, hoehe, 1);
                stapel[i].Position = stapel[i].Position with { Y = a.Mitte.Y + 0.93f + 0.05f * hoehe };
            }
            truhengold.Visible = kasse > 0;
            var kredite = bank.KrediteVon(b.Konto).Count();
            for (var i = 0; i < rollen.Count; i++) rollen[i].Visible = i < kredite;
            var kundschaft = arbeitszeit && b.Belegt > 0;
            kunden[0].Koerper.Visible = kundschaft;
            kunden[1].Koerper.Visible = kundschaft && b.Stufe >= 2;
            var gulden = w.Daten.Allgemein.Waehrung;
            a.Infotafel!.Text = $"Kasse {Zahlenformat.Ganz(kasse)} von {Zahlenformat.Ganz(voll)} {gulden}"
                + (kredite > 0 ? $" · Kredite an Häuser: {kredite}" : "");
        };
    }

    /// <summary>
    /// Siederei: Destillierkessel aus Kupfer über dem Ofen mit Kühlfass und Vorlage (Ost) – die Vorlage hat die Farbe
    /// des Dufts –, Trockengestell mit Lavendel und Jasmin (West), Mischtisch mit Flakons (Mitte).
    /// Stufe 2 (Duftwerkstatt): Spindelpresse. Stufe 3 (Parfumerie): Duftorgel mit vielen Flakons.
    /// </summary>
    private void SiedereiEinrichten(BetriebsAnsicht a)
    {
        var holz = Material(Holz);
        var kupfer = Metall(new Color(0.78f, 0.45f, 0.25f));
        var lavendel = Material(new Color(0.55f, 0.43f, 0.78f));
        var jasmin = Material(new Color(0.96f, 0.95f, 0.88f));
        var eisen = Metall(new Color(0.30f, 0.31f, 0.34f));

        // Ofen mit Glut, Kupferkessel mit Helm, Rohr zum Kühlfass, Vorlage
        var k = new Vector3(3.6f, 0, 0.4f);
        Teil(a, k + new Vector3(0, 0.4f, 0), new Vector3(1.4f, 0.7f, 1.4f), Material(Ziegel));
        var glut = Leuchtend(new Color(0.2f, 0.08f, 0.04f), new Color(1f, 0.45f, 0.1f));
        Teil(a, k + new Vector3(-0.72f, 0.35f, 0), new Vector3(0.05f, 0.3f, 0.6f), glut);
        Kugel(a, k + new Vector3(0, 1.3f, 0), 0.7f, kupfer, hoehe: 1.2f);
        Rund(a, k + new Vector3(0, 2.1f, 0), 0.35f, 0.5f, kupfer, obenRadius: 0.1f);
        Stab(a, k + new Vector3(0, 2.3f, 0), k + new Vector3(0, 1.6f, 2.2f), 0.09f, kupfer);
        Rund(a, k + new Vector3(0, 0.8f, 2.2f), 0.45f, 1.4f, holz);
        Stab(a, k + new Vector3(-0.4f, 0.7f, 2.2f), k + new Vector3(-0.85f, 0.45f, 2.2f), 0.06f, kupfer);
        var vorlage = Leuchtend(lavendel.AlbedoColor, lavendel.AlbedoColor);
        Kugel(a, k + new Vector3(-0.95f, 0.3f, 2.2f), 0.18f, vorlage);
        a.Produktfarben.Add(vorlage);
        a.Feuer.Add(new Feuerstelle(null, vorlage, 0, 0, 0.6f, 0.05f));
        FeuerAnlegen(a, k + new Vector3(-1.0f, 0.8f, 0), glut, lichtAn: 1.5f, glutAn: 1.8f, reichweite: 6);

        // Trockengestell mit Lavendel und Jasmin an der Westwand
        foreach (var z in new[] { -0.6f, 2.4f })
            Teil(a, new Vector3(-4.1f, 1.2f, z), new Vector3(0.1f, 2.4f, 0.1f), holz);
        Teil(a, new Vector3(-4.1f, 2.35f, 0.9f), new Vector3(0.1f, 0.1f, 3.1f), holz);
        for (var i = 0; i < 7; i++)
            Teil(a, new Vector3(-4.1f, 1.95f, -0.4f + i * 0.45f), new Vector3(0.2f, 0.6f, 0.2f), i % 2 == 0 ? lavendel : jasmin);

        // Mischtisch mit Flakons und einem großen Kolben in der Farbe des Dufts
        Teil(a, new Vector3(0, 0.45f, 0.6f), new Vector3(1.6f, 0.9f, 0.9f), holz);
        var flakons = new[] { new Color(0.55f, 0.43f, 0.78f), new Color(0.96f, 0.95f, 0.88f), new Color(0.80f, 0.55f, 0.30f), new Color(0.45f, 0.62f, 0.55f), new Color(0.75f, 0.35f, 0.45f) };
        for (var i = 0; i < flakons.Length; i++)
            Rund(a, new Vector3(-0.55f + i * 0.25f, 1.0f, 0.4f), 0.06f, 0.2f, Material(flakons[i]), segmente: 8);
        var kolben = Leuchtend(lavendel.AlbedoColor, lavendel.AlbedoColor);
        Kugel(a, new Vector3(0.5f, 1.07f, 0.75f), 0.14f, kolben);
        a.Produktfarben.Add(kolben);
        a.Feuer.Add(new Feuerstelle(null, kolben, 0, 0, 0.4f, 0.05f));

        // Stufe 2: Spindelpresse – die Spindel dreht sich bei der Arbeit
        var s2 = Stufenteil(a, 2);
        var presse = new Vector3(-1.8f, 0, 2.8f);
        Teil(a, presse + new Vector3(0, 0.4f, 0), new Vector3(1.0f, 0.7f, 0.7f), holz, s2);
        foreach (var dx in new[] { -0.4f, 0.4f })
            Teil(a, presse + new Vector3(dx, 1.3f, 0), new Vector3(0.12f, 1.2f, 0.12f), holz, s2);
        Teil(a, presse + new Vector3(0, 1.95f, 0), new Vector3(1.0f, 0.15f, 0.2f), holz, s2);
        Teil(a, presse + new Vector3(0, 0.95f, 0), new Vector3(0.7f, 0.08f, 0.55f), holz, s2);
        var spindel = Rund(a, presse + new Vector3(0, 1.45f, 0), 0.06f, 1.0f, eisen, s2, segmente: 6);
        Quader(new Vector3(0, 0.15f, 0), new Vector3(0.7f, 0.05f, 0.05f), eisen, spindel); // Drehgriff – dreht sich mit der Spindel
        a.Bewegt.Add(new Bewegtteil(spindel, Vector3.Up, 2, Vector3.Zero, spindel.Position));

        // Stufe 3: Duftorgel – Stufenregal voller Flakons
        var s3 = Stufenteil(a, 3);
        var orgel = new Vector3(3.95f, 0, -2.55f);
        foreach (var (dx, y, hoehe) in new[] { (0f, 0.45f, 0.9f), (0.15f, 1.1f, 0.4f), (0.3f, 1.45f, 0.3f) })
        {
            Teil(a, orgel + new Vector3(dx, y, 0), new Vector3(1.1f - dx * 2, hoehe, 0.9f), holz, s3);
            for (var i = 0; i < 4; i++)
                Rund(a, orgel + new Vector3(dx - 0.35f + (1.1f - dx * 2) / 2 - 0.2f, y + hoehe / 2 + 0.1f, -0.3f + i * 0.2f), 0.05f, 0.18f,
                    Material(flakons[(i + (int)(dx * 10)) % flakons.Length]), s3, segmente: 8);
        }

        Plaetze(a,
            new(new Vector3(2.6f, 0, 0.4f), Ost, Bewegung.Ruehren, Werkzeug.Kelle),       // am Kessel
            new(new Vector3(0, 0, 1.45f), Nord, Bewegung.Schreiben, Werkzeug.Keins),       // mischt am Tisch
            new(new Vector3(-1.8f, 0, 2.0f), Sued, Bewegung.Ruehren, Werkzeug.Keins),      // dreht die Presse (Stufe 2)
            new(new Vector3(3.2f, 0, -1.55f), Ost / 2, Bewegung.Schreiben, Werkzeug.Keins)); // an der Duftorgel (Stufe 3)
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Figuren
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Tracht je Beruf: Schürze (oder Wappenrock), Kopfbedeckung und – bei Mönchen, Dieben, Wechslern – ein eigenes Gewand.</summary>
    private static (Color? Schuerze, Kopfputz Kopf, Color? Gewand) Tracht(Werkstil stil) => stil switch
    {
        Werkstil.Giesserei => (new Color(0.30f, 0.20f, 0.13f), Kopfputz.Keiner, null),       // Lederschürze
        Werkstil.Tischlerei => (new Color(0.72f, 0.66f, 0.52f), Kopfputz.Keiner, null),      // Leinenschürze
        Werkstil.Maurerei => (new Color(0.62f, 0.60f, 0.56f), Kopfputz.Kappe, null),
        Werkstil.Tinkturei => (new Color(0.28f, 0.42f, 0.26f), Kopfputz.Barett, null),
        Werkstil.Schaenke => (new Color(0.90f, 0.88f, 0.82f), Kopfputz.Keiner, null),
        Werkstil.Kirche => (new Color(0.88f, 0.85f, 0.76f), Kopfputz.Keiner, new Color(0.26f, 0.19f, 0.13f)), // Kutte mit Stola
        Werkstil.Diebesnest => (null, Kopfputz.Kapuze, new Color(0.17f, 0.17f, 0.19f)),
        Werkstil.Wache => (Stadtrot, Kopfputz.Helm, null),                                    // Wappenrock der Stadt
        Werkstil.Bank => (null, Kopfputz.Barett, new Color(0.14f, 0.19f, 0.36f)),
        _ => (new Color(0.55f, 0.43f, 0.78f), Kopfputz.Keiner, null),                        // Siederei: lavendelfarben
    };

    private Arbeiterfigur ArbeiterBauen(BetriebsAnsicht ansicht, string kraft, int nummer)
    {
        // Volk und Stand sieht man an Größe, Kleidung und Haut.
        var (kleidung, haut, groesse) = kraft switch
        {
            var k when IstAnfaenger(k) => (new Color(0.58f, 0.52f, 0.36f), new Color(0.86f, 0.70f, 0.57f), 0.85f),
            var k when IstVorarbeiter(k) => (new Color(0.30f, 0.21f, 0.15f), Haut, 1.05f),
            "Geselle" or "Altknecht" => (new Color(0.42f, 0.30f, 0.20f), Haut, 1.0f),
            "Ork" => (new Color(0.30f, 0.27f, 0.22f), new Color(0.36f, 0.50f, 0.28f), 1.2f),
            "Leibeigener" => (new Color(0.45f, 0.43f, 0.39f), new Color(0.80f, 0.64f, 0.52f), 0.95f),
            _ => (new Color(0.36f, 0.32f, 0.27f), Haut, 1.0f),
        };
        var platz = ansicht.Plaetze[nummer];
        var (schuerze, kopf, gewand) = Tracht(ansicht.Stil);
        return FigurBauen(ansicht.Innen!, ansicht.Mitte + platz.Ort, platz.Drehung, Vector3.One * groesse, gewand ?? kleidung, haut,
            schuerze, kopf, platz.Werkzeug, platz.Art, nummer * 1.3f, platz.Sitzt);
    }

    /// <summary>Gast, Gläubiger, Kunde oder Gefangener – Volk und Gewand ergeben sich aus der Nummer (jede Partie gleich).</summary>
    private Arbeiterfigur GastBauen(InnenAnsicht a, Arbeitsplatz platz, int nummer)
    {
        var (groesse, breite, bart) = Voelker[nummer % Voelker.Length];
        var kleidung = Gewandfarben[nummer * 3 % Gewandfarben.Length];
        var figur = FigurBauen(a.Innen!, a.Mitte + platz.Ort, platz.Drehung, new Vector3(groesse * breite, groesse, groesse * breite), kleidung, Haut,
            null, nummer % 5 == 2 ? Kopfputz.Barett : Kopfputz.Keiner, platz.Werkzeug, platz.Art, nummer * 0.9f, platz.Sitzt, bart);
        figur.Koerper.Visible = false;
        a.Besucher.Add(figur);
        return figur;
    }

    private Arbeiterfigur FigurBauen(Node3D eltern, Vector3 ort, float drehung, Vector3 masse, Color kleidung, Color haut, Color? schuerze,
        Kopfputz kopf, Werkzeug werkzeug, Bewegung art, float takt, bool sitzt = false, bool bart = false)
    {
        // Wer sitzt, sinkt ein Stück ein – Bank und Tisch verdecken den Rest.
        var koerper = new Node3D { Position = ort + (sitzt ? new Vector3(0, -0.42f * masse.Y, 0) : Vector3.Zero), Rotation = new Vector3(0, drehung, 0), Scale = masse };
        eltern.AddChild(koerper);

        koerper.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.28f, Height = 1.3f }, MaterialOverride = Material(kleidung), Position = new Vector3(0, 0.65f, 0) });
        koerper.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.2f, Height = 0.4f }, MaterialOverride = Material(haut), Position = new Vector3(0, 1.5f, 0) });
        if (schuerze is { } s)
            koerper.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.5f, 0.7f, 0.06f) }, MaterialOverride = Material(s), Position = new Vector3(0, 0.75f, -0.27f) });
        if (bart)
            koerper.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.24f, 0.24f, 0.1f) }, MaterialOverride = Material(Bartfarbe), Position = new Vector3(0, 1.38f, -0.16f) });
        var kopfteil = kopf switch
        {
            Kopfputz.Helm => new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.23f, Height = 0.2f }, MaterialOverride = Metall(new Color(0.55f, 0.57f, 0.62f)), Position = new Vector3(0, 1.68f, 0) },
            Kopfputz.Kapuze => new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.24f, Height = 0.5f }, MaterialOverride = Material(kleidung), Position = new Vector3(0, 1.55f, 0.07f) },
            Kopfputz.Barett => new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.24f, BottomRadius = 0.2f, Height = 0.08f }, MaterialOverride = Material(new Color(0.12f, 0.10f, 0.10f)), Position = new Vector3(0, 1.7f, 0) },
            Kopfputz.Kappe => new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.21f, Height = 0.12f }, MaterialOverride = Material(Leinen), Position = new Vector3(0, 1.66f, 0) },
            Kopfputz.Mitra => new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.2f, Height = 0.45f, RadialSegments = 4 }, MaterialOverride = Material(Leinen), Position = new Vector3(0, 1.85f, 0) },
            _ => null,
        };
        if (kopfteil is not null) koerper.AddChild(kopfteil);

        // Arm mit Werkzeug: dreht sich um die Schulter. Ruhestellung zeigt nach unten.
        var arm = new Node3D { Position = new Vector3(0.32f, 1.15f, 0) };
        koerper.AddChild(arm);
        arm.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.12f, 0.12f, 0.6f) }, MaterialOverride = Material(haut), Position = new Vector3(0, 0, -0.3f) });
        WerkzeugBauen(koerper, arm, werkzeug);
        return new Arbeiterfigur(koerper, arm, takt, art, arm.Position);
    }

    private static void WerkzeugBauen(Node3D koerper, Node3D arm, Werkzeug werkzeug)
    {
        void Stueck(Node3D an, Vector3 groesse, Color farbe, Vector3 ort, bool metall = false) =>
            an.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = groesse }, MaterialOverride = metall ? Metall(farbe) : Material(farbe), Position = ort });
        var stahl = new Color(0.62f, 0.64f, 0.68f);
        switch (werkzeug)
        {
            case Werkzeug.Hammer:
                Stueck(arm, new Vector3(0.05f, 0.05f, 0.35f), Holz, new Vector3(0, 0, -0.7f));
                Stueck(arm, new Vector3(0.16f, 0.26f, 0.16f), new Color(0.25f, 0.25f, 0.27f), new Vector3(0, 0.05f, -0.86f), true);
                break;
            case Werkzeug.Saege:
                Stueck(arm, new Vector3(0.06f, 0.12f, 0.12f), Holz, new Vector3(0, 0, -0.62f));
                Stueck(arm, new Vector3(0.02f, 0.16f, 0.7f), stahl, new Vector3(0, -0.04f, -1.0f), true);
                break;
            case Werkzeug.Meissel:
                Stueck(arm, new Vector3(0.05f, 0.05f, 0.28f), stahl, new Vector3(0, 0, -0.74f), true);
                break;
            case Werkzeug.Kelle:
                Stueck(arm, new Vector3(0.05f, 0.9f, 0.05f), Holz, new Vector3(0, -0.3f, -0.62f));
                break;
            case Werkzeug.Feder:
                Stueck(arm, new Vector3(0.02f, 0.22f, 0.03f), Leinen, new Vector3(0, 0.1f, -0.62f));
                break;
            case Werkzeug.Krug:
                Stueck(arm, new Vector3(0.15f, 0.2f, 0.15f), new Color(0.60f, 0.60f, 0.62f), new Vector3(0, 0.06f, -0.64f), true);
                break;
            case Werkzeug.Speer:
                Stueck(arm, new Vector3(0.05f, 2.4f, 0.05f), Holz, new Vector3(0, 0.5f, -0.62f));
                Stueck(arm, new Vector3(0.07f, 0.25f, 0.07f), stahl, new Vector3(0, 1.8f, -0.62f), true);
                break;
            case Werkzeug.Schwert:
                Stueck(arm, new Vector3(0.25f, 0.04f, 0.04f), new Color(0.3f, 0.3f, 0.33f), new Vector3(0, 0, -0.64f), true);
                Stueck(arm, new Vector3(0.04f, 0.06f, 0.8f), stahl, new Vector3(0, 0, -1.05f), true);
                break;
            case Werkzeug.Beutel:
                arm.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.12f, Height = 0.24f }, MaterialOverride = Material(new Color(0.42f, 0.30f, 0.18f)), Position = new Vector3(0, -0.1f, -0.62f) });
                break;
            case Werkzeug.Laute:
                // Die Laute liegt vor dem Bauch; der Arm schlägt die Saiten.
                koerper.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.22f, Height = 0.3f }, MaterialOverride = Material(HellesHolz), Position = new Vector3(0.05f, 0.85f, -0.3f) });
                Stueck(koerper, new Vector3(0.55f, 0.06f, 0.06f), DunklesHolz, new Vector3(-0.35f, 0.95f, -0.32f));
                break;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Jedes Bild: Innenraum nach der Simulation stellen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Stufenteile, Zustand, Feuer, bewegte Teile, Werkstück, Lager und Figuren – nur für den betretenen Betrieb.</summary>
    private void InnenAktualisieren(BetriebsAnsicht a, Spielzeit zeit, bool arbeitszeit, bool arbeitet, double delta)
    {
        var b = a.Betrieb;
        GemeinsamAktualisieren(a, zeit, arbeitszeit, arbeitet, delta, b.Stufe);
        if (a.Modellraum is { } modellraum)
        {
            InnenModellAktualisieren(a, modellraum, zeit, arbeitszeit, arbeitet, delta);
            return;
        }
        if (b.IstWerkstatt)
        {
            var farbe = Warenfarbe(b.Produkt);
            foreach (var m in a.Produktfarben)
            {
                m.AlbedoColor = farbe;
                m.Emission = farbe;
            }
            if (a.Werkstueck is not null)
            {
                a.Werkstueck.Visible = arbeitet;
                ((StandardMaterial3D)a.Werkstueck.MaterialOverride).AlbedoColor = farbe;
            }
        }
        KistenAktualisieren(a);
        ArbeiterAktualisieren(a, zeit, arbeitszeit, arbeitet);
        BesucherBewegen(a);
    }

    /// <summary>
    /// Was jeder Innenraum braucht – Werkstatt, Rathaus oder Dom: Stufenteile, Zustand aus der Simulation,
    /// Feuer und bewegte Teile. <paramref name="aktiv"/> heißt „es wird gearbeitet" (bzw. Amtsgeschäfte laufen).
    /// </summary>
    private void GemeinsamAktualisieren(InnenAnsicht a, Spielzeit zeit, bool arbeitszeit, bool aktiv, double delta, int stufe)
    {
        foreach (var (teil, ab) in a.Stufenteile) teil.Visible = stufe >= ab;
        a.Zustand?.Invoke(zeit, arbeitszeit, aktiv); // vor dem Feuer: setzt z. B., ob gerade Messe ist
        foreach (var raumzustand in a.Raumzustaende) raumzustand(zeit, arbeitszeit);
        foreach (var f in a.Feuer)
        {
            var an = f.An?.Invoke() ?? aktiv;
            if (f.Licht is not null) f.Licht.LightEnergy = an ? f.LichtAn : f.LichtAus;
            if (f.Glut is not null) f.Glut.EmissionEnergyMultiplier = an ? f.GlutAn : f.GlutAus;
        }
        if (aktiv)
            foreach (var t in a.Bewegt)
            {
                if (t.Hub == Vector3.Zero) t.Knoten.RotateObjectLocal(t.Achse, t.Tempo * (float)delta);
                else t.Knoten.Position = t.Ruhe + t.Hub * Mathf.Sin((float)_animationszeit * t.Tempo);
            }
    }

    /// <summary>Gäste, Gläubige, Kunden: sichtbare Besucher bewegen den Arm nach ihrer Tätigkeit.</summary>
    private void BesucherBewegen(InnenAnsicht a)
    {
        foreach (var gast in a.Besucher)
            if (gast.Koerper.Visible) ArmBewegen(gast, true);
    }

    // Farben der Kisten je Ware – so erkennt man auf einen Blick, was im Lager liegt.
    private static Color Warenfarbe(string ware) => ware switch
    {
        "Eisen" => new Color(0.45f, 0.40f, 0.36f),
        "Silber" => new Color(0.78f, 0.80f, 0.84f),
        "Beschlag" => new Color(0.52f, 0.36f, 0.22f),
        "Dolch" => new Color(0.40f, 0.48f, 0.60f),
        "Silberring" => new Color(0.85f, 0.72f, 0.35f),
        "Gold" => new Color(0.92f, 0.75f, 0.30f),
        "Holz" => new Color(0.55f, 0.40f, 0.24f),
        "Holzzapfen" => new Color(0.66f, 0.52f, 0.33f),
        "Bruchstein" => new Color(0.68f, 0.65f, 0.60f),
        "Granit" => new Color(0.50f, 0.50f, 0.54f),
        "Kräuter" => new Color(0.30f, 0.52f, 0.25f),
        "Kröte" => new Color(0.42f, 0.48f, 0.22f),
        "Gerste" => new Color(0.80f, 0.68f, 0.30f),
        "Roggen" => new Color(0.66f, 0.55f, 0.28f),
        "Lumpen" => new Color(0.70f, 0.68f, 0.62f),
        "Wachs" => new Color(0.90f, 0.80f, 0.45f),
        "Lavendel" => new Color(0.55f, 0.43f, 0.78f),
        "Jasmin" => new Color(0.96f, 0.95f, 0.88f),
        "Farbe" => new Color(0.18f, 0.32f, 0.62f),          // Indigo
        "Kräuterschnaps" => new Color(0.78f, 0.56f, 0.18f), // Bernstein
        "Heilsalbe" => new Color(0.62f, 0.68f, 0.30f),      // gelbgrüne Salbe
        "Rosenwasser" => new Color(0.90f, 0.55f, 0.62f),
        "Gegengift" => new Color(0.30f, 0.62f, 0.55f),
        "Theriak" => new Color(0.38f, 0.20f, 0.12f),        // dunkler Sud
        "Zinnober" => new Color(0.82f, 0.18f, 0.10f),
        "Politur" => new Color(0.74f, 0.52f, 0.22f),           // goldbraun
        "Blume der Zwietracht" => new Color(0.55f, 0.28f, 0.72f), // violett
        "Giftdolch" => new Color(0.40f, 0.68f, 0.18f),         // giftgrün
        "Moschusduft" => new Color(0.46f, 0.25f, 0.18f),       // dunkles Rotbraun
        "Fechterduft" => new Color(0.28f, 0.45f, 0.68f),       // Stahlblau
        "Heiligenduft" => new Color(0.93f, 0.88f, 0.68f),      // helles Elfenbein mit Gold
        "Schattendolch" => new Color(0.24f, 0.14f, 0.36f),     // tiefes Violettschwarz
        "Weingeist" => new Color(0.80f, 0.88f, 0.90f),         // klar, ein Hauch Blau
        "Körnermahlzeit" => new Color(0.74f, 0.58f, 0.28f),    // Brei-Ocker
        "Dünnbier" => new Color(0.90f, 0.76f, 0.34f),          // helles Gold
        "Starkbier" => new Color(0.62f, 0.34f, 0.10f),         // Bernstein
        "Kräuterlikör" => new Color(0.32f, 0.58f, 0.22f),      // Grün
        "Festbier" => new Color(0.78f, 0.40f, 0.12f),          // Rotgold
        "Schädelbrand" => new Color(0.44f, 0.20f, 0.14f),      // trübes Rotbraun
        "Papier" => new Color(0.86f, 0.84f, 0.78f),            // helles Grau-Beige
        "Kerze" => new Color(0.93f, 0.82f, 0.38f),             // Wachsgelb
        "Lehrschrift" => new Color(0.45f, 0.30f, 0.18f),       // brauner Einband
        "Hetzschrift" => new Color(0.72f, 0.20f, 0.14f),       // Rot
        "Psalter" => new Color(0.12f, 0.16f, 0.36f),           // Dunkelblau
        "Stundenbuch" => new Color(0.12f, 0.30f, 0.68f),       // Lapisblau (mit Gold)
        "Reliquiar" => new Color(0.60f, 0.10f, 0.28f),         // Rubinrot der Emailfelder (das Gehäuse ist vergoldet)
        _ => Streufarbe(ware),
    };

    /// <summary>
    /// Eine feste Farbe für jede übrige Ware, aus ihrem Namen errechnet. Nicht <c>string.GetHashCode</c> –
    /// der ändert sich in .NET bei jedem Programmstart; diese Rechnung gibt immer dieselbe Farbe.
    /// </summary>
    private static Color Streufarbe(string ware)
    {
        uint h = 17;
        foreach (var zeichen in ware) h = h * 31 + zeichen;
        return Color.FromHsv(h % 360 / 360f, 0.45f, 0.72f);
    }

    /// <summary>
    /// Das Lager als Kistenstapel an der Rückwand des Lagerraums: je Ware eine Spalte, eine Kiste je angefangene
    /// 5 Stück (höchstens 8 = volles Lager). Erst Zutaten, dann das Produkt, dann was sonst im Lager liegt
    /// (im Diebesnest: das Diebesgut).
    /// </summary>
    private void KistenAktualisieren(BetriebsAnsicht ansicht)
    {
        var betrieb = ansicht.Betrieb;
        var waren = betrieb.Zutaten.Keys.Append(betrieb.Produkt).Concat(betrieb.Lager.Keys).Where(x => !string.IsNullOrEmpty(x)).Distinct();
        foreach (var ware in waren)
        {
            if (!ansicht.Kisten.TryGetValue(ware, out var spalte))
            {
                if (ansicht.Kisten.Count >= MaxWarenplaetze) continue;
                spalte = KistenspalteBauen(ansicht, ware, ansicht.Kisten.Count);
                ansicht.Kisten[ware] = spalte;
            }
            var menge = betrieb.ImLager(ware);
            var anzahl = Math.Min(MaxKistenJeWare, (menge + StueckJeKiste - 1) / StueckJeKiste);
            for (var i = 0; i < spalte.Kisten.Length; i++)
                spalte.Kisten[i].Visible = i < anzahl;
            spalte.Schild.Text = $"{ware} {menge}";
            // Schilder abwechselnd hoch und tief, damit sich Nachbarn nicht überdecken
            var versatz = ansicht.Kisten.Keys.ToList().IndexOf(ware) % 2 * 0.9f;
            spalte.Schild.Position = spalte.Schild.Position with { Y = 1.2f + Math.Max(1, (anzahl + 1) / 2) * 0.76f + versatz };
        }
    }

    private (MeshInstance3D[] Kisten, Label3D Schild) KistenspalteBauen(BetriebsAnsicht ansicht, string ware, int platz)
    {
        var material = Material(Warenfarbe(ware));
        // Im Lager an der Rückwand, von links nach rechts
        var basis = ansicht.Mitte + LagerOrt + new Vector3(-3.15f + platz * 1.26f, 0, -2.2f);
        var kisten = new MeshInstance3D[MaxKistenJeWare];
        for (var i = 0; i < kisten.Length; i++)
        {
            // Zwei Reihen tief, bis zu vier Kisten hoch
            var reihe = i % 2;
            var lage = i / 2;
            kisten[i] = Quader(basis + new Vector3(0, 0.1f + 0.36f + lage * 0.76f, reihe * 0.85f - 0.42f),
                               new Vector3(1.05f, 0.72f, 0.8f), material, ansicht.Innen);
        }
        var schild = Beschriftung(ware, basis + new Vector3(0, 2, 0), 34, ansicht.Innen);
        return (kisten, schild);
    }

    /// <summary>
    /// Die Belegschaft als Figuren. Sie sind während der Arbeitszeit (8–20 Uhr) in der Werkstatt und arbeiten,
    /// solange auch wirklich gearbeitet wird (kein Stillstand). Zwei Ausnahmen:
    ///   • Diebesnest: Die Diebe stehlen tagsüber am Markt – nur der Hehler (erster Platz) bleibt; vor 8 und
    ///     nach 20 Uhr sind alle im Versteck.
    ///   • Schänke: Nach Feierabend schenkt der Wirt weiter aus, solange die Gäste kommen (bis 22 Uhr).
    /// </summary>
    private void ArbeiterAktualisieren(BetriebsAnsicht ansicht, Spielzeit zeit, bool arbeitszeit, bool arbeitet)
    {
        var betrieb = ansicht.Betrieb;
        var stand = string.Join(",", betrieb.Belegung.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value}"));
        if (stand != ansicht.Belegungsstand)
        {
            foreach (var figur in ansicht.Arbeiter) figur.Koerper.QueueFree();
            ansicht.Arbeiter.Clear();
            var nummer = 0;
            foreach (var (kraft, anzahl) in betrieb.Belegung.OrderBy(x => x.Key))
                for (var i = 0; i < anzahl && nummer < ansicht.Plaetze.Count; i++, nummer++)
                    ansicht.Arbeiter.Add(ArbeiterBauen(ansicht, kraft, nummer));
            ansicht.Belegungsstand = stand;
        }

        var offen = ansicht.Stil == Werkstil.Schaenke && SchaenkeOffen(zeit.UhrzeitInMinuten);
        for (var i = 0; i < ansicht.Arbeiter.Count; i++)
        {
            var (da, aktiv) = ansicht.Stil switch
            {
                Werkstil.Diebesnest => (!arbeitszeit || i == 0, arbeitszeit && arbeitet),
                Werkstil.Schaenke when !arbeitszeit => (i == 0 && offen, offen),
                _ => (arbeitszeit, arbeitet),
            };
            ansicht.Arbeiter[i].Koerper.Visible = da;
            ArmBewegen(ansicht.Arbeiter[i], aktiv);
        }
    }

    private bool SchaenkeOffen(int uhrzeitInMinuten) =>
        _simulation?.Wirtschaft?.Gastwirtschaft is { IstAktiv: true } g && uhrzeitInMinuten / 60 >= g.Daten.OeffnetUm;

    /// <summary>
    /// Der Arm folgt der Tätigkeit. Die Drehung um X hebt und senkt ihn (0 = waagerecht nach vorn,
    /// negativ = nach unten), die um Y schwenkt ihn zur Seite, die um Z kippt ihn (Laute schlagen).
    /// Der Betrag des Sinus gibt beim Hämmern den „Aufprall".
    /// </summary>
    private void ArmBewegen(Arbeiterfigur figur, bool aktiv)
    {
        var t = (float)_animationszeit + figur.Takt;
        var versatz = Vector3.Zero;
        Vector3 drehung;
        if (!aktiv) drehung = new Vector3(-0.9f, 0, 0);
        else
        {
            switch (figur.Art)
            {
                case Bewegung.Haemmern: drehung = new Vector3(-0.45f + 1.3f * Mathf.Abs(Mathf.Sin(t * 4.5f)), 0, 0); break;
                case Bewegung.Meisseln: drehung = new Vector3(-0.3f + 0.45f * Mathf.Abs(Mathf.Sin(t * 7f)), 0, 0); break;
                case Bewegung.Saegen:
                    drehung = new Vector3(-0.55f, 0, 0);
                    versatz = new Vector3(0, 0, 0.18f * Mathf.Sin(t * 5f)); // vor und zurück
                    break;
                case Bewegung.Ruehren: drehung = new Vector3(-0.75f, 0.45f * Mathf.Sin(t * 2.4f), 0); break;
                case Bewegung.Schreiben: drehung = new Vector3(-0.6f + 0.06f * Mathf.Sin(t * 11f), 0.12f * Mathf.Sin(t * 1.7f), 0); break;
                case Bewegung.Schenken: drehung = new Vector3(-0.9f + 1.2f * Mathf.Max(0, Mathf.Sin(t * 0.9f)), 0, 0); break;
                case Bewegung.Beten: drehung = new Vector3(-0.25f, -0.25f, 0); break;
                case Bewegung.Spielen: drehung = new Vector3(-0.5f, 0, 0.25f * Mathf.Sin(t * 6f)); break;
                default: drehung = new Vector3(-1.3f + 0.05f * Mathf.Sin(t * 1.3f), 0, 0); break; // Stehen
            }
        }
        figur.Arm.Rotation = drehung;
        figur.Arm.Position = figur.ArmRuhe + versatz;
    }
}
