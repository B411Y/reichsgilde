using Godot;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die Natur vor der Stadt – ein Teil der <c>partial class Stadtansicht</c>.
///
/// Wald und Streu kommen aus den Baukästen <c>natur_baeume</c>, <c>natur_buesche</c>, <c>natur_boden</c>, <c>natur_steine</c> und <c>natur_holz</c>
/// (je Art und Variante ein Knoten, der Fuß im Nullpunkt; Konventionen in <c>docs/bereiche/darstellung.md</c>). Alles steht fest aus einem Startwert
/// (gleich bei jedem Start), jede Art ist ein <see cref="MultiMeshInstance3D"/>: Hunderte Teile kosten nur ein paar Zeichenaufrufe.
/// Dazu stellt <see cref="JahreszeitSetzen"/> die Jahreszeit ein: Das Laub aller Modelle färbt sich um (Material „Laub“, „Laub hell“, „Laub dunkel“),
/// im Winter zeigen Bäume und Büsche ihre kahlen „…_Winter“-Fassungen bzw. „Winteraeste_…“ statt der Laubkrone.
/// </summary>
public partial class Stadtansicht
{
    // ═════════════════════════════════════════════════════════════════════════
    //  Sammeln und aufbauen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Netze der Baukästen, je Datei und Knoten einmal geholt.</summary>
    private readonly Dictionary<(string Datei, string Knoten), Mesh?> _naturNetze = new();

    /// <summary>Wo welche Art steht: Lage (Ort, Drehung, Größe) aller Kopien, bis <see cref="NaturAufbauen"/> sie in je ein MultiMesh packt.</summary>
    private readonly Dictionary<(string Datei, string Knoten), List<Transform3D>> _naturLagen = new();

    /// <summary>Die MultiMeshes der Arten mit kahler Fassung: das Netz im Sommer und das im Winter (<see cref="JahreszeitSetzen"/> tauscht).</summary>
    private readonly List<(MultiMesh Vielfach, Mesh Sommer, Mesh Winter)> _naturWinter = new();

    private Mesh? NaturNetz(string datei, string knoten, bool pflicht = true)
    {
        if (_naturNetze.TryGetValue((datei, knoten), out var netz)) return netz;
        if (Modell(datei)?.Instantiate<Node3D>() is { } kasten)
        {
            netz = (kasten.FindChild(knoten, true, false) as MeshInstance3D)?.Mesh;
            kasten.Free();
            if (netz is null) { if (pflicht) GD.PushError($"Modell unvollständig: res://assets/modelle/{datei}.glb hat keinen Knoten „{knoten}“."); }
            else LaubMelden(netz);
        }
        return _naturNetze[(datei, knoten)] = netz;
    }

    /// <summary>Merkt eine Kopie der Art vor: Drehung um die Hochachse und Größe (ein Faktor für alle Richtungen).</summary>
    private void NaturSetzen(string datei, string knoten, Vector3 ort, float drehung, float groesse)
    {
        if (!_naturLagen.TryGetValue((datei, knoten), out var lagen)) _naturLagen[(datei, knoten)] = lagen = new();
        lagen.Add(new Transform3D(new Basis(Vector3.Up, drehung).Scaled(Vector3.One * groesse), ort));
    }

