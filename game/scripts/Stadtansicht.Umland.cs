using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Betriebe vor der Stadt – ein Teil der <c>partial class Stadtansicht</c>.
///
/// Grube, Holzschlag, Steinbruch, Kräutergarten, Duftgarten, Bauernhof, Klosterhof und Wachturm sind Modelle aus Blender (<c>grube.glb</c> usw.,
/// welches, sagt der Baustein des Platzes im Gebäudekatalog): die ganze Anlage mit Gelände, Ursprung in der Mitte der Fläche, Vorderseite +Z
/// mit „Anker_Karren“, wo die Karren halten. Lage und Drehung kommen aus der Karte; dort beginnt auch der Weg zur Straße (Stadtansicht.Karte.cs).
/// Im Betrieb stehen „Haus“, „Arbeit“ und „Fahne“ (Besitzerfarbe), auf einem freien Platz „Haus“ und „Leerstand“ (<see cref="ModellEinsetzen"/>).
/// Der Felsberg hinter einer Grube ist ein eigenes Gebäude der Karte. Dazu die Arbeitsstellen der Einwohner (Weide, Feld, Wald) aus
/// <c>umland_stellen.glb</c> (<see cref="UmlandstellenBauen"/>).
/// </summary>
public partial class Stadtansicht
{
    /// <summary>„Anker_Karren“ und „Anker_Tuer“ eines Modells (lokal) und der Raum, den seine Netze einnehmen – je Datei einmal aus der Datei gelesen.</summary>
    private readonly Dictionary<string, (Vector3 Karren, Vector3 Tuer, Aabb Masse)> _umlandAnker = new();

    private (Vector3 Karren, Vector3 Tuer, Aabb Masse) UmlandAnkerImModell(string datei)
    {
        if (_umlandAnker.TryGetValue(datei, out var anker)) return anker;
        if (Modell(datei)?.Instantiate<Node3D>() is { } wurzel)
        {
            anker = (LageImModell(wurzel, "Anker_Karren"), LageImModell(wurzel, "Anker_Tuer"), MasseImModell(wurzel));
            wurzel.Free();
        }
        return _umlandAnker[datei] = anker;
    }

    /// <summary>Wo ein benannter Knoten im Modell liegt (die Verschiebungen aller Elternknoten bis zur Wurzel eingerechnet).</summary>
    private static Vector3 LageImModell(Node3D wurzel, string name)
    {
        if (wurzel.FindChild(name, true, false) is not Node3D knoten) return Vector3.Zero;
        return LageUnter(wurzel, knoten).Origin;
    }

    private static Transform3D LageUnter(Node3D wurzel, Node3D knoten)
    {
        var lage = Transform3D.Identity;
        for (Node? n = knoten; n is Node3D k && k != wurzel; n = n.GetParent()) lage = k.Transform * lage;
        return lage;
    }

    /// <summary>Der Quader um alle Netze des Modells (lokal) – so groß ist das Modell wirklich, oft größer als die belegte Fläche im Katalog.</summary>
    private static Aabb MasseImModell(Node3D wurzel)
    {
        Aabb? masse = null;
        foreach (var teil in wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            var kasten = LageUnter(wurzel, teil) * teil.GetAabb();
            masse = masse is { } m ? m.Merge(kasten) : kasten;
        }
        return masse ?? new Aabb();
    }

    /// <summary>Der Halteplatz der Karren eines Umlandplatzes der Karte in Stadtkoordinaten („Anker_Karren“ des Modells) – null ohne Modell.</summary>
    private Vector3? UmlandHof(Kartengebaeude platz) =>
        platz.Baustein?.Modell is { } datei ? ModellLage(platz) * UmlandAnkerImModell(datei).Karren : null;

    /// <summary>
    /// Baustein und Lage des Modells eines Umlandbetriebs: auf seinem Platz der Karte dessen Baustein; ohne Platz (wenn es das je gibt) der Baustein
    /// der Betriebsart aus dem Katalog, ungedreht auf der Fläche.
    /// </summary>
    private (Baustein? Baustein, Transform3D Lage) UmlandModell(Platz p, Betriebsart art) =>
        p.Gebaeude is { Baustein: { } b } g ? (b, ModellLage(g)) : (Katalog?.Umland(art), new Transform3D(p.Basis, p.Mitte));

