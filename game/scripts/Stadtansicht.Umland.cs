using Godot;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Betriebe vor der Stadt – ein Teil der <c>partial class Stadtansicht</c>.
///
/// Grube, Holzschlag, Steinbruch, Kräutergarten, Duftgarten, Bauernhof, Klosterhof und Wachturm sind Modelle aus Blender
/// (<c>grube.glb</c> usw.): die ganze Anlage mit Gelände, Ursprung in der Mitte der Fläche, Vorderseite +Z mit „Anker_Karren“, wo der Weg endet
/// und die Karren halten. Das Modell dreht sich um seine Mitte, sodass die Vorderseite zum Abzweig der Straße zeigt (<see cref="UmlandDrehung"/>).
/// Im Betrieb stehen „Haus“, „Arbeit“ und „Fahne“ (Besitzerfarbe), auf einem freien Platz „Haus“ und „Leerstand“ (<see cref="ModellEinsetzen"/>).
/// Dazu die Arbeitsstellen der Einwohner (Weide, Feld, Wald) aus <c>umland_stellen.glb</c> (<see cref="UmlandstellenBauen"/>).
/// </summary>
public partial class Stadtansicht
{
    /// <summary>
    /// Ein Umlandmodell: Datei, belegte Fläche (Breite × Tiefe in Metern), die Höhe der Klickfläche (die Fläche selbst ist 2 m kleiner als das Modell)
    /// und die Höhe des Schildes (das Modell ragt knapp darunter hinaus).
    /// </summary>
    private sealed record UmlandModell(string Datei, Vector2 Flaeche, float KlickHoehe, float SchildHoehe);

    private static readonly Dictionary<Betriebsart, UmlandModell> UmlandModelle = new()
    {
        [Betriebsart.Mine] = new("grube", new(22, 18), 9, 10.5f),
        [Betriebsart.Holzfaellerei] = new("holzschlag", new(22, 18), 10, 11.5f),
        [Betriebsart.Steinbruch] = new("steinbruch", new(22, 18), 8, 9.5f),
        [Betriebsart.Kraeutergarten] = new("kraeutergarten", new(20, 16), 7, 8.5f),
        [Betriebsart.Duftgarten] = new("duftgarten", new(20, 16), 6, 7.5f),
        [Betriebsart.Bauernhof] = new("bauernhof", new(26, 20), 11.5f, 13),
        [Betriebsart.Klosterhof] = new("klosterhof", new(22, 18), 10, 11.5f),
        [Betriebsart.Wachturm] = new("wachturm", new(12, 12), 15, 16.5f),
    };

    /// <summary>Welches Umlandmodell zu einem Platz im Stadtplan gehört, nach dem Anfang seines Namens („Grube Eisenbart“ → Grube) – null für alles andere.</summary>
    private static Betriebsart? UmlandArtVon(string name) => name.Split(' ')[0] switch
    {
        "Grube" => Betriebsart.Mine,
        "Holzschlag" => Betriebsart.Holzfaellerei,
        "Steinbruch" => Betriebsart.Steinbruch,
        "Kräutergarten" => Betriebsart.Kraeutergarten,
        "Duftgarten" => Betriebsart.Duftgarten,
        "Bauernhof" => Betriebsart.Bauernhof,
        "Klosterhof" => Betriebsart.Klosterhof,
        "Wachturm" => Betriebsart.Wachturm,
        _ => null,
    };

    /// <summary>
    /// Plätze, deren Vorderseite von Hand ausgerichtet ist (Grad um die Hochachse, 0 = nach +Z/Süden, −90 = nach −X/Westen): Beim Silberberg zeigt der Abzweig
    /// schräg (−14|−14), und der Berg hinter dem Modell käme dem Kräutergarten in die Quere – so liegt er im freien Osten.
    /// </summary>
    private static readonly Dictionary<string, float> UmlandDrehungGrad = new() { ["Grube am Silberberg"] = -90 };

    /// <summary>
    /// Die Drehung eines Umlandmodells um die Hochachse (Bogenmaß): Seine Vorderseite (+Z) zeigt vom Platz <paramref name="p"/> zum Abzweig, wo der Weg
    /// von der Straße ankommt.
    /// </summary>
    private static float UmlandDrehung(string name, Vector3 p)
    {
        if (UmlandDrehungGrad.TryGetValue(name, out var grad)) return Mathf.DegToRad(grad);
        var richtung = Abzweige.GetValueOrDefault(name, Wegkreuz + new Vector3(10, 0, 0)) - p;
        return Mathf.Atan2(richtung.X, richtung.Z);
    }

    /// <summary>„Anker_Karren“ und „Anker_Tuer“ der Modelle (lokal), je Datei einmal aus der Datei gelesen.</summary>
    private readonly Dictionary<string, (Vector3 Karren, Vector3 Tuer)> _umlandAnker = new();