    /// <summary>
    /// Packt alles Vorgemerkte in je ein MultiMesh je Art. Bodenpflanzen werfen keinen Schatten (zu klein, die Karte der Sonne wäre vergeudet).
    /// </summary>
    private void NaturAufbauen()
    {
        var natur = new Node3D { Name = "Natur" };
        AddChild(natur);
        foreach (var ((datei, knoten), lagen) in _naturLagen)
        {
            if (NaturNetz(datei, knoten) is not { } netz) continue;
            var vielfach = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = netz, InstanceCount = lagen.Count };
            for (var i = 0; i < lagen.Count; i++) vielfach.SetInstanceTransform(i, lagen[i]);
            // Bäume und Büsche haben eine kahle Fassung „<Name>_Winter“ (Tanne, Ginster und Wacholder nicht): im Winter tauscht JahreszeitSetzen das Netz
            if ((datei is "natur_baeume" or "natur_buesche") && NaturNetz(datei, knoten + "_Winter", pflicht: false) is { } winter)
            {
                _naturWinter.Add((vielfach, netz, winter));
                if (_jahreszeit == Jahreszeit.Winter) vielfach.Mesh = winter;
            }
            natur.AddChild(new MultiMeshInstance3D
            {
                Name = knoten,
                Multimesh = vielfach,
                CastShadow = datei == "natur_boden" ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On,
            });
        }
        _naturLagen.Clear();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Wald und Streu
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Der Wald im Ring um die Stadt und lockere Gruppen von Büschen, Bodenpflanzen, Steinen und Totholz dazwischen; freie Wiesen bleiben.
    /// Die Orte der Bäume kommen aus dem Startwert 1400 (wie die alten Kegel), Art, Drehung und Größe aus einem zweiten Zufall, damit sich
    /// die Orte nicht verschieben, wenn die Wahl der Art sich ändert.
    /// </summary>
    private void Wald()
    {
        var zufall = new Random(1400);
        var wahl = new Random(2600);
        for (var i = 0; i < 70; i++)
        {
            var winkel = zufall.NextDouble() * Math.Tau;
            var abstand = 48 + zufall.NextDouble() * 50;
            var p = NachAussen(new Vector3((float)(Math.Cos(winkel) * abstand) + 10, 0, (float)(Math.Sin(winkel) * abstand)));
            var (knoten, drehung, groesse) = BaumWahl(p, wahl);
            if (!NaturFrei(p)) continue; // Stadt, Gruben, Straßen und die Bauzonen der Karte bleiben frei
            NaturSetzen("natur_baeume", knoten, p, drehung, groesse);
        }
        NaturStreuen();
        NaturAufbauen();
    }

    /// <summary>
    /// Welcher Baum steht hier? Tannen überall (die drei Höhen von 6 bis 11 m wie die alten Kegel), im Westen dazwischen Eichen und Buchen.
    /// Jeder Aufruf zieht immer gleich viele Zahlen, damit die Folge der Wahl nicht davon abhängt, wo ein Baum frei steht.
    /// </summary>
    private static (string Knoten, float Drehung, float Groesse) BaumWahl(Vector3 p, Random wahl)
    {
        var art = wahl.NextDouble();
        var nummer = wahl.Next(3) + 1;
        var drehung = (float)(wahl.NextDouble() * Math.Tau);
        var groesse = 0.85f + (float)wahl.NextDouble() * 0.3f;
        var knoten = p.X < -20 && art < 0.30 ? "Eiche" + (nummer % 2 + 1)
                   : p.X < -20 && art < 0.45 ? "Buche" + (nummer % 2 + 1)
                   : "Tanne" + nummer;
        return (knoten, drehung, groesse);
    }

    /// <summary>Steht hier nichts im Weg (Stadt, Wege, Betriebe, Umlandstellen, Bauzonen der Karte)? Dann darf etwas wachsen.</summary>
    private static bool NaturFrei(Vector3 p) => !ImWegFuerWald(p) && !AufBauzone(p);

    private static readonly string[] Straeucher = { "Hasel", "Holunder", "Brombeere", "Ginster", "Wacholder" };
    private static readonly string[] Bodenpflanzen = { "Farn", "Gras", "Brennnessel", "Pilze", "Wildblumen", "Maulwurfshuegel" };
    private static readonly string[] Steine = { "Felsbrocken", "Findling", "Kiesel", "Lesesteinhaufen" };
    private static readonly string[] Totholz = { "Stumpf", "Totholz", "Reisighaufen" };

    /// <summary>
    /// Wie viele Varianten („Gras1“, „Gras2“ …) das Spiel von einer Art zeigt – alle anderen nur die erste. Jede gezeigte Variante ist ein eigenes
    /// MultiMesh mit je einem Zeichenaufruf je Material (und noch einmal im Schatten); Drehung und Größe bringen die Abwechslung billiger.
    /// </summary>
    private static readonly Dictionary<string, int> Varianten = new()
    {
        ["Tanne"] = 3, ["Eiche"] = 2, ["Buche"] = 2, ["Kopfweide"] = 2, ["Hasel"] = 2, ["Holunder"] = 2, ["Gras"] = 2, ["Felsbrocken"] = 2, ["Findling"] = 2,
    };