    // ═════════════════════════════════════════════════════════════════════════
    //  Betrieb
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ein Umlandbetrieb als Modell: „Haus“, „Arbeit“ und „Fahne“ in Besitzerfarbe. Klickfläche über dem Modell (2 m kleiner als das Modell, so hoch
    /// wie im Katalog), Schild darüber; die Karren fahren vom „Anker_Karren“ über das Gassennetz zum Markt.
    /// </summary>
    private BetriebsAnsicht UmlandBauen(Betrieb betrieb, Platz p, Color besitzer)
    {
        var (baustein, lage) = UmlandModell(p, betrieb.Art);
        var gruppe = new Node3D { Name = "Umland", Transform = lage };
        AddChild(gruppe);
        var datei = baustein?.Modell ?? "umland/grube";
        var modell = ModellEinsetzen(datei, gruppe, Vector3.Zero, new ModellZustand(Besitzer: besitzer), betrieb.Name);
        var (anker, _, masse) = UmlandAnkerImModell(datei);

        var kennung = BetriebsKennung(betrieb);
        var klick = (float)(baustein?.KlickHoehe ?? 9);
        Klickflaeche(kennung, new Vector3(masse.GetCenter().X, klick / 2, masse.GetCenter().Z), new Vector3(masse.Size.X - 2, klick, masse.Size.Z - 2), gruppe);
        var schild = Beschriftung(betrieb.Name, p.Mitte + new Vector3(0, (float)(baustein?.SchildHoehe ?? 10.5), 0), 44, kennung: kennung);

        var hof = lage * anker;
        var markt = MarktFuer(betrieb);
        var netzweg = MarktWeg(hof, KarrenwegBreite, markt);
        var kernweg = KernWeg(betrieb); // vom Halteplatz bis zum Halt am Platz, wie der Kern die Fahrzeit rechnet
        return new BetriebsAnsicht
        {
            Betrieb = betrieb,
            Mitte = p.Mitte,
            Hof = hof,
            Markt = markt,
            WegZumMarkt = kernweg ?? netzweg,
            WegVomKern = kernweg is not null,
            Modell = modell,
            Schild = schild,
            KarrenReihe = lage.Basis.Orthonormalized() * new Vector3(anker.X <= 0 ? 4 : -4, 0, 0),
        };
    }

    /// <summary>
    /// Wo der Verkaufsschild-Pfosten eines freien Platzes steht (lokal im gedrehten Modell): neben dem Ende des Weges, ein Stück vor dem
    /// Halteplatz, zur Seite hin, wo mehr Platz ist – so verdeckt er weder den Weg noch das Tor.
    /// </summary>
    private Vector3 UmlandSchildFuss(string datei)
    {
        var anker = UmlandAnkerImModell(datei).Karren;
        return new Vector3(anker.X + (anker.X > 0 ? 3.5f : -3.5f), 0, anker.Z + 1.2f);
    }

    /// <summary>
    /// Ein freier Platz vor der Stadt: das Modell mit „Haus“ und „Leerstand“ (Bretter, Unkraut, Brache), dazu das Schild „zu verpachten“ bzw. „zu
    /// verkaufen“ am Halteplatz. Alles hängt an <paramref name="gruppe"/>, damit es beim Erwerb in einem Zug verschwindet.
    /// </summary>
    private void UmlandPlatzZeigen(LiegenschaftDaten frei, Platz p, Node3D gruppe)
    {
        var (baustein, lage) = UmlandModell(p, frei.Art);
        var datei = baustein?.Modell ?? "umland/grube";
        var modell = new Node3D { Name = "Umland", Transform = lage };
        gruppe.AddChild(modell);
        ModellEinsetzen(datei, modell, Vector3.Zero, new ModellZustand(Leerstand: true));
        Verkaufsschild(UmlandSchildFuss(datei), frei.IstPacht, modell);

        var kennung = LiegenschaftKennung(frei.Name);
        var masse = UmlandAnkerImModell(datei).Masse;
        var klick = (float)(baustein?.KlickHoehe ?? 9);
        Klickflaeche(kennung, new Vector3(masse.GetCenter().X, klick / 2, masse.GetCenter().Z), new Vector3(masse.Size.X - 2, klick, masse.Size.Z - 2), modell);
        Beschriftung($"{frei.Name}\n{(frei.IstPacht ? "zu verpachten" : "zu verkaufen")}", p.Mitte + new Vector3(0, (float)(baustein?.SchildHoehe ?? 10.5), 0), 36, gruppe, kennung);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Arbeitsstellen der Einwohner
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die Arbeitsstellen der Einwohner (Bausteine der Art „umlandstelle“): Weide (Koppel, Gatter, Tränke), Feld (Furchen, Vogelscheuche) und Wald
    /// (Tannen, Linde, Reisig) sind Knoten eines Baukastens (<c>umland_stellen.glb</c>); jeder steht an seiner Lage in der Karte, die Vorderseite
    /// (+Z, dort liegt „Anker_Tuer“, das Gatter) zum Weg. Das Gatter hängt mit einem Fußweg am nächsten Weg, im Netz geht es weiter bis zur Mitte
    /// der Stelle. Die Bäume des Waldes kommen aus dem Modell; um die Stelle wächst sonst nichts (<see cref="Naturfreiraum"/>).
    /// </summary>
    private void UmlandstellenBauen(Stadtkarte karte)
    {
        var kaesten = new Dictionary<string, Node3D?>();
        foreach (var g in karte.GebaeudeDerArt(Bausteinart.Umlandstelle))
        {
            if (g.Baustein is not { Modell: { } datei } b) continue;
            if (!kaesten.TryGetValue(datei, out var kasten)) kaesten[datei] = kasten = Modell(datei)?.Instantiate<Node3D>();
            if (kasten is null) continue;
            var knotenName = b.Knoten ?? g.Name;
            if (datei.StartsWith("wasser/"))
            {
                // der Hafen: das ganze Modell (Kai, Tretrad, Kran, Ware, Kähne) und nicht nur sein Knoten „Hafen“ – Tretrad und Kähne bewegt Stadtansicht.Wasser.Uebergaenge.cs
                if (ModellEinsetzen(datei, this, Vector3.Zero, new ModellZustand()) is { } hafen)
                {
                    hafen.Wurzel.Name = "Hafen_" + g.Kennung;
                    hafen.Wurzel.Transform = ModellLage(g);
                    HafenKlickbar(g, hafen.Wurzel); // ein Klick öffnet die Hafentafel (Stadtansicht.Schiffe.cs)
                }
                // keine Arbeitsstelle der Einwohner (noch keine Wirtschaft am Hafen): nicht in _umlandstellen, nicht am Gassennetz
                continue;
            }
            if (kasten.FindChild(knotenName, true, false) is not MeshInstance3D { Mesh: { } netz } teil)
            {
                GD.PushError($"Modell unvollständig: res://assets/modelle/{datei}.glb hat keinen Knoten „{knotenName}“.");
                continue;
            }
            LaubMelden(netz);
            var lage = ModellLage(g);
            var stelleKnoten = new MeshInstance3D { Name = "Stelle_" + g.Kennung, Mesh = netz, Transform = lage };
            // auch die Kinder kommen mit (der Wald trägt „Laubkrone_Wald“ und „Winteraeste_Wald“), nicht nur sein Netz
            foreach (var kind in teil.GetChildren().OfType<MeshInstance3D>())
                stelleKnoten.AddChild(new MeshInstance3D { Name = kind.Name, Mesh = kind.Mesh, Transform = kind.Transform });
            AddChild(stelleKnoten);
            KronenMelden(stelleKnoten);
            _umlandstellen[g.Name] = g;

            // Das Gatter („Anker_Tuer“ des Baukastens) am Weg; im Netz endet der Gang in der Mitte der Stelle, wo die Arbeit ist
            var gatter = lage * UmlandAnkerImModell(datei).Tuer;
            AnWegAnschliessen(gatter, FusswegBreite);
            _netz.Weg(gatter with { Y = 0 }, Punkt3(g.Punkt));
        }
        foreach (var kasten in kaesten.Values) kasten?.Free();
    }
}