    private (Vector3 Karren, Vector3 Tuer) UmlandAnkerImModell(string datei)
    {
        if (_umlandAnker.TryGetValue(datei, out var anker)) return anker;
        if (Modell(datei)?.Instantiate<Node3D>() is { } wurzel)
        {
            anker = (LageImModell(wurzel, "Anker_Karren"), LageImModell(wurzel, "Anker_Tuer"));
            wurzel.Free();
        }
        return _umlandAnker[datei] = anker;
    }

    /// <summary>Wo ein benannter Knoten im Modell liegt (die Verschiebungen aller Elternknoten bis zur Wurzel eingerechnet).</summary>
    private static Vector3 LageImModell(Node3D wurzel, string name)
    {
        if (wurzel.FindChild(name, true, false) is not Node3D knoten) return Vector3.Zero;
        var lage = Transform3D.Identity;
        for (Node? n = knoten; n is Node3D k && k != wurzel; n = n.GetParent()) lage = k.Transform * lage;
        return lage.Origin;
    }

    /// <summary>Der Halteplatz der Karren eines Platzes in Stadtkoordinaten: dort endet der Weg von der Straße. Gilt auch vor dem Bau (Gassennetz, freier Platz).</summary>
    private Vector3 UmlandHof(string name, Vector3 p, Betriebsart art) =>
        p + new Basis(Vector3.Up, UmlandDrehung(name, p)) * UmlandAnkerImModell(UmlandModelle[art].Datei).Karren;

    /// <summary>Ein gedrehter Knoten an der Stelle des Platzes, in den das Modell kommt (dreht sich samt Berg, Klickfläche und Schild-Pfosten mit).</summary>
    private Node3D UmlandGruppe(string name, Vector3 p, Node3D? eltern = null)
    {
        var gruppe = new Node3D { Name = "Umland", Position = p, Rotation = new Vector3(0, UmlandDrehung(name, p), 0) };
        (eltern ?? this).AddChild(gruppe);
        return gruppe;
    }