    /// <summary>
    /// Verteilt locker Gruppen von Büschen, Steinen, Totholz und Wiesenpflanzen über das Umland (nicht in der Stadt, nicht auf Wegen und Plätzen)
    /// und ein paar Kopfweiden mit Schilf bei den Beeten am Bach.
    /// </summary>
    private void NaturStreuen()
    {
        var zufall = new Random(3100);
        string Eine(string[] namen) => namen[zufall.Next(namen.Length)];

        // Ringsum im Umland: Gruppen an zufälligen Stellen, die freie Wiese dazwischen bleibt
        for (var versuch = 0; versuch < 220; versuch++)
        {
            var mitte = new Vector3(-150 + (float)zufall.NextDouble() * 330, 0, -115 + (float)zufall.NextDouble() * 230);
            var art = zufall.NextDouble();
            var erster = Eine(Straeucher);
            var boden = Eine(Bodenpflanzen);
            var stein = Eine(Steine);
            var holz = Eine(Totholz);
            if (!NaturFrei(mitte)) continue;
            if (art < 0.34) Gruppe(zufall, mitte, 3.5f, ("natur_buesche", erster, 2, 4), ("natur_boden", boden, 2, 4));
            else if (art < 0.54) Gruppe(zufall, mitte, 2.5f, ("natur_steine", stein, 2, 4), ("natur_boden", "Gras", 1, 3));
            else if (art < 0.68) Gruppe(zufall, mitte, 2.5f, ("natur_holz", holz, 1, 3), ("natur_boden", "Farn", 1, 3), ("natur_boden", "Pilze", 0, 2));
            else Gruppe(zufall, mitte, 4f, ("natur_boden", "Wildblumen", 2, 5), ("natur_boden", "Gras", 3, 6), ("natur_boden", "Maulwurfshuegel", 0, 1));
        }

        // Am Bach beim Kräutergarten: Kopfweiden und Schilf
        var bach = Stadtplan["Kräutergarten am Bach"];
        for (var k = 0; k < 4; k++)
        {
            var winkel = 2.3 + k * 0.8;
            var mitte = bach + new Vector3((float)Math.Cos(winkel), 0, (float)Math.Sin(winkel)) * (23 + (float)zufall.NextDouble() * 5);
            if (!NaturFrei(mitte)) continue;
            Gruppe(zufall, mitte, 3f, ("natur_baeume", "Kopfweide", 1, 2), ("natur_boden", "Schilf", 4, 7), ("natur_boden", "Gras", 1, 3));
        }
    }