    /// <summary>
    /// Der Weg von der Straße zum Halteplatz (breit 4 m); liegt der Abzweig nicht an der Straße zum Tor, kommt ein Weg zum Kreuz dazu.
    /// </summary>
    private void UmlandWegZiehen(string name, Vector3 abzweig, Vector3 hof, Node3D? eltern = null)
    {
        WegZiehen(abzweig, hof, 4, eltern);
        if (!AnDerStrasse(name, abzweig)) // nicht an der Straße? Dann eigene Verbindung zum Kreuz
            WegZiehen(Kreuz(name), abzweig, 4, eltern);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Betrieb
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ein Umlandbetrieb als Modell: „Haus“, „Arbeit“ und „Fahne“ in Besitzerfarbe; bei der Grube dahinter der Berg. Klickfläche über dem Modell, Schild
    /// darüber, Weg von der Straße bis zum „Anker_Karren“.
    /// </summary>
    private BetriebsAnsicht UmlandBauen(Betrieb betrieb, Vector3 p, Color besitzer, Vector3 abzweig)
    {
        var art = UmlandModelle.GetValueOrDefault(betrieb.Art, UmlandModelle[Betriebsart.Mine]);
        var gruppe = UmlandGruppe(betrieb.Name, p);
        var modell = ModellEinsetzen(art.Datei, gruppe, Vector3.Zero, new ModellZustand(Besitzer: besitzer), betrieb.Name);
        if (betrieb.Art == Betriebsart.Mine) BergBauen(gruppe);

        var kennung = BetriebsKennung(betrieb);
        Klickflaeche(kennung, new Vector3(0, art.KlickHoehe / 2, 0), new Vector3(art.Flaeche.X - 2, art.KlickHoehe, art.Flaeche.Y - 2), gruppe);
        var schild = Beschriftung(betrieb.Name, p + new Vector3(0, art.SchildHoehe, 0), 44, kennung: kennung);

        var hof = UmlandHof(betrieb.Name, p, betrieb.Art);
        UmlandWegZiehen(betrieb.Name, abzweig, hof);
        return new BetriebsAnsicht
        {
            Betrieb = betrieb,
            Mitte = p,
            Hof = hof,
            // der Halteplatz kommt von BetriebHinzufuegen aus dem „Anker_Karren“ dazu; hier beginnt der Weg beim Abzweig
            WegZumMarkt = WegZumMarktVon(betrieb.Name, hof, abzweig).Skip(1).ToArray(),
            Modell = modell,
            Schild = schild,
            KarrenReihe = UmlandKarrenReihe(art, gruppe.Basis),
        };
    }

    /// <summary>
    /// Mehrere Karren eines Betriebs stehen nebeneinander am Halteplatz, 3 m auseinander – entlang der Vorderkante des Modells, zur freieren Seite hin.
    /// </summary>
    private Vector3 UmlandKarrenReihe(UmlandModell art, Basis drehung)
    {
        var anker = UmlandAnkerImModell(art.Datei).Karren;
        return drehung * new Vector3(anker.X <= 0 ? 3 : -3, 0, 0);
    }

    /// <summary>
    /// Wo der Verkaufsschild-Pfosten eines freien Platzes steht (lokal im gedrehten Modell): neben dem Ende des Weges, ein Stück vor dem
    /// Halteplatz, zur Seite hin, wo mehr Platz ist – so verdeckt er weder den Weg noch das Tor.
    /// </summary>
    private Vector3 UmlandSchildFuss(UmlandModell art)
    {
        var anker = UmlandAnkerImModell(art.Datei).Karren;
        return new Vector3(anker.X + (anker.X > 0 ? 3.5f : -3.5f), 0, anker.Z + 1.2f);
    }

    /// <summary>
    /// Ein freier Platz vor der Stadt: das Modell mit „Haus“ und „Leerstand“ (Bretter, Unkraut, Brache), dazu das Schild „zu verpachten“ bzw. „zu
    /// verkaufen“ am Halteplatz. Alles hängt an <paramref name="gruppe"/>, damit es beim Erwerb in einem Zug verschwindet.
    /// </summary>
    private void UmlandPlatzZeigen(LiegenschaftDaten frei, Vector3 p, Node3D gruppe)
    {
        var art = UmlandModelle.GetValueOrDefault(frei.Art, UmlandModelle[Betriebsart.Mine]);
        var modell = UmlandGruppe(frei.Name, p, gruppe);
        ModellEinsetzen(art.Datei, modell, Vector3.Zero, new ModellZustand(Leerstand: true));
        if (frei.Art == Betriebsart.Mine) BergBauen(modell);

        var abzweig = Abzweige.GetValueOrDefault(frei.Name, Wegkreuz + new Vector3(10, 0, 0));
        UmlandWegZiehen(frei.Name, abzweig, UmlandHof(frei.Name, p, frei.Art), gruppe);
        Verkaufsschild(UmlandSchildFuss(art), frei.IstPacht, modell);

        var kennung = LiegenschaftKennung(frei.Name);
        Klickflaeche(kennung, new Vector3(0, art.KlickHoehe / 2, 0), new Vector3(art.Flaeche.X - 2, art.KlickHoehe, art.Flaeche.Y - 2), modell);
        Beschriftung($"{frei.Name}\n{(frei.IstPacht ? "zu verpachten" : "zu verkaufen")}", p + new Vector3(0, art.SchildHoehe, 0), 36, gruppe, kennung);
    }

    /// <summary>
    /// Der Berg der Grube – viel größer als die Fläche des Modells, darum bleibt er im Code: zwei abgeflachte Kugeln hinter dem Modell (lokal −Z, also von der Straße
    /// abgewandt), die den Felshang des Modells fortsetzen.
    /// </summary>
    private void BergBauen(Node3D eltern)
    {
        foreach (var (versatz, radius) in new[] { (new Vector3(2, 0, -22), 17f), (new Vector3(-12, 0, -16), 12f) })
        {
            eltern.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = radius, Height = radius * 1.3f, RadialSegments = 12, Rings = 6 },
                MaterialOverride = Material(Fels),
                Position = versatz,
            });
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Arbeitsstellen der Einwohner
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Weide (Koppel, Gatter, Tränke), Feld (Furchen, Vogelscheuche) und Wald (Tannen, Linde, Reisig) aus <c>umland_stellen.glb</c>: je ein Knoten des
    /// Modells an der Stelle in <see cref="Umlandstellen"/>, die Vorderseite (+Z, dort liegt „Anker_Tuer“) zum Zugang von der Weststraße gedreht.
    /// Die Bäume des Waldes kommen aus dem Modell; <see cref="ImWegFuerWald"/> hält die Fläche frei.
    /// </summary>
    private void UmlandstellenBauen()
    {
        if (Modell("umland_stellen")?.Instantiate<Node3D>() is not { } kasten) return;
        foreach (var (name, stelle) in Umlandstellen)
        {
            if ((kasten.FindChild(name, true, false) as MeshInstance3D)?.Mesh is not { } netz)
            {
                GD.PushError($"Modell unvollständig: res://assets/modelle/umland_stellen.glb hat keinen Knoten „{name}“.");
                continue;
            }
            LaubMelden(netz);
            AddChild(new MeshInstance3D { Name = "Stelle_" + name, Mesh = netz, Position = stelle, Rotation = new Vector3(0, UmlandstelleDrehung(name), 0) });
        }
        kasten.Free();
    }

    /// <summary>Die Drehung einer Arbeitsstelle (Bogenmaß): Ihre Vorderseite blickt zum Zugang von der Weststraße (Weide und Feld liegen südlich davon, also um 180°).</summary>
    private static float UmlandstelleDrehung(string name)
    {
        var richtung = UmlandZugaenge[name] - Umlandstellen[name];
        return Mathf.Atan2(richtung.X, richtung.Z);
    }
}