    /// <summary>
    /// Eine Gruppe: von jedem Eintrag (Datei, Knoten ohne Nummer, wenigstens, höchstens) eine Anzahl Kopien in einer Scheibe um
    /// <paramref name="mitte"/>. Kopien auf Wegen oder Plätzen entfallen einzeln.
    /// </summary>
    private void Gruppe(Random zufall, Vector3 mitte, float radius, params (string Datei, string Knoten, int Von, int Bis)[] teile)
    {
        foreach (var (datei, knoten, von, bis) in teile)
        {
            var anzahl = von + zufall.Next(bis - von + 1);
            for (var i = 0; i < anzahl; i++)
            {
                var winkel = zufall.NextDouble() * Math.Tau;
                var abstand = Math.Sqrt(zufall.NextDouble()) * radius;
                var p = mitte + new Vector3((float)(Math.Cos(winkel) * abstand), 0, (float)(Math.Sin(winkel) * abstand));
                var nummer = zufall.Next(Varianten.GetValueOrDefault(knoten, 1)) + 1;
                var drehung = (float)(zufall.NextDouble() * Math.Tau);
                var groesse = 0.85f + (float)zufall.NextDouble() * 0.3f;
                if (!NaturFrei(p)) continue;
                NaturSetzen(datei, knoten + nummer, p, drehung, groesse);
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Jahreszeit
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Das Laub aller geladenen Modelle: das Material und seine Sommerfarbe (so wie das Modell es mitbringt).</summary>
    private readonly Dictionary<BaseMaterial3D, Color> _laubStoffe = new();
    private Jahreszeit _jahreszeit = Jahreszeit.Sommer;

    /// <summary>Herbstfarben des Laubs (sRGB): „Laub hell“ gelb, „Laub“ orange, „Laub dunkel“ braun.</summary>
    private static readonly Dictionary<string, Color> Herbstlaub = new()
    {
        ["Laub hell"] = new Color(0.86f, 0.70f, 0.22f),
        ["Laub"] = new Color(0.78f, 0.40f, 0.12f),
        ["Laub dunkel"] = new Color(0.50f, 0.28f, 0.13f),
    };

    /// <summary>
    /// Winterfarben (sRGB) für das Laub, das im Winter noch dran ist, wenn die Kronen kahl sind – Buchshecken, Beete, Weide, Grasbüschel und Marktware:
    /// verdorrt und matt (Stroh, Lehm, Moor) statt herbstlich bunt.
    /// </summary>
    private static readonly Dictionary<string, Color> Winterlaub = new()
    {
        ["Laub hell"] = new Color(0.60f, 0.54f, 0.36f),
        ["Laub"] = new Color(0.40f, 0.34f, 0.23f),
        ["Laub dunkel"] = new Color(0.29f, 0.25f, 0.19f),
    };

    /// <summary>Merkt ein Laubmaterial vor und färbt es sofort nach der gerade geltenden Jahreszeit. Andere Materialien bleiben unberührt.</summary>
    private void LaubMelden(BaseMaterial3D stoff)
    {
        if (!Herbstlaub.ContainsKey(stoff.ResourceName) || _laubStoffe.ContainsKey(stoff)) return;
        _laubStoffe[stoff] = stoff.AlbedoColor;
        LaubFaerben(stoff, _laubStoffe[stoff]);
    }

    /// <summary>Wie <see cref="LaubMelden(BaseMaterial3D)"/> für alle Flächen eines Netzes.</summary>
    private void LaubMelden(Mesh netz)
    {
        for (var i = 0; i < netz.GetSurfaceCount(); i++)
            if (netz.SurfaceGetMaterial(i) is BaseMaterial3D stoff) LaubMelden(stoff);
    }

    private void LaubFaerben(BaseMaterial3D stoff, Color sommer) =>
        stoff.AlbedoColor = _jahreszeit switch
        {
            Jahreszeit.Herbst => Herbstlaub[stoff.ResourceName],
            Jahreszeit.Winter => Winterlaub[stoff.ResourceName],
            _ => sommer,
        };

    // ═════════════════════════════════════════════════════════════════════════
    //  Kronen im Winter
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die Laubkronen („Laubkrone_*“) und kahlen Äste („Winteraeste_*“) der eingesetzten Modelle: Beide sind Kinder des Knotens, in dem der Baum steht
    /// (Stufe, Haus, Wald), und folgen so dessen Sichtbarkeit. Im Winter zeigt das Spiel die Äste statt der Krone.
    /// </summary>
    private readonly List<(MeshInstance3D Knoten, bool Aeste)> _kronen = new();
    private int _kronenSchwelle = 64;

    /// <summary>
    /// Meldet alle Kronen und Winteräste unterhalb von <paramref name="wurzel"/> an und stellt sie auf die gerade geltende Jahreszeit ein – auch Modelle,
    /// die erst später gebaut werden, haben so sofort den richtigen Stand. Die Laubmaterialien der Kronen melden sich dabei mit.
    /// </summary>
    private void KronenMelden(Node wurzel)
    {
        var winter = _jahreszeit == Jahreszeit.Winter;
        foreach (var teil in wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            var name = teil.Name.ToString();
            var aeste = name.StartsWith("Winteraeste_");
            if (!aeste && !name.StartsWith("Laubkrone_")) continue;
            if (_kronen.Count >= _kronenSchwelle) // abgerissene und ersetzte Häuser nehmen ihre Kronen mit
            {
                _kronen.RemoveAll(k => !IsInstanceValid(k.Knoten));
                _kronenSchwelle = Math.Max(64, _kronen.Count * 2);
            }
            _kronen.Add((teil, aeste));
            teil.Visible = aeste == winter;
            if (teil.Mesh is { } netz) LaubMelden(netz);
        }
    }

    /// <summary>
    /// Stellt die Jahreszeit ein (jedes Bild gefragt, angefasst wird nur beim Wechsel). Frühling und Sommer zeigen das Laub, wie die Modelle es
    /// mitbringen, Herbst gelb, orange und braun. Im Winter zeigen Bäume und Büsche der Natur ihre kahlen Fassungen („…_Winter“), in den Modellen
    /// wechseln „Laubkrone_*“ zu „Winteraeste_*“, und das übrige Laub (Hecken, Beete, Weide) verdorrt zu Stroh und Braun.
    /// </summary>
    private void JahreszeitSetzen(Jahreszeit jahreszeit)
    {
        if (jahreszeit == _jahreszeit) return;
        _jahreszeit = jahreszeit;
        foreach (var (stoff, sommer) in _laubStoffe) LaubFaerben(stoff, sommer);

        var winter = jahreszeit == Jahreszeit.Winter;
        foreach (var (vielfach, sommerNetz, winterNetz) in _naturWinter) vielfach.Mesh = winter ? winterNetz : sommerNetz;
        _kronen.RemoveAll(k => !IsInstanceValid(k.Knoten));
        foreach (var (knoten, aeste) in _kronen) knoten.Visible = aeste == winter;
    }
}
